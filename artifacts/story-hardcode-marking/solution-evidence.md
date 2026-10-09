# 剧情硬编码具体内容摘录

引用依据：`b608f5b6` 的源码与资源内容，摘录于 2026-10-09；对应 [解决方案归并清单](solution-review.md) 和 [完整分组](solution-groups.json)。

按原来的 **21 组建议**排列，逐条覆盖 **196 处源码标记和 97 处资源记录**。这里不另开解决建议；同一根因的内容仍在同一组。

源码引用标记后的就近原文，最多 12 行，遇到下一标记则提前结束；它是局部摘录，不是完整方法。资源引用索引指向的原始字段行。源码标记的位置、类别、用途和摘录行号分别列明；类别与理由沿用原索引，不把审计注释当作设计结论。关键判定的更深层语句另在主清单展开。

代码块保留原文，未改写、拼接或补入省略号；语句不完整的摘录需结合链接中的完整实现阅读。这里的具体文案、人物、答案只是在引用现有实现，不新增设定，也不表示这些实现正确或已经修复。生成资源保持原样。

## 分组导航

- [S01 清理 Core 旧案件专用实现](#s01)：5 处源码、0 处资源。
- [S02 语义结果与剧本文案、称呼分离](#s02)：18 处源码、0 处资源。
- [S03 剧本实体登记替代固定名单与专用枚举](#s03)：20 处源码、0 处资源。
- [S04 个体信念、习惯与立场编译为主观规则](#s04)：18 处源码、0 处资源。
- [S05 感知与传播法则驱动认知更新](#s05)：6 处源码、0 处资源。
- [S06 世界法则声明物理后果和劳作配方](#s06)：6 处源码、0 处资源。
- [S07 剧本声明交互条件、试验和演出流程](#s07)：16 处源码、0 处资源。
- [S08 结构化事件替代中文日志反解析](#s08)：10 处源码、0 处资源。
- [S09 通用 AST 执行规矩与社会请求](#s09)：5 处源码、0 处资源。
- [S10 区分历史、制度钟点与人物当前行动](#s10)：6 处源码、0 处资源。
- [S11 数据驱动案件、重演和证据判定](#s11)：15 处源码、0 处资源。
- [S12 真实集会与个人接受驱动对质、议事](#s12)：6 处源码、0 处资源。
- [S13 现场身份和资产关联改为表现声明](#s13)：15 处源码、8 处资源。
- [S14 资源生成器读取剧本显示内容](#s14)：0 处源码、80 处资源。
- [S15 声音配置声明附加音效](#s15)：1 处源码、0 处资源。
- [S16 按作用域结束章节规则](#s16)：1 处源码、0 处资源。
- [S17 整理测试夹具与固定路线采集器](#s17)：32 处源码、0 处资源。
- [S18 UI 回放与采样器消费正式绑定](#s18)：5 处源码、0 处资源。
- [S19 示例内容外置、预览数据中性化](#s19)：6 处源码、9 处资源。
- [S20 保留资产造型与姿态生产配方](#s20)：4 处源码、0 处资源。
- [S21 剧本声明通行状态及其传播](#s21)：1 处源码、0 处资源。

## S01

**对应建议：** 清理 Core 旧案件专用实现。5 处源码、0 处资源。

### CSH-001

**范围与类型：** `production` / `schema`。**索引说明：** 具体隔离门、木楔、灰烟、纸转印、双锁与擦地证据因果模型。

**标记位置：** [csharp/HallCausalityRules.cs:4](../../csharp/HallCausalityRules.cs:4)。

**原文位置：** [csharp/HallCausalityRules.cs](../../csharp/HallCausalityRules.cs:5)，L5–L16。

```csharp
public static class HallCausalityRules
{
    public enum Hypothesis
    {
        InnerWedgeStopsOutsidePush,
        OutsideActorSetBothLocks,
        PassageThroughWedgeGap,
        ClosedDoorContinuesAdmittingSmoke,
        OutsideActorDidAllCleanup
    }
    public enum Action
    {
```

### CSH-002

**范围与类型：** `production` / `text`。**索引说明：** 针对隔离门案件假设输出结构结论与排除边界。

**标记位置：** [csharp/HallCausalityRules.cs:44](../../csharp/HallCausalityRules.cs:44)。

**原文位置：** [csharp/HallCausalityRules.cs](../../csharp/HallCausalityRules.cs:45)，L45–L56。

```csharp
    public static Conflict TestHypothesis(Hypothesis hypothesis, Result state) => hypothesis switch
    {
        Hypothesis.InnerWedgeStopsOutsidePush => new(ConflictKind.OutwardStopAgainstOutsidePush,
            true, "门向主楼侧外开，外推力使门扇压紧石砌止口；生活翼内的木楔不能抵抗该方向推力，只会妨碍合门。"),
        Hypothesis.OutsideActorSetBothLocks => new(ConflictKind.TwoLocksRequireSeparateOperations,
            state.InnerBoltSlid && state.OuterBarDropped,
            "内销位于生活翼侧，外闩位于主楼侧；单独站在外侧的操作者不能同时完成内销插入与外闩落座。"),
        Hypothesis.PassageThroughWedgeGap => new(ConflictKind.PassageRequiresWiderOpening,
            !state.DoorClosed && !state.DoorAllowsPassage,
            "木楔撑出的约十厘米开口不能直接让成人穿行；必须另有把门开大并保持通路的行动。这只否定直接穿缝，不排除先探路、随后撤离。"),
        Hypothesis.ClosedDoorContinuesAdmittingSmoke => new(ConflictKind.ClosureStopsOpenDoorSmokePath,
            state.SmokeReachedDoor && state.DoorClosed,
```

### CSH-003

**范围与类型：** `production` / `logic`。**索引说明：** 模拟隔离门案件动作顺序并生成具体证据痕迹和反馈。

**标记位置：** [csharp/HallCausalityRules.cs:64](../../csharp/HallCausalityRules.cs:64)。

**原文位置：** [csharp/HallCausalityRules.cs](../../csharp/HallCausalityRules.cs:65)，L65–L76。

```csharp
    public static Result Simulate(IEnumerable<Action> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        var traces = new List<Trace>();
        var seen = new HashSet<Action>();
        var failures = new List<string>();
        bool everBraced = false;
        bool braced = false, smoke = false, wedgeRemoved = false, closed = true;
        bool allowsPassage = false;
        bool bolt = false, wrapped = false, moved = false, wiped = false, outerBar = false;
        bool sootOnWedge = false, sootMaskedAtDoor = false, sootOnPaperInside = false, sootOnPaperOutside = false;
        bool wipeAtStopFoot = false;
```

### CSH-004

**范围与类型：** `production` / `logic`。**索引说明：** 将案件重演稿编译为音乐家在隔离门观察烟气并操作门楔的固定流程。

**标记位置：** [csharp/HallReplayProgram.cs:4](../../csharp/HallReplayProgram.cs:4)。

**原文位置：** [csharp/HallReplayProgram.cs](../../csharp/HallReplayProgram.cs:5)，L5–L12。

```csharp
public static class HallReplayProgram
{
    public sealed record Result(IReadOnlyList<HallCausalityRules.Action> Actions,
        IReadOnlyList<string> Events, IReadOnlyList<string> EventActions, string? Error);

    public static bool TryAction(string code, out HallCausalityRules.Action action) =>
        Enum.TryParse(code, out action) && Enum.IsDefined(action) && action.ToString() == code;

```

### CSH-005

**范围与类型：** `production` / `text`。**索引说明：** 重演事件与失败反馈直接描述门厅音乐家、热烟和案件动作。

**标记位置：** [csharp/HallReplayProgram.cs:13](../../csharp/HallReplayProgram.cs:13)。

**原文位置：** [csharp/HallReplayProgram.cs](../../csharp/HallReplayProgram.cs:14)，L14–L25。

```csharp
    public static Result Compile(IReadOnlyList<string> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var actions = new List<HallCausalityRules.Action>();
        var events = new List<string>();
        var eventActions = new List<string>();
        var visits = new int[steps.Count];
        int pc = 0, tick = 0;
        bool smoke = false, musicianAtDoor = false;
        // Old saved control-flow programs retain their original clock. New manuscripts use WAIT_SMOKE.
        bool legacyClock = steps.Any(code => code.StartsWith("IF_SMOKE:", StringComparison.Ordinal));
        Result Error(string message) => new(Array.Empty<HallCausalityRules.Action>(),
```

## S02

**对应建议：** 语义结果与剧本文案、称呼分离。18 处源码、0 处资源。

### AWK-012

**范围与类型：** `production` / `logic`。**索引说明：** 将本案熟悉度与拒绝请求映射为专属心声及案件标记。

**标记位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs:670](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:670)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:671)，L671–L682。

```csharp
    private string? Doubt(Agent a, Decision? pick)
    {
        foreach (var request in a.Soul.World.Projections.Where(p => p.Kind == ProjectionKind.Event && p.Procedure is not null && p.Tags.GetValueOrDefault(Tags.Responded) == 0))
        {
            var attitude = SocialSystem.Evaluate(a.Soul.World, a.Self, request);
            if (attitude.Stance == SocialStance.Accept) continue;
            var sender = request.Relations.FirstOrDefault(r => r.Relation == Relations.Subject).Target;
            if (sender is null || ReferenceEquals(pick?.Basis, request)) continue;
            var stranger = sender.Tags.GetValueOrDefault(NightTags.Acquainted) == 0;
            _log.Add($"{Now:0.0} {Names.Of(a.Who)} 对 {sender.Name} 的请求：{attitude.Stance}（{attitude.Score}）");
            if (stranger && a.Who == Person.Detective) _detectiveRefusedStranger = true;
            return stranger ? Chapter1Data.Text("重演·心声·stranger") : Chapter1Data.Text("夜间·怀疑·熟人", ("人名", sender.Name));
```

### AWK-016

**范围与类型：** `production` / `logic`。**索引说明：** 按苏泠、沈鼎、霍铎在特定场面中的认知生成等待心声。

**标记位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs:901](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:901)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:902)，L902–L913。

```csharp
    private string? IdleThought(Agent a)
    {
        var door = a.Mental[_door];
        var inFoyer = RoomOf(a.Body) == Room.Foyer;
        if (a.Who == Person.Su && inFoyer && door.Tags.GetValueOrDefault(NightTags.Open) == 1 && a.Outsider.Existence == Truth.True)
            return Chapter1Data.Text("重演·心声·wait_voice");
        if (a.Who == Person.Shen && inFoyer && a.Mental[_cloth].Tags.GetValueOrDefault(NightTags.Wet) == 1 && door.Tags.GetValueOrDefault(NightTags.Open) == 1)
            return Chapter1Data.Text("重演·心声·wait_door");
        if (a.Who == Person.Huo && RoomOf(a.Body) == Room.Hall && a.Order.Tags.GetValueOrDefault(NightTags.HoldTube) == 1)
            return Chapter1Data.Text("重演·心声·wait_tube");
        return null;
    }
```

### AWK-062

**范围与类型：** `production` / `schema`。**索引说明：** 回应键把本章言语行为/立场固定映射到剧本词汇键。

**标记位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Speech.cs:12](../../godot_csharp/scenes/Awakening/Script/Chapter1Speech.cs:12)。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Speech.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1Speech.cs:13)，L13–L24。

```csharp
    private static readonly string[] Companions = ["huo", "shen", "su"];

    private static readonly IReadOnlyDictionary<(SpeechAct, Stance), string[]> SpeechKeys = new Dictionary<(SpeechAct, Stance), string[]>
    {
        [(SpeechAct.Tell, Stance.Affirm)] = ["如实·有把握"], [(SpeechAct.Tell, Stance.Deny)] = ["如实·否认"],
        [(SpeechAct.Tell, Stance.Unsure)] = ["如实·没把握"],
        [(SpeechAct.Hedge, Stance.Affirm)] = ["含糊"], [(SpeechAct.Hedge, Stance.Deny)] = ["含糊"], [(SpeechAct.Hedge, Stance.Unsure)] = ["含糊"],
        [(SpeechAct.AskBack, Stance.Affirm)] = ["反问"], [(SpeechAct.AskBack, Stance.Deny)] = ["反问"], [(SpeechAct.AskBack, Stance.Unsure)] = ["反问"],
        [(SpeechAct.Withhold, Stance.Affirm)] = ["隐瞒"], [(SpeechAct.Withhold, Stance.Deny)] = ["隐瞒"],
        [(SpeechAct.Refuse, Stance.Affirm)] = ["拒绝"], [(SpeechAct.Refuse, Stance.Deny)] = ["拒绝"], [(SpeechAct.Refuse, Stance.Unsure)] = ["拒绝"],
        // 说反话时用写给相反立场的台词；作者另写了“撒谎·…”则优先用它。
        [(SpeechAct.Lie, Stance.Affirm)] = ["撒谎·有把握", "如实·有把握"], [(SpeechAct.Lie, Stance.Deny)] = ["撒谎·否认", "如实·否认"],
```

### CH2-015

**范围与类型：** `production` / `text`。**索引说明：** 章节专属教师身份替换与标题常量写在宿主代码中。

**标记位置：** [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:549](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:549)。

**原文位置：** [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:550)，L550–L561。

```csharp
public static class Chapter2Content
{
    // 学员尚不知道教师的身份；作者侧的称呼只在实现里使用。
    public static string PlayerText(string text) => text.Replace("恶魔", "教师", StringComparison.Ordinal);
    public static string DayName(int day) => Chapter2Design.DayName(day);
    public static string SlotName(int day, Slot slot) => Chapter2Design.SlotDisplay(day, slot);
    public static string TimeName(int day, Slot slot) => $"{DayName(day)} · {SlotName(day, slot)}";
    public static string Intro => Chapter2Design.Intro;
    public static string Instructions => Chapter2Design.Instructions;
}

/// <summary>第二章正式剧本入口；原人物、世界与剧情直接编译在本程序集。</summary>
```

### CH2-023

**范围与类型：** `production` / `text`。**索引说明：** 帮助展示具体人名、物证提示与本章行动例句。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:425](../../godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:425)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs](../../godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:426)，L426–L437。

```csharp
    private void Help()
    {
        W("""
          —— 不占时段（随时可看）——
            状态                 你的处境、学过的课、答应别人的事
            笔记 [某人]          人际笔记：他认了你的哪条规矩、他嘴上向着谁、他常与谁同处（只记你亲见亲闻的）
            规矩                 你知道的规矩。称呼（甲、乙……）是你自己记的，后面的命令里可以用
            传播 [称呼]          你的规矩传到了谁：当面讲过谁、谁跟你说他从哪里听来的、你亲眼看见谁讲给谁
            清晨 [第N夜]         某一夜之后的痕迹：细看过的写全，路过的只记明显的变化，没去过的只给地点
            重演 …               假设昨夜某人认/不认某条规矩、在/不在某处，重放一夜，与你读到的痕迹对照
                                 （只写“重演”会列出可选的假设；“重演 1 3”选其中几项；
                                  也可直接写：重演 茉拉 认 甲 / 重演 雀儿 在 后勤院；可加“第3夜”）
```

### CH2-026

**范围与类型：** `production` / `logic`。**索引说明：** 人物印象候选名单固定为本案关键角色。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:542](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:542)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:543)，L543–L554。

```csharp
    private string[] Impressions()=>_game.Npcs.Where(p=>p.Id is P.Ward or P.Quer or P.Mora or P.Huo or P.Ruan or P.Margaret).Select(p=>
    {
        var (c,i)=_game.ImageOfDet(p);
        var text=c>=2?"说话不避着你":c>=1?"对你还算客气":c>=0?"对你话不多":"明显不想跟你多说";
        if(i>=2)text+="，信你的判断"; return p.Name+"："+text;
    }).ToArray();
    private Dictionary<string,object?> LegacySnapshot()=>new Dictionary<string,object?>
    {
        ["detective_name"]=DetectiveName(),["stage"]=_stage,["ended"]=_ended,["replay_open"]=_replayOpen,["replay_result"]=_replayResult,
        ["known_places"]=_knownPlaces.ToDictionary(k=>k,k=>(object?)true),["examined"]=_examined,["talked"]=_talked,["journal"]=_journal,
        ["people"]=_game.Npcs.Select(p=>(object)new Dictionary<string,object?>{["key"]=p.Id.ToString(),["name"]=p.Name,["place"]=_game.Where(p).ToString(),["doing"]=_game.LastDoing(p.Id,ShownDay,ShownSlot)?.Text??"",["frame"]=_game.LastDoing(p.Id,ShownDay,ShownSlot)?.Frame??"",["asleep"]=p.Asleep,["present"]=!p.Asleep&&_game.HostDormReleased(p),["displayable"]=!p.Asleep&&_game.HostDormReleased(p),["court_present"]=(_stage is "council" or "closing")&&Game.Get(p.Body,T.Sick)==0,["sick"]=Game.Get(p.Body,T.Sick)>0}).ToArray()
    };
```

### CH2-044

**范围与类型：** `production` / `text`。**索引说明：** 恶魔对本案各话题的知识边界与回答逐项预写。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Demon.cs:130](../../godot_csharp/scenes/Chapter2/Prototype/Demon.cs:130)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Demon.cs](../../godot_csharp/scenes/Chapter2/Prototype/Demon.cs:131)，L131–L142。

```csharp
    public string AskDemon(int topic, bool precise, out bool praised, out bool refused)
    {
        praised = false; refused = false;
        var learned = (int l) => Det.Lessons.ContainsKey(l);
        string answer;
        switch (topic)
        {
            case 0:
                answer = !precise ? "封存之物存于名录库，凡学员皆可取回己物。"
                    : learned(14) ? "取回，即重新接通。接通之后，我离诸位更近一些。寻回者，自负其果——守则第一条写着。" : "此事《异象与记忆》会讲。现在讲，诸位听不懂。";
                praised = precise && learned(14);
                break;
```

### CH2-046

**范围与类型：** `production` / `text`。**索引说明：** 为本章议事结局编写人物组合对应的开场反馈。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Council.cs:86](../../godot_csharp/scenes/Chapter2/Prototype/Council.cs:86)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Council.cs](../../godot_csharp/scenes/Chapter2/Prototype/Council.cs:87)，L87–L98。

```csharp
    private string OpeningScene(Ending fam, RuleSpec plan, List<Person> who)
    {
        var names = who.Where(w => w != Det).Select(w => w.Name).Take(4).ToArray();
        if (plan.Card is { } c && plan.Author == P.Det)
        {
            var sign = c.Account switch { 3 => "每个进去的人身后都站着两个见证的人，在门边的纸上签了名", 1 or 2 => "门边摆着台账，进去的人一个一个记上名字", _ => "没人记账，进去的人各自摸黑找自己的匣子" };
            var night = c.Exception == 4 ? "说好只在白天开，天一擦黑门就合上了" : c.Exception == 3 ? "两人一组进去，一个人不许单独进" : "";
            return $"门开了。{sign}{(night.Length > 0 ? "；" + night : "")}。{(c.Behavior == 8 ? "本人亲取：每人只拿标着自己名字的那一只。" : c.Behavior == 6 ? "钥匙交给了委员会，取回的人先过一遍登记。" : "")}";
        }
        return fam switch
        {
            Ending.Committee => $"沃德拿着钥匙站在门边，{string.Join("、", names)}排着队，一个一个登记、一个一个进去。",
```

### CH2-048

**范围与类型：** `production` / `text`。**索引说明：** 生成针对本案夜间行动与地点的固定守望报告。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Morning.cs:93](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:93)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Morning.cs](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:94)，L94–L105。

```csharp
    public List<string> StakeoutReport(int day, Place at)
    {
        var cares = DetCares(at);
        var events = new List<(double T, string Text, int Score, bool Figure)>();
        foreach (var m in Moves.Where(m => m.Day == day && m.Who != P.Det))
        {
            var build = BuildText(this[m.Who].Pro.Build);
            if (m.To == at) events.Add((m.Time, $"{NightTime(m.Time)}，一个{build}身影从{From(m.From)}过来", 2 + (cares ? 1 : 0), true));
            else if (m.From == at) events.Add((m.Time, $"{NightTime(m.Time)}，一个{build}身影往{From(m.To)}去了", 1 + (cares ? 1 : 0), true));
        }
        // 从守夜处望得见的别处（中央厅望得见后勤院与名录库前厅的出入）：只有身量，远处的身影分数低于走到身边的。
        foreach (var s in Sightings.Where(s => s.Day == day && s.Witness == P.Det && s.From == at))
```

### CH2-057

**范围与类型：** `production` / `text`。**索引说明：** 为本案专属物证、人物特征及状态撰写调查描述。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Player.cs:537](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:537)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Player.cs](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:538)，L538–L549。

```csharp
    public string Describe(Essence e)
    {
        if (e == Granary) return $"存粮还剩 {Get(Granary, T.Ration)} 份；门闩{(Get(Granary, T.LatchLoose) > 0 ? "松动，一推就开" : Get(Granary, T.ToolMarks) > 0 ? "有新的工具痕，已经紧固" : "完好")}。";
        if (e == Ledger) return X("物件.台账", ("发出", Get(Ledger, T.Handed)), ("补记", Get(Ledger, T.NightRecorded)))
            + string.Concat(AllNews.Where(n => n.Kind == NewsKind.Ledger && n.Claim.Predicate == "补记")
                .Select(n => "\n" + X("物件.台账补记记录", ("人", Names.Of(n.Claim.Subject!.Value)),
                    ("日", Chapter2Design.DayName(n.Day)), ("时段", Names.Of(n.Slot)), ("夜", Chapter2Design.DayName(n.Claim.Night)))))
            + (Get(Ledger, T.Counted) > 0 ? "\n" + (Get(Ledger, T.Unrecorded) > 0 ? X("物件.台账清点", ("缺", Get(Ledger, T.Unrecorded))) : X("物件.台账清点无缺")) : "");
        if (e == Bowl) return Get(Bowl, T.Residue) > 0 ? $"莫兰床边一只空碗，碗底有粥渍{(Get(Bowl, T.Porridge) > 0 ? "；另有一碗粥还温着" : "")}。" : "碗是干净的。";
        if (e == Bed) return Get(this[P.Moran].Body, T.Sick) > 0 ? "莫兰躺着，额头很烫。" : "病床空着。";
        if (e == Blackboard) return Get(Blackboard, T.Chalk) > 0 ? $"黑板上有 {Get(Blackboard, T.Chalk)} 堂课的粉笔字；讲台前的灰上有窄底皮鞋印。" : "黑板是干净的。";
        if (e == Stele) return "铺石顺坡，几级台阶。石面上刻着磨平的纹样，看不出原本写的是什么。";
```

### CH2-058

**范围与类型：** `production` / `text`。**索引说明：** 按具名人物立场生成议事前的本案反馈。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Report.cs:67](../../godot_csharp/scenes/Chapter2/Prototype/Report.cs:67)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Report.cs](../../godot_csharp/scenes/Chapter2/Prototype/Report.cs:68)，L68–L79。

```csharp
    private void StanceTalk()
    {
        if (Day < CaseDay || Slot is not (Slot.Noon or Slot.Evening)) return;
        var here = Where(Det);
        var talkers = Npcs.Where(p => Where(p) == here && Get(p.Body, T.Sick) == 0 && _voicedStance.GetValueOrDefault(p.Id).Day != Day)
            .Select(p => (p, s: StanceOf(p))).Where(x => x.s is not null)
            .OrderByDescending(x => _voicedStance.TryGetValue(x.p.Id, out var v) && v.Spec?.Text != x.s!.Text ? 1 : _voicedStance.ContainsKey(x.p.Id) ? 0 : 2)
            .ThenBy(x => x.p.Id).Take(2).ToArray();
        var said = new List<(Person P, string Line)>();
        // 本人有专属说法就用本人的。
        string Line(string key, Person p) => Chapter2Design.HasText($"立场话.{key}.{p.Id}") ? X($"立场话.{key}.{p.Id}") : X($"立场话.{key}");
        foreach (var (p, s) in talkers)
```

### CH2-059

**范围与类型：** `production` / `text`。**索引说明：** 输出本章案件与议事结局的专属总结。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Report.cs:117](../../godot_csharp/scenes/Chapter2/Prototype/Report.cs:117)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Report.cs](../../godot_csharp/scenes/Chapter2/Prototype/Report.cs:118)，L118–L129。

```csharp
    public static string ChapterEnd(Game g)
    {
        var sb = new StringBuilder();
        sb.AppendLine("【章节结束状态（交第三章）】");
        sb.AppendLine("各人心中的侦探（可信/见识）：" + Image(g));
        foreach (var f in g.Factions()) sb.AppendLine($"派系：领头 {Names.Of(f.Leader)}｜{string.Join("、", f.Members.Select(Names.Of))}｜共享规则：{string.Join("；", f.SharedRules)}");
        sb.AppendLine($"名录库：{Game.EndingText(g.Result)}");
        sb.AppendLine("钩与欲望：" + string.Join(" ", g.Npcs.Select(p => $"{p.Name}欲{p.Desire}钩{p.Hooks}")));
        sb.AppendLine("已学课程：" + string.Join(" ", g.People.Select(p => $"{p.Name}[{string.Join(",", p.Lessons.Select(l => l.Key + (l.Value ? "" : "?")))}]")));
        sb.AppendLine($"物资：口粮 {Game.Get(g.Granary, T.Ration)}，灯油 {Game.Get(g.OilStore, T.Oil)}，药品 {Game.Get(g.MedCab, T.Medicine)}；温室{(g.GreenhouseRepaired ? "已修好" : "未修好")}");
        sb.AppendLine($"《少了一份》：{(g.Accused is not { } acc ? "无人被指控" : g.QuerCleared ? $"{Names.Of(acc)}洗清" : g.QuerOstracized ? $"{Names.Of(acc)}被排斥" : "未对质")}；新规则：{g.CaseRule?.Text ?? "无"}；共同规则：{(g.CommonRuleEstablished ? "成立" : "未成立")}");
        sb.AppendLine("操纵记录：" + (g.Manipulations.Count == 0 ? "无" : string.Join("；", g.Manipulations.Select(m => $"D{m.Day} 对{Names.Of(m.Target)}：{m.Means}{(m.Exposed ? "（已揭穿）" : "")}"))));
```

### CH2-060

**范围与类型：** `production` / `schema`。**索引说明：** 绑定特定人物与本章习惯证词的文本键。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Retell.cs:30](../../godot_csharp/scenes/Chapter2/Prototype/Retell.cs:30)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Retell.cs](../../godot_csharp/scenes/Chapter2/Prototype/Retell.cs:31)，L31–L42。

```csharp
    private string SlantText(NewsItem item, P? slant, Person speaker)
    {
        if (slant is not { } s || item.About is not { } about) return "";
        var key = about == P.Quer ? "走样私取" : "走样惯常";
        if (s == speaker.Id) return X($"知识片段.{key}自述", ("对象", Ref(about, speaker, Det)));
        return X($"知识片段.{key}", ("传话人", Ref(s, speaker, Det)));
    }

    /// <summary>某人把自己知道的一条知识说给侦探：亲历的照说；听来的说明来源；来源本人就是主语时，说成“某人说他……”。</summary>
    private string Hearsay(Person speaker, NewsItem item, Heard h)
    {
        var template = TimedTemplate(item);
```

### CH2-066

**范围与类型：** `production` / `text`。**索引说明：** 为本案指控对象选择具名指控反馈。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:655](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:655)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:656)，L656–L667。

```csharp
    private void AccuseSpeech(Person w)
    {
        // 指控以亲见优先（§13 第 3 条）：先指自己亲眼看见没记账取粮的人，再依转述与成见。
        var suspects = People.Where(q => q != w && (Belief(w, q, T.Suspect) ?? 0) > 0)
            .OrderByDescending(q => _sawTaking.Contains((w.Id, q.Id)) ? 1 : 0).ThenBy(q => q.Id).ToArray();
        if (suspects.Length == 0) return;
        var q = suspects[0];
        var n = Belief(w, w.M[Ledger], T.Unrecorded) ?? 0;
        var build = BuildText(q.Pro.Build);
        var sawFigure = ClaimsOf(w).Any(c => SawFigure(c, q.Pro.Build));
        var charge = K(_sawTaking.Contains((w.Id, q.Id)) ? "指控亲见" : sawFigure ? "指控身影" : "指控台账", w.Id, q.Id, night: Day - 1,
            values: [("数", Cn(n)), ("身形", build)]);
```

### CH2-072

**范围与类型：** `production` / `text`。**索引说明：** 为温室阀门与中继站提供本章专属物证反馈。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Host.cs:48](../../godot_csharp/scenes/Chapter2/Prototype/Host.cs:48)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Host.cs](../../godot_csharp/scenes/Chapter2/Prototype/Host.cs:49)，L49–L60。

```csharp
    public string HostExamineEntity(Essence entity)
    {
        if (!View.ContainsKey(entity)) throw new InvalidOperationException("Unknown chapter entity.");
        if (entity.Get<SpatialProperty>() is not null && SpaceOf(entity) != Space[Where(Det)]) throw new InvalidOperationException("The object is not in the current space.");
        var observed=Rt.Observe(Det.Soul,[View[entity]],Now);
        Det.M[entity]=observed.Single();DetSaw(entity);
        if(entity==Valve)return Get(entity,T.Work)>0?"阀门已有修理的痕迹。":"阀门还没有修好。";
        if(entity==RelayPost)return Get(entity,T.Worked)>0?"中继站留下了操作痕迹。":"中继站未见操作痕迹。";
        return Describe(entity);
    }

    /// <summary>问一个人知道什么（不占时段）。他只报告自己的感知与职业判读。</summary>
```

### CH2-080

**范围与类型：** `production` / `text`。**索引说明：** 构造本章议事结果、角色归宿与章末卡文本。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2SessionView.cs:61](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2SessionView.cs:61)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2SessionView.cs](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2SessionView.cs:62)，L62–L73。

```csharp
    private IEnumerable<Ev> EndEvents(Ev result)
    {
        if (result.GetValueOrDefault("stands") is IDictionary<string, string> stands)
            yield return Event("stage", ("op", "group"), ("anchor", "site:oath_court"), ("stands", stands.ToDictionary(p => p.Key, p => (object?)p.Value)));
        var hooked = ((string[])result["hooks"]!).ToList();
        var detHooks = Convert.ToInt32(result["det_hooks"]);
        if (detHooks > 0) hooked.Insert(0, "你自己");
        string T(string key, params (string, object?)[] args) => Chapter2Design.Text("终局." + key, args);
        var rows = new List<object?> { T("名录库", ("结果", result["result_text"])), result["quer"] };
        if ((string)result["case_rule"]! != "") rows.Add(T(result["case_rule_adopted"] is true ? "对账规矩立住" : "对账规矩没立住", ("规矩", result["case_rule"])));
        rows.Add(T(result["common_rule"] is true ? "共同规则成立" : result["common_rule_proposed"] is true ? "共同规则不成" : "共同规则未提"));
        rows.Add(T("钩", ("铜口", _game.VoiceName(null)), ("名单", hooked.Count == 0 ? T("钩无") : string.Join("、", hooked))));
```

### CH2-081

**范围与类型：** `production` / `text`。**索引说明：** 状态页显示本案特定证物、指控与石碑阅读结果。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:494](../../godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:494)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs](../../godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:495)，L495–L506。

