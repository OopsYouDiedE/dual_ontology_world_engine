using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Core;

public sealed record ActionIntent(Projection Actor, Effect Changes, double Delay = 1, Condition? Requirements = null, Projection? Basis = null);
public enum ActionFailure { None, MissingObject, NoControl, OutOfReach, ConditionFailed, InsufficientResource, UnsupportedOperation, InvalidDelay }
public sealed record ActionResolution(bool Succeeded, ActionFailure Failure, double Time);

/// <summary>运行时只在动作激活时读取现实。每个无等待阶段先准备，成功后统一提交。</summary>
public sealed class ActionRuntime(WorldRuntime world)
{
    internal (ActionResolution Result, Effect? Next, double Delay, Condition? Until) Resolve(SoulEssence soul, ActionIntent intent, double time, CTBSystem timeline, Func<Action, bool, Action> retainCleanup)
    {
        if (!soul.World.Contains(intent.Actor)) return (new(false, ActionFailure.MissingObject, time), null, 0, null);
        var actor = world.Resolve(intent.Actor);
        if (actor is null) return (new(false, ActionFailure.MissingObject, time), null, 0, null);
        if (!world.Controls(soul, actor)) return (new(false, ActionFailure.NoControl, time), null, 0, null);
        var plan = new Plan(world, soul, actor, intent.Actor, time, timeline);
        if (intent.Requirements is not null && plan.Evaluate(intent.Requirements) != Truth.True)
            return (new(false, ActionFailure.ConditionFailed, time), null, 0, null);
        plan.Prepare(intent.Changes);
        if (plan.Failure != ActionFailure.None) return (new(false, plan.Failure, time), null, 0, null);
        plan.Commit(retainCleanup);
        return (new(true, ActionFailure.None, time), plan.Next, plan.Delay, plan.Until);
    }

    private sealed record State(ImmutableDictionary<Tag, int> Tags, ImmutableHashSet<(RelationTag Relation, Essence Target)> Relations,
        Essence? Space, Essence? Holder, bool? Locked, bool Exists);

    private sealed class Plan(WorldRuntime world, SoulEssence soul, Essence actor, Projection actorProjection, double time, CTBSystem timeline)
    {
        private readonly Dictionary<Essence, State> _states = [];
        private readonly HashSet<Essence> _changed = [];
        private readonly List<Projection> _inspections = [];
        private readonly List<(SoulEssence Receiver, string Text, Effect? Request)> _messages = [];
        private readonly List<ObservationInput> _thoughts = [];
        private readonly List<(Essence Target, StateLease Lease)> _leases = [];
        public ActionFailure Failure { get; private set; }
        public Effect? Next { get; private set; }
        public double Delay { get; private set; }
        public Condition? Until { get; private set; }

        private State Read(Essence essence)
        {
            if (!_states.TryGetValue(essence, out var state))
            {
                var spatial = essence.Get<SpatialProperty>();
                state = new(essence.Get<StateProperty>()?.Values.ToImmutableDictionary() ?? ImmutableDictionary<Tag, int>.Empty,
                    essence.Relations.ToImmutableHashSet(), spatial?.Space, spatial?.Holder, essence.Get<LockProperty>()?.IsLocked,
                    world.View(essence) is { } view && world.Resolve(view) is not null);
                _states.Add(essence, state);
            }
            return state;
        }
        private void Write(Essence essence, State state) { _states[essence] = state; _changed.Add(essence); }
        private Essence? Resolve(Noun noun)
        {
            var projection = new ActionContext(actorProjection, Scene: actorProjection.Parent).Resolve(noun);
            return world.Resolve(projection) is { } essence && Read(essence).Exists ? essence : null;
        }
        private int? Value(Essence essence, Tag tag)
        {
            var state = Read(essence);
            if (_leases.LastOrDefault(l=>l.Target==essence&&l.Lease.Tag==tag).Lease is {} staged) return staged.Value;
            if (essence.Get<TemporaryStateProperty>()?.Override(tag) is {} temporary) return temporary;
            return tag == Tags.Locked && state.Locked is { } locked ? locked ? 1 : 0 : state.Tags.GetValueOrDefault(tag);
        }
        private Essence? Location(Essence essence)
        {
            var visited = new HashSet<Essence>();
            for (var parent = Read(essence).Holder ?? Read(essence).Space; parent is not null;
                 parent = Read(parent).Holder ?? Read(parent).Space)
            {
                if (!visited.Add(parent) || !Read(parent).Exists) return null;
                if (parent.Kind == ProjectionKind.Space) return parent;
            }
            return null;
        }
        private bool Accessible(Essence essence)
        {
            var visited = new HashSet<Essence>();
            for (var parent = Read(essence).Holder ?? Read(essence).Space; parent is not null;
                 parent = Read(parent).Holder ?? Read(parent).Space)
            {
                if (!visited.Add(parent) || !Read(parent).Exists || Read(parent).Locked == true ||
                    parent.Kind == ProjectionKind.Body && !ReferenceEquals(parent, actor)) return false;
                if (parent.Kind == ProjectionKind.Space) return true;
            }
            return true;
        }
        private bool Reachable(Essence essence) => ReferenceEquals(essence, actor) || Accessible(essence) &&
            Location(actor) is { } room && (ReferenceEquals(Location(essence), room) ||
                essence.Kind == ProjectionKind.Space && ReferenceEquals(essence, room) ||
                essence.Get<PassageProperty>()?.Connections.ContainsKey(room) == true);

