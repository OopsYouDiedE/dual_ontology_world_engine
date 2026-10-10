using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Story;

/// <summary>Atomic environmental property updates using the same Effect AST as actions.
/// This entry point cannot move bodies, speak, think, or execute action procedures.</summary>
public static class WorldLawTransaction
{
    public static bool Apply(WorldRuntime runtime, Effect effect, Func<Projection, Essence?> resolve,
        double time, out TypedEventV1<Essence?>? receipt, IReadOnlyList<EventId>? causes = null)
    {
        var pending = new Dictionary<(Essence Entity, Tag Tag), int>();
        Essence Entity(Noun noun) => noun is Noun.Reference r && r.Projection.Existence == Truth.True && resolve(r.Projection) is { } e
            ? e : throw new InvalidDataException("World law requires an existing bound reference.");
        int Value(Essence e, Tag tag) => pending.TryGetValue((e, tag), out var v) ? v
            : e.Get<TemporaryStateProperty>()?.Override(tag) ??
              (tag == Tags.Locked && e.Get<LockProperty>() is { } l ? l.IsLocked ? 1 : 0 : e.Get<StateProperty>()?.Values.GetValueOrDefault(tag) ?? 0);
        Truth Test(Condition c) => c switch
        {
            Condition.Constant v => v.Value,
            Condition.Exists v => Ast.From(runtime.Representations(Entity(v.Target)).Any(p => resolve(p) is not null)),
            Condition.Tagged v => Ast.From(Value(Entity(v.Target), v.Tag) >= v.Minimum),
            Condition.At v => Ast.From(ReferenceEquals(Entity(v.Target).Get<SpatialProperty>()?.Space, Entity(v.Space))),
            Condition.IsKind v => Ast.From(Entity(v.Target).Kind == v.Kind),
            Condition.Related v => Ast.From(Entity(v.Target).Relations.Contains((v.Relation, Entity(v.Other)))),
            Condition.Not v => Ast.Negate(Test(v.Operand)),
            Condition.All v => Ast.All(v.Items.Select(Test)),
            Condition.Any v => Ast.Negate(Ast.All(v.Items.Select(item => Ast.Negate(Test(item))))),
            _ => throw new InvalidDataException("Unsupported world law condition.")
        };
        bool Write(Noun n, Tag tag, long v)
        {
            if (v is < int.MinValue or > int.MaxValue) return false;
            pending[(Entity(n), tag)] = (int)v; return true;
        }
        bool Prepare(Effect e) => e switch
        {
            Effect.Sequence v => v.Items.All(Prepare),
            Effect.When v => Test(v.Condition) != Truth.True || Prepare(v.Body),
            Effect.Require v => Test(v.Condition) == Truth.True,
            Effect.Set v => Write(v.Target, v.Tag, v.Value),
            Effect.Change v => Write(v.Target, v.Tag, (long)Value(Entity(v.Target), v.Tag) + v.Amount),
            Effect.Spend v => v.Amount >= 0 && Value(Entity(v.Target), v.Tag) >= v.Amount && Write(v.Target, v.Tag, (long)Value(Entity(v.Target), v.Tag) - v.Amount),
            _ => throw new InvalidDataException("World laws may only transact properties; actions require a subject decision.")
        };
        receipt = null;
        if (!Prepare(effect)) return false;
        if (pending.Count == 0) return true;
        foreach (var ((entity, tag), value) in pending)
            if (tag == Tags.Locked && entity.Get<LockProperty>() is { } l) l.IsLocked = value > 0;
            else (entity.Get<StateProperty>() ?? entity.Add(new StateProperty())).Values[tag] = value;
        runtime.Sync();
        receipt = new ObservationRecordV1<Essence?>(new()
        {
            Time = time, TimeUnit = ObservationTimeUnitV1.ActionValue,
            Phase = EventPhaseV1.Committed, Outcome = EventOutcomeV1.Succeeded,
            KnowledgeSource = ObservationSource.Perception, Causes = causes is null ? [] : [..causes],
            Claims = [..pending.Select(v => new EventClaimV1<Essence?>(new("world.state"), v.Key.Entity, null, null, v.Key.Tag, v.Value, Truth.True))]
        });
        runtime.RecordEvent(receipt);
        return true;
    }
}
