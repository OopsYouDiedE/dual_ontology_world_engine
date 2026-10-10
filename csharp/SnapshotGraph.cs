using System.Collections.Immutable;
using System.Text;

namespace DualOntologyWorldEngine.Core;

/// <summary>Two-phase allocation/rebinding shared by validation and consistent legacy readers.</summary>
public static class CognitionGraph
{
    public static ContractResult<DetachedProjectionGraphV1> Rebind(CognitionSnapshotV1 snapshot)
        => Contract.Try(() => SnapshotGraph.Build(snapshot));
    public static ContractResult<DetachedProjectionGraphV1> Rebind(CognitionGraphArchiveV1 archive)
        => Contract.Try(() =>
        {
            CognitionCodec.ValidateArchive(archive);
            var graph = SnapshotGraph.Build(archive.SubjectSnapshot);
            graph.World.Allocated.UnionWith(archive.AllocatedIds);
            // Persisted reservations are retired: loading must never make an old handle fresh again.
            graph.World.Used.UnionWith(archive.AllocatedIds);
            return graph;
        });
}

internal static class SnapshotGraph
{
    // Only internally published snapshots reach this path. Public Rebind still validates every field.
    // Allocate fresh shells for each revision: an ongoing evaluation must never see a later commit.
    internal static DetachedProjectionGraphV1 BuildRead(CognitionSnapshotV1 snapshot)
    {
        var world = new ProjectionWorld(snapshot.SubjectWorld) { Detached = true };
        var map = snapshot.Projections.ToImmutableDictionary(p => p.Id,
            p => new Projection(world, p.Name, p.Kind, p.Id));
        Projection? Get(SubjectProjectionId? id) => id is { } value ? map[value] : null;
        ObservationEvent Legacy(VisibleObservationV1 o) => new(map[o.Target], o.Source, o.Time, o.Detected,
            o.DeclaredExistence, o.Details, o.ObservedState.Tags,
            [..o.ObservedState.Relations.Select(r => (r.Relation, map[r.Target]))],
            o.Event is null ? null : TypedEvents.Map(o.Event, r => r is null ? null : map[r.Value]));
        foreach (var node in snapshot.Projections)
        {
            var p = map[node.Id];
            p.Existence = node.Existence; p.Parent = Get(node.Parent); p.Tags = node.Tags; p.KnownTerms = node.KnownTerms;
            p.Relations = [..node.Relations.Select(r => (r.Relation, map[r.Target]))];
            p.DeniedRelations = [..node.DeniedRelations.Select(r => (r.Relation, map[r.Target]))];
            p.Rules = [..node.Rules.Select(r => CognitionAst.Thaw(r, map))];
            p.Procedure = node.Procedure is null ? null : CognitionAst.Thaw(node.Procedure, map);
            p.LastObservation = node.LastObservation is { } last ? Legacy(last) : null;
        }
        world.Projections = [..snapshot.Projections.Select(p => map[p.Id])];
        world.History = [..snapshot.VisibleHistory.Select(Legacy)];
        world.Snapshot = snapshot; world.InitialSnapshot = snapshot;
        return new(world, map);
    }

    internal static void ValidateParents(IReadOnlyDictionary<SubjectProjectionId, FrozenProjectionV1> nodes,
        IEnumerable<SubjectProjectionId> changed)
    {
        var complete = new HashSet<SubjectProjectionId>();
        foreach (var start in changed)
        {
            var path = new HashSet<SubjectProjectionId>();
            for (var id = (SubjectProjectionId?)start; id is not null && !complete.Contains(id.Value);)
            {
                Contract.Require(nodes.TryGetValue(id.Value, out var node), ContractErrorCode.InvalidReference,
                    "parent", "引用不在当前主体图。");
                Contract.Require(path.Add(id.Value), ContractErrorCode.InvalidBatch, "parent", "主体空间关系不能形成循环。");
                id = node!.Parent;
            }
            complete.UnionWith(path);
        }
    }

