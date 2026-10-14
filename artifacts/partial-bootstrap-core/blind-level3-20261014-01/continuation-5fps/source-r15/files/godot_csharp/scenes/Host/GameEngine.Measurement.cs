public partial class GameEngine
{
    // Measurement is read-only; no legacy Core is loaded by the production host.
    public string ReadMilestoneProgressJson() => session?.Snapshot("detective") ?? "{}";
}
