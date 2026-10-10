using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DualOntologyWorldEngine.Core;

// Neutral public-API probe. Fixture construction is serial. Concurrency uses only
// ActionLearning Observe/Assess/Capture; it does not establish WorldRuntime safety.
var failures = new List<string>();
void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
void Run(string name, Action test)
{
    try { test(); Console.WriteLine("PASS " + name); }
    catch (Exception e) { failures.Add(name); Console.WriteLine("FAIL " + name + "\n" + e); }
}
void Reject(Action operation, string message)
{
    var rejected = false;
    try { operation(); }
    catch (Exception e) when (e is InvalidDataException or JsonException or ArgumentException or InvalidOperationException or CognitionContractException)
    { rejected = true; }
    Require(rejected, message);
}
T Ok<T>(ContractResult<T> result) => result is ContractResult<T>.Success value ? value.Value
    : throw new InvalidOperationException("Contract failed: " + ((ContractResult<T>.Failure)result).Error);
string Wire(DecisionExperienceV1 row) => JsonSerializer.Serialize(new DecisionMemoryV1(1, [row]));
DecisionExperienceV1 Row(ProjectionWorld world, Projection actor, DecisionExperienceKeyV1 key)
    => ActionLearning.Capture(world, actor).Experiences.Single(row => row.Key == key);
Projection MemoryNode(ProjectionWorld world, Projection actor)
    => world.Projections.Single(node => node.Tags.GetValueOrDefault(new Tag("core.decision-experience.v1")) == 1
        && node.Relations.Contains((Relations.Subject, actor)));
byte[] Graph(ProbeFixture f) => Ok(CognitionCodec.Encode(Ok(f.Runtime.CaptureCognitionGraph(f.Soul))));
void Replace(SoulEssence soul, Projection node, string details)
    => Observation.Observe(soul, new(node.Name, node.Kind)
    { Target = node, Source = ObservationSource.Introspection, Details = details });

Run("warm_assessment_reads_new_personal_feedback", () =>
{
    var f = new ProbeFixture();
    var key = ActionLearning.Key(f.Soul.World, f.Actor, f.First);
    ActionLearning.Observe(f.Soul, f.Actor, key, DecisionOutcomeV1.Attempted);
    var node = MemoryNode(f.Soul.World, f.Actor);
    var oldObservation = node.LastObservation;
    ActionLearning.Assess(f.Soul.World, f.Actor);
    ActionLearning.Capture(f.Soul.World, f.Actor);
    var priorScore = ActionLearning.Assess(f.Soul.World, f.Actor).Single(a => Ast.Equivalent(a.Decision.Effect, f.First)).Decision.Score;
    ActionLearning.Observe(f.Soul, f.Actor, key, DecisionOutcomeV1.Failed);
    var latestScore = ActionLearning.Assess(f.Soul.World, f.Actor).Single(a => Ast.Equivalent(a.Decision.Effect, f.First)).Decision.Score;
    Require(!ReferenceEquals(oldObservation, node.LastObservation), "A replacement must publish a new immutable observation.");
    Require(Row(f.Soul.World, f.Actor, key) is { Attempts: 1, Failures: 1 }, "Warm decoded state lost new personal feedback.");
    Require(latestScore < priorScore, "Warm assessment did not incorporate the new failure.");
    Replace(f.Soul, node, Wire(new(key, Attempts: 4, Failures: 2)));
    Require(Row(f.Soul.World, f.Actor, key) is { Attempts: 4, Failures: 2 }, "A new observation identity retained the previous decoded DTO.");
    Console.WriteLine(JsonSerializer.Serialize(new { Observation = "personal_feedback_after_warm", PriorScore = priorScore, LatestScore = latestScore }));
});

