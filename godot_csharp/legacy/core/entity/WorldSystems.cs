
namespace DualOntologyWorldEngine.Core;

#region 门禁、锁与容器系统

/// <summary>锁事实记录。</summary>
/// <param name="Strength">锁的强度。</param>
/// <param name="Side">受阻侧场景。为 null 表示双向/全向封闭。</param>
/// <param name="Source">施加锁的对象或机制来源。</param>
public record LockRecord(int Strength, Entity? Side = null, object? Source = null);

/// <summary>空间可访问性、容器收纳与门禁系统。</summary>
/// <remarks>
/// 负责处理：
/// 1. 物理或超自然锁的施加、解除与阻断判定。
/// 2. 开合物（Openable）的状态流转与通达性（Accessible）判定。
/// 3. 容器（Container）对内部子实体的置入（Put）与取出（Take）安全检查。
/// 
/// 遵循规则：只有可访问的容器才能进行物体转移，锁定状态由特定方向（Side）锁与全局锁共同决定。
/// </remarks>
public static class AccessControlSystem
{
    /// <summary>检查实体在指定场景方向上是否处于锁定状态。</summary>
    /// <param name="entity">待检查的目标实体（如门或箱子）。</param>
    /// <param name="fromScene">主体所处的场景实体。为 null 时仅检查全局锁。</param>
    /// <returns>true 表示存在生效的阻断锁，false 表示未上锁。</returns>
    public static bool IsLocked(this Entity entity, Entity? fromScene = null)
    {
        var locks = entity.Locks;
        if (locks.Count == 0) return false;
        return locks.Any(l => l.Strength > 0 && (l.Side == null || ReferenceEquals(l.Side, fromScene)));
    }

    /// <summary>检查实体是否在当前方向允许访问或穿越。</summary>
    /// <remarks>
    /// 若实体不具备 Openable 标记，默认始终可访问；
    /// 若具备 Openable 标记，则必须已开启（IsOpen == true）且未被锁阻断。
    /// </remarks>
    public static bool IsAccessible(this Entity entity, Entity? fromScene = null)
    {
        return (!entity.IsOpenable() || entity.IsOpen) && !entity.IsLocked(fromScene);
    }

    /// <summary>在当前方向没有有效锁阻断时开启对象。</summary>
    /// <returns>true 表示成功开启，false 表示因上锁阻断失败。</returns>
    public static bool Open(this Entity entity, Entity? fromScene = null)
    {
        if (entity.IsLocked(fromScene))
        {
            return false;
        }

        entity.IsOpen = true;
        return true;
    }

    /// <summary>关闭可开合对象。</summary>
    public static void Close(this Entity entity)
    {
        entity.IsOpen = false;
    }

    /// <summary>为实体施加一把锁并自动闭合对象。</summary>
    /// <param name="entity">目标实体。</param>
    /// <param name="strength">锁强度。</param>
    /// <param name="side">特定阻断场景方向。为 null 表示全向锁定。</param>
    /// <param name="source">加锁来源。</param>
    /// <returns>新生成的锁记录。</returns>
    public static LockRecord Lock(this Entity entity, int strength = 1, Entity? side = null, object? source = null)
    {
        entity.Close();
        var record = new LockRecord(strength, side, source);
        entity.Locks.Add(record);
        return record;
    }

    /// <summary>移除匹配的锁记录。</summary>
    /// <param name="entity">目标实体。</param>
    /// <param name="side">匹配的方向。为 null 表示匹配全局锁。</param>
    /// <param name="source">匹配的来源。为 null 则忽略来源过滤。</param>
    /// <returns>成功移除的锁数量。</returns>
    public static int Unlock(this Entity entity, Entity? side = null, object? source = null)
    {
        return entity.Locks.RemoveAll(l =>
            (side == null || l.Side == null || ReferenceEquals(l.Side, side)) &&
            (source == null || Equals(l.Source, source)));
    }

