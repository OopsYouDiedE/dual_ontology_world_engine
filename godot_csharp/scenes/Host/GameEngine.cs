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
    private string optionsJson = "{}";
    private string snapshotJson = "{}";
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
        var row = JsonSerializer.Serialize(new { real_us = TimingClockUs(), stage,
            interaction_id = id, command_id = command ?? activeCommand,
            thread_id = System.Environment.CurrentManagedThreadId, details });
        timings.Enqueue(row);
        liveTimings.Enqueue(row);
    }
    public GArray PollTimings()
    {
        GArray result = new();
        while (liveTimings.TryDequeue(out var row)) result.Add(row);
        return result;
    }

    public void RecordTiming(GDict value) => Stamp(value.GetValueOrDefault("stage", "").AsString(),
        value.GetValueOrDefault("interaction_id", interaction).AsString(), Json.Stringify(value));

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

    public GDict Snapshot() => Parse(snapshotJson);

    // Read the same completed host projection; never traverse a worker-owned DV state.
    public string ReadMilestoneProgressJson() => snapshotJson;
    private static GDict Parse(string json) => Json.ParseString(json).AsGodotDictionary();

    private void BindPhases(DvSession current, string timingId, long command)
    {
        // Bind each command, not just opening: later selections have their own
        // interaction ID, and restored sessions need the same phase observer.
        current.PhaseObserver = (stage, details) => Stamp("dv_" + stage, timingId, details, command);
    }

    private GDict Materialize(string json, string timingId, string projection)
    {
        // JSON parsing constructs only an exclusively owned data graph. No
        // scene nodes or shared resources enter the worker. Publish it after
        // await completes, then leave all graph mutation to the main thread.
        Stamp("materialize_begin", timingId, projection);
        var value = Parse(json);
        Stamp("materialize_end", timingId, projection);
        return value;
    }

    public GArray Dispatch(GDict input)
    {
        var json = Json.Stringify(input);
        inputs.Add(json);
        var command = ++nextCommand;
        Stamp("command_received", input.GetValueOrDefault("interaction_id", interaction).AsString(), input.GetValueOrDefault("kind", "").AsString(), command);
        if (closing) return new();
        if (faulted)
        {
            Report(new InvalidOperationException("Session dispatch stopped after a host worker error"));
            return new();
        }
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
                        var opened = await Task.Run(() =>
                        {
                            using var save = kind == "load" ? OpenBookmarkForRead(savePath) : null;
                            Stamp("open_begin", timingId);
                            var current = kind == "load" ? DvSession.RestoreFile(savePath) : DvSession.OpenConfiguredCampaign(options);
                            BindPhases(current, timingId, queued.Id);
                            Stamp("open_end", timingId);
                            Stamp("snapshot_begin", timingId);
                            var initialSnapshot = current.Snapshot(current.Viewer);
                            Stamp("snapshot_end", timingId);
                            if (kind == "load") RequireRestorableSnapshot(initialSnapshot);
                            var journal = kind == "load" ? ReadBoundCasebook(savePath, current.Viewer, save!) : (Json: "{}", Warning: "");
                            return (Session: current, Snapshot: initialSnapshot, Viewer: current.Viewer,
                                Casebook: journal.Json, Warning: journal.Warning);
                        });
                        Stamp("handoff_begin", timingId, kind);
                        var snapshot = Materialize(opened.Snapshot, timingId, "viewer");
                        var journalData = Parse(opened.Casebook);
                        events = new GArray { new GDict { ["kind"] = "view",
                            ["resource"] = snapshot["resource"], ["snapshot"] = snapshot,
                            ["casebook"] = journalData, ["casebook_warning"] = opened.Warning } };
                        session = opened.Session;
                        snapshotJson = opened.Snapshot;
                        casebookViewer = opened.Viewer;
                        casebookJson = opened.Casebook;
                        Stamp("handoff_end", timingId, kind);
                    }
                    else if (kind == "view_ready" || kind == "dv_next" || kind == "select")
                    {
                        if (session is null) throw new InvalidOperationException("Session is not open");
                        // An acknowledgement cannot redispatch an already executing intention.
                        if (pendingEvent.Length != 0) continue;
                        var selection = kind == "select" ? input["choice_id"].AsString() : "";
                        var current = session;
                        var prepared = await Task.Run(() =>
                        {
                            BindPhases(current, timingId, queued.Id);
                            Stamp("prepare_begin", timingId, selection);
                            var intent = current.Prepare(selection);
                            Stamp("prepare_end", timingId);
                            var projectedIntent = Materialize(intent, timingId, "intent");
                            var ready = projectedIntent.GetValueOrDefault("status", "").AsString() == "prepared";
                            var actor = ready ? projectedIntent["actor"].AsString() : null;
                            Stamp("snapshot_begin", timingId);
                            var executionSnapshot = ready ? current.Snapshot(actor!) : "{}";
                            var viewer = current.Viewer;
                            // JSON strings can be shared; the two Godot graphs are
                            // independently materialized before either is published.
                            var viewerSnapshot = ready && string.Equals(actor, viewer, StringComparison.Ordinal)
                                ? executionSnapshot : current.Snapshot(viewer);
                            Stamp("snapshot_end", timingId);
                            var projectedViewer = Materialize(viewerSnapshot, timingId, "viewer");
                            var projectedExecution = Materialize(executionSnapshot, timingId, "executor");
                            GArray delivered = new() {
                                new GDict { ["kind"] = "state", ["snapshot"] = projectedViewer },
                                new GDict { ["kind"] = "dv_action", ["intent"] = projectedIntent,
                                    ["execution_snapshot"] = projectedExecution } };
                            return (Snapshot: viewerSnapshot, Events: delivered,
                                Pending: ready ? projectedIntent["action_event"].AsString() : "");
                        });
                        Stamp("handoff_begin", timingId, kind);
                        snapshotJson = prepared.Snapshot;
                        pendingEvent = prepared.Pending;
                        events = prepared.Events;
                        Stamp("handoff_end", timingId, kind);
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
                            var presentation = current.Complete(json, current.Viewer);
                            Stamp("complete_end", timingId, actionEvent);
                            Stamp("snapshot_begin", timingId);
                            var projected = current.Snapshot(current.Viewer);
                            Stamp("snapshot_end", timingId);
                            var projectedViewer = Materialize(projected, timingId, "viewer");
                            var projectedPresentation = Materialize(presentation, timingId, "presentation");
                            GArray delivered = new() {
                                new GDict { ["kind"] = "state", ["snapshot"] = projectedViewer },
                                new GDict { ["kind"] = "dv_present", ["presentation"] = projectedPresentation } };
                            return (Snapshot: projected, Events: delivered);
                        });
                        Stamp("handoff_begin", timingId, kind);
                        completedEvents.Add(actionEvent);
                        pendingEvent = "";
                        snapshotJson = completed.Snapshot;
                        events = completed.Events;
                        Stamp("handoff_end", timingId, kind);
                    }
                }
                catch (Exception error)
                {
                    if (kind == "load" && session is null)
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

    private void RequireIdle(bool requireSession = true)
    {
        if (busy || maintenance || closing) throw new InvalidOperationException("Use the awaited persistence boundary while session work is active");
        RequireNoPendingExecution();
        if (requireSession && session is null) throw new InvalidOperationException("Session is not open");
    }

    public string Save()
    {
        RequireIdle();
        try { return session!.Save(); }
        catch (Exception error) { hostErrors.Add(new { phase = "save-sync", error = error.ToString() }); throw; }
    }

    public void Restore(string json)
    {
        RequireIdle(requireSession: false);
        try { InstallRestored(Task.Run(() => RestoreValue(json)).GetAwaiter().GetResult()); }
        catch (Exception error) { hostErrors.Add(new { phase = "restore-sync", error = error.ToString() }); throw; }
    }

    private static (DvSession Session, string Snapshot) RestoreValue(string json)
    {
        var current = DvSession.Restore(json);
        return (current, current.Snapshot(current.Viewer));
    }

    private void InstallRestored((DvSession Session, string Snapshot) restored)
    {
        session = restored.Session;
        snapshotJson = restored.Snapshot;
        casebookViewer = restored.Session.Viewer;
        casebookJson = "{}";
        pendingEvent = "";
        completedEvents.Clear();
        // Commands accepted for the previous state cannot be replayed into a restored state.
        commands.Clear();
        faulted = false;
    }

    // Internal Task APIs deliberately stay out of Godot's Variant method binding.
    internal Task<string> SaveAsync()
    {
        if (closing || maintenance) throw new InvalidOperationException("Session persistence is unavailable");
        RequireNoPendingExecution();
        maintenance = true;
        var operation = SaveAfterAsync(work);
        work = operation;
        return operation;
    }

    private async Task<string> SaveAfterAsync(Task previous)
    {
        await Task.Yield();
        try
        {
            await previous;
            RequireNoPendingExecution();
            var current = session ?? throw new InvalidOperationException("Session is not open");
            return await Task.Run(current.Save);
        }
        catch (Exception error) { hostErrors.Add(new { phase = "save", error = error.ToString() }); throw; }
        finally { maintenance = false; StartPump(); }
    }

    internal Task RestoreAsync(string json)
    {
        if (closing || maintenance) throw new InvalidOperationException("Session persistence is unavailable");
        RequireNoPendingExecution();
        maintenance = true;
        var operation = RestoreAfterAsync(work, json);
        work = operation;
        return operation;
    }

    private async Task RestoreAfterAsync(Task previous, string json)
    {
        await Task.Yield();
        try
        {
            await previous;
            RequireNoPendingExecution();
            InstallRestored(await Task.Run(() => RestoreValue(json)));
        }
        catch (Exception error) { hostErrors.Add(new { phase = "restore", error = error.ToString() }); throw; }
        finally { maintenance = false; StartPump(); }
    }

    public void RecordVisible(GDict value) => visible.Add(Json.Stringify(value));
    public void RecordInput(GDict value) => inputs.Add(Json.Stringify(value));
    public async void CloseTrace()
    {
        if (closing) return;
        closing = true;
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
            phase = "create-directory";
            path = ProjectSettings.GlobalizePath("user://dv-sessions/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-ffff"));
            if (Directory.Exists(path)) throw new IOException("Evidence directory already exists");
            Directory.CreateDirectory(path);
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
            try
            {
                if (ownsDirectory && path is not null)
                {
                    using var result = new FileStream(Path.Combine(path, "close-result.dvlog"), FileMode.CreateNew, System.IO.FileAccess.Write);
                    result.Write(DuelView.Compiled.BinaryEvidence.Magic);
                    var closeRecord = System.Text.Json.JsonSerializer.SerializeToElement(new { complete = errors.Count == 0, audit_mode = session?.AuditMode, trace_policy = session?.AuditMode == true ? "full" : "omitted-gameplay", session_opened = session is not null, unprocessed_commands = commands.Count, pending_action = pendingEvent, errors });
                    DuelView.Compiled.BinaryEvidence.WriteFrame(result, closeRecord);
                }
            }
            catch (Exception error) { GD.PushError(error.ToString()); }
            EmitSignal(SignalName.TraceClosed);
        }
    }
}
