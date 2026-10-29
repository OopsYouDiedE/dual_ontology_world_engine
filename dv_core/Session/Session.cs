using System.Diagnostics;
using DuelView.Compiled;
using DuelView.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>The single state owner for Godot, development commands and the text host.</summary>
public sealed partial class DvSession
{
    private object state;
    private object lastCommittedState;
    private object? intentProjection;
    private IReadOnlyDictionary<string, object?>? nativeIntent;
    private object? closedTrace;
    private int committedDecisionEventCursor;
    private readonly bool audit;
    private readonly bool developer;
    private readonly R.ValueContext valueContext;
    private readonly HostValues.Converter converter = new();
    private ContentCatalog content;
    private readonly Dictionary<string, ViewerProjection> viewers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (long Version, IReadOnlyDictionary<string, object?> Value)> executors = new(StringComparer.Ordinal);
    private PreviewBranch? preview;
    private long? simulationPulseOrigin;
    private long simulationPulseBase;
    public Action<string, string>? PhaseObserver { get; set; }
    public TextWriter DiagnosticOutput { get; set; } = Console.Error;
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

    private DvSession(R.ValueContext context, object value, bool auditMode = true, bool? developerMode = null, ContentCatalog? catalog = null)
    {
        valueContext = context;
        using var valuesScope = R.UseValues(valueContext);
        var embedded = Field(value, "content_pack_dv", "");
        content = catalog ?? (embedded is string { Length: > 0 } text ? CampaignContent.Restore(text) : CampaignContent.Read());
        using var contentScope = R.UseContent(content.Data);
        audit = auditMode;
        var story = R.record_find(value, "story", new List<object>());
        var runConfiguration = Field(story, "run_config", new List<object>());
        var configuredStateIndex = R.Bool(Field(runConfiguration, "opt_stateindex", true));
        var restoreRuntimeConfiguration = !R.RuntimeOptionsFixed && R.Num(R.record_index(story, "run_config")) >= 0;
        if (!restoreRuntimeConfiguration && configuredStateIndex != R.Bool(R.stateindex_enabled()))
            throw new InvalidDataException("State-index runtime differs from saved/configured policy; set --opt-stateindex (or DV_OPT_STATEINDEX) to the recorded value.");
        developer = developerMode ?? R.Bool(R.record_find(story, "development_mode", false));
        story = R.record_with(story, "development_mode", developer);
        state = R.record_with(R.record_with(value, "story", story), "audit_mode", audit);
        state = R.record_with(state, "content_pack_dv", content.Source);
        if (HasPending(state)) throw new InvalidDataException("A restorable session must be saved at a committed action boundary.");
        // New campaigns configure the context before world initialization.
        // Restores first retain the existing content/state/developer/pending
        // validation order, then use the policy recorded in their own pack.
        if (restoreRuntimeConfiguration)
        {
            var normalized = CampaignRuntime.F_dvg_run_config(runConfiguration);
            CampaignRuntime.ConfigureCoreRuntime(normalized);
        }
        R.FixRuntimeOptions();
        lastCommittedState = R.snapshot(state);
        committedDecisionEventCursor = Events(state).Count;
        performance.Configure(audit, developer, R.Text(Field(state, "player_actor", "")),
            R.Items(Field(state, "actors", new List<object>())).Count, PerformanceGameMinute(),
            R.Text(Field(runConfiguration, "simulation_mode", "unrecorded")));
        var savedController = R.Text(Field(value, "performance_controller", ""));
        if (savedController.Length > 0) performance.ConfigureController(savedController);
        UpdateEvidenceLifecycle();
    }

