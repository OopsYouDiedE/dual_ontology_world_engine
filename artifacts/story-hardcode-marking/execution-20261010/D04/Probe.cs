using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Awakening;

internal static class Probe
{
    private static readonly Tag Signal = new("Signal");
    private static readonly Tag Hidden = new("Hidden");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly List<object> Checks = [];
    private static int _failed;
    private sealed record NeutralWorld(WorldRuntime Runtime, SoulEssence Soul, Essence Thing,
        Projection Actor, Projection Surface);

    public static int Main(string[] args)
    {
        var output = args.Length == 0 ? Directory.GetCurrentDirectory() : args[0];
        Directory.CreateDirectory(output);
        var a = World(0); var b = World(1);
        var aHistory = History(a.Soul.World); var bHistory = History(b.Soul.World);
        Check("same-visible-history", aHistory == bHistory, new { history = Hash(aHistory) });
        Check("different-hidden-truth", a.Thing.Get<StateProperty>()!.Values[Hidden] != b.Thing.Get<StateProperty>()!.Values[Hidden],
            new { a = a.Thing.Get<StateProperty>()!.Values[Hidden], b = b.Thing.Get<StateProperty>()!.Values[Hidden] });

        var model = SharedModel();
        var realityBeforeA = Reality(a); var realityBeforeB = Reality(b);
        var mindBeforeA = Mind(a.Soul.World); var mindBeforeB = Mind(b.Soul.World);
        var beliefA = Belief(Reasoner.Assess(a.Soul.World, model));
        var beliefB = Belief(Reasoner.Assess(b.Soul.World, model));
        Check("reasoner-hidden-truth-noninterference", beliefA == beliefB, new { a = beliefA, b = beliefB });
        var decisionA = Decision(DecisionSystem.Choose(a.Soul.World, a.Actor));
        var decisionB = Decision(DecisionSystem.Choose(b.Soul.World, b.Actor));
        Check("decision-hidden-truth-noninterference", decisionA == decisionB, new { a = decisionA, b = decisionB });

        using var content = PlannerContent(4, 2, 0, true);
        var plannerA = Planner(content, a.Soul.World); var plannerB = Planner(content, b.Soul.World);
        var planA = plannerA.Plan(); var planB = plannerB.Plan();
        Check("planner-hidden-truth-noninterference", Plan(planA) == Plan(planB), new { a = Plan(planA), b = Plan(planB) });
        Check("reasoning-does-not-write-source-worlds", Reality(a) == realityBeforeA && Reality(b) == realityBeforeB
            && Mind(a.Soul.World) == mindBeforeA && Mind(b.Soul.World) == mindBeforeB,
            new { sourceRealityA = Hash(realityBeforeA), sourceRealityB = Hash(realityBeforeB), sourceMind = Hash(mindBeforeA) });
        Check("planner-action-budget-not-enforced-in-Plan", plannerA.ActionBudget == 0 && planA.Action is not null,
            new { plannerA.ActionBudget, action = planA.Action?.Id, planA.Iterations, planA.MaxDepth, planA.RemainingHypotheses, planA.Confidence });

        // An independent hypothesis mind can be changed through existing observations.
        // This is a caller-built isolated branch, not an existing Core replay API.
        var branch = World(2);
        Observation.Observe(branch.Soul, new ObservationInput("surface")
        { Target = branch.Surface, Source = ObservationSource.Introspection, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Signal, 99), Time = 3 });
        Check("caller-built-detached-branch-does-not-write-source", branch.Surface.Tags[Signal] == 99
            && a.Surface.Tags[Signal] == 7 && b.Surface.Tags[Signal] == 7
            && Reality(a) == realityBeforeA && Reality(b) == realityBeforeB,
            new { branchSignal = branch.Surface.Tags[Signal], sourceSignal = a.Surface.Tags[Signal] });

