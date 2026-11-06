// @mechanism: causal_clock_and_campaign
// @layer: core/session_cli
// @contracts: SmartReplay

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


// --- Source: SmartReplay.cs ---

public sealed record ReplayFrame(long Sequence, string Kind, string Actor, string Action, string Text,
    double DurationSeconds, IReadOnlyDictionary<string, object?> Data);

/// <summary>A presentation clock: recorded wall time never affects playback.</summary>
public sealed class SmartReplay
{
    public const string Contract = "dv-replay-v2";
    public const string InteractionContract = "dv-interaction-v1";
    private readonly IReadOnlyList<ReplayFrame> frames;
    private double elapsed;
    public IReadOnlyList<ReplayFrame> Frames => frames;
    public int Index { get; private set; } = -1;
    public int Count => frames.Count;
    public ReplayFrame? Current => Index >= 0 && Index < Count ? frames[Index] : null;
    public bool Playing { get; private set; }
    public double Speed { get; private set; } = 1;
    private SmartReplay(IReadOnlyList<ReplayFrame> values) => frames = values;
    private sealed class ReadContext
    {
        internal CancellationToken Cancellation;
        internal long RecordIndex = -1;
        internal string Viewer = "detective";
        internal bool ViewerRecorded;
        internal ContentCatalog? Content;
        internal string ContentSource = "";
        internal double? LastInputRealUs;
        internal readonly Dictionary<string, object> Receipts = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, Interaction> Interactions = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> Confirmed = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, object> Selections = new(StringComparer.Ordinal);
        internal readonly Dictionary<object, object?> NativeValues = new(ReferenceEqualityComparer.Instance);
    }

    private sealed class Interaction
    {
        internal int Focus = -1;
        internal int Confirm = -1;
        internal bool Finished;
        internal bool HasExecution;
    }

    public static SmartReplay Load(string path, CancellationToken cancellationToken = default)
    {
        using var valuesScope = R.UseValues(new R.ValueContext());
        using var contentScope = R.UseContent(new List<object>());
        using var recordingScope = R.UseRawRecording(false);
        using var phasesScope = R.UsePhases(null);
        var frames = new List<ReplayFrame>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var context = new ReadContext { Cancellation = cancellationToken };
        foreach (var value in ReplayJournal.Read(path, cancellationToken, record => BindRecordedContent(record, context)))
            Append(value, frames, seen, context);
        cancellationToken.ThrowIfCancellationRequested();
        return new SmartReplay(frames.AsReadOnly());
    }
    public void Play(double speed = 1)
    {
        if (speed is not (1 or 2 or 4 or 8)) throw new ArgumentOutOfRangeException(nameof(speed), "Replay speed must be 1, 2, 4 or 8.");
        Speed = speed;
        Playing = Count > 0 && Index < Count - 1;
    }
    public void Pause() => Playing = false;
    public void Restart() { Index = -1; elapsed = 0; Playing = false; }
    public ReplayFrame? Step()
    {
        if (Index + 1 >= Count) { Playing = false; return null; }
        elapsed = 0;
        return frames[++Index];
    }
    public IReadOnlyList<ReplayFrame> Advance(double seconds)
    {
        if (seconds < 0 || !double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (!Playing) return Array.Empty<ReplayFrame>();
        var delivered = new List<ReplayFrame>();
        elapsed += seconds * Speed;
        if (Index < 0)
        {
            delivered.Add(frames[++Index]);
            if (frames[Index].DurationSeconds > 0) { elapsed = 0; return delivered; }
        }
        // A slow renderer must not consume both focus and confirmation in one update.
        // Zero-duration metadata is bounded as well, keeping pause/input responsive.
        while (Index < Count && delivered.Count < 256)
        {
            var duration = frames[Index].DurationSeconds;
            if (elapsed < duration) break;
            elapsed -= duration;
            if (Index + 1 == Count) { Playing = false; break; }
            delivered.Add(frames[++Index]);
            if (frames[Index].DurationSeconds > 0) { elapsed = 0; break; }
        }
        return delivered;
    }

    private static object? Get(object? value, string key)
    {
        if (value is IReadOnlyDictionary<string, object?> map) return map.GetValueOrDefault(key);
        if (value is not null && R.TryItems(value, out var items))
        {
            for (var index = items.Count - 1; index >= 0; index--)
                if (R.TryItems(items[index], out var pair) && pair.Count == 2 && pair[0] is string name && name == key) return pair[1];
        }
        return null;
    }
    private static string Text(object? value, string key) => Get(value, key) as string ?? "";

    private static void BindRecordedContent(object? record, ReadContext context)
    {
        if (record is null) return;
        var pending = new Queue<object>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        pending.Enqueue(record);
        while (pending.Count > 0)
        {
            var candidate = pending.Dequeue();
            if (!visited.Add(candidate)) continue;
            var source = Text(candidate, "content_pack_dv");
            if (source.Length > 0)
            {
                if (source == context.ContentSource) return;
                var catalog = ContentCatalog.Parse(source);
                // Legacy recordings remain playable from their stored host
                // projections and events. Missing content is never read from disk.
                context.Content = catalog.DataCount == 0 ? null : CampaignContent.ValidatePack(catalog);
                context.ContentSource = source;
                return;
            }
            foreach (var key in new[] { "record", "state", "last_live_session", "trace" })
                if (Get(candidate, key) is { } nested) pending.Enqueue(nested);
        }
    }

    private static object Project(ReadContext context, Func<object> projection)
    {
        var catalog = context.Content ?? throw new InvalidOperationException("Replay has no recorded campaign content pack.");
        using var contentScope = R.UseContent(catalog.Data);
        return projection();
    }
    private static IEnumerable<object?> Items(object? value) => value switch
    {
        IReadOnlyList<object?> list => list,
        _ => Array.Empty<object?>()
    };
    private static object? ConvertNative(object? value, ReadContext context)
    {
        context.Cancellation.ThrowIfCancellationRequested();
        if (value is null) return null;
        if (value is Rational exact) return HostValues.FromDv(exact);
        if (context.NativeValues.TryGetValue(value, out var cached)) return cached;
        object? Native(object? item) => ConvertNative(item, context);
        object? Keep(object? converted) { context.NativeValues[value] = converted; return converted; }
        if (value is IReadOnlyDictionary<string, object?> map)
            return Keep(HostValues.Freeze(map.ToDictionary(pair => pair.Key, pair => Native(pair.Value), StringComparer.Ordinal)));
        if (R.TryItems(value, out var values))
        {
            if (values.Count > 0 && values.All(item => R.TryItems(item, out var pair) && pair.Count == 2 && pair[0] is string))
            {
                var record = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var item in values)
                {
                    var pair = R.Items(item);
                    record[(string)pair[0]] = Native(pair[1]);
                }
                return Keep(HostValues.Freeze(record));
            }
            return Keep(Array.AsReadOnly(values.Select(Native).ToArray()));
        }
        if (value is IReadOnlyList<object?> items) return Keep(Array.AsReadOnly(items.Select(Native).ToArray()));
        return value;
    }
    private static string Lines(object? lines) => string.Join("\n", Items(lines).Select(item =>
        item is string text ? text : Text(item, "text") is { Length: > 0 } line ? line : Text(item, "surface")).Where(text => text.Length > 0));

    private static void Append(object value, List<ReplayFrame> frames, HashSet<string> seen, ReadContext context)
    {
        object? Native(object? item) => ConvertNative(item, context);
        context.Cancellation.ThrowIfCancellationRequested();
        BindRecordedContent(value, context);
        context.RecordIndex++;
        var type = Text(value, "type");
        if (type.Length == 0) type = Text(value, "kind");
        if (type.Length == 0) type = Text(value, "event");
        var interactionPayload = Get(value, "data") ?? Get(value, "payload") ?? value;
        if (type is "cli_interaction" or "interaction" or "host_interaction" ||
            Text(interactionPayload, "kind") == "cli_interaction" ||
            Text(interactionPayload, "interaction_id").Length > 0 &&
            Text(interactionPayload, "phase") is "focus" or "confirm" or "result" or "cancel")
        {
            AppendInteraction(interactionPayload, value, frames, seen, context);
            return;
        }
        if (type is "" or "trace" or "session_trace" && (Get(value, "trace") is { } || Get(value, "events") is { }))
        {
            var trace = Get(value, "trace") ?? value;
            var initial = Get(trace, "initial");
            if (initial is not null) AppendCheckpoint(new Dictionary<string, object?>
            {
                ["checkpoint"] = "initial", ["record"] = initial
            }, frames, context);
            foreach (var entry in Items(Get(trace, "events")))
                if (entry is not null) Append(entry, frames, seen, context);
            var final = Get(trace, "final") ?? Get(value, "last_live_session");
            if (final is not null) AppendCheckpoint(new Dictionary<string, object?>
            {
                ["checkpoint"] = "final", ["record"] = new Dictionary<string, object?> { ["state"] = final, ["trace"] = trace }
            }, frames, context);
            return;
        }
        if (type == "replay_checkpoint")
        {
            AppendCheckpoint(value, frames, context);
            return;
        }
        if (type == "debug_events")
        {
            var batch = Get(value, "batch");
            var entries = Get(batch, "events") ?? batch;
            foreach (var entry in Items(entries)) if (entry is not null) Append(entry, frames, seen, context);
            return;
        }
        if (type is "state" or "snapshot" or "replay_snapshot" || Get(value, "snapshot") is not null)
        {
            var snapshot = Get(value, "snapshot") ?? value;
            var viewer = Text(snapshot, "viewer");
            if (viewer.Length > 0) context.Viewer = viewer;
            Add(frames, "snapshot", context.Viewer, "", "已记录现场", new Dictionary<string, object?>
            {
                ["snapshot"] = Native(snapshot), ["source"] = "recorded-host-state"
            }, value, context, 0);
            return;
        }
        if (type == "player_presentation")
        {
            var presentation = Get(value, "presentation");
            if (presentation is not null) AppendPresentation(presentation, value, frames, seen, context);
            return;
        }
        if (type is "dv_present" or "presentation")
        {
            AppendPresentation(Get(value, "presentation") ?? value, value, frames, seen, context);
            return;
        }
        var id = Text(value, "id");
        if (id.Length == 0) id = Text(value, "event_id");
        if (id.Length > 0 && !seen.Add(type + ":" + id)) return;
        var actor = Text(value, "actor");
        var data = Get(value, "data");
        if (type == "action_selected" && data is not null)
        {
            var selectedId = Text(Get(data, "action"), "id");
            if (selectedId.Length > 0) context.Selections[InteractionKey(actor, selectedId)] = value;
        }
        if (type == "host_completion" && data is not null)
        {
            context.Receipts[id] = data;
            Add(frames, "event", actor, "", type, new Dictionary<string, object?>
            {
                ["event_id"] = id, ["event_type"] = type, ["receipt"] = Native(data),
                ["action_event"] = Get(data, "action_event"), ["source"] = "recorded-host-completion"
            }, value, context, 0);
            return;
        }
        if (type == "action_execution")
        {
            var projected = data is null;
            var action = Get(data, "action");
            var actionId = projected ? Text(value, "action_id") : Text(action, "id");
            var kind = projected ? Text(value, "action_kind") : Text(action, "kind");
            var speech = Get(action, "speech");
            var status = projected ? Text(Get(value, "detail"), "status") : Text(Get(Get(data, "receipt"), "execution"), "status");
            var text = projected ? Text(value, "label") : Text(action, "surface");
            if (text.Length == 0) text = Text(speech, "surface");
            if (text.Length == 0) text = Text(action, "title");
            if (text.Length == 0) text = actionId;
            var dialogue = kind is "talk" or "question" or "receive_question" or "answer" or "opening" || speech is not null && speech is not false;
            var beat = kind == "move" ? "move" : dialogue ? "dialogue" : "investigate";
            if (status.Length > 0 && status != "applied") { beat = "rejected"; text = actionId + " · " + status; }
            var travel = Get(action, "travel");
            var detail = new Dictionary<string, object?>
            {
                ["event_id"] = id, ["game_time"] = Native(Get(value, projected ? "game_time" : "time")), ["status"] = status,
                ["actor"] = actor, ["action_id"] = actionId, ["action_kind"] = kind,
                ["source"] = projected ? "recorded-event-projection" : "recorded-authority-event", ["travel"] = Native(travel),
                ["action"] = Native(action)
            };
            if (projected) detail["detail"] = Native(Get(value, "detail"));
            foreach (var parent in Items(Get(value, "parents")))
            {
                if (parent is not string parentId || !context.Receipts.TryGetValue(parentId, out var receipt)) continue;
                detail["receipt"] = Native(receipt);
                detail["action_event"] = Get(receipt, "action_event");
                if (Get(Get(receipt, "field"), "actual_position") is { } actual)
                    detail["actual_position"] = Native(actual);
                break;
            }
            var destination = Text(travel, "to");
            if (destination.Length > 0 && status == "applied" && context.Content is not null)
                detail["position"] = HostValues.FromDv(Project(context, () => CampaignRuntime.ReplayPlace(destination)));
            detail["result_kind"] = beat;
            if (TryConfirmation(actor, actionId, context, out var interaction))
            {
                MergeConfirmation(frames, interaction, detail, value, text, context);
                interaction.HasExecution = true;
            }
            else
            {
                // Old recordings identify the action, but do not prove which alternatives
                // were displayed or how they were scored. Keep those fields absent.
                var interactionId = "recorded-" + context.RecordIndex.ToString(CultureInfo.InvariantCulture);
                context.Selections.TryGetValue(InteractionKey(actor, actionId), out var selection);
                var selectionData = Get(selection, "data");
                var label = Text(Get(selectionData, "action"), "label");
                if (label.Length == 0) label = Text(selectionData, "label");
                // Historical projected execution.label could contain the result surface.
                // Only an action's explicit intent label can describe the preceding focus.
                if (label.Length == 0 && !projected) label = Text(action, "label");
                if (label.Length == 0 || label == actionId) label = "（未记录标签）";
                var focus = new Dictionary<string, object?>
                {
                    ["phase"] = "focus", ["interaction_id"] = interactionId,
                    ["interaction_contract"] = InteractionContract, ["selected"] = actionId,
                    ["options"] = Array.Empty<object?>(), ["source"] = "recorded-action-derived",
                    ["selection_recorded"] = false, ["action_id"] = actionId,
                    ["label"] = label, ["label_event"] = Text(selection, "id"),
                    ["selection_game_time"] = Native(Get(selection, "time")),
                    ["game_time"] = Native(Get(value, projected ? "game_time" : "time"))
                };
                Add(frames, "focus", actor, actionId, label, focus, value, context, 0.45);
                detail["phase"] = status == "applied" ? "completed" : status.Length > 0 ? "rejected" : "confirm";
                detail["action_state"] = detail["phase"];
                detail["option_time_basis"] = "decision-boundary";
                detail["selection_game_time"] = Native(Get(selection, "time"));
                detail["result_game_time"] = detail["game_time"];
                detail["interaction_id"] = interactionId;
                detail["interaction_contract"] = InteractionContract;
                detail["selected"] = actionId;
                detail["options"] = Array.Empty<object?>();
                detail["selection_recorded"] = false;
                Add(frames, "confirm", actor, actionId, text, detail, value, context,
                    beat == "move" ? 0.3 : beat == "dialogue" ? Math.Clamp(text.Length / 24d, 0.8, 2.0) : 0.6);
                context.Interactions[interactionId] = new Interaction
                {
                    Focus = frames.Count - 2, Confirm = frames.Count - 1, HasExecution = true
                };
                context.Confirmed[InteractionKey(actor, actionId)] = interactionId;
            }
            return;
        }
        var device = Text(value, "device");
        if (device.Length > 0 || type is "select" or "act" or "dev_command" or "dv_complete" or "new" or "load" or "quit")
        {
            var action = Text(value, "choice_id");
            if (action.Length == 0) action = Text(value, "command");
            var input = new Dictionary<string, object?>
            {
                ["input"] = Native(value), ["source"] = "recorded-input", ["device"] = device
            };
            if (Get(value, "receipt") is { } receipt) input["receipt"] = Native(receipt);
            if (Get(value, "real_us") is IConvertible recordedTime)
            {
                var realUs = recordedTime.ToDouble(CultureInfo.InvariantCulture);
                if (context.LastInputRealUs is { } previous)
                    input["recorded_interval_seconds"] = (realUs - previous) / 1_000_000;
                context.LastInputRealUs = realUs;
            }
            var label = action.Length > 0 ? action : device.Length > 0 ? device : type;
            var duration = device == "host_configuration" || Get(value, "pressed") is false && Get(value, "bodies") is null ? 0 : 0.3;
            Add(frames, "input", actor.Length > 0 ? actor : context.Viewer, action, label, input, value, context, duration);
            return;
        }
        if (Get(value, "texts") is { } texts)
        {
            var text = Lines(texts);
            Add(frames, "visible", actor, "", text, new Dictionary<string, object?>
            {
                ["visible"] = Native(value), ["panel"] = Native(Get(value, "panel")),
                ["texts"] = Native(texts), ["source"] = "recorded-ui-content"
            }, value, context, text.Length == 0 ? 0 : Math.Clamp(text.Length / 24d, 0.8, 2.0));
            return;
        }
        // Keep non-presentation records available to the replay consumer. Their
        // original payload is never promoted to a player observation or command.
        Add(frames, "event", actor, "", type.Length > 0 ? type : "记录", new Dictionary<string, object?>
        {
            ["event_id"] = id, ["event_type"] = type,
            ["game_time"] = Native(Get(value, "time") ?? Get(value, "game_time")),
            ["details"] = Native(data ?? Get(value, "detail") ?? value), ["source"] = "recorded-event"
        }, value, context, type is "session_stopped" or "chapter_content_available" or "development_command" ? 0.6 : 0);
    }

    private static string InteractionKey(string actor, string action) => actor + "\u001f" + action;

    private static bool TryConfirmation(string actor, string action, ReadContext context, out Interaction interaction,
        bool allowExecuted = false)
    {
        if (context.Confirmed.TryGetValue(InteractionKey(actor, action), out var id) &&
            context.Interactions.TryGetValue(id, out var match) && match.Confirm >= 0 && !match.Finished &&
            (allowExecuted || !match.HasExecution))
        {
            interaction = match;
            return true;
        }
        interaction = null!;
        return false;
    }

