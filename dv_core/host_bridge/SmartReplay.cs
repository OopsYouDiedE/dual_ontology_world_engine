using DuelView.Compiled;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

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
        var frames = new List<ReplayFrame>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var context = new ReadContext { Cancellation = cancellationToken };
        foreach (var value in ReplayJournal.Read(path, cancellationToken)) Append(value, frames, seen, context);
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
            if (destination.Length > 0 && status == "applied")
                detail["position"] = HostValues.FromDv(DVProgram.ReplayPlace(destination));
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

    private static IReadOnlyList<object?> RecordedOptions(object? value, string actor, ReadContext context) =>
        Array.AsReadOnly(Items(value).Select(option =>
        {
            if (option is not IReadOnlyDictionary<string, object?> fields) return option;
            var id = Text(option, "id");
            var label = Text(option, "label");
            if (!string.IsNullOrWhiteSpace(label) && label != id) return option;
            context.Selections.TryGetValue(InteractionKey(actor, id), out var selection);
            var data = Get(selection, "data");
            label = Text(Get(data, "action"), "label");
            if (string.IsNullOrWhiteSpace(label)) label = Text(data, "label");
            var recorded = !string.IsNullOrWhiteSpace(label) && label != id;
            var normalized = new Dictionary<string, object?>(fields, StringComparer.Ordinal)
            {
                ["label"] = recorded ? label : "（未记录标签）"
            };
            if (recorded) normalized["label_event"] = Text(selection, "id");
            return (object?)HostValues.Freeze(normalized);
        }).ToArray());

    private static IReadOnlyDictionary<string, object?> RecordedPreview(IReadOnlyDictionary<string, object?> value,
        string actor, ReadContext context)
    {
        var viewer = Text(value, "viewer");
        if (viewer.Length > 0) actor = viewer;
        var normalized = new Dictionary<string, object?>(value, StringComparer.Ordinal);
        if (value.TryGetValue("options", out var options)) normalized["options"] = RecordedOptions(options, actor, context);
        if (value.GetValueOrDefault("observers") is IReadOnlyDictionary<string, object?> observers)
            normalized["observers"] = HostValues.Freeze(observers.ToDictionary(pair => pair.Key, pair =>
                pair.Value is IReadOnlyDictionary<string, object?> observer
                    ? (object?)RecordedPreview(observer, pair.Key, context) : pair.Value, StringComparer.Ordinal));
        return HostValues.Freeze(normalized);
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
        return surface.Length == 0 ? lines : lines.Length == 0 ? surface : surface + "\n" + lines;
    }

    private static void AppendCheckpoint(object value, List<ReplayFrame> frames, ReadContext context)
    {
        context.Cancellation.ThrowIfCancellationRequested();
        var record = Get(value, "record");
        var checkpoint = Text(value, "checkpoint");
        var trace = Get(record, "trace") ?? record;
        var state = Get(record, "state") ?? Get(record, "last_live_session") ??
            (Get(record, "physical") is not null ? record : Get(trace, checkpoint == "initial" ? "initial" : "final"));
        if (state is null || Get(state, "physical") is null || Get(state, "actors") is null) return;
        var viewer = Text(Get(state, "story"), "host_viewer");
        if (viewer.Length > 0 && (!context.ViewerRecorded || checkpoint == "initial")) context.Viewer = viewer;
        var dvState = state is IReadOnlyDictionary<string, object?> ? HostValues.ToDv(state) : state;
        var projected = Get(state, "story") is not null
            ? DVProgram.ReplaySnapshot(dvState, context.Viewer)
            : DVProgram.ReplayRecordedSnapshot(dvState, context.Viewer);
        var directory = new List<object?>();
        foreach (var actor in Items(Get(state, "actors")))
        {
            var id = Text(actor, "id");
            var mind = Get(actor, "mind");
            var identity = Get(Get(actor, "location_ref"), "object");
            var name = mind is not null && identity is not null
                ? R.Text(DVProgram.ReplayActorName(mind is IReadOnlyDictionary<string, object?> ? HostValues.ToDv(mind) : mind,
                    identity, id)) : id;
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
    internal static IEnumerable<object> Read(string path, CancellationToken cancellationToken = default)
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

public static partial class DVProgram
{
    internal static object ReplayPlace(string location) => F_dvg_place(location);
    internal static object ReplaySnapshot(object state, string viewer) => F_dvg_snapshot(state, viewer);
    internal static object ReplayRecordedSnapshot(object state, string viewer) => F_dvg_recorded_snapshot(state, viewer);
    internal static object ReplayActorName(object mind, object identity, string fallback) => F_dvg_preview_name(mind, identity, fallback);
}
