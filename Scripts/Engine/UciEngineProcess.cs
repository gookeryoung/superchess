using System.Diagnostics;

namespace SuperChess.Engine;

/// <summary>
/// UCI 引擎子进程封装：启动外部引擎进程，通过 stdio 管道逐行读写。
/// 仅负责进程与管道生命周期，协议状态机由 UciSession（M4）承担。
/// </summary>
public sealed class UciEngineProcess : IDisposable
{
    private Process? _process;

    /// <summary>引擎标准输出的一行（含 info/bestmove 等）。</summary>
    public event Action<string>? LineReceived;

    /// <summary>引擎标准错误输出的一行。</summary>
    public event Action<string>? ErrorReceived;

    /// <summary>进程是否存活。</summary>
    public bool IsAlive => _process is { HasExited: false };

    /// <summary>
    /// 启动引擎进程并接入 stdio 管道。
    /// </summary>
    /// <param name="enginePath">引擎可执行文件绝对路径。</param>
    /// <param name="workingDirectory">引擎工作目录（null 则用引擎所在目录）。</param>
    /// <exception cref="InvalidOperationException">进程已在运行或启动失败。</exception>
    public void Start(string enginePath, string? workingDirectory = null)
    {
        if (IsAlive)
        {
            throw new InvalidOperationException("引擎进程已在运行");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = enginePath,
            Arguments = string.Empty,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(enginePath) ?? string.Empty,
        };
        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("引擎进程启动失败");

        _ = Task.Run(async () =>
        {
            try
            {
                while (await _process.StandardOutput.ReadLineAsync() is { } line)
                {
                    LineReceived?.Invoke(line);
                }
            }
            catch (Exception)
            {
                // 进程退出或管道关闭时读行会抛异常，属正常收尾，无需处理。
            }
        });
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                ErrorReceived?.Invoke(e.Data);
            }
        };
        _process.BeginErrorReadLine();
    }

    /// <summary>
    /// 向引擎写入一行命令（自动换行）。
    /// </summary>
    public void WriteLine(string line)
    {
        if (!IsAlive)
        {
            throw new InvalidOperationException("引擎进程未运行");
        }

        _process!.StandardInput.WriteLine(line);
    }

    /// <summary>
    /// 终止引擎进程并释放资源；进程未运行时静默返回。
    /// </summary>
    public void Stop()
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
        }
        catch (Exception)
        {
            // 进程可能已退出，忽略终止异常。
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    public void Dispose() => Stop();
}
