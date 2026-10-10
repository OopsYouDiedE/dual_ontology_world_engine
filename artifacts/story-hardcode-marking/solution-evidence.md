# 剧情硬编码现存位置摘录

对应 [重构指示](solution-review.md) 与 [分组清单](solution-groups.json)。每条给出当前位置及其起始最多 3 行原文，便于定位；不是完整方法。

类型：**标记**＝源码仍有 `STORY-HARDCODE` 注释；**注释已删**＝注释不在但问题仍在；**残留**＝无编号的同类问题。

## S02

语义结果与剧本文案、称呼分离。12 条。

**AWK-012**（注释已删） [godot_csharp/scenes/Awakening/Night/NightReplay.cs:689](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:689)：重演中按侦探身份记拒绝陌生人

```csharp
            if (stranger && a.Who == Person.Detective) _detectiveRefusedStranger = true;
            var response = ResponseCatalog.Select(Chapter1Data.Root.GetProperty("EXPRESSION"), "doubt",
                name => name == "sender.Acquainted" ? sender.Tags.GetValueOrDefault(NightTags.Acquainted) : null);
```

**CH2-057**（注释已删） [godot_csharp/scenes/Chapter2/Prototype/Player.cs:559](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:559)：Describe 按具体物件逐个分支

```csharp
    public string Describe(Essence e)
    {
        var response = Respond("describe", Det, null, new Dictionary<string, object?> { ["object"] = ExpressionObject(e) },
```

**CH2-059**（注释已删） [godot_csharp/scenes/Chapter2/Prototype/Report.cs:126](../../godot_csharp/scenes/Chapter2/Prototype/Report.cs:126)：章末报告直连专案字段

```csharp
        sb.AppendLine(X("案件", ("结果", g.Accused is not { } acc ? X("无人指控") : X(g.QuerCleared ? "洗清" : g.QuerOstracized ? "排斥" : "未对质", ("人", Names.Of(a…
            ("规矩", g.CaseRule?.Text ?? X("无")), ("共同", X(g.CommonRuleEstablished ? "成立" : "未成立"))));
        sb.AppendLine(X("操纵", ("内容", g.Manipulations.Count == 0 ? X("无") : string.Join("；", g.Manipulations.Select(m => X("操纵项", ("日", m.Day), ("人", N…
```

**CH2-066**（注释已删） [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:665](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:665)：指控对象为具名人物时置专案标志

```csharp
        if (q.Id == P.Quer) AccusedQuer = true;
        Accused ??= q.Id;
        Accusations.Add((Day, w.Id, q.Id, n));
```

**CH2-080**（注释已删） [godot_csharp/scenes/Chapter2/Prototype/Chapter2SessionView.cs:38](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2SessionView.cs:38)：终局事件按专案字段组装

```csharp
    private IEnumerable<Ev> EndEvents(Ev result)
    {
        if (result.GetValueOrDefault("stands") is IDictionary<string, string> stands)
```

**CH2-081**（注释已删） [godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:447](../../godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:447)：状态栏按专案字段分支

```csharp
    private void Status()
    {
        var det = g.Det;
```

**残留** [godot_csharp/scenes/Chapter2/Prototype/Case.cs:415](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:415)：重开对质的中文 Feed 字面量（415–417）

```csharp
        if (_reopenedDay == Day) { Feed.Add("今天已经重开过一次对质了。"); return; }
        if (Day < CaseDay || Day == CaseDay && Slot < Slot.Night) { Feed.Add("翌日傍晚的对质还没开始。"); return; }
        if (QuerCleared || Accused is null) { Feed.Add("眼下没有悬着的指控要重开。"); return; }
```

**残留** [godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:420](../../godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:420)：帮助例句写定剧情人物

```csharp
        W("  例：讲 茉拉 值守者 有人急病时 取一份口粮 次日补记者不算私取 记入台账");
    }

```

**残留** [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:702](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:702)：中文 Feed 字面量（另见 709、799）

```csharp
            if (Where(Det) == Place.Hall) Feed.Add("沃德把那张被撕掉的规矩又贴了回去。");
        }
        if (Day >= CaseDay && Slot == Slot.Evening && WardCommittee is null)
```

**残留** [godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:142](../../godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:142)：宿主写定来源专称

```csharp
        ObservationSource.Corruption => "铜口之言", _ => "自知"
    };

```

**残留** [godot_csharp/scenes/Chapter2/Prototype/Morning.cs:315](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:315)：中文 Feed 字面量（另见 324、328、624、640）

```csharp
            Feed.Add($"配给台前有人数了数：存粮还剩 {stock} 份，照这几天的吃法，{(days >= 7 - Day + 1 ? "撑得到议事那天" : $"撑不到第 7 日的早饭")}。温室{(GreenhouseRepaired ? "已经修好，每天能添几份" : …
        }
        // 早饭前人们说起的昨天的事（§13 第 2 条；多日交互审核 M11）：侦探整天私下说话，清晨也读得到别人自发做的事。
```

**残留** [godot_csharp/scenes/Chapter2/Prototype/Player.cs:245](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:245)：中文 Feed 字面量（本文件约 20 处）

```csharp
                Feed.Add(Get(Det.Body, T.Fed) > 0 ? $"你领到了今天的口粮{(_distributor is { } dd ? $"（开粮的是{dd.Name}{(Get(Stove, T.Cooked) > 0 || _cookedThisSlo…
                break;
            case Cmd.Class:
```

## S03

剧本实体登记替代固定名单与专用枚举。17 条。

**AWK-007**（标记） [godot_csharp/scenes/Awakening/Night/NightReplay.cs:195](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:195)

```csharp
    // STORY-HARDCODE[AWK-007][logic][production]: 构造具体案件物件、四名角色的起始地点与鞋底，并将 V1/V2/V6 绑定到世界状态。
    private void Build()
    {
```

**AWK-034**（标记） [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:464](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:464)

```csharp
        // STORY-HARDCODE[AWK-034][logic][production]: 从本章固定案件真相建立清晨证物实体与醒来人物认知。
        var truth = Chapter1Data.Root.GetProperty("TRUTH");
        int TruthValue(string field, int? equals)
```

**AWK-063**（标记） [godot_csharp/scenes/Awakening/Night/NightVariables.cs:7](../../godot_csharp/scenes/Awakening/Night/NightVariables.cs:7)

```csharp
// STORY-HARDCODE[AWK-063][schema][production]: 保留既有角色、房间与六项变量的兼容枚举序号；运行集合及取值域从声明读取。
public enum Person { Detective, Huo, Shen, Su }

```

**CH2-012**（标记） [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:103](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:103)

```csharp
    // STORY-HARDCODE[CH2-012][logic][production]: 初始化具体角色、案件实体、持钥身份、时空位置与案件状态。
    public Game(GameOptions? options = null, LiveChapterWorld? continuation = null, ChapterWorldDefinition? authored = null, WorldSession? session = n…
    {
```

