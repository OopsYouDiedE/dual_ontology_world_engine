using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

var failed = 0;
var passed = 0;
void Run(string name, Action test)
{
    try { test(); Console.WriteLine($"通过：{name}"); passed++; }
    catch (Exception error) { Console.Error.WriteLine($"失败：{name}\n{error}"); failed++; }
}
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
void Reject<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new InvalidOperationException($"应拒绝并报告 {typeof(T).Name}");
}

Run("误认钥匙：解码规则、延迟失败、观察线索、规则推理、开锁、通行", () =>
{
    var f = new Fixture();
    var keyIdentity = new Essence("档案钥匙", ProjectionKind.Concept);
    var ordinaryIdentity = new Essence("普通钥匙", ProjectionKind.Concept);
    var correct = f.World.Project(keyIdentity, "档案钥匙");
    var ordinary = f.World.Project(ordinaryIdentity, "普通钥匙");
    var keyA = f.Item("甲钥匙", held: true);
    var keyB = f.Item("乙钥匙", held: true);
    keyA.Essence.Relations.Add((Relations.HasIdentity, keyIdentity));
    keyB.Essence.Relations.Add((Relations.HasIdentity, ordinaryIdentity));
    f.Door.Relations.Add((Relations.AcceptsKey, keyIdentity));
    var symbol = new Tag("ArchiveSymbol");
    keyA.Essence.Add(new SurfaceProperty()).Surfaces[keyA.View] = ImmutableDictionary<Tag, int>.Empty.Add(symbol, 1);
    f.World.Sync();
    var concepts = f.World.Observe(f.Soul, [correct, ordinary]);
    // 角色过去获得了错误的钥匙身份；初始记忆同样经过 Observe。
    var mentalA = Observation.Observe(f.Soul, new("甲钥匙") { Source = ObservationSource.Recall, Existence = Truth.True,
        AddRelations = [(Relations.HasIdentity, concepts[1])] });
    var mentalB = Observation.Observe(f.Soul, new("乙钥匙") { Source = ObservationSource.Recall, Existence = Truth.True,
        AddRelations = [(Relations.HasIdentity, concepts[0])] });
    f.World.Associate(f.Soul, mentalA, keyA.View);
    f.World.Associate(f.Soul, mentalB, keyB.View);
    var decoder = new AstDecoder(f.Soul.World, [symbol]);
    f.Learn(decoder.DecodeRules("RULE related($Tool, HasIdentity, @0) => PREFER unlock(@1, $Tool)", concepts[0], f.MentalDoor));
    var inference = decoder.DecodeRules("RULE all(tag_at_least(@0, ArchiveSymbol, 1), related(@0, HasIdentity, @1)) => PREFER sequence(infer(not(related(@0, HasIdentity, @1))), infer(related(@0, HasIdentity, @2)))", mentalA, concepts[1], concepts[0]);
    f.Learn([..inference.Select(r => r with { Weight = 10 })]);
    var first = f.Session.Decide(f.Soul, f.MentalBody, 5)!;
    Check(first.Intent.Changes is Effect.Act { Tool: Noun.Reference r } && ReferenceEquals(r.Projection, mentalB), "第一轮必须依据错误认知选乙钥匙");
    f.Session.CTB.RunUntil(4);
    Check(first.Status == ExecutionStatus.Pending && f.Door.Get<LockProperty>()!.IsLocked, "不得提前结算");
    f.Session.CTB.Step();
    Check(first.Status == ExecutionStatus.Failed, "错误钥匙应在客观结算时失败");
    var inspect = f.Session.Submit(f.Soul, new(f.MentalBody, decoder.DecodeEffect("inspect(@0)", mentalA)));
    f.Session.CTB.Step();
    Check(inspect.Status == ExecutionStatus.Completed && mentalA.Tags.ContainsKey(symbol), "观察摄入刻痕");
    var think = f.Session.Decide(f.Soul, f.MentalBody)!;
    Check(think.Intent.Changes is Effect.Sequence, "推理须由内部规则选出");
    f.Session.CTB.Step();
    Check(mentalA.Relations.Contains((Relations.HasIdentity, concepts[0])), "推理通过 Observe 改写身份");
    var second = f.Session.Decide(f.Soul, f.MentalBody)!;
    f.Session.CTB.Step();
    Check(second.Status == ExecutionStatus.Completed && !f.Door.Get<LockProperty>()!.IsLocked, "依据新认知开锁");
    Check(f.MentalDoor.Tags[Tags.Locked] == 1, "执行成功不能直接改写门的认知");
    Check(f.NearDoor.Tags[Tags.Locked] == 0 && f.FarDoor.Tags[Tags.Locked] == 0, "两侧现实表现共享同一锁状态");
    f.Session.Submit(f.Soul, new(f.MentalBody, decoder.DecodeEffect("travel(@0)", f.MentalDoor)));
    f.Session.CTB.Step();
    Check(ReferenceEquals(f.Body.Get<SpatialProperty>()!.Space, f.Archive), "通行根据真实身体位置结算");
});

