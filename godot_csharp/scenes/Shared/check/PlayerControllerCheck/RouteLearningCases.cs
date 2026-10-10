using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

internal static class RouteLearningCases
{
    private static PublicPlayerCandidate Step(string token, double x, double y) =>
        new(token, $"{x},{y}", "move", false) { Location = new(x, y) };
    private static PublicPlayerCandidate Approach(string token, string destination, double x, double y, double travelDistance = 0) =>
        new(token, $"{destination} {x},{y}", "move", false, Value: destination, Purpose: "approach")
        { Location = new(x, y), TravelDistance = travelDistance };
    private static PublicPlayerCandidate Place(string token, string name = "neutral-route-source") =>
        new(token, name, "interact", false) { Location = new(1024, 16) };
    private static PublicPlayerObservation At(long turn, double x, double y, params PublicPlayerCandidate[] candidates) =>
        new(turn, [], [], [.. candidates], false) { Field = new("opaque-route-learning", new(x, y)) };
    private static ActorControllerDefinition Declaration(string role, int curiosity)
    {
        var definition = new ActorControllerDefinition
        {
            Actor = "world:character." + role, Name = role, Mode = "agent",
            Rules = [new() { Mode = "prefer", Kind = "untried", Weight = curiosity },
                new() { Mode = "prefer", Kind = "interact", Weight = 3 },
                new() { Mode = "prefer", Kind = "menu", Weight = 100, LabelContains = "neutral-prime" }],
            Reasoning = new() { Depth = 8, Expansions = 4096, Hypotheses = 128, ArgumentWeight = 16, InformationWeight = 8 }
        };
        return definition;
    }
    private static PlayerCognition Cognition(string role, bool answerReady = false)
    {
        var runtime = new WorldRuntime();
        using var simulation = new SimulationSession(runtime);
        var world = new CoreScriptWorld(runtime, simulation);
        var room = world.DefineSpace("neutral.route-learning.room", "neutral-room");
        var actor = world.DefineCharacter("neutral.route-learning." + role, role, room);
        var snapshot = ((ContractResult<CognitionSnapshotV1>.Success)actor.Soul.World.CaptureSnapshot()).Value;
        var actorId = actor.Self.SubjectId!.Value;
        var goal = new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(actorId), new("neutral-route-goal"), 1);
        FrozenProjectionV1 Material(string name, Tag? tag, Truth accepted) => new(new(snapshot.SubjectWorld, Guid.NewGuid()),
            name, ProjectionKind.Concept, Truth.True, null,
            tag is null ? ImmutableDictionary<Tag, int>.Empty : ImmutableDictionary<Tag, int>.Empty.Add(tag.Value, 1), [], [],
            [new(new FrozenConditionV1.Constant(Truth.True), RuleMode.Infer, new FrozenEffectV1.Think(goal), 1, accepted)],
            null, [new(new("acquired.neutral." + name), name, name)], null);
        var materials = ImmutableArray.Create(Material("neutral-route-question", AcquiredPropositions.Question, Truth.Unknown),
            Material("neutral-route-source", AcquiredPropositions.Opportunity, Truth.Unknown));
        if (answerReady) materials = materials.Add(Material("neutral-route-proof", null, Truth.True));
        return new(snapshot with { Projections = snapshot.Projections.AddRange(materials) }, actorId);
    }
    private static PlayerCognition NewAcceptedMaterial(PlayerCognition cognition, bool answersQuestion = false)
    {
        var material = new FrozenProjectionV1(new(cognition.Snapshot.SubjectWorld, Guid.NewGuid()), "neutral-new-material",
            ProjectionKind.Concept, Truth.True, null, ImmutableDictionary<Tag, int>.Empty, [], [],
            [new(new FrozenConditionV1.Constant(Truth.True), RuleMode.Infer,
                new FrozenEffectV1.Think(new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(cognition.Actor),
                    new(answersQuestion ? "neutral-route-goal" : "neutral-learning"), 1)),
                1, Truth.True)], null, [new(new("acquired.neutral.new-material"), "neutral-new-material", "neutral-new-material")], null);
        return cognition with { Snapshot = cognition.Snapshot with { Projections = cognition.Snapshot.Projections.Add(material) } };
    }
    private static void Prime(SubjectivePlayerController controller, PlayerCognition cognition) =>
        controller.Decide(At(1, 16, 16, Place("neutral-seen-source"),
            new("neutral-prime", "neutral-prime", "menu", false)), cognition);
    private static bool DirectInvestigation(PlayerDecision decision) =>
        decision.Basis.Any(b => b.StartsWith("seek_current_question_material:") || b.StartsWith("seek_unknown_acquired_proposition:"));
    private static void Contact(SubjectivePlayerController controller, PublicPlayerObservation observation, PlayerCognition cognition)
    {
        var decision = controller.Decide(observation, cognition);
        if (!controller.CompleteInteraction(decision.Token!, true))
            throw new InvalidOperationException("Neutral examination contact was not accepted");
    }
    private static void Observe(string name, object value) => Console.WriteLine("OBSERVATION " + name + " " + JsonSerializer.Serialize(value));

    public static void Run(Action<string, bool> check)
    {
        foreach (var (role, curiosity) in new[] { ("neutral-route-curious", 6), ("neutral-route-feedback", 0) })
        {
            var cognition = Cognition(role);
            var declaration = Declaration(role, curiosity);
            foreach (var ready in new[] { false, true })
            {
                var arrivedCognition = ready ? Cognition(role, answerReady: true) : cognition;
                var arrived = new SubjectivePlayerController(declaration, 23);
                Prime(arrived, arrivedCognition);
                var selected = arrived.Decide(At(2, 1024, 16,
                    Approach("neutral-route-at-arrival", "neutral-route-source", 64, 144), Place("neutral-arrived-interaction")), arrivedCognition);
                Observe(role + " arrived " + (ready ? "answer" : "inquiry"), selected);
                check(role + "_arrived_" + (ready ? "answer" : "inquiry") + "_uses_interaction_before_waypoint", selected.Token == "neutral-arrived-interaction");
            }

            var route = new SubjectivePlayerController(declaration, 23);
            Prime(route, cognition);
            var first = route.Decide(At(2, 16, 16,
                Approach("neutral-route-first", "neutral-route-source", 16, 144), Step("neutral-straight-first", 48, 16)), cognition);
            var following = route.Decide(At(3, 16, 144,
                Approach("neutral-route-next", "neutral-route-source", 144, 144), Step("neutral-straight-next", 48, 144)), cognition);
            Observe(role + " changing-waypoint", new { first, following });
            check(role + "_known_route_survives_initial_retreat_and_new_waypoint", first.Token == "neutral-route-first"
                && following.Token == "neutral-route-next" && first.Basis.Contains("follow_observed_route_to_needed_place")
                && following.Basis.Contains("follow_observed_route_to_needed_place"));
            check(role + "_waypoints_do_not_receive_direct_investigation_rewards", !DirectInvestigation(first) && !DirectInvestigation(following));

            var costedRoute = new SubjectivePlayerController(declaration, 23);
            Prime(costedRoute, cognition);
            var costedFirst = costedRoute.Decide(At(2, 16, 16,
                Approach("neutral-cost-first", "neutral-route-source", 16, 144, 128),
                Step("neutral-cost-unrelated-first", 48, 16)), cognition);
            var costedNext = costedRoute.Decide(At(3, 16, 144,
                Approach("neutral-cost-next", "neutral-route-source", 144, 144, 128),
                Step("neutral-cost-unrelated-next", 48, 144)), cognition);
            Observe(role + " actual-waypoint-cost", new { costedFirst, costedNext });
            check(role + "_paying_each_actual_waypoint_keeps_needed_route_ahead_of_unrelated_movement",
                costedFirst.Token == "neutral-cost-first" && costedNext.Token == "neutral-cost-next"
                && costedFirst.Basis.Contains("observed_route_travel_distance:128.0")
                && costedNext.Basis.Contains("observed_route_travel_distance:128.0")
                && !DirectInvestigation(costedFirst) && !DirectInvestigation(costedNext));
            if (curiosity > 0)
            {
                // Same cognition, candidates, input endpoint and seed; only deliberately
                // corrupted public host cost repeats the full journey for this short input.
                var overcharged = new SubjectivePlayerController(declaration, 23);
                Prime(overcharged, cognition);
                var corruptedCost = overcharged.Decide(At(2, 16, 16,
                    Approach("neutral-cost-first", "neutral-route-source", 16, 144, 1024),
                    Step("neutral-cost-unrelated-first", 48, 16)), cognition);
                Observe(role + " counterfactual-full-route-overcharge", corruptedCost);
                check(role + "_counterfactual_full_journey_cost_exposes_unrelated_move_bias",
                    corruptedCost.Token == "neutral-cost-unrelated-first"
                    && corruptedCost.Alternatives.Single(a => a.Token == "neutral-cost-first").Score
                        < costedFirst.Alternatives.Single(a => a.Token == "neutral-cost-first").Score);
            }

            var rechecking = new SubjectivePlayerController(declaration, 23);
            Prime(rechecking, cognition);
            Contact(rechecking, At(2, 1024, 16, Place("neutral-recheck-first-examination")), cognition);
            var firstRetry = rechecking.Decide(At(3, 16, 16,
                Approach("neutral-recheck-first-waypoint", "neutral-route-source", 144, 144)), cognition);
            var nextRetry = rechecking.Decide(At(4, 144, 144,
                Approach("neutral-recheck-next-waypoint", "neutral-route-source", 272, 144)), cognition);
            Observe(role + " one-examination-new-waypoints", new { firstRetry, nextRetry });
            check(role + "_changing_waypoint_does_not_refresh_target_novelty", firstRetry.Score == nextRetry.Score
                && (curiosity == 0 || firstRetry.Basis.Contains("remembered_goal_already_examined:1")
                    && nextRetry.Basis.Contains("remembered_goal_already_examined:1"))
                && !firstRetry.Basis.Any(b => b.StartsWith("authored_rule:prefer:untried:"))
                && !nextRetry.Basis.Any(b => b.StartsWith("authored_rule:prefer:untried:")));

            var exhausted = new SubjectivePlayerController(declaration, 23);
            Prime(exhausted, cognition);
            Contact(exhausted, At(2, 1024, 16, Place("neutral-examine-first")), cognition);
            Contact(exhausted, At(3, 1024, 16, Place("neutral-examine-second")), cognition);
            var alternative = exhausted.Decide(At(4, 16, 16,
                Approach("neutral-exhausted-new-waypoint", "neutral-route-source", 144, 144),
                new("neutral-unexamined-interaction", "neutral-unexamined", "interact", false)), cognition);
            var exhaustedRoute = exhausted.Decide(At(5, 16, 16,
                Approach("neutral-exhausted-another-waypoint", "neutral-route-source", 272, 144)), cognition);
            Observe(role + " exhausted-source", new { alternative, exhaustedRoute });
            check(role + "_exhausted_source_does_not_beat_unexamined_interaction", alternative.Token == "neutral-unexamined-interaction");
            check(role + "_new_waypoint_does_not_reopen_exhausted_source", !DirectInvestigation(exhaustedRoute)
                && !exhaustedRoute.Basis.Contains("follow_observed_route_to_needed_place"));
            var learned = NewAcceptedMaterial(cognition);
            var reopened = exhausted.Decide(At(6, 1024, 16, Place("neutral-reopened-interaction")), learned);
            Observe(role + " unrelated-material-preserves-source-history", reopened);
            check(role + "_unrelated_accepted_material_keeps_source_exhausted", !DirectInvestigation(reopened)
                && !reopened.Basis.Any(b => b.StartsWith("authored_rule:prefer:untried:")));
            check(role + "_unchanged_material_fixture_completes_actual_reexamination", exhausted.CompleteInteraction(reopened.Token!, true));
            var relevantLearning = NewAcceptedMaterial(cognition, answersQuestion: true);
            var returnAfterProof = exhausted.Decide(At(7, 16, 16,
                Approach("neutral-return-after-proof", "neutral-route-source", 16, 144),
                new("neutral-wait-after-proof", "neutral-wait", "wait", false)), relevantLearning);
            var answerAfterReturn = exhausted.Decide(At(8, 1024, 16,
                Approach("neutral-waypoint-after-return", "neutral-route-source", 64, 144), Place("neutral-answer-after-return")), relevantLearning);
            Observe(role + " relevant-learning-return", new { returnAfterProof, answerAfterReturn });
            check(role + "_relevant_accepted_proof_reopens_return_and_answer", returnAfterProof.Token == "neutral-return-after-proof"
                && returnAfterProof.Basis.Contains("follow_observed_route_to_needed_place")
                && answerAfterReturn.Token == "neutral-answer-after-return"
                && answerAfterReturn.Basis.Any(b => b.StartsWith("return_with_personal_answer:")));

            foreach (var reversed in new[] { false, true })
            {
                var moving = new SubjectivePlayerController(declaration, 23);
                Prime(moving, cognition);
                var initial = moving.Decide(At(2, 16, 16, Step("neutral-forward-initial", 512, 16), Step("neutral-back-initial", 128, 16)), cognition);
                var forward = Step("neutral-forward", 640, 16); var backward = Step("neutral-backward", 128, 16);
                var next = moving.Decide(At(3, 512, 16, reversed ? forward : backward, reversed ? backward : forward), cognition);
                var final = moving.Decide(At(4, 640, 16, Step("neutral-back-final", 512, 16), Step("neutral-forward-final", 768, 16)), cognition);
                Observe(role + " directional-progress " + reversed, new { initial, next, final });
                check(role + "_measured_progress_does_not_reverse_to_old_near_step_" + reversed,
                    initial.Token == "neutral-forward-initial" && next.Token == "neutral-forward" && final.Token == "neutral-forward-final");
            }

            var movementPreference = Declaration(role, curiosity);
            movementPreference.Rules.Add(new() { Mode = "prefer", Kind = "move", Weight = 90 });
            var differentlyAuthored = new SubjectivePlayerController(movementPreference, 23);
            Prime(differentlyAuthored, cognition);
            var roleChoice = differentlyAuthored.Decide(At(2, 1024, 16,
                Place("neutral-authored-interaction"), Approach("neutral-authored-move", "neutral-unrelated-destination", 64, 144)), cognition);
            Observe(role + " declared-movement-preference", roleChoice);
            check(role + "_declared_role_preference_can_choose_movement", roleChoice.Token == "neutral-authored-move"
                && roleChoice.Basis.Contains("authored_rule:prefer:move:90"));
        }
    }
}
