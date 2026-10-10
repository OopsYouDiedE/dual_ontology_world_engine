using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Core;

public sealed record SnapshotDecisionAssessmentV1(KnowledgeRevision Revision, VocabularyRevision VocabularyRevision,
    FrozenEffectV1 Effect, int Score, SubjectProjectionId Basis, bool Forbidden, ImmutableArray<string> Reasons);

public static partial class DecisionSystem
{
    public static ContractResult<ImmutableArray<SnapshotDecisionAssessmentV1>> Assess(CognitionSnapshotV1 snapshot, SubjectProjectionId actor)
        => Contract.Try(() =>
        {
            var graph = SnapshotGraph.Build(snapshot);
            Contract.Id(actor, snapshot.SubjectWorld, "actor");
            Contract.Require(graph.Projections.TryGetValue(actor, out var p), ContractErrorCode.InvalidReference, "actor", "决策主体不在主体图。");
            return AssessLive(graph.World, p!).Select(a => new SnapshotDecisionAssessmentV1(snapshot.Revision, snapshot.VocabularyRevision,
                CognitionAst.Freeze(a.Decision.Effect, p => p.SubjectId!.Value), a.Decision.Score, a.Decision.Basis.SubjectId!.Value, a.Forbidden, [..a.Reasons])).ToImmutableArray();
        });
}
public static partial class SocialSystem
{
    public static ContractResult<SocialAttitude> Evaluate(CognitionSnapshotV1 snapshot, SubjectProjectionId self, SubjectProjectionId request, int? compatibility = null)
        => Contract.Try(() =>
        {
            var graph = SnapshotGraph.Build(snapshot);
            return Evaluate(graph.World, Get(graph, self), Get(graph, request), compatibility) with { Revision = snapshot.Revision };
        });
    public static ContractResult<SocialAttitude> EvaluateSender(CognitionSnapshotV1 snapshot, SubjectProjectionId self,
        SubjectProjectionId? sender, int compatibility = 0, SubjectProjectionId? request = null)
        => Contract.Try(() =>
        {
            var graph = SnapshotGraph.Build(snapshot);
            return EvaluateSender(graph.World, Get(graph, self), sender is null ? null : Get(graph, sender.Value), compatibility,
                request is null ? null : Get(graph, request.Value)) with { Revision = snapshot.Revision };
        });
    private static Projection Get(DetachedProjectionGraphV1 graph, SubjectProjectionId id)
    {
        Contract.Id(id, graph.World.SubjectWorldId!.Value, "context");
        Contract.Require(graph.Projections.TryGetValue(id, out var p), ContractErrorCode.InvalidReference, "context", "社交上下文不在主体图。");
        return p!;
    }
}
public static partial class Reasoner
{
    public static ContractResult<CaseBelief> Assess(CognitionSnapshotV1 snapshot, CaseModel model)
        => Contract.Try(() => Assess(SnapshotGraph.Build(snapshot).World, model) with { Revision = snapshot.Revision });
    public static ContractResult<(double Factor, string Why)> Weight(CognitionSnapshotV1 snapshot, SubjectProjectionId claim)
        => Contract.Try(() =>
        {
            var graph = SnapshotGraph.Build(snapshot);
            Contract.Id(claim, snapshot.SubjectWorld, "claim");
            Contract.Require(graph.Projections.TryGetValue(claim, out var p), ContractErrorCode.InvalidReference, "claim", "命题不在主体图。");
            return Weight(p!);
        });
}
