using System;
using System.Collections.Generic;
using System.Linq;

namespace DualOntologyWorldEngine.Core.Rules;

/// <summary>
/// 面向场景代码的类型安全规则语法糖。
/// </summary>
/// <remarks>
/// 扩展方法只压缩重复的 AST 样板，不隐藏求值顺序。固定对象直接进入表达式；
/// 当前行动的主体、目标、工具与场景使用 <see cref="RuleSlot"/> 延迟绑定。
/// </remarks>
public static class RuleDsl
{
    /// <summary>把效果包装成强制要求。</summary>
    public static Directive Require(this Effect effect) => new(Mode.Require, effect);

    /// <summary>把效果包装成禁止事项。</summary>
    public static Directive Forbid(this Effect effect) => new(Mode.Forbid, effect);

    /// <summary>把效果包装成许可事项。</summary>
    public static Directive Permit(this Effect effect) => new(Mode.Permit, effect);

    /// <summary>把效果包装成优先倡导事项。</summary>
    public static Directive Prefer(this Effect effect) => new(Mode.Prefer, effect);

    /// <summary>以当前表达式为条件建立规则。</summary>
    public static Rule Then(this Expr condition, string source, params Directive[] directives) =>
        new(condition, directives, source);

    /// <summary>建立逻辑合取表达式。</summary>
    public static Expr And(this Expr first, params Expr[] rest)
    {
        var args = new object[1 + rest.Length];
        args[0] = first;
        for (int i = 0; i < rest.Length; i++) args[i + 1] = rest[i];
        return AST.E("all", args);
    }

    /// <summary>建立逻辑析取表达式。</summary>
    public static Expr Or(this Expr first, params Expr[] rest)
    {
        var args = new object[1 + rest.Length];
        args[0] = first;
        for (int i = 0; i < rest.Length; i++) args[i + 1] = rest[i];
        return AST.E("any", args);
    }

    /// <summary>建立逻辑否定表达式。</summary>
    public static Expr Not(this Expr expression) => AST.E("not", expression);

    /// <summary>检查实体标签是否达到门槛。</summary>
    public static Expr HasAtLeast(this Entity subject, string tag, int amount = 1) =>
        AST.E("tag_at_least", subject, tag, amount);

    /// <summary>使用枚举标签建立门槛表达式，名称由编译器检查。</summary>
    public static Expr HasAtLeast<TTag>(this Entity subject, TTag tag, int amount = 1)
        where TTag : struct, Enum => AST.E("tag_at_least", subject, tag.ToString(), amount);

    /// <summary>检查模板槽位所绑定实体的标签门槛。</summary>
    public static Expr HasAtLeast<TTag>(this RuleSlot subject, TTag tag, int amount = 1)
        where TTag : struct, Enum => AST.E("tag_at_least", subject, tag.ToString(), amount);

    /// <summary>检查实体当前状态标签。</summary>
    public static Expr IsState(this Entity subject, string state) => AST.E("state", subject, state);

    /// <summary>检查模板槽位所绑定实体的当前状态标签。</summary>
    public static Expr IsState(this RuleSlot subject, string state) => AST.E("state", subject, state);

    /// <summary>检查实体是否直接位于指定场景。</summary>
    public static Expr IsIn(this Entity subject, Entity scene) => AST.E("parent", subject, scene);

    /// <summary>检查模板槽位所绑定实体是否直接位于指定场景。</summary>
    public static Expr IsIn(this RuleSlot subject, Entity scene) => AST.E("parent", subject, scene);

    /// <summary>检查两个实体之间是否存在指定语义关系。</summary>
    public static Expr IsRelatedTo(this Entity subject, FacetType relation, Entity target) =>
        AST.E("related", subject, relation.ToString(), target);

    /// <summary>构造无目标行动效果。</summary>
    public static Effect Does(this Entity actor, string operation) => AST.F(Required(operation), actor);

    /// <summary>构造作用于一个实际场景对象的行动效果。</summary>
    public static Effect Does(this Entity actor, string operation, Entity target) =>
        AST.F(Required(operation), actor, target);

    /// <summary>构造带工具的行动效果；重载由编译器按参数形状选择。</summary>
    public static Effect Does(this Entity actor, string operation, Entity target, Entity tool) =>
        AST.F(Required(operation), actor, target, tool);

    /// <summary>构造由当前行动槽位延迟绑定的无目标效果。</summary>
    public static Effect Does(this RuleSlot actor, string operation) => AST.F(Required(operation), actor);

    /// <summary>构造由当前行动主体与目标槽位共同绑定的效果。</summary>
    public static Effect Does(this RuleSlot actor, string operation, RuleSlot target) =>
        AST.F(Required(operation), actor, target);

    /// <summary>构造当前行动主体作用于固定对象的效果。</summary>
    public static Effect Does(this RuleSlot actor, string operation, Entity target) =>
        AST.F(Required(operation), actor, target);

    /// <summary>从 Rule 的真实 AST 派生调试文本，避免维护第二份规则语义。</summary>
    public static string ToAstText(this Rule rule) =>
        $"RULE {Format(rule.Condition)} => {string.Join(", ", rule.Directives.Select(d => $"{d.Mode.ToString().ToUpperInvariant()} {Format(d.Effect)}"))}";

    /// <summary>
    /// 判断发布物集合中是否已经存在 AST 语义相同的规则。
    /// 名称和说明文字不参与比较，避免同义规则依靠改名重复发布。
    /// </summary>
    public static bool ContainsEquivalentRule(this IEnumerable<Entity> publications, Rule candidate)
    {
        var signature = candidate.ToAstText();
        return publications
            .Select(publication => publication.Get<Rule>("rule"))
            .OfType<Rule>()
            .Any(rule => rule.ToAstText() == signature);
    }

    private static string Required(string operation) =>
        !string.IsNullOrWhiteSpace(operation)
            ? operation
            : throw new ArgumentException("行动操作符不能为空。", nameof(operation));

    private static string Format(Expr expr) =>
        $"{expr.Op}({string.Join(", ", expr.Args.Select(FormatValue))})";

    private static string Format(Effect effect) =>
        $"{effect.Op}({string.Join(", ", effect.Args.Select(FormatValue))})";

    private static string FormatValue(object? value) => value switch
    {
        Entity entity => $"@{entity.Name}",
        RuleSlot slot => $"${slot}",
        Expr expr => Format(expr),
        Effect effect => Format(effect),
        string text => $"'{text}'",
        null => "null",
        _ => value.ToString() ?? "null"
    };
}
