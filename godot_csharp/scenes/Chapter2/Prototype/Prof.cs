using System.Diagnostics;
namespace DualOntologyWorldEngine.Chapter2.Prototype;
/// <summary>分段计时（只在 prof 模式开启；检查会并行跑多局，默认关闭以免共享计时器）。</summary>
public static class Prof
{
    public static bool Enabled;
    public static readonly Dictionary<string, (Stopwatch W, long N)> T = [];
    private static readonly Stop Nothing = new(null);
    public static IDisposable Time(string k)
    {
        if (!Enabled) return Nothing;
        if (!T.TryGetValue(k, out var e)) T[k] = e = (new Stopwatch(), 0);
        T[k] = (e.W, e.N + 1);
        e.W.Start();
        return new Stop(e.W);
    }
    private sealed class Stop(Stopwatch? w) : IDisposable { public void Dispose() => w?.Stop(); }
    public static void Print() { foreach (var (k, (w, n)) in T.OrderByDescending(x => x.Value.W.ElapsedMilliseconds)) Console.WriteLine($"{k}: {w.ElapsedMilliseconds} ms / {n}"); }
}
