using System.Text.RegularExpressions;
using SuperChess.Core;

namespace SuperChess.Manual;

/// <summary>
/// PGN（ICCS 坐标记谱）解析器：解析 [Tag] 标签与着法序列构建棋谱树；
/// 检测到中文记谱着法时明确报「暂不支持中文记谱 PGN」，其余非法着法按格式错误处理。
/// 变着括号、评注以外的扩展语法本期不支持（与参考项目 PGNManual 行为对齐）。
/// </summary>
public static partial class PgnParser
{
    /// <summary>花括号评注（可能跨行，解析着法前整体剔除）。</summary>
    [GeneratedRegex(@"\{[^}]*\}", RegexOptions.Singleline)]
    private static partial Regex CommentPattern();

    /// <summary>ICCS 坐标着法：4 字符 [a-i][0-9][a-i][0-9]。</summary>
    [GeneratedRegex(@"^[a-i][0-9][a-i][0-9]$")]
    private static partial Regex IccsPattern();

    /// <summary>回合号 token（如 "1."、"12..."）。</summary>
    [GeneratedRegex(@"^\d+\.*$")]
    private static partial Regex MoveNumberPattern();

    private static readonly string[] ResultTokens = ["1-0", "0-1", "1/2-1/2", "*"];

    /// <summary>解析 PGN 文本；magic/FEN 非法或着法不受支持抛 ManualFormatException。</summary>
    public static ManualDocument Parse(string text)
    {
        var (tags, movesSection) = SplitTagsAndMoves(text);

        var doc = new ManualDocument(ResolveInitialBoard(tags))
        {
            Event = tags.GetValueOrDefault("Event", string.Empty),
            Date = tags.GetValueOrDefault("Date", string.Empty),
            Site = tags.GetValueOrDefault("Site", string.Empty),
            Red = tags.GetValueOrDefault("Red", string.Empty),
            Black = tags.GetValueOrDefault("Black", string.Empty),
            Result = tags.GetValueOrDefault("Result", string.Empty),
        };

        var node = doc.Root;
        foreach (var token in TokenizeMoves(movesSection))
        {
            var move = Move.FromUcci(token)
                ?? throw new ManualFormatException(
                    char.IsAscii(token[0])
                        ? $"不支持的 PGN 着法「{token}」（仅支持 ICCS 坐标记谱）"
                        : $"暂不支持中文记谱 PGN（检测到非 ICCS 着法「{token}」）");
            node = MoveNode.Append(move, node);
        }

        return doc;
    }

    /// <summary>分离 [Tag "value"] 标签区与其余着法文本。</summary>
    private static (Dictionary<string, string> Tags, string MovesSection) SplitTagsAndMoves(string text)
    {
        var tags = new Dictionary<string, string>();
        var moves = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']') && trimmed.Contains('"'))
            {
                var tag = ParseTagLine(trimmed);
                if (tag is { } pair)
                {
                    tags[pair.Key] = pair.Value;
                    continue;
                }
            }

            moves.Add(line);
        }

        return (tags, string.Join('\n', moves));
    }

    /// <summary>解析单行标签；格式不符返回 null（该行归入着法段）。</summary>
    private static KeyValuePair<string, string>? ParseTagLine(string line)
    {
        var open = line.IndexOf('"');
        var close = line.LastIndexOf('"');
        if (open < 0 || close <= open)
        {
            return null;
        }

        var key = line[1..open].Trim();
        return key.Length == 0 ? null : new KeyValuePair<string, string>(key, line[(open + 1)..close]);
    }

    /// <summary>从 FEN 标签构造初始局面；无标签用标准起始局面，FEN 非法抛 ManualFormatException。</summary>
    private static Board ResolveInitialBoard(Dictionary<string, string> tags)
    {
        if (!tags.TryGetValue("FEN", out var fen) || fen.Length == 0)
        {
            return new Board();
        }

        try
        {
            return Board.FromFen(fen);
        }
        catch (FenFormatException e)
        {
            throw new ManualFormatException($"PGN FEN 标签非法：{e.Message}");
        }
    }

    /// <summary>剔除花括号评注后按空白切分着法 token，过滤回合号与结果标记。</summary>
    private static List<string> TokenizeMoves(string movesSection)
    {
        var cleaned = CommentPattern().Replace(movesSection, " ");
        var tokens = new List<string>();
        foreach (var token in cleaned.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (MoveNumberPattern().IsMatch(token) || ResultTokens.Contains(token))
            {
                continue;
            }

            tokens.Add(token);
        }

        return tokens;
    }
}
