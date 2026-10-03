namespace SuperChess.Manual;

/// <summary>
/// XQF 解密密钥（移植自参考项目 XQFKey.java 与 XQFParser.initDecryptKey）：
/// 由头部 13 字节加密参数计算棋子布局（KeyXY）、起点（KeyXYf）、终点（KeyXYt）、
/// 注解长度（KeyRmkSize）四把钥匙，以及着法区流式解密的 32 字节钥匙表。
/// </summary>
public sealed class XqfKey
{
    /// <summary>XQF 版权串：流式解密钥匙表的种子（长度恰为 32 字节）。</summary>
    private const string CopyrightKeys = "[(C) Copyright Mr. Dong Shiwei.]";

    private readonly byte[] _f32Keys;

    private XqfKey(int keyXY, int keyXYf, int keyXYt, int keyRmkSize, byte[] f32Keys)
    {
        KeyXY = keyXY;
        KeyXYf = keyXYf;
        KeyXYt = keyXYt;
        KeyRmkSize = keyRmkSize;
        _f32Keys = f32Keys;
    }

    /// <summary>棋子布局位置钥匙。</summary>
    public int KeyXY { get; }

    /// <summary>棋谱起点钥匙。</summary>
    public int KeyXYf { get; }

    /// <summary>棋谱终点钥匙。</summary>
    public int KeyXYt { get; }

    /// <summary>注解大小修正钥匙。</summary>
    public int KeyRmkSize { get; }

    /// <summary>流式解密钥匙表（32 字节）。</summary>
    public byte[] F32Keys => _f32Keys;

    /// <summary>
    /// 从头部加密参数切片（13 字节：掩码、产品号 4 字节、四个或值、钥匙和、三个位置钥匙）计算密钥。
    /// 产品号不参与计算，与参考项目一致忽略。
    /// </summary>
    public static XqfKey FromHeader(byte[] cryptKeys)
    {
        var keyMask = cryptKeys[0];
        var keyOrA = cryptKeys[5];
        var keyOrB = cryptKeys[6];
        var keyOrC = cryptKeys[7];
        var keyOrD = cryptKeys[8];
        var keysSum = cryptKeys[9];
        var headKeyXY = cryptKeys[10];
        var headKeyXYf = cryptKeys[11];
        var headKeyXYt = cryptKeys[12];

        // 首把钥匙以自身为级联因子，后续钥匙以前一把为级联因子（参考 Pascal 源公式）
        var keyXY = ComputeKey(headKeyXY, headKeyXY);
        var keyXYf = ComputeKey(headKeyXYf, keyXY);
        var keyXYt = ComputeKey(headKeyXYt, keyXYf);
        var keyRmkSize = ((keysSum * 256 + headKeyXY) % 32000 + 767) & 0xFFFF;

        var fKeyBytes = new byte[4]
        {
            (byte)((keysSum & keyMask) | keyOrA),
            (byte)((headKeyXY & keyMask) | keyOrB),
            (byte)((headKeyXYf & keyMask) | keyOrC),
            (byte)((headKeyXYt & keyMask) | keyOrD),
        };

        var seed = System.Text.Encoding.ASCII.GetBytes(CopyrightKeys);
        var f32Keys = new byte[seed.Length];
        for (var i = 0; i < seed.Length; i++)
        {
            f32Keys[i] = (byte)(seed[i] & fKeyBytes[i % 4]);
        }

        return new XqfKey(keyXY, keyXYf, keyXYt, keyRmkSize, f32Keys);
    }

    /// <summary>多项式扰动公式：(((((b*b)*3+9)*3+8)*2+1)*3+8)*prev，结果截断为单字节。</summary>
    private static int ComputeKey(int b, int prev) =>
        (((((b * b * 3 + 9) * 3 + 8) * 2 + 1) * 3 + 8) * prev) & 0xFF;
}
