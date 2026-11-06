// @mechanism: perception_and_erosion
// @layer: core/cognition
// @contracts: PersonalPolicy

using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using DuelView.Runtime;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;
using System;
using static DuelView.Compiled.R;
using static DuelView.Core.DVCognitionKernel;
using static DuelView.Core.DVKernel;

// --- Source: PersonalPolicy.cs ---
namespace DuelView.Core
{


/// <summary>Planning / PersonalPolicy: native Core mechanisms.</summary>
public static partial class DVCognitionKernel
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
        if (!CognitionCaseStrategyBridge.Ready(v_actor, v_mind, v_action)) return false;
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
}

// --- Source: PlayerControl.cs ---
namespace DuelView.Core
{


/// <summary>Session / PlayerControl: native Core mechanisms.</summary>
public static partial class DVCognitionKernel
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
}


namespace DuelView.Core
{
    internal static class CognitionCaseStrategyBridge
    {
        public static Func<object, object, object, bool>? ReadyHook { get; set; }
        public static bool Ready(object actor, object mind, object action) => ReadyHook != null ? ReadyHook(actor, mind, action) : true;
    }
}
