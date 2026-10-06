using System.Text.Json;
using DualOntologyWorldEngine.Story;

namespace DualOntologyWorldEngine.Awakening;

using Step = StoryStep<MorningWorld, Person>;

/// <summary>
/// chapter_01.story 里“流程”“判定”“阶段”的执行器：把生成数据里的步骤和条件原样跑起来。
/// 这里只有解释器；哪一步演什么、何时改阶段、答辩怎样才算答对，都写在 .story 里。
/// </summary>
public sealed partial class AwakeningContentSession
{
    private static readonly IReadOnlyDictionary<string, string> NoArgs = new Dictionary<string, string>();
    private string? _judgeSaid, _judgeShown;

    // ------------------------------------------------------------ stages --

    public JsonElement Stage(string name) => Content.RootElement.GetProperty("STAGES").GetProperty(name);
    public string StageEvent(string name) => Content.RootElement.GetProperty("STAGE_EVENTS").GetProperty(name).GetString()!;
    private string CurrentStage => (string)Session.GetProgress("state")["stage"];

    // ----------------------------------------------------------- queries --

    private object State(string key, object fallback) => Session.GetProgress("state").GetValueOrDefault(key, fallback);
    private object LifeField(string who, string key) => Session.GetProgress("life").TryGetValue(who, out var life)
        ? ((Dictionary<string, object>)life).GetValueOrDefault(key, "") : "";
    private bool LifeBusy(string who) => LifeField(who, "state") is "going" or "holding";
    private Dictionary<string, object>? Heard(string who) => ((object[])State("last_sound", Array.Empty<object>()))
        .Cast<Dictionary<string, object>>().FirstOrDefault(h => (string)h["who"] == who);

    /// <summary>剧本条件里能问的所有事实。名字与 compile_story.py 的 QUERIES 一一对应。world 可为空（仅答辩判定等不需要世界的查询）。</summary>
    public object? Query(MorningWorld? world, string name, IReadOnlyList<object?> args)
    {
        string A(int index = 0) => Convert.ToString(args[index]) ?? "";
        bool Has(string group) => Session.Has(group, A());
        MorningWorld W() => world ?? throw new InvalidOperationException("query needs the morning world: " + name);
        return name switch
        {
            "flag" => Has("flags"), "seen" => Has("examined"), "tested" => Has("experiments"), "asked" => Has("helped"),
            "recalled" => Has("recalls"), "compared" => Has("soles_compared"), "v6_open" => V6Open,
            "told_rule" => W().KnowsRule1(PersonOf(A())), "hooks" => W().Hooks(PersonOf(A())), "total_hooks" => W().TotalHooks,
            "matched" => State("full_match_seen", false), "replayed" => Convert.ToInt32(State("replay_count", 0)) > 0,
            "fragment" => Session.GetProgress("recalls").GetValueOrDefault(A(), ""), "alone_choice" => LifeField(A(), "choice"),
            "life_thought" => LifeField(A(), "thought"), "talk_first" => LifeField(A(), "alone") is true && LifeField(A(), "told") is not true,
            "recall_thought" => Session.GetProgress("recall_decision").GetValueOrDefault("thought", ""),
            "wrong_name" => State("wrong_name", ""), "detective_name" => State("detective_name", Chapter1Data.Text("报名·默认名")),
            "addr" => Address(W(), A()), "heard" => HeardText(A()), "heard_where" => SoundSpot(Convert.ToString(Heard(A())?.GetValueOrDefault("room")) ?? ""),
            "sound_sentence" => TestSentences.All[Math.Clamp(Convert.ToInt32(State("sound_sentence", 0)), 0, TestSentences.All.Length - 1)].Full,
            "has" => Session.Has(A(0), A(1)), "count" => Session.GetProgress(A()).Count,
            "value" => Session.GetProgress(A(0)).GetValueOrDefault(A(1)),
            "replay_count" => Convert.ToInt32(State("replay_count", 0)),
            "full_match" => Session.GetProgress("replay_result").TryGetValue("full_match", out var match) && match is true,
            "replay_refused" => ReplayRefused(),
            "in_room" => Session.CharacterRooms.GetValueOrDefault(A(0)) == A(1),
            "local" => Session.IsLocalTarget(A(0), A(1)),
            "busy" => LifeBusy(A()),
            "said" => _judgeSaid ?? PickField("say"), "shown" => _judgeShown ?? PickField("show"),
            "is_person" => Names.PersonKeys.Contains(A()),
            _ => throw new InvalidDataException("Unknown chapter query: " + name),
        };
    }

    private string PickField(string key) => Session.GetProgress("answer_pick").GetValueOrDefault(key, "") as string ?? "";

