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
    bool DoorOpen, bool DoorBolted, bool AshFan, bool SmokeIn, ReplayEvent Event);

/// <summary>重演事件的身份与参与者；显示句型由剧本文案表定义。</summary>
public sealed record ReplayEvent(string Kind, Person? Who = null, Person? Subject = null, string Object = "", Room? Room = null)
{
    public string Label => Chapter1Data.Text("重演·事件·" + Kind,
        ("名", Who is { } who ? Names.Of(who) : ""),
        ("人名", Subject is { } subject ? Names.Of(subject) : Chapter1Data.Text("重演·有人")),
        ("地点", Room is { } room ? Chapter1Data.RoomName(Names.RoomKeys[(int)room]) : ""));
}

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
    public bool DetectiveBoltedOnHuoRequest { get; init; }
    public bool DetectiveRefusedStranger { get; init; }
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
    // STORY-HARDCODE[AWK-006][schema][production]: 固定本案九个空间的夜间通行拓扑与行动耗时。
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
    private readonly Dictionary<Essence, string> _spots = [];
    private readonly List<Agent> _agents = [];
    private readonly List<ReplayFrame> _frames = [];
    private readonly List<string> _log = [];
    private Essence _door = null!, _wedge = null!, _floor = null!, _frame = null!, _fork = null!, _score = null!,
        _slate = null!, _cloth = null!, _cups = null!, _tube = null!;
    private bool _smokeIn, _decisionQueued, _detectiveBoltedOnHuoRequest, _detectiveRefusedStranger;
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

    private sealed record Habit(Rule Rule, ReplayEvent Event, string Thought, string Spot);

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

    private Essence Thing(string id, string name, Room room, params Tag[] tags)
    {
        var essence = new Essence(name);
        essence.Add(new SpatialProperty { Space = _space[room] });
        Init(essence.Add(new StateProperty()), tags);
        _view[essence] = _runtime.Project(essence, name);
        var bindings = Chapter1Data.Root.GetProperty("PRESENTATION").GetProperty("replay_objects");
        if (!bindings.TryGetProperty(id, out var spot)) throw new InvalidDataException("Missing replay object binding: " + id);
        _spots.Add(essence, spot.GetString()!);
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
        _runtime.Project(soul, Chapter1Data.Text("认知·魂名", ("名", Names.Of(who))));
        _view[body] = _runtime.Project(body, Names.Of(who));
        var agent = new Agent(who, soul, body, speed);
        _agents.Add(agent);
        return agent;
    }

    // STORY-HARDCODE[AWK-007][logic][production]: 构造具体案件物件、四名角色的起始地点与鞋底，并将 V1/V2/V6 绑定到世界状态。
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
        _door = Thing("door", Chapter1Data.Text("重演·认知·隔离门"), Room.Foyer, NightTags.Open, NightTags.Wedged, NightTags.Bolted, NightTags.CalledOut);
        _wedge = Thing("wedge", Chapter1Data.Text("重演·认知·木楔"), Room.Foyer, NightTags.Kicked, NightTags.Soot);
        _floor = Thing("floor", Chapter1Data.Text("重演·认知·门厅地面"), Room.Foyer, NightTags.AshFan);
        _floor.Add(new FloorTraceProperty());
        _frame = Thing("frame", Chapter1Data.Text("重演·认知·门框"), Room.Foyer, NightTags.Dent);
        _slate = Thing("slate", Chapter1Data.Text("重演·认知·石板"), Room.Hall, NightTags.Line1, NightTags.Line2);
        _tube = Thing("tube", Chapter1Data.Text("重演·认知·铜口"), Room.Hall);
        _score = Thing("score", Chapter1Data.Text("重演·认知·乐谱"), Room.R08, NightTags.Line1, NightTags.Line2);
        _cloth = Thing("cloth", Chapter1Data.Text("重演·认知·抹布"), Room.Pantry, NightTags.Wet, NightTags.Placed);
        _cups = Thing("cups", Chapter1Data.Text("重演·认知·四只杯子"), Room.Pantry, NightTags.Sugar);

        var su = _v.V1 switch { SuPlace.OwnRoom => Room.R08, SuPlace.Hall => Room.Hall, _ => Room.Pantry };
        var huo = _v.V2 switch { HuoPlace.ByTube => Room.Hall, HuoPlace.OwnRoom => Room.R06, _ => Room.Foyer };
        var detective = MakePerson(Person.Detective, Room.R05, Sole.PoloBoot, _timing.DetectiveSpeed);
        var engineer = MakePerson(Person.Huo, huo, Sole.WorkBoot, _timing.HuoSpeed);
        var chef = MakePerson(Person.Shen, Room.Pantry, Sole.Soft, _timing.ShenSpeed);
        var musician = MakePerson(Person.Su, su, Sole.Soft, _timing.SuSpeed);
        // 苏泠的音叉在她手里。
        _fork = new Essence(Chapter1Data.Text("重演·认知·音叉"));
        _fork.Add(new SpatialProperty { Holder = musician.Body });
        _fork.Add(new StateProperty());
        _view[_fork] = _runtime.Project(_fork, Chapter1Data.Text("重演·认知·音叉"));
        _spots.Add(_fork, Chapter1Data.Root.GetProperty("PRESENTATION").GetProperty("replay_objects").GetProperty("fork").GetString()!);

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
        agent.Alarm = Concept(Chapter1Data.Text("重演·认知·警铃"), ProjectionKind.Event);
        agent.Outsider = Concept(Chapter1Data.Text("重演·认知·门外的人"), ProjectionKind.Soul, NightTags.WantsOpen, NightTags.Help);
        agent.Order = Concept(Chapter1Data.Text("重演·认知·一条指令"), ProjectionKind.Procedure, NightTags.Bolt, NightTags.ForbidOpen, NightTags.HoldTube, NightTags.OpenOrder, NightTags.FromTube);
        agent.Heard = Concept(Chapter1Data.Text("重演·认知·所闻"), ProjectionKind.Event, NightTags.TailOpen, NightTags.TailHelp, NightTags.FullOpen, NightTags.ClearBolt, NightTags.Murmur);
        agent.Shout = Concept(Chapter1Data.Text("重演·认知·活动厅的喊声"), ProjectionKind.Event, NightTags.Bolt, NightTags.OpenOrder);
        agent.DoorHeard = Concept(Chapter1Data.Text("重演·认知·门边所闻"), ProjectionKind.Event, NightTags.Silence);
        agent.Smoke = Concept(Chapter1Data.Text("重演·认知·烟"), ProjectionKind.Material);
        agent.Report = Concept(Chapter1Data.Text("重演·认知·门已闩好的回报"), ProjectionKind.Event);
        agent.Claim = Concept(Chapter1Data.Text("重演·认知·别人对那扇门的说法"), ProjectionKind.Event);

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
    // STORY-HARDCODE[AWK-008][logic][production]: 按 V5/V6 设定侦探对特定同伴的信任偏移与熟人信任。
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

    private void AddHabit(Agent a, string kind, string spot, int weight, Condition condition, Effect effect)
        => a.Habits.Add(new(new Rule(condition, RuleMode.Prefer, effect, weight, Truth.True),
            new(kind, a.Who, Object: spot), Chapter1Data.Text("重演·心声·" + kind), spot));

    // STORY-HARDCODE[AWK-009][logic][production]: 逐人写入昨夜具体行为、认知规则、门/烟/物证互动和请求回应。
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
            AddHabit(a, "inspect_door", "door", 1 + self,
                All(Ex(a.Claim), Not(Ex(a.DoorHeard))), Look(M(_door)));
            AddHabit(a, "close_smoke", "door", 1 + self,
                All(Ex(a.DoorHeard), At(Me, foyer), Ex(a.Smoke), T(M(_door), NightTags.Wedged), Not(T(M(_door), NightTags.CalledOut))),
                Seq(Look(M(_door)), Set(M(_wedge), NightTags.Kicked, 1), Set(M(_door), NightTags.Wedged, 0), Set(M(_door), NightTags.Open, 0)));
            AddHabit(a, "bolt_heard", "door", 1 + self,
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
                new Effect.Say(a.Self, "bolt_report"));
            var openAndReport = Seq(Look(door), Set(door, NightTags.Open, 1),
                new Effect.Act(ActionOp.Travel, M(_passage[(Room.Foyer, Room.Hall)])),
                new Effect.Say(a.Self, "open_report"));
            AddHabit(a, "slate_bolt_tube", "slate", 3,
                All(Ex(a.Order), T(a.Order, NightTags.Bolt), T(a.Order, NightTags.HoldTube), Not(T(slate, NightTags.Line1))),
                Seq(Look(slate), Set(slate, NightTags.Line1, 1)));
            AddHabit(a, "slate_open", "slate", 3,
                All(Ex(a.Order), T(a.Order, NightTags.OpenOrder), Not(T(slate, NightTags.Line1))),
                Seq(Look(slate), Set(slate, NightTags.Line1, 2)));
            AddHabit(a, "slate_bolt_outside", "slate", 1,
                All(Ex(a.Order), T(a.Order, NightTags.Bolt), Not(T(a.Order, NightTags.HoldTube)), Not(T(slate, NightTags.Line1))),
                Seq(Look(slate), Set(slate, NightTags.Line1, 3)));
            AddHabit(a, "shout_bolt", "tube", 2,
                All(T(a.Order, NightTags.FromTube), T(a.Order, NightTags.Bolt), T(slate, NightTags.Line1), Not(T(hall, NightTags.Shout))),
                Seq(Look(hall), Set(hall, NightTags.Shout, 1)));
            AddHabit(a, "shout_open", "tube", 2,
                All(T(a.Order, NightTags.FromTube), T(a.Order, NightTags.OpenOrder), T(slate, NightTags.Line1), Not(T(hall, NightTags.Shout))),
                Seq(Look(hall), Set(hall, NightTags.Shout, 2)));
            AddHabit(a, "ask_bolt", "tube", 4,
                All(T(a.Order, NightTags.FromTube), T(a.Order, NightTags.Bolt), At(Me, hall), At(det, hall), Not(Rel(det, NightRelations.Asked, a.Order))),
                Seq(new Effect.Say(det, "bolt_request", boltAndReport), new Effect.Think(Rel(det, NightRelations.Asked, a.Order))));
            AddHabit(a, "ask_open", "tube", 4,
                All(T(a.Order, NightTags.FromTube), T(a.Order, NightTags.OpenOrder), At(Me, hall), At(det, hall), Not(Rel(det, NightRelations.Asked, a.Order))),
                Seq(new Effect.Say(det, "open_request", openAndReport), new Effect.Think(Rel(det, NightRelations.Asked, a.Order))));
            AddHabit(a, "slate_glyph", "slate", 3,
                All(Ex(a.Report), T(det, NightTags.Glyph), Not(T(slate, NightTags.Line2))),
                Seq(Look(slate), Set(slate, NightTags.Line2, 1)));
            AddHabit(a, "slate_question", "slate", 3,
                All(Ex(a.Report), Not(T(det, NightTags.Glyph)), Not(T(slate, NightTags.Line2))),
                Seq(Look(slate), Set(slate, NightTags.Line2, 2)));
            AddHabit(a, "open_wedge", "door", 3,
                All(Ex(a.Outsider), T(a.Outsider, NightTags.WantsOpen), At(Me, foyer), Not(T(door, NightTags.Open))),
                Seq(Look(door), Set(door, NightTags.Open, 1), Set(door, NightTags.Wedged, 1)));
            AddHabit(a, "bolt_outside", "door", 2,
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
            AddHabit(a, "wet_cloth", "sink", 3,
                All(Ex(a.Smoke), Not(T(cloth, NightTags.Wet))),
                Seq(Look(cloth), new Effect.Act(ActionOp.Take, cloth), Set(cloth, NightTags.Wet, 1)));
            var foyer = M(_space[Room.Foyer]);
            AddHabit(a, "carry_cloth", "door", 3,
                All(T(cloth, NightTags.Wet), Not(T(cloth, NightTags.Placed)), Not(At(Me, foyer))), Look(door));
            // 规则：湿布只压在已闭合的门底；门开着，就等它合上。
            AddHabit(a, "place_cloth", "door", 3,
                All(T(cloth, NightTags.Wet), At(Me, foyer), Not(T(door, NightTags.Open)), Not(T(cloth, NightTags.Placed))),
                Seq(Look(door), new Effect.Act(ActionOp.Place, cloth, foyer), Set(cloth, NightTags.Placed, 1)));
            AddHabit(a, "tea_taste", "sink", 2,
                All(T(cloth, NightTags.Placed), T(M(su.Body), NightTags.Taste), T(M(huo.Body), NightTags.Taste), T(M(detective.Body), NightTags.Taste), Not(T(cups, NightTags.Sugar))),
                Seq(Look(cups), Set(cups, NightTags.Sugar, 1)));
            AddHabit(a, "tea_equal", "sink", 2,
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
            AddHabit(a, "listen_open", "door", 3,
                All(Ex(a.Outsider), T(a.Outsider, NightTags.WantsOpen), Not(T(door, NightTags.Open)), Not(T(frame, NightTags.Dent))),
                Seq(Look(door), Set(door, NightTags.Open, 1), Set(door, NightTags.Wedged, 1)));
            AddHabit(a, "fork_frame", "door", 3,
                All(Ex(a.Outsider), T(a.Outsider, NightTags.WantsOpen), T(door, NightTags.Open), Not(T(frame, NightTags.Dent))),
                Seq(Look(door), Set(frame, NightTags.Dent, 1), new Effect.Act(ActionOp.Place, M(_fork), foyer)));
            AddHabit(a, "score_tail_open", "desk", 2,
                All(T(frame, NightTags.Dent), Ex(a.DoorHeard), T(a.Heard, NightTags.TailOpen), Not(T(door, NightTags.Open)), Not(T(score, NightTags.Line1))),
                Seq(Look(score), Set(score, NightTags.Line1, 1)));
            AddHabit(a, "score_tail_help", "desk", 2,
                All(T(frame, NightTags.Dent), Ex(a.DoorHeard), T(a.Heard, NightTags.TailHelp), Not(T(door, NightTags.Open)), Not(T(score, NightTags.Line1))),
                Seq(Look(score), Set(score, NightTags.Line1, 2)));
            AddHabit(a, "score_full", "desk", 2,
                All(T(frame, NightTags.Dent), Ex(a.DoorHeard), T(a.Heard, NightTags.FullOpen), Not(T(door, NightTags.Open)), Not(T(score, NightTags.Line1))),
                Seq(Look(score), Set(score, NightTags.Line1, 3)));
            // 规则：开了门的苏泠没有任何离开门边的习惯，直到门关上（记谱的习惯要求门已闭合）；她就守在那里。
            AddHabit(a, "ask_keep_open", "door", 4,
                All(At(Me, foyer), At(det, foyer), Ex(a.Outsider), T(door, NightTags.Open), Not(Rel(det, NightRelations.Told, a.Outsider))),
                Seq(new Effect.Say(det, "keep_open_request", keepOpen), new Effect.Think(Rel(det, NightRelations.Told, a.Outsider))));
            AddHabit(a, "score_closed", "desk", 3,
                All(T(score, NightTags.Line1), Not(T(door, NightTags.Open)), Not(T(score, NightTags.Line2))),
                Seq(Look(score), Set(score, NightTags.Line2, 1)));
        }
        foreach (var agent in _agents)
            Observation.Observe(agent.Soul, new ObservationInput(Chapter1Data.Text("重演·认知·习惯", ("名", Names.Of(agent.Who))), ProjectionKind.Concept)
            {
                Source = ObservationSource.Introspection, Existence = Truth.True, Rules = [..agent.Habits.Select(h => h.Rule)]
            });
    }

    // ------------------------------------------------------------------- run --

    // STORY-HARDCODE[AWK-010][logic][production]: 固定警铃、两次声音、烟到门与侦探醒来的事件阶段构成本案昨夜时序。
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
        return new(_v, Traces(), [.._frames], [.._log])
        { DetectiveBoltedOnHuoRequest = _detectiveBoltedOnHuoRequest, DetectiveRefusedStranger = _detectiveRefusedStranger };
    }

    private void Voice(int count)
    {
        var words = Utterance.Of(_v.V4);
        foreach (var a in _agents)
        {
            var hearing = Acoustics.Hear(_v.V3, RoomOf(a.Body));
            Hear(a, words, hearing);
        }
        Frame(new(_v.V3 == VoiceOrigin.Tube
            ? (count == 1 ? "voice_tube" : "voice_tube_repeat")
            : (count == 1 ? "voice_outside" : "voice_outside_repeat")));
        QueueDecisions();
    }

    /// <summary>声路把同一句话变成每个人不同的感知，经 Observation.Observe（来源：感知）写入各自的内部世界。</summary>
    // STORY-HARDCODE[AWK-011][logic][production]: 把本案话语的语义标记映射到人物对指令、门外人和求救的认知。
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
            Hearing.Full => Chapter1Data.Text("重演·心声·full", ("句", words.Full)),
            Hearing.FullMuffled => Chapter1Data.Text("重演·心声·muffled", ("句", words.Full)),
            Hearing.Tail => Chapter1Data.Text("重演·心声·tail", ("句", words.Tail)),
            Hearing.Murmur => Chapter1Data.Text("重演·心声·murmur"),
            _ => a.Who == Person.Detective ? Chapter1Data.Text("重演·心声·alarm_detective") : Chapter1Data.Text("重演·心声·alarm_other")
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
                case "bolt_report":
                    KnowConcept(a, a.Report, ObservationSource.Testimony);
                    break;
                case "keep_open_request":
                    KnowConcept(a, a.Outsider, ObservationSource.Testimony, (NightTags.WantsOpen, true));
                    KnowConcept(a, a.Claim, ObservationSource.Testimony);
                    break;
                case "bolt_request":
                case "open_request":
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
        var (ev, thought, spot) = habit is not null ? (habit.Event, habit.Thought, habit.Spot) : RequestText(a, pick.Basis);
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
            Submit(a, new Effect.Act(ActionOp.Travel, passage), Hops[Key(here, next)] / a.Speed, null, new("arrive", a.Who, Room: next));
            return;
        }
        Settle(a, effect, pick.Basis);
        a.Thought = thought;
        a.Spot = spot;
        // 开口说话不占行动值：在场的人看见对方进门，当即开口。
        Submit(a, effect, effect is Effect.Sequence { Items: [Effect.Say, ..] } ? 0.0 : 1.0, pick.Basis, ev);
    }

    /// <summary>别人请自己做事，但 SocialSystem 没有判为接受：这一刻的心声。</summary>
    // STORY-HARDCODE[AWK-012][logic][production]: 将本案熟悉度与拒绝请求映射为专属心声及案件标记。
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
        string Sender(Projection r) => r.Relations.FirstOrDefault(x => x.Relation == Relations.Subject).Target?.Name ?? Chapter1Data.Text("重演·有人");
        if (pending.Contains(basis))
        {
            var others = accepted.Where(r => !ReferenceEquals(r, basis)).ToArray();
            return others.Length == 0 ? null : Chapter1Data.Text("重演·心声·prefer_sender", ("名单", string.Join("、", others.Select(Sender))), ("人名", Sender(basis)));
        }
        if (accepted.Length >= 1 && ChangesDoorOrLooks(basis))
            return accepted.Length == 1
                ? Chapter1Data.Text("重演·心声·inspect_first", ("人名", Sender(accepted[0])))
                : Chapter1Data.Text("重演·心声·conflicting", ("名单", string.Join("、", accepted.Select(Sender))));
        return null;
    }

    private static bool ChangesDoorOrLooks(Projection basis) => basis.Kind == ProjectionKind.Concept;

    private (ReplayEvent Event, string Thought, string Spot) RequestText(Agent a, Projection request)
    {
        var sender = request.Relations.FirstOrDefault(r => r.Relation == Relations.Subject).Target;
        var subject = _agents.FirstOrDefault(other => ReferenceEquals(a.Mental[other.Body], sender))?.Who;
        var bindings = Chapter1Data.Root.GetProperty("PRESENTATION").GetProperty("replay_requests");
        if (!bindings.TryGetProperty(request.Name, out var binding))
            throw new InvalidDataException("Missing replay request binding: " + request.Name);
        var kind = binding.GetProperty("event").GetString()!;
        var spot = binding.GetProperty("spot").GetString()!;
        var ev = new ReplayEvent(kind, a.Who, subject, spot);
        return (ev, Chapter1Data.Text("重演·心声·" + kind,
            ("人名", subject is { } who ? Names.Of(who) : Chapter1Data.Text("重演·有人"))), spot);
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

    private void Submit(Agent a, Effect effect, double delay, Projection? basis, ReplayEvent ev)
    {
        a.Busy = true;
        a.StartRoom = RoomOf(a.Body);
        var execution = _session.Submit(a.Soul, new ActionIntent(a.Self, effect, delay, Basis: basis));
        _events[execution] = ev;
    }

    private readonly Dictionary<ActionExecution, ReplayEvent> _events = [];

    private void OnCompleted(ActionExecution execution)
    {
        var a = _agents.First(x => ReferenceEquals(x.Self, execution.Intent.Actor));
        a.Busy = false;
        var ev = _events[execution];
        if (execution.Status == ExecutionStatus.Completed && a.Who == Person.Detective &&
            ev.Kind == "request_bolt" && ev.Subject == Person.Huo && Tag(_door, NightTags.Bolted) == 1)
            _detectiveBoltedOnHuoRequest = true;
        if (execution.Status != ExecutionStatus.Completed)
        {
            _log.Add($"{Now:0.0} 失败 {ev.Label}：{execution.Result?.Failure}");
            if (++a.Failures > 5) a.Awake = false;
        }
        Physics(a);
        // 走到目标所在的房间后，先看一眼再决定。
        if (execution.Intent.Changes is Effect.Act { Operation: ActionOp.Travel } && a.Approach is { } approach && RoomOf(a.Body) == approach.Room)
        {
            a.Approach = null;
            Frame(ev);
            PerceiveAll();
            a.Spot = SpotOf(approach.Target);
            Submit(a, Look(approach.Target), LookDelay, null, new("inspect_arrival", a.Who, Room: approach.Room));
            QueueDecisions();
            return;
        }
        if (execution.Intent.Changes is not Effect.Act { Operation: ActionOp.Inspect }) Frame(ev);
        QueueDecisions();
    }

    private string SpotOf(Projection target)
    {
        var essence = _view.Keys.FirstOrDefault(e => _agents.Any(a => a.Mental.TryGetValue(e, out var m) && ReferenceEquals(m, target)));
        if (essence is null) throw new InvalidDataException("Unknown replay projection binding.");
        if (_spots.TryGetValue(essence, out var spot)) return spot;
        if (essence.Kind is ProjectionKind.Space or ProjectionKind.Body or ProjectionKind.Boundary) return "";
        throw new InvalidDataException("Missing replay object station binding: " + essence.TrueName);
    }

    // ---------------------------------------------------------------- physics --

    private (int, int, int, int) DoorState() => (Tag(_door, NightTags.Open), Tag(_door, NightTags.Wedged), Tag(_door, NightTags.Bolted), Tag(_door, NightTags.CalledOut));

    /// <summary>烟灰、鞋印与湿布：与信念无关的环境后果，由宿主按客观状态结算。</summary>
    // STORY-HARDCODE[AWK-015][logic][production]: 本案门状态变化、烟灰、鞋印、湿布压痕与喊声听闻的物理后果。
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
                if (!a.Busy) a.Thought = shout == 1 ? Chapter1Data.Text("重演·心声·hear_bolt_shout") : Chapter1Data.Text("重演·心声·hear_open_shout");
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
        Frame(new("smoke_enter", Object: "floor", Room: Room.Foyer));
        var shen = _agents.First(a => a.Who == Person.Shen);
        _session.CTB.Schedule(SmokeReachesPantry, () =>
        {
            if (RoomOf(shen.Body) == Room.Pantry) KnowConcept(shen, shen.Smoke, ObservationSource.Perception);
            Frame(new("smoke_pantry", Room: Room.Pantry));
            QueueDecisions();
        });
    }

    // ----------------------------------------------------------------- output --

    /// <summary>没有任何习惯可用、只能等着时的心声：等什么，由他自己的认知决定。</summary>
    // STORY-HARDCODE[AWK-016][logic][production]: 按苏泠、沈鼎、霍铎在特定场面中的认知生成等待心声。
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

    private string PlainThought(Agent a) => a.Who switch
    {
        Person.Su => Chapter1Data.Text("重演·心声·alarm_su"),
        Person.Huo => Chapter1Data.Text("重演·心声·alarm_huo"),
        Person.Shen => Chapter1Data.Text("重演·心声·alarm_shen"),
        _ => ""
    };

    private void Frame(ReplayEvent ev)
    {
        if (ev.Who is { } who && ev.Room is null) ev = ev with { Room = RoomOf(_agents.First(a => a.Who == who).Body) };
        _frames.Add(new(Now, ev.Who is { } actor ? Names.Of(actor) : "", ev.Label,
            _agents.ToImmutableDictionary(a => a.Who, a => RoomOf(a.Body)),
            _agents.ToImmutableDictionary(a => a.Who, a => a.Spot),
            _agents.ToImmutableDictionary(a => a.Who, a => a.Thought),
            Tag(_door, NightTags.Open) == 1, Tag(_door, NightTags.Bolted) == 1, Tag(_floor, NightTags.AshFan) == 1, _smokeIn, ev));
        _log.Add($"{Now:0.0} {ev.Label}");
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
