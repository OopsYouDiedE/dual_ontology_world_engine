using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
using static DualOntologyWorldEngine.Story.ChapterEvents;

namespace DualOntologyWorldEngine.Awakening;

public sealed partial class Chapter1Session
{
    private static readonly Lazy<IReadOnlyDictionary<string, int>> PlayerSpeakerTrust = new(() =>
    {
        using var stream = typeof(Chapter1Data).Assembly.GetManifestResourceStream("Awakening.World.json")!;
        using var document = System.Text.Json.JsonDocument.Parse(stream);
        var chapter = document.RootElement.GetProperty("chapters")[0];
        var entities = chapter.GetProperty("entities").EnumerateArray().Where(e => e.GetProperty("kind").GetString() == "character").ToArray();
        var people = chapter.GetProperty("design").GetProperty("people");
        var player = ContentPlayerKey(entities);
        var result = new Dictionary<string, int>();
        if (people.GetProperty(player).TryGetProperty("trust", out var trust))
            foreach (var value in trust.EnumerateObject())
            {
                var entity = entities.First(e => e.GetProperty("id").GetString()!.Split('.').Last() == value.Name);
                result[entity.GetProperty("name").GetString()!] = value.Value.GetInt32();
            }
        return result;
        static string ContentPlayerKey(System.Text.Json.JsonElement[] entities)
        {
            var sprite = Chapter1Data.Root.GetProperty("PRESENTATION").GetProperty("player_actor").GetString();
            return entities.First(e => e.GetProperty("properties").TryGetProperty("sprite", out var property)
                && property.GetProperty("value").GetString() == sprite).GetProperty("id").GetString()!.Split('.').Last();
        }
    });
    private void ObserveShown(IReadOnlyDictionary<string, object?> ev)
    {
        IEnumerable<IReadOnlyDictionary<string, object?>> Rows(string key)
            => (ev.GetValueOrDefault(key) as IEnumerable<object?> ?? []).OfType<IReadOnlyDictionary<string, object?>>();
        var time = _content.GameMinutes;
        if (Text(ev, "kind") == "card" && Text(ev, "facts") != "")
        {
            var id = Text(ev, "id");
            var evidence = Content.GetProperty("EVIDENCE");
            if (evidence.TryGetProperty(id, out var entry))
            {
                var name = entry.TryGetProperty("mind_name", out var mind) ? mind.GetString() : entry.GetProperty("name").GetString();
                var physical = _world.WorldOf(Person.Detective).Projections.FirstOrDefault(p => p.Name == name && p.Kind != ProjectionKind.Concept);
                _knowledge.Acquire("card", id, Text(ev, "title"), time, physicalSource: physical);
            }
        }
        foreach (var line in Rows("lines"))
        {
            var key = Text(line, "line_id"); var speaker = Text(line, "speaker");
            var person = Content.GetProperty("PERSON_NAMES").EnumerateObject()
                .Where(p => p.Value.GetString() == speaker).Select(p => p.Name).FirstOrDefault();
            var perceived = person is null ? null : _knowledge.ObserveSpeaker(speaker, PlayerSpeakerTrust.Value.GetValueOrDefault(speaker), time);
            var trust = perceived is null ? 100 : Math.Clamp(perceived.Tags.GetValueOrDefault(Tags.Trust) * 100 / 3, 0, 100);
            _knowledge.Acquire("line", key, Text(line, "text"), time, speaker, perceivedSpeaker: perceived, trust: trust);
            _knowledge.Acquire("question", key, Text(line, "text"), time, speaker);
        }
        foreach (var option in Rows(Text(ev, "kind") == "lines" ? "choices" : "options").Where(o => Flag(o, "available", true)))
            _knowledge.Acquire("choice", Text(option, "id"), Text(option, "label"), time);
        // A tabbed form initially draws only its first field. The port calls the
        // same observer when a player/agent switches to another visible tab.
        foreach (var field in Rows("fields").Take(Text(ev, "layout") == "tabs" ? 1 : int.MaxValue))
            ObserveFormField(field);
    }
    public void ObservePlayerFormField(IReadOnlyDictionary<string, object?> field) => ObserveFormField(field);
    private void ObserveFormField(IReadOnlyDictionary<string, object?> field)
    {
        var shown = (field.GetValueOrDefault("options") as IEnumerable<object?> ?? []).OfType<IReadOnlyDictionary<string, object?>>().ToArray();
        if (Text(field, "purpose") == "evidence")
            _knowledge.ObservePresentableOptions(shown.Select(option => Text(option, "label")), _content.GameMinutes);
        foreach (var option in shown)
            _knowledge.Acquire("statement", Text(option, "id"), Text(option, "label"), _content.GameMinutes);
    }
    public void ObservePlayerOffers(IReadOnlyList<PlayerFieldOffer> offers)
    {
        foreach (var offer in offers)
        {
            var site = SourceSite(Text(offer.Input, "site"));
            if (site != "") _knowledge.Acquire("field", site, offer.Label, _content.GameMinutes);
        }
    }
    private void ObserveOpenedPanel(string panel)
    {
        if (panel != "manuscript") return;
        var shown = (ReplaySnapshot().GetValueOrDefault("questions") as IEnumerable<object?> ?? []).OfType<IReadOnlyDictionary<string, object?>>().ToArray();
        var variables = Content.GetProperty("VARIABLES").EnumerateArray().ToArray();
        for (var i = 0; i < shown.Length; i++)
        {
            var index = 0;
            foreach (var option in (shown[i].GetValueOrDefault("options") as IEnumerable<object?> ?? []).OfType<string>())
                _knowledge.Acquire("variable", variables[i].GetProperty("key").GetString() + ":" + index++, option, _content.GameMinutes);
        }
    }
}
