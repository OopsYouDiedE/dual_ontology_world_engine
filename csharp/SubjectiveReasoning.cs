using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Core;

public sealed record ReasoningBudgetV1(int Depth = 1, int Expansions = 256, int Hypotheses = 16);
public sealed record SubjectiveProofV1(FrozenConditionV1 Proposition, Truth Value, bool Contradicted,
    bool Hypothetical, ImmutableArray<SubjectProjectionId> Basis, ImmutableArray<string> Steps)
{ public bool Complete { get; init; } = true; }
public sealed record SubjectiveHypothesisV1(string Label, ImmutableArray<FrozenConditionV1> Assumptions);
public sealed record SubjectivePredictionV1(string Label, bool Compatible, ImmutableArray<SubjectiveProofV1> Predictions);
public sealed record SubjectiveInquiryV1(FrozenConditionV1 Proposition, int DiscriminatingPairs,
    ImmutableArray<SubjectProjectionId> Targets);
public sealed record SubjectiveReasoningResultV1(KnowledgeRevision Revision, ImmutableArray<SubjectiveProofV1> Proofs,
    ImmutableArray<SubjectivePredictionV1> Predictions, ImmutableArray<SubjectiveInquiryV1> Inquiries,
    int Expansions, string StopReason);

/// <summary>Bounded deliberation over one acquired subject graph. No runtime, shared
/// calculation sheet, objective lookup or mutation. Only accepted Infer/Think rules
/// are beliefs. Executable procedures and normative rules are NOT entailments.</summary>
public sealed class SubjectiveReasoner
{
    private sealed record Implication(ImmutableArray<FrozenConditionV1> Premises, FrozenConditionV1 Conclusion, SubjectProjectionId Basis);
    private readonly CognitionSnapshotV1 _snapshot;
    private readonly DetachedProjectionGraphV1 _graph;
    private readonly ActionContext _context;
    private readonly ReasoningBudgetV1 _budget;
    private readonly List<Implication> _beliefs = [];
    private int _expanded;
    private string _stop = "complete";
    public ImmutableArray<FrozenConditionV1> KnownQueries { get; }
    public ImmutableArray<SubjectProjectionId> StandingKnowledge => [.._beliefs.Where(b => b.Premises.Length > 0).Select(b => b.Basis).Distinct()];
    public int Expansions => _expanded;
    public string StopReason => _stop;

