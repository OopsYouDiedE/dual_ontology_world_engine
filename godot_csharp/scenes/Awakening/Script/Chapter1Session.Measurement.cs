namespace DualOntologyWorldEngine.Awakening;

public sealed partial class Chapter1Session
{
    // Passive copies of already committed progress. No inference, acquisition,
    // simulation advance, input, or verdict evaluation occurs in this reader.
    public IReadOnlyDictionary<string, object?> ReadMilestoneProgress()
        => new Dictionary<string, object?>
        {
            ["chapter"] = ChapterId, ["game_minutes"] = _world.GameMinutes,
            ["stage"] = Stage,
            ["examined"] = Session.ReadProgress("examined"),
            ["experiments"] = Session.ReadProgress("experiments"),
            ["answer"] = Session.ReadProgress("answer_state"),
            ["replay_count"] = State.GetValueOrDefault("replay_count"),
            ["replay_result"] = Session.ReadProgress("replay_result")
        };
}