Run("存在认知：看见但否认、无法识别、未知真实对象、幻觉、已消失对象", () =>
{
    var f = new Fixture();
    var hidden = f.Item("秘密");
    Check(!f.Soul.World.Projections.Any(p => p.Name == "秘密"), "不能预建隐藏对象占位");
    var denied = f.World.Observe(f.Soul, [hidden.View], transform: new TestTransform(i => i with { Existence = Truth.False, Kind = ProjectionKind.Object, Name = "不明痕迹" }))[0];
    Check(denied.Existence == Truth.False && denied.LastObservation?.Detected == true, "观察证据和存在判断独立");
    var hallucination = Observation.Observe(f.Soul, new("幻觉物体") { Existence = Truth.True, Detected = true });
    var action = f.Session.Submit(f.Soul, new(f.MentalBody, new Effect.Act(ActionOp.Take, hallucination), 3));
    Check(action.Status == ExecutionStatus.Pending, "幻觉目标不能被候选过滤提前拒绝");
    f.Session.CTB.Step();
    Check(action.Result?.Failure == ActionFailure.MissingObject && hallucination.Existence == Truth.True, "失败反馈不能泄露幻觉真相");
    var target = f.Item("会消失的物体");
    var known = f.World.Observe(f.Soul, [target.View])[0];
    f.Session.Submit(f.Soul, new(f.MentalBody, new Effect.Act(ActionOp.Destroy, known)));
    f.Session.CTB.Step();
    Check(target.View.Existence == Truth.False && known.Existence == Truth.True, "客观消失不自动删除记忆");
    f.World.Observe(f.Soul, [target.View]);
    Check(known.Existence == Truth.False && ReferenceEquals(known, f.Soul.World.Projections.First(p => p.Name == known.Name)), "否定观察保留身份");
});

Run("通道两侧不替角色推断同一身份；侵蚀保留原有引用与绑定", () =>
{
    var f = new Fixture();
    var sideB = f.World.Observe(f.Soul, [f.FarDoor])[0];
    Check(!ReferenceEquals(sideB, f.MentalDoor), "两侧表现允许不同主观表征");
    var concept = Observation.Observe(f.Soul, new("垃圾", ProjectionKind.Concept));
    Cognition.Corrupt(f.Soul, new TestTransform(i => ReferenceEquals(i.Target, f.MentalDoor)
        ? i with { Kind = ProjectionKind.Object, AddRelations = [(Relations.IsA, concept)] } : null));
    Check(f.MentalDoor.Kind == ProjectionKind.Object && f.MentalDoor.Relations.Contains((Relations.IsA, concept)), "侵蚀原地改变认知类别");
    f.Door.Get<LockProperty>()!.IsLocked = false;
    f.Session.Submit(f.Soul, new(f.MentalBody, new Effect.Act(ActionOp.Travel, f.MentalDoor)));
    f.Session.CTB.Step();
    Check(ReferenceEquals(f.Body.Get<SpatialProperty>()!.Space, f.Archive), "认知改类后客观绑定仍有效");
});

