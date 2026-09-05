
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
    private const string LocksKey = "_locks";
    private const string OpenedKey = "opened";

    /// <summary>获取实体上施加的所有锁记录列表。</summary>
    private static List<LockRecord> GetLocks(Entity entity) =>
        entity.Get<List<LockRecord>>(LocksKey) ?? [];

    /// <summary>检查实体在指定场景方向上是否处于锁定状态。</summary>
    /// <param name="entity">待检查的目标实体（如门或箱子）。</param>
    /// <param name="fromScene">主体所处的场景实体。为 null 时仅检查全局锁。</param>
    /// <returns>true 表示存在生效的阻断锁，false 表示未上锁。</returns>
    public static bool IsLocked(this Entity entity, Entity? fromScene = null)
    {
        var locks = GetLocks(entity);
        return locks.Any(l => l.Strength > 0 && (l.Side == null || ReferenceEquals(l.Side, fromScene)));
    }

    /// <summary>检查实体是否在当前方向允许访问或穿越。</summary>
    /// <remarks>
    /// 若实体不具备 Openable 标记，默认始终可访问；
    /// 若具备 Openable 标记，则必须已开启（opened == true）且未被锁阻断。
    /// </remarks>
    public static bool IsAccessible(this Entity entity, Entity? fromScene = null)
    {
        var openable = entity.HasTag("Openable") || entity.HasRole(Role.AccessControl) || entity.HasRole(Role.Container);
        var opened = entity.Get<bool>(OpenedKey, false);

        return (!openable || opened) && !entity.IsLocked(fromScene);
    }

    /// <summary>在当前方向没有有效锁阻断时开启对象。</summary>
    /// <returns>true 表示成功开启，false 表示因上锁阻断失败。</returns>
    public static bool Open(this Entity entity, Entity? fromScene = null)
    {
        if (entity.IsLocked(fromScene))
        {
            return false;
        }

        entity.Set(OpenedKey, true);
        return true;
    }

    /// <summary>关闭可开合对象。</summary>
    public static void Close(this Entity entity)
    {
        entity.Set(OpenedKey, false);
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
        var locks = GetLocks(entity);
        locks.Add(record);
        entity.Set(LocksKey, locks);
        return record;
    }

    /// <summary>移除匹配的锁记录。</summary>
    /// <param name="entity">目标实体。</param>
    /// <param name="side">匹配的方向。为 null 表示匹配全局锁。</param>
    /// <param name="source">匹配的来源。为 null 则忽略来源过滤。</param>
    /// <returns>成功移除的锁数量。</returns>
    public static int Unlock(this Entity entity, Entity? side = null, object? source = null)
    {
        var locks = GetLocks(entity);
        return locks.RemoveAll(l =>
            (side == null || l.Side == null || ReferenceEquals(l.Side, side)) &&
            (source == null || Equals(l.Source, source)));
    }

    /// <summary>将物品实体置入可访问的容器实体中。</summary>
    /// <param name="container">容器实体（需具备 Container 角色或标签）。</param>
    /// <param name="item">待放入的物品实体。</param>
    /// <param name="position">在容器内的初始槽位。</param>
    /// <param name="fromScene">观察或操作者所处场景。</param>
    /// <returns>true 表示置入成功，false 表示不可访问或不具备容器角色。</returns>
    public static bool Put(this Entity container, Entity item, int position = 0, Entity? fromScene = null)
    {
        var isContainer = container.HasTag("Container") || container.HasRole(Role.Container);
        if (!isContainer || !container.IsAccessible(fromScene))
        {
            return false;
        }

        item.Reparent(container, position);
        return true;
    }

    /// <summary>从可访问的容器中取出实体，并转移到新的父节点中。</summary>
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

        item.Reparent(newParent, position);
        return true;
    }
}

#endregion

#region 场景拓扑与路径搜索系统

