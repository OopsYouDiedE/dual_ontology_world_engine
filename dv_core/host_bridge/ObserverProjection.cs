using DuelView.Compiled;

/// <summary>Read-only display projections and recorded host interaction boundaries.</summary>
public sealed partial class DvSession
{
    private long observerVersion = -1;
    private readonly Dictionary<string, IReadOnlyDictionary<string, object?>> observerSnapshots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> observerChoices = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyDictionary<string, object?>> objectiveMaps = new(StringComparer.Ordinal);
    private IReadOnlyList<IReadOnlyDictionary<string, object?>> observerActors = Array.Empty<IReadOnlyDictionary<string, object?>>();
    private readonly List<(int Cursor, object Event)> interactions = new();

    private void ClearObserverProjection()
    {
        observerVersion = -1;
        observerSnapshots.Clear();
        observerChoices.Clear();
        objectiveMaps.Clear();
        observerActors = Array.Empty<IReadOnlyDictionary<string, object?>>();
    }

    private void EnsureObservers()
    {
        if (observerVersion == Version) return;
        // DV projection memoization may replace owned list entries; pin the actor references for this read.
        var actors = R.Items(Field(state, "actors", new List<object>())).ToArray();
        var directory = new List<IReadOnlyDictionary<string, object?>>();
        var intent = IntentProjection();
        foreach (var actor in actors)
        {
            var id = R.Text(Field(actor, "id", ""));
            var value = HostValues.Record(Run(() => DVProgram.ObserverSubject(state, actor, intent), speculative: true));
            observerSnapshots[id] = value;
            // Administrative actor identity is distinct from the names held in each mind.
            directory.Add(HostValues.Freeze(new Dictionary<string, object?>
            { ["id"] = id, ["title"] = HostValues.Text(HostValues.Map(value, "snapshot"), "viewer_title", id) }));
        }
        observerActors = directory.AsReadOnly();
        foreach (var id in observerSnapshots.Keys.ToArray())
        {
            var subject = observerSnapshots[id];
            var snapshot = new Dictionary<string, object?>(HostValues.Map(subject, "snapshot"), StringComparer.Ordinal)
            {
                ["actors"] = observerActors
            };
            observerSnapshots[id] = HostValues.Freeze(new Dictionary<string, object?>(subject, StringComparer.Ordinal)
            {
                ["snapshot"] = HostValues.Freeze(snapshot)
            });
        }
        observerVersion = Version;
    }

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Actors
    {
        get { EnsureObservers(); return observerActors; }
    }

    private IReadOnlyDictionary<string, object?> ObserverBase(string viewer)
    {
        EnsureObservers();
        if (!observerSnapshots.TryGetValue(viewer, out var value)) throw new ArgumentException("未知视角 · " + viewer, nameof(viewer));
        return value;
    }

