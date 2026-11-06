// @mechanism: perception_and_erosion
// @layer: core/cognition
// @contracts: Awareness

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

// --- Source: Awareness.cs ---
namespace DuelView.Core
{


/// <summary>Cognition / Awareness: native Core mechanisms.</summary>
public static partial class DVCognitionKernel
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
}

// --- Source: Appraisal.cs ---
namespace DuelView.Core
{


/// <summary>Cognition / Appraisal: native Core mechanisms.</summary>
public static partial class DVCognitionKernel
{
    public static object F_dv_criterion(object v_id, object v_ref, object v_mode, object v_target, object v_weight)
    {
        _ = F_dv_require(F_dv_or(R.eq(v_mode, "numeric"), R.eq(v_mode, "matches")), "Unsupported value criterion");
        return F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("id", v_id), F_dv_pair("ref", v_ref), F_dv_pair("mode", v_mode), F_dv_pair("target", v_target) }, "weight", v_weight);
    }

    public static object F_dv_measure(object v_value, object v_criterion)
    {
        if (R.Bool(R.eq(F_dv_get(v_criterion, "mode"), "numeric")))
        {
            return R.mul(v_value, F_dv_get(v_criterion, "weight"));
        }
        else
        {
            if (R.Bool(F_dv_same(v_value, F_dv_get(v_criterion, "target"))))
            {
                return F_dv_get(v_criterion, "weight");
            }
            else
            {
                return R.number("0");
            }
        }
    }

    public static object F_dv_expected_measure(object v_cell, object v_criterion)
    {
        object v_alternatives__64917 = F_dv_find(v_cell, "alternatives", new System.Collections.Generic.List<object>(0) { });
        object v_sum__64974 = R.number("0");
        object v_i__64990 = R.number("0");
        if (R.Bool(R.eq(R.count(v_alternatives__64917), R.number("0"))))
        {
            return R.mul(F_dv_measure(F_dv_get(v_cell, "value"), v_criterion), F_dv_get(v_cell, "confidence"));
        }
        else
        {
            while (R.Bool(R.lt(v_i__64990, R.count(v_alternatives__64917))))
            {
                object v_alternative__65173 = R.at(v_alternatives__64917, v_i__64990);
                v_sum__64974 = R.add(v_sum__64974, R.mul(F_dv_measure(R.at(v_alternative__65173, R.number("0")), v_criterion), R.at(v_alternative__65173, R.number("1"))));
                v_i__64990 = R.add(v_i__64990, R.number("1"));
            }
            return R.mul(v_sum__64974, F_dv_get(v_cell, "confidence"));
        }
    }

    public static object F_dv_value(object v_world, object v_criteria, object v_subjective)
    {
        object v_i__65400 = R.number("0");
        object v_score__65414 = R.number("0");
        object v_details__65432 = new System.Collections.Generic.List<object>(0) { };
        object v_unknown__65453 = new System.Collections.Generic.List<object>(0) { };
        object v_ids__65474 = new System.Collections.Generic.List<object>(0) { };
        while (R.Bool(R.lt(v_i__65400, R.count(v_criteria))))
        {
            object v_criterion__65531 = R.at(v_criteria, v_i__65400);
            object v_id__65567 = F_dv_get(v_criterion__65531, "id");
            object v_cell__65597 = F_dv_read(v_world, F_dv_get(v_criterion__65531, "ref"));
            _ = F_dv_require(R.not(F_dv_has(v_ids__65474, v_id__65567)), "Duplicate value criterion");
            _ = R.push(v_ids__65474, v_id__65567);
            if (R.Bool(F_dv_get(v_cell__65597, "known")))
            {
                object v_local_1__65769 = false;
                if (R.Bool(v_subjective))
                {
                    v_local_1__65769 = F_dv_expected_measure(v_cell__65597, v_criterion__65531);
                }
                else
                {
                    v_local_1__65769 = F_dv_measure(F_dv_get(v_cell__65597, "value"), v_criterion__65531);
                }
                object v_amount__65969 = v_local_1__65769;
                v_score__65414 = R.add(v_score__65414, v_amount__65969);
                _ = R.push(v_details__65432, new System.Collections.Generic.List<object>(4) { F_dv_pair("criterion", v_id__65567), F_dv_pair("contribution", v_amount__65969), F_dv_pair("confidence", F_dv_get(v_cell__65597, "confidence")), F_dv_pair("conflicted", F_dv_get(v_cell__65597, "conflicted")) });
            }
            else
            {
                _ = R.push(v_unknown__65453, v_id__65567);
            }
            v_i__65400 = R.add(v_i__65400, R.number("1"));
        }
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("score", v_score__65414), F_dv_pair("details", v_details__65432), F_dv_pair("unknown", v_unknown__65453), F_dv_pair("complete", R.eq(R.count(v_unknown__65453), R.number("0"))) }, new System.Collections.Generic.List<object>(3) { F_dv_pair("world", F_dv_get(v_world, "id")), F_dv_pair("revision", F_dv_get(v_world, "revision")), F_dv_pair("subject", F_dv_get(v_world, "subject")) });
    }

    public static object F_dv_value_subjective(object v_mind, object v_criteria)
    {
        _ = F_dv_require(F_dv_or(R.eq(F_dv_get(v_mind, "domain"), "mind"), R.eq(F_dv_get(v_mind, "domain"), "prediction")), "Subjective value requires a mind or prediction");
        return F_dv_value(v_mind, v_criteria, true);
    }

    public static object F_dv_value_objective(object v_physical, object v_criteria)
    {
        _ = F_dv_domain(v_physical, "physical");
        return F_dv_value(v_physical, v_criteria, false);
    }

    public static object F_dv_value_change(object v_before, object v_after, object v_criteria)
    {
        _ = F_dv_require(F_dv_and(R.eq(F_dv_get(v_before, "id"), F_dv_get(v_after, "id")), R.eq(F_dv_get(v_before, "subject"), F_dv_get(v_after, "subject"))), "Value comparison crosses worlds");
        object v_subjective__66929 = R.not(R.eq(F_dv_get(v_before, "domain"), "physical"));
        object v_local_1__66986 = false;
        if (R.Bool(v_subjective__66929))
        {
            v_local_1__66986 = F_dv_and(R.eq(F_dv_get(v_before, "domain"), "mind"), F_dv_or(R.eq(F_dv_get(v_after, "domain"), "mind"), R.eq(F_dv_get(v_after, "domain"), "prediction")));
        }
        else
        {
            v_local_1__66986 = R.eq(F_dv_get(v_after, "domain"), "physical");
        }
        _ = F_dv_require(v_local_1__66986, "Value domain mismatch");
        object v_a__67252 = F_dv_value(v_before, v_criteria, v_subjective__66929);
        object v_b__67303 = F_dv_value(v_after, v_criteria, v_subjective__66929);
        return new System.Collections.Generic.List<object>(4) { F_dv_pair("delta", R.sub(F_dv_get(v_b__67303, "score"), F_dv_get(v_a__67252, "score"))), F_dv_pair("before", v_a__67252), F_dv_pair("after", v_b__67303), F_dv_pair("complete", F_dv_and(F_dv_get(v_a__67252, "complete"), F_dv_get(v_b__67303, "complete"))) };
    }

    public static object F_dv_request(object v_id, object v_requester, object v_self)
    {
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("id", v_id), F_dv_pair("requester", v_requester), F_dv_pair("self", v_self) };
    }

    public static object F_dv_feature_refs(object v_request)
    {
        object v_speaker__67774 = F_dv_get(v_request, "requester");
        object v_self__67810 = F_dv_get(v_request, "self");
        object v_id__67838 = F_dv_get(v_request, "id");
        object v_refs__67862 = new System.Collections.Generic.List<object>(0) { };
        _ = R.push(v_refs__67862, F_dv_pair("trust", F_dv_ref(v_speaker__67774, "relationship", "trust")));
        _ = R.push(v_refs__67862, F_dv_pair("closeness", F_dv_ref(v_speaker__67774, "relationship", "closeness")));
        _ = R.push(v_refs__67862, F_dv_pair("respect", F_dv_ref(v_speaker__67774, "relationship", "respect")));
        _ = R.push(v_refs__67862, F_dv_pair("reciprocity", F_dv_ref(v_speaker__67774, "relationship", "reciprocity")));
        _ = R.push(v_refs__67862, F_dv_pair("valence", F_dv_ref(v_self__67810, "affect", "valence")));
        _ = R.push(v_refs__67862, F_dv_pair("arousal", F_dv_ref(v_self__67810, "affect", "arousal")));
        _ = R.push(v_refs__67862, F_dv_pair("fatigue", F_dv_ref(v_self__67810, "resources", "fatigue")));
        _ = R.push(v_refs__67862, F_dv_pair("stress", F_dv_ref(v_self__67810, "resources", "stress")));
        _ = R.push(v_refs__67862, F_dv_pair("attention", F_dv_ref(v_self__67810, "resources", "attention")));
        _ = R.push(v_refs__67862, F_dv_pair("threat", F_dv_ref(v_id__67838, "appraisal", "threat")));
        _ = R.push(v_refs__67862, F_dv_pair("fairness", F_dv_ref(v_id__67838, "appraisal", "fairness")));
        _ = R.push(v_refs__67862, F_dv_pair("control", F_dv_ref(v_id__67838, "appraisal", "control")));
        _ = R.push(v_refs__67862, F_dv_pair("certainty", F_dv_ref(v_id__67838, "appraisal", "certainty")));
        _ = R.push(v_refs__67862, F_dv_pair("importance", F_dv_ref(v_id__67838, "appraisal", "importance")));
        return v_refs__67862;
    }

    public static object F_dv_appraise_request(object v_mind, object v_request)
    {
        _ = F_dv_domain(v_mind, "mind");
        object v_refs__69056 = F_dv_feature_refs(v_request);
        object v_features__69096 = new System.Collections.Generic.List<object>(0) { };
        object v_unknown__69118 = new System.Collections.Generic.List<object>(0) { };
        object v_basis__69139 = new System.Collections.Generic.List<object>(0) { };
        object v_i__69158 = R.number("0");
        while (R.Bool(R.lt(v_i__69158, R.count(v_refs__69056))))
        {
            object v_entry__69208 = R.at(v_refs__69056, v_i__69158);
            object v_key__69236 = R.at(v_entry__69208, R.number("0"));
            object v_cell__69263 = F_dv_read(v_mind, R.at(v_entry__69208, R.number("1")));
            if (R.Bool(F_dv_and(F_dv_get(v_cell__69263, "known"), R.not(F_dv_get(v_cell__69263, "conflicted")))))
            {
                _ = R.push(v_features__69096, F_dv_pair(v_key__69236, F_dv_get(v_cell__69263, "value")));
                _ = R.push(v_basis__69139, new System.Collections.Generic.List<object>(3) { F_dv_pair("feature", v_key__69236), F_dv_pair("ref", R.at(v_entry__69208, R.number("1"))), F_dv_pair("cell", v_cell__69263) });
            }
            else
            {
                _ = R.push(v_unknown__69118, v_key__69236);
            }
            v_i__69158 = R.add(v_i__69158, R.number("1"));
        }
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("features", v_features__69096), F_dv_pair("unknown", v_unknown__69118), F_dv_pair("basis", v_basis__69139), F_dv_pair("revision", F_dv_get(v_mind, "revision")) }, new System.Collections.Generic.List<object>(2) { F_dv_pair("subject", F_dv_get(v_mind, "subject")), F_dv_pair("request", F_dv_get(v_request, "id")) });
    }

    public static object F_dv_dot(object v_features, object v_weights)
    {
        object v_i__69748 = R.number("0");
        object v_score__69762 = R.number("0");
        object v_terms__69780 = new System.Collections.Generic.List<object>(0) { };
        object v_unknown__69799 = new System.Collections.Generic.List<object>(0) { };
        while (R.Bool(R.lt(v_i__69748, R.count(v_weights))))
        {
            object v_entry__69859 = R.at(v_weights, v_i__69748);
            object v_key__69890 = R.at(v_entry__69859, R.number("0"));
            if (R.Bool(F_dv_has_key(v_features, v_key__69890)))
            {
                object v_contribution__69959 = R.mul(F_dv_get(v_features, v_key__69890), R.at(v_entry__69859, R.number("1")));
                v_score__69762 = R.add(v_score__69762, v_contribution__69959);
                _ = R.push(v_terms__69780, F_dv_pair(v_key__69890, v_contribution__69959));
            }
            else
            {
                _ = R.push(v_unknown__69799, v_key__69890);
            }
            v_i__69748 = R.add(v_i__69748, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("score", v_score__69762), F_dv_pair("terms", v_terms__69780), F_dv_pair("unknown", v_unknown__69799) };
    }

    public static object F_dv_clamp_signed(object v_value)
    {
        if (R.Bool(R.lt(v_value, R.sub(R.number("0"), R.number("1")))))
        {
            return R.sub(R.number("0"), R.number("1"));
        }
        else
        {
            if (R.Bool(R.lt(R.number("1"), v_value)))
            {
                return R.number("1");
            }
            else
            {
                return v_value;
            }
        }
    }

    public static object F_dv_expression(object v_appraisal, object v_expression_policy)
    {
        object v_axes__70457 = new System.Collections.Generic.List<object>(0) { };
        object v_i__70475 = R.number("0");
        while (R.Bool(R.lt(v_i__70475, R.count(v_expression_policy))))
        {
            object v_axis__70538 = R.at(v_expression_policy, v_i__70475);
            object v_evaluation__70578 = F_dv_dot(F_dv_get(v_appraisal, "features"), R.at(v_axis__70538, R.number("1")));
            _ = R.push(v_axes__70457, F_dv_pair(R.at(v_axis__70538, R.number("0")), F_dv_with_many(v_evaluation__70578, new System.Collections.Generic.List<object>(2) { F_dv_pair("value", F_dv_clamp_signed(F_dv_get(v_evaluation__70578, "score"))), F_dv_pair("complete", R.eq(R.count(F_dv_get(v_evaluation__70578, "unknown")), R.number("0"))) })));
            v_i__70475 = R.add(v_i__70475, R.number("1"));
        }
        return v_axes__70457;
    }

    public static object F_dv_no_change_prediction(object v_mind)
    {
        _ = F_dv_domain(v_mind, "mind");
        return F_dv_with_many(v_mind, new System.Collections.Generic.List<object>(2) { F_dv_pair("domain", "prediction"), F_dv_pair("source_revision", F_dv_get(v_mind, "revision")) });
    }

    public static object F_dv_response_option(object v_id, object v_intent, object v_feasible, object v_prediction, object v_effort, object v_feature_weights)
    {
        _ = F_dv_require(R.not(R.lt(v_effort, R.number("0"))), "Negative effort");
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("id", v_id), F_dv_pair("intent", v_intent), F_dv_pair("feasible", v_feasible), F_dv_pair("prediction", v_prediction) }, new System.Collections.Generic.List<object>(2) { F_dv_pair("effort", v_effort), F_dv_pair("feature_weights", v_feature_weights) });
    }

    public static object F_dv_validate_expression_policy(object v_expression_policy)
    {
        _ = F_dv_require(F_dv_record_like(v_expression_policy), "Expression policy must be an axis-to-weight-record matrix");
        object v_i__71445 = R.number("0");
        while (R.Bool(R.lt(v_i__71445, R.count(v_expression_policy))))
        {
            object v_axis__71508 = R.at(v_expression_policy, v_i__71445);
            object v_key__71548 = R.at(R.at(v_expression_policy, v_i__71445), R.number("0"));
            object v_weights__71590 = R.at(R.at(v_expression_policy, v_i__71445), R.number("1"));
            object v_j__71636 = R.number("0");
            _ = F_dv_require(R.eq(R.slice(R.json_stringify(v_key__71548), R.number("0"), R.number("1")), "\""), "Expression policy axis name must be Text");
            _ = F_dv_require(F_dv_record_like(v_weights__71590), R.cat("Expression policy axis weights must be a record: ", v_key__71548));
            while (R.Bool(R.lt(v_j__71636, R.count(v_weights__71590))))
            {
                object v_weight__71920 = R.at(v_weights__71590, v_j__71636);
                object v_feature__71956 = R.at(R.at(v_weights__71590, v_j__71636), R.number("0"));
                _ = F_dv_require(R.eq(R.slice(R.json_stringify(v_feature__71956), R.number("0"), R.number("1")), "\""), "Expression policy feature name must be Text");
                _ = F_dv_require(R.is_numeric_value(R.at(v_weight__71920, R.number("1"))), R.cat(R.cat(R.cat("Expression policy weight must be a finite Number: ", v_key__71548), "."), v_feature__71956));
                v_j__71636 = R.add(v_j__71636, R.number("1"));
            }
            v_i__71445 = R.add(v_i__71445, R.number("1"));
        }
        return true;
    }

    public static object F_dv_person_policy(object v_subject, object v_values, object v_effort_cost, object v_uncertainty_cost, object v_expression_policy)
    {
        _ = F_dv_validate_expression_policy(v_expression_policy);
        _ = F_dv_require(F_dv_and(R.not(R.lt(v_effort_cost, R.number("0"))), R.not(R.lt(v_uncertainty_cost, R.number("0")))), "Negative policy cost");
        return F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("subject", v_subject), F_dv_pair("values", v_values), F_dv_pair("effort_cost", v_effort_cost), F_dv_pair("uncertainty_cost", v_uncertainty_cost) }, "expression", v_expression_policy);
    }

    public static object F_dv_accept_policy(object v_mind, object v_id, object v_policy, object v_basis)
    {
        _ = F_dv_domain(v_mind, "mind");
        _ = F_dv_require(R.eq(F_dv_get(v_mind, "subject"), F_dv_get(v_policy, "subject")), "Foreign value policy");
        _ = F_dv_require(R.lt(R.number("0"), R.count(v_basis)), "Value policy needs an acceptance basis");
        object v_policies__72954 = F_dv_find(v_mind, "policies", new System.Collections.Generic.List<object>(0) { });
        _ = F_dv_require(R.not(F_dv_has_key(v_policies__72954, v_id)), "Duplicate accepted policy");
        return F_dv_with_many(v_mind, new System.Collections.Generic.List<object>(2) { F_dv_pair("policies", F_dv_with(v_policies__72954, v_id, new System.Collections.Generic.List<object>(3) { F_dv_pair("policy", v_policy), F_dv_pair("basis", v_basis), F_dv_pair("active", true) })), F_dv_pair("revision", R.add(F_dv_get(v_mind, "revision"), R.number("1"))) });
    }

    public static object F_dv_respond_adopted(object v_mind, object v_request, object v_options, object v_policy_id)
    {
        object v_accepted__73290 = F_dv_get(F_dv_get(v_mind, "policies"), v_policy_id);
        _ = F_dv_require(F_dv_get(v_accepted__73290, "active"), "Response policy is not adopted");
        return F_dv_with(F_dv_respond(v_mind, v_request, v_options, F_dv_get(v_accepted__73290, "policy")), "policy_basis", F_dv_get(v_accepted__73290, "basis"));
    }

    public static object F_dv_response_score(object v_mind, object v_option, object v_appraisal, object v_policy)
    {
        object v_prediction__73568 = F_dv_get(v_option, "prediction");
        _ = F_dv_domain(v_prediction__73568, "prediction");
        _ = F_dv_require(R.eq(F_dv_get(v_prediction__73568, "source_revision"), F_dv_get(v_mind, "revision")), "Stale response prediction");
        object v_value__73738 = F_dv_value_change(v_mind, v_prediction__73568, F_dv_get(v_policy, "values"));
        object v_relation_and_state__73803 = F_dv_dot(F_dv_get(v_appraisal, "features"), F_dv_get(v_option, "feature_weights"));
        object v_effort__73883 = R.mul(F_dv_get(v_option, "effort"), F_dv_get(v_policy, "effort_cost"));
        object v_uncertainty__73937 = R.mul(R.add(R.count(F_dv_get(v_relation_and_state__73803, "unknown")), R.count(F_dv_get(F_dv_get(v_value__73738, "after"), "unknown"))), F_dv_get(v_policy, "uncertainty_cost"));
        return F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("score", R.sub(R.add(F_dv_get(v_value__73738, "delta"), F_dv_get(v_relation_and_state__73803, "score")), R.add(v_effort__73883, v_uncertainty__73937))), F_dv_pair("value", v_value__73738), F_dv_pair("relationship_and_state", v_relation_and_state__73803), F_dv_pair("effort", v_effort__73883) }, "uncertainty_cost", v_uncertainty__73937);
    }

    public static object F_dv_respond(object v_mind, object v_request, object v_options, object v_policy)
    {
        _ = F_dv_domain(v_mind, "mind");
        _ = F_dv_require(R.eq(F_dv_get(v_mind, "subject"), F_dv_get(v_policy, "subject")), "Foreign person policy");
        object v_appraisal__74420 = F_dv_appraise_request(v_mind, v_request);
        object v_i__74475 = R.number("0");
        object v_found__74489 = false;
        object v_best__74511 = R.number("0");
        object v_chosen__74528 = false;
        object v_evaluated__74551 = new System.Collections.Generic.List<object>(0) { };
        object v_rejected__74574 = new System.Collections.Generic.List<object>(0) { };
        object v_ids__74596 = new System.Collections.Generic.List<object>(0) { };
        while (R.Bool(R.lt(v_i__74475, R.count(v_options))))
        {
            object v_option__74652 = R.at(v_options, v_i__74475);
            object v_id__74684 = F_dv_get(v_option__74652, "id");
            object v_legal__74711 = F_dv_truth(v_mind, F_dv_get(v_option__74652, "feasible"));
            _ = F_dv_require(R.not(F_dv_has(v_ids__74596, v_id__74684)), "Duplicate response identity");
            _ = R.push(v_ids__74596, v_id__74684);
            if (R.Bool(R.eq(v_legal__74711, "true")))
            {
                object v_evaluation__74895 = F_dv_response_score(v_mind, v_option__74652, v_appraisal__74420, v_policy);
                _ = R.push(v_evaluated__74551, F_dv_pair(v_id__74684, v_evaluation__74895));
                object v_local_1__75031 = false;
                if (R.Bool(v_found__74489))
                {
                    v_local_1__75031 = R.lt(v_best__74511, F_dv_get(v_evaluation__74895, "score"));
                }
                else
                {
                    v_local_1__75031 = true;
                }
                if (R.Bool(v_local_1__75031))
                {
                    v_found__74489 = true;
                    v_best__74511 = F_dv_get(v_evaluation__74895, "score");
                    v_chosen__74528 = v_option__74652;
                }
            }
            else
            {
                _ = R.push(v_rejected__74574, F_dv_pair(v_id__74684, v_legal__74711));
            }
            v_i__74475 = R.add(v_i__74475, R.number("1"));
        }
        object v_local_2__75387 = false;
        if (R.Bool(v_found__74489))
        {
            v_local_2__75387 = "selected";
        }
        else
        {
            v_local_2__75387 = "no-response";
        }
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("status", v_local_2__75387), F_dv_pair("choice", v_chosen__74528), F_dv_pair("appraisal", v_appraisal__74420), F_dv_pair("evaluated", v_evaluated__74551) }, new System.Collections.Generic.List<object>(3) { F_dv_pair("rejected", v_rejected__74574), F_dv_pair("expression", F_dv_expression(v_appraisal__74420, F_dv_get(v_policy, "expression"))), F_dv_pair("committed", false) });
    }
}
}
