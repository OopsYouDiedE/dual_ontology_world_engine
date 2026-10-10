using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

// Neutral propositions and visible inputs, independent of chapter content.
internal static class InformationContextCases
{
    private static PlayerCognition Initial()
    {
        var runtime = new WorldRuntime();
        using var simulation = new SimulationSession(runtime);
        var world = new CoreScriptWorld(runtime, simulation);
        var actor = world.DefineCharacter("neutral.context.actor", "neutral-actor", world.DefineSpace("neutral.context.room", "neutral-room"));
        var snapshot = ((ContractResult<CognitionSnapshotV1>.Success)actor.Soul.World.CaptureSnapshot()).Value;
        var cognition = new PlayerCognition(snapshot, actor.Self.SubjectId!.Value);
        return Add(Add(Add(cognition, "neutral-question", "goal-a", Truth.Unknown, AcquiredPropositions.Question),
            "neutral-person-a", "goal-a", Truth.Unknown, AcquiredPropositions.Opportunity),
            "neutral-person-b", "goal-b", Truth.Unknown, AcquiredPropositions.Opportunity);
    }
    private static PlayerCognition Add(PlayerCognition held, string name, string goal, Truth accepted, Tag? tag = null)
    {
        var material = new FrozenProjectionV1(new(held.Snapshot.SubjectWorld, Guid.NewGuid()), name, ProjectionKind.Concept,
            Truth.True, null, tag is null ? ImmutableDictionary<Tag, int>.Empty : ImmutableDictionary<Tag, int>.Empty.Add(tag.Value, 1), [], [],
            [new(new FrozenConditionV1.Constant(Truth.True), RuleMode.Infer,
                new FrozenEffectV1.Think(new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(held.Actor), new(goal), 1)), 1, accepted)],
            null, [new(new("acquired.neutral." + name), name, name)], null);
        return held with { Snapshot = held.Snapshot with { Projections = held.Snapshot.Projections.Add(material) } };
    }
    private static SubjectivePlayerController Controller(int expansions = 4096) => new(new()
    {
        Actor = "neutral.context.actor", Name = "neutral-actor", Mode = "agent",
        Rules = [new() { Mode = "prefer", Kind = "untried", Weight = 6 },
            new() { Mode = "prefer", Kind = "menu", Weight = 1, LabelContains = "neutral-topic" }],
        Reasoning = new() { Depth = 8, Expansions = expansions, Hypotheses = 128, ArgumentWeight = 16, InformationWeight = 8 }
    }, 23);
    private static PublicPlayerCandidate Person(string token, string person) =>
        new(token, person, "interact", false) { Location = new(100, 100) };
    private static PublicPlayerCandidate Topic(string token) => new(token, "neutral-topic", "menu", false);
    private static PublicPlayerCandidate Close(string token) => new(token, "neutral-close", "menu", false, Purpose: "close");
    private static PublicPlayerObservation At(long turn, params PublicPlayerCandidate[] candidates) =>
        new(turn, [], [], [.. candidates], false) { Field = new("neutral-context-field", new(100, 100)) };
    private static void Contact(SubjectivePlayerController controller, PublicPlayerObservation observation, PlayerCognition cognition)
    {
        var decision = controller.Decide(observation, cognition);
        if (!controller.CompleteInteraction(decision.Token!, true))
            throw new InvalidOperationException("Neutral conversation contact was not accepted");
    }
    private static bool Novel(PlayerAlternative alternative) => alternative.Basis.Contains("authored_rule:prefer:untried:6");
    private static void Observe(string name, object value) => Console.WriteLine("OBSERVATION " + name + " " + JsonSerializer.Serialize(value));

    public static void Run(Action<string, bool> check)
    {
        var held = Initial();
        var repeated = Controller();
        Contact(repeated, At(1, Person("first-a", "neutral-person-a")), held);
        repeated.Decide(At(2, Topic("first-topic"), Close("first-close")), held);
        var repeatedQuestion = Add(held, "neutral-question-copy", "goal-a", Truth.Unknown, AcquiredPropositions.Question);
        var afterRepeat = repeated.Decide(At(3, Topic("repeat-topic"), Close("repeat-close")), repeatedQuestion);
        Observe("same-question-does-not-reopen", afterRepeat);
        check("repeated_same_question_does_not_refresh_interview_topic", afterRepeat.Token == "repeat-close"
            && !Novel(afterRepeat.Alternatives.Single(a => a.Token == "repeat-topic")));
        var unrelated = Add(repeatedQuestion, "neutral-unrelated-proof", "unrelated-goal", Truth.True);
        var afterUnrelated = repeated.Decide(At(4, Topic("unrelated-topic"), Close("unrelated-close")), unrelated);
        check("unrelated_accepted_proof_does_not_refresh_interview_topic", afterUnrelated.Token == "unrelated-close"
            && !Novel(afterUnrelated.Alternatives.Single(a => a.Token == "unrelated-topic")));
        var answer = Add(unrelated, "neutral-proof-a", "goal-a", Truth.True);
        var afterAnswer = repeated.Decide(At(5, Topic("answered-topic"), Close("answered-close")), answer);
        Observe("relevant-proof-reopens-topic", afterAnswer);
        check("changed_relevant_proof_reopens_corresponding_interview_topic", afterAnswer.Token == "answered-topic"
            && Novel(afterAnswer.Alternatives.Single(a => a.Token == "answered-topic")));
        var redundant = Add(answer, "neutral-redundant-proof-a", "goal-a", Truth.True);
        var afterRedundant = repeated.Decide(At(6, Topic("redundant-topic"), Close("redundant-close")), redundant);
        check("extra_support_with_same_proof_state_does_not_refresh_topic", afterRedundant.Token == "redundant-close"
            && !Novel(afterRedundant.Alternatives.Single(a => a.Token == "redundant-topic")));

        var isolated = Controller();
        Contact(isolated, At(1, Person("isolated-a", "neutral-person-a")), held);
        isolated.Decide(At(2, Topic("isolated-a-topic"), Close("isolated-a-close")), held);
        Contact(isolated, At(3, Person("isolated-b", "neutral-person-b")), held);
        var otherPerson = isolated.Decide(At(4, Topic("isolated-b-topic"), Close("isolated-b-close")), held);
        check("different_people_with_same_menu_label_have_independent_attempts", otherPerson.Token == "isolated-b-topic"
            && Novel(otherPerson.Alternatives.Single(a => a.Token == "isolated-b-topic")));
        var noRepeatedQuestion = true;
        PlayerDecision? last = null;
        for (var turn = 5; turn <= 28; turn++)
        {
            last = isolated.Decide(At(turn, Topic("b-topic-" + turn), Close("b-close-" + turn)), held);
            noRepeatedQuestion &= last.Token == "b-close-" + turn
                && !Novel(last.Alternatives.Single(a => a.Token == "b-topic-" + turn));
        }
        Observe("same-person-no-new-information", last!);
        check("same_person_without_new_information_does_not_keep_reasking", noRepeatedQuestion);
        var bAfterAProof = isolated.Decide(At(29, Topic("b-topic-after-a-proof"), Close("b-close-after-a-proof")), answer);
        Observe("proof-for-other-person-keeps-current-topic-history", bAfterAProof);
        Observe("neutral-distinct-proposition-serialization", new
        {
            goalA = JsonSerializer.Serialize<FrozenConditionV1>(new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(held.Actor), new("goal-a"), 1)),
            goalB = JsonSerializer.Serialize<FrozenConditionV1>(new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(held.Actor), new("goal-b"), 1))
        });
        check("proof_for_other_person_does_not_reopen_current_person_menu", bAfterAProof.Token == "b-close-after-a-proof"
            && !Novel(bAfterAProof.Alternatives.Single(a => a.Token == "b-topic-after-a-proof")));

        var referenced = Add(held, "neutral-distinct-reference", "unrelated-goal", Truth.Unknown);
        var otherReference = referenced.Snapshot.Projections.Last().Id;
        var otherTargetClaim = new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(otherReference), new("goal-a"), 1);
        referenced = referenced with { Snapshot = referenced.Snapshot with { Projections = [.. referenced.Snapshot.Projections.Select(p =>
            p.Name == "neutral-person-b" ? p with { Rules = [.. p.Rules.Select(r => r with { Effect = new FrozenEffectV1.Think(otherTargetClaim) })] } : p)] } };
        var targetIsolation = Controller();
        Contact(targetIsolation, At(1, Person("reference-a", "neutral-person-a")), referenced);
        targetIsolation.Decide(At(2, Topic("reference-a-topic")), referenced);
        Contact(targetIsolation, At(3, Person("reference-b", "neutral-person-b")), referenced);
        targetIsolation.Decide(At(4, Topic("reference-b-topic")), referenced);
        var actorOnlyProof = Add(referenced, "neutral-actor-only-proof", "goal-a", Truth.True);
        var referenceAfterProof = targetIsolation.Decide(At(5, Topic("reference-b-repeat"), Close("reference-b-close")), actorOnlyProof);
        Observe("same-tag-distinct-subject-reference", referenceAfterProof);
        check("same_tag_on_distinct_subject_reference_keeps_interview_history", referenceAfterProof.Token == "reference-b-close"
            && !Novel(referenceAfterProof.Alternatives.Single(a => a.Token == "reference-b-repeat")));

        var conditional = Add(held, "neutral-conditional-rule", "goal-a", Truth.True);
        FrozenConditionV1 Required(string tag) => new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(held.Actor), new(tag), 1);
        PlayerCognition Requirement(PlayerCognition cognition, string tag) => cognition with
        {
            Snapshot = cognition.Snapshot with { Projections = [.. cognition.Snapshot.Projections.Select(p =>
                p.Name == "neutral-conditional-rule" ? p with { Rules = [.. p.Rules.Select(r => r with { Condition = Required(tag) })] } : p)] }
        };
        conditional = Requirement(conditional, "required-a");
        var changedMissing = Controller();
        Contact(changedMissing, At(1, Person("missing-person", "neutral-person-a")), conditional);
        changedMissing.Decide(At(2, Topic("missing-first-topic")), conditional);
        var nextRequired = Requirement(conditional, "required-b");
        var afterMissingChange = changedMissing.Decide(At(3, Topic("missing-changed-topic"), Close("missing-close")), nextRequired);
        Observe("same-claim-different-missing-proposition", afterMissingChange);
        check("changed_missing_proposition_with_same_truth_state_reopens_related_input", afterMissingChange.Token == "missing-changed-topic"
            && Novel(afterMissingChange.Alternatives.Single(a => a.Token == "missing-changed-topic")));

        foreach (var reverse in new[] { false, true })
        {
            var closer = Person("near", "neutral-person-a") with { TravelDistance = 64 };
            var farther = Person("far", "neutral-person-a") with { TravelDistance = 512 };
            var distance = Controller().Decide(At(1, reverse ? closer : farther, reverse ? farther : closer), held);
            Observe("comparable-route-distance " + reverse, distance);
            check("equal_information_prefers_nearer_observed_route_" + reverse, distance.Token == "near"
                && distance.Alternatives.Single(a => a.Token == "near").Score > distance.Alternatives.Single(a => a.Token == "far").Score
                && distance.Alternatives.Single(a => a.Token == "far").Basis.Contains("observed_route_travel_distance:512.0"));
        }

        // Candidate multiplicity must not turn a resource limit into fresh knowledge.
        var budgeted = Controller(expansions: 1);
        Contact(budgeted, At(1, Person("budget-first", "neutral-person-a")), held);
        var budgetRepeat = budgeted.Decide(At(2, Person("budget-repeat", "neutral-person-a"),
            new("budget-wait", "neutral-wait", "wait", false)), held);
        Observe("unchanged-material-under-small-budget", budgetRepeat);
        check("reasoning_budget_exhaustion_does_not_refresh_unchanged_input", !Novel(budgetRepeat.Alternatives.Single(a => a.Token == "budget-repeat")));
    }
}