    public bool IsScheduled(string actor, string actionId)
    {
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

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> ObserverOptions(string viewer)
    {
        _ = ObserverBase(viewer);
        if (observerChoices.TryGetValue(viewer, out var found)) return found;
        var raw = Run(() => DVProgram.ObserverOptions(state, viewer), speculative: true);
        var options = R.Items(raw).Select(HostValues.Record).ToList();
        var actors = R.Items(Field(state, "actors", new List<object>()));
        var actor = actors.First(item => R.Text(Field(item, "id", "")) == viewer);
        var revision = Field(Field(actor, "mind", new List<object>()), "revision", -1d);
        var events = Events(state);
        // Reuse scores that the planner actually produced for this same cognitive revision.
        for (var index = events.Count - 1; index >= 0; index--)
        {
            var entry = events[index];
            if (R.Text(Field(entry, "actor", "")) != viewer || R.Text(Field(entry, "type", "")) != "planning_choice") continue;
            var data = Field(entry, "data", new List<object>());
            var plans = R.Items(Field(data, "plans", new List<object>()));
            var currentPlans = plans.Where(plan => R.Bool(R.same_value(Field(plan, "mind_revision", -2d), revision))).ToArray();
            if (currentPlans.Length == 0) break;
            for (var item = 0; item < options.Count; item++)
            {
                var option = options[item];
                var id = HostValues.Text(option, "id");
                object? score = null;
                foreach (var plan in currentPlans)
                {
                    var path = R.Items(Field(plan, "path", new List<object>()));
                    if (path.Count != 1 || R.Text(path[0]) != id) continue;
                    score = Field(plan, "score", false);
                    break;
                }
                if (score is null)
                {
                    options[item] = HostValues.Freeze(new Dictionary<string, object?>(option, StringComparer.Ordinal)
                    {
                        ["planning_status"] = "unexpanded", ["planning_event"] = Field(entry, "id", "")
                    });
                    continue;
                }
                var scored = new Dictionary<string, object?>(option, StringComparer.Ordinal)
                {
                    ["ai_score"] = HostValues.FromDv(score), ["score_kind"] = "recorded-root",
                    ["score_event"] = Field(entry, "id", ""), ["planning_status"] = "evaluated-root"
                };
                options[item] = HostValues.Freeze(scored);
            }
            break;
        }
        if (HostValues.Text(Intent, "status") == "prepared" && HostValues.Text(Intent, "actor") == viewer)
        {
            var action = HostValues.Map(Intent, "action");
            var id = HostValues.Text(action, "id");
            if (id.Length > 0 && !options.Any(option => HostValues.Text(option, "id") == id))
            {
                var label = HostValues.Text(action, "label");
                var kind = HostValues.Text(action, "kind");
                var original = R.Items(Field(actor, "actions", new List<object>()))
                    .FirstOrDefault(candidate => R.Text(Field(candidate, "id", "")) == id);
                if (label.Length == 0 && original is not null) label = R.Text(Field(original, "label", ""));
                var selected = new Dictionary<string, object?>
                {
                    ["id"] = id, ["label"] = label.Length > 0 && label != id ? label : "（未记录标签）",
                    ["kind"] = kind, ["available"] = true, ["selected"] = true,
                    ["cost"] = HostValues.FromDv(Field(Field(Field(state, "costs", new List<object>()), kind, new List<object>()), "actual", new List<object>()))
                };
                for (var index = events.Count - 1; index >= 0; index--)
                {
                    var entry = events[index];
                    if (R.Text(Field(entry, "actor", "")) != viewer || R.Text(Field(entry, "type", "")) != "action_selected") continue;
                    var data = Field(entry, "data", new List<object>());
                    if (R.Text(Field(Field(data, "action", new List<object>()), "id", "")) == id)
                    {
                        var recordedLabel = R.Text(Field(Field(data, "action", new List<object>()), "label", Field(data, "label", "")));
                        if (recordedLabel.Length > 0 && recordedLabel != id) selected["label"] = recordedLabel;
                        var score = Field(data, "score", false);
                        if (score is not false)
                        {
                            selected["ai_score"] = HostValues.FromDv(score);
                            selected["score_kind"] = "recorded-choice";
                            selected["score_event"] = Field(entry, "id", "");
                        }
                    }
                    break;
                }
                options.Add(HostValues.Freeze(selected));
            }
        }
        var result = options.AsReadOnly();
        observerChoices[viewer] = result;
        return result;
    }

    public IReadOnlyDictionary<string, object?> TextStatus(string viewer)
    {
        var snapshot = HostValues.Map(ObserverBase(viewer), "snapshot");
        var subjective = HostValues.Map(ObserverBase(viewer), "subjective");
        var room = HostValues.Text(snapshot, "room");
        var map = HostValues.Text(snapshot, "map");
        var people = HostValues.List(subjective, "rooms").OfType<IReadOnlyDictionary<string, object?>>()
            .Where(item => HostValues.Text(item, "map") == map && HostValues.Text(item, "room") == room)
            .SelectMany(item => HostValues.List(item, "members").OfType<IReadOnlyDictionary<string, object?>>())
            .Where(item => HostValues.Text(item, "kind") == "person" && item.GetValueOrDefault("is_viewer") is not true)
            .Select(item => (object?)HostValues.Freeze(new Dictionary<string, object?>
            { ["id"] = HostValues.Text(item, "id"), ["name"] = HostValues.Text(item, "title") })).ToArray();
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["viewer"] = viewer, ["viewer_name"] = HostValues.Text(snapshot, "viewer_title", viewer), ["version"] = Version,
            ["chapter"] = snapshot.GetValueOrDefault("chapter"), ["game_time"] = snapshot.GetValueOrDefault("game_time"),
            ["location"] = HostValues.Text(snapshot, "location_title"), ["room"] = room, ["map"] = map,
            ["people"] = Array.AsReadOnly(people), ["status"] = HostValues.Text(Intent, "status"),
            ["active"] = Active, ["development_mode"] = DevMode
        });
    }

    public IReadOnlyDictionary<string, object?> Preview(string viewer, bool includeObjective = false)
    {
        var selected = ObserverBase(viewer);
        var options = ObserverOptions(viewer);
        var observers = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in observerSnapshots)
        {
            var entry = new Dictionary<string, object?>(pair.Value, StringComparer.Ordinal)
            {
                ["status"] = TextStatus(pair.Key),
                ["options"] = observerChoices.TryGetValue(pair.Key, out var choices)
                    ? choices : Array.Empty<IReadOnlyDictionary<string, object?>>()
            };
            observers[pair.Key] = HostValues.Freeze(entry);
        }
        var result = new Dictionary<string, object?>(selected, StringComparer.Ordinal)
        {
            ["viewer"] = viewer, ["version"] = Version, ["status"] = TextStatus(viewer),
            ["options"] = options, ["actors"] = observerActors, ["observers"] = HostValues.Freeze(observers)
        };
        if (includeObjective)
        {
            if (!objectiveMaps.TryGetValue(viewer, out var objective))
            {
                objective = HostValues.Record(Run(() => DVProgram.ObserverObjective(state, viewer), speculative: true));
                objectiveMaps[viewer] = objective;
            }
            result["objective"] = objective;
        }
        return HostValues.Freeze(result);
    }

    public void RecordInteraction(IReadOnlyDictionary<string, object?> payload)
    {
        if (IsClosed) throw new InvalidOperationException("会话已关闭");
        var recorded = HostValues.ToDv(HostValues.Freeze(new Dictionary<string, object?>
        {
            ["id"] = "host-interaction-" + interactions.Count,
            ["type"] = "cli_interaction", ["actor"] = HostValues.Text(payload, "actor"),
            ["time"] = Field(Field(state, "physical", new List<object>()), "time", 0d),
            ["data"] = payload
        }));
        interactions.Add((Events(state).Count, recorded));
        using var scope = R.UseAudit(audit);
        R.raw_event(recorded);
    }

    private object RecordedTraceValue()
    {
        var trace = TraceValue();
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
    internal static object ObserverSubject(object state, object actor, object intent) => F_dvg_preview_subject(state, actor, intent);
    internal static object ObserverObjective(object state, object viewer) => F_dvg_preview_objective(state, viewer);
    internal static object ObserverOptions(object state, object viewer) => F_dvg_observer_options(state, viewer);
}