    /// <summary>使用钥匙实体解除门禁锁记录（支持 Source 匹配或 Unlocks 语义切面）。</summary>
    /// <param name="entity">目标门禁实体。</param>
    /// <param name="key">钥匙或凭证实体。</param>
    /// <param name="side">特定方向。</param>
    /// <returns>成功解除的锁记录数量。</returns>
    public static int UnlockWithKey(this Entity entity, Entity key, Entity? side = null)
    {
        var hasUnlockFacet = key.Entries(FacetType.Unlocks).Any(f => ReferenceEquals(f.Value, entity));
        return entity.Locks.RemoveAll(l =>
            (side == null || l.Side == null || ReferenceEquals(l.Side, side)) &&
            (hasUnlockFacet || ReferenceEquals(l.Source, key) || Equals(l.Source, key)));
    }

    /// <summary>将物品实体置入可访问的容器实体中，执行严格无环、通达性与容量检查。</summary>
    /// <param name="container">容器实体（需具备 Container 角色或标签）。</param>
    /// <param name="item">待放入的物品实体。</param>
    /// <param name="position">在容器内的初始槽位。</param>
    /// <param name="fromScene">观察或操作者所处场景。</param>
    /// <returns>true 表示置入成功，false 表示不可访问、超容或不具备容器角色。</returns>
    public static bool Put(this Entity container, Entity item, int position = 0, Entity? fromScene = null)
    {
        if (!container.IsContainer() || !container.IsAccessible(fromScene))
        {
            return false;
        }

        // 检查容量上限
        if (container.Properties.TryGetValue("capacity", out var capVal) &&
            TryConvertInt(capVal, out var capacity) && capacity > 0)
        {
            var currentItemsCount = container.Children.Keys.OfType<Entity>().Count();
            if (currentItemsCount >= capacity)
            {
                return false;
            }
        }

        // 祖先环路检查（禁止将自身放入自身或其后代中）
        for (var ancestor = container as TreeMember; ancestor != null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, item))
                return false;
        }

        item.Reparent(container, position);
        return true;
    }

    /// <summary>从可访问的容器中取出实体，并转移到新的父节点中，受目标容量与通达性约束。</summary>
    /// <param name="container">容器实体。</param>
    /// <param name="item">被取出的实体。</param>
    /// <param name="newParent">新的归属父节点。</param>
    /// <param name="position">在新父节点中的槽位。</param>
    /// <param name="fromScene">操作场景。</param>
    /// <returns>true 表示取出成功，false 表示不可访问或该物品不属于此容器。</returns>
    public static bool Take(this Entity container, Entity item, TreeMember newParent, int position = 0, Entity? fromScene = null)
    {
        if (!container.IsAccessible(fromScene) || !ReferenceEquals(item.Parent, container))
        {
            return false;
        }

        if (newParent is Entity newEntity && newEntity.Properties.TryGetValue("capacity", out var capVal) &&
            TryConvertInt(capVal, out var capacity) && capacity > 0)
        {
            var currentItemsCount = newEntity.Children.Keys.OfType<Entity>().Count();
            if (currentItemsCount >= capacity)
            {
                return false;
            }
        }

        item.Reparent(newParent, position);
        return true;
    }

    private static bool TryConvertInt(object val, out int value)
    {
        switch (val)
        {
            case int i: value = i; return true;
            case long l: value = (int)l; return true;
            case float f: value = (int)f; return true;
            case double d: value = (int)d; return true;
            default: value = 0; return false;
        }
    }
}

#endregion

#region 场景拓扑与路径搜索系统

/// <summary>场景拓扑图管理结构，独立于世界树维护空间通道（Passage）与邻接索引。</summary>
public class TopologyGraph
{
    private readonly List<Passage> _passages = [];
    private ILookup<Entity, (Entity From, Entity Next, Passage Passage)>? _cachedAdjacency;

    public IReadOnlyList<Passage> Passages => _passages;

    public ILookup<Entity, (Entity From, Entity Next, Passage Passage)> Adjacency =>
        _cachedAdjacency ??= _passages
            .SelectMany(p => new[] { (From: p.Left, Next: p.Right, Passage: p), (From: p.Right, Next: p.Left, Passage: p) })
            .ToLookup(edge => edge.From);

