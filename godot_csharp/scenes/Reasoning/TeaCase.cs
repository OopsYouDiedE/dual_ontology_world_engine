using System.Collections.Immutable;
using System.Text.RegularExpressions;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Game.Reasoning;

// Room geometry and tea mechanics belong to the game, not the independent Core.
public sealed class TeaState : Property
{
    // Compatibility accessor: placement has one authority, Core's SpatialProperty.
    public Essence? Support
    {
        get => Source!.Get<SpatialProperty>() is { } place ? place.Holder ?? place.Space : null;
        set
        {
            var place = Source!.Get<SpatialProperty>()!;
            place.Holder = value?.Kind == ProjectionKind.Body ? value : null;
            place.Space = place.Holder is null ? value : null;
        }
    }
    public int Tea { get; set; }
    public bool Locked { get; set; }
}

public sealed class TeaPerson(string name)
{
    public SoulEssence Soul { get; } = new(name);
    public Essence Body { get; } = new(name, ProjectionKind.Body);
    public Projection Self { get; set; } = null!;
    public Dictionary<Essence, Projection> Known { get; } = [];
    public List<TeaDecisionRule> Rules { get; } = [];
    public string Name => Soul.TrueName;
    public (int X, int Y) Position { get; set; }
    public Essence Site { get; set; } = null!;
    public bool Busy { get; set; }
    public bool InTransit { get; set; }
    public string Activity { get; set; } = "尚未决定";
    public string Reason { get; set; } = "尚未取得观察";
}

public sealed record TeaDecisionRule(Condition Condition, TeaVerb Verb, Essence Target, Essence? Destination, int Priority, int Line);
public sealed record TeaEvidence(string Title, string Text, string Source);
public sealed record TeaFrame(double Time, string Event, IReadOnlyDictionary<Essence, (Essence? Support, int Tea, bool Locked)> Objects,
    IReadOnlyDictionary<TeaPerson, (int X, int Y)> Positions, IReadOnlyDictionary<TeaPerson, string> Thoughts, string[] Differences);

public sealed partial class TeaCase
{
    public static readonly Tag Filled = new("有茶"), Bells = new("听过茶铃"), Chipped = new("有缺口"), Locked = new("已锁");
    public static readonly RelationTag On = new("承托于");
    public CTBSystem Clock { get; } = new();
    public Essence Room { get; } = new("公共研究室", ProjectionKind.Space);
    public WorldRuntime World { get; } = new();
    public SimulationSession Session { get; }
    public List<Essence> Sites { get; } = [];
    public List<Essence> Objects { get; } = [];
    public TeaPerson[] People { get; } = [new("主厨"), new("整理者"), new("医生"), new("画家")];
    public Dictionary<Essence, (int X, int Y)> Points { get; } = [];
    public List<TeaFrame> Frames { get; } = [];
    public List<string> Differences { get; } = [];
    public Essence Window { get; }
public Essence Work { get; }
public Essence Supply { get; }
public Essence Sink { get; }
public Essence Cabinet { get; }
public Essence Chair { get; }
public Essence Basin { get; }
public Essence Tray { get; }
public Essence Entrance { get; }
    public Essence ChipCup { get; }
public Essence LeafCup { get; }
public Essence PlainCup { get; }
public Essence Pot { get; }
public Essence Saucer { get; }
public Essence Cloth { get; }
    public bool Finished { get; private set; }
    public bool Started { get; private set; }
    
    private readonly HashSet<TeaPerson> _scheduled = [];
    private readonly List<Essence> _all = [];
    private int _bell;
    private readonly HashSet<(TeaPerson Person, Essence Cup, Essence Destination)> _uses = [];

