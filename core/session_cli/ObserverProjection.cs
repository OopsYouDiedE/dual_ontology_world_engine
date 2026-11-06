// @mechanism: causal_clock_and_campaign
// @layer: core/session_cli
// @contracts: HostStateDelta

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Numerics;
using DuelView.Dv;
using DuelView.Core;
using DuelView.Compiled;
using DuelView.Runtime;
using DuelView.Spatial;
using DuelView.Autonomy;
using static DuelView.Core.DVKernel;
using static DuelView.Compiled.R;


// --- Source: ObserverProjection.cs ---

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
        observerActors = Array.AsReadOnly(R.Items(Run(() => CampaignRuntime.ObserverDirectory(R.snapshot(state)), speculative: true))
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
        var projected = converter.Record(Run(() => CampaignRuntime.ObserverSubject(R.snapshot(state), R.snapshot(actor), IntentProjection()), speculative: true));
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
        var raw = Run(() => CampaignRuntime.ObserverOptions(R.snapshot(state), viewer, includeImmediateScores), speculative: true);
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
            if (original is not null) label = R.Text(Run(() => CampaignRuntime.ObserverActionLabel(R.snapshot(state), viewer, original), speculative: true));
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
                    if (original is null && recordedLabel.Length > 0 && recordedLabel != selectedId) selected["label"] = recordedLabel;
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
            objectiveMap = converter.Record(Run(() => CampaignRuntime.ObserverObjective(R.snapshot(state), ""), speculative: true));
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

public static partial class CampaignRuntime
{
    internal static object ObserverActionLabel(object state, object viewer, object action) => F_dvg_observer_action_label(state, viewer, action);
    internal static object ObserverDirectory(object state) => F_dvg_observer_directory(state);
    internal static object ObserverSubject(object state, object actor, object intent) => F_dvg_preview_subject(state, actor, intent);
    internal static object ObserverObjective(object state, object viewer) => F_dvg_preview_objective(state, viewer);
    internal static object ObserverOptions(object state, object viewer, object includeImmediateScores) => F_dvg_observer_options(state, viewer, includeImmediateScores);
}
// --- Source: CampaignProjection.cs ---

// Locations, labels, identity rules and cards enter through campaign content.
// Projection exposes existing state and never grants observations.
public static partial class CampaignRuntime
{
    public static object F_dvg_self_purpose(object action)
    {
        var kind = R.Text(F_dv_get(action, "kind"));
        if (R.Bool(F_dv_find(action, "known_opportunity_wait", false)) || kind == "wait") return "wait";
        return kind is "replay" or "infer" ? "reasoning" : "personal";
    }

    public static object F_dvg_intent(object state)
    {
        var intent = F_dvs_session_intent(state);
        var status = R.Text(F_dv_get(intent, "status"));
        if (status == "awaiting_input")
        {
            var options = new List<object>();
            var actor = R.at(F_dv_get(state, "actors"), F_dv_get(state, "cursor"));
            foreach (var option in R.Items(F_dv_get(intent, "options")))
            {
                var action = F_dvp_find_action(F_dv_get(actor, "actions"), F_dv_get(option, "id"));
                var kind = F_dv_get(action, "kind");
                var offered = F_dv_with_many(option, CampaignRecord(
                    ("label", F_dvg_observer_action_label(state, F_dv_get(actor, "id"), action)),
                    ("kind", kind), ("interaction", F_dvg_interaction(action)),
                    ("cost", F_dv_get(F_dv_get(F_dv_get(state, "costs"), kind), "actual"))));
                if (R.Text(kind) == "replay")
                    offered = F_dv_with(offered, "replay_input", F_dvg_manual_replay_input(actor, action));
                options.Add(offered);
            }
            return F_dv_with(intent, "options", options);
        }
        if (status != "prepared") return intent;
        var viewer = F_dv_find(state, "player_actor", "");
        if (R.Text(viewer) != "")
        {
            var action = F_dv_get(intent, "action");
            intent = F_dv_with(intent, "action", F_dv_with(action, "label", F_dvg_observer_action_label(state, viewer, action)));
        }
        var preparedAction = F_dv_get(intent, "action");
        // The prepared state retains the complete action for Core settlement.
        // Hosts execute identities and spatial contracts, not logical premises,
        // rules, private choices or inference trees. Those trees can legitimately
        // exceed Godot's container recursion limit even in the opening chapter.
        var hostAction = new List<object>();
        foreach (var field in new[] { "id", "kind", "label", "travel", "interruptible", "spatial_execution_ref", "presentation_hold", "background_activity", "case_computation_required" })
            if (R.Bool(F_dv_has_key(preparedAction, field)))
                hostAction.Add(F_dv_pair(field, F_dv_get(preparedAction, field)));
        intent = F_dv_with(intent, "action", hostAction);
        var travel = F_dv_find(preparedAction, "travel", new List<object>());
        var hasStation = R.Bool(F_dv_has_key(travel, "station"));
        if (R.Text(F_dv_get(preparedAction, "kind")) != "move" && !hasStation) return intent;
        var target = F_dv_find(travel, "to", "");
        var place = F_dvg_place(target);
        var destination = F_dv_with_many(place, CampaignRecord(
            ("execution_room", F_dv_get(place, "room")), ("logical_location", target)));
        if (hasStation) destination = F_dv_with(destination, "departure_station", F_dv_get(travel, "station"));
        return F_dv_with(intent, "destination", destination);
    }

    public static object F_dvg_person_motion(object actor, object now)
    {
        var queued = F_dv_find(actor, "scheduled_action", false);
        if (queued is false) return false;
        var action = F_dv_get(queued, "action");
        if (R.Text(F_dv_get(action, "kind")) != "move") return false;
        var travel = F_dv_find(action, "travel", false);
        if (travel is false) return false;
        var from = F_dv_get(travel, "from");
        var to = F_dv_get(travel, "to");
        var origin = F_dvg_place(from);
        var destination = F_dvg_place(to);
        if (R.Text(F_dv_get(origin, "map")) == "" || R.Text(F_dv_get(destination, "map")) == "") return false;
        var start = F_dv_get(queued, "start");
        var due = F_dv_get(queued, "due");
        object progress = 0d;
        if (R.Bool(R.lt(start, due)))
            progress = F_dv_min(1d, F_dv_max(0d, R.div(R.sub(now, start), R.sub(due, start))));
        return CampaignRecord(
            ("action_id", F_dv_get(action, "id")),
            ("from", CampaignRecord(("logical_location", from), ("map", F_dv_get(origin, "map")), ("execution_room", F_dv_get(origin, "room")))),
            ("to", CampaignRecord(("logical_location", to), ("map", F_dv_get(destination, "map")), ("execution_room", F_dv_get(destination, "room")))),
            ("start", start), ("due", due), ("now", now), ("progress", progress),
            ("scope", "host-presentation-only-awaits-formal-receipt"));
    }
}
// --- Source: HostValues.cs ---

/// <summary>Immutable process-boundary values shared by the game and text hosts.</summary>
public static class HostValues
{
    public static readonly IReadOnlyDictionary<string, object?> Empty =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

    public static object? FromDv(object value) => new Converter().FromDv(value);
    public static IReadOnlyDictionary<string, object?> Record(object value) => new Converter().Record(value);

    /// <summary>A session-owned bridge. Only frozen DV values have cross-call identity.</summary>
    public sealed class Converter
    {
        private sealed class Box(object value) { internal readonly object Value = value; }
        private readonly ConditionalWeakTable<object, Box> values = new();
        private readonly ConditionalWeakTable<object, Box> records = new();
        private readonly ConditionalWeakTable<object, Box> origins = new();

        public object? FromDv(object value)
        {
            if (value is Rational exact)
            {
                if (values.TryGetValue(value, out var known)) return known.Value;
                var rational = Freeze(new Dictionary<string, object?> { ["$dv_rational"] = exact.ToString() });
                values.Add(value, new Box(rational));
                origins.Add(rational, new Box(value));
                return rational;
            }
            if (!R.TryItems(value, out _)) return value;
            var frozen = R.snapshot(value);
            if (values.TryGetValue(frozen, out var shared)) return shared.Value;
            var items = R.Items(frozen);
            if (items.Count > 0 && items.All(item => R.TryItems(item, out var pair) && pair.Count == 2 && pair[0] is string))
            {
                var record = Record(frozen);
                values.Add(frozen, new Box(record));
                return record;
            }
            var converted = new object?[items.Count];
            for (var i = 0; i < items.Count; i++) converted[i] = FromDv(items[i]);
            var list = Array.AsReadOnly(converted);
            values.Add(frozen, new Box(list));
            origins.Add(list, new Box(frozen));
            return list;
        }

        public IReadOnlyDictionary<string, object?> Record(object value)
        {
            var frozen = R.snapshot(value);
            if (records.TryGetValue(frozen, out var shared)) return (IReadOnlyDictionary<string, object?>)shared.Value;
            var items = R.Items(frozen);
            var result = new Dictionary<string, object?>(items.Count, StringComparer.Ordinal);
            foreach (var item in items)
            {
                var pair = R.Items(item);
                result[(string)pair[0]] = FromDv(pair[1]);
            }
            var map = Freeze(result);
            records.Add(frozen, new Box(map));
            origins.Add(map, new Box(frozen));
            return map;
        }

        public object ToDv(object? value) => Write(value, new Dictionary<object, object>(ReferenceEqualityComparer.Instance));

        private object Write(object? value, Dictionary<object, object> converted)
        {
            if (value is null) return false;
            if (value is string or bool or double or Rational) return value;
            if (value is int integer) return (double)integer;
            if (value is long longInteger) return (double)longInteger;
            if (value is float single) return (double)single;
            if (value is JsonElement element) return ReadJson(element);
            // These host nodes were created by this bridge from immutable DV
            // nodes. Reuse the original graph without re-encoding its children.
            if (origins.TryGetValue(value, out var origin)) return origin.Value;
            if (converted.TryGetValue(value, out var previous)) return previous;
            if (value is IReadOnlyDictionary<string, object?> map)
            {
                if (map.TryGetValue("$dv_rational", out var exact)) return Rational.From((string)exact!);
                var result = new List<object>(map.Count);
                converted.Add(value, result);
                foreach (var pair in map) result.Add(new List<object> { pair.Key, Write(pair.Value, converted) });
                return result;
            }
            if (value is IDictionary<string, object?> dictionary)
            {
                var result = new List<object>(dictionary.Count);
                converted.Add(value, result);
                foreach (var pair in dictionary) result.Add(new List<object> { pair.Key, Write(pair.Value, converted) });
                return result;
            }
            if (value is IEnumerable sequence)
            {
                var result = new List<object>();
                converted.Add(value, result);
                foreach (var item in sequence) result.Add(Write(item, converted));
                return result;
            }
            throw new ArgumentException("Unsupported host value: " + value.GetType().Name);
        }
    }

    public static IReadOnlyDictionary<string, object?> Freeze(Dictionary<string, object?> values) =>
        new ReadOnlyDictionary<string, object?>(values);

    public static object ToDv(object? value) => new Converter().ToDv(value);

    public static object Parse(string text)
    {
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 512 });
        return ReadJson(document.RootElement);
    }

    private static object ReadJson(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object when value.TryGetProperty("$dv_rational", out var exact) => Rational.From(exact.GetString()!),
        JsonValueKind.Object => value.EnumerateObject().Select(pair => (object)new List<object> { pair.Name, ReadJson(pair.Value) }).ToList(),
        JsonValueKind.Array => value.EnumerateArray().Select(ReadJson).ToList(),
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw new JsonException("Unsupported DV value")
    };

    public static string Text(IReadOnlyDictionary<string, object?> value, string key, string fallback = "") =>
        value.TryGetValue(key, out var found) ? found as string ?? fallback : fallback;
    public static double Number(IReadOnlyDictionary<string, object?> value, string key, double fallback = 0) =>
        value.TryGetValue(key, out var found) && found is IConvertible number ? number.ToDouble(CultureInfo.InvariantCulture) : fallback;
    public static IReadOnlyDictionary<string, object?> Map(IReadOnlyDictionary<string, object?> value, string key) =>
        value.TryGetValue(key, out var found) && found is IReadOnlyDictionary<string, object?> map ? map : Empty;
    public static IReadOnlyList<object?> List(IReadOnlyDictionary<string, object?> value, string key) =>
        value.TryGetValue(key, out var found) && found is IReadOnlyList<object?> list ? list : Array.Empty<object?>();
}

public sealed record HostStateDelta(long Version, long BaseVersion, string Viewer, bool Reset,
    IReadOnlyDictionary<string, object?> Changed, IReadOnlyList<string> Removed);

public sealed record PreparedPreview(string Token, long BaseVersion, string Selection,
    IReadOnlyDictionary<string, object?> Intent);
// --- Source: AuthoredValues.cs ---

public static partial class CampaignRuntime
{
    private static object Authored(string id)
    {
        var definition = R.content("data", id);
        if (definition is false) throw new InvalidDataException("Missing campaign data: " + id);
        return F_dv_get(definition, "value");
    }

    private static object AuthoredCopy(string id) => R.copy_value(Authored(id));
}
