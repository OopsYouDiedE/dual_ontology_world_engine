# 剧情硬编码解决方案审核与归并清单

审核日期：2026-10-09。源码依据：`b608f5b6`；输入为 [源码索引](index.md)、[资源索引](resource-index.md) 及其机器清单。原索引没有附解决方案，本报告根据标记位置、关联调用与现有剧本管线提出方案并审核实现边界。

**结论：196 处源码标记和 97 处资源记录归并为 21 组处理建议，不是 293 个独立缺陷。** 相同根因集中在同一组；一段方法涉及多个问题时只登记一个主归属，其他处理由依赖组承接。完整对应关系和依赖见 [solution-groups.json](solution-groups.json)。编号连续范围均包含首尾。

本清单仅作审核并引用现有实现，没有实施引擎、剧本、资源或架构改造。以下优先级表示后续处理顺序：P1 为内容边界、因果、认知或跨章状态相关；P2 为呈现、工具和预览相关。它不表示所有标记都已证明存在运行故障。

引用方式：每组“引用原文”直接列出具体语句及其源码位置；未展开的同组记录见 [全部具体内容摘录](solution-evidence.md)，逐条覆盖 196 处源码与 97 处资源。代码块为原样局部摘录，不用概括代替引文；它们不是完整方法，也不是新增剧情或实现方案。

## 方案审核的共同边界

