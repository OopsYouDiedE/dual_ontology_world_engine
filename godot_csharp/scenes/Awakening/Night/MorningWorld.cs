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
public sealed class MorningWorld
{
    public static readonly ImmutableArray<RecallMemory> Memories =
    [
        new("E5", Person.Su, "门边……有人在喊开门。我想开门让他进来。", [1]),
        new("E7", Person.Huo, "铜口里的声音很清楚……“守在铜口旁，等我下一次通知。”——他再也没有来。", [2, 3, 4]),
        new("E10", Person.Shen, "第二十四道。阿泠的茶不放糖，老霍的放两勺……", [6]),
        new("E1", Person.Detective, "霍铎说闩。我就闩了。我信他。", [5, 6]),
    ];

    /// <summary>活动厅公告框抄录的契约第 1—8 条（恶魔依第 5、6 条公开的部分）。</summary>
    public static readonly ImmutableArray<string> PostedClauses =
    [
        "一、恶魔常驻岛内，不得离岛。",
        "二、经学员本人授权，恶魔封存其在校记忆（在校经历、师生与同学关系、事故全貌）；姓名、生平与入学前技艺不在封存之列。",
        "三、恶魔维持学园基础生存设施的运转（水、炉火、照明的设施本身；不负责分配物资，不创造物资）。",
        "四、恶魔所受约束，唯有学员出于自主的行动方可解除；恶魔不得代为或强迫学员完成解除。",
        "五、恶魔须公开它自己所立之规；规则许诺的路径必须真实可行。",
        "六、恶魔须传授学员理解处境与继续学习所需之知；学员自负其选择的后果。",
        "七、恶魔不得伤害学员身心，不得拘禁学员，不得令学员因无知而死。",
        "八、封存的记忆可凭对应之物恢复；恢复即重新接通，后果自负。",
    ];

    /// <summary>每人的核心欲望（设计第一部分 §5）。恶魔的真话以 Corruption 来源放大它。</summary>
    public static string DesireOf(Person who) => who switch
    {
        Person.Detective => "弄清自己是什么",
        Person.Huo => "超越卡诺循环的动力图纸",
        Person.Shen => "安稳，护住身边的人",
        _ => "从未听过的声音"
    };

    // 三人倾向各不相同（设计第二部分 §5）：写进各自认知的规则权重，不显示给玩家。
    // 霍铎：欲望最强，对恶魔有一分职业性的信任，读到第 8 条也照样碰。
    // 苏泠：欲望强，但最怕被“接上”：不知代价会碰，知道了就拒绝。
    // 沈鼎：护短而谨慎，信身边人不信恶魔：不知代价时想碰与不碰分量相当（犹豫），听侦探的；知道了就拒绝。
    private static int DemonTrust(Person who) => who == Person.Huo ? 2 : 0;
    private static int Caution(Person who) => who switch { Person.Huo => 0, Person.Shen => 3, Person.Su => 1, _ => 2 };
    /// <summary>此人认知里对侦探的信任：沈鼎信的是身边人。</summary>
    private static int TrustInDetective(Person who) => who == Person.Shen ? 1 : 0;

    public static readonly Tag Strength = new("Strength");
    /// <summary>旧物被拿起又放回：现实中留下的痕迹（摆得和原来不一样）。</summary>
    public static readonly Tag Disturbed = new("被动过");

    private sealed class Mind
    {
        public Projection Self = null!, Desire = null!, Demon = null!, Clause8 = null!, Detective = null!, Object = null!;
        public Effect Touch = null!, Decline = null!, Reach = null!, Stay = null!;
        public Projection AdviceTouch = null!, AdviceDecline = null!, AdviceOwn = null!, Whisper = null!, Settled = null!, Room = null!;
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

