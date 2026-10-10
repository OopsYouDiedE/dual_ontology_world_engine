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
    ImmutableArray<ImmutableArray<double>> Credence, bool Contradicted, ImmutableArray<ReasonBasis> Basis)
{
    public KnowledgeRevision? Revision { get; init; }
    public double Of(int question, int option) => Credence[question][option];
}

public enum ReasonCode { Observed, DefaultAssumed, Compatible, LeastContradictions, Accepted, Discounted, SelfKnown, Prior }
public sealed record EvidenceWeight(double Factor, ObservationSource Source, ReasonCode Code,
    Projection? Speaker = null, int Trust = 0, int Strength = 1);
public sealed record ReasonBasis(ReasonCode Code, string? Subject = null, string? Field = null,
    int Value = 0, string? Option = null, int Total = 0, EvidenceWeight? Weight = null);

/// <summary>
/// The shared reasoning faculty. It reads only the given mind: traces it has perceived narrow the
/// hypotheses; claims it holds weigh them by source (own inference, recall, testimony scaled by trust
/// in the speaker). Words of corruption are not evidence. Nothing here touches reality.
/// </summary>
public static partial class Reasoner
{
    public static CaseBelief Assess(ProjectionWorld mind, CaseModel model)
    {
        if (!mind.Detached)
        {
            var view = SubjectRead.Capture(mind);
            return Assess(view.Graph.World, model) with { Revision = view.Revision };
        }
        var basis = new List<ReasonBasis>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var field in model.Fields)
        {
            var projection = mind.Projections.FirstOrDefault(p => p.Name == field.Projection && p.Existence != Truth.False);
            if (projection is not null && projection.Tags.TryGetValue(field.Tag, out var value))
            {
                seen[field.Key] = value;
                basis.Add(new(ReasonCode.Observed, field.Projection, field.Tag.Name, value));
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
            basis.Add(new(ReasonCode.DefaultAssumed, model.Questions[q], Option: model.Options[q][fallback]));
        }
        var conceivable = Enumerable.Range(0, model.Hypotheses.Length)
            .Where(i => assumed.All(a => model.Hypotheses[i].Options[a.Key] == a.Value)).ToArray();
        int Contradictions(CaseHypothesis h) => seen.Count(f => h.Facts[f.Key] != f.Value);
        var minimum = conceivable.Min(i => Contradictions(model.Hypotheses[i]));
        var contradicted = minimum > 0;
        var candidates = conceivable.Where(i => Contradictions(model.Hypotheses[i]) == minimum).ToImmutableArray();
        basis.Add(new(contradicted ? ReasonCode.LeastContradictions : ReasonCode.Compatible,
            Value: candidates.Length, Total: model.Hypotheses.Length));

        var weights = candidates.ToDictionary(i => i, _ => 1.0);
        for (var q = 0; q < model.Questions.Length; q++)
            for (var o = 0; o < model.Options[q].Length; o++)
            {
                var claim = mind.Projections.FirstOrDefault(p => p.Name == model.ClaimName(q, o) && p.Existence == Truth.True);
                if (claim is null) continue;
                var weight = Weight(claim);
                var factor = weight.Factor;
                if (factor <= 1) { basis.Add(new(ReasonCode.Discounted, claim.Name, Weight: weight)); continue; }
                basis.Add(new(ReasonCode.Accepted, claim.Name, Weight: weight));
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
    public static EvidenceWeight Weight(Projection claim)
    {
        if (!claim.World.Detached)
        {
            var view = SubjectRead.Capture(claim.World);
            var result = Weight(view.Get(claim));
            return result with { Speaker = result.Speaker is { } speaker ? view.Original(speaker) : null };
        }
        var source = claim.LastObservation?.Source ?? ObservationSource.Introspection;
        switch (source)
        {
            case ObservationSource.Perception: return new(8, source, ReasonCode.Accepted);
            case ObservationSource.Inference: return new(6, source, ReasonCode.Accepted);
            case ObservationSource.Recall: return new(3, source, ReasonCode.Accepted);
            case ObservationSource.Corruption: return new(1, source, ReasonCode.Discounted);
            case ObservationSource.Testimony:
                var speaker = claim.Relations.FirstOrDefault(r => r.Relation == Relations.Subject).Target;
                var trust = speaker?.Tags.GetValueOrDefault(Tags.Trust) ?? 0;
                // Neutral testimony counts a little; distrust makes it worthless, never inverted.
                return new(Math.Max(1, 2 + 0.5 * Math.Clamp(trust, -3, 3)), source, ReasonCode.Accepted, speaker, trust);
            // 先入之见、自知：强度记在 Confidence 上（缺省 1）。
            default:
                var strength = claim.Tags.GetValueOrDefault(Tags.Confidence, 1);
                return new(1 + Math.Max(0, strength), source, strength == 1 ? ReasonCode.SelfKnown : ReasonCode.Prior, Strength: strength);
        }
    }
}
