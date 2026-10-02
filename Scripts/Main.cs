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
    private BoardView _boardView = null!;
    private BoardInput _boardInput = null!;
    private ArrowLayer _arrows = null!;
    private SoundPlayer _soundPlayer = null!;
    private HudPanel _hud = null!;
    private AcceptDialog _aboutDialog = null!;

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
        _hud.Position = new Vector2(0, BoardView.NativeHeight * BoardScale + 16);
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

    /// <summary>应用人类走法；人机模式下触发引擎应手。</summary>
    private void OnMoveChosen(int fromX, int fromY, int toX, int toY)
    {
        var move = new Move(new Position(fromX, fromY), new Position(toX, toY));
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
    }

    /// <summary>局面整体恢复：重注入新局面实例并全量重绘，清空箭头与评估。</summary>
    private void OnBoardReverted()
    {
        _boardInput.ClearSelection();
        _boardInput.Board = _session.CurrentBoard;
        _boardView.RenderBoard(_session.CurrentBoard);
        _arrows.ClearAll();
        _hud.ClearEval();
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
        }
    }

    private void OnNewGame()
    {
        _session.NewGame();
        _hud.SetStatus(_session.Mode == GameMode.PlayWithEngine ? "人机对弈（执红）" : "双人对弈");
    }

    private void OnUndo()
    {
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

    /// <summary>分析当前局面：MultiPV 建议箭头 + 评估显示。</summary>
    private async void OnAnalyze()
    {
        var infos = await _session.AnalyzeAsync(GameSession.DefaultAnalyzeMultiPv).ConfigureAwait(true);
        if (infos.Count == 0)
        {
            if (!_session.Busy)
            {
                _soundPlayer.Play(SoundEffect.Invalid);
                _hud.SetStatus("引擎不可用或引擎思考中，无法分析");
            }

            return;
        }

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
