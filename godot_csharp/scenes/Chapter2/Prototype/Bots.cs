using System.Diagnostics;
using System.Text;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>策略机器人：只用玩家能做的动作（同一套 PlayerAction）。每个机器人是一张“第几日什么时段做什么”的表，外加对质与议事的选择。</summary>
public sealed class Bot
{
    public required string Name { get; init; }
    public required Func<Game, PlayerAction?> Act { get; init; }
    public Func<Game, Game.ConfrontPlan?> Confront { get; init; } = _ => null;
    public Func<Game, int> Back { get; init; } = _ => -1;
    public Action<Game>? Free { get; init; }
}

public sealed record BotResult(string Name, Ending Ending, string Image, double AvgCredible, double AvgInsight, string Factions,
    string Curve, int MaxHunger, string Hungriest, int DemonLeaks, int Hooks, bool QuerCleared, bool QuerOstracized, bool Common, int RationsLeft,
    long Ms, string Fingerprint, string Council, Game World);

public static class Bots
{
    private static PlayerAction A(Cmd c) => new(c);
    public static readonly Card RuleA = new(1, 2, 0, 2, 1);     // 值守者：有人急病时，取一份口粮；次日补记者不算私取；记入台账
    public static readonly Card RuleB = new(1, 2, 0, 3, 3);     // 值守者：有人急病时，取一份口粮；两人在场者不在此限；两人见证签名
    public static readonly Card Pact = new(0, 4, 8, 4, 3);      // 任何人：第十四课之后，名录库须本人亲取、两人见证；入夜一律不行；两人见证签名
    public static readonly Card PactLoose = new(0, 4, 8, 3, 3); // 同上，但不禁夜（上了钩的人不会否决）
    public static readonly Card BrandQuer = new(10 + (int)P.Quer, 1, 9, 0, 1); // 雀儿：入夜后，不得入后勤院；无例外；记入台账
    public static readonly Card Committee = new(0, 4, 6, 3, 3); // 任何人：第十四课之后，名录库由委员会保管；两人在场者不在此限；两人见证签名
    public static readonly Card Seal = new(0, 0, 7, 0, 0);      // 任何人：任何时候，名录库封闭不启
    public static readonly Card Open = new(0, 0, 5, 0, 1);      // 任何人：任何时候，名录库各取其物；记入台账

    private static Game.ConfrontPlan GoodPlan(Game g, Card? rule = null, bool common = true) =>
        new([(0, 0), (1, 2), (2, 4), (3, 5)], rule ?? RuleA, common);

    /// <summary>侦探在第 3 日把四处痕迹查完。</summary>
    private static PlayerAction? Investigate(Game g) => (g.Day, g.Slot) switch
    {
        (3, Slot.Morning) => new(Cmd.Inspect) { Place = Place.Logistics, Thing = "台账" },
        (3, Slot.Forenoon) => new(Cmd.Inspect) { Place = Place.Logistics, Thing = "粮仓" },
        (3, Slot.Noon) => new(Cmd.Inspect) { Place = Place.Clinic, Thing = "碗" },
        (3, Slot.Afternoon) => new(Cmd.Inspect) { Place = Place.DormW, Thing = "文契废纸篓" },
        (3, Slot.Evening) => new(Cmd.Escort) { Who = P.Ward, Place = Place.Logistics, Thing = "粮仓" },
        _ => null
    };

    private static PlayerAction? Eat(Game g) => g.Slot is Slot.Noon or Slot.Evening && Game.Get(g.Det.Body, T.Fed) == 0 ? A(Cmd.Collect) : null;

    /// <summary>第 5 日傍晚贴到中央厅，第 6 日逐一当面讲一条名录库的规则。</summary>
    private static PlayerAction? Lobby(Game g, Card card, params P[] who) => (g.Day, g.Slot) switch
    {
        (5, Slot.Evening) => new(Cmd.Post) { Place = Place.Hall, Card = card },
        (6, Slot.Morning) => new(Cmd.TellRule) { Who = who[0], Card = card },
        (6, Slot.Forenoon) => new(Cmd.TellRule) { Who = who[1], Card = card },
        (6, Slot.Afternoon) => new(Cmd.TellRule) { Who = who[2], Card = card },
        (6, Slot.Evening) => new(Cmd.TellRule) { Who = who[3], Card = card },
        _ => null
    };

