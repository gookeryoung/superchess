using SuperChess.Core;
using SuperChess.Manual;
using Xunit;

namespace SuperChess.Manual.Tests;

/// <summary>
/// XQF 解析测试：真实样例 sample_07（v16 加密、含多处变着与节点注释）+
/// 自制最小样例（v10 无加密 / v16 加密），对拍基准为 build/dump_xqf_tree.py 手工解码输出。
/// </summary>
public class XqfParserTests
{
    /// <summary>sample_07 主线 33 步（ICCS），来源：cchess read_xqf 与手工解码器双对拍一致。</summary>
    private static readonly string[] Sample07Mainline =
    [
        "g0e2", "h7e7", "h0g2", "h9g7", "i0h0", "i9h9", "b0c2", "b9a7", "h2h6", "g6g5",
        "c3c4", "b7d7", "a0b0", "a9b9", "b2b6", "d7d2", "e2g0", "a6a5", "d0e1", "d2d3",
        "h6g6", "f9e8", "h0h9", "g7h9", "g0e2", "d3c3", "i3i4", "g9i7", "g3g4", "g5g4",
        "e2g4", "h9f8", "g2f4",
    ];

    private static ManualDocument Load(string name) =>
        XqfParser.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestData", name)));

    /// <summary>取节点走法的 UCCI 串（断言前置前提：节点必须持有走法）。</summary>
    private static string U(MoveNode node) => node.Move!.Value.ToUcci();

    /// <summary>递归统计树中带走法的节点总数。</summary>
    private static int CountNodes(MoveNode node)
    {
        var count = node.Move is null ? 0 : 1;
        foreach (var child in node.Children)
        {
            count += CountNodes(child);
        }

        return count;
    }

    /// <summary>沿第一个子节点收集主线 ICCS 序列。</summary>
    private static List<string> Mainline(MoveNode root)
    {
        var moves = new List<string>();
        for (var node = root.Children.Count > 0 ? root.Children[0] : null;
             node?.Move is not null;
             node = node.Children.Count > 0 ? node.Children[0] : null)
        {
            moves.Add(node.Move.Value.ToUcci());
        }

        return moves;
    }

    [Fact]
    public void Parse_Sample07RealEncrypted_MetadataAndMainline()
    {
        var doc = Load("sample_07_XQStudio.xqf");
        Assert.Equal(16, doc.Version);
        Assert.Equal("布局", doc.Category);
        Assert.Equal("未知", doc.Result);
        Assert.Equal(string.Empty, doc.Title);
        Assert.Equal(string.Empty, doc.Red);
        Assert.Null(doc.Annotation);
        Assert.Equal(Sample07Mainline, Mainline(doc.Root));
        Assert.Equal(135, CountNodes(doc.Root));
        Assert.True(doc.ValidateAllMoves());
    }

    [Fact]
    public void Parse_Sample07RealEncrypted_HasVariations()
    {
        // 树形基准（手工解码）：变着语义为「flag 记录的替代着」——
        // d7d2 变着 a7c8（注释「2」）、a6a5 变着 f9e8、d0e1 变着 c2d4；
        // c3c4 虽有 var 标志但变着区在文件尾已耗尽，实际无变着分支。
        var doc = Load("sample_07_XQStudio.xqf");
        var node = doc.Root.Children[0];
        for (var i = 1; i < 11; i++)
        {
            node = node.Children[0];
        }

        Assert.Equal("c3c4", U(node));
        Assert.Single(node.Children);
        Assert.Equal("b7d7", U(node.Children[0]));

        // c3c4 主线下探 5 步到 d7d2；其 var 对应的变着 a7c8 是 d7d2 的兄弟分支
        // （父节点 b2b6 的第二个子节点），注解原始字节「1\r\n」经 ReadString 去首尾空白后为「1」
        var d7d2 = node.Children[0].Children[0].Children[0].Children[0].Children[0];
        Assert.Equal("d7d2", U(d7d2));
        Assert.Equal("1", d7d2.Annotation);
        var b2b6 = d7d2.Parent!;
        Assert.Equal("b2b6", U(b2b6));
        Assert.Equal(2, b2b6.Children.Count);
        Assert.Equal("a7c8", U(b2b6.Children[1]));
        Assert.Equal("2", b2b6.Children[1].Annotation);
        Assert.Equal("b6b8", U(b2b6.Children[1].Children[0]));

        // a6a5 的 var → 变着 f9e8（父 e2g0 的第二子）；d0e1 的 var → 变着 c2d4（父 a6a5 的第二子）
        var e2g0 = d7d2.Children[0];
        Assert.Equal("e2g0", U(e2g0));
        var a6a5 = e2g0.Children[0];
        Assert.Equal("a6a5", U(a6a5));
        Assert.Equal("f9e8", U(e2g0.Children[1]));
        Assert.Equal("c2d4", U(a6a5.Children[1]));
    }