```csharp
    private void Status()
    {
        var det = g.Det;
        W($"{Chapter2Content.TimeName(g.Day, g.Slot)}。你在{Names.Of(g.Where(det))}。{(Game.Get(det.Body, T.Hungry) > 0 ? "你饿着（下午做不了事）。" : "")}今天{(Game.Get(det.Body, T.Fed) > 0 ? "已经" : "还没")}领到口粮。");
        W($"学过的课：{(det.Lessons.Count == 0 ? "无" : string.Join("、", det.Lessons.Select(l => $"《{Game.Lessons[l.Key].Title}》{(l.Value ? "" : "（听人转述，可能走样）")}")))}");
        if (g.Day >= Game.CaseDay)
        {
            var ev = g.DetEvidence();
            W("台账那件事，你查过的：" + (ev.Count == 0 ? "还没有" : string.Join("、", ev.Select(i => CaseTraces.Labels[i]))) + (g.DetAsked.Count > 0 ? "；当面问过：" + string.Join("、", g.DetAsked.Select(Names.Of)) : ""));
        }
        if (g.Accused is { } acc) W(g.QuerCleared ? $"{Names.Of(acc)}已经洗清。" : g.QuerOstracized ? $"{Names.Of(acc)}被当成私取之人，搬出了寝区。" : "沃德的指控还悬着。");
        foreach (var (who, due, done) in g.Promises.Where(p => !p.Done)) W($"答应过：第{due}日前替{Names.Of(who)}办一件事（用“帮 {Names.Of(who)}”兑现）");
```

### CSH-009

**范围与类型：** `production` / `text`。**索引说明：** 将腐化来源专称为“铜口之言”，是项目世界专属的证据反馈文案。

**标记位置：** [csharp/Reasoner.cs:124](../../csharp/Reasoner.cs:124)。

**原文位置：** [csharp/Reasoner.cs](../../csharp/Reasoner.cs:125)，L125–L136。

```csharp
    public static (double Factor, string Why) Weight(Projection claim)
    {
        var source = claim.LastObservation?.Source ?? ObservationSource.Introspection;
        switch (source)
        {
            case ObservationSource.Perception: return (8, "亲见");
            case ObservationSource.Inference: return (6, "自己推出");
            case ObservationSource.Recall: return (3, "回忆");
            case ObservationSource.Corruption: return (1, "铜口之言，不当证据");
            case ObservationSource.Testimony:
                var speaker = claim.Relations.FirstOrDefault(r => r.Relation == Relations.Subject).Target;
                var trust = speaker?.Tags.GetValueOrDefault(Tags.Trust) ?? 0;
```

## S03

**对应建议：** 剧本实体登记替代固定名单与专用枚举。20 处源码、0 处资源。

### AWK-002

**范围与类型：** `production` / `schema`。**索引说明：** 枚举键与本章人物、房间名称按固定顺序绑定。

**标记位置：** [godot_csharp/scenes/Awakening/Night/NightVariables.cs:130](../../godot_csharp/scenes/Awakening/Night/NightVariables.cs:130)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/NightVariables.cs](../../godot_csharp/scenes/Awakening/Night/NightVariables.cs:131)，L131–L138。

```csharp
public static class Names
{
    public static readonly string[] PersonKeys = ["detective", "huo", "shen", "su"];
    public static readonly string[] RoomKeys = ["R05", "R06", "R07", "R08", "corridor", "hall", "pantry", "foyer", "lounge"];
    public static string Of(Person person) => Chapter1Data.PersonName(PersonKeys[(int)person]);
    public static Person PersonOf(string key) => Array.IndexOf(PersonKeys, key) is var i and >= 0 ? (Person)i : throw new ArgumentException("Unknown person key: " + key);
    public static string Of(Room room) => Chapter1Data.RoomName(RoomKeys[(int)room]);
}
```

### AWK-006

**范围与类型：** `production` / `schema`。**索引说明：** 固定本案九个空间的夜间通行拓扑与行动耗时。

**标记位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs:91](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:91)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:92)，L92–L103。

```csharp
    private static readonly ImmutableDictionary<(Room, Room), double> Hops = new Dictionary<(Room, Room), double>
    {
        [(Room.R05, Room.Corridor)] = 1.2, [(Room.R06, Room.Corridor)] = 1.2,
        [(Room.R07, Room.Corridor)] = 1.2, [(Room.R08, Room.Corridor)] = 1.2,
        [(Room.Corridor, Room.Hall)] = 2.0, [(Room.Hall, Room.Pantry)] = 2.0,
        [(Room.Hall, Room.Foyer)] = 2.5,
    }.ToImmutableDictionary();

    private readonly NightVariables _v;
    private readonly WorldRuntime _runtime = new();
    private readonly SimulationSession _session;
    private readonly Dictionary<Room, Essence> _space = [];
```

### AWK-007

**范围与类型：** `production` / `logic`。**索引说明：** 构造具体案件物件、四名角色的起始地点与鞋底，并将 V1/V2/V6 绑定到世界状态。

**标记位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs:194](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:194)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:195)，L195–L206。

```csharp
    private void Build()
    {
        foreach (var room in Enum.GetValues<Room>()) Space(room);
        foreach (var ((a, b), _) in Hops)
        {
            var passage = new Essence($"{Names.Of(a)}—{Names.Of(b)}", ProjectionKind.Boundary);
            var connections = passage.Add(new PassageProperty()).Connections;
            connections[_space[a]] = _space[b];
            connections[_space[b]] = _space[a];
            _passage[(a, b)] = _passage[(b, a)] = passage;
            _view[passage] = _runtime.Project(passage, passage.TrueName);
        }
```

### AWK-034

**范围与类型：** `production` / `logic`。**索引说明：** 从本章固定案件真相建立清晨证物实体与醒来人物认知。

**标记位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:463](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:463)。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:464)，L464–L475。

```csharp
        var truth = Chapter1Data.Root.GetProperty("TRUTH");
        int TruthValue(string field, int? equals)
        {
            var value = truth.GetProperty(field);
            var number = value.ValueKind is System.Text.Json.JsonValueKind.True ? 1 : value.ValueKind is System.Text.Json.JsonValueKind.False ? 0 : value.GetInt32();
            return equals is { } expected ? (number == expected ? 1 : 0) : number;
        }
        foreach (var item in Chapter1Data.Root.GetProperty("EVIDENCE").EnumerateObject())
        {
            var data = item.Value;
            Evidence(item.Name, (data.TryGetProperty("mind_name", out var mindName) ? mindName : data.GetProperty("name")).GetString()!,
                (Room)Array.IndexOf(Names.RoomKeys, data.GetProperty("room").GetString()),
```

### AWK-036

**范围与类型：** `production` / `schema`。**索引说明：** 登记固定的第一章四人组及三名同伴。

**标记位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Session.cs:26](../../godot_csharp/scenes/Awakening/Script/Chapter1Session.cs:26)。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Session.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1Session.cs:27)，L27–L38。

```csharp
    private static readonly string[] Companions = ["huo", "shen", "su"];
    private static readonly string[] Party = ["detective", "huo", "shen", "su"];
    private const string Art = "res://ShareAsset/Internal/Props/AwakeningEvidence/";

    private MorningWorld _world = null!;
    private AwakeningContentSession _content = null!;
    private Chapter1Dialogue _dialogue = null!;
    private Chapter1Interaction _interaction = null!;
    private JsonElement Content => _content.Content.RootElement;

    public Chapter1Session() => Reset();

```

### AWK-063

**范围与类型：** `production` / `schema`。**索引说明：** 本案角色/房间及 V1–V6 选项由连续固定枚举编码。

**标记位置：** [godot_csharp/scenes/Awakening/Night/NightVariables.cs:6](../../godot_csharp/scenes/Awakening/Night/NightVariables.cs:6)。

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

### CH2-012

**范围与类型：** `production` / `logic`。**索引说明：** 初始化具体角色、案件实体、持钥身份、时空位置与案件状态。

**标记位置：** [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:93](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:93)。

**原文位置：** [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:94)，L94–L105。

```csharp
    public Game(GameOptions? options = null, LiveChapterWorld? continuation = null, ChapterWorldDefinition? authored = null, WorldSession? session = null)
    {
        Options = options ?? new();
        if (authored is not null) Chapter2Bindings.Validate(authored);
        _authored = authored;
        Continuation = continuation;
        PersistentSession = session ?? continuation?.Session;
        if (continuation is not null && PersistentSession is not null
            && (!ReferenceEquals(continuation.Runtime, PersistentSession.Runtime)
                || !ReferenceEquals(continuation.Simulation, PersistentSession.Simulation)))
            throw new InvalidOperationException("The persistent session must own the same live world and simulation.");
        Chapter = continuation?.Progress ?? new ChapterSession();
```

### CH2-074

**范围与类型：** `production` / `schema`。**索引说明：** 定义本章十六位学员的具体身份标识。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Model.cs:7](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:7)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Model.cs](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:8)，L8–L8。

```csharp
public enum P { Det, Huo, Shen, Su, Lucian, Gu, Quer, Ward, Selma, Ruan, Elena, Moran, Veronica, Bertrand, Margaret, Mora }
```

### CH2-075

**范围与类型：** `production` / `schema`。**索引说明：** 定义本案粮仓、痕迹与被指控等专用状态标识。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Model.cs:43](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:43)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Model.cs](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:44)，L44–L55。

```csharp
public static class T
{
    public static readonly Tag Ration = new("Ration"), Locked = Tags.Locked, LatchLoose = new("LatchLoose"), ToolMarks = new("ToolMarks");
    public static readonly Tag Open = new("Open"), Cooked = new("Cooked"), Water = new("Water"), Handed = new("Handed");
    public static readonly Tag NightRecorded = new("NightRecorded"), Porridge = new("Porridge"), Crumbs = new("Crumbs"), Residue = new("Residue");
    public static readonly Tag Medicine = new("Medicine"), Oil = new("Oil"), Treated = new("Treated"), Work = new("Work");
    public static readonly Tag Pressure = new("Pressure"), Chalk = new("Chalk"), Rubbed = new("Rubbed"), Accusation = new("Accusation");
    public static readonly Tag Posted = new("Posted"), Counted = new("Counted"), Shoe = new("Shoe"), Emptied = new("Emptied"), Torn = new("Torn");
    /// <summary>名录库机关的一站被人占着 / 今夜有人在这一站使过劲；有人守门（身上可见）；中央基座的裂纹（凑近才看得见）。</summary>
    public static readonly Tag Occupied = new("Occupied"), Worked = new("Worked"), Guarding = new("Guarding"), Prying = new("Prying"), Crack = new("Crack"), OwesWater = new("OwesWater");
    /// <summary>众所周知的本事（他人心中的标签）：会开锁、力气大、懂传声。</summary>
    public static readonly Tag Picks = new("Picks"), Strong = new("Strong"), Signals = new("Signals");
```

### CH2-076

**范围与类型：** `production` / `schema`。**索引说明：** 初始化本章具体空间、案件物件、持钥人身份与知情模型。

**标记位置：** [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:167](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:167)。

**原文位置：** [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:168)，L168–L179。

```csharp
    private void BuildWorld()
    {
        foreach (var place in Enum.GetValues<Place>())
        {
            var s = Existing("world:room." + place) ?? new Essence(Names.Of(place), ProjectionKind.Space);
            if (s.Get<StateProperty>() is null) s.Add(new StateProperty());
            Space[place] = s;
            PlaceOf[s] = place;
            View[s] = Rt.Representations(s).FirstOrDefault() ?? Rt.Project(s, Names.Of(place));
        }
        // 每个去处一条路：从任何地方都能到。房内几何与寻路属宿主，这里只保留“到哪里”。
        foreach (var place in Enum.GetValues<Place>())
```

### CH2-084

**范围与类型：** `production` / `schema`。**索引说明：** 将剧本里的具体世界物件身份绑定到本章运行对象。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:8](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:8)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:9)，L9–L20。

```csharp
    private static readonly IReadOnlyDictionary<string,Func<Game,Essence>> Objects = new Dictionary<string,Func<Game,Essence>>
    {
        ["Granary"]=g=>g.Granary,["Desk"]=g=>g.Desk,["Stove"]=g=>g.Stove,["Jar"]=g=>g.Jar,["Ledger"]=g=>g.Ledger,
        ["Bowl"]=g=>g.Bowl,["Bed"]=g=>g.Bed,["MedCab"]=g=>g.MedCab,["OilStore"]=g=>g.OilStore,["Valve"]=g=>g.Valve,
        ["Gauge"]=g=>g.Gauge,["Blackboard"]=g=>g.Blackboard,["Stele"]=g=>g.Stele,["ArchiveDoor"]=g=>g.ArchiveDoor,
        ["WeightRoom"]=g=>g.WeightRoom,["LockCore"]=g=>g.LockCore,["RelayPost"]=g=>g.RelayPost,["Base"]=g=>g.Base,
        ["SketchBook"]=g=>g.SketchBook,["Clock"]=g=>g.Clock,["NameVoice"]=g=>g.NameVoice
    };
    private static string Local(WorldEntityDefinition entity)
    {
        if(entity.Scope!="world")throw new InvalidDataException("Formal existing-world bindings require world scope.");
        return entity.Id.StartsWith("world:",StringComparison.Ordinal)?entity.Id[6..]:entity.Id;
```

### CH2-085

**范围与类型：** `production` / `schema`。**索引说明：** 定义本章具名场所与宿舍区身份。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Model.cs:12](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:12)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Model.cs](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:13)，L13–L16。

```csharp
public enum Place { Hall, Lecture, Logistics, Clinic, Greenhouse, ArchiveFront, Oath, DormLife, DormW, DormS, DormL, Resonance, Living }
public enum Slot { Morning, Forenoon, Noon, Afternoon, Evening, Night }
public enum Build { Small, Medium, Large }
/// <summary>跨寝区的价值聚落（§11 第 7 条，初值，可被打破）。</summary>
```

### CH2-092

**范围与类型：** `production` / `schema`。**索引说明：** 固定本章具名学员所属的四个社会群体。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Model.cs:9](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:9)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Model.cs](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:10)，L10–L11。

```csharp
public enum Group { Life, Wenqi, Xingyi, Houqin }
/// <summary>新地点只追加在末尾：已有地点的序号写进了存档与快照。</summary>
```

### CH2-093

**范围与类型：** `production` / `schema`。**索引说明：** 固定本章角色日程可执行的专属行为枚举。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Model.cs:21](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:21)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Model.cs](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:22)，L22–L33。

```csharp
public enum Job
{
    Sleep, Rest, Class, Collect, Distribute, Cook, FetchWater, Care,
    NightTakeSick, NightTakeSickRec, NightTakeSelf, NightTakeSelfRec,
    RepairLatch, InspectStructure, Patrol, GuardArchive, VisitArchive, Greenhouse,
    Socialize, Work, CountLedger, Stele, Scavenge, Hoard, Accuse, PostRules, Defend,
    AskLatchHelp, Seize, AnswerName, Escort, Inform, FeedWeak, GiveKey, Tidy, Wander, Share, Tear, Watch, AskOpen, Relay, Teach, Exchange, Follow, Avoid, AskLeave,
    Treat, Retreat, Report, Sample, Needle, NightSketch, Sketch
}

/// <summary>规则的语义标记：规则接受公式据此与各人的欲望、习惯、利益相比。</summary>
[Flags]
```

### CH2-094

**范围与类型：** `production` / `schema`。**索引说明：** 固定本章人物的内部别名与身份映射。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Model.cs:87](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:87)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Model.cs](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:88)，L88–L93。

```csharp
    public static readonly ImmutableDictionary<string, P> PersonAlias = new Dictionary<string, P>
    {
        ["det"] = P.Det, ["huo"] = P.Huo, ["shen"] = P.Shen, ["su"] = P.Su, ["lucian"] = P.Lucian, ["gu"] = P.Gu, ["quer"] = P.Quer,
        ["ward"] = P.Ward, ["selma"] = P.Selma, ["ruan"] = P.Ruan, ["elena"] = P.Elena, ["moran"] = P.Moran, ["veronica"] = P.Veronica,
        ["bertrand"] = P.Bertrand, ["margaret"] = P.Margaret, ["mora"] = P.Mora,
    }.Concat(Enum.GetValues<P>().Select(p => new KeyValuePair<string, P>(Of(p), p))).ToImmutableDictionary();
```

### CH2-095

**范围与类型：** `production` / `schema`。**索引说明：** 固定本章地点的内部别名与地点映射。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Model.cs:94](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:94)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Model.cs](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:95)，L95–L106。

```csharp
    public static readonly ImmutableDictionary<string, Place> PlaceAlias = new Dictionary<string, Place>
    {
        ["hall"] = Place.Hall, ["lecture"] = Place.Lecture, ["logistics"] = Place.Logistics, ["clinic"] = Place.Clinic,
        ["greenhouse"] = Place.Greenhouse, ["archive"] = Place.ArchiveFront, ["oath"] = Place.Oath, ["life"] = Place.DormLife,
        ["dormw"] = Place.DormW, ["dorms"] = Place.DormS, ["dorml"] = Place.DormL, ["resonance"] = Place.Resonance,
    }.Concat(Enum.GetValues<Place>().Select(p => new KeyValuePair<string, Place>(Of(p), p))).ToImmutableDictionary();
}

/// <summary>一条习惯：在什么条件下做什么、权重多少、是否与核心欲望相连（恶魔放大欲望时随之加权）。</summary>
public sealed record HabitSpec(Job Job, Slot[] Slots, int Weight, bool Desire = false, string Note = "");

public enum Gender { Male, Female }
```

### CH2-096

**范围与类型：** `production` / `schema`。**索引说明：** 限定本章公告板实体可绑定的具体地点集合。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:38](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:38)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:39)，L39–L39。

```csharp
            if(entity.Kind=="object"&&local.StartsWith("Board.",StringComparison.Ordinal)&&Enum.TryParse<Place>(local[6..],out var board)&&board is Place.Hall or Place.Logistics or Place.Lecture or Place.DormLife or Place.DormW or Place.DormS or Place.DormL or Place.Living)continue;
```

### CH2-097

**范围与类型：** `production` / `schema`。**索引说明：** 限定本章废纸篓实体只存在于四个宿舍区。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:40](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:40)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:41)，L41–L52。

```csharp
            if(entity.Kind=="object"&&local.StartsWith("Litter.",StringComparison.Ordinal)&&Enum.TryParse<Place>(local[7..],out var litter)&&litter is Place.DormLife or Place.DormW or Place.DormS or Place.DormL)continue;
            throw new InvalidDataException("Unknown formal binding: "+entity.Id);
        }
    }
    /// <summary>按剧本里的物件编号（object.X / Board.地点 / Litter.地点）取游戏里的实体。</summary>
    public static Essence ResolveId(Game game,string id)=>
        id.StartsWith("Board.",StringComparison.Ordinal)?game.Board[Enum.Parse<Place>(id[6..])]:
        id.StartsWith("Litter.",StringComparison.Ordinal)?game.Litter[Enum.Parse<Place>(id[7..])]:Objects[id[7..]](game);
    public static Essence Resolve(Game game,WorldEntityDefinition entity)
    {
        var local=Local(entity);
        return entity.Kind=="character"?game[PersonId(entity)].Body:
```

### CH2-098

**范围与类型：** `production` / `schema`。**索引说明：** 固定本章价值聚落类别及其社会阵营语义。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Model.cs:17](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:17)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Model.cs](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:18)，L18–L20。

```csharp
public enum Cluster { None, Order, Labor, Beauty, Evidence }

/// <summary>NPC 在一个时段（或夜里的一段）里能做的事。每项编译为 Core Effect，见 Jobs。</summary>
```

### CH2-099

**范围与类型：** `production` / `schema`。**索引说明：** 固定本章议事规则语义位，包括具名目标、档案院权限及夜间议案条件。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Model.cs:34](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:34)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Model.cs](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:35)，L35–L42。

```csharp
public enum RF
{
    None = 0, Records = 1, SickPriority = 2, PerHead = 4, Restricts = 8, TargetsQuer = 16, CentralCook = 32,
    EvidenceFirst = 64, OpenArchive = 128, CommitteeArchive = 256, SealArchive = 512, PactArchive = 1024,
    NightException = 2048, TwoWitness = 4096, WardAuthority = 8192, Tradition = 16384, Relay = 32768,
    Safety = 65536, NoException = 131072, ForbidNightOpen = 262144, ForbidsDesire = 524288, Rations = 1048576, Targets = 2097152
}

```

## S04

**对应建议：** 个体信念、习惯与立场编译为主观规则。18 处源码、0 处资源。

### AWK-008

**范围与类型：** `production` / `logic`。**索引说明：** 按 V5/V6 设定侦探对特定同伴的信任偏移与熟人信任。

**标记位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs:281](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:281)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:282)，L282–L293。

```csharp
    private int TrustOf(Person self, Person other)
    {
        var trust = _v.V6 == Acquaintance.Familiar ? 2 : -1;
        if (self == Person.Detective &&
            (_v.V5 == DetectiveHeeds.Huo && other == Person.Huo || _v.V5 == DetectiveHeeds.Su && other == Person.Su))
            trust += 1;
        return trust;
    }

    // ---------------------------------------------------------------- habits --

    private static readonly Noun Me = new Noun.Slot(RuleSlot.Actor);
```

### AWK-009

**范围与类型：** `production` / `logic`。**索引说明：** 逐人写入昨夜具体行为、认知规则、门/烟/物证互动和请求回应。

**标记位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs:309](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:309)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:310)，L310–L321。

```csharp
    private void WriteHabits(Agent detective, Agent huo, Agent shen, Agent su)
    {
        // ---- 侦探：物证先于言辞。只有警铃时他不动；听见喊声循声而去；听到关于门的说法而不全信时，自己去门边看。
        //      V5=“谁都不全信，自己去看”时，这一习惯更强。
        {
            var a = detective;
            Projection M(Essence e) => a.Mental[e];
            var self = _v.V5 == DetectiveHeeds.Himself ? 3 : 0;
            var hall = M(_space[Room.Hall]);
            var foyer = M(_space[Room.Foyer]);
            AddHabit(a, "follow_shout", "", 2,
                All(Ex(a.Alarm), Ex(a.Shout), Not(At(Me, hall)), Not(Ex(a.Claim))), Look(hall));
```

### AWK-017

**范围与类型：** `production` / `logic`。**索引说明：** 为有具体旧物回忆的人写入欲望、守则、铜口信任、劝告与触物行为规则。

**标记位置：** [godot_csharp/scenes/Awakening/Night/MorningWorld.cs:95](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:95)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/MorningWorld.cs](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:96)，L96–L107。

```csharp
    private void WriteMind(RecallMemory memory)
    {
        var who = memory.Who;
        var soul = _souls[who];
        var views = _runtime.Representations(_bodies[who]).Concat(_runtime.Representations(_bodies[Person.Detective]))
            .Concat(_runtime.Representations(_evidence[memory.Evidence]))
            .Concat(_runtime.Representations(_space[_evidenceRoom[memory.Evidence]])).ToArray();
        var seen = _runtime.Observe(soul, views, 0);
        var mind = new Mind { Self = seen[0], Detective = seen[1], Object = seen[2], Room = seen[3], Desire = _desires[who], Memory = memory };
        Observation.Observe(soul, new ObservationInput(mind.Detective.Name, mind.Detective.Kind)
        {
            Target = mind.Detective, Source = ObservationSource.Perception,
```

### AWK-018

**范围与类型：** `production` / `logic`。**索引说明：** 将本案角色取舍、知守则与劝告分支映射到专属心声。

**标记位置：** [godot_csharp/scenes/Awakening/Night/MorningWorld.cs:303](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:303)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/MorningWorld.cs](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:304)，L304–L315。

```csharp
    public RecallDecision? DecideRecall(string evidence)
    {
        var memory = Memories.FirstOrDefault(m => m.Evidence == evidence);
        if (memory is null || !_minds.TryGetValue(memory.Who, out var mind)) return null;
        var options = DecisionSystem.Assess(_souls[memory.Who].World, mind.Self).Where(a => !a.Forbidden).ToArray();
        var touch = options.FirstOrDefault(o => Ast.Equivalent(o.Decision.Effect, mind.Touch))?.Decision.Score ?? 0;
        var decline = options.FirstOrDefault(o => Ast.Equivalent(o.Decision.Effect, mind.Decline))?.Decision.Score ?? 0;
        var choice = touch > decline ? RecallChoice.Touch : touch < decline ? RecallChoice.Refuse : RecallChoice.Hesitate;
        var wants = DesireOf(memory.Who);
        var knows = mind.Rule1.Existence == Truth.True;
        var advisedTouch = mind.AdviceTouch.Existence == Truth.True;
        var advisedDecline = mind.AdviceDecline.Existence == Truth.True;
```

### AWK-019

**范围与类型：** `production` / `logic`。**索引说明：** 独处时犹豫即放下是本章角色回忆规则。

**标记位置：** [godot_csharp/scenes/Awakening/Night/MorningWorld.cs:336](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:336)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/MorningWorld.cs](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:337)，L337–L348。

```csharp
    public RecallDecision? DecideAlone(string evidence)
    {
        var decision = DecideRecall(evidence);
        if (decision is null || decision.Choice != RecallChoice.Hesitate) return decision;
        return decision with { Choice = RecallChoice.Refuse, Thought = decision.Thought + Chapter1Data.Text("取舍·无人可问") };
    }

    private void Evidence(string id, string name, Room room, params (string Tag, int Value)[] facts)
    {
        _evidenceRoom[id] = room;
        var essence = new Essence(name);
        essence.Add(new SpatialProperty { Space = _space[room] });
```

### AWK-020

**范围与类型：** `production` / `logic`。**索引说明：** 固定以公告框第一条作为角色读到的代价规则。

**标记位置：** [godot_csharp/scenes/Awakening/Night/MorningWorld.cs:254](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:254)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/MorningWorld.cs](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:255)，L255–L266。

```csharp
    public void ShowRule1(Person who)
    {
        if (!_minds.TryGetValue(who, out var mind)) return;
        Observation.Observe(_souls[who], new ObservationInput(mind.Rule1.Name, mind.Rule1.Kind)
        {
            Target = mind.Rule1, Source = ObservationSource.Perception, Existence = Truth.True, Time = ++_time, Details = PostedRules[0]
        });
    }

    public bool KnowsRule1(Person who) => _minds.TryGetValue(who, out var mind) && mind.Rule1.Existence == Truth.True;

    /// <summary>
```

### CH2-001

**范围与类型：** `production` / `logic`。**索引说明：** 聚落阵营规则的具体身份、时段和后果在代码中定义。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Alliances.cs:16](../../godot_csharp/scenes/Chapter2/Prototype/Alliances.cs:16)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Alliances.cs](../../godot_csharp/scenes/Chapter2/Prototype/Alliances.cs:17)，L17–L28。

```csharp
    private void WriteAllianceRules()
    {
        // 劳作互助派（沈鼎）：钥匙交给做饭的人，统一煮，先喂饿着的人。接受者若手里有钥匙，就会交出去。
        ShenKitchen = Spec("ShenKitchen",
            fx => [new(Fx.All(fx.AtAny([Slot.Noon, Slot.Evening]), Fx.Mine(T.GranaryKey), Fx.Not(Fx.Mine(T.Is[P.Shen]))), RuleMode.Prefer, fx.GiveKeyTo(this[P.Shen]), 4)]);
        // 实证派（维罗妮卡）：指控要有实物。
        VeronicaEvidence = Spec("VeronicaEvidence",
            fx => [new(Fx.Not(Fx.Mine(T.SawAct)), RuleMode.Forbid, fx.Job(Job.Accuse), 3)]);
        // 感官与美派（卢西安）：不得以私取之名限制任何人。
        LucianNoBrand = Spec("LucianNoBrand",
            fx => [new(Fx.All(fx.At(Slot.Morning), Fx.Any([.. People.Select(o => fx.Tg(o.Body, T.Accused))])), RuleMode.Prefer, fx.Job(Job.Defend), 2)]);
        // 各聚落领头人何时何处张贴自己的规矩，写在剧本的“规矩”块里（张贴：第 N 日 时段 地点）。
```

### CH2-005

**范围与类型：** `production` / `logic`。**索引说明：** 为具体人物初始化特定规则、时段、目标与结果。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Rules.cs:114](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:114)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Rules.cs](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:115)，L115–L126。

```csharp
    private void WriteRules()
    {
        WardLedger = Spec("WardLedger",
            fx => [new(Fx.Constant, RuleMode.Forbid, fx.Job(Job.NightTakeSelf), 2), new(Fx.Constant, RuleMode.Forbid, fx.Job(Job.NightTakeSick), 2)]);
        WardNight = Spec("WardNight",
            fx => [new(Fx.All(fx.At(Slot.Night), Fx.Mine(T.Branded)), RuleMode.Forbid, fx.Go(Place.Logistics), 2)]);
        MoraNight = Spec("MoraNight",
            fx => [new(Fx.All(fx.At(Slot.Night), Fx.Not(Fx.Mine(T.Fed)), fx.NotDone(Job.Share), fx.NotDone(Job.NightTakeSelf)), RuleMode.Prefer, fx.Job(Job.NightTakeSelf), 3), // 试过、没做成，今夜不再试
                   new(Fx.All(fx.AtAny([Slot.Morning, Slot.Forenoon, Slot.Noon, Slot.Afternoon, Slot.Evening]), Fx.Mine(T.OweNote)), RuleMode.Prefer, fx.Job(Job.Inform), 2)]);
        RuanLedger = Spec("RuanLedger",
            fx => [new(Fx.All(fx.At(Slot.Evening), Fx.Mine(T.Duty), fx.NotDone(Job.CountLedger)), RuleMode.Prefer, fx.Job(Job.CountLedger), 1)]);
        Warning1 = Spec("Warning1", fx => [new(Fx.Constant, RuleMode.Forbid, fx.Job(Job.AnswerName), 5)]);
```

### CH2-008

**范围与类型：** `production` / `logic`。**索引说明：** 阵营成员集合固定引用本章具体人物身份。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Social.cs:282](../../godot_csharp/scenes/Chapter2/Prototype/Social.cs:282)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Social.cs](../../godot_csharp/scenes/Chapter2/Prototype/Social.cs:283)，L283–L294。

```csharp
    public List<Faction> Factions()
    {
        var core = Specs.Where(s => !s.ByDemon && s != RuanLedger && s.Author is not null).ToArray();
        var leaders = new[] { P.Ward, P.Shen, P.Lucian, P.Veronica, P.Det };
        var centers = core.Select(s => s.Author!.Value).Distinct()
            .Where(a => leaders.Contains(a) || core.Where(s => s.Author == a).Any(s => People.Count(p => p.Id != a && p.Acc.GetValueOrDefault(s) == Truth.True) >= 2))
            .Select(a => this[a]).ToArray();
        int Pull(Person p, Person c) =>
            core.Where(s => s.Author == c.Id).Sum(s => p.Acc.GetValueOrDefault(s) == Truth.True ? 2 : p.Vetoed.Contains(s) ? -2 : 0)
            + Math.Clamp(c == Det ? ImageOfDet(p).Credible : TrustIn(p, c), -3, 2)
            + (c != Det && Cast.ClusterOf(c.Id) != Cluster.None && Cast.ClusterOf(c.Id) == Cast.ClusterOf(p.Id) ? 2 : 0);
        var join = new Dictionary<Person, Person>();
```

