public partial class GameEngine
{
    // Read the same completed host projection; never traverse a worker-owned DV state.
    public string ReadMilestoneProgressJson() => snapshotJson;
}
