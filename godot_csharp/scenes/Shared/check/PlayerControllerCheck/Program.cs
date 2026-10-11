using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
using DualOntologyWorldEngine.Gameplay;
using static DualOntologyWorldEngine.Story.ChapterEvents;

var passed = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new Exception("FAIL " + name);
    Console.WriteLine("PASS " + name); passed++;
}
void Reject(string name, Action action, string code)
{
    try { action(); throw new Exception("Did not reject: " + name); }
    catch (PlayerBoundaryException error) { Check(name, error.Code == code); }
}
ActorControllerDefinition Declaration(params ControllerRuleDefinition[] rules) => new()
{ Actor = "world:character.controlled", Name = "A", Mode = "agent", Rules = [..rules.Length > 0 ? rules : [new() { Mode = "prefer", Kind = "untried", Weight = 6 }]] };
Dictionary<string, object?> Menu(string label = "A") => Event("candidates", ("presentation_id", "private-presentation"),
    ("options", new object[] { Option("private-id", label), Option("private-locked", "B", false, "C") }));
PlayerCandidateSession Port(Func<IReadOnlyDictionary<string, object?>, IReadOnlyList<Dictionary<string, object?>>>? submit = null,
    int selections = 10, int observations = 100) => new(submit ?? (_ => []), "A", selections, observations);
PlayerCognition Cognition(int truth, bool nested = false)
{
    var runtime = new WorldRuntime();
    using var simulation = new SimulationSession(runtime);
    var world = new CoreScriptWorld(runtime, simulation);
    var room = world.DefineSpace("neutral.room", "R");
    var hidden = world.DefineObject("neutral.hidden", "H", room, e => e.Get<StateProperty>()!.Values[new("secret")] = truth);
    var actor = world.DefineCharacter("neutral.actor", "A", room);
    if (nested)
    {
        var container = world.DefineObject("neutral.container", "C", room);
        var item = world.DefineObject("neutral.item", "I", container);
        world.Observe(actor, container, item);
    }
    var snapshot = ((ContractResult<CognitionSnapshotV1>.Success)actor.Soul.World.CaptureSnapshot()).Value;
    if (snapshot.Projections.Any(p => p.Tags.ContainsKey(new("secret")))) throw new Exception("Fixture leaked reality");
    return new(snapshot, actor.Self.SubjectId!.Value);
}

RepetitionResultCases.Run(Check);
GroundKnowledgeCases.Run(Check);
PublicMenuScopeCases.Run(Check);
InteractionReceiptCases.Run(Check);
InformationContextCases.Run(Check);
FormEfficiencyCases.Run(Check);
ScopedDeferredAnswerCases.Run(Check);
Check("narrow_port", typeof(IPlayerCandidatePort).GetMethods().Select(m => m.Name).SequenceEqual(["Submit"]));
Check("controller_has_no_engine_objects", typeof(SubjectivePlayerController).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
    .All(f => f.FieldType != typeof(WorldSession) && f.FieldType != typeof(GameSession) && f.FieldType != typeof(WorldRuntime)));
var submitted = new List<IReadOnlyDictionary<string, object?>>();
var port = Port(input => { submitted.Add(input); return []; });
var observation = port.Observe([Menu(), Event("state", ("snapshot", new Dictionary<string, object?> { ["hidden"] = "secret-sentinel", ["reality"] = 42 }))]);
var wire = JsonSerializer.Serialize(observation);
Check("projection_omits_private_fields", !wire.Contains("secret-sentinel") && !wire.Contains("private-id") && !wire.Contains("private-presentation") && !wire.Contains("reality"));
Check("locked_option_not_selectable", observation.Candidates.Length == 1);
var cardPort = Port();
var visibleCard = cardPort.Observe([Event("card", ("id", "private-card"), ("intro", "A"),
    ("items", new object[] { "B" }), ("table", new object[] { new object[] { "C", "D" } }),
    ("hidden", "secret-card"))]);
Check("card_body_and_table_visible", visibleCard.Text.SequenceEqual(["A", "B", "C\tD"])
    && !JsonSerializer.Serialize(visibleCard).Contains("secret-card"));
var token = observation.Candidates.Single().Token;
port.Submit(new(token));
Check("menu_enters_formal_dispatch", Text(submitted.Single(), "kind") == "select" && Text(submitted.Single(), "choice_id") == "private-id");
Reject("consumed_token", () => port.Submit(new(token)), "unknown_consumed_or_expired_token");
Reject("closed_after_rejection", () => port.Observe([Menu()]), "boundary_closed");
port = Port(); token = port.Observe([Menu()]).Candidates.Single().Token; port.Observe([Menu()]);
Reject("expired_token", () => port.Submit(new(token)), "unknown_consumed_or_expired_token");
port = Port(); token = port.Observe([Menu()]).Candidates.Single().Token; port.Observe([Event("state", ("snapshot", new Dictionary<string, object?>()))]);
port.Submit(new(token));
Check("refresh_keeps_unchanged_offer", true);
port = Port(); port.Observe([Menu()]);
Reject("unknown_token", () => port.Submit(new("invented")), "unknown_consumed_or_expired_token");
port = Port(selections: 0); token = port.Observe([Menu()]).Candidates.Single().Token;
Reject("zero_selection_budget", () => port.Submit(new(token)), "selection_budget");
port = Port(observations: 1); port.Observe([Menu()]);
Reject("observation_budget", () => port.Observe([]), "observation_budget");
port = Port(_ => throw new IOException("fixture")); token = port.Observe([Menu()]).Candidates.Single().Token;
try { port.Submit(new(token)); } catch (IOException) { }
Reject("port_failure_closes", () => port.Submit(new(token)), "boundary_closed");

