namespace SuperChess.Core;

/// <summary>
/// FEN 解析失败异常：携带出错的 FEN 字段名与原因，供 UI 显示明确错误（AC-8）。
/// </summary>
public sealed class FenFormatException : Exception
{
    /// <summary>出错的 FEN 字段名（如「棋盘」「走子方」「回合数」「字段数」）。</summary>
    public string FieldName { get; }

    /// <param name="fieldName">出错的 FEN 字段名。</param>
    /// <param name="reason">出错原因（中文描述）。</param>
    public FenFormatException(string fieldName, string reason)
        : base($"FEN 字段「{fieldName}」非法：{reason}")
    {
        FieldName = fieldName;
    }
}

/// <summary>
/// FEN 局面编解码（https://www.xqbase.com/protocol/cchess_fen.htm）。
/// 格式：&lt;棋盘&gt; &lt;走子方 w/b&gt; - - &lt;半回合计数&gt; &lt;回合数&gt;，走子方 w 与 r 等价。
/// </summary>
public static class Fen
{
    private const string InitialFen = "rnbakabnr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9/RNBAKABNR w - - 0 1";

    /// <summary>标准起始局面 FEN。</summary>
    public static string Initial => InitialFen;

    /// <summary>
    /// 解析 FEN 并构造局面。宽松兼容仅含「棋盘 走子方」两个字段的短格式（计数取默认值）。
    /// </summary>
    /// <exception cref="FenFormatException">任一字段非法时抛出，含字段名与原因。</exception>
    public static Board ToBoard(string fen)
    {
        if (string.IsNullOrWhiteSpace(fen))
        {
            throw new FenFormatException("字段数", "FEN 为空");
        }

        var parts = fen.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 && parts.Length != 6)
        {
            throw new FenFormatException("字段数", $"应为 2 或 6 个字段，实际为 {parts.Length} 个");
        }

        var cells = ParseBoard(parts[0]);
        bool redToMove = ParseSide(parts[1]);
        int rounds = 1;
        if (parts.Length == 6)
        {
            if (!int.TryParse(parts[4], out _))
            {
                throw new FenFormatException("半回合计数", $"「{parts[4]}」不是整数");
            }

            if (!int.TryParse(parts[5], out rounds) || rounds < 1)
            {
                throw new FenFormatException("回合数", $"「{parts[5]}」应为不小于 1 的整数");
            }
        }

        return new Board(cells, redToMove, rounds);
    }

    /// <summary>将局面编码为 6 字段 FEN 字符串。</summary>
    public static string FromBoard(Board board)
    {
        var rows = new string[Board.Height];
        for (var y = 0; y < Board.Height; y++)
        {
            var row = new System.Text.StringBuilder();
            var emptyRun = 0;
            for (var x = 0; x < Board.Width; x++)
            {
                var piece = board.GetPiece(x, y);
                if (piece == Piece.Empty)
                {
                    emptyRun++;
                }
                else
                {
                    if (emptyRun > 0)
                    {
                        row.Append(emptyRun);
                        emptyRun = 0;
                    }

                    row.Append(Piece.ToFenChar(piece)!.Value);
                }
            }

            if (emptyRun > 0)
            {
                row.Append(emptyRun);
            }

            rows[y] = row.ToString();
        }

        var side = board.RedToMove ? "w" : "b";
        return $"{string.Join('/', rows)} {side} - - 0 {board.Rounds}";
    }

    /// <summary>解析棋盘字段并校验：10 行、每行 9 列、棋子合法、双方各一将帅且在九宫内。</summary>
    private static int[,] ParseBoard(string boardField)
    {
        var rows = boardField.Split('/');
        if (rows.Length != Board.Height)
        {
            throw new FenFormatException("棋盘", $"应为 {Board.Height} 行，实际为 {rows.Length} 行");
        }

        var cells = new int[Board.Height, Board.Width];
        for (var y = 0; y < Board.Height; y++)
        {
            var x = 0;
            foreach (var c in rows[y])
            {
                if (c is >= '1' and <= '9')
                {
                    x += c - '0';
                }
                else
                {
                    var piece = Piece.FromFenChar(c)
                        ?? throw new FenFormatException("棋盘", $"第 {y + 1} 行含非法字符「{c}」");
                    if (x >= Board.Width)
                    {
                        throw new FenFormatException("棋盘", $"第 {y + 1} 行超出 {Board.Width} 列");
                    }

                    cells[y, x] = piece;
                    x++;
                }
            }

            if (x != Board.Width)
            {
                throw new FenFormatException("棋盘", $"第 {y + 1} 行仅描述 {x} 列，应为 {Board.Width} 列");
            }
        }

        ValidateKings(cells);
        return cells;
    }

    /// <summary>校验双方各恰有一枚将帅，且均在各自九宫内。</summary>
    private static void ValidateKings(int[,] cells)
    {
        var redKingCount = 0;
        var blackKingCount = 0;
        Position redKingPos = default;
        Position blackKingPos = default;
        for (var y = 0; y < Board.Height; y++)
        {
            for (var x = 0; x < Board.Width; x++)
            {
                switch (cells[y, x])
                {
                    case Piece.RedKing:
                        redKingCount++;
                        redKingPos = new Position(x, y);
                        break;
                    case Piece.BlackKing:
                        blackKingCount++;
                        blackKingPos = new Position(x, y);
                        break;
                }
            }
        }

        if (redKingCount != 1 || blackKingCount != 1)
        {
            throw new FenFormatException("棋盘", $"帅与将须各恰有一枚，实际红帅 {redKingCount} 枚、黑将 {blackKingCount} 枚");
        }

        if (!IsInPalace(redKingPos, red: true))
        {
            throw new FenFormatException("棋盘", $"红帅位于 {redKingPos}，不在红方九宫内");
        }

        if (!IsInPalace(blackKingPos, red: false))
        {
            throw new FenFormatException("棋盘", $"黑将位于 {blackKingPos}，不在黑方九宫内");
        }
    }

    /// <summary>坐标是否在指定方九宫内（x 3-5，红 y 7-9，黑 y 0-2）。</summary>
    private static bool IsInPalace(Position pos, bool red) =>
        pos.X is >= 3 and <= 5 && pos.Y >= (red ? 7 : 0) && pos.Y <= (red ? 9 : 2);

    /// <summary>解析走子方字段：w/r 为红方，b 为黑方。</summary>
    private static bool ParseSide(string side) => side switch
    {
        "w" or "r" => true,
        "b" => false,
        _ => throw new FenFormatException("走子方", $"「{side}」应为 w、r 或 b"),
    };
}
