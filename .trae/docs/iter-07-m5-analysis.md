# iter-07 M5 分析模式（MultiPV + 箭头 + 评估显示）

## 需求清单

- M5：AnalyzeAsync（MultiPV 3~5）、ArrowLayer（历史箭头 + 建议箭头 + digit 角标）、评估显示（设计 `.trae/designs/godot-xiangqi-android.md` M5 节，AC-5）

## 迭代目标

- `GameSession.AnalyzeAsync`：MultiPV 分析并返回每路最新评估
- `ArrowLayer.cs`：移植 ArrowShape 多边形绘制，历史箭头渐隐 + 建议箭头 digit 角标
- HUD：分析按钮、当前分值、历史评估列表

## 改动文件

- 新增：Scripts/UI/ArrowLayer.cs
- 修改：Scripts/Game/GameSession.cs（AnalyzeAsync + DefaultAnalyzeDepth/MultiPv 常量）、Scripts/UI/HudPanel.cs（分析按钮 + 评估区 + SetEval/AppendEval/ClearEval）、Scripts/Main.cs（分析接线 + 历史箭头 + 分值换算）、Scenes/Board.tscn（Arrows 节点）、tests/Game.Tests/GameSessionTests.cs（2 例分析用例）

## 关键决策

- AnalyzeAsync 复用忙闲门闸与代际计数；InfoReceived 逐路保留最新 info（引擎逐层刷新，低层不覆盖高层），bestmove 后排序返回
- 箭头多边形移植 ArrowShape.getTransformedPath 数学（局部 +x 轴指向目标、60°/120° 角参数），DrawColoredPolygon 绘制于棋盘原生坐标系
- 建议箭头按排名 5 色分级（红→橙→金→青→蓝），头部叠加 digit1-5 角标（缩放 0.55）；历史箭头蓝色、0.4s 延迟后 1.6s 渐隐
- 分值统一换算为红方视角显示（走子方视角 × ±1），mate 显示 #N/-#N；评估列表行格式「手数. 建议着法（中文记谱） 分值」
- 分析按钮在引擎不可用时禁用（SetMode 联动）

## 测试结果

- `make check`（dotnet format --verify-no-changes + dotnet build + dotnet test）：通过
- Core.Tests 44 例 + Game.Tests 12 例全绿（新增：MultiPV 逐路保留最新并排序、搜索参数下发正确；思考中分析被拒）
- headless 冒烟：主场景 30 帧无报错（ArrowLayer 纹理加载 + HUD 评估区构建）

## 遗留事项

- 分析模式真机观感（箭头粗细/角标位置/列表布局）待真机回归（M6 AC-5）
- UciSession 对真实 Pikafish 端到端验证待真机（桌面无 arm64 引擎）

## 下一轮计划

- M6 发布打磨：FEN 剪贴板导入导出、关于页（MIT + GPL-3.0 声明）、Android 签名导出与真机回归、收尾
