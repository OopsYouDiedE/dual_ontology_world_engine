using System;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Effects;

namespace DualOntologyWorldEngine.Core.Mind;

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
        double motionCost = 10, string label = "", IEnumerable<Expr>? requirements = null)
        : this(actor, mind, effect, motionCost, resolve: null, label: label)
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
