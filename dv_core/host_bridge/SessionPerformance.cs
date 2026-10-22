using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using DuelView.Compiled;

public sealed partial class DvSession
{
    private readonly SessionPerformance performance = new();

    private double PerformanceGameMinute() =>
        R.Num(Field(Field(state, "physical", new List<object>()), "time", 0d));

    private IDisposable Measure(string operation, string category = "engine")
    {
        var pending = Field(state, "pending", false);
        var actor = pending is false ? "" : R.Text(Field(pending, "id", ""));
        return performance.Begin(operation, category, actor, PerformanceGameMinute);
    }

    private void ObservePerformancePhase(string phase, string actor)
    {
        performance.Phase(phase, actor);
    }

    public IReadOnlyDictionary<string, object?> PerformanceSnapshot() => performance.Snapshot(IsClosed);
    public IReadOnlyDictionary<string, object?> PerformanceSummary() => performance.Summary(IsClosed);

    public void WritePerformanceReport(string prefix) => performance.WriteReport(prefix, IsClosed);

    private static DvSession OpenMeasured(string operation, Func<DvSession> create)
    {
        var started = Stopwatch.GetTimestamp();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var current = create();
        current.performance.RecordStartup(operation, started, allocated, current.PerformanceGameMinute());
        return current;
    }
}

/// <summary>Per-session accounting. Timing never participates in world state or action selection.</summary>
internal sealed class SessionPerformance
{
    private const int SampleCapacity = 8192;
    private static readonly double[] HistogramBounds =
        [0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 20, 50, 100, 200, 500,
         1000, 2000, 5000, 10000, 30000, 60000, 120000, 300000, 600000];
    private readonly object gate = new();
    private readonly long origin = Stopwatch.GetTimestamp();
    private readonly string sessionId = Guid.NewGuid().ToString("N");
    private readonly string startedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    private readonly List<Frame> stack = [];
    private readonly Dictionary<(string Category, string Operation, string Actor), Aggregate> totals = [];
    private readonly SortedDictionary<(int Epoch, long Hour), HourBucket> hours = [];
    private readonly Dictionary<string, long> outcomes = new(StringComparer.Ordinal);
    private readonly Queue<Sample> samples = new(SampleCapacity);
    private long sequence, dropped, exceptions, unmatchedPhases, interruptedPhases;
    private double? firstGameMinute;
    private double lastGameMinute, advancedMinutes;
    private int epoch;
    private bool auditMode, developerMode;
    private string playerActor = "";
    private int actorCount;

    internal void Configure(bool audit, bool developer, string player, int actors, double minute)
    {
        lock (gate)
        {
            auditMode = audit; developerMode = developer; playerActor = player; actorCount = actors;
            firstGameMinute = lastGameMinute = minute;
        }
    }

    private sealed class Frame
    {
        internal required string Operation, Category, Actor;
        internal required long Started, Allocated;
        internal required double GameStart;
        internal required int Epoch;
        internal required bool IsPhase;
        internal long ChildTicks, ChildAllocated;
        internal bool Failed;
    }

    private sealed class Aggregate
    {
        internal long Count, Failed, Incomplete, Ticks, SelfTicks, Allocated, SelfAllocated, Maximum;
        internal readonly long[] Histogram = new long[HistogramBounds.Length + 1];

        internal void Add(long ticks, long self, long allocated, long selfAllocated, bool failed, bool incomplete)
        {
            Count++;
            if (failed) Failed++;
            if (incomplete) Incomplete++;
            Ticks += ticks; SelfTicks += self; Allocated += allocated; SelfAllocated += selfAllocated;
            Maximum = Math.Max(Maximum, ticks);
            var milliseconds = Milliseconds(ticks);
            var index = Array.BinarySearch(HistogramBounds, milliseconds);
            if (index < 0) index = ~index;
            Histogram[index]++;
        }

