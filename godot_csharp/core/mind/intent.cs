using System;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Effects;

namespace DualOntologyWorldEngine.Core.Mind;

/// <summary>
/// 行动闭包的明确执行结果。它只描述动作本身，不替代运行时的规则审查结果。
/// </summary>
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

/// <summary>
/// 主体在当前环境下合法且能够立即执行的完整行动候选（意图）。
/// </summary>
/// <remarks>
/// Intent 是心智认知与客观物理世界交互的核心承载契约。
/// 
/// 核心职责：
/// 1. 意图封装：将行动发起者（<see cref="Actor"/>）、认知主体（<see cref="Mind"/>）、目标效果表达式（<see cref="Effect"/>）与预估行动值代价（<see cref="MotionCost"/>）打包。
/// 2. 两阶段审查：首先通过 <see cref="Validate"/> 进行客观前置条件自检；随后由统一动作运行时（<see cref="Timeline.ActionRuntime"/>）递交规则系统与言灵契约进行合法性审查。
/// 3. 闭包执行：当审查通过时，通过调用 <see cref="Resolve"/> 闭包产生客观状态差分与因果审计事件。
/// </remarks>
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
        : this(actor, mind, effect, motionCost, label: label)
    {
        Changes = changes.ToArray();
        Requirements = requirements?.ToArray() ?? [];
    }

    public EffectPlan Forecast(StateView? view = null) => Changes == null
        ? throw new InvalidOperationException("该动作尚未声明可推演的变化")
        : new EffectExecutor().Prepare(Changes, view, Requirements);
    /// <summary>执行该行动的物理身体角色实体。</summary>
    public Entity Actor { get; }

    /// <summary>发起与主导该行动的主观灵魂认知主体。</summary>
    public Soul Mind { get; }

    /// <summary>行动所意图施加的世界效果 AST 表达式。</summary>
    public Effect Effect { get; }

    /// <summary>执行此行动预期消耗的行动值（Action Value / Motion Cost）。</summary>
    public double MotionCost { get; }

    /// <summary>动作实际执行并产生副作用的结算闭包委托。</summary>
    public Func<object?> Resolve { get; }

    /// <summary>以明确成功语义执行动作闭包。</summary>
    public EffectResult Execute() => EffectResult.FromLegacy(Resolve());

    /// <summary>用于 UI 呈现或日志追踪的行动友好描述标签。</summary>
    public string Label { get; }

    /// <summary>行动的客观前置条件自检谓词。返回 false 表示无法发起。</summary>
    public Func<bool> Validate { get; }

    /// <summary>行动所体现的情境社交应对模式。</summary>
    public ResponseMode? ResponseMode { get; }

    /// <summary>
    /// 行动的主要目标实体。
    /// 规范效果形状为 op(actor, target?, tool?, ...)，但也兼容省略 actor 的低层效果。
    /// </summary>
    public Entity? Target => Effect.Args switch
    {
        [Entity first, Entity second, ..] when ReferenceEquals(first, Actor) => second,
        [Entity first, ..] when !ReferenceEquals(first, Actor) => first,
        _ => null
    };

    /// <summary>
    /// 行动使用的工具实体。
    /// 仅在效果参数遵循 op(actor, target, tool, ...) 或 op(target, tool, ...) 时返回。
    /// </summary>
    public Entity? Tool => Effect.Args switch
    {
        [Entity first, Entity _, Entity third, ..] when ReferenceEquals(first, Actor) => third,
        [Entity first, Entity second, ..] when !ReferenceEquals(first, Actor) => second,
        _ => null
    };

    /// <summary>
    /// 初始化一个完备的行动意图实例。
    /// </summary>
    /// <param name="actor">行动身体主体。</param>
    /// <param name="mind">决策认知心智。</param>
    /// <param name="effect">目标效果表达式。</param>
    /// <param name="motionCost">消耗的行动值代价，默认为 1.0。</param>
    /// <param name="resolve">执行副作用的结算委托。为 null 时默认不产生返回值。</param>
    /// <param name="label">行动的展示名称。</param>
    /// <param name="validate">前置条件自检谓词。默认为始终允许。</param>
    /// <param name="responseMode">社交态度应对模式。</param>
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
        Resolve = resolve ?? (() => null);
        Label = string.IsNullOrEmpty(label) ? effect.Op : label;
        Validate = validate ?? (() => true);
        ResponseMode = responseMode;
    }

    /// <summary>
    /// 初始化行动意图实例（无返回值 Action 结算委托重载）。
    /// </summary>
    /// <param name="actor">行动身体主体。</param>
    /// <param name="mind">决策认知心智。</param>
    /// <param name="effect">目标效果表达式。</param>
    /// <param name="motionCost">行动值代价。</param>
    /// <param name="resolve">无返回值的执行委托。</param>
    /// <param name="label">行动标签。</param>
    /// <param name="validate">前置条件自检谓词。</param>
    /// <param name="responseMode">社交应对模式。</param>
    public Intent(
        Entity actor,
        Soul mind,
        Effect effect,
        double motionCost,
        Action resolve,
        string label = "",
        Func<bool>? validate = null,
        ResponseMode? responseMode = null)
        : this(actor, mind, effect, motionCost, () => { resolve(); return null; }, label, validate, responseMode)
    {
    }

    /// <summary>
    /// 生成行动意图的调试诊断字符串。
    /// </summary>
    /// <returns>格式化后的操作原语、标签与行动代价文本。</returns>
    public override string ToString() => $"[{Effect.Op}] {Label} (Cost: {MotionCost})";
}
