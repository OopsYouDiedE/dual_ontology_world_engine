using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>一名学员：客观身体（Essence）+ 灵魂（SoulEssence，内含自己的 ProjectionWorld）+ 游戏层的少量持久状态。</summary>
public sealed class Person
{
    public Person(P id, Profile? profile = null)
    {
        Id = id;
        Pro = profile ?? Cast.Of(id);
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
    /// <summary>此人亲眼见过、会改变名录库立场的事（§13 第 6 条）：应了唤名之人的样子、中央基座的裂纹、侦探试开机关。</summary>
    public readonly HashSet<string> Witnessed = [];
    /// <summary>饭桌上被信得过的人驳过几回（向某一边的推力，上限 2）。</summary>
    public readonly Dictionary<Ending, int> StanceNudge = [];
    public bool HungryAtCouncil;
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
    public Essence WeightRoom = null!, LockCore = null!, RelayPost = null!, Base = null!, SketchBook = null!;
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
    public StoryRuntime? Story { get; internal set; }
    public ChapterSession Chapter { get; } = new();
    /// <summary>作者侧客观日志（调试可见）。</summary>
    public readonly List<string> Log = [];
    /// <summary>本时段侦探可知的信息（感知、听闻、广播）。</summary>
    public readonly List<string> Feed = [];

    public Game(GameOptions? options = null)
    {
        Options = options ?? new();
        S = new SimulationSession(Rt);
        S.Completed += OnCompleted;
        S.StageCommitted += CapturePresentation;
        People = Enum.GetValues<P>().Select(p => new Person(p, Options.Profiles?.GetValueOrDefault(p))).ToArray();
        BuildWorld();
        foreach (var (key, thing) in Things_) Chapter.Place(key, thing.Place.ToString());
        SyncChapterRooms();
        if (Options.Definition is { } definition)
        {
            definition.Initialize?.Invoke(this);
            Rt.Sync();
            PerceiveAll();
            Story = new StoryRuntime(this, definition.Sequences);
            Story.Poll();
        }
    }

    // ------------------------------------------------------------------ build --

    private static readonly Tag[] BodyVisible = [T.Fed, T.Hungry, T.Weak, T.Sick, T.NightAte, T.Stash, T.OweNote, T.Guarding, T.Prying, T.OwesWater];
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
        Stele = NewThing("誓约庭铺石", Place.Oath, [T.Rubbed]);
        ArchiveDoor = NewThing("名录库之门", Place.ArchiveFront, [T.Open, T.Work]);
        // 名录库之门的三站：配重室、锁芯、传声中继。一站同一时刻只容一个身体（Occupied）；今夜有人在这一站使过劲（Worked）。
        WeightRoom = NewThing("配重室", Place.ArchiveFront, [T.Occupied, T.Worked]);
        LockCore = NewThing("锁芯", Place.ArchiveFront, [T.Occupied, T.Worked]);
        RelayPost = NewThing("传声中继", Place.ArchiveFront, [T.Occupied, T.Worked]);
        // 中央基座：第 5 日起有一道新裂纹（担保衰减的迹象），只有凑近细看才看得见。
        Base = NewThing("中央基座", Place.Hall, [], [T.Crack]);
        SketchBook = NewThing("莫兰的速写本", Place.DormS, [T.Posted]);
        foreach (var place in new[] { Place.Hall, Place.Logistics, Place.Lecture, Place.DormLife, Place.DormW, Place.DormS, Place.DormL })
        {
            var board = NewThing(place == Place.Logistics ? "配给台公告板" : Names.Of(place) + "公告板", place, [T.Accusation, T.Posted]);
            board.Add(new RuleProperty());
            Board[place] = board;
        }
        foreach (var dorm in new[] { Place.DormLife, Place.DormW, Place.DormS, Place.DormL })
            Litter[dorm] = NewThing(Names.Of(dorm) + "的废纸篓", dorm, [T.Crumbs, T.Emptied]);
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
                .Add(T.Picks, other.Pro.Lock >= 3 ? 1 : 0).Add(T.Strong, other.Pro.Strength >= 2 ? 1 : 0).Add(T.Signals, other.Pro.Signal >= 3 ? 1 : 0)
                .Add(T.Branded, 0).Add(T.Accused, 0).Add(T.Suspect, 0);
            // 人物之间的“讲过没讲过”初始一律为否；侦探不在人物互相转述、讲课、讨吃的之列（他是玩家，由玩家自己去问）。
            var marks = Enumerable.Range(1, Fx.MaxSpecs).Select(k => (Fx.Told(k), p.M[Clock])).Concat(new[] { 11, 12, 13, 14 }.Select(l => (Fx.Taught(l), p.M[Clock])))
                .Append((Fx.AskedFood, p.M[Clock])).ToImmutableHashSet();
            Observation.Observe(p.Soul, new ObservationInput(them.Name, them.Kind)
            {
                Target = them, Source = ObservationSource.Recall, Tags = tags,
                DenyRelations = [(Fx.Asked, p.M[Granary]), (Fx.Asked, p.M[ArchiveDoor]), (Fx.AskedLeave, p.M[ArchiveDoor]), .. other == Det ? [] : marks],
                AddRelations = other == Det ? marks : []
            });
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
    public void Set(Essence e, Tag t, int v) { e.Get<StateProperty>()!.Values[t] = v; RealDirty = true; }
    public void Add(Essence e, Tag t, int v) { e.Get<StateProperty>()!.Values[t] = Get(e, t) + v; RealDirty = true; }
    /// <summary>游戏层改了客观状态、还没同步到现实投影（Core 的 ActionRuntime 提交时自己会同步）。</summary>
    public bool RealDirty;

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
        SyncChapterRooms();
        using var _ = Prof.Time("Perceive");
        if (RealDirty) using (Prof.Time("C:Sync")) { Rt.Sync(); RealDirty = false; }
        var space = SpaceOf(p.Body);
        var place = PlaceOf[space];
        var views = new List<Projection> { View[space], View[Clock] };
        foreach (var thing in Things) if (SpaceOf(thing) == space) views.Add(View[thing]);
        foreach (var other in People) if (SpaceOf(other.Body) == space && (other == p || !other.Asleep || Slot != Slot.Night)) views.Add(View[other.Body]);
        if (Options.Hide is { } hide) views.RemoveAll(v => hide(p, v));
        foreach (var (name, item) in Things_)
            if (item.Place == place && views.Contains(View[item.Thing(this)])) Chapter.Observe(p.Id.ToString(), name);
        // 每个时段第一次看，全部看一遍；同一时段里再看，只看状态变了的、或心里位置对不上的对象（性能：§12 第 10 条）。
        var full = Options.Hide is not null || !_fullAt.TryGetValue(p, out var fa) || fa != (Day, Slot);
        if (full) _fullAt[p] = (Day, Slot);
        var see = new List<Projection>(views.Count);
        var mySpace = p.M[space];
        foreach (var v in views)
        {
            var sig = ViewSig(v);
            var stale = _viewEssence.TryGetValue(v, out var e) && e != space && e != Clock && !ReferenceEquals(p.M[e].Parent, mySpace);
            if (full || stale || !_viewSig.TryGetValue((p, v), out var old) || old != sig) { see.Add(v); _viewSig[(p, v)] = sig; }
        }
        if (see.Count > 0) using (Prof.Time("Rt.Observe")) Rt.Observe(p.Soul, see, Now);
        // 以为在这里、其实不在眼前的人：位置不再确知。
        foreach (var other in People.Where(o => o != p && SpaceOf(o.Body) != space && ReferenceEquals(p.M[o.Body].Parent, p.M[space])))
            Observation.Observe(p.Soul, new ObservationInput(p.M[other.Body].Name, ProjectionKind.Body) { Target = p.M[other.Body], Time = Now, Move = true, Parent = null });
        if (Slot == Slot.Night) NightSight(p, place);
        // 板上的规则每个时段判一次（板变了就重判）。
        if (_judgedAt.GetValueOrDefault((p, place)) != (Day, Slot, PostVersion))
        {
            _judgedAt[(p, place)] = (Day, Slot, PostVersion);
            using (Prof.Time("JudgeBoards")) JudgeBoards(p, place);
        }
        // 懂结构的人一眼看出门闩松动（隐藏状态，只有行家看得出）。
        if (place == Place.Logistics && p.Pro.Structure >= 2) RevealLatch(p);
        foreach (var other in People.Where(o => o != p && SpaceOf(o.Body) == space))
        {
            OnMeet(p, other);
            if (p == Det) DetMet(other);
            // 亲眼看见昨夜应了唤名的人今早的样子（§13 第 6 条：立场主要由亲见改变）。
            if (p != Det && Slot is Slot.Morning or Slot.Forenoon && _answered.Contains((Day - 1, other.Id)) && p.Witnessed.Add("hook"))
                Log.Add($"D{Day} {Names.Of(Slot)} {p.Name}亲眼看见{other.Name}神情恍惚（应过唤名）");
        }
        if (p == Det && Slot != Slot.Night)
        {
            DetPassed(place);
            if (!DetSeenAt.TryGetValue(place, out var seen)) DetSeenAt[place] = seen = [];
            foreach (var other in People.Where(o => o != p && SpaceOf(o.Body) == space)) seen.Add(other.Id);
        }
    }

