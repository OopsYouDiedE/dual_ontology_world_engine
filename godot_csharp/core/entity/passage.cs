using System;
using System.Collections.Generic;
using System.Linq;

namespace DualOntologyWorldEngine.Core;

/// <summary>
/// 表示两个场景实体之间的拓扑通道连接。
/// </summary>
/// <remarks>
/// Passage 实现世界树接口（继承 <see cref="TreeMember"/>），进入世界树以统一管理生命周期与空间归属。
/// 
/// 但 Passage 并非物理可拾取、消耗或充当仪式原料的世界实体（Entity），因此不继承 Entity。
/// 门（Door）作为物理或超自然存在，可以作为 Passage 的子实体（Child Entity）挂载于其下。
/// 
/// Passage 负责维护：
/// 1. 左端与右端场景实体的双向连接引用。
/// 2. 从各自端点出发跨越通道的行动值代价（Cost）。
/// 3. 通行许可判断（包括门禁状态和自定义条件）。
/// </remarks>
public class Passage : TreeMember
{
    /// <summary>文学模拟使用的可推演通行条件；与普通行动共用状态表达式。</summary>
    public IReadOnlyList<DualOntologyWorldEngine.Core.Rules.Expr> Requirements { get; set; } = [];
    /// <summary>
    /// 通道声明时的左端场景实体。
    /// </summary>
    public Entity Left { get; }

    /// <summary>
    /// 通道声明时的右端场景实体。
    /// </summary>
    public Entity Right { get; }

    /// <summary>
    /// 从各端点出发跨越通道所需的行动值代价映射。
    /// </summary>
    private readonly Dictionary<Entity, double> _costs = new();

    /// <summary>
    /// 通行条件的委托定义。
    /// 参数依次为：来源场景、通行行动主体、环境上下文。
    /// </summary>
    public Func<Entity, Entity?, object?, bool>? Condition { get; set; }

    /// <summary>
    /// 面向不同端点观察时的方向性名称映射。
    /// </summary>
    private readonly Dictionary<Entity, string> _directionalDesignations = new();

    /// <summary>
    /// 创建两个场景之间的拓扑连接通道。
    /// </summary>
    /// <param name="name">通道默认调试名称。</param>
    /// <param name="left">左端场景实体。</param>
    /// <param name="right">右端场景实体。</param>
    /// <param name="cost">从左端进入的行动值代价，默认为 1。</param>
    /// <param name="reverseCost">从右端进入的行动值代价。若为 null 则与 cost 相同。</param>
    /// <param name="parent">所属父世界树成员（通常为区域或世界根节点）。</param>
    /// <param name="position">在父节点中的槽位。</param>
    /// <param name="condition">自定义通行检查委托。</param>
    public Passage(
        string name,
        Entity left,
        Entity right,
        double cost = 1.0,
        double? reverseCost = null,
        TreeMember? parent = null,
        int position = 0,
        Func<Entity, Entity?, object?, bool>? condition = null)
        : base(name, parent, position)
    {
        Left = left;
        Right = right;
        _costs[left] = cost;
        _costs[right] = reverseCost ?? cost;
        Condition = condition;
    }

    /// <summary>
    /// 获取通道的端点对。
    /// </summary>
    public (Entity Left, Entity Right) Endpoints => (Left, Right);

    /// <summary>
    /// 给定通道某一端场景，获取其对端场景。
    /// </summary>
    /// <param name="scene">当前场景实体。</param>
    /// <returns>对端场景实体。若传入的场景不是本通道的合法端点，则返回 null。</returns>
    public Entity? Other(Entity scene)
    {
        if (ReferenceEquals(scene, Left)) return Right;
        if (ReferenceEquals(scene, Right)) return Left;
        return null;
    }

    /// <summary>
    /// 设置从特定场景端点观察该通道时的名称。
    /// </summary>
    /// <remarks>
    /// 同一通道在不同端点往往呈现不同的视觉标识或标牌（例如从大厅看是“通往庭院之门”，从庭院看则是“进入主楼玄关”）。
    /// </remarks>
    /// <param name="scene">观察者所处的出发端场景实体。</param>
    /// <param name="designation">该方向上的通道指称名称。</param>
    public void SetDesignation(Entity scene, string designation) =>
        _directionalDesignations[scene] = designation;

    /// <summary>
    /// 获取从特定场景观察该通道时的方向性名称。
    /// </summary>
    /// <param name="scene">观察者所处的场景实体。</param>
    /// <returns>返回对应场景专属的通道指称；若未配置方向性名称，则返回基类默认的 <see cref="TreeMember.Name"/>。</returns>
    public string GetDesignationFrom(Entity scene) =>
        _directionalDesignations.TryGetValue(scene, out var designation) ? designation : Name;

    /// <summary>
    /// 获取从指定端点出发穿过通道的行动值代价。
    /// </summary>
    /// <remarks>
    /// 通道支持单向阻力或非对称通行成本（例如上坡与下坡、逆风与顺风）。
    /// 若指定场景并非本通道已登记的合法端点，则返回正无穷大（<see cref="float.PositiveInfinity"/>）。
    /// </remarks>
    /// <param name="scene">出发端场景实体。</param>
    /// <returns>穿越通道所需的行动值（Cost）。非合法端点时返回正无穷大。</returns>
    public double CostFrom(Entity scene) =>
        _costs.TryGetValue(scene, out var cost) ? cost : double.PositiveInfinity;

    /// <summary>
    /// 检查指定行动主体是否允许从特定场景穿越此通道。
    /// </summary>
    /// <param name="scene">进入端场景实体。</param>
    /// <param name="actor">行动主体实体。</param>
    /// <param name="env">环境上下文。</param>
    /// <returns>true 表示允许通行，false 表示被门禁或规则阻挡。</returns>
    public bool Allows(Entity scene, Entity? actor = null, object? env = null)
    {
        // 必须为合法端点
        if (Other(scene) == null)
        {
            return false;
        }

        // 检查作为子节点挂载的门（Door）。若存在门且门未处于可通行状态，则阻断
        var doors = Children.Keys.OfType<Entity>().Where(e => e.HasTag("Door"));
        foreach (var door in doors)
        {
            if (!door.IsAccessible(scene))
            {
                return false;
            }
        }

        // 检查自定义拓扑条件
        return Condition == null || Condition(scene, actor, env);
    }
}
