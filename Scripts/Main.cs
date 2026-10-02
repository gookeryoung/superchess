using Godot;
using SuperChess.Core;
using SuperChess.Engine;
using SuperChess.Game;
using SuperChess.UI;

namespace SuperChess;

/// <summary>
/// M4 场景入口：GameSession 持有对局状态，装配棋盘视图、输入、音效与控制面板，
/// 驱动双人/人机对弈回合流转（引擎思考中经 Busy 门闸拒绝互斥操作）。
/// </summary>
public partial class Main : Node2D
{
    /// <summary>棋盘整体缩放：底图 1240 宽等比缩放至 1080 视口宽。</summary>
    private const float BoardScale = 1080f / BoardView.NativeWidth;

    private readonly GameSession _session = new();
    private readonly LessonController _lessonController = new();
    private readonly PuzzleController _puzzleController = new();
    private BoardView _boardView = null!;
    private BoardInput _boardInput = null!;
    private ArrowLayer _arrows = null!;
    private SoundPlayer _soundPlayer = null!;
    private HudPanel _hud = null!;
    private bool _analysisEnabled;
    private bool _analysisRefreshing;
    private AcceptDialog _aboutDialog = null!;
    private AcceptDialog _lessonDialog = null!;
    private AcceptDialog _puzzleDialog = null!;

    public override void _Ready()
    {
        _boardView = GetNode<BoardView>("Board");
        _boardInput = GetNode<BoardInput>("Board/BoardInput");
        _arrows = GetNode<ArrowLayer>("Board/Arrows");
        _hud = GetNode<HudPanel>("Hud");
        _soundPlayer = new SoundPlayer();
        AddChild(_soundPlayer);
        _aboutDialog = BuildAboutDialog();
        AddChild(_aboutDialog);
        _lessonDialog = BuildPickerDialog(
            "选择课程", [.. LessonLibrary.All.Select(l => l.Title)],
            index => LoadLesson(LessonLibrary.All[index]));
        AddChild(_lessonDialog);
        _puzzleDialog = BuildPickerDialog(
            "选择残局", [.. PuzzleLibrary.All.Select(p => p.Title)],
            index => LoadPuzzle(PuzzleLibrary.All[index]));
        AddChild(_puzzleDialog);

        _boardView.Scale = new Vector2(BoardScale, BoardScale);
        _boardInput.Board = _session.CurrentBoard;
        _boardInput.SoundPlayer = _soundPlayer;
        _boardInput.MoveChosen += OnMoveChosen;
        _boardView.RenderBoard(_session.CurrentBoard);

        _session.MoveApplied += OnMoveApplied;
        _session.BoardReverted += OnBoardReverted;
        _session.HintProvided += OnHintProvided;
        _session.BusyChanged += OnBusyChanged;

        _hud.NewGameRequested += OnNewGame;
        _hud.UndoRequested += OnUndo;
        _hud.HintRequested += OnHint;
        _hud.AnalyzeRequested += OnAnalyze;
        _hud.ModeToggled += OnModeToggled;
        _hud.OptionsChanged += OnOptionsChanged;
        _hud.CopyFenRequested += OnCopyFen;
        _hud.PasteFenRequested += OnPasteFen;
        _hud.AboutRequested += () => _aboutDialog.PopupCentered();
        _hud.LessonRequested += OnLessonRequested;
        _hud.PuzzleRequested += OnPuzzleRequested;
        _hud.Position = new Vector2(0, BoardView.NativeHeight * BoardScale + 16);
    }

    /// <summary>构建通用选择弹窗：ItemList 列表 + 确认按钮，选中项回调 onPick。</summary>
    private static AcceptDialog BuildPickerDialog(string title, string[] items, Action<int> onPick)
    {
        var dialog = new AcceptDialog { Title = title, OkButtonText = "开始" };
        var list = new ItemList { CustomMinimumSize = new Vector2(620, 420) };
        foreach (var item in items)
        {
            list.AddItem(item);
        }

        var selected = -1;
        list.ItemSelected += index => selected = (int)index;
        dialog.Confirmed += () =>
        {
            if (selected >= 0)
            {
                onPick(selected);
            }
        };
        dialog.AddChild(list);
        return dialog;
    }

    /// <summary>是否处于教学/残局练习模式。</summary>
    private bool IsPracticing =>
        _lessonController.Current is not null || _puzzleController.Current is not null;

    /// <summary>清除两个练习控制器（不改变对局状态；换题与退出共用）。</summary>
    private void ClearPracticeControllers()
    {
        _lessonController.Reset();
        _puzzleController.Clear();
    }

