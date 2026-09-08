namespace DualOntologyWorldEngine.Core.Mind;

/// <summary>效用决策优先级分桶枚举。</summary>
public enum UtilityBucket
{
    /// <summary>空闲桶：游荡、观察、整理等低效用行为。</summary>
    Idle = 0,

    /// <summary>常规动机桶：工作、社交、娱乐、休息等日常需求。</summary>
    Standard = 1,

    /// <summary>危机桶：生命垂危、极度饥饿等生死攸关行为。存在可用行动时独占优先权。</summary>
    Critical = 2
}

public readonly record struct Consideration
{
    public double Utility { get; }
    public double Weight { get; }

    public Consideration(double utility, double weight = 1.0)
    {
        Utility = Math.Clamp(utility, 0.0, 1.0);
        Weight = Math.Clamp(weight, 0.0, 1.0);
    }
}

/// <summary>标准智能体效用决策（Utility AI）数学模型与响应曲线库。</summary>
public static class UtilityMath
{
    /// <summary>将原始物理量 x 线性映射归一化至 [0, 1] 区间。</summary>
    public static double Normalize(double x, double min, double max)
    {
        if (Math.Abs(max - min) < 1e-9) return 0.0;
        return Math.Clamp((x - min) / (max - min), 0.0, 1.0);
    }

    /// <summary>需求动机时间线性衰减：M_t = M_0 - λ * Δt。</summary>
    public static double DecayMotive(double initial, double lambda, double deltaT, double min = 0.0)
    {
        return Math.Max(min, initial - lambda * deltaT);
    }

    #region 响应曲线模型 (Response Curves)

    /// <summary>线性响应曲线：u(x) = clamp(m * (x - c) + k, 0, 1)。</summary>
    public static double LinearCurve(double x, double m = 1.0, double c = 0.0, double k = 0.0)
    {
        var val = m * (x - c) + k;
        return Math.Clamp(val, 0.0, 1.0);
    }

    /// <summary>多项式/幂函数曲线：u(x) = clamp(m * (x - c)^k + b, 0, 1)。</summary>
    public static double PolynomialCurve(double x, double m = 1.0, double c = 0.0, double k = 2.0, double b = 0.0)
    {
        var diff = Math.Max(0.0, x - c);
        var val = m * Math.Pow(diff, k) + b;
        return Math.Clamp(val, 0.0, 1.0);
    }

    /// <summary>Logistic (S型) 响应曲线：u(x) = clamp(1 / (1 + e^(-k * (x - x0))), 0, 1)。</summary>
    public static double LogisticCurve(double x, double k = 10.0, double x0 = 0.5)
    {
        var exponent = -k * (x - x0);
        if (exponent > 40.0) return 0.0;
        if (exponent < -40.0) return 1.0;
        var val = 1.0 / (1.0 + Math.Exp(exponent));
        return Math.Clamp(val, 0.0, 1.0);
    }

    /// <summary>分段线性样条响应曲线：依据关键控制点进行线性插值。</summary>
    /// <param name="x">输入归一化量 [0, 1]。</param>
    /// <param name="controlPoints">已排序的 (输入x, 输出u) 控制点列表。</param>
    public static double PiecewiseLinearCurve(double x, IReadOnlyList<(double X, double Y)> controlPoints)
    {
        if (controlPoints.Count == 0) return 0.0;
        if (x <= controlPoints[0].X) return Math.Clamp(controlPoints[0].Y, 0.0, 1.0);
        if (x >= controlPoints[^1].X) return Math.Clamp(controlPoints[^1].Y, 0.0, 1.0);

        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            var p0 = controlPoints[i];
            var p1 = controlPoints[i + 1];
            if (x >= p0.X && x <= p1.X)
            {
                var span = p1.X - p0.X;
                if (span < 1e-9) return Math.Clamp(p0.Y, 0.0, 1.0);
                var t = (x - p0.X) / span;
                var val = p0.Y + t * (p1.Y - p0.Y);
                return Math.Clamp(val, 0.0, 1.0);
            }
        }
        return Math.Clamp(controlPoints[^1].Y, 0.0, 1.0);
    }

    #endregion

    #region 效用聚合算法 (Utility Aggregation)

    /// <summary>
    /// 带权乘法模型聚合多维度考虑因素：U = ∏ [ (1 - w_i) + w_i * u_i ]。
    /// 若某项关键因素不满足（u_k = 0 且 w_k = 1），总效用直接归零。
    /// </summary>
    public static double AggregateMultiplicative(IEnumerable<Consideration> considerations)
    {
        double u = 1.0;
        bool hasAny = false;
        foreach (var c in considerations)
        {
            hasAny = true;
            var factor = (1.0 - c.Weight) + c.Weight * c.Utility;
            u *= factor;
            if (u <= 0.0) return 0.0;
        }
        return hasAny ? Math.Clamp(u, 0.0, 1.0) : 0.0;
    }

    #endregion
}

/// <summary>带优先级分桶的候选效用评价包装项。</summary>
public record ScoredOption<T>(
    T Option,
    double UtilityScore,
    UtilityBucket Bucket = UtilityBucket.Standard);

/// <summary>分桶选择与惯性仲裁器。</summary>
public static class UtilityArbiter
{
    /// <summary>
    /// 从多项分桶候选中筛选最优选项。
    /// 遵循危机分桶优先规则（若存在可用 Critical 选项则忽略次级分桶）。
    /// </summary>
    public static ScoredOption<T>? SelectBest<T>(
        IEnumerable<ScoredOption<T>> candidates,
        T? currentExecuting = default,
        double momentumBonus = 0.1,
        double switchPenalty = 0.05)
    {
        var list = candidates.ToList();
        if (list.Count == 0) return null;

        // 最高可用分桶
        var highestBucket = list.Max(c => c.Bucket);
        var inBucket = list.Where(c => c.Bucket == highestBucket).ToList();

        ScoredOption<T>? best = null;
        double bestEffective = double.NegativeInfinity;

        foreach (var item in inBucket)
        {
            var effective = item.UtilityScore;
            if (currentExecuting != null && Equals(item.Option, currentExecuting))
            {
                effective += momentumBonus;
            }
            else if (currentExecuting != null)
            {
                effective -= switchPenalty;
            }

            if (best == null || effective > bestEffective)
            {
                best = item;
                bestEffective = effective;
            }
        }

        return best;
    }
}
