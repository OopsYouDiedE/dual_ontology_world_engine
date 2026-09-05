using System;
using System.Collections.Generic;
using System.Linq;

namespace DualOntologyWorldEngine.Core;

/// <summary>
/// 源质贡献记录。
/// </summary>
/// <param name="Source">提供此源质力量的实体或客观对象引用。</param>
/// <param name="Rule">产生此项贡献的投影规则标识（如 "intrinsic", "material", "property:sharpness", "relation:Witness"）。</param>
/// <param name="Aspect">贡献的源质类型。</param>
/// <param name="Strength">贡献的有效强度值。</param>
public readonly record struct AspectContribution(
    object Source,
    string Rule,
    Aspect Aspect,
    int Strength);

/// <summary>
/// 源质（Aspect）投影与超自然力量计算系统。
/// </summary>
/// <remarks>
/// 源质描述实体在十二项超自然存在原则上的象征与干涉力量。
/// 源质不是静态硬编码的绝对数值，而是单次行动或交互时从客观实体中无副作用实时投影的结果。
/// 
/// 结算原则：
/// 1. 固有性（Intrinsic）：实体自身携带的无条件 Aspect 标签。
/// 2. 物质传承（Material）：从构成材料实体按完整度（Integrity）比例传承源质。
/// 3. 物性投影（Property）：硬度、锋利度、反应性等客观物理性质按常数规则投影。
/// 4. 关系定向（Relation）：指向目标实体的既成事实关系（所有、见证、描绘、伤害等）定向产生源质。
/// 5. 情感浸润（Emotion/Influence）：依附于实体的情感或动态影响随时间衰减后投影。
/// 
/// 源质计算纯函数化，无修改实体状态的副作用。
/// </remarks>
public static class AspectSystem
{
    /// <summary>
    /// 物性到源质的投影规则映射表。(目标源质, 乘数, 除数)
    /// </summary>
    private static readonly Dictionary<string, (Aspect Aspect, int Multiplier, int Divisor)[]> PropertyAspects = new()
    {
        ["hardness"] = [(Aspect.Form, 1, 2), (Aspect.Preservation, 1, 4)],
        ["durability"] = [(Aspect.Preservation, 1, 1), (Aspect.Form, 1, 3)],
        ["sharpness"] = [(Aspect.Strife, 1, 1), (Aspect.Death, 1, 3)],
        ["permeability"] = [(Aspect.Threshold, 1, 1)],
        ["reactivity"] = [(Aspect.Transmutation, 1, 1)],
        ["vitality"] = [(Aspect.Preservation, 1, 1)],
        ["lethality"] = [(Aspect.Death, 1, 1), (Aspect.Strife, 1, 2)],
        ["recognition"] = [(Aspect.Name, 1, 1)],
        ["authority"] = [(Aspect.Law, 1, 1), (Aspect.Name, 1, 2)],
        ["cognition"] = [(Aspect.Mind, 1, 1)],
        ["affinity"] = [(Aspect.Bond, 1, 1)],
        ["motivation"] = [(Aspect.Desire, 1, 1)],
        ["conflict"] = [(Aspect.Strife, 1, 1)],
        ["antiquity"] = [(Aspect.Vestige, 1, 1)],
    };

    /// <summary>
    /// 实体语义关系到源质的定向投影强度映射表。
    /// 仅在交互目标与关系目标匹配时生效。
    /// </summary>
    private static readonly Dictionary<FacetType, (Aspect Aspect, int Multiplier)[]> RelationAspects = new()
    {
        [FacetType.Owner] = [(Aspect.Bond, 1), (Aspect.Name, 1)],
        [FacetType.Bloodline] = [(Aspect.Bond, 1)],
        [FacetType.Witness] = [(Aspect.Bond, 1), (Aspect.Law, 1), (Aspect.Vestige, 1)],
        [FacetType.Depicts] = [(Aspect.Bond, 1), (Aspect.Vestige, 1)],
        [FacetType.RecordOf] = [(Aspect.Vestige, 1)],
        [FacetType.MadeBy] = [(Aspect.Bond, 1), (Aspect.Vestige, 1)],
        [FacetType.Harmed] = [(Aspect.Bond, 1), (Aspect.Strife, 1), (Aspect.Vestige, 1)],
        [FacetType.IssuedBy] = [(Aspect.Law, 1), (Aspect.Name, 1)],
        [FacetType.Grants] = [(Aspect.Law, 1)],
        [FacetType.Unlocks] = [(Aspect.Threshold, 1), (Aspect.Bond, 1)],
        [FacetType.TrueNameOf] = [(Aspect.Name, 3), (Aspect.Bond, 1)],
    };

