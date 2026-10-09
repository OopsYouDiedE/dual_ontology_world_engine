using DualOntologyWorldEngine.Story;
namespace DualOntologyWorldEngine.Awakening;

/// <summary>Authoritative field interaction and evidence-card choices. Geometry is supplied by the host.</summary>
public sealed class Chapter1Interaction(AwakeningContentSession content, MorningWorld world)
{
    private static string Text(string key, params (string Name, string Value)[] args) => Chapter1Data.Text(key, args);
    private string? _card;
    private HashSet<string> _geometricPeople = new();
    private static InteractionPacket Commands(params WorldCommand[] commands) => new([], [], PresentationMode.World, commands);
    public InteractionPacket Interact(string siteId)
    {
        _card = null;
        var routes = content.Content.RootElement.GetProperty("SITE_INTERACTIONS");
        if (!routes.TryGetProperty(siteId, out var route)) throw new ArgumentException("Unknown interaction site: " + siteId);
        var kind = route.GetProperty("kind").GetString()!;
        var target = route.GetProperty("target").GetString()!;
        return kind switch
        {
            "evidence" => Commands(new WorldCommand("card", [target])),
            "person" => Commands(new WorldCommand("talk", [target])),
            "board" => Commands(new WorldCommand("field_board", [])),
            "trace" => Commands(new WorldCommand("trace", [target])),
            "tube" => Commands(new WorldCommand("tube", [])),
            _ => throw new InvalidDataException("Unknown interaction kind: " + kind),
        };
    }
    public InteractionPacket CardPacket(string id, IEnumerable<string> geometricAvailablePeople)
    {
        var evidence = content.Content.RootElement.GetProperty("EVIDENCE").GetProperty(id);
        _card = id;
        _geometricPeople = geometricAvailablePeople.ToHashSet(StringComparer.Ordinal);
        string Name(string who) => content.Content.RootElement.GetProperty("PERSON_NAMES").GetProperty(who).GetString()!;
        var choices = new List<InteractionChoice>();
        void Add(string kind, string label, bool available, string hint = "", string who = "") =>
            choices.Add(new($"card:{id}:{kind}" + (who == "" ? "" : ":" + who), label, available, available ? "" : hint));
        // STORY-HARDCODE[AWK-035][logic][production]: E11 手印卡有先比自己、再比在场同伴的本章专属门槛。
        if (id == "E11")
        {
            foreach (var who in new[] { "detective", "huo", "shen", "su" })
            {
                // 先比自己的手；知道“不是我的”之后，才会想到借在场同伴的手来比。不在场的人不列。
                if (who != "detective" && (!content.Session.Has("hand_comparisons", "detective") || !_geometricPeople.Contains(who))) continue;
                var compared = content.Session.Has("hand_comparisons", who);
                var busy = content.Session.GetProgress("life").TryGetValue(who, out var life) &&
                    ((Dictionary<string, object>)life).GetValueOrDefault("state") is "going" or "holding";
                var local = content.Session.CharacterRooms.GetValueOrDefault("detective") == "R05" && content.Session.IsLocalTarget("detective", who);
                Add("compare", who == "detective" ? Text("卡片·比对自己") : Text("卡片·比对他人", ("人名", Name(who))),
                    !compared && !busy && local && _geometricPeople.Contains(who),
                    compared ? Text("卡片·提示·已比对") : Text("提示·比对站位"), who);
            }
        }
        else if (evidence.TryGetProperty("experiment", out var experiment) && (id != "E8" || content.SoundTestThinkable))
            Add("experiment", experiment.GetString()! + (id == "E8" && content.Session.Has("experiments", id) ? Text("卡片·再做一次") : ""),
                content.CanExperiment(id), Text("卡片·提示·试验已做"));
        Add("close", Text("卡片·返回"), true);
        return new([], choices, PresentationMode.EvidenceCard, [], Card: new(id, evidence.GetProperty("name").GetString()!,
            content.CardFacts(id), evidence.GetProperty("source").GetString()!, evidence.GetProperty("picture").GetString()!, content.CardNotes(world, id)));
    }
    public InteractionPacket Choose(string choiceId, IEnumerable<string>? geometricAvailablePeople = null)
    {
        if (_card is null) throw new InvalidOperationException("No active evidence card.");
        var packet = CardPacket(_card, geometricAvailablePeople ?? _geometricPeople);
        var choice = packet.Choices.SingleOrDefault(c => c.Id == choiceId) ?? throw new ArgumentException("Choice does not belong to the active card.");
        if (!choice.IsAvailable) return Commands(new WorldCommand("aside", [choice.LockHint]), new WorldCommand("card", [_card]));
        var parts = choiceId.Split(':');
        var target = _card;
        _card = null;
        return parts[2] switch
        {
            "experiment" => Commands(new WorldCommand("experiment", [target])), "help" => Commands(new WorldCommand("help", [target])),
            "recall" => Commands(new WorldCommand("recall", [target])), "compare" => Commands(new WorldCommand("compare", [parts[3]])),
            "close" => Commands(new WorldCommand("close", [])), _ => throw new ArgumentException("Unknown evidence choice."),
        };
    }
}
