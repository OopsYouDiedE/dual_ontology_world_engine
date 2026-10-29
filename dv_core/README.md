# DV Core

`dv_core` 保存共用 C# 机制、会话实现、CLI 宿主和章节 DV 内容。Godot 宿主位于仓库根目录的 `godot_csharp`，两个宿主使用同一套 Core 和 Session 程序集。

## 目录

| 路径 | 职责 |
| --- | --- |
| `Core/` | 共用 C# 机制，包括 DV 解析、内容校验、基础值、证据、世界状态、认知、通信、规划与调度。 |
| `Session/` | 共用会话、章节装配、配置、投影、行动派发、内容包加载与回放。 |
| `Cli/` | 正式 CLI 宿主和输入输出协议。 |
| `content/` | 内容包清单、章节数据、拓扑、模板、采纳目录与宿主配置；所有 `.data.dv` 文件直接放在此目录。 |
| `Directory.Build.props` | 将 Core、Session 和 CLI 的构建产物放到仓库 `.runtime/build/`。 |

三个 C# 项目的维护文件分别平铺在项目目录内。`Core/ValueOperations.cs` 提供 DV 值、列表与记录操作，`Core/RecordStorage.cs` 负责底层记录查询、缓存与更新；这两个文件保留原有类型和方法。

三个程序集依赖方向为 `DuelView.Cli → DuelView.Session → DuelView.Core`。Godot 项目引用 `DuelView.Session`。目录调整保留现有程序集名称、命名空间和 DV 标识。

## 内容来源与发布

世界、人物与呈现共用的 `world.dv` 继续维护在 `godot_csharp/Content/world.dv`。该文件同时由 Godot 资源读取入口和共用会话使用。章节内容维护在 `dv_core/content/`。

Session 项目将 `world.dv` 与 `content/*.data.dv` 一同发布到程序集输出目录的 `Content/`。章节源文件集中在一个目录，发布后的文件名保持不变；新增文件应使用唯一文件名。完整内容包由清单和定义共同校验，两个宿主使用相同内容。章节数据、模板、采纳目录与人物认知初值的结构变更要求重新开局。

## 构建与启动

以下命令从仓库根目录运行。环境准备由 `dual-ontology-environment.prepare` 完成，Godot 使用 .NET 版本。

```powershell
dotnet build dv_core/Cli/DuelView.Cli.csproj --nologo
dotnet build godot_csharp/DualOntologyWorldEngine.csproj --nologo
```

人工游玩在 Godot .NET 中打开 `godot_csharp/project.godot`，运行项目主场景并从标题界面开始新局。IDE 的完整解决方案仍为 `godot_csharp/DualOntologyWorldEngine.sln`。

Core、Session 和 CLI 的默认构建产物位于 `.runtime/build/bin/<项目名>/<配置>/net8.0/`，中间文件位于 `.runtime/build/obj/<项目名>/`。正式 CLI MCP 为每个会话构建独立输出到 `.runtime/hosts/`。Godot 项目的托管输出继续由 Godot SDK 管理。

## 正式测试入口

| 服务 | 操作 | 用途 |
| --- | --- | --- |
| `dual-ontology-input` | `on`、`step`、`observe`、`close` | 人工或代理逐步操作；`backend` 选择 `cli` 或 `godot`。 |
| `dual-ontology-auto` | `CliMCP`、`GodotMCP` | 引擎 AI 自动运行；设置 `duration_seconds` 或带时区的 `deadline`。 |
| `dual-ontology-environment` | `prepare` | 准备项目使用的 Godot .NET 和 .NET SDK。 |

以上服务的实现位于 `mcp/game/server.py`，分别以 `--service input`、`--service auto`、`--service environment` 通过 MCP stdio 启动。使用项目 Python 环境 `mcp/game/.venv/Scripts/python.exe`。

Godot 输入会话从标题界面开始。调用 `on(backend="godot")` 后，使用 `step` 提交 `{"type":"start"}` 开始新局。后续选择使用最新观察返回的 `choice_id`；键盘操作使用 `input_action`，点击使用 `click`。`busy=true` 时等待动作完成，`reading=true` 时按实际可见界面阅读和继续。同一会话串行操作，结束调用 `close`。

CLI 输入会话支持 `select` 与 `advance`。CLI 的成功记录只证明其实际运行的会话和规则路径。Godot 的渲染、运动、碰撞与阅读状态使用正式 Godot 新局验证。构建成功、达到测试时限或只进入某个场景都不代表章节通关。

