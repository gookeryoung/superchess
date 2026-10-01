using SuperChess.Core;
using Xunit;

namespace SuperChess.Core.Tests;

/// <summary>中文纵线记谱测试（AC-7：炮二平五、前/后/中兵消歧、红中文黑阿拉伯数字）。</summary>
public class ChineseNotationTests
{
    [Fact]
    public void InitialBoard_CannonToCenter_IsPaoErPingWu()
    {
        var board = new Board();
        Assert.Equal("炮二平五", ChineseNotation.ToChinese(board, new Move(new Position(7, 7), new Position(4, 7))));
        Assert.Equal("炮八平五", ChineseNotation.ToChinese(board, new Move(new Position(1, 7), new Position(4, 7))));
    }

    [Fact]
    public void InitialBoard_KnightAdvance_UsesTargetFile()
    {
        var board = new Board();
        Assert.Equal("马八进七", ChineseNotation.ToChinese(board, new Move(new Position(1, 9), new Position(2, 7))));
    }

    [Fact]
    public void KingAdvance_AndRetreat_UsesStepCount()
    {
        var board = new Board();
        Assert.Equal("帅五进一", ChineseNotation.ToChinese(board, new Move(new Position(4, 9), new Position(4, 8))));
        Assert.Equal("将5退1", ChineseNotation.ToChinese(board, new Move(new Position(4, 0), new Position(4, 1))));
    }

    [Fact]
    public void RookRetreat_UsesStepCount()
    {
        var board = new Board();
        Assert.Equal("车九退二", ChineseNotation.ToChinese(board, new Move(new Position(0, 9), new Position(0, 7))));
    }

    [Fact]
    public void BlackPieces_UseArabicNumerals()
    {
        var board = new Board();
        Assert.Equal("炮2平5", ChineseNotation.ToChinese(board, new Move(new Position(1, 2), new Position(4, 2))));
    }

    [Fact]
    public void TwoBlackCannonsSameFile_UseFrontBackPrefix()
    {
        var board = Board.FromFen("3k5/9/9/9/9/9/9/9/9/4K4 w - - 0 1");
        board.SetPiece(1, 2, Piece.BlackCannon);
        board.SetPiece(1, 6, Piece.BlackCannon);
        // 黑方前为靠近红方一侧（y 更大者先扫描）。
        Assert.Equal("前炮平5", ChineseNotation.ToChinese(board, new Move(new Position(1, 6), new Position(4, 6))));
        Assert.Equal("后炮平5", ChineseNotation.ToChinese(board, new Move(new Position(1, 2), new Position(4, 2))));
    }

    [Fact]
    public void TwoPawnsSameFile_UseFrontBackPrefix()
    {
        var board = Board.FromFen("3k5/9/9/9/9/9/9/9/9/4K4 w - - 0 1");
        board.SetPiece(0, 3, Piece.RedPawn);
        board.SetPiece(0, 6, Piece.RedPawn);
        Assert.Equal("前兵平八", ChineseNotation.ToChinese(board, new Move(new Position(0, 3), new Position(1, 3))));
        Assert.Equal("后兵平八", ChineseNotation.ToChinese(board, new Move(new Position(0, 6), new Position(1, 6))));
    }

    [Fact]
    public void ThreePawnsSameFile_UsesFrontMiddleBack()
    {
        var board = Board.FromFen("3k5/9/9/9/9/9/9/9/9/4K4 w - - 0 1");
        board.SetPiece(0, 2, Piece.RedPawn);
        board.SetPiece(0, 4, Piece.RedPawn);
        board.SetPiece(0, 6, Piece.RedPawn);
        Assert.Equal("前兵平八", ChineseNotation.ToChinese(board, new Move(new Position(0, 2), new Position(1, 2))));
        Assert.Equal("中兵平八", ChineseNotation.ToChinese(board, new Move(new Position(0, 4), new Position(1, 4))));
        Assert.Equal("后兵平八", ChineseNotation.ToChinese(board, new Move(new Position(0, 6), new Position(1, 6))));
    }

    [Fact]
    public void TwoRooksSameFile_UseFrontBackPrefix()
    {
        var board = Board.FromFen("3k5/9/9/9/9/9/9/9/9/4K4 w - - 0 1");
        board.SetPiece(0, 5, Piece.RedRook);
        board.SetPiece(0, 8, Piece.RedRook);
        Assert.Equal("前车平八", ChineseNotation.ToChinese(board, new Move(new Position(0, 5), new Position(1, 5))));
        Assert.Equal("后车平八", ChineseNotation.ToChinese(board, new Move(new Position(0, 8), new Position(1, 8))));
    }

    [Fact]
    public void AdvisorsAndBishops_NeverUsePrefix()
    {
        // 仕相同线两枚必然一进一退，规范要求不加前缀；初始局面直接验证。
        var board = new Board();
        Assert.Equal("仕六进五", ChineseNotation.ToChinese(board, new Move(new Position(3, 9), new Position(4, 8))));
        Assert.Equal("仕四进五", ChineseNotation.ToChinese(board, new Move(new Position(5, 9), new Position(4, 8))));
        Assert.Equal("相七进五", ChineseNotation.ToChinese(board, new Move(new Position(2, 9), new Position(4, 7))));
    }
}
