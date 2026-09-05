using System;
using System.Collections.Generic;
using System.Linq;

namespace DualOntologyWorldEngine.Core.Rules;

/// <summary>
/// 保存可解析别名和当前事实的轻量规则求值环境。
/// </summary>
/// <remarks>
/// RuleEnvironment 是规则表达式（<see cref="Expr"/>）与效果（<see cref="Effect"/>）的求值上下文容器。
/// 
/// 核心职责：
/// 1. 符号别名解析（<see cref="Aliases"/>）：支持将字符串代号（如 "actor"、"room"）映射到实际运行时 Entity 引用；
/// 2. 事实库管理（<see cref="Facts"/>）：维护离散的事实断言与撤销（Assert / Retract）；
/// 3. 条件求值（<see cref="Evaluate"/>）：支持复合布尔逻辑（all, any, not）与实体状态查询（state, parent, related, tag_at_least）；
/// 4. 模式匹配（<see cref="EffectMatches"/>）：支持多步序列（sequence）与单步原语匹配；
/// 5. 规则具象化（<see cref="Materialize(Rule, IReadOnlyDictionary{string, object}?)"/>）：将模版中的代号替换为确定实体。
/// </remarks>
public class RuleEnvironment
{
    private readonly Dictionary<string, object> _aliases = new(StringComparer.OrdinalIgnoreCase);
    public Entity State { get; } = new("规则事实", properties: new Dictionary<string, object> { ["facts"] = new List<Expr>() });
    private List<Expr> _facts => State.Get<List<Expr>>("facts")!;

    /// <summary>当前环境中注册的符号别名映射字典（不区分大小写）。</summary>
    public IReadOnlyDictionary<string, object> Aliases => _aliases;

    /// <summary>当前环境中已生效的事实表达式集合。</summary>
    public IReadOnlyList<Expr> Facts => _facts;

    /// <summary>环境版本号。每次断言或撤销事实时单调递增，用于缓存失效检测。</summary>
    public int Version { get; private set; }

    /// <summary>
    /// 清空所有符号别名与已断言事实，将环境重置为初始状态。
    /// </summary>
    public void Clear()
    {
        _aliases.Clear();
        _facts.Clear();
        Version = 0;
    }

    /// <summary>
    /// 注册或覆盖一条符号别名映射。
    /// </summary>
    /// <param name="name">别名代号（如 "target"、"scene"）。</param>
    /// <param name="value">绑定的目标对象或实体引用。</param>
    public void Alias(string name, object value)
    {
        _aliases[name] = value;
    }

    /// <summary>
    /// 从另一规则环境中复制全部符号别名映射。
    /// </summary>
    /// <param name="other">源求值环境。</param>
    public void CopyAliasesFrom(RuleEnvironment other)
    {
        foreach (var (k, v) in other._aliases)
        {
            _aliases[k] = v;
        }
    }

