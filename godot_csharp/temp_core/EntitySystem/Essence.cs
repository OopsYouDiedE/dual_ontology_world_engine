
namespace DualOntologyWorldEngine.TempCore;

/// <summary>
/// 常用标准语义关系名称常量（兼容别名）。
/// </summary>
public static class RelationNames
{
    public static readonly RelationTag Controls = StandardRelations.Controls;
    public static readonly RelationTag HasIdentity = StandardRelations.HasIdentity;
    public static readonly RelationTag AcceptsKey = StandardRelations.AcceptsKey;
    public static readonly RelationTag AttachedTo = StandardRelations.AttachedTo;
    public static readonly RelationTag IsA = StandardRelations.IsA;
}

/// <summary>
/// 承载对象客观本质事实的基类。
/// 真实状态唯一存在于 Essence 的 Tags、Relations 与 Properties 中。
/// </summary>
public class Essence
{
    public string TrueName { get; init; }

    public HashSet<(RelationTag Relation, Essence Target)> Relations { get; } = [];

    private readonly Dictionary<Type, Property> _properties = [];
    public IReadOnlyCollection<Property> Properties => _properties.Values;

    public void AddProperty(Property property)
    {
        property.Source = this;
        _properties[property.GetType()] = property;
    }

    public T? GetProperty<T>() where T : Property
        => _properties.TryGetValue(typeof(T), out var p) && p is T typed ? typed : null;

    public Dictionary<Tag, double> Tags { get; } = [];

    public Essence(string trueName = "")
    {
        TrueName = trueName;
    }
}

/// <summary>表示空间拓扑节点。</summary>
public class SpaceEssence(string trueName = "") : Essence(trueName);

/// <summary>表示改变空间通行关系的边界通道。</summary>
public class BoundaryEssence(string trueName = "") : Essence(trueName)
{
    public Dictionary<SpaceEssence, SpaceEssence> Connections { get; } = [];
    public Dictionary<SpaceEssence, Dictionary<Tag, double>> SurfaceTags { get; } = [];
    public IEnumerable<SpaceEssence> Surfaces => SurfaceTags.Keys;

    public bool Travell(SpaceEssence from, Essence traveller)
    {
        if (!Connections.TryGetValue(from, out var to)) return false;
        var spatial = traveller.GetProperty<SpatialProperty>();
        if (spatial != null)
        {
            spatial.Space = to;
            return true;
        }
        return false;
    }
}

/// <summary>成形物理实体。</summary>
public class PhysicalEssence(string trueName = "") : Essence(trueName);

/// <summary>承担观察与物理操作的身体实体。</summary>
public class BodyEssence(string trueName = "") : Essence(trueName);

/// <summary>主观精神与认知载体。维护自身状态与独立的 ProjectionWorld。</summary>
public class SoulEssence(string trueName = "") : Essence(trueName)
{
    public ProjectionWorld World { get; } = new();
}

/// <summary>材料或物质本身。</summary>
public class MaterialEssence(string trueName = "") : Essence(trueName);

/// <summary>概念与语义存在。</summary>
public class ConceptEssence(string trueName = "") : Essence(trueName);

/// <summary>行为方法或流程知识。</summary>
public class ProcedureEssence(string trueName = "") : Essence(trueName);

/// <summary>运行时因果事件实体。</summary>
public class EventEssence(string trueName = "") : Essence(trueName)
{
    public bool Success { get; init; }
    public Essence? Subject { get; init; }
    public Essence? Object { get; init; }
    public string Details { get; init; } = "";
}

/// <summary>范围场。</summary>
public class AreaEssence(string trueName = "") : Essence(trueName);