    private static IReadOnlyList<object?> RecordedOptions(object? value, string actor, ReadContext context)
    {
        var options = value as IReadOnlyList<object?> ?? Array.Empty<object?>();
        object?[]? normalized = null;
        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            if (option is not IReadOnlyDictionary<string, object?> fields) continue;
            var id = Text(option, "id");
            var label = Text(option, "label");
            if (!string.IsNullOrWhiteSpace(label) && label != id) continue;
            context.Selections.TryGetValue(InteractionKey(actor, id), out var selection);
            var data = Get(selection, "data");
            label = Text(Get(data, "action"), "label");
            if (string.IsNullOrWhiteSpace(label)) label = Text(data, "label");
            var recorded = !string.IsNullOrWhiteSpace(label) && label != id;
            var fieldsWithLabel = new Dictionary<string, object?>(fields, StringComparer.Ordinal)
            {
                ["label"] = recorded ? label : "（未记录标签）"
            };
            if (recorded) fieldsWithLabel["label_event"] = Text(selection, "id");
            normalized ??= options.ToArray();
            normalized[index] = HostValues.Freeze(fieldsWithLabel);
        }
        // Native conversion already froze this list. Preserve its shared identity
        // unless a legacy intent label actually needs repair; never cache repairs
        // against the changing recorded-selection context.
        return normalized is null ? options : Array.AsReadOnly(normalized);
    }

    private static IReadOnlyDictionary<string, object?> RecordedPreview(IReadOnlyDictionary<string, object?> value,
        string actor, ReadContext context)
    {
        var viewer = Text(value, "viewer");
        if (viewer.Length > 0) actor = viewer;
        Dictionary<string, object?>? normalized = null;
        void Set(string key, object? replacement)
        {
            normalized ??= new Dictionary<string, object?>(value, StringComparer.Ordinal);
            normalized[key] = replacement;
        }
        if (value.TryGetValue("options", out var options))
        {
            var recorded = RecordedOptions(options, actor, context);
            if (!ReferenceEquals(options, recorded)) Set("options", recorded);
        }
        if (value.GetValueOrDefault("observers") is IReadOnlyDictionary<string, object?> observers)
        {
            Dictionary<string, object?>? replacements = null;
            foreach (var pair in observers)
            {
                if (pair.Value is not IReadOnlyDictionary<string, object?> observer) continue;
                var recorded = RecordedPreview(observer, pair.Key, context);
                if (ReferenceEquals(observer, recorded)) continue;
                replacements ??= new Dictionary<string, object?>(observers, StringComparer.Ordinal);
                replacements[pair.Key] = recorded;
            }
            if (replacements is not null) Set("observers", HostValues.Freeze(replacements));
        }
        return normalized is null ? value : HostValues.Freeze(normalized);
    }

    private static void AppendInteraction(object payload, object record, List<ReplayFrame> frames,
        HashSet<string> seen, ReadContext context)
    {
        object? Native(object? item) => ConvertNative(item, context);
        context.Cancellation.ThrowIfCancellationRequested();
        var phase = Text(payload, "phase");
        var id = Text(payload, "interaction_id");
        var viewer = Text(payload, "viewer");
        if (viewer.Length > 0) { context.Viewer = viewer; context.ViewerRecorded = true; }
        var actor = Text(payload, "actor");
        if (actor.Length == 0) actor = Text(payload, "viewer");
        var action = Text(payload, "action_id");
        if (action.Length == 0) action = Text(payload, "choice_id");
        if (action.Length == 0) action = Text(payload, "selected");
        var data = Native(payload) is IReadOnlyDictionary<string, object?> converted
            ? new Dictionary<string, object?>(converted, StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal);
        data["input_record_index"] = context.RecordIndex;
        data["source"] = "recorded-interaction";
        data["interaction_contract"] = InteractionContract;
        data["selected"] = phase == "cancel" ? "" : action;
        data["phase"] = phase;
        data["options"] = RecordedOptions(data.GetValueOrDefault("options"), actor, context);
        var selectedOption = Items(data["options"]).FirstOrDefault(option => Text(option, "id") == action);
        if (!data.ContainsKey("selection_game_time"))
            data["selection_game_time"] = Get(selectedOption, "selection_game_time") ?? data.GetValueOrDefault("game_time");
        if (!data.ContainsKey("selection_state_version"))
            data["selection_state_version"] = data.GetValueOrDefault("source_state_version") ?? data.GetValueOrDefault("state_version");
        data["option_time_basis"] = "decision-boundary";
        if (data.GetValueOrDefault("preview") is IReadOnlyDictionary<string, object?> preview)
            data["preview"] = RecordedPreview(preview, actor, context);
        if (Get(Get(payload, "preview"), "snapshot") is not null)
        {
            data.Remove("snapshot");
            data["snapshot_ref"] = "preview.snapshot";
        }
        var title = Items(data["options"])
            .Where(option => Text(option, "id") == action)
            .Select(option => Text(option, "label"))
            .FirstOrDefault(text => text.Length > 0 && text != action);
        if (title is null && Text(payload, "label") is { Length: > 0 } recordedLabel && recordedLabel != action)
            title = recordedLabel;
        title ??= "（未记录标签）";
        if (id.Length == 0) id = "input-" + context.RecordIndex.ToString(CultureInfo.InvariantCulture);
        data["interaction_id"] = id;
        if (!context.Interactions.TryGetValue(id, out var interaction))
            context.Interactions[id] = interaction = new Interaction();
        if (phase == "result")
        {
            if (interaction.Confirm < 0)
            {
                data["source"] = "recorded-orphan-result";
                Add(frames, "event", actor, action, "", data, record, context, 0);
                return;
            }
            var presentation = Get(payload, "presentation");
            var text = Get(presentation, "visible") is false ? "" : PresentationText(presentation);
            if (presentation is not null)
            {
                var presentationId = Text(presentation, "presentation_id");
                if (presentationId.Length > 0) seen.Add("presentation:" + presentationId);
            }
            MergeConfirmation(frames, interaction, data, record, text, context);
            interaction.Finished = true;
            context.Confirmed.Remove(InteractionKey(actor, action));
            return;
        }
        if (phase == "focus")
        {
            Add(frames, "focus", actor, action, title, data, record, context, 0.45);
            interaction.Focus = frames.Count - 1;
        }
        else if (phase == "confirm")
        {
            data["focus_recorded"] = interaction.Focus >= 0;
            Add(frames, "confirm", actor, action, title, data, record, context, 0.6);
            interaction.Confirm = frames.Count - 1;
            context.Confirmed[InteractionKey(actor, action)] = id;
        }
        else if (phase == "cancel")
        {
            interaction.Finished = true;
            context.Confirmed.Remove(InteractionKey(actor, action));
            Add(frames, "cancel", actor, action, title, data, record, context, 0.2);
        }
        else Add(frames, "event", actor, action, "", data, record, context, 0);
    }

    private static void MergeConfirmation(List<ReplayFrame> frames, Interaction interaction,
        IReadOnlyDictionary<string, object?> result, object record, string text, ReadContext context)
    {
        var confirmation = frames[interaction.Confirm];
        var data = new Dictionary<string, object?>(confirmation.Data, StringComparer.Ordinal);
        if (!data.ContainsKey("selection_game_time")) data["selection_game_time"] = data.GetValueOrDefault("game_time");
        if (!data.ContainsKey("selection_state_version"))
            data["selection_state_version"] = data.GetValueOrDefault("source_state_version") ?? data.GetValueOrDefault("state_version");
        // Keep the chosen alternatives and source version from the focus boundary.
        // Only the actual recorded result may replace the post-confirmation view.
        var projectedKeys = new[] { "preview", "snapshot", "snapshot_ref", "presentation", "viewer", "presentation_viewer", "position", "actual_position", "travel", "action", "detail",
            "receipt", "status", "game_time", "result_kind", "action_event", "state_version", "event_id",
            "performance", "controller_kind", "controller", "measurement_cut", "provenance", "lifecycle" };
        foreach (var key in projectedKeys)
            if (result.TryGetValue(key, out var value)) data[key] = value;
        if (Get(Get(data, "preview"), "snapshot") is not null)
        {
            data.Remove("snapshot");
            data["snapshot_ref"] = "preview.snapshot";
        }
        var references = HostValues.List(data, "result_record_indices").ToList();
        references.Add(context.RecordIndex);
        data["result_record_indices"] = references.AsReadOnly();
        data["result"] = HostValues.Freeze(result.Where(pair =>
            !projectedKeys.Contains(pair.Key, StringComparer.Ordinal) &&
            pair.Key is not ("options" or "input" or "result" or "result_record" or "result_records"))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        data["result_record_index"] = context.RecordIndex;
        var outcome = Text(Get(result, "presentation"), "outcome");
        if (outcome.Length == 0) outcome = Text(result, "status");
        if (outcome.Length == 0) outcome = Text(Get(Get(result, "receipt"), "execution"), "status");
        var actionState = outcome switch
        {
            "applied" or "completed" => "completed", "scheduled" => "scheduled",
            "" => Text(data, "action_state") is { Length: > 0 } recordedState ? recordedState : "confirm",
            _ => "rejected"
        };
        data["phase"] = actionState;
        data["action_state"] = actionState;
        data["option_time_basis"] = "decision-boundary";
        data["view_time_basis"] = "recorded-result";
        if (result.TryGetValue("game_time", out var resultTime)) data["result_game_time"] = resultTime;
        if (result.TryGetValue("state_version", out var resultVersion)) data["result_state_version"] = resultVersion;
        if (text.Length == 0) text = confirmation.Text;
        var duration = Text(data, "result_kind") == "move" ? 0.3 : Math.Clamp(text.Length / 24d, 0.6, 2.0);
        frames[interaction.Confirm] = confirmation with { Text = text, DurationSeconds = duration, Data = HostValues.Freeze(data) };
    }

    private static string PresentationText(object? value)
    {
        var lines = Lines(Get(value, "lines"));
        var surface = Text(value, "surface");
        return surface.Length == 0 ? lines : lines.Length == 0 || lines == surface ? surface : surface + "\n" + lines;
    }

    private static void AppendCheckpoint(object value, List<ReplayFrame> frames, ReadContext context)
    {
        context.Cancellation.ThrowIfCancellationRequested();
        BindRecordedContent(value, context);
        var record = Get(value, "record");
        var checkpoint = Text(value, "checkpoint");
        var trace = Get(record, "trace") ?? record;
        var state = Get(record, "state") ?? Get(record, "last_live_session") ??
            (Get(record, "physical") is not null ? record : Get(trace, checkpoint == "initial" ? "initial" : "final"));
        if (state is null || Get(state, "physical") is null || Get(state, "actors") is null) return;
        var viewer = Text(Get(state, "story"), "host_viewer");
        if (viewer.Length > 0 && (!context.ViewerRecorded || checkpoint == "initial")) context.Viewer = viewer;
        if (checkpoint == "final" && (Get(record, "closing_preview") ?? Get(trace, "closing_preview")) is { } closingPreview)
        {
            AppendClosingPreview(closingPreview, state, record, trace, value, frames, context);
            return;
        }
        if (context.Content is null)
        {
            Add(frames, "event", context.Viewer, "", checkpoint == "initial" ? "记录开始现场" : "记录结束现场",
                new Dictionary<string, object?>
                {
                    ["recorded_state"] = ConvertNative(state, context), ["checkpoint"] = checkpoint,
                    ["source"] = "recorded-checkpoint", ["projection_available"] = false,
                    ["game_time"] = ConvertNative(Get(Get(state, "physical"), "time"), context)
                }, value, context, 0);
            return;
        }
        var dvState = state is IReadOnlyDictionary<string, object?> ? HostValues.ToDv(state) : state;
        var projected = Get(state, "story") is not null
            ? Project(context, () => CampaignRuntime.ReplaySnapshot(dvState, context.Viewer))
            : Project(context, () => CampaignRuntime.ReplayRecordedSnapshot(dvState, context.Viewer));
        var directory = new List<object?>();
        foreach (var actor in Items(Get(state, "actors")))
        {
            var id = Text(actor, "id");
            var mind = Get(actor, "mind");
            var identity = Get(Get(actor, "location_ref"), "object");
            var name = mind is not null && identity is not null
                ? R.Text(Project(context, () => CampaignRuntime.ReplayActorName(
                    mind is IReadOnlyDictionary<string, object?> ? HostValues.ToDv(mind) : mind, identity, id))) : id;
            directory.Add(HostValues.Freeze(new Dictionary<string, object?>
            {
                ["id"] = id, ["title"] = name, ["name_source"] = "recorded-actor-mind"
            }));
        }
        var snapshot = new Dictionary<string, object?>(ConvertNative(projected, context) as IReadOnlyDictionary<string, object?>
            ?? new Dictionary<string, object?>(), StringComparer.Ordinal)
        {
            ["actors"] = directory.AsReadOnly()
        };
        var viewerTitle = directory.FirstOrDefault(actor => Text(actor, "id") == context.Viewer);
        if (viewerTitle is not null) snapshot["viewer_title"] = Text(viewerTitle, "title");
        var metadata = new Dictionary<string, object?>
        {
            ["snapshot"] = HostValues.Freeze(snapshot), ["checkpoint"] = checkpoint,
            ["source"] = "recorded-checkpoint", ["physical_coordinates_recorded"] = false,
            ["phase"] = checkpoint == "final" ? "closed" : "snapshot",
            ["game_time"] = ConvertNative(Get(Get(state, "physical"), "time"), context),
            ["selected"] = "", ["options"] = Array.Empty<object?>(), ["option_time_basis"] = "current"
        };
        foreach (var key in new[] { "provenance", "cut", "lifecycle", "pending_actions", "performance", "controller_kind", "measurement_cut" })
            if ((Get(record, key) ?? Get(trace, key)) is { } recorded) metadata[key] = ConvertNative(recorded, context);
        if (checkpoint == "final" && !metadata.ContainsKey("pending_actions"))
            metadata["pending_actions"] = RecordedPending(state, context);
        Add(frames, "snapshot", context.Viewer, "", checkpoint == "initial" ? "记录开始现场" : "记录结束现场",
            metadata, value, context, 0);
    }

    private static void AppendClosingPreview(object closingPreview, object state, object? record, object? trace,
        object envelope, List<ReplayFrame> frames, ReadContext context)
    {
        if (ConvertNative(closingPreview, context) is not IReadOnlyDictionary<string, object?> recordedPreview)
            throw new InvalidDataException("Recorded closing preview is not a host projection.");
        var snapshot = HostValues.Map(recordedPreview, "snapshot");
        if (snapshot.Count == 0) throw new InvalidDataException("Recorded closing preview has no recorded snapshot.");
        var viewer = HostValues.Text(snapshot, "viewer", HostValues.Text(recordedPreview, "viewer", context.Viewer));
        var preview = RecordedPreview(recordedPreview, viewer, context);
        var metadata = new Dictionary<string, object?>
        {
            ["snapshot"] = snapshot, ["preview"] = preview, ["checkpoint"] = "final",
            ["source"] = "recorded-final-observer-preview", ["physical_coordinates_recorded"] = false,
            ["phase"] = "closed", ["action_state"] = "closed", ["viewer"] = viewer,
            ["game_time"] = snapshot.GetValueOrDefault("game_time") ?? ConvertNative(Get(Get(state, "physical"), "time"), context),
            ["selected"] = "", ["options"] = Array.Empty<object?>(), ["option_time_basis"] = "current"
        };
        foreach (var key in new[] { "provenance", "cut", "lifecycle", "pending_actions", "performance", "controller_kind", "measurement_cut",
                     "state_version", "closing_preview_state_version" })
            if ((Get(record, key) ?? Get(trace, key)) is { } recorded) metadata[key] = ConvertNative(recorded, context);
        if (!metadata.ContainsKey("pending_actions")) metadata["pending_actions"] = RecordedPending(state, context);
        var performance = HostValues.Map(metadata, "performance");
        if (performance.Count == 0) performance = HostValues.Map(preview, "performance");
        if (performance.Count > 0)
        {
            metadata["performance"] = performance;
            metadata["preview"] = DvSession.WithPerformance(preview, performance);
        }
        Add(frames, "snapshot", viewer, "", "记录结束现场", metadata, envelope, context, 0);
    }

    private static IReadOnlyList<object?> RecordedPending(object state, ReadContext context)
    {
        var result = new List<object?>();
        void AddPending(string actor, object? action, object? start, object? due, string phase)
        {
            var id = Text(action, "id");
            if (actor.Length == 0 || id.Length == 0) return;
            var label = Text(action, "label");
            result.Add(HostValues.Freeze(new Dictionary<string, object?>
            {
                ["actor"] = actor, ["action_id"] = id,
                ["label"] = label.Length == 0 || label == id ? "（未记录标签）" : label,
                ["start"] = ConvertNative(start, context), ["due"] = ConvertNative(due, context),
                ["action_state"] = phase, ["source"] = "recorded-checkpoint"
            }));
        }
        foreach (var actor in Items(Get(state, "actors")))
        {
            var scheduled = Get(actor, "scheduled_action");
            if (scheduled is null or false) continue;
            AddPending(Text(actor, "id"), Get(scheduled, "action"), Get(scheduled, "start"), Get(scheduled, "due"), "scheduled");
        }
        var pending = Get(state, "pending");
        if (pending is not null and not false)
            AddPending(Text(pending, "id"), Get(pending, "action"), Get(Get(pending, "quote"), "scheduled_start"), Get(pending, "time"), "prepared");
        return result.AsReadOnly();
    }

    private static void AppendPresentation(object value, object record, List<ReplayFrame> frames, HashSet<string> seen, ReadContext context)
    {
        object? Native(object? item) => ConvertNative(item, context);
        if (Get(value, "visible") is false) return;
        var id = Text(value, "presentation_id");
        if (id.Length == 0) id = Text(value, "execution_event");
        if (id.Length > 0 && !seen.Add("presentation:" + id)) return;
        var text = PresentationText(value);
        var actor = Text(value, "actor");
        var action = Text(value, "action_id");
        var data = new Dictionary<string, object?> { ["presentation"] = Native(value), ["source"] = "recorded-player-presentation" };
        if (TryConfirmation(actor, action, context, out var interaction, true))
            MergeConfirmation(frames, interaction, data, record, text, context);
        else Add(frames, "presentation", actor, action, text, data, record, context);
    }
    private static void Add(List<ReplayFrame> frames, string kind, string actor, string action, string text,
        Dictionary<string, object?> data, object record, ReadContext context, double? seconds = null)
    {
        object? Native(object? item) => ConvertNative(item, context);
        context.Cancellation.ThrowIfCancellationRequested();
        data["contract"] = Contract;
        data["record_index"] = context.RecordIndex;
        foreach (var key in new[] { "frame", "seq", "state_version", "action_event", "presentation_id", "interaction_id", "cause_input", "viewport_size", "real_us", "physics_frame", "bodies", "context", "casebook" })
            if (Get(record, key) is { } value) data[key] = Native(value);
        var duration = seconds ?? (kind == "move" ? 0.3 : kind is "dialogue" or "presentation" ? Math.Clamp(text.Length / 24d, 0.8, 2.0) : 0.6);
        frames.Add(new ReplayFrame(frames.Count, kind, actor, action, text, duration, HostValues.Freeze(data)));
    }
}

