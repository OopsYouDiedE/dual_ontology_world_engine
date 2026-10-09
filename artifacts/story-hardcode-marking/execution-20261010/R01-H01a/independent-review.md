# R01-H01a 独立来源传播复验

## 冻结输入与边界

- Worker：`tmp/solution-review-workers/H01a`，分支 `agent/H01a-line-provenance-run01`。
- Base：`7489db56a714724eb7cb735958dd879dc662d238`；审查 tip：`dc63af04213ac0d418b1b598bcd9e025d657eafe`。
- 全新上下文独立检查；未读取项目设计、`.story`、剧情答案、黄金记录、旧试玩、S18 源码或正式检查的 `Program.cs`。未读取实现方报告正文。
- 源码审阅限定原始差异、`WorldBehavior.cs`、`WorldSession.cs`、`WorldSession.Attach.cs`、`InteractionPacket.cs`、`CoreScriptWorld.cs` 及公开项目引用。另读取任务 scope lock、指定 guard 与工作约束。
- 未改生产源码、正式检查或旧预期；新中性工程、精确基线源码副本、外部构建覆盖、原始日志均在本目录。C# 克隆构建输出在锁声明的目录，独立工程产物在本目录且本地忽略；宿主构建的 Python 检查另留下未获锁授权的缓存，详见范围门待办。未删除克隆任何文件。

## 独立语义结论：通过

`raw-diff.log` 中唯一生产改动为候选 `line` 克隆对象增加 `LineId = step.LineId, SourceId = step.SourceId`。两个字段此前已存在于 `DialogueInstruction` 和 `InteractionLine`；`BehaviorCompleted` 此前已把它们传至 `SemanticDialogue`。这是修复加载冻结过程中遗漏现有元数据的确定性拷贝。

未增加字段、格式、能力、AST、动作、回调、采样或候选顺序；没有对白改写或来源编号生成规则。原有声口、情绪、正文及音频字段仍原样拷贝。编号未提供时仍保留空字符串默认值；字符串值从新对象输出，加载后的调用者修改无法改变已冻结行的编号。

`changed-paths.log` 表明 tip 相对 base 只修改上述生产文件并新增授权实现报告。精确基线 `baseline/WorldBehavior.cs` 与 Git base blob 原始字节相同，SHA-256：`980c57ab319f2f9749d4ff66ebcabf0d46fff3f6542ddd6869dd6607ae049c90`。基线构建仅用本目录的 MSBuild 覆盖替换该文件；其他被执行源码使用相同冻结克隆，没有改写克隆。

## 实际执行与证据

| 检查 | 本轮结果 | 证据 |
| --- | --- | --- |
| 冻结 clone 宿主 solution 构建 | 退出 0，0 警告、0 错误；生成物检查通过 | `host-build.log`、`host-build.result.json` |
| 既有 `WorldScriptCheck` 构建 | tip 与基线均退出 0 | `formal-build.log`、`baseline-formal-build.log` |
| 既有正式检查，传入既有中性开发示例 JSON | tip 与基线均退出 0；原始输出逐字节一致 | `formal-with-argument.log`、`baseline-formal-with-argument.log` 及相应 `.result.json` |
| 新中性来源传播检查 | tip 构建/执行退出 0，27 项断言通过 | `neutral/`、`neutral-build.log`、`neutral-run.log` |
| 同一中性检查的基线敏感性对照 | 基线构建退出 0；执行退出 1，19 项通过、8 项来源保留断言失败 | `baseline-neutral-build.log`、`baseline-neutral-run.log` |
| 当前正式第一章编译物候选数量 | 1 章，flat candidates 总数为 0；仅解析数量，未读取正文 | `formal-candidate-count.json` |

正式检查使用的唯一输入参数为克隆的 `godot_csharp/scenes/Shared/Story/Examples/WorldScript/generated_world.json`；只运行检查，未打开该示例或预期源码。最终正确参数检查的两份原始日志 SHA-256 均为 `33a761a075663623720d0d04dc6809d09950389976a223d51bfc7f7c32d3cbbd`。

此前运行失败均已保留，没有以旧结果替代：直接 `dotnet run` 配合外部输出目录时尝试从默认路径启动不存在的 exe（`formal.log`、`neutral.log` 及基线同名日志）；改为先构建再运行实际 DLL 后，无参数正式检查在 `Program.cs:5` 抛出参数越界（`formal-run.log`、`baseline-formal-run.log`，输出完全一致）。随后由控制者提供既有启动参数，才运行上述最终检查。这两类是本次验证启动方式的问题，不能算生产回归或成功检查。

新工程实际调用生产 `WorldScriptDocument.Parse → WorldSession.Load → Process(RequestCandidates / SelectCandidate / AdvanceWorld) → SemanticDialogue`。选择使用正式返回 token；NPC 通过 Core 完成自动 `response_to` 反应和显式 `RequestReaction`，再走正式呈现确认。没有假引擎、正确剧情答案、状态注入或重写正式测试。

中性输入包含多个不同 `LineId` / `SourceId` 的连续行、无编号旧格式行，以及空 `LineId` / 非空 `SourceId` 的行。检查同时比较全部有序行字段，并在 Load 后修改原始行字段、替换 response、清空候选和 response_to，确认会话仍发出冻结的原行。基线仅在来源相关断言失败，动作完成、反应、行数、无编号兼容与冻结行为继续通过；因此检查能实测识别这次遗漏。

## 范围门状态与待修

读取时指定 guard SHA-256 为 `ac6a34ef73bf70d5b4bb2b4bb8ded5cf0441c3184021f567402f46cfc9cbc6c1`，与控制者提供值一致。按指定 tip / lock 启动只读检测，原始日志为 `final-guard.log`，摘要为 `superseded-guard-summary.json`。这次检测实际退出 1 / DENY：`godot_csharp/tools/__pycache__/` 与其中 8 个 `.pyc` 为锁未授权的未跟踪/忽略文件；本轮未删除它们。宿主构建的现有 Python 编译物检查会留下这类缓存，后续构建应设置 `PYTHONDONTWRITEBYTECODE=1`，并由控制者清理本轮相关构建缓存后复验。不能把 Git 短状态空白当作原始字节范围通过。

控制者另通知旧 guard 存在合法 `[]` 字体路径假阳性，新版由另一全新 Agent 对两 clone 最终复验；本报告不把这次旧检测作最终集成门结论，也不把其 DENY 隐去或解释为本轮已通过。最终有效范围门尚待缓存处理与新版本复验。

生产差异无需修正。待办为控制者处理锁外缓存并执行最终新版范围门；本轮沒有 UI、人物行为审美或实际游玩验收。当前正式第一章候选数量为 0，所以此通用候选路径修复对当前 S18 正式内容没有直接执行影响；不据此推断 UI 已对齐或验收通过。一级、二级、三级验收均未执行、未验证。
