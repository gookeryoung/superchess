# 打谱模式与残局深化设计（REQ-02）

生效范围：打谱（XQF/PGN 解析 + 浏览交互）与残局深化（题库/多正解/进度/分级）。
实施合同：`.claude/artifacts/plans/manual-browse-puzzle-deepening.md`。

## 1. 架构总则

- 打谱为第 4 种练习模式：纯 C# 控制器观察 GameSession（双人模式）+ Main 路由，GameSession 零改动。
- 解析器、控制器、进度模型全部纯 C# 零 Godot 依赖，测试工程链接源码编译（同 Game.Tests 模式）。
- 树结构对齐不共享类型：打谱 `ManualDocument.MoveNode` 与残局 `PuzzleMoveNode` 结构同构但语义独立。

## 2. XQF/PGN 解析器（Scripts/Manual/）

- [x] `XqfKey.cs`：解密 key 结构与计算（keyXY/keyXYf/keyXYt/keyRMKSize/fKeyBytes/f32Keys）。
- [x] `XqfReader.cs`：二进制读取器（`ReadInt32()` LE、`ReadString(int size)` GB18030 解码，字符串统一 Trim）。
- [x] `ManualDocument.cs`：`MoveNode`（Move/Children/Parent）+ 元数据 + `ValidateAllMoves()`（复用 Core.Rule 逐节点校验合法性）；`ManualFormatException` 为解析失败统一异常。
- [x] `XqfParser.cs`：头部定长字段直接偏移读取（无通用二进制 DSL）；magic 校验失败抛 `ManualFormatException`；piecePos 解密（版本 ≥12 换序）；0x400 起递归读着法（hasNextStep/hasVarStep 变着递归，注释长度 keyRMKSize 修正）；坐标转换 value=X*10+Y 左下原点 → `Position(x, 9-y)`。
- [x] `PgnParser.cs`：PGN(ICCS) 文本解析（[Tag] / FEN / 着法序列过滤回合号），ICCS 坐标与 UCCI 同构直接走 `Move.FromUcci`；中文记谱着法（非 4 字符坐标）抛 `ManualFormatException`，消息固定含「暂不支持中文记谱 PGN」。
- [x] 变着语义：记录 flag 的 var 位表示「该记录的替代着」，变着节点挂在其父节点下作兄弟分支；var 标志可能指向文件尾耗尽（无实际变着，children 空为正常）。

## 3. 打谱控制器与交互（Scripts/Manual/ManualController.cs + Main）

- [x] `ManualController`（纯 C# 树游标）：
  - `Open(ManualDocument)` / `Clear()`；`PeekDefaultForward()` 返回默认主线下一着；`FindBranch(Move)` 按落点查分支；`AdvanceTo(MoveNode)` 推进游标；`MoveBack()` 单步后退（返回是否成功）；`Rewind()` 回开局；`FenAt(MoveNode)` 内部 Board 模拟计算任意节点目标 FEN。
  - 跳转语义：回开局与分支跳转 = 控制器内部 Board 逐步模拟目标局面 → Main 调 `LoadFen(目标FEN)` 一次性落地；单步前进 = `TryPlayMove`、单步后退 = `Undo`（保留动画音效）。
- [x] Main 路由：打谱分支在 OnMoveChosen（点击落点=选择分支或主线前进）/ OnUndo / OnNewGame 中与教学/残局并列；`IsPracticing` 含三种练习模式。
- [x] 分支选择：多分支节点处分支着法经 `ArrowLayer.ShowSuggestions` 展示 + 状态栏提示「点击落点选择分支」，BoardInput 落点命中即选中。
- [x] 文件来源：桌面 FileDialog 完整功能；Android 无 native picker 验证通过前以「内置示例棋谱」菜单提供（`assets/manuals/` 打包 XQF，复用 BuildPickerDialog 弹窗）。
- [x] HUD：打谱入口按钮 + 打谱导航行（前进/后退/回开局），打谱模式下禁用互斥按钮（提示/分析/人机）。

## 4. 残局深化（Scripts/Game/ + build/gen_puzzles.py）

### 4.1 数据模型