    public static readonly TeaEvidence[] Evidence =
    [
        new("尚未开始的下午茶", "研究室摆着茶具，主厨守在备物台旁，似乎遇到了麻烦。", "走入研究室时所见。"),
        new("准备茶点时的速写", "窗边：缺口杯放在续茶碟上。工作桌：叶纹杯。备物台：备用素杯。两只前用杯已有茶，备用杯为空。四人在准备结束时一起核过这些器具；这只是他们最初的记忆，后来谁搬动了什么，不会自动传进别人脑中。", "画家的准备速写及四人的共同核对。"),
        new("配套茶具的说明", "每次茶铃响，碟上的空杯从配套壶取得一份茶。满杯、无杯或空壶时无效。不能造茶或搬物，房间内有效，柜门不切断联系。", "备物台的器物说明与茶壶上的配套标记。"),
        new("三种杯底", "缺口、叶纹、素面可区分三只杯子；杯与碟是能分开移动的物件。", "器具外观。"),
        new("医生的温瓶盆", "盆内是茶，不是清水。盆沿留着一枚不完整的杯底印：轮廓缺去一小段，大小与茶杯相近。", "盆内残留及盆沿不完整的杯底印。"),
        new("画家的染纸盘", "盘底有茶色，草稿上的杯印呈叶纹。纸边注明“第一声铃前试色”。纸盘本身没有向茶壶取茶的配套标记。", "染色草稿、叶纹杯印与试色记录。"),
        new("椅布与湿椅", "椅布上有带缺口的杯底湿圈，圈的一端拖成长痕，布褶与椅面有连续的倾倒痕迹；椅背和椅前地面是干的。湿圈不是椅布整个浸湿后留下的，只有杯底大小的一圈较深。茶渍不能给出精确茶量。", "湿圈、抽动方向与局部干湿分布共同限定的过程。"),
        new("第一次铃后的目击", "第一次独立茶铃后，主厨看到缺口杯在窗边，杯沿映着茶光。它曾经离开过又回来，也不与这次目击矛盾。", "主厨的片段目击；没有全程证明无人搬动。"),
        new("第二次铃后的目击", "第二次独立茶铃后，整理者在窗边所见已是有茶的叶纹杯。", "整理者的片段目击；杯子身份明确。"),
        new("第三次铃前锁柜", "主厨说，在第三次独立茶铃前确认杯子是空的，随后锁柜。", "主厨关于空杯与锁柜时间的陈述。"),
        new("通知与茶铃", "准备结束后响第一声铃；接着处理换杯等事，喊收布，再响第二声铃。整理闲置器具后喊收柜，最后响第三声铃。每次都等正在忙的人做完，没有必须赶上的隐藏秒数。", "公共研究室的值日记录。"),
        new("主厨的疑问", "主厨说茶壶已空、窗边椅子湿了，四个人都说没有喝茶。", "主厨的开场陈述，仍需调查核对。"),
        new("医生借茶温瓶", "医生说自己把借来的茶倒入温瓶盆，没有饮用。", "向医生询问茶具用途。"),
        new("画家借茶染纸", "画家说自己把茶倒入纸盘，给道具纸染旧，没有饮用。", "向画家询问茶的用途。"),
        new("整理者记得空杯", "整理者坚持：备用杯收进柜子时是空的。", "整理者的陈述；需与主厨及器物说明交叉核对。"),
        new("柜内满杯的说法", "主厨说，现在柜内有一只满杯，而自己先前锁柜时它是空的。", "主厨对柜内状态的陈述。"),
        new("器具柜的门锁", "主厨用钥匙打开检查口：素杯已经满了，底下压着一只带配套标记的碟。锁舌没有撬痕。碟子的配套标记与备物台说明上的标记相同。", "锁舌、柜内上下承托与器物标记。"),
        new("窗边的叶纹杯", "窗边目前是叶纹杯，杯中有茶。", "窗边现场。"),
        new("洗具台的缺口杯", "缺口杯空着留在洗具台上。", "洗具台现场。"),
        new("备物台的空壶", "茶壶已经空了；准备刻线记着壶中尚有三份，两只前用杯各有一份。现在窗边和柜内各有一杯。其余茶必须有去处，不能只用“魔法”抹掉。", "备物台刻线、准备记录与现存器物。"),
        new("主厨最后一次看椅布", "主厨准备时确认椅布上没有东西，后来一直背对窗边核对备物台。收布通知响起，他沿用先前的印象，没有重新看布面。他说：“我记得那里空着，就没再停下来查看。”", "拿椅布痕迹追问主厨后取得的陈述。"),
        new("换杯不是收杯", "整理者要把有缺口的杯子换下。完好的杯子拿来之前，他必须腾出茶碟，因此把旧杯暂搁在附近承托物上。他说自己没有倒茶，也没有抽椅布；旧杯后来才作为空杯送洗。", "对照椅布杯印与两次目击后取得的陈述。"),
        new("大家听得到的通知", "准备时可以借茶。第一声铃后开始换杯；随后有收布通知，再响第二声铃。第二声铃后收闲置器具；收柜通知之后主厨才锁柜，随后第三声铃响。通知是人物听得到的事情，不是模拟进度提示。", "主厨复述的值日通知顺序，与公共记录一致。")
    ];

