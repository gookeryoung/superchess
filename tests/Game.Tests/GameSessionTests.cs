using SuperChess.Core;
using Xunit;

namespace SuperChess.Game.Tests;

/// <summary>
/// GameSession 对局流转与忙闲门闸测试（设计第 23 步：对照参考项目状态图覆盖
/// 「引擎思考中悔棋/停止/提示/新局」等并发边界）。
/// </summary>
public class GameSessionTests
{
    private readonly FakeUciSession _fake = new();
    private readonly GameSession _session;

    public GameSessionTests()
    {
        _session = new GameSession(_fake);
        Assert.True(_session.AttachEngine(_fake));
        Assert.True(_session.SetMode(GameMode.PlayWithEngine));
    }

    /// <summary>人走（炮二平五）→ 引擎应（黑马 h9g7）完整回合。</summary>
    [Fact]
    public async Task HumanMove_EngineReplies_AppliesBothMoves()
    {
        Assert.True(_session.TryPlayMove(Move.FromUcci("h2e2")!.Value));
        var task = _session.RequestEngineMoveAsync();
        await _fake.WaitGoRequestedAsync();

        // 引擎思考中：门闸生效。
        Assert.True(_session.Busy);
        Assert.False(_session.TryPlayMove(Move.FromUcci("b0c2")!.Value));
        Assert.False(_session.Undo());
        Assert.Null(await _session.HintAsync());

        _fake.Release("h9g7");
        Assert.True(await task);

        Assert.Equal(2, _session.History.Count);
        Assert.Equal(Piece.BlackKnight, _session.CurrentBoard.GetPiece(new Position(6, 2)));
        Assert.True(_session.CurrentBoard.RedToMove);
    }

    /// <summary>引擎思考中新对局：取消搜索、复位局面、迟到 bestmove 被丢弃。</summary>
    [Fact]
    public async Task NewGame_WhileThinking_CancelsSearchAndDiscardsLateBestMove()
    {
        Assert.True(_session.TryPlayMove(Move.FromUcci("h2e2")!.Value));
        var task = _session.RequestEngineMoveAsync();
        await _fake.WaitGoRequestedAsync();

        _session.NewGame();
        Assert.Empty(_session.History);
        Assert.True(_session.CurrentBoard.RedToMove);
        Assert.Equal(Piece.RedCannon, _session.CurrentBoard.GetPiece(new Position(1, 7)));

        // 引擎迟到返回 bestmove，代际已过期，不应落子。
        _fake.Release("h9g7");
        Assert.False(await task);
        Assert.Equal(Piece.RedCannon, _session.CurrentBoard.GetPiece(new Position(1, 7)));
        Assert.False(_session.Busy);
    }

    /// <summary>悔棋在人机模式连退两步；历史仅剩一手时退到初始局面。</summary>
    [Fact]
    public async Task Undo_InEngineMode_PopsTwoRecords()
    {
        Assert.True(_session.TryPlayMove(Move.FromUcci("h2e2")!.Value));
        var task = _session.RequestEngineMoveAsync();
        await _fake.WaitGoRequestedAsync();
        _fake.Release("h9g7");
        Assert.True(await task);
        Assert.Equal(2, _session.History.Count);

        Assert.True(_session.Undo());
        Assert.Empty(_session.History);
        Assert.True(_session.CurrentBoard.RedToMove);
        Assert.Equal(Piece.RedCannon, _session.CurrentBoard.GetPiece(new Position(1, 7)));

        // 单条历史（引擎未应手）悔棋退一手。
        Assert.True(_session.TryPlayMove(Move.FromUcci("h2e2")!.Value));
        Assert.True(_session.Undo());
        Assert.Empty(_session.History);
        Assert.Equal(Piece.RedCannon, _session.CurrentBoard.GetPiece(new Position(1, 7)));
    }

