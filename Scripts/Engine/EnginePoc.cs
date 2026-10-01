using System.Diagnostics;
using System.Threading.Channels;
using Godot;

namespace SuperChess.Engine;

/// <summary>
/// M1 引擎通信 PoC：真机上验证 Process 启动 Pikafish、UCI 握手、NNUE 加载与 go/stop。
/// 临时验证代码，M4 由 UciSession 正式实现后移除。
/// </summary>
public static class EnginePoc
{
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 异步执行握手探针并逐项输出结果（Godot 日志，真机可在 logcat 查看）。
    /// </summary>
    public static void Run()
    {
        _ = RunAsync();
    }

    private static async Task RunAsync()
    {
        var report = new List<string>();
        try
        {
            var enginePath = EngineLocator.ResolveEnginePath();
            if (enginePath is null)
            {
                GD.Print("[EnginePoC] 未找到引擎（非 Android 平台或 jniLibs 未注入），跳过");
                return;
            }

            var nnuePath = EngineLocator.ResolveNnuePath();
            report.Add($"nativeDir={Path.GetDirectoryName(enginePath)}");
            report.Add($"engine={Path.GetFileName(enginePath)} dotprod={EngineLocator.HasDotProd()}");
            report.Add($"nnue={(nnuePath is null ? "缺失" : "存在")}");

            using var engine = new UciEngineProcess();
            var lines = Channel.CreateUnbounded<string>();
            engine.LineReceived += line => _ = lines.Writer.WriteAsync(line);
            engine.ErrorReceived += line => GD.Print($"[EnginePoC][stderr] {line}");
            engine.Start(enginePath);

            var stopwatch = Stopwatch.StartNew();
            engine.WriteLine("uci");
            var uciok = await WaitForLineAsync(lines, "uciok", HandshakeTimeout);
            report.Add($"uciok={uciok} ({stopwatch.ElapsedMilliseconds}ms)");
            if (!uciok)
            {
                Finish(report);
                return;
            }

            if (nnuePath is not null)
            {
                engine.WriteLine($"setoption name EvalFile value {nnuePath}");
            }

            stopwatch.Restart();
            engine.WriteLine("isready");
            var readyok = await WaitForLineAsync(lines, "readyok", HandshakeTimeout);
            report.Add($"readyok={readyok} ({stopwatch.ElapsedMilliseconds}ms)");

            stopwatch.Restart();
            engine.WriteLine("position startpos");
            engine.WriteLine("go depth 10");
            var bestmove = await WaitForLineAsync(lines, "bestmove", SearchTimeout);
            report.Add($"bestmove={bestmove} ({stopwatch.ElapsedMilliseconds}ms)");
            if (bestmove)
            {
                // stop 中断验证：infinite 搜索后发 stop，应仍返回 bestmove。
                stopwatch.Restart();
                engine.WriteLine("go infinite");
                await Task.Delay(1500);
                engine.WriteLine("stop");
                var stopped = await WaitForLineAsync(lines, "bestmove", SearchTimeout);
                report.Add($"stop_bestmove={stopped} ({stopwatch.ElapsedMilliseconds}ms)");
            }

            engine.WriteLine("quit");
        }
        catch (Exception e)
        {
            report.Add($"EXCEPTION={e.GetType().Name}: {e.Message}");
        }

        Finish(report);
    }

    private static async Task<bool> WaitForLineAsync(Channel<string> lines, string token, TimeSpan timeout)
    {
        try
        {
            var deadline = DateTime.UtcNow + timeout;
            while (true)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    return false;
                }

                if (await lines.Reader.WaitToReadAsync(new CancellationTokenSource(remaining).Token))
                {
                    while (lines.Reader.TryRead(out var line))
                    {
                        if (line.Contains(token, StringComparison.Ordinal))
                        {
                            if (token == "bestmove" || token == "uciok")
                            {
                                GD.Print($"[EnginePoC] {line}");
                            }

                            return true;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 超时，返回 false。
        }

        return false;
    }

    private static void Finish(List<string> report)
    {
        GD.Print($"[EnginePoC] 结果：{string.Join(" | ", report)}");
    }
}
