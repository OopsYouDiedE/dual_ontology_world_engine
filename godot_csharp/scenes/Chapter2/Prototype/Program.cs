using static DualOntologyWorldEngine.TestSupport.FixtureData;
namespace DualOntologyWorldEngine.Chapter2.Prototype;

public static class Program
{
    private sealed class InputReadyReader(TextReader input) : TextReader
    {
        public override string? ReadLine()
        {
            Console.WriteLine("\u001einput-ready");
            return input.ReadLine();
        }
    }
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;
        WorldScriptDefinition? world = null;
        var worldArgument = Array.IndexOf(args, "--world-script");
        if (worldArgument >= 0)
        {
            if (worldArgument + 1 >= args.Length) throw new ArgumentException(SampleText("chapter2-program", "text-001"));
            world = WorldScript.Load(args[worldArgument + 1]);
            args = [.. args.Take(worldArgument), .. args.Skip(worldArgument + 2)];
        }
        Game CreateGame() => world?.CreateGame() ?? Chapter2Script.CreateWorld();
        var mode = args.Length > 0 ? args[0] : "play";
        switch (mode)
        {
            case "play":
            {
                // play [--debug] [脚本]：默认关闭调试命令；--debug 才能看作者侧日志与真实认知。
                var debug = args.Contains("--debug");
                var file = args.Skip(1).FirstOrDefault(a => a is not ("--debug" or "--input-ready" or "--room-scoped"));
                var script = file is not null ? File.ReadAllText(file) : null;
                TextReader input = script is not null ? new StringReader(script) : Console.In;
                if (args.Contains("--input-ready")) input = new InputReadyReader(input);
                new ConsoleUI(CreateGame(), input, Console.Out, echo: script is not null || Console.IsInputRedirected, debugAllowed: debug, roomScoped: args.Contains("--room-scoped")).Run();
                return 0;
            }
            case "host":
            {
                // 宿主路径冒烟：与 Godot 第二章同样的调用顺序（读不耗时，写/等待经 RunSlot）。
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var g = CreateGame();
                void Run(PlayerAction? act) { var t0 = sw.ElapsedMilliseconds; var d = g.Day; var s = g.Slot; g.RunSlot(act); Console.WriteLine($"-- D{d} {Names.Of(s)} ({sw.ElapsedMilliseconds - t0} ms)"); foreach (var l in g.TakeFeed()) Console.WriteLine("   " + l); }
                foreach (var step in Sample("chapter2-program", "host-opening").EnumerateArray())
                {
                    if (step.TryGetProperty("examine", out var thing)) Console.WriteLine(g.HostExamine(thing.GetString()!));
                    else if (step.TryGetProperty("wait", out _)) Run(null);
                    else Run(new PlayerAction(Enum.Parse<Cmd>(step.GetProperty("cmd").GetString()!))
                    { Place = step.TryGetProperty("place", out var place) ? Enum.Parse<Place>(place.GetString()!) : null });
                }
                foreach (var th in new[] { SampleText("chapter2-program", "text-003"), SampleText("chapter2-program", "text-004"), SampleText("chapter2-program", "text-005"), SampleText("chapter2-program", "text-006"), SampleText("chapter2-program", "text-002"), SampleText("chapter2-program", "text-007") }) Console.WriteLine($"[{th}] " + g.HostExamine(th));
                foreach (var who in new[] { P.Bertrand, P.Margaret, P.Mora, P.Quer }) Console.WriteLine($"[问{Names.Of(who)}] " + g.HostAsk(who));
                Console.WriteLine("known: " + string.Join(",", g.DetEvidence()));
                var t1 = sw.ElapsedMilliseconds;
                var (tr, replayWorld) = g.ReplayCase(CaseVars.Truth);
                Console.WriteLine($"replay {sw.ElapsedMilliseconds - t1} ms; match={tr == g.CaseActual}; steps={Game.InkSteps(replayWorld, Game.CaseNight).Count}");
                foreach (var st in Game.InkSteps(replayWorld, Game.CaseNight).Take(12)) Console.WriteLine($"   {st.Time:0.0} {Names.Of(st.Who)} {st.From}->{st.To} {st.Act}");
                Run(new PlayerAction(Cmd.Escort) { Who = P.Ward, Place = Place.Logistics, Thing = SampleText("chapter2-program", "text-002") });
                Run(new PlayerAction(Cmd.Collect));
                Run(new PlayerAction(Cmd.Stay) { Place = Place.Hall });
                var pairs = g.ValidConfrontPairs().ToList();
                foreach (var (c, e) in pairs) Console.WriteLine($"pair {Game.Claims[c]} / {Game.EvidenceNames[e]}");
                Run(new PlayerAction(Cmd.Confront) { Plan = new Game.ConfrontPlan(pairs, Game.CaseWordings[0].Card, true) });
                Run(null);
                Console.WriteLine($"ended={g.Ended} day={g.Day}");
                g.CardSpec(Game.ArchiveWordings[0].Card, P.Det);
                var plans = g.ArchivePlans().ToList();
                g.Council(plans.FindIndex(s => s.Author == P.Det));
                foreach (var l in g.CouncilLog) Console.WriteLine("   " + l);
                Console.WriteLine($"total {sw.ElapsedMilliseconds} ms");
                return 0;
            }
            case "idle":
            {
                var g = CreateGame();
                while (!g.Ended) g.RunSlot(null);
                g.Council();
                foreach (var l in g.Log) Console.WriteLine(l);
                foreach (var l in g.CouncilLog) Console.WriteLine(l);
                Console.WriteLine(Report.ChapterEnd(g));
                Console.WriteLine(SampleText("chapter2-program", "text-008") + string.Join(" ", g.People.Select(p => $"{p.Name}{p.MaxHunger}")));
                return 0;
            }
            case "diary":
            {
                var g = CreateGame();
                var until = args.Length > 1 ? int.Parse(args[1]) : 3;
                while (!g.Ended && g.Day < until) g.RunSlot(null);
                foreach (var p in g.People.Skip(1)) { Console.WriteLine("== " + p.Name); foreach (var d in p.Diary) Console.WriteLine("  " + d); }
                return 0;
            }
            case "bots": return Bots.RunAll(args.Skip(1).ToArray());
            case "trace":
            {
                var first = Bots.All[int.Parse(args[1])];
                var then = args.Length > 3 && args[2] == "then" ? Bots.All[int.Parse(args[3])] : null;
                if (then is not null) args = [.. args.Take(2), .. args.Skip(4)];
                Bot bot = then is null ? first : new() { Id = first.Id + ":" + then.Id, Name = first.Name + "→" + then.Name, Act = g => (g.Day >= Game.CaseDay ? then : first).Act(g), Confront = g => then.Confront(g), Back = then.Back };
                var g = CreateGame();
                while (!g.Ended)
                {
                    bot.Free?.Invoke(g);
                    var act = g.Day == Game.CaseDay && g.Slot == Slot.Evening ? new PlayerAction(Cmd.Confront) { Plan = bot.Confront(g) } : bot.Act(g);
                    Console.WriteLine($"【D{g.Day} {Names.Of(g.Slot)}】{act?.Describe() ?? SampleText("chapter2-program", "text-009")}");
                    g.RunSlot(act);
                    foreach (var f in g.TakeFeed()) Console.WriteLine("   " + f);
                }
                g.Council(bot.Back(g));
                foreach (var l in g.CouncilLog) Console.WriteLine("   " + l);
                if (args.Length > 2) foreach (var l in g.Log.Concat(g.DebugLog).Where(l => l.Contains(args[2]))) Console.WriteLine("  [log] " + l);
                Console.WriteLine(Report.ChapterEnd(g));
                return 0;
            }
            case "prof": { Prof.Enabled = true; var r = Bots.Play(Bots.All[int.Parse(args[1])]); Console.WriteLine($"{r.Name} {r.Ms} ms"); Prof.Print(); return 0; }
            case "slots":
            {
                var bot = Bots.All[args.Length > 1 ? int.Parse(args[1]) : 0];
                var g = CreateGame();
                var line = new System.Text.StringBuilder();
                while (!g.Ended)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var cpu = Checks.ThreadCpu();
                    bot.Free?.Invoke(g);
                    g.RunSlot(g.Day == Game.CaseDay && g.Slot == Slot.Evening ? new PlayerAction(Cmd.Confront) { Plan = bot.Confront(g) } : bot.Act(g));
                    line.Append($"{sw.ElapsedMilliseconds}/{(Checks.ThreadCpu() - cpu).TotalMilliseconds:0} ");
                    if (g.Slot == Slot.Morning) { Console.WriteLine($"D{g.Day - 1}: {line}"); line.Clear(); }
                }
                return 0;
            }
            case "emerge":
            {
                // emerge 脚本：按脚本玩一局（不打印），再列出这一局的涌现链与玩家读到的情况（报告用）
                var g = CreateGame();
                new ConsoleUI(g, new StringReader(File.ReadAllText(args[1])), TextWriter.Null, echo: false).Run();
                var chains = g.Chains();
                Console.WriteLine($"结局 {Game.EndingText(g.Result)}；人物动作 {g.Events.Count(e => !e.Fixed && e.Who != P.Det)}；涌现链 {chains.Count}（不经侦探的 {chains.Count(c => !c.ByDetective)}；玩家读得到的 {chains.Count(g.Visible)}）");
                Console.WriteLine(SampleText("chapter2-program", "text-010") + string.Join(" ", Enumerable.Range(2, 5).Select(d => chains.Count(c => c.Day == d && g.Visible(c)))));
                foreach (var c in Emergence.Interesting(chains, g).GroupBy(Emergence.Trunk).Select(x => x.First()).Take(args.Length > 2 ? int.Parse(args[2]) : 8)) Console.WriteLine("  · " + g.Describe(c));
                return 0;
            }
            case "events":
            {
                // events 机器人号：按该策略跑完一局，逐条列出人物（非侦探）的非作者动作，供对比不同打法下人物做了什么
                var r = Bots.Play(Bots.All[int.Parse(args[1])]);
                foreach (var e in r.World.Events.Where(e => !e.Fixed && e.Who != P.Det)) Console.WriteLine($"D{e.Day} {Names.Of(e.Slot)} {e.Short}");
                return 0;
            }
            case "minds":
            {
                // minds 机器人号：跑完一局，列出每人议事时的欲望、钩与是否想开库（调试用）
                var r = Bots.Play(Bots.All[int.Parse(args[1])]);
                foreach (var p in r.World.Npcs) Console.WriteLine($"{p.Name} 欲望 {p.Desire} 钩 {p.Hooks} 开库之欲 {p.Pro.DesireFlags.HasFlag(RF.OpenArchive)} 戒备铜口 {p.Pro.DemonPrejudice} 疑它 {p.DemonDoubt} 低语 {p.Whispered}");
                return 0;
            }
            case "infer":
            {
                // infer 机器人号：按该策略走到翌日傍晚之前（含读），打印侦探自己的推断与真相假设的对照（调试用）
                var bot = Bots.All[int.Parse(args[1])];
                var g = CreateGame();
                while (!(g.Day == Game.CaseDay && g.Slot == Slot.Evening)) { if (bot.Reads) Bots.Read(g); g.RunSlot(bot.Act(g)); }
                var (fits, claims) = g.DetectiveInference();
                Console.WriteLine($"已查痕迹 {string.Join(",", g.DetEvidence())}；吻合 {fits.Count} 组：{string.Join(" | ", fits)}；说得出口的陈述 {string.Join(",", claims)}");
                foreach (var l in g.Compare(g.ReplayCase(CaseVars.Truth).Traces, g.CaseActual!, g.DetEvidence())) Console.WriteLine(SampleText("chapter2-program", "text-011") + l);
                return 0;
            }
            case "chains":
            {
                var r = Bots.Play(Bots.All[args.Length > 1 ? int.Parse(args[1]) : 0]);
                var g = r.World;
                var chains = g.Chains();
                Console.WriteLine($"{r.Name}：结局 {Game.EndingText(r.Ending)}；事件 {g.Events.Count}，涌现链 {chains.Count}（玩家读得到的 {chains.Count(g.Visible)}）");
                Console.WriteLine(SampleText("chapter2-program", "text-012") + string.Join(" ", g.Events.Where(e => !e.Fixed).GroupBy(e => e.Job?.ToString() ?? "说/应").OrderByDescending(x => x.Count()).Select(x => $"{x.Key}{x.Count()}")));
                foreach (var e in g.Events.Where(e => e.Job is Job.AskOpen or Job.Seize || e.Text.StartsWith(SampleText("chapter2-program", "text-013")) || e.Text.Contains(SampleText("chapter2-program", "text-014")))) Console.WriteLine("    " + e.Short + SampleText("chapter2-program", "text-015") + string.Join(",", e.Causes));
                foreach (var d in Enumerable.Range(2, 5)) Console.WriteLine($"  第{d}日：涌现链 {chains.Count(c => c.Day == d)}，玩家读得到 {chains.Count(c => c.Day == d && g.Visible(c))}");
                foreach (var c in Emergence.Interesting(chains, g).Take(args.Length > 2 ? int.Parse(args[2]) : 12)) Console.WriteLine("  · " + g.Describe(c));
                return 0;
            }
            case "probe":
            {
                // probe 机器人号 日 时段号 人名：推演到那个时段开头，打印此人的决策评估（调试用）
                var bot = Bots.All[int.Parse(args[1])];
                var g = CreateGame();
                while (!g.Ended && !(g.Day == int.Parse(args[2]) && (int)g.Slot == int.Parse(args[3])))
                { bot.Free?.Invoke(g); g.RunSlot(g.Day == Game.CaseDay && g.Slot == Slot.Evening ? new PlayerAction(Cmd.Confront) { Plan = bot.Confront(g) } : bot.Act(g)); }
                g.ProbeStart();
                var p = g[Names.PersonAlias[args[4]]];
                Console.WriteLine($"{p.Name} 在 {Names.Of(g.Where(p))}；习惯规则 {p.Habit.Rules.Length} 条（说话类 {p.Habit.Rules.Count(r => r.Effect is DualOntologyWorldEngine.Core.Effect.Sequence { Items: [DualOntologyWorldEngine.Core.Effect.Say, ..] })}）；同处：{string.Join("、", g.People.Where(o => o != p && g.Where(o) == g.Where(p)).Select(o => o.Name))}");
                Console.WriteLine(SampleText("chapter2-program", "text-016") + string.Join(" ", p.M[g.Clock].Tags.Where(t => t.Value != 0).Select(t => $"{t.Key.Name}={t.Value}")) + SampleText("chapter2-program", "text-017") + string.Join(" | ", p.Habit.Rules.Where(r => g.JobOf(p, r.Effect) == Job.Sleep).Select(r => r.Condition.ToString()[..Math.Min(120, r.Condition.ToString().Length)])));
                foreach (var o in g.People.Where(o => o != p && g.Where(o) == g.Where(p)))
                    Console.WriteLine($"  心中的{o.Name}：Trust={p.M[o.Body].Tags.GetValueOrDefault(DualOntologyWorldEngine.Core.Tags.Trust)} Picks={p.M[o.Body].Tags.GetValueOrDefault(T.Picks)}；在{p.M[o.Body].Parent?.Name}；我在{p.Self.Parent?.Name}");
                foreach (var r in p.Habit.Rules.Where(r => r.Effect is DualOntologyWorldEngine.Core.Effect.Sequence { Items: [DualOntologyWorldEngine.Core.Effect.Say, ..] }))
                    foreach (var o in g.People.Where(o => o != p && g.Where(o) == g.Where(p)))
                    {
                        var ctx = new DualOntologyWorldEngine.Core.ActionContext(p.Self, p.M[o.Body], null, p.Self.Parent);
                        var parts = r.Condition is DualOntologyWorldEngine.Core.Condition.All all ? all.Items.Select(c => DualOntologyWorldEngine.Core.Ast.Evaluate(c, ctx).ToString()) : [DualOntologyWorldEngine.Core.Ast.Evaluate(r.Condition, ctx).ToString()];
                        Console.WriteLine($"  规则（对{o.Name}）条件：{string.Join(" ", parts)} 接受={r.Acceptance}");
                    }
                foreach (var a in DualOntologyWorldEngine.Core.DecisionSystem.Assess(p.World, p.Self).Take(8))
                    Console.WriteLine($"  {(a.Forbidden ? SampleText("chapter2-program", "text-018") : "")}{g.JobOf(p, a.Decision.Effect)?.ToString() ?? a.Decision.Effect.GetType().Name} {a.Decision.Score}：{string.Join("；", a.Reasons)}");
                return 0;
            }
            case "check": return Checks.RunAll(args.Skip(1).ToArray());
            default: Console.WriteLine(SampleText("chapter2-program", "text-019")); return 1;
        }
    }
}