Run("世界隔离：关系、AST、父子树和解析嵌入都不能跨越认知边界", () =>
{
    var f = new Fixture();
    var other = new SoulEssence("另一个人");
    var foreign = Observation.Observe(other, new("异界对象"));
    var count = f.Soul.World.Projections.Length;
    Reject<InvalidOperationException>(() => Observation.Observe(f.Soul, new("错误") { AddRelations = [(Relations.IsA, foreign)] }));
    Reject<InvalidOperationException>(() => Observation.Observe(f.Soul, new("错误") { Procedure = new Effect.Act(ActionOp.Inspect, foreign) }));
    Reject<InvalidOperationException>(() => Observation.Observe(f.Soul, new("错误") { Move = true, Parent = foreign }));
    Check(f.Soul.World.Projections.Length == count, "失败提交不留下半个对象");
    Reject<AstParseException>(() => new AstDecoder(f.Soul.World).DecodeEffect("inspect(@0)", f.NearDoor));
    Reject<InvalidOperationException>(() => Observation.Observe(f.Soul, new(f.MentalBody.Name, f.MentalBody.Kind)
        { Target = f.MentalBody, Move = true, Parent = f.MentalBody }));
});

Run("批量感知映射图与抑制：嵌入不泄露现实引用或被屏蔽对象", () =>
{
    var f = new Fixture();
    var thing = f.Item("新对象");
    var sign = f.Item("告示");
    sign.Essence.Add(new ProcedureProperty(new Effect.Act(ActionOp.Inspect, thing.View)));
    f.World.Sync();
    var observed = f.World.Observe(f.Soul, [sign.View, thing.View]);
    Check(observed[0].Procedure is Effect.Act { Target: Noun.Reference r } && ReferenceEquals(r.Projection, observed[1]), "前向嵌入应映射到本批次主观对象");
    var other = new SoulEssence("新观察者");
    var result = f.World.Observe(other, [sign.View, thing.View], transform: new TestTransform(i => i.Name == "新对象" ? null : i));
    Check(result.Length == 1 && result[0].Procedure is null && other.World.Projections.Length == 1, "被抑制对象不能通过 AST 进入认知");
});

Run("AST：文本/嵌入一致、歧义、类型诊断、自定义 Tag、未知否定", () =>
{
    var f = new Fixture();
    var energy = new Tag("Energy");
    var parser = new AstDecoder(f.Soul.World, [energy]);
    Check(Ast.Equivalent(parser.DecodeEffect("inspect(@门)"), parser.DecodeEffect("inspect(@0)", f.MentalDoor)), "文本和嵌入统一");
    var decoded = parser.DecodeEffect("sequence(spend($Actor, Energy, 2), wait(3, set($Actor, Energy, 5)))");
    Check(decoded is Effect.Sequence && !f.Body.Get<StateProperty>()!.Values.ContainsKey(energy), "解码没有效果");
    Observation.Observe(f.Soul, new("门"));
    Reject<AstParseException>(() => parser.DecodeEffect("inspect(@门)"));
    Reject<AstParseException>(() => parser.DecodeEffect("inspect(@不存在)"));
    Reject<AstParseException>(() => parser.DecodeEffect("set(@0, Energ, 1)", f.MentalBody));
    Reject<AstParseException>(() => parser.DecodeEffect("spend(@0, Energy, -1)", f.MentalBody));
    Reject<AstParseException>(() => parser.DecodeEffect("unlock(@0)", f.MentalDoor));
    var unknown = new Condition.Not(new Condition.Tagged(f.MentalBody, energy, 1));
    Check(Ast.Evaluate(unknown, new(f.MentalBody)) == Truth.Unknown, "Not(未知) 仍为未知");
    try { parser.DecodeEffect("\n  inspect(@不存在)"); }
    catch (AstParseException error) { Check(error.Line == 2 && error.Column > 2, "保留行列诊断"); }
});

Run("原子结算：多次支付累积审查，失败不消耗，前置条件读取暂存变化", () =>
{
    var f = new Fixture();
    var energy = new Tag("Energy");
    f.Body.Get<StateProperty>()!.Values[energy] = 5;
    var parser = new AstDecoder(f.Soul.World, [energy]);
    var attempt = f.Session.Submit(f.Soul, new(f.MentalBody, parser.DecodeEffect("sequence(spend($Actor, Energy, 3), spend($Actor, Energy, 3))")));
    f.Session.CTB.Step();
    Check(attempt.Status == ExecutionStatus.Failed && f.Body.Get<StateProperty>()!.Values[energy] == 5, "失败支付必须整体回滚");
    var success = f.Session.Submit(f.Soul, new(f.MentalBody, parser.DecodeEffect("sequence(spend($Actor, Energy, 3), require(not(tag_at_least($Actor, Energy, 3))), change($Actor, Energy, 1))")));
    f.Session.CTB.Step();
    Check(success.Status == ExecutionStatus.Completed && f.Body.Get<StateProperty>()!.Values[energy] == 3, "同阶段条件必须读到前面计划的变化");
});

