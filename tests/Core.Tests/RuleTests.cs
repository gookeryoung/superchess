using SuperChess.Core;
using Xunit;

namespace SuperChess.Core.Tests;

/// <summary>走法规则测试（AC-1：蹩马腿/塞象眼/过河兵/九宫/将军/将死/困毙/白脸将）。</summary>
public class RuleTests
{
    /// <summary>仅含双方将帅（不同纵线，避免白脸将干扰）的空局面。</summary>
    private static Board EmptyBoard() => Board.FromFen("3k5/9/9/9/9/9/9/9/9/4K4 w - - 0 1");

    [Fact]
    public void Knight_HobbledByBlocker_LosesTwoTargets()
    {
        var board = EmptyBoard();
        board.SetPiece(4, 4, Piece.RedKnight);
        var freeMoves = Rule.GetLegalMoves(board, new Position(4, 4));
        Assert.Equal(8, freeMoves.Count);

        // 在 (5,4) 放置车辆蹩马腿：(6,3) 与 (6,5) 两方向不可达。
        board.SetPiece(5, 4, Piece.BlackRook);
        var hobbledMoves = Rule.GetLegalMoves(board, new Position(4, 4));
        Assert.Equal(6, hobbledMoves.Count);
        Assert.DoesNotContain(new Move(new Position(4, 4), new Position(6, 3)), hobbledMoves);
        Assert.DoesNotContain(new Move(new Position(4, 4), new Position(6, 5)), hobbledMoves);
    }

    [Fact]
    public void Bishop_EyeBlocked_CannotCross()
    {
        var board = EmptyBoard();
        board.SetPiece(2, 9, Piece.RedBishop);
        var moves = Rule.GetLegalMoves(board, new Position(2, 9));
        Assert.Equal(new HashSet<Position> { new(0, 7), new(4, 7) }, ToTargets(moves));

        // 塞象眼：堵住 (1,8) 后不能去 (0,7)。
        board.SetPiece(1, 8, Piece.BlackPawn);
        Assert.Equal(new HashSet<Position> { new(4, 7) },
            ToTargets(Rule.GetLegalMoves(board, new Position(2, 9))));
    }

    [Fact]
    public void Pawn_BeforeCrossing_MovesForwardOnly()
    {
        var board = EmptyBoard();
        board.SetPiece(4, 6, Piece.RedPawn);
        Assert.Equal(new HashSet<Position> { new(4, 5) },
            ToTargets(Rule.GetLegalMoves(board, new Position(4, 6))));
    }

    [Fact]
    public void Pawn_AfterCrossing_MovesForwardAndSideways()
    {
        var board = EmptyBoard();
        board.SetPiece(4, 3, Piece.RedPawn);
        Assert.Equal(new HashSet<Position> { new(4, 2), new(3, 3), new(5, 3) },
            ToTargets(Rule.GetLegalMoves(board, new Position(4, 3))));
    }

    [Fact]
    public void Pawn_AtLastRank_MovesSidewaysOnly()
    {
        var board = EmptyBoard();
        board.SetPiece(0, 9, Piece.BlackPawn);
        Assert.Equal(new HashSet<Position> { new(1, 9) },
            ToTargets(Rule.GetLegalMoves(board, new Position(0, 9))));
    }

    [Fact]
    public void King_ConfinedToPalace()
    {
        var board = EmptyBoard();
        Assert.Equal(new HashSet<Position> { new(3, 9), new(5, 9), new(4, 8) },
            ToTargets(Rule.GetLegalMoves(board, new Position(4, 9))));
    }

    [Fact]
    public void ScreenPiece_MovingAway_CausesKingsFacing_IsIllegal()
    {
        // 红帅 (4,9)、黑将 (4,0)、红车 (4,5) 作遮挡：红车平移离开纵线即成白脸将。
        var board = Board.FromFen("4k4/9/9/9/9/9/9/9/9/4K4 w - - 0 1");
        board.SetPiece(4, 5, Piece.RedRook);
        var moves = Rule.GetLegalMoves(board, new Position(4, 5));
        Assert.Contains(new Move(new Position(4, 5), new Position(4, 6)), moves);
        Assert.DoesNotContain(new Move(new Position(4, 5), new Position(3, 5)), moves);
        Assert.DoesNotContain(new Move(new Position(4, 5), new Position(5, 5)), moves);
    }

    [Fact]
    public void IsInCheck_RookOnSameFile_DetectsAndScreenBlocks()
    {
        var board = Board.FromFen("3kr4/9/9/9/9/9/9/9/9/4K4 w - - 0 1");
        Assert.True(Rule.IsInCheck(board, redSide: true));

        // 放入遮挡子后不再被将。
        board.SetPiece(4, 5, Piece.RedAdvisor);
        Assert.False(Rule.IsInCheck(board, redSide: true));
    }

    [Fact]
    public void IsInCheck_KnightHobbleRespected()
    {
        // 黑马 (3,7) 攻击红帅 (4,9)：马腿 (3,8) 空则被将；堵住马腿则解除。
        var board = Board.FromFen("4k4/9/9/9/9/9/4n4/9/9/4K4 w - - 0 1");
        Assert.True(Rule.IsInCheck(board, redSide: true));
        board.SetPiece(3, 8, Piece.RedAdvisor);
        Assert.False(Rule.IsInCheck(board, redSide: true));
    }

    [Fact]
    public void Checkmate_TwoRooksSuffocate_NoLegalMove()
    {
        var board = Board.FromFen("3rrk3/9/9/9/9/9/9/9/9/3K5 w - - 0 1");
        Assert.True(Rule.IsInCheck(board, redSide: true));
        Assert.True(Rule.IsCheckmate(board, redSide: true));
        Assert.False(Rule.IsStalemate(board, redSide: true));
    }

    [Fact]
    public void Stalemate_TwoRooksPinKing_NoLegalMoveWithoutCheck()
    {
        var board = Board.FromFen("3k5/9/9/9/9/9/9/3r1r3/9/4K4 w - - 0 1");
        Assert.False(Rule.IsInCheck(board, redSide: true));
        Assert.True(Rule.IsStalemate(board, redSide: true));
        Assert.False(Rule.IsCheckmate(board, redSide: true));
    }

    [Fact]
    public void IsLegalMove_RejectsHobbledKnightAndAcceptsCannon()
    {
        var board = new Board();
        // 马八进七合法；马八进九方向被蹩腿（马腿在 (2,9) 相位）不合法。
        Assert.True(Rule.IsLegalMove(board, new Move(new Position(1, 9), new Position(2, 7))));
        Assert.False(Rule.IsLegalMove(board, new Move(new Position(1, 9), new Position(3, 8))));
        // 炮二平五合法。
        Assert.True(Rule.IsLegalMove(board, new Move(new Position(7, 7), new Position(4, 7))));
    }

    [Fact]
    public void InitialPosition_NoCheckNoFacing()
    {
        var board = new Board();
        Assert.False(Rule.IsInCheck(board, redSide: true));
        Assert.False(Rule.IsInCheck(board, redSide: false));
        Assert.False(Rule.IsKingsFacing(board));
        Assert.False(Rule.IsCheckmate(board, redSide: true));
        Assert.False(Rule.IsStalemate(board, redSide: true));
    }

    private static HashSet<Position> ToTargets(List<Move> moves) =>
        moves.Select(m => m.To).ToHashSet();
}