依据 [设计规范总纲 §四至§六](../../docs/design-standard.md#四约束等级) 与 [第六部分 §2](../../docs/design-standard.md#2-剧本宿主与章节)：

- 具体设定、台词、初始条件、个体规则及案件定义进入 `.story`，译文维护在现有本地化源中。另建一份手写 JSON、配置 C# 或每章专用策略类不能解决作者源分裂。
- 过去可由作者写定；世界过程声明法则；人物当前选择须由本人认知中的规则、欲望、关系和处境推出。固定 NPC 行动队列即使移到剧本，也不能作为完成方案。
- Core 只保留通用本体、AST、推理、决策、观察和行动提交。无 Godot 的剧本运行时负责装载及执行；Godot 负责几何、节点、渲染和表现回执。
- 复用现有 `WorldSession`、`CoreScriptWorld`、流程解释器及编译器，按迁移需要补能力。当前 [WorldScriptDocument.Parse](../../godot_csharp/scenes/Shared/Story/WorldSession.cs:48) 会拒绝不支持的规则或需要章节适配器的内容；“把数据迁入 story 即可自动执行”尚不成立。新增支持需同时贯通解析、IR 校验、绑定和执行，保留明确报错。
- 方法中的通用控制结构、物理算法和纯几何参数可以保留。是否删除标记以具体绑定是否消失为准，不能通过改注释或改目录宣称完成。

## 合并后的列表

| 建议 | 核心处理 | 源码标记 | 资源记录 | 优先级 |
| --- | --- | ---: | ---: | --- |
| S01 | 清理 Core 旧案件专用实现 | 5 | 0 | P1 |
| S02 | 语义结果与剧本文案、称呼分离 | 18 | 0 | P1 |
| S03 | 剧本实体登记替代固定名单与专用枚举 | 20 | 0 | P1 |
| S04 | 个体信念、习惯与立场编译为主观规则 | 18 | 0 | P1 |
| S05 | 感知与传播法则驱动认知更新 | 6 | 0 | P1 |
| S06 | 世界法则声明物理后果和劳作配方 | 6 | 0 | P1 |
| S07 | 剧本声明交互条件、试验和演出流程 | 16 | 0 | P1 |
| S08 | 结构化事件替代中文日志反解析 | 10 | 0 | P1 |
| S09 | 通用 AST 执行规矩与社会请求 | 5 | 0 | P1 |
| S10 | 区分历史、制度钟点与人物当前行动 | 6 | 0 | P1 |
| S11 | 数据驱动案件、重演和证据判定 | 15 | 0 | P1 |
| S12 | 真实集会与个人接受驱动对质、议事 | 6 | 0 | P1 |
| S13 | 现场身份和资产关联改为表现声明 | 15 | 8 | P2 |
| S14 | 资源生成器读取剧本显示内容 | 0 | 80 | P2 |
| S15 | 声音配置声明附加音效 | 1 | 0 | P2 |
| S16 | 按作用域结束章节规则 | 1 | 0 | P1 |
| S17 | 整理测试夹具与固定路线采集器 | 32 | 0 | P2 |
| S18 | UI 回放与采样器消费正式绑定 | 5 | 0 | P2 |
| S19 | 示例内容外置、预览数据中性化 | 6 | 9 | P2 |
| S20 | 保留资产造型与姿态生产配方 | 4 | 0 | 保留 |
| S21 | 剧本声明通行状态及其传播 | 1 | 0 | P1 |
| **合计** | **每条记录一个主归属** | **196** | **97** | |

### S01：清理 Core 旧案件专用实现

**对应标记：** `CSH-001–005`。

**引用原文：** [本组全部 5 条摘录](solution-evidence.md#s01)。

**`CSH-003`：案件痕迹及其文案直接由 Core 生成**（`production` / `logic`）。

**原文位置：** [csharp/HallCausalityRules.cs](../../csharp/HallCausalityRules.cs:78)，L78–L84。

```csharp
        void MarkWedgeExposure()
        {
            if (!smoke || closed || !braced || allowsPassage || sootMaskedAtDoor) return;
            sootMaskedAtDoor = true;
            sootOnWedge = true;
            traces.Add(new("door-mask", "门底形成木楔遮蔽的斜边洁净区。"));
            traces.Add(new("wedge-soot", "迎灰接触面附着灰黑沉积，背面保留挤压凹痕。"));
```

**`CSH-005`：旧重演解释器绑定热烟与案件动作**（`production` / `text`）。

**原文位置：** [csharp/HallReplayProgram.cs](../../csharp/HallReplayProgram.cs:30)，L30–L36。

```csharp
        void Smoke()
        {
            if (smoke) return;
            smoke = true;
            actions.Add(HallCausalityRules.Action.SmokeArrives);
            Emit("环境 · 门缝涌入灰黑热烟", "SmokeArrives");
        }
```

[HallCausalityRules](../../csharp/HallCausalityRules.cs:4) 和 [HallReplayProgram](../../csharp/HallReplayProgram.cs:4) 包含具体案件动作、物证与反馈，仍被 Core 默认编译纳入。仓内调用搜索只发现 `HallReplayProgram` 对 `HallCausalityRules` 的调用，未见两者之外的源码调用；正式第一章走 `NightReplay`。这是发行边界的问题，不能把这两份实现当作正式第一章重演的替换入口。

**推荐方案：** 优先从 Core 编译与发行中移除，检查 API 文档、回归、工具及包兼容性后清理旧实现。有保留价值的机制再按 S06/S11 提炼，具体示例转换为独立 `.story`。不能将原代码整体移入宿主或另一个类库就算完成。

**完成标准：** Core 构建、打包及通用回归通过，正式重演仍使用既有接口；发行 API 不再包含专案类型。仓外包使用者未在本轮核实，删除公开 API 前需明确兼容性与版本处理。

### S02：语义结果与剧本文案、称呼分离

**对应标记：** `CSH-009`；`AWK-012、016、062`；`CH2-015、023、026、044、046、048、057、058、059、060、066、072、080、081`。

**引用原文：** [本组全部 18 条摘录](solution-evidence.md#s02)。

**`CH2-015`：按整段文字替换教师称呼**（`production` / `text`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:552)，L552–L553。

```csharp
    // 学员尚不知道教师的身份；作者侧的称呼只在实现里使用。
    public static string PlayerText(string text) => text.Replace("恶魔", "教师", StringComparison.Ordinal);
```

**`CSH-009`：证据权重机制直接返回本世界专称**（`production` / `text`）。

**原文位置：** [csharp/Reasoner.cs](../../csharp/Reasoner.cs:130)，L130–L138。

```csharp
            case ObservationSource.Perception: return (8, "亲见");
            case ObservationSource.Inference: return (6, "自己推出");
            case ObservationSource.Recall: return (3, "回忆");
            case ObservationSource.Corruption: return (1, "铜口之言，不当证据");
            case ObservationSource.Testimony:
                var speaker = claim.Relations.FirstOrDefault(r => r.Relation == Relations.Subject).Target;
                var trust = speaker?.Tags.GetValueOrDefault(Tags.Trust) ?? 0;
                // Neutral testimony counts a little; distrust makes it worthless, never inverted.
                return (Math.Max(1, 2 + 0.5 * Math.Clamp(trust, -3, 3)), $"{speaker?.Name ?? "有人"}说的，信任 {trust}");
```

同一问题覆盖 Core 反馈、心声、调查描述、口供表达、帮助、人物印象与章末总结。有些已经读取词汇表，但选择某个具体人物的词条、决定其说法的分支仍在源码中。[Chapter2Content.PlayerText](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:549) 还通过全文替换改变称呼，与按说话人认知称呼的目标不一致。

**推荐方案：** Core 返回来源类型、采信因子、事实、言语行为、立场及理由代码；游戏层从 `.story` 编译的模板与回应声明取文字，绑定结构化参数。个体回应的适用条件读取说话人认知，偏好经决策机制选择。通用来源类型不应直接变成“铜口”专称；教师称呼按说话人、听者和所知身份解析。帮助中的具体剧情例句也从剧本读取，通用命令语法可留在工具中。

**方案限制：** 仅把字面字符串搬进词汇表，保留专人分支及全文替换，不算解决。含日志事实提取的口供方法由 S08 先修正输入，再复用本组表达层。

**完成标准：** 修改台词或译文不改判定；同一人物在不同认知下称呼正确；无适用回应时返回明确无反应。章末表达只总结已发生的状态，不生成新事实。

### S03：剧本实体登记替代固定名单与专用枚举

**对应标记：** `AWK-002、006、007、034、036、063`；`CH2-012、074、075、076、084、085、092、093、094、095、096、097、098、099`。

**引用原文：** [本组全部 20 条摘录](solution-evidence.md#s03)。

**`AWK-063`：人物、房间和六项变量由封闭枚举定义**（`production` / `schema`）。

**原文位置：** [godot_csharp/scenes/Awakening/Night/NightVariables.cs](../../godot_csharp/scenes/Awakening/Night/NightVariables.cs:7)，L7–L18。

```csharp
public enum Person { Detective, Huo, Shen, Su }

/// <summary>乙区的房间与隔离门外的起居厅。地图几何属宿主；这里只有房间拓扑。</summary>
public enum Room { R05, R06, R07, R08, Corridor, Hall, Pantry, Foyer, Lounge }

// 重演面板的六个变量（设计 §4.1）。每个选项都是“处境与人心”，没有任何一项指定某人做了什么。
public enum SuPlace { OwnRoom, Hall, Pantry }
public enum HuoPlace { ByTube, OwnRoom, Foyer }
public enum VoiceOrigin { Tube, OutsideDoor }
public enum VoiceWords { BoltKeepShut, OpenSaveMe }
public enum DetectiveHeeds { Huo, Su, Himself }
public enum Acquaintance { Strangers, Familiar }
```

**`CH2-084`：剧本物件仍只能绑定到固定对象字段**（`production` / `schema`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:9)，L9–L16。

```csharp
    private static readonly IReadOnlyDictionary<string,Func<Game,Essence>> Objects = new Dictionary<string,Func<Game,Essence>>
    {
        ["Granary"]=g=>g.Granary,["Desk"]=g=>g.Desk,["Stove"]=g=>g.Stove,["Jar"]=g=>g.Jar,["Ledger"]=g=>g.Ledger,
        ["Bowl"]=g=>g.Bowl,["Bed"]=g=>g.Bed,["MedCab"]=g=>g.MedCab,["OilStore"]=g=>g.OilStore,["Valve"]=g=>g.Valve,
        ["Gauge"]=g=>g.Gauge,["Blackboard"]=g=>g.Blackboard,["Stele"]=g=>g.Stele,["ArchiveDoor"]=g=>g.ArchiveDoor,
        ["WeightRoom"]=g=>g.WeightRoom,["LockCore"]=g=>g.LockCore,["RelayPost"]=g=>g.RelayPost,["Base"]=g=>g.Base,
        ["SketchBook"]=g=>g.SketchBook,["Clock"]=g=>g.Clock,["NameVoice"]=g=>g.NameVoice
    };
```

人物、地点、通路、状态、行为、群体和别名虽有部分显示名称来自剧本，数量与绑定仍由枚举、固定数组及对象字段限定。[Chapter2Bindings](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:8) 也列出封闭的专用实体表。改剧本无法独立增加这些内容。

**推荐方案：** 以已有章节命名空间和稳定实体 ID 装载剧本声明，运行时解析为同一会话的对象引用。角色集合、群体、通路耗时、初始物件、反事实变量绑定和专用状态由声明产生；保留通用 `ProjectionKind`、`RuleMode`、`ActionOp` 等机制枚举。初始真相可留在剧本并由装载器建立世界，但不得以真相直接授予玩家知识。个体认知与选择规则交 S04。

**方案限制：** 只把具名枚举自动生成到 C#，同时保留按枚举成员分支的执行器，仍不能满足只改剧本扩展内容的目标。旧枚举序号已用于快照/存档，须有版本及稳定 ID 映射，不能直接重排。

**完成标准：** 用中性的测试剧本新增或重排角色、地点、物件，运行代码不改；未知 ID、类型冲突及重复声明明确失败；跨章同一实体、灵魂和既有认知保持引用身份。

### S04：个体信念、习惯与立场编译为主观规则

**对应标记：** `AWK-008、009、017、018、019、020`；`CH2-001、005、008、013、029、034、035、036、041、042、065、073`。

**引用原文：** [本组全部 18 条摘录](solution-evidence.md#s04)。

**`AWK-019`：独处时将犹豫固定改成拒绝**（`production` / `logic`）。

**原文位置：** [godot_csharp/scenes/Awakening/Night/MorningWorld.cs](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:337)，L337–L342。

```csharp
    public RecallDecision? DecideAlone(string evidence)
    {
        var decision = DecideRecall(evidence);
        if (decision is null || decision.Choice != RecallChoice.Hesitate) return decision;
        return decision with { Choice = RecallChoice.Refuse, Thought = decision.Thought + Chapter1Data.Text("取舍·无人可问") };
    }
```

**`CH2-035`：按具体人名写定议案偏好和说法**（`production` / `logic`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Rules.cs](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:448)，L448–L453。

```csharp
    private static IEnumerable<(RF Need, int Penalty, string Text)> Requirements(P p) => p switch
    {
        P.Ruan => [(RF.Records, 2, "不记账就对不上账")],
        P.Quer => [(RF.NightException | RF.TwoWitness | RF.EvidenceFirst, 2, "又是一条只抓人的规矩")],
        _ => []
    };
```

这组统一处理按人名安装习惯、熟悉度偏移、取回取舍、议案偏好、否决底线、嫌疑推断和阵营中心。已有 `DecisionSystem` 调用应保留；问题是决定候选、条件和偏好的具体内容仍由宿主编写。

**推荐方案：** 将初始信念、关系、需求、欲望、技能及接受的规则写入剧本，编译为类型化规则与推理声明。用稳定规则 ID 读取守则，替代“第一条”。犹豫后如何选择、教课后的反应、谁成为派系中心，都由适用的主观规则与接受关系推出；不按固定人名覆盖选择。规则的执行和请求语义复用 S09。

**方案限制：** 将“某人某时必做某事”改成一份数据时间表，或把具名 switch 移到编译器，不算消除硬编码。身体世界的初始化与认知初始化也不能混用；认知条目需保留来源。

**完成标准：** 相同客观现场、不同个人所知/所信，能产生不同且可解释的候选和结果；删去或改变一个人的规则即可改变行为。关键取舍的行动前认知、替代与理由可追溯，需独立人物行为评审。

### S05：感知与传播法则驱动认知更新

**对应标记：** `AWK-001、011、021`；`CH2-014、037、064`。

**引用原文：** [本组全部 6 条摘录](solution-evidence.md#s05)。

**`AWK-001`：传播结果固定为本案的完整、句尾、隔门等组合**（`production` / `logic`）。

**原文位置：** [godot_csharp/scenes/Awakening/Night/NightVariables.cs](../../godot_csharp/scenes/Awakening/Night/NightVariables.cs:94)，L94–L101。

```csharp
        var data = Loaded.Value;
        var leaking = data.Joints.Where(j => loose?.GetValueOrDefault(j.Name, j.Loose) ?? j.Loose).Select(j => j.Room).ToHashSet();
        if (origin == VoiceOrigin.Tube)
            return listener == data.TubeRoom ? Hearing.Full : leaking.Contains(listener) ? Hearing.Tail
                : data.Murmur.Contains(listener) ? Hearing.Murmur : Hearing.AlarmOnly;
        if (listener == data.DoorOuter) return Hearing.Full;
        if (listener == data.DoorInner) return Hearing.FullMuffled;
        return leaking.Contains(data.DoorInner) && leaking.Contains(listener) ? Hearing.FullMuffled : Hearing.Nothing;
```

**`AWK-021`：接头变化固定映射到 E8 和“接头松动”标签**（`production` / `schema`）。

**原文位置：** [godot_csharp/scenes/Awakening/Night/MorningWorld.cs](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:440)，L440–L448。

```csharp
    public bool SetJoint(string name, bool loose)
    {
        if (!_joints.ContainsKey(name)) return false;
        _joints[name] = loose;
        if (_evidence.TryGetValue("E8", out var tube) && tube.Get<StateProperty>() is { } state)
            foreach (var tag in state.Values.Keys.Where(t => t.Name == "接头松动").ToArray())
                state.Values[tag] = _joints.Values.Any(v => v) ? 1 : 0;
        _runtime.Sync();
        return true;
```

声路拓扑已有外部声明，但 [Acoustics.Hear](../../godot_csharp/scenes/Awakening/Night/NightVariables.cs:91) 仍固定整句/句尾/隔门组合；话语语义、接头对应物证标签及夜见取物解释也有专案分支。

**推荐方案：** 剧本声明通道、衰减/片段、事件携带的命题、观察条件与解释规则。通用传播器决定谁能得到哪部分信息，经 `Observation.Observe` 保留时间、来源、发送者和原事件关联。误听与成见作用于主体投影，不改客观事件。维修影响当前声路，已封存的昨夜记录保持原状态。

**方案限制：** 不能在传播后对全员广播完整真相，也不能按最终答案补齐听者的规则；通用声学计算可以留在代码，具体声音与案件标签关联须由声明提供。

**完成标准：** 改接头、位置、遮挡、听者规则或消息来源，会改变相应认知而非全部人的知识；未在场或未闻者不凭空获知。历史重演与当前试验的通道状态分别验证。

### S06：世界法则声明物理后果和劳作配方

**对应标记：** `AWK-015`；`CH2-040、062、063、068、070`。

**引用原文：** [本组全部 6 条摘录](solution-evidence.md#s06)。

**`AWK-015`：灰扇、鞋印和湿布的具体物理后果**（`production` / `logic`）。

**原文位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:854)，L854–L857。

```csharp
        var trace = _floor.Get<FloorTraceProperty>()!;
        if (changed && actor.StartRoom == Room.Foyer && Tag(_floor, NightTags.AshFan) == 1)
            Print(actor, boltedNow ? PrintToward.Bolt : PrintToward.DoorGap);
        if (Tag(_cloth, NightTags.Placed) == 1 && trace.Prints.Count > 0) trace.ClothOverHeel = true;
```

**`CH2-068`：温室配方及两人条件写在完成结算中**（`production` / `logic`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:839)，L839–L848。

```csharp
        // 这个下午在温室干过活的人（干完活走了也算；阀门与压力表须同时有人守，所以至少两人）。
        var crew = People.Where(p => Get(p.Body, T.Did(Job.Greenhouse)) > 0 || p == Det && LastAction?.Cmd == Cmd.Greenhouse && Where(p) == Place.Greenhouse).ToArray();
        if (crew.Length == 0 || GreenhouseRepaired) return;
        if (crew.Length < 2)
        {
            Log.Add($"D{Day} 下午 温室只有一人（{crew[0].Name}），两站没凑齐，白干");
            if (crew[0] == Det) Feed.Add("温室里只有你一个人。阀门和压力表要两个人同时守，白干了一下午。");
            return;
        }
        var skill = crew.Sum(p => Math.Max(1, p.Pro.GreenhouseSkill + (p.Pro.Structure >= 2 ? 1 : 0)));
```

门、烟、鞋印、粮、治疗、温室与油料的具体后果散落在动作编译和完成回调中。它们是世界因果模型，不应全部删除，也不应与 NPC 选择一起外置成固定剧情队列。

**推荐方案：** 在剧本声明配方、物件属性、触发条件、资源消耗、技能要求、耗时与留痕法则。执行器只实现通用事务、约束、事件与属性更新。多个工位要求通过真实占用及重叠时段核验；领取资源、物品残留和失败反馈均来自动作实际提交。人物是否参与及如何请求归 S04/S09。

**方案限制：** 不能预写痕迹以确保吻合答案，也不能仅因“本时段曾干过活”就认定不同岗位同时在场。[GreenhousePhysics](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:836) 的整段条件需要在迁移时重新核对。

**完成标准：** 行动取消、资源不足、换执行者或参与时间不重叠时不产生成功后果；资源守恒、失败留痕与两阶段提交保持正确。代码中只留下通用机制，不新增专案回调。

### S07：剧本声明交互条件、试验和演出流程

**对应标记：** `AWK-022、023、024、025、026、027、028、029、030、031、032、033、035`；`CH2-033、078、079`。

**引用原文：** [本组全部 16 条摘录](solution-evidence.md#s07)。

**`AWK-025`：按 E11、E3 编排不同试验流程**（`production` / `logic`）。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:374)，L374–L388。

```csharp
    private IEnumerable<Op> Experiment(string id)
    {
        CloseModal();
        if (id == "E11") { yield return new Run(CompareHand("detective")); yield break; }
        var who = "";
        if (id == "E3")
        {
            var options = Content.GetProperty("SOLE_TARGETS").EnumerateObject().Select(p => (object?)Option(p.Name, p.Value.GetString()!)).ToList();
            options.Add(Option("cancel", T("界面·取消")));
            yield return new Await(Event("candidates", ("title", T("鞋底·标题")), ("prompt", T("鞋底·提示")), ("options", options.ToArray())), true);
            who = Text(_reply, "choice_id") is var picked && picked != "cancel" ? picked : "";
        }
        yield return new Run(PlayPlan(() => _content.BeginExperiment(_world, id, who)));
        yield return new Run(OpenCard(id));
    }
```

**`AWK-027`：按特定证物和霍铎回忆解锁试验句**（`production` / `logic`）。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:602)，L602–L608。

```csharp
    private bool SentenceKnown(int index) => index switch
    {
        0 => Has("examined", "E7"),
        1 => Has("examined", "E6") || Has("helped", "E5"),
        2 => Has("recalls", "huo"),
        _ => false,
    };
```

同一根因包括物证编号特判、手印前置、鞋底对象菜单、试验句解锁、回忆流程、离场低语、笔记联动以及开场/相遇摆位。现有第一章流程和第二章 `design.flows/schedule` 已可复用，不需再写一套流程所有者。

**推荐方案：** 把候选、前置知识、在场要求、对象参数、时间结算点、笔记模板、触发器和演出步骤写进剧本。C# 保留菜单等待、取消、回执和调用通用行动的解释器；Godot 核验距离、房间与走位，不能代判知识条件。开门世界状态提交与音效/音乐是不同操作，由剧本按成功结果组织。

**方案限制：** 不能把 E3/E8/E11 改名为三个专用命令后仍在执行器特判。演出只能呈现已成立的状态或作者允许写定的过去，不能令当前人物无条件完成选择。

**完成标准：** 调换物证 ID 或新增同类交互不改运行代码；取消菜单、过期回执、走位失败和重复进入均有正确结果；读、写及拒绝耗时符合实际行动类型。

### S08：结构化事件替代中文日志反解析

**对应标记：** `CH2-007、028、031、038、039、045、050、051、052、082`。

**引用原文：** [本组全部 10 条摘录](solution-evidence.md#s08)。

**`CH2-007`：从中文口供提取门闩事实**（`production` / `logic`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Social.cs](../../godot_csharp/scenes/Chapter2/Prototype/Social.cs:261)，L261–L269。

```csharp
    public void Interpret(Person p)
    {
        var h = p.World.History;
        for (; p.Interpreted < h.Count; p.Interpreted++)
        {
            var e = h[p.Interpreted];
            if (e.Source != ObservationSource.Testimony || e.Target.Kind != ProjectionKind.Event) continue;
            if (e.Details.Contains("门闩松了")) Know(p, Granary, T.LatchLoose, 1, ObservationSource.Testimony, e.Details);
        }
```

**`CH2-050`：从中文行动文字推断地点**（`production` / `schema`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Morning.cs](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:415)，L415–L428。

