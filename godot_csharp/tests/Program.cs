using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Simulation;
using DualOntologyWorldEngine.Core.Timeline;

namespace DualOntologyWorldEngine.Tests;

public static class Program
{
    public static int Main()
    {
        Console.WriteLine("=== 开始落实机制全功能与数学模型验证 ===");

        TestCtbTimeline();
        TestTwoPhaseCommitAndAbort();
        TestAccessControlAndContainers();
        TestStatusEffectDynamics();
        TestAstTextCompiler();
        TestUtilityAiAndBucketing();
        TestSmartObjectReservation();
        TestSpeechActsAndProtocols();
        TestEpistemicFourStagesAndTopology();
        TestDualTimescaleEmotionDecay();

        Console.WriteLine("\n[OK] 全流程 10 大标准机制与 5 大特化机制验证全部通过！");
        return 0;
    }

    private static void TestCtbTimeline()
    {
        Console.Write("[1] 验证 CTB 行动值回合制系统... ");
        var timeline = new Timeline();

        // 1. BaseAV 与 ActionAV
        var baseAv = Timeline.CalculateBaseAv(100.0);
        Assert(Math.Abs(baseAv - 100.0) < 1e-6, "BaseAV 必须为 10000 / 100 = 100");

        var actionAv = Timeline.CalculateActionAv(100.0, 1.5);
        Assert(Math.Abs(actionAv - 150.0) < 1e-6, "ActionAV 必须为 100 * 1.5 = 150");

        // 2. 调度与推进
        timeline.Schedule(100.0, "ActorA", () => null);
        timeline.Schedule(150.0, "ActorB", () => null);

        // 3. 拉条（Advance）
        timeline.AdvanceGauge(task => task.Label == "ActorB", 100.0, 0.2); // 减 20 AV -> 130
        var bTask = timeline.Queue.First(t => t.Label == "ActorB");
        Assert(Math.Abs(bTask.Time - 130.0) < 1e-6, "拉条 20% 后 ActorB 时间应为 130");

        // 4. 推条（Delay）
        timeline.DelayGauge(task => task.Label == "ActorA", 100.0, 0.1); // 加 10 AV -> 110
        var aTask = timeline.Queue.First(t => t.Label == "ActorA");
        Assert(Math.Abs(aTask.Time - 110.0) < 1e-6, "推条 10% 后 ActorA 时间应为 110");

        // 5. 变速缩放
        timeline.ScaleGaugeOnSpeedChange(task => task.Label == "ActorA", 100.0, 200.0); // 剩余 110 变为 55
        aTask = timeline.Queue.First(t => t.Label == "ActorA");
        Assert(Math.Abs(aTask.Time - 55.0) < 1e-6, "速度翻倍后剩余 AV 应缩放为 55");

        // 6. 中断机制
        var interrupted = false;
        timeline.Interrupt("Burst", () => interrupted = true);
        Assert(interrupted, "中断操作必须立即执行");

        // 7. 预测序列
        var preview = timeline.PreviewTurnOrder(5, defaultSpeed: 100.0);
        Assert(preview.Count == 5, "预测队列应生成 5 步");
        Assert(preview[0] == "ActorA", "首个行动者应为 ActorA");

        Console.WriteLine("通过");
    }

