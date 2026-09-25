// Client-side chess board. Stockfish supplies legal moves and computer replies.

using System;
using System.Collections.Generic;

namespace ClassicUO.Game.Managers
{
    internal sealed class ChessGame
    {
        private const string Opening = "rnbqkbnrpppppppp                                PPPPPPPPRNBQKBNR";
        private readonly char[] _squares = new char[64];
        private readonly List<string> _moves = new List<string>();

        internal ChessGame()
        {
            Reset();
        }

        internal IReadOnlyList<string> Moves => _moves;
        internal bool WhiteToMove => _moves.Count % 2 == 0;
        internal int LastFrom { get; private set; } = -1;
        internal int LastTo { get; private set; } = -1;

        internal char PieceAt(int square) => _squares[square];

        internal void Reset()
        {
            Opening.CopyTo(0, _squares, 0, 64);
            _moves.Clear();
            LastFrom = LastTo = -1;
        }

        internal void Undo(int plies)
        {
            int keep = Math.Max(0, _moves.Count - plies);
            string[] retained = new string[keep];
            _moves.CopyTo(0, retained, 0, keep);
            Reset();
            foreach (string move in retained)
                Apply(move);
        }

        internal void Apply(string move)
        {
            if (move == null || move.Length < 4)
                throw new ArgumentException("Invalid chess move.", nameof(move));

            int from = Square(move, 0);
            int to = Square(move, 2);
            char piece = _squares[from];
            if (piece == ' ')
                throw new InvalidOperationException("Move has no piece at origin.");

            // En passant: a pawn moves diagonally onto an empty square.
            if (char.ToLowerInvariant(piece) == 'p'
                && from % 8 != to % 8 && _squares[to] == ' ')
                _squares[from / 8 * 8 + to % 8] = ' ';

            _squares[from] = ' ';
            _squares[to] = move.Length == 5
                ? (char.IsUpper(piece) ? char.ToUpperInvariant(move[4]) : move[4])
                : piece;

            // Standard castling. UCI uses the king's destination square.
            if (char.ToLowerInvariant(piece) == 'k' && Math.Abs(from % 8 - to % 8) == 2)
            {
                int row = from / 8 * 8;
                int rookFrom = row + (to > from ? 7 : 0);
                int rookTo = row + (to > from ? 5 : 3);
                _squares[rookTo] = _squares[rookFrom];
                _squares[rookFrom] = ' ';
            }

            _moves.Add(move);
            LastFrom = from;
            LastTo = to;
        }

        internal static int Square(string move, int offset)
        {
            int file = move[offset] - 'a';
            int rank = move[offset + 1] - '1';
            if (file < 0 || file > 7 || rank < 0 || rank > 7)
                throw new ArgumentException("Invalid chess square.", nameof(move));
            return (7 - rank) * 8 + file;
        }

        internal static string SquareName(int square)
        {
            return string.Concat((char)('a' + square % 8), (char)('8' - square / 8));
        }
    }
}
