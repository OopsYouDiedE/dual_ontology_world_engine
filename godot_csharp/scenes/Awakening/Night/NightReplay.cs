using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Awakening;

/// <summary>本案用到的状态标签与认知关系。它们属于游戏层；Core 只提供 Tag/RelationTag 类型。</summary>
public static class NightTags
{
    // 客观状态（Essence 的 StateProperty）
    public static readonly Tag Open = new("Open"), Wedged = new("Wedged"), Bolted = new("Bolted"), CalledOut = new("CalledOut");
    public static readonly Tag Kicked = new("Kicked"), Soot = new("Soot"), AshFan = new("AshFan"), Dent = new("Dent");
    public static readonly Tag Line1 = new("Line1"), Line2 = new("Line2"), Wet = new("Wet"), Placed = new("Placed");
    public static readonly Tag Sugar = new("Sugar"), Shout = new("Shout");
    // 主观认知（只存在于各人的 ProjectionWorld）
    public static readonly Tag WantsOpen = new("WantsOpen"), Help = new("Help");
    public static readonly Tag Bolt = new("Bolt"), ForbidOpen = new("ForbidOpen"), HoldTube = new("HoldTube"), OpenOrder = new("OpenOrder"), FromTube = new("FromTube");
    public static readonly Tag TailOpen = new("TailOpen"), TailHelp = new("TailHelp"), FullOpen = new("FullOpen"), ClearBolt = new("ClearBolt"), Murmur = new("Murmur");
    public static readonly Tag Silence = new("Silence");
    /// <summary>沈鼎知道此人喝茶的口味；霍铎知道此人的工具小图；侦探认识此人多久。</summary>
    public static readonly Tag Taste = new("Taste"), Glyph = new("Glyph"), Acquainted = new("Acquainted");
}

public static class NightRelations
{
    public static readonly RelationTag Told = new("Told"), Asked = new("Asked");
}

/// <summary>鞋底决定踩进灰里会不会留下清楚的印。</summary>
public sealed class BootProperty(Sole sole) : Property
{
    public Sole Sole { get; } = sole;
}

/// <summary>门厅地面上留下的鞋印与湿布压痕。</summary>
public sealed class FloorTraceProperty : Property
{
    public List<BootPrint> Prints { get; } = [];
    public bool ClothOverHeel { get; set; }
}

public sealed record ReplayFrame(
    double Time, string Actor, string Label,
    ImmutableDictionary<Person, Room> Rooms,
    ImmutableDictionary<Person, string> Spots,
    ImmutableDictionary<Person, string> Thoughts,
    bool DoorOpen, bool DoorBolted, bool AshFan, bool SmokeIn);

/// <summary>
/// 行动值参数（只决定先后，不决定谁做什么）。默认值即游戏所用；穷举检查会在一组参数上复测唯一解，
/// 证明结果来自规则，而不是某个延迟值恰好卡住了先后。
/// </summary>
public sealed record NightTiming(double DetectiveSpeed = 0.85, double HuoSpeed = 1.0, double ShenSpeed = 0.9, double SuSpeed = 1.3,
    double SmokeReachesPantry = 13.0, double DetectiveWakes = 0.0, double LookDelay = 1.0)
{
    public static readonly NightTiming Default = new();
}

public sealed record ReplayResult(NightVariables Variables, MorningTraces Traces, ImmutableArray<ReplayFrame> Frames, ImmutableArray<string> Log)
{
    public ImmutableArray<string> Conflicts => Comparison.Conflicts(Traces, MorningTraces.Evidence);
    public bool FullMatch => Conflicts.IsEmpty;
}

/// <summary>
/// 一次“昨夜重演”。每次调用都新建独立的 Essence 世界（WorldRuntime）与四人的 ProjectionWorld，
/// 不读写清晨的现实世界。声路感知经 Observation.Observe 提交；人物习惯是写在各人内部世界中的 Rule，
/// 由 DecisionSystem 选择；请求经 Effect.Say 携带，接受与否由 SocialSystem 依各人内部的 Trust 判定；
/// 行动经 SimulationSession/ActionRuntime 在 CTB 上结算。宿主只负责声路、寻路（走到行动目标所在的房间）与烟灰物理。
/// </summary>
public sealed class NightReplay
{
    // 时间以行动值计，只表示先后。
    private const double VoiceFirst = 1.0, VoiceSecond = 2.2, SmokeAtDoor = 2.0, LongestNight = 160.0;
    private readonly NightTiming _timing;
    private double SmokeReachesPantry => _timing.SmokeReachesPantry;
    private double DetectiveWakes => _timing.DetectiveWakes;
    private double LookDelay => _timing.LookDelay;

    // 乙区拓扑（设计第二部分 §11 第 7 条）：四间住房开向中廊，中廊通活动厅；备餐间与门厅都只经活动厅出入。
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
    private readonly Dictionary<Essence, Room> _roomOf = [];
    private readonly Dictionary<(Room, Room), Essence> _passage = [];
    private readonly Dictionary<Essence, Projection> _view = [];
    private readonly List<Agent> _agents = [];
    private readonly List<ReplayFrame> _frames = [];
    private readonly List<string> _log = [];
    private Essence _door = null!, _wedge = null!, _floor = null!, _frame = null!, _fork = null!, _score = null!,
        _slate = null!, _cloth = null!, _cups = null!, _tube = null!;
    private bool _smokeIn, _decisionQueued;
    private int _lastShout;
    private (int Open, int Wedged, int Bolted, int CalledOut) _lastDoor;

    private NightReplay(NightVariables variables, NightTiming timing)
    {
        _v = variables;
        _timing = timing;
        _session = new SimulationSession(_runtime);
        _session.Completed += OnCompleted;
    }

    public static ReplayResult Run(NightVariables variables) => new NightReplay(variables, NightTiming.Default).Execute();
    public static ReplayResult Run(NightVariables variables, NightTiming timing) => new NightReplay(variables, timing).Execute();

    private double Now => _session.CTB.Now;

    // ------------------------------------------------------------------ world --

    private sealed class Agent(Person who, SoulEssence soul, Essence body, double speed)
    {
        public Person Who { get; } = who;
        public SoulEssence Soul { get; } = soul;
        public Essence Body { get; } = body;
        public double Speed { get; } = speed;
        public Projection Self = null!;
        public readonly Dictionary<Essence, Projection> Mental = [];
        public Projection Alarm = null!, Outsider = null!, Order = null!, Heard = null!, Shout = null!, DoorHeard = null!, Smoke = null!, Report = null!, Claim = null!;
        public readonly List<Habit> Habits = [];
        public bool Awake, Busy;
        public (Room Room, Projection Target)? Approach;
        public string Thought = "", Spot = "";
        public int HistorySeen, Failures;
        public bool HeardSomething, DoorSettled;
        public Room StartRoom;
    }

