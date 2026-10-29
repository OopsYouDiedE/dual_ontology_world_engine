using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DuelView.Core;

/// <summary>Fingerprints assembly files on disk at the first observation.</summary>
internal static class HostBuildIdentity
{
    private static readonly Lazy<IReadOnlyDictionary<string, object?>> Snapshot = new(Create);
    internal static IReadOnlyDictionary<string, object?> Info => Snapshot.Value;
    internal static string Hash => (string)Info["sha256"]!;

    private static IReadOnlyDictionary<string, object?> Create()
    {
        var assemblies = new[] { Assembly.GetEntryAssembly(), typeof(DvSession).Assembly, typeof(DVKernel).Assembly }
            .Where(assembly => assembly is not null).Cast<Assembly>()
            .DistinctBy(assembly => assembly.FullName).OrderBy(assembly => assembly.FullName, StringComparer.Ordinal);
        var records = new List<object>();
        var signature = new StringBuilder();
        foreach (var assembly in assemblies)
        {
            var name = assembly.GetName().Name ?? "";
            var path = assembly.Location;
            // A missing file stays explicit. CLI hosts use immutable per-run
            // output directories. This is not an in-memory Godot image hash.
            var hash = string.IsNullOrEmpty(path) || !File.Exists(path) ? "unavailable"
                : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            records.Add(HostValues.Freeze(new Dictionary<string, object?>
            {
                ["assembly"] = name, ["path"] = path, ["sha256"] = hash
            }));
            signature.Append(name).Append(':').Append(hash).Append('\n');
        }
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["scope"] = "assembly-files-at-first-read; CLI per-run output is immutable; not an in-memory image or source-tree revision",
            ["sha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature.ToString()))).ToLowerInvariant(),
            ["assemblies"] = records.AsReadOnly()
        });
    }
}