    /// <summary>重演里侦探对陌生人的请求产生过“不全信”的心声（文案“夜间·怀疑·生人”）。</summary>
    private bool ReplayRefused()
    {
        var replay = Session.GetProgress("replay_result");
        if (!replay.TryGetValue("frames", out var frames)) return false;
        var doubt = Chapter1Data.Text("夜间·怀疑·生人");
        return ((object[])frames).Cast<Dictionary<string, object>>().Any(frame =>
            ((string)((Dictionary<string, object>)frame["thoughts"])["detective"]).Contains(doubt));
    }

    private string Address(MorningWorld world, string who)
        => Chapter1Data.Text("称呼·" + who + (world.Hooks(PersonOf(who)) > 0 ? "·亲近" : "·生疏"));

    private string HeardText(string who)
    {
        var heard = Heard(who);
        if (heard is null) return "";
        var direction = Convert.ToString(heard.GetValueOrDefault("direction", ""));
        return Convert.ToString(heard["text"]) + (direction == "" ? "" : "　" + direction);
    }

    // ------------------------------------------------------- expressions --

    private static bool Truthy(object? value) => value switch
    {
        null => false, bool b => b, string s => s != "", int n => n != 0, long n => n != 0, double n => n != 0, _ => true,
    };

    private static bool IsNumber(object? value) => value is int or long or double;

    private static int Compare(object? left, object? right) => IsNumber(left) && IsNumber(right)
        ? Convert.ToDouble(left).CompareTo(Convert.ToDouble(right))
        : string.CompareOrdinal(Convert.ToString(left), Convert.ToString(right));

    private static bool Same(object? left, object? right) => IsNumber(left) && IsNumber(right)
        ? Convert.ToDouble(left) == Convert.ToDouble(right) : Equals(Convert.ToString(left), Convert.ToString(right));

    private object? Eval(JsonElement node, MorningWorld? world, IReadOnlyDictionary<string, string> args)
    {
        var op = node.GetProperty("op").GetString();
        JsonElement Operand(int index) => node.GetProperty("args")[index];
        switch (op)
        {
            case "literal":
                var value = node.GetProperty("value");
                return value.ValueKind switch
                {
                    JsonValueKind.String => Fill(value.GetString()!, args), JsonValueKind.True => true, JsonValueKind.False => false,
                    JsonValueKind.Number => value.TryGetInt32(out var whole) ? whole : value.GetDouble(), _ => null,
                };
            case "call":
                var operands = node.TryGetProperty("args", out var list) ? list.EnumerateArray().Select(a => Eval(a, world, args)).ToList() : [];
                return Query(world, node.GetProperty("name").GetString()!, operands);
            case "not": return !Truthy(Eval(Operand(0), world, args));
            case "and": return Truthy(Eval(Operand(0), world, args)) && Truthy(Eval(Operand(1), world, args));
            case "or": return Truthy(Eval(Operand(0), world, args)) || Truthy(Eval(Operand(1), world, args));
        }
        var left = Eval(Operand(0), world, args);
        var right = Eval(Operand(1), world, args);
        return op switch
        {
            "eq" => Same(left, right), "ne" => !Same(left, right), "gt" => Compare(left, right) > 0, "gte" => Compare(left, right) >= 0,
            "lt" => Compare(left, right) < 0, "lte" => Compare(left, right) <= 0,
            _ => throw new InvalidDataException("Unknown expression operator: " + op),
        };
    }

    private static string Fill(string text, IReadOnlyDictionary<string, string> args)
    {
        foreach (var (name, value) in args) text = text.Replace("{" + name + "}", value);
        return text;
    }

    // ------------------------------------------------------------- flows --

    public bool HasFlow(string name) => Content.RootElement.GetProperty("FLOWS").TryGetProperty(name, out _);

    /// <summary>按名字起一个流程；args 是流程里 {名称} 的取值。</summary>
    public void StartFlow(MorningWorld world, string name, params (string Name, string Value)[] args)
    {
        var map = args.ToDictionary(a => a.Name, a => a.Value);
        Start(world, BuildFlow(world, name, map));
    }

    private List<Step> BuildFlow(MorningWorld world, string name, Dictionary<string, string> map)
    {
        var flow = Content.RootElement.GetProperty("FLOWS").GetProperty(name);
        if (flow.TryGetProperty("provider", out var provider))
            foreach (var (key, value) in Provide(provider.GetString()!, world, map)) map[key] = value;
        return Steps(flow.GetProperty("steps"), 0, map);
    }

