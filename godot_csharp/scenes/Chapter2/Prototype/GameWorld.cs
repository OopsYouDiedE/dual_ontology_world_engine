using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>一名学员：客观身体（Essence）+ 灵魂（SoulEssence，内含自己的 ProjectionWorld）+ 游戏层的少量持久状态。</summary>
public sealed class Person
{
    public Person(P id)
    {
        Id = id;
        Pro = Cast.Of(id);
        Soul = new SoulEssence(Names.Of(id));
        Body = new Essence(Names.Of(id), ProjectionKind.Body);
    }
    public P Id { get; }
    public Profile Pro { get; }
    public SoulEssence Soul { get; }
    public Essence Body { get; }
    public string Name => Names.Of(Id);
    public ProjectionWorld World => Soul.World;
    public Projection Self = null!, Habit = null!, Told = null!, Lore = null!;
    /// <summary>客观对象 → 此人心中的投影（由 WorldRuntime.Observe 返回的引用建立）。</summary>
    public readonly Dictionary<Essence, Projection> M = [];
    public readonly Dictionary<Job, Effect> JobFx = [];
    public readonly Dictionary<RuleSpec, Truth> Acc = [];
    public readonly Dictionary<RuleSpec, string> Why = [];
    public int Desire = 1, Hooks, HungerStreak, MaxHunger, InjuredDays;
    public bool Asleep, Shamed, KnowsHookCost;
    public readonly HashSet<RuleSpec> Vetoed = [], Wagered = [];
    public ActionExecution? Exec;
    public Job? Doing;
    public string DoingWhy = "";
    public double DoingScore;
    public readonly Dictionary<int, bool> Lessons = [];
    public readonly Dictionary<int, Heard> News = [];
    public readonly List<string> Diary = [];
    public readonly HashSet<Place> NightVisited = [];
    public int NightSteps, Interpreted, Whispered, DemonDoubt;
    public bool Weak => HungerStreak >= 2;
}

public sealed record Heard(P From, int Hops, ObservationSource Source, int Day, string Text);

public sealed partial class Game
{
    public readonly WorldRuntime Rt = new();
    public readonly SimulationSession S;
    public readonly Dictionary<Place, Essence> Space = [], Way = [], Board = [], Litter = [];
    public readonly Dictionary<Essence, Projection> View = [];
    public readonly Dictionary<Essence, Place> PlaceOf = [];
    public readonly List<Essence> Things = [];
    public Essence Clock = null!, Granary = null!, Desk = null!, Stove = null!, Jar = null!, Ledger = null!, Bowl = null!, Bed = null!,
        MedCab = null!, OilStore = null!, Valve = null!, Gauge = null!, Blackboard = null!, Stele = null!, ArchiveDoor = null!, NameVoice = null!;
    public readonly Person[] People;
    public Person this[P p] => People[(int)p];
    public Person Det => People[0];
    public IEnumerable<Person> Npcs => People.Skip(1);
    public int Day { get; private set; } = 2;
    public Slot Slot { get; private set; } = Slot.Morning;
    public double Now => S.CTB.Now;
    public readonly GameOptions Options;
    /// <summary>作者侧客观日志（调试可见）。</summary>
    public readonly List<string> Log = [];
    /// <summary>本时段侦探可知的信息（感知、听闻、广播）。</summary>
    public readonly List<string> Feed = [];

    public Game(GameOptions? options = null)
    {
        Options = options ?? new();
        S = new SimulationSession(Rt);
        S.Completed += OnCompleted;
        People = Enum.GetValues<P>().Select(p => new Person(p)).ToArray();
        BuildWorld();
    }

    // ------------------------------------------------------------------ build --

    private static readonly Tag[] BodyVisible = [T.Fed, T.Hungry, T.Weak, T.Sick, T.NightAte, T.Stash, T.OweNote];
    private static readonly Tag[] BodyHidden = [T.GranaryKey, T.MedKey, T.ArchiveKey, T.OilKey, T.Lockpick, T.Craftsman, T.Structure];