    public MorningWorld()
    {
        _session = new SimulationSession(_runtime);
        foreach (var room in Enum.GetValues<Room>())
        {
            var space = new Essence(Names.Of(room), ProjectionKind.Space);
            _space[room] = space;
            _runtime.Project(space, Names.Of(room));
        }
        var traces = MorningTraces.Evidence;
        Evidence("E1", "闩上的隔离门", Room.Foyer, ("内闩", traces.InnerBoltDown ? 1 : 0), ("外闩", 1), ("猫眼糊灰", 1));
        Evidence("E2", "门底的灰与木楔", Room.Foyer, ("扇形灰", traces.AshFan ? 1 : 0), ("楔面沾灰", traces.WedgeTopSooted ? 1 : 0), ("楔在角落", traces.WedgeInCorner ? 1 : 0));
        Evidence("E3", "灰上的靴印", Room.Foyer, ("马球靴印朝插闩", 1));
        Evidence("E4", "门底的湿布", Room.Foyer, ("沿门底压成直线", traces.Cloth == 1 ? 1 : 0), ("压住鞋跟", traces.ClothOverHeel ? 1 : 0));
        Evidence("E5", "音叉", Room.Foyer, ("门框凹痕", traces.FrameDent ? 1 : 0));
        Evidence("E6", "苏泠的乐谱", Room.R08, ("第一行", traces.ScoreLine1), ("第二行", traces.ScoreLine2 ? 1 : 0));
        Evidence("E7", "霍铎的石板", Room.Hall, ("第一行", traces.SlateLine1), ("第二行", traces.SlateLine2));
        Evidence("E8", "铜口与管接头", Room.Hall, ("接头松动", 1));
        Evidence("E9", "四只杯子", Room.Pantry, ("按人放糖", traces.CupSugar));
        Evidence("E10", "酵种罐", Room.Pantry, ("刻痕", 24));
        Evidence("E11", "架上的无尘方框", Room.R05, ("窄手印", 1));
        Evidence("E12", "侦探的外衣口袋", Room.R05, ("放大镜", 1));
        foreach (var who in Enum.GetValues<Person>())
        {
            var soul = new SoulEssence(Names.Of(who));
            var body = new Essence(Names.Of(who), ProjectionKind.Body);
            body.Add(new SpatialProperty { Space = _space[who == Person.Detective ? Room.R05 : Room.Hall] });
            soul.Relations.Add((Relations.Controls, body));
            _runtime.Project(soul, Names.Of(who) + "之魂");
            _runtime.Project(body, Names.Of(who));
            _souls[who] = soul;
            _bodies[who] = body;
            _hooks[who] = 0;
            // 醒来时只记得登船。
            Observation.Observe(soul, new ObservationInput("登船", ProjectionKind.Event)
            {
                Source = ObservationSource.Introspection, Existence = Truth.True, Details = "只记得上船。"
            });
            _desires[who] = Observation.Observe(soul, new ObservationInput("想要的：" + DesireOf(who), ProjectionKind.Concept)
            {
                Source = ObservationSource.Introspection, Existence = Truth.True,
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Strength, 1)
            });
        }
        foreach (var memory in Memories.Where(m => m.Who != Person.Detective)) WriteMind(memory);
    }