        private bool WouldCycle(Essence target, Essence destination)
        {
            var ancestors = new HashSet<Essence>();
            for (Essence? parent = destination; parent is not null; parent = Read(parent).Holder ?? Read(parent).Space)
                if (ReferenceEquals(parent, target) || !ancestors.Add(parent)) return true;
            return false;
        }

        public Truth Evaluate(Condition condition) => condition switch
        {
            Condition.Constant c => c.Value,
            Condition.Exists c => Ast.From(Resolve(c.Target) is not null),
            Condition.Tagged c => Resolve(c.Target) is { } e ? Ast.From(Value(e, c.Tag) >= c.Minimum) : Truth.False,
            Condition.Related c => Resolve(c.Target) is { } e && Resolve(c.Other) is { } other ? Ast.From(Read(e).Relations.Contains((c.Relation, other))) : Truth.False,
            Condition.At c => Resolve(c.Target) is { } e && Resolve(c.Space) is { } room ? Ast.From(ReferenceEquals(Read(e).Holder ?? Read(e).Space, room)) : Truth.False,
            Condition.IsKind c => Resolve(c.Target) is { } e ? Ast.From(e.Kind == c.Kind) : Truth.False,
            Condition.Not c => Ast.Negate(Evaluate(c.Operand)),
            Condition.All c => Ast.All(c.Items.Select(Evaluate)),
            Condition.Any c => Ast.Negate(Ast.All(c.Items.Select(item => Ast.Negate(Evaluate(item))))),
            _ => throw new NotSupportedException()
        };

