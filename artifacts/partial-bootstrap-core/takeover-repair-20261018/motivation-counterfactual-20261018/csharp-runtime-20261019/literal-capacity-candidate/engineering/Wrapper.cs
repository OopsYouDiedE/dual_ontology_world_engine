using DuelView.Compiled;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
public static partial class DVProgram
{
    internal static object NeutralFixture() => F_lc_fixture();
    internal static object NeutralError(object log) => F_lc_error(log);
    internal static object NeutralConstruct(object value) => F_lc_construct(value);
}
static class NeutralStartup
{
    static void Main(string[] args)
    {
        var output = args[0];
        if (args.Length > 1 && args[1] == "performance")
        {
            var values = Enumerable.Range(0, 30000).Select(i => (object)("neutral-" + i)).ToArray();
            var roots = new List<object>(values.Length);
            var warm = DVProgram.NeutralConstruct("warm");
            var watch = new Stopwatch(); var start = GC.GetAllocatedBytesForCurrentThread();
            watch.Start(); foreach (var value in values) roots.Add(DVProgram.NeutralConstruct(value)); watch.Stop();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var root in roots) { digest.AppendData(Encoding.UTF8.GetBytes(R.Text(R.json_stringify(root)))); digest.AppendData(new byte[]{10}); }
            File.WriteAllText(Path.Combine(output, "semantics.sha256"), Convert.ToHexString(digest.GetHashAndReset()));
            File.WriteAllText(Path.Combine(output, "metrics.json"), JsonSerializer.Serialize(new { elapsed_ticks = watch.ElapsedTicks, elapsed_milliseconds = watch.Elapsed.TotalMilliseconds, allocated_bytes = allocated, roots = roots.Count }));
            GC.KeepAlive(roots); GC.KeepAlive(values); GC.KeepAlive(warm); return;
        }
        File.WriteAllText(Path.Combine(output, "semantics.json"), R.Text(R.json_stringify(DVProgram.NeutralFixture())));
        var log = new List<object>();
        try { DVProgram.NeutralError(log); throw new Exception("Expected error was absent"); }
        catch (Exception ex)
        {
            // Compare primary facts only; retain the complete stack independently.
            File.WriteAllText(Path.Combine(output, "exception.json"), JsonSerializer.Serialize(new { type = ex.GetType().FullName, primary_message = ex.Message, preceding_effects = R.Text(R.json_stringify(log)) }));
            File.WriteAllText(Path.Combine(output, "exception.raw.txt"), ex.ToString());
        }
    }
}
