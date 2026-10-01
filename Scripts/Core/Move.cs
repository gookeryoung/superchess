namespace SuperChess.Core;

/// <summary>
/// 一步走法（起点 + 终点）。吃子类型由走子前局面的终点棋子决定，不在本结构中携带。
/// </summary>
public readonly record struct Move(Position From, Position To)
{
    /// <summary>
    /// 解析 UCCI 着法字符串（如 "h2e2"：纵线 a-i 自左向右，横线 0-9 自下向上，y 取 9-y）。
    /// </summary>
    /// <param name="ucci">4 字符 UCCI 着法。</param>
    /// <returns>解析成功返回着法，失败返回 null。</returns>
    public static Move? FromUcci(string? ucci)
    {
        if (ucci is null || ucci.Length != 4)
        {
            return null;
        }

        var from = new Position(ucci[0] - 'a', 9 - (ucci[1] - '0'));
        var to = new Position(ucci[2] - 'a', 9 - (ucci[3] - '0'));
        if (!from.IsValid || !to.IsValid)
        {
            return null;
        }

        return new Move(from, to);
    }

    /// <summary>编码为 UCCI 着法字符串（如 "h2e2"，y 取 9-y）。</summary>
    public string ToUcci() =>
        $"{(char)('a' + From.X)}{9 - From.Y}{(char)('a' + To.X)}{9 - To.Y}";
}