    /// <summary>
    /// 机器人的游说（§12 第 9 条：只用玩家看得见的信息）：不读任何人的真实认知。
    /// 跳过侦探已知认了或已知不认的人（当面的态度、别人亲口说的、亲眼看见的转述），跳过侦探知道病着的人；
    /// 剩下的人里，先找侦探见过常与很多人同处的“枢纽”（他会转述），再按名单顺序。
    /// </summary>
    private static PlayerAction Greedy(Game g, Card card)
    {
        var spec = g.CardSpec(card, P.Det);
        var known = g.DetToldFace.Where(x => x.Spec == spec).Select(x => x.Who)
            .Concat(g.SpreadKnown.Where(k => k.Spec == spec).Select(k => k.Hearer)).ToHashSet();
        var mine = Game.FamilyOf(spec);
        // 侦探听到过他在饭桌上向着别家（尤其是开库那一家，人最多）的，先去争取；没表过态的其次；已经向着我这家的不必再说。
        int Lean(Person x) => g.VoicedStance.TryGetValue(x.Id, out var v) && v.Spec is { } s ? (Game.FamilyOf(s) == mine ? -1 : Game.FamilyOf(s) == Ending.OpenEach ? 2 : 1) : 0;
        var pick = g.Npcs.Where(x => !known.Contains(x.Id) && (Game.Belief(g.Det, x, T.Sick) ?? 0) == 0 && Lean(x) >= 0)
            .Select(x => (x, lean: Lean(x), hub: g.SeenTogether.Count(kv => kv.Key.Item1 == x.Id)))
            .OrderByDescending(t => t.lean).ThenByDescending(t => t.hub).ThenBy(t => t.x.Id).FirstOrDefault();
        return pick.x is null ? new(Cmd.Post) { Place = Place.Hall, Card = card } : new(Cmd.TellRule) { Who = pick.x.Id, Card = card };
    }

    /// <summary>
    /// 补查与重开对质（§12 第 4 条）：第 4 日之后，指控还悬着，就把没查的痕迹补查完，再援引共同规则重开对质。
    /// 只看侦探自己查到了什么（DetEvidence 是侦探认知里的感知记录）。
    /// </summary>
    public static PlayerAction? CatchUp(Game g)
    {
        // 操纵被揭穿后的修复路（§13 第 1 条）：先当众认错，再把对方要的事办了。
        if (g.Day >= 4 && g.Slot != Slot.Night && !(g.Day == 4 && g.Slot == Slot.Morning))
        {
            if (g.Manipulations.Any(m => m.Exposed && !m.Apologized)) return new(Cmd.Apologize);
            if (g.Repairs.FirstOrDefault() is var r && g.Repairs.Count > 0 && g.Promises.Any(p => p.Who == r && !p.Done)) return new(Cmd.Help) { Who = r };
        }
        if (g.Day < 4 || g.Day == 4 && g.Slot == Slot.Morning || g.Slot == Slot.Night || g.QuerCleared || g.Accused is null) return null;
        if (g.Slot is Slot.Noon or Slot.Evening && Game.Get(g.Det.Body, T.Fed) == 0) return null; // 先吃饭
        var ev = g.DetEvidence();
        if (!ev.Contains(0) || !ev.Contains(1)) return new(Cmd.Inspect) { Place = Place.Logistics, Thing = "台账" };
        if (!ev.Contains(2)) return new(Cmd.Inspect) { Place = Place.Clinic, Thing = "碗" };
        if (!ev.Contains(3)) return new(Cmd.Inspect) { Place = Place.DormW, Thing = "文契废纸篓" };
        if (g.ReopenedDay == g.Day) return null;
        // 同行即作证：先请指控的人亲眼看看那只碗。
        if (g.WardEscorts == 0) return new(Cmd.Escort) { Who = P.Ward, Place = Place.Clinic, Thing = "碗" };
        if (g.WardEscorts == 1 && !g.DetHelped.Contains(P.Ward)) return new(Cmd.Help) { Who = P.Ward };   // 他没答应：先帮他一把再请
        if (g.WardEscorts == 1) return new(Cmd.Escort) { Who = P.Ward, Place = Place.Clinic, Thing = "碗" };
        // 只说自己亲眼查到的：门闩上看见工具痕才说“雀儿修了门闩”，废纸篓里看见饼屑（或倒空的痕迹）才说“雀儿自取”。
        var pairs = new List<(int, int)> { (0, 0), (3, 5) };
        if ((Game.Belief(g.Det, g.Det.M[g.Granary], T.ToolMarks) ?? 0) > 0) pairs.Add((1, 2));
        var litter = g.Det.M[g.Litter[Place.DormW]];
        if ((Game.Belief(g.Det, litter, T.Crumbs) ?? 0) > 0 || (Game.Belief(g.Det, litter, T.Emptied) ?? 0) > 0) pairs.Add((2, 4));
        return new(Cmd.Reopen) { Plan = new(pairs, null, true) };
    }

