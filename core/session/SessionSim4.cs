using System.Text.Json;
using DuelView.Compiled;
using DuelView.Core;

public sealed partial class DvSession
{
    private sealed record SavedSim4Plan(string Id, long StateVersion, Sim4Planner.Plan Plan);
    private SavedSim4Plan? savedSim4Plan;

    private (Sim4Planner.Input Input, IReadOnlyList<object> Excluded) Sim4Input()
    {
        using var valuesScope = R.UseValues(valueContext);
        if (IsClosed || HasPending(state) || HostValues.Text(Intent, "status") != "awaiting_input")
            throw new InvalidOperationException("Sim4 planning requires the current player's committed input boundary.");
        EnsureObservers();
        var viewer = Viewer;
        var actor = observerSubjects[viewer];
        var profile = Field(Field(actor, "policy", new List<object>()), "sims_autonomy", false);
        if (profile is false) throw new InvalidOperationException("Current actor has no DV autonomy profile.");
        static Dictionary<string, double> Numbers(object record) => R.Items(record).ToDictionary(
            pair => R.Text(R.at(pair, 0d)), pair => R.Num(R.at(pair, 1d)), StringComparer.Ordinal);
        var needs = Numbers(DVKernel.F_dvs_autonomy_needs(actor));
        var weights = Numbers(Field(profile, "autonomy_weights", new List<object>()));
        var decay = Numbers(Field(profile, "need_decay_per_second", new List<object>()));
        if (needs.Count == 0) throw new InvalidOperationException("Current actor has no declared commodity needs.");
        var actions = R.Items(Field(actor, "actions", new List<object>())).ToDictionary(x => R.Text(Field(x, "id", "")), StringComparer.Ordinal);
        var predictions = Field(profile, "action_predictions", new List<object>());
        var options = HostValues.List(Intent, "options").OfType<IReadOnlyDictionary<string, object?>>().ToArray();
        var candidates = new List<Sim4Planner.Candidate>(); var excluded = new List<object>();
        foreach (var option in options)
        {
            var id = HostValues.Text(option, "id");
            if (!actions.TryGetValue(id, out var action)) { excluded.Add(new { action = id, reason = "no-owned-action" }); continue; }
            var effects = Field(action, "perceived_need_effects", false);
            if (effects is false) effects = Field(predictions, R.Text(Field(action, "kind", "")), false);
            if (effects is false) { excluded.Add(new { action = id, reason = "no-declared-need-prediction" }); continue; }
            var declaredSeconds = Field(action, "predicted_seconds", false);
            var seconds = declaredSeconds is not false ? R.Num(declaredSeconds)
                : R.Num(Field(converter.ToDv(HostValues.Map(option, "cost")), "time", 0d)) * 60;
            if (!double.IsFinite(seconds) || seconds <= 0) { excluded.Add(new { action = id, reason = "no-positive-declared-duration" }); continue; }
            // Current options have already passed the formal actor-owned admission rules.
            // No predicted observations or objective effects are copied from an action here.
            // Later queue entries remain conditional and must be admitted again after completion.
            candidates.Add(new(id, seconds, Numbers(effects), [], [], [], []));
        }
        var revision = R.show(Field(Field(actor, "mind", new List<object>()), "revision", 0d));
        var input = new Sim4Planner.Input(viewer, Version + ":" + content.Hash + ":" + revision,
            needs, weights, decay, R.Num(Field(profile, "time_cost", 0d)), [], candidates);
        return (input, excluded);
    }

    public object PlanSim4(int depth = 1, int beamWidth = 32, int expansionBudget = 4096)
    {
        var (input, excluded) = Sim4Input();
        var plan = Sim4Planner.Build(input, new(depth, beamWidth, expansionBudget));
        var saved = new SavedSim4Plan(Guid.NewGuid().ToString("N"), Version, plan);
        savedSim4Plan = saved;
        return new
        {
            policy = "sim4", plan_id = saved.Id, state_version = Version, content_hash = content.Hash,
            actor = input.Actor, candidate_count = input.Candidates.Count, excluded, plan,
            verified = Sim4Planner.Verify(input, plan),
            scope = "current-owned-options-with-declared-need-predictions",
            future_steps = "conditional-replan-after-each-real-completion",
            need_source = "current-actor-DV-autonomy-profile", execution = "first-action-through-session-prepare"
        };
    }

    public object ExecuteSim4(string planId, long stateVersion)
    {
        if (savedSim4Plan is not { } saved || saved.Id != planId) throw new InvalidOperationException("Unknown or consumed Sim4 plan. Plan from the latest observation first.");
        if (stateVersion != Version || saved.StateVersion != Version) throw new InvalidOperationException("Stale Sim4 plan. Replan after the current action or state change.");
        var (input, _) = Sim4Input();
        var verified = Sim4Planner.Verify(input, saved.Plan);
        if (!verified.Valid) throw new InvalidOperationException("Sim4 plan no longer matches the actor's input: " + string.Join("; ", verified.Errors));
        if (saved.Plan.Queue.Count == 0) throw new InvalidOperationException("No currently available action has a declared need prediction.");
        var first = saved.Plan.Queue[0].Action;
        if (!HostValues.List(Intent, "options").OfType<IReadOnlyDictionary<string, object?>>().Any(x => HostValues.Text(x, "id") == first))
            throw new InvalidOperationException("Planned first action is no longer a current input option.");
        savedSim4Plan = null; // A token authorizes one normal submission, never a repeated completion.
        var intent = PrepareValue(first);
        return new
        {
            policy = "sim4", plan_id = planId, actor = input.Actor, action = first,
            submitted = true, completed = false, state_version = Version, intent,
            continuation = "host-completion-then-new-sim4-plan"
        };
    }
}
