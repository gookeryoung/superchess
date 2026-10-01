# Godot 中国象棋跨平台版（Android 优先）开发计划

> Status: APPROVED
> Source: 用户需求 + F:\StudyCodes\chinese-chess-fish-android\ 项目研究
> Mode: default（Planner → Architect → Critic 共识循环）
> Iterations: 1 / 3
> Last updated: 2026-10-01

## 需求摘要

基于开源项目「象棋鱼」（chinese-chess-fish-android，MIT 许可）的可借鉴内容，用 Godot 4 .NET（C#）开发中国象棋跨平台版本，Android 优先。用途：个人训练 + 与 AI 练习。MVP 范围（标准版）：人机对弈（可调强度）、分析模式（MultiPV 评估 + 建议箭头）、FEN 导入导出、悔棋/提示、走子历史与中文纵线记谱。打谱（XQF）与开局库留二期。

## 验收标准

- AC-1 规则正确性：起始局面 perft(1)=44、perft(2)=1920、perft(3)=79666 单测通过；将军/将死/困毙/白脸将判定均有单测用例
- AC-2 Android 真机：arm64-v8a 设备安装 APK 后可完整进行一局人机对弈至终局（将死或认输）
- AC-3 引擎通信：真机 UCI 握手（uciok）、NNUE 加载、思考中可 stop 中断
- AC-4 强度分级：UCI_Elo=1280 与 3133 对同一测试局面出着差异明显，低档明显走弱
- AC-5 分析模式：任一己方回合可触发分析，显示 ≥3 条 MultiPV 建议（着法 + 分值），建议以箭头渲染在棋盘上
- AC-6 交互：选中棋子显示合法落点；走子有动画与音效；悔棋（引擎回合连退两步）；提示（引擎建议一步）；将军/将死/非法走子有对应音效与提示
- AC-7 记谱：走子历史以中文纵线着法显示（如「炮二平五」），含前/后/中兵消歧
- AC-8 FEN：复制当前局面到剪贴板 / 粘贴合法 FEN 恢复局面；非法 FEN 显示明确错误（出错字段与原因），不崩溃
- AC-9 工程门禁：`make check`（dotnet format 校验 + dotnet test）全绿；Android 导出产物可安装运行

## RALPLAN-DR

### Principles

- 最小代码：只做标准版范围，打谱/开局库/棋谱库不进入本期任何代码路径
- 分层解耦：规则核心为纯 C# 库，零 Godot 依赖，可独立单测（perft）
- 风险前置：最大不确定项（Android 上 C# 进程通信）在写任何业务代码前用 PoC 验证
- 复用优先：规则算法、UCI 状态机逻辑、图片/音效资产直接从参考项目移植或复制，不重造
- 外科手术式转型：仓库只删除 Python 模板文件，保留 .git/.trae/.github 与提交历史

### Decision drivers

1. Android 真机体验是第一优先级（用户自用训练 + AI 练习的主要场景）
2. C#（Godot .NET）技术栈已选定：System.Diagnostics.Process 提供最直接的 UCI 管道方案
3. 单人项目，可维护性与开发速度重于架构完备性

### Viable options

**Option A: 忠实移植参考项目架构**
- 实现思路：将 GameController 8 态状态机（IDLE/WAITING_FOR_USER/WAITING_FOR_ENGINE/BESTMV/MULTIPV/...）逐态移植为 C#，UI 按钮与状态交互照搬参考项目
- 改动文件：`Scripts/Controllers/GameController.cs`、`Scripts/Controllers/ManualController.cs`（二期）、对应 UI 层
- Pros：与参考项目逐类对应，真实用户验证过的交互矩阵，踩过的坑已知；二期打谱模式（MANUAL_MODE）天然预留
- Cons：8 态机为 SurfaceView 100ms 轮询渲染设计，与 Godot 信号驱动模式错位；MVP 用不到 MULTIPV_WAITING/MANUAL 等状态，起步即背全量复杂度；状态迁移正确性靠人工对照，无单测抓手

