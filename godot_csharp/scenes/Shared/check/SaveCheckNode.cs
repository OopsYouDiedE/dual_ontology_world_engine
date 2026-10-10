using Godot;
using DualOntologyWorldEngine.Checks;

public partial class SaveCheckNode : Node
{
    public int RunWrite(string directory)=>SessionSaveCheck.Write(directory);
    public int RunRead(string directory)=>SessionSaveCheck.Read(directory);
}
