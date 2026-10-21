using DuelView.Compiled;
using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;

/// <summary>Immutable process-boundary values shared by the game and text hosts.</summary>
public static class HostValues
{
    public static readonly IReadOnlyDictionary<string, object?> Empty =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

    public static object? FromDv(object value)
    {
        if (value is Rational exact)
            return Freeze(new Dictionary<string, object?> { ["$dv_rational"] = exact.ToString() });
        if (!R.TryItems(value, out var items)) return value;
        if (items.Count > 0 && items.All(item => R.TryItems(item, out var pair) && pair.Count == 2 && pair[0] is string))
            return Record(value);
        return Array.AsReadOnly(items.Select(FromDv).ToArray());
    }

    public static IReadOnlyDictionary<string, object?> Record(object value)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var item in R.Items(value))
        {
            var pair = R.Items(item);
            result[(string)pair[0]] = FromDv(pair[1]);
        }
        return Freeze(result);
    }

    public static IReadOnlyDictionary<string, object?> Freeze(Dictionary<string, object?> values) =>
        new ReadOnlyDictionary<string, object?>(values);

    public static object ToDv(object? value) => value switch
    {
        null => false,
        string or bool or double or Rational => value,
        int number => (double)number,
        long number => (double)number,
        float number => (double)number,
        JsonElement element => ReadJson(element),
        IReadOnlyDictionary<string, object?> map when map.TryGetValue("$dv_rational", out var exact) => Rational.From((string)exact!),
        IReadOnlyDictionary<string, object?> map => map.Select(pair => (object)new List<object> { pair.Key, ToDv(pair.Value) }).ToList(),
        IDictionary<string, object?> map => map.Select(pair => (object)new List<object> { pair.Key, ToDv(pair.Value) }).ToList(),
        IEnumerable values => values.Cast<object?>().Select(ToDv).ToList(),
        _ => throw new ArgumentException("Unsupported host value: " + value.GetType().Name)
    };

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
