using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DualOntologyWorldEngine.Core;

// Independent neutral mechanism regression. No authored story or shared answer data.
if (args.Length == 1 && args[0] == "--api-probe")
{
    var unknownProof = new SubjectiveProofV1(new FrozenConditionV1.Constant(Truth.Unknown), Truth.Unknown, false, false, [], []);
    bool Rejected(Action action)
    {
        try { action(); return false; }
        catch (ArgumentOutOfRangeException) { return true; }
    }
    Console.WriteLine(JsonSerializer.Serialize(new {
        Probe = "numeric_api_boundaries",
        LargeDirectionWeight = SubjectiveDecision.DirectionValue(0, 0, 10, int.MaxValue),
        ExtremeFiniteDirection = SubjectiveDecision.DirectionValue(-double.MaxValue, -double.MaxValue, double.MaxValue, 4),
        NegativeInformationWeightRejected = Rejected(() => SubjectiveDecision.InformationValue(unknownProof, false, -1)),
        ExcessiveInformationWeightRejected = Rejected(() => SubjectiveDecision.InformationValue(unknownProof, false, int.MaxValue))
    }));
    foreach (var length in new[] { 32, 64, 128 })
    {
        var f = new NeutralFixture(false);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < length; i++)
            ActionLearning.Observe(f.Soul, f.Actor, new("neutral.performance.method", "neutral.context." + i), DecisionOutcomeV1.Attempted);
        watch.Stop();
        var rows = f.Soul.World.History.Where(h => h.Target.Tags.GetValueOrDefault(new Tag("core.decision-experience.v1")) == 1).ToArray();
        var allocationForObserve = GC.GetAllocatedBytesForCurrentThread() - allocated;
        ActionLearning.Assess(f.Soul.World, f.Actor);
        allocated = GC.GetAllocatedBytesForCurrentThread();
        var assess = Stopwatch.StartNew();
        for (var i = 0; i < 5; i++) ActionLearning.Assess(f.Soul.World, f.Actor);
        assess.Stop();
        Console.WriteLine(JsonSerializer.Serialize(new {
            Probe = "experience_history_growth", ExperienceEntries = length,
            LatestDetailsBytes = Encoding.UTF8.GetByteCount(rows.Last().Details),
            RetainedDetailsBytes = rows.Sum(h => Encoding.UTF8.GetByteCount(h.Details)),
            ObserveElapsedMilliseconds = watch.Elapsed.TotalMilliseconds,
            ObserveAllocatedBytes = allocationForObserve,
            AssessmentAverageMilliseconds = assess.Elapsed.TotalMilliseconds / 5,
            AssessmentAverageAllocatedBytes = (GC.GetAllocatedBytesForCurrentThread() - allocated) / 5
        }));
    }
    Console.WriteLine("API_PROBE: COMPLETE");
    return;
}
var evidence = args.Length == 2 && args[0] == "--evidence" ? Path.GetFullPath(args[1]) : null;
if (evidence is not null) Directory.CreateDirectory(evidence);
var failures = new List<string>();
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
}
void Reject(Action action, string description)
{
    var rejected = false;
    try { action(); }
    catch (Exception e) when (e is InvalidDataException or ArgumentException or InvalidOperationException or JsonException or CognitionContractException)
    { rejected = true; }
    Check(rejected, description);
}
void Run(string name, Action action)
{
    try { action(); Console.WriteLine("PASS " + name); }
    catch (Exception e) { failures.Add(name); Console.WriteLine("FAIL " + name + "\n" + e); }
}
void Preserve(string filename, byte[] bytes)
{
    if (evidence is not null) File.WriteAllBytes(Path.Combine(evidence, filename), bytes);
}
T Ok<T>(ContractResult<T> result) => result is ContractResult<T>.Success success ? success.Value
    : throw new InvalidOperationException("Contract rejected: " + ((ContractResult<T>.Failure)result).Error);
DecisionMemoryV1 Stored(ProjectionWorld world, Projection actor)
    => ActionLearning.Capture(world, actor);
string Scores(ProjectionWorld world, Projection actor, ReasoningBudgetV1? budget = null)
    => string.Join(";", ActionLearning.Assess(world, actor, budget).Select(a =>
        ActionLearning.Key(world, actor, a.Decision.Effect).Method + ":" + a.Decision.Score + ":" + a.Forbidden));

