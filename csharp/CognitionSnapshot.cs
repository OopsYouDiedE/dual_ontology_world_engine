using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Core;

public enum ObservationTimeUnitV1 { Unspecified, ActionValue, GameMinutes, GameDays }

// Source text is external authored data, never an ID or truth proof.
public sealed record SourceLocationV1(string DocumentId,
 int StartLine, int StartColumn, int EndLine, int EndColumn);
public sealed record KnownAuthorTermV1(AuthorTermId TermId,
 string RawLexeme, string CanonicalLexeme);
public sealed record RelationEdgeV1(RelationTag Relation, SubjectProjectionId Target);

// Closed AST whitelist: external/custom subclasses rejected, never reflected.
public abstract record FrozenNounV1 {
 public sealed record Reference(SubjectProjectionId Target) : FrozenNounV1;
 public sealed record Slot(RuleSlot Role) : FrozenNounV1;
}
public abstract record FrozenConditionV1 {
 public sealed record Constant(Truth Value) : FrozenConditionV1;
 public sealed record Exists(FrozenNounV1 Target) : FrozenConditionV1;
 public sealed record Tagged(FrozenNounV1 Target, Tag Tag, int Minimum) : FrozenConditionV1;
 public sealed record Related(FrozenNounV1 Target, RelationTag Relation, FrozenNounV1 Other) : FrozenConditionV1;
 public sealed record At(FrozenNounV1 Target, FrozenNounV1 Space) : FrozenConditionV1;
 public sealed record IsKind(FrozenNounV1 Target, ProjectionKind Kind) : FrozenConditionV1;
 public sealed record Not(FrozenConditionV1 Operand) : FrozenConditionV1;
 public sealed record All(ImmutableArray<FrozenConditionV1> Items) : FrozenConditionV1;
 public sealed record Any(ImmutableArray<FrozenConditionV1> Items) : FrozenConditionV1;
}
public abstract record FrozenEffectV1 {
 public sealed record Act(ActionOp Operation, FrozenNounV1 Target, FrozenNounV1? Tool = null) : FrozenEffectV1;
 public sealed record Set(FrozenNounV1 Target, Tag Tag, int Value) : FrozenEffectV1;
 public sealed record Change(FrozenNounV1 Target, Tag Tag, int Amount) : FrozenEffectV1;
 public sealed record TemporarySet(FrozenNounV1 Target, Tag Tag, int Value, double Duration) : FrozenEffectV1;
 public sealed record TemporarySetUntilEnd(FrozenNounV1 Target, Tag Tag, int Value) : FrozenEffectV1;
 public sealed record Spend(FrozenNounV1 Target, Tag Tag, int Amount) : FrozenEffectV1;
 public sealed record Relate(FrozenNounV1 Target, RelationTag Relation, FrozenNounV1 Other, bool Remove = false) : FrozenEffectV1;
 public sealed record Sequence(ImmutableArray<FrozenEffectV1> Items) : FrozenEffectV1;
 public sealed record When(FrozenConditionV1 Condition, FrozenEffectV1 Body) : FrozenEffectV1;
 public sealed record Require(FrozenConditionV1 Condition) : FrozenEffectV1;
 public sealed record Wait(double Delay, FrozenEffectV1 Body, FrozenConditionV1? Until = null) : FrozenEffectV1;
 public sealed record Say(FrozenNounV1 Recipient, string Text, FrozenEffectV1? Request = null) : FrozenEffectV1;
 public sealed record Think(FrozenConditionV1 Fact) : FrozenEffectV1;
}
public sealed record FrozenRuleV1(FrozenConditionV1 Condition, RuleMode Mode,
 FrozenEffectV1 Effect, int Weight, Truth Acceptance);

// Contains complete state, but no LastObservation, avoiding recursive history.
public sealed record ObservedStateV1(string Name, ProjectionKind Kind, Truth Existence,
 SubjectProjectionId? Parent, ImmutableDictionary<Tag,int> Tags,
 ImmutableArray<RelationEdgeV1> Relations, ImmutableArray<RelationEdgeV1> DeniedRelations,
 ImmutableArray<FrozenRuleV1> Rules, FrozenEffectV1? Procedure,
 ImmutableArray<KnownAuthorTermV1> KnownTerms);
public sealed record VisibleObservationV1(SubjectProjectionId Target,
 ObservationSource Source, double Time, ObservationTimeUnitV1 TimeUnit,
 bool? Detected, Truth? DeclaredExistence,
 string Details, SourceLocationV1? Origin,
 ImmutableArray<SubjectProjectionId> Dependencies, ObservedStateV1 ObservedState);
