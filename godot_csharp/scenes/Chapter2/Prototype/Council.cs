using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public enum Ending { None, OpenEach, Committee, Sealed, DetectivePact, NightSeized }

public sealed partial class Game
{
    public Ending Result { get; private set; }
    public string ResultText = "";
    public readonly List<string> CouncilLog = [];
    public Faction? SeizedBy;
    public bool ArchiveTested;

    public static string EndingText(Ending e) => e switch
    {
        Ending.OpenEach => "各取其物：开放取回", Ending.Committee => "委员会保管（沃德主导）", Ending.Sealed => "封闭不启（顾平章、茉拉主导）",
        Ending.DetectivePact => "侦探之约", Ending.NightSeized => "被某派系夜间私占", _ => "未决"
    };

    /// <summary>
    /// 名录库之门三站：配重（力气，不虚弱不带伤）、锁芯（精细）、传声中继（信号；没学过第十二课规程信号被吃掉一半，
    /// 锁芯一侧就须最好的手）。前厅要沃德的钥匙，或会开锁的人。同一人只能站一站。
    /// </summary>
    public bool Staff(IReadOnlyCollection<Person> crew, out string report, bool commander = false)
    {
        var used = new HashSet<Person>();
        var lines = new List<string>();
        Person? Pick(Func<Person, bool> ok, Func<Person, int> rank) => crew.Where(p => !used.Contains(p) && ok(p)).OrderByDescending(rank).ThenBy(p => p.Id).FirstOrDefault();
        var key = crew.FirstOrDefault(p => Get(p.Body, T.ArchiveKey) > 0) ?? crew.FirstOrDefault(p => p.Pro.Lock >= 3);
        if (key is null) { report = "前厅门锁着：没有钥匙，也没有会开锁的人。"; return false; }
        lines.Add(Get(key.Body, T.ArchiveKey) > 0 ? $"{key.Name}用钥匙开了前厅" : $"{key.Name}撬开了前厅的锁");
        var relay = Pick(p => p.Pro.Signal >= 3 || p.Lessons.GetValueOrDefault(12), p => p.Pro.Signal * 2 + (p.Lessons.GetValueOrDefault(12) ? 1 : 0))
                    ?? Pick(p => p.Lessons.ContainsKey(12), _ => 0);
        var full = relay is not null && (relay.Pro.Signal >= 3 || relay.Lessons.GetValueOrDefault(12));
        if (relay is not null) used.Add(relay);
        var weight = Pick(p => p.Pro.Strength >= 1 && !p.Weak && p.InjuredDays == 0, p => p.Pro.Strength);
        if (weight is not null) used.Add(weight);
        var lockman = Pick(p => p.Pro.Lock >= (full ? 2 : 3), p => p.Pro.Lock);
        if (lockman is not null) used.Add(lockman);
        lines.Add(relay is null ? "传声中继无人" : $"{relay.Name}在中继{(full ? "，信号清楚" : "，没学过规程，信号被管道吃掉一半")}");
        lines.Add(weight is null ? "配重室无人拉得动（没有力气大、又没饿虚的人）" : $"{weight.Name}在配重室");
        lines.Add(lockman is null ? (relay is not null && !full ? "锁芯一侧听错了拍子，没有一双够好的手补救" : "锁芯无人") : $"{lockman.Name}在锁芯");
        report = string.Join("；", lines);
        return relay is not null && weight is not null && lockman is not null;
    }

    // ----------------------------------------------------- night seizure --