    /// <summary>
    /// 情感类型到源质的投影规则。(主要全额源质, 次要半额源质)
    /// </summary>
    private static readonly Dictionary<Emotion, (Aspect[] Primary, Aspect[] Secondary)> EmotionAspects = new()
    {
        [Emotion.Love] = ([Aspect.Desire, Aspect.Bond], [Aspect.Preservation]),
        [Emotion.Hatred] = ([Aspect.Desire, Aspect.Strife], [Aspect.Death]),
        [Emotion.Fear] = ([Aspect.Desire, Aspect.Preservation], [Aspect.Threshold]),
        [Emotion.Anger] = ([Aspect.Desire, Aspect.Strife], [Aspect.Form]),
        [Emotion.Joy] = ([Aspect.Desire, Aspect.Preservation], [Aspect.Bond]),
        [Emotion.Sorrow] = ([Aspect.Death, Aspect.Vestige], [Aspect.Bond]),
        [Emotion.Hope] = ([Aspect.Desire, Aspect.Preservation], [Aspect.Threshold]),
        [Emotion.Despair] = ([Aspect.Death, Aspect.Desire], [Aspect.Mind]),
        [Emotion.Remorse] = ([Aspect.Vestige, Aspect.Law], [Aspect.Bond]),
        [Emotion.Devotion] = ([Aspect.Bond, Aspect.Law], [Aspect.Preservation]),
        [Emotion.Obsession] = ([Aspect.Desire, Aspect.Preservation], [Aspect.Mind]),
        [Emotion.Serenity] = ([Aspect.Mind, Aspect.Preservation], [Aspect.Law]),
    };

