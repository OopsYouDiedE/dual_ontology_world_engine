using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

var passed = 0;
var failed = 0;
void Check(string name, bool condition)
{
    if (condition) { passed++; Console.WriteLine("PASS " + name); }
    else { failed++; Console.WriteLine("FAIL " + name); }
}
void Reject(string name, Action action)
{
    try { action(); Check(name, false); }
    catch (Exception error) when (error is ArgumentException or InvalidDataException or CognitionContractException or JsonException)
    { Check(name, true); }
}
T Ok<T>(ContractResult<T> result) => result is ContractResult<T>.Success success ? success.Value
    : throw new Exception("Contract failure: " + ((ContractResult<T>.Failure)result).Error);
CharacterStrategy Strategy(string name, int priority, string source = "source.a", string[]? methods = null,
    params StrategyThreshold[] conditions) => new(name, "neutral.target", "neutral.stance", priority,
        source, methods ?? ["method.a"], conditions);
void Tags(SoulEssence soul, Projection actor, params (Tag Tag, int? Value)[] changes)
    => Observation.Observe(soul, new(actor.Name, actor.Kind) { Target = actor,
        Source = ObservationSource.Introspection, Tags = changes.ToImmutableDictionary(p => p.Tag, p => p.Value) });
string Choice(Decision? decision) => decision?.Effect is Effect.Set value ? value.Value.ToString() : "none";
string Scores(ProjectionWorld world, Projection actor) => string.Join(",", DecisionSystem.Assess(world, actor)
    .Select(a => Choice(a.Decision) + ":" + a.Decision.Score + ":" + a.Forbidden));