// The same reader feeds CLI and Godot. Node references are decoded once; only
// recorded checkpoints become snapshots, and no replay frame executes a command.
internal static class ReplayJournal
{
    internal static IEnumerable<object> Read(string path, CancellationToken cancellationToken = default, Action<object>? checkpointContent = null)
    {
        using var stream = File.OpenRead(path);
        BinaryEvidence.RequireHeader(stream);
        var nodes = new List<object>();
        var previous = new string('0', 64);
        long sequence = 0, eventCount = 0;
        var dag = false;
        var sealedFile = false;
        var initial = false;
        var final = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = BinaryEvidence.ReadFrame(stream, out var end);
            cancellationToken.ThrowIfCancellationRequested();
            if (end) break;
            if (value is not Dictionary<string, object?> envelope || !envelope.ContainsKey("seq") ||
                !envelope.ContainsKey("prev") || !envelope.TryGetValue("payload", out var payloadValue) ||
                payloadValue is not Dictionary<string, object?> payload)
            {
                if (dag) throw new InvalidDataException("A DAG replay contains an unchained record.");
                yield return value ?? throw new InvalidDataException("Replay record must contain a value.");
                continue;
            }
            dag = true;
            if (sealedFile) throw new InvalidDataException("Replay contains records after its seal.");
            if ((long)envelope["seq"]! != sequence || (string)envelope["prev"]! != previous)
                throw new InvalidDataException("Replay sequence is incomplete.");
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            digest.AppendData(Encoding.ASCII.GetBytes(previous + "\n" + sequence.ToString(CultureInfo.InvariantCulture) + "\n"));
            digest.AppendData(BinaryEvidence.Encode(payload));
            var checksum = Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
            if ((string)envelope["sha256"]! != checksum) throw new InvalidDataException("Replay chain checksum mismatch.");
            previous = checksum;
            var kind = (string)payload["kind"]!;
            if (sequence++ == 0)
            {
                if (kind != "header" || (string)payload["format"]! != "dv-raw-dag-v2") throw new InvalidDataException("Unsupported replay DAG.");
                continue;
            }
            if (final && kind != "seal") throw new InvalidDataException("Replay final checkpoint is not followed by its seal.");
            if (kind == "node")
            {
                if ((long)payload["node_id"]! != nodes.Count) throw new InvalidDataException("Replay node order is incomplete.");
                var node = new List<object>();
                foreach (var item in (List<object?>)payload["items"]!)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (item is Dictionary<string, object?> atom)
                    {
                        if (atom.TryGetValue("node", out var reference)) node.Add(nodes[checked((int)(long)reference!)]);
                        else if (atom.TryGetValue("rational", out var exact)) node.Add(Rational.From((string)exact!));
                        else throw new InvalidDataException("Unknown replay atom.");
                    }
                    else node.Add(item is long integer ? (object)(double)integer : item ?? throw new InvalidDataException("Null DV replay atom."));
                }
                nodes.Add(node);
            }
            else if (kind == "event")
            {
                if ((long)payload["ordinal"]! != eventCount++) throw new InvalidDataException("Replay event order is incomplete.");
                yield return nodes[checked((int)(long)payload["root"]!)];
            }
            else if (kind == "checkpoint")
            {
                if ((long)payload["events"]! != eventCount) throw new InvalidDataException("Replay checkpoint count differs.");
                var name = (string)payload["name"]!;
                initial |= name == "initial";
                final |= name == "final";
                var root = nodes[checked((int)(long)payload["root"]!)];
                checkpointContent?.Invoke(root);
                if (name == "final" && eventCount == 0)
                {
                    var trace = R.record_find(root, "trace", root);
                    var events = R.record_find(trace, "events", new List<object>());
                    foreach (var entry in R.Items(events))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        yield return entry;
                    }
                }
                yield return new Dictionary<string, object?>
                {
                    ["kind"] = "replay_checkpoint", ["checkpoint"] = name, ["record"] = root
                };
            }
            else if (kind == "seal")
            {
                if (!initial || !final || (long)payload["events"]! != eventCount || (long)payload["nodes"]! != nodes.Count || payload["final_checkpoint"] is not true)
                    throw new InvalidDataException("Replay seal is incomplete.");
                sealedFile = true;
            }
            else throw new InvalidDataException("Unknown replay frame: " + kind);
        }
        if (dag && !sealedFile) throw new InvalidDataException("Replay has no final seal.");
    }
}

public static partial class CampaignRuntime
{
    internal static object ReplayPlace(string location) => F_dvg_place(location);
    internal static object ReplaySnapshot(object state, string viewer) => F_dvg_snapshot(state, viewer);
    internal static object ReplayRecordedSnapshot(object state, string viewer) => F_dvg_recorded_snapshot(state, viewer);
    internal static object ReplayActorName(object mind, object identity, string fallback) => F_dvg_preview_name(mind, identity, fallback);
}
// --- Source: SessionPerformance.cs ---

public sealed partial class DvSession
{
    private readonly SessionPerformance performance = new();

    private double PerformanceGameMinute() =>
        R.Num(Field(Field(state, "physical", new List<object>()), "time", 0d));

    private IDisposable Measure(string operation, string category = "engine", string viewer = "", string projectionScope = "")
    {
        var valuesScope = R.UseValues(valueContext);
        try
        {
            var pending = Field(state, "pending", false);
            var actor = pending is false ? "" : R.Text(Field(pending, "id", ""));
            return new MeasuredValues(performance.Begin(operation, category, actor, PerformanceGameMinute, viewer, projectionScope), valuesScope);
        }
        catch { valuesScope.Dispose(); throw; }
    }

    private sealed class MeasuredValues(IDisposable measurement, IDisposable valuesScope) : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // Sample the final DV clock before restoring the caller's context.
            try { measurement.Dispose(); }
            finally { valuesScope.Dispose(); }
        }
    }

    private void ObservePerformancePhase(string phase, string actor) => performance.Phase(phase, actor);
    public IReadOnlyDictionary<string, object?> PerformanceSnapshot()
    {
        using var valuesScope = R.UseValues(valueContext);
        return performance.Snapshot(IsClosed);
    }
    public IReadOnlyDictionary<string, object?> PerformanceSummary()
    {
        using var valuesScope = R.UseValues(valueContext);
        return performance.Summary(IsClosed);
    }
    public IReadOnlyDictionary<string, object?> CompactPerformance()
    {
        using var valuesScope = R.UseValues(valueContext);
        return performance.OperationSummary();
    }
    public void SetPerformanceController(string controller) => performance.ConfigureController(controller);
    public void SetPerformanceProvenance(IReadOnlyDictionary<string, object?> provenance) => performance.SetProvenance(provenance);
    public void SetPerformanceLifecycle(IReadOnlyDictionary<string, object?> lifecycle) => performance.SetLifecycle(lifecycle);
    public void RecordPerformancePlanning(string actor, string eventId, IReadOnlyDictionary<string, object?> work, string stopReason) =>
        performance.RecordPlanning(actor, eventId, work, stopReason);
    public void BeginDecisionInterface(string actor, string phase = "decision") => performance.BeginInterface(actor, phase);
    public void SetDecisionInterfacePhase(string actor, string phase) => performance.SetInterfacePhase(actor, phase);
    public void EndDecisionInterface(string actor, string outcome = "accepted") => performance.EndInterface(actor, outcome);
    public void StartPerformanceRecording(string prefix) => performance.StartRecording(prefix);
    public void StopPerformanceClock() => performance.StopWallClock();
    public void WritePerformanceReport(string prefix)
    {
        using var valuesScope = R.UseValues(valueContext);
        performance.WriteReport(prefix, IsClosed);
    }

    private static DvSession OpenMeasured(string operation, Func<R.ValueContext, DvSession> create)
    {
        var gcBaseline = SessionPerformance.CaptureGcCounters();
        var started = Stopwatch.GetTimestamp();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var context = new R.ValueContext();
        using var valuesScope = R.UseValues(context);
        var current = create(context);
        current.performance.RecordStartup(operation, started, allocated, current.PerformanceGameMinute(), gcBaseline);
        return current;
    }
}

/// <summary>Observational accounting; clocks and counters never participate in world decisions.</summary>
internal sealed class SessionPerformance
{
    private long resourceSampleAt;
    private IReadOnlyDictionary<string, object?> resourceSample = HostValues.Empty;

