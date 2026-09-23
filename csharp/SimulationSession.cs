using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Core;

public enum ExecutionStatus { Pending, Completed, Failed, Cancelled }
public sealed class ActionExecution
{
    internal ActionExecution(SoulEssence soul, ActionIntent intent) => (Soul, Intent) = (soul, intent);
    internal SoulEssence Soul { get; }
    public ActionIntent Intent { get; }
    public ExecutionStatus Status { get; internal set; }
    public ActionResolution? Result { get; internal set; }
    public ScheduledAction? Scheduled { get; internal set; }
}

/// <summary>将意图、CTB 与结算组合。子阶段等待期间父动作占用同一身体行动槽。</summary>
public sealed class SimulationSession(WorldRuntime world, CTBSystem? timeline = null) : IDisposable
{
    private readonly ActionRuntime _runtime = new(world);
    private readonly List<ActionExecution> _active = [];
    private readonly Dictionary<Projection, (SoulEssence Soul, double Delay, Action Changed)> _agents = [];
    private readonly Dictionary<Projection, ScheduledAction> _wakeups = [];
    private bool _disposed;
    public CTBSystem CTB { get; } = timeline ?? new();
    public event Action<ActionExecution>? Completed;

    public ActionExecution Submit(SoulEssence soul, ActionIntent intent)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!soul.World.Contains(intent.Actor) || intent.Actor.Kind != ProjectionKind.Body ||
            Ast.References(intent.Changes).Concat(Ast.References(intent.Requirements)).Any(p => !soul.World.Contains(p)) ||
            intent.Basis is not null && !soul.World.Contains(intent.Basis))
            throw new InvalidOperationException("行动名词必须属于主体内部世界，行动者必须是身体投影。");
        var bound = intent with { Changes = Ast.Bind(intent.Changes, new(intent.Actor, Scene: intent.Actor.Parent)) };
        var body = world.Resolve(intent.Actor);
        if (_active.Any(e => ReferenceEquals(e.Intent.Actor, intent.Actor) || body is not null && ReferenceEquals(world.Resolve(e.Intent.Actor), body)))
            throw new InvalidOperationException("该身体已有未完成动作。");
        var execution = new ActionExecution(soul, bound);
        execution.Scheduled = CTB.Schedule(bound.Delay, () => Continue(execution, bound));
        _active.Add(execution);
        return execution;
    }

    public bool Cancel(ActionExecution execution)
    {
        if (!_active.Contains(execution) || execution.Scheduled is null || !CTB.Cancel(execution.Scheduled)) return false;
        execution.Status = ExecutionStatus.Cancelled;
        _active.Remove(execution);
        return true;
    }

    private void Continue(ActionExecution execution, ActionIntent intent)
    {
        var (result, next, delay) = _runtime.Resolve(execution.Soul, intent, CTB.Now, CTB);
        execution.Result = result;
        if (result.Succeeded && next is not null)
        {
            execution.Scheduled = CTB.Schedule(delay, () => Continue(execution, intent with { Changes = next, Requirements = null }));
            return;
        }
        execution.Status = result.Succeeded ? ExecutionStatus.Completed : ExecutionStatus.Failed;
        _active.Remove(execution);
        if (result.Succeeded && intent.Basis is { Kind: ProjectionKind.Event, Procedure: not null } request && execution.Soul.World.Contains(request))
            Observation.Observe(execution.Soul, new(request.Name, request.Kind) { Target = request,
                Source = ObservationSource.Introspection, Time = CTB.Now,
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Responded, 1) });
        Observation.Observe(execution.Soul, new ObservationInput(result.Succeeded ? "行动完成" : "行动未完成", ProjectionKind.Event)
        {
            Source = ObservationSource.Introspection, Time = CTB.Now, Existence = Truth.True,
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Success, result.Succeeded ? 1 : 0),
            AddRelations = execution.Soul.World.Contains(intent.Actor) ? [(Relations.Subject, intent.Actor)] : []
        });
        Completed?.Invoke(execution);
    }

    public ActionExecution? Decide(SoulEssence soul, Projection actor, double delay = 1)
        => DecisionSystem.Choose(soul.World, actor) is { } decision ? Submit(soul, new(actor, decision.Effect, delay, Basis: decision.Basis)) : null;

    public ActionExecution Invoke(SoulEssence soul, Projection actor, Projection procedure, double delay = 1)
    {
        if (!soul.World.Contains(procedure) || procedure.Procedure is null) throw new InvalidOperationException("主体尚未掌握此流程。");
        return Submit(soul, new(actor, procedure.Procedure, delay, Basis: procedure));
    }

    /// <summary>开启自主行动。没有候选时休眠，新的 Observe 输入使其重新决策。</summary>
    public void Activate(SoulEssence soul, Projection actor, double delay = 1)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!soul.World.Contains(actor) || actor.Kind != ProjectionKind.Body) throw new InvalidOperationException("自主主体必须是内部身体投影。");
        if (!double.IsFinite(delay) || delay <= 0) throw new ArgumentOutOfRangeException(nameof(delay));
        Deactivate(actor);
        Action changed = () => Wake(actor);
        _agents.Add(actor, (soul, delay, changed));
        soul.World.Changed += changed;
        Wake(actor);
    }
    private void Wake(Projection actor)
    {
        if (!_agents.ContainsKey(actor) || _wakeups.ContainsKey(actor) || _active.Any(e => ReferenceEquals(e.Intent.Actor, actor))) return;
        _wakeups[actor] = CTB.Schedule(0, () =>
        {
            _wakeups.Remove(actor);
            if (_agents.TryGetValue(actor, out var agent)) Decide(agent.Soul, actor, agent.Delay);
        });
    }
    public void Deactivate(Projection actor)
    {
        if (_agents.Remove(actor, out var agent)) agent.Soul.World.Changed -= agent.Changed;
        if (_wakeups.Remove(actor, out var wakeup)) CTB.Cancel(wakeup);
        foreach (var execution in _active.Where(e => ReferenceEquals(e.Intent.Actor, actor)).ToArray()) Cancel(execution);
    }
    public void Dispose()
    {
        if (_disposed) return;
        foreach (var actor in _agents.Keys.ToArray()) Deactivate(actor);
        foreach (var execution in _active.ToArray()) Cancel(execution);
        _disposed = true;
    }
}
