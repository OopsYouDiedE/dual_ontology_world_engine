using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Core;

public readonly record struct Tag(string Name);
public readonly record struct RelationTag(string Name);

public static class Tags
{
    public static readonly Tag Locked = new(nameof(Locked));
    public static readonly Tag Confidence = new(nameof(Confidence));
    public static readonly Tag Cooperation = new(nameof(Cooperation));
    public static readonly Tag Trust = new(nameof(Trust));
    public static readonly Tag Success = new(nameof(Success));
    public static readonly Tag Responded = new(nameof(Responded));
}

public static class Relations
{
    public static readonly RelationTag Controls = new(nameof(Controls));
    public static readonly RelationTag HasIdentity = new(nameof(HasIdentity));
    public static readonly RelationTag AcceptsKey = new(nameof(AcceptsKey));
    public static readonly RelationTag IsA = new(nameof(IsA));
    public static readonly RelationTag Subject = new(nameof(Subject));
    public static readonly RelationTag Object = new(nameof(Object));
}

public enum ProjectionKind { Object, Space, Boundary, Body, Soul, Concept, Procedure, Event, Area, Material }
public enum Truth { Unknown, False, True }
public enum ObservationSource { Perception, Testimony, Inference, Introspection, Corruption, Recall }

/// <summary>客观对象的身份。可变状态和功能均由 Property 持有。</summary>
public class Essence
{
    public Essence(string trueName, ProjectionKind kind = ProjectionKind.Object) : this(new EntityId(Contract.Opaque()), trueName, kind) { }
    public Essence(EntityId id, string trueName, ProjectionKind kind = ProjectionKind.Object)
    {
        Contract.Identity(id.Value, "entity.id"); Contract.Text(trueName, "entity.name"); Contract.Enum(kind, "entity.kind");
        (Id, TrueName, Kind) = (id, trueName, kind);
    }
    public EntityId Id { get; }
    private readonly Dictionary<Type, Property> _properties = [];
    public string TrueName { get; }
    public ProjectionKind Kind { get; }
    public HashSet<(RelationTag Relation, Essence Target)> Relations { get; } = [];
    public IEnumerable<Property> Properties => _properties.Values;
    public T? Get<T>() where T : Property => _properties.GetValueOrDefault(typeof(T)) as T;
    public T Add<T>(T property) where T : Property
    {
        if (property.Source is not null && !ReferenceEquals(property.Source, this))
            throw new InvalidOperationException("Property 已属于另一个对象。");
        property.Source = this;
        _properties[typeof(T)] = property;
        return property;
    }
}

public sealed class SoulEssence : Essence
{
    public SoulEssence(string name) : base(name, ProjectionKind.Soul) { }
    public SoulEssence(EntityId id, string name) : base(id, name, ProjectionKind.Soul) { }
    public ProjectionWorld World { get; } = new();
}

public abstract class Property
{
    public Essence? Source { get; internal set; }
}

/// <summary>开放的数值状态。稳定机制使用自己的 Property 字段。</summary>
public sealed class StateProperty : Property
{
    public Dictionary<Tag, int> Values { get; } = [];
    public HashSet<Tag> Observable { get; } = [];
}

public sealed class SpatialProperty : Property
{
    public Essence? Space { get; set; }
    public Essence? Holder { get; set; }
}

public sealed class PassageProperty : Property
{
    public Dictionary<Essence, Essence> Connections { get; } = [];
}

public sealed class LockProperty : Property
{
    public bool IsLocked { get; set; } = true;
    public bool Accepts(Essence key) => Source is { } source && source.Relations.Any(accepted =>
        accepted.Relation == Relations.AcceptsKey && key.Relations.Contains((Relations.HasIdentity, accepted.Target)));
}

/// <summary>同一对象各个现实表现的非本质外观。用表现对象引用区分侧面。</summary>
public sealed class SurfaceProperty : Property
{
    public Dictionary<Projection, ImmutableDictionary<Tag, int>> Surfaces { get; } = [];
    public Dictionary<Projection, ImmutableHashSet<(RelationTag Relation, Projection Target)>> Relations { get; } = [];
}

