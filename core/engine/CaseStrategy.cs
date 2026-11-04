using System.Text.Json;
using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Actor-owned admission and immediate utility. The host supplies only the actor's theory.
/// This does not infer an objective obligation or forecast the outcome of an unexecuted action.</summary>
public static class CaseStrategy
{
    private static readonly AsyncLocal<Func<object, object, string, IReadOnlyList<CaseStrategyChoice>>?> Current = new();

    public static IDisposable Use(Func<object, object, string, IReadOnlyList<CaseStrategyChoice>> evaluate)
    {
        var previous = Current.Value;
        Current.Value = evaluate;
        return new Scope(() => Current.Value = previous);
    }
    private sealed class Scope(Action restore) : IDisposable
    {
        private Action? restoreAction = restore;
        public void Dispose() => Interlocked.Exchange(ref restoreAction, null)?.Invoke();
    }
    public static IReadOnlyList<CaseStrategyChoice> ForAction(object actor, object mind, object action)
        => Current.Value?.Invoke(actor, mind, R.Text(DVKernel.F_dv_get(action, "id"))) ?? Array.Empty<CaseStrategyChoice>();
    public static bool Ready(object actor, object mind, object action) => ForAction(actor, mind, action).All(x => x.Ready);
    public static double Utility(object actor, object mind, object action)
    {
        var utility = ForAction(actor, mind, action).Where(x => x.Ready).Sum(x => x.Utility);
        return double.IsFinite(utility) ? utility : throw new InvalidDataException("Combined strategy utility exceeds finite range.");
    }

    public static IReadOnlyList<CaseStrategyChoice> Evaluate(CaseTheory theory, string actor, JsonElement profile, string? action = null)
        => StrategyJudgment.Evaluate(theory, actor, profile, action);
    public static StrategyJudgment.CaseStrategyWork BeginEvaluation(CaseTheory theory, string actor, JsonElement profile, string? action = null, int maxNodes = 10000, int maxMatches = 1000000)
        => StrategyJudgment.BeginEvaluation(theory, actor, profile, action, maxNodes, maxMatches);
    public static IReadOnlyList<CaseStrategyChoice> AwaitingEvidence(string actor, JsonElement profile, string version)
        => StrategyJudgment.AwaitingEvidence(actor, profile, version);
}