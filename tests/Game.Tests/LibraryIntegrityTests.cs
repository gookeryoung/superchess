using SuperChess.Core;
using Xunit;

namespace SuperChess.Game.Tests;

/// <summary>
/// 题库完整性测试（设计 T1 第 4 步）：课程 FEN 合法且目标可达；
/// 残局主线回放全部合法且末位用户着达成将死。运行时数据错误由此测试前置拦截。
/// </summary>
public class LibraryIntegrityTests
{
    [Fact]
    public void Lessons_FenValid_AndGoalReachable()
    {
        Assert.NotEmpty(LessonLibrary.All);
        foreach (var lesson in LessonLibrary.All)
        {
            var board = Board.FromFen(lesson.Fen);
            Assert.True(HasReachableGoal(board, lesson.Goal), $"课程 {lesson.Id} 目标不可达");
        }
    }

    [Fact]
    public void Puzzles_FenValid_MainlineLegalAndEndsWithCheckmate()
    {
        Assert.NotEmpty(PuzzleLibrary.All);
        foreach (var puzzle in PuzzleLibrary.All)
        {
            var board = Board.FromFen(puzzle.Fen);
            Assert.True(puzzle.Mainline.Length > 0, $"题目 {puzzle.Id} 主线为空");
            Assert.True(puzzle.Mainline.Length % 2 == 1, $"题目 {puzzle.Id} 主线应以用户着收尾");

            foreach (var ucci in puzzle.Mainline)
            {
                var move = Move.FromUcci(ucci);
                Assert.NotNull(move);
                Assert.True(
                    Rule.IsLegalMove(board, move!.Value),
                    $"题目 {puzzle.Id} 主线着法 {ucci} 在局面中非法");
                board.DoMove(move!.Value);
            }

            // 末位用户着（红方）走完后翻边，黑方被将死。
            Assert.True(
                Rule.IsCheckmate(board, board.RedToMove),
                $"题目 {puzzle.Id} 主线走完后未达成将死");
        }
    }

    /// <summary>目标可达性：存在至少一步合法走法满足课程目标。</summary>
    private static bool HasReachableGoal(Board board, LessonGoal goal)
    {
        var moves = Rule.GetAllLegalMoves(board, board.RedToMove);
        return goal.Kind switch
        {
            LessonGoalKind.AnyMoveOfPiece => moves.Any(m => board.GetPiece(m.From) == goal.PieceCode),
            LessonGoalKind.ExactMove => goal.From is { } from && goal.To is { } to
                && moves.Any(m => m.From == from && m.To == to),
            LessonGoalKind.AnyCheckingMove => moves.Any(m =>
            {
                var sim = board.Clone();
                sim.DoMove(m);
                return Rule.IsInCheck(sim, sim.RedToMove);
            }),
            LessonGoalKind.AnyMateMove => moves.Any(m =>
            {
                var sim = board.Clone();
                sim.DoMove(m);
                return Rule.IsCheckmate(sim, sim.RedToMove);
            }),
            LessonGoalKind.AnyCaptureMove => moves.Any(m =>
            {
                var captured = board.GetPiece(m.To);
                return Piece.IsValid(captured)
                    && Piece.IsRed(captured) != board.RedToMove
                    && (goal.PieceCode == 0 || captured == goal.PieceCode);
            }),
            _ => false,
        };
    }
}
