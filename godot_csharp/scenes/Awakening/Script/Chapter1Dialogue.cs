using System.Text.RegularExpressions;
using DualOntologyWorldEngine.Story;

namespace DualOntologyWorldEngine.Awakening;

public sealed class Chapter1Dialogue
{
    private readonly AwakeningContentSession _content;
    private readonly MorningWorld _world;
    private readonly List<WorldCommand> _commands = new();
    private readonly CompiledDialogueRuntime _runtime;
    private IReadOnlyList<InteractionLine> _lastLines = [];
    public Chapter1Dialogue(AwakeningContentSession content, MorningWorld world)
    {
        _content = content; _world = world;
        _runtime = new(Chapter1CompiledDialogue.Create(), new()
        {
            EvaluateCall = Evaluate, ExecuteCall = Execute, Interpolate = Interpolate,
            TakeCommands = () => { var commands = _commands.ToArray(); _commands.Clear(); return commands; },
        });
    }
    public InteractionPacket Start(string title) { _runtime.Start(title); _lastLines = []; return Next(); }
    public InteractionPacket Choose(string id) { _runtime.Choose(id); return Next(); }
    public InteractionPacket Next()
    {
        var packet = _runtime.Next();
        if (packet.Lines.Count > 0) _lastLines = packet.Lines;
        if (packet.Choices.Count > 0 && packet.Lines.Count == 0) packet = packet with { Lines = _lastLines };
        return packet;
    }
    private static Person PersonOf(string who) => who switch
    { "detective" => Person.Detective, "huo" => Person.Huo, "shen" => Person.Shen, "su" => Person.Su, _ => throw new ArgumentException(who) };
    public object? Evaluate(string name, IReadOnlyList<object?> args) => _content.Query(_world, name, args);
    private void Execute(string name, IReadOnlyList<object?> args)
    {
        string A(int index = 0) => Convert.ToString(args[index]) ?? "";
        switch (name)
        {
            case "set_flag": _content.Session.Set("flags", A(), true); break;
            case "clear_flag": _content.Session.Remove("flags", A()); break;
            case "amplify": _world.Amplify(PersonOf(A()), A(1)); break;
            case "whisper":
                _world.Whisper(PersonOf(A()), A(1));
                _content.Session.Set("nudged", _content.OwnObject(A()), true);
                _commands.Add(new("consider", [A()])); break;
            case "share_recall":
                _content.ApplyRecall(_world, _content.OwnObject(A()), A());
                if (_content.Session.GetProgress("life").TryGetValue(A(), out var life)) ((Dictionary<string, object>)life)["told"] = true;
                break;
            default: throw new InvalidDataException("Unknown chapter dialogue effect: " + name);
        }
    }
    private string Interpolate(string text) => Regex.Replace(text, @"\{\{(.*?)\}\}", match =>
    {
        var expression = match.Groups[1].Value.Trim();
        var call = Regex.Match(expression, "^([a-z_]+)\\((?:\"([^\"]*)\")?\\)$");
        var value = call.Success ? Evaluate(call.Groups[1].Value, call.Groups[2].Success ? [call.Groups[2].Value] : []) : Evaluate(expression, []);
        return Convert.ToString(value) ?? "";
    });
}
