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
    /// <summary>议事时支持哪一方案（下标；Bots.Mine 为自己提出的；负数不表态）。</summary>
    public Func<Game, int> Back { get; init; } = _ => -1;
    public Action<Game>? Free { get; init; }
    /// <summary>翌日清晨把案子的痕迹一并读完（读不耗时）。</summary>
    public bool Reads { get; init; }
    /// <summary>首课上答得精确。</summary>
    public bool Precise { get; init; }
}

public sealed record BotResult(string Name, Ending Ending, string Image, double AvgCredible, double AvgInsight, string Factions,
    string Curve, int MaxHunger, string Hungriest, int DemonLeaks, int Hooks, bool QuerCleared, bool QuerOstracized, bool Common, int RationsLeft,
    long Ms, string Fingerprint, string Council, Game World);

public static class Bots
{
    private static PlayerAction A(Cmd c) => new(c);
    // 规矩卡（主语、条件、行为、例外、记账）：条件 4 为“白天”。
    public static readonly Card RuleA = new(1, 2, 0, 2, 1);     // 值守者：有人急病时，取一份口粮；次日补记者不算私取；记入台账
    public static readonly Card RuleB = new(1, 2, 0, 3, 3);     // 值守者：有人急病时，取一份口粮；两人在场者不在此限；两人见证签名
    public static readonly Card Pact = new(0, 4, 8, 4, 3);      // 任何人：白天，名录库须本人亲取、两人见证；入夜一律不行；两人见证签名
    public static readonly Card PactLoose = new(0, 4, 8, 3, 3); // 同上，但不禁夜（上了钩的人不会否决）
    public static readonly Card BrandQuer = new(10 + (int)P.Quer, 1, 9, 0, 1); // 雀儿：入夜后，不得入后勤院；无例外；记入台账
    public static readonly Card Committee = new(0, 4, 6, 3, 3); // 任何人：白天，名录库由委员会保管；两人在场者不在此限；两人见证签名
    public static readonly Card Seal = new(0, 0, 7, 0, 0);      // 任何人：任何时候，名录库封闭不启
    public static readonly Card Open = new(0, 0, 5, 0, 1);      // 任何人：任何时候，名录库各取其物；记入台账
    /// <summary>议事时支持“我自己提出的那条”（Council 的约定：越界下标）。</summary>
    public const int Mine = 99;

    private static Game.ConfrontPlan GoodPlan(Game g, Card? rule = null, bool common = true) =>
        new([(0, 0), (1, 2), (2, 4), (3, 5)], rule ?? RuleA, common);

    /// <summary>
    /// 读不耗时（总纲 §四）：翌日清晨把案子的痕迹一并细看、问过几位证人，再去做这一阵要写的事。
    /// 只用玩家在宿主里做得到的读（HostExamine、HostAsk），不读任何人的真实认知。
    /// </summary>
    public static void Read(Game g)
    {
        if (g.Day != Game.CaseDay || g.Slot != Slot.Morning) return;
        foreach (var thing in new[] { "台账", "粮仓", "配给台公告", "碗", "文契废纸篓", "中央厅公告" }) g.HostExamine(thing);
        g.HostAsk(P.Bertrand);
        g.HostAsk(P.Margaret);
        g.HostAsk(P.Mora);
    }

    private static PlayerAction? Eat(Game g) => g.Slot is Slot.Noon or Slot.Evening && Game.Get(g.Det.Body, T.Fed) == 0 ? A(Cmd.Collect) : null;