**CH2-074**（标记） [godot_csharp/scenes/Chapter2/Prototype/Model.cs:7](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:7)

```csharp
// STORY-HARDCODE[CH2-074][schema][production]: 定义本章十六位学员的具体身份标识。
public enum P { Det, Huo, Shen, Su, Lucian, Gu, Quer, Ward, Selma, Ruan, Elena, Moran, Veronica, Bertrand, Margaret, Mora }
// STORY-HARDCODE[CH2-092][schema][production]: 固定本章具名学员所属的四个社会群体。
```

**CH2-075**（标记） [godot_csharp/scenes/Chapter2/Prototype/Model.cs:43](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:43)

```csharp
// STORY-HARDCODE[CH2-075][schema][production]: 定义本案粮仓、痕迹与被指控等专用状态标识。
public static class T
{
```

**CH2-076**（标记） [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:186](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:186)

```csharp
    // STORY-HARDCODE[CH2-076][schema][production]: 初始化本章具体空间、案件物件、持钥人身份与知情模型。
    private void BuildWorld()
    {
```

**CH2-085**（标记） [godot_csharp/scenes/Chapter2/Prototype/Model.cs:12](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:12)

```csharp
// STORY-HARDCODE[CH2-085][schema][production]: 定义本章具名场所与宿舍区身份。
public enum Place { Hall, Lecture, Logistics, Clinic, Greenhouse, ArchiveFront, Oath, DormLife, DormW, DormS, DormL, Resonance, Living }
public enum Slot { Morning, Forenoon, Noon, Afternoon, Evening, Night }
```

**CH2-092**（标记） [godot_csharp/scenes/Chapter2/Prototype/Model.cs:9](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:9)

```csharp
// STORY-HARDCODE[CH2-092][schema][production]: 固定本章具名学员所属的四个社会群体。
public enum Group { Life, Wenqi, Xingyi, Houqin }
/// <summary>新地点只追加在末尾：已有地点的序号写进了存档与快照。</summary>
```

**CH2-093**（标记） [godot_csharp/scenes/Chapter2/Prototype/Model.cs:21](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:21)

```csharp
// STORY-HARDCODE[CH2-093][schema][production]: 固定本章角色日程可执行的专属行为枚举。
public enum Job
{
```

**CH2-098**（标记） [godot_csharp/scenes/Chapter2/Prototype/Model.cs:17](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:17)

```csharp
// STORY-HARDCODE[CH2-098][schema][production]: 固定本章价值聚落类别及其社会阵营语义。
public enum Cluster { None, Order, Labor, Beauty, Evidence }

```

**CH2-099**（标记） [godot_csharp/scenes/Chapter2/Prototype/Model.cs:34](../../godot_csharp/scenes/Chapter2/Prototype/Model.cs:34)

```csharp
// STORY-HARDCODE[CH2-099][schema][production]: 固定本章议事规则语义位，包括具名目标、档案院权限及夜间议案条件。
public enum RF
{
```

**CH2-028**（注释已删） [godot_csharp/scenes/Chapter2/Prototype/Emergence.cs:138](../../godot_csharp/scenes/Chapter2/Prototype/Emergence.cs:138)：按具名人物指定对头

```csharp
            var enemy = p.Id == P.Quer ? P.Ward : P.Ruan;
            RecordEvent(p.Id, $"一见{Names.Of(enemy)}就走开了", "行走", _startPlace.GetValueOrDefault(p, Where(p)), [], job: Job.Avoid);
            return true;
```

**CH2-031**（注释已删） [godot_csharp/scenes/Chapter2/Prototype/Player.cs:483](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:483)：口供表达按人物分支

```csharp
    private (string Text, string Emotion) TestimonyPresentation(Person p, bool selectedTestimony = false)
    {
        if (Day < CaseDay) return ChatPresentation(p);
```

**残留** [godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:14](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:14)：剧本人物仍映射到旧枚举

```csharp
    public static P PersonId(WorldEntityDefinition entity)
    {
        var local=Local(entity);
```

**残留** [godot_csharp/scenes/Chapter2/Prototype/Morning.cs:520](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:520)：目击者固定为具名人物

```csharp
        if (DetSketchSeen.Contains(night)) foreach (var s in Sightings.Where(s => s.Day == night && s.Witness == P.Moran)) set.Add(s.Truth);
        return set;
    }
```

**残留** [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:189](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:189)：剧本房间与物件仍落到旧 Place/对象字段

```csharp
        BuildDeclaredRooms();
        BuildAccess();
        Clock = BuildStandaloneObject("object.Clock");
```

## S04

个体信念、习惯与立场编译为主观规则。18 条。

**AWK-008**（标记） [godot_csharp/scenes/Awakening/Night/NightReplay.cs:283](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:283)

```csharp
    // STORY-HARDCODE[AWK-008][logic][production]: 按 V5/V6 设定侦探对特定同伴的信任偏移与熟人信任。
    private int TrustOf(Person self, Person other)
    {
```

**AWK-009**（标记） [godot_csharp/scenes/Awakening/Night/NightReplay.cs:310](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:310)

```csharp
    // STORY-HARDCODE[AWK-009][logic][production]: 逐人写入昨夜具体行为、认知规则、门/烟/物证互动和请求回应。
    private void WriteHabits(Agent detective, Agent huo, Agent shen, Agent su)
    {
```

**AWK-017**（标记） [godot_csharp/scenes/Awakening/Night/MorningWorld.cs:95](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:95)

```csharp
    // STORY-HARDCODE[AWK-017][logic][production]: 为有具体旧物回忆的人写入欲望、守则、铜口信任、劝告与触物行为规则。
    private void WriteMind(RecallMemory memory)
    {
```

**AWK-018**（标记） [godot_csharp/scenes/Awakening/Night/MorningWorld.cs:305](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:305)

```csharp
    // STORY-HARDCODE[AWK-018][logic][production]: 将本案角色取舍、知守则与劝告分支映射到专属心声。
    public RecallDecision? DecideRecall(string evidence)
    {
```

**AWK-019**（标记） [godot_csharp/scenes/Awakening/Night/MorningWorld.cs:338](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:338)

```csharp
    // STORY-HARDCODE[AWK-019][logic][production]: 独处时犹豫即放下是本章角色回忆规则。
    public RecallDecision? DecideAlone(string evidence)
    {
```

**AWK-020**（标记） [godot_csharp/scenes/Awakening/Night/MorningWorld.cs:256](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:256)

```csharp
    // STORY-HARDCODE[AWK-020][logic][production]: 固定以公告框第一条作为角色读到的代价规则。
    public void ShowRule1(Person who)
    {
```

**CH2-001**（标记） [godot_csharp/scenes/Chapter2/Prototype/Alliances.cs:16](../../godot_csharp/scenes/Chapter2/Prototype/Alliances.cs:16)

```csharp
    // STORY-HARDCODE[CH2-001][logic][production]: 聚落阵营规则的具体身份、时段和后果在代码中定义。
    private void WriteAllianceRules()
    {
```

