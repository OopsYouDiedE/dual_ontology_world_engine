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
        internal long RecordIndex = -1;
        internal string Viewer = "detective";
        internal double? LastInputRealUs;
        internal readonly Dictionary<string, object> Receipts = new(StringComparer.Ordinal);
    }

    public static SmartReplay Load(string path)
    {
        var frames = new List<ReplayFrame>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var context = new ReadContext();
        foreach (var value in ReplayJournal.Read(path)) Append(value, frames, seen, context);
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
        if (Index < 0) delivered.Add(frames[++Index]);
        while (Index < Count)
        {
            var duration = frames[Index].DurationSeconds;
            if (elapsed < duration) break;
            elapsed -= duration;
            if (Index + 1 == Count) { Playing = false; break; }
            delivered.Add(frames[++Index]);
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
    private static object? Native(object? value)
    {
        if (value is null) return null;
        if (value is Rational exact) return HostValues.FromDv(exact);
        if (value is IReadOnlyDictionary<string, object?> map)
            return HostValues.Freeze(map.ToDictionary(pair => pair.Key, pair => Native(pair.Value), StringComparer.Ordinal));
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
                return HostValues.Freeze(record);
            }
            return Array.AsReadOnly(values.Select(Native).ToArray());
        }
        if (value is IReadOnlyList<object?> items) return Array.AsReadOnly(items.Select(Native).ToArray());
        return value;
    }
    private static string Lines(object? lines) => string.Join("\n", Items(lines).Select(item =>
        item is string text ? text : Text(item, "text") is { Length: > 0 } line ? line : Text(item, "surface")).Where(text => text.Length > 0));

    private static void Append(object value, List<ReplayFrame> frames, HashSet<string> seen, ReadContext context)
    {
        context.RecordIndex++;
        var type = Text(value, "type");
        if (type.Length == 0) type = Text(value, "kind");
        if (type.Length == 0) type = Text(value, "event");
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
            if (status != "applied") { beat = "rejected"; text = actionId + " · " + status; }
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
            Add(frames, beat, actor, actionId, text, detail, value, context);
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

    private static void AppendCheckpoint(object value, List<ReplayFrame> frames, ReadContext context)
    {
        var record = Get(value, "record");
        var checkpoint = Text(value, "checkpoint");
        var trace = Get(record, "trace") ?? record;
        var state = Get(record, "state") ?? Get(record, "last_live_session") ??
            (Get(record, "physical") is not null ? record : Get(trace, checkpoint == "initial" ? "initial" : "final"));
        if (state is null || Get(state, "physical") is null || Get(state, "actors") is null) return;
        var viewer = Text(Get(state, "story"), "host_viewer");
        if (viewer.Length > 0) context.Viewer = viewer;
        var dvState = state is IReadOnlyDictionary<string, object?> ? HostValues.ToDv(state) : state;
        var projected = Get(state, "story") is not null
            ? DVProgram.ReplaySnapshot(dvState, context.Viewer)
            : DVProgram.ReplayRecordedSnapshot(dvState, context.Viewer);
        Add(frames, "snapshot", context.Viewer, "", checkpoint == "initial" ? "记录开始现场" : "记录提交现场",
            new Dictionary<string, object?>
            {
                ["snapshot"] = HostValues.Record(projected), ["checkpoint"] = checkpoint,
                ["source"] = "recorded-checkpoint", ["physical_coordinates_recorded"] = false
            }, value, context, 0);
    }

    private static void AppendPresentation(object value, object record, List<ReplayFrame> frames, HashSet<string> seen, ReadContext context)
    {
        if (Get(value, "visible") is false) return;
        var id = Text(value, "presentation_id");
        if (id.Length == 0) id = Text(value, "execution_event");
        if (id.Length > 0 && !seen.Add("presentation:" + id)) return;
        var lines = Lines(Get(value, "lines"));
        var surface = Text(value, "surface");
        var text = surface.Length == 0 ? lines : lines.Length == 0 ? surface : surface + "\n" + lines;
        Add(frames, "presentation", Text(value, "actor"), Text(value, "action_id"), text,
            new Dictionary<string, object?> { ["presentation"] = Native(value), ["source"] = "recorded-player-presentation" }, record, context);
    }
    private static void Add(List<ReplayFrame> frames, string kind, string actor, string action, string text,
        Dictionary<string, object?> data, object record, ReadContext context, double? seconds = null)
    {
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
    internal static IEnumerable<object> Read(string path)
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
            var value = BinaryEvidence.ReadFrame(stream, out var end);
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
                    foreach (var entry in R.Items(events)) yield return entry;
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
}
