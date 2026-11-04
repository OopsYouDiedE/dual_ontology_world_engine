using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Session / PlayerControl: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_set_player(object v_state, object v_actor)
    {
        _ = F_dv_require(R.eq(F_dv_find(v_state, "pending", false), false), "Cannot change controller during execution");
        object v_found__376238 = R.eq(v_actor, "");
        object v_i__376266 = R.number("0");
        while (R.Bool(R.lt(v_i__376266, R.count(F_dv_get(v_state, "actors")))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(F_dv_get(v_state, "actors"), v_i__376266), "id"), v_actor)))
            {
                v_found__376238 = true;
            }
            v_i__376266 = R.add(v_i__376266, R.number("1"));
        }
        _ = F_dv_require(v_found__376238, "Unknown player actor");
        return F_dvs_control_state(v_state, F_dv_get(v_state, "actors"), F_dv_get(v_state, "events"), F_dv_get(v_state, "cursor"), v_actor, "", false);
    }

    public static object F_dvs_control_state(object v_state, object v_actors, object v_events, object v_cursor, object v_player, object v_selection, object v_waiting)
    {
        return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(6) { F_dv_pair("actors", v_actors), F_dv_pair("events", v_events), F_dv_pair("cursor", v_cursor), F_dv_pair("player_actor", v_player), F_dv_pair("player_selection", v_selection), F_dv_pair("awaiting_player", v_waiting) });
    }

    public static object F_dvs_manual_selection(object v_selection)
    {
        if (R.Bool(R.is_text(v_selection)))
        {
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("action_id", v_selection), F_dv_pair("hypothesis_id", "") };
        }
        _ = F_dv_require(R.is_list(v_selection), "Player selection must be an action ID or data envelope");
        _ = F_dv_require(R.is_text(F_dv_find(v_selection, "action_id", false)), "Player action ID must be text");
        _ = F_dv_require(R.is_text(F_dv_find(v_selection, "hypothesis_id", "")), "Player hypothesis ID must be text");
        return v_selection;
    }

    public static object F_dvs_replay_candidate_available(object v_actor, object v_candidate)
    {
        return (R.Bool((R.Bool((R.Bool((R.Bool(F_dvp_topic(v_actor, v_candidate)) ? (object)(R.lt(R.number("0"), R.count(F_dv_find(v_candidate, "basis", new System.Collections.Generic.List<object>(0) { })))) : (object)(false))) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(v_candidate, "available_when", F_dv_true())), "true")) : (object)(false))) ? (object)(R.lt(R.number("0"), R.count(F_dvs_sim_plan(v_candidate)))) : (object)(false))) ? (object)(F_dvs_replay_inputs_known(F_dv_get(v_actor, "mind"), v_candidate)) : (object)(false));
    }

    public static object F_dvs_replay_inputs_known(object v_mind, object v_candidate)
    {
        object v_refs__377934 = F_dv_find(v_candidate, "external_inputs", new System.Collections.Generic.List<object>(0) { });
        object v_i__377991 = R.number("0");
        while (R.Bool(R.lt(v_i__377991, R.count(v_refs__377934))))
        {
            if (R.Bool(R.not(F_dv_uncontested(F_dv_read(v_mind, R.at(v_refs__377934, v_i__377991))))))
            {
                return false;
            }
            v_i__377991 = R.add(v_i__377991, R.number("1"));
        }
        return true;
    }

    public static object F_dvs_replay_has_available_candidate(object v_actor)
    {
        object v_candidates__378196 = F_dv_find(F_dv_get(v_actor, "policy"), "hypotheses", new System.Collections.Generic.List<object>(0) { });
        if (R.Bool(R.eq(R.count(v_candidates__378196), R.number("0"))))
        {
            return R.lt(R.number("0"), R.count(F_dv_find(F_dv_get(v_actor, "policy"), "factor_constraints", new System.Collections.Generic.List<object>(0) { })));
        }
        object v_key__378531 = new System.Collections.Generic.List<object>(7) { "dvs_replay_has_available_candidate", F_dv_get(v_actor, "id"), v_candidates__378196, F_dv_has_key(v_actor, "question_ref"), F_dv_find(v_actor, "question_ref", false), F_dv_find(F_dv_get(v_actor, "policy"), "topic_bindings", new System.Collections.Generic.List<object>(0) { }), F_dv_find(v_actor, "heard_topics", new System.Collections.Generic.List<object>(0) { }) };
        object v_cached__378761 = F_dvs_plan_cache_get(F_dv_get(v_actor, "mind"), v_key__378531);
        if (R.Bool(R.at(v_cached__378761, R.number("0"))))
        {
            return R.at(v_cached__378761, R.number("1"));
        }
        object v_i__378857 = R.number("0");
        while (R.Bool(R.lt(v_i__378857, R.count(v_candidates__378196))))
        {
            if (R.Bool(F_dvs_replay_candidate_available(v_actor, R.at(v_candidates__378196, v_i__378857))))
            {
                return F_dvs_plan_cache_put(F_dv_get(v_actor, "mind"), v_key__378531, true);
            }
            v_i__378857 = R.add(v_i__378857, R.number("1"));
        }
        return F_dvs_plan_cache_put(F_dv_get(v_actor, "mind"), v_key__378531, false);
    }

    public static object F_dvs_manual_candidate_available(object v_actor, object v_candidate)
    {
        return (R.Bool(F_dvs_replay_candidate_available(v_actor, v_candidate)) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(v_candidate, "manual_available_when", F_dv_true())), "true")) : (object)(false));
    }

    public static object F_dvs_manual_candidate(object v_actor, object v_id)
    {
        object v_candidates__379347 = F_dv_find(F_dv_get(v_actor, "policy"), "hypotheses", new System.Collections.Generic.List<object>(0) { });
        object v_i__379408 = R.number("0");
        while (R.Bool(R.lt(v_i__379408, R.count(v_candidates__379347))))
        {
            object v_candidate__379462 = R.at(v_candidates__379347, v_i__379408);
            if (R.Bool(R.eq(F_dv_get(v_candidate__379462, "id"), v_id)))
            {
                if (R.Bool(F_dvs_manual_candidate_available(v_actor, v_candidate__379462)))
                {
                    return v_candidate__379462;
                }
                return false;
            }
            v_i__379408 = R.add(v_i__379408, R.number("1"));
        }
        return false;
    }

    public static object F_dvs_replay_attempt_key(object v_action)
    {
        object v_id__379721 = F_dv_find(v_action, "replay_hypothesis_id", "");
        if (R.Bool(R.eq(v_id__379721, "")))
        {
            return F_dv_get(v_action, "id");
        }
        return R.json_stringify(new System.Collections.Generic.List<object>(2) { F_dv_get(v_action, "id"), v_id__379721 });
    }

    public static object F_dvs_replay_input_signature(object v_actor, object v_action, object v_refs)
    {
        object v_original__379919 = F_dvp_input_signature(v_actor, v_action, v_refs);
        if (R.Bool(R.eq(F_dv_find(v_action, "replay_hypothesis_id", ""), "")))
        {
            return v_original__379919;
        }
        return F_dv_with_many(v_original__379919, new System.Collections.Generic.List<object>(2) { F_dv_pair("hypothesis_id", F_dv_get(v_action, "replay_hypothesis_id")), F_dv_pair("subject_revision", F_dv_get(F_dv_get(v_actor, "mind"), "revision")) });
    }

    public static object F_dvs_manual_action(object v_actor, object v_selection)
    {
        object v_input__380225 = F_dvs_manual_selection(v_selection);
        object v_action__380273 = F_dvp_find_action(F_dv_get(v_actor, "actions"), F_dv_get(v_input__380225, "action_id"));
        _ = F_dv_require(R.not(R.eq(v_action__380273, false)), "Unknown player action");
        object v_hypothesis__380395 = F_dv_find(v_input__380225, "hypothesis_id", "");
        if (R.Bool(R.eq(F_dv_get(v_action__380273, "kind"), "replay")))
        {
            _ = F_dv_require(R.not(R.eq(v_hypothesis__380395, "")), "Choose a hypothesis before replay");
            _ = F_dv_require(R.not(R.eq(F_dvs_manual_candidate(v_actor, v_hypothesis__380395), false)), "Hypothesis is no longer available to this subject");
            v_action__380273 = F_dv_with(v_action__380273, "replay_hypothesis_id", v_hypothesis__380395);
        }
        else
        {
            _ = F_dv_require(R.eq(v_hypothesis__380395, ""), "Hypotheses apply only to replay");
        }
        return v_action__380273;
    }

    public static object F_dvs_action_label(object v_actor, object v_action)
    {
        object v_label__381051 = F_dv_find(v_action, "label", "（未记录标签）");
        object v_parts__381103 = F_dv_find(v_action, "label_parts", new System.Collections.Generic.List<object>(0) { });
        if (R.Bool(R.lt(R.number("0"), R.count(v_parts__381103))))
        {
            v_label__381051 = "";
            object v_i__381205 = R.number("0");
            while (R.Bool(R.lt(v_i__381205, R.count(v_parts__381103))))
            {
                object v_part__381262 = R.at(v_parts__381103, v_i__381205);
                object v_text__381294 = F_dv_find(v_part__381262, "text", F_dv_find(v_part__381262, "fallback", ""));
                if (R.Bool(F_dv_has_key(v_part__381262, "ref")))
                {
                    object v_value__381414 = F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_part__381262, "ref"));
                    if (R.Bool((R.Bool(F_dv_uncontested(v_value__381414)) ? (object)(R.is_text(F_dv_get(v_value__381414, "value"))) : (object)(false))))
                    {
                        v_text__381294 = F_dv_get(v_value__381414, "value");
                    }
                }
                v_label__381051 = R.cat(v_label__381051, v_text__381294);
                v_i__381205 = R.add(v_i__381205, R.number("1"));
            }
        }
        if (R.Bool(R.eq(v_label__381051, "")))
        {
            return "（未记录标签）";
        }
        return v_label__381051;
    }

    public static object F_dvs_current_local_contact(object actor, object target)
    {
        var context = F_dv_find(F_dv_find(actor, "visible_context", new List<object>()), target, false);
        if (context is false) return false;
        // Contact records and the actor's physical location cache use world
        // identifiers. Mental locations may use a different ontology.
        var room = F_dv_find(actor, "location", F_dv_find(actor, "visible_context_location", false));
        if (room is false) return false;
        return R.Bool(F_dv_same(room, F_dv_get(context, "location")))
            && R.Bool(F_dv_same(room, F_dv_find(actor, "visible_context_location", room)));
    }

    public static object F_dvs_local_candidate_admissible(object v_actor, object v_action)
    {
        object kind = F_dv_find(v_action, "kind", "");
        if (R.Bool(R.eq(kind, "replay")) || R.Bool(R.eq(kind, "wait")) || R.Bool(R.eq(kind, "rest")))
        {
            return true;
        }

        // 6.4: 局部候选 - 目标在场约束与已知目标约束，不枚举非在场对象与全图未知物体
        object speech = F_dv_find(v_action, "speech", false);
        if (R.Bool(R.not(R.eq(speech, false))))
        {
            object request = F_dv_find(speech, "request", false);
            if (R.Bool(R.not(R.eq(request, false))))
            {
                object target = F_dv_find(request, "target", "");
                if (R.Bool(R.not(R.eq(target, ""))))
                {
                    return F_dvs_request_context(v_actor, v_action, target) is not false;
                }
            }
            var reply = F_dv_find(v_action, "response_to", false);
            if (reply is not false)
                return F_dvs_request_context(v_actor, v_action, F_dv_get(reply, "requester")) is not false;

            if (R.Bool(F_dv_has_key(v_action, "response_topic")))
            {
                var pending = F_dvs_pending_request(v_actor, v_action);
                if (R.Items(pending).Count > 0)
                    return F_dvs_request_context(v_actor, v_action, F_dv_get(pending, "requester")) is not false;
                if (R.Bool(F_dv_find(v_action, "requires_actual_request", false))) return false;
            }

            // A broadcast names possible recipients. Group attendance belongs
            // in an explicit precondition or opportunity requirement.
            var recipients = R.Items(F_dv_find(v_action, "listeners", new List<object>()));
            if (recipients.Count > 0)
                return recipients.Any(listener => !R.Bool(R.eq(listener, F_dv_get(v_actor, "id")))
                    && (R.Bool(F_dvs_current_local_contact(v_actor, listener))
                        || F_dvs_request_context(v_actor, v_action, listener) is not false));
        }

        object listeners = F_dv_find(v_action, "listeners", new System.Collections.Generic.List<object>(0));
        if (R.Bool(R.lt(R.number("0"), R.count(listeners))))
        {
            object i = R.number("0");
            while (R.Bool(R.lt(i, R.count(listeners))))
            {
                object listener = R.at(listeners, i);
                if (R.Bool(R.not(R.eq(listener, F_dv_get(v_actor, "id")))))
                {
                    if (!R.Bool(F_dvs_current_local_contact(v_actor, listener)))
                    {
                        return false;
                    }
                }
                i = R.add(i, R.number("1"));
            }
        }

        return true;
    }

    public static object F_dvs_player_options(object v_actor)
    {
        if (R.Bool(R.not(R.eq(F_dv_find(v_actor, "scheduled_action", false), false))))
        {
            return new System.Collections.Generic.List<object>(0) { };
        }
        object v_result__381801 = new System.Collections.Generic.List<object>(0) { };
        object v_view__381821 = F_dv_with(v_actor, "manual_input", true);
        object v_refs__381873 = F_dvp_goal_refs(v_actor);
        object v_i__381909 = R.number("0");
        while (R.Bool(R.lt(v_i__381909, R.count(F_dv_get(v_actor, "actions")))))
        {
            object v_action__381966 = R.at(F_dv_get(v_actor, "actions"), v_i__381909);
            if (R.Bool(F_dvp_eligible(v_view__381821, v_action__381966, F_dv_get(v_actor, "mind"), v_refs__381873)) &&
                R.Bool(F_dvs_local_candidate_admissible(v_actor, v_action__381966)))
            {
                object v_label__382065 = F_dvs_action_label(v_actor, v_action__381966);
                if (R.Bool(R.not(R.eq(v_label__382065, ""))))
                {
                    _ = R.push(v_result__381801, new System.Collections.Generic.List<object>(3) { F_dv_pair("id", F_dv_get(v_action__381966, "id")), F_dv_pair("label", v_label__382065), F_dv_pair("available", true) });
                }
            }
            v_i__381909 = R.add(v_i__381909, R.number("1"));
        }
        return v_result__381801;
    }

    public static object F_dvs_select_player(object v_state, object v_id)
    {
        _ = F_dv_require(F_dv_find(v_state, "awaiting_player", false), "Player input is not currently requested");
        object v_actor__382397 = R.at(F_dv_get(v_state, "actors"), F_dv_get(v_state, "cursor"));
        _ = F_dv_require(R.eq(F_dv_find(v_actor__382397, "scheduled_action", false), false), "Player already has a scheduled action");
        object v_action__382548 = F_dvs_manual_action(v_actor__382397, v_id);
        object v_view__382594 = F_dv_with(v_actor__382397, "manual_input", true);
        _ = F_dv_require(F_dvp_eligible(v_view__382594, v_action__382548, F_dv_get(v_actor__382397, "mind"), F_dvp_goal_refs(v_actor__382397)), "Player action is no longer available");
        _ = F_dv_require(F_dvs_local_candidate_admissible(v_actor__382397, v_action__382548), "Player action is not locally admissible");
        return F_dvs_control_state(v_state, F_dv_get(v_state, "actors"), F_dv_get(v_state, "events"), F_dv_get(v_state, "cursor"), F_dv_find(v_state, "player_actor", ""), v_id, false);
    }

    public static object F_dvs_player_choice(object v_actor, object v_id)
    {
        object v_action__382922 = F_dvs_manual_action(v_actor, v_id);
        object v_view__382968 = F_dv_with(v_actor, "manual_input", true);
        _ = F_dv_require(F_dvp_eligible(v_view__382968, v_action__382922, F_dv_get(v_actor, "mind"), F_dvp_goal_refs(v_actor)), "Player action failed subject admission");
        _ = F_dv_require(F_dvs_local_candidate_admissible(v_actor, v_action__382922), "Player action failed local admission");
        return new System.Collections.Generic.List<object>(6) { F_dv_pair("action", v_action__382922), F_dv_pair("score", R.number("0")), F_dv_pair("plan_expansions", R.number("0")), F_dv_pair("plans", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("relevant_refs", F_dvp_goal_refs(v_actor)), F_dv_pair("controller", "player") };
    }
}
