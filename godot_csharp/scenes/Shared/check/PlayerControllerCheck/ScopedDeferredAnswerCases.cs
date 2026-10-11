using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
using static DualOntologyWorldEngine.Story.ChapterEvents;

// Independent neutral public-input regression. No chapter content or answer table is loaded.
internal static class ScopedDeferredAnswerCases
{
    private const string Entrance = "neutral-answer-entrance";
    private const string OtherEntrance = "neutral-other-entrance";
    private const string Space = "neutral-answer-space";
    private const string Statement = "neutral-statement";
    private const string Material1 = "neutral-material-one";
    private const string Material2 = "neutral-material-two";

    private static FrozenConditionV1 Premise(PlayerCognition cognition, string suffix) =>
        new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(cognition.Actor), new("neutral-required-" + suffix), 1);
    private static FrozenConditionV1 Goal(PlayerCognition cognition) =>
        new FrozenConditionV1.All([Premise(cognition, "one"), Premise(cognition, "two")]);
    private static PlayerCognition Add(PlayerCognition held, string name, ProjectionKind kind,
        FrozenConditionV1 claim, Truth acceptance, params Tag[] markers)
    {
        var material = new FrozenProjectionV1(new(held.Snapshot.SubjectWorld, Guid.NewGuid()), name,
            kind, Truth.True, null, markers.ToImmutableDictionary(t => t, _ => 1), [], [],
            [new(new FrozenConditionV1.Constant(Truth.True), RuleMode.Infer,
                new FrozenEffectV1.Think(claim), 1, acceptance)], null,
            [new(new("acquired.neutral." + name), name, name)], null);
        return held with { Snapshot = held.Snapshot with { Projections = held.Snapshot.Projections.Add(material) } };
    }
    private static PlayerCognition Initial()
    {
        var runtime = new WorldRuntime();
        using var simulation = new SimulationSession(runtime);
        var world = new CoreScriptWorld(runtime, simulation);
        var actor = world.DefineCharacter("neutral.answer.actor", "neutral-actor",
            world.DefineSpace("neutral.answer.room", "neutral-room"));
        var snapshot = ((ContractResult<CognitionSnapshotV1>.Success)actor.Soul.World.CaptureSnapshot()).Value;
        var cognition = new PlayerCognition(snapshot, actor.Self.SubjectId!.Value);
        cognition = Add(cognition, "neutral-question", ProjectionKind.Concept, Goal(cognition), Truth.Unknown, AcquiredPropositions.Question);
        cognition = Add(cognition, Statement, ProjectionKind.Concept, Goal(cognition), Truth.Unknown);
        cognition = Add(cognition, Entrance, ProjectionKind.Concept, Goal(cognition), Truth.Unknown, AcquiredPropositions.Opportunity);
        cognition = Add(cognition, OtherEntrance, ProjectionKind.Concept, Goal(cognition), Truth.Unknown, AcquiredPropositions.Opportunity);
        cognition = Add(cognition, Material1, ProjectionKind.Object, Premise(cognition, "one"), Truth.True, AcquiredPropositions.Presentable);
        return Add(cognition, Material2, ProjectionKind.Object, Premise(cognition, "two"), Truth.Unknown, AcquiredPropositions.Presentable);
    }
    private static PlayerCognition Accepted(PlayerCognition held, bool presentable = true) => held with
    {
        Snapshot = held.Snapshot with { Projections = [.. held.Snapshot.Projections.Select(p => p with
        {
            Rules = p.Name == Material2 ? [.. p.Rules.Select(r => r with { Acceptance = Truth.True })] : p.Rules,
            Tags = !presentable ? p.Tags.Remove(AcquiredPropositions.Presentable) : p.Tags
        })] }
    };
    private static PlayerCognition NoQuestion(PlayerCognition held) => held with
    { Snapshot = held.Snapshot with { Projections = [.. held.Snapshot.Projections.Select(p => p with { Tags = p.Tags.Remove(AcquiredPropositions.Question) })] } };
    private static SubjectivePlayerController Controller(bool preferEntrance = false, int expansions = 4096) => new(new()
    {
        Actor = "neutral.answer.actor", Name = "neutral-actor", Mode = "agent",
        Rules = [new() { Mode = "prefer", Kind = "untried", Weight = 6 },
            new() { Mode = "prefer", Kind = "menu", Weight = 1, LabelContains = "neutral-answer" },
            .. preferEntrance ? new ControllerRuleDefinition[] { new() { Mode = "prefer", Kind = "interact", Weight = 200, LabelContains = Entrance } } : []],
        Reasoning = new() { Depth = 8, Expansions = expansions, Hypotheses = 128, ArgumentWeight = 16, InformationWeight = 8 }
    }, 23);
    private static PublicPlayerCandidate Place(string token, string label = Entrance) =>
        new(token, label, "interact", false) { Location = new(100, 100) };
    private static PublicPlayerObservation At(long turn, string space, params PublicPlayerCandidate[] candidates) =>
        new(turn, [], [], [.. candidates], false) { Field = new(space, new(100, 100)) };
    private static void Contact(SubjectivePlayerController controller, PlayerCognition cognition, string label = Entrance,
        string space = Space, bool reached = true)
    {
        var choice = controller.Decide(At(1, space, Place("neutral-contact-" + label, label)), cognition);
        if (choice.Token is null || !controller.CompleteInteraction(choice.Token, reached))
            throw new InvalidOperationException("Neutral contact did not accept its own public token");
    }
    private static Dictionary<string, object?> Field(string id, string purpose, string name, bool selected) => new()
    {
        ["id"] = id, ["kind"] = "choice", ["heading"] = "neutral-field-" + id,
        ["purpose"] = purpose, ["value"] = selected ? id + "-value" : "",
        ["options"] = new object[] { Option(id + "-value", name) }
    };
    private static Dictionary<string, object?> Form(bool selected = false, bool ordinary = false, bool evidenceOnly = false)
    {
        var fields = new List<object>();
        if (!evidenceOnly) fields.Add(Field("statement", ordinary ? "" : "statement", ordinary ? "neutral-ordinary-value" : Statement, selected));
        fields.Add(Field("evidence", ordinary ? "" : "evidence", Material1, selected));
        return Event("form", ("presentation_id", "neutral-private-form"), ("layout", "tabs"),
            ("header", Event("header", ("question", "neutral-visible-question"))), ("fields", fields.ToArray()),
            ("actions", new object[]
            {
                new Dictionary<string, object?> { ["id"] = "cancel", ["label"] = "neutral-cancel" },
                new Dictionary<string, object?> { ["id"] = "submit", ["label"] = "neutral-submit",
                    ["requires"] = evidenceOnly ? new object[] { "evidence" } : new object[] { "statement", "evidence" } }
            }));
    }
    private static (PlayerDecision Decision, PublicPlayerObservation After, IReadOnlyDictionary<string, object?>? Input) FormDecision(
        SubjectivePlayerController controller, PlayerCognition cognition, Dictionary<string, object?> form, string space = Space)
    {
        var port = new PlayerCandidateSession(_ => [Event("close")], "neutral-actor");
        port.Locate(new(space, new(100, 100)));
        var observation = port.Observe([form]);
        var decision = controller.Decide(observation, cognition);
        var after = decision.Token is null ? observation : port.Submit(new(decision.Token));
        return (decision, after, port.LastInput);
    }
    private static PlayerDecision EntranceDecision(SubjectivePlayerController controller, PlayerCognition cognition,
        string token, string label = Entrance, string space = Space) => controller.Decide(At(10, space,
            Place(token, label), new("neutral-wait-" + token, "neutral-wait", "wait", false)), cognition);
    private static PlayerAlternative EntranceAlternative(PlayerDecision decision, string token) =>
        decision.Alternatives.Single(a => a.Token == token);
    private static bool Inquiry(PlayerAlternative alternative) => alternative.Basis.Any(b =>
        b.StartsWith("seek_current_question_material:", StringComparison.Ordinal)
        || b.StartsWith("seek_unknown_acquired_proposition:", StringComparison.Ordinal)
        || b.StartsWith("expected_information_at_remembered_goal:", StringComparison.Ordinal)
        || b.StartsWith("examine_unexamined_for_open_question", StringComparison.Ordinal));
    private static bool Return(PlayerAlternative alternative) => alternative.Basis.Any(b =>
        b.StartsWith("return_with_personal_answer:", StringComparison.Ordinal));
    private static bool Waiting(PlayerAlternative alternative) => alternative.Basis.Contains("observed_answer_still_missing_support");
    private static void Observe(string name, object value) =>
        Console.WriteLine("OBSERVATION deferred-answer " + name + " " + JsonSerializer.Serialize(value));

    public static void Run(Action<string, bool> check)
    {
        var held = Initial(); var complete = Accepted(held);
        PublicPlayerCandidate Answer(string token, string label = "neutral-answer") => new(token, label, "menu", false, Purpose: "argument");
        PlayerAlternative Assess(SubjectivePlayerController c, PlayerCognition cognition, string label = "neutral-answer") {
            var d = c.Decide(At(20, Space, Answer("answer",label), new("exit","exit","menu",false,Purpose:"close")),cognition);
            Observe("scoped-"+label,d);
            return d.Alternatives.Single(a=>a.Token=="answer");
        }
        SubjectivePlayerController Deferred() {
            var c=Controller(); Contact(c,held);
            check("scoped_select_actual_argument",c.Decide(At(2,Space,Answer("enter"),new PublicPlayerCandidate("inspect-visible","neutral-inspect","menu",false,Purpose:"inspect")),held).Token=="enter");
            var cancelled=FormDecision(c,held,Form());
            check("scoped_cancel_unsupported_without_random_evidence",cancelled.Input is not null && Text(cancelled.Input,"action")=="cancel" && !Map(cancelled.Input,"values").Any());
            check("scoped_hypothesis_is_not_belief",cancelled.Decision.HypothesesCompared>0 && cancelled.Decision.Arguments.All(a=>a.Value!="True"));
            return c;
        }
        var c=Deferred();
        var pending=Assess(c,held);
        check("scoped_same_argument_waits_without_return_reward",Waiting(pending)&&!Return(pending));
        check("scoped_different_argument_not_suppressed",!Waiting(Assess(c,held,"neutral-other-answer")));
        var entrance=EntranceAlternative(EntranceDecision(c,held,"physical"),"physical");
        check("scoped_physical_entrance_keeps_inquiry",!Waiting(entrance)&&Inquiry(entrance));
        c.CompleteInteraction("physical",true);
        foreach(var purpose in new[]{"inspect","experiment"}) {
            var d=c.Decide(At(21,Space,new PublicPlayerCandidate(purpose,purpose,"menu",false,Purpose:purpose)),held);
            check("scoped_same_entrance_"+purpose+"_ordinary",!Waiting(d.Alternatives.Single()));
        }
        c=Deferred();
        var unrelated=Add(held,"neutral-unrelated-proof",ProjectionKind.Object,Premise(held,"other"),Truth.True,AcquiredPropositions.Presentable);
        check("scoped_unrelated_proof_keeps_waiting",Waiting(Assess(c,unrelated)));
        check("scoped_memory_only_keeps_waiting",Waiting(Assess(c,Accepted(held,false))));
        var denied=Add(held,"neutral-denial",ProjectionKind.Object,new FrozenConditionV1.Not(Premise(held,"two")),Truth.True,AcquiredPropositions.Presentable);
        check("scoped_denied_support_keeps_waiting",Waiting(Assess(c,denied)));
        var conflicted=Add(complete,"neutral-conflict",ProjectionKind.Object,new FrozenConditionV1.Not(Premise(held,"two")),Truth.True,AcquiredPropositions.Presentable);
        check("scoped_conflicted_support_keeps_waiting",Waiting(Assess(c,conflicted)));
        var recovered=Assess(c,complete);
        check("scoped_complete_presentable_support_restores_return",!Waiting(recovered)&&Return(recovered));
        var other=Deferred(); Contact(other,held,OtherEntrance);
        check("scoped_different_source_not_suppressed",!Waiting(Assess(other,held)));
        var fresh=Controller(); Contact(fresh,held);
        check("scoped_other_controller_has_no_deferred_memory",!Waiting(Assess(fresh,held)));
        var direct=Controller(); Contact(direct,held); FormDecision(direct,held,Form());
        check("scoped_no_argument_selection_does_not_invent_entry",!Waiting(Assess(direct,held)));
    }
}