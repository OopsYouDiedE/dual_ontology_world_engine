using System.Text.Json;
using System.Text.Json.Serialization;
namespace DualOntologyWorldEngine.Story;

/// <param name="LineId">Stable authored line ID (translation key); empty for generated text.</param>
public sealed record InteractionLine(string Speaker, string Mood, string Text, string VoiceClip, string LineId = "", string SourceId = "");
public sealed record InteractionChoice(string Id, string Label, bool IsAvailable, string LockHint, string LabelId = "", string Purpose = "");
public sealed record WorldCommand(string Name, IReadOnlyList<object?> Arguments);
public enum PresentationMode { Dialogue, EvidenceCard, Menu, World, Done }
public sealed record EvidenceCard(string Id, string Title, string Facts, string Source, string Picture, IReadOnlyList<string> Notes);
public sealed record InteractionPacket(IReadOnlyList<InteractionLine> Lines, IReadOnlyList<InteractionChoice> Choices,
    PresentationMode PresentationMode, IReadOnlyList<WorldCommand> WorldCommands, bool IsComplete = false, EvidenceCard? Card = null);

/// <summary>Compiled game dialogue data. No Godot or core ontology types occur in this protocol.</summary>
public sealed class CompiledDialogueDocument
{
    public Dictionary<string, List<DialogueInstruction>> Entries { get; set; } = new();
    public static CompiledDialogueDocument Parse(string json) => JsonSerializer.Deserialize<CompiledDialogueDocument>(json,
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
        ?? throw new InvalidDataException("Empty dialogue document.");
}
public sealed class DialogueExpression
{
    public string Op { get; set; } = "literal";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement Value { get; set; }
    public string Name { get; set; } = "";
    public List<DialogueExpression> Args { get; set; } = new();
}
public sealed class DialogueInstruction
{
    public string Op { get; set; } = "";
    public string Speaker { get; set; } = "";
    public string Mood { get; set; } = "";
    public string Text { get; set; } = "";
    public string VoiceClip { get; set; } = "";
    public string LineId { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public DialogueExpression? Condition { get; set; }
    public List<DialogueExpression> Args { get; set; } = new();
    public List<DialogueInstruction> Then { get; set; } = new();
    public List<DialogueInstruction> Else { get; set; } = new();
    public List<DialogueOption> Options { get; set; } = new();
}
public sealed class DialogueOption
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string LabelId { get; set; } = "";
    public string LockHint { get; set; } = "";
    public DialogueExpression? Condition { get; set; }
    public List<DialogueInstruction> Body { get; set; } = new();
}
public sealed class DialogueContext
{
    public required Func<string, IReadOnlyList<object?>, object?> EvaluateCall { get; init; }
    public required Action<string, IReadOnlyList<object?>> ExecuteCall { get; init; }
    public required Func<string, string> Interpolate { get; init; }
    public Func<IReadOnlyList<WorldCommand>> TakeCommands { get; init; } = () => [];
}

/// <summary>One acknowledged line per packet; choices and side effects remain in the game runtime.</summary>
public sealed class CompiledDialogueRuntime(CompiledDialogueDocument document, DialogueContext context)
{
    private readonly Stack<IEnumerator<DialogueInstruction>> _frames = new();
    private List<DialogueOption>? _choices;
    private DialogueInstruction? _pending;
    public bool IsRunning => _frames.Count > 0 || _choices is not null || _pending is not null;
    public void Start(string entry)
    {
        if (IsRunning) throw new InvalidOperationException("Finish the active interaction before starting another.");
        Push(document.Entries.TryGetValue(entry, out var body) ? body : throw new KeyNotFoundException(entry));
    }
    private void Push(IEnumerable<DialogueInstruction> body) => _frames.Push(body.GetEnumerator());
    private DialogueInstruction? Read()
    {
        if (_pending is { } pending) { _pending = null; return pending; }
        while (_frames.Count > 0)
        {
            var frame = _frames.Peek();
            if (frame.MoveNext()) return frame.Current;
            frame.Dispose(); _frames.Pop();
        }
        return null;
    }
    private bool Available(DialogueOption option) => option.Condition is null || Truth(Evaluate(option.Condition));
    public void Choose(string id)
    {
        var option = _choices?.SingleOrDefault(o => o.Id == id) ?? throw new InvalidOperationException("No such pending choice: " + id);
        if (!Available(option)) throw new InvalidOperationException("Choice is locked: " + id);
        _choices = null;
        Push(option.Body);
    }
    public InteractionPacket Next()
    {
        if (_choices is not null) return ChoicePacket();
        // A malformed generated jump cycle must fail visibly instead of freezing the host.
        for (var budget = 0; budget < 10000 && IsRunning; budget++)
        {
            var step = Read();
            if (step is null) break;
            switch (step.Op)
            {
                case "line":
                    var lines = new InteractionLine[] { new(context.Interpolate(step.Speaker), step.Mood, context.Interpolate(step.Text), step.VoiceClip, step.LineId, step.SourceId) };
                    var next = Read();
                    if (next?.Op == "choices")
                    { _choices = next.Options; return ChoicePacket() with { Lines = lines }; }
                    _pending = next;
                    return new(lines, [], PresentationMode.Dialogue, []);
                case "if": Push(step.Condition is not null && Truth(Evaluate(step.Condition)) ? step.Then : step.Else); break;
                case "call":
                    context.ExecuteCall(step.Name, step.Args.Select(Evaluate).ToArray());
                    var commands = context.TakeCommands();
                    if (commands.Count > 0) return new([], [], PresentationMode.World, commands);
                    break;
                case "command": return new([], [], PresentationMode.World, [new(step.Name, step.Args.Select(Evaluate).ToArray())]);
                case "choices": _choices = step.Options; return ChoicePacket();
                case "jump":
                    while (_frames.TryPop(out var old)) old.Dispose();
                    if (step.Target != "END") Push(document.Entries[step.Target]);
                    break;
                default: throw new InvalidDataException("Unknown compiled dialogue operation: " + step.Op);
            }
        }
        if (_frames.Count > 0) throw new InvalidDataException("Compiled dialogue exceeded its instruction budget.");
        return new([], [], PresentationMode.Done, [], true);
    }
    private InteractionPacket ChoicePacket() => new([], _choices!.Select(o => new InteractionChoice(o.Id,
        context.Interpolate(o.Label), Available(o), context.Interpolate(o.LockHint), o.LabelId,
        o.Body is [{ Op: "jump", Target: "END" }] ? "close" : "")).ToArray(), PresentationMode.Menu, []);
    private static bool Truth(object? value) => value switch
    { null => false, bool b => b, string s => s.Length > 0, byte n => n != 0, sbyte n => n != 0,
        short n => n != 0, ushort n => n != 0, int n => n != 0, uint n => n != 0, long n => n != 0,
        ulong n => n != 0, float n => n != 0, double n => n != 0, decimal n => n != 0, _ => true };
    public object? Evaluate(DialogueExpression expression)
    {
        object? Arg(int index) => Evaluate(expression.Args[index]);
        int Compare() => Arg(0) is string a && Arg(1) is string b ? string.CompareOrdinal(a, b) : Convert.ToDouble(Arg(0)).CompareTo(Convert.ToDouble(Arg(1)));
        return expression.Op switch
        {
            "literal" => expression.Value.ValueKind switch { JsonValueKind.String => expression.Value.GetString(), JsonValueKind.True => true,
                JsonValueKind.False => false, JsonValueKind.Number => expression.Value.GetDouble(), _ => null },
            "call" => context.EvaluateCall(expression.Name, expression.Args.Select(Evaluate).ToArray()),
            "not" => !Truth(Arg(0)), "and" => expression.Args.All(a => Truth(Evaluate(a))), "or" => expression.Args.Any(a => Truth(Evaluate(a))),
            "eq" => Compare() == 0, "ne" => Compare() != 0, "gt" => Compare() > 0, "gte" => Compare() >= 0, "lt" => Compare() < 0, "lte" => Compare() <= 0,
            _ => throw new InvalidDataException("Unknown compiled expression: " + expression.Op),
        };
    }
}
