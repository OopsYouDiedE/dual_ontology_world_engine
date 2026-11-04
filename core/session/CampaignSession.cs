using DuelView.Compiled;
using static DuelView.Core.DVKernel;

// Session scheduling preserves the existing object ABI and invokes Core.
// Campaign definitions enter through the shared content adapters.
public static partial class CampaignRuntime
{
    private static object CampaignRecord(params (string Key, object Value)[] fields)
        => fields.Select(field => (object)new List<object> { field.Key, field.Value }).ToList();

    public static object F_dvg_text_ends(object text, object suffix)
        => R.Text(text).EndsWith(R.Text(suffix), StringComparison.Ordinal);

    public static object F_dvg_simulation_pulse(object state, object seconds, object visibleRooms)
        => F_dvs_simulation_pulse(state, seconds, visibleRooms);

    public static object F_dvg_trace_snapshot(object state)
        => F_dvs_trace_snapshot(state);

    public static object F_dvg_prepare(object state)
    {
        var current = F_dvg_extend(state);
        while (R.Bool(F_dvs_session_active(current))
            && F_dv_find(current, "pending", false) is false
            && !R.Bool(F_dv_find(current, "awaiting_player", false))
            && !R.Bool(F_dv_find(current, "host_motion_wait", false)))
        {
            if (R.Text(F_dv_find(current, "player_selection", "")) == "")
            {
                R.phase("schedule-start", "");
                current = F_dvs_schedule(current);
                R.phase("schedule-end", "");
            }
            if (F_dv_find(current, "pending", false) is false && !R.Bool(F_dv_find(current, "host_motion_wait", false)) && R.Bool(F_dvs_session_active(current)))
            {
                R.phase("goals-start", "");
                current = F_dvg_refresh_goals(current);
                R.phase("goals-end", "");
                current = F_dvs_prepare_actor(current);
            }
            if (F_dv_find(current, "pending", false) is false)
                current = F_dvg_extend(current);
        }
        return current;
    }

    public static object F_dvg_complete(object state, object receipt)
    {
        var completed = F_island_case_stamp(F_dvg_extend(F_dvs_session_complete(state, receipt)));
        var story = F_dv_get(completed, "story");
        if (!R.Bool(F_dv_has_key(story, "background_waiting_cursor"))) return completed;
        var cursor = F_dv_get(story, "background_waiting_cursor");
        var current = F_dvs_owned_state_update(completed, CampaignRecord(
            ("story", F_dv_without(story, "background_waiting_cursor")),
            ("cursor", cursor), ("awaiting_player", false), ("player_selection", "")));
        var actor = R.at(F_dv_get(current, "actors"), cursor);
        if (F_dv_find(actor, "in_flight_action", false) is not false)
            return F_dvs_owned_state_update(current, CampaignRecord(("awaiting_player", true)));
        return F_dv_find(actor, "scheduled_action", false) is not false
            ? F_dvg_prepare(current)
            : F_dvs_prepare_actor(F_dvg_refresh_goals(current));
    }

    public static object F_dvg_prepare_background(object state, object viewer)
    {
        if (!R.Bool(F_dv_find(state, "awaiting_player", false))
            || F_dv_find(state, "pending", false) is not false
            || !R.Bool(F_dvs_session_active(state))) return state;

        state = F_dvs_interrupt_waits(F_dvs_simulation_sync(state));
        var waiting = F_dv_get(state, "cursor");
        var story = F_dv_get(state, "story");
        var index = F_dv_find(story, "background_scan_cursor", R.add(waiting, 1d));
        var count = R.Items(F_dv_get(state, "actors")).Count;
        for (var offset = 0; offset < count; offset++)
        {
            if (!R.Bool(R.lt(index, (double)count))) index = 0d;
            var physical = F_dv_get(state, "physical");
            var now = F_dv_get(physical, "time");
            var actor = F_dvs_erosion_update(R.at(F_dv_get(state, "actors"), index), physical, now);
            var queued = F_dv_find(actor, "scheduled_action", false);
            if (!R.Bool(R.eq(F_dv_get(actor, "id"), viewer)) && F_dv_find(actor, "in_flight_action", false) is false && R.Bool(F_dvs_actor_can_act(actor)))
            {
                var backgroundStory = F_dv_with_many(story, CampaignRecord(
                    ("background_waiting_cursor", waiting), ("background_scan_cursor", R.add(index, 1d))));
                var current = F_dvs_owned_state_update(state, CampaignRecord(
                    ("story", backgroundStory),
                    ("actors", F_dvs_replace(F_dv_get(state, "actors"), index, actor)),
                    ("cursor", index), ("awaiting_player", false), ("player_selection", "")));
                if (queued is not false)
                {
                    if (!R.Bool(R.lt(now, F_dv_get(queued, "due"))))
                        return F_dvs_dispatch_due(current, index);
                }
                else
                {
                    var mind = F_dv_get(actor, "mind");
                    var fresh = !R.Bool(R.eq(F_dv_find(actor, "decision_time", -1d), now))
                        || !R.Bool(R.eq(F_dv_find(actor, "decision_revision", -1d), F_dv_get(mind, "revision")))
                        || R.Items(F_dv_find(actor, "pending_heard", new List<object>())).Count > 0
                        || R.Bool(F_dvs_idle_inputs_changed(actor, state));
                    if (R.Bool(R.lt(now, F_dv_get(state, "horizon"))) && fresh
                        && R.Bool(R.lt(0d, F_dv_find(F_dv_get(actor, "policy"), "budget", 0d)))
                        && R.Bool(F_dvs_simulation_actor_ready(actor, state))
                        && !R.Bool(F_dvs_idle_guard_matches(actor, state)))
                    {
                        var prepared = F_dvs_prepare_actor(F_dvg_refresh_goals(current));
                        if (F_dv_find(prepared, "pending", false) is not false) return prepared;
                        return F_dvs_owned_state_update(prepared, CampaignRecord(
                            ("story", F_dv_without(F_dv_get(prepared, "story"), "background_waiting_cursor")),
                            ("cursor", waiting), ("awaiting_player", true), ("player_selection", "")));
                    }
                }
            }
            index = R.add(index, 1d);
        }
        return state;
    }
}
