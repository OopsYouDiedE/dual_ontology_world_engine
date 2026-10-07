using DualOntologyWorldEngine.Awakening;
using DualOntologyWorldEngine.Chapter2.Prototype;
using DualOntologyWorldEngine.Story;
using Godot;
using ProtoGame = DualOntologyWorldEngine.Chapter2.Prototype.Game;
using GArray = Godot.Collections.Array;
using GDict = Godot.Collections.Dictionary;

/// <summary>
/// 所有章节共用的 Godot 桥：只做 Variant 与 C# 字典之间的转换、写存档事件，不含任何章节规则。
/// 宿主用 Open 打开章节，此后只调用 Dispatch / Snapshot。
/// </summary>
public partial class ChapterEngine : Node
{
    private IChapterSession? _session;

    public GDict Open(string chapter)
    {
        try
        {
            _session = chapter switch
            {
                "chapter1" => new Chapter1Session(),
                "chapter2" => OpenChapter2(),
                _ => throw new InvalidOperationException("未知章节：" + chapter),
            };
            return new() { ["ok"] = true, ["error"] = "" };
        }
        catch (Exception error) { return new() { ["ok"] = false, ["error"] = error.Message }; }
    }

    public bool Started() => _session is not null;

    public GArray Dispatch(GDict input)
    {
        if (_session is null) return new GArray { new GDict { ["kind"] = "error", ["message"] = "章节尚未启动。" } };
        var events = _session.Dispatch(FromDictionary(input));
        var result = new GArray();
        foreach (var ev in events)
        {
            if (ev.GetValueOrDefault("kind") as string == "save") { Save(ev); continue; }
            result.Add(ToGodot(ev));
        }
        return result;
    }

    public GDict Snapshot() => _session is null ? new() : ToGodot(_session.Snapshot()).AsGodotDictionary();

    /// <summary>自动检查用的只读探针与注入（不经游玩）。</summary>
    public Variant Probe(string name, GArray args)
        => _session is IChapterProbe probe ? ToGodot(probe.Probe(name, args.Select(FromGodot).ToArray())) : default;

    private static void Save(IReadOnlyDictionary<string, object?> ev)
    {
        using var file = Godot.FileAccess.Open((string)ev["path"]!, Godot.FileAccess.ModeFlags.Write);
        file?.StoreString(Json.Stringify(ToGodot(ev.GetValueOrDefault("data")), "\t"));
    }

    // ----------------------------------------------------------- chapter 2 --

    /// <summary>第二章接手第一章运行中的世界；没有时由第一章标准会话（含本地存档）显式初始化。</summary>
    private static LiveChapterWorld EnsureContinuation()
    {
        if (ChapterWorldContinuity.Current is not null) return ChapterWorldContinuity.Current;
        var chapter = new Chapter1Script();
        var morning = chapter.CreateWorld();
        morning.InitializePlayerPerception();
        var content = chapter.CreateContent();
        if (Godot.FileAccess.FileExists("user://chapter1_end.json"))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(Godot.FileAccess.GetFileAsString("user://chapter1_end.json"));
                var root = doc.RootElement;
                if (root.TryGetProperty("detective_name", out var name) && name.GetString() is { } text && !string.IsNullOrWhiteSpace(text))
                    content.Session.Set("state", "detective_name", text);
                if (root.TryGetProperty("trust_familiar", out var trust) && trust.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)
                    content.Session.Set("state", "full_match_seen", trust.GetBoolean());
                if (root.TryGetProperty("asked_hand", out var asked) && asked.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)
                    content.Session.Set("state", "asked_hand", asked.GetBoolean());
            }
            catch (System.Text.Json.JsonException) { }
        }
        var live = morning.ExportContinuation(content.Session);
        ChapterWorldContinuity.Publish(live);
        return live;
    }

    private static IChapterSession OpenChapter2()
    {
        var continuation = EnsureContinuation();
        var json = Godot.FileAccess.GetFileAsString("res://scenes/Chapter2/Script/generated_chapter_02.json");
        var options = Chapter2AuthoredProfiles.Apply(json);
        // 先校验全部作者数据，再改动运行中的世界。
        _ = new Chapter2Narrative(json, true);
        Chapter2InteractionSession.ValidateScript(json);
        foreach (var character in continuation.Characters.Values)
            if (character.Self is null || continuation.Runtime.Representations(character.Body).Count != 1
                || !ReferenceEquals(continuation.Runtime.KnownProjection(character.Soul, character.Body), character.Self)
                || continuation.Simulation.IsActing(character.Self))
                throw new InvalidOperationException("The live awakening character binding is missing or still acting.");
        var game = new ProtoGame(options, continuation, WorldScriptDocument.Parse(json, allowChapterAdapter: true).Single());
        var session = new Chapter2InteractionSession(game, json);
        ChapterWorldContinuity.Clear(); // 第二章从此拥有这个运行中的会话。
        return session;
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