**CH2-005**（标记） [godot_csharp/scenes/Chapter2/Prototype/Rules.cs:118](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:118)

```csharp
    // STORY-HARDCODE[CH2-005][logic][production]: 为具体人物初始化特定规则、时段、目标与结果。
    private void WriteRules()
    {
```

**CH2-008**（标记） [godot_csharp/scenes/Chapter2/Prototype/Social.cs:286](../../godot_csharp/scenes/Chapter2/Prototype/Social.cs:286)

```csharp
    // STORY-HARDCODE[CH2-008][logic][production]: 阵营成员集合固定引用本章具体人物身份。
    public List<Faction> Factions()
    {
```

**CH2-013**（标记） [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:301](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:301)

```csharp
    // STORY-HARDCODE[CH2-013][logic][production]: 初始化不同具体人物对角色、身份、关系、规则及旧案的认知。
    private void Know(Person p)
    {
```

**CH2-029**（标记） [godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:277](../../godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:277)

```csharp
    // STORY-HARDCODE[CH2-029][logic][production]: 背景人物按具体身份、日期和关系执行预设专属行为。
    private void BackgroundLines(Person p, Fx fx, List<Rule> rules)
    {
```

**CH2-034**（标记） [godot_csharp/scenes/Chapter2/Prototype/Rules.cs:290](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:290)

```csharp
    // STORY-HARDCODE[CH2-034][logic][production]: 按具体人物身份写入对议案措辞的立场偏好。
    private int StanceTerms(Person p, RuleSpec s, List<string> parts)
    {
```

**CH2-035**（标记） [godot_csharp/scenes/Chapter2/Prototype/Rules.cs:384](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:384)

```csharp
    // STORY-HARDCODE[CH2-035][logic][production]: 为各具体人物预设议案条件与反对理由。
    private static IEnumerable<(RF Need, int Penalty, string Text)> Requirements(P p) => p switch
    {
```

**CH2-036**（标记） [godot_csharp/scenes/Chapter2/Prototype/Rules.cs:396](../../godot_csharp/scenes/Chapter2/Prototype/Rules.cs:396)

```csharp
    // STORY-HARDCODE[CH2-036][logic][production]: 指定人物对特定议案的否决底线。
    public string? Veto(Person p, RuleSpec s)
    {
```

**CH2-041**（标记） [godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:98](../../godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:98)

```csharp
    // STORY-HARDCODE[CH2-041][logic][production]: 为具名人物预置习惯行动及其观察规则。
    public ImmutableArray<Rule> HabitRules(Person p)
    {
```

**CH2-042**（标记） [godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:195](../../godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:195)

```csharp
    // STORY-HARDCODE[CH2-042][logic][production]: 按本章人物身份与案件状态配置行动计划。
    private void PlanRules(Person p, Fx fx, List<Rule> rules)
    {
```

**CH2-065**（标记） [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:625](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:625)

```csharp
    // STORY-HARDCODE[CH2-065][logic][production]: 按本案特定行为积累或消除嫌疑。
    private void Suspicions()
    {
```

**CH2-073**（标记） [godot_csharp/scenes/Chapter2/Prototype/Host.cs:93](../../godot_csharp/scenes/Chapter2/Prototype/Host.cs:93)

```csharp
    // STORY-HARDCODE[CH2-073][logic][production]: 指定首课答题后特定人群对教师的反应。
    public void HostLessonAnswer(bool precise)
    {
```

## S05

感知与传播法则驱动认知更新。6 条。

**AWK-001**（标记） [godot_csharp/scenes/Awakening/Night/NightVariables.cs:98](../../godot_csharp/scenes/Awakening/Night/NightVariables.cs:98)

```csharp
    // STORY-HARDCODE[AWK-001][logic][production]: 固定传播表规定铜口完整/接头句尾/隔墙低语/其余警铃，以及门内外喊声组合；拓扑与接头配置读取故事数据。
    public static Hearing Hear(VoiceOrigin origin, Room listener, IReadOnlyDictionary<string, bool>? loose = null)
    {
```

**AWK-011**（标记） [godot_csharp/scenes/Awakening/Night/NightReplay.cs:502](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:502)

```csharp
    // STORY-HARDCODE[AWK-011][logic][production]: 把本案话语的语义标记映射到人物对指令、门外人和求救的认知。
    private void Hear(Agent a, Utterance words, Hearing hearing)
    {
```

**AWK-021**（标记） [godot_csharp/scenes/Awakening/Night/MorningWorld.cs:445](../../godot_csharp/scenes/Awakening/Night/MorningWorld.cs:445)

```csharp
    // STORY-HARDCODE[AWK-021][schema][production]: 将现实声路接头状态投影到 E8 铜口证物的固定事实标签。
    public bool SetJoint(string name, bool loose)
    {
```

**CH2-014**（标记） [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:500](../../godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:500)

```csharp
    // STORY-HARDCODE[CH2-014][logic][production]: 限定具体空间的夜间视线并生成身形目击和专案反馈。
    private void NightSight(Person p, Place here)
    {
```

**CH2-037**（标记） [godot_csharp/scenes/Chapter2/Prototype/Social.cs:213](../../godot_csharp/scenes/Chapter2/Prototype/Social.cs:213)

```csharp
    // STORY-HARDCODE[CH2-037][logic][production]: 固定本章恶魔词、基座裂缝指认及指控/教训后果。
    public void Receive(Person teller, Person hearer, NewsItem item, Heard via, Channel channel = Channel.Relay)
    {
```

**CH2-064**（标记） [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:544](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:544)

```csharp
    // STORY-HARDCODE[CH2-064][logic][production]: 判定本案夜间取物是否被特定证据和目击捕捉。
    private void NightSeenTaking(Person taker, bool recorded)
    {
```

## S06

世界法则声明物理后果和劳作配方。4 条。

**CH2-062**（标记） [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:326](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:326)

```csharp
    // STORY-HARDCODE[CH2-062][logic][production]: 把指定案件行动完成映射为本章固定后果。
    private void OnCompleted(ActionExecution exec)
    {
```

**CH2-063**（标记） [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:415](../../godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:415)

```csharp
    // STORY-HARDCODE[CH2-063][logic][production]: 结算本章人物、作案物件与调查线索的专属物理结果。
    private void Physics(Person p, Job? job, ActionExecution exec)
    {
```

**AWK-015**（注释已删） [godot_csharp/scenes/Awakening/Night/NightReplay.cs:866](../../godot_csharp/scenes/Awakening/Night/NightReplay.cs:866)：喊声可闻范围写在宿主

```csharp
        // 霍铎朝走廊的喊声：走廊与宿舍里的人都听得见。
        var shout = Tag(_space[Room.Hall], NightTags.Shout);
        if (shout != _lastShout)
```

**CH2-070**（注释已删） [godot_csharp/scenes/Awakening/Script/story/续篇/10_执行.story:349](../../godot_csharp/scenes/Awakening/Script/story/续篇/10_执行.story:349)：分灯公式写定具名人物信任与病情

