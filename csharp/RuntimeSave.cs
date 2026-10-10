using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DualOntologyWorldEngine.Core;

public sealed record RestoredRuntimeV1(WorldRuntime Runtime,
    ImmutableDictionary<EntityId, Essence> Entities,
    ImmutableDictionary<SubjectProjectionId, Projection> Projections);

/// <summary>Engine-only persistence. Property and AST restoration use closed switches.</summary>
public sealed partial class WorldRuntime
{
    public ImmutableDictionary<EntityId,Essence> PersistenceEntities => _entities.ToImmutableDictionary();
    private Guid _saveRealityDomain = Guid.NewGuid();
    private readonly Dictionary<Projection, SubjectProjectionId> _saveFacets = [];

    public ContractResult<byte[]> Save(CodecLimitsV1? limits = null) => Contract.Try(() =>
    {
        foreach (var e in _entities.Values)
        foreach (var p in e.Properties)
        {
            Contract.Require(p is StateProperty or SpatialProperty or PassageProperty or LockProperty or SurfaceProperty or ProcedureProperty or RuleProperty or TemporaryStateProperty,
                ContractErrorCode.Unsupported, "property", "未知现实属性，不能保存。");
            if (p is TemporaryStateProperty t) Contract.Require(t.Active.Count == 0, ContractErrorCode.UnsupportedSavePoint, "property.temporary_state", "仍有活动时效层。");
        }
        var saveFacets=new Dictionary<Projection,SubjectProjectionId>(_saveFacets);
        foreach (var p in _facets.Values) saveFacets.TryAdd(p, new(_saveRealityDomain, Guid.NewGuid()));
        SubjectProjectionId Handle(Projection p)
        {
            Contract.Require(saveFacets.TryGetValue(p, out var id), ContractErrorCode.InvalidReference, "reality.reference", "现实 AST 必须引用已登记切面。"); return id;
        }
        JsonNode? W(object? x) => CognitionCodec.Write(x);
        JsonArray List<T>(IEnumerable<T> xs, Func<T, JsonNode?> write) => new(xs.Select(write).ToArray());
        JsonObject Obj(params (string, JsonNode?)[] pairs) { var o = new JsonObject(); foreach (var (k,v) in pairs) o.Add(k,v); return o; }
        FrozenProjectionV1 Freeze(Projection p) => new(Handle(p), p.Name, p.Kind, p.Existence,
            p.Parent is null ? null : Handle(p.Parent), p.Tags,
            [..p.Relations.Select(r => new RelationEdgeV1(r.Relation,Handle(r.Target))).OrderBy(r=>r.Relation.Name,StringComparer.Ordinal).ThenBy(r=>Observation.IdKey(r.Target),StringComparer.Ordinal)],
            [..p.DeniedRelations.Select(r => new RelationEdgeV1(r.Relation,Handle(r.Target))).OrderBy(r=>r.Relation.Name,StringComparer.Ordinal).ThenBy(r=>Observation.IdKey(r.Target),StringComparer.Ordinal)],
            [..p.Rules.Select(r=>CognitionAst.Freeze(r,Handle))], p.Procedure is null ? null : CognitionAst.Freeze(p.Procedure,Handle), p.KnownTerms, null);
        var reality = new CognitionSnapshotV1(1,_saveRealityDomain,new(0),new(0),[.._facets.Values.Select(Freeze)],[],[]);
        SnapshotGraph.Build(reality);
        JsonNode Property(Property p) => p switch
        {
            StateProperty s => Obj(("kind",W("state")),("values",W(s.Values.ToImmutableDictionary())),("observable",List(s.Observable.OrderBy(t=>t.Name,StringComparer.Ordinal),t=>W(t)))),
            SpatialProperty s => Obj(("kind",W("spatial")),("space",W(s.Space?.Id)),("holder",W(s.Holder?.Id))),
            PassageProperty s => Obj(("kind",W("passage")),("connections",List(s.Connections.OrderBy(c=>c.Key.Id.Value,StringComparer.Ordinal),c=>Obj(("from",W(c.Key.Id)),("to",W(c.Value.Id)))))),
            LockProperty s => Obj(("kind",W("lock")),("locked",W(s.IsLocked))),
            TemporaryStateProperty => Obj(("kind",W("temporary_state"))),
            ProcedureProperty s => Obj(("kind",W("procedure")),("effect",W(CognitionAst.Freeze(s.Effect,Handle)))),
            RuleProperty s => Obj(("kind",W("rules")),("rules",List(s.Rules,r=>W(CognitionAst.Freeze(r,Handle))))),
            SurfaceProperty s => Obj(("kind",W("surface")),("values",List(s.Surfaces.OrderBy(v=>Observation.IdKey(Handle(v.Key)),StringComparer.Ordinal),v=>Obj(("facet",W(Handle(v.Key))),("tags",W(v.Value))))),
                ("relations",List(s.Relations.OrderBy(v=>Observation.IdKey(Handle(v.Key)),StringComparer.Ordinal),v=>Obj(("facet",W(Handle(v.Key))),("edges",List(v.Value.OrderBy(r=>r.Relation.Name,StringComparer.Ordinal).ThenBy(r=>Observation.IdKey(Handle(r.Target)),StringComparer.Ordinal),r=>W(new RelationEdgeV1(r.Relation,Handle(r.Target))))))))),
            _ => throw new CognitionContractException(new(ContractErrorCode.Unsupported,"property","未知现实属性。",CandidateIds:[]))
        };
        var subjects = _entities.Values.OfType<SoulEssence>().ToArray();
        var root = Obj(("format",W("dual-ontology-runtime/v1")),("content_version",W("core.runtime/1")),("required_capabilities",new JsonArray(W("core.runtime-idle-save/1"),W("core.typed-events/1"))),
            ("reality",W(reality)),
            ("events",List(_eventAudit,e=>CognitionCodec.WriteEvent(e,r=>W(r?.Id)))),
            ("entities",List(_entities.Values.OrderBy(e=>e.Id.Value,StringComparer.Ordinal),e=>Obj(("id",W(e.Id)),("name",W(e.TrueName)),("kind",W(e.Kind)),("active",W(_existing.Contains(e))),
                ("relations",List(e.Relations.OrderBy(r=>r.Relation.Name,StringComparer.Ordinal).ThenBy(r=>r.Target.Id.Value,StringComparer.Ordinal),r=>Obj(("relation",W(r.Relation)),("target",W(r.Target.Id))))),
                ("properties",List(e.Properties.OrderBy(p=>p.GetType().Name,StringComparer.Ordinal),Property))))),
            ("facets",List(_facets.OrderBy(f=>f.Key.Value,StringComparer.Ordinal),f=>Obj(("id",W(f.Key)),("entity",W(_binding[f.Value].Id)),("handle",W(Handle(f.Value)))))),
            ("subjects",List(subjects.OrderBy(s=>s.Id.Value,StringComparer.Ordinal),s=>Obj(("entity",W(s.Id)),
                ("graph",W(Convert.ToBase64String(Contract.Value(CognitionCodec.Encode(Contract.Value(CaptureCognitionGraph(s)),limits))))),
                ("audit",W(Convert.ToBase64String(Contract.Value(CognitionCodec.EncodeAudit(Contract.Value(CaptureCognitionAudit(s)),limits)))))))),
            ("bindings",List(_binding.Where(b=>b.Key.SubjectId is not null).OrderBy(b=>Observation.IdKey(b.Key.SubjectId!.Value),StringComparer.Ordinal),b=>Obj(("projection",W(b.Key.SubjectId)),("entity",W(b.Value.Id))))),
            ("mental",List(_mental.OrderBy(m=>m.Key.Soul.Id.Value,StringComparer.Ordinal).ThenBy(m=>Observation.IdKey(Handle(m.Key.Reality)),StringComparer.Ordinal),m=>Obj(("soul",W(m.Key.Soul.Id)),("facet",W(Handle(m.Key.Reality))),("projection",W(m.Value.SubjectId))))));
        var bytes=Encoding.UTF8.GetBytes(root.ToJsonString());
        Contract.Value(Restore(bytes,limits)); // Full preflight, no mutation or callbacks in the running world.
        foreach(var pair in saveFacets) _saveFacets.TryAdd(pair.Key,pair.Value);
        return bytes;
    });

