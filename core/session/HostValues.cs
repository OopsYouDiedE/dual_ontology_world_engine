using DuelView.Compiled;
using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

/// <summary>Immutable process-boundary values shared by the game and text hosts.</summary>
public static class HostValues
{
    public static readonly IReadOnlyDictionary<string, object?> Empty =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

    public static object? FromDv(object value) => new Converter().FromDv(value);
    public static IReadOnlyDictionary<string, object?> Record(object value) => new Converter().Record(value);

    /// <summary>A session-owned bridge. Only frozen DV values have cross-call identity.</summary>
    public sealed class Converter
    {
        private sealed class Box(object value) { internal readonly object Value = value; }
        private readonly ConditionalWeakTable<object, Box> values = new();
        private readonly ConditionalWeakTable<object, Box> records = new();
        private readonly ConditionalWeakTable<object, Box> origins = new();

        public object? FromDv(object value)
        {
            if (value is Rational exact)
            {
                if (values.TryGetValue(value, out var known)) return known.Value;
                var rational = Freeze(new Dictionary<string, object?> { ["$dv_rational"] = exact.ToString() });
                values.Add(value, new Box(rational));
                origins.Add(rational, new Box(value));
                return rational;
            }
            if (!R.TryItems(value, out _)) return value;
            var frozen = R.snapshot(value);
            if (values.TryGetValue(frozen, out var shared)) return shared.Value;
            var items = R.Items(frozen);
            if (items.Count > 0 && items.All(item => R.TryItems(item, out var pair) && pair.Count == 2 && pair[0] is string))
            {
                var record = Record(frozen);
                values.Add(frozen, new Box(record));
                return record;
            }
            var converted = new object?[items.Count];
            for (var i = 0; i < items.Count; i++) converted[i] = FromDv(items[i]);
            var list = Array.AsReadOnly(converted);
            values.Add(frozen, new Box(list));
            origins.Add(list, new Box(frozen));
            return list;
        }

        public IReadOnlyDictionary<string, object?> Record(object value)
        {
            var frozen = R.snapshot(value);
            if (records.TryGetValue(frozen, out var shared)) return (IReadOnlyDictionary<string, object?>)shared.Value;
            var items = R.Items(frozen);
            var result = new Dictionary<string, object?>(items.Count, StringComparer.Ordinal);
            foreach (var item in items)
            {
                var pair = R.Items(item);
                result[(string)pair[0]] = FromDv(pair[1]);
            }
            var map = Freeze(result);
            records.Add(frozen, new Box(map));
            origins.Add(map, new Box(frozen));
            return map;
        }

        public object ToDv(object? value) => Write(value, new Dictionary<object, object>(ReferenceEqualityComparer.Instance));

        private object Write(object? value, Dictionary<object, object> converted)
        {
            if (value is null) return false;
            if (value is string or bool or double or Rational) return value;
            if (value is int integer) return (double)integer;
            if (value is long longInteger) return (double)longInteger;
            if (value is float single) return (double)single;
            if (value is JsonElement element) return ReadJson(element);
            // These host nodes were created by this bridge from immutable DV
            // nodes. Reuse the original graph without re-encoding its children.
            if (origins.TryGetValue(value, out var origin)) return origin.Value;
            if (converted.TryGetValue(value, out var previous)) return previous;
            if (value is IReadOnlyDictionary<string, object?> map)
            {
                if (map.TryGetValue("$dv_rational", out var exact)) return Rational.From((string)exact!);
                var result = new List<object>(map.Count);
                converted.Add(value, result);
                foreach (var pair in map) result.Add(new List<object> { pair.Key, Write(pair.Value, converted) });
                return result;
            }
            if (value is IDictionary<string, object?> dictionary)
            {
                var result = new List<object>(dictionary.Count);
                converted.Add(value, result);
                foreach (var pair in dictionary) result.Add(new List<object> { pair.Key, Write(pair.Value, converted) });
                return result;
            }
            if (value is IEnumerable sequence)
            {
                var result = new List<object>();
                converted.Add(value, result);
                foreach (var item in sequence) result.Add(Write(item, converted));
                return result;
            }
            throw new ArgumentException("Unsupported host value: " + value.GetType().Name);
        }
    }

    public static IReadOnlyDictionary<string, object?> Freeze(Dictionary<string, object?> values) =>
        new ReadOnlyDictionary<string, object?>(values);

    public static object ToDv(object? value) => new Converter().ToDv(value);

    public static object Parse(string text)
    {
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 512 });
        return ReadJson(document.RootElement);
    }

    private static object ReadJson(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object when value.TryGetProperty("$dv_rational", out var exact) => Rational.From(exact.GetString()!),
        JsonValueKind.Object => value.EnumerateObject().Select(pair => (object)new List<object> { pair.Name, ReadJson(pair.Value) }).ToList(),
        JsonValueKind.Array => value.EnumerateArray().Select(ReadJson).ToList(),
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw new JsonException("Unsupported DV value")
    };

    public static string Text(IReadOnlyDictionary<string, object?> value, string key, string fallback = "") =>
        value.TryGetValue(key, out var found) ? found as string ?? fallback : fallback;
    public static double Number(IReadOnlyDictionary<string, object?> value, string key, double fallback = 0) =>
        value.TryGetValue(key, out var found) && found is IConvertible number ? number.ToDouble(CultureInfo.InvariantCulture) : fallback;
    public static IReadOnlyDictionary<string, object?> Map(IReadOnlyDictionary<string, object?> value, string key) =>
        value.TryGetValue(key, out var found) && found is IReadOnlyDictionary<string, object?> map ? map : Empty;
    public static IReadOnlyList<object?> List(IReadOnlyDictionary<string, object?> value, string key) =>
        value.TryGetValue(key, out var found) && found is IReadOnlyList<object?> list ? list : Array.Empty<object?>();
}

public sealed record HostStateDelta(long Version, long BaseVersion, string Viewer, bool Reset,
    IReadOnlyDictionary<string, object?> Changed, IReadOnlyList<string> Removed);

public sealed record PreparedPreview(string Token, long BaseVersion, string Selection,
    IReadOnlyDictionary<string, object?> Intent);
