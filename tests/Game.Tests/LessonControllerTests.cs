using SuperChess.Core;
using Xunit;

namespace SuperChess.Game.Tests;

/// <summary>
/// LessonController 五类课程目标判定测试：接受/拒绝/完成推进（设计 T1 第 3 步）。
/// </summary>
public class LessonControllerTests
{
    private const string CheckingFen = "4k4/9/9/9/9/3R5/9/3n5/9/3K5 w";
    private const string CaptureFen = "4k4/9/4N4/9/3c1p3/9/9/9/9/3K5 w";
    private const string MateFen = "3aka3/4c4/4R4/4C4/9/9/9/9/4C4/3K5 w";

    [Fact]
    public void AnyMoveOfPiece_AcceptsTargetPieceMove_RejectsOtherPiece()
    {
        var lesson = LessonLibrary.All.Single(l => l.Id == "move-knight");
        var board = Board.FromFen(lesson.Fen);
        var controller = new LessonController();
        controller.Start(lesson);

        var move = Rule.GetLegalMoves(board, new Position(4, 2)).Single(m => m.To == new Position(5, 4));
        Assert.True(controller.Evaluate(board, move).Accepted);
    }

    [Fact]
    public void AnyMoveOfPiece_RejectsMovingOtherPiece()
    {
        var lesson = LessonLibrary.All.Single(l => l.Id == "move-knight");
        var board = Board.FromFen(lesson.Fen);
        var controller = new LessonController();
        controller.Start(lesson);

        var result = controller.Evaluate(board, new Move(new Position(3, 9), new Position(3, 8)));
        Assert.False(result.Accepted);
        Assert.NotEmpty(result.Message);
    }

    [Fact]
    public void ExactMove_AcceptsOnlyGoalMove()
    {
        // 初始局面炮二平五（h2e2）为目标。
        var board = new Board();
        var controller = new LessonController();
        controller.Start(new LessonDefinition(
            "exact-test", "精确走子", "炮二平五", Fen.Initial,
            new LessonGoal(LessonGoalKind.ExactMove, From: new Position(7, 7), To: new Position(4, 7)),
            "完成"));

        Assert.True(controller.Evaluate(board, Move.FromUcci("h2e2")!.Value).Accepted);
        // 兵七进一（h3h4）：合法但不符合目标。
        Assert.False(controller.Evaluate(board, Move.FromUcci("h3h4")!.Value).Accepted);
    }

    [Fact]
    public void AnyCheckingMove_AcceptsOnlyCheckingMove()
    {
        var board = Board.FromFen(CheckingFen);
        var controller = new LessonController();
        controller.Start(new LessonDefinition(
            "check-test", "将军", "车四平五将军", CheckingFen,
            new LessonGoal(LessonGoalKind.AnyCheckingMove), "完成"));

        Assert.True(controller.Evaluate(board, Move.FromUcci("d4e4")!.Value).Accepted);
        Assert.False(controller.Evaluate(board, Move.FromUcci("d4a4")!.Value).Accepted);
    }

    [Fact]
    public void AnyCaptureMove_AcceptsAnyOrTargetCapture()
    {
        // 任意吃子：马吃炮（b5d5... 马 (4,2)→(3,4)）与马吃卒（(4,2)→(5,4)）均可。
        var board = Board.FromFen(CaptureFen);
        var controller = new LessonController();
        controller.Start(new LessonDefinition(
            "capture-test", "吃子", "马吃任意黑子", CaptureFen,
            new LessonGoal(LessonGoalKind.AnyCaptureMove), "完成"));

        Assert.True(controller.Evaluate(board, Move.FromUcci("e7d5")!.Value).Accepted);
        Assert.True(controller.Evaluate(board, Move.FromUcci("e7f5")!.Value).Accepted);
        Assert.False(controller.Evaluate(board, Move.FromUcci("e7g6")!.Value).Accepted);
    }

    [Fact]
    public void AnyCaptureMove_WithPieceCode_RejectsOtherCapture()
    {
        var board = Board.FromFen(CaptureFen);
        var controller = new LessonController();
        controller.Start(new LessonDefinition(
            "capture-target-test", "指定吃子", "马吃炮", CaptureFen,
            new LessonGoal(LessonGoalKind.AnyCaptureMove, Piece.BlackCannon), "完成"));

        Assert.True(controller.Evaluate(board, Move.FromUcci("e7d5")!.Value).Accepted);
        Assert.False(controller.Evaluate(board, Move.FromUcci("e7f5")!.Value).Accepted);
    }

    [Fact]
    public void AnyMateMove_AcceptsMateMove_RejectsNonMate()
    {
        var board = Board.FromFen(MateFen);
        var controller = new LessonController();
        controller.Start(new LessonDefinition(
            "mate-test", "一步杀", "重炮闷宫", MateFen,
            new LessonGoal(LessonGoalKind.AnyMateMove), "完成"));

        Assert.True(controller.Evaluate(board, Move.FromUcci("e7e8")!.Value).Accepted);
        Assert.False(controller.Evaluate(board, Move.FromUcci("e7d7")!.Value).Accepted);
    }

    [Fact]
    public void ConsumeApplied_MarksCompleted_RejectsFurtherMoves()
    {
        var lesson = LessonLibrary.All.Single(l => l.Id == "move-knight");
        var board = Board.FromFen(lesson.Fen);
        var controller = new LessonController();
        controller.Start(lesson);

        var move = Rule.GetLegalMoves(board, new Position(4, 2)).Single(m => m.To == new Position(5, 4));
        Assert.True(controller.Evaluate(board, move).Accepted);
        Assert.True(controller.ConsumeApplied());
        Assert.True(controller.IsCompleted);
        Assert.False(controller.Evaluate(board, move).Accepted);
    }

    [Fact]
    public void WithoutStart_RejectsAllMoves()
    {
        var controller = new LessonController();
        var result = controller.Evaluate(new Board(), Move.FromUcci("h2e2")!.Value);
        Assert.False(result.Accepted);
    }
}
