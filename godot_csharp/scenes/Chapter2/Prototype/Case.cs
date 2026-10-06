using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>《少了一份》第 3 日清晨的痕迹。</summary>
public sealed record CaseTraces(int LedgerShort, bool LatchFixed, bool LatchToolMarks, bool BowlPorridge, bool QuerCrumbs, bool SmallFigure, bool MediumFigure)
{
    public static readonly string[] Labels = [.. CaseDesign.Traces.Select(t => t.Label)];
    public IEnumerable<(int Item, string Text)> Describe()
    {
        yield return (0, $"台账：无记录取用×{LedgerShort}");
        yield return (1, LatchFixed ? "粮仓门闩：新的工具痕，已紧固" : "粮仓门闩：仍然松动");
        yield return (2, BowlPorridge ? "诊疗所：莫兰床边空碗，有粥渍" : "诊疗所：碗是干净的");
        yield return (3, QuerCrumbs ? "甲区废纸篓：一份口粮的包纸与饼屑" : "甲区废纸篓：没有饼屑");
        yield return (4, SmallFigure ? "夜巡：中央厅望见后勤院有瘦小的身影" + (MediumFigure ? "，还有中等身量的身影" : "") : MediumFigure ? "夜巡：望见后勤院有中等身量的身影" : "夜巡：后勤院没见人影");
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
        QuerCrumbs: Get(Litter[Place.DormW], T.Crumbs) > 0,
        SmallFigure: Sightings.Any(s => s.Day == 2 && s.Witness == P.Bertrand && s.At == Place.Logistics && s.Build == Build.Small),
        MediumFigure: Sightings.Any(s => s.Day == 2 && s.Witness == P.Bertrand && s.At == Place.Logistics && s.Build == Build.Medium));

    /// <summary>第 3 日清晨（不跑早上的行动）：结算昨夜并记录痕迹。</summary>
    public CaseTraces Dawn()
    {
        if (!(Day == 3 && Slot == Slot.Morning)) throw new InvalidOperationException("只在第 3 日清晨取痕迹");
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
            Quiet = true, StartRations = Options.StartRations,
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
                if (g.Day != 2) return;
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
        world = Rebuild(Script, FreeScript, 5, options, PlaceScript, StoryChoices, FreeEvents);
        world.RestoreFree(5, PlaceScript, StoryChoices, FreeEvents);
        world.RunSlot(Script.Count > 5 ? Script[5] : null);
        return (world.Dawn(), world);
    }

    /// <summary>侦探第 2 日什么都不做的那条现实下的重演（自动检查用）。</summary>
    public static (CaseTraces Traces, Game World) Replay(CaseVars v)
    {
        var idle = new Game(new GameOptions { Quiet = true });
        for (var i = 0; i < 6; i++) idle.RunSlot(null);
        return idle.ReplayCase(v);
    }

