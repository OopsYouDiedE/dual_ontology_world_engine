using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>一处清晨痕迹：键、所在地点、看到的样子（自然语言）。</summary>
public sealed record TraceItem(string Key, Place Where, string Text);

/// <summary>“若我说中，你采纳我的规则”：与某一个人的约定（§11 第 11 条）。</summary>
public sealed record Wager(int Day, P With, P Who, Place Where, int? Spec);

/// <summary>
/// §11 第 1、2、10 条：读写闭环。
/// · 夜里发生的事只以身影（身量、来去方向）、声音与痕迹到达侦探；守夜也不给姓名与动作。
/// · 每个清晨，昨夜留下的痕迹成表；侦探只读得到自己查过的那几处，其余只给地点。
/// · 任一清晨都可以对“昨夜”做信念重演：从现实快照重建（按同一份行动记录重放到昨夜，包含侦探自己白天的行动），
///   只改玩家假设的那几条“谁接受了哪条规则”，再推演一夜，与现实痕迹对照。
/// </summary>
public sealed partial class Game
{
    // ------------------------------------------------------------ action log --

    public int SlotIndex => (Day - 2) * 6 + (int)Slot;
    /// <summary>侦探每个时段的行动（含 null = 歇着），以及时段之间的“不占时段”的约定。</summary>
    public readonly List<PlayerAction?> Script = [];
    public readonly List<(int Slot, Wager Wager)> FreeScript = [];