```csharp
    private static string InkPlace(string act, Place dorm)
    {
        if (act.StartsWith("没做成：")) return "";
        if (act.Contains("自己寝区")) return dorm.ToString();
        var places = Enum.GetValues<Place>().Where(place => act.Contains(Names.Of(place))).ToArray();
        if (places.Length == 1) return places[0].ToString();
        if (places.Length > 1) return "";
        if (act.StartsWith("开粮仓") || act.StartsWith("修粮仓门闩") || act.StartsWith("清点粮仓")
            || act.StartsWith("夜里守在粮仓边上") || act.StartsWith("去配给台") || act.StartsWith("在配给台")
            || act.StartsWith("在灶上") || act.StartsWith("去台账"))
            return Place.Logistics.ToString();
        if (act.StartsWith("夜里去开名录库")) return Place.ArchiveFront.ToString();
        return "";
    }
```

[Social.Interpret](../../godot_csharp/scenes/Chapter2/Prototype/Social.cs:260) 从“门闩松了”提取事实；[Morning.InkPlace](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:414) 从文字推地点；[Council.Seizure](../../godot_csharp/scenes/Chapter2/Prototype/Council.cs:54) 从“答应了/请回/让开”识别回应。口供、候选、可读线索与“有变化”过滤也有同类依赖。集中修一个事件通路，比逐处替换字符串可靠。

**推荐方案：** 在动作或言语产生时记录稳定事件 ID、行动类型、主体/对象、地点、时间、成败、命题、来源、感知者与支持链。查询这些字段判断事实、承诺、目击与变化；文本只由 S02 渲染。玩家可见查询另经本人认知和来源过滤，不能直接开放权威事件日志。

**关联处理：** S11 中痕迹相等性须采用结构化值；`AWK-038` 所在规划器的文案 cue 匹配也应改为本组命题/来源查询。它们保留在案件主归属中，不另列重复方案。

**完成标准：** 改对白措辞、标点、人名显示及译文后，事件地点、嫌疑、承诺履行、证物比较和行动选择不变；只改变结构化事实才改变结果。缺少来源的信息不能被查询者当作亲见。

### S09：通用 AST 执行规矩与社会请求

**对应标记：** `CH2-006、010、011、055、056`。

**引用原文：** [本组全部 5 条摘录](solution-evidence.md#s09)。

**`CH2-010`：封闭的具体行为编号清单**（`production` / `schema`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2DesignRules.cs](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2DesignRules.cs:120)，L120–L120。

```csharp
    private static readonly string[] BehaviorIds = ["取粮", "按人头", "交钥匙", "指控须证", "转述", "开库各取", "开库委员会", "开库封闭", "开库约束", "限制入院", "温室"];
```

**`CH2-010`：加载时还要求剧本行为与该清单同序**（`production` / `schema`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2DesignRules.cs](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2DesignRules.cs:150)，L150–L150。

```csharp
        if (!data.Behaviors.Select(b => b.Id).SequenceEqual(BehaviorIds)) throw new InvalidDataException("规矩格的行为顺序与代码认的编号不符：" + string.Join("、", data.Behaviors.Select(b => b.Id)));
```

**`CH2-006`：按规则卡编号选择本案夜间取粮条件**（`production` / `schema`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Rules.cs](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:156)，L156–L165。

```csharp
        var who = SubjectCondition(card.Subject);
        Condition cond = card.Cond switch
        {
            1 => fx.At(Slot.Night),
            // “急病时可取”：病人碗里已经有粥，就不必再取。
            2 => Fx.All(fx.At(Slot.Night), fx.AnySick(null), Fx.Not(fx.Tg(Bowl, T.Porridge))),
            3 => Fx.All(fx.At(Slot.Night), Fx.Not(Fx.Mine(T.Fed))),
            4 => Fx.Not(fx.At(Slot.Night)),
            _ => Fx.Constant
        };
```