    private sealed record Habit(Rule Rule, string Label, string Thought, string Spot);

    private Essence Space(Room room)
    {
        var essence = new Essence(Names.Of(room), ProjectionKind.Space);
        var state = essence.Add(new StateProperty());
        Init(state, NightTags.Shout);
        _space[room] = essence;
        _roomOf[essence] = room;
        _view[essence] = _runtime.Project(essence, Names.Of(room));
        return essence;
    }

    private Essence Thing(string name, Room room, params Tag[] tags)
    {
        var essence = new Essence(name);
        essence.Add(new SpatialProperty { Space = _space[room] });
        Init(essence.Add(new StateProperty()), tags);
        _view[essence] = _runtime.Project(essence, name);
        return essence;
    }

    private static void Init(StateProperty state, params Tag[] tags)
    {
        foreach (var tag in tags)
        {
            state.Values[tag] = 0;
            state.Observable.Add(tag);
        }
    }

    private Agent MakePerson(Person who, Room start, Sole sole, double speed)
    {
        var soul = new SoulEssence(Names.Of(who));
        var body = new Essence(Names.Of(who), ProjectionKind.Body);
        body.Add(new SpatialProperty { Space = _space[start] });
        body.Add(new BootProperty(sole));
        soul.Relations.Add((Relations.Controls, body));
        _runtime.Project(soul, Names.Of(who) + "之魂");
        _view[body] = _runtime.Project(body, Names.Of(who));
        var agent = new Agent(who, soul, body, speed);
        _agents.Add(agent);
        return agent;
    }

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
        _door = Thing("隔离门", Room.Foyer, NightTags.Open, NightTags.Wedged, NightTags.Bolted, NightTags.CalledOut);
        _wedge = Thing("木楔", Room.Foyer, NightTags.Kicked, NightTags.Soot);
        _floor = Thing("门厅地面", Room.Foyer, NightTags.AshFan);
        _floor.Add(new FloorTraceProperty());
        _frame = Thing("门框", Room.Foyer, NightTags.Dent);
        _slate = Thing("石板", Room.Hall, NightTags.Line1, NightTags.Line2);
        _tube = Thing("铜口", Room.Hall);
        _score = Thing("乐谱", Room.R08, NightTags.Line1, NightTags.Line2);
        _cloth = Thing("抹布", Room.Pantry, NightTags.Wet, NightTags.Placed);
        _cups = Thing("四只杯子", Room.Pantry, NightTags.Sugar);

        var su = _v.V1 switch { SuPlace.OwnRoom => Room.R08, SuPlace.Hall => Room.Hall, _ => Room.Pantry };
        var huo = _v.V2 switch { HuoPlace.ByTube => Room.Hall, HuoPlace.OwnRoom => Room.R06, _ => Room.Foyer };
        var detective = MakePerson(Person.Detective, Room.R05, Sole.PoloBoot, _timing.DetectiveSpeed);
        var engineer = MakePerson(Person.Huo, huo, Sole.WorkBoot, _timing.HuoSpeed);
        var chef = MakePerson(Person.Shen, Room.Pantry, Sole.Soft, _timing.ShenSpeed);
        var musician = MakePerson(Person.Su, su, Sole.Soft, _timing.SuSpeed);
        // 苏泠的音叉在她手里。
        _fork = new Essence("音叉");
        _fork.Add(new SpatialProperty { Holder = musician.Body });
        _fork.Add(new StateProperty());
        _view[_fork] = _runtime.Project(_fork, "音叉");