    private Essence NewThing(string name, Place place, Tag[] visible, Tag[]? hidden = null)
    {
        var e = new Essence(name);
        e.Add(new SpatialProperty { Space = Space[place] });
        var state = e.Add(new StateProperty());
        foreach (var t in visible) { state.Values[t] = 0; state.Observable.Add(t); }
        foreach (var t in hidden ?? []) state.Values[t] = 0;
        View[e] = Rt.Project(e, name);
        PlaceOf[e] = place;
        Things.Add(e);
        return e;
    }

    private void BuildWorld()
    {
        foreach (var place in Enum.GetValues<Place>())
        {
            var s = new Essence(Names.Of(place), ProjectionKind.Space);
            s.Add(new StateProperty());
            Space[place] = s;
            PlaceOf[s] = place;
            View[s] = Rt.Project(s, Names.Of(place));
        }
        // 每个去处一条路：从任何地方都能到。房内几何与寻路属宿主，这里只保留“到哪里”。
        foreach (var place in Enum.GetValues<Place>())
        {
            var w = new Essence("往" + Names.Of(place), ProjectionKind.Boundary);
            var c = w.Add(new PassageProperty()).Connections;
            foreach (var from in Space.Values) c[from] = Space[place];
            Way[place] = w;
            View[w] = Rt.Project(w, w.TrueName);
        }
        Clock = new Essence("钟");
        var clockState = Clock.Add(new StateProperty());
        foreach (var t in T.SlotTag.Values.Append(T.Day)) { clockState.Values[t] = 0; clockState.Observable.Add(t); }
        View[Clock] = Rt.Project(Clock, "钟");

        Granary = NewThing("粮仓", Place.Logistics, [T.Ration, T.Locked], [T.LatchLoose, T.ToolMarks]);
        Desk = NewThing("配给台", Place.Logistics, [T.Open]);
        Stove = NewThing("灶", Place.Logistics, [T.Cooked]);
        Jar = NewThing("水缸", Place.Logistics, [T.Water]);
        Ledger = NewThing("台账", Place.Logistics, [T.Handed, T.NightRecorded, T.Unrecorded, T.Counted]);
        Bowl = NewThing("诊疗所的碗", Place.Clinic, [T.Porridge, T.Residue]);
        Bed = NewThing("病床", Place.Clinic, [T.Treated]);
        MedCab = NewThing("药柜", Place.Clinic, [T.Medicine]);
        OilStore = NewThing("油库", Place.DormS, [T.Oil]);
        Valve = NewThing("温室阀门", Place.Greenhouse, [T.Work]);
        Gauge = NewThing("温室压力表", Place.Greenhouse, [T.Pressure]);
        Blackboard = NewThing("讲堂黑板", Place.Lecture, [T.Chalk, T.Shoe]);
        Stele = NewThing("誓约庭石碑", Place.Oath, [T.Rubbed]);
        ArchiveDoor = NewThing("名录库之门", Place.ArchiveFront, [T.Open, T.Work]);
        foreach (var place in new[] { Place.Hall, Place.Logistics, Place.Lecture, Place.DormLife, Place.DormW, Place.DormS, Place.DormL })
        {
            var board = NewThing(place == Place.Logistics ? "配给台公告板" : Names.Of(place) + "公告板", place, [T.Accusation, T.Posted]);
            board.Add(new RuleProperty());
            Board[place] = board;
        }
        foreach (var dorm in new[] { Place.DormLife, Place.DormW, Place.DormS, Place.DormL })
            Litter[dorm] = NewThing(Names.Of(dorm) + "的废纸篓", dorm, [T.Crumbs]);
        NameVoice = new Essence("以你之名唤你的声音");
        View[NameVoice] = Rt.Project(NameVoice, "以你之名唤你的声音");

        Set(Granary, T.Ration, Options.StartRations);
        Set(Granary, T.Locked, 1);
        Set(Granary, T.LatchLoose, 1);
        Set(Jar, T.Water, 2);
        Set(MedCab, T.Medicine, Options.StartMedicine);
        Set(OilStore, T.Oil, Options.StartOil);

        foreach (var person in People)
        {
            person.Body.Add(new SpatialProperty { Space = Space[Place.Hall] });
            var state = person.Body.Add(new StateProperty());
            foreach (var t in BodyVisible) { state.Values[t] = 0; state.Observable.Add(t); }
            foreach (var t in BodyHidden) state.Values[t] = 0;
            foreach (var j in Enum.GetValues<Job>()) { state.Values[T.Did(j)] = 0; state.Observable.Add(T.Did(j)); }
            person.Soul.Relations.Add((Relations.Controls, person.Body));
            Rt.Project(person.Soul, person.Name + "之魂");
            View[person.Body] = Rt.Project(person.Body, person.Name);
        }
        Set(this[P.Mora].Body, T.GranaryKey, 1);
        Set(this[P.Mora].Body, T.MedKey, 1);
        Set(this[P.Ward].Body, T.ArchiveKey, 1);
        Set(this[P.Ruan].Body, T.OilKey, 1);
        foreach (var person in People)
        {
            if (person.Pro.Lock >= 3) Set(person.Body, T.Lockpick, 1);
            if (person.Pro.Craftsman) Set(person.Body, T.Craftsman, 1);
            if (person.Pro.Structure >= 2) Set(person.Body, T.Structure, 1);
        }
        Rt.Sync();
        foreach (var person in People) Know(person);
        WriteRules();
        foreach (var person in People) WriteHabits(person);
    }