```text
            allocate_oil:
                condition: tag("$actor", "OilKey", 1) 且 (tag("object.Clock", "SlotEvening", 1) 或 tag("object.Clock", "SlotNight", 1)) 且 (非 tag("$actor…
                mode: Prefer
```

## S07

剧本声明交互条件、试验和演出流程。16 条。

**AWK-022**（标记） [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:14](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:14)

```csharp
    // STORY-HARDCODE[AWK-022][logic][production]: 第一章开场固定摆放侦探与三名同伴并启动醒来流程。
    private IEnumerable<Op> Begin()
    {
```

**AWK-023**（标记） [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:159](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:159)

```csharp
            // STORY-HARDCODE[AWK-023][logic][production]: 开门命令固定联动隔离门状态、音效和主旋律。
            case "door_open":
                if (Session.Has("clock", "door_open_minutes")) break;
```

**AWK-024**（标记） [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:234](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:234)

```csharp
    // STORY-HARDCODE[AWK-024][logic][production]: 相遇阶段固定角色身份与对应会面锚点。
    private IEnumerable<Op> Meeting()
    {
```

**AWK-025**（标记） [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:394](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:394)

```csharp
    // STORY-HARDCODE[AWK-025][logic][production]: E11 手印与 E3 鞋底试验使用本章专属处理分支。
    private IEnumerable<Op> Experiment(string id)
    {
```

**AWK-026**（标记） [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:625](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:625)

```csharp
    // STORY-HARDCODE[AWK-026][schema][production]: 把本章声路听位绑定到对应现场证物热点。
    private static readonly Dictionary<string, string> SoundSpotSite = new()
        { ["foyer"] = "E1", ["R08"] = "E6", ["hall"] = "E7", ["pantry"] = "E9", ["R05"] = "E12" };
```

**AWK-027**（标记） [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:630](../../godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:630)

```csharp
    // STORY-HARDCODE[AWK-027][logic][production]: 按已知石板、乐谱、霍铎回忆开放本章试验句。
    private bool SentenceKnown(int index) => index switch
    {
```

**AWK-028**（标记） [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:76](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:76)

```csharp
    // STORY-HARDCODE[AWK-028][logic][production]: E8 只开听位菜单且 E3 选对象前不结算；其余试验按证物编号调度。
    public void BeginExperiment(MorningWorld world, string id, string who)
    {
```

**AWK-029**（标记） [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:122](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:122)

```csharp
    // STORY-HARDCODE[AWK-029][logic][production]: 侦探与三名同伴的旧物回忆分别走固定角色流程。
    public void BeginRecall(MorningWorld world, string id)
    {
```

**AWK-030**（标记） [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:226](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:226)

```csharp
    // STORY-HARDCODE[AWK-030][logic][production]: 固定同伴顺序、调查痕迹阈值与离场低语触发条件属于本章流程。
    public void BeginRoomChanged(MorningWorld world, string room, long now)
    {
```

**AWK-031**（标记） [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:280](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:280)

```csharp
    // STORY-HARDCODE[AWK-031][logic][production]: E1/E3 开放重演、指定熟悉旁证揭示 V6 的触发映射。
    public string[] Examine(MorningWorld world, string id, IReadOnlySet<string>? visiblePeople = null)
    {
```

**AWK-032**（标记） [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:312](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:312)

```csharp
    // STORY-HARDCODE[AWK-032][schema][production]: E12 展示 E7 石板小图是本案证物间固定关联。
    public string CardFacts(string id)
    {
```

**AWK-033**（标记） [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:318](../../godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:318)

```csharp
    // STORY-HARDCODE[AWK-033][logic][production]: E11 四人手印进度及 E8 声路试验笔记为本案专属摘要逻辑。
    public string[] CardNotes(MorningWorld world, string id)
    {
```

**AWK-035**（标记） [godot_csharp/scenes/Awakening/Script/Chapter1Interaction.cs:37](../../godot_csharp/scenes/Awakening/Script/Chapter1Interaction.cs:37)

```csharp
        // STORY-HARDCODE[AWK-035][logic][production]: E11 手印卡有先比自己、再比在场同伴的本章专属门槛。
        if (id == "E11")
        {
```

**CH2-033**（标记） [godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:345](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:345)

```csharp
        // STORY-HARDCODE[CH2-033][logic][production]: 调查台账在案发日后解锁本案重演的预写逻辑。
        if (thing=="台账" && _game.Day>=Game.CaseDay && !_replayOpen) { _replayOpen=true; _continuations.Enqueue(o=>Activate("card.replay_unlock",o)); }
    }
```

**CH2-078**（标记） [godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs:11](../../godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs:11)

```csharp
    // STORY-HARDCODE[CH2-078][text][production]: 按本章时段、地点与案件状态提供场景动作选项。
    public static RoomView For(Game g, Place? place)
    {
```

**CH2-079**（标记） [godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs:54](../../godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs:54)

```csharp
    // STORY-HARDCODE[CH2-079][text][production]: 定义本章当面交互可执行的具体人物行动。
    public static RoomAction[] PersonActions(Game g, P who, Place? place)
    {
```

## S08

结构化事件替代中文文本反解析。3 条。

**残留** [godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:352](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:352)：按“存粮”前缀解析事实

```csharp
        if(parts.Length!=2||!parts[0].StartsWith("存粮"))return facts;
        return site switch { "stock"=>parts[0]+"。","latch"=>parts[1],_=>facts };
    }
```

**残留** [godot_csharp/scenes/Chapter2/Prototype/Morning.cs:424](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:424)：按“饼屑:”“神情:”前缀解析痕迹键

```csharp
    private static string Label(string key) => key.StartsWith("饼屑:") ? key[3..] + "的废纸篓" : key.StartsWith("神情:") ? key[3..] + "的神情" : key;

    /// <summary>重演中被假设的人的墨影与心声（不显示数值）；其余人只给身影。</summary>
```

**残留** [godot_csharp/scenes/Chapter2/Prototype/Morning.cs:461](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:461)：按“的习惯”“应”判断行动缘由

```csharp
        var b when b.Contains("的习惯") => "平常就这么做。",
        var b when b.StartsWith("应") => "有人请我做的。",
        "听来的规则" => "有人跟我说过这条规矩，我认。",
```

## S09

通用 AST 执行规矩与社会请求。3 条。

**CH2-056**（标记） [godot_csharp/scenes/Chapter2/Prototype/Player.cs:241](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:241)

```csharp
        // STORY-HARDCODE[CH2-056][logic][production]: 剩余玩家行动的章节反馈与案情揭示；社会请求以实际执行回执结算。
        switch (act.Cmd)
        {
```

**CH2-010**（注释已删） [godot_csharp/scenes/Chapter2/Prototype/Council.cs:110](../../godot_csharp/scenes/Chapter2/Prototype/Council.cs:110)：按行为卡数字编号判定（另见 Report.cs:52/86、GameJobs.cs:209/246、GameLoop.cs:551/632、Host.cs:69、Morning.cs:484、Social.cs:257）