        // Deliberately oracle-fed caller data demonstrates why model provenance matters.
        var oracleA = OracleModel(a.Thing.Get<StateProperty>()!.Values[Hidden]);
        var oracleB = OracleModel(b.Thing.Get<StateProperty>()!.Values[Hidden]);
        var unsafeA = Belief(Reasoner.Assess(a.Soul.World, oracleA));
        var unsafeB = Belief(Reasoner.Assess(b.Soul.World, oracleB));
        Check("oracle-fed-model-can-leak-hidden-truth", unsafeA != unsafeB, new { a = unsafeA, b = unsafeB,
            limitation = "Synthetic caller construction; does not assert that Core itself reads Essence." });

        var historyA = RuleHistory(1); var historyB = RuleHistory(5);
        Check("history-loses-prior-rule-version", historyA.History == historyB.History && historyA.EarlyScore != historyB.EarlyScore,
            new { identicalRetainedHistory = Hash(historyA.History), earlyScoreA = historyA.EarlyScore,
                earlyScoreB = historyB.EarlyScore, finalScoreA = historyA.FinalScore, finalScoreB = historyB.FinalScore });
        HistoryAliases();

        var budgetRows = new List<object>();
        foreach (var test in new[] { ("ordinary-unresolved", 4, 2, 1, true), ("zero-iterations", 0, 2, 1, true),
            ("zero-depth", 4, 0, 1, true), ("no-available-actions", 4, 2, 1, false), ("zero-action-budget", 4, 2, 0, true) })
        {
            using var config = PlannerContent(test.Item2, test.Item3, test.Item4, test.Item5);
            var planner = Planner(config, a.Soul.World); var result = planner.Plan();
            budgetRows.Add(new { scenario = test.Item1, actionBudget = planner.ActionBudget, action = result.Action?.Id,
                prediction = result.Prediction?.V1, result.Iterations, result.Nodes, result.MaxDepth, result.RemainingHypotheses,
                result.Confidence, resultFields = typeof(DetectiveSearchDecision).GetProperties().Select(p => p.Name).ToArray() });
        }
        Check("search-contract-has-no-stop-status", typeof(DetectiveSearchDecision).GetProperties()
            .All(p => p.Name != "Status" && p.Name != "StopReason" && p.Name != "BudgetExhausted"), new { scenarios = budgetRows });