    public SubjectiveReasoner(CognitionSnapshotV1 snapshot, SubjectProjectionId actor, ReasoningBudgetV1 budget)
    {
        if (budget.Depth is < 0 or > 64 || budget.Expansions is < 1 or > 100000 || budget.Hypotheses is < 1 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(budget));
        _snapshot = snapshot; _budget = budget; _graph = SnapshotGraph.Build(snapshot);
        Contract.Id(actor, snapshot.SubjectWorld, "actor");
        Contract.Require(_graph.Projections.ContainsKey(actor), ContractErrorCode.InvalidReference, "actor", "推理主体不在认知图。");
        _context = new(_graph.Projections[actor], Scene: _graph.Projections[actor].Parent);
        foreach (var p in snapshot.Projections.Where(p => p.Existence == Truth.True))
            foreach (var rule in p.Rules.Where(r => r.Mode == RuleMode.Infer && r.Acceptance == Truth.True))
                Learn(rule.Effect, Premises(rule.Condition), p.Id);
        KnownQueries = [.._beliefs.SelectMany(b => b.Premises.Append(b.Conclusion)).Distinct()];
    }
    private static ImmutableArray<FrozenConditionV1> Premises(FrozenConditionV1 condition)
        => condition is FrozenConditionV1.Constant { Value: Truth.True } ? []
            : condition is FrozenConditionV1.All all ? all.Items : [condition];
    public ImmutableArray<FrozenConditionV1> Claims(SubjectProjectionId source)
        => [.._beliefs.Where(b => b.Basis == source && b.Premises.Length == 0).Select(b => b.Conclusion)];
    private void Learn(FrozenEffectV1? effect, ImmutableArray<FrozenConditionV1> premises, SubjectProjectionId basis)
    {
        if (effect is FrozenEffectV1.Think thought) _beliefs.Add(new(premises, thought.Fact, basis));
        else if (effect is FrozenEffectV1.When when)
            Learn(when.Body, premises.AddRange(Premises(when.Condition)), basis);
        // Other executable effects and preferences never become knowledge rules.
    }
    private bool Equal(FrozenConditionV1 a, FrozenConditionV1 b) => Contract.Value(Ast.Equivalent(a, b));
    public ImmutableArray<SubjectProjectionId> References(FrozenConditionV1 condition)
    {
        var ids = new HashSet<SubjectProjectionId>();
        Ast.Map(CognitionAst.Thaw(condition, _graph.Projections), noun =>
        {
            if (_context.Resolve(noun) is { } p) ids.Add(p.SubjectId!.Value);
            return noun;
        });
        return [..ids];
    }
    public Truth Observed(FrozenConditionV1 condition) => Ast.EvaluateLive(CognitionAst.Thaw(condition, _graph.Projections), _context);
    private bool Spend()
    {
        if (_expanded >= _budget.Expansions) { _stop = "expansion_budget"; return false; }
        _expanded++; return true;
    }
    public SubjectiveProofV1 Prove(FrozenConditionV1 proposition, ImmutableArray<SubjectProjectionId>? evidence = null)
        => Prove(proposition, [], evidence, 0, []);
    private SubjectiveProofV1 Prove(FrozenConditionV1 goal, ImmutableArray<FrozenConditionV1> assumptions,
        ImmutableArray<SubjectProjectionId>? evidence, int depth, List<FrozenConditionV1> visiting)
    {
        var refs = References(goal); // validates all references in this subject, even when a budget is exhausted
        if (evidence is { } restricted)
            foreach (var id in restricted) Contract.Require(_graph.Projections.ContainsKey(id), ContractErrorCode.InvalidReference, "evidence", "证据不在本人认知图。");
        SubjectiveProofV1 Unknown(string reason) => new(goal, Truth.Unknown, false, false, [], [reason]) { Complete = reason == "cycle" };
        if (!Spend()) return Unknown("expansion_budget");
        if (depth > _budget.Depth) { if (_stop == "complete") _stop = "depth_budget"; return Unknown("depth_budget"); }
        if (visiting.Any(p => Equal(p, goal))) return Unknown("cycle");
        var value = evidence is null || refs.All(evidence.Value.Contains) ? Observed(goal) : Truth.Unknown;
        var basis = value == Truth.Unknown ? new HashSet<SubjectProjectionId>() : refs.ToHashSet();
        var steps = new List<string>(); var supported = value == Truth.True; var denied = value == Truth.False; var hypothetical = false;
        if (value != Truth.Unknown) steps.Add("acquired_state");
        if (assumptions.Any(p => Equal(p, goal))) { supported = true; hypothetical = true; steps.Add("assumption"); }
        if (assumptions.Any(p => p is FrozenConditionV1.Not n && Equal(n.Operand, goal)))
        { denied = true; hypothetical = true; steps.Add("negated_assumption"); }
        if (goal is FrozenConditionV1.Not negation && assumptions.Any(p => Equal(p, negation.Operand)))
        { denied = true; hypothetical = true; steps.Add("negated_assumption"); }
        var next = new List<FrozenConditionV1>(visiting) { goal };
        if (goal is FrozenConditionV1.All conjunction)
        {
            var components = conjunction.Items.Select(p => Prove(p, assumptions, evidence, depth + 1, next)).ToArray();
            if (components.All(p => p.Value == Truth.True && !p.Contradicted)) supported = true;
            if (components.Any(p => p.Value == Truth.False)) denied = true;
            foreach (var proof in components) { basis.UnionWith(proof.Basis); hypothetical |= proof.Hypothetical; }
            steps.Add("conjunction");
        }
        if (goal is FrozenConditionV1.Any disjunction)
        {
            var components = disjunction.Items.Select(p => Prove(p, assumptions, evidence, depth + 1, next)).ToArray();
            if (components.Any(p => p.Value == Truth.True && !p.Contradicted)) supported = true;
            if (components.All(p => p.Value == Truth.False)) denied = true;
            foreach (var proof in components) { basis.UnionWith(proof.Basis); hypothetical |= proof.Hypothetical; }
            steps.Add("disjunction");
        }
        if (goal is FrozenConditionV1.Not not)
        {
            var operand = Prove(not.Operand, assumptions, evidence, depth + 1, next);
            if (operand.Value == Truth.False || operand.Contradicted) supported = true;
            if (operand.Value == Truth.True || operand.Contradicted) denied = true;
            basis.UnionWith(operand.Basis); hypothetical |= operand.Hypothetical; steps.Add("negation");
        }
        foreach (var belief in _beliefs)
        {
            if (!Spend()) break;
            if (evidence is { } allowed && !allowed.Contains(belief.Basis)) continue;
            var positive = Equal(belief.Conclusion, goal);
            var negative = belief.Conclusion is FrozenConditionV1.Not n && Equal(n.Operand, goal)
                || goal is FrozenConditionV1.Not g && Equal(belief.Conclusion, g.Operand);
            if (!positive && !negative) continue;
            var proofs = belief.Premises.Select(p => Prove(p, assumptions, evidence, depth + 1, next)).ToArray();
            if (!proofs.All(p => p.Value == Truth.True && !p.Contradicted)) continue;
            if (positive) supported = true; if (negative) denied = true;
            basis.Add(belief.Basis);
            foreach (var proof in proofs) { basis.UnionWith(proof.Basis); steps.AddRange(proof.Steps); hypothetical |= proof.Hypothetical; }
            steps.Add(belief.Premises.Length == 0 ? "acquired_assertion" : "conditional_entailment");
        }
        if (supported && denied) steps.Add("conflicting_acquired_material");
        return new(goal, supported == denied ? Truth.Unknown : supported ? Truth.True : Truth.False,
            supported && denied, hypothetical, [..basis.OrderBy(id => id.Local)], [..steps.Distinct()])
            { Complete = _stop == "complete" };
    }
    public SubjectiveReasoningResultV1 Explore(ImmutableArray<FrozenConditionV1> traces,
        ImmutableArray<SubjectiveHypothesisV1> hypotheses)
    {
        var proofs = traces.Select(q => Prove(q)).ToImmutableArray();
        var predictions = ImmutableArray.CreateBuilder<SubjectivePredictionV1>();
        foreach (var hypothesis in hypotheses.Take(_budget.Hypotheses))
        {
            if (_stop == "expansion_budget") break;
            foreach (var assumption in hypothesis.Assumptions) References(assumption);
            var predicted = traces.Select(q => Prove(q, hypothesis.Assumptions, null, 0, [])).ToImmutableArray();
            var compatible = hypothesis.Assumptions.All(p => Observed(p) != Truth.False)
                && predicted.All(p => !p.Contradicted)
                && !proofs.Zip(predicted).Any(pair => pair.First.Value != Truth.Unknown && pair.Second.Value != Truth.Unknown && pair.First.Value != pair.Second.Value);
            predictions.Add(new(hypothesis.Label, compatible, predicted));
        }
        if (hypotheses.Length > _budget.Hypotheses && _stop == "complete") _stop = "hypothesis_budget";
        var inquiries = ImmutableArray.CreateBuilder<SubjectiveInquiryV1>();
        for (var i = 0; i < traces.Length; i++)
        {
            if (proofs[i].Value != Truth.Unknown || proofs[i].Contradicted) continue;
            var values = predictions.Where(p => p.Compatible).Select(p => p.Predictions[i].Value).ToArray();
            // Unknown is ignorance, never counted as an alternative factual prediction.
            var pairs = values.Count(v => v == Truth.True) * values.Count(v => v == Truth.False);
            inquiries.Add(new(traces[i], pairs, References(traces[i])));
        }
        return new(_snapshot.Revision, proofs, predictions.ToImmutable(), inquiries.ToImmutable(), _expanded, _stop);
    }
}
