namespace DualOntologyWorldEngine.Core.Timeline;

/// <summary>计划在未来行动值时刻执行的离散任务。登记序号是队列私有排序键，不是世界对象标识。</summary>
public record ScheduledTask(double Time, long Sequence, string Label, Func<double?> Action);

/// <summary>离散行动值队列。唯一排序键为绝对行动值与登记序号。</summary>
public class Timeline
{
    private readonly PriorityQueue<ScheduledTask, (double Time, long Sequence)> _queue = new();
    private long _nextSequence;

    /// <summary>当前队列的稳定只读快照。</summary>
    public IReadOnlyList<ScheduledTask> Queue => _queue.UnorderedItems
        .OrderBy(item => item.Priority.Time)
        .ThenBy(item => item.Priority.Sequence)
        .Select(item => item.Element)
        .ToArray();
    /// <summary>当前全局行动值时刻。</summary>
    public double Now { get; private set; }

    /// <summary>仅在 AV 实际前进时通知连续过程；同刻的多个行动不会重复消耗需求。</summary>
    public event Action<double>? Advanced;

    private void AdvanceTo(double time)
    {
        var elapsed = time - Now;
        if (elapsed <= 0) return;
        Now = time;
        Advanced?.Invoke(elapsed);
    }

    public int Cancel(Func<ScheduledTask, bool> predicate)
    {
        var remaining = _queue.UnorderedItems.Where(item => !predicate(item.Element)).ToArray();
        var removed = _queue.Count - remaining.Length;
        _queue.Clear();
        _queue.EnqueueRange(remaining);
        return removed;
    }

    /// <summary>从当前时刻登记任务，同刻按登记顺序执行。</summary>
    public void Schedule(double delay, string label, Func<double?> action)
    {
        if (!double.IsFinite(delay) || delay < 0)
            throw new ArgumentOutOfRangeException(nameof(delay), "行动值延迟必须是有限非负数。");

        var task = new ScheduledTask(Now + delay, _nextSequence++, label, action);
        _queue.Enqueue(task, (task.Time, task.Sequence));
    }

    /// <summary>执行队首任务，并在回调给出延迟时登记下一次行动槽。</summary>
    public bool Step()
    {
        if (!_queue.TryDequeue(out var task, out _)) return false;
        AdvanceTo(task.Time);
        var nextDelay = task.Action();
        if (nextDelay.HasValue)
            Schedule(nextDelay.Value, task.Label, task.Action);
        return true;
    }

    /// <summary>推进到目标时刻，最大步数由调用方控制以防零成本循环。</summary>
    public int RunUntil(double targetTime, int maxSteps = 1000)
    {
        if (!double.IsFinite(targetTime))
            throw new ArgumentOutOfRangeException(nameof(targetTime), "目标行动值必须是有限数。");

        var steps = 0;
        while (_queue.TryPeek(out _, out var priority) && priority.Time <= targetTime && steps < maxSteps)
        {
            Step();
            steps++;
        }
        // 步数预算耗尽时保留真实进度，不能跳过尚待执行的历史任务。
        if (!_queue.TryPeek(out _, out var nextPriority) || nextPriority.Time > targetTime)
            AdvanceTo(targetTime);
        return steps;
    }
}
