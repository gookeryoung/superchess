# iter-09 桌面引擎集成与 AC-4 桌面验证

## 需求清单

- 用户要求不连接设备继续推进：集成 Windows 版 Pikafish，使 AC-2..AC-5 的软件行为（人机对弈/UCI 通信/强度分级/分析）可在桌面完整验证，收窄真机回归范围

## 迭代目标

- EngineLocator 桌面分支：engines/windows/ 定位引擎与 NNUE
- 桌面真实引擎端到端冒烟：UCI 握手→人机应手→悔棋→提示→MultiPV 分析→AC-4 强度对拍

## 改动文件

- 修改：Scripts/Engine/EngineLocator.cs（ResolveEnginePath/ResolveNnuePath 增加可选 projectDir 参数与桌面分支）、Scripts/Main.cs（OnModeToggled 传项目目录）、.gitignore（engines/windows/*.exe 与 pikafish.nnue 不入库）
- 新增（入库）：engines/windows/Copying.txt（Pikafish GPL-3.0 文本）
- 手动放置（不入库）：engines/windows/Pikafish-Windows-x86-64-universal.exe（6.6MB）、pikafish.nnue（48MB），来自官方 release Pikafish-2026-09-06

## 关键决策

- 选官方「Windows-x86-64-universal」通用构建：运行时自动分派指令集，免去 AVX2/BMI2 变体检测
- EngineLocator 桌面分支以 projectDir 参数驱动（Main 用 ProjectSettings.GlobalizePath("res://") 传入），Engine 层保持零 Godot 依赖；Android nativeLibraryDir 分支不变
- 引擎二进制沿用 *.so 忽略策略：exe 与 NNUE 不入库，Copying.txt（GPL 文本）入库支持合规
- AC-4 桌面验证按设计验收方式执行：同一局面 UCI_Elo 1280/3133 各 5 局固定深度逐局对比

## 测试结果

- `make check`：通过（44 + 12 例）
- 桌面真实引擎冒烟（临时 SceneTree 脚本，验证后删除）：全部通过
  - 握手+NNUE 加载、人机应手（h9g7 马8进7）、悔棋连退两步、提示（h2e2）、MultiPV=3 分析（depth 14 三路带分值着法）
  - AC-4：2 局面 × 双方各 5 局，出着差异 ≥3（1280 出着分散：b2e2/g3g4/h2e2...，3133 稳定收敛）
- debug APK 已重新导出（147MB，含 M3-M6 代码），待设备连接安装

## 遗留事项

- 真机回归（AC-1..AC-8、M0-5、M1-7）仍待设备连接；桌面已验证项真机上仅剩设备特有风险（进程权限/性能/触屏/字体）
- release 签名导出待发布密钥库

## 下一轮计划

- 设备连接后：adb 安装、真机逐条回归、勾选设计 29/30 收尾
