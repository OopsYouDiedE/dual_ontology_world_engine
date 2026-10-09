using System.Text.Json;

// Neutral, isolated compile adapters. They do not load game data or simulate the host.
// The linked production planner dictates the mechanically required six-field shape.
namespace DualOntologyWorldEngine.Awakening;

public enum Acquaintance { Strangers, Other }
public sealed record NightVariables(int V1, int V2, int V3, int V4, int V5, Acquaintance V6);
public enum FindingStatus { Unvisited, Match, Conflict }
public sealed record Finding(string Evidence, FindingStatus Status);
public sealed record NeutralPrediction(NightVariables Variables, IReadOnlyDictionary<string, int> Facts);
public static class AwakeningCase
{
    public static IReadOnlyList<NeutralPrediction> Predictions { get; } =
    [
        new(new(0, 0, 0, 0, 0, Acquaintance.Strangers), new Dictionary<string, int> { ["signal"] = 7 }),
        new(new(1, 0, 0, 0, 0, Acquaintance.Strangers), new Dictionary<string, int> { ["signal"] = 7 })
    ];
}
public sealed class NeutralProgress
{
    private readonly Dictionary<string, Dictionary<string, object>> _groups = [];
    public IReadOnlyDictionary<string, object> GetProgress(string group)
        => _groups.TryGetValue(group, out var value) ? value : new Dictionary<string, object>();
    public bool Has(string group, string key) => GetProgress(group).GetValueOrDefault(key) is true;
    public void Set(string group, string key, object value)
    {
        if (!_groups.TryGetValue(group, out var values)) _groups[group] = values = [];
        values[key] = value;
    }
}
public sealed class AwakeningContentSession(JsonDocument content)
{
    public JsonDocument Content { get; } = content;
    public NeutralProgress Session { get; } = new();
    public bool V6Open { get; set; } = true;
}