    private object Run(Func<object> operation, bool speculative = false)
    {
        using var valuesScope = R.UseValues(valueContext);
        using var contentScope = R.UseContent(content.Data);
        using var scope = R.UseAudit(audit);
        using var phases = R.UsePhases((stage, actor) =>
        {
            ObservePerformancePhase(stage, actor);
            if (!speculative && PhaseObserver is { } observer && stage is not ("decision-compute-start" or "decision-compute-end"))
            {
                var excludedBefore = performance.DecisionInstrumentationCursor;
                var started = Stopwatch.GetTimestamp();
                try { observer(stage, actor); }
                finally { performance.ExcludeDecisionInstrumentation(Stopwatch.GetTimestamp() - started, excludedBefore); }
            }
        });
        using var recording = R.UseRawRecording(!speculative);
        using var output = R.UseOutput(speculative ? TextWriter.Null : DiagnosticOutput);
        try { return operation(); }
        catch { performance.RecordFailure(); throw; }
    }
    private static object Field(object value, string name, object fallback)
    {
        // Host reads never acquire a mutable branch. Preserve the runtime's
        // last matching slot and original list/index errors, but keep frozen
        // histories and actor graphs shared until an actual DV write copies them.
        var slot = (int)R.Num(R.record_index(value, name));
        if (slot >= 0) return R.Items(R.Items(value)[slot])[1];
        if (fallback is string sentinel && sentinel == "__dv_missing_field_sentinel__")
            throw new Exception("Missing field: " + name);
        return fallback;
    }
    private static bool HasPending(object value) => Field(value, "pending", false) is not false;
    private static IReadOnlyList<object> Events(object value) => R.Items(Field(value, "events", new List<object>()));
    private void RecordCommittedDecisions()
    {
        var events = Events(state);
        var player = R.Text(Field(state, "player_actor", ""));
        while (committedDecisionEventCursor < events.Count)
        {
            var entry = events[committedDecisionEventCursor++];
            ObserveLifecycle(entry);
            if (R.Text(Field(entry, "type", "")) != "action_selected") continue;
            var actor = R.Text(Field(entry, "actor", ""));
            var data = Field(entry, "data", new List<object>());
            var action = Field(data, "action", new List<object>());
            var source = player.Length > 0 && string.Equals(actor, player, StringComparison.Ordinal)
                ? performance.ControllerKind : "engine";
            performance.RecordDecision(source, actor, R.Text(Field(entry, "id", "")),
                R.Text(Field(action, "id", "")), R.Num(Field(entry, "time", PerformanceGameMinute())));
            var work = Field(data, "planning_work", false);
            if (work is not false)
                RecordPerformancePlanning(actor, R.Text(Field(entry, "id", "")), converter.Record(work),
                    R.Text(Field(data, "planning_stop", "")));
        }
    }
    private void BeginTransition()
    {
        if (IsClosed) throw new InvalidOperationException("The session is closed.");
        Version++;
        intentProjection = null;
        nativeIntent = null;
        preview = null;
        executors.Clear();
        ClearObserverProjection();
    }
    private void RememberCommitted()
    {
        if (!HasPending(state))
        {
            lastCommittedState = R.snapshot(state);
            durableVersion = Version;
            durableSelectedCount = selectedCount;
            durableScheduledCount = scheduledCount;
            durableCompletedCount = completedCount;
        }
        UpdateEvidenceLifecycle();
    }

    public static DvSession OpenCampaign(string player = "") =>
        OpenMeasured("open-campaign", context =>
        {
            var catalog = CampaignContent.Read();
            using var contentScope = R.UseContent(catalog.Data);
            return new DvSession(context, CampaignRuntime.SetPlayer(CampaignRuntime.CampaignOpen(), player), catalog: catalog);
        });
    public static DvSession OpenConfiguredCampaign(string options) => OpenConfiguredCampaign(options, Console.Error);
    public static DvSession OpenConfiguredCampaign(string options, TextWriter diagnosticOutput) =>
        OpenMeasured("open-configured", context =>
        {
            var configuration = HostValues.Parse(options);
            var source = Field(configuration, "content_source", "") as string ?? "";
            var path = Field(configuration, "content_path", "") as string ?? "";
            var catalog = CampaignContent.Read(source.Length > 0 ? source : null, path.Length > 0 ? path : null);
            using var contentScope = R.UseContent(catalog.Data);
            var auditMode = R.Bool(Field(configuration, "audit", false));
            var developerMode = R.Bool(Field(configuration, "developer", false));
            var controller = R.Text(Field(configuration, "controller", ""));
            if (controller is "human" or "llm") configuration = R.record_with(configuration, "controller", "player");
            using var scope = R.UseAudit(auditMode);
            using var output = R.UseOutput(diagnosticOutput);
            var session = new DvSession(context, CampaignRuntime.HostOpen(configuration), auditMode, developerMode, catalog) { DiagnosticOutput = diagnosticOutput };
            if (controller is "human" or "llm") session.SetPerformanceController(controller);
            return session;
        });
    public static DvSession Open(string scenario) =>
        OpenMeasured("open-scenario", context => new DvSession(context, CampaignRuntime.SessionOpen(HostValues.Parse(scenario))));
    public static DvSession Restore(string snapshot) => OpenMeasured("restore-value", context =>
    {
        var value = HostValues.Parse(snapshot);
        var catalog = RestoreContent(value);
        return new DvSession(context, value, R.Bool(Field(value, "audit_mode", true)), catalog: catalog);
    });
    public static DvSession RestoreFile(string path) => OpenMeasured("restore-file", context =>
    {
        var value = R.ReadEvidenceFile(path);
        var catalog = RestoreContent(value);
        return new DvSession(context, value, R.Bool(Field(value, "audit_mode", true)), catalog: catalog);
    });

