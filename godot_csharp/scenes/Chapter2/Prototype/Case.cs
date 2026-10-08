using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>
/// 《少了一份》案发次日清晨的痕迹。哪个寝区的废纸篓里有饼屑、夜巡望见什么身量的身影，都按推演的实际结果记，
/// 不预设是谁（多日交互审核 M1）。
/// </summary>
public sealed record CaseTraces(int LedgerShort, bool LatchFixed, bool LatchToolMarks, bool BowlPorridge, IReadOnlyList<Place> CrumbDorms, IReadOnlyList<Build> Figures)
{
    public static readonly string[] Labels = [.. CaseDesign.Traces.Select(t => t.Label)];
    public static readonly Place[] Dorms = [Place.DormW, Place.DormLife, Place.DormS, Place.DormL];
    /// <summary>值相等：两组痕迹描述出来完全相同即相等（列表字段按内容比较）。</summary>
    public bool Equals(CaseTraces? other) => other is not null && Describe().Select(d => d.Text).SequenceEqual(other.Describe().Select(d => d.Text));
    public override int GetHashCode() => string.Join("|", Describe().Select(d => d.Text)).GetHashCode();
    /// <summary>lookedLitters：只描述看过的那几个寝区的废纸篓（没看过的不说，避免替侦探看）；null 表示全部。</summary>
    public IEnumerable<(int Item, string Text)> Describe(IReadOnlyCollection<Place>? lookedLitters = null)
    {
        yield return (0, $"台账：无记录取用×{LedgerShort}");
        yield return (1, LatchFixed ? "粮仓门闩：新的工具痕，已紧固" : "粮仓门闩：仍然松动");
        yield return (2, BowlPorridge ? "诊疗所：莫兰床边空碗，有粥渍" : "诊疗所：碗是干净的");
        var dorms = Dorms.Where(d => lookedLitters is null || lookedLitters.Contains(d)).ToArray();
        yield return (3, string.Join("；", dorms.Select(d => $"{Names.Of(d)}废纸篓：" + (CrumbDorms.Contains(d) ? "一份口粮的包纸与饼屑" : "没有饼屑"))));
        var builds = Figures.Distinct().OrderBy(b => b).Select(b => Game.BuildText(b) + "身影").ToArray();
        yield return (4, builds.Length == 0 ? "夜巡：后勤院没见人影" : "夜巡：中央厅望见后勤院有" + string.Join("、", builds));
    }
}

/// <summary>重演变量：全部是“处境与人心”，不指定任何人做了什么。</summary>
public sealed record CaseVars(int MoraBelief, int QuerRule, int MargaretRule, int QuerSupper)
{
    public static readonly string[][] Options = [.. CaseDesign.Variables.Select(v => v.Options)];
    public static readonly string[] Questions = [.. CaseDesign.Variables.Select((v, i) => $"V{i + 1} {v.Question}")];
    public static readonly CaseVars Truth = new(0, 0, 0, 0);
    public static IEnumerable<CaseVars> All()
    {
        for (var a = 0; a < 3; a++) for (var b = 0; b < 3; b++) for (var c = 0; c < 2; c++) for (var d = 0; d < 2; d++) yield return new(a, b, c, d);
    }
    public override string ToString() => $"V1={Options[0][MoraBelief]} V2={Options[1][QuerRule]} V3={Options[2][MargaretRule]} V4={Options[3][QuerSupper]}";
}

public sealed partial class Game
{
    public CaseTraces? CaseActual;

    public CaseTraces CaptureTraces() => new(
        LedgerShort: Math.Max(0, ExpectedRations() - Get(Granary, T.Ration)) + Get(Ledger, T.Unrecorded),
        LatchFixed: Get(Granary, T.LatchLoose) == 0,
        LatchToolMarks: Get(Granary, T.ToolMarks) > 0,
        BowlPorridge: Get(Bowl, T.Residue) > 0,
        CrumbDorms: [.. CaseTraces.Dorms.Where(d => Get(Litter[d], T.Crumbs) > 0)],
        Figures: [.. Sightings.Where(s => s.Day == CaseDesign.Night && s.Witness == P.Bertrand && s.At == Place.Logistics).Select(s => s.Build).Distinct().OrderBy(b => b)]);

