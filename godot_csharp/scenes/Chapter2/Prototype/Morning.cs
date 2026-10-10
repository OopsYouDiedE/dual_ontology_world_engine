using System.Collections.Immutable;
using System.Text.Json.Serialization;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>一处清晨痕迹：键、所在地点、细看到的样子；Glance 是路过时一眼看得出的样子（看不出则为 null）。</summary>
public enum TraceKind { Ledger, Latch, Bowl, Litter, Footprints, Mechanism, Expression, Patrol }
public enum TraceComparisonStatus { Match, Conflict, Unread, Additional }
public sealed record TraceComparison(TraceComparisonStatus Status, string Text);
public sealed record TraceFacts
{
    public bool HasChange { get; init; }
    [JsonIgnore] public ImmutableDictionary<Tag, int> Values { get; init; } = ImmutableDictionary<Tag, int>.Empty;
    public ImmutableArray<(Place At, Build Build)> Figures { get; init; } = [];
    [JsonIgnore] public ImmutableArray<Person> Subjects { get; init; } = [];
    public int StayCategory { get; init; }
    public bool Same(TraceFacts? other) => other is not null && HasChange == other.HasChange && StayCategory == other.StayCategory
        && Values.Count == other.Values.Count && Values.All(v => other.Values.TryGetValue(v.Key, out var n) && v.Value == n)
        && Figures.SequenceEqual(other.Figures) && Subjects.Select(p => p.Id).SequenceEqual(other.Subjects.Select(p => p.Id));
}
public sealed record TraceItem(string Key, Place Where, string Text, string? Glance = null)
{
    public TraceKind Kind { get; init; }
    [JsonIgnore] public Essence? Object { get; init; }
    public TraceFacts Facts { get; init; } = new();
    public TraceFacts? GlanceFacts { get; init; }
    public bool IsGlance { get; init; }
    public bool HasChange => Facts.HasChange;
}

/// <summary>“若我说中，你采纳我的规则”：与某一个人的约定（§11 第 11 条）。</summary>
public sealed record Wager(int Day, P With, P Who, Place Where, int? Spec);

/// <summary>
/// 读写闭环（§11 第 1、2、10 条；§12 第 1、5、6、7 条）。
/// · 夜里发生的事只以身影（身量、来去方向）、声音与痕迹到达侦探；守夜只给最显著的几条。
/// · 每个清晨，昨夜留下的痕迹成表；侦探细看过的读得到全貌，路过的只记下明显的变化，其余只给地点。
/// · 任一清晨都可以对某一夜做信念重演：从现实快照重建，只改玩家假设的“某人认/不认某条规矩”“某人昨夜在/不在某处”。
/// </summary>
public sealed partial class Game
{
    // ------------------------------------------------------------ action log --

    public int SlotIndex => (Day - 2) * 6 + (int)Slot;
    /// <summary>侦探每个时段的行动（含 null = 歇着），以及时段之间的“不占时段”的约定。</summary>
    public readonly List<PlayerAction?> Script = [];
    public readonly List<(int Slot, Wager Wager)> FreeScript = [];

    /// <summary>按同一份行动记录，从第 2 日晨重放到某个时段之前。确定性保证重建的就是现实。</summary>
    public static Game Rebuild(IReadOnlyList<PlayerAction?> script, IReadOnlyList<(int Slot, Wager Wager)> free, int stopBefore, GameOptions options,
        IReadOnlyList<(int Slot, Place Place)>? places = null, IReadOnlyList<(int Slot, string Choice, string Option)>? choices = null,
        IReadOnlyList<FreeEvent>? events = null)
    {
        var g = new Game(options);
        for (var i = 0; i < stopBefore && i < script.Count && !g.Ended; i++)
        {
            g.RestoreFree(i, places, choices, events);
            if (events is null) foreach (var (_, w) in free.Where(f => f.Slot == i)) g.MakeWager(w.With, w.Who, w.Where, w.Spec);
            g.RunSlot(script[i]);
        }
        return g;
    }

    // ---------------------------------------------------------- night senses --

    public sealed record NightMove(int Day, double Time, P Who, Place From, Place To);
    public sealed record NightSound(int Day, double Time, Place Where, string Text);
    public readonly List<NightMove> Moves = [];
    public readonly List<NightSound> Sounds = [];
    private readonly Dictionary<Person, Place> _lastPlace = [];

    private void TrackMoves()
    {
        if (Slot != Slot.Night) return;
        foreach (var p in People)
        {
            var now = Where(p);
            if (_lastPlace.TryGetValue(p, out var before) && before != now) Moves.Add(new(Day, Now, p.Id, before, now));
            _lastPlace[p] = now;
        }
    }

    private void Sound(Place where, string text) { if (Slot == Slot.Night) Sounds.Add(new(Day, Now, where, text)); }

    private string NightTime(double t)
    {
        return X("表达.夜时." + NightPeriod(t));
    }
    private int NightPeriod(double t) => (t - _nightStart) / NightLength is var f && f < 0.25 ? 0 : f < 0.6 ? 1 : 2;
    private double _nightStart;

    private static string From(Place p)
    {
        var selected = DualOntologyWorldEngine.Story.ResponseCatalog.Select(Chapter2Design.Section("expression"), "direction", name => name == "place" ? p.ToString() : null);
        return selected is null ? X("表达.方向", ("地", Names.Of(p))) : X(selected.Text);
    }

    private static readonly string[] CnNum = ["零", "一", "两", "三", "四", "五", "六", "七", "八", "九", "十"];
    public static string Cn(int n) => n >= 0 && n < CnNum.Length ? CnNum[n] : n.ToString();

    /// <summary>侦探在意的地方：他写过或认过这一领域的规矩，或者白天细看过那里。</summary>
    private bool DetCares(Place place)
    {
        var domains = Specs.Where(s => s.Author == P.Det || Det.Acc.GetValueOrDefault(s) == Truth.True).Select(s => s.Domain).ToHashSet();
        if (place is Place.Logistics or Place.Clinic && domains.Contains(Dom.Rations)) return true;
        if (place == Place.ArchiveFront && domains.Contains(Dom.Archive)) return true;
        return DetLookedOn.Values.Any(set => set.Any(e => PlaceOf.GetValueOrDefault(e) == place));
    }

