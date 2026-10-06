using System.Diagnostics;
using System.Text.RegularExpressions;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>第二章原型自动检查（第三版）。只报告真实输出；失败照实打印。互不相干的整局推演并行跑（每局一个独立世界）。</summary>
public static class Checks
{
    private static int _fail, _total;
    private static void Check(bool ok, string what)
    {
        _total++;
        if (!ok) _fail++;
        Console.WriteLine($"[{(ok ? "通过" : "失败")}] {what}");
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern IntPtr GetCurrentThread();
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern bool GetThreadTimes(IntPtr h, out long c, out long e, out long k, out long u);
    /// <summary>当前线程实际占用的 CPU 时间（与他进程争用 CPU 时，墙钟时间会虚高；CPU 时间才是推演本身的开销）。</summary>
    public static TimeSpan ThreadCpu()
    {
        if (OperatingSystem.IsWindows() && GetThreadTimes(GetCurrentThread(), out _, out _, out var k, out var u)) return TimeSpan.FromTicks(k + u);
        return Process.GetCurrentProcess().TotalProcessorTime;
    }

    private static T[] Par<T>(IReadOnlyList<Func<T>> jobs)
    {
        var r = new T[jobs.Count];
        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i => r[i] = jobs[i]());
        return r;
    }

    public static int RunAll(string[] args)
    {
        var sw = Stopwatch.StartNew();
        var only = args.FirstOrDefault();
        if (only is null or "1") Determinism();
        var (results, switched) = only is null or "2" or "5" or "0" ? BotsCheck(only is null or "2" or "5") : ([], []);
        if (only is null or "2" or "0") Emergent(results);
        if (only is null or "3") CaseCheck();
        if (only is null or "4") AcceptanceSample();
        if (only is null or "5") Readability(results, switched);
        if (only is null or "6") Performance(results, sw);
        Console.WriteLine($"\n合计 {_total} 项，失败 {_fail} 项。用时 {sw.Elapsed.TotalSeconds:0.0} s（{Environment.ProcessorCount} 线程并行推演互不相干的整局）");
        return _fail == 0 ? 0 : 1;
    }

    // 1 -------------------------------------------------------------------
    private static void Determinism()
    {
        Console.WriteLine("\n== 1 确定性：同一输入的六日推演逐位一致 ==");
        var bots = new[] { Bots.All[0], Bots.All[7], Bots.All[5] };
        var runs = Par(bots.SelectMany(b => new Func<BotResult>[] { () => Bots.Play(b), () => Bots.Play(b) }).ToArray());
        for (var i = 0; i < bots.Length; i++)
            Check(runs[2 * i].Fingerprint == runs[2 * i + 1].Fingerprint && runs[2 * i].Ending == runs[2 * i + 1].Ending, $"{bots[i].Name}：两次运行指纹 {runs[2 * i].Fingerprint} / {runs[2 * i + 1].Fingerprint}");
        var r = Par<CaseTraces>([() => Game.Replay(CaseVars.Truth).Traces, () => Game.Replay(CaseVars.Truth).Traces]);
        Check(r[0] == r[1], "《少了一份》同一组变量重演两次，痕迹一致");
    }

