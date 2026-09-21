# 双重本体世界引擎

当前 C# Core 使用 Essence–Projection 模型。Essence 表示客观对象，Projection 表示现实表现及主体认知；Soul 只依据自身内部 World 决策，认知更新统一经过 Observe。

核心为独立 .NET 8 / C# 12 项目，支持按行动值延迟激活和嵌套行动。不包含房间内几何、碰撞、实时循环或 UI。

## 构建与验证

```powershell
dotnet build csharp/Core.sln -c Release
dotnet build godot_csharp/DualOntologyWorldEngine.sln
```

- [Core 维护规范](csharp/AGENTS.md)

## 目录架构

- `csharp/`：正式核心引擎，包括对象与观察、AST、CTB、决策、社交及结算。独立于游戏引擎。
  - `csharp/Core.sln`：核心独立构建入口。
  - `csharp/sample/`：核心下属示例与演示合集。
    - `csharp/sample/web/`：网页展示资源；子目录 `dist/` 为发布的网页。
    - `csharp/sample/godot_scenes/`：Godot 机制演示场景与脚本。
    - `csharp/sample/zoo/`：动物园机制模拟与 Web 演示。
- `godot_csharp/`：未来游戏的核心工程目录，采用 2DRPG（48px TileMap + 2D Sprites）架构，作为《异常感知学园》游戏层宿主接入核心引擎。
  - `godot_csharp/DualOntologyWorldEngine.sln`：游戏主解决方案。
  - `godot_csharp/project.godot`：Godot 游戏配置。
  - `godot_csharp/ShareAsset/rpg2d/`：2DRPG 规范资产（瓦片集、交互预制体、21位新角色像素母版）。

docs 目录中原有的 Entity / Belief 规范属于旧实现。当前 API 以本页链接的使用示例及正式 Core 为准。
