using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Cognition / Awareness: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_visible_context(object v_actor, object v_physical, object v_actors)
    {
        v_actor = R.snapshot(v_actor);
        v_physical = R.snapshot(v_physical);
        v_actors = R.snapshot(v_actors);
        object v_result__76865 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.not(R.eq(F_dv_truth(v_physical, F_dv_find(F_dv_get(v_actor, "policy"), "visual_identity_when", F_dv_true())), "true"))))
        {
            return v_result__76865;
        }
        object v_own__77002 = F_dv_read(v_physical, F_dv_get(v_actor, "location_ref"));
        object v_i__77054 = R.number("0");
        while (R.Bool(R.lt(v_i__77054, R.count(v_actors))))
        {
            object v_other__77106 = R.at(v_actors, v_i__77054);
            object v_position__77136 = F_dv_read(v_physical, F_dv_get(R.at(v_actors, v_i__77054), "location_ref"));
            if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.not(F_dv_cognition_hidden(F_dv_get(v_actor, "mind"), F_dv_get(v_other__77106, "id")))) ? (object)(R.not(R.eq(F_dv_get(v_other__77106, "id"), F_dv_get(v_actor, "id")))) : (object)(false))) ? (object)(F_dv_uncontested(v_own__77002)) : (object)(false))) ? (object)(F_dv_uncontested(v_position__77136)) : (object)(false))) ? (object)(F_dv_same(F_dv_get(v_own__77002, "value"), F_dv_get(v_position__77136, "value"))) : (object)(false))))
            {
                if (R.Bool(R.eq(F_dv_truth(v_physical, F_dv_find(v_other__77106, "observable_presence_when", F_dv_true())), "true")))
                {
                    v_result__76865 = F_dv_with(v_result__76865, F_dv_get(v_other__77106, "id"), new System.Collections.Generic.List<object>(1) { F_dv_pair("location", F_dv_get(v_position__77136, "value")) });
                }
            }
            v_i__77054 = R.add(v_i__77054, R.number("1"));
        }
        return v_result__76865;
    }

    public static object F_dvs_absent_peer_locations(object v_actor, object v_actors, object v_bindings, object v_context, object v_location, object v_clear)
    {
        object v_result__77905 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.not(v_clear)))
        {
            return v_result__77905;
        }
        object v_observed__77965 = F_dv_project_value(v_location, v_bindings, F_dv_get(v_actor, "id"));
        if (R.Bool(R.not(F_dv_get(v_observed__77965, "known"))))
        {
            return v_result__77905;
        }
        object v_i__78080 = R.number("0");
        while (R.Bool(R.lt(v_i__78080, R.count(v_actors))))
        {
            object v_peer__78130 = R.at(v_actors, v_i__78080);
            if (R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_peer__78130, "id"), F_dv_get(v_actor, "id")))) ? (object)(R.eq(F_dv_find(v_context, F_dv_get(v_peer__78130, "id"), false), false)) : (object)(false))) ? (object)(F_dv_same(F_dv_find(v_peer__78130, "observable_presence_when", F_dv_true()), F_dv_true())) : (object)(false))))
            {
                object v_projected__78318 = F_dv_project_value(F_dv_get(v_peer__78130, "location_ref"), v_bindings, F_dv_get(v_actor, "id"));
                if (R.Bool(F_dv_get(v_projected__78318, "known")))
                {
                    object v_before__78438 = F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_projected__78318, "value"));
                    if (R.Bool((R.Bool(F_dv_uncontested(v_before__78438)) ? (object)(F_dv_same(F_dv_get(v_before__78438, "value"), F_dv_get(v_observed__77965, "value"))) : (object)(false))))
                    {
                        _ = R.push(v_result__77905, new System.Collections.Generic.List<object>(2) { F_dv_pair("ref", F_dv_get(v_projected__78318, "value")), F_dv_pair("before", v_before__78438) });
                    }
                }
            }
            v_i__78080 = R.add(v_i__78080, R.number("1"));
        }
        return v_result__77905;
    }

    public static object F_dvs_notice_peers(object v_actor, object v_physical, object v_actors, object v_bindings, object v_events, object v_time, object v_parent)
    {
        if (R.Bool(F_dv_find(F_dv_get(v_actor, "policy"), "notice_co_presence", false)))
        {
            object v_context__78835 = F_dvs_visible_context(v_actor, v_physical, v_actors);
            object v_old__78902 = F_dv_find(v_actor, "visible_context", new System.Collections.Generic.List<object>(0) { });
            object v_position__78958 = F_dv_read(v_physical, F_dv_get(v_actor, "location_ref"));
            object v_location__79019 = false;
            if (R.Bool(F_dv_uncontested(v_position__78958)))
            {
                v_location__79019 = F_dv_get(v_position__78958, "value");
            }
            object v_clear__79123 = (R.Bool(R.not(R.eq(v_location__79019, false))) ? (object)(R.eq(F_dv_truth(v_physical, F_dv_find(F_dv_get(v_actor, "policy"), "visual_identity_when", F_dv_true())), "true")) : (object)(false));
            object v_absent__79252 = F_dvs_absent_peer_locations(v_actor, v_actors, v_bindings, v_context__78835, v_location__79019, v_clear__79123);
            if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(R.count(v_absent__79252), R.number("0"))) ? (object)(F_dv_same(v_context__78835, v_old__78902)) : (object)(false))) ? (object)(R.eq(F_dv_find(v_actor, "visible_context_location", false), v_location__79019)) : (object)(false))) ? (object)(R.eq(F_dv_find(v_actor, "visible_context_clear", false), v_clear__79123)) : (object)(false))))
            {
                return v_actor;
            }
            else
            {
                object v_event__79579 = F_dvs_event(v_events, "co_presence", F_dv_get(v_actor, "id"), v_time, F_dvs_cognitive_parents(v_actor, F_dv_single(v_parent)), new System.Collections.Generic.List<object>(5) { F_dv_pair("context_before", v_old__78902), F_dv_pair("context_after", v_context__78835), F_dv_pair("location_ref", F_dv_get(v_actor, "location_ref")), F_dv_pair("observed_location", v_location__79019), F_dv_pair("visibility_available", v_clear__79123) });
                object v_grants__79850 = new System.Collections.Generic.List<object>(0) { };
                object v_i__79878 = R.number("0");
                while (R.Bool(R.lt(v_i__79878, R.count(v_actors))))
                {
                    object v_peer__79944 = R.at(v_actors, v_i__79878);
                    object v_now__79981 = F_dv_find(v_context__78835, F_dv_get(v_peer__79944, "id"), false);
                    object v_previous__80040 = F_dv_find(v_old__78902, F_dv_get(v_peer__79944, "id"), false);
                    if (R.Bool((R.Bool(R.not(R.eq(v_now__79981, false))) ? (object)((R.Bool(R.eq(v_previous__80040, false)) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_previous__80040, "location"), F_dv_get(v_now__79981, "location")))))) : (object)(false))))
                    {
                        _ = R.push(v_grants__79850, F_dv_get(v_peer__79944, "location_ref"));
                    }
                    v_i__79878 = R.add(v_i__79878, R.number("1"));
                }
                object v_mind__80271 = F_dv_get(v_actor, "mind");
                if (R.Bool(R.lt(R.number("0"), R.count(v_grants__79850))))
                {
                    object v_observations__80346 = F_dv_perceive(v_physical, F_dv_get(v_actor, "mind"), v_bindings, v_grants__79850, v_event__79579, v_time);
                    if (R.Bool(R.lt(R.number("0"), R.count(v_observations__80346))))
                    {
                        object v_next__80497 = F_dv_get(F_dv_update_mind(v_mind__80271, v_observations__80346, F_dv_get(v_mind__80271, "revision"), F_dvs_observation_profile(v_actor)), "world");
                        v_event__79579 = F_dvs_event(v_events, "observation", F_dv_get(v_actor, "id"), v_time, F_dv_single(v_event__79579), new System.Collections.Generic.List<object>(6) { F_dv_pair("observations", v_observations__80346), F_dv_pair("revision_before", F_dv_get(v_mind__80271, "revision")), F_dv_pair("revision_after", F_dv_get(v_next__80497, "revision")), F_dv_pair("bindings", v_bindings), F_dv_pair("grants", v_grants__79850), F_dv_pair("execution_id", v_event__79579) });
                        v_mind__80271 = v_next__80497;
                    }
                }
                if (R.Bool(R.lt(R.number("0"), R.count(v_absent__79252))))
                {
                    object v_revision__80947 = F_dv_get(v_mind__80271, "revision");
                    v_event__79579 = F_dvs_event(v_events, "presence_memory_retracted", F_dv_get(v_actor, "id"), v_time, F_dvs_cognitive_parents(v_actor, new System.Collections.Generic.List<object>(1) { v_event__79579 }), new System.Collections.Generic.List<object>(5) { F_dv_pair("retracted", v_absent__79252), F_dv_pair("observed_location", v_location__79019), F_dv_pair("revision_before", v_revision__80947), F_dv_pair("revision_after", R.add(v_revision__80947, R.number("1"))), F_dv_pair("scope", "clear-room-absence-new-location-unknown") });
                    object v_j__81285 = R.number("0");
                    while (R.Bool(R.lt(v_j__81285, R.count(v_absent__79252))))
                    {
                        object v_unknown__81359 = F_dv_with_many(F_dv_unknown(), new System.Collections.Generic.List<object>(2) { F_dv_pair("time", v_time), F_dv_pair("basis", new System.Collections.Generic.List<object>(1) { v_event__79579 }) });
                        v_mind__80271 = F_dv_store(v_mind__80271, F_dv_get(R.at(v_absent__79252, v_j__81285), "ref"), v_unknown__81359);
                        v_j__81285 = R.add(v_j__81285, R.number("1"));
                    }
                    v_mind__80271 = F_dv_with_many(v_mind__80271, new System.Collections.Generic.List<object>(2) { F_dv_pair("revision", R.add(v_revision__80947, R.number("1"))), F_dv_pair("time", F_dv_max(F_dv_find(v_mind__80271, "time", R.number("0")), v_time)) });
                }
                return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(6) { F_dv_pair("visible_context", v_context__78835), F_dv_pair("visible_context_location", v_location__79019), F_dv_pair("visible_context_clear", v_clear__79123), F_dv_pair("visible_context_event", v_event__79579), F_dv_pair("mind", v_mind__80271), F_dv_pair("last_cognitive_event", v_event__79579) });
            }
        }
        else
        {
            return v_actor;
        }
    }

    public static object F_dvs_visible_object_catalog(object v_objects)
    {
        v_objects = R.snapshot(v_objects);
        object v_key__82204 = new System.Collections.Generic.List<object>(1) { "visible-object-catalog-v1" };
        object v_cached__82248 = F_dvs_declaration_cache_get(v_objects, v_key__82204);
        if (R.Bool(R.at(v_cached__82248, R.number("0"))))
        {
            return R.at(v_cached__82248, R.number("1"));
        }
        object v_rooms__82348 = new System.Collections.Generic.List<object>(0) { };
        object v_i__82367 = R.number("0");
        while (R.Bool(R.lt(v_i__82367, R.count(v_objects))))
        {
            object v_object__82418 = R.at(R.at(v_objects, v_i__82367), R.number("1"));
            object v_fields__82453 = F_dv_find(v_object__82418, "visible_fields", new System.Collections.Generic.List<object>(0) { });
            if (R.Bool(R.lt(R.number("0"), R.count(v_fields__82453))))
            {
                object v_state__82549 = F_dv_find(F_dv_get(v_object__82418, "facets"), "state", new System.Collections.Generic.List<object>(0) { });
                object v_location__82609 = F_dv_find(v_state__82549, "location", F_dv_unknown());
                if (R.Bool((R.Bool(F_dv_uncontested(v_location__82609)) ? (object)(R.is_text(F_dv_get(v_location__82609, "value"))) : (object)(false))))
                {
                    object v_visible__82753 = new System.Collections.Generic.List<object>(0) { };
                    object v_j__82786 = R.number("0");
                    while (R.Bool(R.lt(v_j__82786, R.count(v_fields__82453))))
                    {
                        object v_cell__82860 = F_dv_find(v_state__82549, R.at(v_fields__82453, v_j__82786), F_dv_unknown());
                        if (R.Bool(F_dv_uncontested(v_cell__82860)))
                        {
                            _ = R.push(v_visible__82753, F_dv_pair(R.at(v_fields__82453, v_j__82786), v_cell__82860));
                        }
                        v_j__82786 = R.add(v_j__82786, R.number("1"));
                    }
                    object v_room__83069 = F_dv_find(v_rooms__82348, F_dv_get(v_location__82609, "value"), new System.Collections.Generic.List<object>(0) { });
                    v_rooms__82348 = F_dv_with(v_rooms__82348, F_dv_get(v_location__82609, "value"), F_dv_with(v_room__83069, F_dv_get(v_object__82418, "id"), v_visible__82753));
                }
            }
            v_i__82367 = R.add(v_i__82367, R.number("1"));
        }
        return F_dvs_declaration_cache_put(v_objects, v_key__82204, v_rooms__82348);
    }

    public static object F_dvs_visible_objects(object v_actor, object v_physical)
    {
        if (R.Bool(R.not(F_dv_find(F_dv_get(v_actor, "policy"), "notice_visible_objects", false))))
        {
            return new System.Collections.Generic.List<object>(0) { };
        }
        if (R.Bool(R.not(R.eq(F_dv_truth(v_physical, F_dv_find(F_dv_get(v_actor, "policy"), "visual_identity_when", F_dv_true())), "true"))))
        {
            return new System.Collections.Generic.List<object>(0) { };
        }
        object v_own__83525 = F_dv_read(v_physical, F_dv_get(v_actor, "location_ref"));
        if (R.Bool((R.Bool(R.not(F_dv_uncontested(v_own__83525))) ? (object)(true) : (object)(R.not(R.is_text(F_dv_get(v_own__83525, "value")))))))
        {
            return new System.Collections.Generic.List<object>(0) { };
        }
        object v_visible__83656 = F_dv_find(F_dvs_visible_object_catalog(F_dv_get(v_physical, "objects")), F_dv_get(v_own__83525, "value"), new System.Collections.Generic.List<object>(0) { });
        object v_filtered__83743 = new System.Collections.Generic.List<object>(0) { };
        object v_i__83765 = R.number("0");
        while (R.Bool(R.lt(v_i__83765, R.count(v_visible__83656))))
        {
            if (R.Bool(R.not(F_dv_cognition_hidden(F_dv_get(v_actor, "mind"), R.at(R.at(v_visible__83656, v_i__83765), R.number("0"))))))
            {
                _ = R.push(v_filtered__83743, R.at(v_visible__83656, v_i__83765));
            }
            v_i__83765 = R.add(v_i__83765, R.number("1"));
        }
        return v_filtered__83743;
    }

    public static object F_dvs_notice_visible_objects(object v_actor, object v_physical, object v_bindings, object v_events, object v_time, object v_parent)
    {
        if (R.Bool(R.not(F_dv_find(F_dv_get(v_actor, "policy"), "notice_visible_objects", false))))
        {
            return v_actor;
        }
        object v_context__84124 = F_dvs_visible_objects(v_actor, v_physical);
        object v_previous__84179 = F_dv_find(v_actor, "visible_objects", new System.Collections.Generic.List<object>(0) { });
        if (R.Bool(F_dv_same(v_context__84124, v_previous__84179)))
        {
            return v_actor;
        }
        object v_grants__84292 = new System.Collections.Generic.List<object>(0) { };
        object v_i__84312 = R.number("0");
        while (R.Bool(R.lt(v_i__84312, R.count(v_context__84124))))
        {
            object v_object__84363 = R.at(v_context__84124, v_i__84312);
            object v_old_fields__84395 = F_dv_find(v_previous__84179, R.at(v_object__84363, R.number("0")), new System.Collections.Generic.List<object>(0) { });
            object v_j__84453 = R.number("0");
            while (R.Bool(R.lt(v_j__84453, R.count(R.at(v_object__84363, R.number("1"))))))
            {
                object v_field__84514 = R.at(R.at(v_object__84363, R.number("1")), v_j__84453);
                if (R.Bool(R.not(F_dv_same(R.at(v_field__84514, R.number("1")), F_dv_find(v_old_fields__84395, R.at(v_field__84514, R.number("0")), false)))))
                {
                    _ = R.push(v_grants__84292, F_dv_ref(R.at(v_object__84363, R.number("0")), "state", R.at(v_field__84514, R.number("0"))));
                }
                v_j__84453 = R.add(v_j__84453, R.number("1"));
            }
            v_i__84312 = R.add(v_i__84312, R.number("1"));
        }
        object v_event__84729 = F_dvs_event(v_events, "room_perception", F_dv_get(v_actor, "id"), v_time, F_dvs_cognitive_parents(v_actor, new System.Collections.Generic.List<object>(1) { v_parent }), new System.Collections.Generic.List<object>(4) { F_dv_pair("visible_before", v_previous__84179), F_dv_pair("visible_after", v_context__84124), F_dv_pair("grants", v_grants__84292), F_dv_pair("scope", "actual-same-room-visible-fields") });
        object v_mind__84956 = F_dv_get(v_actor, "mind");
        if (R.Bool(R.lt(R.number("0"), R.count(v_grants__84292))))
        {
            object v_observations__85015 = F_dv_perceive(v_physical, v_mind__84956, v_bindings, v_grants__84292, v_event__84729, v_time);
            if (R.Bool(R.lt(R.number("0"), R.count(v_observations__85015))))
            {
                object v_next__85144 = F_dv_get(F_dv_update_mind(v_mind__84956, v_observations__85015, F_dv_get(v_mind__84956, "revision"), F_dvs_observation_profile(v_actor)), "world");
                v_event__84729 = F_dvs_event(v_events, "observation", F_dv_get(v_actor, "id"), v_time, new System.Collections.Generic.List<object>(1) { v_event__84729 }, new System.Collections.Generic.List<object>(6) { F_dv_pair("observations", v_observations__85015), F_dv_pair("revision_before", F_dv_get(v_mind__84956, "revision")), F_dv_pair("revision_after", F_dv_get(v_next__85144, "revision")), F_dv_pair("bindings", v_bindings), F_dv_pair("grants", v_grants__84292), F_dv_pair("execution_id", v_event__84729) });
                v_mind__84956 = v_next__85144;
            }
        }
        return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(3) { F_dv_pair("visible_objects", v_context__84124), F_dv_pair("mind", v_mind__84956), F_dv_pair("last_cognitive_event", v_event__84729) });
    }

    public static object F_dvs_notice_movement_witnesses(object v_actors, object v_moving_index, object v_before, object v_physical, object v_bindings, object v_events, object v_time, object v_parent)
    {
        object v_mover__85733 = R.at(v_actors, v_moving_index);
        if (R.Bool(R.not(F_dv_has_key(v_mover__85733, "location_ref"))))
        {
            return v_actors;
        }
        object v_origin__85838 = F_dv_read(v_before, F_dv_get(v_mover__85733, "location_ref"));
        object v_destination__85891 = F_dv_read(v_physical, F_dv_get(v_mover__85733, "location_ref"));
        if (R.Bool((R.Bool((R.Bool(R.not(F_dv_uncontested(v_origin__85838))) ? (object)(true) : (object)(R.not(F_dv_uncontested(v_destination__85891))))) ? (object)(true) : (object)(R.eq(F_dv_get(v_origin__85838, "value"), F_dv_get(v_destination__85891, "value"))))))
        {
            return v_actors;
        }
        object v_result__86080 = v_actors;
        object v_i__86104 = R.number("0");
        while (R.Bool(R.lt(v_i__86104, R.count(v_result__86080))))
        {
            if (R.Bool((R.Bool(R.not(R.eq(v_i__86104, v_moving_index))) ? (object)(F_dv_has_key(R.at(v_result__86080, v_i__86104), "location_ref")) : (object)(false))))
            {
                object v_position__86230 = F_dv_read(v_physical, F_dv_get(R.at(v_result__86080, v_i__86104), "location_ref"));
                if (R.Bool((R.Bool(F_dv_uncontested(v_position__86230)) ? (object)((R.Bool(R.eq(F_dv_get(v_position__86230, "value"), F_dv_get(v_origin__85838, "value"))) ? (object)(true) : (object)(R.eq(F_dv_get(v_position__86230, "value"), F_dv_get(v_destination__85891, "value"))))) : (object)(false))))
                {
                    v_result__86080 = F_dvs_replace(v_result__86080, v_i__86104, F_dvs_notice_peers(R.at(v_result__86080, v_i__86104), v_physical, v_result__86080, v_bindings, v_events, v_time, v_parent));
                }
            }
            v_i__86104 = R.add(v_i__86104, R.number("1"));
        }
        return v_result__86080;
    }

    public static object F_dvs_visible_action_grants(object v_actors, object v_speaker_index, object v_action, object v_before, object v_physical, object v_bindings, object v_events, object v_time, object v_parent, object v_execution_id)
    {
        object v_listeners__86697 = F_dv_find(v_action, "public_observers", new System.Collections.Generic.List<object>(0) { });
        object v_grants__86757 = F_dv_find(v_action, "visual_grants", F_dv_find(v_action, "public_grants", new System.Collections.Generic.List<object>(0) { }));
        if (R.Bool((R.Bool(R.eq(R.count(v_listeners__86697), R.number("0"))) ? (object)(true) : (object)(R.eq(R.count(v_grants__86757), R.number("0"))))))
        {
            return v_actors;
        }
        object v_speaker__86925 = R.at(v_actors, v_speaker_index);
        object v_result__86965 = v_actors;
        object v_i__86989 = R.number("0");
        object v_own__87003 = F_dv_read(v_physical, F_dv_get(R.at(v_actors, v_speaker_index), "location_ref"));
        object v_origin__87071 = F_dv_read(v_before, F_dv_get(R.at(v_actors, v_speaker_index), "location_ref"));
        while (R.Bool(R.lt(v_i__86989, R.count(v_actors))))
        {
            object v_actor__87178 = R.at(v_result__86965, v_i__86989);
            if (R.Bool((R.Bool(R.not(R.eq(v_i__86989, v_speaker_index))) ? (object)(F_dv_has(v_listeners__86697, F_dv_get(v_actor__87178, "id"))) : (object)(false))))
            {
                object v_position__87277 = F_dv_read(v_physical, F_dv_get(v_actor__87178, "location_ref"));
                object v_position_before__87342 = F_dv_read(v_before, F_dv_get(v_actor__87178, "location_ref"));
                object v_present_after__87412 = F_dv_and(F_dv_uncontested(v_own__87003), F_dv_and(F_dv_uncontested(v_position__87277), F_dv_same(F_dv_get(v_own__87003, "value"), F_dv_get(v_position__87277, "value"))));
                object v_present_at_departure__87542 = (R.Bool(R.eq(F_dv_get(v_action, "kind"), "move")) ? (object)(F_dv_and(F_dv_uncontested(v_origin__87071), F_dv_and(F_dv_uncontested(v_position_before__87342), F_dv_same(F_dv_get(v_origin__87071, "value"), F_dv_get(v_position_before__87342, "value"))))) : (object)(false));
                if (R.Bool((R.Bool(F_dv_or(v_present_after__87412, v_present_at_departure__87542)) ? (object)(R.eq(F_dv_truth(v_physical, F_dv_find(F_dv_get(v_actor__87178, "policy"), "visual_identity_when", F_dv_true())), "true")) : (object)(false))))
                {
                    object v_mind__87881 = F_dv_get(v_actor__87178, "mind");
                    object v_observations__87919 = F_dv_perceive(v_physical, F_dv_get(v_actor__87178, "mind"), v_bindings, v_grants__86757, v_execution_id, v_time);
                    if (R.Bool(R.lt(R.number("0"), R.count(v_observations__87919))))
                    {
                        object v_next__88079 = F_dv_get(F_dv_update_mind(v_mind__87881, v_observations__87919, F_dv_get(v_mind__87881, "revision"), F_dvs_observation_profile(v_actor__87178)), "world");
                        object v_event__88198 = F_dvs_event(v_events, "observation", F_dv_get(v_actor__87178, "id"), v_time, F_dvs_cognitive_parents(v_actor__87178, F_dv_single(v_parent)), F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("observations", v_observations__87919), F_dv_pair("revision_before", F_dv_get(v_mind__87881, "revision")), F_dv_pair("revision_after", F_dv_get(v_next__88079, "revision")), F_dv_pair("action", v_action) }, new System.Collections.Generic.List<object>(3) { F_dv_pair("bindings", v_bindings), F_dv_pair("grants", v_grants__86757), F_dv_pair("execution_id", v_execution_id) }));
                        v_result__86965 = F_dvs_replace(v_result__86965, v_i__86989, F_dv_with_many(v_actor__87178, new System.Collections.Generic.List<object>(2) { F_dv_pair("mind", v_next__88079), F_dv_pair("last_cognitive_event", v_event__88198) }));
                    }
                }
            }
            v_i__86989 = R.add(v_i__86989, R.number("1"));
        }
        return v_result__86965;
    }

    public static object F_dvs_receipt_clock_observations(object v_story, object v_mind, object v_bindings, object v_observations, object v_action_event, object v_execution_event, object v_time)
    {
        object v_clock__347917 = F_dv_find(v_story, "clock_ref", false);
        if (R.Bool(R.eq(v_clock__347917, false)))
        {
            return v_observations;
        }
        object v_local__348019 = F_dv_local(v_bindings, F_dv_get(v_clock__347917, "object"), F_dv_get(v_mind, "subject"));
        if (R.Bool(R.eq(v_local__348019, "")))
        {
            return v_observations;
        }
        object v_ref__348130 = F_dv_scoped_ref(F_dv_get(v_mind, "subject"), v_local__348019, F_dv_get(v_clock__347917, "facet"), F_dv_get(v_clock__347917, "field"));
        object v_prior__348205 = F_dv_read(v_mind, v_ref__348130);
        if (R.Bool((R.Bool(R.not(F_dv_get(v_prior__348205, "known"))) ? (object)(true) : (object)(R.not(R.is_numeric_value(F_dv_get(v_prior__348205, "value")))))))
        {
            return v_observations;
        }
        object v_receipt__348332 = F_dv_observation(F_dv_list3(v_action_event, F_dv_get(v_mind, "subject"), v_ref__348130), F_dv_get(v_mind, "subject"), v_ref__348130, v_time, "perception", "execution-receipt", v_time, R.number("1"), F_dv_pair(v_execution_event, v_ref__348130));
        object v_result__348508 = new System.Collections.Generic.List<object>(0) { };
        object v_replaced__348528 = false;
        object v_i__348553 = R.number("0");
        while (R.Bool(R.lt(v_i__348553, R.count(v_observations))))
        {
            if (R.Bool(F_dv_same_local_ref(F_dv_get(v_mind, "subject"), F_dv_get(R.at(v_observations, v_i__348553), "ref"), v_ref__348130)))
            {
                if (R.Bool(R.not(v_replaced__348528)))
                {
                    _ = R.push(v_result__348508, v_receipt__348332);
                    v_replaced__348528 = true;
                }
            }
            else
            {
                _ = R.push(v_result__348508, R.at(v_observations, v_i__348553));
            }
            v_i__348553 = R.add(v_i__348553, R.number("1"));
        }
        if (R.Bool(R.not(v_replaced__348528)))
        {
            _ = R.push(v_result__348508, v_receipt__348332);
        }
        return v_result__348508;
    }

    public static object F_dvs_public_grants(object v_actors, object v_speaker_index, object v_action, object v_physical, object v_bindings, object v_events, object v_time, object v_parent, object v_execution_id)
    {
        object v_result__349299 = v_actors;
        object v_speaker__349323 = R.at(v_actors, v_speaker_index);
        object v_listeners__349363 = F_dv_find(v_action, "listeners", new System.Collections.Generic.List<object>(0) { });
        object v_grants__349416 = F_dv_find(v_action, "public_grants", new System.Collections.Generic.List<object>(0) { });
        if (R.Bool((R.Bool(R.eq(R.count(v_listeners__349363), R.number("0"))) ? (object)(true) : (object)(R.eq(R.count(v_grants__349416), R.number("0"))))))
        {
            return v_actors;
        }
        object v_projection__349550 = F_dvs_public_projection_inputs(v_events, v_execution_id);
        _ = F_dv_require(R.not(F_dv_get(v_projection__349550, "repeated")), "Repeated public grant projection");
        _ = F_dvs_event(v_events, "public_grants_projection", F_dv_get(v_speaker__349323, "id"), v_time, F_dv_single(v_parent), new System.Collections.Generic.List<object>(3) { F_dv_pair("execution_id", v_execution_id), F_dv_pair("action_id", F_dv_get(v_action, "id")), F_dv_pair("already_projected", F_dv_get(v_projection__349550, "sources")) });
        object v_i__349882 = R.number("0");
        while (R.Bool(R.lt(v_i__349882, R.count(v_actors))))
        {
            object v_actor__349934 = R.at(v_result__349299, v_i__349882);
            object v_mind__349964 = F_dv_get(R.at(v_result__349299, v_i__349882), "mind");
            if (R.Bool(F_dv_and(F_dv_has(v_listeners__349363, F_dv_get(v_actor__349934, "id")), F_dvs_speech_hearing(v_speaker__349323, v_actor__349934, F_dv_get(v_action, "speech"), v_physical))))
            {
                object v_grants__350111 = F_dv_find(v_action, "public_grants", new System.Collections.Generic.List<object>(0) { });
                object v_projected__350173 = F_dv_perceive(v_physical, v_mind__349964, v_bindings, F_dv_find(v_action, "public_grants", new System.Collections.Generic.List<object>(0) { }), v_execution_id, v_time);
                object v_observations__350297 = F_dvs_public_filter_projected(v_projected__350173, F_dv_find(F_dv_get(v_projection__349550, "by_subject"), F_dv_get(v_actor__349934, "id"), new System.Collections.Generic.List<object>(0) { }));
                if (R.Bool(R.lt(R.number("0"), R.count(v_observations__350297))))
                {
                    object v_actual_grants__350462 = new System.Collections.Generic.List<object>(0) { };
                    object v_g__350501 = R.number("0");
                    while (R.Bool(R.lt(v_g__350501, R.count(v_grants__350111))))
                    {
                        object v_local_ref__350575 = F_dv_scoped_ref(F_dv_get(v_actor__349934, "id"), F_dv_local(v_bindings, F_dv_get(R.at(v_grants__350111, v_g__350501), "object"), F_dv_get(v_actor__349934, "id")), F_dv_get(R.at(v_grants__350111, v_g__350501), "facet"), F_dv_get(R.at(v_grants__350111, v_g__350501), "field"));
                        object v_o__350717 = R.number("0");
                        object v_included__350747 = false;
                        while (R.Bool(R.lt(v_o__350717, R.count(v_observations__350297))))
                        {
                            if (R.Bool(F_dv_same(v_local_ref__350575, F_dv_get(R.at(v_observations__350297, v_o__350717), "ref"))))
                            {
                                v_included__350747 = true;
                            }
                            v_o__350717 = R.add(v_o__350717, R.number("1"));
                        }
                        if (R.Bool(v_included__350747))
                        {
                            _ = R.push(v_actual_grants__350462, R.at(v_grants__350111, v_g__350501));
                        }
                        v_g__350501 = R.add(v_g__350501, R.number("1"));
                    }
                    object v_next__351105 = F_dv_get(F_dv_update_mind(v_mind__349964, v_observations__350297, F_dv_get(v_mind__349964, "revision"), F_dvs_observation_profile(v_actor__349934)), "world");
                    object v_observation_event__351220 = "";
                    v_observation_event__351220 = F_dvs_event(v_events, "observation", F_dv_get(v_actor__349934, "id"), v_time, F_dvs_cognitive_parents(v_actor__349934, F_dvs_public_grant_parents(v_events, v_parent, F_dv_get(v_actor__349934, "id"))), F_dv_with_many(new System.Collections.Generic.List<object>(6) { F_dv_pair("observations", v_observations__350297), F_dv_pair("revision_before", F_dv_get(v_mind__349964, "revision")), F_dv_pair("revision_after", F_dv_get(v_next__351105, "revision")), F_dv_pair("mind_before", v_mind__349964), F_dv_pair("mind_after", v_next__351105), F_dv_pair("action", v_action) }, new System.Collections.Generic.List<object>(5) { F_dv_pair("bindings", v_bindings), F_dv_pair("grants", v_actual_grants__350462), F_dv_pair("declared_grants", v_grants__350111), F_dv_pair("execution_id", v_execution_id), F_dv_pair("source_event", F_dvs_public_literal_source(v_events, v_parent, F_dv_get(v_actor__349934, "id"))) }));
                    v_result__349299 = F_dvs_replace(v_result__349299, v_i__349882, F_dv_with_many(v_actor__349934, new System.Collections.Generic.List<object>(2) { F_dv_pair("mind", v_next__351105), F_dv_pair("last_cognitive_event", v_observation_event__351220) }));
                }
            }
            v_i__349882 = R.add(v_i__349882, R.number("1"));
        }
        return v_result__349299;
    }

    public static object F_dvs_public_projection_inputs(object v_events, object v_execution_id)
    {
        v_events = R.snapshot(v_events);
        object v_by_subject__352263 = new System.Collections.Generic.List<object>(0) { };
        object v_sources__352287 = new System.Collections.Generic.List<object>(0) { };
        object v_repeated__352308 = false;
        object v_i__352333 = R.sub(R.count(v_events), R.number("1"));
        object v_scanning__352366 = true;
        while (R.Bool((R.Bool(R.not(R.lt(v_i__352333, R.number("0")))) ? (object)(v_scanning__352366) : (object)(false))))
        {
            object v_event__352425 = R.at(v_events, v_i__352333);
            if (R.Bool(R.eq(F_dv_get(v_event__352425, "type"), "public_grants_projection")))
            {
                if (R.Bool(F_dv_same(F_dv_get(F_dv_get(v_event__352425, "data"), "execution_id"), v_execution_id)))
                {
                    v_repeated__352308 = true;
                }
            }
            if (R.Bool((R.Bool(R.eq(F_dv_get(v_event__352425, "type"), "observation")) ? (object)(F_dv_same(F_dv_find(F_dv_get(v_event__352425, "data"), "execution_id", false), v_execution_id)) : (object)(false))))
            {
                object v_observations__352717 = F_dv_find(F_dv_get(v_event__352425, "data"), "observations", new System.Collections.Generic.List<object>(0) { });
                object v_j__352788 = R.number("0");
                object v_existing__352810 = F_dv_find(v_by_subject__352263, F_dv_get(v_event__352425, "actor"), new System.Collections.Generic.List<object>(0) { });
                while (R.Bool(R.lt(v_j__352788, R.count(v_observations__352717))))
                {
                    _ = R.push(v_existing__352810, F_dv_get(R.at(v_observations__352717, v_j__352788), "id"));
                    _ = R.push(v_sources__352287, new System.Collections.Generic.List<object>(4) { F_dv_pair("event", F_dv_get(v_event__352425, "id")), F_dv_pair("subject", F_dv_get(v_event__352425, "actor")), F_dv_pair("ref", F_dv_get(R.at(v_observations__352717, v_j__352788), "ref")), F_dv_pair("observation_id", F_dv_get(R.at(v_observations__352717, v_j__352788), "id")) });
                    v_j__352788 = R.add(v_j__352788, R.number("1"));
                }
                v_by_subject__352263 = F_dv_with(v_by_subject__352263, F_dv_get(v_event__352425, "actor"), v_existing__352810);
            }
            if (R.Bool(R.eq(F_dv_get(v_event__352425, "type"), "action_execution")))
            {
                v_scanning__352366 = false;
            }
            v_i__352333 = R.sub(v_i__352333, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("by_subject", v_by_subject__352263), F_dv_pair("sources", v_sources__352287), F_dv_pair("repeated", v_repeated__352308) };
    }

    public static object F_dvs_public_filter_projected(object v_projected, object v_existing)
    {
        object v_result__353430 = new System.Collections.Generic.List<object>(0) { };
        object v_i__353450 = R.number("0");
        while (R.Bool(R.lt(v_i__353450, R.count(v_projected))))
        {
            if (R.Bool(R.not(F_dv_has(v_existing, F_dv_get(R.at(v_projected, v_i__353450), "id")))))
            {
                _ = R.push(v_result__353430, R.at(v_projected, v_i__353450));
            }
            v_i__353450 = R.add(v_i__353450, R.number("1"));
        }
        return v_result__353430;
    }

    public static object F_dvs_observation_source(object v_observations, object v_action)
    {
        object v_result__353677 = new System.Collections.Generic.List<object>(0) { };
        object v_i__353697 = R.number("0");
        object v_source__353711 = F_dv_find(v_action, "observation_source", "perception");
        object v_origin__353780 = F_dv_find(v_action, "observation_origin", "direct");
        object v_trust__353845 = F_dv_find(v_action, "observation_trust", R.number("1"));
        _ = F_dv_require(F_dv_has(F_dv_list3("perception", "testimony", "recall"), v_source__353711), "Unknown observation source");
        _ = F_dv_unit(v_trust__353845);
        while (R.Bool(R.lt(v_i__353697, R.count(v_observations))))
        {
            object v_observation__354072 = R.at(v_observations, v_i__353697);
            object v_mapped__354114 = F_dv_with_many(v_observation__354072, new System.Collections.Generic.List<object>(3) { F_dv_pair("source", v_source__353711), F_dv_pair("origin", v_origin__353780), F_dv_pair("trust", v_trust__353845) });
            object v_overrides__354215 = F_dv_find(v_action, "grant_observation_sources", new System.Collections.Generic.List<object>(0) { });
            object v_j__354288 = R.number("0");
            while (R.Bool(R.lt(v_j__354288, R.count(v_overrides__354215))))
            {
                object v_entry__354351 = R.at(v_overrides__354215, v_j__354288);
                if (R.Bool(F_dv_same_local_ref(F_dv_get(v_observation__354072, "subject"), F_dv_get(v_entry__354351, "ref"), F_dv_get(v_observation__354072, "ref"))))
                {
                    v_mapped__354114 = F_dv_with_many(v_mapped__354114, new System.Collections.Generic.List<object>(3) { F_dv_pair("source", F_dv_get(v_entry__354351, "source")), F_dv_pair("origin", F_dv_get(v_entry__354351, "origin")), F_dv_pair("trust", F_dv_get(v_entry__354351, "trust")) });
                }
                v_j__354288 = R.add(v_j__354288, R.number("1"));
            }
            _ = R.push(v_result__353677, v_mapped__354114);
            v_i__353697 = R.add(v_i__353697, R.number("1"));
        }
        return v_result__353677;
    }

    public static object F_dvs_observation_profile(object v_actor)
    {
        return F_dv_observation_profile(F_dv_find(F_dv_get(v_actor, "policy"), "observation_weights", new System.Collections.Generic.List<object>(3) { F_dv_pair("perception", R.number("1")), F_dv_pair("testimony", R.number("1")), F_dv_pair("recall", R.number("1")) }), F_dv_find(F_dv_get(v_actor, "policy"), "observation_threshold", R.number("0.5")));
    }
}
