using System.Diagnostics;
using System.Text.Json;

namespace DuelView.Compiled;

// Diagnostic build only. Inclusive and exclusive times use a nested call stack.
internal static class DvProfiler
{
    private struct Frame(string name, long start)
    {
        internal string Name = name;
        internal long Start = start;
        internal long ChildTicks;
    }

    private sealed class Total
    {
        public long Calls { get; set; }
        public long InclusiveTicks { get; set; }
        public long ExclusiveTicks { get; set; }
    }

    [ThreadStatic] private static List<Frame>? frames;
    private static readonly Dictionary<string, Total> totals = new(StringComparer.Ordinal);
    private static readonly object gate = new();
    private static readonly long started = Stopwatch.GetTimestamp();

    static DvProfiler()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Write();
    }

    internal readonly struct Scope : IDisposable
    {
        public void Dispose()
        {
            var stack = frames!;
            var end = Stopwatch.GetTimestamp();
            var frame = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            var inclusive = end - frame.Start;
            var exclusive = inclusive - frame.ChildTicks;
            if (stack.Count != 0)
            {
                var parent = stack[^1];
                parent.ChildTicks += inclusive;
                stack[^1] = parent;
            }
            lock (gate)
            {
                if (!totals.TryGetValue(frame.Name, out var total))
                    totals[frame.Name] = total = new Total();
                total.Calls++;
                total.InclusiveTicks += inclusive;
                total.ExclusiveTicks += exclusive;
            }
        }
    }

    internal static Scope Enter(string name)
    {
        (frames ??= new List<Frame>(32)).Add(new Frame(name, Stopwatch.GetTimestamp()));
        return default;
    }

    private static void Write()
    {
        var path = Environment.GetEnvironmentVariable("DV_PROFILE_PATH");
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            lock (gate)
            {
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                JsonSerializer.Serialize(stream, new
                {
                    frequency = Stopwatch.Frequency,
                    process_ticks = Stopwatch.GetTimestamp() - started,
                    functions = totals
                });
            }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("DV profiler output failed: " + error.Message);
        }
    }
}
