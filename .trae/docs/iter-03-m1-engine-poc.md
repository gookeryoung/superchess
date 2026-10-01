# iter-03 M1 引擎通信 PoC 实现（Go/No-Go 前置）

## 需求清单

- M1：Android 上 C# Process 启动 Pikafish 并完成 UCI 通信验证（设计 `.trae/designs/godot-xiangqi-android.md` M1 节）

## 迭代目标

- 引擎资产入库与 jniLibs 打包链路打通
- `UciEngineProcess` 进程封装 + 引擎定位器 + 握手探针
- APK 构建验证引擎与 NNUE 注入

## 改动文件

- 新增：engines/android/arm64-v8a/（libpikafish-armv8-dotprod.so、libpikafish-armv8.so、libpikafish.ini.so、libpikafish.nnue.so，GPL-3.0；**引擎二进制不入库**（.gitignore `*.so`），仅本地存在，来源为参考项目 chinese-chess-fish-android）、engines/.gdignore、Scripts/Engine/UciEngineProcess.cs、Scripts/Engine/EngineLocator.cs、Scripts/Engine/EnginePoc.cs
- 修改：android/build/build.gradle（sourceSets 增加 libs/pikafish + copyPikafishLibs 任务）、export_presets.cfg（exclude_filter=engines/*）、.gitignore（排除 libs/pikafish 生成副本）、Scripts/Main.cs（挂接 PoC）

## 关键决策

- AGP 8.6.1 静默忽略项目目录外的 jniLibs srcDirs（实测合并结果不含外部目录），故采用 gradle `copyPikafishLibs`（Copy 任务，依赖挂到各 variant 的 merge*JniLibFolders）构建时把 `engines/android/arm64-v8a` 复制到 `android/build/libs/pikafish/arm64-v8a`
- NNUE 以 `libpikafish.nnue.so` 伪装随 jniLibs 打包（useLegacyPackaging 因 minSdk 24 ≤ 29 自动开启，安装后解压至 nativeLibraryDir 可读）；`EnginePoc` 会将 EvalFile setoption 指向其绝对路径（参照参考项目 PikafishExternalEngine 行为）
- nativeLibraryDir 解析不依赖 Java interop：读 /proc/self/maps 定位已加载 libgodot_android.so 所在目录（fallback A 的替代，PoC 通过则无需 interop）
- dotprod 检测读 /proc/cpuinfo Features；增强版缺失自动回退普通版
- export_presets.cfg exclude_filter 排除 engines/*，避免引擎二进制被同时复制进 APK assets 造成重复
- 引擎二进制不入库（.gitignore `*.so`，与既有"引擎单独分发管理"约定一致）：新环境需手动将 Pikafish 资产放入 engines/android/arm64-v8a/ 才能构建 Android 导出

## 测试结果

- `make check`（dotnet format --verify-no-changes + dotnet test）：通过
- gradle `:copyPikafishLibs` + `:mergeMonoDebugJniLibFolders`：4 个 pikafish .so 全部合并
- `--export-debug`：BUILD SUCCESSFUL、exit 0；APK 129.3MB，lib/arm64-v8a 含 libpikafish-armv8(-dotprod).so、libpikafish.ini.so、libpikafish.nnue.so(43.4MB)
- 桌面 `--quit-after 120`：正常退出

## 遗留事项

- M1-7 真机验证（Go/No-Go）：待用户连接设备后 `adb install` 并 logcat 过滤 `EnginePoC`，验证 uciok/NNUE/bestmove/stop
- EnginePoc 为临时代码，M4 由 UciSession 替换后删除

## 下一轮计划

- 真机 PoC 验证通过 → M2 规则核心库（纯 C# + xUnit perft）；失败 → 按 fallback A（Java interop ProcessBuilder）重试
