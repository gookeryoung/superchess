# iter-01 仓库转型与 Godot 工程初始化（M0）

## 需求清单

- 基于 chinese-chess-fish-android 借鉴内容，用 Godot 开发跨平台中国象棋（Android 优先、C# .NET、标准版 MVP）
- 设计与实施计划：`.trae/designs/godot-xiangqi-android.md`

## 迭代目标

M0 仓库转型与工程管线：清理 Python 模板、初始化 Godot 4 .NET 项目、改造 make check 门禁。

## 改动文件

- 删除：pyproject.toml、src/、tests/、docs/、tox.ini、pyrefly.toml、.coveragerc、.copier-answers.yml、.readthedocs.yaml、.pre-commit-config.yaml、.bumpversion.toml、pytest.ini、ruff.toml、.github/workflows/*.yml（Python 版 CI 失效，暂不重建，待 M6 或用户要求）
- 新增：project.godot、superchess.csproj、superchess.sln、Scripts/Main.cs、Scenes/Main.tscn、icon.svg
- 修改：Makefile（dotnet 门禁）、.gitignore/.gitattributes（Godot C# 模板）、README.md（Godot 版说明）、.trae/designs/godot-xiangqi-android.md（勾选进度）

## 关键决策

- 引擎版本锁定 Godot 4.5.1：本机 `AppData/Roaming/Godot/export_templates` 已存 4.5.1.stable 模板，编辑器本体缺失需重装，锁定同版本避免模板重下
- dotnet 10 SDK 默认生成 .slnx，改用 `--format sln` 传统格式，与 Godot 编辑器自动生成的解决方案保持一致
- C# 目标框架 net8.0，LangVersion 12，Nullable enable；渲染用 gl_compatibility（移动端兼容性最好）
- CI workflow 暂不重建（M0 计划外动作），遗留至后续迭代

## 测试结果

- `dotnet build superchess.sln`：0 错误
- `make check`（dotnet format --verify-no-changes + dotnet test）：通过

## 遗留事项

- M0-4/M0-5（Android 导出管线 + 真机验证）待外部资源：Godot 4.5.1 .NET 编辑器重装、JDK 17、Android SDK
- 当前无测试工程（M2 建 tests/Core.Tests），dotnet test 空跑

## 下一轮计划

- 用户安装 Godot 4.5.1 .NET 版后完成 M0-4/M0-5；随后进入 M1 Android 引擎通信 PoC（Go/No-Go 决策点）
