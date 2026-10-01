using Godot;
using SuperChess.Core;

namespace SuperChess.UI;

/// <summary>
/// 棋盘输入：触点/点击 → 格坐标逆映射，两段式交互（选子 → 落子）。
/// 仅允许选中走子方棋子；选中后显示合法落点提示，点击合法落点发出 MoveChosen。
/// </summary>
public partial class BoardInput : Node2D
{
    private BoardView _view = null!;

    /// <summary>当前对局局面（由 Main 装配时注入；走子方决定可选中颜色）。</summary>
    public Board? Board { get; set; }

    /// <summary>音效播放器（选子/非法点击音效，由 Main 装配时注入）。</summary>
    public SoundPlayer? SoundPlayer { get; set; }

    /// <summary>用户完成两段式选子落子时发出（坐标为格坐标）。</summary>
    [Signal]
    public delegate void MoveChosenEventHandler(int fromX, int fromY, int toX, int toY);

    private Position? _selected;
    private List<Position> _targets = [];

    public override void _Ready()
    {
        _view = GetParent<BoardView>();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Board is null || _view.IsAnimating ||
            @event is not InputEventMouseButton
            {
                Pressed: true,
                ButtonIndex: MouseButton.Left,
            })
        {
            return;
        }

        var pos = _view.TryHit(_view.ToLocal(GetGlobalMousePosition()));
        if (pos is not null)
        {
            HandleClick(pos.Value);
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>清除当前选中状态与落点提示（走子完成后由内部调用）。</summary>
    public void ClearSelection()
    {
        _selected = null;
        _targets = [];
        _view.ClearSelection();
    }

    /// <summary>按两段式状态机处理一次有效点击。</summary>
    private void HandleClick(Position pos)
    {
        var board = Board!;
        var piece = board.GetPiece(pos);

        // 第二段：点击合法落点，发出走子。
        if (_selected is not null && _targets.Contains(pos))
        {
            EmitSignal(SignalName.MoveChosen, _selected.Value.X, _selected.Value.Y, pos.X, pos.Y);
            ClearSelection();
            return;
        }

        // 点击己方棋子：选中（或换选）。
        if (Piece.IsValid(piece) && Piece.IsRed(piece) == board.RedToMove)
        {
            _selected = pos;
            _targets = Rule.GetLegalMoves(board, pos).Select(m => m.To).ToList();
            _view.ShowSelection(pos, Piece.IsRed(piece), _targets);
            SoundPlayer?.Play(SoundEffect.Select);
            return;
        }

        // 已有选中但点击非法落点或对方棋子：非法提示。
        if (_selected is not null)
        {
            SoundPlayer?.Play(SoundEffect.Invalid);
        }

        // 无选中时点击空点或对方棋子：忽略。
    }
}
