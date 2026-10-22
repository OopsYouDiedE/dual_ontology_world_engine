using DuelView.Compiled;

/// <summary>Read-only display projections and recorded host interaction boundaries.</summary>
public sealed partial class DvSession
{
    private long observerVersion = -1;
    private readonly Dictionary<string, IReadOnlyDictionary<string, object?>> observerSnapshots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> observerChoices = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, object?>? objectiveMap;
    private readonly Dictionary<string, object> observerSubjects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyDictionary<string, object?>> observerPreviews = new(StringComparer.Ordinal);
    private IReadOnlyList<IReadOnlyDictionary<string, object?>> observerActors = Array.Empty<IReadOnlyDictionary<string, object?>>();
    private readonly List<(int Cursor, object Event)> interactions = new();
    private string lastPreviewViewer = "";
    private bool lastPreviewObjective, lastPreviewImmediateScores;
    private IReadOnlyDictionary<string, object?>? closingPreview;
    private long closingPreviewVersion;

    private void ClearObserverProjection()
    {
        observerVersion = -1;
        observerSnapshots.Clear();
        observerChoices.Clear();
        objectiveMap = null;
        observerSubjects.Clear();
        observerPreviews.Clear();
        observerActors = Array.Empty<IReadOnlyDictionary<string, object?>>();
    }

    private void EnsureObservers()
    {
        if (observerVersion == Version) return;
        using var performanceScope = Measure("observer-directory", "projection", projectionScope: "actor-directory");
        observerPreviews.Clear();
        // Pin only actor identities here. Opening a directory must not project every mind.
        var actors = R.Items(Field(state, "actors", new List<object>())).ToArray();
        foreach (var actor in actors)
        {
            var id = R.Text(Field(actor, "id", ""));
            observerSubjects[id] = actor;
        }
        observerActors = Array.AsReadOnly(R.Items(Run(() => DVProgram.ObserverDirectory(R.snapshot(state)), speculative: true))
            .Select(converter.Record).ToArray());
        observerVersion = Version;
    }

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Actors
    {
        get
        {
            using var valuesScope = R.UseValues(valueContext);
            EnsureObservers();
            return observerActors;
        }
    }

    private IReadOnlyDictionary<string, object?> ObserverBase(string viewer)
    {
        EnsureObservers();
        if (observerSnapshots.TryGetValue(viewer, out var value)) return value;
        if (!observerSubjects.TryGetValue(viewer, out var actor)) throw new ArgumentException("未知视角 · " + viewer, nameof(viewer));
        using var performanceScope = Measure("observer-subject", "projection", viewer: viewer, projectionScope: "one-subject");
        var projected = converter.Record(Run(() => DVProgram.ObserverSubject(R.snapshot(state), R.snapshot(actor), IntentProjection()), speculative: true));
        var snapshot = new Dictionary<string, object?>(HostValues.Map(projected, "snapshot"), StringComparer.Ordinal)
        {
            ["actors"] = observerActors, ["state_version"] = Version
        };
        value = HostValues.Freeze(new Dictionary<string, object?>(projected, StringComparer.Ordinal)
        {
            ["snapshot"] = HostValues.Freeze(snapshot)
        });
        observerSnapshots[viewer] = value;
        return value;
    }

    public bool IsScheduled(string actor, string actionId)
    {
        using var valuesScope = R.UseValues(valueContext);
        var actors = R.Items(Field(state, "actors", new List<object>()));
        for (var index = 0; index < actors.Count; index++)
        {
            var subject = actors[index];
            if (R.Text(Field(subject, "id", "")) != actor) continue;
            var scheduled = Field(subject, "scheduled_action", false);
            if (scheduled is false) return false;
            var action = Field(scheduled, "action", new List<object>());
            return R.Text(Field(action, "id", "")) == actionId;
        }
        return false;
    }

    private int observerEventCursor;
    private readonly Dictionary<(string Actor, string Type), object> observerEvents = new();

