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
        _ => throw new LanguageException("Unsupported value.")
    };
}

public sealed class LanguageException(string message) : Exception(message);
