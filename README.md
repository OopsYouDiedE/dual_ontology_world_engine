# 双重本体世界引擎

当前 C# Core 使用 Essence–Projection 模型。Essence 表示客观对象，Projection 表示现实表现及主体认知；Soul 只依据自身内部 World 决策，认知更新统一经过 Observe。

核心为独立 .NET 8 / C# 12 项目，支持按行动值延迟激活和嵌套行动。不包含房间内几何、碰撞、实时循环或 UI。

## 本轮验收

```powershell
dotnet build godot_csharp/Core.sln -c Release
dotnet run --project godot_csharp/tests/core_acceptance/CoreAcceptance.csproj -c Release
pwsh -File godot_csharp/measure-core.ps1
```

- [迁移说明与验收边界](docs/core-migration-acceptance-zh.md)
- [当前使用示例](docs/core-essence-projection-usage-zh.md)
- [Core 维护规范](godot_csharp/core/AGENTS.md)

## 目录

- `godot_csharp/core`：正式核心，包括对象与观察、AST、CTB、决策、社交及结算。
- `godot_csharp/tests/core_acceptance`：新核心独立因果场景。
- `godot_csharp/Core.sln`：不依赖 Godot 的构建入口。
- `godot_csharp/DualOntologyWorldEngine.sln`：包含 Godot 宿主的解决方案。
- `godot_csharp/legacy`：迁移前核心与实验核心，保留供后续对照。
- `godot_csharp/examples`：现有动物园程序仍依赖 legacy；验收之后迁移其语义与展示。

docs 目录中原有的 Entity / Belief 规范属于旧实现。当前 API 以本页链接的使用示例及正式 Core 为准。