Run("invalid_new_details_cannot_reuse_warm_dto_and_valid_replacement_recovers", () =>
{
    var f = new ProbeFixture();
    var key = ActionLearning.Key(f.Soul.World, f.Actor, f.First);
    ActionLearning.Observe(f.Soul, f.Actor, key, DecisionOutcomeV1.Attempted);
    var node = MemoryNode(f.Soul.World, f.Actor);
    ActionLearning.Assess(f.Soul.World, f.Actor);
    ActionLearning.Capture(f.Soul.World, f.Actor);
    Replace(f.Soul, node, "{\"Version\":2,\"Experiences\":[]}");
    var invalidGraph = Graph(f);
    Reject(() => ActionLearning.Capture(f.Soul.World, f.Actor), "Capture reused warm valid DTO for invalid new Details.");
    Reject(() => ActionLearning.Assess(f.Soul.World, f.Actor), "Assess reused warm valid DTO for invalid new Details.");
    Reject(() => ActionLearning.Assess(f.Soul.World, f.Actor), "Repeated invalid read reused warm valid DTO.");
    Reject(() => ActionLearning.Observe(f.Soul, f.Actor, key, DecisionOutcomeV1.Attempted), "Observe reused the previous valid DTO.");
    Require(invalidGraph.SequenceEqual(Graph(f)), "Rejected reads or feedback wrote cognition.");
    Replace(f.Soul, node, Wire(new(key, Attempts: 7, Failures: 3)));
    Require(Row(f.Soul.World, f.Actor, key) is { Attempts: 7, Failures: 3 }, "Valid replacement did not recover independently of the old cached state.");
    ActionLearning.Assess(f.Soul.World, f.Actor);
});

Run("other_subject_same_experience_key_has_independent_cache", () =>
{
    var f = new ProbeFixture();
    var key = new DecisionExperienceKeyV1("neutral.same.method", "neutral.same.context");
    ActionLearning.Observe(f.Soul, f.Actor, key, DecisionOutcomeV1.Attempted);
    ActionLearning.Observe(f.Other, f.OtherActor, key, DecisionOutcomeV1.Failed);
    ActionLearning.Assess(f.Soul.World, f.Actor);
    ActionLearning.Assess(f.Other.World, f.OtherActor);
    Require(Row(f.Soul.World, f.Actor, key) is { Attempts: 1, Failures: 0 }, "First subject inherited another subject's outcomes.");
    Require(Row(f.Other.World, f.OtherActor, key) is { Attempts: 0, Failures: 1 }, "Other subject inherited warm state.");
    Replace(f.Other, MemoryNode(f.Other.World, f.OtherActor), "not_json");
    Reject(() => ActionLearning.Capture(f.Other.World, f.OtherActor), "Other subject's invalid observation bypassed decode.");
    Require(Row(f.Soul.World, f.Actor, key) is { Attempts: 1, Failures: 0 }, "Other subject's invalid replacement changed this subject.");
    ActionLearning.Observe(f.Soul, f.Actor, key, DecisionOutcomeV1.Succeeded);
    Require(Row(f.Soul.World, f.Actor, key) is { Attempts: 1, Successes: 1, Failures: 0 }, "Independent subject update retained the other subject's state.");
});

Run("rebound_and_runtime_restored_nodes_do_not_share_old_cache", () =>
{
    var f = new ProbeFixture();
    var key = ActionLearning.Key(f.Soul.World, f.Actor, f.First);
    ActionLearning.Observe(f.Soul, f.Actor, key, DecisionOutcomeV1.Attempted);
    ActionLearning.Assess(f.Soul.World, f.Actor);
    var originalNode = MemoryNode(f.Soul.World, f.Actor);
    var archive = Ok(f.Runtime.CaptureCognitionGraph(f.Soul));
    var save = Ok(f.Runtime.Save());
    Replace(f.Soul, originalNode, Wire(new(key, Attempts: 9, Failures: 5)));
    ActionLearning.Assess(f.Soul.World, f.Actor);
    var detached = Ok(CognitionGraph.Rebind(archive));
    var detachedActor = detached.Projections[f.Actor.SubjectId!.Value];
    var detachedNode = MemoryNode(detached.World, detachedActor);
    Require(detachedNode.SubjectId == originalNode.SubjectId && !ReferenceEquals(detachedNode, originalNode)
        && !ReferenceEquals(detachedNode.LastObservation, originalNode.LastObservation), "Graph restore did not allocate independent node and observation identities.");
    Require(Row(detached.World, detachedActor, key) is { Attempts: 1, Failures: 0 }, "Rebound graph reused original node's latest DTO.");
    ActionLearning.Assess(detached.World, detachedActor);
    var restored = Ok(WorldRuntime.Restore(save));
    var soul = (SoulEssence)restored.Entities[f.Soul.Id];
    var actor = soul.World.Projections.Single(p => p.SubjectId == f.Actor.SubjectId);
    var node = MemoryNode(soul.World, actor);
    Require(node.SubjectId == originalNode.SubjectId && !ReferenceEquals(node, originalNode), "Runtime restore reused a live node identity.");
    Require(Row(soul.World, actor, key) is { Attempts: 1, Failures: 0 }, "Runtime restored node reused original latest DTO.");
    ActionLearning.Assess(soul.World, actor);
    Replace(soul, node, "{\"Version\":1,\"Experiences\":[],\"Unexpected\":1}");
    Reject(() => ActionLearning.Assess(soul.World, actor), "Restored new invalid Details reused warm DTO.");
    Require(Row(f.Soul.World, f.Actor, key) is { Attempts: 9, Failures: 5 }, "Restored invalid observation contaminated original node cache.");
    Require(Row(detached.World, detachedActor, key) is { Attempts: 1, Failures: 0 }, "Restored invalid observation contaminated detached node cache.");
});

