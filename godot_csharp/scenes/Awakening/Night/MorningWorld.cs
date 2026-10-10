using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Awakening;

/// <summary>声路试验中一个听点的感知：听到的原字与声音来处的方向（感知口吻，不含判断）。</summary>
public sealed record HeardAt(Person Who, Room Where, Hearing Hearing, string Text, bool SeemsFromOutside, string Direction);

public enum RecallChoice { Touch, Hesitate, Refuse }

/// <summary>同伴是否触物回忆，由他自己的内部世界经 DecisionSystem 决定。Thought 是自然语言的依据，不含数值。</summary>
public sealed record RecallDecision(Person Who, RecallChoice Choice, string Thought);

/// <summary>可触物回忆的物件、回忆者、唤回的片段与它所证的变量（设计 §5）。</summary>
public sealed record RecallMemory(string Evidence, Person Who, string Fragment, ImmutableArray<int> Confirms);

/// <summary>同伴此刻想不想走过去拿起自己的旧物：由他内部世界的 Rule 经 DecisionSystem 取舍。Thought 是自然语言的依据。</summary>
public sealed record Intention(Person Who, bool Reach, string Thought);

/// <summary>
/// 清晨的现实：玩家实际所在的世界。证物是这里的 Essence；四人各有自己的 ProjectionWorld。
/// 声路试验经 Observation.Observe（感知）进入在场者的认知；触物回忆经 Observation.Observe（回忆）只进入回忆者本人的认知。
/// 昨夜重演从不读写这个世界。
/// </summary>
public sealed partial class MorningWorld
{
    /// <summary>可触物回忆的物件：证物写了“回忆”（谁）、“回忆片段”与“回忆证实”的那几件。</summary>
    public static ImmutableArray<RecallMemory> Memories => Remembered.Value;

    private static readonly Lazy<ImmutableArray<RecallMemory>> Remembered = new(() =>
        [..Chapter1Data.Root.GetProperty("EVIDENCE").EnumerateObject().Where(e => e.Value.TryGetProperty("recall_fragment", out _))
            .Select(e => new RecallMemory(e.Name, (Person)Array.IndexOf(Names.PersonKeys, e.Value.GetProperty("recall").GetString()),
                e.Value.GetProperty("recall_fragment").GetString()!,
                [..e.Value.GetProperty("recall_confirms").EnumerateArray().Select(n => n.GetInt32())]))]);

    /// <summary>活动厅公告框上的“学园守则”四条：恶魔自己所立之规（设计第一部分 §2）。契约不公开。文字写在 chapter_01.story 的“守则”。</summary>
    public static ImmutableArray<string> PostedRules => [..Chapter1Data.Strings("POSTED_RULES")];

    /// <summary>每人的核心欲望（设计第一部分 §5）。恶魔的真话以 Corruption 来源放大它。写在 chapter_01.story 的“人物”。</summary>
    public static string DesireOf(Person who)
        => Chapter1Data.Root.GetProperty("PEOPLE").GetProperty(Names.PersonKeys[(int)who]).GetProperty("desire").GetString()!;

    // 三人倾向各不相同（设计第二部分 §5）：写进各自认知的规则权重，不显示给玩家；初值写在各人的 story 文件（初醒信任、谨慎）。
    // 霍铎：欲望最强，对铜口有一分职业性的信任，读到守则第 1 条也照样碰。
    // 苏泠：欲望强，但最怕被“接上”：不知代价会碰，知道了就拒绝。
    // 沈鼎：护短而谨慎，信身边人不信铜口：不知代价时想碰与不碰分量相当（犹豫），听侦探的；知道了就拒绝。
    private static int DemonTrust(Person who) => Persona.InitialTrust(who, "铜口");
    private static int Caution(Person who) => Persona.Caution(who);
    /// <summary>此人认知里对侦探的信任：沈鼎信的是身边人。</summary>
    private static int TrustInDetective(Person who) => Persona.InitialTrust(who, "侦探");

    public static readonly Tag Strength = new("Strength");
    /// <summary>旧物被拿起又放回：现实中留下的痕迹（摆得和原来不一样）。</summary>
    public static readonly Tag Disturbed = new("被动过");

