# iter-11 入门指导与残局练习 T2（UI 接入）

## 需求清单

- `.trae/req/req-01-入门指导与残局练习.md`（REQ-3/4/5 本轮落地）
- 设计：`.trae/designs/tutorial-endgame.md`

## 迭代目标

T2：HudPanel 教学/残局入口 + Main 选题弹窗与练习模式全流程装配。

## 改动文件

- `Scripts/UI/HudPanel.cs`：新增练习行（教学/残局各半宽按钮）、LessonRequested/PuzzleRequested 事件、SetPracticeMode（练习中禁用切人机/提示/分析）
- `Scripts/Main.cs`：BuildPickerDialog 通用选择弹窗、LoadLesson/LoadPuzzle（双人模式 + LoadFen + 控制器启动 + 练习联动）、OnMoveChosen 控制器先行路由、HandleLessonMove/HandlePuzzleMove、PlayDefenseAsync（0.6s 延迟 + 输入锁定）、OnNewGame 练习中=退出、OnUndo 练习中=重玩

## 关键决策

- 防守着延迟落子用 SceneTreeTimer + ToSignal（SignalAwaiter 不支持 ConfigureAwait，在主线程恢复）；期间 `_boardInput.InputEnabled=false` 防止用户执黑乱走
- ItemList.ItemSelected 信号参数为 long，需显式转 int
- 退出练习经 NewGame 复位并调 SetMode 恢复按钮（SetPracticeMode(false) 不自行恢复 hint/analyze，由 SetMode 统一管理）
- 换题（练习中再开弹窗选题）只清控制器重新载入，不复位局面之外的状态

## 测试结果

- `make check` 全绿：format 校验、build 通过、Core.Tests 44 例 + Game.Tests 30 例全过
- 桌面真机走查待 T3 内容齐备后一并执行（样例课程/残局可先行人工验证）

## 遗留事项

- T3：课程/残局内容扩充（≥8 课/≥8 题）+ Pikafish 对拍正解 + 真机回归（并入 M6-29 待设备）

## 下一轮计划

T3 内容制作与收尾（设计步骤 7-10）。
