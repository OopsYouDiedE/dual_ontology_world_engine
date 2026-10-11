using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
using static DualOntologyWorldEngine.Story.ChapterEvents;

// Neutral public forms and acquired propositions; no chapter assets or answer tables.
internal static class FormEfficiencyCases
{
    private static Dictionary<string, object?> Field(string id, string heading, object[] options,
        string purpose = "", string value = "") => new()
        {
            ["id"] = id, ["kind"] = "choice", ["heading"] = heading, ["purpose"] = purpose,
            ["value"] = value, ["options"] = options
        };

    private static Dictionary<string, object?> Tab(string field) => new()
    { ["id"] = "tab-" + field, ["label"] = "neutral-page-" + field, ["tab"] = field };

    private static Dictionary<string, object?> Form(object[] fields, object[] actions, string question = "neutral-form")
        => Event("form", ("presentation_id", "private-neutral-form"), ("layout", "tabs"),
            ("header", Event("header", ("question", question))), ("fields", fields), ("actions", actions));

    private static Dictionary<string, object?> Commit(params string[] required) => new()
    { ["id"] = "submit", ["label"] = "neutral-submit", ["requires"] = required.Cast<object>().ToArray() };

    private static void Observe(string name, object value)
        => Console.WriteLine("OBSERVATION form-efficiency " + name + " " + JsonSerializer.Serialize(value));

    private static void Projection(Action<string, bool> check, int count)
    {
        var shown = new List<string>();
        var dispatched = new List<IReadOnlyDictionary<string, object?>>();
        var port = new PlayerCandidateSession(input => { dispatched.Add(input); return []; }, "neutral-actor",
            observeField: field => shown.Add(Text(field, "id")));
        var fields = Enumerable.Range(1, count).Select(i => Field("f" + i, "neutral-heading-" + i,
            [Option("v" + i, "neutral-option-" + i), Option("locked" + i, "neutral-locked-" + i, false, "neutral-hint-" + i)])).ToArray();
        var actions = fields.Select(f => (object)Tab(Text(f, "id"))).Append(Commit(fields.Select(f => Text(f, "id")).ToArray())).ToArray();
        var observation = port.Observe([Form(fields.Cast<object>().ToArray(), actions)]);
        Observe(count + "-field-columns", observation);
        check("columns_" + count + "_all_public_fields_and_locked_hints_visible", Enumerable.Range(1, count).All(i =>
            observation.Text.Contains("neutral-heading-" + i) && observation.Text.Contains("neutral-option-" + i)
            && observation.Text.Contains("neutral-locked-" + i + " neutral-hint-" + i)));
        check("columns_" + count + "_all_available_choices_offered", observation.Candidates.Length == count
            && observation.Candidates.All(c => c.Kind == "form_field")
            && Enumerable.Range(1, count).All(i => observation.Candidates.Any(c => c.Group == "neutral-heading-" + i)));
        check("columns_" + count + "_no_nonexistent_tab_operations", !observation.Candidates.Any(c => c.Purpose == "tab")
            && !JsonSerializer.Serialize(observation).Contains("neutral-page-"));
        check("columns_" + count + "_observe_callback_sees_each_public_field", shown.SequenceEqual(fields.Select(f => Text(f, "id"))));
        observation = port.Submit(new(observation.Candidates.First().Token));
        check("columns_" + count + "_selected_field_stays_editable_without_hiding_other_columns", observation.Candidates.Length == count
            && observation.Candidates.Count(c => c.Selected) == 1 && dispatched.Count == 0);
    }

