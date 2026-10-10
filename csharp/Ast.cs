using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Core;

public enum RuleSlot { Actor, Target, Tool, Scene }
public enum RuleMode { Require, Forbid, Permit, Prefer }
public enum ActionOp { Unlock, Travel, Take, Inspect, Destroy, Place }

public abstract record Noun
{
    public sealed record Reference(Projection Projection) : Noun;
    public sealed record Slot(RuleSlot Role) : Noun;
    public static implicit operator Noun(Projection projection) => new Reference(projection);
}

public abstract record Condition
{
    public sealed record Constant(Truth Value) : Condition;
    public sealed record Exists(Noun Target) : Condition;
    public sealed record Tagged(Noun Target, Tag Tag, int Minimum) : Condition;
    public sealed record Related(Noun Target, RelationTag Relation, Noun Other) : Condition;
    public sealed record At(Noun Target, Noun Space) : Condition;
    public sealed record IsKind(Noun Target, ProjectionKind Kind) : Condition;
    public sealed record Not(Condition Operand) : Condition;
    public sealed record All(ImmutableArray<Condition> Items) : Condition;
    public sealed record Any(ImmutableArray<Condition> Items) : Condition;
}

public abstract record Effect
{
    public sealed record Act(ActionOp Operation, Noun Target, Noun? Tool = null) : Effect;
    public sealed record Set(Noun Target, Tag Tag, int Value) : Effect;
    public sealed record Change(Noun Target, Tag Tag, int Amount) : Effect;
    public sealed record TemporarySet(Noun Target, Tag Tag, int Value, double Duration) : Effect;
    public sealed record TemporarySetUntilEnd(Noun Target, Tag Tag, int Value) : Effect;
    public sealed record Spend(Noun Target, Tag Tag, int Amount) : Effect;
    public sealed record Relate(Noun Target, RelationTag Relation, Noun Other, bool Remove = false) : Effect;
    public sealed record Sequence(ImmutableArray<Effect> Items) : Effect;
    public sealed record When(Condition Condition, Effect Body) : Effect;
    public sealed record Require(Condition Condition) : Effect;
    public sealed record Wait(double Delay, Effect Body, Condition? Until = null) : Effect
    {
        public Wait(Condition until, Effect body) : this(0, body, until) { }
    }
    public sealed record Say(Noun Recipient, string Text, Effect? Request = null,
        Condition? Claim = null, AuthorTermId? Topic = null) : Effect;
    public sealed record Think(Condition Fact) : Effect;
}

public sealed record Rule(Condition Condition, RuleMode Mode, Effect Effect, int Weight = 1,
    Truth Acceptance = Truth.Unknown);

public sealed record ActionContext(Projection Actor, Projection? Target = null, Projection? Tool = null, Projection? Scene = null)
{
    public Projection? Resolve(Noun noun) => noun switch
    {
        Noun.Reference reference => reference.Projection,
        Noun.Slot slot => slot.Role switch
        {
            RuleSlot.Actor => Actor, RuleSlot.Target => Target, RuleSlot.Tool => Tool, RuleSlot.Scene => Scene,
            _ => null
        },
        _ => null
    };
}

/// <summary>AST 的结构操作与三值求值。没有绑定或没有认知信息时返回 Unknown。</summary>
public static partial class Ast
{
    public static Truth Evaluate(Condition condition, ActionContext context) => SubjectRead.Evaluate(condition, context);

    internal static Truth EvaluateLive(Condition condition, ActionContext context) => condition switch
    {
        Condition.Constant c => c.Value,
        Condition.Exists c => context.Resolve(c.Target)?.Existence ?? Truth.Unknown,
        Condition.Tagged c => context.Resolve(c.Target) is { } p && p.Tags.TryGetValue(c.Tag, out var value)
            ? From(value >= c.Minimum) : Truth.Unknown,
        Condition.Related c => context.Resolve(c.Target) is { } p && context.Resolve(c.Other) is { } other
            ? (p.Relations.Contains((c.Relation, other)) ? Truth.True : p.DeniedRelations.Contains((c.Relation, other)) ? Truth.False : Truth.Unknown) : Truth.Unknown,
        Condition.At c => context.Resolve(c.Target)?.Parent is { } parent && context.Resolve(c.Space) is { } space
            ? From(ReferenceEquals(parent, space)) : Truth.Unknown,
        Condition.IsKind c => context.Resolve(c.Target) is { } p ? From(p.Kind == c.Kind) : Truth.Unknown,
        Condition.Not c => Negate(EvaluateLive(c.Operand, context)),
        Condition.All c => All(c.Items.Select(item => EvaluateLive(item, context))),
        Condition.Any c => Negate(All(c.Items.Select(item => Negate(EvaluateLive(item, context))))),
        _ => throw new NotSupportedException()
    };

