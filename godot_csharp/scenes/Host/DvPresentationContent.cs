using Godot;
using DuelView.Core;

/// <summary>Read-only presentation projection through the shared C# DV parser.</summary>
public partial class DvPresentationContent : RefCounted
{
    public Godot.Collections.Dictionary ReadPublishedData(string filename, string identity)
    {
        if (System.IO.Path.GetFileName(filename) != filename)
            throw new System.ArgumentException("Expected a published content filename.");
        return ReadRecord(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Content", filename), "data", identity);
    }
    public string PublishedDataSha256(string filename)
    {
        if (System.IO.Path.GetFileName(filename) != filename)
            throw new System.ArgumentException("Expected a published content filename.");
        var path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Content", filename);
        return System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(path))).ToLowerInvariant();
    }
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
