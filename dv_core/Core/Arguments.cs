using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Cognition / Arguments: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_argument_sources(object v_events, object v_actor)
    {
        v_actor = F_dvs_update_support_events(v_actor, v_events);
        object v_subject__235755 = F_dv_get(v_actor, "id");
        v_events = F_dv_get(v_actor, "support_events");
        object v_result__235816 = new System.Collections.Generic.List<object>(0) { };
        object v_i__235836 = R.number("0");
        while (R.Bool(R.lt(v_i__235836, R.count(v_events))))
        {
            object v_event__235888 = R.at(v_events, v_i__235836);
            if (R.Bool(F_dv_and(R.eq(F_dv_get(v_event__235888, "actor"), v_subject__235755), F_dv_or(R.eq(F_dv_get(v_event__235888, "type"), "observation"), R.eq(F_dv_get(v_event__235888, "type"), "listener_parse")))))
            {
                object v_observations__236039 = F_dv_get(F_dv_get(v_event__235888, "data"), "observations");
                object v_j__236094 = R.number("0");
                while (R.Bool(R.lt(v_j__236094, R.count(v_observations__236039))))
                {
                    _ = R.push(v_result__235816, new System.Collections.Generic.List<object>(2) { F_dv_pair("event_id", F_dv_get(v_event__235888, "id")), F_dv_pair("observation", R.at(v_observations__236039, v_j__236094)) });
                    v_j__236094 = R.add(v_j__236094, R.number("1"));
                }
            }
            v_i__235836 = R.add(v_i__235836, R.number("1"));
        }
        return v_result__235816;
    }

    public static object F_dvs_capture_public_topics(object v_actor, object v_events, object v_time, object v_parent)
    {
        object v_bindings__236370 = F_dv_find(F_dv_get(v_actor, "policy"), "topic_bindings", new System.Collections.Generic.List<object>(0) { });
        object v_heard__236433 = F_dv_find(v_actor, "heard_topics", new System.Collections.Generic.List<object>(0) { });
        object v_i__236484 = R.number("0");
        while (R.Bool(R.lt(v_i__236484, R.count(v_bindings__236370))))
        {
            object v_binding__236538 = R.at(v_bindings__236370, v_i__236484);
            object v_topic__236572 = F_dv_get(R.at(v_bindings__236370, v_i__236484), "topic");
            if (R.Bool(F_dv_and(R.not(F_dv_has_key(v_heard__236433, v_topic__236572)), R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_binding__236538, "heard_when")), "true"))))
            {
                object v_sources__236719 = F_dvs_support_parents(F_dv_get(v_actor, "mind"), F_dvs_sim_refs(F_dv_get(v_binding__236538, "heard_when")), v_events, v_actor);
                if (R.Bool(R.lt(R.number("0"), R.count(v_sources__236719))))
                {
                    object v_record__236870 = F_dv_with(new System.Collections.Generic.List<object>(3) { F_dv_pair("topic", v_topic__236572), F_dv_pair("source_event_ids", v_sources__236719), F_dv_pair("heard_when", F_dv_get(v_binding__236538, "heard_when")) }, "origin_speakers", F_dvs_topic_origin_speakers(v_events, v_sources__236719));
                    _ = F_dvs_event(v_events, "public_topic_heard", F_dv_get(v_actor, "id"), v_time, F_dv_explanation_merge(F_dv_single(v_parent), v_sources__236719), v_record__236870);
                    v_heard__236433 = F_dv_with(v_heard__236433, v_topic__236572, v_record__236870);
                }
            }
            v_i__236484 = R.add(v_i__236484, R.number("1"));
        }
        return F_dvs_capture_heard_utterances(F_dv_with(v_actor, "heard_topics", v_heard__236433), v_events);
    }

    public static object F_dvs_argument_revoke(object v_actor, object v_events, object v_time, object v_parent)
    {
        object v_argument__237384 = F_dv_get(v_actor, "argument");
        object v_world__237418 = F_dv_get(v_actor, "mind");
        object v_changes__237445 = F_dv_get(F_dv_get(v_actor, "argument"), "changes");
        object v_i__237486 = R.number("0");
        object v_restored__237500 = new System.Collections.Generic.List<object>(0) { };
        object v_derivations__237522 = F_dv_find(v_actor, "inference_derivations", new System.Collections.Generic.List<object>(0) { });
        object v_contaminated__237588 = new System.Collections.Generic.List<object>(0) { };
        object v_remaining__237614 = F_dv_find(F_dv_get(v_actor, "policy"), "reason_budget", R.number("0"));
        object v_checked__237676 = R.number("0");
        object v_blocked__237696 = false;
        object v_descendants__237720 = new System.Collections.Generic.List<object>(0) { };
        object v_updated__237745 = false;
        while (R.Bool(R.lt(v_i__237486, R.count(v_changes__237445))))
        {
            v_contaminated__237588 = F_dvp_add_ref(v_contaminated__237588, F_dv_get(R.at(v_changes__237445, v_i__237486), "ref"));
            v_i__237486 = R.add(v_i__237486, R.number("1"));
        }
        v_i__237486 = R.sub(R.count(v_derivations__237522), R.number("1"));
        while (R.Bool(F_dv_and(R.not(R.lt(v_i__237486, R.number("0"))), R.not(v_blocked__237696))))
        {
            object v_derivation__237977 = R.at(v_derivations__237522, v_i__237486);
            object v_dependent__238017 = false;
            object v_inputs__238047 = F_dv_get(R.at(v_derivations__237522, v_i__237486), "inputs");
            object v_j__238090 = R.number("0");
            if (R.Bool(R.lt(v_remaining__237614, R.number("1"))))
            {
                v_blocked__237696 = true;
            }
            else
            {
                v_remaining__237614 = R.sub(v_remaining__237614, R.number("1"));
                v_checked__237676 = R.add(v_checked__237676, R.number("1"));
                while (R.Bool(R.lt(v_j__238090, R.count(v_inputs__238047))))
                {
                    if (R.Bool(F_dv_has(v_contaminated__237588, F_dv_get(R.at(v_inputs__238047, v_j__238090), "ref"))))
                    {
                        v_dependent__238017 = true;
                    }
                    v_j__238090 = R.add(v_j__238090, R.number("1"));
                }
            }
            if (R.Bool(F_dv_and(R.not(v_blocked__237696), F_dv_and(v_dependent__238017, F_dv_find(v_derivation__237977, "valid", true)))))
            {
                object v_outputs__238509 = F_dv_get(v_derivation__237977, "changes");
                object v_k__238554 = R.number("0");
                while (R.Bool(R.lt(v_k__238554, R.count(v_outputs__238509))))
                {
                    object v_change__238623 = R.at(v_outputs__238509, v_k__238554);
                    object v_current__238663 = F_dv_read(v_world__237418, F_dv_get(R.at(v_outputs__238509, v_k__238554), "ref"));
                    if (R.Bool(F_dv_same(v_current__238663, F_dv_get(v_change__238623, "after"))))
                    {
                        v_world__237418 = F_dv_store(v_world__237418, F_dv_get(v_change__238623, "ref"), F_dv_get(v_change__238623, "before"));
                        v_contaminated__237588 = F_dvp_add_ref(v_contaminated__237588, F_dv_get(v_change__238623, "ref"));
                        _ = R.push(v_restored__237500, v_change__238623);
                    }
                    v_k__238554 = R.add(v_k__238554, R.number("1"));
                }
                _ = R.push(v_descendants__237720, F_dv_get(v_derivation__237977, "event"));
                v_derivations__237522 = F_dvs_replace(v_derivations__237522, v_i__237486, F_dv_with(v_derivation__237977, "valid", false));
            }
            v_i__237486 = R.sub(v_i__237486, R.number("1"));
        }
        v_i__237486 = R.number("0");
        if (R.Bool(v_blocked__237696))
        {
            _ = false;
        }
        else
        {
            while (R.Bool(R.lt(v_i__237486, R.count(v_changes__237445))))
            {
                object v_change__239240 = R.at(v_changes__237445, v_i__237486);
                object v_cell__239276 = F_dv_read(v_world__237418, F_dv_get(R.at(v_changes__237445, v_i__237486), "ref"));
                if (R.Bool(F_dv_and(R.eq(F_dv_find(v_cell__239276, "source", ""), "abduction"), F_dv_has(F_dv_find(v_cell__239276, "basis", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_argument__237384, "id")))))
                {
                    v_world__237418 = F_dv_store(v_world__237418, F_dv_get(v_change__239240, "ref"), F_dv_get(v_change__239240, "before"));
                    _ = R.push(v_restored__237500, v_change__239240);
                }
                v_i__237486 = R.add(v_i__237486, R.number("1"));
            }
        }
        if (R.Bool(R.lt(R.number("0"), R.count(v_restored__237500))))
        {
            v_world__237418 = F_dv_with(v_world__237418, "revision", R.add(F_dv_get(v_world__237418, "revision"), R.number("1")));
        }
        object v_local_1__239671 = false;
        if (R.Bool(v_blocked__237696))
        {
            v_local_1__239671 = v_argument__237384;
        }
        else
        {
            v_local_1__239671 = false;
        }
        v_updated__237745 = F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(7) { F_dv_pair("mind", v_world__237418), F_dv_pair("argument", v_local_1__239671), F_dv_pair("argument_revocation_pending", v_blocked__237696), F_dv_pair("ready_hypothesis", false), F_dv_pair("inference_derivations", v_derivations__237522), F_dv_pair("cognition_blocked", v_blocked__237696), F_dv_pair("policy", F_dv_with(F_dv_get(v_actor, "policy"), "reason_budget", v_remaining__237614)) });
        v_updated__237745 = F_dv_with(v_updated__237745, "last_cognitive_event", R.cat("e", R.show(R.count(v_events))));
        object v_local_2__240135 = false;
        if (R.Bool(v_blocked__237696))
        {
            v_local_2__240135 = "model_argument_revocation_pending";
        }
        else
        {
            v_local_2__240135 = "model_argument_revoked";
        }
        _ = F_dvs_event(v_events, v_local_2__240135, F_dv_get(v_actor, "id"), v_time, F_dvs_cognitive_parents(v_actor, F_dv_explanation_merge(F_dv_pair(v_parent, F_dv_get(v_argument__237384, "id")), v_descendants__237720)), F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("argument", v_argument__237384), F_dv_pair("mind_revision_before", F_dv_get(F_dv_get(v_actor, "mind"), "revision")), F_dv_pair("mind_revision_after", F_dv_get(v_world__237418, "revision")), F_dv_pair("restored", v_restored__237500) }, new System.Collections.Generic.List<object>(7) { F_dv_pair("checked", v_checked__237676), F_dv_pair("remaining", v_remaining__237614), F_dv_pair("actor_id", F_dv_get(v_actor, "id")), F_dv_pair("actor_before", F_dv_without(v_actor, "simulations")), F_dv_pair("actor_after", F_dv_without(v_updated__237745, "simulations")), F_dv_pair("mind_before", F_dv_get(v_actor, "mind")), F_dv_pair("mind_after", v_world__237418) }));
        return v_updated__237745;
    }

    public static object F_dvs_factor_argument(object v_result, object v_coverage)
    {
        object v_argument__240856 = F_dv_get(v_result, "argument");
        object v_certificates__240891 = F_dv_get(v_coverage, "certificates");
        object v_i__240936 = R.number("0");
        object v_events__240950 = new System.Collections.Generic.List<object>(0) { };
        object v_sources__240970 = new System.Collections.Generic.List<object>(0) { };
        object v_inputs__240991 = new System.Collections.Generic.List<object>(0) { };
        while (R.Bool(R.lt(v_i__240936, R.count(v_certificates__240891))))
        {
            object v_certificate__241055 = R.at(v_certificates__240891, v_i__240936);
            object v_chain__241097 = F_dv_get(R.at(v_certificates__240891, v_i__240936), "source_chain");
            object v_j__241146 = R.number("0");
            v_events__240950 = F_dv_explanation_merge(v_events__240950, F_dv_single(F_dv_get(v_certificate__241055, "event")));
            v_sources__240970 = F_dv_explanation_merge(v_sources__240970, F_dv_get(v_certificate__241055, "source_event_ids"));
            while (R.Bool(R.lt(v_j__241146, R.count(v_chain__241097))))
            {
                _ = R.push(v_inputs__240991, new System.Collections.Generic.List<object>(2) { F_dv_pair("ref", F_dv_get(R.at(v_chain__241097, v_j__241146), "ref")), F_dv_pair("cell", F_dv_get(R.at(v_chain__241097, v_j__241146), "cell")) });
                v_j__241146 = R.add(v_j__241146, R.number("1"));
            }
            v_i__240936 = R.add(v_i__240936, R.number("1"));
        }
        v_argument__240856 = F_dv_with_many(v_argument__240856, new System.Collections.Generic.List<object>(4) { F_dv_pair("factor_coverage", v_coverage), F_dv_pair("competitor_events", F_dv_explanation_merge(F_dv_get(v_argument__240856, "competitor_events"), v_events__240950)), F_dv_pair("source_event_ids", F_dv_explanation_merge(F_dv_get(v_argument__240856, "source_event_ids"), v_sources__240970)), F_dv_pair("inputs", F_dv_explanation_merge(F_dv_get(v_argument__240856, "inputs"), v_inputs__240991)) });
        return F_dv_with(v_result, "argument", v_argument__240856);
    }

    public static object F_dvs_capture_heard_utterances(object v_actor, object v_events)
    {
        v_events = R.snapshot(v_events);
        object v_heard__241883 = F_dv_find(v_actor, "heard_utterances", new System.Collections.Generic.List<object>(0) { });
        object v_i__241938 = F_dv_find(v_actor, "heard_utterances_cursor", R.number("0"));
        object v_total__241995 = R.count(v_events);
        if (R.Bool((R.Bool(R.eq(v_i__241938, v_total__241995)) ? (object)(F_dv_has_key(v_actor, "heard_utterances")) : (object)(false))))
        {
            return v_actor;
        }
        while (R.Bool(R.lt(v_i__241938, v_total__241995)))
        {
            object v_event__242137 = R.at(v_events, v_i__241938);
            if (R.Bool(F_dv_and(R.eq(F_dv_get(v_event__242137, "actor"), F_dv_get(v_actor, "id")), R.eq(F_dv_get(v_event__242137, "type"), "listener_parse"))))
            {
                object v_data__242251 = F_dv_get(v_event__242137, "data");
                object v_source__242285 = F_dv_get(v_data__242251, "source_event");
                object v_existing__242328 = F_dv_find(v_heard__241883, v_source__242285, false);
                if (R.Bool((R.Bool(R.eq(v_existing__242328, false)) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_existing__242328, "parse_event"), F_dv_get(v_event__242137, "id")))))))
                {
                    object v_literal__242459 = F_dvs_find_event_by_id(v_events, v_source__242285);
                    if (R.Bool((R.Bool((R.Bool(R.not(R.eq(v_literal__242459, false))) ? (object)(F_dv_has(F_dv_get(v_event__242137, "parents"), v_source__242285)) : (object)(false))) ? (object)(R.not(R.eq(F_dv_get(v_literal__242459, "actor"), F_dv_get(v_actor, "id")))) : (object)(false))))
                    {
                        v_heard__241883 = F_dv_with(v_heard__241883, v_source__242285, F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("source_event", v_source__242285), F_dv_pair("parse_event", F_dv_get(v_event__242137, "id")), F_dv_pair("speaker", F_dv_get(v_literal__242459, "actor")), F_dv_pair("scope", F_dv_find(F_dv_get(v_data__242251, "speech"), "scope", "")) }, "refs", F_dvs_sim_refs(F_dv_get(v_data__242251, "literal"))));
                    }
                }
            }
            v_i__241938 = R.add(v_i__241938, R.number("1"));
        }
        return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(2) { F_dv_pair("heard_utterances", v_heard__241883), F_dv_pair("heard_utterances_cursor", v_total__241995) });
    }

    public static object F_dvs_topic_origin_speakers(object v_events, object v_sources)
    {
        object v_pending__242994 = F_dv_clone(v_sources);
        object v_visited__243030 = new System.Collections.Generic.List<object>(0) { };
        object v_result__243051 = new System.Collections.Generic.List<object>(0) { };
        object v_i__243071 = R.number("0");
        while (R.Bool(R.lt(v_i__243071, R.count(v_pending__242994))))
        {
            object v_id__243124 = R.at(v_pending__242994, v_i__243071);
            if (R.Bool(F_dv_has(v_visited__243030, v_id__243124)))
            {
                _ = false;
            }
            else
            {
                _ = R.push(v_visited__243030, v_id__243124);
                object v_event__243253 = F_dvs_find_event_by_id(v_events, v_id__243124);
                if (R.Bool(R.not(R.eq(v_event__243253, false))))
                {
                    if (R.Bool(F_dv_or(R.eq(F_dv_get(v_event__243253, "type"), "observation"), R.eq(F_dv_get(v_event__243253, "type"), "listener_parse"))))
                    {
                        object v_source__243442 = F_dv_find(F_dv_get(v_event__243253, "data"), "source_event", "");
                        if (R.Bool(F_dv_and(R.not(R.eq(v_source__243442, "")), F_dv_has(F_dv_get(v_event__243253, "parents"), v_source__243442))))
                        {
                            v_pending__242994 = F_dv_explanation_merge(v_pending__242994, F_dv_single(v_source__243442));
                        }
                    }
                    else
                    {
                        if (R.Bool(R.eq(F_dv_get(v_event__243253, "type"), "public_topic_heard")))
                        {
                            v_pending__242994 = F_dv_explanation_merge(v_pending__242994, F_dv_find(F_dv_get(v_event__243253, "data"), "source_event_ids", new System.Collections.Generic.List<object>(0) { }));
                        }
                    }
                    if (R.Bool(R.eq(F_dv_get(v_event__243253, "type"), "speech_literal")))
                    {
                        v_result__243051 = F_dv_explanation_merge(v_result__243051, F_dv_single(F_dv_get(v_event__243253, "actor")));
                    }
                }
            }
            v_i__243071 = R.add(v_i__243071, R.number("1"));
        }
        return v_result__243051;
    }

    public static object F_dvs_topic_was_heard(object v_actor, object v_topic)
    {
        return F_dv_has_key(F_dv_find(v_actor, "heard_topics", new System.Collections.Generic.List<object>(0) { }), v_topic);
    }

    public static object F_dvs_argument_valid(object v_actor)
    {
        object v_argument__244166 = F_dv_find(v_actor, "argument", false);
        if (R.Bool((R.Bool(R.eq(v_argument__244166, false)) ? (object)(true) : (object)(R.not(F_dv_record_like(v_argument__244166))))))
        {
            return false;
        }
        else
        {
            object v_topic__244312 = F_dv_find(v_argument__244166, "question_id", "");
            return (R.Bool(R.not(R.eq(v_topic__244312, ""))) ? (object)(F_dv_and(F_dv_same(F_dv_find(v_argument__244166, "adoptions", new System.Collections.Generic.List<object>(0) { }), F_dv_get(F_dv_get(v_actor, "mind"), "adoptions")), F_dvs_topic_was_heard(v_actor, v_topic__244312))) : (object)(false));
        }
    }

    public static object F_dvs_argument_inputs_current(object v_actor)
    {
        object v_argument__244545 = F_dv_find(v_actor, "argument", false);
        object v_valid__244598 = true;
        object v_i__244619 = R.number("0");
        if (R.Bool((R.Bool((R.Bool(R.eq(v_argument__244545, false)) ? (object)(true) : (object)(R.not(F_dv_has_key(v_argument__244545, "inputs"))))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_argument__244545, "changes"))))))
        {
            return false;
        }
        else
        {
            object v_inputs__244771 = F_dv_get(v_argument__244545, "inputs");
            while (R.Bool(R.lt(v_i__244619, R.count(v_inputs__244771))))
            {
                object v_input__244850 = R.at(v_inputs__244771, v_i__244619);
                if (R.Bool(F_dv_same(F_dv_get(v_input__244850, "cell"), F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_input__244850, "ref")))))
                {
                    _ = false;
                }
                else
                {
                    object v_changes__244996 = F_dv_get(v_argument__244545, "changes");
                    object v_j__245043 = R.number("0");
                    object v_own_transaction__245069 = false;
                    object v_current__245113 = F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_input__244850, "ref"));
                    while (R.Bool(R.lt(v_j__245043, R.count(v_changes__244996))))
                    {
                        object v_change__245225 = R.at(v_changes__244996, v_j__245043);
                        if (R.Bool(F_dv_and(F_dv_same(F_dv_get(v_change__245225, "ref"), F_dv_get(v_input__244850, "ref")), F_dv_and(R.eq(F_dv_find(v_current__245113, "source", ""), "abduction"), F_dv_and(F_dv_has(F_dv_find(v_current__245113, "basis", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_argument__244545, "id")), F_dv_same(F_dv_get(v_current__245113, "value"), F_dv_get(F_dv_get(v_change__245225, "after"), "value")))))))
                        {
                            v_own_transaction__245069 = true;
                        }
                        v_j__245043 = R.add(v_j__245043, R.number("1"));
                    }
                    if (R.Bool(v_own_transaction__245069))
                    {
                        _ = false;
                    }
                    else
                    {
                        v_valid__244598 = false;
                    }
                }
                v_i__244619 = R.add(v_i__244619, R.number("1"));
            }
            return v_valid__244598;
        }
    }

    public static object F_dvs_action_requires_argument(object v_action)
    {
        return (R.Bool(F_dv_find(v_action, "speech_from_argument", false)) ? (object)(true) : (object)(F_dv_has_key(v_action, "argument_slots")));
    }

    public static object F_dvs_refresh_argument(object v_actor, object v_events, object v_time, object v_parent)
    {
        if (R.Bool(R.not(R.eq(F_dv_find(v_actor, "argument", false), false))))
        {
            if (R.Bool((R.Bool((R.Bool(F_dv_find(v_actor, "argument_revocation_pending", false)) ? (object)(true) : (object)(R.not(F_dvs_argument_valid(v_actor))))) ? (object)(true) : (object)(R.not(F_dvs_argument_inputs_current(v_actor))))))
            {
                return F_dvs_argument_revoke(v_actor, v_events, v_time, v_parent);
            }
        }
        return v_actor;
    }

    public static object F_dvs_prepare_replay_argument(object v_actor, object v_action, object v_events, object v_time, object v_parent)
    {
        v_actor = F_dvs_refresh_argument(v_actor, v_events, v_time, v_parent);
        object v_question__246485 = "";
        object v_argument__246507 = F_dv_find(v_actor, "argument", false);
        object v_previous__246560 = "";
        if (R.Bool(R.not(R.eq(v_argument__246507, false))))
        {
            v_previous__246560 = F_dv_find(v_argument__246507, "question_id", "");
        }
        object v_manual__246664 = F_dv_find(v_action, "replay_hypothesis_id", "");
        object v_candidates__246725 = F_dv_find(F_dv_get(v_actor, "policy"), "hypotheses", new System.Collections.Generic.List<object>(0) { });
        object v_i__246786 = R.number("0");
        while (R.Bool(R.lt(v_i__246786, R.count(v_candidates__246725))))
        {
            object v_candidate__246840 = R.at(v_candidates__246725, v_i__246786);
            object v_topic__246878 = F_dv_find(v_candidate__246840, "question", F_dv_find(v_candidate__246840, "topic", ""));
            if (R.Bool((R.Bool((R.Bool(R.not(R.eq(v_topic__246878, ""))) ? (object)(R.lt(R.number("0"), R.count(F_dv_find(v_candidate__246840, "claim_slots", new System.Collections.Generic.List<object>(0) { })))) : (object)(false))) ? (object)((R.Bool(R.eq(v_manual__246664, "")) ? (object)(true) : (object)(R.eq(v_manual__246664, F_dv_get(v_candidate__246840, "id"))))) : (object)(false))))
            {
                if (R.Bool((R.Bool(F_dvs_replay_candidate_available(v_actor, v_candidate__246840)) ? (object)((R.Bool(R.eq(v_manual__246664, "")) ? (object)(true) : (object)(F_dvs_manual_candidate_available(v_actor, v_candidate__246840)))) : (object)(false))))
                {
                    if (R.Bool((R.Bool(R.eq(v_question__246485, "")) ? (object)(true) : (object)((R.Bool(R.eq(v_question__246485, v_previous__246560)) ? (object)(R.not(R.eq(v_topic__246878, v_previous__246560))) : (object)(false))))))
                    {
                        v_question__246485 = v_topic__246878;
                    }
                }
            }
            v_i__246786 = R.add(v_i__246786, R.number("1"));
        }
        if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(F_dv_find(v_actor, "cognition_blocked", false))) ? (object)(R.not(R.eq(v_argument__246507, false))) : (object)(false))) ? (object)(R.not(R.eq(v_question__246485, ""))) : (object)(false))) ? (object)(R.not(R.eq(v_question__246485, v_previous__246560))) : (object)(false))))
        {
            object v_requested__247482 = F_dvs_event(v_events, "model_argument_question_changed", F_dv_get(v_actor, "id"), v_time, F_dvs_cognitive_parents(v_actor, new System.Collections.Generic.List<object>(2) { v_parent, F_dv_get(v_argument__246507, "id") }), new System.Collections.Generic.List<object>(4) { F_dv_pair("previous_question", v_previous__246560), F_dv_pair("question", v_question__246485), F_dv_pair("argument_id", F_dv_get(v_argument__246507, "id")), F_dv_pair("mind_revision", F_dv_get(F_dv_get(v_actor, "mind"), "revision")) });
            v_actor = F_dvs_argument_revoke(v_actor, v_events, v_time, v_requested__247482);
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("actor", v_actor), F_dv_pair("question", v_question__246485) };
    }

    public static object F_dvs_accept_paid_arguments(object v_actor, object v_replayed, object v_question_id, object v_events, object v_time, object v_parent)
    {
        if (R.Bool(F_dv_find(F_dv_find(F_dv_get(v_actor, "mind"), "CognitionFilter", new System.Collections.Generic.List<object>(0) { }), "anterograde", false)))
        {
            return v_actor;
        }
        v_actor = F_dvs_refresh_argument(v_actor, v_events, v_time, v_parent);
        if (R.Bool(F_dv_find(v_actor, "cognition_blocked", false)))
        {
            return v_actor;
        }
        if (R.Bool((R.Bool((R.Bool(F_dvs_argument_valid(v_actor)) ? (object)(F_dvs_argument_inputs_current(v_actor)) : (object)(false))) ? (object)(R.eq(F_dv_find(F_dv_get(v_actor, "argument"), "question_id", ""), v_question_id)) : (object)(false))))
        {
            return v_actor;
        }
        if (R.Bool(R.not(R.eq(F_dv_find(v_actor, "argument", false), false))))
        {
            return v_actor;
        }
        v_actor = F_dvs_capture_public_topics(F_dvs_update_support_events(v_actor, v_events), v_events, v_time, v_parent);
        object v_sources__248909 = F_dvs_argument_sources(v_events, v_actor);
        object v_candidates__248963 = F_dv_find(F_dv_get(v_actor, "policy"), "hypotheses", new System.Collections.Generic.List<object>(0) { });
        object v_i__249024 = R.number("0");
        while (R.Bool(R.lt(v_i__249024, R.count(v_replayed))))
        {
            object v_hypothesis__249076 = R.at(v_replayed, v_i__249024);
            object v_question__249113 = F_dv_find(v_hypothesis__249076, "question", F_dv_find(v_hypothesis__249076, "topic", ""));
            object v_model__249202 = F_dvs_cached_model(v_actor, v_hypothesis__249076);
            object v_model_event__249258 = F_dvs_find_event_by_id(v_events, F_dv_find(v_model__249202, "event", ""));
            object v_slots__249342 = F_dv_find(v_hypothesis__249076, "claim_slots", new System.Collections.Generic.List<object>(0) { });
            if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.eq(v_question__249113, ""))) ? (object)(R.eq(v_question__249113, v_question_id)) : (object)(false))) ? (object)(F_dvs_topic_was_heard(v_actor, v_question__249113)) : (object)(false))) ? (object)(R.lt(R.number("0"), R.count(v_slots__249342))) : (object)(false))) ? (object)(F_dv_explanation_model_complete(v_model__249202, "explained", F_dv_get(F_dv_get(v_actor, "mind"), "revision"))) : (object)(false))) ? (object)(F_dvs_public_model_paid(v_events, F_dv_find(v_model__249202, "event", ""), F_dv_get(v_actor, "id"))) : (object)(false))))
            {
                if (R.Bool((R.Bool(R.not(R.eq(v_model_event__249258, false))) ? (object)(F_dv_has(F_dv_get(v_model_event__249258, "parents"), F_dv_find(v_actor, "model_paid_event", ""))) : (object)(false))))
                {
                    object v_coverage__249872 = F_dvs_factor_coverage(v_actor, v_hypothesis__249076, v_events);
                    if (R.Bool(R.not(F_dvs_factor_covered(v_hypothesis__249076, F_dv_get(v_coverage__249872, "certificates")))))
                    {
                        object v_competitors__250032 = new System.Collections.Generic.List<object>(0) { };
                        object v_j__250073 = R.number("0");
                        while (R.Bool(R.lt(v_j__250073, R.count(v_candidates__248963))))
                        {
                            object v_other__250159 = R.at(v_candidates__248963, v_j__250073);
                            if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_other__250159, "id"), F_dv_get(v_hypothesis__249076, "id")))) ? (object)(R.eq(F_dv_find(v_other__250159, "group", ""), F_dv_find(v_hypothesis__249076, "group", ""))) : (object)(false))) ? (object)(F_dvp_topic(v_actor, v_other__250159)) : (object)(false))) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(v_other__250159, "available_when", F_dv_true())), "true")) : (object)(false))))
                            {
                                if (R.Bool(R.not(F_dvs_factor_covered(v_other__250159, F_dv_get(v_coverage__249872, "certificates")))))
                                {
                                    object v_comparison__250532 = F_dvs_cached_model(v_actor, v_other__250159);
                                    if (R.Bool(R.not(F_dvs_model_paid_result(v_events, F_dv_find(v_comparison__250532, "event", ""), F_dv_get(v_actor, "id"), "inconsistent"))))
                                    {
                                        v_comparison__250532 = new System.Collections.Generic.List<object>(2) { F_dv_pair("status", "unpaid-or-unresolved-competitor"), F_dv_pair("source_revision", F_dv_get(F_dv_get(v_actor, "mind"), "revision")) };
                                    }
                                    _ = R.push(v_competitors__250032, v_comparison__250532);
                                }
                            }
                            v_j__250073 = R.add(v_j__250073, R.number("1"));
                        }
                        object v_policy__250964 = F_dv_find(F_dv_get(v_actor, "policy"), "abduction", new System.Collections.Generic.List<object>(0) { });
                        v_j__250073 = R.number("0");
                        while (R.Bool(R.lt(v_j__250073, R.count(F_dv_get(v_coverage__249872, "certificates")))))
                        {
                            v_policy__250964 = F_dv_with(v_policy__250964, "confidence_cap", F_dv_min(F_dv_find(v_policy__250964, "confidence_cap", R.number("0")), F_dv_get(R.at(F_dv_get(v_coverage__249872, "certificates"), v_j__250073), "confidence")));
                            v_j__250073 = R.add(v_j__250073, R.number("1"));
                        }
                        object v_result__251309 = F_dvs_factor_argument(F_dv_accept_explanation(F_dv_get(v_actor, "mind"), v_model__249202, v_competitors__250032, v_slots__249342, v_sources__248909, v_policy__250964, F_dv_find(F_dv_get(v_actor, "policy"), "reason_budget", R.number("0")), v_time, R.cat("e", R.show(R.count(v_events)))), v_coverage__249872);
                        object v_parents__251540 = F_dvs_cognitive_parents(v_actor, new System.Collections.Generic.List<object>(2) { v_parent, F_dv_get(v_model__249202, "event") });
                        v_parents__251540 = F_dv_explanation_merge(v_parents__251540, F_dv_get(F_dv_get(v_result__251309, "argument"), "source_event_ids"));
                        v_parents__251540 = F_dv_explanation_merge(v_parents__251540, F_dv_get(F_dv_get(v_result__251309, "argument"), "competitor_events"));
                        object v_kind__251815 = "model_argument_unavailable";
                        if (R.Bool(F_dv_get(v_result__251309, "accepted")))
                        {
                            v_kind__251815 = "model_argument_accepted";
                        }
                        object v_accepted_event__251972 = F_dvs_event(v_events, v_kind__251815, F_dv_get(v_actor, "id"), v_time, v_parents__251540, new System.Collections.Generic.List<object>(5) { F_dv_pair("actor_id", F_dv_get(v_actor, "id")), F_dv_pair("actor_revision", F_dv_get(F_dv_get(v_actor, "mind"), "revision")), F_dv_pair("hypothesis", v_hypothesis__249076), F_dv_pair("competitors", v_competitors__250032), F_dv_pair("result", v_result__251309) });
                        v_actor = F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(2) { F_dv_pair("last_cognitive_event", v_accepted_event__251972), F_dv_pair("policy", F_dv_with(F_dv_get(v_actor, "policy"), "reason_budget", F_dv_get(v_result__251309, "remaining"))) });
                        if (R.Bool(F_dv_get(v_result__251309, "accepted")))
                        {
                            return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(2) { F_dv_pair("mind", F_dv_get(v_result__251309, "world")), F_dv_pair("argument", F_dv_with_many(F_dv_get(v_result__251309, "argument"), new System.Collections.Generic.List<object>(3) { F_dv_pair("adoptions", F_dv_get(F_dv_get(v_actor, "mind"), "adoptions")), F_dv_pair("question_id", v_question__249113), F_dv_pair("hypothesis_id", F_dv_get(v_hypothesis__249076, "id")) })) });
                        }
                    }
                }
            }
            v_i__249024 = R.add(v_i__249024, R.number("1"));
        }
        return v_actor;
    }

    public static object F_dvs_argument_action_claims(object v_actor, object v_action)
    {
        if (R.Bool((R.Bool(R.not(F_dvs_argument_valid(v_actor))) ? (object)(true) : (object)(R.not(F_dvs_argument_inputs_current(v_actor))))))
        {
            return false;
        }
        object v_argument__252934 = F_dv_get(v_actor, "argument");
        if (R.Bool((R.Bool(R.eq(F_dv_find(v_argument__252934, "question_id", ""), "")) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_action, "argument_question", F_dv_get(v_argument__252934, "question_id")), F_dv_get(v_argument__252934, "question_id")))))))
        {
            return false;
        }
        object v_requested__253126 = F_dv_find(v_action, "argument_slots", new System.Collections.Generic.List<object>(0) { });
        if (R.Bool(R.eq(R.count(v_requested__253126), R.number("0"))))
        {
            return false;
        }
        object v_claims__253238 = F_dv_find(v_argument__252934, "claims", new System.Collections.Generic.List<object>(0) { });
        object v_result__253287 = new System.Collections.Generic.List<object>(0) { };
        object v_used__253307 = new System.Collections.Generic.List<object>(0) { };
        object v_i__253325 = R.number("0");
        while (R.Bool(R.lt(v_i__253325, R.count(v_requested__253126))))
        {
            object v_slot__253378 = R.at(v_requested__253126, v_i__253325);
            if (R.Bool(F_dv_has(v_used__253307, v_slot__253378)))
            {
                return false;
            }
            _ = R.push(v_used__253307, v_slot__253378);
            object v_found__253494 = false;
            object v_j__253520 = R.number("0");
            while (R.Bool(R.lt(v_j__253520, R.count(v_claims__253238))))
            {
                if (R.Bool(R.eq(F_dv_find(R.at(v_claims__253238, v_j__253520), "slot_id", ""), v_slot__253378)))
                {
                    if (R.Bool((R.Bool((R.Bool(R.not(R.eq(v_found__253494, false))) ? (object)(true) : (object)(R.not(F_dv_has_key(R.at(v_claims__253238, v_j__253520), "ref"))))) ? (object)(true) : (object)(R.not(F_dv_has_key(R.at(v_claims__253238, v_j__253520), "value"))))))
                    {
                        return false;
                    }
                    v_found__253494 = R.at(v_claims__253238, v_j__253520);
                }
                v_j__253520 = R.add(v_j__253520, R.number("1"));
            }
            if (R.Bool(R.eq(v_found__253494, false)))
            {
                return false;
            }
            _ = R.push(v_result__253287, v_found__253494);
            v_i__253325 = R.add(v_i__253325, R.number("1"));
        }
        return v_result__253287;
    }

    public static object F_dvs_argument_claim_proposition(object v_claims)
    {
        object v_propositions__253989 = new System.Collections.Generic.List<object>(0) { };
        object v_i__254015 = R.number("0");
        while (R.Bool(R.lt(v_i__254015, R.count(v_claims))))
        {
            _ = R.push(v_propositions__253989, F_dv_fact(F_dv_get(R.at(v_claims, v_i__254015), "ref"), F_dv_get(R.at(v_claims, v_i__254015), "value")));
            v_i__254015 = R.add(v_i__254015, R.number("1"));
        }
        return F_dv_all(v_propositions__253989);
    }

    public static object F_dvs_argument_reassessment_sources(object v_actor, object v_action, object v_argument)
    {
        object v_heard__254246 = F_dv_find(v_actor, "heard_utterances", new System.Collections.Generic.List<object>(0) { });
        object v_scope__254301 = F_dv_find(v_action, "reassessment_source_scope", "");
        object v_topic__254366 = F_dv_find(F_dv_find(v_actor, "heard_topics", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_argument, "question_id"), new System.Collections.Generic.List<object>(0) { });
        object v_result__254452 = new System.Collections.Generic.List<object>(0) { };
        object v_i__254472 = R.number("0");
        while (R.Bool(R.lt(v_i__254472, R.count(v_heard__254246))))
        {
            object v_record__254521 = R.at(R.at(v_heard__254246, v_i__254472), R.number("1"));
            if (R.Bool((R.Bool((R.Bool(R.not(R.eq(v_scope__254301, ""))) ? (object)(R.eq(v_scope__254301, F_dv_get(v_record__254521, "scope"))) : (object)(false))) ? (object)(F_dv_has(F_dv_find(v_topic__254366, "origin_speakers", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_record__254521, "speaker"))) : (object)(false))))
            {
                v_result__254452 = F_dv_explanation_merge(v_result__254452, F_dv_single(F_dv_get(v_record__254521, "source_event")));
            }
            v_i__254472 = R.add(v_i__254472, R.number("1"));
        }
        return v_result__254452;
    }

    public static object F_dvs_argument_proposal(object v_actor, object v_action, object v_claims)
    {
        object v_argument__254834 = F_dv_get(v_actor, "argument");
        return new System.Collections.Generic.List<object>(8) { F_dv_pair("argument_id", F_dv_get(v_argument__254834, "id")), F_dv_pair("id", F_dv_get(v_argument__254834, "id")), F_dv_pair("estimated", F_dv_get(v_argument__254834, "estimated")), F_dv_pair("confidence", F_dv_get(v_argument__254834, "confidence")), F_dv_pair("claims", v_claims), F_dv_pair("source_event_ids", F_dv_get(v_argument__254834, "source_event_ids")), F_dv_pair("model_event", F_dv_get(v_argument__254834, "model_event")), F_dv_pair("reassessment_sources", F_dvs_argument_reassessment_sources(v_actor, v_action, v_argument__254834)) };
    }

    public static object F_dvs_argument_surface(object v_action, object v_claims)
    {
        object v_declaration__255380 = F_dv_find(v_action, "argument_surface", false);
        if (R.Bool(R.eq(v_declaration__255380, false)))
        {
            return F_dv_find(F_dv_find(v_action, "argument_speech_template", F_dv_find(v_action, "speech", new System.Collections.Generic.List<object>(0) { })), "surface", "");
        }
        object v_text__255588 = F_dv_get(v_declaration__255380, "template");
        object v_bindings__255624 = F_dv_find(v_declaration__255380, "bindings", new System.Collections.Generic.List<object>(0) { });
        object v_i__255680 = R.number("0");
        while (R.Bool(R.lt(v_i__255680, R.count(v_bindings__255624))))
        {
            object v_binding__255732 = R.at(v_bindings__255624, v_i__255680);
            object v_found__255766 = false;
            object v_j__255792 = R.number("0");
            while (R.Bool(R.lt(v_j__255792, R.count(v_claims))))
            {
                if (R.Bool(R.eq(F_dv_get(R.at(v_claims, v_j__255792), "slot_id"), F_dv_get(v_binding__255732, "slot"))))
                {
                    v_found__255766 = R.at(v_claims, v_j__255792);
                }
                v_j__255792 = R.add(v_j__255792, R.number("1"));
            }
            if (R.Bool(R.eq(v_found__255766, false)))
            {
                return false;
            }
            object v_value__256004 = R.show(F_dv_get(v_found__255766, "value"));
            object v_labels__256045 = F_dv_find(v_binding__255732, "labels", new System.Collections.Generic.List<object>(0) { });
            if (R.Bool(R.not(F_dv_has_key(v_labels__256045, v_value__256004))))
            {
                return false;
            }
            v_text__255588 = F_dvs_present_replace(v_text__255588, R.cat("{", R.cat(F_dv_get(v_binding__255732, "name"), "}")), F_dv_get(v_labels__256045, v_value__256004));
            v_i__255680 = R.add(v_i__255680, R.number("1"));
        }
        return v_text__255588;
    }

    public static object F_dvs_argument_action_current(object v_actor, object v_action)
    {
        if (R.Bool(R.not(F_dvs_action_requires_argument(v_action))))
        {
            return true;
        }
        if (R.Bool((R.Bool(R.not(F_dv_find(v_action, "argument_available", false))) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_find(v_action, "argument", false), F_dv_find(v_actor, "argument", false)))))))
        {
            return false;
        }
        object v_claims__256579 = F_dvs_argument_action_claims(v_actor, v_action);
        if (R.Bool(R.eq(v_claims__256579, false)))
        {
            return false;
        }
        object v_proposition__256683 = F_dvs_argument_claim_proposition(v_claims__256579);
        if (R.Bool(F_dv_find(v_action, "speech_from_argument", false)))
        {
            object v_speech__256803 = F_dv_find(v_action, "speech", new System.Collections.Generic.List<object>(0) { });
            object v_proposal__256854 = F_dvs_argument_proposal(v_actor, v_action, v_claims__256579);
            object v_surface__256922 = F_dvs_argument_surface(v_action, v_claims__256579);
            return (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.eq(v_surface__256922, false))) ? (object)(F_dv_same(F_dv_find(v_action, "surface", ""), v_surface__256922)) : (object)(false))) ? (object)(F_dv_same(F_dv_find(v_speech__256803, "surface", ""), v_surface__256922)) : (object)(false))) ? (object)(F_dv_same(F_dv_find(v_speech__256803, "literal_ast", false), v_proposition__256683)) : (object)(false))) ? (object)(F_dv_same(F_dv_find(v_speech__256803, "proposal", false), v_proposal__256854)) : (object)(false))) ? (object)(F_dv_same(F_dv_find(v_action, "proposal", false), v_proposal__256854)) : (object)(false));
        }
        return F_dv_same(F_dv_find(v_action, "answer", false), v_proposition__256683);
    }

    public static object F_dvs_argument_action_evidence(object v_actor, object v_action, object v_events)
    {
        if (R.Bool(R.not(F_dvs_action_requires_argument(v_action))))
        {
            return true;
        }
        if (R.Bool(R.not(F_dvs_argument_action_current(v_actor, v_action))))
        {
            return false;
        }
        object v_proof__257563 = F_dv_with(v_action, "answer", F_dvs_argument_claim_proposition(F_dvs_argument_action_claims(v_actor, v_action)));
        return F_dvs_public_argument_valid(v_proof__257563, new System.Collections.Generic.List<object>(0) { }, v_events, F_dv_get(v_actor, "id"));
    }

    public static object F_dvs_argument_action(object v_actor, object v_action, object v_events)
    {
        if (R.Bool(R.not(F_dvs_action_requires_argument(v_action))))
        {
            return v_action;
        }
        object v_result__257949 = F_dv_with(F_dv_without(F_dv_without(v_action, "argument"), "proposal"), "argument_available", false);
        v_result__257949 = F_dv_without(v_result__257949, "answer");
        if (R.Bool(F_dv_find(v_action, "speech_from_argument", false)))
        {
            object v_template__258160 = F_dv_without(F_dv_without(F_dv_find(v_action, "argument_speech_template", F_dv_find(v_action, "speech", new System.Collections.Generic.List<object>(0) { })), "literal_ast"), "proposal");
            v_result__257949 = F_dv_without(v_result__257949, "speech");
            v_result__257949 = F_dv_without(v_result__257949, "surface");
            if (R.Bool(R.lt(R.number("0"), R.count(v_template__258160))))
            {
                v_result__257949 = F_dv_with(v_result__257949, "argument_speech_template", v_template__258160);
            }
        }
        object v_claims__258508 = F_dvs_argument_action_claims(v_actor, v_action);
        if (R.Bool(R.eq(v_claims__258508, false)))
        {
            return v_result__257949;
        }
        object v_proposition__258613 = F_dvs_argument_claim_proposition(v_claims__258508);
        object v_proof__258674 = F_dv_with_many(v_result__257949, new System.Collections.Generic.List<object>(2) { F_dv_pair("argument", F_dv_get(v_actor, "argument")), F_dv_pair("answer", v_proposition__258613) });
        if (R.Bool(R.not(F_dvs_public_argument_valid(v_proof__258674, new System.Collections.Generic.List<object>(0) { }, v_events, F_dv_get(v_actor, "id")))))
        {
            return v_result__257949;
        }
        v_result__257949 = F_dv_with_many(v_result__257949, new System.Collections.Generic.List<object>(2) { F_dv_pair("argument", F_dv_get(v_actor, "argument")), F_dv_pair("argument_available", true) });
        if (R.Bool(F_dv_find(v_action, "speech_from_argument", false)))
        {
            object v_proposal__259004 = F_dvs_argument_proposal(v_actor, v_action, v_claims__258508);
            object v_surface__259072 = F_dvs_argument_surface(v_result__257949, v_claims__258508);
            if (R.Bool((R.Bool(R.eq(v_surface__259072, false)) ? (object)(true) : (object)(R.eq(v_surface__259072, "")))))
            {
                return F_dv_with(v_result__257949, "argument_available", false);
            }
            return F_dv_with_many(v_result__257949, new System.Collections.Generic.List<object>(3) { F_dv_pair("surface", v_surface__259072), F_dv_pair("speech", F_dv_with_many(F_dv_find(v_result__257949, "argument_speech_template", new System.Collections.Generic.List<object>(0) { }), new System.Collections.Generic.List<object>(3) { F_dv_pair("literal_ast", v_proposition__258613), F_dv_pair("proposal", v_proposal__259004), F_dv_pair("surface", v_surface__259072) })), F_dv_pair("proposal", v_proposal__259004) });
        }
        return F_dv_with(v_result__257949, "answer", v_proposition__258613);
    }

    public static object F_dvs_materialize_arguments(object v_actor, object v_events)
    {
        object v_actions__259558 = F_dv_get(v_actor, "actions");
        object v_changed__259590 = false;
        object v_i__259614 = R.number("0");
        while (R.Bool(R.lt(v_i__259614, R.count(v_actions__259558))))
        {
            if (R.Bool(F_dvs_action_requires_argument(R.at(v_actions__259558, v_i__259614))))
            {
                object v_action__259722 = F_dvs_argument_action(v_actor, R.at(v_actions__259558, v_i__259614), v_events);
                if (R.Bool(R.not(F_dv_same(v_action__259722, R.at(v_actions__259558, v_i__259614)))))
                {
                    v_actions__259558 = F_dvs_replace(v_actions__259558, v_i__259614, v_action__259722);
                    v_changed__259590 = true;
                }
            }
            v_i__259614 = R.add(v_i__259614, R.number("1"));
        }
        if (R.Bool(R.not(v_changed__259590)))
        {
            return v_actor;
        }
        return F_dv_with(v_actor, "actions", v_actions__259558);
    }

    public static object F_dvs_factor_coverage(object v_actor, object v_hypothesis, object v_events)
    {
        object v_candidates__260080 = F_dv_find(F_dv_get(v_actor, "policy"), "hypotheses", new System.Collections.Generic.List<object>(0) { });
        object v_catalog__260141 = new System.Collections.Generic.List<object>(0) { };
        object v_certificates__260162 = F_dv_find(v_actor, "factor_certificates", new System.Collections.Generic.List<object>(0) { });
        object v_verified__260227 = new System.Collections.Generic.List<object>(0) { };
        object v_i__260249 = R.number("0");
        while (R.Bool(R.lt(v_i__260249, R.count(v_candidates__260080))))
        {
            object v_candidate__260305 = R.at(v_candidates__260080, v_i__260249);
            if (R.Bool(F_dv_and(F_dvp_topic(v_actor, v_candidate__260305), R.eq(F_dv_find(v_candidate__260305, "group", ""), F_dv_find(v_hypothesis, "group", "")))))
            {
                _ = R.push(v_catalog__260141, v_candidate__260305);
            }
            v_i__260249 = R.add(v_i__260249, R.number("1"));
        }
        if (R.Bool(F_dvs_factor_catalog_complete(F_dv_find(F_dv_get(v_actor, "policy"), "factor_domains", new System.Collections.Generic.List<object>(0) { }), v_catalog__260141)))
        {
            v_i__260249 = R.number("0");
            while (R.Bool(R.lt(v_i__260249, R.count(v_certificates__260162))))
            {
                if (R.Bool(F_dvs_factor_certificate_current(v_actor, R.at(v_certificates__260162, v_i__260249), v_events)))
                {
                    _ = R.push(v_verified__260227, R.at(v_certificates__260162, v_i__260249));
                }
                v_i__260249 = R.add(v_i__260249, R.number("1"));
            }
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("catalog", v_catalog__260141), F_dv_pair("certificates", v_verified__260227) };
    }

    public static object F_dvs_factor_covered(object v_candidate, object v_certificates)
    {
        object v_i__260927 = R.number("0");
        object v_covered__260941 = false;
        object v_assignments__260965 = F_dv_find(v_candidate, "variable_assignments", new System.Collections.Generic.List<object>(0) { });
        while (R.Bool(R.lt(v_i__260927, R.count(v_certificates))))
        {
            if (R.Bool(F_dvs_factor_covers(R.at(v_certificates, v_i__260927), v_assignments__260965)))
            {
                v_covered__260941 = true;
            }
            v_i__260927 = R.add(v_i__260927, R.number("1"));
        }
        return v_covered__260941;
    }
}
