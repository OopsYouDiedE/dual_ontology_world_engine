using System.Globalization;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Text;
using System.Text.Json;

namespace DuelView.Compiled;

// Platform/value ABI only. No reader, AST walker, evaluator or code generator.
public static class R
{
    public static string[] Arguments = [];
    public static string Text(object x) => x as string ?? throw new Exception("Expected Text");
    public static double Num(object x) => x is double n && double.IsFinite(n) ? n : throw new Exception("Expected finite Number");
    public static bool Bool(object x) => x is bool b ? b : throw new Exception("Expected Bool");
    public static List<object> Items(object x) => x as List<object> ?? throw new Exception("Expected List");
    public static object number(object s) => Num(double.Parse(Text(s), NumberStyles.Float, CultureInfo.InvariantCulture));
    public static object is_number(object s) => double.TryParse(Text(s), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n);
    public static object add(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Add(Rational.From(b)) : Num(Num(a) + Num(b));
    public static object sub(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Subtract(Rational.From(b)) : Num(Num(a) - Num(b));
    public static object mul(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Multiply(Rational.From(b)) : Num(Num(a) * Num(b));
    public static object div(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Divide(Rational.From(b)) : Num(Num(a) / Num(b));
    public static object lt(object a, object b) => a is Rational || b is Rational ? Rational.From(a).CompareTo(Rational.From(b)) < 0 : Num(a) < Num(b);
    public static object eq(object a, object b) => (a is Rational || b is Rational) && (a is Rational || a is double) && (b is Rational || b is double) ? Rational.From(a).Equals(Rational.From(b)) : Equals(a, b);
    public static object exact(object a) => Rational.From(a);
    public static object unit(object a)
    {
        var value = Rational.From(a);
        if (value.CompareTo(Rational.From(0d)) < 0 || value.CompareTo(Rational.From(1d)) > 0) throw new Exception("Numeric refinement requires 0 <= value <= 1");
        return value;
    }
    public static object floor(object a) => Math.Floor(Num(a));
    public static object not(object a) => !Bool(a);
    public static object cat(object a, object b) => Text(a) + Text(b);
    public static object length(object a) => (double)Text(a).Length;
    private static int Index(object a) { var n = Num(a); return n >= 0 && n <= int.MaxValue && n == Math.Truncate(n) ? (int)n : throw new Exception("Invalid index"); }
    public static object slice(object a, object start, object count) => Text(a).Substring(Index(start), Index(count));
    public static object chr(object a, object i) => Text(a)[Index(i)].ToString();
    public static object code(object a) => Text(a).Length == 1 ? (double)Text(a)[0] : throw new Exception("Expected one UTF-16 unit");
    public static object show(object a) => a switch { string s => s, Rational q => q.ToString(), double n => n.ToString("R", CultureInfo.InvariantCulture), bool b => b ? "true" : "false", _ => throw new Exception("Cannot format value") };
    public static object id_start(object c) => Text(c) == "_" || char.IsLetter(Text(c), 0);
    public static object id_part(object c) => Text(c) == "_" || char.IsLetterOrDigit(Text(c), 0) || CharUnicodeInfo.GetUnicodeCategory(Text(c), 0) == UnicodeCategory.NonSpacingMark;
    public static object normalize(object s) => Text(s).Normalize(NormalizationForm.FormC);
    public static object replace(object list, object index, object value) { Items(list)[Index(index)] = value; return list; }
    public static object list() => new List<object>();
    public static object push(object a, object b) { Items(a).Add(b); return a; }
    public static object at(object a, object i) => Items(a)[Index(i)];
    public static object count(object a) => (double)Items(a).Count;
    public static object record_index(object record, object key)
    {
        var entries = Items(record);
        for (var i = entries.Count - 1; i >= 0; i--)
            if (Bool(eq(Items(entries[i])[0], key))) return (double)i;
        return -1d;
    }
    public static object record_find(object record, object key, object fallback)
    {
        var index = (double)record_index(record, key);
        return index < 0 ? fallback : Items(Items(record)[(int)index])[1];
    }
    public static object is_list(object a) => a is List<object>;
    public static object copy_value(object value)
    {
        if (value is not List<object> items) return value;
        var copy = new List<object>(items.Count);
        foreach (var item in items) copy.Add(copy_value(item));
        return copy;
    }
    public static object fail(object a) => throw new Exception(Text(a));
    public static object arg(object i) => Arguments[Index(i)];
    public static object arg_count() => (double)Arguments.Length;
    public static object json_parse(object text)
    {
        using var document = JsonDocument.Parse(Text(text), new JsonDocumentOptions { MaxDepth = 256 });
        return ReadJson(document.RootElement);
    }
    private static object ReadJson(JsonElement item) => item.ValueKind switch
    {
        JsonValueKind.Array => item.EnumerateArray().Select(ReadJson).ToList(),
        JsonValueKind.String => item.GetString()!,
        JsonValueKind.Number => Num(item.GetDouble()),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw new Exception("DV persistence supports arrays, text, finite numbers and booleans")
    };
    public static object json_stringify(object value) => JsonSerializer.Serialize(value);
    public static object json_write(object path, object value)
    {
        var destination = Text(path);
        var partial = destination + ".partial";
        using (var stream = new FileStream(partial, FileMode.CreateNew, FileAccess.Write))
            JsonSerializer.Serialize(stream, value);
        File.Move(partial, destination, false);
        return true;
    }
    public static object read(object p) => File.ReadAllText(Text(p), new UTF8Encoding(false, true));
    public static object write(object p, object s) { File.WriteAllText(Text(p), Text(s), new UTF8Encoding(false, true)); return s; }
    public static object print(object a) { Console.WriteLine(Text(show(a))); return a; }
    public static object Block(Func<object> f) => f();
}


// Exact arithmetic is a value ABI, independent of cognitive or plot semantics.
[JsonConverter(typeof(RationalJsonConverter))]
public sealed record Rational
{
    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }
    public Rational(BigInteger n, BigInteger d)
    {
        if (d.IsZero) throw new DivideByZeroException();
        if (d.Sign < 0) { n = -n; d = -d; }
        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(n), d);
        Numerator = n / gcd; Denominator = d / gcd;
    }
    public static Rational From(object value)
    {
        if (value is Rational q) return q;
        var text = value is double d ? R.Num(d).ToString("R", CultureInfo.InvariantCulture) : R.Text(value);
        var slash = text.IndexOf('/');
        if (slash >= 0) return new(BigInteger.Parse(text[..slash], CultureInfo.InvariantCulture), BigInteger.Parse(text[(slash+1)..], CultureInfo.InvariantCulture));
        var exponent = text.IndexOfAny(['e', 'E']);
        int power = 0;
        if (exponent >= 0) { power = int.Parse(text[(exponent+1)..], CultureInfo.InvariantCulture); text = text[..exponent]; }
        var point = text.IndexOf('.');
        if (point >= 0) { power -= text.Length - point - 1; text = text.Remove(point, 1); }
        var n = BigInteger.Parse(text, CultureInfo.InvariantCulture);
        return power >= 0 ? new(n * BigInteger.Pow(10, power), 1) : new(n, BigInteger.Pow(10, -power));
    }
    public Rational Add(Rational b) => new(Numerator*b.Denominator+b.Numerator*Denominator, Denominator*b.Denominator);
    public Rational Subtract(Rational b) => new(Numerator*b.Denominator-b.Numerator*Denominator, Denominator*b.Denominator);
    public Rational Multiply(Rational b) => new(Numerator*b.Numerator, Denominator*b.Denominator);
    public Rational Divide(Rational b) => new(Numerator*b.Denominator, Denominator*b.Numerator);
    public int CompareTo(Rational b) => (Numerator*b.Denominator).CompareTo(b.Numerator*Denominator);
    public override string ToString() => Denominator.IsOne ? Numerator.ToString(CultureInfo.InvariantCulture) : Numerator.ToString(CultureInfo.InvariantCulture)+"/"+Denominator.ToString(CultureInfo.InvariantCulture);
}
public sealed class RationalJsonConverter : JsonConverter<Rational>
{
    public override Rational Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => Rational.From(reader.GetString()!);
    public override void Write(Utf8JsonWriter writer, Rational value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}