```csharp
        foreach (var s in Specs.Where(s => s.Card is { Behavior: >= 5 and <= 8 } && s.Author == P.Det)) yield return s;
    }

```

**CH2-055**（注释已删） [godot_csharp/scenes/Chapter2/Prototype/Player.cs:112](../../godot_csharp/scenes/Chapter2/Prototype/Player.cs:112)：调停只在固定日固定时段有效

```csharp
            case Cmd.Mediate when Day == RationFirstDay && Slot == Slot.Noon:
                HostMediate(act.Wording ?? throw new PlayerError("Missing mediation wording."));
                break;
```

## S10

区分历史、制度钟点与人物当前行动。4 条。

**CH2-027**（注释已删） [godot_csharp/scenes/Awakening/Script/story/续篇/10_执行.story:389](../../godot_csharp/scenes/Awakening/Script/story/续篇/10_执行.story:389)：课表另建，与 08_流程 日期错位

```text
        schedule:
            lessons:
                11:
```

**CH2-043**（注释已删） [godot_csharp/scenes/Chapter2/Prototype/Demon.cs:100](../../godot_csharp/scenes/Chapter2/Prototype/Demon.cs:100)：广播按日分支

```csharp
        var rows = ExecutionDesign.GetProperty("schedule").GetProperty("broadcasts");
        string Line(string id, params (string Name, object? Value)[] values) => AstText(rows.GetProperty(id).GetString()!, values);
        if (Day == FirstDay)
```

**CH2-067**（注释已删） [godot_csharp/scenes/Awakening/Script/story/续篇/10_执行.story:354](../../godot_csharp/scenes/Awakening/Script/story/续篇/10_执行.story:354)：盯视/护送/告知以具名身份与日期为门槛

```text
            observe_distrust:
                condition: tag("$actor", "IsWard", 1) 且 tag("object.Clock", "Day", 4) 且 (非 tag("object.Clock", "Day", 5)) 且 tag("object.Clock", "SlotE…
                mode: Prefer
```

**残留** [godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:181](../../godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:181)：自主规则对全员以 Acceptance=True 安装

```csharp
        rules.AddRange(BindAst(fx).Rules(ExecutionDesign.GetProperty("autonomous")).Select(r => r with { Acceptance = Truth.True }));
        return [.. rules];
    }
```

## S11

数据驱动案件、重演和证据判定。15 条。

**AWK-003**（标记） [godot_csharp/scenes/Awakening/Night/AwakeningCase.cs:20](../../godot_csharp/scenes/Awakening/Night/AwakeningCase.cs:20)

```csharp
    // STORY-HARDCODE[AWK-003][schema][production]: 将本章 V1–V6、证物字段及第六问默认认知装配成案件假设。
    private static (CaseModel, ImmutableArray<Prediction>) Build()
    {
```

**AWK-004**（标记） [godot_csharp/scenes/Awakening/Night/MorningTraces.cs:13](../../godot_csharp/scenes/Awakening/Night/MorningTraces.cs:13)

```csharp
// STORY-HARDCODE[AWK-004][schema][production]: 字段枚举本案固定证物事实及其编码取值。
public sealed record MorningTraces(
    bool InnerBoltDown,
```

**AWK-005**（标记） [godot_csharp/scenes/Awakening/Night/MorningTraces.cs:95](../../godot_csharp/scenes/Awakening/Night/MorningTraces.cs:95)

```csharp
    // STORY-HARDCODE[AWK-005][logic][production]: E1–E9 的逐件痕迹比较与字段解释是本案专属映射。
    private static List<string> Differences(string id, MorningTraces p, MorningTraces a)
    {
```

**AWK-037**（标记） [godot_csharp/scenes/Awakening/Script/Chapter1Session.cs:631](../../godot_csharp/scenes/Awakening/Script/Chapter1Session.cs:631)

```csharp
    // STORY-HARDCODE[AWK-037][schema][production]: 手稿固定将第六问作为末项并按认知解锁隐藏显示。
    private Ev ReplaySnapshot()
    {
```

**AWK-039**（标记） [godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs:187](../../godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs:187)

```csharp
            // STORY-HARDCODE[AWK-039][schema][production]: 答辩固定为本章四步，并允许 me 作为自闩陈述。
            && _arguments.Count == 4 && _arguments.Select(a => a.Step).Order().SequenceEqual(Enumerable.Range(1,4))
            && _arguments.All(a => a.Evidence.Any(e => content.Session.GetProgress("examined").GetValueOrDefault(e) is true)
```

**AWK-061**（标记） [godot_csharp/scenes/Awakening/Script/Chapter1Replay.cs:8](../../godot_csharp/scenes/Awakening/Script/Chapter1Replay.cs:8)

```csharp
    // STORY-HARDCODE[AWK-061][logic][production]: 本章实播重演将固定六项变量、吻合判断和角色见证写入玩家进度。
    public ImmutableArray<Finding> RunVisibleReplay(MorningWorld world, NightVariables variables)
    {
```

**CH2-003**（标记） [godot_csharp/scenes/Chapter2/Prototype/Case.cs:74](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:74)

```csharp
    // STORY-HARDCODE[CH2-003][logic][production]: 案发痕迹、人物知识变量与反事实重演映射为特定案件事实。
    public CaseTraces Dawn()
    {
```

**CH2-025**（标记） [godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:426](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:426)

```csharp
    // STORY-HARDCODE[CH2-025][logic][production]: 对质议程固定为本案的取用、门闩、台账及特定人物结局。
    private void BeginConfront(List<Dictionary<string,object?>> output)
    {
```

**CH2-030**（标记） [godot_csharp/scenes/Chapter2/Prototype/Morning.cs:165](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:165)

```csharp
    // STORY-HARDCODE[CH2-030][logic][production]: 清晨生成台账、门闩、病碗、废纸篓与目击等具体案发痕迹。
    private List<TraceItem> TakeTraces(int night)
    {
```

**CH2-032**（标记） [godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:18](../../godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:18)

```csharp
    // STORY-HARDCODE[CH2-032][schema][production]: 调查条目与本章具体证物、案件变量的映射逻辑。
    public static InvestigationEntry[] Entries(Game g)
    {
```

**CH2-049**（标记） [godot_csharp/scenes/Chapter2/Prototype/Morning.cs:255](../../godot_csharp/scenes/Chapter2/Prototype/Morning.cs:255)

```csharp
    // STORY-HARDCODE[CH2-049][logic][production]: 指定本案各类物证达到何种组合才可读全。
    private bool FullyRead(int night, TraceItem t)
    {
```

**CH2-071**（标记） [godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:128](../../godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:128)

```csharp
    // STORY-HARDCODE[CH2-071][schema][production]: 把案件特定痕迹键映射到对应调查物件。
    private static string TraceImage(Game g, TraceItem trace)
    {
```

