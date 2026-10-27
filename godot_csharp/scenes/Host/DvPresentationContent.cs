using Godot;
using DuelView.Core;

/// <summary>Read-only presentation projection through the shared C# DV parser.</summary>
public partial class DvPresentationContent : RefCounted
{
    public Godot.Collections.Dictionary ReadRecord(string path, string category, string identity)
    {
        try
        {
            var source = Godot.FileAccess.GetFileAsString(path);
            var document = DvScriptReader.Parse(source);
            if (document.GetProperty("schema").GetString() != ContentCatalog.Schema)
                throw new System.IO.InvalidDataException("Unsupported DV presentation schema");
            var record = document.GetProperty(category).GetProperty(identity);
            return Json.ParseString(record.GetRawText()).AsGodotDictionary();
        }
        catch (System.Exception error)
        {
            GD.PushError($"Cannot read DV presentation {path}/{identity}: {error.Message}");
            return new();
        }
    }
}
