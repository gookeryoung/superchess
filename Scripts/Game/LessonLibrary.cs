namespace SuperChess.Game;

using SuperChess.Core;

/// <summary>
/// 入门课程库：T3 将扩充至 ≥8 课（七兵种走法 + 将军/将死/吃子）。
/// 课程 FEN 使用「棋盘 走子方」短格式；数据由题库完整性测试兜底校验。
/// </summary>
public static class LessonLibrary
{
    /// <summary>全部内置课程（按教学顺序排列）。</summary>
    public static IReadOnlyList<LessonDefinition> All { get; } =
    [
        new(
            "move-knight",
            "马走日",
            "马走「日」字：先直一格再斜一格，紧邻的直格有子（蹩马腿）时不可走。点击红马，走到任意绿色落点。",
            "4k4/9/4N4/9/9/9/9/9/9/3K5 w",
            new LessonGoal(LessonGoalKind.AnyMoveOfPiece, Piece.RedKnight),
            "马走日学会了！注意蹩马腿的位置。"),
    ];
}
