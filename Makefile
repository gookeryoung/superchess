# Makefile - superchess（Godot 4 .NET 版）快捷命令
# 运行 `make help` 查看所有可用命令

SOLUTION := superchess.sln

.PHONY: help restore build clean test lint format check push

help: ## 显示帮助信息
	@powershell -NoProfile -Command "Get-Content Makefile | Select-String '^\s*[a-zA-Z][\w -]*:.*?##\s*(.*)' | ForEach-Object { if ($$_.Line -match '^([\w -]+):.*?##\s*(.*)') { '{0,-12} {1}' -f $$Matches[1].Trim(), $$Matches[2] } }"

restore: ## 还原 NuGet 依赖
	dotnet restore $(SOLUTION)

build b: ## 构建解决方案
	dotnet build $(SOLUTION) -c Release --nologo

clean c: ## 清理构建产物
	dotnet clean $(SOLUTION) --nologo
	@if (Test-Path .godot) { Remove-Item -Recurse -Force .godot }

test: ## 运行全部测试
	dotnet test $(SOLUTION) --nologo

lint: ## 代码风格检查（不改动文件）
	dotnet format $(SOLUTION) --verify-no-changes

format: ## 自动修复代码风格
	dotnet format $(SOLUTION)

check: lint test ## 运行全套门禁（format 校验 + 测试）

push: ## 推送代码到所有远程仓库（含标签）
	@powershell -NoProfile -Command "git remote | ForEach-Object { Write-Host ('推送 ' + $$_.ToString() + '...'); git push $$_.ToString(); git push $$_.ToString() --tags }"
