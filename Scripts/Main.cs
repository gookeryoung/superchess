using Godot;
using SuperChess.Engine;

namespace SuperChess;

/// <summary>
/// M0/M1 场景入口：验证工程可构建运行，并触发引擎通信 PoC（M4 由正式对弈流程替换）。
/// </summary>
public partial class Main : Node
{
    public override void _Ready()
    {
        GD.Print("SuperChess M0 工程初始化成功");
        EnginePoc.Run();
    }
}