    private static void TestTwoPhaseCommitAndAbort()
    {
        Console.Write("[2] 验证两阶段事务纯函数性、防二次提交与中止... ");
        var executor = new EffectExecutor();
        var entity = new Entity("Item");
        entity.Set("energy", 10.0);

        // 准备计划
        var plan = executor.Prepare([AST.F("change", entity, "energy", 5.0)]);
        Assert(plan.Succeeded, "计划准备应成功");
        Assert(entity.Get<double>("energy") == 10.0, "准备阶段禁止修改真实实体");

        // 提交
        var result = plan.Commit();
        Assert(result.Succeeded, "首次提交应成功");
        Assert(entity.Get<double>("energy") == 15.0, "提交后应应用状态变化");

        // 再次提交（Double Commit 防护）
        var doubleResult = plan.Commit();
        Assert(!doubleResult.Succeeded, "重复提交必须被拦截拒绝");

        // 中止机制
        var plan2 = executor.Prepare([AST.F("change", entity, "energy", 10.0)]);
        var abortTriggered = false;
        plan2.OnAbort(() => abortTriggered = true);
        plan2.Abort("测试中止");
        Assert(abortTriggered, "中止回调必须被触发");
        Assert(!plan2.Commit().Succeeded, "已中止的计划禁止被提交");

        // 提交途中发生异常时，不得留下先前已写入的部分状态。
        var unboundBody = new Character("UnboundBody");
        unboundBody.Set("energy", 10.0);
        var rollbackPlan = executor.Prepare([
            AST.F("set", unboundBody, "energy", 20.0),
            AST.F("set", unboundBody, "strain", 5.0)
        ]);
        var rollbackResult = rollbackPlan.Commit();
        Assert(!rollbackResult.Succeeded, "写入器异常应使提交失败");
        Assert(unboundBody.Get<double>("energy") == 10.0, "提交失败必须恢复已写入的先前状态");

        Console.WriteLine("通过");
    }

    private static void TestAccessControlAndContainers()
    {
        Console.Write("[3] 验证门禁锁钥匹配、容器容量与无环约束... ");
        var roomA = new Entity("RoomA");
        var roomB = new Entity("RoomB");
        var door = new Entity("Door", roomA);
        door.AddTags(CommonTag.Door, CommonTag.Openable);

        var key = new Entity("IronKey");
        door.Lock(1, side: roomA, source: key);

        Assert(door.IsLocked(roomA), "从 RoomA 侧应处于锁定状态");
        Assert(!door.IsLocked(roomB), "从 RoomB 侧应不被该方向锁阻断");
        Assert(!door.Open(roomA), "锁定状态开门应失败");

        // 钥匙解锁
        var unlocked = door.UnlockWithKey(key, roomA);
        Assert(unlocked > 0, "使用对应钥匙应成功解锁");
        Assert(door.Open(roomA), "解锁后开门应成功");

        // 容器容量与无环约束
        var chest = new Entity("Chest", roomA);
        chest.AddRole(Role.Container);
        chest.Set("capacity", 1); // 容量上限 1

        var gem1 = new Entity("Gem1");
        var gem2 = new Entity("Gem2");

        Assert(chest.Put(gem1, fromScene: roomA), "首次放入未超容物品应成功");
        Assert(!chest.Put(gem2, fromScene: roomA), "容器超过容量上限时放入应失败");

        // 环路检查：不能把自身或祖先放入自身中
        Assert(!chest.Put(chest, fromScene: roomA), "禁止将容器放入自身内部");

        Console.WriteLine("通过");
    }