    /// <summary>按同一份行动记录，从第 2 日晨重放到某个时段之前。确定性保证重建的就是现实。</summary>
    public static Game Rebuild(IReadOnlyList<PlayerAction?> script, IReadOnlyList<(int Slot, Wager Wager)> free, int stopBefore, GameOptions options)
    {
        var g = new Game(options);
        for (var i = 0; i < stopBefore && i < script.Count && !g.Ended; i++)
        {
            foreach (var (_, w) in free.Where(f => f.Slot == i)) g.MakeWager(w.With, w.Who, w.Where, w.Spec);
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
        var f = (t - _nightStart) / NightLength;
        return f < 0.25 ? "入夜不久" : f < 0.6 ? "夜深时" : "后半夜";
    }
    private double _nightStart;

    private static string From(Place p) => p switch
    {
        Place.DormLife => "生活翼方向", Place.DormW => "文契寝区方向", Place.DormS => "星仪寝区方向", Place.DormL => "后勤寝区方向",
        _ => Names.Of(p) + "方向"
    };

    /// <summary>守夜所见：只有身影的身量、来去方向与声音（§11 第 2 条）。</summary>
    public List<string> StakeoutReport(int day, Place at)
    {
        var lines = new List<string>();
        var events = new List<(double T, string Text)>();
        foreach (var m in Moves.Where(m => m.Day == day && m.Who != P.Det))
        {
            var build = BuildText(this[m.Who].Pro.Build);
            if (m.To == at) events.Add((m.Time, $"{NightTime(m.Time)}，一个{build}身影从{From(m.From)}过来"));
            else if (m.From == at) events.Add((m.Time, $"{NightTime(m.Time)}，一个{build}身影往{From(m.To)}去了"));
        }
        foreach (var s in Sounds.Where(s => s.Day == day))
        {
            if (s.Where == at) events.Add((s.Time, $"{NightTime(s.Time)}，{s.Text}"));
            else if (Near(s.Where, at)) events.Add((s.Time, $"{NightTime(s.Time)}，远远地从{Names.Of(s.Where)}那边传来：{s.Text}"));
        }
        foreach (var (_, text) in events.OrderBy(e => e.T)) lines.Add(text);
        if (lines.Count == 0) lines.Add("一夜无事。没有人来，也没有声音。");
        return lines;
    }

    private static bool Near(Place a, Place b) => a == Place.Hall && b is Place.Logistics or Place.ArchiveFront || b == Place.Hall && a is Place.Logistics or Place.ArchiveFront;

    // ----------------------------------------------------------- dawn traces --

    /// <summary>每个清晨的痕迹表（键 → 痕迹），按“昨夜是第几日的夜”存。</summary>
    public readonly Dictionary<int, List<TraceItem>> DawnTraces = [];
    public readonly List<string> NightLedgerNames = [];
    private int _failedAtGranary, _pickedGranary;

    private List<TraceItem> TakeTraces(int night)
    {
        var t = new List<TraceItem>();
        var shortBy = Math.Max(0, ExpectedRations() - Get(Granary, T.Ration));
        var names = NightLedgerNames.Where(n => n.StartsWith($"D{night} ")).Select(n => n[(n.IndexOf(' ') + 1)..]).ToArray();
        t.Add(new("台账", Place.Logistics, $"账面与存量差 {shortBy} 份{(names.Length > 0 ? "；夜间记账一栏有" + string.Join("、", names) + "的签名" : "；夜间记账一栏是空的")}"));
        var latch = Get(Granary, T.LatchLoose) > 0 ? "门闩松动" : Get(Granary, T.ToolMarks) > 0 ? "门闩紧固，有工具痕" : "门闩完好";
        if (_pickedGranary > 0) latch += "；锁眼边有极细的新划痕";
        if (_failedAtGranary > 0) latch += $"；仓门上有{(_failedAtGranary > 3 ? "许多" : _failedAtGranary > 1 ? "几处" : "一处")}新的推痕（有人没打开）";
        t.Add(new("门闩", Place.Logistics, latch));
        t.Add(new("碗", Place.Clinic, Get(Bowl, T.Porridge) + Get(Bowl, T.Residue) > 0 && _bowlFilledTonight ? "病床边有一碗夜里送来的粥" : "病床边没有新送来的东西"));
        foreach (var dorm in Litter.Keys)
            t.Add(new("饼屑:" + Names.Of(dorm), dorm, Get(Litter[dorm], T.Crumbs) > _crumbsBefore.GetValueOrDefault(dorm) ? "废纸篓里新添口粮包纸与饼屑" : "废纸篓里没有新东西"));
        var prints = Moves.Where(m => m.Day == night && m.To == Place.ArchiveFront).Select(m => m.Who).Distinct().Select(p => this[p].Pro.Build).ToArray();
        t.Add(new("前厅脚印", Place.ArchiveFront, prints.Length == 0 ? "前厅积灰上没有新脚印" :
            "前厅积灰上有新脚印：" + string.Join("、", prints.GroupBy(b => b).OrderBy(g => g.Key).Select(g => $"{BuildText(g.Key).TrimEnd('的')}×{g.Count()}"))));
        t.Add(new("前厅机关", Place.ArchiveFront, _seizeTried.Contains(night) ? "配重链条被人拉动过" : "三处机关没人碰过"));
        foreach (var p in Npcs.Where(p => _answered.Contains((night, p.Id))))
            t.Add(new("神情:" + p.Name, Where(p), $"{p.Name}今早神情恍惚，说梦里有人一直叫他的名字"));
        var bert = Sightings.Where(s => s.Day == night && s.Witness == P.Bertrand).Select(s => $"{Names.Of(s.At)}有个{BuildText(s.Build)}身影").Distinct().ToArray();
        t.Add(new("夜巡", Place.Hall, bert.Length == 0 ? "贝尔特朗：夜巡无异常" : "贝尔特朗：夜里望见" + string.Join("，", bert)));
        return t;
    }

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
    }

