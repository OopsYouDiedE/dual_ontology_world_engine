using System.Text.Json;
using static DualOntologyWorldEngine.TestSupport.FixtureData;
using System.Diagnostics;
using System.Text;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>策略机器人：只用玩家能做的动作（同一套 PlayerAction）。每个机器人是一张“第几日什么时段做什么”的表，外加对质与议事的选择。</summary>
public sealed class Bot
{
    public required string Id { get; init; }
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
    long Ms, string Fingerprint, string Council, Game World)
{
    public required Bot Strategy { get; init; }
}

public static class Bots
{
    private static PlayerAction A(Cmd c) => new(c);
    private static Card CardOf(string id)
    {
        var v = Sample("bots", "cards").GetProperty(id).EnumerateArray().Select(x => x.GetInt32()).ToArray();
        return new(v[0], v[1], v[2], v[3], v[4]);
    }
    public static readonly Card RuleA = CardOf(nameof(RuleA)), RuleB = CardOf(nameof(RuleB)),
        Pact = CardOf(nameof(Pact)), PactLoose = CardOf(nameof(PactLoose)), BrandQuer = CardOf(nameof(BrandQuer)),
        Committee = CardOf(nameof(Committee)), Seal = CardOf(nameof(Seal)), Open = CardOf(nameof(Open));
    /// <summary>机器人的日程按阶段写：1＝始业日（门开后），2＝案发那一日（夜里出事），3＝案发次日（搜查、晚饭后对质）；其余日子为 0。</summary>
    private static int Old(Game g) => g.Day == Game.CaseDay ? 3 : g.Day == Game.CaseNight ? 2 : g.Day == Game.FirstDay ? 1 : 0;

    /// <summary>议事时支持“我自己提出的那条”（Council 的约定：越界下标）。</summary>
    public const int Mine = 99;

    /// <summary>说得对的对质：现实里为真的主张，各配一件手里相关的证据（谁做了什么按推演的结果，不按作者版本的名字）。</summary>
    private static Game.ConfrontPlan GoodPlan(Game g, Card? rule = null, bool common = true) =>
        new(g.ValidConfrontPairs(), rule ?? RuleA, common);

    /// <summary>
    /// 读不耗时（总纲 §四）：翌日清晨把案子的痕迹一并细看、问过几位证人，再去做这一阵要写的事。
    /// 只用玩家在宿主里做得到的读（HostExamine、HostAsk），不读任何人的真实认知。
    /// </summary>
    public static void Read(Game g)
    {
        if (g.Day != Game.CaseDay || g.Slot != Slot.Morning) return;
        foreach (var thing in SampleStrings("bots", "read-things")) g.HostExamine(thing);
        foreach (var who in SampleStrings("bots", "read-people")) g.HostAsk(Enum.Parse<P>(who));
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
    private static bool Writable(Game g) => Sample("bots", "writable").EnumerateArray()
        .Any(row => row[0].GetInt32() == Old(g) && row[1].GetString() == g.Slot.ToString());

    private static PlayerAction ActionOf(JsonElement row)
    {
        string Value(string key) => row.TryGetProperty(key, out var value) ? value.GetString()! : "";
        return new(Enum.Parse<Cmd>(Value("cmd")))
        {
            Who = Value("who") is { Length: > 0 } who ? Enum.Parse<P>(who) : (P?)null,
            About = Value("about") is { Length: > 0 } about ? Enum.Parse<P>(about) : (P?)null,
            Place = Value("place") is { Length: > 0 } place ? Enum.Parse<Place>(place) : (Place?)null,
            Job = Value("job") is { Length: > 0 } job ? Enum.Parse<Job>(job) : (Job?)null,
            Card = Value("card") is { Length: > 0 } card ? CardOf(card) : null,
            Thing = Value("thing") is { Length: > 0 } thing ? thing : null, Wording = Value("wording") is { Length: > 0 } wording ? wording : null
        };
    }
    private static PlayerAction? PlanAction(Game game, JsonElement plan)
    {
        foreach (var step in plan.GetProperty("steps").EnumerateArray())
            if (step.GetProperty("phase").GetInt32() == Old(game) && step.GetProperty("slot").GetString() == game.Slot.ToString())
                return ActionOf(step);
        return plan.GetProperty("mode").GetString() switch
        {
            "idle" => null,
            "lobby" when Writable(game) => Greedy(game, CardOf(plan.GetProperty("card").GetString()!)),
            "help" when Writable(game) => new(Cmd.Help) { Who = Enum.Parse<P>(plan.GetProperty("who").GetString()!) },
            _ => Eat(game)
        };
    }
    public static readonly Bot[] All = Sample("bots", "plans").EnumerateArray().Select(plan => new Bot
    {
        Id = plan.GetProperty("id").GetString()!, Name = plan.GetProperty("name").GetString()!,
        Act = game => PlanAction(game, plan), Reads = plan.TryGetProperty("reads", out var reads) && reads.GetBoolean(),
        Precise = plan.TryGetProperty("precise", out var precise) && precise.GetBoolean(),
        Back = _ => plan.TryGetProperty("back", out var back) ? back.GetInt32() : -1,
        Confront = game => !plan.TryGetProperty("confront", out var confront) ? null :
            confront.GetProperty("mode").GetString() == "good" ? GoodPlan(game, CardOf(confront.GetProperty("rule").GetString()!), confront.GetProperty("common").GetBoolean()) :
            new(confront.GetProperty("pairs").EnumerateArray().Select(pair => (pair[0].GetInt32(), pair[1].GetInt32())).ToArray(), null, confront.GetProperty("common").GetBoolean())
    }).ToArray();

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
            Fingerprint(g), string.Join("\n    ", g.CouncilLog), g) { Strategy = bot };
    }

    public static bool Leak(string text) => new[] { SampleText("bots", "text-025"), SampleText("bots", "text-026"), SampleText("bots", "text-027"), SampleText("bots", "text-028"), SampleText("bots", "text-029"), SampleText("bots", "text-030"), SampleText("bots", "text-031"), SampleText("bots", "text-032"), SampleText("bots", "text-033") }.Any(text.Contains);

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
        Console.WriteLine($"   雀儿：{(r.QuerCleared ? SampleText("bots", "text-034") : r.QuerOstracized ? SampleText("bots", "text-035") : "—")}；共同规则：{(r.Common ? SampleText("bots", "text-036") : SampleText("bots", "text-037"))}");
        if (verbose) Console.WriteLine(SampleText("bots", "text-038") + r.Council);
    }
}