    private readonly Dictionary<(Person, Projection), ulong> _viewSig = [];
    private readonly Dictionary<Person, (int, Slot)> _fullAt = [];
    private readonly Dictionary<(Person, Place), (int, Slot, int)> _judgedAt = [];
    private Dictionary<Projection, Essence>? _viewEssenceCache;
    private Dictionary<Projection, Essence> _viewEssence => _viewEssenceCache ??= View.ToDictionary(kv => kv.Value, kv => kv.Key);
    /// <summary>每次张贴、改写规则板都加一，令“板上内容变了”进入感知签名。</summary>
    public int PostVersion;

    /// <summary>一个可见对象此刻样子的确定性签名（FNV-1a 64 位）：标签、所在、板的版本。</summary>
    private ulong ViewSig(Projection v)
    {
        var h = 14695981039346656037UL;
        void Mix(ulong x) { unchecked { h ^= x; h *= 1099511628211UL; } }
        var tags = 0UL;
        foreach (var (t, x) in v.Tags) unchecked { tags += (Fnv(t.Name) ^ (ulong)(x + 7)) * 1099511628211UL; }
        Mix(tags);
        Mix(v.Parent is null ? 0 : Fnv(v.Parent.Name));
        if (!v.Rules.IsEmpty) { Mix((ulong)v.Rules.Length); Mix((ulong)PostVersion); }
        return h;
    }

