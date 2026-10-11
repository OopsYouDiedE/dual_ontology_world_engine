using System.Collections.Immutable;

namespace DualOntology.Language;

public enum RuleMode { Require, Forbid, Permit, Prefer, Infer }
public enum RuleLifetime { World, Chapter, Interaction }
public enum Truth { Unknown, False, True }
public enum NounSlot { Actor, Target, Tool, Scene }

public abstract record Noun
{
    public sealed record Reference(string Handle) : Noun;
    public sealed record Slot(NounSlot Role) : Noun;
    internal Value Encode() => this switch
    {
        Reference r => Value.Seq(new Value.Symbol("ref"), new Value.Text(r.Handle)),
        Slot s => Value.Seq(new Value.Symbol("slot"), new Value.Symbol(s.Role.ToString().ToLowerInvariant())),
        _ => throw new LanguageException("Unknown noun node.")
    };
    internal static Noun Decode(Value value)
    {
        var items = TermReader.Form(value, 2);
        return Value.SymbolOf(items[0]) switch
        {
            "ref" when items[1] is Value.Text { Data.Length: > 0 } text => new Reference(text.Data),
            "slot" when items[1] is Value.Symbol symbol && Enum.TryParse<NounSlot>(symbol.Name, true, out var role) && Enum.IsDefined(role) => new Slot(role),
            _ => throw new LanguageException("Invalid noun.")
        };
    }
}

public abstract record Condition
{
    public sealed record Constant(Truth Truth) : Condition;
    public sealed record Exists(Noun Target) : Condition;
    public sealed record Tagged(Noun Target, string Tag, int Minimum) : Condition;
    public sealed record Related(Noun Target, string Relation, Noun Other) : Condition;
    public sealed record At(Noun Target, Noun Place) : Condition;
    public sealed record Kind(Noun Target, string Name) : Condition;
    public sealed record Not(Condition Operand) : Condition;
    public sealed record All(ImmutableArray<Condition> Items) : Condition;
    public sealed record Any(ImmutableArray<Condition> Items) : Condition;

    public static Condition Parse(string source) => Decode(Reader.Read(source));
    internal Value Encode() => this switch
    {
        Constant c => TermReader.Node("const", TermReader.TruthValue(c.Truth)),
        Exists c => TermReader.Node("exists", c.Target.Encode()),
        Tagged c => TermReader.Node("tagged", c.Target.Encode(), TermReader.Name(c.Tag), new Value.Number(c.Minimum)),
        Related c => TermReader.Node("related", c.Target.Encode(), TermReader.Name(c.Relation), c.Other.Encode()),
        At c => TermReader.Node("at", c.Target.Encode(), c.Place.Encode()),
        Kind c => TermReader.Node("kind", c.Target.Encode(), TermReader.Name(c.Name)),
        Not c => TermReader.Node("not", c.Operand.Encode()),
        All c => new Value.List([new Value.Symbol("all"), .. c.Items.Select(x => x.Encode())]),
        Any c => new Value.List([new Value.Symbol("any"), .. c.Items.Select(x => x.Encode())]),
        _ => throw new LanguageException("Unknown condition node.")
    };
    internal static Condition Decode(Value value)
    {
        var f = Value.ItemsOf(value);
        if (f.IsEmpty) throw new LanguageException("Empty condition.");
        var name = Value.SymbolOf(f[0]);
        return name switch
        {
            "const" when f.Length == 2 => new Constant(TermReader.DecodeTruth(f[1])),
            "exists" when f.Length == 2 => new Exists(Noun.Decode(f[1])),
            "tagged" when f.Length == 4 => new Tagged(Noun.Decode(f[1]), Value.SymbolOf(f[2]), TermReader.Integer(f[3])),
            "related" when f.Length == 4 => new Related(Noun.Decode(f[1]), Value.SymbolOf(f[2]), Noun.Decode(f[3])),
            "at" when f.Length == 3 => new At(Noun.Decode(f[1]), Noun.Decode(f[2])),
            "kind" when f.Length == 3 => new Kind(Noun.Decode(f[1]), Value.SymbolOf(f[2])),
            "not" when f.Length == 2 => new Not(Decode(f[1])),
            "all" => new All([.. f.Skip(1).Select(Decode)]),
            "any" => new Any([.. f.Skip(1).Select(Decode)]),
            _ => throw new LanguageException($"Unsupported or malformed condition: {name}")
        };
    }
}

