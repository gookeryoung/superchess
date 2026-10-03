# 打谱模式与残局深化（二期）Implementation Plan

> Status: APPROVED（REVISE 后二轮通过）
> Source: 用户请求（2026-10-03 优先级确认：打谱 > 残局深化）+ `.trae/designs/godot-xiangqi-android.md` ADR Follow-ups + 参考项目 F:\StudyCodes\chinese-chess-fish-android
> Mode: default（Planner → Architect → Critic 共识循环）
> Iterations: 2 / 3
> Author: 用户
> Last updated: 2026-10-03

## Requirements summary

MVP（M0-M6）与教学/残局（T1-T3）软件层已完成，进入二期。本计划覆盖两个已确认优先级的功能：① 打谱模式——打开 XQF/PGN(ICCS) 棋谱文件，前进/后退/回开局浏览，变着分支选择；② 残局深化——题库扩容、难度分级、进度持久化、多正解支持。真机走查（M6-29/30、T3-9）为外部依赖项，并入本计划收尾阶段统一执行。

## Acceptance criteria

- AC-P1 XQF 解析正确性：参考项目棋谱目录 ≥3 个真实 XQF 样例（须含高版本加密文件与含变着文件）解析单测全绿：元数据、初始局面 FEN、主线着法序列、变着分支数量与结构逐项断言
- AC-P2 打谱完整流程：桌面打开 XQF → 逐步前进至终局 → 单步后退 → 回开局 → 多分支节点处分支着法以箭头展示且点击落点完成分支切换；局面、历史记谱、音效全程正确（headless 冒烟 + 手工走查记录）
- AC-P3 PGN(ICCS)：ICCS 坐标格式 PGN 解析单测全绿（[Tag]/FEN/着法序列）；中文记谱 PGN 明确报「不支持」错误信息而非解析错乱
- AC-P4 残局题库：扩容至 ≥30 题且全部通过完整性测试（FEN 合法 + 主线回放合法 + 末位用户着将死 + 引擎对拍并列最优）；选题弹窗按难度分组展示
- AC-P5 进度持久化：完成/重玩状态写入 `user://` 存储，重启后保留；纯 C# 进度模型有单测
- AC-P6 多正解：并列最优着法（题库生成时对拍收录）不被判错；偏离主线非最优着仍被拒绝；单测覆盖两类路径
- AC-P7 工程门禁：`make check` 全绿；真机走查清单逐条输出（待设备连接）

## RALPLAN-DR

### Principles

- 复用优先：GameSession 零改动（LoadFen/TryPlayMove/Undo 原样使用），延续教学/残局已验证的「观察者控制器 + Main 路由」架构
- 纯 C# 可测：XQF/PGN 解析器与打谱控制器零 Godot 依赖，真实样例作测试夹具
- 外科手术式改动：不扩 GameMode 枚举、不动 Core 层；Main 只增路由分支与打谱装配
- 风险前置：Android 文件选择能力 PoC 先行（Go/No-Go 决策点，同 M1 模式）
- 内容质量门禁：残局扩容必须走 gen_puzzles.py 生成 + pyffish 复核既有管线，不手写题库

### Decision drivers

1. 用户优先级：打谱 > 残局深化（2026-10-03 确认）
2. 架构协同：打谱先落地 MoveNode 树基建，残局多正解结构对齐复用经验
3. 测试夹具可得性：参考项目 `棋谱/` 目录有真实 XQF 样例（含加密与变着）
4. 单人项目：开发速度与可维护性并重

### Viable options

**Option A: 打谱作为第 4 种练习模式（favored，强化版）**
- 实现思路：ManualController 观察者游标控制器（纯 C#，内部用 Board 模拟计算任意跳转目标局面）；GameSession 双人模式仅承担状态落地——前进=TryPlayMove（动画/音效/记谱自然触发），后退=Undo，回开局/任意跳转=内部模拟目标 FEN+LoadFen 一次性到位；多分支时分支着法经 ArrowLayer.ShowSuggestions 显示 + 点击落点选分支
- 改动文件：`Scripts/Manual/`（新建 5 文件）、`Scripts/Game/ManualController.cs`、`Scripts/Main.cs`、`Scripts/UI/HudPanel.cs`、`tests/Manual.Tests/`（新建）
- Pros: 与教学/残局完全同构，Main 已有 IsPracticing 路由模式；GameSession 零改动；全逻辑纯 C# 可单测
- Cons: Main.cs 从 553 行继续增长；打谱走子语义（选分支）与其他模式（校验走子）不同，路由可读性靠注释与命名维护

