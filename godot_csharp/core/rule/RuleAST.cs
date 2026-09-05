using System;
using System.Collections.Generic;
using System.Linq;

namespace DualOntologyWorldEngine.Core.Rules;

/// <summary>
/// 可匹配、可求值的事实表达式 AST。
/// </summary>
/// <remarks>
/// Expr 是规则系统中表示环境事实、命题状态与条件谓词的纯数据抽象语法树节点。
/// 
/// 特性：
/// 1. 不区分大小写匹配操作符（Op）；
/// 2. 参数支持直接持有运行时 Entity 引用、字面量值或递归嵌套 Expr。
/// </remarks>
/// <param name="Op">命题谓词操作符（如 "state"、"tag_at_least"、"all"、"not" 等）。</param>
/// <param name="Args">谓词参数列表（支持嵌套表达式、实体引用或字符串标量）。</param>
public record Expr(string Op, params object[] Args)
{
    public virtual bool Equals(Expr? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        if (!string.Equals(Op, other.Op, StringComparison.OrdinalIgnoreCase)) return false;
        if (Args.Length != other.Args.Length) return false;
        for (int i = 0; i < Args.Length; i++)
        {
            if (!Equals(Args[i], other.Args[i])) return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Op, StringComparer.OrdinalIgnoreCase);
        foreach (var arg in Args) hash.Add(arg);
        return hash.ToHashCode();
    }
}

/// <summary>
/// 等待已注册执行路径解释的世界效果 AST。
/// </summary>
/// <remarks>
/// Effect 描述一次行动意图、契约产出或环境变迁所期望施加的客观效果。
/// 既可作为待执行动作的原语，也可作为规则中 Require/Forbid/Permit 匹配的目标模式。
/// </remarks>
/// <param name="Op">效果操作动作原语（如 "move"、"inspect"、"operate"、"sequence" 等）。</param>
/// <param name="Args">效果作用的目标实体、工具或环境参数。</param>
public record Effect(string Op, params object[] Args)
{
    public virtual bool Equals(Effect? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        if (!string.Equals(Op, other.Op, StringComparison.OrdinalIgnoreCase)) return false;
        if (Args.Length != other.Args.Length) return false;
        for (int i = 0; i < Args.Length; i++)
        {
            if (!Equals(Args[i], other.Args[i])) return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Op, StringComparer.OrdinalIgnoreCase);
        foreach (var arg in Args) hash.Add(arg);
        return hash.ToHashCode();
    }
}

/// <summary>
/// 快捷构造事实表达式与待执行效果的辅助工厂。
/// </summary>
public static class AST
{
    /// <summary>快捷创建事实表达式 Expr 节点。</summary>
    public static Expr E(string op, params object[] args) => new(op, args);

    /// <summary>快捷创建世界效果 Effect 节点。</summary>
    public static Effect F(string op, params object[] args) => new(op, args);
}

/// <summary>
/// 规则模板中由当前行动上下文绑定的强类型槽位。
/// </summary>
/// <remarks>
/// 固定的场景对象仍直接保存 Entity 引用；只有随每个 Intent 改变的位置使用槽位。
/// 这避免以 null 或裸字符串冒充通配符，并让编译器检查模板参数。
/// </remarks>
public enum RuleSlot
{
    /// <summary>当前行动主体。</summary>
    Actor,

    /// <summary>当前行动的主要目标。</summary>
    Target,

    /// <summary>当前行动使用的工具。</summary>
    Tool,

    /// <summary>当前行动发生的场景。</summary>
    Scene
}

/// <summary>
/// 规则指令施加的规范模式。
/// </summary>
/// <remarks>
/// Mode 描述一条规范指令对主体意图的约束类型与规约力强度。
/// 规则系统通过区分不同模式计算合规性（Complies）与规约压力得分（Score）。
/// </remarks>
public enum Mode
{
    /// <summary>
    /// 强制性规约要求。
    /// 表示主体在满足规则条件时必须履行指定效果，未履行将产生严重负向违约压力或被规则硬性拦截。
    /// </summary>
    Require,

    /// <summary>
    /// 严禁性规约禁止。
    /// 表示主体被严格禁止执行指定效果，一旦触犯将产生极大负向压力或直接判定违规失败。
    /// </summary>
    Forbid,

    /// <summary>
    /// 许可性权能授予。
    /// 表示解除默认限制或赋予主体采取指定行动的正当性，贡献温和的正向动机压力。
    /// </summary>
    Permit,

