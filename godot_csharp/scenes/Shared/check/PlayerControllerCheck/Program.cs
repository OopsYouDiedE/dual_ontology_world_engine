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
        new Dictionary<string, object?> { ["id"] = "private-second", ["kind"] = "choice", ["heading"] = "second-heading", ["options"] = new object[] { Option("B1", "second-option") } } }),
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
var repeatedTrace = tracePlayer.Decide(new(2, [], [], [new("next", "A", "menu", false)], false), cognitive);
Check("decision_trace_uses_history_before_choice", repeatedTrace.Score == 2
    && repeatedTrace.Basis.Contains("authored_rule:prefer:menu:2")
    && !repeatedTrace.Basis.Contains("authored_rule:prefer:untried:6"));
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