    public TeaCase(bool aftermath = false)
    {
        Session = new(World, Clock);
        Essence Site(string name, int x, int y) { var e = new Essence(name, ProjectionKind.Space); e.Add(new SpatialProperty { Space = Room }); e.Add(new TeaState()); Sites.Add(e); Points[e] = (x, y); return e; }
        Window = Site("窗边", 112, 160); Work = Site("工作桌", 320, 256); Supply = Site("备物台", 480, 160);
        Sink = Site("洗具台", 528, 304); Cabinet = Site("器具柜", 544, 112); Chair = Site("椅面", 112, 224);
        Basin = Site("温瓶盆", 176, 304); Tray = Site("染纸盘", 400, 304); Entrance = Site("门口", 320, 384);
        Essence Item(string name, Essence support, int tea = 0) { var e = new Essence(name); e.Add(new SpatialProperty { Space = support }); e.Add(new TeaState { Tea = tea }); Objects.Add(e); return e; }
        Saucer = Item("续茶碟", Window); Cloth = Item("椅布", Chair); ChipCup = Item("缺口杯", Saucer, 1);
        LeafCup = Item("叶纹杯", Work, 1); PlainCup = Item("素杯", Supply); Pot = Item("茶壶", Supply, 3);
        _all.AddRange(Sites); _all.AddRange(Objects);
        var starts = new[] { Supply, Chair, Basin, Tray };
        for (var i = 0; i < People.Length; i++)
        {
            var p = People[i]; p.Site = starts[i]; p.Position = Points[p.Site];
            p.Body.Add(new SpatialProperty { Space = p.Site }); p.Body.Add(new TeaState());
            p.Self = Observation.Observe(p.Soul, new(p.Name, ProjectionKind.Body) { Existence = Truth.True, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Bells, 0) });
            // Names are known, but unseen state remains unknown. No objective values enter a decision here.
            foreach (var e in _all) p.Known[e] = Observation.Observe(p.Soul, new(e.TrueName, e.Kind) { Existence = Truth.Unknown });
            // The shared preparation inventory is a witnessed initial memory, not omniscience.
            foreach (var place in Sites) { p.Site = place; Perceive(p, preparation: true); }
            p.Site = starts[i];
        }
        if (aftermath)
        {
            State(ChipCup).Support = Sink; State(ChipCup).Tea = 0; State(LeafCup).Support = Window;
            State(Saucer).Support = Cabinet; State(PlainCup).Support = Saucer; State(PlainCup).Tea = 1;
            State(Pot).Tea = 0; State(Chair).Tea = 1; State(Basin).Tea = 1; State(Tray).Tea = 1;
            State(Cloth).Support = Supply; State(Cabinet).Locked = true;
        }
        World.Project(Room, Room.TrueName);
        foreach (var e in _all.Concat(People.Select(p => p.Body))) World.Project(e, e.TrueName);
        foreach (var p in People)
        {
            p.Soul.Relations.Add((Relations.Controls, p.Body));
            World.Project(p.Soul, p.Name + "的意识");
            World.Associate(p.Soul, p.Self, World.Representations(p.Body).Single());
            foreach (var (entity, mental) in p.Known)
                World.Associate(p.Soul, mental, World.Representations(entity).Single());
        }
        Capture(aftermath ? "现实现场 · 调查起点" : "模拟初态 · 尚未运行");
    }

    public static TeaState State(Essence e) => e.Get<TeaState>()!;
    public Essence SiteOf(Essence e)
    {
        if (Sites.Contains(e)) return e;
        var person = People.FirstOrDefault(p => ReferenceEquals(p.Body, e));
        return person is not null ? person.Site : State(e).Support is { } parent ? SiteOf(parent) : Entrance;
    }
    public Essence? Find(string name) => _all.FirstOrDefault(e => e.TrueName == name.Replace(" ", ""));

    private void Perceive(TeaPerson p, bool preparation = false)
    {
        if (p.InTransit) return;
        foreach (var site in Sites)
            Observation.Observe(p.Soul, new(site.TrueName, site.Kind) { Target = p.Known[site], Existence = Truth.True });
        foreach (var e in _all)
        {
            var location = SiteOf(e); var hidden = location == Cabinet && State(Cabinet).Locked && e != Cabinet;
            if (location != p.Site || hidden)
            {
                // A remembered thing disappearing from the place being inspected is not knowledge of its new place.
                if (p.Known[e].Parent == p.Known[p.Site] && location != p.Site)
                    Observation.Observe(p.Soul, new(e.TrueName, e.Kind) { Target = p.Known[e], Move = true, Parent = null, Time = Clock.Now, Details = "查看原处，未见原物；新位置未知" });
                continue;
            }
            var s = State(e); var old = p.Known[e].Relations.Where(r => r.Relation == On).ToImmutableHashSet();
            Observation.Observe(p.Soul, new(e.TrueName, e.Kind)
            {
                Target = p.Known[e], Existence = Truth.True, Move = !Sites.Contains(e), Parent = Sites.Contains(e) ? null : p.Known[location], Time = Clock.Now,
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Filled, s.Tea > 0 ? 1 : 0).Add(Chipped, e == ChipCup ? 1 : 0).Add(Locked, s.Locked ? 1 : 0),
                RemoveRelations = old, AddRelations = s.Support is { } support && p.Known.TryGetValue(support, out var view) ? [(On, view)] : [],
                Details = preparation ? $"准备时共同清点，在{p.Site.TrueName}看见；之后未必仍然如此" : $"在{p.Site.TrueName}直接看见"
            });
        }
    }

    public string DescribeKnowledge(TeaPerson p)
    {
        var lines = p.Known.Where(k => Objects.Contains(k.Key) && k.Value.Existence == Truth.True).Select(k =>
            $"{k.Key.TrueName}：{k.Value.Parent?.Name ?? "位置未知"}，{(k.Value.Tags.GetValueOrDefault(Filled) == 1 ? "有茶" : "空")}\n  来源：{k.Value.LastObservation?.Details} · 行动值 {k.Value.LastObservation?.Time:0}");
        return $"{p.Activity}\n{p.Reason}\n\n" + string.Join("\n", lines) + $"\n\n已听到茶铃：{p.Self.Tags.GetValueOrDefault(Bells)}次";
    }

    private Condition CompileCondition(TeaPerson p, string text)
    {
        text = text.Trim();
        if (text.Contains("或")) return new Condition.Any([..text.Split('或').Select(t => CompileCondition(p, t))]);
        if (text.Contains("且")) return new Condition.All([..text.Split('且').Select(t => CompileCondition(p, t))]);
        if (text.StartsWith("非")) return new Condition.Not(CompileCondition(p, text[1..]));
        if (text == "总是") return new Condition.Constant(Truth.True);
        if (text.StartsWith("听过"))
        {
            var n = text.Contains("第一") ? 1 : text.Contains("第二") ? 2 : text.Contains("第三") ? 3 : 0;
            if (n == 0 || !text.TrimEnd().EndsWith("茶铃")) throw new ArgumentException("铃声条件写作：听过 第一次茶铃（或第二次、第三次）");
            return new Condition.Tagged(p.Self, Bells, n);
        }
        var match = Regex.Match(text, @"^认为\s*(.+?)\s*(在|有茶|为空|有缺口|已锁|未锁|在碟上)\s*(.*?)$");
        if (!match.Success) throw new ArgumentException("条件写作：认为 缺口杯 有茶 / 认为 缺口杯 在 窗边；用 且、或、非 组合");
        var e = Find(match.Groups[1].Value) ?? throw new ArgumentException("不认识的对象：" + match.Groups[1].Value);
        var noun = p.Known[e]; var op = match.Groups[2].Value; var tail = match.Groups[3].Value.Trim();
        if (op == "在")
        {
            var place = Find(tail) ?? throw new ArgumentException("不认识的位置：" + tail);
            return Sites.Contains(place) ? new Condition.At(noun, p.Known[place]) : new Condition.Related(noun, On, p.Known[place]);
        }
        if (tail.Length > 0) throw new ArgumentException("条件末尾有未解析内容：" + tail);
        return op switch
        {
            "有茶" => new Condition.Tagged(noun, Filled, 1), "为空" => new Condition.Not(new Condition.Tagged(noun, Filled, 1)),
            "有缺口" => new Condition.Tagged(noun, Chipped, 1), "已锁" => new Condition.Tagged(noun, Locked, 1),
            "未锁" => new Condition.Not(new Condition.Tagged(noun, Locked, 1)),
            _ => throw new ArgumentException("不支持的判断")
        };
    }

    public IReadOnlyList<string> Configure(IReadOnlyList<string> drafts)
    {
        if (Started) return ["本次重演的规则已固定；修改后请新建重演并从起点运行。"];
        var errors = new List<string>(); var compiled = new List<TeaDecisionRule>[People.Length];
        for (var i = 0; i < People.Length; i++)
        {
            var p = People[i]; compiled[i] = [];
            var legacy = CompileIntents(p, drafts[i], errors, out var intents);
            _pendingIntentConfiguration[p] = intents;
            var parsed = TeaRuleLanguage.Parse(legacy); errors.AddRange(parsed.Errors.Select(e => p.Name + "：" + e));
            foreach (var r in parsed.Rules)
            {
                try
                {
                    var target = Find(r.Command.Target) ?? throw new ArgumentException("不认识的行动对象：" + r.Command.Target);
                    var dest = r.Command.Destination is { } d ? Find(d) ?? throw new ArgumentException("不认识的目标位置：" + d) : null;
                    if (r.Command.Verb is TeaVerb.Move or TeaVerb.Inspect && !Sites.Contains(target)) throw new ArgumentException("前往/查看需要填写地点");
                    if (r.Command.Verb is TeaVerb.Carry or TeaVerb.Pour && !Objects.Contains(target)) throw new ArgumentException("只能搬运或倾倒器物");
                    if (r.Command.Verb == TeaVerb.Pour && target != Pot && !Cups.Contains(target)) throw new ArgumentException("只有杯子与茶壶可以倾倒茶");
                    if (r.Command.Verb == TeaVerb.Carry && (target == dest || dest == Pot || dest == Cloth && target == Cloth)) throw new ArgumentException("承托关系不能指向自身或不支持的承托物");
                    if (r.Command.Verb == TeaVerb.Carry && dest is not null && !(Sites.Contains(dest) || dest == Saucer || dest == Cloth)) throw new ArgumentException("目标应为地点、续茶碟或椅布");
                    if (r.Command.Verb == TeaVerb.Pour && dest != Basin && dest != Tray && dest != Chair && dest != Sink) throw new ArgumentException("倾倒目标：温瓶盆、染纸盘、椅面、洗具台");
                    if (r.Command.Verb == TeaVerb.Pull && target != Cloth || r.Command.Verb == TeaVerb.Lock && target != Cabinet) throw new ArgumentException("此动作不适用于这个对象");
                    compiled[i].Add(new(CompileCondition(p, r.Condition), r.Command.Verb, target, dest, r.Priority, r.Line));
                }
                catch (ArgumentException e) { errors.Add($"{p.Name} 第{r.Line}行：{e.Message}"); }
            }
        }
        if (errors.Count == 0) for (var i = 0; i < People.Length; i++) { People[i].Rules.Clear(); People[i].Rules.AddRange(compiled[i]); _intents[People[i]] = _pendingIntentConfiguration[People[i]]; }
        return errors;
    }

    public void Start()
    {
        if (Started) return; Started = true;
        BeginNotice(0);
        Clock.Schedule(1, CheckNoticeProgress);
    }
    public bool Step()
    {
        if (!Started || Finished) return false;
        var prior = Frames.Count;
        for (var i = 0; i < 100 && Frames.Count == prior && !Finished; i++) if (!Clock.Step()) return false;
        return true;
    }
    private void Wake(TeaPerson p)
    {
        if (p.Busy || Finished || !_scheduled.Add(p)) return;
        Clock.Schedule(1, () => { _scheduled.Remove(p); Decide(p); });
    }
    private void Decide(TeaPerson p)
    {
        if (p.Busy || Finished) return;
        if (RunIntent(p)) return;
        var evaluations = p.Rules.Where(r => !_attempted.Contains((p, r.Line, KnowledgeSignature(p)))).Select(r => (Rule: r, Truth: Ast.Evaluate(r.Condition, new(p.Self)))).ToArray();
        var rule = evaluations.Where(r => r.Truth == Truth.True).OrderByDescending(r => r.Rule.Priority).ThenBy(r => r.Rule.Line).Select(r => r.Rule).FirstOrDefault();
        if (rule is null)
        {
            p.Activity = "等待新观察"; p.Reason = evaluations.Any(r => r.Truth == Truth.Unknown) ? "部分条件未知；未知不等于否。" : "没有成立的行为规则。"; return;
        }
        _attempted.Add((p, rule.Line, KnowledgeSignature(p)));
        p.Busy = true; p.Reason = $"第{rule.Line}行条件在本人认知中成立；优先{rule.Priority}。并列按显示顺序选择。";
        Begin(p, rule);
    }
    private void Begin(TeaPerson p, TeaDecisionRule r)
    {
        var targetPlace = Sites.Contains(r.Target) ? r.Target : p.Known[r.Target].Parent is { } parent ? Sites.FirstOrDefault(s => p.Known[s] == parent) : null;
        if (targetPlace is null) { End(p, "不知道目标在哪里", false); return; }
        Travel(p, targetPlace, () =>
        {
            if (r.Verb is TeaVerb.Move or TeaVerb.Inspect) { End(p, "查看了" + targetPlace.TrueName, true); return; }
            Clock.Schedule(1, () =>
            {
                if (SiteOf(r.Target) != p.Site || p.Site == Cabinet && State(Cabinet).Locked && r.Target != Cabinet) { End(p, "原处没有可取得的对象，或柜门已锁", false); return; }
                if (r.Verb == TeaVerb.Lock)
                {
                    if (SiteOf(PlainCup) == Cabinet && State(PlainCup).Tea != 0) Differences.Add("锁柜时：主厨记录素杯为空，模拟中锁柜时杯子已有茶。");
                    State(Cabinet).Locked = true; End(p, "锁上器具柜", true); return;
                }
                if (r.Verb == TeaVerb.Pull)
                {
                    if (State(Cloth).Support is { Kind: ProjectionKind.Body } heldBy && heldBy != p.Body) { End(p, "椅布由另一人持有", false); return; }
                    foreach (var cup in Cups.Where(c => State(c).Support == Cloth))
                    {
                        if (cup == ChipCup && State(cup).Tea > 0) _clothSpill = true;
                        State(Chair).Tea += State(cup).Tea; State(cup).Tea = 0; State(cup).Support = Chair;
                    }
                    State(Cloth).Support = p.Body; End(p, "抽走椅布；承托其上的杯子随之倾倒", true); return;
                }
                var destination = r.Destination!;
                var destPlace = Sites.Contains(destination) ? destination : p.Known[destination].Parent is { } remembered ? Sites.FirstOrDefault(s => p.Known[s] == remembered) : null;
                if (destPlace is null) { End(p, "不知道目标承托物在哪里", false); return; }
                if (State(r.Target).Support is { Kind: ProjectionKind.Body } holder && holder != p.Body) { End(p, "目标正由另一人持有", false); return; }
                ResolveAction(p, new Effect.Act(ActionOp.Take, p.Known[r.Target]), () =>
                {
                p.Activity = "拿起" + r.Target.TrueName; ObserveNearby(); Capture(p.Name + "：" + p.Activity);
                Travel(p, destPlace, () => Clock.Schedule(1, () =>
                {
                    if (destPlace == Cabinet && State(Cabinet).Locked) { End(p, "柜门已锁，未能放入；仍持有器物", false); return; }
                    if (SiteOf(destination) != p.Site) { End(p, "目标承托物已被移走；仍持有器物", false); return; }
                    if (r.Verb == TeaVerb.Pour)
                    {
                        if (r.Target != Pot && !Cups.Contains(r.Target)) { End(p, "这个物件不能倾倒茶", false); return; }
                        if (State(r.Target).Tea == 0) { End(p, "杯中已没有茶", false); return; }
                    }
                    if (destination == Saucer && Cups.Any(c => c != r.Target && State(c).Support == Saucer)) { End(p, "续茶碟上已有杯子；仍持有器物", false); return; }
                    ResolveAction(p, new Effect.Act(ActionOp.Place, p.Known[r.Target], p.Known[destination]), () =>
                    {
                        if (r.Verb == TeaVerb.Pour)
                        {
                            _uses.Add((p, r.Target, destination));
                            State(destination).Tea += State(r.Target).Tea; State(r.Target).Tea = 0;
                        }
                        End(p, r.Verb == TeaVerb.Pour ? $"将{r.Target.TrueName}的茶倒入{destination.TrueName}" : $"把{r.Target.TrueName}放到{destination.TrueName}", true);
                    });
                }));
                });
            });
        });
    }
    private void ResolveAction(TeaPerson person, Effect effect, Action succeeded)
    {
        var execution = Session.Submit(person.Soul, new(person.Self, effect, Delay: 0));
        void Completed(ActionExecution finished)
        {
            if (!ReferenceEquals(finished, execution)) return;
            Session.Completed -= Completed;
            if (finished.Status == ExecutionStatus.Completed) succeeded();
            else End(person, "未能完成搬放；器物保持原位", false);
        }
        Session.Completed += Completed;
    }
    private void Travel(TeaPerson p, Essence site, Action arrived)
    {
        if (p.Site == site) { arrived(); return; }
        var path = TeaRoomGeometry.Path(p.Position, Points[site]);
        if (path.Count == 0) { End(p, "没有可通行路线", false); return; }
        var index = 0; p.InTransit = true;
        void Next()
        {
            if (index >= path.Count) { p.InTransit = false; p.Site = site; State(p.Body).Support = site; Perceive(p); arrived(); return; }
            var next = path[index++];
            Clock.Schedule(0.5, () => { p.Position = next; p.Activity = "前往" + site.TrueName; Capture(p.Name + "：" + p.Activity); Next(); });
        }
        Next();
    }
    private void End(TeaPerson p, string message, bool succeeded)
    {
        p.Busy = false; p.Activity = succeeded ? message : "行动未完成：" + message;
        if (!succeeded && _activeIntent.Contains(p)) { _intentSteps[p].Clear(); _afterInspection.Remove(p); _activeIntent.Remove(p); }
        ObserveNearby(); Capture(p.Name + "：" + p.Activity);
        // Reconsider only after a later observation or a modest cost: no zero-time loops.
        Clock.Schedule(succeeded ? 2 : 5, () => Wake(p));
    }
    private void ObserveNearby() { foreach (var p in People) { Perceive(p); Wake(p); } }
    private Essence[] Cups => [ChipCup, LeafCup, PlainCup];
    private void Ring()
    {
        if (Finished) return; _bell++;
        var cup = Cups.FirstOrDefault(c => State(c).Support == Saucer);
        var filled = cup is not null && State(cup).Tea == 0 && State(Pot).Tea > 0;
        if (filled) { State(Pot).Tea--; State(cup!).Tea++; }
        foreach (var p in People)
            Observation.Observe(p.Soul, new(p.Name, ProjectionKind.Body) { Target = p.Self, Source = ObservationSource.Perception, Time = Clock.Now, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Bells, _bell), Details = "听到房间内独立茶铃" });
        ObserveNearby();
        if (_bell == 1 && (SiteOf(ChipCup) != Window || State(ChipCup).Tea == 0)) Differences.Add("第一次铃后：记录中缺口杯在窗边且有茶，模拟中位置或茶量不同。");
        if (_bell == 1 && !_uses.Any(u => u.Cup == LeafCup && u.Destination == Tray)) Differences.Add("染纸盘记录：试色发生在第一声铃前，但此前模拟中没有用叶纹杯的茶染纸。");
        if (_bell == 2 && (SiteOf(LeafCup) != Window || State(LeafCup).Tea == 0)) Differences.Add("第二次铃后：记录中叶纹杯在窗边且有茶，模拟中位置或茶量不同。");
        if (_bell == 3 && !State(Cabinet).Locked) Differences.Add("第三次铃响时：模拟中的器具柜尚未锁上。");
        Capture($"第{_bell}次茶铃：" + (filled ? "续茶碟向其上空杯补入一份茶" : "没有符合条件的续茶"));
    }
    private void CheckFinal()
    {
        void Check(bool ok, string message) { if (!ok) Differences.Add(message); }
        Check(State(Pot).Tea == 0, "终态：茶壶仍有茶。");
        Check(State(Chair).Tea > 0, "终态：椅面没有茶迹。");
        Check(_clothSpill, "椅面痕迹：模拟没有留下椅布上拖长的缺口杯底湿圈。");
        Check(State(ChipCup).Tea == 0 && SiteOf(ChipCup) == Sink, "终态：缺口杯应空着留在洗具台。");
        Check(State(LeafCup).Tea > 0 && SiteOf(LeafCup) == Window, "终态：窗边应有装茶的叶纹杯。");
        Check(State(PlainCup).Tea > 0 && SiteOf(PlainCup) == Cabinet && State(Cabinet).Locked, "终态：锁柜内应有装茶的素杯。");
        Check(State(Basin).Tea > 0, "终态：温瓶盆没有茶迹。"); Check(State(Tray).Tea > 0, "终态：染纸盘没有茶迹。");
        Check(_uses.Any(u => u.Cup == ChipCup && u.Destination == Basin), "温瓶盆痕迹：模拟中没有使用缺口杯，无法解释盆沿缺口杯印。");
        Check(_uses.Any(u => u.Person == People[2] && u.Destination == Basin), "医生陈述：模拟中医生没有将茶用于温瓶盆。");
        Check(_uses.Any(u => u.Person == People[3] && u.Destination == Tray), "画家陈述：模拟中画家没有将茶用于染纸盘。");
    }
    private void Capture(string message)
    {
        World.Sync();
        Frames.Add(new(Clock.Now, message, _all.ToDictionary(e => e, e => (State(e).Support, State(e).Tea, State(e).Locked)),
            People.ToDictionary(p => p, p => p.Position), People.ToDictionary(p => p, DescribeKnowledge), Differences.ToArray()));
    }
    public int TotalTea => _all.Sum(e => State(e).Tea);
}

