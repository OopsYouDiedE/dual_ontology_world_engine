using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace DualOntology.Language;

public abstract record Value
{
    public sealed record Symbol(string Name) : Value;
    public sealed record Text(string Data) : Value;
    public sealed record Number : Value
    {
        public Number(double data)
        {
            if (!double.IsFinite(data)) throw new LanguageException("Non-finite number.");
            Data = data;
        }
        public double Data { get; }
    }
    public sealed record Boolean(bool Data) : Value;
    public sealed record Nil : Value;
    public sealed record List(ImmutableArray<Value> Items) : Value;
    internal sealed record Native(string Name, Func<ImmutableArray<Value>, Evaluation, Value> Body) : Value;
    internal sealed record Closure(ImmutableArray<string> Parameters, Value Body, Scope Environment) : Value;
    internal sealed record Wrapped(Value Dispatcher) : Value;
    internal sealed record Cell(Value Initial) : Value { public Value Content { get; set; } = Initial; }

    public static readonly Value Null = new Nil();
    public static readonly Value True = new Boolean(true);
    public static readonly Value False = new Boolean(false);
    public static List Seq(params Value[] items) => new([.. items]);
    public static bool IsTruthy(Value value) => value is not Nil and not Boolean { Data: false };
    public static ImmutableArray<Value> ItemsOf(Value value) => value is List list ? list.Items : throw new LanguageException("Expected list.");
    public static double NumberOf(Value value) => value is Number number ? number.Data : throw new LanguageException("Expected number.");
    public static string SymbolOf(Value value) => value is Symbol symbol ? symbol.Name : throw new LanguageException("Expected symbol.");
    public static bool Same(Value left, Value right) => (left, right) switch
    {
        (List a, List b) => a.Items.Length == b.Items.Length && a.Items.Zip(b.Items).All(p => Same(p.First, p.Second)),
        (Cell, Cell) or (Native, Native) or (Closure, Closure) or (Wrapped, Wrapped) => ReferenceEquals(left, right),
        _ => left == right
    };
    public static string Print(Value value) => value switch
    {
        Symbol symbol => symbol.Name,
        Text text => JsonSerializer.Serialize(text.Data),
        Number number => number.Data.ToString("R", CultureInfo.InvariantCulture),
        Boolean boolean => boolean.Data ? "true" : "false",
        Nil => "nil",
        List list => "(" + string.Join(" ", list.Items.Select(Print)) + ")",
        Native native => $"<native:{native.Name}>",
        Closure or Wrapped => "<closure>",
        Cell => "<cell>",
        _ => throw new LanguageException("Unsupported value.")
    };
}

public sealed class LanguageException(string message) : Exception(message);

internal sealed class Scope(Scope? parent = null)
{
    private readonly Dictionary<string, Value> bindings = new(StringComparer.Ordinal);
    public Value Get(string key) => bindings.TryGetValue(key, out var value) ? value : parent?.Get(key) ?? throw new LanguageException($"Unbound symbol: {key}");
    public Value Set(string key, Value value) { bindings[key] = value; return value; }
}

public sealed record EvaluationLimits(long Steps = 2_000_000, int Depth = 256);
internal sealed class Evaluation(EvaluationLimits limits)
{
    private long remaining = limits.Steps > 0 ? limits.Steps : throw new ArgumentOutOfRangeException(nameof(limits));
    private readonly int maximumDepth = limits.Depth is >= 1 and <= 512 ? limits.Depth : throw new ArgumentOutOfRangeException(nameof(limits));
    private int depth;
    public void Tick() { if (--remaining < 0) throw new LanguageException("Evaluation step budget exhausted."); }
    public void Enter() { Tick(); if (++depth > maximumDepth) { depth--; throw new LanguageException("Evaluation depth budget exhausted."); } }
    public void Leave() => depth--;
}
