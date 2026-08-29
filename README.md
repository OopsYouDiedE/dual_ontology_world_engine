# 物理—语义二重本体论世界模拟引擎

本项目使用 Godot 4.6 与 C# 开发。`docs/engine-axioms-zh.md` 是世界规则的规范来源。

## 当前结构

- `Core/IActionUnit.cs`：循环行动值中的行动能力接口。
- `Core/Entity.cs`：具有运行时身份的实体与可堆叠物品。
- `Core/Space.cs`：不包含三维坐标的语义区域拓扑。
- `Core/Rules.cs`：不可变的规则、效果、指令和言灵 AST。
- `Core/RuleParser.cs`：受控英文规则解析器与 AST 格式化。

核心类型是普通 C# 对象，不继承 Godot 的 `Node` 或 `Resource`。三维坐标、碰撞、导航和场景表现由后续 Godot 适配层负责；核心只处理具有规则意义的空间关系和查询结果。

## 构建

```powershell
dotnet build
```
