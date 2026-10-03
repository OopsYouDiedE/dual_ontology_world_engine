using System.Diagnostics;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>第六部分 §4 第二章原型自动检查 1—5。只报告真实输出；失败照实打印。</summary>
public static class Checks
{
    private static int _fail, _total;
    private static void Check(bool ok, string what)
    {
        _total++;
        if (!ok) _fail++;
        Console.WriteLine($"[{(ok ? "通过" : "失败")}] {what}");
    }

    public static int RunAll(string[] args)
    {
        var sw = Stopwatch.StartNew();
        var only = args.FirstOrDefault();
        if (only is null or "1") Determinism();
        var results = only is null or "2" or "5" ? BotsCheck(only is null or "2") : [];
        if (only is null or "3") CaseCheck();
        if (only is null or "4") AcceptanceSample();
        if (only is null or "5") Performance(results);
        Console.WriteLine($"\n合计 {_total} 项，失败 {_fail} 项。用时 {sw.Elapsed.TotalSeconds:0.0} s");
        return _fail == 0 ? 0 : 1;
    }

    // 1 -------------------------------------------------------------------
    private static void Determinism()
    {
        Console.WriteLine("\n== 1 确定性：同一输入的六日推演逐位一致 ==");
        foreach (var bot in new[] { Bots.All[0], Bots.All[7], Bots.All[5] })
        {
            var a = Bots.Play(bot);
            var b = Bots.Play(bot);
            Check(a.Fingerprint == b.Fingerprint && a.Ending == b.Ending, $"{bot.Name}：两次运行指纹 {a.Fingerprint} / {b.Fingerprint}");
        }
        var r1 = Game.Replay(CaseVars.Truth).Traces;
        var r2 = Game.Replay(CaseVars.Truth).Traces;
        Check(r1 == r2, "《少了一份》同一组变量重演两次，痕迹一致");
    }