    /// <summary>
    /// 守夜所见（§12 第 1 条）：只有身影（身量、来去方向）与声音，并按注意力筛选——
    /// 离得近、声音大、与侦探在意之事相关的优先，只给 3—5 条，其余合成一句。
    /// </summary>
    public List<string> StakeoutReport(int day, Place at)
    {
        var cares = DetCares(at);
        var events = new List<(double T, string Template, Build? Build, Place Place, string Sound, int Score, bool Figure)>();
        foreach (var m in Moves.Where(m => m.Day == day && m.Who != P.Det))
        {
            var build = this[m.Who].Pro.Build;
            if (m.To == at) events.Add((m.Time, "来", build, m.From, "", 2 + (cares ? 1 : 0), true));
            else if (m.From == at) events.Add((m.Time, "去", build, m.To, "", 1 + (cares ? 1 : 0), true));
        }
        // 从守夜处望得见的别处（中央厅望得见后勤院与名录库前厅的出入）：只有身量，远处的身影分数低于走到身边的。
        foreach (var s in Sightings.Where(s => s.Day == day && s.Witness == P.Det && s.From == at))
            events.Add((s.Time, "远见", s.Build, s.At, "", 1 + (DetCares(s.At) ? 1 : 0), true));
        foreach (var s in Sounds.Where(s => s.Day == day))
        {
            if (s.Where == at) events.Add((s.Time, "近声", null, s.Where, s.Text, 3 + (cares ? 2 : 0), false));
            else if (Near(s.Where, at)) events.Add((s.Time, "远声", null, s.Where, s.Text, 1 + (DetCares(s.Where) ? 1 : 0), false));
        }
        if (events.Count == 0) return [X("表达.无事")];
        // 同一时候、同一方向的几道身影合成一句（“两个中等身量的身影……”）。
        var merged = events.GroupBy(e => (e.Template, Period: NightPeriod(e.T), e.Build, e.Place, e.Sound)).Select(g =>
        {
            var first = g.First();
            var text = X("表达." + first.Template, ("时", NightTime(first.T)), ("数", Cn(g.Count())),
                ("身量", first.Build is { } build ? BuildText(build) : ""), ("地", first.Template is "来" or "去" ? From(first.Place) : Names.Of(first.Place)), ("声", first.Sound));
            return (first.T, Text: text, Score: first.Score + (g.Count() > 1 ? 1 : 0), first.Figure, N: g.Count());
        }).ToList();
        var keep = merged.OrderByDescending(e => e.Score).ThenBy(e => e.T).Take(5).ToHashSet();
        var lines = merged.Where(keep.Contains).OrderBy(e => e.T).Select(e => e.Text).ToList();
        var restFigures = merged.Where(e => !keep.Contains(e) && e.Figure).Sum(e => e.N);
        var restSounds = merged.Count(e => !keep.Contains(e) && !e.Figure);
        if (restFigures > 0) lines.Add(X("表达.数道身影", ("数", Cn(restFigures))));
        if (restSounds > 0) lines.Add(X("表达.其他动静"));
        return lines;
    }


    private static bool Near(Place a, Place b) => a == Place.Hall && b is Place.Logistics or Place.ArchiveFront || b == Place.Hall && a is Place.Logistics or Place.ArchiveFront;

    // ----------------------------------------------------------- dawn traces --

    /// <summary>每个清晨的痕迹表（键 → 痕迹），按“昨夜是第几日的夜”存。</summary>
    public readonly Dictionary<int, List<TraceItem>> DawnTraces = [];
    public readonly List<string> NightLedgerNames = [];
    private int _failedAtGranary, _pickedGranary;
    private readonly Dictionary<int, (int Prints, double Stay)> _frontStats = [];
    /// <summary>每一夜名录库前厅的脚印数与停留总时长（自动检查用：私占可预读）。</summary>
    public IReadOnlyDictionary<int, (int Prints, double Stay)> FrontStats => _frontStats;
    private readonly HashSet<(int, Place)> _tidied = [];

