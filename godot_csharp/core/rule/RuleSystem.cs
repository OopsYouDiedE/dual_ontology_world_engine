using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Timeline;

namespace DualOntologyWorldEngine.Core.Rules;

/// <summary>规则推理与规约评价引擎。</summary>
/// <remarks>
/// RuleSystem 负责双重维度的规则结算：
/// 1. 主观规范评价（<see cref="Evaluate"/>）：针对 Soul 的已采信信念（Beliefs），计算 Require、Forbid、Permit、Prefer 模式对行动意图施加的规范压力与合规性；
/// 2. 客观补丁审查（<see cref="PatchAllows"/>）：针对世界中生效的物理或超自然法则补丁（RulePatch），进行不可违抗的刚性拦截判定。
/// </remarks>
public class RuleSystem
{
    /// <summary>递归展开嵌套包含条件（when）的效果指令。</summary>
    private IEnumerable<Directive> Expanded(Rule rule, RuleEnvironment env)
    {
        foreach (var directive in rule.Directives)
        {
            if (string.Equals(directive.Effect.Op, "when", StringComparison.OrdinalIgnoreCase) && directive.Effect.Args.Length == 2)
            {
                if (directive.Effect.Args[0] is Expr condition && directive.Effect.Args[1] is Effect nested)
                {
                    if (env.Evaluate(condition))
                    {
                        yield return new Directive(directive.Mode, nested);
                    }
                    continue;
                }
            }
            yield return directive;
        }
    }

    /// <summary>计算特定规则信念在心智体系中的综合权威与执行优先级权重。</summary>
    /// <remarks>
    /// 权重积分公式：
    /// Priority = 主观信任度(confidence) + 发布者担保强度(guarantee) * 2 + 发布者固有权威(Authority)。
    /// </remarks>
    /// <param name="belief">主体的规则信念实体。</param>
    /// <returns>综合优先级整数分值。</returns>
    public static int BeliefPriority(Entity belief)
    {
        var pub = belief.Get<Entity>("publication");
        var issuer = belief.Get<Entity>("issuer") ?? pub?.Issuer();
        var guarantee = pub?.GuaranteeTag() ?? "Guarantee";
        var confidence = belief.Get<int>("confidence", 1);
        var authority = issuer != null ? issuer.Tag("Authority") : 0;
        var guaranteeVal = issuer != null ? issuer.Tag(guarantee) : 0;

        return confidence + guaranteeVal * 2 + authority;
    }

    /// <summary>提取指定 Soul 当前已采信（Accepted）且触发条件在环境中求值为真的所有活跃规则指令。</summary>
    /// <param name="soul">认知主体。</param>
    /// <param name="env">当前认知求值环境。</param>
    /// <returns>元组序列，包含所属信念实体与展开后的规范指令。</returns>
    public IEnumerable<(Entity Belief, Directive Directive)> Active(Soul soul, RuleEnvironment env) =>
        Active(soul, env, null);

    /// <summary>在当前行动的强类型槽位绑定下提取活跃规则。</summary>
    /// <param name="soul">接受规则的认知主体。</param>
    /// <param name="env">当前认知求值环境。</param>
    /// <param name="bindings">Actor、Target、Tool 与 Scene 的局部对象绑定。</param>
    /// <returns>条件成立且已经具象化的规则指令。</returns>
    private IEnumerable<(Entity Belief, Directive Directive)> Active(
        Soul soul,
        RuleEnvironment env,
        IReadOnlyDictionary<string, object>? bindings)
    {
        foreach (var belief in soul.Beliefs)
        {
            var rule = belief.Get<Rule>("rule");
            if (rule == null || !belief.HasTag("Accepted")) continue;

            var materialized = bindings == null ? rule : env.Materialize(rule, bindings);
            if (!env.Evaluate(materialized.Condition)) continue;

            foreach (var directive in Expanded(materialized, env))
            {
                yield return (belief, directive);
            }
        }
    }

    /// <summary>依据主体已采信规则，对指定候选效果意图进行合规性判定与规约压力积分。</summary>
    /// <remarks>
    /// 评分贡献规则：
    /// - Require 命中：+100 + Priority
    /// - Require 未命中：-80 - Priority（并标记不合规）
    /// - Forbid 命中：-100 - Priority（并标记不合规）
    /// - Prefer 命中：+50 + Priority
    /// - Permit 命中：+20 + Priority
    /// </remarks>
    /// <param name="soul">评价主体。</param>
    /// <param name="intent">待评价效果 AST。</param>
    /// <param name="env">求值环境。</param>
    /// <returns>封装了合规状态、得分、主导依据与全部压力明细的 Decision 实例。</returns>
    public Decision Evaluate(Soul soul, Effect intent, RuleEnvironment env) =>
        Evaluate(intent, env, Active(soul, env));

