using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>一名学员：客观身体（Essence）+ 灵魂（SoulEssence，内含自己的 ProjectionWorld）+ 游戏层的少量持久状态。</summary>
public sealed class Person
{
    public Person(P id, Profile? profile = null, ContinuingCharacter? inherited = null)
    {
        Id = id;
        Pro = profile ?? Cast.Of(id);
        Soul = inherited?.Soul ?? new SoulEssence(Names.Of(id));
        Body = inherited?.Body ?? new Essence(Names.Of(id), ProjectionKind.Body);
        Inherited = inherited is not null;
        if (inherited is not null)
        {
            foreach (var pair in inherited.Known) M[pair.Key] = pair.Value;
            Self = inherited.Self!;
            Hooks = inherited.Hooks; Desire = inherited.Desire; KnowsHookCost = inherited.KnowsRecallCost;
        }
    }
    public bool Inherited { get; }
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
    public int Desire = 1, Hooks;
    private int State(string key) => Body.Get<StateProperty>()?.Values.GetValueOrDefault(new Tag(key)) ?? 0;
    private void State(string key,int value) => (Body.Get<StateProperty>() ?? Body.Add(new StateProperty())).Values[new Tag(key)] = value;
    public int HungerStreak { get => State(nameof(HungerStreak)); set => State(nameof(HungerStreak),value); }
    public int MaxHunger { get => State(nameof(MaxHunger)); set => State(nameof(MaxHunger),value); }
    public int InjuredDays
    {
        get => Body.Get<StateProperty>()?.Values.GetValueOrDefault(new Tag(nameof(InjuredDays))) ?? 0;
        set => (Body.Get<StateProperty>() ?? Body.Add(new StateProperty())).Values[new Tag(nameof(InjuredDays))] = value;
    }
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
    public bool Weak => HungerStreak >= Chapter2Design.Section("execution").GetProperty("constants").GetProperty("WeakAfterDays").GetInt32();
}

/// <summary>某人知道一条知识的方式：从谁那里、隔了几道手、哪天知道的；Slant 是转述时给它添了定性的那个人（成见走样）。</summary>
public sealed record Heard(P From, int Hops, ObservationSource Source, int Day, int Item, P? Slant = null);

public sealed partial class Game
{
    public readonly WorldRuntime Rt;
    public LiveChapterWorld? Continuation { get; }
    public WorldSession? PersistentSession { get; }
    /// <summary>从第一章接续时，各人带进本章的初值；重演以此为起点（见 GameOptions.Inherit）。</summary>
    public IReadOnlyDictionary<P, InheritedSeed>? InheritedSnapshot { get; }
    private readonly ChapterWorldDefinition? _authored;
    public bool InheritedChapter1 => Continuation is not null;
    public readonly SimulationSession S;
    public readonly Dictionary<Place, Essence> Space = [], Way = [], Board = [], Litter = [];
    public readonly Dictionary<Essence, Projection> View = [];
    public readonly Dictionary<Essence, Place> PlaceOf = [];
    public readonly List<Essence> Things = [];
    public Essence WeightRoom = null!, LockCore = null!, RelayPost = null!, Base = null!, SketchBook = null!;
    public Essence Clock = null!, Granary = null!, Desk = null!, Stove = null!, Jar = null!, Ledger = null!, Bowl = null!, Bed = null!,
        MedCab = null!, OilStore = null!, Valve = null!, Gauge = null!, Blackboard = null!, Stele = null!, ArchiveDoor = null!, NameVoice = null!, CopperVoice = null!;
    public readonly Person[] People;
    private readonly Dictionary<P, Person> _people;
    public Person this[P p] => _people[p];
    public Person Det => this[P.Det];
    public IEnumerable<Person> Npcs => People.Where(p => p != Det);
    // 2 是第一章门开启后的始业日排程键；晨段用于上午集合，没有跨夜。
    public int Day { get; private set; } = 2;
    public Slot Slot { get; private set; } = Slot.Morning;
    public double Now => S.CTB.Now;
    public readonly GameOptions Options;
    public StoryRuntime? Story { get; internal set; }
    public ChapterSession Chapter { get; }
    /// <summary>作者侧客观日志（调试可见）。</summary>
    public readonly List<string> Log = [];
    /// <summary>本时段侦探可知的信息（感知、听闻、广播）。</summary>
    public readonly List<string> Feed = [];