**CH2-086**（标记） [godot_csharp/scenes/Chapter2/Prototype/Case.cs:10](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:10)

```csharp
// STORY-HARDCODE[CH2-086][schema][production]: 定义本案缺粮、门闩、空碗、废纸篓与夜巡身影的证物模型。
public sealed record CaseTraces(int LedgerShort, bool LatchFixed, bool LatchToolMarks, bool BowlPorridge, IReadOnlyList<Place> CrumbDorms, IReadOnlyLi…
{
```

**CH2-087**（标记） [godot_csharp/scenes/Chapter2/Prototype/Case.cs:45](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:45)

```csharp
// STORY-HARDCODE[CH2-087][schema][production]: 固定本案反事实重演所用的四项人物信念/处境变量。
public sealed record CaseVars(int MoraBelief, int QuerRule, int MargaretRule, int QuerSupper)
{
```

**AWK-038**（注释已删） [godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs:86](../../godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs:86)：规划器由本章 V1–V6 预测装配

```csharp
        foreach (var prediction in AwakeningCase.Predictions)
            _hypotheses.Add(new(prediction.Variables, prediction.Facts.ToDictionary(p => p.Key, p => p.Value)));
    }
```

## S12

真实集会与个人接受驱动对质、议事。6 条。

**CH2-004**（标记） [godot_csharp/scenes/Chapter2/Prototype/Council.cs:24](../../godot_csharp/scenes/Chapter2/Prototype/Council.cs:24)

```csharp
    // STORY-HARDCODE[CH2-004][logic][production]: 议事前厅三站、角色资格与夜间夺取的具体规则及反馈。
    public bool Staff(IReadOnlyCollection<Person> crew, out string report, bool commander = false)
    {
```

**CH2-047**（标记） [godot_csharp/scenes/Chapter2/Prototype/Council.cs:115](../../godot_csharp/scenes/Chapter2/Prototype/Council.cs:115)

```csharp
    // STORY-HARDCODE[CH2-047][logic][production]: 结算本章誓约庭议案、具名派系立场及具体结局。
    public Ending Council(int detBacks = -1)
    {
```

**CH2-088**（标记） [godot_csharp/scenes/Chapter2/Prototype/Case.cs:243](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:243)

```csharp
    // STORY-HARDCODE[CH2-088][logic][production]: 结算本案公开指控、证据说服、雀儿洗清或排斥及规则提出。
    public void Confront(ConfrontPlan? plan, bool reopen = false)
    {
```

**CH2-089**（标记） [godot_csharp/scenes/Chapter2/Prototype/Case.cs:357](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:357)

```csharp
    // STORY-HARDCODE[CH2-089][logic][production]: 处理案件对质后的特定议案表态、议事站队与共同规则结果。
    private void Propose(ConfrontPlan plan, Person[] room)
    {
```

**CH2-090**（标记） [godot_csharp/scenes/Chapter2/Prototype/Case.cs:412](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:412)

```csharp
    // STORY-HARDCODE[CH2-090][logic][production]: 允许案发后援引本章共同规则重开中央厅对质。
    private void Reopen(ConfrontPlan? plan)
    {
```

**CH2-091**（标记） [godot_csharp/scenes/Chapter2/Prototype/Case.cs:436](../../godot_csharp/scenes/Chapter2/Prototype/Case.cs:436)

```csharp
    // STORY-HARDCODE[CH2-091][logic][production]: 按本案指控结局改变雀儿的寝区、信任与名誉认知。
    private void Ostracize()
    {
```

## S13

现场站点能力改为表现声明。1 条。

**CH2-083**（注释已删） [godot_csharp/scenes/Chapter2/Prototype/Chapter2AuthoredSites.cs:23](../../godot_csharp/scenes/Chapter2/Prototype/Chapter2AuthoredSites.cs:23)：站点 kind 白名单含专案类型

```csharp
            var kind=Read("kind");if(kind is not ("" or "bed" or "watch" or "meal" or "tube" or "school_rules"))throw new InvalidDataException("Unsupp…
            var title=Read("title");if(string.IsNullOrWhiteSpace(title))throw new InvalidDataException("Missing site title: "+id);
            var thing=Read("thing");
```

## S14

资源生成器读取剧本显示内容。3 条。

**残留** [godot_csharp/tools/build_awakening_map.py:191](../../godot_csharp/tools/build_awakening_map.py:191)：第一章地图标题字面量

```python
    room = dict(key="awakening_yi", title="乙区", rows=paint_rows, regions=regions, floor=base["floor"], wall=base["wall"],
                decor=decor, floor_work=floor_work, furniture=furniture())
    return room, game_rows
```

**残留** [godot_csharp/tools/chapter2_art/outdoor.py:38](../../godot_csharp/tools/chapter2_art/outdoor.py:38)：室外图 REGIONS 标题字面量（RES-007–018 等）

```python
REGIONS = [  # key, title, painted rect (u0, u1, v0, v1), core rect
    ("campus_nw", "学园·讲堂与星仪院", (-128, -4, -50, 84), (-128, -16, -38, 84)),
    ("campus_n", "学园·中央厅与名录库", (-28, 98, -50, 84), (-16, 86, -38, 84)),
```

**残留** [godot_csharp/tools/chapter2_art/outdoor.py:893](../../godot_csharp/tools/chapter2_art/outdoor.py:893)：室外图场点标签字面量（RES-035–039 等）

```python
    sites = [{"id": "lane", "point": list(map(int, px(-47.8, -56.7))), "label": "夹道"},
             {"id": "collapse", "point": [int(x), int(y)], "label": "港路塌方处"},
             {"id": "oath_court", "point": list(map(int, px(0, -100))), "label": "誓约庭"},
```

## S15

声音配置声明附加音效。1 条。

**CSH-022**（标记） [godot_csharp/scenes/UI/Panels/Dialogue/ChapterDialogue.gd:565](../../godot_csharp/scenes/UI/Panels/Dialogue/ChapterDialogue.gd:565)

```gdscript
	# STORY-HARDCODE[CSH-022][logic][production]: 铜口之声每段首次开口触发盖片与管道共鸣音。
	if current_voice_stream == VOICE_TUBE and _previous_voice_stream != VOICE_TUBE:
		Sound.sfx("tube", -6.0)
```

## S17

测试去除文本匹配与固定路线。18 条。

**AWK-049**（注释已删） [godot_csharp/scenes/Awakening/check/StoryPacketCheck/Program.cs:82](../../godot_csharp/scenes/Awakening/check/StoryPacketCheck/Program.cs:82)：按中文文本断言

```csharp
    flowContent.StartFlow(flowWorld, "答辩·提问", (SampleText("story-packet-check", "text-001"), "1"));
    Check(flowContent.NextMeeting() == "answer_q3", "question provider replaces the carried step");

```

**AWK-051**（注释已删） [godot_csharp/scenes/Awakening/check/DetectiveSimulation/ContinuationSimulation.cs:131](../../godot_csharp/scenes/Awakening/check/DetectiveSimulation/ContinuationSimulation.cs:131)：固定续玩路线