    /// <summary>使用完整行动对象评价规则，使模板槽位能够由实际人物、目标、工具和场景绑定。</summary>
    /// <remarks>
    /// 场景对象优先取调用者提供的 context.Scene，否则从 Character.CurrentScene 推导。
    /// 未出现的可选目标或工具不写入绑定，含该槽位的规则自然不会错误匹配。
    /// </remarks>
    /// <param name="soul">进行评价的认知主体。</param>
    /// <param name="intent">完整可执行行动候选。</param>
    /// <param name="env">当前认知求值环境。</param>
    /// <param name="context">可选的局部场景与附加上下文。</param>
    /// <returns>包含全部规则压力明细的决策评价。</returns>
    public Decision Evaluate(
        Soul soul,
        Intent intent,
        RuleEnvironment env,
        ActionContext? context = null)
    {
        return Evaluate(intent.Effect, env, Active(soul, env, Bindings(intent, context)), intent);
    }

    public Decision Evaluate(
        Soul soul,
        Intent intent,
        RuleEnvironment env,
        IReadOnlyDictionary<string, object>? context) =>
        Evaluate(soul, intent, env, ActionContext.FromDictionary(context));

    private static Dictionary<string, object> Bindings(Intent intent, ActionContext? context)
    {
        var bindings = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(RuleSlot.Actor)] = intent.Actor
        };
        if (intent.Target is { } target) bindings[nameof(RuleSlot.Target)] = target;
        if (intent.Tool is { } tool) bindings[nameof(RuleSlot.Tool)] = tool;
        if ((context?.Scene ?? TopologySystem.GetEnclosingScene(intent.Actor)) is { } scene)
            bindings[nameof(RuleSlot.Scene)] = scene;
        return bindings;
    }

    private static Decision Evaluate(Effect effect, RuleEnvironment env,
        IEnumerable<(Entity Belief, Directive Directive)> active, Intent? action = null)
    {
        var pressures = new List<RulePressure>();
        foreach (var (belief, directive) in active)
        {
            var matched = env.EffectMatches(directive.Effect, effect);
            var priority = BeliefPriority(belief);
            var contribution = (directive.Mode, matched) switch
            {
                (Mode.Require, true) => 100 + priority,
                (Mode.Require, false) => -80 - priority,
                (Mode.Prefer, true) => 50 + priority,
                (Mode.Permit, true) => 20 + priority,
                (Mode.Forbid, true) => -100 - priority,
                _ => 0
            };
            if (contribution != 0)
            {
                pressures.Add(new RulePressure(contribution, belief, directive, matched));
            }
        }

        if (pressures.Count == 0)
        {
            return new Decision(effect, true, 0, null, "无相关已接受规则", action);
        }

        var score = pressures.Sum(item => item.Amount);
        var strongest = pressures.MaxBy(item => Math.Abs(item.Amount))!;
        var complies = !pressures.Any(item =>
            item.Directive.Mode == Mode.Forbid && item.Matched ||
            item.Directive.Mode == Mode.Require && !item.Matched);
        var issuer = strongest.Belief.Get<Entity>("issuer");
        var reason = strongest.Directive.Mode == Mode.Require && !strongest.Matched
            ? $"未满足 REQUIRE：{issuer?.Name ?? strongest.Belief.Name}"
            : $"{strongest.Directive.Mode}：{issuer?.Name ?? strongest.Belief.Name}";

        return new Decision(effect, complies, score, strongest.Belief, reason, action, pressures);
    }

    /// <summary>从多项候选效果中评价并筛选出综合得分最高的决策方案。</summary>
    /// <param name="soul">决策心智主体。</param>
    /// <param name="candidates">候选效果序列。</param>
    /// <param name="env">认知求值环境。</param>
    /// <param name="utility">外部附加效用函数。</param>
    /// <returns>评分最高的决策结果。</returns>
    public Decision Decide(Soul soul, IEnumerable<Effect> candidates, RuleEnvironment env, Func<Effect, double>? utility = null)
    {
        Decision? best = null;
        foreach (var effect in candidates)
        {
            var d = Evaluate(soul, effect, env);
            double u = utility != null ? utility(effect) : 0.0;
            double totalScore = d.Score + u;
            if (best == null || totalScore > best.Score)
            {
                best = new Decision(d.Intent, d.Complies, totalScore, d.Basis, d.Reason, null, d.Pressures);
            }
        }
        return best ?? new Decision(new Effect("none"), complies: true, score: 0, basis: null, reason: "无候选行动");
    }

    /// <summary>检查候选意图是否完全满足给定的一组客观硬性规则约束。</summary>
    /// <param name="intent">待执行效果 AST。</param>
    /// <param name="constraints">客观规则约束集合。</param>
    /// <param name="env">环境事实。</param>
    /// <returns>true 表示未触犯任何硬性约束。</returns>
    public bool ObjectiveAllows(Effect intent, IEnumerable<Rule> constraints, RuleEnvironment env)
    {
        return constraints.All(rule => ObjectiveComplies(intent, rule, env));
    }

    /// <summary>检查单条客观规则对目标意图的合规性。</summary>
    /// <param name="intent">意图效果。</param>
    /// <param name="rule">客观硬规则。</param>
    /// <param name="env">求值环境。</param>
    /// <returns>false 表示触犯 Forbid 或未满足 Require。</returns>
    public bool ObjectiveComplies(Effect intent, Rule rule, RuleEnvironment env)
    {
        if (!env.Evaluate(rule.Condition))
        {
            return true;
        }

        foreach (var directive in Expanded(rule, env))
        {
            if (directive.Mode == Mode.Forbid && env.EffectMatches(directive.Effect, intent))
            {
                return false;
            }
            if (directive.Mode == Mode.Require && !env.EffectMatches(directive.Effect, intent))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>检查言灵或契约的激活前置规则是否完全作为事实在当前环境中成立。</summary>
    /// <param name="rule">激活触发规则。</param>
    /// <param name="env">求值环境。</param>
    /// <returns>true 表示全部条件与要求均作为事实成立。</returns>
    public bool ActivationSatisfied(Rule rule, RuleEnvironment env)
    {
        if (!env.Evaluate(rule.Condition))
        {
            return false;
        }

        foreach (var directive in Expanded(rule, env))
        {
            if (directive.Mode == Mode.Require && !env.EffectIsFact(directive.Effect))
            {
                return false;
            }
            if (directive.Mode == Mode.Forbid && env.EffectIsFact(directive.Effect))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>检查目标辖域内的所有生效规则补丁（RulePatch）是否允许特定效果意图执行。</summary>
    /// <param name="intent">行动效果 AST。</param>
    /// <param name="patches">世界中部署的补丁实体列表。</param>
    /// <param name="env">求值环境。</param>
    /// <param name="context">包含当前所处辖域（domains）的情境上下文。</param>
    /// <returns>true 表示未被任何激活且适用辖域的补丁拦截阻断。</returns>
    public bool PatchAllows(Effect intent, IEnumerable<Entity> patches, RuleEnvironment env,
        ActionContext? context = null) =>
        PatchAllows(intent, patches, env, Domains(context?.Domains), null);

    /// <summary>完整行动按实际身体绑定槽位；未指定 domains 时沿世界树推导客观补丁辖域。</summary>
    public bool PatchAllows(Intent intent, IEnumerable<Entity> patches, RuleEnvironment env,
        ActionContext? context = null) =>
        PatchAllows(intent.Effect, patches, env,
            context?.Domains != null ? Domains(context.Domains) : Ancestors(intent.Actor),
            Bindings(intent, context));

    public bool PatchAllows(Effect intent, IEnumerable<Entity> patches, RuleEnvironment env,
        IReadOnlyDictionary<string, object>? context) =>
        PatchAllows(intent, patches, env, ActionContext.FromDictionary(context));

    public bool PatchAllows(Intent intent, IEnumerable<Entity> patches, RuleEnvironment env,
        IReadOnlyDictionary<string, object>? context) =>
        PatchAllows(intent, patches, env, ActionContext.FromDictionary(context));

    private bool PatchAllows(Effect effect, IEnumerable<Entity> patches, RuleEnvironment env,
        IEnumerable<Entity> domains, IReadOnlyDictionary<string, object>? bindings)
    {
        var scope = domains.ToHashSet();
        return patches.Where(p => p.HasTag("RulePatch") && p.IsActive()
                && (p.Scope() is not { } domain || scope.Contains(domain)))
            .Select(p => p.Rule()).OfType<Rule>()
            .All(rule => ObjectiveComplies(effect, bindings == null ? rule : env.Materialize(rule, bindings), env));
    }

    private static IEnumerable<Entity> Domains(object? value) => value switch
    {
        Entity entity => [entity],
        IEnumerable<Entity> entities => entities,
        _ => []
    };

    private static IEnumerable<Entity> Ancestors(TreeMember member)
    {
        for (TreeMember? current = member; current != null; current = current.Parent)
            if (current is Entity entity) yield return entity;
    }
}