    public void Add(Passage passage)
    {
        _passages.Add(passage);
        _cachedAdjacency = null;
    }

    public bool Remove(Passage passage)
    {
        var removed = _passages.Remove(passage);
        if (removed) _cachedAdjacency = null;
        return removed;
    }

    public Passage Connect(
        string name,
        Entity from,
        Entity to,
        double cost = 1.0,
        double? reverseCost = null,
        Entity? gate = null,
        Func<Entity, Entity?, object?, bool>? condition = null)
    {
        var passage = new Passage(name, from, to, cost, reverseCost, condition, gate != null ? [gate] : null);
        Add(passage);
        return passage;
    }

    public IReadOnlyList<Passage>? FindPath(
        Entity fromScene,
        Entity toScene,
        Entity? actor = null,
        object? env = null) =>
        TopologySystem.FindPath(fromScene, toScene, _passages, actor, env);

    public (Passage? Passage, Entity? NextScene) GetNextStep(
        Entity fromScene,
        TreeMember destination,
        Entity? actor = null,
        object? env = null) =>
        TopologySystem.GetNextStep(fromScene, destination, _passages, actor, env);
}

/// <summary>场景拓扑与路径搜索系统。</summary>
/// <remarks>
/// 负责基于拓扑通道（<see cref="Passage"/>）与通行代价（Cost）、通行许可（Allows）计算跨场景最优路径。
/// 核心不涉及房间内微观坐标与碰撞，仅计算宏观场景拓扑图。
/// </remarks>
public static class TopologySystem
{
    /// <summary>获取当前世界根节点关联的拓扑图实例。</summary>
    public static TopologyGraph? GetTopology(this TreeMember member)
    {
        var root = GetRoot(member);
        return root.Get<TopologyGraph>("topology");
    }

    /// <summary>沿世界树向上溯源，查找指定成员所属的场景实体（持有 "Scene" 标签）。</summary>
    /// <param name="member">世界树成员（物品或角色等）。</param>
    /// <returns>所属场景实体；若未处于任何场景下则返回 null。</returns>
    public static Entity? GetEnclosingScene(TreeMember? member)
    {
        var current = member;
        while (current != null)
        {
            if (current is Entity entity && entity.HasTag(CommonTag.Scene))
            {
                return entity;
            }
            current = current.Parent;
        }
        return null;
    }

    /// <summary>查找指定成员所在的世界根节点。</summary>
    public static TreeMember GetRoot(TreeMember member)
    {
        var current = member;
        while (current.Parent != null)
        {
            current = current.Parent;
        }
        return current;
    }

    /// <summary>收集指定成员所在世界的全部通道；若世界挂载了 TopologyGraph 则返回其通道，否则返回空列表。</summary>
    public static IReadOnlyList<Passage> CollectPassages(TreeMember root) =>
        root.GetTopology()?.Passages ?? [];

    /// <summary>计算从起始场景到目标场景的最优通道路径（Dijkstra 算法）。</summary>
    /// <param name="fromScene">出发端场景实体。</param>
    /// <param name="toScene">目的地场景实体。</param>
    /// <param name="passages">可选的可用通道集。若为 null 则自动通过所属世界拓扑图获取全部通道。</param>
    /// <param name="actor">通行主体（用于门禁与条件检查）。</param>
    /// <param name="env">环境上下文。</param>
    /// <returns>按通行顺序排列的 Passage 列表；若已在目标场景返回空列表；若不可达返回 null。</returns>
    public static IReadOnlyList<Passage>? FindPath(
        Entity fromScene,
        Entity toScene,
        IEnumerable<Passage>? passages = null,
        Entity? actor = null,
        object? env = null)
    {
        if (fromScene == toScene) return [];
        var passageList = passages ?? fromScene.GetTopology()?.Passages ?? [];
        var adjacency = passageList
            .SelectMany(p => new[] { (From: p.Left, Next: p.Right, Passage: p), (From: p.Right, Next: p.Left, Passage: p) })
            .ToLookup(edge => edge.From);
        var distances = new Dictionary<Entity, double> { [fromScene] = 0 };
        var previous = new Dictionary<Entity, (Entity Scene, Passage Passage)>();
        var queue = new PriorityQueue<Entity, (double Distance, int Order)>();
        // 同距离沿用邻接表的场景顺序，保持旧实现的路径选择稳定。
        var order = adjacency.Select((group, index) => (group.Key, index)).ToDictionary();
        queue.Enqueue(fromScene, (0, order.GetValueOrDefault(fromScene)));
        while (queue.TryDequeue(out var current, out var priority))
        {
            if (priority.Distance != distances[current]) continue;
            if (current == toScene)
            {
                var path = new List<Passage>();
                for (var node = toScene; previous.TryGetValue(node, out var step); node = step.Scene)
                    path.Add(step.Passage);
                path.Reverse();
                return path;
            }
            foreach (var (_, next, passage) in adjacency[current])
            {
                var cost = passage.CostFrom(current);
                if (!double.IsFinite(cost) || cost < 0 || !passage.Allows(current, actor, env)) continue;
                var distance = priority.Distance + cost;
                if (distance >= distances.GetValueOrDefault(next, double.PositiveInfinity)) continue;
                distances[next] = distance;
                previous[next] = (current, passage);
                queue.Enqueue(next, (distance, order[next]));
            }
        }
        return null;
    }

