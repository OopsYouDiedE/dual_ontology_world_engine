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

    private IDisposable Measure(string operation, string category = "engine", string viewer = "", string projectionScope = "")
    {
        var valuesScope = R.UseValues(valueContext);
        try
        {
            var pending = Field(state, "pending", false);
            var actor = pending is false ? "" : R.Text(Field(pending, "id", ""));
            return new MeasuredValues(performance.Begin(operation, category, actor, PerformanceGameMinute, viewer, projectionScope), valuesScope);
        }
        catch { valuesScope.Dispose(); throw; }
    }

    private sealed class MeasuredValues(IDisposable measurement, IDisposable valuesScope) : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // Sample the final DV clock before restoring the caller's context.
            try { measurement.Dispose(); }
            finally { valuesScope.Dispose(); }
        }
    }

    private void ObservePerformancePhase(string phase, string actor) => performance.Phase(phase, actor);
    public IReadOnlyDictionary<string, object?> PerformanceSnapshot()
    {
        using var valuesScope = R.UseValues(valueContext);
        return performance.Snapshot(IsClosed);
    }
    public IReadOnlyDictionary<string, object?> PerformanceSummary()
    {
        using var valuesScope = R.UseValues(valueContext);
        return performance.Summary(IsClosed);
    }
    public void SetPerformanceController(string controller) => performance.ConfigureController(controller);
    public void SetPerformanceProvenance(IReadOnlyDictionary<string, object?> provenance) => performance.SetProvenance(provenance);
    public void SetPerformanceLifecycle(IReadOnlyDictionary<string, object?> lifecycle) => performance.SetLifecycle(lifecycle);
    public void RecordPerformancePlanning(string actor, string eventId, IReadOnlyDictionary<string, object?> work, string stopReason) =>
        performance.RecordPlanning(actor, eventId, work, stopReason);
    public void BeginDecisionInterface(string actor, string phase = "decision") => performance.BeginInterface(actor, phase);
    public void SetDecisionInterfacePhase(string actor, string phase) => performance.SetInterfacePhase(actor, phase);
    public void EndDecisionInterface(string actor, string outcome = "accepted") => performance.EndInterface(actor, outcome);
    public void StartPerformanceRecording(string prefix) => performance.StartRecording(prefix);
    public void StopPerformanceClock() => performance.StopWallClock();
    public void WritePerformanceReport(string prefix)
    {
        using var valuesScope = R.UseValues(valueContext);
        performance.WriteReport(prefix, IsClosed);
    }

    private static DvSession OpenMeasured(string operation, Func<R.ValueContext, DvSession> create)
    {
        var started = Stopwatch.GetTimestamp();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var context = new R.ValueContext();
        using var valuesScope = R.UseValues(context);
        var current = create(context);
        current.performance.RecordStartup(operation, started, allocated, current.PerformanceGameMinute());
        return current;
    }
}