    /// <summary>
    /// 倡导性偏好引导。
    /// 表示倡导或鼓励主体优先选择指定行动，不具备刚性强制力，产生正向激励偏好分。
    /// </summary>
    Prefer
}

/// <summary>
/// 规范模式与目标效果指令。
/// </summary>
/// <param name="Mode">规约模式（Require/Forbid/Permit/Prefer）。</param>
/// <param name="Effect">目标世界效果 AST 表达式或匹配模式。</param>
public record Directive(Mode Mode, Effect Effect);

/// <summary>
/// 仅在 condition 成立时参与评价的一组指令集合。
/// </summary>
/// <param name="Condition">触发评价的前置条件表达式 AST。</param>
/// <param name="Directives">条件成立时生效的一组规范指令。</param>
/// <param name="Source">规则条文来源、自然语言说明或法条标题。</param>
public record Rule(Expr Condition, IReadOnlyList<Directive> Directives, string Source = "");

/// <summary>
/// Soul 对规则发布物的认知立场阶段。
/// </summary>
/// <remarks>
/// 描述外部条文进入主体心智后的内化推进过程。
/// 只有跃迁至 Accepted 状态的规则，才会真正在 Soul 决策评估中施加规约压力。
/// </remarks>
public enum CognitiveStage
{
    /// <summary>
    /// 已接收。
    /// 表示主体已通过视听感官获取该规则条文，但尚未进行深入语义解析与合理性裁决。
    /// </summary>
    Received,

    /// <summary>
    /// 已理解。
    /// 表示主体已掌握规则的触发条件与规约内涵，但尚未决定是否服从或采信该规则。
    /// </summary>
    Understood,

    /// <summary>
    /// 已采信。
    /// 表示主体已将该规则纳入自身信念体系，在自主决策时主动受其约束。
    /// </summary>
    Accepted,

    /// <summary>
    /// 已拒绝。
    /// 表示主体审视后明确否定该规则的效力、合理性或权威，主观上拒绝受其约束。
    /// </summary>
    Rejected
}

/// <summary>
/// 通用反应与社交应对模式种类。
/// </summary>
/// <remarks>
/// ResponseMode 用于在复杂群体交互情境中规范主体的行为倾向与态度表达。
/// </remarks>
public enum ResponseMode
{
    /// <summary>
    /// 核验模式。
    /// 采取查证物证、质疑逻辑与反向推导的审慎核查策略。
    /// </summary>
    Verify,

    /// <summary>
    /// 协商模式。
    /// 采取对话沟通、交换条件与利益博弈的协调策略。
    /// </summary>
    Negotiate,

    /// <summary>
    /// 协作模式。
    /// 采取积极配合、信任协同与合力推进的伙伴策略。
    /// </summary>
    Cooperate,

    /// <summary>
    /// 压制模式。
    /// 采取限制行动、物理阻断或强制执行的对抗性约束策略。
    /// </summary>
    Restrain,

    /// <summary>
    /// 庇护模式。
    /// 采取防范风险、遮蔽伤害与守护目标实体安全的防卫策略。
    /// </summary>
    Protect,

    /// <summary>
    /// 回避模式。
    /// 采取退避三舍、拉开距离与脱离接触的消极防范策略。
    /// </summary>
    Avoid,

    /// <summary>
    /// 隐匿模式。
    /// 采取掩盖痕迹、封锁信息与伪装意图的保密策略。
    /// </summary>
    Conceal
}

