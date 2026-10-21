using DuelView.Compiled;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>The single state owner for Godot, development commands and the text host.</summary>
public sealed class DvSession
{
    private object state;
    private object lastCommittedState;
    private object? intentProjection;
    private IReadOnlyDictionary<string, object?>? nativeIntent;
    private object? closedTrace;
    private readonly bool audit;
    private readonly bool developer;
    private readonly Dictionary<string, ViewerProjection> viewers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (long Version, IReadOnlyDictionary<string, object?> Value)> executors = new(StringComparer.Ordinal);
    private PreviewBranch? preview;
    public Action<string, string>? PhaseObserver { get; set; }
    public bool AuditMode => audit;
    public bool DevMode => developer;
    public bool IsClosed => closedTrace is not null;
    public long Version { get; private set; }

    private sealed class ViewerProjection
    {
        internal long Version = -1, DeliveredVersion = -1;
        internal string Map = "", DeliveredMap = "";
        internal int Chapter;
        internal Dictionary<string, object> Static = new(StringComparer.Ordinal);
        internal Dictionary<string, object> Raw = new(StringComparer.Ordinal);
        internal Dictionary<string, object> Delivered = new(StringComparer.Ordinal);
        internal IReadOnlyDictionary<string, object?> Value = HostValues.Empty;
    }
    private sealed record PreviewBranch(PreparedPreview Public, object State, int FirstEvent);

    private DvSession(object value, bool auditMode = true, bool? developerMode = null)
    {
        audit = auditMode;
        var story = R.record_find(value, "story", new List<object>());
        developer = developerMode ?? R.Bool(R.record_find(story, "development_mode", false));
        story = R.record_with(story, "development_mode", developer);
        state = R.record_with(R.record_with(value, "story", story), "audit_mode", audit);
        if (HasPending(state)) throw new InvalidDataException("A restorable session must be saved at a committed action boundary.");
        lastCommittedState = R.snapshot(state);
    }

    private object Run(Func<object> operation, bool speculative = false)
    {
        using var scope = R.UseAudit(audit);
        using var phases = R.UsePhases(speculative ? null : PhaseObserver);
        using var recording = R.UseRawRecording(!speculative);
        using var output = R.UseOutput(speculative ? TextWriter.Null : Console.Error);
        return operation();
    }
    private static object Field(object value, string name, object fallback) => R.record_find(value, name, fallback);
    private static bool HasPending(object value) => Field(value, "pending", false) is not false;
    private static IReadOnlyList<object> Events(object value) => R.Items(Field(value, "events", new List<object>()));
    private void BeginTransition()
    {
        if (IsClosed) throw new InvalidOperationException("The session is closed.");
        Version++;
        intentProjection = null;
        nativeIntent = null;
        preview = null;
        executors.Clear();
    }
    private void RememberCommitted()
    {
        if (!HasPending(state)) lastCommittedState = R.snapshot(state);
    }

    public static DvSession OpenCampaign(string player = "") => new(DVProgram.SetPlayer(DVProgram.CampaignOpen(), player));
    public static DvSession OpenConfiguredCampaign(string options)
    {
        var configuration = HostValues.Parse(options);
        var auditMode = R.Bool(Field(configuration, "audit", false));
        var developerMode = R.Bool(Field(configuration, "developer", false));
        using var scope = R.UseAudit(auditMode);
        using var output = R.UseOutput(Console.Error);
        return new(DVProgram.HostOpen(configuration), auditMode, developerMode);
    }
    public static DvSession Open(string scenario) => new(DVProgram.SessionOpen(HostValues.Parse(scenario)));
    public static DvSession Restore(string snapshot)
    {
        var value = HostValues.Parse(snapshot);
        return new(value, R.Bool(Field(value, "audit_mode", true)));
    }
    public static DvSession RestoreFile(string path)
    {
        var value = R.ReadEvidenceFile(path);
        return new(value, R.Bool(Field(value, "audit_mode", true)));
    }

