# iter-04 M2 规则核心库（纯 C# + xUnit）

## 需求清单

- M2：纯 C# 规则核心库（零 Godot 依赖）+ xUnit 测试工程，perft 对拍（设计 `.trae/designs/godot-xiangqi-android.md` M2 节，AC-1/AC-7/AC-8 单测抓手）

## 迭代目标

- `Scripts/Core/`：Board/Fen/Move/Rule/ChineseNotation 五个模块 + Position/Piece 基础类型
- `tests/Core.Tests/` xUnit：perft(1..3) + 规则/FEN/记谱用例
- perft 参考值第三方交叉验证后固化

## 改动文件

- 新增：Scripts/Core/Position.cs、Piece.cs、Move.cs、Board.cs、Fen.cs、Rule.cs、ChineseNotation.cs
- 新增：tests/Core.Tests/Core.Tests.csproj（链接 Core 源码编译，不依赖 Godot）及 PerftTests/FenTests/RuleTests/ChineseNotationTests
- 修改：superchess.sln（加入测试工程）、superchess.csproj（补 ImplicitUsings enable，既有 Engine 代码本就依赖隐式 using）

## 关键决策

- 棋盘 `int[10,9]`（y 行 x 列，原点左上），棋子编码沿用参考项目 Piece.java（1-7 红、8-14 黑）；FEN 沿用 6 字段格式，解析宽松兼容「棋盘 走子方」短格式
- FenFormatException 携带 FieldName（字段数/棋盘/走子方/回合数/半回合计数）+ 原因，满足 AC-8；棋盘校验含 10 行 9 列、字符合法、双方各一将帅且在九宫内
- 合法着 = 伪合法着（偏移表）过滤「走后被将军」与「走后将帅照脸」；将死与困毙分离为 IsCheckmate/IsStalemate（参考项目 isJiangShuaiDead 未查照面，此处更正）
- **车炮攻击将帅探测**（移植 attackableByJuPao 陷阱）：以「被攻击方颜色」的同类棋子做探针生成伪合法落点，再检查落点是否为攻击者——若以攻击者颜色探针会因同侧判定漏检；将帅须留在原位参与同侧判定
- perft(1)=44、(2)=1920、(3)=79666 已与 pyffish（Fairy-Stockfish 0.0.90 xiangqi variant）交叉验证一致后固化（设计第 14 步要求）
- 中文记谱移植 getChsString：红中文/黑阿拉伯数字、进退平方向、前/后/中兵消歧；多兵分居多线全盘编号未实现（沿用参考项目局限，注释标明）

## 测试结果

- `make check`（dotnet format --verify-no-changes + dotnet test）：通过
- 44 个用例全绿：perft(1..3)、蹩马腿/塞象眼/过河兵/九宫/白脸将遮挡/将军（车屏/马腿）/将死/困毙、FEN 往返与 13 类非法 FEN、记谱 9 例（含前后中兵与黑方阿拉伯数字）

## 遗留事项

- M1-7 真机 PoC 验证仍待设备连接（不阻塞 M3）

## 下一轮计划

- M3 棋盘 UI 与交互：资产导入、BoardView/BoardInput 渲染与两段式选子落子、音效
