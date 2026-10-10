# 剧情硬编码重构指示

本文是前两章（《安眠者有福》第一章与续篇第二章）剧情硬编码的现行重构指示，只记录当前仍存在的问题、处理方案与完成标准。源码中的待处理位置以 `STORY-HARDCODE[编号]` 注释标记，现存数量用 `git grep -c "STORY-HARDCODE\["` 查看；标记注释已删除但问题仍在的位置、以及无编号的同类残留，一并列在 [solution-groups.json](solution-groups.json)，现行原文摘录见 [solution-evidence.md](solution-evidence.md)。

`AWK-042`（铜管绘制几何）与 `CSH-028–031`（资产造型与姿态生产配方）是合法的表现/资产来源记录，标记保留，不属于待修问题。

## 共同边界

依据 [设计规范总纲 §四至§六](../../docs/design-standard.md#四约束等级) 与 [第六部分 §2](../../docs/design-standard.md#2-剧本宿主与章节)：

- 具体设定、台词、初始条件、个体规则与案件定义写在 `.story`，译文在现有本地化源。另建手写 JSON、配置 C# 或每章专用策略类不算解决。
- 过去可由作者写定；世界过程以法则声明；人物当前选择须由本人认知中的规则、欲望、关系与处境推出。固定 NPC 行动队列即使移进剧本也不算完成。
- Core 只含通用本体、AST、推理、决策、观察与行动提交；无 Godot 的剧本运行时负责装载与执行；Godot 负责几何、节点、渲染与表现回执。
- 复用现有 `WorldSession`、`CoreScriptWorld`、流程解释器与编译器，按迁移需要补能力。新增支持须贯通解析、IR 校验、绑定与执行，不支持的内容明确报错。
- 通用控制结构、物理算法和纯几何参数可以保留。是否完成以具体绑定是否消失为准；删注释、改名或搬目录不算完成。

## 第一阶段：剧情侦探代理操控（最优先）

**目标：** 由 `.story` 中声明了操控规则的侦探经正式引擎决策产出动作（启动时由 AI 坐上玩家席位），截取这些动作直接操控 Godot 中运行的正式游戏。这是“AI 可替代任一玩家角色”的通用特性，不是侦探专用功能。

**全量测试：** 只由主代理运行 Godot。画面只呈现侦探的动作与侦探可见的内容。全量测试一旦卡住（无进展、无候选、报错、超时）立即停止运行并修复，修好后从头重跑。

**禁止：** 为测试绕过 Core 给侦探写特殊动作能力；为了能测试单独为侦探写动作逻辑、路线、答案或注入状态。遇到这类缺口，登记到下方“代理操控缺口”，用通用机制解决。

**完成标准：** 侦探代理在真实 Godot 中从开局连续推进到第一章终局，再到第二章终局；全程无注入、快进或读取隐藏真相；录像/截图只显示侦探视角；所有卡点都有对应修复或登记。

### 席位与全量测试现状

- AI 坐席：任何在 `.story` 声明了 `操控规则` 的角色，都可在启动时由 AI 坐上玩家席位（`GameEngine.ConfigureAiSeat`），正式剧本不必改写；规则、推理预算与决策均来自该角色本人。席位节点 `Host/AgentPilot.gd` 经正式界面与现场把选中的动作做出来，界面普通操作不代替角色决定。
- 全量测试：`python godot_csharp/tools/full_test.py --name <名> --time-scale 4`，场景 `scenes/Shared/check/FullTest/FullTest.tscn` 以正式 `Main.tscn` 为子节点；`--time-scale` 只整体加快引擎时间（实测 4× 约提速 3.5 倍）。外层 Python 看门狗在预算外强制结束。
- 进程：侦探代理已能从醒来连续推进到始业之答（检视、对话、声路试验、比对鞋底、试木楔、连续作答前三问）。

### 代理操控缺口

- 席位只能坐当前章节的玩家角色：章节会话把玩家角色固定为表现声明中的侦探（`PlayerActor`/`PlayerIdentity`），相机跟随、现场 `player` 节点、视线雾与认知采集都绑定该身体；需支持按会话把席位换到任一声明了操控规则的角色。
- 当前问题缺前提时，代理不会转向未交谈的人物与未去过的地点：第四问依赖沈鼎相关前提（饮茶习惯、活酵种罐），整局沈鼎只作为候选出现一次（他在备餐间），代理反复重做已做过的声路试验，最终触发无公开进展停止。寻找缺失前提目前只覆盖已见过的入口。
- 第一章 V1“警铃响时苏泠所在位置”在正式剧本中没有可获知的前提，代理无法凭本人认知完整证明该项。
- 第二章卡片不提供 `help` 选项（处理函数存在），代理无法发起协助。
- 声明代理时存档明确拒绝，代理会话跨进程恢复未实现。
- 策略偏好测量工具随旧无渲染宿主一并移除，按角色偏好的批量测量需改用 `FullTest.tscn` 重建。
- 走位探索按 64 像素格记忆与已见区域交界；交界外是墙时只在第一次到达后失去吸引力，大地图上的探索效率仍待第二章实测。

## 现存剧情与流程缺陷

以下为两章正常路线上已确认、尚未修复的剧情与流程问题。涉及真相时间线的项（D23、D24）先对照设计规范核定，再改文字。

| # | 缺陷 | 位置/性质 |
| --- | --- | --- |
| D1 | 课表比剧本早一天：同一上午广播与授课不一致，首课《守则读法》从未讲授 | `Demon.LessonOfDay` 读取 `10_执行.story` 的 schedule，与 `08_流程.story` 脱节；见 S10 |
| D2 | 案发日台账无缺口时广播“今日无新课”，心声仍引“昨夜，有人进过后勤院”；no_accuse 的“账上有缺口”无条件出现 | 剧本缺分支 |
| D3 | 无缺口时仍问台账 | 剧本缺分支 |
| D4 | 结局卡称呼不一致：第一章称“侦探”，第二章称“你自己” | 表达；见 S02 |
| D5 | 卡片不提供 `help` 选项：正常路线无协助场面、第 3 日时段错位、议事投票投给别人的方案 | 交互/路线；同代理操控缺口 |
| D6 | `s10_open.013` 与甲区出门时门外有人矛盾 | 剧本文字 |
| D7 | `night_hint` 没有发出点，不可达 | 剧本/流程 |
| D8 | 策略牵引的停滞计时把侦探自己做试验的时间算入 | 策略层 |
| D9 | 苏泠的邀请没有她见过的物证作依据 | 策略层/认知来源 |
| D10 | 声路试验后同一证物卡连发两次 | 第一章流程；见 S07 |
| D11 | 侦探请“先别开”后，雀儿仍按定时撬门 | 人物选择未读请求；见 S04/S09 |
| D12 | 撬门演门厅画面而侦探在备餐间，违反视野即认知 | 演出越过本人视野；见 S07 |
| D13 | 第二日集合旁白称“人到齐了”，丁区未出门 | 旁白不读实际在场 |
| D14 | 调停失败后配给停摆、全员饥饿，旁白仍称“人人吃上了早饭” | 旁白不读实际状态；见 S06 |
| D15 | 议事结论句与实际表态不符 | 见 S12 |
| D16 | 章末“没有往下想的”语义不清 | 文字 |
| D17 | 内部物件名外露给玩家 | 表达；见 S02 |
| D18 | 日常旁白缺主语 | 文字 |
| D19 | 菜单弹出前重复上一句 | 呈现 |
| D20 | 东廊未开时提示“后勤院有了变化”，越过本人认知 | 认知来源；见 S05 |
| D21 | E1 闪回提前泄出第一问与第四问的答案 | 第一章结构 |
| D22 | 莫兰病程时间线前后矛盾 | 剧本 |
| D23 | 第二章案件在正常路线上没有揭示与高潮 | 第二章结构 |
| D24 | 第二问“门外没有人”与终局折回鞋印未交代先后 | 剧本时序 |
| D25 | 阮清越代词错误 | 文字 |

## 现存问题分组

组号沿用原编号以便追溯；S01、S16、S20、S21 已无待处理内容，不再列出。“注释已删”指标记注释已不在、问题仍在。

| 组 | 核心处理 | 待处理条目 | 优先级 |
| --- | --- | ---: | --- |
| S02 | 语义结果与剧本文案、称呼分离 | 12 | P1 |
| S03 | 剧本实体登记替代固定名单与专用枚举 | 17 | P1 |
| S04 | 个体信念、习惯与立场编译为主观规则 | 18 | P1 |
| S05 | 感知与传播法则驱动认知更新 | 6 | P1 |
| S06 | 世界法则声明物理后果和劳作配方 | 4 | P1 |
| S07 | 剧本声明交互条件、试验和演出流程 | 16 | P1 |
| S08 | 结构化事件替代中文文本反解析 | 3 | P1 |
| S09 | 通用 AST 执行规矩与社会请求 | 3 | P1 |
| S10 | 区分历史、制度钟点与人物当前行动 | 4 | P1 |
| S11 | 数据驱动案件、重演和证据判定 | 15 | P1 |
| S12 | 真实集会与个人接受驱动对质、议事 | 6 | P1 |
| S13 | 现场站点能力改为表现声明 | 1 | P2 |
| S14 | 资源生成器读取剧本显示内容 | 3 | P2 |
| S15 | 声音配置声明附加音效 | 1 | P2 |
| S17 | 测试去除文本匹配与固定路线 | 18 | P2 |
| S18 | UI 回放与采样器消费正式绑定 | 5 | P2 |
| S19 | 预览数据中性化 | 8 | P2 |

S15、S18 及 S19 的 UI 部分归 UI 工作线处理，此处冻结，只保留登记。

### S02：语义结果与剧本文案、称呼分离

**现状：** Core 已返回来源与理由代码，回应和称呼按说话人认知从剧本解析；剩下的是宿主内的专案分支与中文字面量（[摘录](solution-evidence.md#s02)）：

- `CH2-066`：[GameLoop.cs:665](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:665) `if (q.Id == P.Quer) AccusedQuer = true;`，指控结果按具名人物置专案标志。
- `CH2-057`：[Player.cs:559](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:559) `Describe` 仍按 Granary/Ledger/Bed/SketchBook/MedCab/Litter/Board 逐个分支，病床直读 `P.Moran` 身体。
- `CH2-059`：[Report.cs:115–130](../../godot_csharp/scenes/Chapter2/Prototype/Report.cs:115) 章末报告直连 `QuerCleared/QuerOstracized`、温室、粮/油/药字段；`CH2-080` [Chapter2SessionView.cs:38](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2SessionView.cs:38) 终局事件、`CH2-081` [ConsoleUI.cs:447](../../godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:447) 状态栏同样按案件字段分支。文字已进词条，分支仍在 C#。
- `AWK-012`：[NightReplay.cs:689](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:689) `if (stranger && a.Who == Person.Detective) _detectiveRefusedStranger = true;`。
- [InvestigationPresentation.cs:142](../../godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:142) 宿主写定来源专称“铜口之言”。
- 中文 `Feed` 字面量：[Player.cs](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:245) 约 20 处，[GameLoop.cs:702](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:702)/709/799，[Morning.cs:315](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:315)/324/328/624/640，[Case.cs:415–417](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:415)；帮助例句 [ConsoleUI.cs:420](../../godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:420) 写定“讲 茉拉 …”。
- 结局卡称呼（D4）、内部物件名外露（D17）属本组表达层。

**方案：** Core 与规则层只产出语义结果（事实、言语行为、立场、来源类型、理由代码）；文字一律由 `.story` 模板按结构化参数渲染。物件描述改为按物件声明的表达键与属性取值，不按对象引用分支；章末、终局与状态栏按声明的结果字段与表达键生成。来源专称按来源类型查词汇表。帮助中的剧情例句从剧本读取，通用命令语法可留在工具。

**限制：** 只把字面量搬进词汇表、保留专人/专物分支不算解决。含日志事实提取的输入由 S08 先结构化。

**完成标准：** 改台词或译文不改判定；更换被指控者、病人或物件不改代码；同一人物在不同认知下称呼正确；无适用回应时返回明确无反应；章末只总结已发生的状态。

### S03：剧本实体登记替代固定名单与专用枚举

**现状：** 固定绑定表与集合已改为剧本实体登记，第一章六变量取值域由剧本声明；但人物、群体、地点、行为、规则语义位与专用状态仍是封闭枚举，剧本中声明的人物/地点仍映射回这些枚举，只改剧本无法新增角色（[摘录](solution-evidence.md#s03)）：

- 枚举：[Model.cs:7–43](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:7) 的 `P/Group/Place/Cluster/Job/RF/T`（`CH2-074、092、085、098、093、099、075`）；[NightVariables.cs:7–18](../../godot_csharp/scenes/Awakening/Night/NightVariables.cs:7)（`AWK-063`）。
- 初始化：[NightReplay.cs:195](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:195)（`AWK-007`）、[Chapter1Script.cs:464](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:464)（`AWK-034`）、[Chapter2Script.cs:103](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:103)/[186](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:186)（`CH2-012、076`）。
- 声明回落旧枚举：[Chapter2Bindings.PersonId](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:14)；[Chapter2Script.cs:189](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:189) `BuildDeclaredRooms` 之后仍以 `Place.*` 建物件字段。
- 具名人物分支：`CH2-028` [Emergence.cs:138](../../godot_csharp/scenes/Chapter2/Prototype/Emergence.cs:138) `p.Id == P.Quer ? P.Ward : P.Ruan`；`CH2-031` [Player.cs:483](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:483) `TestimonyPresentation` 按人物分支；[Morning.cs:520](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:520) `s.Witness == P.Moran`。

**方案：** 以章节命名空间和稳定实体 ID 装载剧本声明，运行时解析为同一会话的对象引用；角色集合、群体、通路、初始物件、反事实变量绑定与专用状态由声明产生。保留 `ProjectionKind`、`RuleMode`、`ActionOp` 等机制枚举。具名人物间的对头、目击者、口供风格改读关系与认知声明。个体认知与选择规则交 S04。

**限制：** 把枚举自动生成到 C# 而保留按成员分支的执行器不算解决。旧枚举序号用于快照/存档，须有版本与稳定 ID 映射，不能直接重排。

**完成标准：** 用中性测试剧本新增或重排角色、地点、物件，运行代码不改；未知 ID、类型冲突、重复声明明确失败；跨章同一实体、灵魂与既有认知保持引用身份。

### S04：个体信念、习惯与立场编译为主观规则

**现状：** 18 处标记全部仍在（[摘录](solution-evidence.md#s04)）。人物策略层（`StrategyPolicy.cs`、`MorningStrategies.cs`、`Chapter1Strategies.cs`、`策略轨道.story`）只接入第一章晨间与现场，第二章未接入。具体问题：

- 第一章：[NightReplay.cs:283](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:283)/[310](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:310) 按变量设信任偏移、逐人写入昨夜行为与回应（`AWK-008、009`）；[MorningWorld.cs:95](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:95)/[256](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:256)/[305](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:305)/[338](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:338) 为具名人物写欲望与守则、以“公告第一条”作代价规则、取舍映射心声、独处犹豫固定改拒绝（`AWK-017、020、018、019`）。
- 第二章：[Alliances.cs:16](../../godot_csharp/scenes/Chapter2/Prototype/Alliances.cs:16)、[Social.cs:286](../../godot_csharp/scenes/Chapter2/Prototype/Social.cs:286) 阵营与成员按身份写定（`CH2-001、008`）；[Rules.cs:118](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:118)/[290](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:290)/[384](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:384)/[396](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:396) 具名人物的规则、议案措辞偏好、条件与否决底线（`CH2-005、034、035、036`）；[Chapter2Script.cs:301](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:301) 初始认知（`CH2-013`）；[GameJobs.cs:98](../../godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:98)/[195](../../godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:195)/[277](../../godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:277) 预置习惯、行动计划与背景人物专属行为（`CH2-041、042、029`）；[GameLoop.cs:625](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:625) 嫌疑增减（`CH2-065`）；[Host.cs:93](../../godot_csharp/scenes/Chapter2/Prototype/Host.cs:93) 首课后群体反应（`CH2-073`）。
- 人物不读他人请求即按定时行动（D11）、邀请缺乏本人见过的依据（D9）、牵引停滞计时口径（D8）属本组与策略层。

**方案：** 初始信念、关系、需求、欲望、技能与接受的规则写入剧本，编译为类型化规则与推理声明；守则按稳定规则 ID 引用，不用“第一条”。犹豫后的选择、课后反应、派系中心、议案立场与嫌疑都由适用的主观规则与接受关系推出。第二章接入同一策略层。规则执行与请求语义复用 S09。

**限制：** 把“某人某时必做某事”改成数据时间表，或把具名 switch 搬进编译器，不算解决。身体世界初始化与认知初始化分开，认知条目保留来源。

**完成标准：** 相同客观现场、不同个人所知所信产生不同且可解释的候选与结果；删改一人的规则即改变其行为；关键取舍的行动前认知、替代项与理由可追溯；他人请求能改变人物选择。

### S05：感知与传播法则驱动认知更新

**现状：** 6 处标记全部仍在（[摘录](solution-evidence.md#s05)）。声路拓扑已外置，但 [NightVariables.cs:98](../../godot_csharp/scenes/Awakening/Night/NightVariables.cs:98) 仍固定整句/句尾/隔墙/警铃的传播表（`AWK-001`）；[NightReplay.cs:502](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:502) 话语语义映射到人物认知（`AWK-011`）；[MorningWorld.cs:445](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:445) 接头状态投影到 E8 固定标签（`AWK-021`）；[Chapter2Script.cs:500](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:500) 夜间视线与身形目击（`CH2-014`）；[Social.cs:213](../../godot_csharp/scenes/Chapter2/Prototype/Social.cs:213) 恶魔词与指认后果（`CH2-037`）；[GameLoop.cs:544](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:544) 夜间取物是否被捕捉（`CH2-064`）。东廊未开时的“后勤院有了变化”提示（D20）同属认知越界。

**方案：** 剧本声明通道、衰减/片段、事件携带的命题、观察条件与解释规则；通用传播器决定谁得到哪部分信息，经 `Observation.Observe` 保留时间、来源、发送者与原事件关联。误听与成见作用于主体投影，不改客观事件。维修影响当前声路，昨夜记录保持原状。

**限制：** 不能传播后对全员广播完整真相，也不能按最终答案补齐听者规则；通用声学计算可留在代码，具体声音与案件标签的关联须由声明提供。

**完成标准：** 改接头、位置、遮挡、听者规则或消息来源，只改变相应人的认知；未在场或未闻者不凭空获知；历史重演与当前试验的通道状态分别验证。

### S06：世界法则声明物理后果和劳作配方

**现状：** 世界法则原子事务、工位真实占用与过程/刺激/配方声明已具备；剩余（[摘录](solution-evidence.md#s06)）：

- `CH2-062、063`：[GameLoop.cs:326](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:326) 把指定案件行动映射为固定后果，[GameLoop.cs:415](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:415) `Physics` switch 按具名分支结算人物、作案物件与线索。
- `AWK-015`：喊声可闻范围仍写在宿主 [NightReplay.cs:866](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:866)。
- `CH2-070`：分灯规则 [10_执行.story:349](../../godot_csharp/scenes/Awakening/Script/story/续篇/10_执行.story:349) 的公式写定 `person.Bertrand`/`person.Mora` 的信任与莫兰病情。
- 夜推仓门失败没有留痕法则。
- `WorldProcessCheck` 与 `tools/check/test_world_processes.py` 未纳入常规检查。
- 配给停摆后旁白与实际饥饿状态不符（D14）。

**方案：** 剧本声明配方、物件属性、触发条件、资源消耗、技能要求、耗时与留痕法则；执行器只实现通用事务、约束、事件与属性更新。分配规则按声明的需求与关系条件计算，不点名人物。喊声可闻范围走 S05 传播法则。失败尝试按法则留痕。

**限制：** 不能预写痕迹以吻合答案；不能因“本时段干过活”认定不同工位同时在场。

**完成标准：** 行动取消、资源不足、换执行者或时间不重叠时不产生成功后果；资源守恒、失败留痕、两阶段提交正确；世界过程检查进入常规运行并通过。

### S07：剧本声明交互条件、试验和演出流程

**现状：** 16 处标记全部仍在（[摘录](solution-evidence.md#s07)）。第一章 [Chapter1SessionFlows.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:14)（L14/159/234/394/625/630，`AWK-022–027`）、[Chapter1Script.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:76)（L76/122/226/280/312/318，`AWK-028–033`）、[Chapter1Interaction.cs:37](../../godot_csharp/scenes/Awakening/Script/Chapter1Interaction.cs:37)（`AWK-035`）仍按证物编号特判试验、手印门槛、鞋底菜单、试验句解锁、回忆流程、离场低语、笔记联动与开场/相遇摆位；第二章 [Chapter2InteractionSession.cs:345](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:345)（`CH2-033`）、[RoomPresentation.cs:11](../../godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs:11)/[54](../../godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs:54)（`CH2-078、079`）写定场景选项与当面行动。证物卡重复发出（D10）、侦探不在场仍演门厅画面（D12）、菜单前重复上一句（D19）属本组流程与演出。

**方案：** 候选、前置知识、在场要求、对象参数、结算点、笔记模板、触发器与演出步骤写进剧本；C# 保留菜单等待、取消、回执与调用通用行动的解释器；Godot 核验距离、房间与走位，不代判知识条件。演出按观察者视野过滤，侦探看不到的事件不演画面。开门的世界提交与音效/音乐分开，由剧本按成功结果组织。

**限制：** 把 E3/E8/E11 改名为专用命令仍在执行器特判不算解决。演出只呈现已成立的状态或作者允许写定的过去。

**完成标准：** 调换物证 ID 或新增同类交互不改运行代码；取消、过期回执、走位失败、重复进入结果正确；同一卡片不重复发出；读、写、拒绝耗时符合行动类型。

### S08：结构化事件替代中文文本反解析

**现状：** 类型化事件与权限投影已就位，第二章事实、地点与回应判据已查结构化字段。剩余无编号的文本反解析（[摘录](solution-evidence.md#s08)）：[Morning.cs:424](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:424) `Label` 按“饼屑:”“神情:”前缀解析痕迹键；[Morning.cs:459–466](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:459) 以 `Contains("的习惯")`/`StartsWith("应")` 判断行动缘由；[Chapter2InteractionSession.cs:352](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:352) `StartsWith("存粮")` 解析事实。

**方案：** 痕迹键改为结构化记录（种类、对象、地点）；行动缘由读决策记录中的规则来源（习惯/请求/规矩）；事实直接查询事件字段。文本只由 S02 渲染。

**完成标准：** 改措辞、标点、人名显示或译文后，痕迹标签、缘由与事实判定不变。

### S09：通用 AST 执行规矩与社会请求

**现状：** 规矩执行与社会请求已改为剧本声明的过程 AST，接受与执行分离、预览无副作用。剩余（[摘录](solution-evidence.md#s09)）：

- `CH2-056`：[Player.cs:241](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:241) 起的玩家行动仍写定课堂问答（`act.Topic==0/1/2`、`Lessons.ContainsKey(14)`、中文“铜口当众说：问得好”）、拓碑帮手 `P.Gu`、`WardEscorts`、`Manipulations` 的 `About=P.Quer`。
- `CH2-010`：行为卡按数字编号判定，散见 [Council.cs:110](../../godot_csharp/scenes/Chapter2/Prototype/Council.cs:110)、Report.cs:52/86、GameJobs.cs:209/246、GameLoop.cs:551/632、Host.cs:69、Morning.cs:484、Social.cs:257（`Behavior is 0/2/4/9`、`>= 5 and <= 8`），且无顺序校验；剧本调整卡序会静默错位。
- `CH2-055`：[Player.cs:112](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:112) `Cmd.Mediate when Day == RationFirstDay && Slot == Slot.Noon`，调停只在固定日时段有效。

**方案：** 课堂问答、拓碑、护送与操纵改为剧本声明的社会行动，帮手与对象由请求与接受产生；行为卡以稳定 ID 和声明的语义类别查询，不按序号；调停的可用条件由剧本声明并读取实际处境。

**限制：** 不能把专用白名单换成同名 C# 回调；社交预估分大于零不等于执行。

**完成标准：** 新增规则与同类社会行动只改剧本；重排行为卡不改结果；未知 AST 或未实现操作明确报错；请求被拒、取消、条件变化时不发放成功效果。

### S10：区分历史、制度钟点与人物当前行动

**现状：** 昨夜时序已是剧本历史输入，饥饿与病人早餐为 dawn 法则，唤名为刺激。剩余（[摘录](solution-evidence.md#s10)）：

- `CH2-027`：课表在 [10_执行.story:389](../../godot_csharp/scenes/Awakening/Script/story/续篇/10_执行.story:389) `schedule.lessons` 另建，与 `08_流程.story` 的流程日期错一天（D1）。
- `CH2-043`：[Demon.cs:100–118](../../godot_csharp/scenes/Chapter2/Prototype/Demon.cs:100) 晨间广播按 `FirstDay/CaseDay/LastDay` 分支；案发日无缺口时的口径与心声不一致（D2）。
- `CH2-067`：[10_执行.story:354](../../godot_csharp/scenes/Awakening/Script/story/续篇/10_执行.story:354) `observe_distrust` 以 `IsWard`、第 4 日、傍晚为门槛，护送/告知以 `IsSelma` 为门槛；这些自主规则经 [GameJobs.cs:181](../../godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:181) 以 `Acceptance = True` 装给所有人，没有认知来源。

**方案：** 课表并入 `08_流程` 的流程调度，单一来源；广播口径由制度钟点加教师所知（台账实际状态）选择话语，不按日分支；盯视、护送、告知改为人物依本人所见所知、经决策产生，规则只装给真正接受它的人并记录来源。

**完成标准：** 课程广播、授课与回执同日一致；跨多个时刻、阶段重入不重复效果；疾病可发生而人物回应不同；请求被拒或人物不在场时，不播放或登记其已完成的行动。

### S11：数据驱动案件、重演和证据判定

**现状：** `CaseTraces` 已按全字段比较。案件结构仍为专案（[摘录](solution-evidence.md#s11)）：

- 第一章：[AwakeningCase.cs:20](../../godot_csharp/scenes/Awakening/Night/AwakeningCase.cs:20) 装配 V1–V6（`AWK-003`）；[MorningTraces.cs:13](../../godot_csharp/scenes/Awakening/Night/MorningTraces.cs:13)/[95](../../godot_csharp/scenes/Awakening/Night/MorningTraces.cs:95) 固定证物字段与 E1–E9 比较（`AWK-004、005`）；[Chapter1Session.cs:631](../../godot_csharp/scenes/Awakening/Script/Chapter1Session.cs:631) 第六问固定末项隐藏（`AWK-037`）；[DetectivePlanning.cs:187](../../godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs:187) 答辩固定四步（`AWK-039`）；`AWK-038` [DetectivePlanning.cs:63–88](../../godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs:63) 规划器构造时由 `AwakeningCase.Predictions` 装配本章 V1–V6 假设；[Chapter1Replay.cs:8](../../godot_csharp/scenes/Awakening/Script/Chapter1Replay.cs:8) 实播重演写入固定变量（`AWK-061`）。
- 第二章：[Case.cs:10](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:10)/[45](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:45)/[74](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:74) 证物模型、四项变量与重演映射（`CH2-086、087、003`）；[Morning.cs:165](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:165)/[255](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:255) 案发痕迹与可读组合（`CH2-030、049`）；[InvestigationPresentation.cs:18](../../godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:18)/[128](../../godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:128) 调查条目映射（`CH2-032、071`）；[Chapter2InteractionSession.cs:426](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:426) 对质议程（`CH2-025`）。
- 第一章 V1 无可获知前提（见代理操控缺口）；第二章案件在正常路线上没有揭示与高潮（D23）。

**方案：** 剧本声明变量、取值域、前提修改、隐藏条件、命题、痕迹字段、证物来源与论证支持规则；运行时枚举声明的取值域，复制隔离世界只修改指定处境/信念，调用同一世界法则与决策，比较玩家已知的结构化痕迹。规划器假设从声明的案件模型生成。当前案件来自实际发生的事件，原始真相只作历史声明。

**完成标准：** 改变量数量、顺序或答辩步骤不改代码；未知证物不算吻合；论证凭作答者本人知道且支撑命题的任意有效材料；每个答辩变量在正式剧本中都有可获知路径；重演不改现实与跨章对象；无案或其他危机时流程合乎实际状态。

### S12：真实集会与个人接受驱动对质、议事

**现状：** 6 处标记全部仍在（[摘录](solution-evidence.md#s12)）：[Council.cs:24](../../godot_csharp/scenes/Chapter2/Prototype/Council.cs:24) 议事前厅三站与资格（`CH2-004`）；[Council.cs:115](../../godot_csharp/scenes/Chapter2/Prototype/Council.cs:115) 议案、具名派系立场与结局（`CH2-047`）；[Case.cs:243](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:243)/[357](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:357)/[412](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:412)/[436](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:436) 对质结算、洗清/排斥、重开对质与雀儿结局（`CH2-088–091`）。议事结论句与实际表态不符（D15）。

**方案：** 议事与对质开场可以召集到场；此后每人的表态、搭手、退让、搬离与返回由本人决定并作为实际行动提交。剧本声明议程、议案、适用反应、公开提交要求与结果条件；世界结果由真实支持、资源、技能与并行工位产生，章末按 S02 表达。

**限制：** 召集之后不能统一写支持/信任；缺席、拒绝与迟到者保留各自状态。机构判定与个体反应分别记录，过半数不等于人人接受。

**完成标准：** 更换被指控者、缺席者、持钥人或工位参与者，流程仍适用；未知材料不能说服；缺人/迟到不能开机关；结论句与结尾反映实际表态与后果。

### S13：现场站点能力改为表现声明

**现状：** 现场身份、资产与锚点关联已由剧本表现声明，缺失绑定报错。剩余 `CH2-083`：[Chapter2AuthoredSites.cs:23](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2AuthoredSites.cs:23) 站点 kind 白名单 `bed/watch/meal/tube/school_rules` 中，`tube`、`school_rules` 是本作专用语义（[摘录](solution-evidence.md#s13)）。

**方案：** kind 只保留通用交互能力；专用站点改为通用能力加剧本声明的对象/流程绑定。

**完成标准：** 新增同类站点只改剧本；未知 kind 仍明确报错。

### S14：资源生成器读取剧本显示内容

**现状：** 第一章旁白与第二章室内图的标题、场点标签已从剧本生成。剩余（[摘录](solution-evidence.md#s14)）：第二章 6 张室外图的 [outdoor.py:38–45](../../godot_csharp/tools/chapter2_art/outdoor.py:38) `REGIONS` 标题与 [outdoor.py:893–900](../../godot_csharp/tools/chapter2_art/outdoor.py:893) 场点标签为字面量（17 条资源记录，含 `RES-007–018`、`RES-035–039`）；[build_awakening_map.py:191](../../godot_csharp/tools/build_awakening_map.py:191) `plan()` 中 `title="乙区"` 为字面量。

**方案：** 室外图沿用室内图的做法：坐标、碰撞与摆位留在地图源，标题与标签按 map/site 稳定 ID 取剧本编译数据，经 `--text-only` 入口生成；第一章地图标题同理。重新生成后导入。

**完成标准：** 改一处剧本名或译文，生成标签与运行显示一致；无手改生成物；纯美术数据不变。

### S15：声音配置声明附加音效

**现状：** `CSH-022`：[ChapterDialogue.gd:565](../../godot_csharp/scenes/UI/Panels/Dialogue/ChapterDialogue.gd:565) 按“当前声流是否是铜口”推断首次开口的附加音效（[摘录](solution-evidence.md#s15)）。归 UI 工作线。

**方案：** 由剧本/声音表现配置提供开口音效、语音组与连续组标识，UI 只按配置与呈现生命周期播放、去重。

**完成标准：** 同一语音纹理可用于不同发声源；首次开口音效只在声明的组开始播放，跳过/回看/重复回执不多播。

### S17：测试去除文本匹配与固定路线

**现状：** 测试情境与固定路线已外置为夹具，夹具不进正式调用链；但多处仍以中文文本断言或跑固定路线（[摘录](solution-evidence.md#s17)）：

- 文本匹配：[StoryPacketCheck/Program.cs:82–151](../../godot_csharp/scenes/Awakening/check/StoryPacketCheck/Program.cs:82)（`AWK-049`）；[ContinuationSimulation.cs:131–172](../../godot_csharp/scenes/Awakening/check/DetectiveSimulation/ContinuationSimulation.cs:131)（`AWK-051、052`）；[flow_check.gd](../../godot_csharp/scenes/Awakening/check/flow_check.gd:191)（`AWK-053`）；[state_gallery.gd:308/315/329](../../godot_csharp/scenes/Awakening/check/state_gallery.gd:308) 图注文本，另有 4 个目标为空（`AWK-054`）；[hand_outline_check.gd:29–33](../../godot_csharp/scenes/Awakening/check/hand_outline_check.gd:29)（`AWK-057`）；[Checks.cs:217](../../godot_csharp/scenes/Chapter2/Prototype/Checks.cs:217) 按中文日志前缀匹配（`CH2-022`）；[ui_modules_check.gd:195](../../godot_csharp/scenes/Shared/check/ui_modules_check.gd:195)/[223](../../godot_csharp/scenes/Shared/check/ui_modules_check.gd:223)（`CSH-015、016`）。
- 固定路线与预置：[check_flow.gd:47–69](../../godot_csharp/scenes/Chapter2/check/check_flow.gd:47) 支持方总选最后一项（`CH2-017`）、[check_flow.gd:158–217](../../godot_csharp/scenes/Chapter2/check/check_flow.gd:158)（`CH2-019`）；[chapter2_fixture.gd:56](../../godot_csharp/scenes/Chapter2/check/chapter2_fixture.gd:56)（`CH2-020`）；[Prototype/Program.cs:51–71](../../godot_csharp/scenes/Chapter2/Prototype/Program.cs:51)（`CH2-077`）；[verify_ch1_locker_ui.gd:160](../../godot_csharp/tools/verify_ch1_locker_ui.gd:160)（`CSH-007`）；[chapter1_smoke.gd:61/94](../../godot_csharp/scenes/Shared/check/chapter1_smoke.gd:61)（`CSH-010`）；[check_room_fog.gd:192](../../godot_csharp/scenes/Shared/check/check_room_fog.gd:192)（`CSH-019`）与 [:329](../../godot_csharp/scenes/Shared/check/check_room_fog.gd:329) `_make_actor("Huo")`（`CSH-020`）。
- 失效入口：[check_gallery.gd:48](../../godot_csharp/scenes/Chapter2/Game/check_gallery.gd:48) 预加载已删除的 `AwakeningEngine.cs`（`CH2-021`）。

**方案：** 驱动代码只负责输入、等待与断言；断言按正式行 ID、事件类型与结构化状态，不按自然语言；路线改为按公开候选与目标状态选择，或明确标为夹具并只作回归。机制测试用中性构造。黄金预期独立审阅后封存，不在运行时由同一份真相或同一算法算出。失效画廊修复入口后再作为证据。

**限制：** 不删穷举、认知隔离、跨章与 UI 状态断言；按正式行 ID 定位可以保留。自动路线与模拟器结果不算盲玩证据。

**完成标准：** 改文案或译文不使回归误报；剧情逻辑出错时回归仍能暴露；fixture 与开发代码不进正式调用链；画廊目标非空且入口可运行。

### S18：UI 回放与采样器消费正式绑定

**现状：** 5 处标记全部仍在，归 UI 工作线，冻结（[摘录](solution-evidence.md#s18)）：[capture_ui_start.gd:9](../../godot_csharp/tools/capture_ui_start.gd:9) 按开场剧情节点推进（`CSH-006`）；[capture_story_ui_samples.gd:8](../../godot_csharp/tools/capture_story_ui_samples.gd:8) 固定采样清单（`CSH-024`）；[capture_player_transcript.gd:26/309/783](../../godot_csharp/tools/capture_player_transcript.gd:26) 固定地点映射、坐标复原与截图阶段识别（`CSH-025–027`）。

**方案：** 回放复用 S13 的正式地图/演员/热点绑定，消费已记录的公开快照与语义事件；采样计划用显式测试剧本或稳定场面 ID 声明，按事件类型与表现完成信号推进。

**完成标准：** 新增同类地图不改采集器；无截图也能完成文本回放；缺图/缺节点/终局未绘制如实失败。

### S19：预览数据中性化

**现状：** 示例世界已改为独立 `.story`，作者预览按声明枚举。剩余 UI 预览内容（[摘录](solution-evidence.md#s19)），归 UI 工作线：`CSH-021` [campus_preview.json](../../godot_csharp/scenes/UI/Workbench/samples/campus_preview.json:3) 仍用正式地点 ID（剧情隔离检索不扫 `.json`）；UI 命中 [CampusMap.gd:50](../../godot_csharp/scenes/UI/Panels/CampusMap/CampusMap.gd:50)、[ChapterDialogue.gd:134/141/539/543](../../godot_csharp/scenes/UI/Panels/Dialogue/ChapterDialogue.gd:134)、[ReplayManuscript.tscn:137](../../godot_csharp/scenes/UI/Panels/Manuscript/ReplayManuscript.tscn:137)、[Title.tscn:39](../../godot_csharp/scenes/UI/Panels/Title/Title.tscn:39)、[UISystem.gd:496](../../godot_csharp/scenes/UI/UISystem.gd:496)；shared-ui 命中 [CampusMap.tscn:6](../../godot_csharp/scenes/Shared/UI/CampusMap.tscn:6)、[ChapterDialogue.tscn:133/261](../../godot_csharp/scenes/Shared/UI/ChapterDialogue.tscn:133)（“侦探”）。

**方案：** 编辑器预览改用中性占位与结构样本；手写 `.tscn` 中的预览文字在源场景修改；正式会话只显示宿主数据。

**完成标准：** 编辑器不出现正式案件答案或正式地点/人物 ID；剧情隔离检索 ui、shared-ui 命中为 0；正式会话未收到宿主数据时不回落到样例。

## 验证状态

- 非 Godot 检查全部通过：core、scope 27、cognition 372、host、主观推理 68、命题 69、策略 57、策略集成 271、代理接口 53。
- flow/flow2 未在当前代码上运行。
- 剧情隔离检索（`tools/check_story_leak.py`）：core、interface、host 代码命中 0；adapter 1967（仅报告）；ui 8；shared-ui 15。检索不扫 `.json`。

## 实施顺序

1. 第一阶段：剧情侦探代理操控。全量测试暴露的缺口按通用机制修复，登记在“代理操控缺口”。
2. 其余 P1 按依赖推进：S03（实体登记）与 S08 残留 → S09、S04（第二章接入策略层）、S05、S06 → S07、S10、S11、S12 → S02 表达层收尾。“现存剧情与流程缺陷”随所属组一并修复，纯文字项可随时修。
3. P2：S13、S14、S17；UI 工作线负责 S15、S18、S19。

每组迁移后运行受影响模块的构建、编译一致性与既有回归；涉及玩家文字或流程的改动须经全量测试复核。
