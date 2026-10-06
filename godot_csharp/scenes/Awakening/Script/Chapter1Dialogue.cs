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
    private static string OwnObject(string who) => who switch { "huo" => "E7", "su" => "E5", "shen" => "E10", _ => throw new ArgumentException(who) };
    private object State(string key, object fallback) => _content.Session.GetProgress("state").GetValueOrDefault(key, fallback);
    private object Life(string who, string key) => _content.Session.GetProgress("life").TryGetValue(who, out var life)
        ? ((Dictionary<string, object>)life).GetValueOrDefault(key, "") : "";
    private Dictionary<string, object>? Heard(string who) => ((object[])State("last_sound", Array.Empty<object>()))
        .Cast<Dictionary<string, object>>().FirstOrDefault(h => (string)h["who"] == who);
    public object? Evaluate(string name, IReadOnlyList<object?> args)
    {
        string A(int index = 0) => Convert.ToString(args[index]) ?? "";
        bool Has(string group) => _content.Session.Has(group, A());
        return name switch
        {
            "flag" => Has("flags"), "seen" => Has("examined"), "tested" => Has("experiments"), "asked" => Has("helped"),
            "recalled" => Has("recalls"), "compared" => Has("soles_compared"), "v6_open" => _content.V6Open,
            "told_rule" => _world.KnowsRule1(PersonOf(A())), "hooks" => _world.Hooks(PersonOf(A())), "total_hooks" => _world.TotalHooks,
            "matched" => State("full_match_seen", false), "replayed" => Convert.ToInt32(State("replay_count", 0)) > 0,
            "fragment" => _content.Session.GetProgress("recalls").GetValueOrDefault(A(), ""), "alone_choice" => Life(A(), "choice"),
            "life_thought" => Life(A(), "thought"), "talk_first" => Life(A(), "alone") is true && Life(A(), "told") is not true,
            "recall_thought" => _content.Session.GetProgress("recall_decision").GetValueOrDefault("thought", ""),
            "wrong_name" => State("wrong_name", ""), "detective_name" => State("detective_name", "侦探"),
            "addr" => Address(A()), "heard" => HeardText(A()), "heard_where" => HeardWhere(A()), "sound_sentence" => SoundSentence(),
            _ => throw new InvalidDataException("Unknown chapter dialogue query: " + name),
        };
    }
    private string Address(string who)
    {
        var close = _world.Hooks(PersonOf(who)) > 0;
        return who switch { "detective" => close ? "我的侦探" : "侦探先生", "huo" => close ? "霍铎" : "霍先生",
            "shen" => close ? "沈鼎" : "沈师傅", "su" => close ? "泠" : "苏小姐", _ => "" };
    }
    private string HeardText(string who)
    {
        var heard = Heard(who); if (heard is null) return "";
        var direction = Convert.ToString(heard.GetValueOrDefault("direction", ""));
        return Convert.ToString(heard["text"]) + (direction == "" ? "" : "　" + direction);
    }
    private string HeardWhere(string who) => Heard(who)?.GetValueOrDefault("room") switch
    { "hall" => "铜口旁", "foyer" => "门边", "pantry" => "备餐间", "R05" => "R-05", "R08" => "R-08", _ => "" };
    private string SoundSentence()
    {
        var index = Convert.ToInt32(State("sound_sentence", 0));
        return TestSentences.All[Math.Clamp(index, 0, TestSentences.All.Length - 1)].Full;
    }
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
                _content.Session.Set("nudged", OwnObject(A()), true);
                _commands.Add(new("consider", [A()])); break;
            case "share_recall":
                _content.ApplyRecall(_world, OwnObject(A()), A());
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
