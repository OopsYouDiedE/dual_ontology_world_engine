using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

// Neutral propositions and visible inputs, independent of chapter content.
internal static class RepetitionResultCases
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
        var runtime = new CompiledDialogueRuntime(new() { Entries = new() { ["neutral-entry"] = [new() { Op = "choices", Options = [
            new() { Id = "exit", Label = "neutral-exit", Body = [new() { Op = "jump", Target = "END" }] },
            new() { Id = "call-exit", Label = "neutral-call-exit", Body = [new() { Op = "call", Name = "neutral-effect" }, new() { Op = "jump", Target = "END" }] },
            new() { Id = "line-exit", Label = "neutral-line-exit", Body = [new() { Op = "line", Text = "neutral-text" }, new() { Op = "jump", Target = "END" }] },
            new() { Id = "jump", Label = "neutral-jump", Body = [new() { Op = "jump", Target = "neutral-entry" }] },
            new() { Id = "empty", Label = "neutral-empty", Body = [] }
        ] }] } }, new() { EvaluateCall = (_,_) => null, ExecuteCall = (_,_) => {}, Interpolate = s => s });
        runtime.Start("neutral-entry");
        var choices = runtime.Next().Choices;
        check("r16_only_side_effect_free_end_jump_is_close", choices.Single(c=>c.Id=="exit").Purpose=="close"
            && choices.Where(c=>c.Id!="exit").All(c=>c.Purpose!="close"));
        var port = new PlayerCandidateSession(_=>[],"neutral-actor");
        var projected=port.Observe([ChapterEvents.Event("candidates",("options",choices.Select(c=>ChapterEvents.Option(c.Id,c.Label,c.IsAvailable,c.LockHint,purpose:c.Purpose)).ToArray()))]);
        check("r16_close_purpose_survives_public_candidate_projection",projected.Candidates.Single(c=>c.Label=="neutral-exit").Purpose=="close");
        var held = Initial();
        held = Add(Add(held, "neutral-method-a", "goal-a", Truth.Unknown, AcquiredPropositions.Opportunity),
            "neutral-method-b", "goal-b", Truth.Unknown, AcquiredPropositions.Opportunity);
        PublicPlayerCandidate Method(string token, string suffix) => new(token, "neutral-method-" + suffix, "menu", false);
        PlayerAlternative Alternative(PlayerDecision d, string token) => d.Alternatives.Single(a => a.Token == token);
        bool Inquiry(PlayerAlternative a) => a.Basis.Any(b => b.StartsWith("seek_") || b.StartsWith("examine_unexamined"));
        void Pick(SubjectivePlayerController c, string suffix, PlayerCognition knowledge) => c.Decide(At(2, Method("pick-"+suffix,suffix)),knowledge);
        var own=Controller(); Contact(own,At(1,Person("contact","neutral-person-a")),held);
        Pick(own,"a",held);
        var learned=Add(held,"neutral-result","goal-a",Truth.True);
        own.Decide(At(3,new PublicPlayerCandidate("read","neutral-read","continue",false)),learned);
        var after=own.Decide(At(4,Method("repeat","a"),Close("exit")),learned);
        Observe("own-result-baseline",after);
        check("r16_own_result_does_not_reopen_method",!Novel(Alternative(after,"repeat")) && after.Token=="exit");
        check("r16_own_result_does_not_repeat_information_bonus",!Inquiry(Alternative(after,"repeat")));

        var external=Controller(); Contact(external,At(1,Person("contact","neutral-person-a")),held);
        Pick(external,"a",held); Pick(external,"b",held);
        external.Decide(At(3,Close("exit")),held);
        var changed=external.Decide(At(4,Method("a","a"),Method("b","b"),Close("exit2")),learned);
        Observe("external-method-specific-proof",changed);
        check("r16_external_proof_reopens_corresponding_method",Novel(Alternative(changed,"a")));
        check("r16_external_proof_does_not_reopen_other_method",!Novel(Alternative(changed,"b")));

        var unfinished=Controller(); Contact(unfinished,At(1,Person("contact","neutral-person-a")),held);
        unfinished.Decide(At(2,Method("a","a"),Method("b","b")),held);
        unfinished.Decide(At(3,Close("exit")),held);
        var back=unfinished.Decide(At(4,Person("back","neutral-person-a"),new PublicPlayerCandidate("wait","neutral-wait","wait",false)),held);
        Observe("return-for-untried-method",back);
        check("r16_seen_untried_method_keeps_return_information",Inquiry(Alternative(back,"back")));

        var exhausted=Controller(); Contact(exhausted,At(1,Person("contact","neutral-person-a")),held);
        Pick(exhausted,"a",held); Pick(exhausted,"b",held);
        exhausted.Decide(At(3,Close("exit")),held);
        var noReturn=exhausted.Decide(At(4,Person("back","neutral-person-a"),new PublicPlayerCandidate("wait","neutral-wait","wait",false)),held);
        check("r16_exhausted_source_has_no_arbitrary_second_information_bonus",!Inquiry(Alternative(noReturn,"back")));

        var noQuestion = held with { Snapshot = held.Snapshot with {
            Projections = [.. held.Snapshot.Projections.Where(p => p.Tags.GetValueOrDefault(AcquiredPropositions.Question) != 1)] } };
        foreach (var purpose in new[] { "inspect", "argument" })
        {
            var explorer = Controller();
            Contact(explorer, At(1, Person("first-contact", "neutral-person-a")), noQuestion);
            // The preferred topic is selected while another real method remains visible.
            var visible = explorer.Decide(At(2, Topic("topic"),
                new("untried", "neutral-untried", "menu", false, Purpose: purpose)), noQuestion);
            check("r17_no_question_fixture_leaves_visible_method_" + purpose, visible.Token == "topic");
            explorer.Decide(At(3, Close("leave")), noQuestion);
            var revisit = explorer.Decide(At(4, Person("return", "neutral-person-a"),
                new("wait", "neutral-wait", "wait", false)), noQuestion);
            Observe("no-question-return-" + purpose, revisit);
            check("r17_no_question_returns_for_observed_untried_method_" + purpose,
                revisit.Token == "return" && revisit.Basis.Contains("return_for_untried_observed_method"));
        }

        var frequentlyVisited = Controller();
        for (var visit = 1; visit <= 8; visit++)
            Contact(frequentlyVisited, At(visit, Person("visit-" + visit, "neutral-person-a")), noQuestion);
        frequentlyVisited.Decide(At(9, Topic("chosen-topic"),
            new("remaining-inspect", "neutral-untried", "menu", false, Purpose: "inspect")), noQuestion);
        frequentlyVisited.Decide(At(10, Close("leave-source")), noQuestion);
        var returnRoute = frequentlyVisited.Decide(At(11,
            new PublicPlayerCandidate("needed-route", "neutral-return-route", "move", false,
                Value: "neutral-person-a", Purpose: "approach") { Location = new(0, 228), TravelDistance = 128 },
            new PublicPlayerCandidate("unrelated-step", "neutral-step", "move", false) { Location = new(32, 100) })
            with { Field = new("neutral-context-field", new(0, 100)) }, noQuestion);
        Observe("many-parent-visits-with-untried-method", returnRoute);
        check("r17_parent_visits_do_not_overcharge_route_to_untried_method", returnRoute.Token == "needed-route"
            && returnRoute.Basis.Contains("return_for_untried_observed_method")
            && !returnRoute.Basis.Any(b => b.StartsWith("remembered_goal_already_examined:")));

        var ordinary = Controller().Decide(At(1, Method("ordinary", "a"), Close("close")), learned);
        check("r17_ordinary_menu_gets_no_answer_reward", !Alternative(ordinary,"ordinary").Basis
            .Any(b => b.StartsWith("return_with_personal_answer:")));

        var form=Controller();
        PublicPlayerCandidate Hypothesis(string token,string suffix,bool selected) => new(token,"neutral-method-"+suffix,"form_field",false,
            Group:"neutral-variable",Value:"neutral-method-"+suffix,Purpose:"hypothesis",Selected:selected);
        var unknownForm=Controller().Decide(At(9,Hypothesis("unknown-a","a",false),Hypothesis("unknown-b","b",false)),held);
        check("r16_all_unknown_hypothesis_edits_have_no_information_bonus",unknownForm.Alternatives.Length==2
            && unknownForm.Alternatives.All(a=>!Inquiry(a)));
        var experiment = Controller();
        var configured = experiment.Decide(At(20, Hypothesis("set-a", "a", false)), held);
        check("r18_unknown_experiment_can_be_configured", configured.Token == "set-a");
        var submitted = experiment.Decide(At(21, Hypothesis("keep-a", "a", true),
            Hypothesis("switch-b", "b", false),
            new PublicPlayerCandidate("run", "neutral-run", "form_submit", false, Purpose: "commit")), held);
        Observe("configured-experiment-before-result", submitted);
        check("r18_configured_variable_is_submitted_before_unsupported_revision",
            submitted.Token == "run" && submitted.Alternatives.All(a => a.Token != "switch-b"));
        experiment.Decide(At(22, new PublicPlayerCandidate("read-result", "neutral-result", "continue", false)), held);
        var retry = experiment.Decide(At(23, Hypothesis("retry-b", "b", false)), held);
        check("r18_result_allows_new_hypothesis_trial", retry.Token == "retry-b");

        var revisable = Controller();
        revisable.Decide(At(24, Hypothesis("initial-b", "b", false)), held);
        var improved = revisable.Decide(At(25, Hypothesis("old-b", "b", true),
            Hypothesis("proven-a", "a", false)), learned);
        check("r18_new_proof_allows_better_hypothesis_before_commit", improved.Token == "proven-a");
        for(var i=0;i<6;i++) {
            var decision=form.Decide(At(10+i,Hypothesis("supported","a",true),Hypothesis("unknown","b",false),
                new PublicPlayerCandidate("commit","neutral-commit","form_submit",false)),learned);
            Observe("stable-hypothesis-"+i,decision);
            check("r16_hypothesis_does_not_switch_to_unknown_"+i,decision.Token!="unknown");
            check("r16_hypothesis_edit_has_no_information_bonus_"+i,decision.Alternatives.Where(a=>a.Kind=="form_field").All(a=>!Inquiry(a)));
        }
    }
}
