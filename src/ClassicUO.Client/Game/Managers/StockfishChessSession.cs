// A single local Stockfish process shared by the chess gump's UCI requests.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ClassicUO.Game.Managers
{
    internal sealed class ChessPositionInfo
    {
        internal readonly HashSet<string> LegalMoves = new HashSet<string>(StringComparer.Ordinal);
        internal string Fen;
        internal bool InCheck;
    }

    internal sealed class StockfishChessSession : IDisposable
    {
        private readonly object _gate = new object();
        private readonly string _path;
        private Process _process;
        private volatile bool _disposed;

        internal StockfishChessSession(string path)
        {
            _path = path;
        }

        internal Task<ChessPositionInfo> InspectAsync(IReadOnlyList<string> moves)
        {
            string history = PositionCommand(moves);
            return Task.Run(() =>
            {
                lock (_gate)
                {
                    EnsureStarted();
                    Send(history);
                    Send("go perft 1");
                    var info = new ChessPositionInfo();
                    string line;
                    while ((line = ReadLine()) != null)
                    {
                        if (line.StartsWith("Nodes searched:", StringComparison.Ordinal))
                            break;
                        int separator = line.IndexOf(':');
                        if (separator >= 4 && separator <= 5)
                        {
                            string move = line.Substring(0, separator).Trim();
                            if (move.Length == 4 || move.Length == 5)
                                info.LegalMoves.Add(move);
                        }
                    }

                    // Stockfish's debug board reports FEN and whether the side
                    // to move is in check; used to name mate vs stalemate.
                    Send("d");
                    Send("isready");
                    while ((line = ReadLine()) != "readyok")
                    {
                        if (line.StartsWith("Fen: ", StringComparison.Ordinal))
                            info.Fen = line.Substring(5);
                        else if (line.StartsWith("Checkers: ", StringComparison.Ordinal))
                            info.InCheck = line.Length > 10;
                    }
                    return info;
                }
            });
        }

        internal Task<string> BestMoveAsync(IReadOnlyList<string> moves, int skill, int milliseconds)
        {
            string history = PositionCommand(moves);
            return Task.Run(() =>
            {
                lock (_gate)
                {
                    EnsureStarted();
                    Send($"setoption name Skill Level value {skill}");
                    Send(history);
                    Send($"go movetime {milliseconds}");
                    string line;
                    while ((line = ReadLine()) != null)
                    {
                        if (line.StartsWith("bestmove ", StringComparison.Ordinal))
                            return line.Split(' ')[1];
                    }
                    throw new IOException("Stockfish stopped before returning a move.");
                }
            });
        }

        public void Dispose()
        {
            _disposed = true;
            Process process = _process;
            if (process == null)
                return;
            try
            {
                if (!process.HasExited)
                {
                    process.StandardInput.WriteLine("quit");
                    if (!process.WaitForExit(300))
                        process.Kill();
                }
            }
            catch (InvalidOperationException) { }
            catch (IOException) { }
            finally
            {
                process.Dispose();
            }
        }

        private static string PositionCommand(IReadOnlyList<string> moves)
        {
            return moves.Count == 0
                ? "position startpos"
                : "position startpos moves " + string.Join(" ", moves);
        }

        private void EnsureStarted()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StockfishChessSession));
            if (_process != null)
                return;
            if (!File.Exists(_path))
                throw new FileNotFoundException("Stockfish executable not found. Put stockfish.exe beside the client or use -chess path <file>.", _path);

            var start = new ProcessStartInfo(_path)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                WorkingDirectory = Path.GetDirectoryName(_path)
            };
            _process = Process.Start(start);
            if (_process == null)
                throw new IOException("Stockfish could not be started.");
            if (_disposed)
            {
                _process.Kill();
                throw new ObjectDisposedException(nameof(StockfishChessSession));
            }
            Send("uci");
            while (ReadLine() != "uciok") { }
            Send("setoption name Threads value 1");
            Send("setoption name Hash value 16");
            Send("isready");
            while (ReadLine() != "readyok") { }
        }

        private void Send(string command)
        {
            _process.StandardInput.WriteLine(command);
            _process.StandardInput.Flush();
        }

        private string ReadLine()
        {
            Task<string> pending = _process.StandardOutput.ReadLineAsync();
            if (!pending.Wait(TimeSpan.FromSeconds(15)))
                throw new TimeoutException("Stockfish did not respond within 15 seconds.");
            string line = pending.Result;
            if (line == null)
                throw new IOException("Stockfish closed its output stream.");
            return line;
        }
    }
}
