using System.Globalization;
using System.Text.Json;

namespace DualOntologyWorldEngine.Examples.ZooSimulator;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            int seed=42, visitors=4, maxSteps=2000000;
            double duration=180, startMinute=540;
            string preset="standard";
            string? report=null;
            bool verbose=false, catalog=false;
            bool interactive = args.Length == 0;
            for (int n=0;n<args.Length;n++)
            {
                string Value()=>++n<args.Length ? args[n] : throw new ArgumentException("参数缺少值");
                switch (args[n])
                {
                    case "--interactive": case "-i": interactive=true; break;
                    case "--seed": seed=int.Parse(Value(),CultureInfo.InvariantCulture); break;
                    case "--time": duration=double.Parse(Value(),CultureInfo.InvariantCulture); break;
                    case "--start-minute": startMinute=double.Parse(Value(),CultureInfo.InvariantCulture); break;
                    case "--visitors": visitors=int.Parse(Value(),CultureInfo.InvariantCulture); break;
                    case "--max-steps": maxSteps=int.Parse(Value(),CultureInfo.InvariantCulture); break;
                    case "--rules": preset=Value(); break;
                    case "--report": report=Value(); break;
                    case "--verbose": verbose=true; break;
                    case "--catalog": catalog=true; break;
                    default: throw new ArgumentException($"未知参数：{args[n]}");
                }
            }
            if (interactive)
            {
                var ui = new DashboardUi(seed, visitors, startMinute, preset);
                ui.Run();
                return 0;
            }
            if (!double.IsFinite(duration) || duration<0 || !double.IsFinite(startMinute) || startMinute<0 || visitors<0 || maxSteps<1)
                throw new ArgumentException("时间与人数必须非负，时间必须有限，步数上限必须为正。");
            using var simulation=new ZooSimulation(seed,visitors,startMinute,preset);
            var w=simulation.World;
            var rules=simulation.Rules;
            if (catalog)
            {
                foreach (var clause in rules.Clauses) Console.WriteLine($"{clause.Key} [{(clause.Advice==null ? "声明" : "行动建议")}] {clause.Text}");
                return 0;
            }
            var mechanisms=simulation.Anomalies;
            if (verbose) w.Session.Runtime.Executed+=(i,r)=>
            {
                if (r.Result?.Succeeded==true && i.Effect.Op!="evolve") Console.WriteLine($"[{w.Session.Timeline.Now:F1}] {i.Actor.Name}：{i.Label}");
            };
            int steps=w.Run(duration,maxSteps);
            bool truncated=w.Session.Timeline.Queue.Any(t=>t.Time<=duration);
            Console.WriteLine($"种子 {seed}；初始文书 {preset}；{rules.Clauses.Count} 条原文；时刻 {w.Session.Timeline.Now:F1} 分钟；结算 {steps} 步。");
            foreach (var c in w.Visitors)
                Console.WriteLine($"{c.Name}：{w.Outcome(c)}，位置 {c.CurrentScene?.Name ?? "无"}，生命 {c.Get<double>("health"):F2}，侵蚀 {c.Get<double>("erosion"):F2}，关注 {c.Get<double>("attention"):F2}。");
            Console.WriteLine($"因果变化 {mechanisms.Records.Count} 条。结局由当前状态读取，不为遵守或违反文书赋值。");
            if (report!=null)
            {
                var payload=new
                {
                    seed,preset,startMinute,duration,steps,truncated,
                    visitors=w.Visitors.Select(c=>new { c.Name,scene=c.CurrentScene?.Name,outcome=w.Outcome(c),health=c.Get<double>("health"),erosion=c.Get<double>("erosion"),attention=c.Get<double>("attention") }),
                    changes=mechanisms.Records.Select(r=>new { r.Time,actor=r.Actor.Name,scene=r.Scene.Name,r.Mechanism,r.Attention,r.Erosion,r.Outcome })
                };
                File.WriteAllText(report,JsonSerializer.Serialize(payload,new JsonSerializerOptions { WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            }
            if (truncated) Console.Error.WriteLine("达到步数上限，推演未完成。");
            return truncated ? 2 : 0;
        }
        catch (Exception e) when (e is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }
}
