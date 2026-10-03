using SuperChess.Core;

namespace SuperChess.Manual;

/// <summary>棋谱解析异常：文件格式非法或内容不受支持时抛出。</summary>
public sealed class ManualFormatException(string message) : FormatException(message)
{
}

/// <summary>
/// 打谱棋谱文档：初始局面 + 着法树（参考项目 XQFManual.MoveNode 语义）。
/// 根节点不持有走法，每个子节点持有一步走法与其分支子树（第一个子节点为主线，其余为变着）。
/// </summary>
public sealed class ManualDocument
{
    /// <summary>初始局面（着法树展开之前的局面）。</summary>
    public Board InitialBoard { get; init; }

    /// <summary>着法树根节点（Move 为 null）。</summary>
    public MoveNode Root { get; }

    /// <summary>XQF 版本号；PGN 来源文档为 0。</summary>
    public int Version { get; init; }

    /// <summary>棋谱标题（XQF 头部字段）。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>赛事名称。</summary>
    public string Event { get; init; } = string.Empty;

    /// <summary>对局日期。</summary>
    public string Date { get; init; } = string.Empty;

    /// <summary>对局地点。</summary>
    public string Site { get; init; } = string.Empty;

    /// <summary>红方名称。</summary>
    public string Red { get; init; } = string.Empty;

    /// <summary>黑方名称。</summary>
    public string Black { get; init; } = string.Empty;

    /// <summary>对局结果（XQF 存中文描述，PGN 存标签原值）。</summary>
    public string Result { get; init; } = string.Empty;

    /// <summary>棋谱类型（全局/布局/中局/残局）。</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>评注者。</summary>
    public string Annotator { get; init; } = string.Empty;

    /// <summary>作者。</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>整局注释（XQF 0x400 首记录携带；无则为 null）。</summary>
    public string? Annotation { get; init; }

    /// <summary>以初始局面构造文档。</summary>
    public ManualDocument(Board initialBoard)
    {
        InitialBoard = initialBoard;
        Root = new MoveNode();
    }

    /// <summary>
    /// 递归校验全部着法在对应局面下合法（复用 Core.Rule 走法生成；
    /// 分支子树各自从父局面出发校验，语义与参考项目 XQFManual.validateMove 一致）。
    /// </summary>
    public bool ValidateAllMoves() => ValidateNode(InitialBoard, Root);

    private static bool ValidateNode(Board board, MoveNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.Move is not { } move || !IsLegalMove(board, move))
            {
                return false;
            }

            var next = board.Clone();
            next.DoMove(move);
            if (!ValidateNode(next, child))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLegalMove(Board board, Move move) =>
        Rule.GetLegalMoves(board, move.From).Contains(move);
}

/// <summary>着法树节点：一步走法 + 注解 + 全部分支子树。</summary>
public sealed class MoveNode
{
    /// <summary>本步走法；根节点为 null。</summary>
    public Move? Move { get; }

    /// <summary>父节点；根节点为 null。</summary>
    public MoveNode? Parent { get; }

    /// <summary>本步注解；无注解为 null。</summary>
    public string? Annotation { get; init; }

    /// <summary>分支子树（第一个元素为主线后续，其余为变着分支）。</summary>
    public List<MoveNode> Children { get; } = [];

    /// <summary>创建根节点（无走法、无父节点）。</summary>
    public MoveNode()
    {
    }

    private MoveNode(Move move, MoveNode parent)
    {
        Move = move;
        Parent = parent;
    }

    /// <summary>创建持有走法的子节点并挂到 parent 的分支列表末尾。</summary>
    public static MoveNode Append(Move move, MoveNode parent, string? annotation = null)
    {
        var node = new MoveNode(move, parent) { Annotation = annotation };
        parent.Children.Add(node);
        return node;
    }
}