    private IReadOnlyDictionary<string, object?> ResourceSample()
    {
        var now = Stopwatch.GetTimestamp();
        if (resourceSampleAt != 0 && now - resourceSampleAt < Stopwatch.Frequency) return resourceSample;
        var info = GC.GetGCMemoryInfo();
        using var process = Process.GetCurrentProcess();
        resourceSampleAt = now;
        return resourceSample = HostValues.Freeze(new Dictionary<string, object?>
        {
            ["sampling"] = "observational; cached for one second; memory info describes latest GC",
            ["server_gc"] = System.Runtime.GCSettings.IsServerGC,
            ["latency_mode"] = System.Runtime.GCSettings.LatencyMode.ToString(),
            ["total_available_memory_bytes"] = info.TotalAvailableMemoryBytes,
            ["memory_load_bytes"] = info.MemoryLoadBytes,
            ["high_memory_load_threshold_bytes"] = info.HighMemoryLoadThresholdBytes,
            ["heap_size_bytes"] = info.HeapSizeBytes, ["fragmented_bytes"] = info.FragmentedBytes,
            ["promoted_bytes"] = info.PromotedBytes, ["committed_bytes"] = info.TotalCommittedBytes,
            ["process_cpu_ms"] = process.TotalProcessorTime.TotalMilliseconds,
            ["process_working_set_bytes"] = process.WorkingSet64,
            ["process_private_bytes"] = process.PrivateMemorySize64
        });
    }
    private const int SampleCapacity = 8192;
    private const string Contract = "dv-performance-v3";
    private static readonly double[] HistogramBounds =
        [0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 20, 50, 100, 200, 500,
         1000, 2000, 5000, 10000, 30000, 60000, 120000, 300000, 600000];
    private readonly object gate = new();
    private readonly long origin = Stopwatch.GetTimestamp();
    private GcCounters gcBaseline;
    private readonly string sessionId = Guid.NewGuid().ToString("N");
    private readonly string startedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    private readonly List<Frame> stack = [];
    private readonly Dictionary<(string Category, string Operation, string Actor, string Viewer, string Scope), Aggregate> totals = [];
    private readonly SortedDictionary<(int Epoch, long Hour), HourBucket> hours = [];
    private readonly Dictionary<string, long> outcomes = new(StringComparer.Ordinal);
    private readonly Queue<Sample> samples = new(SampleCapacity);
    private readonly Dictionary<string, long> decisionCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Controller, string Actor), DecisionAggregate> decisions = [];
    private readonly Dictionary<string, PlanningAggregate> planning = new(StringComparer.Ordinal);
    private readonly Queue<DecisionSample> decisionSamples = new(SampleCapacity);
    private readonly Dictionary<string, InterfaceWait> interfaceWaits = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> interfaceTicks = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Controller, string Actor, string Phase), InterfaceAggregate> interfacePhases = [];
    private readonly Queue<IReadOnlyDictionary<string, object?>> interfaceEvents = new(SampleCapacity);
    private IReadOnlyDictionary<string, object?> provenance = HostValues.Freeze(new Dictionary<string, object?>());
    private IReadOnlyDictionary<string, object?> lifecycle = HostValues.Freeze(new Dictionary<string, object?>());
    private long sequence, dropped, exceptions, unmatchedPhases, interruptedPhases;
    private long decisionComputeTicks, decisionComputeCalls, decisionComputeIncomplete, decisionInstrumentationTicks;
    private long decisionSequence, droppedDecisions, elapsedWallTicks, wallCursor, rawSequence, interfaceSequence, droppedInterfaces;
    private double wallGameMinute;
    private int wallEpoch;
    private bool wallStarted, wallStopped, controllerLocked;
    private string controllerKind = "engine", wallStartedUtc = "", wallStoppedUtc = "";
    private double? firstGameMinute;
    private double lastGameMinute, advancedMinutes;
    private int epoch;
    private bool auditMode, developerMode;
    private string simulationMode = "unrecorded";
    private string playerActor = "";
    private int actorCount;
    private IReadOnlyDictionary<string, object?> effectiveRuntimeOptions = HostValues.Empty;
    private FileStream? recording;
    private string? recordingPrefix, recordingError;
    private long recordingOmittedPrefix;
    private bool recordingSealed;

    internal void Configure(bool audit, bool developer, string player, int actors, double minute, string mode)
    {
        lock (gate)
        {
            auditMode = audit; developerMode = developer; simulationMode = mode; playerActor = player; actorCount = actors;
            controllerKind = player.Length == 0 ? "engine" : "human";
            firstGameMinute = lastGameMinute = minute;
            var options = R.CurrentRuntimeOptions;
            effectiveRuntimeOptions = HostValues.Freeze(new Dictionary<string, object?>
            {
                ["opt_lazyinternal"] = options.LazyInternal, ["opt_cow"] = options.Cow,
                ["opt_mask"] = options.Mask, ["opt_mask_verify"] = options.MaskVerify,
                ["opt_appendshare"] = options.AppendShare, ["opt_readview"] = options.ReadView,
                ["opt_recordcache"] = options.RecordCache, ["opt_plancache"] = options.PlanCache,
                ["opt_declcache"] = options.DeclarationCache, ["opt_predicate"] = options.Predicate,
                ["opt_predicatecache"] = options.PredicateCache, ["opt_stateindex"] = options.StateIndex,
                ["opt_runtimespeed"] = options.RuntimeSpeed
            });
        }
    }

    internal string ControllerKind { get { lock (gate) return controllerKind; } }

    internal void ConfigureController(string controller)
    {
        var kind = controller.Trim().ToLowerInvariant() switch
        {
            "ai" or "engine" => "engine", "player" or "human" => "human", "llm" => "llm",
            _ => throw new ArgumentException("Performance controller must be engine, human or llm.", nameof(controller))
        };
        lock (gate)
        {
            if ((kind == "engine") != (playerActor.Length == 0))
                throw new InvalidOperationException("The performance controller must match the session's actual player control.");
            if (kind == controllerKind) return;
            if (controllerLocked || wallStopped)
                throw new InvalidOperationException("Choose the performance controller before the first engine operation.");
            controllerKind = kind;
        }
    }

    internal void SetProvenance(IReadOnlyDictionary<string, object?> value)
    {
        lock (gate) provenance = HostValues.Freeze(value.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal));
    }

    internal void SetLifecycle(IReadOnlyDictionary<string, object?> value)
    {
        lock (gate) lifecycle = HostValues.Freeze(value.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal));
    }

    private sealed class DecisionAggregate
    {
        internal long Count;
        internal string FirstEvent = "", LastEvent = "";
        internal double FirstMinute, LastMinute;
    }

    private sealed record DecisionSample(long Sequence, long RecordSequence, string Controller, string Actor, string Event,
        string Action, int Epoch, double GameMinute, long ObservedTicks);

    private sealed class PlanningAggregate
    {
        internal long Decisions, CacheHits;
        internal string FirstEvent = "", LastEvent = "";
        internal readonly Dictionary<string, double> Work = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, long> Stops = new(StringComparer.Ordinal);
    }

    private static readonly string[] PlanningSums =
        ["candidates_considered", "candidates_skipped", "candidate_order_scans", "candidate_order_reuses", "admission_checks", "eligible_evaluations", "forecasts", "forecast_reuses", "expanded_nodes", "candidate_paths", "retained_paths", "route_reuses", "skipped_expansion", "partial_layer", "examined_layers", "completed_layers"];
    private static readonly string[] PlanningMaxima = ["peak_frontier", "budget_limit", "depth_limit", "lookahead_limit"];

    private static double? PlanningNumber(object? value)
    {
        double? number = value switch
        {
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal =>
                Convert.ToDouble(value, CultureInfo.InvariantCulture),
            JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var parsed) => parsed,
            _ => null
        };
        return number.HasValue && double.IsFinite(number.Value) && number.Value >= 0 ? number : null;
    }

    internal void RecordPlanning(string actor, string eventId, IReadOnlyDictionary<string, object?> work, string stopReason)
    {
        if (work.Count == 0) return;
        lock (gate)
        {
            if (!planning.TryGetValue(actor, out var aggregate)) planning[actor] = aggregate = new();
            if (aggregate.Decisions == 0) aggregate.FirstEvent = eventId;
            aggregate.Decisions++;
            aggregate.LastEvent = eventId;
            var cacheHit = work.GetValueOrDefault("cache_hit") is true;
            if (cacheHit) aggregate.CacheHits++;
            foreach (var field in PlanningSums)
            {
                var value = work.GetValueOrDefault(field);
                var number = (field is "skipped_expansion" or "partial_layer") && value is bool skipped
                    ? skipped ? 1d : 0d : PlanningNumber(value);
                if (number is { } amount)
                {
                    var currentReuse = field is "route_reuses" or "forecast_reuses" or "candidate_order_reuses" or "skipped_expansion";
                    aggregate.Work[field] = aggregate.Work.GetValueOrDefault(field) + (cacheHit && !currentReuse ? 0 : amount);
                }
            }
            foreach (var field in PlanningMaxima)
                if (PlanningNumber(work.GetValueOrDefault(field)) is { } amount)
                    aggregate.Work[field] = Math.Max(aggregate.Work.GetValueOrDefault(field), field == "peak_frontier" && cacheHit ? 0 : amount);
            var reason = string.IsNullOrWhiteSpace(stopReason) ? "unrecorded" : stopReason;
            aggregate.Stops[reason] = aggregate.Stops.GetValueOrDefault(reason) + 1;
        }
    }

    internal void RecordDecision(string source, string actor, string eventId, string actionId, double gameMinute)
    {
        lock (gate)
        {
            controllerLocked = true;
            var now = Stopwatch.GetTimestamp();
            AccrueWallLocked(now);
            decisionCounts[source] = decisionCounts.GetValueOrDefault(source) + 1;
            var key = (source, actor);
            if (!decisions.TryGetValue(key, out var aggregate)) decisions[key] = aggregate = new();
            if (aggregate.Count == 0) { aggregate.FirstEvent = eventId; aggregate.FirstMinute = gameMinute; }
            aggregate.Count++;
            aggregate.LastEvent = eventId; aggregate.LastMinute = gameMinute;
            var bucket = Bucket(epoch, (long)Math.Floor(gameMinute / 60d));
            bucket.Decisions[source] = bucket.Decisions.GetValueOrDefault(source) + 1;
            var sample = new DecisionSample(++decisionSequence, ++rawSequence, source, actor, eventId, actionId,
                epoch, gameMinute, now - origin);
            if (decisionSamples.Count == SampleCapacity) { decisionSamples.Dequeue(); droppedDecisions++; }
            decisionSamples.Enqueue(sample);
            StreamRecordLocked(DecisionRecord(sample));
            if (source is "human" or "llm") EndInterfaceLocked(actor, "accepted", eventId, now);
        }
    }

    private sealed class InterfaceWait
    {
        internal required string Controller, Actor, Phase;
        internal required long Started, PhaseStarted, Cursor;
    }

    private sealed class InterfaceAggregate
    {
        internal long Ticks, Segments, Accepted, Interrupted;
    }

    internal void BeginInterface(string actor, string phase)
    {
        lock (gate)
        {
            if (controllerKind == "engine" || wallStopped || actor != playerActor || interfaceWaits.ContainsKey(actor)) return;
            controllerLocked = true;
            var now = Stopwatch.GetTimestamp();
            AccrueWallLocked(now);
            var wait = new InterfaceWait
            {
                Controller = controllerKind, Actor = actor, Phase = NormalizePhase(phase),
                Started = now, PhaseStarted = now, Cursor = now
            };
            interfaceWaits[actor] = wait;
            interfaceTicks.TryAdd(wait.Controller, 0);
            Bucket(wallEpoch, (long)Math.Floor(wallGameMinute / 60d)).InterfaceTicks.TryAdd(wait.Controller, 0);
            InterfacePhase(wait).Segments++;
            RecordInterfaceEventLocked("request", wait, "", "", now);
        }
    }

    internal void SetInterfacePhase(string actor, string phase)
    {
        lock (gate)
        {
            if (!interfaceWaits.TryGetValue(actor, out var wait) || wallStopped) return;
            var next = NormalizePhase(phase);
            if (next == wait.Phase) return;
            var now = Stopwatch.GetTimestamp();
            AccrueWallLocked(now);
            RecordInterfaceEventLocked("phase-end", wait, "continued", "", now);
            wait.Phase = next;
            wait.PhaseStarted = now;
            InterfacePhase(wait).Segments++;
            RecordInterfaceEventLocked(next == "response" ? "response" : "phase-start", wait, "", "", now);
        }
    }

    internal void EndInterface(string actor, string outcome)
    {
        lock (gate)
        {
            var now = Stopwatch.GetTimestamp();
            AccrueWallLocked(now);
            EndInterfaceLocked(actor, outcome, "", now);
        }
    }

    private void EndInterfaceLocked(string actor, string outcome, string eventId, long now)
    {
        if (!interfaceWaits.Remove(actor, out var wait)) return;
        if (outcome == "accepted") InterfacePhase(wait).Accepted++;
        else InterfacePhase(wait).Interrupted++;
        RecordInterfaceEventLocked(outcome == "accepted" ? "accepted" : "outcome", wait, outcome, eventId, now);
    }

    private static string NormalizePhase(string phase) => string.IsNullOrWhiteSpace(phase) ? "decision" : phase.Trim();

    private InterfaceAggregate InterfacePhase(InterfaceWait wait)
    {
        var key = (wait.Controller, wait.Actor, wait.Phase);
        if (!interfacePhases.TryGetValue(key, out var aggregate)) interfacePhases[key] = aggregate = new();
        return aggregate;
    }

    private void RecordInterfaceEventLocked(string stage, InterfaceWait wait, string outcome, string eventId, long now)
    {
        var record = HostValues.Freeze(new Dictionary<string, object?>
        {
            ["type"] = "decision-interface", ["record_sequence"] = ++rawSequence, ["sequence"] = ++interfaceSequence,
            ["stage"] = stage, ["phase"] = wait.Phase, ["controller_kind"] = wait.Controller, ["actor"] = wait.Actor,
            ["request_ticks"] = wait.Started - origin, ["observed_ticks"] = now - origin,
            ["elapsed_ticks"] = Math.Max(0, now - wait.Started), ["phase_elapsed_ticks"] = Math.Max(0, now - wait.PhaseStarted),
            ["epoch"] = wallEpoch, ["game_minute"] = wallGameMinute, ["outcome"] = outcome, ["event_id"] = eventId
        });
        if (interfaceEvents.Count == SampleCapacity) { interfaceEvents.Dequeue(); droppedInterfaces++; }
        interfaceEvents.Enqueue(record);
        StreamRecordLocked(record);
    }

    private void AccrueWallLocked(long now)
    {
        if (!wallStarted || wallStopped) return;
        now = Math.Max(now, wallCursor);
        var bucket = Bucket(wallEpoch, (long)Math.Floor(wallGameMinute / 60d));
        var ticks = now - wallCursor;
        elapsedWallTicks += ticks;
        bucket.WallTicks += ticks;
        foreach (var wait in interfaceWaits.Values)
        {
            var elapsed = Math.Max(0, now - wait.Cursor);
            interfaceTicks[wait.Controller] = interfaceTicks.GetValueOrDefault(wait.Controller) + elapsed;
            bucket.InterfaceTicks[wait.Controller] = bucket.InterfaceTicks.GetValueOrDefault(wait.Controller) + elapsed;
            InterfacePhase(wait).Ticks += elapsed;
            wait.Cursor = now;
        }
        wallCursor = now;
    }

    internal void StopWallClock()
    {
        lock (gate)
        {
            if (wallStopped) return;
            var now = Stopwatch.GetTimestamp();
            AccrueWallLocked(now);
            foreach (var actor in interfaceWaits.Keys.ToArray()) EndInterfaceLocked(actor, "closed-pending", "", now);
            wallStopped = true;
            wallStoppedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        }
    }

    private sealed class Frame
    {
        internal required string Operation, Category, Actor, Viewer, ProjectionScope;
        internal required long Started, Allocated;
        internal required double GameStart;
        internal required int Epoch;
        internal required bool IsPhase;
        internal long ChildTicks, ChildAllocated, DecisionInstrumentationAtStart;
        internal bool Failed;
    }

    private sealed class Aggregate
    {
        internal long Count, Failed, Incomplete, Ticks, SelfTicks, Allocated, SelfAllocated, Maximum;
        internal readonly long[] Histogram = new long[HistogramBounds.Length + 1];

        internal void Add(long ticks, long self, long allocated, long selfAllocated, bool failed, bool incomplete)
        {
            Count++;
            if (failed) Failed++;
            if (incomplete) Incomplete++;
            Ticks += ticks; SelfTicks += self; Allocated += allocated; SelfAllocated += selfAllocated;
            Maximum = Math.Max(Maximum, ticks);
            var index = Array.BinarySearch(HistogramBounds, Milliseconds(ticks));
            if (index < 0) index = ~index;
            Histogram[index]++;
        }

        internal double PercentileUpper(double quantile)
        {
            var rank = Math.Max(1L, (long)Math.Ceiling(Count * quantile));
            var seen = 0L;
            for (var index = 0; index < Histogram.Length; index++)
            {
                seen += Histogram[index];
                if (seen >= rank)
                    return index < HistogramBounds.Length ? Math.Min(HistogramBounds[index], Milliseconds(Maximum)) : Milliseconds(Maximum);
            }
            return Milliseconds(Maximum);
        }
    }

    private sealed class HourBucket
    {
        internal double CoveredMinutes;
        internal long WallTicks;
        internal readonly Dictionary<string, long> SelfTicks = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, long> Decisions = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, long> InterfaceTicks = new(StringComparer.Ordinal);
    }

    private sealed record Sample(long Sequence, long RecordSequence, string Operation, string Category, string Actor,
        string Viewer, string ProjectionScope, int Epoch, double GameStart, double GameEnd, long StartTicks,
        long DurationTicks, long SelfTicks, long AllocatedBytes, long SelfAllocatedBytes, bool Failed, bool PhaseComplete,
        long? DecisionComputeTicks);

    private sealed class Scope(SessionPerformance owner, Frame frame, Func<double> clock) : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            owner.End(frame, clock());
        }
    }

    internal IDisposable Begin(string operation, string category, string actor, Func<double> clock, string viewer = "", string projectionScope = "")
    {
        var minute = clock();
        lock (gate)
        {
            firstGameMinute ??= minute;
            AccrueWallLocked(Stopwatch.GetTimestamp());
            if (stack.Count == 0) { wallGameMinute = minute; wallEpoch = epoch; }
            if (category is "engine" or "speculation" or "development") controllerLocked = true;
            var frame = new Frame
            {
                Operation = operation, Category = category, Actor = actor, Viewer = viewer, ProjectionScope = projectionScope,
                Started = Stopwatch.GetTimestamp(), Allocated = GC.GetAllocatedBytesForCurrentThread(),
                GameStart = minute, Epoch = epoch, IsPhase = false
            };
            stack.Add(frame);
            return new Scope(this, frame, clock);
        }
    }

    internal void Phase(string phase, string actor)
    {
        var begin = phase.EndsWith("-start", StringComparison.Ordinal) || phase.EndsWith("_begin", StringComparison.Ordinal);
        var end = phase.EndsWith("-end", StringComparison.Ordinal) || phase.EndsWith("_end", StringComparison.Ordinal);
        if (!begin && !end) return;
        var name = phase[..^(begin ? 6 : 4)];
        lock (gate)
        {
            if (stack.Count == 0) return;
            if (begin)
            {
                var parent = stack[^1];
                stack.Add(new Frame
                {
                    Operation = "phase." + name, Category = parent.Category,
                    Actor = parent.Category == "projection" ? parent.Actor : actor,
                    Viewer = parent.Category == "projection" && parent.Viewer.Length == 0 ? actor : parent.Viewer,
                    ProjectionScope = parent.ProjectionScope,
                    Started = Stopwatch.GetTimestamp(), Allocated = GC.GetAllocatedBytesForCurrentThread(),
                    GameStart = parent.GameStart, Epoch = parent.Epoch, IsPhase = true,
                    DecisionInstrumentationAtStart = decisionInstrumentationTicks
                });
                return;
            }
            var index = stack.FindLastIndex(frame => frame.IsPhase && frame.Operation == "phase." + name &&
                (frame.Category == "projection" || frame.Actor == actor));
            if (index < 0) { unmatchedPhases++; return; }
            var stopped = Stopwatch.GetTimestamp();
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            while (stack.Count - 1 > index)
            {
                interruptedPhases++;
                FinishTop(stopped, allocated, stack[^1].GameStart, true, false);
            }
            FinishTop(stopped, allocated, stack[^1].GameStart, false, false);
        }
    }

    internal long DecisionInstrumentationCursor { get { lock (gate) return decisionInstrumentationTicks; } }

    internal void ExcludeDecisionInstrumentation(long ticks, long excludedBefore)
    {
        // A diagnostic callback may itself write performance records. Charge
        // the enclosing interval once, rather than subtracting nested I/O twice.
        lock (gate) decisionInstrumentationTicks += Math.Max(0, ticks - (decisionInstrumentationTicks - excludedBefore));
    }

    internal void RecordFailure()
    {
        lock (gate)
        {
            if (stack.Count == 0 || !stack[^1].Failed) exceptions++;
            foreach (var frame in stack) frame.Failed = true;
        }
    }

    internal void RecordOutcome(string outcome, string outcomeCategory = "")
    {
        lock (gate)
        {
            var key = outcome.Length == 0 ? "unrecorded" : outcome;
            outcomes[key] = outcomes.GetValueOrDefault(key) + 1;
            if (outcomeCategory.Length > 0 && outcomeCategory != outcome)
            {
                var catKey = "category:" + outcomeCategory;
                outcomes[catKey] = outcomes.GetValueOrDefault(catKey) + 1;
            }
        }
    }

    private void End(Frame frame, double minute)
    {
        var stopped = Stopwatch.GetTimestamp();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        lock (gate)
        {
            var index = stack.LastIndexOf(frame);
            if (index < 0) return;
            while (stack.Count - 1 > index)
            {
                interruptedPhases++;
                FinishTop(stopped, allocated, minute, true, false);
            }
            var root = index == 0;
            if (root) AccrueWallLocked(stopped);
            FinishTop(stopped, allocated, minute, false, root);
            if (root)
            {
                if (minute < frame.GameStart) epoch++;
                lastGameMinute = minute;
                wallGameMinute = minute;
                wallEpoch = epoch;
            }
        }
    }

    private void FinishTop(long stopped, long allocated, double minute, bool incomplete, bool root)
    {
        var frame = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        var ticks = Math.Max(0, stopped - frame.Started);
        long? decisionTicks = null;
        if (frame.Operation == "phase.decision-compute" && EngineCategory(frame.Category))
        {
            decisionComputeCalls++;
            if (incomplete || frame.Failed) decisionComputeIncomplete++;
            else
            {
                decisionTicks = Math.Max(0, ticks - (decisionInstrumentationTicks - frame.DecisionInstrumentationAtStart));
                decisionComputeTicks += decisionTicks.Value;
            }
        }
        var bytes = Math.Max(0, allocated - frame.Allocated);
        var self = Math.Max(0, ticks - frame.ChildTicks);
        var selfBytes = Math.Max(0, bytes - frame.ChildAllocated);
        if (stack.Count > 0)
        {
            stack[^1].ChildTicks += ticks;
            stack[^1].ChildAllocated += bytes;
        }
        Add(frame.Category, frame.Operation, frame.Actor, frame.Viewer, frame.ProjectionScope, frame.Epoch, frame.GameStart,
            minute, frame.Started, ticks, self, bytes, selfBytes, frame.Failed, incomplete, decisionTicks);
        if (root && frame.Category == "engine" && minute > frame.GameStart)
        {
            advancedMinutes += minute - frame.GameStart;
            var cursor = frame.GameStart;
            while (cursor < minute)
            {
                var hour = (long)Math.Floor(cursor / 60d);
                var until = Math.Min(minute, (hour + 1) * 60d);
                Bucket(frame.Epoch, hour).CoveredMinutes += until - cursor;
                cursor = until;
            }
        }
    }

    private HourBucket Bucket(int timeline, long hour)
    {
        var key = (timeline, hour);
        if (!hours.TryGetValue(key, out var value)) hours[key] = value = new();
        return value;
    }

    private void Add(string category, string operation, string actor, string viewer, string projectionScope, int timeline,
        double gameStart, double gameEnd, long started, long ticks, long self, long allocated, long selfAllocated, bool failed, bool incomplete,
        long? decisionTicks = null)
    {
        var key = (category, operation, actor, viewer, projectionScope);
        if (!totals.TryGetValue(key, out var aggregate)) totals[key] = aggregate = new();
        aggregate.Add(ticks, self, allocated, selfAllocated, failed, incomplete);
        var bucket = Bucket(timeline, (long)Math.Floor(gameStart / 60d));
        bucket.SelfTicks[category] = bucket.SelfTicks.GetValueOrDefault(category) + self;
        var sample = new Sample(++sequence, ++rawSequence, operation, category, actor, viewer, projectionScope,
            timeline, gameStart, gameEnd, started - origin, ticks, self, allocated, selfAllocated, failed, !incomplete, decisionTicks);
        if (samples.Count == SampleCapacity) { samples.Dequeue(); dropped++; }
        samples.Enqueue(sample);
        StreamRecordLocked(SampleRecord(sample));
    }

    internal void RecordStartup(string operation, long started, long allocated, double minute, GcCounters baseline)
    {
        var stopped = Stopwatch.GetTimestamp();
        var bytes = Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - allocated);
        lock (gate)
        {
            gcBaseline = baseline;
            firstGameMinute ??= minute;
            lastGameMinute = minute;
            Add("startup", operation, "", "", "", epoch, minute, minute, started, stopped - started,
                stopped - started, bytes, bytes, false, false);
            wallStarted = true;
            wallCursor = stopped;
            wallGameMinute = minute;
            wallEpoch = epoch;
            wallStartedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        }
    }

    private static double Milliseconds(long ticks) => ticks * (1000d / Stopwatch.Frequency);
    private static bool EngineCategory(string category) => category is "engine" or "speculation";
    private static object? Ratio(double numerator, double denominator) => denominator > 0 ? numerator / denominator : null;
    private static object? Frequency(long count, double milliseconds) => Ratio(count * 3600000d, milliseconds);
    private static string ControllerLabel(string kind) => kind switch
    {
        "engine" => "引擎", "human" => "人工", "llm" => "大模型", _ => kind
    };
    private static string CategoryLabel(string category) => category switch
    {
        "engine" => "正式计算", "speculation" => "提前准备", "projection" => "视图投影",
        "startup" => "初始化与恢复", "persistence" => "保存与归档", "development" => "开发操作", _ => category
    };
    private static string OperationLabel(string operation) => operation switch
    {
        "phase.cognition" => "人物认知", "phase.choice" => "行动选策", "phase.decision-compute" => "选策计算", "phase.goals" => "目标整理",
        "phase.planning-index" => "规划索引", "phase.planning-dependencies" => "规划依赖",
        "phase.planning-expand" => "规划展开", "phase.planning-forecast" => "规划预测",
        "phase.observer-index" => "投影索引",
        "phase.schedule" => "行动排队", "phase.observer-members" => "地图成员",
        "phase.observer-sites" => "交互站点", "phase.observer-doors" => "通道状态",
        "phase.observer-options" => "行动候选", "prepare" => "准备行动", "background-prepare" => "后台准备",
        "prepare-ahead" => "提前计算", "adopt-prepared" => "采用提前结果", "complete" => "正式结算",
        "intent" => "行动意图", "presentation" => "正文投影", "host-snapshot" => "宿主快照",
        "host-delta" => "宿主增量", "executor-snapshot" => "执行者投影", "text-status" => "状态投影",
        "inspect" => "开发查看", "observer-preview" => "观察者预览", "observer-subjects" or "observer-subject" => "主观地图",
        "observer-objective" => "客观地图", "observer-directory" => "主体目录", "observer-options" => "观察者选项",
        "development" => "开发命令", "open-campaign" or "open-configured" or "open-scenario" => "初始化",
        "restore-value" or "restore-file" => "恢复存档", "save" or "save-json" => "保存",
        "trace" or "trace-json" => "轨迹封存", "close" => "关闭会话", _ => operation
    };

    private static double? InterfaceMilliseconds(string source, double engineMs, IReadOnlyDictionary<string, long> waits) =>
        source == "engine" ? engineMs : waits.TryGetValue(source, out var ticks) ? Milliseconds(ticks) : null;

    private void AppendActivity(Dictionary<string, object?> values, double minutes, double engineMs,
        long wallTicks, IReadOnlyDictionary<string, long> counts, IReadOnlyDictionary<string, long> waits)
    {
        var wallMs = Milliseconds(wallTicks);
        var cost = InterfaceMilliseconds(controllerKind, engineMs, waits);
        values["controller_kind"] = controllerKind;
        values["controller_label"] = ControllerLabel(controllerKind);
        values["advanced_game_minutes"] = minutes;
        values["elapsed_wall_ms"] = wallMs;
        values["total_decisions"] = counts.Values.Sum();
        values["game_hours_per_real_hour"] = Ratio(minutes * 60000d, wallMs);
        values["decisions_per_real_hour"] = Frequency(counts.Values.Sum(), wallMs);
        values["interface_total_ms"] = cost;
        values["interface_hours_per_real_hour"] = cost.HasValue ? Ratio(cost.Value, wallMs) : null;
        values["sample_window_partial"] = wallMs < 3600000d;
    }

    private void AppendPerGameHour(Dictionary<string, object?> values)
    {
        var gameHours = advancedMinutes / 60d;
        var choiceMs = Milliseconds(decisionComputeTicks);
        var activeEngine = stack.Any(frame => EngineCategory(frame.Category));
        var complete = !activeEngine && decisionComputeIncomplete == 0;
        values["per_game_hour_measurement_complete"] = complete;
        values["engine_decision_count"] = decisionCounts.GetValueOrDefault("engine");
        values["engine_decision_compute_total_ms"] = complete ? choiceMs : null;
        values["engine_decision_compute_recorded_ms"] = choiceMs;
        values["engine_decision_compute_calls"] = decisionComputeCalls;
        values["engine_decision_compute_incomplete_calls"] = decisionComputeIncomplete;
        values["engine_decisions_per_game_hour"] = !activeEngine ? Ratio(decisionCounts.GetValueOrDefault("engine"), gameHours) : null;
        values["engine_decision_compute_ms_per_game_hour"] = complete ? Ratio(choiceMs, gameHours) : null;
        values["elapsed_real_ms_per_game_hour"] = !activeEngine ? Ratio(Milliseconds(elapsedWallTicks), gameHours) : null;
        values["per_game_hour_basis"] = "current session accumulated forward game-hour advancement; zero advancement is missing";
        values["engine_decision_compute_basis"] = "dvs_choose wall time including planning and unadopted speculation; excludes cognition, execution, projection, waiting, performance-stream encoding/writes and external phase-observer callbacks; not CPU time";
    }

    private object[] DecisionRows(double wallMs, string actor = "") => decisions.Where(item => actor.Length == 0 || item.Key.Actor == actor).OrderBy(item => item.Key.Controller, StringComparer.Ordinal)
        .ThenBy(item => item.Key.Actor, StringComparer.Ordinal)
        .Select(item => (object)HostValues.Freeze(new Dictionary<string, object?>
        {
            ["actor"] = item.Key.Actor, ["controller_kind"] = item.Key.Controller, ["controller_label"] = ControllerLabel(item.Key.Controller),
            ["decisions"] = item.Value.Count, ["decisions_per_real_hour"] = Frequency(item.Value.Count, wallMs),
            ["first_event"] = item.Value.FirstEvent, ["last_event"] = item.Value.LastEvent,
            ["first_game_minute"] = item.Value.FirstMinute, ["last_game_minute"] = item.Value.LastMinute
        })).ToArray();

    internal readonly record struct GcCounters(long Timestamp, int Generation0, int Generation1, int Generation2,
        long PauseTicks, long AllocatedBytes);

    internal static GcCounters CaptureGcCounters() => new(Stopwatch.GetTimestamp(),
        GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
        GC.GetTotalPauseDuration().Ticks, GC.GetTotalAllocatedBytes(precise: false));

    private IReadOnlyDictionary<string, object?> GcSummary()
    {
        // Process counters are observational and include startup/host reporting.
        // Never force a collection or request the synchronizing precise counter.
        var current = CaptureGcCounters();
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["scope"] = "host process since session open; includes startup and reporting",
            ["elapsed_ms"] = Milliseconds(Math.Max(0, current.Timestamp - gcBaseline.Timestamp)),
            ["collections_by_generation"] = Array.AsReadOnly(new object[]
            {
                Math.Max(0, current.Generation0 - gcBaseline.Generation0),
                Math.Max(0, current.Generation1 - gcBaseline.Generation1),
                Math.Max(0, current.Generation2 - gcBaseline.Generation2)
            }),
            ["pause_ms"] = Math.Max(0, current.PauseTicks - gcBaseline.PauseTicks) / (double)TimeSpan.TicksPerMillisecond,
            ["allocated_bytes_approx"] = Math.Max(0, current.AllocatedBytes - gcBaseline.AllocatedBytes),
            ["managed_heap_bytes_approx"] = GC.GetTotalMemory(forceFullCollection: false),
            ["runtime_and_process"] = ResourceSample()
        });
    }

    internal IReadOnlyDictionary<string, object?> OperationSummary()
    {
        lock (gate)
        {
            AccrueWallLocked(Stopwatch.GetTimestamp());
            var groups = totals.GroupBy(item => (item.Key.Category, item.Key.Operation))
                .OrderByDescending(group => group.Sum(item => item.Value.SelfTicks)).ToArray();
            const int limit = 12;
            return HostValues.Freeze(new Dictionary<string, object?>
            {
                ["elapsed_wall_ms"] = Milliseconds(elapsedWallTicks),
                ["advanced_game_minutes"] = advancedMinutes,
                ["active_scopes"] = stack.Count,
                ["gc"] = GcSummary(),
                ["effective_runtime_options"] = effectiveRuntimeOptions,
                ["scope"] = "cumulative completed scopes through this batch; inclusive times overlap; ordered by exclusive time",
                ["omitted_operations"] = Math.Max(0, groups.Length - limit),
                ["operation_costs"] = Array.AsReadOnly(groups.Take(limit)
                    .Select(group => (object)HostValues.Freeze(new Dictionary<string, object?>
                    {
                        ["category"] = group.Key.Category, ["operation"] = group.Key.Operation,
                        ["calls"] = group.Sum(item => item.Value.Count),
                        ["inclusive_total_ms"] = Milliseconds(group.Sum(item => item.Value.Ticks)),
                        ["exclusive_total_ms"] = Milliseconds(group.Sum(item => item.Value.SelfTicks)),
                        ["self_allocated_bytes"] = group.Sum(item => item.Value.SelfAllocated)
                    })).ToArray())
            });
        }
    }

    internal IReadOnlyDictionary<string, object?> Summary(bool closed)
    {
        lock (gate)
        {
            var now = Stopwatch.GetTimestamp();
            AccrueWallLocked(now);
            var engine = totals.Where(item => EngineCategory(item.Key.Category)).Sum(item => Milliseconds(item.Value.SelfTicks));
            var values = new Dictionary<string, object?>
            {
                ["contract"] = Contract, ["closed"] = closed, ["engine_ms"] = engine,
                ["effective_runtime_options"] = effectiveRuntimeOptions,
                ["operation_costs"] = Array.AsReadOnly(totals
                    .GroupBy(item => (item.Key.Category, item.Key.Operation))
                    .OrderByDescending(group => group.Sum(item => item.Value.SelfTicks))
                    .Select(group => (object)HostValues.Freeze(new Dictionary<string, object?>
                    {
                        ["category"] = group.Key.Category, ["operation"] = group.Key.Operation,
                        ["calls"] = group.Sum(item => item.Value.Count),
                        ["exclusive_total_ms"] = group.Sum(item => Milliseconds(item.Value.SelfTicks)),
                        ["inclusive_total_ms"] = group.Sum(item => Milliseconds(item.Value.Ticks)),
                        ["self_allocated_bytes"] = group.Sum(item => item.Value.SelfAllocated)
                    })).ToArray()),
                ["planning_mode"] = simulationMode,
                ["planning_mode_approximation"] = simulationMode == "approximate-v1" ? true : (bool?)null,
                ["measured_until_ticks"] = now - origin, ["wall_measured_until_ticks"] = wallStarted ? wallCursor - origin : null,
                ["stopwatch_frequency"] = Stopwatch.Frequency, ["wall_clock_stopped"] = wallStopped,
                ["active_scopes"] = stack.Count, ["active_decision_interfaces"] = interfaceWaits.Count,
                ["lifecycle"] = controllerKind == "engine" ? lifecycle : HostValues.Empty,
                ["decision_actor_scope"] = playerActor.Length == 0 ? "all" : playerActor,
                ["decisions"] = Array.AsReadOnly(DecisionRows(Milliseconds(elapsedWallTicks), playerActor))
            };
            AppendActivity(values, advancedMinutes, engine, elapsedWallTicks, decisionCounts, interfaceTicks);
            if (controllerKind == "engine") AppendPerGameHour(values);
            return HostValues.Freeze(values);
        }
    }

    internal IReadOnlyDictionary<string, object?> Snapshot(bool closed)
    {
        lock (gate) return SnapshotLocked(closed);
    }

    private IReadOnlyDictionary<string, object?> SnapshotLocked(bool closed)
    {
        var now = Stopwatch.GetTimestamp();
        AccrueWallLocked(now);
        var wallMs = Milliseconds(elapsedWallTicks);
        var engineMs = totals.Where(item => EngineCategory(item.Key.Category)).Sum(item => Milliseconds(item.Value.SelfTicks));
        var categoryRows = totals.GroupBy(item => item.Key.Category, StringComparer.Ordinal)
            .OrderByDescending(group => group.Sum(item => item.Value.SelfTicks))
            .Select(group => (object)HostValues.Freeze(new Dictionary<string, object?>
            {
                ["category"] = group.Key, ["label"] = CategoryLabel(group.Key),
                ["exclusive_total_ms"] = group.Sum(item => Milliseconds(item.Value.SelfTicks)),
                ["self_allocated_bytes"] = group.Sum(item => item.Value.SelfAllocated)
            })).ToArray();
        var operationRows = totals.OrderByDescending(item => item.Value.SelfTicks)
            .ThenBy(item => item.Key.Operation, StringComparer.Ordinal).ThenBy(item => item.Key.Actor, StringComparer.Ordinal)
            .Select(item => (object)HostValues.Freeze(new Dictionary<string, object?>
            {
                ["category"] = item.Key.Category, ["operation"] = item.Key.Operation, ["trigger_actor"] = item.Key.Actor,
                ["viewer"] = item.Key.Viewer, ["projection_scope"] = item.Key.Scope,
                ["category_label"] = CategoryLabel(item.Key.Category), ["label"] = OperationLabel(item.Key.Operation),
                ["calls"] = item.Value.Count, ["failed_calls"] = item.Value.Failed, ["incomplete_phases"] = item.Value.Incomplete,
                ["inclusive_total_ms"] = Milliseconds(item.Value.Ticks), ["exclusive_total_ms"] = Milliseconds(item.Value.SelfTicks),
                ["inclusive_mean_ms"] = Milliseconds(item.Value.Ticks) / item.Value.Count,
                ["exclusive_mean_ms"] = Milliseconds(item.Value.SelfTicks) / item.Value.Count,
                ["inclusive_p95_upper_ms"] = item.Value.PercentileUpper(0.95), ["inclusive_max_ms"] = Milliseconds(item.Value.Maximum),
                ["allocated_bytes"] = item.Value.Allocated, ["self_allocated_bytes"] = item.Value.SelfAllocated
            })).ToArray();
        var decisionRows = DecisionRows(wallMs);
        var sources = decisionCounts.Keys.Concat(interfaceTicks.Keys).Append("engine").Append(controllerKind)
            .Distinct(StringComparer.Ordinal).OrderBy(source => source, StringComparer.Ordinal);
        var sourceRows = sources.Select(source =>
        {
            var cost = InterfaceMilliseconds(source, engineMs, interfaceTicks);
            return (object)HostValues.Freeze(new Dictionary<string, object?>
            {
                ["controller_kind"] = source, ["controller_label"] = ControllerLabel(source),
                ["decisions"] = decisionCounts.GetValueOrDefault(source),
                ["decisions_per_real_hour"] = Frequency(decisionCounts.GetValueOrDefault(source), wallMs),
                ["interface_total_ms"] = cost, ["interface_hours_per_real_hour"] = cost.HasValue ? Ratio(cost.Value, wallMs) : null,
                ["measurement"] = source == "engine" ? "exclusive-engine-and-speculation" : "choice-visible-to-accepted-or-cancelled"
            });
        }).ToArray();
        var interfaceRows = interfacePhases.OrderByDescending(item => item.Value.Ticks).Select(item =>
            (object)HostValues.Freeze(new Dictionary<string, object?>
            {
                ["controller_kind"] = item.Key.Controller, ["actor"] = item.Key.Actor, ["phase"] = item.Key.Phase,
                ["elapsed_ms"] = Milliseconds(item.Value.Ticks), ["segments"] = item.Value.Segments,
                ["accepted"] = item.Value.Accepted, ["interrupted"] = item.Value.Interrupted
            })).ToArray();
        var planningRows = planning.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item =>
        {
            var row = new Dictionary<string, object?>
            {
                ["actor"] = item.Key, ["decisions"] = item.Value.Decisions, ["cache_hits"] = item.Value.CacheHits,
                ["first_event"] = item.Value.FirstEvent, ["last_event"] = item.Value.LastEvent,
                ["stop_reasons"] = HostValues.Freeze(item.Value.Stops.ToDictionary(stop => stop.Key, stop => (object?)stop.Value, StringComparer.Ordinal))
            };
            foreach (var field in PlanningSums.Concat(PlanningMaxima))
                row[field] = item.Value.Work.TryGetValue(field, out var amount) ? amount : null;
            return (object)HostValues.Freeze(row);
        }).ToArray();
        var hourRows = hours.Select(item =>
        {
            double Category(string category) => Milliseconds(item.Value.SelfTicks.GetValueOrDefault(category));
            var engine = Category("engine") + Category("speculation");
            var values = new Dictionary<string, object?>
            {
                ["epoch"] = item.Key.Epoch, ["game_hour"] = item.Key.Hour,
                ["game_start_minute"] = item.Key.Hour * 60d, ["game_end_minute"] = (item.Key.Hour + 1) * 60d,
                ["covered_game_minutes"] = item.Value.CoveredMinutes,
                ["engine_ms"] = Category("engine"), ["speculation_ms"] = Category("speculation"),
                ["projection_ms"] = Category("projection"), ["persistence_ms"] = Category("persistence"),
                ["startup_ms"] = Category("startup"), ["development_ms"] = Category("development"),
                ["engine_decisions"] = item.Value.Decisions.GetValueOrDefault("engine"),
                ["human_decisions"] = item.Value.Decisions.GetValueOrDefault("human"),
                ["llm_decisions"] = item.Value.Decisions.GetValueOrDefault("llm")
            };
            AppendActivity(values, item.Value.CoveredMinutes, engine, item.Value.WallTicks, item.Value.Decisions, item.Value.InterfaceTicks);
            return (object)HostValues.Freeze(values);
        }).ToArray();
        var result = new Dictionary<string, object?>
        {
            ["contract"] = Contract, ["session_id"] = sessionId, ["started_utc"] = startedUtc,
            ["effective_runtime_options"] = effectiveRuntimeOptions,
            ["runtime"] = Environment.Version.ToString(), ["stopwatch_frequency"] = Stopwatch.Frequency,
            ["assembly_id"] = typeof(DvSession).Assembly.ManifestModule.ModuleVersionId.ToString(),
            ["audit_mode"] = auditMode, ["development_mode"] = developerMode, ["player_actor"] = playerActor, ["actor_count"] = actorCount,
            ["planning_mode"] = simulationMode,
            ["planning_mode_approximation"] = simulationMode == "approximate-v1" ? true : (bool?)null,
            ["closed"] = closed, ["active_scopes"] = stack.Count, ["active_decision_interfaces"] = interfaceWaits.Count,
            ["game_start_minute"] = firstGameMinute, ["game_end_minute"] = lastGameMinute, ["engine_ms"] = engineMs,
            ["measured_until_ticks"] = now - origin, ["wall_measured_until_ticks"] = wallStarted ? wallCursor - origin : null,
            ["rate_basis"] = "game-hours, committed-decisions, and accumulated-interface-hours / elapsed-real-session-hours",
            ["decision_basis"] = "new action_selected events in committed session state; development, restored history and unadopted branches excluded",
            ["wall_clock_basis"] = "open/restore completed through host shutdown request; includes reading, input, execution and projection; excludes initialization and shutdown persistence",
            ["interface_basis"] = "engine: exclusive engine+speculation; human/llm: observed choice visibility through accepted choice, cancellation or shutdown; overlapping interfaces are cumulative",
            ["hour_attribution"] = "game-clock buckets: costs at operation start; waiting at current committed time; forward game coverage split at hour boundaries",
            ["wall_started_utc"] = wallStartedUtc, ["wall_stopped_utc"] = wallStoppedUtc,
            ["wall_clock_started"] = wallStarted, ["wall_clock_stopped"] = wallStopped,
            ["clock_discontinuities"] = epoch, ["sample_capacity"] = SampleCapacity,
            ["sample_count"] = sequence, ["retained_samples"] = samples.Count, ["tail_omitted_samples"] = dropped,
            ["decision_sample_count"] = decisionSequence, ["retained_decision_samples"] = decisionSamples.Count,
            ["tail_omitted_decisions"] = droppedDecisions, ["interface_event_count"] = interfaceSequence,
            ["retained_interface_events"] = interfaceEvents.Count, ["tail_omitted_interface_events"] = droppedInterfaces,
            ["raw_record_count"] = rawSequence, ["raw_recording"] = recordingPrefix is not null,
            ["raw_recording_policy"] = recordingPrefix is null ? "bounded-tail-export" : "buffered-binary-stream",
            ["raw_omitted_prefix_records"] = recordingPrefix is null ? dropped + droppedDecisions + droppedInterfaces : recordingOmittedPrefix,
            ["raw_recording_error"] = recordingError, ["exceptions"] = exceptions,
            ["unmatched_phase_ends"] = unmatchedPhases, ["interrupted_phases"] = interruptedPhases,
            ["percentile_method"] = "inclusive-duration fixed-histogram upper bound, capped by observed maximum",
            ["allocation_basis"] = "current managed thread cumulative allocation; not peak or retained memory",
            ["failure_basis"] = "reported invocation and persistence exceptions; action outcomes and interface cancellation are separate",
            ["provenance"] = provenance, ["lifecycle"] = lifecycle,
            ["categories"] = Array.AsReadOnly(categoryRows), ["operations"] = Array.AsReadOnly(operationRows),
            ["decision_actor_scope"] = "all",
            ["decisions"] = Array.AsReadOnly(decisionRows), ["decision_sources"] = Array.AsReadOnly(sourceRows),
            ["interface_phases"] = Array.AsReadOnly(interfaceRows), ["planning"] = Array.AsReadOnly(planningRows),
            ["hourly"] = Array.AsReadOnly(hourRows),
            ["outcomes"] = HostValues.Freeze(outcomes.ToDictionary(item => item.Key, item => (object?)item.Value, StringComparer.Ordinal))
        };
        AppendActivity(result, advancedMinutes, engineMs, elapsedWallTicks, decisionCounts, interfaceTicks);
        AppendPerGameHour(result);
        return HostValues.Freeze(result);
    }

    private static IReadOnlyDictionary<string, object?> SampleRecord(Sample sample) => new Dictionary<string, object?>
    {
        ["type"] = "performance-sample", ["record_sequence"] = sample.RecordSequence, ["sequence"] = sample.Sequence,
        ["operation"] = sample.Operation, ["category"] = sample.Category, ["trigger_actor"] = sample.Actor,
        ["viewer"] = sample.Viewer, ["projection_scope"] = sample.ProjectionScope, ["epoch"] = sample.Epoch,
        ["game_start_minute"] = sample.GameStart, ["game_end_minute"] = sample.GameEnd,
        ["start_ticks"] = sample.StartTicks, ["duration_ticks"] = sample.DurationTicks, ["self_ticks"] = sample.SelfTicks,
        ["allocated_bytes"] = sample.AllocatedBytes, ["self_allocated_bytes"] = sample.SelfAllocatedBytes,
        ["failed"] = sample.Failed, ["phase_complete"] = sample.PhaseComplete,
        ["decision_compute_ticks"] = sample.DecisionComputeTicks
    };

    private static IReadOnlyDictionary<string, object?> DecisionRecord(DecisionSample decision) => new Dictionary<string, object?>
    {
        ["type"] = "performance-decision", ["record_sequence"] = decision.RecordSequence, ["sequence"] = decision.Sequence,
        ["controller_kind"] = decision.Controller, ["actor"] = decision.Actor,
        ["event_id"] = decision.Event, ["action_id"] = decision.Action, ["epoch"] = decision.Epoch,
        ["game_minute"] = decision.GameMinute, ["observed_ticks"] = decision.ObservedTicks
    };

    private IReadOnlyDictionary<string, object?>[] RetainedRecordsLocked() =>
        samples.Select(SampleRecord).Concat(decisionSamples.Select(DecisionRecord)).Concat(interfaceEvents)
            .OrderBy(record => Convert.ToInt64(record["record_sequence"], CultureInfo.InvariantCulture)).ToArray();

    private Dictionary<string, object?> Header(string policy, long omitted) => new()
    {
        ["type"] = "performance-header", ["contract"] = Contract, ["session_id"] = sessionId,
        ["sample_policy"] = policy, ["omitted_prefix_records"] = omitted, ["stopwatch_frequency"] = Stopwatch.Frequency
    };

    private static Dictionary<string, object?> RecordValue(IReadOnlyDictionary<string, object?> record) =>
        record as Dictionary<string, object?> ?? record.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

    internal void StartRecording(string prefix)
    {
        var full = Path.GetFullPath(prefix);
        lock (gate)
        {
            if (recordingPrefix is not null)
            {
                if (recordingPrefix == full) return;
                throw new InvalidOperationException("A session already has a performance recording prefix.");
            }
            if (wallStopped) throw new InvalidOperationException("Start performance recording before closing the session.");
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            recordingPrefix = full;
            try
            {
                recording = new FileStream(full + ".dvlog.partial", FileMode.CreateNew, FileAccess.Write, FileShare.Read,
                    65536, FileOptions.SequentialScan);
                var retained = RetainedRecordsLocked();
                recordingOmittedPrefix = rawSequence - retained.Length;
                recording.Write(BinaryEvidence.Magic);
                BinaryEvidence.WriteFrame(recording, Header("buffered-binary-stream", recordingOmittedPrefix));
                foreach (var record in retained) BinaryEvidence.WriteFrame(recording, RecordValue(record));
            }
            catch (Exception error)
            {
                AbandonRecordingLocked(error);
                throw;
            }
        }
    }

    private void AbandonRecordingLocked(Exception error)
    {
        recordingError ??= error.Message;
        try { recording?.Dispose(); }
        catch (Exception closeError) { recordingError += "; close: " + closeError.Message; }
        recording = null;
    }

    private void StreamRecordLocked(IReadOnlyDictionary<string, object?> record)
    {
        if (recording is null) return;
        var started = Stopwatch.GetTimestamp();
        try { BinaryEvidence.WriteFrame(recording, RecordValue(record)); }
        catch (Exception error) { AbandonRecordingLocked(error); }
        finally { decisionInstrumentationTicks += Math.Max(0, Stopwatch.GetTimestamp() - started); }
    }

    private static void WriteSummaryAndSeal(Stream stream, IReadOnlyDictionary<string, object?> snapshot, string policy, long omitted)
    {
        BinaryEvidence.WriteFrame(stream, new Dictionary<string, object?>
        {
            ["type"] = "performance-summary", ["data"] = JsonSerializer.SerializeToElement(snapshot)
        });
        BinaryEvidence.WriteFrame(stream, new Dictionary<string, object?>
        {
            ["type"] = "seal", ["sample_policy"] = policy, ["omitted_prefix_records"] = omitted,
            ["total_records"] = snapshot["raw_record_count"], ["total_samples"] = snapshot["sample_count"],
            ["total_decision_samples"] = snapshot["decision_sample_count"], ["total_interface_events"] = snapshot["interface_event_count"]
        });
    }

    internal void WriteReport(string prefix, bool closed)
    {
        var full = Path.GetFullPath(prefix);
        IReadOnlyDictionary<string, object?> snapshot;
        IReadOnlyDictionary<string, object?>[] retained;
        bool useRecording;
        lock (gate)
        {
            useRecording = recordingPrefix == full;
            if (useRecording && !closed)
                throw new InvalidOperationException("The active recording is sealed at shutdown. Use a new prefix to export a snapshot.");
            snapshot = SnapshotLocked(closed);
            retained = RetainedRecordsLocked();
            var exportState = snapshot.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            exportState["export_raw_policy"] = useRecording ? "buffered-binary-stream" : "bounded-tail-export";
            exportState["export_omitted_records"] = useRecording ? recordingOmittedPrefix : rawSequence - retained.Length;
            snapshot = HostValues.Freeze(exportState);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var errors = new List<Exception>();
        void Write(string suffix, Action<Stream> action)
        {
            try
            {
                var path = full + suffix;
                using (var stream = new FileStream(path + ".partial", FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    action(stream);
                    stream.Flush(true);
                }
                File.Move(path + ".partial", path, overwrite: false);
            }
            catch (Exception error) { errors.Add(new IOException("Performance report " + suffix + ": " + error.Message, error)); }
        }
        if (useRecording)
        {
            try
            {
                lock (gate)
                {
                    if (recordingSealed) throw new InvalidOperationException("The performance recording is already sealed.");
                    if (recordingError is not null || recording is null)
                        throw new IOException("Performance stream is incomplete: " + (recordingError ?? "stream unavailable"));
                    WriteSummaryAndSeal(recording, snapshot, "buffered-binary-stream", recordingOmittedPrefix);
                    recording.Flush(true);
                    recording.Dispose(); recording = null;
                    File.Move(full + ".dvlog.partial", full + ".dvlog", overwrite: false);
                    recordingSealed = true;
                }
            }
            catch (Exception error)
            {
                lock (gate) AbandonRecordingLocked(error);
                errors.Add(new IOException("Performance report .dvlog: " + error.Message, error));
            }
        }
        else Write(".dvlog", stream =>
        {
            var omitted = Convert.ToInt64(snapshot["raw_record_count"], CultureInfo.InvariantCulture) - retained.Length;
            stream.Write(BinaryEvidence.Magic);
            BinaryEvidence.WriteFrame(stream, Header("bounded-tail-export", omitted));
            foreach (var record in retained) BinaryEvidence.WriteFrame(stream, RecordValue(record));
            WriteSummaryAndSeal(stream, snapshot, "bounded-tail-export", omitted);
        });
        if (useRecording)
        {
            lock (gate)
            {
                var exportState = snapshot.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
                exportState["raw_recording_error"] = recordingError;
                snapshot = HostValues.Freeze(exportState);
            }
        }
        Write(".hourly.csv", stream => WriteCsv(stream, Rows(snapshot, "hourly"),
            ["epoch", "game_hour", "game_start_minute", "game_end_minute", "covered_game_minutes", "elapsed_wall_ms",
             "game_hours_per_real_hour", "total_decisions", "decisions_per_real_hour", "controller_kind",
             "interface_total_ms", "interface_hours_per_real_hour", "engine_decisions", "human_decisions", "llm_decisions",
             "engine_ms", "speculation_ms", "projection_ms", "startup_ms", "persistence_ms", "development_ms"]));
        Write(".operations.csv", stream => WriteCsv(stream, Rows(snapshot, "operations"),
            ["category", "operation", "trigger_actor", "viewer", "projection_scope", "calls", "failed_calls", "incomplete_phases",
             "inclusive_total_ms", "exclusive_total_ms", "inclusive_mean_ms", "exclusive_mean_ms", "inclusive_p95_upper_ms",
             "inclusive_max_ms", "allocated_bytes", "self_allocated_bytes"]));
        Write(".decisions.csv", stream => WriteCsv(stream, Rows(snapshot, "decisions"),
            ["actor", "controller_kind", "controller_label", "decisions", "decisions_per_real_hour",
             "first_event", "last_event", "first_game_minute", "last_game_minute"]));
        Write(".md", stream =>
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true);
            writer.Write(Markdown(snapshot));
        });
        if (errors.Count > 0) throw new AggregateException(errors);
    }

    private static IEnumerable<IReadOnlyDictionary<string, object?>> Rows(IReadOnlyDictionary<string, object?> value, string field) =>
        HostValues.List(value, field).OfType<IReadOnlyDictionary<string, object?>>();

    private static string Number(object? value) => value switch
    {
        null => "—", double number => number.ToString("0.######", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "",
        _ => value.ToString() ?? ""
    };

    private static void WriteCsv(Stream stream, IEnumerable<IReadOnlyDictionary<string, object?>> rows, string[] columns)
    {
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true);
        writer.WriteLine(string.Join(",", columns));
        foreach (var row in rows)
            writer.WriteLine(string.Join(",", columns.Select(column =>
                "\"" + (row.GetValueOrDefault(column) is null ? "" : Number(row[column])).Replace("\"", "\"\"") + "\"")));
    }

    private static string Markdown(IReadOnlyDictionary<string, object?> snapshot)
    {
        var text = new StringBuilder("# 会话决策与耗时报告\n\n");
        text.AppendLine($"会话：{snapshot["session_id"]}；开始 UTC：{snapshot["wall_started_utc"]}；结束 UTC：{snapshot["wall_stopped_utc"]}。");
        text.AppendLine($"采样现实 {Number(Convert.ToDouble(snapshot["elapsed_wall_ms"], CultureInfo.InvariantCulture) / 1000d)} 秒，实际推进 {Number(snapshot["advanced_game_minutes"])} 游戏分钟；正式选择 {Number(snapshot["total_decisions"])} 次。");
        text.AppendLine($"本次策略配置：{snapshot["planning_mode"]}；模式近似：{snapshot["planning_mode_approximation"]}。approximate-v1 使用当前合法候选即时效用与单次声明推理；缺失模式保留为未记录。");
        text.AppendLine();
        text.AppendLine("| 核心指标 | 本窗口累计比率 | 单位 |");
        text.AppendLine("| --- | ---: | --- |");
        text.AppendLine($"| 推进速度 | {Number(snapshot["game_hours_per_real_hour"])} | 游戏小时 / 现实小时 |");
        text.AppendLine($"| 决策频率 | {Number(snapshot["decisions_per_real_hour"])} | 次 / 现实小时 |");
        text.AppendLine($"| {snapshot["controller_label"]}接口耗时率 | {Number(snapshot["interface_hours_per_real_hour"])} | 累计接口小时 / 现实小时 |");
        text.AppendLine();
        text.AppendLine("分母统一为打开或恢复完成至关闭请求的现实游玩时长。小于一现实小时仍按本窗口累计量相除，属于短窗口观测比率，不代表已运行整小时或整局预测；分母为零时留空。引擎接口累计正式计算及提前准备的排他墙钟耗时，包含未采用的提前计算。人工/大模型接口只累计实际出现的可选请求至接受、取消或关闭的区间；没有该记录时为空。接口并行时累计区间允许重叠，不能当作单线程占用率。");
        text.AppendLine("决策数只来自本次正式状态新增的 action_selected；选择、排队、完成与存档提交边界分别记录。Complete 包含成功及拒绝的真实回执，结果由 outcomes 区分；待执行和未进入存档的尾部选择不算作失败。UI 聚焦、规划节点、旧存档历史、开发操作及未采用分支不重复计数。所有来源的决策频率共用现实分母。");
        text.AppendLine("\n## 每游戏小时折算\n");
        text.AppendLine("以下按本次会话累计实际推进的游戏小时折算，不是完整一游戏小时的独立采样；推进为零时缺失。");
        text.AppendLine($"引擎正式选择 {Number(snapshot["engine_decisions_per_game_hour"])} 次 / 游戏小时；选策计算 {Number(snapshot["engine_decision_compute_ms_per_game_hour"] is double compute ? compute / 1000d : null)} 秒 / 游戏小时；现实经过 {Number(snapshot["elapsed_real_ms_per_game_hour"] is double elapsed ? elapsed / 1000d : null)} 秒 / 游戏小时。");
        text.AppendLine($"选策累计 {Number(snapshot["engine_decision_compute_total_ms"])} ms；有效闭合区间合计 {Number(snapshot["engine_decision_compute_recorded_ms"])} ms；未完整区间 {Number(snapshot["engine_decision_compute_incomplete_calls"])}；计量完整 {Number(snapshot["per_game_hour_measurement_complete"])}。");
        text.AppendLine("选策计算只计 dvs_choose 与规划的实际墙钟，含未采用提前计算，扣除性能流编码/写入与外部阶段观察回调；不含其余认知、Complete、截图/投影、等待或归档，不等于纯 CPU 时间。未闭合或失败区间不补造为完整测量，选策总耗时与其折算值保持缺失。");
        text.AppendLine("\n## 控制来源\n");
        text.AppendLine("| 来源 | 正式选择 | 次/现实小时 | 累计接口 ms | 接口小时/现实小时 |");
        text.AppendLine("| --- | ---: | ---: | ---: | ---: |");
        foreach (var row in Rows(snapshot, "decision_sources"))
            text.AppendLine($"| {row["controller_label"]} | {Number(row["decisions"])} | {Number(row["decisions_per_real_hour"])} | {Number(row["interface_total_ms"])} | {Number(row["interface_hours_per_real_hour"])} |");
        var phases = Rows(snapshot, "interface_phases").ToArray();
        if (phases.Length > 0)
        {
            text.AppendLine("\n### 外部接口区间\n");
            text.AppendLine("| 来源 | 主体 | 已观测阶段 | 耗时 ms | 区间数 | 接受 | 中断 |");
            text.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: |");
            foreach (var row in phases)
                text.AppendLine($"| {row["controller_kind"]} | {row["actor"]} | {row["phase"]} | {Number(row["elapsed_ms"])} | {row["segments"]} | {row["accepted"]} | {row["interrupted"]} |");
            text.AppendLine("阶段仅按宿主实际报告的 request/response 等边界记录；未提供的网络、排队或推理内部阶段不估算。等待可能覆盖同时进行的引擎工作，不能与引擎排他耗时直接相加求整段现实时间。");
        }
        text.AppendLine("\n## 耗时拆解\n");
        text.AppendLine("| 分项 | 排他累计 ms | 当前线程累计分配字节 |");
        text.AppendLine("| --- | ---: | ---: |");
        foreach (var row in Rows(snapshot, "categories"))
            text.AppendLine($"| {row["label"]} | {Number(row["exclusive_total_ms"])} | {Number(row["self_allocated_bytes"])} |");
        text.AppendLine("初始化与关闭归档在现实游玩窗口外单列。投影包含视图生成，不等同于终端打印；墙钟包含调度与 GC，不是线程 CPU 时间。分配字节是累计分配，不是峰值内存或泄漏量。");
        text.AppendLine("\n### 操作与阶段\n");
        text.AppendLine("| 操作 | 触发主体 | 观察者/范围 | 次数 | 含子项累计 ms | 排他累计 ms | 含子项均值 ms | 排他均值 ms | 含子项 P95上界 ms |");
        text.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var row in Rows(snapshot, "operations"))
            text.AppendLine($"| {row["category_label"]}/{row["label"]} | {row["trigger_actor"]} | {row["viewer"]}/{row["projection_scope"]} | {row["calls"]} | {Number(row["inclusive_total_ms"])} | {Number(row["exclusive_total_ms"])} | {Number(row["inclusive_mean_ms"])} | {Number(row["exclusive_mean_ms"])} | {Number(row["inclusive_p95_upper_ms"])} |");
        text.AppendLine("排他项可以沿嵌套调用相加；含子项累计和均值不可重复相加。P95为固定直方图上界并由观测最大值收紧。触发主体表示发起该操作的会话边界；观察者及范围另外标明，不能将全体地图开销解释为该主体个人开销。");
        var planningRows = Rows(snapshot, "planning").ToArray();
        if (planningRows.Length > 0)
        {
            text.AppendLine("\n### 选策实际工作\n");
            text.AppendLine("| 主体 | 有工作记录的选择 | 缓存命中 | 候选检查 | 准入检查 | 资格求值 | 预测 | 预测复用 | 展开节点 | 保留路径 | 路径复用 | 跳过展开 | 峰值前沿 |");
            text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
            foreach (var row in planningRows)
                text.AppendLine($"| {row["actor"]} | {row["decisions"]} | {row["cache_hits"]} | {Number(row["candidates_considered"])} | {Number(row["admission_checks"])} | {Number(row["eligible_evaluations"])} | {Number(row["forecasts"])} | {Number(row["forecast_reuses"])} | {Number(row["expanded_nodes"])} | {Number(row["retained_paths"])} | {Number(row["route_reuses"])} | {Number(row["skipped_expansion"])} | {Number(row["peak_frontier"])} |");
            text.AppendLine("\n| 主体 | 已枚举但跳过的候选 | 排序声明检查 | 排序缓存复用 | 部分遍历次数 | 进入求值的层 | 完整求值的层 |");
            text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
            foreach (var row in planningRows)
                text.AppendLine($"| {row["actor"]} | {Number(row["candidates_skipped"])} | {Number(row["candidate_order_scans"])} | {Number(row["candidate_order_reuses"])} | {Number(row["partial_layer"])} | {Number(row["examined_layers"])} | {Number(row["completed_layers"])} |");
            text.AppendLine("实际工作来自正式 action_selected 的 planning_work。当前近似策略记录候选扫描和准入求值，不执行行动树展开。历史记录中的搜索、缓存及路径字段仅按原始事件展示，缺失计数保持缺失，不把不同策略的记录合为同一基准。");
            text.AppendLine();
            foreach (var row in planningRows)
                if (row["stop_reasons"] is IReadOnlyDictionary<string, object?> reasons)
                    text.AppendLine($"- {row["actor"]} 停止原因：{string.Join("；", reasons.Select(reason => reason.Key + " × " + Number(reason.Value)))}；事件 {row["first_event"]} 至 {row["last_event"]}。");
        }
        text.AppendLine("\n## 主体决策\n");
        text.AppendLine("| 主体 | 来源 | 正式选择 | 次/现实小时 | 首次事件 | 最后事件 |");
        text.AppendLine("| --- | --- | ---: | ---: | --- | --- |");
        foreach (var row in Rows(snapshot, "decisions"))
            text.AppendLine($"| {row["actor"]} | {row["controller_label"]} | {row["decisions"]} | {Number(row["decisions_per_real_hour"])} | {row["first_event"]} | {row["last_event"]} |");
        text.AppendLine("\n## 按游戏时钟归属的片段\n");
        text.AppendLine("| 时间段 | 游戏分钟范围 | 覆盖游戏分钟 | 现实秒 | 游戏小时/现实小时 | 次/现实小时 | 接口小时/现实小时 |");
        text.AppendLine("| ---: | --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (var row in Rows(snapshot, "hourly"))
            text.AppendLine($"| {row["epoch"]} | {Number(row["game_start_minute"])}–{Number(row["game_end_minute"])} | {Number(row["covered_game_minutes"])} | {Number(Convert.ToDouble(row["elapsed_wall_ms"], CultureInfo.InvariantCulture) / 1000d)} | {Number(row["game_hours_per_real_hour"])} | {Number(row["decisions_per_real_hour"])} | {Number(row["interface_hours_per_real_hour"])} |");
        text.AppendLine("计算归操作开始的游戏小时，决策归原事件时刻，现实等待归当前已提交时刻；推进覆盖跨小时拆分。该表用于定位发生时段，不假定跨小时计算均匀分摊。累计比率以累计分子和现实分母计算，不平均逐行比率。开发调时不计正式推进，向后调时另开 epoch。");
        text.AppendLine("\n## 运行来源与提交边界\n");
        text.AppendLine($"控制：{snapshot["controller_label"]}；审计：{snapshot["audit_mode"]}；开发：{snapshot["development_mode"]}；主体数：{snapshot["actor_count"]}；运行时：{snapshot["runtime"]}；程序集：{snapshot["assembly_id"]}。");
        foreach (var section in new[] { "provenance", "lifecycle" })
            if (snapshot[section] is IReadOnlyDictionary<string, object?> fields && fields.Count > 0)
            {
                text.AppendLine();
                foreach (var field in fields) text.AppendLine($"- {field.Key}：{Number(field.Value)}");
            }
        text.AppendLine($"\n汇总截止单调时钟 {snapshot["measured_until_ticks"]} ticks；现实窗口截止 {snapshot["wall_measured_until_ticks"]} ticks；频率 {snapshot["stopwatch_frequency"]} ticks/s。投影帧应只携带同一次摘要，截止点与世界状态版本分列。");
        text.AppendLine($"原始记录策略：{snapshot["raw_recording_policy"]}；累计 {snapshot["raw_record_count"]} 条；记录开启前未保留 {snapshot["raw_omitted_prefix_records"]} 条。内存各保留最近 {snapshot["sample_capacity"]} 条，计算尾部以外 {snapshot["tail_omitted_samples"]} 条、决策尾部以外 {snapshot["tail_omitted_decisions"]} 条；流式记录不随尾部淘汰。报告生成与自身写盘不计入快照。");
        text.AppendLine($"本次导出的原始文件策略：{snapshot["export_raw_policy"]}；未包含原始前缀 {snapshot["export_omitted_records"]} 条。封存是否完整以该文件的 seal 和实际写入结果为准。");
        if (snapshot["raw_recording_error"] is not null) text.AppendLine($"原始流写入失败：{snapshot["raw_recording_error"]}；保留 partial，不能视为完整封存。");
        text.AppendLine($"在途计算 {snapshot["active_scopes"]}，在途外部接口 {snapshot["active_decision_interfaces"]}；异常 {snapshot["exceptions"]}，未配对结束 {snapshot["unmatched_phase_ends"]}，中断计算阶段 {snapshot["interrupted_phases"]}。这些状态不等同于剧情达成或行动成功。");
        return text.ToString();
    }
}