Run("memory_subject_isolation_and_distinct_outcomes", () =>
{
    var first = new SubjectiveDecisionMemory(); var other = new SubjectiveDecisionMemory();
    var key = new DecisionExperienceKeyV1("neutral.method", "neutral.context");
    foreach (var outcome in Enum.GetValues<DecisionOutcomeV1>()) first.Record(key, outcome);
    var row = first.Get(key);
    Check(row.Attempts == 1 && row.Successes == 1 && row.Failures == 1 && row.NoProgress == 1 && row.Interruptions == 1,
        "Each outcome must use its own counter.");
    Check(other.Get(key) == new DecisionExperienceV1(key), "A second memory must not inherit the first actor's experience.");
    Check(SubjectiveDecisionMemory.Restore(first.Capture()).Get(key) == row, "DTO restore must preserve all outcomes.");
    first.ReconsiderAttempts([key]);
    var reconsidered = first.Get(key);
    Check(reconsidered == row with { Attempts = 0 }, "Reconsideration must preserve execution outcomes.");
});

Run("score_composition_and_outcome_semantics", () =>
{
    var key = new DecisionExperienceKeyV1("neutral.method");
    var mixed = new DecisionExperienceV1(key, Attempts: 2, Successes: 3, Failures: 1, NoProgress: 1, Interruptions: 4);
    var result = SubjectiveDecision.Evaluate(new(100, mixed, RevisitWeight: 5, InformationValue: 3, Cost: 2));
    Check(result.Score == 51 && !result.Forbidden, "Rule, information, cost, retry and failure terms must compose.");
    Check(result.Basis.Any(b => b.StartsWith("observed_action_failed:", StringComparison.Ordinal))
        && result.Basis.Any(b => b.StartsWith("observed_movement_stalled:", StringComparison.Ordinal)), "Failure and stalled movement must remain separate reasons.");
    Check(SubjectiveDecision.Evaluate(new(100, new(key, Successes: 9, Interruptions: 9))).Score == 100,
        "Success and interruption must not count as failure or stalled movement.");
    Check(SubjectiveDecision.Evaluate(new(int.MaxValue, new(key), InformationValue: int.MaxValue)).Score == 100000000,
        "Combined scores must saturate safely.");
    Check(SubjectiveDecision.Evaluate(new(1000000, new(key), Forbidden: true)).Forbidden,
        "Scores must not remove an existing ban.");
    Reject(() => SubjectiveDecision.Evaluate(new(0, new(key), RevisitWeight: -1)), "Negative revisit weights must be rejected.");
    Reject(() => SubjectiveDecision.Evaluate(new(0, new(key), Cost: -1)), "Negative cost must be rejected.");
    Reject(() => SubjectiveDecision.Evaluate(new(0, new(key, Failures: -1))), "Scoring must reject malformed experience counters.");
    Reject(() => SubjectiveDecision.Evaluate(new(0, new(key), FailureWeight: -1)), "Negative failure weights must be rejected.");
});

Run("direction_and_interruptible_reconsideration", () =>
{
    Check(SubjectiveDecision.DirectionValue(0, 0, 10, 4) == 8 && SubjectiveDecision.DirectionValue(5, 0, 10, 4) == 4
        && SubjectiveDecision.DirectionValue(10, 0, 10, 4) == 0, "Direction scoring must preserve relative proximity.");
    Check(SubjectiveDecision.DirectionValue(-1, 0, 10, 4) == 8 && SubjectiveDecision.DirectionValue(20, 0, 10, 4) == 0,
        "Direction scoring must clamp the normal input range.");
    Reject(() => SubjectiveDecision.DirectionValue(double.NaN, 0, 10, 4), "Non-finite distance must be rejected.");
    Reject(() => SubjectiveDecision.DirectionValue(1, 0, 10, -1), "Negative direction weight must be rejected.");
    Check(SubjectiveDecision.DirectionValue(0, 0, 10, int.MaxValue) == int.MaxValue,
        "Large direction weights must saturate the public int result without becoming negative.");
    Check(SubjectiveDecision.DirectionValue(-double.MaxValue, -double.MaxValue, double.MaxValue, 4) == 8,
        "Finite extreme distances must preserve relative proximity without overflow.");
    var unknown = new SubjectiveProofV1(new FrozenConditionV1.Constant(Truth.Unknown), Truth.Unknown, false, false, [], []);
    Check(SubjectiveDecision.InformationValue(unknown, true, 1000000) == 3000000,
        "The permitted information-weight boundary must preserve goal weighting.");
    Reject(() => SubjectiveDecision.InformationValue(unknown, false, -1), "Negative information weights must be rejected.");
    Reject(() => SubjectiveDecision.InformationValue(unknown, false, 1000001), "Excessive information weights must be rejected.");
    Check(SubjectiveDecision.ShouldReconsider(5, 6, true) && !SubjectiveDecision.ShouldReconsider(5, 5, true)
        && !SubjectiveDecision.ShouldReconsider(5, 6, false), "Only a better candidate may interrupt an interruptible action.");
    Check(SubjectiveDecision.ShouldReconsider(5.1, 5.2, true) && !SubjectiveDecision.ShouldReconsider(5.2, 5.1, true),
        "Fractional host scores must not be truncated before reconsideration.");
});

