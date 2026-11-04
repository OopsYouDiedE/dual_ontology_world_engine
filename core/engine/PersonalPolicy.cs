using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Planning / PersonalPolicy: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvp_personal_ready(object v_mind, object v_choice)
    {
        return (R.Bool((R.Bool(R.lt(R.number("0"), R.count(F_dv_find(v_choice, "basis", new System.Collections.Generic.List<object>(0) { })))) ? (object)(R.eq(F_dv_truth(v_mind, F_dv_get(v_choice, "requires")), "true")) : (object)(false))) ? (object)(R.eq(F_dv_truth(v_mind, F_dv_get(v_choice, "prohibits")), "false")) : (object)(false));
    }

    public static object F_dvp_personal_impact(object v_choice, object v_value)
    {
        object v_impact__29649 = R.number("0");
        if (R.Bool(F_dv_has(F_dv_get(v_choice, "promotes"), v_value)))
        {
            v_impact__29649 = R.add(v_impact__29649, R.number("1"));
        }
        if (R.Bool(F_dv_has(F_dv_get(v_choice, "sacrifices"), v_value)))
        {
            v_impact__29649 = R.sub(v_impact__29649, R.number("1"));
        }
        return v_impact__29649;
    }

    public static object F_dvp_personal_better(object v_order, object v_left, object v_right)
    {
        object v_i__29867 = R.number("0");
        while (R.Bool(R.lt(v_i__29867, R.count(v_order))))
        {
            object v_a__29918 = F_dvp_personal_impact(v_left, R.at(v_order, v_i__29867));
            object v_b__29970 = F_dvp_personal_impact(v_right, R.at(v_order, v_i__29867));
            if (R.Bool(R.not(R.eq(v_a__29918, v_b__29970))))
            {
                return R.lt(v_b__29970, v_a__29918);
            }
            v_i__29867 = R.add(v_i__29867, R.number("1"));
        }
        return false;
    }

    public static object F_dvp_received_speech_matches(object v_receipts, object v_requirement)
    {
        _ = F_dv_require(F_dv_record_like(v_requirement), "Received speech requirement must be a record");
        object v_i__30248 = R.number("0");
        while (R.Bool(R.lt(v_i__30248, R.count(v_receipts))))
        {
            object v_receipt__30300 = R.at(v_receipts, v_i__30248);
            if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_receipt__30300, "speaker"), F_dv_get(v_requirement, "speaker"))) ? (object)(R.eq(F_dv_get(v_receipt__30300, "action_id"), F_dv_get(v_requirement, "action_id"))) : (object)(false))) ? (object)(R.not(R.eq(F_dv_get(v_receipt__30300, "parse_event"), ""))) : (object)(false))) ? (object)(R.not(R.eq(F_dv_get(v_receipt__30300, "source_event"), ""))) : (object)(false))))
            {
                return true;
            }
            v_i__30248 = R.add(v_i__30248, R.number("1"));
        }
        return false;
    }

    public static object F_dvp_owned_request_ready(object actor, object action)
    {
        object requirement = F_dv_find(action, "owned_request_requirement", false);
        if (R.Bool(R.eq(requirement, false))) return true;
        object task = new List<object> { F_dv_pair("task_id", F_dv_get(action, "id")), F_dv_pair("request_context", requirement) };
        return R.eq(F_dv_get(F_dvs_navigation_request_status(actor, task), "status"), "valid");
    }

    public static object F_dvp_owner_requirements(object v_actor, object v_action, object v_mind)
    {
        if (!CaseStrategy.Ready(v_actor, v_mind, v_action)) return false;
        if (R.Bool(R.not(F_dvs_actor_can_act(v_actor))))
        {
            return false;
        }
        if (R.Bool(R.not(F_dvp_owned_decision_ready(v_actor, v_action, v_mind))))
        {
            return false;
        }
        if (!R.Bool(F_dvp_owned_request_ready(v_actor, v_action))) return false;
        if (R.Bool(R.not(F_dvs_advice_decision_ready(v_actor, v_action, v_mind))))
        {
            return false;
        }
        object v_resonance__30814 = F_dv_find(v_action, "resonance", false);
        if (R.Bool(R.not(R.eq(v_resonance__30814, false))))
        {
            _ = F_dv_require(F_dv_record_like(v_resonance__30814), "Resonance must be a record");
            if (R.Bool(R.not(F_dv_find(v_resonance__30814, "passive", false))))
            {
                if (R.Bool(R.not(R.lt(R.number("0"), F_dvp_resonance_utility(v_actor, v_action, v_mind)))))
                {
                    return false;
                }
            }
        }
        object v_night_call__31124 = F_dv_find(v_action, "night_call_requirement", false);
        if (R.Bool(R.not(R.eq(v_night_call__31124, false))))
        {
            _ = F_dv_require(F_dv_record_like(v_night_call__31124), "Night call requirement must be a record");
            if (R.Bool((R.Bool((R.Bool(R.not(F_dv_find(v_actor, "night_call_eligible", false))) ? (object)(true) : (object)(R.eq(R.count(F_dv_find(v_night_call__31124, "basis", new System.Collections.Generic.List<object>(0) { })), R.number("0"))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_truth(v_mind, F_dv_get(v_night_call__31124, "night_when")), "true"))))))
            {
                return false;
            }
        }
        object v_conflict__31500 = F_dv_find(v_action, "received_claim_conflict", false);
        if (R.Bool(R.not(R.eq(v_conflict__31500, false))))
        {
            _ = F_dv_require(F_dv_record_like(v_conflict__31500), "Received claim conflict must be a record");
            object v_conflicts__31688 = F_dv_find(v_actor, "observed_claim_conflicts", new System.Collections.Generic.List<object>(0) { });
            object v_found_conflict__31759 = false;
            object v_conflict_index__31794 = R.number("0");
            while (R.Bool(R.lt(v_conflict_index__31794, R.count(v_conflicts__31688))))
            {
                object v_evidence__31881 = R.at(v_conflicts__31688, v_conflict_index__31794);
                _ = F_dv_require(F_dv_record_like(v_evidence__31881), "Observed claim conflict must be a record");
                if (R.Bool((R.Bool(R.eq(F_dv_get(v_evidence__31881, "key"), F_dv_get(v_conflict__31500, "key"))) ? (object)(R.eq(F_dv_get(v_evidence__31881, "speaker"), F_dv_get(v_conflict__31500, "speaker"))) : (object)(false))))
                {
                    if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_evidence__31881, "source_event", ""), ""))) ? (object)(R.not(R.eq(F_dv_find(v_evidence__31881, "previous_source", ""), ""))) : (object)(false))) ? (object)(R.not(R.eq(F_dv_find(v_evidence__31881, "parse_event", ""), ""))) : (object)(false))) ? (object)(R.lt(R.number("0"), R.count(F_dv_find(v_evidence__31881, "basis", new System.Collections.Generic.List<object>(0) { })))) : (object)(false))))
                    {
                        v_found_conflict__31759 = true;
                    }
                }
                v_conflict_index__31794 = R.add(v_conflict_index__31794, R.number("1"));
            }
            if (R.Bool(R.not(v_found_conflict__31759)))
            {
                return false;
            }
        }
        object v_opportunity__32462 = F_dv_find(v_action, "opportunity_requirement", false);
        if (R.Bool(R.not(R.eq(v_opportunity__32462, false))))
        {
            _ = F_dv_require(F_dv_record_like(v_opportunity__32462), "Opportunity requirement must be a record");
            _ = F_dv_require(F_dv_has(new System.Collections.Generic.List<object>(3) { "all-present", "at-least-one-present", "none-present" }, F_dv_get(v_opportunity__32462, "mode")), "Unknown opportunity mode");
            object v_own__32789 = F_dv_read(v_mind, F_dv_get(v_actor, "location_ref"));
            if (R.Bool((R.Bool(R.not(F_dv_uncontested(v_own__32789))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_own__32789, "value"), F_dv_get(v_opportunity__32462, "location")))))))
            {
                return false;
            }
            object v_context__32939 = F_dv_find(v_actor, "visible_context", new System.Collections.Generic.List<object>(0) { });
            object v_present__32999 = R.number("0");
            object v_i__33023 = R.number("0");
            while (R.Bool(R.lt(v_i__33023, R.count(F_dv_get(v_opportunity__32462, "participants")))))
            {
                object v_id__33099 = R.at(F_dv_get(v_opportunity__32462, "participants"), v_i__33023);
                object v_peer__33148 = F_dv_find(v_context__32939, v_id__33099, false);
                if (R.Bool(R.eq(v_id__33099, F_dv_get(v_actor, "id"))))
                {
                    v_present__32999 = R.add(v_present__32999, R.number("1"));
                }
                else
                {
                    if (R.Bool(R.not(R.eq(v_peer__33148, false))))
                    {
                        if (R.Bool(R.eq(F_dv_get(v_peer__33148, "location"), F_dv_get(v_opportunity__32462, "location"))))
                        {
                            v_present__32999 = R.add(v_present__32999, R.number("1"));
                        }
                    }
                }
                v_i__33023 = R.add(v_i__33023, R.number("1"));
            }
            if (R.Bool((R.Bool(R.eq(F_dv_get(v_opportunity__32462, "mode"), "all-present")) ? (object)(R.not(R.eq(v_present__32999, R.count(F_dv_get(v_opportunity__32462, "participants"))))) : (object)(false))))
            {
                return false;
            }
            if (R.Bool((R.Bool(R.eq(F_dv_get(v_opportunity__32462, "mode"), "at-least-one-present")) ? (object)(R.eq(v_present__32999, R.number("0"))) : (object)(false))))
            {
                return false;
            }
            if (R.Bool(R.eq(F_dv_get(v_opportunity__32462, "mode"), "none-present")))
            {
                if (R.Bool((R.Bool((R.Bool(R.not(R.eq(v_present__32999, R.number("0")))) ? (object)(true) : (object)(R.not(F_dv_find(v_actor, "visible_context_clear", false))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_actor, "visible_context_location", false), F_dv_get(v_own__32789, "value")))))))
                {
                    return false;
                }
            }
        }
        object v_heard__33975 = F_dv_find(v_action, "received_speech_requirement", false);
        if (R.Bool(R.not(R.eq(v_heard__33975, false))))
        {
            _ = F_dv_require(F_dv_record_like(v_heard__33975), "Received speech requirement must be a record");
            object v_receipts__34162 = F_dv_find(v_actor, "received_speeches", new System.Collections.Generic.List<object>(0) { });
            object v_found__34225 = false;
            if (R.Bool(F_dv_has_key(v_heard__33975, "any_of")))
            {
                _ = F_dv_require(R.is_list(F_dv_get(v_heard__33975, "any_of")), "Received speech any_of must be a list");
                object v_i__34385 = R.number("0");
                while (R.Bool(R.lt(v_i__34385, R.count(F_dv_get(v_heard__33975, "any_of")))))
                {
                    if (R.Bool(F_dvp_received_speech_matches(v_receipts__34162, R.at(F_dv_get(v_heard__33975, "any_of"), v_i__34385))))
                    {
                        v_found__34225 = true;
                    }
                    v_i__34385 = R.add(v_i__34385, R.number("1"));
                }
            }
            else
            {
                v_found__34225 = F_dvp_received_speech_matches(v_receipts__34162, v_heard__33975);
            }
            if (R.Bool(R.not(v_found__34225)))
            {
                return false;
            }
        }
        if (R.Bool((R.Bool(F_dv_find(v_action, "response_once", false)) ? (object)(F_dv_has(F_dv_find(v_actor, "completed_actions", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_action, "id"))) : (object)(false))))
        {
            return false;
        }
        object v_superseded__34837 = F_dv_find(v_action, "suppress_after_received_speech", false);
        if (R.Bool(R.not(R.eq(v_superseded__34837, false))))
        {
            _ = F_dv_require(F_dv_record_like(v_superseded__34837), "Suppressing speech requirement must be a record");
            if (R.Bool(F_dvp_received_speech_matches(F_dv_find(v_actor, "received_speeches", new System.Collections.Generic.List<object>(0) { }), v_superseded__34837)))
            {
                return false;
            }
        }
        object v_completed__35159 = F_dv_find(v_action, "completed_action_requirement", false);
        if (R.Bool(R.not(R.eq(v_completed__35159, false))))
        {
            _ = F_dv_require(F_dv_record_like(v_completed__35159), "Completed action requirement must be a record");
            if (R.Bool(R.not(F_dv_has(F_dv_find(v_actor, "completed_actions", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_completed__35159, "action_id")))))
            {
                return false;
            }
        }
        object v_desire__35466 = F_dv_find(v_action, "desire_requirement", false);
        if (R.Bool(R.not(R.eq(v_desire__35466, false))))
        {
            _ = F_dv_require(F_dv_record_like(v_desire__35466), "Desire requirement must be a record");
            if (R.Bool(R.not(R.eq(F_dv_get(v_desire__35466, "subject"), F_dv_get(v_actor, "id")))))
            {
                return false;
            }
            object v_desires__35702 = F_dv_find(F_dv_get(v_actor, "policy"), "desires", new System.Collections.Generic.List<object>(0) { });
            object v_found__35761 = false;
            object v_i__35787 = R.number("0");
            while (R.Bool(R.lt(v_i__35787, R.count(v_desires__35702))))
            {
                if (R.Bool((R.Bool(R.eq(F_dv_get(R.at(v_desires__35702, v_i__35787), "id"), F_dv_get(v_desire__35466, "desire_id"))) ? (object)(R.lt(R.number("0"), R.count(F_dv_find(R.at(v_desires__35702, v_i__35787), "basis", new System.Collections.Generic.List<object>(0) { })))) : (object)(false))))
                {
                    v_found__35761 = true;
                }
                v_i__35787 = R.add(v_i__35787, R.number("1"));
            }
            if (R.Bool(R.not(v_found__35761)))
            {
                return false;
            }
        }
        return true;
    }

    public static object F_dvp_eligible(object v_actor, object v_action, object v_mind, object v_relevant)
    {
        if (R.Bool(R.not(F_dvp_eligible_base(v_actor, v_action, v_mind, v_relevant))))
        {
            return false;
        }
        return F_dvp_eligible_after_base(v_actor, v_action, v_mind, v_relevant);
    }

    public static object F_dvp_owner_interpretation_ready(object v_actor, object v_action)
    {
        object v_attitude__36303 = F_dv_find(v_action, "owner_interpretation_requirement", false);
        if (R.Bool(R.not(R.eq(v_attitude__36303, false))))
        {
            _ = F_dv_require(F_dv_record_like(v_attitude__36303), "Owner interpretation requirement must be a record");
            object v_interpretation__36509 = F_dv_find(F_dv_find(v_actor, "interpretations", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_attitude__36303, "interpretation_id"), false);
            if (R.Bool(R.eq(v_interpretation__36509, false)))
            {
                return false;
            }
            _ = F_dv_require(F_dv_record_like(v_interpretation__36509), "Owner interpretation must be a record");
            if (R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_attitude__36303, "subject"), F_dv_get(v_actor, "id")))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_interpretation__36509, "status"), F_dv_get(v_attitude__36303, "required_status")))))) ? (object)(true) : (object)(R.eq(F_dv_find(v_interpretation__36509, "source_event", ""), "")))))
            {
                return false;
            }
        }
        return true;
    }

    public static object F_dvp_personal_admissible(object v_actor, object v_action, object v_mind)
    {
        if (R.Bool(R.not(F_dv_has_key(v_action, "personal_choice"))))
        {
            return true;
        }
        object v_choice__37241 = F_dv_get(v_action, "personal_choice");
        if (R.Bool(R.not(F_dvp_personal_ready(v_mind, v_choice__37241))))
        {
            return false;
        }
        object v_order__37347 = F_dv_find(F_dv_get(v_actor, "policy"), "value_order", new System.Collections.Generic.List<object>(0) { });
        if (R.Bool((R.Bool(R.eq(R.count(v_order__37347), R.number("0"))) ? (object)(true) : (object)(R.eq(R.count(F_dv_find(F_dv_get(v_actor, "policy"), "value_basis", new System.Collections.Generic.List<object>(0) { })), R.number("0"))))))
        {
            return false;
        }
        if (R.Bool(F_dv_find(v_choice__37241, "require_positive", false)))
        {
            object v_i__37568 = R.number("0");
            object v_first__37586 = R.number("0");
            while (R.Bool((R.Bool(R.lt(v_i__37568, R.count(v_order__37347))) ? (object)(R.eq(v_first__37586, R.number("0"))) : (object)(false))))
            {
                v_first__37586 = F_dvp_personal_impact(v_choice__37241, R.at(v_order__37347, v_i__37568));
                v_i__37568 = R.add(v_i__37568, R.number("1"));
            }
            if (R.Bool(R.not(R.lt(R.number("0"), v_first__37586))))
            {
                return false;
            }
        }
        return true;
    }

    public static object F_dvp_eligible_after_base(object v_actor, object v_action, object v_mind, object v_relevant)
    {
        if (R.Bool(R.not(F_dvp_owner_requirements(v_actor, v_action, v_mind))))
        {
            return false;
        }
        if (R.Bool(R.not(F_dvp_normative_ready(v_actor, v_action, v_mind))))
        {
            return false;
        }
        if (R.Bool(R.not(F_dvp_owner_interpretation_ready(v_actor, v_action))))
        {
            return false;
        }
        if (R.Bool(R.not(F_dv_has_key(v_action, "personal_choice"))))
        {
            return true;
        }
        object v_choice__38160 = F_dv_get(v_action, "personal_choice");
        if (R.Bool(R.not(F_dvp_personal_admissible(v_actor, v_action, v_mind))))
        {
            return false;
        }
        object v_order__38278 = F_dv_find(F_dv_get(v_actor, "policy"), "value_order", new System.Collections.Generic.List<object>(0) { });
        object v_alternatives__38335 = F_dv_get(v_actor, "actions");
        if (R.Bool(F_dv_has_key(v_actor, "planning_choice_groups")))
        {
            v_alternatives__38335 = F_dv_find(F_dv_get(v_actor, "planning_choice_groups"), F_dv_get(v_choice__38160, "group"), new System.Collections.Generic.List<object>(0) { });
        }
        object v_i__38503 = R.number("0");
        while (R.Bool(R.lt(v_i__38503, R.count(v_alternatives__38335))))
        {
            object v_other__38561 = R.at(v_alternatives__38335, v_i__38503);
            if (R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_other__38561, "id"), F_dv_get(v_action, "id")))) ? (object)(F_dv_has_key(v_other__38561, "personal_choice")) : (object)(false))))
            {
                if (R.Bool((R.Bool(R.eq(F_dv_get(F_dv_get(v_other__38561, "personal_choice"), "group"), F_dv_get(v_choice__38160, "group"))) ? (object)(F_dvp_personal_admissible(v_actor, v_other__38561, v_mind)) : (object)(false))))
                {
                    if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(F_dvp_eligible_base(v_actor, v_other__38561, v_mind, v_relevant)) ? (object)(F_dvp_owner_requirements(v_actor, v_other__38561, v_mind)) : (object)(false))) ? (object)(F_dvp_normative_ready(v_actor, v_other__38561, v_mind)) : (object)(false))) ? (object)(F_dvp_owner_interpretation_ready(v_actor, v_other__38561)) : (object)(false))) ? (object)(F_dvp_personal_better(v_order__38278, F_dv_get(v_other__38561, "personal_choice"), v_choice__38160)) : (object)(false))))
                    {
                        return false;
                    }
                }
            }
            v_i__38503 = R.add(v_i__38503, R.number("1"));
        }
        return true;
    }

    public static object F_dvp_normative_ready(object v_actor, object v_action, object v_mind)
    {
        object v_decision__39243 = F_dv_find(v_action, "norm_decision", false);
        if (R.Bool(R.not(R.eq(v_decision__39243, false))))
        {
            _ = F_dv_require(F_dv_record_like(v_decision__39243), "Norm decision must be a record");
            object v_proposal__39411 = F_dv_find(F_dv_find(v_actor, "norm_proposals", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_decision__39243, "id"), false);
            if (R.Bool(R.eq(v_proposal__39411, false)))
            {
                return false;
            }
            _ = F_dv_require(F_dv_record_like(v_proposal__39411), "Received norm proposal must be a record");
            if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_find(v_proposal__39411, "source_event", ""), "")) ? (object)(true) : (object)(R.eq(F_dv_find(v_proposal__39411, "parse_event", ""), "")))) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_get(v_proposal__39411, "norm"), F_dv_get(v_decision__39243, "norm")))))))
            {
                return false;
            }
        }
        object v_private__39896 = F_dv_find(v_action, "private_execution", false);
        if (R.Bool(R.not(R.eq(v_private__39896, false))))
        {
            _ = F_dv_require(F_dv_record_like(v_private__39896), "Private execution must be a record");
            if (R.Bool((R.Bool(R.eq(R.count(F_dv_find(v_private__39896, "basis", new System.Collections.Generic.List<object>(0) { })), R.number("0"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_truth(v_mind, F_dv_get(v_private__39896, "requires")), "true"))))))
            {
                return false;
            }
            if (R.Bool(R.not(R.lt(R.number("0"), F_dvp_private_execution_utility(v_actor, v_action, v_mind)))))
            {
                return false;
            }
        }
        object v_method__40286 = F_dv_find(v_action, "normative_method", "");
        if (R.Bool(R.eq(v_method__40286, "")))
        {
            return true;
        }
        object v_rules__40384 = F_dv_find(F_dv_get(v_actor, "policy"), "norm_rules", new System.Collections.Generic.List<object>(0) { });
        object v_order__40440 = F_dv_find(F_dv_get(v_actor, "policy"), "value_order", new System.Collections.Generic.List<object>(0) { });
        object v_choice__40497 = F_dv_find(v_action, "personal_choice", new System.Collections.Generic.List<object>(2) { F_dv_pair("promotes", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("sacrifices", new System.Collections.Generic.List<object>(0) { }) });
        object v_matched__40585 = false;
        object v_applicable__40609 = new System.Collections.Generic.List<object>(0) { };
        object v_i__40633 = R.number("0");
        while (R.Bool(R.lt(v_i__40633, R.count(v_rules__40384))))
        {
            object v_rule__40682 = R.at(v_rules__40384, v_i__40633);
            if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.eq(v_method__40286, ""))) ? (object)(R.eq(F_dv_get(v_rule__40682, "method"), v_method__40286)) : (object)(false))) ? (object)(R.lt(R.number("0"), R.count(F_dv_get(v_rule__40682, "basis")))) : (object)(false))) ? (object)(R.eq(F_dv_truth(v_mind, F_dv_get(v_rule__40682, "available_when")), "true")) : (object)(false))))
            {
                _ = R.push(v_applicable__40609, v_rule__40682);
                v_matched__40585 = true;
            }
            v_i__40633 = R.add(v_i__40633, R.number("1"));
        }
        if (R.Bool(R.not(v_matched__40585)))
        {
            return true;
        }
        v_i__40633 = R.number("0");
        while (R.Bool(R.lt(v_i__40633, R.count(v_order__40440))))
        {
            object v_impact__41004 = F_dvp_personal_impact(v_choice__40497, R.at(v_order__40440, v_i__40633));
            object v_j__41063 = R.number("0");
            while (R.Bool(R.lt(v_j__41063, R.count(v_applicable__40609))))
            {
                object v_rule__41125 = R.at(v_applicable__40609, v_j__41063);
                object v_value__41162 = F_dvp_personal_impact(new System.Collections.Generic.List<object>(2) { F_dv_pair("promotes", F_dv_find(v_rule__41125, "promotes", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("sacrifices", F_dv_find(v_rule__41125, "sacrifices", new System.Collections.Generic.List<object>(0) { })) }, R.at(v_order__40440, v_i__40633));
                if (R.Bool(R.eq(F_dv_get(v_rule__41125, "mode"), "forbid")))
                {
                    v_impact__41004 = R.sub(v_impact__41004, v_value__41162);
                }
                else
                {
                    v_impact__41004 = R.add(v_impact__41004, v_value__41162);
                }
                v_j__41063 = R.add(v_j__41063, R.number("1"));
            }
            if (R.Bool(R.not(R.eq(v_impact__41004, R.number("0")))))
            {
                return R.lt(R.number("0"), v_impact__41004);
            }
            v_i__40633 = R.add(v_i__40633, R.number("1"));
        }
        return true;
    }

    public static object F_dvp_owned_quantity(object v_actor, object v_mind, object v_declaration)
    {
        if (R.Bool(R.is_numeric_value(v_declaration)))
        {
            return v_declaration;
        }
        if (R.Bool(R.eq(v_declaration, false)))
        {
            return R.number("0");
        }
        _ = F_dv_require(F_dv_record_like(v_declaration), "Owned quantity must be a number or record");
        if (R.Bool(F_dv_has_key(v_declaration, "speaker")))
        {
            return F_dvs_speaker_trust(F_dv_get(v_actor, "policy"), F_dv_get(v_declaration, "speaker"));
        }
        if (R.Bool(F_dv_has_key(v_declaration, "policy_key")))
        {
            object v_quantity__42046 = F_dv_find(F_dv_get(v_actor, "policy"), F_dv_get(v_declaration, "policy_key"), R.number("0"));
            _ = F_dv_require(R.is_numeric_value(v_quantity__42046), "Owned policy quantity must be numeric");
            return v_quantity__42046;
        }
        _ = F_dv_require(F_dv_has_key(v_declaration, "ref"), "Owned quantity needs policy_key or ref");
        _ = F_dv_require(F_dv_record_like(F_dv_get(v_declaration, "ref")), "Owned quantity ref must be a record");
        object v_cell__42405 = F_dv_read(v_mind, F_dv_get(v_declaration, "ref"));
        if (R.Bool((R.Bool(F_dv_uncontested(v_cell__42405)) ? (object)(R.is_numeric_value(F_dv_get(v_cell__42405, "value"))) : (object)(false))))
        {
            return F_dv_get(v_cell__42405, "value");
        }
        return R.number("0");
    }

    public static object F_dvp_owner_utility(object v_actor, object v_action, object v_mind)
    {
        object v_declaration__42599 = F_dv_find(v_action, "owner_utility", false);
        if (R.Bool(R.eq(v_declaration__42599, false)))
        {
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("value", R.number("0")), F_dv_pair("terms", new System.Collections.Generic.List<object>(0) { }) };
        }
        _ = F_dv_require(F_dv_record_like(v_declaration__42599), "Owner utility must be a record");
        if (R.Bool((R.Bool(R.eq(R.count(F_dv_find(v_declaration__42599, "basis", new System.Collections.Generic.List<object>(0) { })), R.number("0"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_truth(v_mind, F_dv_get(v_declaration__42599, "requires")), "true"))))))
        {
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("value", R.number("0")), F_dv_pair("terms", new System.Collections.Generic.List<object>(0) { }) };
        }
        object v_value__42957 = R.number("0");
        object v_inputs__42975 = new System.Collections.Generic.List<object>(0) { };
        object v_i__42995 = R.number("0");
        while (R.Bool(R.lt(v_i__42995, R.count(F_dv_get(v_declaration__42599, "terms")))))
        {
            object v_term__43056 = R.at(F_dv_get(v_declaration__42599, "terms"), v_i__42995);
            _ = F_dv_require(R.is_numeric_value(F_dv_get(v_term__43056, "weight")), "Owner utility weight must be numeric");
            object v_quantity__43189 = F_dvp_owned_quantity(v_actor, v_mind, F_dv_get(v_term__43056, "quantity"));
            object v_contribution__43259 = R.mul(v_quantity__43189, F_dv_get(v_term__43056, "weight"));
            v_value__42957 = R.add(v_value__42957, v_contribution__43259);
            _ = R.push(v_inputs__42975, new System.Collections.Generic.List<object>(4) { F_dv_pair("declaration", F_dv_get(v_term__43056, "quantity")), F_dv_pair("quantity", v_quantity__43189), F_dv_pair("weight", F_dv_get(v_term__43056, "weight")), F_dv_pair("contribution", v_contribution__43259) });
            v_i__42995 = R.add(v_i__42995, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("value", v_value__42957), F_dv_pair("terms", v_inputs__42975), F_dv_pair("basis", F_dv_get(v_declaration__42599, "basis")) };
    }

    public static object F_dvp_private_execution_utility(object v_actor, object v_action, object v_mind)
    {
        object v_private__43620 = F_dv_find(v_action, "private_execution", false);
        if (R.Bool(R.eq(v_private__43620, false)))
        {
            return R.number("0");
        }
        _ = F_dv_require(F_dv_record_like(v_private__43620), "Private execution must be a record");
        if (R.Bool((R.Bool(R.eq(R.count(F_dv_find(v_private__43620, "basis", new System.Collections.Generic.List<object>(0) { })), R.number("0"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_truth(v_mind, F_dv_get(v_private__43620, "requires")), "true"))))))
        {
            return R.number("0");
        }
        object v_multiplier__43918 = F_dv_find(F_dv_get(v_actor, "policy"), "private_execution_benefit_multiplier", R.number("1"));
        _ = F_dv_require(R.is_numeric_value(v_multiplier__43918), "Private benefit multiplier must be numeric");
        _ = F_dv_require(R.not(R.lt(v_multiplier__43918, R.number("0"))), "Private benefit multiplier must be nonnegative");
        return R.sub(R.mul(F_dvp_owned_quantity(v_actor, v_mind, F_dv_get(v_private__43620, "benefit")), v_multiplier__43918), F_dvp_owned_quantity(v_actor, v_mind, F_dv_get(v_private__43620, "resource_cost")));
    }

    public static object F_dvp_resonance_utility(object v_actor, object v_action, object v_mind)
    {
        object v_r__44358 = F_dv_get(v_action, "resonance");
        _ = F_dv_require(F_dv_record_like(v_r__44358), "Resonance must be a record");
        if (R.Bool((R.Bool(R.eq(R.count(F_dv_find(v_r__44358, "basis", new System.Collections.Generic.List<object>(0) { })), R.number("0"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_truth(v_mind, F_dv_find(v_r__44358, "requires", F_dv_true())), "true"))))))
        {
            return R.number("0");
        }
        object v_retrieved__44578 = F_dv_find(v_r__44358, "retrieved_when", false);
        if (R.Bool((R.Bool(R.not(R.eq(v_retrieved__44578, false))) ? (object)(R.eq(F_dv_truth(v_mind, v_retrieved__44578), "true")) : (object)(false))))
        {
            return R.number("0");
        }
        object v_drive__44718 = F_dvp_owned_quantity(v_actor, v_mind, F_dv_get(v_r__44358, "drive"));
        object v_relevance__44775 = F_dvp_owned_quantity(v_actor, v_mind, F_dv_get(v_r__44358, "relevance"));
        object v_bonus__44840 = F_dvp_owned_quantity(v_actor, v_mind, F_dv_get(v_r__44358, "truth_bonus"));
        object v_caution__44903 = F_dvp_owned_quantity(v_actor, v_mind, F_dv_get(v_r__44358, "caution"));
        object v_risk__44964 = F_dvp_owned_quantity(v_actor, v_mind, F_dv_get(v_r__44358, "perceived_risk"));
        return R.sub(R.sub(R.add(R.mul(v_drive__44718, v_relevance__44775), v_bonus__44840), v_caution__44903), v_risk__44964);
    }

    public static object F_dvp_owner_requirement_refs(object v_action)
    {
        object v_refs__45205 = new System.Collections.Generic.List<object>(0) { };
        object v_utility__45223 = F_dv_find(v_action, "owner_utility", false);
        if (R.Bool(R.not(R.eq(v_utility__45223, false))))
        {
            v_refs__45205 = F_dvp_merge_refs(v_refs__45205, F_dvs_sim_refs(F_dv_get(v_utility__45223, "requires")));
            object v_ui__45378 = R.number("0");
            while (R.Bool(R.lt(v_ui__45378, R.count(F_dv_get(v_utility__45223, "terms")))))
            {
                object v_quantity__45445 = F_dv_get(R.at(F_dv_get(v_utility__45223, "terms"), v_ui__45378), "quantity");
                if (R.Bool((R.Bool(R.not(R.eq(v_quantity__45445, false))) ? (object)(R.not(R.is_numeric_value(v_quantity__45445))) : (object)(false))))
                {
                    if (R.Bool(F_dv_has_key(v_quantity__45445, "ref")))
                    {
                        v_refs__45205 = F_dvp_add_ref(v_refs__45205, F_dv_get(v_quantity__45445, "ref"));
                    }
                }
                v_ui__45378 = R.add(v_ui__45378, R.number("1"));
            }
        }
        object v_private__45694 = F_dv_find(v_action, "private_execution", false);
        if (R.Bool(R.not(R.eq(v_private__45694, false))))
        {
            _ = F_dv_require(F_dv_record_like(v_private__45694), "Private execution must be a record");
            v_refs__45205 = F_dvp_merge_refs(v_refs__45205, F_dvs_sim_refs(F_dv_get(v_private__45694, "requires")));
            object v_quantities__45935 = new System.Collections.Generic.List<object>(2) { F_dv_get(v_private__45694, "benefit"), F_dv_get(v_private__45694, "resource_cost") };
            object v_pi__46001 = R.number("0");
            while (R.Bool(R.lt(v_pi__46001, R.count(v_quantities__45935))))
            {
                object v_quantity__46065 = R.at(v_quantities__45935, v_pi__46001);
                if (R.Bool((R.Bool(R.not(R.eq(v_quantity__46065, false))) ? (object)(R.not(R.is_numeric_value(v_quantity__46065))) : (object)(false))))
                {
                    _ = F_dv_require(F_dv_record_like(v_quantity__46065), "Owned quantity must be a number or record");
                    if (R.Bool((R.Bool(R.not(F_dv_has_key(v_quantity__46065, "policy_key"))) ? (object)(R.not(F_dv_has_key(v_quantity__46065, "speaker"))) : (object)(false))))
                    {
                        _ = F_dv_require(F_dv_has_key(v_quantity__46065, "ref"), "Owned quantity needs policy_key or ref");
                        _ = F_dv_require(F_dv_record_like(F_dv_get(v_quantity__46065, "ref")), "Owned quantity ref must be a record");
                        v_refs__45205 = F_dvp_add_ref(v_refs__45205, F_dv_get(v_quantity__46065, "ref"));
                    }
                }
                v_pi__46001 = R.add(v_pi__46001, R.number("1"));
            }
        }
        object v_r__46653 = F_dv_find(v_action, "resonance", false);
        if (R.Bool(R.not(R.eq(v_r__46653, false))))
        {
            _ = F_dv_require(F_dv_record_like(v_r__46653), "Resonance must be a record");
            if (R.Bool(R.not(F_dv_find(v_r__46653, "passive", false))))
            {
                v_refs__45205 = F_dvp_merge_refs(v_refs__45205, F_dvs_sim_refs(F_dv_find(v_r__46653, "requires", F_dv_true())));
                object v_retrieved__46930 = F_dv_find(v_r__46653, "retrieved_when", false);
                if (R.Bool(R.not(R.eq(v_retrieved__46930, false))))
                {
                    v_refs__45205 = F_dvp_merge_refs(v_refs__45205, F_dvs_sim_refs(v_retrieved__46930));
                }
                object v_quantities__47098 = new System.Collections.Generic.List<object>(5) { F_dv_get(v_r__46653, "drive"), F_dv_get(v_r__46653, "relevance"), F_dv_get(v_r__46653, "truth_bonus"), F_dv_get(v_r__46653, "caution"), F_dv_get(v_r__46653, "perceived_risk") };
                object v_i__47194 = R.number("0");
                while (R.Bool(R.lt(v_i__47194, R.count(v_quantities__47098))))
                {
                    object v_quantity__47264 = R.at(v_quantities__47098, v_i__47194);
                    if (R.Bool((R.Bool(R.not(R.eq(v_quantity__47264, false))) ? (object)(R.not(R.is_numeric_value(v_quantity__47264))) : (object)(false))))
                    {
                        _ = F_dv_require(F_dv_record_like(v_quantity__47264), "Owned quantity must be a number or record");
                        if (R.Bool((R.Bool(R.not(F_dv_has_key(v_quantity__47264, "policy_key"))) ? (object)(R.not(F_dv_has_key(v_quantity__47264, "speaker"))) : (object)(false))))
                        {
                            _ = F_dv_require(F_dv_has_key(v_quantity__47264, "ref"), "Owned quantity needs policy_key or ref");
                            _ = F_dv_require(F_dv_record_like(F_dv_get(v_quantity__47264, "ref")), "Owned quantity ref must be a record");
                            v_refs__45205 = F_dvp_add_ref(v_refs__45205, F_dv_get(v_quantity__47264, "ref"));
                        }
                    }
                    v_i__47194 = R.add(v_i__47194, R.number("1"));
                }
            }
        }
        object v_night__47877 = F_dv_find(v_action, "night_call_requirement", false);
        if (R.Bool(R.not(R.eq(v_night__47877, false))))
        {
            _ = F_dv_require(F_dv_record_like(v_night__47877), "Night call requirement must be a record");
            v_refs__45205 = F_dvp_merge_refs(v_refs__45205, F_dvs_sim_refs(F_dv_get(v_night__47877, "night_when")));
        }
        object v_decision__48118 = F_dv_find(v_action, "owned_decision", false);
        if (R.Bool(R.not(R.eq(v_decision__48118, false))))
        {
            v_refs__45205 = F_dvp_merge_refs(v_refs__45205, F_dv_find(v_decision__48118, "context_refs", new System.Collections.Generic.List<object>(0) { }));
        }
        return v_refs__45205;
    }
}