public static class TeaRoomGeometry
{
    public const int Width = 640, Height = 416;
    // Footprint rectangles, independently from the visual height of each furnishing.
    public static readonly (int X, int Y, int W, int H)[] Obstacles = [(80, 132, 64, 12), (252, 224, 136, 16), (432, 132, 96, 12), (512, 80, 64, 16), (100, 196, 24, 12), (480, 336, 96, 16), (128, 336, 96, 16), (368, 336, 64, 16)];
    public static bool Walkable(int x, int y) => x >= 48 && x <= 592 && y >= 112 && y <= 400 &&
        // Keep grid routes clear of contact edges, including Godot's body safe margin.
        (y < 352 || x >= 304 && x <= 336) && !Obstacles.Any(r => x + 8.1 > r.X && x - 8.1 < r.X + r.W && y + 0.1 > r.Y && y - 12.1 < r.Y + r.H);
    public static List<(int X, int Y)> Path((int X, int Y) from, (int X, int Y) to)
    {
        var start = ((int)Math.Round(from.X / 16.0) * 16, (int)Math.Round(from.Y / 16.0) * 16);
        var queue = new Queue<(int X, int Y)>(); queue.Enqueue(start);
        var previous = new Dictionary<(int X, int Y), (int X, int Y)> { [start] = start };
        while (queue.TryDequeue(out var n))
        {
            if (n == to)
            {
                var path = new List<(int X, int Y)>(); for (var p = to; p != start; p = previous[p]) path.Add(p); path.Reverse(); return path;
            }
            foreach (var next in new[] { (n.X + 16, n.Y), (n.X - 16, n.Y), (n.X, n.Y + 16), (n.X, n.Y - 16) })
                if (Walkable(next.Item1, next.Item2) && previous.TryAdd(next, n)) queue.Enqueue(next);
        }
        return [];
    }
}


