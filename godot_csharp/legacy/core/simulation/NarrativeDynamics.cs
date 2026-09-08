using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Timeline;
using static DualOntologyWorldEngine.Core.Rules.AST;

namespace DualOntologyWorldEngine.Core.Simulation;

public sealed record Motive(Character Actor, string Name, Entity Target, string Field, object Desired, double Importance, Expr? When = null)
{
    public bool Achieved { get; set; }
    public Entity? Cause { get; set; }
}

public sealed class Opportunity : Entity
{
    public Character Actor { get; }
    public Entity Target { get; }
    public bool Announced { get; }
    public bool Used { get; set; }

    public Opportunity(string name, Character actor, Entity target, bool announced = false)
        : base(name)
    {
        Actor = actor;
        Target = target;
        Announced = announced;
    }
}

public static class NarrativeCharacterExtensions
{
    public static double GetInvestigation(this Entity actor) => actor.Get<double>("investigation");
    public static void SetInvestigation(this Entity actor, double value) => actor.Set("investigation", value);

    public static double GetReflectionCredit(this Entity actor) => actor.Get<double>("reflection_credit");
    public static void SetReflectionCredit(this Entity actor, double value) => actor.Set("reflection_credit", value);

    public static double GetReflectAfter(this Entity actor) => actor.Get<double>("reflect_after");
    public static void SetReflectAfter(this Entity actor, double value) => actor.Set("reflect_after", value);
}

/// <summary>作品的多重欲望、有限机会与实证成长。作者定义条件和代价，角色选择行动顺序。</summary>
public sealed class NarrativeDynamics : IDisposable
{
    private readonly SimulationSession _session;
    private readonly List<Motive> _motives = [];
    private readonly List<Intent> _opportunities = [];
    private readonly HashSet<(Soul, Entity)> _studied = [];
    private readonly Func<Intent, StateView, EffectPlan, string?> _guard;
    public IReadOnlyList<Motive> Motives => _motives;

    public NarrativeDynamics(SimulationSession session)
    {
        _session = session;
        session.Runtime.Executed += Remember;
        _guard = (intent, _, _) => intent.Definition is Opportunity { Used: true } ? "机会已使用" : null;
        session.Runtime.Guards.Add(_guard);
    }

    /// <summary>持续在意的世界状态。达成记录只记一次，之后失去该状态仍有负价值。</summary>
    public Motive Care(Character actor, string name, Entity target, string field, object desired, double importance, Expr? when = null)
    {
        if (!double.IsFinite(importance) || importance <= 0) throw new ArgumentOutOfRangeException(nameof(importance));
        var motive = new Motive(actor, name, target, field, desired, importance, when);
        _motives.Add(motive);
        return motive;
    }

    /// <summary>可信作品装配入口；每个机会有一次性结算标记，依赖真实状态解锁，不规定发生时刻。</summary>
    public Opportunity Opportunity(Character actor, string name, Entity target, IEnumerable<Effect> changes,
        IEnumerable<Expr>? requirements = null, double cost = 10, string operation = "choose", bool announced = false)
    {
        var opportunity = new Opportunity(name, actor, target, announced);
        var intent = new Intent(actor, actor.Mind!, F(operation, actor, target),
            changes, cost, name, requirements, validate: () => !opportunity.Used) { Definition = opportunity };
        _opportunities.Add(intent);
        return opportunity;
    }

    /// <summary>同场明确公开的行动提议。只供可能性推演，不代替提议者决策或授予执行权。</summary>
    public IEnumerable<Intent> SharedOffers(Entity observer) => _opportunities.Where(i =>
        i.Actor != observer && i.Actor.Parent == observer.Parent && i.Definition is Opportunity { Announced: true, Used: false });

    public IEnumerable<Intent> Offers(Character actor)
    {
        foreach (var intent in _opportunities.Where(i => i.Actor == actor && i.Definition is Opportunity { Used: false }))
            yield return intent;
        if (actor.GetReflectionCredit() <= 0 || _session.Timeline.Now < actor.GetReflectAfter()) yield break;
        var skill = actor.GetInvestigation();
        yield return new Intent(actor, actor.Mind!, F("reflect", actor),
            [F("set", actor, "investigation", skill + 2 * (1 - skill / 100)),
             F("spend", actor, "reflection_credit", 1d), F("set", actor, "reflect_after", _session.Timeline.Now + 200)],
            20, "复核亲手查证的证据，修正自己的判断",
            [E("gte", E("value", actor, "reflection_credit"), 1d)]);
    }

    public double Value(Intent intent, EffectPlan plan) => Value(intent.Actor, plan) + (intent.Effect.Op == "reflect" ? 3 : 0);

    public double Value(Entity actor, EffectPlan plan)
    {
        var before = plan.View.Fork();
        foreach (var change in plan.Changes.AsEnumerable().Reverse()) before.Write(change.Subject, change.Field, change.Before);
        double value = 0;
        foreach (var motive in _motives.Where(m => m.Actor == actor))
        {
            if (motive.When is { } condition && !Equals(before.Value(condition), true)) continue;
            var target = motive.Target;
            var field = motive.Field;
            var desired = motive.Desired;
            var changes = plan.Changes.Where(c => c.Subject == target && c.Field == field).ToArray();
            if (changes.Length == 0) continue;
            var old = changes[0].Before;
            var next = changes[^1].After;
            if (old == StateView.Unknown || next == StateView.Unknown) continue;
            value += motive.Importance *
                ((StateView.Same(next, desired) ? 1 : 0) - (StateView.Same(old, desired) ? 1 : 0));
        }
        return value;
    }

    private void Remember(Intent intent, ActionResolution result)
    {
        if (result.Result?.Succeeded != true) return;
        if (intent.Definition is Opportunity opp) opp.Used = true;
        foreach (var motive in _motives.Where(m => m.Actor == intent.Actor && !m.Achieved))
        {
            var target = motive.Target;
            var field = motive.Field;
            if (result.Result.Outcome is not StateChange[] outcome || !outcome.Any(c =>
                c.Subject == target && c.Field == field && StateView.Same(c.After, motive.Desired))) continue;
            motive.Achieved = true;
            motive.Cause = result.Event;
        }
        // 获得证据本身必须出现在这次提交里；单纯说话、等待和候选预测不给经验。
        if (intent.Effect.Op != "inspect" || result.Result.Outcome is not StateChange[] changes) return;
        foreach (var change in changes.Where(c => c.Subject == intent.Mind && c.Field == "KnownState"))
        {
            if (change.After is not Dictionary<(Entity, string), object?> known) continue;
            var previous = change.Before as Dictionary<(Entity, string), object?>;
            foreach (var ((source, field), evidence) in known)
            {
                if (field != "evidence" || evidence == null || evidence == StateView.Unknown
                    || !StateView.Same(evidence, StateView.Actual(source, field))
                    || previous != null && previous.TryGetValue((source, field), out var old) && StateView.Same(old, evidence)
                    || !_studied.Add((intent.Mind, source))) continue;
                intent.Actor.SetReflectionCredit(intent.Actor.GetReflectionCredit() + 1);
                result.Event!.Relate("LearningSource", source);
            }
        }
    }

    public void Dispose()
    {
        _session.Runtime.Executed -= Remember;
        _session.Runtime.Guards.Remove(_guard);
    }
}