特定规则卡编号、行为白名单、语义位、社会行动和介入结果依赖本案模型。可保留 Core 社交评估和菜单只读预评估；需要外置的是请求内容、相合依据与实际效果。

**推荐方案：** 剧本声明完整规则、接受条件、目的、成本、可行行动与候选请求，编译为已有 `Condition/Effect/Rule`。通用执行器提交请求、记录接受或拒绝、结算时间、处理承诺与失败。共享同一个相合计算，预览不写入认知，实际执行重新检查当前处境。

**方案限制：** 不能把“取粮/开库”等专用白名单换成注册了一组同名 C# 回调；也不能把社交预估分大于零等同于已经执行动作，直接提交后果。规则接受与行动执行分别记录。

**完成标准：** 新增规则与同类社会行动只改剧本；未知 AST 或未实现操作明确报错。请求拒绝、取消、条件变化及无法完成均不发放成功效果，预览与执行无副作用混用。

### S10：区分历史、制度钟点与人物当前行动

**对应标记：** `AWK-010`；`CH2-027、043、061、067、069`。

**引用原文：** [本组全部 6 条摘录](solution-evidence.md#s10)。

**`CH2-067`：特定时刻、人物信任阈值直接提交盯视后果**（`production` / `logic`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:743)，L743–L755。

```csharp
        else if (Day == CaseNight && Slot == Slot.Evening)
        {
                // 沃德当众盯视雀儿出自他自己的成见：他心里对雀儿的信任低到 -2 以下才会这样（不是作者指定）。
                var ward = this[P.Ward];
                var quer = this[P.Quer];
                if (TrustIn(ward, quer) > -2) return;
                Set(quer.Body, T.Shamed, 1);
                Rt.Sync();
                Observation.Observe(quer.Soul, new ObservationInput(quer.Self.Name, quer.Self.Kind)
                { Target = quer.Self, Source = ObservationSource.Introspection, Time = Now, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(T.Shamed, 1) });
                Feed.Add("傍晚，沃德在众人面前盯着雀儿看了很久。雀儿转身走了。");
                Touch(quer.Body, T.Shamed, RecordEvent(P.Ward, "当众盯着雀儿看了很久", "看守", Where(ward), [],
                    reason: $"沃德心里对雀儿的信任 {TrustIn(ward, quer)}"), P.Ward);
```

**`CH2-067`：同一时序分支同时发病、移人和授予消息**（`production` / `logic`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:757)，L757–L769。

```csharp
        else if (Day == CaseNight && Slot == Slot.Afternoon)
        {
                var moran = this[P.Moran];
                Set(moran.Body, T.Sick, 1);
                moran.Body.Get<SpatialProperty>()!.Space = Space[Place.Clinic];
                Rt.Sync();
                var selma = this[P.Selma];
                NewsFrom(selma, NewsKind.Fact, K("发烧", selma.Id, moran.Id), about: moran, on: moran.Body, tag: T.Sick, value: 1);
                Know(selma, moran.Body, T.Sick, 1, ObservationSource.Perception);
                // 压力而非节点（§11 第 9 条）：作者保证茉拉知道莫兰在烧——她的夜取规则与沃德的“无记录即私取”必然相撞。
                if (Options.BlockNews is null || !Options.BlockNews(this[P.Mora], new NewsItem { Id = 0, Kind = NewsKind.Fact, Claim = K("发烧", P.Selma, P.Moran), Origin = P.Selma, FactTag = T.Sick }))
                    Know(this[P.Mora], moran.Body, T.Sick, 1, ObservationSource.Testimony, "赛尔玛把莫兰送来诊疗所，叫了茉拉");
                Log.Add($"D{Day} 下午 莫兰发烧，被送去诊疗所（作者固定）");
```

第一章昨夜时序与第二章课程、播报、病例、点名混用定时分支。迁移前必须先按设计分类，不能把所有 `Day/Slot` 条件一概删除或一概搬入流程。

**推荐方案：** 作者写定的过去声明为历史输入；课、配给等制度钟点由现有阶段/流程调度；疾病、饥饿等世界过程交 S06 法则。当前盯视、送医、告知、回应及接受规则由人物决策与传播产生。教师策略同样依据其所知、目的与契约条件选择适用话语。

**已确认需重点处理：** [FixedBefore](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:730) 将疾病、搬到诊疗所、转述消息和具名人物盯视一起提交。存在信任阈值不等于已经经过人物选择；疾病发生可以作者定，谁搬运、谁告知仍需行动与来源。

**完成标准：** 跨过多个时刻、阶段重新进入时不重复效果；疾病可以发生而人物作出不同回应；请求被拒或人物不在场时，不播放或登记其已完成的行动。课程文案归 S02，不在 C# 重列课文。

### S11：数据驱动案件、重演和证据判定

**对应标记：** `AWK-003、004、005、037、038、039、061`；`CH2-003、025、030、032、049、071、086、087`。

**引用原文：** [本组全部 15 条摘录](solution-evidence.md#s11)。

**`CH2-086`：专用痕迹字段与依赖描述文字的相等判定**（`production` / `schema`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Case.cs](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:11)，L11–L17。

```csharp
public sealed record CaseTraces(int LedgerShort, bool LatchFixed, bool LatchToolMarks, bool BowlPorridge, IReadOnlyList<Place> CrumbDorms, IReadOnlyList<Build> Figures)
{
    public static readonly string[] Labels = [.. CaseDesign.Traces.Select(t => t.Label)];
    public static readonly Place[] Dorms = [Place.DormW, Place.DormLife, Place.DormS, Place.DormL];
    /// <summary>值相等：两组痕迹描述出来完全相同即相等（列表字段按内容比较）。</summary>
    public bool Equals(CaseTraces? other) => other is not null && Describe().Select(d => d.Text).SequenceEqual(other.Describe().Select(d => d.Text));
    public override int GetHashCode() => string.Join("|", Describe().Select(d => d.Text)).GetHashCode();
```

**`CH2-087`：四变量、默认真相和固定取值域**（`production` / `schema`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Case.cs](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:33)，L33–L40。

```csharp
public sealed record CaseVars(int MoraBelief, int QuerRule, int MargaretRule, int QuerSupper)
{
    public static readonly string[][] Options = [.. CaseDesign.Variables.Select(v => v.Options)];
    public static readonly string[] Questions = [.. CaseDesign.Variables.Select((v, i) => $"V{i + 1} {v.Question}")];
    public static readonly CaseVars Truth = new(0, 0, 0, 0);
    public static IEnumerable<CaseVars> All()
    {
        for (var a = 0; a < 3; a++) for (var b = 0; b < 3; b++) for (var c = 0; c < 2; c++) for (var d = 0; d < 2; d++) yield return new(a, b, c, d);
```

**`AWK-039`：第一章答辩判定固定为四步**（`production` / `schema`）。

**原文位置：** [godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs](../../godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs:178)，L178–L181。

```csharp
            && _arguments.Count == 4 && _arguments.Select(a => a.Step).Order().SequenceEqual(Enumerable.Range(1,4))
            && _arguments.All(a => a.Evidence.Any(e => content.Session.GetProgress("examined").GetValueOrDefault(e) is true)
                && (a.RequiredGroup.Length == 0 || content.Session.GetProgress(a.RequiredGroup).GetValueOrDefault(a.RequiredKey) is true)
                && (a.Variable != -1 || _detectiveBolted));
```

六变量、末项隐藏、四步答辩、固定痕迹字段及四项第二章变量是一组案件结构问题。当前存在可复用的 `CaseModel/CaseBelief/Reasoner`，建议以其为计算基础。仅套一个“通用案件”类包住原硬编码不够。

**推荐方案：** 剧本声明变量、取值域、前提修改、隐藏条件、命题、痕迹字段、证物来源及论证支持规则。运行时枚举声明的取值域，复制隔离世界并只修改指定处境/信念，调用同一世界法则和决策机制，比较玩家已知的结构化痕迹。当前案件来自实际发生的事件；原始真相可作为历史声明，不能成为当前事件兜底答案。

**已确认的方案风险：** [CaseTraces.Equals](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:10) 按 `Describe().Text` 比较痕迹，`LatchToolMarks` 不进入该描述；[CaseVars](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:32) 固定四变量域和零值 Truth；第一章答辩在 [DetectivePlanning](../../godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs:177) 固定四步。本组须借 S08 改成完整结构化值比较，按剧本声明决定哪些字段纳入案件，不能在搬迁中丢字段或放宽答案。

**完成标准：** 改变量数量、顺序或答辩步骤不改运行代码；未知证物不算吻合；论证凭作答者本人知道且支撑命题的任意有效材料。重演不改现实或跨章对象，原有解空间与新模型的一致性分别检查；无案或其他危机也有合乎实际状态的流程。

### S12：真实集会与个人接受驱动对质、议事

**对应标记：** `CH2-004、047、088、089、090、091`。

**引用原文：** [本组全部 6 条摘录](solution-evidence.md#s12)。

**`CH2-088`：对质开始把非病者全部移到中央厅**（`production` / `logic`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Case.cs](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:231)，L231–L237。

```csharp
    public void Confront(ConfrontPlan? plan, bool reopen = false)
    {
        var room = People.Where(p => Get(p.Body, T.Sick) == 0).ToArray();
        foreach (var p in room) p.Body.Get<SpatialProperty>()!.Space = Space[Place.Hall];
        Rt.Sync();
        PerceiveAll();
        var quer = this[Accused ?? P.Quer];
```