public abstract record Effect
{
    public sealed record Set(Noun Target, string Tag, int Value) : Effect;
    public sealed record Change(Noun Target, string Tag, int Amount) : Effect;
    public sealed record Spend(Noun Target, string Tag, int Amount) : Effect;
    public sealed record Relate(Noun Target, string Relation, Noun Other, bool Present) : Effect;
    public sealed record Require(Condition Condition) : Effect;
    public sealed record When(Condition Condition, Effect Body) : Effect;
    public sealed record Sequence(ImmutableArray<Effect> Items) : Effect;
    public static Effect Parse(string source) => Decode(Reader.Read(source));
    internal Value Encode() => this switch
    {
        Set e => TermReader.Node("set", e.Target.Encode(), TermReader.Name(e.Tag), new Value.Number(e.Value)),
        Change e => TermReader.Node("change", e.Target.Encode(), TermReader.Name(e.Tag), new Value.Number(e.Amount)),
        Spend e => TermReader.Node("spend", e.Target.Encode(), TermReader.Name(e.Tag), new Value.Number(e.Amount)),
        Relate e => TermReader.Node("relate", e.Target.Encode(), TermReader.Name(e.Relation), e.Other.Encode(), e.Present ? Value.True : Value.False),
        Require e => TermReader.Node("require", e.Condition.Encode()),
        When e => TermReader.Node("when", e.Condition.Encode(), e.Body.Encode()),
        Sequence e => new Value.List([new Value.Symbol("seq"), .. e.Items.Select(x => x.Encode())]),
        _ => throw new LanguageException("Unknown effect node.")
    };
    internal static Effect Decode(Value value)
    {
        var f = Value.ItemsOf(value);
        if (f.IsEmpty) throw new LanguageException("Empty effect.");
        var name = Value.SymbolOf(f[0]);
        return name switch
        {
            "set" when f.Length == 4 => new Set(Noun.Decode(f[1]), Value.SymbolOf(f[2]), TermReader.Integer(f[3])),
            "change" when f.Length == 4 => new Change(Noun.Decode(f[1]), Value.SymbolOf(f[2]), TermReader.Integer(f[3])),
            "spend" when f.Length == 4 => new Spend(Noun.Decode(f[1]), Value.SymbolOf(f[2]), TermReader.Integer(f[3])),
            "relate" when f.Length == 5 && f[4] is Value.Boolean b => new Relate(Noun.Decode(f[1]), Value.SymbolOf(f[2]), Noun.Decode(f[3]), b.Data),
            "require" when f.Length == 2 => new Require(Condition.Decode(f[1])),
            "when" when f.Length == 3 => new When(Condition.Decode(f[1]), Decode(f[2])),
            "seq" => new Sequence([.. f.Skip(1).Select(Decode)]),
            _ => throw new LanguageException($"Unsupported or malformed effect: {name}")
        };
    }
}

public sealed record Rule(string Id, Condition Condition, RuleMode Mode, Effect? Action, Condition? Conclusion,
    int Weight = 1, Truth Acceptance = Truth.Unknown, RuleLifetime Lifetime = RuleLifetime.World, string? Owner = null)
{
    internal Value Encode()
    {
        if (!Enum.IsDefined(Mode) || !Enum.IsDefined(Acceptance) || !Enum.IsDefined(Lifetime)) throw new LanguageException("Invalid rule enum.");
        if (Lifetime != RuleLifetime.World && string.IsNullOrWhiteSpace(Owner)) throw new LanguageException("Scoped rule requires an owner.");
        var payload = Mode == RuleMode.Infer
            ? Conclusion?.Encode() ?? throw new LanguageException("Inference rule needs a conclusion.")
            : Action?.Encode() ?? throw new LanguageException("Action rule needs an effect.");
        if (Mode == RuleMode.Infer && Action is not null || Mode != RuleMode.Infer && Conclusion is not null) throw new LanguageException("Ambiguous rule payload.");
        return TermReader.Node("rule", TermReader.Name(Id), Condition.Encode(), new Value.Symbol(Mode.ToString().ToLowerInvariant()), payload, new Value.Number(Weight), TermReader.TruthValue(Acceptance));
    }
}

internal static class TermReader
{
    public static Value.Symbol Name(string name) => !string.IsNullOrWhiteSpace(name) && Reader.Read(name) is Value.Symbol s && s.Name == name
        ? s : throw new LanguageException("Invalid symbolic name.");
    public static Value Node(string name, params Value[] args) => new Value.List([new Value.Symbol(name), .. args]);
    public static Value TruthValue(Truth truth) => truth switch { Truth.True => Value.True, Truth.False => Value.False, Truth.Unknown => new Value.Symbol("unknown"), _ => throw new LanguageException("Invalid truth.") };
    public static Truth DecodeTruth(Value value) => value switch
    {
        Value.Boolean { Data: true } => Truth.True,
        Value.Boolean { Data: false } => Truth.False,
        Value.Symbol { Name: "unknown" } => Truth.Unknown,
        _ => throw new LanguageException("Invalid truth value.")
    };
    public static int Integer(Value value)
    {
        var number = Value.NumberOf(value);
        return number == Math.Truncate(number) && number >= int.MinValue && number <= int.MaxValue ? (int)number : throw new LanguageException("Expected 32-bit integer.");
    }
    public static ImmutableArray<Value> Form(Value value, int count)
    {
        var items = Value.ItemsOf(value);
        return items.Length == count ? items : throw new LanguageException("Invalid form length.");
    }
}