    /// <summary>侦探细看过废纸篓的那几个寝区。</summary>
    public IReadOnlyCollection<Place> DetLookedLitters() => [.. CaseTraces.Dorms.Where(d => DetLooked.Contains(Litter[d]))];

    /// <summary>第 3 日清晨（不跑早上的行动）：结算昨夜并记录痕迹。</summary>
    public CaseTraces Dawn()
    {
        if (!(Day == CaseDay && Slot == Slot.Morning)) throw new InvalidOperationException("只在案发次日清晨取痕迹");
        if (!_dawnDone) { BeginDay(); _dawnDone = true; }
        return CaseActual ?? CaptureTraces();
    }
    private bool _dawnDone;

    /// <summary>
    /// 信念重演：新建一个独立世界，从第 2 日晨起按同一套规则推演到第 3 日清晨。变量只改认知与处境
    /// （谁读过并接受了哪条规则、谁知道谁病了、谁吃过晚饭），行动全部由各人的 DecisionSystem 推出。
    /// </summary>
    public (CaseTraces Traces, Game World) ReplayCase(CaseVars v)
    {
        Game? world = null;
        var options = Options with
        {
            Quiet = true, StartRations = Options.StartRations, Inherit = InheritedSnapshot ?? Options.Inherit, RestoreNight = CaseNightSnapshot ?? Options.RestoreNight,
            ForceAcceptance = (gw, p, s) =>
            {
                if (p.Id == P.Quer && s == gw.MoraNight) return v.QuerRule == 0 ? Truth.True : Truth.False;
                if (p.Id == P.Quer && s == gw.WardNight) return v.QuerRule == 1 ? Truth.True : Truth.False;
                if (p.Id == P.Margaret && (s == gw.WardNight || s == gw.WardLedger)) return v.MargaretRule == 1 ? Truth.True : null;
                return null;
            },
            BlockNews = (p, item) => p.Id == P.Mora && v.MoraBelief != 0 && item.FactTag == T.Sick,
            BeforeNight = g =>
            {
                if (g.Day != CaseNight) return;
                if (v.MoraBelief == 2) g.Set(g[P.Mora].Body, T.Fed, 0);
                if (v.QuerRule == 1) g.Tell(g[P.Quer], g.WardNight, Channel.Board);
                if (v.MargaretRule == 1)
                {
                    g.Tell(g[P.Margaret], g.WardLedger, Channel.Board);
                    g.Tell(g[P.Margaret], g.WardNight, Channel.Board);
                    g.Know(g[P.Margaret], g[P.Quer].Body, T.Branded, 1, ObservationSource.Testimony, "沃德的规则");
                }
                if (v.QuerSupper == 1 && Game.Get(g[P.Quer].Body, T.Fed) == 0)
                {
                    g.Set(g[P.Quer].Body, T.Fed, 1);
                    g.Add(g.Granary, T.Ration, -1);
                    g.Add(g.Ledger, T.Handed, 1);
                }
                if (v.QuerSupper == 0 && Game.Get(g[P.Quer].Body, T.Fed) > 0)
                {
                    g.Set(g[P.Quer].Body, T.Fed, 0);
                    g.Add(g.Granary, T.Ration, 1);
                    g.Add(g.Ledger, T.Handed, -1);
                }
                g.Rt.Sync();
            }
        };
        // 现实快照：按侦探真实的行动记录重放第 2 日（§11 第 10 条），只改变量所说的认知与处境。
        var nightSlot = (CaseNight - FirstDay) * 6 + (int)Slot.Night;
        world = Rebuild(Script, FreeScript, nightSlot, options, PlaceScript, StoryChoices, FreeEvents);
        world.RestoreFree(nightSlot, PlaceScript, StoryChoices, FreeEvents);
        world.RunSlot(Script.Count > nightSlot ? Script[nightSlot] : null);
        return (world.Dawn(), world);
    }

    /// <summary>侦探第 2 日什么都不做的那条现实下的重演（自动检查用）。</summary>
    public static (CaseTraces Traces, Game World) Replay(CaseVars v)
    {
        var idle = new Game(new GameOptions { Quiet = true });
        for (var i = 0; i <= (CaseNight - FirstDay) * 6 + (int)Slot.Night; i++) idle.RunSlot(null);
        return idle.ReplayCase(v);
    }

