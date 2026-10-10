using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Core;

public static class Observation
{
    public static Projection Observe(SoulEssence soul, ObservationInput input)
        => Apply(soul.World, input);

    public static ContractResult<CognitionCommitReceiptV1> ObserveBatch(SoulEssence soul,
        ImmutableArray<ObservationChangeV1> changes, KnowledgeRevision? expectedRevision = null)
        => Contract.Try(() => CommitBatch(soul.World, changes, expectedRevision));

    internal static Projection Apply(ProjectionWorld world, ObservationInput input)
        => ApplyBatch(world, [input])[0];

    // Only the engine observation adapter supplies a prepared binding publication, never a public callback.
    internal static ImmutableArray<Projection> ApplyBatch(ProjectionWorld world, IReadOnlyList<ObservationInput> inputs,
        KnowledgeRevision? expectedRevision = null, WorldRuntime.BindingPublication? bindings = null)
    {
        ImmutableArray<Projection> targets;
        lock (world.Gate)
        {
            var before = world.Snapshot;
            Expected(before.Revision, expectedRevision);
            var live = world.Projections.ToDictionary(p => p.SubjectId!.Value);
            var incoming = new List<Projection>();
            foreach (var input in inputs)
            {
                Contract.Require(input is not null, ContractErrorCode.MalformedData, "inputs", "观察输入不能为空。");
                var target = input!.Target ?? new Projection(world, input.Name, input.Kind);
                Contract.Require(ReferenceEquals(target.World, world) && target.SubjectId is not null, ContractErrorCode.InvalidReference, "target", "观察目标须属于接收主体。");
                incoming.Add(target);
                live.TryAdd(target.SubjectId!.Value, target);
            }
            SubjectProjectionId Id(Projection p)
            {
                Contract.Require(p is not null && ReferenceEquals(p.World, world) && p.SubjectId is { } id && live.GetValueOrDefault(id) == p,
                    ContractErrorCode.InvalidReference, "reference", "观察引用必须属于当前主体图或本批新节点。");
                return p!.SubjectId!.Value;
            }
            ImmutableArray<RelationEdgeV1> Edges(ImmutableHashSet<(RelationTag Relation, Projection Target)> edges)
            {
                Contract.Require(edges is not null, ContractErrorCode.MalformedData, "relations", "关系集合不能为空。");
                return [..edges!.Select(r => new RelationEdgeV1(r.Relation, Id(r.Target)))];
            }
            var changes = inputs.Select((i, n) =>
            {
                var p = incoming[n];
                ImmutableArray<FrozenRuleV1>? rules = null;
                if (i.Rules is { } rs) { Contract.Array(rs, "rules"); rules = [..rs.Select(r => CognitionAst.Freeze(r, Id))]; }
                return new ObservationChangeV1(Id(p), !world.Contains(p), i.Name, i.Kind, i.Source, i.Time, i.TimeUnit,
                    i.Detected, i.Existence, i.Move, i.Parent is null ? null : Id(i.Parent), i.Tags,
                    Edges(i.AddRelations), Edges(i.RemoveRelations), Edges(i.DenyRelations), rules,
                    i.Procedure is null ? null : CognitionAst.Freeze(i.Procedure, Id), i.ClearProcedure, i.Forget,
                    i.KnownTerms, i.Details, i.Origin, i.SourceDependencies);
            }).ToImmutableArray();
            Publish(world, changes, live, expectedRevision, bindings);
            targets = [..incoming];
        }
        world.DrainNotifications();
        return targets;
    }

    private static CognitionCommitReceiptV1 CommitBatch(ProjectionWorld world, ImmutableArray<ObservationChangeV1> changes, KnowledgeRevision? expected)
    {
        CognitionCommitReceiptV1 receipt;
        lock (world.Gate)
        {
            Contract.Array(changes, "changes");
            var live = world.Projections.ToDictionary(p => p.SubjectId!.Value);
            foreach (var change in changes)
            {
                Contract.Require(change is not null, ContractErrorCode.MalformedData, "changes", "批输入不能为空。");
                if (change!.Create)
                {
                    Contract.Id(change.Target, world.SubjectWorldId ?? Guid.Empty, "target");
                    if (!live.ContainsKey(change.Target)) live.Add(change.Target, new Projection(world, change.Name, change.Kind, change.Target));
                }
            }
            receipt = Publish(world, changes, live, expected, null);
        }
        if (changes.IsEmpty) return receipt;
        world.DrainNotifications();
        var delivered = Contract.Value(world.ReadNotificationReceipt(receipt.Revision));
        return receipt with { NotificationStatus = delivered.Status, NotificationFaults = delivered.NotificationFaults };
    }