    // STORY-HARDCODE[CH2-012][logic][production]: 初始化具体角色、案件实体、持钥身份、时空位置与案件状态。
    public Game(GameOptions? options = null, LiveChapterWorld? continuation = null, ChapterWorldDefinition? authored = null, WorldSession? session = null)
    {
        Options = options ?? new();
        _authored = authored ?? WorldScriptDocument.Parse(Chapter2Design.Json, allowChapterAdapter: true).Single();
        Chapter2Bindings.Validate(_authored);
        _declarations = _authored.Entities.ToDictionary(Chapter2Bindings.Id, StringComparer.Ordinal);
        Continuation = continuation;
        PersistentSession = session ?? continuation?.Session;
        if (continuation is not null && PersistentSession is not null
            && (!ReferenceEquals(continuation.Runtime, PersistentSession.Runtime)
                || !ReferenceEquals(continuation.Simulation, PersistentSession.Simulation)))
            throw new InvalidOperationException("The persistent session must own the same live world and simulation.");
        Chapter = continuation?.Progress ?? new ChapterSession();
        Rt = continuation?.Runtime ?? PersistentSession?.Runtime ?? new WorldRuntime();
        S = continuation?.Simulation ?? PersistentSession?.Simulation ?? new SimulationSession(Rt);
        S.Completed += OnCompleted;
        S.StageCommitted += CapturePresentation;
        S.StageCommitted += execution => { UpdateWorkOccupancy(execution); DeliverAuthoredClaims(execution); };
        People = _authored.Entities.Where(e => e.Kind == "character").Select(Chapter2Bindings.PersonId)
            .OrderBy(p => (int)p).Select(p => new Person(p, Options.Profiles?.GetValueOrDefault(p), continuation?.Characters.GetValueOrDefault(p.ToString()))).ToArray();
        _people = People.ToDictionary(p => p.Id);
        foreach (var declaration in _authored.Entities.Where(e => e.Kind == "character"))
            RegisterEntity(Chapter2Bindings.Id(declaration), this[Chapter2Bindings.PersonId(declaration)].Body);
        LoadAccess();
        BuildWorld();
        BindInvestigation(Chapter2Design.Json);
        InitializeAccess();
        SyncChapterRooms();
        if (continuation is not null)
            InheritedSnapshot = People.ToDictionary(p => p.Id, p => new InheritedSeed(p.Hooks, p.Desire, p.KnowsHookCost, People.Where(o => o != p).ToDictionary(o => o.Id, o => TrustIn(p, o))));
        else if (Options.Inherit is { } seeds)
            foreach (var p in People)
            {
                if (!seeds.TryGetValue(p.Id, out var seed)) continue;
                p.Hooks = seed.Hooks; p.Desire = seed.Desire; p.KnowsHookCost = seed.KnowsHookCost;
                foreach (var (other, trust) in seed.Trust)
                {
                    var them = p.M[this[other].Body];
                    var delta = trust - TrustIn(p, this[other]);
                    if (delta == 0) continue;
                    Observation.Observe(p.Soul, new ObservationInput(them.Name, them.Kind)
                    { Target = them, Source = ObservationSource.Recall, Time = Now, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, them.Tags.GetValueOrDefault(Tags.Trust) + delta) });
                }
            }
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

    private static readonly Tag[] BodyVisible = [T.Shamed, T.Fed, T.Hungry, T.Weak, T.Sick, T.NightAte, T.Stash, T.OweNote, T.Guarding, T.Prying, T.OwesWater];
    private static readonly Tag[] BodyHidden = [T.GranaryKey, T.MedKey, T.ArchiveKey, T.OilKey, T.Lockpick, T.Craftsman, T.Structure];