    /// <summary>解析前往远端目标（场景、物品或角色）的第一步通行决策。</summary>
    /// <param name="fromScene">主体当前所处场景。</param>
    /// <param name="destination">目标世界对象（场景本身，或位于某场景内的实体）。</param>
    /// <param name="passages">可选通道集合。</param>
    /// <param name="actor">移动主体。</param>
    /// <param name="env">环境上下文。</param>
    /// <returns>第一步要跨越的通道及对端场景；若已在同一场景返回 (null, fromScene)；若不可达返回 (null, null)。</returns>
    public static (Passage? Passage, Entity? NextScene) GetNextStep(
        Entity fromScene,
        TreeMember destination,
        IEnumerable<Passage>? passages = null,
        Entity? actor = null,
        object? env = null)
    {
        var targetScene = GetEnclosingScene(destination);
        if (targetScene == null) return (null, null);
        if (fromScene == targetScene) return (null, fromScene);
        var path = FindPath(fromScene, targetScene, passages, actor, env);
        return path is { Count: > 0 } ? (path[0], path[0].Other(fromScene)) : (null, null);
    }

    /// <summary>执行标准跨场景位移完整四阶段：拓扑验证 -> 通行代价结算 -> 树节点重挂载 -> 触发进出事件。</summary>
    /// <param name="actor">移动主体。</param>
    /// <param name="fromScene">起点场景。</param>
    /// <param name="toScene">目标场景。</param>
    /// <param name="topology">世界拓扑图。</param>
    /// <param name="onExit">起点场景离开事件回调。</param>
    /// <param name="onEnter">目标场景进入事件回调。</param>
    /// <param name="env">环境事实。</param>
    /// <returns>true 表示成功完成迁移；false 表示通道不通或受阻断。</returns>
    public static bool Transition(
        Entity actor,
        Entity fromScene,
        Entity toScene,
        TopologyGraph topology,
        Action<Entity, Entity>? onExit = null,
        Action<Entity, Entity>? onEnter = null,
        object? env = null)
    {
        // 1. 拓扑合法性检验
        var passage = topology.Passages.FirstOrDefault(p =>
            (ReferenceEquals(p.Left, fromScene) && ReferenceEquals(p.Right, toScene)) ||
            (ReferenceEquals(p.Right, fromScene) && ReferenceEquals(p.Left, toScene)));

        if (passage == null || !passage.Allows(fromScene, actor, env))
            return false;

        // 2. 通行代价
        var cost = passage.CostFrom(fromScene);
        if (!double.IsFinite(cost) || cost < 0)
            return false;

        // 3. 树节点重挂载
        actor.Reparent(toScene);

        // 4. 触发进出事件
        onExit?.Invoke(actor, fromScene);
        onEnter?.Invoke(actor, toScene);
        return true;
    }
}

#endregion

#region 动态影响与消耗品时效系统

