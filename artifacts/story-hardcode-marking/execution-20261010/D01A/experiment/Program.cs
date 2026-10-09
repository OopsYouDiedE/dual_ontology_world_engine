using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

// Neutral fixture only: every AST is evaluated/submitted by unchanged production APIs.
var results = new List<object>();
void Record(string id, object observed) => results.Add(new { id, observed });
string ExceptionOf(Action action)
{
    try { action(); return "NO_EXCEPTION"; }
    catch (Exception exception) { return exception.GetType().Name + ": " + exception.Message; }
}
DialogueExpression Literal(object value) => new() { Value = JsonSerializer.SerializeToElement(value) };
WorldPropertyDefinition Prop(string type, object value) => new() { Type = type, Value = JsonSerializer.SerializeToElement(value) };
ChapterWorldDefinition Chapter(string id = "probe") => new()
{
    Id = id,
    Entities = [
        new() { Id = "room", Name = "空间", Kind = "room", Scope = "world" },
        new() { Id = "actor", Name = "主体", Kind = "character", Scope = "world", Properties = new() { ["space"] = Prop("entity", "world:room") } },
        new() { Id = "marker", Name = "件", Kind = "object", Scope = "world", Properties = new() {
            ["space"] = Prop("entity", "world:room"), ["state:signal"] = Prop("integer", 0),
            ["state:counter"] = Prop("integer", 0), ["observable:signal"] = Prop("boolean", true) } }
    ],
    Candidates = [new() {
        Id = "adjust", Kind = "option", Actor = "player", Target = "world:marker", Label = "中性选项", Delay = 0,
        Policy = new() { Mode = "player" },
        Applicability = new() { Op = "eq", Args = [new() { Op = "call", Name = "state", Args = [Literal("world:marker"), Literal("signal")] }, Literal(0)] },
        Response = [new() { Op = "call", Name = "change", Args = [Literal("world:marker"), Literal("counter"), Literal(1)] }]
    }]
};
int OfferCount(WorldSession session) => session.Process(new RequestCandidates("world:actor", "world:marker")).OfType<CandidatesAvailable>().Single().Candidates.Count;

using (var session = new WorldSession())
{
    session.Load(Chapter());
    var actor = session.Character("world:actor");
    var marker = session.Entity("world:marker");
    session.Process(new VerifiedPerception("world:actor", ["world:marker"]));
    var mental = session.Runtime.KnownProjection(actor.Soul, marker)!;
    var before = OfferCount(session);
    var observedBefore = mental.LastObservation;
    marker.Get<StateProperty>()!.Values[new("signal")] = 1;
    session.Runtime.Sync();
    var afterHidden = OfferCount(session);
    var mentalAfterHidden = mental.Tags[new("signal")];
    var sameObservation = ReferenceEquals(observedBefore, mental.LastObservation);
    var choice = session.Process(new RequestCandidates("world:actor", "world:marker")).OfType<CandidatesAvailable>().Single().Candidates.Single();
    session.Process(new SelectCandidate(choice.Token));
    var completion = session.Process(new AdvanceWorld(0)).OfType<CandidateActionFinished>().Single();
    session.Process(new VerifiedPerception("world:actor", ["world:marker"]));
    var afterPerception = OfferCount(session);
    Record("P01", new { before, afterHidden, mentalAfterHidden, sameObservation, completion.Succeeded, completion.Failure,
        counter = marker.Get<StateProperty>()!.Values[new("counter")], afterPerception, mentalAfterPerception = mental.Tags[new("signal")] });
}

