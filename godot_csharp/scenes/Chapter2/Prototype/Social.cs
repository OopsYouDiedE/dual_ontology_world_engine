using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public enum NewsKind { Fact, Sighting, Accusation, Defense, Reputation, Lesson, Ledger, Rumor, DemonWord }

/// <summary>一条可以被转述的信息。来源链保存在每个听者的 Heard 里。</summary>
public sealed class NewsItem
{
    public required int Id { get; init; }
    public required NewsKind Kind { get; init; }
    public required Claim Claim { get; init; }
    public required P Origin { get; init; }
    public P? About { get; init; }
    public int TrustDelta { get; init; }
    public int InsightDelta { get; init; }
    public Essence? FactOn { get; init; }
    public Tag? FactTag { get; init; }
    public int FactValue { get; init; }
    public int Day { get; init; }
    public Slot Slot { get; init; }
    public Build? Build { get; init; }
    public Place? Place { get; init; }
    public int Lesson { get; init; }
    public bool Manipulation { get; init; }
    public Dom Domain { get; init; }
}

public sealed partial class Game
{
    public readonly List<NewsItem> AllNews = [];
    private readonly Dictionary<Person, (int Seen, Dictionary<Projection, (Projection Subject, int Trust, int Insight, Dom Domain)> Events)> _trustCache = [];

    private (int Seen, Dictionary<Projection, (Projection Subject, int Trust, int Insight, Dom Domain)> Events) Scan(Person p)
    {
        if (!_trustCache.TryGetValue(p, out var c)) c = (0, []);
        var history = p.World.History;
        for (var i = c.Seen; i < history.Count; i++)
        {
            var h = history[i];
            if (h.Target.Kind != ProjectionKind.Event) continue;
            var subject = h.Relations.FirstOrDefault(r => r.Relation == Relations.Subject).Target;
            if (subject is null) continue;
            c.Events[h.Target] = (subject, h.Tags.GetValueOrDefault(Tags.Trust), h.Tags.GetValueOrDefault(T.Insight), (Dom)h.Tags.GetValueOrDefault(T.Domain));
        }
        c.Seen = history.Count;
        _trustCache[p] = c;
        return c;
    }

    /// <summary>与 SocialSystem.Evaluate 同一公式（不含接收者的合作倾向）：对方投影上的 Trust + 历史事件中以对方为主语的 Trust。</summary>
    public int TrustIn(Person p, Person q)
    {
        using var _ = Prof.Time("TrustIn");
        if (p == q) return 5;
        var them = p.M[q.Body];
        return them.Tags.GetValueOrDefault(Tags.Trust) + Scan(p).Events.Values.Where(e => ReferenceEquals(e.Subject, them)).Sum(e => e.Trust);
    }

    /// <summary>此人心中的侦探：为人（= 对侦探的信任，SocialSystem 用的同一个数）与判断（历史事件里的 Insight 之和；给定领域时只算该领域与一般事）。</summary>
    public (int Credible, int Insight) ImageOfDet(Person p, Dom? domain = null)
    {
        var det = p.M[Det.Body];
        var ev = Scan(p).Events.Values.Where(e => ReferenceEquals(e.Subject, det)).ToArray();
        return (det.Tags.GetValueOrDefault(Tags.Trust) + ev.Sum(e => e.Trust),
            ev.Where(e => domain is null || e.Domain == domain || e.Domain == Dom.General || domain == Dom.General).Sum(e => e.Insight));
    }

