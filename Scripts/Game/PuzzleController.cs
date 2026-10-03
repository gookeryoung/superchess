namespace SuperChess.Game;

using SuperChess.Core;

/// <summary>
/// 残局练习控制器（纯 C#，零 Godot 依赖）：游标停在着法树当前节点，用户着与并列
/// 正解分支比对（任一命中即接受并推进），防守着唯一由 Main 延迟落子；偏离即拒绝
/// 并要求重试。Evaluate 接受时立即推进游标（题库完整性测试保证树着法在对应局面
/// 合法，落子失败不会发生）。
/// </summary>
public sealed class PuzzleController
{
    private PuzzleDefinition? _current;
    private PuzzleMoveNode? _cursor;

    /// <summary>当前题目；未开始时为 null。</summary>
    public PuzzleDefinition? Current => _current;

    /// <summary>已完成的用户着数（用于「第 x/N 步」进度提示）。</summary>
    public int UserMoveCount => _cursor is null ? 0 : (_cursor.Depth + 1) / 2;

    /// <summary>本题主线用户着总数（沿主线数用户着，与用户选择的正解分支无关）。</summary>
    public int TotalUserMoves
    {
        get
        {
            if (_current is null)
            {
                return 0;
            }

            var count = 0;
            for (var node = _current.Root;
                 node.Children.Count > 0;
                 node = node.Children[0])
            {
                if (node.Depth % 2 == 0)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>主线末位用户着是否已被接受（着法树走完、无防守着，即达成将死、过关）。</summary>
    public bool IsSolved =>
        _current is not null && _cursor is { IsRoot: false } && _cursor.Depth % 2 == 1
        && _cursor.Children.Count == 0;

    /// <summary>开始一道残局题（Main 先调 GameSession.LoadFen 载入局面）。</summary>
    public void Start(PuzzleDefinition puzzle)
    {
        _current = puzzle;
        _cursor = puzzle.Root;
    }

    /// <summary>重玩本题（Main 重新 LoadFen 后调用）。</summary>
    public void Reset()
    {
        if (_current is { } puzzle)
        {
            _cursor = puzzle.Root;
        }
    }

    /// <summary>退出练习（返回对弈模式时由 Main 调用）。</summary>
    public void Clear()
    {
        _current = null;
        _cursor = null;
    }

    /// <summary>
    /// 先行校验用户走子：与当前节点的任一并列正解分支一致则接受并推进；否则拒绝
    /// （局面不变）。命中后有防守着则游标越过它一并推进（棋盘应用由 Main 延迟执行，
    /// 树匹配不依赖棋盘），无防守着即过关。
    /// </summary>
    public PuzzleMoveResult Evaluate(Move move)
    {
        if (_current is null || _cursor is null)
        {
            return new PuzzleMoveResult(false, "当前没有进行中的残局题", null);
        }

        if (IsSolved)
        {
            return new PuzzleMoveResult(false, "本题已过关", null);
        }

        var branch = _cursor.Children.FirstOrDefault(
            child => Matches(child, move));
        if (branch is null)
        {
            return new PuzzleMoveResult(false, "此着不能达成目标，请重试", null);
        }

        _cursor = branch;
        Move? defense = null;
        if (branch.Children.Count > 0)
        {
            var defenseNode = branch.Children[0];
            defense = defenseNode.ToMove();
            _cursor = defenseNode;
        }

        var message = branch.Children.Count == 0
            ? "过关！"
            : $"第 {UserMoveCount}/{TotalUserMoves} 步正确";
        return new PuzzleMoveResult(true, message, defense);
    }

    /// <summary>分支着法与用户走子按起终点匹配。</summary>
    private static bool Matches(PuzzleMoveNode node, Move move)
    {
        if (Move.FromUcci(node.Ucci) is not { } branchMove)
        {
            return false;
        }

        return branchMove.From == move.From && branchMove.To == move.To;
    }
}
