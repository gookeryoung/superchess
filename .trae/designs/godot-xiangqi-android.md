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
public interface IUciSession : IAsyncDisposable {
    bool IsReady { get; }
    event Action<MultiPvInfo>? InfoReceived;          // 含 pv 数据的 info 行（走子方视角分值）
    Task StartAsync(string enginePath, EngineOptions? options, CancellationToken ct);   // uci -> setoption -> isready
    Task<SearchResult> GoAsync(GoParams p, CancellationToken ct);   // position fen ... moves ... + go depth/movetime/infinite
    Task ApplyOptionsAsync(EngineOptions options, CancellationToken ct);  // setoption 同步 + readyok
    void Stop();                                      // 中断搜索，引擎仍回 bestmove（不当作错误）
}
// record 类型：EngineOptions(Threads/HashMb/EvalFile/LimitStrength/Elo/SkillLevel/HandshakeTimeout)
//             GoParams(Fen/Moves/Depth/MoveTimeMs/Infinite/MultiPv)、SearchResult(BestMove/PonderMove)
//             MultiPvInfo(Index/Depth/ScoreCp/IsMate/UpperBound/LowerBound/Pv)

// Scripts/Game
public sealed class GameSession : IDisposable {
    public GameMode Mode { get; }                  // TwoPlayers / PlayWithEngine
    public bool Busy { get; }                      // 忙闲门闸：引擎思考中拒绝互斥操作
    public bool IsGameOver { get; }                // 将死或困毙
    public bool AttachEngine(IUciSession engine);  // 同实例重复附加幂等成功
    public bool SetMode(GameMode mode);
    public bool TryPlayMove(Move move);            // 人类走法（Busy/终局拒绝）
    public Task<bool> RequestEngineMoveAsync(CancellationToken ct);  // 引擎应手（代际计数丢弃过期结果）
    public bool Undo();                            // 人机连退两步，双人退一步
    public Task<Move?> HintAsync(CancellationToken ct);
    public Task<IReadOnlyList<MultiPvInfo>> AnalyzeAsync(int multiPv, CancellationToken ct);  // M5：每路 PV 最新评估，按序号排序
    public void NewGame();                         // 思考中先取消搜索
    public bool LoadFen(string fen);               // 非法 FEN 抛 FenFormatException
    public Task<bool> ApplyEngineOptionsAsync(EngineOptions options, CancellationToken ct);
    public event Action<MoveAppliedEventArgs>? MoveApplied;   // move/chinese/isCapture/isRedMove/isCheck/isCheckmate/isStalemate
    public event Action? BoardReverted;            // 悔棋/新局/载入后全量重绘
    public event Action<Move>? HintProvided;
    public event Action<bool>? BusyChanged;
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
3. [x] 改造 Makefile：`check` = `dotnet format --verify-no-changes` + `dotnet build` + `dotnet test`（M3 起增加 build：dotnet test 仅构建测试工程，主工程编译错误需 build 门禁捕获）；`push` 保留多远程推送包装 — `Makefile`
4. [x] Android 导出管线：export_presets.cfg + Android Build Template（`android/build/`，含 `.build_version`=4.7.2.stable.mono 与 `.gdignore` 标记）+ gradle 构建，CLI `--export-debug` 导出 `build/android/superchess-debug.apk` 并通过 apksigner 验签 — `export_presets.cfg`、`android/`；注意：compileSdk/targetSdk 暂用 35（本机仅装 android-35 平台，SDK licenses 目录写入被沙箱拦截无法补哈希），模板 `config.gradle` 已同步改为 35，后续 SDK 组件齐全后可升回 36
5. [ ] 验收：Windows 桌面空场景可跑（已验证）+ 真机空 APK 可装（待用户连接设备后 adb install 验证）

> M0-4 已完成（2026-10-01）：Godot 4.7.2 .NET（D:\DesignTools\Godot_v4.7.2-stable_mono_win64\）+ 4.7.2.stable.mono 导出模板已装，Android SDK/JDK 21 由编辑器配置。已知问题：① CLI 导出结束后 Godot 进程可能挂起（gradle 已 BUILD SUCCESSFUL、产物已生成），需手动结束进程；② release 导出需先配置发布密钥库（M6 处理）；③ gradle wrapper 发行版已预装到 %USERPROFILE%\.gradle（services.gradle.org 直连超时，用多线程分段下载补装）；④ godot-lib AAR 不入库（超 GitHub 100MB 限制），新克隆环境需解压 `%APPDATA%\Godot\export_templates\4.7.2.stable.mono\android_source.zip` 到 `android/build/` 还原 libs 后方可 gradle 导出。

### M1 Android 引擎通信 PoC（Go/No-Go 决策点）
6. [x] 引擎资产复制到 `engines/android/arm64-v8a/`（dotprod/普通版/ini/NNUE 共 46.6MB；二进制不入库，.gitignore `*.so`，需从参考项目手动放置），gradle `copyPikafishLibs` 任务构建时复制进 jniLibs（AGP 忽略项目目录外 srcDirs，不能直接引用 engines/），导出排除 `engines/*` 防止资产重复进包 — `engines/`、`android/build/build.gradle`
7. [ ] `UciEngineProcess.cs` 初版已完成（stdio 管道、逐行事件、超时退出）+ `EngineLocator.cs`（/proc/self/maps 解析 nativeLibraryDir）+ `EnginePoc.cs`（uci→uciok/setoption EvalFile/isready/go depth 10/stop 握手探针）；真机验证 uciok + NNUE + bestmove 待设备连接 — `Scripts/Engine/`
8. [x] dotprod 运行时检测（/proc/cpuinfo Features）与引擎变体回退（dotprod 缺失→普通版）已在 `EngineLocator.ResolveEnginePath` 实现，真机行为随第 7 步验证 — `Scripts/Engine/EngineLocator.cs`
9. 若第 7 步失败，依序启动 fallback A（Godot 4.3+ Android Java interop 调 ProcessBuilder）→ fallback B（C++ GDExtension 管道封装，仅此一件原生代码）；两个 fallback 均失败则回到用户决策（全项目降级 GDScript+GDExtension，UI 层重做）

### M2 规则核心库（纯 C# + xUnit）
10. [x] `Board.cs` + `Fen.cs`：局面表示与 FEN 编解码（int[10,9]，y 行 x 列原点左上；FenFormatException 携带字段名+原因，棋盘校验含行列/字符/双方各一将帅且在九宫内；宽松兼容「棋盘 走子方」短格式） — `Scripts/Core/Board.cs`、`Scripts/Core/Fen.cs`
11. [x] `Move.cs`：UCCI 编解码（h2e2，y 取 9-y） — `Scripts/Core/Move.cs`
12. [x] `Rule.cs`：7 兵种走法生成（偏移表 + 蹩马腿/塞象眼/过河兵/九宫/飞将）+ 合法着过滤（走后被将军/将帅照脸）+ 将军/将死/困毙/白脸将；车炮攻击探测以被攻击方颜色做探针（attackableByJuPao 语义） — `Scripts/Core/Rule.cs`
13. [x] `ChineseNotation.cs`：中文纵线着法，移植 Move.java getChsString（前/后/中兵消歧；多兵分居多线全盘编号为参考项目同款局限） — `Scripts/Core/ChineseNotation.cs`
14. [x] xUnit 测试工程：perft(1)=44、perft(2)=1920、perft(3)=79666 已与 pyffish（Fairy-Stockfish）交叉验证一致后固化 + 将军/将死/蹩马腿/塞象眼/过河兵/困毙/白脸将用例 + FEN 往返与非法 FEN 用例 + 中文记谱用例，共 44 例全绿 — `tests/Core.Tests/`

### M3 棋盘 UI 与交互
15. [x] 资产导入：棋盘/14 棋子/标记（选中框、落点提示、digit1-5 角标）/音效复制到 `assets/`（checkmate.m4a 经 ffmpeg 转 checkmate.ogg，Godot 不支持 AAC） — `assets/board/chessboard.png`、`assets/pieces/`、`assets/markers/`、`assets/sounds/`
16. [x] `Board.tscn` + `BoardView.cs`：底图 1240x1340 原生坐标系（格距 136、棋子 110、交叉点中心 (77+136x, 60+136y)），Main 按视口宽 1080/1240 等比缩放；棋子 Sprite2D 精灵表 + 走子 Tween 动画（0.15s CubicOut，被吃子同步淡出） — `Scenes/Board.tscn`、`Scripts/UI/BoardView.cs`
17. [x] `BoardInput.cs`：`_UnhandledInput` 鼠标/触点（触屏经 emulate_mouse_from_touch）→ `TryHit` 逆映射（棋子矩形内有效，格间空隙忽略），两段式选子落子（仅可选走子方棋子，Rule.GetLegalMoves 生成落点提示），`MoveChosen` 信号回传格坐标 — `Scripts/UI/BoardInput.cs`
18. [x] 音效：SoundPlayer 每音效独立 AudioStreamPlayer（select/move/capture/check/checkmate/invalid）；选子与非法点击由 BoardInput 触发，走子/吃子/将军/将死由 Main 在走子后判定触发；动画期间输入挂起 — `Scripts/UI/SoundPlayer.cs`、`Scripts/Main.cs`
19. [x] 验收：双人本地对弈完整可玩（headless 冒烟：炮二平五/黑方跳马/非走子方拦截/局面一致性全过）；桌面可视化交互待用户人工确认（AC-6 的 UI 部分）

### M4 对弈模式
20. [x] `UciSession.cs` 完整实现：握手（uci→uciok 15s 超时）/setoption（Threads/Hash/EvalFile/UCI_LimitStrength/UCI_Elo/Skill Level）/position+go/stop/info 解析（移植 ComputerPlayer.parseInfoCmd，只消费 depth/multipv/score/pv）；引擎进程退出故障化挂起搜索；接口抽象 IUciSession 供测试替换 — `Scripts/Engine/UciSession.cs`（含 GoParams/SearchResult/MultiPvInfo）、`Scripts/Engine/EngineOptions.cs`、`Scripts/Engine/UciEngineProcess.cs`（新增 Disconnected 事件）；EngineLocator 增加桌面分支（engines/windows/ 通用构建 + pikafish.nnue，从官方 release 手动放置不入库，Copying.txt GPL 文本入库） — `Scripts/Engine/EngineLocator.cs`
21. [x] `GameSession.cs`：人机对弈循环（人走→引擎应）、忙闲门闸（Busy 拒绝走子/悔棋/提示/切模式）、Undo（人机连退两步/双人退一步）、Hint（不落子）、NewGame（取消搜索 + 代际计数丢弃迟到 bestmove）、LoadFen（AC-8 导入入口，M6 剪贴板用）、ApplyEngineOptionsAsync — `Scripts/Game/GameSession.cs`、`Scripts/Game/GameTypes.cs`（GameMode/HistoryRecord/MoveAppliedEventArgs）
22. [x] 强度设置 UI：限棋力开关 + Elo 滑条（1280-3199）/线程数（默认 CPU 核数）/置换表(MB) + 新局/悔棋/提示/模式切换按钮 + 状态栏；中文文本依赖系统字体回退 — `Scripts/UI/HudPanel.cs`、`Scenes/Main.tscn`
23. [x] 并发场景测试：tests/Game.Tests（链接 Core/Engine/Game 源码，排除依赖 Godot 的 EnginePoc），FakeUciSession 覆盖「引擎思考中走子/悔棋/提示被拒、思考中新对局取消搜索并丢弃迟到 bestmove、对局结束拦截、连退两步、非法 FEN 保持局面」等 10 例 — `tests/Game.Tests/`

### M5 分析模式
24. [x] `AnalyzeAsync`：MultiPV=3（DefaultAnalyzeMultiPv，go depth 14），InfoReceived 逐路保留最新 info（score cp/mate、pv），bestmove 后按 PV 序号排序返回；忙闲门闸内进行，代际计数丢弃过期结果 — `Scripts/Game/GameSession.cs`
25. [x] `ArrowLayer.cs`：移植 ArrowShape 多边形参数（60°/120°、边长 80、头宽 26、尾宽 10）为 Godot `_Draw` 多边形；走子历史箭头 1.6s alpha 渐隐；MultiPV 建议箭头按排名 5 色分级 + digit1-5 角标 — `Scripts/UI/ArrowLayer.cs`、`Scenes/Board.tscn`（Arrows 节点）
26. [x] 评估显示：当前局面分值（红方视角换算，mate 显示 #N/-#N）+ 历史评估列表（手数 + 建议着法中文记谱 + 分值），分析按钮触发，悔棋/新局清空 — `Scripts/UI/HudPanel.cs`、`Scripts/Main.cs`
27. [x] 连续分析开关：分析按钮为开关（文案「分析」/「停止分析」，HudPanel.SetAnalysisActive）；开启后走子应用（双人模式即时、人机模式等引擎忙闲回落）、局面恢复（悔棋/新局/载入 FEN）、忙闲回落三处触发自动重新分析并刷新建议箭头与评估，无需每次点击；防重入标志 `_analysisRefreshing` 避免忙闲事件循环；进入练习模式自动停用并清显示；引擎忙时跳过刷新留待忙闲回落再触发 — `Scripts/Main.cs`（OnAnalyze/StopAnalysis/RefreshAnalysisAsync/ShowAnalysis）、`Scripts/UI/HudPanel.cs`

### M6 发布打磨
28. [x] FEN 导入导出（系统剪贴板 DisplayServer.ClipboardSet/ClipboardGet），非法 FEN 状态栏显示出错字段与原因且不崩溃 — `Scripts/UI/HudPanel.cs`（FEN 行）、`Scripts/Main.cs`（OnCopyFen/OnPasteFen）
29. [x] 关于页改为关于弹窗（AcceptDialog 代码构建，替代独立场景：避免场景切换丢失对局状态）：MIT 声明 + 「象棋鱼」素材来源声明 + Pikafish GPL-3.0 声明与源码指引（UCI 独立进程通信，非衍生作品） — `Scripts/Main.cs`（BuildAboutDialog）
30. [ ] Android 签名导出 + release APK 真机回归（AC-1..AC-8 逐条过） — `export_presets.cfg`；待用户连接设备 + 配置发布密钥库（debug 密钥库已配置）
31. [ ] `make check` 全绿收尾，提交并 make push — 全仓

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
- AC-4：桌面已验证（2026-10-02，Windows Pikafish 2026-09-06：2 个测试局面 × 双方各 5 局 depth 12，出着差异 ≥3，Elo 1280 出着明显分散走弱）；真机安装后可复验
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