    private readonly HashSet<Essence> _retainedEntities = new();
    private Essence? Existing(string id)
    {
        var entity = PersistentSession is not null && PersistentSession.TryEntity(id, out var registered)
            ? registered : Continuation?.Entities.GetValueOrDefault(id);
        if (entity is not null) _retainedEntities.Add(entity);
        return entity;
    }
    private Essence NewThing(Place place, Tag[] visible, Tag[]? hidden = null, string id = "")
    {
        var name = _declarations[EntityKey(id)].Name;
        var e = (id.Length > 0 ? Existing("world:" + id) : null) ?? new Essence(name);
        if (e.Get<SpatialProperty>() is null) e.Add(new SpatialProperty { Space = Space[place] });
        var state = e.Get<StateProperty>() ?? e.Add(new StateProperty());
        foreach (var t in visible) { state.Values.TryAdd(t, 0); state.Observable.Add(t); }
        foreach (var t in hidden ?? []) state.Values.TryAdd(t, 0);
        View[e] = Rt.Representations(e).FirstOrDefault() ?? Rt.Project(e, name);
        PlaceOf[e] = place;
        Things.Add(e);
        if (id.Length > 0) RegisterEntity(id, e);
        return e;
    }

    // STORY-HARDCODE[CH2-076][schema][production]: 初始化本章具体空间、案件物件、持钥人身份与知情模型。
    private void BuildWorld()
    {
        BuildDeclaredRooms();
        BuildAccess();
        Clock = BuildStandaloneObject("object.Clock");
        var clockState = Clock.Get<StateProperty>() ?? Clock.Add(new StateProperty());
        foreach (var t in T.SlotTag.Values.Append(T.Day)) { clockState.Values.TryAdd(t, 0); clockState.Observable.Add(t); }

        Granary = NewThing(Place.Logistics, [T.Ration, T.Locked], [T.LatchLoose, T.ToolMarks], id: "object.Granary");
        Desk = NewThing(Place.Logistics, [T.Open], id: "object.Desk");
        Stove = NewThing(Place.Logistics, [T.Cooked], id: "object.Stove");
        Jar = NewThing(Place.Logistics, [T.Water], id: "object.Jar");
        Ledger = NewThing(Place.Logistics, [T.Handed, T.NightRecorded, T.Unrecorded, T.Counted], id: "object.Ledger");
        Bowl = NewThing(Place.Clinic, [T.Porridge, T.Residue], id: "object.Bowl");
        Bed = NewThing(Place.Clinic, [T.Treated], id: "object.Bed");
        MedCab = NewThing(Place.Clinic, [T.Medicine], id: "object.MedCab");
        OilStore = NewThing(Place.DormS, [T.Oil], id: "object.OilStore");
        Valve = NewThing(Place.Greenhouse, [T.Work], id: "object.Valve");
        Gauge = NewThing(Place.Greenhouse, [T.Pressure], id: "object.Gauge");
        Blackboard = NewThing(Place.Lecture, [T.Chalk, T.Shoe], id: "object.Blackboard");
        Stele = NewThing(Place.Oath, [T.Rubbed], id: "object.Stele");
        ArchiveDoor = NewThing(Place.ArchiveFront, [T.Open, T.Work], id: "object.ArchiveDoor");
        // 名录库之门的三站：配重室、锁芯、传声中继。一站同一时刻只容一个身体（Occupied）；今夜有人在这一站使过劲（Worked）。
        WeightRoom = NewThing(Place.ArchiveFront, [T.Occupied, T.Worked], id: "object.WeightRoom");
        LockCore = NewThing(Place.ArchiveFront, [T.Occupied, T.Worked], id: "object.LockCore");
        RelayPost = NewThing(Place.ArchiveFront, [T.Occupied, T.Worked], id: "object.RelayPost");
        // 中央基座：第 5 日起有一道新裂纹（担保衰减的迹象），只有凑近细看才看得见。
        Base = NewThing(Place.Hall, [], [T.Crack], id: "object.Base");
        SketchBook = NewThing(Place.DormS, [T.Posted], id: "object.SketchBook");
        BuildObjectSet("Board.", Board, [T.Accusation, T.Posted], rules: true);
        BuildObjectSet("Litter.", Litter, [T.Crumbs, T.Emptied]);
        NameVoice = BuildStandaloneObject("object.NameVoice");
        // 铜口背后的那个声音：学员看见的只是墙上的铜口，各人心里对它的称呼是“铜口”。
        // 只有被人直接告知，或强证据推理以高把握证实，才会改称（设计第七部分裁定）；本章没有这样的事。
        CopperVoice = Existing("world:object.CopperVoice") ?? new Essence("铜口背后的声音");
        View[CopperVoice] = Rt.Representations(CopperVoice).FirstOrDefault() ?? Rt.Project(CopperVoice, "铜口");
        BuildRemainingObjects();

        if (!_retainedEntities.Contains(Granary)) { Set(Granary, T.Ration, Options.StartRations); Set(Granary, T.Locked, 1); Set(Granary, T.LatchLoose, 1); }
        if (!_retainedEntities.Contains(Jar)) Set(Jar, T.Water, 2);
        if (!_retainedEntities.Contains(MedCab)) Set(MedCab, T.Medicine, Options.StartMedicine);
        if (!_retainedEntities.Contains(OilStore)) Set(OilStore, T.Oil, Options.StartOil);

        var preexistingTags = People.ToDictionary(p => p.Id,p => p.Body.Get<StateProperty>()?.Values.Keys.ToHashSet() ?? []);
        foreach (var person in People)
        {
            // Arrival moves the existing body; it does not replace its running state.
            var spatial = person.Body.Get<SpatialProperty>() ?? person.Body.Add(new SpatialProperty());
            spatial.Holder = null; spatial.Space = Space[AccessArrival(person)];
            var state = person.Body.Get<StateProperty>() ?? person.Body.Add(new StateProperty());
            if (ExecutionDesign.TryGetProperty("body_defaults", out var defaults))
                foreach (var entry in defaults.EnumerateObject())
                { var tag = new Tag(entry.Name); state.Values.TryAdd(tag, entry.Value.GetInt32()); state.Observable.Add(tag); }
            foreach (var t in BodyVisible) { state.Values.TryAdd(t, 0); state.Observable.Add(t); }
            foreach (var t in BodyHidden) state.Values.TryAdd(t, 0);
            foreach (var j in Enum.GetValues<Job>()) { state.Values.TryAdd(T.Did(j), 0); state.Observable.Add(T.Did(j)); }
            if (!person.Soul.Relations.Contains((Relations.Controls, person.Body))) person.Soul.Relations.Add((Relations.Controls, person.Body));
            if (Rt.Representations(person.Soul).Count == 0) Rt.Project(person.Soul, person.Name + "之魂");
            View[person.Body] = Rt.Representations(person.Body).FirstOrDefault() ?? Rt.Project(person.Body, person.Name);
        }
        if (!preexistingTags[P.Mora].Contains(T.GranaryKey)) Set(this[P.Mora].Body, T.GranaryKey, 1);
        if (!preexistingTags[P.Mora].Contains(T.MedKey)) Set(this[P.Mora].Body, T.MedKey, 1);
        if (!preexistingTags[P.Ward].Contains(T.ArchiveKey)) Set(this[P.Ward].Body, T.ArchiveKey, 1);
        if (!preexistingTags[P.Ruan].Contains(T.OilKey)) Set(this[P.Ruan].Body, T.OilKey, 1);
        foreach (var person in People)
        {
            if (person.Pro.Lock >= 3 && !preexistingTags[person.Id].Contains(T.Lockpick)) Set(person.Body, T.Lockpick, 1);
            if (person.Pro.Craftsman && !preexistingTags[person.Id].Contains(T.Craftsman)) Set(person.Body, T.Craftsman, 1);
            if (person.Pro.Structure >= 2 && !preexistingTags[person.Id].Contains(T.Structure)) Set(person.Body, T.Structure, 1);
        }
        foreach (var person in People)
        { Set(person.Body, new Tag("SkillStructure"), person.Pro.Structure); Set(person.Body, new Tag("SkillGreenhouse"), person.Pro.GreenhouseSkill); }
        foreach (var entry in ExecutionDesign.GetProperty("constants").EnumerateObject()) Set(Clock, new Tag(entry.Name), entry.Value.GetInt32());
        Set(Clock, new Tag(nameof(GameOptions.GreenhouseYield)), Options.GreenhouseYield);
        ApplyAuthoredState();
        Rt.Sync();
        foreach (var person in People) Know(person);
        ApplyAuthoredBeliefs();
        WriteRules();
        foreach (var person in People) WriteHabits(person);
    }
    private void ApplyAuthoredState()
    {
        if (_authored is null) return;
        foreach (var declaration in _authored.Entities)
        {
            if (declaration.Kind=="character" && this[Chapter2Bindings.PersonId(declaration)].Inherited) continue;
            var entity=Chapter2Bindings.Resolve(this,declaration);
            if (_retainedEntities.Contains(entity)) continue;
            var state=entity.Get<StateProperty>();
            foreach (var (key,property) in declaration.Properties)
            {
                if(key.StartsWith("state:",StringComparison.Ordinal))
                { state??=entity.Add(new StateProperty());state.Values[new(key[6..])]=property.Value.GetInt32(); }
                else if(key.StartsWith("observable:",StringComparison.Ordinal))
                { state??=entity.Add(new StateProperty());var tag=new Tag(key[11..]);if(property.Value.GetBoolean())state.Observable.Add(tag);else state.Observable.Remove(tag); }
            }
        }
    }
    private void ApplyAuthoredBeliefs()
    {
        if (_authored is null) return;
        foreach (var declaration in _authored.Entities.Where(d=>d.Kind=="character"))
        {
            var person=this[Chapter2Bindings.PersonId(declaration)];
            var tags=declaration.Properties.Where(p=>p.Key.StartsWith("belief:",StringComparison.Ordinal))
                .Where(p=>!person.Inherited||!person.Self.Tags.ContainsKey(new Tag(p.Key[7..])))
                .ToImmutableDictionary(p=>new Tag(p.Key[7..]),p=>(int?)p.Value.Value.GetInt32());
            if(tags.Count>0)Observation.Observe(person.Soul,new ObservationInput(person.Self.Name,person.Self.Kind)
            { Target=person.Self,Source=ObservationSource.Introspection,Time=Now,Tags=tags });
        }
    }