foreach (var nested in new[] { false, true })
{
    var prefix = nested ? "nested_" : "flat_";
    using var f = new NeutralFixture(nested);
    var actor = f.Actor;
    var self = actor.Self;
    var methods = new[] { new StrategyMethod("method.a", new Effect.Set(self, new("neutral.choice"), 1),
        new Condition.Constant(Truth.True)) };
    var strategy = Strategy("A", 25);
    StrategyPolicy.Bind(actor.Soul, self, [strategy], methods, "neutral.domain", 0);
    Check(prefix + "unknown_source_does_not_activate", DecisionSystem.Choose(actor.Soul.World, self) is null);
    StrategyPolicy.Acquire(f.Other.Soul, f.Other.Self, "source.a", 0, "neutral");
    Check(prefix + "another_subject_source_does_not_activate", DecisionSystem.Choose(actor.Soul.World, self) is null);
    var beforeAcquire = f.Session.CTB.Now;
    StrategyPolicy.Acquire(actor.Soul, self, "source.a", 0, "neutral");
    Check(prefix + "own_acquired_source_activates", Choice(DecisionSystem.Choose(actor.Soul.World, self)) == "1");
    Check(prefix + "bind_and_acquire_do_not_execute_or_queue", !self.Tags.ContainsKey(new("neutral.choice"))
        && !actor.Body.Get<StateProperty>()!.Values.ContainsKey(new("neutral.choice"))
        && !f.Session.CTB.Pending.Any() && f.Session.CTB.Now == beforeAcquire
        && Ok(f.Session.CheckSavePoint()));
    var acquiredRevision = Ok(actor.Soul.World.CaptureSnapshot()).Revision;
    StrategyPolicy.Acquire(actor.Soul, self, "source.a", 10, "neutral");
    Check(prefix + "repeated_source_acquisition_is_idempotent", acquiredRevision == Ok(actor.Soul.World.CaptureSnapshot()).Revision);
    var beforeDecision = Ok(f.Runtime.CaptureCognitionGraph(actor.Soul));
    var repeated = Enumerable.Range(0, 5).Select(_ => Choice(DecisionSystem.Choose(actor.Soul.World, self))).ToArray();
    Check(prefix + "methods_are_candidates_not_consumed_queue", repeated.All(x => x == "1")
        && Ok(CognitionCodec.Encode(beforeDecision)).SequenceEqual(Ok(CognitionCodec.Encode(Ok(f.Runtime.CaptureCognitionGraph(actor.Soul))))));
    Tags(actor.Soul, self, (StrategyPolicy.SourceTag("source.a"), null));
    Check(prefix + "forgotten_source_deactivates", DecisionSystem.Choose(actor.Soul.World, self) is null);
    StrategyPolicy.Acquire(actor.Soul, self, "source.a", 11, "neutral", ObservationSource.Testimony);
    Check(prefix + "source_reacquisition_restores_choice", Choice(DecisionSystem.Choose(actor.Soul.World, self)) == "1");

    var twoMethods = new[] {
        new StrategyMethod("method.a", new Effect.Set(self, new("neutral.choice"), 1), new Condition.Constant(Truth.True)),
        new StrategyMethod("method.b", new Effect.Set(self, new("neutral.choice"), 2), new Condition.Tagged(self, new("neutral.available"), 1)) };
    var low = Strategy("B", 10, methods: ["method.a"]);
    var high = Strategy("C", 40, methods: ["method.b"], conditions: [new("neutral.level", 3)]);
    StrategyPolicy.Bind(actor.Soul, self, [low, high], twoMethods, "neutral.threshold", 0);
    Tags(actor.Soul, self, (new("neutral.level"), 2), (new("neutral.available"), 1));
    Check(prefix + "below_threshold_uses_lower_priority", Choice(DecisionSystem.Choose(actor.Soul.World, self)) == "1");
    Tags(actor.Soul, self, (new("neutral.level"), 3));
    var ranked = DecisionSystem.Assess(actor.Soul.World, self);
    Check(prefix + "threshold_boundary_switches_priority", Choice(ranked.First().Decision) == "2"
        && ranked.First().Decision.Score == 40 && ranked.Last().Decision.Score == 35);
    Tags(actor.Soul, self, (new("neutral.available"), 0));
    Check(prefix + "unavailable_method_is_not_a_candidate", DecisionSystem.Assess(actor.Soul.World, self).All(a => Choice(a.Decision) != "2"));
    Tags(actor.Soul, self, (new("neutral.available"), null));
    Check(prefix + "unknown_availability_is_not_a_candidate", DecisionSystem.Assess(actor.Soul.World, self).All(a => Choice(a.Decision) != "2"));
    Tags(actor.Soul, self, (new("neutral.available"), 1), (new("neutral.level"), null));
    Check(prefix + "unknown_threshold_is_not_a_candidate", Choice(DecisionSystem.Choose(actor.Soul.World, self)) == "1");
    Tags(actor.Soul, self, (new("neutral.level"), 3));
    var archive = Ok(f.Runtime.CaptureCognitionGraph(actor.Soul));
    var bytes = Ok(CognitionCodec.Encode(archive));
    var decoded = Ok(CognitionCodec.Decode(bytes));
    var graph = Ok(CognitionGraph.Rebind(decoded));
    var rebound = graph.Projections[self.SubjectId!.Value];
    Check(prefix + "cognition_wire_roundtrip_preserves_rules_and_scores", bytes.SequenceEqual(Ok(CognitionCodec.Encode(decoded)))
        && Scores(actor.Soul.World, self) == Scores(graph.World, rebound)
        && graph.Projections.Values.SelectMany(p => p.Rules).All(r => r.Mode == RuleMode.Prefer
            && r.Acceptance == Truth.True && r.Lifetime == LifetimeScope.Chapter && r.LifetimeOwner is not null));
    var restored = Ok(WorldRuntime.Restore(Ok(f.Runtime.Save())));
    var loadedSoul = (SoulEssence)restored.Entities[actor.Soul.Id];
    var loadedSelf = restored.Projections[self.SubjectId!.Value];
    Check(prefix + "runtime_save_restores_acquired_source_and_choice", loadedSelf.Tags[StrategyPolicy.SourceTag("source.a")] == 1
        && Scores(actor.Soul.World, self) == Scores(loadedSoul.World, loadedSelf)
        && Choice(DecisionSystem.Choose(loadedSoul.World, loadedSelf)) == "2");
    Tags(loadedSoul, loadedSelf, (StrategyPolicy.SourceTag("source.a"), null));
    Check(prefix + "source_forgetting_after_restore_is_effective", DecisionSystem.Choose(loadedSoul.World, loadedSelf) is null);
}