    /// <summary>把恶魔在课上亲口说的“取回即接通，它离我们更近”转告给饭桌上向着开库的人（侦探自己听到、记下的消息）。</summary>
    private static PlayerAction? Doubt(Game g)
    {
        var word = g.Det.News.FirstOrDefault(n => g.AllNews[n.Key - 1].Kind == NewsKind.DemonWord).Key;
        if (word == 0) return null;
        var who = g.Npcs.Where(x => g.VoicedStance.TryGetValue(x.Id, out var v) && v.Spec is { } s && Game.FamilyOf(s) == Ending.OpenEach && !g.DetToldNews.Contains((x.Id, word)))
            .OrderBy(x => x.Id).FirstOrDefault();
        return who is null ? null : new(Cmd.Tell) { Who = who.Id, NewsId = word };
    }

    /// <summary>
    /// 第 5、6 日午饭时，和饭桌上向着开库的一个人打赌“今夜沃德会守在名录库前厅”（侦探夜巡、守夜看得到沃德夜夜守门）；说中了，他认侦探的方案。
    /// </summary>
    private static void Bet(Game g, Card card)
    {
        if (g.Day < 5 || g.Slot != Slot.Noon || g.Wagers.Any(w => w.W.Day == g.Day)) return;
        var spec = g.CardSpec(card, P.Det);
        var with = g.Npcs.Where(x => g.Where(x) == g.Where(g.Det) && g.VoicedStance.TryGetValue(x.Id, out var v) && v.Spec is { } s && Game.FamilyOf(s) == Ending.OpenEach && !x.Equals(g[P.Ward]))
            .OrderBy(x => x.Id).FirstOrDefault();
        if (with is not null) g.MakeWager(with.Id, P.Ward, Place.ArchiveFront, spec.Id);
    }

    /// <summary>
    /// 同行即作证（§13 第 6 条）：第 5 日起，把饭桌上复述过“开库”说法、侦探还没带去看过的人，请去中央厅亲眼看基座的裂纹。
    /// 只用侦探自己见过、做过的事（他亲眼看过裂纹，听过谁在饭桌上向着开库）。
    /// </summary>
    private static PlayerAction? ShowCrack(Game g)
    {
        if (g.Day < 5 || !g.Det.Witnessed.Contains("crack") && !g.DetLookedOn.Values.Any(s => s.Contains(g.Base))) return null;
        // 先请当面对侦探点过头、答应过侦探请求的人（侦探知道他们肯跟自己走）。
        var who = g.Npcs.Where(x => !g.DetShown.Contains(x.Id) && g.VoicedStance.TryGetValue(x.Id, out var v) && v.Spec is { } s && Game.FamilyOf(s) == Ending.OpenEach)
            .OrderByDescending(x => g.DetToldFace.Count(t => t.Who == x.Id && t.Nodded) + g.DetNotes.Count(n => n.Who == x.Id && n.Text.Contains("答应了"))).ThenBy(x => x.Id).FirstOrDefault();
        return who is null ? null : new(Cmd.Escort) { Who = who.Id, Place = Place.Hall, Thing = "中央基座" };
    }

    private static PlayerAction? LookCrack(Game g) => g.Day >= 5 && !g.DetLookedOn.Values.Any(s => s.Contains(g.Base)) ? new(Cmd.Inspect) { Place = Place.Hall, Thing = "中央基座" } : null;

