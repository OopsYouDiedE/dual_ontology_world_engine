using System;
using System.Collections.Generic;
using System.Linq;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;

namespace DualOntologyWorldEngine.Core.Social;

/// <summary>
/// 有限社交评价、目标情绪组合与态度压力系统。
/// </summary>
/// <remarks>
/// SocialSystem 负责处理人际交互中的主观心理反馈与行为倾向修正：
/// 1. 社交评价驱动（<see cref="SelectSocialInfluence"/>）：基于交互事件中的善意、威胁、损失或背叛等评价原语（Appraisal），决选复合情绪模式；
/// 2. 复合情绪包（<see cref="Combinations"/>）：将情境事件具象化为包含强度与衰减时效的复合情绪态（如 SAFE_ACCEPTANCE、MORAL_REJECTION 等）；
/// 3. 态度模式偏好（<see cref="AttitudePressures"/>）：将情绪态映射为对具体行为反应模式（<see cref="ResponseMode"/>）的决策加减分偏好。
/// </remarks>
public static class SocialSystem
{
    /// <summary>
    /// 预设的社交情境与目标复合情绪触发包映射表。
    /// 格式为：[组合名 -> [(情绪类型, 初始强度, 留存持久步数)]]。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (string Emotion, int Intensity, int Persistence)[]> Combinations =
        new Dictionary<string, (string Emotion, int Intensity, int Persistence)[]>
        {
            ["NO_STRONG_CHANGE"] = [],
            ["AFFILIATION_GAIN"] = [("Joy", 2, 2), ("Hope", 1, 2)],
            ["SAFE_ACCEPTANCE"] = [("Serenity", 2, 2), ("Hope", 1, 2), ("Joy", 1, 1)],
            ["HURT_WITHDRAWAL"] = [("Sorrow", 2, 2), ("Anger", 1, 1)],
            ["DEFENSIVE_ANGER"] = [("Anger", 2, 1), ("Fear", 1, 1)],
            ["MORAL_REJECTION"] = [("Anger", 2, 2), ("Hatred", 1, 2), ("Sorrow", 1, 2)],
            ["GRIEF_BOND"] = [("Sorrow", 3, 3), ("Devotion", 2, 3)],
            ["REMORSE_REPAIR"] = [("Remorse", 3, 2), ("Sorrow", 1, 2), ("Hope", 1, 1)],
            ["CAUTIOUS_REOPENING"] = [("Hope", 1, 2), ("Serenity", 1, 1), ("Sorrow", 1, 2)],
            ["SHARED_RESOLVE"] = [("Hope", 2, 2), ("Devotion", 1, 2), ("Serenity", 1, 1)],
            ["THREATENED_DISTRUST"] = [("Fear", 2, 2), ("Anger", 1, 2), ("Hatred", 1, 2)]
        };

    /// <summary>
    /// 各复合情绪态对不同社交应对模式（ResponseMode）施加的动机压力加权表。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<ResponseMode, int>> AttitudePressures =
        new Dictionary<string, IReadOnlyDictionary<ResponseMode, int>>
        {
            ["NO_STRONG_CHANGE"] = new Dictionary<ResponseMode, int>(),
            ["AFFILIATION_GAIN"] = new Dictionary<ResponseMode, int>
            {
                [ResponseMode.Cooperate] = 2,
                [ResponseMode.Negotiate] = 1
            },
            ["SAFE_ACCEPTANCE"] = new Dictionary<ResponseMode, int>
            {
                [ResponseMode.Cooperate] = 2,
                [ResponseMode.Negotiate] = 2,
                [ResponseMode.Conceal] = -1,
                [ResponseMode.Avoid] = -1
            },
            ["HURT_WITHDRAWAL"] = new Dictionary<ResponseMode, int>
            {
                [ResponseMode.Avoid] = 3,
                [ResponseMode.Restrain] = 1,
                [ResponseMode.Cooperate] = -2
            },
            ["DEFENSIVE_ANGER"] = new Dictionary<ResponseMode, int>
            {
                [ResponseMode.Restrain] = 3,
                [ResponseMode.Protect] = 1,
                [ResponseMode.Avoid] = 1
            },
            ["MORAL_REJECTION"] = new Dictionary<ResponseMode, int>
            {
                [ResponseMode.Restrain] = 4,
                [ResponseMode.Avoid] = 2,
                [ResponseMode.Cooperate] = -3
            },
            ["GRIEF_BOND"] = new Dictionary<ResponseMode, int>
            {
                [ResponseMode.Protect] = 2,
                [ResponseMode.Avoid] = 1
            },
            ["REMORSE_REPAIR"] = new Dictionary<ResponseMode, int>
            {
                [ResponseMode.Negotiate] = 3,
                [ResponseMode.Cooperate] = 2,
                [ResponseMode.Conceal] = -2
            },
            ["CAUTIOUS_REOPENING"] = new Dictionary<ResponseMode, int>
            {
                [ResponseMode.Verify] = 2,
                [ResponseMode.Negotiate] = 1,
                [ResponseMode.Cooperate] = 1
            },
            ["SHARED_RESOLVE"] = new Dictionary<ResponseMode, int>
            {
                [ResponseMode.Cooperate] = 4,
                [ResponseMode.Protect] = 1
            },
            ["THREATENED_DISTRUST"] = new Dictionary<ResponseMode, int>
            {
                [ResponseMode.Avoid] = 3,
                [ResponseMode.Verify] = 2,
                [ResponseMode.Restrain] = 2,
                [ResponseMode.Conceal] = 1,
                [ResponseMode.Cooperate] = -3
            }
        };