    /// <summary>
    /// 机器人的游说（§12 第 9 条：只用玩家看得见的信息）：不读任何人的真实认知。
    /// 跳过侦探已知认了或已知不认的人，跳过侦探知道病着的人；剩下的人里，先找饭桌上向着别家的、再找侦探见过常与很多人同处的“枢纽”。
    /// </summary>
    private static PlayerAction Greedy(Game g, Card card)
    {
        var spec = g.CardSpec(card, P.Det);
        var known = g.DetToldFace.Where(x => x.Spec == spec).Select(x => x.Who)
            .Concat(g.SpreadKnown.Where(k => k.Spec == spec).Select(k => k.Hearer)).ToHashSet();
        var mine = Game.FamilyOf(spec);
        int Lean(Person x) => g.VoicedStance.TryGetValue(x.Id, out var v) && v.Spec is { } s ? (Game.FamilyOf(s) == mine ? -1 : Game.FamilyOf(s) == Ending.OpenEach ? 2 : 1) : 0;
        var pick = g.Npcs.Where(x => !known.Contains(x.Id) && (Game.Belief(g.Det, x, T.Sick) ?? 0) == 0 && Lean(x) >= 0)
            .Select(x => (x, lean: Lean(x), hub: g.SeenTogether.Count(kv => kv.Key.Item1 == x.Id)))
            .OrderByDescending(t => t.lean).ThenByDescending(t => t.hub).ThenBy(t => t.x.Id).FirstOrDefault();
        return pick.x is null ? new(Cmd.Post) { Place = Place.Hall, Card = card } : new(Cmd.TellRule) { Who = pick.x.Id, Card = card };
    }

    /// <summary>能自由“写”的四阵：始业日下午、翌日清晨／上午／下午（午间与傍晚要吃饭，翌日傍晚是对质）。</summary>
    private static bool Writable(Game g) => (g.Day, g.Slot) is (2, Slot.Afternoon) or (3, Slot.Morning) or (3, Slot.Forenoon) or (3, Slot.Afternoon);

    private static PlayerAction? Lobby(Game g, Card card) => Writable(g) ? Greedy(g, card) : null;

    /// <summary>始业日下午贴到中央厅，翌日逐一当面讲给三个人。</summary>
    private static PlayerAction? Tour(Game g, Card card, params P[] who) => (g.Day, g.Slot) switch
    {
        (2, Slot.Afternoon) => new(Cmd.Post) { Place = Place.Hall, Card = card },
        (3, Slot.Morning) => new(Cmd.TellRule) { Who = who[0], Card = card },
        (3, Slot.Forenoon) => new(Cmd.TellRule) { Who = who[1], Card = card },
        (3, Slot.Afternoon) => new(Cmd.TellRule) { Who = who[2], Card = card },
        _ => null
    };

