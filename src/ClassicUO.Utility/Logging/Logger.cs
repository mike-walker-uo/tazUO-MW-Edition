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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ClassicUO.Utility.Logging
{
    public class Logger
    {
        private static readonly Dictionary<LogTypes, Tuple<ConsoleColor, string>> _logTypesInfo = new Dictionary<LogTypes, Tuple<ConsoleColor, string>>
        {
            {
                LogTypes.None, Tuple.Create(ConsoleColor.White, "")
            },
            {
                LogTypes.Info, Tuple.Create(ConsoleColor.Green, "  Info    ")
            },
            {
                LogTypes.Debug, Tuple.Create(ConsoleColor.DarkMagenta, "  Debug   ")
            },
            {
                LogTypes.Trace, Tuple.Create(ConsoleColor.Green, "  Trace   ")
            },
            {
                LogTypes.Warning, Tuple.Create(ConsoleColor.Yellow, "  Warning ")
            },
            {
                LogTypes.Error, Tuple.Create(ConsoleColor.Red, "  Error   ")
            },
            {
                LogTypes.Panic, Tuple.Create(ConsoleColor.Red, "  Panic   ")
            }
        };

        private int _indent;

        private bool _isLogging;
        private readonly object _syncObject = new object();
        private BlockingCollection<PendingMessage> _pendingFileMessages;
        private BlockingCollection<PendingMessage> _pendingConsoleMessages;
        private Thread _fileWriterThread;
        private Thread _consoleWriterThread;
        private LogFile _logFile;

        private sealed class PendingMessage
        {
            public DateTime Timestamp;
            public LogTypes Type;
            public string Text;
            public string Indent;
            public string FileText;
            public bool Clear;
        }

        // No volatile support for properties, let's use a private backing field.
        public LogTypes LogTypes { get; set; }

        public void Start(LogFile logFile = null)
        {
            lock (_syncObject)
            {
                _isLogging = true;
                _logFile = logFile;
                _pendingConsoleMessages = new BlockingCollection<PendingMessage>(2048);
                _consoleWriterThread = new Thread(WriteConsole)
                {
                    IsBackground = true,
                    Name = "CUO_CONSOLE_WRITER"
                };
                _consoleWriterThread.Start();

                if (logFile != null)
                {
                    _pendingFileMessages = new BlockingCollection<PendingMessage>(2048);
                    _fileWriterThread = new Thread(WriteLogFile)
                    {
                        IsBackground = true,
                        Name = "CUO_LOG_WRITER"
                    };
                    _fileWriterThread.Start();
                }
            }
        }

        public void Stop()
        {
            Thread fileWriter;
            Thread consoleWriter;
            lock (_syncObject)
            {
                _isLogging = false;
                _pendingFileMessages?.CompleteAdding();
                _pendingConsoleMessages?.CompleteAdding();
                fileWriter = _fileWriterThread;
                consoleWriter = _consoleWriterThread;
            }

            Stopwatch stopTimer = Stopwatch.StartNew();
            fileWriter?.Join(2000);
            int remaining = Math.Max(0, 2000 - (int) stopTimer.ElapsedMilliseconds);
            consoleWriter?.Join(remaining);
        }

        public void Message(LogTypes logType, string text)
        {
            lock (_syncObject)
            {
                SetLogger(logType, text);
            }
        }

        public void NewLine()
        {
            lock (_syncObject)
            {
                SetLogger(LogTypes.None, string.Empty);
            }
        }

        public void Clear()
        {
            lock (_syncObject)
            {
                if (_isLogging)
                {
                    EnqueueLatest(_pendingConsoleMessages, new PendingMessage { Clear = true });
                }
            }
        }

        public void PushIndent()
        {
            _indent++;
        }

        public void PopIndent()
        {
            _indent--;

            if (_indent < 0)
            {
                _indent = 0;
            }
        }

        private void SetLogger(LogTypes type, string text)
        {
            if (!_isLogging)
            {
                return;
            }

            if ((LogTypes & type) == type)
            {
                DateTime timestamp = DateTime.UtcNow;
                string prefix = type == LogTypes.None
                    ? string.Empty
                    : $"{timestamp:O} | {_logTypesInfo[type].Item2.Trim()} | ";
                string indent = _indent > 0 ? new string('\t', _indent * 2) : string.Empty;
                PendingMessage message = new PendingMessage
                {
                    Timestamp = timestamp,
                    Type = type,
                    Text = text,
                    Indent = indent,
                    FileText = prefix + indent + text
                };

                if (_pendingFileMessages != null)
                {
                    if (!_pendingFileMessages.TryAdd(message))
                    {
                        _pendingFileMessages.TryTake(out _);
                        message.FileText = "[Older queued log entry dropped.]\n" + message.FileText;
                        _pendingFileMessages.TryAdd(message);
                    }
                }

                EnqueueLatest(_pendingConsoleMessages, message);
            }
        }

        private static void EnqueueLatest(BlockingCollection<PendingMessage> queue, PendingMessage message)
        {
            if (queue == null || queue.TryAdd(message))
            {
                return;
            }

            queue.TryTake(out _);
            queue.TryAdd(message);
        }

        private void WriteLogFile()
        {
            Stopwatch flushTimer = Stopwatch.StartNew();

            try
            {
                while (!_pendingFileMessages.IsCompleted)
                {
                    if (_pendingFileMessages.TryTake(out PendingMessage message, 1000))
                    {
                        try { _logFile.Write(message.FileText, false); } catch { }
                    }

                    if (flushTimer.ElapsedMilliseconds >= 1000)
                    {
                        try { _logFile.Flush(); } catch { }
                        flushTimer.Restart();
                    }
                }
            }
            catch
            {
                // Logging must never terminate the client.
            }
            finally
            {
                try { _logFile.Dispose(); } catch { }
            }
        }

        private void WriteConsole()
        {
            while (!_pendingConsoleMessages.IsCompleted)
            {
                if (!_pendingConsoleMessages.TryTake(out PendingMessage message, 1000))
                {
                    continue;
                }

                try
                {
                    if (message.Clear)
                    {
                        Console.Clear();
                    }
                    else if (message.Type == LogTypes.None)
                    {
                        Console.Write(message.Indent);
                        Console.WriteLine(message.Text);
                    }
                    else
                    {
                        Console.Write(message.Timestamp);
                        Console.Write(" | ");
                        ConsoleColor previousColor = default;
                        bool colorChanged = false;
                        try
                        {
                            previousColor = Console.ForegroundColor;
                            Console.ForegroundColor = _logTypesInfo[message.Type].Item1;
                            colorChanged = true;
                        }
                        catch { }

                        Console.Write(_logTypesInfo[message.Type].Item2);

                        if (colorChanged)
                        {
                            try { Console.ForegroundColor = previousColor; } catch { }
                        }
                        Console.Write(" | ");
                        Console.Write(message.Indent);
                        Console.WriteLine(message.Text);
                    }
                }
                catch
                {
                    // Console availability must not affect file logging.
                }
            }
        }
    }
}
