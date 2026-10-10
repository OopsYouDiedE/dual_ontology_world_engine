using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

var checks = 0;
void Check(bool okay, string name) { if (!okay) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
T Value<T>(ContractResult<T> r) => r is ContractResult<T>.Success s ? s.Value : throw new Exception(((ContractResult<T>.Failure)r).Error.ToString());
var compiledController = PlayerControllers.Read("""
    {"format":"dual-ontology-world-script/v1","chapters":[{"id":"fixture",
    "entities":[{"id":"actor","kind":"character","scope":"world"}],
    "controllers":[{"actor":"world:actor","name":"actor","mode":"agent",
    "rules":[{"mode":"prefer","kind":"untried","weight":6}],
    "reasoning":{"depth":8,"expansions":32768,"hypotheses":128,"argument_weight":16,"information_weight":8}}]}]}
    """).Single();
Check(compiledController.Reasoning is { Depth: 8, Expansions: 32768, Hypotheses: 128, ArgumentWeight: 16, InformationWeight: 8 },
    "compiled_role_reasoning_parameters_survive_the_production_loader");
Check(ChapterEvents.SourceSite("person:site") == "site" && ChapterEvents.SourceSite("item") == "item",
    "runtime_person_site_and_authored_observation_key_share_the_same_source");
foreach (var nested in new[] { false, true })
{
    var soul = new SoulEssence("observer"); var world = new WorldRuntime(); world.Project(soul, "observer");
    var room = Observation.Observe(soul, new("room", ProjectionKind.Space) { Existence = Truth.True });
    var actor = Observation.Observe(soul, new("actor", ProjectionKind.Body) { Existence = Truth.True, Parent = nested ? room : null, Move = nested });
    var physical = Observation.Observe(soul, new("physical", ProjectionKind.Object) { Existence = Truth.True });
    var speaker = Observation.Observe(soul, new("speaker", ProjectionKind.Body) { Existence = Truth.True });
    KnowledgeBinding Binding(string kind, string key, string source, string claim, string who = "", int reliability = 90)
        => new() { Kind = kind, Key = key, Source = source, Propositions = [claim], Speaker = who, Reliability = reliability, Act = source == "proposal" ? "propose" : source == "memory" ? "recall" : "assert" };
    var declaration = new KnowledgeDeclaration { Propositions = new() { ["p"] = "p", ["q"] = "q" },
        Bindings = [Binding("card", "item", "perception", "p"), Binding("statement", "claim", "proposal", "q"),
            Binding("statement", "opposite", "proposal", "!q"), Binding("line", "testimony", "testimony", "p", "speaker"),
            Binding("line", "memory", "memory", "p"), Binding("field", "site", "proposal", "p")],
        Implications = [new() { Premises = ["p"], Conclusion = "q" }] };
    var acquisition = new AcquiredPropositions(declaration, soul);
    Check(soul.World.Projections.Length == 4, nested + " declarations_do_not_preload_knowledge");
    var recognized = acquisition.ObserveSpeaker("speaker", 3, 1);
    Check(ReferenceEquals(recognized, speaker) && recognized.Tags[Tags.Trust] == 3,
        nested + " hearing_initializes_authored_personal_speaker_trust");
    Observation.Observe(soul, new(speaker.Name, speaker.Kind) { Target = speaker, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, -2) });
    Check(acquisition.ObserveSpeaker("speaker", 3, 2).Tags[Tags.Trust] == -2,
        nested + " repeated_hearing_preserves_revised_personal_trust");
    acquisition.Acquire("statement", "claim", "claim", 1);
    acquisition.Acquire("statement", "opposite", "opposite", 1);
    var claim = soul.World.Projections.Single(p => p.Name == "claim");
    SubjectiveReasoner Reason() => new(Value(soul.World.CaptureSnapshot()), actor.SubjectId!.Value, new(8, 4096, 16));
    var goal = Reason().Claims(claim.SubjectId!.Value).Single();
    Check(Reason().Prove(goal).Value == Truth.Unknown && !Reason().Prove(goal).Contradicted,
        nested + " opposing_proposals_are_not_conflicting_beliefs");
    acquisition.Acquire("line", "testimony", "heard", 2, "wrong", perceivedSpeaker: speaker);
    Check(!soul.World.Projections.Any(p => p.Name == "heard"), nested + " speaker_binding_gates_hearing");
    acquisition.Acquire("line", "testimony", "heard", 2, "speaker", perceivedSpeaker: speaker, trust: 10);
    Check(Reason().Prove(goal).Value == Truth.Unknown, nested + " low_reliability_testimony_is_proposed_only");
    var heard = soul.World.Projections.Single(p => p.Name == "heard");
    Check(heard.LastObservation!.Source == ObservationSource.Testimony && heard.Tags[AcquiredPropositions.Reliability] == 9,
        nested + " hearing_preserves_source_and_effective_reliability");
    acquisition.Acquire("line", "testimony", "heard", 3, "speaker", perceivedSpeaker: speaker, trust: 100);
    Check(Reason().Prove(goal).Value == Truth.True, nested + " changed_personal_trust_can_accept_testimony");
    acquisition.Acquire("card", "item", "item", 4, physicalSource: physical);
    var item = soul.World.Projections.Single(p => p.Name == "item");
    Check(Reason().Prove(goal).Basis.Contains(item.SubjectId!.Value), nested + " proof_contains_personally_observed_material");
    var version = soul.World.Revision; acquisition.Acquire("card", "item", "item", 4, physicalSource: physical);
    Check(soul.World.Revision == version, nested + " reopening_identical_material_is_idempotent");
    Observation.Observe(soul, new(physical.Name, physical.Kind) { Target = physical, Existence = Truth.False });
    acquisition.RefreshAvailability(5);
    Check(item.Tags[AcquiredPropositions.Presentable] == 0 && Reason().Prove(goal).Value == Truth.True,
        nested + " destroyed_physical_source_does_not_erase_memory");
    var controllerDeclaration = new ActorControllerDefinition { Actor = "neutral.actor", Name = "actor", Mode = "agent",
        Rules = [new() { Mode = "prefer", Kind = "untried", Weight = 6 }],
        Reasoning = compiledController.Reasoning };
    PlayerCognition Cognition() => new(Value(soul.World.CaptureSnapshot()), actor.SubjectId!.Value);
    var candidateObservation = new PublicPlayerObservation(1, [], [], [new("positive", "claim", "form_field", false, Value: "claim", Purpose: "statement"),
        new("negative", "opposite", "form_field", false, Value: "opposite", Purpose: "statement")], false);
    var hidden = new Essence("hidden"); hidden.Add(new StateProperty()).Values[new("secret")] = 0; world.Project(hidden, "hidden");
    var a = new SubjectivePlayerController(controllerDeclaration, 17).Decide(candidateObservation, Cognition());
    hidden.Get<StateProperty>()!.Values[new("secret")] = 91;
    var b = new SubjectivePlayerController(controllerDeclaration, 17).Decide(candidateObservation, Cognition());
    Check(a.Token == "positive" && a.Token == b.Token && a.Basis.SequenceEqual(b.Basis), nested + " semantic_choices_ignore_hidden_truth_metamorphism");
    Check(a.Arguments.Length == 1 && a.Arguments[0].Materials.Contains("heard")
        && !JsonSerializer.Serialize(a).Contains(soul.World.SubjectWorldId!.Value.ToString()), nested + " argument_trace_contains_public_personal_chain_only");
    Check(a.Arguments[0].Derivations.Any(d => d.Premises.Length == 1 && d.Conclusion == "q"),
        nested + " trace_records_inference_premise_and_material");
    var replayController = new SubjectivePlayerController(controllerDeclaration, 17);
    replayController.Decide(candidateObservation with { Candidates = [candidateObservation.Candidates[0] with { Purpose = "hypothesis" }] }, Cognition());
    var replayChoice = replayController.Decide(candidateObservation with { Candidates = [candidateObservation.Candidates[0] with { Purpose = "hypothesis", Selected = true },
        new("run", "run", "form_submit", false, Purpose: "commit")] }, Cognition());
    Check(replayChoice.Token == "run", nested + " hypothesis_selection_does_not_require_argument_evidence");
    var exploring = new SubjectivePlayerController(controllerDeclaration, 17);
    var inspected = new PublicPlayerCandidate("seen", "fixture", "interact", false);
    exploring.Decide(new(1, [], [], [inspected], false), Cognition());
    var walking = new PublicPlayerCandidate("walk", "8,8", "move", false) { Location = new(8, 8) };
    var leave = exploring.Decide(new(2, [], [], [inspected, walking], false), Cognition());
    Check(leave.Token == "walk", nested + " repeated_inspection_yields_to_unvisited_space");
    var jitter = walking with { Token = "jitter", Label = "15,15", Location = new(15, 15) };
    var fresh = walking with { Token = "fresh", Label = "80,80", Location = new(80, 80) };
    var explorationChoice = exploring.Decide(new(3, [], [], [jitter, fresh], false), Cognition());
    Check(explorationChoice.Token == "fresh", nested + " coordinate_jitter_does_not_create_spatial_novelty");
    var traceController = new SubjectivePlayerController(controllerDeclaration, 17);
    var unattachedEvidence = traceController.Decide(new(1, [], [], [new("item", "item", "form_field", false, Value: "item", Purpose: "evidence")], false), Cognition());
    Check(unattachedEvidence.Arguments.IsEmpty && !unattachedEvidence.Basis.Any(b => b.StartsWith("subjective_argument_supported:")),
        nested + " evidence_without_statement_cannot_claim_argument_support");
    traceController = new(controllerDeclaration, 17);
    traceController.Decide(candidateObservation with { Candidates = [candidateObservation.Candidates[0]] }, Cognition());
    var commit = new PublicPlayerObservation(4, [], [], [new("commit", "commit", "form_submit", false, Purpose: "commit")], false);
    var firstCommit = traceController.Decide(commit, Cognition());
    var nextCommit = traceController.Decide(commit with { Turn = 5 }, Cognition());
    Check(firstCommit.Arguments.Length == 1 && nextCommit.Arguments.IsEmpty,
        nested + " unrelated_submission_does_not_reuse_previous_argument_trace");
    // A fresh person without the material must retain a gap until recall occurs.
    var other = new SoulEssence("other"); var otherActor = Observation.Observe(other, new("actor", ProjectionKind.Body) { Existence = Truth.True });
    var missing = new AcquiredPropositions(declaration, other);
    missing.Acquire("statement", "claim", "claim", 1);
    var cautious = new SubjectivePlayerController(controllerDeclaration, 17);
    PlayerCognition MissingCognition() => new(Value(other.World.CaptureSnapshot()), otherActor.SubjectId!.Value);
    cautious.Decide(candidateObservation with { Candidates = [candidateObservation.Candidates[0]] }, MissingCognition());
    var leaveUnknown = cautious.Decide(commit with { Candidates = [commit.Candidates[0], new("cancel", "cancel", "form_submit", false, Purpose: "cancel")] }, MissingCognition());
    Check(leaveUnknown.Token == "cancel", nested + " unknown_statement_cannot_submit_without_personal_basis");
    declaration.Bindings.Add(Binding("question", "asked", "proposal", "q"));
    missing.Acquire("question", "asked", "asked", 2);
    var answering = new SubjectivePlayerController(controllerDeclaration, 17);
    var cancelUnknown = answering.Decide(candidateObservation with { Candidates = [candidateObservation.Candidates[0], new("cancel", "cancel", "form_submit", false, Purpose: "cancel")] }, MissingCognition());
    Check(cancelUnknown.Token == "cancel", nested + " current_unknown_question_returns_to_investigation");
    acquisition.Acquire("question", "asked", "asked", 5);
    declaration.Bindings.Add(Binding("statement", "other-claim", "proposal", "p"));
    acquisition.Acquire("statement", "other-claim", "other-claim", 5);
    var answerCurrent = new SubjectivePlayerController(controllerDeclaration, 17).Decide(candidateObservation with { Candidates =
        [new("other", "other-claim", "form_field", false, Value: "other-claim", Purpose: "statement"), candidateObservation.Candidates[0]] }, Cognition());
    Check(answerCurrent.Token == "positive", nested + " supported_statement_must_answer_the_visible_current_question");
    declaration.Propositions["r"] = "r";
    declaration.Bindings.Add(Binding("card", "fresh", "perception", "p"));
    declaration.Bindings.Add(Binding("card", "spare", "perception", "r"));
    declaration.Bindings.Add(Binding("field", "place", "proposal", "q"));
    acquisition.Acquire("card", "fresh", "fresh", 6);
    acquisition.Acquire("card", "spare", "spare", 6);
    acquisition.Acquire("field", "place", "place", 6);
    var consistent = new SubjectivePlayerController(controllerDeclaration, 17);
    consistent.Decide(candidateObservation with { Candidates = [candidateObservation.Candidates[0]] }, Cognition());
    var supporting = new PublicPlayerCandidate("fresh", "fresh", "form_field", false, Value: "fresh", Purpose: "evidence");
    var unrelated = new PublicPlayerCandidate("spare", "spare", "form_field", true, Value: "spare", Purpose: "evidence");
    var evidenceChoice = consistent.Decide(new(6, [], [], [supporting, unrelated], false), Cognition());
    Check(evidenceChoice.Token == "fresh" && evidenceChoice.Arguments.Single().MaterialSupports == true,
        nested + " fresh_unrelated_evidence_cannot_replace_material_in_current_proof");
    var readyCommit = consistent.Decide(commit with { Candidates = [commit.Candidates[0], supporting with { Selected = true }, unrelated,
        new("cancel", "cancel", "form_submit", false, Purpose: "cancel")] }, Cognition());
    Check(readyCommit.Token == "commit" && readyCommit.Arguments.Single().Value == "True",
        nested + " supported_statement_and_evidence_submit_without_touring_unrelated_materials");
    var returner = new SubjectivePlayerController(controllerDeclaration, 17);
    var place = new PublicPlayerCandidate("place", "place", "interact", false) { Location = new(1000, 64) };
    returner.Decide(new(7, [], [], [place], false), Cognition());
    var nearer = walking with { Token = "nearer", Label = "640,64", Location = new(640, 64) };
    var farther = walking with { Token = "farther", Label = "32,64", Location = new(32, 64) };
    var returnChoice = returner.Decide(new(8, [], [], [nearer, farther], false), Cognition());
    Check(returnChoice.Token == "nearer" && returnChoice.Basis.Contains("approach_observed_answer_opportunity"),
        nested + " newly_supported_question_guides_existing_moves_toward_a_personally_seen_place");
    var answerEntry = returner.Decide(new(9, [], [], [new("inspect", "inspect place", "menu", true),
        new("answer", "answer", "menu", false, Purpose: "argument")], false), Cognition());
    Check(answerEntry.Token == "answer" && answerEntry.Arguments.Single().Value == "True",
        nested + " generic_argument_entry_uses_personal_proof_instead_of_reopening_a_card");
    SubjectiveProofV1 MissingProof()
    {
        var r = new SubjectiveReasoner(Value(other.World.CaptureSnapshot()), otherActor.SubjectId!.Value, new(8, 4096, 16));
        return r.Prove(r.Claims(other.World.Projections.Single(p => p.Name == "claim").SubjectId!.Value).Single());
    }
    Check(MissingProof().Value == Truth.Unknown && MissingProof().Missing.Length > 0, nested + " absent_evidence_reports_gap");
    missing.Acquire("field", ChapterEvents.SourceSite("person:site"), "needed", 2);
    var questionInvestigator = new SubjectivePlayerController(controllerDeclaration, 17);
    var neededPlace = place with { Token = "needed", Label = "needed" };
    var seekNeeded = questionInvestigator.Decide(new(10, [], [], [neededPlace], false), MissingCognition());
    Check(seekNeeded.Basis.Contains("seek_current_question_material:needed") && MissingProof().Value == Truth.Unknown,
        nested + " visible_person_offer_guides_inquiry_without_preloading_its_testimony");
    var incidentalName = new PublicPlayerCandidate("incidental", "needed unrelated", "menu", false);
    var incidentalChoice = new SubjectivePlayerController(controllerDeclaration, 17).Decide(
        new(10, [], [], [incidentalName], false), MissingCognition());
    Check(!incidentalChoice.Basis.Any(b => b.StartsWith("seek_", StringComparison.Ordinal)),
        nested + " incidental_name_in_submenu_does_not_borrow_parent_material_opportunity");
    declaration.Bindings.Add(Binding("choice", "specific", "proposal", "p"));
    missing.Acquire("choice", "specific", "ask needed", 2);
    var specificChoice = new SubjectivePlayerController(controllerDeclaration, 17).Decide(
        new(10, [], [], [new("specific", "ask needed", "menu", false)], false), MissingCognition());
    Check(specificChoice.Basis.Contains("seek_current_question_material:ask needed") && MissingProof().Value == Truth.Unknown,
        nested + " specifically_observed_choice_retains_its_own_material_opportunity");
    var seekMove = questionInvestigator.Decide(new(11, [], [], [nearer, farther], false), MissingCognition());
    Check(seekMove.Token == "nearer" && seekMove.Basis.Contains("approach_observed_question_material"),
        nested + " unknown_question_guides_existing_moves_toward_personally_seen_material_opportunities");
    var revision = new PublicPlayerCandidate("revision", "claim", "menu", false);
    questionInvestigator.Decide(new(12, [], [], [revision], false), MissingCognition());
    declaration.Bindings.Add(Binding("card", "revision", "perception", "r"));
    missing.Acquire("card", "revision", "revision", 2);
    var retry = questionInvestigator.Decide(new(13, [], [], [neededPlace, new("idle", "idle", "wait", false)], false), MissingCognition());
    Check(retry.Token == "needed" && retry.Basis.Contains("seek_current_question_material:needed"),
        nested + " new_accepted_material_reconsiders_previously_tried_inquiry");
    missing.Acquire("line", "memory", "remembered", 2);
    Check(MissingProof().Value == Truth.True && MissingProof().Basis.Any(id => other.World.Projections.Single(p => p.SubjectId == id).Name == "remembered"),
        nested + " absent_evidence_can_use_personal_memory_path");
}
if (args.Length == 2 && args[0] == "--fixture")
    foreach (var name in new[] { "full", "missing" })
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(args[1], name + ".json")));
        var soul = new SoulEssence("fixture");
        var actor = Observation.Observe(soul, new("actor", ProjectionKind.Body) { Existence = Truth.True });
        var acquisition = new AcquiredPropositions(AcquiredPropositions.Read(data.RootElement), soul);
        acquisition.Acquire("statement", "claim", "claim", 1);
        acquisition.Acquire("card", "item", "item", 2);
        acquisition.Acquire("choice", "recall", "recall", 2);
        SubjectiveProofV1 Proof()
        {
            var r = new SubjectiveReasoner(Value(soul.World.CaptureSnapshot()), actor.SubjectId!.Value, new(8, 4096, 16));
            return r.Prove(r.Claims(soul.World.Projections.Single(p => p.Name == "claim").SubjectId!.Value).Single());
        }
        Check(Proof().Value == (name == "full" ? Truth.True : Truth.Unknown), name + " compiled_story_controls_actual_acquisition");
        if (name == "missing")
        {
            var controller = new SubjectivePlayerController(new() { Actor = "neutral.actor", Name = "actor", Mode = "agent",
                Rules = [new() { Mode = "prefer", Kind = "untried", Weight = 6 }],
                Reasoning = new() { Depth = 8, Expansions = 4096, ArgumentWeight = 16, InformationWeight = 8 } }, 17);
            var decision = controller.Decide(new(1, [], [], [new("recall", "recall", "menu", false), new("idle", "idle", "wait", false)], false),
                new(Value(soul.World.CaptureSnapshot()), actor.SubjectId!.Value));
            Check(decision.Token == "recall" && decision.Basis.Any(b => b.StartsWith("seek_unknown_acquired_proposition:")),
                "removed_story_evidence_redirects_agent_to_recall");
            acquisition.Acquire("line", "memory", "remembered", 3, "observer");
            Check(Proof().Value == Truth.True && Proof().Derivations.Length > 0, "removed_story_evidence_uses_actual_recall_chain");
        }
}
var spatialPort = new PlayerCandidateSession(_ => [], "actor");
var spatial = spatialPort.Observe([], [new("point", "move", false, new Dictionary<string, object?> { ["hidden"] = "payload" }) { Location = new(8, 16) }]);
Check(spatial.Candidates.Single().Location == new PlayerPoint(8, 16) && !JsonSerializer.Serialize(spatial).Contains("payload"),
    "public_navigation_point_is_separate_from_private_payload");
Console.WriteLine($"TOTAL {checks} checks");
