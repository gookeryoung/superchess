using Godot;
using SuperChess.Core;
using SuperChess.UI;

namespace SuperChess;

/// <summary>
/// M3 场景入口：装配棋盘视图、输入与音效，驱动双人本地对弈回合流转
/// （M4 由 GameSession 接管人机对弈流程）。
/// </summary>
public partial class Main : Node2D
{
    /// <summary>棋盘整体缩放：底图 1240 宽等比缩放至 1080 视口宽。</summary>
    private const float BoardScale = 1080f / BoardView.NativeWidth;

    private readonly Board _board = new();
    private BoardView _boardView = null!;
    private BoardInput _boardInput = null!;
    private SoundPlayer _soundPlayer = null!;

    public override void _Ready()
    {
        _boardView = GetNode<BoardView>("Board");
        _boardInput = GetNode<BoardInput>("Board/BoardInput");
        _soundPlayer = new SoundPlayer();
        AddChild(_soundPlayer);

        _boardView.Scale = new Vector2(BoardScale, BoardScale);
        _boardInput.Board = _board;
        _boardInput.SoundPlayer = _soundPlayer;
        _boardInput.MoveChosen += OnMoveChosen;
        _boardView.RenderBoard(_board);
    }

    /// <summary>执行用户选择的走法：更新局面、播放动画与音效、判定将军/终局。</summary>
    private void OnMoveChosen(int fromX, int fromY, int toX, int toY)
    {
        var move = new Move(new Position(fromX, fromY), new Position(toX, toY));
        var piece = _board.GetPiece(move.From);
        if (!Piece.IsValid(piece) || Piece.IsRed(piece) != _board.RedToMove || !Rule.IsLegalMove(_board, move))
        {
            _soundPlayer.Play(SoundEffect.Invalid);
            return;
        }

        var captured = _board.GetPiece(move.To);
        _board.DoMove(move);
        _boardView.AnimateMove(move);

        // 吃子音优先于走子音；走后轮到的一方被将军/将死则叠加对应音效。
        _soundPlayer.Play(Piece.IsValid(captured) ? SoundEffect.Capture : SoundEffect.Move);
        if (Rule.IsCheckmate(_board, _board.RedToMove))
        {
            _soundPlayer.Play(SoundEffect.Checkmate);
        }
        else if (Rule.IsInCheck(_board, _board.RedToMove))
        {
            _soundPlayer.Play(SoundEffect.Check);
        }
    }
}
