# dual_ontology_world_engine

正式游戏只使用编译 DV 的动作实现：`Prepare → 现场执行/世界法则回执 → Complete → 呈现`。主体决策、费用、效果、观察、传播与结算由 DV 完成，Godot 负责物理移动、输入、画面和音效。

| 目录 | 职责 |
| --- | --- |
| language_core/core、verification/session | 唯一动作机制与主体推演 |
| language_core/game、verification/scenarios | 原生 DV 生产内容 |
| language_core/compiler、Session | DV 编译器、平台 ABI 与序列化边界 |
| godot_csharp | 场景、物理与纯呈现宿主 |
| docs/design-standard.md | 唯一设计规范 |
| artifacts | 封存证据与阶段报告 |

旧 C# Core、旧解释链、旧章节会话和旧 AI 坐席及其运行工具已移除，源码可从 Git 历史查阅。旧 `.story`、译文和历史证据保留作迁移来源。现场移动只有 Main → WorldView.execute_move → 完成回执；没有旧 stage 执行分派。命令行逻辑执行器复用 DV prepare/complete，不替代 Godot 现场验收。

```powershell
dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers -p:NuGetAudit=false
dotnet build language_core/LanguageCore.sln -c Release --disable-build-servers -p:NuGetAudit=false
python language_core/tools/check_dv.py --format json <source.dv>
```

通过环境变量 GODOT 指定 Godot 4.6.1 .NET，正常启动使用 `--path godot_csharp`。构建时可用 `-p:DvPython=<Python路径>` 指定生成器。

工作约束见 [AGENTS.md](AGENTS.md)，机制接口见 [DV Session](language_core/Session/README.md)。禁止流程回归，不以构建结果证明游戏验收。旧一级回放与三级保护测量工具已退役，尚未提供 DV 替代入口；本次清理证据见 [报告](artifacts/partial-bootstrap-core/single-action-path/report.md)。