    public static Truth From(bool value) => value ? Truth.True : Truth.False;
    public static Truth Negate(Truth value) => value switch { Truth.True => Truth.False, Truth.False => Truth.True, _ => Truth.Unknown };
    public static Truth All(IEnumerable<Truth> values)
    {
        var result = Truth.True;
        foreach (var value in values)
        {
            if (value == Truth.False) return Truth.False;
            if (value == Truth.Unknown) result = Truth.Unknown;
        }
        return result;
    }

    public static Condition Map(Condition condition, Func<Noun, Noun> map) => condition switch
    {
        Condition.Exists c => c with { Target = map(c.Target) },
        Condition.Tagged c => c with { Target = map(c.Target) },
        Condition.Related c => c with { Target = map(c.Target), Other = map(c.Other) },
        Condition.At c => c with { Target = map(c.Target), Space = map(c.Space) },
        Condition.IsKind c => c with { Target = map(c.Target) },
        Condition.Not c => c with { Operand = Map(c.Operand, map) },
        Condition.All c => new Condition.All([..c.Items.Select(item => Map(item, map))]),
        Condition.Any c => new Condition.Any([..c.Items.Select(item => Map(item, map))]),
        _ => condition
    };

    public static Effect Map(Effect effect, Func<Noun, Noun> map) => effect switch
    {
        Effect.Act e => e with { Target = map(e.Target), Tool = e.Tool is null ? null : map(e.Tool) },
        Effect.Set e => e with { Target = map(e.Target) },
        Effect.TemporarySet e => e with { Target = map(e.Target) },
        Effect.TemporarySetUntilEnd e => e with { Target = map(e.Target) },
        Effect.Change e => e with { Target = map(e.Target) },
        Effect.Spend e => e with { Target = map(e.Target) },
        Effect.Relate e => e with { Target = map(e.Target), Other = map(e.Other) },
        Effect.Sequence e => new Effect.Sequence([..e.Items.Select(item => Map(item, map))]),
        Effect.When e => new Effect.When(Map(e.Condition, map), Map(e.Body, map)),
        Effect.Require e => new Effect.Require(Map(e.Condition, map)),
        Effect.Wait e => e with { Body = Map(e.Body, map), Until = e.Until is null ? null : Map(e.Until, map) },
        Effect.Say e => e with { Recipient = map(e.Recipient), Request = e.Request is null ? null : Map(e.Request, map),
            Claim = e.Claim is null ? null : Map(e.Claim, map) },
        Effect.Think e => e with { Fact = Map(e.Fact, map) },
        _ => throw new NotSupportedException()
    };

    public static Rule Map(Rule rule, Func<Noun, Noun> map) => rule with { Condition = Map(rule.Condition, map), Effect = Map(rule.Effect, map) };
    public static Effect Bind(Effect effect, ActionContext context)
    {
        Noun BindNoun(Noun noun) => new Noun.Reference(context.Resolve(noun) ?? throw new InvalidOperationException("行动槽位尚未填充。"));
        return effect switch
        {
            // 请求中的槽位属于接收者的后续行动，不能在发言者上下文提前填充。
            Effect.Say say => say with { Recipient = BindNoun(say.Recipient), Claim = say.Claim is null ? null : Map(say.Claim, BindNoun) },
            Effect.Sequence sequence => new Effect.Sequence([..sequence.Items.Select(e => Bind(e, context))]),
            Effect.When conditional => new Effect.When(Map(conditional.Condition, BindNoun), Bind(conditional.Body, context)),
            Effect.Wait wait => wait with { Body = Bind(wait.Body, context), Until = wait.Until is null ? null : Map(wait.Until, BindNoun) },
            _ => Map(effect, BindNoun)
        };
    }
    public static IEnumerable<Noun> Nouns(Effect? effect)
    {
        var nouns = new List<Noun>();
        if (effect is not null) CollectNouns(effect, nouns);
        return nouns;
    }
    public static IEnumerable<Noun> Nouns(Rule rule)
    {
        var nouns = new List<Noun>();
        CollectNouns(rule.Condition, nouns);
        CollectNouns(rule.Effect, nouns);
        return nouns;
    }
    public static IEnumerable<Projection> References(Effect? effect) => Nouns(effect).OfType<Noun.Reference>().Select(n => n.Projection);
    public static IEnumerable<Projection> References(Rule rule) => Nouns(rule).OfType<Noun.Reference>().Select(n => n.Projection);
    public static IEnumerable<Projection> References(Condition? condition)
    {
        var nouns = new List<Noun>();
        CollectNouns(condition, nouns);
        // Keep the condition overload eager, as it was before direct traversal.
        return nouns.OfType<Noun.Reference>().Select(n => n.Projection).ToArray();
    }