public sealed class ProcedureProperty(Effect effect) : Property
{
    public Effect Effect { get; set; } = effect;
}

public sealed class RuleProperty : Property
{
    public ImmutableArray<Rule> Rules { get; set; } = [];
}

public sealed class CorruptionProperty(IProjectionTransform transform) : Property
{
    public void Apply(SoulEssence soul, double time = 0) => Cognition.Corrupt(soul, transform, time);
}

/// <summary>投影没有指向 Essence 的公开通道。认知变化保留对象引用。</summary>
public sealed class Projection
{
    internal Projection(ProjectionWorld world, string name, ProjectionKind kind, SubjectProjectionId? id = null)
    {
        (World, Name, Kind) = (world, name, kind);
        SubjectId = world.SubjectWorldId is null ? null : id ?? world.AllocateId();
    }
    public SubjectProjectionId? SubjectId { get; }
    public ImmutableArray<KnownAuthorTermV1> KnownTerms { get; internal set; } = [];

    internal ProjectionWorld World { get; }
    public string Name { get; internal set; }
    public ProjectionKind Kind { get; internal set; }
    public Truth Existence { get; internal set; }
    public Projection? Parent { get; internal set; }
    public IEnumerable<Projection> Children => World.Projections.Where(p => ReferenceEquals(p.Parent, this));
    public ImmutableDictionary<Tag, int> Tags { get; internal set; } = ImmutableDictionary<Tag, int>.Empty;
    public ImmutableHashSet<(RelationTag Relation, Projection Target)> Relations { get; internal set; } = [];
    public ImmutableHashSet<(RelationTag Relation, Projection Target)> DeniedRelations { get; internal set; } = [];
    public ImmutableArray<Rule> Rules { get; internal set; } = [];
    public Effect? Procedure { get; internal set; }
    public ObservationEvent? LastObservation { get; internal set; }
    public override string ToString() => Name;
}

/// <summary>一次认知输入。null 表示没有新信息；标签值 null 表示明确撤销。</summary>
public sealed record ObservationInput(string Name, ProjectionKind Kind = ProjectionKind.Object)
{
    public Projection? Target { get; init; }
    public ObservationSource Source { get; init; } = ObservationSource.Perception;
    public Truth? Existence { get; init; }
    public bool? Detected { get; init; }
    public double Time { get; init; }
    public bool Move { get; init; }
    public Projection? Parent { get; init; }
    public ImmutableDictionary<Tag, int?> Tags { get; init; } = ImmutableDictionary<Tag, int?>.Empty;
    public ImmutableHashSet<(RelationTag Relation, Projection Target)> AddRelations { get; init; } = [];
    public ImmutableHashSet<(RelationTag Relation, Projection Target)> RemoveRelations { get; init; } = [];
    public ImmutableHashSet<(RelationTag Relation, Projection Target)> DenyRelations { get; init; } = [];
    public ImmutableArray<Rule>? Rules { get; init; }
    public Effect? Procedure { get; init; }
    public bool ClearProcedure { get; init; }
    public bool Forget { get; init; }
    public string Details { get; init; } = "";
    public ImmutableArray<KnownAuthorTermV1>? KnownTerms { get; init; }
    public SourceLocationV1? Origin { get; init; }
    public ObservationTimeUnitV1 TimeUnit { get; init; } = ObservationTimeUnitV1.Unspecified;
    public ImmutableArray<SubjectProjectionId> SourceDependencies { get; init; } = [];
    public TypedEventV1<Projection?>? Event { get; init; }
}

public sealed record ObservationEvent(Projection Target, ObservationSource Source, double Time,
    bool? Detected, Truth? Existence, string Details,
    ImmutableDictionary<Tag, int> Tags, ImmutableHashSet<(RelationTag Relation, Projection Target)> Relations,
    TypedEventV1<Projection?>? Event = null);
