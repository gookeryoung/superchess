namespace SuperChess.Core;

/// <summary>
/// 中文纵线着法生成（如「炮二平五」，移植自参考项目 Move.java getChsString）：
/// 红方用中文数字（纵线自右向左一至九），黑方用阿拉伯数字（自左向右 1-9）；
/// 同线同种棋子用前/后（三枚及以上含中，超过三枚用一至五）消歧。
/// 规范见 https://www.xqbase.com/protocol/cchess_move.htm。
/// </summary>
public static class ChineseNotation
{
    private static readonly string[] ChineseNumerals =
        ["一", "二", "三", "四", "五", "六", "七", "八", "九"];

    /// <summary>
    /// 生成一步走法的中文纵线记法。board 须为走子前局面。
    /// </summary>
    /// <exception cref="ArgumentException">起点无有效棋子时抛出。</exception>
    public static string ToChinese(Board board, Move move)
    {
        var piece = board.GetPiece(move.From);
        if (!Piece.IsValid(piece))
        {
            throw new ArgumentException($"走法起点 {move.From} 无有效棋子", nameof(move));
        }

        var name = Piece.ToChineseName(piece)!.Value;
        var prefix = FindSameFilePrefix(board, piece, move.From);

        if (Piece.IsRed(piece))
        {
            var num1 = ChineseNumerals[9 - move.From.X - 1];
            // 平取目标纵线；进退时直线棋子取步数、斜线棋子（马相仕）取目标纵线。
            var action = move.To.Y < move.From.Y ? "进" : move.To.Y > move.From.Y ? "退" : "平";
            var num2 = action == "平" || Piece.IsDiagonalPiece(piece)
                ? ChineseNumerals[9 - move.To.X - 1]
                : ChineseNumerals[Math.Abs(move.To.Y - move.From.Y) - 1];

            return prefix is null ? $"{name}{num1}{action}{num2}" : $"{prefix}{name}{action}{num2}";
        }
        else
        {
            var num1 = (move.From.X + 1).ToString();
            var action = move.To.Y > move.From.Y ? "进" : move.To.Y < move.From.Y ? "退" : "平";
            var num2 = action == "平" || Piece.IsDiagonalPiece(piece)
                ? (move.To.X + 1).ToString()
                : Math.Abs(move.To.Y - move.From.Y).ToString();

            return prefix is null ? $"{name}{num1}{action}{num2}" : $"{prefix}{name}{action}{num2}";
        }
    }

    /// <summary>
    /// 同一纵线上存在同种棋子时的前/后/中/数字前缀；无歧义返回 null。
    /// 局限（沿用参考项目）：多枚兵分居多条纵线时未按「从右到左再从前到后」全盘编号。
    /// </summary>
    private static string? FindSameFilePrefix(Board board, int piece, Position pos)
    {
        // 仕相与帅将纵线上同种棋子至多两枚且必然一进一退，无需消歧。
        if (piece is Piece.RedKing or Piece.BlackKing or Piece.RedAdvisor or Piece.BlackAdvisor
            or Piece.RedBishop or Piece.BlackBishop)
        {
            return null;
        }

        // 扫描方向：红方自上向下（ index 1 最靠近敌阵 = 前），黑方自下向上。
        var red = Piece.IsRed(piece);
        var start = red ? 0 : Board.Height - 1;
        var end = red ? Board.Height : -1;
        var step = red ? 1 : -1;

        var count = 0;
        var index = 0;
        for (var y = start; y != end; y += step)
        {
            if (board.GetPiece(pos.X, y) == piece)
            {
                count++;
                if (y == pos.Y)
                {
                    index = count;
                }
            }
        }

        if (count == 1)
        {
            return null;
        }

        if (piece is not (Piece.RedPawn or Piece.BlackPawn))
        {
            // 非兵卒同线同种棋子至多两枚。
            return index == 1 ? "前" : "后";
        }

        // 兵卒：三枚及以下用前/中/后，超过三枚用一至五（红中文、黑阿拉伯）。
        if (count <= 3)
        {
            return index switch
            {
                1 => "前",
                2 when count == 3 => "中",
                _ => "后",
            };
        }

        return red ? ChineseNumerals[index - 1] : index.ToString();
    }
}
