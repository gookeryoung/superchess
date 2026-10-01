using SuperChess.Core;
using SuperChess.Engine;

namespace SuperChess.Game;

/// <summary>
/// 对局会话（忙闲门闸）：持有局面与走子历史，驱动双人/人机对弈回合流转。
/// 引擎思考中（Busy）拒绝走子/悔棋/提示等互斥操作；新对局取消搜索并以
/// 代际计数丢弃迟到的引擎结果。零 Godot 依赖，可独立单测。
/// </summary>
public sealed class GameSession : IDisposable
{
    /// <summary>引擎搜索默认深度（NNUE 引擎在此深度内响应迅速，适配真机）。</summary>
    public const int DefaultSearchDepth = 12;

    private readonly List<HistoryRecord> _history = [];
    private Board _board = new();
    private IUciSession? _engine;
    private int _searchGeneration;
    private CancellationTokenSource? _searchCts;
    private int _busy;

    /// <summary>构造对局会话；engine 为 null 时引擎功能不可用（双人模式仍可完整对弈）。</summary>
    public GameSession(IUciSession? engine = null)
    {
        _engine = engine;
    }

    /// <summary>当前对局模式。</summary>
    public GameMode Mode { get; private set; } = GameMode.TwoPlayers;

    /// <summary>当前局面。</summary>
    public Board CurrentBoard => _board;

    /// <summary>走子历史（尾部为最新一手）。</summary>
    public IReadOnlyList<HistoryRecord> History => _history;

    /// <summary>引擎是否可用（已附加引擎会话）。</summary>
    public bool EngineAvailable => _engine is not null;

    /// <summary>对局是否结束（将死或困毙）。</summary>
    public bool IsGameOver =>
        Rule.IsCheckmate(_board, _board.RedToMove) || Rule.IsStalemate(_board, _board.RedToMove);

    /// <summary>引擎是否思考中（忙闲门闸：Busy 期间拒绝走子/悔棋/提示/切模式）。</summary>
    public bool Busy => Volatile.Read(ref _busy) == 1;

    /// <summary>引擎搜索深度。</summary>
    public int SearchDepth { get; set; } = DefaultSearchDepth;

    /// <summary>一步走子被应用（人类或引擎）。</summary>
    public event Action<MoveAppliedEventArgs>? MoveApplied;

    /// <summary>局面被整体恢复（悔棋/新对局/载入 FEN），UI 应全量重绘。</summary>
    public event Action? BoardReverted;

    /// <summary>引擎给出提示着法（不落子）。</summary>
    public event Action<Move>? HintProvided;

    /// <summary>忙闲状态切换（true=引擎开始思考）。</summary>
    public event Action<bool>? BusyChanged;

    /// <summary>
    /// 附加引擎会话（异步启动完成后由 UI 层调用）；Busy 期间拒绝。
    /// 引擎经构造函数注入时同实例重复附加视为幂等成功。
    /// </summary>
    public bool AttachEngine(IUciSession engine)
    {
        if (Busy || (_engine is not null && !ReferenceEquals(_engine, engine)))
        {
            return false;
        }

        _engine = engine;
        return true;
    }

    /// <summary>切换对局模式；引擎不可用或 Busy 期间返回 false。</summary>
    public bool SetMode(GameMode mode)
    {
        if (Busy || mode == Mode)
        {
            return false;
        }

        if (mode == GameMode.PlayWithEngine && !EngineAvailable)
        {
            return false;
        }

        Mode = mode;
        return true;
    }

    /// <summary>
    /// 应用一步人类走法：校验走子方归属与合法性后落子、记历史、发 MoveApplied。
    /// Busy（引擎思考中）或对局已结束时返回 false。
    /// </summary>
    public bool TryPlayMove(Move move)
    {
        if (Busy || IsGameOver)
        {
            return false;
        }

        return ApplyMoveCore(move);
    }

    /// <summary>
    /// 走法校验与落子的公共实现（不检查 Busy：引擎代落的着法在 Busy 期间到达）。
    /// 走子方归属或走法非法返回 false。
    /// </summary>
    private bool ApplyMoveCore(Move move)
    {
        var piece = _board.GetPiece(move.From);
        if (!Piece.IsValid(piece) || Piece.IsRed(piece) != _board.RedToMove || !Rule.IsLegalMove(_board, move))
        {
            return false;
        }

        var isRedMove = Piece.IsRed(piece);
        var captured = _board.GetPiece(move.To);
        var chinese = ChineseNotation.ToChinese(_board, move);
        _board.DoMove(move);
        _history.Add(new HistoryRecord(move, move.ToUcci(), chinese, isRedMove));

        MoveApplied?.Invoke(new MoveAppliedEventArgs(
            move, chinese, Piece.IsValid(captured), isRedMove,
            Rule.IsInCheck(_board, _board.RedToMove),
            Rule.IsCheckmate(_board, _board.RedToMove),
            Rule.IsStalemate(_board, _board.RedToMove)));
        return true;
    }

