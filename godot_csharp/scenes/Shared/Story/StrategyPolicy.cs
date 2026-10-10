using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Story;

public sealed record StrategyThreshold(string Tag, int Minimum);
public sealed record CharacterStrategy(string Name, string Target, string Stance, int Priority,
    string Source, string[] Methods, StrategyThreshold[] Conditions);
public sealed record StrategyMethod(string Key, Effect Effect, Condition Available, string? Kind = null);

/// <summary>Authored priorities over methods, evaluated by the actor's existing DecisionSystem.</summary>
public static class StrategyPolicy
{
    public static CharacterStrategy[] Read(JsonElement profile)
    {
        if (!profile.TryGetProperty("strategies", out var value)) return [];
        CheckUniqueFields(value);
        var strategies = JsonSerializer.Deserialize<CharacterStrategy[]>(value.GetRawText(), new JsonSerializerOptions
            { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow })
            ?? throw new InvalidDataException("Missing strategies.");
        Validate(strategies);
        return strategies;
    }
    private static void CheckUniqueFields(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate strategy field.");
                CheckUniqueFields(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var child in value.EnumerateArray()) CheckUniqueFields(child);
    }
    private static void Validate(IReadOnlyList<CharacterStrategy> strategies)
    {
        if (strategies.Count is < 1 or > 3 || strategies.Select(s => s.Name).Distinct(StringComparer.Ordinal).Count() != strategies.Count)
            throw new InvalidDataException("Invalid strategy count or duplicate name.");
        foreach (var strategy in strategies)
            if (new[] { strategy.Name, strategy.Target, strategy.Stance, strategy.Source }.Any(string.IsNullOrWhiteSpace)
                || strategy.Priority is < 0 or > 1000 || strategy.Methods is null || strategy.Methods.Length == 0
                || strategy.Methods.Any(string.IsNullOrWhiteSpace) || strategy.Methods.Distinct(StringComparer.Ordinal).Count() != strategy.Methods.Length
                || strategy.Conditions is null || strategy.Conditions.Any(c => string.IsNullOrWhiteSpace(c.Tag) || c.Minimum < 0))
                throw new InvalidDataException("Invalid strategy fields.");
    }

    public static Tag SourceTag(string source) => new("strategy-source:" + source);

    public static void Acquire(SoulEssence soul, Projection self, string source, double time, string details,
        ObservationSource observationSource = ObservationSource.Perception)
    {
        if (!soul.World.Contains(self)) throw new ArgumentException("Foreign strategy actor.");
        var tag = SourceTag(source);
        if (self.Tags.GetValueOrDefault(tag) == 1) return;
        Observation.Observe(soul, new(self.Name, self.Kind) { Target = self, Source = observationSource,
            Time = time, Details = details, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(tag, 1) });
    }

    public static void Bind(SoulEssence soul, Projection self, IReadOnlyList<CharacterStrategy> strategies,
        IReadOnlyList<StrategyMethod> methods, string domain, double time, string? lifetimeOwner = null)
    {
        if (!soul.World.Contains(self)) throw new ArgumentException("Foreign strategy actor.");
        if (strategies.Count == 0) return;
        Validate(strategies);
        if (methods.Select(m => m.Key).Distinct(StringComparer.Ordinal).Count() != methods.Count
            || methods.Any(m => Ast.Nouns(new Rule(m.Available, RuleMode.Prefer, m.Effect)).OfType<Noun.Reference>().Any(n => !soul.World.Contains(n.Projection))))
            throw new ArgumentException("Duplicate method or foreign method reference.");
        foreach (var strategy in strategies)
        {
            Condition active = new Condition.Tagged(self, SourceTag(strategy.Source), 1);
            foreach (var threshold in strategy.Conditions)
                active = new Condition.All([active, new Condition.Tagged(self, new Tag(threshold.Tag), threshold.Minimum)]);
            var rules = methods.Where(m => strategy.Methods.Contains(m.Kind ?? m.Key, StringComparer.Ordinal)).Select(m =>
                new Rule(new Condition.All([active, m.Available]), RuleMode.Prefer, m.Effect, strategy.Priority, Truth.True,
                    LifetimeScope.Chapter, lifetimeOwner ?? domain));
            var name = domain + ":" + strategy.Name;
            var known = soul.World.Projections.FirstOrDefault(p => p.Name == name);
            Observation.Observe(soul, new(name, ProjectionKind.Concept) { Target = known,
                Source = ObservationSource.Introspection, Time = time, Existence = Truth.True,
                Details = strategy.Target + "/" + strategy.Stance, Rules = [.. rules] });
        }
    }
}
