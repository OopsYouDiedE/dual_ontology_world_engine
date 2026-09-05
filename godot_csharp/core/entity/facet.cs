using System;

namespace DualOntologyWorldEngine.Core;

/// <summary>
/// 表示依附于实体的轻量语义关系与功能切面事实。
/// </summary>
/// <remarks>
/// Facet 描述依附在宿主实体上的单一语义命题、定向关系、条件声明或能力切面。
/// 
/// 核心哲学与约束：
/// 1. 轻量性：Facet 自身不是世界实体（Entity），不继承 <see cref="TreeMember"/>，没有父子层级、槽位与独立生命周期。
/// 2. 对象引用：在表达关系时，直接通过 <see cref="Value"/> 持有目标实体对象引用，绝不额外分配或维护间接寻址 ID。
/// 3. 条件生效：可通过 <see cref="When"/> 谓词指定激活条件（如特定观察视界、持有光照或处于特定状态时才生效）。
/// 4. 来源溯源：通过 <see cref="Source"/> 记录赋予该关系的机制或实体，用于审计追踪与防止重复叠加。
/// 5. 实体晋升边界：若一段关系本身需要被独立破坏、转移、挂载子物品或单独到期流转，应将其提升为独立实体，而非使用 Facet。
/// </remarks>
public sealed class Facet
{
    /// <summary>
    /// 切面或关系的字符串键名。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 强类型语义关系类型。
    /// </summary>
    public FacetType Type { get; } = FacetType.Custom;

    /// <summary>
    /// 切面所持有的目标值（实体对象、表达式、规则或委托闭包等）。直接持有对象引用。
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// 若 Value 是 Entity，便捷强类型访问。
    /// </summary>
    public Entity? TargetEntity => Value as Entity;

    /// <summary>
    /// 关系或切面强度，数值越高表示联结越紧密或效力越强。
    /// </summary>
    public int Strength { get; }

    /// <summary>
    /// 关系的来源或赋予者，用于溯源与防止重复结算。
    /// </summary>
    public object? Source { get; }

    /// <summary>
    /// 条件激活判定谓词。签名为 (owner, context) => bool。
    /// </summary>
    public Func<Entity, System.Collections.Generic.IReadOnlyDictionary<string, object>?, bool>? When { get; }

    /// <summary>
    /// 初始化轻量切面事实。
    /// </summary>
    /// <param name="key">切面语义键名（如 "Owner"、"Claim"、"Reveal"、"Aspect" 或自定义命题）。</param>
    /// <param name="value">切面目标值。若表达实体关系，应直接传入目标 <see cref="Entity"/> 引用。</param>
    /// <param name="strength">切面强度或优先级权重，默认为 1。</param>
    /// <param name="when">条件激活判定委托。为 null 时表示无条件生效。</param>
    /// <param name="source">切面的建立来源机制或实体，用于审计与防重复叠加。</param>
    public Facet(
        string key,
        object value,
        int strength = 1,
        Func<Entity, System.Collections.Generic.IReadOnlyDictionary<string, object>?, bool>? when = null,
        object? source = null)
    {
        Key = key;
        Value = value;
        Strength = strength;
        When = when;
        Source = source;
        if (Enum.TryParse<FacetType>(key, true, out var parsed))
        {
            Type = parsed;
        }
    }

    /// <summary>
    /// 初始化强类型语义关系切面。
    /// </summary>
    /// <param name="type">强类型语义关系枚举类型（如 <see cref="FacetType.Owner"/>、<see cref="FacetType.Witness"/>）。</param>
    /// <param name="value">关系所指向的目标实体对象引用。</param>
    /// <param name="strength">关系强度数值，默认为 1。</param>
    /// <param name="source">关系的赋予来源对象。</param>
    public Facet(FacetType type, Entity value, int strength = 1, object? source = null)
        : this(type.ToString(), value, strength, null, source)
    {
        Type = type;
    }

    /// <summary>
    /// 生成切面的调试诊断字符串。
    /// </summary>
    /// <returns>包含键名、目标对象名称、强度与来源的文本表达。</returns>
    public override string ToString() =>
        $"Facet({Key} -> {(Value is Entity e ? e.Name : Value)}, Strength={Strength}, Source={Source})";
}
