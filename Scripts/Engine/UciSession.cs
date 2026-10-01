using System.Diagnostics;

namespace SuperChess.Engine;

/// <summary>一次搜索请求的参数。</summary>
public sealed record GoParams
{
    /// <summary>搜索起始局面的 FEN（6 字段，走子方为引擎思考的一方）。</summary>
    public required string Fen { get; init; }

    /// <summary>FEN 之后追加的 UCCI 着法序列（null/空 = 无）。</summary>
    public IReadOnlyList<string>? Moves { get; init; }

    /// <summary>固定搜索深度。</summary>
    public int? Depth { get; init; }

    /// <summary>固定思考时间（毫秒）。</summary>
    public int? MoveTimeMs { get; init; }

    /// <summary>无限搜索（配合 Stop 中断）。</summary>
    public bool Infinite { get; init; }

    /// <summary>本次搜索的 MultiPV 数（&gt;1 时先下发 setoption MultiPV）。</summary>
    public int? MultiPv { get; init; }
}

/// <summary>
/// 搜索结果：bestmove 与可选 ponder 着法（UCCI 字符串）。
/// 终局引擎回 "(none)" 时 BestMove 为 null。
/// </summary>
public readonly record struct SearchResult(string? BestMove, string? PonderMove);

/// <summary>
/// 一条 MultiPV 评估信息（解析自 info 行；分值为走子方视角，M5 显示时按需换边）。
/// </summary>
public sealed record MultiPvInfo(
    int Index,
    int Depth,
    int ScoreCp,
    bool IsMate,
    bool UpperBound,
    bool LowerBound,
    IReadOnlyList<string> Pv);

/// <summary>UCI 引擎会话抽象（GameSession 依赖此接口以便测试替换实现）。</summary>
public interface IUciSession : IAsyncDisposable
{
    /// <summary>握手与选项同步是否已完成。</summary>
    bool IsReady { get; }

    /// <summary>收到含 PV 数据的 info 行时触发（多行 info 高频触发，调用方自行节流）。</summary>
    event Action<MultiPvInfo>? InfoReceived;

    /// <summary>启动引擎进程并完成 uci→setoption→isready 握手；超时或进程异常抛出。</summary>
    Task StartAsync(string enginePath, EngineOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// 发起一次搜索并等待 bestmove。
    /// 取消令牌触发时发送 stop 并继续等待引擎回当前最优 bestmove（不视为错误）。
    /// </summary>
    Task<SearchResult> GoAsync(GoParams parameters, CancellationToken ct = default);

    /// <summary>同步下发选项并等待 readyok。</summary>
    Task ApplyOptionsAsync(EngineOptions options, CancellationToken ct = default);

    /// <summary>请求引擎中断当前搜索（引擎仍会返回 bestmove）。</summary>
    void Stop();
}

/// <summary>
/// UCI 协议会话：握手、setoption、position+go、stop、info/bestmove 解析。
/// 解析逻辑移植自参考项目 ComputerPlayer.parseInfoCmd。
/// 事件回调可能来自引擎读行线程，await 续体回到调用方同步上下文（Godot 主线程安全）。
/// </summary>
public sealed class UciSession : IUciSession
{
    private static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(15);

    private readonly UciEngineProcess _process = new();
    private readonly SemaphoreSlim _searchLock = new(1, 1);
    private TaskCompletionSource? _uciokTcs;
    private TaskCompletionSource? _readyokTcs;
    private TaskCompletionSource<SearchResult>? _bestmoveTcs;
    private bool _disposed;

    /// <inheritdoc />
    public bool IsReady { get; private set; }

    /// <inheritdoc />
    public event Action<MultiPvInfo>? InfoReceived;

