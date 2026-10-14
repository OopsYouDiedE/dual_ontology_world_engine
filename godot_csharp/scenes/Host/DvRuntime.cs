using Godot;

/// <summary>Godot's serialization boundary to the compiled DV session.</summary>
public partial class DvRuntime : RefCounted
{
    private DvSession session = null!;

    public void Open(string scenarioJson) => session = DvSession.Open(scenarioJson);
    public void Restore(string snapshotJson) => session = DvSession.Restore(snapshotJson);
    public string Save() => session.Save();
    public bool Active() => session.Active;
    public string Prepare() => session.Prepare();
    public string Complete(string receiptJson, string viewer) => session.Complete(receiptJson, viewer);
    public string Trace() => session.Trace();
}
