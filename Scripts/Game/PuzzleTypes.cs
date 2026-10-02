namespace SuperChess.Game;

using SuperChess.Core;

/// <summary>
/// 一道残局练习题定义：用户执主线走子方，按交错主线连杀至将死。
/// </summary>
/// <param name="Id">题目标识（如 "mate1-shuangche"）。</param>
/// <param name="Title">题目标题（如「一步杀·双车错」）。</param>
/// <param name="Description">题目提示文案（载入后显示于状态栏）。</param>
/// <param name="Difficulty">难度（1=一步杀，2=两步杀，3=三步杀）。</param>
/// <param name="Fen">残局局面 FEN（走子方为用户方）。</param>
/// <param name="Mainline">交错 UCCI 主线：偶数位为用户着，奇数位为防守着；末位用户着达成将死。</param>
public sealed record PuzzleDefinition(
    string Id, string Title, string Description, int Difficulty, string Fen, string[] Mainline);

/// <summary>残局走子判定结果。</summary>
/// <param name="Accepted">是否接受该走子（接受后由 Main 调 GameSession.TryPlayMove 落子）。</param>
/// <param name="Message">提示文案：拒绝时为原因，接受时可为进度提示。</param>
/// <param name="DefenseReply">主线防守着；用户着之后存在防守着时非空（Main 延迟落子），过关时为 null。</param>
public sealed record PuzzleMoveResult(bool Accepted, string Message, Move? DefenseReply);
