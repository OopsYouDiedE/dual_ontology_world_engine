using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Communication / Speech: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_speech_cognitive_parents(object v_parents, object v_previous)
    {
        if (R.Bool(F_dv_or(R.eq(v_previous, ""), F_dv_has(v_parents, v_previous))))
        {
            return v_parents;
        }
        else
        {
            return F_dv_append(v_parents, v_previous);
        }
    }

    public static object F_dvs_speech_ast_nodes(object v_ast)
    {
        object v_op__122312 = F_dv_get(v_ast, "op");
        object v_count__122332 = R.number("1");
        if (R.Bool(R.eq(v_op__122312, "not")))
        {
            v_count__122332 = R.add(v_count__122332, F_dvs_speech_ast_nodes(F_dv_get(v_ast, "item")));
        }
        else
        {
            if (R.Bool(R.eq(v_op__122312, "all")))
            {
                object v_items__122473 = F_dv_get(v_ast, "items");
                object v_i__122507 = R.number("0");
                while (R.Bool(R.lt(v_i__122507, R.count(v_items__122473))))
                {
                    v_count__122332 = R.add(v_count__122332, F_dvs_speech_ast_nodes(R.at(v_items__122473, v_i__122507)));
                    v_i__122507 = R.add(v_i__122507, R.number("1"));
                }
            }
        }
        return v_count__122332;
    }

    public static object F_dvs_speech_map(object v_mind, object v_proposition, object v_bindings)
    {
        object v_op__122721 = F_dv_get(v_proposition, "op");
        if (R.Bool(R.eq(v_op__122721, "constant")))
        {
            return v_proposition;
        }
        else
        {
            if (R.Bool(R.eq(v_op__122721, "all")))
            {
                object v_mapped__122847 = new System.Collections.Generic.List<object>(0) { };
                object v_i__122875 = R.number("0");
                object v_items__122897 = F_dv_get(v_proposition, "items");
                while (R.Bool(R.lt(v_i__122875, R.count(v_items__122897))))
                {
                    _ = R.push(v_mapped__122847, F_dvs_speech_map(v_mind, R.at(v_items__122897, v_i__122875), v_bindings));
                    v_i__122875 = R.add(v_i__122875, R.number("1"));
                }
                return F_dv_all(v_mapped__122847);
            }
            else
            {
                if (R.Bool(R.eq(v_op__122721, "not")))
                {
                    return F_dv_not(F_dvs_speech_map(v_mind, F_dv_get(v_proposition, "item"), v_bindings));
                }
                else
                {
                    object v_ref__123262 = F_dv_get(v_proposition, "ref");
                    object v_local__123304 = F_dv_local(v_bindings, F_dv_get(v_ref__123262, "object"), F_dv_get(v_mind, "subject"));
                    if (R.Bool(R.eq(v_local__123304, "")))
                    {
                        if (R.Bool(F_dv_has_key(F_dv_get(v_mind, "objects"), F_dv_get(v_ref__123262, "object"))))
                        {
                            return v_proposition;
                        }
                        else
                        {
                            return new System.Collections.Generic.List<object>(2) { F_dv_pair("op", "constant"), F_dv_pair("value", "unknown") };
                        }
                    }
                    else
                    {
                        return F_dv_with(v_proposition, "ref", F_dv_ref(v_local__123304, F_dv_get(v_ref__123262, "facet"), F_dv_get(v_ref__123262, "field")));
                    }
                }
            }
        }
    }

    public static object F_dvs_speech_truth(object v_mind, object v_speech)
    {
        return F_dv_truth(v_mind, F_dvs_speech_map(v_mind, F_dv_get(v_speech, "literal_ast"), F_dv_find(v_speech, "referents", new System.Collections.Generic.List<object>(0) { })));
    }

    public static object F_dvs_speech_presuppositions(object v_mind, object v_speech)
    {
        object v_items__123897 = F_dv_find(v_speech, "presuppositions", new System.Collections.Generic.List<object>(0) { });
        object v_bindings__123952 = F_dv_find(v_speech, "referents", new System.Collections.Generic.List<object>(0) { });
        object v_i__124004 = R.number("0");
        object v_result__124018 = new System.Collections.Generic.List<object>(0) { };
        while (R.Bool(R.lt(v_i__124004, R.count(v_items__123897))))
        {
            object v_proposition__124075 = F_dvs_speech_map(v_mind, R.at(v_items__123897, v_i__124004), v_bindings__123952);
            _ = R.push(v_result__124018, new System.Collections.Generic.List<object>(2) { F_dv_pair("proposition", v_proposition__124075), F_dv_pair("truth", F_dv_truth(v_mind, v_proposition__124075)) });
            v_i__124004 = R.add(v_i__124004, R.number("1"));
        }
        return v_result__124018;
    }

    public static object F_dvs_emission_location(object v_speaker, object v_speech, object v_physical)
    {
        object v_ref__124322 = F_dv_find(v_speech, "emission_location_ref", F_dv_get(v_speaker, "location_ref"));
        object v_cell__124399 = F_dv_read(v_physical, v_ref__124322);
        if (R.Bool(F_dv_uncontested(v_cell__124399)))
        {
            return F_dv_get(v_cell__124399, "value");
        }
        return "";
    }

    public static object F_dvs_auditory_access(object v_actor, object v_physical)
    {
        return R.eq(F_dv_truth(v_physical, F_dv_find(v_actor, "auditory_access_when", F_dv_true())), "true");
    }

    public static object F_dvs_speech_hearing(object v_speaker, object v_listener, object v_speech, object v_physical)
    {
        if (R.Bool(R.not(F_dvs_auditory_access(v_listener, v_physical))))
        {
            return false;
        }
        object v_source__124773 = F_dv_get(v_speaker, "id");
        object v_channel__124801 = F_dv_find(v_speech, "channel", "");
        object v_source_location__124850 = F_dvs_emission_location(v_speaker, v_speech, v_physical);
        object v_target_cell__124925 = F_dv_read(v_physical, F_dv_get(v_listener, "location_ref"));
        object v_target_location__124988 = "";
        if (R.Bool(F_dv_uncontested(v_target_cell__124925)))
        {
            v_target_location__124988 = F_dv_get(v_target_cell__124925, "value");
        }
        object v_grants__125097 = F_dv_find(v_listener, "hearing_grants", new System.Collections.Generic.List<object>(0) { });
        if (R.Bool((R.Bool(R.not(R.eq(v_source_location__124850, ""))) ? (object)(F_dv_same(v_source_location__124850, v_target_location__124988)) : (object)(false))))
        {
            return true;
        }
        object v_heard__125252 = false;
        object v_i__125274 = R.number("0");
        while (R.Bool(R.lt(v_i__125274, R.count(v_grants__125097))))
        {
            object v_grant__125326 = R.at(v_grants__125097, v_i__125274);
            if (R.Bool(F_dv_and(R.eq(F_dv_get(v_grant__125326, "speaker"), v_source__124773), R.eq(F_dv_get(v_grant__125326, "channel"), v_channel__124801))))
            {
                object v_sender_locations__125434 = F_dv_find(v_grant__125326, "sender_locations", new System.Collections.Generic.List<object>(0) { });
                object v_listener_locations__125508 = F_dv_find(v_grant__125326, "listener_locations", new System.Collections.Generic.List<object>(0) { });
                object v_reception__125586 = v_target_location__124988;
                if (R.Bool(F_dv_has_key(v_grant__125326, "reception_location_ref")))
                {
                    object v_endpoint__125694 = F_dv_read(v_physical, F_dv_get(v_grant__125326, "reception_location_ref"));
                    v_reception__125586 = "";
                    if (R.Bool(F_dv_uncontested(v_endpoint__125694)))
                    {
                        v_reception__125586 = F_dv_get(v_endpoint__125694, "value");
                    }
                }
                if (R.Bool((R.Bool(R.eq(F_dv_truth(F_dv_get(v_listener, "mind"), F_dv_find(v_grant__125326, "available_when", F_dv_true())), "true")) ? (object)(R.eq(F_dv_truth(v_physical, F_dv_find(v_grant__125326, "authority_when", F_dv_true())), "true")) : (object)(false))))
                {
                    if (R.Bool((R.Bool((R.Bool(R.not(R.eq(v_source_location__124850, ""))) ? (object)(R.not(R.eq(v_reception__125586, ""))) : (object)(false))) ? (object)((R.Bool((R.Bool(R.eq(R.count(v_sender_locations__125434), R.number("0"))) ? (object)(true) : (object)(F_dv_has(v_sender_locations__125434, v_source_location__124850)))) ? (object)((R.Bool(R.eq(R.count(v_listener_locations__125508), R.number("0"))) ? (object)(true) : (object)(F_dv_has(v_listener_locations__125508, v_reception__125586)))) : (object)(false))) : (object)(false))))
                    {
                        v_heard__125252 = true;
                    }
                }
            }
            v_i__125274 = R.add(v_i__125274, R.number("1"));
        }
        return v_heard__125252;
    }

    public static object F_dvs_speech_observations(object v_mind, object v_proposition, object v_source, object v_origin, object v_time, object v_trust)
    {
        object v_op__126434 = F_dv_get(v_proposition, "op");
        object v_observations__126462 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.eq(v_op__126434, "all")))
        {
            object v_items__126514 = F_dv_get(v_proposition, "items");
            object v_i__126552 = R.number("0");
            while (R.Bool(R.lt(v_i__126552, R.count(v_items__126514))))
            {
                object v_part__126611 = F_dvs_speech_observations(v_mind, R.at(v_items__126514, v_i__126552), v_source, v_origin, v_time, v_trust);
                object v_j__126703 = R.number("0");
                while (R.Bool(R.lt(v_j__126703, R.count(v_part__126611))))
                {
                    if (R.Bool(F_dv_has(v_observations__126462, R.at(v_part__126611, v_j__126703))))
                    {
                        _ = false;
                    }
                    else
                    {
                        _ = R.push(v_observations__126462, R.at(v_part__126611, v_j__126703));
                    }
                    v_j__126703 = R.add(v_j__126703, R.number("1"));
                }
                v_i__126552 = R.add(v_i__126552, R.number("1"));
            }
        }
        else
        {
            if (R.Bool(R.eq(v_op__126434, "equals")))
            {
                object v_ref__127005 = F_dv_get(v_proposition, "ref");
                _ = R.push(v_observations__126462, F_dv_observation(F_dv_list3(v_source, F_dv_get(v_mind, "subject"), v_ref__127005), F_dv_get(v_mind, "subject"), v_ref__127005, F_dv_get(v_proposition, "value"), "testimony", v_origin, v_time, v_trust, F_dv_pair(v_origin, v_ref__127005)));
            }
        }
        return v_observations__126462;
    }

    public static object F_dvs_speaker_trust(object v_policy, object v_speaker)
    {
        object v_trust__127280 = F_dv_find(F_dv_find(v_policy, "speaker_trust", new System.Collections.Generic.List<object>(0) { }), v_speaker, R.number("0.5"));
        object v_biases__127356 = F_dv_find(v_policy, "class_bias", new System.Collections.Generic.List<object>(0) { });
        object v_i__127407 = R.number("0");
        while (R.Bool(R.lt(v_i__127407, R.count(v_biases__127356))))
        {
            object v_bias__127457 = R.at(v_biases__127356, v_i__127407);
            if (R.Bool((R.Bool(R.lt(R.number("0"), R.count(F_dv_find(v_bias__127457, "basis", new System.Collections.Generic.List<object>(0) { })))) ? (object)(F_dv_has(F_dv_get(v_bias__127457, "speakers"), v_speaker)) : (object)(false))))
            {
                v_trust__127280 = R.add(v_trust__127280, F_dv_get(v_bias__127457, "delta"));
            }
            v_i__127407 = R.add(v_i__127407, R.number("1"));
        }
        return F_dv_min(R.number("1"), F_dv_max(R.number("0"), v_trust__127280));
    }

    public static object F_dvs_speech_proposal_decision(object v_mind, object v_policy, object v_speech, object v_trust)
    {
        object v_proposal__127729 = F_dv_find(v_speech, "proposal", false);
        object v_status__127783 = "ordinary";
        object v_reason__127811 = "ordinary-testimony";
        object v_confidence__127849 = v_trust;
        object v_units__127876 = R.number("0");
        object v_acceptance__127894 = F_dv_find(v_policy, "proposal_acceptance", false);
        if (R.Bool(R.eq(v_proposal__127729, false)))
        {
            _ = false;
        }
        else
        {
            object v_claims__128017 = F_dv_find(v_proposal__127729, "claims", new System.Collections.Generic.List<object>(0) { });
            object v_local_1__128070 = false;
            if (R.Bool(R.eq(v_acceptance__127894, false)))
            {
                v_local_1__128070 = new System.Collections.Generic.List<object>(0) { };
            }
            else
            {
                v_local_1__128070 = F_dv_find(v_acceptance__127894, "required_slots", new System.Collections.Generic.List<object>(0) { });
            }
            object v_required__128235 = v_local_1__128070;
            object v_propositions__128266 = new System.Collections.Generic.List<object>(0) { };
            object v_allowed__128296 = true;
            object v_i__128323 = R.number("0");
            object v_available__128341 = "unknown";
            v_status__127783 = "retained";
            v_reason__127811 = "missing-personal-proposal-policy";
            v_units__127876 = R.number("1");
            v_confidence__127849 = F_dv_min(v_trust, F_dv_find(v_proposal__127729, "confidence", R.number("0")));
            while (R.Bool(R.lt(v_i__128323, R.count(v_claims__128017))))
            {
                object v_claim__128586 = R.at(v_claims__128017, v_i__128323);
                _ = R.push(v_propositions__128266, F_dv_fact(F_dv_get(v_claim__128586, "ref"), F_dv_get(v_claim__128586, "value")));
                v_units__127876 = R.add(v_units__127876, R.number("1"));
                v_i__128323 = R.add(v_i__128323, R.number("1"));
            }
            v_i__128323 = R.number("0");
            while (R.Bool(R.lt(v_i__128323, R.count(v_required__128235))))
            {
                object v_j__128797 = R.number("0");
                object v_found__128819 = false;
                while (R.Bool(R.lt(v_j__128797, R.count(v_claims__128017))))
                {
                    if (R.Bool(R.eq(R.at(v_required__128235, v_i__128323), F_dv_get(R.at(v_claims__128017, v_j__128797), "slot_id"))))
                    {
                        v_found__128819 = true;
                    }
                    v_units__127876 = R.add(v_units__127876, R.number("1"));
                    v_j__128797 = R.add(v_j__128797, R.number("1"));
                }
                if (R.Bool(v_found__128819))
                {
                    _ = false;
                }
                else
                {
                    v_allowed__128296 = false;
                }
                v_i__128323 = R.add(v_i__128323, R.number("1"));
            }
            if (R.Bool(R.eq(v_acceptance__127894, false)))
            {
                _ = false;
            }
            else
            {
                v_available__128341 = F_dv_truth(v_mind, F_dvs_speech_map(v_mind, F_dv_find(v_acceptance__127894, "available_when", F_dv_true()), F_dv_find(v_speech, "referents", new System.Collections.Generic.List<object>(0) { })));
                v_units__127876 = R.add(v_units__127876, R.mul(R.number("2"), F_dvs_speech_ast_nodes(F_dv_find(v_acceptance__127894, "available_when", F_dv_true()))));
                v_reason__127811 = "personal-proposal-policy-withheld";
                if (R.Bool(F_dv_find(v_acceptance__127894, "enabled", false)))
                {
                    _ = false;
                }
                else
                {
                    v_allowed__128296 = false;
                }
                if (R.Bool(R.lt(R.number("0"), R.count(F_dv_find(v_acceptance__127894, "basis", new System.Collections.Generic.List<object>(0) { })))))
                {
                    _ = false;
                }
                else
                {
                    v_allowed__128296 = false;
                }
                if (R.Bool(R.eq(v_available__128341, "true")))
                {
                    _ = false;
                }
                else
                {
                    v_allowed__128296 = false;
                }
                if (R.Bool(R.lt(R.number("0"), R.count(v_required__128235))))
                {
                    _ = false;
                }
                else
                {
                    v_allowed__128296 = false;
                }
                if (R.Bool(R.lt(R.number("0"), R.count(v_claims__128017))))
                {
                    _ = false;
                }
                else
                {
                    v_allowed__128296 = false;
                }
                if (R.Bool(F_dv_find(v_proposal__127729, "estimated", false)))
                {
                    _ = false;
                }
                else
                {
                    v_allowed__128296 = false;
                }
                if (R.Bool(R.eq(F_dv_find(v_proposal__127729, "argument_id", ""), "")))
                {
                    v_allowed__128296 = false;
                }
                if (R.Bool(F_dv_and(R.lt(R.number("0"), F_dv_find(v_proposal__127729, "confidence", R.number("0"))), R.lt(F_dv_find(v_proposal__127729, "confidence", R.number("0")), R.number("1")))))
                {
                    _ = false;
                }
                else
                {
                    v_allowed__128296 = false;
                }
                if (R.Bool(R.lt(R.number("0"), R.count(F_dv_find(v_proposal__127729, "source_event_ids", new System.Collections.Generic.List<object>(0) { })))))
                {
                    _ = false;
                }
                else
                {
                    v_allowed__128296 = false;
                }
                if (R.Bool(R.eq(F_dv_find(v_proposal__127729, "model_event", ""), "")))
                {
                    v_allowed__128296 = false;
                }
                if (R.Bool(F_dv_same(F_dv_all(v_propositions__128266), F_dv_get(v_speech, "literal_ast"))))
                {
                    _ = false;
                }
                else
                {
                    v_allowed__128296 = false;
                }
                if (R.Bool(R.lt(v_confidence__127849, F_dv_find(v_acceptance__127894, "confidence_threshold", R.number("0.5")))))
                {
                    v_allowed__128296 = false;
                }
                if (R.Bool(v_allowed__128296))
                {
                    v_status__127783 = "accepted";
                    v_reason__127811 = "personal-bounded-proposal-adoption";
                }
            }
        }
        object v_local_2__131134 = false;
        if (R.Bool(R.eq(v_acceptance__127894, false)))
        {
            v_local_2__131134 = new System.Collections.Generic.List<object>(0) { };
        }
        else
        {
            v_local_2__131134 = F_dv_find(v_acceptance__127894, "basis", new System.Collections.Generic.List<object>(0) { });
        }
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("status", v_status__127783), F_dv_pair("reason", v_reason__127811), F_dv_pair("confidence_cap", v_confidence__127849), F_dv_pair("proposal", v_proposal__127729) }, new System.Collections.Generic.List<object>(2) { F_dv_pair("policy_basis", v_local_2__131134), F_dv_pair("calculation_units", v_units__127876) });
    }

    public static object F_dvs_speech_received_observations(object v_mind, object v_parsed, object v_source, object v_speaker, object v_time, object v_decision)
    {
        if (R.Bool(R.eq(F_dv_get(v_decision, "status"), "retained")))
        {
            return new System.Collections.Generic.List<object>(0) { };
        }
        else
        {
            object v_items__131596 = F_dvs_speech_observations(v_mind, v_parsed, v_source, v_speaker, v_time, F_dv_get(v_decision, "confidence_cap"));
            object v_i__131702 = R.number("0");
            object v_result__131720 = new System.Collections.Generic.List<object>(0) { };
            while (R.Bool(R.lt(v_i__131702, R.count(v_items__131596))))
            {
                object v_local_1__131785 = false;
                if (R.Bool(R.eq(F_dv_get(v_decision, "status"), "ordinary")))
                {
                    v_local_1__131785 = R.at(v_items__131596, v_i__131702);
                }
                else
                {
                    v_local_1__131785 = F_dv_with_many(R.at(v_items__131596, v_i__131702), new System.Collections.Generic.List<object>(2) { F_dv_pair("estimated", true), F_dv_pair("proposal_source", F_dv_get(v_decision, "proposal")) });
                }
                _ = R.push(v_result__131720, v_local_1__131785);
                v_i__131702 = R.add(v_i__131702, R.number("1"));
            }
            return v_result__131720;
        }
    }

    public static object F_dvs_speech_candidates(object v_policy, object v_speech)
    {
        object v_cues__132144 = F_dv_find(v_speech, "implicature_candidates", new System.Collections.Generic.List<object>(0) { });
        object v_rules__132205 = F_dv_find(v_policy, "implicature_bridges", new System.Collections.Generic.List<object>(0) { });
        object v_result__132264 = new System.Collections.Generic.List<object>(0) { };
        object v_ids__132284 = new System.Collections.Generic.List<object>(0) { };
        object v_local_ids__132301 = new System.Collections.Generic.List<object>(0) { };
        object v_i__132324 = R.number("0");
        while (R.Bool(R.lt(v_i__132324, R.count(v_rules__132205))))
        {
            object v_rule__132375 = R.at(v_rules__132205, v_i__132324);
            object v_id__132403 = F_dv_get(R.at(v_rules__132205, v_i__132324), "id");
            object v_required__132432 = F_dv_find(R.at(v_rules__132205, v_i__132324), "cue", "");
            object v_j__132484 = R.number("0");
            object v_matched__132502 = false;
            _ = R.push(v_local_ids__132301, v_id__132403);
            if (R.Bool(R.eq(v_required__132432, "")))
            {
                v_matched__132502 = true;
            }
            while (R.Bool(R.lt(v_j__132484, R.count(v_cues__132144))))
            {
                if (R.Bool(R.eq(F_dv_get(R.at(v_cues__132144, v_j__132484), "id"), v_required__132432)))
                {
                    v_matched__132502 = true;
                }
                v_j__132484 = R.add(v_j__132484, R.number("1"));
            }
            if (R.Bool(v_matched__132502))
            {
                _ = R.push(v_result__132264, new System.Collections.Generic.List<object>(3) { F_dv_pair("id", v_id__132403), F_dv_pair("rule_source", F_dv_find(v_rule__132375, "basis", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("utterance_scope", F_dv_find(v_speech, "scope", "")) });
                _ = R.push(v_ids__132284, v_id__132403);
            }
            v_i__132324 = R.add(v_i__132324, R.number("1"));
        }
        v_i__132324 = R.number("0");
        while (R.Bool(R.lt(v_i__132324, R.count(v_cues__132144))))
        {
            object v_cue_id__132992 = F_dv_get(R.at(v_cues__132144, v_i__132324), "id");
            if (R.Bool(F_dv_or(F_dv_has(v_ids__132284, v_cue_id__132992), F_dv_has(v_local_ids__132301, v_cue_id__132992))))
            {
                _ = false;
            }
            else
            {
                _ = R.push(v_result__132264, new System.Collections.Generic.List<object>(3) { F_dv_pair("id", v_cue_id__132992), F_dv_pair("rule_source", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("utterance_scope", F_dv_find(v_speech, "scope", "")) });
                _ = R.push(v_ids__132284, v_cue_id__132992);
            }
            v_i__132324 = R.add(v_i__132324, R.number("1"));
        }
        return v_result__132264;
    }

    public static object F_dvs_listener_mapping(object v_mind, object v_policy, object v_bindings, object v_sf_input)
    {
        return F_dvs_speech_interpret(v_mind, v_policy, v_bindings, v_sf_input);
    }

    public static object F_dvs_speech_interpret(object v_mind, object v_policy, object v_bindings, object v_candidate)
    {
        object v_bridges__133519 = F_dv_find(v_policy, "implicature_bridges", new System.Collections.Generic.List<object>(0) { });
        object v_i__133580 = R.number("0");
        object v_bridge__133594 = false;
        object v_proposition__133617 = new System.Collections.Generic.List<object>(2) { F_dv_pair("op", "constant"), F_dv_pair("value", "unknown") };
        object v_available__133678 = F_dv_true();
        object v_threshold__133708 = F_dv_max(F_dv_find(v_policy, "implicature_threshold", R.number("0.5")), F_dv_find(v_policy, "Implicature_Resistance", R.number("0")));
        object v_confidence__133828 = R.number("0");
        object v_adoption__133851 = new System.Collections.Generic.List<object>(2) { F_dv_pair("degree", R.number("0")), F_dv_pair("level", "held") };
        object v_basis__133897 = new System.Collections.Generic.List<object>(0) { };
        object v_status__133916 = "rejected";
        object v_missing__133944 = "missing-bridge";
        object v_scope_check__133979 = new System.Collections.Generic.List<object>(0) { };
        object v_withdrawal_evidence__134004 = false;
        object v_work__134040 = R.number("0");
        while (R.Bool(R.lt(v_i__133580, R.count(v_bridges__133519))))
        {
            object v_item__134096 = R.at(v_bridges__133519, v_i__133580);
            if (R.Bool(R.eq(F_dv_get(v_item__134096, "id"), F_dv_get(v_candidate, "id"))))
            {
                _ = F_dv_require(R.eq(v_bridge__133594, false), "Ambiguous interpretation bridge");
                v_bridge__133594 = v_item__134096;
            }
            v_i__133580 = R.add(v_i__133580, R.number("1"));
        }
        if (R.Bool(R.eq(v_bridge__133594, false)))
        {
            _ = false;
        }
        else
        {
            object v_premise__134337 = F_dvs_speech_map(v_mind, F_dv_get(v_bridge__133594, "premise"), v_bindings);
            object v_bridge_basis__134406 = F_dv_find(v_bridge__133594, "basis", new System.Collections.Generic.List<object>(0) { });
            object v_withdrawal__134462 = F_dvs_speech_map(v_mind, F_dv_find(v_bridge__133594, "withdraw_when", F_dv_not(F_dv_true())), v_bindings);
            object v_scope_ast__134571 = F_dvs_speech_map(v_mind, F_dv_find(v_bridge__133594, "scope_bridge", new System.Collections.Generic.List<object>(2) { F_dv_pair("op", "constant"), F_dv_pair("value", "unknown") }), v_bindings);
            v_proposition__133617 = F_dvs_speech_map(v_mind, F_dv_get(v_bridge__133594, "proposition"), v_bindings);
            v_available__133678 = F_dvs_speech_map(v_mind, F_dv_find(v_bridge__133594, "available_when", F_dv_true()), v_bindings);
            v_confidence__133828 = F_dv_unit(F_dv_find(v_bridge__133594, "prior", R.number("0.5")));
            v_basis__133897 = v_bridge_basis__134406;
            v_work__134040 = R.add(R.number("1"), R.mul(R.number("2"), R.add(R.add(F_dvs_speech_ast_nodes(v_premise__134337), F_dvs_speech_ast_nodes(v_proposition__133617)), R.add(F_dvs_speech_ast_nodes(v_available__133678), R.add(F_dvs_speech_ast_nodes(v_withdrawal__134462), F_dvs_speech_ast_nodes(v_scope_ast__134571))))));
            object v_literal_scope__135162 = F_dv_find(v_candidate, "utterance_scope", F_dv_find(v_bridge__133594, "source_scope", ""));
            object v_interpreted_scope__135265 = F_dv_find(v_bridge__133594, "target_scope", F_dv_find(v_bridge__133594, "source_scope", ""));
            object v_scope_truth__135366 = F_dv_truth(v_mind, v_scope_ast__134571);
            object v_withdraw_truth__135418 = F_dv_truth(v_mind, v_withdrawal__134462);
            v_withdrawal_evidence__134004 = new System.Collections.Generic.List<object>(4) { F_dv_pair("condition", v_withdrawal__134462), F_dv_pair("truth", v_withdraw_truth__135418), F_dv_pair("inputs", F_dvs_sim_inputs(v_mind, v_withdrawal__134462)), F_dv_pair("scope", "owner-known-cells-only") };
            object v_available_truth__135634 = F_dv_truth(v_mind, v_available__133678);
            object v_premise_truth__135690 = F_dv_truth(v_mind, v_premise__134337);
            object v_proposition_truth__135742 = F_dv_truth(v_mind, v_proposition__133617);
            v_scope_check__133979 = F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("literal_scope", v_literal_scope__135162), F_dv_pair("interpretation_scope", v_interpreted_scope__135265), F_dv_pair("bridge_truth", v_scope_truth__135366), F_dv_pair("check_enabled", F_dv_find(v_policy, "scope_reference_check", false)) }, "gap", F_dv_and(R.not(R.eq(v_literal_scope__135162, v_interpreted_scope__135265)), R.not(R.eq(v_scope_truth__135366, "true"))));
            if (R.Bool(R.eq(v_withdraw_truth__135418, "true")))
            {
                v_status__133916 = "withdrawn";
                v_missing__133944 = "explicit-withdrawal-evidence";
            }
            else
            {
                if (R.Bool(R.not(R.eq(v_available_truth__135634, "true"))))
                {
                    v_missing__133944 = "availability-not-known";
                }
                else
                {
                    if (R.Bool(R.eq(R.count(v_bridge_basis__134406), R.number("0"))))
                    {
                        v_missing__133944 = "bridge-without-source";
                    }
                    else
                    {
                        if (R.Bool(R.not(R.eq(v_premise_truth__135690, "true"))))
                        {
                            v_missing__133944 = "bridge-premise-not-known";
                        }
                        else
                        {
                            if (R.Bool(R.eq(R.count(v_basis__133897), R.number("0"))))
                            {
                                v_missing__133944 = "candidate-without-source";
                            }
                            else
                            {
                                if (R.Bool(R.eq(v_proposition_truth__135742, "false")))
                                {
                                    v_missing__133944 = "contrary-evidence";
                                }
                                else
                                {
                                    v_adoption__133851 = F_dv_threshold_acceptance(v_confidence__133828, v_threshold__133708);
                                    if (R.Bool(R.not(R.eq(F_dv_get(v_adoption__133851, "level"), "accepted"))))
                                    {
                                        v_missing__133944 = "below-policy-threshold";
                                    }
                                    else
                                    {
                                        v_status__133916 = "accepted";
                                        v_missing__133944 = "";
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if (R.Bool(F_dv_and(R.eq(v_status__133916, "accepted"), F_dv_and(F_dv_get(v_scope_check__133979, "check_enabled"), F_dv_get(v_scope_check__133979, "gap")))))
            {
                v_status__133916 = "retained";
                v_adoption__133851 = F_dv_threshold_acceptance(R.number("0"), v_threshold__133708);
                v_missing__133944 = "scope-reference-bridge-not-supported";
            }
            v_basis__133897 = F_dv_append(v_basis__133897, new System.Collections.Generic.List<object>(2) { F_dv_pair("bridge", F_dv_get(v_bridge__133594, "id")), F_dv_pair("basis", v_bridge_basis__134406) });
        }
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("id", F_dv_get(v_candidate, "id")), F_dv_pair("proposition", v_proposition__133617), F_dv_pair("status", v_status__133916), F_dv_pair("confidence", v_confidence__133828) }, new System.Collections.Generic.List<object>(7) { F_dv_pair("basis", v_basis__133897), F_dv_pair("missing_bridge", v_missing__133944), F_dv_pair("scope_check", v_scope_check__133979), F_dv_pair("withdrawal_evidence", v_withdrawal_evidence__134004), F_dv_pair("kind", "defeasible-interpretation"), F_dv_pair("calculation_units", v_work__134040), F_dv_pair("acceptance", v_adoption__133851) });
    }

    public static object F_dvs_social_atomic_class(object v_speech)
    {
        object v_special__137979 = new System.Collections.Generic.List<object>(7) { "proposal", "request", "request_change", "public_claim", "manipulation", "advice", "norm_proposal" };
        object v_i__138080 = R.number("0");
        while (R.Bool(R.lt(v_i__138080, R.count(v_special__137979))))
        {
            if (R.Bool(R.not(R.eq(F_dv_find(v_speech, R.at(v_special__137979, v_i__138080), false), false))))
            {
                return R.cat("formal-", R.at(v_special__137979, v_i__138080));
            }
            v_i__138080 = R.add(v_i__138080, R.number("1"));
        }
        return "ordinary-testimony";
    }

    public static object F_dvs_proposal_reassess(object v_actor, object v_decision, object v_events, object v_time, object v_parent)
    {
        object v_rule__138584 = F_dv_find(F_dv_get(v_actor, "policy"), "proposal_reassessment", false);
        object v_proposal__138653 = F_dv_get(v_decision, "proposal");
        object v_interpretations__138690 = F_dv_find(v_actor, "interpretations", new System.Collections.Generic.List<object>(0) { });
        object v_remaining__138754 = F_dv_find(F_dv_get(v_actor, "policy"), "reason_budget", R.number("0"));
        object v_last__138816 = F_dv_find(v_actor, "last_interpretation_event", "");
        object v_cognitive__138879 = F_dv_find(v_actor, "last_cognitive_event", "");
        object v_seen__138942 = F_dv_find(v_actor, "reassessed_proposals", new System.Collections.Generic.List<object>(0) { });
        object v_local_1__139000 = false;
        if (R.Bool(R.eq(F_dv_get(v_decision, "proposal"), false)))
        {
            v_local_1__139000 = "";
        }
        else
        {
            v_local_1__139000 = F_dv_find(F_dv_get(v_decision, "proposal"), "argument_id", "");
        }
        object v_proposal_id__139156 = v_local_1__139000;
        if (R.Bool(F_dv_and(R.eq(F_dv_get(v_decision, "status"), "accepted"), R.not(F_dv_has(v_seen__138942, v_proposal_id__139156)))))
        {
            if (R.Bool(R.eq(v_rule__138584, false)))
            {
                _ = false;
            }
            else
            {
                if (R.Bool(F_dv_and(F_dv_find(v_rule__138584, "enabled", false), R.lt(R.number("0"), R.count(F_dv_find(v_rule__138584, "basis", new System.Collections.Generic.List<object>(0) { }))))))
                {
                    object v_targets__139439 = F_dv_find(v_rule__138584, "interpretation_ids", new System.Collections.Generic.List<object>(0) { });
                    object v_required__139509 = F_dv_find(v_rule__138584, "required_slots", new System.Collections.Generic.List<object>(0) { });
                    object v_claims__139576 = F_dv_find(v_proposal__138653, "claims", new System.Collections.Generic.List<object>(0) { });
                    object v_complete__139637 = true;
                    object v_i__139673 = R.number("0");
                    if (R.Bool(R.eq(R.count(v_required__139509), R.number("0"))))
                    {
                        v_complete__139637 = false;
                    }
                    while (R.Bool(R.lt(v_i__139673, R.count(v_required__139509))))
                    {
                        object v_j__139834 = R.number("0");
                        object v_found__139864 = false;
                        while (R.Bool(R.lt(v_j__139834, R.count(v_claims__139576))))
                        {
                            if (R.Bool(R.eq(R.at(v_required__139509, v_i__139673), F_dv_get(R.at(v_claims__139576, v_j__139834), "slot_id"))))
                            {
                                v_found__139864 = true;
                            }
                            v_j__139834 = R.add(v_j__139834, R.number("1"));
                        }
                        if (R.Bool(v_found__139864))
                        {
                            _ = false;
                        }
                        else
                        {
                            v_complete__139637 = false;
                        }
                        v_i__139673 = R.add(v_i__139673, R.number("1"));
                    }
                    v_i__139673 = R.number("0");
                    while (R.Bool(R.lt(v_i__139673, R.count(v_targets__139439))))
                    {
                        object v_id__140320 = R.at(v_targets__139439, v_i__139673);
                        object v_old__140360 = F_dv_find(v_interpretations__138690, R.at(v_targets__139439, v_i__139673), false);
                        object v_status__140434 = "insufficient-origin";
                        object v_charged__140489 = R.number("0");
                        object v_before__140525 = F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(4) { F_dv_pair("interpretations", v_interpretations__138690), F_dv_pair("policy", F_dv_with(F_dv_get(v_actor, "policy"), "reason_budget", v_remaining__138754)), F_dv_pair("last_interpretation_event", v_last__138816), F_dv_pair("last_cognitive_event", v_cognitive__138879) });
                        if (R.Bool(R.eq(v_old__140360, false)))
                        {
                            v_status__140434 = "no-personal-interpretation";
                        }
                        else
                        {
                            object v_scope__140878 = F_dv_find(v_old__140360, "scope_check", new System.Collections.Generic.List<object>(0) { });
                            object v_source__140946 = F_dv_find(v_old__140360, "source_event", "");
                            if (R.Bool(R.eq(F_dv_get(v_old__140360, "status"), "accepted")))
                            {
                                _ = false;
                            }
                            else
                            {
                                v_status__140434 = "no-prior-accepted-interpretation";
                            }
                            if (R.Bool(F_dv_and(v_complete__139637, F_dv_and(R.eq(F_dv_get(v_old__140360, "status"), "accepted"), F_dv_and(F_dv_has(F_dv_find(v_proposal__138653, "reassessment_sources", new System.Collections.Generic.List<object>(0) { }), v_source__140946), F_dv_and(R.eq(F_dv_find(v_scope__140878, "literal_scope", ""), F_dv_find(v_rule__138584, "source_scope", "")), R.eq(F_dv_find(v_scope__140878, "interpretation_scope", ""), F_dv_find(v_rule__138584, "target_scope", ""))))))))
                            {
                                if (R.Bool(R.lt(v_remaining__138754, R.number("1"))))
                                {
                                    v_status__140434 = "reason-budget-exhausted";
                                }
                                else
                                {
                                    v_remaining__138754 = R.sub(v_remaining__138754, R.number("1"));
                                    v_charged__140489 = R.number("1");
                                    object v_candidate__141787 = new System.Collections.Generic.List<object>(2) { F_dv_pair("id", v_id__140320), F_dv_pair("utterance_scope", F_dv_find(v_scope__140878, "literal_scope", "")) };
                                    object v_revised__141902 = F_dvs_listener_mapping(F_dv_get(v_actor, "mind"), F_dv_get(v_actor, "policy"), F_dv_find(v_old__140360, "referents", new System.Collections.Generic.List<object>(0) { }), new System.Collections.Generic.List<object>(2) { F_dv_pair("id", v_id__140320), F_dv_pair("utterance_scope", F_dv_find(v_scope__140878, "literal_scope", "")) });
                                    if (R.Bool(R.eq(F_dv_get(F_dv_get(v_revised__141902, "scope_check"), "bridge_truth"), "unknown")))
                                    {
                                        v_status__140434 = "withdrawn";
                                        v_interpretations__138690 = F_dv_with(v_interpretations__138690, v_id__140320, F_dv_with_many(v_old__140360, new System.Collections.Generic.List<object>(3) { F_dv_pair("status", "withdrawn"), F_dv_pair("withdrawal_reason", "paid-scope-bridge-unsupported"), F_dv_pair("proposal_event", v_parent) }));
                                    }
                                    else
                                    {
                                        v_status__140434 = "retained-supported-bridge";
                                    }
                                }
                            }
                        }
                        object v_after__142544 = F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(4) { F_dv_pair("interpretations", v_interpretations__138690), F_dv_pair("policy", F_dv_with(F_dv_get(v_actor, "policy"), "reason_budget", v_remaining__138754)), F_dv_pair("last_interpretation_event", R.cat("e", R.show(R.count(v_events)))), F_dv_pair("last_cognitive_event", R.cat("e", R.show(R.count(v_events)))) });
                        object v_local_2__142828 = false;
                        if (R.Bool(R.eq(v_status__140434, "withdrawn")))
                        {
                            v_local_2__142828 = "implicature_withdrawn";
                        }
                        else
                        {
                            v_local_2__142828 = "proposal_scope_reassessment";
                        }
                        object v_local_3__143064 = false;
                        if (R.Bool(R.eq(v_last__138816, "")))
                        {
                            object v_local_4__143145 = false;
                            if (R.Bool(R.eq(v_old__140360, false)))
                            {
                                v_local_4__143145 = "";
                            }
                            else
                            {
                                v_local_4__143145 = F_dv_find(v_old__140360, "source_event", "");
                            }
                            v_local_3__143064 = F_dv_pair(v_parent, v_local_4__143145);
                        }
                        else
                        {
                            object v_local_5__143459 = false;
                            if (R.Bool(R.eq(v_old__140360, false)))
                            {
                                v_local_5__143459 = "";
                            }
                            else
                            {
                                v_local_5__143459 = F_dv_find(v_old__140360, "source_event", "");
                            }
                            v_local_3__143064 = F_dv_list3(v_parent, v_local_5__143459, v_last__138816);
                        }
                        object v_local_6__143750 = false;
                        if (R.Bool(R.eq(v_old__140360, false)))
                        {
                            v_local_6__143750 = "";
                        }
                        else
                        {
                            v_local_6__143750 = F_dv_find(v_old__140360, "source_event", "");
                        }
                        v_last__138816 = F_dvs_event(v_events, v_local_2__142828, F_dv_get(v_actor, "id"), v_time, F_dvs_speech_cognitive_parents(v_local_3__143064, v_cognitive__138879), F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("status", v_status__140434), F_dv_pair("actor_before", F_dv_without(v_before__140525, "simulations")), F_dv_pair("actor_after", F_dv_without(v_after__142544, "simulations")), F_dv_pair("charged_units", v_charged__140489) }, new System.Collections.Generic.List<object>(6) { F_dv_pair("mind_before", F_dv_get(v_actor, "mind")), F_dv_pair("mind_after", F_dv_get(v_actor, "mind")), F_dv_pair("interpretations_before", F_dv_find(v_before__140525, "interpretations", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("interpretations_after", v_interpretations__138690), F_dv_pair("source_event", v_local_6__143750), F_dv_pair("proposal", v_proposal__138653) }));
                        v_cognitive__138879 = v_last__138816;
                        v_i__139673 = R.add(v_i__139673, R.number("1"));
                    }
                    v_seen__138942 = F_dv_append(v_seen__138942, v_proposal_id__139156);
                }
            }
        }
        return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(5) { F_dv_pair("interpretations", v_interpretations__138690), F_dv_pair("policy", F_dv_with(F_dv_get(v_actor, "policy"), "reason_budget", v_remaining__138754)), F_dv_pair("last_interpretation_event", v_last__138816), F_dv_pair("reassessed_proposals", v_seen__138942), F_dv_pair("last_cognitive_event", v_cognitive__138879) });
    }

    public static object F_dvs_receive_norm(object v_actor, object v_norm, object v_source, object v_parse_event, object v_events, object v_time)
    {
        object v_receipt__144947 = new System.Collections.Generic.List<object>(4) { F_dv_pair("norm", v_norm), F_dv_pair("source_event", v_source), F_dv_pair("parse_event", v_parse_event), F_dv_pair("received_time", v_time) };
        object v_before__145055 = F_dv_find(v_actor, "norm_proposals", new System.Collections.Generic.List<object>(0) { });
        object v_after__145109 = F_dv_with(v_before__145055, F_dv_get(v_norm, "id"), v_receipt__144947);
        object v_event__145159 = F_dvs_event(v_events, "norm_receipt", F_dv_get(v_actor, "id"), v_time, F_dv_pair(v_source, v_parse_event), new System.Collections.Generic.List<object>(3) { F_dv_pair("receipt", v_receipt__144947), F_dv_pair("proposals_before", v_before__145055), F_dv_pair("proposals_after", v_after__145109) });
        return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(2) { F_dv_pair("norm_proposals", v_after__145109), F_dv_pair("last_cognitive_event", v_event__145159) });
    }

    public static object F_dvs_complete_norm(object v_actor, object v_action, object v_applied, object v_events, object v_time, object v_execution_event, object v_observation_event)
    {
        if (R.Bool(R.not(v_applied)))
        {
            return v_actor;
        }
        object v_read_receipt__145650 = F_dv_find(v_action, "norm_receipt", false);
        if (R.Bool(R.not(R.eq(v_read_receipt__145650, false))))
        {
            if (R.Bool((R.Bool(R.not(R.eq(v_observation_event, v_execution_event))) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_read_receipt__145650, "content")), "true")) : (object)(false))))
            {
                v_actor = F_dvs_receive_norm(v_actor, F_dv_get(v_read_receipt__145650, "norm"), v_execution_event, v_observation_event, v_events, v_time);
            }
        }
        object v_decision__145961 = F_dv_find(v_action, "norm_decision", false);
        if (R.Bool(R.eq(v_decision__145961, false)))
        {
            return v_actor;
        }
        object v_receipt__146067 = F_dv_find(F_dv_find(v_actor, "norm_proposals", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_decision__145961, "id"), false);
        _ = F_dv_require(R.not(R.eq(v_receipt__146067, false)), "Norm decision needs an owner's actual content receipt");
        _ = F_dv_require((R.Bool((R.Bool(F_dv_same(F_dv_get(v_receipt__146067, "norm"), F_dv_get(v_decision__145961, "norm"))) ? (object)(R.not(R.eq(F_dv_get(v_receipt__146067, "source_event"), ""))) : (object)(false))) ? (object)(R.not(R.eq(F_dv_get(v_receipt__146067, "parse_event"), ""))) : (object)(false)), "Norm decision receipt must match understood content");
        object v_before__146410 = F_dv_find(F_dv_get(v_actor, "policy"), "norm_rules", new System.Collections.Generic.List<object>(0) { });
        object v_rules__146467 = new System.Collections.Generic.List<object>(0) { };
        object v_i__146486 = R.number("0");
        while (R.Bool(R.lt(v_i__146486, R.count(v_before__146410))))
        {
            if (R.Bool(R.not(R.eq(F_dv_get(R.at(v_before__146410, v_i__146486), "id"), F_dv_get(v_decision__145961, "id")))))
            {
                _ = R.push(v_rules__146467, R.at(v_before__146410, v_i__146486));
            }
            v_i__146486 = R.add(v_i__146486, R.number("1"));
        }
        if (R.Bool(F_dv_get(v_decision__145961, "accepted")))
        {
            object v_norm__146658 = F_dv_get(v_receipt__146067, "norm");
            _ = R.push(v_rules__146467, new System.Collections.Generic.List<object>(7) { F_dv_pair("id", F_dv_get(v_norm__146658, "id")), F_dv_pair("method", F_dv_get(v_norm__146658, "method")), F_dv_pair("mode", F_dv_get(v_norm__146658, "mode")), F_dv_pair("promotes", F_dv_get(v_norm__146658, "promotes")), F_dv_pair("sacrifices", F_dv_get(v_norm__146658, "sacrifices")), F_dv_pair("basis", new System.Collections.Generic.List<object>(4) { F_dv_get(v_norm__146658, "source"), F_dv_get(v_receipt__146067, "source_event"), F_dv_get(v_receipt__146067, "parse_event"), v_execution_event }), F_dv_pair("available_when", F_dv_true()) });
        }
        object v_stance__146939 = new System.Collections.Generic.List<object>(6) { F_dv_pair("accepted", F_dv_get(v_decision__145961, "accepted")), F_dv_pair("norm", F_dv_get(v_receipt__146067, "norm")), F_dv_pair("source_event", F_dv_get(v_receipt__146067, "source_event")), F_dv_pair("parse_event", F_dv_get(v_receipt__146067, "parse_event")), F_dv_pair("execution_event", v_execution_event), F_dv_pair("observation_event", v_observation_event) };
        object v_stances__147160 = F_dv_with(F_dv_find(v_actor, "norm_stances", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_decision__145961, "id"), v_stance__146939);
        object v_event__147243 = F_dvs_event(v_events, "norm_decision", F_dv_get(v_actor, "id"), v_time, new System.Collections.Generic.List<object>(4) { v_execution_event, v_observation_event, F_dv_get(v_receipt__146067, "source_event"), F_dv_get(v_receipt__146067, "parse_event") }, new System.Collections.Generic.List<object>(5) { F_dv_pair("decision", v_stance__146939), F_dv_pair("value_order", F_dv_find(F_dv_get(v_actor, "policy"), "value_order", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("personal_choice", F_dv_find(v_action, "personal_choice", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("rules_before", v_before__146410), F_dv_pair("rules_after", v_rules__146467) });
        return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(3) { F_dv_pair("policy", F_dv_with(F_dv_get(v_actor, "policy"), "norm_rules", v_rules__146467)), F_dv_pair("norm_stances", v_stances__147160), F_dv_pair("last_cognitive_event", v_event__147243) });
    }

    public static object F_dvs_receive_scheduled_speech(object v_listener, object v_message, object v_events, object v_time)
    {
        object v_speech__147794 = F_dv_get(v_message, "speech");
        object v_literal__147826 = F_dv_get(v_speech__147794, "literal_ast");
        object v_bindings__147863 = F_dv_find(v_speech__147794, "referents", new System.Collections.Generic.List<object>(0) { });
        object v_source__147915 = F_dv_get(v_message, "source");
        object v_speaker_id__147947 = F_dv_get(v_message, "speaker");
        object v_action_id__147984 = F_dv_get(v_message, "action_id");
        object v_audibility__148022 = F_dv_get(v_message, "audibility");
        object v_before__148062 = F_dv_get(v_listener, "mind");
        object v_parsed__148093 = F_dvs_speech_map(v_before__148062, v_literal__147826, v_bindings__147863);
        object v_policy__148152 = F_dv_get(v_listener, "policy");
        object v_trust__148185 = F_dvs_speaker_trust(F_dv_get(v_listener, "policy"), v_speaker_id__147947);
        object v_decision__148248 = F_dvs_speech_proposal_decision(v_before__148062, v_policy__148152, v_speech__147794, v_trust__148185);
        object v_candidates_actual__148327 = F_dvs_speech_candidates(v_policy__148152, v_speech__147794);
        object v_after__148393 = v_before__148062;
        object v_profile__148416 = F_dv_observation_profile(new System.Collections.Generic.List<object>(3) { F_dv_pair("perception", R.number("1")), F_dv_pair("testimony", R.number("1")), F_dv_pair("recall", R.number("1")) }, F_dv_find(F_dv_get(v_listener, "policy"), "observation_threshold", R.number("0.5")));
        object v_observations__148561 = F_dvs_speech_received_observations(v_before__148062, v_parsed__148093, v_source__147915, v_speaker_id__147947, F_dv_get(v_message, "received_time"), v_decision__148248);
        if (R.Bool(R.lt(R.number("0"), R.count(v_observations__148561))))
        {
            v_after__148393 = F_dv_get(F_dv_update_mind(v_before__148062, v_observations__148561, F_dv_get(v_before__148062, "revision"), v_profile__148416), "world");
        }
        object v_social_class__148806 = F_dvs_social_atomic_class(v_speech__147794);
        if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(F_dv_find(F_dv_get(v_listener, "policy"), "opt_blackbox", true)) ? (object)(R.eq(v_social_class__148806, "ordinary-testimony")) : (object)(false))) ? (object)(R.eq(F_dv_get(v_decision__148248, "status"), "ordinary")) : (object)(false))) ? (object)(R.eq(F_dv_get(v_decision__148248, "proposal"), false)) : (object)(false))) ? (object)(R.eq(R.count(v_candidates_actual__148327), R.number("0"))) : (object)(false))))
        {
            object v_receipt__149453 = F_dvs_event(v_events, "listener_parse", F_dv_get(v_listener, "id"), v_time, F_dvs_speech_cognitive_parents(F_dv_single(v_source__147915), F_dv_find(v_listener, "last_cognitive_event", "")), F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("mind_before", v_before__148062), F_dv_pair("mind_after", v_after__148393), F_dv_pair("literal", v_parsed__148093), F_dv_pair("observations", v_observations__148561) }, new System.Collections.Generic.List<object>(9) { F_dv_pair("source_event", v_source__147915), F_dv_pair("speech", v_speech__147794), F_dv_pair("presuppositions", F_dvs_speech_presuppositions(v_before__148062, v_speech__147794)), F_dv_pair("audibility", v_audibility__148022), F_dv_pair("observation_profile", v_profile__148416), F_dv_pair("proposal_decision", v_decision__148248), F_dv_pair("social_mode", "atomic-ordinary-testimony"), F_dv_pair("charged_execution_units", R.number("0")), F_dv_pair("charge_owner", "original-action-complete") }));
            return F_dv_with_many(v_listener, new System.Collections.Generic.List<object>(8) { F_dv_pair("mind", v_after__148393), F_dv_pair("received_speeches", F_dv_append(F_dv_find(v_listener, "received_speeches", new System.Collections.Generic.List<object>(0) { }), new System.Collections.Generic.List<object>(4) { F_dv_pair("speaker", v_speaker_id__147947), F_dv_pair("action_id", v_action_id__147984), F_dv_pair("parse_event", v_receipt__149453), F_dv_pair("source_event", v_source__147915) })), F_dv_pair("interpretations", F_dv_find(v_listener, "interpretations", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("last_interpretation_event", F_dv_find(v_listener, "last_interpretation_event", "")), F_dv_pair("proposals", F_dv_find(v_listener, "proposals", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("last_cognitive_event", v_receipt__149453), F_dv_pair("policy", F_dv_with(F_dv_get(v_listener, "policy"), "reason_budget", F_dv_find(F_dv_get(v_listener, "policy"), "reason_budget", R.number("0")))), F_dv_pair("reassessed_proposals", F_dv_find(v_listener, "reassessed_proposals", new System.Collections.Generic.List<object>(0) { })) });
        }
        object v_interpretations__150676 = F_dv_find(v_listener, "interpretations", new System.Collections.Generic.List<object>(0) { });
        object v_last_interpretation__150743 = F_dv_find(v_listener, "last_interpretation_event", "");
        object v_n__150824 = R.number("0");
        object v_parse_event__150838 = false;
        object v_proposal_event__150866 = false;
        if (R.Bool(R.eq(F_dv_get(v_decision__148248, "status"), "ordinary")))
        {
            _ = false;
        }
        else
        {
            v_proposal_event__150866 = F_dvs_event(v_events, "proposal_receipt", F_dv_get(v_listener, "id"), v_time, F_dvs_speech_cognitive_parents(F_dv_single(v_source__147915), F_dv_find(v_listener, "last_cognitive_event", "")), F_dv_with_many(v_decision__148248, new System.Collections.Generic.List<object>(3) { F_dv_pair("source_event", v_source__147915), F_dv_pair("mind_before", v_before__148062), F_dv_pair("mind_after", v_before__148062) }));
            v_proposal_event__150866 = F_dvs_event(v_events, "proposal_adoption", F_dv_get(v_listener, "id"), v_time, F_dv_pair(v_source__147915, v_proposal_event__150866), F_dv_with_many(v_decision__148248, new System.Collections.Generic.List<object>(3) { F_dv_pair("source_event", v_source__147915), F_dv_pair("mind_before", v_before__148062), F_dv_pair("mind_after", v_after__148393) }));
        }
        object v_local_2__151462 = false;
        if (R.Bool(R.eq(v_proposal_event__150866, false)))
        {
            v_local_2__151462 = F_dv_single(v_source__147915);
        }
        else
        {
            v_local_2__151462 = F_dv_pair(v_source__147915, v_proposal_event__150866);
        }
        v_parse_event__150838 = F_dvs_event(v_events, "listener_parse", F_dv_get(v_listener, "id"), v_time, F_dvs_speech_cognitive_parents(v_local_2__151462, F_dv_find(v_listener, "last_cognitive_event", "")), F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("mind_before", v_before__148062), F_dv_pair("mind_after", v_after__148393), F_dv_pair("literal", v_parsed__148093), F_dv_pair("observations", v_observations__148561) }, new System.Collections.Generic.List<object>(9) { F_dv_pair("source_event", v_source__147915), F_dv_pair("speech", v_speech__147794), F_dv_pair("presuppositions", F_dvs_speech_presuppositions(v_before__148062, v_speech__147794)), F_dv_pair("audibility", v_audibility__148022), F_dv_pair("observation_profile", v_profile__148416), F_dv_pair("proposal_decision", v_decision__148248), F_dv_pair("social_mode", "formal-reception"), F_dv_pair("social_reason", v_social_class__148806), F_dv_pair("interpretation_candidates", R.count(v_candidates_actual__148327)) }));
        v_listener = F_dvs_receive_request(v_listener, v_speech__147794, v_parse_event__150838, v_source__147915, v_speaker_id__147947);
        v_listener = F_dv_with(v_listener, "received_speeches", F_dv_append(F_dv_find(v_listener, "received_speeches", new System.Collections.Generic.List<object>(0) { }), new System.Collections.Generic.List<object>(4) { F_dv_pair("speaker", v_speaker_id__147947), F_dv_pair("action_id", v_action_id__147984), F_dv_pair("parse_event", v_parse_event__150838), F_dv_pair("source_event", v_source__147915) }));
        v_listener = F_dvs_receive_public_claim(v_listener, v_speech__147794, v_speaker_id__147947, v_source__147915, v_parse_event__150838);
        v_listener = F_dvs_receive_player_influence(v_listener, v_speech__147794, v_speaker_id__147947, v_source__147915, v_parse_event__150838);
        v_listener = F_dvs_receive_advice(v_listener, v_speech__147794, v_speaker_id__147947, v_source__147915, v_parse_event__150838, v_parsed__148093);
        while (R.Bool(R.lt(v_n__150824, R.count(v_candidates_actual__148327))))
        {
            object v_candidate__152837 = R.at(v_candidates_actual__148327, v_n__150824);
            object v_interpretation__152882 = F_dvs_listener_mapping(v_after__148393, v_policy__148152, v_bindings__147863, R.at(v_candidates_actual__148327, v_n__150824));
            object v_interpretations_before__152982 = F_dv_clone(v_interpretations__150676);
            v_interpretation__152882 = F_dv_with_many(v_interpretation__152882, new System.Collections.Generic.List<object>(2) { F_dv_pair("source_event", v_source__147915), F_dv_pair("referents", v_bindings__147863) });
            object v_local_3__153148 = false;
            if (R.Bool(F_dv_has_key(v_interpretations__150676, F_dv_get(v_candidate__152837, "id"))))
            {
                v_local_3__153148 = R.eq(F_dv_get(F_dv_get(v_interpretations__150676, F_dv_get(v_candidate__152837, "id")), "status"), "accepted");
            }
            else
            {
                v_local_3__153148 = false;
            }
            if (R.Bool(F_dv_and(R.eq(F_dv_get(v_interpretation__152882, "status"), "withdrawn"), R.not(v_local_3__153148))))
            {
                v_interpretation__152882 = F_dv_with(v_interpretation__152882, "status", "rejected");
            }
            v_interpretations__150676 = F_dv_with(v_interpretations__150676, F_dv_get(v_candidate__152837, "id"), v_interpretation__152882);
            object v_local_4__153585 = false;
            if (R.Bool(R.eq(F_dv_get(v_interpretation__152882, "status"), "withdrawn")))
            {
                v_local_4__153585 = "implicature_withdrawn";
            }
            else
            {
                v_local_4__153585 = "implicature";
            }
            object v_local_5__153760 = false;
            if (R.Bool(R.eq(v_last_interpretation__150743, "")))
            {
                v_local_5__153760 = F_dv_pair(v_source__147915, v_parse_event__150838);
            }
            else
            {
                v_local_5__153760 = F_dv_list3(v_source__147915, v_parse_event__150838, v_last_interpretation__150743);
            }
            v_last_interpretation__150743 = F_dvs_event(v_events, v_local_4__153585, F_dv_get(v_listener, "id"), v_time, v_local_5__153760, F_dv_with_many(v_interpretation__152882, new System.Collections.Generic.List<object>(5) { F_dv_pair("source_event", v_source__147915), F_dv_pair("mind_before", v_after__148393), F_dv_pair("mind_after", v_after__148393), F_dv_pair("interpretations_before", v_interpretations_before__152982), F_dv_pair("interpretations_after", v_interpretations__150676) }));
            v_n__150824 = R.add(v_n__150824, R.number("1"));
        }
        object v_local_6__154267 = false;
        if (R.Bool(R.eq(F_dv_get(v_decision__148248, "status"), "ordinary")))
        {
            v_local_6__154267 = F_dv_find(v_listener, "proposals", new System.Collections.Generic.List<object>(0) { });
        }
        else
        {
            v_local_6__154267 = F_dv_append(F_dv_find(v_listener, "proposals", new System.Collections.Generic.List<object>(0) { }), F_dv_with(v_decision__148248, "source_event", v_source__147915));
        }
        v_listener = F_dvs_proposal_reassess(F_dv_with_many(v_listener, new System.Collections.Generic.List<object>(5) { F_dv_pair("mind", v_after__148393), F_dv_pair("interpretations", v_interpretations__150676), F_dv_pair("last_interpretation_event", v_last_interpretation__150743), F_dv_pair("proposals", v_local_6__154267), F_dv_pair("last_cognitive_event", v_parse_event__150838) }), v_decision__148248, v_events, v_time, v_parse_event__150838);
        object v_norm__154761 = F_dv_find(v_speech__147794, "norm_proposal", false);
        if (R.Bool((R.Bool((R.Bool(R.not(R.eq(v_norm__154761, false))) ? (object)(F_dv_same(v_parsed__148093, v_literal__147826)) : (object)(false))) ? (object)(R.eq(F_dv_truth(v_after__148393, v_parsed__148093), "true")) : (object)(false))))
        {
            v_listener = F_dvs_receive_norm(v_listener, v_norm__154761, v_source__147915, v_parse_event__150838, v_events, v_time);
        }
        return v_listener;
    }

    public static object F_dvs_drain_received_speech(object v_actor, object v_events, object v_time)
    {
        object v_messages__155064 = F_dv_find(v_actor, "pending_heard", new System.Collections.Generic.List<object>(0) { });
        object v_current__155119 = v_actor;
        if (R.Bool(R.lt(R.number("0"), R.count(v_messages__155064))))
        {
            v_current__155119 = F_dv_with(v_actor, "pending_heard", new System.Collections.Generic.List<object>(0) { });
        }
        object v_i__155228 = R.number("0");
        while (R.Bool(R.lt(v_i__155228, R.count(v_messages__155064))))
        {
            v_current__155119 = F_dvs_receive_scheduled_speech(v_current__155119, R.at(v_messages__155064, v_i__155228), v_events, v_time);
            v_i__155228 = R.add(v_i__155228, R.number("1"));
        }
        return v_current__155119;
    }

    public static object F_dvs_speech_owned(object v_actors, object v_speaker_index, object v_action, object v_events, object v_time, object v_parent, object v_physical)
    {
        object v_speaker__155481 = R.at(v_actors, v_speaker_index);
        object v_speech__155521 = F_dv_get(v_action, "speech");
        object v_speaker_mind__155552 = F_dv_get(v_speaker__155481, "mind");
        object v_bindings__155588 = F_dv_find(v_speech__155521, "referents", new System.Collections.Generic.List<object>(0) { });
        object v_literal__155640 = F_dv_get(v_speech__155521, "literal_ast");
        object v_claim__155677 = F_dvs_speech_map(v_speaker_mind__155552, v_literal__155640, v_bindings__155588);
        object v_listeners__155741 = F_dv_find(v_action, "listeners", new System.Collections.Generic.List<object>(0) { });
        object v_next__155794 = v_actors;
        object v_i__155816 = R.number("0");
        object v_source__155830 = false;
        object v_visited__155853 = new System.Collections.Generic.List<object>(0) { };
        v_source__155830 = F_dvs_event(v_events, "speech_literal", F_dv_get(v_speaker__155481, "id"), v_time, F_dvs_speech_cognitive_parents(F_dv_single(v_parent), F_dv_find(v_speaker__155481, "last_cognitive_event", "")), F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("literal", v_literal__155640), F_dv_pair("claim", v_claim__155677), F_dv_pair("mind_before", v_speaker_mind__155552), F_dv_pair("speech", v_speech__155521) }, "speaker_truth", F_dv_truth(v_speaker_mind__155552, v_claim__155677)));
        if (R.Bool(R.not(R.eq(F_dv_truth(v_speaker_mind__155552, v_claim__155677), "true"))))
        {
            _ = F_dvs_event(v_events, "speech_rejected", F_dv_get(v_speaker__155481, "id"), v_time, F_dv_single(v_source__155830), new System.Collections.Generic.List<object>(2) { F_dv_pair("reason", "speaker-truth-not-supported"), F_dv_pair("source_event", v_source__155830) });
        }
        else
        {
            object sentRequest = F_dv_find(v_speech__155521, "request", false);
            if (!R.Bool(R.eq(sentRequest, false)))
            {
                object history = F_dv_find(v_speaker__155481, "sent_requests", new List<object>());
                object entry = new List<object> { F_dv_pair("action_id", F_dv_get(v_action, "id")), F_dv_pair("target", F_dv_get(sentRequest, "target")), F_dv_pair("topic", F_dv_get(sentRequest, "topic")), F_dv_pair("source_event", v_source__155830) };
                v_speaker__155481 = F_dv_with(v_speaker__155481, "sent_requests", F_dv_append(history, entry));
                v_next__155794 = F_dvs_replace(v_next__155794, v_speaker_index, v_speaker__155481);
            }
            while (R.Bool(R.lt(v_i__155816, R.count(v_listeners__155741))))
            {
                object v_listener_id__156451 = R.at(v_listeners__155741, v_i__155816);
                object v_j__156494 = R.number("0");
                object v_target_index__156516 = R.sub(R.number("0"), R.number("1"));
                _ = F_dv_require(R.not(F_dv_has(v_visited__155853, v_listener_id__156451)), "Repeated speech listener");
                _ = R.push(v_visited__155853, v_listener_id__156451);
                while (R.Bool(R.lt(v_j__156494, R.count(v_next__155794))))
                {
                    if (R.Bool(R.eq(F_dv_get(R.at(v_next__155794, v_j__156494), "id"), v_listener_id__156451)))
                    {
                        v_target_index__156516 = v_j__156494;
                    }
                    v_j__156494 = R.add(v_j__156494, R.number("1"));
                }
                if (R.Bool(R.lt(v_target_index__156516, R.number("0"))))
                {
                    _ = F_dvs_event(v_events, "speech_unheard", v_listener_id__156451, v_time, F_dv_single(v_source__155830), new System.Collections.Generic.List<object>(2) { F_dv_pair("reason", "unknown-listener"), F_dv_pair("source_event", v_source__155830) });
                }
                else
                {
                    object v_listener__157035 = R.at(v_next__155794, v_target_index__156516);
                    if (R.Bool(R.not(F_dvs_speech_hearing(v_speaker__155481, v_listener__157035, v_speech__155521, v_physical))))
                    {
                        _ = F_dvs_event(v_events, "speech_unheard", v_listener_id__156451, v_time, F_dv_single(v_source__155830), new System.Collections.Generic.List<object>(2) { F_dv_pair("reason", "no-audibility-grant"), F_dv_pair("source_event", v_source__155830) });
                    }
                    else
                    {
                        object v_message__157346 = new System.Collections.Generic.List<object>(6) { F_dv_pair("speech", v_speech__155521), F_dv_pair("source", v_source__155830), F_dv_pair("speaker", F_dv_get(v_speaker__155481, "id")), F_dv_pair("action_id", F_dv_get(v_action, "id")), F_dv_pair("received_time", v_time), F_dv_pair("audibility", new System.Collections.Generic.List<object>(3) { F_dv_pair("speaker_location", F_dvs_emission_location(v_speaker__155481, v_speech__155521, v_physical)), F_dv_pair("listener_location", F_dv_find(v_listener__157035, "location", "")), F_dv_pair("hearing_grants", F_dv_find(v_listener__157035, "hearing_grants", new System.Collections.Generic.List<object>(0) { })) }) };
                        if (R.Bool(R.not(R.eq(F_dv_find(v_listener__157035, "scheduled_action", false), false))))
                        {
                            v_next__155794 = F_dvs_replace(v_next__155794, v_target_index__156516, F_dv_with(v_listener__157035, "pending_heard", F_dv_append(F_dv_find(v_listener__157035, "pending_heard", new System.Collections.Generic.List<object>(0) { }), v_message__157346)));
                            _ = F_dvs_event(v_events, "speech_received_pending", F_dv_get(v_listener__157035, "id"), v_time, new System.Collections.Generic.List<object>(1) { v_source__155830 }, new System.Collections.Generic.List<object>(2) { F_dv_pair("source_event", v_source__155830), F_dv_pair("audibility", F_dv_get(v_message__157346, "audibility")) });
                        }
                        else
                        {
                            v_next__155794 = F_dvs_replace(v_next__155794, v_target_index__156516, F_dvs_receive_scheduled_speech(v_listener__157035, v_message__157346, v_events, v_time));
                        }
                    }
                }
                v_i__155816 = R.add(v_i__155816, R.number("1"));
            }
        }
        return v_next__155794;
    }

    public static object F_dvs_complete_owned_decision(object v_actor, object v_action, object v_success, object v_events, object v_execution_event, object v_time)
    {
        object v_decision__158347 = F_dv_find(v_action, "owned_decision", false);
        if (R.Bool((R.Bool(R.not(v_success)) ? (object)(true) : (object)(R.eq(v_decision__158347, false)))))
        {
            return v_actor;
        }
        object v_snapshot__158469 = F_dvs_owned_decision_completion_selection(v_actor, v_action, v_events, v_execution_event);
        object v_execution__158568 = F_dvs_find_event_by_id(v_events, v_execution_event);
        if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(v_snapshot__158469, false)) ? (object)(true) : (object)(R.eq(v_execution__158568, false)))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_execution__158568, "type"), "action_execution"))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_execution__158568, "actor"), F_dv_get(v_actor, "id")))))))
        {
            _ = F_dvs_event(v_events, "owned_decision_unavailable", F_dv_get(v_actor, "id"), v_time, new System.Collections.Generic.List<object>(1) { v_execution_event }, new System.Collections.Generic.List<object>(2) { F_dv_pair("decision", v_decision__158347), F_dv_pair("reason", "current-context-or-actual-selection-unavailable") });
            return v_actor;
        }
        if (R.Bool(R.not(R.eq(F_dv_get(F_dv_get(F_dv_get(F_dv_get(v_execution__158568, "data"), "receipt"), "execution"), "status"), "applied"))))
        {
            return v_actor;
        }
        object v_inputs__159032 = F_dvs_owned_decision_inputs(v_actor, v_decision__158347, F_dv_get(v_actor, "mind"));
        object v_previous__159104 = F_dv_find(F_dv_find(v_actor, "owned_decisions", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_decision__158347, "id"), false);
        object v_previous_event__159190 = "";
        if (R.Bool(R.not(R.eq(v_previous__159104, false))))
        {
            v_previous_event__159190 = F_dv_find(v_previous__159104, "event", "");
        }
        object v_receipt__159300 = new System.Collections.Generic.List<object>(12) { F_dv_pair("id", F_dv_get(v_decision__158347, "id")), F_dv_pair("stance", F_dv_get(v_decision__158347, "stance")), F_dv_pair("context", F_dv_get(v_inputs__159032, "context")), F_dv_pair("basis", F_dv_get(v_decision__158347, "basis")), F_dv_pair("declaration", v_decision__158347), F_dv_pair("selection_sources", F_dv_get(v_snapshot__158469, "sources")), F_dv_pair("completion_sources", F_dv_get(v_inputs__159032, "sources")), F_dv_pair("selection_event", F_dv_get(v_snapshot__158469, "selection_event")), F_dv_pair("execution_event", v_execution_event), F_dv_pair("previous_event", v_previous_event__159190), F_dv_pair("time", v_time), F_dv_pair("meaning", "actual-owner-decision-not-performance-or-outcome") };
        object v_parents__159711 = new System.Collections.Generic.List<object>(2) { v_execution_event, F_dv_get(v_snapshot__158469, "selection_event") };
        object v_i__159773 = R.number("0");
        while (R.Bool(R.lt(v_i__159773, R.count(F_dv_get(F_dv_get(v_snapshot__158469, "sources"), "requests")))))
        {
            object v_request__159842 = F_dv_get(R.at(F_dv_get(F_dv_get(v_snapshot__158469, "sources"), "requests"), v_i__159773), "receipt");
            if (R.Bool(R.not(R.eq(v_request__159842, false))))
            {
                v_parents__159711 = F_dv_explanation_merge(v_parents__159711, new System.Collections.Generic.List<object>(2) { F_dv_get(v_request__159842, "source_event"), F_dv_get(v_request__159842, "parse_event") });
            }
            v_i__159773 = R.add(v_i__159773, R.number("1"));
        }
        object v_event__160041 = F_dvs_event(v_events, "owned_decision", F_dv_get(v_actor, "id"), v_time, v_parents__159711, v_receipt__159300);
        object v_recorded__160127 = F_dv_with(v_receipt__159300, "event", v_event__160041);
        return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(3) { F_dv_pair("owned_decisions", F_dv_with(F_dv_find(v_actor, "owned_decisions", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_decision__158347, "id"), v_recorded__160127)), F_dv_pair("owned_decision_history", F_dv_append(F_dv_find(v_actor, "owned_decision_history", new System.Collections.Generic.List<object>(0) { }), v_recorded__160127)), F_dv_pair("last_cognitive_event", v_event__160041) });
    }

    public static object F_dvs_complete_advice(object v_actor, object v_action, object v_success, object v_events, object v_time, object v_execution_event)
    {
        object v_decision__160506 = F_dv_find(v_action, "advice_decision", false);
        if (R.Bool((R.Bool(R.not(v_success)) ? (object)(true) : (object)(R.eq(v_decision__160506, false)))))
        {
            return v_actor;
        }
        if (R.Bool(R.not(F_dvs_advice_decision_ready(v_actor, v_action, F_dv_get(v_actor, "mind")))))
        {
            _ = F_dvs_event(v_events, "advice_decision_unavailable", F_dv_get(v_actor, "id"), v_time, new System.Collections.Generic.List<object>(1) { v_execution_event }, new System.Collections.Generic.List<object>(2) { F_dv_pair("decision", v_decision__160506), F_dv_pair("reason", "receipt-or-owner-assessment-unavailable-at-complete") });
            return v_actor;
        }
        object v_receipt__160896 = F_dv_get(F_dv_find(v_actor, "received_advice", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_decision__160506, "advice_id"));
        object v_selection__160980 = F_dvs_heard_before_selection(v_events, v_actor, v_action, v_execution_event, F_dv_get(v_receipt__160896, "parse_event"), F_dv_get(v_receipt__160896, "source_event"));
        if (R.Bool(R.eq(v_selection__160980, false)))
        {
            _ = F_dvs_event(v_events, "advice_decision_unavailable", F_dv_get(v_actor, "id"), v_time, new System.Collections.Generic.List<object>(3) { v_execution_event, F_dv_get(v_receipt__160896, "source_event"), F_dv_get(v_receipt__160896, "parse_event") }, new System.Collections.Generic.List<object>(2) { F_dv_pair("decision", v_decision__160506), F_dv_pair("reason", "advice-not-before-decision-selection") });
            return v_actor;
        }
        object v_assessment__161367 = F_dvs_advice_assessment(v_actor, v_receipt__160896, F_dv_get(v_decision__160506, "assessment_action_id"), F_dv_get(v_actor, "mind"));
        object v_status__161469 = "declined";
        if (R.Bool(F_dv_get(v_decision__160506, "accepted")))
        {
            v_status__161469 = "accepted-for-consideration";
        }
        object v_stance__161569 = new System.Collections.Generic.List<object>(10) { F_dv_pair("advice_id", F_dv_get(v_decision__160506, "advice_id")), F_dv_pair("accepted", F_dv_get(v_decision__160506, "accepted")), F_dv_pair("status", v_status__161469), F_dv_pair("source_event", F_dv_get(v_receipt__160896, "source_event")), F_dv_pair("parse_event", F_dv_get(v_receipt__160896, "parse_event")), F_dv_pair("selection_event", F_dv_get(v_selection__160980, "event")), F_dv_pair("execution_event", v_execution_event), F_dv_pair("assessment", v_assessment__161367), F_dv_pair("time", v_time), F_dv_pair("meaning", "owner-stance-not-target-execution-or-causal-proof") };
        object v_event__161919 = F_dvs_event(v_events, "advice_decision", F_dv_get(v_actor, "id"), v_time, new System.Collections.Generic.List<object>(3) { v_execution_event, F_dv_get(v_receipt__160896, "source_event"), F_dv_get(v_receipt__160896, "parse_event") }, v_stance__161569);
        return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(2) { F_dv_pair("advice_stances", F_dv_with(F_dv_find(v_actor, "advice_stances", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_decision__160506, "advice_id"), F_dv_with(v_stance__161569, "event", v_event__161919))), F_dv_pair("last_cognitive_event", v_event__161919) });
    }
}