using (var f = new NeutralFixture(false))
{
    var self = f.Actor.Self;
    var method = new StrategyMethod("method.a", new Effect.Set(self, new("neutral.choice"), 1), new Condition.Constant(Truth.True));
    StrategyPolicy.Acquire(f.Actor.Soul, self, "source.a", 0, "neutral");
    StrategyPolicy.Bind(f.Actor.Soul, self, [Strategy("A", 10, methods: ["method.other"])], [method], "neutral.missing", 0);
    Check("unmatched_method_key_proposes_no_candidate", DecisionSystem.Choose(f.Actor.Soul.World, self) is null);
    Reject("foreign_actor_acquisition_rejected", () => StrategyPolicy.Acquire(f.Actor.Soul, f.Other.Self, "source.a", 0, "neutral"));
    Reject("foreign_actor_binding_rejected", () => StrategyPolicy.Bind(f.Actor.Soul, f.Other.Self, [Strategy("A", 10)], [method], "neutral.foreign", 0));
    var before = Ok(f.Actor.Soul.World.CaptureSnapshot()).Revision;
    Reject("foreign_method_effect_rejected", () => StrategyPolicy.Bind(f.Actor.Soul, self, [Strategy("A", 10)],
        [method with { Effect = new Effect.Set(f.Other.Self, new("neutral.choice"), 2) }], "neutral.foreign-effect", 0));
    Reject("foreign_method_availability_rejected", () => StrategyPolicy.Bind(f.Actor.Soul, self, [Strategy("A", 10)],
        [method with { Available = new Condition.Exists(f.Other.Self) }], "neutral.foreign-condition", 0));
    Reject("duplicate_available_method_key_rejected", () => StrategyPolicy.Bind(f.Actor.Soul, self, [Strategy("A", 10)],
        [method, method with { Effect = new Effect.Set(self, new("neutral.choice"), 2) }], "neutral.duplicate", 0));
    Check("invalid_method_binding_does_not_publish", before == Ok(f.Actor.Soul.World.CaptureSnapshot()).Revision);
    var forgotten = Observation.Observe(f.Actor.Soul, new("neutral.retired", ProjectionKind.Body) { Existence = Truth.True });
    Observation.Observe(f.Actor.Soul, new(forgotten.Name, forgotten.Kind) { Target = forgotten, Forget = true });
    Reject("forgotten_actor_acquisition_rejected", () => StrategyPolicy.Acquire(f.Actor.Soul, forgotten, "source.a", 0, "neutral"));
    Reject("forgotten_actor_binding_rejected", () => StrategyPolicy.Bind(f.Actor.Soul, forgotten, [Strategy("A", 10)], [method], "neutral.retired", 0));
}