submitted.Clear(); port = Port(input => { submitted.Add(input); return []; });
var form = Event("form", ("presentation_id", "P"), ("header", Event("header", ("question", "Q"))),
    ("fields", new object[] { new Dictionary<string, object?> { ["id"] = "statement", ["kind"] = "choice", ["options"] = new object[] { Option("S", "A") } },
        new Dictionary<string, object?> { ["id"] = "evidence", ["kind"] = "choice", ["options"] = new object[] { Option("E", "B") } } }),
    ("actions", new object[] { new Dictionary<string, object?> { ["id"] = "submit", ["label"] = "C", ["requires"] = new object[] { "statement", "evidence" } } }));
observation = port.Observe([form]);
Check("form_header_projected", observation.Text.Contains("Q"));
observation = port.Submit(new(observation.Candidates.Single().Token));
Check("form_local_edit_no_dispatch", submitted.Count == 0 && observation.Candidates.Single().Label.Trim() == "B");
observation = port.Submit(new(observation.Candidates.Single().Token));
Check("form_submit_after_required_fields", observation.Candidates.Single().Kind == "form_submit");
port.Submit(new(observation.Candidates.Single().Token));
Check("form_uses_visible_values", Text(submitted.Single(), "kind") == "form" && Text(Map(submitted.Single(), "values"), "statement") == "S" && Text(Map(submitted.Single(), "values"), "evidence") == "E");
var tabForm = Event("form", ("presentation_id", "private-tab-form"), ("layout", "tabs"),
    ("fields", new object[] {
        new Dictionary<string, object?> { ["id"] = "private-first", ["kind"] = "choice", ["heading"] = "first-heading", ["options"] = new object[] { Option("A1", "first-option"), Option("A2", "first-other") } },
        new Dictionary<string, object?> { ["id"] = "private-second", ["kind"] = "choice", ["heading"] = "second-heading", ["options"] = new object[] { Option("B1", "second-option") } },
        new Dictionary<string, object?> { ["id"] = "private-third", ["kind"] = "choice", ["heading"] = "third-heading", ["value"] = "C1", ["options"] = new object[] { Option("C1", "third-option") } },
        new Dictionary<string, object?> { ["id"] = "private-fourth", ["kind"] = "choice", ["heading"] = "fourth-heading", ["value"] = "D1", ["options"] = new object[] { Option("D1", "fourth-option") } } }),
    ("actions", new object[] {
        new Dictionary<string, object?> { ["id"] = "tab-a", ["label"] = "page A", ["tab"] = "private-first" },
        new Dictionary<string, object?> { ["id"] = "tab-b", ["label"] = "page B", ["tab"] = "private-second" },
        new Dictionary<string, object?> { ["id"] = "submit", ["label"] = "submit", ["requires"] = new object[] { "private-first", "private-second" } } }));
submitted.Clear(); port = Port(input => { submitted.Add(input); return []; });
observation = port.Observe([tabForm]);
Check("tab_form_only_active_field_visible", !JsonSerializer.Serialize(observation).Contains("second-option")
    && !observation.Text.Contains("second-heading") && observation.Candidates.Count(c => c.Kind == "form_field") == 2);
var previousTabTokens = observation.Candidates.Select(c => c.Token).ToHashSet();
observation = port.Submit(new(observation.Candidates.Single(c => c.Label == "page B").Token));
Check("tab_switch_local_and_current_tokens", submitted.Count == 0 && Text(port.LastInput!, "kind") == "form_tab"
    && observation.Candidates.All(c => !previousTabTokens.Contains(c.Token)));
Check("tab_switch_reveals_only_new_field", observation.Text.Contains("second-option")
    && !observation.Text.Contains("first-option") && observation.Candidates.Single(c => c.Kind == "form_field").Label == "second-heading second-option");
observation = port.Submit(new(observation.Candidates.Single(c => c.Kind == "form_field").Token));
Check("tab_submit_requires_both_fields", !observation.Candidates.Any(c => c.Kind == "form_submit"));
observation = port.Submit(new(observation.Candidates.Single(c => c.Label == "page A").Token));
observation = port.Submit(new(observation.Candidates.Single(c => c.Label == "first-heading first-option").Token));
Check("tab_selected_field_remains_editable", observation.Candidates.Count(c => c.Kind == "form_field") == 2);
observation = port.Submit(new(observation.Candidates.Single(c => c.Label == "first-heading first-other").Token));
port.Submit(new(observation.Candidates.Single(c => c.Kind == "form_submit").Token));
Check("tab_form_formal_submit_preserves_edits", submitted.Count == 1 && Text(submitted.Single(), "kind") == "form"
    && Text(Map(submitted.Single(), "values"), "private-first") == "A2"
    && Text(Map(submitted.Single(), "values"), "private-second") == "B1");
port = Port(); form["fields"] = new object[] { new Dictionary<string, object?> { ["id"] = "F", ["kind"] = "text", ["placeholder"] = "A" } };
Check("free_text_uses_actor_self_name", port.Observe([form]).Candidates.Single().Label == "A A");
var cancellable = new Dictionary<string, object?>(form)
{
    ["actions"] = new object[] { new Dictionary<string, object?> { ["id"] = "cancel", ["label"] = "C" },
        new Dictionary<string, object?> { ["id"] = "submit", ["label"] = "D", ["requires"] = new object[] { "F" } } }
};
port = Port(); observation = port.Observe([cancellable]);
Check("form_cancel_available_before_edit", observation.Candidates.Count(c => c.Kind == "form_submit") == 1
    && observation.Candidates.Any(c => c.Kind == "form_field"));
port = Port(); form["fields"] = new object[] { new Dictionary<string, object?> { ["id"] = "F", ["kind"] = "unknown" } };
Reject("unsupported_form_rejected", () => port.Observe([form]), "unsupported_form_field");
submitted.Clear(); port = Port(input => { submitted.Add(input); return []; });
observation = port.Observe([], [new("A", "move", false, Event("field_action", ("point", new object[] { 1, 2 })))]);
port.Submit(new(observation.Candidates.Single().Token));
Check("movement_enters_trusted_formal_route", Text(submitted.Single(), "kind") == "field_action" && submitted.Single().ContainsKey("point"));