        internal double PercentileUpper(double quantile)
        {
            var rank = Math.Max(1L, (long)Math.Ceiling(Count * quantile));
            var seen = 0L;
            for (var index = 0; index < Histogram.Length; index++)
            {
                seen += Histogram[index];
                if (seen >= rank)
                    return index < HistogramBounds.Length ? Math.Min(HistogramBounds[index], Milliseconds(Maximum)) : Milliseconds(Maximum);
            }
            return Milliseconds(Maximum);
        }
    }

    private sealed class HourBucket
    {
        internal double CoveredMinutes;
        internal long Operations;
        internal readonly Dictionary<string, long> SelfTicks = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, long> OperationTicks = new(StringComparer.Ordinal);
    }

    private sealed record Sample(long Sequence, string Operation, string Category, string Actor,
        int Epoch, double GameStart, double GameEnd, long StartTicks, long DurationTicks, long SelfTicks,
        long AllocatedBytes, long SelfAllocatedBytes, bool Failed, bool PhaseComplete);

    private sealed class Scope(SessionPerformance owner, Frame frame, Func<double> clock) : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            owner.End(frame, clock());
        }
    }

    internal IDisposable Begin(string operation, string category, string actor, Func<double> clock)
    {
        var minute = clock();
        lock (gate)
        {
            firstGameMinute ??= minute;
            var frame = new Frame
            {
                Operation = operation, Category = category, Actor = actor,
                Started = Stopwatch.GetTimestamp(), Allocated = GC.GetAllocatedBytesForCurrentThread(),
                GameStart = minute, Epoch = epoch, IsPhase = false
            };
            stack.Add(frame);
            return new Scope(this, frame, clock);
        }
    }

    internal void Phase(string phase, string actor)
    {
        var begin = phase.EndsWith("-start", StringComparison.Ordinal) || phase.EndsWith("_begin", StringComparison.Ordinal);
        var end = phase.EndsWith("-end", StringComparison.Ordinal) || phase.EndsWith("_end", StringComparison.Ordinal);
        if (!begin && !end) return;
        var suffix = begin ? 6 : 4;
        var name = phase[..^suffix];
        lock (gate)
        {
            if (stack.Count == 0) return;
            if (begin)
            {
                var parent = stack[^1];
                stack.Add(new Frame
                {
                    Operation = "phase." + name, Category = parent.Category, Actor = actor,
                    Started = Stopwatch.GetTimestamp(), Allocated = GC.GetAllocatedBytesForCurrentThread(),
                    GameStart = parent.GameStart, Epoch = parent.Epoch, IsPhase = true
                });
                return;
            }
            var index = stack.FindLastIndex(frame => frame.IsPhase && frame.Operation == "phase." + name && frame.Actor == actor);
            if (index < 0) { unmatchedPhases++; return; }
            var stopped = Stopwatch.GetTimestamp();
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            while (stack.Count - 1 > index)
            {
                interruptedPhases++;
                FinishTop(stopped, allocated, stack[^1].GameStart, true, false);
            }
            FinishTop(stopped, allocated, stack[^1].GameStart, false, false);
        }
    }

    internal void RecordFailure()
    {
        lock (gate)
        {
            if (stack.Count == 0 || !stack[^1].Failed) exceptions++;
            foreach (var frame in stack) frame.Failed = true;
        }
    }

    internal void RecordOutcome(string outcome)
    {
        lock (gate) outcomes[outcome.Length == 0 ? "unrecorded" : outcome] = outcomes.GetValueOrDefault(outcome.Length == 0 ? "unrecorded" : outcome) + 1;
    }

    private void End(Frame frame, double minute)
    {
        var stopped = Stopwatch.GetTimestamp();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        lock (gate)
        {
            var index = stack.LastIndexOf(frame);
            if (index < 0) return;
            while (stack.Count - 1 > index)
            {
                interruptedPhases++;
                FinishTop(stopped, allocated, minute, true, false);
            }
            var root = index == 0;
            FinishTop(stopped, allocated, minute, false, root);
            if (root)
            {
                if (minute < frame.GameStart) epoch++;
                lastGameMinute = minute;
            }
        }
    }

    private void FinishTop(long stopped, long allocated, double minute, bool incomplete, bool root)
    {
        var frame = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        var ticks = Math.Max(0, stopped - frame.Started);
        var bytes = Math.Max(0, allocated - frame.Allocated);
        var self = Math.Max(0, ticks - frame.ChildTicks);
        var selfBytes = Math.Max(0, bytes - frame.ChildAllocated);
        if (stack.Count > 0)
        {
            stack[^1].ChildTicks += ticks;
            stack[^1].ChildAllocated += bytes;
        }
        Add(frame.Category, frame.Operation, frame.Actor, frame.Epoch, frame.GameStart, minute,
            frame.Started, ticks, self, bytes, selfBytes, frame.Failed, incomplete);
        if (root && frame.Category == "engine" && minute > frame.GameStart)
        {
            advancedMinutes += minute - frame.GameStart;
            var cursor = frame.GameStart;
            while (cursor < minute)
            {
                var hour = (long)Math.Floor(cursor / 60d);
                var until = Math.Min(minute, (hour + 1) * 60d);
                Bucket(frame.Epoch, hour).CoveredMinutes += until - cursor;
                cursor = until;
            }
        }
    }

    private HourBucket Bucket(int timeline, long hour)
    {
        var key = (timeline, hour);
        if (!hours.TryGetValue(key, out var value)) hours[key] = value = new();
        return value;
    }

    private void Add(string category, string operation, string actor, int timeline, double gameStart,
        double gameEnd, long started, long ticks, long self, long allocated, long selfAllocated, bool failed, bool incomplete)
    {
        var key = (category, operation, actor);
        if (!totals.TryGetValue(key, out var aggregate)) totals[key] = aggregate = new();
        aggregate.Add(ticks, self, allocated, selfAllocated, failed, incomplete);
        var bucket = Bucket(timeline, (long)Math.Floor(gameStart / 60d));
        bucket.Operations++;
        bucket.SelfTicks[category] = bucket.SelfTicks.GetValueOrDefault(category) + self;
        bucket.OperationTicks[operation] = bucket.OperationTicks.GetValueOrDefault(operation) + self;
        var sample = new Sample(++sequence, operation, category, actor, timeline, gameStart, gameEnd,
            started - origin, ticks, self, allocated, selfAllocated, failed, !incomplete);
        if (samples.Count == SampleCapacity) { samples.Dequeue(); dropped++; }
        samples.Enqueue(sample);
    }

    internal void RecordStartup(string operation, long started, long allocated, double minute)
    {
        var stopped = Stopwatch.GetTimestamp();
        var bytes = Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - allocated);
        lock (gate)
        {
            firstGameMinute ??= minute;
            lastGameMinute = minute;
            Add("startup", operation, "", epoch, minute, minute, started, stopped - started,
                stopped - started, bytes, bytes, false, false);
        }
    }

    private static double Milliseconds(long ticks) => ticks * (1000d / Stopwatch.Frequency);
    private static bool EngineCategory(string category) => category is "engine" or "speculation";
    private static string CategoryLabel(string category) => category switch
    {
        "engine" => "正式计算", "speculation" => "提前准备", "projection" => "投影",
        "startup" => "初始化与恢复", "persistence" => "保存与归档", "development" => "开发操作", _ => category
    };
    private static string OperationLabel(string operation) => operation switch
    {
        "phase.cognition" => "人物认知", "phase.choice" => "行动选策",
        "prepare" => "准备行动", "background-prepare" => "后台准备",
        "prepare-ahead" => "提前计算", "adopt-prepared" => "采用提前结果",
        "complete" => "正式结算", "intent" => "行动意图", "presentation" => "正文投影",
        "host-snapshot" => "宿主快照", "host-delta" => "宿主增量", "executor-snapshot" => "执行者投影",
        "text-status" => "状态投影", "inspect" => "开发查看", "observer-preview" => "观察者地图",
        "observer-options" => "观察者选项", "development" => "开发命令",
        "open-campaign" or "open-configured" or "open-scenario" => "初始化",
        "restore-value" or "restore-file" => "恢复存档", "save" or "save-json" => "保存",
        "trace" or "trace-json" => "轨迹封存", "close" => "关闭会话", _ => operation
    };
    private static object? Rate(double milliseconds, double minutes) => minutes > 0 ? milliseconds * 0.06d / minutes : null;

    internal IReadOnlyDictionary<string, object?> Summary(bool closed)
    {
        lock (gate)
        {
            double Total(string category) => totals.Where(item => item.Key.Category == category).Sum(item => Milliseconds(item.Value.SelfTicks));
            var engine = Total("engine") + Total("speculation");
            return HostValues.Freeze(new Dictionary<string, object?>
            {
                ["closed"] = closed, ["advanced_game_minutes"] = advancedMinutes,
                ["engine_ms"] = engine, ["seconds_per_game_hour"] = Rate(engine, advancedMinutes),
                ["speculation_ms"] = Total("speculation"), ["projection_ms"] = Total("projection"),
                ["startup_ms"] = Total("startup"), ["persistence_ms"] = Total("persistence"),
                ["active_scopes"] = stack.Count, ["sample_count"] = sequence, ["dropped_samples"] = dropped
            });
        }
    }

    internal IReadOnlyDictionary<string, object?> Snapshot(bool closed)
    {
        lock (gate) return SnapshotLocked(closed);
    }

    private IReadOnlyDictionary<string, object?> SnapshotLocked(bool closed)
    {
        var engineMs = totals.Where(item => EngineCategory(item.Key.Category)).Sum(item => Milliseconds(item.Value.SelfTicks));
        var categoryRows = totals.GroupBy(item => item.Key.Category, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var milliseconds = group.Sum(item => Milliseconds(item.Value.SelfTicks));
                return (object)HostValues.Freeze(new Dictionary<string, object?>
                {
                    ["category"] = group.Key, ["label"] = CategoryLabel(group.Key), ["self_ms"] = milliseconds,
                    ["seconds_per_game_hour"] = Rate(milliseconds, advancedMinutes),
                    ["allocated_bytes"] = group.Sum(item => item.Value.SelfAllocated)
                });
            }).ToArray();
        var operationRows = totals.OrderByDescending(item => item.Value.SelfTicks)
            .ThenBy(item => item.Key.Operation, StringComparer.Ordinal).ThenBy(item => item.Key.Actor, StringComparer.Ordinal)
            .Select(item => (object)HostValues.Freeze(new Dictionary<string, object?>
            {
                ["category"] = item.Key.Category, ["operation"] = item.Key.Operation, ["actor"] = item.Key.Actor,
                ["category_label"] = CategoryLabel(item.Key.Category), ["label"] = OperationLabel(item.Key.Operation),
                ["calls"] = item.Value.Count, ["failed_calls"] = item.Value.Failed, ["incomplete_phases"] = item.Value.Incomplete,
                ["total_ms"] = Milliseconds(item.Value.Ticks), ["self_ms"] = Milliseconds(item.Value.SelfTicks),
                ["mean_ms"] = Milliseconds(item.Value.Ticks) / item.Value.Count,
                ["p95_upper_ms"] = item.Value.PercentileUpper(0.95), ["max_ms"] = Milliseconds(item.Value.Maximum),
                ["allocated_bytes"] = item.Value.Allocated, ["self_allocated_bytes"] = item.Value.SelfAllocated,
                ["seconds_per_game_hour"] = Rate(Milliseconds(item.Value.SelfTicks), advancedMinutes)
            })).ToArray();
        var hourRows = hours.Select(item =>
        {
            double Category(string category) => Milliseconds(item.Value.SelfTicks.GetValueOrDefault(category));
            double Operation(string operation) => Milliseconds(item.Value.OperationTicks.GetValueOrDefault(operation));
            var engine = Category("engine") + Category("speculation");
            return (object)HostValues.Freeze(new Dictionary<string, object?>
            {
                ["epoch"] = item.Key.Epoch, ["game_hour"] = item.Key.Hour,
                ["game_start_minute"] = item.Key.Hour * 60d, ["game_end_minute"] = (item.Key.Hour + 1) * 60d,
                ["covered_game_minutes"] = item.Value.CoveredMinutes,
                ["full_game_hour"] = item.Value.CoveredMinutes >= 60d,
                ["engine_ms"] = engine, ["foreground_engine_ms"] = Category("engine"), ["speculation_ms"] = Category("speculation"),
                ["cognition_self_ms"] = Operation("phase.cognition"), ["choice_self_ms"] = Operation("phase.choice"),
                ["prepare_self_ms"] = Operation("prepare"), ["background_prepare_self_ms"] = Operation("background-prepare"),
                ["complete_self_ms"] = Operation("complete"), ["adoption_self_ms"] = Operation("adopt-prepared"),
                ["projection_ms"] = Category("projection"), ["persistence_ms"] = Category("persistence"),
                ["startup_ms"] = Category("startup"), ["development_ms"] = Category("development"),
                ["seconds_per_game_hour"] = Rate(engine, item.Value.CoveredMinutes), ["scopes"] = item.Value.Operations
            });
        }).ToArray();
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["contract"] = "dv-performance-v1", ["session_id"] = sessionId, ["started_utc"] = startedUtc,
            ["runtime"] = Environment.Version.ToString(), ["stopwatch_frequency"] = Stopwatch.Frequency,
            ["assembly_id"] = typeof(DvSession).Assembly.ManifestModule.ModuleVersionId.ToString(),
            ["audit_mode"] = auditMode, ["development_mode"] = developerMode,
            ["controller"] = playerActor.Length == 0 ? "ai" : "player", ["player_actor"] = playerActor, ["actor_count"] = actorCount,
            ["closed"] = closed, ["active_scopes"] = stack.Count, ["game_start_minute"] = firstGameMinute,
            ["game_end_minute"] = lastGameMinute, ["advanced_game_minutes"] = advancedMinutes,
            ["covered_game_hours"] = advancedMinutes / 60d, ["engine_ms"] = engineMs,
            ["seconds_per_game_hour"] = Rate(engineMs, advancedMinutes),
            ["rate_basis"] = "sum-exclusive-engine-and-speculation-wall-time / actual-forward-engine-game-hours",
            ["hour_attribution"] = "operation-start-game-hour; clock coverage split at hour boundaries",
            ["clock_discontinuities"] = epoch, ["sample_policy"] = "bounded-tail; lifetime aggregates retained",
            ["sample_capacity"] = SampleCapacity, ["sample_count"] = sequence, ["retained_samples"] = samples.Count,
            ["dropped_samples"] = dropped, ["exceptions"] = exceptions,
            ["unmatched_phase_ends"] = unmatchedPhases, ["interrupted_phases"] = interruptedPhases,
            ["percentile_method"] = "fixed-histogram upper bound; capped by observed maximum",
            ["allocation_basis"] = "current managed thread allocated bytes; not retained heap",
            ["failure_basis"] = "reported DV invocation and persistence exceptions; input rejection is separate",
            ["categories"] = Array.AsReadOnly(categoryRows), ["operations"] = Array.AsReadOnly(operationRows),
            ["hourly"] = Array.AsReadOnly(hourRows),
            ["outcomes"] = HostValues.Freeze(outcomes.ToDictionary(item => item.Key, item => (object?)item.Value, StringComparer.Ordinal))
        });
    }

    internal void WriteReport(string prefix, bool closed)
    {
        IReadOnlyDictionary<string, object?> snapshot;
        Sample[] tail;
        lock (gate) { snapshot = SnapshotLocked(closed); tail = samples.ToArray(); }
        var full = Path.GetFullPath(prefix);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var errors = new List<Exception>();
        void Write(string suffix, Action<Stream> action)
        {
            try
            {
                var path = full + suffix;
                using (var stream = new FileStream(path + ".partial", FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    action(stream);
                    stream.Flush(true);
                }
                File.Move(path + ".partial", path, overwrite: false);
            }
            catch (Exception error) { errors.Add(new IOException("Performance report " + suffix + ": " + error.Message, error)); }
        }
        Write(".dvlog", stream =>
        {
            stream.Write(BinaryEvidence.Magic);
            BinaryEvidence.WriteFrame(stream, new Dictionary<string, object?>
            {
                ["type"] = "performance-header", ["contract"] = "dv-performance-v1",
                ["session_id"] = sessionId, ["sample_policy"] = "bounded-tail", ["stopwatch_frequency"] = Stopwatch.Frequency
            });
            foreach (var sample in tail)
                BinaryEvidence.WriteFrame(stream, new Dictionary<string, object?>
                {
                    ["type"] = "performance-sample", ["sequence"] = sample.Sequence, ["operation"] = sample.Operation,
                    ["category"] = sample.Category, ["actor"] = sample.Actor, ["epoch"] = sample.Epoch,
                    ["game_start_minute"] = sample.GameStart, ["game_end_minute"] = sample.GameEnd,
                    ["start_ticks"] = sample.StartTicks, ["duration_ticks"] = sample.DurationTicks, ["self_ticks"] = sample.SelfTicks,
                    ["allocated_bytes"] = sample.AllocatedBytes, ["self_allocated_bytes"] = sample.SelfAllocatedBytes,
                    ["failed"] = sample.Failed, ["phase_complete"] = sample.PhaseComplete
                });
            BinaryEvidence.WriteFrame(stream, new Dictionary<string, object?>
            {
                ["type"] = "performance-summary", ["data"] = JsonSerializer.SerializeToElement(snapshot)
            });
            BinaryEvidence.WriteFrame(stream, new Dictionary<string, object?>
            {
                ["type"] = "seal", ["retained_samples"] = tail.Length,
                ["total_samples"] = snapshot["sample_count"], ["dropped_samples"] = snapshot["dropped_samples"]
            });
        });
        Write(".hourly.csv", stream => WriteCsv(stream, Rows(snapshot, "hourly"),
            ["epoch", "game_hour", "game_start_minute", "game_end_minute", "covered_game_minutes", "full_game_hour",
             "engine_ms", "foreground_engine_ms", "speculation_ms", "cognition_self_ms", "choice_self_ms",
             "prepare_self_ms", "background_prepare_self_ms", "complete_self_ms", "adoption_self_ms",
             "projection_ms", "persistence_ms", "startup_ms",
             "development_ms", "seconds_per_game_hour", "scopes"]));
        Write(".operations.csv", stream => WriteCsv(stream, Rows(snapshot, "operations"),
            ["category", "operation", "actor", "calls", "failed_calls", "incomplete_phases", "total_ms", "self_ms",
             "mean_ms", "p95_upper_ms", "max_ms", "allocated_bytes", "self_allocated_bytes", "seconds_per_game_hour"]));
        Write(".md", stream =>
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true);
            writer.Write(Markdown(snapshot));
        });
        if (errors.Count > 0) throw new AggregateException(errors);
    }

    private static IEnumerable<IReadOnlyDictionary<string, object?>> Rows(IReadOnlyDictionary<string, object?> value, string field) =>
        HostValues.List(value, field).OfType<IReadOnlyDictionary<string, object?>>();

    private static string Number(object? value) => value switch
    {
        null => "—", double number => number.ToString("0.###", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "",
        _ => value.ToString() ?? ""
    };

    private static void WriteCsv(Stream stream, IEnumerable<IReadOnlyDictionary<string, object?>> rows, string[] columns)
    {
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true);
        writer.WriteLine(string.Join(",", columns));
        foreach (var row in rows)
            writer.WriteLine(string.Join(",", columns.Select(column =>
                "\"" + (row.GetValueOrDefault(column) is null ? "" : Number(row[column])).Replace("\"", "\"\"") + "\"")));
    }

    private static string Markdown(IReadOnlyDictionary<string, object?> snapshot)
    {
        var text = new StringBuilder("# 引擎运行耗时报告\n\n");
        text.AppendLine($"会话：{snapshot["session_id"]}；起始 UTC：{snapshot["started_utc"]}。");
        text.AppendLine($"控制：{snapshot["controller"]}；审计：{snapshot["audit_mode"]}；开发：{snapshot["development_mode"]}；主体数：{snapshot["actor_count"]}；程序集：{snapshot["assembly_id"]}。");
        text.AppendLine($"实际推进 {Number(snapshot["advanced_game_minutes"])} 游戏分钟；引擎累计 {Number(Convert.ToDouble(snapshot["engine_ms"], CultureInfo.InvariantCulture) / 1000d)} 现实秒；平均每游戏小时 {Number(snapshot["seconds_per_game_hour"])} 现实秒。");
        text.AppendLine();
        text.AppendLine("平均值使用累计引擎排他墙钟耗时除以实际前进的游戏小时。包含正式计算和提前准备；开局/恢复、投影、存档/归档、开发命令单独统计。玩家阅读、等待输入、Godot现场移动与动画不进入引擎计算。墙钟耗时包含计算期间的GC与操作系统调度，不能当作线程CPU时间。");
        text.AppendLine("不到一小时的覆盖按实际游戏分钟折算；零推进时比率留空。加载存档开启新统计会话。开发命令产生的时间跳跃不加入正式推进分母，向后调时新建时间段。");
        text.AppendLine("小时表按每项计算开始时的游戏小时归属耗时，实际推进分钟按跨小时边界拆分；跨小时计算不假定均匀分摊。阶段总耗时包含子阶段，self_ms扣除子阶段后可加总。P95为固定直方图的上界估计，分配字节为当前托管线程累计分配。");
        text.AppendLine($"原始采样保留最近 {snapshot["sample_capacity"]} 条；累计 {snapshot["sample_count"]} 条，尾部以外 {snapshot["dropped_samples"]} 条。汇总覆盖全部已结束作用域，当前在途 {snapshot["active_scopes"]} 项；未配对结束 {snapshot["unmatched_phase_ends"]} 项，中断阶段 {snapshot["interrupted_phases"]} 项。报告生成与报告自身写盘不进入该快照。");
        text.AppendLine("\n## 分项累计\n");
        text.AppendLine("| 分项 | 排他耗时 ms | 每游戏小时 s | 分配字节 |");
        text.AppendLine("| --- | ---: | ---: | ---: |");
        foreach (var row in Rows(snapshot, "categories"))
            text.AppendLine($"| {row["label"]} | {Number(row["self_ms"])} | {Number(row["seconds_per_game_hour"])} | {Number(row["allocated_bytes"])} |");
        text.AppendLine("\n## 操作与阶段\n");
        text.AppendLine("| 操作 | 主体 | 次数 | 排他 ms | 均值 ms | P95上界 ms | 最大 ms | 失败 |");
        text.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var row in Rows(snapshot, "operations"))
            text.AppendLine($"| {row["category_label"]}/{row["label"]} | {row["actor"]} | {row["calls"]} | {Number(row["self_ms"])} | {Number(row["mean_ms"])} | {Number(row["p95_upper_ms"])} | {Number(row["max_ms"])} | {row["failed_calls"]} |");
        text.AppendLine("\n## 游戏小时\n");
        text.AppendLine("| 时间段 | 游戏分钟范围 | 覆盖分钟 | 引擎 ms | 每游戏小时 s | 投影 ms | 归档 ms |");
        text.AppendLine("| ---: | --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (var row in Rows(snapshot, "hourly"))
            text.AppendLine($"| {row["epoch"]} | {Number(row["game_start_minute"])}–{Number(row["game_end_minute"])} | {Number(row["covered_game_minutes"])} | {Number(row["engine_ms"])} | {Number(row["seconds_per_game_hour"])} | {Number(row["projection_ms"])} | {Number(row["persistence_ms"])} |");
        return text.ToString();
    }
}
