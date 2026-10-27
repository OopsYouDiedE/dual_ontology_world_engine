using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Cognition / ArgumentValidation: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_physical_proposition(object v_proposition, object v_bindings, object v_subject)
    {
        object v_op__271492 = F_dv_get(v_proposition, "op");
        if (R.Bool(R.eq(v_op__271492, "constant")))
        {
            return v_proposition;
        }
        else
        {
            if (R.Bool(R.eq(v_op__271492, "not")))
            {
                return F_dv_not(F_dvs_physical_proposition(F_dv_get(v_proposition, "item"), v_bindings, v_subject));
            }
            else
            {
                if (R.Bool(R.eq(v_op__271492, "all")))
                {
                    object v_items__271755 = F_dv_get(v_proposition, "items");
                    object v_i__271801 = R.number("0");
                    object v_mapped__271827 = new System.Collections.Generic.List<object>(0) { };
                    while (R.Bool(R.lt(v_i__271801, R.count(v_items__271755))))
                    {
                        _ = R.push(v_mapped__271827, F_dvs_physical_proposition(R.at(v_items__271755, v_i__271801), v_bindings, v_subject));
                        v_i__271801 = R.add(v_i__271801, R.number("1"));
                    }
                    return F_dv_all(v_mapped__271827);
                }
                else
                {
                    object v_ref__272083 = F_dv_get(v_proposition, "ref");
                    object v_i__272125 = R.number("0");
                    object v_physical__272151 = "";
                    while (R.Bool(R.lt(v_i__272125, R.count(v_bindings))))
                    {
                        object v_binding__272237 = R.at(v_bindings, v_i__272125);
                        if (R.Bool(F_dv_and(R.eq(F_dv_get(v_binding__272237, "subject"), v_subject), R.eq(F_dv_get(v_binding__272237, "local"), F_dv_get(v_ref__272083, "object")))))
                        {
                            v_physical__272151 = F_dv_get(v_binding__272237, "physical");
                        }
                        v_i__272125 = R.add(v_i__272125, R.number("1"));
                    }
                    if (R.Bool(R.eq(v_physical__272151, "")))
                    {
                        return new System.Collections.Generic.List<object>(2) { F_dv_pair("op", "constant"), F_dv_pair("value", "unknown") };
                    }
                    else
                    {
                        return F_dv_with(v_proposition, "ref", F_dv_ref(v_physical__272151, F_dv_get(v_ref__272083, "facet"), F_dv_get(v_ref__272083, "field")));
                    }
                }
            }
        }
    }

    public static object F_dvs_refresh_derivations(object v_actor, object v_events, object v_time, object v_parent)
    {
        object v_derivations__272721 = F_dv_find(v_actor, "inference_derivations", new System.Collections.Generic.List<object>(0) { });
        object v_world__272787 = F_dv_get(v_actor, "mind");
        object v_i__272814 = R.sub(R.count(F_dv_find(v_actor, "inference_derivations", new System.Collections.Generic.List<object>(0) { })), R.number("1"));
        object v_contaminated__272886 = new System.Collections.Generic.List<object>(0) { };
        object v_restored__272912 = new System.Collections.Generic.List<object>(0) { };
        object v_parents__272934 = F_dvs_cognitive_parents(v_actor, F_dv_single(v_parent));
        object v_remaining__273000 = F_dv_find(F_dv_get(v_actor, "policy"), "reason_budget", R.number("0"));
        object v_checked__273062 = R.number("0");
        object v_blocked__273082 = false;
        object v_updated__273106 = false;
        while (R.Bool(F_dv_and(R.not(R.lt(v_i__272814, R.number("0"))), R.not(v_blocked__273082))))
        {
            object v_derivation__273182 = R.at(v_derivations__272721, v_i__272814);
            object v_inputs__273222 = F_dv_get(R.at(v_derivations__272721, v_i__272814), "inputs");
            object v_stale__273265 = false;
            object v_j__273291 = R.number("0");
            if (R.Bool(F_dv_find(v_derivation__273182, "valid", true)))
            {
                if (R.Bool(F_dv_same(F_dv_get(v_derivation__273182, "adoption"), F_dv_find(F_dv_get(v_world__272787, "adoptions"), F_dv_get(F_dv_get(F_dv_get(v_derivation__273182, "adoption"), "law"), "name"), false))))
                {
                    _ = false;
                }
                else
                {
                    v_stale__273265 = true;
                }
                while (R.Bool(R.lt(v_j__273291, R.count(v_inputs__273222))))
                {
                    object v_input__273583 = R.at(v_inputs__273222, v_j__273291);
                    if (R.Bool(F_dv_or(F_dv_has(v_contaminated__272886, F_dv_get(v_input__273583, "ref")), R.not(F_dv_same(F_dvs_reason_cell(F_dv_get(v_input__273583, "cell")), F_dvs_reason_cell(F_dv_read(v_world__272787, F_dv_get(v_input__273583, "ref"))))))))
                    {
                        v_stale__273265 = true;
                    }
                    v_j__273291 = R.add(v_j__273291, R.number("1"));
                }
                if (R.Bool(v_stale__273265))
                {
                    if (R.Bool(R.lt(v_remaining__273000, R.number("1"))))
                    {
                        v_blocked__273082 = true;
                    }
                    else
                    {
                        object v_changes__273948 = F_dv_get(v_derivation__273182, "changes");
                        object v_k__274001 = R.number("0");
                        v_remaining__273000 = R.sub(v_remaining__273000, R.number("1"));
                        v_checked__273062 = R.add(v_checked__273062, R.number("1"));
                        v_parents__272934 = F_dv_explanation_merge(v_parents__272934, F_dvs_support_parents(F_dv_get(v_actor, "mind"), F_dvs_sim_refs(F_dv_get(F_dv_get(F_dv_get(v_derivation__273182, "adoption"), "law"), "premise")), v_events, v_actor));
                        while (R.Bool(R.lt(v_k__274001, R.count(v_changes__273948))))
                        {
                            object v_change__274333 = R.at(v_changes__273948, v_k__274001);
                            if (R.Bool(F_dv_same(F_dv_read(v_world__272787, F_dv_get(v_change__274333, "ref")), F_dv_get(v_change__274333, "after"))))
                            {
                                v_world__272787 = F_dv_store(v_world__272787, F_dv_get(v_change__274333, "ref"), F_dv_get(v_change__274333, "before"));
                                v_contaminated__272886 = F_dvp_add_ref(v_contaminated__272886, F_dv_get(v_change__274333, "ref"));
                                _ = R.push(v_restored__272912, v_change__274333);
                            }
                            v_k__274001 = R.add(v_k__274001, R.number("1"));
                        }
                        v_parents__272934 = F_dv_explanation_merge(v_parents__272934, F_dv_single(F_dv_get(v_derivation__273182, "event")));
                        v_derivations__272721 = F_dvs_replace(v_derivations__272721, v_i__272814, F_dv_with(v_derivation__273182, "valid", false));
                    }
                }
            }
            v_i__272814 = R.sub(v_i__272814, R.number("1"));
        }
        if (R.Bool(R.lt(R.number("0"), R.count(v_restored__272912))))
        {
            v_world__272787 = F_dv_with(v_world__272787, "revision", R.add(F_dv_get(v_world__272787, "revision"), R.number("1")));
        }
        v_updated__273106 = F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(4) { F_dv_pair("mind", v_world__272787), F_dv_pair("inference_derivations", v_derivations__272721), F_dv_pair("cognition_blocked", v_blocked__273082), F_dv_pair("policy", F_dv_with(F_dv_get(v_actor, "policy"), "reason_budget", v_remaining__273000)) });
        if (R.Bool(F_dv_or(v_blocked__273082, R.lt(R.number("0"), v_checked__273062))))
        {
            v_updated__273106 = F_dv_with(v_updated__273106, "last_cognitive_event", R.cat("e", R.show(R.count(v_events))));
        }
        if (R.Bool(F_dv_or(v_blocked__273082, R.lt(R.number("0"), v_checked__273062))))
        {
            v_parent = F_dvs_event(v_events, "inference_dependencies_invalidated", F_dv_get(v_actor, "id"), v_time, v_parents__272934, F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("mind_revision_before", F_dv_get(F_dv_get(v_actor, "mind"), "revision")), F_dv_pair("mind_revision_after", F_dv_get(v_world__272787, "revision")), F_dv_pair("restored", v_restored__272912), F_dv_pair("checked", v_checked__273062) }, new System.Collections.Generic.List<object>(7) { F_dv_pair("budget_blocked", v_blocked__273082), F_dv_pair("remaining", v_remaining__273000), F_dv_pair("actor_id", F_dv_get(v_actor, "id")), F_dv_pair("actor_before", F_dv_without(v_actor, "simulations")), F_dv_pair("actor_after", F_dv_without(v_updated__273106, "simulations")), F_dv_pair("mind_before", F_dv_get(v_actor, "mind")), F_dv_pair("mind_after", v_world__272787) }));
        }
        if (R.Bool(F_dv_and(R.not(v_blocked__273082), R.lt(R.number("0"), v_checked__273062))))
        {
            return F_dvs_refresh_derivations(v_updated__273106, v_events, v_time, v_parent);
        }
        else
        {
            return v_updated__273106;
        }
    }

    public static object F_dvs_model_observed_inputs(object v_model)
    {
        object v_comparisons__276009 = F_dv_find(v_model, "comparisons", new System.Collections.Generic.List<object>(0) { });
        object v_result__276065 = new System.Collections.Generic.List<object>(0) { };
        object v_i__276085 = R.number("0");
        while (R.Bool(R.lt(v_i__276085, R.count(v_comparisons__276009))))
        {
            v_result__276065 = F_dv_explanation_merge(v_result__276065, F_dv_find(R.at(v_comparisons__276009, v_i__276085), "observed_cells", new System.Collections.Generic.List<object>(0) { }));
            v_i__276085 = R.add(v_i__276085, R.number("1"));
        }
        return v_result__276065;
    }

    public static object F_dvs_model_paid_result(object v_events, object v_id, object v_subject, object v_status)
    {
        object v_event__276325 = F_dvs_find_event_by_id(v_events, v_id);
        if (R.Bool((R.Bool((R.Bool(R.not(R.eq(v_event__276325, false))) ? (object)(R.eq(F_dv_get(v_event__276325, "actor"), v_subject)) : (object)(false))) ? (object)(R.eq(F_dv_get(v_event__276325, "type"), "model_probe_reuse")) : (object)(false))))
        {
            object v_source__276466 = F_dv_find(F_dv_get(v_event__276325, "data"), "source_event", "");
            object v_original__276527 = F_dvs_find_event_by_id(v_events, v_source__276466);
            if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(v_original__276527, false)) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_original__276527, "actor"), v_subject))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_original__276527, "type"), "mind_simulation"))))) ? (object)(true) : (object)(R.not(F_dv_has(F_dv_get(v_event__276325, "parents"), v_source__276466))))))
            {
                return false;
            }
            object v_reused__276746 = F_dv_get(F_dv_get(v_event__276325, "data"), "result");
            object v_computed__276785 = F_dv_get(F_dv_get(v_original__276527, "data"), "result");
            object v_accounting__276829 = new System.Collections.Generic.List<object>(6) { "expanded", "total_expanded", "remaining", "work_remaining", "reused_work", "reused_from_event" };
            object v_i__276952 = R.number("0");
            while (R.Bool(R.lt(v_i__276952, R.count(v_accounting__276829))))
            {
                v_reused__276746 = F_dv_without(v_reused__276746, R.at(v_accounting__276829, v_i__276952));
                v_computed__276785 = F_dv_without(v_computed__276785, R.at(v_accounting__276829, v_i__276952));
                v_i__276952 = R.add(v_i__276952, R.number("1"));
            }
            if (R.Bool(R.not(F_dv_same(v_reused__276746, v_computed__276785))))
            {
                return false;
            }
            return F_dvs_model_paid_result(v_events, v_source__276466, v_subject, v_status);
        }
        if (R.Bool((R.Bool((R.Bool(R.not(R.eq(v_event__276325, false))) ? (object)(R.eq(F_dv_get(v_event__276325, "actor"), v_subject)) : (object)(false))) ? (object)(F_dv_or(R.eq(F_dv_get(v_event__276325, "type"), "mind_simulation"), R.eq(F_dv_get(v_event__276325, "type"), "model_recheck"))) : (object)(false))))
        {
            object v_parents__277413 = F_dv_get(v_event__276325, "parents");
            object v_result__277449 = F_dv_get(F_dv_get(v_event__276325, "data"), "result");
            object v_j__277488 = R.number("0");
            while (R.Bool(R.lt(v_j__277488, R.count(v_parents__277413))))
            {
                object v_paid__277549 = F_dvs_find_event_by_id(v_events, R.at(v_parents__277413, v_j__277488));
                if (R.Bool((R.Bool((R.Bool(R.not(R.eq(v_paid__277549, false))) ? (object)(R.eq(F_dv_get(v_paid__277549, "actor"), v_subject)) : (object)(false))) ? (object)(R.eq(F_dv_get(v_paid__277549, "type"), "action_execution")) : (object)(false))))
                {
                    object v_data__277711 = F_dv_get(v_paid__277549, "data");
                    if (R.Bool(F_dv_and(R.eq(F_dv_find(F_dv_find(v_data__277711, "action", new System.Collections.Generic.List<object>(0) { }), "kind", ""), "replay"), F_dv_and(R.eq(F_dv_get(F_dv_get(F_dv_get(v_data__277711, "receipt"), "execution"), "status"), "applied"), F_dv_and(R.eq(F_dv_find(v_result__277449, "status", ""), v_status), F_dv_find(v_result__277449, "plan_complete", false))))))
                    {
                        return true;
                    }
                }
                v_j__277488 = R.add(v_j__277488, R.number("1"));
            }
        }
        return false;
    }

    public static object F_dvs_public_model_paid(object v_events, object v_id, object v_subject)
    {
        return F_dvs_model_paid_result(v_events, v_id, v_subject, "explained");
    }

    public static object F_dvs_public_argument_valid(object v_action, object v_rule, object v_events, object v_subject)
    {
        object v_argument__278221 = F_dv_find(v_action, "argument", false);
        if (R.Bool(R.eq(v_argument__278221, false)))
        {
            return false;
        }
        if (R.Bool(R.not(F_dv_has_key(v_argument__278221, "id"))))
        {
            return false;
        }
        object v_event__278386 = F_dvs_find_event_by_id(v_events, F_dv_get(v_argument__278221, "id"));
        if (R.Bool((R.Bool((R.Bool(R.eq(v_event__278386, false)) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_event__278386, "actor"), v_subject))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_event__278386, "type"), "model_argument_accepted"))))))
        {
            return false;
        }
        object v_original__278559 = F_dv_get(F_dv_get(F_dv_get(v_event__278386, "data"), "result"), "argument");
        object v_public__278605 = F_dv_without(F_dv_without(F_dv_without(v_argument__278221, "question_id"), "hypothesis_id"), "adoptions");
        if (R.Bool(R.not(F_dv_same(v_original__278559, v_public__278605))))
        {
            return false;
        }
        if (R.Bool((R.Bool(R.eq(F_dv_find(v_argument__278221, "question_id", ""), "")) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_action, "argument_question", F_dv_get(v_argument__278221, "question_id")), F_dv_get(v_argument__278221, "question_id")))))))
        {
            return false;
        }
        if (R.Bool(R.not(F_dvs_public_model_paid(v_events, F_dv_get(v_argument__278221, "model_event"), v_subject))))
        {
            return false;
        }
        object v_required__279027 = F_dv_find(v_rule, "required_argument_slots", F_dv_find(v_action, "argument_slots", new System.Collections.Generic.List<object>(0) { }));
        object v_claims__279126 = F_dv_get(v_argument__278221, "claims");
        object v_j__279159 = R.number("0");
        object v_propositions__279173 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.not(F_dv_and(R.lt(R.number("0"), R.count(v_required__279027)), F_dv_and(F_dv_get(v_argument__278221, "estimated"), F_dv_and(R.lt(R.number("0"), F_dv_get(v_argument__278221, "confidence")), F_dv_and(R.lt(F_dv_get(v_argument__278221, "confidence"), R.number("1")), F_dv_and(R.not(R.eq(F_dv_get(v_argument__278221, "model_event"), "")), R.lt(R.number("0"), R.count(F_dv_get(v_argument__278221, "source_event_ids")))))))))))
        {
            return false;
        }
        while (R.Bool(R.lt(v_j__279159, R.count(v_required__279027))))
        {
            object v_slot__279492 = R.at(v_required__279027, v_j__279159);
            object v_k__279523 = R.number("0");
            object v_found__279541 = false;
            while (R.Bool(R.lt(v_k__279523, R.count(v_claims__279126))))
            {
                object v_claim__279609 = R.at(v_claims__279126, v_k__279523);
                if (R.Bool(R.eq(F_dv_get(v_claim__279609, "slot_id"), v_slot__279492)))
                {
                    v_found__279541 = true;
                    _ = R.push(v_propositions__279173, F_dv_fact(F_dv_get(v_claim__279609, "ref"), F_dv_get(v_claim__279609, "value")));
                }
                v_k__279523 = R.add(v_k__279523, R.number("1"));
            }
            if (R.Bool(R.not(v_found__279541)))
            {
                return false;
            }
            v_j__279159 = R.add(v_j__279159, R.number("1"));
        }
        return F_dv_same(F_dv_all(v_propositions__279173), F_dv_get(v_action, "answer"));
    }

    public static object F_dvs_factor_candidate_rejected(object v_actor, object v_candidate)
    {
        object v_rejections__279977 = F_dv_find(v_actor, "factor_rejections", new System.Collections.Generic.List<object>(2) { F_dv_pair("source_revision", R.sub(R.number("0"), R.number("1"))), F_dv_pair("ids", new System.Collections.Generic.List<object>(0) { }) });
        return F_dv_and(R.eq(F_dv_get(v_rejections__279977, "source_revision"), F_dv_get(F_dv_get(v_actor, "mind"), "revision")), F_dv_has(F_dv_get(v_rejections__279977, "ids"), F_dv_get(v_candidate, "id")));
    }

    public static object F_dvs_public_literal_source(object v_events, object v_parent, object v_subject)
    {
        object v_literal__280235 = "";
        object v_i__280256 = R.sub(R.count(v_events), R.number("1"));
        while (R.Bool((R.Bool(R.not(R.lt(v_i__280256, R.number("0")))) ? (object)(R.eq(v_literal__280235, "")) : (object)(false))))
        {
            object v_event__280331 = R.at(v_events, v_i__280256);
            if (R.Bool(R.eq(F_dv_get(v_event__280331, "type"), "speech_literal")))
            {
                if (R.Bool(F_dv_has(F_dv_get(v_event__280331, "parents"), v_parent)))
                {
                    v_literal__280235 = F_dv_get(v_event__280331, "id");
                }
            }
            v_i__280256 = R.sub(v_i__280256, R.number("1"));
        }
        if (R.Bool(R.eq(v_literal__280235, "")))
        {
            return "";
        }
        object v_result__280543 = "";
        v_i__280256 = R.sub(R.count(v_events), R.number("1"));
        while (R.Bool((R.Bool(R.not(R.lt(v_i__280256, R.number("0")))) ? (object)(R.eq(v_result__280543, "")) : (object)(false))))
        {
            object v_event__280633 = R.at(v_events, v_i__280256);
            if (R.Bool(F_dv_and(R.eq(F_dv_get(v_event__280633, "type"), "listener_parse"), R.eq(F_dv_get(v_event__280633, "actor"), v_subject))))
            {
                if (R.Bool(F_dv_and(R.eq(F_dv_find(F_dv_get(v_event__280633, "data"), "source_event", ""), v_literal__280235), F_dv_has(F_dv_get(v_event__280633, "parents"), v_literal__280235))))
                {
                    v_result__280543 = v_literal__280235;
                }
            }
            v_i__280256 = R.sub(v_i__280256, R.number("1"));
        }
        return v_result__280543;
    }

    public static object F_dvs_authority_speech_allowed(object v_action, object v_physical, object v_subject)
    {
        object v_contracts__281063 = F_dv_find(v_physical, "speech_contracts", new System.Collections.Generic.List<object>(0) { });
        object v_i__281125 = R.number("0");
        object v_registered__281139 = false;
        object v_allowed__281166 = true;
        while (R.Bool(R.lt(v_i__281125, R.count(v_contracts__281063))))
        {
            object v_contract__281230 = R.at(v_contracts__281063, v_i__281125);
            if (R.Bool(R.eq(F_dv_get(v_contract__281230, "subject"), v_subject)))
            {
                v_registered__281139 = true;
                object v_adoption__281342 = F_dv_find(F_dv_get(v_physical, "adoptions"), F_dv_get(v_contract__281230, "law_id"), false);
                if (R.Bool(R.eq(v_adoption__281342, false)))
                {
                    v_allowed__281166 = false;
                }
                else
                {
                    if (R.Bool(F_dv_and(F_dv_get(v_adoption__281342, "active"), F_dv_and(R.eq(F_dv_get(v_adoption__281342, "mode"), "enforce"), F_dv_and(R.eq(F_dv_get(F_dv_get(v_adoption__281342, "law"), "version"), F_dv_get(v_contract__281230, "version")), F_dv_and(F_dv_get(v_contract__281230, "require_literal_truth"), R.lt(R.number("0"), R.count(F_dv_get(v_contract__281230, "basis")))))))))
                    {
                        _ = false;
                    }
                    else
                    {
                        v_allowed__281166 = false;
                    }
                }
                if (R.Bool(F_dv_has_key(v_action, "authority_truth_gate")))
                {
                    object v_gate__281860 = F_dv_get(v_action, "authority_truth_gate");
                    if (R.Bool(F_dv_and(F_dv_find(v_gate__281860, "required", false), R.eq(F_dv_find(v_gate__281860, "contract_id", ""), F_dv_get(v_contract__281230, "id")))))
                    {
                        _ = false;
                    }
                    else
                    {
                        v_allowed__281166 = false;
                    }
                }
                if (R.Bool(F_dv_has_key(v_action, "speech")))
                {
                    if (R.Bool(R.eq(F_dv_truth(v_physical, F_dv_get(F_dv_get(v_action, "speech"), "literal_ast")), "true")))
                    {
                        _ = false;
                    }
                    else
                    {
                        v_allowed__281166 = false;
                    }
                }
                else
                {
                    v_allowed__281166 = false;
                }
            }
            v_i__281125 = R.add(v_i__281125, R.number("1"));
        }
        if (R.Bool(F_dv_and(R.not(v_registered__281139), F_dv_has_key(v_action, "authority_truth_gate"))))
        {
            if (R.Bool(F_dv_find(F_dv_get(v_action, "authority_truth_gate"), "required", false)))
            {
                v_allowed__281166 = false;
            }
        }
        return v_allowed__281166;
    }

    public static object F_dvs_acceptance_law_ready(object v_physical, object v_name)
    {
        if (R.Bool(F_dv_has_key(F_dv_get(v_physical, "adoptions"), v_name)))
        {
            object v_adoption__282663 = F_dv_get(F_dv_get(v_physical, "adoptions"), v_name);
            object v_law__282719 = F_dv_get(F_dv_get(F_dv_get(v_physical, "adoptions"), v_name), "law");
            if (R.Bool(F_dv_and(F_dv_get(v_adoption__282663, "active"), F_dv_and(R.eq(F_dv_get(v_adoption__282663, "mode"), "enforce"), R.eq(F_dv_truth(v_physical, F_dv_get(v_law__282719, "premise")), "true")))))
            {
                return R.eq(F_dv_get(F_dv_stage(v_physical, F_dv_get(v_law__282719, "effects"), F_dv_find(v_physical, "time", R.number("0")), F_dv_get(v_adoption__282663, "basis"), F_dv_get(v_adoption__282663, "confidence")), "status"), "ready");
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }
    }

    public static object F_dvs_answer(object v_rules, object v_action, object v_physical, object v_mind, object v_bindings, object v_events)
    {
        object v_i__283162 = R.number("0");
        object v_found__283176 = false;
        object v_literal__283198 = "unknown";
        object v_support__283226 = "unknown";
        object v_topic__283254 = "unknown";
        object v_complete__283280 = "unknown";
        object v_transition__283309 = "";
        object v_grants__283333 = new System.Collections.Generic.List<object>(0) { };
        object v_recipients__283353 = new System.Collections.Generic.List<object>(0) { };
        object v_require_arg__283377 = false;
        object v_target_stage__283405 = "";
        object v_progress_ref__283431 = false;
        object v_completion_ref__283460 = false;
        while (R.Bool(R.lt(v_i__283162, R.count(v_rules))))
        {
            object v_rule__283528 = R.at(v_rules, v_i__283162);
            if (R.Bool(R.eq(F_dv_get(v_rule__283528, "id"), F_dv_find(v_action, "answer_id", ""))))
            {
                v_found__283176 = true;
                object v_local_1__283643 = false;
                if (R.Bool(F_dv_find(v_rule__283528, "condition_from_answer", false)))
                {
                    v_local_1__283643 = F_dvs_physical_proposition(F_dv_get(v_action, "answer"), v_bindings, F_dv_get(v_mind, "subject"));
                }
                else
                {
                    v_local_1__283643 = F_dv_get(v_rule__283528, "condition");
                }
                v_literal__283198 = F_dv_truth(v_physical, v_local_1__283643);
                v_support__283226 = F_dv_truth(v_mind, F_dv_get(v_rule__283528, "required_support"));
                v_transition__283309 = F_dv_find(v_rule__283528, "accepted_law", "");
                v_topic__283254 = F_dv_truth(v_physical, F_dv_find(v_rule__283528, "question_match", new System.Collections.Generic.List<object>(2) { F_dv_pair("op", "constant"), F_dv_pair("value", "unknown") }));
                v_complete__283280 = F_dv_truth(v_mind, F_dv_find(v_rule__283528, "answer_complete", new System.Collections.Generic.List<object>(2) { F_dv_pair("op", "constant"), F_dv_pair("value", "unknown") }));
                v_require_arg__283377 = F_dv_find(v_rule__283528, "require_argument", false);
                v_target_stage__283405 = F_dv_find(v_rule__283528, "stage", F_dv_find(v_action, "topic", "auxiliary"));
                v_progress_ref__283431 = F_dv_find(v_rule__283528, "progress_ref", false);
                v_completion_ref__283460 = F_dv_find(v_rule__283528, "completion_ref", false);
                if (R.Bool(v_require_arg__283377))
                {
                    if (R.Bool(F_dvs_public_argument_valid(v_action, v_rule__283528, v_events, F_dv_get(v_mind, "subject"))))
                    {
                        _ = false;
                    }
                    else
                    {
                        v_complete__283280 = "unknown";
                    }
                }
                v_grants__283333 = F_dv_find(v_rule__283528, "accepted_public_grants", new System.Collections.Generic.List<object>(0) { });
                v_recipients__283353 = F_dv_find(v_rule__283528, "accepted_listeners", new System.Collections.Generic.List<object>(0) { });
            }
            v_i__283162 = R.add(v_i__283162, R.number("1"));
        }
        object v_law_ready__284908 = F_dvs_acceptance_law_ready(v_physical, v_transition__283309);
        object v_accepted__284975 = false;
        object v_reason__285000 = "accepted";
        object v_truth_level__285028 = R.number("0");
        object v_local_3__285052 = false;
        if (R.Bool(R.not(v_found__283176)))
        {
            v_local_3__285052 = "undeclared-answer";
        }
        else
        {
            if (R.Bool(R.not(R.eq(v_topic__283254, "true"))))
            {
                v_local_3__285052 = "unasked-topic";
            }
            else
            {
                if (R.Bool(R.not(R.eq(v_literal__283198, "true"))))
                {
                    v_local_3__285052 = "literal-not-established";
                }
                else
                {
                    if (R.Bool(R.not(R.eq(v_support__283226, "true"))))
                    {
                        v_local_3__285052 = "insufficient-source";
                    }
                    else
                    {
                        if (R.Bool(R.not(R.eq(v_complete__283280, "true"))))
                        {
                            v_local_3__285052 = "incomplete-answer";
                        }
                        else
                        {
                            if (R.Bool(R.not(v_law_ready__284908)))
                            {
                                v_local_3__285052 = "protocol-transition-not-ready";
                            }
                            else
                            {
                                v_local_3__285052 = "accepted";
                            }
                        }
                    }
                }
            }
        }
        v_reason__285000 = v_local_3__285052;
        v_accepted__284975 = R.eq(v_reason__285000, "accepted");
        object v_local_4__285847 = false;
        if (R.Bool(v_accepted__284975))
        {
            if (R.Bool(F_dv_has_key(v_action, "argument")))
            {
                v_local_4__285847 = R.number("2");
            }
            else
            {
                if (R.Bool(v_require_arg__283377))
                {
                    v_local_4__285847 = R.number("2");
                }
                else
                {
                    v_local_4__285847 = R.number("1");
                }
            }
        }
        else
        {
            v_local_4__285847 = R.number("0");
        }
        v_truth_level__285028 = v_local_4__285847;
        object v_judgment__286127 = F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("accepted", v_accepted__284975), F_dv_pair("literal_truth", v_literal__283198), F_dv_pair("epistemic_support", v_support__283226), F_dv_pair("accepted_law", v_transition__283309) }, new System.Collections.Generic.List<object>(8) { F_dv_pair("accepted_public_grants", v_grants__283333), F_dv_pair("accepted_listeners", v_recipients__283353), F_dv_pair("question_match", v_topic__283254), F_dv_pair("answer_complete", v_complete__283280), F_dv_pair("law_ready", v_law_ready__284908), F_dv_pair("reason", v_reason__285000), F_dv_pair("truth_level", v_truth_level__285028), F_dv_pair("stage", v_target_stage__283405) });
        return F_dv_with_many(v_judgment__286127, new System.Collections.Generic.List<object>(2) { F_dv_pair("progress_ref", v_progress_ref__283431), F_dv_pair("completion_ref", v_completion_ref__283460) });
    }
}
