using SuperChess.Core;
using Xunit;

namespace SuperChess.Game.Tests;

/// <summary>
/// PuzzleController 主线推进测试：正确推进/防守着提取/偏离拒绝/重置（设计 T1 第 3 步）。
/// 推进逻辑不依赖真实杀局，主线着法合法性由题库完整性测试覆盖。
/// </summary>
public class PuzzleControllerTests
{
    private static readonly PuzzleDefinition TwoStepPuzzle = new(
        "test-two-step", "测试两步题", "验证主线推进", 2,
        "3aka3/4n4/4R4/9/9/9/9/9/4C4/3K5 w - - 0 1",
        ["e7e8", "e8e9", "e7d7"]);

    [Fact]
    public void Start_InvalidUcci_Throws()
    {
        var controller = new PuzzleController();
        Assert.Throws<ArgumentException>(() => controller.Start(TwoStepPuzzle with
        {
            Mainline = ["e7e8", "zzzz", "e7d7"],
        }));
    }

    [Fact]
    public void Evaluate_CorrectUserMove_AdvancesAndReturnsDefense()
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
    public void Evaluate_WrongUserMove_RejectsWithoutProgress()
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
    public void Clear_ExitPractice()
    {
        var controller = new PuzzleController();
        controller.Start(TwoStepPuzzle);
        controller.Clear();

        Assert.Null(controller.Current);
        var result = controller.Evaluate(Move.FromUcci("e7e8")!.Value);
        Assert.False(result.Accepted);
    }
}