    private static void FourFieldTabs(Action<string, bool> check)
    {
        var shown = new List<string>();
        var dispatched = new List<IReadOnlyDictionary<string, object?>>();
        var port = new PlayerCandidateSession(input => { dispatched.Add(input); return []; }, "neutral-actor",
            observeField: field => shown.Add(Text(field, "id")));
        var fields = Enumerable.Range(1, 4).Select(i => Field("f" + i, "neutral-heading-" + i,
            [Option("v" + i, "neutral-option-" + i)])).ToArray();
        var actions = fields.Select(f => (object)Tab(Text(f, "id"))).Append(Tab("absent-field")).ToArray();
        var observation = port.Observe([Form(fields.Cast<object>().ToArray(), actions)]);
        for (var i = 1; i <= 4; i++)
        {
            if (i > 1)
                observation = port.Submit(new(observation.Candidates.Single(c => c.Label == "neutral-page-f" + i).Token));
            Observe("four-field-active-" + i, observation);
            check("four_field_tabs_only_active_page_visible_" + i, observation.Candidates.Count(c => c.Kind == "form_field") == 1
                && observation.Candidates.Single(c => c.Kind == "form_field").Group == "neutral-heading-" + i
                && Enumerable.Range(1, 4).All(n => observation.Text.Contains("neutral-option-" + n) == (n == i)));
            check("four_field_tabs_only_active_page_observed_" + i, shown.Last() == "f" + i
                && observation.Candidates.Single(c => c.Purpose == "tab" && c.Selected).Label == "neutral-page-f" + i);
        }
        check("four_field_tabs_switches_local_and_invalid_target_absent", dispatched.Count == 0
            && port.Selections == 3 && !observation.Candidates.Any(c => c.Label == "neutral-page-absent-field"));
    }

    private static void InitialValues(Action<string, bool> check)
    {
        foreach (var kind in new[] { "valid", "implicit-available", "locked", "removed" })
        {
            var dispatched = new List<IReadOnlyDictionary<string, object?>>();
            var port = new PlayerCandidateSession(input => { dispatched.Add(input); return []; }, "neutral-actor");
            var initial = kind is "locked" or "removed" ? kind : "valid";
            Dictionary<string, object?> valid = kind == "implicit-available"
                ? new() { ["id"] = "valid", ["label"] = "neutral-valid" }
                : Option("valid", "neutral-valid");
            var fields = new object[]
            {
                Field("first", "neutral-first", [valid, Option("locked", "neutral-locked", false, "neutral-lock-hint")], value: initial),
                Field("second", "neutral-second", [Option("other", "neutral-other")], value: "other")
            };
            var observation = port.Observe([Form(fields, [Tab("first"), Tab("second"), Commit("first", "second")])]);
            var accepted = kind is "valid" or "implicit-available";
            check("initial_" + kind + "_selection_matches_available_public_option", observation.Candidates.Count(c => c.Selected) == (accepted ? 2 : 1)
                && observation.Candidates.Any(c => c.Purpose == "commit") == accepted);
            check("initial_" + kind + "_no_locked_or_removed_choice_offered", observation.Candidates.Where(c => c.Kind == "form_field")
                .All(c => c.Value is "neutral-valid" or "neutral-other"));
            if (!accepted)
            {
                observation = port.Submit(new(observation.Candidates.Single(c => c.Value == "neutral-valid").Token));
                check("initial_" + kind + "_requires_actual_valid_edit", observation.Candidates.Count(c => c.Selected) == 2
                    && dispatched.Count == 0 && observation.Candidates.Any(c => c.Purpose == "commit"));
            }
            port.Submit(new(observation.Candidates.Single(c => c.Purpose == "commit").Token));
            check("initial_" + kind + "_dispatch_contains_only_valid_current_values", dispatched.Count == 1
                && Text(Map(dispatched[0], "values"), "first") == "valid"
                && Text(Map(dispatched[0], "values"), "second") == "other");
        }
    }