**Option B: Godot 原生分层架构（favored）**
- 实现思路：四层——Core（纯 C# 规则库）、Engine（async UCI 会话服务）、Game（GameSession 对局会话）、UI（场景 + 信号）；不移植 8 态机，用 async/await + 忙闲门闸管理并发交互
- 改动文件：`Scripts/Core/`（Board/Move/Rule/Fen/ChineseNotation）、`Scripts/Engine/UciSession.cs`、`Scripts/Game/GameSession.cs`、`Scripts/UI/BoardView.cs`、`Scenes/Main.tscn`、`Scenes/Board.tscn`
- Pros：MVP 代码量最小；规则层脱离 Godot 可跑 perft 单测；async/await 天然适配 UCI 长会话；二期打谱只是 GameSession 增加模式，不动地基
- Cons：引擎思考中的并发交互（悔棋/停止/切模式）无现成状态图可抄，需自行设计忙闲门闸并逐一验证——缓解：把参考项目 `controllers/controller-state.drawio.png` 状态图的每条边转化为并发场景测试清单

**Option C: GDScript UI + C# 引擎/规则混合**
- invalidated：用户已选定 C# 单栈；双语言边界需要胶水层，Godot 中 C# 与 GDScript 互调有限制，无对应收益

## 架构设计

### 分层与目录

```
f:\Dev\superchess\
├── project.godot                    # Godot 4.5+ .NET 项目
├── superchess.csproj
├── export_presets.cfg               # Android gradle 导出配置
├── Makefile                         # 改造：check = dotnet format --verify-no-changes + dotnet test
├── assets/
│   ├── board/chessboard.png         # 复用参考项目（MIT）
│   ├── pieces/r_*.png b_*.png       # 14 张棋子
│   ├── markers/                     # 选中框、落点提示、digit1-5 角标
│   └── sounds/                      # select/move/capture/check/checkmate/invalid
├── engines/android/arm64-v8a/       # pikafish 二进制 + nnue（GPL-3.0，进程独立分发）
├── Scripts/
│   ├── Core/                        # 纯 C#，零 Godot 依赖
│   │   ├── Board.cs                 # int[10][9]，Piece 编码
│   │   ├── Move.cs                  # UCCI 编解码
│   │   ├── Rule.cs                  # 走法生成/将军/将死/困毙/白脸将
│   │   ├── Fen.cs                   # FEN 编解码
│   │   └── ChineseNotation.cs       # 中文纵线着法生成
│   ├── Engine/
│   │   ├── UciEngineProcess.cs      # 进程封装抽象（System.Diagnostics.Process / Android interop 可替换实现）
│   │   ├── UciSession.cs            # UCI 协议会话（握手/position/go/stop/解析 info）
│   │   └── EngineOptions.cs         # Threads/Hash/MultiPV/Skill Level/UCI_Elo/UCI_LimitStrength
│   ├── Game/
│   │   ├── GameSession.cs           # 对局状态机（忙闲门闸）
│   │   └── HistoryRecord.cs         # move/ucci/chs/isRedMove
│   └── UI/
│       ├── BoardView.cs             # 棋盘渲染（Node2D，_Draw 画箭头）
│       ├── BoardInput.cs            # 触点→格坐标映射，点击两段式（选子→落子）
│       ├── ArrowLayer.cs            # 走子历史箭头 + MultiPV 建议箭头
│       └── HudPanel.cs              # 控制/历史/设置面板
├── Scenes/
│   ├── Main.tscn
│   └── Board.tscn
└── tests/Core.Tests/                # xUnit 独立测试工程（引用 Core 源码）
```

### 接口定义（核心 API）