    /// <summary>侦探查过的痕迹：细看过对应的物件、清晨以后见过那个人、问过夜巡的人、或亲自守过那个地方。</summary>
    public Dictionary<string, TraceItem> KnownTraces(int night)
    {
        var known = new Dictionary<string, TraceItem>();
        if (!DawnTraces.TryGetValue(night, out var traces)) return known;
        var looked = DetLookedOn.Where(kv => kv.Key > night).SelectMany(kv => kv.Value).ToHashSet();
        var met = DetMetOn.Where(kv => kv.Key > night).SelectMany(kv => kv.Value).ToHashSet();
        foreach (var t in traces)
        {
            var ok = t.Key switch
            {
                "台账" => looked.Contains(Ledger), "门闩" => looked.Contains(Granary), "碗" => looked.Contains(Bowl) || looked.Contains(Bed),
                "前厅脚印" or "前厅机关" => looked.Contains(ArchiveDoor) || Stakeouts.Contains((night, Place.ArchiveFront)),
                "夜巡" => AskedOn.Contains((night + 1, P.Bertrand)) || AskedOn.Contains((night + 2, P.Bertrand)),
                _ when t.Key.StartsWith("饼屑:") => looked.Any(e => Litter.ContainsValue(e) && Litter.First(l => l.Value == e).Key == t.Where),
                _ when t.Key.StartsWith("神情:") => met.Contains(Names.PersonAlias[t.Key[3..]]),
                _ => false
            };
            if (Stakeouts.Contains((night, t.Where)) && t.Key is "门闩" or "台账" || Stakeouts.Contains((night, t.Where)) && t.Key.StartsWith("饼屑:")) ok = true;
            if (ok) known[t.Key] = t;
        }
        return known;
    }

    public readonly Dictionary<int, HashSet<Essence>> DetLookedOn = [];
    public readonly Dictionary<int, HashSet<P>> DetMetOn = [];
    public readonly HashSet<(int Day, P Who)> AskedOn = [];
    public readonly HashSet<(int Day, Place Where)> Stakeouts = [];

    private void DetSaw(Essence e) { if (!DetLookedOn.TryGetValue(Day, out var s)) DetLookedOn[Day] = s = []; s.Add(e); }
    private void DetMet(Person p) { if (!DetMetOn.TryGetValue(Day, out var s)) DetMetOn[Day] = s = []; s.Add(p.Id); }

    /// <summary>清晨提示：哪些地方有了新痕迹（只给地点，不给内容）。</summary>
    private void DawnHint(int night)
    {
        if (!DawnTraces.TryGetValue(night, out var traces)) return;
        var quiet = new Dictionary<string, string>
        {
            ["前厅机关"] = "三处机关没人碰过", ["前厅脚印"] = "前厅积灰上没有新脚印", ["碗"] = "病床边没有新送来的东西", ["夜巡"] = "贝尔特朗：夜巡无异常",
        };
        var changed = traces.Where(t => !(quiet.TryGetValue(t.Key, out var q) && q == t.Text) && !t.Text.Contains("没有新东西") && !(t.Key == "台账" && t.Text.StartsWith("账面与存量差 0 份；夜间记账一栏是空的")) && t.Text != "门闩完好")
            .Select(t => t.Key.StartsWith("神情:") ? "有人的神色" : Names.Of(t.Where)).Distinct().ToArray();
        if (Day > 2) Feed.Add(changed.Length == 0 ? "清晨。昨夜似乎什么也没发生。" : $"清晨。昨夜之后，这些地方有了变化：{string.Join("、", changed)}。（morning 查看你已读到的痕迹；replay 重演昨夜）");
    }

    // ---------------------------------------------------------------- replay --

    /// <summary>一条重演假设：某人对某条规则接受或不接受。</summary>
    public sealed record Hypothesis(P Who, int Spec, bool Accept);

    /// <summary>
    /// 重演昨夜：按行动记录重建到那一夜之前（现实快照，含侦探白天的行动），改写假设里那几个人心中这几条规则的接受，
    /// 推演一夜，返回清晨痕迹与重演世界（供墨影与心声）。
    /// </summary>
    public (List<TraceItem> Traces, Game World) ReplayNight(int night, IReadOnlyList<Hypothesis> hyp)
    {
        // 假设从那一天清晨起生效：规则白天就可能起作用（例如傍晚交出钥匙），所以从当天清晨重建，而不是只改夜里。
        var dayStart = (night - 2) * 6;
        var nightSlot = dayStart + (int)Slot.Night;
        var options = new GameOptions
        {
            Quiet = true, StartRations = Options.StartRations, StartOil = Options.StartOil, StartMedicine = Options.StartMedicine,
            ForceAcceptance = (g, p, s) => g.Day == night && hyp.FirstOrDefault(h => h.Who == p.Id && h.Spec == s.Id) is { } h ? (h.Accept ? Truth.True : Truth.False) : null,
        };
        var w = Rebuild(Script, FreeScript, dayStart, options);
        foreach (var h in hyp)
            if (w.Specs.FirstOrDefault(s => s.Id == h.Spec) is { } spec && w[h.Who].Acc.ContainsKey(spec))
                w.OverrideAcceptance(w[h.Who], spec, h.Accept ? Truth.True : Truth.False);
        for (var i = dayStart; i <= nightSlot && !w.Ended; i++)
        {
            foreach (var (_, wg) in FreeScript.Where(f => f.Slot == i)) w.MakeWager(wg.With, wg.Who, wg.Where, wg.Spec);
            w.RunSlot(i < Script.Count ? Script[i] : null);
        }
        w.BeginDayPublic();
        return (w.DawnTraces.GetValueOrDefault(night) ?? [], w);
    }

