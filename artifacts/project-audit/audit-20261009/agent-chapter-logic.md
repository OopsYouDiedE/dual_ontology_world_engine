# Godot 手写代码剧情逻辑静态审计（切片 B）

- 基线：`4aca6644449cd00ba5f5521ad086af54b31dd225`（本次工作树只读；分支上另有 Awakening 译文文件已修改，本审计未碰这些文件）。
- 范围：`godot_csharp/scenes/Awakening/`、`godot_csharp/scenes/Chapter2/` 中手写 `.cs/.gd`；重点为 Awakening Script/Night 与 Chapter2 Prototype/Script。未读取 `docs/` 或历史评审，未运行游戏、构建或测试，未更改产品代码。
- 结论：**有专案剧情逻辑，而且不止对白。** 有些流程胶水是解释 `.story` 编译数据的通用运行器；但也存在将第一章案件变量、证物映射、重演事实和流程状态，以及第二章人物、案件、日程与规则实体直接固化到 C# 的专案逻辑。应区分来源后判断：从 `.story`/生成 JSON 读取并执行的 authored data 属于授权数据执行；手写专案条件/事实映射仍是硬编码剧情逻辑。切片 B 足以确认“有”，不主张覆盖仓库全部代码。

## 正式编译与场景使用

- [Chapter1Script.csproj](../../../godot_csharp/scenes/Awakening/Script/Chapter1Script.csproj) 显式编译 `Chapter1SessionFlows.cs` 等手写运行时代码、`Chapter1CompiledDialogue.cs` 以及 `../Night/*.cs`；嵌入 `generated_awakening_content.json`、`generated_awakening_layout.json`、`generated_chapter_01_world.json`。这些是正式章节程序集内容，不是孤立原型。
- [GameSession.csproj](../../../godot_csharp/scenes/Shared/Game/GameSession.csproj) 引用 Chapter1Script 与 Chapter2Script；[Chapter2Script.csproj](../../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.csproj) 编译 `Chapter2Script.cs` 和 `../Prototype/*.cs`（排除仅 CLI 的 `Program.cs`），并嵌入 `generated_chapter_02.json`。其注释也说明 Prototype 原逻辑编入正式剧本程序集。
- 正式第一章场景 [Awakening.tscn](../../../godot_csharp/scenes/Awakening/Awakening.tscn) 加载 AwakeningMap 与房间/回放 GDScript；Chapter2 场景代码通过共享 GameSession / 正式视图及章节地图 adapter 使用。Chapter2 `Field` 中 `capture_*.gd` 是采集辅助工具，未作为剧情证据。
- `Chapter1CompiledDialogue.cs` 被排除在本切片的手写判断之外：它是生成对白物，不应算作手写源。它仍被 Chapter1Script.csproj 编译并含大量剧情对白；对白源需要追到 `.story`/生成链，不能据生成物推断为手写对白。两章生成 JSON 同样作为数据载入；本结论不把生成物误分类为手写代码。

## 证据分类

### 通用机制 / 数据解释器

- `Chapter2DesignRules.cs:9-39` 将 JSON 条件树操作符（`true/not/all/any/night_did/sighting/state/saw_key/saw_pos/looked/asked/rule_known/trace_known`）解析为通用 Cond；同文件 `:161-205` 从 `Chapter2Design` 读取 authored rules/facts。这部分本身是解释器。
- `Chapter2AuthoredProfiles.cs:12-79` 从统一编译 JSON 读取人物 profile；`Chapter2Design.cs:98` 获取嵌入资源。此类数据驱动 profile 属于 `.story` 编译数据执行。
- `Chapter1Data.cs` 与 `Chapter1SessionFlows.cs` 的大量 `T(key)`/`Chapter1Data.Text` 调用将文案交给数据层；`Chapter1SessionFlows.cs:96-150` 将泛化动作种类分派给操作实现，单看这些分派不代表逐句写剧情。

### 第一章：硬编码案件/谜题映射与流程

- [MorningTraces.cs](../../../godot_csharp/scenes/Awakening/Night/MorningTraces.cs:8) 的注释把 `E1/E2/E3/E4/E5/E6/E7/E9` 与具体痕迹种类逐一对应；`:13-24` 将晨间“真相”从 `TRUTH` JSON 读取，这部分来源是编译数据。
- 同文件 `:79-112` 以 `switch(id)` 对 `E1` 至至少 `E7` 分别决定比对字段、优先级与冲突类型；`:114` 后继续映射其他证物。即使差异句子从 Chapter1Data 取文案，证物 ID → 逻辑事实字段的案件映射仍由手写代码确定，属于专案谜题逻辑。
- [AwakeningCase.cs](../../../godot_csharp/scenes/Awakening/Night/AwakeningCase.cs:20) 从 JSON `DETECTIVE_TRACE_FIELDS` 读字段定义，`:32-54` 用 `NightReplay.Run` 计算所有 `NightVariables` 预测，并构造 Core `CaseModel`。这是数据驱动推演框架；但 `MorningTraces` 的字段清单/反射字段 schema 是本地专案模型。
- [NightVariables.cs](../../../godot_csharp/scenes/Awakening/Night/NightVariables.cs:6) 固定人物枚举 Detective/Huo/Shen/Su；`:9-17` 固定房间、声源、台词类别、听从对象与熟识变量；`:130-134` 固定这些枚举与故事键的顺序绑定。机制形态是案件世界变量定义，内容具有明确章节语义。
- [NightReplay.cs](../../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:7-25) 定义 `Open/Wedged/Bolted/.../WantsOpen/Help/...` 等此案专用事实标签和关系；`:91` 有固定房间间移动时长；`:216-217` 依据 `SuPlace`/`HuoPlace` 将特定角色变量映射到特定房间。文件的大段重演事件与因果关系是第一章案件模型，不是纯通用引擎机制。部分人物对白/表现通过数据文本键读取，不能因此消除其硬编码的事件模型。
- [Chapter1SessionFlows.cs](../../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:373-405) 针对 E11/E3 有特例；`:592-601` 将固定听位映射到特定证物，并规定句子 0/1/2 分别必须已检视 E7、E6/E5、recall Huo 才可选；`:812-839` 将固定进度状态 `wake/investigate/reversal_one/replay_match/answer_done` 映射到阶段，并在 `reversal_one` 自动检查 E1/E2/E3、`replay_match` 标记 E8。它包含章节流程 / 状态叙事推进规则，不是全部由 `.story` 流程解释器决定。
- 上述第一章检查与映射属于专案条件/证物与状态绑定；目前看到的示例不是硬写的连续对白段落。未在本切片中将文案查找调用视为手写对白。