// --- Source: HostBuildIdentity.cs ---

/// <summary>Fingerprints assembly files on disk at the first observation.</summary>
internal static class HostBuildIdentity
{
    private static readonly Lazy<IReadOnlyDictionary<string, object?>> Snapshot = new(Create);
    internal static IReadOnlyDictionary<string, object?> Info => Snapshot.Value;
    internal static string Hash => (string)Info["sha256"]!;

    private static IReadOnlyDictionary<string, object?> Create()
    {
        var assemblies = new[] { Assembly.GetEntryAssembly(), typeof(DvSession).Assembly, typeof(DVKernel).Assembly, typeof(DvDocument).Assembly }
            .Where(assembly => assembly is not null).Cast<Assembly>()
            .DistinctBy(assembly => assembly.FullName).OrderBy(assembly => assembly.FullName, StringComparer.Ordinal);
        var records = new List<object>();
        var signature = new StringBuilder();
        foreach (var assembly in assemblies)
        {
            var name = assembly.GetName().Name ?? "";
            var path = assembly.Location;
            // A missing file stays explicit. CLI hosts use immutable per-run
            // output directories. This is not an in-memory Godot image hash.
            var hash = string.IsNullOrEmpty(path) || !File.Exists(path) ? "unavailable"
                : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            records.Add(HostValues.Freeze(new Dictionary<string, object?>
            {
                ["assembly"] = name, ["path"] = path, ["sha256"] = hash
            }));
            signature.Append(name).Append(':').Append(hash).Append('\n');
        }
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["scope"] = "assembly-files-at-first-read; CLI per-run output is immutable; not an in-memory image or source-tree revision",
            ["sha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature.ToString()))).ToLowerInvariant(),
            ["assemblies"] = records.AsReadOnly()
        });
    }
}
// --- Source: HostContentSelection.cs ---

