using SuperChess.Core;

namespace SuperChess.Game;

/// <summary>对局模式。</summary>
public enum GameMode
{
    /// <summary>双人本地对弈（同屏轮流走子）。</summary>
    TwoPlayers,

    /// <summary>人机对弈（人类执红，引擎执黑）。</summary>
    PlayWithEngine,
}

/// <summary>
/// 一条走子历史记录：move（结构化）、ucci（UCCI 字符串）、chs（中文纵线记谱）、isRedMove。
/// 悔棋从尾部弹出并重放恢复局面。
/// </summary>
public sealed record HistoryRecord(Move Move, string Ucci, string Chs, bool IsRedMove);

/// <summary>一步走子被应用后的事件参数（UI 据此播放动画与音效）。</summary>
public sealed class MoveAppliedEventArgs : EventArgs
{
    /// <summary>被应用的走法。</summary>
    public Move Move { get; }

    /// <summary>中文纵线记谱（如「炮二平五」）。</summary>
    public string Chinese { get; }

    /// <summary>是否吃子。</summary>
    public bool IsCapture { get; }

    /// <summary>是否红方走子。</summary>
    public bool IsRedMove { get; }

    /// <summary>走子后对方是否被将军。</summary>
    public bool IsCheck { get; }

    /// <summary>走子后对方是否被将死。</summary>
    public bool IsCheckmate { get; }

    /// <summary>走子后对方是否被困毙。</summary>
    public bool IsStalemate { get; }

    public MoveAppliedEventArgs(Move move, string chinese, bool isCapture, bool isRedMove,
        bool isCheck, bool isCheckmate, bool isStalemate)
    {
        Move = move;
        Chinese = chinese;
        IsCapture = isCapture;
        IsRedMove = isRedMove;
        IsCheck = isCheck;
        IsCheckmate = isCheckmate;
        IsStalemate = isStalemate;
    }
}