    /// <summary>对照：只比较侦探已经查过的痕迹，没查过的只给地点。</summary>
    public List<string> Compare(CaseTraces replay, CaseTraces actual, ISet<int>? known = null)
    {
        var lines = new List<string>();
        var looked = known is null ? null : DetLookedLitters();
        var r = replay.Describe(looked).ToArray();
        var a = actual.Describe(looked).ToArray();
        string[] where = [.. CaseDesign.Traces.Select(t => t.Where)];
        for (var i = 0; i < r.Length; i++)
        {
            if (known is not null && !known.Contains(i)) { lines.Add($"未查：你还没查过这里——{where[i]}"); continue; }
            lines.Add(r[i].Text == a[i].Text ? $"吻合：{a[i].Text}" : $"冲突：重演是「{r[i].Text}」，现实是「{a[i].Text}」");
        }
        return lines;
    }

    /// <summary>侦探已查过的痕迹（以他内部世界里的感知与证言为准）。</summary>
    public HashSet<int> DetEvidence()
    {
        var known = new HashSet<int>();
        for (var i = 0; i < CaseDesign.Traces.Count; i++)
        {
            if (CaseDesign.Traces[i].Known.Eval(this)) known.Add(i);
        }
        return known;
    }

    public readonly HashSet<P> DetAsked = [];
    /// <summary>侦探在第 3 日起专门细看过的物件（调查或同行）。</summary>
    public readonly HashSet<Essence> DetLooked = [];
    private readonly Dictionary<int, double> _dayStart = [];
    public double DayStart(int day) => _dayStart.GetValueOrDefault(day, double.MaxValue);

    // ------------------------------------------------------------ 对质 --

    public static readonly string[] Claims = [.. CaseDesign.Claims.Select(c => c.Text)];
    public static readonly string[] EvidenceKeys = [.. CaseDesign.EvidenceList.Select(e => e.Key)];
    public static readonly string[] EvidenceNames = [.. CaseDesign.EvidenceList.Select(e => e.Name)];

    private bool ClaimTrue(int c) => c >= 0 && c < CaseDesign.Claims.Count && CaseDesign.Claims[c].True.Eval(this);

    private static readonly ImmutableDictionary<int, int[]> Relevant = CaseDesign.Claims
        .Select((c, i) => (i, c.Relevant))
        .ToImmutableDictionary(x => x.i, x => x.Relevant);

    public bool DetHasEvidence(int evidence) => DetHas(evidence);

    private (int Known, IReadOnlyList<CaseVars> Fits, IReadOnlyList<int> Claims)? _inference;

    /// <summary>
    /// 侦探自己的推断（与第一章同一种推理：信念重演 + 对照）。把作者侧四变量的每种组合各重演一遍，
    /// 只与侦探亲自查过的痕迹对照；与所见全部吻合的组合留下。在留下的每个假设世界里都成立的陈述，才是他说得出口的；
    /// 冤枉人的陈述不在其列（侦探的底线）。没查过任何痕迹时什么也推不出。结果按已查痕迹缓存。
    /// </summary>
    public (IReadOnlyList<CaseVars> Fits, IReadOnlyList<int> Claims) DetectiveInference()
    {
        var known = DetEvidence();
        var litters = DetLookedLitters();
        var key = known.Aggregate(0, (h, i) => h | 1 << i) | CaseTraces.Dorms.Select((d, i) => litters.Contains(d) ? 1 << (16 + i) : 0).Sum();
        if (_inference is { } cached && cached.Known == key) return (cached.Fits, cached.Claims);
        if (known.Count == 0) return ([], []);
        if (CaseActual is not { } actual) return ([], []);
        var fits = new List<(CaseVars Vars, Game World)>();
        foreach (var v in CaseVars.All())
        {
            var (traces, world) = ReplayCase(v);
            if (Compare(traces, actual, known).All(l => !l.StartsWith("冲突"))) fits.Add((v, world));
        }
        var claims = Enumerable.Range(0, CaseDesign.Claims.Count)
            .Where(c => !CaseDesign.Claims[c].Frames && fits.Count > 0 && fits.All(f => f.World.ClaimTrue(c))).ToArray();
        _inference = (key, fits.Select(f => f.Vars).ToArray(), claims);
        return (_inference.Value.Fits, claims);
    }