    /// <summary>
    /// 同伴面对“触碰旧物、回想”的习惯：欲望越强、越信恶魔，越想碰；天性谨慎、知道第 8 条的代价，越不肯碰。
    /// 两种选择都是写进他内部世界的 Rule，由 DecisionSystem 取舍。
    /// </summary>
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
        mind.AdviceTouch = Advice("侦探劝我碰");
        mind.AdviceDecline = Advice("侦探劝我别碰");
        mind.AdviceOwn = Advice("侦探让我自己拿主意");
        mind.Demon = Observation.Observe(soul, new ObservationInput("铜口里的那位", ProjectionKind.Soul)
        {
            Source = ObservationSource.Perception, Existence = Truth.True,
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, DemonTrust(who))
        });
        mind.Clause8 = Observation.Observe(soul, new ObservationInput("契约第八条：恢复即重新接通，后果自负", ProjectionKind.Concept)
        {
            Source = ObservationSource.Introspection, Existence = Truth.False
        });
        mind.Touch = new Effect.Act(ActionOp.Inspect, mind.Object);
        mind.Decline = new Effect.Say(mind.Detective, "不碰。");
        // 走过去、拿起旧物（Take，由 ActionRuntime 结算：人不在物件所在的房间就拿不到），或者守着手头的事。
        mind.Reach = new Effect.Act(ActionOp.Take, mind.Object);
        mind.Stay = new Effect.Say(mind.Detective, "我手头还有事。");
        mind.Whisper = Advice("铜口对我说的那句话");
        mind.Settled = Advice("这件旧物，我已经拿过主意");
        var rules = new List<Rule>();
        for (var level = 1; level <= 8; level++)
            rules.Add(new Rule(new Condition.Tagged(mind.Desire, Strength, level), RuleMode.Prefer, mind.Touch, 1, Truth.True));
        for (var level = 1; level <= 3; level++)
            rules.Add(new Rule(new Condition.Tagged(mind.Demon, Tags.Trust, level), RuleMode.Prefer, mind.Touch, 1, Truth.True));
        rules.Add(new Rule(new Condition.Constant(Truth.True), RuleMode.Prefer, mind.Decline, Caution(who), Truth.True));
        rules.Add(new Rule(new Condition.Exists(mind.Clause8), RuleMode.Prefer, mind.Decline, 3, Truth.True));
        // 被接受的劝告（SocialSystem 判定）写进认知后，成为取舍的一部分；让他自己拿主意，天性里的谨慎多占一分。
        rules.Add(new Rule(new Condition.Exists(mind.AdviceTouch), RuleMode.Prefer, mind.Touch, 2, Truth.True));
        rules.Add(new Rule(new Condition.Exists(mind.AdviceDecline), RuleMode.Prefer, mind.Decline, 2, Truth.True));
        rules.Add(new Rule(new Condition.Exists(mind.AdviceOwn), RuleMode.Prefer, mind.Decline, 1, Truth.True));
        // 要不要自己走过去拿起它：恶魔对他本人说过那句话（Corruption）、欲望、对恶魔的信任，推他过去；
        // 手头的事、读到过的第 8 条拉住他；已经拿过主意的，不再去。
        rules.Add(new Rule(new Condition.Exists(mind.Whisper), RuleMode.Prefer, mind.Reach, 3, Truth.True));
        for (var level = 1; level <= 8; level++)
            rules.Add(new Rule(new Condition.Tagged(mind.Desire, Strength, level), RuleMode.Prefer, mind.Reach, 1, Truth.True));
        for (var level = 1; level <= 3; level++)
            rules.Add(new Rule(new Condition.Tagged(mind.Demon, Tags.Trust, level), RuleMode.Prefer, mind.Reach, 1, Truth.True));
        rules.Add(new Rule(new Condition.Constant(Truth.True), RuleMode.Prefer, mind.Stay, 5, Truth.True));
        rules.Add(new Rule(new Condition.Exists(mind.Clause8), RuleMode.Prefer, mind.Stay, 2, Truth.True));
        rules.Add(new Rule(new Condition.Exists(mind.Settled), RuleMode.Prefer, mind.Stay, 20, Truth.True));
        Observation.Observe(soul, new ObservationInput(Names.Of(who) + "对旧物的习惯", ProjectionKind.Concept)
        {
            Source = ObservationSource.Introspection, Existence = Truth.True, Rules = [..rules]
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
            ? $"它说的那句话，一直在耳朵里。我想要{wants}。……去看看{mind.Object.Name}。"
            : mind.Settled.Existence == Truth.True ? "那件东西，我已经拿过主意了。"
            : mind.Clause8.Existence == Truth.True ? "第八条我读过。手头的事要紧。"
            : "手头的事要紧。";
        return new(who, reach > stay, thought);
    }

    /// <summary>宿主层把人走到了哪个房间，同步到现实（几何归宿主，房间归现实）。</summary>
    public void Locate(Person who, Room room)
    {
        _bodies[who].Get<SpatialProperty>()!.Space = _space[room];
        _runtime.Sync();
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
        var mind = _minds[who];
        var execution = _session.Submit(_souls[who], new ActionIntent(mind.Self, effect, 1));
        var steps = 0;
        while (execution.Status == ExecutionStatus.Pending && steps++ < 100 && _session.CTB.Step()) { }
        return execution.Status == ExecutionStatus.Completed && execution.Result is { Succeeded: true };
    }

    /// <summary>玩家把公告框上的第 8 条指给某人看：他亲眼读到（感知）。决定仍是他的。</summary>
    public void ShowClause8(Person who)
    {
        if (!_minds.TryGetValue(who, out var mind)) return;
        Observation.Observe(_souls[who], new ObservationInput(mind.Clause8.Name, mind.Clause8.Kind)
        {
            Target = mind.Clause8, Source = ObservationSource.Perception, Existence = Truth.True, Time = ++_time, Details = PostedClauses[7]
        });
    }

    public bool KnowsClause8(Person who) => _minds.TryGetValue(who, out var mind) && mind.Clause8.Existence == Truth.True;

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
            "touch" => ("碰吧。", (Effect?)mind.Touch, mind.AdviceTouch),
            "decline" => ("别碰。", mind.Decline, mind.AdviceDecline),
            _ => ("你自己拿主意。", null, mind.AdviceOwn)
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
    public RecallDecision? DecideRecall(string evidence)
    {
        var memory = Memories.FirstOrDefault(m => m.Evidence == evidence);
        if (memory is null || !_minds.TryGetValue(memory.Who, out var mind)) return null;
        var options = DecisionSystem.Assess(_souls[memory.Who].World, mind.Self).Where(a => !a.Forbidden).ToArray();
        var touch = options.FirstOrDefault(o => Ast.Equivalent(o.Decision.Effect, mind.Touch))?.Decision.Score ?? 0;
        var decline = options.FirstOrDefault(o => Ast.Equivalent(o.Decision.Effect, mind.Decline))?.Decision.Score ?? 0;
        var choice = touch > decline ? RecallChoice.Touch : touch < decline ? RecallChoice.Refuse : RecallChoice.Hesitate;
        var wants = DesireOf(memory.Who);
        var knows = mind.Clause8.Existence == Truth.True;
        var advisedTouch = mind.AdviceTouch.Existence == Truth.True;
        var advisedDecline = mind.AdviceDecline.Existence == Truth.True;
        var advisedOwn = mind.AdviceOwn.Existence == Truth.True;
        var unheeded = _unheeded.GetValueOrDefault(memory.Who) == "decline";
        var thought = choice switch
        {
            RecallChoice.Touch when advisedTouch => "你说碰。……我信你。碰。",
            RecallChoice.Touch when unheeded && memory.Who == Person.Huo => "你说别碰。……图纸值这个价。",
            RecallChoice.Touch when unheeded => $"你劝我别碰。……可我们昨天才认识。我想要{wants}。",
            RecallChoice.Touch when knows && memory.Who == Person.Huo => "第八条我读了。接上就接上。……图纸值这个价。",
            RecallChoice.Touch when knows => $"代价我读到了。可我想要{wants}——想得顾不上了。",
            RecallChoice.Touch => $"它说的是真的。我想要{wants}。碰一下，就知道了。",
            RecallChoice.Refuse when advisedDecline => "你说别碰，那就不碰。",
            RecallChoice.Refuse when advisedOwn => "让我自己拿主意……那就不碰。",
            RecallChoice.Refuse when knows => $"第八条写着：恢复即重新接通，后果自负。我想要{wants}，可我不想被它接上。",
            RecallChoice.Refuse => "这东西认得我。我不一定想认得它。",
            _ => $"想要{wants}……可碰了，会不会把谁也一起接上？"
        };
        return new(memory.Who, choice, thought);
    }

    /// <summary>侦探不在场时的取舍：同样由 DecisionSystem 决定；犹豫的人身边没人可问，便放下。</summary>
    public RecallDecision? DecideAlone(string evidence)
    {
        var decision = DecideRecall(evidence);
        if (decision is null || decision.Choice != RecallChoice.Hesitate) return decision;
        return decision with { Choice = RecallChoice.Refuse, Thought = decision.Thought + "……没人可问。放下吧。" };
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

    /// <summary>某人亲眼检视一件证物：只把它的可见状态经感知写入此人的认知。</summary>
    public void Examine(Person who, string evidence)
    {
        if (!_evidence.TryGetValue(evidence, out var essence)) return;
        _runtime.Observe(_souls[who], _runtime.Representations(essence), ++_time);
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
            var hearing = Acoustics.Hear(VoiceOrigin.Tube, room);
            // 清晨没有警铃：传不到的地方就是什么也听不到。
            var text = hearing is Hearing.AlarmOnly or Hearing.Nothing ? "（什么也听不到）" : Acoustics.Render(sentence, hearing);
            var outside = Acoustics.SeemsFromOutside(hearing);
            Observation.Observe(_souls[who], new ObservationInput("铜口试声", ProjectionKind.Event)
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
    public static string DirectionOf(Room room, Hearing hearing) => hearing switch
    {
        Hearing.Full => "从铜口里。",
        Hearing.Tail when room == Room.Foyer => "从那边。（指向门）",
        Hearing.Tail when room == Room.R08 => "从墙那边。（指向门厅的方向）",
        Hearing.Murmur => "从活动厅那边。",
        _ => ""
    };

    /// <summary>触物回忆：本人触碰物件，唤回一个昨夜的认知（来源：回忆）。只写入回忆者本人；记一枚钩。</summary>
    public RecallMemory? Recall(string evidence)
    {
        var memory = Memories.FirstOrDefault(m => m.Evidence == evidence);
        if (memory is null) return null;
        if (_recalled.ContainsKey(memory.Who)) return memory;
        _recalled[memory.Who] = Observation.Observe(_souls[memory.Who], new ObservationInput("昨夜的片段", ProjectionKind.Event)
        {
            Source = ObservationSource.Recall, Existence = Truth.True, Time = ++_time, Details = memory.Fragment
        });
        _hooks[memory.Who]++;
        return memory;
    }

    public bool HasRecalled(Person who) => _recalled.ContainsKey(who);

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
