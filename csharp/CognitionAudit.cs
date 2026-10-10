using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Core;

public sealed partial class WorldRuntime
{
    public ContractResult<CognitionAuditHistoryV1> CaptureCognitionAudit(SoulEssence soul) => Contract.Try(() =>
    {
        lock (soul.World.Gate) return new CognitionAuditHistoryV1(1, soul.World.SubjectWorldId!.Value,
            soul.World.InitialSnapshot, soul.World.Audit, []);
    });
    public ContractResult<CognitionGraphArchiveV1> CaptureCognitionGraph(SoulEssence soul) => Contract.Try(() =>
    {
        lock (soul.World.Gate) return new CognitionGraphArchiveV1(soul.World.Snapshot, [..soul.World.Allocated.OrderBy(Observation.IdKey, StringComparer.Ordinal)]);
    });
    public ContractResult<ImmutableArray<CognitionAuditCommitV1>> ReadCognitionAudit(SoulEssence soul, KnowledgeRevision fromRevisionExclusive)
        => Contract.Try(() =>
        {
            lock (soul.World.Gate)
            {
                Contract.Require(fromRevisionExclusive.Value >= 0 && fromRevisionExclusive.Value <= soul.World.Revision.Value,
                    ContractErrorCode.InvalidRevision, "revision", "审计增量版本不在可用范围。");
                return soul.World.Audit.Where(c => c.Revision.Value > fromRevisionExclusive.Value).ToImmutableArray();
            }
        });

    public ContractResult<CognitionSnapshotV1> ReconstructCognitionAudit(CognitionAuditHistoryV1 audit, KnowledgeRevision revision)
        => Contract.Try(() =>
        {
            Contract.Require(audit is not null, ContractErrorCode.MalformedData, "audit", "审计不能为空。");
            Contract.Require(audit!.ContractVersion == 1, ContractErrorCode.Unsupported, "contract_version", "不支持的审计版本。");
            Contract.Array(audit.Commits, "commits"); Contract.Array(audit.RequiredCapabilities, "required_capabilities");
            Contract.Require(audit.RequiredCapabilities.IsEmpty, ContractErrorCode.Unsupported, "required_capabilities", "不支持的审计必要能力。");
            var graph = SnapshotGraph.Build(audit.InitialSnapshot);
            Contract.Require(audit.SubjectWorld == audit.InitialSnapshot.SubjectWorld && audit.InitialSnapshot.Revision.Value == 0,
                ContractErrorCode.MalformedData, "initial_snapshot", "审计必须从同一主体的零版本开始。");
            Contract.Require(revision.Value >= 0 && revision.Value <= audit.Commits.Length, ContractErrorCode.InvalidRevision, "revision", "请求版本不在可用审计范围。");
            var requested = revision.Value == 0 ? audit.InitialSnapshot : null;
            foreach (var commit in audit.Commits)
            {
                Contract.Require(commit is not null, ContractErrorCode.MalformedData, "commits", "审计提交不能为空。");
                Contract.Array(commit!.Inputs, "inputs"); Contract.Array(commit.Changes, "changes"); Contract.Array(commit.ForgottenIds, "forgotten_ids");
                Contract.Array(commit.RemovedVisibleHistoryIndices, "removed_visible_history_indices"); Contract.Array(commit.AppendedVisibleHistory, "appended_visible_history");
                Contract.Require(!commit.Inputs.IsEmpty && commit.PreviousRevision == graph.World.Revision && commit.Revision.Value == commit.PreviousRevision.Value + 1 &&
                    commit.PreviousVocabularyRevision == graph.World.VocabularyRevision, ContractErrorCode.MalformedData, "revision", "审计版本断档或前态版本不符。");
                Contract.Require(commit.Inputs.All(c => c is not null) && commit.Inputs.Select(c => c.Target).Distinct().Count() == commit.Inputs.Length,
                    ContractErrorCode.InvalidBatch, "inputs", "审计输入目标重复或缺失。");
                Contract.Require(commit.Changes.All(c => c is not null) && commit.Changes.Select(c => c.Id).Distinct().Count() == commit.Changes.Length,
                    ContractErrorCode.MalformedData, "changes", "审计变化目标重复或缺失。");
                foreach (var change in commit.Changes) Contract.Id(change.Id, audit.SubjectWorld, "changes.id");
                foreach (var id in commit.ForgottenIds) Contract.Id(id, audit.SubjectWorld, "forgotten_ids");
                var prior = graph.World.Snapshot;
                var beforeRecords = commit.Changes.Where(c => c.Before is not null).ToDictionary(c => c.Id, c => c.Before!);
                SnapshotGraph.Build(prior with { Projections = [..prior.Projections.Select(p => beforeRecords.GetValueOrDefault(p.Id) ?? p)] });
                var live = graph.Projections.ToDictionary();
                foreach (var c in commit.Inputs)
                {
                    Contract.Require(c is not null, ContractErrorCode.MalformedData, "inputs", "审计输入不能为空。");
                    if (!c!.Create) continue;
                    Contract.Id(c.Target, audit.SubjectWorld, "target");
                    Contract.Require(graph.World.Allocated.Add(c.Target), ContractErrorCode.DuplicateIdentity, "target", "审计新建身份重复。");
                    live.Add(c.Target, new Projection(graph.World, c.Name, c.Kind, c.Target));
                }
                Observation.Publish(graph.World, commit.Inputs, live, commit.PreviousRevision, null);
                var produced = graph.World.Audit[^1];
                var afterRecords = commit.Changes.Where(c => c.After is not null).ToDictionary(c => c.Id, c => c.After!);
                SnapshotGraph.Build(graph.World.Snapshot with { Projections = [..graph.World.Snapshot.Projections.Select(p => afterRecords.GetValueOrDefault(p.Id) ?? p)] });
                Contract.Require(produced.VocabularyRevision == commit.VocabularyRevision && produced.Changes.Length == commit.Changes.Length &&
                    produced.Changes.Zip(commit.Changes).All(p => SameChange(p.First, p.Second)) &&
                    produced.ForgottenIds.SequenceEqual(commit.ForgottenIds) && produced.RemovedVisibleHistoryIndices.SequenceEqual(commit.RemovedVisibleHistoryIndices) &&
                    produced.AppendedVisibleHistory.Length == commit.AppendedVisibleHistory.Length && produced.AppendedVisibleHistory.Zip(commit.AppendedVisibleHistory).All(p =>
                        p.Second is not null && SnapshotGraph.SameObservation(p.First, p.Second)), ContractErrorCode.MalformedData, "commit", "审计变化与完整重建结果不符。");
                if (commit.Revision == revision) requested = graph.World.Snapshot;
                graph = new(graph.World, live.Where(p => graph.World.Contains(p.Value)).ToImmutableDictionary());
            }
            return requested!;
        });
    private static bool SameChange(CognitionAuditChangeV1 a, CognitionAuditChangeV1? b)
        => b is not null && a.Id == b.Id &&
        (a.Before is null ? b.Before is null : b.Before is not null && SnapshotGraph.SameProjection(a.Before, b.Before)) &&
        (a.After is null ? b.After is null : b.After is not null && SnapshotGraph.SameProjection(a.After, b.After));
}
