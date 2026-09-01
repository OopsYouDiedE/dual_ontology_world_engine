using System.Collections.Immutable;

namespace DualOntologyEngine.Core;

public enum DirectiveMode
{
    Require,
    Forbid,
    Permit,
    Prefer,
}

public sealed record Expression(string Operator, ImmutableArray<object> Arguments)
{
    public Expression(string @operator, params object[] arguments)
        : this(@operator, arguments.ToImmutableArray()) { }
}

public sealed record Effect(string Operator, ImmutableArray<object> Arguments)
{
    public Effect(string @operator, params object[] arguments)
        : this(@operator, arguments.ToImmutableArray()) { }
}

public sealed record Directive(DirectiveMode Mode, Effect Effect);

public sealed record Rule(
    Expression Condition,
    ImmutableArray<Directive> Directives,
    string Source = ""
)
{
    public Rule(Expression condition, IEnumerable<Directive> directives, string source = "")
        : this(condition, [.. directives], source) { }
}

public sealed record WordSpirit(
    ImmutableArray<Rule> Rules,
    ImmutableArray<Rule> Constraints,
    Effect Cost,
    ImmutableArray<Effect> Effects
)
{
    public WordSpirit(IEnumerable<Rule> rules, IEnumerable<Rule> constraints, Effect cost, IEnumerable<Effect> effects)
        : this([.. rules], [.. constraints], cost, [.. effects]) { }

    public WordSpirit(IEnumerable<Rule> rules, Effect cost, IEnumerable<Effect> effects)
        : this([.. rules], [], cost, [.. effects]) { }

    public WordSpirit(IEnumerable<Rule> rules, IEnumerable<Effect> effects)
        : this([.. rules], [], new Effect("none"), [.. effects]) { }
}

public sealed class RuleSyntaxException(string message) : FormatException(message);

public static class RuleAst
{
    public static Expression Expression(string @operator, params object[] arguments) =>
        new(@operator, arguments);

    public static Effect Effect(string @operator, params object[] arguments) =>
        new(@operator, arguments);
}