**`CH2-047`：议事开始重复同一直接移人逻辑**（`production` / `logic`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Council.cs](../../godot_csharp/scenes/Chapter2/Prototype/Council.cs:128)，L128–L133。

```csharp
    public Ending Council(int detBacks = -1)
    {
        var room = People.Where(p => Get(p.Body, T.Sick) == 0).ToArray();
        foreach (var p in room) p.Body.Get<SpatialProperty>()!.Space = Space[Place.Hall];
        Rt.Sync();
        var plans = ArchivePlans().ToArray();
```

议事资格、三站分工、洗清/排斥、重开对质与结果决定集中归到一个社会过程方案。[Confront](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:230) 和 [Council](../../godot_csharp/scenes/Chapter2/Prototype/Council.cs:127) 会把所有非病者直接移到中央厅；前者还用某个固定人物是否改观决定洗清结果，不能只外置开场文字。

**推荐方案：** 剧本声明集会邀请、地点、议程、议案、各人的适用反应、公开提交要求及结果条件。集会参与、搭手、退让、搬离和返回是实际行动；到场者感知材料后各自决定是否改变认知、接受规矩。世界结果由真实支持、资源、技能及并行工位产生，章末卡按 S02 表达。

**方案限制：** 不能按“健康名单”瞬移全员，再统一写支持/信任；缺席者、拒绝者与来不及到场者都必须保留自己的状态。机构判定和个体反应分别记录，不把过半数理解为人人接受。

**完成标准：** 更换被指控者、缺席者、持钥人或工位参与者后，流程仍适用；未知材料不能说服，缺人/迟到不能开机关；已有接受者之外的人不被强行写入规矩，结尾反映实际后果。

### S13：现场身份和资产关联改为表现声明

**对应标记：** `AWK-013、014、041、042、043、044、058、059`；`CH2-024、053、054、083、100`；`CSH-033、035`。**资源：** `RES-081–088`。

**引用原文：** [本组全部 23 条摘录](solution-evidence.md#s13)。

**`AWK-059`：人物键固定对应到具体现场节点**（`production` / `schema`）。

**原文位置：** [godot_csharp/scenes/Awakening/AwakeningMap.gd](../../godot_csharp/scenes/Awakening/AwakeningMap.gd:15)，L15–L15。

```gdscript
const ACTOR_NODES := {"detective": "侦探", "huo": "工程师", "shen": "厨师", "su": "音乐家"}
```

**`CSH-033`：按特定纹理路径识别油灯和柜体**（`production` / `schema`）。

**原文位置：** [godot_csharp/scenes/Shared/Field/WorldView.gd](../../godot_csharp/scenes/Shared/Field/WorldView.gd:235)，L235–L243。

```gdscript
		# nightstand 原生 20×36 纹理同时画了柜体和油灯，分别按像素外观判断入镜。
		if sprite.texture.resource_path == "res://ShareAsset/Internal/Props/FirstAwakeningInterior/nightstand.png" and sprite.get_rect().size == Vector2(20, 36):
			var texture_origin := sprite.get_rect().position
			var lamp_rect := Rect2(texture_origin + Vector2(6, 4), Vector2(8, 13))
			var cabinet_rect := Rect2(texture_origin + Vector2(1, 16), Vector2(18, 20))
			if not _player_object_exposed(sprite.get_global_transform_with_canvas() * lamp_rect, occluded_regions).is_empty():
				var lamp_title := FieldVocabulary.word("床头油灯")
				counts[lamp_title] = int(counts.get(lamp_title, 0)) + 1
			if _player_object_exposed(sprite.get_global_transform_with_canvas() * cabinet_rect, occluded_regions).is_empty(): continue
```

**`RES-085`：生成现场固定绑定侦探动画资源**（`production` / `schema`）。

**原文位置：** [godot_csharp/scenes/Awakening/Awakening.tscn](../../godot_csharp/scenes/Awakening/Awakening.tscn:8)，L8–L8。

```text
[ext_resource type="SpriteFrames" path="res://ShareAsset/Internal/Characters/PixelCast/detective.tres" id="detective_frames"]
```

这里不能一律按剧情因果删除：像素坐标、管线形状、纹理引用、复合家具局部边界都属于表现实现。需要处理的是具体人物/物证/地点与这些几何、资源或例外的绑定。

**推荐方案：** 在现有剧本布局、人物画像和地图适配数据中声明稳定 ID 到节点/资产键/锚点的关联，纯几何留在资产或地图源。痕迹显示消费权威快照和当前可见范围，不能因“R05”或精灵名称直接决定那里有什么痕迹。复合精灵可声明局部区域与词汇键，替代纹理路径特判。非空间实体以通用属性明确表示表现锚点，替代对 Clock/NameVoice 的具体豁免。

**方案限制：** 保留合法的 Godot 节点、动画、碰撞与坐标代码；不要为了消掉标记把绘图算法搬进剧情，或把资产 ID 当作 NPC 的知识/动作答案。站点 kind 属于支持能力契约，要区分通用类型与专案语义。

**完成标准：** 改人物或物证 ID、替换同类纹理不改共享视图；缺失绑定明确报错；地图/门所有者/回放站位一致。资源生成从源执行，真实查看迷雾、遮挡、碰撞、悬停与演员动作。

### S14：资源生成器读取剧本显示内容

**资源：** `RES-001–080`；无新增源码标记编号。

**引用原文：** [本组全部 80 条摘录](solution-evidence.md#s14)。

**`RES-001`：生成地图的具体地点标题**（`production` / `schema`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/archive.tscn](../../godot_csharp/scenes/Chapter2/Maps/archive.tscn:188)，L188–L188。

```text
metadata/title = "名录库·文献工房"
```

**`RES-002`：生成地图的具体热点标签**（`production` / `schema`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/archive.tscn](../../godot_csharp/scenes/Chapter2/Maps/archive.tscn:615)，L615–L615。

```text
metadata/label = "库门锁芯"
```

**`RES-072`：生成布局的固定环境短句**（`production` / `text`）。

**原文位置：** [godot_csharp/scenes/Awakening/map_layout.tres](../../godot_csharp/scenes/Awakening/map_layout.tres:947)，L947–L947。

```text
   "窗。外面是灰的，什么也看不清。"
```

**资源生成上游：上述环境短句的手写生成源**（关联 `RES-072–080`，无新增标记）。

**原文位置：** [godot_csharp/tools/build_awakening_map.py](../../godot_csharp/tools/build_awakening_map.py:193)，L193–L200。

```python
    for d in room["decor"]:
        if d[0] == "window":
            cx, top, w = d[1], d[2] * CELL, d[3]
            asides.append([[cx - w // 2, top + 6, w, 36], "窗。外面是灰的，什么也看不清。" if cx > 600 else "窗。雾贴着玻璃。"])
    for fw in room["floor_work"]:
        if fw[0] == "rug":
            asides.append([[fw[1], fw[2], fw[3], fw[4]], "地毯。踩得发亮的地方，在床边。" if fw[1] == 832 + 72 else "地毯。"])
    asides.append([[832 + 136, 168, 9, 8], "一双拖鞋。摆得很正，鞋头朝外。"])
```

`RES-001–071` 是生成地图的标题/热点标签；`RES-072–080` 是布局里的环境短句。它们不能按 80 条独立建议逐处修 `.tscn/.tres`。

**推荐方案：** 改 [build_chapter2_scenes.py](../../godot_csharp/tools/build_chapter2_scenes.py:117)、其地图数据源及 [build_awakening_map.layout_data](../../godot_csharp/tools/build_awakening_map.py:184)：坐标、碰撞和纯资产摆位仍由地图源提供，标题、标签与环境短句统一取剧本编译数据/词汇键。以 map/site/object 的稳定 ID 合并，避免靠像素坐标决定哪句文字。随后重新生成地图、manifest 和布局，再导入。

**已确认的上游残留：** `layout_data` 按坐标直接选择窗、地毯和拖鞋文案，并按书柜名确定手印种类；后者同时属于 S13 的痕迹表现条件。原 205 份手写 .cs/.gd 审计不包含这些 Python 上游，不能因生成资源未手改就认为来源合规。

**完成标准：** 改一处剧本名或译文，生成标签/运行显示一致；所有生成字段可追溯。地图仍保留其纯美术数据，无手改生成物，正式热点标题不被旧 metadata 覆盖。

### S15：声音配置声明附加音效

**对应标记：** `CSH-022`。