// Maintained host projection mechanisms. All authored identities, room links,
// text and exceptional matches come from host.runtime.dv.
public static partial class CampaignRuntime
{
    private static object HostRecord(params (string Key, object Value)[] fields)
        => fields.Select(field => (object)new List<object> { field.Key, field.Value }).ToList();

    public static object F_host_data(object id) => F_dv_get(R.content("data", id), "value");

    // Matching reads declared equality/suffix constraints. It cannot invoke
    // functions, mutate world state or create observations.
    public static object F_host_match(object pattern, object query)
    {
        foreach (var entry in R.Items(F_dv_find(pattern, "equals", new List<object>())))
        {
            var pair = R.Items(entry);
            if (!R.Bool(F_dv_has_key(query, pair[0])) || !R.Bool(F_dv_same(F_dv_get(query, pair[0]), pair[1]))) return false;
        }
        foreach (var entry in R.Items(F_dv_find(pattern, "suffix", new List<object>())))
        {
            var pair = R.Items(entry);
            if (F_dv_find(query, pair[0], false) is not string value
                || !value.EndsWith(R.Text(pair[1]), StringComparison.Ordinal)) return false;
        }
        return true;
    }

    public static object F_host_select(object rules, object query, object fallback)
    {
        foreach (var rule in R.Items(rules))
            if (R.Bool(F_host_match(F_dv_get(rule, "match"), query))) return F_dv_get(rule, "value");
        return fallback;
    }

