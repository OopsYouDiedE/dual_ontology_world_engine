using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Content;
using DualOntologyWorldEngine.Core.Kotodama;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Patterns;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Simulation;
using static DualOntologyWorldEngine.Core.Rules.AST;

var checks = new LiteraryChecks();
checks.Run();
return checks.Failed == 0 ? 0 : 1;

sealed class LiteraryChecks
{
    private int _passed;
    public int Failed { get; private set; }

    public void Run()
    {
        Check("连续 spend 不透支且不残留", SpendIsAtomic);
        Check("prepare 不改世界", PrepareDoesNotMutate);
        Check("stale plan 被拒绝", StalePlanRejected);
        Check("未知原语被拒绝", UnknownPrimitiveRejected);
        Check("认知隐藏字段为 Unknown", HiddenFieldIsUnknown);
        Check("错误信念只影响预估，不改客观执行", FalseBeliefSeparated);
        Check("deadline 只违约一次", DeadlineBreachesOnce);
        Check("及时履行不触发 deadline 违约", DeadlineHonoured);
        Check("软禁令可违反，hard 禁令阻断", ProhibitionModes);
        Check("真言禁止假命题，secrecy 可拒答", TruthAndSecrecy);
        Check("交换要求双方持有并原子完成", ExchangeIsAtomic);
        Check("Kotodama 成本与产出原子结算", KotodamaIsAtomic);
        Check("SelfBinding 折扣与破缚恢复原价", SelfBindingSealPricing);
        Check("Seal 持续消耗后耗尽释放", SealMaintenanceExhausts);
        Check("LiterarySimulation 反制只消耗有限资源", LiteraryDeflection);
        Check("已知禁令预测违约后果，未知规则不暴露", ConsequenceForecasting);
        Console.WriteLine($"通过项: {_passed}; 失败项: {Failed}; 总计: {_passed + Failed}");
    }