    /// <summary>
    /// 请求引擎走一步（人机模式）：搜索当前局面并把 bestmove 落子。
    /// 新对局/悔棋导致的代际变化会丢弃过期结果。非人机模式、Busy、对局结束或引擎
    /// 未附加时返回 false；引擎返回非法着法（异常情形）同样返回 false。
    /// </summary>
    public async Task<bool> RequestEngineMoveAsync(CancellationToken ct = default)
    {
        if (_engine is null || Mode != GameMode.PlayWithEngine || Busy || IsGameOver)
        {
            return false;
        }

        SetBusy(true);
        try
        {
            var generation = ++_searchGeneration;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _searchCts = cts;
            var result = await _engine.GoAsync(BuildGoParams(), cts.Token).ConfigureAwait(true);
            _searchCts = null;
            if (generation != _searchGeneration || IsGameOver || result.BestMove is null)
            {
                return false;
            }

            var move = Move.FromUcci(result.BestMove);
            return move is not null && ApplyMoveCore(move.Value);
        }
        finally
        {
            _searchCts = null;
            SetBusy(false);
        }
    }

    /// <summary>
    /// 悔棋：人机模式连退两步（引擎 + 人类），双人模式退一步；历史不足则退到初始局面。
    /// Busy 期间返回 false。
    /// </summary>
    public bool Undo()
    {
        if (Busy || _history.Count == 0)
        {
            return false;
        }

        var popCount = Mode == GameMode.PlayWithEngine ? Math.Min(2, _history.Count) : 1;
        _history.RemoveRange(_history.Count - popCount, popCount);
        RebuildBoard();
        BoardReverted?.Invoke();
        return true;
    }

    /// <summary>
    /// 向引擎请求一步提示（不落子）；Busy、对局结束或引擎不可用时返回 null。
    /// </summary>
    public async Task<Move?> HintAsync(CancellationToken ct = default)
    {
        if (_engine is null || Busy || IsGameOver)
        {
            return null;
        }

        SetBusy(true);
        try
        {
            var generation = ++_searchGeneration;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _searchCts = cts;
            var result = await _engine.GoAsync(BuildGoParams(), cts.Token).ConfigureAwait(true);
            _searchCts = null;
            if (generation != _searchGeneration || result.BestMove is null)
            {
                return null;
            }

            var move = Move.FromUcci(result.BestMove);
            if (move is not null)
            {
                HintProvided?.Invoke(move.Value);
            }

            return move;
        }
        finally
        {
            _searchCts = null;
            SetBusy(false);
        }
    }

    /// <summary>开始新对局：清空历史并复位初始局面；引擎思考中则先取消搜索。</summary>
    public void NewGame()
    {
        if (Busy)
        {
            _searchGeneration++;
            _searchCts?.Cancel();
            _engine?.Stop();
        }

        _history.Clear();
        _board = new Board();
        BoardReverted?.Invoke();
    }

    /// <summary>载入 FEN 局面（AC-8 导入入口）；清空历史。Busy 期间返回 false，非法 FEN 抛 FenFormatException。</summary>
    public bool LoadFen(string fen)
    {
        if (Busy)
        {
            return false;
        }

        _board = Board.FromFen(fen);
        _history.Clear();
        BoardReverted?.Invoke();
        return true;
    }

    /// <summary>下发引擎强度选项；Busy 或引擎未附加时返回 false。</summary>
    public async Task<bool> ApplyEngineOptionsAsync(EngineOptions options, CancellationToken ct = default)
    {
        if (_engine is null || Busy)
        {
            return false;
        }

        await _engine.ApplyOptionsAsync(options, ct).ConfigureAwait(true);
        return true;
    }

    /// <summary>组装搜索请求参数（当前局面 + 默认深度）。</summary>
    private GoParams BuildGoParams() => new() { Fen = _board.ToFen(), Depth = SearchDepth };

    /// <summary>从初始局面重放历史恢复当前 _board。</summary>
    private void RebuildBoard()
    {
        var board = new Board();
        foreach (var record in _history)
        {
            board.DoMove(record.Move);
        }

        _board = board;
    }

    private void SetBusy(bool busy)
    {
        Volatile.Write(ref _busy, busy ? 1 : 0);
        BusyChanged?.Invoke(busy);
    }

    /// <summary>释放引擎会话（退出应用时调用）。</summary>
    public void Dispose()
    {
        _searchCts?.Cancel();
        _engine?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _engine = null;
    }
}
