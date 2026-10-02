# 入门指导与残局练习设计

> Status: APPROVED（2026-10-02）
> Source: `.trae/req/req-01-入门指导与残局练习.md`
> 前置: MVP（M0-M6）已完成；本设计不改动 GameSession 既有接口

## 架构决策

教学与残局作为**观察者控制器**复用 GameSession（双人模式 + LoadFen 载入局面），不扩展 GameMode 枚举、不改 GameSession：

- 控制器为纯 C# 类（零 Godot 依赖），独立单测
- 走子校验**先行拦截**：Main 在 OnMoveChosen 中先问控制器，不匹配目标的走子直接拒绝（不进 GameSession.TryPlayMove），避免"先落子再回滚"
- 复用既有资产：BoardView/BoardInput（选子+落点提示）/SoundPlayer/HudPanel/GameSession 事件

## 接口定义

```csharp
// Scripts/Game/LessonTypes.cs
public enum LessonGoalKind
{
    AnyMoveOfPiece,   // 目标棋子任意合法走法（走法教学课）
    ExactMove,        // 指定 from→to（精确走子课）
    AnyCheckingMove,  // 任意一步造成将军的走法（将军课）
    AnyMateMove,      // 任意一步造成将死的走法（将死课）
    AnyCaptureMove,   // 任意吃掉目标棋子的走法（吃子/炮打课）
}

public sealed record LessonGoal(LessonGoalKind Kind, int PieceCode = 0, Position? From = null, Position? To = null);

public sealed record LessonDefinition(
    string Id, string Title, string Intro, string Fen, LessonGoal Goal, string SuccessText);

public sealed record LessonMoveResult(bool Accepted, string Message);

// Scripts/Game/LessonController.cs（纯 C#）
public sealed class LessonController
{
    public LessonDefinition Current { get; }
    public void Start(LessonDefinition lesson);          // 由 Main 调 GameSession.LoadFen 后调用
    public LessonMoveResult Evaluate(Board board, Move move);  // 先行校验；Accepted 才允许 TryPlayMove
    public bool ConsumeApplied();                        // MoveApplied 后调用：推进/完成判定
}
```

```csharp
// Scripts/Game/PuzzleTypes.cs
public sealed record PuzzleDefinition(
    string Id, string Title, string Description, int Difficulty,
    string Fen, string[] Mainline);   // 交错 UCCI 主线：偶数位=用户着，奇数位=防守着；末位用户着达成将死

public sealed record PuzzleMoveResult(bool Accepted, string Message, Move? DefenseReply);

// Scripts/Game/PuzzleController.cs（纯 C#）
public sealed class PuzzleController
{
    public PuzzleDefinition Current { get; }
    public int UserMoveCount { get; }        // 已走用户着数（提示进度「第 x/N 步」）
    public void Start(PuzzleDefinition puzzle);
    public PuzzleMoveResult Evaluate(Board board, Move move);  // 正确→给出防守着法；错误→拒绝
    public bool IsSolved { get; }            // 主线末位用户着已应用且终局将死
    public void Reset();                     // 重玩本题（Main 重新 LoadFen 后调用）
}
```

内容库（C# 静态数据，编译进程序集，无文件 IO）：

```csharp
// Scripts/Game/LessonLibrary.cs
public static class LessonLibrary { public static IReadOnlyList<LessonDefinition> All { get; } }

// Scripts/Game/PuzzleLibrary.cs
public static class PuzzleLibrary { public static IReadOnlyList<PuzzleDefinition> All { get; } }
```

## 数据模型

- 课程/残局均为不可变 record；局面一律以 FEN 字符串存储，Start 时经 `Board.FromFen` 解析（非法 FEN 直接抛 FenFormatException，由题库完整性测试兜底）
- 残局主线用 UCCI 字符串（`Move.FromUcci` 解析），与引擎对拍数据同构

## 算法与流程

### 教学流程

1. HUD「教学」→ 选题弹窗（AcceptDialog + ItemList，复用关于弹窗构建模式）→ Main 调 `_session.LoadFen(lesson.Fen)`（双人模式，清空历史）
2. `LessonController.Start(lesson)`；状态栏显示课程目标（Intro），BoardInput 天然只允许选走子方棋子并显示合法落点
3. 用户走子 → `Evaluate`：按 GoalKind 判定（AnyMateMove/AnyCheckingMove 需模拟落子后调 Rule.IsCheckmate/IsInCheck）→ 拒绝则 invalid 音效 + 原因文案；接受则 TryPlayMove
4. MoveApplied 后 `ConsumeApplied`：完成则 checkmate 音效 + SuccessText + 退出练习态；未完成等待继续走子（同一课程内可连续多步）

### 残局流程

1. HUD「残局」→ 题库弹窗 → LoadFen + `PuzzleController.Start`
2. 用户走子 → `Evaluate` 与主线当前用户着比对：不一致 → 拒绝 + 「此着不能达成目标，请重试」（不动局面）
3. 一致 → TryPlayMove；若为将死着 → IsSolved，过关音效 + 文案；否则 Main 经 `CreateTimer` 延迟约 0.6s 后 TryPlayMove(DefenseReply)（防守着走子方正确，双人模式无落子方限制）
4. 悔棋按钮 → LoadFen 重载本题 FEN + `Reset`；新局按钮 → 退出练习回对弈模式

### Main 装配变化

