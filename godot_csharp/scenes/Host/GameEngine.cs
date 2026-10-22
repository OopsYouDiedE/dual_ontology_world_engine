using Godot;
using System.Text.Json;
using System.Diagnostics;
using System.Collections.Concurrent;
using GArray = Godot.Collections.Array;
using GDict = Godot.Collections.Dictionary;

// Platform boundary: all state transitions and decisions are compiled from DV.
public partial class GameEngine : Node
{
    [Signal] public delegate void EventsReadyEventHandler(GArray events);
    [Signal] public delegate void TraceClosedEventHandler();
    private Task work = Task.CompletedTask;
    private bool closing;
    private bool busy;
    private bool maintenance;
    private bool faulted;
    private DvSession? session;
    private string? evidenceDirectory;
    private string optionsJson = "{}";
    private GDict snapshotCache = new();
    private string snapshotJson { get => Json.Stringify(snapshotCache); set => snapshotCache = Parse(value); }
    private readonly Dictionary<string, GDict> projectionCache = new(StringComparer.Ordinal);
    private bool timingEnabled;
    private const int TimingCapacity = 4096;
    private int timingCount, liveTimingCount;
    private long timingDropped, liveTimingDropped;
    private SmartReplay? replay;
    public void SetDiagnosticsEnabled(bool value) => timingEnabled = value;
    public bool IsDeveloperMode() => session?.DevMode == true;
    public bool CanInspect() => session?.DevMode == true;
    public bool HasReplaySample() => false;
    public bool IsDispatchIdle() => !busy && !maintenance && (faulted || commands.Count == 0);
    public void RecordHostFailure(string message)
    {
        faulted = true;
        hostErrors.Add(new { phase = "physical-settlement", error = message });
        Report(new InvalidOperationException(message));
    }
    public bool CanBackgroundTick() => session is not null && !busy && !maintenance && !closing && !faulted
        && commands.Count == 0 && pendingEvent.Length == 0 && snapshotCache.ContainsKey("pending")
        && snapshotCache["pending"].AsGodotDictionary().GetValueOrDefault("status", "").AsString() == "awaiting_input";
    public void DevCommand(string command) => Dispatch(new GDict { ["kind"] = "dev_command", ["command"] = command });
    public void LoadReplay(string path) => Dispatch(new GDict { ["kind"] = "replay_load", ["path"] = path });
    public void ReplayPlay(double speed = 1) { replay?.Play(speed); }
    public void ReplayPause() { replay?.Pause(); }
    public GDict ReplayStep() => ReplayValue(replay?.Step());
    public GArray ReplayAdvance(double seconds)
    {
        GArray frames = new();
        if (replay is not null) foreach (var frame in replay.Advance(seconds)) frames.Add(ReplayValue(frame));
        return frames;
    }
    private static GDict ReplayValue(ReplayFrame? frame) => frame is null ? new() : new GDict {
        ["sequence"] = frame.Sequence, ["kind"] = frame.Kind, ["actor"] = frame.Actor,
        ["action"] = frame.Action, ["text"] = frame.Text, ["duration"] = frame.DurationSeconds,
        ["data"] = GodotHostValues.Dictionary(frame.Data) };
    private GDict Project(DvSession current, string viewer, bool full = false)
    {
        var delta = current.Project(viewer, full);
        GDict snapshot = !delta.Reset && projectionCache.TryGetValue(viewer, out var previous)
            ? previous.Duplicate(false) : new();
        foreach (var key in delta.Removed) snapshot.Remove(key);
        foreach (var pair in delta.Changed) snapshot[pair.Key] = GodotHostValues.Value(pair.Value);
        snapshot["state_version"] = delta.Version;
        projectionCache[viewer] = snapshot;
        return snapshot;
    }
    private string pendingEvent = "";
    private readonly Queue<(long Id, string Json)> commands = new();
    private readonly HashSet<string> completedEvents = new(StringComparer.Ordinal);
    private readonly List<string> visible = [];
    private readonly List<string> inputs = [];
    private readonly List<object> hostErrors = [];
    private readonly ConcurrentQueue<string> timings = new();
    private readonly ConcurrentQueue<string> liveTimings = new();
    private long nextCommand, activeCommand;
    private readonly long timingOrigin = Stopwatch.GetTimestamp();
    private string interaction = "";