    /// <summary>退出练习回对弈：复位初始局面并恢复 HUD 按钮可用性。</summary>
    private void ExitPractice()
    {
        ClearPracticeControllers();
        _boardInput.InputEnabled = true;
        _session.NewGame();
        _hud.SetPracticeMode(false);
        _hud.SetMode(GameMode.TwoPlayers, _session.EngineAvailable);
        _hud.SetStatus("双人对弈");
    }

    /// <summary>教学/残局入口共用的 Busy 门闸：引擎思考中拒绝打开弹窗。</summary>
    private void TryOpenPicker(AcceptDialog dialog)
    {
        if (_session.Busy)
        {
            _soundPlayer.Play(SoundEffect.Invalid);
            _hud.SetStatus("引擎思考中，无法进入练习");
            return;
        }

        dialog.PopupCentered();
    }

    /// <summary>载入课程：双人模式 + LoadFen + 控制器启动 + 练习模式联动。</summary>
    private void LoadLesson(LessonDefinition lesson)
    {
        StopAnalysis();
        ClearPracticeControllers();
        _session.SetMode(GameMode.TwoPlayers);
        _hud.SetMode(GameMode.TwoPlayers, _session.EngineAvailable);
        _session.LoadFen(lesson.Fen);
        _lessonController.Start(lesson);
        _hud.SetPracticeMode(true);
        _hud.SetStatus($"【{lesson.Title}】{lesson.Intro}");
    }

    /// <summary>载入残局题：流程同课程，主线步数并入提示。</summary>
    private void LoadPuzzle(PuzzleDefinition puzzle)
    {
        StopAnalysis();
        ClearPracticeControllers();
        _session.SetMode(GameMode.TwoPlayers);
        _hud.SetMode(GameMode.TwoPlayers, _session.EngineAvailable);
        _session.LoadFen(puzzle.Fen);
        _puzzleController.Start(puzzle);
        _hud.SetPracticeMode(true);
        _hud.SetStatus($"【{puzzle.Title}】{puzzle.Description}（共 {_puzzleController.TotalUserMoves} 步）");
    }

    /// <summary>课程走子处理：先行校验，拒绝不动局面；接受落子并判定完成。</summary>
    private void HandleLessonMove(Move move)
    {
        var lesson = _lessonController.Current!;
        var result = _lessonController.Evaluate(_session.CurrentBoard, move);
        if (!result.Accepted)
        {
            _soundPlayer.Play(SoundEffect.Invalid);
            _hud.SetStatus($"【{lesson.Title}】{result.Message}");
            return;
        }

        if (!_session.TryPlayMove(move))
        {
            _soundPlayer.Play(SoundEffect.Invalid);
            return;
        }

        if (_lessonController.ConsumeApplied())
        {
            _soundPlayer.Play(SoundEffect.Checkmate);
            _hud.SetStatus($"【{lesson.Title}】{lesson.SuccessText}");
        }
    }

    /// <summary>残局走子处理：主线比对；正确后延迟落防守着，末步过关。</summary>
    private void HandlePuzzleMove(Move move)
    {
        var puzzle = _puzzleController.Current!;
        var result = _puzzleController.Evaluate(move);
        if (!result.Accepted)
        {
            _soundPlayer.Play(SoundEffect.Invalid);
            _hud.SetStatus($"【{puzzle.Title}】{result.Message}");
            return;
        }

        if (!_session.TryPlayMove(move))
        {
            _soundPlayer.Play(SoundEffect.Invalid);
            return;
        }

        if (result.DefenseReply is { } defense)
        {
            _hud.SetStatus($"【{puzzle.Title}】{result.Message}");
            _ = PlayDefenseAsync(defense);
        }
        else
        {
            _soundPlayer.Play(SoundEffect.Checkmate);
            _hud.SetStatus($"【{puzzle.Title}】{result.Message} 正解完成！");
        }
    }

    /// <summary>延迟落防守着；期间锁定棋盘输入，退出练习则放弃。</summary>
    private async Task PlayDefenseAsync(Move defense)
    {
        _boardInput.InputEnabled = false;
        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
        if (!IsPracticing || _puzzleController.IsSolved)
        {
            _boardInput.InputEnabled = true;
            return;
        }

        _session.TryPlayMove(defense);
        _boardInput.InputEnabled = true;
    }