    // STORY-HARDCODE[CH2-030][logic][production]: 清晨生成台账、门闩、病碗、废纸篓与目击等具体案发痕迹。
    private List<TraceItem> TakeTraces(int night)
    {
        var t = new List<TraceItem>();
        var shortBy = Math.Max(0, ExpectedRations() - Get(Granary, T.Ration));
        var signers = NightLedgerRecords.Where(n => n.Night == night).Select(n => n.Actor).ToArray();
        var names = signers.Select(p => p.Name).ToArray();
        t.Add(new("台账", Place.Logistics, $"账面与存量差 {shortBy} 份{(names.Length > 0 ? "；夜间记账一栏有" + string.Join("、", names) + "的签名" : "；夜间记账一栏是空的")}") {
            Kind = TraceKind.Ledger, Object = Ledger, Facts = new() { HasChange = shortBy > 0 || names.Length > 0,
                Values = ImmutableDictionary<Tag, int>.Empty.Add(T.Unrecorded, shortBy), Subjects = [..signers] } });
        var latch = Get(Granary, T.LatchLoose) > 0 ? "门闩松动" : Get(Granary, T.ToolMarks) > 0 ? "门闩紧固，有工具痕" : "门闩完好";
        var glance = new List<string>();
        if (_pickedGranary > 0) { latch += "；锁眼边有极细的新划痕"; }
        if (_failedAtGranary > 0) { var push = $"仓门上有{(_failedAtGranary > 3 ? "许多" : _failedAtGranary > 1 ? "几处" : "一处")}新的推痕（有人没打开）"; latch += "；" + push; glance.Add(push); }
        t.Add(new("门闩", Place.Logistics, latch, glance.Count > 0 ? string.Join("；", glance) : null) {
            Kind = TraceKind.Latch, Object = Granary, Facts = new() { HasChange = Get(Granary, T.LatchLoose) > 0 || Get(Granary, T.ToolMarks) > 0 || _pickedGranary > 0 || _failedAtGranary > 0,
                Values = ImmutableDictionary<Tag, int>.Empty.Add(T.LatchLoose, Get(Granary, T.LatchLoose) > 0 ? 1 : 0).Add(T.ToolMarks, Get(Granary, T.LatchLoose) == 0 && Get(Granary, T.ToolMarks) > 0 ? 1 : 0)
                    .Add(new("picks"), _pickedGranary > 0 ? 1 : 0).Add(new("attempts"), AttemptAppearance(_failedAtGranary)) },
            GlanceFacts = new() { HasChange = _failedAtGranary > 0, Values = ImmutableDictionary<Tag, int>.Empty.Add(new("attempts"), AttemptAppearance(_failedAtGranary)) } });
        var bowl = Get(Bowl, T.Porridge) + Get(Bowl, T.Residue) > 0 && _bowlFilledTonight ? "病床边有一碗夜里送来的粥" : "病床边没有新送来的东西";
        var filled = Get(Bowl, T.Porridge) + Get(Bowl, T.Residue) > 0 && _bowlFilledTonight;
        var bowlFacts = new TraceFacts { HasChange = filled, Values = ImmutableDictionary<Tag, int>.Empty.Add(T.Porridge, filled ? 1 : 0) };
        t.Add(new("碗", Place.Clinic, bowl, bowl) { Kind = TraceKind.Bowl, Object = Bowl, Facts = bowlFacts, GlanceFacts = bowlFacts });
        foreach (var dorm in Litter.Keys)
        {
            var text = _tidied.Contains((night, dorm)) ? "废纸篓被人倒空过，篓底还粘着几粒饼屑"
                : Get(Litter[dorm], T.Crumbs) > _crumbsBefore.GetValueOrDefault(dorm) ? "废纸篓里新添口粮包纸与饼屑" : "废纸篓里没有新东西";
            var tidied = _tidied.Contains((night, dorm));
            var added = Get(Litter[dorm], T.Crumbs) > _crumbsBefore.GetValueOrDefault(dorm);
            t.Add(new("饼屑:" + Names.Of(dorm), dorm, text) { Kind = TraceKind.Litter, Object = Litter[dorm],
                Facts = new() { HasChange = tidied || added, Values = ImmutableDictionary<Tag, int>.Empty.Add(T.Crumbs, added ? 1 : 0).Add(new("emptied"), tidied ? 1 : 0) } });
        }
        // 名录库前厅：脚印（身量）与停留（脚印叠得多密），并与前一夜比较（§12 第 5 条：私占可预读）。
        var arrivals = Moves.Where(m => m.Day == night && m.To == Place.ArchiveFront && m.Who != P.Det).ToList();
        var stay = 0.0;
        foreach (var a in arrivals)
        {
            var leave = Moves.Where(m => m.Day == night && m.Who == a.Who && m.From == Place.ArchiveFront && m.Time > a.Time).Select(m => m.Time).DefaultIfEmpty(_nightStart + NightLength).Min();
            stay += leave - a.Time;
        }
        var prints = arrivals.Select(m => m.Who).Distinct().Select(p => this[p].Pro.Build).ToArray();
        _frontStats[night] = (prints.Length, stay);
        string front;
        if (prints.Length == 0) front = X("物件.前厅无脚印").TrimEnd('。');
        else
        {
            front = X("物件.前厅脚印", ("各色", string.Join("、", prints.GroupBy(b => b).OrderBy(g => g.Key)
                .Select(g => X("物件.前厅脚印一种", ("数", Cn(g.Count())), ("身形", BuildText(g.Key).TrimEnd('的'))))))).TrimEnd('。');
            front += "\n" + X(stay < 4 ? "物件.前厅浅" : stay < 10 ? "物件.前厅一阵" : "物件.前厅很久").TrimEnd('。');
            if (_frontStats.TryGetValue(night - 1, out var prev) && prev.Prints > 0)
                front += "——" + X(prints.Length > prev.Prints || stay > prev.Stay * 1.3 ? "物件.前厅比前夜多" : prints.Length < prev.Prints && stay < prev.Stay ? "物件.前厅比前夜少" : "物件.前厅和前夜差不多").TrimEnd('。');
        }
        var density = prints.Length == 0 ? 0 : stay < 4 ? 1 : stay < 10 ? 2 : 3;
        var relative = _frontStats.TryGetValue(night - 1, out var previous) && previous.Prints > 0 && prints.Length > 0
            ? prints.Length > previous.Prints || stay > previous.Stay * 1.3 ? 1 : prints.Length < previous.Prints && stay < previous.Stay ? -1 : 0 : 0;
        var footprintFacts = new TraceFacts { HasChange = prints.Length > 0, StayCategory = density,
            Values = ImmutableDictionary<Tag, int>.Empty.Add(new("relative-density"), relative), Figures = [..prints.Order().Select(b => (Place.ArchiveFront, b))] };
        t.Add(new("前厅脚印", Place.ArchiveFront, front, front) { Kind = TraceKind.Footprints, Object = ArchiveDoor, Facts = footprintFacts, GlanceFacts = footprintFacts });
        var machine = _seizeTried.Contains(night) ? "配重链条被人拉动过" : "三处机关没人碰过";
        var machineFacts = new TraceFacts { HasChange = _seizeTried.Contains(night) };
        t.Add(new("前厅机关", Place.ArchiveFront, machine, machine) { Kind = TraceKind.Mechanism, Object = ArchiveDoor, Facts = machineFacts, GlanceFacts = machineFacts });
        foreach (var p in Npcs.Where(p => _answered.Contains((night, p.Id))))
            t.Add(new("神情:" + p.Name, Where(p), $"{p.Name}今早神情恍惚，说梦里有人一直叫{p.Pro.Pronoun}的名字") {
                Kind = TraceKind.Expression, Object = p.Body, Facts = new() { HasChange = true, Subjects = [p] } });
        var bert = Sightings.Where(s => s.Day == night && s.Witness == P.Bertrand).Select(s => $"{Names.Of(s.At)}有个{BuildText(s.Build)}身影").Distinct().ToArray();
        t.Add(new("夜巡", Place.Hall, bert.Length == 0 ? "贝尔特朗：夜巡无异常" : "贝尔特朗：夜里望见" + string.Join("，", bert)) {
            Kind = TraceKind.Patrol, Facts = new() { HasChange = bert.Length > 0,
                Figures = [..Sightings.Where(s => s.Day == night && s.Witness == P.Bertrand).Select(s => (s.At, s.Build)).Distinct()] } });
        return t;
    }

