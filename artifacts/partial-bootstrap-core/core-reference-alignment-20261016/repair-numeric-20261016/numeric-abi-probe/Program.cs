using DuelView.Compiled;
using System.Text.Json;
var inputs = new (string Name, object Value)[] {
    ("finite-double", 0.25d), ("zero", 0d), ("negative", -1d),
    ("exact-rational", Rational.From("1/3")), ("exact-integer", Rational.From("2")),
    ("numeric-text", "0.25"), ("plain-text", "value"), ("true", true), ("false", false),
    ("empty-list", new List<object>()), ("list", new List<object>{"item"}),
    ("record", new List<object>{new List<object>{"policy_key", "drive"}}),
    ("NaN", double.NaN), ("positive-infinity", double.PositiveInfinity),
    ("negative-infinity", double.NegativeInfinity)
};
foreach (var input in inputs)
    Console.WriteLine(JsonSerializer.Serialize(new { kind="numeric-value", input=input.Name, actual=R.is_numeric_value(input.Value) }));
Console.WriteLine(JsonSerializer.Serialize(new {kind="text-parser", input="0.25", actual=R.is_number("0.25")}));
Console.WriteLine(JsonSerializer.Serialize(new {kind="text-parser", input="value", actual=R.is_number("value")}));
try { R.is_number(0.25d); Console.WriteLine("unexpected non-text acceptance"); }
catch (Exception error) { Console.WriteLine(JsonSerializer.Serialize(new {kind="text-parser-nontext", error=error.Message})); }
