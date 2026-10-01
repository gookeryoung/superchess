# iter-02 Godot 4.7.2 升级与 Android 导出管线（M0-4/M0-5）

## 需求清单

- 用户确认按 Godot 4.7.2 .NET + Android Studio 环境推进开发（覆盖 iter-01 的 4.5.1 版本锁定）
- 完成 M0-4 Android 导出管线与 M0-5 运行验证：设计与实施计划见 `.trae/designs/godot-xiangqi-android.md`

## 迭代目标

- csproj 升级至 Godot.NET.Sdk/4.7.2，make check 全绿
- 安装 4.7.2.stable.mono 导出模板与 Android 构建模板
- CLI 导出 Android debug APK 并通过签名验证；真机安装验证待设备连接

## 改动文件

- 修改：superchess.csproj（Sdk 4.5.1 → 4.7.2）、.gitignore（android 构建模板入库 + build/ 忽略）、android/build/config.gradle（compileSdk/targetSdk 36 → 35）、.trae/designs/godot-xiangqi-android.md
- 新增：android/（gradle 构建模板 + `.build_version` + `.gdignore`）、export_presets.cfg（Android 预设，arm64-v8a、gradle 构建、com.superchess.game）
- 产物：build/android/superchess-debug.apk（96.6MB，已忽略入库）

## 关键决策

- 导出模板安装目录为 `%APPDATA%\Godot\export_templates\4.7.2.stable.mono`（mono 编辑器查找带 `.mono` 后缀目录，经模板包内 version.txt 确认）
- Android 构建模板必须位于 `android/build/` 子目录，且需两个标记文件：`.build_version`（内容 `4.7.2.stable.mono`，版本不匹配会拒绝构建）与 `.gdignore`（缺省时 Godot 会把模板 res 当游戏资源导入，生成的 `.import` 文件破坏 AAPT 编译）——此为 editor「安装构建模板」行为的逆向还原，zip 内不含标记
- compileSdk/targetSdk 降为 35：本机仅装 android-35 平台与 build-tools 35/36；SDK licenses 目录仅含旧哈希，补齐新哈希需写 SDK 目录（沙箱拦截）。AGP 8.6.1 兼容 API 35，无功能影响
- gradle-8.11.1 发行版经 6 线程分段下载后预装到 `%USERPROFILE%\.gradle\wrapper\dists\`（services.gradle.org 直连读超时）；AGP 依赖走 .gradle 缓存正常
- NDK 未安装：构建脚本无 externalNativeBuild 任务，gradle 构建不触发 NDK（Godot .so 为模板预编译产物）
- godot-lib AAR（debug 107.9MB / release 98.8MB）超 GitHub 100MB 限制，不入库、不引入 Git LFS；`.gitignore` 排除并注明新环境还原命令（解压导出模板包内 android_source.zip 至 android/build）
- C# 目标框架维持 net8.0（GodotSharp 4.7.2 runtimeconfig tfm=net8.0，rollForward LatestMajor）

## 测试结果

- `dotnet build superchess.sln`：0 错误 0 警告
- `make check`（dotnet format --verify-no-changes + dotnet test）：通过
- 桌面运行 `--quit-after 120`：正常退出，输出「SuperChess M0 工程初始化成功」
- `--export-debug`：gradle BUILD SUCCESSFUL；apksigner 验签通过（Godot debug 证书）；aapt2 badging 确认包名 com.superchess.game、native-code arm64-v8a、minSdk 24

## 遗留事项

- M0-5 真机安装验证：待用户连接 Android 设备（需开 USB 调试）后 `adb install` 回归
- CLI 导出结束后 Godot 进程偶发挂起（gradle 产物已生成），需手动结束；后续可改用直接调用 gradlew 的方式规避
- release 导出需发布密钥库（M6 处理）；SDK licenses 哈希待补齐后可升回 compileSdk 36
- CI workflow 仍未重建（沿袭 iter-01 遗留）

## 下一轮计划

- 用户连接真机完成 M0-5 验收后，进入 M1 Android 引擎通信 PoC（Go/No-Go 决策点）
