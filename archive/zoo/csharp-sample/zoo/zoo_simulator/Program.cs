using System.Globalization;
using System.Text.Json;
using DualOntologyWorldEngine.Examples.ZooSimulator;
try
{
    string Arg(string key,string fallback) {var index=Array.IndexOf(args,key);return index>=0?args[index+1]:fallback;}
    var seed=int.Parse(Arg("--seed","7"));var count=int.Parse(Arg("--visitors","120"));
    var start=double.Parse(Arg("--start-minute","480"),CultureInfo.InvariantCulture);
    var duration=double.Parse(Arg("--time","480"),CultureInfo.InvariantCulture);
    var steps=int.Parse(Arg("--max-steps","200000"));
    if(!double.IsFinite(duration)||duration<0)throw new ArgumentException("模拟时长必须为有限非负数。");
    using var simulation=new ZooSimulation(seed,count,start,Arg("--rules","standard"));
    var w=simulation.World;
    if(args.Contains("--catalog")){foreach(var c in w.Rules.Clauses.Values)Console.WriteLine($"{c.Key} {c.Original}\n{c.Script}");return 0;}
    if(args.Contains("--rule")){w.Rules.Publish("Custom.1",Arg("--rule",""));w.Broadcast("Custom.1");}
    if(args.Contains("--withdraw"))foreach(var key in Arg("--withdraw","").Split(',',StringSplitOptions.RemoveEmptyEntries))w.Withdraw(key,true);
    var executed=w.Run(start+duration,steps);
    var report=new { w.Now, Completed=w.Now>=start+duration, Steps=executed,w.PeakVisitors,Visitors=w.InPark,
        w.DailyAdmissions,w.Anomalies.BlackBirths,RedReturns=w.People.Sum(p=>p.Returns),
        Actions=w.People.SelectMany(p=>p.Soul.World.History).Where(h=>h.Details.StartsWith("交互完成:")).GroupBy(h=>h.Details.Split(':')[1]).ToDictionary(g=>g.Key,g=>g.Count()),
        Outcomes=w.People.GroupBy(p=>p.Outcome).ToDictionary(g=>g.Key,g=>g.Count()),
        People=w.People.Select(p=>new {Name=p.Body.TrueName,Room=p.Room?.TrueName,p.Outcome,p.Job,p.TrueSpecies,p.ItsClassification,
            Human=p.Self.Value(Z.Human),KnownRoom=p.Self.Parent?.Name,p.CurrentAction,Rules=p.Books.Count,Trace=p.DecisionTrace.ToArray()}).ToArray(),
        Events=w.Events.ToArray()};
    Console.WriteLine($"时间 {w.Now:F1} 分钟；执行 {executed} 步；峰值游客 {w.PeakVisitors}；在园 {w.InPark}；完成 {report.Completed}");
    foreach(var outcome in report.Outcomes)Console.WriteLine($"{outcome.Key}：{outcome.Value}");
    Console.WriteLine($"新增黑衣 {report.BlackBirths}；红衣返岗 {report.RedReturns}；入园：{string.Join("；",w.DailyAdmissions.Select(d=>$"第{d.Key+1}天 {d.Value}"))}");
    Console.WriteLine("交互："+string.Join("；",report.Actions.Select(a=>$"{a.Key} {a.Value}")));
    if(args.Contains("--verbose"))foreach(var e in w.Events)Console.WriteLine(e);
    if(args.Contains("--report"))File.WriteAllText(Arg("--report","zoo-run.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
    return report.Completed?0:2;
}
catch(Exception e){Console.Error.WriteLine(e);return 1;}