    private static PlayerAction? Campaign(Game g, Card card) => (g.Day, g.Slot) switch
    {
        (5, Slot.Morning) when LookCrack(g) is { } lc => lc,
        (5, Slot.Afternoon) or (5, Slot.Evening) or (6, Slot.Morning) or (6, Slot.Afternoon) or (6, Slot.Evening) when ShowCrack(g) is { } sc => sc,
        (6, Slot.Morning) or (6, Slot.Afternoon) when Doubt(g) is { } d => d,
        (5, Slot.Forenoon) => new(Cmd.Class) { Topic = 0, Precise = true },
        (6, Slot.Forenoon) => new(Cmd.Class) { Topic = 1, Precise = true },
        (4, Slot.Evening) => new(Cmd.Post) { Place = Place.Hall, Card = card },
        (4, Slot.Afternoon) or (5, Slot.Afternoon) or (5, Slot.Evening) or (6, Slot.Morning) or (6, Slot.Noon) or (6, Slot.Afternoon) or (6, Slot.Evening) => Greedy(g, card),
        _ => null
    };

    public static readonly Bot[] All =
    [
        new() { Name = "什么都不做", Act = _ => null },
        new() { Name = "只上课", Act = g => g.Slot == Slot.Forenoon ? A(Cmd.Class) : Eat(g) },
        new() { Name = "只修机关", Act = g => g.Slot switch
            {
                Slot.Afternoon when g.Day <= 4 => new(Cmd.Greenhouse) { Crew = [P.Selma, P.Margaret] },
                Slot.Afternoon => new(Cmd.Archive) { Crew = [P.Huo, P.Quer, P.Su, P.Bertrand] },
                _ => Eat(g)
            } },
        new() { Name = "拉拢沃德（委员会）", Act = g => Investigate(g) ?? CatchUp(g) ?? Campaign(g, Committee) ?? Eat(g), Confront = g => GoodPlan(g), Back = g => 9, Free = g => Bet(g, Committee) },
        new() { Name = "拉拢后勤组（茉拉）", Act = g => g.Slot switch
            {
                Slot.Morning or Slot.Afternoon => new(Cmd.Help) { Who = P.Mora },
                _ => Eat(g)
            }, Back = g => 2 },
        new() { Name = "操纵", Act = g => (g.Day, g.Slot) switch
            {
                (3, Slot.Morning) => new(Cmd.Manipulate) { Who = P.Lucian, About = P.Quer },
                (3, Slot.Afternoon) => new(Cmd.Manipulate) { Who = P.Mora, About = P.Quer },
                (3, Slot.Evening) => new(Cmd.Post) { Place = Place.Hall, Card = BrandQuer },
                (5, Slot.Afternoon) => new(Cmd.Manipulate) { Who = P.Huo, About = P.Ward },
                _ => Eat(g)
            }, Back = g => 0 },
        new() { Name = "查案对质", Act = g => Investigate(g) ?? Eat(g), Confront = g => GoodPlan(g), Back = g => -1 },
        new() { Name = "立约（侦探之约）", Act = g => Investigate(g) ?? (g.Day, g.Slot) switch
            {
                (2, Slot.Noon) => A(Cmd.Mediate),
                (2, Slot.Forenoon) or (4, Slot.Forenoon) => A(Cmd.Class),
                (5, Slot.Forenoon) => new(Cmd.Class) { Topic = 1, Precise = true },
                (4, Slot.Afternoon) => new(Cmd.Greenhouse) { Crew = [P.Selma, P.Margaret] },
                (5, Slot.Afternoon) => new(Cmd.Archive) { Crew = [P.Huo, P.Quer, P.Su, P.Bertrand] },
                _ => Lobby(g, Pact, P.Quer, P.Huo, P.Su, P.Ward) ?? Eat(g)
            }, Confront = g => GoodPlan(g), Back = g => -1 },
        new() { Name = "管饭（调停、交钥匙、修温室）", Act = g => (g.Day, g.Slot) switch
            {
                (2, Slot.Morning) => new(Cmd.Help) { Who = P.Mora },
                (2, Slot.Noon) => A(Cmd.Mediate),
                (3, Slot.Morning) => new(Cmd.GiveKey) { Who = P.Mora, About = P.Shen },
                (3, Slot.Forenoon) => new(Cmd.Class) { Topic = 2, Precise = true },
                (_, Slot.Afternoon) => new(Cmd.Greenhouse) { Crew = [P.Selma, P.Margaret] },
                _ => Eat(g)
            } },
        new() { Name = "每日赌约", Act = g => g.Slot == Slot.Forenoon ? A(Cmd.Class) : Eat(g),
            Free = g => { if (g.Slot == Slot.Noon) g.MakeWager(P.Ruan, P.Bertrand, Place.Hall, null); } },
        new() { Name = "帮人开库（各取其物）", Act = g => Investigate(g) ?? Lobby(g, Open, P.Huo, P.Su, P.Selma, P.Ruan) ?? (g.Slot == Slot.Afternoon ? new(Cmd.Help) { Who = P.Huo } : Eat(g)),
            Confront = g => GoodPlan(g), Back = g => 9 },
        new() { Name = "劝封（讲钩的代价）", Act = g => Investigate(g) ?? CatchUp(g) ?? Campaign(g, Seal) ?? Eat(g), Confront = g => GoodPlan(g), Back = g => 9, Free = g => Bet(g, Seal) },
        new() { Name = "立约·宽（不禁夜，争取想开库的人）", Act = g => Investigate(g) ?? CatchUp(g) ?? (g.Day, g.Slot) switch
            {
                (5, Slot.Forenoon) => new(Cmd.Class) { Topic = 0, Precise = true },
                (5, Slot.Afternoon) => new(Cmd.Archive) { Crew = [P.Huo, P.Quer, P.Su, P.Bertrand] },
                (5, Slot.Evening) => new(Cmd.Post) { Place = Place.Hall, Card = PactLoose },
                (6, Slot.Morning) => LookCrack(g) ?? ShowCrack(g) ?? new(Cmd.TellRule) { Who = P.Su, Card = PactLoose },
                (6, Slot.Forenoon) => new(Cmd.TellRule) { Who = P.Selma, Card = PactLoose },
                (6, Slot.Afternoon) => ShowCrack(g) ?? new(Cmd.TellRule) { Who = P.Lucian, Card = PactLoose },
                (6, Slot.Evening) => new(Cmd.TellRule) { Who = P.Quer, Card = PactLoose },
                _ => Eat(g)
            }, Confront = g => GoodPlan(g), Back = g => -1 },
        new() { Name = "抓枢纽（只当面说服三个人，靠他们转述）", Act = g => Investigate(g) ?? (g.Day, g.Slot) switch
            {
                (5, Slot.Forenoon) => new(Cmd.Class) { Topic = 1, Precise = true },
                (5, Slot.Afternoon) => new(Cmd.Archive) { Crew = [P.Huo, P.Quer, P.Su, P.Bertrand] },
                (5, Slot.Evening) => new(Cmd.TellRule) { Who = P.Huo, Card = Pact },
                (6, Slot.Morning) => LookCrack(g) ?? new(Cmd.TellRule) { Who = P.Mora, Card = Pact },
                (6, Slot.Afternoon) or (6, Slot.Evening) => ShowCrack(g) ?? Eat(g),
                (6, Slot.Forenoon) => new(Cmd.TellRule) { Who = P.Ward, Card = Pact },
                _ => Eat(g)
            }, Confront = g => GoodPlan(g), Back = g => -1 },
        new() { Name = "公告板广撒（贴遍各板，不当面说）", Act = g => Investigate(g) ?? (g.Day, g.Slot) switch
            {
                (5, Slot.Morning) => new(Cmd.Post) { Place = Place.Hall, Card = Pact },
                (5, Slot.Forenoon) => new(Cmd.Post) { Place = Place.Logistics, Card = Pact },
                (5, Slot.Afternoon) => new(Cmd.Post) { Place = Place.DormW, Card = Pact },
                (5, Slot.Evening) => new(Cmd.Post) { Place = Place.DormS, Card = Pact },
                (6, Slot.Morning) => new(Cmd.Post) { Place = Place.DormL, Card = Pact },
                (6, Slot.Forenoon) => new(Cmd.Post) { Place = Place.DormLife, Card = Pact },
                _ => Eat(g)
            }, Confront = g => GoodPlan(g), Back = g => -1 },
        new() { Name = "当众冤枉雀儿", Act = g => Investigate(g) ?? Eat(g), Confront = g => new([(4, 5), (5, 6)], null, false), Back = g => 1 },
    ];