    /// <summary>自动检查用：现实里为真的（不冤枉人的）主张，按对质话题的先后，各配一件侦探手里相关、亲眼查过的优先的证据。</summary>
    public IReadOnlyList<(int Claim, int Evidence)> ValidConfrontPairs()
    {
        var order = new[] { "take", "latch", "ledger" };
        return [.. Enumerable.Range(0, CaseDesign.Claims.Count)
            .Where(c => !CaseDesign.Claims[c].Frames && order.Contains(CaseDesign.Claims[c].Topic) && ClaimTrue(c))
            .OrderBy(c => Array.IndexOf(order, CaseDesign.Claims[c].Topic)).ThenBy(c => c)
            .Select(c => (c, DetectiveSupport(c).DefaultIfEmpty(-1).First())).Where(p => p.Item2 >= 0)];
    }

    /// <summary>侦探手里能撑住这句话的东西：按对质规则与此陈述相关、且他确实持有；亲眼查过的排在前面。</summary>
    public IReadOnlyList<int> DetectiveSupport(int claim)
        => claim >= 0 && Relevant.TryGetValue(claim, out var rel) ? rel.Where(DetHas).OrderByDescending(e => PersonallySaw(Det, e)).ToArray() : [];
    private bool DetHas(int evidence) => evidence >= 0 && evidence < CaseDesign.EvidenceList.Count && CaseDesign.EvidenceList[evidence].Have.Eval(this);

    private bool PersonallySaw(Person p, int evidence) => evidence >= 0 && evidence < CaseDesign.EvidenceList.Count && (CaseDesign.EvidenceList[evidence].Saw?.Eval(this, p) ?? false);

    public sealed record ConfrontPlan(IReadOnlyList<(int Claim, int Evidence)> Pairs, Card? Proposal, bool CommonRule);
    public readonly List<string> ConfrontLog = [];
    public bool QuerCleared, QuerOstracized, CommonRuleEstablished;
    public RuleSpec? CaseRule;

