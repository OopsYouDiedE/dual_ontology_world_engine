using DualOntologyWorldEngine.Core.Rules;

namespace DualOntologyWorldEngine.Core.Mind;

/// <summary>机制规则在一次行动中选择心智目标的通用槽位。</summary>
public enum MindTarget
{
    ActorMind,
    TargetMind,
    Witnesses
}

/// <summary>由某类已成功 Effect 触发的情绪来源配置。</summary>
public sealed record EmotionEffectRule(
    Effect Effect,
    MindTarget Target,
    Emotion Emotion,
    int Delta,
    string Source,
    bool Persistent = false);

/// <summary>由某类已成功 Effect 触发的疲劳变化配置。</summary>
public sealed record FatigueEffectRule(Effect Effect, MindTarget Target, double Delta);

/// <summary>由某类已成功 Effect 触发的双向关系变化配置。</summary>
public sealed record BondEffectRule(Effect Effect, MindTarget Target, int Delta);

/// <summary>
/// 心智通用机制的配置解释器。
/// 情绪、疲劳和关系规则都以 Core 值对象配置，并由 ActionRuntime 在事件生成后统一结算。
/// </summary>
public sealed class MindMechanics
{
    public List<EmotionEffectRule> EmotionRules { get; } = new();
    public List<FatigueEffectRule> FatigueRules { get; } = new();
    public List<BondEffectRule> BondRules { get; } = new();

    /// <summary>短时与长效情绪的周期结算参数。</summary>
    public float TransientEmotionDecay { get; set; } = 0.5f;
    public float PersistentEmotionDecay { get; set; } = 0.95f;

    /// <summary>按当前行动与已登记见证者结算全部匹配规则。</summary>
    public void Apply(
        Intent intent,
        Entity ev,
        RuleEnvironment environment,
        IEnumerable<Soul> witnesses)
    {
        var witnessList = witnesses.Distinct().ToList();
        foreach (var rule in EmotionRules.Where(rule => environment.EffectMatches(rule.Effect, intent.Effect)))
        {
            foreach (var soul in Select(rule.Target, intent, witnessList))
            {
                soul.AdjustEmotion(rule.Emotion, rule.Delta, rule.Source, rule.Persistent);
            }
        }

        foreach (var rule in FatigueRules.Where(rule => environment.EffectMatches(rule.Effect, intent.Effect)))
        {
            foreach (var soul in Select(rule.Target, intent, witnessList)) soul.ChangeFatigue(rule.Delta);
        }

        foreach (var rule in BondRules.Where(rule => environment.EffectMatches(rule.Effect, intent.Effect)))
        {
            foreach (var soul in Select(rule.Target, intent, witnessList))
            {
                if (!ReferenceEquals(soul, intent.Mind)) intent.Mind.IncreaseBond(soul, rule.Delta);
            }
        }
    }

    /// <summary>由 Core 统一完成周期性情绪衰减。</summary>
    public IReadOnlyDictionary<Soul, Dictionary<Emotion, (float OldValue, float NewValue)>> SettleDaily(IEnumerable<Soul> souls) =>
        souls.Distinct().ToDictionary(
            soul => soul,
            soul => soul.SettleDailyEmotions(TransientEmotionDecay, PersistentEmotionDecay));

    private static IEnumerable<Soul> Select(MindTarget target, Intent intent, IReadOnlyList<Soul> witnesses) => target switch
    {
        MindTarget.ActorMind => [intent.Mind],
        MindTarget.TargetMind when intent.Target is Character character && character.Mind != null => [character.Mind],
        MindTarget.TargetMind when intent.Target is Soul soul => [soul],
        MindTarget.Witnesses => witnesses,
        _ => Array.Empty<Soul>()
    };
}
