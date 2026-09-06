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
        if (!double.IsFinite(time))
            throw new ArgumentOutOfRangeException(nameof(time), "行动值时刻必须是有限数。");
        Now = time;
        Advanced?.Invoke(elapsed);
    }

    private double TimeAfterDelay(double delay)
    {
        var time = Now + delay;
        if (!double.IsFinite(time))
            throw new ArgumentOutOfRangeException(nameof(delay), "行动值延迟超出可表示的时间范围。");
        if (delay == 0) return Now;
        // 大时刻下过小的正延迟会舍入回当前时刻；拒绝它，避免静默改变延迟语义。
        if (time <= Now)
            throw new ArgumentOutOfRangeException(nameof(delay), "行动值延迟无法使时间严格前进。");
        return time;
    }

    public int Cancel(Func<ScheduledTask, bool> predicate)
    {
        var remaining = _queue.UnorderedItems.Where(item => !predicate(item.Element)).ToArray();
        var removed = _queue.Count - remaining.Length;
        _queue.Clear();
        _queue.EnqueueRange(remaining);
        return removed;
    }

    /// <summary>标准 CTB 跑道距离基准常数（Gauge Threshold）。</summary>
    public const double GaugeThreshold = 10_000.0;

    /// <summary>计算基础行动值：BaseAV = D / Speed。</summary>
    public static double CalculateBaseAv(double speed)
    {
        if (speed <= 0 || !double.IsFinite(speed))
            throw new ArgumentOutOfRangeException(nameof(speed), "速度必须是大于 0 的有限数值。");
        var result = GaugeThreshold / speed;
        if (!double.IsFinite(result))
            throw new ArgumentOutOfRangeException(nameof(speed), "速度导致行动值超出可表示范围。");
        return result;
    }

    /// <summary>计算动作后摇行动值增量：ΔAV = (D * R) / Speed = BaseAV * R。</summary>
    public static double CalculateActionAv(double speed, double rank = 1.0)
    {
        if (rank < 0 || !double.IsFinite(rank))
            throw new ArgumentOutOfRangeException(nameof(rank), "动作代价等级必须为非负有限数。");
        var result = CalculateBaseAv(speed) * rank;
        if (!double.IsFinite(result))
            throw new ArgumentOutOfRangeException(nameof(rank), "动作代价超出可表示范围。");
        return result;
    }

    /// <summary>调整指定任务的剩余行动值（拉条 / 推条）。</summary>
    /// <param name="predicate">匹配目标任务的谓词。</param>
    /// <param name="deltaAv">行动值变动量（负数为拉条提前，正数为推条延后）。</param>
    /// <returns>成功调整的任务数量。</returns>
    public int AdjustRemainingAv(Func<ScheduledTask, bool> predicate, double deltaAv)
    {
        var items = _queue.UnorderedItems.ToArray();
        var adjustedCount = 0;
        var rebuilt = new List<(ScheduledTask Task, (double Time, long Sequence) Priority)>(items.Length);
        foreach (var item in items)
        {
            var task = item.Element;
            if (predicate(task))
            {
                var remaining = Math.Max(0.0, task.Time - Now);
                var shifted = remaining + deltaAv;
                if (!double.IsFinite(shifted))
                    throw new ArgumentOutOfRangeException(nameof(deltaAv), "行动值调整超出可表示范围。");
                var newRemaining = Math.Max(0.0, shifted);
                var newTime = newRemaining > 0 ? TimeAfterDelay(newRemaining) : Now;
                var newTask = task with { Time = newTime };
                rebuilt.Add((newTask, (newTask.Time, newTask.Sequence)));
                adjustedCount++;
            }
            else
            {
                rebuilt.Add((task, item.Priority));
            }
        }
        _queue.Clear();
        foreach (var item in rebuilt) _queue.Enqueue(item.Task, item.Priority);
        return adjustedCount;
    }

    /// <summary>行动提前（Action Advance / 拉条）：AV_new = max(0, AV_current - BaseAV * ratio)。</summary>
    public int AdvanceGauge(Func<ScheduledTask, bool> predicate, double speed, double ratio)
    {
        if (ratio < 0 || !double.IsFinite(ratio))
            throw new ArgumentOutOfRangeException(nameof(ratio), "拉条比例必须为非负有限数。");
        var baseAv = CalculateBaseAv(speed);
        return AdjustRemainingAv(predicate, -baseAv * ratio);
    }

    /// <summary>行动延后（Action Delay / 推条）：AV_new = AV_current + BaseAV * ratio。</summary>
    public int DelayGauge(Func<ScheduledTask, bool> predicate, double speed, double ratio)
    {
        if (ratio < 0 || !double.IsFinite(ratio))
            throw new ArgumentOutOfRangeException(nameof(ratio), "推条比例必须为非负有限数。");
        var baseAv = CalculateBaseAv(speed);
        return AdjustRemainingAv(predicate, baseAv * ratio);
    }

    /// <summary>进程中变速动态缩放：AV_new = AV_current * (oldSpeed / newSpeed)。</summary>
    public int ScaleGaugeOnSpeedChange(Func<ScheduledTask, bool> predicate, double oldSpeed, double newSpeed)
    {
        if (oldSpeed <= 0 || !double.IsFinite(oldSpeed) || newSpeed <= 0 || !double.IsFinite(newSpeed))
            throw new ArgumentOutOfRangeException(nameof(newSpeed), "速度值必须为大于 0 的有限数。");
        var ratio = oldSpeed / newSpeed;
        var items = _queue.UnorderedItems.ToArray();
        var scaledCount = 0;
        var rebuilt = new List<(ScheduledTask Task, (double Time, long Sequence) Priority)>(items.Length);
        foreach (var item in items)
        {
            var task = item.Element;
            if (predicate(task))
            {
                var remaining = Math.Max(0.0, task.Time - Now);
                var newRemaining = remaining * ratio;
                if (!double.IsFinite(newRemaining))
                    throw new ArgumentOutOfRangeException(nameof(newSpeed), "速度变化导致行动值超出可表示范围。");
                var newTime = newRemaining > 0 ? TimeAfterDelay(newRemaining) : Now;
                var newTask = task with { Time = newTime };
                rebuilt.Add((newTask, (newTask.Time, newTask.Sequence)));
                scaledCount++;
            }
            else
            {
                rebuilt.Add((task, item.Priority));
            }
        }
        _queue.Clear();
        foreach (var item in rebuilt) _queue.Enqueue(item.Task, item.Priority);
        return scaledCount;
    }

    /// <summary>瞬发中断执行（Immediate Turn / Burst）：暂停主推进流程，分配优先执行权，不消耗原本排队 AV。</summary>
    public void Interrupt(string label, Action action)
    {
        action();
    }

    /// <summary>预测行动序列（Turn Order Preview）：轻量模拟未来 depth 步的行动次序。</summary>
    public IReadOnlyList<string> PreviewTurnOrder(int depth = 10, double defaultSpeed = 100.0)
    {
        if (depth <= 0) return [];
        var simQueue = new PriorityQueue<(string Label, double BaseAv), (double Time, long Sequence)>();
        foreach (var item in _queue.UnorderedItems)
        {
            var remaining = Math.Max(0.0, item.Element.Time - Now);
            simQueue.Enqueue((item.Element.Label, CalculateBaseAv(defaultSpeed)), (remaining, item.Element.Sequence));
        }

        var results = new List<string>(depth);
        var simSeq = _nextSequence;
        var simNow = 0.0;

        while (results.Count < depth && simQueue.TryDequeue(out var candidate, out var priority))
        {
            results.Add(candidate.Label);
            simNow = priority.Time;
            var nextTime = simNow + candidate.BaseAv;
            if (!double.IsFinite(nextTime)) break;
            simQueue.Enqueue(candidate, (nextTime, simSeq++));
        }

        return results;
    }

    /// <summary>从当前时刻登记任务，同刻按登记顺序执行。</summary>
    public void Schedule(double delay, string label, Func<double?> action)
    {
        if (!double.IsFinite(delay) || delay < 0)
            throw new ArgumentOutOfRangeException(nameof(delay), "行动值延迟必须是有限非负数。");

        var task = new ScheduledTask(TimeAfterDelay(delay), _nextSequence++, label, action);
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
