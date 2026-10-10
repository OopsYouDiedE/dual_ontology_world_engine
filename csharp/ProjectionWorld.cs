using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Core;

/// <summary>Legacy getters are live references. CaptureSnapshot is the atomic graph read boundary.</summary>
public sealed class ProjectionWorld
{
    internal object Gate { get; } = new();
    internal bool Detached { get; init; }
    internal ProjectionWorld() : this(Guid.NewGuid()) { }
    internal ProjectionWorld(Guid? subjectWorld)
    {
        SubjectWorldId = subjectWorld;
        Snapshot = new(1, subjectWorld ?? Guid.Empty, new(0), new(0), [], [], []);
        InitialSnapshot = Snapshot;
    }
    public Guid? SubjectWorldId { get; }
    public KnowledgeRevision Revision { get { lock (Gate) return Snapshot.Revision; } }
    public VocabularyRevision VocabularyRevision { get { lock (Gate) return Snapshot.VocabularyRevision; } }
    public ImmutableArray<Projection> Projections { get; internal set; } = [];
    public ImmutableList<ObservationEvent> History { get; internal set; } = [];
    internal CognitionSnapshotV1 Snapshot { get; set; }
    internal DetachedProjectionGraphV1? ReaderGraph { get; set; }
    internal CognitionSnapshotV1 InitialSnapshot { get; set; }
    internal ImmutableArray<CognitionAuditCommitV1> Audit { get; set; } = [];
    internal HashSet<SubjectProjectionId> Allocated { get; } = [];
    internal HashSet<SubjectProjectionId> Used { get; } = [];
    internal event Action? Changed;
    public event Action<CognitionChangedV1>? CognitionChanged;
    private readonly Queue<(CognitionChangedV1 Notice, Delegate[] Typed, Delegate[] Legacy)> notifications = [];
    private readonly Dictionary<long, CognitionNotificationReceiptV1> receipts = [];
    private bool draining;

    internal SubjectProjectionId AllocateId()
    {
        lock (Gate)
        {
            Contract.Require(SubjectWorldId is not null, ContractErrorCode.Unsupported, "world", "现实世界不分配主体句柄。");
            SubjectProjectionId id;
            do { id = new(SubjectWorldId!.Value, Guid.NewGuid()); } while (!Allocated.Add(id));
            return id;
        }
    }
    public ContractResult<SubjectProjectionId> AllocateSubjectProjectionId() => Contract.Try(AllocateId);
    public ContractResult<CognitionSnapshotV1> CaptureSnapshot() => Contract.Try(() =>
    {
        lock (Gate)
        {
            Contract.Require(SubjectWorldId is not null, ContractErrorCode.Unsupported, "world", "现实世界没有主体快照。");
            return Snapshot;
        }
    });
    public bool Contains(Projection projection)
    {
        lock (Gate) return projection is not null && ReferenceEquals(projection.World, this) && Projections.Contains(projection);
    }
    public IEnumerable<Projection> Find(Tag tag, int minimum = 1)
    {
        lock (Gate) return Projections.Where(p => p.Tags.TryGetValue(tag, out var value) && value >= minimum).ToArray();
    }
    public IEnumerable<Projection> Find(RelationTag relation, Projection target)
    {
        lock (Gate) return Projections.Where(p => p.Relations.Contains((relation, target))).ToArray();
    }
    internal void QueueNotification()
    {
        var notice = new CognitionChangedV1(Snapshot.Revision, Snapshot.VocabularyRevision);
        receipts[notice.PublishedRevision.Value] = new(notice.PublishedRevision, NotificationDeliveryV1.Pending, []);
        notifications.Enqueue((notice, CognitionChanged?.GetInvocationList() ?? [], Changed?.GetInvocationList() ?? []));
    }
    // A reentrant commit queues its notification and returns Pending; the current drainer completes it later.
    internal void DrainNotifications()
    {
        lock (Gate) { if (draining) return; draining = true; }
        while (true)
        {
            (CognitionChangedV1 Notice, Delegate[] Typed, Delegate[] Legacy) item;
            lock (Gate)
            {
                if (notifications.Count == 0) { draining = false; return; }
                item = notifications.Dequeue();
            }
            var faults = ImmutableArray.CreateBuilder<ContractError>();
            void Deliver(Delegate subscriber, bool typed)
            {
                try
                {
                    if (typed) ((Action<CognitionChangedV1>)subscriber)(item.Notice);
                    else ((Action)subscriber)();
                }
                catch (Exception e)
                {
                    // Exception text may contain external content; expose mechanism diagnostics only.
                    faults.Add(new(ContractErrorCode.NotificationFault, "notification", "认知订阅者抛出异常：" + e.GetType().Name, CandidateIds: []));
                }
            }
            foreach (var subscriber in item.Typed) Deliver(subscriber, true);
            foreach (var subscriber in item.Legacy) Deliver(subscriber, false);
            lock (Gate) receipts[item.Notice.PublishedRevision.Value] = new(item.Notice.PublishedRevision, NotificationDeliveryV1.Completed, faults.ToImmutable());
        }
    }
    public ContractResult<CognitionNotificationReceiptV1> ReadNotificationReceipt(KnowledgeRevision revision) => Contract.Try(() =>
    {
        lock (Gate)
        {
            Contract.Require(revision.Value >= 0 && revision.Value <= Snapshot.Revision.Value, ContractErrorCode.InvalidRevision, "revision", "通知版本不在可用范围。");
            if (revision.Value == 0) return new CognitionNotificationReceiptV1(revision, NotificationDeliveryV1.Completed, []);
            Contract.Require(receipts.TryGetValue(revision.Value, out var receipt), ContractErrorCode.InvalidRevision, "revision", "该版本没有可用通知回执。");
            return receipt!;
        }
    });
}