    public void BeginDayPublic() { if (!_dawnDone) { BeginDay(); _dawnDone = true; } }

    public List<string> CompareTraces(int night, List<TraceItem> replay)
    {
        var lines = new List<string>();
        var actual = DawnTraces.GetValueOrDefault(night) ?? [];
        var known = KnownTraces(night);
        foreach (var a in actual)
        {
            var r = replay.FirstOrDefault(x => x.Key == a.Key);
            if (!known.ContainsKey(a.Key)) { if (!a.Key.StartsWith("神情:")) lines.Add($"未查：{Names.Of(a.Where)}（{Label(a.Key)}）"); continue; }
            lines.Add(r?.Text == a.Text ? $"吻合：{Label(a.Key)}——{a.Text}" : $"冲突：{Label(a.Key)}——重演是「{r?.Text ?? "没有这回事"}」，现实是「{a.Text}」");
        }
        foreach (var r in replay.Where(r => r.Key.StartsWith("神情:") && actual.All(a => a.Key != r.Key)))
            lines.Add($"重演里{r.Text}——现实中{(DetMetOn.Where(kv => kv.Key > night).Any(kv => kv.Value.Contains(Names.PersonAlias[r.Key[3..]])) ? "他今早神色如常" : "你今天还没见过他")}");
        return lines;
    }

    private static string Label(string key) => key.StartsWith("饼屑:") ? key[3..] + "的废纸篓" : key.StartsWith("神情:") ? key[3..] + "的神情" : key;

    /// <summary>重演中被假设的人的墨影与心声（不显示数值）；其余人只给身影。</summary>
    public static List<string> Ink(Game w, int night, IReadOnlyList<Hypothesis> hyp)
    {
        var lines = new List<string>();
        foreach (var who in hyp.Select(h => h.Who).Distinct())
        {
            var p = w[who];
            foreach (var d in p.Diary.Where(d => (d.StartsWith($"D{night} 夜里") || d.StartsWith($"D{night} 傍晚")) && !d.Contains(" 接受#") && !d.Contains("欲望升至")))
            {
                var text = d[(d.IndexOf(' ', 3) + 1)..];
                var arrow = text.IndexOf(" ← ", StringComparison.Ordinal);
                var act = arrow > 0 ? text[..arrow] : text;
                var why = arrow > 0 ? text[(arrow + 3)..] : "";
                var basis = why.Contains('（') ? why[..why.IndexOf('（')] : why;
                lines.Add($"  墨影·{p.Name}：{act}{(basis.Length > 0 ? $"　心声：“{Thought(p, basis)}”" : "")}");
            }
        }
        var others = w.Moves.Where(m => m.Day == night && !hyp.Any(h => h.Who == m.Who) && m.Who != P.Det && m.To is Place.Logistics or Place.ArchiveFront or Place.Clinic)
            .Select(m => $"  墨影：一个{BuildText(w[m.Who].Pro.Build)}身影去了{Names.Of(m.To)}").Distinct();
        lines.AddRange(others);
        return lines;
    }

    private static string Thought(Person p, string basis) => basis switch
    {
        var b when b.Contains("的习惯") => "平常就这么做。",
        var b when b.StartsWith("应") => $"{b[1..^2]}请我做的。",
        "听来的规则" => "有人跟我说过这条规矩，我认。",
        var b when b.Contains("公告板") || b == "台账" => $"{b}上写着的，我认。",
        _ => basis
    };
}
