using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;

// DETECTION_ONLY. Neutral experimental adapter; not a production snapshot, codec or new Core.
var results = new List<object>();
void Record(string id, object actual) => results.Add(new { id, actual });
void Check(bool success, string message) { if (!success) throw new Exception(message); }
string Reject(Action action)
{
    try { action(); return "NOT_REJECTED"; }
    catch (Exception error) { return error.GetType().Name + ": " + error.Message; }
}
var valueTag = new Tag("量");

// E01: hidden objective mutation must not affect subjective queries or terms.
{
    var runtime = new WorldRuntime(); var soul = new SoulEssence("主体甲");
    var entity = new Essence("样本甲"); var state = entity.Add(new StateProperty());
    state.Values[valueTag] = 1; state.Observable.Add(valueTag);
    var view = runtime.Project(entity, "样本甲");
    var mental = runtime.Observe(soul, [view]).Single();
    var registry = new ReferentRegistry(); var old = Snapshot.Capture(soul, registry, 1);
    state.Values[valueTag] = 9; runtime.Sync();
    var freshWithoutObservation = Snapshot.Capture(soul, registry, 1);
    var id = registry.Id(mental);
    var left = old.TagAtLeast(id, valueTag, 5); var right = freshWithoutObservation.TagAtLeast(id, valueTag, 5);
    Check(left == Truth.False && right == Truth.False && mental.Tags[valueTag] == 1, "hidden reality leaked");
    Record("E01", new { objective = state.Values[valueTag], subjective = mental.Tags[valueTag], oldQuery = left.ToString(), newQueryWithoutObservation = right.ToString(), termCandidatesEqual = old.Resolve("样本甲").SequenceEqual(freshWithoutObservation.Resolve("样本甲")) });
}

// E02: one Chinese term names two viable subjective facets, including one Essence with two surfaces.
{
    var runtime = new WorldRuntime(); var soul = new SoulEssence("主体甲");
    var entity = new Essence("中性对象"); var surfaces = entity.Add(new SurfaceProperty());
    var a = runtime.Project(entity, "样本"); var b = runtime.Project(entity, "样本");
    surfaces.Surfaces[a] = ImmutableDictionary<Tag,int>.Empty.Add(valueTag, 1);
    surfaces.Surfaces[b] = ImmutableDictionary<Tag,int>.Empty.Add(valueTag, 2); runtime.Sync();
    var held = runtime.Observe(soul, [a,b]);
    var decoder = new AstDecoder(soul.World, [valueTag]);
    var ambiguity = Reject(() => decoder.DecodeEffect("inspect(@样本)"));
    var explicitEffect = decoder.DecodeEffect("inspect(@0)", held[1]);
    var registry = new ReferentRegistry(); var snapshot = Snapshot.Capture(soul, registry, 1);
    Check(snapshot.Resolve("样本").Count == 2 && ambiguity.Contains("有歧义"), "ambiguity lost");
    Check(ReferenceEquals(((Noun.Reference)((Effect.Act)explicitEffect).Target).Projection, held[1]), "explicit reference wrong");
    Record("E02", new { realityRepresentations = runtime.Representations(entity).Count, mentalRepresentations = held.Length, candidates = snapshot.Resolve("样本"), nameDecode = ambiguity, explicitEmbedding = "SECOND_SUBJECTIVE_PROJECTION", knownProjectionReturnsFirstOnly = ReferenceEquals(runtime.KnownProjection(soul, entity), held[0]) });
    // Formal CaseField string matching silently picks the first matching projection.
    var model = new CaseModel("中性实验", ["选项"], [new[] { "甲", "乙" }], [new CaseField("样本",valueTag,"量")], [
        new CaseHypothesis([0],ImmutableDictionary<string,int>.Empty.Add("量",1)),
        new CaseHypothesis([1],ImmutableDictionary<string,int>.Empty.Add("量",2))]);
    var belief = Reasoner.Assess(soul.World, model);
    Check(belief.Candidates.SequenceEqual([0]), "unexpected Reasoner result");
    Record("E02-Reasoner", new { candidates = belief.Candidates, observedBehavior = "FIRST_MATCH_USED_DESPITE_TWO_REFERENTS", warning = "Do not route an unresolved lexical term into CaseField.Projection." });
}