    /// <summary>构建关于弹窗（MIT 声明 + Pikafish GPL-3.0 声明与源码指引）。</summary>
    private AcceptDialog BuildAboutDialog() => new()
    {
        Title = "关于 SuperChess",
        DialogText = """
			SuperChess — 中国象棋训练与 AI 练习工具

			本程序代码以 MIT 许可发布（详见项目 LICENSE 文件）。

			棋盘/棋子/音效素材与规则算法移植自开源项目
			「象棋鱼」chinese-chess-fish-android（MIT 许可）
			https://github.com/zfdang/chinese-chess-fish-android

			内置引擎 Pikafish 以 GPL-3.0 许可独立发布，本应用以其
			独立进程方式分发并通过 UCI 协议通信：
			https://github.com/official-pikafish/Pikafish
			""",
        OkButtonText = "关闭",
    };

    public override void _ExitTree()
    {
        // 退出时取消搜索并终止引擎进程。
        _session.Dispose();
    }

    /// <summary>应用走法：练习模式先过控制器先行校验，对弈模式直接落子（人机触发引擎应手）。</summary>
    private void OnMoveChosen(int fromX, int fromY, int toX, int toY)
    {
        var move = new Move(new Position(fromX, fromY), new Position(toX, toY));
        if (_lessonController.Current is not null)
        {
            HandleLessonMove(move);
            return;
        }

        if (_puzzleController.Current is not null)
        {
            HandlePuzzleMove(move);
            return;
        }

        if (!_session.TryPlayMove(move))
        {
            _soundPlayer.Play(SoundEffect.Invalid);
            return;
        }

        if (_session.Mode == GameMode.PlayWithEngine)
        {
            _ = PlayEngineMoveAsync();
        }
    }

    /// <summary>请求引擎应手；失败时提示并回退双人模式。</summary>
    private async Task PlayEngineMoveAsync()
    {
        try
        {
            await _session.RequestEngineMoveAsync().ConfigureAwait(true);
        }
        catch (Exception e)
        {
            GD.PrintErr($"引擎走子失败：{e.Message}");
            _hud.SetStatus("引擎出错，已切回双人模式");
            _session.SetMode(GameMode.TwoPlayers);
            _hud.SetMode(GameMode.TwoPlayers, _session.EngineAvailable);
        }
    }

    /// <summary>走子被应用（人类或引擎）：动画 + 音效 + 历史箭头 + 终局状态。</summary>
    private void OnMoveApplied(MoveAppliedEventArgs e)
    {
        _boardView.AnimateMove(e.Move);
        _arrows.ShowHistoryArrow(BoardView.CellCenter(e.Move.From), BoardView.CellCenter(e.Move.To));
        _soundPlayer.Play(e.IsCapture ? SoundEffect.Capture : SoundEffect.Move);
        if (e.IsCheckmate)
        {
            _soundPlayer.Play(SoundEffect.Checkmate);
            _hud.SetStatus(e.IsRedMove ? "红方胜（将死黑方）" : "黑方胜（将死红方）");
        }
        else if (e.IsCheck)
        {
            _soundPlayer.Play(SoundEffect.Check);
        }
        else if (e.IsStalemate)
        {
            _hud.SetStatus(e.IsRedMove ? "黑方困毙，红方胜" : "红方困毙，黑方胜");
        }

        if (_analysisEnabled && _session.Mode != GameMode.PlayWithEngine)
        {
            // 双人模式走子后立即刷新；人机模式等引擎应手结束（忙闲回落）再刷新。
            _ = RefreshAnalysisAsync();
        }
    }

    /// <summary>局面整体恢复：重注入新局面实例并全量重绘，清空箭头与评估。</summary>
    private void OnBoardReverted()
    {
        _boardInput.ClearSelection();
        _boardInput.Board = _session.CurrentBoard;
        _boardView.RenderBoard(_session.CurrentBoard);
        _arrows.ClearAll();
        _hud.ClearEval();
        if (_analysisEnabled && !IsPracticing)
        {
            _ = RefreshAnalysisAsync();
        }
    }

    /// <summary>显示引擎提示着法（选中框 + 落点标记，不落子）。</summary>
    private void OnHintProvided(Move move)
    {
        var board = _session.CurrentBoard;
        var chinese = ChineseNotation.ToChinese(board, move);
        _hud.SetStatus($"提示：{chinese}");
        _boardView.ShowSelection(move.From, Piece.IsRed(board.GetPiece(move.From)), [move.To]);
    }