PlayerCandidateSession? locatedPort = null;
var locatedInputs = new List<IReadOnlyDictionary<string, object?>>();
locatedPort = Port(input =>
{
    locatedInputs.Add(input);
    locatedPort!.Locate(new("opaque-field-a", new(80, 96)));
    return [Event("aside", ("text", "neutral-feedback"))];
});
locatedPort.Locate(new("opaque-field-a", new(16, 32)));
var locatedObservation = locatedPort.Observe([], [new("neutral-step", "move", false,
    Event("field_action", ("point", new object[] { 800, 900 }), ("private_route", "private-route-sentinel")))
    { Location = new(800, 900) }]);
Check("field_locate_observe_uses_actual_position", locatedObservation.Field == new PlayerFieldState("opaque-field-a", new(16, 32))
    && !JsonSerializer.Serialize(locatedObservation).Contains("private-route-sentinel"));
var locatedResult = locatedPort.Submit(new(locatedObservation.Candidates.Single().Token));
Check("field_submit_returns_latest_measured_position", locatedInputs.Count == 1
    && Text(locatedInputs.Single(), "kind") == "field_action"
    && locatedResult.Field == new PlayerFieldState("opaque-field-a", new(80, 96)));
Check("field_metadata_survives_presentation_refresh", locatedPort.Observe([Menu()]).Field == locatedResult.Field);
var travelPort = Port();
var travelObservation = travelPort.Observe([], [new("neutral-route", "move", false,
    Event("field_action", ("point", new object[] { 16, 144 }))) { Location = new(16, 144), TravelDistance = 720.5 }]);
Check("port_preserves_observed_route_distance", travelObservation.Candidates.Single().TravelDistance == 720.5
    && travelPort.Observe([]).Candidates.Single().TravelDistance == 720.5);
foreach (var invalidDistance in new[] { double.NaN, double.PositiveInfinity, -1.0, 1000001.0 })
{
    var invalidTravelPort = Port();
    Reject("invalid_public_route_distance_" + invalidDistance, () => invalidTravelPort.Observe([], [new("neutral-route", "move", false,
        Event("field_action", ("point", new object[] { 16, 144 }))) { TravelDistance = invalidDistance }]), "invalid_travel_distance");
}
foreach (var invalid in new[]
{
    new PlayerFieldState("", new(0, 0)), new PlayerFieldState(new string('x', 129), new(0, 0)),
    new PlayerFieldState("opaque-field-a", new(double.NaN, 0)),
    new PlayerFieldState("opaque-field-a", new(0, double.PositiveInfinity))
})
{
    var invalidPort = Port();
    var label = invalid.Space.Length == 0 ? "empty_space" : invalid.Space.Length > 128 ? "overlong_space"
        : double.IsNaN(invalid.Position.X) ? "nan_position" : "infinite_position";
    Reject("field_rejects_" + label, () => invalidPort.Locate(invalid), "invalid_field_observation");
    Reject("field_rejection_closes_" + label, () => invalidPort.Observe([]), "boundary_closed");
}

var presentation = new Dictionary<string, object?>
{
    ["hidden"] = "private-truth", ["controls"] = new Dictionary<string, object?> { ["notebook"] = true, ["manuscript"] = true, ["campus"] = true },
    ["notebook"] = new Dictionary<string, object?> { ["sections"] = new object[] { new Dictionary<string, object?>
        { ["heading"] = "A", ["entries"] = new object[] { new Dictionary<string, object?> { ["label"] = "B", ["card_id"] = "private-card" } } } } },
    ["replay"] = new Dictionary<string, object?> { ["open"] = true, ["vars"] = new object[] { 0, -1, 999 },
        ["questions"] = new object[] { new Dictionary<string, object?> { ["question"] = "A", ["options"] = new object[] { "B", "C" } },
            new Dictionary<string, object?> { ["question"] = "D", ["options"] = new object[] { "E", "F" } } },
        ["result"] = new Dictionary<string, object?> { ["findings"] = new object[] { new Dictionary<string, object?>
            { ["title"] = "G", ["status"] = "match", ["text"] = "private-undrawn-detail", ["card_id"] = "private-card" } } } },
    ["campus"] = new Dictionary<string, object?> { ["known"] = new Dictionary<string, object?> { ["private-known"] = true },
        ["nodes"] = new Dictionary<string, object?> { ["private-known"] = new Dictionary<string, object?> { ["label"] = "A" },
            ["private-unknown"] = new Dictionary<string, object?> { ["label"] = "private-place" } } }
};
var panel = PlayerPanels.Project(presentation, "notebook", "run");
port = Port(); observation = port.Observe([Event("player_panel_view", ("rows", panel.Text))], panel.Offers);
Check("notebook_public_projection", observation.Text.SequenceEqual(["A", "B"])
    && !JsonSerializer.Serialize(observation).Contains("private-card") && !JsonSerializer.Serialize(observation).Contains("private-truth"));
submitted.Clear(); port = Port(input => { submitted.Add(input); return []; });
observation = port.Observe([], panel.Offers); port.Submit(new(observation.Candidates.Single(c => c.Kind == "interact").Token));
Check("notebook_card_formal_input", Text(submitted.Single(), "kind") == "card" && Text(submitted.Single(), "card_id") == "private-card");
panel = PlayerPanels.Project(presentation, "manuscript", "run");
Check("replay_run_requires_visible_fields", !panel.Offers.Any(o => o.Kind == "form_submit"));
Check("replay_no_hidden_variable_or_undrawn_text", !panel.Text.Contains("private-undrawn-detail")
    && panel.Offers.Where(o => o.Kind == "form_field").All(o => ((object?[])o.Input["vars"]!).Length == 2));
