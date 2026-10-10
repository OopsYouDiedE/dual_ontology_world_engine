using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Core;

public readonly record struct EventId
{
    public EventId(string value) { Contract.Identity(value, "event.id"); Value = value; }
    public string Value { get; }
    public static EventId New() => new(Contract.Opaque());
}
public enum EventPhaseV1 { Submitted, Committed, Completed, Accepted, Delivered, Presented }
public enum EventOutcomeV1 { Pending, Succeeded, Failed, Cancelled }

/// <summary>A proposition, never a rendered sentence. References remain in their own authority domain.</summary>
public sealed record EventClaimV1<T>(AuthorTermId Predicate, T Subject, T Object, T Location,
    Tag? Tag = null, int? Value = null, Truth Truth = Truth.Unknown);

/// <summary>T is Essence? in the audit, Projection? at runtime, and SubjectProjectionId? in snapshots.</summary>
public sealed record EventFieldsV1<T>
{
    public int ContractVersion { get; init; } = 1;
    public EventId Id { get; init; } = EventId.New();
    public double Time { get; init; }
    public ObservationTimeUnitV1 TimeUnit { get; init; }
    public T Subject { get; init; } = default!;
    public T Object { get; init; } = default!;
    public T Location { get; init; } = default!;
    public T Source { get; init; } = default!;
    public ObservationSource KnowledgeSource { get; init; }
    public SourceLocationV1? Origin { get; init; }
    public EventPhaseV1 Phase { get; init; }
    public EventOutcomeV1 Outcome { get; init; }
    public ActionOp? Operation { get; init; }
    public AuthorTermId? Topic { get; init; }
    public ActionFailure Failure { get; init; }
    public ImmutableArray<T> Perceivers { get; init; } = [];
    public ImmutableArray<EventClaimV1<T>> Claims { get; init; } = [];
    public ImmutableArray<EventId> Causes { get; init; } = [];
}

public abstract record TypedEventV1<T>(EventFieldsV1<T> Fields);
public sealed record ActionEventV1<T>(EventFieldsV1<T> Data) : TypedEventV1<T>(Data);
public sealed record ObservationRecordV1<T>(EventFieldsV1<T> Data) : TypedEventV1<T>(Data);
public sealed record TestimonyEventV1<T>(EventFieldsV1<T> Data) : TypedEventV1<T>(Data);
public sealed record PropagationEventV1<T>(EventFieldsV1<T> Data) : TypedEventV1<T>(Data);
public sealed record PresentationEventV1<T>(EventFieldsV1<T> Data) : TypedEventV1<T>(Data);

public static class TypedEvents
{
    public static TypedEventV1<T> WithFields<T>(TypedEventV1<T> e, EventFieldsV1<T> data) => e switch {
        ActionEventV1<T> => new ActionEventV1<T>(data), ObservationRecordV1<T> => new ObservationRecordV1<T>(data),
        TestimonyEventV1<T> => new TestimonyEventV1<T>(data), PropagationEventV1<T> => new PropagationEventV1<T>(data),
        PresentationEventV1<T> => new PresentationEventV1<T>(data),
        _ => throw new CognitionContractException(new(ContractErrorCode.Unsupported, "event", "Unsupported event type.", CandidateIds: []))
    };
    public static TypedEventV1<U> Map<T, U>(TypedEventV1<T> e, Func<T, U> map)
    {
        var f = e.Fields;
        var data = new EventFieldsV1<U> { ContractVersion = f.ContractVersion, Id = f.Id,
            Time = f.Time, TimeUnit = f.TimeUnit, Subject = map(f.Subject), Object = map(f.Object),
            Location = map(f.Location), Source = map(f.Source), KnowledgeSource = f.KnowledgeSource,
            Origin = f.Origin, Phase = f.Phase, Outcome = f.Outcome, Operation = f.Operation, Topic = f.Topic, Failure = f.Failure,
            Perceivers = [..f.Perceivers.Select(map)], Causes = f.Causes,
            Claims = [..f.Claims.Select(c => new EventClaimV1<U>(c.Predicate, map(c.Subject), map(c.Object), map(c.Location), c.Tag, c.Value, c.Truth))] };
        return e switch {
            ActionEventV1<T> => new ActionEventV1<U>(data), ObservationRecordV1<T> => new ObservationRecordV1<U>(data),
            TestimonyEventV1<T> => new TestimonyEventV1<U>(data), PropagationEventV1<T> => new PropagationEventV1<U>(data),
            PresentationEventV1<T> => new PresentationEventV1<U>(data),
            _ => throw new CognitionContractException(new(ContractErrorCode.Unsupported, "event", "Unsupported event type.", CandidateIds: []))
        };
    }
    public static IEnumerable<T> References<T>(TypedEventV1<T> e)
        => new[] { e.Fields.Subject, e.Fields.Object, e.Fields.Location, e.Fields.Source }
            .Concat(e.Fields.Perceivers).Concat(e.Fields.Claims.SelectMany(c => new[] { c.Subject, c.Object, c.Location }))
            .Where(r => r is not null).Distinct();

