public partial class GameEngine
{
    // This side channel is never passed to the detective or other subjects.
    public string ReadMilestoneProgressJson()
        => _session?.ReadMilestoneProgressJson() ?? "{\"available\":false}";
}
