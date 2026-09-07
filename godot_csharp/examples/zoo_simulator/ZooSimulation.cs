namespace DualOntologyWorldEngine.Examples.ZooSimulator;

/// <summary>终端与浏览器共用的模拟入口；表现层仅控制推进与展示。</summary>
public sealed class ZooSimulation : IDisposable
{
    public ZooWorld World { get; }
    public ZooRules Rules { get; }
    public ZooAnomalies Anomalies { get; }

    public ZooSimulation(int seed = 42, int visitors = 4, double startMinute = 540, string preset = "standard")
    {
        World = new ZooWorld(seed, visitors, startMinute);
        Rules = new ZooRules(World);
        Rules.ApplyPreset(preset);
        Anomalies = new ZooAnomalies(World);
        ZooMinds.ActivateAll(World, Rules);
    }

    public void Dispose()
    {
        Anomalies.Dispose();
        World.Dispose();
    }
}
