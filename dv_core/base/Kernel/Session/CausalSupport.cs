using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Session / CausalSupport: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_update_support_events(object v_actor, object v_events)
    {
        v_events = R.snapshot(v_events);
        object v_support__261279 = F_dv_find(v_actor, "support_events", new System.Collections.Generic.List<object>(0) { });
        object v_cursor__261334 = F_dv_find(v_actor, "support_events_cursor", R.number("0"));
        object v_total__261394 = R.count(v_events);
        if (R.Bool(R.eq(v_cursor__261334, v_total__261394)))
        {
            return v_actor;
        }
        object v_subject__261472 = F_dv_get(v_actor, "id");
        object v_index_key__261499 = new System.Collections.Generic.List<object>(2) { "support-compact-index-v1", v_subject__261472 };
        object v_cached_index__261557 = R.memo_get(v_support__261279, v_index_key__261499);
        object v_index__261612 = false;
        if (R.Bool(R.at(v_cached_index__261557, R.number("0"))))
        {
            v_index__261612 = R.at(v_cached_index__261557, R.number("1"));
        }
        object v_added__261690 = false;
        while (R.Bool(R.lt(v_cursor__261334, v_total__261394)))
        {
            object v_event__261742 = R.at(v_events, v_cursor__261334);
            if (R.Bool(R.eq(F_dv_get(v_event__261742, "actor"), v_subject__261472)))
            {
                object v_type__261816 = F_dv_get(v_event__261742, "type");
                if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(v_type__261816, "observation")) ? (object)(true) : (object)(R.eq(v_type__261816, "listener_parse")))) ? (object)(true) : (object)(R.eq(v_type__261816, "inference")))) ? (object)(true) : (object)(R.eq(v_type__261816, "model_argument_accepted")))))
                {
                    if (R.Bool(R.not(R.eq(v_index__261612, false))))
                    {
                        v_index__261612 = F_dvs_support_compact_add(v_index__261612, v_event__261742, R.count(v_support__261279), v_subject__261472);
                    }
                    _ = R.push(v_support__261279, v_event__261742);
                    v_added__261690 = true;
                }
            }
            v_cursor__261334 = R.add(v_cursor__261334, R.number("1"));
        }
        if (R.Bool((R.Bool(v_added__261690) ? (object)(R.not(R.eq(v_index__261612, false))) : (object)(false))))
        {
            _ = R.memo_put(v_support__261279, v_index_key__261499, v_index__261612);
        }
        return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(2) { F_dv_pair("support_events", v_support__261279), F_dv_pair("support_events_cursor", v_total__261394) });
    }

    public static object F_dvs_subject_support_events(object v_events, object v_subject)
    {
        object v_result__262415 = new System.Collections.Generic.List<object>(0) { };
        object v_j__262435 = R.number("0");
        object v_total__262449 = R.count(v_events);
        while (R.Bool(R.lt(v_j__262435, v_total__262449)))
        {
            object v_event__262509 = R.at(v_events, v_j__262435);
            if (R.Bool(R.eq(F_dv_get(v_event__262509, "actor"), v_subject)))
            {
                object v_type__262580 = F_dv_get(v_event__262509, "type");
                if (R.Bool(F_dv_or(R.eq(v_type__262580, "observation"), F_dv_or(R.eq(v_type__262580, "listener_parse"), F_dv_or(R.eq(v_type__262580, "inference"), R.eq(v_type__262580, "model_argument_accepted"))))))
                {
                    _ = R.push(v_result__262415, v_event__262509);
                }
            }
            v_j__262435 = R.add(v_j__262435, R.number("1"));
        }
        return v_result__262415;
    }

    public static object F_dvs_support_ref_key(object v_subject, object v_ref)
    {
        object v_key__262948 = new System.Collections.Generic.List<object>(2) { "dvs_support_ref_key", v_subject };
        object v_cached__262995 = R.memo_get(v_ref, v_key__262948);
        if (R.Bool(R.at(v_cached__262995, R.number("0"))))
        {
            return R.at(v_cached__262995, R.number("1"));
        }
        return R.memo_put(v_ref, v_key__262948, R.json_stringify(new System.Collections.Generic.List<object>(5) { F_dv_get(v_ref, "type"), F_dv_get(v_ref, "object"), F_dv_get(v_ref, "facet"), F_dv_get(v_ref, "field"), F_dv_find(v_ref, "subject", v_subject) }));
    }

    public static object F_dvs_support_compact_add(object v_index, object v_event, object v_position, object v_subject)
    {
        object v_refs__263494 = F_dv_get(v_index, "refs");
        object v_accepted__263520 = F_dv_get(v_index, "accepted");
        if (R.Bool((R.Bool(R.eq(F_dv_get(v_event, "type"), "observation")) ? (object)(true) : (object)(R.eq(F_dv_get(v_event, "type"), "listener_parse")))))
        {
            object v_observations__263628 = F_dv_get(F_dv_get(v_event, "data"), "observations");
            object v_i__263679 = R.number("0");
            while (R.Bool(R.lt(v_i__263679, R.count(v_observations__263628))))
            {
                object v_observation__263743 = R.at(v_observations__263628, v_i__263679);
                object v_key__263789 = F_dvs_support_ref_key(v_subject, F_dv_get(v_observation__263743, "ref"));
                object v_entries__263857 = F_dv_find(v_refs__263494, v_key__263789, new System.Collections.Generic.List<object>(0) { });
                _ = R.push(v_entries__263857, new System.Collections.Generic.List<object>(4) { F_dv_pair("position", v_position), F_dv_pair("event", F_dv_get(v_event, "id")), F_dv_pair("kind", "observation"), F_dv_pair("observation", v_observation__263743) });
                v_refs__263494 = F_dv_with(v_refs__263494, v_key__263789, v_entries__263857);
                v_i__263679 = R.add(v_i__263679, R.number("1"));
            }
        }
        if (R.Bool(R.eq(F_dv_get(v_event, "type"), "inference")))
        {
            object v_changes__264128 = F_dv_get(F_dv_get(F_dv_get(v_event, "data"), "receipt"), "changes");
            object v_i__264177 = R.number("0");
            while (R.Bool(R.lt(v_i__264177, R.count(v_changes__264128))))
            {
                object v_change__264236 = R.at(v_changes__264128, v_i__264177);
                object v_key__264272 = F_dvs_support_ref_key(v_subject, F_dv_get(v_change__264236, "ref"));
                object v_entries__264335 = F_dv_find(v_refs__263494, v_key__264272, new System.Collections.Generic.List<object>(0) { });
                _ = R.push(v_entries__264335, new System.Collections.Generic.List<object>(5) { F_dv_pair("position", v_position), F_dv_pair("event", F_dv_get(v_event, "id")), F_dv_pair("kind", "inference"), F_dv_pair("ref", F_dv_get(v_change__264236, "ref")), F_dv_pair("after", F_dv_get(v_change__264236, "after")) });
                v_refs__263494 = F_dv_with(v_refs__263494, v_key__264272, v_entries__264335);
                v_i__264177 = R.add(v_i__264177, R.number("1"));
            }
        }
        if (R.Bool(R.eq(F_dv_get(v_event, "type"), "model_argument_accepted")))
        {
            _ = R.push(v_accepted__263520, new System.Collections.Generic.List<object>(2) { F_dv_pair("position", v_position), F_dv_pair("event", F_dv_get(v_event, "id")) });
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("refs", v_refs__263494), F_dv_pair("accepted", v_accepted__263520) };
    }

    public static object F_dvs_support_compact_index(object v_events, object v_subject)
    {
        object v_key__264793 = new System.Collections.Generic.List<object>(2) { "support-compact-index-v1", v_subject };
        object v_cached__264845 = R.memo_get(v_events, v_key__264793);
        if (R.Bool(R.at(v_cached__264845, R.number("0"))))
        {
            return R.at(v_cached__264845, R.number("1"));
        }
        object v_index__264930 = new System.Collections.Generic.List<object>(2) { F_dv_pair("refs", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("accepted", new System.Collections.Generic.List<object>(0) { }) };
        object v_i__264975 = R.number("0");
        while (R.Bool(R.lt(v_i__264975, R.count(v_events))))
        {
            v_index__264930 = F_dvs_support_compact_add(v_index__264930, R.at(v_events, v_i__264975), v_i__264975, v_subject);
            v_i__264975 = R.add(v_i__264975, R.number("1"));
        }
        return R.memo_put(v_events, v_key__264793, v_index__264930);
    }

    public static object F_dvs_support_cell_parents(object v_cell, object v_ref, object v_subject, object v_entries, object v_accepted)
    {
        object v_key__265221 = new System.Collections.Generic.List<object>(5) { "support-cell-parents-v1", v_ref, v_subject, v_entries, v_accepted };
        object v_cached__265296 = R.memo_get(v_cell, v_key__265221);
        if (R.Bool(R.at(v_cached__265296, R.number("0"))))
        {
            return R.at(v_cached__265296, R.number("1"));
        }
        object v_claims__265379 = F_dv_find(v_cell, "claims", new System.Collections.Generic.List<object>(0) { });
        object v_basis__265424 = F_dv_find(v_cell, "basis", new System.Collections.Generic.List<object>(0) { });
        object v_parents__265467 = new System.Collections.Generic.List<object>(0) { };
        object v_i__265488 = R.number("0");
        object v_j__265502 = R.number("0");
        while (R.Bool((R.Bool(R.lt(v_i__265488, R.count(v_entries))) ? (object)(true) : (object)(R.lt(v_j__265502, R.count(v_accepted))))))
        {
            object v_from_entry__265579 = R.not(R.lt(v_j__265502, R.count(v_accepted)));
            if (R.Bool((R.Bool(R.lt(v_i__265488, R.count(v_entries))) ? (object)(R.lt(v_j__265502, R.count(v_accepted))) : (object)(false))))
            {
                v_from_entry__265579 = R.lt(F_dv_get(R.at(v_entries, v_i__265488), "position"), F_dv_get(R.at(v_accepted, v_j__265502), "position"));
            }
            object v_parent__265757 = "";
            if (R.Bool(v_from_entry__265579))
            {
                object v_entry__265808 = R.at(v_entries, v_i__265488);
                if (R.Bool(R.eq(F_dv_get(v_entry__265808, "kind"), "observation")))
                {
                    if (R.Bool((R.Bool(F_dv_same_local_ref(v_subject, F_dv_get(F_dv_get(v_entry__265808, "observation"), "ref"), v_ref)) ? (object)(F_dv_has(v_claims__265379, F_dv_get(v_entry__265808, "observation"))) : (object)(false))))
                    {
                        v_parent__265757 = F_dv_get(v_entry__265808, "event");
                    }
                }
                else
                {
                    if (R.Bool((R.Bool(F_dv_same(F_dv_get(v_entry__265808, "ref"), v_ref)) ? (object)(F_dv_same(F_dv_get(v_entry__265808, "after"), v_cell)) : (object)(false))))
                    {
                        v_parent__265757 = F_dv_get(v_entry__265808, "event");
                    }
                }
                v_i__265488 = R.add(v_i__265488, R.number("1"));
            }
            else
            {
                if (R.Bool(F_dv_has(v_basis__265424, F_dv_get(R.at(v_accepted, v_j__265502), "event"))))
                {
                    v_parent__265757 = F_dv_get(R.at(v_accepted, v_j__265502), "event");
                }
                v_j__265502 = R.add(v_j__265502, R.number("1"));
            }
            if (R.Bool((R.Bool(R.not(R.eq(v_parent__265757, ""))) ? (object)(R.not(F_dv_has(v_parents__265467, v_parent__265757))) : (object)(false))))
            {
                _ = R.push(v_parents__265467, v_parent__265757);
            }
        }
        return R.memo_put(v_cell, v_key__265221, v_parents__265467);
    }

    public static object F_dvs_support_event_index(object v_events, object v_subject)
    {
        object v_key__266500 = new System.Collections.Generic.List<object>(2) { "support-event-index", v_subject };
        object v_cached__266547 = R.memo_get(v_events, v_key__266500);
        if (R.Bool(R.at(v_cached__266547, R.number("0"))))
        {
            return R.at(v_cached__266547, R.number("1"));
        }
        object v_index__266632 = new System.Collections.Generic.List<object>(0) { };
        object v_accepted__266651 = new System.Collections.Generic.List<object>(0) { };
        object v_i__266673 = R.number("0");
        while (R.Bool(R.lt(v_i__266673, R.count(v_events))))
        {
            object v_event__266723 = R.at(v_events, v_i__266673);
            object v_refs__266753 = new System.Collections.Generic.List<object>(0) { };
            if (R.Bool((R.Bool(R.eq(F_dv_get(v_event__266723, "type"), "observation")) ? (object)(true) : (object)(R.eq(F_dv_get(v_event__266723, "type"), "listener_parse")))))
            {
                object v_observations__266853 = F_dv_get(F_dv_get(v_event__266723, "data"), "observations");
                object v_j__266908 = R.number("0");
                while (R.Bool(R.lt(v_j__266908, R.count(v_observations__266853))))
                {
                    _ = R.push(v_refs__266753, F_dv_get(R.at(v_observations__266853, v_j__266908), "ref"));
                    v_j__266908 = R.add(v_j__266908, R.number("1"));
                }
            }
            if (R.Bool(R.eq(F_dv_get(v_event__266723, "type"), "inference")))
            {
                object v_changes__267091 = F_dv_get(F_dv_get(F_dv_get(v_event__266723, "data"), "receipt"), "changes");
                object v_j__267144 = R.number("0");
                while (R.Bool(R.lt(v_j__267144, R.count(v_changes__267091))))
                {
                    _ = R.push(v_refs__266753, F_dv_get(R.at(v_changes__267091, v_j__267144), "ref"));
                    v_j__267144 = R.add(v_j__267144, R.number("1"));
                }
            }
            if (R.Bool(R.eq(F_dv_get(v_event__266723, "type"), "model_argument_accepted")))
            {
                _ = R.push(v_accepted__266651, v_i__266673);
            }
            object v_j__267360 = R.number("0");
            while (R.Bool(R.lt(v_j__267360, R.count(v_refs__266753))))
            {
                object v_ref_key__267416 = F_dvs_support_ref_key(v_subject, R.at(v_refs__266753, v_j__267360));
                object v_positions__267480 = F_dv_find(v_index__266632, v_ref_key__267416, new System.Collections.Generic.List<object>(0) { });
                if (R.Bool(R.not(F_dv_has(v_positions__267480, v_i__266673))))
                {
                    _ = R.push(v_positions__267480, v_i__266673);
                    object v_slot__267619 = R.record_index(v_index__266632, v_ref_key__267416);
                    if (R.Bool(R.lt(v_slot__267619, R.number("0"))))
                    {
                        _ = R.push(v_index__266632, F_dv_pair(v_ref_key__267416, v_positions__267480));
                    }
                    else
                    {
                        _ = R.replace(v_index__266632, v_slot__267619, F_dv_pair(v_ref_key__267416, v_positions__267480));
                    }
                }
                v_j__267360 = R.add(v_j__267360, R.number("1"));
            }
            v_i__266673 = R.add(v_i__266673, R.number("1"));
        }
        return R.memo_put(v_events, v_key__266500, new System.Collections.Generic.List<object>(2) { F_dv_pair("refs", v_index__266632), F_dv_pair("accepted", v_accepted__266651) });
    }

    public static object F_dvs_support_parents(object v_mind, object v_refs, object v_events, object v_subject_or_actor)
    {
        object v_subject__268032 = v_subject_or_actor;
        object v_cand_events__268067 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(F_dv_record_like(v_subject_or_actor)))
        {
            v_subject__268032 = F_dv_get(v_subject_or_actor, "id");
            v_cand_events__268067 = F_dv_find(v_subject_or_actor, "support_events", new System.Collections.Generic.List<object>(0) { });
            if (R.Bool(R.eq(R.count(v_cand_events__268067), R.number("0"))))
            {
                v_cand_events__268067 = F_dvs_subject_support_events(v_events, v_subject__268032);
            }
        }
        else
        {
            v_cand_events__268067 = F_dvs_subject_support_events(v_events, v_subject__268032);
        }
        object v_support_index__268426 = F_dvs_support_compact_index(v_cand_events__268067, v_subject__268032);
        object v_parents__268498 = new System.Collections.Generic.List<object>(0) { };
        object v_i__268519 = R.number("0");
        while (R.Bool(R.lt(v_i__268519, R.count(v_refs))))
        {
            object v_ref__268569 = R.at(v_refs, v_i__268519);
            object v_cell__268595 = F_dv_read(v_mind, v_ref__268569);
            object v_entries__268633 = F_dv_find(F_dv_get(v_support_index__268426, "refs"), F_dvs_support_ref_key(v_subject__268032, v_ref__268569), new System.Collections.Generic.List<object>(0) { });
            object v_supported__268722 = F_dvs_support_cell_parents(v_cell__268595, v_ref__268569, v_subject__268032, v_entries__268633, F_dv_get(v_support_index__268426, "accepted"));
            object v_j__268824 = R.number("0");
            while (R.Bool(R.lt(v_j__268824, R.count(v_supported__268722))))
            {
                if (R.Bool(R.not(F_dv_has(v_parents__268498, R.at(v_supported__268722, v_j__268824)))))
                {
                    _ = R.push(v_parents__268498, R.at(v_supported__268722, v_j__268824));
                }
                v_j__268824 = R.add(v_j__268824, R.number("1"));
            }
            v_i__268519 = R.add(v_i__268519, R.number("1"));
        }
        return v_parents__268498;
    }

    public static object F_dvs_selection_parents(object v_actor, object v_action, object v_choice, object v_events, object v_parent)
    {
        object v_refs__269103 = F_dv_get(v_choice, "relevant_refs");
        object v_parents__269139 = F_dvs_cognitive_parents(v_actor, F_dv_single(v_parent));
        v_refs__269103 = F_dvp_merge_refs(v_refs__269103, F_dvs_sim_refs(F_dv_get(v_action, "precondition")));
        v_refs__269103 = F_dvp_merge_refs(v_refs__269103, F_dvp_owner_requirement_refs(v_action));
        if (R.Bool(F_dv_has_key(v_action, "answer")))
        {
            v_refs__269103 = F_dvp_merge_refs(v_refs__269103, F_dvs_sim_refs(F_dv_get(v_action, "answer")));
        }
        if (R.Bool(F_dv_has_key(v_actor, "question_ref")))
        {
            v_refs__269103 = F_dvp_add_ref(v_refs__269103, F_dv_get(v_actor, "question_ref"));
        }
        v_parents__269139 = F_dv_explanation_merge(v_parents__269139, F_dvs_support_parents(F_dv_get(v_actor, "mind"), v_refs__269103, v_events, v_actor));
        if (R.Bool(R.eq(F_dv_find(v_actor, "hypothesis_event", ""), "")))
        {
            _ = false;
        }
        else
        {
            v_parents__269139 = F_dv_explanation_merge(v_parents__269139, F_dv_single(F_dv_get(v_actor, "hypothesis_event")));
        }
        if (R.Bool(F_dv_has_key(v_action, "argument")))
        {
            v_parents__269139 = F_dv_explanation_merge(v_parents__269139, F_dv_single(F_dv_get(F_dv_get(v_action, "argument"), "id")));
        }
        return v_parents__269139;
    }

    public static object F_dvs_model_parent_ids(object v_actor, object v_parent, object v_prior)
    {
        object v_result__269982 = F_dvs_cognitive_parents(v_actor, F_dv_single(v_parent));
        if (R.Bool(R.eq(F_dv_find(v_actor, "model_paid_event", ""), "")))
        {
            _ = false;
        }
        else
        {
            v_result__269982 = F_dv_explanation_merge(v_result__269982, F_dv_single(F_dv_get(v_actor, "model_paid_event")));
        }
        if (R.Bool(R.eq(F_dv_find(v_actor, "model_previous_event", ""), "")))
        {
            _ = false;
        }
        else
        {
            v_result__269982 = F_dv_explanation_merge(v_result__269982, F_dv_single(F_dv_get(v_actor, "model_previous_event")));
        }
        if (R.Bool(R.eq(v_prior, "")))
        {
            _ = false;
        }
        else
        {
            v_result__269982 = F_dv_explanation_merge(v_result__269982, F_dv_single(v_prior));
        }
        return v_result__269982;
    }

    public static object F_dvs_public_grant_parents(object v_events, object v_parent, object v_subject)
    {
        object v_result__270558 = F_dv_single(v_parent);
        object v_literal__270593 = "";
        object v_i__270614 = R.sub(R.count(v_events), R.number("1"));
        while (R.Bool((R.Bool(R.not(R.lt(v_i__270614, R.number("0")))) ? (object)(R.eq(v_literal__270593, "")) : (object)(false))))
        {
            object v_event__270689 = R.at(v_events, v_i__270614);
            if (R.Bool(R.eq(F_dv_get(v_event__270689, "type"), "speech_literal")))
            {
                if (R.Bool(F_dv_has(F_dv_get(v_event__270689, "parents"), v_parent)))
                {
                    v_literal__270593 = F_dv_get(v_event__270689, "id");
                }
            }
            v_i__270614 = R.sub(v_i__270614, R.number("1"));
        }
        if (R.Bool(R.eq(v_literal__270593, "")))
        {
            _ = false;
        }
        else
        {
            v_result__270558 = F_dv_explanation_merge(v_result__270558, F_dv_single(v_literal__270593));
            v_i__270614 = R.sub(R.count(v_events), R.number("1"));
            object v_found_parse__271010 = false;
            while (R.Bool((R.Bool(R.not(R.lt(v_i__270614, R.number("0")))) ? (object)(R.not(v_found_parse__271010)) : (object)(false))))
            {
                object v_event__271090 = R.at(v_events, v_i__270614);
                if (R.Bool(F_dv_and(R.eq(F_dv_get(v_event__271090, "type"), "listener_parse"), R.eq(F_dv_get(v_event__271090, "actor"), v_subject))))
                {
                    if (R.Bool(R.eq(F_dv_find(F_dv_get(v_event__271090, "data"), "source_event", ""), v_literal__270593)))
                    {
                        v_result__270558 = F_dv_explanation_merge(v_result__270558, F_dv_single(F_dv_get(v_event__271090, "id")));
                        v_found_parse__271010 = true;
                    }
                }
                v_i__270614 = R.sub(v_i__270614, R.number("1"));
            }
        }
        return v_result__270558;
    }
}