    private object? LatestObserverEvent(string actor, string type)
    {
        var events = Events(state);
        while (observerEventCursor < events.Count)
        {
            var entry = events[observerEventCursor++];
            var kind = R.Text(Field(entry, "type", ""));
            if (kind is "planning_choice" or "action_selected")
                observerEvents[(R.Text(Field(entry, "actor", "")), kind)] = entry;
        }
        return observerEvents.GetValueOrDefault((actor, type));
    }

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> ObserverOptions(string viewer, bool includeImmediateScores = false)
    {
        using var valuesScope = R.UseValues(valueContext);
        EnsureObservers();
        if (!observerSubjects.TryGetValue(viewer, out var actor)) throw new ArgumentException("未知视角 · " + viewer, nameof(viewer));
        var cacheKey = viewer + (includeImmediateScores ? "|immediate" : "|recorded");
        if (observerChoices.TryGetValue(cacheKey, out var found)) return found;
        using var performanceScope = Measure("observer-options", "projection", viewer: viewer, projectionScope: "one-subject-options");
        var raw = Run(() => DVProgram.ObserverOptions(R.snapshot(state), viewer, includeImmediateScores), speculative: true);
        var options = R.Items(raw).Select(converter.Record).ToList();
        var revision = Field(Field(actor, "mind", new List<object>()), "revision", -1d);
        var planning = LatestObserverEvent(viewer, "planning_choice");
        if (planning is not null)
        {
            var data = Field(planning, "data", new List<object>());
            var plans = R.Items(Field(data, "plans", new List<object>()));
            var currentPlans = plans.Where(plan => R.Bool(R.same_value(Field(plan, "mind_revision", -2d), revision))).ToArray();
            if (currentPlans.Length > 0)
                for (var item = 0; item < options.Count; item++)
                {
                    var option = options[item];
                    var id = HostValues.Text(option, "id");
                    var root = currentPlans.FirstOrDefault(plan =>
                    {
                        var path = R.Items(Field(plan, "path", new List<object>()));
                        return path.Count == 1 && R.Text(path[0]) == id;
                    });
                    var scored = new Dictionary<string, object?>(option, StringComparer.Ordinal)
                    {
                        ["immediate_score"] = option.GetValueOrDefault("immediate_score") ?? option.GetValueOrDefault("ai_score"),
                        ["immediate_breakdown"] = option.GetValueOrDefault("immediate_breakdown") ?? option.GetValueOrDefault("score_breakdown"),
                        ["planning_status"] = root is null ? "unexpanded" : "evaluated-root",
                        ["planning_event"] = Field(planning, "id", "")
                    };
                    if (root is not null)
                    {
                        var score = converter.FromDv(Field(root, "score", false));
                        scored["root_score"] = score;
                        scored["root_event"] = Field(planning, "id", "");
                        scored["root_game_time"] = Field(planning, "time", false);
                        scored["root_mind_revision"] = converter.FromDv(revision);
                        scored["ai_score"] = score;
                        scored["score_kind"] = "recorded-root";
                        scored["score_event"] = Field(planning, "id", "");
                        scored["score_breakdown"] = HostValues.Freeze(new Dictionary<string, object?>
                        {
                            ["scope"] = "recorded-root", ["total"] = score,
                            ["source_event"] = Field(planning, "id", ""), ["components_recorded"] = false
                        });
                    }
                    options[item] = HostValues.Freeze(scored);
                }
        }
        var intent = Intent;
        var prepared = HostValues.Text(intent, "status") == "prepared" && HostValues.Text(intent, "actor") == viewer;
        var scheduled = Field(actor, "scheduled_action", false);
        var selectedAction = prepared ? HostValues.Map(intent, "action") :
            scheduled is not false ? converter.Record(Field(scheduled, "action", new List<object>())) : HostValues.Empty;
        var selectedId = HostValues.Text(selectedAction, "id");
        if (selectedId.Length > 0)
        {
            var selectedIndex = options.FindIndex(option => HostValues.Text(option, "id") == selectedId);
            var original = R.Items(Field(actor, "actions", new List<object>()))
                .FirstOrDefault(candidate => R.Text(Field(candidate, "id", "")) == selectedId);
            var label = HostValues.Text(selectedAction, "label");
            if (label.Length == 0 && original is not null) label = R.Text(Field(original, "label", ""));
            var kind = HostValues.Text(selectedAction, "kind");
            var selected = selectedIndex >= 0 ? new Dictionary<string, object?>(options[selectedIndex], StringComparer.Ordinal) :
                new Dictionary<string, object?>
                {
                    ["id"] = selectedId, ["label"] = label.Length > 0 && label != selectedId ? label : "（未记录标签）",
                    ["kind"] = kind, ["available"] = false,
                    ["cost"] = converter.FromDv(Field(Field(Field(state, "costs", new List<object>()), kind, new List<object>()), "actual", new List<object>()))
                };
            selected["selected"] = true;
            selected["action_state"] = prepared ? "prepared" : "scheduled";
            var chosen = LatestObserverEvent(viewer, "action_selected");
            if (chosen is not null)
            {
                var data = Field(chosen, "data", new List<object>());
                if (R.Text(Field(Field(data, "action", new List<object>()), "id", "")) == selectedId)
                {
                    var score = Field(data, "score", false);
                    var recordedLabel = R.Text(Field(Field(data, "action", new List<object>()), "label", Field(data, "label", "")));
                    if (recordedLabel.Length > 0 && recordedLabel != selectedId) selected["label"] = recordedLabel;
                    selected["selection_event"] = Field(chosen, "id", "");
                    selected["selection_game_time"] = Field(chosen, "time", false);
                    var recordedPath = Field(data, "selected_path", false);
                    if (recordedPath is not false) selected["selection_path"] = converter.FromDv(recordedPath);
                    if (score is not false)
                    {
                        selected["selection_score"] = converter.FromDv(score);
                        selected["selection_score_kind"] = "recorded-path";
                        if (!selected.ContainsKey("ai_score") || selected["ai_score"] is null or false)
                        {
                            selected["ai_score"] = converter.FromDv(score);
                            selected["score_kind"] = "recorded-choice";
                            selected["score_event"] = Field(chosen, "id", "");
                        }
                        var plan = R.Items(Field(data, "plans", new List<object>())).FirstOrDefault(candidate =>
                        {
                            var path = R.Items(Field(candidate, "path", new List<object>()));
                            return path.Count > 0 && R.Text(path[0]) == selectedId &&
                                R.Bool(R.same_value(Field(candidate, "score", false), score));
                        });
                        if (plan is not null)
                        {
                            selected["selection_path"] = converter.FromDv(Field(plan, "path", new List<object>()));
                            selected["selection_mind_revision"] = converter.FromDv(Field(plan, "mind_revision", false));
                        }
                    }
                }
            }
            if (selectedIndex >= 0) options[selectedIndex] = HostValues.Freeze(selected);
            else options.Add(HostValues.Freeze(selected));
        }
        var result = options.AsReadOnly();
        observerChoices[cacheKey] = result;
        return result;
    }