    private static void Expected(KnowledgeRevision actual, KnowledgeRevision? expected)
    {
        if (expected is null) return;
        Contract.Require(expected.Value.Value >= 0, ContractErrorCode.InvalidRevision, "expected_revision", "版本不能为负。");
        if (expected != actual) throw new CognitionContractException(new(ContractErrorCode.StaleRevision, "expected_revision", "认知版本已变化。",
            ExpectedRevision: expected, ActualRevision: actual, CandidateIds: []));
    }
    internal static CognitionCommitReceiptV1 Publish(ProjectionWorld world, ImmutableArray<ObservationChangeV1> changes,
        Dictionary<SubjectProjectionId, Projection> live, KnowledgeRevision? expected, WorldRuntime.BindingPublication? bindings)
    {
        Contract.Array(changes, "changes");
        try { return PrepareAndPublish(world, changes, live, expected, bindings); }
        finally
        {
            // A submitted reservation is consumed even on validation failure; it never denotes a later node.
            foreach (var c in changes)
                if (c is { Create: true } && world.Allocated.Contains(c.Target)) world.Used.Add(c.Target);
        }
    }
    private static CognitionCommitReceiptV1 PrepareAndPublish(ProjectionWorld world, ImmutableArray<ObservationChangeV1> changes,
        Dictionary<SubjectProjectionId, Projection> live, KnowledgeRevision? expected, WorldRuntime.BindingPublication? bindings)
    {
        Contract.Require(world.SubjectWorldId is not null, ContractErrorCode.Unsupported, "world", "现实世界不接受主体认知批次。");
        Contract.Array(changes, "changes");
        var before = world.Snapshot;
        Expected(before.Revision, expected);
        if (changes.IsEmpty) return new(before.Revision, before.Revision, before.VocabularyRevision, [], NotificationDeliveryV1.Completed, []);
        Contract.Require(before.Revision.Value < long.MaxValue, ContractErrorCode.RevisionOverflow, "revision", "认知版本溢出。");
        var ids = new HashSet<SubjectProjectionId>();
        var allowed = before.Projections.Select(p => p.Id).ToHashSet();
        foreach (var c in changes)
        {
            Contract.Require(c is not null, ContractErrorCode.MalformedData, "changes", "批输入不能为空。");
            Contract.Id(c!.Target, world.SubjectWorldId!.Value, "target");
            Contract.Require(ids.Add(c.Target), ContractErrorCode.InvalidBatch, "target", "批次不能重复指定同一目标。");
            Contract.Require(!(c.Create && c.Forget), ContractErrorCode.InvalidBatch, "create", "新建与遗忘不能同时使用。");
            if (c.Create)
            {
                Contract.Require(world.Allocated.Contains(c.Target) && !world.Used.Contains(c.Target), ContractErrorCode.InvalidReference, "target", "新建必须使用未消费的保留句柄。");
                allowed.Add(c.Target);
            }
            else Contract.Require(allowed.Contains(c.Target), ContractErrorCode.InvalidReference, "target", "目标已经失效或未获知。");
        }
        // live consists of the preceding published nodes plus this batch's reserved creations.
        // All targets were checked above; do not copy the same reference map for each input validation.
        var validationMap = live;
        foreach (var c in changes) ValidateInput(c, world.SubjectWorldId!.Value, allowed, validationMap);
        var previous = before.Projections.ToDictionary(p => p.Id);
        var nodes = new Dictionary<SubjectProjectionId, FrozenProjectionV1>(previous);
        var touched = new HashSet<SubjectProjectionId>();
        var appended = new List<VisibleObservationV1>();
        foreach (var c in changes.Where(c => !c.Forget))
        {
            var p = c.Create ? new FrozenProjectionV1(c.Target, c.Name, c.Kind, Truth.Unknown, null,
                ImmutableDictionary<Tag, int>.Empty, [], [], [], null, [], null) : nodes[c.Target];
            var tags = p.Tags;
            foreach (var (tag, value) in c.Tags) tags = value is { } n ? tags.SetItem(tag, n) : tags.Remove(tag);
            var add = c.AddRelations.ToHashSet(); var remove = c.RemoveRelations.ToHashSet(); var deny = c.DenyRelations.ToHashSet();
            var relations = p.Relations.ToHashSet(); var denied = p.DeniedRelations.ToHashSet();
            relations.ExceptWith(remove); relations.ExceptWith(deny); relations.UnionWith(add);
            denied.ExceptWith(remove); denied.ExceptWith(add); denied.UnionWith(deny.Except(add));
            p = p with { Name = c.Name, Kind = c.Kind, Existence = c.Existence ?? p.Existence,
                Parent = c.Move ? c.Parent : p.Parent, Tags = tags,
                Relations = c.AddRelations.IsEmpty && c.RemoveRelations.IsEmpty && c.DenyRelations.IsEmpty ? p.Relations : SortEdges(relations),
                DeniedRelations = c.AddRelations.IsEmpty && c.RemoveRelations.IsEmpty && c.DenyRelations.IsEmpty ? p.DeniedRelations : SortEdges(denied),
                Rules = c.Rules ?? p.Rules, Procedure = c.ClearProcedure || c.Procedure is not null ? c.Procedure : p.Procedure,
                KnownTerms = c.KnownTerms ?? p.KnownTerms };
            var dependencies = Dependencies(p).Union(c.SourceDependencies).Append(p.Id).Distinct().OrderBy(IdKey).ToImmutableArray();
            var observation = new VisibleObservationV1(p.Id, c.Source, c.Time, c.TimeUnit, c.Detected, c.Existence, c.Details,
                c.Origin, dependencies, SnapshotGraph.StateOf(p));
            appended.Add(observation); nodes[p.Id] = p with { LastObservation = observation }; touched.Add(p.Id);
        }
        var forgotten = changes.Where(c => c.Forget).Select(c => c.Target).ToHashSet();
        foreach (var id in forgotten) nodes.Remove(id);
        if (forgotten.Count != 0) foreach (var p in nodes.Values.ToArray())
        {
            var rules = p.Rules.Where(r => !CognitionAst.References(r).Any(forgotten.Contains)).ToImmutableArray();
            var procedure = p.Procedure is not null && CognitionAst.References(p.Procedure).Any(forgotten.Contains) ? null : p.Procedure;
            var parent = p.Parent is { } oldParent && forgotten.Contains(oldParent) ? null : p.Parent;
            var relations = p.Relations.Where(r => !forgotten.Contains(r.Target)).ToImmutableArray();
            var denied = p.DeniedRelations.Where(r => !forgotten.Contains(r.Target)).ToImmutableArray();
            if (parent == p.Parent && relations.Length == p.Relations.Length && denied.Length == p.DeniedRelations.Length &&
                rules.Length == p.Rules.Length && ReferenceEquals(procedure, p.Procedure)) continue;
            nodes[p.Id] = p with
            {
                Parent = parent, Relations = relations, DeniedRelations = denied, Rules = rules, Procedure = procedure
            };
            touched.Add(p.Id);
        }
        bool Retained(VisibleObservationV1 o) => !forgotten.Contains(o.Target) && !o.Dependencies.Any(forgotten.Contains);
        var removed = forgotten.Count == 0 ? [] : before.VisibleHistory.Select((o, n) => (o, n))
            .Where(p => !Retained(p.o)).Select(p => p.n).ToImmutableArray();
        var retainedAppended = appended.Where(Retained).ToImmutableArray();
        var history = (forgotten.Count == 0 ? before.VisibleHistory : before.VisibleHistory.Where(Retained).ToImmutableArray()).AddRange(retainedAppended);
        if (forgotten.Count != 0) foreach (var p in nodes.Values.ToArray())
            if (p.LastObservation is { } last && !Retained(last))
            { nodes[p.Id] = p with { LastObservation = null }; touched.Add(p.Id); }
        // The preceding revision is closed and validated. Only a changed parent can introduce a new cycle.
        SnapshotGraph.ValidateParents(nodes, touched);
        var vocabularyChanged = forgotten.Count != 0 || changes.Any(c => c.Create) || touched.Any(id =>
            previous.TryGetValue(id, out var old) && (old.Name != nodes[id].Name ||
                old.KnownTerms != nodes[id].KnownTerms && !old.KnownTerms.ToHashSet().SetEquals(nodes[id].KnownTerms)));
        Contract.Require(!vocabularyChanged || before.VocabularyRevision.Value < long.MaxValue, ContractErrorCode.RevisionOverflow, "vocabulary_revision", "词典版本溢出。");
        var after = new CognitionSnapshotV1(1, before.SubjectWorld, new(before.Revision.Value + 1),
            new(before.VocabularyRevision.Value + (vocabularyChanged ? 1 : 0)),
            [..before.Projections.Where(p => nodes.ContainsKey(p.Id)).Select(p => nodes[p.Id]).Concat(changes.Where(c => c.Create).Select(c => nodes[c.Target]))], history, []);
        var finalMap = forgotten.Count == 0 && !changes.Any(c => c.Create) && world.ReaderOriginals is { } originals
            ? originals : live.Where(p => nodes.ContainsKey(p.Key)).ToImmutableDictionary();
        ObservationEvent Legacy(VisibleObservationV1 o) => new(finalMap[o.Target], o.Source, o.Time, o.Detected, o.DeclaredExistence,
            o.Details, o.ObservedState.Tags, [..o.ObservedState.Relations.Select(r => (r.Relation, finalMap[r.Target]))]);
        var prepared = touched.Select(id =>
        {
            var p = nodes[id]; var original = finalMap[id]; var old = previous.GetValueOrDefault(id);
            return (State: p, Parent: p.Parent is { } parent ? finalMap[parent] : null,
                Relations: old is not null && old.Relations == p.Relations ? original.Relations :
                    p.Relations.Select(r => (r.Relation, finalMap[r.Target])).ToImmutableHashSet(),
                Denied: old is not null && old.DeniedRelations == p.DeniedRelations ? original.DeniedRelations :
                    p.DeniedRelations.Select(r => (r.Relation, finalMap[r.Target])).ToImmutableHashSet(),
                Rules: old is not null && old.Rules == p.Rules ? original.Rules : p.Rules.Select(r => CognitionAst.Thaw(r, finalMap)).ToImmutableArray(),
                Procedure: old is not null && ReferenceEquals(old.Procedure, p.Procedure) ? original.Procedure :
                    p.Procedure is null ? null : CognitionAst.Thaw(p.Procedure, finalMap),
                Last: p.LastObservation is null ? null : Legacy(p.LastObservation));
        }).ToArray();
        var removedSet = removed.ToHashSet();
        var legacyHistory = (removed.IsEmpty ? world.History : world.History.Where((_, n) => !removedSet.Contains(n)).ToImmutableList())
            .AddRange(retainedAppended.Select(Legacy));
        var auditChanges = before.Projections.Where(p => touched.Contains(p.Id) || forgotten.Contains(p.Id)).Select(p => p.Id)
            .Concat(changes.Where(c => c.Create).Select(c => c.Target))
            .Select(id => new CognitionAuditChangeV1(id, previous.GetValueOrDefault(id), nodes.GetValueOrDefault(id)))
            .Where(c => c.Before is null || c.After is null || !SnapshotGraph.SameProjection(c.Before, c.After)).ToImmutableArray();
        var audit = new CognitionAuditCommitV1(before.Revision, after.Revision, before.VocabularyRevision, after.VocabularyRevision,
            changes, auditChanges, [..forgotten.OrderBy(IdKey)], removed, retainedAppended);
        var allAudit = world.Audit.Add(audit);
        var projections = after.Projections.Select(p => finalMap[p.Id]).ToImmutableArray();
        foreach (var (state, parent, relations, denied, rules, procedure, last) in prepared)
        {
            var p = finalMap[state.Id];
            p.Name = state.Name; p.Kind = state.Kind; p.Existence = state.Existence;
            p.Parent = parent; p.Tags = state.Tags; p.Relations = relations; p.DeniedRelations = denied;
            p.Rules = rules; p.Procedure = procedure; p.KnownTerms = state.KnownTerms; p.LastObservation = last;
        }
        foreach (var c in changes.Where(c => c.Create)) world.Used.Add(c.Target);
        world.Projections = projections; world.History = legacyHistory; world.Snapshot = after; world.Audit = allAudit;
        world.ReaderGraph = null; world.ReaderOriginals = finalMap;
        bindings?.Publish();
        world.QueueNotification();
        return new(before.Revision, after.Revision, after.VocabularyRevision, [..changes.Select(c => c.Target)], NotificationDeliveryV1.Pending, []);
    }
    internal static string IdKey(SubjectProjectionId id) => id.SubjectWorld.ToString("D") + "/" + id.Local.ToString("D");
    private static ImmutableArray<RelationEdgeV1> SortEdges(IEnumerable<RelationEdgeV1> edges)
        => [..edges.OrderBy(r => r.Relation.Name, StringComparer.Ordinal).ThenBy(r => IdKey(r.Target), StringComparer.Ordinal)];
    private static IEnumerable<SubjectProjectionId> Dependencies(FrozenProjectionV1 p)
        => p.Relations.Concat(p.DeniedRelations).Select(r => r.Target).Concat(p.Parent is { } parent ? [parent] : [])
            .Concat(p.Rules.SelectMany(CognitionAst.References)).Concat(p.Procedure is null ? [] : CognitionAst.References(p.Procedure));