    public string Viewer => R.Text(Run(() => DVProgram.HostViewer(state)));
    public bool Active => !IsClosed && R.Bool(Run(() => DVProgram.SessionActive(state)));
    public IReadOnlyDictionary<string, object?> Intent => nativeIntent ??= HostValues.Record(IntentProjection());

    private object IntentProjection()
    {
        if (intentProjection is not null) return intentProjection;
        PhaseObserver?.Invoke("intent_projection_begin", "");
        intentProjection = R.snapshot(Run(() => DVProgram.SessionIntent(state)));
        PhaseObserver?.Invoke("intent_projection_end", "");
        return intentProjection;
    }

    public IReadOnlyDictionary<string, object?> PrepareValue(string selection = "")
    {
        if (selection.Length == 0 && (HasPending(state) || R.Bool(Field(state, "awaiting_player", false)))) return Intent;
        BeginTransition();
        if (selection.Length > 0)
            state = Run(() => DVProgram.SelectPlayer(state, SelectionValue(selection)));
        state = Run(() => DVProgram.SessionPrepare(state));
        RememberCommitted();
        return Intent;
    }
    private static object SelectionValue(string selection) => selection.StartsWith('{') ? HostValues.Parse(selection) : selection;
    public string Prepare(string selection = "") => JsonSerializer.Serialize(PrepareValue(selection));

    public IReadOnlyDictionary<string, object?> PrepareBackgroundValue()
    {
        if (IsClosed || HasPending(state) || !R.Bool(Field(state, "awaiting_player", false))) return Intent;
        var advanced = Run(() => DVProgram.PrepareBackground(state, Viewer));
        if (!ReferenceEquals(advanced, state))
        {
            BeginTransition();
            state = advanced;
            RememberCommitted();
        }
        return Intent;
    }

    public PreparedPreview PrepareAhead(string selection)
    {
        if (preview is { } saved && saved.Public.BaseVersion == Version && saved.Public.Selection == selection) return saved.Public;
        if (HasPending(state) || !R.Bool(Field(state, "awaiting_player", false)))
            throw new InvalidOperationException("PrepareAhead requires the current player's input boundary.");
        var firstEvent = Events(state).Count;
        var branch = R.copy_value(R.snapshot(state));
        branch = Run(() => DVProgram.SelectPlayer(branch, SelectionValue(selection)), speculative: true);
        branch = Run(() => DVProgram.SessionPrepare(branch), speculative: true);
        var intent = HostValues.Record(Run(() => DVProgram.SessionIntent(branch), speculative: true));
        var candidate = new PreparedPreview(Guid.NewGuid().ToString("N"), Version, selection, intent);
        preview = new PreviewBranch(candidate, R.snapshot(branch), firstEvent);
        return candidate;
    }
    public bool TryAdoptPrepared(string token, out IReadOnlyDictionary<string, object?> intent)
    {
        intent = HostValues.Empty;
        if (preview is not { } candidate || candidate.Public.Token != token || candidate.Public.BaseVersion != Version || IsClosed) return false;
        BeginTransition();
        state = R.copy_value(candidate.State);
        using (R.UseAudit(audit))
        {
            var events = Events(state);
            for (var index = candidate.FirstEvent; index < events.Count; index++) R.raw_event(events[index]);
        }
        RememberCommitted();
        intent = Intent;
        return true;
    }
    public void CancelPrepared() => preview = null;

