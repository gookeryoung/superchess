using System.Text.Json;

namespace SuperChess.Game;

/// <summary>
/// 残局练习进度模型（纯 C#，零 Godot 依赖）：按题目 Id 记录完成标记与尝试计数，
/// JSON 序列化供 Main 层写入 user:// 存储（Godot FileAccess，Android 为应用私有目录）。
/// </summary>
public sealed class PuzzleProgress
{
    private sealed record Entry(bool Solved, int Attempts, string LastDate);

    private readonly Dictionary<string, Entry> _entries;

    /// <summary>已过关的题目 Id 集合。</summary>
    public IReadOnlyCollection<string> SolvedIds =>
        _entries.Where(kv => kv.Value.Solved).Select(kv => kv.Key).ToList();

    /// <summary>已过关的题目数。</summary>
    public int SolvedCount => _entries.Values.Count(e => e.Solved);

    public PuzzleProgress()
    {
        _entries = [];
    }

    private PuzzleProgress(Dictionary<string, Entry> entries)
    {
        _entries = entries;
    }

    /// <summary>题目是否已过关。</summary>
    public bool IsSolved(string puzzleId) =>
        _entries.TryGetValue(puzzleId, out var entry) && entry.Solved;

    /// <summary>题目累计尝试次数（未挑战过为 0）。</summary>
    public int Attempts(string puzzleId) =>
        _entries.TryGetValue(puzzleId, out var entry) ? entry.Attempts : 0;

    /// <summary>记录一次挑战（打开题目时调用，计数 +1）。</summary>
    public void MarkAttempt(string puzzleId) => Bump(puzzleId, solved: null);

    /// <summary>标记过关（幂等：重复过关仅刷新日期，不重复计数）。</summary>
    public void MarkSolved(string puzzleId)
    {
        Bump(puzzleId, solved: true);
    }

    /// <summary>序列化为 JSON（供 Main 写入 user://）。</summary>
    public string ToJson() => JsonSerializer.Serialize(_entries);

    /// <summary>
    /// 从 JSON 反序列化；输入为空或非法时返回空进度（进度文件损坏不阻塞启动）。
    /// </summary>
    public static PuzzleProgress FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new PuzzleProgress();
        }

        try
        {
            var entries = JsonSerializer.Deserialize<Dictionary<string, Entry>>(json);
            return new PuzzleProgress(entries ?? []);
        }
        catch (JsonException)
        {
            return new PuzzleProgress();
        }
    }

    /// <summary>更新条目：solved 仅在 true 时置位（null 表示不动）；日期取当日。</summary>
    private void Bump(string puzzleId, bool? solved)
    {
        var existing = _entries.TryGetValue(puzzleId, out var entry) ? entry : null;
        var isSolved = solved == true || (existing?.Solved ?? false);
        var attempts = (existing?.Attempts ?? 0) + (solved is null ? 1 : 0);
        _entries[puzzleId] = new Entry(isSolved, attempts, DateTime.Now.ToString("yyyy-MM-dd"));
    }
}