    // 2 -------------------------------------------------------------------
    private static (List<BotResult>, List<(string Bot, BotResult Pact, BotResult Seal, Ending Original, bool WasOstracized)>) BotsCheck(bool full)
    {
        Console.WriteLine("\n== 2 策略机器人（各跑完六日 + 第 7 日议事；游说只用玩家看得见的信息）==");
        var results = Par(Bots.All.Select(b => (Func<BotResult>)(() => Bots.Play(b))).ToArray()).ToList();
        foreach (var r in results) Bots.Print(r);
        var switched = new List<(string, BotResult, BotResult, Ending, bool)>();
        if (!full) return (results, switched);
        Check(results.Count >= 10, $"策略数 {results.Count} ≥ 10");
        var endings = results.Select(r => r.Ending).ToHashSet();
        foreach (var e in new[] { Ending.OpenEach, Ending.Committee, Ending.Sealed, Ending.DetectivePact, Ending.NightSeized })
            Check(endings.Contains(e), $"结局可达：{Game.EndingText(e)}（{string.Join("、", results.Where(r => r.Ending == e).Select(r => r.Name))}）");
        Check(results.All(r => r.DemonLeaks == 0), $"恶魔在所有策略的全部台词中从未主动提第 9、10 条（共检查 {results.Count} 局）");
        Check(results.All(r => r.MaxHunger < 3), $"六日内无人饿死（各策略最长连饿日数：{string.Join(" ", results.Select(r => r.MaxHunger))}；连饿 3 日记为饿死）");
        double[] M(BotResult r) => [r.Ending == Ending.DetectivePact ? 1 : 0, r.AvgCredible, r.AvgInsight, r.RationsLeft, -r.Hooks, r.QuerCleared ? 1 : 0];
        var dominant = results.Where(a => results.All(b => a == b || M(a).Zip(M(b)).All(p => p.First >= p.Second) && M(a).Zip(M(b)).Any(p => p.First > p.Second))).ToArray();
        Check(dominant.Length == 0, "无万能策略（没有一个策略在 侦探之约/平均可信/平均见识/剩粮/钩少/雀儿洗清 六项上全面压过其余所有策略）" + (dominant.Length > 0 ? "：" + dominant[0].Name : ""));
        Console.WriteLine("  六项指标：" + string.Join(" | ", results.Select(r => $"{r.Name}[{string.Join(",", M(r).Select(x => x.ToString("0.#")))}]")));

        // 口粮：第 7 日不断（§12 第 8 条）。
        Console.WriteLine("  温室与第 6 日末存粮：" + string.Join(" | ", results.Select(r => $"{r.Name}:{(r.World.GreenhouseRepaired ? "修好" : "未修")}/{r.RationsLeft}")));
        Check(results.All(r => r.World.Curve.All(c => c.Rations > 0)), "六日内粮仓从未见底（各局第 6 日末存粮 > 0）");
        var serious = results.Where(r => r.World.GreenhouseRepaired).ToArray();
        Check(serious.Length > 0 && serious.All(r => r.RationsLeft >= 16), $"认真分工修好温室的打法，第 7 日口粮够十六人（{string.Join("、", serious.Select(r => $"{r.Name} {r.RationsLeft}"))}）");

        // 派系向四个价值聚落收敛（§12 第 8 条）。
        static P Leader(Cluster c) => c switch { Cluster.Order => P.Ward, Cluster.Labor => P.Shen, Cluster.Beauty => P.Lucian, _ => P.Veronica };
        int Fit(Game g) => g.Factions().Sum(f => f.Members.Count(m => m != P.Det && Cast.ClusterOf(m) != Cluster.None && Leader(Cast.ClusterOf(m)) == f.Leader));
        Console.WriteLine("  落在自己价值聚落派系里的人数（共 15）：" + string.Join(" ", results.Select(r => $"{r.Name}{Fit(r.World)}")));
        Check(Fit(results[0].World) >= 10, $"不干预时，派系向四个价值聚落收敛（{Fit(results[0].World)}/15 人落在本聚落的派系）");

        // 死局：前 3 日按各策略走，第 4 日起改走“立约·宽”或“劝封”（两者都会补查痕迹、重开对质）。
        Console.WriteLine("  死局检查：前 3 日按各策略走，第 4 日起分别改走“立约·宽”“劝封”“拉拢沃德”（都会先补查、再重开对质）；三种续法都改变不了结局才算死局");
        var pact = Bots.All.First(b => b.Name.StartsWith("立约·宽"));
        var seal = Bots.All.First(b => b.Name.StartsWith("劝封"));
        var comm = Bots.All.First(b => b.Name.StartsWith("拉拢沃德"));
        var others = Bots.All.Where(b => b != pact && b != seal).ToArray();
        var runs3 = Par(others.SelectMany(b => new Func<BotResult>[] { () => Bots.Play(b, switchDay: 4, after: pact), () => Bots.Play(b, switchDay: 4, after: seal), () => Bots.Play(b, switchDay: 4, after: comm) }).ToArray());
        var runs = runs3.Where((_, i) => i % 3 != 2).ToArray();
        var dead = new List<string>();
        for (var i = 0; i < others.Length; i++)
        {
            var original = results.First(x => x.Name == others[i].Name);
            var (r1, r2) = (runs[2 * i], runs[2 * i + 1]);
            switched.Add((others[i].Name, r1, r2, original.Ending, original.QuerOstracized));
            var r3 = runs3[3 * i + 2];
            Console.WriteLine($"    {others[i].Name}（原结局 {Game.EndingText(original.Ending)}，雀儿{(original.QuerCleared ? "洗清" : original.QuerOstracized ? "被排斥" : "—")}）→ 改立约·宽：{Game.EndingText(r1.Ending)}，雀儿{(r1.QuerCleared ? "洗清" : r1.QuerOstracized ? "被排斥" : "—")}；改劝封：{Game.EndingText(r2.Ending)}；改拉拢沃德：{Game.EndingText(r3.Ending)}");
            if (r1.Ending == original.Ending && r2.Ending == original.Ending && r3.Ending == original.Ending) dead.Add(others[i].Name);
        }
        Check(dead.Count == 0, "无死局：任何前三日之后，侦探仍能改写结局" + (dead.Count > 0 ? "（改写不了的：" + string.Join("、", dead) + "）" : ""));
        return (results, switched);
    }

    // 0 -------------------------------------------------------------------
    /// <summary>
    /// §12 第 0 条：人物动作涌现。每局记录涌现链（链长 ≥ 2、跨人物、至少两步是人物自己的决策）；
    /// 比较不同策略下的多样性；玩家可见（在场、守夜、清晨痕迹、他人转述）的涌现每日不少于两条；列出最有意思的十条。
    /// </summary>
    private static void Emergent(List<BotResult> results)
    {
        Console.WriteLine();
        Console.WriteLine("== 0 人物动作涌现（§12 第 0 条）==");
        if (results.Count == 0) return;
        var frames = new HashSet<string> { "行走", "操作", "交谈", "递物", "坐", "看守", "蹲查", "拉拽" };
        var all = new List<(string Bot, Game G, Game.Chain C)>();
        var sigs = new List<HashSet<string>>();
        var passive = new HashSet<string> { "什么都不做", "只上课", "只修机关", "每日赌约" };
        var dailyMin = int.MaxValue;
        foreach (var r in results)
        {
            var g = r.World;
            var chains = g.Chains();
            all.AddRange(chains.Select(c => (r.Name, g, c)));
            sigs.Add(chains.Select(c => c.Signature).ToHashSet());
            var npc = g.Events.Where(e => !e.Fixed && e.Who != P.Det).ToArray();
            // §13 第 2 条口径：当日在场所见 + 次日清晨可读的痕迹与转述，计他人自发的行动（不含作者节点与侦探自己）。
            var perDay = Enumerable.Range(2, 5).Select(d => npc.Count(e => e.Day == d && g.Readable(e) is not null)).ToArray();
            dailyMin = Math.Min(dailyMin, perDay.Min());
            Console.WriteLine($"  {r.Name}：人物动作 {npc.Length}（{npc.Select(e => e.Who).Distinct().Count()} 人，动作 {npc.Select(e => e.Job?.ToString() ?? "说").Distinct().Count()} 种）；涌现链 {chains.Count}，其中不经侦探的 {chains.Count(c => !c.ByDetective)}；玩家读得到的每日（第2—6日）：{string.Join(" ", perDay)}");
        }
        var frameOk = results.All(r => r.World.Events.All(e => frames.Contains(e.Frame)));
        Check(frameOk, "每个动作都标注了动作帧类别（行走、操作、交谈、递物、坐、看守、蹲查、拉拽）");
        Check(results.All(r => r.World.Chains().Count(c => !c.ByDetective) >= 5), "每局都有不经侦探的人物间涌现链（≥ 5 条）");
        var jac = new List<double>();
        for (var i = 0; i < sigs.Count; i++) for (var j = i + 1; j < sigs.Count; j++)
        {
            var u = sigs[i].Union(sigs[j]).Count();
            jac.Add(u == 0 ? 1 : (double)sigs[i].Intersect(sigs[j]).Count() / u);
        }
        var distinct = sigs.SelectMany(s => s).Distinct().Count();
        Console.WriteLine($"  多样性：16 局共 {distinct} 种不同的涌现链（签名＝依次是谁做了什么），两两相同比例平均 {jac.Average():0.00}，最高 {jac.Max():0.00}");
        Check(jac.Average() < 0.8, "同一开局在不同玩家策略下涌现出不同的链（两两相同比例平均 < 0.8）");
        Check(dailyMin >= 2, $"玩家每日读得到他人自发的行动不少于两件（当日在场 + 次日清晨的痕迹与转述；所有打法含什么都不做，各局各日最少 {dailyMin} 件）");
        // 五名背景人物各有自主欲望线（§13 第 14 条）：每人每天至少一件自己的事。
        var bg = new[] { P.Elena, P.Bertrand, P.Veronica, P.Selma, P.Moran };
        var idle = results[0].World;
        var lines = bg.Select(b => (b, days: Enumerable.Range(2, 5).Count(d => idle.Events.Any(e => e.Who == b && e.Day == d && !e.Fixed && e.Job is not (null or Job.Collect or Job.Sleep or Job.Rest or Job.Class or Job.Work or Job.CountLedger or Job.Socialize))))).ToArray();
        Console.WriteLine("  背景人物自主行动的天数（共 5 天，什么都不做的一局）：" + string.Join(" ", lines.Select(l => $"{Names.Of(l.b)}{l.days}")));
        Check(lines.All(l => l.days >= (l.b == P.Moran ? 3 : 5)), "五名背景人物每天都有自己的事（莫兰病好之后）");
        // 动作结算方式：转述、拉人、教人经 ActionRuntime（占身体）。
        var relays = results.Sum(r => r.World.Events.Count(e => e.Job == Job.Relay));
        var teach = results.Sum(r => r.World.Events.Count(e => e.Job == Job.Teach));
        var ex = results.Sum(r => r.World.Events.Count(e => e.Text.StartsWith("答应了") && e.Text.Contains("分我一口")));
        var share = results.Sum(r => r.World.Events.Count(e => e.Job == Job.Share));
        var follow = results.Sum(r => r.World.Events.Count(e => e.Job == Job.Follow));
        var avoid = results.Sum(r => r.World.Events.Count(e => e.Job == Job.Avoid));
        Console.WriteLine($"  16 局合计：转述/拉人 {relays}，教人 {teach}，换吃的（答应的）{ex}，分粮 {share}，跟随 {follow}，躲避 {avoid}");
        Check(relays > 0 && teach > 0 && share > 0 && follow > 0 && avoid > 0, "补全的动作词表都实际发生：转述、教人、分粮、跟随、躲避（换吃的见报告）");
        // 守门：身体占用 + 请求对抗（§13 第 4 条），没有人数阈值。
        var asks = results.SelectMany(r => r.World.Events.Where(e => e.Job == Job.AskLeave || e.Text.StartsWith("答应了") && (e.Text.Contains("让开") || e.Text.Contains("请回")))).ToArray();
        Console.WriteLine($"  守门对抗：请对方让开/请回 {asks.Count(e => e.Job == Job.AskLeave)} 次，答应的 {asks.Count(e => e.Text.StartsWith("答应了"))} 次；" +
            $"被守门拦下的夜 {results.Count(r => r.World.Events.Any(e => e.Text.StartsWith("守着配重室")))} 局");
        Check(asks.Any(e => e.Job == Job.AskLeave) && asks.Any(e => e.Text.StartsWith("答应了")), "守门与开库都是身体占住机关一站、再以请求对抗，由各自的 SocialSystem 决定让不让");
        Console.WriteLine("  最有意思的十条涌现链（跨局去重；〔〕里是玩家能否读到、怎么读到）：");
        var top = all.OrderByDescending(x => Emergence.Score(x.C, x.G)).GroupBy(x => Emergence.Trunk(x.C)).Select(g => g.First())
            .GroupBy(x => x.Bot).SelectMany(g => g.Take(2)).OrderByDescending(x => Emergence.Score(x.C, x.G)).Take(10).ToArray();
        for (var i = 0; i < top.Length; i++) Console.WriteLine($"   {i + 1,2}. 〈{top[i].Bot}〉{top[i].G.Describe(top[i].C)}");
    }

    // 3 -------------------------------------------------------------------
    private static void CaseCheck()
    {
        Console.WriteLine("\n== 3 《少了一份》与重演 ==");
        var actual = new Game();
        while (!(actual.Day == 3 && actual.Slot == Slot.Morning)) actual.RunSlot(null);
        var traces = actual.Dawn();
        Console.WriteLine("  第 3 日清晨（什么都不做的一局）：" + string.Join("；", traces.Describe().Select(d => d.Text)));
        Console.WriteLine("  作者侧昨夜：" + string.Join("；", actual.NightTruth));
        var sw = Stopwatch.StartNew();
        var all = CaseVars.All().ToArray();
        var replays = Par(all.Select(v => (Func<CaseTraces>)(() => Game.Replay(v).Traces)).ToArray());
        var matches = all.Where((v, i) => actual.Compare(replays[i], traces).All(l => !l.StartsWith("冲突"))).ToList();
        Console.WriteLine($"  36 组重演用时 {sw.ElapsedMilliseconds} ms（每组新建独立世界，并行）");
        Check(matches.Count == 1 && matches[0] == CaseVars.Truth, $"案件本身可辨（作者侧四变量）：吻合 {matches.Count} 组：{string.Join(" | ", matches)}");
        // §11 第 10 条：重演从现实快照重建（含侦探第 2 日自己的行动）。
        var nonUnique = new List<string>();
        var bots = Bots.All.Where(b => b.Name is "什么都不做" or "管饭（调停、交钥匙、修温室）" or "立约（侦探之约）" or "拉拢后勤组（茉拉）").ToArray();
        foreach (var b in bots)
        {
            var w = new Game();
            while (!(w.Day == 3 && w.Slot == Slot.Morning)) w.RunSlot(b.Act(w));
            var tr = w.Dawn();
            var rs = Par(all.Select(v => (Func<CaseTraces>)(() => w.ReplayCase(v).Traces)).ToArray());
            var m = all.Where((v, i) => w.Compare(rs[i], tr).All(l => !l.StartsWith("冲突"))).ToArray();
            Console.WriteLine($"    {b.Name}：第 2 夜「{string.Join("；", w.NightTruth.DefaultIfEmpty("无人取粮"))}」，吻合 {m.Length} 组");
            if (m.Length != 1 && w.NightTruth.Any(t => t.StartsWith("D2 雀儿 夜里自取"))) nonUnique.Add(b.Name);
        }
        Check(nonUnique.Count == 0, "快照重演：凡案件按原形发生的现实，作者侧四变量都被唯一解释" + (nonUnique.Count > 0 ? "（不唯一：" + string.Join("、", nonUnique) + "）" : ""));
        GenericVariables();
        ReadWriteLoop();
        WritingVariants();
    }

    /// <summary>
    /// §12 第 6 条：重演变量通用化。玩家第 3 日查完四处、问过夜巡后，清晨重演菜单由“已知规矩 × 怀疑的人”与“某人昨夜在不在某处”生成；
    /// 用其中两项（雀儿认/不认茉拉的夜取规矩、雀儿昨夜在/不在后勤院）的四种组合重演第 2 夜，只有真相与读到的痕迹全部吻合。
    /// </summary>
    private static void GenericVariables()
    {
        Console.WriteLine("  通用重演变量（玩家第 3 日查完痕迹之后，对第 2 夜）：");
        var bot = Bots.All.First(b => b.Name == "查案对质");
        var g = new Game();
        while (!(g.Day == 3 && g.Slot == Slot.Night)) g.RunSlot(g.Day == 3 && g.Slot == Slot.Evening ? new PlayerAction(Cmd.AskAbout) { Who = P.Bertrand } : bot.Act(g));
        var menu = g.ReplayMenu(2);
        Console.WriteLine($"    菜单共 {menu.Count} 项，前几项：{string.Join("；", menu.Take(8).Select(m => m.Text))}");
        var rule = g.MoraNight;
        var hasRule = menu.Any(m => m.Rule is { } r && r.Who == P.Quer && r.Spec == rule.Id);
        var hasWhere = menu.Any(m => m.Where is { } w && w.Who == P.Quer && w.Where == Place.Logistics);
        var combos = new[] { (true, true), (true, false), (false, true), (false, false) };
        var outs = Par(combos.Select(c => (Func<List<string>>)(() =>
        {
            var (tr, w) = g.ReplayNight(2, [new(P.Quer, rule.Id, c.Item1)], [new(P.Quer, Place.Logistics, c.Item2)]);
            var l = g.CompareTraces(2, tr);
            if (c.Item2 && !w.Moves.Any(m => m.Day == 2 && m.Who == P.Quer && m.To == Place.Logistics)) l.Insert(0, "冲突：重演里她那夜没去后勤院");
            return l;
        })).ToArray());
        for (var i = 0; i < combos.Length; i++)
            Console.WriteLine($"    雀儿{(combos[i].Item1 ? "认" : "不认")}茉拉的规矩、昨夜{(combos[i].Item2 ? "在" : "不在")}后勤院：{(outs[i].Any(l => l.StartsWith("冲突")) ? string.Join(" / ", outs[i].Where(l => l.StartsWith("冲突"))) : "全部吻合")}");
        Check(hasRule && hasWhere, "通用重演菜单里自然出现“雀儿 认/不认 茉拉的规矩”与“雀儿 昨夜在/不在 后勤院”（未为案件单独写变量）");
        Check(outs[0].All(l => !l.StartsWith("冲突")) && outs.Skip(1).All(o => o.Any(l => l.StartsWith("冲突"))), "四种组合中只有真相（认、在）与读到的痕迹全部吻合");
    }

    /// <summary>§11 第 1 条：读写闭环（用通用重演接口）。</summary>
    private static void ReadWriteLoop()
    {
        Console.WriteLine("  读写闭环（写下规则 → 次日清晨的痕迹 → 重演读出是谁因此改了做法）：");
        var bot = Bots.All.First(b => b.Name == "查案对质");
        var g = new Game();
        while (!(g.Day == 4 && g.Slot == Slot.Morning)) g.RunSlot(g.Day == 3 && g.Slot == Slot.Evening ? new PlayerAction(Cmd.Confront) { Plan = bot.Confront(g) } : bot.Act(g));
        g.HostExamine("台账");
        var spec = g.CaseRule!;
        Console.WriteLine($"    第 3 夜后侦探读到：{string.Join("；", g.KnownTraces(3).Values.Select(t => t.Text))}");
        var outs = Par<List<string>>([() => g.CompareTraces(3, g.ReplayNight(3, [new(P.Mora, spec.Id, true)]).Traces), () => g.CompareTraces(3, g.ReplayNight(3, [new(P.Mora, spec.Id, false)]).Traces)]);
        Console.WriteLine("    假设茉拉认了：" + string.Join(" / ", outs[0].Where(l => !l.StartsWith("未查"))));
        Console.WriteLine("    假设茉拉没认：" + string.Join(" / ", outs[1].Where(l => !l.StartsWith("未查"))));
        Check(outs[0].All(l => !l.StartsWith("冲突")) && outs[1].Any(l => l.StartsWith("冲突")), "玩家能从清晨痕迹 + 重演读出：茉拉认了侦探写的规则，因而夜取改为记账");
    }

    private static void WritingVariants()
    {
        var g = new Game();
        var bot = Bots.All.First(b => b.Name == "查案对质");
        while (!(g.Day == 4 && g.Slot == Slot.Morning)) g.RunSlot(g.Day == 3 && g.Slot == Slot.Evening ? new PlayerAction(Cmd.Confront) { Plan = new([(0, 0), (1, 2), (2, 4), (3, 5)], null, false) } : bot.Act(g));
        var keys = new[] { P.Ward, P.Mora, P.Ruan, P.Quer };
        var ok = new List<Card>();
        foreach (var card in Card.All(2).Where(c => c.Behavior == 0))
        {
            var spec = new RuleSpec { Id = -1, Text = card.Text, Author = P.Det, Flags = card.Flags(g), Build = _ => [], Card = card, Target = card.Target, Witnessed = (gg, p) => gg.SawNightPermission(p) };
            if (keys.All(k => g.AcceptScore(g[k], spec, Channel.Public, Place.Hall, out _) >= g.Options.AcceptThreshold)) ok.Add(card);
        }
        Console.WriteLine($"  “取一份口粮”（主语为任何人或值守者）的写法中，沃德、茉拉、阮清越、雀儿都接受 {ok.Count} 种，例如：{string.Join("；", ok.Take(4).Select(c => "「" + c.Text + "」"))}");
        Check(ok.Select(c => (c.Exception, c.Account)).Distinct().Count() >= 2, "至少两种有效的新规则写法（例外/记账方式不同）可被四名关键人物接受");
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
        foreach (var (card, label) in cards)
        {
            var spec = new RuleSpec { Id = -1, Text = label, Author = P.Det, Flags = card.Flags(g), Build = _ => [], Card = card, Target = card.Target, Manipulative = card.Target is not null };
            var vetoes = g.Npcs.Select(p => (p, v: g.Veto(p, spec))).Where(x => x.v is not null).Select(x => $"{x.p.Name}“{x.v}”").ToArray();
            Console.WriteLine($"  否决「{label}」：{(vetoes.Length == 0 ? "无" : string.Join("；", vetoes))}");
        }
        var archive = cards.Where(c => c.Card.Behavior is >= 5 and <= 8).Select(c => new RuleSpec { Id = -1, Text = c.Label, Author = P.Det, Flags = c.Card.Flags(g), Build = _ => [], Card = c.Card }).ToArray();
        Check(archive.All(s => g.Npcs.Any(p => g.Veto(p, s) is not null) || s.Flags.HasFlag(RF.PactArchive)), "名录库的每一种去向（约束开库除外）都至少有一个人以底线否决");
    }

    // 5 -------------------------------------------------------------------
    /// <summary>§12 的可读性检查：守夜摘要、信任链、立场与接受分开、重开对质、私占可预读、界面不露内部编号。</summary>
    private static void Readability(List<BotResult> results, List<(string Bot, BotResult Pact, BotResult Seal, Ending Original, bool WasOstracized)> switched)
    {
        Console.WriteLine("\n== 5 可读性（§12）==");
        // 守夜摘要：最显著的不超过五条，其余合成一句。
        var g = new Game();
        var lens = new List<int>();
        var sample = new List<string>();
        while (!g.Ended)
        {
            PlayerAction? act = g.Slot == Slot.Night ? new(Cmd.Stakeout) { Place = g.Day % 2 == 0 ? Place.Logistics : Place.ArchiveFront } : g.Day == 4 && g.Slot == Slot.Morning ? new(Cmd.Confront) : null;
            g.RunSlot(act);
            if (act?.Cmd == Cmd.Stakeout)
            {
                var day = g.Day - 1;
                var rep = g.StakeoutReport(day, act.Place!.Value);
                lens.Add(rep.Count(l => !l.StartsWith("还有")));
                if (sample.Count == 0) sample = rep;
            }
            g.Feed.Clear();
        }
        Console.WriteLine($"  守夜每夜给出的具体条数：{string.Join(" ", lens)}；第 2 夜守后勤院：{string.Join(" / ", sample)}");
        Check(lens.All(n => n <= 5), "守夜只给最显著的至多五条，其余合成“还有几道身影来去”");

        // 信任链：侦探经由亲见或他人亲口得知的转述。
        var spreads = results.SelectMany(r => r.World.SpreadKnown.Where(k => k.Teller is not null).Select(k => (r.Name, k))).ToArray();
        foreach (var (name, k) in spreads.Take(6)) Console.WriteLine($"    {name}：第{k.Day}日 {k.Text}（{k.How}）");
        Check(spreads.Length > 0, $"信任链留下可读痕迹：各局里侦探共得知 {spreads.Length} 次“谁把你的规矩讲给了谁”（只记他亲见或对方亲口说的）");

        // 立场与接受分开；立场可预读。
        int hit = 0, total = 0, nodButOther = 0;
        foreach (var r in results.Where(r => r.Ending != Ending.NightSeized))
            foreach (var (who, (_, spec)) in r.World.VoicedStance)
            {
                if (spec is null || !r.World.CouncilChoice.TryGetValue(who, out var chose)) continue;
                total++;
                if (Game.FamilyOf(spec) == Game.FamilyOf(chose)) hit++;
                if (r.World.DetToldFace.Any(x => x.Who == who && x.Nodded && x.Spec.Card is { Behavior: >= 5 and <= 8 } && Game.FamilyOf(x.Spec) != Game.FamilyOf(chose))) nodButOther++;
            }
        Console.WriteLine($"  议事前最后听到的立场与议事所站一致：{hit}/{total}；当面对你的名录库规矩点过头、议事却站到别处的：{nodButOther} 人次");
        Check(total > 0 && hit >= total * 0.6, "立场可预读：饭桌上复述谁的话，六成以上与议事所站一致（“点头”与“立场”分开呈现）");
        // 立场主要由亲见改变（§13 第 6 条）：亲眼见过裂纹或应名之人的人，议事不站开库的比例，明显高于没见过的人。
        int seenN = 0, seenNot = 0, unseenN = 0, unseenNot = 0;
        foreach (var r in results.Where(r => r.Ending != Ending.NightSeized))
            foreach (var p in r.World.Npcs.Where(p => p.Pro.DesireFlags.HasFlag(RF.OpenArchive) && r.World.CouncilChoice.ContainsKey(p.Id)))
            {
                var open = Game.FamilyOf(r.World.CouncilChoice[p.Id]) == Ending.OpenEach;
                if (p.Witnessed.Contains("crack")) { seenN++; if (!open) seenNot++; } else { unseenN++; if (!open) unseenNot++; }
            }
        var a = seenN == 0 ? 0 : (double)seenNot / seenN; var b = unseenN == 0 ? 0 : (double)unseenNot / unseenN;
        Console.WriteLine($"  想开库的人里：被带去亲眼看过基座裂纹的 {seenN} 人次，议事不站开库 {a:P0}；没见过的 {unseenN} 人次，不站开库 {b:P0}");
        Check(seenN > 0 && a - b >= 0.3, "立场主要由亲见改变：被带去亲眼看过裂纹的人比没看过的人明显更少站开库（差 ≥ 30 个百分点；同行即作证）");
        var councils = results.Where(r => r.Ending != Ending.NightSeized).Select(r => (r.Name, max: r.World.CouncilChoice.Values.GroupBy(Game.FamilyOf).Max(g => g.Count()) + 1)).ToArray();
        Console.WriteLine("  议事中最大一方的人数（含侦探）：" + string.Join(" ", councils.Select(c => $"{c.Name}{c.max}")));
        Check(councils.All(c => c.max <= 11), "没有 14:1:1 式的一边倒（最大一方 ≤ 11 人）");

        // 重开对质：前三日不查的局，第 4 日之后补查、重开，仍能洗清雀儿。
        var reopened = switched.Where(s => s.WasOstracized).ToArray();
        var cleared = reopened.Count(s => s.Pact.QuerCleared || s.Seal.QuerCleared);
        Check(reopened.Length > 0 && cleared == reopened.Length, $"证据不随日消失：第 4 日对质失败的 {reopened.Length} 局，改走续法后补查、重开对质，洗清雀儿的 {cleared} 局");

        // 私占可预读：被私占之前，前厅的脚印与停留逐夜增多。
        foreach (var r in results.Where(r => r.Ending == Ending.NightSeized).Take(3))
            Console.WriteLine($"    {r.Name}：第{r.World.SeizedDay}夜私占；之前各夜前厅（脚印/停留）：" + string.Join(" ", r.World.FrontStats.Where(kv => kv.Key < r.World.SeizedDay).OrderBy(kv => kv.Key).Select(kv => $"第{kv.Key}夜 {kv.Value.Prints}/{kv.Value.Stay:0}")));
        var seized = results.Where(r => r.Ending == Ending.NightSeized).ToArray();
        bool Readable(Game w) => w.FrontStats.TryGetValue(w.SeizedDay - 1, out var a) && w.FrontStats.TryGetValue(w.SeizedDay - 2, out var b) && a.Prints > 0 && b.Prints > 0 && a.Stay >= b.Stay;
        Check(seized.Length > 0 && seized.All(r => Readable(r.World)), $"私占可预读：{seized.Length} 局私占之前两夜，前厅都有脚印、停留不减（{seized.Count(r => Readable(r.World))}/{seized.Length}）");

        // 重演不泄露身份（§13 第 11 条）：墨影只给身量，心声只在全部吻合后出现。
        var rg = new Game();
        var bot = Bots.All.First(x => x.Name == "查案对质");
        while (!(rg.Day == 3 && rg.Slot == Slot.Night)) rg.RunSlot(rg.Day == 3 && rg.Slot == Slot.Evening ? new PlayerAction(Cmd.AskAbout) { Who = P.Bertrand } : bot.Act(rg));
        var (t1, w1) = rg.ReplayNight(2, [new(P.Quer, rg.MoraNight.Id, true)]);
        var c1 = rg.CompareTraces(2, t1);
        var ink = Game.Ink(w1, 2, [P.Quer], c1.All(l => !l.StartsWith("冲突")) && c1.Any(l => l.StartsWith("吻合")));
        var (t2, w2) = rg.ReplayNight(2, [new(P.Quer, rg.MoraNight.Id, false)]);
        var ink2 = Game.Ink(w2, 2, [P.Quer], false);
        Console.WriteLine("  重演墨影（真相假设）：" + string.Join(" / ", ink.Take(3).Select(l => l.Trim())));
        Check(ink.Concat(ink2).All(l => Enum.GetValues<P>().All(n => !l.Contains(Names.Of(n)))) && ink.Any(l => l.Contains("心声")) && ink2.All(l => !l.Contains("心声")),
            "重演不泄露身份：墨影只有身量；心声只在与已读痕迹全部吻合时出现");
        var cands = rg.ReplayCandidates(2);
        Console.WriteLine("  第 2 夜可假设的人（按所见身量与签名）：" + string.Join(" ", cands.Select(Names.Of)));
        Check(cands.Count is > 0 and < 15, "可假设的人限定于身量相符、未被排除者（不是全部十五人）");
        // 指控以亲见优先（§13 第 3 条）。
        var acc = results.SelectMany(r => r.World.Log.Where(l => l.Contains("当众指控") && l.Contains("亲眼所见"))).FirstOrDefault();
        Console.WriteLine("  亲见指控的例子：" + (acc ?? "（本轮没有）"));
        Check(results.All(r => r.World.Log.Where(l => l.Contains("当众指控") && l.Contains("身影")).All(l => !l.Contains("瘦小身影") || l.Contains("雀儿") || l.Contains("苏泠") || l.Contains("伊莲娜"))),
            "指控所依的身影与被指控者的身量一致（不再“瘦小身影却指控中等身量的人”）");
        // 子提示符遇到文件结束：停下，不代为作答（§13 第 16 条）。
        var eof = new StringWriter();
        var g7 = new Game();
        var oneDay = string.Concat(Enumerable.Repeat("歇\n", 5)) + "睡\n";
        new ConsoleUI(g7, new StringReader(oneDay + oneDay + "对质\n完\n" + string.Concat(Enumerable.Repeat("歇\n", 4)) + "睡\n" + oneDay + oneDay), eof, echo: true).Run();
        var eofText = eof.ToString();
        Check((eofText.Contains("没有替你作答") || eofText.Contains("你还没有表态")) && !eofText.Contains("结局："), "子提示符遇到文件结束时停下，不代玩家作答");

        // 界面：不露内部编号、报错是玩家语言、调试默认关闭、拼规矩用选项文字。
        var script = """
            帮助
            规矩
            讲 茉拉 值守者 有人急病时 取一份口粮 次日补记者不算私取 记入台账
            真相 茉拉
            调试 开
            查 不存在的东西
            讲 茉拉 1 2 3
            请 沃德 跳舞
            打赌 沃德 沃德 前厅 甲
            规矩
            传播
            领粮
            守夜 后勤院
            睡
            清晨
            重演
            重演 茉拉 认 甲
            笔记 茉拉
            """;
        var output = new StringWriter();
        new ConsoleUI(new Game(), new StringReader(script), output, echo: true).Run();
        var text = output.ToString();
        var leaks = Regex.Matches(text, @"#\d+|Exception|System\.|at DualOntology|ConditionFailed|OutOfReach|待第七部分|待裁定").Select(m => m.Value).Distinct().ToArray();
        Console.WriteLine("  界面抽查（不带 --debug 启动，含错误输入）：" + string.Join(" / ", text.Split('\n').Where(l => l.Contains("调试命令") || l.Contains("没有“") || l.Contains("规矩要五格") || l.Contains("不是请求") || l.Contains("拼成")).Select(l => l.Trim()).Distinct().Take(5)));
        Check(leaks.Length == 0, "界面不露内部编号与程序报错" + (leaks.Length > 0 ? "：" + string.Join(" ", leaks) : ""));
        Check(text.Contains("调试命令只在以 --debug 启动时可用") && !text.Contains("[真实认知]") && !text.Contains("[作者侧]"), "调试命令（作者侧日志、真实认知）默认关闭");
        Check(text.Contains("拼成：「值守者：有人急病时，取一份口粮；次日补记者不算私取；记入台账」"), "拼规矩用选项文字（也可用序号），拼好后回显整句");
    }

    // 6 -------------------------------------------------------------------
    private static void Performance(List<BotResult> results, Stopwatch total)
    {
        Console.WriteLine("\n== 6 性能 ==");
        if (results.Count > 0)
        {
            var ms = results.Select(r => r.Ms).ToArray();
            Console.WriteLine($"  六日 + 议事一局（并行跑时的墙钟）：最短 {ms.Min()} ms，最长 {ms.Max()} ms，平均 {ms.Average():0} ms");
        }
        var g = new Game();
        double worstWall = 0, worstCpu = 0;
        var where = "";
        var bot = Bots.All.First(b => b.Name.StartsWith("立约·宽"));
        while (!g.Ended)
        {
            var label = $"D{g.Day}{Names.Of(g.Slot)}";
            var sw = Stopwatch.StartNew();
            var cpu = ThreadCpu();
            g.RunSlot(g.Day == 4 && g.Slot == Slot.Morning ? new PlayerAction(Cmd.Confront) { Plan = bot.Confront(g) } : bot.Act(g));
            var c = (ThreadCpu() - cpu).TotalMilliseconds;
            if (c > worstCpu) { worstCpu = c; where = label; }
            worstWall = Math.Max(worstWall, sw.Elapsed.TotalMilliseconds);
        }
        var heaviest = g.People.Max(p => p.World.History.Count);
        Console.WriteLine($"  单个时段（单线程，含夜间推演）最长 CPU {worstCpu:0} ms（{where}），最长墙钟 {worstWall:0} ms；单人认知历史最长 {heaviest} 条");
        Console.WriteLine("  （若本机同时有别的进程占用 CPU，墙钟会虚高；CPU 时间才是推演本身的开销）");
        Check(worstCpu < 300, "单个时段推演 < 300 ms（CPU 时间）");
        Check(total.Elapsed.TotalMinutes < 5, $"完整检查 < 5 分钟（到此 {total.Elapsed.TotalSeconds:0} s）");
    }
}