submitted.Clear(); port = Port(input => { submitted.Add(input); return []; });
observation = port.Observe([], panel.Offers); port.Submit(new(observation.Candidates.First(c => c.Label == "D F").Token));
Check("replay_variable_formal_input", Text(submitted.Single(), "kind") == "replay_vars"
    && ((object?[])submitted.Single()["vars"]!).SequenceEqual(new object?[] { 0, 1 }));
((Dictionary<string, object?>)presentation["replay"]!)["vars"] = new object[] { 0, 1, 999 };
panel = PlayerPanels.Project(presentation, "manuscript", "run");
submitted.Clear(); port = Port(input => { submitted.Add(input); return []; });
observation = port.Observe([], panel.Offers); port.Submit(new(observation.Candidates.Single(c => c.Kind == "form_submit").Token));
Check("replay_run_current_visible_values_no_skip", Text(submitted.Single(), "kind") == "replay" && !Flag(submitted.Single(), "skip")
    && ((object?[])submitted.Single()["vars"]!).SequenceEqual(new object?[] { 0, 1 }));
panel = PlayerPanels.Project(presentation, "campus", "run");
Check("map_only_known_nodes_offered", panel.Offers.Count(o => o.Kind == "move") == 1 && !panel.Offers.Any(o => o.Label == "private-place"));
submitted.Clear(); port = Port(input => { submitted.Add(input); return []; });
observation = port.Observe([], panel.Offers); port.Submit(new(observation.Candidates.Single(c => c.Kind == "move").Token));
Check("map_travel_formal_input", Text(submitted.Single(), "kind") == "travel" && Text(submitted.Single(), "node_id") == "private-known");
Reject("unavailable_panel_rejected", () => PlayerPanels.Project(presentation, "unsupported", "run"), "panel_unavailable");

var cognitive = Cognition(0);
var snapshotBefore = JsonSerializer.Serialize(cognitive.Snapshot);
port = Port(); observation = port.Observe([Menu()]);
var player = new SubjectivePlayerController(Declaration(), 17);
Check("snapshot_assessment_selects_current_token", player.Decide(observation, cognitive).Token == observation.Candidates.Single().Token);
Check("assessment_leaves_cognition_unchanged", snapshotBefore == JsonSerializer.Serialize(cognitive.Snapshot));
var traceDeclaration = Declaration(new ControllerRuleDefinition { Mode = "prefer", Kind = "menu", Weight = 2 },
    new ControllerRuleDefinition { Mode = "prefer", Kind = "untried", Weight = 6 },
    new ControllerRuleDefinition { Mode = "prefer", Kind = "new", Weight = 8 });
var tracePlayer = new SubjectivePlayerController(traceDeclaration, 17);
var firstTrace = tracePlayer.Decide(new(1, [], [], [new("first", "A", "menu", false)], false), cognitive);
Check("decision_trace_explains_matched_preferences", firstTrace.Score == 8
    && firstTrace.Basis.Contains("authored_rule:prefer:menu:2")
    && firstTrace.Basis.Contains("authored_rule:prefer:untried:6")
    && !firstTrace.Basis.Contains("authored_rule:prefer:new:8"));
Check("alternative_trace_explains_full_public_score", firstTrace.Alternatives.Single() is { Score: 8 } scoredAlternative
    && scoredAlternative.Basis.Contains("authored_rule:prefer:menu:2")
    && scoredAlternative.Basis.Contains("authored_rule:prefer:untried:6"));
var repeatedTrace = tracePlayer.Decide(new(2, [], [], [new("next", "A", "menu", false)], false), cognitive);
Check("decision_trace_uses_history_before_choice", repeatedTrace.Score == 2
    && repeatedTrace.Basis.Contains("authored_rule:prefer:menu:2")
    && !repeatedTrace.Basis.Contains("authored_rule:prefer:untried:6"));
PublicPlayerCandidate Move(string token, double x, double y, string purpose = "") =>
    new(token, $"{x},{y}", "move", false, Purpose: purpose) { Location = new(x, y) };
PublicPlayerCandidate Approach(string token, string destination, double x, double y) =>
    new(token, $"{destination} {x},{y}", "move", false, Value: destination, Purpose: "approach") { Location = new(x, y) };
var movementPlayer = new SubjectivePlayerController(Declaration(), 17);
movementPlayer.Decide(new(1, [], [], [Move("ordinary", 16, 16)], false), cognitive);
var firstFrontier = movementPlayer.Decide(new(2, [], [],
    [Move("ordinary-again", 16, 16), Move("frontier", 16, 16, "frontier")], false), cognitive);
Check("ordinary_move_does_not_consume_same_point_frontier", firstFrontier.Token == "frontier"
    && firstFrontier.Score == 12 && firstFrontier.Basis.Contains("edge_of_unseen")
    && firstFrontier.Basis.Contains("authored_rule:prefer:untried:6")
    && !firstFrontier.Basis.Any(b => b.StartsWith("revisit_without_new_accepted_material:")));
var nextFrontier = movementPlayer.Decide(new(3, [], [],
    [Move("frontier-again", 16, 16, "frontier"), Move("adjacent-frontier", 48, 16, "frontier")], false), cognitive);
Check("adjacent_32_pixel_frontiers_keep_distinct_attempt_history", nextFrontier.Token == "adjacent-frontier"
    && nextFrontier.Score == 12 && nextFrontier.Basis.Contains("edge_of_unseen")
    && nextFrontier.Basis.Contains("authored_rule:prefer:untried:6")
    && !nextFrontier.Basis.Any(b => b.StartsWith("revisit_without_new_accepted_material:")));
var repeatFrontier = movementPlayer.Decide(new(4, [], [], [Move("same-frontier", 48, 16, "frontier")], false), cognitive);
Check("same_frontier_still_consumes_novelty", repeatFrontier.Score == 0
    && !repeatFrontier.Basis.Contains("edge_of_unseen")
    && !repeatFrontier.Basis.Contains("authored_rule:prefer:untried:6"));