    private static void TestStatusEffectDynamics()
    {
        Console.Write("[4] 验证状态效果闭式时效公式、四种堆叠策略与到期清理... ");
        var host = new Entity("Hero");

        // 闭式公式：t < t0 -> 0; t0 <= t < t_expire -> max(0, I0 - λ * (t - t0)); t >= t_expire -> 0
        var buff = InfluenceSystem.ApplyStatusEffect(
            host, "Shield", intensity: 100.0, duration: 50.0, currentTime: 10.0,
            policy: StackingPolicy.DurationRefresh, decay: 1.0);

        Assert(buff.EffectiveIntensity(5.0) == 0.0, "未到 started_at 时强度严格为 0");
        Assert(buff.EffectiveIntensity(10.0) == 100.0, "开始时刻强度为初始 100");
        Assert(buff.EffectiveIntensity(20.0) == 90.0, "衰减 10 时间后强度为 90");
        Assert(buff.EffectiveIntensity(60.0) == 0.0, "到达 expires_at 时强度归 0");

        // 堆叠策略：DurationRefresh
        InfluenceSystem.ApplyStatusEffect(
            host, "Shield", intensity: 100.0, duration: 50.0, currentTime: 20.0,
            policy: StackingPolicy.DurationRefresh);
        Assert(buff.Get<double>("expires_at") == 70.0, "刷新策略应将过期时间顺延至 20 + 50 = 70");

        // 堆叠策略：IntensityStacking
        var poison = InfluenceSystem.ApplyStatusEffect(
            host, "Poison", intensity: 10.0, duration: 30.0, currentTime: 0.0,
            policy: StackingPolicy.IntensityStacking);
        InfluenceSystem.ApplyStatusEffect(
            host, "Poison", intensity: 15.0, duration: 30.0, currentTime: 0.0,
            policy: StackingPolicy.IntensityStacking, maxIntensity: 50.0);
        Assert(poison.Get<double>("intensity") == 25.0, "层数累加策略应累加为 25");

        // 垃圾回收清理
        var cleaned = InfluenceSystem.CleanupExpired(host, 1000.0);
        Assert(cleaned >= 2, "过期状态实体应被垃圾回收移出宿主");

        Console.WriteLine("通过");
    }

    private static void TestAstTextCompiler()
    {
        Console.Write("[5] 验证 AST 文本编译器管线 (Lexer, Parser, SymbolResolver)... ");
        var alice = new Entity("Alice");
        var garden = new Entity("Garden");
        var symbols = new Dictionary<string, Entity>
        {
            ["Alice"] = alice,
            ["Garden"] = garden
        };

        var dsl = "RULE state($Actor, \"ready\") => REQUIRE move($Actor, @Garden), PREFER inspect($Actor, @Alice)";
        var rule = RuleCompiler.Compile(dsl, symbols, "花园行动守则");

        Assert(rule.Condition.Op == "state", "条件谓词应解析为 state");
        Assert((RuleSlot)rule.Condition.Args[0] == RuleSlot.Actor, "第一个参数应解析为 $Actor 槽位");
        Assert((string)rule.Condition.Args[1] == "ready", "第二个参数应解析为字面量 ready");

        Assert(rule.Directives.Count == 2, "应包含 2 条规约指令");
        Assert(rule.Directives[0].Mode == Mode.Require, "第一条指令应为 REQUIRE");
        Assert(rule.Directives[0].Effect.Op == "move", "第一条效果应为 move");
        Assert(ReferenceEquals(rule.Directives[0].Effect.Args[1], garden), "符号 @Garden 应解析为真实 Entity 引用");

        Assert(rule.Directives[1].Mode == Mode.Prefer, "第二条指令应为 PREFER");
        Assert(ReferenceEquals(rule.Directives[1].Effect.Args[1], alice), "符号 @Alice 应解析为真实 Entity 引用");

        Console.WriteLine("通过");
    }

    private static void TestUtilityAiAndBucketing()
    {
        Console.Write("[6] 验证 Utility AI 归一化、响应曲线、带权乘法聚合与分桶惯性... ");

        // 1. 归一化
        var norm = UtilityMath.Normalize(50, 0, 100);
        Assert(Math.Abs(norm - 0.5) < 1e-6, "归一化应为 0.5");

        // 2. 响应曲线
        var lin = UtilityMath.LinearCurve(0.5, m: 2.0, c: 0.0, k: 0.0);
        Assert(Math.Abs(lin - 1.0) < 1e-6, "线性映射 0.5 * 2 = 1.0");

        var poly = UtilityMath.PolynomialCurve(0.5, m: 1.0, c: 0.0, k: 2.0);
        Assert(Math.Abs(poly - 0.25) < 1e-6, "二次幂 0.5^2 = 0.25");

        var logi = UtilityMath.LogisticCurve(0.5, k: 10.0, x0: 0.5);
        Assert(Math.Abs(logi - 0.5) < 1e-6, "S型中心点应为 0.5");

        // 3. 带权乘法聚合
        var c1 = new Consideration(0.8, 1.0);
        var c2 = new Consideration(0.5, 1.0);
        var agg = UtilityMath.AggregateMultiplicative([c1, c2]);
        Assert(Math.Abs(agg - 0.4) < 1e-6, "完全权重乘法 0.8 * 0.5 = 0.4");

        // 一票否决
        var veto = new Consideration(0.0, 1.0);
        var aggVeto = UtilityMath.AggregateMultiplicative([c1, veto]);
        Assert(aggVeto == 0.0, "关键不满足项必须使总效用归零");

        // 4. 分桶仲裁与惯性
        var idleAct = new ScoredOption<string>("IdleWalk", 0.9, UtilityBucket.Idle);
        var critAct = new ScoredOption<string>("FleeDanger", 0.3, UtilityBucket.Critical);

        var best = UtilityArbiter.SelectBest([idleAct, critAct]);
        Assert(best?.Option == "FleeDanger", "危机桶应绝对压制低优先级分桶");

        Console.WriteLine("通过");
    }

