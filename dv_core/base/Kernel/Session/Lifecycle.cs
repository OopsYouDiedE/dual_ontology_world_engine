using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Session / Lifecycle: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_session_open(object v_story, object v_capability)
    {
        _ = F_dv_require(R.eq(F_dv_get(v_story, "format"), "dv-session-v1"), "Unsupported story data dialect");
        object v_physical__286709 = F_dvs_simulation_open(v_story, F_dv_clone(F_dv_get(v_story, "physical")));
        object v_actors__286781 = F_dv_clone(F_dv_get(v_story, "actors"));
        object v_events__286821 = new System.Collections.Generic.List<object>(0) { };
        object v_bindings__286841 = F_dv_get(v_story, "bindings");
        object v_costs__286875 = F_dv_get(v_story, "cost_model");
        object v_run_start__286908 = F_dv_find(F_dv_get(v_story, "physical"), "time", R.number("0"));
        object v_duration_budget__286963 = F_dv_find(v_story, "game_duration_budget", R.number("0"));
        object v_horizon__287031 = R.number("0");
        object v_duration_blocked__287051 = false;
        object v_limit__287084 = F_dv_get(v_story, "max_actions");
        object v_cursor__287118 = R.number("0");
        object v_executed__287137 = R.number("0");
        object v_idle__287158 = R.number("0");
        object v_status__287175 = "action-window-exhausted";
        object v_initial__287218 = new System.Collections.Generic.List<object>(0) { };
        v_horizon__287031 = R.add(v_run_start__286908, v_duration_budget__286963);
        _ = F_dv_require(R.not(R.lt(v_duration_budget__286963, R.number("0"))), "Negative game duration budget");
        v_initial__287218 = new System.Collections.Generic.List<object>(2) { F_dv_pair("physical", v_physical__286709), F_dv_pair("actors", v_actors__286781) };
        object v_i__287415 = R.number("0");
        while (R.Bool(R.lt(v_i__287415, R.count(v_actors__286781))))
        {
            object v_actor__287467 = F_dvs_initialize_cognition(R.at(v_actors__286781, v_i__287415), F_dv_find(v_story, "opening_seed", R.number("0")), v_i__287415);
            if (R.Bool(F_dv_and(R.eq(v_capability, "disabled"), R.eq(F_dv_get(v_actor__287467, "id"), F_dv_find(v_story, "capability_actor", "")))))
            {
                v_actor__287467 = F_dv_with(v_actor__287467, "policy", F_dv_with(F_dv_get(v_actor__287467, "policy"), "model_budget", R.number("0")));
            }
            v_actors__286781 = F_dvs_replace(v_actors__286781, v_i__287415, F_dv_with(v_actor__287467, "ledger", F_dv_action_cost_ledger(F_dv_get(v_actor__287467, "id"), F_dv_get(v_actor__287467, "balance"))));
            v_i__287415 = R.add(v_i__287415, R.number("1"));
        }
        v_initial__287218 = new System.Collections.Generic.List<object>(2) { F_dv_pair("physical", v_physical__286709), F_dv_pair("actors", v_actors__286781) };
        _ = F_dvs_event(v_events__286821, "session_start", "", F_dv_find(v_physical__286709, "time", R.number("0")), new System.Collections.Generic.List<object>(0) { }, F_dv_with(new System.Collections.Generic.List<object>(3) { F_dv_pair("initial", v_initial__287218), F_dv_pair("capability", v_capability), F_dv_pair("source_refs", F_dv_find(v_story, "source_refs", new System.Collections.Generic.List<object>(0) { })) }, "time_origin", new System.Collections.Generic.List<object>(4) { F_dv_pair("run_start", v_run_start__286908), F_dv_pair("duration_budget", v_duration_budget__286963), F_dv_pair("horizon", v_horizon__287031), F_dv_pair("unit", "minute") }));
        return new System.Collections.Generic.List<object>(16) { F_dv_pair("story", v_story), F_dv_pair("physical", v_physical__286709), F_dv_pair("actors", v_actors__286781), F_dv_pair("events", v_events__286821), F_dv_pair("bindings", v_bindings__286841), F_dv_pair("costs", v_costs__286875), F_dv_pair("run_start", v_run_start__286908), F_dv_pair("duration_budget", v_duration_budget__286963), F_dv_pair("horizon", v_horizon__287031), F_dv_pair("duration_blocked", v_duration_blocked__287051), F_dv_pair("limit", v_limit__287084), F_dv_pair("cursor", v_cursor__287118), F_dv_pair("executed", v_executed__287137), F_dv_pair("idle", v_idle__287158), F_dv_pair("status", v_status__287175), F_dv_pair("initial", v_initial__287218) };
    }

    public static object F_dvs_session_active(object v_state)
    {
        object v_time__288623 = F_dv_find(F_dv_get(v_state, "physical"), "time", R.number("0"));
        if (R.Bool((R.Bool(R.lt(F_dv_get(v_state, "horizon"), v_time__288623)) ? (object)(true) : (object)(R.not(R.lt(F_dv_get(v_state, "executed"), F_dv_get(v_state, "limit")))))))
        {
            return false;
        }
        if (R.Bool(R.eq(v_time__288623, F_dv_get(v_state, "horizon"))))
        {
            if (R.Bool(R.not(R.eq(F_dv_find(v_state, "pending", false), false))))
            {
                return true;
            }
            object v_i__289016 = R.number("0");
            while (R.Bool(R.lt(v_i__289016, R.count(F_dv_get(v_state, "actors")))))
            {
                object v_queued__289080 = F_dv_find(R.at(F_dv_get(v_state, "actors"), v_i__289016), "scheduled_action", false);
                if (R.Bool(R.not(R.eq(v_queued__289080, false))))
                {
                    if (R.Bool(R.not(R.lt(v_time__288623, F_dv_get(v_queued__289080, "due")))))
                    {
                        return true;
                    }
                }
                v_i__289016 = R.add(v_i__289016, R.number("1"));
            }
            return false;
        }
        if (R.Bool(F_dv_has_key(F_dv_get(v_state, "physical"), "simulation")))
        {
            return true;
        }
        return (R.Bool(R.lt(F_dv_get(v_state, "idle"), R.count(F_dv_get(v_state, "actors")))) ? (object)(true) : (object)(R.lt(R.number("0"), F_dvs_scheduled_count(F_dv_get(v_state, "actors")))));
    }

    public static object F_dvs_session_status(object v_state)
    {
        object v_status__345400 = F_dv_get(v_state, "status");
        if (R.Bool(R.not(R.lt(F_dv_get(v_state, "idle"), R.count(F_dv_get(v_state, "actors"))))))
        {
            v_status__345400 = "no-admissible-action-or-budget";
            if (R.Bool(F_dv_get(v_state, "duration_blocked")))
            {
                v_status__345400 = "budget-exhausted";
            }
        }
        if (R.Bool(R.not(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_get(v_state, "horizon")))))
        {
            v_status__345400 = "budget-exhausted";
        }
        if (R.Bool(F_dvs_session_active(v_state)))
        {
            v_status__345400 = "active";
        }
        if (R.Bool(R.lt(R.number("0"), R.count(F_dv_get(v_state, "events")))))
        {
            object v_last__345780 = R.at(F_dv_get(v_state, "events"), R.sub(R.count(F_dv_get(v_state, "events")), R.number("1")));
            if (R.Bool(R.eq(F_dv_get(v_last__345780, "type"), "session_stopped")))
            {
                v_status__345400 = F_dv_get(F_dv_get(v_last__345780, "data"), "reason");
            }
        }
        return v_status__345400;
    }

    public static object F_dvs_trace_snapshot(object v_state)
    {
        object v_story__345966 = F_dv_get(v_state, "story");
        object v_physical__345994 = F_dv_get(v_state, "physical");
        object v_actors__346028 = F_dv_get(v_state, "actors");
        object v_events__346058 = F_dv_get(v_state, "events");
        object v_costs__346088 = F_dv_get(v_state, "costs");
        object v_bindings__346116 = F_dv_get(v_state, "bindings");
        object v_initial__346150 = F_dv_get(v_state, "initial");
        object v_run_start__346182 = F_dv_get(v_state, "run_start");
        object v_duration_budget__346218 = F_dv_get(v_state, "duration_budget");
        object v_horizon__346266 = F_dv_get(v_state, "horizon");
        object v_status__346298 = F_dvs_session_status(v_state);
        return new System.Collections.Generic.List<object>(8) { F_dv_pair("events", v_events__346058), F_dv_pair("initial", v_initial__346150), F_dv_pair("final", new System.Collections.Generic.List<object>(2) { F_dv_pair("physical", v_physical__345994), F_dv_pair("actors", v_actors__346028) }), F_dv_pair("status", v_status__346298), F_dv_pair("time", F_dv_find(v_physical__345994, "time", R.number("0"))), F_dv_pair("game_time_unit", "minute"), F_dv_pair("configuration", new System.Collections.Generic.List<object>(6) { F_dv_pair("cost_model", v_costs__346088), F_dv_pair("formal_answers", F_dv_find(v_story__345966, "formal_answers", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("source_refs", F_dv_find(v_story__345966, "source_refs", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("time_origin", new System.Collections.Generic.List<object>(4) { F_dv_pair("run_start", v_run_start__346182), F_dv_pair("duration_budget", v_duration_budget__346218), F_dv_pair("horizon", v_horizon__346266), F_dv_pair("unit", "minute") }), F_dv_pair("report_labels", F_dv_find(v_story__345966, "report_labels", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("bindings", v_bindings__346116) }), F_dv_pair("scope", F_dv_find(v_story__345966, "execution_scope", "DV formal model; no Godot host input or geometry")) };
    }

    public static object F_dvs_session_close(object v_state)
    {
        v_state = R.snapshot(v_state);
        object v_events__347020 = F_dv_clone(F_dv_get(v_state, "events"));
        if (R.Bool((R.Bool(R.eq(R.count(v_events__347020), R.number("0"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(R.at(v_events__347020, R.sub(R.count(v_events__347020), R.number("1"))), "type"), "session_stopped"))))))
        {
            object v_parent__347152 = F_dv_get(R.at(v_events__347020, R.sub(R.count(v_events__347020), R.number("1"))), "id");
            object v_status__347205 = F_dvs_session_status(v_state);
            if (R.Bool(R.eq(v_status__347205, "active")))
            {
                v_status__347205 = "host-closed-during-active-session";
            }
            _ = F_dvs_event(v_events__347020, "session_stopped", "", F_dv_get(F_dv_get(v_state, "physical"), "time"), new System.Collections.Generic.List<object>(1) { v_parent__347152 }, new System.Collections.Generic.List<object>(2) { F_dv_pair("reason", v_status__347205), F_dv_pair("actions_executed", F_dv_get(v_state, "executed")) });
            _ = R.print(R.json_stringify(new System.Collections.Generic.List<object>(3) { F_dv_pair("type", "session_stopped"), F_dv_pair("status", v_status__347205), F_dv_pair("game_time", F_dv_get(F_dv_get(v_state, "physical"), "time")) }));
        }
        return F_dvs_trace_snapshot(F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(1) { F_dv_pair("events", v_events__347020) }));
    }

    public static object F_dvs_owned_state_update(object v_state, object v_updates)
    {
        object v_result__355095 = new System.Collections.Generic.List<object>(0) { };
        object v_keys__355115 = new System.Collections.Generic.List<object>(0) { };
        object v_i__355133 = R.number("0");
        while (R.Bool(R.lt(v_i__355133, R.count(v_state))))
        {
            object v_key__355182 = R.at(R.at(v_state, v_i__355133), R.number("0"));
            _ = R.push(v_result__355095, F_dv_pair(v_key__355182, F_dv_find(v_updates, v_key__355182, R.at(R.at(v_state, v_i__355133), R.number("1")))));
            _ = R.push(v_keys__355115, v_key__355182);
            v_i__355133 = R.add(v_i__355133, R.number("1"));
        }
        v_i__355133 = R.number("0");
        while (R.Bool(R.lt(v_i__355133, R.count(v_updates))))
        {
            object v_key__355374 = R.at(R.at(v_updates, v_i__355133), R.number("0"));
            if (R.Bool(R.not(F_dv_has(v_keys__355115, v_key__355374))))
            {
                _ = R.push(v_result__355095, F_dv_pair(v_key__355374, R.at(R.at(v_updates, v_i__355133), R.number("1"))));
                _ = R.push(v_keys__355115, v_key__355374);
            }
            v_i__355133 = R.add(v_i__355133, R.number("1"));
        }
        return v_result__355095;
    }
}
