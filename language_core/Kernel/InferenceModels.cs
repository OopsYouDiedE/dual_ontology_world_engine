using System.Collections.Immutable;

namespace DualOntology.Language;

public enum EvidenceSource { Perception, Testimony, Inference, Introspection, Corruption, Recall }
public sealed record EvidenceClaim(string Model, string Handle, int Question, int Option, EvidenceSource Source, string? Speaker = null, int Strength = 1);
public sealed record CaseField(string Handle, string Tag, string Key);
public sealed record CaseHypothesis(string Id, ImmutableArray<int> Options, ImmutableDictionary<string, int> Facts);
public sealed record CaseModel(string Id, ImmutableArray<int> OptionCounts, ImmutableArray<CaseField> Fields,
    ImmutableArray<CaseHypothesis> Hypotheses, ImmutableDictionary<int, int> Defaults)
{
    internal Value EncodeHypotheses()
    {
        TermReader.Name(Id);
        if (OptionCounts.IsEmpty || OptionCounts.Any(c => c < 1) || Hypotheses.IsEmpty) throw new LanguageException("Empty case model.");
        if (Hypotheses.Select(h => h.Id).Distinct(StringComparer.Ordinal).Count() != Hypotheses.Length) throw new LanguageException("Duplicate hypothesis.");
        if (Fields.Select(f => f.Key).Distinct(StringComparer.Ordinal).Count() != Fields.Length) throw new LanguageException("Duplicate trace key.");
        foreach (var field in Fields) { TermReader.Name(field.Tag); TermReader.Name(field.Key); }
        foreach (var h in Hypotheses)
        {
            TermReader.Name(h.Id);
            if (h.Options.Length != OptionCounts.Length || h.Options.Where((o, q) => o < 0 || o >= OptionCounts[q]).Any()) throw new LanguageException("Invalid hypothesis options.");
            if (Fields.Any(f => !h.Facts.ContainsKey(f.Key))) throw new LanguageException("Incomplete predicted traces.");
        }
        foreach (var (q, o) in Defaults) if (q < 0 || q >= OptionCounts.Length || o < 0 || o >= OptionCounts[q]) throw new LanguageException("Invalid default option.");
        return new Value.List([.. Hypotheses.Select(h => Value.Seq(TermReader.Name(h.Id),
            new Value.List([.. h.Options.Select(o => new Value.Number(o))]),
            new Value.List([.. h.Facts.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => Value.Seq(TermReader.Name(p.Key), new Value.Number(p.Value)))])))]);
    }
}
public sealed record CaseBelief(Guid Subject, long Revision, ImmutableDictionary<string, double> Distribution,
    ImmutableArray<ImmutableArray<double>> Credence, ImmutableArray<string> Candidates, bool Contradicted, Value Basis);
public sealed record SubjectiveProof(Guid Subject, long Revision, Truth Truth, bool Contradicted, bool Hypothetical,
    ImmutableArray<string> RuleBasis, string Stop, int Expansions);
public sealed record PlannedAction(Guid Subject, long Revision, Effect? First, Value Edges, double Score,
    int Depth, int Expansions, string Stop, ImmutableArray<string> PredictionFailures, int ComparedDepth);