Run("per_context_storage_preserves_outcomes_without_quadratic_details", () =>
{
    long Retained(int entries)
    {
        var f = new NeutralFixture(false);
        for (var i = 0; i < entries; i++) ActionLearning.Observe(f.Soul, f.Actor, new("neutral.storage", "neutral.context." + i), DecisionOutcomeV1.Attempted);
        var key = new DecisionExperienceKeyV1("neutral.storage", "neutral.context.0");
        ActionLearning.Observe(f.Soul, f.Actor, key, DecisionOutcomeV1.Failed);
        ActionLearning.Observe(f.Soul, f.Actor, key, DecisionOutcomeV1.Interrupted);
        var nodes = f.Soul.World.Projections.Where(p => p.Tags.GetValueOrDefault(new Tag("core.decision-experience.v1")) == 1).ToArray();
        Check(nodes.Length == entries && nodes.All(p => SubjectiveDecisionMemory.Decode(Encoding.UTF8.GetBytes(p.LastObservation!.Details)).Capture().Experiences.Length == 1),
            "New contexts must store one row each, while repeated outcomes update the existing row.");
        Check(Stored(f.Soul.World, f.Actor).Experiences.Single(e => e.Key == key) is { Attempts: 1, Failures: 1, Interruptions: 1 },
            "Public export must merge independently stored outcomes without losing previous counters.");
        return f.Soul.World.History.Where(h => h.Target.Tags.GetValueOrDefault(new Tag("core.decision-experience.v1")) == 1)
            .Sum(h => Encoding.UTF8.GetByteCount(h.Details));
    }
    var smaller = Retained(32); var larger = Retained(64);
    Check(larger < smaller * 2.2, "Doubling new contexts must not retain four times the experience Details.");
});

Run("decision_dto_closed_validation", () =>
{
    var key = new DecisionExperienceKeyV1("neutral.method", "neutral.context");
    var row = new DecisionExperienceV1(key);
    Reject(() => SubjectiveDecisionMemory.Restore(new(2, [row])), "Unknown DTO version must be rejected.");
    Reject(() => SubjectiveDecisionMemory.Restore(new(1, default)), "Uninitialized experience collection must be rejected.");
    Reject(() => SubjectiveDecisionMemory.Restore(new(1, [row, row])), "Duplicate experience keys must be rejected.");
    foreach (var invalid in new[] { row with { Attempts = -1 }, row with { Successes = -1 }, row with { Failures = -1 },
        row with { NoProgress = -1 }, row with { Interruptions = -1 }, row with { Attempts = 1000001 } })
        Reject(() => SubjectiveDecisionMemory.Restore(new(1, [invalid])), "Negative or excessive outcome counters must be rejected.");
    Reject(() => SubjectiveDecisionMemory.Restore(new(1, [row with { Key = new(" ") }])), "Blank method must be rejected.");
    Reject(() => new SubjectiveDecisionMemory().Record(key, (DecisionOutcomeV1)99), "Unknown outcome must be rejected.");
    var memory = SubjectiveDecisionMemory.Restore(new(1, [row with { Attempts = 1000000 }]));
    Check(memory.Record(key, DecisionOutcomeV1.Attempted).Attempts == 1000000, "Counter saturation must not overflow.");
    Check(SubjectiveDecisionMemory.Decode(memory.Encode()).Get(key) == memory.Get(key), "The public memory wire must round-trip.");
    Reject(() => SubjectiveDecisionMemory.Decode(Encoding.UTF8.GetBytes("{\"Version\":1}")), "Missing required collection must be rejected.");
    Reject(() => SubjectiveDecisionMemory.Decode([0xEF, 0xBB, 0xBF, ..memory.Encode()]), "BOM must be rejected.");
    Reject(() => SubjectiveDecisionMemory.Decode([0xFF]), "Invalid UTF8 must be rejected.");
});

