using ClassicUO.Network.Encryption;
using ClassicUO.Game.Managers;
using ClassicUO.Utility.Logging;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.IO;
using System.Buffers;
using System.Diagnostics;
using SDL3;

namespace ClassicUO.Network
{
    delegate void DataReceivedEventHandler(object sender, byte[] buffer, int offset, int count);

    sealed class AsyncSocketWrapper : IDisposable
    {
        private const int CONNECT_TIMEOUT_MS = 15_000;

        private TcpClient _socket;
        private NetworkStream _stream;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _receiveTask;
        private int _disposed;
        public bool IsConnected => _socket?.Client?.Connected ?? false;
        public EndPoint LocalEndPoint => _socket?.Client?.LocalEndPoint;

        public event EventHandler OnConnected, OnDisconnected;
        public event EventHandler<SocketError> OnError;
        // buffer is borrowed and valid only for the synchronous callback.
        public event DataReceivedEventHandler OnDataReceived;

        public async Task<bool> ConnectAsync(string ip, int port, CancellationToken cancellationToken = default)
        {
            if (IsConnected)
                return true;

            try
            {
                _socket = new TcpClient();
                _socket.NoDelay = true;
                _cancellationTokenSource = new CancellationTokenSource();

                Task connectTask = _socket.ConnectAsync(ip, port);
                Task timeoutTask = Task.Delay(CONNECT_TIMEOUT_MS, cancellationToken);

                if (await Task.WhenAny(connectTask, timeoutTask) != connectTask)
                {
                    CloseSocket();

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Log.Error($"Connection to {ip}:{port} timed out.");
                        OnError?.Invoke(this, SocketError.TimedOut);
                    }

                    return false;
                }

                await connectTask;

                if (!IsConnected)
                {
                    OnError?.Invoke(this, SocketError.NotConnected);

                    return false;
                }

                _stream = _socket.GetStream();

                // Start background receive task
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

                OnConnected?.Invoke(this, EventArgs.Empty);

                return true;
            }
            catch (SocketException) when (
                cancellationToken.IsCancellationRequested
                || _cancellationTokenSource?.IsCancellationRequested == true
            )
            {
                return false;
            }
            catch (ObjectDisposedException) when (
                cancellationToken.IsCancellationRequested
                || _cancellationTokenSource?.IsCancellationRequested == true
            )
            {
                return false;
            }
            catch (SocketException socketEx)
            {
                Log.Error($"Error while connecting {socketEx}");
                OnError?.Invoke(this, socketEx.SocketErrorCode);

                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"Error while connecting {ex}");
                OnError?.Invoke(this, SocketError.SocketError);