### 第二章：硬编码案件、人物行为与规则

- [Case.cs](../../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:12-33) 把 `CaseDesign` 提供的变量/标签作为案件输入；`:61-109` 案件重演回调直接锁定 `P.Quer/P.Margaret/P.Mora`，区分具体夜间规则接受、病情新闻阻断、晚饭状态，并安排向特定角色告知某规则。`:149-193` 通过 authored known 条件筛痕迹并枚举重演匹配。后半属于机制+数据解释；`:79-103` 是绑定到特定人物和案情假设的专案剧情因果条件。
- [Chapter2Script.cs](../../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:199-205) 固定温室阀门、名录库门、配重室等世界实体与身份；`:245-253` 将特定钥匙/能力固定给 Mora/Ward/Ruan 或按 profile 阈值赋给角色；`:344-349` 对 Ward/Gu 与 Quer 的案件关系写入固定认知状态。`:484` 起的 `SightLines` 是按章节地点声明视线关系的静态表。此处混合世界初始化机制与明确专案设定。
- [Chapter2DesignRules.cs](../../../godot_csharp/scenes/Chapter2/Prototype/Chapter2DesignRules.cs:119) 将行为 ID 名单固定为“取粮、按人头、交钥匙、指控须证、转述、开库各取、开库委员会、开库封闭、开库约束、限制入院、温室”；`:205` 显示案件事实通过 `CaseDesign.Fact(name)` 获取。操作解释器通用，但行为类别全集属于章节规则接口。
- [Player.cs](../../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:49-61) 固定地点可交互实体及地点映射；`:217-238` 以课程 Topic 索引决定铜口的特定回答后果/人物怀疑；`:254-264` 将细查/晨间足迹固定到案件物证；`:323-332` 对顾平章拓碑与可读内容作硬编码专案分支；`:468-499` 按特定人物身份提供针对本案的证词模板/逻辑；`:534-555` 按特定世界实体给出专案物件描述。文件包含显著的专案事实、交互与人物响应硬编码，不只是通用命令调度。
- [Council.cs](../../../godot_csharp/scenes/Chapter2/Prototype/Council.cs:30-41, 89-97) 固定特定钥匙/力气门槛、名录库议事方案及叙述后果；[RoomPresentation.cs](../../../godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs:23-40, 85-96) 把特定场景/时段映射到出场行为与指令位置；部分为流程投影，部分是章节行为与活动表。
- [Bots.cs](../../../godot_csharp/scenes/Chapter2/Prototype/Bots.cs:95-139) 是手写自动玩家策略和针对剧情路径的操作脚本，CLI `Program.cs` 本身被正式 csproj 排除；不过 bots 在 Prototype wildcard 中，是否进入正式程序集需注意：当前 csproj 排除的只有 `Program.cs`，所以 `Bots.cs` 会编译进 Chapter2Script。
- 手写中文字串分布在 Chapter2 Prototype 的反馈、证词、物件描述和事件中（例如 `Case.cs:21-26`、`Council.cs:89-97`、`Player.cs:326-332`、`Player.cs:534-555`）。这超出单纯机制错误消息；属于硬编码玩家可见剧情/设定文案。它和 `.story` 中的 authorized 内容存在双重来源风险，应由主审计统一判定其合规性。

## 覆盖与边界

- 文件范围以目录清单为准：Awakening Script（排除生成的 `Chapter1CompiledDialogue.cs`）、Awakening Night、Awakening 根层 `.gd`；Chapter2 Prototype、Chapter2 Script 的 `Chapter2Script.cs`、Chapter2 Field `.gd`。采用关键词/实体/枚举/ID/文本检索后，重点阅读上述关键文件上下文；未逐行文学判断。
- 排除 `check/`、生成物与工具脚本；未读取项目设计文件，未判剧情是否符合设计或人物文学品质。
- `RoomPresentation.cs`、`Chapter1SessionFlows.cs` 等流程文件可同时含通用流程解释器与章专用触发映射；分类依具体映射，而不是把所有 `if`、中文或 UI 文本判为违规。
- 仍需主代理综合其他审计切片与 `.story` 源比对，确认所有硬编码行的授权/重复来源。此报告只回答是否存在及给出可复核证据：**存在明确的章节专用逻辑与手写玩家可见文字**。