    /// <summary>
    /// 计算指定实体的所有源质贡献明细。
    /// </summary>
    /// <param name="entity">计算主体实体。</param>
    /// <param name="target">当前交互或施法所指向的目标实体。关系型源质仅在目标匹配时生效。</param>
    /// <param name="currentTime">当前全局行动值时间，用于计算情感动态衰减。</param>
    /// <returns>源质贡献明细清单，保留来源溯源信息。</returns>
    public static IReadOnlyList<AspectContribution> GetAspectContributions(
        this Entity entity,
        Entity? target = null,
        float? currentTime = null)
    {
        var contributions = new List<AspectContribution>();

        // 1. 固有源质（Intrinsic Aspect Tags）
        foreach (Aspect aspect in Enum.GetValues<Aspect>())
        {
            var strength = entity.Tag(aspect.ToString());
            if (strength > 0)
            {
                contributions.Add(new AspectContribution(entity, "intrinsic", aspect, strength));
            }
        }

        // 2. 材料实体继承（Material Inheritance）
        var material = entity.Get<Entity>("material");
        if (material != null)
        {
            var (numerator, denominator) = entity.GetIntegrityFraction();
            foreach (Aspect aspect in Enum.GetValues<Aspect>())
            {
                var matStrength = material.Tag(aspect.ToString());
                if (matStrength > 0)
                {
                    var inherited = (matStrength * numerator) / Math.Max(1, denominator);
                    if (inherited > 0)
                    {
                        contributions.Add(new AspectContribution(material, "material", aspect, inherited));
                    }
                }
            }
        }

        // 3. 具体客观物性投影（Property Projections）
        foreach (var (propName, rules) in PropertyAspects)
        {
            if (entity.TryGetNumericProperty(propName, out var propVal) && propVal > 0)
            {
                foreach (var (aspect, mul, div) in rules)
                {
                    var projected = (int)(propVal * mul / div);
                    if (projected > 0)
                    {
                        contributions.Add(new AspectContribution(entity, $"property:{propName}", aspect, projected));
                    }
                }
            }
        }

        // 4. 情感与动态影响（Influence Projections）
        if (entity.HasTag("Influence") && entity.Properties.TryGetValue("emotion", out var emotionObj))
        {
            Emotion? emotion = emotionObj switch
            {
                Emotion e => e,
                string s when Enum.TryParse<Emotion>(s, true, out var parsed) => parsed,
                _ => null
            };

            var effective = entity.EffectiveIntensity(currentTime);

            if (emotion is { } validEmotion && EmotionAspects.TryGetValue(validEmotion, out var rules) && effective > 0)
            {
                foreach (var primary in rules.Primary)
                {
                    var amount = (int)Math.Round(effective, MidpointRounding.AwayFromZero);
                    if (amount > 0)
                    {
                        contributions.Add(new AspectContribution(entity, $"emotion:{validEmotion}", primary, amount));
                    }
                }

                foreach (var secondary in rules.Secondary)
                {
                    var amount = (int)Math.Round(effective / 2f, MidpointRounding.AwayFromZero);
                    if (amount > 0)
                    {
                        contributions.Add(new AspectContribution(entity, $"emotion:{validEmotion}:sub", secondary, amount));
                    }
                }
            }
        }

        // 5. 定向语义关系投影（Target-specific Relation Projections）
        if (target != null)
        {
            foreach (var facet in entity.Facets)
            {
                if (ReferenceEquals(facet.Value, target) && RelationAspects.TryGetValue(facet.Type, out var rules))
                {
                    foreach (var (aspect, mul) in rules)
                    {
                        var amount = facet.Strength * mul;
                        if (amount > 0)
                        {
                            contributions.Add(new AspectContribution(
                                facet.Source ?? facet.Value,
                                $"relation:{facet.Type}",
                                aspect,
                                amount));
                        }
                    }
                }
            }
        }

        // 去重：同一来源、同一规则、同一源质仅计算一次
        var unique = new List<AspectContribution>();

        foreach (var c in contributions)
        {
            if (!unique.Any(item =>
                ReferenceEquals(item.Source, c.Source) &&
                item.Rule == c.Rule &&
                item.Aspect == c.Aspect))
            {
                unique.Add(c);
            }
        }

        return unique;
    }

    /// <summary>
    /// 计算指定实体的客观源质总值映射。
    /// </summary>
    /// <param name="entity">计算主体。</param>
    /// <param name="target">交互目标。</param>
    /// <param name="currentTime">当前行动值时刻。</param>
    /// <returns>每种源质对应的累计有效强度字典。</returns>
    public static Dictionary<Aspect, int> GetAspects(
        this Entity entity,
        Entity? target = null,
        float? currentTime = null)
    {
        var totals = new Dictionary<Aspect, int>();
        var contributions = entity.GetAspectContributions(target, currentTime);

        foreach (var c in contributions)
        {
            totals[c.Aspect] = totals.GetValueOrDefault(c.Aspect, 0) + c.Strength;
        }

        return totals;
    }

    #region 辅助解析函数

    private static (int Numerator, int Denominator) GetIntegrityFraction(this Entity entity)
    {
        if (entity.Properties.TryGetValue("integrity", out var val))
        {
            if (val is ValueTuple<int, int> vt) return vt;
            if (val is int[] arr && arr.Length >= 2) return (arr[0], arr[1]);
            if (val is float f) return ((int)(f * 100), 100);
            if (val is double d) return ((int)(d * 100), 100);
        }
        return (1, 1);
    }

    private static bool TryGetNumericProperty(this Entity entity, string key, out double value)
    {
        if (entity.Properties.TryGetValue(key, out var val))
        {
            if (val is int i) { value = i; return true; }
            if (val is float f) { value = f; return true; }
            if (val is double d) { value = d; return true; }
        }
        value = 0;
        return false;
    }

    #endregion
}
