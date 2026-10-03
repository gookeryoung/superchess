using SuperChess.Manual;
using Xunit;

namespace SuperChess.Manual.Tests;

/// <summary>
/// PGN(ICCS) 解析测试：ICCS 正例（自制 + FEN 标签）与中文记谱拒绝例
/// （真实样例 sample_02，GBK 编码，参考项目棋谱目录）。
/// </summary>
public class PgnParserTests
{
    private static string ReadText(string name) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "TestData", name));

    [Fact]
    public void Parse_IccsWithFen_ParsesTagsAndMoves()
    {
        var doc = PgnParser.Parse(ReadText("manual_iccs.pgn"));
        Assert.Equal("SuperChess 测试对局", doc.Event);
        Assert.Equal("红方测试", doc.Red);
        Assert.Equal("黑方测试", doc.Black);
        Assert.Equal("1-0", doc.Result);

        var iccs = new List<string>();
        for (var node = doc.Root.Children[0]; ; node = node.Children[0])
        {
            iccs.Add(node.Move!.Value.ToUcci());
            if (node.Children.Count == 0)
            {
                break;
            }
        }

        Assert.Equal(["h2e2", "h9g7", "h0g2", "b9c7", "i0h0", "i9h9"], iccs);
        Assert.True(doc.ValidateAllMoves());
    }

    [Fact]
    public void Parse_IccsWithoutFen_UsesStandardInitialBoard()
    {
        var doc = PgnParser.Parse("[Event \"t\"]\n\n1. h2e2 *\n");
        Assert.Equal("t", doc.Event);
        Assert.Single(doc.Root.Children);
        Assert.Equal("h2e2", doc.Root.Children[0].Move!.Value.ToUcci());
        Assert.True(doc.ValidateAllMoves());
    }

    [Fact]
    public void Parse_IccsBlockComment_CommentIgnored()
    {
        // 花括号评注（跨行）应整体剔除，不影响着法解析（评注内含换行）
        var doc = PgnParser.Parse("[Event \"t\"]\n\n1. h2e2 {红方\n架中炮} 1... h9g7 *\n");
        var first = Assert.Single(doc.Root.Children);
        Assert.Equal("h2e2", first.Move!.Value.ToUcci());
        var second = Assert.Single(first.Children);
        Assert.Equal("h9g7", second.Move!.Value.ToUcci());
    }

    [Fact]
    public void Parse_ChineseNotation_ThrowsWithClearMessage()
    {
        // 真实中文记谱 PGN（GBK 编码），着法段为「炮二平五」等中文记谱 token
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestData", "sample_02_chinese_pgn.pgn"));
        var text = System.Text.Encoding.GetEncoding("GB18030").GetString(bytes);
        var e = Assert.Throws<ManualFormatException>(() => PgnParser.Parse(text));
        Assert.Contains("暂不支持中文记谱", e.Message);
    }

    [Theory]
    [InlineData("[Event \"t\"]\n\n1. xx99 *\n")]
    [InlineData("[Event \"t\"]\n\n1. h2e2z *\n")]
    public void Parse_InvalidToken_Throws(string text)
    {
        Assert.Throws<ManualFormatException>(() => PgnParser.Parse(text));
    }

    [Fact]
    public void Parse_InvalidFen_Throws()
    {
        var e = Assert.Throws<ManualFormatException>(
            () => PgnParser.Parse("[FEN \"not-a-fen\"]\n\n1. h2e2 *\n"));
        Assert.Contains("FEN", e.Message);
    }
}
