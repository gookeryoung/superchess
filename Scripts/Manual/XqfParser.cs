using System.Text;
using SuperChess.Core;

namespace SuperChess.Manual;

/// <summary>
/// XQF（象棋桥）解析器：定长头部切片读取 + 高版本解密 + 0x400 起递归读着法树，
/// 移植自参考项目 XQFParser.java（JBBP 头部解析改为直接偏移读取，行为一致）。
/// </summary>
public static class XqfParser
{
    // 头部定长字段偏移（XQF 格式 0x00-0x3FF，着法区自 0x400 起）
    private const int OffsetCryptKeys = 0x03;  // 13 字节加密参数
    private const int OffsetPiecePos = 0x10;   // 32 字节棋子布局
    private const int OffsetResult = 0x33;     // 对局结果字节
    private const int OffsetType = 0x40;       // 棋谱类型字节
    private const int OffsetTitle = 0x50;      // 64 字节（首字节为长度）
    private const int OffsetEvent = 0xD0;      // 64 字节
    private const int OffsetDate = 0x110;      // 16 字节
    private const int OffsetSite = 0x120;      // 16 字节
    private const int OffsetRed = 0x130;       // 16 字节
    private const int OffsetBlack = 0x140;     // 16 字节
    private const int OffsetAnnotator = 0x1D0; // 16 字节
    private const int OffsetAuthor = 0x1E0;    // 16 字节
    private const int OffsetSteps = 0x400;

    // 高版本着法记录第 3 字节标志位
    private const byte MaskHasNext = 0x80;
    private const byte MaskHasVar = 0x40;
    private const byte MaskHasAnnotation = 0x20;
    // 低版本着法记录第 3 字节标志位（高半字节有后续，低半字节有变着）
    private const byte MaskLowHasNext = 0xF0;
    private const byte MaskLowHasVar = 0x0F;

    /// <summary>XQF 文件最小长度：头部 0x400 + 首条注释记录 4 字节。</summary>
    private const int MinLength = OffsetSteps + 4;

    // 棋子布局顺序：1-16 红方（车马相士帅士相马车炮炮+兵×5），17-32 黑方同序
    private static readonly int[] PieceKinds =
    [
        Piece.RedRook, Piece.RedKnight, Piece.RedBishop, Piece.RedAdvisor, Piece.RedKing,
        Piece.RedAdvisor, Piece.RedBishop, Piece.RedKnight, Piece.RedRook,
        Piece.RedCannon, Piece.RedCannon,
        Piece.RedPawn, Piece.RedPawn, Piece.RedPawn, Piece.RedPawn, Piece.RedPawn,
        Piece.BlackRook, Piece.BlackKnight, Piece.BlackBishop, Piece.BlackAdvisor, Piece.BlackKing,
        Piece.BlackAdvisor, Piece.BlackBishop, Piece.BlackKnight, Piece.BlackRook,
        Piece.BlackCannon, Piece.BlackCannon,
        Piece.BlackPawn, Piece.BlackPawn, Piece.BlackPawn, Piece.BlackPawn, Piece.BlackPawn,
    ];

    /// <summary>解析 XQF 字节流；magic 非法或文件过短抛 ManualFormatException。</summary>
    public static ManualDocument Parse(byte[] data)
    {
        if (data.Length < MinLength)
        {
            throw new ManualFormatException("XQF 文件过短，不是有效的棋谱文件");
        }

        if (data[0] != (byte)'X' || data[1] != (byte)'Q')
        {
            throw new ManualFormatException("XQF 文件头标识非法（缺少 XQ magic）");
        }

        var version = data[2];

        // 高版本（>0x0A）启用加密：头部密钥 + 棋子布局 + 着法区三处解密
        var keys = version > 0x0A ? XqfKey.FromHeader(data[OffsetCryptKeys..(OffsetCryptKeys + 13)]) : null;
        var piecePos = DecryptPiecePos(data[OffsetPiecePos..(OffsetPiecePos + 32)], version, keys);

        var board = new Board();
        for (var i = 0; i < 32; i++)
        {
            var value = piecePos[i];
            if (value == 0xFF)
            {
                continue;
            }

            var pos = GetPositionFromValue(value);
            board.SetPiece(pos.X, pos.Y, PieceKinds[i]);
        }

        // 着法区（高版本先流式解密）
        var stepBytes = data[OffsetSteps..];
        if (keys is not null)
        {
            stepBytes = DecodeBuff(keys, stepBytes);
        }

        var reader = new XqfReader(stepBytes);
        var gameAnnotation = ReadAnnotationInfo(reader, version, keys);
        var doc = new ManualDocument(board)
        {
            Version = version,
            Title = ReadXqfString(data, OffsetTitle, 64),
            Event = ReadXqfString(data, OffsetEvent, 64),
            Date = ReadXqfString(data, OffsetDate, 16),
            Site = ReadXqfString(data, OffsetSite, 16),
            Red = ReadXqfString(data, OffsetRed, 16),
            Black = ReadXqfString(data, OffsetBlack, 16),
            Result = ParseResult(data[OffsetResult]),
            Category = ParseCategory(data[OffsetType]),
            Annotator = ReadXqfString(data, OffsetAnnotator, 16),
            Author = ReadXqfString(data, OffsetAuthor, 16),
            Annotation = gameAnnotation,
        };

        ReadSteps(reader, version, keys, doc.Root);
        return doc;
    }

