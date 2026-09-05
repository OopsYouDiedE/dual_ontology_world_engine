# 双重本体世界引擎文档索引

## 1. 核心规范（Engine Specifications）

- [C# 文学模拟实现与使用规范](./literary-simulation-implementation-zh.md)：当前可运行入口、效果契约、人物交互和验证边界。

- [文学范式、灵异物品与人物模拟设计](./literary-simulation-design-zh.md)：本轮最新设计，确定十二范式、十二物品、默认数值、人物交互和 AI 推演；新入口已实现，长期数值待校准。

- [面向低配置涌现的核心改进方案](./emergence-core-plan-zh.md)：已敲定的下一轮方案，规定效果共用、规则与言灵协作、认知决策、表现及阶段验收；具体落地以文学模拟实现规范为准。

- [引擎公理设定](./engine-axioms-zh.md)：物理—语义—认知底层公理、言灵、规则、理智、异常与身份定理。
- [Entity System 统一模型](./entity-system-model-zh.md)：Entity/Facet 架构、基础属性、Knowledge 观察采信、Aspect 投影与词表。
- [回合调度、规则与言灵模型](./turn-and-kotodama-model-zh.md)：嵌套行动值 Timeline、Intent 模型、社交情绪组合、规则评估与言灵契约。
- [Python / C# Core 替换规范](./python-csharp-core-replacement-standard-zh.md)：共享 Core 边界、状态唯一权威源、ActionRuntime 管线与跨 Core 验收轨迹。
- [模拟架构与扩展规范](./simulation-architecture-zh.md)：核心与游戏层边界、可组合模拟机制、扩展约定及跨语言适配方向。

当前 C# 实现主线的项目边界为：`godot_csharp/core/DualOntologyWorldEngine.Core.csproj` 是独立的 .NET 8 Core；学园 Simulation 是仅引用 Core 的独立项目；Godot 根项目承担表现宿主职责。Python 仍是参考实现，尚未同步本轮 `SimulationSession` 扩展。

---

## 2. 内容层设计与推演轨迹（Content & Trajectory）

- [异常感知学园设计规范](./academy-design-zh.md)：游戏起点、200 AV 时间调度、群像定向认知、七类反应决策树与前序事件规则树。
- [五日推演规范化轨迹](./five_days_trajectory.md)：历史学园样例的前五日审计日志与状态转移轨迹（保留的历史样例基准）；该文件不能单独证明通用模拟能力或 Python/C# 跨语言一致性。

命令行验证与独立示例入口：

```powershell
dotnet run --project godot_csharp/tests/runner/runner.csproj -- --verify
dotnet run --project godot_csharp/examples/resource_village/DualOntologyWorldEngine.ResourceVillage.csproj
```

资源村示例仅引用 Core，通过有限资源竞争展示行为如何发生变化。
