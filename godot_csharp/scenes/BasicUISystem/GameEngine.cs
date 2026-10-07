using DualOntologyWorldEngine.Gameplay;
using Godot;
using GArray = Godot.Collections.Array;
using GDict = Godot.Collections.Dictionary;

/// <summary>只读取资源、转换事件和写存档；游戏流程由无 Godot 依赖的 GameSession 负责。</summary>
public partial class GameEngine : Node
{
    private GameSession? _session;
    private GameSession Session => _session ??= new GameSession(
        Godot.FileAccess.GetFileAsString("res://scenes/Chapter2/Script/generated_chapter_02.json"),
        "res://scenes/Awakening/Awakening.tscn",
        "res://scenes/Chapter2/Field/CampusMapView.tscn",
        ProjectSettings.GlobalizePath("res://../artifacts/basic-ui-system/player-traces"),
        message => GD.PushWarning(message), ReadAuthorSources);

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
            GD.PushError(error.ToString());
            var output = new Dictionary<string, object?> { ["kind"] = "error", ["message"] = error.Message };
            _session?.RecordOutput(output);
            return new GArray { ToGodot(output) };
        }
    }

    public GDict Snapshot() => _session is null ? new() : ToGodot(_session.Snapshot()).AsGodotDictionary();

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
