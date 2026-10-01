# SuperChess

> 中国象棋训练与 AI 练习工具（Godot 4 跨平台版，Android 优先）。

![Godot](https://img.shields.io/badge/Godot-4.5%20.NET-blue.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)

## 特性

- **人机对弈**：内置 Pikafish 引擎（UCI 协议），强度可调（UCI_Elo 分级）
- **分析模式**：MultiPV 多路评估 + 建议箭头
- **训练辅助**：悔棋、提示、FEN 导入导出、中文纵线记谱
- **跨平台**：Android 优先（arm64-v8a），桌面平台跟随

## 开发

技术栈：Godot 4.5 .NET（C#）+ Pikafish 独立引擎进程。

```bash
# 构建与门禁
make check    # dotnet format 校验 + dotnet test
make build    # 构建解决方案
```

Android 导出：Godot 编辑器 + Android Build Template（见 `.trae/designs/godot-xiangqi-android.md`）。

## 许可证

- 本项目代码：MIT
- Pikafish 引擎：GPL-3.0（以独立进程 + UCI 协议通信，随分发附声明）
- 棋盘/棋子/音效资产：复用自 [chinese-chess-fish-android](https://github.com/zfdang/chinese-chess-fish-android)（MIT）