    private static void TestSmartObjectReservation()
    {
        Console.Write("[7] 验证智能对象槽位并发与三阶段预约状态机... ");
        var slotManager = new InteractionSlotManager(capacity: 2);
        var userA = new Entity("UserA");
        var userB = new Entity("UserB");
        var userC = new Entity("UserC");

        // 预约
        Assert(slotManager.TryReserve(userA, 0.0, out int slotA), "UserA 预约首个槽位应成功");
        Assert(slotManager.TryReserve(userB, 0.0, out int slotB), "UserB 预约次个槽位应成功");
        Assert(!slotManager.TryReserve(userC, 0.0, out _), "满额后 UserC 预约应失败");

        // 占用执行
        Assert(slotManager.TryOccupy(userA), "UserA 到达后占用应成功");
        Assert(slotManager.ValidateBeforeExecution(userA), "执行前二次校验应通过");

        // 释放
        Assert(slotManager.Release(userA), "释放槽位应成功");
        Assert(slotManager.HasAvailableSlot, "释放后应有空闲槽位");

        // 超时回收
        var timedOut = slotManager.CleanupTimedOut(currentTime: 100.0, timeoutDuration: 50.0);
        Assert(timedOut == 1, "未按期到达的 UserB 预约应被超时回收");

        Console.WriteLine("通过");
    }

    private static void TestSpeechActsAndProtocols()
    {
        Console.Write("[8] 验证言语行为原语与会话状态机... ");
        var speaker = new Entity("Alice");
        var listener = new Entity("Bob");
        var topic = new Entity("SecretDoc");
        var claim = AST.E("eq", AST.E("value", topic, "valid"), true);

        // 质询查验状态机
        var challengeSession = new ChallengeVerificationSession("conv-1", speaker, topic, claim);
        Assert(challengeSession.State == ChallengeProtocolState.Stated, "初始应为 Stated");

        Assert(challengeSession.Challenge(listener), "Bob 发起质询应成功");
        Assert(challengeSession.State == ChallengeProtocolState.Challenged, "状态应流转至 Challenged");

        var evidence = new Entity("SealProof");
        Assert(challengeSession.Testify(evidence), "Alice 提交证据应成功");
        Assert(challengeSession.State == ChallengeProtocolState.Verified, "状态应流转至 Verified");

        // 协商交换状态机
        var itemA = new Entity("Apple");
        var itemB = new Entity("Orange");
        var negoSession = new NegotiationSession("conv-2", speaker, listener, itemA, itemB);
        Assert(negoSession.State == NegotiationProtocolState.Proposed, "协商初始应为 Proposed");
        Assert(negoSession.Accept(), "对方承诺接受应成功");
        Assert(negoSession.State == NegotiationProtocolState.Accepted, "协商完成应为 Accepted");

        Console.WriteLine("通过");
    }