    private static ContentCatalog RestoreContent(object state)
    {
        if (Field(state, "content_pack_dv", "") is not string { Length: > 0 } source)
            throw new InvalidDataException("This saved session does not contain its campaign definitions. Start a new session with the migrated content.");
        return CampaignContent.Restore(source);
    }

    public string Viewer => R.Text(Run(() => CampaignRuntime.HostViewer(R.snapshot(state))));
    public bool Active => !IsClosed && R.Bool(Run(() => CampaignRuntime.SessionActive(R.snapshot(state))));
    public IReadOnlyDictionary<string, object?> Intent
    {
        get
        {
            using var valuesScope = R.UseValues(valueContext);
            return nativeIntent ??= converter.Record(IntentProjection());
        }
    }

    private object IntentProjection()
    {
        if (intentProjection is not null) return intentProjection;
        using var performanceScope = Measure("intent", "projection");
        PhaseObserver?.Invoke("intent_projection_begin", "");
        intentProjection = R.snapshot(Run(() => CampaignRuntime.SessionIntent(R.snapshot(state))));
        PhaseObserver?.Invoke("intent_projection_end", "");
        return intentProjection;
    }

    public IReadOnlyDictionary<string, object?> PrepareValue(string selection = "")
    {
        using var valuesScope = R.UseValues(valueContext);
        if (selection.Length == 0 && (HasPending(state) || R.Bool(Field(state, "awaiting_player", false)))) return Intent;
        using var performanceScope = Measure("prepare");
        BeginTransition();
        if (selection.Length > 0)
        {
            state = Run(() => CampaignRuntime.SelectPlayer(state, SelectionValue(selection)));
            RecordCommittedDecisions();
        }
        using (Measure("prepare-engine"))
            state = Run(() => CampaignRuntime.SessionPrepare(R.snapshot(state)));
        RecordCommittedDecisions();
        RememberCommitted();
        return Intent;
    }
    private static object SelectionValue(string selection) => selection.StartsWith('{') ? HostValues.Parse(selection) : selection;
    public string Prepare(string selection = "") => JsonSerializer.Serialize(PrepareValue(selection));

    /// <summary>Refresh the near-field simulation at a committed idle boundary.
    /// The monotonic wall clock never advances campaign minutes or completes actions.</summary>
    public void SimulationPulse(long seconds, IReadOnlyList<string> visibleRooms)
    {
        using var valuesScope = R.UseValues(valueContext);
        if (IsClosed || HasPending(state)) return;
        if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        using var performanceScope = Measure("simulation-pulse");
        if (simulationPulseOrigin is null)
        {
            simulationPulseOrigin = seconds;
            var physical = Field(state, "physical", new List<object>());
            var simulation = Field(physical, "simulation", new List<object>());
            simulationPulseBase = checked((long)R.Num(Field(simulation, "last_nearby_second", -1d)) + 1);
        }
        if (seconds < simulationPulseOrigin.Value)
            throw new ArgumentOutOfRangeException(nameof(seconds), "The host clock moved backwards.");
        var sessionSecond = checked(simulationPulseBase + seconds - simulationPulseOrigin.Value);
        var previous = R.snapshot(state);
        var advanced = Run(() => CampaignRuntime.SimulationPulse(previous,
            converter.ToDv(sessionSecond), converter.ToDv(visibleRooms)));
        if (ReferenceEquals(previous, advanced)) return;
        BeginTransition();
        state = advanced;
        RememberCommitted();
    }