    [Fact]
    public void Parse_LowVersionV10_NoEncryptionWithVariationAndAnnotation()
    {
        var doc = Load("test_manual_v10.xqf");
        Assert.Equal(10, doc.Version);
        Assert.Equal("红胜", doc.Result);
        Assert.Equal("全局", doc.Category);
        Assert.Equal("SuperChess 测试棋谱全局注释", doc.Annotation);

        // 根节点两个分支：主线 h2e2 与变着 e6e5
        Assert.Equal(2, doc.Root.Children.Count);
        Assert.Equal("h2e2", U(doc.Root.Children[0]));
        Assert.Equal("e6e5", U(doc.Root.Children[1]));

        // 主线 4 步，第 2 步带注释
        var main = doc.Root.Children[0];
        Assert.Equal("h9g7", U(main.Children[0]));
        Assert.Equal("黑方进马（主线）", main.Children[0].Annotation);
        Assert.Equal("h0g2", U(main.Children[0].Children[0]));
        Assert.Equal("i9h9", U(main.Children[0].Children[0].Children[0]));

        // 变着分支 2 步
        Assert.Equal("h0g2", U(doc.Root.Children[1].Children[0]));
        Assert.Equal(6, CountNodes(doc.Root));
        Assert.True(doc.ValidateAllMoves());
    }

    [Fact]
    public void Parse_HighVersionV16_EncryptedWithAnnotation()
    {
        var doc = Load("test_manual_v16.xqf");
        Assert.Equal(16, doc.Version);
        Assert.Equal("平局", doc.Result);
        Assert.Equal("残局", doc.Category);

        Assert.Equal("h2e2", U(doc.Root.Children[0]));
        var second = doc.Root.Children[0].Children[0];
        Assert.Equal("h9g7", U(second));
        Assert.Equal("高版本加密注释", second.Annotation);
        Assert.Equal(2, CountNodes(doc.Root));
        Assert.True(doc.ValidateAllMoves());
    }

    [Fact]
    public void Parse_AllSamples_InitialBoardIsStandard()
    {
        foreach (var name in (string[])"sample_07_XQStudio.xqf|test_manual_v10.xqf|test_manual_v16.xqf".Split('|'))
        {
            var doc = Load(name);
            Assert.Equal(Fen.Initial, doc.InitialBoard.ToFen());
            Assert.True(doc.ValidateAllMoves(), name);
        }
    }

    [Fact]
    public void Parse_InvalidMagic_Throws()
    {
        var data = new byte[0x410];
        data[0] = (byte)'X';
        data[1] = (byte)'Y';
        var e = Assert.Throws<ManualFormatException>(() => XqfParser.Parse(data));
        Assert.Contains("文件头标识", e.Message);
    }

    [Fact]
    public void Parse_TooShort_Throws()
    {
        var e = Assert.Throws<ManualFormatException>(() => XqfParser.Parse([0x58, 0x51, 0x10]));
        Assert.Contains("过短", e.Message);
    }
}