Run("嵌套 CTB：子阶段与其他行动交错，取消后续阶段不撤销已完成效果", () =>
{
    var f = new Fixture();
    var energy = new Tag("Energy");
    var parser = new AstDecoder(f.Soul.World, [energy]);
    var execution = f.Session.Submit(f.Soul, new(f.MentalBody, parser.DecodeEffect("sequence(set($Actor, Energy, 1), wait(5, sequence(change($Actor, Energy, 2), wait(3, change($Actor, Energy, 4)))), change($Actor, Energy, 8))"), 2));
    var interleaved = false;
    f.Session.CTB.Schedule(4, () => interleaved = true);
    f.Session.CTB.RunUntil(6);
    Check(interleaved && f.Body.Get<StateProperty>()!.Values[energy] == 1, "其他动作在父行动等待期间继续执行");
    f.Session.CTB.RunUntil(7);
    Check(f.Body.Get<StateProperty>()!.Values[energy] == 3 && execution.Status == ExecutionStatus.Pending, "嵌套阶段等待未结束");
    f.Session.CTB.RunUntil(10);
    Check(f.Body.Get<StateProperty>()!.Values[energy] == 15 && execution.Status == ExecutionStatus.Completed, "子阶段后恢复父序列");
    var cancelled = f.Session.Submit(f.Soul, new(f.MentalBody, parser.DecodeEffect("sequence(set($Actor, Energy, 1), wait(5, set($Actor, Energy, 99)))")));
    f.Session.CTB.Step();
    Check(f.Session.Cancel(cancelled), "可取消等待中的父行动");
    f.Session.CTB.RunUntil(20);
    Check(f.Body.Get<StateProperty>()!.Values[energy] == 1 && cancelled.Status == ExecutionStatus.Cancelled, "取消不撤销先前阶段");
});

Run("排队期间目标变化与身体控制丢失只在激活时结算", () =>
{
    var f = new Fixture();
    f.Door.Get<LockProperty>()!.IsLocked = false;
    var travel = f.Session.Submit(f.Soul, new(f.MentalBody, new Effect.Act(ActionOp.Travel, f.MentalDoor), 5));
    f.Session.CTB.Schedule(2, () => f.Door.Get<LockProperty>()!.IsLocked = true);
    f.Session.CTB.RunUntil(5);
    Check(travel.Status == ExecutionStatus.Failed && ReferenceEquals(f.Body.Get<SpatialProperty>()!.Space, f.Hall), "不能使用提交时的客观快照");
    var next = f.Session.Submit(f.Soul, new(f.MentalBody, new Effect.Act(ActionOp.Inspect, f.MentalDoor), 1));
    f.Soul.Relations.Clear();
    f.Session.CTB.Step();
    Check(next.Result?.Failure == ActionFailure.NoControl, "失去身体控制后不得执行");
});

Run("社交请求：接收者词项隔离、独立历史、态度只提供决策输入", () =>
{
    var f = new Fixture();
    var receiver = new SoulEssence("乙");
    var body = new Essence("乙身体", ProjectionKind.Body);
    body.Add(new SpatialProperty { Space = f.Hall });
    receiver.Relations.Add((Relations.Controls, body));
    var bodyView = f.World.Project(body, "乙身体", f.HallView);
    f.World.Project(receiver, "乙");
    var receiverBody = f.World.Observe(receiver, [f.HallView, bodyView])[1];
    Observation.Observe(receiver, new(receiverBody.Name, receiverBody.Kind) { Target = receiverBody, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Cooperation, 2) });
    var recipient = f.World.Observe(f.Soul, [bodyView])[0];
    var energy = new Tag("Energy");
    var speech = new AstDecoder(f.Soul.World, [energy]).DecodeEffect("say(@0, \"请恢复能量\", set($Actor, Energy, 7))", recipient);
    f.Session.Submit(f.Soul, new(f.MentalBody, speech, 3));
    Check(!receiver.World.Projections.Any(p => p.Kind == ProjectionKind.Event), "请求到期前不可见");
    f.Session.CTB.RunUntil(3);
    var request = receiver.World.Projections.Single(p => p.Kind == ProjectionKind.Event);
    Check(Ast.References(request.Procedure).All(receiver.World.Contains), "请求无发送者世界引用");
    var attitude = SocialSystem.Evaluate(receiver.World, receiverBody, request);
    Check(attitude.Stance == SocialStance.Accept && body.Get<StateProperty>() is null, "态度评价不执行请求");
    var response = f.Session.Decide(receiver, receiverBody)!;
    f.Session.CTB.Step();
    Check(response.Status == ExecutionStatus.Completed && body.Get<StateProperty>()!.Values[energy] == 7, "请求中的 Actor 槽位属于接收者");
    var sender = request.Relations.Single(r => r.Relation == Relations.Subject).Target;
    Observation.Observe(receiver, new("曾被欺骗", ProjectionKind.Event) { Source = ObservationSource.Recall,
        AddRelations = [(Relations.Subject, sender)], Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, -5) });
    Check(SocialSystem.Evaluate(receiver.World, receiverBody, request).Stance == SocialStance.Reject, "个人已知历史改变态度");
});

