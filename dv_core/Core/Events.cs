using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Session / Events: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_event(object v_events, object v_kind, object v_actor, object v_time, object v_parents, object v_data)
    {
        object v_id__892 = R.cat("e", R.show(R.count(v_events)));
        object v_frozen_event__944 = R.snapshot(new System.Collections.Generic.List<object>(6) { F_dv_pair("id", v_id__892), F_dv_pair("type", v_kind), F_dv_pair("actor", v_actor), F_dv_pair("time", v_time), F_dv_pair("parents", v_parents), F_dv_pair("data", F_dvs_event_storage(v_kind, v_data)) });
        _ = R.raw_event(v_frozen_event__944);
        _ = R.push(v_events, v_frozen_event__944);
        return v_id__892;
    }

    public static object F_dvs_event_storage(object v_kind, object v_data)
    {
        if (R.Bool(R.audit_mode()))
        {
            return v_data;
        }
        object v_keep__1415 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.eq(v_kind, "inference")))
        {
            v_keep__1415 = new System.Collections.Generic.List<object>(2) { "mind_before", "mind_after" };
        }
        if (R.Bool((R.Bool(R.eq(v_kind, "observation")) ? (object)(true) : (object)(R.eq(v_kind, "listener_parse")))))
        {
            v_keep__1415 = new System.Collections.Generic.List<object>(1) { "mind_after" };
        }
        if (R.Bool(R.eq(v_kind, "factor_prune")))
        {
            v_keep__1415 = new System.Collections.Generic.List<object>(1) { "actor_before" };
        }
        object v_result__1657 = new System.Collections.Generic.List<object>(0) { };
        object v_index__1677 = R.number("0");
        while (R.Bool(R.lt(v_index__1677, R.count(v_data))))
        {
            object v_entry__1733 = R.at(v_data, v_index__1677);
            if (R.Bool((R.Bool(R.not(F_dv_has(new System.Collections.Generic.List<object>(6) { "mind_before", "mind_after", "physical_before", "physical_after", "actor_before", "actor_after" }, R.at(v_entry__1733, R.number("0"))))) ? (object)(true) : (object)(F_dv_has(v_keep__1415, R.at(v_entry__1733, R.number("0")))))))
            {
                if (R.Bool((R.Bool(R.eq(v_kind, "action_execution")) ? (object)(R.eq(R.at(v_entry__1733, R.number("0")), "receipt")) : (object)(false))))
                {
                    object v_execution__2239 = F_dv_get(R.at(v_entry__1733, R.number("1")), "execution");
                    object v_world__2290 = F_dv_get(v_execution__2239, "world");
                    object v_reference__2334 = R.raw_ref(v_world__2290);
                    if (R.Bool(R.not(R.eq(v_reference__2334, false))))
                    {
                        object v_cut__2595 = new System.Collections.Generic.List<object>(5) { F_dv_pair("world", F_dv_get(v_world__2290, "id")), F_dv_pair("revision", F_dv_get(v_world__2290, "revision")), F_dv_pair("time", F_dv_find(v_world__2290, "time", R.number("0"))), F_dv_pair("raw_ref", v_reference__2334), F_dv_pair("storage", "raw-dag-world-reference") };
                        object v_compact__2768 = F_dv_with(F_dv_without(v_execution__2239, "world"), "world_cut", v_cut__2595);
                        _ = R.push(v_result__1657, F_dv_pair("receipt", F_dv_with(R.at(v_entry__1733, R.number("1")), "execution", v_compact__2768)));
                    }
                    else
                    {
                        _ = R.push(v_result__1657, v_entry__1733);
                    }
                }
                else
                {
                    _ = R.push(v_result__1657, v_entry__1733);
                }
            }
            v_index__1677 = R.add(v_index__1677, R.number("1"));
        }
        return v_result__1657;
    }

    public static object F_dvs_find_event_by_id(object v_events, object v_id)
    {
        v_events = R.snapshot(v_events);
        if (R.Bool((R.Bool(R.lt(R.number("1"), R.length(v_id))) ? (object)(R.eq(R.chr(v_id, R.number("0")), "e")) : (object)(false))))
        {
            object v_num_str__3235 = R.slice(v_id, R.number("1"), R.sub(R.length(v_id), R.number("1")));
            if (R.Bool(R.is_number(v_num_str__3235)))
            {
                object v_idx__3330 = R.number(v_num_str__3235);
                if (R.Bool((R.Bool(R.not(R.lt(v_idx__3330, R.number("0")))) ? (object)(R.lt(v_idx__3330, R.count(v_events))) : (object)(false))))
                {
                    object v_candidate__3429 = R.at(v_events, v_idx__3330);
                    if (R.Bool(R.eq(F_dv_get(v_candidate__3429, "id"), v_id)))
                    {
                        return v_candidate__3429;
                    }
                }
            }
        }
        object v_i__3539 = R.sub(R.count(v_events), R.number("1"));
        while (R.Bool(R.not(R.lt(v_i__3539, R.number("0")))))
        {
            object v_event__3596 = R.at(v_events, v_i__3539);
            if (R.Bool(R.eq(F_dv_get(v_event__3596, "id"), v_id)))
            {
                return v_event__3596;
            }
            v_i__3539 = R.sub(v_i__3539, R.number("1"));
        }
        return false;
    }

    public static object F_dvs_cognitive_parents(object v_actor, object v_parents)
    {
        object v_previous__3754 = F_dv_find(v_actor, "last_cognitive_event", "");
        if (R.Bool(R.eq(v_previous__3754, "")))
        {
            return v_parents;
        }
        else
        {
            return F_dv_explanation_merge(v_parents, F_dv_single(v_previous__3754));
        }
    }

    public static object F_dvs_owned_events_stable(object v_events, object v_actor, object v_cursor)
    {
        v_events = R.snapshot(v_events);
        object v_i__27658 = v_cursor;
        while (R.Bool(R.lt(v_i__27658, R.count(v_events))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(v_events, v_i__27658), "actor"), F_dv_get(v_actor, "id"))))
            {
                if (R.Bool(R.not(F_dv_has(new System.Collections.Generic.List<object>(4) { "planning_stop", "planning_stop_reused", "planning_yield", "action_wait" }, F_dv_get(R.at(v_events, v_i__27658), "type")))))
                {
                    return false;
                }
            }
            v_i__27658 = R.add(v_i__27658, R.number("1"));
        }
        return true;
    }
}