/// <summary>
/// 规则、客观补丁与因果审计事件的实体构造工厂。
/// </summary>
public static class RuleEntityFactory
{
    /// <summary>
    /// 建立一份真假未定的主观规则发布物实体（RulePublication）。
    /// </summary>
    /// <param name="rule">发布的抽象规则对象。</param>
    /// <param name="issuer">发布该规则的主体实体引用。</param>
    /// <param name="guaranteeTag">用于背书该规则的担保或权威标签名（默认为 "Guarantee"）。</param>
    /// <param name="carrier">规则载体实体（如布告栏、信笺、终端等）。</param>
    /// <param name="scope">规则适用的空间辖域或场景实体。</param>
    /// <param name="version">规则版本标识。</param>
    /// <returns>挂载了 IssuedBy 与 Domain 关系的规则发布物实体。</returns>
    public static Entity CreateRulePublication(
        Rule rule,
        Entity issuer,
        string guaranteeTag = "Guarantee",
        Entity? carrier = null,
        Entity? scope = null,
        object? version = null)
    {
        var pub = new Entity(
            $"{issuer.Name}发布的规则",
            carrier,
            new Dictionary<string, int> { ["RulePublication"] = 1, ["Information"] = 1 },
            properties: new Dictionary<string, object>
            {
                ["rule"] = rule,
                ["issuer"] = issuer,
                ["carrier"] = carrier!,
                ["scope"] = scope!,
                ["version"] = version!,
                ["guarantee_tag"] = guaranteeTag
            });
        pub.Relate(FacetType.IssuedBy, issuer);
        if (scope != null)
        {
            pub.Relate(FacetType.Domain, scope);
        }
        return pub;
    }

    /// <summary>
    /// 创建约束现实世界执行路径的客观规则补丁实体（RulePatch）。
    /// </summary>
    /// <remarks>
    /// RulePatch 代表客观世界中不以主体意志为转移的物理、超自然或系统法则。
    /// 当主体行动效果命中补丁中的 Forbid 指令时，将被运行时直接拦截阻断。
    /// </remarks>
    /// <param name="name">补丁调试名称。</param>
    /// <param name="rule">客观硬性规则定义。</param>
    /// <param name="parent">所属世界树父节点。</param>
    /// <param name="source">补丁的生成来源机制或实体。</param>
    /// <param name="scope">补丁生效的辖域空间范围。</param>
    /// <param name="priority">补丁仲裁优先级数值。</param>
    /// <param name="tags">附加标签字典。</param>
    /// <returns>标记为 RulePatch=1 与 Truth=1 的客观规则补丁实体。</returns>
    public static Entity CreateRulePatch(
        string name,
        Rule rule,
        TreeMember? parent = null,
        Entity? source = null,
        Entity? scope = null,
        int priority = 0,
        IDictionary<string, int>? tags = null)
    {
        var patchTags = new Dictionary<string, int> { ["RulePatch"] = 1, ["Truth"] = 1 };
        if (tags != null)
        {
            foreach (var (k, v) in tags) patchTags[k] = v;
        }

        var patch = new Entity(
            name,
            parent,
            patchTags,
            properties: new Dictionary<string, object>
            {
                ["rule"] = rule,
                ["scope"] = scope!,
                ["priority"] = priority,
                ["active"] = true
            });

        if (source != null)
        {
            patch.Relate(FacetType.Source, source);
        }
        if (scope != null)
        {
            patch.Relate(FacetType.Domain, scope);
        }
        return patch;
    }

    /// <summary>
    /// 创建记录因果变迁与动作结果的历史审计事件实体（Event）。
    /// </summary>
    /// <param name="name">事件描述名称。</param>
    /// <param name="actor">发起该事件的行动主体实体。</param>
    /// <param name="effect">触发的世界效果表达式 AST。</param>
    /// <param name="parent">世界树挂载父节点（通常为世界根或事件归档容器）。</param>
    /// <param name="outcome">行动执行产出的结果对象。</param>
    /// <param name="time">发生该事件的行动值时刻。</param>
    /// <param name="cause">导致该事件发生的前置致因实体或描述。</param>
    /// <param name="tags">附加事件标签。</param>
    /// <returns>挂载了 Actor 与 CausedBy 关系的审计事件实体。</returns>
    public static Entity CreateEvent(
        string name,
        Entity? actor,
        Effect effect,
        TreeMember? parent = null,
        object? outcome = null,
        double time = 0,
        object? cause = null,
        IDictionary<string, int>? tags = null)
    {
        var eventTags = new Dictionary<string, int> { ["Event"] = 1 };
        if (tags != null)
        {
            foreach (var (k, v) in tags) eventTags[k] = v;
        }

        var ev = new Entity(
            name,
            parent,
            eventTags,
            properties: new Dictionary<string, object>
            {
                ["actor"] = actor!,
                ["effect"] = effect,
                ["outcome"] = outcome!,
                ["time"] = time,
                ["cause"] = cause!
            });

        if (actor != null)
        {
            ev.Relate(FacetType.Actor, actor);
        }
        if (cause is Entity causeEntity)
        {
            ev.Relate(FacetType.CausedBy, causeEntity);
        }
        return ev;
    }
}