```csharp
// Scripts/Core
public sealed class Board {
    public int[,] Cells { get; }                     // [y=10, x=9]，0=空，1-7 红(帅仕相马车炮兵)，8-14 黑
    public static Board FromFen(string fen);         // 非法 FEN 抛 FenFormatException(字段名+原因)
    public string ToFen();
    public Board Clone();
}

public static class Rule {
    public static List<Move> GetLegalMoves(Board board, Position from);          // 含蹩马腿/塞象眼/过河兵/九宫
    public static bool IsInCheck(Board board, bool isRedSide);
    public static bool IsCheckmate(Board board, bool isRedSide);                  // 将死或困毙
    public static bool IsKingsFacing(Board board);                                // 白脸将
}

public static class ChineseNotation {
    public static string ToChinese(Board board, Move move);   // "炮二平五"，含前/后/中兵消歧（依据 xqbase 规范，移植 Move.java getChsString）
}

// Scripts/Engine
public sealed class UciSession : IAsyncDisposable {
    public Task StartAsync(string enginePath, EngineOptions options);             // uci -> setoption -> isready -> uciok
    public Task<SearchResult> GoAsync(GoParams p, CancellationToken ct);          // position fen ... moves ... + go depth/movetime/infinite
    public void Stop();                                                           // 中断搜索，返回当前 bestmove
    public event Action<MultiPvInfo>? InfoReceived;                               // info depth N multipv K score cp/mate pv ...
}

// Scripts/Game
public sealed class GameSession {
    public GameMode Mode { get; }                  // PlayWithEngine / Analyze
    public bool Busy { get; }                      // 忙闲门闸：引擎思考中拒绝互斥操作或排队
    public Task<Move> RequestEngineMoveAsync();
    public Task<IReadOnlyList<MultiPvInfo>> AnalyzeAsync(int multiPv);
    public void Undo();                            // 引擎回合连退两步
    public Task<Move?> HintAsync();
}
```

### 数据模型

- 局面：`int[10][9]`（y 行 x 列，原点左上）；FEN 沿用参考项目格式（w/b 回合标记 + 回合数）
- 走子：`Move(from, to, piece, comment)`；UCCI 坐标 `h2e2`（y 取 9-y）；XQF 二进制坐标 `x*10+y`（左下原点，二期用）
- 历史：`HistoryRecord(move, ucci, chs, isRedMove)` 列表，悔棋从尾部弹出并逆操作恢复

### 算法与流程

- 走法生成：偏移表法（offsetX/offsetY 二维数组按兵种），移植 `gamelogic/Rule.java`；每步后过滤自杀着（模拟走子后 IsInCheck）
- 将死判定：枚举己方全部应手逐一模拟，无合法着且被将军=将死，无合法着未被将军=困毙
- UCI 会话：启动握手（uci/setoption/isready）→ 就绪后 position+go → 异步读行解析 info/bestmove；stop 后引擎仍回 bestmove（当前最优），不当作错误
- Android 引擎启动：从 APK jniLibs 目录（lib*.so 伪装）解析出可执行路径，先检测 CPU dotprod 特性选择引擎变体，dotprod 失败回退普通版

### 异常处理

| 异常 | 处理 |
|---|---|
| 引擎进程启动失败 | HudPanel 显示错误并禁用 AI/分析功能，本地双人走子仍可用，不崩溃 |
| UCI 读超时（>5s 无响应） | 取消当前搜索，提示重试；连续 2 次标记引擎不可用 |
| FEN 非法 | FenFormatException 携带字段与原因，UI 显示 400 类错误文案 |
| 引擎思考中用户点击互斥操作 | Busy 门闸拦截并给出轻提示（震动/音效），不排队积压 |

### 依赖项

- Godot 4.7.2 .NET 版（D:\DesignTools\Godot_v4.7.2-stable_mono_win64\）、.NET 8 SDK（GodotSharp 4.7.2 TFM 为 net8.0，rollForward LatestMajor）；Android 导出需 JDK 17+（本机 JDK 21）+ Android SDK（本机 %LOCALAPPDATA%\Android\Sdk，build-tools 35/36、platform android-35）
- Pikafish arm64-v8a 引擎二进制 + NNUE 权重（复用参考项目 `app/src/main/pikafish/arm64-v8a/`，GPL-3.0，以独立进程分发，关于页须附 GPL 文本与源码指引）
- 图片/音效资产（复用参考项目 `res/drawable`、`res/raw`，MIT）
- 参考项目源码作为移植蓝本：`gamelogic/Rule.java`、`gamelogic/Board.java`、`gamelogic/Move.java`、`org/petero/droidfish/player/ComputerPlayer.java`（UCI 状态机逻辑）、`utils/ArrowShape.java`
- 不引入：开局库（74MB OBK）、内置棋谱库（2.7 万局 XQF）、tinypinyin、XQFParser（全部二期）

