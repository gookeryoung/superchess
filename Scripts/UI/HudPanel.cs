using Godot;
using SuperChess.Engine;
using SuperChess.Game;

namespace SuperChess.UI;

/// <summary>
/// 底部控制面板：新局/悔棋/提示/模式切换按钮、引擎强度设置（限棋力 + Elo、线程数、
/// 置换表大小）与状态显示。控件在代码中构建；中文文本依赖系统字体回退。
/// </summary>
public partial class HudPanel : PanelContainer
{
    private Button _newGameButton = null!;
    private Button _undoButton = null!;
    private Button _hintButton = null!;
    private Button _analyzeButton = null!;
    private Button _modeButton = null!;
    private CheckBox _limitStrengthBox = null!;
    private HSlider _eloSlider = null!;
    private Label _eloLabel = null!;
    private SpinBox _threadsBox = null!;
    private SpinBox _hashBox = null!;
    private Label _statusLabel = null!;
    private Label _evalLabel = null!;
    private ItemList _evalList = null!;

    /// <summary>用户请求开始新对局。</summary>
    public event Action? NewGameRequested;

    /// <summary>用户请求悔棋。</summary>
    public event Action? UndoRequested;

    /// <summary>用户请求提示。</summary>
    public event Action? HintRequested;

    /// <summary>用户请求分析当前局面。</summary>
    public event Action? AnalyzeRequested;

    /// <summary>用户请求切换对局模式。</summary>
    public event Action? ModeToggled;

    /// <summary>用户请求复制当前局面 FEN 到剪贴板。</summary>
    public event Action? CopyFenRequested;

    /// <summary>用户请求从剪贴板粘贴 FEN 载入局面。</summary>
    public event Action? PasteFenRequested;

    /// <summary>用户请求查看关于/开源声明。</summary>
    public event Action? AboutRequested;

    /// <summary>用户请求打开课程列表（入门指导）。</summary>
    public event Action? LessonRequested;

    /// <summary>用户请求打开残局题库列表。</summary>
    public event Action? PuzzleRequested;

    /// <summary>强度设置变化（含完整选项快照）。</summary>
    public event Action<EngineOptions>? OptionsChanged;

    public override void _Ready()
    {
        var root = new VBoxContainer();
        AddChild(root);

        root.AddChild(BuildButtonRow());
        root.AddChild(BuildPracticeRow());
        root.AddChild(BuildStrengthRow());
        root.AddChild(BuildResourceRow());
        root.AddChild(BuildFenRow());
        _statusLabel = new Label { Text = "双人对弈", HorizontalAlignment = HorizontalAlignment.Center };
        root.AddChild(_statusLabel);
        root.AddChild(BuildEvalSection());

        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        CustomMinimumSize = new Vector2(1080, 0);
    }

    /// <summary>设置状态栏文本（模式/引擎状态/结果等）。</summary>
    public void SetStatus(string text) => _statusLabel.Text = text;

    /// <summary>设置当前局面分值文本（红方视角）。</summary>
    public void SetEval(string text) => _evalLabel.Text = $"评估：{text}";

    /// <summary>清空评估显示。</summary>
    public void ClearEval()
    {
        _evalLabel.Text = "评估：-";
        _evalList.Clear();
    }

    /// <summary>向历史评估列表追加一行。</summary>
    public void AppendEval(string line) => _evalList.AddItem(line);

    /// <summary>收集当前强度设置为引擎选项快照（线程数 null 表示按 CPU 核数自动）。</summary>
    public EngineOptions CollectOptions() => new()
    {
        Threads = (int)_threadsBox.Value,
        HashMb = (int)_hashBox.Value,
        LimitStrength = _limitStrengthBox.ButtonPressed,
        Elo = _limitStrengthBox.ButtonPressed ? (int)_eloSlider.Value : null,
    };

    /// <summary>构建按钮行（新局/悔棋/提示/分析/模式）。</summary>
    private Control BuildButtonRow()
    {
        _newGameButton = new Button { Text = "新局", CustomMinimumSize = new Vector2(0, 88) };
        _undoButton = new Button { Text = "悔棋", CustomMinimumSize = new Vector2(0, 88) };
        _hintButton = new Button { Text = "提示", CustomMinimumSize = new Vector2(0, 88), Disabled = true };
        _analyzeButton = new Button { Text = "分析", CustomMinimumSize = new Vector2(0, 88), Disabled = true };
        _modeButton = new Button { Text = "切人机", CustomMinimumSize = new Vector2(0, 88) };
        _newGameButton.Pressed += () => NewGameRequested?.Invoke();
        _undoButton.Pressed += () => UndoRequested?.Invoke();
        _hintButton.Pressed += () => HintRequested?.Invoke();
        _analyzeButton.Pressed += () => AnalyzeRequested?.Invoke();
        _modeButton.Pressed += () => ModeToggled?.Invoke();

        var row = new HBoxContainer();
        foreach (var button in new[] { _newGameButton, _undoButton, _hintButton, _analyzeButton, _modeButton })
        {
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(button);
        }

        return row;
    }