Run("CTB 稳定次序、改期、跨队列取消隔离与步数预算", () =>
{
    var ctb = new CTBSystem();
    var trace = new List<int>();
    var first = ctb.Schedule(3, () => trace.Add(1));
    ctb.Schedule(3, () => trace.Add(2));
    Check(!new CTBSystem().Cancel(first), "其他队列不能取消本队列项");
    ctb.Reschedule(first, 3);
    Check(ctb.RunUntil(9, 1) == 1 && ctb.Now == 3, "预算耗尽不能跳过待执行动作");
    ctb.RunUntil(9);
    Check(trace.SequenceEqual([1, 2]), "同刻按原登记顺序");
    Reject<ArgumentOutOfRangeException>(() => ctb.Schedule(double.NaN, () => { }));
    Reject<ArgumentOutOfRangeException>(() => ctb.RunUntil(1));
    Reject<ArgumentOutOfRangeException>(() => CTBSystem.ActionValue(0));
});

Run("自主决策：无候选休眠，Observe 唤醒，执行后重新依据认知选择", () =>
{
    var f = new Fixture();
    f.Session.Activate(f.Soul, f.MentalBody, 2);
    f.Session.CTB.RunUntil(1);
    Check(!f.Session.CTB.Pending.Any(), "没有候选不应忙轮询");
    var energy = new Tag("Energy");
    f.Body.Get<StateProperty>()!.Values[energy] = 0;
    f.Body.Get<StateProperty>()!.Observable.Add(energy);
    f.World.Sync();
    f.World.Observe(f.Soul, f.World.Representations(f.Body), f.Session.CTB.Now);
    var parser = new AstDecoder(f.Soul.World, [energy]);
    f.Learn(parser.DecodeRules("RULE not(tag_at_least($Actor, Energy, 1)) => PREFER sequence(set($Actor, Energy, 1), inspect($Actor))"));
    f.Session.CTB.RunUntil(10);
    Check(f.Body.Get<StateProperty>()!.Values[energy] == 1 && !f.Session.CTB.Pending.Any(), "完成后更新认知，规则不再命中并休眠");
    f.Session.Deactivate(f.MentalBody);
    Observation.Observe(f.Soul, new("新线索"));
    Check(!f.Session.CTB.Pending.Any(), "停用后 Observe 不再唤醒");
});

Run("言灵直接执行已解码流程；不可观察 Property 与本质身份保持隔离", () =>
{
    var f = new Fixture();
    var energy = new Tag("Energy");
    f.Body.Get<StateProperty>()!.Values[energy] = 4;
    f.World.Sync();
    f.World.Observe(f.Soul, f.World.Representations(f.Body));
    Check(!f.MentalBody.Tags.ContainsKey(energy), "未声明可观察的状态不能摄入");
    var spell = Observation.Observe(f.Soul, new("开门术", ProjectionKind.Procedure)
    {
        Source = ObservationSource.Recall,
        Procedure = new AstDecoder(f.Soul.World, [energy]).DecodeEffect("sequence(spend($Actor, Energy, 3), set(@0, Locked, 0))", f.MentalDoor)
    });
    var invocation = f.Session.Invoke(f.Soul, f.MentalBody, spell, 2);
    f.Session.CTB.RunUntil(2);
    Check(invocation.Status == ExecutionStatus.Completed && !f.Door.Get<LockProperty>()!.IsLocked && f.Body.Get<StateProperty>()!.Values[energy] == 1, "言灵共用延迟和原子效果执行");
    var secret = new Essence("本质身份", ProjectionKind.Concept);
    var secretView = f.World.Project(secret, "本质身份");
    f.Body.Relations.Add((Relations.IsA, secret));
    f.World.Sync();
    var seen = f.World.Observe(f.Soul, [..f.World.Representations(f.Body), secretView]);
    Check(!seen[0].Relations.Contains((Relations.IsA, seen[1])), "知道概念存在也不应自动读出对象本质身份");
});

