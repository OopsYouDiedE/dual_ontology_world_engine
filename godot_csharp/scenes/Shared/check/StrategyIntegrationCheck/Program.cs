using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using DualOntologyWorldEngine.Awakening;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

// Mechanism checks may inject neutral state. They are not UI or playtest evidence.
// Chapter-specific identities and content are read from the formal embedded story outputs.
var passed = 0;
var failed = 0;
void Check(string name, bool okay)
{
    Console.WriteLine((okay ? "PASS " : "FAIL ") + name);
    if (okay) passed++; else failed++;
}
void Case(string name, Action body)
{
    try { body(); }
    catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error); }
}
void Reject(string name, Action action)
{
    try { action(); Check(name, false); }
    catch (ArgumentException) { Check(name, true); }
}
object? Invoke(object target, string method, params object?[] args)
    => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
void Drain(object? sequence) { foreach (var _ in (IEnumerable)sequence!) { } }
void Testimony(AwakeningContentSession content, string id, string actor)
    => content.Session.Set("testimony", id, new Dictionary<string, object>
        { ["speaker"] = actor, ["text"] = content.Content.RootElement.GetProperty("EVIDENCE").GetProperty(id).GetProperty("facts").GetString()! });
IEnumerable<JsonElement> Nodes(JsonElement node)
{
    yield return node;
    if (node.TryGetProperty("args", out var args))
        foreach (var item in args.EnumerateArray()) foreach (var descendant in Nodes(item)) yield return descendant;
}
string[] EqualLiterals(JsonElement expression, string query) => Nodes(expression)
    .Where(n => n.GetProperty("op").GetString() == "eq")
    .SelectMany(n => n.GetProperty("args").EnumerateArray().Any(a => a.GetProperty("op").GetString() == "call"
        && a.GetProperty("name").GetString() == query)
        ? n.GetProperty("args").EnumerateArray().Where(a => a.GetProperty("op").GetString() == "literal")
            .Select(a => a.GetProperty("value").GetString()!) : []).ToArray();

Case("public_label_filter", () =>
{
    var runtime = new WorldRuntime();
    using var simulation = new SimulationSession(runtime);
    var neutral = new CoreScriptWorld(runtime, simulation);
    var actor = neutral.DefineCharacter("neutral.actor", "A", neutral.DefineSpace("neutral.room", "R"));
    var snapshot = ((ContractResult<CognitionSnapshotV1>.Success)actor.Soul.World.CaptureSnapshot()).Value;
    var cognition = new PlayerCognition(snapshot, actor.Self.SubjectId!.Value);
    var before = JsonSerializer.Serialize(snapshot);
    ControllerRuleDefinition Rule(string mode, int weight, string label = "neutral-target", string kind = "menu")
        => new() { Mode = mode, Weight = weight, Kind = kind, LabelContains = label };
    SubjectivePlayerController Controller(params ControllerRuleDefinition[] rules) => new(new()
        { Actor = "neutral.actor", Name = "A", Mode = "agent", Rules = [..rules],
            Reasoning = new() { Depth = 0, ArgumentWeight = 0, InformationWeight = 0 } }, 19);
    PublicPlayerObservation Offers(params PublicPlayerCandidate[] candidates) => new(1, [], [], [..candidates], false);
    var match = new PublicPlayerCandidate("match", "prefix neutral-target suffix", "menu", false);
    var other = new PublicPlayerCandidate("other", "neutral-other", "menu", false);
    var observation = Offers(match, other);
    var decision = Controller(Rule("prefer", 9)).Decide(observation, cognition);
    Check("matching_public_substring_receives_authored_score", decision.Token == "match" && decision.Score == 9
        && decision.Basis.Contains("authored_rule:prefer:menu:9:public_label=neutral-target"));
    decision = Controller(Rule("prefer", 9)).Decide(Offers(other), cognition);
    Check("nonmatching_label_keeps_zero_baseline", decision.Token == "other" && decision.Score == 0
        && !decision.Basis.Any(b => b.StartsWith("authored_rule:")));
    decision = Controller(Rule("prefer", 9)).Decide(Offers(other with { Label = "NEUTRAL-TARGET" }), cognition);
    Check("label_match_is_ordinal_case_sensitive", decision.Score == 0);
    decision = Controller(Rule("prefer", 9)).Decide(Offers(match with { Kind = "move" }), cognition);
    Check("matching_label_does_not_bypass_kind_gate", decision.Score == 0);
    decision = Controller(Rule("forbid", 10), Rule("prefer", 1000)).Decide(observation, cognition);
    Check("high_preference_cannot_override_matching_prohibition", decision.Token == "other");
    decision = Controller(Rule("forbid", 10), Rule("permit", 9)).Decide(observation, cognition);
    Check("lower_permission_cannot_override_prohibition", decision.Token == "other");
    decision = Controller(Rule("forbid", 10), Rule("permit", 10, "neutral-other")).Decide(Offers(match), cognition);
    Check("nonmatching_permission_cannot_release_prohibited_choice", decision.Token is null && decision.Reason == "agent_cannot_assess");
    decision = Controller(Rule("forbid", 10), Rule("permit", 10)).Decide(Offers(match), cognition);
    Check("matching_permission_at_boundary_releases_prohibition", decision.Token == "match");
    decision = Controller(Rule("prefer", 7, "")).Decide(Offers(other, match with { Kind = "move" }), cognition);
    Check("legacy_empty_label_rule_keeps_kind_wide_behavior", decision.Token == "other" && decision.Score == 7
        && decision.Basis.Contains("authored_rule:prefer:menu:7"));
    var repeated = Controller(Rule("prefer", 5, kind: "untried"));
    Check("filtered_untried_rule_applies_before_first_choice", repeated.Decide(Offers(match), cognition).Score == 5);
    decision = repeated.Decide(Offers(match with { Token = "again" }), cognition);
    Check("history_changes_score_without_exhausting_method", decision.Token == "again" && decision.Score == 0);
    Check("controller_filter_does_not_mutate_subject_cognition", before == JsonSerializer.Serialize(snapshot));
});

