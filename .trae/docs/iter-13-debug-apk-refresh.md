# iter-13 真机回归 APK 刷新

## 需求清单

req-01 已全交付（REQ-1..7）；本轮聚焦 M6-29 真机收尾的前置准备（设计 `godot-xiangqi-android.md` 步骤 5/7/30）。

## 迭代目标

既有 debug APK（10-01 20:59）不含教学/残局与分析开关代码，重新导出含全部最新代码的 debug APK，使设备连接后可立即开始真机回归。

## 改动文件

- `build/android/superchess-debug.apk`（不入库）：Godot CLI `--export-debug Android` 重新导出，147,688,192 字节，含教学 10 课 + 残局 8 题 + 连续分析开关全部代码；apksigner 验签通过（debug 密钥库）
- `.trae/docs/`：本迭代记录；按「保留最新 5 条」删除 iter-08

## 关键决策

- 沿用 debug 密钥库导出（export_presets.cfg 未改）：真机回归仅装自用包，release 签名（M6-30）待设备连接与发布密钥库配置一并处理。
- 导出后 Godot 控制台进程与 gradle java 进程挂起为已知问题（gradle 已成功、产物已生成），手动结束两进程即收尾；产物位于 gradle 输出后由 Godot 复制为 `build/android/superchess.apk`，本轮统一重命名为 `superchess-debug.apk` 保持既有回归指引不变。

## 测试结果

- `make check`：format + build + test 全绿（Core.Tests 44 例 + Game.Tests 30 例）
- `apksigner verify --print-certs`：签名有效（CN=Godot debug 密钥库）

## 遗留事项

- M6-29/30 真机收尾：adb 安装 debug APK → 教学课程/残局走查 + AC-1..AC-8 回归 + M0-5 空 APK/M1-7 引擎 PoC 复验 → release 密钥库决策与签名导出；全部待用户连接设备。

## 下一轮计划

设备连接后执行真机回归清单（逐条对照 AC-1..AC-8 + 教学/残局流程）。
