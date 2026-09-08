using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Rules;

namespace DualOntologyWorldEngine.Core.Mind;

#region 行动意图 (Intent)

/// <summary>行动闭包的明确执行结果；运行时审查结果仍由 ActionResolution 表示。</summary>
public sealed record EffectResult(bool Succeeded, object? Outcome = null, string Reason = "")
{
    /// <summary>将既有动作闭包的返回值归一为明确结果。</summary>
    public static EffectResult FromLegacy(object? value) => value switch
    {
        EffectResult result => result,
        false => new EffectResult(false, null, "动作未完成"),
        _ => new EffectResult(true, value)
    };
}

/// <summary>主体在当前环境下可发起的行动候选，承载声明式变化或历史闭包。</summary>
public class Intent
{
    public IReadOnlyList<Effect>? Changes { get; }
    public IReadOnlyList<Expr> Requirements { get; } = [];
    public Entity? Definition { get; init; }
    public string? Speech { get; init; }
    public Expr? Claim { get; init; }
    public bool IsPrivate { get; init; }

    public Intent(Entity actor, Soul mind, Effect effect, IEnumerable<Effect> changes,
        double motionCost = 10, string label = "", IEnumerable<Expr>? requirements = null,
        Func<bool>? validate = null)
        : this(actor, mind, effect, motionCost, resolve: null, label: label, validate: validate)
    {
        Changes = changes.ToArray();
        Requirements = requirements?.ToArray() ?? [];
    }

    /// <summary>执行该行动的物理身体角色实体。</summary>
    public Entity Actor { get; }

    /// <summary>发起与主导该行动的主观灵魂认知主体。</summary>
    public Soul Mind { get; }

    /// <summary>行动所意图施加的世界效果 AST 表达式。</summary>
    public Effect Effect { get; }

    /// <summary>执行此行动预期消耗的行动值（Action Value / Motion Cost）。</summary>
    public double MotionCost { get; }

    /// <summary>历史闭包式行动的结算委托；声明式行动为 null。</summary>
    public Func<object?>? Resolve { get; }

    /// <summary>执行历史闭包；声明式行动没有闭包时视为成功。</summary>
    public EffectResult Execute() => Resolve == null
        ? new EffectResult(true)
        : EffectResult.FromLegacy(Resolve());

    /// <summary>用于 UI 呈现或日志追踪的行动友好描述标签。</summary>
    public string Label { get; }

    /// <summary>行动的客观前置条件自检谓词。返回 false 表示无法发起。</summary>
    public Func<bool> Validate { get; }

    /// <summary>行动所体现的情境社交应对模式。</summary>
    public ResponseMode? ResponseMode { get; }

    /// <summary>按规范效果形状提取主要目标，兼容省略 actor 的低层效果。</summary>
    public Entity? Target => Effect.Args switch
    {
        [Entity first, Entity second, ..] when ReferenceEquals(first, Actor) => second,
        [Entity first, ..] when !ReferenceEquals(first, Actor) => first,
        _ => null
    };

    /// <summary>按规范效果形状提取工具实体。</summary>
    public Entity? Tool => Effect.Args switch
    {
        [Entity first, Entity _, Entity third, ..] when ReferenceEquals(first, Actor) => third,
        [Entity first, Entity second, ..] when !ReferenceEquals(first, Actor) => second,
        _ => null
    };

    /// <summary>创建历史闭包式行动。</summary>
    public Intent(
        Entity actor,
        Soul mind,
        Effect effect,
        double motionCost = 1.0,
        Func<object?>? resolve = null,
        string label = "",
        Func<bool>? validate = null,
        ResponseMode? responseMode = null)
    {
        Actor = actor;
        Mind = mind;
        Effect = effect;
        MotionCost = motionCost;
        Resolve = resolve;
        Label = string.IsNullOrEmpty(label) ? effect.Op : label;
        Validate = validate ?? (() => true);
        ResponseMode = responseMode;
    }

    /// <summary>生成行动意图的调试诊断字符串。</summary>
    public override string ToString() => $"[{Effect.Op}] {Label} (Cost: {MotionCost})";
}

#endregion

#region 决策裁决与动机压力 (Decision)

/// <summary>单条规则规范对意图产生的规范压力记录。</summary>
/// <param name="Amount">规范压力数值（正数表示鼓励或必须履行，负数表示禁止或违反惩戒）。</param>
/// <param name="Belief">施加该规范压力的主体已采信规则信念实体（RuleBelief）。</param>
/// <param name="Directive">匹配触发的具体规约指令对象（Mode + Effect）。</param>
/// <param name="Matched">候选意图效果是否在当前求值环境中命中了指令模式。</param>
public record RulePressure(double Amount, Entity Belief, Directive Directive, bool Matched);

/// <summary>单条人格特质、社交态度或情境偏好产生的动机偏好压力记录。</summary>
/// <param name="Amount">偏好动机得分（正数代表正向激励与执行倾向，负数代表抗拒或心理不适）。</param>
/// <param name="Source">动机来源对象或文本描述（如人格标签、社交态度模式或特定情绪源）。</param>
public record PreferencePressure(double Amount, object Source);

/// <summary>规则系统与心智对候选行动意图的综合评价与决策裁决结果。</summary>
/// <remarks>
/// Decision 是主观认知决策管线的产出。
/// 
/// 结算模型：
/// 1. 规范合规性（<see cref="Complies"/>）：只要存在命中 Forbid 模式或未满足 Require 模式的刚性规则，即视为违规。
/// 2. 效用综合评分（<see cref="Score"/>）：聚合所有规则压力（<see cref="Pressures"/>）与人格社交偏好（<see cref="Preferences"/>）的总和。
/// 3. 主导依据（<see cref="Basis"/>）与理由（<see cref="Reason"/>）：提取绝对权重大、影响力最强的核心信念实体与规约模式作为向外部汇报的理由。
/// </remarks>
public sealed class Decision(
    Effect intent, bool complies, double score, Entity? basis, string reason,
    Intent? action = null, IEnumerable<RulePressure>? pressures = null,
    IEnumerable<PreferencePressure>? preferences = null,
    double repetitionPenalty = 0, double commitmentInertia = 0)
{
    public Effect Intent { get; } = intent;
    public bool Complies { get; } = complies;
    public double Score { get; } = score;
    public Entity? Basis { get; } = basis;
    public string Reason { get; } = reason;
    public Intent? Action { get; } = action;
    public IReadOnlyList<RulePressure> Pressures { get; } = pressures?.ToArray() ?? [];
    public IReadOnlyList<PreferencePressure> Preferences { get; } = preferences?.ToArray() ?? [];
    public double RepetitionPenalty { get; init; } = repetitionPenalty;
    public double CommitmentInertia { get; init; } = commitmentInertia;

    /// <summary>生成决策结果的调试诊断字符串。</summary>
    public override string ToString() =>
        $"Decision: [{Intent.Op}] Complies={Complies}, Score={Score:F2}, Reason='{Reason}'";
}

#endregion