// E03: deterministic observation update between leaves of the formal Core All aggregator.
// This establishes a mixed-version possibility, not a simultaneous-thread proof about Ast.Evaluate.
{
    var soul = new SoulEssence("主体甲");
    var p = Observation.Observe(soul, new("样本甲") { Existence = Truth.True, Tags = ImmutableDictionary<Tag,int?>.Empty.Add(valueTag,1) });
    var registry = new ReferentRegistry(); var old = Snapshot.Capture(soul, registry, 1); var id = registry.Id(p);
    var liveReads = new List<string>();
    IEnumerable<Truth> ReadLive()
    {
        var first = Ast.Evaluate(new Condition.Tagged(p,valueTag,1), new(p)); liveReads.Add(first.ToString()); yield return first;
        Observation.Observe(soul, new(p.Name,p.Kind) { Target=p, Tags=ImmutableDictionary<Tag,int?>.Empty.Add(valueTag,0) });
        var second = Ast.Evaluate(new Condition.Tagged(p,valueTag,1), new(p)); liveReads.Add(second.ToString()); yield return second;
    }
    var mixed = Ast.All(ReadLive());
    // Reset, capture, then repeat the same update while evaluating only frozen subjective projections.
    Observation.Observe(soul, new(p.Name,p.Kind) { Target=p, Tags=ImmutableDictionary<Tag,int?>.Empty.Add(valueTag,1) });
    var frozen = Snapshot.Capture(soul, registry, 2); var frozenReads = new List<string>();
    IEnumerable<Truth> ReadFrozen()
    {
        var first = frozen.TagAtLeast(id,valueTag,1); frozenReads.Add(first.ToString()); yield return first;
        Observation.Observe(soul, new(p.Name,p.Kind) { Target=p, Tags=ImmutableDictionary<Tag,int?>.Empty.Add(valueTag,0) });
        var second = frozen.TagAtLeast(id,valueTag,1); frozenReads.Add(second.ToString()); yield return second;
    }
    var consistent = Ast.All(ReadFrozen());
    Check(mixed == Truth.False && consistent == Truth.True, "version discrimination failed");
    Record("E03", new { injection="OBSERVATION_BETWEEN_CORE_ALL_LEAVES", liveReads, liveResult=mixed.ToString(), frozenReads, frozenResult=consistent.ToString(), oldSnapshotStillTrue=old.TagAtLeast(id,valueTag,1).ToString(), captureAtomicity="QUIESCENT_SINGLE_THREAD_ONLY; NOT_PROVEN_UNDER_CONCURRENT_WRITES" });
}

// E04: neutral entity additions/forgetting need no hardcoded entity enum and never reuse an old referent.
{
    var soul=new SoulEssence("主体甲"); var registry=new ReferentRegistry();
    var a=Observation.Observe(soul,new("样本甲") { Existence=Truth.True });
    var first=Snapshot.Capture(soul,registry,1);
    var b=Observation.Observe(soul,new("样本乙") { Existence=Truth.True });
    var expanded=Snapshot.Capture(soul,registry,2);
    Observation.Observe(soul,new(a.Name,a.Kind) { Target=a,Forget=true });
    var removed=Snapshot.Capture(soul,registry,3);
    var replacement=Observation.Observe(soul,new("样本甲") { Existence=Truth.True });
    var replaced=Snapshot.Capture(soul,registry,4);
    Check(expanded.Resolve("样本乙").Count==1 && removed.Resolve("样本甲").Count==0 && registry.Id(a)!=registry.Id(replacement), "dynamic registry failed");
    Record("E04",new { before=first.Resolve("样本甲"), added=expanded.Resolve("样本乙"), afterForget=removed.Resolve("样本甲"), replacement=replaced.Resolve("样本甲"), oldId=registry.Id(a),newId=registry.Id(replacement),hardcodedEntityEnum=false });
}

// E05: History stores immutable Tags/Relations but its Target is a live reference; Forget removes records.
{
    var soul=new SoulEssence("主体甲"); var registry=new ReferentRegistry();
    var p=Observation.Observe(soul,new("旧称") { Existence=Truth.True,Time=1,Tags=ImmutableDictionary<Tag,int?>.Empty.Add(valueTag,1) });
    var original=soul.World.History.Last(); var snapshot=Snapshot.Capture(soul,registry,1);
    Observation.Observe(soul,new("新称",ProjectionKind.Concept) { Target=p,Time=2,Tags=ImmutableDictionary<Tag,int?>.Empty.Add(valueTag,2) });
    var historyFields=typeof(ObservationEvent).GetProperties().Select(v=>v.Name).ToArray();
    var renamedHistoricalTarget=original.Target.Name;
    var oldFrozenName=snapshot.Name(registry.Id(p));
    Observation.Observe(soul,new(p.Name,p.Kind) { Target=p,Forget=true });
    Check(renamedHistoricalTarget=="新称" && oldFrozenName=="旧称" && soul.World.History.Count==0, "history behavior changed");
    Record("E05",new { eventTargetNameAfterRename=renamedHistoricalTarget,frozenOldName=oldFrozenName,oldEventCapturedTag=original.Tags[valueTag],historyFields,historyCountAfterForget=soul.World.History.Count,fullHistoryRebuild="NOT_SUPPORTED_BY_REVIEWED_HISTORY_SHAPE",crossProcessCoreSaveRestore="NOT_VERIFIED; NO_CODEC_IN_REVIEWED_PUBLIC_TYPES",stableIdAcrossProcesses="EXPERIMENT_REGISTRY_DOES_NOT_PROVIDE_IT" });
}

