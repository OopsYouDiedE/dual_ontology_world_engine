using System.Globalization;
using System.Threading;
using System.Collections;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography;

namespace DuelView.Compiled;

// Exact arithmetic is a value ABI, independent of cognitive or plot semantics.
[JsonConverter(typeof(RationalJsonConverter))]
public sealed record Rational
{
    private static readonly Rational Zero = new(BigInteger.Zero, BigInteger.One);
    private static readonly Rational One = new(BigInteger.One, BigInteger.One);
    private static readonly Rational MinusOne = new(BigInteger.MinusOne, BigInteger.One);
    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }
    public Rational(BigInteger n, BigInteger d)
    {
        if (d.IsZero) throw new DivideByZeroException();
        if (d.Sign < 0) { n = -n; d = -d; }
        if (d.IsOne) { Numerator = n; Denominator = BigInteger.One; return; }
        if (n.IsZero) { Numerator = BigInteger.Zero; Denominator = BigInteger.One; return; }
        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(n), d);
        Numerator = n / gcd; Denominator = d / gcd;
    }
    public static Rational From(object value)
    {
        if (value is Rational q) return q;
        string text;
        if (value is double d)
        {
            d = R.Num(d);
            if (d == 0) return Zero;
            if (d == 1) return One;
            if (d == -1) return MinusOne;
            // Within the safe-integer range, the round-trip decimal spelling
            // denotes this exact integer. Larger values and fractions must keep
            // the original decimal conversion, not their binary significand.
            if (d >= -9007199254740991d && d <= 9007199254740991d && d == Math.Truncate(d))
                return new Rational(new BigInteger((long)d), BigInteger.One);
            text = d.ToString("R", CultureInfo.InvariantCulture);
        }
        else text = R.Text(value);
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
    public Rational Add(Rational b)
    {
        if (b.Numerator.IsZero) return this;
        if (Numerator.IsZero) return b;
        return Denominator == b.Denominator
            ? new Rational(Numerator + b.Numerator, Denominator)
            : new Rational(Numerator*b.Denominator+b.Numerator*Denominator, Denominator*b.Denominator);
    }
    public Rational Subtract(Rational b)
    {
        if (b.Numerator.IsZero) return this;
        return Denominator == b.Denominator
            ? new Rational(Numerator - b.Numerator, Denominator)
            : new Rational(Numerator*b.Denominator-b.Numerator*Denominator, Denominator*b.Denominator);
    }
    public Rational Multiply(Rational b)
    {
        if (Numerator.IsZero || b.Numerator.IsZero) return Zero;
        if (b.Numerator.IsOne && b.Denominator.IsOne) return this;
        if (Numerator.IsOne && Denominator.IsOne) return b;
        return new Rational(Numerator*b.Numerator, Denominator*b.Denominator);
    }
    public Rational Divide(Rational b)
    {
        if (b.Numerator.IsZero) throw new DivideByZeroException();
        if (Numerator.IsZero) return Zero;
        if (b.Numerator.IsOne && b.Denominator.IsOne) return this;
        return new Rational(Numerator*b.Denominator, Denominator*b.Numerator);
    }
    public int CompareTo(Rational b) => Denominator == b.Denominator
        ? Numerator.CompareTo(b.Numerator) : (Numerator*b.Denominator).CompareTo(b.Numerator*Denominator);
    public override string ToString() => Denominator.IsOne ? Numerator.ToString(CultureInfo.InvariantCulture) : Numerator.ToString(CultureInfo.InvariantCulture)+"/"+Denominator.ToString(CultureInfo.InvariantCulture);
}
public sealed class RationalJsonConverter : JsonConverter<Rational>
{
    public override Rational Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => Rational.From(reader.GetString()!);
    public override void Write(Utf8JsonWriter writer, Rational value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}
