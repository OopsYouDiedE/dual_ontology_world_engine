namespace DualOntologyWorldEngine.Game.FirstAwakening;

public sealed record RedTokenResult(bool Ok, string Message)
{
    public IReadOnlyDictionary<string, string> Predictions { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<string> Conflicts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Unproven { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RedTokenFrame> Snapshots { get; init; } = Array.Empty<RedTokenFrame>();
    public IReadOnlyList<string> Events { get; init; } = Array.Empty<string>();
}
