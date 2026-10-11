using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

// Neutral mechanism fixture. The memory reflection is read-only and never
// injects experience, world state, chapter data, geometry or receipts.
internal static class InteractionReceiptCases
{
    private const string Space = "neutral-receipt-space";
    private const string Source = "neutral-receipt-source";
    private static SubjectivePlayerController Controller() => new(new()
    {
        Actor = "neutral.receipt.actor", Name = "neutral-actor", Mode = "agent",
        Rules = [new() { Mode = "prefer", Kind = "untried", Weight = 6 },
            new() { Mode = "prefer", Kind = "interact", Weight = 3 }],
        Reasoning = new() { Depth = 8, Expansions = 4096, Hypotheses = 128, ArgumentWeight = 16, InformationWeight = 8 }
    }, 23);
    private static PlayerCognition Cognition()
    {
        var runtime = new WorldRuntime();
        using var simulation = new SimulationSession(runtime);
        var world = new CoreScriptWorld(runtime, simulation);
        var actor = world.DefineCharacter("neutral.receipt.actor", "neutral-actor",
            world.DefineSpace("neutral.receipt.room", "neutral-room"));
        var snapshot = ((ContractResult<CognitionSnapshotV1>.Success)actor.Soul.World.CaptureSnapshot()).Value;
        var cognition = new PlayerCognition(snapshot, actor.Self.SubjectId!.Value);
        return Add(Add(cognition, "neutral-receipt-question", "neutral-receipt-goal", Truth.Unknown,
            AcquiredPropositions.Question), Source, "neutral-receipt-goal", Truth.Unknown, AcquiredPropositions.Opportunity);
    }
    private static PlayerCognition Add(PlayerCognition held, string name, string tag, Truth accepted, Tag? marker = null)
    {
        var material = new FrozenProjectionV1(new(held.Snapshot.SubjectWorld, Guid.NewGuid()), name,
            ProjectionKind.Concept, Truth.True, null,
            marker is null ? ImmutableDictionary<Tag, int>.Empty : ImmutableDictionary<Tag, int>.Empty.Add(marker.Value, 1), [], [],
            [new(new FrozenConditionV1.Constant(Truth.True), RuleMode.Infer,
                new FrozenEffectV1.Think(new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(held.Actor), new(tag), 1)),
                1, accepted)], null, [new(new("acquired.neutral." + name), name, name)], null);
        return held with { Snapshot = held.Snapshot with { Projections = held.Snapshot.Projections.Add(material) } };
    }
    private static PublicPlayerCandidate Place(string token, string label = Source) =>
        new(token, label, "interact", false) { Location = new(1024, 16) };
    private static PublicPlayerObservation At(long turn, params PublicPlayerCandidate[] offers) =>
        new(turn, [], [], [.. offers], false) { Field = new(Space, new(16, 16)) };
    private static SubjectiveDecisionMemory History(SubjectivePlayerController controller) =>
        (SubjectiveDecisionMemory)(typeof(SubjectivePlayerController)
            .GetField("_experience", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(controller)
            ?? throw new InvalidOperationException("Neutral receipt fixture cannot inspect the existing experience adapter"));
    private static DecisionExperienceV1 Memory(SubjectivePlayerController controller, string kind = "interact", string label = Source) =>
        History(controller).Get(new(kind + ":" + (kind == "interact" ? Space + ":" : kind == "menu" ? Space + "::" : "") + label));
    private static DecisionExperienceV1[] Routes(SubjectivePlayerController controller, string label = Source) =>
        History(controller).Capture().Experiences.Where(e => e.Key.Method == "contact:" + Space + ":" + label).ToArray();
    private static int RouteFailures(SubjectivePlayerController controller, string label = Source) =>
        Routes(controller, label).Sum(e => e.Failures);
    private static bool Inquiry(PlayerAlternative alternative) => alternative.Basis.Any(b =>
        b.StartsWith("seek_current_question_material:", StringComparison.Ordinal)
        || b.StartsWith("seek_unknown_acquired_proposition:", StringComparison.Ordinal));
    private static void Observe(string name, object value) =>
        Console.WriteLine("OBSERVATION interaction-receipt " + name + " " + JsonSerializer.Serialize(value));

    public static void Run(Action<string, bool> check)
    {
        var held = Cognition();
        var results = new List<(string Name, bool Holds)>();
        void Record(string name, bool holds) => results.Add((name, holds));

        var unreachable = Controller();
        var scores = new List<int>();
        var failedTokens = new List<string>();
        for (var turn = 1; turn <= 4; turn++)
        {
            var decision = unreachable.Decide(At(turn, Place("unreached-" + turn)), held);
            var before = Memory(unreachable);
            var alternative = decision.Alternatives.Single();
            scores.Add(alternative.Score);
            Record("field_selection_waits_for_actual_contact_" + turn, before.Attempts == 0 && before.Successes == 0);
            Record("unreached_unknown_source_retains_information_reward_" + turn,
                Inquiry(alternative) && alternative.Basis.Contains("authored_rule:prefer:untried:6"));
            var accepted = unreachable.CompleteInteraction(decision.Token!, false);
            var after = Memory(unreachable);
            failedTokens.Add(decision.Token!);
            Observe("unreached-path " + turn, new { decision, accepted, before, after, routeHistory = Routes(unreachable) });
            Record("unreached_path_records_failure_without_examination_" + turn,
                accepted && after.Attempts == 0 && after.Successes == 0 && RouteFailures(unreachable) == turn);
        }
        Record("repeated_unreached_paths_retain_increasing_failure_cost",
            scores.Zip(scores.Skip(1), (earlier, later) => later < earlier).All(v => v));

        var contact = unreachable.Decide(At(5, Place("actual-contact")), held);
        var beforeContact = Memory(unreachable);
        var forged = unreachable.CompleteInteraction("invented-neutral-token", true);
        var afterForged = Memory(unreachable);
        var old = unreachable.CompleteInteraction(failedTokens[0], true);
        var acceptedContact = unreachable.CompleteInteraction(contact.Token!, true);
        var afterContact = Memory(unreachable);
        var duplicateSuccess = unreachable.CompleteInteraction(contact.Token!, true);
        var duplicateFailure = unreachable.CompleteInteraction(contact.Token!, false);
        Observe("real-contact-and-token-isolation", new
        {
            contact, beforeContact, forged, afterForged, old, acceptedContact, afterContact,
            duplicateSuccess, duplicateFailure, final = Memory(unreachable), routeHistory = Routes(unreachable)
        });
        Record("forged_and_old_tokens_leave_current_contact_pending",
            !forged && afterForged == beforeContact && !old && acceptedContact);
        Record("actual_contact_records_attempt_and_success_while_preserving_failures",
            afterContact.Attempts == 1 && afterContact.Successes == 1 && RouteFailures(unreachable) == 4);
        Record("contact_receipt_is_one_time_for_both_outcomes",
            !duplicateSuccess && !duplicateFailure && Memory(unreachable) == afterContact && RouteFailures(unreachable) == 4);

        var second = unreachable.Decide(At(6, Place("second-actual-contact")), held);
        Record("one_actual_examination_has_no_automatic_second_inquiry", !Inquiry(second.Alternatives.Single()));
        Record("second_actual_contact_is_accepted", unreachable.CompleteInteraction(second.Token!, true));
        var exhausted = unreachable.Decide(At(7, Place("exhausted-source")), held);
        Observe("actual-examinations-exhaust-source", new { second, exhausted, history = Memory(unreachable) });
        Record("actual_examinations_exhaust_unknown_source_reward",
            Memory(unreachable).Attempts == 2 && Memory(unreachable).Successes == 2
            && !Inquiry(exhausted.Alternatives.Single()));
        Record("post_exhaustion_unreached_path_preserves_actual_attempts",
            unreachable.CompleteInteraction(exhausted.Token!, false) && Memory(unreachable).Attempts == 2);

        var unrelated = Add(held, "neutral-unrelated-proof", "neutral-other-goal", Truth.True);
        var stillExhausted = unreachable.Decide(At(8, Place("unrelated-proof-source")), unrelated);
        Record("unrelated_proof_does_not_reopen_actual_examination_history",
            Memory(unreachable).Attempts == 2 && !Inquiry(stillExhausted.Alternatives.Single()));
        unreachable.CompleteInteraction(stillExhausted.Token!, false);
        var beforeProof = Memory(unreachable);
        var routeFailuresBeforeProof = RouteFailures(unreachable);
        var relevant = Add(held, "neutral-receipt-proof", "neutral-receipt-goal", Truth.True);
        var reopened = unreachable.Decide(At(9, Place("reopened-source")), relevant);
        var afterProof = Memory(unreachable);
        Observe("relevant-proof-reopens-with-failure-memory", new { reopened, beforeProof, afterProof,
            routeFailuresBeforeProof, routeHistory = Routes(unreachable) });
        Record("relevant_proof_reopens_examination_but_retains_execution_failures",
            afterProof.Attempts == 0 && RouteFailures(unreachable) == routeFailuresBeforeProof && afterProof.Successes == 2
            && reopened.Basis.Contains("authored_rule:prefer:untried:6")
            && reopened.Basis.Contains("observed_action_failed:" + routeFailuresBeforeProof));
        unreachable.CompleteInteraction(reopened.Token!, false);

        var superseded = Controller();
        var prior = superseded.Decide(At(1, Place("pending-prior")), held);
        var current = superseded.Decide(At(2, Place("pending-current", "neutral-other-source")), held);
        var priorMemory = Memory(superseded);
        var currentMemory = Memory(superseded, label: "neutral-other-source");
        var priorRejected = !superseded.CompleteInteraction(prior.Token!, true);
        var currentAccepted = superseded.CompleteInteraction(current.Token!, true);
        Observe("unreported-prior-and-current-token", new { prior, current, priorMemory, currentMemory,
            priorRejected, currentAccepted, finalPrior = Memory(superseded),
            finalCurrent = Memory(superseded, label: "neutral-other-source") });
        Record("new_decision_ends_unreported_prior_contact_as_failure",
            priorMemory.Attempts == 0 && priorMemory.Successes == 0 && RouteFailures(superseded) == 1);
        Record("late_prior_receipt_cannot_complete_another_contact",
            priorRejected && currentMemory.Attempts == 0 && currentAccepted
            && Memory(superseded, label: "neutral-other-source").Attempts == 1);

        var emptyReplacement = Controller();
        var expired = emptyReplacement.Decide(At(1, Place("before-empty")), held);
        emptyReplacement.Decide(At(2), held);
        Observe("empty-replacement-ends-pending", new { examination = Memory(emptyReplacement), routes = Routes(emptyReplacement) });
        Record("empty_candidate_replacement_also_ends_pending_contact",
            !emptyReplacement.CompleteInteraction(expired.Token!, true)
            && Memory(emptyReplacement).Attempts == 0 && RouteFailures(emptyReplacement) == 1);

        var missingActor = Controller();
        var invalidated = missingActor.Decide(At(1, Place("before-missing-actor")), held);
        var missingResult = missingActor.Decide(At(2, Place("missing-actor-next")),
            held with { Actor = new(held.Snapshot.SubjectWorld, Guid.NewGuid()) });
        Observe("missing-actor-ends-pending", new { missingResult, history = Memory(missingActor), routes = Routes(missingActor) });
        Record("missing_actor_early_return_also_ends_pending_contact",
            !missingActor.CompleteInteraction(invalidated.Token!, true)
            && Memory(missingActor).Attempts == 0 && RouteFailures(missingActor) == 1);

        var conversation = Controller();
        var personA = conversation.Decide(At(1, Place("person-a", "neutral-person-a")), held);
        conversation.CompleteInteraction(personA.Token!, true);
        conversation.Decide(At(2, new PublicPlayerCandidate("person-a-topic", "neutral-receipt-topic", "menu", false)), held);
        var personB = conversation.Decide(At(3, Place("person-b-unreached", "neutral-person-b")), held);
        conversation.CompleteInteraction(personB.Token!, false);
        var afterFailedB = conversation.Decide(At(4,
            new("person-a-topic-after-failed-b", "neutral-receipt-topic", "menu", false),
            new("person-a-close-after-failed-b", "neutral-close", "menu", false, Purpose: "close")), held);
        var retainedTopic = afterFailedB.Alternatives.Single(a => a.Token == "person-a-topic-after-failed-b");
        var reachedB = conversation.Decide(At(5, Place("person-b-reached", "neutral-person-b")), held);
        conversation.CompleteInteraction(reachedB.Token!, true);
        var firstBTopic = conversation.Decide(At(6, new PublicPlayerCandidate("person-b-first-topic", "neutral-receipt-topic", "menu", false)), held);
        var reachedA = conversation.Decide(At(7, Place("person-a-return", "neutral-person-a")), held);
        conversation.CompleteInteraction(reachedA.Token!, true);
        var returnATopic = conversation.Decide(At(8,
            new("person-a-return-topic", "neutral-receipt-topic", "menu", false),
            new("person-a-return-close", "neutral-close", "menu", false, Purpose: "close")), held);
        var returnAlternative = returnATopic.Alternatives.Single(a => a.Token == "person-a-return-topic");
        Observe("failed-person-approach-preserves-current-topic", new { afterFailedB, firstBTopic, returnATopic });
        Record("failed_approach_to_another_person_does_not_refresh_current_person_topic",
            afterFailedB.Token == "person-a-close-after-failed-b"
            && retainedTopic.Basis.Contains("revisit_without_new_accepted_material:1")
            && !retainedTopic.Basis.Contains("authored_rule:prefer:untried:6"));
        Record("only_real_contact_switches_person_topic_scope_and_preserves_return_history",
            firstBTopic.Basis.Contains("authored_rule:prefer:untried:6")
            && returnAlternative.Basis.Contains("revisit_without_new_accepted_material:1")
            && !returnAlternative.Basis.Contains("authored_rule:prefer:untried:6"));

        var contextual = Controller();
        var firstExamination = contextual.Decide(At(1, Place("context-examination")), held);
        contextual.CompleteInteraction(firstExamination.Token!, true);
        var failedRoute = contextual.Decide(At(2, Place("context-failed-route")), held);
        contextual.CompleteInteraction(failedRoute.Token!, false);
        var sameRoute = contextual.Decide(At(3, Place("context-same-route")), held);
        Record("same_public_start_and_target_cells_retain_route_failure_cost",
            sameRoute.Basis.Contains("observed_action_failed:1") && Memory(contextual).Attempts == 1);
        contextual.CompleteInteraction(sameRoute.Token!, false);
        var newStart = contextual.Decide(At(4, Place("context-new-player-position")) with
            { Field = new(Space, new(80, 16)) }, held);
        Record("new_public_player_cell_does_not_inherit_previous_approach_failure",
            !newStart.Basis.Any(b => b.StartsWith("observed_action_failed:", StringComparison.Ordinal))
            && newStart.Score > sameRoute.Score && Memory(contextual).Attempts == 1);
        contextual.CompleteInteraction(newStart.Token!, false);
        var restoredStart = contextual.Decide(At(5, Place("context-return-to-original-start")), held);
        Record("returning_to_previous_public_start_retains_its_route_failure_history",
            restoredStart.Basis.Contains("observed_action_failed:2") && Memory(contextual).Attempts == 1);
        contextual.CompleteInteraction(restoredStart.Token!, true);
        var jittered = contextual.Decide(At(6, Place("context-same-cell-jitter") with { Location = new(1025, 17) }) with
            { Field = new(Space, new(17, 17)) }, held);
        Record("position_jitter_within_public_cells_does_not_reset_route_failure",
            jittered.Basis.Contains("observed_action_failed:2") && Memory(contextual).Attempts == 2);
        contextual.CompleteInteraction(jittered.Token!, false);
        var shiftedTarget = contextual.Decide(At(7, Place("context-visible-target-moved") with { Location = new(1088, 16) }), held);
        Record("newly_visible_target_cell_does_not_inherit_previous_approach_failure",
            !shiftedTarget.Basis.Any(b => b.StartsWith("observed_action_failed:", StringComparison.Ordinal))
            && shiftedTarget.Score > jittered.Score);
        Record("public_position_changes_do_not_reopen_actual_examination_history",
            Memory(contextual).Attempts == 2 && Memory(contextual).Successes == 2
            && !Inquiry(shiftedTarget.Alternatives.Single())
            && !shiftedTarget.Basis.Contains("authored_rule:prefer:untried:6"));
        contextual.CompleteInteraction(shiftedTarget.Token!, false);
        var restoredTarget = contextual.Decide(At(8, Place("context-original-target-returned")), held);
        Record("returning_to_previous_public_target_retains_its_route_failure_history",
            restoredTarget.Basis.Contains("observed_action_failed:3") && Memory(contextual).Attempts == 2);
        Observe("public-route-context-and-stable-examination", new
        {
            sameRoute, newStart, restoredStart, jittered, shiftedTarget, restoredTarget,
            examination = Memory(contextual), routes = Routes(contextual)
        });
        contextual.CompleteInteraction(restoredTarget.Token!, false);

        var isolated = Controller();
        var otherSeat = isolated.Decide(At(1, Place("another-controller")), held);
        Record("token_from_other_controller_cannot_complete_contact",
            !isolated.CompleteInteraction(contact.Token!, true)
            && isolated.CompleteInteraction(otherSeat.Token!, true) && Memory(isolated).Attempts == 1);

        foreach (var kind in new[] { "interact", "menu" })
        {
            var ordinary = Controller();
            var candidate = new PublicPlayerCandidate("ordinary-" + kind, "neutral-ordinary", kind, false);
            ordinary.Decide(At(1, candidate), held);
            var firstMemory = Memory(ordinary, kind, "neutral-ordinary");
            var repeated = ordinary.Decide(At(2, candidate with { Token = "ordinary-repeat-" + kind }), held);
            var firstAlternative = repeated.Alternatives.Single();
            Observe("ordinary-input " + kind, new { firstMemory, repeated,
                receiptAccepted = ordinary.CompleteInteraction(candidate.Token, true) });
            Record("ordinary_non_field_" + kind + "_keeps_selection_attempt_behavior",
                firstMemory.Attempts == 1 && Memory(ordinary, kind, "neutral-ordinary").Attempts == 2
                && !firstAlternative.Basis.Contains("authored_rule:prefer:untried:6"));
        }

        foreach (var result in results) Observe("behavior", new { result.Name, result.Holds });
        foreach (var result in results) check(result.Name, result.Holds);
    }
}