    /// <summary>
    /// 第 4 日中央厅的公开对质（读与写）。陈述与出示成对；有效的一对让听者积累“材料”：亲见过的 +2，
    /// 信侦探的转述 +1，当事人当场承认按听者对她的信任再 +1。够 3 点的人改写心中对雀儿的认定。
    /// </summary>
    public void Confront(ConfrontPlan? plan, bool reopen = false)
    {
        var room = People.Where(p => Get(p.Body, T.Sick) == 0).ToArray();
        foreach (var p in room) p.Body.Get<SpatialProperty>()!.Space = Space[Place.Hall];
        Rt.Sync();
        PerceiveAll();
        var quer = this[Accused ?? P.Quer];
        var logStart = ConfrontLog.Count;
        // 对账在晚饭之后（场面“晚饭后。中央厅点起了灯”）：侦探来对账，不因此错过这一顿。
        if (!reopen && Slot == Slot.Evening && Get(Det.Body, T.Fed) == 0 && Get(Granary, T.Ration) > 0)
        {
            Add(Granary, T.Ration, -1);
            Add(Ledger, T.Handed, 1);
            Set(Det.Body, T.Fed, 1);
            Feed.Add(X("对质.先领晚饭"));
        }
        if (!reopen)
        {
            var ledgerShort = Get(Ledger, T.Unrecorded) > 0 || ExpectedRations() > Get(Granary, T.Ration);
            ConfrontLog.Add(X(Accused is not null
                ? ledgerShort ? "对质.开场指控缺口" : "对质.开场指控补齐"
                : ledgerShort ? "对质.开场无人缺口" : "对质.开场无人补齐", ("人", quer.Name)));
        }
        if (plan is null || plan.Pairs.Count == 0)
        {
            // 没有出示：指控照旧搁着；但提议新规则、提议共同规则不依赖出示，照样当众提出（不出示也能说话）。
            ConfrontLog.Add(X(reopen ? "对质.重开未出示" : plan is null ? "对质.未说话" : "对质.未出示"));
            if (Accused is not null && !reopen) Ostracize();
            if (plan is not null) Propose(plan, room);
            Feed.AddRange(ConfrontLog.Skip(logStart));
            return;
        }
        var points = room.ToDictionary(p => p, _ => 0);
        foreach (var (claim, evidence) in plan.Pairs)
        {
            var valid = ClaimTrue(claim) && Relevant[claim].Contains(evidence) && DetHas(evidence);
            var line = X("对质.出示", ("主张", Claims[claim]), ("证据", EvidenceNames[evidence]));
            var frames = claim >= 0 && claim < CaseDesign.Claims.Count && CaseDesign.Claims[claim].Frames;
            if (!valid)
            {
                var why = X(!ClaimTrue(claim) ? "对质.主张不实" : !DetHas(evidence) ? "对质.未持证据" : "对质.证据不相关");
                ConfrontLog.Add(line + X("对质.出示无效", ("缘由", why)));
                Witness(Det, K("对质失言", P.Det, values: ("话", Claims[claim])), frames ? -1 : 0, -1, room.Where(p => p != Det), domain: Dom.Rations);
                if (frames) Witness(Det, K("冤枉", P.Det, quer.Id), -2, 0, [quer], spread: false);
                continue;
            }
            ConfrontLog.Add(line + " ——" + Reaction(claim));
            if (frames) { foreach (var p in room) points[p] -= 2; continue; }
            foreach (var p in room.Where(p => p != Det))
            {
                var pts = PersonallySaw(p, evidence) ? 2 : TrustIn(p, Det) >= 1 ? 1 : 0;
                // 当事人当场承认（说的是谁，听的人信他）；出示的是某人的话（听的人信说话的人）：各再 +1。
                if (CaseDesign.Claims[claim].Person is { } about && about != p.Id && TrustIn(p, this[about]) >= 1) pts += 1;
                else if (CaseDesign.EvidenceList[evidence].Person is { } speaker && speaker != p.Id && TrustIn(p, this[speaker]) >= 1) pts += 1;
                points[p] += pts;
            }
        }
        var revised = new List<Person>();
        foreach (var p in room.Where(p => p != Det && p != quer))
        {
            var believed = (Belief(p, quer, T.Branded) ?? 0) > 0 || (Belief(p, quer, T.Accused) ?? 0) > 0;
            if (!believed) continue;
            if (points[p] >= 3)
            {
                foreach (var t in new[] { T.Branded, T.Suspect, T.Accused }) Know(p, quer.Body, t, 0, ObservationSource.Inference, "对质");
                Witness(quer, K("认清无私取", p.Id, quer.Id), 1, 0, [p], spread: false);
                revised.Add(p);
            }
        }
        var ward = this[P.Ward];
        QuerCleared = revised.Contains(ward) || Accused is null && revised.Count > 0;
        if (QuerCleared && QuerOstracized)
        {
            QuerOstracized = false;
            quer.Body.Get<SpatialProperty>()!.Space = Space[quer.Pro.Dorm];
            RealDirty = true;
            ConfrontLog.Add(X("对质.搬回", ("人", quer.Name), ("寝区", Names.Of(quer.Pro.Dorm))));
        }
        if (QuerCleared && Accused is null)
        {
            ConfrontLog.Add(X("对质.缺口说清", ("人们", JoinNames(revised.Select(p => p.Name)))));
            Witness(Det, K("推断证实", P.Det), 0, 2, room.Where(p => p != Det), domain: Dom.Rations);
        }
        else if (QuerCleared)
        {
            ConfrontLog.Add(X("对质.撤销指控", ("人们", JoinNames(revised.Select(p => p.Name)))));
            Witness(Det, K("洗清冤屈", P.Det, quer.Id), 2, 1, [quer]);
            // 推得准是“判断”，只在口粮与案子这一领域；为人不因此上涨（§11 第 4 条）。
            Witness(Det, K("推断证实", P.Det), 0, 2, room.Where(p => p != Det && p != quer), domain: Dom.Rations);
            foreach (var m in Manipulations.Where(m => m.About == P.Quer))
            {
                Witness(Det, K("片面之词", P.Det, m.Target), -2, 0, [this[m.Target]]);
                m.Exposed = true;
                ConfrontLog.Add(X("对质.片面被揭", ("人", Names.Of(m.Target)), ("称谓", Cast.Of(m.Target).Pronoun)));
            }
        }
        else
        {
            ConfrontLog.Add(X("对质.材料不足", ("回应", X(revised.Count > 0 ? "对质.部分改观" : "对质.无人改观", ("人们", JoinNames(revised.Select(p => p.Name)))))));
            if (!reopen) Ostracize();
        }
        Propose(plan, room);
        Feed.AddRange(ConfrontLog.Skip(logStart));
    }

