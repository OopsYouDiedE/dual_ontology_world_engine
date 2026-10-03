namespace DualOntologyWorldEngine.Chapter2.Prototype;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;
        var mode = args.Length > 0 ? args[0] : "play";
        switch (mode)
        {
            case "play":
            {
                var script = args.Length > 1 ? File.ReadAllText(args[1]) : null;
                var input = script is not null ? new StringReader(script) : Console.In;
                new ConsoleUI(new Game(), input, Console.Out, echo: script is not null || Console.IsInputRedirected).Run();
                return 0;
            }
            case "idle":
            {
                var g = new Game();
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
                var g = new Game();
                var until = args.Length > 1 ? int.Parse(args[1]) : 3;
                while (!g.Ended && g.Day < until) g.RunSlot(null);
                foreach (var p in g.People.Skip(1)) { Console.WriteLine("== " + p.Name); foreach (var d in p.Diary) Console.WriteLine("  " + d); }
                return 0;
            }
            case "bots": return Bots.RunAll(args.Skip(1).ToArray());
            case "trace":
            {
                var bot = Bots.All[int.Parse(args[1])];
                var g = new Game();
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
            case "prof": { var r = Bots.Play(Bots.All[int.Parse(args[1])]); Console.WriteLine($"{r.Name} {r.Ms} ms"); Prof.Print(); return 0; }
            case "check": return Checks.RunAll(args.Skip(1).ToArray());
            default: Console.WriteLine("用法：play [脚本] | idle | diary [日] | bots | check"); return 1;
        }
    }
}
