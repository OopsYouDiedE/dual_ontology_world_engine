using System.Diagnostics;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Simulation;
using DualOntologyWorldEngine.Core.Timeline;

var failed = 0;
Run("百万次重复调度与同刻稳定顺序", () =>
{
    var timeline = new Timeline();
    var count = 0;
    double elapsed = 0;
    timeline.Advanced += dt => elapsed += dt;
    for (var actor = 0; actor < 32; actor++)
    {
        var expected = actor;
        timeline.Schedule(0, actor.ToString(), () =>
        {
            Check(count % 32 == expected, "同刻顺序改变");
            Check(timeline.Now == count / 32, "时刻偏移");
            count++;
            return 1;
        });
    }
    for (var batch = 0; batch < 1000; batch++)
        Check(timeline.RunUntil(1_000_000, 1000) == 1000, "步数预算失效");
    Check(count == 1_000_000 && timeline.Queue.Count == 32, "队列增长或行动丢失");
    Check(elapsed == timeline.Now, "连续过程重复推进");
});
Run("20 个固定种子各一万次随机队列操作", () =>
{
    for (var seed = 0; seed < 20; seed++)
    {
        var rng = new Random(seed);
        var timeline = new Timeline();
        var model = new List<(double Time, long Seq, string Label)>();
        long seq = 0;
        for (var i = 0; i < 10_000; i++)
        {
            var label = rng.Next(8).ToString();
            switch (rng.Next(4))
            {
                case 0:
                    var delay = rng.Next(100);
                    timeline.Schedule(delay, label, () => null);
                    model.Add((timeline.Now + delay, seq++, label));
                    break;
                case 1:
                    Check(timeline.Cancel(t => t.Label == label) == model.RemoveAll(t => t.Label == label), "取消数量错误");
                    break;
                case 2:
                    var delta = rng.Next(-50, 51);
                    timeline.AdjustRemainingAv(t => t.Label == label, delta);
                    model = model.Select(t => t.Label == label ? (Math.Max(timeline.Now, t.Time + delta), t.Seq, t.Label) : t).ToList();
                    break;
                default:
                    var sorted = model.OrderBy(t => t.Time).ThenBy(t => t.Seq).ToArray();
                    Check(timeline.Step() == (sorted.Length > 0), "空队列行为错误");
                    if (sorted.Length > 0)
                    {
                        Check(timeline.Now == sorted[0].Time, "执行时刻错误");
                        model.Remove(sorted[0]);
                    }
                    break;
            }
            Check(timeline.Queue.Select(t => (t.Time, t.Sequence, t.Label)).SequenceEqual(model.OrderBy(t => t.Time).ThenBy(t => t.Seq)), $"队列不一致 seed={seed} step={i}");
        }
    }
});
Run("十万次事务资源守恒、过期计划与重复提交", () =>
{
    var executor = new EffectExecutor();
    var left = new Entity("左");
    var right = new Entity("右");
    left.Set("fuel", 100d); right.Set("fuel", 100d);
    for (var i = 0; i < 100_000; i++)
    {
        var from = i % 2 == 0 ? left : right;
        var to = i % 2 == 0 ? right : left;
        Effect[] effects = [AST.F(EffectOp.Spend, from, "fuel", 1d), AST.F(EffectOp.Change, to, "fuel", 1d)];
        var plan = executor.Prepare(effects);
        var stale = executor.Prepare(effects);
        Check(left.Get<double>("fuel") + right.Get<double>("fuel") == 200, "准备改变状态");
        Check(plan.Commit().Succeeded, "正常提交失败");
        Check(!stale.Commit().Succeeded && !plan.Commit().Succeeded, "过期或重复计划被提交");
        Check(left.Get<double>("fuel") + right.Get<double>("fuel") == 200, "资源不守恒");
    }
    var denied = executor.Prepare([AST.F(EffectOp.Spend, left, "fuel", 101d)]);
    Check(!denied.Commit().Succeeded && left.Get<double>("fuel") == 100, "允许透支");
});
Run("两万次完整结算与身体恢复时间", () =>
{
    using var session = new SimulationSession(new Entity("世界"));
    var actor = new Character("角色");
    var soul = new Soul("心智"); actor.Bind(soul);
    var action = new Intent(actor, soul, AST.F(EffectOp.Wait, actor), Array.Empty<Effect>(), motionCost: 3);
    for (var i = 0; i < 20_000; i++)
    {
        var finished = false;
        session.Submit(action, result => { Check(result.Result?.Succeeded == true, "结算失败"); finished = true; });
        Check(session.Timeline.Step() && finished, "命令未执行");
        Check(session.Timeline.Now == i * 3d && session.ReadyAt(actor) == (i + 1) * 3d, "恢复时间漂移");
        Check(session.Timeline.Queue.Count == 0, "已完成命令残留");
    }
    Check(session.Runtime.Events.Count == 20_000, "事件丢失或重复");
});
Run("巨大时刻的正延迟必须推进或明确拒绝", () =>
{
    var timeline = new Timeline(); timeline.RunUntil(1e20);
    try { timeline.Schedule(1, "正延迟", () => null); }
    catch (ArgumentOutOfRangeException) { return; }
    Check(timeline.Queue.Single().Time > timeline.Now, "正延迟退化为同刻循环");
});
Run("绝对行动值溢出必须拒绝", () =>
{
    var timeline = new Timeline(); timeline.RunUntil(double.MaxValue);
    try { timeline.Schedule(double.MaxValue, "溢出", () => null); }
    catch (ArgumentOutOfRangeException) { Check(timeline.Queue.Count == 0, "拒绝后队列被修改"); return; }
    throw new Exception("接受无穷任务时刻");
});
Run("一万次嵌套反应与独立恢复记账", () =>
{
    using var session = new SimulationSession(new Entity("世界"));
    var main = new Character("主行动者"); var mind = new Soul("主心智"); main.Bind(mind);
    var responder = new Character("反应者"); var responseMind = new Soul("反应心智"); responder.Bind(responseMind);
    var action = new Intent(main, mind, AST.F(EffectOp.Wait, main), Array.Empty<Effect>(), motionCost: 3);
    var reaction = new Intent(responder, responseMind, AST.F(EffectOp.Respond, responder), Array.Empty<Effect>(), motionCost: 2);
    session.Runtime.BeforeReactions = (intent, _) => ReferenceEquals(intent.Actor, main) ? [reaction] : [];
    for (var i = 0; i < 10_000; i++)
    {
        session.Submit(action); session.Timeline.Step();
        Check(session.Runtime.Events.Count == (i + 1) * 2, "嵌套事件数量错误");
        Check(session.ReadyAt(responder) == session.Timeline.Now + 2, "反应恢复记账错误");
        Check(session.Runtime.Events[^2].GetIntent() == reaction && session.Runtime.Events[^1].GetIntent() == action, "前置反应顺序错误");
    }
});
Run("一万轮激活替换、取消与释放", () =>
{
    var session = new SimulationSession(new Entity("世界"));
    var actor = new Character("角色"); var soul = new Soul("心智"); actor.Bind(soul);
    var action = new Intent(actor, soul, AST.F(EffectOp.Wait, actor), Array.Empty<Effect>());
    for (var i = 0; i < 10_000; i++)
    {
        session.Activate(actor, soul); session.Activate(actor, soul);
        Check(session.Timeline.Queue.Count == 1, "激活重复行动槽");
        session.Deactivate(actor);
        var cancel = session.Submit(action); cancel(); cancel();
        Check(session.Timeline.Queue.Count == 0, "取消留下任务");
    }
    session.Activate(actor, soul); session.Submit(action);
    session.Timeline.Schedule(1, "外部任务", () => null);
    session.Dispose(); session.Dispose();
    Check(session.Timeline.Queue.Count == 1 && session.Timeline.Queue[0].Label == "外部任务", "释放误删或遗留任务");
});
Run("资源相加溢出不得提交", () =>
{
    var entity = new Entity("资源"); entity.Set("fuel", double.MaxValue);
    var plan = new EffectExecutor().Prepare([AST.F(EffectOp.Change, entity, "fuel", double.MaxValue)]);
    Check(!plan.Commit().Succeeded && entity.Get<double>("fuel") == double.MaxValue, "资源被写入无穷值");
});
Run("非法队列调整不丢失已登记行动", () =>
{
    foreach (var scale in new[] { false, true })
    {
        var timeline = new Timeline();
        timeline.Schedule(1, "甲", () => null); timeline.Schedule(double.MaxValue, "乙", () => null);
        var before = timeline.Queue.ToArray();
        try
        {
            if (scale) timeline.ScaleGaugeOnSpeedChange(_ => true, double.MaxValue, double.Epsilon);
            else timeline.AdjustRemainingAv(_ => true, double.MaxValue);
            throw new Exception("应拒绝溢出");
        }
        catch (ArgumentOutOfRangeException) { }
        Check(timeline.Queue.SequenceEqual(before), "失败调整丢失或改变队列");
    }
});
Run("恢复时间溢出在效果提交前拒绝", () =>
{
    using var session = new SimulationSession(new Entity("世界"));
    var actor = new Character("角色"); var soul = new Soul("心智"); actor.Bind(soul);
    actor.Set("fuel", 1d);
    session.Timeline.RunUntil(double.MaxValue);
    var action = new Intent(actor, soul, AST.F(EffectOp.Work, actor), [AST.F(EffectOp.Spend, actor, "fuel", 1d)], motionCost: double.MaxValue);
    var result = session.Runtime.ResolveDetailed(action);
    Check(!result.Allowed && actor.Get<double>("fuel") == 1 && session.Runtime.Events.Count == 0, "恢复溢出仍提交效果");
});
Console.WriteLine($"RESULT failures={failed}");
return failed == 0 ? 0 : 1;
void Run(string name, Action test)
{
    var watch = Stopwatch.StartNew();
    try { test(); Console.WriteLine($"PASS {name} ({watch.ElapsedMilliseconds} ms)"); }
    catch (Exception error) { failed++; Console.WriteLine($"FAIL {name}: {error.Message}"); }
}
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }


