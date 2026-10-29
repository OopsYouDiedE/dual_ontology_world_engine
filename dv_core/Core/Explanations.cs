using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Cognition / Explanations: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dv_explanation_model_complete(object v_model, object v_status, object v_revision)
    {
        return F_dv_and(R.eq(F_dv_find(v_model, "status", "unknown"), v_status), F_dv_and(F_dv_or(R.eq(F_dv_find(v_model, "source_revision", R.sub(R.number("0"), R.number("1"))), v_revision), F_dv_find(v_model, "validated_by_inputs", false)), F_dv_and(F_dv_find(v_model, "plan_complete", false), F_dv_and(R.not(R.lt(F_dv_find(v_model, "remaining", R.sub(R.number("0"), R.number("1"))), R.number("0"))), F_dv_and(R.not(R.lt(F_dv_find(v_model, "work_remaining", R.sub(R.number("0"), R.number("1"))), R.number("0"))), F_dv_and(R.eq(R.count(F_dv_find(v_model, "failures", new System.Collections.Generic.List<object>(0) { })), R.number("0")), F_dv_and(R.lt(R.number("0"), R.count(F_dv_find(v_model, "predictions", new System.Collections.Generic.List<object>(0) { }))), F_dv_and(R.eq(R.count(F_dv_find(v_model, "predictions", new System.Collections.Generic.List<object>(0) { })), R.count(F_dv_find(v_model, "comparisons", new System.Collections.Generic.List<object>(0) { }))), F_dv_has_key(v_model, "event")))))))));
    }

    public static object F_dv_explanation_observation_sources(object v_mind, object v_comparison, object v_sources)
    {
        object v_observed__119315 = F_dv_get(v_comparison, "observed_cells");
        object v_i__119360 = R.number("0");
        object v_valid__119374 = true;
        object v_ids__119395 = new System.Collections.Generic.List<object>(0) { };
        object v_inputs__119412 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.eq(R.count(v_observed__119315), R.number("0"))))
        {
            v_valid__119374 = false;
        }
        while (R.Bool(R.lt(v_i__119360, R.count(v_observed__119315))))
        {
            object v_item__119528 = R.at(v_observed__119315, v_i__119360);
            object v_ref__119559 = F_dv_get(R.at(v_observed__119315, v_i__119360), "ref");
            object v_cell__119593 = F_dv_read(v_mind, F_dv_get(R.at(v_observed__119315, v_i__119360), "ref"));
            object v_j__119643 = R.number("0");
            object v_found__119661 = false;
            if (R.Bool(F_dv_same(v_cell__119593, F_dv_get(v_item__119528, "cell"))))
            {
                _ = false;
            }
            else
            {
                v_valid__119374 = false;
            }
            while (R.Bool(R.lt(v_j__119643, R.count(v_sources))))
            {
                object v_source__119825 = R.at(v_sources, v_j__119643);
                object v_observation__119861 = F_dv_get(R.at(v_sources, v_j__119643), "observation");
                if (R.Bool(F_dv_and(R.eq(F_dv_get(v_observation__119861, "subject"), F_dv_get(v_mind, "subject")), F_dv_and(F_dv_same_local_ref(F_dv_get(v_mind, "subject"), F_dv_get(v_observation__119861, "ref"), v_ref__119559), F_dv_same(F_dv_get(v_observation__119861, "value"), F_dv_get(v_cell__119593, "value"))))))
                {
                    if (R.Bool(F_dv_has(F_dv_find(v_cell__119593, "claims", new System.Collections.Generic.List<object>(0) { }), v_observation__119861)))
                    {
                        v_found__119661 = true;
                        if (R.Bool(F_dv_has(v_ids__119395, F_dv_get(v_source__119825, "event_id"))))
                        {
                            _ = false;
                        }
                        else
                        {
                            v_ids__119395 = F_dv_append(v_ids__119395, F_dv_get(v_source__119825, "event_id"));
                        }
                    }
                }
                v_j__119643 = R.add(v_j__119643, R.number("1"));
            }
            if (R.Bool(v_found__119661))
            {
                _ = false;
            }
            else
            {
                v_valid__119374 = false;
            }
            _ = R.push(v_inputs__119412, v_item__119528);
            v_i__119360 = R.add(v_i__119360, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("valid", v_valid__119374), F_dv_pair("source_event_ids", v_ids__119395), F_dv_pair("inputs", v_inputs__119412) };
    }

    public static object F_dv_explanation_trace_changes(object v_step)
    {
        if (R.Bool(F_dv_has_key(v_step, "changes")))
        {
            return F_dv_get(v_step, "changes");
        }
        else
        {
            if (R.Bool(F_dv_has_key(v_step, "response")))
            {
                object v_response__120728 = F_dv_get(v_step, "response");
                if (R.Bool(F_dv_has_key(v_response__120728, "bridge")))
                {
                    return F_dv_find(F_dv_get(v_response__120728, "bridge"), "changes", new System.Collections.Generic.List<object>(0) { });
                }
                else
                {
                    return new System.Collections.Generic.List<object>(0) { };
                }
            }
            else
            {
                return new System.Collections.Generic.List<object>(0) { };
            }
        }
    }

    public static object F_dv_explanation_slot(object v_model, object v_slot)
    {
        object v_predictions__120989 = F_dv_get(v_model, "predictions");
        object v_index__121029 = F_dv_get(v_slot, "feature_index");
        object v_valid__121064 = false;
        object v_value__121086 = false;
        object v_confidence__121108 = R.number("0");
        object v_trace_step__121131 = R.sub(R.number("0"), R.number("1"));
        object v_trace__121155 = F_dv_get(v_model, "trace");
        object v_j__121183 = R.number("0");
        if (R.Bool(F_dv_and(R.not(R.lt(v_index__121029, R.number("0"))), R.lt(v_index__121029, R.count(v_predictions__120989)))))
        {
            object v_prediction__121268 = R.at(v_predictions__120989, v_index__121029);
            while (R.Bool(R.lt(v_j__121183, R.count(v_trace__121155))))
            {
                object v_step__121353 = R.at(v_trace__121155, v_j__121183);
                object v_changes__121385 = F_dv_explanation_trace_changes(R.at(v_trace__121155, v_j__121183));
                object v_k__121450 = R.number("0");
                if (R.Bool(R.eq(F_dv_get(v_step__121353, "at"), F_dv_get(v_slot, "trace_at"))))
                {
                    while (R.Bool(R.lt(v_k__121450, R.count(v_changes__121385))))
                    {
                        object v_change__121570 = R.at(v_changes__121385, v_k__121450);
                        if (R.Bool(F_dv_and(F_dv_same(F_dv_get(v_change__121570, "ref"), F_dv_get(v_slot, "ref")), F_dv_and(F_dv_uncontested(F_dv_get(v_change__121570, "after")), F_dv_and(F_dv_get(v_prediction__121268, "covered"), F_dv_find(v_slot, "value_from_prediction", false))))))
                        {
                            v_valid__121064 = true;
                            v_value__121086 = F_dv_get(F_dv_get(v_change__121570, "after"), "value");
                            v_confidence__121108 = F_dv_min(F_dv_get(v_prediction__121268, "confidence"), F_dv_get(F_dv_get(v_change__121570, "after"), "confidence"));
                            v_trace_step__121131 = v_j__121183;
                        }
                        v_k__121450 = R.add(v_k__121450, R.number("1"));
                    }
                }
                v_j__121183 = R.add(v_j__121183, R.number("1"));
            }
        }
        return F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("valid", v_valid__121064), F_dv_pair("value", v_value__121086), F_dv_pair("confidence", v_confidence__121108), F_dv_pair("trace_step", v_trace_step__121131) }, "slot", v_slot);
    }

    public static object F_dv_explanation_model_inputs(object v_mind, object v_model)
    {
        object v_trace__122213 = F_dv_find(v_model, "trace", new System.Collections.Generic.List<object>(0) { });
        object v_result__122257 = new System.Collections.Generic.List<object>(0) { };
        object v_i__122277 = R.number("0");
        while (R.Bool(R.lt(v_i__122277, R.count(v_trace__122213))))
        {
            object v_refs__122328 = F_dv_find(R.at(v_trace__122213, v_i__122277), "inputs", new System.Collections.Generic.List<object>(0) { });
            object v_j__122379 = R.number("0");
            while (R.Bool(R.lt(v_j__122379, R.count(v_refs__122328))))
            {
                object v_ref__122437 = F_dv_get(R.at(v_refs__122328, v_j__122379), "ref");
                v_result__122257 = F_dv_explanation_merge(v_result__122257, F_dv_single(new System.Collections.Generic.List<object>(2) { F_dv_pair("ref", v_ref__122437), F_dv_pair("cell", F_dv_read(v_mind, v_ref__122437)) }));
                v_j__122379 = R.add(v_j__122379, R.number("1"));
            }
            v_i__122277 = R.add(v_i__122277, R.number("1"));
        }
        return v_result__122257;
    }

    public static object F_dv_explanation_model_current(object v_mind, object v_model)
    {
        object v_inputs__122675 = F_dv_find(v_model, "validation_inputs", new System.Collections.Generic.List<object>(0) { });
        object v_valid__122732 = F_dv_same(F_dv_find(v_model, "source_adoptions", false), F_dv_get(v_mind, "adoptions"));
        object v_i__122815 = R.number("0");
        while (R.Bool(R.lt(v_i__122815, R.count(v_inputs__122675))))
        {
            object v_input__122867 = R.at(v_inputs__122675, v_i__122815);
            if (R.Bool(F_dv_same(F_dv_without(F_dv_get(v_input__122867, "cell"), "time"), F_dv_without(F_dv_read(v_mind, F_dv_get(v_input__122867, "ref")), "time"))))
            {
                _ = false;
            }
            else
            {
                v_valid__122732 = false;
            }
            v_i__122815 = R.add(v_i__122815, R.number("1"));
        }
        return v_valid__122732;
    }

    public static object F_dv_accept_explanation(object v_mind, object v_model, object v_competitors, object v_slots, object v_sources, object v_policy, object v_budget, object v_time, object v_id)
    {
        object v_valid__123181 = true;
        object v_reason__123202 = "accepted-estimated";
        object v_comparisons__123240 = F_dv_find(v_model, "comparisons", new System.Collections.Generic.List<object>(0) { });
        object v_i__123296 = R.number("0");
        object v_source_ids__123310 = new System.Collections.Generic.List<object>(0) { };
        object v_inputs__123334 = new System.Collections.Generic.List<object>(0) { };
        object v_competitor_ids__123354 = new System.Collections.Generic.List<object>(0) { };
        object v_claims__123382 = new System.Collections.Generic.List<object>(0) { };
        object v_effects__123402 = new System.Collections.Generic.List<object>(0) { };
        object v_remaining__123423 = v_budget;
        object v_confidence__123450 = F_dv_find(v_policy, "confidence_cap", R.number("0"));
        _ = F_dv_domain(v_mind, "mind");
        if (R.Bool(F_dv_and(F_dv_find(v_policy, "active", false), F_dv_and(R.lt(R.number("0"), v_confidence__123450), F_dv_and(R.lt(v_confidence__123450, R.number("1")), R.lt(R.number("0"), R.count(F_dv_find(v_policy, "basis", new System.Collections.Generic.List<object>(0) { }))))))))
        {
            _ = false;
        }
        else
        {
            v_valid__123181 = false;
            v_reason__123202 = "abduction-strategy-not-adopted";
        }
        object v_local_1__123779 = false;
        if (R.Bool(F_dv_find(v_model, "validated_by_inputs", false)))
        {
            v_local_1__123779 = F_dv_explanation_model_current(v_mind, v_model);
        }
        else
        {
            v_local_1__123779 = true;
        }
        if (R.Bool(F_dv_and(F_dv_explanation_model_complete(v_model, "explained", F_dv_get(v_mind, "revision")), v_local_1__123779)))
        {
            _ = false;
        }
        else
        {
            v_valid__123181 = false;
            v_reason__123202 = "model-incomplete-or-stale";
        }
        while (R.Bool(R.lt(v_i__123296, R.count(v_comparisons__123240))))
        {
            object v_comparison__124173 = R.at(v_comparisons__123240, v_i__123296);
            object v_observed__124213 = F_dv_explanation_observation_sources(v_mind, R.at(v_comparisons__123240, v_i__123296), v_sources);
            if (R.Bool(F_dv_and(R.eq(F_dv_get(v_comparison__124173, "result"), "match"), F_dv_get(v_observed__124213, "valid"))))
            {
                _ = false;
            }
            else
            {
                v_valid__123181 = false;
                v_reason__123202 = "unobserved-or-unmatched-feature";
            }
            v_confidence__123450 = F_dv_min(v_confidence__123450, F_dv_get(v_comparison__124173, "confidence"));
            v_source_ids__123310 = F_dv_explanation_merge(v_source_ids__123310, F_dv_get(v_observed__124213, "source_event_ids"));
            v_inputs__123334 = F_dv_explanation_merge(v_inputs__123334, F_dv_get(v_observed__124213, "inputs"));
            v_i__123296 = R.add(v_i__123296, R.number("1"));
        }
        v_inputs__123334 = F_dv_explanation_merge(v_inputs__123334, F_dv_explanation_model_inputs(v_mind, v_model));
        v_i__123296 = R.number("0");
        while (R.Bool(R.lt(v_i__123296, R.count(v_competitors))))
        {
            object v_other__124842 = R.at(v_competitors, v_i__123296);
            object v_j__124877 = R.number("0");
            object v_other_comparisons__124895 = F_dv_find(R.at(v_competitors, v_i__123296), "comparisons", new System.Collections.Generic.List<object>(0) { });
            object v_contradiction__124970 = false;
            object v_local_2__125004 = false;
            if (R.Bool(F_dv_find(v_other__124842, "validated_by_inputs", false)))
            {
                v_local_2__125004 = F_dv_explanation_model_current(v_mind, v_other__124842);
            }
            else
            {
                v_local_2__125004 = true;
            }
            if (R.Bool(F_dv_and(F_dv_explanation_model_complete(v_other__124842, "inconsistent", F_dv_get(v_mind, "revision")), v_local_2__125004)))
            {
                _ = false;
            }
            else
            {
                v_valid__123181 = false;
                v_reason__123202 = "unresolved-competitor";
            }
            while (R.Bool(R.lt(v_j__124877, R.count(v_other_comparisons__124895))))
            {
                object v_comparison__125447 = R.at(v_other_comparisons__124895, v_j__124877);
                object v_observed__125497 = F_dv_explanation_observation_sources(v_mind, R.at(v_other_comparisons__124895, v_j__124877), v_sources);
                if (R.Bool(F_dv_and(R.not(R.eq(F_dv_get(v_comparison__125447, "result"), "unknown")), F_dv_get(v_observed__125497, "valid"))))
                {
                    _ = false;
                }
                else
                {
                    v_valid__123181 = false;
                    v_reason__123202 = "competitor-feature-unresolved";
                }
                if (R.Bool(R.eq(F_dv_get(v_comparison__125447, "result"), "contradiction")))
                {
                    v_contradiction__124970 = true;
                }
                v_source_ids__123310 = F_dv_explanation_merge(v_source_ids__123310, F_dv_get(v_observed__125497, "source_event_ids"));
                v_inputs__123334 = F_dv_explanation_merge(v_inputs__123334, F_dv_get(v_observed__125497, "inputs"));
                v_j__124877 = R.add(v_j__124877, R.number("1"));
            }
            v_inputs__123334 = F_dv_explanation_merge(v_inputs__123334, F_dv_explanation_model_inputs(v_mind, v_other__124842));
            if (R.Bool(v_contradiction__124970))
            {
                _ = false;
            }
            else
            {
                v_valid__123181 = false;
                v_reason__123202 = "competitor-not-refuted";
            }
            if (R.Bool(F_dv_has_key(v_other__124842, "event")))
            {
                v_competitor_ids__123354 = F_dv_append(v_competitor_ids__123354, F_dv_get(v_other__124842, "event"));
            }
            v_i__123296 = R.add(v_i__123296, R.number("1"));
        }
        if (R.Bool(R.eq(R.count(v_slots), R.number("0"))))
        {
            v_valid__123181 = false;
            v_reason__123202 = "no-public-claim-slots";
        }
        v_i__123296 = R.number("0");
        while (R.Bool(F_dv_and(v_valid__123181, R.lt(v_i__123296, R.count(v_slots)))))
        {
            if (R.Bool(R.lt(v_remaining__123423, R.number("1"))))
            {
                v_valid__123181 = false;
                v_reason__123202 = "argument-reason-budget";
            }
            else
            {
                object v_slot__126681 = R.at(v_slots, v_i__123296);
                object v_resolved__126713 = F_dv_explanation_slot(v_model, R.at(v_slots, v_i__123296));
                v_remaining__123423 = R.sub(v_remaining__123423, R.number("1"));
                if (R.Bool(F_dv_get(v_resolved__126713, "valid")))
                {
                    object v_ref__126852 = F_dv_get(v_slot__126681, "ref");
                    object v_existing__126887 = F_dv_read(v_mind, F_dv_get(v_slot__126681, "ref"));
                    if (R.Bool(F_dv_has_key(F_dv_get(v_mind, "objects"), F_dv_get(v_ref__126852, "object"))))
                    {
                        _ = false;
                    }
                    else
                    {
                        v_valid__123181 = false;
                        v_reason__123202 = "unknown-local-claim-object";
                    }
                    if (R.Bool(F_dv_and(F_dv_uncontested(v_existing__126887), R.not(F_dv_same(F_dv_get(v_existing__126887, "value"), F_dv_get(v_resolved__126713, "value"))))))
                    {
                        v_valid__123181 = false;
                        v_reason__123202 = "claim-conflicts-with-current-evidence";
                    }
                    v_confidence__123450 = F_dv_min(v_confidence__123450, F_dv_get(v_resolved__126713, "confidence"));
                    _ = R.push(v_claims__123382, F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("slot_id", F_dv_get(v_slot__126681, "slot_id")), F_dv_pair("ref", v_ref__126852), F_dv_pair("value", F_dv_get(v_resolved__126713, "value")), F_dv_pair("trace_step", F_dv_get(v_resolved__126713, "trace_step")) }, "feature_index", F_dv_get(v_slot__126681, "feature_index")));
                    if (R.Bool(F_dv_uncontested(v_existing__126887)))
                    {
                        _ = false;
                    }
                    else
                    {
                        _ = R.push(v_effects__123402, F_dv_assign(v_ref__126852, F_dv_get(v_resolved__126713, "value")));
                    }
                }
                else
                {
                    v_valid__123181 = false;
                    v_reason__123202 = "claim-not-covered-by-actual-predictive-trace";
                }
                v_i__123296 = R.add(v_i__123296, R.number("1"));
            }
        }
        if (R.Bool(R.lt(R.number("0"), v_confidence__123450)))
        {
            _ = false;
        }
        else
        {
            v_valid__123181 = false;
            v_reason__123202 = "unsupported-zero-confidence";
        }
        object v_basis__128000 = F_dv_explanation_merge(F_dv_find(v_policy, "basis", new System.Collections.Generic.List<object>(0) { }), F_dv_explanation_merge(v_source_ids__123310, F_dv_append(v_competitor_ids__123354, F_dv_find(v_model, "event", ""))));
        object v_world__128157 = v_mind;
        object v_changes__128178 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(v_valid__123181))
        {
            object v_stage__128217 = F_dv_stage(v_mind, v_effects__123402, v_time, F_dv_append(v_basis__128000, v_id), v_confidence__123450);
            object v_j__128301 = R.number("0");
            if (R.Bool(R.eq(F_dv_get(v_stage__128217, "status"), "ready")))
            {
                v_world__128157 = F_dv_get(v_stage__128217, "world");
                v_changes__128178 = F_dv_get(v_stage__128217, "changes");
                while (R.Bool(R.lt(v_j__128301, R.count(v_changes__128178))))
                {
                    object v_ref__128476 = F_dv_get(R.at(v_changes__128178, v_j__128301), "ref");
                    v_world__128157 = F_dv_store(v_world__128157, v_ref__128476, F_dv_with_many(F_dv_read(v_world__128157, v_ref__128476), new System.Collections.Generic.List<object>(2) { F_dv_pair("source", "abduction"), F_dv_pair("estimated", true) }));
                    v_j__128301 = R.add(v_j__128301, R.number("1"));
                }
                if (R.Bool(R.lt(R.number("0"), R.count(v_changes__128178))))
                {
                    v_world__128157 = F_dv_with(v_world__128157, "revision", R.add(F_dv_get(v_mind, "revision"), R.number("1")));
                }
            }
            else
            {
                v_valid__123181 = false;
                v_reason__123202 = F_dv_get(v_stage__128217, "status");
            }
        }
        object v_argument__128843 = F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("id", v_id), F_dv_pair("estimated", true), F_dv_pair("confidence", v_confidence__123450), F_dv_pair("claims", v_claims__123382) }, new System.Collections.Generic.List<object>(8) { F_dv_pair("model_event", F_dv_find(v_model, "event", "")), F_dv_pair("source_event_ids", v_source_ids__123310), F_dv_pair("competitor_events", v_competitor_ids__123354), F_dv_pair("source_revision", F_dv_get(v_mind, "revision")), F_dv_pair("mind_revision_after", F_dv_get(v_world__128157, "revision")), F_dv_pair("inputs", v_inputs__123334), F_dv_pair("changes", v_changes__128178), F_dv_pair("basis", v_basis__128000) });
        if (R.Bool(v_valid__123181))
        {
            v_world__128157 = F_dv_with(v_world__128157, "history", F_dv_append(F_dv_get(v_world__128157, "history"), new System.Collections.Generic.List<object>(3) { F_dv_pair("kind", "estimated-model-argument"), F_dv_pair("argument", v_argument__128843), F_dv_pair("time", v_time) }));
        }
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("accepted", v_valid__123181), F_dv_pair("reason", v_reason__123202), F_dv_pair("world", v_world__128157), F_dv_pair("argument", v_argument__128843) }, new System.Collections.Generic.List<object>(2) { F_dv_pair("remaining", v_remaining__123423), F_dv_pair("expanded", R.sub(v_budget, v_remaining__123423)) });
    }

    public static object F_dv_explanation_merge(object v_a, object v_b)
    {
        object v_result__129541 = v_a;
        object v_i__129560 = R.number("0");
        while (R.Bool(R.lt(v_i__129560, R.count(v_b))))
        {
            if (R.Bool(F_dv_has(v_result__129541, R.at(v_b, v_i__129560))))
            {
                _ = false;
            }
            else
            {
                v_result__129541 = F_dv_append(v_result__129541, R.at(v_b, v_i__129560));
            }
            v_i__129560 = R.add(v_i__129560, R.number("1"));
        }
        return v_result__129541;
    }
}