- Main 新增 `LessonController? _lesson` / `PuzzleController? _puzzle` 活动控制器字段；OnMoveChosen 路由：练习活动时先过控制器，控制器放行后才调 TryPlayMove
- HUD 新增 `LessonRequested`/`PuzzleRequested` 事件与「教学」「残局」按钮（新按钮行，两按钮各半宽）；新增 `SetPracticeMode(bool)`：练习中禁用 提示/分析/切人机 按钮，新局/悔棋文案语义由 Main 状态栏说明
- 退出练习：复用既有 OnNewGame（NewGame 复位初始局面），控制器置 null

## 异常处理

| 异常 | 处理 |
|---|---|
| 题库 FEN 非法 | 不可能到运行时——题库完整性单测在 CI 兜底；防御上 LoadFen 抛 FenFormatException 由 Main 既有 catch 显示 |
| 引擎思考中点教学/残局 | Busy 门闸复用：练习入口按钮在 Busy 时无效（与提示/分析同语义） |
| 防守着非法（题库错误） | 完整性测试保证；运行时 TryPlayMove 返回 false 仅记日志，不崩溃 |
| 练习中用户点选题弹窗再选一题 | 直接 LoadFen 换题，旧控制器替换 |

## 依赖项

- 复用：GameSession.LoadFen/TryPlayMove/NewGame（零改动）、BoardInput 落点提示、SoundPlayer、ArrowLayer 历史箭头、HudPanel 状态栏
- 新增文件：`Scripts/Game/LessonTypes.cs`、`LessonController.cs`、`LessonLibrary.cs`、`PuzzleTypes.cs`、`PuzzleController.cs`、`PuzzleLibrary.cs`；`Scripts/Main.cs`、`Scripts/UI/HudPanel.cs` 修改
- 内容制作依赖：桌面 Pikafish（engines/windows/）验证残局正解最优性；课程/残局 FEN 用 FEN 短格式（棋盘+走子方）
- 不引入：XQF 解析（保持二期）、文件 IO 题库、开局库

## 实施步骤

### T1 控制器与数据模型（纯 C# + 单测）
1. [x] `LessonTypes.cs`/`LessonController.cs`：五种 GoalKind 判定（模拟落子调 Rule）+ 完成推进 — `Scripts/Game/`
2. [x] `PuzzleTypes.cs`/`PuzzleController.cs`：主线比对/推进/防守着提取/IsSolved — `Scripts/Game/`
3. [x] tests/Game.Tests 新增：GoalKind 五类判定用例、拒绝用例、残局主线推进与完成用例、防守着正确性用例（FakeUciSession 工程追加，不依赖 Godot）
4. [x] 题库完整性测试：LessonLibrary 全部课程 FEN 可解析且目标可达；PuzzleLibrary 全部残局主线回放（交错着法逐一合法）且末位用户着达成将死

### T2 UI 接入
5. [x] HudPanel：教学/残局按钮行 + LessonRequested/PuzzleRequested 事件 + SetPracticeMode — `Scripts/UI/HudPanel.cs`
6. [x] Main：选题弹窗（ItemList 动态填充课程/题目列表）、活动控制器路由 OnMoveChosen、残局防守着延迟落子、完成/过关反馈、练习模式按钮联动 — `Scripts/Main.cs`

### T3 内容制作与收尾
7. [x] 入门课程 10 课：帅/仕/相/马/车/炮/兵走法各 1 课 + 将军 + 将死（一步杀演示）+ 吃子（炮打）课；每课 FEN + 目标 + 讲解文案 — `Scripts/Game/LessonLibrary.cs`（FEN 黑将行 9 列校验由完整性测试拦截，"3k5" 而非 "3k4"）
8. [x] 残局题库 8 题：一步杀×3（重炮/卧槽马/闷宫）+ 两步杀×3（双车错/马炮/双车胁士）+ 三步杀×1（马炮）+ 进阶四步杀×1（车炮破双士）；REQ-2「覆盖一步杀至三步杀」满足。主线由桌面 Pikafish（depth 16）逐层 bestmove 生成（`build/gen_puzzles.py`，不入库可随时重生成），经 pyffish 复核终局真将死（自动淘汰困毙线——黑方无子可动但未被将军的"杀"不收），红手各步经 MultiPV 对拍并列最优（REQ-7）— `Scripts/Game/PuzzleLibrary.cs`
9. [ ] 真机回归：AC-1..AC-8 复验 + 新增教学/残局真机走查（并入 M6-29，待设备连接）
10. [x] `make check` 全绿收尾，同步 `.trae/docs/` 迭代记录与本设计文件勾选

## 验收标准（映射 REQ）

- REQ-1/2/5：桌面手工走查每课/每题完整流程（正确推进、错误拒绝、完成反馈）
- REQ-3/4：UI 手工走查（入口、练习中按钮禁用、悔棋重玩、新局退出）
- REQ-6：`dotnet test` 含题库完整性测试全绿；`make check` 退出码 0
- REQ-7：残局正解对拍记录（引擎 depth 16 与主线着法一致或分值并列最优；`build/gen_puzzles.py --verify` 输出 8/8 OK）

## 风险与缓解

| Risk | Mitigation |
|---|---|
| 残局主线唯一性（用户走出主线外但同样制胜的着法被判错） | 提示文案引导回主线（教学工具定位）；v1 不做多正解分支，文档标注 |
| 手工 FEN 写错导致课程不可玩 | T1 题库完整性测试前置拦截（合法性 + 目标可达性） |
| 光将局面大量「困毙杀」终局（无将胜，非将死） | 主线生成脚本用 pyffish 复核终着将军且无合法着；题库完整性回放测试（Rule.IsCheckmate 不含困毙）兜底拒绝 |
| 练习与对弈状态串扰（练习中事件污染对弈 UI） | 控制器生命周期由 Main 显式管理；退出练习一律 NewGame 复位 |