    /// <summary>忙闲切换：引擎思考中关闭棋盘输入并更新状态栏。</summary>
    private void OnBusyChanged(bool busy)
    {
        _boardInput.InputEnabled = !busy;
        if (busy)
        {
            _hud.SetStatus("引擎思考中…");
        }
        else if (!_session.IsGameOver)
        {
            // 终局状态由 OnMoveApplied 写入，此处不覆盖。
            _hud.SetStatus(_session.Mode == GameMode.PlayWithEngine ? "人机对弈（执红）" : "双人对弈");
            if (_analysisEnabled && !IsPracticing)
            {
                // 引擎应手/提示结束后自动刷新连续分析（刷新自身的忙闲回落经防重入标志跳过）。
                _ = RefreshAnalysisAsync();
            }
        }
    }

    /// <summary>新局请求：练习模式中退出练习回对弈；对弈模式复位初始局面。</summary>
    private void OnNewGame()
    {
        if (IsPracticing)
        {
            ExitPractice();
            return;
        }

        _session.NewGame();
        _hud.SetStatus(_session.Mode == GameMode.PlayWithEngine ? "人机对弈（执红）" : "双人对弈");
    }

    /// <summary>悔棋请求：练习模式中重玩本课/本题；对弈模式回退一步。</summary>
    private void OnUndo()
    {
        if (_lessonController.Current is { } lesson)
        {
            _session.LoadFen(lesson.Fen);
            _lessonController.Reset();
            _hud.SetStatus($"【{lesson.Title}】重新开始：{lesson.Intro}");
            return;
        }

        if (_puzzleController.Current is { } puzzle)
        {
            _session.LoadFen(puzzle.Fen);
            _puzzleController.Reset();
            _hud.SetStatus($"【{puzzle.Title}】重新开始：{puzzle.Description}");
            return;
        }

        if (!_session.Undo())
        {
            _soundPlayer.Play(SoundEffect.Invalid);
            _hud.SetStatus("引擎思考中，无法悔棋");
        }
    }

    private async void OnHint()
    {
        var move = await _session.HintAsync().ConfigureAwait(true);
        if (move is null && !_session.Busy)
        {
            _soundPlayer.Play(SoundEffect.Invalid);
        }
        // 提示着法的展示由 HintProvided 事件处理。
    }

    /// <summary>
    /// 切换连续分析开关：开启后走子/悔棋/新局/引擎应手结束均自动重新分析并刷新
    /// 建议箭头与评估，无需每次点击；关闭时清除建议显示。
    /// </summary>
    private async void OnAnalyze()
    {
        if (_analysisEnabled)
        {
            StopAnalysis();
            return;
        }

        _analysisEnabled = true;
        _hud.SetAnalysisActive(true);
        _hud.SetStatus("分析已开启，走子后自动刷新建议");
        await RefreshAnalysisAsync().ConfigureAwait(true);
    }

    /// <summary>关闭连续分析并清除建议箭头与评估显示。</summary>
    private void StopAnalysis()
    {
        _analysisEnabled = false;
        _hud.SetAnalysisActive(false);
        _arrows.ClearAll();
        _hud.ClearEval();
    }

    /// <summary>
    /// 连续分析刷新：非练习、非终局且引擎空闲时重新分析当前局面；
    /// 引擎忙时跳过，留待忙闲回落事件再触发。防重入标志避免忙闲事件循环。
    /// </summary>
    private async Task RefreshAnalysisAsync()
    {
        if (!_analysisEnabled || _analysisRefreshing || _session.Busy || IsPracticing || _session.IsGameOver)
        {
            return;
        }

        _analysisRefreshing = true;
        try
        {
            var infos = await _session.AnalyzeAsync(GameSession.DefaultAnalyzeMultiPv).ConfigureAwait(true);
            if (infos.Count > 0)
            {
                ShowAnalysis(infos);
            }
        }
        finally
        {
            _analysisRefreshing = false;
        }
    }

    /// <summary>显示分析结果：MultiPV 建议箭头 + 评估列表（当前分值 + 每路建议）。</summary>
    private void ShowAnalysis(IReadOnlyList<MultiPvInfo> infos)
    {
        var redToMove = _session.CurrentBoard.RedToMove;
        var suggestions = new List<(Vector2, Vector2)>();
        foreach (var info in infos)
        {
            if (info.Pv.Count == 0 || Move.FromUcci(info.Pv[0]) is not { } move)
            {
                continue;
            }

            suggestions.Add((BoardView.CellCenter(move.From), BoardView.CellCenter(move.To)));
            var chinese = ChineseNotation.ToChinese(_session.CurrentBoard, move);
            _hud.AppendEval($"{_session.History.Count + 1}. {chinese}  {FormatScore(info, redToMove)}");
        }

        if (suggestions.Count > 0)
        {
            _arrows.ShowSuggestions(suggestions);
            _hud.SetEval(FormatScore(infos[0], redToMove));
        }
    }

