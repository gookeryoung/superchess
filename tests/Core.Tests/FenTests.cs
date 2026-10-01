using SuperChess.Core;
using Xunit;

namespace SuperChess.Core.Tests;

/// <summary>FEN 编解码测试（AC-8：非法 FEN 报明确错误且含出错字段）。</summary>
public class FenTests
{
    [Fact]
    public void RoundTrip_InitialFen_IsStable()
    {
        var board = Board.FromFen(Fen.Initial);
        Assert.Equal(Fen.Initial, board.ToFen());
    }

    [Fact]
    public void RoundTrip_CustomPosition_IsStable()
    {
        var board = new Board();
        Assert.True(board.DoMove(Move.FromUcci("h2e2")!.Value));
        var fen = board.ToFen();
        var restored = Board.FromFen(fen);
        Assert.Equal(fen, restored.ToFen());
        Assert.Equal("h2e2", Move.FromUcci("h2e2")!.Value.ToUcci());
    }

    [Fact]
    public void Parse_TwoFieldShortFormat_UsesDefaults()
    {
        var board = Board.FromFen("rnbakabnr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9/RNBAKABNR w");
        Assert.True(board.RedToMove);
        Assert.Equal(1, board.Rounds);
    }

    [Fact]
    public void Parse_SideAliases_RAndWBothMeanRed()
    {
        Assert.True(Board.FromFen(Fen.Initial.Replace(" w ", " r ")).RedToMove);
        Assert.False(Board.FromFen(Fen.Initial.Replace(" w ", " b ")).RedToMove);
    }

    [Fact]
    public void DoMove_FlipsSideAndCountsRound()
    {
        var board = new Board();
        Assert.True(board.DoMove(Move.FromUcci("h2e2")!.Value));
        Assert.False(board.RedToMove);
        Assert.Equal(2, board.Rounds);
    }

    [Theory]
    [InlineData("", "字段数")]
    [InlineData("rnbakabnr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9/RNBAKABNR w w", "字段数")]
    [InlineData("rnbakabnr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9 w - - 0 1", "棋盘")]
    [InlineData("rnbakabnr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9/RNBAKABNR/RNBAKABNR w - - 0 1", "棋盘")]
    [InlineData("rnbakabnr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9/RNBAKABNRX w - - 0 1", "棋盘")]
    [InlineData("rnbakabnr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9/RNBAKAB w - - 0 1", "棋盘")]
    [InlineData("rnbakabnr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9/RNBA1ABNR w - - 0 1", "棋盘")]
    [InlineData("rnbakabnr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9/rnba1abnr w - - 0 1", "棋盘")]
    [InlineData("knbakabnr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9/RNBAKABNR w - - 0 1", "棋盘")]
    [InlineData("rnbakabnr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9/RNBAKABNQ w - - 0 1", "棋盘")]
    [InlineData("rnbakabnr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9/RNBAKABNR q - - 0 1", "走子方")]
    [InlineData("rnbakabnr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9/RNBAKABNR w - - 0 abc", "回合数")]
    [InlineData("rnbakabnr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9/RNBAKABNR w - - 0 0", "回合数")]
    public void Parse_InvalidFen_ThrowsWithFieldName(string fen, string expectedField)
    {
        var ex = Assert.Throws<FenFormatException>(() => Board.FromFen(fen));
        Assert.Equal(expectedField, ex.FieldName);
    }
}
