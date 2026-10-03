using Xunit;

namespace SuperChess.Game.Tests;

/// <summary>
/// 残局进度模型测试：完成标记/尝试计数/JSON 往返/幂等过关/损坏输入容错。
/// </summary>
public class PuzzleProgressTests
{
    [Fact]
    public void MarkSolved_SetsFlagAndCount()
    {
        var progress = new PuzzleProgress();
        Assert.False(progress.IsSolved("p1"));
        Assert.Equal(0, progress.SolvedCount);

        progress.MarkSolved("p1");
        Assert.True(progress.IsSolved("p1"));
        Assert.Equal(1, progress.SolvedCount);
        Assert.Contains("p1", progress.SolvedIds);
    }

    [Fact]
    public void MarkSolved_Idempotent_DoesNotDuplicate()
    {
        var progress = new PuzzleProgress();
        progress.MarkSolved("p1");
        progress.MarkSolved("p1");

        Assert.Equal(1, progress.SolvedCount);
        Assert.Equal(0, progress.Attempts("p1"));
    }

    [Fact]
    public void MarkAttempt_IncrementsCounter()
    {
        var progress = new PuzzleProgress();
        progress.MarkAttempt("p1");
        progress.MarkAttempt("p1");
        progress.MarkAttempt("p1");

        Assert.Equal(3, progress.Attempts("p1"));
        Assert.False(progress.IsSolved("p1"));
    }

    [Fact]
    public void MarkSolved_PreservesExistingAttempts()
    {
        var progress = new PuzzleProgress();
        progress.MarkAttempt("p1");
        progress.MarkAttempt("p1");
        progress.MarkSolved("p1");

        Assert.Equal(2, progress.Attempts("p1"));
        Assert.True(progress.IsSolved("p1"));
    }

    [Fact]
    public void JsonRoundTrip_PreservesAllEntries()
    {
        var progress = new PuzzleProgress();
        progress.MarkAttempt("p1");
        progress.MarkAttempt("p1");
        progress.MarkSolved("p2");

        var restored = PuzzleProgress.FromJson(progress.ToJson());
        Assert.True(restored.IsSolved("p2"));
        Assert.Equal(2, restored.Attempts("p1"));
        Assert.False(restored.IsSolved("p1"));
        Assert.Equal(1, restored.SolvedCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-json{{{")]
    [InlineData("[]")]
    public void FromJson_InvalidInput_ReturnsEmptyProgress(string json)
    {
        var progress = PuzzleProgress.FromJson(json);
        Assert.Equal(0, progress.SolvedCount);
        Assert.Equal(0, progress.Attempts("p1"));
    }

    [Fact]
    public void FromJson_NullObject_ReturnsEmptyProgress()
    {
        var progress = PuzzleProgress.FromJson("null");
        Assert.Equal(0, progress.SolvedCount);
    }
}
