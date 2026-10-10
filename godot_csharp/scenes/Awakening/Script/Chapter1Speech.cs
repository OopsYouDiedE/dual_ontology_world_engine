using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

namespace DualOntologyWorldEngine.Awakening;

/// <summary>
/// 话题回应：说话人用推理器读自己的认知得出把握，再按言语规则（对听者的信任、是否牵连自己、说话习惯）
/// 选行为，最后取 story 里写给此人或“任何人”的台词（总纲 §六 第二期）。只读本人认知，不读真相与剧情进度。
/// </summary>
public sealed partial class AwakeningContentSession
{
    private static readonly string[] Companions = ["huo", "shen", "su"];

    private JsonElement Topic(string topic) => Content.RootElement.GetProperty("TOPICS").TryGetProperty(topic, out var t) ? t
        : throw new InvalidDataException("Unknown topic: " + topic);

    private static (int Question, int Option) Proposition(JsonElement topic)
    {
        var model = AwakeningCase.Model;
        var question = model.Question(topic.GetProperty("question").GetString()!);
        return (question, model.Option(question, topic.GetProperty("option").GetString()!));
    }

    /// <summary>侦探自己想到第六问（重演被拒或细看过两件相熟旁证）即记入他的认知。</summary>
    private void SyncConception(MorningWorld world)
    {
        var concept = AwakeningCase.Model.ConceptName(AwakeningCase.Model.Question("V6"));
        if (V6Open && !world.Knows(Person.Detective, concept)) world.Conceive(Person.Detective, concept);
    }

    /// <summary>此人对话题命题的把握（0—100），由推理器读其本人认知得出。</summary>
    public int Credence(MorningWorld world, string who, string topic)
    {
        SyncConception(world);
        var (question, option) = Proposition(Topic(topic));
        return (int)Math.Round(100 * Reasoner.Assess(world.WorldOf(PersonOf(who)), AwakeningCase.Model).Of(question, option));
    }

    /// <summary>此人回答侦探这个话题时选的言语行为，返回 story 里对应的回应键。依据记入会话以供审读。</summary>
    public string SpeechKey(MorningWorld world, string who, string topic)
    {
        SyncConception(world);
        var data = Topic(topic);
        var person = PersonOf(who);
        var (question, option) = Proposition(data);
        var belief = Reasoner.Assess(world.WorldOf(person), AwakeningCase.Model);
        var credence = belief.Of(question, option);
        var trust = world.TrustIn(person, Person.Detective);
        var implicated = data.GetProperty("implicates").EnumerateArray().Any(p => p.GetString() == who);
        var responses = data.GetProperty("responses");
        bool Written(string key) => responses.TryGetProperty(who, out var own) && own.TryGetProperty(key, out _)
            || responses.TryGetProperty("any", out var any) && any.TryGetProperty(key, out _);
        string? KeyOf(SpeechAct act, Stance stance) => ResponseCatalog.Select(Chapter1Data.Root.GetProperty("EXPRESSION"), "speech", name => name switch
        {
            "act" => act.ToString(), "stance" => stance.ToString(), "speaker" => who, "listener" => "detective",
            "written" => null,
            _ when name.StartsWith("written:", StringComparison.Ordinal) => Written(name[8..]),
            _ when name.StartsWith("known:", StringComparison.Ordinal) => world.Knows(person, name[6..]),
            _ => null,
        })?.Text;
        var choice = SpeechPolicy.Choose(credence, trust, Persona.Speech(person), implicated, (act, stance) => KeyOf(act, stance) is not null);
        if (choice is null) { Session.Set("speech", who + ":" + topic, "none"); return "none"; }
        var key = KeyOf(choice.Act, choice.Stance)!;
        Session.Set("speech", who + ":" + topic,
            $"{key}；把握 {credence:0.00}；信任 {trust}；{string.Join("，", choice.Reasons.Select(Chapter1Expression.SpeechReason))}；依据：{string.Join("；", belief.Basis.Select(Chapter1Expression.Basis))}");
        return key;
    }

    /// <summary>
    /// 侦探重演昨夜：同处一室的同伴看见他在重演；吻合时侦探把这组认定当作自己推出的，
    /// 被召来目睹收尾的三位同伴各自按对侦探的信任折算这组认定。
    /// </summary>
    public void WitnessReplay(MorningWorld world, NightVariables variables, bool fullMatch)
    {
        var here = Session.CharacterRooms.GetValueOrDefault("detective");
        foreach (var who in Companions)
            if (fullMatch || here is not null && Session.CharacterRooms.GetValueOrDefault(who) == here)
                world.Witness(PersonOf(who), Chapter1Data.Text("认知·正在重演"));
        if (!fullMatch) return;
        var claims = AwakeningCase.Claims(variables).ToArray();
        world.Hear(Person.Detective, Person.Detective, claims);
        foreach (var who in Companions)
        {
            world.Witness(PersonOf(who), Chapter1Data.Text("认知·重演吻合"));
            world.Hear(PersonOf(who), Person.Detective, claims);
        }
    }

    /// <summary>检视时在场的同伴也看见了这件东西（在场即可见）。</summary>
    private void ShareExamination(MorningWorld world, string id)
    {
        var here = Session.CharacterRooms.GetValueOrDefault("detective");
        if (here is null) return;
        foreach (var who in Companions)
            if (Session.CharacterRooms.GetValueOrDefault(who) == here) world.Examine(PersonOf(who), id);
    }
}