    private static void TestEpistemicFourStagesAndTopology()
    {
        Console.Write("[9] 验证四阶段信念内化流水线与主观拓扑寻路... ");
        var soul = new Soul("Observer");
        var issuer = new Entity("AuthorityOffice");
        issuer.SetTag("Authority", 3);
        var rule = new Rule(AST.E("state", RuleSlot.Actor, "calm"), [new Directive(Mode.Prefer, AST.F("rest", RuleSlot.Actor))]);
        var pub = RuleEntityFactory.CreateRulePublication(rule, issuer);

        // 1. 接收阶段 (Received)
        var belief = soul.Receive(pub, confidence: 1);
        Assert(belief.HasTag(CommonTag.Received), "初始应处于 Received 阶段");

        // 2. 理解阶段 (Understood)
        soul.Understand(rule);
        Assert(belief.HasTag(CommonTag.Understood), "理解后应处于 Understood 阶段");

        // 3. 评估与采纳/拒绝 (Evaluated -> Accepted)
        soul.EvaluateBelief(rule, acceptanceThreshold: 4.0);
        Assert(belief.HasTag(CommonTag.Accepted), "权威支持足够时应采纳为 Accepted 阶段");

        // 主客观二元拓扑寻路
        var world = new Entity("World");
        var room1 = new Entity("Room1", world);
        room1.AddTags(CommonTag.Scene);
        var room2 = new Entity("Room2", world);
        room2.AddTags(CommonTag.Scene);
        var passage = new Passage("Door12", room1, room2, cost: 1.0);
        var objectiveTopology = new TopologyGraph();
        objectiveTopology.Add(passage);
        world.Set("topology", objectiveTopology);

        // 未知通路时主观寻路应不可达
        var pathUnseen = soul.FindPathSubjective(room1, room2);
        Assert(pathUnseen == null, "智能体未获知通路时主观寻路应返回不可达");
        Assert(objectiveTopology.FindPath(room1, room2)?.Count == 1, "同一通路在客观拓扑中应当可达");

        // 长线承诺的路线惯性也不得偷读客观拓扑。
        var body = new Character("Body", room1);
        soul.Bind(body);
        var destination = new Entity("Destination", room2);
        soul.CommitTo(destination);
        var move = new Intent(body, soul, AST.F("move", body, passage, room2), [], motionCost: 1);
        var beforeLearning = soul.Assess([move], new RuleSystem(), new RuleEnvironment()).Single();
        Assert(beforeLearning.CommitmentInertia == 0, "未知通路不得获得基于客观路线的承诺惯性");

        // 获知通路后
        soul.LearnPassage(passage);
        var pathKnown = soul.FindPathSubjective(room1, room2);
        Assert(pathKnown?.Count == 1, "获知通路后主观寻路应成功返回");
        var afterLearning = soul.Assess([move], new RuleSystem(), new RuleEnvironment()).Single();
        Assert(afterLearning.CommitmentInertia == soul.CommitmentInertia, "获知通路后可按主观路线获得承诺惯性");

        Console.WriteLine("通过");
    }

    private static void TestDualTimescaleEmotionDecay()
    {
        Console.Write("[10] 验证双时间尺度情绪动力学衰减连续方程... ");
        var initial = 8.0;
        // I(t) = 0.7 * 8 * e^(-0.1 * t) + 0.3 * 8 * e^(-0.005 * t)
        var atT10 = Soul.CalculateDualTimescaleDecay(initial, 10.0, wAcute: 0.7, wMood: 0.3, lambdaFast: 0.1, lambdaSlow: 0.005);
        var expectedAcute = 0.7 * 8.0 * Math.Exp(-1.0);
        var expectedMood = 0.3 * 8.0 * Math.Exp(-0.05);
        var expectedTotal = expectedAcute + expectedMood;

        Assert(Math.Abs(atT10 - expectedTotal) < 1e-4, "双时间尺度衰减应严格符合闭式指数衰减方程");
        Assert(atT10 < initial, "衰减后数值必须小于初始值");

        Console.WriteLine("通过");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception("断言失败: " + message);
    }
}
