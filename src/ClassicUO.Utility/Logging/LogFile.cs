#region license

// Copyright (c) 2021, andreakarasho
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ClassicUO.Utility.Logging
{
    public sealed class LogFile : IDisposable
    {
        private readonly FileStream logStream;
        private readonly long _maximumLength;
        private readonly object _syncObject = new object();

        public LogFile(string directory, string file, long maximumLength = 0)
        {
            Directory.CreateDirectory(directory);
            _maximumLength = maximumLength;
            logStream = new FileStream
            (
                $"{directory}/{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}_{file}",
                maximumLength > 0 ? FileMode.Create : FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite,
                4096,
                true
            );
        }

        public void Dispose()
        {
            lock (_syncObject)
            {
                logStream.Dispose();
            }
        }


        public void Write(string message, bool flush = true)
        {
            const string TRUNCATED_MESSAGE = "[Oversized log entry truncated to its most recent text.]\n";
            int byteCount = Encoding.UTF8.GetByteCount(message);

            if (_maximumLength > 0 && byteCount + 1 > _maximumLength)
            {
                int markerBytes = Encoding.UTF8.GetByteCount(TRUNCATED_MESSAGE);
                string prefix = markerBytes + 1 <= _maximumLength ? TRUNCATED_MESSAGE : string.Empty;
                int prefixBytes = prefix.Length == 0 ? 0 : markerBytes;
                int byteBudget = (int)Math.Max(0, Math.Min(int.MaxValue, _maximumLength - prefixBytes - 1));
                char[] characters = message.ToCharArray();
                int low = 0;
                int high = characters.Length;

                while (low < high)
                {
                    int middle = low + (high - low) / 2;
                    if (Encoding.UTF8.GetByteCount(characters, middle, characters.Length - middle) > byteBudget)
                    {
                        low = middle + 1;
                    }
                    else
                    {
                        high = middle;
                    }
                }

                message = prefix + message.Substring(low);
                byteCount = Encoding.UTF8.GetByteCount(message);
            }

            byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(byteCount);

            try
            {
                int bytesWritten = Encoding.UTF8.GetBytes(message, 0, message.Length, buffer, 0);

                lock (_syncObject)
                {
                    if (_maximumLength > 0 && logStream.Length + bytesWritten + 1 > _maximumLength)
                    {
                        logStream.SetLength(0);
                        logStream.Position = 0;
                        byte[] marker = Encoding.UTF8.GetBytes("[Session log restarted after reaching its size limit.]\n");
                        if (marker.Length + bytesWritten + 1 <= _maximumLength)
                        {
                            logStream.Write(marker, 0, marker.Length);
                        }
                    }

                    logStream.Write(buffer, 0, bytesWritten);
                    logStream.WriteByte((byte) '\n');
                    if (flush)
                    {
                        logStream.Flush();
                    }
                }
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public async Task WriteAsync(string message)
        {
            int byteCount = Encoding.UTF8.GetByteCount(message);
            byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(byteCount);

            try
            {
                int bytesWritten = Encoding.UTF8.GetBytes(message, 0, message.Length, buffer, 0);

                // Async writes are serialized by callers; LogFile.Write is used by the
                // background session-log writer when concurrent producers are possible.
                if (_maximumLength > 0 && logStream.Length + bytesWritten + 1 > _maximumLength)
                {
                    return;
                }

                await logStream.WriteAsync(buffer, 0, bytesWritten);
                logStream.WriteByte((byte) '\n');
                await logStream.FlushAsync();
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public void Flush()
        {
            lock (_syncObject)
            {
                logStream.Flush();
            }
        }


        public override string ToString()
        {
            return logStream.Name;
        }
    }
}
