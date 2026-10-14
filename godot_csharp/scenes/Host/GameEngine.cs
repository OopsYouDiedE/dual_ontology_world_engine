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
    private DvSession? session;
    private bool busy;
    private bool awaitingReceipt;
    private readonly List<string> visible = [];
    private readonly List<string> inputs = [];
    public GDict Snapshot() => session is null ? new() : Parse(session.Snapshot("detective"));
    private static GDict Parse(string json) => Json.ParseString(json).AsGodotDictionary();
    public GArray Dispatch(GDict input)
    {
        var kind = input.GetValueOrDefault("kind", "").AsString();
        inputs.Add(Json.Stringify(input));
        if (kind == "start") { StartCampaign(); return new(); }
        if (kind == "view_ready" || kind == "dv_next") Prepare();
        if (kind == "dv_complete") Complete(input["receipt"].AsGodotDictionary());
        return new();
    }
    private async void StartCampaign()
    {
        if (busy || closing) return;
        busy = true;
        try
        {
            var opening = Task.Run(DvSession.OpenCampaign);
            work = opening;
            session = await opening;
            var snapshot = Snapshot();
            EmitSignal(SignalName.EventsReady, new GArray { new GDict { ["kind"] = "view", ["resource"] = snapshot["resource"] } });
        }
        catch (Exception error) { Report(error); }
        finally { busy = false; }
    }
    private async void Prepare()
    {
        if (busy || closing || awaitingReceipt || session is null) return;
        busy = true;
        try
        {
            var preparing = Task.Run(session.Prepare);
            work = preparing;
            var intent = Parse(await preparing);
            awaitingReceipt = intent.GetValueOrDefault("status", "").AsString() == "prepared";
            EmitSignal(SignalName.EventsReady, new GArray { new GDict { ["kind"] = "dv_action", ["intent"] = intent } });
        }
        catch (Exception error) { Report(error); }
        finally { busy = false; }
    }
    private async void Complete(GDict receipt)
    {
        if (busy || closing || !awaitingReceipt || session is null) return;
        busy = true;
        try
        {
            var json = Json.Stringify(receipt);
            var completing = Task.Run(() => session.Complete(json, "detective"));
            work = completing;
            var presentation = Parse(await completing);
            awaitingReceipt = false;
            EmitSignal(SignalName.EventsReady, new GArray { new GDict { ["kind"] = "state", ["snapshot"] = Snapshot() }, new GDict { ["kind"] = "dv_present", ["presentation"] = presentation } });
        }
        catch (Exception error) { Report(error); }
        finally { busy = false; }
    }
    private void Report(Exception error)
    {
        GD.PushError(error.ToString());
        EmitSignal(SignalName.EventsReady, new GArray { new GDict { ["kind"] = "error", ["message"] = error.Message } });
    }
    public string Save() => session!.Save();
    public void Restore(string json) => session = DvSession.Restore(json);
    public void RecordVisible(GDict value) => visible.Add(Json.Stringify(value));
    public void RecordInput(GDict value) => inputs.Add(Json.Stringify(value));
    public async void CloseTrace()
    {
        if (closing) return;
        closing = true;
        try { await work; } catch (Exception error) { GD.PushError(error.ToString()); }
        if (session is null) { EmitSignal(SignalName.TraceClosed); return; }
        var path = ProjectSettings.GlobalizePath("user://dv-sessions/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-ffff"));
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "session.json"), session.Trace());
        File.WriteAllLines(Path.Combine(path, "visible.jsonl"), visible);
        File.WriteAllLines(Path.Combine(path, "inputs.jsonl"), inputs);
        File.WriteAllText(Path.Combine(path, "save.json"), session.Save());
        EmitSignal(SignalName.TraceClosed);
    }
}
