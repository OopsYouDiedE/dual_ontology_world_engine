using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

// Only neutral, public inputs: purpose declares the scope, never a chapter label.
internal static class PublicMenuScopeCases
{
    private static PlayerCognition Cognition()
    {
        var runtime = new WorldRuntime();
        using var simulation = new SimulationSession(runtime);
        var world = new CoreScriptWorld(runtime, simulation);
        var actor = world.DefineCharacter("neutral.menu-scope.actor", "neutral-actor",
            world.DefineSpace("neutral.menu-scope.room", "neutral-room"));
        var snapshot = ((ContractResult<CognitionSnapshotV1>.Success)actor.Soul.World.CaptureSnapshot()).Value;
        return new(snapshot, actor.Self.SubjectId!.Value);
    }

    private static PlayerCognition UnrelatedMaterial(PlayerCognition cognition)
    {
        var material = new FrozenProjectionV1(new(cognition.Snapshot.SubjectWorld, Guid.NewGuid()),
            "neutral-unrelated-acquired-material", ProjectionKind.Concept, Truth.True, null,
            ImmutableDictionary<Tag, int>.Empty, [], [],
            [new(new FrozenConditionV1.Constant(Truth.True), RuleMode.Infer,
                new FrozenEffectV1.Think(new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(cognition.Actor),
                    new("neutral-unrelated-goal"), 1)), 1, Truth.True)], null,
            [new(new("acquired.neutral.menu-scope.unrelated"), "neutral-unrelated-acquired-material",
                "neutral-unrelated-acquired-material")], null);
        return cognition with { Snapshot = cognition.Snapshot with { Projections = cognition.Snapshot.Projections.Add(material) } };
    }

    private static SubjectivePlayerController Controller() => new(new()
    {
        Actor = "neutral.menu-scope.actor", Name = "neutral-actor", Mode = "agent",
        Rules = [new() { Mode = "prefer", Kind = "untried", Weight = 6 },
            new() { Mode = "prefer", Kind = "menu", Weight = 2 }],
        Reasoning = new() { Depth = 8, Expansions = 4096, Hypotheses = 128, ArgumentWeight = 16, InformationWeight = 8 }
    }, 23);
    private static PublicPlayerCandidate Interaction(string token, string name) =>
        new(token, name, "interact", false) { Location = new(100, 100) };
    private static PublicPlayerCandidate Menu(string token, string label, string purpose = "") =>
        new(token, label, "menu", false, Purpose: purpose);
    private static PublicPlayerObservation At(long turn, string space, params PublicPlayerCandidate[] candidates) =>
        new(turn, [], [], [.. candidates], false) { Field = new(space, new(100, 100)) };
    private static void Contact(SubjectivePlayerController controller, PublicPlayerObservation observation, PlayerCognition cognition)
    {
        var decision = controller.Decide(observation, cognition);
        if (!controller.CompleteInteraction(decision.Token!, true))
            throw new InvalidOperationException("Neutral person/object contact was not accepted");
    }
    private static PlayerDecision Offer(SubjectivePlayerController controller, PlayerCognition cognition,
        long turn, string token, string label, string purpose = "", string space = "neutral-scope-space-a") =>
        controller.Decide(At(turn, space, Menu(token, label, purpose), new(token + "-wait", "neutral-wait", "wait", false)), cognition);
    private static PlayerAlternative Alternative(PlayerDecision decision, string token) =>
        decision.Alternatives.Single(a => a.Token == token);
    private static bool Novel(PlayerAlternative alternative) =>
        alternative.Basis.Contains("authored_rule:prefer:untried:6");
    private static bool Repeated(PlayerAlternative alternative) => !Novel(alternative)
        && alternative.Basis.Any(b => b.StartsWith("revisit_without_new_accepted_material:", StringComparison.Ordinal));
    private static void Observe(string name, object value) =>
        Console.WriteLine("OBSERVATION " + name + " " + JsonSerializer.Serialize(value));

    public static void Run(Action<string, bool> check)
    {
        var results = new List<(string Name, bool Passed)>();
        void Record(string name, bool passed) => results.Add((name, passed));
        var held = Cognition();

        // A menu with no panel purpose is an actual person's topic, including a
        // label which could look like a panel name to a text heuristic.
        foreach (var label in new[] { "neutral-common-topic", "neutral-panel-looking", "opaque-731" })
        {
            var topics = Controller();
            Contact(topics, At(1, "neutral-scope-space-a", Interaction("topic-person-a", "neutral-person-a")), held);
            var firstA = Offer(topics, held, 2, "topic-a-first", label);
            var repeatA = Offer(topics, held, 3, "topic-a-repeat", label);
            Contact(topics, At(4, "neutral-scope-space-a", Interaction("topic-person-b", "neutral-person-b")), held);
            var firstB = Offer(topics, held, 5, "topic-b-first", label);
            var repeatB = Offer(topics, held, 6, "topic-b-repeat", label);
            Contact(topics, At(7, "neutral-scope-space-a", Interaction("topic-person-a-return", "neutral-person-a")), held);
            var returnA = Offer(topics, held, 8, "topic-a-return", label);
            Observe("person-topics " + label, new { firstA, repeatA, firstB, repeatB, returnA });
            Record("same_label_person_topics_are_independent_" + label,
                firstA.Token == "topic-a-first" && firstB.Token == "topic-b-first"
                && Novel(Alternative(firstA, "topic-a-first")) && Novel(Alternative(firstB, "topic-b-first"))
                && Repeated(Alternative(repeatA, "topic-a-repeat")) && Repeated(Alternative(repeatB, "topic-b-repeat"))
                && Repeated(Alternative(returnA, "topic-a-return")));

            var panels = Controller();
            Contact(panels, At(1, "neutral-scope-space-a", Interaction("panel-person-a", "neutral-person-a")), held);
            var first = Offer(panels, held, 2, "panel-first", label, "panel");
            var sameA = Offer(panels, held, 3, "panel-same-a", label, "panel");
            Contact(panels, At(4, "neutral-scope-space-a", Interaction("panel-person-b", "neutral-person-b")), held);
            var afterB = Offer(panels, held, 5, "panel-after-b", label, "panel");
            Contact(panels, At(6, "neutral-scope-space-a", Interaction("panel-person-c", "neutral-person-c")), held);
            var afterC = Offer(panels, held, 7, "panel-after-c", label, "panel");
            var unrelated = UnrelatedMaterial(held);
            Contact(panels, At(8, "neutral-scope-space-a", Interaction("panel-neutral-object", "neutral-unrelated-object")), unrelated);
            var afterObject = Offer(panels, unrelated, 9, "panel-after-object", label, "panel");
            Observe("public-panel " + label, new { first, sameA, afterB, afterC, afterObject });
            Record("panel_first_use_is_novel_" + label,
                first.Token == "panel-first" && Novel(Alternative(first, "panel-first")));
            Record("panel_same_context_retains_repeat_cost_" + label, Repeated(Alternative(sameA, "panel-same-a")));
            Record("panel_switch_to_b_retains_repeat_cost_" + label,
                Repeated(Alternative(afterB, "panel-after-b"))
                && Alternative(afterB, "panel-after-b").Score <= Alternative(sameA, "panel-same-a").Score);
            Record("panel_third_person_does_not_refresh_history_" + label, Repeated(Alternative(afterC, "panel-after-c")));
            Record("panel_unrelated_object_and_material_do_not_refresh_history_" + label,
                Repeated(Alternative(afterObject, "panel-after-object")));
        }

        var spatial = Controller();
        var spaceAFirst = Offer(spatial, held, 1, "space-a-first", "neutral-shared-item", "panel");
        var spaceARepeat = Offer(spatial, held, 2, "space-a-repeat", "neutral-shared-item", "panel");
        var spaceBFirst = Offer(spatial, held, 3, "space-b-first", "neutral-shared-item", "panel", "neutral-scope-space-b");
        var spaceAReturn = Offer(spatial, held, 4, "space-a-return", "neutral-shared-item", "panel");
        Observe("public-panel-space-isolation", new { spaceAFirst, spaceARepeat, spaceBFirst, spaceAReturn });
        Record("panel_history_is_independent_across_spaces_and_retained_on_return",
            Novel(Alternative(spaceAFirst, "space-a-first")) && Repeated(Alternative(spaceARepeat, "space-a-repeat"))
            && Novel(Alternative(spaceBFirst, "space-b-first")) && Repeated(Alternative(spaceAReturn, "space-a-return")));

        foreach (var firstPurpose in new[] { "", "panel" })
        {
            var purpose = Controller();
            Contact(purpose, At(1, "neutral-scope-space-a", Interaction("purpose-person", "neutral-person-a")), held);
            var first = Offer(purpose, held, 2, "purpose-first", "neutral-identical-label", firstPurpose);
            var secondPurpose = firstPurpose == "" ? "panel" : "";
            var second = Offer(purpose, held, 3, "purpose-second", "neutral-identical-label", secondPurpose);
            var firstAgain = Offer(purpose, held, 4, "purpose-first-again", "neutral-identical-label", firstPurpose);
            Observe("same-label-distinct-purpose " + firstPurpose, new { firstPurpose, secondPurpose, first, second, firstAgain });
            Record("panel_and_person_topic_with_identical_labels_have_separate_history_" + (firstPurpose == "" ? "topic-first" : "panel-first"),
                Novel(Alternative(first, "purpose-first")) && Novel(Alternative(second, "purpose-second"))
                && Repeated(Alternative(firstAgain, "purpose-first-again")));
        }

        // Preserve every neutral trace before the usual first-failure assertion.
        foreach (var result in results) Observe("assertion", new { result.Name, result.Passed });
        foreach (var result in results) check(result.Name, result.Passed);
    }
}