**引用原文：** [本组全部 1 条摘录](solution-evidence.md#s15)。

**`CSH-022`：以铜口声流身份触发附加音效**（`production` / `logic`）。

**原文位置：** [godot_csharp/scenes/UI/Panels/Dialogue/ChapterDialogue.gd](../../godot_csharp/scenes/UI/Panels/Dialogue/ChapterDialogue.gd:556)，L556–L558。

```gdscript
	if current_voice_stream == VOICE_TUBE and _previous_voice_stream != VOICE_TUBE:
		Sound.sfx("tube", -6.0)
	_previous_voice_stream = current_voice_stream
```

[ChapterDialogue._present_line](../../godot_csharp/scenes/UI/Panels/Dialogue/ChapterDialogue.gd:555) 根据“当前声流是否是铜口”推断首次开口时的附加音效。这是专用声音表现绑定，优先级低于人物因果改造。

**推荐方案：** 由剧本/声音表现配置提供开口音效、语音组和连续组标识。UI 只按配置与呈现生命周期播放、去重；也可由已有 `sound` 事件明确触发。保持打字音、音量及播放算法在声音系统中。

**完成标准：** 相同语音纹理可用于不同发声源；首次开口音效只在声明的组开始播放，跳过/回看/重复回执不多播，不能通过声音类型替人物提交剧情结果。

### S16：按作用域结束章节规则

**对应标记：** `AWK-060`。

**引用原文：** [本组全部 1 条摘录](solution-evidence.md#s16)。

**`AWK-060`：章节交接统一清空旧物习惯的规则**（`production` / `logic`）。

**原文位置：** [godot_csharp/scenes/Awakening/Night/MorningScriptWorld.cs](../../godot_csharp/scenes/Awakening/Night/MorningScriptWorld.cs:59)，L59–L66。

```csharp
        // 离开生活翼：拿不拿那件旧物的打算系在本章的物件与场所上，到此失效（本人内省得知，不删记忆、不改钩与欲望）。
        // 否则其中“守着手头的事”等常数偏好会带进下一章，压过那里的一切日常。
        foreach (var who in _minds.Keys)
        {
            var habit = _minds[who].Habits;
            if (habit is not null)
                Observation.Observe(_souls[who], new ObservationInput(habit.Name, habit.Kind) { Target = habit, Source = ObservationSource.Introspection, Time = ++_time, Rules = [] });
        }
```

[ExportContinuation](../../godot_csharp/scenes/Awakening/Night/MorningScriptWorld.cs:51) 为避免旧物习惯压过续章日常，统一清空该习惯投影的 Rules。这是跨章规则生命周期问题，不能只搬迁四个人的清理名单。

**推荐方案：** 为规则与触发器声明 world/chapter/interaction 有效范围和退出条件。章节退出只撤销章节局部规则；人物习惯、已接受的持久规矩、记忆与欲望继续存在。若规则因物件/处境改变自然失效，优先让其适用条件失效；需要撤销时经明确效果与认知来源记录。

**方案限制：** 不能全部清空人物规则，也不能在新章按模板重建同一人。规则 scope 应与现有实体/候选 scope 区分实现，不能假定已有全部生命周期能力。

**完成标准：** 跨章持久习惯保持、局部规则精确退出，既有物件和认知引用不变；未完成动作交接、重复退出和重复加载不会丢状态或重复效果。

### S17：整理测试夹具与固定路线采集器

**对应标记：** `AWK-045–054、056、057`；`CH2-002、016–022、077`；`CSH-007、010–013、015、016、019、020、036、037`。

**引用原文：** [本组全部 32 条摘录](solution-evidence.md#s17)。

**`AWK-045`：测试夹具中的具体真值和显示名称**（`fixture` / `schema`）。

**原文位置：** [godot_csharp/scenes/Awakening/check/ReplayCheck/ReplayCheckProgram.cs](../../godot_csharp/scenes/Awakening/check/ReplayCheck/ReplayCheckProgram.cs:20)，L20–L27。

```csharp
var truth = new NightVariables(SuPlace.OwnRoom, HuoPlace.ByTube, VoiceOrigin.Tube, VoiceWords.BoltKeepShut, DetectiveHeeds.Huo, Acquaintance.Familiar);
string Short(NightVariables v) => string.Join(" ",
    v.V1 switch { SuPlace.OwnRoom => "苏泠在房", SuPlace.Hall => "苏泠在厅", _ => "苏泠在备餐间" },
    v.V2 switch { HuoPlace.ByTube => "霍铎在铜口", HuoPlace.OwnRoom => "霍铎在房", _ => "霍铎在门厅" },
    v.V3 == VoiceOrigin.Tube ? "经铜口" : "在门外",
    v.V4 == VoiceWords.BoltKeepShut ? "闩门别开" : "开门救我",
    v.V5 switch { DetectiveHeeds.Huo => "听霍铎", DetectiveHeeds.Su => "听苏泠", _ => "听自己" },
    v.V6 == Acquaintance.Familiar ? "相熟" : "初识");
```

**`CSH-012`：记录工具按预写答辩与证物映射代选**（`tool` / `logic`）。

**原文位置：** [godot_csharp/tools/PlayerTranscript/Program.cs](../../godot_csharp/tools/PlayerTranscript/Program.cs:169)，L169–L175。

```csharp
            else if(fields.Any(f=>S(f,"id")=="say"))
            {
                string[] texts=["是我闩的门。","门外没有人。那声音是从主楼对着铜口说的，从管接头漏到了门边。","他说的是：闩门，别开。","我信霍铎——我们早已相熟。"];
                string[] shows=["E3","E8","E7","E9"];
                var say=Maps(fields.First(f=>S(f,"id")=="say"),"options").Single(o=>S(o,"label")==texts[answer]);
                values["say"]=say["id"];values["show"]=shows[answer++];action="submit";
                var evidence=Maps(fields.First(f=>S(f,"id")=="show"),"options").Single(o=>S(o,"id")==shows[answer-1]);
```

这组共 32 处，覆盖 20 处 fixture 和 12 处 tool。固定答案、预置状态、选项策略与 UI 自动推进属于测试/采集输入，不应误判成正式 NPC 决策，也不能用作盲玩证据。

**推荐方案：** 通用测试/驱动代码只负责输入、等待与断言；具体故事路线、台词预期与情境移入显式测试用途的 `.story` 或译文源，按稳定 ID 查询。机制测试用中性构造验证合同。黄金预期由独立审阅后封存，不能运行时从同一份 TRUTH/同一算法计算期望值，避免实现与预期一起出错仍通过。

**方案限制：** 不删掉穷举、认知隔离、跨章及 UI 状态断言；不要把测试文件搬个目录就保留全部剧情字符串。按正式行 ID 定位可以保留，但不能以自然语言匹配驱动剧情。自动模拟器即使只读公开菜单，也仍是自动工具。

**完成标准：** 构建引用核实 fixture 与开发代码没有进入正式调用链；剧情迁移后已有回归仍能暴露错误。原画廊缺失依赖另记为旧失败，启用它作为证据前先修复入口与依赖。路线记录、UI 回放、自动回归与独立盲玩保持各自证据身份。

### S18：UI 回放与采样器消费正式绑定

**对应标记：** `CSH-006、024、025、026、027`。

**引用原文：** [本组全部 5 条摘录](solution-evidence.md#s18)。

**`CSH-025`：回放器重复维护固定地图到地点的表**（`tool` / `schema`）。

**原文位置：** [godot_csharp/tools/capture_player_transcript.gd](../../godot_csharp/tools/capture_player_transcript.gd:27)，L27–L31。

```gdscript
const HOST_MAP_PLACES := {
	"central_hall": "Hall", "lecture": "Lecture", "logistics": "Logistics", "granary": "Logistics", "clinic": "Clinic",
	"greenhouse": "Greenhouse", "archive": "ArchiveFront", "counterweight": "ArchiveFront", "campus_s": "Oath", "living_hall": "DormLife",
	"zone_yi": "DormLife", "zone_jia": "DormW", "zone_bing": "DormS", "zone_ding": "DormL", "resonance": "Resonance",
}
```

**`CSH-024`：截图采样器固定选择两章具体场面**（`tool` / `schema`）。

**原文位置：** [godot_csharp/tools/capture_story_ui_samples.gd](../../godot_csharp/tools/capture_story_ui_samples.gd:9)，L9–L17。

```gdscript
const SAMPLE_SPECS := [
	{"slug": "01-c1-long-dialogue", "kind": "lines", "line_prefix": "s2_meet.", "prefer_longest": true},
	{"slug": "02-c1-person-dialogue", "kind": "lines", "line_prefix": "s2_tea.", "require_speaker": true, "prefer_longest": true},
	{"slug": "03-c1-evidence-card", "kind": "card", "card_id": "E1"},
	{"slug": "04-c1-defense-form", "kind": "form", "layout": "tabs", "field_ids": ["say", "show"]},
	{"slug": "05-c2-short-dialogue", "kind": "lines", "line_prefix": "chapter2.noon_dispute.", "require_speaker": true, "prefer_shortest": true},
	{"slug": "06-c2-council-options", "kind": "candidates", "option_prefix": "support:"},
	{"slug": "07-ending", "kind": "ending", "prefer_last": true},
]
```

这些工具有固定地点映射、场景复原、样本选择及开场节点识别。正式输出的重放与按截图阶段筛样本是两个不同职责。

**推荐方案：** 回放复用 S13 的正式地图/演员/热点绑定，消费已记录的公开快照和语义事件，不另列地点表或推测玩家站位。采样计划用显式测试剧本/稳定场面 ID 声明；通过事件类型、关联 ID 和表现完成信号推进。辅助截图选择不能左右完整 UI 文本回放和终局导出。

**方案限制：** `_apply_context` 消费真实快照本身合法，重点是消除重复绑定及缺失映射时的推测。不能用答案策略或旧输出补造新一轮 UI 记录。

**完成标准：** 新增同类地图无需改采集器；无截图也能完成一级回放；缺图/缺节点/终局未绘制如实失败。图文记录都来自实际呈现并保留原始证据。

### S19：示例内容外置、预览数据中性化

**对应标记：** `CSH-008、014、017、018、021、038`。**资源：** `RES-089–097`。

**引用原文：** [本组全部 15 条摘录](solution-evidence.md#s19)。

**`CSH-014`：编辑器笔记预览写入具体案件样例**（`preview` / `text`）。

**原文位置：** [godot_csharp/scenes/UI/Panels/Notebook/Notebook.gd](../../godot_csharp/scenes/UI/Panels/Notebook/Notebook.gd:25)，L25–L28。

```gdscript
@export var preview_sections: Array[Dictionary] = [
	{"heading": "[b]看过的[/b]", "style": "grid", "entries": [{"label": "窗台的水渍", "card_id": "E1"}, {"label": "桌上的钥匙", "card_id": "E2"}, {"label": "门锁", "card_id": "E3"}]},
	{"heading": "[b]读过、听过的规矩[/b]", "style": "list", "entries": [{"label": "夜里不得离开房间。"}, {"label": "（还没听说第二条）", "muted": true}]},
]:
```

**`RES-089`：工作台预览中的固定昨夜问答**（`preview` / `text`）。

**原文位置：** [godot_csharp/scenes/UI/Workbench/UIWorkbench.tscn](../../godot_csharp/scenes/UI/Workbench/UIWorkbench.tscn:184)，L184–L184。

```text
preview_question = "提问者问：[b]昨夜是谁关的灯？[/b]"
```

代码示例、固定实体预览、编辑器笔记、地图节点和旧对白/工作台样例属于同一内容维护问题；不能混入正式世界的默认真相。

**推荐方案：** 有示例故事的 `ExampleWorld` 改为加载独立示例 `.story`，已有 `.world` 示例同步检查作者源格式与编译入口。作者预览从示例声明枚举角色/对象，不预设“侦探/内门闩”。纯 UI 编辑器预览改用中性占位和结构样本；手写 `.tscn` 中对应预览文字直接在源场景修改。

**完成标准：** 编辑器不出现正式案件答案；示例仍能展示声明、继承和交互机制；正式会话必须收到宿主数据后显示，不回落到样例内容。需要故事文字的示例仅从 `.story` 和译文读取。

### S20：保留资产造型与姿态生产配方

**对应标记：** `CSH-028、029、030、031`。

**引用原文：** [本组全部 4 条摘录](solution-evidence.md#s20)。

**`CSH-028`：早餐资产工具只执行纹理裁切、缩放和保存**（`tool` / `schema`）。

**原文位置：** [godot_csharp/tools/build_breakfast_exchange.gd](../../godot_csharp/tools/build_breakfast_exchange.gd:25)，L25–L36。

```gdscript
	var table := Image.load_from_file(ProjectSettings.globalize_path("res://AssetsSource/Internal/Props/breakfast_table_clear.png"))
	table = table.get_region(bounds(table))
	table.resize(224, 44, Image.INTERPOLATE_NEAREST)
	var canvas := Image.create_empty(224, 64, false, Image.FORMAT_RGBA8)
	canvas.blit_rect(table, Rect2i(Vector2i.ZERO, table.get_size()), Vector2i(0, 8))
	assert(ResourceSaver.save(ImageTexture.create_from_image(canvas), "res://ShareAsset/Internal/Props/breakfast_table_clear.res") == OK)
	var soup := Image.load_from_file(ProjectSettings.globalize_path("res://AssetsSource/Internal/Props/breakfast_soup_bowl_source.png"))
	soup = soup.get_region(bounds(soup))
	soup.resize(12, 9, Image.INTERPOLATE_NEAREST)
	var bowl := Image.create_empty(16, 16, false, Image.FORMAT_RGBA8)
	bowl.blit_rect(soup, Rect2i(Vector2i.ZERO, soup.get_size()), Vector2i(2, 3))
	assert(ResourceSaver.save(ImageTexture.create_from_image(bowl), "res://ShareAsset/Internal/Props/breakfast_exchange_bowl.res") == OK)
```

**`CSH-030`：具体资产名单只用于生产道具与动作资源**（`tool` / `schema`）。

**原文位置：** [godot_csharp/tools/build_story_tiles.gd](../../godot_csharp/tools/build_story_tiles.gd:34)，L34–L38。

```gdscript
	if "--breakfast-only" in OS.get_cmdline_user_args():
		build_compact_story_props(["breakfast_table", "prep_island", "closed_door", "side_table"])
		build_breakfast_bowl()
		preload("res://tools/build_breakfast_exchange.gd").build()
		quit()
```

早餐交换、角色姿态、道具图集和书房生产器描述具体资产的像素造型、摆位及动作帧。已标记绑定，但该内容本身不是运行中的案件逻辑或人物决策。

**推荐方案：** 保留资产生产配方及纯绘制/切片代码，按现有 AssetsSource/ShareAsset 归属管理。故事里的“何时由谁使用哪个资产、说什么、为何行动”由 S07/S13 绑定；如果工具中另有实际对白或案件判断，再按相应组外置。

**完成标准：** 工具只生成视觉资源，不参与权威会话、答案计算或人物选择。不为消除计数而把像素配方搬进剧本；标记可以继续作为资产来源记录，不能据此认定未修复剧情缺陷。

### S21：剧本声明通行状态及其传播

**对应标记：** `CH2-009`。

**引用原文：** [本组全部 1 条摘录](solution-evidence.md#s21)。

**`CH2-009`：释放宿舍同时把区内 NPC 直接移到起居厅**（`production` / `logic`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2Access.cs](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2Access.cs:29)，L29–L35。

```csharp
    public void HostDormOut(string name)
    {
        var dorm = new[] { Place.DormLife, Place.DormW, Place.DormS, Place.DormL }.Single(p => Names.Of(p) == name || p.ToString() == name);
        if (!_releasedDorms.Add(dorm)) return;
        foreach (var p in Npcs.Where(p => p.Pro.Dorm == dorm)) p.Body.Get<SpatialProperty>()!.Space = Space[Place.Living];
        RefreshAccess(); SyncChapterRooms(); PerceiveAll();
        FreeEvents.Add(new FreeEvent.DormOut(SlotIndex, name));
```

**`CH2-009`：通行刷新向已有认知投影的人物观察所有通路**（`production` / `logic`）。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2Access.cs](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2Access.cs:49)，L49–L53。

```csharp
        foreach (var p in People.Where(p => p.M.Count > 0))
        {
            var routes = Rt.Observe(p.Soul, Way.Values.Select(w => View[w]), Now);
            var i = 0; foreach (var way in Way.Values) p.M[way] = routes[i++];
        }
```

[Chapter2Access](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2Access.cs:8) 已集中同步通行状态，值得保留；问题是具体宿舍释放集合、走廊分组和人物出区操作固定。它与 S13 的“如何画/绑定门”不同，是权威世界权限与知识来源问题。

**推荐方案：** 从剧本声明实际通路、边界、初始状态及开放条件，由通用提交更新现实并输出 access。NPC 通过当场观察、已知通路复查或消息获得变化，UI 快照只读权威状态。开门与人走出来分开：`HostDormOut` 不能仅因房间释放就把所有人直接移到起居厅。

**已确认需核对的传播：** `RefreshAccess` 对 `M.Count > 0` 的每个人调用所有通路的观察；改造时必须核实哪些变化公开可闻、哪些需亲见，不能默认远方人物立即知道所有通路。

**完成标准：** UI、物理通路与尝试行动的现实可行性一致；人物可保留旧认知并在失败/观察后更新，不能靠客观权限替人物选择。实际通路拓扑不被全连接映射抹平，改宿舍/走廊身份不改代码。

## 建议实施顺序与复核

1. 先清理 S01，并建立 S03 的稳定登记、S08 的事件事实以及 S02 的语义表达合同。这些是其他组共同依赖，逐个模块迁移，避免一次替换全部世界。
2. 接着迁移 S09、S04、S05、S06 和 S21，补齐规则执行、个体认知与世界法则，再处理 S16 的章节退出。通用能力先用中性样例验证，具体内容从现有剧本迁移，不新编设定。
3. 然后迁移 S07/S10 的流程与时序、S11 的案件/重演、S12 的社会过程；世界选择先成立，呈现再跟随。
4. 最后贯通 S13–S15 的地图/声音生产，以及 S17–S19 的测试与预览。S20 保留合法资产配方。这些组可在相应基础接口确定后穿插实施，不必等所有 P1 全部结束。

依赖不是新架构规范；实际实施涉及架构、工作流或命令调整时，再按现有同步规则更新对应 AGENTS.md。本轮没有改变设计规范或现行管线。

原审核检查清单覆盖、分组唯一性、路径/标记位置与报告链接，独立结果见 [审核清单验证](verification/solution-review-20261009.md)。本次补引用另核对 21 组代表性引文及全部 293 条摘录的原文、位置与主归属，结果见 [具体内容引用验证](verification/solution-quotes-20261009.md)。未重新构建或运行自动回归；原索引中的构建通过和画廊基线失败属于标记轮证据，不能当成本轮改造证据。

| 结论项 | 本轮结论 |
| --- | --- |
| 构建 | 未重新执行；本轮没有程序改动 |
| 机制 | 静态审核完成；21 组为待实施建议，未证明改造行为 |
| 人物行为合理性 | 核对方案是否符合设计边界；未进行独立人物行为验收 |
| 游玩体验 | 未验证 |
| 一级/二级/三级 | 均未执行；本轮不是剧情、UI 或最终游戏效果验收 |

实际迁移后按受影响模块运行构建、编译一致性与既有回归；涉及玩家文字/逻辑对齐时选一级，涉及地图和 UI 组合时选二级并实际查看图像。只有用户明确要求最终效果时才进行三级。任何静态归并通过均不能替代这些验收。

## 实施状态（2026-10-10，整合分支 `claude/core-remediation`）

本节记录按本清单实施后的实际结果。每批由 Codex 在独立 worktree 实现，主代理对照设计审查 diff、运行剧情隔离检索（`tools/check_story_leak.py`）与完整回归后合入；各批报告在 `execution-20261010/<任务名>/REPORT.md`。

### 各组结论

| 组 | 状态 | 落地内容（批次） | 未完成 / 保留 |
| --- | --- | --- | --- |
| S01 | 已解决 | 删除 Core 旧案件执行器（第一阶段） | — |
| S02 | 已解决 | Core 返回来源/理由代码；回应与称呼按说话人认知从剧本表达声明解析；删除全文称呼替换（s02） | — |
| S03 | 部分 | 按用户收窄范围：固定绑定表与集合改为剧本实体登记，第一章六变量取值域由剧本声明（s03） | 存量 `P`/`Place`/`Group` 枚举逐步迁移 |
| S04 | 未做 | — | 待建在人物策略层之上；`codex/ch1-strategy` 为中断的半成品分支 |
| S05 | 未做 | — | 感知与传播法则 |
| S06 | 未做 | — | `codex/s06s10` 为刚开始即中断的半成品分支 |
| S07 | 未做 | — | 第一章交互条件、试验与演出流程 |
| S08 | 已解决 | 类型化事件与权限投影；第二章事实、地点、回应判据改查结构化字段（core-d） | — |
| S09 | 已解决 | 规矩执行与社会请求改为剧本声明的过程 AST，接受与执行分离、预览无副作用（s09） | — |
| S10 | 未做 | — | 同 S06 分支 |
| S11 | 部分 | `CaseTraces` 改全字段比较，修复 `LatchToolMarks` 被忽略的缺陷（core-d） | 变量/重演/答辩结构通用化；InvestigationCheck 与 CognitionCheck 两项旧失败（测试前提过时，主代理已裁定改走法） |
| S12 | 未做 | — | 用户裁定：议事开场可召集到场，表态由人物决定 |
| S13 | 已解决 | 现场身份、资产与锚点关联改为剧本表现声明，缺失绑定报错（s13） | AWK-042 为合法几何，保留 |
| S14 | 已解决 | 地图标题、场点标签、旁白逐字迁入剧本，`--text-only` 入口，产物字节不变（s14） | — |
| S15 | 移交 | — | 属 UI，归 UI 对话 |
| S16 | 已解决 | 规则与触发器按 world/chapter/interaction 有效期撤销，持久习惯保留（s21s16） | — |
| S17 | 已解决 | 测试情境与固定路线外置为夹具，按稳定 ID 驱动，夹具不进正式调用链（s17） | — |
| S18 | 冻结 | 仅为一级验收修正回放工具中起居厅的地点映射 | 其余归 UI 对话 |
| S19 | 部分 | 非 UI 部分：示例改为独立 `.story`，作者预览按声明枚举（s19）；界面平面设计分支合入后 UI 命中 54→8 | UI 内剩余样例归 UI 对话 |
| S20 | 保留 | 资产配方本就合法 | — |
| S21 | 已解决 | 通行由剧本声明；开门不瞬移、改为召集；通路认知按亲见/消息/失败更新（s21s16） | — |

### AR-CORE-01 与新增能力

| 项 | 状态 | 内容 |
| --- | --- | --- |
| A 身份与词项 | 已实现 | 四域稳定身份、中文词项解释桥（core-ab）；用户判定词项桥属过度设计，保留但不再扩展 |
| B 认知版本与保存 | 已实现（首版） | 原子认知提交、不可变快照、审计（core-ab）；版本化认知图 codec 与空闲切点保存/跨进程恢复（core-save、save-events）；活动状态在切点明确拒绝 |
| C 主观推演隔离 | 部分 | 虚拟侦探的主观推理器已有逻辑层假设推演（vplayer）；行动/时空分叉的隔离模拟未做 |
| D 类型化事件 | 已实现 | 五类事件、权限投影、发言≠提交、接受≠执行（core-d） |
| 性能 | 已解决 | core-ab 一度使回归变慢至 2.4 倍，core-perf 回到 1.03–1.06 倍 |
| 虚拟侦探 | 部分 | 剧本声明的代理操控者、可见候选窄接口、headless 驱动、主观论证/假设/信息价值推理、`RuleMode.Infer`（vplayer，已合入）；正式命题接线与 Core 四项改进（`codex/vplayer2` 半成品分支，未完成十种子通关验证） |
| 人物策略与第一章轨道 | 未完成 | 设计已写入总纲 §六.14；实现为中断的 `codex/ch1-strategy` 分支 |
| 回归修复 | 已解决 | 27 项既有 flow/flow2 失败全部修复（fix27） |
| 剧情隔离检索 | 已建 | Core、共享接口、宿主代码命中为 0；UI 8、Shared/UI 15（归 UI 对话）；章节适配层 2285→2039 |

### 一级验收（2026-10-10）：未通过

01（源推导，1078 行）与 02（引擎连续一局，122 个操作，两章到终局）已生成；03 第一章采集完成，第二章因回放工具地点映射过时中止，修正后按用户要求停止采集。已确认的问题：

| # | 问题 | 性质 | 状态 |
| --- | --- | --- | --- |
| 1 | 第二章课表比剧本早一天：同一上午广播《传声与机械》、铜口讲“守则的读法”、回执又是《传声与机械》；首课《守则读法》从未讲授（`Demon.cs` 课表与 `续篇/08_流程.story` 脱节） | 引擎/剧本缺陷 | 未解决 |
| 2 | 案发日台账无缺口时广播“今日无新课”，心声仍引用“昨夜，有人进过后勤院” | 剧本缺分支 | 未解决 |
| 3 | 结局卡对侦探的称呼：第一章“侦探”、第二章“你自己” | 表达不一致 | 未解决 |
| 4 | 第二章终局时仍有人物行动未完成，章节作用域无法结束 | 引擎缺陷 | 已修复（终局收场取消未完成行动并记录） |
| 5 | 正式路线：第 2、3 日走法与新日程不符；到终局后仍执行剩余步骤 | 路线夹具 | 已修复 |
| 6 | 正式路线：`help` 动作在源码中不存在，02 无协助场面；第 3 日时段错位；议事投票投给了别人的方案 | 路线夹具 | 未解决 |
| 7 | “门外的人”菜单可能循环、`s10_open` 两句台词矛盾、`night_hint` 不可达 | 01 推导的疑点 | 未核实 |

完整回归最近一次全部通过于 s09 合入后（scope 27、cognition 372、save、flow 136/136、flow2 109/109）。其后合入的终局修复、路线修正、界面平面设计与人物动作重绘只通过了 core/scope/cognition/host 与剧本一致性检查，**flow/flow2 尚未在当前状态复跑**。

### 一级验收补充（不运行 Godot）

用户要求不再运行 Godot，02 → 03 对照本轮未执行。另由两名全新独立 Agent 完成：

- **01 → 02 对照**（[01-02对照](../basic-ui-system/level1-20261010/01-02对照.md)）：96 段中一致 73 段；第一章全段与第二章场面台词逐字一致。引擎/剧本缺陷 11 条（B-1 至 B-11），源推导失误 6 条，路线夹具问题 5 条；动态项 88 个中 13 个不合理。主代理裁定：B-6（调停失败后连日不开粮却称“人人吃上了早饭”）与 B-11（东廊未开时提示“后勤院有了变化”，越过本人认知）均为缺陷。**不通过。**
- **01 文学评审**（[01-文学评审](../basic-ui-system/level1-20261010/01-文学评审.md)）：55/100，**不通过**。第一章大体达标；主要问题为 E1 闪回提前泄出第一问与第四问的答案、莫兰病程时间线矛盾、课程三方不一致、第二章案件在正常路线上没有揭示与高潮、第二问“门外没有人”与终局折回鞋印未交代先后、阮清越代词错误。

**一级验收结论：不通过。** 待修项按性质：课表与广播（B-3）、案发日账目与心声口径（B-1、B-2）、议事统计句（B-4）、称呼与文字（B-5、B-7–B-10、阮清越代词）、配给与认知越界（B-6、B-11）、文学结构（E1 闪回时机、莫兰病程、第二章案件揭示、门外鞋印时序——后两项须先对照设计规范核定真相时间线）、路线夹具五项。

### 一级验收第二轮（合并三个半成品后，只做 01 与 01↔02）

主代理合并 `codex/vplayer2`、`codex/ch1-strategy`、`codex/s06s10`：补写 12 句牵引台词，去掉临时证书配置；修复合并后暴露的两处缺陷——代理论证/求知权重被静默读成默认值（字段命名两侧修复相互抵消）、“门外的人”菜单无限循环。非 Godot 检查全部通过（core、scope 27、cognition 372、host；主观推理 68、命题 69、策略 57、策略集成 271、代理接口 53）。剧情隔离：Core/接口/宿主 0，适配层 1967。

02（[第二轮](../basic-ui-system/level1-20261010-r2/02-引擎输出的玩家可见动作.txt)）两章到终局，122 个操作。[01↔02 对照](../basic-ui-system/level1-20261010-r2/01-02对照.md)：一致 93 段，02 错 15 条，01 推错 4 条，路线夹具 4 条，动态项 26 个中 8 个不合理。第一章剧本文字逐句一致。**结论：未对齐，不通过。**

新增问题（第一轮未见）：策略牵引的停滞计时把侦探自己做试验的时间算入（B1）；苏泠的邀请没有她见过的物证（B2）；声路试验后同一证物卡连发两次（B5）；侦探请“先别开”后雀儿仍按定时撬门（B6）；撬门演门厅画面而侦探在备餐间，违反视野即认知（B7）；第二日集合旁白称“人到齐了”而丁区未出门（B9）。第一轮遗留仍在：课表与广播（B8）、配给停摆与全员饥饿（B10）、案发日广播与心声（B11）、无缺口仍问台账（B12）、议事结论句（B13）、章末“没有往下想的”语义（B14）、内部物件名外露（B15）、日常旁白缺主语（B3）、菜单前重复上一句（B4）。

