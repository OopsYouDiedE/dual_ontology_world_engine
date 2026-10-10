using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;

var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
foreach (var nested in new[] { false, true })
{
    var runtime = new WorldRuntime(); var soul = new SoulEssence("observer");
    runtime.Project(soul, "observer");
    Projection Add(string name, ProjectionKind kind = ProjectionKind.Concept, Effect? procedure = null, Projection? parent = null)
        => Observation.Observe(soul, new(name, kind) { Source = ObservationSource.Introspection, Existence = Truth.True,
            Rules = procedure is null ? [] : [new(new Condition.Constant(Truth.True), RuleMode.Infer, procedure, 1, Truth.True)],
            Move = parent is not null, Parent = parent });
    var room = Add("space", ProjectionKind.Space);
    var actor = Add("actor", ProjectionKind.Body, parent: nested ? room : null);
    var p = new Condition.Tagged(actor, new("p"), 1); var q = new Condition.Tagged(actor, new("q"), 1); var r = new Condition.Tagged(actor, new("r"), 1);
    var first = Add("first", ProjectionKind.Object, new Effect.Think(p));
    var law1 = Add("law1", ProjectionKind.Procedure, new Effect.When(p, new Effect.Think(q)));
    var law2 = Add("law2", ProjectionKind.Procedure, new Effect.When(q, new Effect.Think(r)));
    var desire = Observation.Observe(soul, new("desire", ProjectionKind.Concept) { Source = ObservationSource.Introspection, Existence = Truth.True,
        Rules = [new(new Condition.Constant(Truth.True), RuleMode.Prefer, new Effect.Think(new Condition.Constant(Truth.False)), 99, Truth.True)] });
    var snapshot = ((ContractResult<CognitionSnapshotV1>.Success)soul.World.CaptureSnapshot()).Value;
    FrozenConditionV1 Freeze(Condition c) => ((ContractResult<FrozenConditionV1>.Success)CognitionAst.FreezeCondition(c, soul.World.Projections.ToImmutableDictionary(p => p.SubjectId!.Value))).Value;
    var fp = Freeze(p); var fq = Freeze(q); var fr = Freeze(r); var id = actor.SubjectId!.Value;
    var before = JsonSerializer.Serialize(snapshot);
    SubjectiveReasoner Reason(int depth = 8, int expansions = 2048, int hypotheses = 128) => new(snapshot, id, new(depth, expansions, hypotheses));
    var proof = Reason().Prove(fr);
    Check(proof.Value == Truth.True && proof.Complete && !proof.Hypothetical, nested + " multi_step_entailment");
    Check(proof.Basis.Contains(first.SubjectId!.Value) && proof.Basis.Contains(law1.SubjectId!.Value) && proof.Basis.Contains(law2.SubjectId!.Value), nested + " provenance_chain");
    Check(Reason(0).Prove(fr).Value == Truth.Unknown, nested + " shallow_depth_preserves_unknown");
    var budgeted = Reason(expansions: 1); var limited = budgeted.Prove(fr);
    Check(!limited.Complete && budgeted.Expansions == 1 && budgeted.StopReason == "expansion_budget", nested + " strict_expansion_budget");
    Check(Reason().Prove(fr, [first.SubjectId!.Value, law1.SubjectId!.Value, law2.SubjectId!.Value]).Value == Truth.True, nested + " selected_evidence_supports_argument");
    Check(Reason().Prove(fr, [law1.SubjectId!.Value, law2.SubjectId!.Value]).Value == Truth.Unknown, nested + " missing_evidence_not_invented");
    Check(Reason().Prove(new FrozenConditionV1.Constant(Truth.False)).Value == Truth.False, nested + " preference_is_not_fact");
    var executable = Observation.Observe(soul, new("executable", ProjectionKind.Procedure) { Source = ObservationSource.Introspection, Existence = Truth.True,
        Procedure = new Effect.When(new Condition.Constant(Truth.True), new Effect.Think(new Condition.Not(p))) });
    var unaccepted = Observation.Observe(soul, new("unaccepted", ProjectionKind.Concept) { Source = ObservationSource.Introspection, Existence = Truth.True,
        Rules = [new(new Condition.Constant(Truth.True), RuleMode.Infer, new Effect.Think(new Condition.Not(p)), 1, Truth.Unknown)] });
    CognitionSnapshotV1 Capture() => ((ContractResult<CognitionSnapshotV1>.Success)soul.World.CaptureSnapshot()).Value;
    Check(new SubjectiveReasoner(Capture(), id, new(8, 4096, 16)).Prove(fp).Value == Truth.True,
        nested + " executable_and_unaccepted_rule_are_not_facts");
    var invalidInfer = false;
    try { Observation.Observe(soul, new("invalid-infer", ProjectionKind.Concept) { Source = ObservationSource.Introspection, Existence = Truth.True,
        Rules = [new(new Condition.Constant(Truth.True), RuleMode.Infer, new Effect.Set(actor, new("p"), 1), 1, Truth.True)] }); }
    catch (CognitionContractException) { invalidInfer = true; }
    Check(invalidInfer, nested + " executable_infer_effect_rejected");
    var pureInfer = Capture() with { Projections = [..Capture().Projections.Select(n => n with { Rules = [..n.Rules.Where(r => r.Mode == RuleMode.Infer)] })] };
    Check(((ContractResult<ImmutableArray<SnapshotDecisionAssessmentV1>>.Success)DecisionSystem.Assess(pureInfer, id)).Value.Length == 0,
        nested + " inference_proposes_no_action");
    var preference = Observation.Observe(soul, new("preference", ProjectionKind.Concept) { Source = ObservationSource.Introspection, Existence = Truth.True,
        Rules = [new(new Condition.Constant(Truth.True), RuleMode.Prefer, new Effect.Think(p), 3, Truth.True)] });
    var scored = ((ContractResult<ImmutableArray<SnapshotDecisionAssessmentV1>>.Success)DecisionSystem.Assess(Capture(), id)).Value;
    Check(scored.Single(a => a.Effect == new FrozenEffectV1.Think(fp)).Score == 3, nested + " inference_does_not_add_decision_score");
    var negative = Add("negative-law", ProjectionKind.Procedure, new Effect.When(new Condition.Not(p), new Effect.Think(new Condition.Not(q))));
    // Use the newly captured history for new nodes; do not splice provenance from
    // a later revision into an earlier snapshot without its visible history.
    var current = Capture();
    var unknownSnapshot = current with { Projections = [..current.Projections.Select(n => n.Id == first.SubjectId ? n with { Rules = [] } : n)] };
    var planner = new SubjectiveReasoner(unknownSnapshot, id, new(8, 4096, 16));
    var explored = planner.Explore([fq], [new("a", [fp]), new("b", [new FrozenConditionV1.Not(fp)])]);
    Check(explored.Predictions.Length == 2 && explored.Predictions.All(h => h.Compatible), nested + " alternatives_remain_hypothetical");
    Check(explored.Predictions[0].Predictions[0].Value == Truth.True && explored.Predictions[1].Predictions[0].Value == Truth.False, nested + " predicted_traces");
    Check(explored.Inquiries.Single().DiscriminatingPairs == 1 && explored.Inquiries.Single().Targets.Contains(id), nested + " inquiry_discriminates_known_hypotheses");
    var capped = new SubjectiveReasoner(unknownSnapshot, id, new(8, 4096, 1)).Explore([fq], [new("a", [fp]), new("b", [new FrozenConditionV1.Not(fp)])]);
    Check(capped.Predictions.Length == 1 && capped.StopReason == "hypothesis_budget", nested + " hypothesis_limit_reported");
    var combined = Reason().Explore([new FrozenConditionV1.All([fq, fr])], [new("combination", [fp, fq])]);
    Check(combined.Predictions.Single().Predictions.Single().Value == Truth.True, nested + " variable_combination_simulation");
    // An acquired contrary assertion is kept as a conflict, not silently selected as truth.
    var opposing = Add("opposing", ProjectionKind.Object, new Effect.Think(new Condition.Not(p)));
    var conflict = new SubjectiveReasoner(Capture(), id, new(8, 4096, 16)).Prove(fp);
    Check(conflict.Contradicted && conflict.Value == Truth.Unknown, nested + " conflict_preserved");
    var noFacts = unknownSnapshot with { Projections = [..unknownSnapshot.Projections.Select(n => n.Id == law2.SubjectId
        ? n with { Rules = [new(fr, RuleMode.Infer, new FrozenEffectV1.Think(fp), 1, Truth.True)] } : n)] };
    Check(new SubjectiveReasoner(noFacts, id, new(8, 4096, 16)).Prove(fq).Value == Truth.Unknown, nested + " cycle_does_not_prove_itself");
    Check(before == JsonSerializer.Serialize(snapshot), nested + " proof_does_not_mutate_cognition");
    var hidden = new Essence("unseen"); hidden.Add(new StateProperty()).Values[new("secret")] = 0;
    runtime.Project(hidden, "unseen"); var a = Reason().Prove(fr);
    hidden.Get<StateProperty>()!.Values[new("secret")] = 91; var b = Reason().Prove(fr);
    Check(a.Value == b.Value && a.Basis.SequenceEqual(b.Basis) && a.Steps.SequenceEqual(b.Steps), nested + " hidden_truth_metamorphism");
    var invalid = false;
    try { Reason().Prove(new FrozenConditionV1.Exists(new FrozenNounV1.Reference(new(Guid.NewGuid(), Guid.NewGuid())))); }
    catch (CognitionContractException) { invalid = true; }
    Check(invalid, nested + " foreign_reference_rejected");
}
Console.WriteLine($"TOTAL {checks} checks");
