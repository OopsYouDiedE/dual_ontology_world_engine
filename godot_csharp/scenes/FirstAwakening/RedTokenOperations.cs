using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Game.FirstAwakening;

public sealed partial class RedTokenSimulation
{
    private readonly Dictionary<string, Projection> _holdPolicies = new();
    private void LearnHoldRules(string actor)
    {
        var body = See(actor, actor);
        var handle = See(actor, "handle");
        var signal = _mental.GetValueOrDefault((actor, "confirmation")) ?? ObserveObject(actor, "confirmation");
        var rack = See(actor, "rack");
        var ownsHandle = new Condition.At(handle, body);
        var unfinished = new Condition.Not(new Condition.Tagged(body, Released, 1));
        var acceptedException = new Condition.All([ownsHandle, unfinished, new Condition.Tagged(body, Read, 1), new Condition.Tagged(signal, FilterReady, 1)]);
        Effect daily = Sequence(new Effect.Require(ownsHandle), new Effect.Set(body, Accepted, 1), Set(actor, "rack_record", Accepted, 1), new Effect.Act(ActionOp.Place, handle, rack));
        Effect sustained = Sequence(new Effect.Require(new Condition.All([ownsHandle, new Condition.Tagged(body, Read, 1), new Condition.Tagged(signal, FilterReady, 1), new Condition.At(body, See(actor, "B"))])),
            new Effect.Set(body, Accepted, 1), Set(actor, "rack_record", Accepted, 1), new Effect.Set(body, Released, 0), new Effect.TemporarySetUntilEnd(body, Holding, 1),
            new Effect.Wait(new Condition.Tagged(signal, Confirmed, 1), new Effect.Set(body, Released, 1)));
        _holdPolicies[actor] = Observation.Observe(Souls[actor], new ObservationInput("本人持柄工作规则", ProjectionKind.Procedure)
        {
            Target = _holdPolicies.GetValueOrDefault(actor), Source = ObservationSource.Introspection, Time = Runtime.CTB.Now,
            Rules = [new Rule(new Condition.All([ownsHandle, unfinished]), RuleMode.Prefer, daily, 1, Truth.True),
                new Rule(acceptedException, RuleMode.Prefer, sustained, 10, body.Tags.GetValueOrDefault(Read) == 1 ? Truth.True : Truth.Unknown)]
        });
    }
    private RedTokenResult StartHold(string actor, bool safe)
    {
        if (Flowing || Value("receipt_handle", Received) == 1) return new(false, "手柄正在使用或已经交回。");
        if (!ReferenceEquals(Objects["handle"].Get<SpatialProperty>()!.Holder, Objects[actor])) return new(false, "须先接掌唯一手柄。");
        var body = See(actor, actor);
        ActionExecution execution;
        var due = Runtime.CTB.Now + 1;
        if (safe)
        {
            var effect = Sequence(new Effect.Require(new Condition.All([new Condition.At(body, See(actor, "B")), new Condition.At(See(actor, "handle"), body)])), new Effect.TemporarySetUntilEnd(body, Holding, 1), new Effect.Wait(new Condition.Constant(Truth.False), new Effect.Require(new Condition.Constant(Truth.True))));
            var procedure = RememberProcedure(actor, RedTokenOperation.SafePress, effect, "空载安全试柄");
            execution = Runtime.Invoke(Souls[actor], body, procedure, 1);
        }
        else
        {
            LearnHoldRules(actor);
            var decision = DecisionSystem.Choose(Souls[actor].World, body);
            if (decision is null || !ReferenceEquals(decision.Basis, _holdPolicies[actor])) return new(false, "本人尚未形成可执行的持柄工作规则。");
            execution = Runtime.Submit(Souls[actor], new ActionIntent(body, decision.Effect, 1, Basis: decision.Basis));
        }
        _holding[actor] = execution; _holder = actor;
        Runtime.CTB.RunUntil(due);
        RefreshLocalObservation(actor);
        if (execution.Status == ExecutionStatus.Failed) { _holder = null; Failures.Add("持柄动作失败：" + execution.Result?.Failure); return new(false, "持柄动作失败：" + execution.Result?.Failure); }
        if (execution.Status == ExecutionStatus.Completed) { _holder = null; Snapshot(actor, "本人按日常规则检查后复位"); return new(true, "本人按日常规则检查后复位。"); }
        Events.Add(actor + (safe ? ":空载安全试柄" : ":持续持柄等待具名确认"));
        Snapshot(actor, safe ? "空载安全试柄保持，取消即停流" : "持续持柄等待主厨具名确认");
        return new(true, safe ? "空载试柄按住；放手后弹簧立即停流。" : "持续持柄等待已提交。");
    }
}