Case("compiled_controller_wire", () =>
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (!File.Exists(Path.Combine(directory.FullName, "godot_csharp", "tools", "story_script.py")))
        directory = directory.Parent ?? throw new DirectoryNotFoundException("Run the check from this repository.");
    var compiler = Path.Combine(directory.FullName, "godot_csharp", "tools", "story_script.py");
    var fixture = Path.Combine(directory.FullName, "godot_csharp", "scenes", "Shared", "check", "StrategyIntegrationCheck", "controller-wire.story");
    var supplied = Array.IndexOf(args, "--controller-wire-json");
    string json;
    if (supplied >= 0)
    {
        json = File.ReadAllText(args[supplied + 1]);
        Console.WriteLine("CONTROLLER_WIRE_SOURCE " + Path.GetFullPath(args[supplied + 1]));
    }
    else
    {
        var start = new System.Diagnostics.ProcessStartInfo("python")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = directory.FullName,
            StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        start.ArgumentList.Add(compiler); start.ArgumentList.Add(fixture);
        using var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("Story compiler did not start.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Neutral story compiler exceeded 30 seconds.");
        }
        json = output.GetAwaiter().GetResult();
        var stderr = errors.GetAwaiter().GetResult();
        if (process.ExitCode != 0) throw new InvalidDataException("Story compiler failed: " + stderr);
        Console.WriteLine("CONTROLLER_WIRE_SOURCE real_python_story_compiler");
    }
    var declaration = PlayerControllers.Read(json).Single();
    Check("compiled_wire_keeps_actor_identity", declaration.Actor == "world:neutral.actor"
        && declaration.Name == "NeutralActor" && declaration.Mode == "agent");
    Check("compiled_wire_keeps_all_public_label_filters", declaration.Rules.Select(r => r.LabelContains)
        .SequenceEqual(["neutral-target", "neutral-veto", "neutral-veto", "neutral-low", "neutral-release"]));
    Check("compiled_wire_keeps_rule_modes_kinds_and_weights", declaration.Rules.Select(r => (r.Mode, r.Kind, r.Weight))
        .SequenceEqual([("prefer", "menu", 17), ("forbid", "menu", 10), ("prefer", "menu", 1000),
            ("permit", "menu", 9), ("permit", "menu", 10)]));
    Check("compiled_wire_keeps_depth_budget", declaration.Reasoning.Depth == 4);
    Check("compiled_wire_keeps_expansion_budget", declaration.Reasoning.Expansions == 512);
    Check("compiled_wire_keeps_hypothesis_budget", declaration.Reasoning.Hypotheses == 3);
    Check("compiled_wire_keeps_argument_weight", declaration.Reasoning.ArgumentWeight == 13);
    Check("compiled_wire_keeps_information_weight", declaration.Reasoning.InformationWeight == 7);

    var runtime = new WorldRuntime();
    using var simulation = new SimulationSession(runtime);
    var world = new CoreScriptWorld(runtime, simulation);
    var actor = world.DefineCharacter(declaration.Actor, declaration.Name, world.DefineSpace("neutral.room", "NeutralRoom"));
    PlayerCognition Capture() => new(((ContractResult<CognitionSnapshotV1>.Success)actor.Soul.World.CaptureSnapshot()).Value,
        actor.Self.SubjectId!.Value);
    PublicPlayerObservation Offers(params PublicPlayerCandidate[] candidates) => new(1, [], [], [..candidates], false);
    PlayerDecision Choose(params PublicPlayerCandidate[] candidates)
        => new SubjectivePlayerController(declaration, 19).Decide(Offers(candidates), Capture());
    var target = new PublicPlayerCandidate("match", "prefix neutral-target suffix", "menu", false);
    var other = new PublicPlayerCandidate("other", "neutral-other", "menu", false);
    var decision = Choose(target, other);
    Check("compiled_wire_matching_label_receives_only_authored_preference", decision.Token == "match" && decision.Score == 17
        && decision.Basis.Contains("authored_rule:prefer:menu:17:public_label=neutral-target"));
    decision = Choose(other);
    Check("compiled_wire_nonmatching_label_keeps_zero_baseline", decision.Token == "other" && decision.Score == 0
        && !decision.Basis.Any(b => b.StartsWith("authored_rule:")));
    decision = Choose(other with { Label = "NEUTRAL-TARGET" });
    Check("compiled_wire_filter_retains_case_sensitive_matching", decision.Token == "other" && decision.Score == 0);
    decision = Choose(target with { Kind = "move" });
    Check("compiled_wire_label_filter_does_not_bypass_kind", decision.Token == "match" && decision.Score == 0);
    var veto = target with { Token = "veto", Label = "neutral-veto" };
    decision = Choose(veto, other);
    Check("compiled_wire_preference_does_not_override_forbid", decision.Token == "other" && decision.Score == 0);
    decision = Choose(veto);
    Check("compiled_wire_nonmatching_permission_cannot_release_forbid", decision.Token is null && decision.Reason == "agent_cannot_assess");
    decision = Choose(veto with { Label = "neutral-veto neutral-low" });
    Check("compiled_wire_lower_permission_cannot_release_forbid", decision.Token is null && decision.Reason == "agent_cannot_assess");
    decision = Choose(veto with { Label = "neutral-veto neutral-release" });
    Check("compiled_wire_equal_matching_permission_releases_forbid", decision.Token == "veto" && decision.Score == 1000
        && decision.Basis.Contains("authored_rule:permit:menu:10:public_label=neutral-release"));

    Projection Believe(string name, Condition claim, ProjectionKind kind = ProjectionKind.Concept)
        => Observation.Observe(actor.Soul, new(name, kind)
        {
            Source = ObservationSource.Introspection, Existence = Truth.True,
            Rules = [new(new Condition.Constant(Truth.True), RuleMode.Infer, new Effect.Think(claim), 1, Truth.True)]
        });
    var assertion = Believe("neutral-argument", new Condition.Tagged(actor.Self, new("neutral.argument"), 1));
    var before = JsonSerializer.Serialize(Capture().Snapshot);
    decision = Choose(new("argument", assertion.Name, "form_field", false), other with { Kind = "form_field" });
    Check("compiled_wire_argument_weight_changes_real_reasoning_score", decision.Token == "argument" && decision.Score == 13
        && decision.Basis.Contains("subjective_argument_supported:neutral-argument"));
    Check("compiled_wire_reasoning_does_not_mutate_cognition", before == JsonSerializer.Serialize(Capture().Snapshot));

    // The public body label binds two unknown acquired propositions to one inquiry target.
    Observation.Observe(actor.Soul, new("neutral-law", ProjectionKind.Procedure)
    {
        Source = ObservationSource.Introspection, Existence = Truth.True,
        Rules = [new(new Condition.Constant(Truth.True), RuleMode.Infer,
            new Effect.When(new Condition.Tagged(actor.Self, new("neutral.premise"), 1),
                new Effect.Think(new Condition.Tagged(actor.Self, new("neutral.conclusion"), 1))), 1, Truth.True)]
    });
    decision = Choose(new("inquiry", actor.Self.Name, "interact", false), other with { Kind = "interact" });
    Check("compiled_wire_information_weight_changes_real_inquiry_score", decision.Token == "inquiry" && decision.Score == 14
        && decision.Basis.Contains("seek_unknown_acquired_proposition:" + actor.Self.Name));
    Check("compiled_wire_reasoning_respects_expansion_limit", decision.ReasoningExpansions is > 0 and <= 512
        && decision.ReasoningStop == "complete");

    var alternatives = Enumerable.Range(0, 4).Select(i =>
    {
        var material = Believe("neutral-alternative-" + i, new Condition.Tagged(actor.Self, new("neutral.option." + i), 1));
        return new PublicPlayerCandidate("alternative-" + i, material.Name, "form_field", false, "neutral-group", material.Name);
    }).ToArray();
    decision = Choose(alternatives);
    Check("compiled_wire_hypothesis_budget_limits_actual_comparison", decision.HypothesesCompared == 3
        && decision.ReasoningStop == "hypothesis_budget");
});

var root = Chapter1Data.Root;
var track = root.GetProperty("STRATEGY_TRACK");
var people = track.GetProperty("people").EnumerateObject().ToArray();
Check("formal_strategy_track_wrong_scenes_is_array", track.GetProperty("wrong_scenes").ValueKind == JsonValueKind.Array);
Check("formal_strategy_track_world_resolution_scenes_is_array", track.GetProperty("world_resolution_scenes").ValueKind == JsonValueKind.Array);
Check("formal_strategy_track_clue_lists_are_arrays", people.All(p => p.Value.GetProperty("clues").ValueKind == JsonValueKind.Array));
Check("formal_strategy_track_object_permissions_are_arrays", track.GetProperty("object_choices").EnumerateObject().All(p => p.Value.ValueKind == JsonValueKind.Array));