{
    var runtime = new WorldRuntime();
    using var simulation = new SimulationSession(runtime);
    var world = new CoreScriptWorld(runtime, simulation);
    var room = world.DefineSpace("r", "空间");
    var actor = world.DefineCharacter("a", "主体", room);
    var token = world.DefineObject("t", "件", room);
    var first = runtime.Representations(token).Single();
    var second = runtime.Project(token, "件");
    var capability = new Tag("probe_affordance");
    var counter = new Tag("counter");
    var surface = token.Add(new SurfaceProperty());
    surface.Surfaces[first] = ImmutableDictionary<Tag, int>.Empty.Add(capability, 1).Add(new("facet_mark"), 10);
    surface.Surfaces[second] = ImmutableDictionary<Tag, int>.Empty.Add(capability, 1).Add(new("facet_mark"), 20);
    runtime.Sync();
    var mental = runtime.Observe(actor.Soul, [first, second], 1);
    var decoder = new AstDecoder(actor.Soul.World, [counter]);
    var ambiguous = ExceptionOf(() => decoder.DecodeEffect("change(@\"件\", counter, 1)"));
    var rule = new Rule(new Condition.All([new Condition.IsKind(new Noun.Slot(RuleSlot.Target), ProjectionKind.Object),
        new Condition.Tagged(new Noun.Slot(RuleSlot.Target), capability, 1)]), RuleMode.Prefer,
        new Effect.Change(new Noun.Slot(RuleSlot.Target), counter, 1), Acceptance: Truth.True);
    Observation.Observe(actor.Soul, new("中性规则", ProjectionKind.Concept) { Existence = Truth.True, Rules = [rule] });
    var candidates = DecisionSystem.Assess(actor.Soul.World, actor.Self);
    var executions = new List<string>();
    foreach (var projection in mental)
    {
        var effect = decoder.DecodeEffect("change(@0, counter, 1)", projection);
        var execution = simulation.Submit(actor.Soul, new(actor.Self, effect, 0));
        simulation.CTB.RunUntil(simulation.CTB.Now);
        executions.Add(execution.Status + ":" + execution.Result!.Failure);
    }
    var oldFirst = mental[0].LastObservation;
    var oldSecond = mental[1].LastObservation;
    var inspect = simulation.Submit(actor.Soul, new(actor.Self, new Effect.Act(ActionOp.Inspect, mental[1]), 0));
    simulation.CTB.RunUntil(simulation.CTB.Now);
    Record("P04_CORE", new { representations = runtime.Representations(token).Count, separateMentalReferences = !ReferenceEquals(mental[0], mental[1]),
        sameWord = mental[0].Name == mental[1].Name, ambiguous, candidateCount = candidates.Count, executions,
        objectiveCounter = token.Get<StateProperty>()!.Values[counter], inspectStatus = inspect.Status,
        inspectSelectedSecondRefreshed = !ReferenceEquals(oldSecond, mental[1].LastObservation),
        inspectFirstRefreshed = !ReferenceEquals(oldFirst, mental[0].LastObservation),
        knownProjectionSelectsFirst = ReferenceEquals(runtime.KnownProjection(actor.Soul, token), mental[0]) });
}
using (var session = new WorldSession())
{
    session.Load(Chapter());
    session.Runtime.Project(session.Entity("world:marker"), "件");
    Record("P04_SESSION", new { result = ExceptionOf(() => session.Process(new VerifiedPerception("world:actor", ["world:marker"]))) });
}

{
    var soul = new SoulEssence("主体");
    var tag = new Tag("revision_probe");
    var p = Observation.Observe(soul, new("件") { Existence = Truth.True, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(tag, 1) });
    var oldOnly = new Condition.Not(new Condition.Tagged(p, tag, 2));
    var newOnly = new Condition.Tagged(p, tag, 2);
    var items = ImmutableArray.CreateBuilder<Condition>(250002);
    items.Add(oldOnly);
    for (var i = 0; i < 250000; i++) items.Add(new Condition.Constant(Truth.True));
    items.Add(newOnly);
    var conjunction = new Condition.All(items.ToImmutable());
    void Update(int value, int time) => Observation.Observe(soul, new(p.Name, p.Kind) { Target = p, Time = time,
        Tags = ImmutableDictionary<Tag, int?>.Empty.Add(tag, value) });
    var before = Ast.Evaluate(conjunction, new(p));
    Update(2, 1);
    var after = Ast.Evaluate(conjunction, new(p));
    var attempts = new List<object>();
    for (var i = 0; i < 6; i++)
    {
        Update(1, 2 + i * 2);
        using var start = new ManualResetEventSlim();
        var stopwatch = Stopwatch.StartNew();
        var reader = Task.Run(() => { start.Set(); return Ast.Evaluate(conjunction, new(p)); });
        start.Wait();
        Thread.Sleep(2);
        var mutationAtMs = stopwatch.Elapsed.TotalMilliseconds;
        Update(2, 3 + i * 2);
        var mixed = reader.GetAwaiter().GetResult();
        attempts.Add(new { attempt = i + 1, result = mixed.ToString(), mutationAtMs, endedAtMs = stopwatch.Elapsed.TotalMilliseconds });
    }
    Record("P05", new { coherentBefore = before.ToString(), coherentAfter = after.ToString(), fillerCount = 250000, attempts,
        explanation = "True requires the first leaf to read value 1 and the final leaf to read value 2; no Core hook or custom AST node was used." });
}