foreach (var nested in new[] { false, true })
{
    using var a = new NeutralFixture(nested, hiddenTruth: 0);
    using var b = new NeutralFixture(nested, hiddenTruth: 91);
    foreach (var f in new[] { a, b })
    {
        var actor = f.Actor;
        StrategyPolicy.Bind(actor.Soul, actor.Self, [Strategy("A", 10), Strategy("B", 40,
            methods: ["method.b"], conditions: [new("neutral.level", 3)])],
            [new("method.a", new Effect.Set(actor.Self, new("neutral.choice"), 1), new Condition.Constant(Truth.True)),
             new("method.b", new Effect.Set(actor.Self, new("neutral.choice"), 2), new Condition.Constant(Truth.True))], "neutral.metamorphic", 0);
        StrategyPolicy.Acquire(actor.Soul, actor.Self, "source.a", 0, "neutral");
        Check("hidden_state_not_acquired_" + nested + "_" + f.HiddenTruth,
            !Ok(actor.Soul.World.CaptureSnapshot()).Projections.Any(p => p.Tags.ContainsKey(new("neutral.hidden"))));
    }
    var same = true;
    for (var level = 0; level < 6; level++)
    {
        foreach (var f in new[] { a, b }) Tags(f.Actor.Soul, f.Actor.Self, (new("neutral.level"), level));
        same &= Scores(a.Actor.Soul.World, a.Actor.Self) == Scores(b.Actor.Soul.World, b.Actor.Self)
            && Choice(DecisionSystem.Choose(a.Actor.Soul.World, a.Actor.Self)) == Choice(DecisionSystem.Choose(b.Actor.Soul.World, b.Actor.Self));
    }
    Check("hidden_truth_metamorphism_six_threshold_states_" + nested, same);
}

using (var f = new NeutralFixture(false))
{
    var self = f.Actor.Self;
    StrategyPolicy.Acquire(f.Actor.Soul, self, "source.a", 0, "neutral");
    var methods = new[] {
        new StrategyMethod("neutral.method.one", new Effect.Set(self, new("neutral.choice"), 1), new Condition.Constant(Truth.True), "neutral.category"),
        new StrategyMethod("neutral.method.two", new Effect.Set(self, new("neutral.choice"), 2), new Condition.Constant(Truth.True), "neutral.category") };
    var category = Strategy("A", 20, methods: ["neutral.category"]);
    StrategyPolicy.Bind(f.Actor.Soul, self, [category], methods, "neutral.category-domain", 0, "neutral.chapter");
    var candidates = DecisionSystem.Assess(f.Actor.Soul.World, self);
    Check("method_category_offers_individual_candidates", candidates.Count == 2
        && candidates.All(a => a.Decision.Effect is Effect.Set && a.Decision.Score == 20));
    Check("explicit_lifetime_owner_is_preserved", f.Actor.Soul.World.Projections.SelectMany(p => p.Rules)
        .All(r => r.Lifetime == LifetimeScope.Chapter && r.LifetimeOwner == "neutral.chapter"));
    StrategyPolicy.Bind(f.Actor.Soul, self, [category], [methods[1]], "neutral.category-domain", 1, "neutral.chapter");
    Check("rebinding_current_methods_removes_retired_candidates", Choice(DecisionSystem.Choose(f.Actor.Soul.World, self)) == "2"
        && DecisionSystem.Assess(f.Actor.Soul.World, self).Count == 1);
}