    public IReadOnlyDictionary<string, object?> TextStatus(string viewer)
    {
        using var valuesScope = R.UseValues(valueContext);
        var projected = ObserverBase(viewer);
        var snapshot = HostValues.Map(projected, "snapshot");
        var subjective = HostValues.Map(projected, "subjective");
        var room = HostValues.Text(snapshot, "room");
        var map = HostValues.Text(snapshot, "map");
        var people = HostValues.List(subjective, "rooms").OfType<IReadOnlyDictionary<string, object?>>()
            .Where(item => HostValues.Text(item, "map") == map && HostValues.Text(item, "room") == room)
            .SelectMany(item => HostValues.List(item, "members").OfType<IReadOnlyDictionary<string, object?>>())
            .Where(item => HostValues.Text(item, "kind") is "person" or "actor" && item.GetValueOrDefault("is_viewer") is not true)
            .Select(item => (object?)HostValues.Freeze(new Dictionary<string, object?>
            { ["id"] = HostValues.Text(item, "id"), ["name"] = HostValues.Text(item, "title") })).ToArray();
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["viewer"] = viewer, ["viewer_name"] = HostValues.Text(snapshot, "viewer_title", "（未记录名称）"), ["version"] = Version,
            ["chapter"] = snapshot.GetValueOrDefault("chapter"), ["game_time"] = snapshot.GetValueOrDefault("game_time"),
            ["location"] = HostValues.Text(snapshot, "location_title"), ["room"] = room, ["map"] = map,
            ["people"] = Array.AsReadOnly(people), ["status"] = HostValues.Text(Intent, "status"),
            ["active"] = Active, ["development_mode"] = DevMode
        });
    }

    private IReadOnlyDictionary<string, object?> ObjectiveForViewer(string viewer)
    {
        if (objectiveMap is null)
        {
            using var scope = Measure("observer-objective", "projection", projectionScope: "shared-physical-world");
            objectiveMap = converter.Record(Run(() => DVProgram.ObserverObjective(R.snapshot(state), ""), speculative: true));
        }
        var rooms = HostValues.List(objectiveMap, "rooms").OfType<IReadOnlyDictionary<string, object?>>()
            .Select(room => (object?)HostValues.Freeze(new Dictionary<string, object?>(room, StringComparer.Ordinal)
            {
                ["members"] = Array.AsReadOnly(HostValues.List(room, "members").OfType<IReadOnlyDictionary<string, object?>>()
                    .Select(member => (object?)HostValues.Freeze(new Dictionary<string, object?>(member, StringComparer.Ordinal)
                    {
                        ["is_viewer"] = HostValues.Text(member, "id") == viewer
                    })).ToArray())
            })).ToArray();
        return HostValues.Freeze(new Dictionary<string, object?>(objectiveMap, StringComparer.Ordinal)
        {
            ["rooms"] = Array.AsReadOnly(rooms), ["viewer"] = viewer
        });
    }

    public IReadOnlyDictionary<string, object?> Preview(string viewer, bool includeObjective = false, bool includeImmediateScores = false)
    {
        using var valuesScope = R.UseValues(valueContext);
        lastPreviewViewer = viewer;
        lastPreviewObjective = includeObjective;
        lastPreviewImmediateScores = includeImmediateScores;
        var selected = ObserverBase(viewer);
        var cacheKey = viewer + (includeObjective ? "|objective" : "|subjective") + (includeImmediateScores ? "|immediate" : "|recorded");
        if (!observerPreviews.TryGetValue(cacheKey, out var cached))
        {
            Dictionary<string, object?> result;
            using (Measure("observer-preview", "projection", viewer: viewer, projectionScope: "requested-view"))
            {
                var options = ObserverOptions(viewer, includeImmediateScores);
                var status = TextStatus(viewer);
                var observation = HostValues.Freeze(new Dictionary<string, object?>(selected, StringComparer.Ordinal)
                {
                    ["status"] = status, ["options"] = options
                });
                result = new Dictionary<string, object?>(observation, StringComparer.Ordinal)
                {
                    ["viewer"] = viewer, ["version"] = Version, ["actors"] = observerActors,
                    ["projection_scope"] = includeObjective ? "viewer-and-objective" : "viewer-only",
                    ["recorded_viewers"] = Array.AsReadOnly(new object?[] { viewer }),
                    ["observers"] = HostValues.Freeze(new Dictionary<string, object?> { [viewer] = observation })
                };
                if (includeObjective) result["objective"] = ObjectiveForViewer(viewer);
            }
            cached = HostValues.Freeze(result);
            observerPreviews[cacheKey] = cached;
        }
        // Projection is cached; wall-clock measurements always use the current cut.
        return WithPerformance(cached, PerformanceSummary());
    }

    private void CaptureClosingPreview()
    {
        // This is an observation at the actual final state, never a reuse of
        // an earlier result frame. Close must not advance the world afterwards.
        if (lastPreviewViewer.Length == 0) return;
        closingPreview = Preview(lastPreviewViewer, lastPreviewObjective, lastPreviewImmediateScores);
        closingPreviewVersion = Version;
    }

    public static IReadOnlyDictionary<string, object?> WithPerformance(IReadOnlyDictionary<string, object?> value,
        IReadOnlyDictionary<string, object?> performance) =>
        HostValues.Freeze(new Dictionary<string, object?>(value, StringComparer.Ordinal)
        {
            ["performance"] = performance, ["measured_until_ticks"] = performance.GetValueOrDefault("measured_until_ticks")
        });

    public void RecordInteraction(IReadOnlyDictionary<string, object?> payload)
    {
        using var valuesScope = R.UseValues(valueContext);
        if (IsClosed) throw new InvalidOperationException("会话已关闭");
        var recorded = R.snapshot(converter.ToDv(HostValues.Freeze(new Dictionary<string, object?>
        {
            ["id"] = "host-interaction-" + interactions.Count,
            ["type"] = "cli_interaction", ["actor"] = HostValues.Text(payload, "actor"),
            ["time"] = Field(Field(state, "physical", new List<object>()), "time", 0d),
            ["data"] = payload
        })));
        interactions.Add((Events(state).Count, recorded));
        using var scope = R.UseAudit(audit);
        R.raw_event(recorded);
    }

    private object RecordedTraceValue()
    {
        var trace = DecorateTrace(TraceValue());
        if (interactions.Count == 0) return trace;
        var events = Events(trace);
        var recorded = new List<object>(events.Count + interactions.Count);
        var next = 0;
        for (var index = 0; index <= events.Count; index++)
        {
            while (next < interactions.Count && interactions[next].Cursor <= index)
                recorded.Add(interactions[next++].Event);
            if (index < events.Count) recorded.Add(events[index]);
        }
        while (next < interactions.Count) recorded.Add(interactions[next++].Event);
        return R.record_with(trace, "events", recorded);
    }
}

public static partial class DVProgram
{
    internal static object ObserverDirectory(object state) => F_dvg_observer_directory(state);
    internal static object ObserverSubject(object state, object actor, object intent) => F_dvg_preview_subject(state, actor, intent);
    internal static object ObserverObjective(object state, object viewer) => F_dvg_preview_objective(state, viewer);
    internal static object ObserverOptions(object state, object viewer, object includeImmediateScores) => F_dvg_observer_options(state, viewer, includeImmediateScores);
}