    public long TimingClockUs() => (Stopwatch.GetTimestamp() - timingOrigin) * 1000000L / Stopwatch.Frequency;
    private void Stamp(string stage, string id, string details = "", long? command = null)
    {
        if (!timingEnabled) return;
        var row = JsonSerializer.Serialize(new { real_us = TimingClockUs(), stage,
            interaction_id = id, command_id = command ?? activeCommand,
            thread_id = System.Environment.CurrentManagedThreadId, details });
        timings.Enqueue(row);
        liveTimings.Enqueue(row);
        if (Interlocked.Increment(ref timingCount) > TimingCapacity && timings.TryDequeue(out _)) { Interlocked.Decrement(ref timingCount); Interlocked.Increment(ref timingDropped); }
        if (Interlocked.Increment(ref liveTimingCount) > TimingCapacity && liveTimings.TryDequeue(out _)) { Interlocked.Decrement(ref liveTimingCount); Interlocked.Increment(ref liveTimingDropped); }
    }
    public GArray PollTimings()
    {
        GArray result = new();
        while (result.Count < 256 && liveTimings.TryDequeue(out var row)) { Interlocked.Decrement(ref liveTimingCount); result.Add(row); }
        return result;
    }

    public void RecordTiming(GDict value)
    {
        if (timingEnabled) Stamp(value.GetValueOrDefault("stage", "").AsString(),
            value.GetValueOrDefault("interaction_id", interaction).AsString(), Json.Stringify(value));
    }

    public void ConfigureSession(GDict options)
    {
        if (session is not null || busy || maintenance || commands.Count != 0 || closing || faulted)
            throw new InvalidOperationException("Session options can only be configured before opening a new session");
        optionsJson = Json.Stringify(options);
    }

    // Compatibility entry: control is an opening option, never a second decision path.
    public void SetPlayerControlled(bool value)
    {
        var options = Parse(optionsJson);
        options["controller"] = value ? "player" : "ai";
        ConfigureSession(options);
    }

    public GDict Snapshot() => snapshotCache.Duplicate(false);
    public GDict PerformanceSummary() => session is { } current
        ? GodotHostValues.Dictionary(current.PerformanceSummary()) : new();
    public GDict PerformanceSnapshot() => session is { } current
        ? GodotHostValues.Dictionary(current.PerformanceSnapshot()) : new();

    // Read the same completed host projection; never traverse a worker-owned DV state.
    public string ReadMilestoneProgressJson() => snapshotJson;
    private static GDict Parse(string json) => Json.ParseString(json).AsGodotDictionary();

    private void BindPhases(DvSession current, string timingId, long command)
    {
        // Bind each command, not just opening: later selections have their own
        // interaction ID, and restored sessions need the same phase observer.
        current.PhaseObserver = (stage, details) => Stamp("dv_" + stage, timingId, details, command);
    }

