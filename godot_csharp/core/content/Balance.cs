
namespace DualOntologyWorldEngine.Core.Content;

/// <summary>Core 默认数值档案。作品只在需要改变基调时覆盖这些值。</summary>
public enum PersonalityProfile
{
    Cautious,
    TruthSeeking,
    Guardian,
    Autonomous,
    Dignified
}

public static class Balance
{
    /// <summary>兼容旧调用的声明式后果预算。它只作未准备效果的粗略估计。</summary>
    public static double ConsequenceBudget(IEnumerable<DualOntologyWorldEngine.Core.Rules.Effect> effects) => effects.Sum(effect => effect.Op switch
    {
        "sequence" => ConsequenceBudget(effect.Args.OfType<DualOntologyWorldEngine.Core.Rules.Effect>()),
        "when" => ConsequenceBudget(effect.Args.OfType<DualOntologyWorldEngine.Core.Rules.Effect>()),
        "change" when effect.Args.Length > 2 && effect.Args[2] is IConvertible amount => Math.Max(1, Math.Abs(Convert.ToDouble(amount))),
        "require" => 0,
        _ => 1d
    });

    /// <summary>按已准备的实际状态变化计费；数值变化按绝对差额，其他非空变化按 1 Q，无变化为 0。</summary>
    public static double ConsequenceBudget(IEnumerable<DualOntologyWorldEngine.Core.Effects.StateChange> changes) =>
        changes.Sum(change =>
        {
            if (DualOntologyWorldEngine.Core.Effects.StateView.Same(change.Before, change.After)) return 0d;
            if (ReferenceEquals(change.Before, DualOntologyWorldEngine.Core.Effects.StateView.Unknown)
                || ReferenceEquals(change.After, DualOntologyWorldEngine.Core.Effects.StateView.Unknown)) return 0d;
            return IsNumber(change.Before) || IsNumber(change.After)
                ? Math.Abs((IsNumber(change.After) ? Convert.ToDouble(change.After) : 0d)
                    - (IsNumber(change.Before) ? Convert.ToDouble(change.Before) : 0d))
                : 1d;
        });

    private static bool IsNumber(object? value) => value is int or double or float or long or decimal;
    public const double BasicActionAv = 10.0;
    public const double BriefResponseAv = 5.0;
    public const double InvestigationAv = 20.0;
    public const double RitualAv = 30.0;
    public const double CandleMaintenanceIntervalAv = 20.0;
    public const double CandleExtinguishAv = 1.0;

    public const double DefaultFatigue = 0.0;
    public const double FatigueSoftLimit = 4.0;
    public const double FatigueHardLimit = 6.0;
    public const double FatigueRecoveryPerRest = 1.0;
    public const double OccultAnchorCapacity = 12.0;

    public const double TalismanCharges = 2.0;
    public const double SaltCharges = 3.0;
    public const double CandleFuel = 3.0;
    public const double BellCharges = 3.0;
    public const double MirrorIntegrity = 3.0;
    public const double RedThreadCharges = 2.0;
    public const double SealBoxCapacity = 2.0;
    public const double EffigyCharges = 2.0;
    public const double RecorderBattery = 6.0;
    public const double RecorderCapacity = 6.0;

    /// <summary>六类预期价值：安全、目标、关系、自主、求真、形象。</summary>
    public static IReadOnlyDictionary<PersonalityProfile, IReadOnlyDictionary<string, double>> PersonalityWeights { get; } =
        new Dictionary<PersonalityProfile, IReadOnlyDictionary<string, double>>
        {
            [PersonalityProfile.Cautious] = Weights(3, 2, 2, 2, 1, 1),
            [PersonalityProfile.TruthSeeking] = Weights(2, 2, 1, 2, 3, 1),
            [PersonalityProfile.Guardian] = Weights(2, 2, 3, 1, 1, 2),
            [PersonalityProfile.Autonomous] = Weights(2, 2, 1, 3, 2, 1),
            [PersonalityProfile.Dignified] = Weights(2, 2, 2, 1, 1, 3)
        };

    private static IReadOnlyDictionary<string, double> Weights(
        double safety, double goal, double relationship, double autonomy, double truth, double image) =>
        new Dictionary<string, double>
        {
            ["safety"] = safety,
            ["goal"] = goal,
            ["relationship"] = relationship,
            ["autonomy"] = autonomy,
            ["truth"] = truth,
            ["image"] = image
        };
}