    private sealed class Mind
    {
        public Projection Self = null!, Desire = null!, Demon = null!, Rule1 = null!, Detective = null!, Object = null!;
        public Effect Touch = null!, Decline = null!, Reach = null!, Stay = null!;
        public Projection AdviceTouch = null!, AdviceDecline = null!, AdviceOwn = null!, Whisper = null!, Settled = null!, Room = null!, Habits = null!;
        public RecallMemory Memory = null!;
    }

    private readonly Dictionary<Person, Mind> _minds = [];
    private readonly Dictionary<Person, Projection> _desires = [];
    private readonly WorldRuntime _runtime = new();
    private readonly Dictionary<Room, Essence> _space = [];
    private readonly Dictionary<Person, SoulEssence> _souls = [];
    private readonly Dictionary<Person, Essence> _bodies = [];
    private readonly Dictionary<string, Essence> _evidence = [];
    private readonly Dictionary<Person, int> _hooks = [];
    private readonly Dictionary<Person, Projection> _recalled = [];
    private readonly Dictionary<string, Room> _evidenceRoom = [];
    private readonly Dictionary<Person, string> _unheeded = [];
    private readonly SimulationSession _session;
    private double _time;

    public CTBSystem StoryTimeline => _session.CTB;
    private readonly Dictionary<Person, ActionExecution> _storyActions = new();
    public bool StoryBusy(Person who) => _storyActions.TryGetValue(who, out var action) && action.Status == ExecutionStatus.Pending;
    public ActionExecution SubmitStory(Person who, Effect effect)
    {
        DailySuspend(Names.PersonKeys[(int)who]);
        var self = _runtime.Observe(_souls[who], _runtime.Representations(_bodies[who]), _session.CTB.Now)[0];
        return _storyActions[who] = _session.Submit(_souls[who], new ActionIntent(self, effect, 1));
    }


