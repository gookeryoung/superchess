# iter-15 debug APK 刷新至二期代码

## 需求清单

req-02 AC-P7 真机走查的前置准备：现有 debug APK（2026-10-02 导出）不含二期打谱/残局深化代码，设备接入前刷新。

## 迭代目标

导出包含全部二期代码（main @ a9f4b8f）的 debug APK，验签并统一命名，供真机回归使用。

## 改动文件

- `Scripts/Game/PuzzleProgress.cs.uid`、`tests/Game.Tests/PuzzleProgressTests.cs.uid`：补录入库（Godot .uid 旁车文件须入库，commit a9f4b8f）
- `build/android/superchess-debug.apk`：重新导出（不入库），147,740,561 字节，apksigner 验签通过

## 关键决策

- Godot CLI 导出（Android debug 预设，export_path build/android/superchess.apk）后手动复制为 -debug 名，沿用 iter-13 流程
- 导出进程再次挂起（gradle 已成功、产物已生成），确认产物后结束进程，与既往经验一致
- CI 历史失败确认为 init commit 时代已删除的 Python 工作流记录，无需处理

## 测试结果

- `make check` 全绿：format + build + test 119 例（Core 44 + Manual 32 + Game 43）
- apksigner verify 证书为 Godot 调试密钥库（CN=Godot）

## 遗留事项

- 真机走查（M6-29/30 + AC-P7 + M0-5 + M1-7）：adb devices 为空，待用户连接设备
- Android native 文件选择器 PoC 未验证，打谱入口维持内置棋谱降级方案

## 下一轮计划

设备连接后：adb 安装 superchess-debug.apk，逐条执行真机回归清单。