    /// <summary>流程开始时才知道的参数。名字对应 compile_story.py 的 FLOW_PROVIDERS。</summary>
    private Dictionary<string, string> Provide(string provider, MorningWorld world, Dictionary<string, string> map)
    {
        int AnswerStep() => Convert.ToInt32(Session.GetProgress("answer_state")["step"]);
        switch (provider)
        {
            case "协助者":
                return new() { ["人"] = Content.RootElement.GetProperty("EVIDENCE").GetProperty(map["物"]).GetProperty("help").GetString()! };
            case "答辩步":
                return new() { ["步"] = AnswerStep().ToString() };
            case "答辩裁决":
                var step = AnswerStep();
                var result = new Dictionary<string, string> { ["步"] = step.ToString(), ["下一步"] = (step + 1).ToString() };
                var pick = Session.GetProgress("answer_pick");
                if (pick.Count == 0) { result["裁决"] = "none"; return result; }
                var verdict = Judge(step, (string)pick["say"], (string)pick["show"]);
                result["裁决"] = verdict == "answer_ok" + step ? "ok" : verdict.Substring("answer_".Length);
                return result;
            default:
                throw new InvalidDataException("Unknown flow parameter provider: " + provider);
        }
    }

    private static object Parsed(string text) => text.Length > 0 && text.All(char.IsDigit) ? int.Parse(text) : text;

    private object? FlowValue(JsonElement value, IReadOnlyDictionary<string, string> map) => value.ValueKind switch
    {
        JsonValueKind.True => true, JsonValueKind.False => false,
        JsonValueKind.Number => value.TryGetInt32(out var whole) ? whole : value.GetDouble(),
        JsonValueKind.String => Parsed(Fill(value.GetString()!, map)),
        JsonValueKind.Object => Chapter1Data.Text(Fill(value.GetProperty("text").GetString()!, map)),
        _ => throw new InvalidDataException("Unsupported flow value."),
    };

    /// <summary>
    /// 把流程步骤译成故事运行时的步骤。“若……结束”的写法译成：条件成立走分支，不成立才继续后面的步骤。
    /// </summary>
    private List<Step> Steps(JsonElement nodes, int start, IReadOnlyDictionary<string, string> map)
    {
        var steps = new List<Step>();
        var items = nodes.EnumerateArray().ToList();
        for (var i = start; i < items.Count; i++)
        {
            var node = items[i];
            string Str(string field) => Fill(node.GetProperty(field).GetString()!, map);
            switch (node.GetProperty("op").GetString())
            {
                case "show":
                    steps.AddRange(Show(Str("scene")));
                    break;
                case "stage":
                    var stage = Str("value");
                    steps.Add(new Step.Update(_ => Session.Set("state", "stage", stage)));
                    break;
                case "set_flag": var setKey = Str("key"); steps.Add(new Step.Update(_ => Session.Set("flags", setKey, true))); break;
                case "clear_flag": var clearKey = Str("key"); steps.Add(new Step.Update(_ => Session.Remove("flags", clearKey))); break;
                case "record":
                    var (group, key) = (Str("group"), Str("key"));
                    var recorded = FlowValue(node.GetProperty("value"), map)!;
                    steps.Add(new Step.Update(_ => Session.Set(group, key, recorded)));
                    break;
                case "remove": var (rGroup, rKey) = (Str("group"), Str("key")); steps.Add(new Step.Update(_ => Session.Remove(rGroup, rKey))); break;
                case "clear": var cGroup = Str("group"); steps.Add(new Step.Update(_ => Session.GetProgress(cGroup).Clear())); break;
                case "detail":
                    var (dGroup, dKey, field) = (Str("group"), Str("key"), Str("field"));
                    var detail = FlowValue(node.GetProperty("value"), map)!;
                    steps.Add(new Step.Update(_ => ((Dictionary<string, object>)Session.GetProgress(dGroup)[dKey])[field] = detail));
                    break;
                case "continue":
                    var next = Str("flow");
                    var carried = map.ToDictionary(p => p.Key, p => p.Value);
                    steps.Add(new Step.Update(w => StartFlow(w, next, carried.Select(p => (p.Key, p.Value)).ToArray())));
                    break;
                case "stop":
                    return steps;
                case "if":
                    var condition = node.GetProperty("condition");
                    var then = node.GetProperty("then").EnumerateArray().ToList();
                    var otherwise = Steps(node.GetProperty("else"), 0, map);
                    if (then.Count > 0 && then[^1].GetProperty("op").GetString() == "stop")
                    {
                        // 分支以“结束”收尾：后面的步骤只在条件不成立时继续。
                        var body = Steps(JsonSerializer.SerializeToElement(then.Take(then.Count - 1)), 0, map);
                        otherwise.AddRange(Steps(nodes, i + 1, map));
                        steps.Add(new Step.Branch(w => Truthy(Eval(condition, w, map)), body, otherwise));
                        return steps;
                    }
                    steps.Add(new Step.Branch(w => Truthy(Eval(condition, w, map)), Steps(node.GetProperty("then"), 0, map), otherwise));
                    break;
                default:
                    throw new InvalidDataException("Unsupported flow step: " + node.GetProperty("op"));
            }
        }
        return steps;
    }
}
