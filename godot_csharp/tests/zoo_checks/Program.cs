using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Timeline;
using DualOntologyWorldEngine.Examples.ZooBaseline;

var failures = 0;
Run("无异常对照：20个种子均能正常游览", () =>
{
    for (var seed = 0; seed < 20; seed++)
    {
        using var zoo = new ZooBaseline(seed, visitorCount: 6);
        var homes = zoo.Animals.ToDictionary(a => a, a => a.Parent);
        zoo.Start();
        zoo.Run(500, 50_000);
        Check(zoo.Visitors.All(v => ReferenceEquals(v.CurrentScene, zoo.Exit)), $"游客未离园 seed={seed}");
        Check(zoo.Animals.All(a => ReferenceEquals(a.Parent, homes[a])), "无异常时动物越区");
        Check(zoo.CausalTrace.Count > 30, "场景未实际运行");
        Check(zoo.CausalTrace.All(ev => ev.IsSucceeded()), "对照出现结算失败");
        Check(zoo.Session.Runtime.Patches.Count == 0 && zoo.Visitors.Concat(zoo.Staff).All(a => a.Mind!.Beliefs.Count == 0), "意外装载守则");
        VerifyMovement(zoo);
    }
});
Run("无守则保留异常：持续接触导致失踪且可追溯", () =>
{
    using var zoo = new ZooBaseline(); using var anomaly = new ZooAnomalies(zoo);
    var actor = zoo.Visitors[0];
    Move(zoo, actor, zoo.Walkway); Move(zoo, actor, zoo.RabbitArea);
    Check(anomaly.AttentionOf(actor) == 1, "没有因接触产生关注");
    Wait(zoo, actor); Wait(zoo, actor);
    Check(actor.Parent == null && anomaly.AttentionOf(actor) == 3, "持续暴露未产生后果");
    Check(anomaly.Trace.Count == 3 && anomaly.Trace.All(e => e.TriggerEvent != null && zoo.CausalTrace.Contains(e.TriggerEvent)), "异常轨迹缺失真实事件");
    Check(zoo.Session.Timeline.Queue.Count == 0, "终局仍有行动槽");
    Check(actor.Mind!.Beliefs.Count == 0, "角色获知了未发放守则");
});
Run("认知推演不泄漏异常且客观准备无副作用", () =>
{
    using var zoo = new ZooBaseline(); using var anomaly = new ZooAnomalies(zoo);
    var actor = zoo.Visitors[0]; Move(zoo, actor, zoo.Walkway);
    var action = MoveIntent(zoo, actor, zoo.RabbitArea);
    for (var i = 0; i < 100; i++)
    {
        var subjective = zoo.Session.Runtime.Prepare(action, new StateView(actor.Mind));
        Check(subjective.Changes.All(c => ReferenceEquals(c.Subject, actor)), "认知推演窥见异常账簿");
        var objective = zoo.Session.Runtime.Prepare(action);
        Check(objective.Succeeded && anomaly.AttentionOf(actor) == 0 && anomaly.Trace.Count == 0, "准备修改真实状态");
    }
    Execute(zoo, action);
    Check(anomaly.AttentionOf(actor) == 1, "真实执行未触发异常");
});
Run("关闭异常的相同接触轨迹无异常后果", () =>
{
    using var zoo = new ZooBaseline(); using var anomaly = new ZooAnomalies(zoo, new() { Enable = false });
    var actor = zoo.Visitors[0]; Move(zoo, actor, zoo.Walkway); Move(zoo, actor, zoo.RabbitArea);
    for (var i = 0; i < 100; i++) Wait(zoo, actor);
    Check(actor.CurrentScene == zoo.RabbitArea && anomaly.AttentionOf(actor) == 0 && anomaly.Trace.Count == 0, "关闭异常后仍有后果");
});
Run("狮区保护干预改变相同路径的结局", () =>
{
    foreach (var protection in new[] { false, true })
    {
        using var zoo = new ZooBaseline();
        using var anomaly = new ZooAnomalies(zoo, new() { ProtectAtLionArea = protection });
        var actor = zoo.Visitors[0]; Move(zoo, actor, zoo.Walkway); Move(zoo, actor, zoo.RabbitArea); Wait(zoo, actor);
        Move(zoo, actor, zoo.Walkway); Move(zoo, actor, zoo.LionArea);
        Check(anomaly.AttentionOf(actor) == (protection ? 0 : 2), "保护没有改变关注");
        Move(zoo, actor, zoo.Walkway); Move(zoo, actor, zoo.ElephantArea);
        Check((actor.Parent != null) == protection, "结局不受保护条件影响");
    }
});
Run("休息点只有黑暗且封闭才提供保护", () =>
{
    foreach (var dark in new[] { false, true }) foreach (var enclosed in new[] { false, true })
    {
        using var zoo = new ZooBaseline(); using var anomaly = new ZooAnomalies(zoo);
        zoo.RestPoint.Set("dark", dark); zoo.RestPoint.Set("enclosed", enclosed);
        var actor = zoo.Visitors[0]; Move(zoo, actor, zoo.Walkway); Move(zoo, actor, zoo.RabbitArea);
        Move(zoo, actor, zoo.Walkway); Move(zoo, actor, zoo.RestPoint);
        Check(anomaly.AttentionOf(actor) == (dark && enclosed ? 0 : 1), "保护条件不正确");
    }
});
Run("未获守则的员工也受到异常影响", () =>
{
    using var zoo = new ZooBaseline(); using var anomaly = new ZooAnomalies(zoo);
    var actor = zoo.Staff[0]; Move(zoo, actor, zoo.Walkway); Move(zoo, actor, zoo.RabbitArea);
    Wait(zoo, actor); Wait(zoo, actor);
    Check(actor.Parent == null && anomaly.AttentionOf(actor) == 3, "员工被错误豁免异常");
});
Run("同种子自主场景可重放", () =>
{
    string Replay()
    {
        using var zoo = new ZooBaseline(42, 6); using var anomaly = new ZooAnomalies(zoo);
        zoo.Start(); zoo.Run(500, 50_000);
        return string.Join('\n', zoo.CausalTrace.Select(e => $"{e.Get<double>("time")}:{e.GetIntent()!.Actor.Name}:{e.GetIntent()!.Label}"))
            + string.Join('\n', anomaly.Trace.Select(e => $"{e.Time}:{e.Actor.Name}:{e.Phase}:{e.Attention}"));
    }
    Check(Replay() == Replay(), "同种子轨迹不一致");
});
Run("同一世界连续30批游客的长程无守则场景", () =>
{
    using var zoo = new ZooBaseline(7, 6); using var anomaly = new ZooAnomalies(zoo);
    zoo.Start();
    for (var day = 1; day <= 30; day++)
    {
        if (day > 1) zoo.AddVisitors(6);
        zoo.Run(day * 200, 50_000);
        Check(zoo.Session.Timeline.Now == day * 200, "无法完成本批运行");
        Check(zoo.Visitors.All(v => v.Parent == null || v.CurrentScene == zoo.Exit), "游客长期卡住");
        Check(zoo.Session.Timeline.Queue.Count <= zoo.Staff.Count, "终局游客行动槽累积");
        var conserved = zoo.FoodSupply.Get<double>("food") + zoo.Staff.Sum(s => s.Get<double>("carried_food")) + zoo.Animals.Sum(a => a.Get<double>("fed_count"));
        Check(conserved == 18, "饲料不守恒");
        Check(zoo.Visitors.Concat(zoo.Staff).All(a => a.Mind!.Beliefs.Count == 0), "长期运行出现守则知识");
    }
    Check(anomaly.Trace.Any(e => e.Phase == ZooAnomalies.AnomalyPhase.Missing), "无守则自主运行从未产生异常后果");
    Check(anomaly.Trace.Any(e => e.Phase == ZooAnomalies.AnomalyPhase.Protection), "自主运行未覆盖保护");
    Console.WriteLine($"METRICS visitors={zoo.Visitors.Count} exited={zoo.Visitors.Count(v => v.CurrentScene == zoo.Exit)} missing={zoo.Visitors.Count(v => v.Parent == null)} events={zoo.CausalTrace.Count} anomalyEvents={anomaly.Trace.Count} queue={zoo.Session.Timeline.Queue.Count} av={zoo.Session.Timeline.Now}");
});
Run("20个种子的自主无守则场景：终局与事件一致", () =>
{
    var missing = 0; var exited = 0; var events = 0;
    for (var seed = 0; seed < 20; seed++)
    {
        using var zoo = new ZooBaseline(seed, 6); using var anomaly = new ZooAnomalies(zoo);
        zoo.Start(); zoo.Run(500, 50_000);
        foreach (var actor in zoo.Visitors)
        {
            var isMissing = anomaly.StatusOf(actor) == ZooAnomalies.AnomalyStatus.Missing;
            Check(isMissing == (actor.Parent == null), "异常终局与实体位置不一致");
            Check(isMissing || actor.CurrentScene == zoo.Exit, "游客未结束行程");
        }
        foreach (var terminal in anomaly.Trace.Where(e => e.Phase == ZooAnomalies.AnomalyPhase.Missing))
        {
            var later = zoo.CausalTrace.SkipWhile(e => !ReferenceEquals(e, terminal.TriggerEvent)).Skip(1);
            Check(later.All(e => !ReferenceEquals(e.GetIntent()!.Actor, terminal.Actor)), "失踪角色仍在执行行动");
        }
        VerifyMovement(zoo);
        missing += zoo.Visitors.Count(v => v.Parent == null);
        exited += zoo.Visitors.Count(v => v.CurrentScene == zoo.Exit);
        events += zoo.CausalTrace.Count;
    }
    Check(missing > 0 && exited > 0, "自主测试没有覆盖不同结局");
    Console.WriteLine($"MULTISEED seeds=20 visitors=120 missing={missing} exited={exited} events={events}");
});
Run("工作资源耗尽后停止无效行动", () =>
{
    using var zoo = new ZooBaseline(1, 3);
    zoo.Start(); zoo.Run(500, 50_000);
    Check(zoo.FoodSupply.Get<double>("food") == 0 && zoo.Staff.Sum(s => s.Get<double>("carried_food")) == 0, "没有完成有限饲料配送");
    Check(zoo.Animals.All(a => a.Get<double>("fed_count") > 0), "部分动物从未喂养");
    Check(zoo.Animals.Sum(a => a.Get<double>("fed_count")) == 18, "饲料总量错误");
    var count = zoo.CausalTrace.Count;
    zoo.Run(5_000, 50_000);
    Check(zoo.CausalTrace.Count == count && zoo.Session.Timeline.Queue.Count == 0, "资源耗尽后员工仍无效空转");
});
Console.WriteLine($"RESULT failures={failures}");
return failures == 0 ? 0 : 1;
void Run(string name, Action action)
{
    try { action(); Console.WriteLine($"PASS {name}"); }
    catch (Exception e) { failures++; Console.WriteLine($"FAIL {name}: {e}"); }
}
static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static void VerifyMovement(ZooBaseline zoo)
{
    var lastTimes = new Dictionary<Entity, double>();
    foreach (var ev in zoo.CausalTrace)
    {
        var intent = ev.GetIntent()!;
        var time = ev.Get<double>("time");
        Check(double.IsFinite(time), "事件时间非有限");
        if (lastTimes.TryGetValue(intent.Actor, out var ready)) Check(time >= ready, "同身体恢复未结束就行动");
        lastTimes[intent.Actor] = time + intent.MotionCost;
        foreach (var change in ev.GetChanges().Where(c => c.Field == "$parent"))
            if (change.Before is Entity from && change.After is Entity to)
                Check(zoo.Topology.Passages.Any(p => ReferenceEquals(p.Other(from), to)), "无通道瞬移");
    }
}

static Intent MoveIntent(ZooBaseline zoo, Character actor, Entity destination)
{
    var current = actor.CurrentScene ?? throw new Exception("角色已离开场景");
    var passage = zoo.Topology.Passages.Single(p => ReferenceEquals(p.Other(current), destination));
    return new Intent(actor, actor.Mind!, AST.F(EffectOp.Move, actor, destination), [AST.F(EffectOp.Move, actor, destination)], passage.CostFrom(current), $"干预移动到{destination.Name}",
        [AST.E("eq", AST.E("value", actor, "$parent"), current)]);
}
static void Move(ZooBaseline zoo, Character actor, Entity destination) => Execute(zoo, MoveIntent(zoo, actor, destination));
static void Wait(ZooBaseline zoo, Character actor) => Execute(zoo, new Intent(actor, actor.Mind!, AST.F(EffectOp.Wait, actor), Array.Empty<Effect>(), 1, "停留观察"));
static void Execute(ZooBaseline zoo, Intent action)
{
    ActionResolution? result = null;
    zoo.Session.Submit(action, r => result = r);
    for (var i = 0; i < 1000 && result == null && zoo.Session.Timeline.Step(); i++) { }
    Check(result?.Result?.Succeeded == true, "受控行动未成功执行");
}

