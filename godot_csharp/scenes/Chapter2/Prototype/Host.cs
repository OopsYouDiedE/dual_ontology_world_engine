using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>
/// 宿主（Godot 第二章）用的入口，按设计总纲 §四：读不耗时，写走 RunSlot（做完即过去一阵）。
/// 这里的方法只在两次 RunSlot 之间调用；它们只改侦探自己的认知与位置，不改任何人物的认知，
/// 因此重演（Rebuild 按行动记录重放）不受影响。
/// </summary>
public sealed partial class Game
{
    /// <summary>玩家能说出口的规矩写法（总纲 §四.2：从具体写法中选，不在格子里拼）。</summary>
    public static readonly (Card Card, string Text)[] CaseWordings = [.. CardKit.CaseWordings];
    public static readonly (Card Card, string Text)[] ArchiveWordings = [.. CardKit.ArchiveWordings];

    public static string? WordingOf(Card card) =>
        CaseWordings.Concat(ArchiveWordings).Where(w => w.Card == card).Select(w => w.Text).FirstOrDefault();

    /// <summary>侦探走到某处（宿主换图时调用）。</summary>
    public void HostMoveDet(Place place)
    {
        if (Where(Det) == place) return;
        Det.Body.Get<SpatialProperty>()!.Space = Space[place];
        RealDirty = true;
    }

    /// <summary>细看一件东西：侦探亲见，所在一处一并看清（§12 第 7 条）。返回看见了什么。</summary>
    public string HostExamine(string thing)
    {
        var (place, of) = Things_[thing];
        var e = of(this);
        HostMoveDet(place);
        Perceive(Det);
        if (Day >= CaseDay) DetLooked.Add(e);
        DetSaw(e);
        if (e == Granary) RevealLatch(Det);
        if (Board.ContainsValue(e))
            foreach (var s in Specs.Where(s => s.PostedAt.Contains(place) && s != RuanLedger))
            {
                DetHeard.Add(s);
                Det.Acc.TryAdd(s, Truth.Unknown);
            }
        var text = Describe(e);
        if (place == Place.ArchiveFront && DawnTraces.TryGetValue(Day - 1, out var tr) && tr.FirstOrDefault(t => t.Key == "前厅脚印") is { } fp) text += "\n" + fp.Text + "。";
        return text;
    }
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
    public string HostAsk(P who)
        => HostAskPresentation(who).Text;

    public (string Text, string Emotion) HostAskPresentation(P who, bool selectedTestimony = false)
    {
        DetAsked.Add(who);
        AskedOn.Add((Day, who));
        return TestimonyPresentation(this[who], selectedTestimony);
    }

    /// <summary>侦探读过或听过、可以讲给别人听的规矩。</summary>
    public IEnumerable<RuleSpec> HostTellable() => DetKnownSpecs().Where(s => !s.ByDemon && s.Card?.Behavior is not (>= 5 and <= 8));

    /// <summary>对质的一对陈述与出示是否成立。</summary>
    public bool HostPairValid(int claim, int evidence) => ClaimTrue(claim) && Relevant[claim].Contains(evidence) && DetHas(evidence);
    public bool HostClaimTrue(int claim) => ClaimTrue(claim);
    public string HostReaction(int claim) => Reaction(claim);

    /// <summary>一次案件重演的回放：按时间排好的墨影步子（身量、从哪到哪、做了什么）。</summary>
    public sealed record InkStep(double Time, P Who, Build Build, Place From, Place To, string Act);

    public static List<InkStep> InkSteps(Game world, int night)
    {
        var steps = new List<InkStep>();
        foreach (var m in world.Moves.Where(m => m.Day == night && m.Who != P.Det))
            steps.Add(new(m.Time, m.Who, world[m.Who].Pro.Build, m.From, m.To, ""));
        foreach (var e in world.Events.Where(e => e.Day == night && e.Slot == Slot.Night && e.Who != P.Det && e.Job is not (Job.Sleep or Job.Rest)))
            steps.Add(new(e.Time, e.Who, world[e.Who].Pro.Build, e.Where, e.Where, e.Text));
        return steps.OrderBy(s => s.Time).ThenBy(s => s.Act.Length).ToList();
    }

    /// <summary>某个时段里，此人最后做的一件事（地图上显示他“正在做什么”）。</summary>
    public ActEvent? LastDoing(P who, int day, Slot slot) => Events.LastOrDefault(e => e.Who == who && e.Day == day && e.Slot == slot);

    /// <summary>首课当众答题（设计第三部分 §5.8）：答得精确被当众称赞——信它或爱学的人更信你，戒备它的人疑你是它的宠儿。</summary>
    public void HostLessonAnswer(bool precise)
    {
        if (!precise) return;
        var present = People.Where(p => p != Det && Where(p) == Place.Lecture).ToArray();
        Witness(Det, "侦探的回答被恶魔当众称赞", 0, 1, present.Where(p => p.Pro.DemonPrejudice >= 0), domain: Dom.Archive);
        Witness(Det, "侦探被恶魔当众称赞——它的宠儿？", -1, 0, present.Where(p => p.Pro.DemonPrejudice < 0));
    }

    /// <summary>第一章留下的钩（跨章节持久状态）。</summary>
    public void HostSeedHook(P who) => this[who].Hooks += 1;

    public List<string> TakeFeed()
    {
        var lines = Feed.ToList();
        Feed.Clear();
        return lines;
    }
}
