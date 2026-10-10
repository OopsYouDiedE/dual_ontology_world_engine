using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Story;

/// <summary>Tracks actual, uninterrupted occupancy by execution identity, not daily completion tags.</summary>
public sealed class WorkOccupancy
{
    public sealed record Interval(ActionExecution Execution, Essence Actor, Essence Station, double Start, double End);
    private readonly Dictionary<ActionExecution, (Essence Actor, Essence Station, double Start)> _open = [];
    private readonly List<Interval> _closed = [];
    private readonly HashSet<(ActionExecution, ActionExecution)> _consumed = [];

    public void Update(double time, IEnumerable<(ActionExecution Execution, Essence Actor, Essence Station)> occupied)
    {
        var current = occupied.ToDictionary(v => v.Execution);
        foreach (var (execution, old) in _open.ToArray())
            if (!current.TryGetValue(execution, out var next) || !ReferenceEquals(old.Actor, next.Actor) || !ReferenceEquals(old.Station, next.Station))
            {
                _closed.Add(new(execution, old.Actor, old.Station, old.Start, time)); _open.Remove(execution);
            }
        foreach (var v in current.Values)
            if (!_open.ContainsKey(v.Execution)) _open.Add(v.Execution, (v.Actor, v.Station, time));
    }

    public IEnumerable<(Interval First, Interval Second)> CompletedPairs(Essence first, Essence second, double duration)
    {
        if (!double.IsFinite(duration) || duration <= 0) throw new InvalidDataException("Work overlap duration must be positive.");
        foreach (var a in _closed.Where(v => ReferenceEquals(v.Station, first)))
        foreach (var b in _closed.Where(v => ReferenceEquals(v.Station, second)))
            if (!ReferenceEquals(a.Actor, b.Actor) && a.Execution.Status == ExecutionStatus.Completed && b.Execution.Status == ExecutionStatus.Completed
                && Math.Min(a.End, b.End) - Math.Max(a.Start, b.Start) >= duration
                && !_consumed.Contains((a.Execution, b.Execution))) yield return (a, b);
    }
    public void Consume(Interval first, Interval second) => _consumed.Add((first.Execution, second.Execution));
}