    public IReadOnlyDictionary<string, object?> PrepareBackgroundValue()
    {
        using var valuesScope = R.UseValues(valueContext);
        if (IsClosed || HasPending(state) || !R.Bool(Field(state, "awaiting_player", false))) return Intent;
        using var performanceScope = Measure("background-prepare");
        var previous = R.snapshot(state);
        var advanced = Run(() => CampaignRuntime.PrepareBackground(previous, Viewer));
        if (!ReferenceEquals(advanced, previous))
        {
            BeginTransition();
            state = advanced;
            RecordCommittedDecisions();
            RememberCommitted();
        }
        return Intent;
    }

    public PreparedPreview PrepareAhead(string selection)
    {
        if (preview is { } saved && saved.Public.BaseVersion == Version && saved.Public.Selection == selection) return saved.Public;
        using var performanceScope = Measure("prepare-ahead", "speculation");
        if (HasPending(state) || !R.Bool(Field(state, "awaiting_player", false)))
            throw new InvalidOperationException("PrepareAhead requires the current player's input boundary.");
        var firstEvent = Events(state).Count;
        var branch = R.copy_value(R.snapshot(state));
        branch = Run(() => CampaignRuntime.SelectPlayer(branch, SelectionValue(selection)), speculative: true);
        branch = Run(() => CampaignRuntime.SessionPrepare(R.snapshot(branch)), speculative: true);
        var intent = converter.Record(Run(() => CampaignRuntime.SessionIntent(R.snapshot(branch)), speculative: true));
        var candidate = new PreparedPreview(Guid.NewGuid().ToString("N"), Version, selection, intent);
        preview = new PreviewBranch(candidate, R.snapshot(branch), firstEvent);
        return candidate;
    }
    public bool TryAdoptPrepared(string token, out IReadOnlyDictionary<string, object?> intent)
    {
        intent = HostValues.Empty;
        if (preview is not { } candidate || candidate.Public.Token != token || candidate.Public.BaseVersion != Version || IsClosed) return false;
        using var performanceScope = Measure("adopt-prepared");
        BeginTransition();
        state = R.copy_value(candidate.State);
        RecordCommittedDecisions();
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

    public IReadOnlyDictionary<string, object?> CompleteValue(string receipt, string viewer)
    {
        using var valuesScope = R.UseValues(valueContext);
        return CompleteDv(HostValues.Parse(receipt), viewer);
    }
    public IReadOnlyDictionary<string, object?> CompleteValue(IReadOnlyDictionary<string, object?> receipt, string viewer)
    {
        using var valuesScope = R.UseValues(valueContext);
        return CompleteDv(converter.ToDv(receipt), viewer);
    }
    private IReadOnlyDictionary<string, object?> CompleteDv(object receipt, string viewer)
    {
        using var performanceScope = Measure("complete");
        var pending = Field(state, "pending", false);
        if (pending is false)
        {
            // Idempotent duplicate completion protection (1.5):
            // Check if this action_event was already completed previously.
            var receiptEvent = Field(receipt, "action_event", false) as string;
            if (receiptEvent is { Length: > 0 })
            {
                var eventsList = Events(state);
                for (var i = eventsList.Count - 1; i >= 0; i--)
                {
                    var entry = eventsList[i];
                    if (R.Text(Field(entry, "type", "")) != "action_execution") continue;
                    var data = Field(entry, "data", new List<object>());
                    var pastActionEvt = R.Text(Field(Field(data, "receipt", new List<object>()), "action_event", ""));
                    var pastEvent = R.Text(Field(entry, "id", ""));
                    if (pastActionEvt == receiptEvent || pastEvent == receiptEvent)
                    {
                        DiagnosticOutput.WriteLine($"[Session.CompleteDv] Duplicate completion ignored idempotently: {receiptEvent}");
                        object duplicateProjected;
                        using (Measure("presentation", "projection"))
                            duplicateProjected = Run(() => CampaignRuntime.SessionPresentation(R.snapshot(state), viewer));
                        var dupPresentation = new Dictionary<string, object?>(converter.Record(duplicateProjected), StringComparer.Ordinal);
                        dupPresentation["outcome"] = "applied";
                        dupPresentation["outcome_category"] = "completed";
                        dupPresentation["action_event"] = receiptEvent;
                        dupPresentation["duplicate_ignored"] = true;
                        return HostValues.Freeze(dupPresentation);
                    }
                }
            }
            throw new InvalidOperationException("Completion belongs to no pending Core action.");
        }
        var action = Field(pending, "action", new List<object>());
        var actionEvent = R.Text(Field(pending, "action_event", ""));
        if (actionEvent.Length == 0 || Field(receipt, "action_event", false) is not string receiptEventMatch
            || !string.Equals(receiptEventMatch, actionEvent, StringComparison.Ordinal))
            throw new InvalidDataException("Completion event does not match the pending Core action.");
        // A native spatial completion is only valid against the exact content pack
        // and state version from which its host binding was issued.
        if (Field(action, "spatial_execution_ref", false) is string
            && Field(receipt, "executor", false) is "godot"
            && Field(receipt, "outcome", false) is "applied")
        {
            var native = Field(receipt, "field", false);
            var bindingVersion = Field(native, "state_version", false);
            if (Field(native, "content_hash", false) is not string hash || hash != content.Hash
                || !R.Bool(R.is_numeric_value(bindingVersion)) || R.Num(bindingVersion) != Version)
                throw new InvalidDataException("Native spatial completion has a stale content hash or prepared state version.");
        }
        var actor = R.Text(Field(pending, "id", ""));
        var actionId = R.Text(Field(action, "id", ""));
        DiagnosticOutput.WriteLine($"[Session.CompleteDv] actor={actor} actionId={actionId}");
        var eventCursor = Events(state).Count;
        BeginTransition();
        state = Run(() => CampaignRuntime.SessionComplete(R.snapshot(state), receipt));
        RecordCommittedDecisions();
        RememberCommitted();
        object projected;
        using (Measure("presentation", "projection"))
            projected = Run(() => CampaignRuntime.SessionPresentation(R.snapshot(state), viewer));
        var presentation = new Dictionary<string, object?>(converter.Record(projected), StringComparer.Ordinal);
        var outcome = "";
        var outcomeCategory = "";
        var failureClass = "";
        var reason = "";
        var target = "";
        var events = Events(state);
        for (var index = eventCursor; index < events.Count; index++)
        {
            var entry = events[index];
            if (R.Text(Field(entry, "type", "")) != "action_execution") continue;
            var data = Field(entry, "data", new List<object>());
            var result = Field(data, "receipt", new List<object>());
            var execution = Field(result, "execution", new List<object>());
            outcome = R.Text(Field(execution, "status", ""));
            outcomeCategory = R.Text(Field(execution, "outcome_category", ""));
            failureClass = R.Text(Field(execution, "failure_class", ""));
            reason = R.Text(Field(execution, "reason", ""));
            target = R.Text(Field(execution, "target", ""));
            break;
        }
        if (outcomeCategory.Length == 0)
            outcomeCategory = outcome == "applied" ? "completed" : "unclassified-failure";
        performance.RecordOutcome(outcome, outcomeCategory);
        presentation["outcome"] = outcome;
        presentation["outcome_category"] = outcomeCategory;
        if (failureClass.Length > 0) presentation["failure_class"] = failureClass;
        if (reason.Length > 0) presentation["reason"] = reason;
        if (target.Length > 0) presentation["target"] = target;
        presentation["action_event"] = actionEvent;
        presentation["completed_actor"] = actor;
        presentation["completed_action"] = actionId;
        return HostValues.Freeze(presentation);
    }
    public string Complete(string receipt, string viewer) => JsonSerializer.Serialize(CompleteValue(receipt, viewer));

    public IReadOnlyDictionary<string, object?> CompleteFormal(string viewer)
    {
        using var valuesScope = R.UseValues(valueContext);
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
        using var performanceScope = Measure("host-snapshot", "projection");
        PhaseObserver?.Invoke("snapshot_projection_begin", viewer);
        var dynamic = RawFields(Run(() => CampaignRuntime.DynamicSnapshot(R.snapshot(state), viewer, IntentProjection())));
        var map = (string)dynamic["map"];
        var chapter = Convert.ToInt32(dynamic["chapter"]);
        if (cache.Version < 0 || cache.Map != map || cache.Chapter != chapter)
            cache.Static = RawFields(Run(() => CampaignRuntime.StaticSnapshot(R.snapshot(state), viewer)));
        var next = new Dictionary<string, object>(cache.Static, StringComparer.Ordinal);
        foreach (var pair in dynamic) next[pair.Key] = pair.Value;
        var values = new Dictionary<string, object?>(next.Count, StringComparer.Ordinal);
        foreach (var pair in next)
        {
            if (cache.Raw.TryGetValue(pair.Key, out var old) && R.Bool(R.same_value(old, pair.Value)))
                values[pair.Key] = cache.Value[pair.Key];
            else values[pair.Key] = converter.FromDv(pair.Value);
        }
        cache.Raw = next;
        cache.Value = HostValues.Freeze(values);
        cache.Map = map;
        cache.Chapter = chapter;
        cache.Version = Version;
        PhaseObserver?.Invoke("snapshot_projection_end", viewer);
        return cache;
    }
    public IReadOnlyDictionary<string, object?> Materialize(string viewer)
    {
        using var valuesScope = R.UseValues(valueContext);
        return Projection(viewer).Value;
    }
    public string Snapshot(string viewer) => JsonSerializer.Serialize(Materialize(viewer));
    public HostStateDelta Project(string viewer, bool forceFull = false)
    {
        using var performanceScope = Measure("host-delta", "projection");
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
        using var performanceScope = Measure("executor-snapshot", "projection");
        var value = converter.Record(Run(() => CampaignRuntime.ExecutorSnapshot(R.snapshot(state), actor, IntentProjection())));
        executors[actor] = (Version, value);
        return value;
    }

    public IReadOnlyDictionary<string, object?> TextStatus()
    {
        using var performanceScope = Measure("text-status", "projection");
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
                observations.Add(converter.FromDv(observation));
                if (observations.Count == 8) break;
            }
        }
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["viewer"] = viewer, ["version"] = Version, ["chapter"] = snapshot.GetValueOrDefault("chapter"),
            ["game_time"] = snapshot.GetValueOrDefault("game_time"), ["location"] = HostValues.Text(snapshot, "location_title"),
            ["room"] = room, ["map"] = map, ["people"] = Array.AsReadOnly(people),
            ["observations"] = observations.AsReadOnly(), ["status"] = HostValues.Text(Intent, "status"),
            ["active"] = Active, ["development_mode"] = DevMode
        });
    }

    public IReadOnlyDictionary<string, object?> Inspect(string actor)
    {
        if (!DevMode) throw new InvalidOperationException("Inspect requires a development session (--dv-dev / --dev).");
        using var performanceScope = Measure("inspect", "projection");
        var selected = R.Items(Field(state, "actors", new List<object>())).First(item => R.Text(Field(item, "id", "")) == actor);
        var details = new Dictionary<string, object?>
        {
            ["actor"] = actor, ["version"] = Version, ["development_mode"] = true,
            ["physical"] = converter.FromDv(R.snapshot(Field(state, "physical", new List<object>()))),
            ["mind"] = converter.FromDv(R.snapshot(Field(selected, "mind", new List<object>()))),
            ["scheduled_action"] = converter.FromDv(R.snapshot(Field(selected, "scheduled_action", false))),
            ["erosion"] = converter.FromDv(Field(selected, "erosion", false)),
            ["erosion_rank"] = Field(selected, "erosion_rank", 0d),
            ["erosion_stage"] = Field(selected, "erosion_stage", "early"),
            ["erosion_memory_truncated"] = Field(selected, "erosion_memory_truncated", false),
            ["policy"] = converter.FromDv(R.snapshot(Field(selected, "policy", new List<object>())))
        };
        var events = Events(state);
        for (var index = events.Count - 1; index >= 0; index--)
        {
            var entry = events[index];
            var kind = R.Text(Field(entry, "type", ""));
            if (R.Text(Field(entry, "actor", "")) == actor && kind is "planning_choice" or "planning_stop")
            {
                details["planning"] = converter.FromDv(Field(entry, "data", new List<object>()));
                details["planning_event"] = Field(entry, "id", "");
                break;
            }
        }
        return HostValues.Freeze(details);
    }
    public IReadOnlyDictionary<string, object?> ExecuteDebug(string command, string argument)
    {
        using var valuesScope = R.UseValues(valueContext);
        if (!DevMode) throw new InvalidOperationException("Development commands require --dv-dev / --dev.");
        if (HasPending(state)) throw new InvalidOperationException("Finish the current action before a development command.");
        using var performanceScope = Measure("development", "development");
        BeginTransition();
        state = Run(() => CampaignRuntime.DevelopmentCommand(state, command, argument));
        committedDecisionEventCursor = Events(state).Count;
        RememberCommitted();
        return Materialize(Viewer);
    }

    private object DurableState()
    {
        var durable = R.record_with(R.record_with(lastCommittedState, "audit_mode", audit), "performance_controller", performance.ControllerKind);
        durable = R.record_with(durable, "archive_provenance", converter.ToDv(Provenance()));
        return R.record_with(durable, "archive_cut", converter.ToDv(EvidenceCut()));
    }
    public string Save()
    {
        using var performanceScope = Measure("save-json", "persistence");
        try { return JsonSerializer.Serialize(DurableState(), SnapshotOptions); }
        catch { performance.RecordFailure(); throw; }
    }
    public void WriteSave(string path)
    {
        using var performanceScope = Measure("save", "persistence");
        try { R.WriteEvidenceFile(path, DurableState()); }
        catch { performance.RecordFailure(); throw; }
    }
    public void Close()
    {
        if (IsClosed) return;
        performance.StopWallClock();
        using var performanceScope = Measure("close", "persistence");
        CancelPrepared();
        CaptureClosingPreview();
        closedTrace = R.snapshot(Run(() => CampaignRuntime.SessionClose(R.snapshot(state))));
        shutdownReason = R.Text(Field(closedTrace, "status", "closed"));
        UpdateEvidenceLifecycle();
    }
    private object TraceValue() => closedTrace ?? R.snapshot(Run(() => CampaignRuntime.TraceSnapshot(R.snapshot(state))));
    public string Trace()
    {
        using var performanceScope = Measure("trace-json", "persistence");
        return JsonSerializer.Serialize(RecordedTraceValue(), SnapshotOptions);
    }
    public void WriteTrace(string path)
    {
        using var performanceScope = Measure("trace", "persistence");
        try { R.WriteEvidenceFile(path, RecordedTraceValue()); }
        catch { performance.RecordFailure(); throw; }
    }

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

