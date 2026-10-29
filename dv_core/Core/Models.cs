using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Planning / Models: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_sim_rule(object v_predicted, object v_covered, object v_step, object v_mind, object v_confidence, object v_start, object v_i, object v_event_count, object v_selected_index)
    {
        object v_name__221893 = F_dv_get(v_step, "law");
        object v_at__221917 = F_dv_get(v_step, "at");
        object v_adoptions__221938 = F_dv_get(v_mind, "adoptions");
        object v_complete__221973 = true;
        object v_failures__221997 = new System.Collections.Generic.List<object>(0) { };
        object v_trace__222019 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(F_dv_has_key(v_adoptions__221938, v_name__221893)))
        {
            object v_adoption__222078 = F_dv_get(v_adoptions__221938, v_name__221893);
            if (R.Bool(F_dv_and(F_dv_get(v_adoption__222078, "active"), R.eq(F_dv_get(v_adoption__222078, "mode"), "predict"))))
            {
                object v_law__222195 = F_dv_get(v_adoption__222078, "law");
                object v_premise__222230 = F_dv_truth(v_predicted, F_dv_get(F_dv_get(v_adoption__222078, "law"), "premise"));
                object v_inputs__222298 = F_dvs_sim_inputs(v_predicted, F_dv_get(F_dv_get(v_adoption__222078, "law"), "premise"));
                object v_local_confidence__222371 = F_dv_min(F_dv_get(v_adoption__222078, "confidence"), F_dv_min(v_confidence, F_dv_premise_confidence(v_predicted, F_dv_get(F_dv_get(v_adoption__222078, "law"), "premise"))));
                object v_result__222510 = false;
                object v_withheld__222541 = new System.Collections.Generic.List<object>(0) { };
                v_predicted = F_dv_with(v_predicted, "adoptions", F_dv_with(v_adoptions__221938, v_name__221893, F_dv_with(v_adoption__222078, "confidence", v_local_confidence__222371)));
                object v_event_id__222704 = R.cat(R.cat(R.cat("hypothetical-", R.show(v_event_count)), R.cat("-", R.show(v_i))), R.cat("-", R.show(v_selected_index)));
                v_result__222510 = F_dv_predict(F_dv_with(v_predicted, "domain", "mind"), v_name__221893, v_event_id__222704, R.add(v_start, v_at__221917), F_dv_get(v_predicted, "revision"));
                if (R.Bool(R.eq(v_premise__222230, "unknown")))
                {
                    object v_uncertain__223019 = F_dvs_sim_withhold(v_predicted, F_dv_get(v_law__222195, "effects"));
                    v_predicted = F_dv_get(v_uncertain__223019, "world");
                    v_withheld__222541 = F_dv_get(v_uncertain__223019, "refs");
                }
                object v_local_1__223174 = false;
                if (R.Bool(R.eq(v_premise__222230, "false")))
                {
                    if (R.Bool(F_dv_get(v_step, "optional")))
                    {
                        v_local_1__223174 = "condition-false-not-applicable";
                    }
                    else
                    {
                        v_local_1__223174 = "required-premise-false";
                    }
                }
                else
                {
                    if (R.Bool(R.eq(v_premise__222230, "unknown")))
                    {
                        v_local_1__223174 = "conditional-output-unknown";
                    }
                    else
                    {
                        v_local_1__223174 = F_dv_get(v_result__222510, "status");
                    }
                }
                _ = R.push(v_trace__222019, F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("step", v_i), F_dv_pair("at", v_at__221917), F_dv_pair("law", v_name__221893), F_dv_pair("premise", v_premise__222230) }, new System.Collections.Generic.List<object>(8) { F_dv_pair("inputs", v_inputs__222298), F_dv_pair("status", F_dv_get(v_result__222510, "status")), F_dv_pair("changes", F_dv_get(v_result__222510, "changes")), F_dv_pair("basis", F_dv_get(v_adoption__222078, "basis")), F_dv_pair("stage", F_dv_get(v_step, "stage")), F_dv_pair("optional", F_dv_get(v_step, "optional")), F_dv_pair("withheld", v_withheld__222541), F_dv_pair("reason", v_local_1__223174) }));
                if (R.Bool(R.eq(F_dv_get(v_result__222510, "status"), "applied")))
                {
                    object v_changes__223935 = F_dv_get(v_result__222510, "changes");
                    object v_j__223980 = R.number("0");
                    v_predicted = F_dv_get(v_result__222510, "world");
                    while (R.Bool(R.lt(v_j__223980, R.count(v_changes__223935))))
                    {
                        if (R.Bool(F_dv_has(v_covered, F_dv_get(R.at(v_changes__223935, v_j__223980), "ref"))))
                        {
                            _ = false;
                        }
                        else
                        {
                            _ = R.push(v_covered, F_dv_get(R.at(v_changes__223935, v_j__223980), "ref"));
                        }
                        v_j__223980 = R.add(v_j__223980, R.number("1"));
                    }
                }
                else
                {
                    if (R.Bool(F_dv_and(R.eq(v_premise__222230, "false"), F_dv_get(v_step, "optional"))))
                    {
                        _ = false;
                    }
                    else
                    {
                        v_complete__221973 = false;
                        _ = R.push(v_failures__221997, new System.Collections.Generic.List<object>(3) { F_dv_pair("law", v_name__221893), F_dv_pair("reason", F_dv_get(v_result__222510, "status")), F_dv_pair("premise", v_premise__222230) });
                    }
                }
            }
            else
            {
                v_complete__221973 = false;
                _ = R.push(v_failures__221997, new System.Collections.Generic.List<object>(2) { F_dv_pair("law", v_name__221893), F_dv_pair("reason", "not-adopted-predict") });
            }
        }
        else
        {
            v_complete__221973 = false;
            _ = R.push(v_failures__221997, new System.Collections.Generic.List<object>(2) { F_dv_pair("law", v_name__221893), F_dv_pair("reason", "unknown-law") });
        }
        return new System.Collections.Generic.List<object>(5) { F_dv_pair("world", v_predicted), F_dv_pair("coverage", v_covered), F_dv_pair("complete", v_complete__221973), F_dv_pair("failures", v_failures__221997), F_dv_pair("trace", v_trace__222019) };
    }

    public static object F_dvs_sim_equal_guard(object v_proposition, object v_ref)
    {
        if (R.Bool(R.eq(F_dv_get(v_proposition, "op"), "equals")))
        {
            if (R.Bool(F_dv_same(F_dv_get(v_proposition, "ref"), v_ref)))
            {
                return new System.Collections.Generic.List<object>(2) { F_dv_pair("found", true), F_dv_pair("value", F_dv_get(v_proposition, "value")) };
            }
        }
        if (R.Bool(R.eq(F_dv_get(v_proposition, "op"), "all")))
        {
            object v_i__225106 = R.number("0");
            while (R.Bool(R.lt(v_i__225106, R.count(F_dv_get(v_proposition, "items")))))
            {
                object v_result__225177 = F_dvs_sim_equal_guard(R.at(F_dv_get(v_proposition, "items"), v_i__225106), v_ref);
                if (R.Bool(F_dv_get(v_result__225177, "found")))
                {
                    return v_result__225177;
                }
                v_i__225106 = R.add(v_i__225106, R.number("1"));
            }
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("found", false), F_dv_pair("value", false) };
    }

    public static object F_dvs_sim_dispatch(object v_mind, object v_predicted, object v_step)
    {
        object v_table__225411 = F_dv_get(v_step, "dispatch");
        object v_selector__225441 = F_dv_read(v_predicted, F_dv_get(v_table__225411, "selector"));
        object v_indexed__225495 = F_dv_uncontested(v_selector__225441);
        object v_entries__225538 = new System.Collections.Generic.List<object>(0) { };
        object v_i__225559 = R.number("0");
        while (R.Bool(R.lt(v_i__225559, R.count(F_dv_get(v_table__225411, "laws")))))
        {
            object v_name__225615 = R.at(F_dv_get(v_table__225411, "laws"), v_i__225559);
            object v_adoption__225648 = F_dv_find(F_dv_get(v_mind, "adoptions"), v_name__225615, false);
            object v_guard__225708 = new System.Collections.Generic.List<object>(2) { F_dv_pair("found", false), F_dv_pair("value", false) };
            if (R.Bool(R.eq(v_adoption__225648, false)))
            {
                v_indexed__225495 = false;
            }
            else
            {
                if (R.Bool((R.Bool(R.not(F_dv_get(v_adoption__225648, "active"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_adoption__225648, "mode"), "predict"))))))
                {
                    v_indexed__225495 = false;
                }
                v_guard__225708 = F_dvs_sim_equal_guard(F_dv_get(F_dv_get(v_adoption__225648, "law"), "premise"), F_dv_get(v_table__225411, "selector"));
                if (R.Bool(R.not(F_dv_get(v_guard__225708, "found"))))
                {
                    v_indexed__225495 = false;
                }
                object v_j__226083 = R.number("0");
                while (R.Bool(R.lt(v_j__226083, R.count(F_dv_get(F_dv_get(v_adoption__225648, "law"), "effects")))))
                {
                    if (R.Bool(F_dv_same(F_dv_get(R.at(F_dv_get(F_dv_get(v_adoption__225648, "law"), "effects"), v_j__226083), "ref"), F_dv_get(v_table__225411, "selector"))))
                    {
                        v_indexed__225495 = false;
                    }
                    v_j__226083 = R.add(v_j__226083, R.number("1"));
                }
            }
            _ = R.push(v_entries__225538, new System.Collections.Generic.List<object>(2) { F_dv_pair("law", v_name__225615), F_dv_pair("guard", v_guard__225708) });
            v_i__225559 = R.add(v_i__225559, R.number("1"));
        }
        object v_selected__226362 = new System.Collections.Generic.List<object>(0) { };
        object v_excluded__226384 = new System.Collections.Generic.List<object>(0) { };
        v_i__225559 = R.number("0");
        while (R.Bool(R.lt(v_i__225559, R.count(v_entries__225538))))
        {
            object v_entry__226455 = R.at(v_entries__225538, v_i__225559);
            if (R.Bool((R.Bool(v_indexed__225495) ? (object)(R.not(F_dv_same(F_dv_get(F_dv_get(v_entry__226455, "guard"), "value"), F_dv_get(v_selector__225441, "value")))) : (object)(false))))
            {
                _ = R.push(v_excluded__226384, v_entry__226455);
            }
            else
            {
                _ = R.push(v_selected__226362, new System.Collections.Generic.List<object>(4) { F_dv_pair("law", F_dv_get(v_entry__226455, "law")), F_dv_pair("at", F_dv_get(v_step, "at")), F_dv_pair("stage", F_dv_get(v_step, "stage")), F_dv_pair("optional", F_dv_get(v_step, "optional")) });
            }
            v_i__225559 = R.add(v_i__225559, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(4) { F_dv_pair("steps", v_selected__226362), F_dv_pair("indexed", v_indexed__225495), F_dv_pair("excluded", v_excluded__226384), F_dv_pair("inputs", new System.Collections.Generic.List<object>(1) { new System.Collections.Generic.List<object>(2) { F_dv_pair("ref", F_dv_get(v_table__225411, "selector")), F_dv_pair("cell", v_selector__225441) } }) };
    }

    public static object F_dvs_slice_intersects(object v_left, object v_right)
    {
        object v_i__227124 = R.number("0");
        while (R.Bool(R.lt(v_i__227124, R.count(v_left))))
        {
            if (R.Bool(F_dv_has(v_right, R.at(v_left, v_i__227124))))
            {
                return true;
            }
            v_i__227124 = R.add(v_i__227124, R.number("1"));
        }
        return false;
    }

    public static object F_dvs_slice_law_io(object v_mind, object v_name)
    {
        object v_adoption__227298 = F_dv_find(F_dv_get(v_mind, "adoptions"), v_name, false);
        if (R.Bool(R.eq(v_adoption__227298, false)))
        {
            return new System.Collections.Generic.List<object>(3) { F_dv_pair("known", false), F_dv_pair("reads", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("writes", new System.Collections.Generic.List<object>(0) { }) };
        }
        object v_law__227441 = F_dv_get(v_adoption__227298, "law");
        object v_reads__227468 = F_dvs_sim_refs(F_dv_get(v_law__227441, "premise"));
        object v_writes__227510 = new System.Collections.Generic.List<object>(0) { };
        object v_i__227530 = R.number("0");
        while (R.Bool(R.lt(v_i__227530, R.count(F_dv_get(v_law__227441, "effects")))))
        {
            object v_effect__227587 = R.at(F_dv_get(v_law__227441, "effects"), v_i__227530);
            v_writes__227510 = F_dv_explanation_merge(v_writes__227510, new System.Collections.Generic.List<object>(1) { F_dv_get(v_effect__227587, "ref") });
            if (R.Bool(R.eq(F_dv_get(v_effect__227587, "op"), "increment")))
            {
                v_reads__227468 = F_dv_explanation_merge(v_reads__227468, new System.Collections.Generic.List<object>(1) { F_dv_get(v_effect__227587, "ref") });
            }
            v_i__227530 = R.add(v_i__227530, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("known", (R.Bool(F_dv_get(v_adoption__227298, "active")) ? (object)(R.eq(F_dv_get(v_adoption__227298, "mode"), "predict")) : (object)(false))), F_dv_pair("reads", v_reads__227468), F_dv_pair("writes", v_writes__227510) };
    }

    public static object F_dvs_slice_step_io(object v_actor, object v_step)
    {
        if (R.Bool(F_dv_has_key(v_step, "response")))
        {
            object v_model__227978 = F_dv_find(F_dv_find(v_actor, "response_models", new System.Collections.Generic.List<object>(0) { }), F_dv_get(F_dv_get(v_step, "response"), "model"), false);
            if (R.Bool(R.eq(v_model__227978, false)))
            {
                return new System.Collections.Generic.List<object>(3) { F_dv_pair("known", false), F_dv_pair("reads", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("writes", new System.Collections.Generic.List<object>(0) { }) };
            }
            object v_response_reads__228165 = F_dvs_sim_refs(F_dv_find(v_step, "available_when", F_dv_true()));
            object v_response_writes__228251 = new System.Collections.Generic.List<object>(0) { };
            object v_context__228284 = F_dv_find(v_model__227978, "context", new System.Collections.Generic.List<object>(0) { });
            object v_k__228336 = R.number("0");
            while (R.Bool(R.lt(v_k__228336, R.count(v_context__228284))))
            {
                v_response_reads__228165 = F_dv_explanation_merge(v_response_reads__228165, new System.Collections.Generic.List<object>(1) { F_dv_get(R.at(v_context__228284, v_k__228336), "from") });
                v_k__228336 = R.add(v_k__228336, R.number("1"));
            }
            if (R.Bool(F_dv_has_key(v_model__227978, "choice_ref")))
            {
                v_response_writes__228251 = new System.Collections.Generic.List<object>(1) { F_dv_get(v_model__227978, "choice_ref") };
            }
            v_k__228336 = R.number("0");
            object v_response_known__228607 = true;
            while (R.Bool(R.lt(v_k__228336, R.count(F_dv_get(F_dv_get(v_step, "response"), "options")))))
            {
                object v_option__228698 = F_dv_find(F_dv_get(v_model__227978, "options"), R.at(F_dv_get(F_dv_get(v_step, "response"), "options"), v_k__228336), new System.Collections.Generic.List<object>(0) { });
                object v_bridge__228776 = F_dvs_slice_law_io(F_dv_get(v_actor, "mind"), F_dv_find(v_option__228698, "bridge_law", ""));
                v_response_known__228607 = (R.Bool(v_response_known__228607) ? (object)(F_dv_get(v_bridge__228776, "known")) : (object)(false));
                v_response_reads__228165 = F_dv_explanation_merge(v_response_reads__228165, F_dv_get(v_bridge__228776, "reads"));
                v_response_writes__228251 = F_dv_explanation_merge(v_response_writes__228251, F_dv_get(v_bridge__228776, "writes"));
                v_k__228336 = R.add(v_k__228336, R.number("1"));
            }
            return new System.Collections.Generic.List<object>(3) { F_dv_pair("known", v_response_known__228607), F_dv_pair("reads", v_response_reads__228165), F_dv_pair("writes", v_response_writes__228251) };
        }
        object v_names__229196 = new System.Collections.Generic.List<object>(1) { F_dv_get(v_step, "law") };
        object v_reads__229223 = new System.Collections.Generic.List<object>(0) { };
        object v_writes__229242 = new System.Collections.Generic.List<object>(0) { };
        object v_known__229262 = true;
        if (R.Bool(F_dv_has_key(v_step, "dispatch")))
        {
            v_names__229196 = F_dv_get(F_dv_get(v_step, "dispatch"), "laws");
            v_reads__229223 = new System.Collections.Generic.List<object>(1) { F_dv_get(F_dv_get(v_step, "dispatch"), "selector") };
        }
        object v_i__229396 = R.number("0");
        while (R.Bool(R.lt(v_i__229396, R.count(v_names__229196))))
        {
            object v_io__229447 = F_dvs_slice_law_io(F_dv_get(v_actor, "mind"), R.at(v_names__229196, v_i__229396));
            v_known__229262 = (R.Bool(v_known__229262) ? (object)(F_dv_get(v_io__229447, "known")) : (object)(false));
            v_reads__229223 = F_dv_explanation_merge(v_reads__229223, F_dv_get(v_io__229447, "reads"));
            v_writes__229242 = F_dv_explanation_merge(v_writes__229242, F_dv_get(v_io__229447, "writes"));
            v_i__229396 = R.add(v_i__229396, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("known", v_known__229262), F_dv_pair("reads", v_reads__229223), F_dv_pair("writes", v_writes__229242) };
    }

    public static object F_dvs_slice_feature(object v_hypothesis, object v_feature, object v_plan, object v_plan_io)
    {
        object v_needed__229784 = F_dvs_sim_refs(F_dvs_sim_feature(v_feature));
        object v_selected__229840 = new System.Collections.Generic.List<object>(0) { };
        object v_keep_prefix__229862 = false;
        object v_i__229890 = R.sub(R.count(v_plan), R.number("1"));
        while (R.Bool(R.not(R.lt(v_i__229890, R.number("0")))))
        {
            object v_step__229945 = R.at(v_plan, v_i__229890);
            if (R.Bool((R.Bool(R.not(F_dv_has_key(v_feature, "at"))) ? (object)(true) : (object)(R.not(R.lt(F_dv_get(v_feature, "at"), F_dv_get(v_step__229945, "at")))))))
            {
                object v_io__230045 = R.at(v_plan_io, v_i__229890);
                if (R.Bool((R.Bool((R.Bool(v_keep_prefix__229862) ? (object)(true) : (object)(R.not(F_dv_get(v_io__230045, "known"))))) ? (object)(true) : (object)(F_dvs_slice_intersects(F_dv_get(v_io__230045, "writes"), v_needed__229784)))))
                {
                    v_selected__229840 = F_dv_explanation_merge(v_selected__229840, new System.Collections.Generic.List<object>(1) { v_i__229890 });
                    v_needed__229784 = F_dv_explanation_merge(v_needed__229784, F_dv_get(v_io__230045, "reads"));
                    if (R.Bool(R.not(F_dv_get(v_io__230045, "known"))))
                    {
                        v_keep_prefix__229862 = true;
                    }
                }
            }
            v_i__229890 = R.sub(v_i__229890, R.number("1"));
        }
        object v_timeline__230377 = new System.Collections.Generic.List<object>(0) { };
        v_i__229890 = R.number("0");
        while (R.Bool(R.lt(v_i__229890, R.count(v_plan))))
        {
            if (R.Bool(F_dv_has(v_selected__229840, v_i__229890)))
            {
                _ = R.push(v_timeline__230377, R.at(v_plan, v_i__229890));
            }
            v_i__229890 = R.add(v_i__229890, R.number("1"));
        }
        object v_assumptions__230530 = new System.Collections.Generic.List<object>(0) { };
        object v_original__230555 = F_dv_find(v_hypothesis, "assumptions", new System.Collections.Generic.List<object>(0) { });
        v_i__229890 = R.number("0");
        while (R.Bool(R.lt(v_i__229890, R.count(v_original__230555))))
        {
            if (R.Bool((R.Bool(v_keep_prefix__229862) ? (object)(true) : (object)(F_dvs_slice_intersects(F_dvs_sim_refs(R.at(v_original__230555, v_i__229890)), v_needed__229784)))))
            {
                _ = R.push(v_assumptions__230530, R.at(v_original__230555, v_i__229890));
            }
            v_i__229890 = R.add(v_i__229890, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(6) { F_dv_pair("timeline", v_timeline__230377), F_dv_pair("assumptions", v_assumptions__230530), F_dv_pair("feature", F_dv_without(v_feature, "at")), F_dv_pair("original_feature", v_feature), F_dv_pair("size", R.add(R.count(v_timeline__230377), R.count(v_assumptions__230530))), F_dv_pair("mask_plan", v_plan) };
    }

    public static object F_dvs_model_probe_key(object v_actor, object v_hypothesis, object v_time)
    {
        object v_plan__231286 = F_dvs_sim_plan(v_hypothesis);
        object v_start__231326 = F_dv_find(v_hypothesis, "start_time", v_time);
        object v_mask__231382 = F_dv_find(v_hypothesis, "mask_plan", v_plan__231286);
        object v_initial__231436 = F_dvs_sim_mask_response(v_actor, F_dvs_sim_mask(F_dv_get(v_actor, "mind"), v_mask__231382, v_start__231326), v_mask__231382);
        return new System.Collections.Generic.List<object>(10) { F_dv_get(v_actor, "id"), v_initial__231436, F_dv_find(v_actor, "response_models", new System.Collections.Generic.List<object>(0) { }), F_dv_find(F_dv_get(v_actor, "policy"), "Replay_Depth", R.count(v_plan__231286)), v_plan__231286, F_dv_find(v_hypothesis, "assumptions", new System.Collections.Generic.List<object>(0) { }), F_dv_find(v_hypothesis, "features", new System.Collections.Generic.List<object>(0) { }), F_dv_find(v_hypothesis, "prior", R.number("0.5")), F_dv_get(v_hypothesis, "basis"), v_start__231326 };
    }

    public static object F_dvs_model_probe(object v_actor, object v_hypothesis, object v_events, object v_time, object v_parent)
    {
        if (R.Bool(R.not(F_dv_find(F_dv_get(v_actor, "policy"), "opt_replaycache", true))))
        {
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("result", F_dvs_model_execute(v_actor, v_hypothesis, v_events, v_time, v_parent)), F_dv_pair("cache", new System.Collections.Generic.List<object>(0) { }) };
        }
        object v_key__232016 = F_dvs_model_probe_key(v_actor, v_hypothesis, v_time);
        object v_cache__232075 = F_dv_find(v_actor, "model_probe_cache", new System.Collections.Generic.List<object>(0) { });
        object v_i__232131 = R.number("0");
        while (R.Bool(R.lt(v_i__232131, R.count(v_cache__232075))))
        {
            object v_saved__232180 = R.at(v_cache__232075, v_i__232131);
            if (R.Bool((R.Bool(F_dv_same(F_dv_get(v_saved__232180, "key"), v_key__232016)) ? (object)(R.not(R.lt(F_dvs_sim_work_budget(v_actor), F_dv_get(F_dv_get(v_saved__232180, "result"), "expanded")))) : (object)(false))))
            {
                object v_result__232305 = F_dv_with_many(F_dv_get(v_saved__232180, "result"), new System.Collections.Generic.List<object>(6) { F_dv_pair("expanded", R.number("0")), F_dv_pair("total_expanded", R.number("0")), F_dv_pair("remaining", F_dv_find(F_dv_get(v_actor, "policy"), "model_budget", R.number("0"))), F_dv_pair("work_remaining", F_dvs_sim_work_budget(v_actor)), F_dv_pair("reused_work", F_dv_get(F_dv_get(v_saved__232180, "result"), "expanded")), F_dv_pair("reused_from_event", F_dv_get(v_saved__232180, "event")) });
                _ = F_dvs_event(v_events, "model_probe_reuse", F_dv_get(v_actor, "id"), v_time, F_dvs_cognitive_parents(v_actor, new System.Collections.Generic.List<object>(2) { v_parent, F_dv_get(v_saved__232180, "event") }), new System.Collections.Generic.List<object>(4) { F_dv_pair("hypothesis", v_hypothesis), F_dv_pair("source_event", F_dv_get(v_saved__232180, "event")), F_dv_pair("result", v_result__232305), F_dv_pair("scope", "identical-completed-causal-probe-not-new-prediction") });
                return new System.Collections.Generic.List<object>(2) { F_dv_pair("result", v_result__232305), F_dv_pair("cache", v_cache__232075) };
            }
            v_i__232131 = R.add(v_i__232131, R.number("1"));
        }
        object v_result__232891 = F_dvs_model_execute(v_actor, v_hypothesis, v_events, v_time, v_parent);
        object v_next__232967 = v_cache__232075;
        if (R.Bool((R.Bool((R.Bool(F_dv_get(v_result__232891, "plan_complete")) ? (object)(R.eq(R.count(F_dv_get(v_result__232891, "failures")), R.number("0"))) : (object)(false))) ? (object)(R.not(R.eq(F_dv_get(v_result__232891, "stop_reason"), "budget-exhausted"))) : (object)(false))))
        {
            v_next__232967 = F_dv_append(v_cache__232075, new System.Collections.Generic.List<object>(3) { F_dv_pair("key", v_key__232016), F_dv_pair("result", v_result__232891), F_dv_pair("event", F_dv_get(R.at(v_events, R.sub(R.count(v_events), R.number("1"))), "id")) });
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("result", v_result__232891), F_dv_pair("cache", v_next__232967) };
    }

    public static object F_dvs_model(object v_actor, object v_hypothesis, object v_events, object v_time, object v_parent)
    {
        object v_features__233304 = F_dv_find(v_hypothesis, "features", new System.Collections.Generic.List<object>(0) { });
        object v_plan__233359 = F_dvs_sim_plan(v_hypothesis);
        object v_plan_io__233399 = new System.Collections.Generic.List<object>(0) { };
        object v_p__233420 = R.number("0");
        while (R.Bool(R.lt(v_p__233420, R.count(v_plan__233359))))
        {
            _ = R.push(v_plan_io__233399, F_dvs_slice_step_io(v_actor, R.at(v_plan__233359, v_p__233420)));
            v_p__233420 = R.add(v_p__233420, R.number("1"));
        }
        object v_smallest__233544 = false;
        object v_i__233569 = R.number("0");
        while (R.Bool(R.lt(v_i__233569, R.count(v_features__233304))))
        {
            object v_feature__233623 = R.at(v_features__233304, v_i__233569);
            object v_observed__233657 = F_dv_find(v_feature__233623, "observation", F_dvs_sim_feature(v_feature__233623));
            if (R.Bool(R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), v_observed__233657), "unknown"))))
            {
                object v_slice__233800 = F_dvs_slice_feature(v_hypothesis, v_feature__233623, v_plan__233359, v_plan_io__233399);
                if (R.Bool(R.eq(v_smallest__233544, false)))
                {
                    v_smallest__233544 = v_slice__233800;
                }
                else
                {
                    if (R.Bool(R.lt(F_dv_get(v_slice__233800, "size"), F_dv_get(v_smallest__233544, "size"))))
                    {
                        v_smallest__233544 = v_slice__233800;
                    }
                }
            }
            v_i__233569 = R.add(v_i__233569, R.number("1"));
        }
        object v_full_size__234061 = R.add(R.count(v_plan__233359), R.count(F_dv_find(v_hypothesis, "assumptions", new System.Collections.Generic.List<object>(0) { })));
        if (R.Bool(R.eq(v_smallest__233544, false)))
        {
            return F_dvs_model_execute(v_actor, v_hypothesis, v_events, v_time, v_parent);
        }
        if (R.Bool(R.not(R.lt(R.mul(F_dv_get(v_smallest__233544, "size"), R.number("2")), v_full_size__234061))))
        {
            return F_dvs_model_execute(v_actor, v_hypothesis, v_events, v_time, v_parent);
        }
        object v_trial__234369 = F_dv_with_many(v_hypothesis, new System.Collections.Generic.List<object>(5) { F_dv_pair("timeline", F_dv_get(v_smallest__233544, "timeline")), F_dv_pair("assumptions", F_dv_get(v_smallest__233544, "assumptions")), F_dv_pair("features", new System.Collections.Generic.List<object>(1) { F_dv_get(v_smallest__233544, "feature") }), F_dv_pair("mask_plan", F_dv_get(v_smallest__233544, "mask_plan")), F_dv_pair("evaluation_scope", new System.Collections.Generic.List<object>(2) { F_dv_pair("kind", "causal-refutation"), F_dv_pair("feature", F_dv_get(v_smallest__233544, "original_feature")) }) });
        object v_probed__234634 = F_dvs_model_probe(v_actor, v_trial__234369, v_events, v_time, v_parent);
        object v_probe__234703 = F_dv_get(v_probed__234634, "result");
        if (R.Bool(R.eq(F_dv_get(v_probe__234703, "status"), "inconsistent")))
        {
            return F_dv_with_many(v_probe__234703, new System.Collections.Generic.List<object>(2) { F_dv_pair("evaluation_scope", F_dv_get(v_trial__234369, "evaluation_scope")), F_dv_pair("model_probe_cache", F_dv_get(v_probed__234634, "cache")) });
        }
        object v_next__234890 = F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(2) { F_dv_pair("model_previous_event", F_dv_get(R.at(v_events, R.sub(R.count(v_events), R.number("1"))), "id")), F_dv_pair("policy", F_dv_with_many(F_dv_get(v_actor, "policy"), new System.Collections.Generic.List<object>(2) { F_dv_pair("model_budget", F_dv_get(v_probe__234703, "remaining")), F_dv_pair("model_work_remaining", F_dv_get(v_probe__234703, "work_remaining")) })) });
        object v_result__235103 = F_dvs_model_execute(v_next__234890, v_hypothesis, v_events, v_time, v_parent);
        return F_dv_with_many(v_result__235103, new System.Collections.Generic.List<object>(5) { F_dv_pair("probe_expanded", F_dv_get(v_probe__234703, "expanded")), F_dv_pair("probe_reused_work", F_dv_find(v_probe__234703, "reused_work", R.number("0"))), F_dv_pair("model_probe_cache", F_dv_get(v_probed__234634, "cache")), F_dv_pair("expanded", R.add(F_dv_get(v_probe__234703, "expanded"), F_dv_get(v_result__235103, "expanded"))), F_dv_pair("total_expanded", R.add(F_dv_get(v_probe__234703, "expanded"), F_dv_get(v_result__235103, "total_expanded"))) });
    }
}
