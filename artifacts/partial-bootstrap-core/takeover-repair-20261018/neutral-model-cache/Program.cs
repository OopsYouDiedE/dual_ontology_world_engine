using System.Reflection;
using DuelView.Compiled;
public static class ProbeHost
{
    public static void Main()
    {
        var method = typeof(DVProgram).GetMethod("F_neutral_model_cache_probe", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                     ?? throw new Exception("Generated neutral probe missing");
        Console.WriteLine(R.json_stringify(method.Invoke(null, null)!));
    }
}