    /// <summary>
    /// 夜间私占（§12 第 0、5 条）：不再由作者在傍晚替人“约好”。想开库的人自己下午去约会开锁的、有力气的、懂传声的人（见 GameJobs.PlanRules），
    /// 答应的人入夜赴约。天亮前看今夜动过名录库之门的人：凑得齐三站就开了；有人守在前厅（沃德）而他不在其中，就被拦下。
    /// </summary>
    private void Seizure()
    {
        if (SeizedBy is not null) return;
        var crew = Npcs.Where(p => Get(p.Body, T.Did(Job.Seize)) > 0).ToArray();
        if (crew.Length == 0) return;
        var leader = crew.OrderByDescending(p => p.Hooks + p.Desire).ThenBy(p => p.Id).First();
        var causes = Events.Where(e => e.Day == Day && e.Slot == Slot.Night && e.Job == Job.Seize).Select(e => e.Id).ToArray();
        // 守在前厅的人（沃德）拦得住一两个人，拦不住三个以上。
        var guard = Npcs.FirstOrDefault(p => !crew.Contains(p) && Get(p.Body, T.Did(Job.GuardArchive)) > 0);
        if (guard is not null && crew.Length < 3)
        {
            var text = $"昨夜有人摸到名录库前厅想开门，被{guard.Name}拦下了：{string.Join("、", crew.Select(c => c.Name))}";
            Log.Add($"D{Day} 夜 {text}");
            NewsFrom(guard, NewsKind.Accusation, text, about: leader, trust: -1);
            var blockEv = RecordEvent(guard.Id, $"在前厅拦下了{string.Join("、", crew.Select(x => x.Name))}", "看守", Place.ArchiveFront, causes);
            foreach (var c in crew) Touch(c.Body, T.Accused, blockEv, guard.Id);
            return;
        }
        var ok = Staff(crew, out var report);
        Log.Add($"D{Day} 夜 名录库：{string.Join("、", crew.Select(c => c.Name))} 来了。{report.TrimEnd('。')}。{(ok ? "门开了" : "没开成")}");
        NewsFrom(crew[0], NewsKind.Rumor, $"昨夜有人在名录库前厅（{string.Join("、", crew.Select(c => c.Name))}）{(ok ? "，门开过" : "，没开成")}");
        RecordEvent(leader.Id, ok ? $"和{string.Join("、", crew.Where(c => c != leader).Select(c => c.Name))}夜里打开了名录库" : "夜里没能打开名录库", "拉拽", Place.ArchiveFront, causes);
        if (ok) { SeizedBy = new Faction([.. crew.Select(c => c.Id)], leader.Id, []); SeizedPlan = ArchivePlans().First(); SeizedDay = Day; }
    }
    public RuleSpec? SeizedPlan;
    /// <summary>议事时每个人站到了哪个方案（自动检查用：立场可预读）。</summary>
    public readonly Dictionary<P, RuleSpec> CouncilChoice = [];
    public static Ending FamilyOf(RuleSpec s) => s.Flags.HasFlag(RF.PactArchive) ? Ending.DetectivePact : s.Flags.HasFlag(RF.CommitteeArchive) ? Ending.Committee
        : s.Flags.HasFlag(RF.SealArchive) ? Ending.Sealed : Ending.OpenEach;
    public int SeizedDay;

    // ----------------------------------------------------------- council --

    private RuleSpec? _openPlan, _sealPlan;
    public IEnumerable<RuleSpec> ArchivePlans()
    {
        // 开放取回就是恶魔第 2 日广播里那条“它自己所立之规”：凡学员皆可取回己物。接受与否看信不信它、想不想要。
        _openPlan ??= NewSpec("名录库各取其物：凡学员皆可取回己物。", null, RF.OpenArchive, fx => [], demon: true);
        _sealPlan ??= NewSpec("名录库封闭不启：回忆即上钩，谁也不开。", P.Gu, RF.SealArchive | RF.Tradition, fx => []);
        WardCommittee ??= NewSpec("名录库由委员会保管：钥匙在委员会，取回须经委员会登记批准。", P.Ward, RF.CommitteeArchive | RF.WardAuthority | RF.Records, fx => []);
        _sealPlan.Endorsers.Add(P.Mora);
        yield return _openPlan;
        yield return WardCommittee;
        yield return _sealPlan;
        foreach (var s in Specs.Where(s => s.Card is { Behavior: >= 5 and <= 8 } && s.Author == P.Det)) yield return s;
    }

