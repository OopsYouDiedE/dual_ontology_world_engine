using System.Text.Json;
namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed partial class Chapter2InteractionSession
{
    private JsonElement _flows, _schedule, _flowEvents;
    private readonly Stack<JsonElement> _flowSteps = new();
    private readonly HashSet<(int Day, Slot Slot)> _enteredSlots = new();

    private void LoadFlows(JsonElement chapter)
    {
        _flows = chapter.GetProperty("design").GetProperty("flows").Clone();
        _schedule = chapter.GetProperty("design").GetProperty("schedule").Clone();
        _flowEvents = chapter.GetProperty("design").GetProperty("flow_events").Clone();
    }

    private void PushSteps(JsonElement steps)
    {
        foreach (var step in steps.EnumerateArray().Reverse()) _flowSteps.Push(step);
    }
    private void StartFlow(string name) => PushSteps(_flows.GetProperty(name).GetProperty("steps"));
    private void NightAction(List<Dictionary<string,object?>> output)
    { StartFlow(_flowEvents.GetProperty("night_action").GetString()!); Pump(output); }

    private bool EnterSlot()
    {
        var key = (_game.Day, _game.Slot);
        if (!_enteredSlots.Add(key)) return false;
        foreach (var row in _schedule.EnumerateArray())
            if (row.GetProperty("day").GetInt32() == key.Day && Enum.Parse<Slot>(row.GetProperty("slot").GetString()!) == key.Slot)
            { StartFlow(row.GetProperty("flow").GetString()!); return true; }
        return false;
    }

    private bool FlowCondition(JsonElement node) => node.GetProperty("op").GetString() switch
    {
        "not" => !FlowCondition(node.GetProperty("args")[0]),
        "and" => FlowCondition(node.GetProperty("args")[0]) && FlowCondition(node.GetProperty("args")[1]),
        "or" => FlowCondition(node.GetProperty("args")[0]) || FlowCondition(node.GetProperty("args")[1]),
        "call" when node.GetProperty("name").GetString() == "accused" => _game.Accused is not null,
        "literal" => node.GetProperty("value").ValueKind == JsonValueKind.True,
        _ => throw new InvalidDataException("Unsupported chapter flow condition.")
    };

    private void FlowStep(JsonElement step, List<Dictionary<string,object?>> output)
    {
        switch (step.GetProperty("op").GetString())
        {
            case "show":
                var scene = step.GetProperty("scene").GetString()!;
                if (scene.StartsWith('@')) FlowCommand(scene[1..], output);
                else { ShownDay = _game.Day; ShownSlot = _game.Slot; Consume(_narrative.Scene(scene, Variables()), output); }
                break;
            case "continue": StartFlow(step.GetProperty("flow").GetString()!); break;
            case "if": PushSteps(step.GetProperty(FlowCondition(step.GetProperty("condition")) ? "then" : "else")); break;
            case "stop": _flowSteps.Clear(); break;
            case "choices":
                var choices = step.GetProperty("options").EnumerateArray().Select(option =>
                    (option.GetProperty("id").GetString()!, option.GetProperty("label").GetString()!, (Func<bool>)(() => true),
                     (Action<List<Dictionary<string,object?>> >)(o => PushSteps(option.GetProperty("steps")))));
                Menu(_content.Text("sleep_question"), choices, output);
                break;
            default: throw new InvalidDataException("Unsupported chapter flow step.");
        }
    }

    private void FlowCommand(string command, List<Dictionary<string,object?>> output)
    {
        var parts = command.Split(':');
        switch (parts[0])
        {
            case "scene": Scene(Enum.Parse<Place>(parts[1]), output); break;
            case "corridor": _game.HostCorridor(parts[1], parts[2] == "\u5f00"); break;
            case "dorm_out": _game.HostDormOut(parts[1]); break;
            case "summon": _game.HostSummon(parts[1]); break;
            case "field": _stage = "field"; ShownDay = _game.Day; ShownSlot = _game.Slot; break;
            case "confront": BeginConfront(output); break;
            case "council": Council(output); break;
            case "run":
                var action = parts[1] switch
                {
                    "eat_pantry" => new PlayerAction(Cmd.EatPantry), "class" => new(Cmd.Class),
                    "collect" => new(Cmd.Collect), "sleep" => new(Cmd.Sleep),
                    "stay" => new(Cmd.Stay) { Place = _game.Where(_game.Det) },
                    _ => throw new InvalidDataException("Unknown authored slot action.")
                };
                Run(action, output); break;
            default: throw new InvalidDataException("Unknown authored flow command.");
        }
    }

    private void Mediate(string wording)
    {
        foreach (var (who, accepted) in _game.HostMediate(wording))
        {
            var trigger = "mediate." + (accepted ? "accept." : "reject.") + who.ToString().ToLowerInvariant();
            _continuations.Enqueue(o => Activate(trigger, o));
        }
    }
}