// E06: ambiguity, unsupported syntax, and Unknown must remain separate from False.
{
    var soul=new SoulEssence("主体甲"); var p=Observation.Observe(soul,new("样本甲"));
    var unknown=Ast.Evaluate(new Condition.Tagged(p,valueTag,1),new(p));
    Observation.Observe(soul,new(p.Name,p.Kind) { Target=p,Tags=ImmutableDictionary<Tag,int?>.Empty.Add(valueTag,0) });
    var falseValue=Ast.Evaluate(new Condition.Tagged(p,valueTag,1),new(p));
    Observation.Observe(soul,new(p.Name,p.Kind) { Target=p,Tags=ImmutableDictionary<Tag,int?>.Empty.Add(valueTag,1) });
    var trueValue=Ast.Evaluate(new Condition.Tagged(p,valueTag,1),new(p));
    var unsupported=Reject(()=>new AstDecoder(soul.World,[valueTag]).DecodeEffect("identify(@样本甲)"));
    Check(unknown==Truth.Unknown && falseValue==Truth.False && trueValue==Truth.True && unsupported.StartsWith(nameof(AstParseException)),"truth/status distinction failed");
    Record("E06",new { unknown=unknown.ToString(),falseValue=falseValue.ToString(),trueValue=trueValue.ToString(),unsupported,claim="UNSUPPORTED_IS_NOT_A_TRUTH_VALUE" });
}

// E07: identity survives renaming, but existing association may not silently rebind to another Essence.
{
    var runtime=new WorldRuntime(); var soul=new SoulEssence("主体甲");
    var a=runtime.Project(new Essence("对象甲"),"样本甲"); var b=runtime.Project(new Essence("对象乙"),"样本乙");
    var p=runtime.Observe(soul,[a]).Single(); var registry=new ReferentRegistry(); var id=registry.Id(p);
    Observation.Observe(soul,new("改名后") { Target=p });
    var rebound=Reject(()=>runtime.Associate(soul,p,b));
    Check(registry.Id(p)==id && rebound.Contains("不能静默改绑"),"identity continuity broken");
    Record("E07",new { originalId=id,renamedId=registry.Id(p),name=p.Name,rebind=rebound });
}

// E08: official execution must distinguish an invalid submission, objective disappearance,
// and forgetting after a previously valid delayed action was accepted.
{
    var fixture=Fixture.Make(); using var simulation=new SimulationSession(fixture.Runtime);
    var rejectedAfterForget="";
    Observation.Observe(fixture.Soul,new(fixture.Target.Name) { Target=fixture.Target,Forget=true });
    var staleRead=Ast.Evaluate(new Condition.Exists(fixture.Target),new(fixture.Self));
    var staleDecode=Reject(()=>new AstDecoder(fixture.Soul.World).DecodeEffect("inspect(@0)",fixture.Target));
    rejectedAfterForget=Reject(()=>simulation.Submit(fixture.Soul,new(fixture.Self,new Effect.Set(fixture.Target,valueTag,7),1)));
    Check(staleRead==Truth.True && staleDecode.Contains("当前 ProjectionWorld") && rejectedAfterForget.Contains("主体内部世界"),"stale handling differs");
    Record("E08-before-submit",new { staleRawAstTruth=staleRead.ToString(),embeddingDecode=staleDecode,submission=rejectedAfterForget });
}
{
    var fixture=Fixture.Make(); using var simulation=new SimulationSession(fixture.Runtime);
    var execution=simulation.Submit(fixture.Soul,new(fixture.Self,new Effect.Set(fixture.Target,valueTag,7),1));
    fixture.Runtime.Remove(fixture.Object); simulation.CTB.RunUntil(1);
    Check(execution.Status==ExecutionStatus.Failed && execution.Result?.Failure==ActionFailure.MissingObject && fixture.Object.Get<StateProperty>()!.Values[valueTag]==0,"objective loss unsafe");
    Record("E08-objective-remove",new { status=execution.Status.ToString(),failure=execution.Result?.Failure.ToString(),objectiveValue=fixture.Object.Get<StateProperty>()!.Values[valueTag] });
}
{
    var fixture=Fixture.Make(); using var simulation=new SimulationSession(fixture.Runtime);
    var execution=simulation.Submit(fixture.Soul,new(fixture.Self,new Effect.Set(fixture.Target,valueTag,7),1));
    Observation.Observe(fixture.Soul,new(fixture.Target.Name) { Target=fixture.Target,Forget=true }); simulation.CTB.RunUntil(1);
    Check(execution.Status==ExecutionStatus.Completed && fixture.Object.Get<StateProperty>()!.Values[valueTag]==7,"queued forget behavior changed");
    Record("E08-forget-after-submit",new { targetStillInMind=fixture.Soul.World.Contains(fixture.Target),status=execution.Status.ToString(),failure=execution.Result?.Failure.ToString(),objectiveValue=fixture.Object.Get<StateProperty>()!.Values[valueTag],contractGap="PHYSICAL_TARGET_MEMBERSHIP_NOT_RECHECKED_AT_ACTIVATION" });
}