Case("completed_dialogue_strategy_source_binding", () =>
{
    var aliases = track.GetProperty("source_scenes").EnumerateObject().ToArray();
    Check("formal_strategy_source_scene_lists_are_unique_arrays", aliases.Length > 0 && aliases.All(a =>
        a.Value.ValueKind == JsonValueKind.Array && a.Value.GetArrayLength() > 0
        && a.Value.EnumerateArray().All(s => s.ValueKind == JsonValueKind.String)
        && a.Value.EnumerateArray().Select(s => s.GetString()).Distinct(StringComparer.Ordinal).Count() == a.Value.GetArrayLength()));
    using var stream = typeof(MorningWorld).Assembly.GetManifestResourceStream("Awakening.World.json")!;
    using var document = JsonDocument.Parse(stream);
    var declarations = document.RootElement.GetProperty("chapters")[0].GetProperty("entities").EnumerateArray()
        .Where(e => e.GetProperty("kind").GetString() == "character").ToArray();
    var actors = people.Select(p => p.Name).ToArray();
    var profiles = actors.ToDictionary(who => who, who => declarations.Single(e =>
        e.GetProperty("name").GetString() == root.GetProperty("PERSON_NAMES").GetProperty(who).GetString()));
    var strategies = profiles.ToDictionary(p => p.Key, p => StrategyPolicy.Read(p.Value.GetProperty("profile")));
    var player = root.GetProperty("PRESENTATION").GetProperty("player_actor").GetString()!;
    var rooms = root.GetProperty("ROOM_NAMES").EnumerateObject().Select(p => p.Name).ToArray();
    var scenes = aliases.SelectMany(a => a.Value.EnumerateArray().Select(s => s.GetString()!)).Distinct(StringComparer.Ordinal).ToArray();
    Check("source_binding_fixture_has_locality_and_visibility_participants", actors.Length >= 3 && rooms.Length >= 2);
    Check("formal_source_aliases_bind_declared_actor_sources", aliases.All(a => strategies.Values.All(ss => ss.Any(s => s.Source == a.Name))));

    string[] Expected(string who, string scene) => strategies[who].Select(s => s.Source).Distinct(StringComparer.Ordinal)
        .Where(source => source == scene || aliases.Any(a => a.Name == source && a.Value.EnumerateArray().Any(s => s.GetString() == scene))).ToArray();
    ScriptCharacter Actor(Chapter1Session session, string who)
    {
        var id = profiles[who].GetProperty("id").GetString()!;
        return session.World.PersistentSession.Character(id.Contains(':') ? id : "world:" + id);
    }
    void Locate(Chapter1Session session, string who, bool local, bool visible)
    {
        session.ContentSession.Session.Locate(who, rooms[local ? 0 : 1]);
        var fact = Invoke(session, "F", who)!;
        fact.GetType().GetField("Visible")!.SetValue(fact, visible);
    }
    Chapter1Session Create()
    {
        var session = new Chapter1Session();
        session.ContentSession.Session.Locate(player, rooms[0]);
        foreach (var who in actors) Locate(session, who, true, true);
        return session;
    }
    string[] Sources(Chapter1Session session, string who) => session.World.StrategyBasis(who)
        .Where(p => p.Key.StartsWith("strategy-source:", StringComparison.Ordinal) && p.Value == 1)
        .Select(p => p.Key["strategy-source:".Length..]).Order(StringComparer.Ordinal).ToArray();
    int Observations(Chapter1Session session, string who, string source)
    {
        var actor = Actor(session, who);
        return actor.Soul.World.History.Count(h => ReferenceEquals(h.Target, actor.Self)
            && h.Source == ObservationSource.Perception && h.Details == source);
    }
    int CompleteSay(Chapter1Session session, string scene, Action? awaiting = null)
    {
        var lines = 0;
        // Resume formal Say packets in this mechanism fixture; no UI or player timing is claimed.
        void Complete(IEnumerable sequence)
        {
            foreach (var operation in sequence)
            {
                var type = operation!.GetType();
                switch (type.Name)
                {
                    case "Until":
                        if (!((Func<bool>)type.GetProperty("Ready")!.GetValue(operation)!)())
                            throw new InvalidOperationException("Fixture encountered an unresolved dialogue wait.");
                        break;
                    case "Run": Complete((IEnumerable)type.GetProperty("Body")!.GetValue(operation)!); break;
                    case "Await":
                        var packet = (Dictionary<string, object?>)type.GetProperty("Event")!.GetValue(operation)!;
                        if (packet.GetValueOrDefault("kind")?.ToString() != "lines")
                            throw new InvalidOperationException("Fixture encountered a non-dialogue presentation.");
                        lines += ((IEnumerable)packet["lines"]!).Cast<object>().Count();
                        awaiting?.Invoke();
                        var choices = packet.GetValueOrDefault("choices") as IEnumerable;
                        var choice = choices?.Cast<Dictionary<string, object?>>().FirstOrDefault(c => c.GetValueOrDefault("available") is true);
                        if (choices is not null && choice is null) throw new InvalidOperationException("Fixture has no available dialogue reply.");
                        typeof(Chapter1Session).GetField("_reply", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
                            choice is null ? new Dictionary<string, object?>() : new Dictionary<string, object?> { ["choice_id"] = choice["id"] });
                        break;
                    default: throw new InvalidOperationException("Fixture cannot complete operation: " + type.Name);
                }
            }
        }
        Complete((IEnumerable)Invoke(session, "Say", scene)!);
        return lines;
    }

    foreach (var scene in scenes)
    {
        var session = Create();
        var initial = actors.ToDictionary(who => who, who => Sources(session, who));
        Check("mapped_scene_source_is_initially_unacquired_" + scene,
            actors.All(who => Expected(who, scene).All(source => !initial[who].Contains(source, StringComparer.Ordinal))));
        var premature = false;
        var lines = CompleteSay(session, scene, () => premature |= actors.Any(who => !Sources(session, who).SequenceEqual(initial[who])));
        Check("source_scene_produces_actual_dialogue_packets_" + scene, lines > 0);
        Check("source_is_not_acquired_before_dialogue_completion_" + scene, !premature);
        foreach (var who in actors)
        {
            var expected = Expected(who, scene);
            Check("visible_local_actor_acquires_exact_declared_sources_" + scene + "_" + who,
                expected.Length > 0 && Sources(session, who).SequenceEqual(initial[who].Concat(expected).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)));
            Check("source_enters_actor_history_as_single_perception_" + scene + "_" + who,
                expected.All(source => Observations(session, who, source) == 1));
        }
        var before = actors.ToDictionary(who => who, who => Actor(session, who).Soul.World.History.Count);
        foreach (var related in scenes) CompleteSay(session, related);
        Check("repeat_and_alias_scene_do_not_duplicate_source_observations_" + scene,
            actors.All(who => Actor(session, who).Soul.World.History.Count == before[who]
                && Expected(who, scene).All(source => Observations(session, who, source) == 1)));

        for (var active = 0; active < actors.Length; active++)
        {
            session = Create();
            initial = actors.ToDictionary(who => who, who => Sources(session, who));
            for (var index = 0; index < actors.Length; index++)
                Locate(session, actors[index], index != (active + 1) % actors.Length, index == active || index == (active + 1) % actors.Length);
            CompleteSay(session, scene);
            foreach (var who in actors)
            {
                var expected = who == actors[active] ? Expected(who, scene) : [];
                Check("source_respects_rotated_local_visibility_gate_" + scene + "_" + actors[active] + "_" + who,
                    Sources(session, who).SequenceEqual(initial[who].Concat(expected).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
                    && Expected(who, scene).All(source => Observations(session, who, source) == (who == actors[active] ? 1 : 0)));
            }
        }
        session = Create();
        initial = actors.ToDictionary(who => who, who => Sources(session, who));
        var interrupted = ((IEnumerable)Invoke(session, "Say", scene)!).GetEnumerator();
        try
        {
            while (interrupted.MoveNext() && interrupted.Current!.GetType().Name != "Await") { }
            Check("incomplete_source_dialogue_does_not_grant_cognition_" + scene, actors.All(who => Sources(session, who).SequenceEqual(initial[who])));
        }
        finally { (interrupted as IDisposable)?.Dispose(); }
    }
    var unrelated = people.SelectMany(p => p.Value.GetProperty("methods").EnumerateObject().Select(m => m.Value.GetString()!))
        .First(scene => !scenes.Contains(scene, StringComparer.Ordinal) && strategies.Values.All(ss => ss.All(s => s.Source != scene)));
    var unrelatedSession = Create();
    var unrelatedInitial = actors.ToDictionary(who => who, who => Sources(unrelatedSession, who));
    Check("unrelated_authored_scene_produces_dialogue", CompleteSay(unrelatedSession, unrelated) > 0);
    Check("unrelated_scene_does_not_invent_source_cognition", actors.All(who => Sources(unrelatedSession, who).SequenceEqual(unrelatedInitial[who])));
    foreach (var who in actors)
    {
        unrelatedSession.World.ObserveStrategyState(who, strategies[who].SelectMany(s => s.Conditions).GroupBy(c => c.Tag)
            .ToDictionary(g => g.Key, g => g.Max(c => c.Minimum)), "neutral.fixture");
        var methods = people.Single(p => p.Name == who).Value.GetProperty("methods").EnumerateObject().Select(m => m.Name).ToArray();
        Check("unrelated_scene_cannot_activate_even_satisfied_strategy_thresholds_" + who,
            unrelatedSession.World.ChooseStrategyMethod(who, methods) is null);
    }
});

