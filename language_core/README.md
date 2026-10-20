# DuelView 独立语言 Core

DuelView 简称 DV，源码后缀为 `.dv`。领域算法、主体推演和原生章节内容由 DV 编译为 C#；Godot 通过 `DvSession` 使用同一正式动作链。这里的源码与工程说明不代替实际游戏验收。

## 当前文件与依赖

| 文件或目录 | 职责 |
| --- | --- |
| [10-compiler.dv](10-compiler.dv) | 编译器前端、名称/基本名义类型检查与直接 C# 后端；自身仍使用 S 表达式启动语法 |
| [20-core.dv](20-core.dv) | 数据、世界、观察、传播、价值、费用、论证与有限域刻画 |
| [30-session.dv](30-session.dv) | 主体认知、规划、话语、付费重演、调度和 Prepare/Complete |
| [40-chapter1.dv](40-chapter1.dv) | 第一章原生内容与认知配置 |
| [50-chapter2.dv](50-chapter2.dv) | 第二章原生内容、目标、对话与空间声明 |
| [60-game.dv](60-game.dv) | include 20/30/40/50，组合生产、宿主呈现与 `dvg_debug_run` |
| [70-checks.dv](70-checks.dv) | include 60 的独立只读因果检查、oracle、夹具与报告；仍有启动语法报告函数及旧 JSON 输入接口 |
| [compiler/](compiler/README.md) | `seed/Program.cs`、`Seed.csproj` 与基础值/容器/文件 ABI `Runtime.cs` |
| [Session/](Session/README.md) | C# 宿主与序列化边界，构建时生成 `obj/dv/DVProgram.cs` |
| [tools/](tools/) | 共用构建、编辑器诊断、正式逻辑运行及二进制证据工具 |
| [editors/](editors/AGENTS.md) | VS Code 与 Visual Studio 2022 客户端；Godot 客户端在宿主插件目录 |

`tools/dv_toolchain.py` 解析相对 `#include`，保留各源文件的 define 作用域与位置边界。它以 `compiler/seed/Seed.csproj` 为 C0，依次用 `10-compiler.dv` 生成 C1、C2、C3，并比较 C2/C3 的 C# 源码。`tools/build_session.py` 默认将 `60-game.dv` 的闭包交给 C3，生成由 Session 项目引用的类库。`70-checks.dv` 不被生产入口反向包含；seed、Runtime、检查源码与诊断恢复工具都具有独立用途。

## 构建与工程检查

在仓库根目录运行：

```powershell
dotnet build language_core/LanguageCore.sln -c Release --disable-build-servers -p:NuGetAudit=false
python language_core/tools/build_session.py --out language_core/Session/obj/dv
python language_core/tools/check_dv.py --format json language_core/30-session.dv
```

首条命令构建 Seed 与 Session；第二条只生成可检查的生产 C# 源码，并在需要时构建自举编译器。Session 项目本身也会调用生成器。生成物存于 `obj`，不手工修改；可用 `-p:DvPython=<Python路径>` 为 MSBuild 指定 Python。环境要求 .NET 8 SDK 与 Python 3，宿主要求见仓库根 README。

[编译组配置](dv-check.json) 依次声明编译器、生产入口和独立检查入口。编辑 20–60 时选择生产闭包，编辑 70 时才加入检查/oracle；编辑 10 时单独检查启动源。配置只决定静态诊断的源集合，不执行世界或会话。组内其他文件从磁盘读取，当前文件可以来自未保存缓冲区。

[VS Code 客户端](editors/vscode/README.md)、[Visual Studio 2022 客户端](editors/visualstudio/README.md) 和 [Godot 插件](../godot_csharp/addons/dv_checker/README.md) 复用 `tools/check_dv.py` / `tools/dv_lsp.py`。当前提供编译诊断，尚无完整证明状态、自动补全或跨文件定义导航；协议检查、插件安装与实际编辑器呈现须分别验证。

## 正式逻辑与证据

只有任务要求实际逻辑会话时，使用：

```powershell
python language_core/tools/run_debug.py --out artifacts/partial-bootstrap-core/<新目录> --timeout <本轮授权秒数>
```

该入口调用 `60-game.dv` 的 `dvg_debug_run`，以 `formal-model` 执行同一 Prepare/Complete 和连续章节扩展。它不包含 Godot 的几何、碰撞、输入与画面；达到时域、实际剧情达成和完整封存分别报告。测试授权与上限以当前用户指令及 [AGENTS.md](AGENTS.md) 为准，不沿用历史任务窗口自动启动会话。

新原始事件、状态、存档和投影使用 `.dvdag` / `.dvsave` / `.dvlog`。`tools/binary_evidence.py`、`tools/raw_dag.py` 校验二进制记录；`tools/debug_checkpoints.py` 与 `tools/DebugCheckpointResume.cs` 保留实际阶段来源的恢复能力。旧 JSON 记录只作只读兼容，JSON 仍用于进程内接口与编译配置。人工游玩的精简审计策略和完整逻辑证据分别说明，不能据精简输出宣称完整审计。

## 实现范围与文档

20–60 使用 surface-v2 的可执行表层，10 保留启动语法，70 同时含表层与启动语法。因此不能声称七个文件都已彻底消除 S 表达式。已接入缩进、普通调用、记录/列表、中缀运算、单绑定列表推导、mapping、acceptance 和有限域 imprint；完整泛型、单位推导、能力类型、闭包及目标无关 Typed IR 仍属设计范围。

[docs/syntax.md](../docs/syntax.md) 是现行语法入口，[SYNTAX.md](SYNTAX.md) 明确区分执行子集与完整语法方案，[DESIGN.md](DESIGN.md) 维护工程架构与不变量，[REFERENCES.md](REFERENCES.md) 保留研究来源。剧情、人物和玩法只依据 [设计规范](../docs/design-standard.md)。已移除目录及历史任务记录从 Git 历史追溯，不作为当前入口或本轮验收证据。