    private static int AttemptAppearance(int attempts) => attempts > 3 ? 3 : attempts > 1 ? 2 : attempts;

    private readonly Dictionary<Place, int> _crumbsBefore = [];
    private bool _bowlFilledTonight;
    private readonly HashSet<int> _seizeTried = [];
    private readonly HashSet<(int, P)> _answered = [];

    private void NightBegins()
    {
        _nightStart = Now;
        _failedAtGranary = 0; _pickedGranary = 0; _bowlFilledTonight = false;
        foreach (var (dorm, litter) in Litter) _crumbsBefore[dorm] = Get(litter, T.Crumbs);
        foreach (var p in People) _lastPlace[p] = Where(p);
        foreach (var st in new[] { WeightRoom, LockCore, RelayPost }) { Set(st, T.Occupied, 0); Set(st, T.Worked, 0); }
        foreach (var p in People) { Set(p.Body, T.Guarding, 0); Set(p.Body, T.Prying, 0); }
        _seizeOk.Clear();
    }

    /// <summary>这处痕迹侦探是否细看过（读得到全貌）。</summary>
    // STORY-HARDCODE[CH2-049][logic][production]: 指定本案各类物证达到何种组合才可读全。
    private bool FullyRead(int night, TraceItem t)
    {
        var looked = DetLookedOn.Where(kv => kv.Key > night).SelectMany(kv => kv.Value).ToHashSet();
        var met = DetMetOn.Where(kv => kv.Key > night).SelectMany(kv => kv.Value).ToHashSet();
        var ok = t.Kind switch
        {
            TraceKind.Ledger or TraceKind.Latch or TraceKind.Litter => t.Object is { } obj && looked.Contains(obj),
            TraceKind.Bowl => looked.Contains(Bowl) || looked.Contains(Bed),
            TraceKind.Footprints or TraceKind.Mechanism => looked.Contains(ArchiveDoor) || Stakeouts.Contains((night, Place.ArchiveFront)),
            TraceKind.Patrol => AskedOn.Contains((night + 1, P.Bertrand)) || AskedOn.Contains((night + 2, P.Bertrand)) || Stakeouts.Contains((night, Place.Hall)),
            TraceKind.Expression => t.Facts.Subjects.Any(p => met.Contains(p.Id)),
            _ => false
        };
        if (Stakeouts.Contains((night, t.Where)) && t.Kind is TraceKind.Latch or TraceKind.Ledger or TraceKind.Litter) ok = true;
        return ok;
    }

    /// <summary>
    /// 侦探读到的痕迹（§12 第 7 条）：细看过那一处（同一区域的痕迹一并细看）、守过那个地方、问过夜巡的人、见过那个人——读到全貌；
    /// 只是之后路过那里——只记下一眼看得出的变化。
    /// </summary>
    public Dictionary<string, TraceItem> KnownTraces(int night)
    {
        var known = new Dictionary<string, TraceItem>();
        if (!DawnTraces.TryGetValue(night, out var traces)) return known;
        var passed = DetPassedOn.Where(kv => kv.Key > night).SelectMany(kv => kv.Value).ToHashSet();
        foreach (var t in traces)
        {
            if (FullyRead(night, t)) known[t.Key] = t;
            else if (t.Glance is { } g && passed.Contains(t.Where)) known[t.Key] = t with { Text = g, Facts = t.GlanceFacts ?? new(), IsGlance = true };
        }
        return known;
    }

    public readonly Dictionary<int, HashSet<Essence>> DetLookedOn = [];
    public readonly Dictionary<int, HashSet<P>> DetMetOn = [];
    public readonly Dictionary<int, HashSet<Place>> DetPassedOn = [];
    public readonly HashSet<(int Day, P Who)> AskedOn = [];
    public readonly HashSet<(int Day, Place Where)> Stakeouts = [];

    private void DetSaw(Essence e) { if (!DetLookedOn.TryGetValue(Day, out var s)) DetLookedOn[Day] = s = []; s.Add(e); }
    private void DetMet(Person p) { if (!DetMetOn.TryGetValue(Day, out var s)) DetMetOn[Day] = s = []; s.Add(p.Id); }
    private void DetPassed(Place place) { if (Slot == Slot.Night) return; if (!DetPassedOn.TryGetValue(Day, out var s)) DetPassedOn[Day] = s = []; s.Add(place); }

    /// <summary>清晨提示：哪些地方有了新痕迹（只给地点，不给内容）；以及昨天你经由亲见或他人亲口得知的规矩传播。</summary>
    private void DawnHint(int night)
    {
        if (!DawnTraces.TryGetValue(night, out var traces)) return;
        var moved = traces.Where(Changed).ToArray();
        var places = moved.Where(t => t.Kind != TraceKind.Expression).Select(t => Names.Of(t.Where)).Distinct().ToArray();
        var faces = moved.Any(t => t.Kind == TraceKind.Expression);
        if (Day > FirstDay) Feed.Add(places.Length == 0 ? X(faces ? "清晨.只有神色" : "清晨.无事")
            : X(faces ? "清晨.有变化带神色" : "清晨.有变化", ("地点", JoinNames(places))));
        if (Day is 99)
        {
            // 第 7 日的短缺要在第 5—6 日清晨读得到（§13 第 8、13 条）：存量、每日耗用、温室状态。
            var stock = Get(Granary, T.Ration);
            var perDay = Curve.Count >= 2 ? Math.Max(1, Curve[^2].Rations - Curve[^1].Rations) : 13;
            var days = stock / perDay;
            Feed.Add($"配给台前有人数了数：存粮还剩 {stock} 份，照这几天的吃法，{(days >= 7 - Day + 1 ? "撑得到议事那天" : $"撑不到第 7 日的早饭")}。温室{(GreenhouseRepaired ? "已经修好，每天能添几份" : "还没修好")}。");
        }
        // 早饭前人们说起的昨天的事（§13 第 2 条；多日交互审核 M11）：侦探整天私下说话，清晨也读得到别人自发做的事。
        // 说的人就是做事的人自己（或亲眼看见的人，事件即记在他名下），而且他得信得过侦探才肯说；转述带名字。
        var heard = Events.Where(e => e.Day == night && !e.Fixed && e.Who != P.Det && !e.SeenByDet && e.Job is { } j && MorningWorthy.Contains(j)
                && TrustIn(this[e.Who], Det) >= 1)
            .OrderByDescending(e => e.Slot == Slot.Night ? 1 : 0).ThenBy(e => e.Id).GroupBy(e => e.Who).Select(g => g.First()).Take(2).ToArray();
        if (heard.Length > 0)
        {
            Feed.Add("早饭前，" + string.Join("；", heard.Select(e => $"{Names.Of(e.Who)}跟你说起{(e.Slot == Slot.Night ? "昨夜" : "昨天" + Names.Of(e.Slot))}的事：{Names.Of(e.Who)}{e.Text}")) + "。");
            foreach (var e in heard) { e.SeenByDet = true; MorningHeard.Add(e.Id); }
        }
        var learned = SpreadKnown.Where(k => k.Day == night).GroupBy(k => k.Spec).ToArray();
        if (learned.Length > 0) Feed.Add("昨天你得知的规矩传播：" + string.Join("；", learned.Select(g => $"{LabelOf(g.Key)}　" + string.Join("、", g.Select(k => k.Text)))) + "。");
    }