/// <summary>Observational accounting; clocks and counters never participate in world decisions.</summary>
internal sealed class SessionPerformance
{
    private const int SampleCapacity = 8192;
    private const string Contract = "dv-performance-v3";
    private static readonly double[] HistogramBounds =
        [0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 20, 50, 100, 200, 500,
         1000, 2000, 5000, 10000, 30000, 60000, 120000, 300000, 600000];
    private readonly object gate = new();
    private readonly long origin = Stopwatch.GetTimestamp();
    private readonly string sessionId = Guid.NewGuid().ToString("N");
    private readonly string startedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    private readonly List<Frame> stack = [];
    private readonly Dictionary<(string Category, string Operation, string Actor, string Viewer, string Scope), Aggregate> totals = [];
    private readonly SortedDictionary<(int Epoch, long Hour), HourBucket> hours = [];
    private readonly Dictionary<string, long> outcomes = new(StringComparer.Ordinal);
    private readonly Queue<Sample> samples = new(SampleCapacity);
    private readonly Dictionary<string, long> decisionCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Controller, string Actor), DecisionAggregate> decisions = [];
    private readonly Dictionary<string, PlanningAggregate> planning = new(StringComparer.Ordinal);
    private readonly Queue<DecisionSample> decisionSamples = new(SampleCapacity);
    private readonly Dictionary<string, InterfaceWait> interfaceWaits = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> interfaceTicks = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Controller, string Actor, string Phase), InterfaceAggregate> interfacePhases = [];
    private readonly Queue<IReadOnlyDictionary<string, object?>> interfaceEvents = new(SampleCapacity);
    private IReadOnlyDictionary<string, object?> provenance = HostValues.Freeze(new Dictionary<string, object?>());
    private IReadOnlyDictionary<string, object?> lifecycle = HostValues.Freeze(new Dictionary<string, object?>());
    private long sequence, dropped, exceptions, unmatchedPhases, interruptedPhases;
    private long decisionComputeTicks, decisionComputeCalls, decisionComputeIncomplete, decisionInstrumentationTicks;
    private long decisionSequence, droppedDecisions, elapsedWallTicks, wallCursor, rawSequence, interfaceSequence, droppedInterfaces;
    private double wallGameMinute;
    private int wallEpoch;
    private bool wallStarted, wallStopped, controllerLocked;
    private string controllerKind = "engine", wallStartedUtc = "", wallStoppedUtc = "";
    private double? firstGameMinute;
    private double lastGameMinute, advancedMinutes;
    private int epoch;
    private bool auditMode, developerMode;
    private string simulationMode = "unrecorded";
    private string playerActor = "";
    private int actorCount;
    private FileStream? recording;
    private string? recordingPrefix, recordingError;
    private long recordingOmittedPrefix;
    private bool recordingSealed;

    internal void Configure(bool audit, bool developer, string player, int actors, double minute, string mode)
    {
        lock (gate)
        {
            auditMode = audit; developerMode = developer; simulationMode = mode; playerActor = player; actorCount = actors;
            controllerKind = player.Length == 0 ? "engine" : "human";
            firstGameMinute = lastGameMinute = minute;
        }
    }

    internal string ControllerKind { get { lock (gate) return controllerKind; } }

    internal void ConfigureController(string controller)
    {
        var kind = controller.Trim().ToLowerInvariant() switch
        {
            "ai" or "engine" => "engine", "player" or "human" => "human", "llm" => "llm",
            _ => throw new ArgumentException("Performance controller must be engine, human or llm.", nameof(controller))
        };
        lock (gate)
        {
            if ((kind == "engine") != (playerActor.Length == 0))
                throw new InvalidOperationException("The performance controller must match the session's actual player control.");
            if (kind == controllerKind) return;
            if (controllerLocked || wallStopped)
                throw new InvalidOperationException("Choose the performance controller before the first engine operation.");
            controllerKind = kind;
        }
    }

    internal void SetProvenance(IReadOnlyDictionary<string, object?> value)
    {
        lock (gate) provenance = HostValues.Freeze(value.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal));
    }

    internal void SetLifecycle(IReadOnlyDictionary<string, object?> value)
    {
        lock (gate) lifecycle = HostValues.Freeze(value.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal));
    }

    private sealed class DecisionAggregate
    {
        internal long Count;
        internal string FirstEvent = "", LastEvent = "";
        internal double FirstMinute, LastMinute;
    }

    private sealed record DecisionSample(long Sequence, long RecordSequence, string Controller, string Actor, string Event,
        string Action, int Epoch, double GameMinute, long ObservedTicks);

    private sealed class PlanningAggregate
    {
        internal long Decisions, CacheHits;
        internal string FirstEvent = "", LastEvent = "";
        internal readonly Dictionary<string, double> Work = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, long> Stops = new(StringComparer.Ordinal);
    }

    private static readonly string[] PlanningSums =
        ["candidates_considered", "candidates_skipped", "candidate_order_scans", "candidate_order_reuses", "admission_checks", "eligible_evaluations", "forecasts", "forecast_reuses", "expanded_nodes", "candidate_paths", "retained_paths", "route_reuses", "skipped_expansion", "partial_layer", "examined_layers", "completed_layers"];
    private static readonly string[] PlanningMaxima = ["peak_frontier", "budget_limit", "depth_limit", "lookahead_limit"];

    private static double? PlanningNumber(object? value)
    {
        double? number = value switch
        {
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal =>
                Convert.ToDouble(value, CultureInfo.InvariantCulture),
            JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var parsed) => parsed,
            _ => null
        };
        return number.HasValue && double.IsFinite(number.Value) && number.Value >= 0 ? number : null;
    }

    internal void RecordPlanning(string actor, string eventId, IReadOnlyDictionary<string, object?> work, string stopReason)
    {
        if (work.Count == 0) return;
        lock (gate)
        {
            if (!planning.TryGetValue(actor, out var aggregate)) planning[actor] = aggregate = new();
            if (aggregate.Decisions == 0) aggregate.FirstEvent = eventId;
            aggregate.Decisions++;
            aggregate.LastEvent = eventId;
            var cacheHit = work.GetValueOrDefault("cache_hit") is true;
            if (cacheHit) aggregate.CacheHits++;
            foreach (var field in PlanningSums)
            {
                var value = work.GetValueOrDefault(field);
                var number = (field is "skipped_expansion" or "partial_layer") && value is bool skipped
                    ? skipped ? 1d : 0d : PlanningNumber(value);
                if (number is { } amount)
                {
                    var currentReuse = field is "route_reuses" or "forecast_reuses" or "candidate_order_reuses" or "skipped_expansion";
                    aggregate.Work[field] = aggregate.Work.GetValueOrDefault(field) + (cacheHit && !currentReuse ? 0 : amount);
                }
            }
            foreach (var field in PlanningMaxima)
                if (PlanningNumber(work.GetValueOrDefault(field)) is { } amount)
                    aggregate.Work[field] = Math.Max(aggregate.Work.GetValueOrDefault(field), field == "peak_frontier" && cacheHit ? 0 : amount);
            var reason = string.IsNullOrWhiteSpace(stopReason) ? "unrecorded" : stopReason;
            aggregate.Stops[reason] = aggregate.Stops.GetValueOrDefault(reason) + 1;
        }
    }

    internal void RecordDecision(string source, string actor, string eventId, string actionId, double gameMinute)
    {
        lock (gate)
        {
            controllerLocked = true;
            var now = Stopwatch.GetTimestamp();
            AccrueWallLocked(now);
            decisionCounts[source] = decisionCounts.GetValueOrDefault(source) + 1;
            var key = (source, actor);
            if (!decisions.TryGetValue(key, out var aggregate)) decisions[key] = aggregate = new();
            if (aggregate.Count == 0) { aggregate.FirstEvent = eventId; aggregate.FirstMinute = gameMinute; }
            aggregate.Count++;
            aggregate.LastEvent = eventId; aggregate.LastMinute = gameMinute;
            var bucket = Bucket(epoch, (long)Math.Floor(gameMinute / 60d));
            bucket.Decisions[source] = bucket.Decisions.GetValueOrDefault(source) + 1;
            var sample = new DecisionSample(++decisionSequence, ++rawSequence, source, actor, eventId, actionId,
                epoch, gameMinute, now - origin);
            if (decisionSamples.Count == SampleCapacity) { decisionSamples.Dequeue(); droppedDecisions++; }
            decisionSamples.Enqueue(sample);
            StreamRecordLocked(DecisionRecord(sample));
            if (source is "human" or "llm") EndInterfaceLocked(actor, "accepted", eventId, now);
        }
    }

    private sealed class InterfaceWait
    {
        internal required string Controller, Actor, Phase;
        internal required long Started, PhaseStarted, Cursor;
    }

    private sealed class InterfaceAggregate
    {
        internal long Ticks, Segments, Accepted, Interrupted;
    }

    internal void BeginInterface(string actor, string phase)
    {
        lock (gate)
        {
            if (controllerKind == "engine" || wallStopped || actor != playerActor || interfaceWaits.ContainsKey(actor)) return;
            controllerLocked = true;
            var now = Stopwatch.GetTimestamp();
            AccrueWallLocked(now);
            var wait = new InterfaceWait
            {
                Controller = controllerKind, Actor = actor, Phase = NormalizePhase(phase),
                Started = now, PhaseStarted = now, Cursor = now
            };
            interfaceWaits[actor] = wait;
            interfaceTicks.TryAdd(wait.Controller, 0);
            Bucket(wallEpoch, (long)Math.Floor(wallGameMinute / 60d)).InterfaceTicks.TryAdd(wait.Controller, 0);
            InterfacePhase(wait).Segments++;
            RecordInterfaceEventLocked("request", wait, "", "", now);
        }
    }

    internal void SetInterfacePhase(string actor, string phase)
    {
        lock (gate)
        {
            if (!interfaceWaits.TryGetValue(actor, out var wait) || wallStopped) return;
            var next = NormalizePhase(phase);
            if (next == wait.Phase) return;
            var now = Stopwatch.GetTimestamp();
            AccrueWallLocked(now);
            RecordInterfaceEventLocked("phase-end", wait, "continued", "", now);
            wait.Phase = next;
            wait.PhaseStarted = now;
            InterfacePhase(wait).Segments++;
            RecordInterfaceEventLocked(next == "response" ? "response" : "phase-start", wait, "", "", now);
        }
    }

    internal void EndInterface(string actor, string outcome)
    {
        lock (gate)
        {
            var now = Stopwatch.GetTimestamp();
            AccrueWallLocked(now);
            EndInterfaceLocked(actor, outcome, "", now);
        }
    }

    private void EndInterfaceLocked(string actor, string outcome, string eventId, long now)
    {
        if (!interfaceWaits.Remove(actor, out var wait)) return;
        if (outcome == "accepted") InterfacePhase(wait).Accepted++;
        else InterfacePhase(wait).Interrupted++;
        RecordInterfaceEventLocked(outcome == "accepted" ? "accepted" : "outcome", wait, outcome, eventId, now);
    }

    private static string NormalizePhase(string phase) => string.IsNullOrWhiteSpace(phase) ? "decision" : phase.Trim();

    private InterfaceAggregate InterfacePhase(InterfaceWait wait)
    {
        var key = (wait.Controller, wait.Actor, wait.Phase);
        if (!interfacePhases.TryGetValue(key, out var aggregate)) interfacePhases[key] = aggregate = new();
        return aggregate;
    }

    private void RecordInterfaceEventLocked(string stage, InterfaceWait wait, string outcome, string eventId, long now)
    {
        var record = HostValues.Freeze(new Dictionary<string, object?>
        {
            ["type"] = "decision-interface", ["record_sequence"] = ++rawSequence, ["sequence"] = ++interfaceSequence,
            ["stage"] = stage, ["phase"] = wait.Phase, ["controller_kind"] = wait.Controller, ["actor"] = wait.Actor,
            ["request_ticks"] = wait.Started - origin, ["observed_ticks"] = now - origin,
            ["elapsed_ticks"] = Math.Max(0, now - wait.Started), ["phase_elapsed_ticks"] = Math.Max(0, now - wait.PhaseStarted),
            ["epoch"] = wallEpoch, ["game_minute"] = wallGameMinute, ["outcome"] = outcome, ["event_id"] = eventId
        });
        if (interfaceEvents.Count == SampleCapacity) { interfaceEvents.Dequeue(); droppedInterfaces++; }
        interfaceEvents.Enqueue(record);
        StreamRecordLocked(record);
    }

    private void AccrueWallLocked(long now)
    {
        if (!wallStarted || wallStopped) return;
        now = Math.Max(now, wallCursor);
        var bucket = Bucket(wallEpoch, (long)Math.Floor(wallGameMinute / 60d));
        var ticks = now - wallCursor;
        elapsedWallTicks += ticks;
        bucket.WallTicks += ticks;
        foreach (var wait in interfaceWaits.Values)
        {
            var elapsed = Math.Max(0, now - wait.Cursor);
            interfaceTicks[wait.Controller] = interfaceTicks.GetValueOrDefault(wait.Controller) + elapsed;
            bucket.InterfaceTicks[wait.Controller] = bucket.InterfaceTicks.GetValueOrDefault(wait.Controller) + elapsed;
            InterfacePhase(wait).Ticks += elapsed;
            wait.Cursor = now;
        }
        wallCursor = now;
    }

    internal void StopWallClock()
    {
        lock (gate)
        {
            if (wallStopped) return;
            var now = Stopwatch.GetTimestamp();
            AccrueWallLocked(now);
            foreach (var actor in interfaceWaits.Keys.ToArray()) EndInterfaceLocked(actor, "closed-pending", "", now);
            wallStopped = true;
            wallStoppedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        }
    }

    private sealed class Frame
    {
        internal required string Operation, Category, Actor, Viewer, ProjectionScope;
        internal required long Started, Allocated;
        internal required double GameStart;
        internal required int Epoch;
        internal required bool IsPhase;
        internal long ChildTicks, ChildAllocated, DecisionInstrumentationAtStart;
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
            var index = Array.BinarySearch(HistogramBounds, Milliseconds(ticks));
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
        internal long WallTicks;
        internal readonly Dictionary<string, long> SelfTicks = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, long> Decisions = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, long> InterfaceTicks = new(StringComparer.Ordinal);
    }

    private sealed record Sample(long Sequence, long RecordSequence, string Operation, string Category, string Actor,
        string Viewer, string ProjectionScope, int Epoch, double GameStart, double GameEnd, long StartTicks,
        long DurationTicks, long SelfTicks, long AllocatedBytes, long SelfAllocatedBytes, bool Failed, bool PhaseComplete,
        long? DecisionComputeTicks);

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

    internal IDisposable Begin(string operation, string category, string actor, Func<double> clock, string viewer = "", string projectionScope = "")
    {
        var minute = clock();
        lock (gate)
        {
            firstGameMinute ??= minute;
            AccrueWallLocked(Stopwatch.GetTimestamp());
            if (stack.Count == 0) { wallGameMinute = minute; wallEpoch = epoch; }
            if (category is "engine" or "speculation" or "development") controllerLocked = true;
            var frame = new Frame
            {
                Operation = operation, Category = category, Actor = actor, Viewer = viewer, ProjectionScope = projectionScope,
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
        var name = phase[..^(begin ? 6 : 4)];
        lock (gate)
        {
            if (stack.Count == 0) return;
            if (begin)
            {
                var parent = stack[^1];
                stack.Add(new Frame
                {
                    Operation = "phase." + name, Category = parent.Category,
                    Actor = parent.Category == "projection" ? parent.Actor : actor,
                    Viewer = parent.Category == "projection" && parent.Viewer.Length == 0 ? actor : parent.Viewer,
                    ProjectionScope = parent.ProjectionScope,
                    Started = Stopwatch.GetTimestamp(), Allocated = GC.GetAllocatedBytesForCurrentThread(),
                    GameStart = parent.GameStart, Epoch = parent.Epoch, IsPhase = true,
                    DecisionInstrumentationAtStart = decisionInstrumentationTicks
                });
                return;
            }
            var index = stack.FindLastIndex(frame => frame.IsPhase && frame.Operation == "phase." + name &&
                (frame.Category == "projection" || frame.Actor == actor));
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

    internal long DecisionInstrumentationCursor { get { lock (gate) return decisionInstrumentationTicks; } }

    internal void ExcludeDecisionInstrumentation(long ticks, long excludedBefore)
    {
        // A diagnostic callback may itself write performance records. Charge
        // the enclosing interval once, rather than subtracting nested I/O twice.
        lock (gate) decisionInstrumentationTicks += Math.Max(0, ticks - (decisionInstrumentationTicks - excludedBefore));
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
        lock (gate)
        {
            var key = outcome.Length == 0 ? "unrecorded" : outcome;
            outcomes[key] = outcomes.GetValueOrDefault(key) + 1;
        }
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
            if (root) AccrueWallLocked(stopped);
            FinishTop(stopped, allocated, minute, false, root);
            if (root)
            {
                if (minute < frame.GameStart) epoch++;
                lastGameMinute = minute;
                wallGameMinute = minute;
                wallEpoch = epoch;
            }
        }
    }

    private void FinishTop(long stopped, long allocated, double minute, bool incomplete, bool root)
    {
        var frame = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        var ticks = Math.Max(0, stopped - frame.Started);
        long? decisionTicks = null;
        if (frame.Operation == "phase.decision-compute" && EngineCategory(frame.Category))
        {
            decisionComputeCalls++;
            if (incomplete || frame.Failed) decisionComputeIncomplete++;
            else
            {
                decisionTicks = Math.Max(0, ticks - (decisionInstrumentationTicks - frame.DecisionInstrumentationAtStart));
                decisionComputeTicks += decisionTicks.Value;
            }
        }
        var bytes = Math.Max(0, allocated - frame.Allocated);
        var self = Math.Max(0, ticks - frame.ChildTicks);
        var selfBytes = Math.Max(0, bytes - frame.ChildAllocated);
        if (stack.Count > 0)
        {
            stack[^1].ChildTicks += ticks;
            stack[^1].ChildAllocated += bytes;
        }
        Add(frame.Category, frame.Operation, frame.Actor, frame.Viewer, frame.ProjectionScope, frame.Epoch, frame.GameStart,
            minute, frame.Started, ticks, self, bytes, selfBytes, frame.Failed, incomplete, decisionTicks);
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

    private void Add(string category, string operation, string actor, string viewer, string projectionScope, int timeline,
        double gameStart, double gameEnd, long started, long ticks, long self, long allocated, long selfAllocated, bool failed, bool incomplete,
        long? decisionTicks = null)
    {
        var key = (category, operation, actor, viewer, projectionScope);
        if (!totals.TryGetValue(key, out var aggregate)) totals[key] = aggregate = new();
        aggregate.Add(ticks, self, allocated, selfAllocated, failed, incomplete);
        var bucket = Bucket(timeline, (long)Math.Floor(gameStart / 60d));
        bucket.SelfTicks[category] = bucket.SelfTicks.GetValueOrDefault(category) + self;
        var sample = new Sample(++sequence, ++rawSequence, operation, category, actor, viewer, projectionScope,
            timeline, gameStart, gameEnd, started - origin, ticks, self, allocated, selfAllocated, failed, !incomplete, decisionTicks);
        if (samples.Count == SampleCapacity) { samples.Dequeue(); dropped++; }
        samples.Enqueue(sample);
        StreamRecordLocked(SampleRecord(sample));
    }

    internal void RecordStartup(string operation, long started, long allocated, double minute)
    {
        var stopped = Stopwatch.GetTimestamp();
        var bytes = Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - allocated);
        lock (gate)
        {
            firstGameMinute ??= minute;
            lastGameMinute = minute;
            Add("startup", operation, "", "", "", epoch, minute, minute, started, stopped - started,
                stopped - started, bytes, bytes, false, false);
            wallStarted = true;
            wallCursor = stopped;
            wallGameMinute = minute;
            wallEpoch = epoch;
            wallStartedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        }
    }

    private static double Milliseconds(long ticks) => ticks * (1000d / Stopwatch.Frequency);
    private static bool EngineCategory(string category) => category is "engine" or "speculation";
    private static object? Ratio(double numerator, double denominator) => denominator > 0 ? numerator / denominator : null;
    private static object? Frequency(long count, double milliseconds) => Ratio(count * 3600000d, milliseconds);
    private static string ControllerLabel(string kind) => kind switch
    {
        "engine" => "引擎", "human" => "人工", "llm" => "大模型", _ => kind
    };
    private static string CategoryLabel(string category) => category switch
    {
        "engine" => "正式计算", "speculation" => "提前准备", "projection" => "视图投影",
        "startup" => "初始化与恢复", "persistence" => "保存与归档", "development" => "开发操作", _ => category
    };
    private static string OperationLabel(string operation) => operation switch
    {
        "phase.cognition" => "人物认知", "phase.choice" => "行动选策", "phase.decision-compute" => "选策计算", "phase.goals" => "目标整理",
        "phase.planning-index" => "规划索引", "phase.planning-dependencies" => "规划依赖",
        "phase.planning-expand" => "规划展开", "phase.planning-forecast" => "规划预测",
        "phase.observer-index" => "投影索引",
        "phase.schedule" => "行动排队", "phase.observer-members" => "地图成员",
        "phase.observer-sites" => "交互站点", "phase.observer-doors" => "通道状态",
        "phase.observer-options" => "行动候选", "prepare" => "准备行动", "background-prepare" => "后台准备",
        "prepare-ahead" => "提前计算", "adopt-prepared" => "采用提前结果", "complete" => "正式结算",
        "intent" => "行动意图", "presentation" => "正文投影", "host-snapshot" => "宿主快照",
        "host-delta" => "宿主增量", "executor-snapshot" => "执行者投影", "text-status" => "状态投影",
        "inspect" => "开发查看", "observer-preview" => "观察者预览", "observer-subjects" or "observer-subject" => "主观地图",
        "observer-objective" => "客观地图", "observer-directory" => "主体目录", "observer-options" => "观察者选项",
        "development" => "开发命令", "open-campaign" or "open-configured" or "open-scenario" => "初始化",
        "restore-value" or "restore-file" => "恢复存档", "save" or "save-json" => "保存",
        "trace" or "trace-json" => "轨迹封存", "close" => "关闭会话", _ => operation
    };

    private static double? InterfaceMilliseconds(string source, double engineMs, IReadOnlyDictionary<string, long> waits) =>
        source == "engine" ? engineMs : waits.TryGetValue(source, out var ticks) ? Milliseconds(ticks) : null;

    private void AppendActivity(Dictionary<string, object?> values, double minutes, double engineMs,
        long wallTicks, IReadOnlyDictionary<string, long> counts, IReadOnlyDictionary<string, long> waits)
    {
        var wallMs = Milliseconds(wallTicks);
        var cost = InterfaceMilliseconds(controllerKind, engineMs, waits);
        values["controller_kind"] = controllerKind;
        values["controller_label"] = ControllerLabel(controllerKind);
        values["advanced_game_minutes"] = minutes;
        values["elapsed_wall_ms"] = wallMs;
        values["total_decisions"] = counts.Values.Sum();
        values["game_hours_per_real_hour"] = Ratio(minutes * 60000d, wallMs);
        values["decisions_per_real_hour"] = Frequency(counts.Values.Sum(), wallMs);
        values["interface_total_ms"] = cost;
        values["interface_hours_per_real_hour"] = cost.HasValue ? Ratio(cost.Value, wallMs) : null;
        values["sample_window_partial"] = wallMs < 3600000d;
    }

    private void AppendPerGameHour(Dictionary<string, object?> values)
    {
        var gameHours = advancedMinutes / 60d;
        var choiceMs = Milliseconds(decisionComputeTicks);
        var activeEngine = stack.Any(frame => EngineCategory(frame.Category));
        var complete = !activeEngine && decisionComputeIncomplete == 0;
        values["per_game_hour_measurement_complete"] = complete;
        values["engine_decision_count"] = decisionCounts.GetValueOrDefault("engine");
        values["engine_decision_compute_total_ms"] = complete ? choiceMs : null;
        values["engine_decision_compute_recorded_ms"] = choiceMs;
        values["engine_decision_compute_calls"] = decisionComputeCalls;
        values["engine_decision_compute_incomplete_calls"] = decisionComputeIncomplete;
        values["engine_decisions_per_game_hour"] = !activeEngine ? Ratio(decisionCounts.GetValueOrDefault("engine"), gameHours) : null;
        values["engine_decision_compute_ms_per_game_hour"] = complete ? Ratio(choiceMs, gameHours) : null;
        values["elapsed_real_ms_per_game_hour"] = !activeEngine ? Ratio(Milliseconds(elapsedWallTicks), gameHours) : null;
        values["per_game_hour_basis"] = "current session accumulated forward game-hour advancement; zero advancement is missing";
        values["engine_decision_compute_basis"] = "dvs_choose wall time including planning and unadopted speculation; excludes cognition, execution, projection, waiting, performance-stream encoding/writes and external phase-observer callbacks; not CPU time";
    }

    private object[] DecisionRows(double wallMs, string actor = "") => decisions.Where(item => actor.Length == 0 || item.Key.Actor == actor).OrderBy(item => item.Key.Controller, StringComparer.Ordinal)
        .ThenBy(item => item.Key.Actor, StringComparer.Ordinal)
        .Select(item => (object)HostValues.Freeze(new Dictionary<string, object?>
        {
            ["actor"] = item.Key.Actor, ["controller_kind"] = item.Key.Controller, ["controller_label"] = ControllerLabel(item.Key.Controller),
            ["decisions"] = item.Value.Count, ["decisions_per_real_hour"] = Frequency(item.Value.Count, wallMs),
            ["first_event"] = item.Value.FirstEvent, ["last_event"] = item.Value.LastEvent,
            ["first_game_minute"] = item.Value.FirstMinute, ["last_game_minute"] = item.Value.LastMinute
        })).ToArray();

    internal IReadOnlyDictionary<string, object?> Summary(bool closed)
    {
        lock (gate)
        {
            var now = Stopwatch.GetTimestamp();
            AccrueWallLocked(now);
            var engine = totals.Where(item => EngineCategory(item.Key.Category)).Sum(item => Milliseconds(item.Value.SelfTicks));
            var values = new Dictionary<string, object?>
            {
                ["contract"] = Contract, ["closed"] = closed, ["engine_ms"] = engine,
                ["operation_costs"] = Array.AsReadOnly(totals
                    .GroupBy(item => (item.Key.Category, item.Key.Operation))
                    .OrderByDescending(group => group.Sum(item => item.Value.SelfTicks))
                    .Select(group => (object)HostValues.Freeze(new Dictionary<string, object?>
                    {
                        ["category"] = group.Key.Category, ["operation"] = group.Key.Operation,
                        ["calls"] = group.Sum(item => item.Value.Count),
                        ["exclusive_total_ms"] = group.Sum(item => Milliseconds(item.Value.SelfTicks)),
                        ["inclusive_total_ms"] = group.Sum(item => Milliseconds(item.Value.Ticks)),
                        ["self_allocated_bytes"] = group.Sum(item => item.Value.SelfAllocated)
                    })).ToArray()),
                ["planning_mode"] = simulationMode,
                ["planning_mode_approximation"] = simulationMode == "approximate-v1" ? true : (bool?)null,
                ["measured_until_ticks"] = now - origin, ["wall_measured_until_ticks"] = wallStarted ? wallCursor - origin : null,
                ["stopwatch_frequency"] = Stopwatch.Frequency, ["wall_clock_stopped"] = wallStopped,
                ["active_scopes"] = stack.Count, ["active_decision_interfaces"] = interfaceWaits.Count,
                ["lifecycle"] = controllerKind == "engine" ? lifecycle : HostValues.Empty,
                ["decision_actor_scope"] = playerActor.Length == 0 ? "all" : playerActor,
                ["decisions"] = Array.AsReadOnly(DecisionRows(Milliseconds(elapsedWallTicks), playerActor))
            };
            AppendActivity(values, advancedMinutes, engine, elapsedWallTicks, decisionCounts, interfaceTicks);
            if (controllerKind == "engine") AppendPerGameHour(values);
            return HostValues.Freeze(values);
        }
    }

    internal IReadOnlyDictionary<string, object?> Snapshot(bool closed)
    {
        lock (gate) return SnapshotLocked(closed);
    }

    private IReadOnlyDictionary<string, object?> SnapshotLocked(bool closed)
    {
        var now = Stopwatch.GetTimestamp();
        AccrueWallLocked(now);
        var wallMs = Milliseconds(elapsedWallTicks);
        var engineMs = totals.Where(item => EngineCategory(item.Key.Category)).Sum(item => Milliseconds(item.Value.SelfTicks));
        var categoryRows = totals.GroupBy(item => item.Key.Category, StringComparer.Ordinal)
            .OrderByDescending(group => group.Sum(item => item.Value.SelfTicks))
            .Select(group => (object)HostValues.Freeze(new Dictionary<string, object?>
            {
                ["category"] = group.Key, ["label"] = CategoryLabel(group.Key),
                ["exclusive_total_ms"] = group.Sum(item => Milliseconds(item.Value.SelfTicks)),
                ["self_allocated_bytes"] = group.Sum(item => item.Value.SelfAllocated)
            })).ToArray();
        var operationRows = totals.OrderByDescending(item => item.Value.SelfTicks)
            .ThenBy(item => item.Key.Operation, StringComparer.Ordinal).ThenBy(item => item.Key.Actor, StringComparer.Ordinal)
            .Select(item => (object)HostValues.Freeze(new Dictionary<string, object?>
            {
                ["category"] = item.Key.Category, ["operation"] = item.Key.Operation, ["trigger_actor"] = item.Key.Actor,
                ["viewer"] = item.Key.Viewer, ["projection_scope"] = item.Key.Scope,
                ["category_label"] = CategoryLabel(item.Key.Category), ["label"] = OperationLabel(item.Key.Operation),
                ["calls"] = item.Value.Count, ["failed_calls"] = item.Value.Failed, ["incomplete_phases"] = item.Value.Incomplete,
                ["inclusive_total_ms"] = Milliseconds(item.Value.Ticks), ["exclusive_total_ms"] = Milliseconds(item.Value.SelfTicks),
                ["inclusive_mean_ms"] = Milliseconds(item.Value.Ticks) / item.Value.Count,
                ["exclusive_mean_ms"] = Milliseconds(item.Value.SelfTicks) / item.Value.Count,
                ["inclusive_p95_upper_ms"] = item.Value.PercentileUpper(0.95), ["inclusive_max_ms"] = Milliseconds(item.Value.Maximum),
                ["allocated_bytes"] = item.Value.Allocated, ["self_allocated_bytes"] = item.Value.SelfAllocated
            })).ToArray();
        var decisionRows = DecisionRows(wallMs);
        var sources = decisionCounts.Keys.Concat(interfaceTicks.Keys).Append("engine").Append(controllerKind)
            .Distinct(StringComparer.Ordinal).OrderBy(source => source, StringComparer.Ordinal);
        var sourceRows = sources.Select(source =>
        {
            var cost = InterfaceMilliseconds(source, engineMs, interfaceTicks);
            return (object)HostValues.Freeze(new Dictionary<string, object?>
            {
                ["controller_kind"] = source, ["controller_label"] = ControllerLabel(source),
                ["decisions"] = decisionCounts.GetValueOrDefault(source),
                ["decisions_per_real_hour"] = Frequency(decisionCounts.GetValueOrDefault(source), wallMs),
                ["interface_total_ms"] = cost, ["interface_hours_per_real_hour"] = cost.HasValue ? Ratio(cost.Value, wallMs) : null,
                ["measurement"] = source == "engine" ? "exclusive-engine-and-speculation" : "choice-visible-to-accepted-or-cancelled"
            });
        }).ToArray();
        var interfaceRows = interfacePhases.OrderByDescending(item => item.Value.Ticks).Select(item =>
            (object)HostValues.Freeze(new Dictionary<string, object?>
            {
                ["controller_kind"] = item.Key.Controller, ["actor"] = item.Key.Actor, ["phase"] = item.Key.Phase,
                ["elapsed_ms"] = Milliseconds(item.Value.Ticks), ["segments"] = item.Value.Segments,
                ["accepted"] = item.Value.Accepted, ["interrupted"] = item.Value.Interrupted
            })).ToArray();
        var planningRows = planning.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item =>
        {
            var row = new Dictionary<string, object?>
            {
                ["actor"] = item.Key, ["decisions"] = item.Value.Decisions, ["cache_hits"] = item.Value.CacheHits,
                ["first_event"] = item.Value.FirstEvent, ["last_event"] = item.Value.LastEvent,
                ["stop_reasons"] = HostValues.Freeze(item.Value.Stops.ToDictionary(stop => stop.Key, stop => (object?)stop.Value, StringComparer.Ordinal))
            };
            foreach (var field in PlanningSums.Concat(PlanningMaxima))
                row[field] = item.Value.Work.TryGetValue(field, out var amount) ? amount : null;
            return (object)HostValues.Freeze(row);
        }).ToArray();
        var hourRows = hours.Select(item =>
        {
            double Category(string category) => Milliseconds(item.Value.SelfTicks.GetValueOrDefault(category));
            var engine = Category("engine") + Category("speculation");
            var values = new Dictionary<string, object?>
            {
                ["epoch"] = item.Key.Epoch, ["game_hour"] = item.Key.Hour,
                ["game_start_minute"] = item.Key.Hour * 60d, ["game_end_minute"] = (item.Key.Hour + 1) * 60d,
                ["covered_game_minutes"] = item.Value.CoveredMinutes,
                ["engine_ms"] = Category("engine"), ["speculation_ms"] = Category("speculation"),
                ["projection_ms"] = Category("projection"), ["persistence_ms"] = Category("persistence"),
                ["startup_ms"] = Category("startup"), ["development_ms"] = Category("development"),
                ["engine_decisions"] = item.Value.Decisions.GetValueOrDefault("engine"),
                ["human_decisions"] = item.Value.Decisions.GetValueOrDefault("human"),
                ["llm_decisions"] = item.Value.Decisions.GetValueOrDefault("llm")
            };
            AppendActivity(values, item.Value.CoveredMinutes, engine, item.Value.WallTicks, item.Value.Decisions, item.Value.InterfaceTicks);
            return (object)HostValues.Freeze(values);
        }).ToArray();
        var result = new Dictionary<string, object?>
        {
            ["contract"] = Contract, ["session_id"] = sessionId, ["started_utc"] = startedUtc,
            ["runtime"] = Environment.Version.ToString(), ["stopwatch_frequency"] = Stopwatch.Frequency,
            ["assembly_id"] = typeof(DvSession).Assembly.ManifestModule.ModuleVersionId.ToString(),
            ["audit_mode"] = auditMode, ["development_mode"] = developerMode, ["player_actor"] = playerActor, ["actor_count"] = actorCount,
            ["planning_mode"] = simulationMode,
            ["planning_mode_approximation"] = simulationMode == "approximate-v1" ? true : (bool?)null,
            ["closed"] = closed, ["active_scopes"] = stack.Count, ["active_decision_interfaces"] = interfaceWaits.Count,
            ["game_start_minute"] = firstGameMinute, ["game_end_minute"] = lastGameMinute, ["engine_ms"] = engineMs,
            ["measured_until_ticks"] = now - origin, ["wall_measured_until_ticks"] = wallStarted ? wallCursor - origin : null,
            ["rate_basis"] = "game-hours, committed-decisions, and accumulated-interface-hours / elapsed-real-session-hours",
            ["decision_basis"] = "new action_selected events in committed session state; development, restored history and unadopted branches excluded",
            ["wall_clock_basis"] = "open/restore completed through host shutdown request; includes reading, input, execution and projection; excludes initialization and shutdown persistence",
            ["interface_basis"] = "engine: exclusive engine+speculation; human/llm: observed choice visibility through accepted choice, cancellation or shutdown; overlapping interfaces are cumulative",
            ["hour_attribution"] = "game-clock buckets: costs at operation start; waiting at current committed time; forward game coverage split at hour boundaries",
            ["wall_started_utc"] = wallStartedUtc, ["wall_stopped_utc"] = wallStoppedUtc,
            ["wall_clock_started"] = wallStarted, ["wall_clock_stopped"] = wallStopped,
            ["clock_discontinuities"] = epoch, ["sample_capacity"] = SampleCapacity,
            ["sample_count"] = sequence, ["retained_samples"] = samples.Count, ["tail_omitted_samples"] = dropped,
            ["decision_sample_count"] = decisionSequence, ["retained_decision_samples"] = decisionSamples.Count,
            ["tail_omitted_decisions"] = droppedDecisions, ["interface_event_count"] = interfaceSequence,
            ["retained_interface_events"] = interfaceEvents.Count, ["tail_omitted_interface_events"] = droppedInterfaces,
            ["raw_record_count"] = rawSequence, ["raw_recording"] = recordingPrefix is not null,
            ["raw_recording_policy"] = recordingPrefix is null ? "bounded-tail-export" : "buffered-binary-stream",
            ["raw_omitted_prefix_records"] = recordingPrefix is null ? dropped + droppedDecisions + droppedInterfaces : recordingOmittedPrefix,
            ["raw_recording_error"] = recordingError, ["exceptions"] = exceptions,
            ["unmatched_phase_ends"] = unmatchedPhases, ["interrupted_phases"] = interruptedPhases,
            ["percentile_method"] = "inclusive-duration fixed-histogram upper bound, capped by observed maximum",
            ["allocation_basis"] = "current managed thread cumulative allocation; not peak or retained memory",
            ["failure_basis"] = "reported invocation and persistence exceptions; action outcomes and interface cancellation are separate",
            ["provenance"] = provenance, ["lifecycle"] = lifecycle,
            ["categories"] = Array.AsReadOnly(categoryRows), ["operations"] = Array.AsReadOnly(operationRows),
            ["decision_actor_scope"] = "all",
            ["decisions"] = Array.AsReadOnly(decisionRows), ["decision_sources"] = Array.AsReadOnly(sourceRows),
            ["interface_phases"] = Array.AsReadOnly(interfaceRows), ["planning"] = Array.AsReadOnly(planningRows),
            ["hourly"] = Array.AsReadOnly(hourRows),
            ["outcomes"] = HostValues.Freeze(outcomes.ToDictionary(item => item.Key, item => (object?)item.Value, StringComparer.Ordinal))
        };
        AppendActivity(result, advancedMinutes, engineMs, elapsedWallTicks, decisionCounts, interfaceTicks);
        AppendPerGameHour(result);
        return HostValues.Freeze(result);
    }

    private static IReadOnlyDictionary<string, object?> SampleRecord(Sample sample) => new Dictionary<string, object?>
    {
        ["type"] = "performance-sample", ["record_sequence"] = sample.RecordSequence, ["sequence"] = sample.Sequence,
        ["operation"] = sample.Operation, ["category"] = sample.Category, ["trigger_actor"] = sample.Actor,
        ["viewer"] = sample.Viewer, ["projection_scope"] = sample.ProjectionScope, ["epoch"] = sample.Epoch,
        ["game_start_minute"] = sample.GameStart, ["game_end_minute"] = sample.GameEnd,
        ["start_ticks"] = sample.StartTicks, ["duration_ticks"] = sample.DurationTicks, ["self_ticks"] = sample.SelfTicks,
        ["allocated_bytes"] = sample.AllocatedBytes, ["self_allocated_bytes"] = sample.SelfAllocatedBytes,
        ["failed"] = sample.Failed, ["phase_complete"] = sample.PhaseComplete,
        ["decision_compute_ticks"] = sample.DecisionComputeTicks
    };

    private static IReadOnlyDictionary<string, object?> DecisionRecord(DecisionSample decision) => new Dictionary<string, object?>
    {
        ["type"] = "performance-decision", ["record_sequence"] = decision.RecordSequence, ["sequence"] = decision.Sequence,
        ["controller_kind"] = decision.Controller, ["actor"] = decision.Actor,
        ["event_id"] = decision.Event, ["action_id"] = decision.Action, ["epoch"] = decision.Epoch,
        ["game_minute"] = decision.GameMinute, ["observed_ticks"] = decision.ObservedTicks
    };

    private IReadOnlyDictionary<string, object?>[] RetainedRecordsLocked() =>
        samples.Select(SampleRecord).Concat(decisionSamples.Select(DecisionRecord)).Concat(interfaceEvents)
            .OrderBy(record => Convert.ToInt64(record["record_sequence"], CultureInfo.InvariantCulture)).ToArray();

    private Dictionary<string, object?> Header(string policy, long omitted) => new()
    {
        ["type"] = "performance-header", ["contract"] = Contract, ["session_id"] = sessionId,
        ["sample_policy"] = policy, ["omitted_prefix_records"] = omitted, ["stopwatch_frequency"] = Stopwatch.Frequency
    };

    private static Dictionary<string, object?> RecordValue(IReadOnlyDictionary<string, object?> record) =>
        record as Dictionary<string, object?> ?? record.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

    internal void StartRecording(string prefix)
    {
        var full = Path.GetFullPath(prefix);
        lock (gate)
        {
            if (recordingPrefix is not null)
            {
                if (recordingPrefix == full) return;
                throw new InvalidOperationException("A session already has a performance recording prefix.");
            }
            if (wallStopped) throw new InvalidOperationException("Start performance recording before closing the session.");
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            recordingPrefix = full;
            try
            {
                recording = new FileStream(full + ".dvlog.partial", FileMode.CreateNew, FileAccess.Write, FileShare.Read,
                    65536, FileOptions.SequentialScan);
                var retained = RetainedRecordsLocked();
                recordingOmittedPrefix = rawSequence - retained.Length;
                recording.Write(BinaryEvidence.Magic);
                BinaryEvidence.WriteFrame(recording, Header("buffered-binary-stream", recordingOmittedPrefix));
                foreach (var record in retained) BinaryEvidence.WriteFrame(recording, RecordValue(record));
            }
            catch (Exception error)
            {
                AbandonRecordingLocked(error);
                throw;
            }
        }
    }

    private void AbandonRecordingLocked(Exception error)
    {
        recordingError ??= error.Message;
        try { recording?.Dispose(); }
        catch (Exception closeError) { recordingError += "; close: " + closeError.Message; }
        recording = null;
    }

    private void StreamRecordLocked(IReadOnlyDictionary<string, object?> record)
    {
        if (recording is null) return;
        var started = Stopwatch.GetTimestamp();
        try { BinaryEvidence.WriteFrame(recording, RecordValue(record)); }
        catch (Exception error) { AbandonRecordingLocked(error); }
        finally { decisionInstrumentationTicks += Math.Max(0, Stopwatch.GetTimestamp() - started); }
    }

    private static void WriteSummaryAndSeal(Stream stream, IReadOnlyDictionary<string, object?> snapshot, string policy, long omitted)
    {
        BinaryEvidence.WriteFrame(stream, new Dictionary<string, object?>
        {
            ["type"] = "performance-summary", ["data"] = JsonSerializer.SerializeToElement(snapshot)
        });
        BinaryEvidence.WriteFrame(stream, new Dictionary<string, object?>
        {
            ["type"] = "seal", ["sample_policy"] = policy, ["omitted_prefix_records"] = omitted,
            ["total_records"] = snapshot["raw_record_count"], ["total_samples"] = snapshot["sample_count"],
            ["total_decision_samples"] = snapshot["decision_sample_count"], ["total_interface_events"] = snapshot["interface_event_count"]
        });
    }

    internal void WriteReport(string prefix, bool closed)
    {
        var full = Path.GetFullPath(prefix);
        IReadOnlyDictionary<string, object?> snapshot;
        IReadOnlyDictionary<string, object?>[] retained;
        bool useRecording;
        lock (gate)
        {
            useRecording = recordingPrefix == full;
            if (useRecording && !closed)
                throw new InvalidOperationException("The active recording is sealed at shutdown. Use a new prefix to export a snapshot.");
            snapshot = SnapshotLocked(closed);
            retained = RetainedRecordsLocked();
            var exportState = snapshot.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            exportState["export_raw_policy"] = useRecording ? "buffered-binary-stream" : "bounded-tail-export";
            exportState["export_omitted_records"] = useRecording ? recordingOmittedPrefix : rawSequence - retained.Length;
            snapshot = HostValues.Freeze(exportState);
        }
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
        if (useRecording)
        {
            try
            {
                lock (gate)
                {
                    if (recordingSealed) throw new InvalidOperationException("The performance recording is already sealed.");
                    if (recordingError is not null || recording is null)
                        throw new IOException("Performance stream is incomplete: " + (recordingError ?? "stream unavailable"));
                    WriteSummaryAndSeal(recording, snapshot, "buffered-binary-stream", recordingOmittedPrefix);
                    recording.Flush(true);
                    recording.Dispose(); recording = null;
                    File.Move(full + ".dvlog.partial", full + ".dvlog", overwrite: false);
                    recordingSealed = true;
                }
            }
            catch (Exception error)
            {
                lock (gate) AbandonRecordingLocked(error);
                errors.Add(new IOException("Performance report .dvlog: " + error.Message, error));
            }
        }
        else Write(".dvlog", stream =>
        {
            var omitted = Convert.ToInt64(snapshot["raw_record_count"], CultureInfo.InvariantCulture) - retained.Length;
            stream.Write(BinaryEvidence.Magic);
            BinaryEvidence.WriteFrame(stream, Header("bounded-tail-export", omitted));
            foreach (var record in retained) BinaryEvidence.WriteFrame(stream, RecordValue(record));
            WriteSummaryAndSeal(stream, snapshot, "bounded-tail-export", omitted);
        });
        if (useRecording)
        {
            lock (gate)
            {
                var exportState = snapshot.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
                exportState["raw_recording_error"] = recordingError;
                snapshot = HostValues.Freeze(exportState);
            }
        }
        Write(".hourly.csv", stream => WriteCsv(stream, Rows(snapshot, "hourly"),
            ["epoch", "game_hour", "game_start_minute", "game_end_minute", "covered_game_minutes", "elapsed_wall_ms",
             "game_hours_per_real_hour", "total_decisions", "decisions_per_real_hour", "controller_kind",
             "interface_total_ms", "interface_hours_per_real_hour", "engine_decisions", "human_decisions", "llm_decisions",
             "engine_ms", "speculation_ms", "projection_ms", "startup_ms", "persistence_ms", "development_ms"]));
        Write(".operations.csv", stream => WriteCsv(stream, Rows(snapshot, "operations"),
            ["category", "operation", "trigger_actor", "viewer", "projection_scope", "calls", "failed_calls", "incomplete_phases",
             "inclusive_total_ms", "exclusive_total_ms", "inclusive_mean_ms", "exclusive_mean_ms", "inclusive_p95_upper_ms",
             "inclusive_max_ms", "allocated_bytes", "self_allocated_bytes"]));
        Write(".decisions.csv", stream => WriteCsv(stream, Rows(snapshot, "decisions"),
            ["actor", "controller_kind", "controller_label", "decisions", "decisions_per_real_hour",
             "first_event", "last_event", "first_game_minute", "last_game_minute"]));
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
        null => "—", double number => number.ToString("0.######", CultureInfo.InvariantCulture),
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
        var text = new StringBuilder("# 会话决策与耗时报告\n\n");
        text.AppendLine($"会话：{snapshot["session_id"]}；开始 UTC：{snapshot["wall_started_utc"]}；结束 UTC：{snapshot["wall_stopped_utc"]}。");
        text.AppendLine($"采样现实 {Number(Convert.ToDouble(snapshot["elapsed_wall_ms"], CultureInfo.InvariantCulture) / 1000d)} 秒，实际推进 {Number(snapshot["advanced_game_minutes"])} 游戏分钟；正式选择 {Number(snapshot["total_decisions"])} 次。");
        text.AppendLine($"本次策略配置：{snapshot["planning_mode"]}；模式近似：{snapshot["planning_mode_approximation"]}。approximate-v1 使用当前合法候选即时效用与单次声明推理；缺失模式保留为未记录。");
        text.AppendLine();
        text.AppendLine("| 核心指标 | 本窗口累计比率 | 单位 |");
        text.AppendLine("| --- | ---: | --- |");
        text.AppendLine($"| 推进速度 | {Number(snapshot["game_hours_per_real_hour"])} | 游戏小时 / 现实小时 |");
        text.AppendLine($"| 决策频率 | {Number(snapshot["decisions_per_real_hour"])} | 次 / 现实小时 |");
        text.AppendLine($"| {snapshot["controller_label"]}接口耗时率 | {Number(snapshot["interface_hours_per_real_hour"])} | 累计接口小时 / 现实小时 |");
        text.AppendLine();
        text.AppendLine("分母统一为打开或恢复完成至关闭请求的现实游玩时长。小于一现实小时仍按本窗口累计量相除，属于短窗口观测比率，不代表已运行整小时或整局预测；分母为零时留空。引擎接口累计正式计算及提前准备的排他墙钟耗时，包含未采用的提前计算。人工/大模型接口只累计实际出现的可选请求至接受、取消或关闭的区间；没有该记录时为空。接口并行时累计区间允许重叠，不能当作单线程占用率。");
        text.AppendLine("决策数只来自本次正式状态新增的 action_selected；选择、排队、完成与存档提交边界分别记录。Complete 包含成功及拒绝的真实回执，结果由 outcomes 区分；待执行和未进入存档的尾部选择不算作失败。UI 聚焦、规划节点、旧存档历史、开发操作及未采用分支不重复计数。所有来源的决策频率共用现实分母。");
        text.AppendLine("\n## 每游戏小时折算\n");
        text.AppendLine("以下按本次会话累计实际推进的游戏小时折算，不是完整一游戏小时的独立采样；推进为零时缺失。");
        text.AppendLine($"引擎正式选择 {Number(snapshot["engine_decisions_per_game_hour"])} 次 / 游戏小时；选策计算 {Number(snapshot["engine_decision_compute_ms_per_game_hour"] is double compute ? compute / 1000d : null)} 秒 / 游戏小时；现实经过 {Number(snapshot["elapsed_real_ms_per_game_hour"] is double elapsed ? elapsed / 1000d : null)} 秒 / 游戏小时。");
        text.AppendLine($"选策累计 {Number(snapshot["engine_decision_compute_total_ms"])} ms；有效闭合区间合计 {Number(snapshot["engine_decision_compute_recorded_ms"])} ms；未完整区间 {Number(snapshot["engine_decision_compute_incomplete_calls"])}；计量完整 {Number(snapshot["per_game_hour_measurement_complete"])}。");
        text.AppendLine("选策计算只计 dvs_choose 与规划的实际墙钟，含未采用提前计算，扣除性能流编码/写入与外部阶段观察回调；不含其余认知、Complete、截图/投影、等待或归档，不等于纯 CPU 时间。未闭合或失败区间不补造为完整测量，选策总耗时与其折算值保持缺失。");
        text.AppendLine("\n## 控制来源\n");
        text.AppendLine("| 来源 | 正式选择 | 次/现实小时 | 累计接口 ms | 接口小时/现实小时 |");
        text.AppendLine("| --- | ---: | ---: | ---: | ---: |");
        foreach (var row in Rows(snapshot, "decision_sources"))
            text.AppendLine($"| {row["controller_label"]} | {Number(row["decisions"])} | {Number(row["decisions_per_real_hour"])} | {Number(row["interface_total_ms"])} | {Number(row["interface_hours_per_real_hour"])} |");
        var phases = Rows(snapshot, "interface_phases").ToArray();
        if (phases.Length > 0)
        {
            text.AppendLine("\n### 外部接口区间\n");
            text.AppendLine("| 来源 | 主体 | 已观测阶段 | 耗时 ms | 区间数 | 接受 | 中断 |");
            text.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: |");
            foreach (var row in phases)
                text.AppendLine($"| {row["controller_kind"]} | {row["actor"]} | {row["phase"]} | {Number(row["elapsed_ms"])} | {row["segments"]} | {row["accepted"]} | {row["interrupted"]} |");
            text.AppendLine("阶段仅按宿主实际报告的 request/response 等边界记录；未提供的网络、排队或推理内部阶段不估算。等待可能覆盖同时进行的引擎工作，不能与引擎排他耗时直接相加求整段现实时间。");
        }
        text.AppendLine("\n## 耗时拆解\n");
        text.AppendLine("| 分项 | 排他累计 ms | 当前线程累计分配字节 |");
        text.AppendLine("| --- | ---: | ---: |");
        foreach (var row in Rows(snapshot, "categories"))
            text.AppendLine($"| {row["label"]} | {Number(row["exclusive_total_ms"])} | {Number(row["self_allocated_bytes"])} |");
        text.AppendLine("初始化与关闭归档在现实游玩窗口外单列。投影包含视图生成，不等同于终端打印；墙钟包含调度与 GC，不是线程 CPU 时间。分配字节是累计分配，不是峰值内存或泄漏量。");
        text.AppendLine("\n### 操作与阶段\n");
        text.AppendLine("| 操作 | 触发主体 | 观察者/范围 | 次数 | 含子项累计 ms | 排他累计 ms | 含子项均值 ms | 排他均值 ms | 含子项 P95上界 ms |");
        text.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var row in Rows(snapshot, "operations"))
            text.AppendLine($"| {row["category_label"]}/{row["label"]} | {row["trigger_actor"]} | {row["viewer"]}/{row["projection_scope"]} | {row["calls"]} | {Number(row["inclusive_total_ms"])} | {Number(row["exclusive_total_ms"])} | {Number(row["inclusive_mean_ms"])} | {Number(row["exclusive_mean_ms"])} | {Number(row["inclusive_p95_upper_ms"])} |");
        text.AppendLine("排他项可以沿嵌套调用相加；含子项累计和均值不可重复相加。P95为固定直方图上界并由观测最大值收紧。触发主体表示发起该操作的会话边界；观察者及范围另外标明，不能将全体地图开销解释为该主体个人开销。");
        var planningRows = Rows(snapshot, "planning").ToArray();
        if (planningRows.Length > 0)
        {
            text.AppendLine("\n### 选策实际工作\n");
            text.AppendLine("| 主体 | 有工作记录的选择 | 缓存命中 | 候选检查 | 准入检查 | 资格求值 | 预测 | 预测复用 | 展开节点 | 保留路径 | 路径复用 | 跳过展开 | 峰值前沿 |");
            text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
            foreach (var row in planningRows)
                text.AppendLine($"| {row["actor"]} | {row["decisions"]} | {row["cache_hits"]} | {Number(row["candidates_considered"])} | {Number(row["admission_checks"])} | {Number(row["eligible_evaluations"])} | {Number(row["forecasts"])} | {Number(row["forecast_reuses"])} | {Number(row["expanded_nodes"])} | {Number(row["retained_paths"])} | {Number(row["route_reuses"])} | {Number(row["skipped_expansion"])} | {Number(row["peak_frontier"])} |");
            text.AppendLine("\n| 主体 | 已枚举但跳过的候选 | 排序声明检查 | 排序缓存复用 | 部分遍历次数 | 进入求值的层 | 完整求值的层 |");
            text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
            foreach (var row in planningRows)
                text.AppendLine($"| {row["actor"]} | {Number(row["candidates_skipped"])} | {Number(row["candidate_order_scans"])} | {Number(row["candidate_order_reuses"])} | {Number(row["partial_layer"])} | {Number(row["examined_layers"])} | {Number(row["completed_layers"])} |");
            text.AppendLine("实际工作来自正式 action_selected 的 planning_work。当前近似策略记录候选扫描和准入求值，不执行行动树展开。历史记录中的搜索、缓存及路径字段仅按原始事件展示，缺失计数保持缺失，不把不同策略的记录合为同一基准。");
            text.AppendLine();
            foreach (var row in planningRows)
                if (row["stop_reasons"] is IReadOnlyDictionary<string, object?> reasons)
                    text.AppendLine($"- {row["actor"]} 停止原因：{string.Join("；", reasons.Select(reason => reason.Key + " × " + Number(reason.Value)))}；事件 {row["first_event"]} 至 {row["last_event"]}。");
        }
        text.AppendLine("\n## 主体决策\n");
        text.AppendLine("| 主体 | 来源 | 正式选择 | 次/现实小时 | 首次事件 | 最后事件 |");
        text.AppendLine("| --- | --- | ---: | ---: | --- | --- |");
        foreach (var row in Rows(snapshot, "decisions"))
            text.AppendLine($"| {row["actor"]} | {row["controller_label"]} | {row["decisions"]} | {Number(row["decisions_per_real_hour"])} | {row["first_event"]} | {row["last_event"]} |");
        text.AppendLine("\n## 按游戏时钟归属的片段\n");
        text.AppendLine("| 时间段 | 游戏分钟范围 | 覆盖游戏分钟 | 现实秒 | 游戏小时/现实小时 | 次/现实小时 | 接口小时/现实小时 |");
        text.AppendLine("| ---: | --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (var row in Rows(snapshot, "hourly"))
            text.AppendLine($"| {row["epoch"]} | {Number(row["game_start_minute"])}–{Number(row["game_end_minute"])} | {Number(row["covered_game_minutes"])} | {Number(Convert.ToDouble(row["elapsed_wall_ms"], CultureInfo.InvariantCulture) / 1000d)} | {Number(row["game_hours_per_real_hour"])} | {Number(row["decisions_per_real_hour"])} | {Number(row["interface_hours_per_real_hour"])} |");
        text.AppendLine("计算归操作开始的游戏小时，决策归原事件时刻，现实等待归当前已提交时刻；推进覆盖跨小时拆分。该表用于定位发生时段，不假定跨小时计算均匀分摊。累计比率以累计分子和现实分母计算，不平均逐行比率。开发调时不计正式推进，向后调时另开 epoch。");
        text.AppendLine("\n## 运行来源与提交边界\n");
        text.AppendLine($"控制：{snapshot["controller_label"]}；审计：{snapshot["audit_mode"]}；开发：{snapshot["development_mode"]}；主体数：{snapshot["actor_count"]}；运行时：{snapshot["runtime"]}；程序集：{snapshot["assembly_id"]}。");
        foreach (var section in new[] { "provenance", "lifecycle" })
            if (snapshot[section] is IReadOnlyDictionary<string, object?> fields && fields.Count > 0)
            {
                text.AppendLine();
                foreach (var field in fields) text.AppendLine($"- {field.Key}：{Number(field.Value)}");
            }
        text.AppendLine($"\n汇总截止单调时钟 {snapshot["measured_until_ticks"]} ticks；现实窗口截止 {snapshot["wall_measured_until_ticks"]} ticks；频率 {snapshot["stopwatch_frequency"]} ticks/s。投影帧应只携带同一次摘要，截止点与世界状态版本分列。");
        text.AppendLine($"原始记录策略：{snapshot["raw_recording_policy"]}；累计 {snapshot["raw_record_count"]} 条；记录开启前未保留 {snapshot["raw_omitted_prefix_records"]} 条。内存各保留最近 {snapshot["sample_capacity"]} 条，计算尾部以外 {snapshot["tail_omitted_samples"]} 条、决策尾部以外 {snapshot["tail_omitted_decisions"]} 条；流式记录不随尾部淘汰。报告生成与自身写盘不计入快照。");
        text.AppendLine($"本次导出的原始文件策略：{snapshot["export_raw_policy"]}；未包含原始前缀 {snapshot["export_omitted_records"]} 条。封存是否完整以该文件的 seal 和实际写入结果为准。");
        if (snapshot["raw_recording_error"] is not null) text.AppendLine($"原始流写入失败：{snapshot["raw_recording_error"]}；保留 partial，不能视为完整封存。");
        text.AppendLine($"在途计算 {snapshot["active_scopes"]}，在途外部接口 {snapshot["active_decision_interfaces"]}；异常 {snapshot["exceptions"]}，未配对结束 {snapshot["unmatched_phase_ends"]}，中断计算阶段 {snapshot["interrupted_phases"]}。这些状态不等同于剧情达成或行动成功。");
        return text.ToString();
    }
}

