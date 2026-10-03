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
    public void Puzzles_FenValid_TreeLegalAndEndsWithCheckmate()
    {
        Assert.True(PuzzleLibrary.All.Count >= 30,
            $"题库共 {PuzzleLibrary.All.Count} 题，AC-P4 要求 ≥30 题");
        foreach (var puzzle in PuzzleLibrary.All)
        {
            var board = Board.FromFen(puzzle.Fen);
            Assert.True(puzzle.Difficulty >= 1 && puzzle.Difficulty <= 4,
                $"题目 {puzzle.Id} 难度 {puzzle.Difficulty} 越界");
            ReplayChildren(board, puzzle.Root, puzzle.Id);
        }
    }

    [Fact]
    public void Puzzles_IdsUnique()
    {
        var ids = PuzzleLibrary.All.Select(p => p.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    /// <summary>
    /// 递归回放着法树所有路径：用户着层（父深度偶）允许并列正解分支，
    /// 防守着层恒单线；用户着收尾（无后续防守）必须达成将死。
    /// </summary>
    private static void ReplayChildren(Board board, PuzzleMoveNode node, string puzzleId)
    {
        if (node.Children.Count == 0)
        {
            if (!node.IsRoot)
            {
                Assert.True(node.Depth % 2 == 1,
                    $"题目 {puzzleId} 树在防守着深度 {node.Depth} 处中断");
            }

            return;
        }

        if (node.Depth % 2 == 1)
        {
            Assert.True(node.Children.Count == 1,
                $"题目 {puzzleId} 用户着层出现多防守分支（深度 {node.Depth}）");
        }

        foreach (var child in node.Children)
        {
            var move = Move.FromUcci(child.Ucci);
            Assert.True(move is not null,
                $"题目 {puzzleId} 节点含非法 UCCI：{child.Ucci}");
            Assert.True(Rule.IsLegalMove(board, move!.Value),
                $"题目 {puzzleId} 着法 {child.Ucci}（深度 {child.Depth}）在局面中非法");
            var sim = board.Clone();
            sim.DoMove(move!.Value);
            if (child.Depth % 2 == 1 && child.Children.Count == 0)
            {
                Assert.True(Rule.IsCheckmate(sim, sim.RedToMove),
                    $"题目 {puzzleId} 用户着 {child.Ucci} 收尾未达成将死");
            }
            else
            {
                ReplayChildren(sim, child, puzzleId);
            }
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
