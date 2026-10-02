namespace SuperChess.Game;

using SuperChess.Core;

/// <summary>课程目标类型（入门指导的走子判定语义）。</summary>
public enum LessonGoalKind
{
    /// <summary>目标棋子任意合法走法（棋子走法教学课）。</summary>
    AnyMoveOfPiece,

    /// <summary>精确走子：从指定起点到指定终点（精确走子课）。</summary>
    ExactMove,

    /// <summary>任意一步造成将军的走法（将军课）。</summary>
    AnyCheckingMove,

    /// <summary>任意一步造成将死的走法（将死课）。</summary>
    AnyMateMove,

    /// <summary>任意吃子走法；PieceCode 非 0 时须吃掉指定棋子（吃子/炮打课）。</summary>
    AnyCaptureMove,
}

/// <summary>课程目标：Kind 决定判定语义，其余字段按 Kind 取用。</summary>
/// <param name="Kind">目标类型。</param>
/// <param name="PieceCode">AnyMoveOfPiece/AnyCaptureMove 的目标棋子编码（AnyCaptureMove 为 0 表示任意吃子）。</param>
/// <param name="From">ExactMove 的起点。</param>
/// <param name="To">ExactMove 的终点。</param>
public sealed record LessonGoal(LessonGoalKind Kind, int PieceCode = 0, Position? From = null, Position? To = null);

/// <summary>一节入门课程定义：指定局面下完成一个走子目标。</summary>
/// <param name="Id">课程标识（如 "move-knight"）。</param>
/// <param name="Title">课程标题（如「马走日」）。</param>
/// <param name="Intro">课程讲解文案（载入后显示于状态栏）。</param>
/// <param name="Fen">课程局面 FEN。</param>
/// <param name="Goal">走子目标。</param>
/// <param name="SuccessText">完成后的反馈文案。</param>
public sealed record LessonDefinition(
    string Id, string Title, string Intro, string Fen, LessonGoal Goal, string SuccessText);

/// <summary>课程走子判定结果。</summary>
/// <param name="Accepted">是否接受该走子（接受后由 Main 调 GameSession.TryPlayMove 落子）。</param>
/// <param name="Message">提示文案：拒绝时为原因，接受时为空。</param>
public sealed record LessonMoveResult(bool Accepted, string Message);
