using static DualOntologyWorldEngine.TestSupport.FixtureData;
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
        Console.WriteLine($"[{(ok ? SampleText("chapter2-checks", "text-001") : SampleText("chapter2-checks", "text-002"))}] {what}");
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
        Console.WriteLine(SampleText("chapter2-checks", "text-003"));
        var bots = SampleStrings("chapter2-checks", "determinism-bots")
            .Select(id => Bots.All.Single(bot => bot.Id == id)).ToArray();
        var runs = Par(bots.SelectMany(b => new Func<BotResult>[] { () => Bots.Play(b), () => Bots.Play(b) }).ToArray());
        for (var i = 0; i < bots.Length; i++)
            Check(runs[2 * i].Fingerprint == runs[2 * i + 1].Fingerprint && runs[2 * i].Ending == runs[2 * i + 1].Ending, $"{bots[i].Name}：两次运行指纹 {runs[2 * i].Fingerprint} / {runs[2 * i + 1].Fingerprint}");
        var r = Par<CaseTraces>([() => Game.Replay(SampleAs<CaseVars>("chapter2-checks", "golden-variables")).Traces, () => Game.Replay(SampleAs<CaseVars>("chapter2-checks", "golden-variables")).Traces]);
        Check(r[0] == r[1], SampleText("chapter2-checks", "text-004"));
    }

    // 2 -------------------------------------------------------------------
    private static (List<BotResult>, List<(string Bot, BotResult Pact, BotResult Seal, Ending Original, bool WasOstracized)>) BotsCheck(bool full)
    {
        Console.WriteLine(SampleText("chapter2-checks", "text-005"));
        var results = Par(Bots.All.Select(b => (Func<BotResult>)(() => Bots.Play(b))).ToArray()).ToList();
        foreach (var r in results) Bots.Print(r);
        var switched = new List<(string, BotResult, BotResult, Ending, bool)>();
        if (!full) return (results, switched);
        Check(results.Count >= 10, $"策略数 {results.Count} ≥ 10");
        var endings = results.Select(r => r.Ending).ToHashSet();
        foreach (var e in new[] { Ending.OpenEach, Ending.Committee, Ending.Sealed, Ending.DetectivePact, Ending.NightSeized })
            Check(endings.Contains(e), $"结局可达：{Game.EndingText(e)}（{string.Join("、", results.Where(r => r.Ending == e).Select(r => r.Name))}）");
        Check(results.All(r => r.DemonLeaks == 0), $"恶魔在所有策略的全部台词中从未主动提第 9、10 条（共检查 {results.Count} 局）");
        Check(results.All(r => r.MaxHunger < 3), $"整章无人饿死（各策略最长连饿日数：{string.Join(" ", results.Select(r => r.MaxHunger))}；连饿 3 日记为饿死）");
        double[] M(BotResult r) => [r.Ending == Ending.DetectivePact ? 1 : 0, r.AvgCredible, r.AvgInsight, r.RationsLeft, -r.Hooks, r.QuerCleared ? 1 : 0];
        var dominant = results.Where(a => results.All(b => a == b || M(a).Zip(M(b)).All(p => p.First >= p.Second) && M(a).Zip(M(b)).Any(p => p.First > p.Second))).ToArray();
        Check(dominant.Length == 0, "无万能策略（没有一个策略在 侦探之约/平均可信/平均见识/剩粮/钩少/雀儿洗清 六项上全面压过其余所有策略）" + (dominant.Length > 0 ? "：" + dominant[0].Name : ""));
        Console.WriteLine(SampleText("chapter2-checks", "text-006") + string.Join(" | ", results.Select(r => $"{r.Name}[{string.Join(",", M(r).Select(x => x.ToString("0.#")))}]")));

        // 口粮：章内不断（温室已移出本章，第三部分 §15）。
        Console.WriteLine(SampleText("chapter2-checks", "text-007") + string.Join(" | ", results.Select(r => $"{r.Name}:{r.RationsLeft}")));
        Check(results.All(r => r.World.Curve.All(c => c.Rations > 0)), SampleText("chapter2-checks", "text-008"));
        Check(results.Select(r => r.QuerCleared).Distinct().Count() == 2 && results.Select(r => r.Common).Distinct().Count() == 2,
            SampleText("chapter2-checks", "text-009"));
        var cleared = results.Where(r => SampleStrings("chapter2-checks", "cleared-bots").Contains(r.Strategy.Id)).ToArray();
        Check(cleared.Length > 0 && cleared.All(r => r.QuerCleared), SampleText("chapter2-checks", "text-011"));

        // 派系向四个价值聚落收敛（§12 第 8 条）。
        static P Leader(Cluster c) => c switch { Cluster.Order => P.Ward, Cluster.Labor => P.Shen, Cluster.Beauty => P.Lucian, _ => P.Veronica };
        int Fit(Game g) => g.Factions().Sum(f => f.Members.Count(m => m != P.Det && Cast.ClusterOf(m) != Cluster.None && Leader(Cast.ClusterOf(m)) == f.Leader));
        Console.WriteLine(SampleText("chapter2-checks", "text-012") + string.Join(" ", results.Select(r => $"{r.Name}{Fit(r.World)}")));
        Check(Fit(results[0].World) >= 10, $"不干预时，派系向四个价值聚落收敛（{Fit(results[0].World)}/15 人落在本聚落的派系）");

        // 死局：始业日按各策略走，翌日起改走“立约·宽”“劝封”或“拉拢沃德”。
        Console.WriteLine(SampleText("chapter2-checks", "text-013"));
        var pact = Bots.All.First(b => b.Id == "pact-loose");
        var seal = Bots.All.First(b => b.Id == "seal");
        var comm = Bots.All.First(b => b.Id == "committee");
        var others = Bots.All.Where(b => b != pact && b != seal && b != comm).ToArray();
        var runs3 = Par(others.SelectMany(b => new Func<BotResult>[] { () => Bots.Play(b, switchDay: Game.CaseDay, after: pact), () => Bots.Play(b, switchDay: Game.CaseDay, after: seal), () => Bots.Play(b, switchDay: Game.CaseDay, after: comm) }).ToArray());
        var runs = runs3.Where((_, i) => i % 3 != 2).ToArray();
        var dead = new List<string>();
        for (var i = 0; i < others.Length; i++)
        {
            var original = results.First(x => x.Name == others[i].Name);
            var (r1, r2) = (runs[2 * i], runs[2 * i + 1]);
            switched.Add((others[i].Name, r1, r2, original.Ending, original.QuerOstracized));
            var r3 = runs3[3 * i + 2];
            Console.WriteLine($"    {others[i].Name}（原结局 {Game.EndingText(original.Ending)}，雀儿{(original.QuerCleared ? SampleText("chapter2-checks", "text-017") : original.QuerOstracized ? SampleText("chapter2-checks", "text-018") : "—")}）→ 改立约·宽：{Game.EndingText(r1.Ending)}，雀儿{(r1.QuerCleared ? SampleText("chapter2-checks", "text-017") : r1.QuerOstracized ? SampleText("chapter2-checks", "text-018") : "—")}；改劝封：{Game.EndingText(r2.Ending)}；改拉拢沃德：{Game.EndingText(r3.Ending)}");
            if (r1.Ending == original.Ending && r2.Ending == original.Ending && r3.Ending == original.Ending) dead.Add(others[i].Name);
        }
        Check(dead.Count == 0, SampleText("chapter2-checks", "text-019") + (dead.Count > 0 ? SampleText("chapter2-checks", "text-020") + string.Join("、", dead) + "）" : ""));
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
        Console.WriteLine(SampleText("chapter2-checks", "text-021"));
        if (results.Count == 0) return;
        var frames = new HashSet<string> { SampleText("chapter2-checks", "text-022"), SampleText("chapter2-checks", "text-023"), SampleText("chapter2-checks", "text-024"), SampleText("chapter2-checks", "text-025"), SampleText("chapter2-checks", "text-026"), SampleText("chapter2-checks", "text-027"), SampleText("chapter2-checks", "text-028"), SampleText("chapter2-checks", "text-029") };
        var all = new List<(string Bot, Game G, Game.Chain C)>();
        var sigs = new List<HashSet<string>>();
                var dailyMin = int.MaxValue;
        foreach (var r in results)
        {
            var g = r.World;
            var chains = g.Chains();
            all.AddRange(chains.Select(c => (r.Name, g, c)));
            sigs.Add(chains.Select(c => c.Signature).ToHashSet());
            var npc = g.Events.Where(e => !e.Fixed && e.Who != P.Det).ToArray();
            // §13 第 2 条口径：当日在场所见 + 次日清晨可读的痕迹与转述，计他人自发的行动（不含作者节点与侦探自己）。
            var perDay = Enumerable.Range(Game.FirstDay, Game.LastDay - Game.FirstDay).Select(d => npc.Count(e => e.Day == d && g.Readable(e) is not null)).ToArray();
            dailyMin = Math.Min(dailyMin, perDay.Min());
            Console.WriteLine($"  {r.Name}：人物动作 {npc.Length}（{npc.Select(e => e.Who).Distinct().Count()} 人，动作 {npc.Select(e => e.Job?.ToString() ?? SampleText("chapter2-checks", "text-030")).Distinct().Count()} 种）；涌现链 {chains.Count}，其中不经侦探的 {chains.Count(c => !c.ByDetective)}；玩家读得到的每日（始业日、翌日）：{string.Join(" ", perDay)}");
        }
        var frameOk = results.All(r => r.World.Events.All(e => frames.Contains(e.Frame)));
        Check(frameOk, SampleText("chapter2-checks", "text-031"));
        Check(results.All(r => r.World.Chains().Count(c => !c.ByDetective) >= 5), SampleText("chapter2-checks", "text-032"));
        var jac = new List<double>();
        for (var i = 0; i < sigs.Count; i++) for (var j = i + 1; j < sigs.Count; j++)
        {
            var u = sigs[i].Union(sigs[j]).Count();
            jac.Add(u == 0 ? 1 : (double)sigs[i].Intersect(sigs[j]).Count() / u);
        }
        var distinct = sigs.SelectMany(s => s).Distinct().Count();
        Console.WriteLine($"  多样性：16 局共 {distinct} 种不同的涌现链（签名＝依次是谁做了什么），两两相同比例平均 {jac.Average():0.00}，最高 {jac.Max():0.00}");
        Check(jac.Average() < 0.8, SampleText("chapter2-checks", "text-033"));
        Check(dailyMin >= 2, $"玩家每日读得到他人自发的行动不少于两件（当日在场 + 次日清晨的痕迹与转述；所有打法含什么都不做，各局各日最少 {dailyMin} 件）");
        // 五名背景人物各有自主欲望线（§13 第 14 条）：每人每天至少一件自己的事。
        var bg = new[] { P.Elena, P.Bertrand, P.Veronica, P.Selma, P.Moran };
        var idle = results[0].World;
        var lines = bg.Select(b => (b, days: Enumerable.Range(Game.FirstDay, Game.LastDay - Game.FirstDay).Count(d => idle.Events.Any(e => e.Who == b && e.Day == d && !e.Fixed && e.Job is not (null or Job.Collect or Job.Sleep or Job.Rest or Job.Class or Job.Work or Job.CountLedger or Job.Socialize))))).ToArray();
        Console.WriteLine(SampleText("chapter2-checks", "text-034") + string.Join(" ", lines.Select(l => $"{Names.Of(l.b)}{l.days}")));
        var moranRecovered = idle.Log.Any(l => l.Contains(SampleText("chapter2-checks", "text-035")));
        Check(lines.All(l => l.b == P.Moran ? !moranRecovered || l.days >= 1 : l.days >= 2), $"背景人物每天都有自己的事（莫兰始业日下午起发烧，{(moranRecovered ? SampleText("chapter2-checks", "text-036") : SampleText("chapter2-checks", "text-037"))}）");
        // 动作结算方式：转述、拉人、教人经 ActionRuntime（占身体）。
        var relays = results.Sum(r => r.World.Events.Count(e => e.Job == Job.Relay));
        var teach = results.Sum(r => r.World.Events.Count(e => e.Job == Job.Teach));
        var ex = results.Sum(r => r.World.Events.Count(e => e.Text.StartsWith(SampleText("chapter2-checks", "text-038")) && e.Text.Contains(SampleText("chapter2-checks", "text-039"))));
        var share = results.Sum(r => r.World.Events.Count(e => e.Job == Job.Share));
        var follow = results.Sum(r => r.World.Events.Count(e => e.Job == Job.Follow));
        var avoid = results.Sum(r => r.World.Events.Count(e => e.Job == Job.Avoid));
        Console.WriteLine($"  16 局合计：转述/拉人 {relays}，教人 {teach}，换吃的（答应的）{ex}，分粮 {share}，跟随 {follow}，躲避 {avoid}");
        // 两日的章节里：只有一门课且全员到课，没人需要别人转教；连饿一整天又恰与信得过的人同处才会分粮。两者只报告，不强求。
        Check(relays > 0 && follow > 0 && avoid > 0, SampleText("chapter2-checks", "text-040"));
        // 守门：身体占用 + 请求对抗（§13 第 4 条），没有人数阈值。
        var asks = results.SelectMany(r => r.World.Events.Where(e => e.Job == Job.AskLeave || e.Text.StartsWith(SampleText("chapter2-checks", "text-038")) && (e.Text.Contains(SampleText("chapter2-checks", "text-041")) || e.Text.Contains(SampleText("chapter2-checks", "text-042"))))).ToArray();
        Console.WriteLine($"  守门对抗：请对方让开/请回 {asks.Count(e => e.Job == Job.AskLeave)} 次，答应的 {asks.Count(e => e.Text.StartsWith(SampleText("chapter2-checks", "text-038")))} 次；" +
            $"被守门拦下的夜 {results.Count(r => r.World.Events.Any(e => e.Text.StartsWith(SampleText("chapter2-checks", "text-043"))))} 局");
        Check(asks.Any(e => e.Job == Job.AskLeave) && asks.Any(e => e.Text.StartsWith(SampleText("chapter2-checks", "text-038"))), SampleText("chapter2-checks", "text-044"));
        Console.WriteLine(SampleText("chapter2-checks", "text-045"));
        var top = all.OrderByDescending(x => Emergence.Score(x.C, x.G)).GroupBy(x => Emergence.Trunk(x.C)).Select(g => g.First())
            .GroupBy(x => x.Bot).SelectMany(g => g.Take(2)).OrderByDescending(x => Emergence.Score(x.C, x.G)).Take(10).ToArray();
        for (var i = 0; i < top.Length; i++) Console.WriteLine($"   {i + 1,2}. 〈{top[i].Bot}〉{top[i].G.Describe(top[i].C)}");
    }

    // 3 -------------------------------------------------------------------
    private static void CaseCheck()
    {
        Console.WriteLine(SampleText("chapter2-checks", "text-046"));
        var actual = new Game();
        while (!(actual.Day == Game.CaseDay && actual.Slot == Slot.Morning)) actual.RunSlot(null);
        var traces = actual.Dawn();
        Console.WriteLine(SampleText("chapter2-checks", "text-047") + string.Join("；", traces.Describe().Select(d => d.Text)));
        Console.WriteLine(SampleText("chapter2-checks", "text-048") + string.Join("；", actual.NightTruth));
        var sw = Stopwatch.StartNew();
        var all = CaseVars.All().ToArray();
        var replayRuns = Par(all.Select(v => (Func<(CaseTraces Traces, Game World)>)(() => Game.Replay(v))).ToArray());
        var replays = replayRuns.Select(r => r.Traces).ToArray();
        var matches = all.Where((v, i) => actual.Compare(replays[i], traces).All(l => !l.StartsWith(SampleText("chapter2-checks", "text-049")))).ToList();
        var matchWorlds = all.Select((v, i) => (v, i)).Where(x => matches.Contains(x.v)).Select(x => replayRuns[x.i].World).ToArray();
        Console.WriteLine($"  36 组重演用时 {sw.ElapsedMilliseconds} ms（每组新建独立世界，并行）");
        // 推演出来的世界里，某个变量可能已不影响任何痕迹（例如门闩前一夜就修好了，玛格丽特认不认沃德的规矩就无从分辨）。
        // 可辨的标准是：真相在吻合的假设之中，且所有吻合的假设对每一句主张的真假结论一致（侦探说得出口的话不因此含糊）。
        bool Agree(IReadOnlyList<Game> worlds) => worlds.Count > 0 && Enumerable.Range(0, CaseDesign.Claims.Count).All(c => worlds.All(w => w.HostClaimTrue(c)) || worlds.All(w => !w.HostClaimTrue(c)));
        Check(matches.Contains(SampleAs<CaseVars>("chapter2-checks", "golden-variables")) && Agree(matchWorlds), $"案件本身可辨（作者侧四变量）：吻合 {matches.Count} 组，对每一句主张结论一致：{string.Join(" | ", matches)}");
        // §11 第 10 条：重演从现实快照重建（含侦探第 2 日自己的行动）。
        var nonUnique = new List<string>();
        var bots = Bots.All.Where(b => SampleStrings("chapter2-checks", "replay-bots").Contains(b.Id)).ToArray();
        foreach (var b in bots)
        {
            var w = new Game();
            while (!(w.Day == Game.CaseDay && w.Slot == Slot.Morning)) w.RunSlot(b.Act(w));
            var tr = w.Dawn();
            var runs = Par(all.Select(v => (Func<(CaseTraces Traces, Game World)>)(() => w.ReplayCase(v))).ToArray());
            var m = all.Where((v, i) => w.Compare(runs[i].Traces, tr).All(l => !l.StartsWith(SampleText("chapter2-checks", "text-049")))).ToArray();
            var mw = all.Select((v, i) => (v, i)).Where(x => m.Contains(x.v)).Select(x => runs[x.i].World).ToArray();
            Console.WriteLine($"    {b.Name}：第 2 夜「{string.Join("；", w.NightTruth.DefaultIfEmpty(SampleText("chapter2-checks", "text-054")))}」，吻合 {m.Length} 组");
            if (!Agree(mw) && w.NightTruth.Any(t => t.StartsWith($"D{Game.CaseNight} 雀儿 夜里自取"))) nonUnique.Add(b.Name);
        }
        Check(nonUnique.Count == 0, SampleText("chapter2-checks", "text-055") + (nonUnique.Count > 0 ? SampleText("chapter2-checks", "text-056") + string.Join("、", nonUnique) + "）" : ""));
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
        Console.WriteLine(SampleText("chapter2-checks", "text-057"));
        var bot = Bots.All.First(b => b.Id == "investigate");
        var g = new Game();
        while (!(g.Day == Game.CaseDay && g.Slot == Slot.Night)) { Bots.Read(g); g.RunSlot(g.Day == Game.CaseDay && g.Slot == Slot.Evening ? new PlayerAction(Cmd.AskAbout) { Who = P.Bertrand } : bot.Act(g)); }
        var menu = g.ReplayMenu(Game.CaseNight);
        Console.WriteLine($"    菜单共 {menu.Count} 项，前几项：{string.Join("；", menu.Take(8).Select(m => m.Text))}");
        var rule = g.MoraNight;
        var hasRule = menu.Any(m => m.Rule is { } r && r.Who == P.Quer && r.Spec == rule.Id);
        var hasWhere = menu.Any(m => m.Where is { } w && w.Who == P.Quer && w.Where == Place.Logistics);
        var combos = new[] { (true, true), (true, false), (false, true), (false, false) };
        var outs = Par(combos.Select(c => (Func<List<string>>)(() =>
        {
            var (tr, w) = g.ReplayNight(Game.CaseNight, [new(P.Quer, rule.Id, c.Item1)], [new(P.Quer, Place.Logistics, c.Item2)]);
            var l = g.CompareTraces(Game.CaseNight, tr);
            if (c.Item2 && !w.Moves.Any(m => m.Day == Game.CaseNight && m.Who == P.Quer && m.To == Place.Logistics)) l.Insert(0, SampleText("chapter2-checks", "text-058"));
            return l;
        })).ToArray());
        for (var i = 0; i < combos.Length; i++)
            Console.WriteLine($"    雀儿{(combos[i].Item1 ? SampleText("chapter2-checks", "text-059") : SampleText("chapter2-checks", "text-060"))}茉拉的规矩、昨夜{(combos[i].Item2 ? SampleText("chapter2-checks", "text-061") : SampleText("chapter2-checks", "text-062"))}后勤院：{(outs[i].Any(l => l.StartsWith(SampleText("chapter2-checks", "text-049"))) ? string.Join(" / ", outs[i].Where(l => l.StartsWith(SampleText("chapter2-checks", "text-049")))) : SampleText("chapter2-checks", "text-063"))}");
        Check(hasRule && hasWhere, "通用重演菜单里自然出现“雀儿 认/不认 茉拉的规矩”与“雀儿 昨夜在/不在 后勤院”（未为案件单独写变量）");
        Check(outs[0].All(l => !l.StartsWith(SampleText("chapter2-checks", "text-049"))) && outs.Skip(1).All(o => o.Any(l => l.StartsWith(SampleText("chapter2-checks", "text-049")))), SampleText("chapter2-checks", "text-064"));
    }

    /// <summary>§11 第 1 条：读写闭环（用通用重演接口）。</summary>
    private static void ReadWriteLoop()
    {
        Console.WriteLine(SampleText("chapter2-checks", "text-065"));
        var bot = Bots.All.First(b => b.Id == "investigate");
        var g = new Game();
        while (!(g.Day == Game.LastDay && g.Slot == Slot.Morning)) { Bots.Read(g); g.RunSlot(g.Day == Game.CaseDay && g.Slot == Slot.Evening ? new PlayerAction(Cmd.Confront) { Plan = bot.Confront(g) } : bot.Act(g)); }
        g.HostExamine(SampleText("chapter2-checks", "text-066"));
        var spec = g.CaseRule!;
        Console.WriteLine($"    第 3 夜后侦探读到：{string.Join("；", g.KnownTraces(Game.CaseDay).Values.Select(t => t.Text))}");
        var outs = Par<List<string>>([() => g.CompareTraces(Game.CaseDay, g.ReplayNight(Game.CaseDay, [new(P.Mora, spec.Id, true)]).Traces), () => g.CompareTraces(Game.CaseDay, g.ReplayNight(Game.CaseDay, [new(P.Mora, spec.Id, false)]).Traces)]);
        Console.WriteLine(SampleText("chapter2-checks", "text-067") + string.Join(" / ", outs[0].Where(l => !l.StartsWith(SampleText("chapter2-checks", "text-068")))));
        Console.WriteLine(SampleText("chapter2-checks", "text-069") + string.Join(" / ", outs[1].Where(l => !l.StartsWith(SampleText("chapter2-checks", "text-068")))));
        Check(outs[0].All(l => !l.StartsWith(SampleText("chapter2-checks", "text-049"))) && outs[1].Any(l => l.StartsWith(SampleText("chapter2-checks", "text-049"))), SampleText("chapter2-checks", "text-070"));
    }

    private static void WritingVariants()
    {
        var g = new Game();
        var bot = Bots.All.First(b => b.Id == "investigate");
        while (!(g.Day == Game.LastDay && g.Slot == Slot.Morning)) { Bots.Read(g); g.RunSlot(g.Day == Game.CaseDay && g.Slot == Slot.Evening ? new PlayerAction(Cmd.Confront) { Plan = new(g.ValidConfrontPairs(), null, false) } : bot.Act(g)); }
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
        Console.WriteLine(SampleText("chapter2-checks", "text-071"));
        var g = new Game();
        g.RunSlot(null);
        var cards = Sample("chapter2-checks", "acceptance-cards").EnumerateArray().Select(row =>
        {
            var v = row.GetProperty("card").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            return (Card: new Card(v[0], v[1], v[2], v[3], v[4]), Label: row.GetProperty("label").GetString()!);
        }).ToArray();
        Console.WriteLine("  " + SampleText("chapter2-checks", "text-081").PadRight(12) + string.Join("", g.Npcs.Select(p => p.Name.PadRight(5))));
        foreach (var (card, label) in cards)
        {
            var spec = new RuleSpec { Id = -1, Text = label, Author = P.Det, Flags = card.Flags(g), Build = _ => [], Card = card, Target = card.Target, Manipulative = card.Target is not null };
            Console.WriteLine("  " + label.PadRight(12) + string.Join("", g.Npcs.Select(p => { var s = g.AcceptScore(p, spec, Channel.Face, null, out _); return $"{(s >= 2 ? "✓" : "·")}{s,-4}"; })));
        }
        foreach (var (card, label) in cards)
        {
            var spec = new RuleSpec { Id = -1, Text = label, Author = P.Det, Flags = card.Flags(g), Build = _ => [], Card = card, Target = card.Target, Manipulative = card.Target is not null };
            var vetoes = g.Npcs.Select(p => (p, v: g.Veto(p, spec))).Where(x => x.v is not null).Select(x => $"{x.p.Name}“{x.v}”").ToArray();
            Console.WriteLine($"  否决「{label}」：{(vetoes.Length == 0 ? SampleText("chapter2-checks", "text-082") : string.Join("；", vetoes))}");
        }
        var archive = cards.Where(c => c.Card.Behavior is >= 5 and <= 8).Select(c => new RuleSpec { Id = -1, Text = c.Label, Author = P.Det, Flags = c.Card.Flags(g), Build = _ => [], Card = c.Card }).ToArray();
        Check(archive.All(s => g.Npcs.Any(p => g.Veto(p, s) is not null) || s.Flags.HasFlag(RF.PactArchive)), SampleText("chapter2-checks", "text-083"));
    }

    // 5 -------------------------------------------------------------------
    /// <summary>§12 的可读性检查：守夜摘要、信任链、立场与接受分开、重开对质、私占可预读、界面不露内部编号。</summary>
    private static void Readability(List<BotResult> results, List<(string Bot, BotResult Pact, BotResult Seal, Ending Original, bool WasOstracized)> switched)
    {
        Console.WriteLine(SampleText("chapter2-checks", "text-084"));
        // 守夜摘要：最显著的不超过五条，其余合成一句。
        var g = new Game();
        var lens = new List<int>();
        var sample = new List<string>();
        while (!g.Ended)
        {
            PlayerAction? act = g.Slot == Slot.Night ? new(Cmd.Stakeout) { Place = g.Day % 2 == 0 ? Place.Logistics : Place.ArchiveFront } : null;
            g.RunSlot(act);
            if (act?.Cmd == Cmd.Stakeout)
            {
                var day = g.Day - 1;
                var rep = g.StakeoutReport(day, act.Place!.Value);
                lens.Add(rep.Count(l => !l.StartsWith(SampleText("chapter2-checks", "text-085"))));
                if (sample.Count == 0) sample = rep;
            }
            g.TakeFeed();
        }
        Console.WriteLine($"  守夜每夜给出的具体条数：{string.Join(" ", lens)}；第 2 夜守后勤院：{string.Join(" / ", sample)}");
        Check(lens.All(n => n <= 5), SampleText("chapter2-checks", "text-086"));

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
        Check(total > 0 && hit >= total * 0.6, SampleText("chapter2-checks", "text-087"));
        // 立场主要由亲见改变（§13 第 6 条）：同行即作证。被带去亲眼看过碗的沃德，在对质中按亲见计材料（基座裂纹已移至第三章，第三部分 §15）。
        var escort = results.FirstOrDefault(r => r.Strategy.Id == "escort");
        Check(escort is not null && escort.World.WardEscorts >= 10 && escort.QuerCleared, SampleText("chapter2-checks", "text-089"));
        var councils = results.Where(r => r.Ending != Ending.NightSeized).Select(r => (r.Name, max: r.World.CouncilChoice.Values.GroupBy(Game.FamilyOf).Max(g => g.Count()) + 1)).ToArray();
        Console.WriteLine(SampleText("chapter2-checks", "text-090") + string.Join(" ", councils.Select(c => $"{c.Name}{c.max}")));
        Check(councils.All(c => c.max <= 12), SampleText("chapter2-checks", "text-091"));

        // 不出示也能提议（第三部分 §7）：没有查案、什么都不出示的人，也能当众提出共同规则，由众人各自定。
        var mute = new Game();
        while (!mute.Ended) mute.RunSlot(mute.Day == Game.CaseDay && mute.Slot == Slot.Evening ? new PlayerAction(Cmd.Confront) { Plan = new([], null, true) } : null);
        Check(mute.ConfrontLog.Any(l => l.StartsWith(SampleText("chapter2-checks", "text-092"))), SampleText("chapter2-checks", "text-093"));

        // 私占可预读：被私占之前，前厅的脚印与停留逐夜增多。
        foreach (var r in results.Where(r => r.Ending == Ending.NightSeized).Take(3))
            Console.WriteLine($"    {r.Name}：第{r.World.SeizedDay}夜私占；之前各夜前厅（脚印/停留）：" + string.Join(" ", r.World.FrontStats.Where(kv => kv.Key < r.World.SeizedDay).OrderBy(kv => kv.Key).Select(kv => $"第{kv.Key}夜 {kv.Value.Prints}/{kv.Value.Stay:0}")));
        var seized = results.Where(r => r.Ending == Ending.NightSeized).ToArray();
        bool Readable(Game w) => w.FrontStats.TryGetValue(w.SeizedDay - 1, out var a) && a.Prints > 0;
        Check(seized.Length > 0 && seized.All(r => Readable(r.World)), $"私占可预读：{seized.Length} 局私占的前一夜，前厅已有脚印（{seized.Count(r => Readable(r.World))}/{seized.Length}）");

        // 重演不泄露身份（§13 第 11 条）：墨影只给身量，心声只在全部吻合后出现。
        var rg = new Game();
        var bot = Bots.All.First(x => x.Id == "investigate");
        while (!(rg.Day == Game.CaseDay && rg.Slot == Slot.Night)) { Bots.Read(rg); rg.RunSlot(rg.Day == Game.CaseDay && rg.Slot == Slot.Evening ? new PlayerAction(Cmd.AskAbout) { Who = P.Bertrand } : bot.Act(rg)); }
        var (t1, w1) = rg.ReplayNight(Game.CaseNight, [new(P.Quer, rg.MoraNight.Id, true)]);
        var c1 = rg.CompareTraces(Game.CaseNight, t1);
        var ink = Game.Ink(w1, Game.CaseNight, [P.Quer], c1.All(l => !l.StartsWith(SampleText("chapter2-checks", "text-049"))) && c1.Any(l => l.StartsWith(SampleText("chapter2-checks", "text-094"))));
        var (t2, w2) = rg.ReplayNight(Game.CaseNight, [new(P.Quer, rg.MoraNight.Id, false)]);
        var ink2 = Game.Ink(w2, Game.CaseNight, [P.Quer], false);
        Console.WriteLine(SampleText("chapter2-checks", "text-095") + string.Join(" / ", ink.Take(3).Select(l => l.Trim())));
        Check(ink.Concat(ink2).All(l => Enum.GetValues<P>().All(n => !l.Contains(Names.Of(n)))) && ink.Any(l => l.Contains(SampleText("chapter2-checks", "text-096"))) && ink2.All(l => !l.Contains(SampleText("chapter2-checks", "text-096"))),
            SampleText("chapter2-checks", "text-097"));
        var cands = rg.ReplayCandidates(Game.CaseNight);
        Console.WriteLine(SampleText("chapter2-checks", "text-098") + string.Join(" ", cands.Select(Names.Of)));
        Check(cands.Count is > 0 and < 15, SampleText("chapter2-checks", "text-099"));
        // 指控以亲见优先（§13 第 3 条）。
        var acc = results.SelectMany(r => r.World.Log.Where(l => l.Contains(SampleText("chapter2-checks", "text-100")) && l.Contains(SampleText("chapter2-checks", "text-101")))).FirstOrDefault();
        Console.WriteLine(SampleText("chapter2-checks", "text-102") + (acc ?? SampleText("chapter2-checks", "text-103")));
        Check(results.All(r => r.World.Log.Where(l => l.Contains(SampleText("chapter2-checks", "text-100")) && l.Contains(SampleText("chapter2-checks", "text-104"))).All(l => !l.Contains(SampleText("chapter2-checks", "text-105")) || l.Contains(SampleText("chapter2-checks", "text-106")) || l.Contains(SampleText("chapter2-checks", "text-107")) || l.Contains(SampleText("chapter2-checks", "text-108")))),
            SampleText("chapter2-checks", "text-109"));
        // 子提示符遇到文件结束：停下，不代为作答（§13 第 16 条）。
        var eof = new StringWriter();
        var g7 = new Game();
        var oneDay = string.Concat(Enumerable.Repeat(SampleText("chapter2-checks", "text-110"), 5)) + SampleText("chapter2-checks", "text-111");
        new ConsoleUI(g7, new StringReader(string.Concat(Enumerable.Repeat(oneDay, Game.CaseDay - Game.FirstDay + 1)) + SampleText("chapter2-checks", "text-112") + string.Concat(Enumerable.Repeat(SampleText("chapter2-checks", "text-110"), 4)) + SampleText("chapter2-checks", "text-111") + oneDay + oneDay), eof, echo: true).Run();
        var eofText = eof.ToString();
        Check((eofText.Contains(SampleText("chapter2-checks", "text-113")) || eofText.Contains(SampleText("chapter2-checks", "text-114"))) && !eofText.Contains(SampleText("chapter2-checks", "text-115")), SampleText("chapter2-checks", "text-116"));

        // 界面：不露内部编号、报错是玩家语言、调试默认关闭、拼规矩用选项文字。
        var script = SampleText("chapter2-checks", "console-route-1");
        var output = new StringWriter();
        new ConsoleUI(new Game(), new StringReader(script), output, echo: true).Run();
        var text = output.ToString();
        var leaks = Regex.Matches(text, SampleText("chapter2-checks", "text-117")).Select(m => m.Value).Distinct().ToArray();
        Console.WriteLine(SampleText("chapter2-checks", "text-118") + string.Join(" / ", text.Split('\n').Where(l => l.Contains(SampleText("chapter2-checks", "text-119")) || l.Contains(SampleText("chapter2-checks", "text-120")) || l.Contains(SampleText("chapter2-checks", "text-121")) || l.Contains(SampleText("chapter2-checks", "text-122")) || l.Contains(SampleText("chapter2-checks", "text-123"))).Select(l => l.Trim()).Distinct().Take(5)));
        Check(leaks.Length == 0, SampleText("chapter2-checks", "text-124") + (leaks.Length > 0 ? "：" + string.Join(" ", leaks) : ""));
        Check(text.Contains(SampleText("chapter2-checks", "text-125")) && !text.Contains(SampleText("chapter2-checks", "text-126")) && !text.Contains(SampleText("chapter2-checks", "text-127")), SampleText("chapter2-checks", "text-128"));
        Check(text.Contains(SampleText("chapter2-checks", "text-129")), SampleText("chapter2-checks", "text-130"));
    }

    // 6 -------------------------------------------------------------------
    private static void Performance(List<BotResult> results, Stopwatch total)
    {
        Console.WriteLine(SampleText("chapter2-checks", "text-131"));
        if (results.Count > 0)
        {
            var ms = results.Select(r => r.Ms).ToArray();
            Console.WriteLine($"  整章 + 议事一局（并行跑时的墙钟）：最短 {ms.Min()} ms，最长 {ms.Max()} ms，平均 {ms.Average():0} ms");
        }
        var g = new Game();
        double worstWall = 0, worstCpu = 0;
        var where = "";
        var bot = Bots.All.First(b => b.Id == "pact-loose");
        while (!g.Ended)
        {
            var label = $"D{g.Day}{Names.Of(g.Slot)}";
            var sw = Stopwatch.StartNew();
            var cpu = ThreadCpu();
            Bots.Read(g); g.RunSlot(g.Day == Game.CaseDay && g.Slot == Slot.Evening ? new PlayerAction(Cmd.Confront) { Plan = bot.Confront(g) } : bot.Act(g));
            var c = (ThreadCpu() - cpu).TotalMilliseconds;
            if (c > worstCpu) { worstCpu = c; where = label; }
            worstWall = Math.Max(worstWall, sw.Elapsed.TotalMilliseconds);
        }
        var heaviest = g.People.Max(p => p.World.History.Count);
        Console.WriteLine($"  单个时段（单线程，含夜间推演）最长 CPU {worstCpu:0} ms（{where}），最长墙钟 {worstWall:0} ms；单人认知历史最长 {heaviest} 条");
        Console.WriteLine(SampleText("chapter2-checks", "text-132"));
        Check(worstCpu < 300, SampleText("chapter2-checks", "text-133"));
        Check(total.Elapsed.TotalMinutes < 5, $"完整检查 < 5 分钟（到此 {total.Elapsed.TotalSeconds:0} s）");
    }
}
