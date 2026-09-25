// Local chess game. Stockfish validates moves and optionally plays Black/White.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class ChessGump : Gump
    {
        private const int SquareSize = 48;
        private const int BoardX = 28;
        private const int BoardY = 54;
        private const int WidthPixels = 620;
        private const int HeightPixels = 482;
        private const int ComputerMoveDurationMs = 900;
        private static readonly int[] SkillLevels = { 0, 4, 9, 15, 20 };
        private static readonly string[] SkillNames = { "Novice", "Casual", "Club", "Expert", "Master" };
        // Chessboard containers render these as gump art (item art ID minus ITEM_GUMP_TEXTURE_OFFSET).
        private static readonly Dictionary<char, ushort> PieceArt = new Dictionary<char, ushort>
        {
            { 'K', 0x091E }, { 'Q', 0x0921 }, { 'R', 0x091D },
            { 'B', 0x091C }, { 'N', 0x091F }, { 'P', 0x0920 },
            { 'k', 0x0925 }, { 'q', 0x0928 }, { 'r', 0x0924 },
            { 'b', 0x0923 }, { 'n', 0x0926 }, { 'p', 0x0927 }
        };

        private readonly ChessGame _game = new ChessGame();
        private readonly DataBox _board;
        private readonly DataBox _promotion;
        private readonly Label _status;
        private readonly Label _difficulty;
        private readonly Label _mode;
        private readonly HashSet<string> _legal = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _positionKeys = new List<string>();
        private StockfishChessSession _engine;
        private Task<ChessPositionInfo> _inspection;
        private Task<string> _computerMove;
        private DataBox _animatedPiece;
        private string _animatedMove;
        private uint _animationStartedAt;
        private int _animationFromSquare = -1;
        private int _animationFromX;
        private int _animationFromY;
        private int _animationToX;
        private int _animationToY;
        private int _selected = -1;
        private int _promotionTo = -1;
        private int _skillIndex = 2;
        private bool _started;
        private bool _computer;
        private bool _humanWhite = true;
        private bool _finished;

        internal static string EnginePath { get; set; } = Path.Combine(AppContext.BaseDirectory, "stockfish.exe");

        internal ChessGump() : base(0, 0)
        {
            X = 150;
            Y = 90;
            Width = WidthPixels;
            Height = HeightPixels;
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 0.95f));
            Add(new Label("Chess", true, CustomGumpThemeManager.TitleHue, font: 1) { X = 28, Y = 17 });
            Add(new Label("Drag the gump; click a piece, then a square.", true,
                CustomGumpThemeManager.DimHue, font: 1) { X = 100, Y = 19 });

            _board = new DataBox(BoardX, BoardY, SquareSize * 8, SquareSize * 8);
            Add(_board);
            _status = new Label("Choose a game mode to begin.", true,
                CustomGumpThemeManager.TitleHue, 540, font: 1) { X = BoardX, Y = 449 };
            Add(_status);
            _mode = new Label("No game", true, CustomGumpThemeManager.TitleHue, font: 1)
            { X = 430, Y = 61 };
            Add(_mode);
            _difficulty = new Label("Difficulty: Club", true, CustomGumpThemeManager.DimHue, font: 1)
            { X = 430, Y = 228 };
            Add(_difficulty);
            _promotion = new DataBox(430, 348, 165, 88);
            Add(_promotion);

            AddButton(430, 88, 160, "Play Stockfish", () => StartGame(true));
            AddButton(430, 120, 160, "Two players", () => StartGame(false));
            AddButton(430, 163, 160, "Side: White / Black", () =>
            {
                _humanWhite = !_humanWhite;
                if (_started && _computer)
                    StartGame(true);
                else
                    DrawBoard();
                SetStatus($"Your side: {(_humanWhite ? "White" : "Black")}");
            });
            AddButton(430, 255, 160, "Change difficulty", () =>
            {
                _skillIndex = (_skillIndex + 1) % SkillLevels.Length;
                _difficulty.Text = "Difficulty: " + SkillNames[_skillIndex];
            });
            AddButton(430, 298, 76, "Undo", Undo);
            AddButton(514, 298, 76, "Close", Dispose);
            DrawBoard();
            SetInScreen();
        }

        public override GumpType GumpType => GumpType.None;

        public override void Update()
        {
            base.Update();
            if (IsDisposed)
                return;
            if (_inspection != null && _inspection.IsCompleted)
            {
                Task<ChessPositionInfo> completed = _inspection;
                _inspection = null;
                if (completed.IsFaulted)
                {
                    EngineError(completed.Exception?.GetBaseException());
                    return;
                }
                ChessPositionInfo info = completed.Result;
                _legal.Clear();
                _legal.UnionWith(info.LegalMoves);
                string positionKey = PositionKey(info.Fen);
                while (_positionKeys.Count > _game.Moves.Count)
                    _positionKeys.RemoveAt(_positionKeys.Count - 1);
                if (positionKey != null)
                    _positionKeys.Add(positionKey);
                if (_legal.Count == 0)
                {
                    _finished = true;
                    SetStatus(info.InCheck
                        ? $"Checkmate — {(_game.WhiteToMove ? "Black" : "White")} wins."
                        : "Stalemate — draw.");
                }
                else if (info.Fen != null && FiftyMoveDraw(info.Fen))
                {
                    _finished = true;
                    SetStatus("Fifty-move rule — draw.");
                }
                else if (positionKey != null && _positionKeys.Count(key => key == positionKey) >= 3)
                {
                    _finished = true;
                    SetStatus("Threefold repetition — draw.");
                }
                else if (_computer && _game.WhiteToMove != _humanWhite)
                {
                    SetStatus("Stockfish is thinking...");
                    _computerMove = _engine.BestMoveAsync(_game.Moves,
                        SkillLevels[_skillIndex], 300 + SkillLevels[_skillIndex] * 55);
                }
                else
                    SetStatus((_game.WhiteToMove ? "White" : "Black") + " to move.");
            }

            if (_computerMove != null && _computerMove.IsCompleted)
            {
                Task<string> completed = _computerMove;
                _computerMove = null;
                if (completed.IsFaulted)
                {
                    EngineError(completed.Exception?.GetBaseException());
                    return;
                }
                string move = completed.Result;
                if (!_legal.Contains(move))
                {
                    EngineError(new IOException("Stockfish returned an unexpected move: " + move));
                    return;
                }
                AnimateComputerMove(move);
            }

            if (_animatedMove != null)
            {
                uint elapsed = unchecked(Time.Ticks - _animationStartedAt);
                float progress = Math.Min(1f,
                    (float)elapsed / ComputerMoveDurationMs);
                if (progress >= 1f)
                {
                    string move = _animatedMove;
                    _animatedMove = null;
                    _animatedPiece = null;
                    _animationFromSquare = -1;
                    Play(move);
                }
                else
                {
                    float eased = progress * progress * (3f - 2f * progress);
                    _animatedPiece.X = _animationFromX
                        + (int)Math.Round((_animationToX - _animationFromX) * eased);
                    _animatedPiece.Y = _animationFromY
                        + (int)Math.Round((_animationToY - _animationFromY) * eased);
                }
            }
        }

        public override void Dispose()
        {
            _inspection = null;
            _computerMove = null;
            _engine?.Dispose();
            _engine = null;
            base.Dispose();
        }

        private void StartGame(bool computer)
        {
            _engine?.Dispose();
            _engine = new StockfishChessSession(EnginePath);
            _game.Reset();
            _selected = _promotionTo = -1;
            _started = true;
            _finished = false;
            _computer = computer;
            _inspection = null;
            _computerMove = null;
            _animatedMove = null;
            _animatedPiece = null;
            _animationFromSquare = -1;
            _mode.Text = computer ? "Vs Stockfish" : "Two players";
            _legal.Clear();
            _positionKeys.Clear();
            DrawPromotionChoices();
            DrawBoard();
            SetStatus("Starting Stockfish...");
            Inspect();
        }

        private void Undo()
        {
            if (!_started || _engine == null || _inspection != null || _computerMove != null
                || _animatedMove != null
                || _game.Moves.Count == 0)
                return;
            _game.Undo(_computer ? Math.Min(2, _game.Moves.Count) : 1);
            _finished = false;
            _selected = _promotionTo = -1;
            DrawPromotionChoices();
            DrawBoard();
            Inspect();
        }

        private void Inspect()
        {
            _legal.Clear();
            _inspection = _engine.InspectAsync(_game.Moves);
        }

        private void OnSquare(int square)
        {
            if (!_started || _finished || _inspection != null || _computerMove != null
                || _animatedMove != null
                || _promotionTo >= 0 || (_computer && _game.WhiteToMove != _humanWhite))
                return;
            char piece = _game.PieceAt(square);
            bool ownPiece = piece != ' ' && char.IsUpper(piece) == _game.WhiteToMove;
            if (_selected < 0 || ownPiece)
            {
                _selected = ownPiece ? square : -1;
                DrawBoard();
                return;
            }

            string prefix = ChessGame.SquareName(_selected) + ChessGame.SquareName(square);
            string[] candidates = _legal.Where(move => move.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            if (candidates.Length == 0)
            {
                SetStatus("Illegal move. Select a piece and try again.");
                return;
            }
            if (candidates.Any(move => move.Length == 5))
            {
                _promotionTo = square;
                DrawPromotionChoices();
                SetStatus("Choose a promotion piece.");
                return;
            }
            Play(candidates[0]);
        }

        private void DrawPromotionChoices()
        {
            _promotion.Clear();
            if (_promotionTo < 0)
                return;
            _promotion.Add(new Label("Promote pawn to:", true,
                CustomGumpThemeManager.TitleHue, font: 1) { X = 0, Y = 0 });
            const string options = "qrbn";
            for (int i = 0; i < options.Length; i++)
            {
                char choice = options[i];
                AddButton(i * 40, 27, 36, char.ToUpperInvariant(choice).ToString(), () =>
                {
                    string move = ChessGame.SquareName(_selected)
                        + ChessGame.SquareName(_promotionTo) + choice;
                    if (_legal.Contains(move))
                        Play(move);
                }, _promotion);
            }
        }

        private void Play(string move)
        {
            _game.Apply(move);
            _selected = _promotionTo = -1;
            DrawPromotionChoices();
            DrawBoard();
            SetStatus("Checking position...");
            Inspect();
        }

        private void AnimateComputerMove(string move)
        {
            int from = ChessGame.Square(move, 0);
            int to = ChessGame.Square(move, 2);
            char piece = _game.PieceAt(from);
            if (piece == ' ')
            {
                EngineError(new IOException("Stockfish moved from an empty square: " + move));
                return;
            }

            _animatedMove = move;
            _animationFromSquare = from;
            _animationStartedAt = Time.Ticks;
            int fromDisplay = _humanWhite ? from : 63 - from;
            int toDisplay = _humanWhite ? to : 63 - to;
            _animationFromX = fromDisplay % 8 * SquareSize;
            _animationFromY = fromDisplay / 8 * SquareSize;
            _animationToX = toDisplay % 8 * SquareSize;
            _animationToY = toDisplay / 8 * SquareSize;

            DrawBoard();
            _animatedPiece = new DataBox(_animationFromX, _animationFromY, SquareSize, SquareSize)
            { AcceptMouseInput = false };
            AddPiece(piece, 0, 0, _animatedPiece);
            _board.Add(_animatedPiece);
            SetStatus("Stockfish moves " + PieceName(piece) + "...");
        }

        private void DrawBoard()
        {
            _board.Clear();
            for (int display = 0; display < 64; display++)
            {
                int square = _humanWhite || !_computer ? display : 63 - display;
                int file = display % 8;
                int rank = display / 8;
                int x = file * SquareSize;
                int y = rank * SquareSize;
                bool dark = (file + rank) % 2 != 0;
                Color color = dark ? new Color(88, 67, 52) : new Color(218, 198, 157);
                if (square == _game.LastFrom || square == _game.LastTo)
                    color = dark ? new Color(126, 112, 55) : new Color(220, 208, 106);
                if (square == _selected)
                    color = new Color(80, 140, 123);
                _board.Add(new AlphaBlendControl(0.97f)
                {
                    X = x, Y = y, Width = SquareSize, Height = SquareSize, BaseColor = color
                });

                char piece = square == _animationFromSquare ? ' ' : _game.PieceAt(square);
                if (piece != ' ')
                    AddPiece(piece, x, y);
                int clicked = square;
                string squareName = ChessGame.SquareName(square);
                string tooltip = piece == ' ' ? squareName
                    : (char.IsUpper(piece) ? "White " : "Black ")
                        + PieceName(piece) + " (" + squareName + ")";
                var hit = new HitBox(x, y, SquareSize, SquareSize, tooltip, 0f);
                hit.MouseUp += (sender, e) =>
                {
                    if (e.Button == MouseButtonType.Left)
                        OnSquare(clicked);
                };
                _board.Add(hit);
            }
        }

        private void AddPiece(char piece, int x, int y, DataBox parent = null)
        {
            DataBox target = parent ?? _board;
            if (PieceArt.TryGetValue(piece, out ushort graphic))
            {
                var art = new GumpPic(0, 0, graphic, 0) { CanMove = false, AcceptMouseInput = false };
                if (!art.IsDisposed && art.Width > 0 && art.Height > 0)
                {
                    float scale = Math.Min(1f, 40f / Math.Max(art.Width, art.Height));
                    art.Width = (int)(art.Width * scale);
                    art.Height = (int)(art.Height * scale);
                    art.X = x + (SquareSize - art.Width) / 2;
                    art.Y = y + (SquareSize - art.Height) / 2;
                    target.Add(art);
                    return;
                }
            }

            bool white = char.IsUpper(piece);
            target.Add(new AlphaBlendControl(0.9f)
            {
                X = x + 9, Y = y + 8, Width = 30, Height = 30,
                BaseColor = white ? new Color(240, 231, 204) : new Color(38, 37, 43)
            });
            var label = new Label(char.ToUpperInvariant(piece).ToString(), true,
                white ? (ushort)0x0455 : (ushort)0x03E5, font: 1)
            { AcceptMouseInput = false };
            label.X = x + (SquareSize - label.Width) / 2;
            label.Y = y + (SquareSize - label.Height) / 2;
            target.Add(label);
        }

        private static string PieceName(char piece)
        {
            switch (char.ToUpperInvariant(piece))
            {
                case 'K': return "King";
                case 'Q': return "Queen";
                case 'R': return "Rook";
                case 'B': return "Bishop";
                case 'N': return "Knight";
                default: return "Pawn";
            }
        }

        private static bool FiftyMoveDraw(string fen)
        {
            string[] fields = fen.Split(' ');
            return fields.Length > 4 && int.TryParse(fields[4], out int halfMoves) && halfMoves >= 100;
        }

        private static string PositionKey(string fen)
        {
            if (fen == null)
                return null;
            string[] fields = fen.Split(' ');
            return fields.Length >= 4 ? string.Join(" ", fields.Take(4)) : null;
        }

        private void EngineError(Exception error)
        {
            _finished = true;
            _engine?.Dispose();
            _engine = null;
            SetStatus(error?.Message ?? "Stockfish failed.");
        }

        private void SetStatus(string text) => _status.Text = text;

        private void AddButton(int x, int y, int width, string text, Action action, DataBox parent = null)
        {
            var button = new ChessButton(x, y, width, text, action)
            { IsSelectable = false, DisplayBorder = true };
            CustomGumpThemeManager.StyleButton(button);
            if (parent == null)
                Add(button);
            else
                parent.Add(button);
        }

        private sealed class ChessButton : NiceButton
        {
            private readonly Action _action;

            internal ChessButton(int x, int y, int width, string text, Action action)
                : base(x, y, width, 26, ButtonAction.Activate, text, font: 1,
                    hue: CustomGumpThemeManager.TitleHue)
            {
                _action = action;
            }

            protected override void OnMouseUp(int x, int y, MouseButtonType button)
            {
                // Skip NiceButton's gump-submit path; chess buttons are local actions.
                if (button == MouseButtonType.Left && MouseIsOver)
                    _action();
            }
        }
    }
}
