using DuelView.Core;
using DuelView.Compiled;
using static DuelView.Core.DVKernel;
public static partial class CampaignRuntime {
    public static object F_campaign_source(object v_path,object v_line,object v_id) {
        return DvValueTemplate.Instantiate("template.campaign_source", v_path, v_line, v_id);
    }
    public static object F_cognitive_erosion(object v_exposure_ref,object v_thresholds,object v_coupling_factor,object v_basis) {
        return DvValueTemplate.Instantiate("template.cognitive_erosion", v_exposure_ref, v_thresholds, v_coupling_factor, v_basis);
    }
    public static object F_campaign_evidence_ref(object v_atom) {
        return DvValueTemplate.Instantiate("template.campaign_evidence_ref", v_atom);
    }
    public static object F_campaign_at(object v_actor,object v_room) {
        return DvValueTemplate.Instantiate("template.campaign_at", v_actor, v_room);
    }
    public static object F_campaign_local_object(object v_id) {
        return DvValueTemplate.Instantiate("template.campaign_local_object", v_id);
    }
    public static object F_campaign_state_object(object v_id,object v_fields) {
        return DvValueTemplate.Instantiate("template.campaign_state_object", v_id, v_fields);
    }
    public static object F_campaign_install_visible_objects(object v_scene) {
        object v_visible__1657 = new List<object> {  };
        object v_i__1679 = R.number("0");
        while (R.Bool(R.lt(v_i__1679, R.count(F_dv_get(F_dv_get(v_scene, "physical"), "objects")))))
        {
            object v_object__1747 = R.at(R.at(F_dv_get(F_dv_get(v_scene, "physical"), "objects"), v_i__1679), R.number("1"));
            if (R.Bool(R.lt(R.number("0"), R.count(F_dv_find(v_object__1747, "visible_fields", new List<object> {  })))))
            {
                _ = R.push(v_visible__1657, F_dv_get(v_object__1747, "id"));
            }
            v_i__1679 = R.add(v_i__1679, R.number("1"));
        }
        object v_actors__1919 = new List<object> {  };
        object v_bindings__1940 = F_dv_get(v_scene, "bindings");
        v_i__1679 = R.number("0");
        while (R.Bool(R.lt(v_i__1679, R.count(F_dv_get(v_scene, "actors")))))
        {
            object v_actor__2029 = R.at(F_dv_get(v_scene, "actors"), v_i__1679);
            object v_mind__2066 = F_dv_get(v_actor__2029, "mind");
            object v_j__2097 = R.number("0");
            while (R.Bool(R.lt(v_j__2097, R.count(v_visible__1657))))
            {
                object v_id__2158 = R.at(v_visible__1657, v_j__2097);
                if (R.Bool(R.eq(F_dv_local(v_bindings__1940, v_id__2158, F_dv_get(v_actor__2029, "id")), "")))
                {
                    if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_mind__2066, "objects"), v_id__2158))))
                    {
                        v_mind__2066 = F_dv_add_object(v_mind__2066, F_campaign_local_object(v_id__2158));
                    }
                    v_bindings__1940 = F_dv_append(v_bindings__1940, F_dv_binding(v_id__2158, v_id__2158, F_dv_get(v_actor__2029, "id")));
                }
                v_j__2097 = R.add(v_j__2097, R.number("1"));
            }
            _ = R.push(v_actors__1919, DvValueTemplate.Instantiate("template.inline.campaign_install_visible_objects.1", v_actor__2029, v_mind__2066));
            v_i__1679 = R.add(v_i__1679, R.number("1"));
        }
        return F_dv_with_many(v_scene, new List<object> { F_dv_pair("actors", v_actors__1919), F_dv_pair("bindings", v_bindings__1940) });
    }
    public static object F_campaign_object_descriptor(object v_id) {
        object v_key__2863 = DvValueTemplate.Instantiate("template.inline.campaign_object_descriptor.2", v_id);
        object v_cached__2990 = R.memo_get(new List<object> {  }, v_key__2863);
        if (R.Bool(R.at(v_cached__2990, R.number("0"))))
        {
            return R.at(v_cached__2990, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__2863, F_campaign_object_descriptor_build(v_id));
    }
    public static object F_campaign_object_descriptor_build(object v_id) {
        return DvValueTemplate.Instantiate("template.campaign_object_descriptor_build", v_id);
    }
    public static object F_campaign_location_title(object v_location) {
        object v_key__5574 = DvValueTemplate.Instantiate("template.inline.campaign_location_title.3", v_location);
        object v_cached__5676 = R.memo_get(new List<object> {  }, v_key__5574);
        if (R.Bool(R.at(v_cached__5676, R.number("0"))))
        {
            return R.at(v_cached__5676, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__5574, F_campaign_location_title_build(v_location));
    }
    public static object F_campaign_location_title_build(object v_location) {
        return DvValueTemplate.Instantiate("template.campaign_location_title_build", v_location);
    }
    public static object F_campaign_location_region(object v_location) {
        object v_key__6156 = DvValueTemplate.Instantiate("template.inline.campaign_location_region.4", v_location);
        object v_cached__6259 = R.memo_get(new List<object> {  }, v_key__6156);
        if (R.Bool(R.at(v_cached__6259, R.number("0"))))
        {
            return R.at(v_cached__6259, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__6156, F_campaign_location_region_build(v_location));
    }
    public static object F_campaign_location_region_build(object v_location) {
        return DvValueTemplate.Instantiate("template.campaign_location_region_build", v_location);
    }
    public static object F_campaign_action(object v_id,object v_kind,object v_known,object v_physical,object v_law,object v_grants,object v_source) {
        return DvValueTemplate.Instantiate("template.campaign_action", v_id, v_kind, v_known, v_physical, v_law, v_grants, v_source);
    }
    public static object F_campaign_intent_action(object v_id,object v_kind,object v_known,object v_physical,object v_law,object v_grants,object v_label,object v_source) {
        return DvValueTemplate.Instantiate("template.campaign_intent_action", v_id, v_kind, v_known, v_physical, v_law, v_grants, v_label, v_source);
    }
    public static object F_campaign_method(object v_name,object v_law) {
        return DvValueTemplate.Instantiate("template.campaign_method", v_name, v_law);
    }
    public static object F_campaign_any(object v_left,object v_right) {
        return DvValueTemplate.Instantiate("template.campaign_any", v_left, v_right);
    }
    public static object F_campaign_authorized_speech(object v_action) {
        return DvValueTemplate.Instantiate("template.campaign_authorized_speech", v_action);
    }
    public static object F_campaign_case_ref(object v_group,object v_field) {
        return DvValueTemplate.Instantiate("template.campaign_case_ref", v_group, v_field);
    }
    public static object F_campaign_case_claim(object v_group,object v_field) {
        return DvValueTemplate.Instantiate("template.campaign_case_claim", v_group, v_field);
    }
    public static object F_campaign_first_night_ref(object v_field) {
        return DvValueTemplate.Instantiate("template.campaign_first_night_ref", v_field);
    }
    public static object F_campaign_positive_clock_bounds(object v_condition) {
        return F_dv_condition_bounds(v_condition, F_campaign_clock(), false);
    }
    public static object F_campaign_clock_boundaries(object v_condition) {
        return F_dv_condition_bounds(v_condition, F_campaign_clock(), true);
    }
    public static object F_campaign_without_clock(object v_condition) {
        return F_dv_condition_without_context(v_condition, F_campaign_clock(), AuthoredCopy("m35.campaign_without_clock.literal"));
    }
    public static object F_campaign_future_goal_pending(object v_actor,object v_goal) {
        object v_kind__9214 = F_dv_find(v_goal, "kind", "");
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.campaign_future_goal_pending.5", v_kind__9214)))
        {
            return R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_goal, "proposition")), "true"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.campaign_future_goal_pending.6", v_kind__9214)))
        {
            object v_method__9524 = F_dvp_find_action(F_dv_get(v_actor, "actions"), F_dv_find(v_goal, Authored("m35.campaign_future_goal_pending.literal_0003"), ""));
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.campaign_future_goal_pending.7", v_method__9524)))
            {
                return F_dvp_owned_decision_ready(v_actor, v_method__9524, F_dv_get(v_actor, "mind"));
            }
            return R.not(F_dv_has(F_dv_find(v_actor, Authored("m35.campaign_future_goal_pending.literal_0005"), new List<object> {  }), F_dv_find(v_goal, Authored("m35.campaign_future_goal_pending.literal_0003"), "")));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.campaign_future_goal_pending.8", v_kind__9214)))
        {
            if (R.Bool((R.Bool(F_dv_has_key(v_goal, "minimum_time")) ? (object)(R.lt(F_dv_get(F_dv_get(v_actor, "mind"), "time"), F_dv_get(v_goal, "minimum_time"))) : (object)(false))))
            {
                return true;
            }
            return R.not(F_dv_uncontested(F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_goal, "ref"))));
        }
        return false;
    }
    public static object F_campaign_index_goal_window(object v_goal) {
        object v_condition__10559 = F_dv_find(v_goal, Authored("m35.campaign_index_goal_window.literal"), F_dv_true());
        object v_key__10684 = AuthoredCopy("m35.campaign_index_goal_window.key");
        object v_cached__10788 = R.memo_get(v_condition__10559, v_key__10684);
        object v_window__10834 = new List<object> {  };
        if (R.Bool(R.at(v_cached__10788, R.number("0"))))
        {
            v_window__10834 = R.at(v_cached__10788, R.number("1"));
        }
        else
        {
            v_window__10834 = R.memo_put(v_condition__10559, v_key__10684, new List<object> { F_dv_pair("future_clock_condition", v_condition__10559), F_dv_pair("future_clock_bounds", F_campaign_positive_clock_bounds(v_condition__10559)), F_dv_pair("future_clock_boundaries", F_campaign_clock_boundaries(v_condition__10559)), F_dv_pair("future_nonclock", F_campaign_without_clock(v_condition__10559)) });
        }
        return F_dv_with_many(v_goal, v_window__10834);
    }
    public static object F_campaign_approved(object v_id) {
        return DvValueTemplate.Instantiate("template.campaign_approved", v_id);
    }
    public static object F_campaign_ref(object v_object,object v_field) {
        return DvValueTemplate.Instantiate("template.campaign_ref", v_object, v_field);
    }
    public static object F_campaign_body(object v_actor) {
        return DvValueTemplate.Instantiate("template.campaign_body", v_actor);
    }
    public static object F_campaign_own(object v_actor,object v_field) {
        return DvValueTemplate.Instantiate("template.campaign_own", v_actor, v_field);
    }
    public static object F_campaign_clock() {
        return DvValueTemplate.Instantiate("template.campaign_clock");
    }
    public static object F_campaign_access(object v_gate) {
        return DvValueTemplate.Instantiate("template.campaign_access", v_gate);
    }
    public static object F_campaign_position_ref(object v_actor) {
        return DvValueTemplate.Instantiate("template.campaign_position_ref", v_actor);
    }
    public static object F_campaign_window(object v_day,object v_start,object v_end) {
        return DvValueTemplate.Instantiate("template.campaign_window", v_day, v_start, v_end);
    }
    public static object F_campaign_people() {
        return DvValueTemplate.Instantiate("template.campaign_people");
    }
    public static object F_campaign_simulation_config(object v_scene) {
        return DvValueTemplate.Instantiate("template.campaign_simulation_config", v_scene);
    }
    public static object F_campaign_person_source(object v_actor) {
        return DvValueTemplate.Instantiate("template.campaign_person_source", v_actor);
    }
    public static object F_campaign_profile(object v_actor) {
        return DvValueTemplate.Instantiate("template.campaign_profile", v_actor);
    }
    public static object F_campaign_add_new(object v_world,object v_object) {
        if (R.Bool(F_dv_has_key(F_dv_get(v_world, "objects"), F_dv_get(v_object, "id"))))
        {
            return v_world;
        }
        return F_dv_add_object(v_world, v_object);
    }
    public static object F_campaign_surface_action(object v_id,object v_kind,object v_known,object v_physical,object v_law,object v_grants,object v_label,object v_text,object v_source) {
        return DvValueTemplate.Instantiate("template.campaign_surface_action", v_id, v_kind, v_known, v_physical, v_law, v_grants, v_label, v_text, v_source);
    }
    public static object F_campaign_goal(object v_proposition,object v_when,object v_basis) {
        return F_campaign_index_goal_window(DvValueTemplate.Instantiate("template.inline.campaign_goal.9", v_proposition, v_when, v_basis));
    }
    public static object F_campaign_observation_goal(object v_ref,object v_when,object v_basis,object v_time) {
        return F_campaign_index_goal_window(DvValueTemplate.Instantiate("template.inline.campaign_observation_goal.10", v_ref, v_when, v_basis, v_time));
    }
    public static object F_campaign_contract_text() {
        return DvValueTemplate.Instantiate("template.campaign_contract_text");
    }
    public static object F_campaign_person_name(object v_actor) {
        return DvValueTemplate.Instantiate("template.campaign_person_name", v_actor);
    }
    public static object F_campaign_handoff_fields() {
        return DvValueTemplate.Instantiate("template.campaign_handoff_fields");
    }
    public static object F_campaign_speech(object v_literal,object v_surface,object v_scope) {
        return DvValueTemplate.Instantiate("template.campaign_speech", v_literal, v_surface, v_scope);
    }
    public static object F_campaign_public_methods(object v_actions) {
        return DvValueTemplate.Instantiate("template.campaign_public_methods", v_actions);
    }
    public static object F_sims_autonomy_source(object v_id) {
        return DvValueTemplate.Instantiate("template.sims_autonomy_source", v_id);
    }

    public static object F_sims_implicit_habits(object v_actor) {
        return DvValueTemplate.Instantiate("template.sims_implicit_habits", v_actor);
    }

    public static object F_sims_actor_profile(object v_actor) {
        return F_dv_content_autonomy(v_actor, R.content("characters", v_actor), new List<object> { F_sims_autonomy_source(v_actor) });
    }
    public static object F_sims_brush_off_rows() {
        return DvValueTemplate.Instantiate("template.sims_brush_off_rows");
    }
    public static object F_campaign_finale_eaten_ref(object v_actor) {
        return DvValueTemplate.Instantiate("template.campaign_finale_eaten_ref", v_actor);
    }
    public static object F_campaign_finale_meal_complete() {
        return DvValueTemplate.Instantiate("template.campaign_finale_meal_complete");
    }

    public static object F_campaign_finale_meal_ready_ref() {
        return DvValueTemplate.Instantiate("template.campaign_finale_meal_ready_ref");
    }
    public static object F_campaign_finale_meal_remaining_ref() {
        return DvValueTemplate.Instantiate("template.campaign_finale_meal_remaining_ref");
    }
}
