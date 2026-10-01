# iter-05 M3 棋盘 UI 与交互

## 需求清单

- M3：资产导入、Board.tscn + BoardView 渲染、BoardInput 两段式选子落子、音效、双人本地对弈可玩（设计 `.trae/designs/godot-xiangqi-android.md` M3 节，AC-6 的 UI 部分）

## 迭代目标

- `assets/`：棋盘/14 棋子/标记/音效从参考项目复制并完成 Godot 导入
- `Scripts/UI/`：BoardView（渲染 + Tween 动画）、BoardInput（点击 → 格坐标 + 选子状态机）、SoundPlayer（6 音效）
- `Scenes/Board.tscn` + Main 装配：双人本地对弈回合流转

## 改动文件

- 新增：assets/board/chessboard.png、assets/pieces/r_*.png b_*.png（14 张）、assets/markers/（r_box/b_box/redpot/blackpot/digit1-5）、assets/sounds/（select.wav、move/capture/check/invalid.mp3、checkmate.ogg）
- 新增：Scripts/UI/BoardView.cs、BoardInput.cs、SoundPlayer.cs、Scenes/Board.tscn
- 修改：Scenes/Main.tscn（实例化 Board）、Scripts/Main.cs（装配 + 走子流转，移除 EnginePoc 调用）、superchess.csproj（DefaultItemExcludes 排除 tests/**）、Makefile（check 增加 dotnet build）

## 关键决策

- 坐标系沿用参考项目：底图 1240x1340，格距 136，棋子 110，交叉点中心 (77+136x, 60+136y)，偏移 (22,5)；BoardView 以原生坐标渲染，Main 按 1080/1240 等比缩放
- checkmate.m4a 为 AAC（Godot 不支持），经 ffmpeg 转 checkmate.ogg 后删除原文件
- 分层：BoardInput 只负责「点击→格坐标→两段式状态机」并发 MoveChosen 信号；Main 持有 Board 执行回合流转与终局判定；选子/非法音效在 BoardInput，走子/吃子/将军/将死音效在 Main
- OnMoveChosen 校验走子方归属（冒烟测试发现直接发信号可绕过 BoardInput 的回合检查，防御性补齐）+ Rule.IsLegalMove 兜底
- 动画期间 IsAnimating 挂起输入；AnimateMove 同步维护精灵表，无全量重渲
- **门禁修复**：Godot.NET.Sdk 不排除 tests/**（主工程误编测试源码），且 dotnet test 仅构建测试工程导致主工程编译错误从未被 make check 捕获——csproj 增加 `DefaultItemExcludes;tests/**`，Makefile check 在 test 前加 `dotnet build`
- EnginePoc.Run() 从 Main._Ready 移除（M3 起进入对弈流程，PoC 文件保留待 M4 正式 UCI 实现替代）

## 测试结果

- `make check`（dotnet format --verify-no-changes + dotnet build + dotnet test）：通过
- 44 个 Core 用例全绿
- headless 冒烟（临时 SceneTree 脚本，验证后已删除）：主场景实例化、炮二平五（翻边/落点/起点清空）、非走子方走法拦截（局面不变）、黑方跳马应手、SMOKE OK
- Godot CLI `--headless --import` 资产导入正常；主场景 30 帧运行无报错

## 遗留事项

- 桌面可视化双人交互（选中框/落点提示/动画/音效的实际观感）待用户运行确认
- M0-5 真机空 APK 安装、M1-7 真机引擎 PoC 仍待设备连接（不阻塞 M4）

## 下一轮计划

- M4 对弈模式：UciSession 完整实现、GameSession 人机对弈循环（忙闲门闸/Undo/Hint）、强度设置 UI、并发场景测试