### CH2-013

**范围与类型：** `production` / `logic`。**索引说明：** 初始化不同具体人物对角色、身份、关系、规则及旧案的认知。

**标记位置：** [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:297](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:297)。

**原文位置：** [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:298)，L298–L309。

```csharp
    private void Know(Person p)
    {
        var previouslyKnown = p.M.Keys.ToHashSet();
        var sources = View.Keys.Where(e => !p.Inherited || !previouslyKnown.Contains(e)).ToArray();
        var mental = Rt.Observe(p.Soul, sources.Select(e => View[e]), Now);
        for (var i = 0; i < sources.Length; i++) p.M[sources[i]] = mental[i];
        p.Self = p.M[p.Body];
        if (p.Inherited)
            Observation.Observe(p.Soul, new ObservationInput(p.Self.Name,p.Self.Kind)
            { Target=p.Self, Source=ObservationSource.Perception, Time=Now, Move=true, Parent=p.M[Space[Where(p)]] });
        Projection Concept(string name, ProjectionKind kind, ObservationSource source) => Observation.Observe(p.Soul,
            new ObservationInput(name, kind) { Source = source, Existence = Truth.True });
```

### CH2-029

**范围与类型：** `production` / `logic`。**索引说明：** 背景人物按具体身份、日期和关系执行预设专属行为。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:341](../../godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:341)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs](../../godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:342)，L342–L353。

```csharp
    private void BackgroundLines(Person p, Fx fx, List<Rule> rules)
    {
        void Daily(Job j, Slot[] slots, int w, Condition? extra = null) =>
            rules.Add(new Rule(Fx.All([fx.AtAny(slots), fx.NotDone(j), .. extra is null ? Array.Empty<Condition>() : [extra]]), RuleMode.Prefer, p.JobFx[j], w, Truth.True));
        switch (p.Id)
        {
            case P.Elena: Daily(Job.Treat, [Slot.Morning, Slot.Forenoon], 4); Daily(Job.Retreat, [Slot.Afternoon], 3); break;
            case P.Bertrand: Daily(Job.Report, [Slot.Morning], 5, fx.Tg(Clock, T.Day, FirstDay + 1)); break; // 始业日前夜各寝区都闩着，没有巡夜可报
            case P.Veronica:
                Daily(Job.Sample, [Slot.Afternoon], 3);
                rules.Add(new Rule(Fx.All(fx.AtAny([Slot.Noon, Slot.Evening]), new Condition.At(fx.R(this[P.Mora].Body), new Noun.Slot(RuleSlot.Scene)), fx.NotDone(Job.Needle)), RuleMode.Prefer,
                    Fx.Seq(new Effect.Say(fx.R(this[P.Mora].Body), "你夜里的取用，误差是多少？"), Fx.MeChange(T.Did(Job.Needle), 1)), 4, Truth.True));
```

### CH2-034

**范围与类型：** `production` / `logic`。**索引说明：** 按具体人物身份写入对议案措辞的立场偏好。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Rules.cs:353](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:353)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Rules.cs](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:354)，L354–L365。

```csharp
    private int StanceTerms(Person p, RuleSpec s, List<string> parts)
    {
        var fam = FamilyOf(s);
        var score = 0;
        void Add(int v, string why) { if (v == 0) return; score += v; parts.Add($"{why}{v:+0;-0;0}"); }
        if (p.Witnessed.Contains("hook")) Add(fam switch { Ending.OpenEach => -2, Ending.Sealed or Ending.DetectivePact => 1, _ => 0 }, "亲眼见过应了唤名的人");
        if (p.Witnessed.Contains("crack")) Add(fam switch { Ending.OpenEach => -4, Ending.DetectivePact or Ending.Committee => 1, _ => 0 }, "亲眼见过基座的裂纹");
        else if (p.Witnessed.Contains("crack-heard")) Add(fam == Ending.OpenEach ? -1 : 0, "听亲眼见过的人说基座裂了");
        if (p.Witnessed.Contains("machine")) Add(fam == Ending.DetectivePact ? 2 : 0, "亲眼见过侦探把机关安稳地试开");
        if (p.Acc.GetValueOrDefault(s) == Truth.True) Add(1, "点过头");
        if (p.StanceNudge.TryGetValue(fam, out var n)) Add(Math.Min(1, n), "饭桌上被说动");
        if (ClaimsOf(p).Any(c => c.Predicate == "石碑条文")) Add(fam switch { Ending.OpenEach => -1, Ending.DetectivePact => 1, _ => 0 }, "听说石碑上还有第九条");
```

### CH2-035

**范围与类型：** `production` / `logic`。**索引说明：** 为各具体人物预设议案条件与反对理由。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Rules.cs:447](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:447)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Rules.cs](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:448)，L448–L458。

```csharp
    private static IEnumerable<(RF Need, int Penalty, string Text)> Requirements(P p) => p switch
    {
        P.Ruan => [(RF.Records, 2, "不记账就对不上账")],
        P.Quer => [(RF.NightException | RF.TwoWitness | RF.EvidenceFirst, 2, "又是一条只抓人的规矩")],
        _ => []
    };

    /// <summary>
    /// 底线价值（§11 第 4 条）：每人一至两条，触犯即拒绝。返回此人会说的那句话；没触犯返回 null。
    /// 上了钩的人多一条：名录库“入夜一律不行”——名字在叫他。
    /// </summary>
```

### CH2-036

**范围与类型：** `production` / `logic`。**索引说明：** 指定人物对特定议案的否决底线。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Rules.cs:459](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:459)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Rules.cs](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:460)，L460–L471。

```csharp
    public string? Veto(Person p, RuleSpec s)
    {
        var f = s.Flags;
        bool Has(RF x) => (f & x) != 0;
        var openPlain = Has(RF.OpenArchive) && !Has(RF.PactArchive);
        if (p.Hooks > 0 && Has(RF.ForbidNightOpen)) return "夜里有人在叫我的名字。不许我去，不行。";
        return p.Id switch
        {
            P.Ward when Has(RF.Rations) && !Has(RF.Records | RF.TwoWitness) => "依据：无记录，即不可。",
            P.Ward when openPlain && !Has(RF.Records | RF.TwoWitness) => "依据：无记录，即不可。",
            P.Mora when Has(RF.Rations) && Has(RF.NoException | RF.Restricts) && !Has(RF.SickPriority | RF.NightException) => "病人不能等。谁定的规矩我都不认。",
            P.Mora when openPlain => "取回的东西会伤人。我不同意随便开。",
```

### CH2-041

**范围与类型：** `production` / `logic`。**索引说明：** 为具名人物预置习惯行动及其观察规则。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:163](../../godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:163)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs](../../godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:164)，L164–L175。

```csharp
    public ImmutableArray<Rule> HabitRules(Person p)
    {
        var fx = FxOf(p);
        var rules = new List<Rule>();
        void Add(Job job, Condition when, int weight) => rules.Add(new Rule(when, RuleMode.Prefer, p.JobFx[job], Math.Max(weight, 0), Truth.True));
        Condition Base(Job job, IEnumerable<Slot> slots) => Fx.All(fx.AtAny(slots), fx.NotDone(job));
        foreach (var h in p.Pro.Habits)
        {
            var weight = h.Weight + (h.Desire ? p.Desire - 1 : 0);
            var c = Base(h.Job, h.Slots);
            c = h.Job switch
            {
```

### CH2-042

**范围与类型：** `production` / `logic`。**索引说明：** 按本章人物身份与案件状态配置行动计划。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:259](../../godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:259)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs](../../godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:260)，L260–L271。

```csharp
    private void PlanRules(Person p, Fx fx, List<Rule> rules)
    {
        var door = ArchiveDoor;
        var myStation = fx.Tg(StationFor(p), T.Occupied);
        rules.Add(new Rule(Fx.All(fx.At(Slot.Night), new Condition.Related(Fx.Me, Fx.Promised, fx.R(door)), fx.NotDone(Job.Seize), Fx.Not(myStation)), RuleMode.Prefer, p.JobFx[Job.Seize], 6, Truth.True));
        // 赴约或领头去开库的人，在前厅撞见守门的人：请他让开（他让不让，由他自己的 SocialSystem 决定）。
        var leave = Fx.Seq(fx.Set(WeightRoom, T.Occupied, 0), new Effect.Set(Fx.Me, T.Guarding, 0), fx.Go(Place.Hall));
        rules.Add(new Rule(Fx.All(fx.At(Slot.Night), Fx.Any([new Condition.Related(Fx.Me, Fx.Promised, fx.R(door)), .. People.Where(o => o != p).Select(o => (Condition)new Condition.Related(fx.R(o.Body), Fx.Asked, fx.R(door)))]),
                new Condition.IsKind(Fx.Target, ProjectionKind.Body), new Condition.Tagged(Fx.Target, T.Guarding, 1), new Condition.At(Fx.Target, new Noun.Slot(RuleSlot.Scene)),
                Fx.Not(new Condition.Related(Fx.Target, Fx.AskedLeave, fx.R(door)))), RuleMode.Prefer,
            Fx.Seq(new Effect.Say(Fx.Target, "让开。今夜这道门我们要开。", leave), new Effect.Think(new Condition.Related(Fx.Target, Fx.AskedLeave, fx.R(door))),
                Fx.MeChange(T.Did(Job.AskLeave), 1)), 7, Truth.True));
```

### CH2-065

**范围与类型：** `production` / `logic`。**索引说明：** 按本案特定行为积累或消除嫌疑。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:637](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:637)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:638)，L638–L649。

```csharp
    private void Suspicions()
    {
        foreach (var p in Npcs)
        {
            if (p.Acc.GetValueOrDefault(WardLedger) != Truth.True && WardLedger.Author != p.Id) continue;
            if ((Belief(p, p.M[Ledger], T.Unrecorded) ?? 0) < 1) continue;
            if (Specs.Any(s => s.Card is { Behavior: 0 } && s.Flags.HasFlag(RF.NightException) && s.Flags.HasFlag(RF.Records) && p.Acc.GetValueOrDefault(s) == Truth.True)) continue;
            var smallSeen = ClaimsOf(p).Any(c => SawFigure(c, Build.Small));
            foreach (var q in People.Where(q => q != p && q != Det))
            {
                if ((Belief(p, q, T.Branded) ?? 0) < 1 || (Belief(p, q, T.Suspect) ?? 0) > 0) continue;
                Know(p, q.Body, T.Suspect, 1, ObservationSource.Inference, smallSeen ? "账上少了，夜里又有个瘦小的身影" : "账上少了");
```

### CH2-073

**范围与类型：** `production` / `logic`。**索引说明：** 指定首课答题后特定人群对教师的反应。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Host.cs:96](../../godot_csharp/scenes/Chapter2/Prototype/Host.cs:96)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Host.cs](../../godot_csharp/scenes/Chapter2/Prototype/Host.cs:97)，L97–L108。

```csharp
    public void HostLessonAnswer(bool precise)
    {
        if (!precise) return;
        // 首课在集合之后、全体随之进讲堂；作答时众人可能还在中央厅，两处都算在场。
        var present = People.Where(p => p != Det && Get(p.Body, T.Sick) == 0 && Where(p) is Place.Lecture or Place.Hall).ToArray();
        Witness(Det, K("回答受赞", P.Det), 0, 1, present.Where(p => p.Pro.DemonPrejudice >= 0), domain: Dom.Archive);
        Witness(Det, K("宠儿", P.Det), -1, 0, present.Where(p => p.Pro.DemonPrejudice < 0));
        // 当众读出“自负其果，果是什么，条文一字未提”：本就不偏信它、欲望也不系在名录库上的人对它那条“凡学员皆可取回己物”多了一分疑心。
        foreach (var p in present.Where(p => p.Pro.DemonPrejudice <= 0 && !p.Pro.DesireFlags.HasFlag(RF.OpenArchive)))
        {
            p.DemonDoubt = Math.Min(2, p.DemonDoubt + 1);
            NoteFor(p.Id, "铜口", $"首课上听到“果是什么，条文一字未提”时，{p.Pro.Pronoun}看了一眼黑板上的第一条");
```

## S05

**对应建议：** 感知与传播法则驱动认知更新。6 处源码、0 处资源。

### AWK-001

**范围与类型：** `production` / `logic`。**索引说明：** 固定传播表规定铜口完整/接头句尾/隔墙低语/其余警铃，以及门内外喊声组合；拓扑与接头配置读取故事数据。

**标记位置：** [godot_csharp/scenes/Awakening/Night/NightVariables.cs:91](../../godot_csharp/scenes/Awakening/Night/NightVariables.cs:91)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/NightVariables.cs](../../godot_csharp/scenes/Awakening/Night/NightVariables.cs:92)，L92–L103。

```csharp
    public static Hearing Hear(VoiceOrigin origin, Room listener, IReadOnlyDictionary<string, bool>? loose = null)
    {
        var data = Loaded.Value;
        var leaking = data.Joints.Where(j => loose?.GetValueOrDefault(j.Name, j.Loose) ?? j.Loose).Select(j => j.Room).ToHashSet();
        if (origin == VoiceOrigin.Tube)
            return listener == data.TubeRoom ? Hearing.Full : leaking.Contains(listener) ? Hearing.Tail
                : data.Murmur.Contains(listener) ? Hearing.Murmur : Hearing.AlarmOnly;
        if (listener == data.DoorOuter) return Hearing.Full;
        if (listener == data.DoorInner) return Hearing.FullMuffled;
        return leaking.Contains(data.DoorInner) && leaking.Contains(listener) ? Hearing.FullMuffled : Hearing.Nothing;
    }

```

### AWK-011

**范围与类型：** `production` / `logic`。**索引说明：** 把本案话语的语义标记映射到人物对指令、门外人和求救的认知。

**标记位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs:493](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:493)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:494)，L494–L505。

```csharp
    private void Hear(Agent a, Utterance words, Hearing hearing)
    {
        switch (hearing)
        {
            case Hearing.Full:
                KnowConcept(a, a.Order, ObservationSource.Perception,
                    (NightTags.Bolt, words.Bolt), (NightTags.ForbidOpen, words.ForbidOpen), (NightTags.HoldTube, words.HoldTube),
                    (NightTags.OpenOrder, words.Open), (NightTags.FromTube, _v.V3 == VoiceOrigin.Tube));
                KnowConcept(a, a.Heard, ObservationSource.Perception, (NightTags.ClearBolt, words.Bolt), (NightTags.FullOpen, words.Open));
                if (words.Open) KnowConcept(a, a.Outsider, ObservationSource.Perception, (NightTags.WantsOpen, true), (NightTags.Help, words.Help));
                break;
            case Hearing.FullMuffled:
```

### AWK-021

**范围与类型：** `production` / `schema`。**索引说明：** 将现实声路接头状态投影到 E8 铜口证物的固定事实标签。

**标记位置：** [godot_csharp/scenes/Awakening/Night/MorningWorld.cs:439](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:439)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/MorningWorld.cs](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:440)，L440–L451。

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
    }

    // ---- 推理与言语所需的认知读写（总纲 §六 第二期）：只经 Observation.Observe 写入，只读本人认知 ----
