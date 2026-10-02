# iter-10 入门指导与残局练习 T1（控制器与数据模型）

## 需求清单

- `.trae/req/req-01-入门指导与残局练习.md`（REQ-1..7）
- 设计：`.trae/designs/tutorial-endgame.md`

## 迭代目标

T1：教学/残局控制器（纯 C#）+ 数据模型 + 单测 + 题库完整性回放测试。

## 改动文件

- 新增 `Scripts/Game/LessonTypes.cs`：LessonGoalKind/LessonGoal/LessonDefinition/LessonMoveResult
- 新增 `Scripts/Game/LessonController.cs`：五类目标先行校验（AnyMoveOfPiece/ExactMove/AnyCheckingMove/AnyMateMove/AnyCaptureMove），将军/将死判定模拟落子后复用 Rule
- 新增 `Scripts/Game/PuzzleTypes.cs`：PuzzleDefinition/PuzzleMoveResult
- 新增 `Scripts/Game/PuzzleController.cs`：主线比对推进、防守着提取、IsSolved、Reset/Clear
- 新增 `Scripts/Game/LessonLibrary.cs`：课程库骨架（样例课「马走日」）
- 新增 `Scripts/Game/PuzzleLibrary.cs`：残局库骨架（样例题「一步杀·重炮」）
- 新增 `tests/Game.Tests/LessonControllerTests.cs`（9 例）、`PuzzleControllerTests.cs`（7 例）、`LibraryIntegrityTests.cs`（2 例）
- `Scripts/Main.cs`：dotnet format 统一 tab→空格缩进（无语义变化）

## 关键决策

- 走子方将军判定方向：DoMove 翻边后 `board.RedToMove` 即应将方，AnyCheckingMove/AnyMateMove 谓词以此为判定对象
- PuzzleController.Evaluate 接受时立即推进进度（题库完整性测试保证主线着法合法，落子失败不会发生），无需显式 Advance
- integrity 断言收紧为「主线回放全部合法 + 末位用户着达成将死」，不强制中间用户着均为将军着（T3 多步杀不要求连将）
- 残局样例从「闷宫」改为「重炮杀」（车吃炮成杀：将吃车遭后炮隔架攻击、士垫 (4,1) 反成炮架仍被将军），结构经完整性测试验证
- FEN 段序与坐标陷阱：第 n 段对应 y=n-1；UCCI 行号 = 9-y。样例数据两处踩坑均由完整性测试捕获后修正

## 测试结果

- `make check` 全绿：format 校验通过、build 通过、Core.Tests 44 例 + Game.Tests 30 例（新增 18 例）全过

## 遗留事项

- LessonLibrary/PuzzleLibrary 仅含样例数据，T3 扩充至 ≥8 课/≥8 题
- T2：HudPanel 按钮 + 选题弹窗 + Main 装配路由 + 防守着延迟落子

## 下一轮计划

T2 UI 接入（设计步骤 5-6）；T3 内容制作与真机回归（步骤 7-10，并入 M6-29 待设备）。