    private static readonly HashSet<Job> MorningWorthy = [Job.Tear, Job.PostRules, Job.Accuse, Job.Defend, Job.Share, Job.Exchange, Job.Teach, Job.AskOpen, Job.AskLeave,
        Job.GuardArchive, Job.Watch, Job.Tidy, Job.Treat, Job.Retreat, Job.Report, Job.Sample, Job.Needle, Job.Sketch, Job.Follow, Job.Avoid, Job.RepairLatch, Job.Greenhouse, Job.Relay];
    /// <summary>侦探经清晨闲谈读到的事件（计量用）。</summary>
    public readonly HashSet<int> MorningHeard = [];

    private static bool Changed(TraceItem t) => t.HasChange;

    // ---------------------------------------------------------------- replay --

    /// <summary>一条重演假设：某人对某条规矩认或不认。</summary>
    public sealed record Hypothesis(P Who, int Spec, bool Accept);
    /// <summary>一条重演假设：某人那一夜在或不在某处（§12 第 6 条）。</summary>
    public sealed record Presence(P Who, Place Where, bool There);

    /// <summary>
    /// 重演某一夜：按行动记录重建到那一天清晨（现实快照，含侦探白天的行动），改写假设里那几个人心中这几条规矩的接受、
    /// 或令他那一夜去/不去某处，推演当天与当夜，返回清晨痕迹与重演世界（供墨影与心声）。
    /// </summary>
    public (List<TraceItem> Traces, Game World) ReplayNight(int night, IReadOnlyList<Hypothesis> hyp, IReadOnlyList<Presence>? pres = null)
    {
        var presence = pres ?? [];
        var dayStart = (night - 2) * 6;
        var nightSlot = dayStart + (int)Slot.Night;
        var options = Options with
        {
            Quiet = true, StartRations = Options.StartRations, StartOil = Options.StartOil, StartMedicine = Options.StartMedicine, Inherit = InheritedSnapshot ?? Options.Inherit, RestoreNight = night == CaseDay - 1 ? CaseNightSnapshot ?? Options.RestoreNight : null,
            ForceAcceptance = (g, p, s) => g.Day == night && hyp.FirstOrDefault(h => h.Who == p.Id && h.Spec == s.Id) is { } h ? (h.Accept ? Truth.True : Truth.False) : null,
            BeforeNight = g => { if (g.Day == night) foreach (var pr in presence) g.ForcePresence(g[pr.Who], pr.Where, pr.There); },
        };
        var w = Rebuild(Script, FreeScript, dayStart, options, PlaceScript, StoryChoices, FreeEvents);
        foreach (var h in hyp)
            if (w.Specs.FirstOrDefault(s => s.Id == h.Spec) is { } spec && w[h.Who].Acc.ContainsKey(spec))
                w.OverrideAcceptance(w[h.Who], spec, h.Accept ? Truth.True : Truth.False);
        for (var i = dayStart; i <= nightSlot && !w.Ended; i++)
        {
            w.RestoreFree(i, PlaceScript, StoryChoices, FreeEvents);
            w.RunSlot(i < Script.Count ? Script[i] : null);
        }
        w.BeginDayPublic();
        return (w.DawnTraces.GetValueOrDefault(night) ?? [], w);
    }

    /// <summary>重演假设“他那一夜在/不在某处”：写进他心里一条只在重演里存在的规则（去那里看一眼 / 不往那里去）。</summary>
    public void ForcePresence(Person p, Place place, bool there)
    {
        // “在”不强迫他去（强迫会改变当夜的先后，留下现实里没有的痕迹）：只作为检验——重演里他若没去那里，这组假设就自相矛盾。
        if (there) return;
        var fx = FxOf(p);
        Rule rule = there
            ? new(Fx.All(fx.At(Slot.Night), fx.NotDone(Job.Wander)), RuleMode.Prefer,
                Fx.Seq(fx.Go(place), fx.Look(Space[place]), Fx.Wait(3, fx.Look(Space[place])), Fx.MeChange(T.Did(Job.Wander), 1)), 9, Truth.True)
            : new(fx.At(Slot.Night), RuleMode.Forbid, fx.Go(place), 9, Truth.True);
        var holder = p.World.Projections.FirstOrDefault(x => x.Kind == ProjectionKind.Concept && x.Name == "重演假设");
        Observation.Observe(p.Soul, new ObservationInput("重演假设", ProjectionKind.Concept)
        { Target = holder, Source = ObservationSource.Inference, Existence = Truth.True, Time = Now, Rules = [.. holder?.Rules ?? [], rule] });
    }

    public void BeginDayPublic() { if (!_dawnDone) { BeginDay(); _dawnDone = true; } }

    /// <summary>与现实对照：只比你读到的；细看过的比全貌，路过的只比一眼看得出的。</summary>
    public List<string> CompareTraces(int night, List<TraceItem> replay)
        => CompareTraceResults(night, replay).Select(r => r.Text).ToList();