```

### CH2-014

**范围与类型：** `production` / `logic`。**索引说明：** 限定具体空间的夜间视线并生成身形目击和专案反馈。

**标记位置：** [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:495](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:495)。

**原文位置：** [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:496)，L496–L507。

```csharp
    private void NightSight(Person p, Place here)
    {
        if (!SightLines.TryGetValue(here, out var lines) || p.Asleep) return;
        foreach (var at in lines)
        foreach (var other in People.Where(o => o != p && Where(o) == at && !o.Asleep))
        {
            if (Sightings.Any(s => s.Day == Day && s.Witness == p.Id && s.At == at && s.Truth == other.Id)) continue;
            var s = new Sighting(Day, p.Id, here, at, other.Pro.Build, other.Id, Now);
            Sightings.Add(s);
            var glimpse = Prototype.Game.K("望见身影", p.Id, null, at, Day, ("从", Names.Of(here)), ("身形", BuildText(other.Pro.Build)), ("build", other.Pro.Build));
            var text = Plain(glimpse);
            Observation.Observe(p.Soul, new ObservationInput(text, ProjectionKind.Event)
```

### CH2-037

**范围与类型：** `production` / `logic`。**索引说明：** 固定本章恶魔词、基座裂缝指认及指控/教训后果。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Social.cs:211](../../godot_csharp/scenes/Chapter2/Prototype/Social.cs:211)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Social.cs](../../godot_csharp/scenes/Chapter2/Prototype/Social.cs:212)，L212–L223。

```csharp
    public void Receive(Person teller, Person hearer, NewsItem item, Heard via, Channel channel = Channel.Relay)
    {
        var trust = TrustIn(hearer, teller);
        if (KnowsClaim(hearer, item.Claim)) { hearer.News[item.Id] = new(teller.Id, via.Hops + 1, ObservationSource.Testimony, Day, item.Id, via.Slant); return; }
        var delta = item.TrustDelta;
        var slant = via.Slant;
        if (item.About is { } about && delta < 0 && teller.Pro.Prejudice.GetValueOrDefault(about) < 0 && slant is null && teller.Id != item.Origin)
        {
            delta -= 1;
            slant = teller.Id;
        }
        var heard = new Heard(teller.Id, via.Hops + 1, ObservationSource.Testimony, Day, item.Id, slant);
```

### CH2-064

**范围与类型：** `production` / `logic`。**索引说明：** 判定本案夜间取物是否被特定证据和目击捕捉。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:556](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:556)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:557)，L557–L568。

```csharp
    private void NightSeenTaking(Person taker, bool recorded)
    {
        var take = _currentEvent;
        foreach (var w in People.Where(w => w != taker && w != Det && !w.Asleep && Where(w) == Place.Logistics))
        {
            var permits = MoraNight.Author == w.Id || w.Acc.GetValueOrDefault(MoraNight) == Truth.True
                || Specs.Any(s => s.Card is { Behavior: 0 } && s.Flags.HasFlag(RF.NightException) && w.Acc.GetValueOrDefault(s) == Truth.True);
            var condemns = WardLedger.Author == w.Id || w.Acc.GetValueOrDefault(WardLedger) == Truth.True;
            // 同一件事，说出口的样子随看见的人认了哪条规矩而不同（他心里的这件事本来就不同）。
            var seenAs = K(recorded ? "夜见取粮记账" : condemns && !permits ? "夜见私取" : permits ? "夜见照规取粮" : "夜见取粮", w.Id, taker.Id, Place.Logistics, Day);
            var text = Plain(seenAs);
            var id = RecordEvent(w.Id, Say(seenAs with { Subject = null, Night = 0 }).Trim(), "看守", Place.Logistics, [take], job: Job.Watch);
```

## S06

**对应建议：** 世界法则声明物理后果和劳作配方。6 处源码、0 处资源。

### AWK-015

**范围与类型：** `production` / `logic`。**索引说明：** 本案门状态变化、烟灰、鞋印、湿布压痕与喊声听闻的物理后果。

**标记位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs:846](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:846)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:847)，L847–L858。

```csharp
    private void Physics(Agent actor)
    {
        var door = DoorState();
        var changed = door != _lastDoor;
        var boltedNow = door.Item3 == 1 && _lastDoor.Bolted == 0;
        _lastDoor = door;
        UpdateSmoke();
        var trace = _floor.Get<FloorTraceProperty>()!;
        if (changed && actor.StartRoom == Room.Foyer && Tag(_floor, NightTags.AshFan) == 1)
            Print(actor, boltedNow ? PrintToward.Bolt : PrintToward.DoorGap);
        if (Tag(_cloth, NightTags.Placed) == 1 && trace.Prints.Count > 0) trace.ClothOverHeel = true;
        // 霍铎朝走廊的喊声：走廊与宿舍里的人都听得见。
```

### CH2-040

**范围与类型：** `production` / `logic`。**索引说明：** 把具体本章工作映射为案件物证与人物行动结果。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:61](../../godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:61)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs](../../godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:62)，L62–L73。

```csharp
    public Effect Job(Job job, Person? who = null) => job switch
    {
        Prototype.Job.Sleep => Seq(Go(who!.Pro.Dorm), Look(g.Space[who.Pro.Dorm])),
        Prototype.Job.Rest => Seq(Go(who!.Pro.Dorm), Look(g.Space[who.Pro.Dorm]), Wait(3, Look(g.Space[who.Pro.Dorm]))),
        Prototype.Job.Work => Seq(Go(Game.WorkPlace(who!.Id)), Look(g.Space[Game.WorkPlace(who.Id)]), Wait(2.5, Look(g.Space[Game.WorkPlace(who.Id)]))),
        Prototype.Job.Class => Seq(Go(Place.Lecture), Look(g.Blackboard)),
        Prototype.Job.Collect => Seq(Go(Place.Logistics), new Effect.Wait(Tg(g.Desk, T.Open),
            Seq(Need(Tg(g.Granary, T.Ration)), Spend(g.Granary, T.Ration, 1), MeChange(T.Fed, 1), Change(g.Ledger, T.Handed, 1)))),
        Prototype.Job.Distribute => Seq(Go(Place.Logistics), Need(Mine(T.GranaryKey)), Set(g.Granary, T.Locked, 0), Set(g.Desk, T.Open, 1), Look(g.Ledger)),
        Prototype.Job.Cook => Seq(Go(Place.Logistics), Need(Tg(g.Jar, T.Water)), Spend(g.Jar, T.Water, 1), Set(g.Stove, T.Cooked, 1)),
        Prototype.Job.FetchWater => Seq(Go(Place.Logistics), Change(g.Jar, T.Water, 3)),
        Prototype.Job.Care => Seq(Go(Place.Clinic), Look(g.Bed), When(Mine(T.MedKey), Seq(Need(Tg(g.MedCab, T.Medicine)), Spend(g.MedCab, T.Medicine, 1))), Change(g.Bed, T.Treated, 1)),
```

### CH2-062

**范围与类型：** `production` / `logic`。**索引说明：** 把指定案件行动完成映射为本章固定后果。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:332](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:332)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:333)，L333–L344。

```csharp
    private void OnCompleted(ActionExecution exec)
    {
        using var _ = Prof.Time("C:OnCompleted");
        var p = People.FirstOrDefault(x => ReferenceEquals(x.Self, exec.Intent.Actor));
        if (Story?.Owns(exec) == true)
        {
            if (p is not null) { p.Exec = null; p.Doing = null; }
            PerceiveAll();
            return;
        }
        if (p is null) return;
        var (job, why, _) = _running.GetValueOrDefault(exec);
```

### CH2-063

**范围与类型：** `production` / `logic`。**索引说明：** 结算本章人物、作案物件与调查线索的专属物理结果。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:428](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:428)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:429)，L429–L440。

```csharp
    private void Physics(Person p, Job? job, ActionExecution exec)
    {
        CompletePostIntents(p, exec);
        var place = Where(p);
        if (Slot == Slot.Night) p.NightVisited.Add(place);
        NightNoise(p, job);
        switch (job)
        {
            case Job.Sleep when Slot == Slot.Night: p.Asleep = true; break;
            case Job.NightTakeSickRec or Job.NightTakeSelfRec:
                NightSeenTaking(p, true);
                NightTruth.Add($"D{Day} {p.Name} {Names.Of(job.Value)}");
```

### CH2-068

**范围与类型：** `production` / `logic`。**索引说明：** 结算本案温室特定事件、人物和物件状态。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:836](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:836)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:837)，L837–L848。

```csharp
    private void GreenhousePhysics()
    {
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

### CH2-070

**范围与类型：** `production` / `logic`。**索引说明：** 结算阮、贝特朗、莫拉与茉拉相关的专属油料剧情。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:1011](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:1011)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:1012)，L1012–L1023。

```csharp
    private void OilPhysics()
    {
        var holder = People.FirstOrDefault(p => Get(p.Body, T.OilKey) > 0);
        var lamps = 0;
        if (holder is not null)
        {
            lamps = 1 + People.Count(p => p != holder && p.Pro.Dorm != holder.Pro.Dorm && TrustIn(holder, p) >= 2 && p.Id is P.Bertrand or P.Mora) ;
            lamps += Get(this[P.Moran].Body, T.Sick) > 0 ? 1 : 0;
        }
        lamps = Math.Min(lamps + 1, Get(OilStore, T.Oil));
        Add(OilStore, T.Oil, -lamps);
    }
```

## S07

**对应建议：** 剧本声明交互条件、试验和演出流程。16 处源码、0 处资源。

### AWK-022

**范围与类型：** `production` / `logic`。**索引说明：** 第一章开场固定摆放侦探与三名同伴并启动醒来流程。

**标记位置：** [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:14](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:14)。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:15)，L15–L26。

```csharp
    private IEnumerable<Op> Begin()
    {
        foreach (var who in Party)
            _out.Add(StageOp("place", ("who", who), ("to", new Ev { ["anchor"] = "起点·" + who })));
        foreach (var who in Companions) _out.Add(StageOp("pose", ("who", who), ("anim", "idle"), ("facing", "front")));
        _out.Add(StageOp("sit", ("who", "detective"), ("facing", "front"), ("lift", 4.0)));
        Music("door_side");
        yield return new Run(PlayPlan(() => _content.BeginWake(_world)));
    }

    /// <summary>C# 剧本计划逐项给出场面：对白标题，或 @命令。</summary>
    private IEnumerable<Op> PlayPlan(Action begin)
```

### AWK-023

**范围与类型：** `production` / `logic`。**索引说明：** 开门命令固定联动隔离门状态、音效和主旋律。

**标记位置：** [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:141](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:141)。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:142)，L142–L153。

```csharp
            case "door_open":
                if (Session.Has("clock", "door_open_minutes")) break;
                _content.RecordDoorOpen();
                Sfx("door_unbar", 0);
                Music("main_theme", 4.0);
                _out.Add(StageOp("prop", ("id", "isolation_door"), ("state", "open")));
                break;
            case "enter": return EnterActor(A(0), A(1));
            case "redraw": _out.Add(StageOp("prop", ("id", "traces"), ("state", "refresh"))); break;
            case "su_approach": return WalkTo("su", new Ev { ["near_actor"] = "detective", ["offsets"] = new object?[] { new object?[] { 34.0, 6.0 } } });
            case "save": SaveChapterEnd(A(0) == "true"); break;
            case "ending": Ending(A(0)); break;
```

### AWK-024

**范围与类型：** `production` / `logic`。**索引说明：** 相遇阶段固定角色身份与对应会面锚点。

**标记位置：** [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:216](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:216)。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:217)，L217–L228。

```csharp
    private IEnumerable<Op> Meeting()
    {
        PauseDaily();
        Stage = _content.StageEvent("相遇");
        _storyLock = true;
        _out.Add(StageOp("cancel_click"));
        foreach (var (who, spot, facing) in new[] { ("huo", "Engineer", "right"), ("shen", "Chef", "back"), ("su", "Musician", "left") })
        {
            Claim(who);
            _out.Add(StageOp("place", ("who", who), ("to", new Ev { ["anchor"] = "meeting:" + spot })));
            _out.Add(StageOp("pose", ("who", who), ("anim", "idle"), ("facing", facing)));
            Unclaim(who);
```

### AWK-025

**范围与类型：** `production` / `logic`。**索引说明：** E11 手印与 E3 鞋底试验使用本章专属处理分支。

**标记位置：** [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:373](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:373)。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:374)，L374–L385。

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
```

### AWK-026

**范围与类型：** `production` / `schema`。**索引说明：** 把本章声路听位绑定到对应现场证物热点。

**标记位置：** [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:596](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:596)。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:597)，L597–L600。

```csharp
    private static readonly Dictionary<string, string> SoundSpotSite = new()
        { ["foyer"] = "E1", ["R08"] = "E6", ["hall"] = "E7", ["pantry"] = "E9", ["R05"] = "E12" };

    /// <summary>侦探能请铜口说的句子，只来自他已经知道的说法：石板上的指令、乐谱与门边的“开门”、霍铎想起的原话。</summary>
```

### AWK-027

**范围与类型：** `production` / `logic`。**索引说明：** 按已知石板、乐谱、霍铎回忆开放本章试验句。

**标记位置：** [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:601](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:601)。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:602)，L602–L613。

```csharp
    private bool SentenceKnown(int index) => index switch
    {
        0 => Has("examined", "E7"),
        1 => Has("examined", "E6") || Has("helped", "E5"),
        2 => Has("recalls", "huo"),
        _ => false,
    };

    private IEnumerable<Op> SoundTest()
    {
        var sentences = TestSentences.All.Select((s, i) => (s, i)).Where(x => SentenceKnown(x.i))
            .Select(x => (object?)new Ev { ["id"] = x.i.ToString(), ["label"] = $"“{x.s.Full}”" }).ToArray();
```

### AWK-028

**范围与类型：** `production` / `logic`。**索引说明：** E8 只开听位菜单且 E3 选对象前不结算；其余试验按证物编号调度。

**标记位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:76](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:76)。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:77)，L77–L88。

```csharp
    public void BeginExperiment(MorningWorld world, string id, string who)
    {
        // E8 only opens the placement menu; the actual sound run settles its own time.
        if (id != "E8" && !(id == "E3" && who == "")) ConsumeAction("试验", id);
        StartFlow(world, HasFlow("实验·" + id) ? "实验·" + id : "实验·默认", ("物", id), ("人", who));
    }
    public void BeginHelp(MorningWorld world, string id, string? who = null)
    {
        ConsumeAction("协助", id);
        // 请来辨认的人亲眼看了这件东西。
        if (Content.RootElement.GetProperty("EVIDENCE").GetProperty(id).TryGetProperty("help", out var helper))
            world.Examine(PersonOf(who ?? helper.GetString()!), id);
```

### AWK-029

**范围与类型：** `production` / `logic`。**索引说明：** 侦探与三名同伴的旧物回忆分别走固定角色流程。

**标记位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:122](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:122)。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:123)，L123–L134。

```csharp
    public void BeginRecall(MorningWorld world, string id)
    {
        var who = Content.RootElement.GetProperty("EVIDENCE").GetProperty(id).GetProperty("recall").GetString()!;
        var steps = new List<StoryStep<MorningWorld, Person>>();
        if (who == "detective")
        {
            steps.Add(new StoryStep<MorningWorld, Person>.Update(_ => Session.Remove("flags", "recall_yes")));
            steps.AddRange(Show("recall_ask_detective"));
            steps.Add(new StoryStep<MorningWorld, Person>.Branch(_ => Session.Has("flags", "recall_yes"),
                [new StoryStep<MorningWorld, Person>.Update(w => ApplyRecall(w, id, who)), .. Show("recall_detective")], []));
        }
        else
```

### AWK-030

**范围与类型：** `production` / `logic`。**索引说明：** 固定同伴顺序、调查痕迹阈值与离场低语触发条件属于本章流程。

**标记位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:226](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:226)。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:227)，L227–L238。

```csharp
    public void BeginRoomChanged(MorningWorld world, string room, long now)
    {
        var steps = new List<StoryStep<MorningWorld, Person>>();
        foreach (var who in new[] { "huo", "su", "shen" })
        {
            if (!Session.GetProgress("life").TryGetValue(who, out var value)) continue;
            var life = (Dictionary<string, object>)value;
            if (life.GetValueOrDefault("alone") is not true || life.GetValueOrDefault("noticed") is true || Session.CharacterRooms.GetValueOrDefault(who) != room) continue;
            steps.Add(new StoryStep<MorningWorld, Person>.Update(_ => life["noticed"] = true));
            var touched = life.GetValueOrDefault("choice") is "touch";
            var text = Chapter1Data.Text((touched ? "旁白·碰过·" : "旁白·没碰·") + who);
            steps.AddRange(Show("@aside:" + text));
```

### AWK-031

**范围与类型：** `production` / `logic`。**索引说明：** E1/E3 开放重演、指定熟悉旁证揭示 V6 的触发映射。

**标记位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:280](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:280)。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:281)，L281–L292。

```csharp
    public string[] Examine(MorningWorld world, string id)
    {
        if (Session.Has("examined", id)) return [];
        ConsumeAction("检视", id);
        Session.Set("examined", id, true);
        Session.Observe("detective", id);
        world.Examine(Person.Detective, id);
        ShareExamination(world, id);
        var events = new List<string> { "evidence" };
        if (id is "E1" or "E3" && !(bool)Session.GetProgress("state")["replay_open"]) events.Add("unlock");
        if (ContentList("ACQUAINTANCE_EVIDENCE", id) && RevealV6()) events.Add("v6");
        return events.ToArray();
```

### AWK-032

**范围与类型：** `production` / `schema`。**索引说明：** E12 展示 E7 石板小图是本案证物间固定关联。

**标记位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:312](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:312)。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:313)，L313–L317。

```csharp
    public string CardFacts(string id)
    {
        var facts = Content.RootElement.GetProperty("EVIDENCE").GetProperty(id).GetProperty("facts").GetString()!;
        return id == "E12" && Session.Has("examined", "E7") ? facts + "\n" + Chapter1Data.Text("卡片·E12·石板小图") : facts;
    }
```

### AWK-033

**范围与类型：** `production` / `logic`。**索引说明：** E11 四人手印进度及 E8 声路试验笔记为本案专属摘要逻辑。

**标记位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:318](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:318)。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:319)，L319–L330。

```csharp
    public string[] CardNotes(MorningWorld world, string id)
    {
        var notes = new List<string>();
        var evidence = Content.RootElement.GetProperty("EVIDENCE").GetProperty(id);
        var helps = Content.RootElement.GetProperty("HELP_NOTES");
        string Name(string who) => Content.RootElement.GetProperty("PERSON_NAMES").GetProperty(who).GetString()!;
        if (id == "E11")
        {
            var people = new[] { "detective", "huo", "shen", "su" };
            var compared = people.Where(person => Session.Has("hand_comparisons", person)).Select(Name).ToArray();
            var missing = people.Where(person => !Session.Has("hand_comparisons", person)).Select(Name).ToArray();
            // 只记已经比过的手；还没想到要比的人不列出来，结论等四只手都比过才下。
```

### AWK-035

**范围与类型：** `production` / `logic`。**索引说明：** E11 手印卡有先比自己、再比在场同伴的本章专属门槛。

**标记位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Interaction.cs:37](../../godot_csharp/scenes/Awakening/Script/Chapter1Interaction.cs:37)。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Interaction.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1Interaction.cs:38)，L38–L49。

```csharp
        if (id == "E11")
        {
            foreach (var who in new[] { "detective", "huo", "shen", "su" })
            {
                // 先比自己的手；知道“不是我的”之后，才会想到借在场同伴的手来比。不在场的人不列。
                if (who != "detective" && (!content.Session.Has("hand_comparisons", "detective") || !_geometricPeople.Contains(who))) continue;
                var compared = content.Session.Has("hand_comparisons", who);
                var busy = content.Session.GetProgress("life").TryGetValue(who, out var life) &&
                    ((Dictionary<string, object>)life).GetValueOrDefault("state") is "going" or "holding";
                var local = content.Session.CharacterRooms.GetValueOrDefault("detective") == "R05" && content.Session.IsLocalTarget("detective", who);
                Add("compare", who == "detective" ? Text("卡片·比对自己") : Text("卡片·比对他人", ("人名", Name(who))),
                    !compared && !busy && local && _geometricPeople.Contains(who),
```

### CH2-033

**范围与类型：** `production` / `logic`。**索引说明：** 调查台账在案发日后解锁本案重演的预写逻辑。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:331](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:331)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:332)，L332–L343。

```csharp
        if (thing=="台账" && _game.Day>=Game.CaseDay && !_replayOpen) { _replayOpen=true; _continuations.Enqueue(o=>Activate("card.replay_unlock",o)); }
    }
    // 粮仓的两个交互点各看一面：门闩只说门闩，存粮只说存量。
    private static string GranaryAspect(string site,string facts)
    {
        var parts=facts.Split('；');
        if(parts.Length!=2||!parts[0].StartsWith("存粮"))return facts;
        return site switch { "stock"=>parts[0]+"。","latch"=>parts[1],_=>facts };
    }
    private Dictionary<string,string> SiteSpec(Chapter2AuthoredSite site)
    {
        var thing="";
```

### CH2-078

**范围与类型：** `production` / `text`。**索引说明：** 按本章时段、地点与案件状态提供场景动作选项。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs:11](../../godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs:11)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs](../../godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs:12)，L12–L23。

```csharp
    public static RoomView For(Game g, Place? place)
    {
        if (place is not { } here) return new(null, [], [], false, []);
        var actions = new List<RoomAction>();
        var board = g.Board.ContainsKey(here);
        var things = g.RoomThings(here).ToArray();
        var daytime = g.Slot != Slot.Night;
        var confrontation = g.Day == Game.LastDay && g.Slot == Slot.Morning;
        if (daytime && !confrontation)
        {
            foreach (var thing in things) actions.Add(new("细看：" + thing, "查 " + thing, "inspect"));
            if (board) actions.Add(new("在这里贴一条规矩……", "贴 " + Names.Of(here), "post"));
```

### CH2-079

**范围与类型：** `production` / `text`。**索引说明：** 定义本章当面交互可执行的具体人物行动。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs:54](../../godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs:54)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs](../../godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs:55)，L55–L66。

```csharp
    public static RoomAction[] PersonActions(Game g, P who, Place? place)
    {
        if (place is not { } here || !g.RoomPeople(here).Any(p => p.Id == who)) return [];
        var name = Names.Of(who);
        var actions = new List<RoomAction> { new("看你对" + name + "的笔记", "笔记 " + name, "notes") };
        if (g.Slot == Slot.Night || g.Day == Game.LastDay && g.Slot == Slot.Morning) return actions.ToArray();
        actions.InsertRange(0, [
            new("问" + name + "知道什么", "问 " + name),
            new("请" + name + "做一件事……", "请 " + name, "ask"),
            new("请" + name + "同去看……", "同去 " + name, "escort"),
            new("对" + name + "讲一条规矩……", "讲 " + name, "tellrule"),
            new("把一条消息转告" + name + "……", "转告 " + name, "news"),
```

## S08

**对应建议：** 结构化事件替代中文日志反解析。10 处源码、0 处资源。

### CH2-007

**范围与类型：** `production` / `logic`。**索引说明：** 对“门闩松了”证词执行本案专属解释。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Social.cs:260](../../godot_csharp/scenes/Chapter2/Prototype/Social.cs:260)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Social.cs](../../godot_csharp/scenes/Chapter2/Prototype/Social.cs:261)，L261–L272。

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
    }

    // ------------------------------------------------------------ factions --
```

### CH2-028

**范围与类型：** `production` / `logic`。**索引说明：** 事件追踪按本章人物与案件谓词生成特定因果链。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Emergence.cs:84](../../godot_csharp/scenes/Chapter2/Prototype/Emergence.cs:84)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Emergence.cs](../../godot_csharp/scenes/Chapter2/Prototype/Emergence.cs:85)，L85–L96。

```csharp
    private bool HandleTalk(Person p, Job? job, ActionExecution exec)
    {
        if (job is not (Job.Relay or Job.Teach or Job.Avoid or Job.Follow) || p == Det) return false;
        var say = exec.Intent.Changes is Effect.Sequence seq ? seq.Items.OfType<Effect.Say>().FirstOrDefault() : null;
        var hearer = say?.Recipient is Noun.Reference r ? PersonOf(p, r.Projection) : null;
        if (job == Job.Relay && say is not null && hearer is not null && RelayText.TryGetValue(say.Text, out var spec))
        {
            if (hearer == Det) return true;
            var v = Tell(hearer, spec, Channel.Relay, p);
            if (Where(Det) == Where(p) && Slot != Slot.Night) DetHeard.Add(spec);
            RelayLog.Add((Day, Slot, p.Id, hearer.Id, spec.Id, v == Truth.True));
            var from = _transmit.TryGetValue((spec.Id, p.Id), out var tf) ? tf : _postedEv.GetValueOrDefault(spec.Id);
```

### CH2-031

**范围与类型：** `production` / `text`。**索引说明：** 不同具体角色对案发夜的预写口供反馈直接写在代码中。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Player.cs:464](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:464)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Player.cs](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:465)，L465–L476。

```csharp
    private (string Text, string Emotion) TestimonyPresentation(Person p, bool selectedTestimony = false)
    {
        if (Day < CaseDay) return ChatPresentation(p);
        var prefix = $"D{CaseNight} {p.Name} ";
        var did = NightTruth.Where(t => t.StartsWith(prefix, StringComparison.Ordinal)).Select(t => t[prefix.Length..]).ToArray();
        bool Did(params Job[] jobs) => did.Any(d => jobs.Any(j => d.StartsWith(Names.Of(j), StringComparison.Ordinal)));
        var open = selectedTestimony || TrustIn(p, Det) >= 2;
        var latch = Did(Job.RepairLatch) ? "门闩是我修的。" : "";
        switch (p.Id)
        {
            case P.Bertrand:
                return ("“报告：" + string.Join("；", Sightings.Where(s => s.Witness == P.Bertrand && (s.Day == Day - 1 || s.Day == CaseNight)).GroupBy(s => (s.Day, s.At))
```

### CH2-038

**范围与类型：** `production` / `logic`。**索引说明：** 将本章特定人物行动映射为案件因果事件。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Emergence.cs:138](../../godot_csharp/scenes/Chapter2/Prototype/Emergence.cs:138)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Emergence.cs](../../godot_csharp/scenes/Chapter2/Prototype/Emergence.cs:139)，L139–L150。

```csharp
    private void RecordAction(Person p, Job? job, ActionExecution exec)
    {
        if (p == Det) return;
        var basis = exec.Intent.Basis;
        var changes = exec.Intent.Changes;
        var request = basis is { Kind: ProjectionKind.Event, Procedure: not null } ? basis : null;
        string text;
        var frame = job is { } jj ? Names.Frame(jj) : "交谈";
        Person? sayTo = null;
        if (changes is Effect.Sequence { Items: [Effect.Act { Operation: ActionOp.Travel }, Effect.Say say0, ..] } || changes is Effect.Sequence { Items: [Effect.Say, ..] })
        {
            var say = ((Effect.Sequence)changes).Items.OfType<Effect.Say>().First();
```

### CH2-039

**范围与类型：** `production` / `logic`。**索引说明：** 仅对本章具体人物与行动组合开放可读线索。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Emergence.cs:289](../../godot_csharp/scenes/Chapter2/Prototype/Emergence.cs:289)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Emergence.cs](../../godot_csharp/scenes/Chapter2/Prototype/Emergence.cs:290)，L290–L301。

```csharp
    public string? Readable(ActEvent e)
    {
        if (e.Who == P.Det) return "自己做的";
        if (MorningHeard.Contains(e.Id)) return "清晨闲谈";
        if (e.SeenByDet) return e.Slot == Slot.Night ? "守夜" : "在场";
        if (e.Slot == Slot.Night && e.Job is { } j)
        {
            var known = KnownTraces(e.Day);
            var key = j switch
            {
                Job.NightTakeSick or Job.NightTakeSickRec or Job.NightTakeSelf or Job.NightTakeSelfRec or Job.Hoard => known.ContainsKey("台账") ? "台账" : "门闩",
                Job.RepairLatch => "门闩", Job.Tidy => "饼屑:" + Names.Of(Cast.Of(e.Who).Dorm), Job.VisitArchive or Job.Seize => "前厅脚印",
```

### CH2-045

**范围与类型：** `production` / `logic`。**索引说明：** 按具名人物、地点和本案时间安排议事前的控制与封锁。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Council.cs:54](../../godot_csharp/scenes/Chapter2/Prototype/Council.cs:54)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Council.cs](../../godot_csharp/scenes/Chapter2/Prototype/Council.cs:55)，L55–L66。

```csharp
    private void Seizure()
    {
        if (SeizedBy is not null) return;
        // 答应了守门人“请回”的人，这一夜就不算在开门的人里；他那一站要由别人顶上才算数。
        var yielded = Events.Where(e => e.Day == Day && e.Slot == Slot.Night && e.Text.StartsWith("答应了") && (e.Text.Contains("请回") || e.Text.Contains("让开"))).Select(e => e.Who).ToHashSet();
        var crew = _seizeOk.Where(p => !yielded.Contains(p.Id)).ToArray();
        var tried = Events.Where(e => e.Day == Day && e.Slot == Slot.Night && (e.Job == Job.Seize || e.Job == Job.AskLeave)).Select(e => e.Who).Distinct().ToArray();
        if (crew.Length == 0 && tried.Length == 0) return;
        var leader = (crew.Length > 0 ? crew : tried.Select(t => this[t]).ToArray()).OrderByDescending(p => p.Hooks + p.Desire).ThenBy(p => p.Id).First();
        var causes = Events.Where(e => e.Day == Day && e.Slot == Slot.Night && (e.Job is Job.Seize or Job.AskLeave || e.Text.StartsWith("答应了") && e.Text.Contains("让开"))).Select(e => e.Id).ToArray();
        // 三站都有人使过劲、又有人开得了前厅的锁，门就开了；有一站始终被守门的人占着，就开不了（§13 第 4 条：没有人数阈值）。
        var worked = new[] { WeightRoom, LockCore, RelayPost }.All(st => Get(st, T.Worked) > 0 && crew.Any(c => StationFor(c) == st));
```

### CH2-050

**范围与类型：** `production` / `schema`。**索引说明：** 将本案特定行动地点编码为重演档案中的位置名称。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Morning.cs:414](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:414)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Morning.cs](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:415)，L415–L426。

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
```

### CH2-051

**范围与类型：** `production` / `logic`。**索引说明：** 用本案预设体型与可见轨迹筛选嫌疑人。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Morning.cs:483](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:483)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Morning.cs](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:484)，L484–L495。

```csharp
    public HashSet<P> ReplayCandidates(int night)
    {
        var known = KnownTraces(night);
        var builds = new HashSet<Build>();
        foreach (var place in Enum.GetValues<Place>()) builds.UnionWith(BuildsSeenAt(night, place));
        foreach (var t in known.Values) foreach (Build b in Enum.GetValues<Build>()) if (t.Text.Contains(BuildText(b).TrimEnd('的'))) builds.Add(b);
        var set = Npcs.Where(p => builds.Contains(p.Pro.Build) && (Belief(Det, p, T.Sick) ?? 0) == 0).Select(p => p.Id).ToHashSet();
        foreach (var t in known.Values) foreach (var p in Npcs) if (t.Text.Contains(p.Name)) set.Add(p.Id);
        if (DetSketchSeen.Contains(night)) foreach (var s in Sightings.Where(s => s.Day == night && s.Witness == P.Moran)) set.Add(s.Truth);
        return set;
    }

```

### CH2-052

**范围与类型：** `production` / `logic`。**索引说明：** 将案夜特定证词和现场记录折算为可见体型。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Morning.cs:497](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:497)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Morning.cs](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:498)，L498–L509。

```csharp
    public HashSet<Build> BuildsSeenAt(int night, Place place)
    {
        var set = new HashSet<Build>();
        if (Stakeouts.Contains((night, place)))
            foreach (var m in Moves.Where(m => m.Day == night && (m.To == place || m.From == place) && m.Who != P.Det)) set.Add(this[m.Who].Pro.Build);
        foreach (var s in Sightings.Where(s => s.Day == night && s.At == place && (s.Witness == P.Det || AskedOn.Contains((night + 1, s.Witness)) || AskedOn.Contains((night + 2, s.Witness))))) set.Add(s.Build);
        if (place == Place.ArchiveFront && KnownTraces(night).ContainsKey("前厅脚印"))
            foreach (var m in Moves.Where(m => m.Day == night && m.To == place && m.Who != P.Det)) set.Add(this[m.Who].Pro.Build);
        return set;
    }

    // ------------------------------------------------- 规矩的称呼与传播 --
```

### CH2-082

**范围与类型：** `production` / `logic`。**索引说明：** 过滤本案清晨跟踪的特定无变文字。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:621](../../godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:621)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs](../../godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:622)，L622–L633。

```csharp
    private static bool Changed(TraceItem t) => !(t.Text is "三处机关没人碰过" or "前厅积灰上没有新脚印" or "病床边没有新送来的东西" or "贝尔特朗：夜巡无异常" or "门闩完好")
        && !t.Text.Contains("没有新东西") && !(t.Key == "台账" && t.Text.StartsWith("账面与存量差 0 份；夜间记账一栏是空的"));

    /// <summary>重演（§12 第 6 条）：变量由“你知道的规矩 × 你怀疑认了它的人”和“某人那一夜在不在某处”组合而成。</summary>
    private void Replay(string[] v)
    {
        LastReplay = null;
        if (g.Day < Game.CaseDay) { W("还没有可以重演的夜。"); return; }
        var night = g.Day - 1;
        if (v.Length > 0 && v[0].StartsWith('第') && v[0].EndsWith('夜')) { night = Night(v[0]); v = v[1..]; }
        if (night < 2 || night >= g.Day) throw new PlayerError($"能重演的是第 2—{g.Day - 1} 夜。");
        var menu = g.ReplayMenu(night);
```

## S09

**对应建议：** 通用 AST 执行规矩与社会请求。5 处源码、0 处资源。

### CH2-006

**范围与类型：** `production` / `schema`。**索引说明：** 将本项目特定规则卡编号和行为映射为具体游戏后果。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Rules.cs:153](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:153)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Rules.cs](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:154)，L154–L165。

```csharp
    private IEnumerable<Rule> CompileCard(Fx fx, Card card)
    {
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

### CH2-010

**范围与类型：** `production` / `schema`。**索引说明：** 允许的行为编号清单是本项目规则语义的硬编码 schema。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2DesignRules.cs:119](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2DesignRules.cs:119)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2DesignRules.cs](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2DesignRules.cs:120)，L120–L131。

```csharp
    private static readonly string[] BehaviorIds = ["取粮", "按人头", "交钥匙", "指控须证", "转述", "开库各取", "开库委员会", "开库封闭", "开库约束", "限制入院", "温室"];

    private sealed class Data
    {
        public string[] Subjects = [];
        public CardOption[] Conds = [], Behaviors = [], Exceptions = [], Accounts = [];
        public (Card, string)[] CaseWordings = [], ArchiveWordings = [];
    }

    private static RF Flags(JsonElement list)
    {
        var f = RF.None;
```

### CH2-011

**范围与类型：** `production` / `logic`。**索引说明：** 本文件按本章具体社会行动实现角色互动及预写反馈。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2SocialActions.cs:3](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2SocialActions.cs:3)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2SocialActions.cs](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2SocialActions.cs:4)，L4–L15。

```csharp
namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed partial class Game
{
    private PlayerAction? _preparedAction;
    private Truth? _preparedRuleVerdict;

    private RF ActionFlags(PlayerAction action) => action.Cmd switch
    {
        Cmd.TellRule => action.Spec?.Flags ?? action.Card?.Flags(this) ?? RF.None,
        Cmd.ShareFood => RF.SickPriority | RF.Rations,
        Cmd.Escort => RF.EvidenceFirst,
```

### CH2-055

**范围与类型：** `production` / `logic`。**索引说明：** 处理玩家介入后本案指定人物请求及证据相关分支。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Player.cs:69](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:69)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Player.cs](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:70)，L70–L81。

```csharp
    private void PlayerBegin(PlayerAction? act)
    {
        LastAction = act;
        if (ReferenceEquals(_preparedAction, act) && act is not null) { _preparedAction = null; DetHelpers.Clear(); return; }
        _asked.Clear();
        DetHelpers.Clear();
        if (Slot == Slot.Afternoon && Get(Det.Body, T.Hungry) > 0 && act is not null)
        {
            Feed.Add("你饿得发虚，这个下午什么也做不了。");
            LastAction = null;
            return;
        }
```

### CH2-056

**范围与类型：** `production` / `logic`。**索引说明：** 结算本章玩家行动、证据反馈与案情特定揭示。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Player.cs:195](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:195)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Player.cs](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:196)，L196–L207。

```csharp
    private void PlayerEnd(PlayerAction? act)
    {
        // 违约检查
        for (var i = 0; i < Promises.Count; i++)
        {
            var (who, due, done) = Promises[i];
            if (!done && Day > due || !done && Day == due && Slot == Slot.Night)
            {
                Promises[i] = (who, due, true);
                Witness(Det, K("食言", P.Det, who), -2, 0, [this[who]]);
                Feed.Add($"你答应{Names.Of(who)}的事，没有做到。");
            }
```

## S10

**对应建议：** 区分历史、制度钟点与人物当前行动。6 处源码、0 处资源。

### AWK-010

**范围与类型：** `production` / `logic`。**索引说明：** 固定警铃、两次声音、烟到门与侦探醒来的事件阶段构成本案昨夜时序。

**标记位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs:449](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:449)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:450)，L450–L461。

```csharp
    private ReplayResult Execute()
    {
        Build();
        var ctb = _session.CTB;
        ctb.Schedule(0, () =>
        {
            foreach (var a in _agents) KnowConcept(a, a.Alarm, ObservationSource.Perception);
            foreach (var a in _agents.Where(a => a.Who != Person.Detective)) a.Awake = true;
            _agents[0].Thought = Chapter1Data.Text("重演·心声·wake");
            foreach (var a in _agents.Where(a => a.Who != Person.Detective)) a.Thought = PlainThought(a);
            Frame(new("alarm"));
            QueueDecisions();
```

### CH2-027

**范围与类型：** `production` / `text`。**索引说明：** 具体课次、日期、标题和教师讲授文字写入代码。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Demon.cs:35](../../godot_csharp/scenes/Chapter2/Prototype/Demon.cs:35)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Demon.cs](../../godot_csharp/scenes/Chapter2/Prototype/Demon.cs:36)，L36–L47。

```csharp
    public static readonly ImmutableDictionary<int, (int Day, string Title, string Teach)> Lessons = new Dictionary<int, (int, string, string)>
    {
        [11] = (FirstDay, X("授课.第十一课.题名"), X("授课.第十一课.内容")),
        [12] = (FirstDay + 1, X("授课.第十二课.题名"), X("授课.第十二课.内容")),
        [13] = (0, X("授课.第十三课.题名"), X("授课.第十三课.内容")),
        [14] = (FirstDay + 2, X("授课.第十四课.题名"), X("授课.第十四课.内容")),
    }.ToImmutableDictionary();

    public static int LessonOfDay(int day) => Lessons.FirstOrDefault(l => l.Value.Day == day).Key;

    /// <summary>上课：在场者获得本课（完整），认知中写入课上所学；恶魔显形板书，留下粉笔字与窄底鞋印；对到场者各说一句放大欲望的真话。</summary>
    public void TeachClass()
```

### CH2-043

**范围与类型：** `production` / `text`。**索引说明：** 逐日晨间播报包含本章特定课程与事件。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Demon.cs:100](../../godot_csharp/scenes/Chapter2/Prototype/Demon.cs:100)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Demon.cs](../../godot_csharp/scenes/Chapter2/Prototype/Demon.cs:101)，L101–L112。

```csharp
    public void MorningBroadcast()
    {
        if (Day == FirstDay)
        {
                // 集合场面里铜口当面说了这几句，广播不再给侦探重复一遍（人人照样听到）。
                Broadcast("诸位，请到讲堂来。现在讲守则的读法。", shown: false);
                Broadcast("另：入夜后，名录库中若有人以你之名唤你，勿应。", shown: false);
                foreach (var p in People.Where(p => p != Det)) Tell(p, Warning1, Channel.Public);
                Det.Acc[Warning1] = Truth.True;
                // 它只对不戒备它的人低语（信它的人它多说，戒备它的人它不白费口舌）；低语只有本人听见，旁人看不出谁听到过什么（总纲 恶魔“不自曝”）。
                foreach (var p in People.Where(p => p == Det || p.Pro.DemonPrejudice >= 0 || p.Hooks > 0)) Whisper(p);
        }
```

### CH2-061

**范围与类型：** `production` / `logic`。**索引说明：** 按本章日序初始化病情、案件发现与线索状态。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:108](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:108)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:109)，L109–L120。

```csharp
    private void BeginDay()
    {
        _dayStart[Day] = Now;
        foreach (var (who, memory) in _failures)
            Observation.Observe(who.Soul, new ObservationInput(memory.Name, memory.Kind)
            { Target = memory, Source = ObservationSource.Introspection, Existence = Truth.False, Time = Now });
        _failures.Clear();
        var hungry = 0;
        foreach (var p in People)
        {
            var fed = Get(p.Body, T.Fed) > 0 || Day == FirstDay;
            p.HungerStreak = fed ? 0 : p.HungerStreak + 1;
```

### CH2-067

**范围与类型：** `production` / `logic`。**索引说明：** 执行本案预置的按日/时段线索及人物状态变更。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:730](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:730)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:731)，L731–L742。

```csharp
    private void FixedBefore()
    {
        // 始业日：门开后的集合与第一顿晚饭；案发那一日：下午莫兰发烧、傍晚沃德当众盯视雀儿（压力，见第三部分 §7、§16）。
        if (Day == RationFirstDay && Slot == Slot.Forenoon)
        {
                Feed.Add(X("节点.集合"));
                LedgerBaseline = Get(Granary, T.Ration);
        }
        else if (Day == RationFirstDay && Slot == Slot.Noon)
        {
                if (!Mediated) Feed.Add(X("节点.配给僵住"));
        }
```

### CH2-069

**范围与类型：** `production` / `logic`。**索引说明：** 在本案案发后按固定条件触发点名事件。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:985](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:985)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:986)，L986–L997。

```csharp
    private void NameCall()
    {
        if (_nameCalledNight == Day) return;
        var present = People.Where(p => !p.Asleep && Where(p) == Place.ArchiveFront && p != Det
            && !p.World.Projections.Any(x => x.Kind == ProjectionKind.Event && x.Name == "有人以你之名唤你")
            && (p.Hooks > 0 || p.Whispered > 0 || Events.Any(e => e.Who == p.Id && e.Job == Job.VisitArchive && e.Day < Day)))
            .OrderByDescending(p => p.Hooks * 2 + p.Desire).ThenBy(p => p.Id).Take(1).ToArray();
        if (present.Length > 0) _nameCalledNight = Day;
        foreach (var p in present)
        {
            var voice = p.M[NameVoice];
            Observation.Observe(p.Soul, new ObservationInput(voice.Name, voice.Kind)
```

## S11

**对应建议：** 数据驱动案件、重演和证据判定。15 处源码、0 处资源。

### AWK-003

**范围与类型：** `production` / `schema`。**索引说明：** 将本章 V1–V6、证物字段及第六问默认认知装配成案件假设。

**标记位置：** [godot_csharp/scenes/Awakening/Night/AwakeningCase.cs:20](../../godot_csharp/scenes/Awakening/Night/AwakeningCase.cs:20)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/AwakeningCase.cs](../../godot_csharp/scenes/Awakening/Night/AwakeningCase.cs:21)，L21–L32。

```csharp
    private static (CaseModel, ImmutableArray<Prediction>) Build()
    {
        var root = Chapter1Data.Root;
        var fields = root.GetProperty("DETECTIVE_TRACE_FIELDS").EnumerateObject().Select(item =>
        {
            var evidence = item.Value.GetProperty("evidence").GetString()!;
            var card = root.GetProperty("EVIDENCE").GetProperty(evidence);
            var projection = card.TryGetProperty("mind_name", out var alias) ? alias.GetString()! : card.GetProperty("name").GetString()!;
            return (Field: new CaseField(projection, new Tag(item.Value.GetProperty("tag").GetString()!), item.Value.GetProperty("field").GetString()!),
                Expected: item.Value.GetProperty("equals").GetInt32());
        }).ToArray();
        var predictions = NightVariables.All().Select(variables =>
```

### AWK-004

**范围与类型：** `production` / `schema`。**索引说明：** 字段枚举本案固定证物事实及其编码取值。

**标记位置：** [godot_csharp/scenes/Awakening/Night/MorningTraces.cs:13](../../godot_csharp/scenes/Awakening/Night/MorningTraces.cs:13)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/MorningTraces.cs](../../godot_csharp/scenes/Awakening/Night/MorningTraces.cs:14)，L14–L25。

```csharp
public sealed record MorningTraces(
    bool InnerBoltDown,
    bool AshFan,
    bool WedgeTopSooted,
    bool WedgeInCorner,
    ImmutableArray<BootPrint> Prints,
    int Cloth,            // 0 无；1 沿闭合的门底压成直线；2 塞在敞开的门缝里
    bool ClothOverHeel,
    bool ForkByFrame,
    bool FrameDent,
    int ScoreLine1,       // 0 无；1 两拍弱音+“开门”记两遍、旁注“门外？”；2 记“救我”；3 记整句“开门，救我”
    bool ScoreLine2,      // “他们关了门。”
```

### AWK-005

**范围与类型：** `production` / `logic`。**索引说明：** E1–E9 的逐件痕迹比较与字段解释是本案专属映射。

**标记位置：** [godot_csharp/scenes/Awakening/Night/MorningTraces.cs:95](../../godot_csharp/scenes/Awakening/Night/MorningTraces.cs:95)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/MorningTraces.cs](../../godot_csharp/scenes/Awakening/Night/MorningTraces.cs:96)，L96–L107。

```csharp
    private static List<string> Differences(string id, MorningTraces p, MorningTraces a)
    {
        var d = new List<string>();
        string Case(int value, params int[] known) => known.Contains(value) ? value.ToString() : "其他";
        switch (id)
        {
            case "E1":
                if (p.InnerBoltDown != a.InnerBoltDown) d.Add(T(p.InnerBoltDown ? "对照·E1·重演有闩" : "对照·E1·重演无闩"));
                break;
            case "E2":
                if (p.AshFan != a.AshFan) d.Add(T(p.AshFan ? "对照·E2·重演有灰" : "对照·E2·重演无灰"));
                else if (p.WedgeTopSooted != a.WedgeTopSooted) d.Add(T(p.WedgeTopSooted ? "对照·E2·重演楔沾灰" : "对照·E2·重演楔干净"));
```

### AWK-037

**范围与类型：** `production` / `schema`。**索引说明：** 手稿固定将第六问作为末项并按认知解锁隐藏显示。

**标记位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Session.cs:576](../../godot_csharp/scenes/Awakening/Script/Chapter1Session.cs:576)。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Session.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1Session.cs:577)，L577–L588。