    /// <summary>一件事被人亲眼看见：亲见者心里记一个以当事人为主语的事件（Perception）。没人看见就什么都不改变。</summary>
    public void Witness(Person subject, Claim claim, int trust, int insight, IEnumerable<Person> witnesses, bool spread = true, bool manipulation = false, Dom domain = Dom.General)
    {
        var list = witnesses.Where(w => w != subject).Distinct().ToArray();
        var plain = Plain(claim);
        foreach (var w in list)
        {
            Observation.Observe(w.Soul, new ObservationInput(plain, ProjectionKind.Event)
            {
                Source = ObservationSource.Perception, Existence = Truth.True, Time = Now, Details = plain,
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, trust).Add(T.Insight, insight).Add(T.Domain, (int)domain),
                AddRelations = [(Relations.Subject, w.M[subject.Body])]
            });
        }
        if (!spread || list.Length == 0) return;
        var item = NewItem(NewsKind.Reputation, claim, list[0].Id, subject.Id, trust, insight, manipulation: manipulation, domain: domain);
        foreach (var w in list) w.News[item.Id] = new(w.Id, 0, ObservationSource.Perception, Day, item.Id);
        Log.Add($"D{Day} {Names.Of(Slot)} 目睹：{plain}（信{trust:+0;-0;0} 识{insight:+0;-0;0}）→ {string.Join("、", list.Select(w => w.Name))}");
    }

    private NewsItem NewItem(NewsKind kind, Claim claim, P origin, P? about, int trust = 0, int insight = 0, Essence? on = null, Tag? tag = null,
        int value = 0, Build? build = null, Place? place = null, int lesson = 0, bool manipulation = false, Dom domain = Dom.General)
    {
        var item = new NewsItem
        {
            Id = AllNews.Count + 1, Kind = kind, Claim = claim, Origin = origin, About = about, TrustDelta = trust, InsightDelta = insight,
            FactOn = on, FactTag = tag, FactValue = value, Day = Day, Slot = Slot, Build = build, Place = place, Lesson = lesson, Manipulation = manipulation, Domain = domain
        };
        AllNews.Add(item);
        return item;
    }

    public NewsItem NewsFrom(Person origin, NewsKind kind, Claim claim, Person? about = null, int trust = 0, int insight = 0, Essence? on = null,
        Tag? tag = null, int value = 0, Build? build = null, Place? place = null, int lesson = 0, bool manipulation = false)
    {
        var item = NewItem(kind, claim, origin.Id, about?.Id, trust, insight, on, tag, value, build, place, lesson, manipulation);
        origin.News[item.Id] = new(origin.Id, 0, ObservationSource.Perception, Day, item.Id);
        return item;
    }

    /// <summary>此人是否已经知道这条知识（同一件事，不论从谁那里听来）。</summary>
    public bool KnowsClaim(Person p, Claim c) => p.News.Values.Any(h => AllNews[h.Item - 1].Claim.Identity == c.Identity);
    public IEnumerable<Claim> ClaimsOf(Person p) => p.News.Values.Select(h => AllNews[h.Item - 1].Claim);

    /// <summary>
    /// 同处一地的人交换消息。转述经过不信任的人打折；经过成见会走样（对其成见对象的负面消息再加重一分，并带上定性）。
    /// 每对人每次最多说四条，先说最新的。
    /// </summary>
    public void Gossip()
    {
        using var _ = Prof.Time("Gossip");
        foreach (var group in People.Where(p => !(Slot == Slot.Night && p.Asleep)).GroupBy(Where))
        {
            var here = group.ToArray();
            foreach (var teller in here)
            foreach (var hearer in here)
            {
                if (teller == hearer || TrustIn(teller, hearer) < 0) continue;
                var told = 0;
                foreach (var (id, heard) in teller.News.OrderByDescending(n => n.Key).ToArray())
                {
                    if (told >= 4) break;
                    if (hearer.News.ContainsKey(id)) continue;
                    var item = AllNews[id - 1];
                    if (item.Day < Day - 1 && item.Kind != NewsKind.Lesson) continue;
                    if (item.About == hearer.Id && item.Kind != NewsKind.Reputation) continue;
                    if (Options.BlockNews?.Invoke(hearer, item) == true) continue;
                    Receive(teller, hearer, item, heard);
                    told++;
                }
            }
            Bonds(here);
            // 转述与拉人改由各人心中的规则经 ActionRuntime 结算（GameJobs.TalkRules、Game.HandleTalk），这里只交换消息。
        }
    }

    private readonly HashSet<(P, P, int)> _bonded = [];

    /// <summary>价值相投：同处一地、接受了同一条人立规则的两个人，彼此多信一分（每条规则每对一次）。这是不靠侦探的结盟。</summary>
    private void Bonds(Person[] here)
    {
        foreach (var a in here)
        foreach (var b in here.Where(b => b.Id > a.Id && a != Det && b != Det))
            foreach (var s in Specs.Where(s => !s.ByDemon && s.Author is not null && s != RuanLedger && Holds(a, s) && Holds(b, s)))
            {
                if (!_bonded.Add((a.Id, b.Id, s.Id))) continue;
                Witness(a, K("同认规矩", a.Id, b.Id, values: ("规矩", Short(s))), 1, 0, [b], spread: false);
                Witness(b, K("同认规矩", b.Id, a.Id, values: ("规矩", Short(s))), 1, 0, [a], spread: false);
            }
    }

    private static bool Holds(Person p, RuleSpec s) => s.Author == p.Id || p.Acc.GetValueOrDefault(s) == Truth.True;
    /// <summary>规矩的简称：有“：”“；”分出的题头就用题头，否则整句（不截半句）。</summary>
    public static string Short(RuleSpec s)
    {
        var t = s.Text.TrimEnd('。');
        var cut = t.IndexOfAny(['：', '；']);
        return cut >= 4 ? t[..cut] : t;
    }

    /// <summary>
    /// 信任链传播（§11 第 3 条）：接受得真心（或自己立的）规则，会被转述给同处一地、信得过自己、还没听过的人。
    /// 听者的判断里，转述者的担保代替大半作者的分量。每人每轮最多转述一条；被底线否决过的不再重判。
    /// </summary>
    private void Relays(Person[] here)
    {
        if (_relaySlot != (Day, Slot)) { _relaySlot = (Day, Slot); _relayedThisSlot.Clear(); }
        foreach (var teller in here.Where(t => t != Det))
        {
            var spec = Specs.Where(s => !s.ByDemon && s.Author is not null && s != RuanLedger && Holds(teller, s) &&
                    (s.Author == teller.Id || AcceptScore(teller, s, Channel.Relay, null, out _) >= Options.AcceptThreshold + 2))
                .OrderByDescending(s => s.Author == P.Det ? 1 : 0).ThenByDescending(s => s.Id)
                .FirstOrDefault(s => here.Any(h => h != teller && h != Det && !h.Acc.ContainsKey(s) && TrustIn(h, teller) >= 1));
            if (spec is null) continue;
            // 转述也受注意力限制：一个人一个时段最多跟两个人细说（先挑最信他的）。
            var budget = 2 - _relayedThisSlot.GetValueOrDefault(teller);
            if (budget <= 0) continue;
            var seen = new List<(Person, Truth)>();
            foreach (var hearer in here.Where(h => h != teller && h != Det && !h.Acc.ContainsKey(spec) && TrustIn(h, teller) >= 1)
                         .OrderByDescending(h => TrustIn(h, teller)).ThenBy(h => h.Id).Take(budget).ToArray())
            {
                var v = Tell(hearer, spec, Channel.Relay, teller);
                _relayedThisSlot[teller] = _relayedThisSlot.GetValueOrDefault(teller) + 1;
                var from = _transmit.TryGetValue((spec.Id, teller.Id), out var tf) ? tf : _postedEv.GetValueOrDefault(spec.Id);
                _transmit[(spec.Id, hearer.Id)] = RecordEvent(teller.Id, $"向{hearer.Name}转述「{Short(spec)}」，{hearer.Name}{(v == Truth.True ? "认了" : "没认")}", "交谈", Where(teller), [from]);
                RelayLog.Add((Day, Slot, teller.Id, hearer.Id, spec.Id, v == Truth.True));
                // 信任链留下可读痕迹（§12 第 2 条）：侦探在场就亲眼看见；不在场，听者下次见到侦探会说出是谁跟他讲的。
                if (spec.Author == P.Det)
                {
                    if (here.Contains(Det) && Slot != Slot.Night) seen.Add((hearer, v));
                    else QueueDisclosure(hearer, spec, teller, null, v == Truth.True);
                }
            }
            if (seen.Count > 0) DetSawRelay(teller, seen, spec);
        }
    }

    public readonly List<(int Day, Slot Slot, P Teller, P Hearer, int Spec, bool Accepted)> RelayLog = [];
    private readonly Dictionary<Person, int> _relayedThisSlot = [];
    private (int, Slot) _relaySlot;

    public void Receive(Person teller, Person hearer, NewsItem item, Heard via, Channel channel = Channel.Relay)
    {
        var trust = TrustIn(hearer, teller);
        if (KnowsClaim(hearer, item.Claim)) { hearer.News[item.Id] = new(teller.Id, via.Hops + 1, ObservationSource.Testimony, Day, item.Id, via.Slant); return; }
        var delta = item.TrustDelta;
        var slant = via.Slant;
        if (item.About is { } about && delta < 0 && teller.Pro.Prejudice.GetValueOrDefault(about) < 0 && slant is null && teller.Id != item.Origin)
        {
            delta -= 1;
            slant = teller.Id;
        }
        var heard = new Heard(teller.Id, via.Hops + 1, ObservationSource.Testimony, Day, item.Id, slant);
        var text = Plain(item.Claim);
        var scaled = trust >= 2 ? delta : trust >= 0 ? delta / 2 : 0;
        var insight = trust >= 2 ? item.InsightDelta : trust >= 0 ? item.InsightDelta / 2 : 0;
        hearer.News[item.Id] = heard;
        if (item.About is { } a2 && (scaled != 0 || insight != 0))
            Observation.Observe(hearer.Soul, new ObservationInput(text, ProjectionKind.Event)
            {
                Source = ObservationSource.Testimony, Existence = Truth.True, Time = Now, Details = $"{text}（听{teller.Name}说）",
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, scaled).Add(T.Insight, insight).Add(T.Domain, (int)item.Domain),
                AddRelations = [(Relations.Subject, hearer.M[this[a2].Body]), (Relations.Object, hearer.M[teller.Body])]
            });
        else
            Observation.Observe(hearer.Soul, new ObservationInput(text, ProjectionKind.Event)
            {
                Source = ObservationSource.Testimony, Existence = Truth.True, Time = Now, Details = $"{text}（听{teller.Name}说）",
                AddRelations = [(Relations.Object, hearer.M[teller.Body])]
            });
        if (item.FactOn is { } on && item.FactTag is { } tag && trust >= 0) Know(hearer, on, tag, item.FactValue, ObservationSource.Testimony, text);
        // 恶魔亲口承认的话经人转述：信得过转述者，就对它那条“凡学员皆可取回己物”多一分疑心（信任链传播）。
        if (item.Kind == NewsKind.DemonWord && trust >= 1 && hearer.DemonDoubt < 1) hearer.DemonDoubt = 1;
        if (item.Claim.Predicate == "基座裂缝" && trust >= 2) hearer.Witnessed.Add("crack-heard");
        if (item.Kind == NewsKind.Accusation && item.About is { } acc && trust >= 0) Know(hearer, this[acc].Body, T.Accused, 1, ObservationSource.Testimony, text);
        if (item.Kind == NewsKind.Lesson && !hearer.Lessons.ContainsKey(item.Lesson))
        {
            var full = teller.Lessons.GetValueOrDefault(item.Lesson) && (teller.Pro.Signal >= 3 || trust >= 2 && RelayAccepted(teller));
            hearer.Lessons[item.Lesson] = full;
            Log.Add($"D{Day} {Names.Of(Slot)} {hearer.Name} 从{teller.Name}处听到第{item.Lesson}课（{(full ? "完整" : "走样")}）");
        }
        if (hearer == Det) QueueRelayToDet(teller, item, heard);
    }

    private bool RelayAccepted(Person p) => Specs.Any(s => s.Card?.Behavior == 4 && p.Acc.GetValueOrDefault(s) == Truth.True);

    /// <summary>见面时的即时反应：听懂对自己说的话里的事实（第一章同样以台词解释 Testimony）。</summary>
    private void OnMeet(Person p, Person other) { }

    public void Interpret(Person p)
    {
        var h = p.World.History;
        for (; p.Interpreted < h.Count; p.Interpreted++)
        {
            var e = h[p.Interpreted];
            if (e.Source != ObservationSource.Testimony || e.Target.Kind != ProjectionKind.Event) continue;
            if (e.Details.Contains("门闩松了")) Know(p, Granary, T.LatchLoose, 1, ObservationSource.Testimony, e.Details);
        }
    }

    // ------------------------------------------------------------ factions --

    public sealed record Faction(ImmutableArray<P> Members, P Leader, ImmutableArray<string> SharedRules);

    /// <summary>
    /// 派系 = 围绕立规者的追随（§11 第 7 条；§12 第 8 条向价值聚落收敛）：
    /// 中心是四个价值聚落的立规者（沃德、沈鼎、卢西安、维罗妮卡）、侦探，以及别的立过规矩且至少有两人认的人；
    /// 每个人投向“认了他几条规矩（每条 +2，触犯底线被否一条 −2）+ 信他（上限 2）+ 价值相近（同一聚落 +2）”最高的中心，得分 ≥ 3 才算入派。
    /// 价值相近只是倾向：认了别家的规矩、信别的人，就会被拉走。派系少于两人不计。
    /// </summary>
    public List<Faction> Factions()
    {
        var core = Specs.Where(s => !s.ByDemon && s != RuanLedger && s.Author is not null).ToArray();
        var leaders = new[] { P.Ward, P.Shen, P.Lucian, P.Veronica, P.Det };
        var centers = core.Select(s => s.Author!.Value).Distinct()
            .Where(a => leaders.Contains(a) || core.Where(s => s.Author == a).Any(s => People.Count(p => p.Id != a && p.Acc.GetValueOrDefault(s) == Truth.True) >= 2))
            .Select(a => this[a]).ToArray();
        int Pull(Person p, Person c) =>
            core.Where(s => s.Author == c.Id).Sum(s => p.Acc.GetValueOrDefault(s) == Truth.True ? 2 : p.Vetoed.Contains(s) ? -2 : 0)
            + Math.Clamp(c == Det ? ImageOfDet(p).Credible : TrustIn(p, c), -3, 2)
            + (c != Det && Cast.ClusterOf(c.Id) != Cluster.None && Cast.ClusterOf(c.Id) == Cast.ClusterOf(p.Id) ? 2 : 0);
        var join = new Dictionary<Person, Person>();
        foreach (var p in People)
        {
            if (p == Det && !centers.Contains(p)) continue;
            if (centers.Contains(p) && leaders.Contains(p.Id)) { join[p] = p; continue; }
            var best = centers.Where(c => c != p).Select(c => (c, score: Pull(p, c))).OrderByDescending(x => x.score).ThenBy(x => x.c.Id).FirstOrDefault();
            if (centers.Contains(p) && best.score < 5) { join[p] = p; continue; }
            if (best.c is not null && best.score >= 3) join[p] = best.c;
        }
        var result = new List<Faction>();
        foreach (var g in join.GroupBy(kv => kv.Value).Where(g => g.Count() >= 2))
        {
            var members = g.Select(kv => kv.Key).ToArray();
            var shared = core.Where(s => members.Count(m => m.Acc.GetValueOrDefault(s) == Truth.True || s.Author == m.Id) >= 2).Select(s => s.Text).ToImmutableArray();
            result.Add(new([.. members.Select(m => m.Id).Order()], g.Key.Id, shared));
        }
        return result.OrderByDescending(f => f.Members.Length).ToList();
    }
}