```csharp
                    if (menu != SampleText("continuation-simulation", "text-026")) claimQuestion = menu;
                    var (fits, claims) = game.DetectiveInference();
                    if (fits.Count == 0 && game.CaseActual is { } actualTraces && !tried.ContainsKey(SampleText("continuation-simulation", "text-027")…
```

**AWK-052**（注释已删） [godot_csharp/scenes/Awakening/check/DetectiveSimulation/ContinuationSimulation.cs:160](../../godot_csharp/scenes/Awakening/check/DetectiveSimulation/ContinuationSimulation.cs:160)：按中文文本断言

```csharp
                    var next = game.DetectiveSupport(lastClaim).Select(e => "evidence:" + e).FirstOrDefault(id => !done.Contains(id) && Find(id) is no…
                        ?? ids.FirstOrDefault(id => id.StartsWith("evidence:") && !done.Contains(id));
                    if (next is null) return null;
```

**AWK-053**（注释已删） [godot_csharp/scenes/Awakening/check/flow_check.gd:191](../../godot_csharp/scenes/Awakening/check/flow_check.gd:191)：流程检查按文本/固定路线

```gdscript
		check(fx.select("experiment"), "声路试验第 %d 轮：已看 E7/E6，E8 有试验选项" % (round + 1))
		if not await fx.wait_until(func(): return host.ui.modal_kind == "form"):
			check(false, Samples.text("flow-check", "text-076"))
```

**AWK-054**（注释已删） [godot_csharp/scenes/Awakening/check/state_gallery.gd:308](../../godot_csharp/scenes/Awakening/check/state_gallery.gd:308)：图注文本匹配；4 个目标为空

```gdscript
		var thought := detective_thought(stage_node)
		if not seen.voice and stage_node.caption.text.contains(Samples.text("state-gallery", "text-011")):
			seen.voice = true
```

**AWK-057**（注释已删） [godot_csharp/scenes/Awakening/check/hand_outline_check.gd:29](../../godot_csharp/scenes/Awakening/check/hand_outline_check.gd:29)：写定具体物件

```gdscript
	var document: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://scenes/Awakening/Script/generated_awakening_dialogue.json"))
	var story := JSON.stringify(document.entries)
	check(document.entries.has("help_E11") and story.contains(Samples.text("hand-outline-check", "text-003")) and story.contains(Samples.text("hand-outli…
```

**CH2-017**（注释已删） [godot_csharp/scenes/Chapter2/check/check_flow.gd:47](../../godot_csharp/scenes/Chapter2/check/check_flow.gd:47)：支持方总选最后一项

```gdscript
func planned_choice(options: Array) -> String:
	var available: Array = options.filter(func(option): return bool(option.get("available", true)))
	if available.is_empty(): return ""
```

**CH2-019**（注释已删） [godot_csharp/scenes/Chapter2/check/check_flow.gd:158](../../godot_csharp/scenes/Chapter2/check/check_flow.gd:158)：固定路线

```gdscript
	# 日常篇：就地等待，夜里睡下；规矩仍通过实际可用的规则编号转述。
	var case_day := int(fx.probe("case_day"))
	var guard := 0
```

**CH2-020**（注释已删） [godot_csharp/scenes/Chapter2/check/chapter2_fixture.gd:56](../../godot_csharp/scenes/Chapter2/check/chapter2_fixture.gd:56)：夹具写定剧情状态

```gdscript
		if host.ui.modal_kind == "form" and str(host.ui._modal_event.get("fields", [{}])[0].get("id", "")) == "name":
			name_form_answer("flow2-fixture")
		elif host.ui.dialogue.active:
```

**CH2-021**（注释已删） [godot_csharp/scenes/Chapter2/Game/check_gallery.gd:48](../../godot_csharp/scenes/Chapter2/Game/check_gallery.gd:48)：预加载已删除的 AwakeningEngine.cs，入口失效

```gdscript
	var first: Node = preload("res://scenes/Awakening/AwakeningEngine.cs").new()
	root.add_child(first)
	first.ChapterEnd([], false, false, false)
```

**CH2-022**（注释已删） [godot_csharp/scenes/Chapter2/Prototype/Checks.cs:217](../../godot_csharp/scenes/Chapter2/Prototype/Checks.cs:217)：按中文日志前缀匹配

```csharp
            if (!Agree(mw) && w.NightTruth.Any(t => t.StartsWith($"D{Game.CaseNight} 雀儿 夜里自取"))) nonUnique.Add(b.Name);
        }
        Check(nonUnique.Count == 0, SampleText("chapter2-checks", "text-055") + (nonUnique.Count > 0 ? SampleText("chapter2-checks", "text-056") + str…
```

**CH2-077**（注释已删） [godot_csharp/scenes/Chapter2/Prototype/Program.cs:51](../../godot_csharp/scenes/Chapter2/Prototype/Program.cs:51)：固定路线

```csharp
                    else Run(new PlayerAction(Enum.Parse<Cmd>(step.GetProperty("cmd").GetString()!))
                    { Place = step.TryGetProperty("place", out var place) ? Enum.Parse<Place>(place.GetString()!) : null });
                }
```

**CSH-007**（注释已删） [godot_csharp/tools/verify_ch1_locker_ui.gd:160](../../godot_csharp/tools/verify_ch1_locker_ui.gd:160)：按场面名等待固定流程

```gdscript
		check(await fx.wait_until(func(): return fx.stage() == "meeting", 30.0), "walk from wake into meeting")
	var anchor: Vector2 = fx.map().anchor(Samples.text("locker-ui-check", "text-001"))
	record("geometry", {"anchor": [anchor.x, anchor.y], "furniture": fx.map().layout.furniture})
```

**CSH-010**（注释已删） [godot_csharp/scenes/Shared/check/chapter1_smoke.gd:61](../../godot_csharp/scenes/Shared/check/chapter1_smoke.gd:61)：固定流程（另见 94）

```gdscript
	check(await settle(func(): return host.ui.modal_kind == "card" and not host.ui.dialogue.active), Samples.text("chapter1-smoke", "text-008"))
	check(str(host.ui._modal_event.get("notes", [])).contains(Samples.text("chapter1-smoke", "text-009")), Samples.text("chapter1-smoke", "text-010"))
	auto.select("close")
```

**CSH-015**（注释已删） [godot_csharp/scenes/Shared/check/ui_modules_check.gd:195](../../godot_csharp/scenes/Shared/check/ui_modules_check.gd:195)：样例写定剧情问题

```gdscript
	form.show_data({"header": {"question": "提问者问：[b]谁关了灯？[/b]", "instructions": Samples.text("ui-modules-check", "text-044")}, "layout": "tabs", "unset_l…
		"summary": [{"field": "say", "label": Samples.text("ui-modules-check", "text-046")}, {"field": "show", "label": Samples.text("ui-modules-check", "te…
		"fields": [
```

