using DuelView.Compiled;
using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// Serialization and ownership boundary only. The generated DV implementation
// decides eligibility, execution effects, observation, inference and settlement.
public sealed class DvSession
{
    private object state;
    // Owned by the serialized session worker; never published as a mutable DV value.
    // Every transition invalidates this before it can change state or throw.
    private object? intentProjection;
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
    public string Snapshot(string viewer)
    {
        var intent = IntentProjection();
        PhaseObserver?.Invoke("snapshot_projection_begin", viewer);
        var projected = Run(() => DVProgram.CampaignSnapshotWithIntent(state, viewer, intent));
        PhaseObserver?.Invoke("snapshot_projection_end", viewer);
        return EncodeHost(projected, "snapshot:" + viewer);
    }

    private object IntentProjection()
    {
        if (intentProjection is not null) return intentProjection;
        PhaseObserver?.Invoke("intent_projection_begin", "");
        intentProjection = Run(() => DVProgram.SessionIntent(state));
        PhaseObserver?.Invoke("intent_projection_end", "");
        return intentProjection;
    }

    private string EncodeHost(object value, string projection)
    {
        PhaseObserver?.Invoke("host_json_begin", projection);
        var json = HostJson(value);
        PhaseObserver?.Invoke("host_json_end", projection);
        return json;
    }

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
        intentProjection = null;
        if (selection.Length > 0) state = Run(() => DVProgram.SelectPlayer(state, selection.StartsWith("{", StringComparison.Ordinal) ? ParseValue(selection) : selection));
        state = Run(() => DVProgram.SessionPrepare(state));
        return EncodeHost(IntentProjection(), "intent");
    }

    public string Complete(string receipt, string viewer)
    {
        intentProjection = null;
        state = Run(() => DVProgram.SessionComplete(state, ParseValue(receipt)));
        return EncodeHost(Run(() => DVProgram.SessionPresentation(state, viewer)), "presentation");
    }

    public string Trace()
    {
        intentProjection = null;
        return R.Text(R.json_stringify(Run(() => DVProgram.SessionClose(state))));
    }

    public void WriteTrace(string path)
    {
        intentProjection = null;
        R.WriteEvidenceFile(path, Run(() => DVProgram.SessionClose(state)));
    }
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

    // Preserve the existing JSON shape without first allocating a second complete
    // dictionary/array tree. This reads DV containers and never thaws or edits them.
    private static string HostJson(object value)
    {
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { MaxDepth = 64 }))
            WriteHostValue(writer, value);
        return Encoding.UTF8.GetString(output.WrittenSpan);
    }

    private static void StartHostContainer(Utf8JsonWriter writer, bool record)
    {
        if (record) writer.WriteStartObject(); else writer.WriteStartArray();
    }

    private static void WriteHostValue(Utf8JsonWriter writer, object value)
    {
        // The former object-value converter checks depth before every value,
        // including a primitive inside the 64th array or record. An empty
        // container may still end at depth 64 without visiting another value.
        if (writer.CurrentDepth >= 64) throw new JsonException("DV host projection exceeds the JSON nesting limit of 64");
        switch (value)
        {
            case string text: writer.WriteStringValue(text); return;
            case bool flag: writer.WriteBooleanValue(flag); return;
            case double number: writer.WriteNumberValue(number); return;
        }
        if (value is Rational exact)
        {
            StartHostContainer(writer, record: true);
            // The former Dictionary<string, string> tag used the serializer's
            // direct string writer; preserve its legal depth-64 object leaf.
            writer.WriteString("$dv_rational", exact.ToString());
            writer.WriteEndObject();
            return;
        }
        if (!R.TryItems(value, out var items))
        {
            JsonSerializer.Serialize(writer, value);
            return;
        }
        if (items.Count > 0 && items.All(item => R.TryItems(item, out var pair) && pair.Count == 2 && pair[0] is string))
        {
            // Keep the first insertion order and the last value for duplicate
            // keys, exactly as the former Dictionary<string, object> projection.
            var record = new Dictionary<string, object>(items.Count, StringComparer.Ordinal);
            foreach (var item in items)
            {
                var pair = R.Items(item);
                record[(string)pair[0]] = pair[1];
            }
            StartHostContainer(writer, record: true);
            foreach (var pair in record)
            {
                writer.WritePropertyName(pair.Key);
                WriteHostValue(writer, pair.Value);
            }
            writer.WriteEndObject();
            return;
        }
        StartHostContainer(writer, record: false);
        foreach (var item in items) WriteHostValue(writer, item);
        writer.WriteEndArray();
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
    internal static object CampaignSnapshotWithIntent(object state, object viewer, object intent) => F_dvg_snapshot_with_intent(state, viewer, intent);
    internal static object SessionOpen(object scenario) => F_dvs_session_open(scenario, "enabled");
    internal static object SessionActive(object state) => F_dvs_session_active(state);
    internal static object SessionPrepare(object state) => F_dvg_prepare(state);
    internal static object SessionComplete(object state, object receipt) => F_dvg_complete(state, receipt);
    internal static object SessionIntent(object state) => F_dvg_intent(state);
    internal static object SessionPresentation(object state, object viewer) => F_dvg_presentation(state, viewer);
    internal static object SessionClose(object state) => F_dvs_session_close(state);
}
