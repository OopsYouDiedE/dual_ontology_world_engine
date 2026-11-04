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

// Shared runtime implementation; state remains owned by the execution context.
public static partial class R
{
    public static object read_view(object value)
    {
        var caches = Caches;
        if (!caches.ReadViewEnabled || value is FrozenList) return value;
        return UnchangedSnapshot(value, caches) ?? value;
    }
    // The view must not turn reference-valued subjects or lookup keys into
    // different identities. This non-throwing guard never thaws input branches.
    // Invalid/unusual refs retain every original read and its error ordering.
    public static object read_ref_view_safe(object reference)
    {
        if (!Caches.ReadViewEnabled || !TryItems(reference, out var pairs)) return BoolFalse;
        object? objectId = null, facet = null, field = null, subject = null;
        var hasSubject = false;
        for (var i = 0; i < pairs.Count; i++)
        {
            if (!TryItems(pairs[i], out var pair) || pair.Count < 2 || pair[0] is not string key) return BoolFalse;
            switch (key)
            {
                case "object": objectId = pair[1]; break;
                case "facet": facet = pair[1]; break;
                case "field": field = pair[1]; break;
                case "subject": subject = pair[1]; hasSubject = true; break;
            }
        }
        return objectId is string && facet is string && field is string && (!hasSubject || subject is string)
            ? BoolTrue : BoolFalse;
    }
    public static string Text(object x) => x as string ?? throw new Exception("Expected Text");
    public static double Num(object x) => x is double n && double.IsFinite(n) ? n : throw new Exception("Expected finite Number");
    public static bool Bool(object x) => x is bool b ? b : throw new Exception("Expected Bool");
    public static IReadOnlyList<object> Items(object x) => TryItems(x, out var items)
        ? items : throw new Exception("Expected List");
    // Read interface is not a value-domain predicate: external arrays and other
    // IReadOnlyList implementations are deliberately not accepted as DV lists.
    public static bool IsDvList(object value) => value is List<object> or FrozenList;
    public static bool TryItems(object value, out IReadOnlyList<object> items)
    {
        switch (value)
        {
            case List<object> mutable: items = mutable; return true;
            case FrozenList frozen: items = frozen; return true;
            default: items = null!; return false;
        }
    }
    private static readonly object NumberZero = 0d;
    private static readonly object NumberOne = 1d;
    private static readonly object NumberMinusOne = -1d;
    // Numeric atoms are immutable. Reuse small exact integers without sharing
    // mutable value state; preserve the distinct binary representation of -0.
    private static readonly object[] SmallNumbers = CreateSmallNumbers();
    private static object[] CreateSmallNumbers()
    {
        var values = new object[1025];
        values[0] = NumberZero; values[1] = NumberOne;
        for (var i = 2; i < values.Length; i++) values[i] = (double)i;
        return values;
    }
    private static object NumberValue(double number)
    {
        if (!double.IsFinite(number)) throw new Exception("Expected finite Number");
        if (number >= 0 && number < SmallNumbers.Length && number == Math.Truncate(number))
        {
            if (number != 0 || BitConverter.DoubleToInt64Bits(number) == 0) return SmallNumbers[(int)number];
        }
        if (number == -1) return NumberMinusOne;
        return number;
    }
    public static object number(object s)
    {
        // Validate Text before selecting exact finite spellings. All other
        // input, including negative zero, keeps the original parse/error path.
        var text = Text(s);
        return text switch
        {
            "0" => NumberZero,
            "1" => NumberOne,
            "-1" => NumberMinusOne,
            _ => NumberValue(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture))
        };
    }
    public static object is_number(object s) => double.TryParse(Text(s), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n);
    // Runtime value predicate; unlike is_number, this never parses or requires Text.
    public static object is_text(object value) => value is string;
    public static object is_numeric_value(object value) => value is double n && double.IsFinite(n) || value is Rational q && q.Denominator.Sign > 0;
    public static object add(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Add(Rational.From(b)) : NumberValue(Num(a) + Num(b));
    public static object sub(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Subtract(Rational.From(b)) : NumberValue(Num(a) - Num(b));
    public static object mul(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Multiply(Rational.From(b)) : NumberValue(Num(a) * Num(b));
    public static object div(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Divide(Rational.From(b)) : NumberValue(Num(a) / Num(b));
    public static object lt(object a, object b) => (a is Rational || b is Rational ? Rational.From(a).CompareTo(Rational.From(b)) < 0 : Num(a) < Num(b)) ? BoolTrue : BoolFalse;
    private static bool EqValue(object a, object b) => (a is Rational || b is Rational) && (a is Rational || a is double) && (b is Rational || b is double) ? Rational.From(a).Equals(Rational.From(b)) : Equals(a, b);
    private static readonly object BoolTrue = true;
    private static readonly object BoolFalse = false;
    public static object eq(object a, object b) => EqValue(a, b) ? BoolTrue : BoolFalse;
    public static object exact(object a) => Rational.From(a);
    public static object unit(object a)
    {
        var value = Rational.From(a);
        if (value.CompareTo(Rational.From(0d)) < 0 || value.CompareTo(Rational.From(1d)) > 0) throw new Exception("Numeric refinement requires 0 <= value <= 1");
        return value;
    }
    public static object floor(object a) => NumberValue(Math.Floor(Num(a)));
    public static object not(object a) => Bool(a) ? BoolFalse : BoolTrue;
    public static object cat(object a, object b) => Text(a) + Text(b);
    // Concatenate the text fragments used by session presentation.
    public static object text_join(object values)
    {
        var result = new StringBuilder();
        foreach (var value in Items(values)) result.Append(Text(value));
        return result.ToString();
    }
    public static object length(object a) => NumberValue(Text(a).Length);
    private static int Index(object a) { var n = Num(a); return n >= 0 && n <= int.MaxValue && n == Math.Truncate(n) ? (int)n : throw new Exception("Invalid index"); }
    public static object slice(object a, object start, object count) => Text(a).Substring(Index(start), Index(count));
    public static object chr(object a, object i) => Text(a)[Index(i)].ToString();
    public static object code(object a) => Text(a).Length == 1 ? (double)Text(a)[0] : throw new Exception("Expected one UTF-16 unit");
    public static object show(object a) => a switch { string s => s, Rational q => q.ToString(), double n => n.ToString("R", CultureInfo.InvariantCulture), bool b => b ? "true" : "false", List<object> list => FormatList(list), FrozenList frozen => FormatList(frozen), _ => throw new Exception("Cannot format value") };
    private static string FormatList(IReadOnlyList<object> list)
    {
        var sb = new StringBuilder("[");
        for (var i = 0; i < list.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(show(list[i]));
        }
        sb.Append(']');
        return sb.ToString();
    }
    public static object replace(object list, object index, object value) { Invalidate(list); MutableItems(list)[Index(index)] = value; return list; }
    // Trusted newly created roots keep metadata beside the same List<object>
    // ABI. State remains lazy; external and cross-context lists still follow
    // MutableStateTable's existing weak-table fallback.
    private static List<object> NewMutableList(int capacity)
    {
        var caches = Caches;
        return caches.RuntimeSpeedEnabled
            ? new OwnedList(capacity, caches.MutableStates)
            : new List<object>(capacity);
    }
    // Strongly typed CLR bridge for Core's value constructors, like Items;
    // this is not an additional DV script primitive or a new value domain.
    public static List<object> NewValueList(int capacity) => NewMutableList(capacity);
    public static object list() => NewMutableList(0);
    public static object push(object a, object b)
    {
        var items = MutableItems(a);
        if (!MutableStates.TryGetValue(items, out var state))
        {
            items.Add(b);
            return a;
        }
        // Retain only a certified, unchanged immutable prefix. A later write to
        // any old child invalidates this prefix through the ordinary owner links.
        var prefix = state.Backbone ?? state.AppendBase;
        Invalidate(a);
        items.Add(b);
        state.AppendBase = prefix;
        return a;
    }
    public static object at(object a, object i) => ReadItem(a, Index(i));
    // Persistent spine only: mirror dvs_replace's equality and selected reads.
    public static object list_with_index(object items, object index, object value)
    {
        var source = Items(items);
        var result = NewMutableList(source.Count);
        if (index is double selected)
        {
            // The source position is always a finite non-negative integer.
            // Double equality therefore has the same result as eq here,
            // including negative zero, fractions and non-finite indices.
            for (var i = 0; i < source.Count; i++)
                result.Add(i == selected ? value : ReadItem(items, i));
            return result;
        }
        for (var i = 0; i < source.Count; i++)
            result.Add(Bool(eq(NumberValue(i), index)) ? value : ReadItem(items, i));
        return result;
    }
    public static object count(object a) => a switch
    {
        FrozenList frozen => NumberValue(frozen.Count),
        List<object> mutable => NumberValue(mutable.Count),
        _ => NumberValue(Items(a).Count)
    };
    private static object RawItem(object owner, int index) => owner is FrozenList frozen
        ? frozen[index] : (owner as List<object> ?? throw new Exception("Expected List"))[index];
    // Tiny records stay linear until repeated lookup amortizes an index.
}
