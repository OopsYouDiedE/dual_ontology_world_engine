using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Simulation;
using static DualOntologyWorldEngine.Core.Rules.AST;

static class MechanismChecks
{
    public static void Run()
    {
        using var simulation = new LiterarySimulation("经验与选择回归");
        var scene = simulation.Scene("证物室");
        var ledger = simulation.Object("账本", scene, evidence: "药物只能救一个人");
        var actor = simulation.Person("阿芙", scene, ledger, "unreachable", true);
        var inspect = new Intent(actor, actor.Mind!, F("inspect", actor, ledger),
            [F("observe", actor.Mind!, ledger, "evidence")], 20);
        for (var i = 0; i < 20; i++) simulation.Predict(inspect);
        Require(actor.Get<double>("reflection_credit") == 0, "预测不得获得经验");
        var result = simulation.Session.Runtime.ResolveDetailed(inspect);
        Require(result.Result?.Succeeded == true && actor.Get<double>("reflection_credit") == 1, "亲手取得新证据应获得一次复核机会");
        Require(actor.Mind!.Observations.Contains(E("learned", ledger, "evidence", "药物只能救一个人")),
            "声明式观察须同步到认知事实，不能只有数值视图知道证据");
        var reflect = simulation.Narrative.Offers(actor).Single(i => i.Effect.Op == "reflect");
        Require(simulation.Session.Runtime.ResolveDetailed(reflect).Result?.Succeeded == true, "复核应成功");
        Require(actor.Get<double>("investigation") == 2 && actor.Get<double>("reflection_credit") == 0, "成长应支付证据额度");
        simulation.Session.Runtime.ResolveDetailed(inspect);
        Require(actor.Get<double>("reflection_credit") == 0, "重复调查同一证物不得刷成长");
        var second = simulation.Object("独立证词", scene, evidence: "确有一名病人");
        simulation.Session.Runtime.ResolveDetailed(new Intent(actor, actor.Mind!, F("inspect", actor, second),
            [F("observe", actor.Mind!, second, "evidence")], 20));
        Require(!simulation.Narrative.Offers(actor).Any(i => i.Effect.Op == "reflect"), "复核间隔必须生效");
        simulation.Run(200);
        simulation.Session.Runtime.ResolveDetailed(simulation.Narrative.Offers(actor).Single(i => i.Effect.Op == "reflect"));
        Require(Math.Abs(actor.Get<double>("investigation") - 3.96) < 1e-9, "成长应递减而非线性通胀");

        actor.Set("protected", true);
        simulation.Narrative.Care(actor, "保护已经得到的东西", actor, "protected", true, 9);
        var sacrifice = new Intent(actor, actor.Mind!, F("sacrifice", actor), [F("set", actor, "protected", false)]);
        Require(simulation.Narrative.Value(sacrifice, simulation.Predict(sacrifice)) == -9, "失去已有目标必须进入估值");
        var restore = new Intent(actor, actor.Mind!, F("restore", actor),
            [F("set", actor, "protected", false), F("set", actor, "protected", true)]);
        Require(simulation.Narrative.Value(restore, simulation.Predict(restore)) == 0, "同次行动反复写值不能刷目标收益");

        // 成长必须真正解锁能力，机会只在成功提交后消费。
        var opportunity = simulation.Narrative.Opportunity(actor, "辨认伪造的封条", ledger,
            [F("set", actor, "identified", true)], [E("gte", E("value", actor, "investigation"), 3d)]);
        var identify = simulation.Narrative.Offers(actor).Single(i => i.Definition == opportunity);
        simulation.Predict(identify);
        Require(!opportunity.Get<bool>("used"), "预测不得消耗文学机会");
        Require(simulation.Session.Runtime.ResolveDetailed(identify).Result?.Succeeded == true, "成长后的能力应可执行");
        Require(!simulation.Session.Runtime.ResolveDetailed(identify).Allowed, "同一机会不得重复兑现");
        var hidden = new Entity("未获知的关系后果");
        hidden.Set("broken", true);
        simulation.Narrative.Care(actor, "获知背弃后珍惜判断", actor, "identified", true, 7,
            E("eq", E("value", hidden, "broken"), true));
        var forget = new Intent(actor, actor.Mind!, F("forget", actor), [F("set", actor, "identified", false)]);
        Require(simulation.Narrative.Value(forget, simulation.Predict(forget)) == 0, "隐藏关系后果不得提前改变估值");
        StateView.Learn(actor.Mind!, hidden, "broken", true);
        Require(simulation.Narrative.Value(forget, simulation.Predict(forget)) == -7, "获知关系后果必须改变后续估值");
        Console.WriteLine("PASS 经验来源、复核间隔、递减成长、目标损失、机会一次性与能力解锁");
        IdleAndWake();
        LongGrowth();
    }

    private static void IdleAndWake()
    {
        using var simulation = new LiterarySimulation("安静与机会");
        var scene = simulation.Scene("候车亭");
        var actor = simulation.Person("等候者", scene, scene, "unreachable", true);
        actor.Set("done", false);
        simulation.Narrative.Care(actor, "完成交付", actor, "done", true, 5);
        simulation.Start();
        simulation.Run(1000);
        Require(simulation.Session.Runtime.Events.Count == 0 && simulation.Session.Timeline.Queue.Count == 1,
            "平静期间不得制造空事件或丢失唤醒槽");
        simulation.Narrative.Opportunity(actor, "终于等到交付机会", actor, [F("set", actor, "done", true)]);
        simulation.Run(1020);
        Require(actor.Get<bool>("done") && simulation.Session.Runtime.Events.Count == 1,
            "真实机会出现后必须恢复自主行动");
        Console.WriteLine("PASS 安静期不累积空事件，真实机会仍能唤醒行动");
    }

    private static void LongGrowth()
    {
        using var simulation = new LiterarySimulation("100000 AV 成长稳定性");
        var scene = simulation.Scene("调查生涯");
        var actor = simulation.Person("调查者", scene, scene, "unreachable", true);
        double previous = 0;
        for (var episode = 0; episode < 500; episode++)
        {
            simulation.Run(episode * 200);
            var evidence = new Entity($"独立案卷{episode}", scene);
            evidence.Set("evidence", $"第{episode}份可核实结论");
            simulation.Session.Submit(new Intent(actor, actor.Mind!, F("inspect", actor, evidence),
                [F("observe", actor.Mind!, evidence, "evidence")], 20));
            simulation.Run(episode * 200 + 20);
            simulation.Session.Submit(simulation.Narrative.Offers(actor).Single(i => i.Effect.Op == "reflect"));
            simulation.Run(episode * 200 + 40);
            var skill = actor.Get<double>("investigation");
            Require(skill > previous && skill < 100 && skill - previous <= 2, "长程成长越界或未递减");
            previous = skill;
        }
        simulation.Run(100000);
        Require(actor.Get<double>("reflection_credit") == 0 && simulation.Session.Runtime.Events.Count == 1000,
            "每份证据须恰好对应一次调查和复核");
        Require(Math.Abs(previous - 100 * (1 - Math.Pow(.98, 500))) < 1e-8, "实际长程成长不符合递减曲线");
        Console.WriteLine($"PASS 100000 AV / 500 次独立实证复核，能力={previous:F6}，事件=1000");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