movementPlayer = new SubjectivePlayerController(Declaration(), 17);
movementPlayer.Decide(new(1, [], [], [Move("frontier-first", 16, 16, "frontier")], false), cognitive);
var ordinaryAfterFrontier = movementPlayer.Decide(new(2, [], [], [Move("ordinary-after-frontier", 16, 16)], false), cognitive);
Check("frontier_does_not_consume_ordinary_move_history", ordinaryAfterFrontier.Score == 6
    && ordinaryAfterFrontier.Basis.Contains("authored_rule:prefer:untried:6"));
player = new(Declaration(new ControllerRuleDefinition { Mode = "forbid", Kind = "menu", Weight = 1 }), 17);
Check("subjective_prohibition_reports_stuck", player.Decide(observation, cognitive).Reason == "agent_cannot_assess");
player = new(Declaration(new ControllerRuleDefinition { Mode = "forbid", Kind = "menu", Weight = 1 }, new ControllerRuleDefinition { Mode = "permit", Kind = "menu", Weight = 1 }), 17);
Check("subjective_permission_releases_ban", player.Decide(observation, cognitive).Token is not null);
Check("no_candidates_reports_gap", player.Decide(new(1, [], [], [], false), cognitive).Reason == "candidate_missing");
var agentScript = JsonSerializer.Serialize(new { format = "dual-ontology-world-script/v1", chapters = new[] {
    new { id = "neutral", entities = new[] { new { id = "character.controlled", name = "A", kind = "character", scope = "world" } },
        controllers = new[] { Declaration() } }
} }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
using (var agentSession = new GameSession("{}", "neutral.living", "neutral.campus", controllerScript: agentScript))
{
    Check("agent_save_rejects_unpersisted_controller_state", agentSession.Save() is ContractResult<byte[]>.Failure
        { Error.Code: ContractErrorCode.UnsupportedSavePoint, Error.Path: "host.controller_state" });
    Check("agent_restore_rejects_partial_state_before_publication", agentSession.Restore([]) is ContractResult<bool>.Failure
        { Error.Code: ContractErrorCode.UnsupportedSavePoint, Error.Path: "host.controller_state" }
        && !agentSession.AgentEnabled && agentSession.AgentObservation is null);
}
var subject = cognitive.Snapshot.SubjectWorld;
var goal = new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(cognitive.Actor), new("neutral-goal"), 1);
FrozenProjectionV1 Material(string name, ProjectionKind kind, FrozenEffectV1? procedure) => new(new(subject, Guid.NewGuid()), name, kind, Truth.True, null,
    ImmutableDictionary<Tag, int>.Empty, [], [], procedure is null ? [] : [new(new FrozenConditionV1.Constant(Truth.True), RuleMode.Infer, procedure, 1, Truth.True)],
    null, [new(new("neutral:" + name), name, name)], null);
var claim = Material("claim", ProjectionKind.Concept, new FrozenEffectV1.Think(goal));
var good = Material("good", ProjectionKind.Object, new FrozenEffectV1.Think(goal));
var bad = Material("bad", ProjectionKind.Object, new FrozenEffectV1.Think(new FrozenConditionV1.Not(goal)));
var argumentCognition = cognitive with { Snapshot = cognitive.Snapshot with { Projections = cognitive.Snapshot.Projections.AddRange(ImmutableArray.Create(claim, good, bad)) } };
var deepDeclaration = Declaration(); deepDeclaration.Reasoning = new() { Depth = 8, Expansions = 4096, Hypotheses = 128, ArgumentWeight = 16, InformationWeight = 8 };
player = new(deepDeclaration, 17);
player.Decide(new(1, [], [], [new("claim-token", "claim", "form_field", false, "first", "claim")], false), argumentCognition);
var argumentChoice = player.Decide(new(2, [], [], [new("bad-token", "bad", "form_field", false, "second", "bad"), new("good-token", "good", "form_field", false, "second", "good")], false), argumentCognition);
Check("argument_uses_chosen_statement_and_presented_evidence", argumentChoice.Token == "good-token");
Check("argument_trace_is_public_and_has_acquired_basis", argumentChoice.Basis.Contains("good") && argumentChoice.ReasoningExpansions > 0
    && !JsonSerializer.Serialize(argumentChoice).Contains(subject.ToString()));
var tabDeclaration = Declaration(new ControllerRuleDefinition { Mode = "prefer", Kind = "menu", Weight = 100 }); tabDeclaration.Reasoning = deepDeclaration.Reasoning;
player = new(tabDeclaration, 17);
player.Decide(new(1, [], [], [new("statement", "claim", "form_field", false, "first", "claim")], false), argumentCognition);
var switched = player.Decide(new(2, [], [], [new("statement-edit", "claim", "form_field", false, "first", "claim"), new("tab", "other page", "menu", false)], false), argumentCognition);
var afterTab = player.Decide(new(3, [], [], [new("bad", "bad", "form_field", false, "second", "bad"), new("good", "good", "form_field", false, "second", "good")], false), argumentCognition);
Check("argument_survives_visible_form_page_switch", switched.Token == "tab" && afterTab.Token == "good" && afterTab.Basis.Contains("good"));
var revisedGoal = new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(cognitive.Actor), new("revised-goal"), 1);
var revisedClaim = Material("revised-claim", ProjectionKind.Concept, new FrozenEffectV1.Think(revisedGoal));
var revisedGood = Material("revised-good", ProjectionKind.Object, new FrozenEffectV1.Think(revisedGoal));
var revisedBad = Material("revised-bad", ProjectionKind.Object, new FrozenEffectV1.Think(new FrozenConditionV1.Not(revisedGoal)));
var editedCognition = argumentCognition with { Snapshot = argumentCognition.Snapshot with {
    Projections = argumentCognition.Snapshot.Projections.AddRange(ImmutableArray.Create(revisedClaim, revisedGood, revisedBad)) } };