Run("认知撤销、关系否定与遗忘后的重新观察", () =>
{
    var f = new Fixture();
    var item = f.Item("记忆对象");
    var mental = f.World.Observe(f.Soul, [item.View])[0];
    var concept = Observation.Observe(f.Soul, new("类别", ProjectionKind.Concept));
    Cognition.Infer(f.Soul, new Condition.Not(new Condition.Related(mental, Relations.IsA, concept)));
    Check(Ast.Evaluate(new Condition.Related(mental, Relations.IsA, concept), new(f.MentalBody)) == Truth.False, "明确否定区别于未知");
    Observation.Observe(f.Soul, new(mental.Name, mental.Kind) { Target = mental, RemoveRelations = [(Relations.IsA, concept)] });
    Check(Ast.Evaluate(new Condition.Related(mental, Relations.IsA, concept), new(f.MentalBody)) == Truth.Unknown, "撤销恢复未知");
    f.Learn(new AstDecoder(f.Soul.World).DecodeRules("RULE true() => PREFER inspect(@0)", mental));
    Observation.Observe(f.Soul, new(mental.Name, mental.Kind) { Target = mental, Forget = true, Source = ObservationSource.Corruption });
    Check(!f.Soul.World.Contains(mental) && DecisionSystem.Choose(f.Soul.World, f.MentalBody) is null, "遗忘清理依赖该对象的规则");
    var rediscovered = f.World.Observe(f.Soul, [item.View])[0];
    Check(!ReferenceEquals(mental, rediscovered) && f.Soul.World.Contains(rediscovered), "重新观察不能复用已遗忘投影");
});

Run("执行前置条件不能引用其他世界；推理失败不留下部分物理效果", () =>
{
    var f = new Fixture();
    Reject<InvalidOperationException>(() => f.Session.Submit(f.Soul, new(f.MentalBody, new Effect.Sequence([]), Requirements: new Condition.Exists(f.NearDoor))));
    var energy = new Tag("Energy");
    var effect = new Effect.Sequence([new Effect.Set(f.MentalBody, energy, 3), new Effect.Think(new Condition.Constant(Truth.Unknown))]);
    var execution = f.Session.Submit(f.Soul, new(f.MentalBody, effect));
    f.Session.CTB.Step();
    Check(execution.Status == ExecutionStatus.Failed && !f.Body.Get<StateProperty>()!.Values.ContainsKey(energy), "不支持的推理须在提交前失败");
});

Run("社交回应完成后不重复响应同一请求", () =>
{
    var f = new Fixture();
    var person = Observation.Observe(f.Soul, new("请求者", ProjectionKind.Body));
    Observation.Observe(f.Soul, new(f.MentalBody.Name, f.MentalBody.Kind) { Target = f.MentalBody,
        Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Cooperation, 1) });
    var request = Observation.Observe(f.Soul, new("等待请求", ProjectionKind.Event) { Procedure = new Effect.Wait(1, new Effect.Sequence([])),
        Source = ObservationSource.Testimony, AddRelations = [(Relations.Subject, person)] });
    f.Session.Activate(f.Soul, f.MentalBody);
    f.Session.CTB.RunUntil(10);
    Check(request.Tags.GetValueOrDefault(Tags.Responded) == 1 && !f.Session.CTB.Pending.Any(), "完成反馈经 Observe 标记请求并停止重复执行");
});