{
    var runtime = new WorldRuntime();
    using var simulation = new SimulationSession(runtime);
    var world = new CoreScriptWorld(runtime, simulation);
    var room = world.DefineSpace("r", "空间");
    var a = world.DefineCharacter("a", "主体甲", room);
    var b = world.DefineCharacter("b", "主体乙", room);
    var ea = world.DefineObject("ea", "件", room);
    var eb = world.DefineObject("eb", "件", room);
    var pa = runtime.Observe(a.Soul, runtime.Representations(ea)).Single();
    var pb = runtime.Observe(b.Soul, runtime.Representations(eb)).Single();
    var da = new AstDecoder(a.Soul.World, [new("counter")]);
    var db = new AstDecoder(b.Soul.World, [new("counter")]);
    var fa = da.DecodeEffect("change(@\"件\", counter, 1)");
    var fb = db.DecodeEffect("change(@\"件\", counter, 1)");
    var xa = simulation.Submit(a.Soul, new(a.Self, fa, 0));
    var xb = simulation.Submit(b.Soul, new(b.Self, fb, 0));
    simulation.CTB.RunUntil(0);
    Record("N02", new { sameWordDifferentPrivateReference = !ReferenceEquals(pa, pb),
        aCounter = ea.Get<StateProperty>()!.Values[new("counter")], bCounter = eb.Get<StateProperty>()!.Values[new("counter")],
        aStatus = xa.Status.ToString(), bStatus = xb.Status.ToString(),
        foreignEmbedding = ExceptionOf(() => da.DecodeEffect("inspect(@0)", pb)),
        unknownWord = ExceptionOf(() => da.DecodeEffect("inspect(@\"未见件\")")),
        unsupportedSyntax = ExceptionOf(() => da.DecodeEffect("warp(@\"件\")")) });
    var nfc = Observation.Observe(a.Soul, new("é") { Existence = Truth.True });
    var nfd = Observation.Observe(a.Soul, new("e\u0301") { Existence = Truth.True });
    var nfcRef = (Noun.Reference)((Effect.Act)da.DecodeEffect("inspect(@\"é\")")).Target;
    var nfdRef = (Noun.Reference)((Effect.Act)da.DecodeEffect("inspect(@\"e\u0301\")")).Target;
    Record("N01", new { canonicalEquivalentButSeparate = !ReferenceEquals(nfcRef.Projection, nfdRef.Projection),
        nfcBoundCorrectly = ReferenceEquals(nfc, nfcRef.Projection), nfdBoundCorrectly = ReferenceEquals(nfd, nfdRef.Projection) });
}
{
    var soul = new SoulEssence("主体");
    var tag = new Tag("value");
    var p = Observation.Observe(soul, new("旧称") { Existence = Truth.True, Time = 1, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(tag, 1) });
    var old = p.LastObservation!;
    Observation.Observe(soul, new("新称", ProjectionKind.Concept) { Target = p, Existence = Truth.False, Time = 2,
        Tags = ImmutableDictionary<Tag, int?>.Empty.Add(tag, 2) });
    var beforeForget = soul.World.History.Count;
    var oldTargetNameNow = old.Target.Name;
    var oldTargetKindNow = old.Target.Kind.ToString();
    var oldTargetExistenceNow = old.Target.Existence.ToString();
    Observation.Observe(soul, new(p.Name, p.Kind) { Target = p, Forget = true, Time = 3 });
    Record("H01", new { oldTagSnapshot = old.Tags[tag], oldExistenceSnapshot = old.Existence!.Value.ToString(),
        oldTargetNameNow, oldTargetKindNow, oldTargetExistenceNow, beforeForget, historyAfterForget = soul.World.History.Count,
        aliveAfterForget = soul.World.Contains(p), saveMigration = "NOT_VERIFIED: no historical save fixture or codec supplied" });
}
{
    var runtime = new WorldRuntime();
    using var simulation = new SimulationSession(runtime);
    var world = new CoreScriptWorld(runtime, simulation);
    var room = world.DefineSpace("r", "空间");
    var actor = world.DefineCharacter("a", "主体", room);
    var token = world.DefineObject("t", "件", room);
    var mental = world.Observe(actor, token).Single();
    var delayed = simulation.Submit(actor.Soul, new(actor.Self, new Effect.Change(mental, new("counter"), 1), 1));
    runtime.Remove(token);
    simulation.CTB.RunUntil(1);
    var imaginary = Observation.Observe(actor.Soul, new("中性未绑定概念") { Existence = Truth.True });
    var unbound = simulation.Submit(actor.Soul, new(actor.Self, new Effect.Change(imaginary, new("counter"), 1), 0));
    simulation.CTB.RunUntil(1);
    var bare = world.DefineObject("bare", "无锁件", room);
    var tool = world.DefineObject("tool", "工具", room);
    tool.Get<SpatialProperty>()!.Holder = actor.Body;
    tool.Get<SpatialProperty>()!.Space = null;
    runtime.Sync();
    var bareMental = world.Observe(actor, bare).Single();
    var toolMental = world.Observe(actor, tool).Single();
    var unsupported = simulation.Submit(actor.Soul, new(actor.Self, new Effect.Act(ActionOp.Unlock, bareMental, toolMental), 0));
    simulation.CTB.RunUntil(1);
    Record("X01", new { removedBeforeActivation = delayed.Result!.Failure.ToString(), unboundPrivateReference = unbound.Result!.Failure.ToString(),
        missingCapability = unsupported.Result!.Failure.ToString(), staleMentalExistence = mental.Existence.ToString(),
        deadRealityExistence = runtime.Representations(token).Single().Existence.ToString(),
        knownProjectionStillReturnsStaleMental = ReferenceEquals(runtime.KnownProjection(actor.Soul, token), mental) });
    var additions = Enumerable.Range(0, 32).Select(i => world.DefineObject("neutral:" + i, "中性件" + i, room)).ToArray();
    foreach (var entity in additions.Take(16)) runtime.Remove(entity);
    Record("E01_CORE", new { added = additions.Length, physicalRemoved = additions.Take(16).Count(e => runtime.Representations(e).Single().Existence == Truth.False),
        registryEntriesRetained = additions.Count(world.Objects.Values.Contains), usedKind = ProjectionKind.Object.ToString(), newEnumRequired = false });
}
using (var session = new WorldSession())
{
    session.Load(Chapter());
    var entity = session.Entity("world:marker");
    var actor = session.Character("world:actor");
    session.Process(new VerifiedPerception("world:actor", ["world:marker"]));
    var mental = session.Runtime.KnownProjection(actor.Soul, entity)!;
    var history = actor.Soul.World.History.Count;
    session.Load(new() { Id = "next", Requires = ["probe"], Entities = [new() { Id = "fresh", Name = "新件", Kind = "object", Scope = "chapter",
        Properties = new() { ["space"] = Prop("entity", "world:room") } }] });
    Record("E01_SESSION", new { entityReused = ReferenceEquals(entity, session.Entity("world:marker")), soulReused = ReferenceEquals(actor.Soul, session.Character("world:actor").Soul),
        mentalReused = ReferenceEquals(mental, session.Runtime.KnownProjection(actor.Soul, entity)), oldHistoryCount = history, newHistoryCount = actor.Soul.World.History.Count,
        newEntityFound = session.Entity("chapter:next:fresh").TrueName, saveRoundTrip = "NOT_VERIFIED" });
}
Console.WriteLine(JsonSerializer.Serialize(new { schema = "D01A-neutral-observations/v1", actualExecutor = "unchanged linked Core source and unchanged shared WorldSession", results },
    new JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }));