    /// <summary>分值文本：引擎分值为走子方视角，统一换算为红方视角
    /// （正数红方占优；mate 分值显示 #N / -#N）。</summary>
    private static string FormatScore(MultiPvInfo info, bool redToMove)
    {
        var value = info.ScoreCp * (redToMove ? 1 : -1);
        if (info.IsMate)
        {
            return value > 0 ? $"#{Math.Abs(value)}" : $"-#{Math.Abs(value)}";
        }

        return (value / 100.0).ToString("+0.00;-0.00");
    }

    /// <summary>复制当前局面 FEN 到系统剪贴板（AC-8 导出）。</summary>
    private void OnCopyFen()
    {
        DisplayServer.ClipboardSet(_session.CurrentBoard.ToFen());
        _hud.SetStatus("已复制当前局面 FEN");
    }

    /// <summary>从系统剪贴板载入 FEN；非法 FEN 显示出错字段且不崩溃（AC-8）。</summary>
    private void OnPasteFen()
    {
        var fen = DisplayServer.ClipboardGet().Trim();
        if (fen.Length == 0)
        {
            _soundPlayer.Play(SoundEffect.Invalid);
            _hud.SetStatus("剪贴板为空，无法载入 FEN");
            return;
        }

        try
        {
            if (_session.LoadFen(fen))
            {
                _hud.SetStatus("已载入剪贴板 FEN");
            }
            else
            {
                _soundPlayer.Play(SoundEffect.Invalid);
                _hud.SetStatus("引擎思考中，无法载入 FEN");
            }
        }
        catch (FenFormatException e)
        {
            _soundPlayer.Play(SoundEffect.Invalid);
            _hud.SetStatus($"非法 FEN：{e.FieldName} 字段错误（{e.Message}）");
        }
    }

    /// <summary>切换对局模式；首次进入人机模式时启动引擎。</summary>
    private async void OnModeToggled()
    {
        if (_session.Mode == GameMode.TwoPlayers)
        {
            if (!_session.EngineAvailable)
            {
                var projectDir = ProjectSettings.GlobalizePath("res://");
                var enginePath = EngineLocator.ResolveEnginePath(projectDir);
                if (enginePath is null)
                {
                    _hud.SetStatus("未找到引擎（Android 内置；桌面需放置 engines/windows/）");
                    _soundPlayer.Play(SoundEffect.Invalid);
                    return;
                }

                _hud.SetStatus("引擎启动中…");
                var uci = new UciSession();
                try
                {
                    var options = _hud.CollectOptions() with
                    {
                        EvalFile = EngineLocator.ResolveNnuePath(projectDir),
                    };
                    await uci.StartAsync(enginePath, options).ConfigureAwait(true);
                }
                catch (Exception e)
                {
                    GD.PrintErr($"引擎启动失败：{e.Message}");
                    _hud.SetStatus($"引擎启动失败：{e.Message}");
                    await uci.DisposeAsync().ConfigureAwait(true);
                    _soundPlayer.Play(SoundEffect.Invalid);
                    return;
                }

                if (!_session.AttachEngine(uci))
                {
                    await uci.DisposeAsync().ConfigureAwait(true);
                }
            }

            if (_session.SetMode(GameMode.PlayWithEngine))
            {
                _hud.SetMode(GameMode.PlayWithEngine, true);
                _hud.SetStatus("人机对弈（执红）");
            }
        }
        else if (_session.SetMode(GameMode.TwoPlayers))
        {
            _hud.SetMode(GameMode.TwoPlayers, _session.EngineAvailable);
            _hud.SetStatus("双人对弈");
        }
    }

    /// <summary>打开课程列表（引擎思考中拒绝）。</summary>
    private void OnLessonRequested() => TryOpenPicker(_lessonDialog);

    /// <summary>打开残局题库列表（引擎思考中拒绝）。</summary>
    private void OnPuzzleRequested() => TryOpenPicker(_puzzleDialog);

    /// <summary>强度设置变化：非思考状态下同步到引擎。</summary>
    private async void OnOptionsChanged(EngineOptions options)
    {
        try
        {
            await _session.ApplyEngineOptionsAsync(options).ConfigureAwait(true);
        }
        catch (Exception e)
        {
            GD.PrintErr($"下发引擎选项失败：{e.Message}");
        }
    }
}