正式 CLI 自动测试将逐步操作写入 `.runtime/runs/<run>/actions.yaml`，将运行结果写入同目录的 `report.yaml`。记录保留宿主、决策者、实际动作、游戏进度、耗时和结束状态。CLI 不再额外落盘逐次请求 JSON、动作 JSON 或报告 JSON。Godot 回执继续按实际宿主的正式入口保存。

CLI 宿主为所有新局生成简洁游玩记录 `transcript.txt` 和详细日志 `details.txt`。简洁记录按实际动作完成顺序，只记录玩家主动动作、玩家实际接收到的发言、叙述和动作反馈。人物名字使用正式呈现中的玩家所知名字。不可见活动不写入简洁记录。同一完成事件只记录一次，面板刷新不重复发言。动作未成功时单独标注。

详细日志保留每次显示的正文、场景、全部选项、AI 评分、实际选择及执行结果。`panels.yaml` 保存对应的完整显示字段和选择记录。相同状态的重复观察不重复写入。批量推进中的每次动作完成均记录。每段立即刷新到文件，截止时保留已写完的记录。

自动测试的显示记录与 `actions.yaml` 位于同一运行目录。玩家输入会话默认保存在 `.runtime/runs/cli-input-<标识>/`。正式 CLI 观察和关闭响应通过 `transcript_path` 返回简洁记录路径，通过 `detail_log_path` 返回详细日志路径。引擎 AI 的选项使用当前视角的正式观察投影，选择标注为引擎已准备执行。人工和大语言模型使用玩家输入面板，记录实际提交的选项。提交失败另记错误；提交记录不表示动作执行成功。当前面板没有对应选项时直接标注，不补造选项编号。详细日志包含截止前后的真实写入时间，自动测试的有效覆盖仍以报告中的截止时间和回执为准。

MCP 在打开 CLI 时传入 `decision_maker`，分别标注 `engine-ai`、`agent`、`llm` 或 `human`。直接使用 CLI 协议可以传入同名字段。没有指定且控制器为 `player` 时记录为 `player`，不推断人工或大语言模型身份。已启动的 MCP 服务需要重新加载 Python 代码才会传入新增标记；显示记录本身由每次新构建的 CLI 宿主生成。

CLI 在决策面板中增加 `ai_candidates`，显示总分最高的 5 项，同分保持原候选顺序。引擎 AI 模式读取当前视角实际决策事件中保存的候选、路径、总分和分数组成。人工或 Agent 的待输入面板调用同一 Core 评分器生成当前可选项的参考分数，标注为 `current-panel-reference`，不改变输入或选项顺序。参考分数不表示引擎 AI 的最终选择。没有保存组成的决策标注缺失，不事后补算成历史评分。

评分明细保留评分器提供的 `information_component`、`progress_component`、`time_penalty`、`components` 和公式。`components.sims_autonomy` 是加权前的原始值，不能直接与其余分项相加。实际加权结果已经计入 `progress_component`。`panels.yaml` 保存原始分数；可读记录用于检查显示和选择顺序。

详细日志中的可读评分使用中文摘要，总分和贡献值向零截去小数，正数显示为 `+25`，负数显示为 `-3`，零显示为 `0`。主要贡献按正负分别筛选，保留绝对值达到该方向最大贡献 80% 的项目，各最多 3 项。截断后为零的贡献省略。摘要只列主要项，不要求显示项之和等于总分。候选排序仍使用原始分数。生活动机使用进展总分扣除其他已记录进展分项的实际贡献，不将加权前的原始值计作分数。结构化记录保留完整精度和全部分项。

玩家输入分别记录提交和最终接纳，实际动作完成后记录执行结果。引擎 AI 记录实际准备执行的最终选择及执行结果。`on` 可传 `decision_maker` 指定 `human`、`agent`、`llm` 或 `player`，CLI 的 `select` 也可传入同名字段逐次指定。引擎自动测试固定标注 `engine-ai`。Agent 输入会话默认标注 `agent`。这些身份标记记录输入来源，不参与规则和评分。旧 MCP 服务重载后提供新增输入字段。

## 目录维护

维护源码分别放在 `Core`、`Session`、`Cli`，剧情与内容声明放在 `content`。`bin`、`obj` 和旧编译链产物不进入维护源码目录。历次目录调整的备份保存在 `.runtime/migrations/`。本次扁平化的 `dv-core-flat-20261028/migration.json` 记录逐项路径、编辑前后哈希、备份位置与恢复状态。

机制变更遵守仓库根 `AGENTS.md`。技术设计见 [32 技术路线](../docs/32%20技术路线.md)。