## 实施步骤

### M0 仓库转型与工程管线
1. [x] 清理 Python 模板文件（pyproject.toml、src/、tests/、docs/*.rst、tox.ini、pyrefly.toml、.coveragerc、.copier-answers.yml、.readthedocs.yaml、pre-commit、bumpversion、pytest.ini、ruff.toml、旧 CI workflows），保留 .git/.trae/.github/LICENSE — 全仓范围
2. [x] 初始化 Godot 4 .NET 项目（project.godot、superchess.csproj、superchess.sln、Scripts/Main.cs、Scenes/Main.tscn、icon.svg、.gitignore/.gitattributes 更新为 Godot C# 模板）— 根目录；目标引擎版本 Godot 4.7.2 .NET（2026-10-01 用户确认按 4.7.2 + Android Studio 推进，覆盖原 4.5.1 锁定），C# net8.0，渲染 gl_compatibility，竖屏 1080x1920
3. [x] 改造 Makefile：`check` = `dotnet format --verify-no-changes` + `dotnet test`；`push` 保留多远程推送包装 — `Makefile`
4. [x] Android 导出管线：export_presets.cfg + Android Build Template（`android/build/`，含 `.build_version`=4.7.2.stable.mono 与 `.gdignore` 标记）+ gradle 构建，CLI `--export-debug` 导出 `build/android/superchess-debug.apk` 并通过 apksigner 验签 — `export_presets.cfg`、`android/`；注意：compileSdk/targetSdk 暂用 35（本机仅装 android-35 平台，SDK licenses 目录写入被沙箱拦截无法补哈希），模板 `config.gradle` 已同步改为 35，后续 SDK 组件齐全后可升回 36
5. [ ] 验收：Windows 桌面空场景可跑（已验证）+ 真机空 APK 可装（待用户连接设备后 adb install 验证）

> M0-4 已完成（2026-10-01）：Godot 4.7.2 .NET（D:\DesignTools\Godot_v4.7.2-stable_mono_win64\）+ 4.7.2.stable.mono 导出模板已装，Android SDK/JDK 21 由编辑器配置。已知问题：① CLI 导出结束后 Godot 进程可能挂起（gradle 已 BUILD SUCCESSFUL、产物已生成），需手动结束进程；② release 导出需先配置发布密钥库（M6 处理）；③ gradle wrapper 发行版已预装到 %USERPROFILE%\.gradle（services.gradle.org 直连超时，用多线程分段下载补装）；④ godot-lib AAR 不入库（超 GitHub 100MB 限制），新克隆环境需解压 `%APPDATA%\Godot\export_templates\4.7.2.stable.mono\android_source.zip` 到 `android/build/` 还原 libs 后方可 gradle 导出。

### M1 Android 引擎通信 PoC（Go/No-Go 决策点）
6. [x] 引擎资产复制到 `engines/android/arm64-v8a/`（dotprod/普通版/ini/NNUE 共 46.6MB；二进制不入库，.gitignore `*.so`，需从参考项目手动放置），gradle `copyPikafishLibs` 任务构建时复制进 jniLibs（AGP 忽略项目目录外 srcDirs，不能直接引用 engines/），导出排除 `engines/*` 防止资产重复进包 — `engines/`、`android/build/build.gradle`
7. [ ] `UciEngineProcess.cs` 初版已完成（stdio 管道、逐行事件、超时退出）+ `EngineLocator.cs`（/proc/self/maps 解析 nativeLibraryDir）+ `EnginePoc.cs`（uci→uciok/setoption EvalFile/isready/go depth 10/stop 握手探针）；真机验证 uciok + NNUE + bestmove 待设备连接 — `Scripts/Engine/`
8. [x] dotprod 运行时检测（/proc/cpuinfo Features）与引擎变体回退（dotprod 缺失→普通版）已在 `EngineLocator.ResolveEnginePath` 实现，真机行为随第 7 步验证 — `Scripts/Engine/EngineLocator.cs`
9. 若第 7 步失败，依序启动 fallback A（Godot 4.3+ Android Java interop 调 ProcessBuilder）→ fallback B（C++ GDExtension 管道封装，仅此一件原生代码）；两个 fallback 均失败则回到用户决策（全项目降级 GDScript+GDExtension，UI 层重做）

### M2 规则核心库（纯 C# + xUnit）
10. `Board.cs` + `Fen.cs`：局面表示与 FEN 编解码，移植 Board.java（含 toFENString/restoreFromFEN 语义） — `Scripts/Core/Board.cs`、`Scripts/Core/Fen.cs`
11. `Move.cs`：UCCI 编解码（h2e2，y 取 9-y） — `Scripts/Core/Move.cs`
12. `Rule.cs`：7 兵种走法生成（偏移表 + 蹩马腿/塞象眼/过河兵/九宫）+ 将军/将死/困毙/白脸将 — `Scripts/Core/Rule.cs`
13. `ChineseNotation.cs`：中文纵线着法，移植 Move.java getChsString（前/后/中兵消歧） — `Scripts/Core/ChineseNotation.cs`
14. xUnit 测试工程：perft(1..3) 对拍（参考值先与第三方实现如 xqlite 交叉验证后再固化）+ 将军/将死/蹩马腿/过河兵/白脸将用例 + FEN 往返用例 + 中文记谱用例 — `tests/Core.Tests/`

### M3 棋盘 UI 与交互
15. 资产导入：棋盘/棋子/标记/音效复制到 `assets/` 并导入配置 — `assets/`
16. `Board.tscn` + `BoardView.cs`：棋盘渲染（底图等比缩放，1240x1340 坐标系）、棋子 Sprite2D、走子 Tween 动画 — `Scenes/Board.tscn`、`Scripts/UI/BoardView.cs`
17. `BoardInput.cs`：触点/点击 → 格坐标逆映射，两段式选子落子，选中框与可走点提示 — `Scripts/UI/BoardInput.cs`
18. 音效：select/move/capture/check/checkmate/invalid 触发 — `Scripts/UI/`
19. 验收：双人本地对弈完整可玩（AC-6 的 UI 部分）

### M4 对弈模式
20. `UciSession.cs` 完整实现：握手/setoption/position/go/stop/info 解析，移植 ComputerPlayer.java 的解析逻辑 — `Scripts/Engine/UciSession.cs`
21. `GameSession.cs`：人机对弈循环（人走→引擎应）、忙闲门闸、Undo（连退两步）、Hint — `Scripts/Game/GameSession.cs`
22. 强度设置 UI：UCI_Elo/UCI_LimitStrength/Threads/Hash（Threads 按 CPU 核数默认） — `Scripts/UI/HudPanel.cs`
23. 并发场景测试：对照参考项目 controller-state 状态图，覆盖「引擎思考中悔棋/停止/提示/退出」每条边 — `tests/`（GameSession 层单测，mock UciSession）

### M5 分析模式
24. `AnalyzeAsync`：MultiPV=3~5，解析 info 行（score cp/mate、pv）— `Scripts/Game/GameSession.cs`、`Scripts/Engine/UciSession.cs`
25. `ArrowLayer.cs`：走子历史箭头（alpha 渐隐）+ MultiPV 建议箭头（digit1-5 角标），移植 ArrowShape 为 Godot `_Draw` — `Scripts/UI/ArrowLayer.cs`
26. 评估显示：当前局面分值 + 历史走子评估列表 — `Scripts/UI/HudPanel.cs`

### M6 发布打磨
27. FEN 导入导出（剪贴板），非法 FEN 错误提示 — `Scripts/UI/HudPanel.cs`
28. 关于页：MIT 声明 + Pikafish GPL-3.0 声明与源码指引 — `Scenes/About.tscn`
29. Android 签名导出 + release APK 真机回归（AC-1..AC-8 逐条过） — `export_presets.cfg`
30. `make check` 全绿收尾，提交并 make push — 全仓

## Workspace setup

- 实施前运行 `git status --short` 与 `git branch --show-current`
- M0 涉及批量删除 Python 模板文件：用户已批准原地转型；删除前确认工作区无未提交的其他改动，删除后单独一个 commit（`refactor: 仓库由 Python 模板转型为 Godot 项目`）便于回溯
- 当前分支若为 main/master，M0 起建议新建 `feat/godot-mvp` 分支开发

## Risks & mitigations

| Risk | Mitigation |
|---|---|
| C# System.Diagnostics.Process 在 Godot Android AOT 导出中不可用或受限 | M1 作为 Go/No-Go 前置验证，写业务代码前确认；fallback A：Android Java interop（ProcessBuilder，参考项目同方案）；fallback B：C++ GDExtension 管道封装 |
| NNUE 45MB 打包后 Godot Android 无法从 res:// 直接 exec | 走 gradle jniLibs（lib*.so 伪装技巧，与参考项目一致）；APK 不压缩 .so 由 AGP 处理 |
| .NET Android AOT 反射/序列化限制 | Core 与 Engine 层不使用反射与 System.Text.Json；PoC 覆盖 |
| dotprod 设备兼容性（部分 arm64 无 dotprod） | 运行时读 /proc/cpuinfo features，默认普通版，检测到 dotprod 才启用增强版，启动失败自动回退 |
| 异步竞态（引擎思考中连续操作） | Busy 门闸 + CancellationToken；M4 用参考项目状态图逐边写并发测试 |
| perft 参考值记错导致规则库对拍失败 | M2 先用第三方实现（xqlite 或 elephant eye）交叉验证 perft 值再固化为断言 |
| GPL 传染疑虑 | Pikafish 以独立进程 + UCI 通用协议通信，主程序不链接；分发二进制随附 GPL 文本与源码指引（M6 关于页） |
| 触屏/桌面输入差异 | 两段式点击（选子→落子）同时适配鼠标与触屏，不实现拖拽（MVP 外） |

## Verification steps

- AC-1：`dotnet test`（perft 断言 + 规则用例）全绿
- AC-2/AC-3：真机 adb 安装 release APK，完整对弈一局；PoC 期间用日志验证 uciok/NNUE/stop
- AC-4：脚本化对拍——同一中局 FEN 分别以 UCI_Elo 1280/3133 各跑 5 局固定深度，出着差异 ≥ 3 处
- AC-5/AC-6/AC-7/AC-8：真机手工回归清单（M6 验收表逐条勾选）
- AC-9：`make check` 退出码 0

## ADR

- **Decision**: 采用 Godot 4 .NET 分层架构（Option B）：纯 C# 规则核心库 + async UCI 引擎会话 + 信号驱动 UI；引擎为独立 Pikafish 进程，Android 上以 gradle jniLibs 方式打包
- **Drivers**: Android 优先（决定 jniLibs 与 arm64-only）；C# 技术栈（决定 Process 管道方案与 async 架构）；单人项目开发速度
- **Alternatives considered**: Option A 忠实移植 8 态状态机 — rejected（与 Godot 模式错位、MVP 背全量复杂度，但其状态图转为并发测试清单被吸收）；Option C GDScript 混合 — rejected（双语言无收益）
- **Why chosen**: 规则层可独立 perft 单测保证正确性；async/await 与 UCI 长会话天然契合；复用参考项目算法与资产最大化减少新写代码
- **Consequences**: 二期打谱（XQF）在 GameSession 加模式即可；Android .NET 导出风险由 M1 PoC 前置隔离；包体下限约 50MB（NNUE）
- **Follow-ups**: XQF 打谱与变着分支、开局库（OBK）、局面编辑器、走子历史箭头数设置、评估趋势折线图、按需下载 NNUE 减包体

## Review trail

- Planner draft v1: Option B 分层架构 + 6 里程碑 + 风险前置 PoC
- Architect challenge v1: steelman 支持状态机方案（并发交互防御设计），tension 为 Android 优先 vs .NET 导出成熟度；结论保持 Option B，吸收参考项目状态图为并发测试清单
- Critic verdict v1: APPROVED with 3 improvements — ① perft 值须第三方交叉验证 ② GPL 声明页列入 M6 ③ 补全 fallback 全失败时的用户决策出口（M1 第 9 步）
- Final iterations: 1 / 3
