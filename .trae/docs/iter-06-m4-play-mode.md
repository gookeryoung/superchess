# iter-06 M4 对弈模式（UCI 会话 + 对局会话 + 强度设置 UI）

## 需求清单

- M4：UciSession 完整实现、GameSession 人机对弈循环（忙闲门闸/Undo/Hint）、强度设置 UI、并发场景测试（设计 `.trae/designs/godot-xiangqi-android.md` M4 节，AC-3/AC-4/AC-6 的软件层抓手）

## 迭代目标

- `Scripts/Engine/`：IUciSession + UciSession（握手/setoption/position+go/stop/info 解析）+ EngineOptions
- `Scripts/Game/`：GameSession（对局状态、忙闲门闸、Undo/Hint/NewGame/LoadFen）+ GameTypes
- `Scripts/UI/HudPanel.cs`：强度设置与对局控制面板；Main 重构为 GameSession 驱动
- `tests/Game.Tests/`：FakeUciSession 并发边界测试

## 改动文件

- 新增：Scripts/Engine/EngineOptions.cs、UciSession.cs，Scripts/Game/GameTypes.cs、GameSession.cs，Scripts/UI/HudPanel.cs，tests/Game.Tests/（csproj + FakeUciSession + GameSessionTests）
- 修改：Scripts/Engine/UciEngineProcess.cs（新增 Disconnected 事件）、Scripts/UI/BoardInput.cs（InputEnabled 门闸）、Scripts/Main.cs（GameSession 装配 + HUD 接线 + 退出释放）、Scenes/Main.tscn（Hud 节点）、superchess.sln（加入 Game.Tests）

## 关键决策

- UciSession 解析移植 ComputerPlayer.parseInfoCmd：仅消费 depth/multipv/score/pv，未知关键字只跳过自身不跳过值；info 分值为走子方视角（M5 显示时换边）
- IUciSession 接口抽象使 GameSession 可测：FakeUciSession 的 GoAsync 挂起等待 Release，能确定性复现「引擎思考中」窗口
- 引擎进程退出（stdout 管道关闭）故障化挂起的 bestmove TCS，避免调用方永久挂起；stop 后引擎仍回 bestmove，不视为错误（沿用 PoC 结论）
- Busy 门闸下引擎代落子不走 TryPlayMove（其检查 Busy），改由内部 ApplyMoveCore 承担校验+落子+事件
- 新对局/悔棋引发的过期 bestmove 用代际计数（_searchGeneration）丢弃，替代参考项目 8 态状态机
- 引擎生命周期归 UI 层（Main）：首次切人机模式时 StartAsync（15s 超时、失败提示并可回双人模式），退出时 Dispose（quit + kill）；EngineLocator 桌面端返回 null → 引擎不可用，双人模式不受影响
- 强度设置变更实时经 ApplyOptionsAsync 下发（setoption + readyok 同步）；限棋力开启才携带 UCI_Elo
- HUD 中文文本依赖 Godot 系统字体回退（桌面/Android 均有 CJK 系统字体），如真机出现方块则后续内置字体

## 测试结果

- `make check`（dotnet format --verify-no-changes + dotnet build + dotnet test）：通过
- Core.Tests 44 例 + Game.Tests 10 例全绿（人机完整回合、思考中走子/悔棋/提示/切模式被拒、思考中新对局取消搜索并丢弃迟到 bestmove、连退两步/单步、将死后拦截、LoadFen 非法保持局面、无引擎双人可用）
- headless 冒烟：主场景 30 帧无报错；HUD 选项收集 + 双人两回合走子（炮二平五/黑马应着）经 GameSession 链路全过（临时脚本验证后已删除）

## 遗留事项

- UciSession 对真实 Pikafish 的端到端验证待真机（桌面无 arm64 引擎二进制），M6 真机回归覆盖 AC-3
- HUD 中文若在真机显示方块，需内置 CJK 字体（M6 打磨）
- M0-5 真机空 APK 安装、M1-7 真机引擎 PoC 仍待设备连接

## 下一轮计划

- M5 分析模式：AnalyzeAsync（MultiPV 3~5）、ArrowLayer（历史箭头 + 建议箭头 + digit 角标）、评估显示