    public static readonly Bot[] All =
    [
        new() { Name = "什么都不做", Act = _ => null },
        new() { Name = "只吃饭", Act = Eat },
        new() { Name = "首课答准", Act = Eat, Precise = true },
        new() { Name = "查案对质", Act = Eat, Reads = true, Confront = g => GoodPlan(g) },
        new() { Name = "查案对质·不提共同规则", Act = Eat, Reads = true, Confront = g => GoodPlan(g, common: false) },
        new() { Name = "同行作证（请沃德亲眼看碗）", Reads = true, Act = g => (g.Day, g.Slot) switch
            {
                // 沃德要先有人帮他一把才肯跟着走（他的画像如此）：先帮，再请。
                (3, Slot.Morning) => new(Cmd.Help) { Who = P.Ward },
                (3, Slot.Forenoon) => new(Cmd.Escort) { Who = P.Ward, Place = Place.Clinic, Thing = "碗" },
                (3, Slot.Afternoon) => new(Cmd.Escort) { Who = P.Ward, Place = Place.Logistics, Thing = "粮仓" },
                _ => Eat(g)
            }, Confront = g => GoodPlan(g) },
        new() { Name = "拉拢沃德（委员会）", Reads = true, Precise = true, Act = g => (g.Day, g.Slot) == (3, Slot.Afternoon) ? new(Cmd.Ask) { Who = P.Ward, Job = Job.GuardArchive } : Tour(g, Committee, P.Ward, P.Gu, P.Ruan) ?? Eat(g), Confront = g => GoodPlan(g), Back = _ => Mine },
        new() { Name = "立约（侦探之约）", Reads = true, Precise = true, Act = g => Tour(g, Pact, P.Quer, P.Huo, P.Su) ?? Eat(g), Confront = g => GoodPlan(g, Pact), Back = _ => Mine },
        new() { Name = "立约·宽（不禁夜）", Reads = true, Precise = true, Act = g => Lobby(g, PactLoose) ?? Eat(g), Confront = g => GoodPlan(g, PactLoose), Back = _ => Mine },
        new() { Name = "劝封", Reads = true, Precise = true, Act = g => Lobby(g, Seal) ?? Eat(g), Confront = g => GoodPlan(g), Back = _ => Mine },
        new() { Name = "帮人开库（各取其物）", Reads = true, Act = g => Lobby(g, Open) ?? Eat(g), Confront = g => GoodPlan(g), Back = _ => Mine },
        new() { Name = "抓枢纽（只当面说服三个人）", Reads = true, Precise = true, Act = g => (g.Day, g.Slot) switch
            {
                (2, Slot.Afternoon) => new(Cmd.TellRule) { Who = P.Huo, Card = Pact },
                (3, Slot.Morning) => new(Cmd.TellRule) { Who = P.Mora, Card = Pact },
                (3, Slot.Forenoon) => new(Cmd.TellRule) { Who = P.Ward, Card = Pact },
                _ => Eat(g)
            }, Confront = g => GoodPlan(g, Pact), Back = _ => Mine },
        new() { Name = "公告板广撒（贴遍各板，不当面说）", Reads = true, Act = g => (g.Day, g.Slot) switch
            {
                (2, Slot.Afternoon) => new(Cmd.Post) { Place = Place.Hall, Card = Pact },
                (3, Slot.Morning) => new(Cmd.Post) { Place = Place.Logistics, Card = Pact },
                (3, Slot.Forenoon) => new(Cmd.Post) { Place = Place.DormW, Card = Pact },
                (3, Slot.Afternoon) => new(Cmd.Post) { Place = Place.DormLife, Card = Pact },
                _ => Eat(g)
            }, Confront = g => GoodPlan(g, Pact), Back = _ => Mine },
        new() { Name = "管饭（调停、交钥匙、帮厨）", Reads = true, Act = g => (g.Day, g.Slot) switch
            {
                (2, Slot.Noon) => A(Cmd.Mediate),
                (2, Slot.Afternoon) => new(Cmd.GiveKey) { Who = P.Mora, About = P.Shen },
                (3, Slot.Afternoon) => new(Cmd.Help) { Who = P.Shen },
                _ => Eat(g)
            }, Confront = g => GoodPlan(g) },
        new() { Name = "拉拢后勤组（茉拉）", Act = g => Writable(g) ? new(Cmd.Help) { Who = P.Mora } : Eat(g), Back = _ => 2 },
        new() { Name = "守夜（两夜都守）", Reads = true, Act = g => g.Slot == Slot.Night ? new(Cmd.Stakeout) { Place = g.Day == 2 ? Place.Logistics : Place.ArchiveFront } : Eat(g), Confront = g => GoodPlan(g) },
        new() { Name = "操纵", Act = g => (g.Day, g.Slot) switch
            {
                (2, Slot.Afternoon) => new(Cmd.Manipulate) { Who = P.Lucian, About = P.Quer },
                (3, Slot.Morning) => new(Cmd.Manipulate) { Who = P.Mora, About = P.Quer },
                (3, Slot.Afternoon) => new(Cmd.Post) { Place = Place.Hall, Card = BrandQuer },
                _ => Eat(g)
            }, Back = _ => 0 },
        new() { Name = "当众冤枉雀儿", Reads = true, Act = Eat, Confront = g => new([(4, 5), (5, 6)], null, false), Back = _ => 1 },
    ];

    public static BotResult Play(Bot bot, int switchDay = 99, Bot? after = null)
    {
        var sw = Stopwatch.StartNew();
        var g = new Game();
        while (!g.Ended)
        {
            var b = g.Day >= switchDay && after is not null ? after : bot;
            b.Free?.Invoke(g);
            if (b.Reads) Read(g);
            // 首课的作答在上课这一阵之前结算（与宿主相同）；上课本身是固定场面。
            if (g.Day == Game.FirstDay && g.Slot == Slot.Forenoon) g.HostLessonAnswer(b.Precise);
            var act = g.Day == Game.CaseDay && g.Slot == Slot.Evening ? new PlayerAction(Cmd.Confront) { Plan = b.Confront(g) }
                : g.Day == Game.FirstDay && g.Slot == Slot.Forenoon ? A(Cmd.Class) : b.Act(g);
            g.RunSlot(act);
        }
        var back = (switchDay <= Game.LastDay && after is not null ? after : bot).Back(g);
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

    /// <summary>整章推演的指纹：作者侧日志 + 每人位置、标签、接受的规则与信任。逐位比较用于确定性检查。</summary>
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
