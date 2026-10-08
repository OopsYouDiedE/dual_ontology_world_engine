# 第一章文案迁移独立验证

本轮执行自动回归及重演等价性验证。独立验证 Agent 未读取 `docs/design-standard.md`，没有修改生产逻辑或提交。一级三份全程记录、二级真实 UI 验收、三级盲玩均未执行；本报告不能替代这些验收，也不评价人物行为合理性、文学质量或游玩体验。

## 结论

- 剧本生成一致性、单进程宿主构建、Core 作用域回归以及 ReplayCheck / SessionCheck / CognitionCheck / LawCheck 均通过。
- 所有 216 组重演与迁移前基线逐项一致：1966 帧、7864 条心声的正文、时序、空间、门状态及全部痕迹、日志、冲突和吻合结果没有差异。
- 两个正式重演入口的结构化事件与标志一致。改写显示文字后，所有痕迹、事件身份和标志仍一致。
- 指定统一回归命令整体失败：其 host 默认构建退出 1 且未打印具体错误。独立单进程回退构建通过，二者分别记录，不能将统一命令记为通过。
- ReplayStage 的真实 Godot 加载未验证成功。缺少字体导入缓存导致依赖加载失败，补做导入时引擎在读取系统证书存储处崩溃；不能因进程返回 0 而记为通过。

## 构建与自动回归

| 项目 | 结果与证据 |
| --- | --- |
| `python godot_csharp/tools/compile_story.py --check` | 冻结后的最后检查通过，7 份输出一致；`final-compile-check.stdout.log`、`.result.json`。 |
| 原始 `dotnet build godot_csharp/DualOntologyWorldEngine.sln` | 失败，退出 1；旧本机 NuGet 源不存在且 nuget.org 无法访问；`post-host-build.stdout.log`、`.stderr.log`。 |
| 最终宿主回退构建 | `dotnet build godot_csharp/DualOntologyWorldEngine.sln --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false -p:NuGetAudit=false` 通过，0 错误、1 处既有 Chapter2 `_confrontIndex` 未使用警告；`final-host-build.stdout.log`、`.result.json`。 |
| `python tools/verify.py --only core scope host` | 已实际执行：core 构建通过，scope 24 项通过，host 默认构建退出 1（0 警告、0 错误，未打印原因）。整体失败；`post-unified-verify.stdout.log`、`.result.json`；分通道原日志另存 `unified-verify-logs/`。 |
| ReplayCheck | 实际运行通过，91 项、0 失败；`post-replay-check.stdout.log`、`replay-check-solution-space.txt`。 |
| SessionCheck | 实际运行通过，输出 89 句、0 失败；`post-session-check.stdout.log`。构建有两处既有 nullable 警告。 |
| CognitionCheck | 实际运行通过，25 项；`post-cognition-check.stdout.log`。 |
| LawCheck | 构建完成后实际运行通过，33 项；`post-law-check-retry.stdout.log`。 |

所有 dotnet 命令使用工作区内 `DOTNET_CLI_HOME`。宿主恢复时将用户目录里已有的四个 Godot 包缓存只读复制到 `scratch/ch1-strings/nuget-packages`，并使用工作区内 HTTP 缓存。没有写用户缓存或改仓库 NuGet 配置。实际 SDK 为已安装的 10.0.401，目标和运行时为 net8.0。

准备基线时的首次普通构建失败、完整快照缺少中文路径的首次构建失败，以及 LawCheck / post probe 在构建结束前提前运行造成的缺少 EXE 失败均保留原日志，随后成功重跑。后者是验证调度失误，不是产品失败。`post-law-check.*`、`post-replay-export.*`、`replay-comparison.*` 为原失败尝试；带 `-retry` 的日志为有效结果。

SessionCheck 使用注入房间和即时演出回执，并含一个 `|| true` 的手稿状态断言，因此只证明其已实际检查的协议路径，不证明所有手稿状态正确或真实 UI 表现。

## 全文与行为一致性

迁移前基线从开始时干净工作区对应的 HEAD `601a0609fb73f73464d46701e5a432e89885812d` 建立独立源码快照，避免并行实现污染。来源清单在 `baseline-source.json`，导出与构建日志在 `baseline-replay-export.*`、`baseline-probe-build-complete.*`。