Run("parallel_public_learning_updates_keep_every_outcome", () =>
{
    var f = new ProbeFixture();
    var key = ActionLearning.Key(f.Soul.World, f.Actor, f.First);
    const int workers = 6;
    const int repetitions = 40;
    using var start = new ManualResetEventSlim(false);
    var tasks = Enumerable.Range(0, workers).Select(worker => Task.Run(() =>
    {
        start.Wait();
        for (var i = 0; i < repetitions; i++)
        {
            ActionLearning.Observe(f.Soul, f.Actor, key, DecisionOutcomeV1.Attempted);
            ActionLearning.Observe(f.Soul, f.Actor, key, DecisionOutcomeV1.Failed);
            if ((i & 7) == 0)
            {
                ActionLearning.Assess(f.Soul.World, f.Actor);
                ActionLearning.Capture(f.Soul.World, f.Actor);
            }
        }
    })).ToArray();
    start.Set();
    Task.WaitAll(tasks);
    var row = Row(f.Soul.World, f.Actor, key);
    Require(row is { Successes: 0, NoProgress: 0, Interruptions: 0 }
        && row.Attempts == workers * repetitions && row.Failures == workers * repetitions,
        "Concurrent public feedback lost an update or mixed outcomes.");
    Require(f.Soul.World.Projections.Count(node => node.Tags.GetValueOrDefault(new Tag("core.decision-experience.v1")) == 1
        && node.Relations.Contains((Relations.Subject, f.Actor))) == 1, "Concurrent first publication created duplicate experience nodes.");
    Console.WriteLine(JsonSerializer.Serialize(new { Observation = "parallel_public_action_learning", Workers = workers,
        ExpectedAttempts = workers * repetitions, ObservedAttempts = row.Attempts,
        ExpectedFailures = workers * repetitions, ObservedFailures = row.Failures }));
});

Run("warm_assess_and_capture_remain_read_only", () =>
{
    var f = new ProbeFixture();
    var key = ActionLearning.Key(f.Soul.World, f.Actor, f.First);
    ActionLearning.Observe(f.Soul, f.Actor, key, DecisionOutcomeV1.Attempted);
    ActionLearning.Observe(f.Soul, f.Actor, key, DecisionOutcomeV1.NoProgress);
    var before = Graph(f);
    var node = MemoryNode(f.Soul.World, f.Actor);
    var beforeObservation = node.LastObservation;
    for (var i = 0; i < 20; i++)
    {
        ActionLearning.Assess(f.Soul.World, f.Actor);
        ActionLearning.Capture(f.Soul.World, f.Actor);
    }
    Require(before.SequenceEqual(Graph(f)), "Warm read operations changed cognition graph bytes.");
    Require(ReferenceEquals(beforeObservation, node.LastObservation), "Warm read operations replaced an observation.");
});