    /// <summary>
    /// 共同规则成立的门槛：在场者（侦探除外）过半数接受。规则本身只约束接受它的人；“共同”只说明它已是多数人的规矩，
    /// 之后重开对质可以援引它（第三部分 §7）。
    /// </summary>
    private static bool CommonEnough(int accepted, int present) => accepted * 2 > present;
    /// <summary>侦探在对质中提出的规矩是否过了半数（立住了）；共同规则是否提过。</summary>
    public bool CaseRuleAdopted, CommonRuleProposed;

    private void Propose(ConfrontPlan plan, Person[] room)
    {
        if (plan.Proposal is { } card)
        {
            CaseRule = CardSpec(card, P.Det);
            Det.Acc[CaseRule] = Truth.True;
            Post(CaseRule, Place.Hall);
            ConfrontLog.Add(X("对质.提出新规", ("规矩", CaseRule.Text)));
            var said = RecordEvent(P.Det, X("对质.新规事件", ("规矩", Short(CaseRule))), "交谈", Place.Hall, []);
            foreach (var p in room.Where(p => p != Det)) { Tell(p, CaseRule, Channel.Public); _transmit[(CaseRule.Id, p.Id)] = said; }
            foreach (var p in room.Where(p => p != Det)) NoteFor(p.Id, "规则", X("对质.新规旁注", ("规矩", card.Text), ("称谓", p.Pro.Pronoun),
                ("态度", X(p.Acc[CaseRule] == Truth.True ? "对质.旁注照办" : p.Vetoed.Contains(CaseRule) ? "对质.旁注拒绝" : "对质.旁注未承诺"))));
            foreach (var key in new[] { P.Ward, P.Mora, P.Ruan, P.Quer })
            {
                ConfrontLog.Add(X("对质.新规表态", ("人", Names.Of(key)), ("话", Voice(this[key], CaseRule, this[key].Acc[CaseRule]))));
                DebugLog.Add($"对质 {Names.Of(key)} 对新规则：{this[key].Why[CaseRule]}");
            }
            var all4 = new[] { P.Ward, P.Mora, P.Ruan, P.Quer }.All(k => this[k].Acc[CaseRule] == Truth.True);
            var n = room.Count(p => p.Acc.GetValueOrDefault(CaseRule) == Truth.True);
            CaseRuleAdopted = CommonEnough(n, room.Count(p => p != Det));
            ConfrontLog.Add(X(n >= room.Length - 2 ? "对质.几乎都点头" : CaseRuleAdopted ? "对质.过半点头" : "对质.不到一半"));
            if (all4) Witness(Det, K("规则公平", P.Det), 0, 1, room.Where(p => p != Det), domain: Dom.Rations);
        }
        if (plan.CommonRule)
        {
            Det.Acc[CommonRule] = Truth.True;
            Post(CommonRule, Place.Hall);
            foreach (var p in room.Where(p => p != Det)) Tell(p, CommonRule, Channel.Public);
            var n = room.Count(p => p != Det && p.Acc.GetValueOrDefault(CommonRule) == Truth.True);
            CommonRuleEstablished = CommonEnough(n, room.Count(p => p != Det));
            CommonRuleProposed = true;
            var nodders = room.Where(p => p != Det && p.Acc.GetValueOrDefault(CommonRule) == Truth.True).Select(p => p.Name).ToArray();
            ConfrontLog.Add(X("对质.共同规则提议", ("规矩", CommonRule.Text)) + (nodders.Length == 0 ? X("对质.共同规则无人点头") : X("对质.共同规则点头", ("人们", JoinNames(nodders))))
                + X(CommonRuleEstablished ? "对质.共同规则成立" : "对质.共同规则不成"));
            var uncommitted = room.Where(p => p != Det && p.Acc.GetValueOrDefault(CommonRule) != Truth.True && !p.Vetoed.Contains(CommonRule)).ToArray();
            var refusers = room.Where(p => p != Det && p.Acc.GetValueOrDefault(CommonRule) != Truth.True && p.Vetoed.Contains(CommonRule)).ToArray();
            foreach (var (people, key) in new[] { (uncommitted, "对质.共同规则未承诺"), (refusers, "对质.共同规则拒绝") })
            {
                if (people.Length == 0) continue;
                var order = new[] { P.Lucian, P.Veronica, P.Ward, P.Quer, P.Ruan, P.Mora };
                var voices = people.Where(p => order.Contains(p.Id)).OrderBy(p => Array.IndexOf(order, p.Id)).Take(3)
                    .Select(p => X("对质.共同规则一句", ("人", p.Name), ("话", Voice(p, CommonRule, Truth.False))));
                ConfrontLog.Add(X(key, ("各人", JoinNames(people.Select(p => p.Name))), ("几句", string.Join("，", voices))));
            }
        }
    }

