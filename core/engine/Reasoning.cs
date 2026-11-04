using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Cognition / Reasoning: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_reason_cell(object v_cell)
    {
        return F_dv_without(v_cell, "time");
    }

    public static object F_dvs_reason_value(object v_cell)
    {
        return new System.Collections.Generic.List<object>(4) { F_dv_pair("known", F_dv_get(v_cell, "known")), F_dv_pair("value", F_dv_get(v_cell, "value")), F_dv_pair("confidence", F_dv_get(v_cell, "confidence")), F_dv_pair("conflicted", F_dv_get(v_cell, "conflicted")) };
    }

    public static object F_dvs_reason_premises(object v_mind, object v_proposition)
    {
        object v_op__4191 = F_dv_get(v_proposition, "op");
        if (R.Bool(R.eq(v_op__4191, "held_for")))
        {
            return new System.Collections.Generic.List<object>(3) { F_dv_pair("proposition", v_proposition), F_dv_pair("support", F_dv_read(v_mind, F_dv_get(v_proposition, "ref"))), F_dv_pair("clock", F_dv_read(v_mind, F_dv_get(v_proposition, "clock"))) };
        }
        if (R.Bool(R.eq(v_op__4191, "constant")))
        {
            return v_proposition;
        }
        else
        {
            if (R.Bool(R.eq(v_op__4191, "not")))
            {
                return F_dvs_reason_premises(v_mind, F_dv_get(v_proposition, "item"));
            }
            else
            {
                if (R.Bool(R.eq(v_op__4191, "all")))
                {
                    object v_items__4735 = F_dv_get(v_proposition, "items");
                    object v_result__4781 = new System.Collections.Generic.List<object>(0) { };
                    object v_i__4813 = R.number("0");
                    while (R.Bool(R.lt(v_i__4813, R.count(v_items__4735))))
                    {
                        _ = R.push(v_result__4781, F_dvs_reason_premises(v_mind, R.at(v_items__4735, v_i__4813)));
                        v_i__4813 = R.add(v_i__4813, R.number("1"));
                    }
                    return v_result__4781;
                }
                else
                {
                    return new System.Collections.Generic.List<object>(2) { F_dv_pair("proposition", v_proposition), F_dv_pair("support", F_dvs_reason_cell(F_dv_read(v_mind, F_dv_get(v_proposition, "ref")))) };
                }
            }
        }
    }

    public static object F_dvs_reason_signature(object v_mind, object v_adoption)
    {
        object v_law__5179 = F_dv_get(v_adoption, "law");
        object v_effects__5206 = F_dv_get(F_dv_get(v_adoption, "law"), "effects");
        object v_outputs__5245 = new System.Collections.Generic.List<object>(0) { };
        object v_i__5266 = R.number("0");
        while (R.Bool(R.lt(v_i__5266, R.count(v_effects__5206))))
        {
            object v_cell__5319 = F_dv_read(v_mind, F_dv_get(R.at(v_effects__5206, v_i__5266), "ref"));
            _ = R.push(v_outputs__5245, new System.Collections.Generic.List<object>(4) { F_dv_pair("known", F_dv_get(v_cell__5319, "known")), F_dv_pair("value", F_dv_get(v_cell__5319, "value")), F_dv_pair("confidence", F_dv_get(v_cell__5319, "confidence")), F_dv_pair("conflicted", F_dv_get(v_cell__5319, "conflicted")) });
            v_i__5266 = R.add(v_i__5266, R.number("1"));
        }
        return F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("law", v_law__5179), F_dv_pair("confidence", F_dv_get(v_adoption, "confidence")), F_dv_pair("basis", F_dv_get(v_adoption, "basis")), F_dv_pair("premises", F_dvs_reason_premises(v_mind, F_dv_get(v_law__5179, "premise"))) }, "outputs", v_outputs__5245);
    }

    public static object F_dvs_reason_changes(object v_changes)
    {
        object v_i__5713 = R.number("0");
        object v_changed__5727 = false;
        while (R.Bool(R.lt(v_i__5713, R.count(v_changes))))
        {
            object v_change__5790 = R.at(v_changes, v_i__5713);
            if (R.Bool(F_dv_same(F_dvs_reason_value(F_dv_get(v_change__5790, "before")), F_dvs_reason_value(F_dv_get(v_change__5790, "after")))))
            {
                _ = false;
            }
            else
            {
                v_changed__5727 = true;
            }
            v_i__5713 = R.add(v_i__5713, R.number("1"));
        }
        return v_changed__5727;
    }

    public static object F_dvs_reason_monotone_satisfied(object v_mind, object v_effects)
    {
        object v_e__6051 = R.number("0");
        object v_n__6065 = R.count(v_effects);
        while (R.Bool(R.lt(v_e__6051, v_n__6065)))
        {
            object v_eff__6116 = R.at(v_effects, v_e__6051);
            if (R.Bool(R.not(R.eq(F_dv_find(v_eff__6116, "op", ""), "assign"))))
            {
                return false;
            }
            object v_cell__6217 = F_dv_read(v_mind, F_dv_get(v_eff__6116, "ref"));
            if (R.Bool((R.Bool((R.Bool(R.not(F_dv_get(v_cell__6217, "known"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_cell__6217, "value"), F_dv_get(v_eff__6116, "value")))))) ? (object)(true) : (object)(R.lt(F_dv_get(v_cell__6217, "confidence"), R.number("1"))))))
            {
                return false;
            }
            v_e__6051 = R.add(v_e__6051, R.number("1"));
        }
        return true;
    }

    public static object F_dvs_dirty_refs(object v_condition) => DirtyRefs(v_condition, false);

    // Dirty truth only reads these declarations. Keep the public operation's
    // writable result while its internal query borrows immutable cache values.
    private static object DirtyRefs(object v_condition, bool readOnly)
    {
        object key = new System.Collections.Generic.List<object>(1) { "dirty-declaration-refs-v1" };
        object v_cached__6580 = readOnly ? R.memo_read(v_condition, key) : R.memo_get(v_condition, key);
        if (R.Bool(R.at(v_cached__6580, R.number("0"))))
        {
            return R.at(v_cached__6580, R.number("1"));
        }
        object v_op__6694 = F_dv_get(v_condition, "op");
        object v_refs__6720 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.eq(v_op__6694, "held_for")))
        {
            object v_declared__6767 = F_dv_condition_inputs(v_condition);
            _ = F_dv_require(F_dv_get(v_declared__6767, "complete"), "Invalid held-for dependency declaration");
            object declaredResult = R.memo_put(v_condition, new System.Collections.Generic.List<object>(1) { "dirty-declaration-refs-v1" }, F_dv_get(v_declared__6767, "reads"));
            return readOnly ? R.snapshot(declaredResult) : declaredResult;
        }
        if (R.Bool((R.Bool(R.eq(v_op__6694, "equals")) ? (object)(true) : (object)(R.eq(v_op__6694, "at_least")))))
        {
            _ = R.push(v_refs__6720, F_dv_get(v_condition, "ref"));
        }
        else
        {
            if (R.Bool(R.eq(v_op__6694, "not")))
            {
                v_refs__6720 = DirtyRefs(F_dv_get(v_condition, "item"), readOnly);
            }
            else
            {
                if (R.Bool(R.eq(v_op__6694, "all")))
                {
                    object v_i__7201 = R.number("0");
                    while (R.Bool(R.lt(v_i__7201, R.count(F_dv_get(v_condition, "items")))))
                    {
                        v_refs__6720 = F_dvp_merge_refs(v_refs__6720, DirtyRefs(R.at(F_dv_get(v_condition, "items"), v_i__7201), readOnly));
                        v_i__7201 = R.add(v_i__7201, R.number("1"));
                    }
                }
                else
                {
                    _ = F_dv_require(R.eq(v_op__6694, "constant"), R.cat("Unindexed condition operator: ", v_op__6694));
                }
            }
        }
        object result = R.memo_put(v_condition, new System.Collections.Generic.List<object>(1) { "dirty-declaration-refs-v1" }, v_refs__6720);
        return readOnly ? R.snapshot(result) : result;
    }

    public static object F_dvs_dirty_inputs(object v_world, object v_refs)
    {
        object v_result__7597 = new System.Collections.Generic.List<object>(1) { F_dv_get(v_world, "subject") };
        object v_i__7630 = R.number("0");
        while (R.Bool(R.lt(v_i__7630, R.count(v_refs))))
        {
            _ = R.push(v_result__7597, new System.Collections.Generic.List<object>(2) { F_dv_has_key(F_dv_get(v_world, "objects"), F_dv_get(R.at(v_refs, v_i__7630), "object")), F_dv_read(v_world, R.at(v_refs, v_i__7630)) });
            v_i__7630 = R.add(v_i__7630, R.number("1"));
        }
        return v_result__7597;
    }

    public static object F_dvs_dirty_truth(object v_world, object v_condition, object v_config, object v_name)
    {
        if (R.Bool(R.not(F_dv_find(v_config, "opt_dirty", false))))
        {
            return F_dv_truth(v_world, v_condition);
        }
        v_condition = R.snapshot(v_condition);
        object v_inputs__8069 = F_dvs_dirty_inputs(v_world, DirtyRefs(v_condition, true));
        object v_key__8137 = new System.Collections.Generic.List<object>(2) { "dirty-truth-last-v1", F_dv_get(v_world, "subject") };
        object v_cached__8190 = R.memo_read(v_condition, v_key__8137);
        object v_calls__8235 = R.number("1");
        if (R.Bool(R.at(v_cached__8190, R.number("0"))))
        {
            v_calls__8235 = R.add(F_dv_get(R.at(v_cached__8190, R.number("1")), "calls"), R.number("1"));
            if (R.Bool(F_dv_same(v_inputs__8069, F_dv_get(R.at(v_cached__8190, R.number("1")), "inputs"))))
            {
                object v_every__8361 = F_dv_find(v_config, "opt_dirty_verify", R.number("0"));
                if (R.Bool((R.Bool(R.lt(R.number("0"), v_every__8361)) ? (object)(R.not(R.lt(v_calls__8235, v_every__8361))) : (object)(false))))
                {
                    _ = F_dv_require(F_dv_same(F_dv_get(R.at(v_cached__8190, R.number("1")), "truth"), F_dv_truth_uncached(v_world, v_condition)), R.cat("Dirty dependency mismatch: ", v_name));
                    v_calls__8235 = R.number("0");
                }
                _ = R.memo_put(v_condition, v_key__8137, F_dv_with(R.at(v_cached__8190, R.number("1")), "calls", v_calls__8235));
                return F_dv_get(R.at(v_cached__8190, R.number("1")), "truth");
            }
        }
        object v_truth__8733 = F_dv_truth(v_world, v_condition);
        _ = R.memo_put(v_condition, v_key__8137, new System.Collections.Generic.List<object>(3) { F_dv_pair("inputs", v_inputs__8069), F_dv_pair("truth", v_truth__8733), F_dv_pair("calls", v_calls__8235) });
        return v_truth__8733;
    }

    public static object F_dvs_dirty_reason_index(object v_actor)
    {
        object v_mind__9070 = F_dv_get(v_actor, "mind");
        object v_adoptions__9096 = R.snapshot(F_dv_get(v_mind__9070, "adoptions"));
        object v_key__9144 = new System.Collections.Generic.List<object>(4) { "dirty-reason-index-v2", F_dv_get(v_mind__9070, "subject"), F_dv_find(v_actor, "inference_signatures", new System.Collections.Generic.List<object>(0) { }), F_dv_find(v_mind__9070, "profile", new System.Collections.Generic.List<object>(0) { }) };
        object v_cached__9272 = R.memo_get(v_adoptions__9096, v_key__9144);
        if (R.Bool(R.at(v_cached__9272, R.number("0"))))
        {
            return R.at(v_cached__9272, R.number("1"));
        }
        object v_result__9360 = new System.Collections.Generic.List<object>(0) { };
        object v_i__9380 = R.number("0");
        while (R.Bool(R.lt(v_i__9380, R.count(v_adoptions__9096))))
        {
            object v_entry__9433 = R.at(v_adoptions__9096, v_i__9380);
            object v_refs__9466 = new System.Collections.Generic.List<object>(0) { };
            if (R.Bool((R.Bool(F_dv_get(R.at(v_entry__9433, R.number("1")), "active")) ? (object)(R.eq(F_dv_get(R.at(v_entry__9433, R.number("1")), "mode"), "infer")) : (object)(false))))
            {
                object v_law__9629 = F_dv_get(R.at(v_entry__9433, R.number("1")), "law");
                v_refs__9466 = F_dvs_dirty_refs(F_dv_get(v_law__9629, "premise"));
                object v_j__9711 = R.number("0");
                while (R.Bool(R.lt(v_j__9711, R.count(F_dv_get(v_law__9629, "effects")))))
                {
                    object v_effect__9782 = R.at(F_dv_get(v_law__9629, "effects"), v_j__9711);
                    _ = F_dv_require((R.Bool(R.eq(F_dv_get(v_effect__9782, "op"), "assign")) ? (object)(true) : (object)(R.eq(F_dv_get(v_effect__9782, "op"), "increment"))), R.cat("Unindexed inference effect: ", F_dv_get(v_effect__9782, "op")));
                    v_refs__9466 = F_dvp_merge_refs(v_refs__9466, new System.Collections.Generic.List<object>(1) { F_dv_get(v_effect__9782, "ref") });
                    v_j__9711 = R.add(v_j__9711, R.number("1"));
                }
                v_j__9711 = R.number("0");
                while (R.Bool(R.lt(v_j__9711, R.count(v_refs__9466))))
                {
                    object v_subject__10095 = F_dv_find(R.at(v_refs__9466, v_j__9711), "subject", "");
                    _ = F_dv_require((R.Bool(R.eq(v_subject__10095, "")) ? (object)(true) : (object)(R.eq(v_subject__10095, F_dv_get(v_mind__9070, "subject")))), "Cross-subject reference access");
                    v_j__9711 = R.add(v_j__9711, R.number("1"));
                }
            }
            _ = R.push(v_result__9360, new System.Collections.Generic.List<object>(2) { F_dv_pair("law", R.at(v_entry__9433, R.number("0"))), F_dv_pair("refs", v_refs__9466) });
            v_i__9380 = R.add(v_i__9380, R.number("1"));
        }
        return R.memo_put(v_adoptions__9096, v_key__9144, R.snapshot(v_result__9360));
    }

    public static object F_dvs_dirty_verify_reason(object v_mind, object v_adoptions, object v_signatures, object v_first, object v_until, object v_time)
    {
        object v_i__10487 = v_first;
        while (R.Bool(R.lt(v_i__10487, v_until)))
        {
            object v_entry__10530 = R.at(v_adoptions, v_i__10487);
            object v_law__10563 = F_dv_get(R.at(v_entry__10530, R.number("1")), "law");
            if (R.Bool((R.Bool(F_dv_get(R.at(v_entry__10530, R.number("1")), "active")) ? (object)(R.eq(F_dv_get(R.at(v_entry__10530, R.number("1")), "mode"), "infer")) : (object)(false))))
            {
                object v_skip__10655 = false;
                if (R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_signatures, R.at(v_entry__10530, R.number("0")), false), false))) ? (object)(F_dvs_reason_monotone_satisfied(v_mind, F_dv_get(v_law__10563, "effects"))) : (object)(false))))
                {
                    v_skip__10655 = true;
                }
                if (R.Bool((R.Bool(R.not(v_skip__10655)) ? (object)(R.eq(F_dv_truth_uncached(v_mind, F_dv_get(v_law__10563, "premise")), "true")) : (object)(false))))
                {
                    object v_signature__10907 = F_dvs_reason_signature(v_mind, R.at(v_entry__10530, R.number("1")));
                    if (R.Bool(R.not(F_dv_same(F_dv_find(v_signatures, R.at(v_entry__10530, R.number("0")), false), v_signature__10907))))
                    {
                        object v_stage__11061 = F_dv_stage(v_mind, F_dv_get(v_law__10563, "effects"), v_time, F_dv_get(R.at(v_entry__10530, R.number("1")), "basis"), F_dv_get(R.at(v_entry__10530, R.number("1")), "confidence"));
                        _ = F_dv_require(R.not(R.eq(F_dv_get(v_stage__11061, "status"), "ready")), R.cat("Dirty inference omitted rule: ", F_dv_get(v_law__10563, "name")));
                    }
                }
            }
            v_i__10487 = R.add(v_i__10487, R.number("1"));
        }
        return true;
    }

    public static object F_dvs_reason_run(object v_actor, object v_events, object v_time, object v_parent)
    {
        object v_mind__11502 = F_dv_get(v_actor, "mind");
        object v_policy__11528 = F_dv_get(v_actor, "policy");
        object v_budget__11558 = F_dv_find(v_policy__11528, "reason_budget", R.number("0"));
        object v_count__11611 = R.number("0");
        object v_changed__11629 = false;
        object v_signatures__11653 = F_dv_find(v_actor, "inference_signatures", new System.Collections.Generic.List<object>(0) { });
        object v_derivations__11717 = F_dv_find(v_actor, "inference_derivations", new System.Collections.Generic.List<object>(0) { });
        object v_dirty__11783 = F_dv_find(v_policy__11528, "opt_dirty", false);
        object v_dependency_index__11835 = new System.Collections.Generic.List<object>(0) { };
        object v_verify__11865 = false;
        if (R.Bool((R.Bool(v_dirty__11783) ? (object)(R.lt(R.number("0"), v_budget__11558)) : (object)(false))))
        {
            v_dependency_index__11835 = F_dvs_dirty_reason_index(v_actor);
            object v_every__11978 = F_dv_find(v_policy__11528, "opt_dirty_verify", R.number("0"));
            if (R.Bool(R.lt(R.number("0"), v_every__11978)))
            {
                object v_key__12063 = new System.Collections.Generic.List<object>(1) { "dirty-reason-verify-tick" };
                object v_previous__12114 = R.memo_get(v_dependency_index__11835, v_key__12063);
                object v_calls__12176 = R.number("1");
                if (R.Bool(R.at(v_previous__12114, R.number("0"))))
                {
                    v_calls__12176 = R.add(R.at(v_previous__12114, R.number("1")), R.number("1"));
                }
                if (R.Bool(R.not(R.lt(v_calls__12176, v_every__11978))))
                {
                    v_verify__11865 = true;
                    v_calls__12176 = R.number("0");
                }
                _ = R.memo_put(v_dependency_index__11835, v_key__12063, v_calls__12176);
            }
        }
        if (R.Bool(R.lt(v_count__11611, v_budget__11558)))
        {
            object v_adoptions__12430 = F_dv_get(v_mind__11502, "adoptions");
            object v_i__12469 = R.number("0");
            if (R.Bool(v_dirty__11783))
            {
                v_i__12469 = R.dirty_next(v_mind__11502, v_dependency_index__11835, R.sub(R.number("0"), R.number("1")));
                if (R.Bool(v_verify__11865))
                {
                    _ = F_dvs_dirty_verify_reason(v_mind__11502, v_adoptions__12430, v_signatures__11653, R.number("0"), v_i__12469, v_time);
                }
            }
            v_changed__11629 = false;
            while (R.Bool(F_dv_and(R.lt(v_i__12469, R.count(v_adoptions__12430)), R.lt(v_count__11611, v_budget__11558))))
            {
                object v_entry__12762 = R.at(v_adoptions__12430, v_i__12469);
                object v_law__12799 = F_dv_get(R.at(v_entry__12762, R.number("1")), "law");
                if (R.Bool(F_dv_and(F_dv_get(R.at(v_entry__12762, R.number("1")), "active"), R.eq(F_dv_get(R.at(v_entry__12762, R.number("1")), "mode"), "infer"))))
                {
                    object v_skip__12906 = false;
                    if (R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_signatures__11653, R.at(v_entry__12762, R.number("0")), false), false))) ? (object)(F_dvs_reason_monotone_satisfied(v_mind__11502, F_dv_get(v_law__12799, "effects"))) : (object)(false))))
                    {
                        v_skip__12906 = true;
                    }
                    if (R.Bool((R.Bool(R.not(v_skip__12906)) ? (object)(R.eq(F_dvs_dirty_truth(v_mind__11502, F_dv_get(v_law__12799, "premise"), v_policy__11528, F_dv_get(v_law__12799, "name")), "true")) : (object)(false))))
                    {
                        object v_sig__13192 = F_dvs_reason_signature(v_mind__11502, R.at(v_entry__12762, R.number("1")));
                        if (R.Bool(R.not(F_dv_same(F_dv_find(v_signatures__11653, R.at(v_entry__12762, R.number("0")), false), v_sig__13192))))
                        {
                            object v_stage__13342 = F_dv_stage(v_mind__11502, F_dv_get(v_law__12799, "effects"), v_time, F_dv_get(R.at(v_entry__12762, R.number("1")), "basis"), F_dv_get(R.at(v_entry__12762, R.number("1")), "confidence"));
                            if (R.Bool(F_dv_and(R.eq(F_dv_get(v_stage__13342, "status"), "ready"), F_dvs_reason_changes(F_dv_get(v_stage__13342, "changes")))))
                            {
                                object v_before__13550 = v_mind__11502;
                                object v_result__13596 = F_dv_infer(v_mind__11502, R.at(v_entry__12762, R.number("0")), R.cat("infer", R.show(R.count(v_events))), v_time, F_dv_get(v_mind__11502, "revision"));
                                v_mind__11502 = F_dv_get(v_result__13596, "world");
                                v_changed__11629 = true;
                                v_count__11611 = R.add(v_count__11611, R.number("1"));
                                v_signatures__11653 = F_dv_with(v_signatures__11653, R.at(v_entry__12762, R.number("0")), v_sig__13192);
                                object v_event__13942 = F_dvs_event(v_events, "inference", F_dv_get(v_actor, "id"), v_time, F_dv_explanation_merge(F_dvs_cognitive_parents(v_actor, F_dv_single(v_parent)), F_dvs_support_parents(v_before__13550, F_dvs_sim_refs(F_dv_get(v_law__12799, "premise")), v_events, v_actor)), new System.Collections.Generic.List<object>(4) { F_dv_pair("mind_before", v_before__13550), F_dv_pair("mind_after", v_mind__11502), F_dv_pair("law", v_law__12799), F_dv_pair("receipt", v_result__13596) });
                                v_actor = F_dv_with(v_actor, "last_cognitive_event", v_event__13942);
                                _ = R.push(v_derivations__11717, F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("event", v_event__13942), F_dv_pair("adoption", R.at(v_entry__12762, R.number("1"))), F_dv_pair("inputs", F_dvs_sim_inputs(v_before__13550, F_dv_get(v_law__12799, "premise"))), F_dv_pair("changes", F_dv_get(v_result__13596, "changes")) }, "valid", true));
                            }
                            else
                            {
                                if (R.Bool(R.eq(F_dv_get(v_stage__13342, "status"), "ready")))
                                {
                                    v_count__11611 = R.add(v_count__11611, R.number("1"));
                                    v_signatures__11653 = F_dv_with(v_signatures__11653, R.at(v_entry__12762, R.number("0")), v_sig__13192);
                                    _ = F_dvs_event(v_events, "inference_alternative_basis", F_dv_get(v_actor, "id"), v_time, F_dvs_cognitive_parents(v_actor, F_dv_single(v_parent)), new System.Collections.Generic.List<object>(4) { F_dv_pair("law", v_law__12799), F_dv_pair("basis", F_dv_get(R.at(v_entry__12762, R.number("1")), "basis")), F_dv_pair("premises", F_dvs_reason_premises(v_mind__11502, F_dv_get(v_law__12799, "premise"))), F_dv_pair("mind_revision", F_dv_get(v_mind__11502, "revision")) });
                                }
                            }
                        }
                    }
                }
                object v_previous__14989 = v_i__12469;
                if (R.Bool(v_dirty__11783))
                {
                    if (R.Bool(R.lt(v_count__11611, v_budget__11558)))
                    {
                        v_i__12469 = R.dirty_next(v_mind__11502, v_dependency_index__11835, v_previous__14989);
                        if (R.Bool(v_verify__11865))
                        {
                            _ = F_dvs_dirty_verify_reason(v_mind__11502, v_adoptions__12430, v_signatures__11653, R.add(v_previous__14989, R.number("1")), v_i__12469, v_time);
                        }
                    }
                }
                else
                {
                    v_i__12469 = R.add(v_i__12469, R.number("1"));
                }
            }
        }
        object v_local_1__15475 = false;
        if (R.Bool(R.lt(v_count__11611, v_budget__11558)))
        {
            v_local_1__15475 = "no-new-effective-conclusion";
        }
        else
        {
            v_local_1__15475 = "budget-exhausted";
        }
        if (R.Bool((R.Bool(v_changed__11629) ? (object)(R.lt(v_count__11611, v_budget__11558)) : (object)(false))))
        {
            v_local_1__15475 = "approximate-single-pass";
        }
        object v_report__15698 = new System.Collections.Generic.List<object>(5) { F_dv_pair("steps", v_count__11611), F_dv_pair("remaining", R.sub(v_budget__11558, v_count__11611)), F_dv_pair("stop", v_local_1__15475), F_dv_pair("mind_revision", F_dv_get(v_mind__11502, "revision")), F_dv_pair("approximation", true) };
        if (R.Bool((R.Bool(R.lt(R.number("0"), v_count__11611)) ? (object)(true) : (object)(R.not(F_dv_same(v_report__15698, F_dv_find(v_actor, "last_reason_report", false)))))))
        {
            v_actor = F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(2) { F_dv_pair("last_cognitive_event", F_dvs_event(v_events, "reason_stop", F_dv_get(v_actor, "id"), v_time, F_dvs_cognitive_parents(v_actor, F_dv_single(v_parent)), v_report__15698)), F_dv_pair("last_reason_report", v_report__15698) });
        }
        return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(4) { F_dv_pair("mind", v_mind__11502), F_dv_pair("inference_derivations", v_derivations__11717), F_dv_pair("inference_signatures", v_signatures__11653), F_dv_pair("policy", F_dv_with(v_policy__11528, "reason_budget", R.sub(v_budget__11558, v_count__11611))) });
    }

    public static object F_dvs_has_active_communication_goal(object v_actor)
    {
        object v_goals__27977 = F_dv_find(F_dv_get(v_actor, "policy"), "communication_goals", new System.Collections.Generic.List<object>(0) { });
        object v_i__28042 = R.number("0");
        while (R.Bool(R.lt(v_i__28042, R.count(v_goals__27977))))
        {
            object v_goal__28091 = R.at(v_goals__27977, v_i__28042);
            if (R.Bool((R.Bool(R.lt(R.number("0"), R.count(F_dv_find(v_goal__28091, "basis", new System.Collections.Generic.List<object>(0) { })))) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(v_goal__28091, "available_when", F_dv_true())), "true")) : (object)(false))))
            {
                return true;
            }
            v_i__28042 = R.add(v_i__28042, R.number("1"));
        }
        return false;
    }

    public static object F_dvs_prepare_cognition(object v_actor, object v_events, object v_time, object v_parent)
    {
        if (R.Bool(F_dv_find(F_dv_find(F_dv_get(v_actor, "mind"), "CognitionFilter", new System.Collections.Generic.List<object>(0) { }), "anterograde", false)))
        {
            return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(3) { F_dv_pair("cognition_stop", "anterograde-no-new-memory"), F_dv_pair("cognition_reused", false), F_dv_pair("reason_reused", false) });
        }
        v_actor = F_dvs_refresh_argument(v_actor, v_events, v_time, v_parent);
        if (R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_actor, "argument", false), false))) ? (object)(F_dv_find(v_actor, "cognition_blocked", false)) : (object)(false))))
        {
            return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(3) { F_dv_pair("cognition_stop", "argument-revocation-budget"), F_dv_pair("cognition_reused", false), F_dv_pair("reason_reused", false) });
        }
        v_actor = F_dvs_reason(v_actor, v_events, v_time, v_parent);
        v_actor = F_dvs_current_appraisals(v_actor, v_events, v_time, v_parent);
        v_actor = F_dvs_update_support_events(v_actor, v_events);
        v_actor = F_dvs_capture_public_topics(v_actor, v_events, v_time, v_parent);
        v_actor = F_dvs_refresh_argument(v_actor, v_events, v_time, v_parent);
        return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(4) { F_dv_pair("cognition_stop", "approximate-single-pass"), F_dv_pair("cognition_mode", "approximate-v1"), F_dv_pair("cognition_reused", false), F_dv_pair("reason_reused", false) });
    }
}
