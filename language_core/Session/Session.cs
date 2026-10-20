using DuelView.Compiled;
using System.Text.Json;
using System.Text.Json.Serialization;

// Serialization and ownership boundary only. The generated DV implementation
// decides eligibility, execution effects, observation, inference and settlement.
public sealed class DvSession
{
    private object state;
    private readonly bool audit;
    public Action<string, string>? PhaseObserver { get; set; }
    public bool AuditMode => audit;
    private DvSession(object value, bool auditMode = true)
    {
        audit = auditMode;
        state = R.record_with(value, "audit_mode", audit);
    }
    private object Run(Func<object> operation)
    {
        using var scope = R.UseAudit(audit);
        using var phases = R.UsePhases(PhaseObserver);
        return operation();
    }

    public static DvSession OpenCampaign(string player = "") => new(DVProgram.SetPlayer(DVProgram.CampaignOpen(), player));
    public static DvSession OpenConfiguredCampaign(string options)
    {
        var configuration = ParseValue(options);
        var audit = R.Bool(R.record_find(configuration, "audit", false));
        using var scope = R.UseAudit(audit);
        return new(DVProgram.HostOpen(configuration), audit);
    }
    public string Viewer => R.Text(Run(() => DVProgram.HostViewer(state)));
    public string Snapshot(string viewer) => HostJson(Run(() => DVProgram.CampaignSnapshot(state, viewer)));

    public static DvSession Open(string scenario) => new(DVProgram.SessionOpen(R.json_parse(scenario)));
    public static DvSession Restore(string snapshot)
    {
        var value = ParseValue(snapshot);
        return new(value, R.Bool(R.record_find(value, "audit_mode", true)));
    }
    // Explicit constructors in DV may replace the state shell; persist host policy
    // at the boundary instead of depending on every shell to carry that metadata.
    private object DurableState() => R.record_with(state, "audit_mode", audit);
    public string Save() => JsonSerializer.Serialize(DurableState(), SnapshotOptions);
    public bool Active => R.Bool(DVProgram.SessionActive(state));

    public string Prepare(string selection = "")
    {
        if (selection.Length > 0) state = Run(() => DVProgram.SelectPlayer(state, selection));
        state = Run(() => DVProgram.SessionPrepare(state));
        return HostJson(Run(() => DVProgram.SessionIntent(state)));
    }

    public string Complete(string receipt, string viewer)
    {
        state = Run(() => DVProgram.SessionComplete(state, ParseValue(receipt)));
        return HostJson(Run(() => DVProgram.SessionPresentation(state, viewer)));
    }

    public string Trace() => R.Text(R.json_stringify(Run(() => DVProgram.SessionClose(state))));

    public void WriteTrace(string path) => R.WriteEvidenceFile(path, Run(() => DVProgram.SessionClose(state)));
    public void WriteSave(string path) => R.WriteEvidenceFile(path, DurableState());
    public static DvSession RestoreFile(string path)
    {
        var value = R.ReadEvidenceFile(path);
        return new(value, R.Bool(R.record_find(value, "audit_mode", true)));
    }

    private static readonly JsonSerializerOptions SnapshotOptions = new()
    {
        MaxDepth = 512,
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
        object dvList when R.TryItems(dvList, out var items) => items.Select(SnapshotValue).ToArray(),
        _ => value
    };

    private static object ParseValue(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 512 });
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
        if (!R.TryItems(value, out var items)) return value;
        if (items.Count > 0 && items.All(item => R.TryItems(item, out var pair) && pair.Count == 2 && pair[0] is string))
        {
            var record = new Dictionary<string, object>();
            foreach (var item in items)
            {
                var pair = R.Items(item);
                record[(string)pair[0]] = HostValue(pair[1]);
            }
            return record;
        }
        return items.Select(HostValue).ToArray();
    }
}

public static partial class DVProgram
{
    internal static object CampaignTrackedActors() => F_dvg_tracked_actors();
    internal static object CampaignChronologicalTrace(object state) => F_dvg_chronological_trace(state);
    internal static object CampaignRun(object maxSteps) => F_dvg_run_campaign(maxSteps);
    internal static object CampaignOpen() => F_dvg_open();
    internal static object HostOpen(object options) => F_dvg_host_open(options);
    internal static object HostViewer(object state) => F_dvg_host_viewer(state);
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
