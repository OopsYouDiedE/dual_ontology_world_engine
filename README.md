# 双重本体世界引擎

双重本体世界引擎以实体、单时间线行动值调度、Soul 决策、规则与言灵契约为核心。当前实现主线为 C#：Core 是独立的 .NET 8 项目，Simulation 仅引用 Core，Godot 根项目作为表现宿主；同时维护 Python 参考实现。

核心不计算房间内几何距离与碰撞，空间交互交由游戏层处理。跨场景移动依拓扑图寻路并支付通行代价；所有权、绑定、作用域与规则约束均保持客观闭合。

C# Core 的当前接口与维护约定见 [Core 维护与验收](docs/csharp-core-maintenance-zh.md)及 [C# 语法规范](godot_csharp/core/AGENTS.md)。

新游戏从 [当前 C# Core 使用规范](docs/csharp-core-usage-zh.md) 开始。文学模拟入口提供默认规则范式、灵异物品和人物交互。后续扩展见 [长篇成长、张力与平衡设计和测试方案](docs/long-term-simulation-plan-zh.md)，其中部分长期机制已由[四人与恶魔场景](docs/long-term-campaign-results.md)验证，尚未接入原学院五日流程。规范见[长程平衡数值表设计规范](docs/long-term-balance-standard-zh.md)和[核心使用规范](docs/csharp-core-usage-zh.md)。

```powershell
dotnet run --project godot_csharp/examples/literary_story/DualOntologyWorldEngine.LiteraryStory.csproj
dotnet run --project godot_csharp/tests/literary_checks/literary_checks.csproj
```

## 目录结构

- `godot_csharp/core/DualOntologyWorldEngine.Core.csproj`：独立 .NET 8 Core 项目，包含 Entity、Mind/Soul、Kotodama、Rule、Social、Timeline 等引擎基础。
- `godot_csharp/simulation/`：独立学园 Simulation 项目，仅引用 Core，负责群像动机、自主决策与日常日程模拟。
- `godot_csharp/presentation/`：Godot 4 荒岛学园 2D 表现层；Godot 根项目是表现宿主（`scenes/smallville_demo.tscn`）。
- `godot_csharp/tests/runner/`：独立命令行仿真运行入口。
- `python/`：Python 核心与原型参考实现。
- `docs/`：引擎底层公理、数据模型、调度管线与设计规范。

## 运行与推演

### 1. C# 命令行推演（.NET 8）

全自动执行五日推演并生成轨迹：
```powershell
dotnet run --project godot_csharp/tests/runner/runner.csproj -- --auto
```

进入交互控制台接管角色决策：
```powershell
dotnet run --project godot_csharp/tests/runner/runner.csproj -- --interactive
```

执行无输出覆盖验证：
```powershell
dotnet run --project godot_csharp/tests/runner/runner.csproj -- --verify
```

运行独立资源竞争示例（仅引用 Core；有限资源竞争会改变角色行为）：
```powershell
dotnet run --project godot_csharp/examples/resource_village/DualOntologyWorldEngine.ResourceVillage.csproj
```

### 2. Godot 4 场景展示

使用 Godot 4 (Mono/C# 版) 打开 `godot_csharp/project.godot` 并启动主场景。
- `Space`：暂停或继续。
- `1` / `2` / `3`：切换推演速度（0.5x / 1x / 2x）。
- `T`：显隐移动轨迹。
- `R`：重置模拟。

### 3. Python 参考实现

Python 用于参考与原型验证，尚未同步本轮 C# `SimulationSession` 扩展。

```powershell
pip install -r requirements.txt
python -m python
```

## 文档索引

- [文档总索引](docs/README.md)
- [引擎公理设定](docs/engine-axioms-zh.md)
- [模拟架构与扩展规范](docs/simulation-architecture-zh.md)
- [Entity System 统一模型](docs/entity-system-model-zh.md)
- [回合调度、规则与言灵模型](docs/turn-and-kotodama-model-zh.md)
- [Python / C# Core 替换规范](docs/python-csharp-core-replacement-standard-zh.md)
- [异常感知学园设计规范](docs/academy-design-zh.md)
- [五日推演规范化轨迹](docs/five_days_trajectory.md)：历史学园样例轨迹，用于审计与回归参考，不能单独证明通用模拟能力或 Python/C# 跨语言一致性。


## 长期运行与动物园验证

独立核心长程回归：

```powershell
dotnet run --project godot_csharp/tests/long_run_checks/long_run_checks.csproj -c Release
```

动物园怪谈的无守则场景保留异常机制，另设关闭异常的对照组。模型解释与测试范围见[动物园无守则测试](docs/zoo-no-rules-validation-zh.md)。

```powershell
dotnet run --project godot_csharp/tests/zoo_checks/zoo_checks.csproj -c Release
```