foreach (var nested in new[] { false, true })
{
    var prefix = nested ? "nested_" : "flat_";
    Run(prefix + "learning_changes_choice_without_cross_subject_effects", () =>
    {
        var f = new NeutralFixture(nested);
        Check(Ast.Equivalent(ActionLearning.Assess(f.Soul.World, f.Actor).First(a => !a.Forbidden).Decision.Effect, f.First),
            "Initial subjective preference must select the first known target.");
        var key = ActionLearning.Key(f.Soul.World, f.Actor, f.First);
        ActionLearning.Observe(f.Soul, f.Actor, key, DecisionOutcomeV1.Attempted);
        ActionLearning.Observe(f.Soul, f.Actor, key, DecisionOutcomeV1.Failed);
        Check(Ast.Equivalent(ActionLearning.Assess(f.Soul.World, f.Actor).First(a => !a.Forbidden).Decision.Effect, f.Second),
            "An observed failure in the same context must permit the alternative to win.");
        Check(Ast.Equivalent(ActionLearning.Assess(f.Other.World, f.OtherActor).First(a => !a.Forbidden).Decision.Effect, f.OtherFirst),
            "Another observer must keep its own untried preference.");
        Reject(() => ActionLearning.Key(f.Soul.World, f.OtherActor, f.First), "Foreign actor must be rejected.");
        Reject(() => ActionLearning.Key(f.Soul.World, f.Actor, f.OtherFirst), "Foreign effect reference must be rejected.");
        Reject(() => ActionLearning.Observe(f.Soul, f.OtherActor, key, DecisionOutcomeV1.Attempted), "Foreign actor experience must be rejected.");
        var state = Stored(f.Soul.World, f.Actor).Experiences.Single();
        Check(state.Key == key && state.Attempts == 1 && state.Failures == 1 && state.Successes == 0 && state.NoProgress == 0,
            "Actual observation must store the original attempted context.");
    });

    Run(prefix + "actual_execution_receipts_keep_failure_success_and_cancel_separate", () =>
    {
        var f = new NeutralFixture(nested);
        using var session = new SimulationSession(f.Runtime);
        var inspectedKey = ActionLearning.Key(f.Soul.World, f.Actor, f.First);
        ActionLearning.Observe(f.Soul, f.Actor, inspectedKey, DecisionOutcomeV1.Attempted);
        var inspected = session.Submit(f.Soul, new(f.Actor, f.First));
        Check(session.CTB.Step() && inspected.Status == ExecutionStatus.Completed && inspected.Result?.Succeeded == true,
            "An actual inspection must complete before it is learned as successful.");
        ActionLearning.Observe(f.Soul, f.Actor, inspectedKey, DecisionOutcomeV1.Succeeded, session.CTB.Now);
        var unsupported = new Effect.Act(ActionOp.Travel, f.Target);
        var intended = new Effect.Sequence([unsupported, f.Second]);
        var failed = session.Submit(f.Soul, new(f.Actor, intended));
        Check(session.CTB.Step() && failed.Status == ExecutionStatus.Failed && failed.Result?.Succeeded == false
            && failed.Result.FailedEffect is not null && Ast.Equivalent(failed.Result.FailedEffect, unsupported)
            && failed.Events.All(e => e.Fields.Phase != EventPhaseV1.Committed), "The actual failed effect must be retained without pretending later effects ran.");
        var failedKey = ActionLearning.Key(f.Soul.World, f.Actor, failed.Result!.FailedEffect!);
        ActionLearning.Observe(f.Soul, f.Actor, failedKey, DecisionOutcomeV1.Attempted);
        ActionLearning.Observe(f.Soul, f.Actor, failedKey, DecisionOutcomeV1.Failed, session.CTB.Now);
        var pendingEffect = new Effect.Wait(10, f.Second);
        var interruptedKey = ActionLearning.Key(f.Soul.World, f.Actor, pendingEffect);
        ActionLearning.Observe(f.Soul, f.Actor, interruptedKey, DecisionOutcomeV1.Attempted);
        var pending = session.Submit(f.Soul, new(f.Actor, pendingEffect, Delay: 10));
        Check(session.Cancel(pending) && pending.Status == ExecutionStatus.Cancelled, "Pending execution must produce a real cancellation receipt.");
        ActionLearning.Observe(f.Soul, f.Actor, interruptedKey, DecisionOutcomeV1.Interrupted, session.CTB.Now);
        var rows = Stored(f.Soul.World, f.Actor).Experiences;
        Check(rows.Single(e => e.Key == inspectedKey) is { Successes: 1, Failures: 0, Interruptions: 0 }
            && rows.Single(e => e.Key == failedKey) is { Successes: 0, Failures: 1, Interruptions: 0 }
            && rows.Single(e => e.Key == interruptedKey) is { Successes: 0, Failures: 0, Interruptions: 1 },
            "Learning the real receipts must preserve distinct execution outcomes.");
    });

    Run(prefix + "unknown_unrelated_and_remote_changes_do_not_reset_failure", () =>
    {
        var f = new NeutralFixture(nested);
        var key = ActionLearning.Key(f.Soul.World, f.Actor, f.First);
        ActionLearning.Observe(f.Soul, f.Actor, key, DecisionOutcomeV1.Attempted);
        ActionLearning.Observe(f.Soul, f.Actor, key, DecisionOutcomeV1.NoProgress);
        var score = Scores(f.Soul.World, f.Actor);
        Observation.Observe(f.Soul, new("neutral.unrecognized", ProjectionKind.Object) { Source = ObservationSource.Testimony,
            Existence = Truth.Unknown, Details = "neutral.unknown" });
        Observation.Observe(f.Soul, new(f.Target.Name, f.Target.Kind) { Target = f.Target, Source = ObservationSource.Testimony,
            Details = "neutral.no_new_state" });
        Check(key == ActionLearning.Key(f.Soul.World, f.Actor, f.First) && score == Scores(f.Soul.World, f.Actor),
            "An unknown object or a report with no new target state must not reset failure.");
        f.TargetEntity.Get<StateProperty>()!.Values[new("neutral.state")] = 7;
        f.HiddenEntity.Get<StateProperty>()!.Values[new("neutral.hidden")] = 91;
        f.Runtime.Sync();
        Check(key == ActionLearning.Key(f.Soul.World, f.Actor, f.First) && score == Scores(f.Soul.World, f.Actor),
            "Unobserved authoritative changes must not reset failure from afar.");
        Observation.Observe(f.Other, new(f.OtherTarget.Name, f.OtherTarget.Kind) { Target = f.OtherTarget,
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(new("neutral.state"), 7) });
        Check(key == ActionLearning.Key(f.Soul.World, f.Actor, f.First) && score == Scores(f.Soul.World, f.Actor),
            "A different subject's observation must not reset this subject's failure.");
        var before = Ok(CognitionCodec.Encode(Ok(f.Runtime.CaptureCognitionGraph(f.Soul))));
        ActionLearning.Assess(f.Soul.World, f.Actor);
        Check(before.SequenceEqual(Ok(CognitionCodec.Encode(Ok(f.Runtime.CaptureCognitionGraph(f.Soul))))), "Assessment must not write cognition.");
    });

    Run(prefix + "relevant_personal_observation_allows_retry_and_keeps_history", () =>
    {
        var f = new NeutralFixture(nested);
        var old = ActionLearning.Key(f.Soul.World, f.Actor, f.First);
        ActionLearning.Observe(f.Soul, f.Actor, old, DecisionOutcomeV1.Attempted);
        ActionLearning.Observe(f.Soul, f.Actor, old, DecisionOutcomeV1.Failed);
        Observation.Observe(f.Soul, new(f.Target.Name, f.Target.Kind) { Target = f.Target, Source = ObservationSource.Perception,
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(new("neutral.state"), 1) });
        var current = ActionLearning.Key(f.Soul.World, f.Actor, f.First);
        Check(current != old && current.Method == old.Method, "Only the retry context must change after relevant personal state change.");
        Check(Ast.Equivalent(ActionLearning.Assess(f.Soul.World, f.Actor).First(a => !a.Forbidden).Decision.Effect, f.First),
            "Relevant personal knowledge must permit a fresh attempt.");
        Check(Stored(f.Soul.World, f.Actor).Experiences.Single().Key == old
            && Stored(f.Soul.World, f.Actor).Experiences.Single().Failures == 1, "The previous failed context must remain recorded.");
    });

    Run(prefix + "existing_bans_and_permission_threshold_survive_learning", () =>
    {
        var f = new NeutralFixture(nested);
        f.Rule(f.Soul, "neutral.ban", new(new Condition.Constant(Truth.True), RuleMode.Forbid, f.First, 5, Truth.True));
        var key = ActionLearning.Key(f.Soul.World, f.Actor, f.First);
        ActionLearning.Observe(f.Soul, f.Actor, key, DecisionOutcomeV1.Succeeded);
        var banned = ActionLearning.Assess(f.Soul.World, f.Actor).Single(a => Ast.Equivalent(a.Decision.Effect, f.First));
        Check(banned.Forbidden && Ast.Equivalent(ActionLearning.Assess(f.Soul.World, f.Actor).First(a => !a.Forbidden).Decision.Effect, f.Second),
            "Learned outcomes or information must not bypass an existing ban.");
        f.Rule(f.Soul, "neutral.weak_permission", new(new Condition.Constant(Truth.True), RuleMode.Permit, f.First, 4, Truth.True));
        Check(ActionLearning.Assess(f.Soul.World, f.Actor).Single(a => Ast.Equivalent(a.Decision.Effect, f.First)).Forbidden,
            "Permission below the ban threshold must remain insufficient.");
        f.Rule(f.Soul, "neutral.sufficient_permission", new(new Condition.Constant(Truth.True), RuleMode.Permit, f.First, 5, Truth.True));
        Check(!ActionLearning.Assess(f.Soul.World, f.Actor).Single(a => Ast.Equivalent(a.Decision.Effect, f.First)).Forbidden,
            "The original permission threshold must still allow the action.");
    });

    Run(prefix + "cognition_wire_and_runtime_save_preserve_experience", () =>
    {
        var f = new NeutralFixture(nested);
        var key = ActionLearning.Key(f.Soul.World, f.Actor, f.First);
        foreach (var outcome in Enum.GetValues<DecisionOutcomeV1>()) ActionLearning.Observe(f.Soul, f.Actor, key, outcome);
        var expected = JsonSerializer.Serialize(Stored(f.Soul.World, f.Actor));
        var expectedScores = Scores(f.Soul.World, f.Actor);
        var archive = Ok(f.Runtime.CaptureCognitionGraph(f.Soul));
        var bytes = Ok(CognitionCodec.Encode(archive));
        Preserve(prefix + "cognition.json", bytes);
        var rebound = Ok(CognitionGraph.Rebind(Ok(CognitionCodec.Decode(bytes))));
        var reboundActor = rebound.Projections[f.Actor.SubjectId!.Value];
        Check(expected == JsonSerializer.Serialize(Stored(rebound.World, reboundActor))
            && expectedScores == Scores(rebound.World, reboundActor), "Ordinary cognition codec and graph restore must preserve outcome counters and choices.");
        var saved = Ok(f.Runtime.Save());
        Preserve(prefix + "runtime-save.json", saved);
        var restored = Ok(WorldRuntime.Restore(saved));
        var soul = (SoulEssence)restored.Entities[f.Soul.Id];
        var restoredActor = soul.World.Projections.Single(p => p.SubjectId == f.Actor.SubjectId);
        Check(expected == JsonSerializer.Serialize(Stored(soul.World, restoredActor))
            && expectedScores == Scores(soul.World, restoredActor), "Runtime save restore must preserve all learned outcomes and choices.");
        Check(Ok(CognitionCodec.Encode(Ok(restored.Runtime.CaptureCognitionGraph(soul)))).SequenceEqual(bytes),
            "Runtime restoration must not replay observations or mutate saved cognition.");
    });

    Run(prefix + "role_reasoning_budgets_remain_distinct", () =>
    {
        var f = new NeutralFixture(nested);
        Condition P(string tag) => new Condition.Tagged(f.Target, new(tag), 1);
        f.Rule(f.Soul, "neutral.fact", new(new Condition.Constant(Truth.True), RuleMode.Infer, new Effect.Think(P("neutral.p")), 1, Truth.True));
        f.Rule(f.Soul, "neutral.first_implication", new(P("neutral.p"), RuleMode.Infer, new Effect.Think(P("neutral.q")), 1, Truth.True));
        f.Rule(f.Soul, "neutral.second_implication", new(P("neutral.q"), RuleMode.Infer, new Effect.Think(P("neutral.r")), 1, Truth.True));
        var snapshot = Ok(f.Soul.World.CaptureSnapshot());
        var query = Ok(CognitionAst.FreezeCondition(P("neutral.r"), f.Soul.World.Projections.ToImmutableDictionary(p => p.SubjectId!.Value)));
        var shallow = new SubjectiveReasoner(snapshot, f.Actor.SubjectId!.Value, new(0, 256, 16));
        var deep = new SubjectiveReasoner(snapshot, f.Actor.SubjectId!.Value, new(8, 4096, 16));
        Check(shallow.Prove(query).Value == Truth.Unknown && deep.Prove(query).Value == Truth.True,
            "Different role depths must retain different reachable deductions.");
        Check(Scores(f.Soul.World, f.Actor, new(0, 256, 16)) != Scores(f.Soul.World, f.Actor, new(8, 4096, 16)),
            "The live assessment must use its supplied role budget for subjective information.");
        Reject(() => ActionLearning.Assess(f.Soul.World, f.Actor, new(-1, 256, 16)), "Invalid role budget must be rejected.");
    });
}

