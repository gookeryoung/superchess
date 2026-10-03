# iter-14 打谱模式与残局深化（二期）

## 需求清单

req-02（AC-P1..P7）：打谱模式（XQF/PGN 解析、浏览、变着分支）+ 残局深化（题库扩容、多正解、进度持久化、分级弹窗）；AC-P7 真机走查并入 M6-29/30 待设备。

## 迭代目标

按实施合同 `.claude/artifacts/plans/manual-browse-puzzle-deepening.md`（APPROVED）交付二期全部软件层功能，分支 `feat/phase2-manual`。

## 改动文件

- P1 解析器（纯 C# 零 Godot）：`Scripts/Manual/XqfKey.cs`、`XqfReader.cs`（GB18030）、`ManualDocument.cs`（MoveNode 树 + ValidateAllMoves）、`XqfParser.cs`（头部定长偏移 + piecePos 解密 + 变着递归）、`PgnParser.cs`（ICCS 正例 + 中文记谱明确拒绝）；`superchess.csproj` 引入 System.Text.Encoding.CodePages
- P2 打谱接入：`Scripts/Manual/ManualDocument.cs` 协作、`Scripts/Game/ManualController.cs`（树游标 + 内部 Board 模拟跳转 FEN）、`Scripts/UI/HudPanel.cs`（打谱入口 + 导航行 + SetManualMode）、`Scripts/Main.cs`（四态模式路由小节 + 内置棋谱弹窗 + 桌面 FileDialog）、`assets/manuals/` 3 个内置示例棋谱
- P3 残局深化：`Scripts/Game/PuzzleTypes.cs`（主线扩展为 PuzzleMoveNode 树）、`PuzzleController.cs`（树推进：并列正解任一命中即接受，Evaluate 一次推进两步越过防守着）、`PuzzleLibrary.cs`（脚本再生，50 题）、`PuzzleProgress.cs`（纯 C# 进度模型）、`Scripts/Main.cs`（user:// 读写 + 选题弹窗按难度分组 + 过关标记/挑战次数）
- 测试：`tests/Manual.Tests/`（新工程 32 例：真实样例解析/变着/加密版本/PGN 正反例/ManualController 游标）、`tests/Game.Tests/` 追加至 43 例（PuzzleController 树推进 9 例、PuzzleProgress 7 例、题库完整性含 ≥30 断言 + 递归回放）
- 文档：`.trae/req/req-02-打谱与残局深化.md`、`.trae/designs/manual-browse-puzzle.md`（生效设计）；本迭代记录；按「保留最新 5 条」删除 iter-09

## 关键决策

- 打谱为第 4 种练习模式：ManualController 观察者游标（纯 C#）+ GameSession 双人模式零改动——前进=TryPlayMove、后退=Undo、回开局/跳转=控制器内部 Board 模拟目标 FEN + LoadFen 一次性落地（规避 Undo 事件风暴与重放动画风暴）
- XQF 头部为定长字段直接偏移读取（不引入 JBBP 类等价物）；GB18030 经 CodePages 微软官方纯托管包（已授权）
- Android 打谱入口降级为内置示例棋谱菜单（assets/manuals/），桌面保留完整 FileDialog；能力分级而非功能缺失
- 残局题库生成管线（`build/gen_puzzles.py`，gitignore 不入库）：T3 已验证 8 题局面为母本 → 惰性子变体（黑象/黑卒/红相）+ 左右镜像展开 64 候选 → pyffish position_ok 预检 → Pikafish depth16 MultiPV=8 收集并列最优多正解树 → pyffish 真将死过滤（困毙杀淘汰）→ expected 逐层校验拦截生成期波动 → reconcile 剪枝自洽循环
- **搜索确定性化（本轮关键）**：Pikafish 改单线程 + 每次搜索前 Clear Hash——4 线程 + TT 残留导致同局面两次搜索结果不同（MultiPV 并列 mate 排名横跳），是此前 verify 反复失败的根因；确定性下对拍可复现，reconcile 第 1 轮剪 1 分支、第 2 轮全绿收敛
- 多正解树结构与 ManualDocument.MoveNode 对齐但不共享类型（语义不同）；PGN 仅支持 ICCS 坐标记谱，中文记谱明确报「暂不支持」

## 测试结果

- `make check` 全绿：format + build（0 警告 0 错误）+ test 119 例（Core 44 + Manual 32 + Game 43）
- `gen_puzzles.py --verify`（确定性引擎）：50/50 题对拍全绿（mate 并列最优 + 终着真将死）
- headless 冒烟：打谱流程（XQF 打开 → 前进 → 变着切换 → 局面一致）通过

## 遗留事项

- AC-P7 真机走查清单（设计文件第 6 章）：打谱 XQF 打开/浏览/变着 + 残局分级/进度/多正解，与 M6-29/30、M0-5、M1-7 合并执行；待用户连接设备
- Android native 文件选择器（DisplayServer.FileDialogShow）未验证，PoC 结论维持降级方案

## 下一轮计划

设备连接后执行真机回归（MVP AC-1..AC-8 + 教学/残局 + 打谱 + 残局新功能逐条走查）。
