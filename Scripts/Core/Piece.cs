namespace SuperChess.Core;

/// <summary>
/// 棋子类型常量与判定（编码沿用参考项目 Piece.java）：
/// 0=空，1-7 红方（帅仕相马车炮兵），8-14 黑方（将士象马车炮卒）。
/// </summary>
public static class Piece
{
    public const int Empty = 0;

    public const int RedKing = 1;    // K, 帅
    public const int RedAdvisor = 2; // A, 仕
    public const int RedBishop = 3;  // B, 相
    public const int RedKnight = 4;  // N, 马
    public const int RedRook = 5;    // R, 车
    public const int RedCannon = 6;  // C, 炮
    public const int RedPawn = 7;    // P, 兵

    public const int BlackKing = 8;    // k, 将
    public const int BlackAdvisor = 9; // a, 士
    public const int BlackBishop = 10; // b, 象
    public const int BlackKnight = 11; // n, 马
    public const int BlackRook = 12;   // r, 车
    public const int BlackCannon = 13; // c, 炮
    public const int BlackPawn = 14;   // p, 卒

    /// <summary>是否为有效棋子（非空且在 1-14 范围内）。</summary>
    public static bool IsValid(int piece) => piece is >= RedKing and <= BlackPawn;

    /// <summary>是否为红方棋子（对 Empty 返回值无意义，调用方须先验证 IsValid）。</summary>
    public static bool IsRed(int piece) => piece is >= RedKing and <= RedPawn;

    /// <summary>是否为黑方棋子（对 Empty 返回值无意义，调用方须先验证 IsValid）。</summary>
    public static bool IsBlack(int piece) => piece is >= BlackKing and <= BlackPawn;

    /// <summary>两枚棋子是否同方（空子视为不同方）。</summary>
    public static bool IsSameSide(int a, int b) => IsValid(a) && IsValid(b) && IsRed(a) == IsRed(b);

    /// <summary>是否为走斜线的棋子（马、相/象、仕/士），记谱时第二数字为目标纵线。</summary>
    public static bool IsDiagonalPiece(int piece) =>
        piece is RedKnight or BlackKnight or RedBishop or BlackBishop or RedAdvisor or BlackAdvisor;

    /// <summary>FEN 字符（红大写、黑小写）；非法值返回 null。</summary>
    public static char? ToFenChar(int piece) => piece switch
    {
        RedKing => 'K', RedAdvisor => 'A', RedBishop => 'B',
        RedKnight => 'N', RedRook => 'R', RedCannon => 'C', RedPawn => 'P',
        BlackKing => 'k', BlackAdvisor => 'a', BlackBishop => 'b',
        BlackKnight => 'n', BlackRook => 'r', BlackCannon => 'c', BlackPawn => 'p',
        _ => null,
    };

    /// <summary>由 FEN 字符解析棋子；非法字符返回 null。</summary>
    public static int? FromFenChar(char c) => c switch
    {
        'K' => RedKing, 'A' => RedAdvisor, 'B' => RedBishop,
        'N' => RedKnight, 'R' => RedRook, 'C' => RedCannon, 'P' => RedPawn,
        'k' => BlackKing, 'a' => BlackAdvisor, 'b' => BlackBishop,
        'n' => BlackKnight, 'r' => BlackRook, 'c' => BlackCannon, 'p' => BlackPawn,
        _ => null,
    };

    /// <summary>中文棋子名（红：帅仕相马车炮兵；黑：将士象马车炮卒）；非法值返回 null。</summary>
    public static char? ToChineseName(int piece) => piece switch
    {
        RedKing => '帅', RedAdvisor => '仕', RedBishop => '相',
        RedKnight => '马', RedRook => '车', RedCannon => '炮', RedPawn => '兵',
        BlackKing => '将', BlackAdvisor => '士', BlackBishop => '象',
        BlackKnight => '马', BlackRook => '车', BlackCannon => '炮', BlackPawn => '卒',
        _ => null,
    };
}