Run("观察规则不等于采信；已有认知对象不能改绑为另一个真实对象", () =>
{
    var f = new Fixture();
    var sign = f.Item("规则告示");
    sign.Essence.Add(new RuleProperty()).Rules = [new(new Condition.Constant(Truth.True), RuleMode.Prefer,
        new Effect.Act(ActionOp.Inspect, f.NearDoor), Acceptance: Truth.True)];
    f.World.Sync();
    var knowledge = f.World.Observe(f.Soul, [sign.View])[0];
    Check(knowledge.Rules.Single().Acceptance == Truth.Unknown && DecisionSystem.Choose(f.Soul.World, f.MentalBody) is null, "看见规则不能继承发布者的采信状态");
    Observation.Observe(f.Soul, new(knowledge.Name, knowledge.Kind) { Target = knowledge,
        Source = ObservationSource.Introspection, Rules = [..knowledge.Rules.Select(r => r with { Acceptance = Truth.True })] });
    f.World.Observe(f.Soul, [sign.View]);
    Check(knowledge.Rules.Single().Acceptance == Truth.True, "重读相同规则保留主体立场");
    var item = f.Item("另一物体");
    Reject<InvalidOperationException>(() => f.World.Associate(f.Soul, f.MentalDoor, item.View));
});

Run("排队后遗忘推理目标，激活失败不产生部分支付", () =>
{
    var f = new Fixture();
    var target = Observation.Observe(f.Soul, new("即将遗忘的概念", ProjectionKind.Concept));
    var energy = new Tag("Energy");
    f.Body.Get<StateProperty>()!.Values[energy] = 5;
    var execution = f.Session.Submit(f.Soul, new(f.MentalBody,
        new Effect.Sequence([new Effect.Spend(f.MentalBody, energy, 2), new Effect.Think(new Condition.Exists(target))])));
    Observation.Observe(f.Soul, new(target.Name, target.Kind) { Target = target, Forget = true });
    f.Session.CTB.Step();
    Check(execution.Status == ExecutionStatus.Failed && f.Body.Get<StateProperty>()!.Values[energy] == 5, "认知提交的失败必须在物理提交前发现");
});

Console.WriteLine($"验收结果：{passed} 组通过，{failed} 组失败。");
return failed == 0 ? 0 : 1;

sealed class TestTransform(Func<ObservationInput, ObservationInput?> transform) : IProjectionTransform
{
    public ObservationInput? Transform(ObservationInput input) => transform(input);
}

sealed class Fixture
{
    public WorldRuntime World { get; } = new();
    public SoulEssence Soul { get; } = new("甲");
    public Essence Hall { get; } = new("走廊", ProjectionKind.Space);
    public Essence Archive { get; } = new("档案室", ProjectionKind.Space);
    public Essence Body { get; } = new("甲身体", ProjectionKind.Body);
    public Essence Door { get; } = new("通道", ProjectionKind.Boundary);
    public Projection HallView { get; }
    public Projection NearDoor { get; }
    public Projection FarDoor { get; }
    public Projection MentalBody { get; }
    public Projection MentalDoor { get; }
    public SimulationSession Session { get; }
    public Fixture()
    {
        HallView = World.Project(Hall, "走廊");
        var archiveView = World.Project(Archive, "档案室");
        Door.Add(new PassageProperty()).Connections[Hall] = Archive;
        Door.Get<PassageProperty>()!.Connections[Archive] = Hall;
        Door.Add(new LockProperty());
        NearDoor = World.Project(Door, "门", HallView);
        FarDoor = World.Project(Door, "另一侧门", archiveView);
        Body.Add(new SpatialProperty { Space = Hall });
        Body.Add(new StateProperty());
        var bodyView = World.Project(Body, "甲身体", HallView);
        Soul.Relations.Add((Relations.Controls, Body));
        World.Project(Soul, "甲");
        var observed = World.Observe(Soul, [HallView, NearDoor, bodyView]);
        MentalDoor = observed[1];
        MentalBody = observed[2];
        Session = new(World);
    }
    public (Essence Essence, Projection View) Item(string name, bool held = false)
    {
        var item = new Essence(name);
        item.Add(new SpatialProperty { Space = held ? null : Hall, Holder = held ? Body : null });
        return (item, World.Project(item, name, HallView));
    }
    public void Learn(ImmutableArray<Rule> rules) => Observation.Observe(Soul, new("规则", ProjectionKind.Procedure)
    {
        Source = ObservationSource.Introspection, Rules = [..rules.Select(r => r with { Acceptance = Truth.True })]
    });
}