        public void Prepare(Effect effect)
        {
            if (Failure != ActionFailure.None) return;
            switch (effect)
            {
                case Effect.Sequence sequence:
                    for (var i = 0; i < sequence.Items.Length; i++)
                    {
                        Prepare(sequence.Items[i]);
                        if (Failure != ActionFailure.None) return;
                        if (Next is not null)
                        {
                            Next = new Effect.Sequence([Next, ..sequence.Items.Skip(i + 1)]);
                            return;
                        }
                    }
                    break;
                case Effect.When conditional:
                    if (Evaluate(conditional.Condition) == Truth.True) Prepare(conditional.Body);
                    break;
                case Effect.Require require:
                    if (Evaluate(require.Condition) != Truth.True) Failure = ActionFailure.ConditionFailed;
                    break;
                case Effect.Wait wait:
                    if (wait.Until is not null)
                    {
                        (Next, Until) = (wait.Body, wait.Until);
                        break;
                    }
                    if (!double.IsFinite(wait.Delay) || wait.Delay < 0 || !double.IsFinite(time + wait.Delay) || wait.Delay > 0 && time + wait.Delay <= time)
                        Failure = ActionFailure.InvalidDelay;
                    else (Next, Delay) = (wait.Body, wait.Delay);
                    break;
                case Effect.TemporarySet temporary:
                    if (Resolve(temporary.Target) is not {} target) { Failure = ActionFailure.MissingObject; break; }
                    if (!Reachable(target)) { Failure = ActionFailure.OutOfReach; break; }
                    if (temporary.Tag==Tags.Locked) { Failure = ActionFailure.UnsupportedOperation; break; }
                    if (!double.IsFinite(temporary.Duration)||temporary.Duration<=0||!double.IsFinite(time+temporary.Duration)||time+temporary.Duration<=time)
                    { Failure = ActionFailure.InvalidDelay; break; }
                    _leases.Add((target,new(temporary.Tag,temporary.Value,time+temporary.Duration)));
                    break;
                case Effect.TemporarySetUntilEnd temporary:
                    if (Resolve(temporary.Target) is not { } scopedTarget) { Failure = ActionFailure.MissingObject; break; }
                    if (!Reachable(scopedTarget)) { Failure = ActionFailure.OutOfReach; break; }
                    if (temporary.Tag == Tags.Locked) { Failure = ActionFailure.UnsupportedOperation; break; }
                    _leases.Add((scopedTarget, new(temporary.Tag, temporary.Value, null)));
                    break;
                case Effect.Set set: Change(set.Target, set.Tag, set.Value, false, false); break;
                case Effect.Change change: Change(change.Target, change.Tag, change.Amount, true, false); break;
                case Effect.Spend spend: Change(spend.Target, spend.Tag, spend.Amount, true, true); break;
                case Effect.Relate relation:
                    if (Resolve(relation.Target) is not { } source || Resolve(relation.Other) is not { } other)
                        Failure = ActionFailure.MissingObject;
                    else
                    {
                        var state = Read(source);
                        Write(source, state with { Relations = relation.Remove ? state.Relations.Remove((relation.Relation, other)) : state.Relations.Add((relation.Relation, other)) });
                    }
                    break;
                case Effect.Act action: Act(action); break;
                case Effect.Think think:
                    if (Ast.References(think.Fact).Any(p => !soul.World.Contains(p))) Failure = ActionFailure.MissingObject;
                    else if (Cognition.Input(think.Fact, time) is { } input) _thoughts.Add(input);
                    else Failure = ActionFailure.UnsupportedOperation;
                    break;
                case Effect.Say say:
                    if (Resolve(say.Recipient) is not { } recipient) { Failure = ActionFailure.MissingObject; break; }
                    if (!Reachable(recipient)) { Failure = ActionFailure.OutOfReach; break; }
                    foreach (var receiver in world.Souls.Where(s => s.Relations.Contains((Relations.Controls, recipient))))
                        _messages.Add((receiver, say.Text, say.Request));
                    break;
                default: Failure = ActionFailure.UnsupportedOperation; break;
            }
        }

        private void Change(Noun noun, Tag tag, int amount, bool relative, bool spend)
        {
            if (Resolve(noun) is not { } essence) { Failure = ActionFailure.MissingObject; return; }
            var state = Read(essence);
            var previous = tag==Tags.Locked ? Value(essence,tag)??0 : state.Tags.GetValueOrDefault(tag);
            if (spend && (amount < 0 || previous < amount)) { Failure = ActionFailure.InsufficientResource; return; }
            var value = relative ? (long)previous + (spend ? -(long)amount : amount) : amount;
            if (value is < int.MinValue or > int.MaxValue) { Failure = ActionFailure.InsufficientResource; return; }
            if (tag == Tags.Locked && state.Locked.HasValue) Write(essence, state with { Locked = value > 0 });
            else Write(essence, state with { Tags = state.Tags.SetItem(tag, (int)value) });
        }