    private static PlayerCognition Acquired()
    {
        var runtime = new WorldRuntime();
        using var simulation = new SimulationSession(runtime);
        var world = new CoreScriptWorld(runtime, simulation);
        var actor = world.DefineCharacter("neutral.form.actor", "neutral-actor", world.DefineSpace("neutral.form.room", "neutral-room"));
        var snapshot = ((ContractResult<CognitionSnapshotV1>.Success)actor.Soul.World.CaptureSnapshot()).Value;
        var cognition = new PlayerCognition(snapshot, actor.Self.SubjectId!.Value);
        foreach (var suffix in new[] { "a", "b", "c", "d" })
        {
            FrozenConditionV1 Premise(string index) => new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(cognition.Actor), new("neutral-premise-" + suffix + "-" + index), 1);
            var goal = new FrozenConditionV1.All([Premise("1"), Premise("2")]);
            FrozenProjectionV1 Material(string name, ProjectionKind kind, FrozenConditionV1 claim, Truth accepted, bool presentable = false)
                => new(new(snapshot.SubjectWorld, Guid.NewGuid()), name, kind, Truth.True, null,
                    presentable ? ImmutableDictionary<Tag, int>.Empty.Add(AcquiredPropositions.Presentable, 1) : ImmutableDictionary<Tag, int>.Empty,
                    [], [], [new(new FrozenConditionV1.Constant(Truth.True), RuleMode.Infer, new FrozenEffectV1.Think(claim), 1, accepted)],
                    null, [new(new("acquired.neutral." + name), name, name)], null);
            snapshot = snapshot with { Projections = snapshot.Projections.AddRange(ImmutableArray.Create(
                Material("neutral-claim-" + suffix, ProjectionKind.Concept, goal, Truth.Unknown),
                Material("neutral-source-" + suffix + "-1", ProjectionKind.Object, Premise("1"), Truth.True, true),
                Material("neutral-source-" + suffix + "-2", ProjectionKind.Object, Premise("2"), Truth.True, true),
                Material("neutral-question-" + suffix, ProjectionKind.Concept, goal, Truth.Unknown),
                Material("neutral-unrelated-" + suffix, ProjectionKind.Object, Premise("other"), Truth.True, true))) };
        }
        return cognition with { Snapshot = snapshot };
    }

    private static PlayerCognition Asked(PlayerCognition cognition, string suffix)
        => cognition with { Snapshot = cognition.Snapshot with { Projections = [.. cognition.Snapshot.Projections.Select(p =>
            p with { Tags = p.Name == "neutral-question-" + suffix
                ? p.Tags.SetItem(AcquiredPropositions.Question, 1) : p.Tags.Remove(AcquiredPropositions.Question) })] } };

    private static SubjectivePlayerController Controller() => new(new()
    {
        Actor = "neutral.form.actor", Name = "neutral-actor", Mode = "agent",
        Rules = [new() { Mode = "prefer", Kind = "untried", Weight = 6 }],
        Reasoning = new() { Depth = 8, Expansions = 4096, Hypotheses = 128, ArgumentWeight = 16, InformationWeight = 8 }
    }, 23);

    private static Dictionary<string, object?> ArgumentForm(string suffix, bool selected = false, bool tabs = false)
    {
        var fields = new List<object>
        {
            Field("statement", "neutral-statement-field", [Option("claim", "neutral-claim-" + suffix)], "statement", selected ? "claim" : ""),
            Field("evidence", "neutral-evidence-field", [Option("source-1", "neutral-source-" + suffix + "-1"),
                Option("source-2", "neutral-source-" + suffix + "-2"), Option("unrelated", "neutral-unrelated-" + suffix)], "evidence", selected ? "source-1" : "")
        };
        if (tabs)
        {
            fields.Add(Field("third", "neutral-third-field", [Option("third", "neutral-third-value")], value: "third"));
            fields.Add(Field("fourth", "neutral-fourth-field", [Option("fourth", "neutral-fourth-value")], value: "fourth"));
        }
        return Form(fields.ToArray(), [Tab("statement"), Tab("evidence"), Commit("statement", "evidence")], "neutral-question-" + suffix);
    }

    private static (PublicPlayerObservation Observation, PlayerDecision Decision, PublicPlayerCandidate Chosen) Step(
        PlayerCandidateSession port, SubjectivePlayerController controller, PublicPlayerObservation observation, PlayerCognition cognition, string label)
    {
        var decision = controller.Decide(observation, cognition);
        Observe(label, decision);
        var chosen = observation.Candidates.SingleOrDefault(c => c.Token == decision.Token)
            ?? throw new InvalidOperationException("Neutral form has no chosen current candidate: " + decision.Reason);
        return (port.Submit(new(chosen.Token)), decision, chosen);
    }

    private static void ArgumentSequence(Action<string, bool> check)
    {
        var acquired = Acquired();
        var controller = Controller();
        var submitted = new List<IReadOnlyDictionary<string, object?>>();
        var port = new PlayerCandidateSession(input => { submitted.Add(input); return [Event("close")]; }, "neutral-actor");
        long? freshCommitScore = null;
        foreach (var suffix in new[] { "a", "b", "c", "d" })
        {
            var cognition = Asked(acquired, suffix);
            var observation = port.Observe([ArgumentForm(suffix)]);
            var purposes = new List<string>();
            var selectedAgain = false;
            PlayerDecision? commit = null;
            for (var step = 0; step < 3; step++)
            {
                var result = Step(port, controller, observation, cognition, suffix + "-step-" + step);
                purposes.Add(result.Chosen.Purpose);
                selectedAgain |= result.Chosen.Selected;
                if (result.Chosen.Purpose == "commit") commit = result.Decision;
                observation = result.Observation;
            }
            check("argument_" + suffix + "_statement_then_supporting_evidence_then_one_commit", purposes.SequenceEqual(["statement", "evidence", "commit"])
                && submitted.Count == suffix[0] - 'a' + 1 && Text(submitted.Last(), "kind") == "form"
                && Text(Map(submitted.Last(), "values"), "statement") == "claim"
                && Text(Map(submitted.Last(), "values"), "evidence") is "source-1" or "source-2");
            check("argument_" + suffix + "_does_not_reselect_public_selected_options", !selectedAgain);
            check("argument_" + suffix + "_commit_keeps_current_complete_support", commit is not null
                && commit.Arguments.Any(a => a.Value == "True" && a.Complete && a.MaterialSupports == true));
            check("argument_" + suffix + "_new_question_does_not_inherit_submit_attempts", commit is not null
                && commit.Basis.Contains("authored_rule:prefer:untried:6")
                && !commit.Basis.Any(b => b.StartsWith("revisit_without_new_accepted_material:", StringComparison.Ordinal))
                && (freshCommitScore is null || freshCommitScore == commit.Score));
            freshCommitScore ??= commit?.Score;
        }

        // Reopen the same public question with its unchanged, valid displayed selections.
        var repeatedObservation = port.Observe([ArgumentForm("d", selected: true)]);
        var repeated = controller.Decide(repeatedObservation, Asked(acquired, "d"));
        Observe("unchanged-question-reopened", repeated);
        var repeatedCommit = repeated.Alternatives.Single(a => a.Kind == "form_submit");
        check("argument_unchanged_question_preserves_submit_attempt_cost", !repeatedCommit.Basis.Contains("authored_rule:prefer:untried:6")
            && repeatedCommit.Basis.Contains("revisit_without_new_accepted_material:1") && repeatedCommit.Score < freshCommitScore);

        // A fresh controller must use displayed selections directly, without redundant edits.
        var initialController = Controller();
        var initialPort = new PlayerCandidateSession(_ => [], "neutral-actor");
        var initialObservation = initialPort.Observe([ArgumentForm("a", selected: true)]);
        var initiallyComplete = initialController.Decide(initialObservation, Asked(acquired, "a"));
        Observe("initial-complete-public-selection", initiallyComplete);
        check("argument_valid_initial_statement_and_evidence_submit_without_reselection", initialObservation.Candidates
            .Single(c => c.Token == initiallyComplete.Token).Purpose == "commit"
            && initiallyComplete.Alternatives.Any(a => a.Kind == "form_field" && a.Score < initiallyComplete.Score));
    }

    private static void EvidenceTab(Action<string, bool> check)
    {
        var cognition = Asked(Acquired(), "a");
        var controller = Controller();
        var submitted = new List<IReadOnlyDictionary<string, object?>>();
        var port = new PlayerCandidateSession(input => { submitted.Add(input); return [Event("close")]; }, "neutral-actor");
        var observation = port.Observe([ArgumentForm("a", tabs: true)]);
        var purposes = new List<string>();
        for (var step = 0; step < 4; step++)
        {
            var result = Step(port, controller, observation, cognition, "true-tab-step-" + step);
            purposes.Add(result.Chosen.Purpose);
            if (step == 2)
            {
                check("true_tab_selects_supporting_evidence_instead_of_switching_back", result.Chosen.Purpose == "evidence"
                    && result.Decision.Arguments.Any(a => a.MaterialSupports == true && a.Complete && a.Value == "True"));
                var returnTab = result.Decision.Alternatives.Single(a => a.Label == "neutral-page-statement");
                check("true_tab_evidence_page_return_has_no_progress_reward", returnTab.Score == 6);
            }
            observation = result.Observation;
        }
        check("true_tab_argument_sequence_uses_one_required_page_switch_and_one_commit", purposes.SequenceEqual(["statement", "tab", "evidence", "commit"])
            && submitted.Count == 1 && port.Selections == 4);
    }

    public static void Run(Action<string, bool> check)
    {
        Projection(check, 2);
        Projection(check, 3);
        FourFieldTabs(check);
        InitialValues(check);
        ArgumentSequence(check);
        EvidenceTab(check);
    }
}
