namespace SuperChess.Game;

/// <summary>
/// 残局题库：T3 将扩充至 ≥8 题（一步杀×4 + 两步杀×3 + 三步杀×1），
/// 正解主线经桌面 Pikafish 对拍验证（REQ-7）。数据由题库完整性测试兜底校验。
/// </summary>
public static class PuzzleLibrary
{
    /// <summary>全部内置残局题（按难度升序排列）。</summary>
    public static IReadOnlyList<PuzzleDefinition> All { get; } =
    [
        new(
            "mate1-menggong",
            "一步杀·重炮闷宫",
            "红先一步杀：黑将闷在宫顶，马已被架住。找到制胜一着。",
            1,
            "3aka3/4n4/4R4/9/9/9/9/9/4C4/3K5 w - - 0 1",
            ["e7e8"]),
    ];
}
