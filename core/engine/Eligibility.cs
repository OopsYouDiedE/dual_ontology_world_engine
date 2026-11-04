using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Planning / Eligibility: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvp_question(object v_actor)
    {
        if (R.Bool(F_dv_has_key(v_actor, "question_ref")))
        {
            object v_cell__88846 = F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_actor, "question_ref"));
            if (R.Bool(F_dv_uncontested(v_cell__88846)))
            {
                return F_dv_get(v_cell__88846, "value");
            }
            else
            {
                return "";
            }
        }
        else
        {
            return "";
        }
    }

    public static object F_dvp_topic(object v_actor, object v_item)
    {
        if (R.Bool((R.Bool(R.eq(F_dv_find(v_item, "scope", ""), "personal")) ? (object)(R.lt(R.number("0"), R.count(F_dv_find(v_item, "basis", new System.Collections.Generic.List<object>(0) { })))) : (object)(false))))
        {
            return true;
        }
        object v_topic__89170 = F_dv_find(v_item, "question", F_dv_find(v_item, "topic", ""));
        object v_question__89240 = F_dvp_question(v_actor);
        object v_bindings__89279 = F_dv_find(F_dv_get(v_actor, "policy"), "topic_bindings", new System.Collections.Generic.List<object>(0) { });
        object v_mind__89493 = F_dv_find(v_actor, "mind", false);
        if (R.Bool(R.not(R.is_list(v_mind__89493))))
        {
            return F_dvp_topic_scan(v_actor, v_topic__89170, v_question__89240, v_bindings__89279);
        }
        object v_key__89631 = new System.Collections.Generic.List<object>(6) { "dvp_topic", v_item, F_dv_has_key(v_actor, "question_ref"), F_dv_find(v_actor, "question_ref", false), v_bindings__89279, F_dv_find(v_actor, "heard_topics", new System.Collections.Generic.List<object>(0) { }) };
        object v_cached__89785 = F_dvs_plan_cache_get(v_mind__89493, v_key__89631);
        if (R.Bool(R.at(v_cached__89785, R.number("0"))))
        {
            return R.at(v_cached__89785, R.number("1"));
        }
        return F_dvs_plan_cache_put(v_mind__89493, v_key__89631, F_dvp_topic_scan(v_actor, v_topic__89170, v_question__89240, v_bindings__89279));
    }

    public static object F_dvp_topic_scan(object v_actor, object v_topic, object v_question, object v_bindings)
    {
        object v_i__90020 = R.number("0");
        object v_active__90034 = false;
        if (R.Bool(F_dv_and(R.not(R.eq(v_question, "")), R.eq(v_topic, v_question))))
        {
            v_active__90034 = true;
        }
        while (R.Bool(R.lt(v_i__90020, R.count(v_bindings))))
        {
            object v_binding__90179 = R.at(v_bindings, v_i__90020);
            if (R.Bool(F_dv_and(R.eq(F_dv_get(v_binding__90179, "topic"), v_topic), F_dv_and(R.lt(R.number("0"), R.count(F_dv_find(v_binding__90179, "basis", new System.Collections.Generic.List<object>(0) { }))), F_dv_and(F_dv_or(F_dvs_topic_was_heard(v_actor, v_topic), R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_binding__90179, "heard_when")), "true")), R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_binding__90179, "completed_when")), "true")))))))
            {
                v_active__90034 = true;
            }
            v_i__90020 = R.add(v_i__90020, R.number("1"));
        }
        return v_active__90034;
    }

    public static object F_dvp_add_ref(object v_refs, object v_ref)
    {
        if (R.Bool(F_dv_has(v_refs, v_ref)))
        {
            return v_refs;
        }
        else
        {
            return F_dv_append(v_refs, v_ref);
        }
    }

    public static object F_dvp_merge_refs(object v_refs, object v_extra)
    {
        object v_result__90694 = v_refs;
        object v_i__90716 = R.number("0");
        while (R.Bool(R.lt(v_i__90716, R.count(v_extra))))
        {
            v_result__90694 = F_dvp_add_ref(v_result__90694, R.at(v_extra, v_i__90716));
            v_i__90716 = R.add(v_i__90716, R.number("1"));
        }
        return v_result__90694;
    }

    public static object F_dvp_unmet_refs(object v_mind, object v_proposition, object v_include_known)
    {
        object v_op__90902 = F_dv_get(v_proposition, "op");
        if (R.Bool(R.eq(v_op__90902, "constant")))
        {
            return new System.Collections.Generic.List<object>(0) { };
        }
        else
        {
            if (R.Bool(R.eq(v_op__90902, "not")))
            {
                return F_dvp_unmet_refs(v_mind, F_dv_get(v_proposition, "item"), v_include_known);
            }
            else
            {
                if (R.Bool(R.eq(v_op__90902, "all")))
                {
                    object v_items__91140 = F_dv_get(v_proposition, "items");
                    object v_result__91186 = new System.Collections.Generic.List<object>(0) { };
                    object v_i__91218 = R.number("0");
                    while (R.Bool(R.lt(v_i__91218, R.count(v_items__91140))))
                    {
                        v_result__91186 = F_dvp_merge_refs(v_result__91186, F_dvp_unmet_refs(v_mind, R.at(v_items__91140, v_i__91218), v_include_known));
                        v_i__91218 = R.add(v_i__91218, R.number("1"));
                    }
                    return v_result__91186;
                }
                else
                {
                    if (R.Bool(F_dv_or(v_include_known, R.not(R.eq(F_dv_truth(v_mind, v_proposition), "true")))))
                    {
                        if (R.Bool(R.eq(v_op__90902, "held_for")))
                        {
                            return new System.Collections.Generic.List<object>(2) { F_dv_get(v_proposition, "ref"), F_dv_get(v_proposition, "clock") };
                        }
                        return F_dv_single(F_dv_get(v_proposition, "ref"));
                    }
                    else
                    {
                        return new System.Collections.Generic.List<object>(0) { };
                    }
                }
            }
        }
    }

    public static object F_dvp_goal_refs(object v_actor) => GoalRefs(v_actor, false);

    private static object ReadGoalRefs(object actor) => GoalRefs(R.snapshot(actor), true);

    private static object GoalRefs(object v_actor, bool readOnly)
    {
        var key = new List<object> { "owned-goal-refs-v1" };
        var cached = readOnly ? ReadCachedValue("plan", v_actor, key) : F_dvs_plan_cache_get(v_actor, key);
        if (R.Bool(R.at(cached, R.number("0")))) return R.at(cached, R.number("1"));
        object v_goals__91785 = F_dv_find(F_dv_get(v_actor, "policy"), "goals", new System.Collections.Generic.List<object>(0) { });
        object v_mind__91836 = F_dv_get(v_actor, "mind");
        object v_refs__91862 = new System.Collections.Generic.List<object>(0) { };
        object v_i__91880 = R.number("0");
        while (R.Bool(R.lt(v_i__91880, R.count(v_goals__91785))))
        {
            object v_goal__91931 = R.at(v_goals__91785, v_i__91880);
            if (R.Bool(F_dv_and(F_dvp_topic(v_actor, v_goal__91931), R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(v_goal__91931, "available_when", F_dv_true())), "true"))))
            {
                if (R.Bool(F_dv_has_key(v_goal__91931, "ref")))
                {
                    if (R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_goal__91931, "kind", "know"), "evidence"))) ? (object)(true) : (object)(F_dvp_evidence_due(v_actor, v_goal__91931)))))
                    {
                        v_refs__91862 = F_dvp_add_ref(v_refs__91862, F_dv_get(v_goal__91931, "ref"));
                    }
                }
                else
                {
                    if (R.Bool(F_dv_has_key(v_goal__91931, "proposition")))
                    {
                        v_refs__91862 = F_dvp_merge_refs(v_refs__91862, F_dvp_unmet_refs(v_mind__91836, F_dv_get(v_goal__91931, "proposition"), true));
                    }
                }
            }
            v_i__91880 = R.add(v_i__91880, R.number("1"));
        }
        var result = F_dvs_plan_cache_put(v_actor, key, v_refs__91862);
        return readOnly ? R.snapshot(result) : result;
    }

    public static object F_dvp_scope(object v_actor, object v_action, object v_bindings)
    {
        object v_key__92508 = new System.Collections.Generic.List<object>(3) { "dvp-owned-action-scope-v1", F_dv_get(v_actor, "id"), v_bindings };
        object v_cached__92572 = F_dvs_plan_cache_get(v_action, v_key__92508);
        if (R.Bool(R.at(v_cached__92572, R.number("0"))))
        {
            return R.at(v_cached__92572, R.number("1"));
        }
        object v_declared__92664 = F_dv_find(v_action, "purpose_refs", new System.Collections.Generic.List<object>(0) { });
        object v_grants__92719 = F_dv_find(v_action, "grants", new System.Collections.Generic.List<object>(0) { });
        object v_refs__92766 = v_declared__92664;
        object v_i__92790 = R.number("0");
        while (R.Bool(R.lt(v_i__92790, R.count(v_grants__92719))))
        {
            object v_ref__92842 = R.at(v_grants__92719, v_i__92790);
            if (R.Bool(F_dv_has_key(v_ref__92842, "object")))
            {
                object v_local__92912 = F_dv_local(v_bindings, F_dv_get(v_ref__92842, "object"), F_dv_get(v_actor, "id"));
                if (R.Bool(R.eq(v_local__92912, "")))
                {
                    _ = false;
                }
                else
                {
                    v_refs__92766 = F_dvp_add_ref(v_refs__92766, F_dv_ref(v_local__92912, F_dv_get(v_ref__92842, "facet"), F_dv_get(v_ref__92842, "field")));
                }
            }
            v_i__92790 = R.add(v_i__92790, R.number("1"));
        }
        return F_dvs_plan_cache_put(v_action, v_key__92508, v_refs__92766);
    }

    public static object F_dvp_answer_reward(object v_actor, object v_action, object v_mind)
    {
        if (R.Bool(R.eq(F_dv_find(v_action, "answer_form", "complete-answer"), "supporting-statement")))
        {
            return R.number("0");
        }
        if (R.Bool(F_dv_has_key(v_action, "answer")))
        {
            if (R.Bool(R.eq(F_dv_truth(v_mind, F_dv_get(v_action, "answer")), "true")))
            {
                return R.number("1.0");
            }
            else
            {
                if (R.Bool(F_dv_and(F_dv_has_key(v_actor, "argument"), R.eq(F_dv_find(F_dv_find(v_actor, "argument", new System.Collections.Generic.List<object>(0) { }), "question", ""), F_dv_find(v_action, "argument_question", "")))))
                {
                    return R.number("1.0");
                }
                else
                {
                    object v_hypothesis__93673 = F_dv_find(v_actor, "hypothesis", false);
                    if (R.Bool(R.eq(v_hypothesis__93673, false)))
                    {
                        return R.number("0");
                    }
                    else
                    {
                        if (R.Bool(F_dv_same(F_dv_get(v_hypothesis__93673, "proposition"), F_dv_get(v_action, "answer"))))
                        {
                            return F_dv_find(v_hypothesis__93673, "prior", R.number("0.5"));
                        }
                        else
                        {
                            return R.number("0");
                        }
                    }
                }
            }
        }
        else
        {
            return R.number("0");
        }
    }

    public static object F_dvp_input_signature(object v_actor, object v_action, object v_refs)
    {
        object v_mind__94091 = F_dv_get(v_actor, "mind");
        object v_inputs__94117 = new System.Collections.Generic.List<object>(0) { };
        object v_i__94137 = R.number("0");
        object v_local_1__94151 = false;
        if (R.Bool(F_dv_has_key(v_action, "answer")))
        {
            v_local_1__94151 = F_dvs_sim_refs(F_dv_get(v_action, "answer"));
        }
        else
        {
            v_local_1__94151 = new System.Collections.Generic.List<object>(0) { };
        }
        object v_selected__94289 = F_dvp_merge_refs(v_refs, v_local_1__94151);
        if (R.Bool(F_dv_has_key(v_action, "support")))
        {
            v_selected__94289 = F_dvp_merge_refs(v_selected__94289, F_dvs_sim_refs(F_dv_get(v_action, "support")));
        }
        while (R.Bool(R.lt(v_i__94137, R.count(v_selected__94289))))
        {
            _ = R.push(v_inputs__94117, new System.Collections.Generic.List<object>(2) { F_dv_pair("ref", R.at(v_selected__94289, v_i__94137)), F_dv_pair("cell", F_dvs_reason_cell(F_dv_read(v_mind__94091, R.at(v_selected__94289, v_i__94137)))) });
            v_i__94137 = R.add(v_i__94137, R.number("1"));
        }
        return F_dv_with(new System.Collections.Generic.List<object>(3) { F_dv_pair("question", F_dvp_question(v_actor)), F_dv_pair("claim", F_dv_find(v_action, "answer", false)), F_dv_pair("inputs", v_inputs__94117) }, "reward", F_dvp_answer_reward(v_actor, v_action, v_mind__94091));
    }

    public static object F_dvp_answer_relevant(object v_actor, object v_action)
    {
        if (R.Bool(F_dv_has_key(v_action, "topic")))
        {
            return F_dvp_topic(v_actor, v_action);
        }
        else
        {
            object v_refs__94900 = F_dvs_sim_refs(F_dv_get(v_action, "answer"));
            object v_goals__94947 = ReadGoalRefs(v_actor);
            object v_i__94988 = R.number("0");
            object v_found__95006 = false;
            while (R.Bool(R.lt(v_i__94988, R.count(v_refs__94900))))
            {
                if (R.Bool(F_dv_has(v_goals__94947, R.at(v_refs__94900, v_i__94988))))
                {
                    v_found__95006 = true;
                }
                v_i__94988 = R.add(v_i__94988, R.number("1"));
            }
            return v_found__95006;
        }
    }

    public static object F_dvp_answer_attempt_allowed(object v_prev_sig, object v_cur_sig)
    {
        if (R.Bool(R.eq(v_prev_sig, false)))
        {
            return true;
        }
        else
        {
            if (R.Bool(R.not(F_dv_same(F_dv_get(v_prev_sig, "inputs"), F_dv_get(v_cur_sig, "inputs")))))
            {
                return true;
            }
            else
            {
                if (R.Bool(R.not(F_dv_same(F_dv_get(v_prev_sig, "claim"), F_dv_get(v_cur_sig, "claim")))))
                {
                    return true;
                }
                else
                {
                    if (R.Bool(R.not(F_dv_same(F_dv_get(v_prev_sig, "question"), F_dv_get(v_cur_sig, "question")))))
                    {
                        return true;
                    }
                    else
                    {
                        object v_r1__95631 = F_dv_find(v_prev_sig, "reward", R.number("0"));
                        object v_r2__95691 = F_dv_find(v_cur_sig, "reward", R.number("0"));
                        object v_local_1__95750 = false;
                        if (R.Bool(R.lt(v_r1__95631, v_r2__95691)))
                        {
                            v_local_1__95750 = R.sub(v_r2__95691, v_r1__95631);
                        }
                        else
                        {
                            v_local_1__95750 = R.sub(v_r1__95631, v_r2__95691);
                        }
                        object v_diff__95938 = v_local_1__95750;
                        return R.not(R.lt(v_diff__95938, R.number("0.2")));
                    }
                }
            }
        }
    }

    public static object F_dvp_inspection_signature(object v_actor, object v_action, object v_mind)
    {
        object v_refs__96058 = F_dvp_merge_refs(F_dv_find(v_action, "grants", new System.Collections.Generic.List<object>(0) { }), F_dvs_sim_refs(F_dv_get(v_action, "precondition")));
        object v_inputs__96154 = new System.Collections.Generic.List<object>(0) { };
        object v_i__96174 = R.number("0");
        while (R.Bool(R.lt(v_i__96174, R.count(v_refs__96058))))
        {
            object v_cell__96222 = F_dv_read(v_mind, R.at(v_refs__96058, v_i__96174));
            _ = R.push(v_inputs__96154, new System.Collections.Generic.List<object>(4) { F_dv_pair("ref", R.at(v_refs__96058, v_i__96174)), F_dv_pair("known", F_dv_get(v_cell__96222, "known")), F_dv_pair("value", F_dv_get(v_cell__96222, "value")), F_dv_pair("conflicted", F_dv_get(v_cell__96222, "conflicted")) });
            v_i__96174 = R.add(v_i__96174, R.number("1"));
        }
        object v_evidence__96393 = F_dvp_inspection_freshness(v_actor, v_mind);
        object v_freshness__96450 = new System.Collections.Generic.List<object>(0) { };
        v_i__96174 = R.number("0");
        while (R.Bool(R.lt(v_i__96174, R.count(v_refs__96058))))
        {
            object v_entries__96517 = F_dv_find(v_evidence__96393, F_dv_ref_lookup_key(R.at(v_refs__96058, v_i__96174)), new System.Collections.Generic.List<object>(0) { });
            object v_j__96589 = R.number("0");
            while (R.Bool(R.lt(v_j__96589, R.count(v_entries__96517))))
            {
                _ = R.push(v_freshness__96450, R.at(v_entries__96517, v_j__96589));
                v_j__96589 = R.add(v_j__96589, R.number("1"));
            }
            v_i__96174 = R.add(v_i__96174, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("inputs", v_inputs__96154), F_dv_pair("freshness", v_freshness__96450) };
    }

    public static object F_dvp_rejected_condition_signature(object v_mind, object v_proposition)
    {
        if (R.Bool(R.eq(F_dv_get(v_proposition, "op"), "all")))
        {
            object v_items__97029 = new System.Collections.Generic.List<object>(0) { };
            object v_i__97052 = R.number("0");
            while (R.Bool(R.lt(v_i__97052, R.count(F_dv_get(v_proposition, "items")))))
            {
                _ = R.push(v_items__97029, F_dvp_rejected_condition_signature(v_mind, R.at(F_dv_get(v_proposition, "items"), v_i__97052)));
                v_i__97052 = R.add(v_i__97052, R.number("1"));
            }
            return v_items__97029;
        }
        if (R.Bool(R.eq(F_dv_get(v_proposition, "op"), "not")))
        {
            return F_dvp_rejected_condition_signature(v_mind, F_dv_get(v_proposition, "item"));
        }
        if (R.Bool(R.eq(F_dv_get(v_proposition, "op"), "equals")))
        {
            object v_cell__97388 = F_dv_read(v_mind, F_dv_get(v_proposition, "ref"));
            return new System.Collections.Generic.List<object>(4) { F_dv_pair("ref", F_dv_get(v_proposition, "ref")), F_dv_pair("known", F_dv_get(v_cell__97388, "known")), F_dv_pair("value", F_dv_get(v_cell__97388, "value")), F_dv_pair("conflicted", F_dv_get(v_cell__97388, "conflicted")) };
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("condition", v_proposition), F_dv_pair("truth", F_dv_truth(v_mind, v_proposition)) };
    }

    public static object F_dvp_rejected_signature(object v_actor, object v_action, object v_mind)
    {
        object v_adoption__97672 = F_dv_find(F_dv_get(v_mind, "adoptions"), F_dv_find(v_action, "predict_law", ""), false);
        object v_prediction__97758 = false;
        if (R.Bool(R.not(R.eq(v_adoption__97672, false))))
        {
            v_prediction__97758 = new System.Collections.Generic.List<object>(4) { F_dv_pair("active", F_dv_get(v_adoption__97672, "active")), F_dv_pair("mode", F_dv_get(v_adoption__97672, "mode")), F_dv_pair("law", F_dv_get(v_adoption__97672, "law")), F_dv_pair("inputs", F_dvp_rejected_condition_signature(v_mind, F_dv_get(F_dv_get(v_adoption__97672, "law"), "premise"))) };
        }
        object v_location__97976 = false;
        if (R.Bool(F_dv_has_key(v_actor, "location_ref")))
        {
            object v_cell__98047 = F_dv_read(v_mind, F_dv_get(v_actor, "location_ref"));
            v_location__97976 = new System.Collections.Generic.List<object>(3) { F_dv_pair("known", F_dv_get(v_cell__98047, "known")), F_dv_pair("value", F_dv_get(v_cell__98047, "value")), F_dv_pair("conflicted", F_dv_get(v_cell__98047, "conflicted")) };
        }
        object v_opportunity__98189 = false;
        if (R.Bool(F_dv_has_key(v_action, "opportunity_requirement")))
        {
            v_opportunity__98189 = new System.Collections.Generic.List<object>(3) { F_dv_pair("context", F_dv_find(v_actor, "visible_context", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("observed_location", F_dv_find(v_actor, "visible_context_location", false)), F_dv_pair("visibility_available", F_dv_find(v_actor, "visible_context_clear", false)) };
        }
        object v_request_context__98488 = false;
        object v_request__98520 = F_dv_find(F_dv_find(v_action, "speech", new System.Collections.Generic.List<object>(0) { }), "request", false);
        if (R.Bool(R.not(R.eq(v_request__98520, false))))
        {
            v_request_context__98488 = F_dvs_request_context(F_dvp_actor_view(v_actor, v_mind), v_action, F_dv_get(v_request__98520, "target"));
        }
        return new System.Collections.Generic.List<object>(5) { F_dv_pair("precondition", F_dvp_rejected_condition_signature(v_mind, F_dv_get(v_action, "precondition"))), F_dv_pair("prediction", v_prediction__97758), F_dv_pair("location", v_location__97976), F_dv_pair("opportunity", v_opportunity__98189), F_dv_pair("request_context", v_request_context__98488) };
    }

    public static object F_dvs_manual_continuous_cancel(object v_action, object v_receipt, object v_subject)
    {
        object v_field__99370 = F_dv_find(v_receipt, "field", new System.Collections.Generic.List<object>(0) { });
        object v_proof__99416 = F_dv_find(v_field__99370, "continuous_cancel", false);
        if (R.Bool((R.Bool(R.eq(v_proof__99416, false)) ? (object)(true) : (object)(R.not(F_dv_record_like(v_proof__99416))))))
        {
            return false;
        }
        if (R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_proof__99416, "version", ""), "source-cancel-v2"))) ? (object)(true) : (object)(R.not(F_dv_find(v_proof__99416, "identity_verified", false))))))
        {
            return false;
        }
        if (R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_action, "kind"), "move"))) ? (object)(true) : (object)(F_dv_has_key(F_dv_find(v_action, "travel", new System.Collections.Generic.List<object>(0) { }), "station")))))
        {
            return false;
        }
        object v_travel__99786 = F_dv_find(v_action, "travel", new System.Collections.Generic.List<object>(0) { });
        if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_find(v_travel__99786, "from", ""), "")) ? (object)(true) : (object)(R.eq(F_dv_find(v_travel__99786, "to", ""), "")))) ? (object)(true) : (object)(R.eq(F_dv_get(v_travel__99786, "from"), F_dv_get(v_travel__99786, "to"))))))
        {
            return false;
        }
        if (R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_field__99370, "kind", ""), "daily"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__99370, "status", ""), "blocked"))))) ? (object)(true) : (object)(R.not(F_dv_find(v_field__99370, "continuous_walk", false))))))
        {
            return false;
        }
        if (R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_field__99370, "reason", ""), "released"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_proof__99416, "reason", ""), "released"))))))
        {
            return false;
        }
        if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_field__99370, "who", ""), v_subject))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__99370, "action", ""), F_dv_get(v_action, "id")))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_proof__99416, "actor", ""), v_subject))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_proof__99416, "action", ""), F_dv_get(v_action, "id")))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_proof__99416, "action_event", ""), F_dv_get(v_receipt, "action_event")))))))
        {
            return false;
        }
        object v_request__100497 = F_dv_find(v_field__99370, "request_id", "");
        object v_serial__100548 = F_dv_find(v_field__99370, "serial", false);
        if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.eq(v_request__100497, "")) ? (object)(true) : (object)(R.not(R.eq(v_request__100497, F_dv_find(v_proof__99416, "request_id", "")))))) ? (object)(true) : (object)(R.not(R.is_numeric_value(v_serial__100548))))) ? (object)(true) : (object)(R.not(R.lt(R.number("0"), v_serial__100548))))) ? (object)(true) : (object)(R.not(F_dv_same(v_serial__100548, F_dv_find(v_proof__99416, "serial", false)))))))
        {
            return false;
        }
        object v_from_room__100793 = F_dv_find(v_proof__99416, "from_room", "");
        object v_to_room__100845 = F_dv_find(v_proof__99416, "to_room", "");
        object v_map__100893 = F_dv_find(v_proof__99416, "map", "");
        if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.eq(v_from_room__100793, "")) ? (object)(true) : (object)(R.eq(v_to_room__100845, "")))) ? (object)(true) : (object)(R.eq(v_from_room__100793, v_to_room__100845)))) ? (object)(true) : (object)(R.eq(v_map__100893, "")))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__99370, "source_room", ""), v_from_room__100793))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__99370, "room", ""), v_from_room__100793))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__99370, "source_map", ""), v_map__100893))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__99370, "map", ""), v_map__100893))))))
        {
            return false;
        }
        if (R.Bool((R.Bool(R.not(F_dv_same(F_dv_find(v_field__99370, "transitions", false), new System.Collections.Generic.List<object>(0) { }))) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_find(v_proof__99416, "transitions", false), new System.Collections.Generic.List<object>(0) { }))))))
        {
            return false;
        }
        object v_fields__101344 = new System.Collections.Generic.List<object>(2) { "source_position", "actual_position" };
        object v_i__101400 = R.number("0");
        while (R.Bool(R.lt(v_i__101400, R.count(v_fields__101344))))
        {
            object v_position__101450 = F_dv_find(v_field__99370, R.at(v_fields__101344, v_i__101400), false);
            if (R.Bool((R.Bool((R.Bool(R.not(R.is_list(v_position__101450))) ? (object)(true) : (object)(R.not(R.eq(R.count(v_position__101450), R.number("2")))))) ? (object)(true) : (object)(R.not(F_dv_same(v_position__101450, F_dv_find(v_proof__99416, R.at(v_fields__101344, v_i__101400), false)))))))
            {
                return false;
            }
            if (R.Bool((R.Bool(R.not(R.is_numeric_value(R.at(v_position__101450, R.number("0"))))) ? (object)(true) : (object)(R.not(R.is_numeric_value(R.at(v_position__101450, R.number("1"))))))))
            {
                return false;
            }
            v_i__101400 = R.add(v_i__101400, R.number("1"));
        }
        // Stopping within the departure room is a cancellation even after local movement.
        // The host binds both positions to the retained body and verifies that room.
        if (!R.Bool(F_dv_find(v_proof__99416, "source_room_verified", false)))
        {
            return false;
        }
        object v_source_geometry__101869 = F_dv_find(v_field__99370, "source_geometry", "");
        if (R.Bool((R.Bool(R.not(F_dv_has(new System.Collections.Generic.List<object>(2) { "interior", "source-side-doorway" }, v_source_geometry__101869))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_proof__99416, "source_geometry", ""), v_source_geometry__101869))))))
        {
            return false;
        }
        return true;
    }

    public static object F_dvs_manual_contact_failure(object v_action, object v_receipt, object v_subject)
    {
        if (R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_receipt, "executor", ""), "godot"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_receipt, "outcome", ""), "rejected"))))))
        {
            return "";
        }
        if (R.Bool(F_dvs_manual_continuous_cancel(v_action, v_receipt, v_subject)))
        {
            return "native-continuous-source-cancel";
        }
        object v_field__102392 = F_dv_find(v_receipt, "field", new System.Collections.Generic.List<object>(0) { });
        if (R.Bool((R.Bool(R.eq(F_dv_find(v_field__102392, "executor", ""), "dv-world")) ? (object)(R.eq(F_dv_find(v_field__102392, "contact", true), false)) : (object)(false))))
        {
            object v_site__102540 = F_dv_find(v_field__102392, "site", "");
            if (R.Bool((R.Bool(R.not(R.eq(v_site__102540, ""))) ? (object)(R.not(R.eq(v_site__102540, "self"))) : (object)(false))))
            {
                return "contact-out-of-reach";
            }
        }
        if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_field__102392, "kind", ""), "daily"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__102392, "status", ""), "blocked"))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__102392, "who", ""), v_subject))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__102392, "action", ""), F_dv_get(v_action, "id")))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__102392, "reason", ""), ""))))))
        {
            return "";
        }
        object v_proof__102898 = new System.Collections.Generic.List<object>(7) { "station", "approach_point", "station_point", "station_radius", "arrival_tolerance", "actual_position", "arrival_error" };
        object v_i__103036 = R.number("0");
        while (R.Bool(R.lt(v_i__103036, R.count(v_proof__102898))))
        {
            if (R.Bool(R.not(F_dv_has_key(v_field__102392, R.at(v_proof__102898, v_i__103036)))))
            {
                return "";
            }
            v_i__103036 = R.add(v_i__103036, R.number("1"));
        }
        return "native-motion-blocked";
    }

    public static object F_dvp_eligible_base(object v_actor, object v_action, object v_mind, object v_relevant)
    {
        if (R.Bool((R.Bool(F_dv_find(v_actor, "manual_input", false)) ? (object)(F_dv_find(v_action, "known_opportunity_wait", false)) : (object)(false))))
        {
            return false;
        }
        object v_unknown_refs__103519 = F_dv_find(v_action, "unknown_refs", new System.Collections.Generic.List<object>(0) { });
        object v_unknown_index__103578 = R.number("0");
        while (R.Bool(R.lt(v_unknown_index__103578, R.count(v_unknown_refs__103519))))
        {
            if (R.Bool(F_dv_uncontested(F_dv_read(v_mind, R.at(v_unknown_refs__103519, v_unknown_index__103578)))))
            {
                return false;
            }
            v_unknown_index__103578 = R.add(v_unknown_index__103578, R.number("1"));
        }
        object v_entity_lock__103792 = F_dv_find(v_actor, "simulation_entity_lock", false);
        if (R.Bool((R.Bool(F_dv_find(v_action, "requires_entity_reservation", false)) ? (object)(R.eq(v_entity_lock__103792, false)) : (object)(false))))
        {
            return false;
        }
        if (R.Bool((R.Bool((R.Bool(R.not(R.eq(v_entity_lock__103792, false))) ? (object)(F_dv_find(v_entity_lock__103792, "protecting", false)) : (object)(false))) ? (object)(R.not(F_dv_find(v_action, "entity_hold_compatible", false))) : (object)(false))))
        {
            return false;
        }
        if (R.Bool(F_dvs_action_requires_argument(v_action)))
        {
            if (R.Bool(R.not(F_dvs_argument_action_current(F_dvp_actor_view(v_actor, v_mind), v_action))))
            {
                return false;
            }
        }
        if (R.Bool((R.Bool(F_dv_find(v_action, "manual_only", false)) ? (object)(R.not(F_dv_find(v_actor, "manual_input", false))) : (object)(false))))
        {
            return false;
        }
        object v_id__104387 = F_dv_get(v_action, "id");
        object v_used__104479 = F_dv_find(v_actor, "completed_actions", new System.Collections.Generic.List<object>(0) { });
        if (R.Bool((R.Bool((R.Bool(R.not(F_dv_has_key(v_action, "answer"))) ? (object)(R.not(F_dv_has_key(v_action, "speech"))) : (object)(false))) ? (object)(R.not(R.eq(F_dv_get(v_action, "kind"), "replay"))) : (object)(false))))
        {
            if (R.Bool((R.Bool(F_dv_has(v_used__104479, v_id__104387)) ? (object)((R.Bool(F_dv_find(v_action, "once", false)) ? (object)(true) : (object)(R.not(F_dv_find(v_action, "repeatable", false))))) : (object)(false))))
            {
                return false;
            }
        }
        object v_retry__105006 = F_dv_find(F_dv_find(v_actor, "manual_contact_retries", new System.Collections.Generic.List<object>(0) { }), v_id__104387, false);
        object v_explicit_contact_retry__105087 = (R.Bool((R.Bool(F_dv_find(v_actor, "manual_input", false)) ? (object)(R.not(R.eq(v_retry__105006, false))) : (object)(false))) ? (object)((R.Bool(R.not(R.eq(F_dv_find(v_retry__105006, "failure_class", ""), "native-continuous-source-cancel"))) ? (object)(true) : (object)(F_dv_same(F_dv_find(v_retry__105006, "source_signature", false), F_dvp_rejected_signature(v_actor, v_action, v_mind))))) : (object)(false));
        object v_rejected__105355 = F_dv_find(F_dv_find(v_actor, "rejected_attempt_signatures", new System.Collections.Generic.List<object>(0) { }), v_id__104387, false);
        if (R.Bool(R.not(R.eq(v_rejected__105355, false))))
        {
            if (R.Bool((R.Bool(F_dv_same(v_rejected__105355, F_dvp_rejected_signature(v_actor, v_action, v_mind))) ? (object)(R.not(v_explicit_contact_retry__105087)) : (object)(false))))
            {
                return false;
            }
        }
        if (R.Bool(F_dv_and(R.not(F_dv_find(v_actor, "cognition_blocked", false)), F_dv_and(F_dv_find(v_action, "argument_available", true), R.eq(F_dvs_dirty_truth(v_mind, F_dv_get(v_action, "precondition"), F_dv_get(v_actor, "policy"), F_dv_get(v_action, "id")), "true")))))
        {
            if (R.Bool(R.eq(F_dv_get(v_action, "kind"), "inspect")))
            {
                object v_inspection_actor__105845 = F_dvp_actor_view(v_actor, v_mind);
                if (R.Bool(F_dv_same(F_dv_find(F_dv_find(v_actor, "inspection_attempt_signatures", new System.Collections.Generic.List<object>(0) { }), v_id__104387, false), F_dvp_inspection_signature(v_inspection_actor__105845, v_action, v_mind))))
                {
                    if (R.Bool(R.not((R.Bool(F_dv_find(v_actor, "manual_input", false)) ? (object)(F_dv_find(v_action, "manual_reinspect", false)) : (object)(false)))))
                    {
                        return false;
                    }
                }
            }
            if (R.Bool(F_dv_has_key(v_action, "answer")))
            {
                object v_local_1__106248 = false;
                if (R.Bool(F_dv_has_key(v_action, "argument")))
                {
                    v_local_1__106248 = R.number("2");
                }
                else
                {
                    v_local_1__106248 = R.number("1");
                }
                if (R.Bool(F_dv_and(F_dvp_answer_relevant(v_actor, v_action), R.lt(F_dv_find(F_dv_find(v_actor, "accepted_answers", new System.Collections.Generic.List<object>(0) { }), F_dv_find(v_action, "topic", ""), R.number("0")), v_local_1__106248))))
                {
                    object v_speech_ready__106558 = true;
                    if (R.Bool(F_dv_has_key(v_action, "speech")))
                    {
                        v_speech_ready__106558 = R.eq(F_dvs_speech_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_action, "speech")), "true");
                    }
                    return F_dv_and(v_speech_ready__106558, F_dv_and(R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_action, "answer")), "false")), F_dvp_answer_attempt_allowed(F_dv_find(F_dv_find(v_actor, "answer_attempt_signatures", new System.Collections.Generic.List<object>(0) { }), v_id__104387, false), F_dvp_input_signature(v_actor, v_action, v_relevant))));
                }
                else
                {
                    return false;
                }
            }
            else
            {
                if (R.Bool(F_dv_has_key(v_action, "speech")))
                {
                    object v_local_2__107095 = false;
                    if (R.Bool(F_dv_has_key(v_action, "response_topic")))
                    {
                        object v_request__107192 = F_dvs_pending_request(v_actor, v_action);
                        if (R.Bool(R.lt(R.number("0"), R.count(v_request__107192))))
                        {
                            v_local_2__107095 = R.not(R.eq(F_dvs_request_context(v_actor, v_action, F_dv_get(v_request__107192, "requester")), false));
                        }
                        else
                        {
                            v_local_2__107095 = (R.Bool(R.not(F_dv_find(v_action, "requires_actual_request", false))) ? (object)(F_dv_and(R.not(F_dv_has(v_used__104479, v_id__104387)), F_dv_or(F_dv_has_key(F_dv_find(v_actor, "visible_context", new System.Collections.Generic.List<object>(0) { }), F_dv_find(v_action, "offer_to", "")), (R.Bool(R.not(R.eq(F_dv_find(v_action, "opportunity_requirement", false), false))) ? (object)(F_dvp_communication_progress(v_actor, v_action)) : (object)(false))))) : (object)(false));
                        }
                    }
                    else
                    {
                        if (R.Bool(F_dv_has_key(F_dv_get(v_action, "speech"), "request")))
                        {
                            object v_context__107841 = F_dvs_request_context(v_actor, v_action, F_dv_get(F_dv_get(F_dv_get(v_action, "speech"), "request"), "target"));
                            v_local_2__107095 = F_dv_and(R.not(R.eq(v_context__107841, false)), R.not(F_dv_same(v_context__107841, F_dv_find(F_dv_find(v_actor, "request_contexts", new System.Collections.Generic.List<object>(0) { }), v_id__104387, false))));
                        }
                        else
                        {
                            v_local_2__107095 = (R.Bool(R.not(F_dv_has(v_used__104479, v_id__104387))) ? (object)(true) : (object)((R.Bool((R.Bool(F_dv_find(v_action, "repeatable", false)) ? (object)(R.not(F_dv_find(v_action, "once", false))) : (object)(false))) ? (object)(R.not(F_dv_find(v_action, "response_once", false))) : (object)(false))));
                        }
                    }
                    return F_dv_and(R.eq(F_dvs_speech_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_action, "speech")), "true"), v_local_2__107095);
                }
                else
                {
                    if (R.Bool(R.eq(F_dv_get(v_action, "kind"), "replay")))
                    {
                        return (R.Bool((R.Bool(R.lt(R.number("0"), F_dv_find(F_dv_get(v_actor, "policy"), "model_budget", R.number("0")))) ? (object)(F_dvs_replay_has_available_candidate(F_dvp_actor_view(v_actor, v_mind))) : (object)(false))) ? (object)(R.not(F_dv_same(F_dv_find(F_dv_find(v_actor, "replay_attempt_signatures", new System.Collections.Generic.List<object>(0) { }), F_dvs_replay_attempt_key(v_action), false), F_dvs_replay_input_signature(v_actor, v_action, v_relevant)))) : (object)(false));
                    }
                    else
                    {
                        if (R.Bool(F_dv_find(v_action, "once", false)))
                        {
                            return R.not(F_dv_has(v_used__104479, v_id__104387));
                        }
                        else
                        {
                            if (R.Bool(F_dv_find(v_action, "repeatable", false)))
                            {
                                return true;
                            }
                            else
                            {
                                return R.not(F_dv_has(v_used__104479, v_id__104387));
                            }
                        }
                    }
                }
            }
        }
        else
        {
            return false;
        }
    }

    public static object F_dvp_source_supported_since(object v_cell, object v_source, object v_minimum_time)
    {
        object v_claims__109194 = F_dv_find(v_cell, "claims", new System.Collections.Generic.List<object>(0) { });
        object v_i__109239 = R.number("0");
        object v_found__109253 = false;
        while (R.Bool(R.lt(v_i__109239, R.count(v_claims__109194))))
        {
            object v_claim__109313 = R.at(v_claims__109194, v_i__109239);
            if (R.Bool((R.Bool(F_dv_and(R.eq(F_dv_get(v_claim__109313, "source"), v_source), F_dv_same(F_dv_get(v_claim__109313, "value"), F_dv_get(v_cell, "value")))) ? (object)(R.not(R.lt(F_dv_find(v_claim__109313, "time", R.number("0")), v_minimum_time))) : (object)(false))))
            {
                v_found__109253 = true;
            }
            v_i__109239 = R.add(v_i__109239, R.number("1"));
        }
        return v_found__109253;
    }

    public static object F_dvp_evidence_due(object v_actor, object v_goal)
    {
        object v_origin__109786 = F_dv_find(v_actor, "planning_evidence_mind", F_dv_get(v_actor, "mind"));
        return R.not(R.lt(F_dv_find(v_origin__109786, "time", R.number("0")), F_dv_find(v_goal, "minimum_time", R.number("0"))));
    }

    public static object F_dvp_needs_evidence(object v_actor, object v_ref)
    {
        return PendingEvidence(v_actor, v_ref, null);
    }

    // Use the same revision-scoped freshness index as inspection admission.
    // A known value does not fulfill a request for a newer, sourced observation.
    private static bool PendingEvidence(object actor, object reference, object? observationSource)
    {
        var mind = F_dv_get(actor, "mind");
        var index = F_dvp_inspection_freshness(actor, mind);
        var entries = F_dv_find(index, F_dv_ref_lookup_key(reference), new List<object>());
        foreach (var entry in R.Items(entries))
        {
            if (!R.Bool(F_dv_same(F_dv_get(entry, "ref"), reference)) ||
                !R.Bool(F_dv_get(entry, "topic")) ||
                !R.Bool(R.eq(F_dv_get(entry, "available"), "true")) ||
                !R.Bool(F_dv_get(entry, "due"))) continue;
            var source = F_dv_get(entry, "source");
            if (observationSource is not null && !R.Bool(R.eq(source, observationSource))) continue;
            if (!R.Bool(F_dvp_source_supported_since(F_dv_read(mind, reference), source,
                F_dv_get(entry, "minimum_time")))) return true;
        }
        return false;
    }

    public static object F_dvp_evidence_action_progress(object actor, object action, object reference, object bindings)
    {
        if (!PendingEvidence(actor, reference, null)) return false;
        var subject = F_dv_get(actor, "id");
        foreach (var grant in R.Items(F_dv_find(action, "grants", new List<object>())))
        {
            var local = F_dv_local(bindings, F_dv_get(grant, "object"), subject);
            if (R.Bool(R.eq(local, ""))) continue;
            var mapped = F_dv_ref(local, F_dv_get(grant, "facet"), F_dv_get(grant, "field"));
            if (!R.Bool(F_dv_same_local_ref(subject, mapped, reference))) continue;
            var source = F_dv_find(action, "observation_source", "perception");
            foreach (var declared in R.Items(F_dv_find(action, "grant_observation_sources", new List<object>())))
                if (R.Bool(F_dv_same_local_ref(subject, F_dv_get(declared, "ref"), grant)))
                    source = F_dv_get(declared, "source");
            if (PendingEvidence(actor, reference, source)) return true;
        }
        return false;
    }

    public static object F_dvp_owned_task_ready(object v_actor, object v_goals)
    {
        if (R.Bool(R.not(F_dv_find(F_dv_get(v_actor, "policy"), "coordinate_owned_tasks", false))))
        {
            return false;
        }
        object v_i__110889 = R.number("0");
        while (R.Bool(R.lt(v_i__110889, R.count(v_goals))))
        {
            object v_goal__110938 = R.at(v_goals, v_i__110889);
            if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_find(v_goal__110938, "scope", ""), "personal")) ? (object)(R.lt(R.number("0"), R.count(F_dv_find(v_goal__110938, "basis", new System.Collections.Generic.List<object>(0) { })))) : (object)(false))) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(v_goal__110938, "available_when", F_dv_true())), "true")) : (object)(false))))
            {
                object v_kind__111147 = F_dv_find(v_goal__110938, "kind", "");
                object v_pending__111196 = false;
                if (R.Bool((R.Bool(R.eq(v_kind__111147, "achieve")) ? (object)(F_dv_find(v_goal__110938, "navigation_commitment", false)) : (object)(false))))
                {
                    v_pending__111196 = R.Bool(F_dvs_navigation_evidence_pending(v_actor, v_goal__110938)) &&
                        R.Bool(R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_goal__110938, "proposition")), "true")));
                }
                if (R.Bool((R.Bool((R.Bool(R.eq(v_kind__111147, "evidence")) ? (object)(F_dv_has_key(v_goal__110938, "ref")) : (object)(false))) ? (object)(F_dvp_evidence_due(v_actor, v_goal__110938)) : (object)(false))))
                {
                    v_pending__111196 = R.not(F_dvp_source_supported_since(F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_goal__110938, "ref")), F_dv_find(v_goal__110938, "source", "perception"), F_dv_find(v_goal__110938, "minimum_time", R.number("0"))));
                }
                if (R.Bool(v_pending__111196))
                {
                    object v_a__111673 = R.number("0");
                    while (R.Bool(R.lt(v_a__111673, R.count(F_dv_get(v_actor, "actions")))))
                    {
                        object v_action__111754 = R.at(F_dv_get(v_actor, "actions"), v_a__111673);
                        object v_practical__111804 = (R.Bool((R.Bool(R.eq(v_kind__111147, "evidence")) ? (object)(R.eq(F_dv_find(v_action__111754, "inquiry_purpose", ""), "practical-access")) : (object)(false))) ? (object)(F_dv_has(F_dv_find(v_action__111754, "grants", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_goal__110938, "ref"))) : (object)(false));
                        if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(v_kind__111147, "achieve")) ? (object)(true) : (object)(v_practical__111804))) ? (object)(F_dvp_eligible(v_actor, v_action__111754, F_dv_get(v_actor, "mind"), new System.Collections.Generic.List<object>(0) { })) : (object)(false))) ? (object)(R.not(R.lt(F_dvp_personal_motivation(v_actor, v_action__111754), R.number("0")))) : (object)(false))))
                        {
                            if (R.Bool(v_practical__111804))
                            {
                                return true;
                            }
                            if (R.Bool(R.eq(v_kind__111147, "achieve")))
                            {
                                object v_work__112261 = F_dvp_achievement_pending_work(v_actor, v_action__111754, new System.Collections.Generic.List<object>(1) { v_goal__110938 });
                                if (R.Bool(R.lt(R.number("0"), F_dv_get(v_work__112261, "value"))))
                                {
                                    return true;
                                }
                            }
                        }
                        v_a__111673 = R.add(v_a__111673, R.number("1"));
                    }
                }
            }
            v_i__110889 = R.add(v_i__110889, R.number("1"));
        }
        return false;
    }

    public static object F_dvp_personal_motivation(object v_actor, object v_action)
    {
        if (R.Bool(R.not(F_dv_has_key(v_action, "personal_choice"))))
        {
            return R.number("0");
        }
        if (R.Bool(R.not(F_dv_find(F_dv_get(v_actor, "policy"), "value_sensitive_progress", false))))
        {
            return R.number("1");
        }
        object v_order__112681 = F_dv_find(F_dv_get(v_actor, "policy"), "value_order", new System.Collections.Generic.List<object>(0) { });
        object v_i__112738 = R.number("0");
        while (R.Bool(R.lt(v_i__112738, R.count(v_order__112681))))
        {
            object v_impact__112787 = F_dvp_personal_impact(F_dv_get(v_action, "personal_choice"), R.at(v_order__112681, v_i__112738));
            if (R.Bool(R.not(R.eq(v_impact__112787, R.number("0")))))
            {
                return v_impact__112787;
            }
            v_i__112738 = R.add(v_i__112738, R.number("1"));
        }
        return R.number("0");
    }

    public static object F_dvp_extract_required_room(object v_prop, object v_loc_ref)
    {
        if (R.Bool(R.not(F_dv_record_like(v_prop))))
        {
            return "";
        }
        object v_op__113195 = F_dv_find(v_prop, "op", "");
        if (R.Bool(R.eq(v_op__113195, "equals")))
        {
            object v_ref__113259 = F_dv_find(v_prop, "ref", false);
            if (R.Bool((R.Bool(R.not(R.eq(v_ref__113259, false))) ? (object)(F_dv_same(v_ref__113259, v_loc_ref)) : (object)(false))))
            {
                object v_val__113360 = F_dv_find(v_prop, "value", "");
                if (R.Bool(R.not(F_dv_record_like(v_val__113360))))
                {
                    return v_val__113360;
                }
            }
        }
        if (R.Bool(R.eq(v_op__113195, "all")))
        {
            object v_items__113492 = F_dv_find(v_prop, "items", new System.Collections.Generic.List<object>(0) { });
            object v_i__113539 = R.number("0");
            object v_n__113557 = R.count(v_items__113492);
            while (R.Bool(R.lt(v_i__113539, v_n__113557)))
            {
                object v_r__113614 = F_dvp_extract_required_room(R.at(v_items__113492, v_i__113539), v_loc_ref);
                if (R.Bool(R.not(R.eq(v_r__113614, ""))))
                {
                    return v_r__113614;
                }
                v_i__113539 = R.add(v_i__113539, R.number("1"));
            }
        }
        return "";
    }

    public static object F_dvp_find_action(object v_actions, object v_id)
    {
        object v_key__113790 = new System.Collections.Generic.List<object>(2) { "dvp-find-action", v_id };
        object v_cached__113828 = F_dvs_declaration_cache_get(v_actions, v_key__113790);
        if (R.Bool(R.at(v_cached__113828, R.number("0"))))
        {
            return R.at(v_cached__113828, R.number("1"));
        }
        object v_i__113928 = R.number("0");
        object v_result__113942 = false;
        while (R.Bool(R.lt(v_i__113928, R.count(v_actions))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(v_actions, v_i__113928), "id"), v_id)))
            {
                v_result__113942 = R.at(v_actions, v_i__113928);
            }
            v_i__113928 = R.add(v_i__113928, R.number("1"));
        }
        return F_dvs_declaration_cache_put(v_actions, v_key__113790, v_result__113942);
    }

    public static object F_dvp_view_visible_context(object v_actor, object v_mind)
    {
        object v_result__114423 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.not(F_dv_has_key(v_actor, "location_ref"))))
        {
            return v_result__114423;
        }
        object v_own__114511 = F_dv_read(v_mind, F_dv_get(v_actor, "location_ref"));
        if (R.Bool(R.not(F_dv_uncontested(v_own__114511))))
        {
            return v_result__114423;
        }
        object v_previous__114613 = F_dv_find(v_actor, "visible_context", new System.Collections.Generic.List<object>(0) { });
        object v_i__114670 = R.number("0");
        while (R.Bool(R.lt(v_i__114670, R.count(v_previous__114613))))
        {
            object v_entry__114722 = R.at(v_previous__114613, v_i__114670);
            if (R.Bool(R.eq(F_dv_find(R.at(v_entry__114722, R.number("1")), "location", false), F_dv_get(v_own__114511, "value"))))
            {
                _ = R.push(v_result__114423, v_entry__114722);
            }
            v_i__114670 = R.add(v_i__114670, R.number("1"));
        }
        return v_result__114423;
    }

    public static object F_dvp_actor_view(object v_actor, object v_mind)
    {
        v_actor = R.snapshot(v_actor);
        object v_result__114947 = new System.Collections.Generic.List<object>(0) { };
        object v_i__114967 = R.number("0");
        while (R.Bool(R.lt(v_i__114967, R.count(v_actor))))
        {
            object v_entry__115018 = R.at(v_actor, v_i__114967);
            object v_local_1__115047 = false;
            if (R.Bool(R.eq(R.at(v_entry__115018, R.number("0")), "mind")))
            {
                v_local_1__115047 = F_dv_pair("mind", v_mind);
            }
            else
            {
                if (R.Bool(R.eq(R.at(v_entry__115018, R.number("0")), "visible_context")))
                {
                    v_local_1__115047 = F_dv_pair("visible_context", F_dvp_view_visible_context(v_actor, v_mind));
                }
                else
                {
                    v_local_1__115047 = v_entry__115018;
                }
            }
            _ = R.push(v_result__114947, v_local_1__115047);
            v_i__114967 = R.add(v_i__114967, R.number("1"));
        }
        if (R.Bool((R.Bool(R.eq(F_dv_find(v_mind, "domain", ""), "prediction")) ? (object)(R.not(F_dv_has_key(v_actor, "planning_evidence_mind"))) : (object)(false))))
        {
            v_result__114947 = F_dv_with(v_result__114947, "planning_evidence_mind", F_dv_get(v_actor, "mind"));
        }
        return v_result__114947;
    }

    public static object F_dvp_communication_progress(object v_actor, object v_action)
    {
        v_actor = R.snapshot(v_actor);
        object v_goals__115643 = F_dv_find(F_dv_get(v_actor, "policy"), "goals", new System.Collections.Generic.List<object>(0) { });
        // Preserve the original eager basis/availability validation for every
        // communicate goal, in declaration order, once per complete actor
        // snapshot. Argument requirements remain specific to the actual action.
        // Nontext action identifiers retain the original equality/scan path.
        var availableIndex = false;
        if (R.Bool(F_dv_record_like(v_action)) && R.Bool(F_dv_has_key(v_action, "id")) &&
            F_dv_get(v_action, "id") is string method)
        {
            var key = new List<object> { "owned-available-communication-methods-v2" };
            var cached = ReadCachedValue("plan", v_actor, key);
            object index;
            if (R.Bool(R.at(cached, R.number("0"))))
            {
                index = R.at(cached, R.number("1"));
            }
            else
            {
                var groups = new Dictionary<string, List<object>>(StringComparer.Ordinal);
                foreach (var goal in R.Items(v_goals__115643))
                {
                    if (!R.Bool(R.eq(F_dv_find(goal, "kind", ""), "communicate"))) continue;
                    var identifier = F_dv_find(goal, "method_id", "");
                    var available = R.Bool(F_dv_and(
                        R.lt(R.number("0"), R.count(F_dv_find(goal, "basis", new List<object>()))),
                        R.eq(F_dv_truth(F_dv_get(v_actor, "mind"),
                            F_dv_find(goal, "available_when", F_dv_true())), "true")));
                    if (identifier is not string id || !available) continue;
                    if (!groups.TryGetValue(id, out var matching)) groups[id] = matching = new List<object>();
                    matching.Add(goal);
                }
                var entries = new List<object>(groups.Count);
                foreach (var group in groups) entries.Add(F_dv_pair(group.Key, group.Value));
                index = R.snapshot(F_dvs_plan_cache_put(v_actor, key, entries));
            }
            v_goals__115643 = F_dv_find(index, method, new List<object>());
            availableIndex = true;
        }
        object v_i__115694 = R.number("0");
        object v_value__115708 = false;
        while (R.Bool(R.lt(v_i__115694, R.count(v_goals__115643))))
        {
            object v_goal__115767 = R.at(v_goals__115643, v_i__115694);
            if (R.Bool(R.eq(F_dv_find(v_goal__115767, "kind", ""), "communicate")))
            {
                if (availableIndex || R.Bool(F_dv_and(R.eq(F_dv_find(v_goal__115767, "method_id", ""), F_dv_get(v_action, "id")), F_dv_and(R.lt(R.number("0"), R.count(F_dv_find(v_goal__115767, "basis", new System.Collections.Generic.List<object>(0) { }))), R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(v_goal__115767, "available_when", new System.Collections.Generic.List<object>(2) { F_dv_pair("op", "constant"), F_dv_pair("value", "true") })), "true")))))
                {
                    if (R.Bool(F_dv_and(F_dv_find(v_goal__115767, "requires_argument", false), F_dv_find(v_action, "speech_from_argument", false))))
                    {
                        if (R.Bool(F_dv_find(v_action, "argument_available", false)))
                        {
                            v_value__115708 = true;
                        }
                    }
                    if (R.Bool(R.not(F_dv_find(v_goal__115767, "requires_argument", false))))
                    {
                        v_value__115708 = true;
                    }
                }
            }
            v_i__115694 = R.add(v_i__115694, R.number("1"));
        }
        return v_value__115708;
    }

    public static object F_dvp_achievement_pending_work(object v_actor, object v_action, object v_pending)
    {
        object v_i__116492 = R.number("0");
        if (R.Bool(R.eq(R.count(v_pending), R.number("0"))))
        {
            return new System.Collections.Generic.List<object>(3) { F_dv_pair("value", R.number("0")), F_dv_pair("forecasts", R.number("0")), F_dv_pair("forecast_reuses", R.number("0")) };
        }
        object v_name__116603 = F_dv_find(v_action, "predict_law", "");
        object v_adoption__116653 = F_dv_find(F_dv_get(F_dv_get(v_actor, "mind"), "adoptions"), v_name__116603, false);
        if (R.Bool(R.eq(v_adoption__116653, false)))
        {
            return new System.Collections.Generic.List<object>(3) { F_dv_pair("value", R.number("0")), F_dv_pair("forecasts", R.number("0")), F_dv_pair("forecast_reuses", R.number("0")) };
        }
        if (R.Bool((R.Bool(R.not(F_dv_get(v_adoption__116653, "active"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_adoption__116653, "mode"), "predict"))))))
        {
            return new System.Collections.Generic.List<object>(3) { F_dv_pair("value", R.number("0")), F_dv_pair("forecasts", R.number("0")), F_dv_pair("forecast_reuses", R.number("0")) };
        }
        object v_relevant__116931 = false;
        v_i__116492 = R.number("0");
        while (R.Bool((R.Bool(R.lt(v_i__116492, R.count(v_pending))) ? (object)(R.not(v_relevant__116931)) : (object)(false))))
        {
            object v_refs__117020 = new System.Collections.Generic.List<object>(0) { };
            if (R.Bool(F_dv_has_key(R.at(v_pending, v_i__116492), "planning_refs")))
            {
                v_refs__117020 = F_dv_get(R.at(v_pending, v_i__116492), "planning_refs");
            }
            else
            {
                v_refs__117020 = F_dvs_sim_refs(F_dv_get(R.at(v_pending, v_i__116492), "proposition"));
            }
            object v_j__117208 = R.number("0");
            while (R.Bool((R.Bool(R.lt(v_j__117208, R.count(F_dv_get(F_dv_get(v_adoption__116653, "law"), "effects")))) ? (object)(R.not(v_relevant__116931)) : (object)(false))))
            {
                object v_r__117297 = R.number("0");
                while (R.Bool(R.lt(v_r__117297, R.count(v_refs__117020))))
                {
                    if (R.Bool(F_dv_same_local_ref(F_dv_get(v_actor, "id"), F_dv_get(R.at(F_dv_get(F_dv_get(v_adoption__116653, "law"), "effects"), v_j__117208), "ref"), R.at(v_refs__117020, v_r__117297))))
                    {
                        v_relevant__116931 = true;
                    }
                    v_r__117297 = R.add(v_r__117297, R.number("1"));
                }
                v_j__117208 = R.add(v_j__117208, R.number("1"));
            }
            v_i__116492 = R.add(v_i__116492, R.number("1"));
        }
        if (R.Bool(R.not(v_relevant__116931)))
        {
            return new System.Collections.Generic.List<object>(3) { F_dv_pair("value", R.number("0")), F_dv_pair("forecasts", R.number("0")), F_dv_pair("forecast_reuses", R.number("0")) };
        }
        object v_prediction__117624 = F_dvp_forecast(F_dv_get(v_actor, "mind"), v_name__116603);
        object v_forecasts__117676 = R.number("1");
        object v_reuses__117698 = R.number("0");
        if (R.Bool(F_dv_find(v_prediction__117624, "planning_cache_hit", false)))
        {
            v_forecasts__117676 = R.number("0");
            v_reuses__117698 = R.number("1");
        }
        if (R.Bool(R.not(R.eq(F_dv_get(v_prediction__117624, "status"), "applied"))))
        {
            return new System.Collections.Generic.List<object>(3) { F_dv_pair("value", R.number("0")), F_dv_pair("forecasts", v_forecasts__117676), F_dv_pair("forecast_reuses", v_reuses__117698) };
        }
        object v_value__117933 = R.number("0");
        object matchedGoals = new List<object>();
        v_i__116492 = R.number("0");
        while (R.Bool(R.lt(v_i__116492, R.count(v_pending))))
        {
            if (R.Bool(R.eq(F_dv_truth(F_dv_get(v_prediction__117624, "world"), F_dv_get(R.at(v_pending, v_i__116492), "proposition")), "true")))
            {
                v_value__117933 = R.add(v_value__117933, F_dv_find(R.at(v_pending, v_i__116492), "weight", R.number("1")));
                if (R.Bool(R.eq(F_dv_find(v_action, "kind", ""), "move")))
                {
                    object goal = R.at(v_pending, v_i__116492);
                    _ = R.push(matchedGoals, new List<object> { F_dv_pair("proposition", F_dv_get(goal, "proposition")), F_dv_pair("basis", F_dv_find(goal, "basis", new List<object>())), F_dv_pair("weight", F_dv_find(goal, "weight", R.number("1"))) });
                }
            }
            v_i__116492 = R.add(v_i__116492, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(4) { F_dv_pair("value", v_value__117933), F_dv_pair("forecasts", v_forecasts__117676), F_dv_pair("forecast_reuses", v_reuses__117698), F_dv_pair("matched_goals", matchedGoals) };
    }

    public static object F_dvp_refresh_current_evidence(object v_actor, object v_goals)
    {
        object v_result__118356 = new System.Collections.Generic.List<object>(0) { };
        object v_i__118376 = R.number("0");
        while (R.Bool(R.lt(v_i__118376, R.count(v_goals))))
        {
            object v_goal__118425 = R.at(v_goals, v_i__118376);
            if (R.Bool((R.Bool(R.eq(F_dv_find(v_goal__118425, "kind", ""), "evidence")) ? (object)(F_dv_has_key(v_goal__118425, "refresh_until")) : (object)(false))))
            {
                if (R.Bool(R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_goal__118425, "refresh_until")), "true"))))
                {
                    if (R.Bool(F_dv_has_key(v_goal__118425, "refresh_refs")))
                    {
                        object v_relevant_time__118675 = F_dv_find(v_goal__118425, "minimum_time", R.number("0"));
                        object v_r__118748 = R.number("0");
                        while (R.Bool(R.lt(v_r__118748, R.count(F_dv_get(v_goal__118425, "refresh_refs")))))
                        {
                            object v_cell__118841 = F_dv_read(F_dv_get(v_actor, "mind"), R.at(F_dv_get(v_goal__118425, "refresh_refs"), v_r__118748));
                            if (R.Bool(F_dv_get(v_cell__118841, "known")))
                            {
                                v_relevant_time__118675 = F_dv_max(v_relevant_time__118675, F_dv_find(v_cell__118841, "time", R.number("0")));
                            }
                            v_r__118748 = R.add(v_r__118748, R.number("1"));
                        }
                        v_goal__118425 = F_dv_with(v_goal__118425, "minimum_time", v_relevant_time__118675);
                    }
                }
            }
            _ = R.push(v_result__118356, v_goal__118425);
            v_i__118376 = R.add(v_i__118376, R.number("1"));
        }
        return v_result__118356;
    }

    public static object F_dvp_forecast(object v_mind, object v_name)
    {
        object v_key__119314 = new System.Collections.Generic.List<object>(2) { "dvp_forecast", v_name };
        object v_cached__119351 = F_dvs_plan_cache_get(v_mind, v_key__119314);
        if (R.Bool(R.at(v_cached__119351, R.number("0"))))
        {
            return F_dv_with(R.at(v_cached__119351, R.number("1")), "planning_cache_hit", true);
        }
        object v_result__119478 = F_dv_predict(F_dv_with_many(v_mind, new System.Collections.Generic.List<object>(3) { F_dv_pair("domain", "mind"), F_dv_pair("history", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("seen", new System.Collections.Generic.List<object>(0) { }) }), v_name, R.cat("planning-forecast-", R.show(F_dv_get(v_mind, "revision"))), F_dv_find(v_mind, "time", R.number("0")), F_dv_get(v_mind, "revision"));
        _ = F_dvs_plan_cache_put(v_mind, v_key__119314, v_result__119478);
        return F_dv_with(v_result__119478, "planning_cache_hit", false);
    }

    public static object F_dvp_inspection_freshness(object v_actor, object v_mind)
    {
        object v_declared__119897 = F_dv_find(F_dv_get(v_actor, "policy"), "goals", new System.Collections.Generic.List<object>(0) { });
        object v_key__119951 = new System.Collections.Generic.List<object>(2) { "dvp_inspection_freshness-ref-key-v2", v_declared__119897 };
        object v_cached__120015 = F_dvs_plan_cache_get(v_mind, v_key__119951);
        if (R.Bool(R.at(v_cached__120015, R.number("0"))))
        {
            return R.at(v_cached__120015, R.number("1"));
        }
        object v_goals__120105 = F_dvp_refresh_current_evidence(F_dvp_actor_view(v_actor, v_mind), v_declared__119897);
        object v_index__120189 = new System.Collections.Generic.List<object>(0) { };
        object v_i__120208 = R.number("0");
        while (R.Bool(R.lt(v_i__120208, R.count(v_goals__120105))))
        {
            object v_goal__120257 = R.at(v_goals__120105, v_i__120208);
            if (R.Bool((R.Bool(R.eq(F_dv_find(v_goal__120257, "kind", ""), "evidence")) ? (object)(F_dv_has_key(v_goal__120257, "ref")) : (object)(false))))
            {
                object v_name__120369 = F_dv_ref_lookup_key(F_dv_get(v_goal__120257, "ref"));
                object v_entries__120420 = F_dv_append(F_dv_find(v_index__120189, v_name__120369, new System.Collections.Generic.List<object>(0) { }), new System.Collections.Generic.List<object>(6) { F_dv_pair("ref", F_dv_get(v_goal__120257, "ref")), F_dv_pair("minimum_time", F_dv_find(v_goal__120257, "minimum_time", R.number("0"))), F_dv_pair("available", F_dv_truth(v_mind, F_dv_find(v_goal__120257, "available_when", F_dv_true()))), F_dv_pair("due", F_dvp_evidence_due(v_actor, v_goal__120257)), F_dv_pair("source", F_dv_find(v_goal__120257, "source", "perception")), F_dv_pair("topic", F_dvp_topic(v_actor, v_goal__120257)) });
                object v_owned_entries__120744 = F_dv_clone(v_entries__120420);
                object v_position__120794 = R.record_index(v_index__120189, v_name__120369);
                object v_pair__120850 = new System.Collections.Generic.List<object>(2) { v_name__120369, v_owned_entries__120744 };
                if (R.Bool(R.not(R.lt(v_position__120794, R.number("0")))))
                {
                    _ = R.replace(v_index__120189, v_position__120794, v_pair__120850);
                }
                else
                {
                    _ = R.push(v_index__120189, v_pair__120850);
                }
            }
            v_i__120208 = R.add(v_i__120208, R.number("1"));
        }
        return F_dvs_plan_cache_put(v_mind, v_key__119951, v_index__120189);
    }
}
