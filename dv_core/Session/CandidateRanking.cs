using DuelView.Compiled;

public sealed partial class DvSession
{
    private long candidateRankingVersion = -1;
    private IReadOnlyDictionary<string, object?> candidateRanking = HostValues.Empty;

    /// <summary>Diagnostic scores only. Does not select or commit an action.</summary>
    public IReadOnlyDictionary<string, object?> CliCandidateRanking(bool engineControlled)
    {
        using var valuesScope = R.UseValues(valueContext);
        if (candidateRankingVersion == Version) return candidateRanking;
        var intent = Intent;
        var manual = HostValues.Text(intent, "status") == "awaiting_input";
        var prepared = engineControlled && HostValues.Text(intent, "status") == "prepared" && HostValues.Text(intent, "actor") == Viewer;
        candidateRanking = HostValues.Empty;
        candidateRankingVersion = Version;
        if (!manual && !prepared) return candidateRanking;
        EnsureObservers();
        var actor = observerSubjects[Viewer];
        var rows = new List<(object Score, IReadOnlyDictionary<string, object?> Row)>();
        string source, eventId = "", strategy;
        if (manual)
        {
            source = "current-panel-reference";
            strategy = "current-owned-utility";
            var view = F_dv_with(F_dv_with(actor, "manual_input", true), "decision_time",
                Field(Field(state, "physical", new List<object>()), "time", 0d));
            var actions = R.Items(Field(actor, "actions", new List<object>()));
            foreach (var option in HostValues.List(intent, "options").OfType<IReadOnlyDictionary<string, object?>>())
            {
                var id = HostValues.Text(option, "id");
                var action = actions.FirstOrDefault(item => R.Text(Field(item, "id", "")) == id);
                if (action is null) continue;
                var score = Run(() => F_dvs_current_action_score(view, action, Field(state, "bindings", new List<object>()),
                    F_dvp_goal_refs(view), Field(Field(Field(state, "costs", new List<object>()), R.Text(Field(action, "kind", "")), new List<object>()), "actual", new List<object>())), speculative: true);
                var total = Field(score, "total", false);
                if (!R.Bool(R.is_numeric_value(total))) continue;
                rows.Add((total, HostValues.Freeze(new Dictionary<string, object?>
                {
                    ["id"] = id, ["label"] = HostValues.Text(option, "label"),
                    ["score"] = converter.FromDv(total), ["score_text"] = R.show(total),
                    ["score_structure"] = converter.FromDv(score), ["components_recorded"] = true
                })));
            }
        }
        else
        {
            var entry = LatestObserverEvent(Viewer, "planning_choice");
            if (entry is null) return candidateRanking;
            var choice = Field(entry, "data", new List<object>());
            var selectedId = HostValues.Text(HostValues.Map(intent, "action"), "id");
            if (R.Text(Field(Field(choice, "action", new List<object>()), "id", "")) != selectedId) return candidateRanking;
            source = "recorded-engine-decision";
            eventId = R.Text(Field(entry, "id", ""));
            strategy = R.Text(Field(choice, "strategy", ""));
            foreach (var plan in R.Items(Field(choice, "plans", new List<object>())))
            {
                var path = R.Items(Field(plan, "path", new List<object>()));
                var score = Field(plan, "score", false);
                if (path.Count == 0 || !R.Bool(R.is_numeric_value(score))) continue;
                var id = R.Text(path[0]);
                var action = R.Items(Field(actor, "actions", new List<object>())).FirstOrDefault(item => R.Text(Field(item, "id", "")) == id);
                var label = action is null ? id : R.Text(Run(() => CampaignRuntime.ObserverActionLabel(R.snapshot(state), Viewer, action), speculative: true));
                var components = Field(Field(plan, "value", new List<object>()), "current_score", false);
                rows.Add((score, HostValues.Freeze(new Dictionary<string, object?>
                {
                    ["id"] = id, ["label"] = label, ["score"] = converter.FromDv(score), ["score_text"] = R.show(score),
                    ["path"] = converter.FromDv(Field(plan, "path", new List<object>())),
                    ["score_scope"] = converter.FromDv(Field(plan, "score_scope", "")),
                    ["score_structure"] = components is false ? null : converter.FromDv(components),
                    ["components_recorded"] = components is not false, ["plan"] = converter.FromDv(plan),
                    ["engine_selected"] = id == selectedId
                })));
            }
        }
        // Sort exact DV numbers. Equal scores retain their original catalogue order.
        var sorted = rows.OrderBy(row => row.Score, Comparer<object>.Create((a, b) =>
            R.Bool(R.lt(a, b)) ? 1 : R.Bool(R.lt(b, a)) ? -1 : 0)).Take(5).Select(row => row.Row).ToArray();
        candidateRanking = HostValues.Freeze(new Dictionary<string, object?>
        {
            ["source"] = source, ["event"] = eventId, ["strategy"] = strategy, ["version"] = Version,
            ["actor"] = Viewer, ["candidate_count"] = rows.Count, ["top_limit"] = 5, ["top"] = sorted
        });
        return candidateRanking;
    }
}