    /// <summary>构建练习行（教学/残局入口，各半宽）。</summary>
    private Control BuildPracticeRow()
    {
        var lessonButton = new Button { Text = "教学", CustomMinimumSize = new Vector2(0, 88) };
        var puzzleButton = new Button { Text = "残局", CustomMinimumSize = new Vector2(0, 88) };
        lessonButton.Pressed += () => LessonRequested?.Invoke();
        puzzleButton.Pressed += () => PuzzleRequested?.Invoke();

        var row = new HBoxContainer();
        foreach (var button in new[] { lessonButton, puzzleButton })
        {
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(button);
        }

        return row;
    }

    /// <summary>构建 FEN 行（复制/粘贴/关于）。</summary>
    private Control BuildFenRow()
    {
        var copyButton = new Button { Text = "复制FEN", CustomMinimumSize = new Vector2(0, 80) };
        var pasteButton = new Button { Text = "粘贴FEN", CustomMinimumSize = new Vector2(0, 80) };
        var aboutButton = new Button { Text = "关于", CustomMinimumSize = new Vector2(0, 80) };
        copyButton.Pressed += () => CopyFenRequested?.Invoke();
        pasteButton.Pressed += () => PasteFenRequested?.Invoke();
        aboutButton.Pressed += () => AboutRequested?.Invoke();

        var row = new HBoxContainer();
        foreach (var button in new[] { copyButton, pasteButton, aboutButton })
        {
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(button);
        }

        return row;
    }

    /// <summary>构建评估区（当前分值 + 历史评估列表）。</summary>
    private Control BuildEvalSection()
    {
        _evalLabel = new Label { Text = "评估：-", HorizontalAlignment = HorizontalAlignment.Center };
        _evalList = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 320),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };

        var box = new VBoxContainer();
        box.AddChild(_evalLabel);
        box.AddChild(_evalList);
        return box;
    }

    /// <summary>更新模式按钮文案与提示/分析可用性。</summary>
    public void SetMode(GameMode mode, bool engineAvailable)
    {
        _modeButton.Text = mode == GameMode.PlayWithEngine ? "切双人" : "切人机";
        _hintButton.Disabled = mode != GameMode.PlayWithEngine || !engineAvailable;
        _analyzeButton.Disabled = !engineAvailable;
    }

    /// <summary>更新分析按钮文案以反映连续分析开关状态（开启后走子自动刷新建议）。</summary>
    public void SetAnalysisActive(bool active) => _analyzeButton.Text = active ? "停止分析" : "分析";

    /// <summary>
    /// 进入/退出练习模式。练习中禁用切人机/提示/分析（悔棋=重玩、新局=退出由 Main 拦截语义）；
    /// 退出后由 Main 调 SetMode 恢复按钮可用性。
    /// </summary>
    public void SetPracticeMode(bool practicing)
    {
        _modeButton.Disabled = practicing;
        if (practicing)
        {
            _hintButton.Disabled = true;
            _analyzeButton.Disabled = true;
        }
    }

    /// <summary>构建强度行（限棋力开关 + Elo 滑条）。</summary>
    private Control BuildStrengthRow()
    {
        _limitStrengthBox = new CheckBox { Text = "限棋力", ButtonPressed = true };
        _eloSlider = new HSlider
        {
            MinValue = 1280,
            MaxValue = 3199,
            Step = 1,
            Value = 2000,
            CustomMinimumSize = new Vector2(520, 0),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        _eloLabel = new Label { Text = "Elo 2000" };
        _eloSlider.ValueChanged += _ => UpdateEloLabel();
        _limitStrengthBox.Toggled += _ => EmitOptionsChanged();

        var row = new HBoxContainer();
        row.AddChild(_limitStrengthBox);
        row.AddChild(_eloSlider);
        row.AddChild(_eloLabel);
        return row;
    }

    /// <summary>构建资源行（线程数/置换表）。</summary>
    private Control BuildResourceRow()
    {
        _threadsBox = new SpinBox
        {
            MinValue = 1,
            MaxValue = 8,
            Step = 1,
            Value = System.Environment.ProcessorCount,
        };
        _hashBox = new SpinBox { MinValue = 16, MaxValue = 1024, Step = 16, Value = 64 };

        var row = new HBoxContainer();
        row.AddChild(new Label { Text = "线程" });
        row.AddChild(_threadsBox);
        row.AddChild(new Label { Text = "哈希(MB)" });
        row.AddChild(_hashBox);

        foreach (var box in new[] { _threadsBox, _hashBox })
        {
            box.ValueChanged += _ => EmitOptionsChanged();
            box.GetLineEdit().Editable = false;
        }

        return row;
    }

    /// <summary>更新 Elo 显示文本并广播选项变化。</summary>
    private void UpdateEloLabel()
    {
        _eloLabel.Text = $"Elo {(int)_eloSlider.Value}";
        EmitOptionsChanged();
    }

    /// <summary>广播当前选项快照。</summary>
    private void EmitOptionsChanged() => OptionsChanged?.Invoke(CollectOptions());
}
