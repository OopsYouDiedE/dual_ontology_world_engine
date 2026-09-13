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
public class Essence(string trueName, ProjectionKind kind = ProjectionKind.Object)
{
    private readonly Dictionary<Type, Property> _properties = [];
    public string TrueName { get; } = trueName;
    public ProjectionKind Kind { get; } = kind;
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

public sealed class SoulEssence(string name) : Essence(name, ProjectionKind.Soul)
{
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
    internal Projection(ProjectionWorld world, string name, ProjectionKind kind)
        => (World, Name, Kind) = (world, name, kind);

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

/// <summary>世界仅提供查询；主观世界的增删改通过 Observation.Observe 提交。</summary>
public sealed class ProjectionWorld
{
    internal ProjectionWorld() { }
    internal event Action? Changed;
    internal void Notify() => Changed?.Invoke();
    public ImmutableArray<Projection> Projections { get; internal set; } = [];
    public ImmutableList<ObservationEvent> History { get; internal set; } = [];
    public bool Contains(Projection projection) => ReferenceEquals(projection.World, this) && Projections.Contains(projection);
    public IEnumerable<Projection> Find(Tag tag, int minimum = 1) => Projections.Where(p => p.Tags.TryGetValue(tag, out var value) && value >= minimum);
    public IEnumerable<Projection> Find(RelationTag relation, Projection target) => Projections.Where(p => p.Relations.Contains((relation, target)));
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
}

public sealed record ObservationEvent(Projection Target, ObservationSource Source, double Time,
    bool? Detected, Truth? Existence, string Details,
    ImmutableDictionary<Tag, int> Tags, ImmutableHashSet<(RelationTag Relation, Projection Target)> Relations);

public static class Observation
{
    /// <summary>先验证世界边界再提交，关系和 AST 的嵌入引用不能跨世界。</summary>
    public static Projection Observe(SoulEssence soul, ObservationInput input) => Apply(soul.World, input);

    internal static Projection Apply(ProjectionWorld world, ObservationInput input)
    {
        Validate(world, input, []);
        var result = Commit(world, input);
        world.Notify();
        return result;
    }

    internal static void ApplyBatch(ProjectionWorld world, IReadOnlyList<ObservationInput> inputs)
    {
        var incoming = inputs.Select(i => i.Target!).ToHashSet();
        foreach (var input in inputs) Validate(world, input, incoming);
        var parents = inputs.Where(i => i.Move).ToDictionary(i => i.Target!, i => i.Parent);
        foreach (var input in inputs)
        {
            var visited = new HashSet<Projection>();
            for (var node = input.Target; node is not null; node = parents.TryGetValue(node, out var parent) ? parent : node.Parent)
                if (!visited.Add(node)) throw new InvalidOperationException("观察不能形成循环空间关系。");
        }
        foreach (var input in inputs.OrderBy(i => i.Forget)) Commit(world, input);
        if (inputs.Count > 0) world.Notify();
    }

    private static void Validate(ProjectionWorld world, ObservationInput input, HashSet<Projection> incoming)
    {
        void Check(Projection? p)
        {
            if (p is not null && !(world.Contains(p) || ReferenceEquals(p.World, world) && incoming.Contains(p)))
                throw new InvalidOperationException("观察引用必须属于接收者内部世界。");
        }
        Check(input.Target);
        if (input.Forget && input.Target is null) throw new InvalidOperationException("遗忘必须指定已有投影。");
        Check(input.Parent);
        foreach (var (_, target) in input.AddRelations.Concat(input.RemoveRelations).Concat(input.DenyRelations)) Check(target);
        foreach (var reference in (input.Rules ?? []).SelectMany(Ast.References).Concat(Ast.References(input.Procedure))) Check(reference);
        for (var ancestor = input.Move ? input.Parent : null; ancestor is not null; ancestor = ancestor.Parent)
            if (ReferenceEquals(ancestor, input.Target)) throw new InvalidOperationException("投影不能成为自己的后代。");
    }

    private static Projection Commit(ProjectionWorld world, ObservationInput input)
    {
        var targetProjection = input.Target ?? new Projection(world, input.Name, input.Kind);
        if (input.Forget)
        {
            world.Projections = world.Projections.Remove(targetProjection);
            foreach (var p in world.Projections)
            {
                if (ReferenceEquals(p.Parent, targetProjection)) p.Parent = null;
                p.Relations = p.Relations.Except(p.Relations.Where(r => ReferenceEquals(r.Target, targetProjection)));
                p.DeniedRelations = p.DeniedRelations.Except(p.DeniedRelations.Where(r => ReferenceEquals(r.Target, targetProjection)));
                p.Rules = [..p.Rules.Where(r => !Ast.References(r).Contains(targetProjection))];
                if (Ast.References(p.Procedure).Contains(targetProjection)) p.Procedure = null;
            }
            world.History = world.History.RemoveAll(h => ReferenceEquals(h.Target, targetProjection) || h.Relations.Any(r => ReferenceEquals(r.Target, targetProjection)));
            return targetProjection;
        }
        if (!world.Contains(targetProjection)) world.Projections = world.Projections.Add(targetProjection);
        targetProjection.Name = input.Name;
        targetProjection.Kind = input.Kind;
        if (input.Existence is { } existence) targetProjection.Existence = existence;
        if (input.Move) targetProjection.Parent = input.Parent;
        foreach (var (tag, value) in input.Tags)
            targetProjection.Tags = value is { } number ? targetProjection.Tags.SetItem(tag, number) : targetProjection.Tags.Remove(tag);
        targetProjection.Relations = targetProjection.Relations.Except(input.RemoveRelations).Except(input.DenyRelations).Union(input.AddRelations);
        targetProjection.DeniedRelations = targetProjection.DeniedRelations.Except(input.RemoveRelations).Except(input.AddRelations).Union(input.DenyRelations.Except(input.AddRelations));
        if (input.Rules is { } rules) targetProjection.Rules = rules;
        if (input.ClearProcedure || input.Procedure is not null) targetProjection.Procedure = input.Procedure;
        var observed = new ObservationEvent(targetProjection, input.Source, input.Time, input.Detected, input.Existence, input.Details, targetProjection.Tags, targetProjection.Relations);
        targetProjection.LastObservation = observed;
        world.History = world.History.Add(observed);
        return targetProjection;
    }
}
