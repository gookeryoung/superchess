using Godot;

namespace SuperChess.UI;

/// <summary>
/// 箭头绘制层：移植参考项目 ArrowShape 的箭头多边形参数
/// （张角 60°/内角 120°、边长 80、头宽 26、尾宽 10，棋盘原生坐标系），
/// 支持走子历史箭头（alpha 渐隐）与 MultiPV 建议箭头（digit1-5 角标）。
/// </summary>
public partial class ArrowLayer : Node2D
{
    private const float AngleDeg = 60f;
    private const float InnerAngleDeg = 120f;
    private const float SideLength = 80f;
    private const float HeadWidth = 26f;
    private const float TailWidth = 10f;
    private const float FadeDuration = 1.6f;

    /// <summary>建议箭头按排名取色（第 1 路最醒目）。</summary>
    private static readonly Color[] RankColors =
    [
        new(0.9f, 0.2f, 0.15f, 0.75f),
        new(0.95f, 0.55f, 0.1f, 0.6f),
        new(0.85f, 0.75f, 0.1f, 0.5f),
        new(0.2f, 0.65f, 0.85f, 0.45f),
        new(0.45f, 0.45f, 0.9f, 0.4f),
    ];

    private readonly List<(Vector2[] Points, Color Color)> _arrows = [];
    private readonly List<(Texture2D Texture, Vector2 Position)> _digits = [];
    private Texture2D?[] _digitTextures = new Texture2D?[5];
    private bool _historyArrowVisible;

    public override void _Ready()
    {
        for (var i = 0; i < 5; i++)
        {
            _digitTextures[i] = GD.Load<Texture2D>($"res://assets/markers/digit{i + 1}.png");
        }

        ZIndex = 5;
    }

    /// <summary>显示走子历史箭头（随后 alpha 渐隐消失）。</summary>
    public void ShowHistoryArrow(Vector2 from, Vector2 to)
    {
        _arrows.Clear();
        _digits.Clear();
        _arrows.Add((BuildPolygon(from, to), new Color(0.25f, 0.5f, 0.95f, 0.8f)));
        _historyArrowVisible = true;
        QueueRedraw();
        StartFade();
    }

    /// <summary>显示 MultiPV 建议箭头（按排名着色），箭头头部叠加 digit 角标。</summary>
    public void ShowSuggestions(IReadOnlyList<(Vector2 From, Vector2 To)> suggestions)
    {
        ClearAll();
        for (var i = 0; i < suggestions.Count && i < 5; i++)
        {
            _arrows.Add((BuildPolygon(suggestions[i].From, suggestions[i].To), RankColors[i]));
            if (_digitTextures[i] is { } digit)
            {
                var size = digit.GetSize() * 0.55f;
                _digits.Add((digit, suggestions[i].To - new Vector2(size.X * 0.4f, size.Y * 0.1f)));
            }
        }

        QueueRedraw();
    }

    /// <summary>清除全部箭头与角标。</summary>
    public void ClearAll()
    {
        _arrows.Clear();
        _digits.Clear();
        _historyArrowVisible = false;
        Modulate = Colors.White;
        QueueRedraw();
    }

    /// <summary>历史箭头 alpha 渐隐；建议箭头不受影响。</summary>
    private void StartFade()
    {
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0f, FadeDuration).SetDelay(0.4f);
        tween.Finished += () =>
        {
            if (_historyArrowVisible)
            {
                _arrows.Clear();
                _historyArrowVisible = false;
                Modulate = Colors.White;
                QueueRedraw();
            }
        };
    }

    public override void _Draw()
    {
        foreach (var (points, color) in _arrows)
        {
            DrawColoredPolygon(points, color);
        }

        foreach (var (texture, position) in _digits)
        {
            var size = texture.GetSize() * 0.55f;
            DrawTextureRect(texture, new Rect2(position, size), false);
        }
    }

    /// <summary>
    /// 生成箭头多边形顶点（移植 ArrowShape.getTransformedPath）：
    /// 局部坐标 +x 轴指向目标方向，经旋转与平移映射到 (from, to) 线段。
    /// </summary>
    public static Vector2[] BuildPolygon(Vector2 from, Vector2 to)
    {
        var angle1 = AngleDeg / 360f * MathF.PI;
        var angle2 = InnerAngleDeg / 360f * MathF.PI;
        var sinv1 = MathF.Sin(angle1);
        var cosv1 = MathF.Cos(angle1);
        var tanv2 = MathF.Tan(angle2);

        // 局部坐标系：目标长度直接作为 x2，避免箭头随长度缩放变形。
        var x2 = new Vector2(to.X - from.X, to.Y - from.Y).Length();
        var x3 = x2 - SideLength * cosv1;
        var y3 = -SideLength * sinv1;
        var x5 = x3 + SideLength * sinv1 / tanv2 - HeadWidth / 2f / tanv2;
        var y5 = -HeadWidth / 2f;
        var y6 = -TailWidth / 2f;

        Vector2[] local =
        [
            new(x2, 0),
            new(x3, y3),
            new(x5, y5),
            new(0, y6),
            new(0, -y6),
            new(x5, -y5),
            new(x3, -y3),
        ];

        var rotation = MathF.Atan2(to.Y - from.Y, to.X - from.X);
        return local.Select(p => from + new Vector2(
            p.X * MathF.Cos(rotation) - p.Y * MathF.Sin(rotation),
            p.X * MathF.Sin(rotation) + p.Y * MathF.Cos(rotation))).ToArray();
    }
}