public sealed record FrozenProjectionV1(SubjectProjectionId Id,
 string Name, ProjectionKind Kind, Truth Existence, SubjectProjectionId? Parent,
 ImmutableDictionary<Tag,int> Tags, ImmutableArray<RelationEdgeV1> Relations,
 ImmutableArray<RelationEdgeV1> DeniedRelations, ImmutableArray<FrozenRuleV1> Rules,
 FrozenEffectV1? Procedure, ImmutableArray<KnownAuthorTermV1> KnownTerms,
 VisibleObservationV1? LastObservation);
public sealed record CognitionSnapshotV1(int ContractVersion, Guid SubjectWorld,
 KnowledgeRevision Revision, VocabularyRevision VocabularyRevision,
 ImmutableArray<FrozenProjectionV1> Projections,
 ImmutableArray<VisibleObservationV1> VisibleHistory,
 ImmutableArray<string> RequiredCapabilities);
public sealed record SnapshotActionContextV1(SubjectProjectionId Actor,
 SubjectProjectionId? Target = null, SubjectProjectionId? Tool = null,
 SubjectProjectionId? Scene = null);
public sealed record SnapshotTruthV1(KnowledgeRevision Revision, Truth Value);

// Null in optional update fields means unchanged, not absent state.
// Create requires a reserved fresh ID. Name/Kind remain explicit as legacy input.
public sealed record ObservationChangeV1(SubjectProjectionId Target, bool Create,
 string Name, ProjectionKind Kind, ObservationSource Source, double Time,
 ObservationTimeUnitV1 TimeUnit,
 bool? Detected, Truth? Existence, bool Move, SubjectProjectionId? Parent,
 ImmutableDictionary<Tag,int?> Tags,
 ImmutableArray<RelationEdgeV1> AddRelations, ImmutableArray<RelationEdgeV1> RemoveRelations,
 ImmutableArray<RelationEdgeV1> DenyRelations, ImmutableArray<FrozenRuleV1>? Rules,
 FrozenEffectV1? Procedure, bool ClearProcedure, bool Forget,
 ImmutableArray<KnownAuthorTermV1>? KnownTerms, string Details,
 SourceLocationV1? Origin, ImmutableArray<SubjectProjectionId> SourceDependencies);
public enum NotificationDeliveryV1 { Pending, Completed }
public sealed record CognitionNotificationReceiptV1(KnowledgeRevision Revision,
 NotificationDeliveryV1 Status, ImmutableArray<ContractError> NotificationFaults);
public sealed record CognitionCommitReceiptV1(KnowledgeRevision PreviousRevision,
 KnowledgeRevision Revision, VocabularyRevision VocabularyRevision,
 ImmutableArray<SubjectProjectionId> Targets,
 NotificationDeliveryV1 NotificationStatus,
 ImmutableArray<ContractError> NotificationFaults);
public sealed record CognitionChangedV1(KnowledgeRevision PublishedRevision,
 VocabularyRevision PublishedVocabularyRevision);

// Engine-only type domain; never appended to CognitionSnapshot.VisibleHistory.
public sealed record CognitionAuditChangeV1(SubjectProjectionId Id,
 FrozenProjectionV1? Before, FrozenProjectionV1? After);
public sealed record CognitionAuditCommitV1(KnowledgeRevision PreviousRevision,
 KnowledgeRevision Revision, VocabularyRevision PreviousVocabularyRevision,
 VocabularyRevision VocabularyRevision, ImmutableArray<ObservationChangeV1> Inputs,
 ImmutableArray<CognitionAuditChangeV1> Changes,
 ImmutableArray<SubjectProjectionId> ForgottenIds,
 ImmutableArray<int> RemovedVisibleHistoryIndices,
 ImmutableArray<VisibleObservationV1> AppendedVisibleHistory);
public sealed record CognitionAuditHistoryV1(int ContractVersion, Guid SubjectWorld,
 CognitionSnapshotV1 InitialSnapshot, ImmutableArray<CognitionAuditCommitV1> Commits,
 ImmutableArray<string> RequiredCapabilities);
// Engine persistence metadata, including retired/reserved opaque IDs; never planner input.
public sealed record CognitionGraphArchiveV1(CognitionSnapshotV1 SubjectSnapshot,
 ImmutableArray<SubjectProjectionId> AllocatedIds);
public sealed record DetachedProjectionGraphV1(ProjectionWorld World,
 ImmutableDictionary<SubjectProjectionId,Projection> Projections);
public sealed record CognitionForkV1(DetachedProjectionGraphV1 Graph,
 ImmutableDictionary<SubjectProjectionId,SubjectProjectionId> RebindMap);
public sealed record CodecLimitsV1(long MaxBytes = 67108864,
 int MaxProjections = 100000, int MaxAstNodes = 1000000,
 int MaxAstDepth = 256, int MaxHistoryEntries = 1000000);