    /// <summary>
    /// 同伴面对“触碰旧物、回想”的习惯：欲望越强、越信恶魔，越想碰；天性谨慎、读到守则第 1 条“自负其果”，越不肯碰。
    /// 两种选择都是写进他内部世界的 Rule，由 DecisionSystem 取舍。
    /// </summary>
    // STORY-HARDCODE[AWK-017][logic][production]: 为有具体旧物回忆的人写入欲望、守则、铜口信任、劝告与触物行为规则。
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
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, TrustInDetective(who))
        });
        Projection Advice(string name) => Observation.Observe(soul, new ObservationInput(name, ProjectionKind.Concept)
        {
            Source = ObservationSource.Introspection, Existence = Truth.False
        });
        mind.AdviceTouch = Advice(Chapter1Data.Text("认知·劝碰"));
        mind.AdviceDecline = Advice(Chapter1Data.Text("认知·劝别碰"));
        mind.AdviceOwn = Advice(Chapter1Data.Text("认知·自主取舍"));
        mind.Demon = Observation.Observe(soul, new ObservationInput(Chapter1Data.Text("认知·铜口之人"), ProjectionKind.Soul)
        {
            Source = ObservationSource.Perception, Existence = Truth.True,
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, DemonTrust(who))
        });
        mind.Rule1 = Observation.Observe(soul, new ObservationInput(Chapter1Data.Text("认知·守则代价"), ProjectionKind.Concept)
        {
            Source = ObservationSource.Introspection, Existence = Truth.False
        });
        mind.Touch = new Effect.Act(ActionOp.Inspect, mind.Object);
        mind.Decline = new Effect.Say(mind.Detective, Chapter1Data.Text("认知·不碰"));
        // 走过去、拿起旧物（Take，由 ActionRuntime 结算：人不在物件所在的房间就拿不到），或者守着手头的事。
        mind.Reach = new Effect.Act(ActionOp.Take, mind.Object);
        mind.Stay = new Effect.Say(mind.Detective, Chapter1Data.Text("认知·有事"));
        mind.Whisper = Advice(Chapter1Data.Text("认知·铜口低语"));
        mind.Settled = Advice(Chapter1Data.Text("认知·旧物已定"));
        var rules = new List<Rule>();
        for (var level = 1; level <= 8; level++)
            rules.Add(new Rule(new Condition.Tagged(mind.Desire, Strength, level), RuleMode.Prefer, mind.Touch, 1, Truth.True));
        for (var level = 1; level <= 3; level++)
            rules.Add(new Rule(new Condition.Tagged(mind.Demon, Tags.Trust, level), RuleMode.Prefer, mind.Touch, 1, Truth.True));
        rules.Add(new Rule(new Condition.Constant(Truth.True), RuleMode.Prefer, mind.Decline, Caution(who), Truth.True));
        rules.Add(new Rule(new Condition.Exists(mind.Rule1), RuleMode.Prefer, mind.Decline, 3, Truth.True));
        // 被接受的劝告（SocialSystem 判定）写进认知后，成为取舍的一部分；让他自己拿主意，天性里的谨慎多占一分。
        rules.Add(new Rule(new Condition.Exists(mind.AdviceTouch), RuleMode.Prefer, mind.Touch, 2, Truth.True));
        rules.Add(new Rule(new Condition.Exists(mind.AdviceDecline), RuleMode.Prefer, mind.Decline, 2, Truth.True));
        rules.Add(new Rule(new Condition.Exists(mind.AdviceOwn), RuleMode.Prefer, mind.Decline, 1, Truth.True));
        // 要不要自己走过去拿起它：恶魔对他本人说过那句话（Corruption）、欲望、对恶魔的信任，推他过去；
        // 手头的事、读到过的守则第 1 条拉住他；已经拿过主意的，不再去。
        rules.Add(new Rule(new Condition.Exists(mind.Whisper), RuleMode.Prefer, mind.Reach, 3, Truth.True));
        for (var level = 1; level <= 8; level++)
            rules.Add(new Rule(new Condition.Tagged(mind.Desire, Strength, level), RuleMode.Prefer, mind.Reach, 1, Truth.True));
        for (var level = 1; level <= 3; level++)
            rules.Add(new Rule(new Condition.Tagged(mind.Demon, Tags.Trust, level), RuleMode.Prefer, mind.Reach, 1, Truth.True));
        rules.Add(new Rule(new Condition.Constant(Truth.True), RuleMode.Prefer, mind.Stay, 5, Truth.True));
        rules.Add(new Rule(new Condition.Exists(mind.Rule1), RuleMode.Prefer, mind.Stay, 2, Truth.True));
        rules.Add(new Rule(new Condition.Exists(mind.Settled), RuleMode.Prefer, mind.Stay, 20, Truth.True));
        mind.Habits = Observation.Observe(soul, new ObservationInput(Chapter1Data.Text("认知·旧物习惯", ("名", Names.Of(who))), ProjectionKind.Concept)
        {
            Source = ObservationSource.Introspection, Existence = Truth.True,
            Rules = DualOntologyWorldEngine.Story.RuleLifetimes.Bind(rules,
                Chapter1Data.Root.GetProperty("RULE_LIFETIMES").GetProperty("old_object_habits").GetString()!, "awakening")
        });
        _minds[who] = mind;
    }

    /// <summary>恶魔对某人说一句朝向其欲望的真话：以 Corruption 来源写入此人认知，欲望加强。返回新的强度。</summary>
    public int Amplify(Person who, string words)
    {
        var desire = _desires[who];
        var next = desire.Tags.GetValueOrDefault(Strength) + 1;
        Observation.Observe(_souls[who], new ObservationInput(desire.Name, desire.Kind)
        {
            Target = desire, Source = ObservationSource.Corruption, Time = ++_time, Details = words,
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Strength, next)
        });
        return next;
    }

    public int DesireStrength(Person who) => _desires[who].Tags.GetValueOrDefault(Strength);

    /// <summary>恶魔对某人低语：欲望加强一级，并把“铜口对我说的那句话”写进他的认知（Corruption 来源）。</summary>
    public void Whisper(Person who, string words)
    {
        Amplify(who, words);
        if (!_minds.TryGetValue(who, out var mind)) return;
        Observation.Observe(_souls[who], new ObservationInput(mind.Whisper.Name, mind.Whisper.Kind)
        {
            Target = mind.Whisper, Source = ObservationSource.Corruption, Existence = Truth.True, Time = ++_time, Details = words
        });
    }

    public bool Whispered(Person who) => _minds.TryGetValue(who, out var mind) && mind.Whisper.Existence == Truth.True;

    /// <summary>此刻想不想自己走过去拿起旧物：DecisionSystem 在他自己的内部世界里比较“拿起它”与“守着手头的事”。</summary>
    public Intention? Intend(Person who)
    {
        if (!_minds.TryGetValue(who, out var mind)) return null;
        var options = DecisionSystem.Assess(_souls[who].World, mind.Self).Where(a => !a.Forbidden).ToArray();
        var reach = options.FirstOrDefault(o => Ast.Equivalent(o.Decision.Effect, mind.Reach))?.Decision.Score ?? 0;
        var stay = options.FirstOrDefault(o => Ast.Equivalent(o.Decision.Effect, mind.Stay))?.Decision.Score ?? 0;
        var wants = DesireOf(who);
        var thought = reach > stay
            ? Chapter1Data.Text("心声·想去拿", ("想要", wants), ("物名", mind.Object.Name))
            : mind.Settled.Existence == Truth.True ? Chapter1Data.Text("心声·已定")
            : mind.Rule1.Existence == Truth.True ? Chapter1Data.Text("心声·读过守则")
            : Chapter1Data.Text("心声·手头有事");
        return new(who, reach > stay, thought);
    }

    /// <summary>宿主层把人走到了哪个房间，同步到现实（几何归宿主，房间归现实）。</summary>
    public void Locate(Person who, Room room)
    {
        var spatial = _bodies[who].Get<SpatialProperty>()!;
        if (ReferenceEquals(spatial.Space, _space[room])) return;
        spatial.Space = _space[room];
        _runtime.Sync();
        if (who == Person.Detective) ClearUnattendedWitnesses();
    }

    /// <summary>伸手拿起旧物：经 SimulationSession/ActionRuntime 结算；人不在物件所在的房间时拿不到。</summary>
    public bool Reach(Person who) => _minds.TryGetValue(who, out var mind) && Execute(who, mind.Reach);

    /// <summary>把旧物放回原处：放得和原来不一样（现实里留下“被动过”的痕迹）；此人从此对这件旧物拿定了主意。</summary>
    public bool PutBack(Person who)
    {
        if (!_minds.TryGetValue(who, out var mind)) return false;
        var done = Execute(who, new Effect.Sequence([new Effect.Set(mind.Object, Disturbed, 1), new Effect.Act(ActionOp.Place, mind.Object, mind.Room)]));
        Settle(who);
        return done;
    }

    /// <summary>此人对旧物拿定了主意（碰过或不碰），不会再自己去拿。</summary>
    public void Settle(Person who)
    {
        if (!_minds.TryGetValue(who, out var mind)) return;
        Observation.Observe(_souls[who], new ObservationInput(mind.Settled.Name, mind.Settled.Kind)
        {
            Target = mind.Settled, Source = ObservationSource.Introspection, Existence = Truth.True, Time = ++_time
        });
    }

    public bool Holding(Person who)
        => _minds.TryGetValue(who, out var mind) && ReferenceEquals(_evidence[mind.Memory.Evidence].Get<SpatialProperty>()!.Holder, _bodies[who]);

    public bool IsDisturbed(string evidence)
        => _evidence.TryGetValue(evidence, out var essence) && essence.Get<StateProperty>()!.Values.GetValueOrDefault(Disturbed) > 0;

    private bool Execute(Person who, Effect effect)
    {
        DailySuspend(Names.PersonKeys[(int)who]);
        var mind = _minds[who];
        var execution = _session.Submit(_souls[who], new ActionIntent(mind.Self, effect, 1));
        var steps = 0;
        while (execution.Status == ExecutionStatus.Pending && steps++ < 100 && _session.CTB.Step()) { }
        return execution.Status == ExecutionStatus.Completed && execution.Result is { Succeeded: true };
    }

    /// <summary>玩家把公告框上的守则第 1 条指给某人看：他亲眼读到（感知）。决定仍是他的。</summary>
    // STORY-HARDCODE[AWK-020][logic][production]: 固定以公告框第一条作为角色读到的代价规则。
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
    /// 犹豫的人反问侦探“你说呢？”。侦探的回答作为一句带请求的话（Testimony）进入此人认知；
    /// 是否听进去由 SocialSystem 依他对侦探的信任判定，听进去了才写成他自己的认知。返回判定结果。
    /// advice：touch（劝碰）／decline（劝别碰）／own（由他自己）。
    /// </summary>
    public SocialStance Advise(Person who, string advice)
    {
        if (!_minds.TryGetValue(who, out var mind)) return SocialStance.Reject;
        var soul = _souls[who];
        var (text, procedure, concept) = advice switch
        {
            "touch" => (Chapter1Data.Text("认知·劝告碰"), (Effect?)mind.Touch, mind.AdviceTouch),
            "decline" => (Chapter1Data.Text("认知·劝告别碰"), mind.Decline, mind.AdviceDecline),
            _ => (Chapter1Data.Text("认知·劝告自便"), null, mind.AdviceOwn)
        };
        var said = Observation.Observe(soul, new ObservationInput(text, ProjectionKind.Event)
        {
            Source = ObservationSource.Testimony, Existence = Truth.True, Time = ++_time, Details = text,
            AddRelations = [(Relations.Subject, mind.Detective)], Procedure = procedure
        });
        var stance = procedure is null ? SocialStance.Accept : SocialSystem.Evaluate(soul.World, mind.Self, said).Stance;
        if (stance != SocialStance.Accept) _unheeded[who] = advice;
        if (procedure is not null)
            Observation.Observe(soul, new ObservationInput(said.Name, said.Kind)
            {
                Target = said, Source = ObservationSource.Introspection, Time = _time,
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Responded, 1)
            });
        if (stance == SocialStance.Accept)
            Observation.Observe(soul, new ObservationInput(concept.Name, concept.Kind)
            {
                Target = concept, Source = ObservationSource.Testimony, Existence = Truth.True, Time = _time, Details = text
            });
        return stance;
    }

    /// <summary>此人碰不碰：DecisionSystem 在他自己的内部世界里取舍。两边分量相同则犹豫。</summary>
    // STORY-HARDCODE[AWK-018][logic][production]: 将本案角色取舍、知守则与劝告分支映射到专属心声。
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
        var advisedOwn = mind.AdviceOwn.Existence == Truth.True;
        var unheeded = _unheeded.GetValueOrDefault(memory.Who) == "decline";
        string Say(string key, string who = "") => Chapter1Data.Text(who != "" && Chapter1Data.HasText(key + "·" + who) ? key + "·" + who : key, ("想要", wants));
        var person = Names.PersonKeys[(int)memory.Who];
        var thought = choice switch
        {
            RecallChoice.Touch when advisedTouch => Say("取舍·碰·听劝"),
            RecallChoice.Touch when unheeded => Say("取舍·碰·不听劝", person),
            RecallChoice.Touch when knows => Say("取舍·碰·知守则", person),
            RecallChoice.Touch => Say("取舍·碰"),
            RecallChoice.Refuse when advisedDecline => Say("取舍·不碰·听劝"),
            RecallChoice.Refuse when advisedOwn => Say("取舍·不碰·自便"),
            RecallChoice.Refuse when knows => Say("取舍·不碰·知守则"),
            RecallChoice.Refuse => Say("取舍·不碰"),
            _ => Say("取舍·犹豫")
        };
        return new(memory.Who, choice, thought);
    }

    /// <summary>侦探不在场时的取舍：同样由 DecisionSystem 决定；犹豫的人身边没人可问，便放下。</summary>
    // STORY-HARDCODE[AWK-019][logic][production]: 独处时犹豫即放下是本章角色回忆规则。
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
        var state = essence.Add(new StateProperty());
        foreach (var (tag, value) in facts)
        {
            state.Values[new Tag(tag)] = value;
            state.Observable.Add(new Tag(tag));
        }
        _evidence[id] = essence;
        _runtime.Project(essence, name);
    }

    public ProjectionWorld WorldOf(Person who) => _souls[who].World;
    public int Hooks(Person who) => _hooks[who];
    public int TotalHooks => _hooks.Values.Sum();

    private readonly HashSet<string> _destroyed = new(StringComparer.Ordinal);
    public bool IsDestroyed(string evidence) => _destroyed.Contains(evidence);

    /// <summary>
    /// 现实中毁掉一件证物（非常规动作，混沌测试与日后的玩法用）：它离开房间，此后谁也看不见；
    /// 已经看过它的人，认知里的投影不受影响。没有这件证物时返回 false，由调用方记“未建模”。
    /// </summary>
    public bool Destroy(string evidence)
    {
        if (!_evidence.TryGetValue(evidence, out var essence) || !_destroyed.Add(evidence)) return false;
        essence.Get<SpatialProperty>()!.Space = null;
        return true;
    }

    /// <summary>某人亲眼检视一件证物：只把它的可见状态经感知写入此人的认知。</summary>
    public void Examine(Person who, string evidence)
    {
        if (!_evidence.TryGetValue(evidence, out var essence) || _destroyed.Contains(evidence)) return;
        _runtime.Observe(_souls[who], _runtime.Representations(essence), ++_time);
        var known = _runtime.KnownProjection(_souls[who], essence)!;
        var facts = Chapter1Data.Root.GetProperty("EVIDENCE").GetProperty(evidence).GetProperty("facts").GetString()!;
        Observation.Observe(_souls[who], new(known.Name, known.Kind) { Target = known,
            Source = ObservationSource.Perception, Time = _time, Details = facts });
    }

    /// <summary>
    /// 声路试验：铜口说一句话，各人分别站在不同位置听。每个听者得到的感知由同一张声路表决定，
    /// 经 Observation.Observe 写入其本人的内部世界。
    /// </summary>
    public ImmutableArray<HeardAt> SoundTest(Utterance sentence, IReadOnlyDictionary<Person, Room> positions)
    {
        _time++;
        var heard = ImmutableArray.CreateBuilder<HeardAt>();
        foreach (var (who, room) in positions)
        {
            var hearing = Acoustics.Hear(VoiceOrigin.Tube, room, _joints);
            // 清晨没有警铃：传不到的地方就是什么也听不到。
            var text = hearing is Hearing.AlarmOnly or Hearing.Nothing ? Chapter1Data.Text("声响·无") : Acoustics.Render(sentence, hearing);
            var outside = Acoustics.SeemsFromOutside(hearing);
            Observation.Observe(_souls[who], new ObservationInput(Chapter1Data.Text("认知·声路试验"), ProjectionKind.Event)
            {
                Source = ObservationSource.Perception, Existence = Truth.True, Time = _time, Details = text,
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(new Tag("听来像在门外"), outside ? 1 : 0)
            });
            heard.Add(new(who, room, hearing, text, outside, DirectionOf(room, hearing)));
        }
        return heard.ToImmutable();
    }

    /// <summary>
    /// 声音来处的方向，只说听者感到它从哪边来：门边的接头在门框旁，R-08 的接头在墙里，
    /// 铜口旁就在铜口，备餐间隔着墙从活动厅那边来。不说“门外”这类判断。
    /// </summary>
    public static string DirectionOf(Room room, Hearing hearing)
    {
        var key = hearing switch { Hearing.Full => "方位·完整", Hearing.Tail => "方位·句尾·" + Names.RoomKeys[(int)room], Hearing.Murmur => "方位·低语", _ => "" };
        return key != "" && Chapter1Data.HasText(key) ? Chapter1Data.Text(key) : "";
    }

    /// <summary>触物回忆：本人触碰物件，唤回一个昨夜的认知（来源：回忆）。只写入回忆者本人；记一枚钩。</summary>
    public RecallMemory? Recall(string evidence)
    {
        var memory = Memories.FirstOrDefault(m => m.Evidence == evidence);
        if (memory is null) return null;
        if (_recalled.ContainsKey(memory.Who)) return memory;
        _recalled[memory.Who] = Observation.Observe(_souls[memory.Who], new ObservationInput(Chapter1Data.Text("认知·回忆片段"), ProjectionKind.Event)
        {
            Source = ObservationSource.Recall, Existence = Truth.True, Time = ++_time, Details = memory.Fragment
        });
        _hooks[memory.Who]++;
        return memory;
    }

    public bool HasRecalled(Person who) => _recalled.ContainsKey(who);

    // ---- 现实中可改动的声路：接头（总纲 §六 第三期） ----
    private readonly Dictionary<string, bool> _joints = Acoustics.AuthoredJoints.ToDictionary(j => j.Name, j => j.Loose);
    public IReadOnlyDictionary<string, bool> Joints => _joints;

    /// <summary>拧紧（或拆松）一个接头：此后的声路试验按新状态传声，铜口证物上的“接头松动”随之改变；昨夜的痕迹不变。</summary>
    // STORY-HARDCODE[AWK-021][schema][production]: 将现实声路接头状态投影到 E8 铜口证物的固定事实标签。
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

    /// <summary>此人心里对另一人的信任：其认知中那人投影上的 Trust。</summary>
    public int TrustIn(Person who, Person other)
        => _runtime.KnownProjection(_souls[who], _bodies[other])?.Tags.GetValueOrDefault(Tags.Trust) ?? 0;

    /// <summary>此人亲眼看见了一件事（以事件名记入其认知）。</summary>
    public void Witness(Person who, string name)
    {
        var known = _souls[who].World.Projections.FirstOrDefault(p => p.Name == name);
        Observation.Observe(_souls[who], new ObservationInput(name, ProjectionKind.Event)
        { Target = known, Source = ObservationSource.Perception, Existence = Truth.True, Time = ++_time });
    }

    /// <summary>此人自己想到了一个问题（以概念名记入其认知，来源是推断）。</summary>
    public void Conceive(Person who, string concept)
        => Observation.Observe(_souls[who], new ObservationInput(concept, ProjectionKind.Concept)
        { Target = _souls[who].World.Projections.FirstOrDefault(p => p.Name == concept), Source = ObservationSource.Inference, Existence = Truth.True, Time = ++_time });

    /// <summary>此人认知里有没有这件事或这个概念。</summary>
    public bool Knows(Person who, string name)
        => _souls[who].World.Projections.Any(p => p.Name == name && p.Existence == Truth.True);

    /// <summary>
    /// 一组认定进入此人认知：自己推出的记为推断；别人说的记为证言，挂上说话人，由听者心里对说话人的信任折算。
    /// </summary>
    public void Hear(Person listener, Person speaker, IEnumerable<string> claims)
    {
        var soul = _souls[listener];
        var source = listener == speaker ? ObservationSource.Inference : ObservationSource.Testimony;
        var subject = listener == speaker ? null : _runtime.KnownProjection(soul, _bodies[speaker])
            ?? _runtime.Observe(soul, _runtime.Representations(_bodies[speaker]), _time)[0];
        foreach (var claim in claims)
        {
            var known = soul.World.Projections.FirstOrDefault(p => p.Name == claim);
            Observation.Observe(soul, new ObservationInput(claim, ProjectionKind.Concept)
            {
                Target = known, Source = source, Existence = Truth.True, Time = ++_time,
                AddRelations = subject is null ? [] : [(Relations.Subject, subject)]
            });
        }
    }

    /// <summary>客观世界的指纹：所有证物与身体的状态。重演前后必须一致。</summary>
    public string RealityFingerprint()
    {
        var text = new StringBuilder();
        foreach (var (id, essence) in _evidence.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            text.Append(id).Append(':');
            foreach (var (tag, value) in essence.Get<StateProperty>()!.Values.OrderBy(p => p.Key.Name, StringComparer.Ordinal))
                text.Append(tag.Name).Append('=').Append(value).Append(';');
            text.Append(essence.Get<SpatialProperty>()!.Space?.TrueName).Append('|');
        }
        foreach (var (who, body) in _bodies) text.Append(who).Append('@').Append(body.Get<SpatialProperty>()!.Space?.TrueName).Append('|');
        return Hash(text.ToString());
    }

    /// <summary>某人内部世界的指纹：投影与历史。</summary>
    public string MindFingerprint(Person who)
    {
        var world = _souls[who].World;
        var text = new StringBuilder();
        foreach (var p in world.Projections)
        {
            text.Append(p.Name).Append(p.Existence).Append(p.Parent?.Name);
            foreach (var (tag, value) in p.Tags.OrderBy(t => t.Key.Name, StringComparer.Ordinal)) text.Append(tag.Name).Append(value);
            text.Append('|');
        }
        foreach (var h in world.History) text.Append(h.Source).Append(h.Details).Append(h.Time).Append('|');
        return Hash(text.ToString());
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
}