Case("known_material_and_verdict", () =>
{
    var content = new Chapter1Script().CreateContent();
    var variable = root.GetProperty("VARIABLES")[0];
    var id = variable.GetProperty("evidence")[0].GetString()!;
    Check("unknown_material_does_not_grant_seen_or_known", !content.KnownMaterial(id)
        && content.Query(null, "known", [id]) is false && content.Query(null, "seen", [id]) is false);
    Testimony(content, id, people[0].Name);
    Check("testimony_grants_known_without_claiming_direct_inspection", content.KnownMaterial(id)
        && content.Query(null, "known", [id]) is true && content.Query(null, "seen", [id]) is false
        && content.Session.GetProgress("examined").Count == 0);
    Check("testimony_unlocks_authored_statement_options", Enumerable.Range(0, variable.GetProperty("options").GetArrayLength())
        .All(n => content.Statements([], 0, false).Contains("V1_" + n)));
    content.Session.Set("examined", id, true);
    content.Session.Remove("testimony", id);
    Check("direct_inspection_keeps_legacy_known_gate", content.KnownMaterial(id) && content.Query(null, "seen", [id]) is true);
    content.Session.Remove("examined", id);
    Check("removing_last_source_closes_known_gate", !content.KnownMaterial(id));
    var acquaintance = root.GetProperty("ACQUAINTANCE_EVIDENCE").EnumerateArray().Select(v => v.GetString()!).ToArray();
    content = new Chapter1Script().CreateContent();
    Testimony(content, acquaintance[0], people[0].Name);
    Check("one_testimony_does_not_unlock_two_material_threshold", !content.V6Open);
    Testimony(content, acquaintance[1], people[0].Name);
    Check("two_testimonies_unlock_threshold_without_relabeling_as_seen", content.V6Open && content.Session.GetProgress("examined").Count == 0);
    var gated = root.GetProperty("VERDICTS").EnumerateObject().First(v => Nodes(v.Value.GetProperty("show_ok"))
        .Any(n => n.GetProperty("op").GetString() == "call" && n.GetProperty("name").GetString() == "known"));
    var expression = gated.Value.GetProperty("show_ok");
    var required = Nodes(expression).Where(n => n.GetProperty("op").GetString() == "call" && n.GetProperty("name").GetString() == "known")
        .Select(n => n.GetProperty("args")[0].GetProperty("value").GetString()!).Distinct().ToArray();
    var said = EqualLiterals(gated.Value.GetProperty("say_ok"), "said").First();
    var shown = EqualLiterals(expression, "shown").First(required.Contains);
    content = new Chapter1Script().CreateContent();
    var step = int.Parse(gated.Name);
    Check("authored_verdict_rejects_missing_material_gate", content.Judge(step, said, shown) != "answer_ok" + step);
    foreach (var material in required) Testimony(content, material, people[0].Name);
    Check("authored_known_verdict_accepts_testimony_sources", content.Judge(step, said, shown) == "answer_ok" + step
        && content.Session.GetProgress("examined").Count == 0);
    content.Session.GetProgress("testimony").Clear();
    foreach (var material in required) content.Session.Set("examined", material, true);
    Check("authored_known_verdict_preserves_direct_inspection_compatibility", content.Judge(step, said, shown) == "answer_ok" + step);
});