    /// <summary>始业日上午十六人在中央厅会合：每人看见全部可指称的对象与彼此；此后的位置与状态只靠感知更新。</summary>
    // STORY-HARDCODE[CH2-013][logic][production]: 初始化不同具体人物对角色、身份、关系、规则及旧案的认知。
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
        p.Habit = Concept(p.Name + "的习惯", ProjectionKind.Concept, ObservationSource.Introspection);
        p.Told = Concept("听来的规则", ProjectionKind.Concept, ObservationSource.Testimony);
        p.Lore = Concept("课上所学", ProjectionKind.Concept, ObservationSource.Testimony);
        // 对他人的认识：信任初值、谁是工匠（公开所长）、钥匙在谁手里（事故当夜的分配，众所周知）。
        foreach (var other in People.Where(o => o != p))
        {
            if (p.Inherited && previouslyKnown.Contains(other.Body))
            {
                var existing=p.M[other.Body];
                if(!existing.Tags.ContainsKey(Tags.Trust))
                    Observation.Observe(p.Soul,new ObservationInput(existing.Name,existing.Kind)
                    { Target=existing,Source=ObservationSource.Recall,Time=Now,Tags=ImmutableDictionary<Tag,int?>.Empty.Add(Tags.Trust,Cast.BaseTrust(p.Id,other.Id)) });
                continue;
            }
            var them = p.M[other.Body];
            var tags = ImmutableDictionary<Tag, int?>.Empty
                .Add(Tags.Trust, them.Tags.TryGetValue(Tags.Trust, out var trust) ? trust : Cast.BaseTrust(p.Id, other.Id))
                .Add(T.Craftsman, other.Pro.Craftsman ? 1 : 0)
                .Add(T.Picks, other.Pro.Lock >= 3 ? 1 : 0).Add(T.Strong, other.Pro.Strength >= 2 ? 1 : 0).Add(T.Signals, other.Pro.Signal >= 3 ? 1 : 0)
                .Add(T.Branded, them.Tags.GetValueOrDefault(T.Branded)).Add(T.Accused, them.Tags.GetValueOrDefault(T.Accused)).Add(T.Suspect, them.Tags.GetValueOrDefault(T.Suspect));
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
        foreach (var t in BodyHidden) self = self.SetItem(t, p.Self.Tags.TryGetValue(t, out var val) ? val : Get(p.Body, t));
        self = self.SetItem(T.Branded, p.Self.Tags.GetValueOrDefault(T.Branded)).SetItem(T.Shamed, p.Self.Tags.GetValueOrDefault(T.Shamed)).SetItem(Tags.Cooperation, p.Self.Tags.TryGetValue(Tags.Cooperation, out var coop) ? coop : p.Pro.Cooperation)
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
        ObserveLocalAccess(p);
        var views = new List<Projection> { View[space], View[Clock] };
        foreach (var thing in Things) if (SpaceOf(thing) == space) views.Add(View[thing]);
        foreach (var other in People) if (SpaceOf(other.Body) == space && (other == p || !other.Asleep || Slot != Slot.Night)) views.Add(View[other.Body]);
        if (Options.Hide is { } hide) views.RemoveAll(v => hide(p, v));
        foreach (var (name, item) in Things_)
            if (item.Place == place && views.Contains(View[item.Thing])) Chapter.Observe(p.Id.ToString(), name);
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

    // STORY-HARDCODE[CH2-014][logic][production]: 限定具体空间的夜间视线并生成身形目击和专案反馈。
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
            { Source = ObservationSource.Perception, Existence = Truth.True, Time = Now, Details = text });
            NewsFrom(p, NewsKind.Sighting, glimpse, about: null, build: other.Pro.Build, place: at);
            // 侦探自己的守夜所见不逐条报：天亮时由守夜报告按注意力筛选、合并后给出（至多五条）。
            if (p == Det && Slot != Slot.Night) Feed.Add(Stop(Present(glimpse)));
            Log.Add($"D{Day} {Now:0.0} {p.Name} 望见 {Names.Of(at)} 的{BuildText(other.Pro.Build)}身影（实为{other.Name}）");
        }
    }

    public static string BuildText(Build b) => Chapter2Design.BuildFigure(b);
}

