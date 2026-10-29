using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Communication / Requests: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_receive_player_influence(object v_listener, object v_speech, object v_speaker, object v_source, object v_parse_event)
    {
        object v_manipulation__64690 = F_dv_find(v_speech, "manipulation", false);
        if (R.Bool((R.Bool((R.Bool(R.eq(v_manipulation__64690, false)) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_speech, "actual_influence_actor", F_dv_find(v_speech, "actual_player_actor", "")), v_speaker))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_speech, "actual_literal_truth", "unknown"), "true"))))))
        {
            return v_listener;
        }
        if (R.Bool((R.Bool(R.not(F_dv_has(F_dv_find(v_manipulation__64690, "targets", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_listener, "id")))) ? (object)(true) : (object)(R.eq(R.count(F_dv_find(v_manipulation__64690, "basis", new System.Collections.Generic.List<object>(0) { })), R.number("0"))))))
        {
            return v_listener;
        }
        object v_receipt__65117 = new System.Collections.Generic.List<object>(7) { F_dv_pair("player", v_speaker), F_dv_pair("control", F_dv_find(v_speech, "actual_influence_control", "player")), F_dv_pair("target", F_dv_get(v_listener, "id")), F_dv_pair("method", F_dv_get(v_manipulation__64690, "method")), F_dv_pair("speech_event", v_source), F_dv_pair("parse_event", v_parse_event), F_dv_pair("declaration", v_manipulation__64690) };
        return F_dv_with(v_listener, "pending_player_influences", F_dv_append(F_dv_find(v_listener, "pending_player_influences", new System.Collections.Generic.List<object>(0) { }), v_receipt__65117));
    }

    public static object F_dvs_receive_advice(object v_listener, object v_speech, object v_speaker, object v_source, object v_parse_event, object v_literal)
    {
        object v_proposal__65648 = F_dv_find(v_speech, "advice", false);
        if (R.Bool(R.eq(v_proposal__65648, false)))
        {
            return v_listener;
        }
        if (R.Bool((R.Bool(R.not(F_dv_has(F_dv_find(v_proposal__65648, "targets", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_listener, "id")))) ? (object)(true) : (object)(R.eq(R.count(F_dv_find(v_proposal__65648, "basis", new System.Collections.Generic.List<object>(0) { })), R.number("0"))))))
        {
            return v_listener;
        }
        _ = F_dv_require((R.Bool(F_dv_has_key(v_proposal__65648, "id")) ? (object)(F_dv_has_key(v_proposal__65648, "position")) : (object)(false)), "Advice needs an identity and a recommendation position");
        object v_receipt__66027 = new System.Collections.Generic.List<object>(7) { F_dv_pair("id", F_dv_get(v_proposal__65648, "id")), F_dv_pair("speaker", v_speaker), F_dv_pair("source_event", v_source), F_dv_pair("parse_event", v_parse_event), F_dv_pair("literal", v_literal), F_dv_pair("proposal", v_proposal__65648), F_dv_pair("meaning", "recommendation-received-not-adopted-or-executed") };
        return F_dv_with_many(v_listener, new System.Collections.Generic.List<object>(2) { F_dv_pair("received_advice", F_dv_with(F_dv_find(v_listener, "received_advice", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_proposal__65648, "id"), v_receipt__66027)), F_dv_pair("advice_receipts", F_dv_append(F_dv_find(v_listener, "advice_receipts", new System.Collections.Generic.List<object>(0) { }), v_receipt__66027)) });
    }

    public static object F_dvs_advice_assessment(object v_actor, object v_receipt, object v_action_id, object v_mind)
    {
        if (R.Bool(R.not(F_dv_has(F_dv_find(F_dv_get(v_receipt, "proposal"), "proposed_action_ids", new System.Collections.Generic.List<object>(0) { }), v_action_id))))
        {
            return false;
        }
        object v_target__66776 = F_dvp_find_action(F_dv_get(v_actor, "actions"), v_action_id);
        if (R.Bool(R.eq(v_target__66776, false)))
        {
            return false;
        }
        object v_resonance__66880 = F_dv_find(v_target__66776, "resonance", false);
        if (R.Bool(R.eq(v_resonance__66880, false)))
        {
            object v_choice__66967 = F_dv_find(v_target__66776, "personal_choice", false);
            object v_order__67030 = F_dv_find(F_dv_get(v_actor, "policy"), "value_order", new System.Collections.Generic.List<object>(0) { });
            if (R.Bool((R.Bool((R.Bool(R.eq(v_choice__66967, false)) ? (object)(true) : (object)(R.eq(R.count(v_order__67030), R.number("0"))))) ? (object)(true) : (object)(R.eq(R.count(F_dv_find(F_dv_get(v_actor, "policy"), "value_basis", new System.Collections.Generic.List<object>(0) { })), R.number("0"))))))
            {
                return false;
            }
            object v_ready__67227 = (R.Bool((R.Bool(R.eq(F_dv_truth(v_mind, F_dv_get(v_target__66776, "precondition")), "true")) ? (object)(F_dvp_personal_ready(v_mind, v_choice__66967)) : (object)(false))) ? (object)(F_dvp_normative_ready(v_actor, v_target__66776, v_mind)) : (object)(false));
            object v_impact__67375 = R.number("0");
            object v_i__67398 = R.number("0");
            while (R.Bool((R.Bool(R.lt(v_i__67398, R.count(v_order__67030))) ? (object)(R.eq(v_impact__67375, R.number("0"))) : (object)(false))))
            {
                v_impact__67375 = F_dvp_personal_impact(v_choice__66967, R.at(v_order__67030, v_i__67398));
                v_i__67398 = R.add(v_i__67398, R.number("1"));
            }
            object v_accepted__67548 = false;
            if (R.Bool(R.eq(F_dv_get(F_dv_get(v_receipt, "proposal"), "position"), "consider")))
            {
                v_accepted__67548 = (R.Bool(v_ready__67227) ? (object)(R.lt(R.number("0"), v_impact__67375)) : (object)(false));
            }
            if (R.Bool(R.eq(F_dv_get(F_dv_get(v_receipt, "proposal"), "position"), "avoid")))
            {
                v_accepted__67548 = (R.Bool(v_ready__67227) ? (object)(R.not(R.lt(R.number("0"), v_impact__67375))) : (object)(false));
            }
            if (R.Bool(R.eq(F_dv_get(F_dv_get(v_receipt, "proposal"), "position"), "owner-decides")))
            {
                v_accepted__67548 = v_ready__67227;
            }
            return new System.Collections.Generic.List<object>(9) { F_dv_pair("accepted", v_accepted__67548), F_dv_pair("target_action", v_action_id), F_dv_pair("assessment_kind", "existing-owner-ordinal-values"), F_dv_pair("first_impact", v_impact__67375), F_dv_pair("requires_truth", F_dv_truth(v_mind, F_dv_get(v_target__66776, "precondition"))), F_dv_pair("basis", F_dv_find(F_dv_get(v_actor, "policy"), "value_basis", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("value_order", v_order__67030), F_dv_pair("choice", v_choice__66967), F_dv_pair("meaning", "owner-assessment-only; acceptance-does-not-execute-or-reweight-target") };
        }
        if (R.Bool(F_dv_find(v_resonance__66880, "passive", false)))
        {
            return false;
        }
        object v_requires__68287 = F_dv_find(v_resonance__66880, "requires", F_dv_true());
        object v_ready__68348 = (R.Bool(R.eq(F_dv_truth(v_mind, v_requires__68287), "true")) ? (object)(R.lt(R.number("0"), R.count(F_dv_find(v_resonance__66880, "basis", new System.Collections.Generic.List<object>(0) { })))) : (object)(false));
        object v_utility__68449 = F_dvp_resonance_utility(v_actor, v_target__66776, v_mind);
        object v_accepted__68510 = false;
        if (R.Bool(R.eq(F_dv_get(F_dv_get(v_receipt, "proposal"), "position"), "consider")))
        {
            v_accepted__68510 = (R.Bool(v_ready__68348) ? (object)(R.lt(R.number("0"), v_utility__68449)) : (object)(false));
        }
        if (R.Bool(R.eq(F_dv_get(F_dv_get(v_receipt, "proposal"), "position"), "avoid")))
        {
            v_accepted__68510 = (R.Bool(v_ready__68348) ? (object)(R.not(R.lt(R.number("0"), v_utility__68449))) : (object)(false));
        }
        if (R.Bool(R.eq(F_dv_get(F_dv_get(v_receipt, "proposal"), "position"), "owner-decides")))
        {
            v_accepted__68510 = v_ready__68348;
        }
        return new System.Collections.Generic.List<object>(8) { F_dv_pair("accepted", v_accepted__68510), F_dv_pair("target_action", v_action_id), F_dv_pair("assessment_kind", "existing-owner-resonance-utility"), F_dv_pair("utility", v_utility__68449), F_dv_pair("requires_truth", F_dv_truth(v_mind, v_requires__68287)), F_dv_pair("basis", F_dv_get(v_resonance__66880, "basis")), F_dv_pair("inputs", new System.Collections.Generic.List<object>(5) { F_dv_pair("drive", F_dvp_owned_quantity(v_actor, v_mind, F_dv_get(v_resonance__66880, "drive"))), F_dv_pair("relevance", F_dvp_owned_quantity(v_actor, v_mind, F_dv_get(v_resonance__66880, "relevance"))), F_dv_pair("truth_bonus", F_dvp_owned_quantity(v_actor, v_mind, F_dv_get(v_resonance__66880, "truth_bonus"))), F_dv_pair("caution", F_dvp_owned_quantity(v_actor, v_mind, F_dv_get(v_resonance__66880, "caution"))), F_dv_pair("perceived_risk", F_dvp_owned_quantity(v_actor, v_mind, F_dv_get(v_resonance__66880, "perceived_risk"))) }), F_dv_pair("meaning", "owner-assessment-only; acceptance-does-not-execute-or-reweight-target") };
    }

    public static object F_dvs_advice_decision_ready(object v_actor, object v_action, object v_mind)
    {
        object v_decision__69481 = F_dv_find(v_action, "advice_decision", false);
        if (R.Bool(R.eq(v_decision__69481, false)))
        {
            return true;
        }
        object v_receipt__69588 = F_dv_find(F_dv_find(v_actor, "received_advice", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_decision__69481, "advice_id"), false);
        if (R.Bool((R.Bool((R.Bool(R.eq(v_receipt__69588, false)) ? (object)(true) : (object)(R.eq(F_dv_get(v_receipt__69588, "source_event"), "")))) ? (object)(true) : (object)(R.eq(F_dv_get(v_receipt__69588, "parse_event"), "")))))
        {
            return false;
        }
        object v_stance__69785 = F_dv_find(F_dv_find(v_actor, "advice_stances", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_decision__69481, "advice_id"), false);
        if (R.Bool((R.Bool(R.not(R.eq(v_stance__69785, false))) ? (object)(R.eq(F_dv_find(v_stance__69785, "parse_event", ""), F_dv_get(v_receipt__69588, "parse_event"))) : (object)(false))))
        {
            return false;
        }
        object v_assessment__69982 = F_dvs_advice_assessment(v_actor, v_receipt__69588, F_dv_get(v_decision__69481, "assessment_action_id"), v_mind);
        return (R.Bool(R.not(R.eq(v_assessment__69982, false))) ? (object)(R.eq(F_dv_get(v_assessment__69982, "accepted"), F_dv_get(v_decision__69481, "accepted"))) : (object)(false));
    }

    public static object F_dvs_complete_player_influences(object v_actor, object v_action, object v_before, object v_after, object v_success, object v_events, object v_event, object v_time)
    {
        if (R.Bool(R.not(v_success)))
        {
            return v_actor;
        }
        object v_pending__70458 = F_dv_find(v_actor, "pending_player_influences", new System.Collections.Generic.List<object>(0) { });
        if (R.Bool(R.eq(R.count(v_pending__70458), R.number("0"))))
        {
            return v_actor;
        }
        object v_retained__70576 = new System.Collections.Generic.List<object>(0) { };
        object v_history__70598 = F_dv_find(v_actor, "player_influence_history", new System.Collections.Generic.List<object>(0) { });
        object v_i__70663 = R.number("0");
        while (R.Bool(R.lt(v_i__70663, R.count(v_pending__70458))))
        {
            object v_influence__70714 = R.at(v_pending__70458, v_i__70663);
            object v_declaration__70749 = F_dv_get(v_influence__70714, "declaration");
            object v_completed__70797 = false;
            object v_selection__70827 = F_dvs_heard_before_selection(v_events, v_actor, v_action, v_event, F_dv_get(v_influence__70714, "parse_event"), F_dv_get(v_influence__70714, "speech_event"));
            if (R.Bool((R.Bool(R.not(R.eq(v_selection__70827, false))) ? (object)(F_dv_has(F_dv_find(v_declaration__70749, "action_ids", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_action, "id"))) : (object)(false))))
            {
                object v_prior__71052 = F_dv_truth(v_before, F_dv_get(v_declaration__70749, "consequence_when"));
                object v_actual__71123 = F_dv_truth(v_after, F_dv_get(v_declaration__70749, "consequence_when"));
                if (R.Bool((R.Bool(R.not(R.eq(v_prior__71052, "true"))) ? (object)(R.eq(v_actual__71123, "true")) : (object)(false))))
                {
                    object v_stance__71251 = F_dv_find(F_dv_find(v_actor, "advice_stances", new System.Collections.Generic.List<object>(0) { }), F_dv_find(v_declaration__70749, "advice_id", ""), false);
                    if (R.Bool(R.not(R.eq(v_stance__71251, false))))
                    {
                        object v_stance_position__71412 = F_dvs_recorded_event_position(v_events, F_dv_get(v_stance__71251, "event"));
                        if (R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_stance__71251, "parse_event"), F_dv_get(v_influence__70714, "parse_event")))) ? (object)(true) : (object)(R.lt(v_stance_position__71412, R.number("0"))))) ? (object)(true) : (object)(R.not(R.lt(v_stance_position__71412, F_dv_get(v_selection__70827, "position")))))))
                        {
                            v_stance__71251 = false;
                        }
                    }
                    v_history__70598 = F_dv_append(v_history__70598, new System.Collections.Generic.List<object>(17) { F_dv_pair("player", F_dv_get(v_influence__70714, "player")), F_dv_pair("control", F_dv_find(v_influence__70714, "control", "player")), F_dv_pair("target", F_dv_get(v_actor, "id")), F_dv_pair("method", F_dv_get(v_influence__70714, "method")), F_dv_pair("speech_event", F_dv_get(v_influence__70714, "speech_event")), F_dv_pair("parse_event", F_dv_get(v_influence__70714, "parse_event")), F_dv_pair("selection_event", F_dv_get(v_selection__70827, "event")), F_dv_pair("execution_event", v_event), F_dv_pair("action", F_dv_get(v_action, "id")), F_dv_pair("time", v_time), F_dv_pair("consequence", F_dv_get(v_declaration__70749, "consequence_when")), F_dv_pair("before", v_prior__71052), F_dv_pair("after", v_actual__71123), F_dv_pair("basis", F_dv_get(v_declaration__70749, "basis")), F_dv_pair("advice_stance_before_selection", v_stance__71251), F_dv_pair("record_kind", "post-advice-matching-behavior"), F_dv_pair("causal_attribution", "not-established") });
                    v_completed__70797 = true;
                }
            }
            if (R.Bool(R.not(v_completed__70797)))
            {
                _ = R.push(v_retained__70576, v_influence__70714);
            }
            v_i__70663 = R.add(v_i__70663, R.number("1"));
        }
        return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(2) { F_dv_pair("pending_player_influences", v_retained__70576), F_dv_pair("player_influence_history", v_history__70598) });
    }

    public static object F_dvs_request_withdrawn(object actor, object request)
    {
        object source = F_dv_find(request, "source_event", "");
        object requester = F_dv_find(request, "requester", "");
        foreach (object change in R.Items(F_dv_find(actor, "received_request_changes", new List<object>())))
            if (R.Bool(F_dv_find(change, "matched_received_request", false)) && R.Bool(R.eq(F_dv_find(change, "request_source_event", ""), source)) &&
                R.Bool(R.eq(F_dv_find(change, "requester", ""), requester))) return true;
        return false;
    }

    public static object F_dvs_pending_request(object v_actor, object v_action)
    {
        object v_topic__72646 = F_dv_find(v_action, "response_topic", "");
        object v_requests__72700 = F_dv_find(v_actor, "received_requests", new System.Collections.Generic.List<object>(0) { });
        object v_answered__72759 = F_dv_find(v_actor, "answered_requests", new System.Collections.Generic.List<object>(0) { });
        object v_result__72818 = new System.Collections.Generic.List<object>(0) { };
        object v_i__72838 = R.number("0");
        while (R.Bool(F_dv_and(R.eq(R.count(v_result__72818), R.number("0")), R.lt(v_i__72838, R.count(v_requests__72700)))))
        {
            object v_request__72925 = R.at(v_requests__72700, v_i__72838);
            if (R.Bool(F_dv_and(R.eq(F_dv_get(v_request__72925, "topic"), v_topic__72646), F_dv_and(R.not(F_dv_has(v_answered__72759, F_dv_get(v_request__72925, "parse_event"))), R.not(F_dvs_request_withdrawn(v_actor, v_request__72925))))))
            {
                object v_matches__73053 = R.eq(F_dv_find(v_action, "response_requester", F_dv_get(v_request__72925, "requester")), F_dv_get(v_request__72925, "requester"));
                object v_request_action__73157 = F_dv_find(v_action, "response_request_action", "");
                if (R.Bool(R.not(R.eq(v_request_action__73157, ""))))
                {
                    v_matches__73053 = false;
                    object v_receipts__73310 = F_dv_find(v_actor, "received_speeches", new System.Collections.Generic.List<object>(0) { });
                    object v_receipt_index__73381 = R.number("0");
                    while (R.Bool(R.lt(v_receipt_index__73381, R.count(v_receipts__73310))))
                    {
                        object v_receipt__73481 = R.at(v_receipts__73310, v_receipt_index__73381);
                        if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_receipt__73481, "parse_event"), F_dv_get(v_request__72925, "parse_event"))) ? (object)(R.eq(F_dv_get(v_receipt__73481, "speaker"), F_dv_get(v_request__72925, "requester"))) : (object)(false))) ? (object)(R.eq(F_dv_get(v_receipt__73481, "action_id"), v_request_action__73157)) : (object)(false))) ? (object)(R.eq(F_dv_find(v_action, "response_requester", F_dv_get(v_request__72925, "requester")), F_dv_get(v_request__72925, "requester"))) : (object)(false))))
                        {
                            v_matches__73053 = true;
                        }
                        v_receipt_index__73381 = R.add(v_receipt_index__73381, R.number("1"));
                    }
                }
                if (R.Bool(v_matches__73053))
                {
                    v_result__72818 = v_request__72925;
                }
            }
            v_i__72838 = R.add(v_i__72838, R.number("1"));
        }
        return v_result__72818;
    }

    public static object F_dvs_receive_request(object actor, object speech, object parseEvent, object source, object speaker)
    {
        object change = F_dv_find(speech, "request_change", false);
        if (!R.Bool(R.eq(change, false)))
        {
            _ = F_dv_require(F_dv_record_like(change), "Request change must be a record");
            object operation = F_dv_find(change, "operation", "");
            _ = F_dv_require(F_dv_has(new List<object> { "withdraw", "replace" }, operation), "Request change requires withdraw or replace");
            object target = F_dv_find(change, "target", "");
            object priorSource = F_dv_find(change, "request_source_event", "");
            _ = F_dv_require(R.is_text(target), "Request change target must be text");
            _ = F_dv_require(R.is_text(priorSource), "Request change source must be text");
            _ = F_dv_require(R.not(R.eq(priorSource, "")), "Request change requires a nonempty request source event");
            if (R.Bool(R.eq(operation, "replace")))
            {
                object replacement = F_dv_find(speech, "request", false);
                _ = F_dv_require(R.not(R.eq(replacement, false)), "Request replacement requires a new request");
                _ = F_dv_require(R.eq(F_dv_get(replacement, "target"), target), "Request replacement target mismatch");
            }
            if (R.Bool(R.eq(target, F_dv_get(actor, "id"))))
            {
                bool matched = false;
                foreach (object request in R.Items(F_dv_find(actor, "received_requests", new List<object>())))
                    if (R.Bool(R.eq(F_dv_find(request, "source_event", ""), priorSource)) && R.Bool(R.eq(F_dv_find(request, "requester", ""), speaker))) { matched = true; break; }
                object record = new List<object> { F_dv_pair("operation", operation), F_dv_pair("request_source_event", priorSource), F_dv_pair("requester", speaker),
                    F_dv_pair("source_event", source), F_dv_pair("parse_event", parseEvent), F_dv_pair("matched_received_request", matched) };
                actor = F_dv_with(actor, "received_request_changes", F_dv_append(F_dv_find(actor, "received_request_changes", new List<object>()), record));
            }
        }
        object requestDeclaration = F_dv_find(speech, "request", false);
        if (R.Bool(R.eq(requestDeclaration, false))) return actor;
        if (!R.Bool(R.eq(F_dv_get(requestDeclaration, "target"), F_dv_get(actor, "id")))) return actor;
        return F_dv_with(actor, "received_requests", F_dv_append(F_dv_find(actor, "received_requests", new List<object>()),
            F_dv_with_many(new List<object> { F_dv_pair("topic", F_dv_get(requestDeclaration, "topic")), F_dv_pair("parse_event", parseEvent), F_dv_pair("source_event", source) },
                new List<object> { F_dv_pair("requester", speaker), F_dv_pair("request", requestDeclaration) })));
    }

    public static object F_dvs_finish_reply(object v_actor, object v_action)
    {
        object v_request__74481 = F_dvs_pending_request(v_actor, v_action);
        if (R.Bool(R.lt(R.number("0"), R.count(v_request__74481))))
        {
            return F_dv_with(v_actor, "answered_requests", F_dv_append(F_dv_find(v_actor, "answered_requests", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_request__74481, "parse_event")));
        }
        else
        {
            return v_actor;
        }
    }

    public static object F_dvs_record_request(object v_actor, object v_action)
    {
        object v_request__74760 = F_dv_find(F_dv_find(v_action, "speech", new System.Collections.Generic.List<object>(0) { }), "request", false);
        if (R.Bool(R.eq(v_request__74760, false)))
        {
            return v_actor;
        }
        else
        {
            return F_dv_with(v_actor, "request_contexts", F_dv_with(F_dv_find(v_actor, "request_contexts", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_action, "id"), F_dvs_request_context(v_actor, v_action, F_dv_get(v_request__74760, "target"))));
        }
    }

    public static object F_dvs_request_inputs(object v_mind, object v_proposition)
    {
        object v_refs__75098 = F_dvs_sim_refs(v_proposition);
        object v_inputs__75139 = new System.Collections.Generic.List<object>(0) { };
        object v_i__75159 = R.number("0");
        while (R.Bool(R.lt(v_i__75159, R.count(v_refs__75098))))
        {
            object v_cell__75207 = F_dv_read(v_mind, R.at(v_refs__75098, v_i__75159));
            _ = R.push(v_inputs__75139, new System.Collections.Generic.List<object>(4) { F_dv_pair("ref", R.at(v_refs__75098, v_i__75159)), F_dv_pair("known", F_dv_find(v_cell__75207, "known", false)), F_dv_pair("value", F_dv_find(v_cell__75207, "value", false)), F_dv_pair("conflicted", F_dv_find(v_cell__75207, "conflicted", false)) });
            v_i__75159 = R.add(v_i__75159, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("inputs", v_inputs__75139), F_dv_pair("truth", F_dv_truth(v_mind, v_proposition)) };
    }

    public static object F_dvs_request_context(object v_actor, object v_action, object v_target)
    {
        object v_topic__75551 = F_dv_find(F_dv_find(F_dv_find(v_action, "speech", new System.Collections.Generic.List<object>(0) { }), "request", new System.Collections.Generic.List<object>(0) { }), "topic", F_dv_find(v_action, "response_topic", ""));
        object v_visible__75678 = F_dv_find(F_dv_find(v_actor, "visible_context", new System.Collections.Generic.List<object>(0) { }), v_target, false);
        if (R.Bool(R.not(R.eq(v_visible__75678, false))))
        {
            return new System.Collections.Generic.List<object>(5) { F_dv_pair("target", v_target), F_dv_pair("topic", v_topic__75551), F_dv_pair("location", F_dv_get(v_visible__75678, "location")), F_dv_pair("presence_event", F_dv_find(v_actor, "visible_context_event", "")), F_dv_pair("inputs", F_dvs_request_inputs(F_dv_get(v_actor, "mind"), F_dv_get(v_action, "precondition"))) };
        }
        object v_transport__75991 = F_dv_find(v_action, "request_transport", false);
        if (R.Bool(R.eq(v_transport__75991, false)))
        {
            return false;
        }
        object v_channels__76105 = F_dv_find(v_actor, "request_channels", new System.Collections.Generic.List<object>(0) { });
        object v_i__76163 = R.number("0");
        while (R.Bool(R.lt(v_i__76163, R.count(v_channels__76105))))
        {
            object v_channel__76217 = R.at(v_channels__76105, v_i__76163);
            if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_channel__76217, "target"), v_target)) ? (object)(R.eq(F_dv_get(v_channel__76217, "channel"), F_dv_get(v_transport__75991, "channel"))) : (object)(false))) ? (object)(R.lt(R.number("0"), R.count(F_dv_get(v_channel__76217, "basis")))) : (object)(false))))
            {
                if (R.Bool(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_channel__76217, "available_when")), "true")))
                {
                    return new System.Collections.Generic.List<object>(6) { F_dv_pair("target", v_target), F_dv_pair("topic", v_topic__75551), F_dv_pair("channel", F_dv_get(v_channel__76217, "channel")), F_dv_pair("basis", F_dv_get(v_channel__76217, "basis")), F_dv_pair("inputs", F_dvs_request_inputs(F_dv_get(v_actor, "mind"), F_dv_get(v_action, "precondition"))), F_dv_pair("channel_inputs", F_dvs_request_inputs(F_dv_get(v_actor, "mind"), F_dv_get(v_channel__76217, "available_when"))) };
                }
            }
            v_i__76163 = R.add(v_i__76163, R.number("1"));
        }
        return false;
    }
}
