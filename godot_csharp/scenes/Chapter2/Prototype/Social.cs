using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public enum NewsKind { Fact, Sighting, Accusation, Defense, Reputation, Lesson, Ledger, Rumor, DemonWord }

/// <summary>一条可以被转述的信息。来源链保存在每个听者的 Heard 里。</summary>
public sealed class NewsItem
{
    public required int Id { get; init; }
    public required NewsKind Kind { get; init; }
    public required string Text { get; init; }
    public required P Origin { get; init; }
    public P? About { get; init; }
    public int TrustDelta { get; init; }
    public int InsightDelta { get; init; }
    public Essence? FactOn { get; init; }
    public Tag? FactTag { get; init; }
    public int FactValue { get; init; }
    public int Day { get; init; }
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
    public void Witness(Person subject, string text, int trust, int insight, IEnumerable<Person> witnesses, bool spread = true, bool manipulation = false, Dom domain = Dom.General)
    {
        var list = witnesses.Where(w => w != subject).Distinct().ToArray();
        foreach (var w in list)
        {
            Observation.Observe(w.Soul, new ObservationInput(text, ProjectionKind.Event)
            {
                Source = ObservationSource.Perception, Existence = Truth.True, Time = Now, Details = text,
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, trust).Add(T.Insight, insight).Add(T.Domain, (int)domain),
                AddRelations = [(Relations.Subject, w.M[subject.Body])]
            });
        }
        if (!spread || list.Length == 0) return;
        var item = NewItem(NewsKind.Reputation, text, list[0].Id, subject.Id, trust, insight, manipulation: manipulation, domain: domain);
        foreach (var w in list) w.News[item.Id] = new(w.Id, 0, ObservationSource.Perception, Day, text);
        Log.Add($"D{Day} {Names.Of(Slot)} 目睹：{text}（信{trust:+0;-0;0} 识{insight:+0;-0;0}）→ {string.Join("、", list.Select(w => w.Name))}");
    }

    private NewsItem NewItem(NewsKind kind, string text, P origin, P? about, int trust = 0, int insight = 0, Essence? on = null, Tag? tag = null,
        int value = 0, Build? build = null, Place? place = null, int lesson = 0, bool manipulation = false, Dom domain = Dom.General)
    {
        var item = new NewsItem
        {
            Id = AllNews.Count + 1, Kind = kind, Text = text, Origin = origin, About = about, TrustDelta = trust, InsightDelta = insight,
            FactOn = on, FactTag = tag, FactValue = value, Day = Day, Build = build, Place = place, Lesson = lesson, Manipulation = manipulation, Domain = domain
        };
        AllNews.Add(item);
        return item;
    }

    public NewsItem NewsFrom(Person origin, NewsKind kind, string text, Person? about = null, int trust = 0, int insight = 0, Essence? on = null,
        Tag? tag = null, int value = 0, Build? build = null, Place? place = null, int lesson = 0, bool manipulation = false)
    {
        var item = NewItem(kind, text, origin.Id, about?.Id, trust, insight, on, tag, value, build, place, lesson, manipulation);
        origin.News[item.Id] = new(origin.Id, 0, ObservationSource.Perception, Day, text);
        return item;
    }

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
            Relays(here);
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
                Witness(a, $"{a.Name}和我一样认「{Short(s)}」", 1, 0, [b], spread: false);
                Witness(b, $"{b.Name}和我一样认「{Short(s)}」", 1, 0, [a], spread: false);
            }
    }

    private static bool Holds(Person p, RuleSpec s) => s.Author == p.Id || p.Acc.GetValueOrDefault(s) == Truth.True;
    public static string Short(RuleSpec s) => s.Text.Length > 14 ? s.Text[..14] + "…" : s.Text;

    /// <summary>
    /// 信任链传播（§11 第 3 条）：接受得真心（或自己立的）规则，会被转述给同处一地、信得过自己、还没听过的人。
    /// 听者的判断里，转述者的担保代替大半作者的分量。每人每轮最多转述一条；被底线否决过的不再重判。
    /// </summary>
    private void Relays(Person[] here)
    {
        foreach (var teller in here.Where(t => t != Det))
        {
            var spec = Specs.Where(s => !s.ByDemon && s.Author is not null && s != RuanLedger && Holds(teller, s) &&
                    (s.Author == teller.Id || AcceptScore(teller, s, Channel.Relay, null, out _) >= Options.AcceptThreshold + 2))
                .OrderByDescending(s => s.Author == P.Det ? 1 : 0).ThenByDescending(s => s.Id)
                .FirstOrDefault(s => here.Any(h => h != teller && h != Det && !h.Acc.ContainsKey(s) && TrustIn(h, teller) >= 1));
            if (spec is null) continue;
            foreach (var hearer in here.Where(h => h != teller && h != Det && !h.Acc.ContainsKey(spec) && TrustIn(h, teller) >= 1))
            {
                var v = Tell(hearer, spec, Channel.Relay, teller);
                RelayLog.Add((Day, Slot, teller.Id, hearer.Id, spec.Id, v == Truth.True));
            }
        }
    }

    public readonly List<(int Day, Slot Slot, P Teller, P Hearer, int Spec, bool Accepted)> RelayLog = [];

    public void Receive(Person teller, Person hearer, NewsItem item, Heard via, Channel channel = Channel.Relay)
    {
        var trust = TrustIn(hearer, teller);
        var text = via.Text;
        if (hearer.News.Values.Any(h => h.Text == text)) { hearer.News[item.Id] = new(teller.Id, via.Hops + 1, ObservationSource.Testimony, Day, text); return; }
        var delta = item.TrustDelta;
        if (item.About is { } about && delta < 0 && teller.Pro.Prejudice.GetValueOrDefault(about) < 0 && !text.Contains("（据"))
        {
            delta -= 1;
            text += $"（据{teller.Name}说，这是{(about == P.Quer ? "私取的老毛病" : "惯常的事")}）";
        }
        var scaled = trust >= 2 ? delta : trust >= 0 ? delta / 2 : 0;
        var insight = trust >= 2 ? item.InsightDelta : trust >= 0 ? item.InsightDelta / 2 : 0;
        hearer.News[item.Id] = new(teller.Id, via.Hops + 1, ObservationSource.Testimony, Day, text);
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
        if (item.Kind == NewsKind.Accusation && item.About is { } acc && trust >= 0) Know(hearer, this[acc].Body, T.Accused, 1, ObservationSource.Testimony, text);
        if (item.Kind == NewsKind.Lesson && !hearer.Lessons.ContainsKey(item.Lesson))
        {
            var full = teller.Lessons.GetValueOrDefault(item.Lesson) && (teller.Pro.Signal >= 3 || trust >= 2 && RelayAccepted(teller));
            hearer.Lessons[item.Lesson] = full;
            Log.Add($"D{Day} {Names.Of(Slot)} {hearer.Name} 从{teller.Name}处听到第{item.Lesson}课（{(full ? "完整" : "走样")}）");
        }
        if (hearer == Det) Feed.Add($"{teller.Name}说：{text}");
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

    /// <summary>派系 = 相互信任 ≥ 2、且至少共享一条已接受的人立规则的人群（连通分量）；领头者是组内被信任总和最高的人。</summary>
    /// <summary>
    /// 派系 = 围绕立规者的追随（§11 第 7 条）：每个立过人立规则的人（含侦探）是一个潜在的中心；
    /// 每个人投向“他认了最多条其规则、又最信”的那个中心（认一条 +2，触犯底线被否一条 −2，再加对中心的信任，上限 2）；
    /// 得分 ≥ 3 才算入派。中心本人自成其派。派系少于两人不计。
    /// </summary>
    public List<Faction> Factions()
    {
        var core = Specs.Where(s => !s.ByDemon && s != RuanLedger && s.Author is not null).ToArray();
        var centers = core.Select(s => s.Author!.Value).Distinct().Select(a => this[a]).ToArray();
        int Pull(Person p, Person c) =>
            core.Where(s => s.Author == c.Id).Sum(s => p.Acc.GetValueOrDefault(s) == Truth.True ? 2 : p.Vetoed.Contains(s) ? -2 : 0)
            + Math.Clamp(c == Det ? ImageOfDet(p).Credible : TrustIn(p, c), -3, 2);
        var join = new Dictionary<Person, Person>();
        foreach (var p in People)
        {
            if (centers.Contains(p)) { join[p] = p; continue; }
            var best = centers.Select(c => (c, score: Pull(p, c))).OrderByDescending(x => x.score).ThenBy(x => x.c.Id).FirstOrDefault();
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
