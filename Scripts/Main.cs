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
    private SoundPlayer _soundPlayer = null!;
    private HudPanel _hud = null!;

    public override void _Ready()
    {
        _boardView = GetNode<BoardView>("Board");
        _boardInput = GetNode<BoardInput>("Board/BoardInput");
        _hud = GetNode<HudPanel>("Hud");
        _soundPlayer = new SoundPlayer();
        AddChild(_soundPlayer);

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
        _hud.ModeToggled += OnModeToggled;
        _hud.OptionsChanged += OnOptionsChanged;
        _hud.Position = new Vector2(0, BoardView.NativeHeight * BoardScale + 16);
    }

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

    /// <summary>走子被应用（人类或引擎）：动画 + 音效 + 终局状态。</summary>
    private void OnMoveApplied(MoveAppliedEventArgs e)
    {
        _boardView.AnimateMove(e.Move);
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

    /// <summary>局面整体恢复：重注入新局面实例并全量重绘。</summary>
    private void OnBoardReverted()
    {
        _boardInput.ClearSelection();
        _boardInput.Board = _session.CurrentBoard;
        _boardView.RenderBoard(_session.CurrentBoard);
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

    /// <summary>切换对局模式；首次进入人机模式时启动引擎。</summary>
    private async void OnModeToggled()
    {
        if (_session.Mode == GameMode.TwoPlayers)
        {
            if (!_session.EngineAvailable)
            {
                var enginePath = EngineLocator.ResolveEnginePath();
                if (enginePath is null)
                {
                    _hud.SetStatus("未找到引擎（Android 版内置），双人模式可用");
                    _soundPlayer.Play(SoundEffect.Invalid);
                    return;
                }

                _hud.SetStatus("引擎启动中…");
                var uci = new UciSession();
                try
                {
                    var options = _hud.CollectOptions() with { EvalFile = EngineLocator.ResolveNnuePath() };
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