        private void Act(Effect.Act action)
        {
            if (Resolve(action.Target) is not { } target) { Failure = ActionFailure.MissingObject; return; }
            if (!Reachable(target)) { Failure = ActionFailure.OutOfReach; return; }
            var state = Read(target);
            switch (action.Operation)
            {
                case ActionOp.Unlock:
                    if (action.Tool is null || Resolve(action.Tool) is not { } key) { Failure = ActionFailure.MissingObject; return; }
                    if (!ReferenceEquals(Read(key).Holder, actor)) { Failure = ActionFailure.OutOfReach; return; }
                    if (state.Locked is null) { Failure = ActionFailure.UnsupportedOperation; return; }
                    if (!state.Relations.Any(r => r.Relation == Relations.AcceptsKey && Read(key).Relations.Contains((Relations.HasIdentity, r.Target))))
                    { Failure = ActionFailure.ConditionFailed; return; }
                    Write(target, state with { Locked = false });
                    break;
                case ActionOp.Travel:
                    if (state.Locked == true || Read(actor).Space is not { } from ||
                        target.Get<PassageProperty>() is not { } passage || !passage.Connections.TryGetValue(from, out var to))
                    { Failure = ActionFailure.ConditionFailed; return; }
                    if (!Read(to).Exists) { Failure = ActionFailure.MissingObject; return; }
                    Write(actor, Read(actor) with { Space = to, Holder = null });
                    break;
                case ActionOp.Take:
                    if (target.Get<SpatialProperty>() is null || target.Kind != ProjectionKind.Object || state.Holder is { Kind: ProjectionKind.Body } holder && !ReferenceEquals(holder, actor))
                    { Failure = ActionFailure.UnsupportedOperation; return; }
                    if (WouldCycle(target, actor)) { Failure = ActionFailure.ConditionFailed; return; }
                    Write(target, state with { Holder = actor, Space = null });
                    break;
                case ActionOp.Place:
                    // Tool names the destination support; placement preserves the existing object identity.
                    if (action.Tool is null || Resolve(action.Tool) is not { } destination)
                    { Failure = ActionFailure.MissingObject; return; }
                    if (!ReferenceEquals(state.Holder, actor) || !Reachable(destination))
                    { Failure = ActionFailure.OutOfReach; return; }
                    if (Read(destination).Locked == true || destination.Kind is not (ProjectionKind.Object or ProjectionKind.Space))
                    { Failure = ActionFailure.ConditionFailed; return; }
                    if (WouldCycle(target, destination)) { Failure = ActionFailure.ConditionFailed; return; }
                    Write(target, state with { Holder = null, Space = destination });
                    break;
                case ActionOp.Inspect:
                    if (world.Representations(target).FirstOrDefault() is { } view) _inspections.Add(view);
                    else Failure = ActionFailure.OutOfReach;
                    break;
                case ActionOp.Destroy: Write(target, state with { Exists = false }); break;
            }
        }

        public void Commit(Func<Action, bool, Action> retainCleanup)
        {
            foreach (var essence in _changed)
            {
                var state = _states[essence];
                if (essence.Get<StateProperty>() is not null || !state.Tags.IsEmpty)
                {
                    var values = essence.Get<StateProperty>() ?? essence.Add(new StateProperty());
                    values.Values.Clear();
                    foreach (var (tag, value) in state.Tags) values.Values.Add(tag, value);
                }
                essence.Relations.Clear();
                essence.Relations.UnionWith(state.Relations);
                if (essence.Get<SpatialProperty>() is { } spatial) (spatial.Space, spatial.Holder) = (state.Space, state.Holder);
                if (essence.Get<LockProperty>() is { } doorLock) doorLock.IsLocked = state.Locked!.Value;
                if (!state.Exists) world.Destroy(essence);
            }
            foreach (var (target, lease) in _leases)
            {
                var property=target.Get<TemporaryStateProperty>()??target.Add(new TemporaryStateProperty());
                property.Layers.Add(lease);
                ScheduledAction? expiry = null;
                Action? release = null;
                void Remove()
                {
                    if (expiry is { Pending: true }) timeline.Cancel(expiry);
                    release?.Invoke();
                    if (property.Layers.RemoveAll(l => ReferenceEquals(l, lease)) > 0) world.Sync();
                }
                if (lease.ExpiresAt is { } expiresAt) expiry = timeline.Schedule(expiresAt-time, Remove);
                release = retainCleanup(Remove, lease.ExpiresAt is null);
            }
            world.Sync();
            foreach (var thought in _thoughts) Observation.Observe(soul, thought);
            if (_inspections.Count > 0) world.Observe(soul, _inspections, time);
            foreach (var (receiver, text, request) in _messages) world.Deliver(receiver, actor, text, request, time);
        }
    }
}