**Option B: 独立打谱场景（Manual.tscn 场景切换）**
- invalidated：场景切换丢失对局状态——M6 阶段已把 About 从独立场景改为弹窗验证过此反模式；且需重建 HUD/棋盘装配，重复 400+ 行
- （吸收项：B 的隔离诉求转化为 Main 内提取统一模式路由小节 + 控制器全逻辑外置）

**Option C: 绕过 GameSession 的独立棋盘状态**
- invalidated：悔棋/历史/音效/中文记谱全部重新接线，重复实现已验证逻辑，违背最小代码原则

### 打谱状态管理子决策（Architect 综合）

跳转目标状态（回开局/任意分支跳转）计算用「控制器内部 Board 逐步模拟 → LoadFen(目标FEN) 一次性落地」，不用 GameSession 逐手 Undo×n（中途态事件风暴）也不用顺序 TryPlayMove 重放（动画风暴）；单步前进/后退仍走 TryPlayMove/Undo 保留动画音效。

## Implementation steps

### P1 XQF/PGN 解析器（纯 C# + 真实样例测试）

1. `Scripts/Manual/XqfKey.cs`：解密 key 结构与计算（keyXY/keyXYf/keyXYt/keyRMKSize/fKeyBytes/f32Keys，移植 XQFParser.initDecryptKey）— 参考 `XQFParser.java:171-218`
2. `Scripts/Manual/XqfReader.cs`：二进制读取器（ReadBytes/ReadInt32 LE/ReadString GB18030），替代参考项目 XQFBufferDecoder — 参考 `XQFBufferDecoder.java`
3. `Scripts/Manual/ManualDocument.cs`：MoveNode 树（Move/Children/Parent）+ 元数据 + ValidateAllMoves（复用 Core.Rule 走法合法性）— 参考 `XQFManual.java:51-217`
4. `Scripts/Manual/XqfParser.cs`：解析主流程——头部定偏移切片读取（替代 JBBP，头部全定长字段直接偏移）、magic 校验、piecePos 解密（版本 ≥12 换序）、0x400 起递归读着法（含 hasNextStep/hasVarStep 变着递归与注释长度 keyRMKSize 修正）— 参考 `XQFParser.java:30-338`；坐标转换 value=X*10+Y 左下原点 → `Position(x, 9-y)`（`XQFParser.java:369-376`）
5. `Scripts/Manual/PgnParser.cs`：PGN(ICCS) 文本解析（[Tag]/FEN/着法序列过滤回合号），中文记谱着法（非 4 字符坐标）报「暂不支持中文记谱 PGN」— 参考 `PGNManual.java:176-183`
6. `tests/Manual.Tests/`（新建工程，链接源码编译模式同 Game.Tests）：从参考项目 `棋谱/` 取 ≥3 个 XQF 样例入 `tests/Manual.Tests/TestData/`（含高版本加密与变着样例，文件头注释标注来源）；断言元数据/初始 FEN/主线序列/分支结构/ValidateAllMoves 通过；PGN 用例含 ICCS 正例与中文记谱拒绝例
7. GB18030 解码：引入 `System.Text.Encoding.CodePages` 包 + `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`（net8.0 默认无 GB18030）—— 已获用户授权（2026-10-03），superchess.csproj 增 PackageReference

### P2 打谱控制器与 UI 接入

