using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Story;

public sealed class KnowledgeDeclaration
{
    public Dictionary<string, string> Propositions { get; set; } = [];
    public List<KnowledgeBinding> Bindings { get; set; } = [];
    public List<KnowledgeImplication> Implications { get; set; } = [];
}
public sealed class KnowledgeBinding
{
    public string Kind { get; set; } = "";
    public string Key { get; set; } = "";
    public string Source { get; set; } = "";
    public int Reliability { get; set; }
    public string Speaker { get; set; } = "";
    public string Act { get; set; } = "";
    public List<string> Propositions { get; set; } = [];
}
public sealed class KnowledgeImplication
{
    public List<string> Premises { get; set; } = [];
    public string Conclusion { get; set; } = "";
}

/// <summary>Trusted presentation adapter. Acquisition is called only for delivered
/// text/visible options, never for declarations alone. Atoms have no truth tags.
/// Learned content and source availability are independent subject projections.</summary>
public sealed class AcquiredPropositions(KnowledgeDeclaration declaration, SoulEssence soul, int acceptanceThreshold = 60)
{
    public static readonly Tag Reliability = new("SourceReliability");
    public static readonly Tag Proposition = new("Proposition");
    public static readonly Tag Opportunity = new("ObservationOpportunity");
    public static readonly Tag Question = new("CurrentQuestion");
    public static readonly Tag Presentable = new("PresentableMaterial");
    private static readonly Tag Asserted = new("Asserted");
    private static readonly RelationTag SourceObject = new("SourceObject");
    private static readonly RelationTag Speaker = new("Speaker");
    public Projection ObserveSpeaker(string label, int initialTrust, double time)
    {
        var previous = soul.World.Projections.FirstOrDefault(p => p.Name == label && p.Kind == ProjectionKind.Body);
        if (previous?.Tags.ContainsKey(Tags.Trust) == true) return previous;
        return Observation.Observe(soul, new(label, ProjectionKind.Body)
        {
            Target = previous, Source = ObservationSource.Testimony, Time = time,
            TimeUnit = ObservationTimeUnitV1.GameMinutes, Existence = Truth.True,
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, initialTrust)
        });
    }
    public static KnowledgeDeclaration Read(JsonElement root) => root.TryGetProperty("KNOWLEDGE", out var data)
        ? JsonSerializer.Deserialize<KnowledgeDeclaration>(data.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!
        : new();
    private Projection? Find(string key) => soul.World.Projections.FirstOrDefault(p => p.KnownTerms.Any(t => t.TermId.Value == "acquired." + key));
    private static KnownAuthorTermV1 Term(string key, string text)
    {
        var raw = string.IsNullOrWhiteSpace(text) ? "acquired." + key : text;
        return new(new("acquired." + key), raw, raw.Normalize(NormalizationForm.FormC));
    }
    private Projection Node(string key, string label, double time, Tag? tag = null)
    {
        var previous = Find(key);
        if (previous is not null && (label == "" || previous.Name == label)) return previous;
        return Observation.Observe(soul, new(label, ProjectionKind.Concept)
        {
            Target = previous, Source = ObservationSource.Introspection, Time = time, Existence = Truth.True,
            KnownTerms = [Term(key, label)],
            Tags = tag is null ? ImmutableDictionary<Tag, int?>.Empty : ImmutableDictionary<Tag, int?>.Empty.Add(tag.Value, 1)
        });
    }
    private Condition Atom(string key, double time)
    {
        var negative = key.StartsWith('!'); var id = negative ? key[1..] : key;
        if (!declaration.Propositions.ContainsKey(id)) throw new InvalidDataException("Undeclared proposition.");
        Condition atom = new Condition.Tagged(Node("atom." + id, declaration.Propositions[id], time, Proposition), Asserted, 1);
        return negative ? new Condition.Not(atom) : atom;
    }
    public void Acquire(string kind, string key, string visibleText, double time, string speaker = "",
        Projection? physicalSource = null, Projection? perceivedSpeaker = null, int trust = 100)
    {
        foreach (var b in declaration.Bindings.Where(b => b.Kind == kind && b.Key == key && b.Speaker == speaker))
        {
            var identity = kind + "." + key;
            var previous = Find(identity);
            var proposal = b.Source == "proposal";
            var source = b.Source switch { "perception" => ObservationSource.Perception,
                "testimony" => ObservationSource.Testimony, "memory" => ObservationSource.Recall,
                "proposal" => ObservationSource.Introspection, _ => throw new InvalidDataException("Unknown acquisition source.") };
            var effectiveReliability = b.Source == "testimony" ? b.Reliability * Math.Clamp(trust, 0, 100) / 100 : b.Reliability;
            if (previous is not null && previous.Name == visibleText && previous.Tags.GetValueOrDefault(Reliability) == effectiveReliability
                && (kind != "question" || previous.Tags.GetValueOrDefault(Question) == 1)) return;
            if (kind == "question") ClearQuestions(time);
            var acceptance = !proposal && (b.Act is "assert" or "recall") && effectiveReliability >= acceptanceThreshold ? Truth.True : Truth.Unknown;
            var rules = b.Propositions.Select(p => new Rule(new Condition.Constant(Truth.True), RuleMode.Infer,
                new Effect.Think(Atom(p, time)), Acceptance: acceptance)).ToImmutableArray();
            // Learn conditional relationships only along the acquired vocabulary.
            // They are not unconditional answers and cannot supply a missing fact.
            var reachable = b.Propositions.Select(p => p.TrimStart('!')).ToHashSet();
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var law in declaration.Implications.Where(l => l.Premises.Any(p => reachable.Contains(p.TrimStart('!')))))
                    if (reachable.Add(law.Conclusion.TrimStart('!'))) changed = true;
            }
            foreach (var (law, index) in declaration.Implications.Select((law, index) => (law, index))
                .Where(pair => pair.law.Premises.Any(p => reachable.Contains(p.TrimStart('!')))))
            {
                var lawKey = "laws." + index;
                if (Find(lawKey) is not null) continue;
                var rule = new Rule(new Condition.All([..law.Premises.Select(p => Atom(p, time))]), RuleMode.Infer,
                    new Effect.Think(Atom(law.Conclusion, time)), Acceptance: Truth.True);
                Observation.Observe(soul, new("", ProjectionKind.Procedure)
                { Source = ObservationSource.Inference, Time = time, Existence = Truth.True,
                    Rules = [rule], KnownTerms = [Term(lawKey, "")] });
            }
            var tags = ImmutableDictionary<Tag, int?>.Empty.Add(Reliability, effectiveReliability);
            if (kind == "card") tags = tags.Add(Presentable, 1);
            if (kind is "choice" or "field") tags = tags.Add(Opportunity, 1);
            if (kind == "question") tags = tags.Add(Question, 1);
            if (kind is "statement" or "variable") tags = tags.Add(Proposition, 1);
            Observation.Observe(soul, new(visibleText, ProjectionKind.Concept)
            {
                Target = previous, Source = source, Time = time, TimeUnit = ObservationTimeUnitV1.GameMinutes,
                Existence = Truth.True, Rules = rules, Tags = tags,
                KnownTerms = [Term(identity, visibleText)],
                AddRelations = (physicalSource is null ? ImmutableHashSet<(RelationTag, Projection)>.Empty : [(SourceObject, physicalSource)])
                    .Union(perceivedSpeaker is null ? [] : [(Speaker, perceivedSpeaker)]),
                Details = b.Act,
            });
        }
    }
    public void ClearQuestions(double time)
    {
        foreach (var p in soul.World.Projections.Where(p => p.Tags.ContainsKey(Question)).ToArray())
            Observation.Observe(soul, new(p.Name, p.Kind) { Target = p, Time = time, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Question, null) });
    }
    public void SourceAvailability(Projection physicalSource, bool exists, double time)
    {
        foreach (var p in soul.World.Projections.Where(p => p.Relations.Contains((SourceObject, physicalSource))).ToArray())
            Observation.Observe(soul, new(p.Name, p.Kind) { Target = p, Time = time,
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Presentable, exists ? 1 : 0) });
    }
    public void ObservePresentableOptions(IEnumerable<string> labels, double time)
    {
        var visible = labels.ToHashSet(StringComparer.Ordinal);
        foreach (var binding in declaration.Bindings.Where(b => b.Kind == "card"))
        {
            var material = Find("card." + binding.Key);
            if (material is null) continue;
            var available = visible.Contains(material.Name) ? 1 : 0;
            if (material.Tags.GetValueOrDefault(Presentable) == available) continue;
            Observation.Observe(soul, new(material.Name, material.Kind) { Target = material, Time = time,
                Source = ObservationSource.Perception, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Presentable, available) });
        }
    }
    public void RefreshAvailability(double time)
    {
        foreach (var p in soul.World.Projections.Where(p => p.Tags.ContainsKey(Presentable)).ToArray())
        {
            var objects = p.Relations.Where(r => r.Relation == SourceObject).Select(r => r.Target).ToArray();
            if (objects.Length > 0 && objects.Any(o => o.Existence == Truth.False) && p.Tags[Presentable] != 0)
                Observation.Observe(soul, new(p.Name, p.Kind) { Target = p, Time = time,
                    Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Presentable, 0) });
        }
    }
}
