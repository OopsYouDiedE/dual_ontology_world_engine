using System;
using System.Collections.Generic;
using System.Linq;

namespace DualOntologyWorldEngine.Core;

/// <summary>
/// 动态影响（Influence）、时效性与世界树生命周期操作体系。
/// </summary>
/// <remarks>
/// 负责处理：
/// 1. 随行动值时间流逝的强度线性衰减与到期失效计算。
/// 2. 消耗品（Consumable）的剂量扣减与效果释放。
/// 3. 世界树实体的移除、槽位迁移、原位替换与组合重构。
/// </remarks>
public static class InfluenceSystem
{
    #region 动态影响与时效计算

    /// <summary>
    /// 计算指定 Influence 实体在特定时刻的剩余有效强度。
    /// </summary>
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

    /// <summary>
    /// 检查带有到期时刻（expires_at）或撤销状态（revoked）的实体在指定时间点是否有效。
    /// </summary>
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

    /// <summary>
    /// 消耗一次实体剂量，并返回其所承载的效果清单。
    /// </summary>
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
