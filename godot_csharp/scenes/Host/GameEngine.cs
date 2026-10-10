using DualOntologyWorldEngine.Gameplay;
using Godot;
using DualOntologyWorldEngine.Story;
using System.Text.Json;
using GArray = Godot.Collections.Array;
using GDict = Godot.Collections.Dictionary;

/// <summary>只读取资源、转换事件和写存档；游戏流程由无 Godot 依赖的 GameSession 负责。</summary>
public partial class GameEngine : Node
{
    private GameSession? _session;
    private SubjectivePlayerController? _controller;
    private int _agentSeed, _maxAgentSelections = 10000, _maxAgentObservations = 20000;
    private GameSession Session => _session ??= new GameSession(
        Godot.FileAccess.GetFileAsString("res://scenes/Chapter2/Script/generated_chapter_02.json"),
        "res://scenes/Awakening/Awakening.tscn",
        "res://scenes/Chapter2/Field/CampusMapView.tscn",
        _aiSeat ? null : ProjectSettings.GlobalizePath("res://../artifacts/basic-ui-system/player-traces"),
        message => GD.PushWarning(message), _aiSeat ? null : ReadAuthorSources, null, _maxAgentSelections, _maxAgentObservations,
        _aiSeat);
    private bool _aiSeat;

    /// <summary>AI takes the player seat with that character's own declared rules (any role).</summary>
    public void ConfigureAiSeat(int seed, int maxSelections, int maxObservations)
    {
        if (_session is not null) throw new InvalidOperationException("Configure the controller before starting a session.");
        (_aiSeat, _agentSeed, _maxAgentSelections, _maxAgentObservations) = (true, seed, maxSelections, maxObservations);
    }
    public string AgentCandidateInputJson(string token)
    {
        try { return JsonSerializer.Serialize(Session.AgentCandidateInput(token)); }
        catch (PlayerBoundaryException error) { return JsonSerializer.Serialize(new Dictionary<string, object?> { ["boundary"] = error.Code }); }
    }
    public bool AgentEnabled() => _session?.AgentEnabled ?? false;
    public string AgentObservationJson() => JsonSerializer.Serialize(Session.AgentObservation);
    public string AgentTimeJson() => JsonSerializer.Serialize(Session.AgentTime);
    public int AgentSelections() => Session.AgentSelections;
    public string AgentDecideJson()
    {
        try
        {
            var declaration = Session.AgentDeclaration ?? throw new PlayerBoundaryException("agent_not_declared");
            _controller ??= new(declaration, _agentSeed);
            return JsonSerializer.Serialize(_controller.Decide(Session.AgentObservation!, Session.AgentCognition));
        }
        catch (PlayerBoundaryException error)
        { Session.CloseAgent(); return JsonSerializer.Serialize(new PlayerDecision(null, error.Code, null)); }
        catch (Exception error)
        { Session.CloseAgent(); GD.PushError(error.ToString()); return JsonSerializer.Serialize(new PlayerDecision(null, "engine_error", null)); }
    }
    public string AgentObservePanel(string panel, string runLabel)
    {
        try { Session.ObserveAgentPanel(panel, runLabel); return ""; }
        catch (PlayerBoundaryException error) { Session.CloseAgent(); return error.Code; }
    }
    public string AgentObserveField(GArray offers)
    {
        try
        {
            var entries = offers.Select(v => FromDictionary(v.AsGodotDictionary())).Select(o =>
            {
                var point = (o.GetValueOrDefault("location") as IEnumerable<object?> ?? []).ToArray();
                return new PlayerFieldOffer(ChapterEvents.Text(o, "label"), ChapterEvents.Text(o, "kind"),
                    ChapterEvents.Flag(o, "new"), ChapterEvents.Map(o, "input"), Purpose: ChapterEvents.Text(o, "purpose"))
                { Location = point.Length == 2 ? new(Convert.ToDouble(point[0]), Convert.ToDouble(point[1])) : null };
            }).ToArray();
            Session.ObserveAgentField(entries);
            return "";
        }
        catch (PlayerBoundaryException error) { return error.Code; }
    }
    public void CloseAgent() => Session.CloseAgent();
    public GArray DispatchField(GDict input)
    {
        try
        {
            var output = new GArray();
            foreach (var ev in Session.DispatchField(FromDictionary(input))) output.Add(ToGodot(ev));
            return output;
        }
        catch (PlayerBoundaryException error)
        { return new GArray { ToGodot(ChapterEvents.Event("agent_stopped", ("reason", error.Code))) }; }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            return new GArray { ToGodot(ChapterEvents.Event("error", ("message", error.Message))) };
        }
    }

    private static IReadOnlyDictionary<string, string> ReadAuthorSources()
    {
        var sources = new SortedDictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var repository = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://../"));
            var scriptDirectory = ProjectSettings.GlobalizePath("res://scenes/Awakening/Script");
            var paths = new List<string> { System.IO.Path.Combine(scriptDirectory, "chapter_01.story") };
            try
            {
                paths.AddRange(System.IO.Directory.EnumerateFiles(System.IO.Path.Combine(scriptDirectory, "story"),
                    "*.story", System.IO.SearchOption.AllDirectories));
            }
            catch (Exception error)
            { GD.PushWarning("Author story snapshot could not enumerate fragments; tracing continues: " + error.Message); }
            foreach (var path in paths.OrderBy(p => System.IO.Path.GetRelativePath(repository, p).Replace('\\', '/'), StringComparer.Ordinal))
            {
                try
                {
                    var key = System.IO.Path.GetRelativePath(repository, path).Replace('\\', '/');
                    sources[key] = System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8);
                }
                catch (Exception error)
                { GD.PushWarning("Author story snapshot could not read " + path + "; tracing continues: " + error.Message); }
            }
        }
        catch (Exception error)
        { GD.PushWarning("Author story snapshot could not be read; tracing continues: " + error.Message); }
        return sources;
    }

    public void RecordVisible(GDict payload) => Session.RecordVisible(FromDictionary(payload));
    public void RecordInput(GDict payload) => Session.RecordInput(FromDictionary(payload));
    public void CloseTrace() { _session?.Dispose(); }
    public override void _ExitTree() => CloseTrace();

    public GArray Dispatch(GDict input)
    {
        try
        {
            var result = new GArray();
            foreach (var ev in Session.Dispatch(FromDictionary(input)))
            {
                if (ev.GetValueOrDefault("kind") as string == "save") { Save(ev); continue; }
                result.Add(ToGodot(ev));
            }
            return result;
        }
        catch (Exception error)
        {
            if (error is PlayerBoundaryException boundary)
                return new GArray { ToGodot(ChapterEvents.Event("agent_stopped", ("reason", boundary.Code))) };
            GD.PushError(error.ToString());
            var output = new Dictionary<string, object?> { ["kind"] = "error", ["message"] = error.Message };
            _session?.RecordOutput(output);
            return new GArray { ToGodot(output) };
        }
    }

    public GDict Snapshot() => _session is null ? new() : ToGodot(_session.Snapshot()).AsGodotDictionary();

    /// <summary>UI-independent full session API. Buttons and file selection belong to the UI host.</summary>
    public GDict ExportSession()
    {
        return Session.Save() switch
        {
            DualOntologyWorldEngine.Core.ContractResult<byte[]>.Success s => new() { ["ok"]=true,["data"]=s.Value },
            DualOntologyWorldEngine.Core.ContractResult<byte[]>.Failure f => new() { ["ok"]=false,["code"]=f.Error.Code.ToString(),["path"]=f.Error.Path },
            _ => throw new InvalidOperationException()
        };
    }
    public GDict RestoreSession(byte[] data)
    {
        return Session.Restore(data) switch
        {
            DualOntologyWorldEngine.Core.ContractResult<bool>.Success => new() { ["ok"]=true,["snapshot"]=Snapshot(),["restore_epoch"]=Session.RestoreEpoch.ToString("D") },
            DualOntologyWorldEngine.Core.ContractResult<bool>.Failure f => new() { ["ok"]=false,["code"]=f.Error.Code.ToString(),["path"]=f.Error.Path },
            _ => throw new InvalidOperationException()
        };
    }

    private static void Save(IReadOnlyDictionary<string, object?> ev)
    {
        using var file = Godot.FileAccess.Open((string)ev["path"]!, Godot.FileAccess.ModeFlags.Write);
        file?.StoreString(Json.Stringify(ToGodot(ev.GetValueOrDefault("data")), "\t"));
    }

    // ---------------------------------------------------------- conversion --

    private static Dictionary<string, object?> FromDictionary(GDict input) => input.ToDictionary(p => p.Key.AsString(), p => FromGodot(p.Value));
    private static object? FromGodot(Variant value) => value.VariantType switch
    {
        Variant.Type.Nil => null, Variant.Type.Dictionary => FromDictionary(value.AsGodotDictionary()),
        Variant.Type.Array => value.AsGodotArray().Select(FromGodot).ToArray(),
        Variant.Type.PackedStringArray => value.AsStringArray().Cast<object?>().ToArray(),
        Variant.Type.Bool => value.AsBool(), Variant.Type.Int => value.AsInt64(), Variant.Type.Float => value.AsDouble(),
        Variant.Type.Vector2 => new object?[] { (double)value.AsVector2().X, (double)value.AsVector2().Y },
        _ => value.AsString(),
    };
    private static Variant ToGodot(object? value)
    {
        switch (value)
        {
            case null: return default;
            case string text: return text;
            case System.Collections.IDictionary dictionary:
            {
                var result = new GDict();
                foreach (System.Collections.DictionaryEntry pair in dictionary) result[pair.Key.ToString()!] = ToGodot(pair.Value);
                return result;
            }
            case IReadOnlyDictionary<string, object?> readOnly:
            {
                var result = new GDict();
                foreach (var pair in readOnly) result[pair.Key] = ToGodot(pair.Value);
                return result;
            }
            case System.Collections.IEnumerable sequence:
            {
                var result = new GArray();
                foreach (var item in sequence) result.Add(ToGodot(item));
                return result;
            }
            case bool b: return b;
            case int i: return i;
            case long l: return l;
            case double d: return d;
            case float f: return f;
            default: return value.ToString()!;
        }
    }
}
