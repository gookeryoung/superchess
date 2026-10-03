namespace SuperChess.Game;

using SuperChess.Core;

/// <summary>
/// 残局着法树节点：一着 UCCI 走法 + 分支子树（结构与打谱 ManualDocument.MoveNode
/// 对齐但不共享类型——残局树仅表达单题的交错主线与并列正解分支，语义独立）。
/// 深度约定：根节点为空着法（深度 0），奇数深度为用户着（红），偶数深度为防守着（黑）。
/// 用户着节点在父节点 children 中并列多项即多正解；防守着节点恒唯一。
/// </summary>
public sealed class PuzzleMoveNode
{
    /// <summary>本着 UCCI 串；根节点为空串。</summary>
    public string Ucci { get; }

    /// <summary>父节点；根节点为 null。</summary>
    public PuzzleMoveNode? Parent { get; private set; }

    /// <summary>分支子树（用户着层为并列正解，防守着层恒单线）。</summary>
    public List<PuzzleMoveNode> Children { get; } = [];

    /// <summary>是否根节点（不持有走法）。</summary>
    public bool IsRoot => Ucci.Length == 0;

    /// <summary>根到本节点的深度（根为 0，每着 +1）。</summary>
    public int Depth
    {
        get
        {
            var depth = 0;
            for (var cursor = this; cursor.Parent is not null; cursor = cursor.Parent)
            {
                depth++;
            }

            return depth;
        }
    }

    /// <summary>创建持有 UCCI 走法的节点（树构建 DSL 用；挂接父节点经 AddChild）。</summary>
    public PuzzleMoveNode(string ucci)
    {
        Ucci = ucci;
    }

    /// <summary>创建根节点（空着法）。</summary>
    public PuzzleMoveNode()
        : this(string.Empty)
    {
    }

    /// <summary>挂接已构建的子节点（重设其 Parent 并追加到分支列表末尾）。</summary>
    public void AddChild(PuzzleMoveNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    /// <summary>解析本节点的走法（根节点抛 InvalidOperationException）。</summary>
    public Move ToMove() =>
        Move.FromUcci(Ucci) ?? throw new InvalidOperationException($"节点含非法 UCCI 着法：{Ucci}");
}

/// <summary>
/// 一道残局练习题定义：用户执主线走子方，沿交错树连杀至将死；
/// 并列最优着法以分支形式收录（任一命中均算正解）。
/// </summary>
/// <param name="Id">题目标识（如 "mate1-chongpao"）。</param>
/// <param name="Title">题目标题（如「一步杀·重炮」）。</param>
/// <param name="Description">题目提示文案（载入后显示于状态栏）。</param>
/// <param name="Difficulty">难度（1=一步杀，2=两步杀，3=三步杀，4=四步及以上进阶）。</param>
/// <param name="Fen">残局局面 FEN（走子方为用户方）。</param>
/// <param name="Root">着法树根节点（空着法，children 首层为并列正解首着）。</param>
public sealed record PuzzleDefinition(
    string Id, string Title, string Description, int Difficulty, string Fen, PuzzleMoveNode Root);

/// <summary>残局走子判定结果。</summary>
/// <param name="Accepted">是否接受该走子（接受后由 Main 调 GameSession.TryPlayMove 落子）。</param>
/// <param name="Message">提示文案：拒绝时为原因，接受时可为进度提示。</param>
/// <param name="DefenseReply">主线防守着；用户着之后存在防守着时非空（Main 延迟落子），过关时为 null。</param>
public sealed record PuzzleMoveResult(bool Accepted, string Message, Move? DefenseReply);