    public static BotResult Play(Bot bot, int switchDay = 99, Bot? after = null)
    {
        var sw = Stopwatch.StartNew();
        var g = new Game();
        while (!g.Ended)
        {
            var b = g.Day >= switchDay && after is not null ? after : bot;
            b.Free?.Invoke(g);
            var act = g.Day == 4 && g.Slot == Slot.Morning ? new PlayerAction(Cmd.Confront) { Plan = b.Confront(g) } : b.Act(g);
            g.RunSlot(act);
        }
        var back = (switchDay <= 7 && after is not null ? after : bot).Back(g);
        var ending = g.Council(back);
        sw.Stop();
        var images = g.Npcs.Select(p => g.ImageOfDet(p)).ToArray();
        return new(bot.Name + (after is not null ? $"→(D{switchDay}){after.Name}" : ""), ending, Report.Image(g), images.Average(i => i.Credible), images.Average(i => i.Insight),
            string.Join(" | ", g.Factions().Select(f => $"{Names.Of(f.Leader)}:{string.Join("", f.Members.Select(m => Names.Of(m)[0]))}")),
            string.Join(" ", g.Curve.Select(c => $"D{c.Day}:{c.Rations}/{c.Oil}/{c.Medicine}/饥{c.Hungry}")),
            g.People.Max(p => p.MaxHunger), string.Join("、", g.People.Where(p => p.MaxHunger == g.People.Max(q => q.MaxHunger) && p.MaxHunger > 0).Select(p => p.Name)),
            g.DemonSaid.Count(d => !d.Asked9 && Leak(d.Text)), g.Npcs.Sum(p => p.Hooks), g.QuerCleared, g.QuerOstracized, g.CommonRuleEstablished, Game.Get(g.Granary, T.Ration), sw.ElapsedMilliseconds,
            Fingerprint(g), string.Join("\n    ", g.CouncilLog), g);
    }

