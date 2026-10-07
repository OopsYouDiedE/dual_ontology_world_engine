using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>A line of authored presentation. Rendering, voice playback and layout belong to Godot.</summary>
public sealed record Chapter2NarrativeLine(string Speaker, string Text, string Mood, string VoiceClip, string LineId = "", string SourceId = "");
public sealed record Chapter2NarrativeChoice(string CandidateId, string Label);
public sealed record Chapter2NarrativeEvent
{
    public required string Kind { get; init; }
    public string Id { get; init; } = "";
    public string PresentationId { get; init; } = "";
    public IReadOnlyList<Chapter2NarrativeLine> Lines { get; init; } = [];
    public IReadOnlyList<Chapter2NarrativeChoice> Choices { get; init; } = [];
    public string ActionName { get; init; } = "";
    public IReadOnlyList<string> Args { get; init; } = [];
}

/// <summary>
/// Executes authored chapter beats as semantic events. It never applies a world effect or
/// chooses an NPC response: the owning C# interaction session validates candidate availability
/// and executes each whitelisted action through the existing Core-backed Game.
/// </summary>
public sealed class Chapter2Narrative
{
    private sealed record Trigger(string Event, int? Day, Slot? Slot, bool Once);
    private sealed record Beat(string Id, Trigger Trigger, IReadOnlyList<JsonElement> Steps);
    private static readonly Regex Variable = new(@"\{\{\s*(detective_name|wrong_name(?:\(\))?)\s*\}\}", RegexOptions.Compiled);
    private static readonly Regex AnyVariable = new(@"\{\{.*?\}\}", RegexOptions.Compiled);
    private readonly List<Beat> _beats = [];
    private readonly HashSet<string> _played = new(StringComparer.Ordinal);
    private readonly HashSet<string> _optionIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _referencedOptions = new(StringComparer.Ordinal);
    private readonly Queue<JsonElement> _steps = new();
    private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);
    private HashSet<string>? _pendingChoices;
    private string? _presentationId;
    private long _nextPresentation;

    public Chapter2Narrative(string json, bool awakeningActivated)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("format").GetString() != "dual-ontology-world-script/v1")
            throw new InvalidDataException("Unsupported chapter narrative format.");
        var chapters = root.GetProperty("chapters");
        if (chapters.GetArrayLength() != 1)
            throw new InvalidDataException("A formal chapter narrative must contain exactly one chapter.");
        var chapter = chapters[0];
        var requires = chapter.GetProperty("requires").EnumerateArray().Select(x => x.GetString()).ToArray();
        if (!requires.Contains("awakening", StringComparer.Ordinal))
            throw new InvalidDataException("Chapter two must declare the awakening handoff.");
        if (!awakeningActivated)
            throw new InvalidOperationException("Chapter two cannot inherit an absent awakening session.");
        var prefix = chapter.GetProperty("id").GetString() + ":";
        if (chapter.TryGetProperty("candidates", out var candidates))
        foreach (var candidate in candidates.EnumerateArray())
        {
            if (candidate.GetProperty("kind").GetString() == "option")
            {
                var candidateId = candidate.GetProperty("id").GetString()!;
                _optionIds.Add(candidateId.Contains(':') ? candidateId : prefix + candidateId);
            }
        }
        if (!chapter.TryGetProperty("sequences", out var sequences))
            throw new InvalidDataException("Formal chapter is missing authored sequences.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sequence in sequences.EnumerateArray())
        {
            var id = sequence.GetProperty("id").GetString() ?? throw new InvalidDataException("Missing sequence ID.");
            if (!ids.Add(id)) throw new InvalidDataException($"Duplicate sequence {id}.");
            var trigger = sequence.GetProperty("trigger");
            var eventName = trigger.GetProperty("event").GetString() ?? throw new InvalidDataException($"Missing trigger for {id}.");
            var day = trigger.TryGetProperty("day", out var dayValue) ? dayValue.GetInt32() : (int?)null;
            Slot? slot = null;
            if (trigger.TryGetProperty("slot", out var slotValue))
            {
                if (slotValue.ValueKind == JsonValueKind.Number) slot = (Slot)slotValue.GetInt32();
                else if (Enum.TryParse<Slot>(slotValue.GetString(), true, out var parsed)) slot = parsed;
                else throw new InvalidDataException($"Invalid slot in {id}.");
                if (!Enum.IsDefined(slot.Value)) throw new InvalidDataException($"Invalid slot in {id}.");
            }
            if (trigger.TryGetProperty("condition", out var condition) &&
                (condition.ValueKind != JsonValueKind.True && condition.ValueKind != JsonValueKind.False))
                throw new NotSupportedException($"Sequence {id} uses an unsupported condition; it cannot be silently ignored.");
            if (condition.ValueKind == JsonValueKind.False) continue;
            var once = trigger.TryGetProperty("once", out var onceValue) && onceValue.GetBoolean();
            var steps = sequence.GetProperty("steps").EnumerateArray().Select(step => step.Clone()).ToArray();
            foreach (var step in steps) ValidateStep(id, step);
            _beats.Add(new(id, new(eventName, day, slot, once), steps));
        }
        foreach (var option in _referencedOptions)
            if (_beats.Count(beat => beat.Trigger.Event == "choice:" + option) != 1)
                throw new InvalidDataException($"Option {option} must have exactly one flat choice branch.");
    }

    private void ValidateStep(string sequence, JsonElement step)
    {
        var op = step.GetProperty("op").GetString();
        if (op == "line")
        {
            ValidateInterpolation(step.GetProperty("speaker").GetString() ?? "", sequence);
            ValidateInterpolation(step.GetProperty("text").GetString() ?? "", sequence);
        }
        else if (op == "action")
        {
            var action = step.GetProperty("name").GetString();
            var args = step.GetProperty("args");
            if (action is not ("lesson_answer" or "mediate") ||
                args.GetArrayLength() != (action == "lesson_answer" ? 1 : 0) ||
                action == "lesson_answer" && args[0].ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new InvalidDataException($"Unsupported or malformed authored action {action} in {sequence}.");
            foreach (var arg in args.EnumerateArray())
                if (arg.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null)
                    throw new InvalidDataException($"Action argument in {sequence} must be a scalar.");
        }
        else if (op == "choice")
        {
            var choices = new HashSet<string>(StringComparer.Ordinal);
            foreach (var option in step.GetProperty("options").EnumerateArray())
            {
                var candidate = option.GetProperty("candidate_id").GetString() ?? "";
                if (!_optionIds.Contains(candidate) || !choices.Add(candidate))
                    throw new InvalidDataException($"Choice in {sequence} references a missing or duplicate flat option {candidate}.");
                ValidateInterpolation(option.GetProperty("label").GetString() ?? "", sequence);
                _referencedOptions.Add(candidate);
            }
            if (choices.Count == 0) throw new InvalidDataException($"Choice in {sequence} has no options.");
        }
        else throw new NotSupportedException($"Unsupported authored step {op} in {sequence}.");
    }

    private static void ValidateInterpolation(string value, string sequence)
    {
        var stripped = AnyVariable.Replace(value, match =>
        {
            if (!Variable.IsMatch(match.Value))
                throw new InvalidDataException($"Unsupported interpolation {match.Value} in {sequence}.");
            return "";
        });
        if (stripped.Contains("{{", StringComparison.Ordinal) || stripped.Contains("}}", StringComparison.Ordinal))
            throw new InvalidDataException($"Malformed interpolation in {sequence}.");
    }

    public IReadOnlyList<Chapter2NarrativeEvent> Activate(string eventName, int day, Slot slot,
        IReadOnlyDictionary<string, string>? variables = null)
    {
        if (_presentationId is not null || _pendingChoices is not null || _steps.Count != 0)
            throw new InvalidOperationException("Complete the current presentation or choice before activating another beat.");
        _variables.Clear();
        if (variables is not null)
            foreach (var (key, value) in variables) _variables[key] = value;
        foreach (var beat in _beats)
        {
            if (beat.Trigger.Event != eventName || beat.Trigger.Day is { } d && d != day ||
                beat.Trigger.Slot is { } s && s != slot || beat.Trigger.Once && !_played.Add(beat.Id)) continue;
            foreach (var step in beat.Steps) _steps.Enqueue(step);
        }
        return Drain();
    }

    public IReadOnlyList<Chapter2NarrativeEvent> Select(string candidateId)
    {
        if (_pendingChoices is null || !_pendingChoices.Contains(candidateId))
            throw new InvalidOperationException("The chapter option is unavailable or has already been used.");
        _pendingChoices = null;
        var continuation = _steps.ToArray();
        _steps.Clear();
        var branch = _beats.Where(b => b.Trigger.Event == "choice:" + candidateId).ToArray();
        if (branch.Length != 1) throw new InvalidDataException($"Missing unique branch for {candidateId}.");
        foreach (var beat in branch)
            foreach (var step in beat.Steps) _steps.Enqueue(step);
        foreach (var step in continuation) _steps.Enqueue(step);
        return Drain();
    }

    public IReadOnlyList<Chapter2NarrativeEvent> Presented(string presentationId)
    {
        if (_presentationId is null || presentationId != _presentationId)
            throw new InvalidOperationException("The presentation acknowledgement is stale or duplicated.");
        _presentationId = null;
        return Drain();
    }

    private IReadOnlyList<Chapter2NarrativeEvent> Drain()
    {
        var emitted = new List<Chapter2NarrativeEvent>();
        while (_steps.Count > 0)
        {
            var next = _steps.Peek();
            var op = next.GetProperty("op").GetString();
            if (op == "line")
            {
                var lines = new List<Chapter2NarrativeLine>();
                while (_steps.TryPeek(out next) && next.GetProperty("op").GetString() == "line")
                {
                    _steps.Dequeue();
                    lines.Add(new(Expand(next.GetProperty("speaker").GetString() ?? ""),
                        Expand(next.GetProperty("text").GetString() ?? ""),
                        next.TryGetProperty("mood", out var mood) ? mood.GetString() ?? "" : "",
                        next.TryGetProperty("voice_clip", out var clip) ? clip.GetString() ?? "" : "",
                        next.TryGetProperty("line_id", out var lineId) ? lineId.GetString() ?? "" : "",
                        next.TryGetProperty("source_id", out var sourceId) ? sourceId.GetString() ?? "" : ""));
                }
                _presentationId = "chapter2-presentation-" + (++_nextPresentation).ToString(CultureInfo.InvariantCulture);
                emitted.Add(new() { Kind = "lines", PresentationId = _presentationId, Lines = lines });
                break;
            }
            _steps.Dequeue();
            if (op == "choice")
            {
                var options = next.GetProperty("options").EnumerateArray().Select(o =>
                    new Chapter2NarrativeChoice(o.GetProperty("candidate_id").GetString()!, Expand(o.GetProperty("label").GetString() ?? ""))).ToArray();
                _pendingChoices = options.Select(o => o.CandidateId).ToHashSet(StringComparer.Ordinal);
                emitted.Add(new() { Kind = "choice", Id = next.GetProperty("id").GetString() ?? "", Choices = options });
                break;
            }
            if (op == "action")
            {
                var args = next.GetProperty("args").EnumerateArray().Select(a => a.ValueKind == JsonValueKind.String
                    ? a.GetString() ?? "" : a.GetRawText()).ToArray();
                emitted.Add(new() { Kind = "action", ActionName = next.GetProperty("name").GetString() ?? "", Args = args });
            }
        }
        return emitted;
    }

    private string Expand(string text) => Variable.Replace(text, match =>
    {
        var key = match.Groups[1].Value.Replace("()", "", StringComparison.Ordinal);
        if (!_variables.TryGetValue(key, out var value))
            throw new InvalidOperationException($"Missing narrative variable {key}.");
        return value;
    });
}
