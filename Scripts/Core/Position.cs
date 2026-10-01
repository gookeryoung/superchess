namespace SuperChess.Core;

/// <summary>
/// 棋盘格坐标（x 列 0-8 自左向右，y 行 0-9 自上向下，原点在左上）。
/// </summary>
public readonly record struct Position(int X, int Y)
{
    /// <summary>坐标是否落在棋盘内。</summary>
    public bool IsValid => X >= 0 && X < Board.Width && Y >= 0 && Y < Board.Height;

    /// <inheritdoc />
    public override string ToString() => $"({X}, {Y})";
}
