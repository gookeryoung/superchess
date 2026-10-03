using System.Text;

namespace SuperChess.Manual;

/// <summary>
/// XQF 字节流顺序读取器（替代参考项目 XQFBufferDecoder）：
/// 提供字节数组、小端整数与 GB18030 字符串的顺序读取，越界时返回实际可读内容。
/// </summary>
public sealed class XqfReader
{
    private readonly byte[] _buffer;
    private int _index;

    /// <summary>以完整字节缓冲构造读取器，起始位置为 0。</summary>
    public XqfReader(byte[] buffer)
    {
        _buffer = buffer;
    }

    /// <summary>顺序读取指定长度字节；剩余不足时返回实际可读内容（可能为空数组）。</summary>
    public byte[] ReadBytes(int size)
    {
        var start = _index;
        var stop = Math.Min(_index + size, _buffer.Length);
        _index = stop;
        return _buffer[start..stop];
    }

    /// <summary>顺序读取 4 字节小端整数；剩余不足 4 字节时以 0 补齐。</summary>
    public int ReadInt32()
    {
        var bytes = ReadBytes(4);
        var result = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            result |= bytes[i] << (8 * i);
        }

        return result;
    }

    /// <summary>顺序读取指定长度 GB18030 字符串并去除首尾空白；剩余不足时按实际长度解码。</summary>
    public string ReadString(int size)
    {
        var bytes = ReadBytes(size);
        try
        {
            return EncodingCache.Gb18030.GetString(bytes).Trim();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
