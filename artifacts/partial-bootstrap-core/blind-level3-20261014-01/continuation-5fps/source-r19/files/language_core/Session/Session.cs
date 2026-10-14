using DuelView.Compiled;
using System.Text.Json;
using System.Text.Json.Serialization;

// Serialization and ownership boundary only. The generated DV implementation
// decides eligibility, execution effects, observation, inference and settlement.
public sealed class DvSession
{
    private object state;
    private DvSession(object value) => state = value;

    public static DvSession OpenCampaign(string player = "") => new(DVProgram.SetPlayer(DVProgram.CampaignOpen(), player));
    public string Snapshot(string viewer) => HostJson(DVProgram.CampaignSnapshot(state, viewer));

    public static DvSession Open(string scenario) => new(DVProgram.SessionOpen(R.json_parse(scenario)));
    public static DvSession Restore(string snapshot) => new(ParseValue(snapshot));
    public string Save() => JsonSerializer.Serialize(SnapshotValue(state));
    public bool Active => R.Bool(DVProgram.SessionActive(state));

    public string Prepare(string selection = "")
    {
        if (selection.Length > 0) state = DVProgram.SelectPlayer(state, selection);
        state = DVProgram.SessionPrepare(state);
        return HostJson(DVProgram.SessionIntent(state));
    }

    public string Complete(string receipt, string viewer)
    {
        state = DVProgram.SessionComplete(state, ParseValue(receipt));
        return HostJson(DVProgram.SessionPresentation(state, viewer));
    }

    public string Trace() => R.Text(R.json_stringify(DVProgram.SessionClose(state)));

    public void WriteTrace(string path)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        JsonSerializer.Serialize(stream, DVProgram.SessionClose(state));
    }

    public void WriteSave(string path)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        JsonSerializer.Serialize(stream, state, SnapshotOptions);
    }

    private static readonly JsonSerializerOptions SnapshotOptions = new()
    {
        Converters = { new SnapshotExactConverter() }
    };

    private sealed class SnapshotExactConverter : JsonConverter<Rational>
    {
        public override Rational Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            using var value = JsonDocument.ParseValue(ref reader);
            return Rational.From(value.RootElement.GetProperty("$dv_rational").GetString()!);
        }

        public override void Write(Utf8JsonWriter writer, Rational value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("$dv_rational", value.ToString());
            writer.WriteEndObject();
        }
    }

    private static object SnapshotValue(object value) => value switch
    {
        Rational exact => new Dictionary<string, string> { ["$dv_rational"] = exact.ToString() },
        List<object> items => items.Select(SnapshotValue).ToArray(),
        _ => value
    };

    private static object ParseValue(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 256 });
        return ReadValue(document.RootElement);
    }

    private static object ReadValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Array => value.EnumerateArray().Select(ReadValue).ToList(),
        JsonValueKind.Object when value.TryGetProperty("$dv_rational", out var exact) => Rational.From(exact.GetString()!),
        JsonValueKind.Object => value.EnumerateObject().Select(p => (object)new List<object> { p.Name, ReadValue(p.Value) }).ToList(),
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw new JsonException("Unsupported DV value")
    };

    private static string HostJson(object value) => JsonSerializer.Serialize(HostValue(value));
    private static object HostValue(object value)
    {
        if (value is Rational) return SnapshotValue(value);
        if (value is not List<object> items) return value;
        if (items.Count > 0 && items.All(item => item is List<object> { Count: 2 } pair && pair[0] is string))
        {
            var record = new Dictionary<string, object>();
            foreach (List<object> pair in items) record[(string)pair[0]] = HostValue(pair[1]);
            return record;
        }
        return items.Select(HostValue).ToArray();
    }
}

public static partial class DVProgram
{
    internal static object CampaignOpen() => F_dvg_open();
    internal static object SetPlayer(object state, object actor) => F_dvs_set_player(state, actor);
    internal static object SelectPlayer(object state, object id) => F_dvs_select_player(state, id);
    internal static object CampaignSnapshot(object state, object viewer) => F_dvg_snapshot(state, viewer);
    internal static object SessionOpen(object scenario) => F_dvs_session_open(scenario, "enabled");
    internal static object SessionActive(object state) => F_dvs_session_active(state);
    internal static object SessionPrepare(object state) => F_dvg_prepare(state);
    internal static object SessionComplete(object state, object receipt) => F_dvg_complete(state, receipt);
    internal static object SessionIntent(object state) => F_dvg_intent(state);
    internal static object SessionPresentation(object state, object viewer) => F_dvg_presentation(state, viewer);
    internal static object SessionClose(object state) => F_dvs_session_close(state);
}
