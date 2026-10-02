# iter-12 教学残局 T3 内容制作

## 需求清单

req-01 REQ-1..7（本轮聚焦内容项：REQ-1 课程数据、REQ-2 题库数据、REQ-6/7 验证门禁）。

## 迭代目标

T3：课程扩至 10 课、残局题库扩至 8 题、全部正解经桌面 Pikafish 对拍验证，make check 收尾。

## 改动文件

- `Scripts/Game/LessonLibrary.cs`：扩至 10 课（七兵种走法 + 将军 + 将死 + 吃子炮打）；修正 7 课 FEN 黑将行 "3k4"→"3k5"（8 列不合法）
- `Scripts/Game/PuzzleLibrary.cs`：8 题全量替换——一步杀×3（重炮 e7e8 / 卧槽马 e5d7 / 闷宫 c5d7）+ 两步杀×3（双车错 a1e1-e9f9-i4f4 / 马炮 i4g5-e9e8-g5e6 / 双车胁士 i4e4-d9e8-a1a9）+ 三步杀×1（马炮 i3h5-e9f9-h5f6-f9f8-e1f1）+ 进阶四步杀×1（车炮破双士 a1e1-f9e8-d0e0-e9f9-e1f1-e8f7-f1f7）
- `build/gen_puzzles.py`（不入库）：Pikafish depth 16 逐层 bestmove 主线生成 + pyffish 真将死过滤 + `--verify` 对拍
- `.trae/designs/tutorial-endgame.md`：步骤 7/8/10 勾选、风险表困毙语义修正、REQ-7 记录更新
- `.trae/req/req-01-*.md`：REQ-1..7 全部勾选

## 关键决策

- 删除铁门栓一步杀：单炮隔车打将被士垫解（垫子变炮架后第一子非将），一步杀不成立，integrity 测试拦截。
- 题量分布由设计的「一步×4 + 两步×3 + 三步×1」调整为「一步×3 + 两步×3 + 三步×1 + 四步×1」：光将类一手杀局面黑方近乎困毙，任意等着成杀导致正解不唯一（引擎 MultiPV 前 5 全为等将着）；给黑方加闲兵（卒）后一步杀正解唯一。四步杀取车炮对双士「王位线杀」经典局，难度递进且杀法模式不重复。
- 主线一律由引擎逐层 bestmove 生成并经 pyffish 复核「终着将军且无合法着」：Rule.IsCheckmate 不含困毙语义，光将局面大量「困毙杀」（无将胜）被自动淘汰（车炮杀/车马杀初版主线均因此废弃重造）。
- pyffish xiangqi 记谱 rank = UCCI rank + 1（炮二平五 = h3e3），转换后做终局校验。

## 测试结果

- `build/gen_puzzles.py --verify`：8/8 OK（红手各步 MultiPV mate 分数并列最优 + 终局真将死）
- `dotnet test --filter LibraryIntegrity`：课程 FEN 合法且目标可达、8 题主线回放终局将死，全绿
- `make check`：format + build + test 全绿（Core.Tests 44 例 + Game.Tests 30 例）

## 遗留事项

- 真机走查（课程/残局全流程 + AC-1..8）并入 M6-29，待设备连接。
- Pikafish 在部分稀子局面（车对双士、叠马炮等）搜索中进程崩溃，脚本已按候选独立启动引擎规避；与本项目代码无关。

## 下一轮计划

M6 真机收尾：release 签名导出 + adb 安装 + 真机回归（待用户连接设备）。