player = new(deepDeclaration, 17);
player.Decide(new(1, [], [], [new("statement", "claim", "form_field", false, "first", "claim")], false), editedCognition);
player.Decide(new(2, [], [], [new("edit", "revised-claim", "form_field", false, "first", "revised-claim")], false), editedCognition);
var afterEdit = player.Decide(new(3, [], [], [new("bad", "revised-bad", "form_field", false, "second", "revised-bad"), new("good", "revised-good", "form_field", false, "second", "revised-good")], false), editedCognition);
Check("argument_tracks_reselected_statement", afterEdit.Token == "good" && afterEdit.Basis.Contains("revised-good"));
var target = Material("target", ProjectionKind.Object, null);
var missing = new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(target.Id), new("neutral-unknown"), 1);
var law = Material("law", ProjectionKind.Procedure, new FrozenEffectV1.When(missing, new FrozenEffectV1.Think(new FrozenConditionV1.Exists(new FrozenNounV1.Reference(cognitive.Actor)))));
var inquiryCognition = cognitive with { Snapshot = cognitive.Snapshot with { Projections = cognitive.Snapshot.Projections.AddRange(ImmutableArray.Create(target, law)) } };
player = new(deepDeclaration, 17);
var inquiryChoice = player.Decide(new(1, [], [], [new("other-token", "other", "interact", false), new("target-token", "target", "interact", false)], false), inquiryCognition);
Check("investigation_values_unknown_acquired_material", inquiryChoice.Token == "target-token" && inquiryChoice.Basis.Any(b => b.StartsWith("seek_unknown_acquired_proposition:")));
PlayerCognition NeutralRole(string role)
{
    var runtime = new WorldRuntime();
    using var simulation = new SimulationSession(runtime);
    var world = new CoreScriptWorld(runtime, simulation);
    var room = world.DefineSpace("neutral.movement.room", "neutral-room");
    var actor = world.DefineCharacter("neutral.movement." + role, role, room);
    return new(((ContractResult<CognitionSnapshotV1>.Success)actor.Soul.World.CaptureSnapshot()).Value, actor.Self.SubjectId!.Value);
}
ActorControllerDefinition NeutralDeclaration(string role, int curiosity, bool interactionPreference = false)
{
    var declaration = Declaration(new ControllerRuleDefinition { Mode = "prefer", Kind = curiosity > 0 ? "untried" : "menu", Weight = curiosity });
    declaration.Actor = "world:character." + role;
    declaration.Name = role;
    if (interactionPreference) declaration.Rules.Add(new() { Mode = "prefer", Kind = "interact", Weight = 3 });
    declaration.Reasoning = new() { Depth = 8, Expansions = 4096, Hypotheses = 128, ArgumentWeight = 16, InformationWeight = 8 };
    return declaration;
}
PublicPlayerObservation At(long turn, string space, double x, double y, params PublicPlayerCandidate[] candidates)
    => new(turn, [], [], [..candidates], false) { Field = new(space, new(x, y)) };
