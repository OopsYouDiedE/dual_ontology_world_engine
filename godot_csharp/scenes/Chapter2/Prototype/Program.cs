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
            if (worldArgument + 1 >= args.Length) throw new ArgumentException("--world-script 需要已编译的脚本 DLL 路径。");
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
            case "idle":
            {
                var g = CreateGame();
                while (!g.Ended) g.RunSlot(null);
                g.Council();
                foreach (var l in g.Log) Console.WriteLine(l);
                foreach (var l in g.CouncilLog) Console.WriteLine(l);
                Console.WriteLine(Report.ChapterEnd(g));
                Console.WriteLine("最长连饿：" + string.Join(" ", g.People.Select(p => $"{p.Name}{p.MaxHunger}")));
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
                Bot bot = then is null ? first : new() { Name = first.Name + "→" + then.Name, Act = g => (g.Day >= 4 ? then : first).Act(g), Confront = g => then.Confront(g), Back = then.Back };
                var g = CreateGame();
                while (!g.Ended)
                {
                    bot.Free?.Invoke(g);
                    var act = g.Day == 4 && g.Slot == Slot.Morning ? new PlayerAction(Cmd.Confront) { Plan = bot.Confront(g) } : bot.Act(g);
                    Console.WriteLine($"【D{g.Day} {Names.Of(g.Slot)}】{act?.Describe() ?? "歇着"}");
                    g.RunSlot(act);
                    foreach (var f in g.Feed) Console.WriteLine("   " + f);
                    g.Feed.Clear();
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
                    g.RunSlot(g.Day == 4 && g.Slot == Slot.Morning ? new PlayerAction(Cmd.Confront) { Plan = bot.Confront(g) } : bot.Act(g));
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
                Console.WriteLine("每日玩家读得到的涌现链（第2—6日）：" + string.Join(" ", Enumerable.Range(2, 5).Select(d => chains.Count(c => c.Day == d && g.Visible(c)))));
                foreach (var c in Emergence.Interesting(chains, g).GroupBy(Emergence.Trunk).Select(x => x.First()).Take(args.Length > 2 ? int.Parse(args[2]) : 8)) Console.WriteLine("  · " + g.Describe(c));
                return 0;
            }
            case "chains":
            {
                var r = Bots.Play(Bots.All[args.Length > 1 ? int.Parse(args[1]) : 0]);
                var g = r.World;
                var chains = g.Chains();
                Console.WriteLine($"{r.Name}：结局 {Game.EndingText(r.Ending)}；事件 {g.Events.Count}，涌现链 {chains.Count}（玩家读得到的 {chains.Count(g.Visible)}）");
                Console.WriteLine("  动作：" + string.Join(" ", g.Events.Where(e => !e.Fixed).GroupBy(e => e.Job?.ToString() ?? "说/应").OrderByDescending(x => x.Count()).Select(x => $"{x.Key}{x.Count()}")));
                foreach (var e in g.Events.Where(e => e.Job is Job.AskOpen or Job.Seize || e.Text.StartsWith("答应") || e.Text.Contains("名录库"))) Console.WriteLine("    " + e.Short + " 因：" + string.Join(",", e.Causes));
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
                { bot.Free?.Invoke(g); g.RunSlot(g.Day == 4 && g.Slot == Slot.Morning ? new PlayerAction(Cmd.Confront) { Plan = bot.Confront(g) } : bot.Act(g)); }
                g.ProbeStart();
                var p = g[Names.PersonAlias[args[4]]];
                Console.WriteLine($"{p.Name} 在 {Names.Of(g.Where(p))}；习惯规则 {p.Habit.Rules.Length} 条（说话类 {p.Habit.Rules.Count(r => r.Effect is DualOntologyWorldEngine.Core.Effect.Sequence { Items: [DualOntologyWorldEngine.Core.Effect.Say, ..] })}）；同处：{string.Join("、", g.People.Where(o => o != p && g.Where(o) == g.Where(p)).Select(o => o.Name))}");
                Console.WriteLine("  心中的钟：" + string.Join(" ", p.M[g.Clock].Tags.Where(t => t.Value != 0).Select(t => $"{t.Key.Name}={t.Value}")) + "；规则：" + string.Join(" | ", p.Habit.Rules.Where(r => g.JobOf(p, r.Effect) == Job.Sleep).Select(r => r.Condition.ToString()[..Math.Min(120, r.Condition.ToString().Length)])));
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
                    Console.WriteLine($"  {(a.Forbidden ? "[禁]" : "")}{g.JobOf(p, a.Decision.Effect)?.ToString() ?? a.Decision.Effect.GetType().Name} {a.Decision.Score}：{string.Join("；", a.Reasons)}");
                return 0;
            }
            case "check": return Checks.RunAll(args.Skip(1).ToArray());
            default: Console.WriteLine("用法：play [--debug] [脚本] | bots [-v] | check [1-5] | trace 机器人号 | slots | idle | diary [日] | prof 机器人号"); return 1;
        }
    }
}