public static partial class CampaignRuntime
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
    internal static object SessionOpen(object scenario)
    {
        // Keep the formal dialect rejection first. The Core entry repeats the
        // same pure check and retains every remaining initialization step.
        _ = F_dv_require(R.eq(F_dv_get(scenario, "format"), "dv-session-v1"), "Unsupported story data dialect");
        var configuration = F_dv_find(scenario, "run_config", false);
        if (configuration is not false) ConfigureCoreRuntime(configuration);
        else R.FixRuntimeOptions();
        return F_dvs_session_open(scenario, "enabled");
    }
    internal static object SessionActive(object state) => F_dvs_session_active(state);
    internal static object SessionPrepare(object state) => F_dvg_prepare(state);
    internal static object PrepareBackground(object state, object viewer) => F_dvg_prepare_background(state, viewer);
    internal static object SimulationPulse(object state, object seconds, object rooms) => F_dvg_simulation_pulse(state, seconds, rooms);
    internal static object SessionComplete(object state, object receipt) => F_dvg_complete(state, receipt);
    internal static object SessionIntent(object state) => F_dvg_intent(state);
    internal static object SessionPresentation(object state, object viewer) => F_dvg_presentation(state, viewer);
    internal static object SessionClose(object state) => F_dvs_session_close(state);
}