    private int _reopenedDay;
    public int ReopenedDay => _reopenedDay;

    /// <summary>
    /// 重开对质（§12 第 4 条）：第 4 日之后的任一日，援引第一条共同规则“公开指控须附可检查的材料”，请众人回中央厅再对一次。
    /// 共同规则还没成立的，先当众再提一次；证物不随日消失，没被清理的仍在原处，被清理的留下清理的痕迹。
    /// </summary>
    private void Reopen(ConfrontPlan? plan)
    {
        if (_reopenedDay == Day) { Feed.Add("今天已经重开过一次对质了。"); return; }
        if (Day < CaseDay || Day == CaseDay && Slot < Slot.Night) { Feed.Add("翌日傍晚的对质还没开始。"); return; }
        if (QuerCleared || Accused is null) { Feed.Add("眼下没有悬着的指控要重开。"); return; }
        _reopenedDay = Day;
        var head = $"你援引「{CommonRule.Text}」，请众人回中央厅，把台账的事重新对一遍。";
        Feed.Add(head);
        if (!CommonRuleEstablished)
        {
            var room = People.Where(p => p != Det && Get(p.Body, T.Sick) == 0).ToArray();
            foreach (var p in room) Tell(p, CommonRule, Channel.Public);
            Det.Acc[CommonRule] = Truth.True;
            CommonRuleEstablished = CommonEnough(room.Count(p => p.Acc.GetValueOrDefault(CommonRule) == Truth.True), room.Length);
            Feed.Add(CommonRuleEstablished ? "这一次，点头的人够了：它成了十六人的第一条共同规则。" : "点头的人还不够；但人都来了，对质照开。");
            if (CommonRuleEstablished && !CommonRule.PostedAt.Contains(Place.Hall)) Post(CommonRule, Place.Hall);
        }
        Confront(plan, reopen: true);
    }

    private static string Reaction(int claim) => claim >= 0 && claim < CaseDesign.Claims.Count ? CaseDesign.Claims[claim].Reaction : "";

    /// <summary>指控成立或不了了之：雀儿被排斥，对没有改变看法的人更不信任，转而独行。</summary>
    private void Ostracize()
    {
        if (Accused is not { } acc) return;
        QuerOstracized = true;
        var who = this[acc];
        foreach (var p in People.Where(p => p != who && ((Belief(p, who, T.Branded) ?? 0) > 0 || (Belief(p, who, T.Accused) ?? 0) > 0)))
            Witness(p, K("仍当作贼", p.Id, who.Id), -2, 0, [who], spread: false);
        if (acc == P.Quer)
        {
            ConfrontLog.Add(X("对质.雀儿未撤指控"));
            ConfrontLog.Add(X("对质.雀儿搬出"));
            who.Body.Get<SpatialProperty>()!.Space = Space[Place.Oath];
        }
        else ConfrontLog.Add(X("对质.他人被排斥", ("人", who.Name)));
        Rt.Sync();
    }

    public sealed class Manipulation
    {
        public required P Target { get; init; }
        public required P About { get; init; }
        public required string Means { get; init; }
        public required int Day { get; init; }
        public bool Exposed { get; set; }
        public bool Apologized { get; set; }
    }
    public readonly List<Manipulation> Manipulations = [];
}