8. `Scripts/Game/ManualController.cs`（纯 C# 游标控制器）：Open(ManualDocument)/Forward()/Back()/First()/CurrentBranches()；内部 Board 模拟计算跳转目标 FEN（单步走子由 Main 调 GameSession，控制器只维护树游标与目标状态）
9. 文件选择 PoC（Go/No-Go）：桌面 `FileDialog` 直接可用；Android 验证 `DisplayServer.FileDialogShow` native picker（Godot 4.4+）可用性 → 可用则走系统选择器；不可用则降级「内置示例棋谱」菜单（`assets/manuals/` 打包 2-3 个 XQF，复用 BuildPickerDialog 弹窗选择）
10. `Scripts/Main.cs`：新增 `_manualController` 字段与打谱模式路由（OnMoveChosen 打谱分支：点击落点=选择分支或主线前进；OnUndo=单步后退；OnNewGame=退出打谱）；将现有 IsPracticing 三处分支（OnMoveChosen/OnUndo/OnNewGame）整理为统一模式路由小节，防无序膨胀
11. `Scripts/UI/HudPanel.cs`：「打谱」入口按钮（练习行扩展）+ 打谱导航行（前进/后退/回开局）+ SetManualMode 禁用互斥按钮（提示/分析/切人机/悔棋语义变化提示）
12. 分支选择交互：多分支节点 Forward 暂停，分支着法经 `_arrows.ShowSuggestions` 显示 + 状态栏提示「点击落点选择分支」；BoardInput 落点命中某分支即选中（先行拦截模式，同教学/残局）
13. `tests/Manual.Tests/` 追加 ManualController 用例：前进/后退/回开局/分支选择的游标与目标 FEN 断言（零 Godot 依赖）；Main 路由回归用现有 headless 冒烟模式补 1 例（XQF 打开→前进→变着切换→局面一致）

### P3 残局深化

14. 题库扩容至 ≥30 题：`build/gen_puzzles.py`（不入库）题材扩展——重炮/卧槽马/钓鱼马/马后炮/铁门栓/双车错/侧面虎/闷宫等经典杀法题材 FEN 库 + depth16 生成 + pyffish 真将死过滤 + MultiPV 对拍管线不变 — 产出 `Scripts/Game/PuzzleLibrary.cs` 全量再生
15. 多正解：`Scripts/Game/PuzzleTypes.cs` 主线扩展为树结构（着法节点 + children 列表，结构与 ManualDocument.MoveNode 对齐但不共享类型——语义不同不强行抽象）；`Scripts/Game/PuzzleController.cs` 树推进（当前节点 children 任一命中即接受）；gen_puzzles.py 每层并列最优着法全收为分支
16. `Scripts/Game/PuzzleProgress.cs`（纯 C# 进度模型：题目完成标记/尝试计数/序列化字典）；`Scripts/Main.cs` 层 user:// JSON 读写（Godot FileAccess，Android 为 app 私有目录无权限问题）；完成过关时写入
17. 选题弹窗分级：`Scripts/Main.cs` BuildPickerDialog 复用，题目列表按一步杀/两步杀/三步杀+/进阶分组排序显示（标题前缀或分组标题）
18. `tests/Game.Tests/` 追加：多正解推进（并列最优接受/非最优拒绝）、进度模型读写与完成标记、新题库完整性（≥30 题全量回放）

### P4 收尾

19. `make check` 全绿；`make push`；`.trae/designs/` 沉淀二期设计文件（本 plan 为实施合同，生效设计按项目惯例落 `.trae/designs/manual-browse-puzzle.md`）；memory 更新
20. 真机走查清单（待设备，与 M6-29/30 合并执行）：MVP AC-1..8 + 教学/残局 + 打谱（XQF 打开/浏览/变着）+ 残局新功能（分级/进度/多正解）

## Workspace setup

- 实施前运行 `git status --short` 与 `git branch --show-current`；当前分支 main（feat/godot-mvp 已合并删除，2026-10-03 确认）
- 工作区若不干净，先保护现有改动（M6 真机收尾可能有未提交调试产物）
- 二期功能从 main 新建分支 `feat/phase2-manual`（沿用 feat/ 分支惯例）

## Risks & mitigations

| Risk | Mitigation |
|---|---|
| XQF 高版本解密移植错误（key 算法/换序/坐标修正） | 真实样例对拍：参考项目 Python XQFParser（`棋谱/read_xqf.py`）作交叉验证 oracle；对拍方式与输出物在 P1 完成标准中注明（JSON dump 对比或人工核验记录，二选一明确记录） |
| Android 文件选择不可用（FileDialogShow 行为未知） | P2 第 9 步 PoC 前置 Go/No-Go；降级方案具体化：Android 打谱入口=内置示例棋谱菜单（`assets/manuals/` 打包 2-3 个 XQF），桌面 FileDialog 完整功能——能力分级而非功能缺失 |
| GB18030 解码在 Godot Android AOT 下的兼容性 | 依赖已授权（2026-10-03）；CodePages 为微软官方纯托管包，AOT 安全；P1 首个测试即验证桌面行为，Android 行为并入真机走查 |
| Main.cs 膨胀（553 → ~800 行）模式路由可读性下降 | 控制器全逻辑外置纯 C#；P2 第 10 步统一模式路由小节化（集中一处 switch 而非散落 if）；headless 冒烟回归 |
| 多正解树改造破坏题库数据结构 | PuzzleLibrary.cs 由脚本全量再生（生成管线已验证），不手写迁移；测试同步改写为树断言 |
| 变着深递归栈溢出（极端棋谱） | 保持参考项目同款递归（真实棋谱深度有限）；防御上 ValidateAllMoves 已是递归树遍历，测试样例覆盖最深可用样例 |
| 测试样例第三方版权 | 测试夹具仅取 2-3 个小样例入库并在文件头标注来源（参考项目 MIT，其棋谱样例为第三方对局数据，测试用途）；有疑虑则改用自制最小 XQF（脚本构造）——实施时二选一并记录 |

