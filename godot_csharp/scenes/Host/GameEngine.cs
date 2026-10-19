using Godot;
using System.Text.Json;
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
    private readonly Queue<string> commands = new();
    private readonly HashSet<string> completedEvents = new(StringComparer.Ordinal);
    private readonly List<string> visible = [];
    private readonly List<string> inputs = [];
    private readonly List<object> hostErrors = [];

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
    private static GDict Parse(string json) => Json.ParseString(json).AsGodotDictionary();

    public GArray Dispatch(GDict input)
    {
        var json = Json.Stringify(input);
        inputs.Add(json);
        if (closing) return new();
        if (faulted)
        {
            Report(new InvalidOperationException("Session dispatch stopped after a host worker error"));
            return new();
        }
        commands.Enqueue(json);
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
                var input = Parse(commands.Dequeue());
                var kind = input.GetValueOrDefault("kind", "").AsString();
                GArray events = new();
                try
                {
                    if (kind == "start")
                    {
                        if (session is not null) throw new InvalidOperationException("Session is already open");
                        var options = optionsJson;
                        var opened = await Task.Run(() =>
                        {
                            var current = DvSession.OpenConfiguredCampaign(options);
                            return (Session: current, Snapshot: current.Snapshot(current.Viewer));
                        });
                        session = opened.Session;
                        snapshotJson = opened.Snapshot;
                        var snapshot = Snapshot();
                        events.Add(new GDict { ["kind"] = "view", ["resource"] = snapshot["resource"] });
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
                            var intent = current.Prepare(selection);
                            using var document = JsonDocument.Parse(intent);
                            var root = document.RootElement;
                            var ready = root.GetProperty("status").GetString() == "prepared";
                            var actor = ready ? root.GetProperty("actor").GetString() : null;
                            var executionSnapshot = ready ? current.Snapshot(actor!) : "{}";
                            var viewer = current.Viewer;
                            // JSON strings are immutable; the two Godot boundaries still parse independent values.
                            var viewerSnapshot = ready && string.Equals(actor, viewer, StringComparison.Ordinal)
                                ? executionSnapshot : current.Snapshot(viewer);
                            return (Intent: intent, Snapshot: viewerSnapshot, ExecutionSnapshot: executionSnapshot);
                        });
                        snapshotJson = prepared.Snapshot;
                        var intent = Parse(prepared.Intent);
                        if (intent.GetValueOrDefault("status", "").AsString() == "prepared")
                            pendingEvent = intent["action_event"].AsString();
                        events.Add(new GDict { ["kind"] = "state", ["snapshot"] = Snapshot() });
                        events.Add(new GDict { ["kind"] = "dv_action", ["intent"] = intent,
                            ["execution_snapshot"] = Parse(prepared.ExecutionSnapshot) });
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
                            var presentation = current.Complete(json, current.Viewer);
                            return (Presentation: presentation, Snapshot: current.Snapshot(current.Viewer));
                        });
                        completedEvents.Add(actionEvent);
                        pendingEvent = "";
                        snapshotJson = completed.Snapshot;
                        events.Add(new GDict { ["kind"] = "state", ["snapshot"] = Snapshot() });
                        events.Add(new GDict { ["kind"] = "dv_present", ["presentation"] = Parse(completed.Presentation) });
                    }
                }
                catch (Exception error)
                {
                    // A failed projection can follow a successful state transition. Never retry it blindly.
                    faulted = true;
                    hostErrors.Add(new { phase = kind, error = error.ToString() });
                    Report(error);
                }
                // Reentrant signal handlers append to this pump; no input is dropped while it is busy.
                if (!closing && events.Count != 0) EmitSignal(SignalName.EventsReady, events);
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
            phase = "inputs";
            File.WriteAllLines(Path.Combine(path, "inputs.jsonl"), inputs);
            phase = "visible";
            File.WriteAllLines(Path.Combine(path, "visible.jsonl"), visible);
            var current = session;
            if (current is null) return;
            phase = "trace";
            var tracePath = Path.Combine(path, "session.json.partial");
            try
            {
                await Task.Run(() => current.WriteTrace(tracePath));
                File.Move(tracePath, Path.Combine(path, "session.json"));
            }
            catch (Exception error)
            {
                GD.PushError(error.ToString());
                errors.Add(new { phase, error = error.ToString() });
            }
            phase = "save";
            var savePath = Path.Combine(path, "save.json.partial");
            try
            {
                await Task.Run(() => current.WriteSave(savePath));
                File.Move(savePath, Path.Combine(path, "save.json"));
            }
            catch (Exception error)
            {
                GD.PushError(error.ToString());
                errors.Add(new { phase, error = error.ToString() });
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
                    using var result = new FileStream(Path.Combine(path, "close-result.json"), FileMode.CreateNew, System.IO.FileAccess.Write);
                    System.Text.Json.JsonSerializer.Serialize(result, new { complete = errors.Count == 0, session_opened = session is not null, unprocessed_commands = commands.Count, pending_action = pendingEvent, errors });
                }
            }
            catch (Exception error) { GD.PushError(error.ToString()); }
            EmitSignal(SignalName.TraceClosed);
        }
    }
}