    /// <summary>第 2 日晨十六人在中央厅会合：每人看见全部可指称的对象与彼此；此后的位置与状态只靠感知更新。</summary>
    private void Know(Person p)
    {
        var sources = View.Keys.ToArray();
        var mental = Rt.Observe(p.Soul, sources.Select(e => View[e]), 0);
        for (var i = 0; i < sources.Length; i++) p.M[sources[i]] = mental[i];
        p.Self = p.M[p.Body];
        Projection Concept(string name, ProjectionKind kind, ObservationSource source) => Observation.Observe(p.Soul,
            new ObservationInput(name, kind) { Source = source, Existence = Truth.True });
        p.Habit = Concept(p.Name + "的习惯", ProjectionKind.Concept, ObservationSource.Introspection);
        p.Told = Concept("听来的规则", ProjectionKind.Concept, ObservationSource.Testimony);
        p.Lore = Concept("课上所学", ProjectionKind.Concept, ObservationSource.Testimony);
        // 对他人的认识：信任初值、谁是工匠（公开所长）、钥匙在谁手里（事故当夜的分配，众所周知）。
        foreach (var other in People.Where(o => o != p))
        {
            var them = p.M[other.Body];
            var tags = ImmutableDictionary<Tag, int?>.Empty
                .Add(Tags.Trust, Cast.BaseTrust(p.Id, other.Id))
                .Add(T.Craftsman, other.Pro.Craftsman ? 1 : 0)
                .Add(T.Branded, 0).Add(T.Accused, 0).Add(T.Suspect, 0);
            Observation.Observe(p.Soul, new ObservationInput(them.Name, them.Kind)
            { Target = them, Source = ObservationSource.Recall, Tags = tags, DenyRelations = [(Fx.Asked, p.M[Granary])] });
        }
        // 自知：钥匙、技艺在自己身上（不可见标签，经内省写入）。
        var self = ImmutableDictionary<Tag, int?>.Empty;
        foreach (var t in BodyHidden) self = self.SetItem(t, Get(p.Body, t));
        self = self.SetItem(T.Branded, 0).SetItem(T.Shamed, 0).SetItem(Tags.Cooperation, p.Pro.Cooperation)
            .SetItem(T.Duty, Cast.OnDuty(p.Id) ? 1 : 0).SetItem(T.InGroup[p.Pro.Group], 1).SetItem(T.Is[p.Id], 1);
        Observation.Observe(p.Soul, new ObservationInput(p.Self.Name, p.Self.Kind) { Target = p.Self, Source = ObservationSource.Introspection, Tags = self });
        // 文契组的旧答案：“是雀儿。”沃德与顾平章把她当作私取之人（第三部分 §2）。
        if (p.Id is P.Ward or P.Gu)
            Observation.Observe(p.Soul, new ObservationInput(p.M[this[P.Quer].Body].Name, ProjectionKind.Body)
            { Target = p.M[this[P.Quer].Body], Source = ObservationSource.Recall, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(T.Branded, 1) });
        if (p.Id == P.Quer)
            Observation.Observe(p.Soul, new ObservationInput(p.Self.Name, p.Self.Kind)
            { Target = p.Self, Source = ObservationSource.Recall, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(T.Branded, 1) });
    }