    /// <inheritdoc />
    public async Task StartAsync(string enginePath, EngineOptions? options = null, CancellationToken ct = default)
    {
        ObjectDisposedThrow();

        options ??= new EngineOptions();
        var timeout = options.HandshakeTimeout ?? DefaultHandshakeTimeout;

        _process.LineReceived += HandleLine;
        _process.Disconnected += OnDisconnected;
        _process.Start(enginePath);

        _uciokTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _process.WriteLine("uci");
        using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        handshakeCts.CancelAfter(timeout);
        try
        {
            await _uciokTcs.Task.WaitAsync(handshakeCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("引擎握手超时（未收到 uciok）");
        }

        await ApplyOptionsAsync(options, ct).ConfigureAwait(true);
        IsReady = true;
    }

    /// <inheritdoc />
    public async Task ApplyOptionsAsync(EngineOptions options, CancellationToken ct = default)
    {
        ObjectDisposedThrow();
        var timeout = options.HandshakeTimeout ?? DefaultHandshakeTimeout;

        if (options.Threads is { } threads)
        {
            _process.WriteLine($"setoption name Threads value {threads}");
        }

        if (options.HashMb is { } hash)
        {
            _process.WriteLine($"setoption name Hash value {hash}");
        }

        if (!string.IsNullOrEmpty(options.EvalFile))
        {
            _process.WriteLine($"setoption name EvalFile value {options.EvalFile}");
        }

        if (options.LimitStrength is { } limit)
        {
            _process.WriteLine($"setoption name UCI_LimitStrength value {(limit ? "true" : "false")}");
        }

        if (options.Elo is { } elo)
        {
            _process.WriteLine($"setoption name UCI_Elo value {elo}");
        }

        if (options.SkillLevel is { } skill)
        {
            _process.WriteLine($"setoption name Skill Level value {skill}");
        }

        _readyokTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _process.WriteLine("isready");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            await _readyokTcs.Task.WaitAsync(cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("引擎选项同步超时（未收到 readyok）");
        }
    }

    /// <inheritdoc />
    public async Task<SearchResult> GoAsync(GoParams parameters, CancellationToken ct = default)
    {
        ObjectDisposedThrow();
        await _searchLock.WaitAsync(ct).ConfigureAwait(true);
        try
        {
            if (_bestmoveTcs is not null)
            {
                throw new InvalidOperationException("已有搜索在进行中");
            }

            if (parameters.MultiPv is { } multiPv)
            {
                _process.WriteLine($"setoption name MultiPV value {multiPv}");
            }

            var position = $"position fen {parameters.Fen}";
            if (parameters.Moves is { Count: > 0 })
            {
                position += " moves " + string.Join(' ', parameters.Moves);
            }

            _process.WriteLine(position);
            _process.WriteLine(BuildGoCommand(parameters));

            _bestmoveTcs = new TaskCompletionSource<SearchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = ct.Register(static s => ((UciSession)s!).Stop(), this);
            return await _bestmoveTcs.Task.ConfigureAwait(true);
        }
        finally
        {
            _bestmoveTcs = null;
            _searchLock.Release();
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (_bestmoveTcs is not null)
        {
            _process.WriteLine("stop");
        }
    }

    /// <summary>组装 go 命令（depth / movetime / infinite 三选一）。</summary>
    private static string BuildGoCommand(GoParams parameters)
    {
        if (parameters.Infinite)
        {
            return "go infinite";
        }

        if (parameters.MoveTimeMs is { } moveTime)
        {
            return $"go movetime {moveTime}";
        }

        return $"go depth {parameters.Depth ?? 12}";
    }

    /// <summary>
    /// 处理引擎输出行：uciok/readyok 完成握手，info 解析 MultiPV，bestmove 结束搜索。
    /// </summary>
    private void HandleLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        switch (tokens[0])
        {
            case "uciok":
                _uciokTcs?.TrySetResult();
                break;

            case "readyok":
                _readyokTcs?.TrySetResult();
                break;

            case "info":
                if (ParseInfo(tokens) is { } info)
                {
                    InfoReceived?.Invoke(info);
                }

                break;

            case "bestmove":
                var best = tokens.Length > 1 ? tokens[1] : string.Empty;
                var ponder = tokens.Length >= 4 && tokens[2] == "ponder" ? tokens[3] : null;
                _bestmoveTcs?.TrySetResult(new SearchResult(
                    best.Length == 0 || best == "(none)" ? null : best,
                    ponder));
                break;
        }
    }

    /// <summary>引擎进程退出时故障化挂起的搜索，避免调用方永久挂起。</summary>
    private void OnDisconnected()
    {
        _uciokTcs?.TrySetCanceled();
        _readyokTcs?.TrySetCanceled();
        _bestmoveTcs?.TrySetException(new InvalidOperationException("引擎进程已退出"));
    }

    /// <summary>
    /// 解析 info 行为 MultiPvInfo（移植 parseInfoCmd）；不含 pv 数据时返回 null。
    /// 分值/着法序列为走子方视角。
    /// </summary>
    private MultiPvInfo? ParseInfo(string[] tokens)
    {
        var depth = 0;
        var scoreCp = 0;
        var isMate = false;
        var upperBound = false;
        var lowerBound = false;
        var pvIndex = 0;
        List<string>? pv = null;

        try
        {
            var i = 1;
            while (i < tokens.Length - 1)
            {
                switch (tokens[i++])
                {
                    case "depth":
                        depth = int.Parse(tokens[i++], System.Globalization.CultureInfo.InvariantCulture);
                        break;

                    case "multipv":
                        pvIndex = Math.Clamp(int.Parse(tokens[i++], System.Globalization.CultureInfo.InvariantCulture) - 1, 0, 255);
                        break;

                    case "score":
                        isMate = tokens[i++] == "mate";
                        scoreCp = int.Parse(tokens[i++], System.Globalization.CultureInfo.InvariantCulture);
                        if (tokens[i] == "upperbound")
                        {
                            upperBound = true;
                            i++;
                        }
                        else if (tokens[i] == "lowerbound")
                        {
                            lowerBound = true;
                            i++;
                        }

                        break;

                    case "pv":
                        pv = new List<string>();
                        while (i < tokens.Length)
                        {
                            pv.Add(tokens[i++]);
                        }

                        break;

                    default:
                        // 其余字段（seldepth/time/nodes/currmove 等）MVP 不消费；与参考项目一致，
                        // 未知关键字仅跳过自身，不额外跳过后续 token。
                        break;
                }
            }
        }
        catch (FormatException)
        {
            // 引擎输出异常字段时忽略整行（与参考项目一致）。
            return null;
        }
        catch (Exception e) when (e is OverflowException or IndexOutOfRangeException)
        {
            return null;
        }

        if (pv is null)
        {
            return null;
        }

        return new MultiPvInfo(pvIndex + 1, depth, scoreCp, isMate, upperBound, lowerBound, pv);
    }

    private void ObjectDisposedThrow()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UciSession));
        }
    }

    /// <summary>通知引擎退出并终止进程。</summary>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        IsReady = false;
        try
        {
            _process.WriteLine("quit");
        }
        catch (InvalidOperationException)
        {
            // 进程已退出，直接终止即可。
        }

        _process.LineReceived -= HandleLine;
        _process.Disconnected -= OnDisconnected;
        _process.Dispose();
        _searchLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
