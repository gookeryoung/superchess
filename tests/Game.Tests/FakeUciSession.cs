using SuperChess.Engine;
using Xunit;

namespace SuperChess.Game.Tests;

/// <summary>
/// 可控的 IUciSession 测试替身：GoAsync 挂起等待测试用例放行（Release），
/// 用于驱动 GameSession 的忙闲门闸与并发边界。
/// </summary>
public sealed class FakeUciSession : IUciSession
{
    private readonly TaskCompletionSource _startCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsReady { get; private set; }

    public int StopCount { get; private set; }

    public bool Disposed { get; private set; }

    public List<GoParams> Requests { get; } = [];

    /// <summary>GoAsync 被调用时置位（测试据此确认引擎已进入思考）。</summary>
    public TaskCompletionSource GoRequested { get; private set; } = NewTcs();

    /// <summary>挂起的搜索由测试放行时置位。</summary>
    private TaskCompletionSource<SearchResult>? PendingSearch { get; set; }

    public event Action<MultiPvInfo>? InfoReceived;

    /// <summary>测试用：模拟引擎发出一条 info 评估信息。</summary>
    public void EmitInfo(MultiPvInfo info) => InfoReceived?.Invoke(info);

    public Task StartAsync(string enginePath, EngineOptions? options = null, CancellationToken ct = default)
    {
        IsReady = true;
        _startCompleted.TrySetResult();
        return Task.CompletedTask;
    }

    public Task ApplyOptionsAsync(EngineOptions options, CancellationToken ct = default) => Task.CompletedTask;

    public Task<SearchResult> GoAsync(GoParams parameters, CancellationToken ct = default)
    {
        Requests.Add(parameters);
        PendingSearch = new TaskCompletionSource<SearchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        GoRequested.TrySetResult();
        return PendingSearch.Task;
    }

    public void Stop() => StopCount++;

    public void Release(string bestMove) => PendingSearch?.TrySetResult(new SearchResult(bestMove, null));

    /// <summary>等待下一次 GoAsync 被调用（带超时防测试挂死）。</summary>
    public async Task WaitGoRequestedAsync()
    {
        await GoRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }

    private static TaskCompletionSource NewTcs() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