Run("warm_and_rebound_first_read_performance_observation", () =>
{
    var f = new ProbeFixture();
    for (var i = 0; i < 128; i++)
        ActionLearning.Observe(f.Soul, f.Actor, new("neutral.performance", "neutral.context." + i), DecisionOutcomeV1.Attempted);
    for (var i = 0; i < 12; i++) ActionLearning.Assess(f.Soul.World, f.Actor);
    var snapshot = Ok(f.Soul.World.CaptureSnapshot());
    const int repetitions = 40;
    var cold = Enumerable.Range(0, repetitions).Select(_ => Ok(CognitionGraph.Rebind(snapshot))).ToArray();
    // All graph construction and snapshot validation happen before timing. Each
    // detached graph is assessed once, while warm uses the same published graph.
    var watch = Stopwatch.StartNew();
    watch.Stop();
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    watch.Restart();
    for (var i = 0; i < repetitions; i++) ActionLearning.Assess(f.Soul.World, f.Actor);
    watch.Stop();
    var warmMs = watch.Elapsed.TotalMilliseconds / repetitions;
    var warmBytes = (GC.GetAllocatedBytesForCurrentThread() - allocated) / repetitions;
    allocated = GC.GetAllocatedBytesForCurrentThread();
    watch.Restart();
    foreach (var graph in cold) ActionLearning.Assess(graph.World, graph.Projections[f.Actor.SubjectId!.Value]);
    watch.Stop();
    Console.WriteLine(JsonSerializer.Serialize(new { Observation = "same_snapshot_warm_vs_rebound_first_read",
        WarmupAssessments = 12, MeasurementAssessments = repetitions, ExperienceEntries = 128,
        WarmAverageMilliseconds = warmMs, WarmAverageAllocatedBytes = warmBytes,
        ReboundFirstReadAverageMilliseconds = watch.Elapsed.TotalMilliseconds / repetitions,
        ReboundFirstReadAverageAllocatedBytes = (GC.GetAllocatedBytesForCurrentThread() - allocated) / repetitions,
        Scope = "Whole Assess comparison; rebound first read also initializes snapshot read structures. No isolated cache speedup claim." }));
});

Console.WriteLine(failures.Count == 0 ? "CACHE_PUBLICATION_PROBE: PASS" : "CACHE_PUBLICATION_PROBE: FAIL");
Environment.ExitCode = failures.Count == 0 ? 0 : 1;

sealed class ProbeFixture
{
    public WorldRuntime Runtime { get; } = new();
    public SoulEssence Soul { get; } = new(new EntityId("neutral.observer"), "neutral.observer");
    public SoulEssence Other { get; } = new(new EntityId("neutral.other"), "neutral.other");
    public Projection Actor { get; }
    public Projection OtherActor { get; }
    public Effect First { get; }
    public ProbeFixture()
    {
        var room = new Essence(new EntityId("neutral.room"), "neutral.room", ProjectionKind.Space);
        var body = new Essence(new EntityId("neutral.body"), "neutral.body", ProjectionKind.Body);
        var target = new Essence(new EntityId("neutral.target"), "neutral.target");
        var second = new Essence(new EntityId("neutral.second"), "neutral.second");
        body.Add(new SpatialProperty { Space = room });
        target.Add(new SpatialProperty { Space = room });
        second.Add(new SpatialProperty { Space = room });
        Soul.Relations.Add((Relations.Controls, body));
        Other.Relations.Add((Relations.Controls, body));
        Runtime.Project(Soul, new FacetId("neutral.observer.facet"), "neutral.observer");
        Runtime.Project(Other, new FacetId("neutral.other.facet"), "neutral.other");
        var rr = Runtime.Project(room, new FacetId("neutral.room.facet"), "neutral.room");
        var rb = Runtime.Project(body, new FacetId("neutral.body.facet"), "neutral.body");
        var rt = Runtime.Project(target, new FacetId("neutral.target.facet"), "neutral.target");
        var rs = Runtime.Project(second, new FacetId("neutral.second.facet"), "neutral.second");
        var known = Runtime.Observe(Soul, [rr, rb, rt, rs]);
        var other = Runtime.Observe(Other, [rr, rb, rt, rs]);
        Actor = known[1]; OtherActor = other[1];
        First = new Effect.Act(ActionOp.Inspect, known[2]);
        void Rule(SoulEssence soul, string name, Effect effect, int score)
            => Observation.Observe(soul, new(name, ProjectionKind.Concept) { Source = ObservationSource.Introspection,
                Existence = Truth.True, Rules = [new(new Condition.Constant(Truth.True), RuleMode.Prefer, effect, score, Truth.True)] });
        Rule(Soul, "neutral.first.rule", First, 20);
        Rule(Soul, "neutral.second.rule", new Effect.Act(ActionOp.Inspect, known[3]), 10);
        Rule(Other, "neutral.first.rule", new Effect.Act(ActionOp.Inspect, other[2]), 20);
        Rule(Other, "neutral.second.rule", new Effect.Act(ActionOp.Inspect, other[3]), 10);
    }
}
