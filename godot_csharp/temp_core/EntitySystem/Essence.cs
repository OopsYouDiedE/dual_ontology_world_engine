
using System.Security.Cryptography;


/// <summary>
/// Essence 用于为场景中的 Projection 提供本质依据。
/// Projection 表示某个 Essence 在场景中的具体显现；
/// 对 Projection 的观察结果可以进一步受到观察者状态影响。
/// </summary>
namespace DualOntologyWorldEngine.TempCore;

/// <summary>
/// 承载一个对象本质事实的基础类。
///
/// Core 中只存在有限种具有独立运行机制的 Essence 类型。
/// 具体对象之间的差异主要由 Tags、Properties 和 Relations 表达，
/// 而不是为每一种具体对象建立新的 Essence 子类。
///
/// 当前 Essence 类型包括：
/// - SpaceEssence：空间；
/// - BoundaryEssence：改变空间之间通行关系的边界；
/// - PhysicalEssence：普通成形物理实体；
/// - BodyEssence：能够承担观察与物理操作的身体；
/// - SoulEssence：主体的精神、认知与主观状态载体；
/// - MaterialEssence：不要求具有固定形状的材料或物质本身；
/// - ConceptEssence：概念、身份、名称等语义存在；
/// - EventEssence：能够被指称、记录和引用的事件；
/// - ProcedureEssence：描述行为执行方法或流程的知识；
/// - AreaEssence：在一定范围内持续产生作用的场。
/// </summary>
public class Essence
{
    /// <summary>
    /// Essence 的真实名称。
    /// 表示对象在本体层面的名称，而不是观察者看到的名称。
    /// </summary>
    public string TrueName;

    /// <summary>
    /// 当前 Essence 与其他 Essence 之间的语义关系。
    ///
    /// string 表示关系类型，
    /// Essence 表示关系所指向的对象。
    /// </summary>
    public HashSet<(string, Essence)> Relations = new();

    /// <summary>
    /// 当前 Essence 挂载的 Property。
    ///
    /// 同一种 Property 在一个 Essence 上原则上只存在一个实例。
    /// Property 负责关联效果来源 Source 与实际作用目标 Control。
    /// </summary>
    public HashSet<Property> Properties = new();

    /// <summary>
    /// Essence 自身携带的数值标签。
    ///
    /// Tags 用于表示能够直接由该 Essence 提供的数值事实，
    /// 例如硬度、强度、质量、腐化强度、亮度等。
    /// </summary>
    public Dictionary<Tag, double> Tags = new();
}

/// <summary>
/// 表示一个可以容纳场景内容的空间。
///
/// SpaceEssence 是空间拓扑中的基本节点，
/// 可以作为 BoundaryEssence 通行关系的起点或终点。
/// </summary>
public class SpaceEssence : Essence
{
}

/// <summary>
/// 表示能够改变多个空间之间通行关系的边界。
///
/// BoundaryEssence 本身不限定具体外观。
/// 门、洞口、栅栏、墙、空间裂缝等都可以通过不同的
/// Property、Tags 与 Projection 表现出来。
/// </summary>
public class BoundaryEssence : Essence
{
    /// <summary>
    /// 当前有效的空间通行映射。
    ///
    /// 从一个 SpaceEssence 进入时，
    /// 唯一对应另一个 SpaceEssence 作为出口。
    /// </summary>
    public Dictionary<SpaceEssence, SpaceEssence> Connections = [];

    /// <summary>
    /// Boundary 在不同表面或方向上表现出的数值标签。
    ///
    /// 可用于描述边界不同侧面的外观、性质或局部表现。
    /// </summary>
    public List<Dictionary<Tag, double>> SurfaceTags = [];
    public List<SpaceEssence> Surfaces = [];
    public void Travell(SpaceEssence from, Essence traveller)
    {

    }

}

/// <summary>
/// 表示一个已经形成具体物理结构和形态的普通物理实体。
///
/// PhysicalEssence 与 MaterialEssence 的区别在于：
/// PhysicalEssence 表示一个具体成形的物体；
/// MaterialEssence 表示构成物体的材料或物质本身。
/// </summary>
public class PhysicalEssence : Essence
{
}

/// <summary>
/// 表示能够作为主体与物理世界之间交互接口的身体。
///
/// BodyEssence 具有两项核心意义：
/// 1. 承担对外界的观察；
/// 2. 承担主体对物理世界的实际操作。
/// </summary>
public class BodyEssence : Essence
{
}

/// <summary>
/// 表示主体的精神与主观存在。
///
/// SoulEssence 可以承载认知、记忆、欲望、身份认同、
/// 情绪以及其他与主体内部状态有关的内容。
/// </summary>
public class SoulEssence : Essence
{
}

/// <summary>
/// 表示不要求具有固定形状即可提供自身性质的材料或物质。
///
/// MaterialEssence 可以作为其他 Essence 的 Property 来源，
/// 为具体物体提供硬度、密度、燃烧性、导电性等性质。
/// </summary>
public class MaterialEssence : Essence
{
}

/// <summary>
/// 表示没有必要以普通物理实体形式存在的语义概念。
///
/// 例如身份、名称、职业、死亡、所有权等。
/// ConceptEssence 可以参与语义关系、规则和认知计算。
/// </summary>
public class ConceptEssence : Essence
{
}

/// <summary>
/// 表示“如何完成某项行为”的流程或路径知识。
///
/// ProcedureEssence 描述的是行为方法本身，
/// 而不是某次正在执行的 Action。
///
/// 例如：
/// 如何开锁、如何进行仪式、如何治疗伤口等。
/// </summary>
public class ProcedureEssence : Essence
{
}

/// <summary>
/// 表示一个能够被世界继续引用的事件。
///
/// EventEssence 可以被记录、观察、记忆、关联，
/// 并作为规则条件或其他语义过程的对象。
/// </summary>
public class EventEssence : Essence
{
}

/// <summary>
/// 表示在一定范围内持续存在并对范围内对象施加影响的“场”。
///
/// AreaEssence 本身不是空间容器。
/// 它通常依附于某个空间、位置或其他 Essence，
/// 并根据其覆盖范围对其中的目标产生持续影响。
///
/// 例如：
/// 电场、磁场、辐射场、温度场、腐化场、
/// 神圣领域、恐惧场、规则影响场等。
/// </summary>
public class AreaEssence : Essence
{
}
