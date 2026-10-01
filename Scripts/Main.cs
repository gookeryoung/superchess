using Godot;

namespace SuperChess;

/// <summary>
/// M0 空场景入口：仅验证 Godot .NET 工程可构建与运行，后续里程碑逐步替换。
/// </summary>
public partial class Main : Node
{
    public override void _Ready()
    {
        GD.Print("SuperChess M0 工程初始化成功");
    }
}
