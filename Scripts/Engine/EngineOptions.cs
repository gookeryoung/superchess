namespace SuperChess.Engine;

/// <summary>
/// 引擎强度与资源选项（UCI setoption 封装）；null 字段不下发，保持引擎默认值。
/// </summary>
public sealed record EngineOptions
{
    /// <summary>搜索线程数（null 时由 Main 按 CPU 核数填充）。</summary>
    public int? Threads { get; init; }

    /// <summary>置换表大小（MB）。</summary>
    public int? HashMb { get; init; }

    /// <summary>NNUE 评估文件绝对路径（Android 上来自 nativeLibraryDir）。</summary>
    public string? EvalFile { get; init; }

    /// <summary>是否启用限棋力（UCI_LimitStrength）。</summary>
    public bool? LimitStrength { get; init; }

    /// <summary>限棋力时的 Elo 等级（UCI_Elo，Pikafish 支持 1280-3199）。</summary>
    public int? Elo { get; init; }

    /// <summary>Skill Level 弱化等级（与限棋力二选一使用）。</summary>
    public int? SkillLevel { get; init; }

    /// <summary>握手/选项同步超时（null 用默认 15 秒）。</summary>
    public TimeSpan? HandshakeTimeout { get; init; }
}
