using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
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
// Merge contract: inference is persisted as subject-owned material, alongside
// speech and events, without promoting testimony or authoritative causes to facts.
T Ok<T>(ContractResult<T> result) => result is ContractResult<T>.Success success ? success.Value
    : throw new Exception("Merge contract failed: " + ((ContractResult<T>.Failure)result).Error);
foreach (var nested in new[] { false, true })
{
    var runtime = new WorldRuntime();
    var soul = new SoulEssence(new("observer"), "observer");
    var other = new SoulEssence(new("other"), "other");
    var root = new Essence(new("root"), "root", ProjectionKind.Space);
    var room = new Essence(new("room"), "room", ProjectionKind.Space);
    var body = new Essence(new("body"), "body", ProjectionKind.Body);
    if (nested) room.Add(new SpatialProperty { Space = root });
    body.Add(new SpatialProperty { Space = room });
    runtime.Project(soul, "observer"); runtime.Project(other, "other");
    var rr = runtime.Project(root, "root"); var rm = runtime.Project(room, "room", nested ? rr : null);
    var rb = runtime.Project(body, "body");
    var known = runtime.Observe(soul, [rr, rm, rb]); runtime.Observe(other, [rb]);
    var actor = known[2]; var id = actor.SubjectId!.Value;
    Condition P(string tag) => new Condition.Tagged(actor, new(tag), 1);
    FrozenConditionV1 F(string tag) => new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(id), new(tag), 1);
    Projection Belief(string name, Effect effect, LifetimeScope lifetime, string? owner)
        => Observation.Observe(soul, new(name, ProjectionKind.Concept) { Source = ObservationSource.Introspection,
            Existence = Truth.True, Rules = [new(new Condition.Constant(Truth.True), RuleMode.Infer, effect, 1, Truth.True, lifetime, owner)] });
    var first = Belief("first", new Effect.Think(P("p")), LifetimeScope.World, null);
    var law = Belief("law", new Effect.When(P("p"), new Effect.Think(P("q"))), LifetimeScope.Chapter, "neutral.chapter");
    var last = Belief("last", new Effect.When(P("q"), new Effect.Think(P("r"))), LifetimeScope.Interaction, "neutral.interaction");
    var speech = Observation.Observe(soul, new("speech", ProjectionKind.Procedure) { Source = ObservationSource.Introspection,
        Existence = Truth.True, Procedure = new Effect.Say(actor, "neutral", Claim: P("p"), Topic: new("neutral.topic")) });
    // Also exercise the engine-only reality AST path used by RuntimeSave.
    body.Add(new RuleProperty { Rules = [new(new Condition.Constant(Truth.True), RuleMode.Infer,
        new Effect.When(new Condition.Exists(rb), new Effect.Think(new Condition.Exists(rb))),
        1, Truth.True, LifetimeScope.Interaction, "neutral.runtime")] });
    runtime.Sync();
    var cause = new ObservationRecordV1<Essence?>(new() { Id = new("visible.cause"), Subject = body,
        KnowledgeSource = ObservationSource.Perception, Phase = EventPhaseV1.Delivered,
        Outcome = EventOutcomeV1.Succeeded, Perceivers = [soul] });
    runtime.RecordEvent(cause); runtime.PublishEvent(soul, cause);
    var secret = new ObservationRecordV1<Essence?>(cause.Fields with { Id = new("hidden.cause"), Perceivers = [other] });
    runtime.RecordEvent(secret); runtime.PublishEvent(other, secret);
    var testimony = new TestimonyEventV1<Essence?>(cause.Fields with { Id = new("testimony"),
        KnowledgeSource = ObservationSource.Testimony, Causes = [cause.Fields.Id, secret.Fields.Id],
        Claims = [new(new("neutral.state"), body, null, room, new("unaccepted"), 1, Truth.True)] });
    runtime.RecordEvent(testimony); runtime.PublishEvent(soul, testimony);
    var archive = Ok(runtime.CaptureCognitionGraph(soul));
    var proof = new SubjectiveReasoner(archive.SubjectSnapshot, id, new(8, 4096, 128)).Prove(F("r"));
    var bytes = Ok(CognitionCodec.Encode(archive)); var decoded = Ok(CognitionCodec.Decode(bytes));
    var rebound = Ok(CognitionGraph.Rebind(decoded));
    var decodedProof = new SubjectiveReasoner(decoded.SubjectSnapshot, id, new(8, 4096, 128)).Prove(F("r"));
    var loadedSay = rebound.Projections[speech.SubjectId!.Value].Procedure as Effect.Say;
    Check(Ok(CognitionCodec.Encode(decoded)).SequenceEqual(bytes) && proof.Value == Truth.True && decodedProof.Complete
        && decodedProof.Value == proof.Value && decodedProof.Basis.SequenceEqual(proof.Basis)
        && rebound.Projections[first.SubjectId!.Value].Rules.Single().Lifetime == LifetimeScope.World
        && rebound.Projections[law.SubjectId!.Value].Rules.Single().LifetimeOwner == "neutral.chapter"
        && rebound.Projections[law.SubjectId!.Value].Rules.Single().Lifetime == LifetimeScope.Chapter
        && rebound.Projections[last.SubjectId!.Value].Rules.Single().Lifetime == LifetimeScope.Interaction
        && rebound.Projections[last.SubjectId!.Value].Rules.Single().LifetimeOwner == "neutral.interaction"
        && loadedSay?.Topic == new AuthorTermId("neutral.topic")
        && loadedSay.Claim is Condition.Tagged tagged && ((Noun.Reference)tagged.Target).Projection == rebound.Projections[id],
        nested + " infer_graph_wire_rebind_preserves_lifetime_speech_and_proof");
    var audit = Ok(CognitionCodec.DecodeAudit(Ok(CognitionCodec.EncodeAudit(Ok(runtime.CaptureCognitionAudit(soul))))));
    var rebuilt = Ok(runtime.ReconstructCognitionAudit(audit, archive.SubjectSnapshot.Revision));
    Check(Ok(CognitionCodec.Encode(archive with { SubjectSnapshot = rebuilt })).SequenceEqual(bytes),
        nested + " infer_audit_wire_reconstructs_subject_material");
    var restored = Ok(WorldRuntime.Restore(Ok(runtime.Save())));
    var restoredSoul = (SoulEssence)restored.Entities[soul.Id];
    var restoredArchive = Ok(restored.Runtime.CaptureCognitionGraph(restoredSoul));
    var restoredProof = new SubjectiveReasoner(restoredArchive.SubjectSnapshot, id, new(8, 4096, 128)).Prove(F("r"));
    var runtimeRule = restored.Runtime.Representations(restored.Entities[body.Id]).Single().Rules.Single();
    Check(Ok(CognitionCodec.Encode(restoredArchive)).SequenceEqual(bytes) && restoredProof.Value == proof.Value
        && restoredProof.Basis.SequenceEqual(proof.Basis) && runtimeRule.Mode == RuleMode.Infer
        && runtimeRule.Lifetime == LifetimeScope.Interaction && runtimeRule.LifetimeOwner == "neutral.runtime"
        && Ast.References(runtimeRule).All(p => p == restored.Runtime.Representations(restored.Entities[body.Id]).Single())
        && restoredSoul.World.Revision == soul.World.Revision && restored.Runtime.EventAudit.Count == runtime.EventAudit.Count,
        nested + " infer_runtime_save_restores_new_references_without_replaying");
    var visible = TypedEvents.Of(restoredArchive.SubjectSnapshot);
    var shown = visible.Single(e => e.Fields.Id == testimony.Fields.Id);
    var unknown = new SubjectiveReasoner(restoredArchive.SubjectSnapshot, id, new(8, 4096, 128)).Prove(F("unaccepted"));
    Check(shown.Fields.Causes.SequenceEqual([cause.Fields.Id]) && !visible.Any(e => e.Fields.Id == secret.Fields.Id)
        && unknown.Value == Truth.Unknown && restoredProof.Basis.All(b => restoredArchive.SubjectSnapshot.Projections.Any(p => p.Id == b)),
        nested + " infer_uses_own_material_with_visible_events_not_hidden_causes_or_unaccepted_claims");
    JsonObject EditedRule() => JsonNode.Parse(bytes)!.AsObject();
    JsonObject RuleIn(JsonObject document) => document["subject_snapshot"]!["projections"]!.AsArray()
        .Single(p => p!["id"]!["local"]!.GetValue<string>() == first.SubjectId!.Value.Local.ToString("D"))!["rules"]![0]!.AsObject();
    var unknownMode = EditedRule(); RuleIn(unknownMode)["mode"] = "unlisted";
    Check(CognitionCodec.Decode(JsonSerializer.SerializeToUtf8Bytes(unknownMode)) is ContractResult<CognitionGraphArchiveV1>.Failure
        { Error.Code: ContractErrorCode.Unsupported }, nested + " infer_codec_keeps_unknown_modes_closed");
    var invalidEffect = EditedRule(); RuleIn(invalidEffect)["effect"] = new JsonObject { ["kind"] = "set",
        ["target"] = new JsonObject { ["kind"] = "reference", ["target"] = JsonNode.Parse(JsonSerializer.Serialize(new { subject_world = id.SubjectWorld, local = id.Local })) },
        ["tag"] = "p", ["value"] = 1 };
    var invalidOwner = EditedRule(); RuleIn(invalidOwner)["lifetime"] = "chapter";
    Check(CognitionCodec.Decode(JsonSerializer.SerializeToUtf8Bytes(invalidEffect)) is ContractResult<CognitionGraphArchiveV1>.Failure
        { Error.Code: ContractErrorCode.MalformedData }
        && CognitionCodec.Decode(JsonSerializer.SerializeToUtf8Bytes(invalidOwner)) is ContractResult<CognitionGraphArchiveV1>.Failure
        { Error.Code: ContractErrorCode.MalformedData }, nested + " infer_decode_rejects_executable_effect_and_ownerless_lifetime");
}
Console.WriteLine($"TOTAL {checks} checks");
