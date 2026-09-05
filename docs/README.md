# 双重本体世界引擎文档索引

## 当前 C# Core

- [当前 C# Core 使用规范](csharp-core-usage-zh.md)：装配入口、状态类型、行动声明、认知、规则、结算、生命周期与可运行代码。
- [C# Core 维护与验收](csharp-core-maintenance-zh.md)：上一轮结构优化、接口迁移及验证记录。
- [Core 语法与维护规则](../godot_csharp/core/AGENTS.md)：C# 12、变量可变性、nullable、对象引用和扩展边界。

使用规范描述现有代码；维护记录保存当时结果，不代表后续版本自动通过相同验证。

## 后续设计

- [长篇成长、持续戏剧张力与长期数值平衡设计和测试方案](long-term-simulation-plan-zh.md)：待实施机制、测试场景、资源审计、长期记录和阶段验收。

首批运行机制已实施：多重欲望、公开提议的合作前瞻、条件机会和实证成长。完整长期叙事设计仍分阶段实施。

- [文学运行机制与验证边界](narrative-runtime-zh.md)：当前接口、运行原则、修复原因与验证范围。
- [低成本 AI 文学案例提案](literary-case-proposals-zh.md)：三个具体文学压力场景。
- [渡船自主案例实测](narrative-case-results.md)：有限资源、人物取舍与信息条件的反事实轨迹。

## AI 构造平衡测试

- [测试使用说明](../godot_csharp/tests/balance_checks/README.md)：规则候选、言灵组合、契约选择、预算矩阵与严格模式。
- [实测结果](ai-construction-balance-results.md)：受限候选的守恒结果，以及尚未关闭的开放式创建风险。

## 运行入口

```powershell
dotnet run --project godot_csharp/examples/resource_village
dotnet run --project godot_csharp/examples/literary_story
dotnet run --project godot_csharp/tests/literary_checks
dotnet run --project godot_csharp/tests/runner -- --verify
dotnet run --project godot_csharp/tests/balance_checks -c Release -- --long
dotnet run --project godot_csharp/tests/narrative_cases -- --output docs/narrative-case-results.md
dotnet run --project godot_csharp/tests/consequence_budget_checks
```

Core 为独立 .NET 8 项目。学院 Simulation 引用 Core，Godot 项目提供表现宿主。