Console.WriteLine(JsonSerializer.Serialize(new { schema="D01B-neutral-core-experiments/v1",mode="DETECTION_ONLY", baseline="7489db56a714724eb7cb735958dd879dc662d238",passedAssertions=true,results },new JsonSerializerOptions { WriteIndented=true }));

sealed class ReferentRegistry
{
    private readonly Dictionary<Projection,string> ids=[]; private int serial;
    public string Id(Projection projection)
    {
        if (!ids.TryGetValue(projection,out var id)) ids.Add(projection,id="subject-ref:"+(++serial));
        return id;
    }
}

sealed class Snapshot
{
    private readonly SoulEssence frozen;
    private readonly ImmutableDictionary<string,Projection> byId;
    private readonly ImmutableDictionary<string,ImmutableArray<string>> terms;
    public long Revision { get; }
    private Snapshot(SoulEssence frozen, ImmutableDictionary<string,Projection> byId, ImmutableDictionary<string,ImmutableArray<string>> terms,long revision)
        => (this.frozen,this.byId,this.terms,Revision)=(frozen,byId,terms,revision);
    public IReadOnlyList<string> Resolve(string term)=>terms.GetValueOrDefault(term,[]);
    public string Name(string id)=>byId[id].Name;
    public Truth TagAtLeast(string id,Tag tag,int minimum)=>Ast.Evaluate(new Condition.Tagged(byId[id],tag,minimum),new(byId[id]));
    // Quiescence is an experimental precondition. No claim of atomic Core capture under concurrent mutation.
    public static Snapshot Capture(SoulEssence soul,ReferentRegistry registry,long revision)
    {
        var frozen=new SoulEssence("中性冻结容器"); var live=soul.World.Projections;
        var copies=live.ToDictionary(p=>p,p=>Observation.Observe(frozen,new(p.Name,p.Kind)));
        foreach (var p in live)
            Observation.Observe(frozen,new(p.Name,p.Kind) { Target=copies[p],Existence=p.Existence,Move=true,Parent=p.Parent is null?null:copies[p.Parent],
                Tags=p.Tags.ToImmutableDictionary(v=>v.Key,v=>(int?)v.Value),
                AddRelations=p.Relations.Select(v=>(v.Relation,copies[v.Target])).ToImmutableHashSet(),
                DenyRelations=p.DeniedRelations.Select(v=>(v.Relation,copies[v.Target])).ToImmutableHashSet() });
        return new(frozen,live.ToImmutableDictionary(registry.Id,p=>copies[p]),
            live.GroupBy(p=>p.Name,StringComparer.Ordinal).ToImmutableDictionary(g=>g.Key,g=>g.Select(registry.Id).ToImmutableArray(),StringComparer.Ordinal),revision);
    }
}

sealed record Fixture(WorldRuntime Runtime,SoulEssence Soul,Essence Object,Projection Self,Projection Target)
{
    public static Fixture Make()
    {
        var runtime=new WorldRuntime(); var room=new Essence("中性空间",ProjectionKind.Space);
        var roomView=runtime.Project(room,room.TrueName);
        var body=new Essence("主体甲",ProjectionKind.Body); body.Add(new SpatialProperty { Space=room });
        var bodyView=runtime.Project(body,body.TrueName);
        var soul=new SoulEssence("主体甲之魂"); soul.Relations.Add((Relations.Controls,body)); runtime.Project(soul,soul.TrueName);
        var entity=new Essence("样本甲"); entity.Add(new SpatialProperty { Space=room });
        var state=entity.Add(new StateProperty()); state.Values[new("量")]=0; state.Observable.Add(new("量"));
        var entityView=runtime.Project(entity,entity.TrueName);
        var held=runtime.Observe(soul,[roomView,bodyView,entityView]);
        return new(runtime,soul,entity,held.Single(p=>p.Kind==ProjectionKind.Body),held.Single(p=>p.Name=="样本甲"));
    }
}
