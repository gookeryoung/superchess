using Godot;

namespace SuperChess.UI;

/// <summary>音效类型（与 assets/sounds 下文件一一对应）。</summary>
public enum SoundEffect
{
    /// <summary>选中棋子。</summary>
    Select,

    /// <summary>走子。</summary>
    Move,

    /// <summary>吃子。</summary>
    Capture,

    /// <summary>将军。</summary>
    Check,

    /// <summary>将死/困毙。</summary>
    Checkmate,

    /// <summary>非法操作。</summary>
    Invalid,
}

/// <summary>
/// 音效播放器：每种音效独立 AudioStreamPlayer，允许走子音与将军音叠加播放。
/// 由 Main 在 _Ready 中动态创建并挂载。
/// </summary>
public partial class SoundPlayer : Node
{
    private static readonly Dictionary<SoundEffect, string> Paths = new()
    {
        [SoundEffect.Select] = "res://assets/sounds/select.wav",
        [SoundEffect.Move] = "res://assets/sounds/move.mp3",
        [SoundEffect.Capture] = "res://assets/sounds/capture.mp3",
        [SoundEffect.Check] = "res://assets/sounds/check.mp3",
        [SoundEffect.Checkmate] = "res://assets/sounds/checkmate.ogg",
        [SoundEffect.Invalid] = "res://assets/sounds/invalid.mp3",
    };

    private readonly Dictionary<SoundEffect, AudioStreamPlayer> _players = new();

    public override void _Ready()
    {
        foreach (var (effect, path) in Paths)
        {
            var player = new AudioStreamPlayer { Stream = GD.Load<AudioStream>(path) };
            AddChild(player);
            _players[effect] = player;
        }
    }

    /// <summary>播放指定音效（重复触发会从头重播同一音效）。</summary>
    public void Play(SoundEffect effect) => _players[effect].Play();
}
