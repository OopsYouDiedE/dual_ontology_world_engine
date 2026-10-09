# Core / Shared / Host / UI 静态源码审计（切片 A）

- 基线：`4aca6644449cd00ba5f5521ad086af54b31dd225`
- 仓库：`C:\Users\iii\Desktop\dual_ontology_world_engine`
- 范围：`csharp/` 手写生产 `.cs`；`godot_csharp/scenes/Shared/Story/`、`Shared/Game/`、`Host/`、`UI/` 手写生产 `.cs/.gd`。
- 方法：只读静态审计；按专案名/人名/实体、剧情词汇、字符串常量及分支条件检索，再阅读高风险命中上下文。未运行构建、游戏或测试；未改动产品。
- 工作区初始状态：分支 `claude/blind-main-20261009`；3 个既存修改文件均为 `godot_csharp/scenes/Awakening/Script/generated_lines_zh.{scene.translation,speaker.translation,zh.translation}`，未触碰。
- 使用 AGENTS：根目录、`csharp/AGENTS.md`、`godot_csharp/AGENTS.md` 仅用于流程/范围；未读取 `docs/` 或历史剧情评审。

## 结论

**存在明确的专案固定因果模型及重演行为；Core 的 Hall 专用机制已处于生产编译范围，但本切片没有找到生产调用方。** `HallCausalityRules` 把某一固定门、木楔、配重、烟、双锁、包纸转印与止挡擦拭模型直接编入 Core；`HallReplayProgram` 将“音乐家走到隔离门、等待热烟、按烟状态分支”的流程和中文输出写死。两者名称、领域实体、动作/假设枚举与自然语言因果说明均非通用引擎抽象。

静态证据支持“在 Core 程序集中编译”：`csharp/DualOntologyWorldEngine.Core.csproj` 使用 SDK 默认源码包含，且仅显式排除两个 RegressionProgram；这两个 Hall 文件没有被排除。引用搜索只发现 Hall 两类之间的互相调用，没有 `godot_csharp` 或其他生产路径的调用点。因此报告区分“已编译进 Core”与“已发现正式调用”：前者是，后者未发现；不据此推断它运行时是否可达。

## 逐项发现

### A1. Core 固定门厅因果/证据模型（类别 2，含固定中文输出）

- 文件：[csharp/HallCausalityRules.cs](../../../csharp/HallCausalityRules.cs)
- 关键位置：行 3–40 定义 `HallCausalityRules`、`Hypothesis`、`Action`、`ConflictKind` 和结果结构；枚举直接固定门厅案的假设与动作（内楔、外侧双锁、楔缝、持续烟路、外侧清理；烟、门、包楔、内销、止挡等）。
- 行 42–58 `TestHypothesis` 固定检验外开门受力、门两侧锁具、楔缝成人通行、撤楔后烟路关闭、外侧人员触及内侧清理的因果约束；返回的解释直接写有“生活翼”“主楼”“木楔”“灰迹”等专案内容。
- 行 61–205 `Simulate` 维护 `braced/smoke/wedgeRemoved/bolt/wrapped/moved/wiped/outerBar` 等专案状态，按固定动作顺序产生固定 trace ID 与中文痕迹/失败文本。代表例：行 75–82 木楔遮蔽和楔面烟灰；行 147–164 门两侧锁具与包纸内外转印；行 170–202 移止挡后擦拭带及专案冲突。
- 类别判定：不是单纯通用的因果模拟器。即使自然语言输出不计入判断，专案命名的假设/动作/状态、固定物理结构及唯一案件假说约束已构成硬编码的专案因果/固定世界知识。文本是机制结果描述，不是用户角色对白。
- 编译/调用证据：Core 项目文件行 1–13；SDK 默认包含两个 `.cs`，排除项只有两个回归程序。全文引用搜索中，本文件仅由 `HallReplayProgram` 调用，没有外部生产调用点。

### A2. Core 固定人物与重演程序（类别 2、3）

