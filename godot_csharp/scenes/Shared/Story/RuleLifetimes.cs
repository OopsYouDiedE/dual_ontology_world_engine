using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Story;

/// <summary>Lifecycle metadata is independent of entity/candidate scope. Exiting records an introspective rule revision.</summary>
public static class RuleLifetimes
{
    public static LifetimeScope Parse(string scope) => scope switch
    {
        "world" => LifetimeScope.World, "chapter" => LifetimeScope.Chapter, "interaction" => LifetimeScope.Interaction,
        _ => throw new InvalidDataException("Unknown rule or trigger lifetime: " + scope)
    };

    public static ImmutableArray<Rule> Bind(IEnumerable<Rule> rules, string scope, string owner)
    {
        var lifetime = Parse(scope);
        return [..rules.Select(r => r with { Lifetime = lifetime, LifetimeOwner = lifetime == LifetimeScope.World ? null : owner })];
    }

    public static void End(IEnumerable<SoulEssence> souls, LifetimeScope scope, string owner, double time)
    {
        if (scope == LifetimeScope.World) throw new ArgumentException("World rules end with the world, not a local exit.");
        foreach (var soul in souls.Distinct())
        foreach (var basis in soul.World.Projections.ToArray())
        {
            var remaining = basis.Rules.Where(r => r.Lifetime != scope || r.LifetimeOwner != owner).ToImmutableArray();
            if (remaining.Length == basis.Rules.Length) continue;
            Observation.Observe(soul, new ObservationInput(basis.Name, basis.Kind)
            {
                Target = basis, Source = ObservationSource.Introspection, Time = time,
                Rules = remaining, Details = "scope-ended:" + scope + ":" + owner
            });
        }
    }

    public static bool EndSources(IEnumerable<Essence> sources, LifetimeScope scope, string owner)
    {
        if (scope == LifetimeScope.World) throw new ArgumentException("World rules end with the world, not a local exit.");
        var changed = false;
        foreach (var source in sources.Distinct())
        {
            if (source.Get<RuleProperty>() is not { } property) continue;
            var remaining = property.Rules.Where(r => r.Lifetime != scope || r.LifetimeOwner != owner).ToImmutableArray();
            if (remaining.Length == property.Rules.Length) continue;
            property.Rules = remaining;
            changed = true;
        }
        return changed;
    }
}
