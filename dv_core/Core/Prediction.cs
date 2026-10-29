using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Planning / Prediction: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_sim_refs(object v_proposition)
    {
        object v_op__162271 = F_dv_get(v_proposition, "op");
        if (R.Bool(R.eq(v_op__162271, "held_for")))
        {
            return new System.Collections.Generic.List<object>(2) { F_dv_get(v_proposition, "ref"), F_dv_get(v_proposition, "clock") };
        }
        if (R.Bool(F_dv_or(R.eq(v_op__162271, "equals"), R.eq(v_op__162271, "at_least"))))
        {
            return F_dv_single(F_dv_get(v_proposition, "ref"));
        }
        else
        {
            if (R.Bool(R.eq(v_op__162271, "not")))
            {
                return F_dvs_sim_refs(F_dv_get(v_proposition, "item"));
            }
            else
            {
                if (R.Bool(R.eq(v_op__162271, "all")))
                {
                    object v_items__162612 = F_dv_get(v_proposition, "items");
                    object v_refs__162658 = new System.Collections.Generic.List<object>(0) { };
                    object v_i__162688 = R.number("0");
                    while (R.Bool(R.lt(v_i__162688, R.count(v_items__162612))))
                    {
                        object v_nested__162763 = F_dvs_sim_refs(R.at(v_items__162612, v_i__162688));
                        object v_j__162819 = R.number("0");
                        while (R.Bool(R.lt(v_j__162819, R.count(v_nested__162763))))
                        {
                            if (R.Bool(F_dv_has(v_refs__162658, R.at(v_nested__162763, v_j__162819))))
                            {
                                _ = false;
                            }
                            else
                            {
                                _ = R.push(v_refs__162658, R.at(v_nested__162763, v_j__162819));
                            }
                            v_j__162819 = R.add(v_j__162819, R.number("1"));
                        }
                        v_i__162688 = R.add(v_i__162688, R.number("1"));
                    }
                    return v_refs__162658;
                }
                else
                {
                    return new System.Collections.Generic.List<object>(0) { };
                }
            }
        }
    }

    public static object F_dvs_sim_plan(object v_hypothesis)
    {
        object v_key__163222 = new System.Collections.Generic.List<object>(3) { "compiled-model-plan", F_dv_has_key(v_hypothesis, "timeline"), F_dv_find(v_hypothesis, "predict_laws", new System.Collections.Generic.List<object>(0) { }) };
        object v_timeline__163337 = F_dv_find(v_hypothesis, "timeline", new System.Collections.Generic.List<object>(0) { });
        object v_cached__163392 = F_dvs_declaration_cache_get(v_timeline__163337, v_key__163222);
        if (R.Bool(R.at(v_cached__163392, R.number("0"))))
        {
            return R.at(v_cached__163392, R.number("1"));
        }
        return F_dvs_declaration_cache_put(v_timeline__163337, v_key__163222, F_dvs_sim_plan_build(v_hypothesis));
    }

    public static object F_dvs_sim_plan_build(object v_hypothesis)
    {
        object v_timeline__163613 = F_dv_find(v_hypothesis, "timeline", new System.Collections.Generic.List<object>(0) { });
        object v_plan__163668 = new System.Collections.Generic.List<object>(0) { };
        object v_i__163686 = R.number("0");
        if (R.Bool(F_dv_has_key(v_hypothesis, "timeline")))
        {
            while (R.Bool(R.lt(v_i__163686, R.count(v_timeline__163613))))
            {
                object v_stage__163791 = R.at(v_timeline__163613, v_i__163686);
                object v_j__163827 = R.number("0");
                if (R.Bool(F_dv_has_key(v_stage__163791, "dispatch")))
                {
                    _ = R.push(v_plan__163668, new System.Collections.Generic.List<object>(5) { F_dv_pair("law", ""), F_dv_pair("at", F_dv_get(v_stage__163791, "at")), F_dv_pair("optional", true), F_dv_pair("stage", v_i__163686), F_dv_pair("dispatch", F_dv_get(v_stage__163791, "dispatch")) });
                }
                else
                {
                    if (R.Bool(F_dv_has_key(v_stage__163791, "response")))
                    {
                        _ = R.push(v_plan__163668, F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("law", ""), F_dv_pair("at", F_dv_get(v_stage__163791, "at")), F_dv_pair("optional", F_dv_find(v_stage__163791, "optional", false)), F_dv_pair("stage", v_i__163686) }, new System.Collections.Generic.List<object>(2) { F_dv_pair("response", F_dv_get(v_stage__163791, "response")), F_dv_pair("available_when", F_dv_find(v_stage__163791, "available_when", F_dv_true())) }));
                    }
                    else
                    {
                        object v_local_1__164339 = false;
                        if (R.Bool(F_dv_has_key(v_stage__163791, "laws")))
                        {
                            v_local_1__164339 = F_dv_get(v_stage__163791, "laws");
                        }
                        else
                        {
                            v_local_1__164339 = F_dv_single(F_dv_get(v_stage__163791, "law"));
                        }
                        object v_laws__164555 = v_local_1__164339;
                        _ = F_dv_require(R.lt(R.number("0"), R.count(v_laws__164555)), "Empty model event stage");
                        while (R.Bool(R.lt(v_j__163827, R.count(v_laws__164555))))
                        {
                            _ = R.push(v_plan__163668, new System.Collections.Generic.List<object>(4) { F_dv_pair("law", R.at(v_laws__164555, v_j__163827)), F_dv_pair("at", F_dv_get(v_stage__163791, "at")), F_dv_pair("optional", F_dv_find(v_stage__163791, "optional", false)), F_dv_pair("stage", v_i__163686) });
                            v_j__163827 = R.add(v_j__163827, R.number("1"));
                        }
                    }
                }
                v_i__163686 = R.add(v_i__163686, R.number("1"));
            }
        }
        else
        {
            object v_laws__164911 = F_dv_find(v_hypothesis, "predict_laws", new System.Collections.Generic.List<object>(0) { });
            while (R.Bool(R.lt(v_i__163686, R.count(v_laws__164911))))
            {
                _ = R.push(v_plan__163668, new System.Collections.Generic.List<object>(4) { F_dv_pair("law", R.at(v_laws__164911, v_i__163686)), F_dv_pair("at", R.number("0")), F_dv_pair("optional", false), F_dv_pair("stage", v_i__163686) });
                v_i__163686 = R.add(v_i__163686, R.number("1"));
            }
        }
        return v_plan__163668;
    }

    public static object F_dvs_sim_withhold(object v_world, object v_effects)
    {
        object v_copy__165164 = v_world;
        object v_refs__165185 = new System.Collections.Generic.List<object>(0) { };
        object v_i__165203 = R.number("0");
        while (R.Bool(R.lt(v_i__165203, R.count(v_effects))))
        {
            object v_ref__165256 = F_dv_get(R.at(v_effects, v_i__165203), "ref");
            if (R.Bool(F_dv_has_key(F_dv_get(v_copy__165164, "objects"), F_dv_get(v_ref__165256, "object"))))
            {
                v_copy__165164 = F_dv_store(v_copy__165164, v_ref__165256, F_dv_unknown());
                _ = R.push(v_refs__165185, v_ref__165256);
            }
            v_i__165203 = R.add(v_i__165203, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("world", v_copy__165164), F_dv_pair("refs", v_refs__165185) };
    }

    public static object F_dvs_sim_feature(object v_feature)
    {
        if (R.Bool(F_dv_has_key(v_feature, "proposition")))
        {
            return F_dv_get(v_feature, "proposition");
        }
        else
        {
            return v_feature;
        }
    }

    public static object F_dvs_sim_mask_plan(object v_plan)
    {
        object v_result__165649 = new System.Collections.Generic.List<object>(0) { };
        object v_i__165669 = R.number("0");
        while (R.Bool(R.lt(v_i__165669, R.count(v_plan))))
        {
            object v_step__165719 = R.at(v_plan, v_i__165669);
            if (R.Bool(F_dv_has_key(v_step__165719, "dispatch")))
            {
                object v_j__165791 = R.number("0");
                while (R.Bool(R.lt(v_j__165791, R.count(F_dv_get(F_dv_get(v_step__165719, "dispatch"), "laws")))))
                {
                    _ = R.push(v_result__165649, new System.Collections.Generic.List<object>(1) { F_dv_pair("law", R.at(F_dv_get(F_dv_get(v_step__165719, "dispatch"), "laws"), v_j__165791)) });
                    v_j__165791 = R.add(v_j__165791, R.number("1"));
                }
            }
            else
            {
                _ = R.push(v_result__165649, v_step__165719);
            }
            v_i__165669 = R.add(v_i__165669, R.number("1"));
        }
        return v_result__165649;
    }

    public static object F_dvs_sim_mask(object v_mind, object v_source_plan, object v_start)
    {
        object v_plan__166077 = F_dvs_sim_mask_plan(v_source_plan);
        object v_copy__166123 = F_dv_with_many(v_mind, new System.Collections.Generic.List<object>(3) { F_dv_pair("time", v_start), F_dv_pair("seen", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("history", new System.Collections.Generic.List<object>(0) { }) });
        object v_i__166201 = R.number("0");
        while (R.Bool(R.lt(v_i__166201, R.count(v_plan__166077))))
        {
            object v_name__166251 = F_dv_get(R.at(v_plan__166077, v_i__166201), "law");
            object v_adoptions__166282 = F_dv_get(v_mind, "adoptions");
            if (R.Bool(F_dv_has_key(v_adoptions__166282, v_name__166251)))
            {
                object v_adoption__166365 = F_dv_get(v_adoptions__166282, v_name__166251);
                object v_j__166416 = R.number("0");
                object v_effects__166438 = F_dv_get(F_dv_get(F_dv_get(v_adoptions__166282, v_name__166251), "law"), "effects");
                if (R.Bool(F_dv_and(F_dv_get(v_adoption__166365, "active"), R.eq(F_dv_get(v_adoption__166365, "mode"), "predict"))))
                {
                    while (R.Bool(R.lt(v_j__166416, R.count(v_effects__166438))))
                    {
                        object v_ref__166625 = F_dv_get(R.at(v_effects__166438, v_j__166416), "ref");
                        if (R.Bool(F_dv_has_key(F_dv_get(v_copy__166123, "objects"), F_dv_get(v_ref__166625, "object"))))
                        {
                            v_copy__166123 = F_dv_store(v_copy__166123, v_ref__166625, F_dv_unknown());
                        }
                        v_j__166416 = R.add(v_j__166416, R.number("1"));
                    }
                }
            }
            v_i__166201 = R.add(v_i__166201, R.number("1"));
        }
        return v_copy__166123;
    }

    public static object F_dvs_sim_inputs(object v_world, object v_proposition)
    {
        object v_refs__166888 = F_dvs_sim_refs(v_proposition);
        object v_i__166929 = R.number("0");
        object v_inputs__166943 = new System.Collections.Generic.List<object>(0) { };
        while (R.Bool(R.lt(v_i__166929, R.count(v_refs__166888))))
        {
            _ = R.push(v_inputs__166943, new System.Collections.Generic.List<object>(2) { F_dv_pair("ref", R.at(v_refs__166888, v_i__166929)), F_dv_pair("cell", F_dv_read(v_world, R.at(v_refs__166888, v_i__166929))) });
            v_i__166929 = R.add(v_i__166929, R.number("1"));
        }
        return v_inputs__166943;
    }

    public static object F_dvs_sim_snapshot(object v_snapshots, object v_final, object v_covered, object v_feature)
    {
        if (R.Bool(F_dv_has_key(v_feature, "at")))
        {
            object v_i__167204 = R.number("0");
            object v_selected__167222 = new System.Collections.Generic.List<object>(3) { F_dv_pair("world", v_final), F_dv_pair("coverage", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("step", R.sub(R.number("0"), R.number("1"))) };
            object v_found__167290 = false;
            object v_at__167316 = F_dv_get(v_feature, "at");
            _ = F_dv_require(R.not(R.lt(v_at__167316, R.number("0"))), "Negative model feature time");
            while (R.Bool(R.lt(v_i__167204, R.count(v_snapshots))))
            {
                object v_snapshot__167455 = R.at(v_snapshots, v_i__167204);
                if (R.Bool(R.eq(v_at__167316, F_dv_get(v_snapshot__167455, "at"))))
                {
                    v_selected__167222 = v_snapshot__167455;
                    v_found__167290 = true;
                }
                v_i__167204 = R.add(v_i__167204, R.number("1"));
            }
            if (R.Bool(v_found__167290))
            {
                return v_selected__167222;
            }
            else
            {
                return new System.Collections.Generic.List<object>(3) { F_dv_pair("world", v_final), F_dv_pair("coverage", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("step", R.sub(R.number("0"), R.number("1"))) };
            }
        }
        else
        {
            return new System.Collections.Generic.List<object>(3) { F_dv_pair("world", v_final), F_dv_pair("coverage", v_covered), F_dv_pair("step", R.sub(R.count(v_snapshots), R.number("1"))) };
        }
    }

    public static object F_dvs_sim_prediction(object v_feature, object v_snapshot, object v_start)
    {
        object v_proposition__167884 = F_dvs_sim_feature(v_feature);
        object v_refs__167931 = F_dvs_sim_refs(F_dvs_sim_feature(v_feature));
        object v_world__167985 = F_dv_get(v_snapshot, "world");
        object v_coverage__168016 = F_dv_get(v_snapshot, "coverage");
        object v_i__168053 = R.number("0");
        object v_cells__168067 = new System.Collections.Generic.List<object>(0) { };
        object v_valid__168086 = true;
        object v_confidence__168107 = R.number("1");
        if (R.Bool(R.eq(R.count(v_refs__167931), R.number("0"))))
        {
            v_valid__168086 = false;
        }
        while (R.Bool(R.lt(v_i__168053, R.count(v_refs__167931))))
        {
            object v_ref__168218 = R.at(v_refs__167931, v_i__168053);
            object v_cell__168244 = F_dv_read(v_world__167985, R.at(v_refs__167931, v_i__168053));
            _ = R.push(v_cells__168067, new System.Collections.Generic.List<object>(2) { F_dv_pair("ref", v_ref__168218), F_dv_pair("cell", v_cell__168244) });
            if (R.Bool(F_dv_and(F_dv_has(v_coverage__168016, v_ref__168218), F_dv_uncontested(v_cell__168244))))
            {
                v_confidence__168107 = F_dv_min(v_confidence__168107, F_dv_get(v_cell__168244, "confidence"));
            }
            else
            {
                v_valid__168086 = false;
            }
            v_i__168053 = R.add(v_i__168053, R.number("1"));
        }
        object v_local_1__168519 = false;
        if (R.Bool(v_valid__168086))
        {
            v_local_1__168519 = F_dv_truth(v_world__167985, v_proposition__167884);
        }
        else
        {
            v_local_1__168519 = "unknown";
        }
        object v_local_2__168642 = false;
        if (R.Bool(v_valid__168086))
        {
            v_local_2__168642 = v_confidence__168107;
        }
        else
        {
            v_local_2__168642 = R.number("0");
        }
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("feature", v_feature), F_dv_pair("proposition", v_proposition__167884), F_dv_pair("prediction", v_local_1__168519), F_dv_pair("cells", v_cells__168067) }, new System.Collections.Generic.List<object>(3) { F_dv_pair("covered", v_valid__168086), F_dv_pair("confidence", v_local_2__168642), F_dv_pair("source_step", F_dv_find(v_snapshot, "step", R.sub(R.number("0"), R.number("1")))) });
    }

    public static object F_dvs_sim_compare(object v_mind, object v_prediction)
    {
        object v_feature__168980 = F_dv_get(v_prediction, "feature");
        object v_proposition__169017 = F_dv_get(v_prediction, "proposition");
        object v_refs__169062 = F_dvs_sim_refs(F_dv_find(F_dv_get(v_prediction, "feature"), "observation", F_dv_get(v_prediction, "proposition")));
        object v_observation__169158 = F_dv_truth(v_mind, F_dv_find(F_dv_get(v_prediction, "feature"), "observation", F_dv_get(v_prediction, "proposition")));
        object v_certainty__169263 = F_dv_get(v_prediction, "confidence");
        object v_available__169305 = true;
        object v_temporal_mismatch__169330 = false;
        object v_i__169364 = R.number("0");
        object v_observed__169378 = new System.Collections.Generic.List<object>(0) { };
        while (R.Bool(R.lt(v_i__169364, R.count(v_refs__169062))))
        {
            object v_cell__169436 = F_dv_read(v_mind, R.at(v_refs__169062, v_i__169364));
            _ = R.push(v_observed__169378, new System.Collections.Generic.List<object>(2) { F_dv_pair("ref", R.at(v_refs__169062, v_i__169364)), F_dv_pair("cell", v_cell__169436) });
            if (R.Bool(F_dv_uncontested(v_cell__169436)))
            {
                v_certainty__169263 = F_dv_min(v_certainty__169263, F_dv_get(v_cell__169436, "confidence"));
            }
            else
            {
                v_available__169305 = false;
            }
            v_i__169364 = R.add(v_i__169364, R.number("1"));
        }
        if (R.Bool(F_dv_has_key(v_feature__168980, "occurrence_ref")))
        {
            object v_occurrence__169738 = F_dv_read(v_mind, F_dv_get(v_feature__168980, "occurrence_ref"));
            _ = R.push(v_observed__169378, new System.Collections.Generic.List<object>(2) { F_dv_pair("ref", F_dv_get(v_feature__168980, "occurrence_ref")), F_dv_pair("cell", v_occurrence__169738) });
            if (R.Bool(F_dv_uncontested(v_occurrence__169738)))
            {
                v_certainty__169263 = F_dv_min(v_certainty__169263, F_dv_get(v_occurrence__169738, "confidence"));
                if (R.Bool(F_dv_same(F_dv_get(v_occurrence__169738, "value"), F_dv_get(v_feature__168980, "occurrence_time"))))
                {
                    _ = false;
                }
                else
                {
                    v_temporal_mismatch__169330 = true;
                }
            }
            else
            {
                v_available__169305 = false;
            }
        }
        object v_resolved__170172 = F_dv_and(v_available__169305, F_dv_and(F_dv_get(v_prediction, "covered"), F_dv_and(R.lt(R.number("0"), v_certainty__169263), F_dv_and(R.not(R.eq(v_observation__169158, "unknown")), R.not(R.eq(F_dv_get(v_prediction, "prediction"), "unknown"))))));
        object v_local_1__170348 = false;
        if (R.Bool(v_available__169305))
        {
            v_local_1__170348 = v_observation__169158;
        }
        else
        {
            v_local_1__170348 = "unknown";
        }
        object v_local_2__170458 = false;
        if (R.Bool(v_resolved__170172))
        {
            v_local_2__170458 = v_certainty__169263;
        }
        else
        {
            v_local_2__170458 = R.number("0");
        }
        object v_local_3__170557 = false;
        if (R.Bool(v_resolved__170172))
        {
            if (R.Bool(F_dv_and(R.not(v_temporal_mismatch__169330), R.eq(v_observation__169158, F_dv_get(v_prediction, "prediction")))))
            {
                v_local_3__170557 = "match";
            }
            else
            {
                v_local_3__170557 = "contradiction";
            }
        }
        else
        {
            v_local_3__170557 = "unknown";
        }
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("feature", v_feature__168980), F_dv_pair("prediction", F_dv_get(v_prediction, "prediction")), F_dv_pair("observation", v_local_1__170348), F_dv_pair("covered", F_dv_get(v_prediction, "covered")) }, new System.Collections.Generic.List<object>(3) { F_dv_pair("confidence", v_local_2__170458), F_dv_pair("observed_cells", v_observed__169378), F_dv_pair("result", v_local_3__170557) });
    }

    public static object F_dvs_sim_comparisons(object v_mind, object v_predictions, object v_budget)
    {
        object v_remaining__171064 = v_budget;
        object v_expanded__171091 = R.number("0");
        object v_i__171112 = R.number("0");
        object v_fit__171126 = R.number("0");
        object v_comparisons__171142 = new System.Collections.Generic.List<object>(0) { };
        object v_contradictions__171167 = new System.Collections.Generic.List<object>(0) { };
        object v_complete__171195 = true;
        object v_probes__171219 = new System.Collections.Generic.List<object>(0) { };
        _ = F_dv_require(R.not(R.lt(v_budget, R.number("0"))), "Negative simulation comparison budget");
        while (R.Bool(F_dv_and(R.lt(v_i__171112, R.count(v_predictions)), R.not(R.lt(v_remaining__171064, R.number("1"))))))
        {
            object v_prediction__171389 = R.at(v_predictions, v_i__171112);
            object v_comparison__171429 = F_dvs_sim_compare(v_mind, R.at(v_predictions, v_i__171112));
            object v_j__171492 = R.number("0");
            object v_refs__171510 = F_dvs_sim_refs(F_dv_get(R.at(v_predictions, v_i__171112), "proposition"));
            v_remaining__171064 = R.sub(v_remaining__171064, R.number("1"));
            v_expanded__171091 = R.add(v_expanded__171091, R.number("1"));
            _ = R.push(v_comparisons__171142, v_comparison__171429);
            if (R.Bool(R.eq(F_dv_get(v_comparison__171429, "result"), "match")))
            {
                v_fit__171126 = R.add(v_fit__171126, F_dv_get(v_comparison__171429, "confidence"));
            }
            else
            {
                if (R.Bool(R.eq(F_dv_get(v_comparison__171429, "result"), "contradiction")))
                {
                    _ = R.push(v_contradictions__171167, v_comparison__171429);
                }
                else
                {
                    v_complete__171195 = false;
                    while (R.Bool(R.lt(v_j__171492, R.count(v_refs__171510))))
                    {
                        if (R.Bool(F_dv_has(v_probes__171219, R.at(v_refs__171510, v_j__171492))))
                        {
                            _ = false;
                        }
                        else
                        {
                            _ = R.push(v_probes__171219, R.at(v_refs__171510, v_j__171492));
                        }
                        v_j__171492 = R.add(v_j__171492, R.number("1"));
                    }
                }
            }
            v_i__171112 = R.add(v_i__171112, R.number("1"));
        }
        if (R.Bool(F_dv_or(R.eq(R.count(v_predictions), R.number("0")), R.lt(v_i__171112, R.count(v_predictions)))))
        {
            v_complete__171195 = false;
        }
        object v_local_1__172287 = false;
        if (R.Bool(R.lt(R.number("0"), R.count(v_contradictions__171167))))
        {
            v_local_1__172287 = R.sub(R.number("0"), R.number("1000000"));
        }
        else
        {
            v_local_1__172287 = v_fit__171126;
        }
        object v_local_2__172409 = false;
        if (R.Bool(R.lt(R.number("0"), R.count(v_contradictions__171167))))
        {
            v_local_2__172409 = "inconsistent";
        }
        else
        {
            if (R.Bool(v_complete__171195))
            {
                v_local_2__172409 = "explained";
            }
            else
            {
                v_local_2__172409 = "unknown";
            }
        }
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("fit", v_local_1__172287), F_dv_pair("comparisons", v_comparisons__171142), F_dv_pair("contradictions", v_contradictions__171167), F_dv_pair("remaining", v_remaining__171064) }, new System.Collections.Generic.List<object>(3) { F_dv_pair("expanded", v_expanded__171091), F_dv_pair("probe_refs", v_probes__171219), F_dv_pair("status", v_local_2__172409) });
    }

    public static object F_dvs_sim_work_budget(object v_actor)
    {
        object v_policy__172841 = F_dv_get(v_actor, "policy");
        object v_total__172871 = F_dv_find(F_dv_get(v_actor, "policy"), "model_budget", R.number("0"));
        object v_work__172928 = F_dv_find(F_dv_get(v_actor, "policy"), "model_work_remaining", F_dv_find(F_dv_get(v_actor, "policy"), "model_work_limit", F_dv_find(F_dv_get(v_actor, "policy"), "model_budget", R.number("0"))));
        _ = F_dv_require(R.not(R.lt(v_work__172928, R.number("0"))), "Negative method work limit");
        _ = F_dv_require(R.not(R.lt(v_total__172871, R.number("0"))), "Negative total model budget");
        return F_dv_min(v_total__172871, v_work__172928);
    }

    public static object F_dvs_model_execute(object v_actor, object v_hypothesis, object v_events, object v_time, object v_parent)
    {
        object v_mind__173297 = F_dv_get(v_actor, "mind");
        object v_plan__173323 = F_dvs_sim_plan(v_hypothesis);
        object v_start__173363 = F_dv_find(v_hypothesis, "start_time", v_time);
        object v_predicted__173419 = false;
        object v_assumptions__173445 = F_dv_find(v_hypothesis, "assumptions", new System.Collections.Generic.List<object>(0) { });
        object v_features__173506 = F_dv_find(v_hypothesis, "features", new System.Collections.Generic.List<object>(0) { });
        object v_remaining__173561 = F_dvs_sim_work_budget(v_actor);
        object v_total_budget__173608 = F_dv_find(F_dv_get(v_actor, "policy"), "model_budget", R.number("0"));
        object v_expanded__173672 = R.number("0");
        object v_failures__173693 = new System.Collections.Generic.List<object>(0) { };
        object v_covered__173715 = new System.Collections.Generic.List<object>(0) { };
        object v_trace__173736 = new System.Collections.Generic.List<object>(0) { };
        object v_snapshots__173755 = new System.Collections.Generic.List<object>(0) { };
        object v_predictions__173778 = new System.Collections.Generic.List<object>(0) { };
        object v_assumption_records__173803 = new System.Collections.Generic.List<object>(0) { };
        object v_confidence__173835 = F_dv_unit(F_dv_find(v_hypothesis, "prior", R.number("0.5")));
        object v_i__173899 = R.number("0");
        object v_complete__173913 = true;
        object v_replay_limit__174023 = F_dv_find(F_dv_get(v_actor, "policy"), "Replay_Depth", R.count(v_plan__173323));
        _ = F_dv_require((R.Bool(R.not(R.lt(v_replay_limit__174023, R.number("0")))) ? (object)(R.eq(R.floor(v_replay_limit__174023), v_replay_limit__174023)) : (object)(false)), "Replay depth must be a nonnegative integer");
        object v_replay_stages__174223 = R.number("0");
        object v_replay_stage__174249 = R.sub(R.number("0"), R.number("1"));
        object v_replay_limited__174275 = false;
        object v_last_at__174306 = R.number("0");
        _ = F_dv_domain(v_mind__173297, "mind");
        _ = F_dv_require(R.not(R.lt(v_remaining__173561, R.number("0"))), "Negative simulation budget");
        _ = F_dv_require(R.not(R.lt(v_start__173363, R.number("0"))), "Negative model origin time");
        object v_mask_plan__174486 = F_dv_find(v_hypothesis, "mask_plan", v_plan__173323);
        v_predicted__173419 = F_dvs_sim_mask_response(v_actor, F_dvs_sim_mask(v_mind__173297, v_mask_plan__174486, v_start__173363), v_mask_plan__174486);
        if (R.Bool(R.lt(R.number("0"), R.count(v_assumptions__173445))))
        {
            _ = F_dv_require(R.lt(v_confidence__173835, R.number("1")), "Hypothetical assumption cannot have certainty");
        }
        while (R.Bool(F_dv_and(R.lt(v_i__173899, R.count(v_assumptions__173445)), R.not(R.lt(v_remaining__173561, R.number("1"))))))
        {
            object v_assumption__174835 = R.at(v_assumptions__173445, v_i__173899);
            v_remaining__173561 = R.sub(v_remaining__173561, R.number("1"));
            v_expanded__173672 = R.add(v_expanded__173672, R.number("1"));
            if (R.Bool(R.eq(F_dv_get(v_assumption__174835, "op"), "equals")))
            {
                object v_ref__174989 = F_dv_get(v_assumption__174835, "ref");
                if (R.Bool(F_dv_has_key(F_dv_get(v_predicted__173419, "objects"), F_dv_get(v_ref__174989, "object"))))
                {
                    v_predicted__173419 = F_dv_store(v_predicted__173419, v_ref__174989, F_dv_cell(F_dv_get(v_assumption__174835, "value"), v_confidence__173835, v_start__173363, F_dv_get(v_hypothesis, "basis")));
                    _ = R.push(v_assumption_records__173803, new System.Collections.Generic.List<object>(3) { F_dv_pair("proposition", v_assumption__174835), F_dv_pair("confidence", v_confidence__173835), F_dv_pair("role", "hypothetical-only") });
                }
                else
                {
                    v_complete__173913 = false;
                    _ = R.push(v_failures__173693, new System.Collections.Generic.List<object>(2) { F_dv_pair("assumption", v_assumption__174835), F_dv_pair("reason", "unknown-subject-local-object") });
                }
            }
            else
            {
                v_complete__173913 = false;
                _ = R.push(v_failures__173693, new System.Collections.Generic.List<object>(2) { F_dv_pair("assumption", v_assumption__174835), F_dv_pair("reason", "unsupported-assumption") });
            }
            v_i__173899 = R.add(v_i__173899, R.number("1"));
        }
        if (R.Bool(R.lt(v_i__173899, R.count(v_assumptions__173445))))
        {
            v_complete__173913 = false;
        }
        v_i__173899 = R.number("0");
        while (R.Bool((R.Bool((R.Bool(R.lt(v_i__173899, R.count(v_plan__173323))) ? (object)(R.lt(R.number("0"), v_remaining__173561)) : (object)(false))) ? (object)((R.Bool(R.eq(F_dv_get(R.at(v_plan__173323, v_i__173899), "stage"), v_replay_stage__174249)) ? (object)(true) : (object)(R.lt(v_replay_stages__174223, v_replay_limit__174023)))) : (object)(false))))
        {
            object v_step__175824 = R.at(v_plan__173323, v_i__173899);
            if (R.Bool(R.not(R.eq(F_dv_get(v_step__175824, "stage"), v_replay_stage__174249))))
            {
                v_replay_stage__174249 = F_dv_get(v_step__175824, "stage");
                v_replay_stages__174223 = R.add(v_replay_stages__174223, R.number("1"));
            }
            object v_name__175974 = F_dv_get(R.at(v_plan__173323, v_i__173899), "law");
            object v_at__176005 = F_dv_get(R.at(v_plan__173323, v_i__173899), "at");
            object v_adoptions__176033 = F_dv_get(v_mind__173297, "adoptions");
            _ = F_dv_require(R.not(R.lt(v_at__176005, v_last_at__174306)), "Model time cannot move backwards");
            v_remaining__173561 = R.sub(v_remaining__173561, R.number("1"));
            v_expanded__173672 = R.add(v_expanded__173672, R.number("1"));
            v_last_at__174306 = v_at__176005;
            if (R.Bool(F_dv_has_key(v_step__175824, "response")))
            {
                object v_response__176285 = F_dvs_sim_gated_response(v_actor, F_dv_with(v_predicted__173419, "domain", "mind"), v_step__175824, R.add(v_start__173363, v_at__176005), v_remaining__173561, v_confidence__173835);
                object v_detail__176423 = F_dv_get(v_response__176285, "detail");
                v_predicted__173419 = F_dv_get(v_response__176285, "world");
                v_remaining__173561 = F_dv_get(v_response__176285, "remaining");
                v_expanded__173672 = R.add(v_expanded__173672, F_dv_get(v_detail__176423, "expanded"));
                v_detail__176423 = F_dv_with(v_detail__176423, "choice_record_changes", F_dv_get(v_response__176285, "changes"));
                if (R.Bool(R.eq(F_dv_get(v_detail__176423, "status"), "selected")))
                {
                    object v_bridge__176728 = F_dvs_sim_response_bridge(v_actor, v_predicted__173419, v_detail__176423, R.add(v_start__173363, v_at__176005), v_remaining__173561, v_confidence__173835);
                    v_predicted__173419 = F_dv_get(v_bridge__176728, "world");
                    v_remaining__173561 = F_dv_get(v_bridge__176728, "remaining");
                    v_expanded__173672 = R.add(v_expanded__173672, F_dv_get(v_bridge__176728, "expanded"));
                    v_detail__176423 = F_dv_with(v_detail__176423, "bridge", v_bridge__176728);
                    if (R.Bool(R.eq(F_dv_get(v_bridge__176728, "status"), "applied")))
                    {
                        object v_changes__177098 = F_dv_get(v_bridge__176728, "changes");
                        object v_j__177147 = R.number("0");
                        while (R.Bool(R.lt(v_j__177147, R.count(v_changes__177098))))
                        {
                            if (R.Bool(F_dv_has(v_covered__173715, F_dv_get(R.at(v_changes__177098, v_j__177147), "ref"))))
                            {
                                _ = false;
                            }
                            else
                            {
                                _ = R.push(v_covered__173715, F_dv_get(R.at(v_changes__177098, v_j__177147), "ref"));
                            }
                            v_j__177147 = R.add(v_j__177147, R.number("1"));
                        }
                    }
                    else
                    {
                        v_complete__173913 = false;
                        _ = R.push(v_failures__173693, new System.Collections.Generic.List<object>(2) { F_dv_pair("response", F_dv_get(v_step__175824, "response")), F_dv_pair("reason", F_dv_get(v_bridge__176728, "status")) });
                    }
                }
                else
                {
                    if (R.Bool(F_dv_and(R.eq(F_dv_get(v_detail__176423, "status"), "not-applicable"), F_dv_get(v_step__175824, "optional"))))
                    {
                        _ = false;
                    }
                    else
                    {
                        v_complete__173913 = false;
                        _ = R.push(v_failures__173693, new System.Collections.Generic.List<object>(2) { F_dv_pair("response", F_dv_get(v_step__175824, "response")), F_dv_pair("reason", F_dv_get(v_detail__176423, "reason")) });
                    }
                }
                _ = R.push(v_trace__173736, new System.Collections.Generic.List<object>(4) { F_dv_pair("step", v_i__173899), F_dv_pair("at", v_at__176005), F_dv_pair("stage", F_dv_get(v_step__175824, "stage")), F_dv_pair("response", v_detail__176423) });
            }
            else
            {
                object v_selected_steps__177971 = new System.Collections.Generic.List<object>(1) { v_step__175824 };
                if (R.Bool(F_dv_has_key(v_step__175824, "dispatch")))
                {
                    object v_dispatch__178060 = F_dvs_sim_dispatch(v_mind__173297, v_predicted__173419, v_step__175824);
                    v_selected_steps__177971 = F_dv_get(v_dispatch__178060, "steps");
                    _ = R.push(v_trace__173736, new System.Collections.Generic.List<object>(8) { F_dv_pair("step", v_i__173899), F_dv_pair("at", v_at__176005), F_dv_pair("stage", F_dv_get(v_step__175824, "stage")), F_dv_pair("status", "indexed-dispatch"), F_dv_pair("indexed", F_dv_get(v_dispatch__178060, "indexed")), F_dv_pair("inputs", F_dv_get(v_dispatch__178060, "inputs")), F_dv_pair("excluded", F_dv_get(v_dispatch__178060, "excluded")), F_dv_pair("changes", new System.Collections.Generic.List<object>(0) { }) });
                }
                object v_selected_index__178382 = R.number("0");
                while (R.Bool((R.Bool(R.lt(v_selected_index__178382, R.count(v_selected_steps__177971))) ? (object)((R.Bool(R.not(F_dv_has_key(v_step__175824, "dispatch"))) ? (object)(true) : (object)(R.lt(R.number("0"), v_remaining__173561)))) : (object)(false))))
                {
                    if (R.Bool(F_dv_has_key(v_step__175824, "dispatch")))
                    {
                        v_remaining__173561 = R.sub(v_remaining__173561, R.number("1"));
                        v_expanded__173672 = R.add(v_expanded__173672, R.number("1"));
                    }
                    object v_execution__178679 = F_dvs_sim_rule(v_predicted__173419, v_covered__173715, R.at(v_selected_steps__177971, v_selected_index__178382), v_mind__173297, v_confidence__173835, v_start__173363, v_i__173899, R.count(v_events), v_selected_index__178382);
                    v_predicted__173419 = F_dv_get(v_execution__178679, "world");
                    v_covered__173715 = F_dv_get(v_execution__178679, "coverage");
                    v_complete__173913 = (R.Bool(v_complete__173913) ? (object)(F_dv_get(v_execution__178679, "complete")) : (object)(false));
                    object v_entry_index__178986 = R.number("0");
                    while (R.Bool(R.lt(v_entry_index__178986, R.count(F_dv_get(v_execution__178679, "trace")))))
                    {
                        _ = R.push(v_trace__173736, R.at(F_dv_get(v_execution__178679, "trace"), v_entry_index__178986));
                        v_entry_index__178986 = R.add(v_entry_index__178986, R.number("1"));
                    }
                    v_entry_index__178986 = R.number("0");
                    while (R.Bool(R.lt(v_entry_index__178986, R.count(F_dv_get(v_execution__178679, "failures")))))
                    {
                        _ = R.push(v_failures__173693, R.at(F_dv_get(v_execution__178679, "failures"), v_entry_index__178986));
                        v_entry_index__178986 = R.add(v_entry_index__178986, R.number("1"));
                    }
                    v_selected_index__178382 = R.add(v_selected_index__178382, R.number("1"));
                }
                if (R.Bool(R.lt(v_selected_index__178382, R.count(v_selected_steps__177971))))
                {
                    v_complete__173913 = false;
                }
            }
            _ = R.push(v_snapshots__173755, new System.Collections.Generic.List<object>(4) { F_dv_pair("world", v_predicted__173419), F_dv_pair("coverage", F_dv_clone(v_covered__173715)), F_dv_pair("step", v_i__173899), F_dv_pair("at", v_at__176005) });
            v_i__173899 = R.add(v_i__173899, R.number("1"));
        }
        if (R.Bool(R.lt(v_i__173899, R.count(v_plan__173323))))
        {
            v_replay_limited__174275 = (R.Bool(R.not(R.eq(F_dv_get(R.at(v_plan__173323, v_i__173899), "stage"), v_replay_stage__174249))) ? (object)(R.not(R.lt(v_replay_stages__174223, v_replay_limit__174023))) : (object)(false));
        }
        if (R.Bool(F_dv_or(R.eq(R.count(v_plan__173323), R.number("0")), R.lt(v_i__173899, R.count(v_plan__173323)))))
        {
            v_complete__173913 = false;
        }
        v_i__173899 = R.number("0");
        while (R.Bool(R.lt(v_i__173899, R.count(v_features__173506))))
        {
            _ = R.push(v_predictions__173778, F_dvs_sim_prediction(R.at(v_features__173506, v_i__173899), F_dvs_sim_snapshot(v_snapshots__173755, v_predicted__173419, v_covered__173715, R.at(v_features__173506, v_i__173899)), v_start__173363));
            v_i__173899 = R.add(v_i__173899, R.number("1"));
        }
        object v_comparison__180074 = F_dvs_sim_comparisons(v_mind__173297, v_predictions__173778, v_remaining__173561);
        object v_result__180145 = F_dv_with_many(v_comparison__180074, new System.Collections.Generic.List<object>(6) { F_dv_pair("expanded", R.add(v_expanded__173672, F_dv_get(v_comparison__180074, "expanded"))), F_dv_pair("failures", v_failures__173693), F_dv_pair("coverage", v_covered__173715), F_dv_pair("assumptions", v_assumption_records__173803), F_dv_pair("predictions", v_predictions__173778), F_dv_pair("trace", v_trace__173736) });
        v_result__180145 = F_dv_with(v_result__180145, "predicted", v_predicted__173419);
        if (R.Bool(F_dv_and(R.not(v_complete__173913), R.eq(F_dv_get(v_result__180145, "status"), "explained"))))
        {
            v_result__180145 = F_dv_with(v_result__180145, "status", "unknown");
        }
        v_result__180145 = F_dv_with_many(v_result__180145, new System.Collections.Generic.List<object>(5) { F_dv_pair("plan_complete", v_complete__173913), F_dv_pair("source_revision", F_dv_get(v_mind__173297, "revision")), F_dv_pair("original_revision", F_dv_get(v_mind__173297, "revision")), F_dv_pair("role", "internal-enumeration"), F_dv_pair("start_time", v_start__173363) });
        object v_local_2__180713 = false;
        if (R.Bool(R.lt(F_dv_get(v_result__180145, "remaining"), R.number("1"))))
        {
            v_local_2__180713 = "budget-exhausted";
        }
        else
        {
            if (R.Bool(R.eq(R.count(v_plan__173323), R.number("0"))))
            {
                v_local_2__180713 = "no-declared-plan";
            }
            else
            {
                if (R.Bool(v_complete__173913))
                {
                    v_local_2__180713 = "declared-plan-complete";
                }
                else
                {
                    v_local_2__180713 = "declared-plan-incomplete";
                }
            }
        }
        v_result__180145 = F_dv_with_many(v_result__180145, new System.Collections.Generic.List<object>(2) { F_dv_pair("calculation_unit", "one-assumption-predict-rule-indexed-dispatch-context-binding-option-prediction-option-evaluation-response-dispatch-or-feature-comparison"), F_dv_pair("stop_reason", v_local_2__180713) });
        if (R.Bool(v_replay_limited__174275))
        {
            v_result__180145 = F_dv_with(v_result__180145, "stop_reason", "replay-depth-exhausted");
        }
        v_result__180145 = F_dv_with_many(v_result__180145, new System.Collections.Generic.List<object>(3) { F_dv_pair("replay_depth_limit", v_replay_limit__174023), F_dv_pair("replay_stages_expanded", v_replay_stages__174223), F_dv_pair("replay_depth_exhausted", v_replay_limited__174275) });
        v_result__180145 = F_dv_with_many(v_result__180145, new System.Collections.Generic.List<object>(2) { F_dv_pair("work_remaining", F_dv_get(v_result__180145, "remaining")), F_dv_pair("remaining", R.sub(v_total_budget__173608, F_dv_get(v_result__180145, "expanded"))) });
        v_result__180145 = F_dv_with_many(v_result__180145, new System.Collections.Generic.List<object>(2) { F_dv_pair("original_expanded", F_dv_get(v_result__180145, "expanded")), F_dv_pair("total_expanded", F_dv_get(v_result__180145, "expanded")) });
        object v_parents__181749 = F_dvs_cognitive_parents(v_actor, F_dv_single(v_parent));
        object v_paid__181815 = F_dv_find(v_actor, "model_paid_event", "");
        object v_previous__181869 = F_dv_find(v_actor, "model_previous_event", "");
        if (R.Bool(F_dv_and(R.not(R.eq(v_paid__181815, "")), R.not(F_dv_has(v_parents__181749, v_paid__181815)))))
        {
            _ = R.push(v_parents__181749, v_paid__181815);
        }
        if (R.Bool(F_dv_and(R.not(R.eq(v_previous__181869, "")), R.not(F_dv_has(v_parents__181749, v_previous__181869)))))
        {
            _ = R.push(v_parents__181749, v_previous__181869);
        }
        _ = F_dvs_event(v_events, "mind_simulation", F_dv_get(v_actor, "id"), v_time, v_parents__181749, new System.Collections.Generic.List<object>(5) { F_dv_pair("mind_revision", F_dv_get(v_mind__173297, "revision")), F_dv_pair("hypothesis", v_hypothesis), F_dv_pair("result", v_result__180145), F_dv_pair("actor_before", F_dv_without(v_actor, "simulations")), F_dv_pair("actor_id", F_dv_get(v_actor, "id")) });
        return v_result__180145;
    }

    public static object F_dvs_model_recheck_eligible(object v_actor, object v_hypothesis, object v_model, object v_time)
    {
        if (R.Bool(R.eq(v_model, false)))
        {
            return false;
        }
        if (R.Bool(R.eq(F_dv_find(F_dv_find(v_model, "evaluation_scope", new System.Collections.Generic.List<object>(0) { }), "kind", ""), "causal-refutation")))
        {
            return false;
        }
        object v_required__182758 = new System.Collections.Generic.List<object>(9) { "plan_complete", "failures", "source_revision", "source_mind_time", "source_hypothesis", "source_adoptions", "source_response_models", "source_replay_depth", "start_time" };
        object v_i__182950 = R.number("0");
        while (R.Bool(R.lt(v_i__182950, R.count(v_required__182758))))
        {
            if (R.Bool(R.not(F_dv_has_key(v_model, R.at(v_required__182758, v_i__182950)))))
            {
                return false;
            }
            v_i__182950 = R.add(v_i__182950, R.number("1"));
        }
        if (R.Bool((R.Bool(R.not(F_dv_get(v_model, "plan_complete"))) ? (object)(true) : (object)(R.not(R.eq(R.count(F_dv_get(v_model, "failures")), R.number("0")))))))
        {
            return false;
        }
        if (R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_model, "source_revision"), F_dv_get(F_dv_get(v_actor, "mind"), "revision")))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_model, "source_mind_time"), F_dv_get(F_dv_get(v_actor, "mind"), "time")))))))
        {
            return false;
        }
        if (R.Bool(R.not(F_dv_same(F_dv_get(v_model, "source_hypothesis"), v_hypothesis))))
        {
            return false;
        }
        if (R.Bool(R.not(F_dv_same(F_dv_get(v_model, "source_adoptions"), F_dv_get(F_dv_get(v_actor, "mind"), "adoptions")))))
        {
            return false;
        }
        if (R.Bool(R.not(F_dv_same(F_dv_get(v_model, "source_response_models"), F_dv_find(v_actor, "response_models", new System.Collections.Generic.List<object>(0) { })))))
        {
            return false;
        }
        object v_depth__183568 = F_dv_find(F_dv_get(v_actor, "policy"), "Replay_Depth", R.count(F_dvs_sim_plan(v_hypothesis)));
        if (R.Bool(R.not(R.eq(F_dv_get(v_model, "source_replay_depth"), v_depth__183568))))
        {
            return false;
        }
        return R.eq(F_dv_get(v_model, "start_time"), F_dv_find(v_hypothesis, "start_time", v_time));
    }

    public static object F_dvs_model_recheck(object v_actor, object v_model)
    {
        object v_mind__183830 = F_dv_get(v_actor, "mind");
        object v_comparison__183856 = F_dvs_sim_comparisons(F_dv_get(v_actor, "mind"), F_dv_get(v_model, "predictions"), F_dvs_sim_work_budget(v_actor));
        object v_result__183956 = F_dv_clone(v_model);
        object v_keys__183989 = F_dv_list3("fit", "comparisons", "contradictions");
        object v_i__184053 = R.number("0");
        _ = R.push(v_keys__183989, "remaining");
        _ = R.push(v_keys__183989, "expanded");
        _ = R.push(v_keys__183989, "probe_refs");
        _ = R.push(v_keys__183989, "status");
        while (R.Bool(R.lt(v_i__184053, R.count(v_keys__183989))))
        {
            v_result__183956 = F_dv_with(v_result__183956, R.at(v_keys__183989, v_i__184053), F_dv_get(v_comparison__183856, R.at(v_keys__183989, v_i__184053)));
            v_i__184053 = R.add(v_i__184053, R.number("1"));
        }
        if (R.Bool(F_dv_and(R.not(F_dv_get(v_model, "plan_complete")), R.eq(F_dv_get(v_result__183956, "status"), "explained"))))
        {
            v_result__183956 = F_dv_with(v_result__183956, "status", "unknown");
        }
        v_result__183956 = F_dv_with_many(v_result__183956, new System.Collections.Generic.List<object>(2) { F_dv_pair("work_remaining", F_dv_get(v_comparison__183856, "remaining")), F_dv_pair("remaining", R.sub(F_dv_find(F_dv_get(v_actor, "policy"), "model_budget", R.number("0")), F_dv_get(v_comparison__183856, "expanded"))) });
        object v_local_1__184594 = false;
        if (R.Bool(R.lt(F_dv_get(v_comparison__183856, "remaining"), R.number("1"))))
        {
            v_local_1__184594 = "budget-exhausted";
        }
        else
        {
            v_local_1__184594 = "observations-compared";
        }
        v_result__183956 = F_dv_with_many(v_result__183956, new System.Collections.Generic.List<object>(2) { F_dv_pair("total_expanded", R.add(F_dv_find(v_model, "total_expanded", F_dv_get(v_model, "expanded")), F_dv_get(v_comparison__183856, "expanded"))), F_dv_pair("stop_reason", v_local_1__184594) });
        if (R.Bool(F_dv_find(v_model, "replay_depth_exhausted", false)))
        {
            v_result__183956 = F_dv_with(v_result__183956, "stop_reason", "replay-depth-exhausted");
        }
        return F_dv_with_many(v_result__183956, new System.Collections.Generic.List<object>(3) { F_dv_pair("source_revision", F_dv_get(v_mind__183830, "revision")), F_dv_pair("paid_model_event", F_dv_find(v_model, "paid_model_event", F_dv_find(v_model, "event", ""))), F_dv_pair("role", "new-observation-recheck") });
    }

    public static object F_dvs_sim_response_unknown(object v_world, object v_remaining, object v_reason)
    {
        return new System.Collections.Generic.List<object>(4) { F_dv_pair("world", v_world), F_dv_pair("changes", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("remaining", v_remaining), F_dv_pair("detail", new System.Collections.Generic.List<object>(4) { F_dv_pair("status", "unknown"), F_dv_pair("reason", v_reason), F_dv_pair("expanded", R.number("0")), F_dv_pair("committed", false) }) };
    }

    public static object F_dvs_sim_response(object v_actor, object v_world, object v_stage, object v_time, object v_remaining, object v_confidence)
    {
        object v_models__185493 = F_dv_find(v_actor, "response_models", new System.Collections.Generic.List<object>(0) { });
        object v_model_id__185548 = F_dv_get(v_stage, "model");
        if (R.Bool(R.not(F_dv_has_key(v_models__185493, v_model_id__185548))))
        {
            return F_dvs_sim_response_unknown(v_world, v_remaining, "no-subject-local-response-model");
        }
        else
        {
            object v_model__185729 = F_dv_with(F_dv_get(v_models__185493, v_model_id__185548), "confidence", F_dv_min(v_confidence, F_dv_find(F_dv_get(v_models__185493, v_model_id__185548), "confidence", R.number("0"))));
            if (R.Bool(R.not(F_dv_and(R.eq(F_dv_find(v_model__185729, "owner", ""), F_dv_get(v_actor, "id")), F_dv_and(R.lt(R.number("0"), R.count(F_dv_find(v_model__185729, "basis", new System.Collections.Generic.List<object>(0) { }))), R.lt(R.number("0"), F_dv_unit(F_dv_find(v_model__185729, "confidence", R.number("0")))))))))
            {
                return F_dvs_sim_response_unknown(v_world, v_remaining, "unavailable-response-model-basis");
            }
            else
            {
                object v_local__186154 = F_dv_clone(F_dv_get(v_model__185729, "world"));
                object v_bindings__186200 = F_dv_find(v_model__185729, "context", new System.Collections.Generic.List<object>(0) { });
                object v_option_ids__186257 = F_dv_get(v_stage, "options");
                object v_catalog__186300 = F_dv_get(v_model__185729, "options");
                object v_policy_id__186340 = F_dv_get(v_model__185729, "policy_id");
                object v_request_id__186384 = F_dv_get(v_stage, "request");
                object v_requests__186427 = F_dv_get(v_model__185729, "requests");
                object v_required__186469 = R.add(R.count(v_bindings__186200), R.mul(R.number("2"), R.count(v_option_ids__186257)));
                _ = F_dv_domain(v_local__186154, "mind");
                if (R.Bool(R.lt(v_remaining, v_required__186469)))
                {
                    return F_dvs_sim_response_unknown(v_world, v_remaining, "method-work-limit-before-response");
                }
                else
                {
                    if (R.Bool(R.not(F_dv_has_key(v_requests__186427, v_request_id__186384))))
                    {
                        return F_dvs_sim_response_unknown(v_world, v_remaining, "unknown-modeled-request");
                    }
                    else
                    {
                        if (R.Bool(R.not(F_dv_has_key(F_dv_find(v_local__186154, "policies", new System.Collections.Generic.List<object>(0) { }), v_policy_id__186340))))
                        {
                            return F_dvs_sim_response_unknown(v_world, v_remaining, "unknown-modeled-value-policy");
                        }
                        else
                        {
                            object v_accepted__187149 = F_dv_get(F_dv_get(v_local__186154, "policies"), v_policy_id__186340);
                            if (R.Bool(R.not(F_dv_and(F_dv_get(v_accepted__187149, "active"), R.lt(R.number("0"), R.count(F_dv_get(v_accepted__187149, "basis")))))))
                            {
                                return F_dvs_sim_response_unknown(v_world, v_remaining, "unadopted-modeled-value-policy");
                            }
                            else
                            {
                                object v_request__187458 = F_dv_get(v_requests__186427, v_request_id__186384);
                                object v_options__187529 = new System.Collections.Generic.List<object>(0) { };
                                object v_inputs__187574 = new System.Collections.Generic.List<object>(0) { };
                                object v_option_traces__187618 = new System.Collections.Generic.List<object>(0) { };
                                object v_i__187669 = R.number("0");
                                object v_complete__187707 = true;
                                object v_expanded__187755 = R.number("0");
                                object v_chosen__187800 = false;
                                object v_prediction__187847 = false;
                                object v_response__187898 = false;
                                object v_next__187947 = v_world;
                                object v_changes__187992 = new System.Collections.Generic.List<object>(0) { };
                                object v_reason__188037 = "unknown-response-basis";
                                object v_response_confidence__188103 = F_dv_get(v_model__185729, "confidence");
                                v_local__186154 = F_dv_with_many(v_local__186154, new System.Collections.Generic.List<object>(2) { F_dv_pair("time", v_time), F_dv_pair("seen", new System.Collections.Generic.List<object>(0) { }) });
                                while (R.Bool(R.lt(v_i__187669, R.count(v_bindings__186200))))
                                {
                                    object v_binding__188322 = R.at(v_bindings__186200, v_i__187669);
                                    object v_cell__188380 = F_dv_read(v_world, F_dv_get(R.at(v_bindings__186200, v_i__187669), "from"));
                                    v_expanded__187755 = R.add(v_expanded__187755, R.number("1"));
                                    v_remaining = R.sub(v_remaining, R.number("1"));
                                    _ = R.push(v_inputs__187574, new System.Collections.Generic.List<object>(3) { F_dv_pair("from", F_dv_get(v_binding__188322, "from")), F_dv_pair("to", F_dv_get(v_binding__188322, "to")), F_dv_pair("cell", v_cell__188380) });
                                    if (R.Bool(F_dv_and(F_dv_uncontested(v_cell__188380), R.lt(R.number("0"), F_dv_get(v_cell__188380, "confidence")))))
                                    {
                                        v_response_confidence__188103 = F_dv_min(v_response_confidence__188103, F_dv_get(v_cell__188380, "confidence"));
                                    }
                                    if (R.Bool(F_dv_has_key(F_dv_get(v_local__186154, "objects"), F_dv_get(F_dv_get(v_binding__188322, "to"), "object"))))
                                    {
                                        object v_local_1__189154 = false;
                                        if (R.Bool(F_dv_uncontested(v_cell__188380)))
                                        {
                                            v_local_1__189154 = F_dv_with(v_cell__188380, "confidence", F_dv_min(F_dv_get(v_cell__188380, "confidence"), F_dv_get(v_model__185729, "confidence")));
                                        }
                                        else
                                        {
                                            v_local_1__189154 = F_dv_unknown();
                                        }
                                        v_local__186154 = F_dv_store(v_local__186154, F_dv_get(v_binding__188322, "to"), v_local_1__189154);
                                    }
                                    else
                                    {
                                        v_complete__187707 = false;
                                    }
                                    v_i__187669 = R.add(v_i__187669, R.number("1"));
                                }
                                v_i__187669 = R.number("0");
                                while (R.Bool(R.lt(v_i__187669, R.count(v_option_ids__186257))))
                                {
                                    object v_option_id__189805 = R.at(v_option_ids__186257, v_i__187669);
                                    v_expanded__187755 = R.add(v_expanded__187755, R.number("2"));
                                    v_remaining = R.sub(v_remaining, R.number("2"));
                                    if (R.Bool(F_dv_has_key(v_catalog__186300, v_option_id__189805)))
                                    {
                                        object v_descriptor__190056 = F_dv_get(v_catalog__186300, v_option_id__189805);
                                        object v_name__190136 = F_dv_find(F_dv_get(v_catalog__186300, v_option_id__189805), "predict_law", "");
                                        object v_result__190238 = false;
                                        object v_legal__190293 = F_dv_truth(v_local__186154, F_dv_get(F_dv_get(v_catalog__186300, v_option_id__189805), "feasible"));
                                        v_prediction__187847 = F_dv_no_change_prediction(v_local__186154);
                                        if (R.Bool(R.eq(v_legal__190293, "unknown")))
                                        {
                                            v_complete__187707 = false;
                                            v_reason__188037 = "unknown-required-response-context";
                                        }
                                        if (R.Bool(R.eq(v_name__190136, "")))
                                        {
                                            _ = false;
                                        }
                                        else
                                        {
                                            if (R.Bool(F_dv_has_key(F_dv_get(v_local__186154, "adoptions"), v_name__190136)))
                                            {
                                                object v_adoption__190904 = F_dv_get(F_dv_get(v_local__186154, "adoptions"), v_name__190136);
                                                if (R.Bool(F_dv_and(F_dv_get(v_adoption__190904, "active"), R.eq(F_dv_get(v_adoption__190904, "mode"), "predict"))))
                                                {
                                                    v_result__190238 = F_dv_predict(v_local__186154, v_name__190136, R.cat("response-option-", R.show(v_i__187669)), v_time, F_dv_get(v_local__186154, "revision"));
                                                    if (R.Bool(R.eq(F_dv_get(v_result__190238, "status"), "applied")))
                                                    {
                                                        v_prediction__187847 = F_dv_get(v_result__190238, "world");
                                                    }
                                                    else
                                                    {
                                                        if (R.Bool(R.eq(v_legal__190293, "false")))
                                                        {
                                                            _ = false;
                                                        }
                                                        else
                                                        {
                                                            v_complete__187707 = false;
                                                            if (R.Bool(R.eq(F_dv_truth(v_local__186154, F_dv_get(F_dv_get(v_adoption__190904, "law"), "premise")), "unknown")))
                                                            {
                                                                v_reason__188037 = "unknown-required-response-context";
                                                            }
                                                            else
                                                            {
                                                                v_reason__188037 = "response-option-prediction-failed";
                                                            }
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    if (R.Bool(R.eq(v_legal__190293, "false")))
                                                    {
                                                        _ = false;
                                                    }
                                                    else
                                                    {
                                                        v_complete__187707 = false;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                if (R.Bool(R.eq(v_legal__190293, "false")))
                                                {
                                                    _ = false;
                                                }
                                                else
                                                {
                                                    v_complete__187707 = false;
                                                }
                                            }
                                        }
                                        _ = R.push(v_options__187529, F_dv_response_option(v_option_id__189805, F_dv_get(v_descriptor__190056, "intent"), F_dv_get(v_descriptor__190056, "feasible"), v_prediction__187847, F_dv_get(v_descriptor__190056, "effort"), F_dv_get(v_descriptor__190056, "feature_weights")));
                                        _ = R.push(v_option_traces__187618, new System.Collections.Generic.List<object>(4) { F_dv_pair("id", v_option_id__189805), F_dv_pair("legal", v_legal__190293), F_dv_pair("prediction_receipt", v_result__190238), F_dv_pair("model_prediction", v_prediction__187847) });
                                    }
                                    else
                                    {
                                        v_complete__187707 = false;
                                        _ = R.push(v_option_traces__187618, new System.Collections.Generic.List<object>(2) { F_dv_pair("id", v_option_id__189805), F_dv_pair("legal", "unknown-option") });
                                    }
                                    v_i__187669 = R.add(v_i__187669, R.number("1"));
                                }
                                v_response__187898 = F_dv_respond_adopted(v_local__186154, v_request__187458, v_options__187529, v_policy_id__186340);
                                object v_evaluated__193343 = F_dv_get(v_response__187898, "evaluated");
                                object v_j__193406 = R.number("0");
                                object v_ties__193444 = R.number("0");
                                object v_best__193485 = R.number("0");
                                object v_found_best__193526 = false;
                                while (R.Bool(R.lt(v_j__193406, R.count(v_evaluated__193343))))
                                {
                                    object v_evaluation__193642 = R.at(R.at(v_evaluated__193343, v_j__193406), R.number("1"));
                                    object v_score__193707 = F_dv_get(R.at(R.at(v_evaluated__193343, v_j__193406), R.number("1")), "score");
                                    if (R.Bool(F_dv_and(F_dv_get(F_dv_get(v_evaluation__193642, "value"), "complete"), R.eq(R.count(F_dv_get(F_dv_get(v_evaluation__193642, "relationship_and_state"), "unknown")), R.number("0")))))
                                    {
                                        _ = false;
                                    }
                                    else
                                    {
                                        v_complete__187707 = false;
                                    }
                                    object v_local_2__194036 = false;
                                    if (R.Bool(v_found_best__193526))
                                    {
                                        v_local_2__194036 = R.lt(v_best__193485, v_score__193707);
                                    }
                                    else
                                    {
                                        v_local_2__194036 = true;
                                    }
                                    if (R.Bool(v_local_2__194036))
                                    {
                                        v_best__193485 = v_score__193707;
                                        v_ties__193444 = R.number("1");
                                        v_found_best__193526 = true;
                                    }
                                    else
                                    {
                                        if (R.Bool(R.eq(v_best__193485, v_score__193707)))
                                        {
                                            v_ties__193444 = R.add(v_ties__193444, R.number("1"));
                                        }
                                    }
                                    v_j__193406 = R.add(v_j__193406, R.number("1"));
                                }
                                if (R.Bool(R.lt(R.number("1"), v_ties__193444)))
                                {
                                    if (R.Bool(v_complete__187707))
                                    {
                                        v_reason__188037 = "modeled-response-tie";
                                    }
                                    v_complete__187707 = false;
                                }
                                if (R.Bool(F_dv_and(v_complete__187707, R.eq(F_dv_get(v_response__187898, "status"), "selected"))))
                                {
                                    object v_id__194960 = F_dv_get(F_dv_get(v_response__187898, "choice"), "id");
                                    object v_choice_ref__195020 = F_dv_get(v_model__185729, "choice_ref");
                                    v_chosen__187800 = v_id__194960;
                                    v_reason__188037 = "selected-by-shared-response";
                                    if (R.Bool(F_dv_has_key(F_dv_get(v_next__187947, "objects"), F_dv_get(v_choice_ref__195020, "object"))))
                                    {
                                        object v_before__195285 = F_dv_read(v_next__187947, v_choice_ref__195020);
                                        object v_after__195360 = F_dv_cell(v_id__194960, v_response_confidence__188103, v_time, F_dv_get(v_model__185729, "basis"));
                                        v_next__187947 = F_dv_store(v_next__187947, v_choice_ref__195020, v_after__195360);
                                        _ = R.push(v_changes__187992, new System.Collections.Generic.List<object>(3) { F_dv_pair("ref", v_choice_ref__195020), F_dv_pair("before", v_before__195285), F_dv_pair("after", v_after__195360) });
                                    }
                                    else
                                    {
                                        v_complete__187707 = false;
                                        v_reason__188037 = "unknown-local-choice-record";
                                    }
                                }
                                object v_local_3__195803 = false;
                                if (R.Bool(v_complete__187707))
                                {
                                    v_local_3__195803 = F_dv_get(v_response__187898, "status");
                                }
                                else
                                {
                                    v_local_3__195803 = "unknown";
                                }
                                object v_local_4__196036 = false;
                                if (R.Bool(R.eq(v_chosen__187800, false)))
                                {
                                    v_local_4__196036 = "";
                                }
                                else
                                {
                                    v_local_4__196036 = F_dv_find(F_dv_get(v_catalog__186300, v_chosen__187800), "bridge_law", "");
                                }
                                return new System.Collections.Generic.List<object>(4) { F_dv_pair("world", v_next__187947), F_dv_pair("changes", v_changes__187992), F_dv_pair("remaining", v_remaining), F_dv_pair("detail", F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("status", v_local_3__195803), F_dv_pair("reason", v_reason__188037), F_dv_pair("expanded", v_expanded__187755), F_dv_pair("committed", false) }, new System.Collections.Generic.List<object>(7) { F_dv_pair("model", v_model_id__185548), F_dv_pair("model_basis", F_dv_get(v_model__185729, "basis")), F_dv_pair("context_inputs", v_inputs__187574), F_dv_pair("option_predictions", v_option_traces__187618), F_dv_pair("response", v_response__187898), F_dv_pair("selected", v_chosen__187800), F_dv_pair("bridge_law", v_local_4__196036) })) };
                            }
                        }
                    }
                }
            }
        }
    }

    public static object F_dvs_sim_mask_response_option(object v_world, object v_model, object v_option_id)
    {
        object v_option__196718 = F_dv_find(F_dv_get(v_model, "options"), v_option_id, new System.Collections.Generic.List<object>(0) { });
        if (R.Bool(F_dv_has_key(v_option__196718, "bridge_law")))
        {
            object v_name__196818 = F_dv_get(v_option__196718, "bridge_law");
            if (R.Bool(F_dv_has_key(F_dv_get(v_world, "adoptions"), v_name__196818)))
            {
                return F_dv_get(F_dvs_sim_withhold(v_world, F_dv_get(F_dv_get(F_dv_get(F_dv_get(v_world, "adoptions"), v_name__196818), "law"), "effects")), "world");
            }
            else
            {
                return v_world;
            }
        }
        else
        {
            return v_world;
        }
    }

    public static object F_dvs_sim_mask_response_stage(object v_actor, object v_world, object v_stage)
    {
        object v_models__197113 = F_dv_find(v_actor, "response_models", new System.Collections.Generic.List<object>(0) { });
        object v_id__197168 = F_dv_get(v_stage, "model");
        if (R.Bool(F_dv_has_key(v_models__197113, v_id__197168)))
        {
            object v_model__197228 = F_dv_get(v_models__197113, v_id__197168);
            object v_options__197267 = F_dv_get(v_stage, "options");
            object v_i__197303 = R.number("0");
            object v_copy__197321 = v_world;
            if (R.Bool(F_dv_has_key(v_model__197228, "choice_ref")))
            {
                if (R.Bool(F_dv_has_key(F_dv_get(v_copy__197321, "objects"), F_dv_get(F_dv_get(v_model__197228, "choice_ref"), "object"))))
                {
                    v_copy__197321 = F_dv_store(v_copy__197321, F_dv_get(v_model__197228, "choice_ref"), F_dv_unknown());
                }
            }
            while (R.Bool(R.lt(v_i__197303, R.count(v_options__197267))))
            {
                v_copy__197321 = F_dvs_sim_mask_response_option(v_copy__197321, v_model__197228, R.at(v_options__197267, v_i__197303));
                v_i__197303 = R.add(v_i__197303, R.number("1"));
            }
            return v_copy__197321;
        }
        else
        {
            return v_world;
        }
    }

    public static object F_dvs_sim_mask_response(object v_actor, object v_world, object v_plan)
    {
        object v_copy__197756 = v_world;
        object v_i__197777 = R.number("0");
        while (R.Bool(R.lt(v_i__197777, R.count(v_plan))))
        {
            object v_step__197827 = R.at(v_plan, v_i__197777);
            if (R.Bool(F_dv_has_key(v_step__197827, "response")))
            {
                v_copy__197756 = F_dvs_sim_mask_response_stage(v_actor, v_copy__197756, F_dv_get(v_step__197827, "response"));
            }
            v_i__197777 = R.add(v_i__197777, R.number("1"));
        }
        return v_copy__197756;
    }

    public static object F_dvs_sim_response_bridge(object v_actor, object v_world, object v_detail, object v_time, object v_remaining, object v_confidence)
    {
        object v_name__198082 = F_dv_get(v_detail, "bridge_law");
        object v_adoptions__198115 = F_dv_get(F_dv_get(v_actor, "mind"), "adoptions");
        if (R.Bool(F_dv_or(R.eq(v_name__198082, ""), R.not(F_dv_has_key(v_adoptions__198115, v_name__198082)))))
        {
            return F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("world", v_world), F_dv_pair("changes", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("remaining", v_remaining), F_dv_pair("status", "unknown-response-bridge") }, "expanded", R.number("0"));
        }
        else
        {
            object v_adoption__198365 = F_dv_get(v_adoptions__198115, v_name__198082);
            object v_effects__198412 = F_dv_get(F_dv_get(F_dv_get(v_adoptions__198115, v_name__198082), "law"), "effects");
            if (R.Bool(F_dv_or(R.lt(v_remaining, R.number("1")), R.not(F_dv_and(F_dv_get(v_adoption__198365, "active"), R.eq(F_dv_get(v_adoption__198365, "mode"), "predict"))))))
            {
                return F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("world", F_dv_get(F_dvs_sim_withhold(v_world, v_effects__198412), "world")), F_dv_pair("changes", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("remaining", v_remaining), F_dv_pair("status", "unavailable-response-bridge") }, "expanded", R.number("0"));
            }
            else
            {
                object v_local__198757 = F_dv_with_many(v_world, new System.Collections.Generic.List<object>(2) { F_dv_pair("domain", "mind"), F_dv_pair("adoptions", F_dv_with(F_dv_get(v_world, "adoptions"), v_name__198082, F_dv_with(v_adoption__198365, "confidence", F_dv_min(v_confidence, F_dv_min(F_dv_get(v_adoption__198365, "confidence"), F_dv_premise_confidence(v_world, F_dv_get(F_dv_get(v_adoption__198365, "law"), "premise"))))))) });
                object v_result__199000 = false;
                v_result__199000 = F_dv_predict(v_local__198757, v_name__198082, R.cat("response-bridge-", R.show(F_dv_get(v_world, "revision"))), v_time, F_dv_get(v_world, "revision"));
                object v_local_1__199151 = false;
                if (R.Bool(R.eq(F_dv_get(v_result__199000, "status"), "applied")))
                {
                    v_local_1__199151 = F_dv_get(v_result__199000, "world");
                }
                else
                {
                    v_local_1__199151 = F_dv_get(F_dvs_sim_withhold(v_world, v_effects__198412), "world");
                }
                return F_dv_with_many(v_result__199000, new System.Collections.Generic.List<object>(3) { F_dv_pair("world", v_local_1__199151), F_dv_pair("remaining", R.sub(v_remaining, R.number("1"))), F_dv_pair("expanded", R.number("1")) });
            }
        }
    }

    public static object F_dvs_sim_gated_response(object v_actor, object v_world, object v_step, object v_time, object v_remaining, object v_confidence)
    {
        object v_gate__199524 = F_dv_find(v_step, "available_when", F_dv_true());
        object v_truth__199582 = F_dv_truth(v_world, F_dv_find(v_step, "available_when", F_dv_true()));
        object v_result__199658 = false;
        object v_local_1__199681 = false;
        if (R.Bool(R.eq(v_truth__199582, "true")))
        {
            v_local_1__199681 = F_dvs_sim_response(v_actor, v_world, F_dv_get(v_step, "response"), v_time, v_remaining, v_confidence);
        }
        else
        {
            if (R.Bool(R.eq(v_truth__199582, "false")))
            {
                v_local_1__199681 = new System.Collections.Generic.List<object>(4) { F_dv_pair("world", v_world), F_dv_pair("changes", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("remaining", v_remaining), F_dv_pair("detail", new System.Collections.Generic.List<object>(4) { F_dv_pair("status", "not-applicable"), F_dv_pair("reason", "request-gate-false"), F_dv_pair("expanded", R.number("0")), F_dv_pair("committed", false) }) };
            }
            else
            {
                v_local_1__199681 = F_dvs_sim_response_unknown(F_dvs_sim_mask_response_stage(v_actor, v_world, F_dv_get(v_step, "response")), v_remaining, "request-gate-unknown");
            }
        }
        v_result__199658 = v_local_1__199681;
        return F_dv_with(v_result__199658, "detail", F_dv_with_many(F_dv_get(v_result__199658, "detail"), new System.Collections.Generic.List<object>(3) { F_dv_pair("gate", v_gate__199524), F_dv_pair("gate_truth", v_truth__199582), F_dv_pair("gate_inputs", F_dvs_sim_inputs(v_world, v_gate__199524)) }));
    }
}