    private static object HostNameReference(object template, object id)
        => F_dv_ref(R.cat(F_dv_get(template, "object_prefix"), id), F_dv_get(template, "facet"), F_dv_get(template, "field"));

    private static string? HostKnownText(object world, object reference)
    {
        var cell = F_dv_read(world, reference);
        return R.Bool(F_dv_uncontested(cell)) && F_dv_get(cell, "value") is string text ? text : null;
    }

    private static bool HostSameEdge(object origin, object destination, object from, object to)
        => (R.Bool(R.eq(origin, from)) && R.Bool(R.eq(destination, to)))
            || (R.Bool(R.eq(origin, to)) && R.Bool(R.eq(destination, from)));

    public static object F_dvg_places() => F_host_data("host.runtime.places");
    public static object F_dvg_places_build() => R.copy_value(F_dvg_places());

    public static object F_dvg_place(object location)
        => F_dv_find(F_dvg_places(), location, HostRecord(("map", ""), ("room", location)));

    public static object F_dvg_initial_anchor(object actor, object location)
        => F_host_select(F_host_data("host.runtime.initial_anchors"), HostRecord(("actor", actor), ("location", location)), "");

    public static object F_dvg_interaction(object action)
    {
        if (R.Bool(F_dv_has_key(action, "interaction")))
        {
            var interaction = F_dv_get(action, "interaction");
            return R.Text(F_dv_find(interaction, "kind", "")) == "self"
                ? F_dv_with(interaction, "purpose", F_dv_find(interaction, "purpose", F_dvg_self_purpose(action)))
                : interaction;
        }
        var travel = F_dv_find(action, "travel", new List<object>());
        if (R.Text(F_dv_get(action, "kind")) == "move" && R.Bool(F_dv_has_key(travel, "to")))
            return HostRecord(("kind", "doorway"), ("destination", F_dvg_place(F_dv_get(travel, "to"))));
        var definitions = F_host_data("host.runtime.interactions");
        if (R.Bool(F_dv_has_key(travel, "station")))
        {
            var station = F_dv_get(travel, "station");
            var prefixLength = F_dv_get(definitions, "station_prefix_length");
            return HostRecord(("kind", "site"), ("site", R.slice(station, prefixLength, R.sub(R.length(station), prefixLength))));
        }
        var selected = F_host_select(F_dv_get(definitions, "rules"), action, false);
        if (selected is not false) return R.copy_value(selected);
        var request = F_dv_find(F_dv_find(action, "speech", new List<object>()), "request", new List<object>());
        if (R.Bool(F_dv_has_key(request, "target")))
            return HostRecord(("kind", "person"), ("person", F_dv_get(request, "target")));
        var listeners = R.Items(F_dv_find(action, "listeners", new List<object>()));
        return listeners.Count == 1
            ? HostRecord(("kind", "person"), ("person", listeners[0]))
            : HostRecord(("kind", "self"), ("purpose", F_dvg_self_purpose(action)));
    }

    public static object F_dvg_unknown_person_label(object actor, object viewer)
    {
        // Archived records can bind an older complete content pack. They may
        // display recorded labels but cannot run a declaration added later.
        if (R.content("data", "template.host_unknown_person_label") is false)
        {
            var identity = R.content("data", "host.runtime.identity");
            if (identity is false) return "";
            var unknown = F_dv_find(F_dv_get(identity, "value"), "unknown", false);
            return unknown is false ? "" : F_dv_find(unknown, "fallback", "");
        }
        return DuelView.Core.DvValueTemplate.Instantiate("template.host_unknown_person_label", actor, viewer);
    }

    public static object F_campaign_unknown_person_label(object actor, object viewer)
        => F_dvg_unknown_person_label(actor, viewer);

    public static object F_campaign_person_name_ref(object actor)
    {
        if (R.content("data", "template.host_person_name_ref") is false)
        {
            var identity = R.content("data", "host.runtime.identity");
            if (identity is not false)
            {
                var references = F_dv_find(F_dv_get(identity, "value"), "person_name_refs", new List<object>());
                var recorded = F_dv_find(references, actor, false);
                if (recorded is not false) return R.copy_value(recorded);
            }
            return F_dv_ref(actor, "identity", "name");
        }
        return DuelView.Core.DvValueTemplate.Instantiate("template.host_person_name_ref", actor);
    }

    public static object F_c1_player_name_parts(object actor, object label)
    {
        var definitions = F_host_data("host.runtime.identity");
        var excluded = F_dv_get(definitions, "label_excluded");
        var overrides = F_dv_get(definitions, "label_name_refs");
        var people = R.Items(F_campaign_people());
        var text = R.Text(label);
        var parts = new List<object>();
        var plain = new System.Text.StringBuilder();
        for (var cursor = 0; cursor < text.Length;)
        {
            object matched = "";
            var matchedName = "";
            foreach (var person in people)
            {
                if (R.Bool(F_dv_has(excluded, person))) continue;
                var name = R.Text(F_campaign_person_name(person));
                if (cursor + name.Length <= text.Length && text.AsSpan(cursor, name.Length).SequenceEqual(name.AsSpan()))
                {
                    matched = person;
                    matchedName = name;
                    break;
                }
            }
            if (R.Text(matched) == "")
            {
                plain.Append(text[cursor++]);
                continue;
            }
            if (plain.Length > 0)
            {
                parts.Add(HostRecord(("text", plain.ToString())));
                plain.Clear();
            }
            var reference = F_dv_find(overrides, matched, F_dv_ref(matched, "identity", "name"));
            parts.Add(HostRecord(("ref", R.copy_value(reference)), ("fallback", F_dvg_unknown_person_label(matched, actor))));
            cursor += matchedName.Length;
        }
        if (plain.Length > 0) parts.Add(HostRecord(("text", plain.ToString())));
        return parts;
    }

    public static object F_dvg_subjective_person_text(object state, object subject, object text)
    {
        var excluded = F_dv_get(F_host_data("host.runtime.identity"), "narration_excluded");
        var result = text;
        var subjectId = F_dv_get(subject, "id");
        foreach (var person in R.Items(F_campaign_people()))
        {
            if (R.Bool(R.eq(person, subjectId)) || R.Bool(F_dv_has(excluded, person))) continue;
            var known = F_dvg_preview_person_name(state, F_dv_get(subject, "mind"), person, person, "");
            if (R.Text(known) == "")
                result = F_dvs_present_replace(result, F_campaign_person_name(person), F_dvg_unknown_person_label(person, subjectId));
        }
        return result;
    }

    public static object F_dvg_preview_name(object world, object id, object fallback)
    {
        var name = HostKnownText(world, F_dv_ref(id, "identity", "name"));
        if (name is not null) return name;
        name = HostKnownText(world, F_dv_ref(id, "state", "name"));
        if (name is not null) return name;
        var definitions = F_host_data("host.runtime.identity");
        var objectName = F_dv_find(F_dv_get(definitions, "object_names"), id, false);
        if (objectName is not false)
        {
            name = HostKnownText(world, F_dv_get(objectName, "ref"));
            if (name is not null) return F_dvs_present_replace(F_dv_get(objectName, "template"), "{value}", name);
        }
        foreach (var template in R.Items(F_dv_get(definitions, "name_lookup_templates")))
        {
            name = HostKnownText(world, HostNameReference(template, id));
            if (name is not null) return name;
        }
        var people = F_campaign_people();
        if (R.Text(F_dv_get(world, "domain")) == "physical")
        {
            if (R.Bool(F_dv_has(people, id)) || R.Bool(F_dv_has(F_dv_get(definitions, "physical_people_extra"), id)))
                return F_campaign_person_name(id);
            return F_dv_find(F_dv_find(F_dv_get(world, "objects"), id, new List<object>()), "map_title", fallback);
        }
        var query = HostRecord(("id", id));
        foreach (var rule in R.Items(F_dv_get(definitions, "known_speaker_rules")))
            if (R.Bool(F_host_match(F_dv_get(rule, "match"), query))
                && R.Text(F_dv_truth(world, F_dv_get(rule, "when"))) == "true")
                return F_dv_get(rule, "value");
        if (R.Bool(F_dv_has(people, id)))
        {
            name = HostKnownText(world, HostNameReference(F_dv_get(definitions, "introduced_name_template"), id));
            if (!string.IsNullOrEmpty(name)) return name;
        }
        return fallback;
    }

