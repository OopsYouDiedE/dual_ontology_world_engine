using System.Collections.Generic;
using System.Linq;

namespace DualOntologyWorldEngine.Core;

/// <summary>
/// 锁事实记录。
/// </summary>
/// <param name="Strength">锁的强度。</param>
/// <param name="Side">受阻侧场景。为 null 表示双向/全向封闭。</param>
/// <param name="Source">施加锁的对象或机制来源。</param>
public record LockRecord(int Strength, Entity? Side = null, object? Source = null);

/// <summary>
/// 空间可访问性、容器收纳与门禁系统。
/// </summary>
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

    /// <summary>
    /// 获取实体上施加的所有锁记录列表。
    /// </summary>
    private static List<LockRecord> GetLocks(Entity entity)
    {
        if (entity.Properties.TryGetValue(LocksKey, out var val) && val is List<LockRecord> list)
        {
            return list;
        }
        var newList = new List<LockRecord>();
        entity.Properties[LocksKey] = newList;
        return newList;
    }

    /// <summary>
    /// 检查实体在指定场景方向上是否处于锁定状态。
    /// </summary>
    /// <param name="entity">待检查的目标实体（如门或箱子）。</param>
    /// <param name="fromScene">主体所处的场景实体。为 null 时仅检查全局锁。</param>
    /// <returns>true 表示存在生效的阻断锁，false 表示未上锁。</returns>
    public static bool IsLocked(this Entity entity, Entity? fromScene = null)
    {
        var locks = GetLocks(entity);
        return locks.Any(l => l.Strength > 0 && (l.Side == null || ReferenceEquals(l.Side, fromScene)));
    }

    /// <summary>
    /// 检查实体是否在当前方向允许访问或穿越。
    /// </summary>
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

    /// <summary>
    /// 在当前方向没有有效锁阻断时开启对象。
    /// </summary>
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

    /// <summary>
    /// 关闭可开合对象。
    /// </summary>
    public static void Close(this Entity entity)
    {
        entity.Set(OpenedKey, false);
    }

    /// <summary>
    /// 为实体施加一把锁并自动闭合对象。
    /// </summary>
    /// <param name="entity">目标实体。</param>
    /// <param name="strength">锁强度。</param>
    /// <param name="side">特定阻断场景方向。为 null 表示全向锁定。</param>
    /// <param name="source">加锁来源。</param>
    /// <returns>新生成的锁记录。</returns>
    public static LockRecord Lock(this Entity entity, int strength = 1, Entity? side = null, object? source = null)
    {
        entity.Close();
        var record = new LockRecord(strength, side, source);
        GetLocks(entity).Add(record);
        return record;
    }

    /// <summary>
    /// 移除匹配的锁记录。
    /// </summary>
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

    /// <summary>
    /// 将物品实体置入可访问的容器实体中。
    /// </summary>
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

    /// <summary>
    /// 从可访问的容器中取出实体，并转移到新的父节点中。
    /// </summary>
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