- 文件：[csharp/HallReplayProgram.cs](../../../csharp/HallReplayProgram.cs)
- 行 3–10 专用类名与 Hall 动作解析。
- 行 12–33 编译重演步骤；代码固定“烟到达”环境事件并输出“环境 · 门缝涌入灰黑热烟”。
- 行 35–45 校验固定指令码（`WAIT_SMOKE`、`WAIT`、`GoToDoor`）与 Hall 动作；行 47–77 固定步进/循环限制、烟时钟、烟条件分支，并显式将人物定为“音乐家”、目的地定为“隔离门”。行 55–57 根据音乐家是否在门边和烟是否到达决定跳转；行 67–76 还有硬编码中文流程输出。
- 类别判定：属于专案角色、场景、事件和流程逻辑（2），也有硬编码面向玩家/日志的叙述性文本（3）。与通用 AST 或任意 story 步骤执行器不同，它自行规定特定人物及烟/门厅流程。
- 编译/调用证据：同 A1；本类只引用 A1 的 `Action` 与 `Simulate`。引用搜索未见 Godot 生产路径、正式宿主或其他 Core 生产类调用 `HallReplayProgram.Compile`/`TryAction`。

### A3. 宿主按章节选择数据驱动适配器，并读取剧本资源（类别 1/4；有章节固定接线）

- 文件：[godot_csharp/scenes/Shared/Game/GameSession.cs](../../../godot_csharp/scenes/Shared/Game/GameSession.cs:49)、[godot_csharp/scenes/Host/GameEngine.cs](../../../godot_csharp/scenes/Host/GameEngine.cs:6)
- `GameSession.cs` 行 49–89 是输入分派和地图已知/可用性检查；行 94–101 新建 `Chapter1Session`；行 104–125 仅在第一章结束后实例化第二章适配器、载入对应视图。这是章节流程编排和宿主胶水，条件依赖快照与传入脚本数据。它有项目章节标识/资源路径，但本切片中未发现它自身硬编码案件谜底、角色关系或剧情对白。
- `GameSession.cs` 行 110–120 把 `campusScript` 交给 `Chapter2AuthoredProfiles.Apply`、`Chapter2Narrative`、`Chapter2InteractionSession.ValidateScript` 和 WorldScript parser，再创建章节游戏。被调定义在本审计范围之外（`scenes/Chapter2/...`），这里仅能确认有这条正式调用链，不能代替对外部适配器内容的审计。
- 行 66、68、85、107、109、117、132 通过 `Chapter1Data.Text(...)` 按键获取界面诊断/游戏名。此处硬编码的是文案键和调用位置，实际文字来源定义在范围之外；没有把这些键当作本地硬编码对白。
- `GameEngine.cs` 行 10–15 将生成的 chapter 2 JSON、第一章场景、第二章地图视图及追踪回调注入 `GameSession`；行 17–46 读取 `.story` 源文件作来源追踪元数据。此处表现为剧本外置/宿主配置，而不是剧情源在宿主代码中展开。

### A4. Godot 界面中的样例与专案标识（类别 3 的编辑器预览；不是运行剧情）

- [ChapterDialogue.gd](../../../godot_csharp/scenes/UI/Panels/Dialogue/ChapterDialogue.gd:7)：行 7–35 的对白、选项、历史默认值均标为 `Editor Preview`，并且行 54 声明这是呈现适配器；运行路径行 539–554 从 packet engine 取数据并显示 `line.text`，没有在 `_present_line` 生成剧情正文。因此这些字串是可见的编辑器示例对白，不是生产运行对白。
- [Notebook.gd](../../../godot_csharp/scenes/UI/Panels/Notebook/Notebook.gd:19)：行 19–30 是 `Editor Preview` 的示例物品/规矩；行 37–46 区分宿主传入数据和编辑器预览。示例“窗台的水渍/桌上的钥匙/门锁/夜里不得离开房间”会在编辑器预览中出现，不能据此认定正式游戏把这些内容写死。
- [GameEngine.cs](../../../godot_csharp/scenes/Host/GameEngine.cs:11) 的硬编码章资源路径、[GameSession.cs](../../../godot_csharp/scenes/Shared/Game/GameSession.cs:124) 的场景资源/初始地图/出生点，以及 [Main.gd](../../../godot_csharp/scenes/Host/Main.gd:23) 的窗口标题属于项目/章节集成标识和 UI 初始化，不构成门厅案的因果答案逻辑。窗口标题是固定项目名（非通用可配置值）。
- UI 正式呈现主要通过 packet、snapshot、story text 和宿主事件数据填充。例如 `ChapterDialogue.gd` 行 543–554 将事件 speaker/text 映射到 UI；`UISystem.gd` 行 3–6、31–40 声明共享 UI 收快照并回传事件。

