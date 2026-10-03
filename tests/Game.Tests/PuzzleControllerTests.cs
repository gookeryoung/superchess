using SuperChess.Core;
using Xunit;

namespace SuperChess.Game.Tests;

/// <summary>
/// PuzzleController 树推进测试：正确推进/并列正解接受/非最优拒绝/防守着提取/
/// 过关判定/重置。推进逻辑不依赖真实杀局，树着法合法性由题库完整性测试覆盖。
/// </summary>
public class PuzzleControllerTests
{
    /// <summary>
    /// 测试两步题（含并列正解）：第一手 e7e8 与 b7e7 并列正解，防守 e8e9 唯一，
    /// 第二手 e7d7 收官。树形：Root → [e7e8 | b7e7] → e8e9 → e7d7。
    /// </summary>
    private static readonly PuzzleDefinition TwoStepPuzzle = new(
        "test-two-step", "测试两步题", "验证树推进", 2,
        "3aka3/4n4/4R4/9/9/9/9/9/4C4/3K5 w - - 0 1",
        BuildTree());

    private static PuzzleMoveNode BuildTree()
    {
        var root = new PuzzleMoveNode();
        var main = new PuzzleMoveNode("e7e8");
        var alt = new PuzzleMoveNode("b7e7");
        root.AddChild(main);
        root.AddChild(alt);
        var defense = new PuzzleMoveNode("e8e9");
        main.AddChild(defense);
        alt.AddChild(new PuzzleMoveNode("d9e8"));
        defense.AddChild(new PuzzleMoveNode("e7d7"));
        return root;
    }

    [Fact]
    public void Start_InvalidUcci_InTree_DetectedOnEvaluate()
    {
        // 树结构含非法 UCCI 时 Evaluate 解析分支返回未命中（拒绝），不抛异常
        var root = new PuzzleMoveNode();
        var bad = new PuzzleMoveNode("zzzz");
        root.AddChild(bad);
        var puzzle = new PuzzleDefinition("bad", "坏题", "", 1,
            "3aka3/4n4/4R4/9/9/9/9/9/4C4/3K5 w - - 0 1", root);
        var controller = new PuzzleController();
        controller.Start(puzzle);

        var result = controller.Evaluate(Move.FromUcci("e7e8")!.Value);
        Assert.False(result.Accepted);
    }

    [Fact]
    public void Evaluate_MainlineSolution_AdvancesAndReturnsDefense()
    {
        var controller = new PuzzleController();
        controller.Start(TwoStepPuzzle);

        var result = controller.Evaluate(Move.FromUcci("e7e8")!.Value);
        Assert.True(result.Accepted);
        Assert.Equal(1, controller.UserMoveCount);
        Assert.False(controller.IsSolved);
        Assert.NotNull(result.DefenseReply);
        Assert.Equal("e8e9", result.DefenseReply!.Value.ToUcci());
    }

    [Fact]
    public void Evaluate_ParallelSolution_AlsoAccepted()
    {
        // 多正解：并列最优 b7e7 与主线 e7e8 同样被接受（各自接自己的防守着）
        var controller = new PuzzleController();
        controller.Start(TwoStepPuzzle);

        var result = controller.Evaluate(Move.FromUcci("b7e7")!.Value);
        Assert.True(result.Accepted);
        Assert.NotNull(result.DefenseReply);
        Assert.Equal("d9e8", result.DefenseReply!.Value.ToUcci());
        Assert.Equal(1, controller.UserMoveCount);
    }

    [Fact]
    public void Evaluate_SuboptimalMove_RejectedWithoutProgress()
    {
        var controller = new PuzzleController();
        controller.Start(TwoStepPuzzle);

        var result = controller.Evaluate(Move.FromUcci("e7d7")!.Value);
        Assert.False(result.Accepted);
        Assert.Equal(0, controller.UserMoveCount);
        Assert.Null(result.DefenseReply);
    }

    [Fact]
    public void Evaluate_FinalUserMove_SolvesWithoutDefense()
    {
        var controller = new PuzzleController();
        controller.Start(TwoStepPuzzle);

        Assert.True(controller.Evaluate(Move.FromUcci("e7e8")!.Value).Accepted);
        var final = controller.Evaluate(Move.FromUcci("e7d7")!.Value);
        Assert.True(final.Accepted);
        Assert.Null(final.DefenseReply);
        Assert.True(controller.IsSolved);
        Assert.Equal(2, controller.UserMoveCount);
    }

    [Fact]
    public void Evaluate_AfterSolved_RejectsFurtherMoves()
    {
        var controller = new PuzzleController();
        controller.Start(TwoStepPuzzle);
        controller.Evaluate(Move.FromUcci("e7e8")!.Value);
        controller.Evaluate(Move.FromUcci("e7d7")!.Value);

        var result = controller.Evaluate(Move.FromUcci("e7e8")!.Value);
        Assert.False(result.Accepted);
        Assert.Contains("已过关", result.Message);
    }

    [Fact]
    public void Reset_RestartsProgress_KeepsPuzzle()
    {
        var controller = new PuzzleController();
        controller.Start(TwoStepPuzzle);
        controller.Evaluate(Move.FromUcci("e7e8")!.Value);

        controller.Reset();
        Assert.Equal(0, controller.UserMoveCount);
        Assert.False(controller.IsSolved);
        Assert.NotNull(controller.Current);
    }

    [Fact]
    public void Clear_ExitsPuzzle()
    {
        var controller = new PuzzleController();
        controller.Start(TwoStepPuzzle);
        controller.Clear();

        Assert.Null(controller.Current);
        var result = controller.Evaluate(Move.FromUcci("e7e8")!.Value);
        Assert.False(result.Accepted);
    }

    [Fact]
    public void TotalUserMoves_CountsMainlineUserMoves()
    {
        var controller = new PuzzleController();
        controller.Start(TwoStepPuzzle);
        Assert.Equal(2, controller.TotalUserMoves);
    }
}