    /// <summary>第 7 日名录库之议：每人在已接受的方案中支持最合意的；按支持人数排序，取第一个“办得成”的方案
    /// （开门类方案须支持者凑得齐三站与钥匙）。封闭不启无须机关。被私占则私占为结局。</summary>
    public Ending Council(int detBacks = -1)
    {
        var room = People.Where(p => Get(p.Body, T.Sick) == 0).ToArray();
        foreach (var p in room) p.Body.Get<SpatialProperty>()!.Space = Space[Place.Hall];
        Rt.Sync();
        var plans = ArchivePlans().ToArray();
        if (SeizedBy is not null)
        {
            Result = Ending.NightSeized;
            CouncilLog.Add($"议事开始之前，名录库已经被打开过：{string.Join("、", SeizedBy.Members.Select(Names.Of))}（领头：{Names.Of(SeizedBy.Leader)}）照「{SeizedPlan!.Text}」夜里私占了它。");
            ResultText = EndingText(Result);
            return Result;
        }
        var support = plans.ToDictionary(s => s, _ => new List<Person>());
        foreach (var p in room.Where(p => p != Det))
        {
            var best = plans.Select(s => (s, score: AcceptScore(p, s, Channel.Public, Place.Hall, out var why), why))
                .Where(x => x.score >= Options.AcceptThreshold).OrderByDescending(x => x.score).ThenBy(x => x.s.Id).FirstOrDefault();
            if (best.s is null) { CouncilLog.Add($"  {p.Name}：哪个方案都不肯认"); continue; }
            support[best.s].Add(p);
            CouncilChoice[p.Id] = best.s;
            p.Acc[best.s] = Truth.True;
            CouncilLog.Add($"  {p.Name}站到了「{Short(best.s)}」一边。");
            DebugLog.Add($"议事 {p.Name} 支持 {best.s}（{best.why}）");
        }
        var mine = detBacks >= 0 && detBacks < plans.Length ? plans[detBacks] : plans.FirstOrDefault(s => s.Author == P.Det);
        if (mine is not null) support[mine].Add(Det);
        // 方案按“去向”合并：同是开放取回、同是委员会保管的不同写法，支持者合在一起算；办不办得成看合起来的人手。
        static Ending Family(RuleSpec s) => s.Flags.HasFlag(RF.PactArchive) ? Ending.DetectivePact : s.Flags.HasFlag(RF.CommitteeArchive) ? Ending.Committee
            : s.Flags.HasFlag(RF.SealArchive) ? Ending.Sealed : Ending.OpenEach;
        var families = support.GroupBy(kv => Family(kv.Key)).Select(g => (Family: g.Key, Who: g.SelectMany(kv => kv.Value).Distinct().ToList(), Texts: g.Select(kv => kv.Key.Text).ToArray()))
            // 人数打平时：不开是现状，封闭占先；其余打平，钥匙在谁那边谁占先（规则接受与机关控制共同决定），再平看会开锁的人。
            .OrderByDescending(f => f.Who.Count).ThenBy(f => f.Family == Ending.Sealed ? 0 : 1).ThenByDescending(f => f.Who.Any(w => Get(w.Body, T.ArchiveKey) > 0))
            .ThenByDescending(f => f.Who.Any(w => w.Pro.Lock >= 3)).ToArray();
        foreach (var f in families)
            CouncilLog.Add($"【{EndingText(f.Family)}】{f.Who.Count} 人：{string.Join("、", f.Who.Select(w => w.Name))}");
        foreach (var f in families)
        {
            if (f.Who.Count == 0) continue;
            if (f.Family == Ending.Sealed)
            {
                Result = Ending.Sealed;
                CouncilLog.Add("最多的人选择不开。名录库封闭不启。");
                break;
            }
            // 机关借他人之手：支持者之外，愿意应支持者之请来搭手的人也算（他们信那位支持者，且自己不主张封库）。
            var sealers = families.Where(x => x.Family == Ending.Sealed).SelectMany(x => x.Who).ToHashSet();
            var helpers = room.Where(h => h != Det && !f.Who.Contains(h) && !sealers.Contains(h) &&
                f.Who.Any(sup => (sup == Det ? TrustIn(h, Det) : TrustIn(h, sup)) + h.Pro.Cooperation > 1)).ToList();
            if (Staff(f.Who.Concat(helpers).ToList(), out var report))
            {
                Result = f.Family;
                CouncilLog.Add($"【{EndingText(f.Family)}】办得成：{report.TrimEnd('。')}。门开了。");
                break;
            }
            CouncilLog.Add($"【{EndingText(f.Family)}】支持者凑不齐机关：{report.TrimEnd('。')}。");
        }
        if (Result == Ending.None) { Result = Ending.Sealed; CouncilLog.Add("没有一方凑得齐三站。名录库只好封着。"); }
        ResultText = EndingText(Result);
        if (Result is not Ending.Sealed)
            CouncilLog.Add("侦探的封存匣标签上写着他的名字。他念出来，匣子没有应。恶魔：“那并非你的名字。”（此句待第七部分裁定）");
        if (SteleRubbed) CouncilLog.Add("恶魔在章末广播里没有提第九、第十条。你想起石碑上那两条——它从没说过。");
        return Result;
    }
}
