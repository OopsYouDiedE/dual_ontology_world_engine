// @mechanism: dual_ontology_state
// @layer: core/ontology
// @contracts: Arguments

using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using System.Buffers;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;
using System;

// --- Source: Arguments.cs ---
namespace DuelView.Core
{


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
}

// --- Source: ArgumentValidation.cs ---
namespace DuelView.Core
{


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
}

// --- Source: Requests.cs ---
namespace DuelView.Core
{


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
        object bound = F_dv_find(v_action, "response_to", false);
        object v_topic__72646 = F_dv_find(v_action, "response_topic", bound is false ? "" : F_dv_get(bound, "topic"));
        object v_requests__72700 = F_dv_find(v_actor, "received_requests", new System.Collections.Generic.List<object>(0) { });
        object v_answered__72759 = F_dv_find(v_actor, "answered_requests", new System.Collections.Generic.List<object>(0) { });
        object v_result__72818 = new System.Collections.Generic.List<object>(0) { };
        object v_i__72838 = R.number("0");
        while (R.Bool(F_dv_and(R.eq(R.count(v_result__72818), R.number("0")), R.lt(v_i__72838, R.count(v_requests__72700)))))
        {
            object v_request__72925 = R.at(v_requests__72700, v_i__72838);
            if (bound is not false && (!R.Bool(R.eq(F_dv_get(bound, "source_event"), F_dv_get(v_request__72925, "source_event")))
                || !R.Bool(R.eq(F_dv_get(bound, "requester"), F_dv_get(v_request__72925, "requester")))))
            {
                v_i__72838 = R.add(v_i__72838, R.number("1"));
                continue;
            }
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

    public static object F_dvs_receive_request(object actor, object speech, object parseEvent, object source, object speaker, object? message = null)
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
            F_dv_with_many(new List<object> { F_dv_pair("topic", F_dv_get(requestDeclaration, "topic")), F_dv_pair("parse_event", parseEvent), F_dv_pair("source_event", source),
                F_dv_pair("received_time", message is null ? F_dv_get(F_dv_get(actor, "mind"), "time") : F_dv_get(message, "received_time")) },
                new List<object> { F_dv_pair("requester", speaker), F_dv_pair("request", requestDeclaration) })));
    }

    // A response remains attached to the exact question that selected it. A
    // later request with the same topic cannot silently take its place.
    public static object F_dvs_bind_reply(object actor, object action)
    {
        if (F_dv_find(action, "speech", false) is false || F_dv_find(action, "response_to", false) is not false) return action;
        object request = F_dvs_pending_request(actor, action);
        if (R.Items(request).Count == 0 && F_dv_find(action, "response_topic", false) is false)
        {
            // An introduction already in flight can itself answer a question.
            // Match declared requested references, never infer from prose.
            var speech = F_dv_get(action, "speech");
            foreach (var candidate in R.Items(F_dv_find(actor, "received_requests", new List<object>())))
            {
                if (R.Bool(F_dv_has(F_dv_find(actor, "answered_requests", new List<object>()), F_dv_get(candidate, "parse_event")))
                    || R.Bool(F_dvs_request_withdrawn(actor, candidate))) continue;
                var refs = F_dv_find(F_dv_get(candidate, "request"), "answer_refs", new List<object>());
                if (R.Items(refs).Count == 0 || !R.Bool(F_dv_has(F_dv_find(action, "listeners", new List<object>()), F_dv_get(candidate, "requester")))) continue;
                if (R.Items(refs).All(reference => SpeechSuppliesReference(F_dv_get(speech, "literal_ast"), reference)))
                {
                    request = candidate;
                    break;
                }
            }
        }
        if (R.Items(request).Count == 0) return action;
        object reply = new List<object> { F_dv_pair("requester", F_dv_get(request, "requester")), F_dv_pair("topic", F_dv_get(request, "topic")),
            F_dv_pair("source_event", F_dv_get(request, "source_event")), F_dv_pair("parse_event", F_dv_get(request, "parse_event")) };
        return F_dv_with_many(action, new List<object> { F_dv_pair("response_to", reply),
            F_dv_pair("speech", F_dv_with(F_dv_get(action, "speech"), "reply_to", reply)) });
    }

    private static bool SpeechSuppliesReference(object literal, object reference)
    {
        var operation = R.Text(F_dv_find(literal, "op", ""));
        if (operation == "equals") return R.Bool(F_dv_same(F_dv_get(literal, "ref"), reference));
        return operation == "all" && R.Items(F_dv_get(literal, "items")).Any(item => SpeechSuppliesReference(item, reference));
    }

    public static object F_dvs_receive_reply(object listener, object speech, object source, object parseEvent, object speaker)
    {
        var reply = F_dv_find(speech, "reply_to", false);
        if (reply is false || !R.Bool(R.eq(F_dv_get(reply, "requester"), F_dv_get(listener, "id")))) return listener;
        var sent = R.Items(F_dv_find(listener, "sent_requests", new List<object>())).Any(request =>
            R.Bool(R.eq(F_dv_get(request, "source_event"), F_dv_get(reply, "source_event"))) && R.Bool(R.eq(F_dv_get(request, "target"), speaker)));
        if (!sent) return listener;
        return F_dv_with(listener, "received_replies", F_dv_append(F_dv_find(listener, "received_replies", new List<object>()),
            F_dv_with_many(reply, new List<object> { F_dv_pair("speaker", speaker), F_dv_pair("reply_event", source), F_dv_pair("reply_parse_event", parseEvent) })));
    }

    public static object F_dvs_conversation_candidates(object actor, object actions, object costs)
    {
        // A request offers a reply; it does not override the recipient's needs or commitments.
        return actions;
    }

    private static List<object> ConversationReplies(object actor, object actions, object costs, object source)
    {
        var replies = new List<object>();
        var relevant = F_dvp_goal_refs(actor);
        foreach (var candidate in R.Items(actions))
        {
            var action = F_dvs_bind_reply(actor, candidate);
            var bound = F_dv_find(action, "response_to", false);
            if (bound is false || !R.Bool(R.eq(F_dv_get(bound, "source_event"), source))) continue;
            if (!R.Bool(F_dvp_eligible(actor, action, F_dv_get(actor, "mind"), relevant))
                || !R.Bool(F_dvs_local_candidate_admissible(actor, action))) continue;
            var cost = F_dv_get(costs, F_dv_get(action, "kind"));
            if (!R.Bool(F_dv_action_cost_le(F_dv_get(cost, "maximum"), F_dv_get(F_dv_get(actor, "ledger"), "balances")))) continue;
            if (R.Bool(R.lt(F_dv_find(F_dv_get(actor, "policy"), "remaining_game_time", F_dv_get(F_dv_get(cost, "maximum"), "time")), F_dv_get(F_dv_get(cost, "maximum"), "time")))) continue;
            replies.Add(action);
        }
        return replies;
    }

    public static object F_dvs_conversation_wait_until(object actor, object state)
    {
        var physical = F_dv_get(state, "physical");
        var now = F_dv_get(physical, "time");
        if (F_dv_find(actor, "scheduled_action", false) is not false) return now;
        if (R.Text(F_dv_find(actor, "simulation_priority_action", "")).Length > 0) return now;
        var sentRequests = R.Items(F_dv_find(actor, "sent_requests", new List<object>()));
        if (sentRequests.Count == 0) return now;
        var position = F_dv_read(physical, F_dv_get(actor, "location_ref"));
        if (!R.Bool(F_dv_uncontested(position))) return now;
        foreach (var request in sentRequests.Reverse())
        {
            if (!R.Bool(F_dv_find(request, "wait_for_reply", false))) return now;
            var until = F_dv_find(request, "reply_deadline", now);
            if (!R.Bool(R.lt(now, until))) return now;
            if (R.Items(F_dv_find(actor, "received_replies", new List<object>())).Any(reply =>
                R.Bool(R.eq(F_dv_get(reply, "source_event"), F_dv_get(request, "source_event"))))) return now;
            var refs = F_dv_find(request, "answer_refs", new List<object>());
            if (R.Items(refs).Count > 0 && R.Items(refs).All(reference => R.Bool(F_dv_uncontested(F_dv_read(F_dv_get(actor, "mind"), reference))))) return now;
            var target = R.Items(F_dv_get(state, "actors")).FirstOrDefault(other => R.Bool(R.eq(F_dv_get(other, "id"), F_dv_get(request, "target"))));
            if (target is null || !R.Bool(F_dvs_actor_can_act(target))) return now;
            var otherPosition = F_dv_read(physical, F_dv_get(target, "location_ref"));
            if (!R.Bool(F_dv_uncontested(otherPosition))) return now;
            if (!R.Bool(F_dv_same(F_dv_get(position, "value"), F_dv_get(otherPosition, "value"))))
            {
                var question = F_dvp_find_action(F_dv_get(actor, "actions"), F_dv_get(request, "action_id"));
                if (question is false || F_dv_find(question, "request_transport", false) is false
                    || F_dvs_request_context(actor, question, F_dv_get(target, "id")) is false
                    || !R.Bool(F_dvs_speech_contact_ready(actor, question, F_dv_get(state, "actors"), physical))) return now;
            }
            if (!R.Items(F_dv_find(target, "received_requests", new List<object>())).Any(received =>
                R.Bool(R.eq(F_dv_get(received, "source_event"), F_dv_get(request, "source_event"))) && !R.Bool(F_dvs_request_withdrawn(target, received)))) return now;
            var queued = F_dv_find(target, "scheduled_action", false);
            if (queued is not false)
            {
                var queuedReply = F_dvs_bind_reply(target, F_dv_get(queued, "action"));
                var bound = F_dv_find(queuedReply, "response_to", false);
                if (bound is not false && R.Bool(R.eq(F_dv_get(bound, "source_event"), F_dv_get(request, "source_event")))
                    && R.Bool(F_dvs_speech_contact_ready(target, queuedReply, F_dv_get(state, "actors"), physical)))
                    return F_dv_min(until, F_dv_get(state, "horizon"));
            }
            return ConversationReplies(target, F_dv_get(target, "actions"), F_dv_get(state, "costs"), F_dv_get(request, "source_event"))
                .Any(reply => R.Bool(F_dvs_speech_contact_ready(target, reply, F_dv_get(state, "actors"), physical)))
                ? F_dv_min(until, F_dv_get(state, "horizon")) : now;
        }
        return now;
    }

    public static object F_dvs_speech_contact_ready(object actor, object action, object actors, object physical)
    {
        var speech = F_dv_find(action, "speech", false);
        if (speech is false) return true;
        var request = F_dv_find(speech, "request", false);
        var response = F_dv_find(action, "response_to", false);
        object target = request is not false ? F_dv_get(request, "target") : response is not false ? F_dv_get(response, "requester") : "";
        if (R.Text(target).Length == 0) return !R.Bool(F_dv_find(action, "requires_actual_request", false));
        if (!R.Bool(F_dv_has(F_dv_find(action, "listeners", new List<object>()), target))) return false;
        if (response is not false && R.Items(F_dvs_pending_request(actor, action)).Count == 0) return false;
        var listener = R.Items(actors).FirstOrDefault(other => R.Bool(R.eq(F_dv_get(other, "id"), target)));
        return listener is not null && R.Bool(F_dvs_speech_hearing(actor, listener, speech, physical));
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
        if (R.Bool(R.not(R.eq(v_visible__75678, false))) && R.Bool(F_dvs_current_local_contact(v_actor, v_target)))
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
}