**CSH-016**（注释已删） [godot_csharp/scenes/Shared/check/ui_modules_check.gd:223](../../godot_csharp/scenes/Shared/check/ui_modules_check.gd:223)：样例写定剧情内容

```gdscript
		{"heading": "[b]看过的[/b]", "style": "grid", "entries": [{"label": Samples.text("ui-modules-check", "text-055"), "card_id": "E1"}, {"label": Samples.t…
		{"heading": "[b]规矩[/b]", "style": "list", "entries": [{"label": Samples.text("ui-modules-check", "text-058")}, {"label": Samples.text("ui-modules-ch…
	]})
```

**CSH-019**（注释已删） [godot_csharp/scenes/Shared/check/check_room_fog.gd:192](../../godot_csharp/scenes/Shared/check/check_room_fog.gd:192)：写定房间/人物

```gdscript
	if key == "chapter1":
		game.sight.seen_cells.clear()
		var unknown := true
```

**CSH-020**（注释已删） [godot_csharp/scenes/Shared/check/check_room_fog.gd:329](../../godot_csharp/scenes/Shared/check/check_room_fog.gd:329)：写定演员 Huo

```gdscript
	if game.actors.is_empty(): game._make_actor("Huo")
	if not game.actors.is_empty():
		var npc: Node2D = game.actors.values()[0]
```

## S18

UI 回放与采样器消费正式绑定。5 条。

**CSH-006**（标记） [godot_csharp/tools/capture_ui_start.gd:9](../../godot_csharp/tools/capture_ui_start.gd:9)

```gdscript
# STORY-HARDCODE[CSH-006][logic][tool]: 采集器按正式开场的“开始游戏”与“如何称呼你”剧情节点推进。
func _run() -> void:
	capture_viewport = SubViewport.new()
```

**CSH-024**（标记） [godot_csharp/tools/capture_story_ui_samples.gd:8](../../godot_csharp/tools/capture_story_ui_samples.gd:8)

```gdscript
# STORY-HARDCODE[CSH-024][schema][tool]: 采样清单固定选择两章对白、E1证物、防辩表、午间争执、议事支持项与结局。
const SAMPLE_SPECS := [
	{"slug": "01-c1-long-dialogue", "kind": "lines", "line_prefix": "s2_meet.", "prefer_longest": true},
```

**CSH-025**（标记） [godot_csharp/tools/capture_player_transcript.gd:26](../../godot_csharp/tools/capture_player_transcript.gd:26)

```gdscript
# STORY-HARDCODE[CSH-025][schema][tool]: 回放工具硬编码学园场景地图与项目地点的对应关系。
const HOST_MAP_PLACES := {
	"central_hall": "Hall", "lecture": "Lecture", "logistics": "Logistics", "granary": "Logistics", "clinic": "Clinic",
```

**CSH-026**（标记） [godot_csharp/tools/capture_player_transcript.gd:309](../../godot_csharp/tools/capture_player_transcript.gd:309)

```gdscript
# STORY-HARDCODE[CSH-026][logic][tool]: 用正式快照中的人物/地点（包括侦探及实际热点）复原场景坐标，只重放不执行剧情答案。
func _apply_context(input: Dictionary) -> void:
	if not is_instance_valid(host.field): return
```

**CSH-027**（标记） [godot_csharp/tools/capture_player_transcript.gd:783](../../godot_csharp/tools/capture_player_transcript.gd:783)

```gdscript
# STORY-HARDCODE[CSH-027][schema][tool]: 可选截图阶段按E1、say字段、support选项及两场景对白识别剧情节点。
func _screenshot_stage(ev: Dictionary) -> String:
	# Match production event structure, never dispatch offsets or translated prose.
```

## S19

预览数据中性化。8 条。

**CSH-021**（注释已删） [godot_csharp/scenes/UI/Workbench/samples/campus_preview.json:3](../../godot_csharp/scenes/UI/Workbench/samples/campus_preview.json:3)：预览仍用正式地点 ID

```json
 "ArchiveFront": {"pos": [500, 70], "label": "地点1"},
 "Hall": {"pos": [500, 200], "label": "地点2"},
 "Lecture": {"pos": [300, 200], "label": "地点3"},
```

**残留** [godot_csharp/scenes/Shared/UI/CampusMap.tscn:6](../../godot_csharp/scenes/Shared/UI/CampusMap.tscn:6)：shared-ui 命中

```text
links = [["ArchiveFront", "Hall"], ["Hall", "Lecture"], ["Lecture", "Resonance"], ["Hall", "Commons"], ["Commons", "DormW"], ["Commons", "DormL"], ["C…

[node name="Locations" parent="." index="0"]
```

**残留** [godot_csharp/scenes/Shared/UI/ChapterDialogue.tscn:133](../../godot_csharp/scenes/Shared/UI/ChapterDialogue.tscn:133)：shared-ui 命中（另见 261）

```text
text = "侦探"

[node name="Body" type="RichTextLabel" parent="DialoguePanel" unique_id=116598757]
```

**残留** [godot_csharp/scenes/UI/Panels/CampusMap/CampusMap.gd:50](../../godot_csharp/scenes/UI/Panels/CampusMap/CampusMap.gd:50)：UI 命中

```gdscript
var known: Dictionary = {}      # travel：侦探知道的地点；replay：全部画出（那是假设，不是所见）
var figures: Array = []         # {pos, label, alpha, bubble}
var hovered := ""
```

**残留** [godot_csharp/scenes/UI/Panels/Dialogue/ChapterDialogue.gd:134](../../godot_csharp/scenes/UI/Panels/Dialogue/ChapterDialogue.gd:134)：UI 命中（另见 141、539、543）

```gdscript
	"detective": 0.0,
	"chef": 2.4,
	"engineer": -1.5,
```

**残留** [godot_csharp/scenes/UI/Panels/Manuscript/ReplayManuscript.tscn:137](../../godot_csharp/scenes/UI/Panels/Manuscript/ReplayManuscript.tscn:137)：UI 命中

```text
[node name="Ledger" type="Control" parent="RightPage/Scroll/Column"]
custom_minimum_size = Vector2(0, 512)
layout_mode = 2
```

**残留** [godot_csharp/scenes/UI/Panels/Title/Title.tscn:39](../../godot_csharp/scenes/UI/Panels/Title/Title.tscn:39)：UI 命中

```text
[node name="Desk" type="TextureRect" parent="."]
texture_filter = 1
layout_mode = 1
```

**残留** [godot_csharp/scenes/UI/UISystem.gd:496](../../godot_csharp/scenes/UI/UISystem.gd:496)：UI 命中

```gdscript
		view.show_data({"empty_text": "还没有记下任何东西。", "sections": [{"heading": "[b]某处[/b]", "style": "grid", "entries": [{"label": "示例证物", "card_id": "E1"}]}]…
	elif preview_page == 6:
		var body := open_modal("", Vector2(1152, 0), "replay")
```