    public IReadOnlyDictionary<string, object?> CompleteValue(string receipt, string viewer) => CompleteDv(HostValues.Parse(receipt), viewer);
    public IReadOnlyDictionary<string, object?> CompleteValue(IReadOnlyDictionary<string, object?> receipt, string viewer) => CompleteDv(HostValues.ToDv(receipt), viewer);
    private IReadOnlyDictionary<string, object?> CompleteDv(object receipt, string viewer)
    {
        var pending = Field(state, "pending", false);
        var action = Field(pending, "action", new List<object>());
        var actionEvent = R.Text(Field(pending, "action_event", ""));
        var actor = R.Text(Field(pending, "id", ""));
        var actionId = R.Text(Field(action, "id", ""));
        var eventCursor = Events(state).Count;
        BeginTransition();
        state = Run(() => DVProgram.SessionComplete(state, receipt));
        RememberCommitted();
        var projected = Run(() => DVProgram.SessionPresentation(state, viewer));
        var presentation = new Dictionary<string, object?>(HostValues.Record(projected), StringComparer.Ordinal);
        var outcome = "";
        var events = Events(state);
        for (var index = eventCursor; index < events.Count; index++)
        {
            var entry = events[index];
            if (R.Text(Field(entry, "type", "")) != "action_execution") continue;
            var data = Field(entry, "data", new List<object>());
            var result = Field(data, "receipt", new List<object>());
            outcome = R.Text(Field(Field(result, "execution", new List<object>()), "status", ""));
            break;
        }
        presentation["outcome"] = outcome;
        presentation["action_event"] = actionEvent;
        presentation["completed_actor"] = actor;
        presentation["completed_action"] = actionId;
        return HostValues.Freeze(presentation);
    }
    public string Complete(string receipt, string viewer) => JsonSerializer.Serialize(CompleteValue(receipt, viewer));

    public IReadOnlyDictionary<string, object?> CompleteFormal(string viewer)
    {
        var pending = Field(state, "pending", false);
        var model = Field(pending, "model", new List<object>());
        var receipt = new List<object>
        {
            new List<object> { "action_event", Field(pending, "action_event", "") },
            new List<object> { "outcome", "applied" },
            new List<object> { "actual", Field(model, "actual", new List<object>()) },
            new List<object> { "executor", "formal-model" }
        };
        return CompleteDv(receipt, viewer);
    }