```csharp
    private Ev ReplaySnapshot()
    {
        var variables = Content.GetProperty("VARIABLES").EnumerateArray().ToArray();
        var count = _content.V6Open ? variables.Length : variables.Length - 1;
        var marks = Session.GetProgress("recall_marks");
        var questions = variables.Take(count).Select((variable, index) => (object?)new Ev
        {
            ["question"] = variable.GetProperty("question").GetString(),
            ["options"] = variable.GetProperty("options").EnumerateArray().Select(o => (object?)o.GetString()).ToArray(),
            ["mark"] = marks.TryGetValue((index + 1).ToString(), out var who)
                ? "　[color=seal]" + T("重演·回忆所证", ("人名", PersonName((string)who))) + "[/color]" : "",
            ["fresh"] = index == 5 && !Has("flags", "v6_seen"),
```

### AWK-038

**范围与类型：** `production` / `schema`。**索引说明：** 装配本章 V1–V6 假设、证物、陈述步与搜索模型。

**标记位置：** [godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs:63](../../godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs:63)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs](../../godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs:64)，L64–L75。

```csharp
    public DetectivePlanner(JsonElement content)
    {
        _search = content.GetProperty("DETECTIVE_SEARCH").Clone();
        _random = new((int)N("seed"));
        foreach (var item in content.GetProperty("DETECTIVE_ACTIONS").EnumerateObject())
        {
            var a = item.Value;
            _actions.Add(new(item.Name, S(a,"kind"), S(a,"target"), S(a,"room"), S(a,"label"),
                a.GetProperty("minutes").GetDouble(), a.GetProperty("weight").GetDouble(),
                S(a,"requires_group"), S(a,"requires_key"), S(a,"completion_group"), S(a,"completion_key"), a.GetProperty("repeatable").GetBoolean()));
        }
        foreach (var item in content.GetProperty("DETECTIVE_TRACE_FIELDS").EnumerateObject())
```

### AWK-039

**范围与类型：** `production` / `schema`。**索引说明：** 答辩固定为本章四步，并允许 me 作为自闩陈述。

**标记位置：** [godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs:177](../../godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs:177)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs](../../godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs:178)，L178–L189。

```csharp
            && _arguments.Count == 4 && _arguments.Select(a => a.Step).Order().SequenceEqual(Enumerable.Range(1,4))
            && _arguments.All(a => a.Evidence.Any(e => content.Session.GetProgress("examined").GetValueOrDefault(e) is true)
                && (a.RequiredGroup.Length == 0 || content.Session.GetProgress(a.RequiredGroup).GetValueOrDefault(a.RequiredKey) is true)
                && (a.Variable != -1 || _detectiveBolted));
    }
    public (string Said,string Shown)? Argument(int step, AwakeningContentSession content, NightVariables prediction)
    {
        if (!_canAnswer) return null;
        var argument = _arguments.FirstOrDefault(a => a.Step == step);
        if (argument is null) return null;
        var evidence = argument.Evidence.Where(e => content.Session.GetProgress("examined").GetValueOrDefault(e) is true).ToArray();
        if (evidence.Length == 0) return null;
```

### AWK-061

**范围与类型：** `production` / `logic`。**索引说明：** 本章实播重演将固定六项变量、吻合判断和角色见证写入玩家进度。

**标记位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Replay.cs:8](../../godot_csharp/scenes/Awakening/Script/Chapter1Replay.cs:8)。

**原文位置：** [godot_csharp/scenes/Awakening/Script/Chapter1Replay.cs](../../godot_csharp/scenes/Awakening/Script/Chapter1Replay.cs:9)，L9–L20。

```csharp
    public ImmutableArray<Finding> RunVisibleReplay(MorningWorld world, NightVariables variables)
    {
        RecordReplayRun();
        var result = NightReplay.Run(variables);
        var compared = Comparison.Compare(result.Traces, MorningTraces.Evidence, id => Session.Has("examined", id));
        // 没查过的现场不算吻合：推演再对，也只是猜中。
        var checkedMatch = result.FullMatch && compared.All(f => f.Status == FindingStatus.Match);
        WitnessReplay(world, variables, checkedMatch);
        var values = new object[] { (int)variables.V1, (int)variables.V2, (int)variables.V3,
            (int)variables.V4, (int)variables.V5, (int)variables.V6 };
        var state = Session.GetProgress("state");
        state["replay_vars"] = values;
```

### CH2-003

**范围与类型：** `production` / `logic`。**索引说明：** 案发痕迹、人物知识变量与反事实重演映射为特定案件事实。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Case.cs:61](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:61)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Case.cs](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:62)，L62–L73。

```csharp
    public CaseTraces Dawn()
    {
        if (!(Day == CaseDay && Slot == Slot.Morning)) throw new InvalidOperationException("只在案发次日清晨取痕迹");
        if (!_dawnDone) { BeginDay(); _dawnDone = true; }
        return CaseActual ?? CaptureTraces();
    }
    private bool _dawnDone;

    /// <summary>
    /// 信念重演：新建一个独立世界，从第 2 日晨起按同一套规则推演到第 3 日清晨。变量只改认知与处境
    /// （谁读过并接受了哪条规则、谁知道谁病了、谁吃过晚饭），行动全部由各人的 DecisionSystem 推出。
    /// </summary>
```

### CH2-025

**范围与类型：** `production` / `logic`。**索引说明：** 对质议程固定为本案的取用、门闩、台账及特定人物结局。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:410](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:410)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:411)，L411–L422。

```csharp
    private void BeginConfront(List<Dictionary<string,object?>> output)
    {
        _stage="confront"; _pairs.Clear(); _answered.Clear(); _agendaDone.Clear(); _skippedTopics.Clear(); Scene(Place.Hall,output);
        _continuations.Enqueue(o=>Activate("confront.begin",o)); _continuations.Enqueue(ConfrontStep); Pump(output);
    }
    private void ConfrontStep(List<Dictionary<string,object?>> output)
    {
        // 议程由现实里为真的主张决定（多日交互审核 M1）：先问取用（有几份问几遍，第二遍起问“其余的呢？”），再问门闩，最后问台账。
        string? topic=null;HashSet<int> expected=new();
        foreach(var t in new[]{"take","latch","ledger"})
        {
            if(_skippedTopics.Contains(t)) continue;
```

### CH2-030

**范围与类型：** `production` / `logic`。**索引说明：** 清晨生成台账、门闩、病碗、废纸篓与目击等具体案发痕迹。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Morning.cs:144](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:144)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Morning.cs](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:145)，L145–L156。

```csharp
    private List<TraceItem> TakeTraces(int night)
    {
        var t = new List<TraceItem>();
        var shortBy = Math.Max(0, ExpectedRations() - Get(Granary, T.Ration));
        var names = NightLedgerNames.Where(n => n.StartsWith($"D{night} ")).Select(n => n[(n.IndexOf(' ') + 1)..]).ToArray();
        t.Add(new("台账", Place.Logistics, $"账面与存量差 {shortBy} 份{(names.Length > 0 ? "；夜间记账一栏有" + string.Join("、", names) + "的签名" : "；夜间记账一栏是空的")}"));
        var latch = Get(Granary, T.LatchLoose) > 0 ? "门闩松动" : Get(Granary, T.ToolMarks) > 0 ? "门闩紧固，有工具痕" : "门闩完好";
        var glance = new List<string>();
        if (_pickedGranary > 0) { latch += "；锁眼边有极细的新划痕"; }
        if (_failedAtGranary > 0) { var push = $"仓门上有{(_failedAtGranary > 3 ? "许多" : _failedAtGranary > 1 ? "几处" : "一处")}新的推痕（有人没打开）"; latch += "；" + push; glance.Add(push); }
        t.Add(new("门闩", Place.Logistics, latch, glance.Count > 0 ? string.Join("；", glance) : null));
        var bowl = Get(Bowl, T.Porridge) + Get(Bowl, T.Residue) > 0 && _bowlFilledTonight ? "病床边有一碗夜里送来的粥" : "病床边没有新送来的东西";
```

### CH2-032

**范围与类型：** `production` / `schema`。**索引说明：** 调查条目与本章具体证物、案件变量的映射逻辑。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:18](../../godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:18)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs](../../godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:19)，L19–L30。

```csharp
    public static InvestigationEntry[] Entries(Game g)
    {
        var entries = new List<InvestigationEntry>();
        foreach (var place in g.InspectionRecords.GroupBy(record => record.Place))
        {
            var last = place.Last();
            var inspection = place.Where(record => record.Day == last.Day && record.Slot == last.Slot).ToArray();
            var first = inspection[0];
            entries.Add(new("inspection:" + place.Key, first.Key, Names.Of(place.Key), "物证",
                inspection.Select(record => record.Text).ToArray(), [InspectionSource(first)], first.Key));
        }
        foreach (var records in g.InspectionRecords.GroupBy(record => record.Key))
```

### CH2-049

**范围与类型：** `production` / `logic`。**索引说明：** 指定本案各类物证达到何种组合才可读全。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Morning.cs:211](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:211)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Morning.cs](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:212)，L212–L223。

```csharp
    private bool FullyRead(int night, TraceItem t)
    {
        var looked = DetLookedOn.Where(kv => kv.Key > night).SelectMany(kv => kv.Value).ToHashSet();
        var met = DetMetOn.Where(kv => kv.Key > night).SelectMany(kv => kv.Value).ToHashSet();
        var ok = t.Key switch
        {
            "台账" => looked.Contains(Ledger), "门闩" => looked.Contains(Granary), "碗" => looked.Contains(Bowl) || looked.Contains(Bed),
            "前厅脚印" or "前厅机关" => looked.Contains(ArchiveDoor) || Stakeouts.Contains((night, Place.ArchiveFront)),
            "夜巡" => AskedOn.Contains((night + 1, P.Bertrand)) || AskedOn.Contains((night + 2, P.Bertrand)) || Stakeouts.Contains((night, Place.Hall)),
            _ when t.Key.StartsWith("饼屑:") => looked.Contains(Litter[t.Where]),
            _ when t.Key.StartsWith("神情:") => met.Contains(Names.PersonAlias[t.Key[3..]]),
            _ => false
```

### CH2-071

**范围与类型：** `production` / `schema`。**索引说明：** 把案件特定痕迹键映射到对应调查物件。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:128](../../godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:128)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs](../../godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:129)，L129–L140。

```csharp
    private static string TraceImage(Game g, TraceItem trace)
    {
        if (Game.Things_.ContainsKey(trace.Key)) return trace.Key;
        if (trace.Key == "门闩") return "粮仓";
        if (trace.Key is "前厅脚印" or "前厅机关") return "名录库之门";
        if (trace.Key.StartsWith("饼屑:"))
            return Game.Things_.FirstOrDefault(kv => kv.Value.Place == trace.Where && ReferenceEquals(kv.Value.Thing(g), g.Litter.GetValueOrDefault(trace.Where))).Key ?? "";
        return "";
    }

    internal static string SourceText(ObservationSource source) => source switch
    {
```

### CH2-086

**范围与类型：** `production` / `schema`。**索引说明：** 定义本案缺粮、门闩、空碗、废纸篓与夜巡身影的证物模型。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Case.cs:10](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:10)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Case.cs](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:11)，L11–L22。

```csharp
public sealed record CaseTraces(int LedgerShort, bool LatchFixed, bool LatchToolMarks, bool BowlPorridge, IReadOnlyList<Place> CrumbDorms, IReadOnlyList<Build> Figures)
{
    public static readonly string[] Labels = [.. CaseDesign.Traces.Select(t => t.Label)];
    public static readonly Place[] Dorms = [Place.DormW, Place.DormLife, Place.DormS, Place.DormL];
    /// <summary>值相等：两组痕迹描述出来完全相同即相等（列表字段按内容比较）。</summary>
    public bool Equals(CaseTraces? other) => other is not null && Describe().Select(d => d.Text).SequenceEqual(other.Describe().Select(d => d.Text));
    public override int GetHashCode() => string.Join("|", Describe().Select(d => d.Text)).GetHashCode();
    /// <summary>lookedLitters：只描述看过的那几个寝区的废纸篓（没看过的不说，避免替侦探看）；null 表示全部。</summary>
    public IEnumerable<(int Item, string Text)> Describe(IReadOnlyCollection<Place>? lookedLitters = null)
    {
        yield return (0, $"台账：无记录取用×{LedgerShort}");
        yield return (1, LatchFixed ? "粮仓门闩：新的工具痕，已紧固" : "粮仓门闩：仍然松动");
```

### CH2-087

**范围与类型：** `production` / `schema`。**索引说明：** 固定本案反事实重演所用的四项人物信念/处境变量。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Case.cs:32](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:32)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Case.cs](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:33)，L33–L44。

```csharp
public sealed record CaseVars(int MoraBelief, int QuerRule, int MargaretRule, int QuerSupper)
{
    public static readonly string[][] Options = [.. CaseDesign.Variables.Select(v => v.Options)];
    public static readonly string[] Questions = [.. CaseDesign.Variables.Select((v, i) => $"V{i + 1} {v.Question}")];
    public static readonly CaseVars Truth = new(0, 0, 0, 0);
    public static IEnumerable<CaseVars> All()
    {
        for (var a = 0; a < 3; a++) for (var b = 0; b < 3; b++) for (var c = 0; c < 2; c++) for (var d = 0; d < 2; d++) yield return new(a, b, c, d);
    }
    public override string ToString() => $"V1={Options[0][MoraBelief]} V2={Options[1][QuerRule]} V3={Options[2][MargaretRule]} V4={Options[3][QuerSupper]}";
}

```

## S12

**对应建议：** 真实集会与个人接受驱动对质、议事。6 处源码、0 处资源。

### CH2-004

**范围与类型：** `production` / `logic`。**索引说明：** 议事前厅三站、角色资格与夜间夺取的具体规则及反馈。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Council.cs:24](../../godot_csharp/scenes/Chapter2/Prototype/Council.cs:24)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Council.cs](../../godot_csharp/scenes/Chapter2/Prototype/Council.cs:25)，L25–L36。

```csharp
    public bool Staff(IReadOnlyCollection<Person> crew, out string report, bool commander = false)
    {
        var used = new HashSet<Person>();
        var lines = new List<string>();
        Person? Pick(Func<Person, bool> ok, Func<Person, int> rank) => crew.Where(p => !used.Contains(p) && ok(p)).OrderByDescending(rank).ThenBy(p => p.Id).FirstOrDefault();
        var key = crew.FirstOrDefault(p => Get(p.Body, T.ArchiveKey) > 0) ?? crew.FirstOrDefault(p => p.Pro.Lock >= 3);
        if (key is null) { report = "前厅门锁着：没有钥匙，也没有会开锁的人。"; return false; }
        lines.Add(Get(key.Body, T.ArchiveKey) > 0 ? $"{key.Name}用钥匙开了前厅" : $"{key.Name}撬开了前厅的锁");
        var relay = Pick(p => p.Pro.Signal >= 3 || p.Lessons.GetValueOrDefault(12), p => p.Pro.Signal * 2 + (p.Lessons.GetValueOrDefault(12) ? 1 : 0))
                    ?? Pick(p => p.Lessons.ContainsKey(12), _ => 0);
        var full = relay is not null && (relay.Pro.Signal >= 3 || relay.Lessons.GetValueOrDefault(12));
        if (relay is not null) used.Add(relay);
```

### CH2-047

**范围与类型：** `production` / `logic`。**索引说明：** 结算本章誓约庭议案、具名派系立场及具体结局。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Council.cs:127](../../godot_csharp/scenes/Chapter2/Prototype/Council.cs:127)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Council.cs](../../godot_csharp/scenes/Chapter2/Prototype/Council.cs:128)，L128–L139。

```csharp
    public Ending Council(int detBacks = -1)
    {
        var room = People.Where(p => Get(p.Body, T.Sick) == 0).ToArray();
        foreach (var p in room) p.Body.Get<SpatialProperty>()!.Space = Space[Place.Hall];
        Rt.Sync();
        var plans = ArchivePlans().ToArray();
        if (SeizedBy is not null)
        {
            Result = Ending.NightSeized;
            CouncilLog.Add($"议事开始之前，名录库已经被打开过：{Chapter2Design.DayName(SeizedDay)}夜里，{string.Join("、", SeizedBy.Members.Select(Names.Of))}（领头：{Names.Of(SeizedBy.Leader)}）各占一站，把门撬开了。没人记账，也没人见证。");
            ResultText = EndingText(Result);
            return Result;
```

### CH2-088

**范围与类型：** `production` / `logic`。**索引说明：** 结算本案公开指控、证据说服、雀儿洗清或排斥及规则提出。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Case.cs:230](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:230)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Case.cs](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:231)，L231–L242。

```csharp
    public void Confront(ConfrontPlan? plan, bool reopen = false)
    {
        var room = People.Where(p => Get(p.Body, T.Sick) == 0).ToArray();
        foreach (var p in room) p.Body.Get<SpatialProperty>()!.Space = Space[Place.Hall];
        Rt.Sync();
        PerceiveAll();
        var quer = this[Accused ?? P.Quer];
        var logStart = ConfrontLog.Count;
        // 对账在晚饭之后（场面“晚饭后。中央厅点起了灯”）：侦探来对账，不因此错过这一顿。
        if (!reopen && Slot == Slot.Evening && Get(Det.Body, T.Fed) == 0 && Get(Granary, T.Ration) > 0)
        {
            Add(Granary, T.Ration, -1);
```

### CH2-089

**范围与类型：** `production` / `logic`。**索引说明：** 处理案件对质后的特定议案表态、议事站队与共同规则结果。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Case.cs:344](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:344)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Case.cs](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:345)，L345–L356。

```csharp
    private void Propose(ConfrontPlan plan, Person[] room)
    {
        if (plan.Proposal is { } card)
        {
            CaseRule = CardSpec(card, P.Det);
            Det.Acc[CaseRule] = Truth.True;
            Post(CaseRule, Place.Hall);
            ConfrontLog.Add(X("对质.提出新规", ("规矩", CaseRule.Text)));
            var said = RecordEvent(P.Det, X("对质.新规事件", ("规矩", Short(CaseRule))), "交谈", Place.Hall, []);
            foreach (var p in room.Where(p => p != Det)) { Tell(p, CaseRule, Channel.Public); _transmit[(CaseRule.Id, p.Id)] = said; }
            foreach (var p in room.Where(p => p != Det)) NoteFor(p.Id, "规则", X("对质.新规旁注", ("规矩", card.Text), ("称谓", p.Pro.Pronoun),
                ("态度", X(p.Acc[CaseRule] == Truth.True ? "对质.旁注照办" : p.Vetoed.Contains(CaseRule) ? "对质.旁注拒绝" : "对质.旁注未承诺"))));
```

### CH2-090

**范围与类型：** `production` / `logic`。**索引说明：** 允许案发后援引本章共同规则重开中央厅对质。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Case.cs:399](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:399)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Case.cs](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:400)，L400–L411。

```csharp
    private void Reopen(ConfrontPlan? plan)
    {
        if (_reopenedDay == Day) { Feed.Add("今天已经重开过一次对质了。"); return; }
        if (Day < CaseDay || Day == CaseDay && Slot < Slot.Night) { Feed.Add("翌日傍晚的对质还没开始。"); return; }
        if (QuerCleared || Accused is null) { Feed.Add("眼下没有悬着的指控要重开。"); return; }
        _reopenedDay = Day;
        var head = $"你援引「{CommonRule.Text}」，请众人回中央厅，把台账的事重新对一遍。";
        Feed.Add(head);
        if (!CommonRuleEstablished)
        {
            var room = People.Where(p => p != Det && Get(p.Body, T.Sick) == 0).ToArray();
            foreach (var p in room) Tell(p, CommonRule, Channel.Public);
```

### CH2-091

**范围与类型：** `production` / `logic`。**索引说明：** 按本案指控结局改变雀儿的寝区、信任与名誉认知。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Case.cs:423](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:423)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Case.cs](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:424)，L424–L435。

```csharp
    private void Ostracize()
    {
        if (Accused is not { } acc) return;
        QuerOstracized = true;
        var who = this[acc];
        foreach (var p in People.Where(p => p != who && ((Belief(p, who, T.Branded) ?? 0) > 0 || (Belief(p, who, T.Accused) ?? 0) > 0)))
            Witness(p, K("仍当作贼", p.Id, who.Id), -2, 0, [who], spread: false);
        if (acc == P.Quer)
        {
            ConfrontLog.Add(X("对质.雀儿未撤指控"));
            ConfrontLog.Add(X("对质.雀儿搬出"));
            who.Body.Get<SpatialProperty>()!.Space = Space[Place.Oath];
```

## S13

**对应建议：** 现场身份和资产关联改为表现声明。15 处源码、8 处资源。

### AWK-013

**范围与类型：** `production` / `schema`。**索引说明：** 本案请求类型名映射到固定重演事件与门边站位。

**标记位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs:711](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:711)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:712)，L712–L723。

```csharp
    private (ReplayEvent Event, string Thought, string Spot) RequestText(Agent a, Projection request)
    {
        var sender = request.Relations.FirstOrDefault(r => r.Relation == Relations.Subject).Target;
        var subject = _agents.FirstOrDefault(other => ReferenceEquals(a.Mental[other.Body], sender))?.Who;
        var kind = request.Name switch
        {
            "bolt_request" => "request_bolt", "open_request" => "request_open",
            "keep_open_request" => "request_keep_open", _ => "request_other"
        };
        var spot = kind == "request_other" ? "" : "door";
        var ev = new ReplayEvent(kind, a.Who, subject, spot);
        return (ev, kind == "request_other" ? "" : Chapter1Data.Text("重演·心声·" + kind,
```

### AWK-014

**范围与类型：** `production` / `schema`。**索引说明：** 案件物件分别映射到门、石板、铜口、书桌和水池回放站位。

**标记位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs:829](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:829)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/NightReplay.cs](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:830)，L830–L841。

```csharp
    private string SpotOf(Projection target)
    {
        var essence = _view.Keys.FirstOrDefault(e => _agents.Any(a => a.Mental.TryGetValue(e, out var m) && ReferenceEquals(m, target)));
        if (essence == _door || essence == _frame || essence == _wedge) return "door";
        if (essence == _slate) return "slate";
        if (essence == _tube) return "tube";
        if (essence == _score) return "desk";
        if (essence == _cups || essence == _cloth) return "sink";
        return "";
    }

    // ---------------------------------------------------------------- physics --
```

### AWK-041

**范围与类型：** `production` / `schema`。**索引说明：** 日常行动目标映射到本章 piano/tube/sink/door/score 等现场物件。

**标记位置：** [godot_csharp/scenes/Awakening/AwakeningMap.gd:284](../../godot_csharp/scenes/Awakening/AwakeningMap.gd:284)。

**原文位置：** [godot_csharp/scenes/Awakening/AwakeningMap.gd](../../godot_csharp/scenes/Awakening/AwakeningMap.gd:285)，L285–L296。

```gdscript
func station(target: String, who: String) -> Dictionary:
	match target:
		"self":
			var start = anchor("起点·" + who)
			return {} if start == null else {"point": start, "radius": 12.0}
		"piano":
			var piano := $ObjectsAndPeople.get_node_or_null("Piano") as Node2D
			return {} if piano == null else {"point": piano.position + Vector2(0, 32), "focus": piano.position, "radius": 16.0}
	var site := {"tube": "E8", "sink": "E9", "door": "E8j", "score": "E6"}
	if site.has(target):
		var geometry := site_geometry({})
		if geometry.has(site[target]):
```

### AWK-042

**范围与类型：** `production` / `schema`。**索引说明：** 绘制本案铜管两条具体路径及松动接头位置。

**标记位置：** [godot_csharp/scenes/Awakening/TraceDecals.gd:8](../../godot_csharp/scenes/Awakening/TraceDecals.gd:8)。

**原文位置：** [godot_csharp/scenes/Awakening/TraceDecals.gd](../../godot_csharp/scenes/Awakening/TraceDecals.gd:9)，L9–L13。

```gdscript
const PIPES := [
	[Vector2(500, 40), Vector2(304, 40), Vector2(304, 266), Vector2(164, 266), Vector2(164, 346), Vector2(154, 346)],
	[Vector2(500, 40), Vector2(592, 40), Vector2(592, 204)],
]
## [贴图, 位置, 旋转角度]：门在门厅西墙，灰扇向东散开，湿布贴着门竖着压成一条，靴印鞋尖朝北侧的插闩把手。
```

### AWK-043

**范围与类型：** `production` / `schema`。**索引说明：** 绘制本案灰扇、靴印、湿布、接头与门框凹痕的位置方向。

**标记位置：** [godot_csharp/scenes/Awakening/TraceDecals.gd:14](../../godot_csharp/scenes/Awakening/TraceDecals.gd:14)。

**原文位置：** [godot_csharp/scenes/Awakening/TraceDecals.gd](../../godot_csharp/scenes/Awakening/TraceDecals.gd:15)，L15–L26。

```gdscript
const DECALS := [
	["ash_fan.png", Vector2(184, 322), 90.0],
	["boot_print.png", Vector2(156, 418), -90.0],
	["wet_cloth.png", Vector2(157, 354), 90.0],
	["pipe_joint.png", Vector2(157, 320), 0.0],
	["pipe_joint.png", Vector2(585, 186), 0.0],
	["frame_dent.png", Vector2(157, 344), 0.0],
]

var host: Node
var textures: Dictionary = {}
var _known_cells := -1
```

### AWK-044

**范围与类型：** `production` / `logic`。**索引说明：** 按本章 R-05 半枚手印、住人房方框与空房均匀灰尘呈现固定证物。

**标记位置：** [godot_csharp/scenes/Awakening/TraceDecals.gd:60](../../godot_csharp/scenes/Awakening/TraceDecals.gd:60)。

**原文位置：** [godot_csharp/scenes/Awakening/TraceDecals.gd](../../godot_csharp/scenes/Awakening/TraceDecals.gd:61)，L61–L72。

```gdscript
	for shelf in host.layout.shelves:
		if not _known(Vector2(shelf[0], shelf[1])):
			continue
		var texture: Texture2D = textures.get({"hand": "shelf_dust_hand.png", "plain": "shelf_dust.png", "even": "shelf_dust_even.png"}[shelf[2]])
		if texture:
			draw_texture(texture, Vector2(shelf[0], shelf[1]))

func _pipe(points: Array, copper: Color, copper_light: Color, shade: Color) -> void:
	for i in range(1, points.size()):
		var a: Vector2 = points[i - 1]
		var b: Vector2 = points[i]
		var r := Rect2(a.min(b) - Vector2(1, 1), (b - a).abs() + Vector2(3, 3))
```

### AWK-058

**范围与类型：** `production` / `schema`。**索引说明：** 宿舍门所有者键与本章角色节点名称固定绑定。

**标记位置：** [godot_csharp/scenes/Awakening/RoomDoor.gd:18](../../godot_csharp/scenes/Awakening/RoomDoor.gd:18)。

**原文位置：** [godot_csharp/scenes/Awakening/RoomDoor.gd](../../godot_csharp/scenes/Awakening/RoomDoor.gd:19)，L19–L30。

```gdscript
func owner_actor() -> CharacterBody2D:
	var actor_name: String = {"detective": "侦探", "engineer": "工程师", "cook": "厨师", "musician": "音乐家"}[dorm_owner]
	return room.actors.get(actor_name)

func access_allowed() -> bool:
	return dorm_owner == "detective" or (room.has_method("can_access_dorm") and bool(room.call("can_access_dorm", dorm_owner)))

func threshold_collision_rect() -> Rect2:
	var size: Vector2 = leaf_collision.shape.size
	return Rect2(position + leaf_collision.position - size * 0.5, size)

func door_click_rect() -> Rect2:
```

### AWK-059

**范围与类型：** `production` / `schema`。**索引说明：** 本章人物键与正式现场角色节点名称固定绑定。

**标记位置：** [godot_csharp/scenes/Awakening/AwakeningMap.gd:14](../../godot_csharp/scenes/Awakening/AwakeningMap.gd:14)。

**原文位置：** [godot_csharp/scenes/Awakening/AwakeningMap.gd](../../godot_csharp/scenes/Awakening/AwakeningMap.gd:15)，L15–L26。

```gdscript
const ACTOR_NODES := {"detective": "侦探", "huo": "工程师", "shen": "厨师", "su": "音乐家"}

var field: Node2D
var layout: Dictionary = {}
var spatial: Dictionary = {}
var regions: Array = []
var room_names: Dictionary = {}
var floors: Dictionary = {}
var obstacle_rects: Array[Rect2] = []
var decals: Node2D
var stage_node: Node2D
## RoomDoor 读取：按节点名索引的人物、玩家与宿舍门权限。
```

### CH2-024

**范围与类型：** `production` / `schema`。**索引说明：** 角色身份到专属肖像标识及地图地点的映射写在宿主代码。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2SessionView.cs:20](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2SessionView.cs:20)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2SessionView.cs](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2SessionView.cs:21)，L21–L32。

```csharp
    private static readonly Dictionary<string, string> Sprites = new()
    {
        ["Det"] = "detective", ["Huo"] = "engineer", ["Shen"] = "chef", ["Su"] = "musician", ["Lucian"] = "poet", ["Gu"] = "archaeologist",
        ["Quer"] = "thief", ["Ward"] = "lawyer", ["Selma"] = "naturalist", ["Ruan"] = "chessplayer", ["Elena"] = "dancer", ["Moran"] = "painter",
        ["Veronica"] = "chemist", ["Bertrand"] = "marksman", ["Margaret"] = "architect", ["Mora"] = "doctor",
    };
    /// <summary>地图 → 侦探在引擎里的地点（室外过道不改）。</summary>
    private static readonly Dictionary<string, string> MapPlaces = new()
    {
        ["central_hall"] = "Hall", ["lecture"] = "Lecture", ["logistics"] = "Logistics", ["granary"] = "Logistics", ["clinic"] = "Clinic",
        ["greenhouse"] = "Greenhouse", ["archive"] = "ArchiveFront", ["counterweight"] = "ArchiveFront", ["campus_s"] = "Oath", ["living_hall"] = "Living",
        ["campus_n"] = "Hall", ["campus_nw"] = "Hall", ["campus_ne"] = "Logistics", ["campus_sw"] = "Hall", ["harbor"] = "Oath",
```

### CH2-053

**范围与类型：** `production` / `schema`。**索引说明：** 固定本案可调查物件、对应地点和真实实体。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Player.cs:44](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:44)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Player.cs](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:45)，L45–L56。

```csharp
    public static readonly ImmutableDictionary<string, (Place Place, Func<Game, Essence> Thing)> Things_ = new Dictionary<string, (Place, Func<Game, Essence>)>
    {
        ["粮仓"] = (Place.Logistics, g => g.Granary), ["台账"] = (Place.Logistics, g => g.Ledger), ["配给台公告"] = (Place.Logistics, g => g.Board[Place.Logistics]),
        ["中央厅公告"] = (Place.Hall, g => g.Board[Place.Hall]), ["中央基座"] = (Place.Hall, g => g.Base), ["速写本"] = (Place.DormS, g => g.SketchBook), ["碗"] = (Place.Clinic, g => g.Bowl), ["病床"] = (Place.Clinic, g => g.Bed),
        ["药柜"] = (Place.Clinic, g => g.MedCab), ["黑板"] = (Place.Lecture, g => g.Blackboard), ["铺石"] = (Place.Oath, g => g.Stele),
        ["压力表"] = (Place.Greenhouse, g => g.Gauge), ["名录库之门"] = (Place.ArchiveFront, g => g.ArchiveDoor), ["油库"] = (Place.DormS, g => g.OilStore),
        ["文契废纸篓"] = (Place.DormW, g => g.Litter[Place.DormW]), ["星仪废纸篓"] = (Place.DormS, g => g.Litter[Place.DormS]),
        ["后勤废纸篓"] = (Place.DormL, g => g.Litter[Place.DormL]), ["生活翼废纸篓"] = (Place.DormLife, g => g.Litter[Place.DormLife]), ["文契公告"] = (Place.DormW, g => g.Board[Place.DormW]),
        ["生活翼公告"] = (Place.DormLife, g => g.Board[Place.DormLife]), ["讲堂公告"] = (Place.Lecture, g => g.Board[Place.Lecture]),
        ["星仪公告"] = (Place.DormS, g => g.Board[Place.DormS]), ["后勤公告"] = (Place.DormL, g => g.Board[Place.DormL]),
    }.ToImmutableDictionary();

```