- [x] `PuzzleMoveNode`：Ucci（根为空串）/ Parent / Children / IsRoot / Depth（根 0，每着 +1）/ AddChild。深度约定：奇数深度=用户着（红），偶数深度=防守着（黑）；用户着层可并列多正解，防守着层恒唯一。
- [x] `PuzzleDefinition(Id, Title, Description, Difficulty, Fen, Root)`：Root 为多正解树。
- [x] `PuzzleLibrary`：`All` 按难度升序；树 DSL `R(children...)` 根节点 / `N(ucci, children...)` 着法节点，由 gen_puzzles.py 全量再生，手工编辑无效。

### 4.2 控制器（PuzzleController）

- [x] `Evaluate(Move)`：与当前节点任一并列正解分支按起终点匹配即接受；命中后游标一次越过用户着与防守着两步（防守着棋盘应用由 Main 的 `PlayDefenseAsync` 延迟 0.6s 执行，树匹配不依赖棋盘）；无防守着即过关。拒绝时局面不变。
- [x] `UserMoveCount = cursor.Depth / 2`（游标在防守着层时计已完成用户着数）；`TotalUserMoves` 沿主线数用户着；`IsSolved` = 主线末位用户着已被接受；`Start/Reset/Clear`。
- [x] 树着法 UCCI 非法时 Evaluate 返回未命中（不抛异常）；树合法性由题库完整性测试保证。

### 4.3 进度持久化（PuzzleProgress）

- [x] 纯 C# 模型：按题目 Id 记录 Solved/Attempts/LastDate；`MarkAttempt`（打开题目 +1）/ `MarkSolved`（幂等，重复过关仅刷新日期）/ `IsSolved` / `Attempts` / `ToJson` / `FromJson`。
- [x] Main 层 user:// JSON 读写（Godot FileAccess；Android 为应用私有目录）；过关时写入。

### 4.4 选题弹窗

- [x] 按难度分组（一步杀/两步杀/三步杀/进阶），条目标注过关标记与挑战次数；每次弹出按当前进度刷新（Window `VisibilityChanged` 信号，弹出时 Visible 才刷新）。

### 4.5 题库生成管线（build/gen_puzzles.py，不入库）

- [x] 候选策略：T3 已验证 8 题局面为母本 + 惰性子变体（黑象/黑卒/红相，加子不挡杀线）+ 左右镜像（col→8-col，杀局同构），FEN 去重。
- [x] pyffish 预检 `position_ok`：行棋方与对方均不得被将（含对将），非法局面引擎会拒载（stdout EOF，后续管道写报 OSError）。
- [x] 树生成 `build_tree`（depth16 MultiPV=8，红手层并列最优收 ≤3 分支，黑手层 bestmove 单线）：
  - `expected` 逐层校验：红手层 n 着杀 → 黑手层应为 -(n-1)，黑应后红手层应为 n-1；分数不符即波动/非并列，整支淘汰。
  - 终着层仅收 pyffish 真将死着（等将/困毙淘汰）。
- [x] `--reconcile` 自洽循环（≤3 轮）：verify 全量对拍（MultiPV=8，波动时提池 16 复核）→ 失败分支从树剪除（防守层无子整支淘汰）→ 重写 puzzle_lines.json 与 PuzzleLibrary.cs → 复验，直至全绿；整题失效剔除。
- [x] 题量门禁：≥30 题（当前 50 题， difficulty 1-4 分级）。
- [x] 走子方视角：mate 分数从行棋方视角（正=行棋方杀）；pyffish 记谱 rank = UCCI rank + 1。

## 5. 依赖与测试

- [x] `System.Text.Encoding.CodePages`（GB18030，微软官方纯托管包，AOT 安全）——用户已授权（2026-10-03）。
- [x] tests/Manual.Tests：真实样例（含 v16 加密 135 节点样例）+ 解析器/控制器单测（32 例）。
- [x] tests/Game.Tests：PuzzleController 树推进（并列正解接受/非最优拒绝/过关/重置）、PuzzleProgress 读写、题库完整性（≥30 题全量回放 + 树递归）共 43 例。
- [x] 引擎二进制不入库：engines/windows/ 从 Pikafish release（2026-09-06）手动放置（.exe + .nnue）。

## 6. 真机走查清单（待设备，与 M6-29/30 合并）

- [ ] 打谱：内置棋谱打开 → 前进/后退/回开局 → 分支箭头与点击切换 → FileDialogShow（Android native picker PoC 若验证）
- [ ] 残局：选题弹窗分组与过关标记 → 多正解并列着法接受 → 进度重启保留 → 防守着延迟落子节奏
