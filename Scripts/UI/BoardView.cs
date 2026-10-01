using Godot;
using SuperChess.Core;

namespace SuperChess.UI;

/// <summary>
/// 棋盘渲染：底图等比缩放、棋子 Sprite2D、选中框与落点提示标记、走子 Tween 动画。
/// 内部使用参考项目棋盘图原生坐标系（1240x1340，格距 136，棋子 110），整体缩放由 Main 控制。
/// </summary>
public partial class BoardView : Node2D
{
    /// <summary>棋盘底图原生宽度（像素）。</summary>
    public const int NativeWidth = 1240;

    /// <summary>棋盘底图原生高度（像素）。</summary>
    public const int NativeHeight = 1340;

    /// <summary>棋子/标记渲染边长（像素）。</summary>
    public const int PieceSize = 110;

    /// <summary>棋盘线交叉点区域左上角 X 偏移（x1 - PieceSize/2，x1=77）。</summary>
    public const int XOffset = 22;

    /// <summary>棋盘线交叉点区域左上角 Y 偏移（y1 - PieceSize/2，y1=60）。</summary>
    public const int YOffset = 5;

    /// <summary>相邻纵/横线间距（像素）。</summary>
    public const int GridInterval = 136;

    private const float PieceTextureSize = 140f;
    private const float MoveDuration = 0.15f;
    private const string PieceDir = "res://assets/pieces/";

    // 棋子编码 1-14 对应贴图文件（与 Piece 常量顺序一致：红帅仕相马车炮兵、黑将士象马车炮卒）。
    private static readonly string[] PieceTextureNames =
    [
        "r_shuai", "r_shi", "r_xiang", "r_ma", "r_ju", "r_pao", "r_bing",
        "b_jiang", "b_shi", "b_xiang", "b_ma", "b_ju", "b_pao", "b_zu",
    ];

    private Sprite2D _boardSprite = null!;
    private Node2D _pieceRoot = null!;
    private Node2D _markerRoot = null!;
    private readonly Sprite2D?[,] _sprites = new Sprite2D[Board.Height, Board.Width];
    private readonly Texture2D?[] _pieceTextures = new Texture2D[Piece.BlackPawn];
    private Texture2D _redBox = null!;
    private Texture2D _blackBox = null!;
    private Texture2D _redPot = null!;
    private Texture2D _blackPot = null!;

    /// <summary>走子动画播放中为 true，期间输入被 BoardInput 忽略。</summary>
    public bool IsAnimating { get; private set; }

    public override void _Ready()
    {
        _boardSprite = new Sprite2D
        {
            Centered = false,
            Texture = GD.Load<Texture2D>("res://assets/board/chessboard.png"),
        };
        AddChild(_boardSprite);

        for (var piece = Piece.RedKing; piece <= Piece.BlackPawn; piece++)
        {
            _pieceTextures[piece - 1] = GD.Load<Texture2D>(PieceDir + PieceTextureNames[piece - 1] + ".png");
        }

        _redBox = GD.Load<Texture2D>("res://assets/markers/r_box.png");
        _blackBox = GD.Load<Texture2D>("res://assets/markers/b_box.png");
        _redPot = GD.Load<Texture2D>("res://assets/markers/redpot.png");
        _blackPot = GD.Load<Texture2D>("res://assets/markers/blackpot.png");

        _pieceRoot = new Node2D { Name = "Pieces" };
        _markerRoot = new Node2D { Name = "Markers" };
        AddChild(_pieceRoot);
        AddChild(_markerRoot);
    }

    /// <summary>格坐标 → 底图局部坐标（返回交叉点中心，即棋子中心）。</summary>
    public static Vector2 CellCenter(Position pos) => new(
        XOffset + pos.X * GridInterval + PieceSize / 2f,
        YOffset + pos.Y * GridInterval + PieceSize / 2f);

    /// <summary>
    /// 底图局部坐标 → 格坐标（命中棋子矩形区域内才算有效）；
    /// 落在棋盘外或格子间空隙返回 null。
    /// </summary>
    public Position? TryHit(Vector2 local)
    {
        var ix = (int)((local.X - XOffset) / GridInterval);
        var iy = (int)((local.Y - YOffset) / GridInterval);
        if (ix < 0 || ix >= Board.Width || iy < 0 || iy >= Board.Height)
        {
            return null;
        }

        var left = XOffset + ix * GridInterval;
        var top = YOffset + iy * GridInterval;
        if (local.X < left || local.X > left + PieceSize || local.Y < top || local.Y > top + PieceSize)
        {
            return null;
        }

        return new Position(ix, iy);
    }

    /// <summary>按指定局面重建全部棋子精灵（全量重建，局面规模下开销可忽略）。</summary>
    public void RenderBoard(Board board)
    {
        foreach (var sprite in _sprites)
        {
            sprite?.QueueFree();
        }

        Array.Clear(_sprites);

        for (var y = 0; y < Board.Height; y++)
        {
            for (var x = 0; x < Board.Width; x++)
            {
                var piece = board.GetPiece(x, y);
                if (Piece.IsValid(piece))
                {
                    _sprites[y, x] = CreatePieceSprite(piece, new Position(x, y));
                }
            }
        }
    }

    /// <summary>
    /// 播放走子动画：起点棋子平移至终点，被吃棋子同步淡出，结束后棋子精灵表即与局面一致。
    /// </summary>
    public async void AnimateMove(Move move)
    {
        var moving = _sprites[move.From.Y, move.From.X];
        if (moving is null)
        {
            return;
        }

        IsAnimating = true;
        _sprites[move.From.Y, move.From.X] = null;
        var captured = _sprites[move.To.Y, move.To.X];
        _sprites[move.To.Y, move.To.X] = moving;

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(moving, "position", CellCenter(move.To), MoveDuration)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        if (captured is not null)
        {
            tween.TweenProperty(captured, "modulate:a", 0f, MoveDuration);
        }

        await ToSignal(tween, Tween.SignalName.Finished);
        captured?.QueueFree();
        IsAnimating = false;
    }

    /// <summary>显示选中框与合法落点提示（按棋子颜色取红/黑标记贴图）。</summary>
    public void ShowSelection(Position from, bool red, IReadOnlyList<Position> targets)
    {
        ClearSelection();

        var box = new Sprite2D
        {
            Texture = red ? _redBox : _blackBox,
            Position = CellCenter(from),
            Scale = MarkerScale(red ? _redBox : _blackBox),
        };
        _markerRoot.AddChild(box);

        var pot = red ? _redPot : _blackPot;
        foreach (var target in targets)
        {
            _markerRoot.AddChild(new Sprite2D
            {
                Texture = pot,
                Position = CellCenter(target),
                Scale = MarkerScale(pot),
            });
        }
    }

    /// <summary>清除选中框与全部落点提示标记。</summary>
    public void ClearSelection()
    {
        foreach (var child in _markerRoot.GetChildren())
        {
            child.QueueFree();
        }
    }

    /// <summary>标记贴图统一缩放到 PieceSize 边长。</summary>
    private static Vector2 MarkerScale(Texture2D texture) =>
        Vector2.One * (PieceSize / (float)texture.GetWidth());

    /// <summary>创建指定格坐标的棋子精灵并登记到精灵表。</summary>
    private Sprite2D CreatePieceSprite(int piece, Position pos)
    {
        var texture = _pieceTextures[piece - 1]!;
        var sprite = new Sprite2D
        {
            Texture = texture,
            Position = CellCenter(pos),
            Scale = Vector2.One * (PieceSize / PieceTextureSize),
        };
        _pieceRoot.AddChild(sprite);
        return sprite;
    }
}