        try
        {
            var empty = new CaseModel("empty", ["choice"], new[] { new[] { "a", "b" } }, [], []);
            Reasoner.Assess(a.Soul.World, empty);
            Check("empty-case-model-is-untyped-exception", false, new { error = "No exception" });
        }
        catch (InvalidOperationException error)
        {
            Check("empty-case-model-is-untyped-exception", true, new { exception = error.GetType().FullName, error.Message });
        }
        var envelope = new { scope = "D04 neutral isolated mechanism experiment", mode = "DETECTION_ONLY", failed = _failed,
            limitations = new[] { "Existing Core project is referenced unchanged.",
                "DetectivePlanning.cs is compiled unchanged with neutral minimal adapters.",
                "No game content, design files, story source, golden expectations or full Godot session is loaded.",
                "Chapter2 snapshot and inference findings are source diagnosis, not a host runtime test." }, checks = Checks };
        File.WriteAllText(Path.Combine(output, "probe-results.json"), JsonSerializer.Serialize(envelope, Json), new UTF8Encoding(false));
        Console.WriteLine(JsonSerializer.Serialize(envelope, Json));
        return _failed == 0 ? 0 : 1;
    }

    private static NeutralWorld World(int hidden)
    {
        var runtime = new WorldRuntime(); var soul = new SoulEssence("agent");
        var actorView = runtime.Project(soul, "agent");
        var thing = new Essence("neutral-object"); var state = thing.Add(new StateProperty());
        state.Values[Signal] = 7; state.Observable.Add(Signal); state.Values[Hidden] = hidden;
        var surfaceView = runtime.Project(thing, "surface");
        runtime.Observe(soul, [actorView, surfaceView], 1);
        var actor = runtime.KnownProjection(soul, soul)!; var surface = runtime.KnownProjection(soul, thing)!;
        Observation.Observe(soul, new ObservationInput("policy", ProjectionKind.Concept)
        { Existence = Truth.True, Time = 2, Source = ObservationSource.Introspection,
            Rules = [new(new Condition.Tagged(surface, Signal, 7), RuleMode.Prefer, new Effect.Act(ActionOp.Inspect, surface), 3, Truth.True)] });
        return new(runtime, soul, thing, actor, surface);
    }
    private static CaseModel SharedModel() => new("neutral", ["choice"], new[] { new[] { "a", "b" } },
        [new("surface", Signal, "signal")], [new([0], ImmutableDictionary<string, int>.Empty.Add("signal", 7)),
            new([1], ImmutableDictionary<string, int>.Empty.Add("signal", 7))]);
    private static CaseModel OracleModel(int value) => new("neutral", ["choice"], new[] { new[] { "a", "b" } },
        [new("surface", Signal, "signal")], [new([value], ImmutableDictionary<string, int>.Empty.Add("signal", 7))]);
    private static string Belief(CaseBelief belief) => JsonSerializer.Serialize(new { belief.Candidates, belief.Credence, belief.Contradicted, belief.Basis });
    private static string Decision(Decision? decision) => decision is null ? "none" : JsonSerializer.Serialize(new { effect = EffectText(decision.Effect), decision.Score, basis = decision.Basis.Name });
    private static string Plan(DetectiveSearchDecision value) => JsonSerializer.Serialize(new { action = value.Action?.Id,
        value.Prediction, value.Iterations, value.Nodes, value.MaxDepth, value.RemainingHypotheses, value.Confidence, value.Branches });
    private static string EffectText(Effect? effect) => effect is Effect.Act action
        ? $"{action.Operation}:{(action.Target as Noun.Reference)?.Projection.Name}" : effect?.GetType().Name ?? "none";
    private static string Mind(ProjectionWorld mind) => JsonSerializer.Serialize(new { current = mind.Projections.Select(p => new
        { p.Name, kind = p.Kind.ToString(), existence = p.Existence.ToString(), parent = p.Parent?.Name,
            tags = p.Tags.OrderBy(x => x.Key.Name).Select(x => new { tag = x.Key.Name, x.Value }),
            rules = p.Rules.Select(r => new { mode = r.Mode.ToString(), r.Weight, acceptance = r.Acceptance.ToString(), effect = EffectText(r.Effect) }) }),
        history = History(mind) });
    private static string History(ProjectionWorld mind) => JsonSerializer.Serialize(mind.History.Select(h => new
        { target = h.Target.Name, kind = h.Target.Kind.ToString(), parent = h.Target.Parent?.Name,
            source = h.Source.ToString(), h.Time, h.Detected, h.Existence, h.Details,
            tags = h.Tags.OrderBy(x => x.Key.Name).Select(x => new { tag = x.Key.Name, x.Value }),
            relations = h.Relations.Select(r => new { tag = r.Relation.Name, target = r.Target.Name }).OrderBy(r => r.tag).ThenBy(r => r.target) }));
    private static string Reality(NeutralWorld world) => JsonSerializer.Serialize(new
    { essence = world.Thing.Get<StateProperty>()!.Values.OrderBy(x => x.Key.Name).Select(x => new { tag = x.Key.Name, x.Value }),
        runtime = Mind(world.Runtime.Reality) });
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static void Check(string name, bool pass, object evidence)
    { Checks.Add(new { name, pass, evidence }); if (!pass) _failed++; }

    private static (string History, int EarlyScore, int FinalScore) RuleHistory(int earlyWeight)
    {
        var soul = new SoulEssence("agent");
        var actor = Observation.Observe(soul, new ObservationInput("agent", ProjectionKind.Body) { Existence = Truth.True, Time = 1 });
        var surface = Observation.Observe(soul, new ObservationInput("surface") { Existence = Truth.True, Time = 1 });
        Rule rule(int weight) => new(new Condition.Constant(Truth.True), RuleMode.Prefer, new Effect.Act(ActionOp.Inspect, surface), weight, Truth.True);
        var policy = Observation.Observe(soul, new ObservationInput("policy", ProjectionKind.Concept)
        { Existence = Truth.True, Time = 2, Rules = [rule(earlyWeight)] });
        var early = DecisionSystem.Choose(soul.World, actor)!.Score;
        Observation.Observe(soul, new ObservationInput("policy", ProjectionKind.Concept)
        { Target = policy, Time = 3, Rules = [rule(9)] });
        return (History(soul.World), early, DecisionSystem.Choose(soul.World, actor)!.Score);
    }
    private static void HistoryAliases()
    {
        var soul = new SoulEssence("agent");
        var areaA = Observation.Observe(soul, new ObservationInput("area-a", ProjectionKind.Space) { Existence = Truth.True, Time = 1 });
        var areaB = Observation.Observe(soul, new ObservationInput("area-b", ProjectionKind.Space) { Existence = Truth.True, Time = 1 });
        var item = Observation.Observe(soul, new ObservationInput("item-old")
        { Existence = Truth.True, Time = 2, Move = true, Parent = areaA, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Signal, 7) });
        var oldEvent = soul.World.History[^1];
        Observation.Observe(soul, new ObservationInput("item-current", ProjectionKind.Body)
        { Target = item, Time = 3, Move = true, Parent = areaB, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Signal, 9) });
        Check("old-history-target-aliases-current-projection", oldEvent.Target.Name == "item-current"
            && oldEvent.Target.Kind == ProjectionKind.Body && oldEvent.Target.Parent == areaB && oldEvent.Tags[Signal] == 7,
            new { recordedTime = oldEvent.Time, currentNameThroughOldEvent = oldEvent.Target.Name,
                currentKindThroughOldEvent = oldEvent.Target.Kind.ToString(), currentParentThroughOldEvent = oldEvent.Target.Parent?.Name,
                retainedTagSnapshot = oldEvent.Tags[Signal], currentTagThroughOldEvent = oldEvent.Target.Tags[Signal] });
        var before = soul.World.History.Count;
        Observation.Observe(soul, new ObservationInput(item.Name, item.Kind) { Target = item, Forget = true, Time = 4 });
        Check("forget-removes-history-instead-of-recording-tombstone", soul.World.History.Count < before
            && !soul.World.History.Any(h => ReferenceEquals(h.Target, item)),
            new { before, after = soul.World.History.Count, forgottenTargetStillRecorded = soul.World.History.Any(h => ReferenceEquals(h.Target, item)) });
    }
    private static JsonDocument PlannerContent(int iterations, int depth, int actionBudget, bool actions)
        => JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["DETECTIVE_SEARCH"] = new { seed = 17, iterations, depth, action_budget = actionBudget, confidence = 0.9,
                unknown_reliability = 2.0, information_weight = 1.0, progress_weight = 1.0, time_cost = 0.1,
                repeat_penalty = 0.1, exploration = 1.0, discount = 0.9 },
            ["DETECTIVE_ACTIONS"] = actions ? new Dictionary<string, object> { ["inspect-neutral"] = new
            { kind = "inspect", target = "neutral", room = "neutral-space", label = "inspect", minutes = 1.0, weight = 1.0,
                requires_group = "", requires_key = "", completion_group = "", completion_key = "", repeatable = true } } : new Dictionary<string, object>(),
            ["DETECTIVE_TRACE_FIELDS"] = new Dictionary<string, object> { ["signal"] = new { evidence = "neutral", field = "signal", tag = "Signal", equals = 7 } },
            ["DETECTIVE_ARGUMENTS"] = new Dictionary<string, object>(),
            ["DETECTIVE_MOTIVES"] = new Dictionary<string, object>(),
            ["EVIDENCE"] = new Dictionary<string, object> { ["neutral"] = new { name = "surface" } }
        }));
    private static DetectivePlanner Planner(JsonDocument content, ProjectionWorld mind)
    {
        var session = new AwakeningContentSession(content); session.Session.Set("examined", "neutral", true);
        var planner = new DetectivePlanner(content.RootElement); planner.Refresh(mind, session); return planner;
    }
}
