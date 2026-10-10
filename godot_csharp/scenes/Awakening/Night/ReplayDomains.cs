using System.Collections.Immutable;
using System.Text.Json;

namespace DualOntologyWorldEngine.Awakening;

/// <summary>Authored domains for the existing six replay variables. Integers remain wire-compatible option indices.</summary>
public sealed class ReplayDomains
{
    public sealed record Value(string Id, string Display, string Binding);
    private readonly ImmutableDictionary<string, ImmutableArray<Value>> _variables;
    private readonly JsonElement _content;
    public static ReplayDomains Default { get; } = new(Chapter1Data.Root);
    public ReplayDomains(JsonElement content)
    {
        _content = content.Clone();
        var variables = new Dictionary<string, ImmutableArray<Value>>(StringComparer.Ordinal);
        foreach (var row in content.GetProperty("VARIABLES").EnumerateArray())
        {
            var key = row.GetProperty("key").GetString()!;
            var ids = row.GetProperty("value_ids").EnumerateArray().Select(v => v.GetString()!).ToArray();
            var labels = row.GetProperty("options").EnumerateArray().Select(v => v.GetString()!).ToArray();
            var bindings = row.GetProperty("bindings").EnumerateArray().Select(v => v.GetString()!).ToArray();
            if (ids.Length == 0 || ids.Length != labels.Length || ids.Length != bindings.Length
                || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length || ids.Concat(bindings).Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException("Invalid replay domain: " + key);
            if (!variables.TryAdd(key, [.. ids.Select((id, i) => new Value(id, labels[i], bindings[i]))]))
                throw new InvalidDataException("Duplicate replay variable: " + key);
        }
        _variables = variables.ToImmutableDictionary(StringComparer.Ordinal);
        // Existing enums are compatibility names, never the source of the option domain.
        ValidateLegacy<SuPlace>("V1"); ValidateLegacy<HuoPlace>("V2"); ValidateLegacy<VoiceOrigin>("V3");
        ValidateLegacy<VoiceWords>("V4"); ValidateLegacy<DetectiveHeeds>("V5"); ValidateLegacy<Acquaintance>("V6");
        foreach (var value in Values("V1").Concat(Values("V2"))) _ = Names.RoomOf(value.Binding);
        foreach (var value in Values("V3"))
            if (!Enum.TryParse<VoiceOrigin>(value.Binding, out var origin) || !Enum.IsDefined(origin))
                throw new InvalidDataException("Unknown replay origin binding: " + value.Binding);
        foreach (var value in Values("V4"))
            if (!content.GetProperty("UTTERANCES").TryGetProperty(value.Binding, out _))
                throw new InvalidDataException("Unknown replay utterance binding: " + value.Binding);
        foreach (var value in Values("V5")) _ = Names.PersonOf(value.Binding);
        foreach (var value in Values("V6"))
            if (!bool.TryParse(value.Binding, out _)) throw new InvalidDataException("Invalid replay boolean binding: " + value.Binding);
    }
    private void ValidateLegacy<T>(string key) where T : struct, Enum
    {
        var values = Values(key);
        foreach (var legacy in Enum.GetValues<T>())
        {
            var index = Convert.ToInt32(legacy);
            if (index >= values.Length || values[index].Id != legacy.ToString())
                throw new InvalidDataException("Legacy replay ID moved: " + key + "/" + legacy);
        }
    }
    public ImmutableArray<Value> Values(string key) => _variables.TryGetValue(key, out var values) ? values
        : throw new InvalidDataException("Unknown replay variable: " + key);
    public Value At(string key, int index) => index >= 0 && index < Values(key).Length ? Values(key)[index]
        : throw new InvalidDataException("Unknown replay value: " + key + "/" + index);
    public Utterance Words(int index) => Utterance.From(_content.GetProperty("UTTERANCES").GetProperty(At("V4", index).Binding));
}
