using System.Collections.Concurrent;
using System.Text.Json;

namespace DualOntologyWorldEngine.TestSupport;

// Linked only by check/tool projects. Expectations are checked-in snapshots,
// never derived from production truth tables or the algorithm under test.
internal static class FixtureData
{
    private static readonly ConcurrentDictionary<string, JsonElement> Cache = new();

    public static JsonElement Sample(string fixture, string key) => Root(fixture).GetProperty(key);
    public static string SampleText(string fixture, string key) => Sample(fixture, "texts").GetProperty(key).GetString()
        ?? throw new InvalidDataException($"Null fixture text: {fixture}/{key}");
    public static string[] SampleStrings(string fixture, string key) => Sample(fixture, key).EnumerateArray()
        .Select(value => value.GetString() ?? throw new InvalidDataException($"Null fixture ID: {fixture}/{key}")).ToArray();
    public static int[] SampleInts(string fixture, string key) => Sample(fixture, key).EnumerateArray().Select(value => value.GetInt32()).ToArray();
    public static T SampleAs<T>(string fixture, string key) => Sample(fixture, key).Deserialize<T>()
        ?? throw new InvalidDataException($"Invalid fixture: {fixture}/{key}");

    private static JsonElement Root(string fixture) => Cache.GetOrAdd(fixture, name =>
    {
        using var stream = typeof(FixtureData).Assembly.GetManifestResourceStream($"Fixtures.{name}.json")
            ?? throw new FileNotFoundException($"Missing fixture: {name}");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    });
}