    /// <summary>双人模式悔棋只退一步。</summary>
    [Fact]
    public void Undo_InTwoPlayersMode_PopsOneRecord()
    {
        var twoPlayerSession = new GameSession();
        Assert.True(twoPlayerSession.SetMode(GameMode.TwoPlayers) || twoPlayerSession.Mode == GameMode.TwoPlayers);
        Assert.True(twoPlayerSession.TryPlayMove(Move.FromUcci("h2e2")!.Value));
        Assert.True(twoPlayerSession.TryPlayMove(Move.FromUcci("h9g7")!.Value));

        Assert.True(twoPlayerSession.Undo());
        Assert.Single(twoPlayerSession.History);
        Assert.Equal(Piece.BlackKnight, twoPlayerSession.CurrentBoard.GetPiece(new Position(7, 0)));
    }

    /// <summary>非走子方走法与非法走法被拒绝。</summary>
    [Fact]
    public void TryPlayMove_RejectsWrongTurnAndIllegalMove()
    {
        // 黑先走：被走子方校验拒绝。
        Assert.False(_session.TryPlayMove(Move.FromUcci("h9g7")!.Value));
        // 马走直线（b0b1）：被合法性校验拒绝。
        Assert.False(_session.TryPlayMove(Move.FromUcci("b0b1")!.Value));
        Assert.Empty(_session.History);
    }

    /// <summary>提示返回着法但不落子。</summary>
    [Fact]
    public async Task HintAsync_ReturnsMoveWithoutApplying()
    {
        var task = _session.HintAsync();
        await _fake.WaitGoRequestedAsync();
        _fake.Release("h9g7");

        var hint = await task;
        Assert.Equal("h9g7", hint!.Value.ToUcci());
        Assert.Empty(_session.History);
        Assert.Equal(Piece.BlackKnight, _session.CurrentBoard.GetPiece(new Position(7, 0)));
    }

    /// <summary>对局结束后走子与引擎请求均被拒绝（将死局面）。</summary>
    [Fact]
    public async Task GameOver_BlocksMovesAndEngineRequest()
    {
        // 黑方被将死局面：红车 d9 控制 d 线，黑王动帅即白脸将。
        Assert.True(_session.LoadFen("3k5/9/9/9/9/9/9/9/3R5/4K4 b - - 0 1"));
        Assert.True(_session.IsGameOver);
        Assert.False(_session.TryPlayMove(Move.FromUcci("d9e9")!.Value));
        Assert.False(await _session.RequestEngineMoveAsync());
    }

    /// <summary>引擎思考中不切模式；AttachEngine 幂等拒绝。</summary>
    [Fact]
    public async Task SetMode_AndAttachEngine_RejectedWhileBusy()
    {
        _session.TryPlayMove(Move.FromUcci("h2e2")!.Value);
        var task = _session.RequestEngineMoveAsync();
        await _fake.WaitGoRequestedAsync();

        Assert.False(_session.SetMode(GameMode.TwoPlayers));
        Assert.False(_session.AttachEngine(new FakeUciSession()));

        _fake.Release("h9g7");
        await task;
    }

    /// <summary>LoadFen 非法 FEN 抛出 FenFormatException 并保持局面不变。</summary>
    [Fact]
    public void LoadFen_InvalidFen_ThrowsAndKeepsBoard()
    {
        _session.TryPlayMove(Move.FromUcci("h2e2")!.Value);
        Assert.Throws<FenFormatException>(() => _session.LoadFen("9/9 w"));
        Assert.Single(_session.History);
        Assert.Equal(Piece.RedCannon, _session.CurrentBoard.GetPiece(new Position(4, 7)));
    }

    /// <summary>引擎不可用时双人模式可完整走子；切人机被拒绝。</summary>
    [Fact]
    public void NoEngine_TwoPlayersWorks_PlayWithEngineRejected()
    {
        var bareSession = new GameSession();
        Assert.False(bareSession.SetMode(GameMode.PlayWithEngine));
        Assert.True(bareSession.TryPlayMove(Move.FromUcci("h2e2")!.Value));
        Assert.True(bareSession.TryPlayMove(Move.FromUcci("h9g7")!.Value));
        Assert.Equal(2, bareSession.History.Count);
    }
}