### CH2-054

**范围与类型：** `production` / `schema`。**索引说明：** 固定各案发相关地点的调查物件集合。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Player.cs:58](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:58)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Player.cs](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:59)，L59–L68。

```csharp
    public static readonly ImmutableDictionary<Place, string[]> AreaThings = new Dictionary<Place, string[]>
    {
        [Place.Logistics] = ["粮仓", "台账", "配给台公告"], [Place.Clinic] = ["碗", "病床", "药柜"], [Place.DormW] = ["文契废纸篓", "文契公告"],
        [Place.DormS] = ["星仪废纸篓", "油库", "速写本", "星仪公告"], [Place.DormL] = ["后勤废纸篓", "后勤公告"], [Place.DormLife] = ["生活翼废纸篓", "生活翼公告"],
        [Place.ArchiveFront] = ["名录库之门"], [Place.Lecture] = ["黑板", "讲堂公告"], [Place.Greenhouse] = ["压力表"], [Place.Oath] = ["铺石"], [Place.Hall] = ["中央基座", "中央厅公告"],
    }.ToImmutableDictionary();

    private Effect SayTo(Fx fx, Person who, string text, Effect request) => Fx.Seq(fx.Go(Where(who)), new Effect.Say(fx.R(who.Body), text, request));

    /// <summary>时段开头：侦探先找到要说话的人，把请求说出口（Effect.Say 携带 Request），对方再与自己的习惯一起决策。</summary>
```

### CH2-083

**范围与类型：** `production` / `schema`。**索引说明：** 白名单固定本章专用的站点交互类型。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2AuthoredSites.cs:22](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2AuthoredSites.cs:22)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2AuthoredSites.cs](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2AuthoredSites.cs:23)，L23–L29。

```csharp
            var kind=Read("kind");if(kind is not ("" or "bed" or "watch" or "meal" or "tube" or "school_rules"))throw new InvalidDataException("Unsupported site kind: "+kind);
            var title=Read("title");if(string.IsNullOrWhiteSpace(title))throw new InvalidDataException("Missing site title: "+id);
            var thing=Read("thing");
            if(thing!=""&&(!entities.TryGetValue(thing,out var target)||target.Kind!="object"))throw new InvalidDataException("Site references an undeclared object: "+thing);
            if(thing!="")
            {
                var declaration=entities[thing];
```

### CH2-100

**范围与类型：** `production` / `schema`。**索引说明：** 豁免本章时钟和传声物件的房间一致性校验。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2AuthoredSites.cs:30](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2AuthoredSites.cs:30)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2AuthoredSites.cs](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2AuthoredSites.cs:31)，L31–L40。

```csharp
                // Clock and NameVoice have no Core SpatialProperty; room is their presentation anchor.
                if(thing is not ("world:object.Clock" or "world:object.NameVoice") &&
                   (!declaration.Properties.TryGetValue("space",out var space)||space.Value.GetString()!=roomId))
                    throw new InvalidDataException("Site room differs from its physical object: "+map+":"+id);
            }
            result.Add(new(map+":"+id,map,id,room,kind,title,thing==""?null:thing,Read("lore")));
        }
        return result;
    }
}
```

### CSH-033

**范围与类型：** `production` / `schema`。**索引说明：** 特定章节纹理到油灯/柜体可见名称的绑定；不是剧情因果规则。

**标记位置：** [godot_csharp/scenes/Shared/Field/WorldView.gd:234](../../godot_csharp/scenes/Shared/Field/WorldView.gd:234)。

**原文位置：** [godot_csharp/scenes/Shared/Field/WorldView.gd](../../godot_csharp/scenes/Shared/Field/WorldView.gd:235)，L235–L246。

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
		var title := _player_object_readable(str(sites[site_id].title)) if visible_site else _player_object_title(sprite)
		if title == "": title = _player_object_title(sprite)
		if str(sprite.name) == "DoorFace": title += FieldVocabulary.word("门关")
```

### CSH-035

**范围与类型：** `production` / `schema`。**索引说明：** 专用盟誓庭锚点与侦探位置绑定；名单由外部快照驱动。

**标记位置：** [godot_csharp/scenes/Shared/Field/WorldView.gd:703](../../godot_csharp/scenes/Shared/Field/WorldView.gd:703)。

**原文位置：** [godot_csharp/scenes/Shared/Field/WorldView.gd](../../godot_csharp/scenes/Shared/Field/WorldView.gd:704)，L704–L715。

```gdscript
	var court: Vector2 = adapter.anchor("site:oath_court") if adapter.anchor("site:oath_court") != null else player.position
	var i := 0
	for p in snapshot.get("people", []):
		if not bool(p.get("court_present", false)): continue
		var actor: CharacterBody2D = actors.get(str(p.key))
		if not is_instance_valid(actor): actor = _make_actor(str(p.key), str(p.get("sprite", "detective")))
		actor.position = court + Vector2((i % 5) * 34 - 68, (i / 5) * 34 - 30)
		i += 1
	player.position = court + Vector2(0, 110)

## 有人挡在侦探的路上：没有被剧情占用的同伴自己让开。
func _make_way() -> void:
```

### RES-081

**范围与类型：** `production` / `schema`。**索引说明：** 生成布局将明确角色绑定到固定房间；只记录空间/人物绑定，不推断角色行为或剧情因果。

**原文位置：** [godot_csharp/scenes/Awakening/map_layout.tres](../../godot_csharp/scenes/Awakening/map_layout.tres:1066)，L1066–L1066。

```text
   "owner": "musician",
```

### RES-082

**范围与类型：** `production` / `schema`。**索引说明：** 生成布局将明确角色绑定到固定房间；只记录空间/人物绑定，不推断角色行为或剧情因果。

**原文位置：** [godot_csharp/scenes/Awakening/map_layout.tres](../../godot_csharp/scenes/Awakening/map_layout.tres:1072)，L1072–L1072。

```text
   "owner": "detective",
```

### RES-083

**范围与类型：** `production` / `schema`。**索引说明：** 生成布局将明确角色绑定到固定房间；只记录空间/人物绑定，不推断角色行为或剧情因果。

**原文位置：** [godot_csharp/scenes/Awakening/map_layout.tres](../../godot_csharp/scenes/Awakening/map_layout.tres:1084)，L1084–L1084。

```text
   "owner": "cook",
```

### RES-084

**范围与类型：** `production` / `schema`。**索引说明：** 生成布局将明确角色绑定到固定房间；只记录空间/人物绑定，不推断角色行为或剧情因果。

**原文位置：** [godot_csharp/scenes/Awakening/map_layout.tres](../../godot_csharp/scenes/Awakening/map_layout.tres:1090)，L1090–L1090。

```text
   "owner": "engineer",
```

### RES-085

**范围与类型：** `production` / `schema`。**索引说明：** 生成现场将固定角色动画资源接入场景；记录资源身份绑定，不表示剧情因果。

**原文位置：** [godot_csharp/scenes/Awakening/Awakening.tscn](../../godot_csharp/scenes/Awakening/Awakening.tscn:8)，L8–L8。

```text
[ext_resource type="SpriteFrames" path="res://ShareAsset/Internal/Characters/PixelCast/detective.tres" id="detective_frames"]
```

### RES-086

**范围与类型：** `production` / `schema`。**索引说明：** 生成现场将固定角色动画资源接入场景；记录资源身份绑定，不表示剧情因果。

**原文位置：** [godot_csharp/scenes/Awakening/Awakening.tscn](../../godot_csharp/scenes/Awakening/Awakening.tscn:9)，L9–L9。

```text
[ext_resource type="SpriteFrames" path="res://ShareAsset/Internal/Characters/PixelCast/engineer.tres" id="engineer_frames"]
```

### RES-087

**范围与类型：** `production` / `schema`。**索引说明：** 生成现场将固定角色动画资源接入场景；记录资源身份绑定，不表示剧情因果。

**原文位置：** [godot_csharp/scenes/Awakening/Awakening.tscn](../../godot_csharp/scenes/Awakening/Awakening.tscn:10)，L10–L10。

```text
[ext_resource type="SpriteFrames" path="res://ShareAsset/Internal/Characters/PixelCast/chef.tres" id="chef_frames"]
```

### RES-088

**范围与类型：** `production` / `schema`。**索引说明：** 生成现场将固定角色动画资源接入场景；记录资源身份绑定，不表示剧情因果。

**原文位置：** [godot_csharp/scenes/Awakening/Awakening.tscn](../../godot_csharp/scenes/Awakening/Awakening.tscn:11)，L11–L11。

```text
[ext_resource type="SpriteFrames" path="res://ShareAsset/Internal/Characters/PixelCast/musician.tres" id="musician_frames"]
```

## S14

**对应建议：** 资源生成器读取剧本显示内容。0 处源码、80 处资源。

### RES-001

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/archive.tscn](../../godot_csharp/scenes/Chapter2/Maps/archive.tscn:188)，L188–L188。

```text
metadata/title = "名录库·文献工房"
```

### RES-002

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/archive.tscn](../../godot_csharp/scenes/Chapter2/Maps/archive.tscn:615)，L615–L615。

```text
metadata/label = "库门锁芯"
```

### RES-003

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/archive.tscn](../../godot_csharp/scenes/Chapter2/Maps/archive.tscn:619)，L619–L619。

```text
metadata/label = "封存匣"
```

### RES-004

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/archive.tscn](../../godot_csharp/scenes/Chapter2/Maps/archive.tscn:623)，L623–L623。

```text
metadata/label = "唤名警告"
```

### RES-005

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/archive.tscn](../../godot_csharp/scenes/Chapter2/Maps/archive.tscn:627)，L627–L627。

```text
metadata/label = "规程页"
```

### RES-006

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/archive.tscn](../../godot_csharp/scenes/Chapter2/Maps/archive.tscn:631)，L631–L631。

```text
metadata/label = "守夜点"
```

### RES-007

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/campus_n.tscn](../../godot_csharp/scenes/Chapter2/Maps/campus_n.tscn:564)，L564–L564。

```text
metadata/title = "学园·中央厅与名录库"
```

### RES-008

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/campus_n.tscn](../../godot_csharp/scenes/Chapter2/Maps/campus_n.tscn:2125)，L2125–L2125。

```text
metadata/label = "汲水泉"
```

### RES-009

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/campus_ne.tscn](../../godot_csharp/scenes/Chapter2/Maps/campus_ne.tscn:520)，L520–L520。

```text
metadata/title = "学园·温室与汲水泉"
```

### RES-010

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/campus_ne.tscn](../../godot_csharp/scenes/Chapter2/Maps/campus_ne.tscn:1923)，L1923–L1923。

```text
metadata/label = "汲水泉"
```

### RES-011

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/campus_nw.tscn](../../godot_csharp/scenes/Chapter2/Maps/campus_nw.tscn:766)，L766–L766。

```text
metadata/title = "学园·讲堂与星仪院"
```

### RES-012

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/campus_s.tscn](../../godot_csharp/scenes/Chapter2/Maps/campus_s.tscn:1142)，L1142–L1142。

```text
metadata/title = "学园·誓约庭与后勤院"
```

### RES-013

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/campus_s.tscn](../../godot_csharp/scenes/Chapter2/Maps/campus_s.tscn:4080)，L4080–L4080。

```text
metadata/label = "誓约庭"
```

### RES-014

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/campus_s.tscn](../../godot_csharp/scenes/Chapter2/Maps/campus_s.tscn:4084)，L4084–L4084。

```text
metadata/label = "汲水泉"
```

### RES-015

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/campus_s.tscn](../../godot_csharp/scenes/Chapter2/Maps/campus_s.tscn:4088)，L4088–L4088。

```text
metadata/label = "晾晒场"
```

### RES-016

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/campus_s.tscn](../../godot_csharp/scenes/Chapter2/Maps/campus_s.tscn:4092)，L4092–L4092。

```text
metadata/label = "门楼"
```

### RES-017

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/campus_sw.tscn](../../godot_csharp/scenes/Chapter2/Maps/campus_sw.tscn:635)，L635–L635。

```text
metadata/title = "学园·生活翼"
```

### RES-018

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/campus_sw.tscn](../../godot_csharp/scenes/Chapter2/Maps/campus_sw.tscn:2250)，L2250–L2250。

```text
metadata/label = "夹道"
```

### RES-019

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/central_hall.tscn](../../godot_csharp/scenes/Chapter2/Maps/central_hall.tscn:193)，L193–L193。

```text
metadata/title = "中央厅"
```

### RES-020

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/central_hall.tscn](../../godot_csharp/scenes/Chapter2/Maps/central_hall.tscn:608)，L608–L608。

```text
metadata/label = "传声中继"
```

### RES-021

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/central_hall.tscn](../../godot_csharp/scenes/Chapter2/Maps/central_hall.tscn:612)，L612–L612。

```text
metadata/label = "铜口"
```

### RES-022

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/central_hall.tscn](../../godot_csharp/scenes/Chapter2/Maps/central_hall.tscn:616)，L616–L616。

```text
metadata/label = "中央公告板"
```

### RES-023

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/central_hall.tscn](../../godot_csharp/scenes/Chapter2/Maps/central_hall.tscn:620)，L620–L620。

```text
metadata/label = "守夜点"
```

### RES-024

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/clinic.tscn](../../godot_csharp/scenes/Chapter2/Maps/clinic.tscn:118)，L118–L118。

```text
metadata/title = "诊疗所·安全端"
```

### RES-025

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/clinic.tscn](../../godot_csharp/scenes/Chapter2/Maps/clinic.tscn:400)，L400–L400。

```text
metadata/label = "试剂柜"
```

### RES-026

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/clinic.tscn](../../godot_csharp/scenes/Chapter2/Maps/clinic.tscn:404)，L404–L404。

```text
metadata/label = "病床"
```

### RES-027

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/counterweight.tscn](../../godot_csharp/scenes/Chapter2/Maps/counterweight.tscn:68)，L68–L68。

```text
metadata/title = "配重室"
```

### RES-028

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/counterweight.tscn](../../godot_csharp/scenes/Chapter2/Maps/counterweight.tscn:236)，L236–L236。

```text
metadata/label = "配重"
```

### RES-029

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/granary.tscn](../../godot_csharp/scenes/Chapter2/Maps/granary.tscn:73)，L73–L73。

```text
metadata/title = "粮仓"
```

### RES-030

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/granary.tscn](../../godot_csharp/scenes/Chapter2/Maps/granary.tscn:227)，L227–L227。

```text
metadata/label = "门闩"
```

### RES-031

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/granary.tscn](../../godot_csharp/scenes/Chapter2/Maps/granary.tscn:231)，L231–L231。

```text
metadata/label = "存粮"
```

### RES-032

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/greenhouse.tscn](../../godot_csharp/scenes/Chapter2/Maps/greenhouse.tscn:218)，L218–L218。

```text
metadata/title = "温室"
```

### RES-033

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/greenhouse.tscn](../../godot_csharp/scenes/Chapter2/Maps/greenhouse.tscn:630)，L630–L630。

```text
metadata/label = "阀门"
```

### RES-034

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/greenhouse.tscn](../../godot_csharp/scenes/Chapter2/Maps/greenhouse.tscn:634)，L634–L634。

```text
metadata/label = "压力表"
```

### RES-035

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/harbor.tscn](../../godot_csharp/scenes/Chapter2/Maps/harbor.tscn:1243)，L1243–L1243。

```text
metadata/title = "港路与港口"
```

### RES-036

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/harbor.tscn](../../godot_csharp/scenes/Chapter2/Maps/harbor.tscn:3995)，L3995–L3995。

```text
metadata/label = "港路塌方处"
```

### RES-037

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/harbor.tscn](../../godot_csharp/scenes/Chapter2/Maps/harbor.tscn:3999)，L3999–L3999。

```text
metadata/label = "汲水泉"
```

### RES-038

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/harbor.tscn](../../godot_csharp/scenes/Chapter2/Maps/harbor.tscn:4003)，L4003–L4003。

```text
metadata/label = "契约石碑"
```

### RES-039

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/harbor.tscn](../../godot_csharp/scenes/Chapter2/Maps/harbor.tscn:4007)，L4007–L4007。

```text
metadata/label = "码头"
```

### RES-040

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/lecture.tscn](../../godot_csharp/scenes/Chapter2/Maps/lecture.tscn:131)，L131–L131。

```text
metadata/title = "讲堂"
```

### RES-041

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/lecture.tscn](../../godot_csharp/scenes/Chapter2/Maps/lecture.tscn:439)，L439–L439。

```text
metadata/label = "黑板"
```

### RES-042

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/lecture.tscn](../../godot_csharp/scenes/Chapter2/Maps/lecture.tscn:443)，L443–L443。

```text
metadata/label = "课表"
```

### RES-043

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/living_hall.tscn](../../godot_csharp/scenes/Chapter2/Maps/living_hall.tscn:319)，L319–L319。

```text
metadata/title = "起居厅"
```

### RES-044

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/living_hall.tscn](../../godot_csharp/scenes/Chapter2/Maps/living_hall.tscn:987)，L987–L987。

```text
metadata/label = "守夜点"
```

### RES-045

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/living_hall.tscn](../../godot_csharp/scenes/Chapter2/Maps/living_hall.tscn:991)，L991–L991。

```text
metadata/label = "生活翼公告板"
```

### RES-046

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/living_hall.tscn](../../godot_csharp/scenes/Chapter2/Maps/living_hall.tscn:995)，L995–L995。

```text
metadata/label = "壁炉"
```

### RES-047

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/logistics.tscn](../../godot_csharp/scenes/Chapter2/Maps/logistics.tscn:152)，L152–L152。

```text
metadata/title = "后勤院"
```

### RES-048

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/logistics.tscn](../../godot_csharp/scenes/Chapter2/Maps/logistics.tscn:521)，L521–L521。

```text
metadata/label = "配给台"
```

### RES-049

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/logistics.tscn](../../godot_csharp/scenes/Chapter2/Maps/logistics.tscn:525)，L525–L525。

```text
metadata/label = "台账"
```

### RES-050

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/logistics.tscn](../../godot_csharp/scenes/Chapter2/Maps/logistics.tscn:529)，L529–L529。

```text
metadata/label = "配给台公告板"
```

### RES-051

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/logistics.tscn](../../godot_csharp/scenes/Chapter2/Maps/logistics.tscn:533)，L533–L533。

```text
metadata/label = "守夜点"
```

### RES-052

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/resonance.tscn](../../godot_csharp/scenes/Chapter2/Maps/resonance.tscn:157)，L157–L157。

```text
metadata/title = "共鸣厅·沉静室"
```

### RES-053

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/resonance.tscn](../../godot_csharp/scenes/Chapter2/Maps/resonance.tscn:531)，L531–L531。

```text
metadata/label = "门内步位标线"
```

### RES-054

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/resonance.tscn](../../godot_csharp/scenes/Chapter2/Maps/resonance.tscn:535)，L535–L535。

```text
metadata/label = "沉静室门"
```

### RES-055

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/zone_bing.tscn](../../godot_csharp/scenes/Chapter2/Maps/zone_bing.tscn:489)，L489–L489。

```text
metadata/title = "丙区"
```

### RES-056

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/zone_bing.tscn](../../godot_csharp/scenes/Chapter2/Maps/zone_bing.tscn:1495)，L1495–L1495。

```text
metadata/label = "铜口"
```

### RES-057

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/zone_bing.tscn](../../godot_csharp/scenes/Chapter2/Maps/zone_bing.tscn:1499)，L1499–L1499。

```text
metadata/label = "公告框"
```

### RES-058

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/zone_bing.tscn](../../godot_csharp/scenes/Chapter2/Maps/zone_bing.tscn:1503)，L1503–L1503。

```text
metadata/label = "隔离门"
```

### RES-059

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/zone_ding.tscn](../../godot_csharp/scenes/Chapter2/Maps/zone_ding.tscn:485)，L485–L485。

```text
metadata/title = "丁区"
```

### RES-060

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/zone_ding.tscn](../../godot_csharp/scenes/Chapter2/Maps/zone_ding.tscn:1481)，L1481–L1481。

```text
metadata/label = "铜口"
```

### RES-061

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/zone_ding.tscn](../../godot_csharp/scenes/Chapter2/Maps/zone_ding.tscn:1485)，L1485–L1485。

```text
metadata/label = "公告框"
```

### RES-062

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/zone_ding.tscn](../../godot_csharp/scenes/Chapter2/Maps/zone_ding.tscn:1489)，L1489–L1489。

```text
metadata/label = "隔离门"
```

### RES-063

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/zone_jia.tscn](../../godot_csharp/scenes/Chapter2/Maps/zone_jia.tscn:485)，L485–L485。

```text
metadata/title = "甲区"
```

### RES-064

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/zone_jia.tscn](../../godot_csharp/scenes/Chapter2/Maps/zone_jia.tscn:1481)，L1481–L1481。

```text
metadata/label = "铜口"
```

### RES-065

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/zone_jia.tscn](../../godot_csharp/scenes/Chapter2/Maps/zone_jia.tscn:1485)，L1485–L1485。

```text
metadata/label = "公告框"
```

### RES-066

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/zone_jia.tscn](../../godot_csharp/scenes/Chapter2/Maps/zone_jia.tscn:1489)，L1489–L1489。

```text
metadata/label = "隔离门"
```

### RES-067

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/zone_yi.tscn](../../godot_csharp/scenes/Chapter2/Maps/zone_yi.tscn:484)，L484–L484。

```text
metadata/title = "乙区"
```

### RES-068

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/zone_yi.tscn](../../godot_csharp/scenes/Chapter2/Maps/zone_yi.tscn:1480)，L1480–L1480。

```text
metadata/label = "铜口"
```

### RES-069

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/zone_yi.tscn](../../godot_csharp/scenes/Chapter2/Maps/zone_yi.tscn:1484)，L1484–L1484。

```text
metadata/label = "公告框"
```

### RES-070

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/zone_yi.tscn](../../godot_csharp/scenes/Chapter2/Maps/zone_yi.tscn:1488)，L1488–L1488。

```text
metadata/label = "隔离门"
```

### RES-071

**范围与类型：** `production` / `schema`。**索引说明：** 生成地图的固定地点名称/热点显示标签资源绑定；来自 build_chapter2_scenes.py 与 chapter2_art/rooms.py 或 outdoor.py，仅记录绑定，不表示因果规则。

**原文位置：** [godot_csharp/scenes/Chapter2/Maps/zone_yi.tscn](../../godot_csharp/scenes/Chapter2/Maps/zone_yi.tscn:1492)，L1492–L1492。

```text
metadata/label = "bed"
```

### RES-072

**范围与类型：** `production` / `text`。**索引说明：** 生成布局内固定现场环境短句；属于可见描写文字，不陈述事件因果。

**原文位置：** [godot_csharp/scenes/Awakening/map_layout.tres](../../godot_csharp/scenes/Awakening/map_layout.tres:947)，L947–L947。

```text
   "窗。外面是灰的，什么也看不清。"
```

### RES-073

**范围与类型：** `production` / `text`。**索引说明：** 生成布局内固定现场环境短句；属于可见描写文字，不陈述事件因果。

**原文位置：** [godot_csharp/scenes/Awakening/map_layout.tres](../../godot_csharp/scenes/Awakening/map_layout.tres:956)，L956–L956。

```text
   "窗。外面是灰的，什么也看不清。"
```

### RES-074

**范围与类型：** `production` / `text`。**索引说明：** 生成布局内固定现场环境短句；属于可见描写文字，不陈述事件因果。

**原文位置：** [godot_csharp/scenes/Awakening/map_layout.tres](../../godot_csharp/scenes/Awakening/map_layout.tres:965)，L965–L965。

```text
   "窗。外面是灰的，什么也看不清。"
```

### RES-075

**范围与类型：** `production` / `text`。**索引说明：** 生成布局内固定现场环境短句；属于可见描写文字，不陈述事件因果。

**原文位置：** [godot_csharp/scenes/Awakening/map_layout.tres](../../godot_csharp/scenes/Awakening/map_layout.tres:974)，L974–L974。

```text
   "窗。雾贴着玻璃。"
```

### RES-076

**范围与类型：** `production` / `text`。**索引说明：** 生成布局内固定现场环境短句；属于可见描写文字，不陈述事件因果。

**原文位置：** [godot_csharp/scenes/Awakening/map_layout.tres](../../godot_csharp/scenes/Awakening/map_layout.tres:983)，L983–L983。

```text
   "地毯。"
```

### RES-077

**范围与类型：** `production` / `text`。**索引说明：** 生成布局内固定现场环境短句；属于可见描写文字，不陈述事件因果。

**原文位置：** [godot_csharp/scenes/Awakening/map_layout.tres](../../godot_csharp/scenes/Awakening/map_layout.tres:992)，L992–L992。

```text
   "地毯。踩得发亮的地方，在床边。"
```

### RES-078

**范围与类型：** `production` / `text`。**索引说明：** 生成布局内固定现场环境短句；属于可见描写文字，不陈述事件因果。

**原文位置：** [godot_csharp/scenes/Awakening/map_layout.tres](../../godot_csharp/scenes/Awakening/map_layout.tres:1001)，L1001–L1001。

```text
   "地毯。"
```

### RES-079

**范围与类型：** `production` / `text`。**索引说明：** 生成布局内固定现场环境短句；属于可见描写文字，不陈述事件因果。

**原文位置：** [godot_csharp/scenes/Awakening/map_layout.tres](../../godot_csharp/scenes/Awakening/map_layout.tres:1010)，L1010–L1010。

```text
   "地毯。踩得发亮的地方，在床边。"
```

### RES-080

**范围与类型：** `production` / `text`。**索引说明：** 生成布局内固定现场环境短句；属于可见描写文字，不陈述事件因果。

**原文位置：** [godot_csharp/scenes/Awakening/map_layout.tres](../../godot_csharp/scenes/Awakening/map_layout.tres:1019)，L1019–L1019。

```text
   "一双拖鞋。摆得很正，鞋头朝外。"
```

## S15

**对应建议：** 声音配置声明附加音效。1 处源码、0 处资源。

### CSH-022

**范围与类型：** `production` / `logic`。**索引说明：** 铜口之声每段首次开口触发盖片与管道共鸣音。

**标记位置：** [godot_csharp/scenes/UI/Panels/Dialogue/ChapterDialogue.gd:555](../../godot_csharp/scenes/UI/Panels/Dialogue/ChapterDialogue.gd:555)。

**原文位置：** [godot_csharp/scenes/UI/Panels/Dialogue/ChapterDialogue.gd](../../godot_csharp/scenes/UI/Panels/Dialogue/ChapterDialogue.gd:556)，L556–L567。

```gdscript
	if current_voice_stream == VOICE_TUBE and _previous_voice_stream != VOICE_TUBE:
		Sound.sfx("tube", -6.0)
	_previous_voice_stream = current_voice_stream
	last_voice_char_index = -1
	history.append(_escape_rich_text(speaker.text) + "\n" + body.text)
	line_shown.emit(line.character)
	hint.hide()
	_update_continue_hint()
	for response in line.responses:
		# Conditional story topics remain hidden until learned. Explicit locks carry a hint.
		if not response.is_allowed and response.lock_hint.is_empty():
			continue
