using System.Collections.Immutable;

namespace DualOntology.Language;

public static class Bootstrap
{
    public static readonly ImmutableArray<string> Modules = ["00-base.dv", "10-evaluator.dv", "20-world.dv", "30-logic.dv", "40-belief.dv", "45-deduction.dv", "50-planning.dv"];
    public static string Source(string module)
    {
        if (!Modules.Contains(module)) throw new ArgumentException("Unknown bootstrap module.", nameof(module));
        var assembly = typeof(Bootstrap).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(module, StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
        return reader.ReadToEnd();
    }
    public static Interpreter CreateSeed()
    {
        var interpreter = new Interpreter();
        foreach (var module in Modules)
        {
            try { interpreter.Evaluate(Source(module)); }
            catch (LanguageException ex) { throw new LanguageException($"{module}: {ex.Message}"); }
        }
        return interpreter;
    }
    /// <summary>Every module, including the evaluator, is evaluated by dv-eval. No host eval is exposed.</summary>
    public static Value LoadSelf(Interpreter seed, EvaluationLimits? limits = null)
    {
        var budget = limits ?? new EvaluationLimits(20_000_000);
        var environment = seed.Call("dv-root");
        foreach (var module in Modules) seed.Call("dv-load", budget, new Value.List(Reader.ReadAll(Source(module))), environment);
        return environment;
    }
}

/// <summary>The bootstrap-loaded interpreter executes all exported inference functions.</summary>
public sealed class InferenceProgram
{
    private readonly Interpreter seed = Bootstrap.CreateSeed();
    private readonly Value environment;
    public InferenceProgram() => environment = Bootstrap.LoadSelf(seed);
    public Value Call(string name, params Value[] arguments)
    {
        var function = seed.Call("dv-get", environment, new Value.Symbol(name));
        return seed.Call("dv-apply", new EvaluationLimits(100_000_000), function, new Value.List([.. arguments]));
    }
}