using (var profile = JsonDocument.Parse("""
{"strategies":[{"name":"A","target":"neutral.target","stance":"neutral.stance","priority":12,"source":"source.a","methods":["method.a"],"conditions":[{"tag":"neutral_level","minimum":2}]}]}
"""))
{
    var read = StrategyPolicy.Read(profile.RootElement);
    Check("read_preserves_closed_strategy_fields", read.Length == 1 && read[0].Priority == 12
        && read[0].Methods.SequenceEqual(["method.a"]) && read[0].Conditions.Single() == new StrategyThreshold("neutral_level", 2));
}
foreach (var (name, json) in new[] {
    ("read_rejects_unknown_field", """{"strategies":[{"name":"A","target":"T","stance":"S","priority":1,"source":"source.a","methods":["m"],"conditions":[],"intent":"X"}]}"""),
    ("read_rejects_duplicate_field", """{"strategies":[{"name":"A","name":"B","target":"T","stance":"S","priority":1,"source":"source.a","methods":["m"],"conditions":[]}]}"""),
    ("read_rejects_missing_source", """{"strategies":[{"name":"A","target":"T","stance":"S","priority":1,"methods":["m"],"conditions":[]}]}"""),
    ("read_rejects_empty_methods", """{"strategies":[{"name":"A","target":"T","stance":"S","priority":1,"source":"source.a","methods":[],"conditions":[]}]}"""),
    ("read_rejects_negative_threshold", """{"strategies":[{"name":"A","target":"T","stance":"S","priority":1,"source":"source.a","methods":["m"],"conditions":[{"tag":"level","minimum":-1}]}]}"""),
    ("read_rejects_priority_out_of_range", """{"strategies":[{"name":"A","target":"T","stance":"S","priority":1001,"source":"source.a","methods":["m"],"conditions":[]}]}""") })
{
    using var profile = JsonDocument.Parse(json);
    Reject(name, () => StrategyPolicy.Read(profile.RootElement));
}
foreach (var nested in new[] { false, true })
{
    using var f = new NeutralFixture(nested);
    var soul = f.Actor.Soul;
    var self = f.Actor.Self;
    var world = soul.World;
    var prefix = "cache_" + (nested ? "nested_" : "flat_");
    var strategy = Strategy("A", 20);
    var method = new StrategyMethod("method.a", new Effect.Set(self, new("neutral.choice"), 1), new Condition.Constant(Truth.True));
    void Bind() => StrategyPolicy.Bind(soul, self, [strategy], [method], "neutral.cache", 20, "neutral.owner");
    StrategyPolicy.Acquire(soul, self, "source.a", 0, "neutral");
    Bind();
    var node = world.Projections.Single(p => p.Name == "neutral.cache:A");
    var revision = world.Revision;
    var history = world.History.Count;
    var last = node.LastObservation;
    for (var i = 0; i < 10; i++)
        StrategyPolicy.Bind(soul, self, [strategy with { }], [method with { Effect = new Effect.Set(self, new("neutral.choice"), 1),
            Available = new Condition.Constant(Truth.True) }], "neutral.cache", 100 + i, "neutral.owner");
    Check(prefix + "equivalent_rebind_preserves_revision_history_and_observation", world.Revision == revision
        && world.History.Count == history && ReferenceEquals(last, node.LastObservation));
    var cache = new StrategyAssessmentCache();
    var initial = cache.Assess(world, self);
    Check(prefix + "same_revision_reuses_assessment_without_observation", ReferenceEquals(initial, cache.Assess(world, self))
        && world.Revision == revision && world.History.Count == history);
    strategy = strategy with { Priority = 45 };
    Bind();
    var changed = cache.Assess(world, self);
    Check(prefix + "priority_change_publishes_and_invalidates", world.Revision != revision
        && world.History.Count == history + 1 && !ReferenceEquals(initial, changed) && changed.Single().Decision.Score == 45);
    revision = world.Revision;
    strategy = strategy with { Target = "neutral.target.changed", Stance = "neutral.stance.changed" };
    Bind();
    Check(prefix + "description_change_publishes", world.Revision != revision
        && node.LastObservation!.Details == "neutral.target.changed/neutral.stance.changed");
    revision = world.Revision;
    method = method with { Available = new Condition.Constant(Truth.False) };
    Bind();
    Check(prefix + "availability_change_removes_candidate", world.Revision != revision && cache.Assess(world, self).Count == 0);
    method = method with { Available = new Condition.Constant(Truth.True), Effect = new Effect.Set(self, new("neutral.choice"), 2) };
    Bind();
    Check(prefix + "effect_change_replaces_candidate", Choice(cache.Assess(world, self).Single().Decision) == "2");
    revision = world.Revision;
    StrategyPolicy.Bind(soul, self, [strategy], [method], "neutral.cache", 21, "neutral.owner.changed");
    Check(prefix + "lifetime_owner_change_publishes", world.Revision != revision
        && node.Rules.Single().LifetimeOwner == "neutral.owner.changed");
    Observation.Observe(soul, new(node.Name, node.Kind) { Target = node, Source = ObservationSource.Introspection,
        Rules = [node.Rules.Single() with { Lifetime = LifetimeScope.Interaction }] });
    revision = world.Revision;
    Bind();
    Check(prefix + "changed_lifetime_scope_is_restored", world.Revision != revision
        && node.Rules.Single().Lifetime == LifetimeScope.Chapter);
    Observation.Observe(soul, new(node.Name, node.Kind) { Target = node, Source = ObservationSource.Introspection,
        Rules = [node.Rules.Single() with { Acceptance = Truth.False }] });
    revision = world.Revision;
    Bind();
    Check(prefix + "changed_acceptance_is_restored", world.Revision != revision && node.Rules.Single().Acceptance == Truth.True);
    strategy = strategy with { Conditions = [new("neutral.gate", 2)] };
    Bind();
    var gated = cache.Assess(world, self);
    Tags(soul, self, (new("neutral.gate"), 2));
    var unblocked = cache.Assess(world, self);
    Check(prefix + "cognition_change_invalidates_and_updates_availability", gated.Count == 0
        && !ReferenceEquals(gated, unblocked) && unblocked.Count == 1);
    var score = unblocked.Single().Decision.Score;
    ActionLearning.Observe(soul, self, ActionLearning.Key(world, self, method.Effect), DecisionOutcomeV1.Failed);
    var learned = cache.Assess(world, self);
    Check(prefix + "action_feedback_invalidates_and_changes_score", !ReferenceEquals(unblocked, learned)
        && learned.Single().Decision.Score < score && ReferenceEquals(learned, cache.Assess(world, self)));
    var other = f.Other;
    StrategyPolicy.Acquire(other.Soul, other.Self, "source.a", 0, "neutral");
    StrategyPolicy.Bind(other.Soul, other.Self, [Strategy("A", 90)],
        [new("method.a", new Effect.Set(other.Self, new("neutral.choice"), 3), new Condition.Constant(Truth.True))], "neutral.cache", 0);
    var otherResult = cache.Assess(other.Soul.World, other.Self);
    Check(prefix + "subjects_are_isolated", otherResult.Single().Decision.Score == 90
        && Choice(otherResult.Single().Decision) == "3" && ReferenceEquals(learned, cache.Assess(world, self))
        && ActionLearning.Capture(other.Soul.World, other.Self).Experiences.Length == 0);
    Reject(prefix + "foreign_actor_rejected_even_when_cached", () => cache.Assess(other.Soul.World, self));
    var saved = Ok(WorldRuntime.Restore(Ok(f.Runtime.Save())));
    var restoredSoul = (SoulEssence)saved.Entities[soul.Id];
    var restoredSelf = saved.Projections[self.SubjectId!.Value];
    var restoredResult = cache.Assess(restoredSoul.World, restoredSelf);
    Check(prefix + "restored_session_gets_fresh_assessment", !ReferenceEquals(learned, restoredResult)
        && restoredResult.Single().Decision.Score == learned.Single().Decision.Score);
}
Console.WriteLine($"StrategyCheck: {passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

sealed class NeutralFixture : IDisposable
{
    public WorldRuntime Runtime { get; } = new();
    public SimulationSession Session { get; }
    public ScriptCharacter Actor { get; }
    public ScriptCharacter Other { get; }
    public int HiddenTruth { get; }
    public NeutralFixture(bool nested, int hiddenTruth = 0)
    {
        HiddenTruth = hiddenTruth;
        Session = new(Runtime);
        var world = new CoreScriptWorld(Runtime, Session);
        var room = world.DefineSpace("neutral.room", "R");
        world.DefineObject("neutral.hidden", "H", room, entity => entity.Get<StateProperty>()!.Values[new("neutral.hidden")] = hiddenTruth);
        Actor = world.DefineCharacter("neutral.actor", "A", room);
        Other = world.DefineCharacter("neutral.other", "B", room);
        if (nested)
        {
            var container = world.DefineObject("neutral.container", "C", room);
            var item = world.DefineObject("neutral.item", "I", container);
            world.Observe(Actor, container, item);
        }
    }
    public void Dispose() => Session.Dispose();
}
