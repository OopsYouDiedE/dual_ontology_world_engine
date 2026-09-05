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

        // 规则语义比较必须使用实体引用，并对 AST 操作符忽略大小写。
        var sameNameLeft = new Entity("同名对象");
        var sameNameRight = new Entity("同名对象");
        var leftRule = AST.E("true").Then("同名对象规则",
            AST.F("open", sameNameLeft).Forbid());
        var rightRule = AST.E("TRUE").Then("另一条同名规则",
            AST.F("OPEN", sameNameRight).Forbid());
        var equivalentCaseRule = AST.E("TRUE").Then("大小写变化",
            AST.F("OPEN", sameNameLeft).Forbid());
        var publication = new Entity("规则发布物",
            properties: new Dictionary<string, object> { ["rule"] = leftRule });
        Check(!new[] { publication }.ContainsEquivalentRule(rightRule),
            "规则比较把同名但不同实体错误判定为等价");
        Check(new[] { publication }.ContainsEquivalentRule(equivalentCaseRule),
            "规则比较未按大小写不敏感处理 AST 操作符");

        // 客观补丁按当前辖域生效，并绑定行动中的 Actor/Target 槽位。
        var patchWorld = new Entity("补丁世界");
        var patchScene = new Entity("补丁场景", patchWorld, new Dictionary<string, int> { ["Scene"] = 1 });
        var otherScene = new Entity("其他场景", patchWorld, new Dictionary<string, int> { ["Scene"] = 1 });
        var patchActor = new Character("补丁行动者", patchScene);
        var patchMind = new Soul("补丁心智");
        patchActor.Bind(patchMind);
        var patchTarget = new Entity("补丁目标", patchScene);
        var patchRule = AST.E("true").Then("当前场景禁止操作",
            AST.F("operate", RuleSlot.Actor, RuleSlot.Target).Forbid());
        using var patchSession = new SimulationSession(patchWorld);
        patchSession.Runtime.Patches.Add(RuleEntityFactory.CreateRulePatch(
            "场景补丁", patchRule, parent: patchWorld, scope: patchScene));
        var patchIntent = new Intent(patchActor, patchMind,
            AST.F("operate", patchActor, patchTarget), []);
        var patchContext = new Dictionary<string, object> { ["domains"] = new[] { patchScene } };
        Check(!patchSession.Runtime.Review(patchIntent).Allowed,
            "未传 domains 时没有从身体所在世界树推导补丁辖域");
        Check(!patchSession.Runtime.Review(patchIntent, patchContext).Allowed,
            "当前场景的 Actor/Target Forbid 补丁未拦截行动");
        var blocked = patchSession.Runtime.ResolveDetailed(patchIntent, patchContext);
        Check(!blocked.Allowed && patchSession.Runtime.Events.Count == 0,
            "被当前场景补丁拦截的行动仍产生了事件");
        patchActor.Reparent(otherScene);
        patchTarget.Reparent(otherScene);
        var otherContext = new Dictionary<string, object> { ["domains"] = new[] { otherScene } };
        Check(patchSession.Runtime.Review(patchIntent, otherContext).Allowed
            && patchSession.Runtime.Review(patchIntent).Allowed,
            "其他场景错误继承了当前场景补丁");

        // 决策应基于心智观察形成的认知环境，而不能读取共享环境的隐藏事实。
        var knowledgeWorld = new Entity("认知世界");
        var knowledgeActor = new Character("认知行动者", knowledgeWorld);
        var knowledgeMind = new Soul("认知心智");
        knowledgeActor.Bind(knowledgeMind);
        var hiddenFact = AST.E("state", knowledgeActor, "hidden_fact");
        var observedRule = hiddenFact.Then("隐藏事实禁止行动",
            AST.F("operate", knowledgeActor).Forbid());
        var observedPublication = RuleEntityFactory.CreateRulePublication(observedRule, knowledgeActor);
        knowledgeMind.Receive(observedPublication);
        knowledgeMind.Accept(observedRule);
        using var knowledgeSession = new SimulationSession(knowledgeWorld);
        knowledgeSession.Runtime.Env.AssertFact(hiddenFact);
        var knowledgeIntent = new Intent(knowledgeActor, knowledgeMind,
            AST.F("operate", knowledgeActor), []);
        var hiddenDecision = knowledgeSession.Runtime.ResolveDetailed(knowledgeIntent).Decision!;
        Check(hiddenDecision.Complies && hiddenDecision.Pressures.Count == 0,
            "Decision 读取了 Soul 未观察到的 Env 独有事实");
        knowledgeMind.Observe(hiddenFact);
        var knownDecision = knowledgeSession.Runtime.ResolveDetailed(knowledgeIntent).Decision!;
        Check(!knownDecision.Complies,
            "Decision 未使用 Soul 已观察到的事实触发规则");

        // Listener 的再入调用必须受 Runtime.MaxReactionDepth 限制。
        var listenerWorld = new Entity("监听世界");
        var listenerActor = new Character("监听行动者", listenerWorld);
        var listenerMind = new Soul("监听心智");
        listenerActor.Bind(listenerMind);
        using var listenerSession = new SimulationSession(listenerWorld);
        listenerSession.Runtime.MaxReactionDepth = 3;
        var listenerIntent = new Intent(listenerActor, listenerMind,
            AST.F("ping", listenerActor), []);
        var listenerCalls = 0;
        listenerSession.Runtime.Listeners.Add((_, _) =>
        {
            listenerCalls++;
            listenerSession.Runtime.ResolveDetailed(listenerIntent);
        });
        listenerSession.Runtime.ResolveDetailed(listenerIntent);
        Check(listenerCalls <= listenerSession.Runtime.MaxReactionDepth + 1,
            "Listener 再入未被 MaxReactionDepth 终止");

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

        // 路径选择使用总成本，且同成本路径在重复查询中保持稳定顺序。
        var routeWorld = new Entity("路径代价世界");
        var routeA = new Entity("路线起点", routeWorld, new Dictionary<string, int> { ["Scene"] = 1 });
        var routeB = new Entity("路线中点一", routeWorld, new Dictionary<string, int> { ["Scene"] = 1 });
        var routeC = new Entity("路线中点二", routeWorld, new Dictionary<string, int> { ["Scene"] = 1 });
        var routeD = new Entity("路线终点", routeWorld, new Dictionary<string, int> { ["Scene"] = 1 });
        var expensiveDirect = new Passage("昂贵直达", routeA, routeD, cost: 10, parent: routeWorld);
        _ = new Passage("廉价第一段", routeA, routeB, cost: 3, parent: routeWorld);
        _ = new Passage("廉价第二段", routeB, routeD, cost: 3, parent: routeWorld);
        _ = new Passage("等价路线第一段", routeA, routeC, cost: 3, parent: routeWorld);
        _ = new Passage("等价路线第二段", routeC, routeD, cost: 3, parent: routeWorld);
        var routeOne = TopologySystem.FindPath(routeA, routeD)!;
        var routeTwo = TopologySystem.FindPath(routeA, routeD)!;
        Check(routeOne.Count == 2 && !routeOne.Contains(expensiveDirect) &&
            routeOne.All(passage => passage.CostFrom(passage.Left) == 3),
            "路径搜索未选择两段廉价路线替代昂贵直达路线");
        Check(routeOne.SequenceEqual(routeTwo), "等价路径查询顺序不稳定");

        // 读取未加锁对象不应为了查询创建锁状态；失败转移也不能清除 Held。
        var unlocked = new Entity("未加锁对象");
        Check(!unlocked.HasProperty("_locks") && !unlocked.IsLocked() && !unlocked.HasProperty("_locks"),
            "IsLocked 查询未加锁对象时新增了锁状态属性");
        var accessScene = new Entity("访问场景");
        var holder = new Character("持有者", accessScene);
        var heldItem = new Entity("持有物");
        heldItem.Hold(holder);
        var lockedContainer = new Entity("锁定容器", accessScene,
            new Dictionary<string, int> { ["Container"] = 1 });
        lockedContainer.Lock();
        Check(!heldItem.PutInto(lockedContainer, fromScene: accessScene) &&
            ReferenceEquals(heldItem.Parent, holder) && heldItem.Has("Held"),
            "失败的 PutInto 清除了物品 Held 状态或改变了归属");

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
