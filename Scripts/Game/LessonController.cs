namespace SuperChess.Game;

using SuperChess.Core;

/// <summary>
/// 入门指导控制器（纯 C#，零 Godot 依赖）：对用户走子做课程目标先行校验。
/// Main 在 OnMoveChosen 中先调 Evaluate，拒绝的走子不进 GameSession.TryPlayMove；
/// 接受并落子后由 Main 调 ConsumeApplied 确认完成。课程目标均为单步达成。
/// </summary>
public sealed class LessonController
{
    private LessonDefinition? _current;
    private bool _completed;

    /// <summary>当前课程；未开始时为 null。</summary>
    public LessonDefinition? Current => _current;

    /// <summary>当前课程是否已完成。</summary>
    public bool IsCompleted => _completed;

    /// <summary>开始一节课程（Main 先调 GameSession.LoadFen 载入局面）。</summary>
    public void Start(LessonDefinition lesson)
    {
        _current = lesson;
        _completed = false;
    }

    /// <summary>退出课程（返回对弈模式时由 Main 调用）。</summary>
    public void Reset()
    {
        _current = null;
        _completed = false;
    }

    /// <summary>
    /// 先行校验用户走子是否符合课程目标；走法本身的合法性由 GameSession.TryPlayMove 校验，
    /// 本方法只做目标匹配（含模拟落子后的将军/将死判定）。
    /// </summary>
    public LessonMoveResult Evaluate(Board board, Move move)
    {
        if (_current is not { } lesson || _completed)
        {
            return new LessonMoveResult(false, "当前没有进行中的课程");
        }

        return lesson.Goal.Kind switch
        {
            LessonGoalKind.AnyMoveOfPiece => EvaluatePieceMove(board, move, lesson),
            LessonGoalKind.ExactMove => EvaluateExactMove(board, move, lesson),
            // DoMove 已翻边：走子后的 RedToMove 即应将方（被将军/被将死判定对象）。
            LessonGoalKind.AnyCheckingMove => EvaluatePredicate(board, move, lesson,
                b => Rule.IsInCheck(b, b.RedToMove), "这一步没有造成将军"),
            LessonGoalKind.AnyMateMove => EvaluatePredicate(board, move, lesson,
                b => Rule.IsCheckmate(b, b.RedToMove), "这一步没有将死对方"),
            LessonGoalKind.AnyCaptureMove => EvaluateCapture(board, move, lesson),
            _ => new LessonMoveResult(false, "未知课程目标"),
        };
    }

    /// <summary>走子被应用后调用；课程目标均为单步，应用即完成。</summary>
    /// <returns>课程是否完成。</returns>
    public bool ConsumeApplied()
    {
        if (_current is null || _completed)
        {
            return false;
        }

        _completed = true;
        return true;
    }

    private static LessonMoveResult EvaluatePieceMove(Board board, Move move, LessonDefinition lesson)
    {
        var piece = board.GetPiece(move.From);
        if (piece != lesson.Goal.PieceCode)
        {
            return new LessonMoveResult(false, "请移动指定的棋子");
        }

        return new LessonMoveResult(true, string.Empty);
    }

    private static LessonMoveResult EvaluateExactMove(Board board, Move move, LessonDefinition lesson)
    {
        if (lesson.Goal.From is not { } from || lesson.Goal.To is not { } to)
        {
            return new LessonMoveResult(false, "课程目标缺少起终点坐标");
        }

        return move.From == from && move.To == to
            ? new LessonMoveResult(true, string.Empty)
            : new LessonMoveResult(false, "走子路线不符合目标，请按课程要求走子");
    }

    private static LessonMoveResult EvaluateCapture(Board board, Move move, LessonDefinition lesson)
    {
        var captured = board.GetPiece(move.To);
        if (!Piece.IsValid(captured) || Piece.IsRed(captured) == board.RedToMove)
        {
            return new LessonMoveResult(false, "这一步没有吃子，请吃掉目标棋子");
        }

        if (lesson.Goal.PieceCode != 0 && captured != lesson.Goal.PieceCode)
        {
            return new LessonMoveResult(false, "请吃掉指定的棋子");
        }

        return new LessonMoveResult(true, string.Empty);
    }

    /// <summary>模拟落子后按谓词判定（将军/将死共用）；走子方归属与合法性由 TryPlayMove 兜底。</summary>
    private static LessonMoveResult EvaluatePredicate(
        Board board, Move move, LessonDefinition lesson, Func<Board, bool> predicate, string rejectText)
    {
        var piece = board.GetPiece(move.From);
        if (!Piece.IsValid(piece) || Piece.IsRed(piece) != board.RedToMove)
        {
            return new LessonMoveResult(false, "请移动走子方的棋子");
        }

        var simulated = board.Clone();
        if (!simulated.DoMove(move))
        {
            return new LessonMoveResult(false, "走法非法");
        }

        return predicate(simulated)
            ? new LessonMoveResult(true, string.Empty)
            : new LessonMoveResult(false, rejectText);
    }
}