Case("actual_card_choices", () =>
{
    var permissions = track.GetProperty("object_choices").EnumerateObject().ToArray();
    foreach (var permission in permissions)
    {
        var content = new Chapter1Script().CreateContent();
        var world = new Chapter1Script().CreateWorld();
        var interaction = new Chapter1Interaction(content, world);
        var allowed = permission.Value.EnumerateArray().Select(a => a.GetString()!).ToArray();
        var packet = interaction.CardPacket(permission.Name, [], false);
        Check("remote_card_has_no_alteration_choices_" + permission.Name,
            !packet.Choices.Any(c => allowed.Any(a => c.Id.EndsWith(":" + a))));
        packet = interaction.CardPacket(permission.Name, [], true);
        Check("near_card_offers_only_authored_alteration_choices_" + permission.Name,
            allowed.All(a => packet.Choices.Any(c => c.Id == "card:" + permission.Name + ":" + a && c.IsAvailable)));
        foreach (var action in allowed)
        {
            var choice = "card:" + permission.Name + ":" + action;
            interaction.CardPacket(permission.Name, [], true);
            Reject("choice_rechecks_current_remote_position_" + permission.Name + "_" + action, () => interaction.Choose(choice, [], false));
            var result = interaction.Choose(choice, [], true);
            Check("near_choice_issues_only_authored_world_command_" + permission.Name + "_" + action,
                result.WorldCommands.Single().Name == "object_" + action && result.WorldCommands.Single().Arguments.Single()?.ToString() == permission.Name);
        }
        content.Session.Set("hidden", permission.Name, true);
        Check("hidden_object_cannot_be_altered_again_" + permission.Name,
            !interaction.CardPacket(permission.Name, [], true).Choices.Any(c => allowed.Any(a => c.Id.EndsWith(":" + a))));
        content.Session.Remove("hidden", permission.Name);
        content.Session.Set("destroyed", permission.Name, true);
        Check("destroyed_object_cannot_be_altered_again_" + permission.Name,
            !interaction.CardPacket(permission.Name, [], true).Choices.Any(c => allowed.Any(a => c.Id.EndsWith(":" + a))));
    }
    var unpermitted = root.GetProperty("EVIDENCE").EnumerateObject().First(e => !permissions.Any(p => p.Name == e.Name)).Name;
    var unauthorised = new Chapter1Interaction(new Chapter1Script().CreateContent(), new Chapter1Script().CreateWorld());
    var unpermittedCard = unauthorised.CardPacket(unpermitted, [], true);
    Check("near_position_does_not_grant_unauthored_object_actions", !unpermittedCard.Choices.Any(c => c.Id.EndsWith(":hide") || c.Id.EndsWith(":destroy")));
    Reject("invented_hide_choice_is_rejected", () => unauthorised.Choose("card:" + unpermitted + ":hide", [], true));
});

Case("object_execution_gates", () =>
{
    foreach (var action in new[] { "hide", "destroy" })
    {
        var permission = track.GetProperty("object_choices").EnumerateObject().First(p => p.Value.EnumerateArray().Any(a => a.GetString() == action));
        var session = new Chapter1Session();
        var world = session.World;
        var body = world.PersistentSession.Entity("world:object." + permission.Name);
        var originalSpace = body.Get<SpatialProperty>()!.Space;
        var minutes = session.ContentSession.GameMinutes;
        Drain(Invoke(session, "AlterEvidenceObject", permission.Name, action == "destroy"));
        Check("remote_execution_is_noop_" + action, ReferenceEquals(originalSpace, body.Get<SpatialProperty>()!.Space)
            && session.ContentSession.GameMinutes == minutes && !session.ContentSession.Session.Has("hidden", permission.Name)
            && !session.ContentSession.Session.Has("destroyed", permission.Name));
        var actor = world.PersistentSession.Character(session.PlayerActor);
        actor.Body.Get<SpatialProperty>()!.Space = originalSpace;
        world.Examine(Person.Detective, permission.Name);
        using var layoutStream = typeof(Chapter1Data).Assembly.GetManifestResourceStream("Awakening.Layout.json")!;
        using var layout = JsonDocument.Parse(layoutStream);
        var site = layout.RootElement.GetProperty("sites").EnumerateArray().First(s => s.GetProperty("evidence").GetString() == permission.Name).GetProperty("id").GetString()!;
        var fact = Invoke(session, "F", "detective")!;
        ((HashSet<string>)fact.GetType().GetField("Near")!.GetValue(fact)!).Add(site);
        Drain(Invoke(session, "AlterEvidenceObject", permission.Name, action == "destroy"));
        Check("near_authored_execution_changes_world_" + action, action == "hide"
            ? ReferenceEquals(body.Get<SpatialProperty>()!.Holder, actor.Body) && body.Get<SpatialProperty>()!.Space is null
                && session.ContentSession.Session.Has("hidden", permission.Name)
            : world.IsDestroyed(permission.Name) && body.Get<SpatialProperty>()!.Space is null && session.ContentSession.Session.Has("destroyed", permission.Name));
    }
});

Case("subject_strategy_isolation_and_reuse", () =>
{
    var session = new Chapter1Session();
    var world = session.World;
    var who = people[0].Name;
    var other = people[1].Name;
    using var stream = typeof(MorningWorld).Assembly.GetManifestResourceStream("Awakening.World.json")!;
    using var doc = JsonDocument.Parse(stream);
    var person = (Person)Array.IndexOf(Names.PersonKeys, who);
    var declaration = doc.RootElement.GetProperty("chapters")[0].GetProperty("entities").EnumerateArray()
        .First(e => e.GetProperty("kind").GetString() == "character" && e.GetProperty("name").GetString() == Names.Of(person));
    var profile = declaration.GetProperty("profile");
    var methods = people[0].Value.GetProperty("methods").EnumerateObject().Select(m => m.Name).ToArray();
    var strategies = StrategyPolicy.Read(profile).Where(s => s.Methods.Intersect(methods).Any()).ToArray();
    Check("story_declares_strategy_methods_under_test", strategies.Length > 0);
    var initial = world.ChooseStrategyMethod(who, methods);
    Check("unacquired_question_source_cannot_activate_field_methods", initial is null);
    foreach (var strategy in strategies) world.AcquireStrategySource(who, strategy.Source);
    world.ObserveStrategyState(who, strategies.SelectMany(s => s.Conditions).GroupBy(c => c.Tag)
        .ToDictionary(g => g.Key, g => g.Max(c => c.Minimum)), "neutral.fixture");
    string? Chosen(string[] available) => world.ChooseStrategyMethod(who, available) is { } d ? MorningWorld.StrategyMethodKey(d) : null;
    var selected = Chosen(methods);
    Check("own_acquired_thresholds_activate_authored_methods", selected is not null && methods.Contains(selected));
    world.ObserveStrategyState(other, new Dictionary<string, int> { ["observed_damage"] = 91, ["observed_stall"] = 91 }, "neutral.other");
    foreach (var strategy in strategies) world.AcquireStrategySource(other, strategy.Source);
    Check("other_subject_cognition_does_not_change_actor_choice", Chosen(methods) == selected);
    Check("repeated_method_choice_is_not_permanently_consumed", Enumerable.Range(0, 5).All(_ => Chosen(methods) == selected));
    Check("empty_feasible_methods_propose_nothing", Chosen([]) is null);
    Check("restoring_availability_restores_prior_method", Chosen(methods) == selected);
    var unknown = root.GetProperty("EVIDENCE").EnumerateObject().Select(e => e.Name).First(id => world.KnownEvidenceText(who, id) == "");
    world.Examine((Person)Array.IndexOf(Names.PersonKeys, other), unknown);
    Check("other_subject_evidence_cannot_be_reported_as_own", world.KnownEvidenceText(who, unknown) == "" && !world.ReportKnownEvidence(who, unknown, out _));
    var known = people[0].Value.GetProperty("clues").EnumerateArray().Select(e => e.GetString()!).First();
    world.Examine(person, known);
    var speaker = world.PersistentSession.Entity("world:" + declaration.GetProperty("id").GetString());
    var listener = world.PersistentSession.Character(session.PlayerActor);
    world.PersistentSession.Runtime.Observe(listener.Soul, world.PersistentSession.Runtime.Representations(speaker), 0);
    var direct = world.KnownEvidenceText("detective", known);
    Check("known_fact_report_uses_only_speaker_acquired_text", world.ReportKnownEvidence(who, known, out var text)
        && text == world.KnownEvidenceText(who, known));
    var reported = listener.Soul.World.History.Last();
    Check("reported_evidence_is_a_distinct_testimony_concept", reported.Source == ObservationSource.Testimony
        && reported.Target.Kind == ProjectionKind.Concept && reported.Details == text
        && world.KnownEvidenceText("detective", known) == direct);
    Testimony(session.ContentSession, known, who);
    Check("report_progress_keeps_seen_separate_from_testimony", session.ContentSession.KnownMaterial(known)
        && !session.ContentSession.Session.Has("examined", known));
});

