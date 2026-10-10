using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed record ResponsePresentation(string Id, string Text, JsonElement Declaration)
{
    public bool Responded => Id != "none";
}

public sealed partial class Game
{
    /// <summary>Conditions read the speaker's projections, never the authority's object tags.</summary>
    public ResponsePresentation Respond(string group, Person? speaker, Person? listener = null,
        IReadOnlyDictionary<string, object?>? context = null, params (string Name, object? Value)[] args)
    {
        object? Read(string name)
        {
            if (name == "speaker") return speaker?.Id.ToString() ?? "";
            if (name == "listener") return listener?.Id.ToString() ?? "";
            if (name.StartsWith("listener.lesson:", StringComparison.Ordinal)) return listener?.Lessons.ContainsKey(int.Parse(name[16..]));
            if (name.StartsWith("lesson:", StringComparison.Ordinal)) return speaker?.Lessons.ContainsKey(int.Parse(name[7..]));
            if (name.StartsWith("known:", StringComparison.Ordinal)) return speaker?.World.Projections.Any(p => p.Name == name[6..] && p.Existence == Truth.True);
            if (name.StartsWith("tag:", StringComparison.Ordinal))
            {
                if (speaker is null) return null;
                var parts = name[4..].Split(':');
                if (parts.Length != 2) throw new InvalidDataException("Malformed expression tag: " + name);
                var target = parts[0] == "voice" ? CopperVoice : parts[0] == "self" ? speaker.Body : Chapter2Bindings.ResolveId(this, parts[0]);
                return speaker.M.TryGetValue(target, out var projection) && projection.Existence == Truth.True ? projection.Tags.GetValueOrDefault(new Tag(parts[1])) : null;
            }
            return context?.GetValueOrDefault(name);
        }
        var selected = ResponseCatalog.Select(Chapter2Design.Section("expression"), group, Read);
        return selected is null ? new("none", "", default) : new(selected.Id, RenderText(Chapter2Design.Text(selected.Text, args), speaker, listener), selected.Declaration);
    }

    public string IdentityName(string identity, Person? speaker = null, Person? listener = null)
    {
        var result = Respond("identity." + identity, speaker ?? Det, listener);
        return result.Responded ? result.Text : "";
    }

    public string RenderText(string text, Person? speaker = null, Person? listener = null)
    {
        foreach (var group in Chapter2Design.Section("expression").EnumerateObject().Where(g => g.Name.StartsWith("identity.", StringComparison.Ordinal)))
        {
            var identity = group.Name[9..];
            var token = "{称呼:" + identity + "}";
            if (text.Contains(token, StringComparison.Ordinal)) text = text.Replace(token, IdentityName(identity, speaker, listener), StringComparison.Ordinal);
        }
        return text;
    }

    private Dictionary<Essence, string>? _expressionObjects;
    private string ExpressionObject(Essence entity)
    {
        _expressionObjects ??= Chapter2Design.Chapter.GetProperty("entities").EnumerateArray()
            .Where(e => e.GetProperty("kind").GetString() == "object")
            .Select(e => e.GetProperty("id").GetString()!.Replace("world:", "", StringComparison.Ordinal))
            .ToDictionary(id => Chapter2Bindings.ResolveId(this, id), id => id);
        return _expressionObjects.GetValueOrDefault(entity, "");
    }
}