    // Read-only traversal must not rebuild the AST. Preserve noun order and repeated references.
    private static void CollectNouns(Condition? condition, List<Noun> nouns)
    {
        switch (condition)
        {
            case Condition.Exists c: nouns.Add(c.Target); break;
            case Condition.Tagged c: nouns.Add(c.Target); break;
            case Condition.Related c: nouns.Add(c.Target); nouns.Add(c.Other); break;
            case Condition.At c: nouns.Add(c.Target); nouns.Add(c.Space); break;
            case Condition.IsKind c: nouns.Add(c.Target); break;
            case Condition.Not c: CollectNouns(c.Operand, nouns); break;
            case Condition.All c:
                foreach (var item in c.Items) CollectNouns(item, nouns);
                break;
            case Condition.Any c:
                foreach (var item in c.Items) CollectNouns(item, nouns);
                break;
        }
    }

    private static void CollectNouns(Effect effect, List<Noun> nouns)
    {
        switch (effect)
        {
            case Effect.Act e:
                nouns.Add(e.Target);
                if (e.Tool is not null) nouns.Add(e.Tool);
                break;
            case Effect.Set e: nouns.Add(e.Target); break;
            case Effect.TemporarySet e: nouns.Add(e.Target); break;
            case Effect.TemporarySetUntilEnd e: nouns.Add(e.Target); break;
            case Effect.Change e: nouns.Add(e.Target); break;
            case Effect.Spend e: nouns.Add(e.Target); break;
            case Effect.Relate e: nouns.Add(e.Target); nouns.Add(e.Other); break;
            case Effect.Sequence e:
                foreach (var item in e.Items) CollectNouns(item, nouns);
                break;
            case Effect.When e: CollectNouns(e.Condition, nouns); CollectNouns(e.Body, nouns); break;
            case Effect.Require e: CollectNouns(e.Condition, nouns); break;
            case Effect.Wait e: CollectNouns(e.Until, nouns); CollectNouns(e.Body, nouns); break;
            case Effect.Say e:
                nouns.Add(e.Recipient);
                if (e.Request is not null) CollectNouns(e.Request, nouns);
                CollectNouns(e.Claim, nouns);
                break;
            case Effect.Think e: CollectNouns(e.Fact, nouns); break;
            default: throw new NotSupportedException();
        }
    }

    /// <summary>ImmutableArray 的默认相等比较不比较内容，规则匹配使用结构比较。</summary>
    public static bool Equivalent(Effect left, Effect right) => (left, right) switch
    {
        (Effect.Sequence a, Effect.Sequence b) => a.Items.Length == b.Items.Length && a.Items.Zip(b.Items).All(p => Equivalent(p.First, p.Second)),
        (Effect.Wait a, Effect.Wait b) => a.Delay == b.Delay &&
            (a.Until is null ? b.Until is null : b.Until is not null && Equivalent(a.Until, b.Until)) && Equivalent(a.Body, b.Body),
        (Effect.When a, Effect.When b) => Equivalent(a.Condition, b.Condition) && Equivalent(a.Body, b.Body),
        (Effect.Require a, Effect.Require b) => Equivalent(a.Condition, b.Condition),
        (Effect.Think a, Effect.Think b) => Equivalent(a.Fact, b.Fact),
        (Effect.Say a, Effect.Say b) => a.Recipient == b.Recipient &&
            (a.Topic is not null || a.Claim is not null || a.Text == b.Text) &&
            a.Topic == b.Topic && (a.Claim is null ? b.Claim is null : b.Claim is not null && Equivalent(a.Claim, b.Claim)) &&
            (a.Request is null ? b.Request is null : b.Request is not null && Equivalent(a.Request, b.Request)),
        _ => left == right
    };
    public static bool Equivalent(Condition left, Condition right) => (left, right) switch
    {
        (Condition.All a, Condition.All b) => a.Items.Length == b.Items.Length && a.Items.Zip(b.Items).All(p => Equivalent(p.First, p.Second)),
        (Condition.Any a, Condition.Any b) => a.Items.Length == b.Items.Length && a.Items.Zip(b.Items).All(p => Equivalent(p.First, p.Second)),
        (Condition.Not a, Condition.Not b) => Equivalent(a.Operand, b.Operand),
        _ => left == right
    };
}