    private static string NewSessionEvidenceDirectory()
    {
        var name = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
        var path = ProjectSettings.GlobalizePath("user://dv-sessions/" + name);
        if (Directory.Exists(path)) throw new IOException("Evidence directory already exists");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void StartSessionRecording(DvSession current, string directory)
    {
        current.SetExecutionContext("godot", "godot");
        current.StartPerformanceRecording(Path.Combine(directory, "engine-performance"));
    }

    // A replaced or rejected candidate owns its recorder until this one close.
    // Errors are returned to the transaction instead of retrying a partial seal.
    private static string SealSessionEvidence(DvSession current, string directory, string reason, string backupPath = "")
    {
        var errors = new List<string>();
        try { current.StopPerformanceClock(); }
        catch (Exception error) { errors.Add("stop-clock: " + error); }
        try { current.Close(); }
        catch (Exception error) { errors.Add("close: " + error); }
        if (current.AuditMode)
        {
            try { current.WriteTrace(Path.Combine(directory, "session.dvdag")); }
            catch (Exception error) { errors.Add("trace: " + error); }
        }
        try { current.WritePerformanceReport(Path.Combine(directory, "engine-performance")); }
        catch (Exception error) { errors.Add("performance-report: " + error); }
        try
        {
            using var output = new FileStream(Path.Combine(directory, "close-result.dvlog"), FileMode.CreateNew, System.IO.FileAccess.Write);
            output.Write(DuelView.Compiled.BinaryEvidence.Magic);
            DuelView.Compiled.BinaryEvidence.WriteFrame(output, JsonSerializer.SerializeToElement(new
            {
                complete = errors.Count == 0, reason, backup_path = backupPath,
                audit_mode = current.AuditMode, trace_policy = current.AuditMode ? "full" : "omitted-gameplay",
                performance_policy = "streamed-session-samples-and-aggregates", performance_prefix = "engine-performance", errors
            }));
        }
        catch (Exception error) { errors.Add("close-result: " + error); }
        return string.Join("\n", errors);
    }

    private void BeginDeliveredDecision()
    {
        var current = session;
        if (current is null || closing || faulted || !snapshotCache.ContainsKey("pending")) return;
        var pending = snapshotCache["pending"].AsGodotDictionary();
        if (pending.GetValueOrDefault("status", "").AsString() != "awaiting_input") return;
        var options = pending.GetValueOrDefault("options", new GArray());
        if (options.VariantType != Variant.Type.Array || options.AsGodotArray().Count == 0) return;
        current.BeginDecisionInterface(current.Viewer, "decision");
    }


    public GArray Dispatch(GDict input)
    {
        var json = Json.Stringify(input);
        var kind = input.GetValueOrDefault("kind", "").AsString();
        if (kind is not ("npc_tick" or "prepare_ahead" or "cancel_ahead")) inputs.Add(json);
        var command = ++nextCommand;
        Stamp("command_received", input.GetValueOrDefault("interaction_id", interaction).AsString(), input.GetValueOrDefault("kind", "").AsString(), command);
        if (closing) return new();
        if (faulted)
        {
            Report(new InvalidOperationException("Session dispatch stopped after a host worker error"));
            return new();
        }
        if (kind == "select" && session is { } current)
            current.SetDecisionInterfacePhase(current.Viewer, "response");
        commands.Enqueue((command, json));
        StartPump();
        return new();
    }

    private void StartPump()
    {
        if (busy || maintenance || closing || faulted || commands.Count == 0) return;
        busy = true;
        work = DrainCommands();
    }

    private async Task DrainCommands()
    {
        try
        {
            while (commands.Count != 0 && !closing && !maintenance && !faulted)
            {
                var queued = commands.Dequeue();
                activeCommand = queued.Id;
                var input = Parse(queued.Json);
                var kind = input.GetValueOrDefault("kind", "").AsString();
                if (input.ContainsKey("interaction_id")) interaction = input["interaction_id"].AsString();
                var timingId = interaction;
                Stamp("command_started", timingId, kind);
                GArray events = new();
                try
                {
                    if (kind == "start" || kind == "load")
                    {
                        if (session is not null) throw new InvalidOperationException("Session is already open");
                        var options = optionsJson;
                        var savePath = kind == "load" ? NormalizeBookmarkPath(input.GetValueOrDefault("path", "").AsString()) : "";
                        var recordingDirectory = NewSessionEvidenceDirectory();
                        var opened = await Task.Run(() =>
                        {
                            using var save = kind == "load" ? OpenBookmarkForRead(savePath) : null;
                            Stamp("open_begin", timingId);
                            var current = kind == "load" ? DvSession.RestoreFile(savePath) : DvSession.OpenConfiguredCampaign(options);
                            try
                            {
                            StartSessionRecording(current, recordingDirectory);
                            BindPhases(current, timingId, queued.Id);
                            Stamp("open_end", timingId);
                            Stamp("snapshot_begin", timingId);
                            var initialSnapshot = Project(current, current.Viewer, true);
                            Stamp("snapshot_end", timingId);
                            if (kind == "load") RequireRestorableSnapshot(Json.Stringify(initialSnapshot));
                            var journal = kind == "load" ? ReadBoundCasebook(savePath, current.Viewer, save!) : (Json: "{}", Warning: "");
                            return (Session: current, Snapshot: initialSnapshot, Viewer: current.Viewer,
                                Casebook: journal.Json, Warning: journal.Warning,
                                ProjectedSnapshot: initialSnapshot,
                                ProjectedCasebook: Parse(journal.Json));
                            }
                            catch (Exception error)
                            {
                                var sealError = SealSessionEvidence(current, recordingDirectory, "open-not-installed");
                                if (sealError.Length > 0) throw new AggregateException(error, new IOException(sealError));
                                throw;
                            }
                        });
                        Stamp("handoff_begin", timingId, kind);
                        var snapshot = opened.ProjectedSnapshot;
                        var journalData = opened.ProjectedCasebook;
                        events = new GArray { new GDict { ["kind"] = "view",
                            ["snapshot"] = snapshot,
                            ["casebook"] = journalData, ["casebook_warning"] = opened.Warning } };
                        session = opened.Session;
                        evidenceDirectory = recordingDirectory;
                        snapshotCache = opened.Snapshot;
                        casebookViewer = opened.Viewer;
                        casebookJson = opened.Casebook;
                        Stamp("handoff_end", timingId, kind);
                    }
                    else if (kind == "view_ready" || kind == "dv_next" || kind == "select" || kind == "npc_tick")
                    {
                        if (session is null) throw new InvalidOperationException("Session is not open");
                        // An acknowledgement cannot redispatch an already executing intention.
                        if (pendingEvent.Length != 0) continue;
                        var selection = kind == "select" ? input["choice_id"].AsString() : "";
                        var current = session;
                        if (kind == "select") current.SetDecisionInterfacePhase(current.Viewer, "response");
                        var prepared = await Task.Run(() =>
                        {
                            BindPhases(current, timingId, queued.Id);
                            Stamp("prepare_begin", timingId, selection);
                            var token = input.GetValueOrDefault("preview_token", "").AsString();
                            IReadOnlyDictionary<string, object?> intent;
                            var beforeVersion = current.Version;
                            if (kind == "npc_tick") intent = current.PrepareBackgroundValue();
                            else if (token.Length == 0 || !current.TryAdoptPrepared(token, out intent)) intent = current.PrepareValue(selection);
                            Stamp("prepare_end", timingId);
                            var projectedIntent = GodotHostValues.Dictionary(intent);
                            var ready = projectedIntent.GetValueOrDefault("status", "").AsString() == "prepared";
                            var actor = ready ? projectedIntent["actor"].AsString() : "";
                            Stamp("snapshot_begin", timingId);
                            var projectedViewer = Project(current, current.Viewer);
                            var projectedExecution = ready
                                ? (actor == current.Viewer ? projectedViewer : GodotHostValues.Dictionary(current.ExecutorProjection(actor)))
                                : new GDict();
                            Stamp("snapshot_end", timingId);
                            GArray delivered = new();
                            if (kind != "npc_tick" || current.Version != beforeVersion)
                            {
                                delivered.Add(new GDict { ["kind"] = "state", ["snapshot"] = projectedViewer });
                                delivered.Add(new GDict { ["kind"] = "dv_action", ["intent"] = projectedIntent,
                                    ["execution_snapshot"] = projectedExecution });
                            }
                            return (Snapshot: projectedViewer, Events: delivered,
                                Pending: ready ? projectedIntent["action_event"].AsString() : "");
                        });
                        Stamp("handoff_begin", timingId, kind);
                        snapshotCache = prepared.Snapshot;
                        pendingEvent = prepared.Pending;
                        events = prepared.Events;
                        Stamp("handoff_end", timingId, kind);
                    }
                    else if (kind == "prepare_ahead")
                    {
                        var current = session;
                        if (current is null || pendingEvent.Length != 0) continue;
                        var choice = input["choice_id"].AsString();
                        var preview = await Task.Run(() => current.PrepareAhead(choice));
                        events.Add(new GDict { ["kind"] = "prepared_ahead", ["choice_id"] = choice,
                            ["token"] = preview.Token, ["state_version"] = preview.BaseVersion });
                    }
                    else if (kind == "cancel_ahead")
                    {
                        var current = session;
                        if (current is not null) await Task.Run(current.CancelPrepared);
                    }
                    else if (kind == "dev_command")
                    {
                        var current = session ?? throw new InvalidOperationException("Start a session before using the console.");
                        var parts = input["command"].AsString().Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 0) continue;
                        var argument = parts.Length == 2 ? parts[1] : "";
                        if (parts[0] == "inspect")
                        {
                            var inspected = await Task.Run(() => GodotHostValues.Dictionary(current.Inspect(argument.Length == 0 ? current.Viewer : argument)));
                            events.Add(new GDict { ["kind"] = "dev_inspect", ["data"] = inspected });
                        }
                        else
                        {
                            RequireNoPendingExecution();
                            var changed = await Task.Run(() => { current.ExecuteDebug(parts[0], argument); return Project(current, current.Viewer, true); });
                            snapshotCache = changed;
                            var result = changed.GetValueOrDefault("development_result", new GDict()).AsGodotDictionary();
                            if (result.GetValueOrDefault("resync", false).AsBool())
                            {
                                var rebuilding = changed.Duplicate(false);
                                rebuilding["development_resync"] = true;
                                events.Add(new GDict { ["kind"] = "dev_state", ["snapshot"] = rebuilding });
                            }
                            events.Add(new GDict { ["kind"] = "dev_error", ["message"] = result.GetValueOrDefault("message", "开发命令已完成。") });
                        }
                    }
                    else if (kind == "replay_load")
                    {
                        var path = ProjectSettings.GlobalizePath(input["path"].AsString());
                        replay = await Task.Run(() => SmartReplay.Load(path));
                        events.Add(new GDict { ["kind"] = "replay_loaded", ["count"] = replay.Count });
                    }
                    else if (kind == "dv_complete")
                    {
                        var receipt = input["receipt"].AsGodotDictionary();
                        var actionEvent = receipt.GetValueOrDefault("action_event", "").AsString();
                        if (completedEvents.Contains(actionEvent)) continue;
                        if (session is null || pendingEvent.Length == 0 || actionEvent != pendingEvent)
                        {
                            Report(new InvalidOperationException("Completion belongs to no pending host action"));
                            continue;
                        }
                        var json = Json.Stringify(receipt);
                        var current = session;
                        var completed = await Task.Run(() =>
                        {
                            BindPhases(current, timingId, queued.Id);
                            Stamp("complete_begin", timingId, actionEvent);
                            var presentation = current.CompleteValue(json, current.Viewer);
                            Stamp("complete_end", timingId, actionEvent);
                            Stamp("snapshot_begin", timingId);
                            var projectedViewer = Project(current, current.Viewer);
                            Stamp("snapshot_end", timingId);
                            var projectedPresentation = GodotHostValues.Dictionary(presentation);
                            GArray delivered = new() {
                                new GDict { ["kind"] = "dv_commit", ["action_event"] = actionEvent,
                                    ["outcome"] = projectedPresentation.GetValueOrDefault("outcome", projectedPresentation.GetValueOrDefault("status", "rejected")),
                                    ["actor"] = projectedPresentation.GetValueOrDefault("completed_actor", projectedPresentation.GetValueOrDefault("actor", "")) },
                                new GDict { ["kind"] = "state", ["snapshot"] = projectedViewer },
                                new GDict { ["kind"] = "dv_present", ["presentation"] = projectedPresentation } };
                            return (Snapshot: projectedViewer, Events: delivered);
                        });
                        Stamp("handoff_begin", timingId, kind);
                        completedEvents.Add(actionEvent);
                        pendingEvent = "";
                        snapshotCache = completed.Snapshot;
                        events = completed.Events;
                        Stamp("handoff_end", timingId, kind);
                    }
                }
                catch (Exception error)
                {
                    if (kind == "select" && session is { } failedSelection)
                        failedSelection.EndDecisionInterface(failedSelection.Viewer, "failed");
                    if (kind is "dev_command" or "replay_load" or "prepare_ahead")
                    {
                        events.Add(new GDict { ["kind"] = "dev_error", ["message"] = error.Message });
                    }
                    else if (kind == "load" && session is null)
                    {
                        // File validation did not install or mutate a session. Keep the book usable.
                        hostErrors.Add(new { phase = kind, error = error.ToString() });
                        events.Add(new GDict { ["kind"] = "load_error", ["message"] = error.Message });
                    }
                    else
                    {
                        // A failed projection can follow a successful state transition. Never retry it blindly.
                        faulted = true;
                        hostErrors.Add(new { phase = kind, error = error.ToString() });
                        Report(error);
                    }
                }
                // Reentrant signal handlers append to this pump; no input is dropped while it is busy.
                if (!closing && events.Count != 0)
                {
                    Stamp("events_delivered", timingId, kind);
                    Stamp("delivery_begin", timingId, kind);
                    EmitSignal(SignalName.EventsReady, events);
                    BeginDeliveredDecision();
                    Stamp("delivery_end", timingId, kind);
                }
                Stamp("command_finished", timingId, kind);
            }
        }
        catch (Exception error)
        {
            faulted = true;
            hostErrors.Add(new { phase = "host-pump", error = error.ToString() });
            GD.PushError(error.ToString());
        }
        finally { busy = false; }
    }

