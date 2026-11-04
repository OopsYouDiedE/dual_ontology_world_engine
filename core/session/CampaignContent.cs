using System.Text;
using DuelView.Compiled;
using DuelView.Core;

/// <summary>Loads the published DV content pack for both formal hosts.</summary>
internal static class CampaignContent
{
    // Godot loads managed assemblies from memory; the host base directory still
    // identifies the published output containing the shared DV content pack.
    private static string DirectoryPath => Path.Combine(AppContext.BaseDirectory, "Content");

    internal static ContentCatalog Read(string? source = null, string? path = null)
    {
        var selected = source is not null ? ContentCatalog.Parse(source)
            : ContentCatalog.Load(path ?? Path.Combine(DirectoryPath, "world.dv"));
        if (selected.DataCount > 0) return ValidatePack(selected);
        var paths = Directory.GetFiles(DirectoryPath, "*.data.dv");
        Array.Sort(paths, StringComparer.Ordinal);
        if (paths.Length == 0) throw new FileNotFoundException("The published campaign DV data pack is missing.", DirectoryPath);
        var sources = new List<string> { selected.Source };
        foreach (var file in paths) sources.Add(File.ReadAllText(file, Encoding.UTF8));
        return ValidatePack(ContentCatalog.ParseBundle(sources, selected.Path));
    }

    /// <summary>Restores exactly the definitions recorded with the session.</summary>
    internal static ContentCatalog Restore(string source)
    {
        var selected = ContentCatalog.Parse(source);
        if (selected.DataCount == 0)
            throw new InvalidDataException("This saved session has no recorded campaign data pack. Start a new session with the migrated content.");
        return ValidatePack(selected);
    }

    internal static ContentCatalog ValidatePack(ContentCatalog catalog)
    {
        var manifest = catalog.Get("data", "campaign.pack");
        if (manifest is false)
            throw new InvalidDataException("Campaign data pack is missing its campaign.pack manifest.");
        var value = R.record_find(manifest, "value", false);
        if (!R.Bool(R.eq(R.record_find(value, "version", false), 1d)))
            throw new InvalidDataException("Unsupported campaign data pack version.");
        if (!R.TryItems(R.record_find(value, "definitions", false), out var definitions))
            throw new InvalidDataException("Campaign pack definitions must be an array of data identities.");
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (definition is not string { Length: > 0 } id || !declared.Add(id))
                throw new InvalidDataException("Campaign pack contains an invalid or duplicate data identity.");
            if (catalog.Get("data", id) is false)
                throw new InvalidDataException("Campaign pack is missing a declared definition: " + id);
        }
        foreach (var entry in R.Items(R.record_find(catalog.Data, "data", new List<object>())))
        {
            var id = R.Text(R.Items(entry)[0]);
            if (id != "campaign.pack" && !declared.Contains(id))
                throw new InvalidDataException("Campaign data is absent from its pack manifest: " + id);
        }
        if (declared.Count == 0 || declared.Count == 1 && declared.Contains("campaign.pack"))
            throw new InvalidDataException("Campaign pack contains no content definitions.");
        SpatialPublication.Validate(catalog, value);
        DvValueTemplate.Validate(catalog);
        return catalog;
    }
}
