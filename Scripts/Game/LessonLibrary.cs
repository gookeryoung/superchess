namespace SuperChess.Game;

using SuperChess.Core;

/// <summary>
/// 入门课程库：十课覆盖七兵种走法、将军、将死与吃子（炮打）。
/// 课程 FEN 使用「棋盘 走子方」短格式；数据由题库完整性测试兜底校验。
/// </summary>
public static class LessonLibrary
{
    /// <summary>全部内置课程（按教学顺序排列）。</summary>
    public static IReadOnlyList<LessonDefinition> All { get; } =
    [
        new(
            "move-king",
            "帅（将）走法",
            "帅（将）限九宫内一步直行，且不能与对方将帅照脸。点击红帅，走到任意绿色落点。",
            "3k5/9/9/9/9/9/9/9/9/4K4 w",
            new LessonGoal(LessonGoalKind.AnyMoveOfPiece, Piece.RedKing),
            "帅（将）走法学会了！记住：九宫之内，一步直行。"),
        new(
            "move-advisor",
            "仕（士）走法",
            "仕（士）限九宫内沿斜线走一格，是帅（将）的贴身护卫。点击红仕，走到任意绿色落点。",
            "3k5/9/9/9/9/9/9/9/4A4/4K4 w",
            new LessonGoal(LessonGoalKind.AnyMoveOfPiece, Piece.RedAdvisor),
            "仕（士）走法学会了！九宫斜行一步格。"),
        new(
            "move-bishop",
            "相（象）走法",
            "相（象）走「田」字斜行两格，不能过河；田字中心有子（塞象眼）时不可走。点击红相，走到任意绿色落点。",
            "3k5/9/9/9/9/9/9/9/9/4K1B2 w",
            new LessonGoal(LessonGoalKind.AnyMoveOfPiece, Piece.RedBishop),
            "相（象）走法学会了！田字飞行不过河，小心塞象眼。"),
        new(
            "move-knight",
            "马走日",
            "马走「日」字：先直一格再斜一格，紧邻的直格有子（蹩马腿）时不可走。点击红马，走到任意绿色落点。",
            "4k4/9/4N4/9/9/9/9/9/9/3K5 w",
            new LessonGoal(LessonGoalKind.AnyMoveOfPiece, Piece.RedKnight),
            "马走日学会了！注意蹩马腿的位置。"),
        new(
            "move-rook",
            "车走直线",
            "车沿横竖线任意距离直行，威力最强。点击红车，走到任意绿色落点。",
            "3k5/9/9/9/9/R8/9/9/9/4K4 w",
            new LessonGoal(LessonGoalKind.AnyMoveOfPiece, Piece.RedRook),
            "车走法学会了！一车十子寒，直线任驰骋。"),
        new(
            "move-cannon",
            "炮走法",
            "炮与车一样直线行走，但吃子必须隔一个「炮架」。点击红炮，走到任意绿色落点。",
            "3k5/r8/9/9/p8/C8/9/9/9/4K4 w",
            new LessonGoal(LessonGoalKind.AnyMoveOfPiece, Piece.RedCannon),
            "炮走法学会了！行走如车，吃子借架。"),
        new(
            "move-pawn",
            "兵（卒）走法",
            "兵（卒）过河前只能前行，过河后可前行或横移，永不能后退。点击红兵，走到任意绿色落点。",
            "3k5/9/9/9/4P4/9/9/9/9/4K4 w",
            new LessonGoal(LessonGoalKind.AnyMoveOfPiece, Piece.RedPawn),
            "兵（卒）走法学会了！过河小卒赛如车。"),
        new(
            "check-basic",
            "将军与应将",
            "走一步攻击对方将（帅）即为「将军」。黑车正红车线，请走一步车形成将军。",
            "4k4/9/9/9/9/3R5/9/3n5/9/3K5 w",
            new LessonGoal(LessonGoalKind.AnyCheckingMove),
            "会将军了！被将军一方必须应将：应将、垫子或吃掉攻击子。"),
        new(
            "mate-basic",
            "将死",
            "将（帅）被将军且无法应将、垫子或吃掉攻击子，即为「将死」，对局结束。此局面红方一步可成重炮杀，请找出制胜一着。",
            "3aka3/4c4/4R4/4C4/9/9/9/9/4C4/3K5 w",
            new LessonGoal(LessonGoalKind.AnyMateMove),
            "一步杀！将死后对局立即结束，这就是残局练习的目标。"),
        new(
            "capture-cannon",
            "吃子（炮打）",
            "炮吃子必须隔一个棋子作炮架。请用红炮隔黑卒打掉黑车。",
            "3k5/r8/9/9/p8/C8/9/9/9/4K4 w",
            new LessonGoal(LessonGoalKind.AnyCaptureMove, Piece.BlackRook),
            "炮打学会了！隔山打牛，正是炮的精髓。"),
    ];
}