    public List<TraceComparison> CompareTraceResults(int night, List<TraceItem> replay)
    {
        var lines = new List<TraceComparison>();
        var actual = DawnTraces.GetValueOrDefault(night) ?? [];
        var known = KnownTraces(night);
        foreach (var a in actual)
        {
            var r = replay.FirstOrDefault(x => TraceIdentity(x, a));
            if (!known.TryGetValue(a.Key, out var k)) { if (a.Kind != TraceKind.Expression && Changed(a)) lines.Add(new(TraceComparisonStatus.Unread, $"未查：{Names.Of(a.Where)}（{Label(a.Key)}）")); continue; }
            var full = !k.IsGlance;
            var mine = full ? r?.Text : r?.Glance ?? (r is null ? null : "看不出什么变化");
            var same = (full ? r?.Facts : r?.GlanceFacts)?.Same(k.Facts) is true;
            lines.Add(new(same ? TraceComparisonStatus.Match : TraceComparisonStatus.Conflict,
                same ? $"吻合：{Label(a.Key)}——{k.Text}" : $"冲突：{Label(a.Key)}——重演是「{mine ?? "没有这回事"}」，现实是「{k.Text}」"));
        }
        foreach (var r in replay.Where(r => r.Kind == TraceKind.Expression && actual.All(a => !TraceIdentity(a, r))))
            lines.Add(new(TraceComparisonStatus.Additional, $"重演里{r.Text}——现实中{(DetMetOn.Where(kv => kv.Key > night).Any(kv => r.Facts.Subjects.Any(p => kv.Value.Contains(p.Id))) ? "他今早神色如常" : "你今天还没见过他")}"));
        return lines;
    }

    private static bool TraceIdentity(TraceItem a, TraceItem b) => a.Kind == b.Kind && (a.Kind == TraceKind.Expression || a.Where == b.Where)
        && (a.Kind != TraceKind.Expression || a.Facts.Subjects.Select(p => p.Id).SequenceEqual(b.Facts.Subjects.Select(p => p.Id)));

    public Place[] UnreadTracePlaces(int night)
    {
        var known = KnownTraces(night);
        return (DawnTraces.GetValueOrDefault(night) ?? []).Where(t => !known.ContainsKey(t.Key) && t.Kind != TraceKind.Expression && Changed(t))
            .Select(t => t.Where).Distinct().ToArray();
    }

    private static string Label(string key) => key.StartsWith("饼屑:") ? key[3..] + "的废纸篓" : key.StartsWith("神情:") ? key[3..] + "的神情" : key;

    /// <summary>重演中被假设的人的墨影与心声（不显示数值）；其余人只给身影。</summary>
    /// <summary>
    /// 重演中被假设的人的墨影：只给身影（身量），不换成人名（§13 第 11 条）；心声只在重演与你细看过的痕迹全部吻合之后才给，作为确认。
    /// 动作里的失败原因一律不露出来，只说“没做成”。
    /// </summary>
    public sealed record InkFrame(string Text, string Place, string Figure);

    public static List<string> Ink(Game w, int night, IEnumerable<P> who, bool confirmed = false)
        => InkFrames(w, night, who, confirmed).Select(frame => frame.Text).ToList();

    public static List<InkFrame> InkFrames(Game w, int night, IEnumerable<P> who, bool confirmed = false)
    {
        var lines = new List<InkFrame>();
        var names = who.Distinct().ToArray();
        foreach (var id in names)
        {
            var p = w[id];
            var figure = $"一个{BuildText(p.Pro.Build).TrimEnd('的')}的身影";
            foreach (var d in w.ActionRecords.Where(d => d.Actor == p && d.Day == night && d.Slot == Slot.Night && d.Job is not (Job.Sleep or Job.Rest)))
            {
                var act = d.Succeeded ? d.Text : "没做成：" + d.Text;
                foreach (var n in Enum.GetValues<P>()) act = act.Replace(Names.Of(n), "某人");
                var basis = d.Basis?.Name ?? "";
                lines.Add(new($"  墨影（{figure}）：{act}{(confirmed && basis.Length > 0 ? $"　心声：“{Thought(basis)}”" : "")}", d.Location.ToString(), figure));
            }
        }
        var others = w.Moves.Where(m => m.Day == night && !names.Contains(m.Who) && m.Who != P.Det && m.To is Place.Logistics or Place.ArchiveFront or Place.Clinic)
            .Select(m => new InkFrame($"  墨影：一个{BuildText(w[m.Who].Pro.Build)}身影去了{Names.Of(m.To)}", m.To.ToString(), $"一个{BuildText(w[m.Who].Pro.Build)}身影")).DistinctBy(frame => frame.Text);
        lines.AddRange(others);
        return lines;
    }


    private static string Thought(string basis) => basis switch
    {
        var b when b.Contains("的习惯") => "平常就这么做。",
        var b when b.StartsWith("应") => "有人请我做的。",
        "听来的规则" => "有人跟我说过这条规矩，我认。",
        "重演假设" => "（你假设他这么做。）",
        var b when b.Contains("公告板") || b == "台账" => $"{b}上写着的，我认。",
        _ => basis
    };

    // ------------------------------------------------- 重演变量（通用化）--

    /// <summary>一项可供重演的假设及其说法。</summary>
    public sealed record ReplayOption(string Text, Hypothesis? Rule, Presence? Where);