public sealed record GameOptions
{
    public IReadOnlyDictionary<P, Profile>? Profiles { get; init; }
    public WorldScriptDefinition? Definition { get; init; }
    /// <summary>五日、一日两顿（始业日只一顿晚饭）：不统一煮粥、各自生领就撑不到议事那天的早饭；认真分工煮粥刚好够（第三部分 §16）。</summary>
    public int StartRations { get; init; } = 99;
    public int StartOil { get; init; } = 30;
    public int StartMedicine { get; init; } = 10;
    public int GreenhouseYield { get; init; } = Chapter2Design.Chapter.GetProperty("design").GetProperty("execution").GetProperty("constants").GetProperty(nameof(GreenhouseYield)).GetInt32();
    public int AcceptThreshold { get; init; } = 2;
    /// <summary>重演用：强制某人对某条规则的接受（只改认知，不改行动）。</summary>
    public Func<Game, Person, RuleSpec, Truth?>? ForceAcceptance { get; init; }
    /// <summary>重演用：某人看不见某个对象（例如茉拉不知道莫兰发烧）。</summary>
    public Func<Person, Projection, bool>? Hide { get; init; }
    public Func<Person, NewsItem, bool>? BlockNews { get; init; }
    public Action<Game>? BeforeNight { get; init; }
    public bool Quiet { get; init; }
    /// <summary>
    /// 重演用：第一章留下的、每个人带进本章的初值（钩、欲望强度、是否知道回忆的代价、对每个人的信任）。
    /// 重演必须新建独立世界，不能共用第一章活着的灵魂引用；但起点要与现实一致，否则任何假设都对不上现实。
    /// </summary>
    public IReadOnlyDictionary<P, InheritedSeed>? Inherit { get; init; }
    /// <summary>重演用：案发夜开始时的现实快照（见 NightSnapshot）。</summary>
    public NightSnapshot? RestoreNight { get; init; }
}

public sealed record InheritedSeed(int Hooks, int Desire, bool KnowsHookCost, IReadOnlyDictionary<P, int> Trust);


/// <summary>第二章命令行与 Godot 宿主共用的玩家可见剧情。</summary>
public static class Chapter2Content
{
    public static string DayName(int day) => Chapter2Design.DayName(day);
    public static string SlotName(int day, Slot slot) => Chapter2Design.SlotDisplay(day, slot);
    public static string TimeName(int day, Slot slot) => $"{DayName(day)} · {SlotName(day, slot)}";
    public static string Intro => Chapter2Design.Intro;
    public static string Instructions => Chapter2Design.Instructions;
}

/// <summary>第二章正式剧本入口；原人物、世界与剧情直接编译在本程序集。</summary>
public static class Chapter2Script
{
    public const string Id = "chapter2";
    public static string Title => Chapter2Design.Text("表达.标题");
    public static Game CreateWorld(GameOptions? options = null) => new(options);
}
