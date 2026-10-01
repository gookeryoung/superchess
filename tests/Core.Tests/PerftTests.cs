using SuperChess.Core;
using Xunit;

namespace SuperChess.Core.Tests;

/// <summary>
/// 起始局面 perft 对拍测试（AC-1）。参考值 44/1920/79666 为业界公认数值，
/// 已按设计要求先与第三方实现（pyffish/Fairy-Stockfish）交叉验证后固化。
/// </summary>
public class PerftTests
{
    [Theory]
    [InlineData(1, 44)]
    [InlineData(2, 1920)]
    [InlineData(3, 79666)]
    public void Perft_InitialPosition_MatchesReference(int depth, long expected)
    {
        var board = new Board();
        Assert.Equal(expected, Perft(board, depth));
    }

    /// <summary>标准 perft：枚举全部合法着法递归计数，叶子层直接取合法着数。</summary>
    private static long Perft(Board board, int depth)
    {
        var moves = Rule.GetAllLegalMoves(board, board.RedToMove);
        if (depth == 1)
        {
            return moves.Count;
        }

        long nodes = 0;
        foreach (var move in moves)
        {
            var next = board.Clone();
            next.DoMove(move);
            nodes += Perft(next, depth - 1);
        }

        return nodes;
    }
}
