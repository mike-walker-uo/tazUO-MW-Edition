// TazUO addition: non-blocking MP3 internet-radio playback.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework.Audio;
using MP3Sharp;

namespace ClassicUO.IO.Audio
{
    public enum RadioStreamState
    {
        Stopped,
        Connecting,
        Buffering,
        Playing,
        Reconnecting
    }

    public sealed class UORadioMusic : Sound
    {
        private const int PCM_BUFFER_SIZE = 0x8000;
        private const int START_BUFFER_COUNT = 3;
        private const int MAX_BUFFER_COUNT = 12;
        private const int RECONNECT_DELAY_MS = 3000;

        private static readonly byte[] _silenceBuffer = new byte[PCM_BUFFER_SIZE];

        private readonly string[] _streamUrls;
        private readonly ConcurrentQueue<byte[]> _buffers = new ConcurrentQueue<byte[]>();
        private readonly object _sync = new object();
        private CancellationTokenSource _cancellation;
        private HttpWebRequest _request;
        private volatile bool _audioPlaying;
        private volatile bool _connected;
        private volatile RadioStreamState _state;
        private volatile string _activeStreamUrl = string.Empty;
        private volatile string _currentTitle = string.Empty;
        private volatile string _description = string.Empty;
        private volatile string _genre = string.Empty;
        private volatile string _stationName;
        private volatile int _bitrateKbps;
        private int _bufferCount;
        private int _generation;

        public UORadioMusic(string name, params string[] streamUrls) : base(name, -1)
        {
            _streamUrls = streamUrls ?? throw new ArgumentNullException(nameof(streamUrls));
            Channels = AudioChannels.Stereo;
            Delay = 0;
            Frequency = 44100;
            _state = RadioStreamState.Stopped;
            _stationName = name;
        }

        public string ActiveStreamUrl => _activeStreamUrl;
        public int BitrateKbps => _bitrateKbps;
        public string CurrentTitle => _currentTitle;
        public string Description => _description;
        public string Genre => _genre;
        public bool IsAudioPlaying => _audioPlaying;
        public string StationName => _stationName;
        public RadioStreamState StreamState => _state;

        public void StartConnecting()
        {
            StopWorker();
            ClearBuffers();

            CancellationTokenSource cancellation = new CancellationTokenSource();
            int generation;

            lock (_sync)
            {
                generation = ++_generation;
                _cancellation = cancellation;
                _connected = false;
                _state = RadioStreamState.Connecting;
            }

            _ = Task.Run(() => StreamLoop(generation, cancellation));
        }

        public void TryStartPlayback(uint currentTime, float volume)
        {
            if (!_audioPlaying && Volatile.Read(ref _bufferCount) >= START_BUFFER_COUNT)
            {
                Play(currentTime, volume);
            }
        }

        public void Update()
        {
            if (!_audioPlaying)
            {
                return;
            }

            OnBufferNeeded(null, null);

            if (_connected)
            {
                _state = Volatile.Read(ref _bufferCount) > 0
                    ? RadioStreamState.Playing
                    : RadioStreamState.Buffering;
            }
        }

        protected override byte[] GetBuffer()
        {
            if (_buffers.TryDequeue(out byte[] buffer))
            {
                Interlocked.Decrement(ref _bufferCount);
                return buffer;
            }

            return _silenceBuffer;
        }

        protected override void OnBufferNeeded(object sender, EventArgs e)
        {
            if (!_audioPlaying || SoundInstance == null || SoundInstance.IsDisposed)
            {
                return;
            }

            while (SoundInstance.PendingBufferCount < START_BUFFER_COUNT)
            {
                SoundInstance.SubmitBuffer(GetBuffer());
            }
        }

        protected override void BeforePlay()
        {
            _audioPlaying = true;
            _state = RadioStreamState.Playing;
        }

        protected override void AfterStop()
        {
            _audioPlaying = false;
            StopWorker();
            ClearBuffers();
            _state = RadioStreamState.Stopped;
        }