    /// <summary>
    /// 重演变量由两类组合生成（§12 第 6 条）：
    /// “侦探知道存在的规矩 × 他有理由怀疑认了它的人”与“某人那一夜在不在某处（那一夜有痕迹、且身量对得上的人）”。
    /// 案件变形时同样可用；不为任何一个案子单独写变量。
    /// </summary>
    public List<ReplayOption> ReplayMenu(int night, bool preview = false)
    {
        var labels = preview ? new Dictionary<RuleSpec, string>(_labels) : _labels;
        var opts = new List<ReplayOption>();
        var cands = ReplayCandidates(night);
        foreach (var spec in DetKnownSpecs().Where(s => s.Card is null || s.Card.Behavior is 0 or 2 or 9 || s.Domain != Dom.General).Reverse().Take(6))
            foreach (var who in DetSuspects(spec).Where(cands.Contains).Take(4))
            {
                opts.Add(new($"{Names.Of(who)} 认 {LabelOf(spec, labels)}「{Short(spec)}」", new(who, spec.Id, true), null));
                opts.Add(new($"{Names.Of(who)} 不认 {LabelOf(spec, labels)}「{Short(spec)}」", new(who, spec.Id, false), null));
            }
        var known = KnownTraces(night);
        foreach (var place in known.Values.Where(t => Changed(t) && t.Kind != TraceKind.Expression).Select(t => t.Where).Distinct())
        {
            var builds = BuildsSeenAt(night, place);
            // 先列侦探有理由联想到这里的人：听人提起过他与此处、白天在此处见过他、他认了与此处相关的规矩（侦探所知）。
            int Relevance(Person p) =>
                (ClaimsOf(Det).Any(c => (c.Subject == p.Id || c.Object == p.Id) && c.Where == place) ? 4 : 0)
                + (DetSeenAt.GetValueOrDefault(place)?.Contains(p.Id) == true ? 2 : 0)
                + (SpreadKnown.Any(k => k.Hearer == p.Id && k.Accepted) || DetToldFace.Any(x => x.Who == p.Id && x.Nodded) ? 1 : 0);
            foreach (var p in Npcs.Where(p => cands.Contains(p.Id) && (builds.Count == 0 || builds.Contains(p.Pro.Build))).OrderByDescending(Relevance).ThenBy(p => p.Id).Take(5))
            {
                opts.Add(new($"{p.Name} 昨夜在 {Names.Of(place)}", null, new(p.Id, place, true)));
                opts.Add(new($"{p.Name} 昨夜不在 {Names.Of(place)}", null, new(p.Id, place, false)));
            }
        }
        return opts;
    }

    /// <summary>
    /// 那一夜可以被假设的人（§13 第 11 条）：身量与你读到的身影、脚印相符，或者名字出现在你读到的痕迹里（台账签名）；
    /// 你知道那夜病着躺在诊疗所的人排除。你什么都没读到，就没有人可以假设。
    /// </summary>
    public HashSet<P> ReplayCandidates(int night)
    {
        var known = KnownTraces(night);
        var builds = new HashSet<Build>();
        foreach (var place in Enum.GetValues<Place>()) builds.UnionWith(BuildsSeenAt(night, place));
        foreach (var t in known.Values) builds.UnionWith(t.Facts.Figures.Select(f => f.Build));
        var set = Npcs.Where(p => builds.Contains(p.Pro.Build) && (Belief(Det, p, T.Sick) ?? 0) == 0).Select(p => p.Id).ToHashSet();
        foreach (var t in known.Values) foreach (var p in t.Facts.Subjects) if (p != Det) set.Add(p.Id);
        if (DetSketchSeen.Contains(night)) foreach (var s in Sightings.Where(s => s.Day == night && s.Witness == P.Moran)) set.Add(s.Truth);
        return set;
    }

    /// <summary>侦探读到的那一夜某处出现过的身量（守夜、夜巡证言、前厅脚印）。</summary>
    public HashSet<Build> BuildsSeenAt(int night, Place place)
    {
        var set = new HashSet<Build>();
        if (Stakeouts.Contains((night, place)))
            foreach (var m in Moves.Where(m => m.Day == night && (m.To == place || m.From == place) && m.Who != P.Det)) set.Add(this[m.Who].Pro.Build);
        foreach (var s in Sightings.Where(s => s.Day == night && s.At == place && (s.Witness == P.Det || AskedOn.Contains((night + 1, s.Witness)) || AskedOn.Contains((night + 2, s.Witness))))) set.Add(s.Build);
        foreach (var trace in KnownTraces(night).Values)
            foreach (var figure in trace.Facts.Figures.Where(f => f.At == place)) set.Add(figure.Build);
        return set;
    }

    // ------------------------------------------------- 规矩的称呼与传播 --

    private static readonly string[] Stems = ["甲", "乙", "丙", "丁", "戊", "己", "庚", "辛", "壬", "癸", "子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥"];
    private readonly Dictionary<RuleSpec, string> _labels = [];

    /// <summary>侦探给规矩起的称呼：按他第一次知道它的先后，甲、乙、丙……（不是内部编号）。</summary>
    public string LabelOf(RuleSpec s) => LabelOf(s, _labels);

    public IEnumerable<(string Label, RuleSpec Spec)> PreviewKnownRules()
    {
        var labels = new Dictionary<RuleSpec, string>(_labels);
        return DetKnownSpecs().Select(s => (LabelOf(s, labels), s)).ToArray();
    }

    private static string LabelOf(RuleSpec s, Dictionary<RuleSpec, string> labels)
    {
        if (labels.TryGetValue(s, out var l)) return l;
        l = labels.Count < Stems.Length ? Stems[labels.Count] : $"第{labels.Count + 1}条";
        labels[s] = l;
        return l;
    }

    public RuleSpec? SpecByLabel(string label) => _labels.FirstOrDefault(kv => kv.Value == label).Key;

    /// <summary>侦探知道存在的规矩：自己写的、读过的板上的、听人说起的。</summary>
    public IEnumerable<RuleSpec> DetKnownSpecs() => Specs.Where(s => s != RuanLedger && !s.ByDemon && (s.Author == P.Det && (s != CommonRule || Det.Acc.ContainsKey(s)) || Det.Acc.ContainsKey(s) || DetHeard.Contains(s) || SpreadKnown.Any(k => k.Spec == s)))
        .Concat(Specs.Where(s => s.ByDemon && Det.Acc.ContainsKey(s) && s != Warning2)).Distinct();

    /// <summary>侦探有理由怀疑认了某条规矩的人：他当面讲给谁、谁亲口说起、他亲见谁转述给谁、规矩点名的人或值守者、贴出时在板前的人。</summary>
    public IEnumerable<P> DetSuspects(RuleSpec s)
    {
        var list = new List<P>();
        void Add(P p) { if (p != P.Det && p != s.Author && !list.Contains(p)) list.Add(p); }
        foreach (var k in SpreadKnown.Where(k => k.Spec == s)) { Add(k.Hearer); if (k.Teller is { } t) Add(t); }
        foreach (var p in DetToldFace.Where(x => x.Spec == s).Select(x => x.Who)) Add(p);
        if (s.Card is { } c)
        {
            if (c.Subject >= 10) Add((P)(c.Subject - 10));
            if (c.Subject == 1) foreach (var p in Enum.GetValues<P>().Where(Cast.OnDuty)) Add(p);
            if (c.Subject is >= 2 and <= 5) foreach (var p in Enum.GetValues<P>().Where(p => p != P.Det && Cast.Of(p).Group == (Group)(c.Subject - 2))) Add(p);
        }
        foreach (var place in s.PostedAt) foreach (var p in DetSeenAt.GetValueOrDefault(place) ?? []) Add(p);
        return list;
    }