## 无命中项与分类边界

- 对 Core 其余生产文件的汉字字符串扫描，命中是 API/机制注释、中文解析/状态错误、推理诊断（例如 `Cognition.cs` 对明确命题的限制、`Speech.cs`/`Reasoner.cs` 对信任/证据来源的通用解释）。这些描述抽象输入变量，不含项目角色、身份答案或特定剧情因果，按通用执行/诊断处理，未列为剧情。
- `Ast.cs`、`AstDecoder.cs`、`Model.cs`、`WorldRuntime.cs`、`ActionRuntime.cs`、`SimulationSession.cs`、`CTBSystem.cs`、`TemporaryState.cs`、`DecisionSystem.cs`、`SocialSystem.cs`、`Cognition.cs`、`Reasoner.cs`、`Speech.cs` 中检索到的是类型化 AST、观察/推理、动作交易、调度和社交评分机制；未发现固定专案实体 ID、人物名、答案常量或对白。
- UI 中“未选”“变量已改动”“打开证物”等提示是跨章节的结构性界面语句；在调用上下文中作为可覆盖字段/状态提示使用，没有作为故事对白或固定推理答案处理。

## 覆盖范围、排除与未覆盖

- 已覆盖 Core 生产 `.cs` 15 个：`ActionRuntime.cs`、`Ast.cs`、`AstDecoder.cs`、`Cognition.cs`、`CTBSystem.cs`、`DecisionSystem.cs`、`HallCausalityRules.cs`、`HallReplayProgram.cs`、`Model.cs`、`Reasoner.cs`、`SimulationSession.cs`、`SocialSystem.cs`、`Speech.cs`、`TemporaryState.cs`、`WorldRuntime.cs`。
- 已覆盖目标 Godot 目录全部 32 个手写生产 `.cs/.gd`（以 `rg --files` 枚举；Shared/Story 9、Shared/Game 2、Host 3、UI 18）。本范围没有更深层 `AGENTS.md`。
- 排除 `bin/`、`obj/`、`.godot/`、生成物、回归/检查文件。`csharp/ScopeRegressionProgram.cs`、`ReasonerRegressionProgram.cs`、Godot 的 `Shared/check/` 和其他 `check/`、PlayerTranscript/工具脚本不在此次产品源码范围。
- 单独排除样例：`godot_csharp/scenes/Shared/Story/Examples/ExampleWorld.cs` 与 `Examples/WorldScript/Host/WorldSessionEngine.cs`、`WorldScriptPreview.gd`；本报告没有把样例作为生产证据。
- 未覆盖 `godot_csharp/scenes/Chapter2/` 与 `scenes/Awakening/` 的专章实现（含 `Chapter1Data`、`Chapter2AuthoredProfiles`、`Chapter2Narrative` 定义及专案脚本），也未覆盖编译后的 JSON、`.story`、翻译表和场景资源。A3 指出的调用链是范围内代码到范围外实现的静态引用，不能据本报告判定那些外部实现是否还有额外剧情硬编码。
- 本报告只回答源码里是否有剧情逻辑/文案硬编码；不评价设计符合性、规则正确性、完整运行可达性或游戏输出。
