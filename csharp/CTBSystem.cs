namespace DualOntologyWorldEngine.Core;

/// <summary>调度项是可取消的对象句柄；序号仅用于同刻排序。</summary>
public sealed class ScheduledAction
{
    internal ScheduledAction(double due, long sequence, Action activate) => (Due, Sequence, Activate) = (due, sequence, activate);
    public double Due { get; internal set; }
    public bool Pending { get; internal set; } = true;
    internal long Sequence { get; }
    internal Action Activate { get; }
}

/// <summary>离散行动值调度。每项仅激活一次，不选择行动、不解释效果。</summary>
public sealed class CTBSystem
{
    private readonly PriorityQueue<ScheduledAction, (double Due, long Sequence)> _queue = new();
    private long _sequence;
    public double Now { get; private set; }
    public IEnumerable<ScheduledAction> Pending => _queue.UnorderedItems.Select(p => p.Element).Where(p => p.Pending).OrderBy(p => p.Due).ThenBy(p => p.Sequence);

    private double DueAfter(double delay)
    {
        var due = Now + delay;
        if (!double.IsFinite(delay) || delay < 0 || !double.IsFinite(due) || delay > 0 && due <= Now)
            throw new ArgumentOutOfRangeException(nameof(delay), "行动值必须有限、非负，正延迟必须推进时间。");
        return due;
    }
    public ScheduledAction Schedule(double delay, Action activate)
    {
        var action = new ScheduledAction(DueAfter(delay), _sequence++, activate);
        _queue.Enqueue(action, (action.Due, action.Sequence));
        return action;
    }
    public bool Cancel(ScheduledAction action)
    {
        if (!Pending.Contains(action)) return false;
        action.Pending = false;
        Rebuild();
        return true;
    }
    public bool Reschedule(ScheduledAction action, double remaining)
    {
        if (!Pending.Contains(action)) return false;
        action.Due = DueAfter(remaining);
        Rebuild();
        return true;
    }
    private void Rebuild()
    {
        var pending = Pending.ToArray();
        _queue.Clear();
        foreach (var action in pending) _queue.Enqueue(action, (action.Due, action.Sequence));
    }
    public bool Step()
    {
        if (!_queue.TryDequeue(out var action, out _)) return false;
        Now = action.Due;
        action.Pending = false;
        action.Activate();
        return true;
    }
    public int RunUntil(double time, int maxSteps = 1000)
    {
        if (!double.IsFinite(time) || time < Now) throw new ArgumentOutOfRangeException(nameof(time));
        var steps = 0;
        while (_queue.TryPeek(out var action, out _) && action.Due <= time && steps < maxSteps) { Step(); steps++; }
        if (!_queue.TryPeek(out var next, out _) || next.Due > time) Now = Math.Max(Now, time);
        return steps;
    }
    public static double ActionValue(double speed, double cost = 1, double distance = 10_000)
    {
        var value = distance * cost / speed;
        if (!double.IsFinite(speed) || speed <= 0 || !double.IsFinite(cost) || cost <= 0 || !double.IsFinite(distance) || distance <= 0 || !double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(nameof(speed));
        return value;
    }
}