    /// <summary>
    /// 将可能的符号代号解析为具体的运行时对象引用。
    /// </summary>
    /// <param name="value">待解析的对象（若为字符串则尝试在 bindings 或 aliases 中寻址）。</param>
    /// <param name="bindings">临时优先绑定的局部代号字典。</param>
    /// <returns>解析后的具体对象引用；若未命中任何别名则返回原值。</returns>
    public object? Resolve(object? value, IReadOnlyDictionary<string, object>? bindings = null)
    {
        if (value is RuleSlot slot)
        {
            var key = slot.ToString();
            if (bindings != null)
            {
                foreach (var (name, bound) in bindings)
                {
                    if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase)) return bound;
                }
            }
            return _aliases.TryGetValue(key, out var slotted) ? slotted : value;
        }
        if (value is not string s) return value;
        if (bindings != null)
        {
            foreach (var (k, v) in bindings)
            {
                if (string.Equals(k, s, StringComparison.OrdinalIgnoreCase)) return v;
            }
        }
        if (_aliases.TryGetValue(s, out var found)) return found;
        return value;
    }

    /// <summary>
    /// 判定两个参数在解析别名后是否在语义上等同。
    /// </summary>
    /// <param name="left">左侧参数。</param>
    /// <param name="right">右侧参数。</param>
    /// <returns>true 表示两对象完全等同或引用同一实体。</returns>
    public bool Same(object? left, object? right)
    {
        left = Resolve(left);
        right = Resolve(right);
        if (left is Expr el && right is Expr er) return Matches(el, er);
        if (left is Effect efl && right is Effect efr) return EffectMatches(efl, efr);
        if (left is string sl && right is string sr) return string.Equals(sl, sr, StringComparison.OrdinalIgnoreCase);
        if (left is Entity || right is Entity) return ReferenceEquals(left, right);
        return Equals(left, right);
    }

    /// <summary>
    /// 检查特定事实表达式是否精确匹配目标查询表达式。
    /// </summary>
    /// <param name="fact">事实表达式。</param>
    /// <param name="query">查询模板表达式。</param>
    /// <returns>true 表示操作符与全部参数匹配一致。</returns>
    public bool Matches(Expr fact, Expr query)
    {
        if (!string.Equals(fact.Op, query.Op, StringComparison.OrdinalIgnoreCase)) return false;
        if (fact.Args.Length != query.Args.Length) return false;
        for (int i = 0; i < fact.Args.Length; i++)
        {
            if (!Same(fact.Args[i], query.Args[i])) return false;
        }
        return true;
    }

    /// <summary>
    /// 检查候选效果是否命中了目标效果模式（支持 sequence 嵌套拆解）。
    /// </summary>
    /// <param name="pattern">规则指令中规定的模式效果。</param>
    /// <param name="candidate">候选行动意图携带的实际效果。</param>
    /// <returns>true 表示匹配成功。</returns>
    public bool EffectMatches(Effect pattern, Effect candidate)
    {
        if (string.Equals(pattern.Op, "sequence", StringComparison.OrdinalIgnoreCase))
        {
            return pattern.Args.OfType<Effect>().Any(item => EffectMatches(item, candidate));
        }

        if (!string.Equals(pattern.Op, candidate.Op, StringComparison.OrdinalIgnoreCase)) return false;
        if (pattern.Args.Length != candidate.Args.Length) return false;
        for (int i = 0; i < pattern.Args.Length; i++)
        {
            if (!Same(pattern.Args[i], candidate.Args[i])) return false;
        }
        return true;
    }

    /// <summary>
    /// 向环境中追加断言一条事实表达式。
    /// </summary>
    /// <param name="fact">待断言的事实表达式。</param>
    /// <returns>true 表示新增成功；false 表示已存在等价事实。</returns>
    public bool AssertFact(Expr fact)
    {
        if (_facts.Any(f => Matches(f, fact))) return false;
        _facts.Add(fact);
        Version++;
        return true;
    }

    /// <summary>
    /// 从环境中撤销并移除特定事实表达式。
    /// </summary>
    /// <param name="fact">待撤销的事实表达式。</param>
    /// <returns>true 表示成功撤销；false 表示原本未记录该事实。</returns>
    public bool Retract(Expr fact)
    {
        var found = _facts.FirstOrDefault(f => Matches(f, fact));
        if (found != null)
        {
            _facts.Remove(found);
            Version++;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 对条件表达式进行求值，递归判定其在当前环境与实体状态下是否成立。
    /// </summary>
    /// <param name="expression">待求值的条件表达式 AST。</param>
    /// <returns>true 表示条件成立，false 表示不满足。</returns>
    public bool Evaluate(Expr expression)
    {
        var op = expression.Op.ToLowerInvariant();
        switch (op)
        {
            case "true":
                return true;

            case "all":
                return expression.Args.OfType<Expr>().All(Evaluate);

            case "any":
                return expression.Args.OfType<Expr>().Any(Evaluate);

            case "not" when expression.Args.Length >= 1 && expression.Args[0] is Expr inner:
                return !Evaluate(inner);

            case "state" when expression.Args.Length == 2 && expression.Args[1] is string state:
                var subj = Resolve(expression.Args[0]);
                if (subj is Entity entity)
                {
                    return entity.HasTag(state) || _facts.Any(f => Matches(f, expression));
                }
                return false;

            case "parent" when expression.Args.Length == 2:
                var child = Resolve(expression.Args[0]);
                var parent = Resolve(expression.Args[1]);
                if (child is Entity ce)
                {
                    return ReferenceEquals(ce.Parent, parent);
                }
                return false;

            case "related" when expression.Args.Length >= 3 && expression.Args[1] is string kind:
                var s = Resolve(expression.Args[0]);
                var t = Resolve(expression.Args[2]);
                if (s is Entity se && t is Entity te)
                {
                    return se.Related(kind, te).Any();
                }
                return false;

            case "tag_at_least" when expression.Args.Length == 3 && expression.Args[1] is string tag:
                var targetObj = Resolve(expression.Args[0]);
                if (targetObj is Entity teAtLeast && double.TryParse(expression.Args[2].ToString(), out var minAmount))
                {
                    return teAtLeast.Tag(tag) >= minAmount;
                }
                return false;

            default:
                return _facts.Any(f => Matches(f, expression));
        }
    }

    /// <summary>
    /// 检查指定效果是否已作为既定事实存在于环境中。
    /// </summary>
    /// <param name="effect">待检查效果 AST。</param>
    /// <returns>true 表示已作为事实成立。</returns>
    public bool EffectIsFact(Effect effect)
    {
        if (string.Equals(effect.Op, "sequence", StringComparison.OrdinalIgnoreCase))
        {
            return effect.Args.OfType<Effect>().All(EffectIsFact);
        }
        return Evaluate(new Expr(effect.Op, effect.Args));
    }

    /// <summary>
    /// 将效果转化为等价的事实表达式并断言记录至环境中。
    /// </summary>
    /// <param name="effect">待固化的效果 AST。</param>
    public void AssertEffect(Effect effect)
    {
        if (string.Equals(effect.Op, "sequence", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var item in effect.Args.OfType<Effect>())
            {
                AssertEffect(item);
            }
        }
        else
        {
            AssertFact(new Expr(effect.Op, effect.Args));
        }
    }

    /// <summary>
    /// 具象化事实表达式，将其内部包含的所有符号代号替换为具体的运行时对象引用。
    /// </summary>
    /// <param name="expr">模板表达式。</param>
    /// <param name="bindings">当前上下文局部绑定字典。</param>
    /// <returns>参数已绑定具体实体的具象表达式新实例。</returns>
    public Expr Materialize(Expr expr, IReadOnlyDictionary<string, object>? bindings = null)
    {
        if (expr == null) return AST.E("true");
        if (expr.Args == null || expr.Args.Length == 0) return new Expr(expr.Op);
        var resolvedArgs = expr.Args.Select(arg =>
        {
            if (arg is Expr subExpr) return Materialize(subExpr, bindings);
            if (arg is Effect subEffect) return Materialize(subEffect, bindings);
            return Resolve(arg, bindings) ?? arg;
        }).ToArray();
        return new Expr(expr.Op, resolvedArgs);
    }

    /// <summary>
    /// 具象化效果表达式，将其内部包含的所有符号代号替换为具体的运行时对象引用。
    /// </summary>
    /// <param name="effect">模板效果。</param>
    /// <param name="bindings">局部代号字典。</param>
    /// <returns>参数已绑定的具象效果新实例。</returns>
    public Effect Materialize(Effect effect, IReadOnlyDictionary<string, object>? bindings = null)
    {
        if (effect == null) return AST.F("none");
        if (effect.Args == null || effect.Args.Length == 0) return new Effect(effect.Op);
        var resolvedArgs = effect.Args.Select(arg =>
        {
            if (arg is Expr subExpr) return Materialize(subExpr, bindings);
            if (arg is Effect subEffect) return Materialize(subEffect, bindings);
            return Resolve(arg, bindings) ?? arg;
        }).ToArray();
        return new Effect(effect.Op, resolvedArgs);
    }

    /// <summary>
    /// 具象化规则对象，将其条件谓词与全部规约指令具象化绑定至确定实体。
    /// </summary>
    /// <param name="rule">抽象规则定义。</param>
    /// <param name="bindings">代号替换映射表。</param>
    /// <returns>具象化后的全新 Rule 实例。</returns>
    public Rule Materialize(Rule rule, IReadOnlyDictionary<string, object>? bindings = null)
    {
        if (rule == null) return new Rule(AST.E("true"), []);
        var cond = Materialize(rule.Condition, bindings);
        var dirs = (rule.Directives ?? []).Select(d => new Directive(d.Mode, Materialize(d.Effect, bindings))).ToList();
        return new Rule(cond, dirs, rule.Source);
    }
}