    internal static void Validate<T>(TypedEventV1<T> e, Action<T> reference)
    {
        Contract.Require(e is not null, ContractErrorCode.MalformedData, "event", "Event is required.");
        var f = e!.Fields;
        Contract.Require(f is not null && f.ContractVersion == 1, ContractErrorCode.Unsupported, "event.version", "Unsupported event version.");
        Contract.Identity(f!.Id.Value, "event.id"); Contract.Number(f.Time, "event.time");
        Contract.Enum(f.TimeUnit, "event.time_unit"); Contract.Enum(f.Phase, "event.phase");
        Contract.Enum(f.Outcome, "event.outcome"); Contract.Enum(f.KnowledgeSource, "event.source");
        Contract.Enum(f.Failure, "event.failure"); if (f.Operation is { } op) Contract.Enum(op, "event.operation");
        if (f.Topic is { } topic) Contract.Identity(topic.Value, "event.topic");
        SnapshotGraph.Origin(f.Origin);
        Contract.Array(f.Perceivers, "event.perceivers"); Contract.Array(f.Claims, "event.claims"); Contract.Array(f.Causes, "event.causes");
        WithFields(e, f);
        foreach (var claim in f.Claims) {
            Contract.Require(claim is not null, ContractErrorCode.MalformedData, "event.claim", "Claim is required.");
            Contract.Identity(claim!.Predicate.Value, "event.predicate"); Contract.Enum(claim.Truth, "event.truth");
            if (claim.Tag is { } tag) Contract.Identity(tag.Name, "event.tag");
        }
        foreach (var cause in f.Causes) Contract.Identity(cause.Value, "event.cause");
        Contract.Require(!f.Causes.Contains(f.Id) && f.Causes.Distinct().Count() == f.Causes.Length,
            ContractErrorCode.InvalidReference, "event.causes", "Causal references must be distinct preceding events.");
        foreach (var r in References(e)) reference(r);
    }
    internal static bool Same<T>(TypedEventV1<T>? a, TypedEventV1<T>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null || a.GetType() != b.GetType()) return false;
        var x = a.Fields; var y = b.Fields;
        return (x with { Claims = [], Perceivers = [], Causes = [] }) == (y with { Claims = [], Perceivers = [], Causes = [] })
            && x.Claims.SequenceEqual(y.Claims) && x.Perceivers.SequenceEqual(y.Perceivers) && x.Causes.SequenceEqual(y.Causes);
    }
    public static ImmutableArray<TypedEventV1<SubjectProjectionId?>> Of(CognitionSnapshotV1 snapshot,
        ObservationSource? source = null)
        => [..snapshot.VisibleHistory.Where(o => source is null || o.Source == source).Select(o => o.Event).OfType<TypedEventV1<SubjectProjectionId?>>()];
}

public sealed partial class WorldRuntime
{
    private ImmutableList<TypedEventV1<Essence?>> _eventAudit = [];
    private readonly Dictionary<EventId, TypedEventV1<Essence?>> _eventIds = [];
    public IReadOnlyList<TypedEventV1<Essence?>> EventAudit => _eventAudit;
    internal bool HasEvent(EventId id) => _eventIds.ContainsKey(id);
    public void RecordEvent(TypedEventV1<Essence?> e)
        => RecordEvent(e, false);
    internal void RecordCommittedEvent(TypedEventV1<Essence?> e) => RecordEvent(e, true);
    private void RecordEvent(TypedEventV1<Essence?> e, bool allowRetired)
    {
        TypedEvents.Validate(e, r => Contract.Require(r is not null && _entities.GetValueOrDefault(r.Id) == r && (allowRetired || _existing.Contains(r)),
            ContractErrorCode.InvalidReference, "event.reference", "Event reference belongs to another world or is retired."));
        Contract.Require(!_eventIds.ContainsKey(e.Fields.Id), ContractErrorCode.DuplicateIdentity, "event.id", "Event ID cannot be reused.");
        Contract.Require(e.Fields.Causes.All(_eventIds.ContainsKey), ContractErrorCode.InvalidReference, "event.causes", "Unknown causal event.");
        _eventIds.Add(e.Fields.Id, e);
        _eventAudit = _eventAudit.Add(e);
    }
    /// <summary>Engine-only permission projection. Unknown entities/claims and hidden causes are omitted.</summary>
    public TypedEventV1<Projection?>? ProjectEvent(SoulEssence soul, TypedEventV1<Essence?> e)
    {
        if (!ReferenceEquals(_eventIds.GetValueOrDefault(e.Fields.Id), e) || !e.Fields.Perceivers.Any(p => p == soul || p is not null && Controls(soul, p))) return null;
        Projection? Known(Essence? r) => r is null ? null : KnownProjection(soul, r);
        var projected = TypedEvents.Map(e, Known);
        var retained = e.Fields.Claims.Where(c => new[] { c.Subject, c.Object, c.Location }.All(r => r is null || Known(r) is not null)).ToArray();
        var remaining = e.Fields.Causes.ToHashSet(); var knownCauses = new HashSet<EventId>();
        if (remaining.Count > 0) foreach (var observation in soul.World.History.Reverse()) {
            if (observation.Event is { } known && remaining.Remove(known.Fields.Id)) knownCauses.Add(known.Fields.Id);
            if (remaining.Count == 0) break;
        }
        var fields = projected.Fields with { Claims = [..retained.Select(c => new EventClaimV1<Projection?>(c.Predicate, Known(c.Subject), Known(c.Object), Known(c.Location), c.Tag, c.Value, c.Truth))],
            Perceivers = [], Causes = [..projected.Fields.Causes.Where(id => knownCauses.Contains(id))] };
        return TypedEvents.WithFields(projected, fields);
    }
    public Projection? PublishEvent(SoulEssence soul, TypedEventV1<Essence?> e, string text = "")
    {
        var projected = ProjectEvent(soul, e);
        return projected is null ? null : Observation.Observe(soul, new(text, ProjectionKind.Event) {
            Source = e.Fields.KnowledgeSource, Time = e.Fields.Time, TimeUnit = e.Fields.TimeUnit,
            Origin = e.Fields.Origin, Details = text, Existence = Truth.True, Event = projected });
    }
}
