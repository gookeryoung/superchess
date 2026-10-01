namespace SuperChess.Core;

/// <summary>
/// 棋盘局面：int[10,9]（y 行 x 列，原点左上），含走子方与回合数。
/// 移植自参考项目 Board.java（不含 Zobrist/score，MVP 无需）。
/// </summary>
public sealed class Board
{
    /// <summary>棋盘列数。</summary>
    public const int Width = 9;

    /// <summary>棋盘行数。</summary>
    public const int Height = 10;

    private readonly int[,] _cells;

    /// <summary>棋盘格子（[y, x]），仅供只读访问；改动请用 GetPiece/SetPiece/DoMove。</summary>
    public int[,] Cells => _cells;

    /// <summary>当前是否轮到红方走子。</summary>
    public bool RedToMove { get; private set; }

    /// <summary>回合数（每走半着 +1，与参考项目语义一致，写入 FEN 第 6 字段）。</summary>
    public int Rounds { get; private set; }

    /// <summary>构造标准起始局面。</summary>
    public Board() : this(CreateInitialCells(), redToMove: true, rounds: 1)
    {
    }

    internal Board(int[,] cells, bool redToMove, int rounds)
    {
        _cells = cells;
        RedToMove = redToMove;
        Rounds = rounds;
    }

    /// <summary>从 FEN 构造局面；非法 FEN 抛 FenFormatException（含字段名与原因）。</summary>
    public static Board FromFen(string fen) => Fen.ToBoard(fen);

    /// <summary>编码为 6 字段 FEN 字符串。</summary>
    public string ToFen() => Fen.FromBoard(this);

    /// <summary>深拷贝局面。</summary>
    public Board Clone() => new((int[,])_cells.Clone(), RedToMove, Rounds);

    /// <summary>读取 (x, y) 处棋子；越界返回 -1。</summary>
    public int GetPiece(int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height ? _cells[y, x] : -1;

    /// <summary>读取指定坐标处棋子；越界返回 -1。</summary>
    public int GetPiece(Position pos) => GetPiece(pos.X, pos.Y);

    /// <summary>
    /// 设置 (x, y) 处棋子（供走法模拟与局面编辑使用，不翻边、不计回合）；越界或非法棋子值返回 false。
    /// </summary>
    public bool SetPiece(int x, int y, int piece)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height || (piece != Piece.Empty && !Piece.IsValid(piece)))
        {
            return false;
        }

        _cells[y, x] = piece;
        return true;
    }

    /// <summary>
    /// 在棋盘上执行一步走法：移动棋子、翻边、回合数 +1。
    /// 走法合法性由 Rule 校验，本方法仅验证起点有子。
    /// </summary>
    /// <returns>起点有子且坐标合法返回 true，否则返回 false 且棋盘不变。</returns>
    public bool DoMove(Move move)
    {
        var piece = GetPiece(move.From);
        if (!Piece.IsValid(piece) || !move.From.IsValid || !move.To.IsValid)
        {
            return false;
        }

        _cells[move.To.Y, move.To.X] = piece;
        _cells[move.From.Y, move.From.X] = Piece.Empty;
        RedToMove = !RedToMove;
        Rounds++;
        return true;
    }

    /// <summary>定位指定方的将/帅位置；不存在返回 null。</summary>
    public Position? FindKing(bool red)
    {
        var target = red ? Piece.RedKing : Piece.BlackKing;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (_cells[y, x] == target)
                {
                    return new Position(x, y);
                }
            }
        }

        return null;
    }

    /// <summary>构造标准起始局面的棋子矩阵。</summary>
    private static int[,] CreateInitialCells()
    {
        var cells = new int[Height, Width];
        var initial = Fen.Initial;
        // 起始 FEN 为内置常量，解析必然成功，直接复用 Fen 的行解析逻辑。
        var boardField = initial.Split(' ')[0];
        var rows = boardField.Split('/');
        for (var y = 0; y < Height; y++)
        {
            var x = 0;
            foreach (var c in rows[y])
            {
                if (c is >= '1' and <= '9')
                {
                    x += c - '0';
                }
                else
                {
                    cells[y, x] = Piece.FromFenChar(c)!.Value;
                    x++;
                }
            }
        }

        return cells;
    }
}