    // ---------------------------------------------------------------- helpers --

    public static int Get(Essence e, Tag t) => e.Get<StateProperty>()?.Values.GetValueOrDefault(t) ?? 0;
    public void Set(Essence e, Tag t, int v) => e.Get<StateProperty>()!.Values[t] = v;
    public void Add(Essence e, Tag t, int v) => e.Get<StateProperty>()!.Values[t] = Get(e, t) + v;

    public Place Where(Person p) => PlaceOf[SpaceOf(p.Body)];
    public Essence SpaceOf(Essence e)
    {
        for (var x = e; ;)
        {
            if (x.Kind == ProjectionKind.Space) return x;
            var s = x.Get<SpatialProperty>() ?? throw new InvalidOperationException(x.TrueName + " 没有位置");
            x = s.Holder ?? s.Space ?? throw new InvalidOperationException(x.TrueName + " 没有位置");
        }
    }

    /// <summary>此人心中某人的标签（未知返回 null）。</summary>
    public static int? Belief(Person p, Person about, Tag t) => p.M[about.Body].Tags.TryGetValue(t, out var v) ? v : null;
    public static int? Belief(Person p, Projection target, Tag t) => target.Tags.TryGetValue(t, out var v) ? v : null;

    public void Know(Person p, Essence about, Tag t, int value, ObservationSource source, string details = "")
    {
        var target = p.M[about];
        Observation.Observe(p.Soul, new ObservationInput(target.Name, target.Kind)
        { Target = target, Source = source, Time = Now, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(t, value), Details = details });
    }

    /// <summary>
    /// 感知：看见同一空间的对象与人（规则板上的规则随之进入认知，接受状态未知，再由接受判定写回）。
    /// 夜里另有视线：中央厅望得见后勤院与名录库前厅，只能看到身影大小。
    /// </summary>
    public void Perceive(Person p)
    {
        using var _ = Prof.Time("Perceive");
        var space = SpaceOf(p.Body);
        var place = PlaceOf[space];
        var views = new List<Projection> { View[space], View[Clock] };
        foreach (var thing in Things) if (SpaceOf(thing) == space) views.Add(View[thing]);
        foreach (var other in People) if (SpaceOf(other.Body) == space && (other == p || !other.Asleep || Slot != Slot.Night)) views.Add(View[other.Body]);
        if (Options.Hide is { } hide) views.RemoveAll(v => hide(p, v));
        using (Prof.Time("Rt.Observe")) Rt.Observe(p.Soul, views, Now);
        // 以为在这里、其实不在眼前的人：位置不再确知。
        foreach (var other in People.Where(o => o != p && SpaceOf(o.Body) != space && ReferenceEquals(p.M[o.Body].Parent, p.M[space])))
            Observation.Observe(p.Soul, new ObservationInput(p.M[other.Body].Name, ProjectionKind.Body) { Target = p.M[other.Body], Time = Now, Move = true, Parent = null });
        if (Slot == Slot.Night) NightSight(p, place);
        using (Prof.Time("JudgeBoards")) JudgeBoards(p, place);
        // 懂结构的人一眼看出门闩松动（隐藏状态，只有行家看得出）。
        if (place == Place.Logistics && p.Pro.Structure >= 2) RevealLatch(p);
        foreach (var other in People.Where(o => o != p && SpaceOf(o.Body) == space)) { OnMeet(p, other); if (p == Det) DetMet(other); }
    }

