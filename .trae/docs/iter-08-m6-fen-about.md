# iter-08 M6 发布打磨（FEN 剪贴板 + 关于弹窗）

## 需求清单

- M6-27/28：FEN 剪贴板导入导出与错误提示、关于/开源声明（设计 `.trae/designs/godot-xiangqi-android.md` M6 节，AC-8）
- M6-29：Android 签名导出 + 真机回归（待设备连接，本轮暂停）

## 迭代目标

- HUD FEN 行（复制/粘贴/关于）；粘贴 FEN 载入局面，非法 FEN 明确报错不崩溃
- 关于弹窗：MIT + 象棋鱼素材来源 + Pikafish GPL-3.0 声明与源码指引

## 改动文件

- 修改：Scripts/UI/HudPanel.cs（CopyFen/PasteFen/About 事件 + FEN 行）、Scripts/Main.cs（OnCopyFen/OnPasteFen/BuildAboutDialog）

## 关键决策

- 关于页用 AcceptDialog 代码构建替代设计原定的独立 Scenes/About.tscn：场景切换会丢失对局状态，弹窗零成本且不中断对局（设计文件已同步修正）
- FEN 复制/粘贴走 DisplayServer.ClipboardSet/ClipboardGet（Android/桌面通用）；粘贴失败按 FenFormatException.FieldName 定位出错字段，状态栏显示「非法 FEN：<字段> 字段错误（<原因>）」
- 载入成功后经 BoardReverted 事件自动全量重绘并清空箭头/评估

## 测试结果

- `make check`（dotnet format --verify-no-changes + dotnet build + dotnet test）：通过（44 + 12 例）
- headless 冒烟：主场景 30 帧无报错
- FEN 剪贴板行为与非法 FEN 提示为平台交互功能，待真机/桌面人工确认（AC-8）

## 遗留事项

- M6-29 Android release 签名导出 + 真机回归（AC-1..AC-8 逐条过）：待用户连接设备；release 导出还需配置发布密钥库（debug 密钥库已配置）
- HUD 中文系统字体回退在真机的实际效果待确认

## 下一轮计划

- 设备连接后：debug/release APK 导出、adb 安装、真机逐条回归 AC-1..AC-8、M0-5/M1-7 遗留验证，全过后勾选设计 29/30 收尾
