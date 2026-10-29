using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Cognition / Appraisal: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dv_criterion(object v_id, object v_ref, object v_mode, object v_target, object v_weight)
    {
        _ = F_dv_require(F_dv_or(R.eq(v_mode, "numeric"), R.eq(v_mode, "matches")), "Unsupported value criterion");
        return F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("id", v_id), F_dv_pair("ref", v_ref), F_dv_pair("mode", v_mode), F_dv_pair("target", v_target) }, "weight", v_weight);
    }

    public static object F_dv_measure(object v_value, object v_criterion)
    {
        if (R.Bool(R.eq(F_dv_get(v_criterion, "mode"), "numeric")))
        {
            return R.mul(v_value, F_dv_get(v_criterion, "weight"));
        }
        else
        {
            if (R.Bool(F_dv_same(v_value, F_dv_get(v_criterion, "target"))))
            {
                return F_dv_get(v_criterion, "weight");
            }
            else
            {
                return R.number("0");
            }
        }
    }

    public static object F_dv_expected_measure(object v_cell, object v_criterion)
    {
        object v_alternatives__64917 = F_dv_find(v_cell, "alternatives", new System.Collections.Generic.List<object>(0) { });
        object v_sum__64974 = R.number("0");
        object v_i__64990 = R.number("0");
        if (R.Bool(R.eq(R.count(v_alternatives__64917), R.number("0"))))
        {
            return R.mul(F_dv_measure(F_dv_get(v_cell, "value"), v_criterion), F_dv_get(v_cell, "confidence"));
        }
        else
        {
            while (R.Bool(R.lt(v_i__64990, R.count(v_alternatives__64917))))
            {
                object v_alternative__65173 = R.at(v_alternatives__64917, v_i__64990);
                v_sum__64974 = R.add(v_sum__64974, R.mul(F_dv_measure(R.at(v_alternative__65173, R.number("0")), v_criterion), R.at(v_alternative__65173, R.number("1"))));
                v_i__64990 = R.add(v_i__64990, R.number("1"));
            }
            return R.mul(v_sum__64974, F_dv_get(v_cell, "confidence"));
        }
    }

    public static object F_dv_value(object v_world, object v_criteria, object v_subjective)
    {
        object v_i__65400 = R.number("0");
        object v_score__65414 = R.number("0");
        object v_details__65432 = new System.Collections.Generic.List<object>(0) { };
        object v_unknown__65453 = new System.Collections.Generic.List<object>(0) { };
        object v_ids__65474 = new System.Collections.Generic.List<object>(0) { };
        while (R.Bool(R.lt(v_i__65400, R.count(v_criteria))))
        {
            object v_criterion__65531 = R.at(v_criteria, v_i__65400);
            object v_id__65567 = F_dv_get(v_criterion__65531, "id");
            object v_cell__65597 = F_dv_read(v_world, F_dv_get(v_criterion__65531, "ref"));
            _ = F_dv_require(R.not(F_dv_has(v_ids__65474, v_id__65567)), "Duplicate value criterion");
            _ = R.push(v_ids__65474, v_id__65567);
            if (R.Bool(F_dv_get(v_cell__65597, "known")))
            {
                object v_local_1__65769 = false;
                if (R.Bool(v_subjective))
                {
                    v_local_1__65769 = F_dv_expected_measure(v_cell__65597, v_criterion__65531);
                }
                else
                {
                    v_local_1__65769 = F_dv_measure(F_dv_get(v_cell__65597, "value"), v_criterion__65531);
                }
                object v_amount__65969 = v_local_1__65769;
                v_score__65414 = R.add(v_score__65414, v_amount__65969);
                _ = R.push(v_details__65432, new System.Collections.Generic.List<object>(4) { F_dv_pair("criterion", v_id__65567), F_dv_pair("contribution", v_amount__65969), F_dv_pair("confidence", F_dv_get(v_cell__65597, "confidence")), F_dv_pair("conflicted", F_dv_get(v_cell__65597, "conflicted")) });
            }
            else
            {
                _ = R.push(v_unknown__65453, v_id__65567);
            }
            v_i__65400 = R.add(v_i__65400, R.number("1"));
        }
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("score", v_score__65414), F_dv_pair("details", v_details__65432), F_dv_pair("unknown", v_unknown__65453), F_dv_pair("complete", R.eq(R.count(v_unknown__65453), R.number("0"))) }, new System.Collections.Generic.List<object>(3) { F_dv_pair("world", F_dv_get(v_world, "id")), F_dv_pair("revision", F_dv_get(v_world, "revision")), F_dv_pair("subject", F_dv_get(v_world, "subject")) });
    }

    public static object F_dv_value_subjective(object v_mind, object v_criteria)
    {
        _ = F_dv_require(F_dv_or(R.eq(F_dv_get(v_mind, "domain"), "mind"), R.eq(F_dv_get(v_mind, "domain"), "prediction")), "Subjective value requires a mind or prediction");
        return F_dv_value(v_mind, v_criteria, true);
    }

    public static object F_dv_value_objective(object v_physical, object v_criteria)
    {
        _ = F_dv_domain(v_physical, "physical");
        return F_dv_value(v_physical, v_criteria, false);
    }

    public static object F_dv_value_change(object v_before, object v_after, object v_criteria)
    {
        _ = F_dv_require(F_dv_and(R.eq(F_dv_get(v_before, "id"), F_dv_get(v_after, "id")), R.eq(F_dv_get(v_before, "subject"), F_dv_get(v_after, "subject"))), "Value comparison crosses worlds");
        object v_subjective__66929 = R.not(R.eq(F_dv_get(v_before, "domain"), "physical"));
        object v_local_1__66986 = false;
        if (R.Bool(v_subjective__66929))
        {
            v_local_1__66986 = F_dv_and(R.eq(F_dv_get(v_before, "domain"), "mind"), F_dv_or(R.eq(F_dv_get(v_after, "domain"), "mind"), R.eq(F_dv_get(v_after, "domain"), "prediction")));
        }
        else
        {
            v_local_1__66986 = R.eq(F_dv_get(v_after, "domain"), "physical");
        }
        _ = F_dv_require(v_local_1__66986, "Value domain mismatch");
        object v_a__67252 = F_dv_value(v_before, v_criteria, v_subjective__66929);
        object v_b__67303 = F_dv_value(v_after, v_criteria, v_subjective__66929);
        return new System.Collections.Generic.List<object>(4) { F_dv_pair("delta", R.sub(F_dv_get(v_b__67303, "score"), F_dv_get(v_a__67252, "score"))), F_dv_pair("before", v_a__67252), F_dv_pair("after", v_b__67303), F_dv_pair("complete", F_dv_and(F_dv_get(v_a__67252, "complete"), F_dv_get(v_b__67303, "complete"))) };
    }

    public static object F_dv_request(object v_id, object v_requester, object v_self)
    {
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("id", v_id), F_dv_pair("requester", v_requester), F_dv_pair("self", v_self) };
    }

    public static object F_dv_feature_refs(object v_request)
    {
        object v_speaker__67774 = F_dv_get(v_request, "requester");
        object v_self__67810 = F_dv_get(v_request, "self");
        object v_id__67838 = F_dv_get(v_request, "id");
        object v_refs__67862 = new System.Collections.Generic.List<object>(0) { };
        _ = R.push(v_refs__67862, F_dv_pair("trust", F_dv_ref(v_speaker__67774, "relationship", "trust")));
        _ = R.push(v_refs__67862, F_dv_pair("closeness", F_dv_ref(v_speaker__67774, "relationship", "closeness")));
        _ = R.push(v_refs__67862, F_dv_pair("respect", F_dv_ref(v_speaker__67774, "relationship", "respect")));
        _ = R.push(v_refs__67862, F_dv_pair("reciprocity", F_dv_ref(v_speaker__67774, "relationship", "reciprocity")));
        _ = R.push(v_refs__67862, F_dv_pair("valence", F_dv_ref(v_self__67810, "affect", "valence")));
        _ = R.push(v_refs__67862, F_dv_pair("arousal", F_dv_ref(v_self__67810, "affect", "arousal")));
        _ = R.push(v_refs__67862, F_dv_pair("fatigue", F_dv_ref(v_self__67810, "resources", "fatigue")));
        _ = R.push(v_refs__67862, F_dv_pair("stress", F_dv_ref(v_self__67810, "resources", "stress")));
        _ = R.push(v_refs__67862, F_dv_pair("attention", F_dv_ref(v_self__67810, "resources", "attention")));
        _ = R.push(v_refs__67862, F_dv_pair("threat", F_dv_ref(v_id__67838, "appraisal", "threat")));
        _ = R.push(v_refs__67862, F_dv_pair("fairness", F_dv_ref(v_id__67838, "appraisal", "fairness")));
        _ = R.push(v_refs__67862, F_dv_pair("control", F_dv_ref(v_id__67838, "appraisal", "control")));
        _ = R.push(v_refs__67862, F_dv_pair("certainty", F_dv_ref(v_id__67838, "appraisal", "certainty")));
        _ = R.push(v_refs__67862, F_dv_pair("importance", F_dv_ref(v_id__67838, "appraisal", "importance")));
        return v_refs__67862;
    }

    public static object F_dv_appraise_request(object v_mind, object v_request)
    {
        _ = F_dv_domain(v_mind, "mind");
        object v_refs__69056 = F_dv_feature_refs(v_request);
        object v_features__69096 = new System.Collections.Generic.List<object>(0) { };
        object v_unknown__69118 = new System.Collections.Generic.List<object>(0) { };
        object v_basis__69139 = new System.Collections.Generic.List<object>(0) { };
        object v_i__69158 = R.number("0");
        while (R.Bool(R.lt(v_i__69158, R.count(v_refs__69056))))
        {
            object v_entry__69208 = R.at(v_refs__69056, v_i__69158);
            object v_key__69236 = R.at(v_entry__69208, R.number("0"));
            object v_cell__69263 = F_dv_read(v_mind, R.at(v_entry__69208, R.number("1")));
            if (R.Bool(F_dv_and(F_dv_get(v_cell__69263, "known"), R.not(F_dv_get(v_cell__69263, "conflicted")))))
            {
                _ = R.push(v_features__69096, F_dv_pair(v_key__69236, F_dv_get(v_cell__69263, "value")));
                _ = R.push(v_basis__69139, new System.Collections.Generic.List<object>(3) { F_dv_pair("feature", v_key__69236), F_dv_pair("ref", R.at(v_entry__69208, R.number("1"))), F_dv_pair("cell", v_cell__69263) });
            }
            else
            {
                _ = R.push(v_unknown__69118, v_key__69236);
            }
            v_i__69158 = R.add(v_i__69158, R.number("1"));
        }
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("features", v_features__69096), F_dv_pair("unknown", v_unknown__69118), F_dv_pair("basis", v_basis__69139), F_dv_pair("revision", F_dv_get(v_mind, "revision")) }, new System.Collections.Generic.List<object>(2) { F_dv_pair("subject", F_dv_get(v_mind, "subject")), F_dv_pair("request", F_dv_get(v_request, "id")) });
    }

    public static object F_dv_dot(object v_features, object v_weights)
    {
        object v_i__69748 = R.number("0");
        object v_score__69762 = R.number("0");
        object v_terms__69780 = new System.Collections.Generic.List<object>(0) { };
        object v_unknown__69799 = new System.Collections.Generic.List<object>(0) { };
        while (R.Bool(R.lt(v_i__69748, R.count(v_weights))))
        {
            object v_entry__69859 = R.at(v_weights, v_i__69748);
            object v_key__69890 = R.at(v_entry__69859, R.number("0"));
            if (R.Bool(F_dv_has_key(v_features, v_key__69890)))
            {
                object v_contribution__69959 = R.mul(F_dv_get(v_features, v_key__69890), R.at(v_entry__69859, R.number("1")));
                v_score__69762 = R.add(v_score__69762, v_contribution__69959);
                _ = R.push(v_terms__69780, F_dv_pair(v_key__69890, v_contribution__69959));
            }
            else
            {
                _ = R.push(v_unknown__69799, v_key__69890);
            }
            v_i__69748 = R.add(v_i__69748, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("score", v_score__69762), F_dv_pair("terms", v_terms__69780), F_dv_pair("unknown", v_unknown__69799) };
    }

    public static object F_dv_clamp_signed(object v_value)
    {
        if (R.Bool(R.lt(v_value, R.sub(R.number("0"), R.number("1")))))
        {
            return R.sub(R.number("0"), R.number("1"));
        }
        else
        {
            if (R.Bool(R.lt(R.number("1"), v_value)))
            {
                return R.number("1");
            }
            else
            {
                return v_value;
            }
        }
    }

    public static object F_dv_expression(object v_appraisal, object v_expression_policy)
    {
        object v_axes__70457 = new System.Collections.Generic.List<object>(0) { };
        object v_i__70475 = R.number("0");
        while (R.Bool(R.lt(v_i__70475, R.count(v_expression_policy))))
        {
            object v_axis__70538 = R.at(v_expression_policy, v_i__70475);
            object v_evaluation__70578 = F_dv_dot(F_dv_get(v_appraisal, "features"), R.at(v_axis__70538, R.number("1")));
            _ = R.push(v_axes__70457, F_dv_pair(R.at(v_axis__70538, R.number("0")), F_dv_with_many(v_evaluation__70578, new System.Collections.Generic.List<object>(2) { F_dv_pair("value", F_dv_clamp_signed(F_dv_get(v_evaluation__70578, "score"))), F_dv_pair("complete", R.eq(R.count(F_dv_get(v_evaluation__70578, "unknown")), R.number("0"))) })));
            v_i__70475 = R.add(v_i__70475, R.number("1"));
        }
        return v_axes__70457;
    }

    public static object F_dv_no_change_prediction(object v_mind)
    {
        _ = F_dv_domain(v_mind, "mind");
        return F_dv_with_many(v_mind, new System.Collections.Generic.List<object>(2) { F_dv_pair("domain", "prediction"), F_dv_pair("source_revision", F_dv_get(v_mind, "revision")) });
    }

    public static object F_dv_response_option(object v_id, object v_intent, object v_feasible, object v_prediction, object v_effort, object v_feature_weights)
    {
        _ = F_dv_require(R.not(R.lt(v_effort, R.number("0"))), "Negative effort");
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("id", v_id), F_dv_pair("intent", v_intent), F_dv_pair("feasible", v_feasible), F_dv_pair("prediction", v_prediction) }, new System.Collections.Generic.List<object>(2) { F_dv_pair("effort", v_effort), F_dv_pair("feature_weights", v_feature_weights) });
    }

    public static object F_dv_validate_expression_policy(object v_expression_policy)
    {
        _ = F_dv_require(F_dv_record_like(v_expression_policy), "Expression policy must be an axis-to-weight-record matrix");
        object v_i__71445 = R.number("0");
        while (R.Bool(R.lt(v_i__71445, R.count(v_expression_policy))))
        {
            object v_axis__71508 = R.at(v_expression_policy, v_i__71445);
            object v_key__71548 = R.at(R.at(v_expression_policy, v_i__71445), R.number("0"));
            object v_weights__71590 = R.at(R.at(v_expression_policy, v_i__71445), R.number("1"));
            object v_j__71636 = R.number("0");
            _ = F_dv_require(R.eq(R.slice(R.json_stringify(v_key__71548), R.number("0"), R.number("1")), "\""), "Expression policy axis name must be Text");
            _ = F_dv_require(F_dv_record_like(v_weights__71590), R.cat("Expression policy axis weights must be a record: ", v_key__71548));
            while (R.Bool(R.lt(v_j__71636, R.count(v_weights__71590))))
            {
                object v_weight__71920 = R.at(v_weights__71590, v_j__71636);
                object v_feature__71956 = R.at(R.at(v_weights__71590, v_j__71636), R.number("0"));
                _ = F_dv_require(R.eq(R.slice(R.json_stringify(v_feature__71956), R.number("0"), R.number("1")), "\""), "Expression policy feature name must be Text");
                _ = F_dv_require(R.is_numeric_value(R.at(v_weight__71920, R.number("1"))), R.cat(R.cat(R.cat("Expression policy weight must be a finite Number: ", v_key__71548), "."), v_feature__71956));
                v_j__71636 = R.add(v_j__71636, R.number("1"));
            }
            v_i__71445 = R.add(v_i__71445, R.number("1"));
        }
        return true;
    }

    public static object F_dv_person_policy(object v_subject, object v_values, object v_effort_cost, object v_uncertainty_cost, object v_expression_policy)
    {
        _ = F_dv_validate_expression_policy(v_expression_policy);
        _ = F_dv_require(F_dv_and(R.not(R.lt(v_effort_cost, R.number("0"))), R.not(R.lt(v_uncertainty_cost, R.number("0")))), "Negative policy cost");
        return F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("subject", v_subject), F_dv_pair("values", v_values), F_dv_pair("effort_cost", v_effort_cost), F_dv_pair("uncertainty_cost", v_uncertainty_cost) }, "expression", v_expression_policy);
    }

    public static object F_dv_accept_policy(object v_mind, object v_id, object v_policy, object v_basis)
    {
        _ = F_dv_domain(v_mind, "mind");
        _ = F_dv_require(R.eq(F_dv_get(v_mind, "subject"), F_dv_get(v_policy, "subject")), "Foreign value policy");
        _ = F_dv_require(R.lt(R.number("0"), R.count(v_basis)), "Value policy needs an acceptance basis");
        object v_policies__72954 = F_dv_find(v_mind, "policies", new System.Collections.Generic.List<object>(0) { });
        _ = F_dv_require(R.not(F_dv_has_key(v_policies__72954, v_id)), "Duplicate accepted policy");
        return F_dv_with_many(v_mind, new System.Collections.Generic.List<object>(2) { F_dv_pair("policies", F_dv_with(v_policies__72954, v_id, new System.Collections.Generic.List<object>(3) { F_dv_pair("policy", v_policy), F_dv_pair("basis", v_basis), F_dv_pair("active", true) })), F_dv_pair("revision", R.add(F_dv_get(v_mind, "revision"), R.number("1"))) });
    }

    public static object F_dv_respond_adopted(object v_mind, object v_request, object v_options, object v_policy_id)
    {
        object v_accepted__73290 = F_dv_get(F_dv_get(v_mind, "policies"), v_policy_id);
        _ = F_dv_require(F_dv_get(v_accepted__73290, "active"), "Response policy is not adopted");
        return F_dv_with(F_dv_respond(v_mind, v_request, v_options, F_dv_get(v_accepted__73290, "policy")), "policy_basis", F_dv_get(v_accepted__73290, "basis"));
    }

    public static object F_dv_response_score(object v_mind, object v_option, object v_appraisal, object v_policy)
    {
        object v_prediction__73568 = F_dv_get(v_option, "prediction");
        _ = F_dv_domain(v_prediction__73568, "prediction");
        _ = F_dv_require(R.eq(F_dv_get(v_prediction__73568, "source_revision"), F_dv_get(v_mind, "revision")), "Stale response prediction");
        object v_value__73738 = F_dv_value_change(v_mind, v_prediction__73568, F_dv_get(v_policy, "values"));
        object v_relation_and_state__73803 = F_dv_dot(F_dv_get(v_appraisal, "features"), F_dv_get(v_option, "feature_weights"));
        object v_effort__73883 = R.mul(F_dv_get(v_option, "effort"), F_dv_get(v_policy, "effort_cost"));
        object v_uncertainty__73937 = R.mul(R.add(R.count(F_dv_get(v_relation_and_state__73803, "unknown")), R.count(F_dv_get(F_dv_get(v_value__73738, "after"), "unknown"))), F_dv_get(v_policy, "uncertainty_cost"));
        return F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("score", R.sub(R.add(F_dv_get(v_value__73738, "delta"), F_dv_get(v_relation_and_state__73803, "score")), R.add(v_effort__73883, v_uncertainty__73937))), F_dv_pair("value", v_value__73738), F_dv_pair("relationship_and_state", v_relation_and_state__73803), F_dv_pair("effort", v_effort__73883) }, "uncertainty_cost", v_uncertainty__73937);
    }

    public static object F_dv_respond(object v_mind, object v_request, object v_options, object v_policy)
    {
        _ = F_dv_domain(v_mind, "mind");
        _ = F_dv_require(R.eq(F_dv_get(v_mind, "subject"), F_dv_get(v_policy, "subject")), "Foreign person policy");
        object v_appraisal__74420 = F_dv_appraise_request(v_mind, v_request);
        object v_i__74475 = R.number("0");
        object v_found__74489 = false;
        object v_best__74511 = R.number("0");
        object v_chosen__74528 = false;
        object v_evaluated__74551 = new System.Collections.Generic.List<object>(0) { };
        object v_rejected__74574 = new System.Collections.Generic.List<object>(0) { };
        object v_ids__74596 = new System.Collections.Generic.List<object>(0) { };
        while (R.Bool(R.lt(v_i__74475, R.count(v_options))))
        {
            object v_option__74652 = R.at(v_options, v_i__74475);
            object v_id__74684 = F_dv_get(v_option__74652, "id");
            object v_legal__74711 = F_dv_truth(v_mind, F_dv_get(v_option__74652, "feasible"));
            _ = F_dv_require(R.not(F_dv_has(v_ids__74596, v_id__74684)), "Duplicate response identity");
            _ = R.push(v_ids__74596, v_id__74684);
            if (R.Bool(R.eq(v_legal__74711, "true")))
            {
                object v_evaluation__74895 = F_dv_response_score(v_mind, v_option__74652, v_appraisal__74420, v_policy);
                _ = R.push(v_evaluated__74551, F_dv_pair(v_id__74684, v_evaluation__74895));
                object v_local_1__75031 = false;
                if (R.Bool(v_found__74489))
                {
                    v_local_1__75031 = R.lt(v_best__74511, F_dv_get(v_evaluation__74895, "score"));
                }
                else
                {
                    v_local_1__75031 = true;
                }
                if (R.Bool(v_local_1__75031))
                {
                    v_found__74489 = true;
                    v_best__74511 = F_dv_get(v_evaluation__74895, "score");
                    v_chosen__74528 = v_option__74652;
                }
            }
            else
            {
                _ = R.push(v_rejected__74574, F_dv_pair(v_id__74684, v_legal__74711));
            }
            v_i__74475 = R.add(v_i__74475, R.number("1"));
        }
        object v_local_2__75387 = false;
        if (R.Bool(v_found__74489))
        {
            v_local_2__75387 = "selected";
        }
        else
        {
            v_local_2__75387 = "no-response";
        }
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("status", v_local_2__75387), F_dv_pair("choice", v_chosen__74528), F_dv_pair("appraisal", v_appraisal__74420), F_dv_pair("evaluated", v_evaluated__74551) }, new System.Collections.Generic.List<object>(3) { F_dv_pair("rejected", v_rejected__74574), F_dv_pair("expression", F_dv_expression(v_appraisal__74420, F_dv_get(v_policy, "expression"))), F_dv_pair("committed", false) });
    }
}
