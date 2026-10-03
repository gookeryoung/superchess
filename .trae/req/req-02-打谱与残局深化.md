# REQ-02 打谱模式与残局深化

来源：用户请求（2026-10-03 优先级确认：打谱 > 残局深化）；实施合同 `.claude/artifacts/plans/manual-browse-puzzle-deepening.md`（APPROVED）。分支 `feat/phase2-manual`。

## AC 勾选

- [x] AC-P1 XQF 解析正确性：≥3 个真实 XQF 样例（含高版本加密与变着）解析单测全绿——元数据/初始 FEN/主线序列/分支结构逐项断言
- [x] AC-P2 打谱完整流程：XQF 打开 → 前进 → 后退 → 回开局 → 分支箭头 + 点击落点切换（headless 冒烟 + 手工走查）
- [x] AC-P3 PGN(ICCS) 解析正例全绿；中文记谱 PGN 明确报「暂不支持中文记谱 PGN」
- [x] AC-P4 残局题库扩容至 ≥30 题（实际 48 题），全量通过完整性测试 + 生成对拍（`gen_puzzles.py --verify` 全 OK）；选题弹窗按难度分组
- [x] AC-P5 进度持久化：`user://` JSON 读写（PuzzleProgress 纯 C# 模型 + Main 层 FileAccess），单测覆盖
- [x] AC-P6 多正解：并列最优着法被接受（各接各自防守线），偏离主线非最优着被拒绝，单测覆盖两类路径
- [ ] AC-P7 工程门禁：`make check` 全绿（已过）；真机走查清单逐条输出——**待设备连接**（与 M6-29/30 合并）

## 范围外（Follow-ups）

PGN 中文记谱逆解析、云开局库、局面编辑器、评估趋势折线图、Android SAF 深度文件方案。