    internal static void Terms(ImmutableArray<KnownAuthorTermV1> terms)
    {
        Contract.Array(terms, "known_terms");
        var seen = new HashSet<KnownAuthorTermV1>();
        foreach (var term in terms)
        {
            Contract.Require(term is not null, ContractErrorCode.MalformedData, "known_terms", "词项不能为空。");
            Contract.Identity(term!.TermId.Value, "term_id");
            Contract.Text(term.RawLexeme, "raw_lexeme"); Contract.Text(term.CanonicalLexeme, "canonical_lexeme");
            Contract.Require(!string.IsNullOrWhiteSpace(term.RawLexeme) && term.CanonicalLexeme == term.RawLexeme.Normalize(NormalizationForm.FormC),
                ContractErrorCode.MalformedData, "canonical_lexeme", "词形必须保留原文与对应 NFC 结果。");
            Contract.Require(seen.Add(term), ContractErrorCode.MalformedData, "known_terms", "词项关联重复。");
        }
    }
    internal static void Origin(SourceLocationV1? origin)
    {
        if (origin is null) return;
        Contract.Text(origin.DocumentId, "origin.document_id");
        Contract.Require(origin.StartLine >= 1 && origin.StartColumn >= 1 && origin.EndLine >= origin.StartLine && origin.EndColumn >= 1 &&
            (origin.StartLine != origin.EndLine || origin.EndColumn >= origin.StartColumn), ContractErrorCode.MalformedData, "origin", "来源区间无效。");
    }
    internal static DetachedProjectionGraphV1 Build(CognitionSnapshotV1 snapshot, bool detached = true)
    {
        Contract.Require(snapshot is not null, ContractErrorCode.MalformedData, "snapshot", "快照不能为空。");
        Contract.Require(snapshot!.ContractVersion == 1, ContractErrorCode.Unsupported, "contract_version", "不支持的快照版本。");
        Contract.Require(snapshot.SubjectWorld != Guid.Empty, ContractErrorCode.InvalidIdentity, "subject_world", "主体域不能为空。");
        Contract.Require(snapshot.Revision.Value >= 0 && snapshot.VocabularyRevision.Value >= 0, ContractErrorCode.InvalidRevision, "revision", "版本不能为负。");
        Contract.Array(snapshot.RequiredCapabilities, "required_capabilities");
        foreach (var capability in snapshot.RequiredCapabilities)
            Contract.Require(capability is "core.stable-identity/1" or "core.cognition-atomic-batch/1" or "core.cognition-snapshot/1" or "core.cognition-audit/1" or "core.frozen-ast/1" or "core.typed-events/1" or "core.cognition-graph-codec/1",
                ContractErrorCode.Unsupported, "required_capabilities", "不支持的必要能力。");
        Contract.Array(snapshot.Projections, "projections"); Contract.Array(snapshot.VisibleHistory, "visible_history");
        var world = new ProjectionWorld(snapshot.SubjectWorld) { Detached = detached };
        var map = ImmutableDictionary.CreateBuilder<SubjectProjectionId, Projection>();
        foreach (var node in snapshot.Projections)
        {
            Contract.Require(node is not null, ContractErrorCode.MalformedData, "projections", "图节点不能为空。");
            Contract.Id(node!.Id, snapshot.SubjectWorld, "id");
            Contract.Require(!map.ContainsKey(node.Id), ContractErrorCode.DuplicateIdentity, "id", "主体句柄重复。");
            map.Add(node.Id, new Projection(world, node.Name, node.Kind, node.Id));
            world.Allocated.Add(node.Id); world.Used.Add(node.Id);
        }
        var projections = map.ToImmutable();
        var nodes = snapshot.Projections.ToDictionary(p => p.Id);
        world.Projections = [..snapshot.Projections.Select(p => projections[p.Id])];
        Projection? Get(SubjectProjectionId? id)
        {
            if (id is null) return null;
            Contract.Id(id.Value, snapshot.SubjectWorld, "reference");
            Contract.Require(projections.TryGetValue(id.Value, out var p), ContractErrorCode.InvalidReference, "reference", "引用不在当前主体图。");
            return p;
        }
        void State(ObservedStateV1 state)
        {
            Contract.Require(state is not null, ContractErrorCode.MalformedData, "state", "节点状态不能为空。");
            Contract.Text(state!.Name, "name"); Contract.Enum(state.Kind, "kind"); Contract.Enum(state.Existence, "existence");
            Get(state.Parent);
            Contract.Require(state.Tags is not null, ContractErrorCode.MalformedData, "tags", "标签字典不能为空。");
            foreach (var tag in state.Tags!.Keys) Contract.Identity(tag.Name, "tag");
            Edges(state.Relations); Edges(state.DeniedRelations);
            Contract.Require(!state.Relations.Intersect(state.DeniedRelations).Any(), ContractErrorCode.MalformedData, "relations", "同一关系不能同时肯定与否定。");
            Contract.Array(state.Rules, "rules");
            foreach (var rule in state.Rules) CognitionAst.Thaw(rule, projections);
            if (state.Procedure is not null) CognitionAst.Thaw(state.Procedure, projections);
            Terms(state.KnownTerms);
        }
        void Edges(ImmutableArray<RelationEdgeV1> edges)
        {
            Contract.Array(edges, "relations");
            var seen = new HashSet<RelationEdgeV1>();
            foreach (var edge in edges)
            {
                Contract.Require(edge is not null, ContractErrorCode.MalformedData, "relation", "关系不能为空。");
                Contract.Identity(edge!.Relation.Name, "relation"); Get(edge.Target);
                Contract.Require(seen.Add(edge), ContractErrorCode.MalformedData, "relations", "关系重复。");
            }
        }
        void Observation(VisibleObservationV1 observation)
        {
            Contract.Require(observation is not null, ContractErrorCode.MalformedData, "observation", "来源记录不能为空。");
            Get(observation!.Target); Contract.Enum(observation.Source, "source"); Contract.Enum(observation.TimeUnit, "time_unit");
            Contract.Number(observation.Time, "time"); Contract.Text(observation.Details, "details");
            if (observation.DeclaredExistence is { } existence) Contract.Enum(existence, "declared_existence");
            Origin(observation.Origin); Contract.Array(observation.Dependencies, "dependencies");
            foreach (var dependency in observation.Dependencies) Get(dependency);
            Contract.Require(observation.Dependencies.Distinct().Count() == observation.Dependencies.Length, ContractErrorCode.MalformedData, "dependencies", "来源依赖重复。");
            State(observation.ObservedState);
            if (observation.Event is { } e) {
                TypedEvents.Validate(e, r => { Get(r); });
                Contract.Require(TypedEvents.References(e).OfType<SubjectProjectionId>().All(observation.Dependencies.Contains),
                    ContractErrorCode.InvalidReference, "event.dependencies", "Event references must be included in source dependencies.");
            }
        }
        foreach (var node in snapshot.Projections)
        {
            State(StateOf(node));
            var p = projections[node.Id];
            p.Name = node.Name; p.Kind = node.Kind; p.Existence = node.Existence; p.Parent = Get(node.Parent);
            p.Tags = node.Tags; p.KnownTerms = node.KnownTerms;
            p.Relations = [..node.Relations.Select(r => (r.Relation, Get(r.Target)!))];
            p.DeniedRelations = [..node.DeniedRelations.Select(r => (r.Relation, Get(r.Target)!))];
            p.Rules = [..node.Rules.Select(r => CognitionAst.Thaw(r, projections))];
            p.Procedure = node.Procedure is null ? null : CognitionAst.Thaw(node.Procedure, projections);
            var visited = new HashSet<SubjectProjectionId>();
            for (var id = (SubjectProjectionId?)node.Id; id is not null; id = nodes[id.Value].Parent)
            {
                Get(id); // Validate every hop before indexing, including indirect missing references.
                Contract.Require(visited.Add(id.Value), ContractErrorCode.InvalidBatch, "parent", "主体空间关系不能形成循环。");
            }
        }
        foreach (var observation in snapshot.VisibleHistory) Observation(observation);
        ObservationEvent Legacy(VisibleObservationV1 o) => new(projections[o.Target], o.Source, o.Time, o.Detected, o.DeclaredExistence,
            o.Details, o.ObservedState.Tags, [..o.ObservedState.Relations.Select(r => (r.Relation, Get(r.Target)!))],
            o.Event is null ? null : TypedEvents.Map(o.Event, r => Get(r)));
        world.History = [..snapshot.VisibleHistory.Select(Legacy)];
        foreach (var node in snapshot.Projections)
        {
            if (node.LastObservation is not { } last) continue;
            Observation(last);
            Contract.Require(last.Target == node.Id && snapshot.VisibleHistory.LastOrDefault(o => o.Target == node.Id) is { } latest && SameObservation(last, latest),
                ContractErrorCode.MalformedData, "last_observation", "最近来源须对应可见历史的最后一条记录。");
            projections[node.Id].LastObservation = Legacy(last);
        }
        world.Snapshot = snapshot; world.InitialSnapshot = snapshot;
        return new(world, projections);
    }
    internal static ObservedStateV1 StateOf(FrozenProjectionV1 p)
        => new(p.Name, p.Kind, p.Existence, p.Parent, p.Tags, p.Relations, p.DeniedRelations, p.Rules, p.Procedure, p.KnownTerms);
    internal static bool SameState(ObservedStateV1 a, ObservedStateV1 b)
        => ReferenceEquals(a, b) || a.Name == b.Name && a.Kind == b.Kind && a.Existence == b.Existence && a.Parent == b.Parent &&
        (ReferenceEquals(a.Tags, b.Tags) || a.Tags.Count == b.Tags.Count && a.Tags.All(p => b.Tags.TryGetValue(p.Key, out var v) && v == p.Value)) &&
        (a.Relations == b.Relations || a.Relations.ToHashSet().SetEquals(b.Relations)) &&
        (a.DeniedRelations == b.DeniedRelations || a.DeniedRelations.ToHashSet().SetEquals(b.DeniedRelations)) &&
        (a.KnownTerms == b.KnownTerms || a.KnownTerms.ToHashSet().SetEquals(b.KnownTerms)) && (a.Rules == b.Rules || a.Rules.Length == b.Rules.Length && a.Rules.Zip(b.Rules).All(p =>
            p.First.Mode == p.Second.Mode && p.First.Weight == p.Second.Weight && p.First.Acceptance == p.Second.Acceptance && p.First.Lifetime == p.Second.Lifetime && p.First.LifetimeOwner == p.Second.LifetimeOwner &&
            Contract.Value(Ast.Equivalent(p.First.Condition, p.Second.Condition)) && Contract.Value(Ast.Equivalent(p.First.Effect, p.Second.Effect)))) &&
        (ReferenceEquals(a.Procedure, b.Procedure) || a.Procedure is not null && b.Procedure is not null && Contract.Value(Ast.Equivalent(a.Procedure, b.Procedure)));
    internal static bool SameObservation(VisibleObservationV1 a, VisibleObservationV1 b)
        => a.Target == b.Target && a.Source == b.Source && a.Time == b.Time && a.TimeUnit == b.TimeUnit && a.Detected == b.Detected &&
        a.DeclaredExistence == b.DeclaredExistence && a.Details == b.Details && a.Origin == b.Origin && a.Dependencies.ToHashSet().SetEquals(b.Dependencies)
        && TypedEvents.Same(a.Event, b.Event) && SameState(a.ObservedState, b.ObservedState);
    internal static bool SameProjection(FrozenProjectionV1 a, FrozenProjectionV1 b)
        => a.Id == b.Id && SameState(StateOf(a), StateOf(b)) &&
        (a.LastObservation is null ? b.LastObservation is null : b.LastObservation is not null && SameObservation(a.LastObservation, b.LastObservation));
}
