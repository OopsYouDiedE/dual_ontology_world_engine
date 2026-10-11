namespace DualOntologyWorldEngine.Story;

public sealed partial class ChapterSession
{
    // Unlike GetProgress, an absent group remains absent after this read.
    public IReadOnlyDictionary<string, object> ReadProgress(string group)
        => _progress.TryGetValue(group, out var values)
            ? values.ToDictionary(p => p.Key, p => p.Value)
            : new Dictionary<string, object>();
}