                return false;
            }
        }

        public async Task SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            if (!IsConnected || _stream == null)
                return;

            try
            {
                await _stream.WriteAsync(buffer, offset, count, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Log.Error($"Error while sending {ex}");
                CloseSocket();
                OnError?.Invoke(this, SocketError.SocketError);
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(4096);

            try
            {
                while (!cancellationToken.IsCancellationRequested && IsConnected)
                {
                    int bytesRead = await _stream.ReadAsync(
                        buffer,
                        0,
                        buffer.Length,
                        cancellationToken
                    );

                    if (bytesRead == 0)
                    {
                        OnDisconnected?.Invoke(this, EventArgs.Empty);
                        CloseSocket();

                        break;
                    }

                    if (bytesRead > 0 && !cancellationToken.IsCancellationRequested)
                    {
                        OnDataReceived?.Invoke(this, buffer, 0, bytesRead);
                    }
                }
            }
            catch (IOException) when (cancellationToken.IsCancellationRequested)
            {
                CloseSocket();
            }
            catch (IOException ioEx) when (ioEx.InnerException is SocketException socketEx)
            {
                CloseSocket();

                switch (socketEx.SocketErrorCode)
                {
                    case SocketError.OperationAborted: break;
                    default:
                        Log.Error($"Socket error in receive loop: {socketEx.SocketErrorCode} - {socketEx.Message}");
                        OnError?.Invoke(this, socketEx.SocketErrorCode); break;
                }

            }
            catch (OperationCanceledException)
            {
                CloseSocket();
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                CloseSocket();
            }
            catch (Exception ex)
            {
                Log.Error($"Error in receive loop {ex}");
                CloseSocket();
                OnError?.Invoke(this, SocketError.SocketError);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public void Disconnect()
        {
            CloseSocket();
        }

        public async Task WaitForReceiveCompletionAsync()
        {
            Task receiveTask = _receiveTask;

            if (receiveTask != null && !receiveTask.IsCompleted)
            {
                try
                {
                    await receiveTask;
                }
                catch
                {
                }
            }
        }

        private void CloseSocket()
        {
            _cancellationTokenSource?.Cancel();
            _stream?.Close();
            _socket?.Close();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            CloseSocket();
            _stream?.Dispose();
            _socket?.Dispose();

            CancellationTokenSource cancellation = _cancellationTokenSource;
            Task receiveTask = _receiveTask;
            if (receiveTask == null || receiveTask.IsCompleted)
            {
                cancellation?.Dispose();
            }
            else
            {
                receiveTask.ContinueWith(
                    _ => cancellation?.Dispose(),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default
                );
            }
        }
    }

    internal sealed class AsyncNetClient : IDisposable
    {
        private const int BUFF_SIZE = 0x10000;
        private const int MAX_INCOMING_MESSAGES = 8192;
        private const long MAX_INCOMING_BYTES = 32L * 1024 * 1024;

        private readonly byte[] _uncompressedBuffer = new byte[BUFF_SIZE];
        private readonly byte[] _sendingBuffer = new byte[4096];
        private readonly Huffman _huffman = new Huffman();
        private volatile bool _isCompressionEnabled;
        private readonly AsyncSocketWrapper _socket;
        private uint? _localIP;
        private readonly CircularBuffer _sendStream;
        private readonly SemaphoreSlim _sendSignal = new SemaphoreSlim(0, 1);
        private readonly ConcurrentQueue<IncomingMessage> _incomingMessages = new();
        private readonly object _receiveSync = new object();
        private readonly object _lifecycleSync = new object();
        private Task _networkTask;
        private Task _disconnectTask;
        private CancellationTokenSource _cancellationTokenSource;
        private int _disposed;
        private int _protocolDisconnectPending;
        private int _sessionGeneration;
        private int _incomingMessageCount;
        private long _incomingBytes;

        private readonly struct IncomingMessage
        {
            public IncomingMessage(byte[] data)
            {
                Data = data;
                EnqueuedAt = Stopwatch.GetTimestamp();
            }

            public byte[] Data { get; }
            public long EnqueuedAt { get; }
        }

        public AsyncNetClient()
        {
            Statistics = new NetStatistics(this);
            _sendStream = new CircularBuffer();

            _socket = new AsyncSocketWrapper();

            _socket.OnConnected += (o, e) =>
            {
                Statistics.Reset();
                MainThreadQueue.EnqueueAction(() => Connected?.Invoke(this, EventArgs.Empty));
            };

            _socket.OnDisconnected += (o, e) => RaiseDisconnected(SocketError.Success);
            _socket.OnError += (o, e) => RaiseDisconnected(e);
            _socket.OnDataReceived += OnDataReceived;
        }

        public static AsyncNetClient Socket { get; set; } = new AsyncNetClient();

        public bool IsConnected => _socket != null && _socket.IsConnected;
        public NetStatistics Statistics { get; }

        public uint LocalIP
        {
            get
            {
                if (!_localIP.HasValue)
                {
                    try
                    {
                        byte[] addressBytes = (_socket?.LocalEndPoint as IPEndPoint)?.Address.MapToIPv4().GetAddressBytes();

                        if (addressBytes != null && addressBytes.Length != 0)
                        {
                            _localIP = (uint)(addressBytes[0] | (addressBytes[1] << 8) | (addressBytes[2] << 16) | (addressBytes[3] << 24));
                        }

                        if (!_localIP.HasValue || _localIP == 0)
                        {
                            _localIP = 0x100007f;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"error while retrieving local endpoint address: \n{ex}");
                        _localIP = 0x100007f;
                    }
                }

                return _localIP.Value;
            }
        }

        public event EventHandler Connected;
        public event EventHandler<SocketError> Disconnected;

        private void RaiseDisconnected(SocketError error)
        {
            MainThreadQueue.EnqueueAction(() => Disconnected?.Invoke(this, error));
        }

        public async Task<bool> Connect(string ip, ushort port, CancellationToken cancellationToken = new ())
        {
            if (Volatile.Read(ref _disposed) != 0)
                return false;

            Task previousDisconnect;
            lock (_lifecycleSync)
            {
                previousDisconnect = _disconnectTask;
            }

            if (previousDisconnect != null)
                await previousDisconnect;

            lock (_lifecycleSync)
            {
                if (_disconnectTask == previousDisconnect)
                    _disconnectTask = null;
            }

            Interlocked.Increment(ref _sessionGeneration);
            Interlocked.Exchange(ref _protocolDisconnectPending, 0);

            lock (_sendStream)
                _sendStream.Clear();

            lock (_receiveSync)
            {
                _huffman.Reset();
                _isCompressionEnabled = false;
            }

            ClearIncomingMessages();
            PacketHandlers.Handler.Reset();
            Statistics.Reset();

            var success = await _socket.ConnectAsync(ip, port, cancellationToken);

            if (success)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _networkTask = Task.Run(() => NetworkLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            }

            return success;
        }

        public Task Disconnect()
        {
            if (Volatile.Read(ref _disposed) != 0)
                return Task.CompletedTask;

            lock (_lifecycleSync)
            {
                return _disconnectTask ??= DisconnectCoreAsync();
            }
        }

        private async Task DisconnectCoreAsync()
        {
            SDL.SDL_CaptureMouse(false);
            Statistics.Reset();

            _cancellationTokenSource?.Cancel();
            _socket.Disconnect();
            await _socket.WaitForReceiveCompletionAsync();

            if(_networkTask != null)
            {
                try
                {
                    await _networkTask;
                }
                catch { }
            }

            lock (_sendStream)
                _sendStream.Clear();

            lock (_receiveSync)
            {
                _isCompressionEnabled = false;
                _huffman.Reset();
            }

            ClearIncomingMessages();
            PacketHandlers.Handler.Reset();
        }

        public void EnableCompression()
        {
            lock (_receiveSync)
            {
                _isCompressionEnabled = true;
                _huffman.Reset();
            }

            lock (_sendStream)
                _sendStream.Clear();
        }

        private async Task NetworkLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && IsConnected)
            {
                try
                {
                    // Send immediately when signaled; wake twice per second
                    // while idle so the statistics snapshot stays current.
                    await _sendSignal.WaitAsync(500, cancellationToken);
                    await ProcessSendAsync(cancellationToken);

                    Statistics.Update();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _cancellationTokenSource?.Cancel();
                    _socket.Disconnect();
                    Log.Error($"Network loop error: {ex}");
                    RaiseDisconnected(SocketError.SocketError);
                    break;
                }
            }
        }

        private void OnDataReceived(object sender, byte[] buffer, int offset, int count)
        {
            bool decompressionFailed = false;
            int sessionGeneration = Volatile.Read(ref _sessionGeneration);

            try
            {
                lock (_receiveSync)
                {
                    if (!IsConnected || _cancellationTokenSource?.IsCancellationRequested == true)
                        return;

                    Statistics.TotalBytesReceived += (uint)count;

                    var span = buffer.AsSpan(offset, count);
                    ProcessEncryption(span);
                    byte[] message;

                    if (_isCompressionEnabled)
                    {
                        int size = _uncompressedBuffer.Length;

                        if (!_huffman.Decompress(span, _uncompressedBuffer, ref size))
                        {
                            decompressionFailed = true;
                            message = null;
                        }
                        else if (size == 0)
                        {
                            message = null;
                        }
                        else
                        {
                            message = _uncompressedBuffer.AsSpan(0, size).ToArray();
                        }
                    }
                    else
                    {
                        message = span.ToArray();
                    }

                    if (message != null)
                    {
                        int queuedMessages = Interlocked.Increment(ref _incomingMessageCount);
                        long queuedBytes = Interlocked.Add(ref _incomingBytes, message.Length);
                        _incomingMessages.Enqueue(new IncomingMessage(message));

                        if ((queuedMessages > MAX_INCOMING_MESSAGES || queuedBytes > MAX_INCOMING_BYTES)
                            && Interlocked.CompareExchange(ref _protocolDisconnectPending, 1, 0) == 0)
                        {
                            Log.Error($"Incoming network queue limit exceeded: messages={queuedMessages}, bytes={queuedBytes}.");
                            MainThreadQueue.EnqueueAction(
                                () => DisconnectForProtocolErrorCore(sessionGeneration)
                            );
                        }
                    }
                }

                if (decompressionFailed)
                {
                    Log.Error("Invalid compressed packet stream received from server.");

                    if (Interlocked.CompareExchange(ref _protocolDisconnectPending, 1, 0) == 0)
                    {
                        MainThreadQueue.EnqueueAction(
                            () => DisconnectForProtocolErrorCore(sessionGeneration)
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error processing received data: {ex}");
            }
        }

        public bool TryDequeuePacket(out byte[] packet)
        {
            if (_incomingMessages.TryDequeue(out IncomingMessage message))
            {
                packet = message.Data;
                Interlocked.Decrement(ref _incomingMessageCount);
                Interlocked.Add(ref _incomingBytes, -message.Data.Length);
                return true;
            }

            packet = null;
            return false;
        }

        public int IncomingMessageCount => Volatile.Read(ref _incomingMessageCount);
        public long IncomingBytes => Interlocked.Read(ref _incomingBytes);

        public long OldestIncomingMessageAgeMilliseconds
        {
            get
            {
                if (!_incomingMessages.TryPeek(out IncomingMessage message))
                    return 0;

                long elapsed = Stopwatch.GetTimestamp() - message.EnqueuedAt;
                return elapsed * 1000 / Stopwatch.Frequency;
            }
        }

        public void ClearIncomingMessages()
        {
            while (_incomingMessages.TryDequeue(out IncomingMessage message))
            {
                Interlocked.Decrement(ref _incomingMessageCount);
                Interlocked.Add(ref _incomingBytes, -message.Data.Length);
            }
        }

        internal void DisconnectForProtocolError()
        {
            if (Interlocked.CompareExchange(ref _protocolDisconnectPending, 1, 0) != 0)
                return;

            DisconnectForProtocolErrorCore(Volatile.Read(ref _sessionGeneration));
        }

        private void DisconnectForProtocolErrorCore(int sessionGeneration)
        {
            if (sessionGeneration != Volatile.Read(ref _sessionGeneration))
                return;

            RaiseDisconnected(SocketError.ProtocolNotSupported);
            _ = Disconnect();
        }

        public void Send(Span<byte> message, bool ignorePlugin = false, bool skipEncryption = false)
        {
            if (!IsConnected || message == null || message.Length == 0)
            {
                return;
            }

            if (!ignorePlugin && !Plugin.ProcessSendPacket(ref message))
            {
                return;
            }

            if (message.IsEmpty)
                return;

            PacketLogger.Default?.Log(message, true);

            if (!skipEncryption)
            {
                EncryptionHelper.Encrypt(!_isCompressionEnabled, message, message, message.Length);
            }

            lock (_sendStream)
            {
                _sendStream.Enqueue(message);

                if (_sendSignal.CurrentCount == 0)
                {
                    _sendSignal.Release();
                }
            }

            Statistics.TotalBytesSent += (uint)message.Length;
            Statistics.TotalPacketsSent++;
        }

        private void ProcessEncryption(Span<byte> buffer)
        {
            if (!_isCompressionEnabled)
                return;

            EncryptionHelper.Decrypt(buffer, buffer, buffer.Length);
        }

        private async Task ProcessSendAsync(CancellationToken cancellationToken)
        {
            if (!IsConnected)
                return;

            try
            {
                while (IsConnected)
                {
                    int bytesToSend;

                    lock (_sendStream)
                    {
                        int size = Math.Min(_sendingBuffer.Length, _sendStream.Length);
                        bytesToSend = _sendStream.Dequeue(_sendingBuffer, 0, size);
                    }

                    if (bytesToSend <= 0)
                    {
                        break;
                    }

                    await _socket.SendAsync(
                        _sendingBuffer,
                        0,
                        bytesToSend,
                        cancellationToken
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error in ProcessSendAsync: {ex}");
                throw;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _cancellationTokenSource?.Cancel();
            _socket?.Dispose();

            Task networkTask = _networkTask;
            if (networkTask == null || networkTask.IsCompleted)
            {
                DisposeSynchronizationObjects();
            }
            else
            {
                networkTask.ContinueWith(
                    _ => DisposeSynchronizationObjects(),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default
                );
            }
        }

        private void DisposeSynchronizationObjects()
        {
            _sendSignal.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}