    /// <summary>对照：只比较侦探已经查过的痕迹，没查过的只给地点。</summary>
    public List<string> Compare(CaseTraces replay, CaseTraces actual, ISet<int>? known = null)
    {
        var lines = new List<string>();
        var r = replay.Describe().ToArray();
        var a = actual.Describe().ToArray();
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
        if (!reopen) ConfrontLog.Add(Accused is not null ? $"中央厅。十六人到了十五个（莫兰还在诊疗所）。沃德重申：台账少了，{quer.Name}私取。" : "中央厅。十六人到了十五个。台账上的缺口还没人说清。");
        if (plan is null || plan.Pairs.Count == 0)
        {
            ConfrontLog.Add(reopen ? "你把人请来了，却什么也没出示。众人各自散去。" : "你没有说话。指控就这样搁着。");
            if (Accused is not null && !reopen) Ostracize();
            Feed.AddRange(ConfrontLog.Skip(logStart));
            return;
        }
        var points = room.ToDictionary(p => p, _ => 0);
        foreach (var (claim, evidence) in plan.Pairs)
        {
            var valid = ClaimTrue(claim) && Relevant[claim].Contains(evidence) && DetHas(evidence);
            var line = $"你：“{Claims[claim]}。”出示：{EvidenceNames[evidence]}。";
            var frames = claim >= 0 && claim < CaseDesign.Claims.Count && CaseDesign.Claims[claim].Frames;
            if (!valid)
            {
                var why = !ClaimTrue(claim) ? "这话不对" : !DetHas(evidence) ? "你手里没有这样东西" : "这东西证明不了这句话";
                ConfrontLog.Add(line + $" ——众人面面相觑（{why}）。");
                Witness(Det, $"侦探在对质中说了站不住的话：{Claims[claim]}", frames ? -1 : 0, -1, room.Where(p => p != Det), domain: Dom.Rations);
                if (frames) Witness(Det, "侦探当众冤枉我", -2, 0, [quer], spread: false);
                continue;
            }
            ConfrontLog.Add(line + " ——" + Reaction(claim));
            if (frames) { foreach (var p in room) points[p] -= 2; continue; }
            foreach (var p in room.Where(p => p != Det))
            {
                var pts = PersonallySaw(p, evidence) ? 2 : TrustIn(p, Det) >= 1 ? 1 : 0;
                if (claim == 0 && TrustIn(p, this[P.Mora]) >= 1) pts += 1;
                if (claim == 1 && evidence == 3 && TrustIn(p, this[P.Margaret]) >= 1) pts += 1;
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
                Witness(quer, $"{p.Name}在对质里认清了：{quer.Name}没有私取", 1, 0, [p], spread: false);
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
            ConfrontLog.Add($"{quer.Name}听说了，傍晚搬回了{Names.Of(quer.Pro.Dorm)}。");
        }
        if (QuerCleared && Accused is null)
        {
            ConfrontLog.Add($"台账上的缺口说清了。改变看法的人：{string.Join("、", revised.Select(p => p.Name))}。");
            Witness(Det, "侦探的推断在对质中被证实", 0, 2, room.Where(p => p != Det), domain: Dom.Rations);
        }
        else if (QuerCleared)
        {
            ConfrontLog.Add($"沃德沉默了一会：“依据……补记之前，不视为私取。”改变看法的人：{string.Join("、", revised.Select(p => p.Name))}。");
            Witness(Det, $"侦探替{quer.Name}洗清了冤屈", 2, 1, [quer]);
            // 推得准是“判断”，只在口粮与案子这一领域；为人不因此上涨（§11 第 4 条）。
            Witness(Det, "侦探的推断在对质中被证实", 0, 2, room.Where(p => p != Det && p != quer), domain: Dom.Rations);
            foreach (var m in Manipulations.Where(m => m.About == P.Quer))
            {
                Witness(Det, "侦探之前对我说的雀儿的事是片面的", -2, 0, [this[m.Target]]);
                m.Exposed = true;
                ConfrontLog.Add($"{Names.Of(m.Target)}看了你一眼：你之前对他说的，只说了一半。");
            }
        }
        else
        {
            ConfrontLog.Add($"沃德不为所动：“材料不足。”{(revised.Count > 0 ? "改变看法的只有：" + string.Join("、", revised.Select(p => p.Name)) : "没有人改变看法")}。");
            if (!reopen) Ostracize();
        }
        if (plan.Proposal is { } card)
        {
            CaseRule = CardSpec(card, P.Det);
            Det.Acc[CaseRule] = Truth.True;
            Post(CaseRule, Place.Hall);
            ConfrontLog.Add($"你提出新规则{LabelOf(CaseRule)}「{card.Text}」");
            var said = RecordEvent(P.Det, $"在对质中当众提出「{Short(CaseRule)}」", "交谈", Place.Hall, []);
            foreach (var p in room.Where(p => p != Det)) { Tell(p, CaseRule, Channel.Public); _transmit[(CaseRule.Id, p.Id)] = said; }
            foreach (var p in room.Where(p => p != Det)) NoteFor(p.Id, "规则", $"对质中你提出「{card.Text}」，他{(p.Acc[CaseRule] == Truth.True ? "表示照办" : "摇头")}");
            foreach (var key in new[] { P.Ward, P.Mora, P.Ruan, P.Quer })
            {
                ConfrontLog.Add($"  {Names.Of(key)}：{Voice(this[key], CaseRule, this[key].Acc[CaseRule])}");
                DebugLog.Add($"对质 {Names.Of(key)} 对新规则：{this[key].Why[CaseRule]}");
            }
            var all4 = new[] { P.Ward, P.Mora, P.Ruan, P.Quer }.All(k => this[k].Acc[CaseRule] == Truth.True);
            var n = room.Count(p => p.Acc.GetValueOrDefault(CaseRule) == Truth.True);
            ConfrontLog.Add(n >= room.Length - 2 ? "  几乎所有人都点了头。" : n >= room.Length / 2 ? "  点头的人过了半数。" : "  点头的人不到一半。");
            if (all4) Witness(Det, "侦探提出的规则被接受，结果公平", 0, 1, room.Where(p => p != Det), domain: Dom.Rations);
        }
        if (plan.CommonRule)
        {
            Det.Acc[CommonRule] = Truth.True;
            Post(CommonRule, Place.Hall);
            foreach (var p in room.Where(p => p != Det)) Tell(p, CommonRule, Channel.Public);
            var n = room.Count(p => p.Acc.GetValueOrDefault(CommonRule) == Truth.True);
            CommonRuleEstablished = n >= 12;
            ConfrontLog.Add($"你提议：「{CommonRule.Text}」点头的有：{string.Join("、", room.Where(p => p != Det && p.Acc.GetValueOrDefault(CommonRule) == Truth.True).Select(p => p.Name))}" +
                (CommonRuleEstablished ? "。十六人的第一条共同规则成立。" : "。人数不够，没有成为共同规则。"));
            var refusers = room.Where(p => p != Det && p.Acc.GetValueOrDefault(CommonRule) != Truth.True).ToArray();
            if (refusers.Length > 0) ConfrontLog.Add($"  没接受的：{string.Join("、", refusers.Select(p => $"{p.Name}{Voice(p, CommonRule, Truth.False)}"))}");
        }
        Feed.AddRange(ConfrontLog.Skip(logStart));
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
        if (Day < CaseDay || Day == CaseDay && Slot < Slot.Night) { Feed.Add("第 3 日傍晚的对质还没开始。"); return; }
        if (QuerCleared || Accused is null) { Feed.Add("眼下没有悬着的指控要重开。"); return; }
        _reopenedDay = Day;
        var head = $"你援引「{CommonRule.Text}」，请众人回中央厅，把台账的事重新对一遍。";
        Feed.Add(head);
        if (!CommonRuleEstablished)
        {
            var room = People.Where(p => p != Det && Get(p.Body, T.Sick) == 0).ToArray();
            foreach (var p in room) Tell(p, CommonRule, Channel.Public);
            Det.Acc[CommonRule] = Truth.True;
            CommonRuleEstablished = room.Count(p => p.Acc.GetValueOrDefault(CommonRule) == Truth.True) >= 12;
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
            Witness(p, $"{p.Name}仍把我当贼", -2, 0, [who], spread: false);
        if (acc == P.Quer)
        {
            ConfrontLog.Add("雀儿当天搬出了甲区。");
            who.Body.Get<SpatialProperty>()!.Space = Space[Place.Oath];
        }
        else ConfrontLog.Add($"{who.Name}被当成了私取之人。这一天没什么人跟{who.Name}说话。");
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