        foreach (var agent in _agents) Know(agent);
        WriteHabits(detective, engineer, chef, musician);
        _lastDoor = DoorState();
    }

    /// <summary>入睡前各人对生活翼的认识：房间、通道、物件与彼此。其他人此刻在哪里，只有看见了才知道。</summary>
    private void Know(Agent agent)
    {
        var sources = _view.Keys.ToArray();
        var mental = _runtime.Observe(agent.Soul, sources.Select(e => _view[e]), 0);
        for (var i = 0; i < sources.Length; i++) agent.Mental[sources[i]] = mental[i];
        agent.Self = agent.Mental[agent.Body];
        Projection Concept(string name, ProjectionKind kind, params Tag[] tags) => Observation.Observe(agent.Soul,
            new ObservationInput(name, kind)
            {
                Source = ObservationSource.Introspection, Existence = Truth.False,
                Tags = tags.ToImmutableDictionary(t => t, _ => (int?)0)
            });
        agent.Alarm = Concept("警铃", ProjectionKind.Event);
        agent.Outsider = Concept("门外的人", ProjectionKind.Soul, NightTags.WantsOpen, NightTags.Help);
        agent.Order = Concept("一条指令", ProjectionKind.Procedure, NightTags.Bolt, NightTags.ForbidOpen, NightTags.HoldTube, NightTags.OpenOrder, NightTags.FromTube);
        agent.Heard = Concept("所闻", ProjectionKind.Event, NightTags.TailOpen, NightTags.TailHelp, NightTags.FullOpen, NightTags.ClearBolt, NightTags.Murmur);
        agent.Shout = Concept("活动厅的喊声", ProjectionKind.Event, NightTags.Bolt, NightTags.OpenOrder);
        agent.DoorHeard = Concept("门边所闻", ProjectionKind.Event, NightTags.Silence);
        agent.Smoke = Concept("烟", ProjectionKind.Material);
        agent.Report = Concept("门已闩好的回报", ProjectionKind.Event);
        agent.Claim = Concept("别人对那扇门的说法", ProjectionKind.Event);

        // V6：昨夜之前，四人彼此是否相熟。这决定每个人内部世界里对别人的认识，而不是任何人的行动。
        var familiar = _v.V6 == Acquaintance.Familiar;
        foreach (var other in _agents.Where(o => o != agent))
        {
            var them = agent.Mental[other.Body];
            var tags = ImmutableDictionary<Tag, int?>.Empty
                .Add(NightTags.Acquainted, familiar ? 1 : 0)
                .Add(NightTags.Taste, familiar ? 1 : 0)
                .Add(NightTags.Glyph, familiar ? 1 : 0)
                .Add(Tags.Trust, TrustOf(agent.Who, other.Who));
            Observation.Observe(agent.Soul, new ObservationInput(them.Name, them.Kind)
            {
                Target = them, Source = ObservationSource.Recall, Tags = tags,
                DenyRelations = [(NightRelations.Told, agent.Outsider), (NightRelations.Asked, agent.Order)],
                Move = true, Parent = _roomOf[SpaceOf(other.Body)] == _roomOf[SpaceOf(agent.Body)] ? agent.Mental[SpaceOf(other.Body)] : null
            });
        }
    }

    /// <summary>
    /// 信任初值。V6 素不相识：对一个昨天才认识的人，不足以凭一句转述去动隔离门（-1）；早已相熟：+2。
    /// V5 是玩家对侦探倾向的假设：他更愿意听谁（+1）。SocialSystem 依此判 Accept / Negotiate / Reject。
    /// </summary>
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
    private static Condition Ex(Projection p) => new Condition.Exists(p);
    private static Condition T(Projection p, Tag tag, int minimum = 1) => new Condition.Tagged(p, tag, minimum);
    private static Condition Not(Condition c) => new Condition.Not(c);
    private static Condition All(params Condition[] items) => new Condition.All([..items]);
    private static Condition At(Noun who, Projection room) => new Condition.At(who, room);
    private static Condition Rel(Projection a, RelationTag r, Projection b) => new Condition.Related(a, r, b);
    private static Effect Seq(params Effect[] items) => new Effect.Sequence([..items]);
    private static Effect Look(Projection p) => new Effect.Act(ActionOp.Inspect, p);
    private static Effect Set(Projection p, Tag tag, int value) => new Effect.Set(p, tag, value);
    private static Effect When(Condition c, Effect e) => new Effect.When(c, e);

    private void AddHabit(Agent a, string label, string thought, string spot, int weight, Condition condition, Effect effect)
        => a.Habits.Add(new(new Rule(condition, RuleMode.Prefer, effect, weight, Truth.True), label, thought, spot));

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
            AddHabit(a, "侦探循着喊声去活动厅", "活动厅有人在喊。去看看出了什么事。", "", 2,
                All(Ex(a.Alarm), Ex(a.Shout), Not(At(Me, hall)), Not(Ex(a.Claim))), Look(hall));
            AddHabit(a, "侦探自己去门边看", "说法我听到了。可门是什么样，我得自己去看。", "door", 1 + self,
                All(Ex(a.Claim), Not(Ex(a.DoorHeard))), Look(M(_door)));
            AddHabit(a, "侦探撤开木楔，门合上挡烟——没有插闩", "门被楔着，烟在往里灌。先撤楔让门合上挡烟——门外是谁还不知道，闩先不插。", "door", 1 + self,
                All(Ex(a.DoorHeard), At(Me, foyer), Ex(a.Smoke), T(M(_door), NightTags.Wedged), Not(T(M(_door), NightTags.CalledOut))),
                Seq(Look(M(_door)), Set(M(_wedge), NightTags.Kicked, 1), Set(M(_door), NightTags.Wedged, 0), Set(M(_door), NightTags.Open, 0)));
            AddHabit(a, "侦探照门外听清的话，闩上门", "门外的人自己说了：闩门，别开。我亲耳听见的。", "door", 1 + self,
                All(Ex(a.DoorHeard), At(Me, foyer), T(a.Heard, NightTags.ClearBolt), Not(T(M(_door), NightTags.Bolted))),
                Seq(Look(M(_door)), When(T(M(_door), NightTags.Wedged), Seq(Set(M(_wedge), NightTags.Kicked, 1), Set(M(_door), NightTags.Wedged, 0))),
                    Set(M(_door), NightTags.Open, 0), Set(M(_door), NightTags.Bolted, 1)));
        }
        // ---- 霍铎：凡收到指令必记在石板上；收到“守位”就不离位；有人来到身边时请他去执行。
        {
            var a = huo;
            Projection M(Essence e) => a.Mental[e];
            var hall = M(_space[Room.Hall]);
            var foyer = M(_space[Room.Foyer]);
            var det = M(detective.Body);
            var slate = M(_slate);
            var door = M(_door);
            var boltAndReport = Seq(Look(door),
                When(T(door, NightTags.Wedged), Seq(Set(M(_wedge), NightTags.Kicked, 1), Set(door, NightTags.Wedged, 0))),
                Set(door, NightTags.Open, 0), Set(door, NightTags.Bolted, 1),
                new Effect.Act(ActionOp.Travel, M(_passage[(Room.Foyer, Room.Hall)])),
                new Effect.Say(a.Self, "闩好了。"));
            var openAndReport = Seq(Look(door), Set(door, NightTags.Open, 1),
                new Effect.Act(ActionOp.Travel, M(_passage[(Room.Foyer, Room.Hall)])),
                new Effect.Say(a.Self, "门开着。"));
            AddHabit(a, "霍铎在石板上速记：G：⊥门 ✕开 ◎", "格雷厄姆：闩门。禁开。守铜口。记下。", "slate", 3,
                All(Ex(a.Order), T(a.Order, NightTags.Bolt), T(a.Order, NightTags.HoldTube), Not(T(slate, NightTags.Line1))),
                Seq(Look(slate), Set(slate, NightTags.Line1, 1)));
            AddHabit(a, "霍铎在石板上速记：G：开门", "格雷厄姆：开门。记下。", "slate", 3,
                All(Ex(a.Order), T(a.Order, NightTags.OpenOrder), Not(T(slate, NightTags.Line1))),
                Seq(Look(slate), Set(slate, NightTags.Line1, 2)));
            AddHabit(a, "霍铎在石板上速记：⊥门 ✕开", "门外的人说：闩门，别开。记下。", "slate", 1,
                All(Ex(a.Order), T(a.Order, NightTags.Bolt), Not(T(a.Order, NightTags.HoldTube)), Not(T(slate, NightTags.Line1))),
                Seq(Look(slate), Set(slate, NightTags.Line1, 3)));
            AddHabit(a, "霍铎守在铜口旁，朝走廊喊：闩门！", "格雷厄姆叫我守铜口，我不能走。谁来，谁去闩。", "tube", 2,
                All(T(a.Order, NightTags.FromTube), T(a.Order, NightTags.Bolt), T(slate, NightTags.Line1), Not(T(hall, NightTags.Shout))),
                Seq(Look(hall), Set(hall, NightTags.Shout, 1)));
            AddHabit(a, "霍铎守在铜口旁，朝走廊喊：开门！", "我守着铜口。谁来，谁去开。", "tube", 2,
                All(T(a.Order, NightTags.FromTube), T(a.Order, NightTags.OpenOrder), T(slate, NightTags.Line1), Not(T(hall, NightTags.Shout))),
                Seq(Look(hall), Set(hall, NightTags.Shout, 2)));
            AddHabit(a, "霍铎给侦探看石板：“格雷厄姆说的。闩门。我得守在这。”", "他来了。上头的话我记在石板上，他认得我的字——让他去闩。", "tube", 4,
                All(T(a.Order, NightTags.FromTube), T(a.Order, NightTags.Bolt), At(Me, hall), At(det, hall), Not(Rel(det, NightRelations.Asked, a.Order))),
                Seq(new Effect.Say(det, "格雷厄姆说的。闩门。我得守在这。", boltAndReport), new Effect.Think(Rel(det, NightRelations.Asked, a.Order))));
            AddHabit(a, "霍铎对侦探说：“铜口里有人喊开门。你去开。”", "他来了。让他去开。", "tube", 4,
                All(T(a.Order, NightTags.FromTube), T(a.Order, NightTags.OpenOrder), At(Me, hall), At(det, hall), Not(Rel(det, NightRelations.Asked, a.Order))),
                Seq(new Effect.Say(det, "铜口里有人喊开门。你去开。", openAndReport), new Effect.Think(Rel(det, NightRelations.Asked, a.Order))));
            AddHabit(a, "霍铎在石板速记下补一行：放大镜小图 + ⊥✓", "闩好了。记下：放大镜，⊥✓。", "slate", 3,
                All(Ex(a.Report), T(det, NightTags.Glyph), Not(T(slate, NightTags.Line2))),
                Seq(Look(slate), Set(slate, NightTags.Line2, 1)));
            AddHabit(a, "霍铎在石板速记下补一行：一个问号 + ⊥✓", "闩好了。……这人用什么记号？画个问号。", "slate", 3,
                All(Ex(a.Report), Not(T(det, NightTags.Glyph)), Not(T(slate, NightTags.Line2))),
                Seq(Look(slate), Set(slate, NightTags.Line2, 2)));
            AddHabit(a, "霍铎自己开门查看，用木楔撑住", "门外有人。开门看看。", "door", 3,
                All(Ex(a.Outsider), T(a.Outsider, NightTags.WantsOpen), At(Me, foyer), Not(T(door, NightTags.Open))),
                Seq(Look(door), Set(door, NightTags.Open, 1), Set(door, NightTags.Wedged, 1)));
            AddHabit(a, "霍铎照门外的话，自己闩上门", "门外说闩门。闩。", "door", 2,
                All(Ex(a.Order), T(a.Order, NightTags.Bolt), Not(T(a.Order, NightTags.FromTube)), At(Me, foyer), Not(T(door, NightTags.Bolted))),
                Seq(Look(door), Set(door, NightTags.Open, 0), Set(door, NightTags.Bolted, 1)));
        }
        // ---- 沈鼎：守住水与食物；见烟先湿布堵缝；人心乱时泡茶，按人放糖。
        {
            var a = shen;
            Projection M(Essence e) => a.Mental[e];
            var cloth = M(_cloth);
            var door = M(_door);
            var cups = M(_cups);
            AddHabit(a, "沈鼎浸湿抹布", "烟。先湿布。", "sink", 3,
                All(Ex(a.Smoke), Not(T(cloth, NightTags.Wet))),
                Seq(Look(cloth), new Effect.Act(ActionOp.Take, cloth), Set(cloth, NightTags.Wet, 1)));
            var foyer = M(_space[Room.Foyer]);
            AddHabit(a, "沈鼎拿着湿布去门厅", "烟从门那边来。把缝堵上。", "door", 3,
                All(T(cloth, NightTags.Wet), Not(T(cloth, NightTags.Placed)), Not(At(Me, foyer))), Look(door));
            // 规则：湿布只压在已闭合的门底；门开着，就等它合上。
            AddHabit(a, "沈鼎沿闭合的门底压下湿布", "门合上了。顺着门底压一条，烟就进不来。", "door", 3,
                All(T(cloth, NightTags.Wet), At(Me, foyer), Not(T(door, NightTags.Open)), Not(T(cloth, NightTags.Placed))),
                Seq(Look(door), new Effect.Act(ActionOp.Place, cloth, foyer), Set(cloth, NightTags.Placed, 1)));
            AddHabit(a, "沈鼎泡了四杯茶，按各人的口味放糖", "人都吓着了，先泡茶。阿泠不吃糖，老霍要两勺——这我记得。", "sink", 2,
                All(T(cloth, NightTags.Placed), T(M(su.Body), NightTags.Taste), T(M(huo.Body), NightTags.Taste), T(M(detective.Body), NightTags.Taste), Not(T(cups, NightTags.Sugar))),
                Seq(Look(cups), Set(cups, NightTags.Sugar, 1)));
            AddHabit(a, "沈鼎泡了四杯茶，每杯放一样的糖", "人都吓着了，先泡茶。谁爱吃甜，我不知道——一样放。", "sink", 2,
                All(T(cloth, NightTags.Placed), Not(T(M(su.Body), NightTags.Taste)), Not(T(cups, NightTags.Sugar))),
                Seq(Look(cups), Set(cups, NightTags.Sugar, 2)));
        }
        // ---- 苏泠：用耳朵定位；走近声源，用音叉辨声路；听到的东西记成乐谱。
        {
            var a = su;
            Projection M(Essence e) => a.Mental[e];
            var door = M(_door);
            var frame = M(_frame);
            var score = M(_score);
            var foyer = M(_space[Room.Foyer]);
            var det = M(detective.Body);
            var keepOpen = Seq(Look(door), Set(door, NightTags.Open, 1), Set(door, NightTags.CalledOut, 1));
            AddHabit(a, "苏泠拉开门，用木楔撑住，贴缝听", "门外有人在喊开门……我得开门让他进来。", "door", 3,
                All(Ex(a.Outsider), T(a.Outsider, NightTags.WantsOpen), Not(T(door, NightTags.Open)), Not(T(frame, NightTags.Dent))),
                Seq(Look(door), Set(door, NightTags.Open, 1), Set(door, NightTags.Wedged, 1)));
            AddHabit(a, "苏泠贴着门缝听，用音叉敲门框辨声路", "门开着，外面却没声了。声音从哪来？敲一下就知道。", "door", 3,
                All(Ex(a.Outsider), T(a.Outsider, NightTags.WantsOpen), T(door, NightTags.Open), Not(T(frame, NightTags.Dent))),
                Seq(Look(door), Set(frame, NightTags.Dent, 1), new Effect.Act(ActionOp.Place, M(_fork), foyer)));
            AddHabit(a, "苏泠回房记谱：两拍弱音，后两拍“开门”，记了两遍；旁注“门外？”", "门关上了。把听到的记下来。", "desk", 2,
                All(T(frame, NightTags.Dent), Ex(a.DoorHeard), T(a.Heard, NightTags.TailOpen), Not(T(door, NightTags.Open)), Not(T(score, NightTags.Line1))),
                Seq(Look(score), Set(score, NightTags.Line1, 1)));
            AddHabit(a, "苏泠回房记谱：“救我”", "门关上了。把听到的记下来。", "desk", 2,
                All(T(frame, NightTags.Dent), Ex(a.DoorHeard), T(a.Heard, NightTags.TailHelp), Not(T(door, NightTags.Open)), Not(T(score, NightTags.Line1))),
                Seq(Look(score), Set(score, NightTags.Line1, 2)));
            AddHabit(a, "苏泠回房记谱：整句“开门，救我”", "门关上了。那句话，记下来。", "desk", 2,
                All(T(frame, NightTags.Dent), Ex(a.DoorHeard), T(a.Heard, NightTags.FullOpen), Not(T(door, NightTags.Open)), Not(T(score, NightTags.Line1))),
                Seq(Look(score), Set(score, NightTags.Line1, 3)));
            // 规则：开了门的苏泠没有任何离开门边的习惯，直到门关上（记谱的习惯要求门已闭合）；她就守在那里。
            AddHabit(a, "苏泠对侦探说：“外面有人在喊开门。”", "他是来关门的。外面有人在喊开门——我得告诉他。", "door", 4,
                All(At(Me, foyer), At(det, foyer), Ex(a.Outsider), T(door, NightTags.Open), Not(Rel(det, NightRelations.Told, a.Outsider))),
                Seq(new Effect.Say(det, "外面有人在喊开门。", keepOpen), new Effect.Think(Rel(det, NightRelations.Told, a.Outsider))));
            AddHabit(a, "苏泠回房，在乐谱下方换了一支笔写：“他们关了门。”", "他们关了门。", "desk", 3,
                All(T(score, NightTags.Line1), Not(T(door, NightTags.Open)), Not(T(score, NightTags.Line2))),
                Seq(Look(score), Set(score, NightTags.Line2, 1)));
        }
        foreach (var agent in _agents)
            Observation.Observe(agent.Soul, new ObservationInput(Names.Of(agent.Who) + "的习惯", ProjectionKind.Concept)
            {
                Source = ObservationSource.Introspection, Existence = Truth.True, Rules = [..agent.Habits.Select(h => h.Rule)]
            });
    }

    // ------------------------------------------------------------------- run --

    private ReplayResult Execute()
    {
        Build();
        var ctb = _session.CTB;
        ctb.Schedule(0, () =>
        {
            foreach (var a in _agents) KnowConcept(a, a.Alarm, ObservationSource.Perception);
            foreach (var a in _agents.Where(a => a.Who != Person.Detective)) a.Awake = true;
            _agents[0].Thought = "（警铃。惊醒。）";
            foreach (var a in _agents.Where(a => a.Who != Person.Detective)) a.Thought = PlainThought(a);
            Frame("", "主楼警铃大作");
            QueueDecisions();
        });
        ctb.Schedule(VoiceFirst, () => Voice(1));
        ctb.Schedule(SmokeAtDoor, () => { UpdateSmoke(); QueueDecisions(); });
        ctb.Schedule(VoiceSecond, () => Voice(2));
        ctb.Schedule(DetectiveWakes, () =>
        {
            _agents[0].Awake = true;
            QueueDecisions();
        });
        var steps = 0;
        while (steps++ < 20000 && ctb.Pending.Any() && ctb.Pending.First().Due <= LongestNight && ctb.Step()) { }
        _session.Dispose();
        return new(_v, Traces(), [.._frames], [.._log]);
    }

    private void Voice(int count)
    {
        var words = Utterance.Of(_v.V4);
        foreach (var a in _agents)
        {
            var hearing = Acoustics.Hear(_v.V3, RoomOf(a.Body));
            Hear(a, words, hearing);
        }
        Frame("", _v.V3 == VoiceOrigin.Tube
            ? (count == 1 ? "主楼有人对着铜口说话" : "铜口里的话又说了一遍")
            : (count == 1 ? "门外有人喊话" : "门外的人又喊了一遍"));
        QueueDecisions();
    }

    /// <summary>声路把同一句话变成每个人不同的感知，经 Observation.Observe（来源：感知）写入各自的内部世界。</summary>
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
                KnowConcept(a, a.Outsider, ObservationSource.Perception, (NightTags.WantsOpen, words.Open), (NightTags.Help, words.Help));
                KnowConcept(a, a.Heard, ObservationSource.Perception, (NightTags.ClearBolt, words.Bolt), (NightTags.FullOpen, words.Open));
                if (words.Bolt) KnowConcept(a, a.Order, ObservationSource.Perception, (NightTags.Bolt, true), (NightTags.ForbidOpen, words.ForbidOpen));
                break;
            case Hearing.Tail:
                KnowConcept(a, a.Outsider, ObservationSource.Perception, (NightTags.WantsOpen, words.TailWantsOpen), (NightTags.Help, words.TailHelp));
                KnowConcept(a, a.Heard, ObservationSource.Perception, (NightTags.TailOpen, !words.TailHelp), (NightTags.TailHelp, words.TailHelp));
                break;
            case Hearing.Murmur:
                KnowConcept(a, a.Heard, ObservationSource.Perception, (NightTags.Murmur, true));
                break;
        }
        // 同一句话再听一遍、或走到听不见的地方，不改变已经形成的认知。
        if (hearing is Hearing.AlarmOnly or Hearing.Nothing && a.HeardSomething) return;
        if (hearing is not (Hearing.AlarmOnly or Hearing.Nothing))
        {
            if (a.HeardSomething) return;
            a.HeardSomething = true;
        }
        a.Thought = hearing switch
        {
            Hearing.Full => "“" + words.Full + "”",
            Hearing.FullMuffled => "门外有人：“" + words.Full + "”",
            Hearing.Tail => "“" + words.Tail + "”……门外有人。",
            Hearing.Murmur => "有人在说话……听不清。",
            _ => a.Who == Person.Detective ? "只有铃声。" : "只有铃声。……出什么事了？"
        };
    }

    private void KnowConcept(Agent a, Projection concept, ObservationSource source, params (Tag Tag, bool On)[] tags)
        => Observation.Observe(a.Soul, new ObservationInput(concept.Name, concept.Kind)
        {
            Target = concept, Source = source, Time = Now, Existence = Truth.True,
            Tags = tags.Where(t => t.On).ToImmutableDictionary(t => t.Tag, _ => (int?)1)
        });

    // ------------------------------------------------------------ perception --

    private Essence SpaceOf(Essence essence)
    {
        for (var e = essence; ;)
        {
            if (e.Kind == ProjectionKind.Space) return e;
            var spatial = e.Get<SpatialProperty>() ?? throw new InvalidOperationException(e.TrueName + " 没有位置。");
            e = spatial.Holder ?? spatial.Space ?? throw new InvalidOperationException(e.TrueName + " 没有位置。");
        }
    }

    private Room RoomOf(Essence essence) => _roomOf[SpaceOf(essence)];

    private int Tag(Essence e, Tag tag) => e.Get<StateProperty>()!.Values.GetValueOrDefault(tag);

    /// <summary>看见同一房间里的一切；不在眼前的人，位置变为未知。在门厅的人会贴门听、看见烟。</summary>
    private void Perceive(Agent a)
    {
        var room = SpaceOf(a.Body);
        var here = _roomOf[room];
        var visible = _view.Where(p => p.Key.Kind != ProjectionKind.Boundary && p.Key.Kind != ProjectionKind.Space
                ? SpaceOf(p.Key) == room
                : p.Key == room || p.Key.Get<PassageProperty>()?.Connections.ContainsKey(room) == true)
            .Select(p => p.Value).ToArray();
        _runtime.Observe(a.Soul, visible, Now);
        foreach (var other in _agents.Where(o => o != a && SpaceOf(o.Body) != room))
        {
            var them = a.Mental[other.Body];
            if (them.Parent is not null)
                Observation.Observe(a.Soul, new ObservationInput(them.Name, them.Kind) { Target = them, Time = Now, Move = true, Parent = null });
        }
        if (here == Room.Foyer)
        {
            KnowConcept(a, a.DoorHeard, ObservationSource.Perception, (NightTags.Silence, true));
            if (_smokeIn) KnowConcept(a, a.Smoke, ObservationSource.Perception);
        }
        // 门的事已经决定过的人，后来再听到关于这扇门的请求，也算答复过了：他不会回头推翻自己。
        if (a.DoorSettled)
            foreach (var request in a.Soul.World.Projections.Where(p => p.Kind == ProjectionKind.Event && p.Procedure is not null &&
                         p.Tags.GetValueOrDefault(Tags.Responded) == 0 && ChangesDoor(p.Procedure!, a)).ToArray())
                Observation.Observe(a.Soul, new ObservationInput(request.Name, request.Kind)
                {
                    Target = request, Source = ObservationSource.Introspection, Time = Now,
                    Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Responded, 1)
                });
        // 听懂别人对自己说的话（陈述只是 Testimony；请求另由 SocialSystem 评价）。
        var history = a.Soul.World.History;
        for (; a.HistorySeen < history.Count; a.HistorySeen++)
        {
            var e = history[a.HistorySeen];
            if (e.Source != ObservationSource.Testimony) continue;
            switch (e.Details)
            {
                case "闩好了。":
                    KnowConcept(a, a.Report, ObservationSource.Testimony);
                    break;
                case "外面有人在喊开门。":
                    KnowConcept(a, a.Outsider, ObservationSource.Testimony, (NightTags.WantsOpen, true));
                    KnowConcept(a, a.Claim, ObservationSource.Testimony);
                    break;
                case "格雷厄姆说的。闩门。我得守在这。":
                case "铜口里有人喊开门。你去开。":
                    KnowConcept(a, a.Claim, ObservationSource.Testimony);
                    break;
            }
        }
    }

    private void PerceiveAll()
    {
        foreach (var a in _agents) Perceive(a);
    }

    // -------------------------------------------------------------- decisions --

    private void QueueDecisions()
    {
        if (_decisionQueued) return;
        _decisionQueued = true;
        _session.CTB.Schedule(0, () =>
        {
            _decisionQueued = false;
            PerceiveAll();
            foreach (var a in _agents) Decide(a);
        });
    }

    private void Decide(Agent a)
    {
        if (!a.Awake || a.Busy) return;
        var world = a.Soul.World;
        var assessments = DecisionSystem.Assess(world, a.Self);
        var pick = assessments.FirstOrDefault(x => !x.Forbidden)?.Decision;
        var doubt = Doubt(a, pick);
        if (pick is null)
        {
            a.Thought = doubt ?? IdleThought(a) ?? a.Thought;
            return;
        }
        var effect = pick.Effect;
        var habit = a.Habits.FirstOrDefault(h => Ast.Equivalent(Ast.Bind(h.Rule.Effect, new ActionContext(a.Self, Scene: a.Self.Parent)), effect));
        var (label, thought, spot) = habit is not null ? (habit.Label, habit.Thought, habit.Spot) : RequestText(a, pick.Basis);
        var weighing = Weighing(a, pick.Basis);
        if (weighing is not null) thought = habit is not null ? weighing + thought : thought + weighing;
        else if (doubt is not null) thought = doubt + thought;

        var target = Anchor(a, effect);
        var here = RoomOf(a.Body);
        var destination = target is null ? (Room?)null : RoomOfMental(a, target);
        if (destination is { } room && room != here)
        {
            a.Approach = (room, target!);
            var next = NextHop(here, room);
            var passage = a.Mental[_passage[(here, next)]];
            a.Thought = thought;
            a.Spot = "";
            Submit(a, new Effect.Act(ActionOp.Travel, passage), Hops[Key(here, next)] / a.Speed, null, $"{Names.Of(a.Who)}来到{Names.Of(next)}");
            return;
        }
        Settle(a, effect, pick.Basis);
        a.Thought = thought;
        a.Spot = spot;
        // 开口说话不占行动值：在场的人看见对方进门，当即开口。
        Submit(a, effect, effect is Effect.Sequence { Items: [Effect.Say, ..] } ? 0.0 : 1.0, pick.Basis, label);
    }

    /// <summary>别人请自己做事，但 SocialSystem 没有判为接受：这一刻的心声。</summary>
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
            return stranger ? "我凭什么信一个昨天才认识的人？" : $"{sender.Name}的话，我不全信。";
        }
        return null;
    }

    /// <summary>
    /// 心声里的决策依据（自然语言，不出现数值）：手上有几个都愿意照做的说法时，他更信谁；
    /// 都愿意照做却选了自己去看时，为什么不全信。依据来自 SocialSystem 的判定与 DecisionSystem 的取舍。
    /// </summary>
    private string? Weighing(Agent a, Projection basis)
    {
        var pending = a.Soul.World.Projections.Where(p => p.Kind == ProjectionKind.Event && p.Procedure is not null &&
            p.Tags.GetValueOrDefault(Tags.Responded) == 0).ToArray();
        var accepted = pending.Where(r => SocialSystem.Evaluate(a.Soul.World, a.Self, r).Stance == SocialStance.Accept).ToArray();
        string Sender(Projection r) => r.Relations.FirstOrDefault(x => x.Relation == Relations.Subject).Target?.Name ?? "有人";
        if (pending.Contains(basis))
        {
            var others = accepted.Where(r => !ReferenceEquals(r, basis)).ToArray();
            return others.Length == 0 ? null : $"{string.Join("、", others.Select(Sender))}的话我也听了——两边比，我更信{Sender(basis)}。";
        }
        if (accepted.Length >= 1 && ChangesDoorOrLooks(basis))
            return accepted.Length == 1
                ? $"{Sender(accepted[0])}的话我不是不信，可我得亲眼看过才算数——"
                : $"{string.Join("、", accepted.Select(Sender))}两边的说法对不上，我谁都不全信——";
        return null;
    }

    private static bool ChangesDoorOrLooks(Projection basis) => basis.Kind == ProjectionKind.Concept;

    private (string Label, string Thought, string Spot) RequestText(Agent a, Projection request)
    {
        var sender = request.Relations.FirstOrDefault(r => r.Relation == Relations.Subject).Target?.Name ?? "有人";
        return request.Name switch
        {
            "格雷厄姆说的。闩门。我得守在这。" => ($"{Names.Of(a.Who)}照{sender}转述的指令，踢开木楔，门自行合上，插上闩，回去报告",
                $"{sender}说上头下令闩门……那是他亲手记的速记，我认得他的字。我信他。", "door"),
            "铜口里有人喊开门。你去开。" => ($"{Names.Of(a.Who)}照{sender}的话去开门", $"{sender}说铜口里有人喊开门……我信他。去开。", "door"),
            "外面有人在喊开门。" => ($"{Names.Of(a.Who)}照{sender}的判断，让门开着，朝门外喊话",
                $"{sender}说门外有人在喊开门……她的耳朵，我信。门先开着，喊一声看看。", "door"),
            _ => ($"{Names.Of(a.Who)}照{sender}的话做", "", "")
        };
    }

    /// <summary>对同一扇门的几个请求，一旦作出决定就都算答复过了：照了一个，就是没照另一个。</summary>
    private void Settle(Agent a, Effect chosen, Projection basis)
    {
        if (!Ast.Nouns(chosen).OfType<Noun.Reference>().Any(n => ReferenceEquals(n.Projection, a.Mental[_door])) || !ChangesDoor(chosen, a)) return;
        a.DoorSettled = true;
        foreach (var request in a.Soul.World.Projections.Where(p => p.Kind == ProjectionKind.Event && p.Procedure is not null &&
                     p.Tags.GetValueOrDefault(Tags.Responded) == 0 && !ReferenceEquals(p, basis)).ToArray())
            Observation.Observe(a.Soul, new ObservationInput(request.Name, request.Kind)
            {
                Target = request, Source = ObservationSource.Introspection, Time = Now,
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Responded, 1)
            });
    }

    private bool ChangesDoor(Effect effect, Agent a) => effect switch
    {
        Effect.Set s => s.Target is Noun.Reference r && ReferenceEquals(r.Projection, a.Mental[_door]),
        Effect.Sequence s => s.Items.Any(i => ChangesDoor(i, a)),
        Effect.When w => ChangesDoor(w.Body, a),
        Effect.Wait w => ChangesDoor(w.Body, a),
        _ => false
    };

    /// <summary>行动的第一个对象：宿主先把人带到它所在的房间，到了再看一眼、重新决定。</summary>
    private static Projection? Anchor(Agent a, Effect effect) => effect switch
    {
        Effect.Act act => (act.Target as Noun.Reference)?.Projection,
        Effect.Say say => (say.Recipient as Noun.Reference)?.Projection,
        Effect.Set set => (set.Target as Noun.Reference)?.Projection,
        Effect.Sequence s => s.Items.Select(i => Anchor(a, i)).FirstOrDefault(p => p is not null),
        _ => null
    };

    private Room? RoomOfMental(Agent a, Projection p)
    {
        for (var node = p; node is not null; node = node.Parent)
            if (node.Kind == ProjectionKind.Space)
                return _roomOf.FirstOrDefault(r => ReferenceEquals(a.Mental[r.Key], node)).Value;
        return null;
    }

    private static (Room, Room) Key(Room a, Room b) => Hops.ContainsKey((a, b)) ? (a, b) : (b, a);

    private static Room NextHop(Room from, Room to)
    {
        var previous = new Dictionary<Room, Room>();
        var queue = new Queue<Room>([from]);
        var seen = new HashSet<Room> { from };
        while (queue.Count > 0)
        {
            var room = queue.Dequeue();
            if (room == to) break;
            foreach (var ((a, b), _) in Hops)
            {
                var next = a == room ? b : b == room ? a : (Room?)null;
                if (next is { } n && seen.Add(n)) { previous[n] = room; queue.Enqueue(n); }
            }
        }
        var step = to;
        while (previous.TryGetValue(step, out var back) && back != from) step = back;
        return step;
    }

    private void Submit(Agent a, Effect effect, double delay, Projection? basis, string label)
    {
        a.Busy = true;
        a.StartRoom = RoomOf(a.Body);
        var execution = _session.Submit(a.Soul, new ActionIntent(a.Self, effect, delay, Basis: basis));
        _labels[execution] = label;
    }

    private readonly Dictionary<ActionExecution, string> _labels = [];

    private void OnCompleted(ActionExecution execution)
    {
        var a = _agents.First(x => ReferenceEquals(x.Self, execution.Intent.Actor));
        a.Busy = false;
        var label = _labels.GetValueOrDefault(execution, "");
        if (execution.Status != ExecutionStatus.Completed)
        {
            _log.Add($"{Now:0.0} 失败 {label}：{execution.Result?.Failure}");
            if (++a.Failures > 5) a.Awake = false;
        }
        Physics(a);
        // 走到目标所在的房间后，先看一眼再决定。
        if (execution.Intent.Changes is Effect.Act { Operation: ActionOp.Travel } && a.Approach is { } approach && RoomOf(a.Body) == approach.Room)
        {
            a.Approach = null;
            Frame(Names.Of(a.Who), label);
            PerceiveAll();
            a.Spot = SpotOf(approach.Target);
            Submit(a, Look(approach.Target), LookDelay, null, $"{Names.Of(a.Who)}到了{Names.Of(approach.Room)}，先看一眼");
            QueueDecisions();
            return;
        }
        if (execution.Intent.Changes is not Effect.Act { Operation: ActionOp.Inspect }) Frame(Names.Of(a.Who), label);
        QueueDecisions();
    }

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

    private (int, int, int, int) DoorState() => (Tag(_door, NightTags.Open), Tag(_door, NightTags.Wedged), Tag(_door, NightTags.Bolted), Tag(_door, NightTags.CalledOut));

    /// <summary>烟灰、鞋印与湿布：与信念无关的环境后果，由宿主按客观状态结算。</summary>
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
        var shout = Tag(_space[Room.Hall], NightTags.Shout);
        if (shout != _lastShout)
        {
            _lastShout = shout;
            foreach (var a in _agents.Where(a => a != actor && Acoustics.HearsShout(RoomOf(a.Body))))
            {
                KnowConcept(a, a.Shout, ObservationSource.Perception, (NightTags.Bolt, shout == 1), (NightTags.OpenOrder, shout == 2));
                if (!a.Busy) a.Thought = shout == 1 ? "活动厅有人在喊：“闩门！”" : "活动厅有人在喊：“开门！”";
            }
        }
    }

    private void Print(Agent actor, PrintToward toward)
    {
        var sole = actor.Body.Get<BootProperty>()!.Sole;
        if (sole == Sole.Soft) return;
        var trace = _floor.Get<FloorTraceProperty>()!;
        var print = new BootPrint(sole, toward);
        if (!trace.Prints.Contains(print)) trace.Prints.Add(print);
    }

    private void UpdateSmoke()
    {
        if (_smokeIn || Now < SmokeAtDoor || Tag(_door, NightTags.Open) != 1) return;
        _smokeIn = true;
        _floor.Get<StateProperty>()!.Values[NightTags.AshFan] = 1;
        if (Tag(_door, NightTags.Wedged) == 1) _wedge.Get<StateProperty>()!.Values[NightTags.Soot] = 1;
        _runtime.Sync();
        // 鞋印只来自之后在灰上动门的人（见 Physics）；进烟那一刻站着不动的人不留印。
        Frame("", "门缝外的烟灰涌入门厅");
        var shen = _agents.First(a => a.Who == Person.Shen);
        _session.CTB.Schedule(SmokeReachesPantry, () =>
        {
            if (RoomOf(shen.Body) == Room.Pantry) KnowConcept(shen, shen.Smoke, ObservationSource.Perception);
            Frame("", "烟味传到备餐间");
            QueueDecisions();
        });
    }

    // ----------------------------------------------------------------- output --

    /// <summary>没有任何习惯可用、只能等着时的心声：等什么，由他自己的认知决定。</summary>
    private string? IdleThought(Agent a)
    {
        var door = a.Mental[_door];
        var inFoyer = RoomOf(a.Body) == Room.Foyer;
        if (a.Who == Person.Su && inFoyer && door.Tags.GetValueOrDefault(NightTags.Open) == 1 && a.Outsider.Existence == Truth.True)
            return "门外又没声了……我守在这儿，等它再响。";
        if (a.Who == Person.Shen && inFoyer && a.Mental[_cloth].Tags.GetValueOrDefault(NightTags.Wet) == 1 && door.Tags.GetValueOrDefault(NightTags.Open) == 1)
            return "门还开着，湿布压不住——等它合上。";
        if (a.Who == Person.Huo && RoomOf(a.Body) == Room.Hall && a.Order.Tags.GetValueOrDefault(NightTags.HoldTube) == 1)
            return "格雷厄姆叫我守在铜口旁，等下一次通知。我等。";
        return null;
    }

    private string PlainThought(Agent a) => a.Who switch
    {
        Person.Su => "铃……",
        Person.Huo => "铃。",
        Person.Shen => "铃？酵种还没续完。",
        _ => ""
    };

    private void Frame(string actor, string label)
    {
        _frames.Add(new(Now, actor, label,
            _agents.ToImmutableDictionary(a => a.Who, a => RoomOf(a.Body)),
            _agents.ToImmutableDictionary(a => a.Who, a => a.Spot),
            _agents.ToImmutableDictionary(a => a.Who, a => a.Thought),
            Tag(_door, NightTags.Open) == 1, Tag(_door, NightTags.Bolted) == 1, Tag(_floor, NightTags.AshFan) == 1, _smokeIn));
        _log.Add($"{Now:0.0} {label}");
    }

    private MorningTraces Traces()
    {
        var trace = _floor.Get<FloorTraceProperty>()!;
        var fork = _fork.Get<SpatialProperty>()!;
        return new(
            InnerBoltDown: Tag(_door, NightTags.Bolted) == 1,
            AshFan: Tag(_floor, NightTags.AshFan) == 1,
            WedgeTopSooted: Tag(_wedge, NightTags.Soot) == 1,
            WedgeInCorner: Tag(_wedge, NightTags.Kicked) == 1,
            Prints: [..trace.Prints],
            Cloth: Tag(_cloth, NightTags.Placed),
            ClothOverHeel: trace.ClothOverHeel,
            ForkByFrame: fork.Holder is null && fork.Space == _space[Room.Foyer],
            FrameDent: Tag(_frame, NightTags.Dent) == 1,
            ScoreLine1: Tag(_score, NightTags.Line1),
            ScoreLine2: Tag(_score, NightTags.Line2) == 1,
            SlateLine1: Tag(_slate, NightTags.Line1),
            SlateLine2: Tag(_slate, NightTags.Line2),
            CupSugar: Tag(_cups, NightTags.Sugar));
    }
}
