using DuelView.Core;
using DuelView.Compiled;
using static DuelView.Core.DVKernel;

public static partial class CampaignRuntime
{
    // Install only values constructed by the authored frontier template.
    // Old derived candidates are removed before the current observation context
    // is evaluated; repeated refreshes therefore do not duplicate actions.
    public static object F_campaign_navigation_frontier(object actor)
    {
        object diagnostic = F_dv_find(actor, "navigation_route_diagnostic", false);
        if (R.Bool(R.eq(diagnostic, false))) return actor;
        object goals = new List<object>();
        foreach (object goal in R.Items(F_dv_find(F_dv_get(actor, "policy"), "goals", new List<object>())))
            if (!R.Bool(F_dv_find(goal, "navigation_gate_probe", false))) _ = R.push(goals, goal);
        object entries = DvValueTemplate.Instantiate("template.c2_gate_frontier", actor, diagnostic);
        object generated = new List<object>();
        foreach (object goal in R.Items(entries))
        {
            object reference = F_dv_get(goal, "ref");
            if (R.Bool(F_dv_has(generated, reference))) continue;
            _ = R.push(generated, reference);
            _ = R.push(goals, goal);
        }
        diagnostic = F_dv_with_many(diagnostic, new List<object> { F_dv_pair("frontier_generated", R.count(generated)), F_dv_pair("frontier_goal_refs", generated) });
        return F_dv_with_many(actor, new List<object> { F_dv_pair("policy", F_dv_with(F_dv_get(actor, "policy"), "goals", goals)), F_dv_pair("navigation_route_diagnostic", diagnostic) });
    }
    public static object F_campaign_request_withdrawals(object actor)
    {
        object actions = new List<object>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (object action in R.Items(F_dv_get(actor, "actions")))
            if (!R.Bool(F_dv_find(action, "request_withdrawal_action", false))) { _ = R.push(actions, action); ids.Add(R.Text(F_dv_get(action, "id"))); }
        foreach (object action in R.Items(DvValueTemplate.Instantiate("template.c2_request_withdrawals", actor)))
        {
            string id = R.Text(F_dv_get(action, "id"));
            if (!ids.Add(id)) throw new InvalidOperationException($"Request withdrawal action conflicts with existing action {id}");
            _ = R.push(actions, action);
        }
        return F_dv_with(actor, "actions", actions);
    }

    // Route only a goal that explicitly carries an authored task contract.
    // The chosen hop and base action come from this actor's actual known route.
    public static object F_c1_request_navigation(object actor)
    {
        object actions = new List<object>();
        var actionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (object action in R.Items(F_dv_get(actor, "actions")))
            if (!R.Bool(F_dv_find(action, "request_coordination_dynamic", false)))
            { _ = R.push(actions, action); actionIds.Add(R.Text(F_dv_get(action, "id"))); }
        foreach (object action in R.Items(DvValueTemplate.Instantiate("template.c1_request_coordination_actions", actor)))
        {
            string id = R.Text(F_dv_get(action, "id"));
            if (!actionIds.Add(id)) throw new InvalidOperationException($"Request coordination action conflicts with existing action {id}");
            _ = R.push(actions, F_dv_with(action, "request_coordination_dynamic", true));
        }
        actor = F_dv_with(actor, "actions", actions);
        object goals = new List<object>();
        object authored = DvValueTemplate.Instantiate("template.c1_request_coordination_goals", actor);
        foreach (object goal in R.Items(authored)) _ = R.push(goals, F_dv_with(goal, "request_coordination_dynamic", true));
        object mind = F_dv_get(actor, "mind");
        object position = F_dv_read(mind, F_dv_get(actor, "location_ref"));
        foreach (object goal in R.Items(F_dv_get(F_dv_get(actor, "policy"), "goals")))
        {
            if (R.Bool(F_dv_find(goal, "request_coordination_dynamic", false))) continue;
            _ = R.push(goals, goal);
        }
        object resolvedGoals = new List<object>();
        foreach (object goal in R.Items(goals))
        {
            object spec = F_dv_find(goal, "navigation_request_spec", false);
            object resolved = goal;
            if (!R.Bool(R.eq(spec, false)) && R.Bool(F_dvs_navigation_task_completed(actor, spec))) continue;
            if (!R.Bool(R.eq(spec, false)) && R.Bool(F_dv_uncontested(position)))
            {
                object target = F_dv_get(spec, "target");
                object route = F_c1_inquiry_route(actor, target);
                if (!R.Bool(R.eq(route, false)))
                {
                    object origin = F_dv_get(position, "value");
                    object next = F_dv_get(route, "next");
                    object method = F_c1_move_name(F_dv_get(actor, "id"), origin, next);
                    object task = F_dv_with_many(spec, new List<object> { F_dv_pair("origin", origin), F_dv_pair("next", next), F_dv_pair("base_action_id", method) });
                    resolved = F_dv_with(goal, "navigation_task", task);
                }
            }
            _ = R.push(resolvedGoals, resolved);
        }
        return F_dv_with(actor, "policy", F_dv_with(F_dv_get(actor, "policy"), "goals", resolvedGoals));
    }

}