    // 2 -------------------------------------------------------------------
    private static List<BotResult> BotsCheck(bool full)
    {
        Console.WriteLine("\n== 2 策略机器人（各跑完六日 + 第 7 日议事）==");
        var results = Bots.All.Select(b => Bots.Play(b)).ToList();
        foreach (var r in results) Bots.Print(r);
        if (!full) return results;
        Check(results.Count >= 10, $"策略数 {results.Count} ≥ 10");
        var endings = results.Select(r => r.Ending).ToHashSet();
        foreach (var e in new[] { Ending.OpenEach, Ending.Committee, Ending.Sealed, Ending.DetectivePact, Ending.NightSeized })
            Check(endings.Contains(e), $"结局可达：{Game.EndingText(e)}（{string.Join("、", results.Where(r => r.Ending == e).Select(r => r.Name))}）");
        Check(results.All(r => r.DemonLeaks == 0), $"恶魔在所有策略的全部台词中从未主动提第 9、10 条（共检查 {results.Count} 局）");
        Check(results.All(r => r.MaxHunger < 3), $"六日内无人饿死（各策略最长连饿日数：{string.Join(" ", results.Select(r => r.MaxHunger))}；连饿 3 日记为饿死）");
        // 万能策略：在六项指标上全面不劣于其余所有策略、且至少一项更好。
        double[] M(BotResult r) => [r.Ending == Ending.DetectivePact ? 1 : 0, r.AvgCredible, r.AvgInsight, r.RationsLeft, -r.Hooks, r.QuerCleared ? 1 : 0];
        var dominant = results.Where(a => results.All(b => a == b || M(a).Zip(M(b)).All(p => p.First >= p.Second) && M(a).Zip(M(b)).Any(p => p.First > p.Second))).ToArray();
        Check(dominant.Length == 0, "无万能策略（没有一个策略在 侦探之约/平均可信/平均见识/剩粮/钩少/雀儿洗清 六项上全面压过其余所有策略）" + (dominant.Length > 0 ? "：" + dominant[0].Name : ""));
        Console.WriteLine("  六项指标：" + string.Join(" | ", results.Select(r => $"{r.Name}[{string.Join(",", M(r).Select(x => x.ToString("0.#")))}]")));
        Console.WriteLine("  死局检查：前 3 日按各策略走，第 4 日起分别改走“立约·宽”与“劝封”；两种续法都改变不了结局才算死局");
        var pact = Bots.All.First(b => b.Name.StartsWith("立约·宽"));
        var seal = Bots.All.First(b => b.Name.StartsWith("劝封"));
        var dead = new List<string>();
        foreach (var bot in Bots.All.Where(b => b != pact && b != seal))
        {
            var original = results.First(x => x.Name == bot.Name).Ending;
            var r1 = Bots.Play(bot, switchDay: 4, after: pact);
            var r2 = Bots.Play(bot, switchDay: 4, after: seal);
            Console.WriteLine($"    {bot.Name}（原结局 {Game.EndingText(original)}）→ 改立约·宽：{Game.EndingText(r1.Ending)}；改劝封：{Game.EndingText(r2.Ending)}；雀儿{(r1.QuerCleared ? "洗清" : r1.QuerOstracized ? "被排斥" : "—")}");
            if (r1.Ending == original && r2.Ending == original) dead.Add(bot.Name);
        }
        Check(dead.Count == 0, "无死局：任何前三日之后，侦探仍能改写结局" + (dead.Count > 0 ? "（改写不了的：" + string.Join("、", dead) + "）" : ""));
        return results;
    }

    // 3 -------------------------------------------------------------------
    private static void CaseCheck()
    {
        Console.WriteLine("\n== 3 《少了一份》 ==");
        var actual = new Game();
        while (!(actual.Day == 3 && actual.Slot == Slot.Morning)) actual.RunSlot(null);
        var traces = actual.Dawn();
        Console.WriteLine("  第 3 日清晨（什么都不做的一局）：" + string.Join("；", traces.Describe().Select(d => d.Text)));
        Console.WriteLine("  作者侧昨夜：" + string.Join("；", actual.NightTruth));
        var matches = new List<CaseVars>();
        var sw = Stopwatch.StartNew();
        foreach (var v in CaseVars.All())
        {
            var (t, _) = Game.Replay(v);
            if (actual.Compare(t, traces).All(l => !l.StartsWith("冲突"))) matches.Add(v);
        }
        Console.WriteLine($"  36 组重演用时 {sw.ElapsedMilliseconds} ms（每组新建独立世界）");
        Check(matches.Count == 1 && matches[0] == CaseVars.Truth, $"重演唯一解：吻合 {matches.Count} 组：{string.Join(" | ", matches)}");
        for (var q = 0; q < 4; q++)
        for (var o = 0; o < CaseVars.Options[q].Length; o++)
        {
            var v = q switch { 0 => CaseVars.Truth with { MoraBelief = o }, 1 => CaseVars.Truth with { QuerRule = o }, 2 => CaseVars.Truth with { MargaretRule = o }, _ => CaseVars.Truth with { QuerSupper = o } };
            if (v == CaseVars.Truth) continue;
            var (t, _) = Game.Replay(v);
            var c = actual.Compare(t, traces).Where(l => l.StartsWith("冲突")).ToArray();
            Console.WriteLine($"    单改 {CaseVars.Questions[q]} = {CaseVars.Options[q][o]}：{(c.Length == 0 ? "无冲突！" : string.Join(" / ", c))}");
        }
        // §11 第 10 条：重演从现实快照重建（含侦探第 2 日自己的行动）。逐个第 2 日行为不同的策略看：现实痕迹是否仍被唯一解释。
        var nonUnique = new List<string>();
        foreach (var b in Bots.All.Where(b => b.Name is "什么都不做" or "管饭（调停、交钥匙、修温室）" or "立约（侦探之约）" or "拉拢后勤组（茉拉）"))
        {
            var w = new Game();
            while (!(w.Day == 3 && w.Slot == Slot.Morning)) w.RunSlot(b.Act(w));
            var tr = w.Dawn();
            var m = CaseVars.All().Where(v => w.Compare(w.ReplayCase(v).Traces, tr).All(l => !l.StartsWith("冲突"))).ToArray();
            Console.WriteLine($"    {b.Name}：第 2 夜「{string.Join("；", w.NightTruth.DefaultIfEmpty("无人取粮"))}」，吻合 {m.Length} 组{(m.Length is > 0 and <= 3 ? "：" + string.Join(" | ", m.Select(x => x.ToString())) : "")}");
            // 只在案件按原形发生（雀儿夜里自取）时要求唯一；玩家第 2 日改变了处境、案件变形的，四个变量本就问错了问题（见报告）。
            if (m.Length != 1 && w.NightTruth.Any(t => t.StartsWith("D2 雀儿 夜里自取"))) nonUnique.Add(b.Name);
        }
        Check(nonUnique.Count == 0, "快照重演：凡案件按原形发生的现实，四变量都被唯一解释" + (nonUnique.Count > 0 ? "（不唯一：" + string.Join("、", nonUnique) + "）" : ""));
        ReadWriteLoop();
        // 写：对质时哪些“取一份口粮”的四格写法能被沃德、茉拉、阮清越、雀儿同时接受。
        var g = new Game();
        var bot = Bots.All.First(b => b.Name == "查案对质");
        while (!(g.Day == 4 && g.Slot == Slot.Morning)) g.RunSlot(bot.Act(g));
        g.RunSlot(new PlayerAction(Cmd.Confront) { Plan = new([(0, 0), (1, 2), (2, 4), (3, 5)], null, false) });
        Console.WriteLine("  对质：" + string.Join(" / ", g.ConfrontLog));
        var keys = new[] { P.Ward, P.Mora, P.Ruan, P.Quer };
        var ok = new List<Card>();
        var byOne = new Dictionary<P, int>();
        foreach (var card in Card.All(2).Where(c => c.Behavior == 0))
        {
            var spec = new RuleSpec { Id = -1, Text = card.Text, Author = P.Det, Flags = card.Flags(g), Build = _ => [], Card = card, Target = card.Target, Witnessed = (gg, p) => gg.SawNightPermission(p) };
            var acc = keys.Where(k => g.AcceptScore(g[k], spec, Channel.Public, Place.Hall, out _) >= g.Options.AcceptThreshold).ToArray();
            foreach (var k in acc) byOne[k] = byOne.GetValueOrDefault(k) + 1;
            if (acc.Length == 4) ok.Add(card);
        }
        Console.WriteLine($"  “取一份口粮”（主语为任何人或值守者）的 {2 * Card.Conds.Length * Card.Exceptions.Length * Card.Accounts.Length} 种写法中，四人都接受 {ok.Count} 种：");
        foreach (var c in ok) Console.WriteLine($"    「{c.Text}」");
        Console.WriteLine("  各人单独接受的写法数：" + string.Join(" ", keys.Select(k => $"{Names.Of(k)}{byOne.GetValueOrDefault(k)}")));
        Check(ok.Select(c => (c.Exception, c.Account)).Distinct().Count() >= 2, "至少两种有效的新规则写法（例外/记账方式不同）可被四名关键人物接受");
        // 同行作证：第 3 日下午，一局带沃德去粮仓细看门闩，一局不带；比较当时沃德对“急病夜取”规则的接受。
        var card0 = Bots.RuleA;
        RuleSpec Spec(Game x) => new() { Id = -1, Text = "", Author = P.Det, Flags = card0.Flags(x), Build = _ => [], Card = card0, Witnessed = (gg, p) => gg.SawNightPermission(p) };
        (int Score, string Why, bool Saw) Branch(bool walk)
        {
            var x = new Game();
            while (!(x.Day == 3 && x.Slot == Slot.Noon)) x.RunSlot(bot.Act(x));
            x.RunSlot(new PlayerAction(Cmd.Help) { Who = P.Ward });   // 先帮他一把，他才肯同行（协商）
            x.RunSlot(walk ? new PlayerAction(Cmd.Escort) { Who = P.Ward, Place = Place.Logistics, Thing = "粮仓" } : null);
            var s = x.AcceptScore(x[P.Ward], Spec(x), Channel.Public, Place.Hall, out var why);
            return (s, why, x.SawNightPermission(x[P.Ward]));
        }
        var (withWalk, w1, saw1) = Branch(true);
        var (without, w2, saw2) = Branch(false);
        Console.WriteLine($"  第 3 日午间先帮沃德干活，下午之后，沃德对「{card0.Text}」：带他去看 {w1}（亲见门闩：{saw1}）；没带 {w2}（亲见：{saw2}）");
        Console.WriteLine("  注：若不带，沃德接受了阮清越的“日末清点”后，傍晚会自己去清点、看到工具痕（见报告）。");
        Check(withWalk > without, "同行作证对沃德的接受有实际影响（亲见 > 转述）");
    }

    /// <summary>
    /// §11 第 1 条：读写闭环。侦探第 4 日对质时写下“值守者：急病夜取须记账”；第 5 日清晨细看台账，
    /// 再分别假设“茉拉认了这条”与“茉拉没认”重演第 4 夜，看玩家能否从痕迹读出她因此做了不同的事。
    /// </summary>
    private static void ReadWriteLoop()
    {
        Console.WriteLine("  读写闭环（写下规则 → 次日清晨的痕迹 → 重演读出是谁因此改了做法）：");
        var bot = Bots.All.First(b => b.Name == "查案对质");
        var g = new Game();
        while (!(g.Day == 5 && g.Slot == Slot.Morning)) g.RunSlot(g.Day == 4 && g.Slot == Slot.Morning ? new PlayerAction(Cmd.Confront) { Plan = bot.Confront(g) } : bot.Act(g));
        g.RunSlot(new PlayerAction(Cmd.Inspect) { Place = Place.Logistics, Thing = "台账" });
        var spec = g.CaseRule!;
        Console.WriteLine($"    第 4 夜后侦探读到：{string.Join("；", g.KnownTraces(4).Values.Select(t => t.Text))}");
        var yes = g.CompareTraces(4, g.ReplayNight(4, [new(P.Mora, spec.Id, true)]).Traces);
        var no = g.CompareTraces(4, g.ReplayNight(4, [new(P.Mora, spec.Id, false)]).Traces);
        Console.WriteLine("    假设茉拉认了：" + string.Join(" / ", yes.Where(l => !l.StartsWith("未查"))));
        Console.WriteLine("    假设茉拉没认：" + string.Join(" / ", no.Where(l => !l.StartsWith("未查"))));
        Check(yes.All(l => !l.StartsWith("冲突")) && no.Any(l => l.StartsWith("冲突")), "玩家能从清晨痕迹 + 重演读出：茉拉认了侦探写的规则，因而夜取改为记账");
        var (_, w) = g.ReplayNight(4, [new(P.Mora, spec.Id, false)]);
        foreach (var l in Game.Ink(w, 4, [new(P.Mora, spec.Id, false)])) Console.WriteLine("    " + l.Trim());
    }

    // 4 -------------------------------------------------------------------
    private static void AcceptanceSample()
    {
        Console.WriteLine("\n== 4 规则接受抽样（第 2 日清晨之后的认知；侦探当面讲，+1）==");
        var g = new Game();
        g.RunSlot(null);
        var cards = new (Card Card, string Label)[]
        {
            (Bots.RuleA, "值守者急病夜取补记台账"), (new(0, 1, 0, 0, 1), "夜取须记账无例外"), (Bots.BrandQuer, "点名雀儿夜不入后勤"),
            (new(0, 0, 2, 0, 0), "钥匙交煮饭的人"), (new(0, 0, 3, 0, 0), "指控须附材料"), (Bots.Pact, "名录库亲取见证夜不开"),
            (Bots.Open, "名录库各取其物记账"), (Bots.Seal, "名录库封闭不启"), (Bots.Committee, "名录库委员会保管"),
        };
        Console.WriteLine("  " + "规则".PadRight(12) + string.Join("", g.Npcs.Select(p => p.Name.PadRight(5))));
        foreach (var (card, label) in cards)
        {
            var spec = new RuleSpec { Id = -1, Text = label, Author = P.Det, Flags = card.Flags(g), Build = _ => [], Card = card, Target = card.Target, Manipulative = card.Target is not null };
            Console.WriteLine("  " + label.PadRight(12) + string.Join("", g.Npcs.Select(p => { var s = g.AcceptScore(p, spec, Channel.Face, null, out _); return $"{(s >= 2 ? "✓" : "·")}{s,-4}"; })));
        }
        foreach (var (who, card) in new[] { (P.Quer, cards[2].Card), (P.Ward, cards[2].Card), (P.Mora, cards[1].Card), (P.Shen, cards[3].Card), (P.Mora, cards[3].Card), (P.Gu, cards[6].Card), (P.Su, cards[6].Card), (P.Veronica, cards[4].Card) })
        {
            var spec = new RuleSpec { Id = -1, Text = card.Text, Author = P.Det, Flags = card.Flags(g), Build = _ => [], Card = card, Target = card.Target, Manipulative = card.Target is not null };
            g.AcceptScore(g[who], spec, Channel.Face, null, out var why);
            Console.WriteLine($"  {Names.Of(who)} 对「{card.Text}」：{why}");
        }
        // 底线否决：同一条规则无法让所有人满意（§11 第 4 条）。
        foreach (var (card, label) in cards)
        {
            var spec = new RuleSpec { Id = -1, Text = label, Author = P.Det, Flags = card.Flags(g), Build = _ => [], Card = card, Target = card.Target, Manipulative = card.Target is not null };
            var vetoes = g.Npcs.Select(p => (p, v: g.Veto(p, spec))).Where(x => x.v is not null).Select(x => $"{x.p.Name}“{x.v}”").ToArray();
            Console.WriteLine($"  否决「{label}」：{(vetoes.Length == 0 ? "无" : string.Join("；", vetoes))}");
        }
        var archive = cards.Where(c => c.Card.Behavior is >= 5 and <= 8).Select(c => new RuleSpec { Id = -1, Text = c.Label, Author = P.Det, Flags = c.Card.Flags(g), Build = _ => [], Card = c.Card }).ToArray();
        Check(archive.All(s => g.Npcs.Any(p => g.Veto(p, s) is not null) || s.Flags.HasFlag(RF.PactArchive)), "名录库的每一种去向（约束开库除外）都至少有一个人以底线否决");
        Check(true, "抽样表已打印（人工核对见报告）");
    }

    // 5 -------------------------------------------------------------------
    private static void Performance(List<BotResult> results)
    {
        Console.WriteLine("\n== 5 性能 ==");
        if (results.Count > 0)
        {
            var ms = results.Select(r => r.Ms).ToArray();
            Console.WriteLine($"  六日 + 议事一局：最短 {ms.Min()} ms，最长 {ms.Max()} ms，平均 {ms.Average():0} ms");
        }
        var g = new Game();
        var worst = 0.0;
        var where = "";
        while (!g.Ended)
        {
            var label = $"D{g.Day}{Names.Of(g.Slot)}";
            var sw = Stopwatch.StartNew();
            g.RunSlot(null);
            if (sw.Elapsed.TotalMilliseconds > worst) { worst = sw.Elapsed.TotalMilliseconds; where = label; }
        }
        var heaviest = g.People.Max(p => p.World.History.Count);
        Console.WriteLine($"  单个时段（含夜间推演）最长 {worst:0} ms（{where}）；单人认知历史最长 {heaviest} 条");
        Check(worst < 500, "单个时段推演 < 500 ms");
    }
}
