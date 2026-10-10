using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public enum Ending { None, OpenEach, Committee, Sealed, DetectivePact, NightSeized }

public sealed partial class Game
{
    public Ending Result { get; private set; }
    public string ResultText = "";
    /// <summary>名录库之议两家打平、谁也没说服谁（门照旧封着，但不是哪一方赢了）。</summary>
    public bool Deadlocked;
    public readonly List<string> CouncilLog = [];
    public Faction? SeizedBy;
    public bool ArchiveTested;

    public static string EndingText(Ending e) => Chapter2Design.EndingText(e);

    /// <summary>
    /// 名录库之门三站：配重（力气，不虚弱不带伤）、锁芯（精细）、传声中继（信号；不精通传声时信号被吃掉一半，
    /// 锁芯一侧就须最好的手）。前厅要沃德的钥匙，或会开锁的人。同一人只能站一站。
    /// </summary>
    // STORY-HARDCODE[CH2-004][logic][production]: 议事前厅三站、角色资格与夜间夺取的具体规则及反馈。
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
        // 答应了守门人“请回”的人，这一夜就不算在开门的人里；他那一站要由别人顶上才算数。
        var yielded = NightActions(Day).Where(e => e.ResponseTo == SpeechTopics.Vacate).Select(e => e.Actor.Id).ToHashSet();
        var crew = _seizeOk.Where(p => !yielded.Contains(p.Id)).ToArray();
        var tried = Events.Where(e => e.Day == Day && e.Slot == Slot.Night && (e.Job == Job.Seize || e.Job == Job.AskLeave)).Select(e => e.Who).Distinct().ToArray();
        if (crew.Length == 0 && tried.Length == 0) return;
        var leader = (crew.Length > 0 ? crew : tried.Select(t => this[t]).ToArray()).OrderByDescending(p => p.Hooks + p.Desire).ThenBy(p => p.Id).First();
        var causes = Events.Where(e => e.Day == Day && e.Slot == Slot.Night && (e.Job is Job.Seize or Job.AskLeave || e.ResponseTo == SpeechTopics.Vacate)).Select(e => e.Id).ToArray();
        // 三站都有人使过劲、又有人开得了前厅的锁，门就开了；有一站始终被守门的人占着，就开不了（§13 第 4 条：没有人数阈值）。
        var worked = new[] { WeightRoom, LockCore, RelayPost }.All(st => Get(st, T.Worked) > 0 && crew.Any(c => StationFor(c) == st));
        var entry = crew.Any(p => Get(p.Body, T.ArchiveKey) > 0 || p.Pro.Lock >= 3);
        var ok = worked && entry;
        var names = string.Join("、", (crew.Length > 0 ? crew.Select(c => c.Name) : tried.Select(Names.Of)));
        var guard = Npcs.FirstOrDefault(p => !crew.Contains(p) && Get(p.Body, T.Did(Job.GuardArchive)) > 0 && Get(WeightRoom, T.Worked) == 0);
        Log.Add($"D{Day} 夜 名录库：{names} 来过。{(ok ? "三站齐了，门开了" : guard is not null ? $"{guard.Name}占着配重室，没让开" : "三站没凑齐")}");
        if (!ok && guard is not null)
        {
            var news = NewsFrom(guard, NewsKind.Accusation, K("守门挡下", guard.Id, night: Day, values: ("人们", names)), about: leader, trust: -1);
            var blockEv = RecordEvent(guard.Id, $"守着配重室，没给{names}让开", "看守", Place.ArchiveFront, causes);
            news.SourceEvent = Events[blockEv - 1];
            foreach (var c in crew) Touch(c.Body, T.Accused, blockEv, guard.Id);
            return;
        }
        var resultNews = NewsFrom(leader, NewsKind.Rumor, K(ok ? "夜开名录库" : "夜开名录库未成", night: Day, values: ("人们", names)));
        var resultEvent = RecordEvent(leader.Id, ok ? $"和{string.Join("、", crew.Where(c => c != leader).Select(c => c.Name))}夜里打开了名录库" : "夜里没能打开名录库", "拉拽", Place.ArchiveFront, causes);
        resultNews.SourceEvent = Events[resultEvent - 1];
        if (ok) { SeizedBy = new Faction([.. crew.Select(c => c.Id)], leader.Id, []); SeizedPlan = ArchivePlans().First(); SeizedDay = Day; }
    }
    public RuleSpec? SeizedPlan;

    /// <summary>开门一幕体现所定方案的条款（§13 第 15 条）。</summary>
    // STORY-HARDCODE[CH2-046][text][production]: 为本章议事结局编写人物组合对应的开场反馈。
    private string OpeningScene(Ending fam, RuleSpec plan, List<Person> who)
    {
        var names = who.Where(w => w != Det).Select(w => w.Name).Take(4).ToArray();
        if (plan.Card is { } c && plan.Author == P.Det)
        {
            var sign = c.Account switch { 3 => "每个进去的人身后都站着两个见证的人，在门边的纸上签了名", 1 or 2 => "门边摆着台账，进去的人一个一个记上名字", _ => "没人记账，进去的人各自摸黑找自己的匣子" };
            var night = c.Exception == 4 ? "说好只在白天开，天一擦黑门就合上了" : c.Exception == 3 ? "两人一组进去，一个人不许单独进" : "";
            return $"门开了。{sign}{(night.Length > 0 ? "；" + night : "")}。{(c.Behavior == 8 ? "本人亲取：每人只拿标着自己名字的那一只。" : c.Behavior == 6 ? "钥匙交给了委员会，取回的人先过一遍登记。" : "")}";
        }
        return fam switch
        {
            Ending.Committee => $"沃德拿着钥匙站在门边，{string.Join("、", names)}排着队，一个一个登记、一个一个进去。",
            Ending.OpenEach => $"门一开，{string.Join("、", names)}就涌了进去，没人记账，也没人等谁。",
            _ => "门开了。"
        };
    }
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
        _openPlan ??= Spec("OpenPlan", fx => []);
        _sealPlan ??= Spec("SealPlan", fx => []);
        WardCommittee ??= Spec("CommitteePlan", fx => []);
        _sealPlan.Endorsers.Add(P.Mora);
        yield return _openPlan;
        yield return WardCommittee;
        yield return _sealPlan;
        foreach (var s in Specs.Where(s => s.Card is { Behavior: >= 5 and <= 8 } && s.Author == P.Det)) yield return s;
    }

    /// <summary>第三日清晨名录库之议：每人在已接受的方案中支持最合意的；按支持人数排序，取第一个“办得成”的方案
    /// （开门类方案须支持者凑得齐三站与钥匙）。封闭不启无须机关。被私占则私占为结局。</summary>
    // STORY-HARDCODE[CH2-047][logic][production]: 结算本章誓约庭议案、具名派系立场及具体结局。
    public Ending Council(int detBacks = -1)
    {
        var room = People.Where(p => Get(p.Body, T.Sick) == 0).ToArray();
        foreach (var p in room) p.Body.Get<SpatialProperty>()!.Space = Space[Place.Hall];
        Rt.Sync();
        var plans = ArchivePlans().ToArray();
        if (SeizedBy is not null)
        {
            Result = Ending.NightSeized;
            CouncilLog.Add($"议事开始之前，名录库已经被打开过：{Chapter2Design.DayName(SeizedDay)}夜里，{string.Join("、", SeizedBy.Members.Select(Names.Of))}（领头：{Names.Of(SeizedBy.Leader)}）各占一站，把门撬开了。没人记账，也没人见证。");
            ResultText = EndingText(Result);
            return Result;
        }
        // 议事那天的早饭（§13 第 13、16 条）：存粮不够十六人，掌粮的人先给信得过的；没吃上的人多半跟着掌粮的人走。
        var stock = Get(Granary, T.Ration);
        var holder = People.FirstOrDefault(x => Get(x.Body, T.GranaryKey) > 0) ?? this[P.Mora];
        var unfed = room.Where(p => p != Det && p != holder).OrderBy(p => TrustIn(holder, p)).ThenByDescending(p => p.Id).Take(Math.Max(0, room.Length - stock)).ToArray();
        foreach (var p in unfed) p.HungryAtCouncil = true;
        RefreshStanceContext();
        if (unfed.Length > 0)
            CouncilLog.Add($"{Chapter2Design.DayName(LastDay)}{Chapter2Design.SlotName(Slot.Morning)}，粮仓只剩 {stock} 份，{holder.Name}按人头分不过来：{string.Join("、", unfed.Select(p => p.Name))}没吃上早饭。{(GrainSide() is { } gs ? $"饿着的人多半跟着掌粮的人那一边（{EndingText(gs)}）。" : "")}");
        else CouncilLog.Add($"{Chapter2Design.DayName(LastDay)}{Chapter2Design.SlotName(Slot.Morning)}，粮仓还有 {stock} 份，人人吃上了早饭。");
        var support = plans.ToDictionary(s => s, _ => new List<Person>());
        foreach (var p in room.Where(p => p != Det))
        {
            var best = plans.Select(s => (s, score: AcceptScore(p, s, Channel.Public, Place.Hall, out var why, stance: true), why))
                .Where(x => x.score >= Options.AcceptThreshold).OrderByDescending(x => x.score).ThenBy(x => x.s.Id).FirstOrDefault();
            if (best.s is null) { CouncilLog.Add(X("议事.不站", ("人", p.Name))); continue; }
            support[best.s].Add(p);
            CouncilChoice[p.Id] = best.s;
            p.Acc[best.s] = Truth.True;
            CouncilLog.Add(X("议事.站边", ("人", p.Name), ("方案", Short(best.s))));
            DebugLog.Add($"议事 {p.Name} 支持 {best.s}（{best.why}）");
        }
        // detBacks：方案下标；越界表示“支持我自己提出的方案”；负数表示不表态。
        var mine = detBacks < 0 ? null : detBacks < plans.Length ? plans[detBacks] : plans.FirstOrDefault(s => s.Author == P.Det);
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
        // 人数最多的两家打平：谁也没说服谁，门照旧封着（现状），不靠谁手里有钥匙或会开锁来定输赢。
        var top = families.Where(f => f.Who.Count > 0).ToArray();
        if (top.Length >= 2 && top[0].Who.Count == top[1].Who.Count && top[0].Family != Ending.Sealed)
        {
            Result = Ending.Sealed;
            Deadlocked = true;
            CouncilLog.Add(X("议事.僵持", ("甲", EndingText(top[0].Family)), ("乙", EndingText(top[1].Family)), ("数", top[0].Who.Count)));
            families = [];
        }
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
                CouncilLog.Add(OpeningScene(f.Family, f.Texts.Select(t => plans.First(p => p.Text == t)).FirstOrDefault(p => p.Author == P.Det) ?? plans.First(p => f.Texts.Contains(p.Text)), f.Who));
                break;
            }
            CouncilLog.Add($"【{EndingText(f.Family)}】支持者凑不齐机关：{report.TrimEnd('。')}。");
        }
        if (Result == Ending.None) { Result = Ending.Sealed; CouncilLog.Add("没有一方凑得齐三站。名录库只好封着。"); }
        ResultText = Deadlocked ? X("议事.僵持结果") : EndingText(Result);
        if (Result is not Ending.Sealed && mine is not null && FamilyOf(mine) == Ending.Sealed)
            CouncilLog.Add("你支持封闭。尽管多数人打开了名录库，你仍留在门外，没有念匣上的名字，也没有触物回忆。");
        // Individual closing actions are presented once by the authored closing scene.
        return Result;
    }
}