`baseline-replay.jsonl` 的 SHA256 为 `a437ddf1accb07ce189b147da878cf7db301987fa51cb0d2a4a306de1064e4df`。冻结后的全量输出在 `post-replay.jsonl`。`replay-comparison.json` 递归比较全部旧 JSON 字段，只允许增加字段：216 组、1966 帧、7864 条心声、唯一吻合组合，差异数为 0；连内部 Log 也逐字一致。

这份等价性证据覆盖全部重演组合，不是完整正式连续游玩的 01/02/03 三份记录，也不测真实游玩时长。

## 结构化事件和标志

`structured-entry-checks.json` 实际比较 `NightReplay.Run` 与两个正式入口 `AwakeningContentSession.RunVisibleReplay`、`Chapter1Session.Dispatch(replay)`，覆盖全部 216 组和 1966 帧，共 11558 项检查通过。两入口导出的 `event.kind/who/subject/object/room` 以及 `detective_bolted`、`detective_refused_stranger` 完全一致。

额外核对闩门标志只对应侦探成功完成霍铎请求后的已闩帧；熟人组合不被标成拒绝陌生人。全量输出有 7 组闩门标志为真、18 组陌生人拒绝标志为真，两个标志均实际覆盖真/假分支。

文案稳定性测试在 `scratch/ch1-strings/variant-source` 的独立副本中修改 99 条 `.story` 显示文字，包含全部重演事件、心声以及完整句/句尾文字，保留 `句尾含义` 和其他语义字段，再通过正式 `compile_story.py` 生成。变更清单在 `display-variant-source.json`。

`display-variant-replay.jsonl` 与 `post-replay.jsonl` 比较时，1966 个标签及 7864 条心声实际全部变化，但变量、全部痕迹、冲突、吻合、两标志、帧时序/空间/门状态、事件的五个结构字段均无差异；`display-variant-comparison.json`。变体的两个正式入口再次通过相同 11558 项结构检查；`display-variant-structured-entries.json`。因此这些正式路径的逻辑不再依赖被改写的显示文字。

## Godot 加载边界

已实际用 Godot 4.6.1 .NET 运行 `--headless --path godot_csharp --script res://scenes/Awakening/ReplayStage.gd --check-only`。`APPDATA` 重定向到工作区，日志显式写入本目录。首次与导入后的两次尝试都返回 0，但有 `SCRIPT ERROR` / `ERROR`：`ZhaohuaTypeWriter.ttf` 的 `.fontdata` 不存在，`UITheme.tres` / `Paper.gd` 依赖预加载失败，最终 ReplayStage 编译加载失败。证据为 `replay-stage-parse.*`、`replay-stage-parse-after-import.*` 及相应 `.engine.log`。

补做正式 headless editor `--import` 时输出 `Failed to read the root certificate store`，退出 3221225477，字体缓存未生成；`godot-import.*`、`godot-import.engine.log`。未继续扩大环境修复。导入造成的三份 tracked translation 副作用已恢复开始时的原始字节。

局部静态检查确认 ReplayStage 的到达停留时间读取 `event.kind`，人物高亮读取 `event.who`，控制提示从 `control_texts` 读取；帧 `label` 和心声只用于展示。这不能替代真实依赖加载或画面/交互验证，相关运行项仍未验证。

## 残留及未覆盖

- 本轮只验迁移等价性和明确的结构字段，完整静态审计由主 Agent 的 `implementation-audit.md` 记录。
- Shared Framework 的技术异常、作者预览和 ExampleScript / DetectiveSimulation 的样例、检查输入与日志属于调试/检查路径，应与正式玩家剧情边界分别报告。DetectivePlanning 的 `cue/counter` 文本匹配仅供 DetectiveSimulation 检查工具，本轮保留，没有宣称该检查工具也已完成结构化改写。
- 全量真实 Godot UI、几何/碰撞/遮挡、输入交互、三个验收级别、人物行为合理性、文学质量及真实游玩体验均未覆盖。

临时验证程序和工作区内缓存位于 `scratch/ch1-strings/`；所有持久证据位于本目录。每个命令的 `.result.json` 记录精确参数、退出码和耗时，stdout/stderr 以原始字节保留；本目录 `.gitattributes` 禁用日志与 JSONL 的文本转换。