    public static ContractResult<RestoredRuntimeV1> Restore(byte[] bytes, CodecLimitsV1? limits = null)
    {
        try { return new ContractResult<RestoredRuntimeV1>.Success(RestoreCandidate(bytes,limits)); }
        catch(CognitionContractException e) { return new ContractResult<RestoredRuntimeV1>.Failure(e.Error); }
        catch(Exception e) when(e is JsonException or FormatException or InvalidOperationException or ArgumentException or OverflowException or DecoderFallbackException or KeyNotFoundException or NullReferenceException)
        { return new ContractResult<RestoredRuntimeV1>.Failure(new(ContractErrorCode.MalformedData,"runtime","无效的运行时存档结构。",CandidateIds:[])); }
    }
    private static RestoredRuntimeV1 RestoreCandidate(byte[] bytes, CodecLimitsV1? limits)
    {
        using var doc=CognitionCodec.Parse(bytes,limits); var root=doc.RootElement;
        void F(JsonElement e,params string[] names)=>CognitionCodec.Fields(e,names);
        void Require(bool b,ContractErrorCode c,string p)=>Contract.Require(b,c,p,"运行时存档校验失败。");
        string S(JsonElement e)=>e.GetString() ?? throw new CognitionContractException(new(ContractErrorCode.MalformedData,"text","必要文本为空。",CandidateIds:[]));
        EntityId EId(JsonElement e) { var id=new EntityId(S(e)); Contract.Identity(id.Value,"entity.id"); return id; }
        SubjectProjectionId PId(JsonElement e)=>CognitionCodec.ReadSubjectProjectionId(e);
        CognitionCodec.OptionalFields(root,["format","content_version","required_capabilities","reality","entities","facets","subjects","bindings","mental"],"events");
        Require(S(root.GetProperty("format"))=="dual-ontology-runtime/v1" && S(root.GetProperty("content_version"))=="core.runtime/1",ContractErrorCode.Unsupported,"version");
        if(root.TryGetProperty("required_capabilities",out var caps)) foreach(var c in caps.EnumerateArray())
        {
            var capability=S(c);
            Require(capability is "core.runtime-idle-save/1" or "core.typed-events/1",ContractErrorCode.Unsupported,"required_capabilities");
            if(capability=="core.typed-events/1") Require(root.TryGetProperty("events",out _),ContractErrorCode.MalformedData,"events");
        }
        var frozen=CognitionCodec.ReadCognitionSnapshotV1(root.GetProperty("reality"));
        var validated=SnapshotGraph.Build(frozen);
        var runtime=new WorldRuntime { _saveRealityDomain=frozen.SubjectWorld };
        var reality=validated.Projections.ToDictionary(p=>p.Key,p=>new Projection(runtime.Reality,p.Value.Name,p.Value.Kind));
        foreach(var (id,p) in reality) runtime._saveFacets.Add(p,id);
        var subjectGraphs=new Dictionary<EntityId,(DetachedProjectionGraphV1 Graph,CognitionAuditHistoryV1 Audit)>();
        var handles=new Dictionary<SubjectProjectionId,Projection>(); var domains=new HashSet<Guid> { frozen.SubjectWorld };
        foreach(var s in root.GetProperty("subjects").EnumerateArray())
        {
            F(s,"entity","graph","audit"); var id=EId(s.GetProperty("entity"));
            var archive=Contract.Value(CognitionCodec.Decode(Convert.FromBase64String(S(s.GetProperty("graph"))),limits));
            var audit=Contract.Value(CognitionCodec.DecodeAudit(Convert.FromBase64String(S(s.GetProperty("audit"))),limits));
            Require(domains.Add(archive.SubjectSnapshot.SubjectWorld),ContractErrorCode.DuplicateIdentity,"subject_world");
            Require(audit.SubjectWorld==archive.SubjectSnapshot.SubjectWorld,ContractErrorCode.InvalidReference,"audit.subject_world");
            var last=Contract.Value(runtime.ReconstructCognitionAudit(audit,archive.SubjectSnapshot.Revision));
            Require(last.Revision.Value==audit.Commits.Length && SameSnapshot(last,archive.SubjectSnapshot),ContractErrorCode.MalformedData,"audit.snapshot");
            // A live restored world must continue using version-fixed readers; detached
            // Rebind graphs are only for external snapshot consumers.
            var graph=SnapshotGraph.Build(archive.SubjectSnapshot,detached:false);
            graph.World.Allocated.UnionWith(archive.AllocatedIds); graph.World.Used.UnionWith(archive.AllocatedIds);
            graph.World.InitialSnapshot=audit.InitialSnapshot; graph.World.Audit=audit.Commits.ToImmutableList();
            Require(audit.Commits.SelectMany(c=>c.Inputs).All(c=>archive.AllocatedIds.Contains(c.Target)),ContractErrorCode.InvalidReference,"audit.allocated_ids");
            Require(subjectGraphs.TryAdd(id,(graph,audit)),ContractErrorCode.DuplicateIdentity,"subjects");
            foreach(var p in graph.Projections) Require(handles.TryAdd(p.Key,p.Value),ContractErrorCode.DuplicateIdentity,"projections");
        }
        var entityRecords=root.GetProperty("entities").EnumerateArray().ToArray();
        Require(entityRecords.Length <= (limits ?? new()).MaxProjections,ContractErrorCode.LimitExceeded,"entities");
        foreach(var e in entityRecords)
        {
            F(e,"id","name","kind","active","relations","properties"); var id=EId(e.GetProperty("id"));
            var kind=S(e.GetProperty("kind")); var enumKind=Enum.GetValues<ProjectionKind>().SingleOrDefault(k=>k.ToString().ToLowerInvariant()==kind);
            Require(Enum.GetValues<ProjectionKind>().Any(k=>k.ToString().ToLowerInvariant()==kind),ContractErrorCode.Unsupported,"kind");
            Essence entity=enumKind==ProjectionKind.Soul ? new SoulEssence(id,S(e.GetProperty("name")),subjectGraphs.TryGetValue(id,out var sg) ? sg.Graph.World : throw new CognitionContractException(new(ContractErrorCode.InvalidReference,"soul","缺少主体图。",CandidateIds:[]))) : new Essence(id,S(e.GetProperty("name")),enumKind);
            Require(runtime._entities.TryAdd(id,entity),ContractErrorCode.DuplicateIdentity,"entity.id");
            if(e.GetProperty("active").GetBoolean()) runtime._existing.Add(entity);
        }
        Require(subjectGraphs.Keys.All(id=>runtime._entities.TryGetValue(id,out var e)&&e is SoulEssence),ContractErrorCode.InvalidReference,"subjects.entity");
        Essence Entity(JsonElement e) { var id=EId(e); Require(runtime._entities.ContainsKey(id),ContractErrorCode.InvalidReference,"entity.reference"); return runtime._entities[id]; }
        Projection Real(JsonElement e) { var id=PId(e); Require(reality.ContainsKey(id),ContractErrorCode.InvalidReference,"facet.reference"); return reality[id]; }
        foreach(var f in root.GetProperty("facets").EnumerateArray())
        {
            F(f,"id","entity","handle"); var id=new FacetId(S(f.GetProperty("id"))); Contract.Identity(id.Value,"facet.id");
            var p=Real(f.GetProperty("handle")); var entity=Entity(f.GetProperty("entity"));
            Require(runtime._facets.TryAdd(id,p)&&runtime._binding.TryAdd(p,entity),ContractErrorCode.DuplicateIdentity,"facet");
            if(!runtime._reality.TryGetValue(entity,out var views)) runtime._reality.Add(entity,views=[]); views.Add(p);
        }
        Require(runtime._facets.Count==reality.Count,ContractErrorCode.InvalidReference,"facets");
        runtime.Reality.Projections=[..frozen.Projections.Select(p=>reality[p.Id])];
        foreach(var n in frozen.Projections)
        {
            var p=reality[n.Id]; p.Existence=n.Existence; p.Parent=n.Parent is {} parent ? reality[parent] : null; p.Tags=n.Tags; p.KnownTerms=n.KnownTerms;
            p.Relations=[..n.Relations.Select(r=>(r.Relation,reality[r.Target]))]; p.DeniedRelations=[..n.DeniedRelations.Select(r=>(r.Relation,reality[r.Target]))];
            p.Rules=[..n.Rules.Select(r=>CognitionAst.Thaw(r,reality))]; p.Procedure=n.Procedure is null ? null : CognitionAst.Thaw(n.Procedure,reality);
        }
        foreach(var e in entityRecords)
        {
            var entity=Entity(e.GetProperty("id")); var rels=new HashSet<(RelationTag,Essence)>();
            foreach(var r in e.GetProperty("relations").EnumerateArray()) { F(r,"relation","target"); var tag=new RelationTag(S(r.GetProperty("relation"))); Contract.Identity(tag.Name,"relation"); Require(rels.Add((tag,Entity(r.GetProperty("target")))),ContractErrorCode.MalformedData,"relations"); }
            entity.Relations.UnionWith(rels); var kinds=new HashSet<string>();
            foreach(var p in e.GetProperty("properties").EnumerateArray())
            {
                var kind=S(p.GetProperty("kind")); Require(kinds.Add(kind),ContractErrorCode.MalformedData,"properties");
                switch(kind)
                {
                    case "state":
                        F(p,"kind","values","observable"); var state=entity.Add(new StateProperty());
                        foreach(var tag in p.GetProperty("values").EnumerateArray()) { F(tag,"tag","value"); var t=new Tag(S(tag.GetProperty("tag"))); Contract.Identity(t.Name,"tag"); Require(tag.GetProperty("value").TryGetInt32(out var v),ContractErrorCode.Unsupported,"value"); Require(state.Values.TryAdd(t,v),ContractErrorCode.MalformedData,"values"); }
                        foreach(var tag in p.GetProperty("observable").EnumerateArray()) { var t=new Tag(S(tag)); Contract.Identity(t.Name,"tag"); Require(state.Observable.Add(t),ContractErrorCode.MalformedData,"observable"); } break;
                    case "spatial": F(p,"kind","space","holder"); entity.Add(new SpatialProperty { Space=p.GetProperty("space").ValueKind==JsonValueKind.Null ? null : Entity(p.GetProperty("space")), Holder=p.GetProperty("holder").ValueKind==JsonValueKind.Null ? null : Entity(p.GetProperty("holder")) }); break;
                    case "passage": F(p,"kind","connections"); var passage=entity.Add(new PassageProperty()); foreach(var c in p.GetProperty("connections").EnumerateArray()) { F(c,"from","to"); Require(passage.Connections.TryAdd(Entity(c.GetProperty("from")),Entity(c.GetProperty("to"))),ContractErrorCode.MalformedData,"connections"); } break;
                    case "lock": F(p,"kind","locked"); entity.Add(new LockProperty { IsLocked=p.GetProperty("locked").GetBoolean() }); break;
                    case "temporary_state": F(p,"kind"); entity.Add(new TemporaryStateProperty()); break;
                    case "procedure": F(p,"kind","effect"); entity.Add(new ProcedureProperty(CognitionAst.Thaw(ReadEffect(p.GetProperty("effect")),reality))); break;
                    case "rules": F(p,"kind","rules"); entity.Add(new RuleProperty { Rules=[..p.GetProperty("rules").EnumerateArray().Select(r=>CognitionAst.Thaw(CognitionCodec.ReadFrozenRuleV1(r),reality))] }); break;
                    case "surface":
                        F(p,"kind","values","relations"); var surface=entity.Add(new SurfaceProperty());
                        foreach(var v in p.GetProperty("values").EnumerateArray())
                        { F(v,"facet","tags"); var view=Real(v.GetProperty("facet")); Require(ReferenceEquals(runtime._binding[view],entity),ContractErrorCode.InvalidReference,"surface.facet"); var b=ImmutableDictionary.CreateBuilder<Tag,int>(); foreach(var t in v.GetProperty("tags").EnumerateArray()) { F(t,"tag","value"); var tag=new Tag(S(t.GetProperty("tag"))); Contract.Identity(tag.Name,"tag"); Require(t.GetProperty("value").TryGetInt32(out var number),ContractErrorCode.Unsupported,"surface.value"); Require(!b.ContainsKey(tag),ContractErrorCode.MalformedData,"surface.tags"); b.Add(tag,number); } Require(surface.Surfaces.TryAdd(view,b.ToImmutable()),ContractErrorCode.MalformedData,"surface"); }
                        foreach(var v in p.GetProperty("relations").EnumerateArray())
                        { F(v,"facet","edges"); var view=Real(v.GetProperty("facet")); Require(ReferenceEquals(runtime._binding[view],entity),ContractErrorCode.InvalidReference,"surface.facet"); var b=ImmutableHashSet.CreateBuilder<(RelationTag,Projection)>(); foreach(var r in v.GetProperty("edges").EnumerateArray()) { var edge=CognitionCodec.ReadRelationEdgeV1(r); Contract.Identity(edge.Relation.Name,"relation"); Require(reality.TryGetValue(edge.Target,out var target),ContractErrorCode.InvalidReference,"surface.target"); Require(b.Add((edge.Relation,target!)),ContractErrorCode.MalformedData,"surface.edges"); } Require(surface.Relations.TryAdd(view,b.ToImmutable()),ContractErrorCode.MalformedData,"surface"); } break;
                    default: throw new CognitionContractException(new(ContractErrorCode.Unsupported,"property.kind","未知现实属性。",CandidateIds:[]));
                }
            }
        }
        Projection Mental(JsonElement e)
        {
            var id=PId(e); if(handles.TryGetValue(id,out var p)) return p;
            var owner=subjectGraphs.Values.SingleOrDefault(s=>s.Graph.World.SubjectWorldId==id.SubjectWorld).Graph;
            Require(owner is not null && owner.World.Allocated.Contains(id),ContractErrorCode.InvalidReference,"binding.projection");
            // Retired binding shells have no subject-visible state and cannot enter cognition.
            p=new Projection(owner!.World,"",ProjectionKind.Object,id); handles.Add(id,p); return p;
        }
        foreach(var b in root.GetProperty("bindings").EnumerateArray()) { F(b,"projection","entity"); Require(runtime._binding.TryAdd(Mental(b.GetProperty("projection")),Entity(b.GetProperty("entity"))),ContractErrorCode.DuplicateIdentity,"binding"); }
        foreach(var m in root.GetProperty("mental").EnumerateArray())
        {
            F(m,"soul","facet","projection"); var soul=Entity(m.GetProperty("soul")) as SoulEssence; var facet=Real(m.GetProperty("facet")); var p=Mental(m.GetProperty("projection"));
            Require(soul is not null && ReferenceEquals(soul.World,p.World) && runtime._binding.TryGetValue(p,out var target) && ReferenceEquals(target,runtime._binding[facet]),ContractErrorCode.InvalidReference,"mental");
            Require(runtime._mental.TryAdd((soul!,facet),p),ContractErrorCode.DuplicateIdentity,"mental");
        }
        if(root.TryGetProperty("events",out var events))
        {
            Require(events.GetArrayLength()<=(limits??new()).MaxHistoryEntries,ContractErrorCode.LimitExceeded,"events");
            foreach(var e in events.EnumerateArray())
                // The ordered audit is restored only after all live/retired entity shells exist.
                // Registration validates IDs and preceding causes; it never publishes an observation.
                runtime.RecordCommittedEvent(CognitionCodec.ReadEvent<Essence?>(e,r=>r.ValueKind==JsonValueKind.Null?null:Entity(r)));
        }
        // Sync only derives reality. No observation, action, effect or notification is replayed.
        runtime.Sync();
        foreach(var n in frozen.Projections)
        { var p=reality[n.Id]; Require(SameReality(p,n,reality),ContractErrorCode.MalformedData,"reality.properties"); }
        return new(runtime,runtime._entities.ToImmutableDictionary(),handles.ToImmutableDictionary());
    }
    private static FrozenEffectV1 ReadEffect(JsonElement e) => CognitionCodec.ReadEffect(e);
    private static bool SameSnapshot(CognitionSnapshotV1 a,CognitionSnapshotV1 b)=>a.SubjectWorld==b.SubjectWorld&&a.Revision==b.Revision&&a.VocabularyRevision==b.VocabularyRevision&&a.Projections.Length==b.Projections.Length&&a.Projections.Zip(b.Projections).All(p=>SnapshotGraph.SameProjection(p.First,p.Second))&&a.VisibleHistory.Length==b.VisibleHistory.Length&&a.VisibleHistory.Zip(b.VisibleHistory).All(p=>SnapshotGraph.SameObservation(p.First,p.Second));
    private static bool SameReality(Projection p,FrozenProjectionV1 n,IReadOnlyDictionary<SubjectProjectionId,Projection> map)
    {
        var reverse=map.ToDictionary(p=>p.Value,p=>p.Key);
        var candidate=new FrozenProjectionV1(n.Id,p.Name,p.Kind,p.Existence,p.Parent is null ? null : reverse[p.Parent],p.Tags,
            [..p.Relations.Select(r=>new RelationEdgeV1(r.Relation,reverse[r.Target]))],[..p.DeniedRelations.Select(r=>new RelationEdgeV1(r.Relation,reverse[r.Target]))],
            [..p.Rules.Select(r=>CognitionAst.Freeze(r,x=>reverse[x]))],p.Procedure is null ? null : CognitionAst.Freeze(p.Procedure,x=>reverse[x]),p.KnownTerms,null);
        return SnapshotGraph.SameProjection(candidate,n);
    }
}
