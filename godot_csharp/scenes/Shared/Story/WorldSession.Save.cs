using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using DualOntologyWorldEngine.Core;
using static DualOntologyWorldEngine.Story.SessionPersistence;

namespace DualOntologyWorldEngine.Story;

public sealed partial class WorldSession
{
    public ChapterSession Progress { get; private set; }=new();
    // No random consumer exists in generic WorldSession. A versioned stream is available to host adapters.
    public ulong RandomState { get; private set; }=0x9e3779b97f4a7c15UL;
    public ulong NextRandom()
    { lock(_gate) { var x=RandomState; x^=x>>12; x^=x<<25; x^=x>>27; RandomState=x; return unchecked(x*2685821657736338717UL); } }
    private Func<ContractResult<bool>>? _hostSaveGuard;
    public void SetSavePointGuard(Func<ContractResult<bool>> guard) { lock(_gate) _hostSaveGuard=guard ?? throw new ArgumentNullException(nameof(guard)); }
    public ContractResult<bool> CheckSavePoint() => Try(() =>
    {
        lock(_gate)
        {
            Require(!_disposed,"session.disposed",ContractErrorCode.UnsupportedSavePoint);
            Value(Simulation.CheckSavePoint());
            Require(_choiceTokens.Count==0,"host.choice",ContractErrorCode.UnsupportedSavePoint);
            Require(_presentations.Count==0,"host.presentation",ContractErrorCode.UnsupportedSavePoint);
            Require(_executions.Count==0 && _outputs.Count==0,"host.outputs",ContractErrorCode.UnsupportedSavePoint);
            Require(_ownsSimulation || _restoreOwnsSimulation || _hostSaveGuard is not null,"host.provider",ContractErrorCode.UnsupportedSavePoint);
            if(_hostSaveGuard is not null) Value(_hostSaveGuard());
            return true;
        }
    });
    private sealed record EntitySave([property:System.Text.Json.Serialization.JsonRequired] string Id,[property:System.Text.Json.Serialization.JsonRequired] string Name,[property:System.Text.Json.Serialization.JsonRequired] string Kind,[property:System.Text.Json.Serialization.JsonRequired] string Scope,[property:System.Text.Json.Serialization.JsonRequired] string? Space,[property:System.Text.Json.Serialization.JsonRequired] string? Sprite,
        [property:System.Text.Json.Serialization.JsonRequired] ImmutableDictionary<string,int> Defaults,[property:System.Text.Json.Serialization.JsonRequired] ImmutableHashSet<string> Observable,[property:System.Text.Json.Serialization.JsonRequired] EntityId Entity,
        [property:System.Text.Json.Serialization.JsonRequired] EntityId? Soul,[property:System.Text.Json.Serialization.JsonRequired] SubjectProjectionId? Self);
    private sealed record MarkerSave([property:System.Text.Json.Serialization.JsonRequired] string Actor,[property:System.Text.Json.Serialization.JsonRequired] string Candidate,[property:System.Text.Json.Serialization.JsonRequired] SubjectProjectionId Projection);
    private sealed record SessionSave
    {
        public required string Format {get;init;}
        public required string ContentVersion {get;init;}
        public required string[] RequiredCapabilities {get;init;}
        public required byte[] Runtime {get;init;}
        public required double Now {get;init;}
        public required long Sequence {get;init;}
        public required string[] Loaded {get;init;}
        public required string[] Ended {get;init;}
        public required string? CurrentChapter {get;init;}
        public required long Version {get;init;}
        public required long Serial {get;init;}
        public required ulong RandomState {get;init;}
        public required EntitySave[] Entities {get;init;}
        public required Dictionary<string,Planned> Declarations {get;init;}
        public required Dictionary<string,List<BoundCandidate>> Candidates {get;init;}
        public required Dictionary<string,Dictionary<string,SubjectProjectionId>> Known {get;init;}
        public required MarkerSave[] Markers {get;init;}
        public required ChapterProgressSaveV1 Progress {get;init;}
    }
    public ContractResult<byte[]> Save(CodecLimitsV1? limits=null) => Try(() =>
    {
        lock(_gate)
        {
            Value(CheckSavePoint());
            var data=new SessionSave
            {
                Format="dual-ontology-world-session/v1", ContentVersion="story.world-session/1", RequiredCapabilities=["story.idle-save/1"],
                Runtime=Value(Runtime.Save(limits)),Now=Simulation.CTB.Now,Sequence=Simulation.CTB.Sequence,
                Loaded=[.._order],Ended=[.._endedChapters.Order(StringComparer.Ordinal)],CurrentChapter=CurrentChapter,
                Version=_version,Serial=_serial,RandomState=RandomState,
                Entities=[.._entities.Values.Select(r=>new EntitySave(r.Id,r.Name,r.Kind,r.Scope,r.Space,r.Sprite,r.Defaults,r.Observable,r.Entity.Id,r.Character?.Soul.Id,r.Character?.Self.SubjectId))],
                Declarations=new(_declarations),Candidates=new(_candidates),
                Known=_known.ToDictionary(p=>p.Key,p=>p.Value.ToDictionary(k=>k.Key,k=>k.Value.SubjectId!.Value)),
                Markers=[.._responseMarkers.Select(p=>new MarkerSave(p.Key.Actor,p.Key.Candidate,p.Value.SubjectId!.Value))],Progress=Progress.CaptureProgress()
            };
            var bytes=JsonSerializer.SerializeToUtf8Bytes(data,Json); Value(Restore(bytes,limits)); return bytes;
        }
    });
    public static ContractResult<WorldSession> Restore(byte[] bytes,CodecLimitsV1? limits=null) => Try(() =>
    {
        Value(CognitionCodec.ValidateJson(bytes,limits));
        var d=JsonSerializer.Deserialize<SessionSave>(bytes,Json)!;
        Require(d is not null,"session");
        Require(d!.Format=="dual-ontology-world-session/v1" && d.ContentVersion=="story.world-session/1","version",ContractErrorCode.Unsupported);
        Require(d.RequiredCapabilities is not null && d.RequiredCapabilities.All(c=>c=="story.idle-save/1"),"required_capabilities",ContractErrorCode.Unsupported);
        Require(double.IsFinite(d.Now)&&d.Now>=0&&d.Sequence>=0&&d.Version>=0&&d.Serial>=0&&d.RandomState!=0,"clock",ContractErrorCode.Unsupported);
        Require(d.Loaded is not null&&d.Ended is not null&&d.Entities is not null&&d.Declarations is not null&&d.Candidates is not null&&d.Known is not null&&d.Markers is not null&&d.Progress is not null,"collections");
        Require(d.Loaded!.Distinct(StringComparer.Ordinal).Count()==d.Loaded.Length&&d.Ended!.Distinct(StringComparer.Ordinal).Count()==d.Ended.Length&&d.Ended.All(d.Loaded.Contains)&&
            (d.CurrentChapter is null ? d.Loaded.Length==0 : d.Loaded.LastOrDefault()==d.CurrentChapter),"chapters");
        var restored=Value(WorldRuntime.Restore(d.Runtime,limits));
        var s=new WorldSession(restored.Runtime,new SimulationSession(restored.Runtime,new CTBSystem(d.Now,d.Sequence))) { CurrentChapter=d.CurrentChapter,RandomState=d.RandomState,Progress=ChapterSession.RestoreProgress(d.Progress!),_version=d.Version,_serial=d.Serial };
        // A restored session owns its scheduler; no chapter initializer, observation or response runs.
        s._restoreOwnsSimulation=true;
        Essence Entity(EntityId id) { Require(restored.Entities.ContainsKey(id),"entity.reference",ContractErrorCode.InvalidReference); return restored.Entities[id]; }
        Projection Projection(SubjectProjectionId id) { Require(restored.Projections.TryGetValue(id,out var p),"projection.reference",ContractErrorCode.InvalidReference); return p!; }
        foreach(var r in d.Entities!)
        {
            Require(r is not null&&r.Defaults is not null&&r.Observable is not null,"entity");
            ValidateName(r!.Id,"entity id"); var e=Entity(r.Entity); ScriptCharacter? actor=null;
            Require(restored.Runtime.Representations(e).Count>0,"entity.facets",ContractErrorCode.InvalidReference);
            Require(r.Kind is "room" or "object" or "character","kind",ContractErrorCode.Unsupported);
            Require((r.Kind=="room")== (e.Kind==ProjectionKind.Space)&&r.Name==e.TrueName,"entity.kind");
            Require((r.Soul is not null)==(r.Kind=="character")&&(r.Self is not null)==(r.Kind=="character"),"character");
            if(r.Soul is {} soulId&&r.Self is {} selfId)
            {
                var soul=Entity(soulId) as SoulEssence; var self=Projection(selfId);
                Require(soul is not null&&restored.Runtime.Representations(soul).Count>0&&soul.World.Contains(self)&&self.Kind==ProjectionKind.Body&&restored.Runtime.KnownProjections(soul,e).Contains(self),"self",ContractErrorCode.InvalidReference);
                actor=s.World.RegisterCharacter(r.Id,soul!,e,self); actor.SpriteFramesPath=r.Sprite;
            }
            else s.World.Register(r.Id,e);
            Require(s._entities.TryAdd(r.Id,new(r.Id,r.Name,r.Kind,r.Scope,r.Space,r.Sprite,r.Defaults!,r.Observable!,e,actor)),"entity.id",ContractErrorCode.DuplicateIdentity);
        }
        foreach(var p in d.Declarations!) { Require(s._entities.ContainsKey(p.Key)&&p.Value.Id==p.Key,"declarations"); s._declarations.Add(p.Key,p.Value); }
        Require(s._declarations.Count==s._entities.Count,"declarations");
        var allIds=s._entities.Keys.ToHashSet(StringComparer.Ordinal); var kinds=s._entities.ToDictionary(p=>p.Key,p=>p.Value.Kind);
        var optionIds=d.Candidates!.Values.SelectMany(c=>c).Where(c=>c.Kind=="option").Select(c=>c.Id).ToHashSet(StringComparer.Ordinal);
        foreach(var p in d.Candidates)
        {
            Require(d.Loaded.Contains(p.Key),"candidates.chapter");
            var definition=new ChapterWorldDefinition { Id=p.Key,Candidates=p.Value.Select(c=>new WorldCandidateDefinition { Id=c.Id,Kind=c.Kind,Scope=c.Scope,Actor=c.Actor,Target=c.Target,Label=c.Label,Applicability=c.Applicability,Goal=c.Goal,Response=c.Response.ToList(),ResponseTo=c.ResponseTo.ToList(),Policy=new() { Mode=c.Kind=="option"?"player":"core",Weight=c.Weight },Delay=c.Delay }).ToList() };
            _=ValidateCandidatePlan(definition,allIds,kinds,optionIds);
            s._candidates.Add(p.Key,p.Value);
        }
        foreach(var p in d.Known!)
        {
            var soul=s.Character(p.Key).Soul; var map=new Dictionary<string,Projection>(StringComparer.Ordinal);
            foreach(var k in p.Value) { Require(s._entities.ContainsKey(k.Key),"known.entity",ContractErrorCode.InvalidReference); var projection=Projection(k.Value); Require(soul.World.Contains(projection)&&restored.Runtime.KnownProjections(soul,s._entities[k.Key].Entity).Contains(projection),"known.subject",ContractErrorCode.InvalidReference); map.Add(k.Key,projection); }
            s._known.Add(p.Key,map);
        }
        Require(s._known.Count==s.World.Characters.Count,"known");
        foreach(var m in d.Markers!) { var actor=s.Character(m.Actor); var p=Projection(m.Projection); Require(actor.Soul.World.Contains(p)&&optionIds.Contains(m.Candidate)||actor.Soul.World.Contains(p)&&s._candidates.Values.SelectMany(c=>c).Any(c=>c.Id==m.Candidate),"markers",ContractErrorCode.InvalidReference); Require(s._responseMarkers.TryAdd((m.Actor,m.Candidate),p),"markers",ContractErrorCode.DuplicateIdentity); }
        s._order.AddRange(d.Loaded); s._loaded.UnionWith(d.Loaded); s._endedChapters.UnionWith(d.Ended!); return s;
    });
    private bool _restoreOwnsSimulation;
}