    /// <summary>门闩的松紧与工具痕只有凑近细看（Inspect 粮仓）或懂结构的人才看得出。</summary>
    public void RevealLatch(Person p)
    {
        foreach (var t in new[] { T.LatchLoose, T.ToolMarks })
            if ((Belief(p, p.M[Granary], t) ?? -1) != Get(Granary, t)) Know(p, Granary, t, Get(Granary, t), ObservationSource.Perception);
    }

    public void PerceiveAll()
    {
        foreach (var p in People) if (!(Slot == Slot.Night && p.Asleep)) Perceive(p);
    }

    public static readonly ImmutableDictionary<Place, Place[]> SightLines = new Dictionary<Place, Place[]>
    {
        [Place.Hall] = [Place.Logistics, Place.ArchiveFront],
    }.ToImmutableDictionary();

    public readonly List<Sighting> Sightings = [];
    public sealed record Sighting(int Day, P Witness, Place From, Place At, Build Build, P Truth, double Time);

    private void NightSight(Person p, Place here)
    {
        if (!SightLines.TryGetValue(here, out var lines) || p.Asleep) return;
        foreach (var at in lines)
        foreach (var other in People.Where(o => o != p && Where(o) == at && !o.Asleep))
        {
            if (Sightings.Any(s => s.Day == Day && s.Witness == p.Id && s.At == at && s.Truth == other.Id)) continue;
            var s = new Sighting(Day, p.Id, here, at, other.Pro.Build, other.Id, Now);
            Sightings.Add(s);
            var text = $"夜里从{Names.Of(here)}望见{Names.Of(at)}有一个{BuildText(other.Pro.Build)}身影";
            Observation.Observe(p.Soul, new ObservationInput(text, ProjectionKind.Event)
            { Source = ObservationSource.Perception, Existence = Truth.True, Time = Now, Details = text });
            NewsFrom(p, NewsKind.Sighting, text, about: null, build: other.Pro.Build, place: at);
            if (p == Det) Feed.Add(text + "。");
            Log.Add($"D{Day} {Now:0.0} {p.Name} 望见 {Names.Of(at)} 的{BuildText(other.Pro.Build)}身影（实为{other.Name}）");
        }
    }

    public static string BuildText(Build b) => b switch { Build.Small => "瘦小的", Build.Large => "高大的", _ => "中等身量的" };
}

public sealed class GameOptions
{
    public int StartRations { get; init; } = 80;
    public int StartOil { get; init; } = 30;
    public int StartMedicine { get; init; } = 10;
    public int GreenhouseYield { get; init; } = 6;
    public int AcceptThreshold { get; init; } = 2;
    /// <summary>重演用：强制某人对某条规则的接受（只改认知，不改行动）。</summary>
    public Func<Game, Person, RuleSpec, Truth?>? ForceAcceptance { get; init; }
    /// <summary>重演用：某人看不见某个对象（例如茉拉不知道莫兰发烧）。</summary>
    public Func<Person, Projection, bool>? Hide { get; init; }
    public Func<Person, NewsItem, bool>? BlockNews { get; init; }
    public Action<Game>? BeforeNight { get; init; }
    public bool Quiet { get; init; }
}