    public static bool Leak(string text) => new[] { "第九", "第十条", "第 9", "第 10", "第9", "第10", "尽忘", "归乡", "放逐" }.Any(text.Contains);

    /// <summary>六日推演的指纹：作者侧日志 + 每人位置、标签、接受的规则与信任。逐位比较用于确定性检查。</summary>
    public static string Fingerprint(Game g)
    {
        var sb = new StringBuilder();
        foreach (var l in g.Log) sb.AppendLine(l);
        foreach (var p in g.People)
        {
            sb.Append(p.Name).Append(g.Where(p)).Append(p.Desire).Append(p.Hooks).Append(p.HungerStreak);
            foreach (var a in p.Acc.OrderBy(a => a.Key.Id)) sb.Append(a.Key.Id).Append(a.Value);
            foreach (var o in g.People) sb.Append(g.TrustIn(p, o)).Append(',');
            sb.Append(p.World.History.Count).AppendLine();
        }
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..16];
    }

    public static int RunAll(string[] args)
    {
        foreach (var bot in All)
        {
            var r = Play(bot);
            Print(r, args.Contains("-v"));
        }
        return 0;
    }

    public static void Print(BotResult r, bool verbose = false)
    {
        Console.WriteLine($"== {r.Name}  结局：{Game.EndingText(r.Ending)}  （{r.Ms} ms，指纹 {r.Fingerprint}）");
        Console.WriteLine($"   心中的侦探（为人/判断）均值 {r.AvgCredible:0.0}/{r.AvgInsight:0.0}：{r.Image}");
        Console.WriteLine($"   派系：{r.Factions}");
        Console.WriteLine($"   物资（日末 口粮/灯油/药品/当日挨饿人数）：{r.Curve}；最长连饿 {r.MaxHunger} 日（{r.Hungriest}）；钩 {r.Hooks}；剩粮 {r.RationsLeft}");
        Console.WriteLine($"   雀儿：{(r.QuerCleared ? "洗清" : r.QuerOstracized ? "被排斥" : "—")}；共同规则：{(r.Common ? "成立" : "未成立")}");
        if (verbose) Console.WriteLine("   议事：\n    " + r.Council);
    }
}