    private void Report(Exception error)
    {
        GD.PushError(error.ToString());
        if (!closing) EmitSignal(SignalName.EventsReady, new GArray { new GDict { ["kind"] = "error", ["message"] = error.Message } });
    }

    private void RequireNoPendingExecution()
    {
        if (pendingEvent.Length != 0)
            throw new InvalidOperationException("Session persistence is unavailable while a Godot action awaits its execution receipt");
    }

    private void InstallRestored((DvSession Session, string Snapshot) restored, GDict preparedSnapshot, string directory)
    {
        session = restored.Session;
        evidenceDirectory = directory;
        projectionCache.Clear();
        snapshotCache = preparedSnapshot;
        casebookViewer = restored.Session.Viewer;
        casebookJson = "{}";
        pendingEvent = "";
        completedEvents.Clear();
        // Commands accepted for the previous state cannot be replayed into a restored state.
        commands.Clear();
        faulted = false;
    }

    public void RecordVisible(GDict value) => visible.Add(Json.Stringify(value));
    public void RecordInput(GDict value) => inputs.Add(Json.Stringify(value));
    public async void CloseTrace()
    {
        if (closing) return;
        closing = true;
        session?.StopPerformanceClock();
        string? path = null;
        var ownsDirectory = false;
        var phase = "await-work";
        var errors = new List<object>();
        try
        {
            try { await work; }
            catch (Exception error)
            {
                GD.PushError(error.ToString());
                errors.Add(new { phase, error = error.ToString() });
            }
            errors.AddRange(hostErrors);
            // Opening may have completed while shutdown awaited the worker.
            session?.StopPerformanceClock();
            phase = "evidence-directory";
            path = evidenceDirectory ??= NewSessionEvidenceDirectory();
            ownsDirectory = true;
            // A failed output must not suppress independent evidence or the save.
            foreach (var output in new (string, IEnumerable<string>)[] {
                ("inputs", inputs.ToArray()), ("visible", visible.ToArray()), ("interaction-timing", timings.ToArray()) })
            {
                phase = output.Item1;
                try
                {
                    var recordPath = Path.Combine(path, phase + ".dvlog");
                    await Task.Run(() =>
                    {
                        DuelView.Compiled.BinaryEvidence.WriteJsonRecords(recordPath + ".partial", output.Item2);
                        File.Move(recordPath + ".partial", recordPath, overwrite: false);
                    });
                }
                catch (Exception error)
                {
                    GD.PushError(error.ToString());
                    errors.Add(new { phase, error = error.ToString() });
                }
            }
            var current = session;
            if (current is null) return;
            phase = "close";
            await Task.Run(current.Close);
            phase = "trace";
            var tracePath = Path.Combine(path, "session.dvdag");
            try
            {
                if (current.AuditMode)
                {
                    await Task.Run(() => current.WriteTrace(tracePath));
                }
            }
            catch (Exception error)
            {
                GD.PushError(error.ToString());
                errors.Add(new { phase, error = error.ToString() });
            }
            phase = "save";
            var savePath = Path.Combine(path, "save.dvsave");
            var saved = false;
            try
            {
                await Task.Run(() => current.WriteSave(savePath));
                saved = true;
            }
            catch (Exception error)
            {
                GD.PushError(error.ToString());
                errors.Add(new { phase, error = error.ToString() });
            }
            if (saved)
            {
                phase = "casebook";
                // Capture the latest main-thread UI projection after automatic
                // world saving succeeds. Its owner and exact save bytes are bound.
                var journal = casebookJson;
                var viewer = current.Viewer;
                try { await Task.Run(() => WriteBoundCasebook(savePath, viewer, journal)); }
                catch (Exception error)
                {
                    GD.PushError(error.ToString());
                    errors.Add(new { phase, error = error.ToString() });
                }
            }
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            errors.Add(new { phase, error = error.ToString() });
        }
        finally
        {
            if (ownsDirectory && path is not null && session is { } measured)
            {
                try { await Task.Run(() => measured.WritePerformanceReport(Path.Combine(path, "engine-performance"))); }
                catch (Exception error)
                {
                    GD.PushError(error.ToString());
                    errors.Add(new { phase = "performance-report", error = error.ToString() });
                }
            }
            try
            {
                if (ownsDirectory && path is not null)
                {
                    using var result = new FileStream(Path.Combine(path, "close-result.dvlog"), FileMode.CreateNew, System.IO.FileAccess.Write);
                    result.Write(DuelView.Compiled.BinaryEvidence.Magic);
                    var closeRecord = System.Text.Json.JsonSerializer.SerializeToElement(new { complete = errors.Count == 0, audit_mode = session?.AuditMode, trace_policy = session?.AuditMode == true ? "full" : "omitted-gameplay", session_opened = session is not null, unprocessed_commands = commands.Count, pending_action = pendingEvent, performance_policy = "streamed-session-samples-and-aggregates", performance_prefix = "engine-performance", timing_policy = "bounded-ring", timing_capacity = TimingCapacity, timing_dropped = timingDropped, live_timing_dropped = liveTimingDropped, errors });
                    DuelView.Compiled.BinaryEvidence.WriteFrame(result, closeRecord);
                }
            }
            catch (Exception error) { GD.PushError(error.ToString()); }
            EmitSignal(SignalName.TraceClosed);
        }
    }
}