```

## S16

**对应建议：** 按作用域结束章节规则。1 处源码、0 处资源。

### AWK-060

**范围与类型：** `production` / `logic`。**索引说明：** 离开生活翼时清除本章旧物牵引习惯，避免它压过后续日常。

**标记位置：** [godot_csharp/scenes/Awakening/Night/MorningScriptWorld.cs:51](../../godot_csharp/scenes/Awakening/Night/MorningScriptWorld.cs:51)。

**原文位置：** [godot_csharp/scenes/Awakening/Night/MorningScriptWorld.cs](../../godot_csharp/scenes/Awakening/Night/MorningScriptWorld.cs:52)，L52–L63。

```csharp
    public LiveChapterWorld ExportContinuation(ChapterSession progress)
    {
        if (_continuationExported) throw new InvalidOperationException("This live chapter world has already been handed off.");
        if (Enum.GetValues<Person>().Any(p => StoryBusy(p) ||
            _runtime.KnownProjection(_souls[p],_bodies[p]) is { } self && _session.IsActing(self)))
            throw new InvalidOperationException("Finish pending chapter actions before handing the live world to the next chapter.");
        StopEcology();
        // 离开生活翼：拿不拿那件旧物的打算系在本章的物件与场所上，到此失效（本人内省得知，不删记忆、不改钩与欲望）。
        // 否则其中“守着手头的事”等常数偏好会带进下一章，压过那里的一切日常。
        foreach (var who in _minds.Keys)
        {
            var habit = _minds[who].Habits;
```

## S17

**对应建议：** 整理测试夹具与固定路线采集器。32 处源码、0 处资源。

### AWK-045

**范围与类型：** `fixture` / `schema`。**索引说明：** 穷举检查内的真值组合、逐项反证表和答案是验证夹具数据。

**标记位置：** [godot_csharp/scenes/Awakening/check/ReplayCheck/ReplayCheckProgram.cs:19](../../godot_csharp/scenes/Awakening/check/ReplayCheck/ReplayCheckProgram.cs:19)。

**原文位置：** [godot_csharp/scenes/Awakening/check/ReplayCheck/ReplayCheckProgram.cs](../../godot_csharp/scenes/Awakening/check/ReplayCheck/ReplayCheckProgram.cs:20)，L20–L31。

```csharp
var truth = new NightVariables(SuPlace.OwnRoom, HuoPlace.ByTube, VoiceOrigin.Tube, VoiceWords.BoltKeepShut, DetectiveHeeds.Huo, Acquaintance.Familiar);
string Short(NightVariables v) => string.Join(" ",
    v.V1 switch { SuPlace.OwnRoom => "苏泠在房", SuPlace.Hall => "苏泠在厅", _ => "苏泠在备餐间" },
    v.V2 switch { HuoPlace.ByTube => "霍铎在铜口", HuoPlace.OwnRoom => "霍铎在房", _ => "霍铎在门厅" },
    v.V3 == VoiceOrigin.Tube ? "经铜口" : "在门外",
    v.V4 == VoiceWords.BoltKeepShut ? "闩门别开" : "开门救我",
    v.V5 switch { DetectiveHeeds.Huo => "听霍铎", DetectiveHeeds.Su => "听苏泠", _ => "听自己" },
    v.V6 == Acquaintance.Familiar ? "相熟" : "初识");

// ---------------------------------------------------------------- 1. 解空间穷举
var morning = new MorningWorld();
var realityBefore = morning.RealityFingerprint();
```

### AWK-046

**范围与类型：** `fixture` / `schema`。**索引说明：** 选角、心愿以及案件变体中的具体人物与预期答案属于验证夹具。

**标记位置：** [godot_csharp/scenes/Awakening/check/VariantCheck/Program.cs:8](../../godot_csharp/scenes/Awakening/check/VariantCheck/Program.cs:8)。

**原文位置：** [godot_csharp/scenes/Awakening/check/VariantCheck/Program.cs](../../godot_csharp/scenes/Awakening/check/VariantCheck/Program.cs:9)，L9–L20。

```csharp
var root = Chapter1Data.Root;
var document = Chapter1CompiledDialogue.Create();

// ---- 开局人选：十六人各有开场，侦探可选，其余待验收 ----
var openings = root.GetProperty("OPENINGS").EnumerateArray().ToArray();
Check(openings.Length == 16, "sixteen people can be chosen at the start (pending acceptance except the detective)");
Check(openings.All(o => document.Entries.ContainsKey(o.GetProperty("scene").GetString()!)), "every opening monologue exists as a compiled scene");
Check(openings.Count(o => o.GetProperty("selectable").GetBoolean()) == 1 && openings.Single(o => o.GetProperty("selectable").GetBoolean()).GetProperty("person").GetString() == "侦探",
    "only the detective is selectable until the others pass acceptance");
Check(openings.Where(o => o.GetProperty("person").GetString() != "侦探").All(o =>
    document.Entries[o.GetProperty("scene").GetString()!].Any(i => i.Op == "line" && i.Text.Contains("只记得上船"))), "every opening keeps the shared skeleton: only the boarding is remembered");

```

### AWK-047

**范围与类型：** `fixture` / `schema`。**索引说明：** 具体人物、证物与预期认知/言语结果仅用于自动测试。

**标记位置：** [godot_csharp/scenes/Awakening/check/CognitionCheck/Program.cs:45](../../godot_csharp/scenes/Awakening/check/CognitionCheck/Program.cs:45)。

**原文位置：** [godot_csharp/scenes/Awakening/check/CognitionCheck/Program.cs](../../godot_csharp/scenes/Awakening/check/CognitionCheck/Program.cs:46)，L46–L57。

```csharp
var baseline = Answers();
foreach (var (who, answer) in baseline) Console.WriteLine($"  默认：{who} → {answer.Act}（把握 {answer.Credence}）");
Check(new[] { "shen", "su" }.All(who => baseline[who].Credence is > 50 and < 100), "testimony alone moves Shen and Su towards the claim without certainty");
Check(baseline["huo"].Credence == 100, "Huo, who has his own slate before him, is certain: its second line only fits long acquaintance");

// ---- 证据：亲眼看过杯子的人更有把握 ----
var cups = Answers(shenSawCups: true);
Check(cups["shen"].Credence > baseline["shen"].Credence, "the companion who examined the cups is surer than without");
Check(cups["huo"] == baseline["huo"] && cups["su"] == baseline["su"], "one person's examination does not change others' answers");

// ---- 在场即可见：不在场的人认知不变 ----
{
```

### AWK-048

**范围与类型：** `fixture` / `schema`。**索引说明：** 声路和证物破坏检查中的具体状态及答案是自动测试夹具。

**标记位置：** [godot_csharp/scenes/Awakening/check/LawCheck/Program.cs:7](../../godot_csharp/scenes/Awakening/check/LawCheck/Program.cs:7)。

**原文位置：** [godot_csharp/scenes/Awakening/check/LawCheck/Program.cs](../../godot_csharp/scenes/Awakening/check/LawCheck/Program.cs:8)，L8–L19。

```csharp
var truth = new NightVariables(SuPlace.OwnRoom, HuoPlace.ByTube, VoiceOrigin.Tube, VoiceWords.BoltKeepShut, DetectiveHeeds.Huo, Acquaintance.Familiar);
var acoustics = Chapter1Data.Root.GetProperty("ACOUSTICS").GetProperty("hearing");

// ---- 声学法则与编译出的声路表一致 ----
foreach (var origin in Enum.GetValues<VoiceOrigin>())
    foreach (var room in Enum.GetValues<Room>())
    {
        var rows = acoustics.GetProperty(origin.ToString());
        var expected = rows.TryGetProperty(Names.RoomKeys[(int)room], out var row) ? row.GetString() : rows.GetProperty("*").GetString();
        Check(Acoustics.Hear(origin, room).ToString() == expected, $"law equals table: {origin} at {room}");
    }

```

### AWK-049

**范围与类型：** `fixture` / `schema`。**索引说明：** 预写角色、剧情分支与固定台词断言属于自动测试夹具。

**标记位置：** [godot_csharp/scenes/Awakening/check/StoryPacketCheck/Program.cs:41](../../godot_csharp/scenes/Awakening/check/StoryPacketCheck/Program.cs:41)。

**原文位置：** [godot_csharp/scenes/Awakening/check/StoryPacketCheck/Program.cs](../../godot_csharp/scenes/Awakening/check/StoryPacketCheck/Program.cs:42)，L42–L53。

```csharp
var document = Chapter1CompiledDialogue.Create();
Check(document.Entries.Count > 0, "real generated dialogue is present");
var fileDocument = CompiledDialogueDocument.Parse(File.ReadAllText("godot_csharp/scenes/Awakening/Script/generated_awakening_dialogue.json"));
Check(JsonSerializer.Serialize(document) == JsonSerializer.Serialize(fileDocument), "compiled dialogue matches complete generated production document");

var chapter = new Chapter1Script();
var world = chapter.CreateWorld();
var content = chapter.CreateContent();
var dialogue = chapter.CreateDialogue(content, world);
var people = Enum.GetValues<Person>();
Check(people.All(person => world.DesireStrength(person) == 1), "opening starts with original desire strength");
var reward = dialogue.Start("s3_broadcast");
```

### AWK-050

**范围与类型：** `fixture` / `schema`。**索引说明：** 模拟会话中的角色站位、E1 检视与固定状态跳转属于测试夹具。

**标记位置：** [godot_csharp/scenes/Awakening/check/SessionCheck/SessionCheckProgram.cs:8](../../godot_csharp/scenes/Awakening/check/SessionCheck/SessionCheckProgram.cs:8)。

**原文位置：** [godot_csharp/scenes/Awakening/check/SessionCheck/SessionCheckProgram.cs](../../godot_csharp/scenes/Awakening/check/SessionCheck/SessionCheckProgram.cs:9)，L9–L20。

```csharp
var session = new Chapter1Session();
var rooms = new Dictionary<string, string> { ["detective"] = "R05", ["huo"] = "R06", ["shen"] = "R07", ["su"] = "R08" };
var lines = new List<string>();
var kinds = new List<string>();
string modal = "";
Ev? openModal = null;
Ev? snapshot = null;

Ev Context(bool active = true) => new()
{
    ["field_active"] = active, ["modal"] = modal,
    ["actors"] = rooms.ToDictionary(p => p.Key, p => (object?)new Ev
```

### AWK-051

**范围与类型：** `tool` / `schema`。**索引说明：** 自动模拟器含固定侦探输入/空间采样与选项策略，仅作工具模拟。

**标记位置：** [godot_csharp/scenes/Awakening/check/DetectiveSimulation/Program.cs:94](../../godot_csharp/scenes/Awakening/check/DetectiveSimulation/Program.cs:94)。

**原文位置：** [godot_csharp/scenes/Awakening/check/DetectiveSimulation/Program.cs](../../godot_csharp/scenes/Awakening/check/DetectiveSimulation/Program.cs:95)，L95–L106。

```csharp
var startMinutes = root.GetProperty("DAY_CLOCK").GetProperty("start_minutes").GetInt32();
double? stopMinutes;
string runSource;
if (durationArgument is not null) { stopMinutes = startMinutes + RunDuration.Parse(durationArgument); runSource = "外部参数 --duration " + durationArgument; }
else if (root.TryGetProperty("RUN", out var run))
{
    var declared = run.GetProperty("duration_minutes");
    stopMinutes = declared.ValueKind == JsonValueKind.Null ? null : startMinutes + declared.GetInt32();
    runSource = "剧本的运行语句";
}
else throw new ArgumentException("剧本没有“运行”语句时，须用 --duration 指定时长（可写“到结局”）。");
if (stopMinutes is not null && double.IsNaN(stopMinutes.Value)) stopMinutes = null;
```

### AWK-052

**范围与类型：** `tool` / `schema`。**索引说明：** 续篇模拟器按公开菜单、空间与信息制定固定探索策略；未读取正确答案或自动通关。

**标记位置：** [godot_csharp/scenes/Awakening/check/DetectiveSimulation/ContinuationSimulation.cs:12](../../godot_csharp/scenes/Awakening/check/DetectiveSimulation/ContinuationSimulation.cs:12)。

**原文位置：** [godot_csharp/scenes/Awakening/check/DetectiveSimulation/ContinuationSimulation.cs](../../godot_csharp/scenes/Awakening/check/DetectiveSimulation/ContinuationSimulation.cs:13)，L13–L24。

```csharp
    public static Outcome Run(MorningWorld morning, ChapterSession progress, int seed, double handoffGameMinutes,
        double? stopGameMinutes, Action<double, double, int, C.Slot, string, string> record)
    {
        var live = morning.ExportContinuation(progress);
        var json = C.Chapter2Design.Json;
        var game = new C.Game(C.Chapter2AuthoredProfiles.Apply(json), live,
            WorldScriptDocument.Parse(json, allowChapterAdapter: true).Single());
        var session = new C.Chapter2InteractionSession(game, json);
        var random = new Random(seed);
        var baseline = game.Now;
        using var authoredTime = System.Text.Json.JsonDocument.Parse(json);
        var clock = authoredTime.RootElement.GetProperty("chapters")[0].GetProperty("design").GetProperty("time");
```

### AWK-053

**范围与类型：** `fixture` / `schema`。**索引说明：** 流程回归中的具体台词/场景/答案约束只作 fixture 断言。

**标记位置：** [godot_csharp/scenes/Awakening/check/flow_check.gd:10](../../godot_csharp/scenes/Awakening/check/flow_check.gd:10)。

**原文位置：** [godot_csharp/scenes/Awakening/check/flow_check.gd](../../godot_csharp/scenes/Awakening/check/flow_check.gd:11)，L11–L22。

```gdscript
const FORBIDDEN := ["第九", "第 9", "第9", "第十条", "第 10", "九、", "十、", "尽忘", "归乡", "放逐", "十五名", "担保耗尽"]
## 第五轮修订删去的说法：可数的刻痕、恶魔自报为恶魔或提导师与契约、门外是“主楼走廊”。
const RETIRED := ["二十四", "刻痕", "恶魔亦可", "导师将我拘于", "契约第", "主楼走廊", "依第二条", "第 8 条", "第八条"]
var checks := 0
var failures := 0
var failed_names: Array[String] = []
var fx: Fixture
var host: Node

func _initialize() -> void:
	call_deferred("_run")

```

### AWK-054

**范围与类型：** `fixture` / `schema`。**索引说明：** 诊断画廊注入案件状态并按固定菜单推进，只作 UI 回归夹具。

**标记位置：** [godot_csharp/scenes/Awakening/check/state_gallery.gd:17](../../godot_csharp/scenes/Awakening/check/state_gallery.gd:17)。

**原文位置：** [godot_csharp/scenes/Awakening/check/state_gallery.gd](../../godot_csharp/scenes/Awakening/check/state_gallery.gd:18)，L18–L29。

```gdscript
func _initialize() -> void:
	fx = Fixture.new(self)
	out_dir = ProjectSettings.globalize_path("res://").path_join("../artifacts/remake-belief-replay/gallery").simplify_path()
	DirAccess.make_dir_recursive_absolute(out_dir)
	call_deferred("_run")

func check(ok: bool, message: String) -> void:
	print(("PASS " if ok else "FAIL ") + message)
	if not ok: failures += 1

func _finish() -> void:
	# 释放 _run 的局部引用后再删宿主；界面与 C# 章节会话的排队删除要在退出前完成。
```

### AWK-056

**范围与类型：** `fixture` / `schema`。**索引说明：** E11 固定人物、证物顺序与结论只作专项回归夹具。

**标记位置：** [godot_csharp/scenes/Awakening/check/cabinet_fog_current_check.gd:3](../../godot_csharp/scenes/Awakening/check/cabinet_fog_current_check.gd:3)。

**原文位置：** [godot_csharp/scenes/Awakening/check/cabinet_fog_current_check.gd](../../godot_csharp/scenes/Awakening/check/cabinet_fog_current_check.gd:4)，L4–L15。

```gdscript
const OUT := "C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/remake-chapter2/shared-runtime/"
var checks := 0
var failures := 0
var game: Node
func _initialize() -> void: run.call_deferred()
func check(ok: bool, label: String) -> void:
	checks += 1
	print("PASS " if ok else "FAIL ", label)
	if not ok:
		failures += 1
		var f := FileAccess.open("C:/Users/iii/Desktop/dual_ontology_world_engine/tmp/cabinet-fog-verification-round2/issue.txt", FileAccess.WRITE)
		f.store_string("Cabinet automatic regression failed: " + label + "\nRepro: run res://scenes/Awakening/check/cabinet_fog_current_check.gd; current-cabinet.log records preceding checks.\n")
```

### AWK-057

**范围与类型：** `fixture` / `schema`。**索引说明：** E11 人物、证物和结果断言只作 UI 回归夹具。

**标记位置：** [godot_csharp/scenes/Awakening/check/hand_outline_check.gd:17](../../godot_csharp/scenes/Awakening/check/hand_outline_check.gd:17)。

**原文位置：** [godot_csharp/scenes/Awakening/check/hand_outline_check.gd](../../godot_csharp/scenes/Awakening/check/hand_outline_check.gd:18)，L18–L29。

```gdscript
func run() -> void:
	var holder: Node = load("res://scenes/Main2D.tscn").instantiate()
	var game: Node = holder.get_node("Awakening")
	game.set_meta("skip_intro", true)
	root.add_child(holder)
	for i in 8: await process_frame
	game.jump_to("investigate")
	var content: Dictionary = game.engine.Content()
	check(content.EVIDENCE.E11.experiment == "比对手掌轮廓", "formal assembly E11 experiment caption")
	check(content.HELP_NOTES.E11 == "在痕迹旁逐人比对过手掌轮廓：半枚手印，比四个人的手掌都窄。", "formal assembly HELP_NOTES individual side comparison and preserved size conclusion")
	var document: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://scenes/Awakening/Script/generated_awakening_dialogue.json"))
	var story := JSON.stringify(document.entries)
```

### CH2-002

**范围与类型：** `fixture` / `logic`。**索引说明：** 自动推演目录预写具体策略、时间表、角色与议事选择，仅属开发夹具。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Bots.cs:26](../../godot_csharp/scenes/Chapter2/Prototype/Bots.cs:26)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Bots.cs](../../godot_csharp/scenes/Chapter2/Prototype/Bots.cs:27)，L27–L38。

```csharp
public static class Bots
{
    private static PlayerAction A(Cmd c) => new(c);
    // 规矩卡（主语、条件、行为、例外、记账）：条件 4 为“白天”。
    public static readonly Card RuleA = new(1, 2, 0, 2, 1);     // 值守者：有人急病时，取一份口粮；次日补记者不算私取；记入台账
    public static readonly Card RuleB = new(1, 2, 0, 3, 3);     // 值守者：有人急病时，取一份口粮；两人在场者不在此限；两人见证签名
    public static readonly Card Pact = new(0, 4, 8, 4, 3);      // 任何人：白天，名录库须本人亲取、两人见证；入夜一律不行；两人见证签名
    public static readonly Card PactLoose = new(0, 4, 8, 3, 3); // 同上，但不禁夜（上了钩的人不会否决）
    public static readonly Card BrandQuer = new(10 + (int)P.Quer, 1, 9, 0, 1); // 雀儿：入夜后，不得入后勤院；无例外；记入台账
    public static readonly Card Committee = new(0, 4, 6, 3, 3); // 任何人：白天，名录库由委员会保管；两人在场者不在此限；两人见证签名
    public static readonly Card Seal = new(0, 0, 7, 0, 0);      // 任何人：任何时候，名录库封闭不启
    public static readonly Card Open = new(0, 0, 5, 0, 1);      // 任何人：任何时候，名录库各取其物；记入台账
```

### CH2-016

**范围与类型：** `tool` / `logic`。**索引说明：** 自动流程预写具体地图出生点、角色、案件操作顺序和终局检查。

**标记位置：** [godot_csharp/scenes/Chapter2/check/check_flow.gd:7](../../godot_csharp/scenes/Chapter2/check/check_flow.gd:7)。

**原文位置：** [godot_csharp/scenes/Chapter2/check/check_flow.gd](../../godot_csharp/scenes/Chapter2/check/check_flow.gd:8)，L8–L19。

```gdscript
const SPAWN_OF := {
	"central_hall": "south", "lecture": "from_central", "logistics": "west", "granary": "south", "clinic": "south", "greenhouse": "south",
	"archive": "south", "campus_s": "gate_inside", "living_hall": "north_door", "zone_yi": "from_hall", "zone_jia": "from_hall",
	"zone_bing": "from_hall", "zone_ding": "from_hall", "resonance": "south", "counterweight": "from_stairs",
}
var checks := 0
var failures := 0
var failed_names: Array[String] = []
var fx: Fixture
var host: Node
var planned_pairs: Array = []
var planned_pair_index := 0
```

### CH2-017

**范围与类型：** `tool` / `logic`。**索引说明：** 自动验收按预定证词、证据、支持与分支策略代选。

**标记位置：** [godot_csharp/scenes/Chapter2/check/check_flow.gd:50](../../godot_csharp/scenes/Chapter2/check/check_flow.gd:50)。

**原文位置：** [godot_csharp/scenes/Chapter2/check/check_flow.gd](../../godot_csharp/scenes/Chapter2/check/check_flow.gd:51)，L51–L62。

```gdscript
func planned_choice(options: Array) -> String:
	var available: Array = options.filter(func(option): return bool(option.get("available", true)))
	if available.is_empty(): return ""
	var first_id := str(available[0].id)
	if first_id.begins_with("claim:") or first_id.begins_with("template:") or first_id.begins_with("evidence:"):
		var is_evidence := first_id.begins_with("evidence:")
		var wanted := "pause"
		if planned_pair_index < planned_pairs.size():
			var pair: Array = planned_pairs[planned_pair_index]
			if is_evidence:
				wanted = "evidence:" + str(pair[1])
			else:
```

### CH2-018

**范围与类型：** `tool` / `logic`。**索引说明：** 测试构造章一继承状态并预置姓名、答案和章二交接。

**标记位置：** [godot_csharp/scenes/Chapter2/check/check_flow.gd:76](../../godot_csharp/scenes/Chapter2/check/check_flow.gd:76)。

**原文位置：** [godot_csharp/scenes/Chapter2/check/check_flow.gd](../../godot_csharp/scenes/Chapter2/check/check_flow.gd:77)，L77–L88。

```gdscript
func handoff() -> bool:
	host = await fx.prepare()
	fx.set_progress("state", "detective_name", "flow2-fixture")
	await fx.jump("answer_done")
	# answer_done 保留已答完的进度但禁用行走；恢复调查阶段后才可从现场打开菜单。
	await fx.jump("investigate")
	var ending_started := Time.get_unix_time_from_system()
	if not await send({"kind": "interact", "site": "E8"}): return false
	if not check(host.ui.modal_kind == "menu" and fx.select("answer"), "章一调试答题完成后从正式菜单进入终局"): return false
	if not check(await fx.settle(false, 60.0, func(): return fx.ended() and host.ui.modal_kind == "ending"), "章一开门演出在有限时间内完成"): return false
	if not check(fx.ended() and fx.progress("flags").has("door_open") and host.ui.modal_kind == "ending", "章一门开、结束状态与章末卡同时成立"): return false
	check(fx.map().get_node("ObjectsAndPeople/MechanicalIsolationDoor/DoorGap").visible, "章一隔离门实际切换为开启")
```

### CH2-019

**范围与类型：** `tool` / `logic`。**索引说明：** 测试硬编码第二章时间推进、规则转述、特定人物问话、证物和议事流程。

**标记位置：** [godot_csharp/scenes/Chapter2/check/check_flow.gd:122](../../godot_csharp/scenes/Chapter2/check/check_flow.gd:122)。

**原文位置：** [godot_csharp/scenes/Chapter2/check/check_flow.gd](../../godot_csharp/scenes/Chapter2/check/check_flow.gd:123)，L123–L134。

```gdscript
func _run() -> void:
	var t0 := Time.get_ticks_msec()
	fx = Fixture.new(self)
	fx.on_error = func(message: String) -> void: check(false, "夹具：" + message)
	fx.choose = planned_choice
	if not await handoff():
		await _finish()
		return
	# 保留多地图出生点检查；检查换图后恢复讲堂，不再第二次发送 begin。
	for key in ["zone_yi", "living_hall", "zone_jia", "zone_bing", "zone_ding", "central_hall", "lecture", "logistics", "granary", "clinic", "archive", "counterweight", "resonance", "greenhouse", "campus_s", "campus_n", "campus_sw"]:
		var t := Time.get_ticks_msec()
		if not await move_to_map(key):
```

### CH2-020

**范围与类型：** `fixture` / `logic`。**索引说明：** 夹具自动填写固定侦探名、代按对白与菜单并跳过回放。

**标记位置：** [godot_csharp/scenes/Chapter2/check/chapter2_fixture.gd:46](../../godot_csharp/scenes/Chapter2/check/chapter2_fixture.gd:46)。

**原文位置：** [godot_csharp/scenes/Chapter2/check/chapter2_fixture.gd](../../godot_csharp/scenes/Chapter2/check/chapter2_fixture.gd:47)，L47–L58。

```gdscript
func settle(resolve_choices := false, seconds := 60.0, until := Callable()) -> bool:
	var deadline := Time.get_ticks_msec() + int(seconds * 1000.0)
	var idle_since := -1
	while Time.get_ticks_msec() < deadline:
		await tree.process_frame
		if not is_instance_valid(host.ui) or not is_instance_valid(host.field):
			on_error.call("Main 缺少 UI 或现场")
			return false
		_observe()
		if host.ui.modal_kind == "form" and str(host.ui._modal_event.get("fields", [{}])[0].get("id", "")) == "name":
			name_form_answer("flow2-fixture")
		elif host.ui.dialogue.active:
```

### CH2-021

**范围与类型：** `tool` / `logic`。**索引说明：** 画廊脚本注入章一继承状态并预写第二章场景、行动与日期路线。

**标记位置：** [godot_csharp/scenes/Chapter2/Game/check_gallery.gd:43](../../godot_csharp/scenes/Chapter2/Game/check_gallery.gd:43)。

**原文位置：** [godot_csharp/scenes/Chapter2/Game/check_gallery.gd](../../godot_csharp/scenes/Chapter2/Game/check_gallery.gd:44)，L44–L55。

```gdscript
func _run() -> void:
	# Inject a real chapter-one handoff for this standalone automatic fixture.
	# This is not a completed playthrough or a JSON reconstruction.
	var first: Node = preload("res://scenes/Awakening/AwakeningEngine.cs").new()
	root.add_child(first)
	first.ChapterEnd([], false, false, false)
	game = (load("res://scenes/Chapter2/Game/Chapter2Game.tscn") as PackedScene).instantiate()
	game.set_meta("skip_intro", true)
	root.add_child(game)
	await process_frame
	if game.startup_error != "":
		push_error("Gallery chapter startup failed: " + game.startup_error)
```

### CH2-022

**范围与类型：** `tool` / `logic`。**索引说明：** 自动检查中的预写章节性断言、推演路线和游戏结局策略。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Checks.cs:8](../../godot_csharp/scenes/Chapter2/Prototype/Checks.cs:8)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Checks.cs](../../godot_csharp/scenes/Chapter2/Prototype/Checks.cs:9)，L9–L20。

```csharp
public static class Checks
{
    private static int _fail, _total;
    private static void Check(bool ok, string what)
    {
        _total++;
        if (!ok) _fail++;
        Console.WriteLine($"[{(ok ? "通过" : "失败")}] {what}");
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern IntPtr GetCurrentThread();
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern bool GetThreadTimes(IntPtr h, out long c, out long e, out long k, out long u);
```

### CH2-077

**范围与类型：** `tool` / `logic`。**索引说明：** 宿主冒烟流程固定演示本章查案、对质与议事。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Program.cs:40](../../godot_csharp/scenes/Chapter2/Prototype/Program.cs:40)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Program.cs](../../godot_csharp/scenes/Chapter2/Prototype/Program.cs:41)，L41–L52。

```csharp
            case "host":
            {
                // 宿主路径冒烟：与 Godot 第二章同样的调用顺序（读不耗时，写/等待经 RunSlot）。
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var g = CreateGame();
                void Run(PlayerAction? act) { var t0 = sw.ElapsedMilliseconds; var d = g.Day; var s = g.Slot; g.RunSlot(act); Console.WriteLine($"-- D{d} {Names.Of(s)} ({sw.ElapsedMilliseconds - t0} ms)"); foreach (var l in g.TakeFeed()) Console.WriteLine("   " + l); }
                Run(new PlayerAction(Cmd.Stay) { Place = Place.Hall });
                Run(new PlayerAction(Cmd.Class));
                Console.WriteLine(g.HostExamine("配给台公告"));
                Run(new PlayerAction(Cmd.Collect));
                Run(new PlayerAction(Cmd.Stay) { Place = Place.Logistics });
                Run(new PlayerAction(Cmd.Collect));
```

### CSH-007

**范围与类型：** `fixture` / `logic`。**索引说明：** UI 检查定位第一章领取柜并跟踪固定角色的领取动画。

**标记位置：** [godot_csharp/tools/verify_ch1_locker_ui.gd:159](../../godot_csharp/tools/verify_ch1_locker_ui.gd:159)。

**原文位置：** [godot_csharp/tools/verify_ch1_locker_ui.gd](../../godot_csharp/tools/verify_ch1_locker_ui.gd:160)，L160–L163。

```gdscript
	var anchor: Vector2 = fx.map().anchor("领取柜")
	record("geometry", {"anchor": [anchor.x, anchor.y], "furniture": fx.map().layout.furniture})
	var deadline := Time.get_ticks_msec() + 150000
	var source_line := ""
```

### CSH-010

**范围与类型：** `fixture` / `logic`。**索引说明：** 自动脚本执行第一章固定调查、重演、始业问答与结尾检查，并预置角色回答。

**标记位置：** [godot_csharp/scenes/Shared/check/chapter1_smoke.gd:30](../../godot_csharp/scenes/Shared/check/chapter1_smoke.gd:30)。

**原文位置：** [godot_csharp/scenes/Shared/check/chapter1_smoke.gd](../../godot_csharp/scenes/Shared/check/chapter1_smoke.gd:31)，L31–L42。

```gdscript
func _run() -> void:
	fx = Fixture.new(self)
	host = await fx.fresh(false)
	auto = Autoplay.new(host)
	var field: Node2D = host.field
	check(await settle(func(): return stage() == "roam" and not host.ui.is_busy()), "醒来后可以走动（%d 句）" % auto.lines.size())
	check(field.player != null and field.player.seated, "侦探醒在床上")
	# 走进活动厅：相遇、报名、公告框、核验报偿，直到进入调查。
	field.player.stand_up()
	field.player.position = field.adapter.anchor("meeting:Detective") + Vector2(0, 40)
	var seen := {"board": false}
	var reached := await settle(func():
```

### CSH-011

**范围与类型：** `tool` / `logic`。**索引说明：** 玩家记录器绑定第一章与学园地点、人物、调查路线及条件分支。

**标记位置：** [godot_csharp/tools/PlayerTranscript/Program.cs:14](../../godot_csharp/tools/PlayerTranscript/Program.cs:14)。

**原文位置：** [godot_csharp/tools/PlayerTranscript/Program.cs](../../godot_csharp/tools/PlayerTranscript/Program.cs:15)，L15–L26。

```csharp
sealed class Transcript
{
    readonly string root;
    readonly GameSession game;
    readonly JsonDocument layout, content;
    readonly IReadOnlyList<Chapter2AuthoredSite> campusSites;
    readonly StreamWriter log;
    readonly StreamWriter raw;
    readonly Queue<Ev> events = new();
    readonly Dictionary<string, (double X, double Y)> actors = new();
    readonly Dictionary<string, string> propStates = new();
    readonly Dictionary<string, string> stateText = new();
```

### CSH-012

**范围与类型：** `tool` / `logic`。**索引说明：** 预设第一章作答文本/物证映射及第五日学园议事选择策略。

**标记位置：** [godot_csharp/tools/PlayerTranscript/Program.cs:132](../../godot_csharp/tools/PlayerTranscript/Program.cs:132)。

**原文位置：** [godot_csharp/tools/PlayerTranscript/Program.cs](../../godot_csharp/tools/PlayerTranscript/Program.cs:133)，L133–L144。

```csharp
    void Respond(Ev ev)
    {
        string kind=S(ev,"kind"), pid=S(ev,"presentation_id");
        if(superseded.Contains(pid))return;
        if(kind=="error") throw new InvalidOperationException(S(ev,"message"));
        if(kind=="scene") map=S(ev,"map");
        if(kind=="stage")
        {
            var op=S(ev,"op");var who=S(ev,"who");
            if(op is "place" or "walk") actors[who]=Resolve(M(ev,"to"));
            if(op=="daily_go")
            {
```

### CSH-013

**范围与类型：** `tool` / `logic`。**索引说明：** 固定执行第一章检视顺序与连续五日路线以生成语义记录。

**标记位置：** [godot_csharp/tools/PlayerTranscript/Program.cs:396](../../godot_csharp/tools/PlayerTranscript/Program.cs:396)。

**原文位置：** [godot_csharp/tools/PlayerTranscript/Program.cs](../../godot_csharp/tools/PlayerTranscript/Program.cs:397)，L397–L408。

```csharp
    public void Run()
    {
        try
        {
            Header();
            Input("开始",E(("kind","start")));
            Input("场景就绪；正常醒来",E(("kind","view_ready")));
            actors["detective"]=(480,300);
            Input("进入活动厅相遇",E(("kind","tick"),("delta",.1)));
            C1Site("E12");
            foreach(var id in new[]{"E1","E2","E3","E4","E5","E6","E7","E8j","E9"})
                C1Site(id,id is "E1" or "E2" or "E3" or "E8j"?"experiment":id is "E4" or "E5" or "E6" or "E7" or "E9"?"help":"");
```

### CSH-015

**范围与类型：** `fixture` / `text`。**索引说明：** UI测试自带的案件问答样例，不视为正式剧情规则。

**标记位置：** [godot_csharp/scenes/Shared/check/ui_modules_check.gd:183](../../godot_csharp/scenes/Shared/check/ui_modules_check.gd:183)。

**原文位置：** [godot_csharp/scenes/Shared/check/ui_modules_check.gd](../../godot_csharp/scenes/Shared/check/ui_modules_check.gd:184)，L184–L195。

```gdscript
	form.show_data({"header": {"question": "提问者问：[b]谁关了灯？[/b]", "instructions": "陈述一句，再出示一件。"}, "layout": "tabs", "unset_label": "（未选）",
		"summary": [{"field": "say", "label": "陈述"}, {"field": "show", "label": "出示"}],
		"fields": [
			{"id": "say", "kind": "choice", "options": [{"id": "a", "label": "结论甲"}, {"id": "b", "label": "结论乙"}]},
			{"id": "show", "kind": "choice", "options": [], "empty_text": "还没有可出示的东西。"},
		]})
	ok(form.get_node("Header").visible and form.get_node("Header/Question").visible, "FormPanel header pinned")
	var summary: RichTextLabel = form.get_node("Header/Summary")
	ok(summary.text == "陈述：（未选）\n出示：（未选）", "FormPanel summary shows unset labels")
	ok(form.get_node("Fields").get_child(0).visible and not form.get_node("Fields").get_child(1).visible, "FormPanel tabs show first group only")
	form.show_field("show")
	ok(not form.get_node("Fields").get_child(0).visible and form.get_node("Fields").get_child(1).visible, "FormPanel show_field swaps group")
```

### CSH-016

**范围与类型：** `fixture` / `text`。**索引说明：** UI测试自带的具体调查样例文本，未证明与正式案件相同。

**标记位置：** [godot_csharp/scenes/Shared/check/ui_modules_check.gd:210](../../godot_csharp/scenes/Shared/check/ui_modules_check.gd:210)。

**原文位置：** [godot_csharp/scenes/Shared/check/ui_modules_check.gd](../../godot_csharp/scenes/Shared/check/ui_modules_check.gd:211)，L211–L222。

```gdscript
	notebook.show_data({"empty_text": "空", "sections": [
		{"heading": "[b]看过的[/b]", "style": "grid", "entries": [{"label": "水渍", "card_id": "E1"}, {"label": "钥匙", "card_id": "E2"}, {"label": "门锁", "card_id": "E3"}]},
		{"heading": "[b]规矩[/b]", "style": "list", "entries": [{"label": "夜里不得离开房间。"}, {"label": "（待查）", "muted": true}]},
	]})
	ok(not notebook.get_node("Empty").visible, "Notebook empty text hidden when sections exist")
	var sections: Control = notebook.get_node("Sections")
	var grid: GridContainer = null
	var list: VBoxContainer = null
	for child in sections.get_children():
		if child is GridContainer:
			grid = child
		elif child is VBoxContainer:
```

### CSH-019

**范围与类型：** `fixture` / `logic`。**索引说明：** 验证第一章E11a书架痕迹仅在进入其房间后可知。

**标记位置：** [godot_csharp/scenes/Shared/check/check_room_fog.gd:191](../../godot_csharp/scenes/Shared/check/check_room_fog.gd:191)。

**原文位置：** [godot_csharp/scenes/Shared/check/check_room_fog.gd](../../godot_csharp/scenes/Shared/check/check_room_fog.gd:192)，L192–L203。

```gdscript
		game.sight.seen_cells.clear()
		var unknown := true
		for spec in game.decals.DECALS: unknown = unknown and not game.decals._known(spec[1])
		for shelf in game.layout.shelves: unknown = unknown and not game.decals._known(Vector2(shelf[0], shelf[1]))
		check(unknown, "TraceDecals runtime unknown decals and shelf traces gated off")
		await move(game, game.site_point("E11a") + Vector2(0, 72))
		check(game.decals._known(game.site_point("E11a")), "TraceDecals shelf becomes known only after room entry")
	prop.free()
	game.sight.seen_cells.clear()
func _capture() -> void:
	for key in ["chapter1", "zone_yi"]:
		var path: String = "res://scenes/Awakening/Awakening.tscn" if key == "chapter1" else "res://scenes/Chapter2/Game/Chapter2Game.tscn"
```

### CSH-020

**范围与类型：** `fixture` / `logic`。**索引说明：** 用第二章Huo检验未探索宿舍与当前房间人物的可见性。

**标记位置：** [godot_csharp/scenes/Shared/check/check_room_fog.gd:328](../../godot_csharp/scenes/Shared/check/check_room_fog.gd:328)。

**原文位置：** [godot_csharp/scenes/Shared/check/check_room_fog.gd](../../godot_csharp/scenes/Shared/check/check_room_fog.gd:329)，L329–L340。

```gdscript
	if game.actors.is_empty(): game._make_actor("Huo")
	if not game.actors.is_empty():
		var npc: Node2D = game.actors.values()[0]
		npc.position = Vector2(1040, 112)
		game._process(0.0)
		check(not npc.visible and not game.in_sight(npc.position), "unknown dorm NPC cannot leak")
		await move(game, npc.position)
		game._process(0.0)
		check(npc.visible, "current room NPC visible")
		await move(game, Vector2(112, 368))
		game._process(0.0)
		check(not npc.visible and game.sight.terrain_known(npc.position), "known blind dorm NPC hidden")
```

### CSH-036

**范围与类型：** `fixture` / `logic`。**索引说明：** 用第一章铜口热点E8及黑板热点BOARD验证其相对领取柜的具体命中优先级。

**标记位置：** [godot_csharp/tools/verify_ch1_locker_ui.gd:233](../../godot_csharp/tools/verify_ch1_locker_ui.gd:233)。

**原文位置：** [godot_csharp/tools/verify_ch1_locker_ui.gd](../../godot_csharp/tools/verify_ch1_locker_ui.gd:234)，L234–L245。

```gdscript
	for site_id in ["E8", "BOARD"]:
		check(host.field.sites.has(site_id) and host.field.site_visible(site_id), site_id + " hotspot visible beside new furniture")
		if not host.field.sites.has(site_id): continue
		var point: Vector2 = host.field.site_box(site_id).get_center()
		if site_id == "E8":
			var locker: Sprite2D = fx.map().objects_root().get_node("RewardLocker")
			var overlap: Rect2 = host.field.site_box(site_id).grow(3).intersection(Rect2(locker.position + locker.offset, locker.texture.get_size()))
			check(overlap.has_area(), "copper hotspot and locker have an overlap to test priority")
			if overlap.has_area(): point = overlap.get_center()
		await mouse_at(point)
		var title := str(host.field.sites[site_id].title)
		var hits: Dictionary = fx.map().furniture_hits(point)
```

### CSH-037

**范围与类型：** `fixture` / `logic`。**索引说明：** 依项目奖励对白行号定位开场与空柜台词，核对固定物证阶段的位置/输入状态。

**标记位置：** [godot_csharp/tools/verify_ch1_locker_ui.gd:164](../../godot_csharp/tools/verify_ch1_locker_ui.gd:164)。

**原文位置：** [godot_csharp/tools/verify_ch1_locker_ui.gd](../../godot_csharp/tools/verify_ch1_locker_ui.gd:165)，L165–L176。

```gdscript
	var source_targets := {
		"s4_rewards.004": fx.line_text("s4_rewards.004"),
		"s4_rewards.020": fx.line_text("s4_rewards.020"),
	}
	while Time.get_ticks_msec() < deadline and fx.stage() != "investigate":
		for who in ["shen", "huo", "su", "detective"]:
			var actor: CharacterBody2D = host.field.actors[who]
			if not captures.has(who) and actor.position.distance_to(anchor) <= 0.2 and str(actor.sprite.animation) == "interact_back":
				captures[who] = true
				if who == "detective": check(not host.field.player_controlled(), "detective claim snapshot disables player control")
				await shot("claim-" + who)
				record("animation", {"who": who, "name": "interact_back", "frame_count": actor.sprite.sprite_frames.get_frame_count("interact_back"), "fps": actor.sprite.sprite_frames.get_animation_speed("interact_back")})
```

## S18

**对应建议：** UI 回放与采样器消费正式绑定。5 处源码、0 处资源。

### CSH-006

**范围与类型：** `tool` / `logic`。**索引说明：** 采集器按正式开场的“开始游戏”与“如何称呼你”剧情节点推进。

**标记位置：** [godot_csharp/tools/capture_ui_start.gd:9](../../godot_csharp/tools/capture_ui_start.gd:9)。

**原文位置：** [godot_csharp/tools/capture_ui_start.gd](../../godot_csharp/tools/capture_ui_start.gd:10)，L10–L21。

```gdscript
func _run() -> void:
	capture_viewport = SubViewport.new()
	capture_viewport.size = Vector2i(1280, 992)
	capture_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(capture_viewport)
	var packed: PackedScene = load("res://scenes/Host/Main.tscn")
	if packed == null:
		push_error("Capture could not load the production main scene.")
		quit(2)
		return
	var scene: Node = packed.instantiate()
	capture_viewport.add_child(scene)
```

### CSH-024

**范围与类型：** `tool` / `schema`。**索引说明：** 采样清单固定选择两章对白、E1证物、防辩表、午间争执、议事支持项与结局。

**标记位置：** [godot_csharp/tools/capture_story_ui_samples.gd:8](../../godot_csharp/tools/capture_story_ui_samples.gd:8)。

**原文位置：** [godot_csharp/tools/capture_story_ui_samples.gd](../../godot_csharp/tools/capture_story_ui_samples.gd:9)，L9–L20。

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

var _round := ""
var _sample_dir := ""
```

### CSH-025

**范围与类型：** `tool` / `schema`。**索引说明：** 回放工具硬编码学园场景地图与项目地点的对应关系。

**标记位置：** [godot_csharp/tools/capture_player_transcript.gd:26](../../godot_csharp/tools/capture_player_transcript.gd:26)。

**原文位置：** [godot_csharp/tools/capture_player_transcript.gd](../../godot_csharp/tools/capture_player_transcript.gd:27)，L27–L38。

```gdscript
const HOST_MAP_PLACES := {
	"central_hall": "Hall", "lecture": "Lecture", "logistics": "Logistics", "granary": "Logistics", "clinic": "Clinic",
	"greenhouse": "Greenhouse", "archive": "ArchiveFront", "counterweight": "ArchiveFront", "campus_s": "Oath", "living_hall": "DormLife",
	"zone_yi": "DormLife", "zone_jia": "DormW", "zone_bing": "DormS", "zone_ding": "DormL", "resonance": "Resonance",
}
const DEADLINE_SECONDS := 90.0

class ReplayHost extends Node:
	var ui: Node
	var field: Node2D
	var title_layer: CanvasLayer

```

### CSH-026

**范围与类型：** `tool` / `logic`。**索引说明：** 用正式快照中的人物/地点（包括侦探及实际热点）复原场景坐标，只重放不执行剧情答案。

**标记位置：** [godot_csharp/tools/capture_player_transcript.gd:309](../../godot_csharp/tools/capture_player_transcript.gd:309)。

**原文位置：** [godot_csharp/tools/capture_player_transcript.gd](../../godot_csharp/tools/capture_player_transcript.gd:310)，L310–L321。

```gdscript
func _apply_context(input: Dictionary) -> void:
	if not is_instance_valid(host.field): return
	var context: Dictionary = input.get("context", {})
	var map_key := str(input.get("map", context.get("map", "")))
	var npc_route := _npc_map_reconstruction(input, map_key)
	if _failed: return
	if not npc_route.is_empty(): map_key = str(npc_route.target_map)
	if map_key != "" and map_key != host.field.adapter.map_key():
		if not _map_exists(map_key): return
		await host.field.execute({"kind": "scene", "map": map_key,
			"spawn": str(npc_route.get("spawn", ""))})
	if not npc_route.is_empty():
```

### CSH-027

**范围与类型：** `tool` / `schema`。**索引说明：** 可选截图阶段按E1、say字段、support选项及两场景对白识别剧情节点。

**标记位置：** [godot_csharp/tools/capture_player_transcript.gd:783](../../godot_csharp/tools/capture_player_transcript.gd:783)。

**原文位置：** [godot_csharp/tools/capture_player_transcript.gd](../../godot_csharp/tools/capture_player_transcript.gd:784)，L784–L795。

```gdscript
func _screenshot_stage(ev: Dictionary) -> String:
	# Match production event structure, never dispatch offsets or translated prose.
	var kind := str(ev.get("kind", ""))
	if kind == "ending" and source_sequence == int(packets.back().sequence):
		return "06-ending"
	if _view_resource == "living":
		if kind == "lines": return "01-wake"
		if kind == "card" and str(ev.get("id", "")) == "E1":
			return "02-living-investigation"
		if kind == "form":
			for field in ev.get("fields", []):
				if str(field.get("id", "")) == "say": return "03-confrontation"
```

## S19

**对应建议：** 示例内容外置、预览数据中性化。6 处源码、9 处资源。

### CSH-008

**范围与类型：** `example` / `text`。**索引说明：** 可执行示例定义值夜学员、值夜记录、中央厅与调查选择文案。

**标记位置：** [godot_csharp/scenes/Shared/Story/Examples/ExampleWorld.cs:7](../../godot_csharp/scenes/Shared/Story/Examples/ExampleWorld.cs:7)。

**原文位置：** [godot_csharp/scenes/Shared/Story/Examples/ExampleWorld.cs](../../godot_csharp/scenes/Shared/Story/Examples/ExampleWorld.cs:8)，L8–L19。

```csharp
public sealed class ExampleWorld : IWorldScript
{
    private ScriptCharacter _guest = null!;
    private Essence _note = null!;
    private void Initialize(Game game)
    {
        game.Set(game.Jar, T.Water, 3);
        var world = game.ScriptWorld;
        _guest = world.DefineCharacter("guest", "值夜学员", game.Space[Place.Hall],
            "res://ShareAsset/Internal/Characters/animation_v2/engineer.tres");
        _note = world.DefineObject("note", "值夜记录", game.Space[Place.Hall]);
        var unusedRoom = world.DefineSpace("side-room", "未开放的侧室");
```

### CSH-014

**范围与类型：** `preview` / `text`。**索引说明：** 编辑器笔记样例写入窗台水渍、桌钥匙、门锁和夜间离室禁令等案件事实。

**标记位置：** [godot_csharp/scenes/UI/Panels/Notebook/Notebook.gd:24](../../godot_csharp/scenes/UI/Panels/Notebook/Notebook.gd:24)。

**原文位置：** [godot_csharp/scenes/UI/Panels/Notebook/Notebook.gd](../../godot_csharp/scenes/UI/Panels/Notebook/Notebook.gd:25)，L25–L36。

```gdscript
@export var preview_sections: Array[Dictionary] = [
	{"heading": "[b]看过的[/b]", "style": "grid", "entries": [{"label": "窗台的水渍", "card_id": "E1"}, {"label": "桌上的钥匙", "card_id": "E2"}, {"label": "门锁", "card_id": "E3"}]},
	{"heading": "[b]读过、听过的规矩[/b]", "style": "list", "entries": [{"label": "夜里不得离开房间。"}, {"label": "（还没听说第二条）", "muted": true}]},
]:
	set(value):
		preview_sections = value
		_render_preview_or_data()

var _data: Dictionary = {}

func _ready() -> void:
	_render_preview_or_data()
```

### CSH-017

**范围与类型：** `example` / `logic`。**索引说明：** 回归测试实际执行侦探、守卫、内门闩及arrival/morning项目示例章节。

**标记位置：** [godot_csharp/scenes/Shared/check/WorldScriptCheck/Program.cs:4](../../godot_csharp/scenes/Shared/check/WorldScriptCheck/Program.cs:4)。

**原文位置：** [godot_csharp/scenes/Shared/check/WorldScriptCheck/Program.cs](../../godot_csharp/scenes/Shared/check/WorldScriptCheck/Program.cs:5)，L5–L16。

```csharp
var json=File.ReadAllText(args[0]);int failures=0;
Console.WriteLine("SCOPE: development world-script examples; not formal chapter migration or blind play.");
void Check(string name,Action test){try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+" "+e);}}
void Assert(bool value,string message="assertion"){if(!value)throw new Exception(message);}
void Reject(Action a){bool rejected=false;try{a();}catch{rejected=true;}Assert(rejected,"expected rejection");}
WorldSession New(){var s=new WorldSession();s.Load(WorldScriptDocument.Parse(json)[0]);return s;}
const string player="character.detective",guard="character.guard",bolt="object.inner_bolt";
AvailableCandidate[] Offer(WorldSession s)=>s.Process(new RequestCandidates(player,bolt)).OfType<CandidatesAvailable>().Single().Candidates.ToArray();
void See(WorldSession s,string who){s.Process(new VerifiedPerception(who,[bolt,player,guard,"room.foyer"]));}
Check("actual compiled chapters",()=>Assert(WorldScriptDocument.Parse(json).Select(c=>c.Id).SequenceEqual(new[]{"arrival","morning"})));
Check("malformed Int32 input rejected before chapter mutation",()=>{using var s=new WorldSession();var chapter=WorldScriptDocument.Parse(json)[0];chapter.Entities.Single(e=>e.Id==bolt).Properties["state:large"]=new(){Type="integer",Value=JsonSerializer.SerializeToElement(2147483648L)};Reject(()=>s.Load(chapter));Assert(s.LoadedChapters.Count==0);});
Check("only applicable player candidates",()=>{using var s=New();Assert(Offer(s).Length==0);See(s,player);Assert(Offer(s).Select(c=>c.Id).SequenceEqual(new[]{"arrival:inspect_inner_bolt"}));});
```

### CSH-018

**范围与类型：** `preview` / `logic`。**索引说明：** 逐步呈现并切换实际arrival/morning示例章节内容。

**标记位置：** [godot_csharp/scenes/Shared/check/WorldScriptCheck/preview_ui_check_01.gd:10](../../godot_csharp/scenes/Shared/check/WorldScriptCheck/preview_ui_check_01.gd:10)。

**原文位置：** [godot_csharp/scenes/Shared/check/WorldScriptCheck/preview_ui_check_01.gd](../../godot_csharp/scenes/Shared/check/WorldScriptCheck/preview_ui_check_01.gd:11)，L11–L22。

```gdscript
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://../artifacts/python-story-refactor/world-preview-ui"))
	host = load("res://scenes/Shared/Story/Examples/WorldScript/Host/WorldScriptPreview.tscn").instantiate()
	host.compiled_document = ProjectSettings.globalize_path("res://scenes/Shared/Story/Examples/WorldScript/generated_world.json")
	root.add_child(host)
	await process_frame
	if not host.opened:
		fail("document did not open: " + host.last_error)
		return
	for chapter in ["arrival", "morning"]:
		var refresh: Button = host.get_node("Margin/Column/Actions/Refresh")
		refresh.pressed.emit()
		await process_frame
```

### CSH-021

**范围与类型：** `preview` / `schema`。**索引说明：** 编辑器地图预览固定使用项目学园地点及其连线。

**标记位置：** [godot_csharp/scenes/UI/Panels/CampusMap/CampusMap.gd:31](../../godot_csharp/scenes/UI/Panels/CampusMap/CampusMap.gd:31)。

**原文位置：** [godot_csharp/scenes/UI/Panels/CampusMap/CampusMap.gd](../../godot_csharp/scenes/UI/Panels/CampusMap/CampusMap.gd:32)，L32–L43。

```gdscript
const PREVIEW_NODES := {
	"ArchiveFront": {"pos": Vector2(500, 70), "label": "地点1"},
	"Hall": {"pos": Vector2(500, 200), "label": "地点2"},
	"Lecture": {"pos": Vector2(300, 200), "label": "地点3"},
	"Resonance": {"pos": Vector2(120, 300), "label": "地点4"},
	"Commons": {"pos": Vector2(500, 380), "label": "地点5"},
	"DormW": {"pos": Vector2(370, 330), "label": "地点6"},
	"DormL": {"pos": Vector2(630, 330), "label": "地点7"},
	"DormS": {"pos": Vector2(370, 440), "label": "地点8"},
	"DormLife": {"pos": Vector2(630, 440), "label": "地点9"},
	"Logistics": {"pos": Vector2(820, 430), "label": "地点10"},
	"Granary": {"pos": Vector2(900, 330), "label": "地点11"},
```

### CSH-038

**范围与类型：** `preview` / `schema`。**索引说明：** 作者预览固定以侦探与内门闩控件代表编译故事示例中的实体。

**标记位置：** [godot_csharp/scenes/Shared/Story/Examples/WorldScript/Host/WorldScriptPreview.gd:15](../../godot_csharp/scenes/Shared/Story/Examples/WorldScript/Host/WorldScriptPreview.gd:15)。

**原文位置：** [godot_csharp/scenes/Shared/Story/Examples/WorldScript/Host/WorldScriptPreview.gd](../../godot_csharp/scenes/Shared/Story/Examples/WorldScript/Host/WorldScriptPreview.gd:16)，L16–L27。

```gdscript
@onready var detective: Label = $Margin/Column/RoomView/Detective
@onready var target: Button = $Margin/Column/RoomView/Bolt

var entity_names: Dictionary = {}
var opened := false
var executions: Dictionary = {}
var dialogue_queue: Array = []
var presentation: Dictionary = {}
var line_index := 0
var last_error := ""

func _ready() -> void:
```

### RES-089

**范围与类型：** `preview` / `text`。**索引说明：** 工作台预览的具体昨夜关灯问答与固定作答指引；仅为预览文本。

**原文位置：** [godot_csharp/scenes/UI/Workbench/UIWorkbench.tscn](../../godot_csharp/scenes/UI/Workbench/UIWorkbench.tscn:184)，L184–L184。

```text
preview_question = "提问者问：[b]昨夜是谁关的灯？[/b]"
```

### RES-090

**范围与类型：** `preview` / `text`。**索引说明：** 工作台预览的具体昨夜关灯问答与固定作答指引；仅为预览文本。

**原文位置：** [godot_csharp/scenes/UI/Workbench/UIWorkbench.tscn](../../godot_csharp/scenes/UI/Workbench/UIWorkbench.tscn:185)，L185–L185。

```text
preview_instructions = "陈述一句结论，再出示一件能支撑它的东西。"
```

### RES-091

**范围与类型：** `preview` / `schema`。**索引说明：** 旧共享对白预览将固定侦探说话人标签绑定到该对白样例；不代表正式运行的人物台词。

**原文位置：** [godot_csharp/scenes/Shared/UI/ChapterDialogue.tscn](../../godot_csharp/scenes/Shared/UI/ChapterDialogue.tscn:133)，L133–L133。

```text
text = "侦探"
```

### RES-092

**范围与类型：** `preview` / `text`。**索引说明：** 旧共享对白预览中侦探/工程师的固定剧情身份或台词；不作为正式会话文案。

**原文位置：** [godot_csharp/scenes/Shared/UI/ChapterDialogue.tscn](../../godot_csharp/scenes/Shared/UI/ChapterDialogue.tscn:146)，L146–L146。

```text
text = "我想先看看这张纸上写了什么，再听听你的说法。"
```

### RES-093

**范围与类型：** `preview` / `text`。**索引说明：** 旧共享对白预览中侦探/工程师的固定剧情身份或台词；不作为正式会话文案。

**原文位置：** [godot_csharp/scenes/Shared/UI/ChapterDialogue.tscn](../../godot_csharp/scenes/Shared/UI/ChapterDialogue.tscn:261)，L261–L261。

```text
text = "侦探
```

### RES-094

**范围与类型：** `example` / `schema`。**索引说明：** 示例预览将门厅、侦探、守夜人与内侧插闩绑定为固定世界实体；只代表示例场景。

**原文位置：** [godot_csharp/scenes/Shared/Story/Examples/WorldScript/Host/WorldScriptPreview.tscn](../../godot_csharp/scenes/Shared/Story/Examples/WorldScript/Host/WorldScriptPreview.tscn:61)，L61–L61。

```text
metadata/entity_id = "world:room.foyer"
```

### RES-095

**范围与类型：** `example` / `schema`。**索引说明：** 示例预览将门厅、侦探、守夜人与内侧插闩绑定为固定世界实体；只代表示例场景。

**原文位置：** [godot_csharp/scenes/Shared/Story/Examples/WorldScript/Host/WorldScriptPreview.tscn](../../godot_csharp/scenes/Shared/Story/Examples/WorldScript/Host/WorldScriptPreview.tscn:71)，L71–L71。

```text
metadata/entity_id = "world:character.detective"
```

### RES-096

**范围与类型：** `example` / `schema`。**索引说明：** 示例预览将门厅、侦探、守夜人与内侧插闩绑定为固定世界实体；只代表示例场景。

**原文位置：** [godot_csharp/scenes/Shared/Story/Examples/WorldScript/Host/WorldScriptPreview.tscn](../../godot_csharp/scenes/Shared/Story/Examples/WorldScript/Host/WorldScriptPreview.tscn:77)，L77–L77。

```text
metadata/entity_id = "world:character.guard"
```

### RES-097

**范围与类型：** `example` / `schema`。**索引说明：** 示例预览将门厅、侦探、守夜人与内侧插闩绑定为固定世界实体；只代表示例场景。

**原文位置：** [godot_csharp/scenes/Shared/Story/Examples/WorldScript/Host/WorldScriptPreview.tscn](../../godot_csharp/scenes/Shared/Story/Examples/WorldScript/Host/WorldScriptPreview.tscn:83)，L83–L83。

```text
metadata/entity_id = "world:object.inner_bolt"
```

## S20

**对应建议：** 保留资产造型与姿态生产配方。4 处源码、0 处资源。

### CSH-028

**范围与类型：** `tool` / `schema`。**索引说明：** 素材工具为早餐交换场景绑定人物姿态、桌面与汤碗资源；不推进剧情或映射答案。

**标记位置：** [godot_csharp/tools/build_breakfast_exchange.gd:17](../../godot_csharp/tools/build_breakfast_exchange.gd:17)。

**原文位置：** [godot_csharp/tools/build_breakfast_exchange.gd](../../godot_csharp/tools/build_breakfast_exchange.gd:18)，L18–L29。

```gdscript
static func build() -> void:
	var source := Image.load_from_file(ProjectSettings.globalize_path(SOURCE))
	var atlas := Image.create_empty(512, 128, false, Image.FORMAT_RGBA8)
	append_poses(source, 6, 0, atlas)
	var between := Image.load_from_file(ProjectSettings.globalize_path("res://AssetsSource/Internal/Characters/DemoAnimation/breakfast_exchange_between.png"))
	append_poses(between, 2, 6, atlas)
	assert(atlas.save_png(ProjectSettings.globalize_path(OUT)) == OK)
	var table := Image.load_from_file(ProjectSettings.globalize_path("res://AssetsSource/Internal/Props/breakfast_table_clear.png"))
	table = table.get_region(bounds(table))
	table.resize(224, 44, Image.INTERPOLATE_NEAREST)
	var canvas := Image.create_empty(224, 64, false, Image.FORMAT_RGBA8)
	canvas.blit_rect(table, Rect2i(Vector2i.ZERO, table.get_size()), Vector2i(0, 8))
```

### CSH-029

**范围与类型：** `tool` / `schema`。**索引说明：** Maxwell素材工具将开场角色绑定为手按胸口/鞠躬姿态及白手套像素；不推进剧情或映射答案。

**标记位置：** [godot_csharp/tools/build_maxwell_intro.gd:24](../../godot_csharp/tools/build_maxwell_intro.gd:24)。

**原文位置：** [godot_csharp/tools/build_maxwell_intro.gd](../../godot_csharp/tools/build_maxwell_intro.gd:25)，L25–L36。

```gdscript
func build_pose(name: String, source_name: String, maximum: Vector2i, palette: Array[Color]) -> void:
	var source := Image.load_from_file(ProjectSettings.globalize_path(SOURCE + source_name))
	assert(source != null and not source.is_empty(), "Missing Maxwell pose source: " + source_name)
	var bounds := Rect2i()
	var found := false
	for y in source.get_height():
		for x in source.get_width():
			if source.get_pixel(x, y).a > 0.5:
				var pixel := Rect2i(x, y, 1, 1)
				bounds = bounds.merge(pixel) if found else pixel
				found = true
	assert(found, "Maxwell pose has no opaque pixels: " + source_name)
```

### CSH-030

**范围与类型：** `tool` / `schema`。**索引说明：** 资产工具固定生成项目故事场景道具（钟、传送门、罗盘、早餐桌、书柜、客厅钢琴及横幅等）。

**标记位置：** [godot_csharp/tools/build_story_tiles.gd:23](../../godot_csharp/tools/build_story_tiles.gd:23)。

**原文位置：** [godot_csharp/tools/build_story_tiles.gd](../../godot_csharp/tools/build_story_tiles.gd:24)，L24–L35。

```gdscript
func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT))
	if "--breakfast-exchange-only" in OS.get_cmdline_user_args():
		preload("res://tools/build_breakfast_exchange.gd").build()
		quit()
		return
	if "--parlor-only" in OS.get_cmdline_user_args():
		build_parlor_furniture()
		quit()
		return
	if "--breakfast-only" in OS.get_cmdline_user_args():
		build_compact_story_props(["breakfast_table", "prep_island", "closed_door", "side_table"])
```

### CSH-031

**范围与类型：** `tool` / `schema`。**索引说明：** 素材工具绑定推理书房的道具与空间摆位；不推进剧情或映射答案。

**标记位置：** [godot_csharp/tools/build_study.gd:38](../../godot_csharp/tools/build_study.gd:38)。

**原文位置：** [godot_csharp/tools/build_study.gd](../../godot_csharp/tools/build_study.gd:39)，L39–L50。

```gdscript
func build() -> void:
	var room := Node2D.new(); room.name = "StudyRoom"; room.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	var ts: TileSet = load(ART + "Study.tres")
	room.set_meta("architecture", "32px TileMap terrains; independent Y-sorted furniture")
	var floor_cells := rect_cells(Rect2i(1,3,18,8)); floor_cells.append_array(rect_cells(Rect2i(9,11,2,2)))
	layer(room,ts,"Floor",0,"石板地面",floor_cells)
	layer(room,ts,"Walls",1,"石砌墙面",rect_cells(Rect2i(1,0,18,3)))
	var caps := rect_cells(Rect2i(0,0,1,12)); caps.append_array(rect_cells(Rect2i(19,0,1,12)))
	caps.append_array(rect_cells(Rect2i(1,11,8,1))); caps.append_array(rect_cells(Rect2i(11,11,8,1)))
	layer(room,ts,"WallCaps",0,"木质墙顶",caps)
	layer(room,ts,"Wainscot",1,"木护墙",rect_cells(Rect2i(1,2,18,1)))
	var rug := rect_cells(Rect2i(7,5,6,4)); rug.append_array(rect_cells(Rect2i(9,11,2,2)))
```

## S21

**对应建议：** 剧本声明通行状态及其传播。1 处源码、0 处资源。

### CH2-009

**范围与类型：** `production` / `logic`。**索引说明：** 具体宿舍、走廊和名录库区域的通行身份硬编码。

**标记位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2Access.cs:8](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2Access.cs:8)。

**原文位置：** [godot_csharp/scenes/Chapter2/Prototype/Chapter2Access.cs](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2Access.cs:9)，L9–L20。

```csharp
    private readonly HashSet<Place> _releasedDorms = [Place.DormLife, Place.DormW];
    private static readonly Tag Passable = new("Passable");
    public int CalendarMinutes { get; private set; }
    public static int RationFirstDay => FirstDay + 1;
    public bool HostDormReleased(Person p) => _releasedDorms.Contains(p.Pro.Dorm);
    public bool HostCanEnter(Place place) => place switch
    {
        Place.Hall or Place.Lecture => _corridors["north"],
        Place.Logistics or Place.Clinic or Place.Greenhouse => _corridors["east"],
        Place.ArchiveFront or Place.Resonance => _corridors["archive"],
        Place.Oath => _corridors["north"],
        Place.DormS or Place.DormL => _releasedDorms.Contains(place),
```


