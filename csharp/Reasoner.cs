using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Core;

/// <summary>One observable trace: a projection a mind can hold and the tag read from it.</summary>
public sealed record CaseField(string Projection, Tag Tag, string Key);

/// <summary>One combination of answers and the traces it would have left.</summary>
public sealed record CaseHypothesis(ImmutableArray<int> Options, ImmutableDictionary<string, int> Facts);

/// <summary>
/// A shared calculation sheet for one case: questions, their exclusive options, and the predicted
/// traces of every combination. It belongs to no one's cognition; each mind filters it with its own
/// observations through <see cref="Reasoner"/>.
/// </summary>
public sealed class CaseModel
{
    public CaseModel(string id, IEnumerable<string> questions, IEnumerable<IEnumerable<string>> options,
        IEnumerable<CaseField> fields, IEnumerable<CaseHypothesis> hypotheses, IEnumerable<int?>? defaults = null)
    {
        Id = id;
        Questions = [.. questions];
        Options = [.. options.Select(o => o.ToImmutableArray())];
        Fields = [.. fields];
        Hypotheses = [.. hypotheses];
        Defaults = defaults is null ? [.. Questions.Select(_ => (int?)null)] : [.. defaults];
        if (Defaults.Length != Questions.Length) throw new ArgumentException("One default (or none) per question.");
        if (Questions.Length != Options.Length || Hypotheses.Any(h => h.Options.Length != Questions.Length))
            throw new ArgumentException("Every hypothesis must choose one option per question.");
        if (Hypotheses.Any(h => Fields.Any(f => !h.Facts.ContainsKey(f.Key))))
            throw new ArgumentException("Every hypothesis must predict every trace field.");
    }

    public string Id { get; }
    public ImmutableArray<string> Questions { get; }
    public ImmutableArray<ImmutableArray<string>> Options { get; }
    public ImmutableArray<CaseField> Fields { get; }
    public ImmutableArray<CaseHypothesis> Hypotheses { get; }
    /// <summary>An unwritten default answer a mind assumes until it has conceived the question at all.</summary>
    public ImmutableArray<int?> Defaults { get; }

    /// <summary>The concept a mind holds once it has thought of the question itself.</summary>
    public string ConceptName(int question) => $"{Id}:{Questions[question]}";

    public int Question(string key) => Questions.IndexOf(key) is var i and >= 0 ? i
        : throw new KeyNotFoundException("Unknown case question: " + key);
    public int Option(int question, string text) => Options[question].IndexOf(text) is var i and >= 0 ? i
        : throw new KeyNotFoundException($"Unknown option for {Questions[question]}: {text}");

    /// <summary>The concept name a mind holds when it has been told or has inferred an answer.</summary>
    public string ClaimName(int question, int option) => $"{Id}:{Questions[question]}={Options[question][option]}";
}

/// <summary>A mind's current reading of a case.</summary>
public sealed record CaseBelief(CaseModel Model, ImmutableArray<int> Candidates,
    ImmutableArray<ImmutableArray<double>> Credence, bool Contradicted, ImmutableArray<string> Basis)
{
    public double Of(int question, int option) => Credence[question][option];
}

/// <summary>
/// The shared reasoning faculty. It reads only the given mind: traces it has perceived narrow the
/// hypotheses; claims it holds weigh them by source (own inference, recall, testimony scaled by trust
/// in the speaker). Words of corruption are not evidence. Nothing here touches reality.
/// </summary>
public static class Reasoner
{
    public static CaseBelief Assess(ProjectionWorld mind, CaseModel model)
    {
        var basis = new List<string>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var field in model.Fields)
        {
            var projection = mind.Projections.FirstOrDefault(p => p.Name == field.Projection && p.Existence != Truth.False);
            if (projection is not null && projection.Tags.TryGetValue(field.Tag, out var value))
            {
                seen[field.Key] = value;
                basis.Add($"亲见：{field.Projection}·{field.Tag.Name}={value}");
            }
        }
        // A question the mind has not conceived stays at its unwritten default.
        var assumed = new Dictionary<int, int>();
        for (var q = 0; q < model.Questions.Length; q++)
        {
            if (model.Defaults[q] is not { } fallback) continue;
            var question = q;
            var conceived = Holds(mind, model.ConceptName(q))
                || Enumerable.Range(0, model.Options[q].Length).Any(o => Holds(mind, model.ClaimName(question, o)));
            if (conceived) continue;
            assumed[q] = fallback;
            basis.Add($"尚未想到：{model.Questions[q]}（默认 {model.Options[q][fallback]}）");
        }
        var conceivable = Enumerable.Range(0, model.Hypotheses.Length)
            .Where(i => assumed.All(a => model.Hypotheses[i].Options[a.Key] == a.Value)).ToArray();
        int Contradictions(CaseHypothesis h) => seen.Count(f => h.Facts[f.Key] != f.Value);
        var minimum = conceivable.Min(i => Contradictions(model.Hypotheses[i]));
        var contradicted = minimum > 0;
        var candidates = conceivable.Where(i => Contradictions(model.Hypotheses[i]) == minimum).ToImmutableArray();
        basis.Add(contradicted
            ? $"所见与任何组合都不完全相容；暂取矛盾最少的 {candidates.Length} 种"
            : $"与所见相容的组合 {candidates.Length}/{model.Hypotheses.Length}");

        var weights = candidates.ToDictionary(i => i, _ => 1.0);
        for (var q = 0; q < model.Questions.Length; q++)
            for (var o = 0; o < model.Options[q].Length; o++)
            {
                var claim = mind.Projections.FirstOrDefault(p => p.Name == model.ClaimName(q, o) && p.Existence == Truth.True);
                if (claim is null) continue;
                var (factor, why) = Weight(claim);
                if (factor <= 1) { basis.Add($"不采信：{claim.Name}（{why}）"); continue; }
                basis.Add($"采信：{claim.Name}（{why}，权重 {factor:0.##}）");
                foreach (var i in candidates)
                    if (model.Hypotheses[i].Options[q] == o) weights[i] *= factor;
            }
        var total = weights.Values.Sum();
        var credence = model.Options.Select((options, q) => options.Select((_, o) =>
            candidates.Where(i => model.Hypotheses[i].Options[q] == o).Sum(i => weights[i]) / total).ToImmutableArray()).ToImmutableArray();
        return new(model, candidates, credence, contradicted, [.. basis]);
    }

    private static bool Holds(ProjectionWorld mind, string name) => mind.Projections.Any(p => p.Name == name && p.Existence == Truth.True);

    /// <summary>How much a held claim weighs, by how it entered the mind.</summary>
    public static (double Factor, string Why) Weight(Projection claim)
    {
        var source = claim.LastObservation?.Source ?? ObservationSource.Introspection;
        switch (source)
        {
            case ObservationSource.Perception: return (8, "亲见");
            case ObservationSource.Inference: return (6, "自己推出");
            case ObservationSource.Recall: return (3, "回忆");
            case ObservationSource.Corruption: return (1, "铜口之言，不当证据");
            case ObservationSource.Testimony:
                var speaker = claim.Relations.FirstOrDefault(r => r.Relation == Relations.Subject).Target;
                var trust = speaker?.Tags.GetValueOrDefault(Tags.Trust) ?? 0;
                // Neutral testimony counts a little; distrust makes it worthless, never inverted.
                return (Math.Max(1, 2 + 0.5 * Math.Clamp(trust, -3, 3)), $"{speaker?.Name ?? "有人"}说的，信任 {trust}");
            default: return (2, "自知");
        }
    }
}