/// <summary>场景拓扑与路径搜索系统。</summary>
/// <remarks>
/// 负责在世界树中收集拓扑通道（<see cref="Passage"/>），
/// 并基于通行代价（Cost）与通行许可（Allows）计算跨场景最优路径。
/// 核心不涉及房间内微观坐标与碰撞，仅计算宏观场景拓扑图。
/// </remarks>
public static class TopologySystem
{
    /// <summary>沿世界树向上溯源，查找指定成员所属的场景实体（持有 "Scene" 标签）。</summary>
    /// <param name="member">世界树成员（物品、角色或通道等）。</param>
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

    /// <summary>递归收集指定根节点或世界树分支下的所有通道（<see cref="Passage"/>）。</summary>
    public static List<Passage> CollectPassages(TreeMember root)
    {
        var passages = new List<Passage>();
        var stack = new Stack<TreeMember>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is Passage p)
            {
                passages.Add(p);
            }

            foreach (var child in node.Children.Keys)
            {
                stack.Push(child);
            }
        }

        return passages;
    }

    /// <summary>计算从起始场景到目标场景的最优通道路径（Dijkstra 算法）。</summary>
    /// <param name="fromScene">出发端场景实体。</param>
    /// <param name="toScene">目的地场景实体。</param>
    /// <param name="passages">可选的可用通道集。若为 null 则自动向上追溯世界树根节点并收集全部通道。</param>
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
        var adjacency = (passages ?? CollectPassages(GetRoot(fromScene)))
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
}

#endregion

#region 动态影响与消耗品时效系统

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

    /// <summary>计算指定 Influence 实体在特定时刻的剩余有效强度。</summary>
    /// <param name="entity">具备 Influence 标签的实体。</param>
    /// <param name="currentTime">当前行动值时刻。若为 null 则返回未衰减的基础强度。</param>
    /// <returns>计算衰减与到期后的非负强度数值。</returns>
    public static float EffectiveIntensity(this Entity entity, float? currentTime = null)
    {
        var intensity = entity.Get<float>("intensity", entity.Get<int>("intensity", 0));
        if (!entity.HasTag("Influence") || currentTime == null)
        {
            return intensity;
        }

        var expiresAt = entity.Get<float?>("expires_at", null);
        if (expiresAt.HasValue && currentTime.Value >= expiresAt.Value)
        {
            return 0.0f;
        }

        var startedAt = entity.Get<float>("started_at", currentTime.Value);
        var decay = entity.Get<float>("decay", 0.0f);
        var elapsed = Math.Max(0.0f, currentTime.Value - startedAt);

        return Math.Max(0.0f, intensity - elapsed * decay);
    }

    /// <summary>检查带有到期时刻（expires_at）或撤销状态（revoked）的实体在指定时间点是否有效。</summary>
    /// <param name="entity">待检查实体（如凭证、临时状态或许可）。</param>
    /// <param name="currentTime">当前行动值时间点。</param>
    /// <returns>true 表示未被撤销且未过期。</returns>
    public static bool IsEffectiveAt(this Entity entity, float currentTime)
    {
        var revoked = entity.Get<bool>("revoked", false);
        if (revoked) return false;

        var expiresAt = entity.Get<float?>("expires_at", null);
        return !expiresAt.HasValue || currentTime < expiresAt.Value;
    }

    #endregion

    #region 消耗品与剂量操作

    /// <summary>消耗一次实体剂量，并返回其所承载的效果清单。</summary>
    /// <param name="consumable">具有 doses 属性的消耗品实体。</param>
    /// <returns>该次使用释放的效果对象列表。若剂量已耗尽则返回空列表。</returns>
    public static IReadOnlyList<object> Consume(this Entity consumable)
    {
        var doses = consumable.Get<int>("doses", 0);
        if (doses <= 0)
        {
            return Array.Empty<object>();
        }

        consumable.Set("doses", doses - 1);

        // 获取预存的效果列表
        if (consumable.Properties.TryGetValue("effects", out var effVal) && effVal is IEnumerable<object> effList)
        {
            return effList.ToList();
        }

        return Array.Empty<object>();
    }

    #endregion
}

#endregion
