using SuperChess.Core;
using SuperChess.Manual;
using Xunit;

namespace SuperChess.Manual.Tests;

/// <summary>
/// 打谱游标控制器测试：程序化构造含变着的棋谱树（初始局面 h2e2 主线 + b2e2 变着），
/// 断言游标推进/后退/回开局/分支查找与目标局面计算的树语义。
/// </summary>
public class ManualControllerTests
{
    /// <summary>UCCI 串转走法（断言前置前提：串合法）。</summary>
    private static Move U(string ucci) => Move.FromUcci(ucci) ?? throw new ArgumentException(ucci);

    /// <summary>
    /// 构造测试棋谱：根节点两个分支（主线 h2e2，变着 b2e2），
    /// 主线 h2e2 → h9g7（带注解）→ h0g2。
    /// </summary>
    private static ManualDocument CreateDocument()
    {
        var doc = new ManualDocument(new Board());
        var main1 = MoveNode.Append(U("h2e2"), doc.Root);
        var main2 = MoveNode.Append(U("h9g7"), main1, "黑方进马");
        MoveNode.Append(U("h0g2"), main2);
        MoveNode.Append(U("b2e2"), doc.Root);
        return doc;
    }

    private static ManualController CreateOpenController()
    {
        var controller = new ManualController();
        controller.Open(CreateDocument());
        return controller;
    }

    [Fact]
    public void Open_ResetsCursorToRoot()
    {
        var controller = new ManualController();
        Assert.False(controller.IsOpen);
        controller.Open(CreateDocument());
        Assert.True(controller.IsOpen);
        Assert.Same(controller.Document!.Root, controller.Current);
        Assert.Equal(3, controller.MainlineCount);
        Assert.Equal(0, controller.CurrentDepth);
        Assert.False(controller.IsAtEnd);
        Assert.Equal(2, controller.Branches.Count);
    }

    [Fact]
    public void Clear_ResetsState()
    {
        var controller = CreateOpenController();
        controller.Clear();
        Assert.False(controller.IsOpen);
        Assert.Null(controller.Current);
        Assert.Equal(0, controller.MainlineCount);
        Assert.Empty(controller.Branches);
    }

    [Fact]
    public void PeekDefaultForward_ReturnsFirstMainlineChild()
    {
        var controller = CreateOpenController();
        var next = controller.PeekDefaultForward();
        Assert.NotNull(next);
        Assert.Equal("h2e2", next!.Move!.Value.ToUcci());
    }

    [Fact]
    public void AdvanceTo_RejectsNonChildNode()
    {
        var controller = CreateOpenController();
        var root = controller.Current!;
        var main1 = root.Children[0];
        controller.AdvanceTo(main1);

        // 变着节点 b2e2 的父节点是 Root，从 main1 推进到它属于非法跳转
        var variation = root.Children[1];
        Assert.Throws<InvalidOperationException>(() => controller.AdvanceTo(variation));
    }

    [Fact]
    public void AdvanceTo_WalksMainlineAndCountsDepth()
    {
        var controller = CreateOpenController();
        var node = controller.Current!;
        while (controller.PeekDefaultForward() is { } next)
        {
            controller.AdvanceTo(next);
            node = next;
        }

        Assert.True(controller.IsAtEnd);
        Assert.Equal(3, controller.CurrentDepth);
        Assert.Equal("h0g2", node.Move!.Value.ToUcci());
    }

    [Fact]
    public void FindBranch_MatchesMainlineAndVariation()
    {
        var controller = CreateOpenController();
        var root = controller.Current!;
        Assert.Same(root.Children[0], controller.FindBranch(U("h2e2")));
        Assert.Same(root.Children[1], controller.FindBranch(U("b2e2")));
        Assert.Null(controller.FindBranch(U("e6e5")));
    }

    [Fact]
    public void MoveBack_AtRoot_ReturnsFalse()
    {
        var controller = CreateOpenController();
        Assert.False(controller.MoveBack());
    }

    [Fact]
    public void MoveBack_WalksToParent()
    {
        var controller = CreateOpenController();
        controller.AdvanceTo(controller.PeekDefaultForward()!);
        controller.AdvanceTo(controller.PeekDefaultForward()!);
        Assert.Equal(2, controller.CurrentDepth);
        Assert.True(controller.MoveBack());
        Assert.Equal(1, controller.CurrentDepth);
        Assert.Equal("h2e2", controller.Current!.Move!.Value.ToUcci());
    }

    [Fact]
    public void Rewind_ReturnsToRoot()
    {
        var controller = CreateOpenController();
        while (controller.PeekDefaultForward() is { } next)
        {
            controller.AdvanceTo(next);
        }

        controller.Rewind();
        Assert.Same(controller.Document!.Root, controller.Current);
        Assert.Equal(0, controller.CurrentDepth);
    }

    [Fact]
    public void FenAt_Root_EqualsInitialFen()
    {
        var controller = CreateOpenController();
        Assert.Equal(controller.Document!.InitialBoard.ToFen(), controller.FenAt(controller.Document.Root));
        Assert.Equal(controller.InitialFen, controller.FenAt(controller.Document.Root));
    }

    [Fact]
    public void FenAt_MainlineNode_EqualsSequentialSimulation()
    {
        var controller = CreateOpenController();
        var board = controller.Document!.InitialBoard.Clone();
        var node = controller.Document.Root;
        foreach (var ucci in (string[])"h2e2|h9g7|h0g2".Split('|'))
        {
            board.DoMove(U(ucci));
            node = node.Children[0];
            Assert.Equal(board.ToFen(), controller.FenAt(node));
        }
    }

    [Fact]
    public void FenAt_VariationNode_EqualsVariationSimulation()
    {
        var controller = CreateOpenController();
        var variation = controller.Document!.Root.Children[1];
        var board = controller.Document.InitialBoard.Clone();
        board.DoMove(U("b2e2"));
        Assert.Equal(board.ToFen(), controller.FenAt(variation));
    }

    [Fact]
    public void Methods_BeforeOpen_ThrowOrReturnDefaults()
    {
        var controller = new ManualController();
        Assert.Null(controller.Document);
        Assert.Null(controller.PeekDefaultForward());
        Assert.Null(controller.FindBranch(U("h2e2")));
        Assert.Throws<InvalidOperationException>(() => controller.InitialFen);
        Assert.Throws<InvalidOperationException>(() => controller.FenAt(new MoveNode()));
    }
}