void InvalidWire(string name, string json)
{
    Run(name, () =>
    {
        Preserve(name + ".json", Encoding.UTF8.GetBytes(json));
        var f = new NeutralFixture(false);
        f.InstallMemory(json);
        Reject(() => ActionLearning.Assess(f.Soul.World, f.Actor), "Invalid decision memory must be rejected when read.");
    });
}
var validRow = new DecisionExperienceV1(new("neutral.method", "neutral.context"), Attempts: 1, Failures: 1);
string Wire(DecisionMemoryV1 state) => JsonSerializer.Serialize(state);
InvalidWire("wire_unknown_version_rejected", Wire(new(2, [validRow])));
InvalidWire("wire_duplicate_experience_key_rejected", Wire(new(1, [validRow, validRow])));
InvalidWire("wire_negative_counter_rejected", Wire(new(1, [validRow with { NoProgress = -1 }])));
var unknown = JsonNode.Parse(Wire(new(1, [validRow])))!.AsObject(); unknown["UnlistedCapability"] = "neutral";
InvalidWire("wire_unknown_top_level_field_rejected", unknown.ToJsonString());
var unknownRow = JsonNode.Parse(Wire(new(1, [validRow])))!.AsObject(); unknownRow["Experiences"]![0]!["Unexpected"] = 1;
InvalidWire("wire_unknown_experience_field_rejected", unknownRow.ToJsonString());
InvalidWire("wire_duplicate_json_property_rejected", "{\"Version\":2,\"Version\":1,\"Experiences\":[]}");
InvalidWire("wire_duplicate_nested_json_property_rejected", "{\"Version\":1,\"Experiences\":[{\"Key\":{\"Method\":\"neutral.method\",\"Context\":\"\"},\"Attempts\":-1,\"Attempts\":0,\"Successes\":0,\"Failures\":0,\"NoProgress\":0,\"Interruptions\":0}]}");
Run("wire_duplicate_key_across_storage_nodes_rejected", () =>
{
    var f = new NeutralFixture(false);
    f.InstallMemory(Wire(new(1, [validRow]))); f.InstallMemory(Wire(new(1, [validRow])));
    Reject(() => ActionLearning.Capture(f.Soul.World, f.Actor), "Public merged export must reject duplicate keys across storage nodes.");
    Reject(() => ActionLearning.Assess(f.Soul.World, f.Actor), "Assessment must reject duplicate keys across storage nodes.");
});