PlayerCognition WithAcceptedNeutralMaterial(PlayerCognition held, string name)
{
    var material = new FrozenProjectionV1(new(held.Snapshot.SubjectWorld, Guid.NewGuid()), name, ProjectionKind.Concept,
        Truth.True, null, ImmutableDictionary<Tag, int>.Empty, [], [],
        [new(new FrozenConditionV1.Constant(Truth.True), RuleMode.Infer,
            new FrozenEffectV1.Think(new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(held.Actor), new("neutral-learned"), 1)),
            1, Truth.True)], null, [new(new("acquired.neutral." + name), name, name)], null);
    return held with { Snapshot = held.Snapshot with { Projections = held.Snapshot.Projections.Add(material) } };
}
foreach (var (role, curiosity) in new[] { ("neutral-observer", 6), ("neutral-feedback-only", 0) })
{
    var roleCognition = NeutralRole(role);
    var roleDeclaration = NeutralDeclaration(role, curiosity, interactionPreference: true);
    var rolePlayer = new SubjectivePlayerController(roleDeclaration, 17);
    rolePlayer.Decide(At(1, "opaque-room-a", 16, 16, Move("blocked-first", 144, 16)), roleCognition);
    var alternative = rolePlayer.Decide(At(2, "opaque-room-a", 16, 16,
        Move("blocked-repeat", 144, 16), Move("viable-alternative", 16, 144)), roleCognition);
    Check(role + "_failed_move_selects_viable_alternative", alternative.Token == "viable-alternative");
    var learnedCognition = WithAcceptedNeutralMaterial(roleCognition, role + "-material");
    var rememberedWall = rolePlayer.Decide(At(3, "opaque-room-a", 16, 144,
        Move("wall-after-learning", 144, 16)), learnedCognition);
    Check(role + "_knowledge_update_preserves_observed_wall", rememberedWall.Basis.Any(b => b.StartsWith("observed_movement_stalled:")));

    rolePlayer = new(roleDeclaration, 17);
    rolePlayer.Decide(At(1, "opaque-room-a", 16, 16, Move("first-space-request", 272, 16)), roleCognition);
    rolePlayer.Decide(At(2, "opaque-room-a", 272, 16, Move("arrived-space-request", 272, 16)), roleCognition);
    rolePlayer.Decide(At(3, "opaque-room-a", 272, 16, Move("stalled-first-space", 272, 16)), roleCognition);
    var otherSpace = rolePlayer.Decide(At(4, "opaque-room-b", 16, 16, Move("same-coordinates-new-space", 272, 16)), roleCognition);
    Check(role + "_same_coordinates_in_other_space_have_no_stall_history",
        !otherSpace.Basis.Any(b => b.StartsWith("observed_movement_stalled:") || b.StartsWith("revisit_without_new_accepted_material:")));
    if (curiosity > 0)
    {
        Check(role + "_other_space_does_not_inherit_walked_ground", otherSpace.Basis.Any(b => b.StartsWith("unwalked_ground:"))
            && otherSpace.Basis.Contains("authored_rule:prefer:untried:6"));
        rolePlayer = new(roleDeclaration, 17);
        rolePlayer.Decide(At(1, "opaque-room-a", 16, 16, Move("unreached-request", 144, 16)), roleCognition);
        var beyondRequest = rolePlayer.Decide(At(2, "opaque-room-a", 16, 16,
            Move("beyond-unreached-request", 208, 16), Move("other-ground", 16, 144)), roleCognition);
        Check(role + "_requested_destination_is_not_walked_ground", beyondRequest.Token == "beyond-unreached-request");

        rolePlayer = new(roleDeclaration, 17);
        rolePlayer.Decide(At(1, "opaque-room-a", 16, 16, new PublicPlayerCandidate("interaction-first", "neutral-panel", "interact", false)), roleCognition);
        var exhausted = rolePlayer.Decide(At(2, "opaque-room-a", 16, 16,
            new("interaction-repeat", "neutral-panel", "interact", false), new("other-menu", "neutral-other-menu", "menu", false)), roleCognition);
        Check(role + "_interaction_is_deferred_without_new_material", exhausted.Token == "other-menu");
        var reconsidered = rolePlayer.Decide(At(3, "opaque-room-a", 16, 16,
            new("interaction-after-learning", "neutral-panel", "interact", false), new("other-menu-repeat", "neutral-other-menu", "menu", false)), learnedCognition);
        var reconsideredAlternative = reconsidered.Alternatives.Single(a => a.Token == "interaction-after-learning");
        Check(role + "_unrelated_accepted_material_does_not_reopen_interaction", !reconsideredAlternative.Basis.Contains("authored_rule:prefer:untried:6")
            && reconsideredAlternative.Basis.Any(b => b.StartsWith("revisit_without_new_accepted_material:")));
    }

    var directionGoal = new FrozenConditionV1.Tagged(new FrozenNounV1.Reference(roleCognition.Actor), new("neutral-direction-goal"), 1);
    FrozenProjectionV1 DirectionMaterial(string name, Tag tag) => new(new(roleCognition.Snapshot.SubjectWorld, Guid.NewGuid()), name,
        ProjectionKind.Concept, Truth.True, null, ImmutableDictionary<Tag, int>.Empty.Add(tag, 1), [], [],
        [new(new FrozenConditionV1.Constant(Truth.True), RuleMode.Infer, new FrozenEffectV1.Think(directionGoal), 1, Truth.Unknown)],
        null, [new(new("acquired.neutral." + name), name, name)], null);
    var directionCognition = roleCognition with { Snapshot = roleCognition.Snapshot with { Projections = roleCognition.Snapshot.Projections.AddRange(ImmutableArray.Create(
        DirectionMaterial("neutral-question", AcquiredPropositions.Question), DirectionMaterial("neutral-bench", AcquiredPropositions.Opportunity))) } };
    foreach (var reverse in new[] { false, true })
    {
        var directionalPlayer = new SubjectivePlayerController(NeutralDeclaration(role, 0), 23);
        directionalPlayer.Decide(At(1, "opaque-direction-space", 16, 16,
            new PublicPlayerCandidate("seen-bench", "neutral-bench", "interact", false) { Location = new(1024, 16) }), directionCognition);
        var near = Move("near-distant-goal", 512, 16); var far = Move("far-distant-goal", 128, 16);
        var directed = directionalPlayer.Decide(At(2, "opaque-direction-space", 16, 16,
            reverse ? near : far, reverse ? far : near), directionCognition);
        Console.WriteLine("OBSERVATION " + role + " distant-goal " + JsonSerializer.Serialize(directed));
        Check(role + "_distant_goal_keeps_directional_order_" + reverse, directed.Token == "near-distant-goal"
            && directed.Basis.Contains("approach_observed_question_material"));
        var unrelatedSpace = directionalPlayer.Decide(At(3, "opaque-direction-other", 16, 16,
            Move("same-point-without-local-lead", 512, 16)), directionCognition);
        Check(role + "_other_space_does_not_inherit_observed_lead_" + reverse, unrelatedSpace.Score == 0
            && !unrelatedSpace.Basis.Any(b => b.StartsWith("approach_observed_")));
    }

    var uDeclaration = NeutralDeclaration(role, curiosity, interactionPreference: true);
    uDeclaration.Rules.Add(new() { Mode = "prefer", Kind = "menu", LabelContains = "neutral-depart", Weight = 100 });
    var uPlayer = new SubjectivePlayerController(uDeclaration, 23);
    var uContact = uPlayer.Decide(At(1, "opaque-u-space", 1024, 16,
        new PublicPlayerCandidate("u-seen-place", "neutral-bench", "interact", false) { Location = new(1024, 16) }), directionCognition);
    Check(role + "_u_route_fixture_completes_prior_examination", uPlayer.CompleteInteraction(uContact.Token!, true));
    var uDeparture = uPlayer.Decide(At(2, "opaque-u-space", 1024, 16,
        new("u-untried-inspect", "neutral-inspect", "menu", false, Purpose: "inspect"),
        new("u-close", "neutral-depart", "menu", false, Purpose: "close")), directionCognition);
    Check(role + "_u_route_remembers_visible_untried_method_after_departure", uDeparture.Token == "u-close");
    var uChoice = uPlayer.Decide(At(3, "opaque-u-space", 16, 16,
        Approach("u-relevant-route", "neutral-bench", 16, 144),
        Approach("u-unrelated-route", "neutral-unused-place", 16, 144),
        Move("u-straight-direction", 48, 16)), directionCognition);
    Console.WriteLine("OBSERVATION " + role + " U-route " + JsonSerializer.Serialize(uChoice));
    Check(role + "_known_question_selects_initially_retreating_route", uChoice.Token == "u-relevant-route"
        && uChoice.Basis.Contains("follow_observed_route_to_needed_place")
        && uChoice.Basis.Any(b => b.StartsWith("expected_information_at_remembered_goal:"))
        && !uChoice.Basis.Any(b => b.StartsWith("seek_current_question_material:")
            || b.StartsWith("seek_unknown_acquired_proposition:") || b.StartsWith("inspect_missing_material:")
            || b.StartsWith("unwalked_ground:") || b.StartsWith("authored_rule:prefer:untried:")
            || b == "approach_observed_question_material"));

    var routeProof = new FrozenProjectionV1(new(roleCognition.Snapshot.SubjectWorld, Guid.NewGuid()), "neutral-route-proof",
        ProjectionKind.Concept, Truth.True, null, ImmutableDictionary<Tag, int>.Empty, [], [],
        [new(new FrozenConditionV1.Constant(Truth.True), RuleMode.Infer, new FrozenEffectV1.Think(directionGoal), 1, Truth.True)],
        null, [new(new("acquired.neutral.route-proof"), "neutral-route-proof", "neutral-route-proof")], null);
    var returningCognition = directionCognition with { Snapshot = directionCognition.Snapshot with {
        Projections = directionCognition.Snapshot.Projections.Add(routeProof) } };
    var returnPlayer = new SubjectivePlayerController(roleDeclaration, 23);
    var returnContact = returnPlayer.Decide(At(1, "opaque-return-space", 1024, 16,
        new PublicPlayerCandidate("return-seen-place", "neutral-bench", "interact", false) { Location = new(1024, 16) }), returningCognition);
    Check(role + "_return_fixture_completes_prior_examination", returnPlayer.CompleteInteraction(returnContact.Token!, true));
    returnPlayer.Decide(At(2, "opaque-return-space", 16, 16, Approach("return-first", "neutral-bench", 16, 144)), returningCognition);
    var earlyReturn = returnPlayer.Decide(At(3, "opaque-return-space", 17, 16,
        Approach("return-early", "neutral-bench", 16, 144), new("return-wait-early", "neutral-wait", "wait", false)), returningCognition);
    for (var turn = 4; turn <= 28; turn++)
        returnPlayer.Decide(At(turn, "opaque-return-space", turn % 2 == 0 ? 16 : 17, 16,
            Approach("return-loop-" + turn, "neutral-bench", 16, 144)), returningCognition);
    var lateReturn = returnPlayer.Decide(At(29, "opaque-return-space", 17, 16,
        Approach("return-late", "neutral-bench", 16, 144), new("return-wait-late", "neutral-wait", "wait", false)), returningCognition);
    Console.WriteLine("OBSERVATION " + role + " necessary-return " + JsonSerializer.Serialize(new { earlyReturn, lateReturn }));
    Check(role + "_necessary_return_cost_is_bounded", earlyReturn.Token == "return-early" && lateReturn.Token == "return-late"
        && earlyReturn.Score == lateReturn.Score
        && lateReturn.Basis.Contains("follow_observed_route_to_needed_place")
        && !lateReturn.Basis.Any(b => b.StartsWith("observed_movement_stalled:")));
    if (curiosity > 0)
        Check(role + "_bounded_return_still_retains_visit_history", earlyReturn.Basis.Contains("revisit_without_new_accepted_material:1")
            && lateReturn.Basis.Contains("revisit_without_new_accepted_material:27")
            && !lateReturn.Basis.Contains("authored_rule:prefer:untried:6"));

    var destinationPlayer = new SubjectivePlayerController(roleDeclaration, 23);
    destinationPlayer.Decide(At(1, "opaque-destination-space", 16, 16,
        Approach("destination-a-first", "neutral-route-a", 16, 144)), roleCognition);
    var distinctDestination = destinationPlayer.Decide(At(2, "opaque-destination-space", 16, 16,
        Approach("destination-b-same-waypoint", "neutral-route-b", 16, 144)), roleCognition);
    Check(role + "_shared_waypoint_different_destination_has_distinct_retry_identity",
        !distinctDestination.Basis.Any(b => b.StartsWith("observed_movement_stalled:") || b.StartsWith("revisit_without_new_accepted_material:")));
    var sameDestination = destinationPlayer.Decide(At(3, "opaque-destination-space", 16, 16,
        Approach("destination-a-after-learning", "neutral-route-a", 16, 144)), WithAcceptedNeutralMaterial(roleCognition, "neutral-retry-memory"));
    Check(role + "_new_knowledge_preserves_route_specific_collision_memory",
        sameDestination.Basis.Contains("observed_movement_stalled:1"));
}
RouteLearningCases.Run(Check);
var invalidBudget = Declaration(); invalidBudget.Reasoning.Depth = 65;
try { PlayerControllers.Validate(invalidBudget); throw new Exception("invalid depth accepted"); }
catch (InvalidDataException) { Check("invalid_role_reasoning_budget_rejected", true); }

foreach (var nested in new[] { false, true })
{
    var first = new SubjectivePlayerController(Declaration(), 23);
    var second = new SubjectivePlayerController(Declaration(), 23);
    var a = Cognition(0, nested); var b = Cognition(91, nested);
    var same = true;
    for (var step = 0; step < 12; step++)
    {
        var offers = new PublicPlayerObservation(step, [], [], [new("t0:" + step, "A", "menu", false), new("t1:" + step, "B", "menu", false)], false);
        same &= first.Decide(offers, a).Token == second.Decide(offers, b).Token;
    }
    Check("hidden_truth_metamorphism_12_steps_" + nested, same);
}
Console.WriteLine($"{passed} checks passed");
