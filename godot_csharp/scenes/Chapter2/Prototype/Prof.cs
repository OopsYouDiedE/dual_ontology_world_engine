using System.Diagnostics;
namespace DualOntologyWorldEngine.Chapter2.Prototype;
public static class Prof
{
    public static readonly Dictionary<string, (Stopwatch W, long N)> T = [];
    public static IDisposable Time(string k)
    {
        if (!T.TryGetValue(k, out var e)) T[k] = e = (new Stopwatch(), 0);
        T[k] = (e.W, e.N + 1);
        e.W.Start();
        return new Stop(e.W);
    }
    private sealed class Stop(Stopwatch w) : IDisposable { public void Dispose() => w.Stop(); }
    public static void Print() { foreach (var (k, (w, n)) in T.OrderByDescending(x => x.Value.W.ElapsedMilliseconds)) Console.WriteLine($"{k}: {w.ElapsedMilliseconds} ms / {n}"); }
}