    private static readonly Dictionary<string, ulong> _fnv = [];
    private static ulong Fnv(string s)
    {
        lock (_fnv)
        {
            if (_fnv.TryGetValue(s, out var v)) return v;
            var h = 14695981039346656037UL;
            foreach (var c in s) unchecked { h ^= c; h *= 1099511628211UL; }
            return _fnv[s] = h;
        }
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

public sealed record GameOptions
{
    public IReadOnlyDictionary<P, Profile>? Profiles { get; init; }
    public WorldScriptDefinition? Definition { get; init; }
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


/// <summary>第二章命令行与 Godot 宿主共用的玩家可见剧情。</summary>
public static class Chapter2Content
{
    // 学员尚不知道教师的身份；作者侧的称呼只在实现里使用。
    public static string PlayerText(string text) => text.Replace("恶魔", "教师", StringComparison.Ordinal);
    public const string Intro =
        "第 1 日傍晚，四个寝区的隔离门都开了。十六个人在起居厅里第一次见面——人人都以为彼此是初识。\n\n" +
        "另外三组各自答过了关于昨夜的题，答案字面上都对，裂痕却留了下来：文契组为名录库的柜锁，星仪组为沉静室的门，后勤组为白塔柜里少的那瓶试剂。\n\n" +
        "第 2 日清晨，铜口召集全体到中央厅。";

    public const string Instructions =
        "每日五个时段，侦探每个时段只能做一件事；其余十五人各按自己的认知行动。夜里发生的事，只以痕迹、身影与转述到达你。第 7 日，十六人在誓约庭决定名录库归谁、怎么开。";
}

/// <summary>第二章正式剧本入口；原人物、世界与剧情直接编译在本程序集。</summary>
public static class Chapter2Script
{
    public const string Id = "chapter2";
    public const string Title = "第十一课";
    public static Game CreateWorld(GameOptions? options = null) => new(options);
}