    /// <summary>
    /// 根据交互事件中的社交评价命题集合（Appraisal），决选匹配最恰当的复合情绪组合键名。
    /// </summary>
    /// <param name="appraisal">包含 bond、threat 及各情境布尔标记的评价值字典。</param>
    /// <returns>命中的复合情绪组合名；未命中强变化时返回 "NO_STRONG_CHANGE"。</returns>
    public static string SelectSocialInfluence(IReadOnlyDictionary<string, object> appraisal)
    {
        int bond = 0;
        if (appraisal.TryGetValue("bond", out var bVal) && int.TryParse(bVal.ToString(), out var b)) bond = b;

        int threat = 0;
        if (appraisal.TryGetValue("threat", out var tVal) && int.TryParse(tVal.ToString(), out var t)) threat = t;

        bool BoolVal(string key) => appraisal.TryGetValue(key, out var v) && v is true;

        if (BoolVal("corrected_self_blame")) return "REMORSE_REPAIR";
        if (BoolVal("irreversible_loss") && bond >= 1) return "GRIEF_BOND";
        if (BoolVal("malicious_intent") && threat >= 2) return "THREATENED_DISTRUST";
        if (BoolVal("costly_repair")) return "CAUTIOUS_REOPENING";
        if (BoolVal("credible_but_unverified_offer")) return "CAUTIOUS_REOPENING";
        if (BoolVal("severe_harm_attributed_to_target")) return "MORAL_REJECTION";
        if ((BoolVal("reciprocity_breach") || BoolVal("truth_conflict")) && bond >= 1) return "HURT_WITHDRAWAL";
        if (BoolVal("vulnerability_accepted")) return "SAFE_ACCEPTANCE";
        if (BoolVal("shared_objective_advanced")) return "SHARED_RESOLVE";
        if (BoolVal("support_received")) return "AFFILIATION_GAIN";
        if (BoolVal("norm_breach")) return "DEFENSIVE_ANGER";

        return "NO_STRONG_CHANGE";
    }

    /// <summary>
    /// 创建并向主体挂载一个包含多个时效性情感影响（Influence）的复合社交冲击包实体。
    /// </summary>
    /// <param name="name">社交冲击包调试名称。</param>
    /// <param name="parent">承受该社交冲击的 Soul 主体。</param>
    /// <param name="target">本次社交关系指向的目标客体。</param>
    /// <param name="source">触发本次社交事件的发起者。</param>
    /// <param name="combination">采用的复合情绪组合键名。</param>
    /// <param name="startedAt">发生该冲击的起始行动值时刻。</param>
    /// <param name="durationUnit">持久步数转化为行动值时长的基准系数（默认 200f）。</param>
    /// <returns>装配好的 SocialImpact 实体引用。</returns>
    public static Entity CreateSocialImpact(
        string name,
        Soul parent,
        Entity target,
        Entity source,
        string combination,
        double startedAt,
        double durationUnit = 200)
    {
        var specs = Combinations.GetValueOrDefault(combination, []);
        double duration = (specs.Length > 0 ? specs.Max(item => item.Persistence) : 0) * durationUnit;

        var impact = new Entity(
            name,
            parent,
            new Dictionary<string, int> { ["SocialImpact"] = 1, ["InfluenceBundle"] = 1 },
            properties: new Dictionary<string, object>
            {
                ["combination"] = combination,
                ["started_at"] = startedAt,
                ["duration"] = duration,
                ["expires_at"] = startedAt + duration
            });

        impact.Relate("Target", target);
        impact.Relate("Source", source);

        foreach (var (emotion, intensity, persistence) in specs)
        {
            var inf = new Entity(
                $"{impact.Name}·{emotion}",
                impact,
                new Dictionary<string, int> { ["Influence"] = 1, [emotion] = intensity },
                properties: new Dictionary<string, object>
                {
                    ["emotion"] = emotion,
                    ["intensity"] = (float)intensity,
                    ["target"] = target,
                    ["source"] = source,
                    ["mode"] = "relational",
                    ["persistence"] = persistence,
                    ["started_at"] = startedAt,
                    ["expires_at"] = startedAt + persistence * durationUnit
                });
            inf.Relate("Target", target);
            inf.Relate("Source", source);
        }

        return impact;
    }

    /// <summary>
    /// 查询指定复合情绪态对特定社交应对模式施加的偏好修正值。
    /// </summary>
    /// <param name="combination">情绪组合键名。</param>
    /// <param name="mode">待检查的社交应对模式枚举。</param>
    /// <returns>动机偏好修正分值（未配置时返回 0）。</returns>
    public static int GetSocialAttitudePressure(string combination, ResponseMode? mode)
    {
        if (mode == null) return 0;
        if (AttitudePressures.TryGetValue(combination, out var map) && map.TryGetValue(mode.Value, out var val))
        {
            return val;
        }
        return 0;
    }
}
