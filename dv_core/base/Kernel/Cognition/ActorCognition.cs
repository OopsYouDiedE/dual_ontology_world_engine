using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Cognition / ActorCognition: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_actor_can_act(object v_actor)
    {
        object v_stage__48325 = F_dv_find(v_actor, "erosion_stage", "early");
        return (R.Bool((R.Bool(R.not(R.eq(v_stage__48325, "terminal"))) ? (object)(R.not(R.eq(v_stage__48325, "death"))) : (object)(false))) ? (object)(R.not(F_dv_find(v_actor, "incapacitated", false))) : (object)(false));
    }

    public static object F_dvs_cognitive_seed_next(object v_seed)
    {
        object v_n__48599 = R.add(R.mul(v_seed, R.number("48271")), R.number("1"));
        return R.sub(v_n__48599, R.mul(R.floor(R.div(v_n__48599, R.number("2147483647"))), R.number("2147483647")));
    }

    public static object F_dvs_initialize_cognition(object v_actor, object v_seed, object v_ordinal)
    {
        _ = F_dv_require((R.Bool(R.not(R.lt(v_seed, R.number("0")))) ? (object)(R.eq(R.floor(v_seed), v_seed)) : (object)(false)), "Opening seed must be a nonnegative integer");
        object v_declaration__48832 = F_dv_find(v_actor, "cognitive_initialization", false);
        if (R.Bool(R.eq(v_declaration__48832, false)))
        {
            if (R.Bool(F_dv_has_key(v_actor, "erosion")))
            {
                return F_dv_with(v_actor, "first_day_mind", F_dv_get(v_actor, "mind"));
            }
            return v_actor;
        }
        _ = F_dv_require(R.lt(R.number("0"), R.count(F_dv_find(v_declaration__48832, "basis", new System.Collections.Generic.List<object>(0) { }))), "Cognitive initialization needs declared provenance");
        object v_stream__49177 = F_dvs_cognitive_seed_next(R.add(v_seed, v_ordinal));
        object v_policy__49234 = F_dv_get(v_actor, "policy");
        object v_samples__49264 = F_dv_find(v_declaration__48832, "samples", new System.Collections.Generic.List<object>(0) { });
        object v_selected__49318 = new System.Collections.Generic.List<object>(0) { };
        object v_i__49340 = R.number("0");
        while (R.Bool(R.lt(v_i__49340, R.count(v_samples__49264))))
        {
            object v_sample__49391 = R.at(v_samples__49264, v_i__49340);
            _ = F_dv_require(R.lt(R.number("0"), R.count(F_dv_find(v_sample__49391, "basis", new System.Collections.Generic.List<object>(0) { }))), "Cognitive sample needs provenance");
            object v_options__49523 = F_dv_get(v_sample__49391, "options");
            _ = F_dv_require(R.lt(R.number("0"), R.count(v_options__49523)), "Empty cognitive sample domain");
            v_stream__49177 = F_dvs_cognitive_seed_next(v_stream__49177);
            object v_index__49684 = R.sub(v_stream__49177, R.mul(R.floor(R.div(v_stream__49177, R.count(v_options__49523))), R.count(v_options__49523)));
            v_policy__49234 = F_dv_with(v_policy__49234, F_dv_get(v_sample__49391, "key"), R.at(v_options__49523, v_index__49684));
            v_selected__49318 = F_dv_with(v_selected__49318, F_dv_get(v_sample__49391, "key"), R.at(v_options__49523, v_index__49684));
            v_i__49340 = R.add(v_i__49340, R.number("1"));
        }
        v_policy__49234 = F_dv_with_many(v_policy__49234, F_dv_find(v_declaration__48832, "profile", new System.Collections.Generic.List<object>(0) { }));
        return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(4) { F_dv_pair("policy", v_policy__49234), F_dv_pair("cognitive_seed", v_seed), F_dv_pair("cognitive_samples", v_selected__49318), F_dv_pair("first_day_mind", F_dv_get(v_actor, "mind")) });
    }

    public static object F_dvs_erosion_update(object v_actor, object v_physical, object v_time)
    {
        object v_erosion__50256 = F_dv_find(v_actor, "erosion", false);
        if (R.Bool(R.eq(v_erosion__50256, false)))
        {
            return v_actor;
        }
        object v_incapacitated__50353 = false;
        if (R.Bool(F_dv_has_key(v_erosion__50256, "incapacity_when")))
        {
            v_incapacitated__50353 = R.eq(F_dv_truth(v_physical, F_dv_get(v_erosion__50256, "incapacity_when")), "true");
        }
        object v_exposure_cell__50508 = F_dv_read(v_physical, F_dv_get(v_erosion__50256, "physical_ref"));
        object v_exposure__50572 = R.number("0");
        if (R.Bool((R.Bool(F_dv_uncontested(v_exposure_cell__50508)) ? (object)(R.is_numeric_value(F_dv_get(v_exposure_cell__50508, "value"))) : (object)(false))))
        {
            v_exposure__50572 = F_dv_get(v_exposure_cell__50508, "value");
        }
        v_exposure__50572 = R.mul(v_exposure__50572, R.add(R.number("1"), R.mul(F_dv_find(v_actor, "resonance_coupling", R.number("0")), F_dv_find(v_erosion__50256, "coupling_factor", R.number("0")))));
        object v_stages__50828 = new System.Collections.Generic.List<object>(5) { "early", "middle", "late", "terminal", "death" };
        object v_rank__50894 = F_dv_find(v_actor, "erosion_rank", R.number("0"));
        object v_i__50943 = R.number("0");
        while (R.Bool(R.lt(v_i__50943, R.count(F_dv_get(v_erosion__50256, "thresholds")))))
        {
            object v_threshold__51005 = R.at(F_dv_get(v_erosion__50256, "thresholds"), v_i__50943);
            if (R.Bool((R.Bool(R.not(R.lt(v_exposure__50572, F_dv_get(v_threshold__51005, "at")))) ? (object)(R.lt(v_rank__50894, F_dv_get(v_threshold__51005, "rank"))) : (object)(false))))
            {
                if (R.Bool((R.Bool(R.not(F_dv_has_key(v_threshold__51005, "when"))) ? (object)(true) : (object)(R.eq(F_dv_truth(v_physical, F_dv_get(v_threshold__51005, "when")), "true")))))
                {
                    v_rank__50894 = F_dv_get(v_threshold__51005, "rank");
                }
            }
            v_i__50943 = R.add(v_i__50943, R.number("1"));
        }
        _ = F_dv_require((R.Bool(R.not(R.lt(v_rank__50894, R.number("0")))) ? (object)(R.lt(v_rank__50894, R.count(v_stages__50828))) : (object)(false)), "Invalid erosion stage");
        object v_memory_period__51343 = F_dv_find(v_erosion__50256, "memory_period", R.number("1440"));
        _ = F_dv_require(R.lt(R.number("0"), v_memory_period__51343), "Erosion memory period must be positive");
        object v_memory_epoch__51483 = R.floor(R.div(v_time, v_memory_period__51343));
        object v_truncate_memory__51537 = (R.Bool(R.not(R.lt(v_rank__50894, R.number("2")))) ? (object)((R.Bool(R.not(F_dv_find(v_actor, "erosion_memory_truncated", false))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_actor, "erosion_memory_epoch", R.sub(R.number("0"), R.number("1"))), v_memory_epoch__51483))))) : (object)(false));
        object v_incapacity_changed__51695 = (R.Bool(F_dv_has_key(v_erosion__50256, "incapacity_when")) ? (object)((R.Bool(R.not(F_dv_has_key(v_actor, "incapacitated"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_actor, "incapacitated", false), v_incapacitated__50353))))) : (object)(false));
        if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(F_dv_find(v_actor, "erosion_rank", R.sub(R.number("0"), R.number("1"))), v_rank__50894)) ? (object)(R.eq(F_dv_find(v_actor, "erosion_stage", ""), R.at(v_stages__50828, v_rank__50894))) : (object)(false))) ? (object)(R.not(v_incapacity_changed__51695)) : (object)(false))) ? (object)(R.not(v_truncate_memory__51537)) : (object)(false))))
        {
            return v_actor;
        }
        object v_result__52046 = F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(2) { F_dv_pair("erosion_rank", v_rank__50894), F_dv_pair("erosion_stage", R.at(v_stages__50828, v_rank__50894)) });
        if (R.Bool(F_dv_has_key(v_erosion__50256, "incapacity_when")))
        {
            v_result__52046 = F_dv_with(v_result__52046, "incapacitated", v_incapacitated__50353);
        }
        if (R.Bool(v_truncate_memory__51537))
        {
            _ = F_dv_require(F_dv_has_key(v_actor, "first_day_mind"), "Late erosion needs declared first-day memory snapshot");
            object v_old__52391 = F_dv_get(v_actor, "mind");
            object v_memory__52420 = F_dv_with_many(F_dv_get(v_actor, "first_day_mind"), new System.Collections.Generic.List<object>(2) { F_dv_pair("time", v_time), F_dv_pair("revision", R.add(F_dv_get(v_old__52391, "revision"), R.number("1"))) });
            v_result__52046 = F_dv_with_many(v_result__52046, new System.Collections.Generic.List<object>(14) { F_dv_pair("mind", v_memory__52420), F_dv_pair("erosion_memory_truncated", true), F_dv_pair("erosion_memory_epoch", v_memory_epoch__51483), F_dv_pair("simulations", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("interpretations", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("response_models", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("proposals", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("received_speeches", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("received_advice", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("owned_decisions", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("pending_heard", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("observed_public_claims", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("observed_claim_conflicts", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("pending_player_influences", new System.Collections.Generic.List<object>(0) { }) });
        }
        return v_result__52046;
    }

    public static object F_dvs_receive_public_claim(object v_listener, object v_speech, object v_speaker, object v_source, object v_parse_event)
    {
        object v_claim__53092 = F_dv_find(v_speech, "public_claim", false);
        if (R.Bool(R.eq(v_claim__53092, false)))
        {
            return v_listener;
        }
        _ = F_dv_require(R.lt(R.number("0"), R.count(F_dv_find(v_claim__53092, "basis", new System.Collections.Generic.List<object>(0) { }))), "Public claim needs content provenance");
        object v_history__53293 = F_dv_find(v_listener, "observed_public_claims", new System.Collections.Generic.List<object>(0) { });
        object v_conflicts__53359 = F_dv_find(v_listener, "observed_claim_conflicts", new System.Collections.Generic.List<object>(0) { });
        object v_i__53429 = R.number("0");
        while (R.Bool(R.lt(v_i__53429, R.count(v_history__53293))))
        {
            object v_prior__53480 = R.at(v_history__53293, v_i__53429);
            if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_prior__53480, "speaker"), v_speaker)) ? (object)(R.eq(F_dv_get(v_prior__53480, "key"), F_dv_get(v_claim__53092, "key"))) : (object)(false))) ? (object)(F_dv_has(F_dv_find(v_claim__53092, "conflicts_with", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_prior__53480, "value"))) : (object)(false))))
            {
                v_conflicts__53359 = F_dv_append(v_conflicts__53359, new System.Collections.Generic.List<object>(6) { F_dv_pair("speaker", v_speaker), F_dv_pair("key", F_dv_get(v_claim__53092, "key")), F_dv_pair("previous_source", F_dv_get(v_prior__53480, "source_event")), F_dv_pair("source_event", v_source), F_dv_pair("parse_event", v_parse_event), F_dv_pair("basis", F_dv_get(v_claim__53092, "basis")) });
            }
            v_i__53429 = R.add(v_i__53429, R.number("1"));
        }
        object v_received__53849 = new System.Collections.Generic.List<object>(5) { F_dv_pair("speaker", v_speaker), F_dv_pair("key", F_dv_get(v_claim__53092, "key")), F_dv_pair("value", F_dv_get(v_claim__53092, "value")), F_dv_pair("source_event", v_source), F_dv_pair("parse_event", v_parse_event) };
        return F_dv_with_many(v_listener, new System.Collections.Generic.List<object>(2) { F_dv_pair("observed_public_claims", F_dv_append(v_history__53293, v_received__53849)), F_dv_pair("observed_claim_conflicts", v_conflicts__53359) });
    }

    public static object F_dvs_recorded_event_position(object v_events, object v_id)
    {
        v_events = R.snapshot(v_events);
        object v_i__54190 = R.sub(R.count(v_events), R.number("1"));
        while (R.Bool(R.not(R.lt(v_i__54190, R.number("0")))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(v_events, v_i__54190), "id"), v_id)))
            {
                return v_i__54190;
            }
            v_i__54190 = R.sub(v_i__54190, R.number("1"));
        }
        return R.sub(R.number("0"), R.number("1"));
    }

    public static object F_dvs_action_selection_evidence(object v_events, object v_actor, object v_action, object v_execution_event)
    {
        object v_cursor__54556 = v_execution_event;
        object v_seen__54589 = new System.Collections.Generic.List<object>(0) { };
        while (R.Bool((R.Bool(R.not(R.eq(v_cursor__54556, ""))) ? (object)(R.not(F_dv_has(v_seen__54589, v_cursor__54556))) : (object)(false))))
        {
            _ = R.push(v_seen__54589, v_cursor__54556);
            object v_current__54694 = F_dvs_find_event_by_id(v_events, v_cursor__54556);
            if (R.Bool((R.Bool(R.eq(v_current__54694, false)) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_current__54694, "actor"), F_dv_get(v_actor, "id")))))))
            {
                return false;
            }
            if (R.Bool(R.eq(F_dv_get(v_current__54694, "type"), "action_selected")))
            {
                if (R.Bool(R.eq(F_dv_find(F_dv_find(F_dv_get(v_current__54694, "data"), "action", new System.Collections.Generic.List<object>(0) { }), "id", ""), F_dv_get(v_action, "id"))))
                {
                    return new System.Collections.Generic.List<object>(3) { F_dv_pair("event", F_dv_get(v_current__54694, "id")), F_dv_pair("position", F_dvs_recorded_event_position(v_events, F_dv_get(v_current__54694, "id"))), F_dv_pair("time", F_dv_get(v_current__54694, "time")) };
                }
                return false;
            }
            if (R.Bool((R.Bool(R.not(F_dv_has(new System.Collections.Generic.List<object>(5) { "action_execution", "host_completion", "cost_quote", "planning_choice", "action_scheduled" }, F_dv_get(v_current__54694, "type")))) ? (object)(true) : (object)(R.not(R.eq(R.count(F_dv_get(v_current__54694, "parents")), R.number("1")))))))
            {
                return false;
            }
            v_cursor__54556 = R.at(F_dv_get(v_current__54694, "parents"), R.number("0"));
        }
        return false;
    }

    public static object F_dvs_heard_before_selection(object v_events, object v_actor, object v_action, object v_execution_event, object v_parse_event, object v_source_event)
    {
        object v_selection__55457 = F_dvs_action_selection_evidence(v_events, v_actor, v_action, v_execution_event);
        object v_parsed__55547 = F_dvs_find_event_by_id(v_events, v_parse_event);
        if (R.Bool((R.Bool(R.eq(v_selection__55457, false)) ? (object)(true) : (object)(R.eq(v_parsed__55547, false)))))
        {
            return false;
        }
        if (R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_parsed__55547, "type"), "listener_parse"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_parsed__55547, "actor"), F_dv_get(v_actor, "id")))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(F_dv_get(v_parsed__55547, "data"), "source_event", ""), v_source_event))))))
        {
            return false;
        }
        object v_position__55822 = F_dvs_recorded_event_position(v_events, v_parse_event);
        if (R.Bool((R.Bool(R.lt(v_position__55822, R.number("0"))) ? (object)(true) : (object)(R.not(R.lt(v_position__55822, F_dv_get(v_selection__55457, "position")))))))
        {
            return false;
        }
        return v_selection__55457;
    }

    public static object F_dvs_owned_decision_request(object v_actor, object v_topic)
    {
        object v_requests__56191 = F_dv_find(v_actor, "received_requests", new System.Collections.Generic.List<object>(0) { });
        object v_latest__56250 = false;
        object v_i__56273 = R.number("0");
        while (R.Bool(R.lt(v_i__56273, R.count(v_requests__56191))))
        {
            object v_receipt__56325 = R.at(v_requests__56191, v_i__56273);
            if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_receipt__56325, "topic"), v_topic)) ? (object)(R.not(R.eq(F_dv_find(v_receipt__56325, "source_event", ""), ""))) : (object)(false))) ? (object)(R.not(R.eq(F_dv_find(v_receipt__56325, "parse_event", ""), ""))) : (object)(false))))
            {
                v_latest__56250 = v_receipt__56325;
            }
            v_i__56273 = R.add(v_i__56273, R.number("1"));
        }
        return v_latest__56250;
    }

    public static object F_dvs_owned_decision_inputs(object v_actor, object v_decision, object v_mind)
    {
        _ = F_dv_require(F_dv_record_like(v_decision), "Owned decision must be a record");
        _ = F_dv_require((R.Bool(R.not(R.eq(F_dv_find(v_decision, "id", ""), ""))) ? (object)(R.not(R.eq(F_dv_find(v_decision, "stance", ""), ""))) : (object)(false)), "Owned decision needs identity and stance");
        _ = F_dv_require(R.lt(R.number("0"), R.count(F_dv_find(v_decision, "basis", new System.Collections.Generic.List<object>(0) { }))), "Owned decision needs declared provenance");
        object v_refs__56919 = F_dv_find(v_decision, "context_refs", new System.Collections.Generic.List<object>(0) { });
        object v_topics__56972 = F_dv_find(v_decision, "request_topics", new System.Collections.Generic.List<object>(0) { });
        _ = F_dv_require((R.Bool(R.is_list(v_refs__56919)) ? (object)(R.is_list(v_topics__56972)) : (object)(false)), "Owned decision references and request topics must be lists");
        object v_values__57147 = new System.Collections.Generic.List<object>(0) { };
        object v_sources__57167 = new System.Collections.Generic.List<object>(0) { };
        object v_requests__57188 = new System.Collections.Generic.List<object>(0) { };
        object v_request_sources__57210 = new System.Collections.Generic.List<object>(0) { };
        object v_i__57239 = R.number("0");
        while (R.Bool(R.lt(v_i__57239, R.count(v_refs__56919))))
        {
            object v_ref__57287 = R.at(v_refs__56919, v_i__57239);
            _ = F_dv_require((R.Bool(F_dv_record_like(v_ref__57287)) ? (object)(R.eq(F_dv_find(v_ref__57287, "type", ""), "ref")) : (object)(false)), "Owned decision context needs field references");
            object v_subject__57440 = F_dv_find(v_ref__57287, "subject", F_dv_get(v_actor, "id"));
            _ = F_dv_require((R.Bool(R.eq(v_subject__57440, "")) ? (object)(true) : (object)(R.eq(v_subject__57440, F_dv_get(v_actor, "id")))), "Owned decision context must belong to this subject");
            object v_local__57607 = F_dv_scoped_ref(F_dv_get(v_actor, "id"), F_dv_get(v_ref__57287, "object"), F_dv_get(v_ref__57287, "facet"), F_dv_get(v_ref__57287, "field"));
            object v_cell__57685 = F_dv_read(v_mind, v_local__57607);
            _ = R.push(v_values__57147, new System.Collections.Generic.List<object>(4) { F_dv_pair("ref", v_local__57607), F_dv_pair("known", F_dv_get(v_cell__57685, "known")), F_dv_pair("conflicted", F_dv_get(v_cell__57685, "conflicted")), F_dv_pair("value", F_dv_get(v_cell__57685, "value")) });
            _ = R.push(v_sources__57167, new System.Collections.Generic.List<object>(2) { F_dv_pair("ref", v_local__57607), F_dv_pair("cell", v_cell__57685) });
            v_i__57239 = R.add(v_i__57239, R.number("1"));
        }
        v_i__57239 = R.number("0");
        while (R.Bool(R.lt(v_i__57239, R.count(v_topics__56972))))
        {
            object v_topic__57953 = R.at(v_topics__56972, v_i__57239);
            object v_receipt__57983 = F_dvs_owned_decision_request(v_actor, v_topic__57953);
            object v_identity__58046 = false;
            if (R.Bool(R.not(R.eq(v_receipt__57983, false))))
            {
                v_identity__58046 = new System.Collections.Generic.List<object>(3) { F_dv_pair("requester", F_dv_get(v_receipt__57983, "requester")), F_dv_pair("source_event", F_dv_get(v_receipt__57983, "source_event")), F_dv_pair("parse_event", F_dv_get(v_receipt__57983, "parse_event")) };
            }
            _ = R.push(v_requests__57188, new System.Collections.Generic.List<object>(2) { F_dv_pair("topic", v_topic__57953), F_dv_pair("receipt", v_identity__58046) });
            _ = R.push(v_request_sources__57210, new System.Collections.Generic.List<object>(2) { F_dv_pair("topic", v_topic__57953), F_dv_pair("receipt", v_receipt__57983) });
            v_i__57239 = R.add(v_i__57239, R.number("1"));
        }
        object v_policy_keys__58384 = F_dv_find(v_decision, "context_policy_keys", new System.Collections.Generic.List<object>(0) { });
        _ = F_dv_require(R.is_list(v_policy_keys__58384), "Owned decision policy keys must be a list");
        object v_policy_values__58536 = new System.Collections.Generic.List<object>(0) { };
        v_i__57239 = R.number("0");
        while (R.Bool(R.lt(v_i__57239, R.count(v_policy_keys__58384))))
        {
            _ = R.push(v_policy_values__58536, new System.Collections.Generic.List<object>(2) { F_dv_pair("key", R.at(v_policy_keys__58384, v_i__57239)), F_dv_pair("value", F_dv_find(F_dv_get(v_actor, "policy"), R.at(v_policy_keys__58384, v_i__57239), false)) });
            v_i__57239 = R.add(v_i__57239, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("context", new System.Collections.Generic.List<object>(3) { F_dv_pair("values", v_values__57147), F_dv_pair("requests", v_requests__57188), F_dv_pair("policy_values", v_policy_values__58536) }), F_dv_pair("sources", new System.Collections.Generic.List<object>(3) { F_dv_pair("fields", v_sources__57167), F_dv_pair("requests", v_request_sources__57210), F_dv_pair("policy_values", v_policy_values__58536) }) };
    }

    public static object F_dvs_owned_decision_context(object v_actor, object v_decision, object v_mind)
    {
        return F_dv_get(F_dvs_owned_decision_inputs(v_actor, v_decision, v_mind), "context");
    }

    public static object F_dvp_owned_decision_ready(object v_actor, object v_action, object v_mind)
    {
        object v_decision__59103 = F_dv_find(v_action, "owned_decision", false);
        if (R.Bool(R.eq(v_decision__59103, false)))
        {
            return true;
        }
        object v_context__59209 = F_dvs_owned_decision_context(v_actor, v_decision__59103, v_mind);
        object v_previous__59277 = F_dv_find(F_dv_find(v_actor, "owned_decisions", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_decision__59103, "id"), false);
        return (R.Bool(R.eq(v_previous__59277, false)) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_get(v_previous__59277, "context"), v_context__59209))));
    }

    public static object F_dvs_owned_decision_selection(object v_actor, object v_action)
    {
        object v_decision__59560 = F_dv_find(v_action, "owned_decision", false);
        if (R.Bool(R.eq(v_decision__59560, false)))
        {
            return false;
        }
        return F_dv_with(F_dvs_owned_decision_inputs(v_actor, v_decision__59560, F_dv_get(v_actor, "mind")), "declaration", v_decision__59560);
    }

    public static object F_dvs_owned_decision_completion_selection(object v_actor, object v_action, object v_events, object v_parent)
    {
        object v_decision__60003 = F_dv_find(v_action, "owned_decision", false);
        if (R.Bool(R.eq(v_decision__60003, false)))
        {
            return true;
        }
        if (R.Bool(R.not(F_dvp_owned_decision_ready(v_actor, v_action, F_dv_get(v_actor, "mind")))))
        {
            return false;
        }
        object v_selection__60194 = F_dvs_action_selection_evidence(v_events, v_actor, v_action, v_parent);
        if (R.Bool(R.eq(v_selection__60194, false)))
        {
            return false;
        }
        object v_selected_event__60323 = F_dvs_find_event_by_id(v_events, F_dv_get(v_selection__60194, "event"));
        object v_snapshot__60394 = F_dv_find(F_dv_get(v_selected_event__60323, "data"), "owned_decision", false);
        if (R.Bool((R.Bool((R.Bool(R.eq(v_snapshot__60394, false)) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_get(v_snapshot__60394, "declaration"), v_decision__60003))))) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_get(v_snapshot__60394, "context"), F_dvs_owned_decision_context(v_actor, v_decision__60003, F_dv_get(v_actor, "mind"))))))))
        {
            return false;
        }
        object v_requests__60651 = F_dv_get(F_dv_get(v_snapshot__60394, "sources"), "requests");
        object v_i__60696 = R.number("0");
        while (R.Bool(R.lt(v_i__60696, R.count(v_requests__60651))))
        {
            object v_receipt__60748 = F_dv_get(R.at(v_requests__60651, v_i__60696), "receipt");
            if (R.Bool(R.not(R.eq(v_receipt__60748, false))))
            {
                object v_source__60823 = F_dvs_find_event_by_id(v_events, F_dv_get(v_receipt__60748, "source_event"));
                if (R.Bool((R.Bool((R.Bool(R.eq(v_source__60823, false)) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_source__60823, "type"), "speech_literal"))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_source__60823, "actor"), F_dv_get(v_receipt__60748, "requester")))))))
                {
                    return false;
                }
                object v_request__61032 = F_dv_find(F_dv_find(F_dv_get(v_source__60823, "data"), "speech", new System.Collections.Generic.List<object>(0) { }), "request", false);
                if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(v_request__61032, false)) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_request__61032, "target"), F_dv_get(v_actor, "id")))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_request__61032, "topic"), F_dv_get(R.at(v_requests__60651, v_i__60696), "topic")))))) ? (object)(true) : (object)(R.not(F_dv_same(v_request__61032, F_dv_get(v_receipt__60748, "request")))))))
                {
                    return false;
                }
                if (R.Bool(R.eq(F_dvs_heard_before_selection(v_events, v_actor, v_action, v_parent, F_dv_get(v_receipt__60748, "parse_event"), F_dv_get(v_receipt__60748, "source_event")), false)))
                {
                    return false;
                }
                object v_parsed__61446 = F_dvs_find_event_by_id(v_events, F_dv_get(v_receipt__60748, "parse_event"));
                if (R.Bool((R.Bool(R.not(F_dv_has(F_dv_get(v_parsed__61446, "parents"), F_dv_get(v_receipt__60748, "source_event")))) ? (object)(true) : (object)(R.not(R.lt(F_dvs_recorded_event_position(v_events, F_dv_get(v_receipt__60748, "source_event")), F_dvs_recorded_event_position(v_events, F_dv_get(v_receipt__60748, "parse_event"))))))))
                {
                    return false;
                }
            }
            v_i__60696 = R.add(v_i__60696, R.number("1"));
        }
        return F_dv_with(v_snapshot__60394, "selection_event", F_dv_get(v_selection__60194, "event"));
    }

    public static object F_dvs_complete_cognitive_effects(object v_actor, object v_action, object v_success, object v_events, object v_event, object v_time)
    {
        if (R.Bool(R.not(v_success)))
        {
            return v_actor;
        }
        object v_result__62017 = v_actor;
        object v_r__62040 = F_dv_find(v_action, "resonance", false);
        if (R.Bool(R.not(R.eq(v_r__62040, false))))
        {
            _ = F_dv_require(F_dv_record_like(v_r__62040), "Resonance must be a record");
        }
        if (R.Bool((R.Bool(R.not(R.eq(v_r__62040, false))) ? (object)(R.not(F_dv_find(v_r__62040, "passive", false))) : (object)(false))))
        {
            object v_retrieved__62235 = F_dv_find(v_r__62040, "retrieved_when", new System.Collections.Generic.List<object>(2) { F_dv_pair("op", "constant"), F_dv_pair("value", "false") });
            if (R.Bool(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), v_retrieved__62235), "true")))
            {
                object v_coupling__62384 = R.add(F_dv_find(v_actor, "resonance_coupling", R.number("0")), F_dv_find(v_r__62040, "coupling", R.number("0")));
                object v_policy__62479 = F_dv_get(v_actor, "policy");
                object v_trust__62517 = F_dv_find(v_policy__62479, "speaker_trust", new System.Collections.Generic.List<object>(0) { });
                object v_changes__62578 = F_dv_find(v_r__62040, "trust_changes", new System.Collections.Generic.List<object>(0) { });
                object v_i__62636 = R.number("0");
                while (R.Bool(R.lt(v_i__62636, R.count(v_changes__62578))))
                {
                    object v_change__62703 = R.at(v_changes__62578, v_i__62636);
                    v_trust__62517 = F_dv_with(v_trust__62517, F_dv_get(v_change__62703, "subject"), F_dv_unit(F_dv_min(R.number("1"), F_dv_max(R.number("0"), R.add(F_dv_find(v_trust__62517, F_dv_get(v_change__62703, "subject"), R.number("0.5")), F_dv_get(v_change__62703, "delta"))))));
                    v_i__62636 = R.add(v_i__62636, R.number("1"));
                }
                v_policy__62479 = F_dv_with(v_policy__62479, "speaker_trust", v_trust__62517);
                v_result__62017 = F_dv_with_many(v_result__62017, new System.Collections.Generic.List<object>(4) { F_dv_pair("resonance_coupling", v_coupling__62384), F_dv_pair("night_call_eligible", (R.Bool(F_dv_find(v_actor, "night_call_eligible", false)) ? (object)(true) : (object)(F_dv_find(v_r__62040, "night_call", false)))), F_dv_pair("policy", v_policy__62479), F_dv_pair("resonance_receipts", F_dv_append(F_dv_find(v_actor, "resonance_receipts", new System.Collections.Generic.List<object>(0) { }), new System.Collections.Generic.List<object>(4) { F_dv_pair("event", v_event), F_dv_pair("time", v_time), F_dv_pair("action", F_dv_get(v_action, "id")), F_dv_pair("coupling", v_coupling__62384) })) });
            }
        }
        object v_influence__63304 = F_dv_find(v_action, "influence_consequence", false);
        if (R.Bool(R.not(R.eq(v_influence__63304, false))))
        {
            if (R.Bool((R.Bool(R.lt(R.number("0"), R.count(F_dv_find(v_influence__63304, "basis", new System.Collections.Generic.List<object>(0) { })))) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_influence__63304, "consequence_when")), "true")) : (object)(false))))
            {
                object v_heard__63528 = F_dv_find(v_actor, "received_speeches", new System.Collections.Generic.List<object>(0) { });
                object v_i__63592 = R.number("0");
                while (R.Bool(R.lt(v_i__63592, R.count(v_heard__63528))))
                {
                    object v_speech__63657 = R.at(v_heard__63528, v_i__63592);
                    if (R.Bool((R.Bool(R.eq(F_dv_get(v_speech__63657, "speaker"), F_dv_get(v_influence__63304, "speaker"))) ? (object)(R.eq(F_dv_get(v_speech__63657, "action_id"), F_dv_get(v_influence__63304, "action_id"))) : (object)(false))))
                    {
                        object v_selection__63799 = F_dvs_heard_before_selection(v_events, v_actor, v_action, v_event, F_dv_get(v_speech__63657, "parse_event"), F_dv_get(v_speech__63657, "source_event"));
                        if (R.Bool(R.not(R.eq(v_selection__63799, false))))
                        {
                            v_result__62017 = F_dv_with(v_result__62017, "influence_consequences", F_dv_append(F_dv_find(v_result__62017, "influence_consequences", new System.Collections.Generic.List<object>(0) { }), new System.Collections.Generic.List<object>(10) { F_dv_pair("speech_event", F_dv_get(v_speech__63657, "source_event")), F_dv_pair("parse_event", F_dv_get(v_speech__63657, "parse_event")), F_dv_pair("selection_event", F_dv_get(v_selection__63799, "event")), F_dv_pair("execution_event", v_event), F_dv_pair("action", F_dv_get(v_action, "id")), F_dv_pair("time", v_time), F_dv_pair("basis", F_dv_get(v_influence__63304, "basis")), F_dv_pair("record_kind", "post-advice-matching-behavior"), F_dv_pair("causal_attribution", "not-established"), F_dv_pair("consequence_transition", "not-assessed") }));
                        }
                    }
                    v_i__63592 = R.add(v_i__63592, R.number("1"));
                }
            }
        }
        return v_result__62017;
    }
}