## Verification steps

- AC-P1：`dotnet test tests/Manual.Tests`（样例解析断言）全绿
- AC-P2：headless 冒烟（Godot `--headless --script` 模式，同 M3）跑打谱流程断言 + 桌面手工走查记录入 `.trae/docs/` 迭代记录
- AC-P3：`dotnet test`（PGN 正反例）全绿
- AC-P4：`dotnet test`（题库完整性 ≥30 题）全绿；`build/gen_puzzles.py --verify` 输出 N/N OK
- AC-P5：进度模型单测 + 桌面重启手工核验 user:// 文件
- AC-P6：`dotnet test`（多正解正反例）全绿
- AC-P7：`make check` 退出码 0；真机走查清单待设备输出

## ADR

- **Decision**: 打谱作为第 4 种练习模式（Option A 强化版）：纯 C# ManualController 树游标 + GameSession 双人模式零改动复用（前进=TryPlayMove、后退=Undo、跳转=内部模拟+LoadFen）+ 分支选择复用 MultiPV 箭头交互；残局深化复用对齐 MoveNode 的树结构与既有生成管线
- **Drivers**: 用户优先级（打谱优先）起决定作用；架构协同（打谱树基建先行、残局多正解结构对齐）决定阶段顺序；GameSession 零改动原则排除 Option C
- **Alternatives considered**: Option B 独立场景 — rejected（场景切换丢对局状态，M6 已验证反模式；其隔离诉求转化为 Main 模式路由小节化被吸收）；Option C 独立棋盘状态 — rejected（重复接线已验证逻辑）
- **Why chosen**: 与已验证的教学/残局控制器架构完全同构，Main 侧增量最小；解析器与控制器全部纯 C# 可用真实样例单测；两功能共享树结构经验降低总量复杂度
- **Consequences**: Main.cs 增长 ~250 行（路由+装配），以模式路由小节化约束；引入 System.Text.Encoding.CodePages 新依赖（待授权，降级方案明确）；残局题库数据结构破坏性变更（脚本再生，无迁移成本）
- **Follow-ups**: PGN 中文记谱逆解析（需中文记谱→着法映射，复杂度高本期不做）；云开局库（chessdb.cn，需联网权限，用户未选）；局面编辑器；评估趋势折线图；Android 真机文件选择器若 PoC 失败，后续可接 SAF 深度方案

## Review trail

- Planner draft v1: Option A 练习模式复用 + P1-P4 四阶段 + 风险清单
- Architect challenge v1: steelman 独立场景方案（模式四态路由可读性）→ 结论保持 A，吸收「Main 模式路由小节化」；识别 tension：跳转状态计算（Undo×n 事件风暴 vs 重放动画风暴 vs 内部模拟+LoadFen）→ 定为内部模拟方案；树结构复用 vs 语义差异 → 对齐不共享类型
- Critic verdict v1: REVISE — ① 两处风险缓解不具体（Android 文件选择、GB18030 降级）② AC 未二值化 ③ 多正解破坏性变更无清单
- Planner draft v2: AC 全部二值化（P1-P7）；两个降级方案具体化（内置示例棋谱菜单/文本字段占位）；破坏性变更清单补全
- Critic verdict v2: APPROVED with 2 reservations — ① XQF 对拍 oracle 的输出物须在 P1 完成标准中明确记录（JSON 对比或人工核验二选一注明）② Main 四态路由触碰既有三处分支，须补 headless 冒烟回归例
- Final iterations: 2 / 3