    private static Dictionary<string, object> RawFields(object record)
    {
        var fields = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var item in R.Items(R.snapshot(record)))
        {
            var pair = R.Items(item);
            fields[(string)pair[0]] = pair[1];
        }
        return fields;
    }
    private ViewerProjection Projection(string viewer)
    {
        if (!viewers.TryGetValue(viewer, out var cache)) viewers[viewer] = cache = new();
        if (cache.Version == Version) return cache;
        PhaseObserver?.Invoke("snapshot_projection_begin", viewer);
        var dynamic = RawFields(Run(() => DVProgram.DynamicSnapshot(state, viewer, IntentProjection())));
        var map = (string)dynamic["map"];
        var chapter = Convert.ToInt32(dynamic["chapter"]);
        if (cache.Version < 0 || cache.Map != map || cache.Chapter != chapter)
            cache.Static = RawFields(Run(() => DVProgram.StaticSnapshot(state, viewer)));
        var next = new Dictionary<string, object>(cache.Static, StringComparer.Ordinal);
        foreach (var pair in dynamic) next[pair.Key] = pair.Value;
        var values = new Dictionary<string, object?>(next.Count, StringComparer.Ordinal);
        foreach (var pair in next)
        {
            if (cache.Raw.TryGetValue(pair.Key, out var old) && R.Bool(R.same_value(old, pair.Value)))
                values[pair.Key] = cache.Value[pair.Key];
            else values[pair.Key] = HostValues.FromDv(pair.Value);
        }
        cache.Raw = next;
        cache.Value = HostValues.Freeze(values);
        cache.Map = map;
        cache.Chapter = chapter;
        cache.Version = Version;
        PhaseObserver?.Invoke("snapshot_projection_end", viewer);
        return cache;
    }
    public IReadOnlyDictionary<string, object?> Materialize(string viewer) => Projection(viewer).Value;
    public string Snapshot(string viewer) => JsonSerializer.Serialize(Materialize(viewer));
    public HostStateDelta Project(string viewer, bool forceFull = false)
    {
        var cache = Projection(viewer);
        var reset = forceFull || cache.DeliveredVersion < 0 || cache.DeliveredMap != cache.Map;
        var changed = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in cache.Raw)
            if (reset || !cache.Delivered.TryGetValue(pair.Key, out var old) || !R.Bool(R.same_value(old, pair.Value)))
                changed[pair.Key] = cache.Value[pair.Key];
        var removed = reset ? Array.Empty<string>() : cache.Delivered.Keys.Where(key => !cache.Raw.ContainsKey(key)).ToArray();
        var delta = new HostStateDelta(Version, cache.DeliveredVersion, viewer, reset, HostValues.Freeze(changed), Array.AsReadOnly(removed));
        cache.DeliveredVersion = Version;
        cache.DeliveredMap = cache.Map;
        cache.Delivered = cache.Raw;
        return delta;
    }
    public IReadOnlyDictionary<string, object?> ExecutorProjection(string actor)
    {
        if (actor == Viewer) return Materialize(actor);
        if (executors.TryGetValue(actor, out var found) && found.Version == Version) return found.Value;
        var value = HostValues.Record(Run(() => DVProgram.ExecutorSnapshot(state, actor, IntentProjection())));
        executors[actor] = (Version, value);
        return value;
    }

    public IReadOnlyDictionary<string, object?> TextStatus()
    {
        var viewer = Viewer;
        var snapshot = Materialize(viewer);
        var room = HostValues.Text(snapshot, "room");
        var map = HostValues.Text(snapshot, "map");
        var people = HostValues.List(snapshot, "people").OfType<IReadOnlyDictionary<string, object?>>()
            .Where(person => HostValues.Text(person, "room") == room && HostValues.Text(person, "map") == map)
            .Select(person => (object?)HostValues.Freeze(new Dictionary<string, object?>
            {
                ["id"] = HostValues.Text(person, "key"), ["name"] = HostValues.Text(person, "title")
            })).ToArray();
        var observations = new List<object?>();
        var events = Events(state);
        for (var index = events.Count - 1; index >= 0 && observations.Count < 8; index--)
        {
            var entry = events[index];
            if (R.Text(Field(entry, "type", "")) != "observation" || R.Text(Field(entry, "actor", "")) != viewer) continue;
            var data = Field(entry, "data", new List<object>());
            foreach (var observation in R.Items(Field(data, "observations", new List<object>())))
            {
                observations.Add(HostValues.FromDv(observation));
                if (observations.Count == 8) break;
            }
        }
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["viewer"] = viewer, ["version"] = Version, ["chapter"] = snapshot.GetValueOrDefault("chapter"),
            ["game_time"] = snapshot.GetValueOrDefault("game_time"), ["location"] = HostValues.Text(HostValues.Map(snapshot, "hud"), "room"),
            ["room"] = room, ["map"] = map, ["people"] = Array.AsReadOnly(people),
            ["observations"] = observations.AsReadOnly(), ["status"] = HostValues.Text(Intent, "status"),
            ["active"] = Active, ["development_mode"] = DevMode
        });
    }

    public IReadOnlyDictionary<string, object?> Inspect(string actor)
    {
        if (!DevMode) throw new InvalidOperationException("Inspect requires a development session (--dv-dev / --dev).");
        var selected = R.Items(Field(state, "actors", new List<object>())).First(item => R.Text(Field(item, "id", "")) == actor);
        var details = new Dictionary<string, object?>
        {
            ["actor"] = actor, ["version"] = Version, ["development_mode"] = true,
            ["physical"] = HostValues.FromDv(R.snapshot(Field(state, "physical", new List<object>()))),
            ["mind"] = HostValues.FromDv(R.snapshot(Field(selected, "mind", new List<object>()))),
            ["scheduled_action"] = HostValues.FromDv(R.snapshot(Field(selected, "scheduled_action", false))),
            ["erosion"] = HostValues.FromDv(Field(selected, "erosion", false)),
            ["erosion_rank"] = Field(selected, "erosion_rank", 0d),
            ["erosion_stage"] = Field(selected, "erosion_stage", "early"),
            ["erosion_memory_truncated"] = Field(selected, "erosion_memory_truncated", false),
            ["policy"] = HostValues.FromDv(R.snapshot(Field(selected, "policy", new List<object>())))
        };
        var events = Events(state);
        for (var index = events.Count - 1; index >= 0; index--)
        {
            var entry = events[index];
            var kind = R.Text(Field(entry, "type", ""));
            if (R.Text(Field(entry, "actor", "")) == actor && kind is "planning_choice" or "planning_stop")
            {
                details["planning"] = HostValues.FromDv(Field(entry, "data", new List<object>()));
                details["planning_event"] = Field(entry, "id", "");
                break;
            }
        }
        return HostValues.Freeze(details);
    }
    public IReadOnlyDictionary<string, object?> ExecuteDebug(string command, string argument)
    {
        if (!DevMode) throw new InvalidOperationException("Development commands require --dv-dev / --dev.");
        if (HasPending(state)) throw new InvalidOperationException("Finish the current action before a development command.");
        BeginTransition();
        state = Run(() => DVProgram.DevelopmentCommand(state, command, argument));
        RememberCommitted();
        return Materialize(Viewer);
    }

    private object DurableState() => R.record_with(lastCommittedState, "audit_mode", audit);
    public string Save() => JsonSerializer.Serialize(DurableState(), SnapshotOptions);
    public void WriteSave(string path) => R.WriteEvidenceFile(path, DurableState());
    public void Close()
    {
        if (IsClosed) return;
        CancelPrepared();
        closedTrace = R.snapshot(Run(() => DVProgram.SessionClose(state)));
    }
    private object TraceValue() => closedTrace ?? R.snapshot(Run(() => DVProgram.TraceSnapshot(state)));
    public string Trace() => JsonSerializer.Serialize(TraceValue(), SnapshotOptions);
    public void WriteTrace(string path) => R.WriteEvidenceFile(path, TraceValue());

    private static readonly JsonSerializerOptions SnapshotOptions = new()
    {
        MaxDepth = 512, Converters = { new SnapshotExactConverter() }
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
            writer.WriteStartObject(); writer.WriteString("$dv_rational", value.ToString()); writer.WriteEndObject();
        }
    }
}

