namespace SuperChess.Game;

using SuperChess.Core;

/// <summary>
/// 残局练习控制器（纯 C#，零 Godot 依赖）：用户走子与主线当前步比对，正确则推进
/// 并给出防守着（Main 落子）；偏离主线即拒绝并要求重试。 Evaluate 接受时立即推进
/// 进度（题库完整性测试保证主线着法合法，落子失败不会发生）。
/// </summary>
public sealed class PuzzleController
{
    private PuzzleDefinition? _current;
    private List<Move> _mainline = [];
    private int _step;

    /// <summary>当前题目；未开始时为 null。</summary>
    public PuzzleDefinition? Current => _current;

    /// <summary>已完成的用户着数（用于「第 x/N 步」进度提示）。</summary>
    public int UserMoveCount => _step;

    /// <summary>本题用户着总数。</summary>
    public int TotalUserMoves => _current is null ? 0 : (_mainline.Count + 1) / 2;

    /// <summary>主线末位用户着是否已被接受（即达成将死、过关）。</summary>
    public bool IsSolved =>
        _current is not null && _step * 2 >= _mainline.Count;

    /// <summary>开始一道残局题（Main 先调 GameSession.LoadFen 载入局面）。</summary>
    public void Start(PuzzleDefinition puzzle)
    {
        _current = puzzle;
        _mainline = [.. puzzle.Mainline.Select(u => Move.FromUcci(u))
            .Select(m => m ?? throw new ArgumentException($"题目 {puzzle.Id} 主线含非法 UCCI 着法"))];
        _step = 0;
    }

    /// <summary>重玩本题（Main 重新 LoadFen 后调用）。</summary>
    public void Reset()
    {
        _step = 0;
    }

    /// <summary>退出练习（返回对弈模式时由 Main 调用）。</summary>
    public void Clear()
    {
        _current = null;
        _mainline = [];
        _step = 0;
    }

    /// <summary>
    /// 先行校验用户走子：与主线当前用户着一致则接受并推进；否则拒绝（局面不变）。
    /// 主线着法经题库完整性测试保证与当前局面匹配。
    /// </summary>
    public PuzzleMoveResult Evaluate(Move move)
    {
        if (_current is not { } puzzle || IsSolved)
        {
            return new PuzzleMoveResult(false, "当前没有进行中的残局题", null);
        }

        var expected = _mainline[_step * 2];
        if (move.From != expected.From || move.To != expected.To)
        {
            return new PuzzleMoveResult(false, "此着不能达成目标，请重试", null);
        }

        _step++;
        Move? defense = _step * 2 < _mainline.Count ? _mainline[_step * 2 - 1] : null;
        var message = IsSolved
            ? "过关！"
            : $"第 {UserMoveCount}/{TotalUserMoves} 步正确";
        return new PuzzleMoveResult(true, message, defense);
    }
}