Console.WriteLine(failures.Count == 0 ? "SUBJECTIVE_DECISION_REGRESSION: PASS" : "SUBJECTIVE_DECISION_REGRESSION: FAIL");
Environment.ExitCode = failures.Count == 0 ? 0 : 1;

sealed class NeutralFixture
{
    public WorldRuntime Runtime { get; } = new();
    public SoulEssence Soul { get; } = new(new EntityId("neutral.observer"), "neutral.observer");
    public SoulEssence Other { get; } = new(new EntityId("neutral.other"), "neutral.other");
    public Essence TargetEntity { get; } = new(new EntityId("neutral.target"), "neutral.target");
    public Essence HiddenEntity { get; } = new(new EntityId("neutral.hidden"), "neutral.hidden");
    public Projection Actor { get; }
    public Projection OtherActor { get; }
    public Projection Target { get; }
    public Projection OtherTarget { get; }
    public Effect First { get; }
    public Effect Second { get; }
    public Effect OtherFirst { get; }
    public NeutralFixture(bool nested)
    {
        var root = new Essence(new EntityId("neutral.root"), "neutral.root", ProjectionKind.Space);
        var room = new Essence(new EntityId("neutral.room"), "neutral.room", ProjectionKind.Space);
        var body = new Essence(new EntityId("neutral.body"), "neutral.body", ProjectionKind.Body);
        var second = new Essence(new EntityId("neutral.second"), "neutral.second");
        if (nested) room.Add(new SpatialProperty { Space = root });
        body.Add(new SpatialProperty { Space = room });
        TargetEntity.Add(new SpatialProperty { Space = room });
        second.Add(new SpatialProperty { Space = room });
        var state = TargetEntity.Add(new StateProperty()); state.Values[new("neutral.state")] = 0; state.Observable.Add(new("neutral.state"));
        HiddenEntity.Add(new StateProperty()).Values[new("neutral.hidden")] = 0;
        Soul.Relations.Add((Relations.Controls, body)); Other.Relations.Add((Relations.Controls, body));
        Runtime.Project(Soul, new FacetId("neutral.observer.facet"), "neutral.observer");
        Runtime.Project(Other, new FacetId("neutral.other.facet"), "neutral.other");
        var rr = Runtime.Project(root, new FacetId("neutral.root.facet"), "neutral.root");
        var rm = Runtime.Project(room, new FacetId("neutral.room.facet"), "neutral.room", nested ? rr : null);
        var rb = Runtime.Project(body, new FacetId("neutral.body.facet"), "neutral.body");
        var rt = Runtime.Project(TargetEntity, new FacetId("neutral.target.facet"), "neutral.target");
        var rs = Runtime.Project(second, new FacetId("neutral.second.facet"), "neutral.second");
        Runtime.Project(HiddenEntity, new FacetId("neutral.hidden.facet"), "neutral.hidden");
        var known = Runtime.Observe(Soul, [rr, rm, rb, rt, rs]);
        var other = Runtime.Observe(Other, [rr, rm, rb, rt, rs]);
        Actor = known[2]; Target = known[3]; OtherActor = other[2]; OtherTarget = other[3];
        First = new Effect.Act(ActionOp.Inspect, Target); Second = new Effect.Act(ActionOp.Inspect, known[4]);
        OtherFirst = new Effect.Act(ActionOp.Inspect, OtherTarget);
        Rule(Soul, "neutral.first_preference", new(new Condition.Constant(Truth.True), RuleMode.Prefer, First, 20, Truth.True));
        Rule(Soul, "neutral.second_preference", new(new Condition.Constant(Truth.True), RuleMode.Prefer, Second, 10, Truth.True));
        Rule(Other, "neutral.first_preference", new(new Condition.Constant(Truth.True), RuleMode.Prefer, OtherFirst, 20, Truth.True));
        Rule(Other, "neutral.second_preference", new(new Condition.Constant(Truth.True), RuleMode.Prefer,
            new Effect.Act(ActionOp.Inspect, other[4]), 10, Truth.True));
    }
    public Projection Rule(SoulEssence soul, string name, Rule rule)
        => Observation.Observe(soul, new(name, ProjectionKind.Concept) { Source = ObservationSource.Introspection, Existence = Truth.True, Rules = [rule] });
    public Projection InstallMemory(string json)
        => Observation.Observe(Soul, new("neutral.memory", ProjectionKind.Concept) { Source = ObservationSource.Introspection,
            Existence = Truth.True, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(new("core.decision-experience.v1"), 1),
            AddRelations = [(Relations.Subject, Actor)], Details = json });
}