    internal static void ValidateInput(ObservationChangeV1 c, Guid world, HashSet<SubjectProjectionId> allowed,
        IReadOnlyDictionary<SubjectProjectionId, Projection> map)
    {
        void Ref(SubjectProjectionId id) { Contract.Id(id, world, "reference"); Contract.Require(allowed.Contains(id), ContractErrorCode.InvalidReference, "reference", "输入引用不在前态或本批新建图。"); }
        Contract.Text(c.Name, "name"); Contract.Enum(c.Kind, "kind"); Contract.Enum(c.Source, "source"); Contract.Enum(c.TimeUnit, "time_unit");
        Contract.Number(c.Time, "time"); Contract.Text(c.Details, "details");
        if (c.Existence is { } existence) Contract.Enum(existence, "existence");
        if (c.Parent is { } parent) Ref(parent);
        Contract.Require(c.Tags is not null, ContractErrorCode.MalformedData, "tags", "标签字典不能为空。");
        foreach (var tag in c.Tags!.Keys) Contract.Identity(tag.Name, "tag");
        foreach (var edges in new[] { c.AddRelations, c.RemoveRelations, c.DenyRelations })
        {
            Contract.Array(edges, "relations"); var seen = new HashSet<RelationEdgeV1>();
            foreach (var edge in edges)
            {
                Contract.Require(edge is not null, ContractErrorCode.MalformedData, "relations", "关系不能为空。");
                Contract.Identity(edge!.Relation.Name, "relation"); Ref(edge.Target);
                Contract.Require(seen.Add(edge), ContractErrorCode.InvalidBatch, "relations", "输入关系重复。");
            }
        }
        if (c.Rules is { } rules) { Contract.Array(rules, "rules"); foreach (var rule in rules) CognitionAst.Thaw(rule, map); }
        if (c.Procedure is not null) CognitionAst.Thaw(c.Procedure, map);
        if (c.KnownTerms is { } terms) SnapshotGraph.Terms(terms);
        Contract.Array(c.SourceDependencies, "source_dependencies"); foreach (var dependency in c.SourceDependencies) Ref(dependency);
        Contract.Require(c.SourceDependencies.Distinct().Count() == c.SourceDependencies.Length, ContractErrorCode.MalformedData,
            "source_dependencies", "来源依赖不能重复。");
        SnapshotGraph.Origin(c.Origin);
    }
}
