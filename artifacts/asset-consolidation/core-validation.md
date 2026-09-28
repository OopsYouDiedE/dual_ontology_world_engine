# Core 与 Godot 宿主架构验证

日期：2026-09-23

## 验证结果

- `dotnet build godot_csharp/DualOntologyWorldEngine.sln`：成功，0 错误，23 个 nullable/未使用变量警告。警告均来自 `godot_csharp/addons/dialogue_manager/DialogueManager.cs` 与 `godot_csharp/addons/dialogue_manager/example_balloon/ExampleBalloon.cs`；Core 项目本次构建未报告警告。
- `dotnet run --project godot_csharp/scenes/Reasoning/check/Reasoning.Check.csproj`：成功。输出通过早餐案未校时反例、词汇门槛、开关对照、节奏复现、重置等检查，以及道本语句法、位格、误伤、分阶段词汇、旧声反例、等价描述、覆盖顺序和原子提交检查。
- `rg` 检查 `csharp/` 的 C#、项目、props/targets 文件：未发现 Godot / Godot.NET 引用。Core 项目是独立 `net8.0` 类库；宿主 `godot_csharp/DualOntologyWorldEngine.csproj` 通过 `ProjectReference` 单向引用 `../csharp/DualOntologyWorldEngine.Core.csproj`。Reasoning 检查项目也直接引用 Core 并链接场景规则源码。这符合 Core 不依赖游戏宿主的边界。
- Godot CLI 可用：本机控制台程序返回 `4.6.1.stable.mono.official.14d19694e`。宿主项目声明 `Godot.NET.Sdk/4.6.1`、`net8.0`，本机 `NuGet.Config` 有 Godot 4.6.1 本地工具包源；宿主 solution 构建成功。

## 导出配置与离线冒烟条件

- `godot_csharp/project.godot` 存在，指定主场景 `res://scenes/Main2D.tscn` 和 Godot 4.6 / C# 特性。
- 未找到 `godot_csharp/export_presets.cfg`，仓库当前没有可用于导出的平台 preset。
- 本机 `%APPDATA%/Godot/export_templates/` 存在但递归内容为 0 个文件。Godot 4.6.1 mono 的安装目录包含编辑器/CLI 及 GodotSharp NuGet 工具包，但没有可见的 export templates；NuGet 工具包不能替代导出模板。
- 因为缺少导出 preset 和 export templates，目前不能进行离线导出冒烟。当前能确认的是 .NET 宿主 solution 可构建；这不证明 Godot 平台导出或导出的游戏可运行。未添加发行配置或模板。

本报告只记录构建、代码边界与本机导出条件；没有改动素材或游戏代码。