    public static object F_dvg_literal_self_name(object literal, object person)
    {
        var operation = R.Text(F_dv_find(literal, "op", ""));
        if (operation == "equals" && F_dv_find(literal, "value", false) is string name)
        {
            var reference = F_dv_find(literal, "ref", false);
            foreach (var template in R.Items(F_dv_get(F_host_data("host.runtime.identity"), "self_name_reference_templates")))
                if (R.Bool(F_dv_same(HostNameReference(template, person), reference))) return name;
        }
        if (operation == "all")
            foreach (var item in R.Items(F_dv_get(literal, "items")))
            {
                var selfName = R.Text(F_dvg_literal_self_name(item, person));
                if (selfName != "") return selfName;
            }
        return "";
    }

    public static object F_dvg_map_access(object state, object currentMap)
    {
        var definitions = F_host_data("host.runtime.access");
        var maps = R.copy_value(F_dv_get(definitions, "defaults"));
        var physical = F_dv_get(state, "physical");
        foreach (var entry in R.Items(F_dv_get(definitions, "maps")))
        {
            var map = F_dv_get(entry, "map");
            var opened = R.Text(F_dv_truth(physical, F_dv_get(entry, "condition"))) == "true";
            maps = F_dv_with(maps, map, opened);
            if (R.Bool(R.eq(currentMap, map))) maps = F_dv_with(maps, F_dv_get(definitions, "hub_map"), opened);
        }
        return maps;
    }

    public static object F_dvg_host_access(object state, object map)
    {
        var definitions = F_host_data("host.runtime.access");
        return F_dv_with_many(F_dv_get(definitions, "host_defaults"), HostRecord(
            ("isolation_open", R.Text(F_dv_truth(F_dv_get(state, "physical"), F_dv_get(definitions, "isolation"))) == "true"),
            ("maps", F_dvg_map_access(state, map))));
    }

    public static object F_dvg_traces(object world)
    {
        object result = new List<object>();
        foreach (var entry in R.Items(F_host_data("host.runtime.traces")))
        {
            var pair = R.Items(entry);
            result = F_dv_with(result, pair[0], R.Text(F_dv_truth(world, pair[1])) == "true");
        }
        return result;
    }

    public static object F_dvg_preview_site_descriptor(object action, object site, object location)
    {
        var descriptor = F_dv_find(F_host_data("host.runtime.site_descriptors"), site, false);
        if (descriptor is not false)
        {
            if (F_dv_find(descriptor, "id_template", false) is string template)
                descriptor = F_dv_with(F_dv_without(descriptor, "id_template"), "id", F_dvs_present_replace(template, "{location}", location));
            return R.copy_value(descriptor);
        }
        var definition = F_campaign_object_descriptor(site);
        return R.Text(F_dv_get(definition, "map_kind")) == "object"
            ? HostRecord(("id", site), ("title", F_dv_get(definition, "map_title")), ("kind", "object"), ("is_viewer", false))
            : false;
    }

    public static object F_dvg_preview_door_gates(object origin, object destination)
    {
        var edges = F_c2_edges();
        var supplemental = F_dv_get(F_host_data("host.runtime.doors"), "supplemental_gates");
        var key = new List<object> { "host-door-gates-v2", origin, destination, supplemental };
        var cached = R.Items(R.memo_get(edges, key));
        if (R.Bool(cached[0])) return cached[1];
        var result = new List<object>();
        foreach (var edge in R.Items(edges))
            if (HostSameEdge(origin, destination, F_dv_get(edge, "from"), F_dv_get(edge, "to")))
                result.Add(F_campaign_access(F_dv_get(edge, "gate")));
        foreach (var entry in R.Items(supplemental))
        {
            var pair = R.Items(entry);
            if (R.Bool(R.eq(origin, pair[0])) || R.Bool(R.eq(destination, pair[0]))) result.Add(R.copy_value(pair[1]));
        }
        return R.memo_put(edges, key, result);
    }

    public static object F_dvg_preview_doors(object world, object doors)
    {
        var subject = F_dv_find(world, "subject", "");
        R.phase("observer-doors-start", subject);
        var definitions = F_host_data("host.runtime.doors");
        var labels = F_dv_get(definitions, "labels");
        var result = new List<object>();
        foreach (var door in R.Items(doors))
        {
            var origin = F_dv_find(F_dv_get(door, "from"), "logical_location", "");
            var destination = F_dv_find(F_dv_get(door, "to"), "logical_location", "");
            object status = "unknown";
            var label = F_dv_get(labels, "unknown");
            object basis = new List<object>();
            object special = false;
            foreach (var rule in R.Items(F_dv_get(definitions, "special")))
            {
                var edge = R.Items(F_dv_get(rule, "between"));
                if (HostSameEdge(origin, destination, edge[0], edge[1])) { special = rule; break; }
            }
            if (special is not false)
            {
                foreach (var choice in R.Items(F_dv_get(special, "states")))
                    if (R.Text(F_dv_truth(world, F_dv_get(choice, "when"))) == "true")
                    {
                        status = F_dv_get(choice, "state");
                        label = F_dv_get(choice, "label");
                        basis = R.copy_value(F_dv_get(choice, "basis"));
                        break;
                    }
            }
            else
            {
                foreach (var reference in R.Items(F_dvg_preview_door_gates(origin, destination)))
                {
                    var access = F_dv_read(world, reference);
                    if (!R.Bool(F_dv_uncontested(access))) continue;
                    basis = new List<object> { reference };
                    status = R.Bool(R.eq(F_dv_get(access, "value"), true)) ? "open" : "closed";
                    label = F_dv_get(labels, status);
                }
            }
            var passed = R.Items(F_dv_find(door, "passage_action_ids", new List<object>())).Count > 0;
            var known = R.Text(status) != "unknown";
            if (!known && passed) label = F_dv_get(labels, "observed_unknown");
            result.Add(F_dv_with_many(door, HostRecord(
                ("state", status), ("state_label", label), ("known", known),
                ("connection_known", true), ("passage_observed", passed),
                ("state_refs", basis), ("state_scope", F_dv_get(world, "domain")))));
        }
        R.phase("observer-doors-end", subject);
        return result;
    }
}
// --- Source: SessionContent.cs ---

public sealed partial class DvSession
{
    private IReadOnlyDictionary<string, object?> lastContentReload = HostValues.Empty;
    public IReadOnlyDictionary<string, object?> ContentInfo => HostValues.Freeze(new Dictionary<string, object?>
    {
        ["schema"] = ContentCatalog.Schema, ["hash"] = content.Hash,
        ["items"] = content.Items.Count, ["rules"] = content.Rules.Count,
        ["narratives"] = content.NarrativeCount, ["profiles"] = content.ProfileCount,
        ["characters"] = content.Characters.Count, ["layouts"] = content.LayoutCount, ["data"] = content.DataCount,
        ["last_reload"] = lastContentReload,
        ["case_reasoning"] = CaseReasoningInfo
    });

    /// <summary>Reject formal spatial publication while an object lacks its declared interaction contract.</summary>
    public void RequireSpatialInteractionReadiness(IReadOnlyList<string> objectIds)
    {
        var pack = content.Get("data", "campaign.pack");
        SpatialPublication.RequireInteractionReadiness(content, R.record_find(pack, "value", false), Field(state, "physical", false), objectIds);
    }

    /// <summary>Return only explicitly requested, publication-ready spatial objects.</summary>
    public IReadOnlyDictionary<string, object?> ProjectSpatialObjects(IReadOnlyList<string> objectIds)
    {
        if (IsClosed) throw new InvalidOperationException("The session is closed.");
        var pack = content.Get("data", "campaign.pack");
        return SpatialPublication.ProjectObjects(content, R.record_find(pack, "value", false), Field(state, "physical", false), objectIds);
    }

    /// <summary>Return only explicitly selected floor surfaces from the validated pack.</summary>
    public IReadOnlyDictionary<string, object?> ProjectFloorSurfaces(IReadOnlyList<string> pieceIds)
    {
        if (IsClosed) throw new InvalidOperationException("The session is closed.");
        var pack = content.Get("data", "campaign.pack");
        return SpatialPublication.ProjectFloorSurfaces(content, R.record_find(pack, "value", false), pieceIds);
    }

    /// <summary>Replace authored definitions at an unprepared action boundary, never a state snapshot.</summary>
    public IReadOnlyDictionary<string, object?> ReloadContent(string? path = null, string? source = null)
    {
        if (IsClosed) throw new InvalidOperationException("The session is closed.");
        if (HasPending(state) || HasInFlightMovements) throw new InvalidOperationException("Finish the prepared and in-flight actions before reloading content.");
        using var valuesScope = R.UseValues(valueContext);
        using var auditScope = R.UseAudit(audit);
        using var outputScope = R.UseOutput(DiagnosticOutput);
        var next = source is not null ? CampaignContent.Read(source: source) : CampaignContent.Read(path: path ?? content.Path
            ?? throw new InvalidOperationException("Specify a content path for embedded/restored definitions."));
        if (next.Hash == content.Hash) return ContentInfo;
        if (next.InitializationDefinitions != content.InitializationDefinitions)
            throw new InvalidOperationException("Campaign data, narrative and cognitive template changes require a new session.");
        using var contentScope = R.UseContent(next.Data);
        var physical = R.snapshot(Field(state, "physical", new List<object>()));
        var objects = Field(physical, "objects", new List<object>());
        var adoptions = Field(physical, "adoptions", new List<object>());
        var changedInstances = new HashSet<string>(StringComparer.Ordinal);
        var renamedItems = 0;
        var changedCharacters = 0;
        foreach (var entry in R.Items(adoptions))
        {
            var pair = R.Items(entry);
            var adoption = pair[1];
            var law = Field(adoption, "law", new List<object>());
            var id = R.Text(Field(law, "schema", Field(law, "name", "")));
            if (!content.Rules.ContainsKey(id)) continue;
            if (!next.Rules.ContainsKey(id)) throw new InvalidDataException("Cannot remove an adopted live rule: " + id);
            if (content.Rules[id].GetRawText() == next.Rules[id].GetRawText()) continue;
            var replacement = R.record_with(next.Get("rules", id), "name", Field(law, "name", id));
            replacement = R.record_with(replacement, "schema", id);
            adoptions = R.record_with(adoptions, pair[0], R.record_with(adoption, "law", replacement));
            changedInstances.Add(R.Text(pair[0]));
        }
        foreach (var actor in R.Items(Field(state, "actors", new List<object>())))
        {
            var scheduled = Field(actor, "scheduled_action", false);
            if (scheduled is false) continue;
            var action = Field(scheduled, "action", new List<object>());
            var law = Field(action, "law", "") as string ?? "";
            var id = Field(action, "id", "") as string ?? "";
            if (changedInstances.Contains(law) || changedInstances.Contains(id))
                throw new InvalidOperationException("An affected action is scheduled; finish it before reloading: " + id);
        }
        physical = R.record_with(physical, "adoptions", adoptions);
        foreach (var entry in content.Items)
        {
            var existing = Field(objects, entry.Key, false);
            if (existing is false) continue;
            if (!next.Items.TryGetValue(entry.Key, out var definition))
                throw new InvalidDataException("Cannot remove a live item's definition: " + entry.Key);
            var previousName = entry.Value.GetProperty("name").GetString()!;
            var name = definition.GetProperty("name").GetString()!;
            if (Field(existing, "map_title", "") is string title && title == previousName)
                existing = R.record_with(existing, "map_title", name);
            existing = R.record_with(existing, "visible_fields", Field(next.Get("items", entry.Key), "visible_fields", new List<object>()));
            objects = R.record_with(objects, entry.Key, existing);
            physical = R.record_with(physical, "objects", objects);
            var reference = F_dv_ref(entry.Key, "state", "name");
            var cell = F_dv_read(physical, reference);
            if (name != previousName && R.Bool(F_dv_uncontested(cell)) && Field(cell, "value", false) is string old && old == previousName)
            {
                var basis = new List<object> { R.record_with(new List<object>(), "content_hash", next.Hash) };
                physical = F_dv_store(physical, reference, F_dv_cell(name, 1d, Field(physical, "time", 0d), basis));
                renamedItems++;
                objects = Field(physical, "objects", objects);
            }
        }
        var actors = R.Items(Field(state, "actors", new List<object>())).ToList();
        for (var index = 0; index < actors.Count; index++)
        {
            var actor = actors[index];
            var id = R.Text(Field(actor, "id", ""));
            if (!content.Characters.TryGetValue(id, out var previous)) continue;
            if (!next.Characters.TryGetValue(id, out var definition))
                throw new InvalidDataException("Cannot remove a live character definition: " + id);
            if (previous.GetRawText() == definition.GetRawText()) continue;
            if (Field(actor, "scheduled_action", false) is not false)
                throw new InvalidOperationException("Finish this character's scheduled action before reloading its personality or norms: " + id);
            foreach (var structural in new[] { "group", "domain", "station_roles" })
                if (previous.GetProperty("personality").GetProperty(structural).GetRawText() != definition.GetProperty("personality").GetProperty(structural).GetRawText())
                    throw new InvalidOperationException("Structural character changes require a new session: " + structural);
            actors[index] = F_dv_apply_content_policy(actor, next.Get("characters", id), true);
            changedCharacters++;
        }
        physical = R.record_with(physical, "revision", R.Num(Field(physical, "revision", 0d)) + 1d);
        var candidate = R.record_with(R.snapshot(state), "physical", physical);
        candidate = R.record_with(candidate, "actors", actors);
        candidate = R.record_with(candidate, "content_pack_dv", next.Source);
        var events = F_dv_clone(Field(candidate, "events", new List<object>()));
        F_dvs_event(events, "content_reloaded", "", Field(physical, "time", 0d), new List<object>(),
            HostValues.ToDv(new Dictionary<string, object?> { ["previous_hash"] = content.Hash, ["hash"] = next.Hash,
                ["physical_laws_replaced"] = changedInstances.Count, ["belief_policy"] = "existing-minds-retained" }));
        candidate = R.record_with(candidate, "events", events);
        // No live state changes occurred before every definition and boundary check succeeded.
        BeginTransition();
        content = next;
        state = candidate;
        lastContentReload = HostValues.Freeze(new Dictionary<string, object?>
        {
            ["physical_rules"] = changedInstances.Count, ["item_names"] = renamedItems,
            ["characters"] = changedCharacters, ["beliefs_preserved"] = true
        });
        RecordCommittedDecisions();
        RememberCommitted();
        SetPerformanceProvenance(Provenance());
        return ContentInfo;
    }
}
// --- Source: AuthoredSceneExtensions.cs ---

public static partial class CampaignRuntime
{
    // Scene additions contain authored objects, methods and actor-local knowledge.
    // Installing a method never executes it or copies its physical conclusions to a mind.
    public static object F_campaign_install_extension(object scene, object extension)
    {
        var empty = new List<object>();
        var physical = F_dv_get(scene, "physical");
        var objects = F_dv_find(extension, "objects", empty);
        foreach (var item in R.Items(objects)) physical = F_dv_add_object(physical, item);
        foreach (var cell in R.Items(F_dv_find(extension, "physical_cells", empty)))
            physical = F_dv_store(physical, F_dv_get(cell, "ref"), F_dv_get(cell, "cell"));

        var rules = F_dv_find(extension, "rules", empty);
        foreach (var rule in R.Items(rules))
            physical = F_dv_adopt(physical,
                F_campaign_method(F_dv_get(rule, "name"), F_dv_get(rule, "law")),
                "enforce", 1d, F_dv_get(rule, "basis"));

        var actors = new List<object>();
        var bindings = F_dv_clone(F_dv_get(scene, "bindings"));
        foreach (var original in R.Items(F_dv_get(scene, "actors")))
        {
            var actor = original;
            var id = F_dv_get(actor, "id");
            var mind = F_dv_get(actor, "mind");
            foreach (var item in R.Items(objects))
            {
                var objectId = F_dv_get(item, "id");
                mind = F_dv_add_object(mind, F_campaign_local_object(objectId));
                bindings = F_dv_append(bindings, F_dv_binding(objectId, objectId, id));
            }
            foreach (var entry in R.Items(F_dv_find(extension, "actors", empty)))
            {
                if (!R.Bool(R.eq(F_dv_get(entry, "id"), id))) continue;
                foreach (var cell in R.Items(F_dv_find(entry, "initial_cells", empty)))
                {
                    var reference = F_dv_get(cell, "ref");
                    var objectId = F_dv_get(reference, "object");
                    if (!R.Bool(F_dv_has_key(F_dv_get(mind, "objects"), objectId)))
                    {
                        // An authored initial belief can refer to an existing scene object
                        // that this actor's original mind did not yet represent.
                        F_dv_require(F_dv_has_key(F_dv_get(physical, "objects"), objectId),
                            R.cat("Unknown initial belief object: ", objectId));
                        mind = F_dv_add_object(mind, F_campaign_local_object(objectId));
                        if (R.Bool(R.eq(F_dv_local(bindings, objectId, id), "")))
                            bindings = F_dv_append(bindings, F_dv_binding(objectId, objectId, id));
                    }
                    mind = F_dv_store(mind, reference, F_dv_get(cell, "cell"));
                }
                foreach (var rule in R.Items(rules))
                {
                    if (!R.Bool(F_dv_has(F_dv_find(rule, "predictors", empty), id))) continue;
                    mind = F_dv_adopt(mind,
                        F_campaign_method(F_dv_get(rule, "name"),
                            F_dv_find(rule, "prediction_law", F_dv_get(rule, "law"))),
                        "predict", 1d, F_dv_get(rule, "basis"));
                }
                foreach (var field in new[] { "actions", "hearing_grants", "request_channels" })
                {
                    var values = F_dv_clone(F_dv_find(actor, field, empty));
                    foreach (var item in R.Items(F_dv_find(entry, field, empty)))
                        values = F_dv_append(values, item);
                    actor = F_dv_with(actor, field, values);
                }
                var policy = F_dv_get(actor, "policy");
                var goals = F_dv_clone(F_dv_find(policy, "goals", empty));
                foreach (var goal in R.Items(F_dv_find(entry, "goals", empty)))
                    goals = F_dv_append(goals, goal);
                actor = F_dv_with(actor, "policy", F_dv_with(policy, "goals", goals));
            }
            actors.Add(F_dv_with(actor, "mind", mind));
        }
        return F_dv_with_many(scene, new List<object>
        {
            F_dv_pair("physical", physical), F_dv_pair("actors", actors),
            F_dv_pair("bindings", bindings)
        });
    }
}
