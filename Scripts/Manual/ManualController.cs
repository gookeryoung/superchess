using SuperChess.Core;

namespace SuperChess.Manual;

/// <summary>
/// 打谱游标控制器（纯 C#，零 Godot 依赖）：维护着法树游标，提供主线前进、后退、
/// 回开局与变着分支选择。局面落地由 Main 驱动 GameSession 完成（单步前进/后退走
/// TryPlayMove/Undo 保留动画音效；游标与 GameSession 局面的同步不变量由调用顺序
/// 保证——仅在对应落子/悔棋/载入成功后调用 AdvanceTo/MoveBack/Rewind）。
/// </summary>
public sealed class ManualController
{
    private ManualDocument? _document;
    private MoveNode? _current;

    /// <summary>当前棋谱文档；未打开时为 null。</summary>
    public ManualDocument? Document => _document;

    /// <summary>游标当前节点（着法树位置）；未打开时为 null。</summary>
    public MoveNode? Current => _current;

    /// <summary>是否已打开棋谱。</summary>
    public bool IsOpen => _document is not null;

    /// <summary>游标是否位于棋谱终局（无后续着法）。</summary>
    public bool IsAtEnd => _current is null || _current.Children.Count == 0;

    /// <summary>当前节点的全部分支着法（单元素即主线，多元素含变着）。</summary>
    public IReadOnlyList<MoveNode> Branches => _current?.Children ?? [];

    /// <summary>主线总着数（根节点沿第一个子节点到叶的长度）；未打开时为 0。</summary>
    public int MainlineCount
    {
        get
        {
            if (_document is null)
            {
                return 0;
            }

            var count = 0;
            for (var node = _document.Root;
                 node is { Children.Count: > 0 };
                 node = node.Children[0])
            {
                count++;
            }

            return count;
        }
    }

    /// <summary>游标深度（根到当前节点走过的着数，开局为 0）。</summary>
    public int CurrentDepth
    {
        get
        {
            var depth = 0;
            for (var cursor = _current; cursor?.Move is not null; cursor = cursor.Parent!)
            {
                depth++;
            }

            return depth;
        }
    }

    /// <summary>打开棋谱：游标复位到根节点（开局局面）。</summary>
    public void Open(ManualDocument document)
    {
        _document = document;
        _current = document.Root;
    }

    /// <summary>关闭棋谱（退出打谱时由 Main 调用）。</summary>
    public void Clear()
    {
        _document = null;
        _current = null;
    }

    /// <summary>主线下一节点（第一个子节点）；无后续着法返回 null。</summary>
    public MoveNode? PeekDefaultForward() =>
        _current is { Children.Count: > 0 } node ? node.Children[0] : null;

    /// <summary>在当前节点的分支中按起终点查找节点（变着点击落点选择）；未命中返回 null。</summary>
    public MoveNode? FindBranch(Move move) =>
        _current?.Children.FirstOrDefault(child =>
            child.Move is { } branchMove &&
            branchMove.From == move.From && branchMove.To == move.To);

    /// <summary>
    /// 推进游标到目标节点（必须为当前节点的直接子节点，游标与局面同步由调用方保证）。
    /// 目标不是当前节点的子节点时抛出 InvalidOperationException（编程错误）。
    /// </summary>
    public void AdvanceTo(MoveNode node)
    {
        if (_current is null || node.Parent != _current)
        {
            throw new InvalidOperationException("目标节点不是当前游标的直接子节点");
        }

        _current = node;
    }

    /// <summary>游标后退一步；已在根节点（开局）返回 false。</summary>
    public bool MoveBack()
    {
        if (_current?.Parent is not { } parent)
        {
            return false;
        }

        _current = parent;
        return true;
    }

    /// <summary>游标复位到根节点（开局局面）。</summary>
    public void Rewind()
    {
        if (_document is { } document)
        {
            _current = document.Root;
        }
    }

    /// <summary>初始局面 FEN（回开局落地用）。</summary>
    public string InitialFen => _document?.InitialBoard.ToFen()
        ?? throw new InvalidOperationException("尚未打开棋谱");

    /// <summary>
    /// 计算任意节点对应局面 FEN：从目标节点沿父链上溯到根收集走法序列，
    /// 再从初始局面正向模拟（供跳转落地与测试断言）。
    /// </summary>
    public string FenAt(MoveNode node)
    {
        if (_document is not { } document)
        {
            throw new InvalidOperationException("尚未打开棋谱");
        }

        var path = new List<Move>();
        for (var cursor = node; cursor.Move is { } move; cursor = cursor.Parent!)
        {
            path.Add(move);
        }

        path.Reverse();
        var board = document.InitialBoard.Clone();
        foreach (var move in path)
        {
            board.DoMove(move);
        }

        return board.ToFen();
    }
}
