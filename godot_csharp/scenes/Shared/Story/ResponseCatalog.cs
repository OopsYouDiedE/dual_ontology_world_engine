using System.Globalization;
using System.Text.Json;

namespace DualOntologyWorldEngine.Story;

/// <summary>Read-only expression selection. The caller supplies the speaker's known facts.</summary>
public static class ResponseCatalog
{
    public sealed record Response(string Id, string Text, JsonElement Declaration);

    public static Response? Select(JsonElement table, string group, Func<string, object?> read)
    {
        if (!table.TryGetProperty(group, out var rows)) return null;
        return rows.EnumerateObject().Where(row => Applies(row.Value, read))
            .OrderByDescending(row => row.Value.TryGetProperty("priority", out var p) ? p.GetInt32() : 0)
            .Select(row => new Response(row.Name, row.Value.GetProperty("text").GetString()!, row.Value)).FirstOrDefault();
    }

    public static bool Applies(JsonElement row, Func<string, object?> read)
        => !row.TryGetProperty("when", out var terms) || terms.EnumerateObject().All(term => Match(read(term.Name), term.Value));

    private static bool Match(object? actual, JsonElement expected)
    {
        if (actual is null) return false;
        var value = actual is bool flag ? (flag ? "true" : "false") : Convert.ToString(actual, CultureInfo.InvariantCulture) ?? "";
        var test = expected.ValueKind == JsonValueKind.String ? expected.GetString()! : expected.GetRawText();
        foreach (var op in new[] { ">=", "<=", "!=", ">", "<" })
        {
            if (!test.StartsWith(op, StringComparison.Ordinal)) continue;
            var rhs = test[op.Length..];
            if (op == "!=") return value != rhs;
            if (!double.TryParse(value, CultureInfo.InvariantCulture, out var a) || !double.TryParse(rhs, CultureInfo.InvariantCulture, out var b)) return false;
            return op switch { ">=" => a >= b, "<=" => a <= b, ">" => a > b, _ => a < b };
        }
        return value == test;
    }
}
