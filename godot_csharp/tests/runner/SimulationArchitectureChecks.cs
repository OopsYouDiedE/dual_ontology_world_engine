using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Kotodama;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Simulation;
using DualOntologyWorldEngine.Core.Timeline;
using DualOntologyWorldEngine.Simulation;

namespace DualOntologyWorldEngine.Tests;

internal static class SimulationArchitectureChecks
{
    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    public static void Run()
    {
        // 达到步数预算时，不能把尚未执行的 2 AV 回调伪装成 10 AV 的事件。
        var timeline = new Timeline();
        double elapsed = 0;
        timeline.Advanced += delta => elapsed += delta;
        timeline.Schedule(1, "周期", () => 1);
        Check(timeline.RunUntil(10, 1) == 1 && timeline.Now == 1, "步数耗尽仍跳过了待执行事件");
        timeline.RunUntil(10, 20);
        Check(timeline.Now == 10 && elapsed == 10, "累计过程与实际 AV 不一致");

        var world = new Entity("测试世界");
        using var session = new SimulationSession(world);
        var actor = new Character("行动者", world);
        var soul = new Soul("心智");
        actor.Bind(soul);
        var witness = new Character("见证者", world);
        var observer = new Soul("观察心智");
        witness.Bind(observer);
        soul.InstallStep((mind, body, env, ctx) => [new Intent(body, mind, AST.F("work", body), 10)]);
        observer.InstallStep((mind, body, env, ctx) => [new Intent(body, mind, AST.F("wait", body), 10)]);
        session.Runtime.WitnessesCollector = (ev, ctx) => [observer, observer];
        session.Runtime.ReactionHandler = (mind, ev, ctx) =>
            ev.Get<Effect>("effect")?.Op == "work"
                ? new Intent(witness, observer, AST.F("respond", witness), 3) : null;
        session.Activate(actor, soul);
        session.Activate(actor, soul); // 替换而非再添加一个角色槽。
        session.Activate(witness, observer);
        session.Timeline.RunUntil(3);
        Check(session.Runtime.Events.Count(ev => ev.Get<Effect>("effect")?.Op == "work") == 1, "重复激活导致双重行动");
        var reaction = session.Runtime.Events.Single(ev => ev.Get<Effect>("effect")?.Op == "respond");
        Check(reaction.Get<Entity>("cause") != null, "嵌套反应丢失因果来源");
        var wait = session.Runtime.Events.Single(ev => ev.Get<Effect>("effect")?.Op == "wait");
        Check(wait.Get<double>("time") == 3, "反应没有支付行动代价");
        Check(observer.Observations.Count == 3, "见证者未获得真实事件或重复见证");

        // 无候选不是永久删除。普通时间线过程改变环境后角色会重新发现机会。
        using var idleSession = new SimulationSession(new Entity("等待世界"));
        var idleActor = new Character("等待者");
        var idleSoul = new Soul("等待心智");
        idleActor.Bind(idleSoul);
        bool supplied = false;
        var table = new Entity("食物台");
        table.Offer((target, mind, body, env, ctx) => supplied
            ? [new Intent(body, mind, AST.F("eat", body, target), 5,
                resolve: () => { supplied = false; return true; }, validate: () => supplied)] : []);
        idleSoul.Discover(_ => [table]);
        idleSession.Activate(idleActor, idleSoul);
        idleSession.Timeline.Schedule(2, "送餐", () => { supplied = true; return null; });
        idleSession.Timeline.RunUntil(4);
        Check(idleSession.Runtime.Events.Count == 1 && !supplied, "空闲主体不能恢复行动或重复消耗资源");

        using var intercepted = new SimulationSession(new Entity("拦截世界"));
        bool open = true;
        var take = new Intent(actor, soul, AST.F("take", actor), 4, validate: () => open);
        intercepted.Runtime.BeforeReactions = (intent, ctx) => ReferenceEquals(intent, take)
            ? [new Intent(witness, observer, AST.F("close", witness), 2,
                resolve: () => { open = false; return true; })] : [];
        ActionResolution? denied = null;
        intercepted.Submit(take, result => denied = result);
        intercepted.Submit(new Intent(actor, soul, AST.F("rest", actor), 4));
        intercepted.Submit(new Intent(actor, soul, AST.F("rest", actor), 4));
        intercepted.Timeline.RunUntil(4);
        Check(denied?.Allowed == false && intercepted.Runtime.Events.All(ev => ev.Get<Effect>("effect")?.Op != "take"),
            "前置反应后未重新检查主行动，或制造了成功事件");
        Check(ReferenceEquals(intercepted.Runtime.Events[0].Get<object>("cause"), take), "拦截反应没有引用待执行意图");
        Check(intercepted.Runtime.Events.Select(ev => ev.Get<double>("time")).SequenceEqual([0d, 0d, 4d]),
            "外部命令未按身体恢复时间串行执行");

        var roadWorld = new Entity("道路世界");
        var firstRoom = new Entity("起点", roadWorld, new Dictionary<string, int> { ["Scene"] = 1 });
        var middleRoom = new Entity("中间", roadWorld, new Dictionary<string, int> { ["Scene"] = 1 });
        var lastRoom = new Entity("终点", roadWorld, new Dictionary<string, int> { ["Scene"] = 1 });
        var door = new Passage("第一道门", firstRoom, middleRoom, cost: 7, parent: roadWorld);
        _ = new Passage("第二道门", middleRoom, lastRoom, parent: roadWorld);
        actor.Reparent(firstRoom);
        var move = BaseActions.CreateMoveIntent(roadWorld, actor, lastRoom);
        Check(move.MotionCost == 7 && ReferenceEquals(move.Target, middleRoom), "基础移动绕过了拓扑首步或代价");
        door.Condition = (_, _, _) => false;
        Check(!move.Validate(), "旧移动意图未复核已关闭通道");
        var emptyFood = new Entity("空食物", firstRoom, new Dictionary<string, int> { ["Consumable"] = 1 });
        Check(!BaseActions.CreateConsumeIntent(actor, emptyFood).Validate(), "空消耗品仍能生成收益");

        // 真实学园执行五天；禁止改写用户的历史轨迹文件。
        var academy = new AcademySimulation { ExportOnCompletion = false };
        academy.RunSimulationFiveDays();
        Check(academy.Timeline.Now == 1000, "学园未完成五日 AV 推演");
        Check(academy.Runtime.Events.Count > 0, "学园没有产生真实事件");
        Check(academy.SceneItems.Concat(academy.StudentItems).All(item => item.Tag("Servings") >= 0), "学园资源透支");
        Check(academy.Timeline.Queue.Count == 0, "学园结束后遗留角色行动槽");
        Console.WriteLine($"架构验证通过：时间预算、反应成本、观察、单槽、空闲唤醒、学园五日（{academy.Runtime.Events.Count} 个事件）。");
    }
}
