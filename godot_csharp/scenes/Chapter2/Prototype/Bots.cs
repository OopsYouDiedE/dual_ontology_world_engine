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
    /// 机器人用的“会看人”的游说：挑一个此刻最值得当面说服的人——他听了会站到这条规则一边（比别的方案更合他意），
    /// 而且信他的人多（他会转述）。机器人偷看了真实认知；这是测试工具，不是玩家能做的事。
    /// </summary>
    private static PlayerAction Greedy(Game g, Card card)
    {
        var spec = g.CardSpec(card, P.Det);
        var plans = g.ArchivePlans().Where(s => s.Card != card).ToArray();
        int Best(Person x) => plans.Select(s => g.AcceptScore(x, s, Channel.Public, Place.Hall, out _)).DefaultIfEmpty(-99).Max();
        var pick = g.Npcs.Where(x => Game.Get(x.Body, T.Sick) == 0 && x.Acc.GetValueOrDefault(spec) != Truth.True && !x.Vetoed.Contains(spec))
            .Select(x => (x, win: g.AcceptScore(x, spec, Channel.Face, null, out _) >= Math.Max(g.Options.AcceptThreshold, Best(x)) ? 1 : 0,
                reach: g.Npcs.Count(o => o != x && g.TrustIn(o, x) >= 1 && o.Acc.GetValueOrDefault(spec) != Truth.True)))
            .OrderByDescending(t => t.win).ThenByDescending(t => t.reach).ThenBy(t => t.x.Id).FirstOrDefault();
        return pick.x is null ? new(Cmd.Post) { Place = Place.Hall, Card = card } : new(Cmd.TellRule) { Who = pick.x.Id, Card = card };
    }

    private static PlayerAction? Campaign(Game g, Card card) => (g.Day, g.Slot) switch
    {
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
        new() { Name = "拉拢沃德（委员会）", Act = g => Investigate(g) ?? Campaign(g, Committee) ?? Eat(g), Confront = g => GoodPlan(g), Back = g => 9 },
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
                (_, Slot.Afternoon) => new(Cmd.Greenhouse) { Crew = [P.Selma, P.Margaret] },
                _ => Eat(g)
            } },
        new() { Name = "每日赌约", Act = g => g.Slot == Slot.Forenoon ? A(Cmd.Class) : Eat(g),
            Free = g => { if (g.Slot == Slot.Noon) g.MakeWager(P.Ruan, P.Bertrand, Place.Hall, null); } },
        new() { Name = "帮人开库（各取其物）", Act = g => Investigate(g) ?? Lobby(g, Open, P.Huo, P.Su, P.Selma, P.Ruan) ?? (g.Slot == Slot.Afternoon ? new(Cmd.Help) { Who = P.Huo } : Eat(g)),
            Confront = g => GoodPlan(g), Back = g => 9 },
        new() { Name = "劝封（讲钩的代价）", Act = g => Investigate(g) ?? Campaign(g, Seal) ?? Eat(g), Confront = g => GoodPlan(g), Back = g => 9 },
        new() { Name = "立约·宽（不禁夜，争取想开库的人）", Act = g => Investigate(g) ?? (g.Day, g.Slot) switch
            {
                (5, Slot.Forenoon) => new(Cmd.Class) { Topic = 0, Precise = true },
                (5, Slot.Afternoon) => new(Cmd.Archive) { Crew = [P.Huo, P.Quer, P.Su, P.Bertrand] },
                (5, Slot.Evening) => new(Cmd.Post) { Place = Place.Hall, Card = PactLoose },
                (6, Slot.Morning) => new(Cmd.TellRule) { Who = P.Su, Card = PactLoose },
                (6, Slot.Forenoon) => new(Cmd.TellRule) { Who = P.Selma, Card = PactLoose },
                (6, Slot.Afternoon) => new(Cmd.TellRule) { Who = P.Lucian, Card = PactLoose },
                (6, Slot.Evening) => new(Cmd.TellRule) { Who = P.Quer, Card = PactLoose },
                _ => Eat(g)
            }, Confront = g => GoodPlan(g), Back = g => -1 },
        new() { Name = "抓枢纽（只当面说服三个人，靠他们转述）", Act = g => Investigate(g) ?? (g.Day, g.Slot) switch
            {
                (5, Slot.Forenoon) => new(Cmd.Class) { Topic = 1, Precise = true },
                (5, Slot.Afternoon) => new(Cmd.Archive) { Crew = [P.Huo, P.Quer, P.Su, P.Bertrand] },
                (5, Slot.Evening) => new(Cmd.TellRule) { Who = P.Huo, Card = Pact },
                (6, Slot.Morning) => new(Cmd.TellRule) { Who = P.Mora, Card = Pact },
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