    /// <summary>侦探在场听见别人转述过的规矩（及时进入“规矩”列表）。</summary>
    public readonly HashSet<RuleSpec> DetHeard = [];

    /// <summary>规矩的来历与是否通过（玩家看的说法）。</summary>
    public string SourceOf(RuleSpec s)
    {
        if (s.ByDemon) return "铜口的广播";
        if (s.Author != P.Det) return Names.Of(s.Author!.Value) + "立";
        if (s == CommonRule) return CommonRuleEstablished ? "你提议的共同规则（已成立）" : "你提议过的共同规则（没成立）";
        var nods = DetToldFace.Count(x => x.Spec == s && x.Nodded) + SpreadKnown.Count(k => k.Spec == s && k.Accepted);
        return s == CaseRule ? $"你在对质中当众提出（你知道认了的：{nods} 人）" : $"你写的（你知道认了的：{nods} 人）";
    }

    /// <summary>侦探当面讲过规矩的人（与对方的当场态度）。</summary>
    public readonly List<(RuleSpec Spec, P Who, bool Nodded, int Day)> DetToldFace = [];
    /// <summary>侦探见过谁在某处（板前读规矩的可能人选）。</summary>
    public readonly Dictionary<Place, HashSet<P>> DetSeenAt = [];

    /// <summary>侦探经由亲见或他人亲口得知的一次规矩传播。</summary>
    public sealed record Spread(int Day, RuleSpec Spec, P? Teller, P Hearer, bool Accepted, string How, string Text);
    public readonly List<Spread> SpreadKnown = [];

    /// <summary>还没对侦探说出口的“我从谁那里听说了你的规矩”。</summary>
    private readonly List<(P Who, RuleSpec Spec, P? Teller, Place? Board, bool Accepted, int Day)> _toDisclose = [];

    private void QueueDisclosure(Person hearer, RuleSpec spec, Person? teller, Place? board, bool accepted)
    {
        if (spec.Author != P.Det || hearer == Det) return;
        if (_toDisclose.Any(d => d.Who == hearer.Id && d.Spec == spec) || SpreadKnown.Any(k => k.Hearer == hearer.Id && k.Spec == spec)) return;
        _toDisclose.Add((hearer.Id, spec, teller?.Id, board, accepted, Day));
    }

    /// <summary>侦探亲眼看见一次转述（他在场）。同一人同一条规矩讲给几个人，合成一句。</summary>
    private void DetSawRelay(Person teller, IReadOnlyList<(Person Hearer, Truth V)> heard, RuleSpec spec)
    {
        foreach (var (hearer, v) in heard)
        {
            _toDisclose.RemoveAll(d => d.Who == hearer.Id && d.Spec == spec);
            SpreadKnown.Add(new(Day, spec, teller.Id, hearer.Id, v == Truth.True, "亲见", $"{teller.Name}→{hearer.Name}（{(v == Truth.True ? "认了" : "没认")}）"));
            NoteFor(hearer.Id, "规矩", $"{(v == Truth.True ? "认了" : "没认")}{LabelOf(spec)}「{Short(spec)}」（你看见{teller.Name}讲给他听）");
        }
        var yes = heard.Where(h => h.V == Truth.True).Select(h => h.Hearer.Name).ToArray();
        var no = heard.Where(h => h.V != Truth.True).Select(h => h.Hearer.Name).ToArray();
        Feed.Add($"你看见{teller.Name}把你那条{LabelOf(spec)}「{Short(spec)}」讲给{string.Join("、", heard.Select(h => h.Hearer.Name))}听。" +
            (yes.Length > 0 ? $"{string.Join("、", yes)}点了头。" : "") + (no.Length > 0 ? $"{string.Join("、", no)}没认。" : ""));
    }

    /// <summary>见到侦探的人说出自己从哪里听来了他的规矩（§12 第 2 条）。每个时段最多两人开口，先是侦探正对着说话的那个人。</summary>
    private void Disclose(P? first)
    {
        var here = Where(Det);
        var present = People.Where(p => p != Det && Where(p) == here && !p.Asleep).Select(p => p.Id).ToHashSet();
        var due = _toDisclose.Where(d => present.Contains(d.Who)).OrderByDescending(d => d.Who == first).ThenBy(d => d.Day).Take(2).ToArray();
        foreach (var d in due)
        {
            _toDisclose.Remove(d);
            var p = this[d.Who];
            var src = d.Teller is { } t ? $"{Names.Of(t)}跟我说过你那条" : $"我在{Names.Of(d.Board!.Value)}的板上看到你那条";
            var stance = d.Accepted ? Voice(p, d.Spec, Truth.True) : p.Vetoed.Contains(d.Spec) ? Voice(p, d.Spec, Truth.False) : "“我没认。”";
            Feed.Add($"{p.Name}：“{src}——「{Short(d.Spec)}」。”{stance}（你记作{LabelOf(d.Spec)}）");
            var text = d.Teller is { } t2 ? $"{Names.Of(t2)}→{p.Name}（{(d.Accepted ? "认了" : "没认")}，他亲口说的）" : $"{p.Name}读了{Names.Of(d.Board!.Value)}的板（{(d.Accepted ? "认了" : "没认")}，他亲口说的）";
            SpreadKnown.Add(new(Day, d.Spec, d.Teller, d.Who, d.Accepted, "亲口说", text));
            NoteFor(d.Who, "规矩", $"{(d.Accepted ? "认了" : "没认")}{LabelOf(d.Spec)}「{Short(d.Spec)}」（{(d.Teller is { } t3 ? "听" + Names.Of(t3) + "说的" : "读板")}）");
        }
    }
}