/// <summary>状态效果堆叠策略枚举。</summary>
public enum StackingPolicy
{
    /// <summary>持续时间刷新策略：保留当前强度，重置到期时刻。</summary>
    DurationRefresh,

    /// <summary>强度层数累加策略：新强度累加到当前强度（受最大上限限制）。</summary>
    IntensityStacking,

    /// <summary>独立多实例策略：每个状态独立计时，综合取总和或最高值。</summary>
    IndependentMultiInstance,

    /// <summary>最高值覆盖策略：仅当新强度高于剩余强度时覆盖。</summary>
    HighestValueOverride
}

/// <summary>动态影响（Influence）、时效性与世界树生命周期操作体系。</summary>
/// <remarks>
/// 负责处理：
/// 1. 随行动值时间流逝的强度线性衰减与到期失效计算。
/// 2. 消耗品（Consumable）的剂量扣减与效果释放。
/// 3. 世界树实体的移除、槽位迁移、原位替换与组合重构。
/// </remarks>
public static class InfluenceSystem
{
    #region 动态影响与时效计算

    /// <summary>依据标准闭式公式计算指定 Influence 实体在特定时刻的剩余有效强度。</summary>
    /// <remarks>
    /// 公式：
    /// 若 t < t0 或 t >= t_expire，强度为 0；
    /// 若 t0 <= t < t_expire，强度为 max(0, I0 - λ * (t - t0))。
    /// </remarks>
    /// <param name="entity">具备 Influence 标签的实体。</param>
    /// <param name="currentTime">当前行动值时刻。若为 null 则返回未衰减的基础强度。</param>
    /// <returns>计算衰减与到期后的非负强度数值。</returns>
    public static double EffectiveIntensity(this Entity entity, double? currentTime = null)
    {
        if (!TryGetNumeric(entity.Properties, "intensity", out var intensity))
        {
            return 0.0;
        }

        if (!entity.HasTag(CommonTag.Influence) || currentTime is null)
        {
            return intensity;
        }

        var startedAt = TryGetNumeric(entity.Properties, "started_at", out var s) ? s : 0.0;
        // 未到起始时间
        if (currentTime.Value < startedAt)
        {
            return 0.0;
        }

        // 已过过期时间
        if (TryGetNumeric(entity.Properties, "expires_at", out var expiresAt) && currentTime.Value >= expiresAt)
        {
            return 0.0;
        }

        var decay = TryGetNumeric(entity.Properties, "decay", out var d) ? d : 0.0;
        var elapsed = currentTime.Value - startedAt;

        return Math.Max(0.0, intensity - elapsed * decay);
    }

    /// <summary>检查带有到期时刻（expires_at）或撤销状态（revoked）的实体在指定时间点是否有效。</summary>
    /// <param name="entity">待检查实体（如凭证、临时状态或许可）。</param>
    /// <param name="currentTime">当前行动值时间点。</param>
    /// <returns>true 表示未被撤销且未过期。</returns>
    public static bool IsEffectiveAt(this Entity entity, double currentTime)
    {
        if (entity.Properties.TryGetValue("revoked", out var revoked) && revoked is true)
        {
            return false;
        }

        if (TryGetNumeric(entity.Properties, "started_at", out var startedAt) && currentTime < startedAt)
        {
            return false;
        }

        return !TryGetNumeric(entity.Properties, "expires_at", out var expiresAt) || currentTime < expiresAt;
    }

