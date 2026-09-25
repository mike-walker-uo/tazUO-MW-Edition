using ClassicUO.Game.Managers;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class ChessGameTests
    {
        [Fact]
        public void Starts_with_standard_position_and_moves_a_pawn()
        {
            var game = new ChessGame();
            game.PieceAt(ChessGame.Square("e2e4", 0)).Should().Be('P');
            game.Apply("e2e4");
            game.PieceAt(ChessGame.Square("e2e4", 0)).Should().Be(' ');
            game.PieceAt(ChessGame.Square("e2e4", 2)).Should().Be('P');
            game.WhiteToMove.Should().BeFalse();
        }

        [Fact]
        public void Applies_en_passant_and_undo()
        {
            var game = new ChessGame();
            game.Apply("e2e4");
            game.Apply("a7a6");
            game.Apply("e4e5");
            game.Apply("d7d5");
            game.Apply("e5d6");
            game.PieceAt(ChessGame.Square("d5d6", 0)).Should().Be(' ');
            game.PieceAt(ChessGame.Square("d5d6", 2)).Should().Be('P');
            game.Undo(1);
            game.PieceAt(ChessGame.Square("d7d5", 2)).Should().Be('p');
            game.PieceAt(ChessGame.Square("e4e5", 2)).Should().Be('P');
        }

        [Fact]
        public void Moves_rook_on_castling()
        {
            var game = new ChessGame();
            game.Apply("e2e4");
            game.Apply("e7e5");
            game.Apply("g1f3");
            game.Apply("b8c6");
            game.Apply("f1e2");
            game.Apply("g8f6");
            game.Apply("e1g1");
            game.PieceAt(ChessGame.Square("f1f2", 0)).Should().Be('R');
            game.PieceAt(ChessGame.Square("e1e2", 0)).Should().Be(' ');
            game.PieceAt(ChessGame.Square("g1g2", 0)).Should().Be('K');
        }

        [Fact]
        public void Promotes_a_pawn()
        {
            var game = new ChessGame();
            game.Apply("a2a4");
            game.Apply("b7b5");
            game.Apply("a4b5");
            game.Apply("h7h6");
            game.Apply("b5b6");
            game.Apply("h6h5");
            game.Apply("b6b7");
            game.Apply("h5h4");
            game.Apply("b7a8q");
            game.PieceAt(ChessGame.Square("a8a7", 0)).Should().Be('Q');
        }
    }
}
