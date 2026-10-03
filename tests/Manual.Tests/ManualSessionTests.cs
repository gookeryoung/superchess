using SuperChess.Core;
using SuperChess.Game;
using SuperChess.Manual;
using Xunit;

namespace SuperChess.Manual.Tests;

/// <summary>
/// 打谱协作语义测试：重演 Main 打谱路由与 GameSession 的协作协议——
/// 前进=TryPlayMove+AdvanceTo、后退=Undo+MoveBack、回开局=LoadFen+Rewind，
/// 断言游标与对局局面在全程保持同步（含真实样例 sample_07 的变着切换）。
/// </summary>
public class ManualSessionTests
{
    /// <summary>UCCI 串转走法（断言前置前提：串合法）。</summary>
    private static Move U(string ucci) => Move.FromUcci(ucci) ?? throw new ArgumentException(ucci);

    private static ManualDocument Load(string name) =>
        XqfParser.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestData", name)));

    /// <summary>按 Main 打谱路由语义沿主线前进一步，返回新游标节点。</summary>
    private static MoveNode ForwardOne(GameSession session, ManualController controller)
    {
        var node = controller.PeekDefaultForward()!;
        Assert.True(session.TryPlayMove(node.Move!.Value), node.Move.Value.ToUcci());
        controller.AdvanceTo(node);
        return node;
    }

    [Fact]
    public void ForwardMainline_CursorAndBoardStayInSync()
    {
        using var session = new GameSession();
        var controller = new ManualController();
        var doc = Load("sample_07_XQStudio.xqf");
        session.LoadFen(doc.InitialBoard.ToFen());
        controller.Open(doc);

        // 逐前进：每步对局局面必须与游标节点对应局面一致（同步不变量）
        while (controller.PeekDefaultForward() is { } next)
        {
            ForwardOne(session, controller);
            Assert.Equal(controller.FenAt(controller.Current!), session.CurrentBoard.ToFen());
        }

        Assert.True(controller.IsAtEnd);
        Assert.Equal(33, controller.CurrentDepth);
        Assert.Equal(33, session.History.Count);
    }

    [Fact]
    public void BackThenSwitchVariation_BoardMatchesVariationFen()
    {
        using var session = new GameSession();
        var controller = new ManualController();
        var doc = Load("sample_07_XQStudio.xqf");
        session.LoadFen(doc.InitialBoard.ToFen());
        controller.Open(doc);

        // 主线走 16 步到 d7d2（其父节点 b2b6 下有主线 d7d2 与变着 a7c8 两个分支）
        for (var i = 0; i < 16; i++)
        {
            ForwardOne(session, controller);
        }

        Assert.Equal("d7d2", controller.Current!.Move!.Value.ToUcci());

        // 后退一步（Undo + MoveBack）回到分支点 b2b6，点击变着 a7c8 切换
        Assert.True(session.Undo());
        Assert.True(controller.MoveBack());
        Assert.Equal("b2b6", controller.Current!.Move!.Value.ToUcci());
        Assert.Equal(2, controller.Branches.Count);
        var variation = controller.FindBranch(U("a7c8"));
        Assert.NotNull(variation);
        Assert.True(session.TryPlayMove(U("a7c8")));
        controller.AdvanceTo(variation!);

        // oracle：从初始局面直接模拟主线 15 步 + 变着 a7c8
        var oracle = doc.InitialBoard.Clone();
        foreach (var ucci in (string[])"g0e2|h7e7|h0g2|h9g7|i0h0|i9h9|b0c2|b9a7|h2h6|g6g5|c3c4|b7d7|a0b0|a9b9|b2b6".Split('|'))
        {
            oracle.DoMove(U(ucci));
        }

        oracle.DoMove(U("a7c8"));
        Assert.Equal(oracle.ToFen(), session.CurrentBoard.ToFen());
        Assert.Equal(controller.FenAt(controller.Current!), session.CurrentBoard.ToFen());
    }

    [Fact]
    public void Rewind_LoadFenRestoresInitialPosition()
    {
        using var session = new GameSession();
        var controller = new ManualController();
        var doc = Load("sample_07_XQStudio.xqf");
        session.LoadFen(doc.InitialBoard.ToFen());
        controller.Open(doc);

        for (var i = 0; i < 5; i++)
        {
            ForwardOne(session, controller);
        }

        // 回开局（Main: LoadFen + Rewind）
        Assert.True(session.LoadFen(controller.InitialFen));
        controller.Rewind();
        Assert.Same(doc.Root, controller.Current);
        Assert.Equal(doc.InitialBoard.ToFen(), session.CurrentBoard.ToFen());
        Assert.Empty(session.History);
    }

    [Fact]
    public void Undo_AtStart_IsRejected()
    {
        using var session = new GameSession();
        var controller = new ManualController();
        var doc = Load("test_manual_v10.xqf");
        session.LoadFen(doc.InitialBoard.ToFen());
        controller.Open(doc);

        Assert.False(session.Undo());
        Assert.Equal(0, controller.CurrentDepth);
    }

    [Fact]
    public void ClickNonExistingMove_WhileAmbiguous_MatchesMainRoutingRejection()
    {
        // 模拟 Main.HandleManualMove：多分支待选时点击非分支落点 → 游标不动
        using var session = new GameSession();
        var controller = new ManualController();
        var doc = Load("test_manual_v10.xqf");
        session.LoadFen(doc.InitialBoard.ToFen());
        controller.Open(doc);

        Assert.Equal(2, controller.Branches.Count);
        Assert.Null(controller.FindBranch(U("b0c2")));
        Assert.Equal(0, controller.CurrentDepth);
        Assert.Equal(doc.InitialBoard.ToFen(), session.CurrentBoard.ToFen());
    }
}