        private void StreamLoop(int generation, CancellationTokenSource cancellation)
        {
            CancellationToken token = cancellation.Token;
            bool firstAttempt = true;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    for (int i = 0; i < _streamUrls.Length && !token.IsCancellationRequested; i++)
                    {
                        SetState(generation, firstAttempt ? RadioStreamState.Connecting : RadioStreamState.Reconnecting);
                        firstAttempt = false;

                        try
                        {
                            StreamEndpoint(generation, _streamUrls[i], token);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            if (!token.IsCancellationRequested)
                            {
                                Log.Warn($"Radio '{Name}' endpoint failed ({_streamUrls[i]}): {ex.Message}");
                            }
                        }
                    }

                    if (!token.IsCancellationRequested && token.WaitHandle.WaitOne(RECONNECT_DELAY_MS))
                    {
                        return;
                    }
                }
            }
            finally
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_cancellation, cancellation))
                    {
                        _cancellation = null;
                    }
                }

                cancellation.Dispose();
            }
        }

        private void StreamEndpoint(int generation, string url, CancellationToken token)
        {
            HttpWebRequest request = WebRequest.CreateHttp(url);
            request.Accept = "audio/mpeg, audio/*";
            request.AllowAutoRedirect = true;
            request.KeepAlive = true;
            request.ReadWriteTimeout = 15000;
            request.Timeout = 10000;
            request.UserAgent = "TazUO Internet Radio";
            request.Headers["Icy-MetaData"] = "1";

            lock (_sync)
            {
                if (generation != _generation || token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(token);
                }

                _request = request;
            }

            try
            {
                using (WebResponse response = request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                {
                    UpdateStreamInformation(generation, url, response);
                    Stream audioStream = responseStream;

                    if (int.TryParse(response.Headers["icy-metaint"], out int metadataInterval) && metadataInterval > 0)
                    {
                        audioStream = new IcyMetadataStream(responseStream, metadataInterval, UpdateCurrentTitle);
                    }

                    using (MP3Stream decoder = new MP3Stream(audioStream, PCM_BUFFER_SIZE))
                    {
                        if (decoder.Frequency <= 0)
                        {
                            throw new InvalidDataException("The radio stream did not provide a valid MP3 sample rate.");
                        }

                        Frequency = decoder.Frequency;
                        SetConnected(generation, true);
                        SetState(generation, RadioStreamState.Buffering);

                        while (!token.IsCancellationRequested)
                        {
                            while (Volatile.Read(ref _bufferCount) >= MAX_BUFFER_COUNT)
                            {
                                if (token.WaitHandle.WaitOne(25))
                                {
                                    return;
                                }
                            }

                            byte[] buffer = new byte[PCM_BUFFER_SIZE];
                            int bytesRead = decoder.Read(buffer, 0, buffer.Length);

                            if (bytesRead <= 0)
                            {
                                throw new EndOfStreamException("The radio stream ended.");
                            }

                            if (bytesRead != buffer.Length)
                            {
                                Array.Resize(ref buffer, bytesRead);
                            }

                            lock (_sync)
                            {
                                if (generation != _generation || token.IsCancellationRequested)
                                {
                                    return;
                                }

                                _buffers.Enqueue(buffer);
                                Interlocked.Increment(ref _bufferCount);
                            }
                        }
                    }
                }
            }
            finally
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_request, request))
                    {
                        _request = null;
                    }
                }

                SetConnected(generation, false);
            }
        }

        private void StopWorker()
        {
            CancellationTokenSource cancellation;
            HttpWebRequest request;

            lock (_sync)
            {
                _generation++;
                cancellation = _cancellation;
                request = _request;
                _cancellation = null;
                _request = null;
                _connected = false;
            }

            cancellation?.Cancel();

            try
            {
                request?.Abort();
            }
            catch (WebException)
            {
            }
        }

        private void ClearBuffers()
        {
            while (_buffers.TryDequeue(out _))
            {
            }

            Interlocked.Exchange(ref _bufferCount, 0);
        }

        private void UpdateCurrentTitle(string title)
        {
            _currentTitle = title ?? string.Empty;
        }

        private void UpdateStreamInformation(int generation, string url, WebResponse response)
        {
            lock (_sync)
            {
                if (generation != _generation)
                {
                    return;
                }

                _activeStreamUrl = url;
                _stationName = GetHeader(response, "icy-name", Name);
                _description = GetHeader(response, "icy-description", string.Empty);
                _genre = GetHeader(response, "icy-genre", string.Empty);

                if (!int.TryParse(response.Headers["icy-br"], out int bitrateKbps))
                {
                    bitrateKbps = 0;
                }

                _bitrateKbps = bitrateKbps;
            }
        }

        private static string GetHeader(WebResponse response, string name, string fallback)
        {
            string value = response?.Headers?[name];
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private void SetConnected(int generation, bool connected)
        {
            lock (_sync)
            {
                if (generation == _generation)
                {
                    _connected = connected;
                }
            }
        }

        private void SetState(int generation, RadioStreamState state)
        {
            lock (_sync)
            {
                if (generation == _generation)
                {
                    _state = state;
                }
            }
        }

        private sealed class IcyMetadataStream : Stream
        {
            private readonly Action<string> _titleChanged;
            private readonly int _metadataInterval;
            private readonly Stream _source;
            private int _audioBytesRemaining;
            private bool _endOfStream;

            public IcyMetadataStream(Stream source, int metadataInterval, Action<string> titleChanged)
            {
                _source = source ?? throw new ArgumentNullException(nameof(source));
                _metadataInterval = metadataInterval;
                _audioBytesRemaining = metadataInterval;
                _titleChanged = titleChanged;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_endOfStream || count == 0)
                {
                    return 0;
                }

                if (_audioBytesRemaining == 0 && !ReadMetadata())
                {
                    return 0;
                }

                int bytesRead = _source.Read(buffer, offset, Math.Min(count, _audioBytesRemaining));

                if (bytesRead <= 0)
                {
                    _endOfStream = true;
                    return 0;
                }

                _audioBytesRemaining -= bytesRead;
                return bytesRead;
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _source.Dispose();
                }

                base.Dispose(disposing);
            }

            private bool ReadMetadata()
            {
                int lengthByte = _source.ReadByte();

                if (lengthByte < 0)
                {
                    _endOfStream = true;
                    return false;
                }

                int metadataLength = lengthByte * 16;

                if (metadataLength > 0)
                {
                    byte[] metadata = new byte[metadataLength];
                    int offset = 0;

                    while (offset < metadata.Length)
                    {
                        int bytesRead = _source.Read(metadata, offset, metadata.Length - offset);

                        if (bytesRead <= 0)
                        {
                            _endOfStream = true;
                            return false;
                        }

                        offset += bytesRead;
                    }

                    string value = DecodeMetadata(metadata);
                    string title = GetMetadataValue(value, "StreamTitle");

                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        _titleChanged?.Invoke(title.Trim());
                    }
                }

                _audioBytesRemaining = _metadataInterval;
                return true;
            }

            private static string DecodeMetadata(byte[] metadata)
            {
                char[] characters = new char[metadata.Length];
                int length = 0;

                for (int i = 0; i < metadata.Length && metadata[i] != 0; i++)
                {
                    characters[length++] = (char)metadata[i];
                }

                return new string(characters, 0, length);
            }

            private static string GetMetadataValue(string metadata, string key)
            {
                string prefix = key + "='";
                int start = metadata.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);

                if (start < 0)
                {
                    return string.Empty;
                }

                start += prefix.Length;
                int end = metadata.IndexOf("';", start, StringComparison.Ordinal);

                if (end < 0)
                {
                    end = metadata.IndexOf('\'', start);
                }

                return end < 0 ? string.Empty : metadata.Substring(start, end - start);
            }
        }
    }
}