    /// <summary>依据指定堆叠策略向宿主施加或合并状态效果。</summary>
    /// <param name="host">宿主实体。</param>
    /// <param name="effectName">效果名称。</param>
    /// <param name="intensity">施加强度。</param>
    /// <param name="duration">持续时长。</param>
    /// <param name="currentTime">当前时刻。</param>
    /// <param name="policy">堆叠策略。</param>
    /// <param name="decay">线性衰减率。</param>
    /// <param name="maxIntensity">强度累加上限。</param>
    /// <param name="source">来源实体。</param>
    /// <returns>生效的状态效果实体。</returns>
    public static Entity ApplyStatusEffect(
        Entity host,
        string effectName,
        double intensity,
        double duration,
        double currentTime,
        StackingPolicy policy = StackingPolicy.DurationRefresh,
        double decay = 0.0,
        double maxIntensity = double.PositiveInfinity,
        Entity? source = null)
    {
        var existing = host.Children.Keys.OfType<Entity>().FirstOrDefault(e =>
            e.HasTag(CommonTag.Influence) && e.Name == effectName);

        if (existing == null || policy == StackingPolicy.IndependentMultiInstance)
        {
            var effect = new Entity(
                effectName,
                host,
                new Dictionary<string, int> { ["Influence"] = 1 },
                properties: new Dictionary<string, object>
                {
                    ["intensity"] = intensity,
                    ["started_at"] = currentTime,
                    ["duration"] = duration,
                    ["expires_at"] = currentTime + duration,
                    ["decay"] = decay
                });
            if (source != null) effect.Relate(FacetType.Source, source);
            return effect;
        }

        var currentEffective = existing.EffectiveIntensity(currentTime);
        switch (policy)
        {
            case StackingPolicy.DurationRefresh:
                existing.Set("expires_at", currentTime + duration);
                break;

            case StackingPolicy.IntensityStacking:
                var newIntensity = Math.Min(maxIntensity, currentEffective + intensity);
                existing.Set("intensity", newIntensity);
                existing.Set("started_at", currentTime);
                existing.Set("expires_at", currentTime + duration);
                break;

            case StackingPolicy.HighestValueOverride:
                if (intensity > currentEffective)
                {
                    existing.Set("intensity", intensity);
                    existing.Set("started_at", currentTime);
                    existing.Set("expires_at", currentTime + duration);
                    existing.Set("decay", decay);
                }
                break;
        }
        return existing;
    }

    /// <summary>垃圾回收：扫描并清除宿主实体下所有已过期的 Influence 状态子节点。</summary>
    /// <param name="host">宿主实体。</param>
    /// <param name="currentTime">当前时刻。</param>
    /// <param name="onExpire">状态注销回调。</param>
    /// <returns>清除的到期状态数量。</returns>
    public static int CleanupExpired(Entity host, double currentTime, Action<Entity>? onExpire = null)
    {
        var expired = host.Children.Keys.OfType<Entity>()
            .Where(e => e.HasTag(CommonTag.Influence) && e.EffectiveIntensity(currentTime) <= 0.0)
            .ToList();

        foreach (var eff in expired)
        {
            onExpire?.Invoke(eff);
            eff.Reparent(null);
        }
        return expired.Count;
    }

    #endregion

    #region 消耗品与剂量操作

    /// <summary>消耗一次实体剂量，并返回其所承载的效果清单。</summary>
    /// <param name="consumable">具有 doses 属性的消耗品实体。</param>
    /// <returns>该次使用释放的效果对象列表。若剂量已耗尽则返回空列表。</returns>
    public static IReadOnlyList<object> Consume(this Entity consumable)
    {
        if (!consumable.Properties.TryGetValue("doses", out var val) || !TryConvertInt(val, out var doses) || doses <= 0)
        {
            return Array.Empty<object>();
        }

        consumable.Properties["doses"] = doses - 1;

        // 获取预存的效果列表
        if (consumable.Properties.TryGetValue("effects", out var effVal) && effVal is IEnumerable<object> effList)
        {
            return effList as IReadOnlyList<object> ?? effList.ToList();
        }

        return Array.Empty<object>();
    }

    #endregion

    private static bool TryGetNumeric(Dictionary<string, object> properties, string key, out double value)
    {
        if (properties.TryGetValue(key, out var val))
        {
            switch (val)
            {
                case double d: value = d; return true;
                case float f: value = f; return true;
                case int i: value = i; return true;
                case long l: value = l; return true;
            }
        }
        value = 0;
        return false;
    }

    private static bool TryConvertInt(object val, out int value)
    {
        switch (val)
        {
            case int i: value = i; return true;
            case long l: value = (int)l; return true;
            case float f: value = (int)f; return true;
            case double d: value = (int)d; return true;
            default: value = 0; return false;
        }
    }
}

#endregion