    /// <summary>读取 XQF 头部字符串：首字节为内容长度，其后为 GB18030 文本。</summary>
    private static string ReadXqfString(byte[] data, int offset, int fieldSize)
    {
        var length = Math.Min(data[offset], fieldSize - 1);
        try
        {
            return EncodingCache.Gb18030.GetString(data, offset + 1, length).Trim();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>读取首条记录携带的整局注释（无着法语义，仅注解标志与长度）。</summary>
    private static string? ReadAnnotationInfo(XqfReader reader, int version, XqfKey? keys)
    {
        var stepInfo = reader.ReadBytes(4);
        if (stepInfo.Length < 4)
        {
            return null;
        }

        int annoteLen;
        if (version <= 0x0A)
        {
            // 低版本在标志字节后紧跟注释长度，为 0 则无注释
            annoteLen = reader.ReadInt32();
        }
        else
        {
            var flags = (byte)(stepInfo[2] & 0xE0);
            annoteLen = (flags & MaskHasAnnotation) != 0
                ? reader.ReadInt32() - keys!.KeyRmkSize
                : 0;
        }

        return annoteLen > 0 ? reader.ReadString(annoteLen) : null;
    }

    /// <summary>
    /// 递归读取着法记录构建着法树：主线延续挂在当前节点，变着分支回挂到父节点（与参考项目一致）。
    /// </summary>
    private static void ReadSteps(XqfReader reader, int version, XqfKey? keys, MoveNode node)
    {
        var stepInfo = reader.ReadBytes(4);
        if (stepInfo.Length < 4)
        {
            return;
        }

        int annoteLen;
        bool hasNextStep;
        bool hasVarStep;
        int moveFrom;
        int moveTo;
        if (version <= 0x0A)
        {
            hasNextStep = (stepInfo[2] & MaskLowHasNext) != 0;
            hasVarStep = (stepInfo[2] & MaskLowHasVar) != 0;
            annoteLen = reader.ReadInt32();
            moveFrom = ((stepInfo[0] & 0xFF) - 0x18) & 0xFF;
            moveTo = ((stepInfo[1] & 0xFF) - 0x20) & 0xFF;
        }
        else
        {
            var flags = (byte)(stepInfo[2] & 0xE0);
            hasNextStep = (flags & MaskHasNext) != 0;
            hasVarStep = (flags & MaskHasVar) != 0;
            annoteLen = (flags & MaskHasAnnotation) != 0
                ? reader.ReadInt32() - keys!.KeyRmkSize
                : 0;
            moveFrom = ((((stepInfo[0] & 0xFF) - 0x18) & 0xFF) - keys!.KeyXYf) & 0xFF;
            moveTo = ((((stepInfo[1] & 0xFF) - 0x20) & 0xFF) - keys!.KeyXYt) & 0xFF;
        }

        var move = new Move(GetPositionFromValue(moveFrom), GetPositionFromValue(moveTo));
        var annotation = annoteLen > 0 ? reader.ReadString(annoteLen) : null;
        var nextNode = MoveNode.Append(move, node, annotation);

        if (hasNextStep)
        {
            ReadSteps(reader, version, keys, nextNode);
        }

        if (hasVarStep)
        {
            ReadSteps(reader, version, keys, node);
        }
    }

    /// <summary>
    /// 解密棋子布局：先按版本 ≥12 的乱序规则重排，再逐字节减 KeyXY；无效值（>89）置 0xFF（无子）。
    /// </summary>
    private static byte[] DecryptPiecePos(byte[] manStr, int version, XqfKey? keys)
    {
        if (keys is null)
        {
            return (byte[])manStr.Clone();
        }

        var tmpMan = new byte[32];
        for (var i = 0; i < 32; i++)
        {
            var target = version >= 12 ? (keys.KeyXY + i + 1) & 0x1F : i;
            tmpMan[target] = manStr[i];
        }

        for (var i = 0; i < 32; i++)
        {
            tmpMan[i] = (byte)((tmpMan[i] - keys.KeyXY) & 0xFF);
            if (tmpMan[i] > 89)
            {
                tmpMan[i] = 0xFF;
            }
        }

        return tmpMan;
    }

    /// <summary>着法区流式解密：逐字节减去钥匙表对应字节（位置自 0x400 起循环取模）。</summary>
    private static byte[] DecodeBuff(XqfKey keys, byte[] buff)
    {
        var deBuff = (byte[])buff.Clone();
        for (var i = 0; i < buff.Length; i++)
        {
            deBuff[i] = (byte)(deBuff[i] - keys.F32Keys[(OffsetSteps + i) % 32]);
        }

        return deBuff;
    }

    /// <summary>XQF 坐标 value = X*10 + Y（原点左下）转为左上原点 Position(x, 9-y)。</summary>
    private static Position GetPositionFromValue(int value)
    {
        var y = value % 10;
        var x = (value - y) / 10;
        return new Position(x, 9 - y);
    }

    /// <summary>对局结果字节转中文描述：0 未知、1 红胜、2 黑胜、3 平局。</summary>
    private static string ParseResult(byte result) => result switch
    {
        0x01 => "红胜",
        0x02 => "黑胜",
        0x03 => "平局",
        _ => "未知",
    };

    /// <summary>棋谱类型字节转中文描述：0 全局、1 布局、2 中局、3 残局。</summary>
    private static string ParseCategory(byte type) => type switch
    {
        0x00 => "全局",
        0x01 => "布局",
        0x02 => "中局",
        0x03 => "残局",
        _ => string.Empty,
    };
}

/// <summary>GB18030 编码缓存：net8.0 默认无此编码，首次访问时注册 CodePages 提供程序（微软官方纯托管包，AOT 安全）。</summary>
internal static class EncodingCache
{
    private static readonly Lazy<Encoding> Lazy = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding("GB18030");
    });

    /// <summary>GB18030 编码实例（首次访问时完成提供程序注册）。</summary>
    public static Encoding Gb18030 => Lazy.Value;
}
