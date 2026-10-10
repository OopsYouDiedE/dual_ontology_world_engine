using System.Collections.Immutable;
using System.Text;

namespace DualOntologyWorldEngine.Core;

public enum TermInterpretationStatus { Unrecognized, Ambiguous, Unknown, Unsupported, InvalidReference, Resolved }
public sealed record TermQueryV1(string? Lexeme = null, AuthorTermId? TermId = null,
    FrozenConditionV1? Qualifier = null, SubjectProjectionId? Reference = null);
public sealed record TermCandidateV1(SubjectProjectionId Target, Truth Qualification);
public sealed record TermInterpretationV1(TermInterpretationStatus Status, Guid SubjectWorld,
    KnowledgeRevision Revision, VocabularyRevision VocabularyRevision,
    ImmutableArray<TermCandidateV1> Candidates, ContractError? Error = null)
{
    public SubjectProjectionId? ResolvedTarget => Status == TermInterpretationStatus.Resolved && Candidates.Length == 1 ? Candidates[0].Target : null;
}

/// <summary>Author terms are interpreted only in acquired vocabulary, never through objective names.</summary>
public static class ChineseTermBridge
{
    public static ContractResult<bool> ValidateSyntax(TermQueryV1 query) => Contract.Try(() =>
    {
        Contract.Require(query is not null, ContractErrorCode.MalformedData, "query", "词项查询不能为空。");
        Contract.Require(query!.Lexeme is not null || query.TermId is not null || query.Reference is not null,
            ContractErrorCode.MalformedData, "query", "查询须提供词形、作者词项或主体句柄。");
        if (query.TermId is { } term) Contract.Identity(term.Value, "term_id");
        if (query.Reference is { } reference) Contract.Id(reference, reference.SubjectWorld, "reference");
        if (query.Lexeme is { } lexeme) Contract.Require(!string.IsNullOrWhiteSpace(lexeme), ContractErrorCode.MalformedData, "lexeme", "词形不能为空。");
        if (query.Qualifier is { } qualifier) Contract.Value(Ast.Equivalent(qualifier, qualifier));
        return true;
    });
    public static TermInterpretationV1 Interpret(CognitionSnapshotV1 snapshot, TermQueryV1 query,
        KnowledgeRevision? expectedRevision = null, VocabularyRevision? expectedVocabularyRevision = null)
    {
        var result = Contract.Try(() =>
        {
            Contract.Value(ValidateSyntax(query));
            var graph = SnapshotGraph.Build(snapshot);
            if (expectedRevision is not null && expectedRevision != snapshot.Revision || expectedVocabularyRevision is not null && expectedVocabularyRevision != snapshot.VocabularyRevision)
                throw new CognitionContractException(new(ContractErrorCode.StaleRevision, "revision", "解释凭证的认知或词典版本已失效。",
                    ExpectedRevision: expectedRevision, ActualRevision: snapshot.Revision, CandidateIds: []));
            var canonical = query.Lexeme?.Normalize(NormalizationForm.FormC);
            var recognized = snapshot.Projections.Where(p =>
                (query.Reference is null || p.Id == query.Reference) &&
                (query.Lexeme is null && query.TermId is null || p.KnownTerms.Any(t =>
                    (query.TermId is null || t.TermId == query.TermId) && (canonical is null || string.Equals(t.CanonicalLexeme, canonical, StringComparison.Ordinal))))).ToArray();
            if (query.Reference is { } reference)
            {
                Contract.Id(reference, snapshot.SubjectWorld, "reference");
                Contract.Require(graph.Projections.ContainsKey(reference), ContractErrorCode.InvalidReference, "reference", "主体引用已遗忘或不存在。");
            }
            var condition = query.Qualifier is null ? null : CognitionAst.Thaw(query.Qualifier, graph.Projections);
            var candidates = recognized.Select(p =>
            {
                var target = graph.Projections[p.Id];
                var truth = condition is null ? p.Existence : Ast.EvaluateLive(condition, new(target, target, Scene: target.Parent));
                return new TermCandidateV1(p.Id, truth);
            }).Where(c => c.Qualification != Truth.False).OrderBy(c => Observation.IdKey(c.Target), StringComparer.Ordinal).ToImmutableArray();
            var status = candidates.Length == 0 ? TermInterpretationStatus.Unrecognized : candidates.Length > 1 ? TermInterpretationStatus.Ambiguous :
                candidates[0].Qualification == Truth.Unknown ? TermInterpretationStatus.Unknown : TermInterpretationStatus.Resolved;
            return new TermInterpretationV1(status, snapshot.SubjectWorld, snapshot.Revision, snapshot.VocabularyRevision, candidates);
        });
        if (result is ContractResult<TermInterpretationV1>.Success success) return success.Value;
        var error = ((ContractResult<TermInterpretationV1>.Failure)result).Error;
        return new(error.Code == ContractErrorCode.Unsupported ? TermInterpretationStatus.Unsupported : TermInterpretationStatus.InvalidReference,
            snapshot?.SubjectWorld ?? Guid.Empty, snapshot?.Revision ?? new(0), snapshot?.VocabularyRevision ?? new(0), [], error);
    }
}

public sealed partial class WorldRuntime
{
    /// <summary>Returns the existing execution target; this bridge creates no new effects.</summary>
    public ContractResult<Projection> BindResolvedTerm(SoulEssence soul, TermInterpretationV1 interpretation) => Contract.Try(() =>
    {
        lock (soul.World.Gate)
        {
            Contract.Require(interpretation is not null, ContractErrorCode.MalformedData, "interpretation", "解释凭证不能为空。");
            Contract.Require(interpretation!.SubjectWorld == soul.World.SubjectWorldId, ContractErrorCode.InvalidReference, "subject_world", "解释凭证属于另一主体。");
            Contract.Enum(interpretation.Status, "interpretation.status");
            if (interpretation.Revision != soul.World.Revision || interpretation.VocabularyRevision != soul.World.VocabularyRevision)
                throw new CognitionContractException(new(ContractErrorCode.StaleRevision, "revision", "解释凭证已失效。",
                    ExpectedRevision: interpretation.Revision, ActualRevision: soul.World.Revision, CandidateIds: []));
            Contract.Array(interpretation.Candidates, "candidates");
            foreach (var candidate in interpretation.Candidates)
            {
                Contract.Require(candidate is not null, ContractErrorCode.MalformedData, "candidate", "词项候选不能为空。");
                Contract.Id(candidate!.Target, interpretation.SubjectWorld, "candidate.target"); Contract.Enum(candidate.Qualification, "candidate.qualification");
            }
            Contract.Require(interpretation.ResolvedTarget is not null && interpretation.Candidates[0].Qualification == Truth.True,
                ContractErrorCode.InvalidReference, "interpretation", "只有明确消歧的目标可以绑定。");
            var target = soul.World.Projections.FirstOrDefault(p => p.SubjectId == interpretation.ResolvedTarget);
            Contract.Require(target is not null, ContractErrorCode.InvalidReference, "target", "目标已失效。");
            return target!;
        }
    });
}