public static partial class DVProgram
{
    internal static object CampaignOpen() => F_dvg_open();
    internal static object HostOpen(object options) => F_dvg_host_open(options);
    internal static object HostViewer(object state) => F_dvg_host_viewer(state);
    internal static object SetPlayer(object state, object actor) => F_dvs_set_player(state, actor);
    internal static object SelectPlayer(object state, object id) => F_dvs_select_player(state, id);
    internal static object DynamicSnapshot(object state, object viewer, object intent) => F_dvg_snapshot_dynamic(state, viewer, intent);
    internal static object StaticSnapshot(object state, object viewer) => F_dvg_snapshot_static(state, viewer);
    internal static object ExecutorSnapshot(object state, object actor, object intent) => F_dvg_executor_snapshot(state, actor, intent);
    internal static object DevelopmentCommand(object state, object command, object argument) => F_dvg_dev_command(state, command, argument);
    internal static object TraceSnapshot(object state) => F_dvg_trace_snapshot(state);
    internal static object SessionOpen(object scenario) => F_dvs_session_open(scenario, "enabled");
    internal static object SessionActive(object state) => F_dvs_session_active(state);
    internal static object SessionPrepare(object state) => F_dvg_prepare(state);
    internal static object PrepareBackground(object state, object viewer) => F_dvg_prepare_background(state, viewer);
    internal static object SessionComplete(object state, object receipt) => F_dvg_complete(state, receipt);
    internal static object SessionIntent(object state) => F_dvg_intent(state);
    internal static object SessionPresentation(object state, object viewer) => F_dvg_presentation(state, viewer);
    internal static object SessionClose(object state) => F_dvs_session_close(state);
}