Case("host_method_cooldown_reopens_choice", () =>
{
    var session = new Chapter1Session();
    var content = session.ContentSession;
    var who = people[0].Name;
    var dailyStage = root.GetProperty("STAGES").EnumerateObject().First(s => s.Value.GetProperty("daily").GetBoolean()
        && s.Value.GetProperty("sites").ValueKind == JsonValueKind.String).Name;
    content.Session.Set("state", "stage", dailyStage);
    var room = Names.RoomKeys[0];
    content.Session.Locate("detective", room);
    foreach (var entry in people)
    {
        content.Session.Locate(entry.Name, room);
        var fact = Invoke(session, "F", entry.Name)!;
        fact.GetType().GetField("Visible")!.SetValue(fact, entry.Name == who);
    }
    using var stream = typeof(MorningWorld).Assembly.GetManifestResourceStream("Awakening.World.json")!;
    using var doc = JsonDocument.Parse(stream);
    var person = (Person)Array.IndexOf(Names.PersonKeys, who);
    var profile = doc.RootElement.GetProperty("chapters")[0].GetProperty("entities").EnumerateArray()
        .First(e => e.GetProperty("kind").GetString() == "character" && e.GetProperty("name").GetString() == Names.Of(person)).GetProperty("profile");
    var strategies = StrategyPolicy.Read(profile);
    foreach (var strategy in strategies) session.World.AcquireStrategySource(who, strategy.Source);
    session.World.ObserveStrategyState(who, strategies.SelectMany(s => s.Conditions).GroupBy(c => c.Tag)
        .ToDictionary(g => g.Key, g => g.Max(c => c.Minimum)), "neutral.fixture");
    var clue = people[0].Value.GetProperty("clues")[0].GetString()!;
    session.World.Examine(person, clue);
    var now = content.GameMinutes;
    Check("host_selects_current_authored_strategy", Invoke(session, "TryStrategyAction") is true);
    var events = (List<Dictionary<string, object?>>)typeof(Chapter1Session).GetField("_out", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
    var initial = events.Last(e => e.GetValueOrDefault("kind")?.ToString() == "strategy_action");
    var method = initial["method"]!.ToString();
    Check("host_global_cooldown_blocks_immediate_reentry", Invoke(session, "TryStrategyAction") is false);
    ((IList)typeof(Chapter1Session).GetField("_jobs", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!).Clear();
    content.Session.Set("clock", "game_minutes", now + track.GetProperty("cooldown_minutes").GetInt32() * 2);
    Check("host_selects_strategy_again_after_method_cooldown", Invoke(session, "TryStrategyAction") is true);
    var repeated = events.Last(e => e.GetValueOrDefault("kind")?.ToString() == "strategy_action");
    Check("method_is_available_again_without_permanent_exhaustion", repeated["method"]!.ToString() == method
        && content.Session.GetProgress("strategy_actions").Count == 0);
});

// Boundary fixtures retain the original checks above and read chapter identities from compiled data.
ScriptCharacter BoundaryActor(Chapter1Session session, string who)
{
    using var stream = typeof(MorningWorld).Assembly.GetManifestResourceStream("Awakening.World.json")!;
    using var document = JsonDocument.Parse(stream);
    var declaration = document.RootElement.GetProperty("chapters")[0].GetProperty("entities").EnumerateArray()
        .Single(e => e.GetProperty("kind").GetString() == "character"
            && e.GetProperty("name").GetString() == root.GetProperty("PERSON_NAMES").GetProperty(who).GetString());
    var id = declaration.GetProperty("id").GetString()!;
    return session.World.PersistentSession.Character(id.Contains(':') ? id : "world:" + id);
}
void BoundaryLocate(Chapter1Session session, string who, bool local, bool visible)
{
    var rooms = root.GetProperty("ROOM_NAMES").EnumerateObject().Select(p => p.Name).ToArray();
    session.ContentSession.Session.Locate(who, rooms[local ? 0 : 1]);
    var fact = Invoke(session, "F", who)!;
    fact.GetType().GetField("Visible")!.SetValue(fact, visible);
}
Chapter1Session BoundarySession()
{
    var session = new Chapter1Session();
    session.ContentSession.Session.Set("state", "stage", root.GetProperty("STAGES").EnumerateObject()
        .First(s => s.Value.GetProperty("daily").GetBoolean() && s.Value.GetProperty("sites").ValueKind == JsonValueKind.String).Name);
    var player = root.GetProperty("PRESENTATION").GetProperty("player_actor").GetString()!;
    BoundaryLocate(session, player, true, true);
    foreach (var entry in people) BoundaryLocate(session, entry.Name, true, true);
    return session;
}
void BoundaryPressure(Chapter1Session session, string who)
{
    session.World.ObserveStrategyState(who, new Dictionary<string, int>
        { ["observed_stall"] = 37, ["observed_refusal"] = 3, ["observed_wrong"] = 4 }, "neutral.boundary.pressure");
    var observed = session.ContentSession.Session.GetProgress("strategy_observed");
    observed[who + ":stall"] = 37;
    observed[who + ":last"] = session.ContentSession.GameMinutes;
    observed[who + ":local"] = true;
    observed[who + ":absence"] = 0;
}
bool BoundaryPressureIs(Chapter1Session session, string who, int stall, int refusal, int wrong)
{
    var basis = session.World.StrategyBasis(who);
    return basis.GetValueOrDefault("observed_stall") == stall && basis.GetValueOrDefault("observed_refusal") == refusal
        && basis.GetValueOrDefault("observed_wrong") == wrong;
}

Case("initial_evidence_text_and_existing_touch_recall_boundary", () =>
{
    var session = new Chapter1Session();
    var world = session.World;
    var evidence = root.GetProperty("EVIDENCE").EnumerateObject().ToArray();
    using var stream = typeof(MorningWorld).Assembly.GetManifestResourceStream("Awakening.World.json")!;
    using var document = JsonDocument.Parse(stream);
    var declarations = document.RootElement.GetProperty("chapters")[0].GetProperty("entities").EnumerateArray()
        .Where(e => e.GetProperty("kind").GetString() == "character").ToArray();
    foreach (var declaration in declarations)
    {
        var who = declaration.GetProperty("id").GetString()!["person.".Length..];
        var id = declaration.GetProperty("id").GetString()!;
        var actor = world.PersistentSession.Character(id.Contains(':') ? id : "world:" + id);
        Check("initial_own_known_evidence_text_is_empty_" + who, evidence.All(e => world.KnownEvidenceText(who, e.Name) == ""));
        Check("initial_body_projection_does_not_contain_full_evidence_details_" + who, evidence.All(e =>
        {
            var body = world.PersistentSession.Entity("world:object." + e.Name);
            var known = world.PersistentSession.Runtime.KnownProjection(actor.Soul, body);
            return known is null || !actor.Soul.World.History.Any(h => ReferenceEquals(h.Target, known)
                && h.Source is ObservationSource.Perception or ObservationSource.Recall && h.Details != "");
        }));
    }
    foreach (var memory in MorningWorld.Memories)
    {
        session = new Chapter1Session(); world = session.World;
        var who = Names.PersonKeys[(int)memory.Who];
        var actor = BoundaryActor(session, who);
        var body = world.PersistentSession.Entity("world:object." + memory.Evidence);
        var known = world.PersistentSession.Runtime.KnownProjection(actor.Soul, body);
        // Formal initialization creates old-object action minds only for companions; the player's recall still exists.
        if (memory.Who != Person.Detective)
            Check("existing_old_object_body_projection_remains_available_" + who, known is not null
                && world.DecideRecall(memory.Evidence) is not null);
        var room = root.GetProperty("EVIDENCE").GetProperty(memory.Evidence).GetProperty("room").GetString();
        world.Locate(memory.Who, (Room)Array.IndexOf(Names.RoomKeys, room));
        if (memory.Who != Person.Detective)
            Check("existing_old_object_can_still_be_taken_" + who, world.Reach(memory.Who) && world.Holding(memory.Who));
        world.Examine(memory.Who, memory.Evidence);
        known = world.PersistentSession.Runtime.KnownProjection(actor.Soul, body);
        var facts = root.GetProperty("EVIDENCE").GetProperty(memory.Evidence).GetProperty("facts").GetString();
        Check("actual_inspection_writes_full_perception_details_" + who, world.KnownEvidenceText(who, memory.Evidence) == facts
            && actor.Soul.World.History.Any(h => ReferenceEquals(h.Target, known) && h.Source == ObservationSource.Perception && h.Details == facts));
        var hooks = world.Hooks(memory.Who);
        Check("existing_touch_recall_writes_own_recall_fragment_" + who, world.Recall(memory.Evidence) == memory
            && world.HasRecalled(memory.Who) && world.Hooks(memory.Who) == hooks + 1
            && actor.Soul.World.History.Any(h => h.Source == ObservationSource.Recall && h.Details == memory.Fragment));
        var history = actor.Soul.World.History.Count;
        Check("repeat_touch_recall_preserves_single_hook_and_history_" + who, world.Recall(memory.Evidence) == memory
            && world.Hooks(memory.Who) == hooks + 1 && actor.Soul.World.History.Count == history);
        if (memory.Who != Person.Detective)
            Check("existing_old_object_can_still_be_put_back_" + who, world.PutBack(memory.Who) && !world.Holding(memory.Who));
    }
});

Case("formal_session_examination_respects_visibility_and_room", () =>
{
    var actors = people.Select(p => p.Name).ToArray();
    var id = root.GetProperty("EVIDENCE").EnumerateObject().First().Name;
    var facts = root.GetProperty("EVIDENCE").GetProperty(id).GetProperty("facts").GetString();
    for (var visible = 0; visible < actors.Length; visible++)
    {
        var session = BoundarySession();
        var hidden = (visible + 1) % actors.Length;
        var remote = (visible + 2) % actors.Length;
        BoundaryLocate(session, actors[hidden], true, false);
        BoundaryLocate(session, actors[remote], false, true);
        foreach (var who in actors) BoundaryPressure(session, who);
        Invoke(session, "Examine", id);
        Check("formal_examination_grants_player_full_text_" + actors[visible], session.World.KnownEvidenceText(
            root.GetProperty("PRESENTATION").GetProperty("player_actor").GetString()!, id) == facts);
        Check("formal_examination_grants_visible_local_actor_full_text_" + actors[visible],
            session.World.KnownEvidenceText(actors[visible], id) == facts && BoundaryPressureIs(session, actors[visible], 0, 0, 0));
        Check("formal_examination_hidden_local_actor_gets_no_full_text_or_pressure_reset_" + actors[hidden],
            session.World.KnownEvidenceText(actors[hidden], id) == "" && BoundaryPressureIs(session, actors[hidden], 37, 3, 4));
        Check("formal_examination_visible_remote_actor_gets_no_full_text_or_pressure_reset_" + actors[remote],
            session.World.KnownEvidenceText(actors[remote], id) == "" && BoundaryPressureIs(session, actors[remote], 37, 3, 4));
        var before = actors.ToDictionary(who => who, who => BoundaryActor(session, who).Soul.World.History.Count);
        Invoke(session, "Examine", id);
        Check("repeat_formal_examination_does_not_duplicate_actor_cognition_" + actors[visible],
            actors.All(who => BoundaryActor(session, who).Soul.World.History.Count == before[who]));
    }
});

Case("formal_say_first_choice_refusal_boundary", () =>
{
    using var dialogue = JsonDocument.Parse((string)typeof(Chapter1CompiledDialogue).GetField("Json", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!);
    foreach (var decline in track.GetProperty("decline_choices").EnumerateObject())
    {
        var who = decline.Value.GetString()!;
        var scene = decline.Name[..decline.Name.IndexOf(":choice:", StringComparison.Ordinal)];
        var inquiry = dialogue.RootElement.GetProperty("entries").GetProperty(scene).EnumerateArray()
            .Single(n => n.GetProperty("op").GetString() == "choices").GetProperty("options").EnumerateArray()
            .First(o => o.GetProperty("id").GetString() != decline.Name && o.GetProperty("condition").ValueKind == JsonValueKind.Null
                && o.GetProperty("body").EnumerateArray().Any(n => n.GetProperty("op").GetString() == "jump" && n.GetProperty("target").GetString() == scene))
            .GetProperty("id").GetString()!;
        int CompleteReplies(Chapter1Session session, params string[] replies)
        {
            var pending = new Queue<string>(replies);
            var selections = 0;
            void Complete(IEnumerable sequence)
            {
                foreach (var operation in sequence)
                {
                    var type = operation!.GetType();
                    switch (type.Name)
                    {
                        case "Until":
                            if (!((Func<bool>)type.GetProperty("Ready")!.GetValue(operation)!)()) throw new InvalidOperationException("Unresolved neutral dialogue wait.");
                            break;
                        case "Run": Complete((IEnumerable)type.GetProperty("Body")!.GetValue(operation)!); break;
                        case "Await":
                            var packet = (Dictionary<string, object?>)type.GetProperty("Event")!.GetValue(operation)!;
                            if (packet.GetValueOrDefault("kind")?.ToString() != "lines") throw new InvalidOperationException("Expected formal dialogue packet.");
                            var reply = new Dictionary<string, object?>();
                            if (packet.GetValueOrDefault("choices") is IEnumerable choices)
                            {
                                var selected = pending.Dequeue();
                                if (!choices.Cast<Dictionary<string, object?>>().Any(c => c.GetValueOrDefault("id")?.ToString() == selected
                                    && c.GetValueOrDefault("available") is true)) throw new InvalidOperationException("Requested reply is not currently available.");
                                reply["choice_id"] = selected; selections++;
                            }
                            typeof(Chapter1Session).GetField("_reply", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, reply);
                            break;
                        default: throw new InvalidOperationException("Unexpected formal dialogue operation: " + type.Name);
                    }
                }
            }
            Complete((IEnumerable)Invoke(session, "Say", scene)!);
            if (pending.Count != 0) throw new InvalidOperationException("Formal dialogue did not consume all requested replies.");
            return selections;
        }
        var session = BoundarySession();
        Check("formal_say_question_then_exit_is_not_refusal_" + who, CompleteReplies(session, inquiry, decline.Name) == 2
            && session.World.StrategyBasis(who).GetValueOrDefault("observed_refusal") == 0);
        session = BoundarySession();
        Check("formal_say_immediate_first_exit_is_refusal_" + who, CompleteReplies(session, decline.Name) == 1
            && session.World.StrategyBasis(who).GetValueOrDefault("observed_refusal") == 1
            && BoundaryActor(session, who).Soul.World.History.Count(h => h.Source == ObservationSource.Perception && h.Details == "heard_reply:" + decline.Name) == 1);
        session = BoundarySession(); BoundaryLocate(session, who, true, false);
        Check("formal_say_hidden_recipient_does_not_observe_refusal_" + who, CompleteReplies(session, decline.Name) == 1
            && session.World.StrategyBasis(who).GetValueOrDefault("observed_refusal") == 0);
        session = BoundarySession(); BoundaryLocate(session, who, false, true);
        Check("formal_say_remote_recipient_does_not_observe_refusal_" + who, CompleteReplies(session, decline.Name) == 1
            && session.World.StrategyBasis(who).GetValueOrDefault("observed_refusal") == 0);
    }
});

Case("new_witnessed_material_resets_active_pressure_and_retains_history", () =>
{
    foreach (var entry in people)
    {
        var who = entry.Name;
        var session = BoundarySession();
        BoundaryPressure(session, who);
        var actor = BoundaryActor(session, who);
        var prior = actor.Soul.World.History.ToArray();
        Invoke(session, "ObserveVisibleInvestigation", "neutral.boundary.material.a");
        Check("new_visible_material_clears_all_active_pressure_" + who, BoundaryPressureIs(session, who, 0, 0, 0)
            && session.ContentSession.Session.Has("strategy_witnessed", who + ":neutral.boundary.material.a"));
        Check("clearing_active_pressure_keeps_old_perception_history_" + who, prior.SequenceEqual(actor.Soul.World.History.Take(prior.Length))
            && actor.Soul.World.History.Any(h => h.Source == ObservationSource.Perception && h.Details == "neutral.boundary.pressure"));
        Invoke(session, "ObserveStrategySituation");
        Check("next_situation_does_not_reactivate_cleared_refusal_or_wrong_pressure_" + who, BoundaryPressureIs(session, who, 0, 0, 0));
        BoundaryPressure(session, who);
        var count = actor.Soul.World.History.Count;
        Invoke(session, "ObserveVisibleInvestigation", "neutral.boundary.material.a");
        Check("repeated_material_does_not_clear_new_pressure_or_append_history_" + who,
            BoundaryPressureIs(session, who, 37, 3, 4) && actor.Soul.World.History.Count == count);
        BoundaryLocate(session, who, true, false);
        Invoke(session, "ObserveVisibleInvestigation", "neutral.boundary.material.b");
        Check("unseen_local_material_does_not_clear_pressure_or_claim_witness_" + who, BoundaryPressureIs(session, who, 37, 3, 4)
            && !session.ContentSession.Session.Has("strategy_witnessed", who + ":neutral.boundary.material.b"));
        BoundaryLocate(session, who, false, true);
        Invoke(session, "ObserveVisibleInvestigation", "neutral.boundary.material.b");
        Check("remote_material_does_not_clear_pressure_or_claim_witness_" + who, BoundaryPressureIs(session, who, 37, 3, 4)
            && !session.ContentSession.Session.Has("strategy_witnessed", who + ":neutral.boundary.material.b"));
        BoundaryLocate(session, who, true, true);
        Invoke(session, "ObserveVisibleInvestigation", "neutral.boundary.material.b");
        Check("first_actual_witness_after_missing_material_clears_pressure_" + who, BoundaryPressureIs(session, who, 0, 0, 0));
    }
});

Case("public_answer_advance_witness_boundary", () =>
{
    foreach (var entry in people)
    {
        var who = entry.Name;
        var session = BoundarySession();
        BoundaryPressure(session, who);
        void Advance(int prior, int current)
        {
            session.ContentSession.Session.Set("answer_state", "step", current);
            Invoke(session, "ObserveAnswerAdvance", prior);
        }
        Advance(0, 1);
        Check("opening_answer_does_not_clear_pressure_" + who, BoundaryPressureIs(session, who, 37, 3, 4)
            && !session.ContentSession.Session.Has("strategy_witnessed", who + ":answer:1"));
        Advance(0, 2);
        Check("answer_advance_without_prior_public_step_does_not_clear_pressure_" + who, BoundaryPressureIs(session, who, 37, 3, 4)
            && !session.ContentSession.Session.Has("strategy_witnessed", who + ":answer:2"));
        Advance(1, 1);
        Check("unchanged_answer_step_does_not_clear_pressure_" + who, BoundaryPressureIs(session, who, 37, 3, 4));
        Advance(2, 1);
        Check("backwards_answer_step_does_not_clear_pressure_" + who, BoundaryPressureIs(session, who, 37, 3, 4));
        Advance(1, 2);
        Check("actual_public_answer_advance_clears_visible_local_pressure_" + who, BoundaryPressureIs(session, who, 0, 0, 0)
            && session.ContentSession.Session.Has("strategy_witnessed", who + ":answer:2"));
        Invoke(session, "ObserveStrategySituation");
        Check("situation_after_answer_advance_does_not_reactivate_old_pressure_" + who, BoundaryPressureIs(session, who, 0, 0, 0));
        BoundaryPressure(session, who);
        Advance(1, 2);
        Check("repeated_public_answer_material_does_not_clear_new_pressure_" + who, BoundaryPressureIs(session, who, 37, 3, 4));
        BoundaryLocate(session, who, true, false);
        Advance(2, 3);
        Check("hidden_actor_does_not_witness_answer_advance_" + who, BoundaryPressureIs(session, who, 37, 3, 4)
            && !session.ContentSession.Session.Has("strategy_witnessed", who + ":answer:3"));
        BoundaryLocate(session, who, false, true);
        Advance(2, 3);
        Check("remote_actor_does_not_witness_answer_advance_" + who, BoundaryPressureIs(session, who, 37, 3, 4)
            && !session.ContentSession.Session.Has("strategy_witnessed", who + ":answer:3"));
        BoundaryLocate(session, who, true, true);
        Advance(2, 3);
        Check("first_local_visible_answer_witness_clears_pressure_" + who, BoundaryPressureIs(session, who, 0, 0, 0));
    }
});

Case("neutral_world_execution_learning_and_save", () => LearningHostCases.WorldFailureAndSave(Check));
Case("neutral_world_finale_cancellation_and_save", () => LearningHostCases.WorldFinaleCancellation(Check));
Case("neutral_morning_candidate_learning", () => LearningHostCases.MorningCandidateSelection(Check));
Case("neutral_morning_strategy_interruption", () => LearningHostCases.MorningStrategyInterruption(Check));
Case("neutral_entered_room_boundary", () => LearningHostCases.EnteredRoomBoundary(Check));

Console.WriteLine($"StrategyIntegrationCheck: {passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