    private void Check(string name, Action test)
    {
        try { test(); _passed++; Console.WriteLine($"PASS {name}"); }
        catch (Exception ex) { Failed++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
    }

    private static (Entity World, Character Actor, Soul Soul, SimulationSession Session) Setup(double charges = 12)
    {
        var world = new Entity("World");
        var actor = new Character("Actor", world);
        actor.Set("charges", charges);
        var soul = new Soul("ActorSoul", world);
        actor.Bind(soul);
        return (world, actor, soul, new SimulationSession(world));
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void SpendIsAtomic()
    {
        var (world, actor, soul, session) = Setup();
        var plan = session.Runtime.Effects.Prepare([F("spend", actor, "charges", 7), F("spend", actor, "charges", 6)]);
        Assert(!plan.Succeeded, "透支计划不应成功");
        Assert(actor.Get<double>("charges") == 12, "失败计划不得残留第一笔支付");
        Assert(plan.Changes.Count == 1, "应记录局部第一笔变化但不提交");
        session.Dispose();
    }

    private static void PrepareDoesNotMutate()
    {
        var (world, actor, soul, session) = Setup();
        var plan = session.Runtime.Effects.Prepare([F("set", actor, "mood", "calm")]);
        Assert(actor.Get<string?>("mood") == null, "prepare 不得写入实体");
        var result = plan.Commit();
        Assert(result.Succeeded && actor.Get<string>("mood") == "calm", "commit 应写入预估变化");
        Assert(result.Outcome is StateChange[] changes && changes.Length == 1, "commit 应产生状态变化审计");
        session.Dispose();
    }

    private static void StalePlanRejected()
    {
        var (world, actor, soul, session) = Setup();
        var plan = session.Runtime.Effects.Prepare([F("set", actor, "mood", "calm")]);
        actor.Set("mood", "angry");
        var result = plan.Commit();
        Assert(!result.Succeeded && result.Reason.Contains("状态已变化"), "陈旧计划必须拒绝");
        Assert(actor.Get<string>("mood") == "angry", "陈旧提交不得覆盖新状态");
        session.Dispose();
    }

    private static void UnknownPrimitiveRejected()
    {
        var (world, actor, soul, session) = Setup();
        var plan = session.Runtime.Effects.Prepare([F("does_not_exist", actor, "x", 1)]);
        Assert(!plan.Succeeded && plan.Reason.Contains("未知效果"), "未知效果原语必须拒绝");
        session.Dispose();
    }

    private static void HiddenFieldIsUnknown()
    {
        var (world, actor, soul, session) = Setup();
        var other = new Entity("Other", world, properties: new Dictionary<string, object> { ["secret"] = 42d });
        var view = new StateView(soul);
        Assert(view.Read(other, "secret") == StateView.Unknown, "未观察的他者字段必须隐藏");
        Assert(view.Value(E("value", other, "secret")) == StateView.Unknown, "表达式读取也必须保持 Unknown");
        session.Dispose();
    }

    private static void FalseBeliefSeparated()
    {
        var (world, actor, soul, session) = Setup();
        var other = new Entity("Other", world, properties: new Dictionary<string, object> { ["secret"] = true });
        StateView.Learn(soul, other, "secret", false, "rumor");
        var subjective = new StateView(soul).Value(E("value", other, "secret"));
        Assert(Equals(subjective, false) && Equals(StateView.Actual(other, "secret"), true), "错误信念应与客观状态分离");
        var plan = session.Runtime.Effects.Prepare([F("set", other, "observed", true)], new StateView(soul));
        Assert(!plan.Commit().Succeeded, "认知视图计划不得提交到客观世界");
        Assert(other.Get<bool?>("observed") != true, "认知执行不得改变客观字段");
        session.Dispose();
    }

    private static void DeadlineBreachesOnce()
    {
        var (world, actor, soul, session) = Setup();
        var target = new Entity("Door", world);
        using var book = new PatternBook(session);
        var clause = Patterns.Deadline(actor, actor, target, "open", 2, F("set", target, "breached", true));
        book.Add(clause);
        session.Timeline.RunUntil(3);
        var breachEvents = session.Runtime.Events.Count(e => e.Get<string>("phase") == "Main" && e.Get<Effect>("effect")?.Op == "consequence");
        Assert(target.Get<bool>("breached"), "到期应产生违约后果");
        Assert(!clause.Get<bool>("active") && breachEvents == 1, "deadline 违约只应发生一次");
        session.Dispose();
    }

    private static void DeadlineHonoured()
    {
        var (world, actor, soul, session) = Setup();
        var target = new Entity("Door", world);
        using var book = new PatternBook(session);
        var clause = Patterns.Deadline(actor, actor, target, "open", 2, F("set", target, "breached", true));
        book.Add(clause);
        var intent = new Intent(actor, soul, F("open", actor, target), [F("set", target, "opened", true)], 1);
        var result = session.Runtime.ResolveDetailed(intent);
        Assert(result.Allowed && result.Result?.Succeeded == true, "及时履行应成功");
        Assert(clause.Get<bool>("completed") && !target.Get<bool>("breached"), "及时履行不得违约");
        session.Timeline.RunUntil(3);
        Assert(!target.Get<bool>("breached"), "履行后的计时器不得追加违约");
        session.Dispose();
    }

    private static void ProhibitionModes()
    {
        var (world, actor, soul, session) = Setup();
        var target = new Entity("Door", world);
        using var book = new PatternBook(session);
        book.Add(Patterns.Prohibit(actor, actor, "open", target, hard: false, breach: [F("set", target, "breached", true)]));
        var soft = new Intent(actor, soul, F("open", actor, target), [F("set", target, "opened", true)]);
        var softResult = session.Runtime.ResolveDetailed(soft);
        Assert(softResult.Allowed && target.Get<bool>("opened"), "软禁令允许行动通过");
        var hard = Patterns.Prohibit(actor, actor, "close", target, hard: true);
        book.Add(hard);
        var hardResult = session.Runtime.ResolveDetailed(new Intent(actor, soul, F("close", actor, target), [F("set", target, "closed", true)]));
        Assert(!hardResult.Allowed && !target.Get<bool>("closed"), "hard 禁令必须阻断行动");
        session.Dispose();
    }

    private static void TruthAndSecrecy()
    {
        var (world, actor, soul, session) = Setup();
        var listener = new Character("Listener", world); var listenerSoul = new Soul("ListenerSoul", world); listener.Bind(listenerSoul);
        using var book = new PatternBook(session);
        var proposition = E("true");
        book.Add(Patterns.Truth(actor, actor, proposition));
        var lie = new Intent(actor, soul, F("say", actor, listener), 1) { Claim = E("not", proposition) };
        Assert(!session.Runtime.ResolveDetailed(lie).Allowed, "真言不得接受假命题");
        var secret = Patterns.Truth(actor, actor, proposition, secrecy: true, allowedListener: listener);
        book.Add(secret);
        var publicClaim = new Intent(actor, soul, F("say", actor, listener), 1) { Claim = proposition, IsPrivate = false };
        Assert(!session.Runtime.ResolveDetailed(publicClaim).Allowed, "secrecy 条款应拒绝公开陈述");
        var privateClaim = new Intent(actor, soul, F("say", actor, listener), 1) { Claim = proposition, IsPrivate = true };
        Assert(session.Runtime.ResolveDetailed(privateClaim).Allowed, "向允许对象私下陈述应通过");
        session.Dispose();
    }

    private static void ExchangeIsAtomic()
    {
        var (world, actor, soul, session) = Setup();
        var issuer = new Character("Issuer", world); issuer.Set("charges", 12d);
        var issuerSoul = new Soul("IssuerSoul", world); issuer.Bind(issuerSoul);
        var offered = new Entity("Offered", issuer); var requested = new Entity("Requested", actor);
        using var book = new PatternBook(session);
        var clause = Patterns.Exchange(issuer, actor, offered, requested); book.Add(clause);
        StateView.Learn(soul, clause, "text", clause.Get<string>("text"));
        var offer = book.Offers(soul, actor).Single(i => i.Effect.Op == "exchange");
        var result = session.Runtime.ResolveDetailed(offer);
        Assert(result.Allowed && result.Result?.Succeeded == true, "双方持有物品时交换应成功");
        Assert(offered.Parent == actor && requested.Parent == issuer && clause.Get<bool>("completed"), "交换必须原子转移双方物品");
        var unavailable = new Entity("Unavailable", world);
        var requested2 = new Entity("Requested2", actor);
        var badClause = Patterns.Exchange(issuer, actor, unavailable, requested2); book.Add(badClause);
        StateView.Learn(soul, badClause, "text", badClause.Get<string>("text"));
        var badOffer = book.Offers(soul, actor).Single(i => i.Definition == badClause);
        var badResult = session.Runtime.ResolveDetailed(badOffer);
        Assert(!badResult.Allowed && unavailable.Parent == world && requested2.Parent == actor, "未持有 offered 时不得部分交换");
        session.Dispose();
    }

    private static void KotodamaIsAtomic()
    {
        var (world, actor, soul, session) = Setup();
        var over = KotodamaFactory.CreateRecipe("过量配方", costs:
            [F("spend", actor, "charges", 7), F("spend", actor, "charges", 6)],
            effects: [F("set", actor, "output", true)], parent: world);
        Assert(session.Runtime.Words.Invoke(over, soul, new Dictionary<string, object>()) == null, "超额成本应使言灵失败");
        Assert(actor.Get<double>("charges") == 12 && actor.Get<bool?>("output") != true, "超额成本不得扣除或产出");

        var unknown = KotodamaFactory.CreateRecipe("未知产出", costs:
            [F("spend", actor, "charges", 2)],
            effects: [F("unknown_output", actor, "opaque")], parent: world);
        Assert(session.Runtime.Words.Invoke(unknown, soul, new Dictionary<string, object>()) == null, "未知产出应使言灵失败");
        Assert(actor.Get<double>("charges") == 12, "未知产出失败时成本不得扣除");
        session.Dispose();
    }

    private static void SelfBindingSealPricing()
    {
        var (world, actor, soul, session) = Setup();
        var target = new Entity("SealTarget", world, properties: new Dictionary<string, object> { ["sealed"] = false });
        var medium = new Entity("Medium", actor, properties: new Dictionary<string, object> { ["charges"] = 2d });
        using var book = new PatternBook(session);
        var binding = Patterns.SelfBinding(actor, actor, "open"); book.Add(binding);
        StateView.Learn(soul, binding, "text", binding.Get<string>("text"));
        var bind = book.Offers(soul, actor).Single(i => i.Effect.Op == "bind_self");
        Assert(session.Runtime.ResolveDetailed(bind).Result?.Succeeded == true, "SelfBinding 应成功建立");
        var seal = Patterns.Seal(actor, actor, target, medium, strength: 2); book.Add(seal);
        StateView.Learn(soul, seal, "text", seal.Get<string>("text"));
        var invoke = book.Offers(soul, actor).Single(i => i.Effect.Op == "invoke_seal");
        Assert(session.Runtime.ResolveDetailed(invoke).Result?.Succeeded == true, "折扣后的 Seal 应成功");
        Assert(medium.Get<double>("charges") == 1 && target.Get<bool>("sealed"), "SelfBinding 应将 2 点成本折为 1 点");

        var breakIntent = new Intent(actor, soul, F("open", actor, target), [F("set", target, "opened", true)]);
        Assert(session.Runtime.ResolveDetailed(breakIntent).Result?.Succeeded == true, "破缚行动应成功");
        Assert(!binding.Get<bool>("active") && actor.Get<Entity?>("self_binding") == null, "破缚后绑定应失效");

        var target2 = new Entity("SealTarget2", world, properties: new Dictionary<string, object> { ["sealed"] = false });
        var medium2 = new Entity("Medium2", actor, properties: new Dictionary<string, object> { ["charges"] = 2d });
        var seal2 = Patterns.Seal(actor, actor, target2, medium2, strength: 2); book.Add(seal2);
        StateView.Learn(soul, seal2, "text", seal2.Get<string>("text"));
        var invoke2 = book.Offers(soul, actor).Single(i => i.Effect.Op == "invoke_seal" && i.Definition == seal2);
        Assert(session.Runtime.ResolveDetailed(invoke2).Result?.Succeeded == true && medium2.Get<double>("charges") == 0, "破缚后应恢复 Seal 原价");
        session.Dispose();
    }

    private static void SealMaintenanceExhausts()
    {
        var (world, actor, soul, session) = Setup();
        var target = new Entity("PersistentTarget", world, properties: new Dictionary<string, object> { ["sealed"] = false });
        var medium = new Entity("PersistentMedium", actor, properties: new Dictionary<string, object> { ["charges"] = 2d });
        using var book = new PatternBook(session);
        var seal = Patterns.Seal(actor, actor, target, medium); book.Add(seal);
        StateView.Learn(soul, seal, "text", seal.Get<string>("text"));
        var invoke = book.Offers(soul, actor).Single(i => i.Effect.Op == "invoke_seal");
        Assert(session.Runtime.ResolveDetailed(invoke).Result?.Succeeded == true, "持续 Seal 初次施放应成功");
        session.Timeline.RunUntil(41);
        Assert(medium.Get<double>("charges") == 0 && !target.Get<bool>("sealed"), "20 AV 维护两次后耗尽应释放封印");
        session.Dispose();
    }

    private static void LiteraryDeflection()
    {
        using var sim = new LiterarySimulation("Deflection");
        var scene = sim.Scene("Scene");
        var goal = new Entity("Goal", scene);
        var attacker = sim.Person("Attacker", scene, goal, "marked", false);
        var victim = sim.Person("Victim", scene, goal, "marked", false);
        var mirror = HauntedItems.Create(HauntedItem.Mirror, "Mirror", victim, attacker);
        victim.Set("mirror", mirror);
        var attack = new Intent(attacker, attacker.Mind!, F("hurt", attacker, victim), [F("change", victim, "strain", 2d)]);
        var result = sim.Session.Runtime.ResolveDetailed(attack);
        Assert(result.Result?.Succeeded == true, "反制行动应结算成功");
        Assert(victim.Mind!.Fatigue == 0 && attacker.Mind!.Fatigue == 2, "镜面反制不得复制原受害者伤害");
        Assert(mirror.Get<double>("integrity") == 1, "镜面反制应扣除有限完整度");

        var effigy = HauntedItems.Create(HauntedItem.Effigy, "Effigy", victim, attacker);
        victim.Set("substitute", effigy);
        var attack2 = new Intent(attacker, attacker.Mind!, F("hurt", attacker, victim), [F("change", victim, "strain", 1d)]);
        Assert(sim.Session.Runtime.ResolveDetailed(attack2).Result?.Succeeded == true, "纸人反制行动应结算成功");
        Assert(victim.Mind!.Fatigue == 0 && effigy.Get<double>("charges") == 1, "纸人应只消耗有限次数且不复制伤害");
    }

    private static void ConsequenceForecasting()
    {
        using var sim = new LiterarySimulation("Forecast");
        var scene = sim.Scene("Scene");
        var otherScene = sim.Scene("OtherScene");
        var goal = new Entity("Goal", scene);
        var actor = sim.Person("Actor", scene, goal, "marked", false);
        var target = sim.Object("Target", scene);
        var known = Patterns.Prohibit(actor, actor, "open", target, breach: [F("change", actor, "strain", 2d)]);
        sim.Rules.Add(known); sim.Perceive(actor);
        var intent = new Intent(actor, actor.Mind!, F("open", actor, target), [F("set", target, "opened", true)]);
        var plan = sim.Predict(intent);
        Assert(plan.Changes.Any(c => c.Subject == actor && c.Field == "strain" && StateView.Same(c.After, 2d)), "已知禁令预测应包含违约 strain 后果");
        Assert(actor.Mind!.Fatigue == 0 && actor.Get<double>("charges") == 12, "预测不得改变客观真值");

        var outsider = sim.Person("Outsider", otherScene, goal, "marked", false);
        var unknown = Patterns.Prohibit(outsider, actor, "open", target, breach: [F("change", actor, "strain", 3d)]);
        sim.Rules.Add(unknown);
        var unknownPlan = sim.Predict(intent);
        Assert(!unknownPlan.Changes.Any(c => c.Subject == actor && c.Field == "strain" && StateView.Same(c.After, 3d)), "未知规则不得暴露预测后果");
    }
}
