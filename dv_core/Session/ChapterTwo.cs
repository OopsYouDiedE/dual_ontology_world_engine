using DuelView.Core;
using DuelView.Compiled;
using static DuelView.Core.DVKernel;
public static partial class CampaignRuntime {
    public static object F_c2_source(object v_file,object v_line,object v_id) {
        return DvValueTemplate.Instantiate("template.c2_source", v_file, v_line, v_id);
    }
    public static object F_c2_night(object v_day) {
        return DvValueTemplate.Instantiate("template.c2_night", v_day);
    }
    public static object F_c2_any_night() {
        return DvValueTemplate.Instantiate("template.c2_any_night");
    }
    public static object F_c2_edges() {
        object v_key__2043 = AuthoredCopy("m50.c2_edges.key");
        object v_cached__2129 = R.memo_get(new List<object> {  }, v_key__2043);
        if (R.Bool(R.at(v_cached__2129, R.number("0"))))
        {
            return R.at(v_cached__2129, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__2043, F_c2_edges_build());
    }
    public static object F_c2_edges_build() {
        return DvValueTemplate.Instantiate("template.c2_edges_build");
    }
    public static object F_c2_rooms() {
        return DvValueTemplate.Instantiate("template.c2_rooms");
    }
    public static object F_c2_norms() {
        object v_key__2502 = AuthoredCopy("m50.c2_norms.key");
        object v_cached__2588 = R.memo_get(new List<object> {  }, v_key__2502);
        if (R.Bool(R.at(v_cached__2588, R.number("0"))))
        {
            return R.at(v_cached__2588, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__2502, F_c2_norms_build());
    }
    public static object F_c2_norms_build() {
        return DvValueTemplate.Instantiate("template.c2_norms_build");
    }

    public static object F_c2_object(object v_id,object v_room,object v_fields) {
        return DvValueTemplate.Instantiate("template.c2_object", v_id, v_room, v_fields);
    }

    public static object F_c2_place_label(object v_place) {
        return DvValueTemplate.Instantiate("template.c2_place_label", v_place);
    }

    public static object F_c2_norm_condition(object v_norm,object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_norm_condition", v_norm, v_actor);
    }
    public static object F_c2_personal_norms(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_personal_norms", v_actor);
    }
    public static object F_c2_inspect_action(object v_actor,object v_object,object v_room,object v_fields,object v_label,object v_text,object v_source) {
        v_room = SelectionFixedInspectionRoom(v_object, v_room);
        object v_grants__40576 = new List<object> {  };
        object v_i__40597 = R.number("0");
        while (R.Bool(R.lt(v_i__40597, R.count(v_fields))))
        {
            _ = R.push(v_grants__40576, F_campaign_ref(v_object, R.at(v_fields, v_i__40597)));
            v_i__40597 = R.add(v_i__40597, R.number("1"));
        }
        object v_descriptor__40722 = F_campaign_object_descriptor(v_object);
        if (R.Bool(R.eq(F_dv_get(v_descriptor__40722, "map_kind"), "object")))
        {
            _ = R.push(v_grants__40576, F_campaign_ref(v_object, "name"));
            _ = R.push(v_grants__40576, F_campaign_ref(v_object, "location"));
        }
        object v_intent_label__40934 = v_label;
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspect_action.654", v_label, v_descriptor__40722)))
        {
            v_intent_label__40934 = R.cat(v_label, R.cat(Authored("m50.c2_inspect_action.literal_0002"), F_dv_get(v_descriptor__40722, "map_title")));
        }
        object v_action__41246 = F_dv_with_many(DvValueTemplate.Instantiate("template.inline.c2_inspect_action.655", v_actor, v_object, v_room, v_grants__40576, v_intent_label__40934, v_text, v_source), new List<object> { F_dv_pair("repeatable", true), F_dv_pair("manual_reinspect", true), F_dv_pair("presentation", F_c2_inspection_presentation(v_actor, v_object, v_fields)), F_dv_pair("required_room", v_room) });
        if (R.Bool(R.eq(F_dv_get(v_descriptor__40722, "map_kind"), "object")))
        {
            v_action__41246 = DvValueTemplate.Instantiate("template.inline.c2_inspect_action.656", v_action__41246, v_object);
        }
        return v_action__41246;
    }
    public static object F_c2_visible_line(object v_when,object v_template,object v_bindings,object v_source) {
        return DvValueTemplate.Instantiate("template.c2_visible_line", v_when, v_template, v_bindings, v_source);
    }
    public static object F_c2_visible_binding(object v_name,object v_ref,object v_labels) {
        return DvValueTemplate.Instantiate("template.c2_visible_binding", v_name, v_ref, v_labels);
    }
    public static object F_c2_inspection_presentation(object v_actor,object v_object,object v_fields) {
        object v_lines__42399 = F_c2m_visible_lines(v_object, v_fields);
        object v_source__42450 = F_campaign_approved(Authored("m50.c2_inspection_presentation.literal"));
        object v_labels__42565 = new List<object> {  };
        object v_states__42586 = new List<object> {  };
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspection_presentation.657", v_object)))
        {
            v_labels__42565 = AuthoredCopy("m50.c2_inspection_presentation.literal_0002");
            v_states__42586 = AuthoredCopy("m50.c2_inspection_presentation.literal_0003");
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspection_presentation.658", v_object)))
        {
            v_labels__42565 = AuthoredCopy("m50.c2_inspection_presentation.literal_0004");
            v_states__42586 = AuthoredCopy("m50.c2_inspection_presentation.literal_0005");
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspection_presentation.659", v_object)))
        {
            v_labels__42565 = AuthoredCopy("m50.c2_inspection_presentation.literal_0006");
            v_states__42586 = AuthoredCopy("m50.c2_inspection_presentation.literal_0007");
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspection_presentation.660", v_object)))
        {
            v_labels__42565 = AuthoredCopy("m50.c2_inspection_presentation.literal_0008");
            v_states__42586 = AuthoredCopy("m50.c2_inspection_presentation.literal_0009");
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspection_presentation.661", v_object)))
        {
            v_labels__42565 = AuthoredCopy("m50.c2_inspection_presentation.literal_0010");
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspection_presentation.662", v_object)))
        {
            v_labels__42565 = AuthoredCopy("m50.c2_inspection_presentation.literal_0011");
            v_states__42586 = AuthoredCopy("m50.c2_inspection_presentation.literal_0012");
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspection_presentation.663", v_object)))
        {
            v_labels__42565 = AuthoredCopy("m50.c2_inspection_presentation.literal_0014");
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspection_presentation.664", v_object)))
        {
            v_labels__42565 = AuthoredCopy("m50.c2_inspection_presentation.literal_0016");
            v_states__42586 = AuthoredCopy("m50.c2_inspection_presentation.literal_0017");
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspection_presentation.665", v_object)))
        {
            v_labels__42565 = AuthoredCopy("m50.c2_inspection_presentation.literal_0018");
            v_states__42586 = AuthoredCopy("m50.c2_inspection_presentation.literal_0019");
        }
        object v_i__45414 = R.number("0");
        while (R.Bool(R.lt(v_i__45414, R.count(v_fields))))
        {
            object v_field__45466 = R.at(v_fields, v_i__45414);
            if (R.Bool(F_dv_has_key(v_labels__42565, v_field__45466)))
            {
                object v_binding__45540 = F_c2_visible_binding(Authored("m50.c2_inspection_presentation.literal_0020"), F_campaign_ref(v_object, v_field__45466), F_dv_find(v_states__42586, v_field__45466, new List<object> {  }));
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspection_presentation.666", v_object, v_field__45466)))
                {
                    v_binding__45540 = DvValueTemplate.Instantiate("template.inline.c2_inspection_presentation.667", v_binding__45540);
                }
                _ = R.push(v_lines__42399, F_c2_visible_line(F_dv_true(), R.cat(F_dv_get(v_labels__42565, v_field__45466), Authored("m50.c2_inspection_presentation.literal_0024")), new List<object> { v_binding__45540 }, v_source__42450));
            }
            v_i__45414 = R.add(v_i__45414, R.number("1"));
        }
        object v_extra__46332 = F_c2_inspection_detail_lines(v_actor, v_object, v_fields);
        v_i__45414 = R.number("0");
        while (R.Bool(R.lt(v_i__45414, R.count(v_extra__46332))))
        {
            _ = R.push(v_lines__42399, R.at(v_extra__46332, v_i__45414));
            v_i__45414 = R.add(v_i__45414, R.number("1"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspection_presentation.668", v_object, v_fields)))
        {
            _ = R.push(v_lines__42399, F_c2_visible_line(DvValueTemplate.Instantiate("template.inline.c2_inspection_presentation.669"), Authored("m50.c2_inspection_presentation.literal_0027"), new List<object> {  }, v_source__42450));
        }
        return DvValueTemplate.Instantiate("template.inline.c2_inspection_presentation.670", v_actor, v_lines__42399);
    }
    public static object F_c2_inspection_people_labels() {
        object v_labels__47512 = new List<object> {  };
        object v_people__47533 = F_campaign_people();
        object v_i__47569 = R.number("0");
        while (R.Bool(R.lt(v_i__47569, R.count(v_people__47533))))
        {
            v_labels__47512 = F_dv_with(v_labels__47512, R.at(v_people__47533, v_i__47569), F_campaign_person_name(R.at(v_people__47533, v_i__47569)));
            v_i__47569 = R.add(v_i__47569, R.number("1"));
        }
        return v_labels__47512;
    }
    public static object F_c2_inspection_detail_lines(object v_actor,object v_object,object v_fields) {
        object v_lines__47790 = new List<object> {  };
        object v_source__47810 = F_campaign_approved(Authored("m50.c2_inspection_presentation.literal"));
        object v_labels__47925 = new List<object> {  };
        object v_states__47946 = new List<object> {  };
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.671", v_object)))
        {
            v_labels__47925 = AuthoredCopy("m50.c2_inspection_detail_lines.literal");
            v_states__47946 = AuthoredCopy("m50.c2_inspection_detail_lines.literal_0002");
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.672", v_object)))
        {
            v_labels__47925 = AuthoredCopy("m50.c2_inspection_detail_lines.literal_0003");
            v_states__47946 = AuthoredCopy("m50.c2_inspection_detail_lines.literal_0004");
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.673", v_object)))
        {
            v_labels__47925 = AuthoredCopy("m50.c2_inspection_detail_lines.literal_0005");
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.674", v_object)))
        {
            v_labels__47925 = AuthoredCopy("m50.c2_inspection_detail_lines.literal_0006");
            v_states__47946 = AuthoredCopy("m50.c2_inspection_detail_lines.literal_0007");
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.675", v_object)))
        {
            v_labels__47925 = AuthoredCopy("m50.c2_inspection_detail_lines.literal_0011");
            v_states__47946 = new List<object> { DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.676"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.677"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.678"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.679"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.680"), F_dv_pair("cabinet_key_holder", F_c2_inspection_people_labels()), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.681"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.682"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.683"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.684"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.685"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.686"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.687"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.688"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.689"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.690"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.691"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.692"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.693"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.694"), F_dv_pair("medicine_key_holder", F_c2_inspection_people_labels()), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.695"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.696"), DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.697") };
        }
        object v_people__52336 = F_campaign_people();
        object v_person_index__52372 = R.number("0");
        while (R.Bool(R.lt(v_person_index__52372, R.count(v_people__52336))))
        {
            object v_person__52446 = R.at(v_people__52336, v_person_index__52372);
            if (R.Bool(R.eq(v_object, F_campaign_body(v_person__52446))))
            {
                v_labels__47925 = AuthoredCopy("m50.c2_inspection_detail_lines.literal_0034");
                v_states__47946 = AuthoredCopy("m50.c2_inspection_detail_lines.literal_0035");
                object v_jobs__52778 = F_c2_work_specs();
                object v_job_index__52818 = R.number("0");
                while (R.Bool(R.lt(v_job_index__52818, R.count(v_jobs__52778))))
                {
                    if (R.Bool(R.eq(F_dv_get(R.at(v_jobs__52778, v_job_index__52818), "actor"), v_person__52446)))
                    {
                        object v_field__52957 = R.cat(Authored("m50.c2_initial_body.literal_0008"), F_dv_get(R.at(v_jobs__52778, v_job_index__52818), "id"));
                        v_labels__47925 = DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.698", v_labels__47925, v_field__52957, v_jobs__52778, v_job_index__52818);
                        v_states__47946 = DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.699", v_states__47946, v_field__52957);
                    }
                    v_job_index__52818 = R.add(v_job_index__52818, R.number("1"));
                }
            }
            v_person_index__52372 = R.add(v_person_index__52372, R.number("1"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.700", v_object)))
        {
            v_labels__47925 = AuthoredCopy("m50.c2_inspection_detail_lines.literal_0038");
            v_states__47946 = AuthoredCopy("m50.c2_inspection_detail_lines.literal_0039");
            v_person_index__52372 = R.number("0");
            while (R.Bool(R.lt(v_person_index__52372, R.count(v_people__52336))))
            {
                object v_person__53896 = R.at(v_people__52336, v_person_index__52372);
                object v_name__53943 = F_campaign_person_name(v_person__53896);
                v_labels__47925 = DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.701", v_labels__47925, v_person__53896, v_name__53943);
                v_labels__47925 = DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.702", v_labels__47925, v_person__53896, v_name__53943);
                v_labels__47925 = DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.703", v_labels__47925, v_person__53896, v_name__53943);
                v_states__47946 = DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.704", v_states__47946, v_person__53896);
                v_states__47946 = DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.705", v_states__47946, v_person__53896);
                v_states__47946 = DvValueTemplate.Instantiate("template.inline.c2_inspection_detail_lines.706", v_states__47946, v_person__53896);
                v_person_index__52372 = R.add(v_person_index__52372, R.number("1"));
            }
        }
        object v_i__55452 = R.number("0");
        while (R.Bool(R.lt(v_i__55452, R.count(v_fields))))
        {
            object v_field__55504 = R.at(v_fields, v_i__55452);
            if (R.Bool(F_dv_has_key(v_labels__47925, v_field__55504)))
            {
                object v_binding__55578 = F_c2_visible_binding(Authored("m50.c2_inspection_presentation.literal_0020"), F_campaign_ref(v_object, v_field__55504), F_dv_find(v_states__47946, v_field__55504, new List<object> {  }));
                _ = R.push(v_lines__47790, F_c2_visible_line(F_dv_true(), R.cat(F_dv_get(v_labels__47925, v_field__55504), Authored("m50.c2_inspection_presentation.literal_0024")), new List<object> { v_binding__55578 }, v_source__47810));
            }
            v_i__55452 = R.add(v_i__55452, R.number("1"));
        }
        return v_lines__47790;
    }
    public static object F_c2_route_inspection(object v_actor,object v_origin,object v_target,object v_gate,object v_airlock) {
        return DvValueTemplate.Instantiate("template.c2_route_inspection", v_actor, v_origin, v_target, v_gate, v_airlock);
    }
    public static object F_c2_route_inspections(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_route_inspections", v_actor);
    }
    public static object F_c2_collect_law(object v_actor,object v_day,object v_meal,object v_start,object v_end) {
        return DvValueTemplate.Instantiate("template.c2_collect_law", v_actor, v_day, v_meal, v_start, v_end);
    }
    public static object F_c2_eat_law(object v_actor,object v_day,object v_meal) {
        return DvValueTemplate.Instantiate("template.c2_eat_law", v_actor, v_day, v_meal);
    }
    public static object F_c2_open_granary_law(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_open_granary_law", v_actor);
    }
    public static object F_c2_initial_count_law(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c2_initial_count_law", v_行动者);
    }
    public static object F_c2_distribute_law(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c2_distribute_law", v_行动者);
    }
    public static object F_c2_total_store_open_known(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_total_store_open_known", v_actor);
    }
    public static object F_c2_total_store_open_law(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c2_total_store_open_law", v_行动者);
    }
    public static object F_c2_give_key_law(object v_行动者,object v_目标) {
        return DvValueTemplate.Instantiate("template.c2_give_key_law", v_行动者, v_目标);
    }
    public static object F_c2_night_take_variant_law(object v_actor,object v_patient,object v_registered) {
        return DvValueTemplate.Instantiate("template.c2_night_take_variant_law", v_actor, v_patient, v_registered);
    }
    public static object F_c2_night_take_law(object v_actor,object v_patient) {
        return DvValueTemplate.Instantiate("template.c2_night_take_law", v_actor, v_patient);
    }
    public static object F_c2_report_law(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c2_report_law", v_行动者);
    }
    public static object F_c2_night_eat_law(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_night_eat_law", v_actor);
    }
    public static object F_c2_deliver_patient_law(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c2_deliver_patient_law", v_行动者);
    }
    public static object F_c2_feed_patient_law(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c2_feed_patient_law", v_行动者);
    }
    public static object F_c2_leave_care_law(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c2_leave_care_law", v_行动者);
    }
    public static object F_c2_latch_request_law(object v_发起者,object v_目标) {
        return DvValueTemplate.Instantiate("template.c2_latch_request_law", v_发起者, v_目标);
    }
    public static object F_c2_check_structure_law(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c2_check_structure_law", v_行动者);
    }
    public static object F_c2_repair_latch_law(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c2_repair_latch_law", v_行动者);
    }
    public static object F_c2_norm_available(object v_norm) {
        return DvValueTemplate.Instantiate("template.c2_norm_available", v_norm);
    }
    public static object F_c2_propose_law(object v_行动者,object v_规约) {
        return DvValueTemplate.Instantiate("template.c2_propose_law", v_行动者, v_规约);
    }
    public static object F_c2_post_law(object v_行动者,object v_规约) {
        return DvValueTemplate.Instantiate("template.c2_post_law", v_行动者, v_规约);
    }
    public static object F_c2_read_norm_law(object v_行动者,object v_规约) {
        return DvValueTemplate.Instantiate("template.c2_read_norm_law", v_行动者, v_规约);
    }
    public static object F_c2_decide_norm_law(object v_行动者,object v_规约,object v_是否接受) {
        return DvValueTemplate.Instantiate("template.c2_decide_norm_law", v_行动者, v_规约, v_是否接受);
    }
    public static object F_c2_board_actions(object v_actor) {
        object v_norms__83407 = F_c2_norms();
        object v_boards__83435 = new List<object> {  };
        object v_actions__83456 = new List<object> {  };
        object v_i__83478 = R.number("0");
        while (R.Bool(R.lt(v_i__83478, R.count(v_norms__83407))))
        {
            object v_board__83529 = F_dv_get(R.at(v_norms__83407, v_i__83478), "board");
            if (R.Bool(R.not(F_dv_has(v_boards__83435, v_board__83529))))
            {
                _ = R.push(v_boards__83435, v_board__83529);
                object v_grants__83644 = new List<object> {  };
                object v_lines__83673 = new List<object> {  };
                object v_empty__83701 = new List<object> {  };
                object v_j__83729 = R.number("0");
                while (R.Bool(R.lt(v_j__83729, R.count(v_norms__83407))))
                {
                    object v_norm__83796 = R.at(v_norms__83407, v_j__83729);
                    if (R.Bool(R.eq(F_dv_get(v_norm__83796, "board"), v_board__83529)))
                    {
                        object v_posted__83878 = F_campaign_ref(F_c2_norm_object(F_dv_get(v_norm__83796, "id")), Authored("m50.c2_norm_available.literal_0008"));
                        _ = R.push(v_grants__83644, v_posted__83878);
                        _ = R.push(v_empty__83701, F_dv_fact(v_posted__83878, false));
                        _ = R.push(v_lines__83673, F_c2_visible_line(F_dv_fact(v_posted__83878, true), R.cat(Authored("m50.c2_board_actions.literal"), R.cat(F_dv_get(v_norm__83796, "title"), Authored("m50.c2_board_actions.literal_0002"))), new List<object> {  }, F_dv_get(v_norm__83796, "source")));
                    }
                    v_j__83729 = R.add(v_j__83729, R.number("1"));
                }
                _ = R.push(v_lines__83673, F_c2_visible_line(F_dv_all(v_empty__83701), Authored("m50.c2_board_actions.literal_0003"), new List<object> {  }, F_campaign_approved(Authored("m50.c2_board_actions.literal_0004"))));
                object v_id__84644 = R.cat(v_actor, R.cat(Authored("m50.c2_board_actions.literal_0005"), v_board__83529));
                object v_here__84769 = F_campaign_at(v_actor, v_board__83529);
                object v_action__84819 = DvValueTemplate.Instantiate("template.inline.c2_board_actions.707", v_id__84644, v_here__84769, v_grants__83644);
                _ = R.push(v_actions__83456, DvValueTemplate.Instantiate("template.inline.c2_board_actions.708", v_action__84819, v_actor, v_lines__83673));
            }
            v_i__83478 = R.add(v_i__83478, R.number("1"));
        }
        return v_actions__83456;
    }
    public static object F_c2_norm_actions(object v_actor,object v_norm) {
        object v_object__85459 = F_c2_norm_object(F_dv_get(v_norm, "id"));
        object v_result__85501 = new List<object> {  };
        if (R.Bool(R.eq(v_actor, F_dv_get(v_norm, "owner"))))
        {
            object v_id__85555 = R.cat(v_actor, R.cat(Authored("m50.c2_norm_actions.literal"), F_dv_get(v_norm, "id")));
            object v_speech__85672 = DvValueTemplate.Instantiate("template.inline.c2_norm_actions.709", v_object__85459, v_norm);
            object v_proposal__86004 = DvValueTemplate.Instantiate("template.inline.c2_norm_actions.710", v_id__85555, v_actor, v_norm, v_object__85459, v_speech__85672);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_norm_actions.711", v_norm)))
            {
                v_proposal__86004 = DvValueTemplate.Instantiate("template.inline.c2_norm_actions.712", v_proposal__86004, v_norm, v_object__85459);
            }
            _ = R.push(v_result__85501, v_proposal__86004);
            object v_post_id__87140 = R.cat(v_actor, R.cat(Authored("m50.c2_norm_actions.literal_0007"), F_dv_get(v_norm, "id")));
            object v_pre__87267 = DvValueTemplate.Instantiate("template.inline.c2_norm_actions.713", v_actor, v_norm, v_object__85459);
            _ = R.push(v_result__85501, DvValueTemplate.Instantiate("template.inline.c2_norm_actions.714", v_post_id__87140, v_pre__87267, v_object__85459, v_norm));
        }
        object v_read_id__88051 = R.cat(v_actor, R.cat(Authored("m50.c2_norm_actions.literal_0011"), F_dv_get(v_norm, "id")));
        object v_read_pre__88174 = DvValueTemplate.Instantiate("template.inline.c2_norm_actions.715", v_actor, v_norm, v_object__85459);
        _ = R.push(v_result__85501, DvValueTemplate.Instantiate("template.inline.c2_norm_actions.716", v_read_id__88051, v_read_pre__88174, v_object__85459, v_actor, v_norm));
        object v_recheck_pre__89214 = DvValueTemplate.Instantiate("template.inline.c2_norm_actions.717", v_actor, v_norm, v_object__85459);
        object v_recheck__89390 = DvValueTemplate.Instantiate("template.inline.c2_norm_actions.718", v_actor, v_norm, v_recheck_pre__89214, v_object__85459);
        _ = R.push(v_result__85501, F_dv_with_many(v_recheck__89390, new List<object> { F_dv_pair("repeatable", true), F_dv_pair("manual_reinspect", true), F_dv_pair("norm_receipt", new List<object> { F_dv_pair("norm", v_norm), F_dv_pair("content", F_dv_fact(F_campaign_ref(v_object__85459, "text"), F_dv_get(v_norm, "surface"))) }) }));
        object v_known__90007 = F_dv_fact(F_campaign_ref(v_object__85459, "text"), F_dv_get(v_norm, "surface"));
        object v_accepted__90076 = true;
        object v_n__90101 = R.number("0");
        while (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_norm_actions.719", v_n__90101)))
        {
            object v_verb__90200 = Authored("m50.c2_norm_actions.verb");
            object v_promotes__90284 = F_dv_get(v_norm, "promotes");
            object v_sacrifices__90322 = F_dv_get(v_norm, "sacrifices");
            object v_surface__90364 = Authored("m50.c2_norm_actions.surface");
            if (R.Bool(R.not(v_accepted__90076)))
            {
                v_verb__90200 = Authored("m50.c2_norm_actions.literal_0018");
                v_promotes__90284 = F_dv_get(v_norm, "sacrifices");
                v_sacrifices__90322 = F_dv_get(v_norm, "promotes");
                v_surface__90364 = Authored("m50.c2_norm_actions.literal_0019");
            }
            v_surface__90364 = F_dvs_surface_parts(DvValueTemplate.Instantiate("template.inline.c2_norm_actions.720", v_norm, v_surface__90364));
            object v_id__90955 = R.cat(v_actor, R.cat(Authored("m50.c2_norm_actions.literal_0022"), R.cat(v_verb__90200, R.cat(Authored("m50.c2_move_action.literal_0002"), F_dv_get(v_norm, "id")))));
            object v_grants__91171 = DvValueTemplate.Instantiate("template.inline.c2_norm_actions.721", v_actor, v_norm);
            object v_speech__91423 = DvValueTemplate.Instantiate("template.inline.c2_norm_actions.722", v_norm, v_surface__90364);
            object v_choice__91587 = DvValueTemplate.Instantiate("template.inline.c2_norm_actions.723", v_norm, v_known__90007, v_actor, v_promotes__90284, v_sacrifices__90322);
            object v_supported_choice__91939 = v_choice__91587;
            if (R.Bool(v_accepted__90076))
            {
                v_supported_choice__91939 = DvValueTemplate.Instantiate("template.inline.c2_norm_actions.724", v_choice__91587);
            }
            _ = R.push(v_result__85501, DvValueTemplate.Instantiate("template.inline.c2_norm_actions.725", v_id__90955, v_known__90007, v_actor, v_norm, v_grants__91171, v_verb__90200, v_surface__90364, v_speech__91423, v_supported_choice__91939, v_accepted__90076));
            v_accepted__90076 = false;
            v_n__90101 = R.add(v_n__90101, R.number("1"));
        }
        return v_result__85501;
    }
    public static object F_c2_missed_meal_law(object v_actor,object v_day,object v_meal,object v_end) {
        return DvValueTemplate.Instantiate("template.c2_missed_meal_law", v_actor, v_day, v_meal, v_end);
    }
    public static object F_c2_terminal_exposure_law(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c2_terminal_exposure_law", v_行动者);
    }
    public static object F_c2_first_wind_law() {
        return DvValueTemplate.Instantiate("template.c2_first_wind_law");
    }
    public static object F_c2_weather_changes() {
        return DvValueTemplate.Instantiate("template.c2_weather_changes");
    }
    public static object F_c2_weather_change_law(object v_change) {
        return DvValueTemplate.Instantiate("template.c2_weather_change_law", v_change);
    }
    public static object F_c2_daylight_law(object v_day,object v_light) {
        return DvValueTemplate.Instantiate("template.c2_daylight_law", v_day, v_light);
    }
    public static object F_c2_corridor_origin(object v_gate) {
        return DvValueTemplate.Instantiate("template.c2_corridor_origin", v_gate);
    }
    public static object F_c2_purge_corridor_law(object v_actor,object v_gate) {
        return DvValueTemplate.Instantiate("template.c2_purge_corridor_law", v_actor, v_gate);
    }
    public static object F_c2_open_corridor_law(object v_actor,object v_gate) {
        return DvValueTemplate.Instantiate("template.c2_open_corridor_law", v_actor, v_gate);
    }
    public static object F_c2_corridor_actions(object v_actor) {
        object v_actions__102072 = new List<object> {  };
        object v_gates__102094 = AuthoredCopy("m50.c2_world_objects.corridor_gates");
        object v_i__102201 = R.number("0");
        while (R.Bool(R.lt(v_i__102201, R.count(v_gates__102094))))
        {
            object v_gate__102252 = R.at(v_gates__102094, v_i__102201);
            object v_object__102281 = R.cat(Authored("m50.c2_weather_changes.literal_0002"), v_gate__102252);
            object v_title__102392 = Authored("m50.c2_corridor_actions.title");
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_corridor_actions.726", v_gate__102252)))
            {
                v_title__102392 = Authored("m50.c2_corridor_actions.literal");
            }
            object v_source__102665 = F_campaign_approved(Authored("m50.c2_corridor_actions.literal_0002"));
            object v_inspection__102782 = F_c2_inspect_action(v_actor, v_object__102281, F_c2_corridor_origin(v_gate__102252), AuthoredCopy("m50.c2_corridor_actions.literal_0003"), R.cat(Authored("m50.c2_corridor_actions.literal_0004"), v_title__102392), R.cat(Authored("m50.c2_corridor_actions.literal_0005"), R.cat(v_title__102392, Authored("m50.c2_corridor_actions.literal_0006"))), v_source__102665);
            _ = R.push(v_actions__102072, DvValueTemplate.Instantiate("template.inline.c2_corridor_actions.727", v_inspection__102782));
            object v_purge__103484 = F_c2_purge_corridor_law(v_actor, v_gate__102252);
            object v_purge_id__103540 = R.cat(v_actor, R.cat(Authored("m50.c2_corridor_actions.literal_0007"), v_gate__102252));
            object v_purge_grants__103669 = DvValueTemplate.Instantiate("template.inline.c2_corridor_actions.728", v_object__102281);
            _ = R.push(v_actions__102072, DvValueTemplate.Instantiate("template.inline.c2_corridor_actions.729", v_purge_id__103540, v_purge__103484, v_purge_grants__103669, v_title__102392, v_source__102665, v_object__102281));
            object v_law__105042 = F_c2_open_corridor_law(v_actor, v_gate__102252);
            object v_id__105095 = R.cat(v_actor, R.cat(Authored("m50.c2_corridor_actions.literal_0011"), v_gate__102252));
            object v_action__105218 = DvValueTemplate.Instantiate("template.inline.c2_corridor_actions.730", v_id__105095, v_law__105042, v_gate__102252, v_title__102392, v_source__102665);
            v_action__105218 = DvValueTemplate.Instantiate("template.inline.c2_corridor_actions.731", v_action__105218, v_id__105095, v_gate__102252);
            _ = R.push(v_actions__102072, v_action__105218);
            v_i__102201 = R.add(v_i__102201, R.number("1"));
        }
        return v_actions__102072;
    }
    public static object F_c2_supply_actions(object v_actor) {
        object v_result__105987 = new List<object> {  };
        object v_day__106008 = Authored("m50.c2_any_night.literal");
        while (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_supply_actions.732", v_day__106008)))
        {
            object v_meals__106179 = AuthoredCopy("m50.c2_supply_actions.meals");
            object v_i__106282 = R.number("0");
            while (R.Bool(R.lt(v_i__106282, R.count(v_meals__106179))))
            {
                object v_meal__106341 = R.at(v_meals__106179, v_i__106282);
                object v_suffix__106374 = R.cat(R.show(v_day__106008), R.cat(Authored("m50.c2_move_action.literal_0002"), F_dv_get(v_meal__106341, "id")));
                object v_id__106510 = R.cat(v_actor, R.cat(Authored("m50.c2_supply_actions.literal"), v_suffix__106374));
                object v_flag__106632 = R.cat(Authored("m50.c2_initial_body.literal_0014"), v_suffix__106374);
                object v_known__106979 = DvValueTemplate.Instantiate("template.inline.c2_supply_actions.733", v_actor, v_day__106008, v_meal__106341);
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_supply_actions.734", v_day__106008, v_meal__106341)))
                {
                    _ = R.push(v_result__105987, DvValueTemplate.Instantiate("template.inline.c2_supply_actions.735", v_id__106510, v_known__106979, v_actor, v_day__106008, v_meal__106341, v_flag__106632));
                }
                object v_eat__108721 = R.cat(v_actor, R.cat(Authored("m50.c2_supply_actions.literal_0009"), v_suffix__106374));
                object v_eat_known__108849 = DvValueTemplate.Instantiate("template.inline.c2_supply_actions.736", v_day__106008, v_meal__106341, v_actor);
                _ = R.push(v_result__105987, DvValueTemplate.Instantiate("template.inline.c2_supply_actions.737", v_eat__108721, v_eat_known__108849, v_actor, v_day__106008, v_meal__106341, v_suffix__106374));
                v_i__106282 = R.add(v_i__106282, R.number("1"));
            }
            v_day__106008 = R.add(v_day__106008, R.number("1"));
        }
        object v_open__109923 = R.cat(v_actor, Authored("m50.c2_supply_actions.literal_0013"));
        object v_can_open__110028 = F_campaign_any(F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0006"), Authored("m50.c2_route_inspections.literal_0011")), v_actor), F_dv_fact(F_campaign_own(v_actor, Authored("m50.c2_initial_body.literal_0023")), true));
        object v_open_pre__110352 = DvValueTemplate.Instantiate("template.inline.c2_supply_actions.738", v_actor, v_can_open__110028);
        _ = R.push(v_result__105987, DvValueTemplate.Instantiate("template.inline.c2_supply_actions.739", v_open__109923, v_open_pre__110352));
        object v_key__111206 = DvValueTemplate.Instantiate("template.inline.c2_supply_actions.740", v_actor);
        object v_count_id__111511 = R.cat(v_actor, Authored("m50.c2_supply_actions.literal_0019"));
        object v_count_known__111620 = DvValueTemplate.Instantiate("template.inline.c2_supply_actions.741", v_actor);
        _ = R.push(v_result__105987, DvValueTemplate.Instantiate("template.inline.c2_supply_actions.742", v_count_id__111511, v_count_known__111620, v_actor));
        object v_distribute__112772 = R.cat(v_actor, Authored("m50.c2_supply_actions.literal_0023"));
        _ = R.push(v_result__105987, DvValueTemplate.Instantiate("template.inline.c2_supply_actions.743", v_distribute__112772, v_actor));
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_supply_actions.744", v_actor)))
        {
            object v_total_store__113602 = R.cat(v_actor, Authored("m50.c2_supply_actions.literal_0028"));
            object v_ready__113718 = DvValueTemplate.Instantiate("template.inline.c2_supply_actions.745", v_actor);
            _ = R.push(v_result__105987, DvValueTemplate.Instantiate("template.inline.c2_supply_actions.746", v_total_store__113602, v_ready__113718, v_actor));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_supply_actions.747", v_actor)))
        {
            object v_give__115589 = R.cat(v_actor, Authored("m50.c2_supply_actions.literal_0033"));
            _ = R.push(v_result__105987, DvValueTemplate.Instantiate("template.inline.c2_supply_actions.748", v_give__115589, v_key__111206));
        }
        object v_report__116576 = R.cat(v_actor, Authored("m50.c2_supply_actions.literal_0038"));
        object v_report_pre__116683 = F_dv_get(F_c2_report_law(v_actor), "premise");
        _ = R.push(v_result__105987, DvValueTemplate.Instantiate("template.inline.c2_supply_actions.749", v_report__116576, v_report_pre__116683, v_actor));
        if (R.Bool(F_dv_has(F_campaign_people(), v_actor)))
        {
            object v_id__117703 = R.cat(v_actor, Authored("m50.c2_supply_actions.literal_0042"));
            object v_pre__117810 = F_dv_get(F_c2_night_take_law(v_actor, false), "premise");
            _ = R.push(v_result__105987, DvValueTemplate.Instantiate("template.inline.c2_supply_actions.750", v_id__117703, v_pre__117810, v_actor));
            object v_eat__118885 = R.cat(v_actor, Authored("m50.c2_supply_actions.literal_0046"));
            object v_pre_eat__118993 = F_dv_get(F_c2_night_eat_law(v_actor), "premise");
            _ = R.push(v_result__105987, DvValueTemplate.Instantiate("template.inline.c2_supply_actions.751", v_eat__118885, v_pre_eat__118993, v_actor));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_supply_actions.752", v_actor)))
        {
            object v_pre__120038 = F_dv_get(F_c2_night_take_law(v_actor, true), "premise");
            _ = R.push(v_result__105987, DvValueTemplate.Instantiate("template.inline.c2_supply_actions.753", v_pre__120038, v_actor));
        }
        return v_result__105987;
    }

    public static object F_c2_inspections(object v_actor) {
        object v_result__130222 = F_c2_route_inspections(v_actor);
        object v_source__130268 = F_c2_source(Authored("m50.c2_special_actions.literal_0005"), Authored("m50.c2_inspections.literal"), Authored("m50.c2_inspections.literal_0002"));
        _ = R.push(v_result__130222, F_c2_inspect_action(v_actor, Authored("m50.c2_world_objects.literal_0006"), Authored("m50.c2_norms_build.literal_0034"), AuthoredCopy("m50.c2_inspections.literal_0003"), Authored("m50.c2_inspections.literal_0004"), Authored("m50.c2_inspections.literal_0005"), v_source__130268));
        object v_stock_when__131098 = DvValueTemplate.Instantiate("template.inline.c2_inspections.754", v_actor);
        object v_stock__131427 = F_c2_inspect_action(v_actor, Authored("m50.c2_world_objects.literal_0006"), Authored("m50.c2_norms_build.literal_0034"), AuthoredCopy("m50.c2_inspections.literal_0006"), Authored("m50.c2_inspections.literal_0007"), Authored("m50.c2_inspections.literal_0008"), F_campaign_approved(Authored("m50.c2_inspections.literal_0009")));
        _ = R.push(v_result__130222, DvValueTemplate.Instantiate("template.inline.c2_inspections.755", v_stock__131427, v_actor, v_stock_when__131098));
        _ = R.push(v_result__130222, F_c2_inspect_action(v_actor, Authored("m50.c2_world_objects.literal_0009"), Authored("m50.c2_norms_build.literal_0034"), AuthoredCopy("m50.c2_inspections.literal_0011"), Authored("m50.c2_inspections.literal_0012"), Authored("m50.c2_inspections.literal_0013"), v_source__130268));
        _ = R.push(v_result__130222, F_c2_inspect_action(v_actor, Authored("m50.c2_world_objects.literal_0007"), Authored("m50.c2_norms_build.literal_0034"), AuthoredCopy("m50.c2_inspections.literal_0014"), Authored("m50.c2_inspections.literal_0015"), Authored("m50.c2_inspections.literal_0016"), v_source__130268));
        _ = R.push(v_result__130222, F_c2_inspect_action(v_actor, Authored("m50.c2_world_objects.literal_0010"), Authored("m50.c2_world_objects.literal_0011"), AuthoredCopy("m50.c2_inspections.literal_0017"), Authored("m50.c2_inspections.literal_0018"), Authored("m50.c2_inspections.literal_0019"), v_source__130268));
        object v_groups__133438 = AuthoredCopy("m50.c2_inspections.groups");
        object v_i__133536 = R.number("0");
        while (R.Bool(R.lt(v_i__133536, R.count(v_groups__133438))))
        {
            object v_room__133588 = R.cat(Authored("m50.c2_world_objects.literal_0026"), R.at(v_groups__133438, v_i__133536));
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_inspections.756", v_groups__133438, v_i__133536)))
            {
                v_room__133588 = Authored("m50.c2_inspections.literal_0020");
            }
            _ = R.push(v_result__130222, F_c2_inspect_action(v_actor, R.cat(Authored("m50.c2_world_objects.literal_0025"), R.at(v_groups__133438, v_i__133536)), v_room__133588, AuthoredCopy("m50.c2_inspections.literal_0021"), Authored("m50.c2_inspections.literal_0022"), Authored("m50.c2_inspections.literal_0023"), v_source__130268));
            v_i__133536 = R.add(v_i__133536, R.number("1"));
        }
        return v_result__130222;
    }
    public static object F_c2_duty_assist_rations_law() {
        return DvValueTemplate.Instantiate("template.c2_duty_assist_rations_law");
    }
    public static object F_c2_duty_assist_prop_law() {
        return DvValueTemplate.Instantiate("template.c2_duty_assist_prop_law");
    }
    public static object F_c2_duty_assist_medicine_law() {
        return DvValueTemplate.Instantiate("template.c2_duty_assist_medicine_law");
    }
    public static object F_c2_duty_comfort_law() {
        return DvValueTemplate.Instantiate("template.c2_duty_comfort_law");
    }
    public static object F_c2_hero_airlock_brace_law() {
        return DvValueTemplate.Instantiate("template.c2_hero_airlock_brace_law");
    }
    public static object F_c2_hero_valve_repair_law() {
        return DvValueTemplate.Instantiate("template.c2_hero_valve_repair_law");
    }
    public static object F_c2_hero_pulley_brace_law(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c2_hero_pulley_brace_law", v_行动者);
    }
    public static object F_c2_hero_timber_brace_law() {
        return DvValueTemplate.Instantiate("template.c2_hero_timber_brace_law");
    }
    public static object F_c2_moran_acute_collapse_law() {
        return DvValueTemplate.Instantiate("template.c2_moran_acute_collapse_law");
    }
    public static object F_c2_moran_sketch_outdoor_law() {
        return DvValueTemplate.Instantiate("template.c2_moran_sketch_outdoor_law");
    }
    public static object F_c2_bertrand_rescue_law() {
        return DvValueTemplate.Instantiate("template.c2_bertrand_rescue_law");
    }
    public static object F_c2_rescue_edges() {
        return DvValueTemplate.Instantiate("template.c2_rescue_edges");
    }

    public static object F_c2_care_patients() {
        return DvValueTemplate.Instantiate("template.c2_care_patients");
    }
    public static object F_c2_disease_weakness_law(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_disease_weakness_law", v_actor);
    }
    public static object F_c2_disease_impairment_law(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_disease_impairment_law", v_actor);
    }
    public static object F_c2_respiratory_failure_law(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_respiratory_failure_law", v_actor);
    }
    public static object F_c2_respiratory_terminal_law(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_respiratory_terminal_law", v_actor);
    }
    public static object F_c2_moran_fatal_settle_law() {
        return DvValueTemplate.Instantiate("template.c2_moran_fatal_settle_law");
    }
    public static object F_c2_bertrand_petrification_settle_law() {
        return DvValueTemplate.Instantiate("template.c2_bertrand_petrification_settle_law");
    }
    public static object F_c2_unconventional_drive_demon_law() {
        return DvValueTemplate.Instantiate("template.c2_unconventional_drive_demon_law");
    }
    public static object F_c2_unconventional_iron_wedge_law() {
        return DvValueTemplate.Instantiate("template.c2_unconventional_iron_wedge_law");
    }
    public static object F_c2_unconventional_false_alarm_law() {
        return DvValueTemplate.Instantiate("template.c2_unconventional_false_alarm_law");
    }
    public static object F_c2_elena_dead_hand_law() {
        return DvValueTemplate.Instantiate("template.c2_elena_dead_hand_law");
    }
    public static object F_c2_veronica_dead_hand_law() {
        return DvValueTemplate.Instantiate("template.c2_veronica_dead_hand_law");
    }
    public static object F_c2_bertrand_dead_hand_law() {
        return DvValueTemplate.Instantiate("template.c2_bertrand_dead_hand_law");
    }
    public static object F_c2_ruan_dead_hand_law() {
        return DvValueTemplate.Instantiate("template.c2_ruan_dead_hand_law");
    }
    public static object F_c2_detective_intercom_law() {
        return DvValueTemplate.Instantiate("template.c2_detective_intercom_law");
    }
    public static object F_c2_detective_ascension_law() {
        return DvValueTemplate.Instantiate("template.c2_detective_ascension_law");
    }
    public static object F_c2_all_companions_fallen() {
        return DvValueTemplate.Instantiate("template.c2_all_companions_fallen");
    }
    public static object F_c2_foolish_duty_actions(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_foolish_duty_actions", v_actor);
    }
    public static object F_c2_hero_substitutions(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_hero_substitutions", v_actor);
    }
    public static object F_c2_hero_actions(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_hero_actions", v_actor);
    }
    public static object F_c2_moran_outing_heard(object v_actor) {
        if (R.Bool((R.Bool(F_c2_received_requirement(v_actor, AuthoredCopy("m50.c2_moran_outing_heard.literal"))) ? (object)(true) : (object)(F_c2_received_requirement(v_actor, AuthoredCopy("m50.c2_moran_outing_heard.literal_0002"))))))
        {
            return true;
        }
        return (R.Bool(F_c2_received_requirement(v_actor, AuthoredCopy("m50.c2_moran_outing_heard.literal_0003"))) ? (object)(true) : (object)(F_c2_received_requirement(v_actor, AuthoredCopy("m50.c2_moran_outing_heard.literal_0004"))));
    }
    public static object F_c2_casualty_actions(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_casualty_actions", v_actor);
    }
    public static object F_c2_patient_contact(object v_actor,object v_patient) {
        return DvValueTemplate.Instantiate("template.c2_patient_contact", v_actor, v_patient);
    }
    public static object F_c2_take_anti_erosion_law() {
        return DvValueTemplate.Instantiate("template.c2_take_anti_erosion_law");
    }
    public static object F_c2_administer_anti_erosion_law(object v_patient) {
        return DvValueTemplate.Instantiate("template.c2_administer_anti_erosion_law", v_patient);
    }
    public static object F_c2_anti_erosion_actions(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_anti_erosion_actions", v_actor);
    }
    public static object F_c2_unconventional_actions(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_unconventional_actions", v_actor);
    }
    public static object F_c2_dead_hand_actions(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_dead_hand_actions", v_actor);
    }
    public static object F_c2_shura_actions(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_shura_actions", v_actor);
    }
    public static object F_c2_actor_actions(object v_actor) {
        object v_result__196415 = F_c2_inspections(v_actor);
        object v_edges__196455 = F_c2_edges();
        object v_i__196483 = R.number("0");
        while (R.Bool(R.lt(v_i__196483, R.count(v_edges__196455))))
        {
            object v_edge__196534 = R.at(v_edges__196455, v_i__196483);
            _ = R.push(v_result__196415, F_c2_move_action(v_actor, F_dv_get(v_edge__196534, "from"), F_dv_get(v_edge__196534, "to"), F_dv_get(v_edge__196534, "gate")));
            _ = R.push(v_result__196415, F_c2_move_action(v_actor, F_dv_get(v_edge__196534, "to"), F_dv_get(v_edge__196534, "from"), F_dv_get(v_edge__196534, "gate")));
            v_i__196483 = R.add(v_i__196483, R.number("1"));
        }
        object v_rows__196851 = new List<object> { F_c1n_manual_wait_actions(v_actor), F_c2_board_actions(v_actor), F_c2i_evening_actions(v_actor), F_c2i_record_actions(v_actor), F_c2_supply_actions(v_actor), F_c2_special_actions(v_actor), F_c2_corridor_actions(v_actor), F_c2i_actions(v_actor), F_c2_safety_actions(v_actor), F_c2m_actions(v_actor), F_c2_foolish_duty_actions(v_actor), F_c2_hero_actions(v_actor), F_c2_casualty_actions(v_actor), F_c2_anti_erosion_actions(v_actor), F_c2_unconventional_actions(v_actor), F_c2_dead_hand_actions(v_actor), F_c2_shura_actions(v_actor) };
        v_i__196483 = R.number("0");
        while (R.Bool(R.lt(v_i__196483, R.count(v_rows__196851))))
        {
            object v_j__197377 = R.number("0");
            while (R.Bool(R.lt(v_j__197377, R.count(R.at(v_rows__196851, v_i__196483)))))
            {
                _ = R.push(v_result__196415, R.at(R.at(v_rows__196851, v_i__196483), v_j__197377));
                v_j__197377 = R.add(v_j__197377, R.number("1"));
            }
            v_i__196483 = R.add(v_i__196483, R.number("1"));
        }
        object v_norms__197513 = F_c2_norms();
        v_i__196483 = R.number("0");
        while (R.Bool(R.lt(v_i__196483, R.count(v_norms__197513))))
        {
            object v_actions__197588 = F_c2_norm_actions(v_actor, R.at(v_norms__197513, v_i__196483));
            object v_j__197644 = R.number("0");
            while (R.Bool(R.lt(v_j__197644, R.count(v_actions__197588))))
            {
                _ = R.push(v_result__196415, R.at(v_actions__197588, v_j__197644));
                v_j__197644 = R.add(v_j__197644, R.number("1"));
            }
            v_i__196483 = R.add(v_i__196483, R.number("1"));
        }
        object v_observations__197780 = F_c2_compact_observations(v_actor);
        v_i__196483 = R.number("0");
        while (R.Bool(R.lt(v_i__196483, R.count(v_observations__197780))))
        {
            _ = R.push(v_result__196415, R.at(v_observations__197780, v_i__196483));
            v_i__196483 = R.add(v_i__196483, R.number("1"));
        }
        _ = R.push(v_result__196415, DvValueTemplate.Instantiate("template.inline.c2_actor_actions.757", v_actor));
        object v_prepared__198490 = new List<object> {  };
        v_i__196483 = R.number("0");
        while (R.Bool(R.lt(v_i__196483, R.count(v_result__196415))))
        {
            object v_action__198561 = F_dv_with_many(R.at(v_result__196415, v_i__196483), new List<object> { F_dv_pair("precondition", F_dv_all(new List<object> { F_dv_get(R.at(v_result__196415, v_i__196483), "precondition"), F_c2_can_act(v_actor) })), F_dv_pair("authority_precondition", F_dv_all(new List<object> { F_dv_get(R.at(v_result__196415, v_i__196483), "authority_precondition"), F_c2_can_act(v_actor) })) });
            if (R.Bool(R.not(R.eq(F_dv_find(R.at(v_result__196415, v_i__196483), "law", ""), ""))))
            {
                v_action__198561 = F_dv_with(v_action__198561, "predict_law", F_dv_get(v_action__198561, "law"));
            }
            _ = R.push(v_prepared__198490, v_action__198561);
            v_i__196483 = R.add(v_i__196483, R.number("1"));
        }
        _ = R.push(v_prepared__198490, F_c2_orientation_action(v_actor));
        return F_campaign_public_methods(v_prepared__198490);
    }
    public static object F_c2_orientation_grants(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_orientation_grants", v_actor);
    }
    public static object F_c2_orientation_action(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_orientation_action", v_actor);
    }
    public static object F_c2_compact_observations(object v_actor) {
        object v_clock__201697 = DvValueTemplate.Instantiate("template.inline.c2_compact_observations.758", v_actor);
        object v_places__202148 = AuthoredCopy("m50.c2_compact_observations.places");
        object v_conditions__202255 = new List<object> {  };
        object v_i__202280 = R.number("0");
        while (R.Bool(R.lt(v_i__202280, R.count(v_places__202148))))
        {
            _ = R.push(v_conditions__202255, F_campaign_at(v_actor, R.at(v_places__202148, v_i__202280)));
            v_i__202280 = R.add(v_i__202280, R.number("1"));
        }
        object v_visible__202407 = F_dv_not(F_dv_all(F_c2_negated_conditions(v_conditions__202255)));
        object v_fields__202476 = AuthoredCopy("m50.c2_compact_observations.fields");
        object v_weather__202583 = DvValueTemplate.Instantiate("template.inline.c2_compact_observations.759", v_actor, v_visible__202407);
        return new List<object> { DvValueTemplate.Instantiate("template.inline.c2_compact_observations.760", v_clock__201697, v_actor), F_dv_with_many(v_weather__202583, new List<object> { F_dv_pair("repeatable", true), F_dv_pair("presentation", F_c2_inspection_presentation(v_actor, Authored("m50.c2_world_objects.literal_0021"), v_fields__202476)) }) };
    }
    public static object F_c2_self_presentation(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_self_presentation", v_actor);
    }
    public static object F_c2_self_grants(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2_self_grants", v_actor);
    }

    public static object F_c2_install_actor_laws(object v_world,object v_actor) {
        object v_owned_adoptions__227035 = F_dv_clone(F_dv_get(v_world, "adoptions"));
        object v_mode__227088 = "enforce";
        if (R.Bool(R.eq(F_dv_get(v_world, "domain"), "mind")))
        {
            v_mode__227088 = "predict";
        }
        _ = F_c2_install_actor_laws_into(F_dv_get(v_world, "domain"), v_owned_adoptions__227035, v_actor, v_mode__227088);
        return F_dv_finish_owned_adoptions(v_world, v_owned_adoptions__227035);
    }
    public static object F_c2_inherit_finale_meal(object v_world,object v_actor) {
        object v_actual__227431 = F_dv_read(v_world, F_campaign_finale_eaten_ref(v_actor));
        if (R.Bool((R.Bool(R.not(F_dv_uncontested(v_actual__227431))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_actual__227431, "value"), true))))))
        {
            return v_world;
        }
        object v_prior_meal__227580 = F_dv_read(v_world, F_campaign_own(v_actor, Authored("m50.c2_inherit_finale_meal.literal")));
        object v_result__227713 = v_world;
        if (R.Bool((R.Bool(F_dv_uncontested(v_prior_meal__227580)) ? (object)(R.eq(F_dv_get(v_prior_meal__227580, "value"), false)) : (object)(false))))
        {
            object v_prior_count__227807 = F_dv_read(v_world, F_campaign_own(v_actor, Authored("m50.c2_route_inspections.literal_0010")));
            if (R.Bool((R.Bool(F_dv_uncontested(v_prior_count__227807)) ? (object)(R.is_numeric_value(F_dv_get(v_prior_count__227807, "value"))) : (object)(false))))
            {
                object v_count__228036 = DvValueTemplate.Instantiate("template.inline.c2_inherit_finale_meal.814", v_prior_count__227807, v_actual__227431);
                v_result__227713 = F_dv_store(v_result__227713, F_campaign_own(v_actor, Authored("m50.c2_route_inspections.literal_0010")), v_count__228036);
            }
        }
        v_result__227713 = F_dv_store(v_result__227713, F_campaign_own(v_actor, Authored("m50.c2_inherit_finale_meal.literal")), v_actual__227431);
        return F_dv_store(v_result__227713, F_campaign_own(v_actor, Authored("m50.c2_inherit_finale_meal.literal_0002")), v_actual__227431);
    }

    public static object F_c2_shared_objects() {
        return DvValueTemplate.Instantiate("template.c2_shared_objects");
    }
    public static object F_c2_query_view(object v_actor,object v_value) {
        return DvValueTemplate.Instantiate("template.c2_query_view", v_actor, v_value);
    }
    public static object F_c2_navigation_condition(object v_condition,object v_actor) {
        object v_op__236670 = F_dv_find(v_condition, Authored("m50.c2_navigation_condition.literal"), "");
        if (R.Bool((R.Bool(R.eq(v_op__236670, "equals")) ? (object)(F_dv_same_local_ref(v_actor, F_dv_get(v_condition, "ref"), F_campaign_position_ref(v_actor))) : (object)(false))))
        {
            return F_dv_true();
        }
        if (R.Bool(R.eq(v_op__236670, "all")))
        {
            object v_items__236932 = new List<object> {  };
            object v_i__236956 = R.number("0");
            while (R.Bool(R.lt(v_i__236956, R.count(F_dv_get(v_condition, "items")))))
            {
                _ = R.push(v_items__236932, F_c2_navigation_condition(R.at(F_dv_get(v_condition, "items"), v_i__236956), v_actor));
                v_i__236956 = R.add(v_i__236956, R.number("1"));
            }
            return F_dv_all(v_items__236932);
        }
        return v_condition;
    }
    public static object F_c2_navigation_pending(object v_actor,object v_goal) {
        object v_completion__237214 = F_dv_find(v_goal, Authored("m50.c2_navigation_pending.literal"), false);
        if (R.Bool((R.Bool(R.not(R.eq(v_completion__237214, false))) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), v_completion__237214), "true")) : (object)(false))))
        {
            return false;
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_navigation_pending.832", v_goal)))
        {
            return (R.Bool(F_dvp_evidence_due(v_actor, v_goal)) ? (object)(R.not(F_dvp_source_supported_since(F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_goal, "ref")), F_dv_find(v_goal, "source", Authored("m50.c2_navigation_pending.literal_0003")), F_dv_find(v_goal, Authored("m50.c2_navigation_pending.literal_0004"), R.number("0"))))) : (object)(false));
        }
        return F_campaign_future_goal_pending(v_actor, v_goal);
    }
    public static object F_c2_navigation_local_ready(object v_actor,object v_goal) {
        v_actor = F_c2_query_view(v_actor, v_actor);
        v_goal = F_c2_query_view(v_actor, v_goal);
        object v_methods__238272 = F_dv_find(v_goal, Authored("m50.c2_navigation_local_ready.literal"), new List<object> {  });
        if (R.Bool(F_dv_has_key(v_goal, "method_id")))
        {
            v_methods__238272 = F_dv_append(v_methods__238272, F_dv_get(v_goal, "method_id"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_navigation_local_ready.833", v_methods__238272, v_goal)))
        {
            object v_proposition__238631 = F_dv_find(v_goal, "proposition", new List<object> {  });
            if (R.Bool((R.Bool(R.eq(F_dv_find(v_proposition__238631, Authored("m50.c2_navigation_condition.literal"), ""), "equals")) ? (object)(F_dv_same_local_ref(F_dv_get(v_actor, "id"), F_dv_get(v_proposition__238631, "ref"), F_campaign_position_ref(F_dv_get(v_actor, "id")))) : (object)(false))))
            {
                return true;
            }
        }
        object v_relevant__238920 = F_dvp_goal_refs(v_actor);
        object v_refs__238961 = new List<object> {  };
        if (R.Bool(F_dv_has_key(v_goal, "ref")))
        {
            v_refs__238961 = F_dvp_add_ref(v_refs__238961, F_dv_get(v_goal, "ref"));
        }
        if (R.Bool(F_dv_has_key(v_goal, "proposition")))
        {
            v_refs__238961 = F_dvp_merge_refs(v_refs__238961, F_dvs_sim_refs(F_dv_get(v_goal, "proposition")));
        }
        object v_i__239167 = R.number("0");
        while (R.Bool(R.lt(v_i__239167, R.count(v_methods__238272))))
        {
            object v_action__239220 = F_dvp_find_action(F_dv_get(v_actor, "actions"), R.at(v_methods__238272, v_i__239167));
            if (R.Bool((R.Bool(R.not(R.eq(v_action__239220, false))) ? (object)(R.not(R.eq(F_dv_get(v_action__239220, "kind"), "move"))) : (object)(false))))
            {
                if (R.Bool((R.Bool(F_dvp_eligible(v_actor, v_action__239220, F_dv_get(v_actor, "mind"), v_relevant__238920)) ? (object)(R.not(R.lt(F_dvp_personal_motivation(v_actor, v_action__239220), R.number("0")))) : (object)(false))))
                {
                    return true;
                }
                v_refs__238961 = F_dvp_merge_refs(v_refs__238961, F_dvs_sim_refs(F_dv_get(v_action__239220, "precondition")));
            }
            v_i__239167 = R.add(v_i__239167, R.number("1"));
        }
        object v_origin__239730 = F_dv_find(v_actor, Authored("m50.c2_navigation_local_ready.literal_0003"), F_dv_get(v_actor, "mind"));
        v_i__239167 = R.number("0");
        while (R.Bool(R.lt(v_i__239167, R.count(F_dv_get(v_actor, "actions")))))
        {
            object v_action__239913 = R.at(F_dv_get(v_actor, "actions"), v_i__239167);
            if (R.Bool(R.eq(F_dv_get(v_action__239913, "kind"), "inspect")))
            {
                object v_grants__239994 = F_dv_find(v_action__239913, "grants", new List<object> {  });
                object v_pending__240050 = false;
                object v_r__240083 = R.number("0");
                while (R.Bool(R.lt(v_r__240083, R.count(v_grants__239994))))
                {
                    object v_ref__240151 = R.at(v_grants__239994, v_r__240083);
                    if (R.Bool((R.Bool((R.Bool(F_dv_has(v_refs__238961, v_ref__240151)) ? (object)(R.not(F_dv_same_local_ref(F_dv_get(v_actor, "id"), v_ref__240151, F_campaign_position_ref(F_dv_get(v_actor, "id"))))) : (object)(false))) ? (object)(R.not(F_dv_same(v_ref__240151, F_campaign_clock()))) : (object)(false))))
                    {
                        if (R.Bool((R.Bool(R.not(F_dv_uncontested(F_dv_read(v_origin__239730, v_ref__240151)))) ? (object)(true) : (object)(F_dvp_needs_evidence(v_actor, v_ref__240151)))))
                        {
                            v_pending__240050 = true;
                        }
                    }
                    v_r__240083 = R.add(v_r__240083, R.number("1"));
                }
                if (R.Bool((R.Bool(v_pending__240050) ? (object)(F_dvp_eligible(v_actor, v_action__239913, F_dv_get(v_actor, "mind"), v_relevant__238920)) : (object)(false))))
                {
                    return true;
                }
            }
            v_i__239167 = R.add(v_i__239167, R.number("1"));
        }
        return false;
    }
    public static object F_c2_navigation_route_ready(object v_actor,object v_goal,object v_route) {
        v_actor = F_c2_query_view(v_actor, v_actor);
        v_goal = F_c2_query_view(v_actor, v_goal);
        v_route = F_c2_query_view(v_actor, v_route);
        object v_view__240972 = v_actor;
        object v_i__240994 = R.number("0");
        while (R.Bool(R.lt(v_i__240994, R.count(F_dv_get(v_route, "path_basis")))))
        {
            object v_step__241056 = R.at(F_dv_get(v_route, "path_basis"), v_i__240994);
            object v_action__241096 = F_dvp_find_action(F_dv_get(v_view__240972, "actions"), F_dv_get(v_step__241056, "method"));
            if (R.Bool((R.Bool(R.eq(v_action__241096, false)) ? (object)(true) : (object)(R.not(F_dvp_eligible(v_view__240972, v_action__241096, F_dv_get(v_view__240972, "mind"), new List<object> {  }))))))
            {
                return false;
            }
            object v_prediction__241265 = F_dvp_forecast(F_dv_get(v_view__240972, "mind"), F_dv_get(v_action__241096, "predict_law"));
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_navigation_route_ready.834", v_prediction__241265)))
            {
                return false;
            }
            v_view__240972 = F_dvp_actor_view(v_view__240972, F_dv_get(v_prediction__241265, "world"));
            v_i__240994 = R.add(v_i__240994, R.number("1"));
        }
        if (R.Bool((R.Bool(F_dv_has_key(v_route, "requested_target")) ? (object)(R.not(R.eq(F_dv_get(v_route, "target"), F_dv_get(v_route, "requested_target")))) : (object)(false))))
        {
            return true;
        }
        return F_c2_navigation_local_ready(v_view__240972, v_goal);
    }
    public static object F_c2_navigation_arrival_goals(object v_actor,object v_goals) {
        object v_result__242064 = new List<object> {  };
        object v_i__242085 = R.number("0");
        while (R.Bool(R.lt(v_i__242085, R.count(v_goals))))
        {
            object v_goal__242136 = R.at(v_goals, v_i__242085);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_navigation_arrival_goals.835", v_goal__242136)))
            {
                object v_proposition__242401 = F_dv_find(v_goal__242136, "proposition", new List<object> {  });
                if (R.Bool((R.Bool(R.eq(F_dv_find(v_proposition__242401, Authored("m50.c2_navigation_condition.literal"), ""), "equals")) ? (object)(F_dv_same_local_ref(F_dv_get(v_actor, "id"), F_dv_get(v_proposition__242401, "ref"), F_campaign_position_ref(F_dv_get(v_actor, "id")))) : (object)(false))))
                {
                    v_goal__242136 = DvValueTemplate.Instantiate("template.inline.c2_navigation_arrival_goals.836", v_goal__242136);
                }
            }
            _ = R.push(v_result__242064, v_goal__242136);
            v_i__242085 = R.add(v_i__242085, R.number("1"));
        }
        return v_result__242064;
    }
    public static object F_c2_navigation_goals(object v_actor,object v_goals) => F_c2_navigation_goals(v_actor, v_goals, out _);
    public static object F_c2_navigation_goals(object v_actor,object v_goals, out object navigationDiagnostic) {
        v_actor = F_c2_query_view(v_actor, v_actor);
        v_goals = F_c2_query_view(v_actor, v_goals);
        v_goals = F_c2_navigation_arrival_goals(v_actor, v_goals);
        object v_closing__243030 = F_c2i_closing_navigation(v_actor, out navigationDiagnostic);
        if (R.Bool(R.not(R.eq(v_closing__243030, false))))
        {
            object v_when__243109 = F_campaign_any(F_dv_fact(F_campaign_ref(Authored("m50.c2_inspection_presentation.literal_0015"), Authored("m50.c2_inspection_presentation.literal_0025")), true), F_dv_at_least(F_campaign_clock(), Authored("m50.c2_navigation_goals.literal")));
            object priorCommitment = F_dv_find(v_actor, "navigation_commitment", false);
            if (!R.Bool(R.eq(priorCommitment, false)))
            {
                object priorTask = F_dv_get(priorCommitment, "task");
                object probeGoal = DvValueTemplate.Instantiate("template.inline.c2_navigation_goals.838", new List<object>(), v_closing__243030, F_dv_get(v_actor, "id"), v_when__243109);
                object probeTask = F_dv_get(probeGoal, "navigation_task");
                if (R.Bool(F_dv_same(F_dv_find(priorTask, "routing_family", false), F_dv_get(probeTask, "routing_family"))) &&
                    R.Bool(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(priorTask, "valid_when")), "true")) &&
                    R.Bool(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(priorTask, "complete_when")), "false")))
                {
                    object retainedRoute = F_c2i_known_route(v_actor, F_dv_get(priorTask, "target"), out object retainedDiagnostic);
                    navigationDiagnostic = F_dv_with(navigationDiagnostic, "retained_route", retainedDiagnostic);
                    if (!R.Bool(R.eq(retainedRoute, false)))
                    {
                        v_closing__243030 = retainedRoute;
                        navigationDiagnostic = F_dv_with_many(navigationDiagnostic, new List<object> { F_dv_pair("reason", "retained-route"), F_dv_pair("target", F_dv_get(retainedRoute, "target")), F_dv_pair("next", F_dv_get(retainedRoute, "next")) });
                    }
                }
            }
            object v_intended__243442 = F_campaign_goal(F_campaign_at(F_dv_get(v_actor, "id"), F_dv_get(v_closing__243030, "next")), F_dv_all(new List<object> { v_when__243109, F_campaign_at(F_dv_get(v_actor, "id"), F_dv_get(v_closing__243030, "origin")) }), DvValueTemplate.Instantiate("template.inline.c2_navigation_goals.837", v_closing__243030));
            return F_dv_append(v_goals, DvValueTemplate.Instantiate("template.inline.c2_navigation_goals.838", v_intended__243442, v_closing__243030, F_dv_get(v_actor, "id"), v_when__243109));
        }
        object v_held__244097 = R.number("0");
        object v_maintained_route__244115 = false;
        while (R.Bool(R.lt(v_held__244097, R.count(v_goals))))
        {
            object v_goal__244188 = R.at(v_goals, v_held__244097);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_navigation_goals.839", v_goal__244188)))
            {
                if (R.Bool(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(v_goal__244188, "available_when", F_dv_true())), "true")))
                {
                    if (R.Bool(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_goal__244188, "proposition")), "true")))
                    {
                        object v_room__244550 = F_dv_find(v_goal__244188, Authored("m50.c2_navigation_arrival_goals.literal"), "");
                        object v_intended__244680 = F_campaign_goal(F_campaign_at(F_dv_get(v_actor, "id"), v_room__244550), F_dv_get(v_goal__244188, "available_when"), F_dv_get(v_goal__244188, "basis"));
                        return F_dv_append(v_goals, DvValueTemplate.Instantiate("template.inline.c2_navigation_goals.840", v_intended__244680, v_room__244550));
                    }
                    object v_room__245047 = F_dv_find(v_goal__244188, Authored("m50.c2_navigation_arrival_goals.literal"), "");
                    if (R.Bool((R.Bool(R.not(R.eq(v_room__245047, ""))) ? (object)(R.eq(v_maintained_route__244115, false)) : (object)(false))))
                    {
                        object v_route__245239 = F_c2_navigation_route(v_actor, v_room__245047);
                        if (R.Bool(R.not(R.eq(v_route__245239, false))))
                        {
                            v_maintained_route__244115 = new List<object> { F_dv_pair("goal", v_goal__244188), F_dv_pair("route", v_route__245239) };
                        }
                    }
                }
            }
            v_held__244097 = R.add(v_held__244097, R.number("1"));
        }
        if (R.Bool(R.not(R.eq(v_maintained_route__244115, false))))
        {
            object v_route__245468 = F_dv_get(v_maintained_route__244115, "route");
            object v_goal__245512 = F_dv_get(v_maintained_route__244115, "goal");
            object v_intended__245554 = F_campaign_goal(F_campaign_at(F_dv_get(v_actor, "id"), F_dv_get(v_route__245468, "next")), F_dv_all(new List<object> { F_dv_find(v_goal__245512, "available_when", F_dv_true()), F_campaign_at(F_dv_get(v_actor, "id"), F_dv_get(v_route__245468, "origin")) }), F_dv_append(F_dv_get(v_goal__245512, "basis"), v_route__245468));
            return F_dv_append(v_goals, DvValueTemplate.Instantiate("template.inline.c2_navigation_goals.841", v_intended__245554, v_route__245468, v_goal__245512));
        }
        object v_first__246158 = false;
        bool navigationMemoEvaluated = false;
        bool navigationMemoEnabled = false;
        object? navigationMemoKey = null;
        object v_i__246181 = R.number("0");
        while (R.Bool(R.lt(v_i__246181, R.count(v_goals))))
        {
            object v_goal__246232 = R.at(v_goals, v_i__246181);
            object v_room__246261 = F_dv_find(v_goal__246232, Authored("m50.c2_navigation_arrival_goals.literal"), "");
            object v_basis__246379 = F_dv_find(v_goal__246232, "basis", new List<object> {  });
            object v_condition__246427 = F_dv_find(v_goal__246232, "available_when", F_dv_true());
            object v_when__246495 = false;
            // These templates read only this call's unchanged actor. Evaluate
            // lazily at their original first use, after all earlier returns.
            if (!navigationMemoEvaluated)
            {
                navigationMemoEnabled = R.Bool(DvValueTemplate.Instantiate("template.inline.c2_navigation_goals.842", v_actor));
                navigationMemoEvaluated = true;
            }
            if (navigationMemoEnabled)
            {
                object v_key__246644 = navigationMemoKey ??= R.snapshot(DvValueTemplate.Instantiate("template.inline.c2_navigation_goals.843", v_actor));
                object v_cached__246755 = R.memo_get(v_condition__246427, v_key__246644);
                if (R.Bool(R.at(v_cached__246755, R.number("0"))))
                {
                    v_when__246495 = R.at(v_cached__246755, R.number("1"));
                }
                else
                {
                    v_when__246495 = R.memo_put(v_condition__246427, v_key__246644, F_c2_navigation_condition(v_condition__246427, F_dv_get(v_actor, "id")));
                }
            }
            else
            {
                v_when__246495 = F_c2_navigation_condition(v_condition__246427, F_dv_get(v_actor, "id"));
            }
            if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.eq(v_room__246261, ""))) ? (object)(R.lt(R.number("0"), R.count(v_basis__246379))) : (object)(false))) ? (object)(F_c2_navigation_pending(v_actor, v_goal__246232)) : (object)(false))) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), v_when__246495), "true")) : (object)(false))))
            {
                object v_ready__247195 = R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), DvValueTemplate.Instantiate("template.inline.c2_navigation_goals.844", v_goal__246232)), "true");
                if (R.Bool(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_campaign_at(F_dv_get(v_actor, "id"), v_room__246261)), "true")))
                {
                    if (R.Bool((R.Bool(v_ready__247195) ? (object)(F_c2_navigation_local_ready(v_actor, v_goal__246232)) : (object)(false))))
                    {
                        object v_intended__247591 = F_campaign_goal(F_campaign_at(F_dv_get(v_actor, "id"), v_room__246261), F_dv_all(new List<object> { v_when__246495, F_campaign_at(F_dv_get(v_actor, "id"), v_room__246261) }), v_basis__246379);
                        return F_dv_append(v_goals, DvValueTemplate.Instantiate("template.inline.c2_navigation_goals.845", v_intended__247591, v_room__246261));
                    }
                }
                else
                {
                    if (R.Bool((R.Bool(v_ready__247195) ? (object)(R.eq(v_first__246158, false)) : (object)(false))))
                    {
                        object v_route__248021 = F_c2_navigation_route(v_actor, v_room__246261);
                        if (R.Bool((R.Bool(R.not(R.eq(v_route__248021, false))) ? (object)(F_c2_navigation_route_ready(v_actor, v_goal__246232, v_route__248021)) : (object)(false))))
                        {
                            object v_candidate__248173 = new List<object> { F_dv_pair("goal", v_goal__246232), F_dv_pair("route", v_route__248021), F_dv_pair("when", v_when__246495) };
                            if (R.Bool(R.eq(v_first__246158, false)))
                            {
                                v_first__246158 = v_candidate__248173;
                            }
                        }
                    }
                }
            }
            v_i__246181 = R.add(v_i__246181, R.number("1"));
        }
        if (R.Bool(R.not(R.eq(v_first__246158, false))))
        {
            object v_route__248369 = F_dv_get(v_first__246158, "route");
            object v_basis__248402 = F_dv_append(F_dv_get(F_dv_get(v_first__246158, "goal"), "basis"), v_route__248369);
            object v_intended__248458 = F_campaign_goal(F_campaign_at(F_dv_get(v_actor, "id"), F_dv_get(v_route__248369, "next")), F_dv_all(new List<object> { F_dv_get(v_first__246158, "when"), F_campaign_at(F_dv_get(v_actor, "id"), F_dv_get(v_route__248369, "origin")) }), v_basis__248402);
            return F_dv_append(v_goals, DvValueTemplate.Instantiate("template.inline.c2_navigation_goals.846", v_intended__248458, v_route__248369, F_dv_get(v_first__246158, "goal")));
        }
        return v_goals;
    }
    public static object F_c2_navigation_route(object v_actor,object v_target) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_navigation_route.847", v_actor)))
        {
            return F_c2_navigation_route_uncached(v_actor, v_target);
        }
        v_actor = R.snapshot(v_actor);
        object v_key__249368 = DvValueTemplate.Instantiate("template.inline.c2_navigation_route.848", v_actor, v_target);
        object v_cached__249558 = R.memo_get(F_dv_get(v_actor, "mind"), v_key__249368);
        if (R.Bool(R.at(v_cached__249558, R.number("0"))))
        {
            return R.at(v_cached__249558, R.number("1"));
        }
        return R.memo_put(F_dv_get(v_actor, "mind"), v_key__249368, F_c2_navigation_route_uncached(v_actor, v_target));
    }

    public static object F_c2_gate_visible(object v_actor,object v_gate) {
        return DvValueTemplate.Instantiate("template.c2_gate_visible", v_actor, v_gate);
    }
    public static object F_c2_negated_conditions(object v_conditions) {
        return DvValueTemplate.Instantiate("template.c2_negated_conditions", v_conditions);
    }
    public static object F_c2_personal_goal_templates_build(object v_id) {
        object v_source__253343 = new List<object> { F_campaign_person_source(v_id) };
        object v_goals__253390 = new List<object> { F_campaign_observation_goal(F_campaign_clock(), F_dv_true(), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.849"), R.number("0")) };
        object v_weather_fields__253742 = AuthoredCopy("m50.c2_personal_goal_templates_build.weather_fields");
        object v_weather_day__253874 = Authored("m50.c2_any_night.literal");
        while (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.850", v_weather_day__253874)))
        {
            object v_boundaries__254061 = AuthoredCopy("m50.c2_personal_goal_templates_build.boundaries");
            object v_phase__254189 = R.number("0");
            while (R.Bool(R.lt(v_phase__254189, R.count(v_boundaries__254061))))
            {
                object v_start__254261 = R.at(v_boundaries__254061, v_phase__254189);
                object v_end__254304 = Authored("m50.c2_night.literal");
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.851", v_start__254261)))
                {
                    v_end__254304 = Authored("m50.c2_norm_actions.literal_0028");
                }
                object v_field_index__254568 = R.number("0");
                while (R.Bool(R.lt(v_field_index__254568, R.count(v_weather_fields__253742))))
                {
                    _ = R.push(v_goals__253390, F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_world_objects.literal_0021"), R.at(v_weather_fields__253742, v_field_index__254568)), F_campaign_window(v_weather_day__253874, v_start__254261, v_end__254304), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.852"), R.add(R.mul(R.sub(v_weather_day__253874, R.number("1")), Authored("m50.c2_norm_actions.literal_0028")), v_start__254261)));
                    v_field_index__254568 = R.add(v_field_index__254568, R.number("1"));
                }
                v_phase__254189 = R.add(v_phase__254189, R.number("1"));
            }
            v_weather_day__253874 = R.add(v_weather_day__253874, R.number("1"));
        }
        object v_quake_field__255199 = R.number("0");
        while (R.Bool(R.lt(v_quake_field__255199, R.count(v_weather_fields__253742))))
        {
            _ = R.push(v_goals__253390, F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_world_objects.literal_0021"), R.at(v_weather_fields__253742, v_quake_field__255199)), F_campaign_window(Authored("m50.c2_any_night.literal_0003"), Authored("m50.c2_norm_available.literal"), Authored("m50.c2_night.literal")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.853"), Authored("m50.c2_personal_goal_templates_build.literal_0004")));
            v_quake_field__255199 = R.add(v_quake_field__255199, R.number("1"));
        }
        _ = R.push(v_goals__253390, F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_inspection_presentation.literal_0015"), Authored("m50.c2_inspection_presentation.literal_0025")), F_dv_at_least(F_campaign_clock(), Authored("m50.c2_navigation_goals.literal")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.854"), Authored("m50.c2_navigation_goals.literal")));
        _ = R.push(v_goals__253390, F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_inspection_presentation.literal_0015"), Authored("m50.c2_inspection_presentation.literal_0026")), F_dv_fact(F_campaign_ref(Authored("m50.c2_inspection_presentation.literal_0015"), Authored("m50.c2_inspection_presentation.literal_0025")), true), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.855"), R.number("0")));
        _ = R.push(v_goals__253390, F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_inspection_presentation.literal_0015"), Authored("m50.c2_personal_goal_templates_build.literal_0005")), F_dv_fact(F_campaign_ref(Authored("m50.c2_inspection_presentation.literal_0015"), Authored("m50.c2_inspection_presentation.literal_0025")), true), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.856"), Authored("m50.c2_navigation_goals.literal")));
        _ = R.push(v_goals__253390, F_campaign_observation_goal(F_campaign_clock(), F_dv_at_least(F_campaign_clock(), Authored("m50.c2_personal_goal_templates_build.literal_0006")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.857"), Authored("m50.c2_personal_goal_templates_build.literal_0006")));
        object v_gates__257963 = AuthoredCopy("m50.c2_personal_goal_templates_build.gates");
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.858", v_id)))
        {
            _ = R.push(v_goals__253390, F_dv_with_many(F_campaign_observation_goal(F_campaign_clock(), F_campaign_at(v_id, Authored("m50.c2_norms_build.literal_0048")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.859"), Authored("m50.c2_norm_actions.literal_0031")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.860")));
        }
        object v_gate_index__258849 = R.number("0");
        while (R.Bool(R.lt(v_gate_index__258849, R.count(v_gates__257963))))
        {
            _ = R.push(v_goals__253390, F_dv_with(F_campaign_observation_goal(F_campaign_access(R.at(v_gates__257963, v_gate_index__258849)), F_c2_gate_visible(v_id, R.at(v_gates__257963, v_gate_index__258849)), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.861"), R.number("0")), Authored("m50.c2_personal_goal_templates_build.literal_0009"), F_dv_fact(F_campaign_access(R.at(v_gates__257963, v_gate_index__258849)), true)));
            v_gate_index__258849 = R.add(v_gate_index__258849, R.number("1"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.862", v_id)))
        {
            _ = R.push(v_goals__253390, F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_world_objects.literal_0021"), Authored("m50.c2_weather_changes.literal_0009")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.863", v_id), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.864"), Authored("m50.c2_norm_actions.literal_0031")));
            _ = R.push(v_goals__253390, F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_world_objects.literal_0021"), "wind"), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.865", v_id), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.866"), Authored("m50.c2_norm_actions.literal_0031")));
            _ = R.push(v_goals__253390, F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_world_objects.literal_0021"), Authored("m50.c2_norm_actions.literal_0034")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.867", v_id), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.868"), Authored("m50.c2_norm_actions.literal_0031")));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.869", v_id)))
        {
            _ = R.push(v_goals__253390, F_dv_with_many(F_campaign_observation_goal(F_campaign_clock(), F_dv_true(), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.870"), Authored("m50.c2_norm_actions.literal_0031")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.871")));
            _ = R.push(v_goals__253390, F_campaign_observation_goal(F_campaign_position_ref(v_id), F_dv_true(), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.872"), R.number("0")));
            _ = R.push(v_goals__253390, F_dv_with(F_campaign_observation_goal(F_campaign_access(Authored("m50.c2_world_objects.literal_0032")), F_c2_gate_visible(v_id, Authored("m50.c2_world_objects.literal_0032")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.873"), R.number("0")), Authored("m50.c2_personal_goal_templates_build.literal_0009"), F_dv_fact(F_campaign_access(Authored("m50.c2_world_objects.literal_0032")), true)));
            _ = R.push(v_goals__253390, F_dv_with(F_campaign_observation_goal(F_campaign_access(Authored("m50.c2_corridor_origin.literal")), F_c2_gate_visible(v_id, Authored("m50.c2_corridor_origin.literal")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.874"), R.number("0")), Authored("m50.c2_personal_goal_templates_build.literal_0009"), F_dv_fact(F_campaign_access(Authored("m50.c2_corridor_origin.literal")), true)));
            return v_goals__253390;
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.875", v_id)))
        {
            object v_door_fields__263812 = AuthoredCopy("m50.c2_personal_goal_templates_build.door_fields");
            object v_door_index__263942 = R.number("0");
            while (R.Bool(R.lt(v_door_index__263942, R.count(v_door_fields__263812))))
            {
                _ = R.push(v_goals__253390, F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_personal_goal_templates_build.literal_0017"), R.at(v_door_fields__263812, v_door_index__263942)), F_campaign_at(v_id, Authored("m50.c2_personal_goal_templates_build.literal_0018")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.876"), R.number("0")));
                v_door_index__263942 = R.add(v_door_index__263942, R.number("1"));
            }
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.877", v_id)))
        {
            object v_indoor_gates__264750 = AuthoredCopy("m50.c2_world_objects.corridor_gates");
            object v_corridor_index__264868 = R.number("0");
            while (R.Bool(R.lt(v_corridor_index__264868, R.count(v_indoor_gates__264750))))
            {
                object v_gate__264960 = R.at(v_indoor_gates__264750, v_corridor_index__264868);
                _ = R.push(v_goals__253390, F_dv_with_many(F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_world_objects.literal_0021"), Authored("m50.c2_norm_actions.literal_0034")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.878", v_id, v_gate__264960), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.879"), Authored("m50.c2_norm_actions.literal_0031")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.880", v_gate__264960)));
                _ = R.push(v_goals__253390, F_campaign_observation_goal(F_campaign_ref(R.cat(Authored("m50.c2_weather_changes.literal_0002"), v_gate__264960), Authored("m50.c2_weather_changes.literal_0004")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.881", v_id, v_gate__264960), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.882"), Authored("m50.c2_norm_actions.literal_0031")));
                _ = R.push(v_goals__253390, F_campaign_goal(F_dv_fact(F_campaign_ref(R.cat(Authored("m50.c2_weather_changes.literal_0002"), v_gate__264960), Authored("m50.c2_weather_changes.literal_0004")), true), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.883", v_id, v_gate__264960), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.884")));
                _ = R.push(v_goals__253390, F_campaign_goal(F_dv_fact(F_campaign_access(v_gate__264960), true), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.885", v_id, v_gate__264960), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.886")));
                v_corridor_index__264868 = R.add(v_corridor_index__264868, R.number("1"));
            }
        }
        object v_norms__267645 = F_c2_norms();
        object v_i__267673 = R.number("0");
        while (R.Bool(R.lt(v_i__267673, R.count(v_norms__267645))))
        {
            object v_norm__267724 = R.at(v_norms__267645, v_i__267673);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.887", v_norm__267724, v_id)))
            {
                object normPostingGoal = F_dv_with_many(F_campaign_goal(F_dv_fact(F_campaign_ref(F_c2_norm_object(F_dv_get(v_norm__267724, "id")), Authored("m50.c2_norm_available.literal_0008")), true), F_dv_at_least(F_campaign_clock(), Authored("m50.c2_norm_condition.literal_0005")), new List<object> { F_dv_get(v_norm__267724, "source") }), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.888", v_norm__267724, v_id));
                _ = R.push(v_goals__253390, F_campaign_index_goal_window(DvValueTemplate.Instantiate("template.c2_norm_proposal_goal", v_id, v_norm__267724, normPostingGoal)));
                _ = R.push(v_goals__253390, normPostingGoal);
            }
            _ = R.push(v_goals__253390, F_campaign_observation_goal(F_campaign_ref(F_c2_norm_object(F_dv_get(v_norm__267724, "id")), Authored("m50.c2_norm_available.literal_0008")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.889", v_id, v_norm__267724), new List<object> { F_dv_get(v_norm__267724, "source") }, R.number("0")));
            _ = R.push(v_goals__253390, F_campaign_observation_goal(F_campaign_ref(F_c2_norm_object(F_dv_get(v_norm__267724, "id")), "text"), F_dv_fact(F_campaign_ref(F_c2_norm_object(F_dv_get(v_norm__267724, "id")), Authored("m50.c2_norm_available.literal_0008")), true), new List<object> { F_dv_get(v_norm__267724, "source") }, R.number("0")));
            _ = R.push(v_goals__253390, F_campaign_goal(F_dv_fact(F_campaign_own(v_id, R.cat(Authored("m50.c2_initial_body.literal_0013"), F_dv_get(v_norm__267724, "id"))), true), F_dv_fact(F_campaign_ref(F_c2_norm_object(F_dv_get(v_norm__267724, "id")), "text"), F_dv_get(v_norm__267724, "surface")), new List<object> { F_dv_get(v_norm__267724, "source") }));
            v_i__267673 = R.add(v_i__267673, R.number("1"));
        }
        object v_day__269321 = Authored("m50.c2_any_night.literal");
        while (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.890", v_day__269321)))
        {
            object v_meals__269492 = AuthoredCopy("m50.c2_supply_actions.meals");
            v_i__267673 = R.number("0");
            while (R.Bool(R.lt(v_i__267673, R.count(v_meals__269492))))
            {
                object v_meal__269650 = R.at(v_meals__269492, v_i__267673);
                object v_flag__269683 = R.cat(Authored("m50.c2_initial_body.literal_0017"), R.cat(R.show(v_day__269321), R.cat(Authored("m50.c2_move_action.literal_0002"), F_dv_get(v_meal__269650, "id"))));
                object v_meal_room__269898 = Authored("m50.c2_norms_build.literal_0034");
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.891", v_day__269321, v_meal__269650)))
                {
                    v_meal_room__269898 = Authored("m50.c2_norms_build.literal_0048");
                }
                object v_meal_ready__270275 = F_campaign_any(F_dv_at_least(F_campaign_own(v_id, Authored("m50.c2_route_inspections.literal_0006")), R.number("1")), F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0007"), Authored("m50.c2_route_inspections.literal_0002")), true));
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.892", v_meal_room__269898)))
                {
                    v_meal_ready__270275 = F_campaign_any(F_dv_at_least(F_campaign_own(v_id, Authored("m50.c2_route_inspections.literal_0006")), R.number("1")), F_dv_fact(F_c2daily_ref(Authored("m50.c2_personal_goal_templates_build.literal_0026")), true));
                }
                object v_suffix__270985 = R.cat(R.show(v_day__269321), R.cat(Authored("m50.c2_move_action.literal_0002"), F_dv_get(v_meal__269650, "id")));
                object v_meal_methods__271121 = DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.893", v_id, v_suffix__270985);
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.894", v_meal_room__269898)))
                {
                    _ = R.push(v_meal_methods__271121, R.cat(v_id, Authored("m50.c2_personal_goal_templates_build.literal_0027")));
                }
                _ = R.push(v_goals__253390, F_dv_with_many(F_campaign_goal(F_dv_fact(F_campaign_own(v_id, v_flag__269683), true), F_campaign_window(v_day__269321, F_dv_get(v_meal__269650, "start"), F_dv_get(v_meal__269650, "end")), v_source__253343), new List<object> { F_dv_pair("navigation_room", v_meal_room__269898), F_dv_pair("navigation_requirements", new List<object> { v_meal_ready__270275 }), F_dv_pair("navigation_methods", v_meal_methods__271121) }));
                v_i__267673 = R.add(v_i__267673, R.number("1"));
            }
            v_day__269321 = R.add(v_day__269321, R.number("1"));
        }
        _ = R.push(v_goals__253390, F_campaign_goal(F_dv_fact(F_campaign_own(v_id, Authored("m50.c2_norm_condition.literal_0004")), false), F_dv_fact(F_campaign_own(v_id, Authored("m50.c2_norm_condition.literal_0004")), true), v_source__253343));
        _ = R.push(v_goals__253390, F_campaign_goal(F_dv_fact(F_campaign_own(v_id, Authored("m50.c2_personal_goal_templates_build.literal_0028")), true), F_dv_at_least(F_campaign_clock(), Authored("m50.c2_personal_goal_templates_build.literal_0006")), v_source__253343));
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.895", v_id)))
        {
            _ = R.push(v_goals__253390, F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_world_objects.literal_0007"), Authored("m50.c2_route_inspections.literal_0003")), F_campaign_at(v_id, Authored("m50.c2_norms_build.literal_0034")), v_source__253343, R.number("0")));
            _ = R.push(v_goals__253390, F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_world_objects.literal_0009"), Authored("m50.c2_route_inspections.literal_0014")), F_campaign_at(v_id, Authored("m50.c2_norms_build.literal_0034")), v_source__253343, R.number("0")));
            object v_service_day__273206 = Authored("m50.c2_any_night.literal");
            while (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.896", v_service_day__273206)))
            {
                object v_active__273401 = F_campaign_any(F_campaign_window(v_service_day__273206, Authored("m50.c2_route_inspections.start"), Authored("m50.c2_route_inspections.end")), F_campaign_window(v_service_day__273206, Authored("m50.c2_route_inspections.literal_0008"), Authored("m50.c2_night.literal")));
                _ = R.push(v_goals__253390, F_dv_with_many(F_campaign_goal(F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0007"), Authored("m50.c2_route_inspections.literal_0002")), true), v_active__273401, DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.897")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.898", v_id)));
                v_service_day__273206 = R.add(v_service_day__273206, R.number("1"));
            }
        }
        _ = R.push(v_goals__253390, F_dv_with_many(F_campaign_goal(F_dv_fact(F_campaign_own(v_id, Authored("m50.c2_total_store_open_known.literal_0008")), R.number("0")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.899", v_id), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.900")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.901", v_id)));
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.902", v_id)))
        {
            object v_record_day__275870 = Authored("m50.c2_any_night.literal");
            while (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.903", v_record_day__275870)))
            {
                object v_periods__276063 = AuthoredCopy("m50.c2_personal_goal_templates_build.periods");
                object v_period_index__276189 = R.number("0");
                while (R.Bool(R.lt(v_period_index__276189, R.count(v_periods__276063))))
                {
                    object v_period__276280 = R.at(v_periods__276063, v_period_index__276189);
                    object v_available__276332 = DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.904", v_id, v_record_day__275870, v_period__276280);
                    _ = R.push(v_goals__253390, F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_world_objects.literal_0009"), Authored("m50.c2_total_store_open_known.literal_0012")), v_available__276332, DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.905"), R.add(R.mul(R.sub(v_record_day__275870, R.number("1")), Authored("m50.c2_norm_actions.literal_0028")), F_dv_get(v_period__276280, "start"))));
                    v_period_index__276189 = R.add(v_period_index__276189, R.number("1"));
                }
                v_record_day__275870 = R.add(v_record_day__275870, R.number("1"));
            }
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.906", v_id)))
        {
            _ = R.push(v_goals__253390, F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_world_objects.literal_0006"), Authored("m50.c2_total_store_open_known.literal_0024")), F_campaign_at(v_id, Authored("m50.c2_norms_build.literal_0034")), v_source__253343, R.number("0")));
            _ = R.push(v_goals__253390, F_campaign_goal(F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0006"), Authored("m50.c2_total_store_open_known.literal_0024")), true), F_campaign_at(v_id, Authored("m50.c2_norms_build.literal_0034")), v_source__253343));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.907", v_id)))
        {
            object v_patients__277956 = F_c2_care_patients();
            object v_patient_index__277999 = R.number("0");
            while (R.Bool(R.lt(v_patient_index__277999, R.count(v_patients__277956))))
            {
                object v_patient__278085 = R.at(v_patients__277956, v_patient_index__277999);
                object v_need__278136 = F_campaign_any(F_dv_fact(F_campaign_own(v_patient__278085, Authored("m50.c2_initial_body.literal_0002")), true), F_campaign_any(F_dv_fact(F_campaign_own(v_patient__278085, Authored("m50.c2_can_act.literal")), true), F_campaign_any(F_dv_fact(F_campaign_own(v_patient__278085, Authored("m50.c2_initial_body.literal_0010")), true), F_dv_fact(F_campaign_own(v_patient__278085, Authored("m50.c2_inspections.literal_0042")), true))));
                _ = R.push(v_goals__253390, F_campaign_goal(F_dv_fact(F_campaign_own(v_patient__278085, Authored("m50.c2_care_patients.literal_0002")), true), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.908", v_need__278136), v_source__253343));
                v_patient_index__277999 = R.add(v_patient_index__277999, R.number("1"));
            }
            _ = R.push(v_goals__253390, F_campaign_goal(F_dv_fact(F_campaign_own(Authored("m50.c2_initial_body.literal_0009"), Authored("m50.c2_norm_condition.literal_0004")), false), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.909"), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.910")));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.911", v_id)))
        {
            _ = R.push(v_goals__253390, F_campaign_goal(F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0018"), Authored("m50.c2_inspections.literal_0024")), true), F_campaign_at(v_id, Authored("m50.c2_norms_build.literal_0048")), v_source__253343));
            _ = R.push(v_goals__253390, F_campaign_goal(F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0018"), Authored("m50.c2_inspections.literal_0025")), true), F_campaign_at(v_id, Authored("m50.c2_norms_build.literal_0034")), v_source__253343));
            _ = R.push(v_goals__253390, F_campaign_goal(F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0018"), Authored("m50.c2_inspections.literal_0026")), true), F_campaign_at(v_id, Authored("m50.c2_world_objects.literal_0011")), v_source__253343));
            _ = R.push(v_goals__253390, F_campaign_goal(F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0018"), Authored("m50.c2_inspections.literal_0027")), true), F_campaign_at(v_id, Authored("m50.c2_norms_build.literal_0048")), v_source__253343));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.912", v_id)))
        {
            _ = R.push(v_goals__253390, F_campaign_goal(F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0004"), Authored("m50.c2_world_objects.literal_0034")), true), F_c2_night(Authored("m50.c2_any_night.literal_0003")), v_source__253343));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.913", v_id)))
        {
            _ = R.push(v_goals__253390, F_campaign_goal(F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0006"), Authored("m50.c2_inspections.literal_0030")), true), F_c2_night(Authored("m50.c2_any_night.literal_0002")), v_source__253343));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.914", v_id)))
        {
            _ = R.push(v_goals__253390, F_campaign_goal(F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0006"), Authored("m50.c2_inspections.literal_0032")), true), F_c2_night(Authored("m50.c2_any_night.literal_0003")), v_source__253343));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.915", v_id)))
        {
            _ = R.push(v_goals__253390, F_campaign_goal(F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0006"), Authored("m50.c2_inspections.literal_0034")), true), F_c2_night(Authored("m50.c2_any_night.literal_0003")), v_source__253343));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.916", v_id)))
        {
            object v_sketch_ready__282792 = DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.917");
            _ = R.push(v_goals__253390, F_dv_with_many(F_campaign_goal(F_dv_fact(F_campaign_own(v_id, Authored("m50.c2_inspections.literal_0040")), true), v_sketch_ready__282792, v_source__253343), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.918", v_id, v_sketch_ready__282792)));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.919", v_id)))
        {
            _ = R.push(v_goals__253390, F_campaign_goal(F_campaign_at(Authored("m50.c2_initial_body.literal_0009"), Authored("m50.c2_norms_build.literal_0048")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.920"), v_source__253343));
            _ = R.push(v_goals__253390, F_dv_with_many(F_campaign_goal(F_campaign_at(v_id, Authored("m50.c2_inspections.literal_0035")), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.921", v_id), v_source__253343), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.922", v_id)));
            _ = R.push(v_goals__253390, F_dv_with_many(F_campaign_goal(F_campaign_at(v_id, Authored("m50.c2_norms_build.literal_0048")), F_dv_fact(F_campaign_own(v_id, Authored("m50.c2_world_objects.literal_0031")), Authored("m50.c2_initial_body.literal_0009")), v_source__253343), DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates_build.923", v_id)));
        }
        return v_goals__253390;
    }
    public static object F_c2_personal_goal_templates(object v_id) {
        object v_key__286530 = DvValueTemplate.Instantiate("template.inline.c2_personal_goal_templates.924", v_id);
        object v_cached__286629 = R.memo_get(new List<object> {  }, v_key__286530);
        if (R.Bool(R.at(v_cached__286629, R.number("0"))))
        {
            return R.at(v_cached__286629, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__286530, F_c2_personal_goal_templates_build(v_id));
    }
    public static object F_c2_personal_goals(object v_actor) {
        v_actor = F_c2_query_view(v_actor, v_actor);
        object v_goals__286857 = F_dv_clone(F_c2_personal_goal_templates(F_dv_get(v_actor, "id")));
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goals.925", v_actor)))
        {
            object v_minimum__287248 = R.add(F_dv_find(F_dv_find(v_actor, Authored("m50.c2_personal_goals.literal_0002"), new List<object> {  }), "time", R.number("0")), R.number("1"));
            object v_refs__287389 = F_c2_orientation_grants(F_dv_get(v_actor, "id"));
            object v_orientation_index__287441 = R.number("0");
            while (R.Bool(R.lt(v_orientation_index__287441, R.count(v_refs__287389))))
            {
                _ = R.push(v_goals__286857, F_campaign_observation_goal(R.at(v_refs__287389, v_orientation_index__287441), F_dv_true(), DvValueTemplate.Instantiate("template.inline.c2_personal_goals.926"), v_minimum__287248));
                v_orientation_index__287441 = R.add(v_orientation_index__287441, R.number("1"));
            }
        }
        object v_board_when__287855 = DvValueTemplate.Instantiate("template.inline.c2_personal_goals.927", v_actor);
        _ = R.push(v_goals__286857, F_campaign_observation_goal(F_campaign_ref(F_c2_norm_object(Authored("m50.c2_norms_build.literal_0072")), Authored("m50.c2_norm_available.literal_0008")), v_board_when__287855, DvValueTemplate.Instantiate("template.inline.c2_personal_goals.928"), Authored("m50.c2_personal_goals.literal_0005")));
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goals.929", v_actor)))
        {
            if (R.Bool(F_c2_moran_outing_heard(v_actor)))
            {
                object v_relay__288599 = Authored("m50.c2_personal_goals.relay");
                object v_pending__288691 = R.not(F_dv_has(F_dv_find(v_actor, Authored("m50.c2_personal_goals.literal_0006"), new List<object> {  }), v_relay__288599));
                if (R.Bool(F_c2_received_requirement(v_actor, AuthoredCopy("m50.c2_moran_outing_heard.literal_0004"))))
                {
                    v_pending__288691 = R.not(F_dv_has(F_dv_find(v_actor, Authored("m50.c2_personal_goals.literal_0006"), new List<object> {  }), Authored("m50.c2_personal_goals.literal_0007")));
                }
                if (R.Bool(v_pending__288691))
                {
                    _ = R.push(v_goals__286857, F_dv_with(F_campaign_goal(F_campaign_at(F_dv_get(v_actor, "id"), Authored("m50.c2_world_objects.literal_0013")), F_c2_night(Authored("m50.c2_any_night.literal_0003")), DvValueTemplate.Instantiate("template.inline.c2_personal_goals.930")), Authored("m50.c2_navigation_arrival_goals.literal"), Authored("m50.c2_world_objects.literal_0013")));
                }
            }
            return v_goals__286857;
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_personal_goals.931", v_actor)))
        {
            object v_scientific_choice__289799 = AuthoredCopy("m50.c2_personal_goals.scientific_choice");
            if (R.Bool(R.lt(R.number("0"), F_dvp_personal_motivation(v_actor, v_scientific_choice__289799))))
            {
                object v_when__289997 = F_campaign_window(Authored("m50.c2_any_night.literal_0003"), Authored("m50.c2_norm_available.literal"), Authored("m50.c2_night.literal"));
                _ = R.push(v_goals__286857, F_dv_with_many(F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_world_objects.literal_0021"), Authored("m50.c2_norm_actions.literal_0037")), v_when__289997, DvValueTemplate.Instantiate("template.inline.c2_personal_goals.932"), Authored("m50.c2_personal_goal_templates_build.literal_0004")), AuthoredCopy("m50.c2_personal_goals.literal_0010")));
                object v_object__290750 = F_c2_norm_object(Authored("m50.c2_norms_build.literal_0072"));
                object v_ready__290863 = DvValueTemplate.Instantiate("template.inline.c2_personal_goals.933", v_when__289997);
                _ = R.push(v_goals__286857, F_dv_with_many(F_campaign_goal(F_dv_fact(F_campaign_ref(v_object__290750, Authored("m50.c2_norm_available.literal_0008")), true), v_ready__290863, DvValueTemplate.Instantiate("template.inline.c2_personal_goals.934")), AuthoredCopy("m50.c2_personal_goals.literal_0011")));
            }
        }
        object v_investigations__291558 = F_c2i_goals(v_actor);
        object v_investigation_index__291601 = R.number("0");
        while (R.Bool(R.lt(v_investigation_index__291601, R.count(v_investigations__291558))))
        {
            _ = R.push(v_goals__286857, R.at(v_investigations__291558, v_investigation_index__291601));
            v_investigation_index__291601 = R.add(v_investigation_index__291601, R.number("1"));
        }
        object v_safety__291809 = F_c2_safety_goals(v_actor);
        v_investigation_index__291601 = R.number("0");
        while (R.Bool(R.lt(v_investigation_index__291601, R.count(v_safety__291809))))
        {
            _ = R.push(v_goals__286857, R.at(v_safety__291809, v_investigation_index__291601));
            v_investigation_index__291601 = R.add(v_investigation_index__291601, R.number("1"));
        }
        return v_goals__286857;
    }
    public static object F_c2_refresh_actor_goals(object v_actor) {
        v_actor = F_c2g_refresh_inventory_review(v_actor);
        v_actor = DvValueTemplate.Instantiate("template.c2_coordination_observation_context", v_actor);
        object v_goals__292142 = F_c2_personal_goals(v_actor);
        object v_carried__292184 = F_dv_find(F_dv_get(v_actor, "policy"), Authored("m50.c2_refresh_actor_goals.literal"), new List<object> {  });
        object v_i__292304 = R.number("0");
        while (R.Bool(R.lt(v_i__292304, R.count(v_carried__292184))))
        {
            _ = R.push(v_goals__292142, R.at(v_carried__292184, v_i__292304));
            v_i__292304 = R.add(v_i__292304, R.number("1"));
        }
        return F_dv_with(v_actor, "policy", F_dv_with(F_dv_get(v_actor, "policy"), "goals", v_goals__292142));
    }

    public static object F_c2_install_teacher_laws(object v_world) {
        object v_owned_adoptions__293909 = F_dv_clone(F_dv_get(v_world, "adoptions"));
        object v_mode__293962 = "enforce";
        if (R.Bool(R.eq(F_dv_get(v_world, "domain"), "mind")))
        {
            v_mode__293962 = "predict";
        }
        _ = F_c2_install_teacher_laws_into(F_dv_get(v_world, "domain"), v_owned_adoptions__293909, v_mode__293962);
        return F_dv_finish_owned_adoptions(v_world, v_owned_adoptions__293909);
    }
    public static object F_c2_extend_teacher(object v_actor,object v_physical) {
        object v_mind__294221 = F_dv_get(v_actor, "mind");
        object v_objects__294248 = F_c2_shared_objects();
        object v_i__294287 = R.number("0");
        while (R.Bool(R.lt(v_i__294287, R.count(v_objects__294248))))
        {
            v_mind__294221 = F_campaign_add_new(v_mind__294221, F_campaign_local_object(R.at(v_objects__294248, v_i__294287)));
            v_i__294287 = R.add(v_i__294287, R.number("1"));
        }
        v_mind__294221 = F_c2_install_teacher_laws(v_mind__294221);
        object v_retired__294471 = R.eq(F_dv_truth(v_physical, F_campaign_finale_meal_complete()), "true");
        object v_actions__294552 = new List<object> {  };
        object v_carried__294574 = new List<object> {  };
        if (R.Bool(R.not(v_retired__294471)))
        {
            v_actions__294552 = F_dv_clone(F_dv_get(v_actor, "actions"));
            v_carried__294574 = F_dv_find(F_dv_get(v_actor, "policy"), "goals", new List<object> {  });
        }
        _ = R.push(v_actions__294552, DvValueTemplate.Instantiate("template.inline.c2_extend_teacher.937"));
        object v_routes__295475 = F_c2_route_inspections(F_dv_get(v_actor, "id"));
        v_i__294287 = R.number("0");
        while (R.Bool(R.lt(v_i__294287, R.count(v_routes__295475))))
        {
            _ = R.push(v_actions__294552, R.at(v_routes__295475, v_i__294287));
            v_i__294287 = R.add(v_i__294287, R.number("1"));
        }
        object v_edges__295624 = F_c2_edges();
        v_i__294287 = R.number("0");
        while (R.Bool(R.lt(v_i__294287, R.count(v_edges__295624))))
        {
            _ = R.push(v_actions__294552, F_c2_move_action(F_dv_get(v_actor, "id"), F_dv_get(R.at(v_edges__295624, v_i__294287), "from"), F_dv_get(R.at(v_edges__295624, v_i__294287), "to"), F_dv_get(R.at(v_edges__295624, v_i__294287), "gate")));
            _ = R.push(v_actions__294552, F_c2_move_action(F_dv_get(v_actor, "id"), F_dv_get(R.at(v_edges__295624, v_i__294287), "to"), F_dv_get(R.at(v_edges__295624, v_i__294287), "from"), F_dv_get(R.at(v_edges__295624, v_i__294287), "gate")));
            v_i__294287 = R.add(v_i__294287, R.number("1"));
        }
        object v_observations__295904 = F_c2_compact_observations(F_dv_get(v_actor, "id"));
        v_i__294287 = R.number("0");
        while (R.Bool(R.lt(v_i__294287, R.count(v_observations__295904))))
        {
            _ = R.push(v_actions__294552, R.at(v_observations__295904, v_i__294287));
            v_i__294287 = R.add(v_i__294287, R.number("1"));
        }
        object v_result__296074 = DvValueTemplate.Instantiate("template.inline.c2_extend_teacher.938", v_actor, v_mind__294221, v_actions__294552, v_carried__294574, v_retired__294471);
        return F_c2_refresh_actor_goals(v_result__296074);
    }
    public static object F_c2_extend_actor_from(object v_actor,object v_physical,object v_previous) {
        if (R.Bool(R.not(F_dv_has(F_campaign_people(),F_dv_get(v_actor,"id"))))) {
            if (R.Bool(R.eq(F_dv_get(v_actor,"id"),Authored("m50.c2_alive.literal")))) {
                return F_c2_extend_teacher(v_actor,v_physical);
            }
            return v_actor;
        }
        object v_mind__297855 = F_dv_get(v_actor,"mind");
        object v_objects__297882 = F_c2_shared_objects();
        object v_i__297921 = R.number("0");
        while (R.Bool(R.lt(v_i__297921,R.count(v_objects__297882)))) {
            if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_mind__297855,"objects"),R.at(v_objects__297882,v_i__297921))))) {
                v_mind__297855 = F_dv_add_object(v_mind__297855,F_campaign_local_object(R.at(v_objects__297882,v_i__297921)));
            }
            v_i__297921 = R.add(v_i__297921,R.number("1"));
        }
        object v_own__298360 = F_dv_get(F_dv_get(F_dv_get(F_dv_get(v_physical,"objects"),F_campaign_body(F_dv_get(v_actor,"id"))),"facets"),"state");
        v_i__297921 = R.number("0");
        while (R.Bool(R.lt(v_i__297921,R.count(v_own__298360)))) {
            object v_field__298483 = R.at(v_own__298360,v_i__297921);
            if (R.Bool(R.not(F_dv_get(F_dv_read(v_mind__297855,F_campaign_own(F_dv_get(v_actor,"id"),R.at(v_field__298483,R.number("0")))),"known")))) {
                v_mind__297855 = F_dv_store(v_mind__297855,F_campaign_own(F_dv_get(v_actor,"id"),R.at(v_field__298483,R.number("0"))),F_dv_with(R.at(v_field__298483,R.number("1")),"source",Authored("m50.c2_navigation_pending.literal_0003")));
            }
            v_i__297921 = R.add(v_i__297921,R.number("1"));
        }
        if (R.Bool((R.Bool(R.eq(F_dv_get(v_actor,"id"),Authored("m50.c2_initial_body.literal_0009"))) ? (object)(R.not(F_dv_get(F_dv_read(v_mind__297855,F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0009"),Authored("m50.c2_inspections.literal_0038"))),"known"))) : (object)(false)))) {
            v_mind__297855 = F_dv_store(v_mind__297855,F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0009"),Authored("m50.c2_inspections.literal_0038")),F_dv_with(F_dv_field(""),"basis",new System.Collections.Generic.List<object>(1) {F_campaign_approved(Authored("m50.c2_extend_actor_from.literal"))}));
        }
        object v_norms__299632 = F_c2_norms();
        v_i__297921 = R.number("0");
        while (R.Bool(R.lt(v_i__297921,R.count(v_norms__299632)))) {
            object v_norm__299707 = R.at(v_norms__299632,v_i__297921);
            if (R.Bool(R.eq(F_dv_get(v_norm__299707,"owner"),F_dv_get(v_actor,"id")))) {
                object v_object__299776 = F_c2_norm_object(F_dv_get(v_norm__299707,"id"));
                if (R.Bool(R.not(F_dv_get(F_dv_read(v_mind__297855,F_campaign_ref(v_object__299776,"owner")),"known")))) {
                    v_mind__297855 = F_dv_store(v_mind__297855,F_campaign_ref(v_object__299776,"owner"),F_dv_with(F_dv_field(F_dv_get(v_actor,"id")),"basis",new System.Collections.Generic.List<object>(1) {F_dv_get(v_norm__299707,"source")}));
                }
                if (R.Bool(R.not(F_dv_get(F_dv_read(v_mind__297855,F_campaign_ref(v_object__299776,"text")),"known")))) {
                    v_mind__297855 = F_dv_store(v_mind__297855,F_campaign_ref(v_object__299776,"text"),F_dv_with(F_dv_field(F_dv_get(v_norm__299707,"surface")),"basis",new System.Collections.Generic.List<object>(1) {F_dv_get(v_norm__299707,"source")}));
                }
                object v_unpublished__300218 = F_dv_all(new System.Collections.Generic.List<object>(2) {F_dv_fact(F_campaign_ref(v_object__299776,Authored("m50.c2_norm_available.literal_0007")),false),F_dv_fact(F_campaign_ref(v_object__299776,Authored("m50.c2_norm_available.literal_0008")),false)});
                if (R.Bool(R.eq(F_dv_truth(v_physical,v_unpublished__300218),"true"))) {
                    if (R.Bool(R.not(F_dv_get(F_dv_read(v_mind__297855,F_campaign_ref(v_object__299776,Authored("m50.c2_norm_available.literal_0007"))),"known")))) {
                        v_mind__297855 = F_dv_store(v_mind__297855,F_campaign_ref(v_object__299776,Authored("m50.c2_norm_available.literal_0007")),F_dv_with(F_dv_field(false),"basis",new System.Collections.Generic.List<object>(1) {F_dv_get(v_norm__299707,"source")}));
                    }
                    if (R.Bool(R.not(F_dv_get(F_dv_read(v_mind__297855,F_campaign_ref(v_object__299776,Authored("m50.c2_norm_available.literal_0008"))),"known")))) {
                        v_mind__297855 = F_dv_store(v_mind__297855,F_campaign_ref(v_object__299776,Authored("m50.c2_norm_available.literal_0008")),F_dv_with(F_dv_field(false),"basis",new System.Collections.Generic.List<object>(1) {F_dv_get(v_norm__299707,"source")}));
                    }
                }
            }
            v_i__297921 = R.add(v_i__297921,R.number("1"));
        }
        v_mind__297855 = F_c2_install_actor_laws(v_mind__297855,F_dv_get(v_actor,"id"));
        object v_profile__301268 = F_campaign_profile(F_dv_get(v_actor,"id"));
        object v_binding__301314 = new System.Collections.Generic.List<object>(4) {F_dv_pair("topic",Authored("m50.c2_extend_actor_from.literal_0002")),F_dv_pair("heard_when",F_dv_true()),F_dv_pair("completed_when",F_dv_not(F_dv_true())),F_dv_pair("basis",F_dv_get(v_profile__301268,"basis"))};
        object v_order__301507 = F_dv_clone(F_dv_find(F_dv_get(v_actor,"policy"),"value_order",new System.Collections.Generic.List<object>(0) {}));
        if (R.Bool(F_dv_has(AuthoredCopy("m50.c2_initial_body.literal_0004"),F_dv_get(v_actor,"id")))) {
            v_order__301507 = F_c2m_concat(F_dv_get(v_profile__301268,"value_order"),R.Block(() => { object comp_source_301733 = v_order__301507;var comp_result_301733 = new System.Collections.Generic.List<object>();for (int comp_i_301733 = 0; comp_i_301733 < R.Items(comp_source_301733).Count; comp_i_301733++) {object v_value__comp_301733 = R.at(comp_source_301733, (double)comp_i_301733);if (R.Bool(R.not(F_dv_has(F_dv_get(v_profile__301268,"value_order"),v_value__comp_301733)))) comp_result_301733.Add(v_value__comp_301733);}return (object)comp_result_301733;}));
        }
        v_i__297921 = R.number("0");
        while (R.Bool(R.lt(v_i__297921,R.count(F_dv_get(v_profile__301268,"value_order"))))) {
            if (R.Bool(R.not(F_dv_has(v_order__301507,R.at(F_dv_get(v_profile__301268,"value_order"),v_i__297921))))) {
                _ = R.push(v_order__301507,R.at(F_dv_get(v_profile__301268,"value_order"),v_i__297921));
            }
            v_i__297921 = R.add(v_i__297921,R.number("1"));
        }
        object v_value_basis__301992 = F_dv_clone(F_dv_find(F_dv_get(v_actor,"policy"),Authored("m50.c2_extend_actor_from.literal_0003"),new System.Collections.Generic.List<object>(0) {}));
        v_i__297921 = R.number("0");
        while (R.Bool(R.lt(v_i__297921,R.count(F_dv_get(v_profile__301268,"basis"))))) {
            _ = R.push(v_value_basis__301992,R.at(F_dv_get(v_profile__301268,"basis"),v_i__297921));
            v_i__297921 = R.add(v_i__297921,R.number("1"));
        }
        object v_norm_rules__302247 = F_dv_clone(F_dv_find(F_dv_get(v_actor,"policy"),Authored("m50.c2_extend_actor_from.literal_0004"),new System.Collections.Generic.List<object>(0) {}));
        object v_extra_rules__302383 = F_c2_personal_norms(F_dv_get(v_actor,"id"));
        v_i__297921 = R.number("0");
        while (R.Bool(R.lt(v_i__297921,R.count(v_extra_rules__302383)))) {
            object v_found__302487 = false;
            object v_rule_index__302514 = R.number("0");
            while (R.Bool(R.lt(v_rule_index__302514,R.count(v_norm_rules__302247)))) {
                if (R.Bool(R.eq(F_dv_get(R.at(v_norm_rules__302247,v_rule_index__302514),"id"),F_dv_get(R.at(v_extra_rules__302383,v_i__297921),"id")))) {
                    v_found__302487 = true;
                }
                v_rule_index__302514 = R.add(v_rule_index__302514,R.number("1"));
            }
            if (R.Bool(R.not(v_found__302487))) {
                _ = R.push(v_norm_rules__302247,R.at(v_extra_rules__302383,v_i__297921));
            }
            v_i__297921 = R.add(v_i__297921,R.number("1"));
        }
        object v_policy__302814 = F_dv_with_many(F_dv_get(v_actor,"policy"),new System.Collections.Generic.List<object>(6) {F_dv_pair("topic_bindings",F_dv_append(F_dv_find(F_dv_get(v_actor,"policy"),Authored("m50.c2_extend_actor_from.literal_0005"),new System.Collections.Generic.List<object>(0) {}),v_binding__301314)),F_dv_pair("value_order",v_order__301507),F_dv_pair("value_basis",v_value_basis__301992),F_dv_pair("chapter2_profile",v_profile__301268),F_dv_pair("norm_rules",v_norm_rules__302247),F_dv_pair("visual_identity_when",F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0021"),Authored("m50.c2_weather_changes.literal_0009")),true))});
        v_policy__302814 = F_dv_with_many(v_policy__302814,AuthoredCopy("m50.c2_extend_actor_from.literal_0006"));
        object v_actions__303440 = F_c2_actor_actions(F_dv_get(v_actor,"id"));
        object v_retired__303486 = R.eq(F_dv_truth(v_physical,F_dv_fact(F_campaign_finale_eaten_ref(F_dv_get(v_actor,"id")),true)),"true");
        v_policy__302814 = F_dv_with(v_policy__302814,Authored("m50.c2_extend_actor_from.literal_0007"),v_retired__303486);
        if (R.Bool(R.not(v_retired__303486))) {
            v_i__297921 = R.number("0");
            while (R.Bool(R.lt(v_i__297921,R.count(F_dv_get(v_actor,"actions"))))) {
                _ = R.push(v_actions__303440,R.at(F_dv_get(v_actor,"actions"),v_i__297921));
                v_i__297921 = R.add(v_i__297921,R.number("1"));
            }
            v_policy__302814 = F_dv_with(v_policy__302814,Authored("m50.c2_refresh_actor_goals.literal"),F_dv_find(F_dv_get(v_actor,"policy"),"goals",new System.Collections.Generic.List<object>(0) {}));
        } else {
            v_policy__302814 = F_dv_with(v_policy__302814,Authored("m50.c2_refresh_actor_goals.literal"),new System.Collections.Generic.List<object>(0) {});
        }
        object v_erosion__304277 = F_cognitive_erosion(F_campaign_own(F_dv_get(v_actor,"id"),Authored("m50.c2_inspections.literal_0036")),F_c2i_erosion_parameters(F_dv_get(v_actor,"id")),R.number("1"),new System.Collections.Generic.List<object>(1) {F_campaign_approved(Authored("m50.c2_extend_actor_from.literal_0008"))});
        v_erosion__304277 = F_dv_with(v_erosion__304277,Authored("m50.c2_extend_actor_from.literal_0009"),F_campaign_any(F_dv_fact(F_campaign_own(F_dv_get(v_actor,"id"),Authored("m50.c2_alive.literal_0002")),false),F_campaign_any(F_dv_fact(F_campaign_own(F_dv_get(v_actor,"id"),Authored("m50.c2_can_act.literal")),true),F_dv_fact(F_campaign_own(F_dv_get(v_actor,"id"),Authored("m50.c2_can_act.literal_0002")),true))));
        object v_result__304999 = F_dv_with_many(v_actor,new System.Collections.Generic.List<object>(4) {F_dv_pair("mind",v_mind__297855),F_dv_pair("actions",v_actions__303440),F_dv_pair("policy",v_policy__302814),F_dv_pair("erosion",v_erosion__304277)});
        return F_c2_refresh_actor_goals(v_result__304999);
    }
    public static object F_c2_extend_scene(object v_scene) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_extend_scene.939", v_scene)))
        {
            return v_scene;
        }
        object v_physical__305305 = F_c2_extend_world(F_dv_get(v_scene, "physical"));
        object v_actors__305357 = new List<object> {  };
        object v_bindings__305378 = F_dv_clone(F_dv_get(v_scene, "bindings"));
        object v_objects__305423 = F_c2_shared_objects();
        object v_i__305462 = R.number("0");
        while (R.Bool(R.lt(v_i__305462, R.count(F_dv_get(v_scene, "actors")))))
        {
            object v_actor__305520 = R.at(F_dv_get(v_scene, "actors"), v_i__305462);
            _ = R.push(v_actors__305357, F_c2_extend_actor_from(v_actor__305520, v_physical__305305, F_dv_get(v_scene, "physical")));
            object v_j__305637 = R.number("0");
            while (R.Bool(R.lt(v_j__305637, R.count(v_objects__305423))))
            {
                if (R.Bool(R.eq(F_dv_local(v_bindings__305378, R.at(v_objects__305423, v_j__305637), F_dv_get(v_actor__305520, "id")), "")))
                {
                    _ = R.push(v_bindings__305378, F_dv_binding(R.at(v_objects__305423, v_j__305637), R.at(v_objects__305423, v_j__305637), F_dv_get(v_actor__305520, "id")));
                }
                v_j__305637 = R.add(v_j__305637, R.number("1"));
            }
            v_i__305462 = R.add(v_i__305462, R.number("1"));
        }
        object v_processes__305877 = F_dv_clone(F_dv_find(v_scene, Authored("m50.c2_extend_scene.literal_0002"), new List<object> {  }));
        object v_extra__306000 = F_c2_world_processes();
        v_i__305462 = R.number("0");
        while (R.Bool(R.lt(v_i__305462, R.count(v_extra__306000))))
        {
            _ = R.push(v_processes__305877, R.at(v_extra__306000, v_i__305462));
            v_i__305462 = R.add(v_i__305462, R.number("1"));
        }
        return DvValueTemplate.Instantiate("template.inline.c2_extend_scene.940", v_scene, v_physical__305305, v_actors__305357, v_bindings__305378, v_processes__305877);
    }
    public static object F_c2_world_processes() {
        object v_result__306484 = DvValueTemplate.Instantiate("template.inline.c2_world_processes.941");
        object v_changes__307327 = F_c2_weather_changes();
        object v_change_index__307367 = R.number("0");
        while (R.Bool(R.lt(v_change_index__307367, R.count(v_changes__307327))))
        {
            object v_id__307442 = R.cat(Authored("m50.c2_extend_world.literal_0003"), F_dv_get(R.at(v_changes__307327, v_change_index__307367), "id"));
            _ = R.push(v_result__306484, DvValueTemplate.Instantiate("template.inline.c2_world_processes.942", v_id__307442));
            v_change_index__307367 = R.add(v_change_index__307367, R.number("1"));
        }
        object v_i__307768 = R.number("0");
        object v_light_day__307783 = R.number("1");
        while (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_world_processes.943", v_light_day__307783)))
        {
            _ = R.push(v_result__306484, DvValueTemplate.Instantiate("template.inline.c2_world_processes.944", v_light_day__307783));
            _ = R.push(v_result__306484, DvValueTemplate.Instantiate("template.inline.c2_world_processes.945", v_light_day__307783));
            v_light_day__307783 = R.add(v_light_day__307783, R.number("1"));
        }
        object v_people__308930 = F_campaign_people();
        v_i__307768 = R.number("0");
        while (R.Bool(R.lt(v_i__307768, R.count(v_people__308930))))
        {
            object v_day__309014 = Authored("m50.c2_any_night.literal");
            while (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_world_processes.946", v_day__309014)))
            {
                object v_prefix__309193 = R.cat(Authored("m50.c2_extend_world.literal_0021"), R.cat(R.at(v_people__308930, v_i__307768), R.cat(Authored("m50.c2_move_action.literal_0002"), R.show(v_day__309014))));
                _ = R.push(v_result__306484, DvValueTemplate.Instantiate("template.inline.c2_world_processes.947", v_prefix__309193));
                _ = R.push(v_result__306484, DvValueTemplate.Instantiate("template.inline.c2_world_processes.948", v_prefix__309193));
                v_day__309014 = R.add(v_day__309014, R.number("1"));
            }
            object v_danger__310113 = R.cat(Authored("m50.c2_extend_world.literal_0014"), R.at(v_people__308930, v_i__307768));
            _ = R.push(v_result__306484, DvValueTemplate.Instantiate("template.inline.c2_world_processes.949", v_danger__310113));
            object v_pathology__310385 = AuthoredCopy("m50.c2_world_processes.pathology");
            object v_p__310497 = R.number("0");
            while (R.Bool(R.lt(v_p__310497, R.count(v_pathology__310385))))
            {
                object v_id__310560 = R.cat(Authored("m50.c2_extend_world.literal_0020"), R.cat(R.at(v_people__308930, v_i__307768), R.cat(Authored("m50.c2_move_action.literal_0002"), R.at(v_pathology__310385, v_p__310497))));
                _ = R.push(v_result__306484, DvValueTemplate.Instantiate("template.inline.c2_world_processes.950", v_id__310560));
                v_p__310497 = R.add(v_p__310497, R.number("1"));
            }
            object v_terminal__310953 = R.cat(Authored("m50.c2_extend_world.literal_0023"), R.at(v_people__308930, v_i__307768));
            _ = R.push(v_result__306484, DvValueTemplate.Instantiate("template.inline.c2_world_processes.951", v_terminal__310953));
            v_i__307768 = R.add(v_i__307768, R.number("1"));
        }
        _ = R.push(v_result__306484, DvValueTemplate.Instantiate("template.inline.c2_world_processes.952"));
        _ = R.push(v_result__306484, DvValueTemplate.Instantiate("template.inline.c2_world_processes.953"));
        _ = R.push(v_result__306484, DvValueTemplate.Instantiate("template.inline.c2_world_processes.954"));
        _ = R.push(v_result__306484, DvValueTemplate.Instantiate("template.inline.c2_world_processes.955"));
        return v_result__306484;
    }
    public static object F_c2_runtime_contracts() {
        return DvValueTemplate.Instantiate("template.c2_runtime_contracts");
    }
    public static object F_c2_next_chapter_contract(object v_scene) {
        return DvValueTemplate.Instantiate("template.c2_next_chapter_contract", v_scene);
    }
    public static object F_c2i_row(object v_actor,object v_id,object v_room,object v_when,object v_object,object v_field,object v_requirements,object v_label,object v_text) {
        return DvValueTemplate.Instantiate("template.c2i_row", v_actor, v_id, v_room, v_when, v_object, v_field, v_requirements, v_label, v_text);
    }
    public static object F_c2i_granary_team() {
        return DvValueTemplate.Instantiate("template.c2i_granary_team");
    }
    public static object F_c2i_granary_support() {
        return DvValueTemplate.Instantiate("template.c2i_granary_support");
    }
    public static object F_c2i_rows() {
        object v_key__316442 = AuthoredCopy("m50.c2i_rows.key");
        object v_cached__316528 = R.memo_get(new List<object> {  }, v_key__316442);
        if (R.Bool(R.at(v_cached__316528, R.number("0"))))
        {
            return R.at(v_cached__316528, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__316442, F_c2i_rows_build());
    }
    public static object F_c2i_rows_build() {
        return DvValueTemplate.Instantiate("template.c2i_rows_build");
    }
    public static object F_c2i_value(object v_row) {
        return DvValueTemplate.Instantiate("template.c2i_value", v_row);
    }
    public static object F_c2sf_enabled(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2sf_enabled", v_actor);
    }
    public static object F_c2sf_case_row(object v_row) {
        return DvValueTemplate.Instantiate("template.c2sf_case_row", v_row);
    }
    public static object F_c2sf_row(object v_row) {
        return DvValueTemplate.Instantiate("template.c2sf_row", v_row);
    }
    public static object F_c2sf_actor(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2sf_actor", v_actor);
    }
    public static object F_c2sf_ceremony_ids() {
        object v_key__346542 = AuthoredCopy("m50.c2sf_ceremony_ids.key");
        object v_cached__346637 = R.memo_get(new List<object> {  }, v_key__346542);
        if (R.Bool(R.at(v_cached__346637, R.number("0"))))
        {
            return R.at(v_cached__346637, R.number("1"));
        }
        object v_ids__346721 = new List<object> {  };
        object v_rows__346739 = F_c2i_rows();
        object v_i__346766 = R.number("0");
        while (R.Bool(R.lt(v_i__346766, R.count(v_rows__346739))))
        {
            if (R.Bool(F_c2sf_case_row(R.at(v_rows__346739, v_i__346766))))
            {
                v_ids__346721 = DvValueTemplate.Instantiate("template.inline.c2sf_ceremony_ids.956", v_ids__346721, v_rows__346739, v_i__346766);
                v_ids__346721 = DvValueTemplate.Instantiate("template.inline.c2sf_ceremony_ids.957", v_ids__346721, v_rows__346739, v_i__346766);
            }
            v_i__346766 = R.add(v_i__346766, R.number("1"));
        }
        object v_hosts__347128 = AuthoredCopy("m50.c2sf_ceremony_ids.hosts");
        object v_guests__347227 = AuthoredCopy("m50.c2sf_ceremony_ids.guests");
        object v_rooms__347328 = AuthoredCopy("m50.c2sf_ceremony_ids.rooms");
        v_i__346766 = R.number("0");
        while (R.Bool(R.lt(v_i__346766, R.count(v_hosts__347128))))
        {
            object v_j__347474 = R.number("0");
            while (R.Bool(R.lt(v_j__347474, R.count(v_guests__347227))))
            {
                object v_k__347534 = R.number("0");
                while (R.Bool(R.lt(v_k__347534, R.count(v_rooms__347328))))
                {
                    v_ids__346721 = DvValueTemplate.Instantiate("template.inline.c2sf_ceremony_ids.958", v_ids__346721, v_hosts__347128, v_i__346766, v_guests__347227, v_j__347474, v_rooms__347328, v_k__347534);
                    v_k__347534 = R.add(v_k__347534, R.number("1"));
                }
                v_j__347474 = R.add(v_j__347474, R.number("1"));
            }
            v_i__346766 = R.add(v_i__346766, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__346542, v_ids__346721);
    }
    // Only membership queries borrow this declaration. The public entry point
    // above keeps its writable-copy contract and the same content-scoped memo.
    private static object ReadC2StoryflowCeremonyIds() {
        object key = AuthoredCopy("m50.c2sf_ceremony_ids.key");
        object cached = R.memo_read(new List<object> { }, key);
        if (R.Bool(R.at(cached, R.number("0"))))
        {
            return R.at(cached, R.number("1"));
        }
        return R.snapshot(F_c2sf_ceremony_ids());
    }
    public static object F_c2sf_action(object v_actor,object v_action) {
        if (R.Bool(R.not(F_c2sf_enabled(v_actor))))
        {
            return v_action;
        }
        return ApplyC2StoryflowAction(v_actor, v_action, ReadC2StoryflowCeremonyIds());
    }
    private static object ApplyC2StoryflowAction(object v_actor, object v_action, object ceremonyIds) {
        if (R.Bool(F_dv_has_key(ceremonyIds, F_dv_get(v_action, "id"))))
        {
            return F_dv_with(v_action, "precondition", F_dv_not(F_dv_true()));
        }
        if (R.Bool(R.not(F_dv_has_key(v_action, "storyflow_case_row"))))
        {
            return v_action;
        }
        object v_row__348234 = F_c2sf_row(F_dv_get(v_action, "storyflow_case_row"));
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2sf_action.959", v_row__348234)))
        {
            return F_dv_with(v_action, "precondition", F_dv_not(F_dv_true()));
        }
        object v_owned_clearance__348445 = F_dv_true();
        object v_sacrifices__348482 = new List<object> {  };
        object v_basis__348507 = DvValueTemplate.Instantiate("template.inline.c2sf_action.960");
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2sf_action.961", v_actor, v_action)))
        {
            v_basis__348507 = F_c2_cooperation_basis(v_actor, v_basis__348507);
            if (R.Bool(F_c2_quer_concerned(v_actor)))
            {
                v_owned_clearance__348445 = F_c2_work_accepted(F_dv_get(v_actor, "id"), F_dv_get(v_row__348234, "id"));
                v_sacrifices__348482 = AuthoredCopy("m50.c2_norms_build.literal_0029");
            }
        }
        object v_pre__349012 = F_dv_all(new List<object> { F_c2i_physical_premise(v_row__348234), F_c2_can_act(F_dv_get(v_actor, "id")), F_dv_fact(F_campaign_ref(F_dv_get(v_row__348234, "object"), F_dv_get(v_row__348234, "field")), false), v_owned_clearance__348445 });
        object v_promotes__349155 = AuthoredCopy("m50.c2sf_action.promotes");
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2sf_action.962", v_row__348234)))
        {
            v_promotes__349155 = AuthoredCopy("m50.c2sf_action.literal_0002");
        }
        object v_result__349463 = F_dv_with_many(v_action, new List<object> { F_dv_pair("precondition", v_pre__349012), F_dv_pair("authority_precondition", F_c2i_physical_premise(v_row__348234)), F_dv_pair("personal_choice", new List<object> { F_dv_pair("group", F_dv_get(v_row__348234, "id")), F_dv_pair("requires", v_pre__349012), F_dv_pair("prohibits", F_dv_fact(F_campaign_ref(F_dv_get(v_row__348234, "object"), F_dv_get(v_row__348234, "field")), true)), F_dv_pair("promotes", v_promotes__349155), F_dv_pair("sacrifices", v_sacrifices__348482), F_dv_pair("require_positive", true), F_dv_pair("basis", v_basis__348507) }) });
        v_result__349463 = F_dv_without(v_result__349463, "received_speech_requirement");
        return v_result__349463;
    }
    public static object F_c2i_premise(object v_row) {
        return DvValueTemplate.Instantiate("template.c2i_premise", v_row);
    }
    public static object F_c2i_physical_premise(object v_row) {
        return DvValueTemplate.Instantiate("template.c2i_physical_premise", v_row);
    }
    public static object F_c2i_contact_law(object v_row) {
        return DvValueTemplate.Instantiate("template.c2i_contact_law", v_row);
    }
    public static object F_c2i_world_objects(object v_world) {
        object v_result__355078 = v_world;
        v_result__355078 = F_campaign_add_new(v_result__355078, F_c2_object(Authored("m50.c2_norm_condition.literal"), Authored("m50.c2_world_objects.literal_0033"), DvValueTemplate.Instantiate("template.inline.c2i_world_objects.963")));
        object v_cases__355460 = AuthoredCopy("m50.c2i_world_objects.cases");
        object v_i__355559 = R.number("0");
        while (R.Bool(R.lt(v_i__355559, R.count(v_cases__355460))))
        {
            v_result__355078 = F_campaign_add_new(v_result__355078, F_c2_object(R.at(v_cases__355460, v_i__355559), "", new List<object> {  }));
            v_i__355559 = R.add(v_i__355559, R.number("1"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_world_objects.964", v_result__355078)))
        {
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_world_objects.965", v_world)))
            {
                v_result__355078 = F_dv_store(v_result__355078, F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0008"), Authored("m50.c2i_rows_build.literal_0061")), F_dv_field(Authored("m50.c2_norms_build.literal_0013")));
            }
            else
            {
                v_result__355078 = F_dv_store(v_result__355078, F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0008"), Authored("m50.c2i_rows_build.literal_0061")), F_dv_unknown());
            }
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_world_objects.966", v_result__355078)))
        {
            v_result__355078 = F_dv_store(v_result__355078, F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0010"), Authored("m50.c2i_rows_build.literal_0098")), F_dv_field(Authored("m50.c2_norms_build.literal_0033")));
        }
        object v_rows__357127 = F_c2i_rows();
        v_i__355559 = R.number("0");
        while (R.Bool(R.lt(v_i__355559, R.count(v_rows__357127))))
        {
            object v_ref__357200 = F_campaign_ref(F_dv_get(R.at(v_rows__357127, v_i__355559), "object"), F_dv_get(R.at(v_rows__357127, v_i__355559), "field"));
            if (R.Bool(R.not(F_dv_get(F_dv_read(v_result__355078, v_ref__357200), "known"))))
            {
                object v_initial__357311 = false;
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_world_objects.967", v_rows__357127, v_i__355559)))
                {
                    v_initial__357311 = R.number("0");
                }
                v_result__355078 = F_dv_store(v_result__355078, v_ref__357200, F_dv_field(v_initial__357311));
            }
            v_i__355559 = R.add(v_i__355559, R.number("1"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_world_objects.968", v_result__355078)))
        {
            v_result__355078 = F_dv_store(v_result__355078, F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0009"), Authored("m50.c2_inspections.literal_0038")), F_dv_field(""));
        }
        v_result__355078 = F_campaign_add_new(v_result__355078, F_c2_object(Authored("m50.c2i_world_objects.literal"), "", DvValueTemplate.Instantiate("template.inline.c2i_world_objects.969")));
        return F_c2m_initialize(v_result__355078);
    }

    public static object F_c2i_actions(object v_actor) {
        object v_result__359048 = new List<object> {  };
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_actions.973", v_actor)))
        {
            object v_source__359162 = F_campaign_approved(Authored("m50.c2i_install_laws.literal_0002"));
            object v_pre__359276 = DvValueTemplate.Instantiate("template.inline.c2i_actions.974", v_actor);
            object v_grants__359514 = DvValueTemplate.Instantiate("template.inline.c2i_actions.975", v_actor);
            object v_speech__359962 = DvValueTemplate.Instantiate("template.inline.c2i_actions.976", v_source__359162);
            object v_correction__360186 = DvValueTemplate.Instantiate("template.inline.c2i_actions.977", v_pre__359276, v_grants__359514, v_speech__359962, v_source__359162);
            v_correction__360186 = DvValueTemplate.Instantiate("template.inline.c2i_actions.978", v_correction__360186, v_speech__359962, v_grants__359514, v_pre__359276, v_actor, v_source__359162);
            _ = R.push(v_result__359048, v_correction__360186);
        }
        object v_custody__361220 = F_c2_inspect_action(v_actor, Authored("m50.c2_inspection_detail_lines.literal_0008"), Authored("m50.c2i_rows_build.literal_0005"), AuthoredCopy("m50.c2i_actions.literal_0007"), Authored("m50.c2i_actions.literal_0008"), Authored("m50.c2i_actions.literal_0009"), F_campaign_approved(Authored("m50.c2i_actions.literal_0010")));
        _ = R.push(v_result__359048, DvValueTemplate.Instantiate("template.inline.c2i_actions.979", v_custody__361220, v_actor));
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_actions.980", v_actor)))
        {
            object v_peer__361970 = Authored("m50.c2_norms_build.literal_0033");
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_actions.981", v_actor)))
            {
                v_peer__361970 = Authored("m50.c2_norms_build.literal_0056");
            }
            object v_contact__362245 = DvValueTemplate.Instantiate("template.inline.c2i_actions.982", v_actor, v_peer__361970);
            object v_inspect_peer__362458 = F_c2_inspect_action(v_actor, F_campaign_body(v_peer__361970), Authored("m50.c2_patient_contact.literal"), AuthoredCopy("m50.c2i_actions.literal_0013"), Authored("m50.c2i_actions.literal_0014"), Authored("m50.c2i_actions.literal_0015"), F_campaign_approved(Authored("m50.c2i_actions.literal_0016")));
            v_inspect_peer__362458 = DvValueTemplate.Instantiate("template.inline.c2i_actions.983", v_inspect_peer__362458, v_actor, v_contact__362245, v_peer__361970);
            object v_peer_presentation__363187 = F_dv_get(v_inspect_peer__362458, "presentation");
            object v_peer_position__363246 = F_c2_visible_line(F_dv_true(), Authored("m50.c2i_actions.literal_0018"), DvValueTemplate.Instantiate("template.inline.c2i_actions.984", v_peer__361970), F_campaign_approved(Authored("m50.c2i_actions.literal_0016")));
            v_inspect_peer__362458 = F_dv_with(v_inspect_peer__362458, "presentation", F_dv_with(v_peer_presentation__363187, "lines", F_dv_append(F_dv_get(v_peer_presentation__363187, "lines"), v_peer_position__363246)));
            _ = R.push(v_result__359048, v_inspect_peer__362458);
        }
        object v_rows__363848 = F_c2i_rows();
        object v_i__363875 = R.number("0");
        while (R.Bool(R.lt(v_i__363875, R.count(v_rows__363848))))
        {
            object v_row__363925 = R.at(v_rows__363848, v_i__363875);
            if (R.Bool(R.eq(F_dv_get(v_row__363925, "actor"), v_actor)))
            {
                object v_pre__363988 = F_c2i_premise(v_row__363925);
                object v_ref__364028 = F_campaign_ref(F_dv_get(v_row__363925, "object"), F_dv_get(v_row__363925, "field"));
                object v_grants__364087 = new List<object> { v_ref__364028 };
                object v_visible_grants__364119 = new List<object> { v_ref__364028 };
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_actions.985", v_row__363925)))
                {
                    _ = R.push(v_grants__364087, F_campaign_own(v_actor, Authored("m50.c2_world_objects.literal_0040")));
                    _ = R.push(v_visible_grants__364119, F_campaign_own(v_actor, Authored("m50.c2_world_objects.literal_0040")));
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_actions.986", v_row__363925)))
                {
                    _ = R.push(v_grants__364087, F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0008"), Authored("m50.c2i_rows_build.literal_0061")));
                    _ = R.push(v_visible_grants__364119, F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0008"), Authored("m50.c2i_rows_build.literal_0061")));
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_actions.987", v_row__363925)))
                {
                    _ = R.push(v_grants__364087, F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0010"), Authored("m50.c2i_rows_build.literal_0098")));
                    _ = R.push(v_grants__364087, F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0010"), Authored("m50.c2i_rows_build.literal_0103")));
                    _ = R.push(v_visible_grants__364119, F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0010"), Authored("m50.c2i_rows_build.literal_0098")));
                    _ = R.push(v_visible_grants__364119, F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0010"), Authored("m50.c2i_rows_build.literal_0103")));
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_actions.988", v_row__363925)))
                {
                    _ = R.push(v_grants__364087, F_campaign_own(Authored("m50.c2_initial_body.literal_0009"), Authored("m50.c2_initial_body.literal_0010")));
                    _ = R.push(v_grants__364087, F_campaign_own(Authored("m50.c2_initial_body.literal_0009"), Authored("m50.c2_inspections.literal_0036")));
                }
                object v_action__366552 = DvValueTemplate.Instantiate("template.inline.c2i_actions.989", v_row__363925, v_pre__363988, v_grants__364087, v_visible_grants__364119);
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_actions.990", v_row__363925)))
                {
                    object v_invitations__367058 = new List<object> {  };
                    object v_hosts__367096 = AuthoredCopy("m50.c2sf_ceremony_ids.hosts");
                    object v_rooms__367207 = AuthoredCopy("m50.c2sf_ceremony_ids.rooms");
                    object v_host_index__367318 = R.number("0");
                    while (R.Bool(R.lt(v_host_index__367318, R.count(v_hosts__367096))))
                    {
                        object v_room_index__367411 = R.number("0");
                        while (R.Bool(R.lt(v_room_index__367411, R.count(v_rooms__367207))))
                        {
                            _ = R.push(v_invitations__367058, DvValueTemplate.Instantiate("template.inline.c2i_actions.991", v_hosts__367096, v_host_index__367318, v_actor, v_rooms__367207, v_room_index__367411));
                            v_room_index__367411 = R.add(v_room_index__367411, R.number("1"));
                        }
                        v_host_index__367318 = R.add(v_host_index__367318, R.number("1"));
                    }
                    v_action__366552 = F_dv_with(v_action__366552, "received_speech_requirement", new List<object> { F_dv_pair("any_of", v_invitations__367058) });
                }
                if (R.Bool(F_c2sf_case_row(v_row__363925)))
                {
                    v_action__366552 = F_dv_with(v_action__366552, "storyflow_case_row", v_row__363925);
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_actions.992", v_row__363925)))
                {
                    v_action__366552 = DvValueTemplate.Instantiate("template.inline.c2i_actions.993", v_action__366552);
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_actions.994", v_row__363925)))
                {
                    v_action__366552 = DvValueTemplate.Instantiate("template.inline.c2i_actions.995", v_action__366552);
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_actions.996", v_actor, v_row__363925)))
                {
                    v_action__366552 = F_dv_with(v_action__366552, "work_operation", F_dv_get(v_row__363925, "id"));
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_actions.997", v_row__363925)))
                {
                    v_action__366552 = DvValueTemplate.Instantiate("template.inline.c2i_actions.998", v_action__366552);
                }
                object v_talk_fields__369156 = AuthoredCopy("m50.c2i_actions.talk_fields");
                if (R.Bool(F_dv_has(v_talk_fields__369156, F_dv_get(v_row__363925, "field"))))
                {
                    v_action__366552 = DvValueTemplate.Instantiate("template.inline.c2i_actions.999", v_action__366552, v_row__363925);
                }
                _ = R.push(v_result__359048, v_action__366552);
            }
            v_i__363875 = R.add(v_i__363875, R.number("1"));
        }
        object v_places__369840 = AuthoredCopy("m50.c2i_actions.places");
        v_i__363875 = R.number("0");
        while (R.Bool(R.lt(v_i__363875, R.count(v_places__369840))))
        {
            object v_place__369983 = R.at(v_places__369840, v_i__363875);
            object v_inspect__370014 = F_c2_inspect_action(v_actor, F_dv_get(v_place__369983, "object"), F_dv_get(v_place__369983, "room"), F_dv_get(v_place__369983, "fields"), Authored("m50.c2i_actions.literal_0026"), Authored("m50.c2i_actions.literal_0027"), F_campaign_approved(Authored("m50.c2i_actions.literal_0028")));
            v_inspect__370014 = DvValueTemplate.Instantiate("template.inline.c2i_actions.1000", v_inspect__370014, v_actor, v_place__369983);
            _ = R.push(v_result__359048, v_inspect__370014);
            v_i__363875 = R.add(v_i__363875, R.number("1"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_actions.1001", v_actor)))
        {
            object v_team__370634 = AuthoredCopy("m50.c2i_actions.team");
            v_i__363875 = R.number("0");
            while (R.Bool(R.lt(v_i__363875, R.count(v_team__370634))))
            {
                object v_peer__370783 = R.at(v_team__370634, v_i__363875);
                object v_pre__370815 = DvValueTemplate.Instantiate("template.inline.c2i_actions.1002", v_actor, v_peer__370783);
                object v_inspect__371036 = F_c2_inspect_action(v_actor, F_campaign_body(F_dv_get(v_peer__370783, "actor")), Authored("m50.c2_norms_build.literal_0034"), DvValueTemplate.Instantiate("template.inline.c2i_actions.1003", v_peer__370783), Authored("m50.c2i_actions.literal_0031"), Authored("m50.c2i_actions.literal_0032"), F_campaign_approved(Authored("m50.c2i_actions.literal_0033")));
                v_inspect__371036 = DvValueTemplate.Instantiate("template.inline.c2i_actions.1004", v_inspect__371036, v_actor, v_peer__370783, v_pre__370815);
                _ = R.push(v_result__359048, v_inspect__371036);
                v_i__363875 = R.add(v_i__363875, R.number("1"));
            }
        }
        return v_result__359048;
    }
    public static object F_c2i_watch_goal(object v_actor,object v_when,object v_source,object v_minimum) {
        object v_room__372071 = Authored("m50.c2_norms_build.literal_0004");
        object v_method__372158 = Authored("m50.c2i_watch_goal.method");
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_watch_goal.1005", v_actor)))
        {
            v_room__372071 = Authored("m50.c2_norms_build.literal_0048");
            v_method__372158 = Authored("m50.c2i_watch_goal.literal");
        }
        return F_dv_with_many(F_campaign_observation_goal(F_campaign_own(Authored("m50.c2_initial_body.literal_0009"), Authored("m50.c2i_watch_goal.literal_0002")), v_when, v_source, v_minimum), new List<object> { F_dv_pair("navigation_room", v_room__372071), F_dv_pair("navigation_methods", new List<object> { v_method__372158 }) });
    }
    public static object F_c2i_goals(object v_actor) {
        v_actor = F_c2_query_view(v_actor, v_actor);
        object v_id__372905 = F_dv_get(v_actor, "id");
        object v_goals__372928 = new List<object> {  };
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1006", v_id__372905)))
        {
            object v_visit__373038 = F_dv_find(F_dv_find(v_actor, Authored("m50.c2i_goals.literal"), new List<object> {  }), Authored("m50.c2i_goals.literal_0002"), false);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1007", v_visit__373038, v_actor)))
            {
                object v_pending__373523 = DvValueTemplate.Instantiate("template.inline.c2i_goals.1008", v_id__372905);
                _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_goal(F_campaign_at(v_id__372905, Authored("m50.c2i_rows_build.literal_0005")), v_pending__373523, new List<object> { v_visit__373038 }), AuthoredCopy("m50.c2i_goals.literal_0004")));
            }
        }
        if (R.Bool((R.Bool(R.eq(v_id__372905, Authored("m50.c2_supply_actions.literal_0027"))) ? (object)(F_c2_quer_concerned(v_actor)) : (object)(false))))
        {
            _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_goal(F_dv_fact(F_campaign_own(Authored("m50.c2_norms_build.literal_0013"), Authored("m50.c2i_actions.literal")), true), DvValueTemplate.Instantiate("template.inline.c2i_goals.1009"), DvValueTemplate.Instantiate("template.inline.c2i_goals.1010")), AuthoredCopy("m50.c2i_goals.literal_0006")));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1011", v_actor, v_id__372905)))
        {
            object v_unresolved__375388 = F_dv_true();
            _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_goal(F_campaign_at(v_id__372905, Authored("m50.c2i_rows_build.literal_0005")), v_unresolved__375388, DvValueTemplate.Instantiate("template.inline.c2i_goals.1012")), AuthoredCopy("m50.c2i_goals.literal_0004")));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1013", v_actor, v_id__372905)))
        {
            object v_contributed__375942 = Authored("m50.c2i_rows_build.literal_0045");
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1014", v_id__372905)))
            {
                v_contributed__375942 = Authored("m50.c2i_rows_build.literal_0004");
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1015", v_actor, v_contributed__375942)))
            {
                _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_goal(F_campaign_at(v_id__372905, Authored("m50.c2i_rows_build.literal_0005")), F_dv_true(), DvValueTemplate.Instantiate("template.inline.c2i_goals.1016")), AuthoredCopy("m50.c2i_goals.literal_0004")));
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1017", v_actor)))
                {
                    _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0008"), Authored("m50.c2i_rows_build.literal_0052")), F_dv_true(), DvValueTemplate.Instantiate("template.inline.c2i_goals.1018"), F_dv_find(F_dv_find(v_actor, Authored("m50.c2i_goals.literal_0009"), new List<object> {  }), Authored("m50.c2_navigation_pending.literal_0004"), R.number("0"))), DvValueTemplate.Instantiate("template.inline.c2i_goals.1019", v_id__372905)));
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1020", v_id__372905)))
                {
                    _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_goal(F_dv_fact(F_campaign_own(v_id__372905, Authored("m50.c2i_actions.literal")), true), F_dv_true(), DvValueTemplate.Instantiate("template.inline.c2i_goals.1021")), AuthoredCopy("m50.c2i_goals.literal_0011")));
                }
            }
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1022", v_id__372905)))
        {
            if (R.Bool(F_c2g_review_heard(v_actor)))
            {
                _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_goal(F_dv_fact(F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0008"), Authored("m50.c2i_rows_build.literal_0052")), true), F_dv_true(), DvValueTemplate.Instantiate("template.inline.c2i_goals.1023")), DvValueTemplate.Instantiate("template.inline.c2i_goals.1024", v_id__372905)));
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1025", v_actor)))
                {
                    object v_minimum__379282 = F_dv_find(F_dv_find(v_actor, Authored("m50.c2i_goals.literal_0009"), new List<object> {  }), Authored("m50.c2_navigation_pending.literal_0004"), R.number("0"));
                    _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0008"), Authored("m50.c2i_rows_build.literal_0052")), F_dv_true(), DvValueTemplate.Instantiate("template.inline.c2i_goals.1026"), v_minimum__379282), DvValueTemplate.Instantiate("template.inline.c2i_goals.1027", v_id__372905)));
                }
            }
            _ = R.push(v_goals__372928, F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0008"), Authored("m50.c2i_rows_build.literal_0061")), F_campaign_at(v_id__372905, Authored("m50.c2i_rows_build.literal_0005")), DvValueTemplate.Instantiate("template.inline.c2i_goals.1028"), R.number("0")));
        }
        v_goals__372928 = F_c2m_concat(v_goals__372928, F_c2m_goals(v_actor));
        if (R.Bool((R.Bool(R.eq(v_id__372905, Authored("m50.c2_world_objects.literal_0030"))) ? (object)(F_c2_moran_outing_heard(v_actor)) : (object)(false))))
        {
            _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_observation_goal(F_campaign_own(Authored("m50.c2_initial_body.literal_0009"), Authored("m50.c2_can_act.literal")), F_c2_night(Authored("m50.c2_any_night.literal_0003")), DvValueTemplate.Instantiate("template.inline.c2i_goals.1029"), Authored("m50.c2_personal_goal_templates_build.literal_0004")), AuthoredCopy("m50.c2i_goals.literal_0014")));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1030", v_actor, v_id__372905)))
        {
            object v_live_preparation__381295 = DvValueTemplate.Instantiate("template.inline.c2i_goals.1031", v_id__372905);
            _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_goal(F_campaign_at(v_id__372905, Authored("m50.c2_patient_contact.literal")), v_live_preparation__381295, DvValueTemplate.Instantiate("template.inline.c2i_goals.1032")), AuthoredCopy("m50.c2i_goals.literal_0016")));
            object v_peer__382056 = Authored("m50.c2_norms_build.literal_0033");
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1033", v_id__372905)))
            {
                v_peer__382056 = Authored("m50.c2_norms_build.literal_0056");
            }
            object v_contact__382328 = DvValueTemplate.Instantiate("template.inline.c2i_goals.1034", v_live_preparation__381295, v_id__372905, v_peer__382056);
            object v_fields__382556 = AuthoredCopy("m50.c2i_actions.literal_0013");
            object v_field_index__382661 = R.number("0");
            while (R.Bool(R.lt(v_field_index__382661, R.count(v_fields__382556))))
            {
                _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_observation_goal(F_campaign_own(v_peer__382056, R.at(v_fields__382556, v_field_index__382661)), v_contact__382328, DvValueTemplate.Instantiate("template.inline.c2i_goals.1035"), R.number("0")), DvValueTemplate.Instantiate("template.inline.c2i_goals.1036", v_id__372905)));
                v_field_index__382661 = R.add(v_field_index__382661, R.number("1"));
            }
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1037", v_id__372905)))
        {
            object v_mixed__383277 = F_dv_fact(F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0010"), Authored("m50.c2_patient_contact.literal_0002")), true);
            _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_goal(F_dv_fact(F_campaign_own(v_id__372905, Authored("m50.c2_patient_contact.literal_0003")), true), v_mixed__383277, DvValueTemplate.Instantiate("template.inline.c2i_goals.1038")), DvValueTemplate.Instantiate("template.inline.c2i_goals.1039", v_id__372905, v_mixed__383277)));
            object v_patients__384118 = F_c2_care_patients();
            object v_patient_index__384161 = R.number("0");
            while (R.Bool(R.lt(v_patient_index__384161, R.count(v_patients__384118))))
            {
                object v_patient__384247 = R.at(v_patients__384118, v_patient_index__384161);
                object v_position__384298 = F_dv_read(F_dv_get(v_actor, "mind"), F_campaign_position_ref(v_patient__384247));
                object v_need__384378 = F_campaign_any(F_dv_fact(F_campaign_own(v_patient__384247, Authored("m50.c2_initial_body.literal_0002")), true), F_campaign_any(F_dv_fact(F_campaign_own(v_patient__384247, Authored("m50.c2_can_act.literal")), true), F_campaign_any(F_dv_fact(F_campaign_own(v_patient__384247, Authored("m50.c2_initial_body.literal_0010")), true), F_dv_fact(F_campaign_own(v_patient__384247, Authored("m50.c2_inspections.literal_0042")), true))));
                if (R.Bool((R.Bool(F_dv_uncontested(v_position__384298)) ? (object)(F_dv_has(F_c2_rooms(), F_dv_get(v_position__384298, "value"))) : (object)(false))))
                {
                    object v_condition__384961 = DvValueTemplate.Instantiate("template.inline.c2i_goals.1040", v_need__384378, v_id__372905);
                    _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_goal(F_dv_fact(F_campaign_own(v_patient__384247, Authored("m50.c2_care_patients.literal_0002")), true), v_condition__384961, DvValueTemplate.Instantiate("template.inline.c2i_goals.1041")), DvValueTemplate.Instantiate("template.inline.c2i_goals.1042", v_patient__384247, v_position__384298, v_condition__384961)));
                }
                v_patient_index__384161 = R.add(v_patient_index__384161, R.number("1"));
            }
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1043", v_id__372905)))
        {
            object v_patients__385942 = F_c2_care_patients();
            object v_patient_index__385985 = R.number("0");
            while (R.Bool(R.lt(v_patient_index__385985, R.count(v_patients__385942))))
            {
                object v_patient__386071 = R.at(v_patients__385942, v_patient_index__385985);
                _ = R.push(v_goals__372928, F_campaign_observation_goal(F_campaign_own(v_patient__386071, Authored("m50.c2_can_act.literal")), F_c2_patient_contact(v_id__372905, v_patient__386071), DvValueTemplate.Instantiate("template.inline.c2i_goals.1044"), R.number("0")));
                _ = R.push(v_goals__372928, F_campaign_observation_goal(F_campaign_own(v_patient__386071, Authored("m50.c2_initial_body.literal_0002")), F_c2_patient_contact(v_id__372905, v_patient__386071), DvValueTemplate.Instantiate("template.inline.c2i_goals.1045"), R.number("0")));
                _ = R.push(v_goals__372928, F_campaign_observation_goal(F_campaign_own(v_patient__386071, Authored("m50.c2_world_objects.literal_0038")), DvValueTemplate.Instantiate("template.inline.c2i_goals.1046", v_id__372905, v_patient__386071), DvValueTemplate.Instantiate("template.inline.c2i_goals.1047"), Authored("m50.c2_care_patients.literal")));
                _ = R.push(v_goals__372928, F_campaign_observation_goal(F_campaign_own(v_patient__386071, Authored("m50.c2_care_patients.literal_0008")), F_c2_patient_contact(v_id__372905, v_patient__386071), DvValueTemplate.Instantiate("template.inline.c2i_goals.1048"), Authored("m50.c2_personal_goal_templates_build.literal_0004")));
                _ = R.push(v_goals__372928, F_campaign_observation_goal(F_campaign_own(v_patient__386071, Authored("m50.c2_care_patients.literal_0002")), F_c2_patient_contact(v_id__372905, v_patient__386071), DvValueTemplate.Instantiate("template.inline.c2i_goals.1049"), R.number("0")));
                v_patient_index__385985 = R.add(v_patient_index__385985, R.number("1"));
            }
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1050", v_actor, v_id__372905)))
        {
            object v_guests__387995 = AuthoredCopy("m50.c2sf_ceremony_ids.guests");
            object v_rooms__388100 = AuthoredCopy("m50.c2sf_ceremony_ids.rooms");
            object v_completed__388203 = F_dv_find(v_actor, Authored("m50.c2_personal_goals.literal_0006"), new List<object> {  });
            object v_invited__388322 = false;
            object v_guest_index__388351 = R.number("0");
            while (R.Bool(R.lt(v_guest_index__388351, R.count(v_guests__387995))))
            {
                object v_room_index__388431 = R.number("0");
                while (R.Bool(R.lt(v_room_index__388431, R.count(v_rooms__388100))))
                {
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1051", v_completed__388203, v_id__372905, v_guests__387995, v_guest_index__388351, v_rooms__388100, v_room_index__388431)))
                    {
                        v_invited__388322 = true;
                    }
                    v_room_index__388431 = R.add(v_room_index__388431, R.number("1"));
                }
                v_guest_index__388351 = R.add(v_guest_index__388351, R.number("1"));
            }
            if (R.Bool(v_invited__388322))
            {
                _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_goal(F_campaign_at(v_id__372905, Authored("m50.c2_patient_contact.literal")), DvValueTemplate.Instantiate("template.inline.c2i_goals.1052"), DvValueTemplate.Instantiate("template.inline.c2i_goals.1053")), AuthoredCopy("m50.c2i_goals.literal_0016")));
            }
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1054", v_actor, v_id__372905)))
        {
            object v_hosts__389784 = AuthoredCopy("m50.c2sf_ceremony_ids.hosts");
            object v_rooms__389887 = AuthoredCopy("m50.c2sf_ceremony_ids.rooms");
            object v_invited__389990 = false;
            object v_host_index__390019 = R.number("0");
            while (R.Bool(R.lt(v_host_index__390019, R.count(v_hosts__389784))))
            {
                object v_room_index__390096 = R.number("0");
                while (R.Bool(R.lt(v_room_index__390096, R.count(v_rooms__389887))))
                {
                    object v_invite_id__390181 = R.cat(R.at(v_hosts__389784, v_host_index__390019), R.cat(Authored("m50.c2sf_ceremony_ids.literal_0003"), R.cat(v_id__372905, R.cat(Authored("m50.c2_move_action.literal_0002"), R.at(v_rooms__389887, v_room_index__390096)))));
                    if (R.Bool(F_c2_received_requirement(v_actor, new List<object> { F_dv_pair("speaker", R.at(v_hosts__389784, v_host_index__390019)), F_dv_pair("action_id", v_invite_id__390181) })))
                    {
                        v_invited__389990 = true;
                    }
                    v_room_index__390096 = R.add(v_room_index__390096, R.number("1"));
                }
                v_host_index__390019 = R.add(v_host_index__390019, R.number("1"));
            }
            if (R.Bool(v_invited__389990))
            {
                _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_goal(F_campaign_at(v_id__372905, Authored("m50.c2_patient_contact.literal")), F_campaign_window(Authored("m50.c2_any_night.literal_0002"), Authored("m50.c2i_goals.literal_0019"), Authored("m50.c2i_rows_build.literal_0003")), DvValueTemplate.Instantiate("template.inline.c2i_goals.1055")), AuthoredCopy("m50.c2i_goals.literal_0016")));
            }
        }
        object v_rows__391214 = F_c2i_rows();
        object v_closing__391241 = F_c2i_closing_navigation(v_actor);
        if (R.Bool(R.not(R.eq(v_closing__391241, false))))
        {
            object v_source__391320 = F_campaign_approved(Authored("m50.c2_navigation_goals.literal_0002"));
            object v_when__391437 = F_campaign_any(F_dv_fact(F_campaign_ref(Authored("m50.c2_inspection_presentation.literal_0015"), Authored("m50.c2_inspection_presentation.literal_0025")), true), F_dv_at_least(F_campaign_clock(), Authored("m50.c2_navigation_goals.literal")));
            _ = R.push(v_goals__372928, F_campaign_goal(F_campaign_at(v_id__372905, F_dv_get(v_closing__391241, "target")), v_when__391437, new List<object> { v_source__391320, v_closing__391241 }));
            if (R.Bool(R.not(R.eq(F_dv_get(v_closing__391241, "next"), F_dv_get(v_closing__391241, "target")))))
            {
                _ = R.push(v_goals__372928, F_campaign_goal(F_campaign_at(v_id__372905, F_dv_get(v_closing__391241, "next")), F_dv_all(new List<object> { v_when__391437, F_campaign_at(v_id__372905, F_dv_get(v_closing__391241, "origin")) }), new List<object> { v_source__391320, v_closing__391241 }));
            }
        }
        object v_i__392049 = R.number("0");
        while (R.Bool(R.lt(v_i__392049, R.count(v_rows__391214))))
        {
            object v_row__392099 = R.at(v_rows__391214, v_i__392049);
            object v_streamlined__392126 = (R.Bool(F_c2sf_enabled(v_actor)) ? (object)(F_c2sf_case_row(v_row__392099)) : (object)(false));
            if (R.Bool(v_streamlined__392126))
            {
                v_row__392099 = F_c2sf_row(v_row__392099);
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1056", v_row__392099, v_id__372905, v_streamlined__392126)))
            {
                object v_completion__392397 = F_dv_fact(F_campaign_ref(F_dv_get(v_row__392099, "object"), F_dv_get(v_row__392099, "field")), F_c2i_value(v_row__392099));
                object v_undertaking_when__392488 = F_dv_get(v_row__392099, "when");
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1057", v_actor, v_row__392099)))
                {
                    v_undertaking_when__392488 = F_dv_true();
                }
                object v_undertaking__392808 = F_dv_all(new List<object> { v_undertaking_when__392488, F_c2_work_accepted(v_id__372905, F_dv_get(v_row__392099, "id")) });
                object v_navigation_requirements__392896 = F_dv_get(v_row__392099, "requirements");
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1058", v_actor, v_row__392099)))
                {
                    v_undertaking__392808 = v_undertaking_when__392488;
                }
                if (R.Bool(v_streamlined__392126))
                {
                    v_undertaking__392808 = F_dv_get(v_row__392099, "when");
                    v_navigation_requirements__392896 = new List<object> {  };
                }
                _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_goal(v_completion__392397, v_undertaking__392808, DvValueTemplate.Instantiate("template.inline.c2i_goals.1059")), new List<object> { F_dv_pair("navigation_completion", v_completion__392397), F_dv_pair("navigation_room", F_dv_get(v_row__392099, "room")), F_dv_pair("navigation_requirements", v_navigation_requirements__392896), F_dv_pair("navigation_methods", new List<object> { F_dv_get(v_row__392099, "id") }) }));
            }
            v_i__392049 = R.add(v_i__392049, R.number("1"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1060", v_id__372905)))
        {
            _ = R.push(v_goals__372928, F_campaign_goal(F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0009"), Authored("m50.c2_total_store_open_known.literal_0007")), true), F_dv_fact(F_campaign_own(Authored("m50.c2_norms_build.literal_0003"), Authored("m50.c2i_goals.literal_0023")), true), DvValueTemplate.Instantiate("template.inline.c2i_goals.1061")));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1062", v_id__372905)))
        {
            if (R.Bool(R.not(F_c2_received_requirement(v_actor, AuthoredCopy("m50.c2i_goals.literal_0025")))))
            {
                _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_goal(F_campaign_at(v_id__372905, Authored("m50.c2_norms_build.literal_0048")), F_campaign_window(Authored("m50.c2_any_night.literal_0003"), Authored("m50.c2_norm_available.literal"), Authored("m50.c2_night.literal")), DvValueTemplate.Instantiate("template.inline.c2i_goals.1063")), AuthoredCopy("m50.c2i_goals.literal_0027")));
            }
            _ = R.push(v_goals__372928, F_c2i_watch_goal(v_actor, F_c2_night(Authored("m50.c2_any_night.literal_0003")), DvValueTemplate.Instantiate("template.inline.c2i_goals.1064"), Authored("m50.c2_personal_goal_templates_build.literal_0004")));
            _ = R.push(v_goals__372928, F_campaign_goal(F_campaign_at(Authored("m50.c2_initial_body.literal_0009"), Authored("m50.c2_inspections.literal_0035")), DvValueTemplate.Instantiate("template.inline.c2i_goals.1065"), DvValueTemplate.Instantiate("template.inline.c2i_goals.1066")));
            _ = R.push(v_goals__372928, F_campaign_goal(F_dv_fact(F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0009"), Authored("m50.c2_inspections.literal_0038")), Authored("m50.c2_inspections.literal_0039")), F_c2u_sketch_opportunity(), DvValueTemplate.Instantiate("template.inline.c2i_goals.1067")));
            _ = R.push(v_goals__372928, F_campaign_goal(F_campaign_at(Authored("m50.c2_initial_body.literal_0009"), Authored("m50.c2_world_objects.literal_0011")), F_dv_fact(F_campaign_own(Authored("m50.c2_initial_body.literal_0009"), Authored("m50.c2_initial_body.literal_0010")), true), DvValueTemplate.Instantiate("template.inline.c2i_goals.1068")));
        }
        object v_evidence_places__396807 = AuthoredCopy("m50.c2i_goals.evidence_places");
        object v_place_index__396918 = R.number("0");
        while (R.Bool(R.lt(v_place_index__396918, R.count(v_evidence_places__396807))))
        {
            object v_place__396999 = R.at(v_evidence_places__396807, v_place_index__396918);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1069", v_id__372905, v_place__396999)))
            {
                object v_field_index__397354 = R.number("0");
                while (R.Bool(R.lt(v_field_index__397354, R.count(F_dv_get(v_place__396999, "fields")))))
                {
                    _ = R.push(v_goals__372928, F_campaign_observation_goal(F_campaign_ref(F_dv_get(v_place__396999, "object"), R.at(F_dv_get(v_place__396999, "fields"), v_field_index__397354)), F_campaign_at(v_id__372905, F_dv_get(v_place__396999, "room")), DvValueTemplate.Instantiate("template.inline.c2i_goals.1070"), R.number("0")));
                    v_field_index__397354 = R.add(v_field_index__397354, R.number("1"));
                }
            }
            v_place_index__396918 = R.add(v_place_index__396918, R.number("1"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1071", v_id__372905)))
        {
            object v_granary_when__397862 = F_campaign_window(Authored("m50.c2_any_night.literal_0002"), Authored("m50.c2_route_inspections.start"), Authored("m50.c2_route_inspections.end"));
            object v_granary_minimum__398116 = Authored("m50.c2i_goals.granary_minimum");
            if (R.Bool(F_c2g_enabled(v_actor)))
            {
                v_granary_when__397862 = F_c2g_granary_available(v_actor);
                v_granary_minimum__398116 = R.number("0");
            }
            object v_granary_fields__398338 = AuthoredCopy("m50.c2i_goals.granary_fields");
            object v_field_index__398451 = R.number("0");
            while (R.Bool(R.lt(v_field_index__398451, R.count(v_granary_fields__398338))))
            {
                _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_world_objects.literal_0006"), R.at(v_granary_fields__398338, v_field_index__398451)), v_granary_when__397862, DvValueTemplate.Instantiate("template.inline.c2i_goals.1072"), v_granary_minimum__398116), DvValueTemplate.Instantiate("template.inline.c2i_goals.1073", v_id__372905)));
                v_field_index__398451 = R.add(v_field_index__398451, R.number("1"));
            }
            _ = R.push(v_goals__372928, F_campaign_goal(F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0006"), Authored("m50.c2_total_store_open_known.literal_0002")), false), v_granary_when__397862, DvValueTemplate.Instantiate("template.inline.c2i_goals.1074")));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_goals.1075", v_actor, v_id__372905)))
        {
            object v_live__399613 = F_dv_all(new List<object> { F_c2g_granary_available(v_actor), F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0006"), Authored("m50.c2_total_store_open_known.literal_0002")), true) });
            _ = R.push(v_goals__372928, F_dv_with_many(F_campaign_goal(F_campaign_at(v_id__372905, Authored("m50.c2_norms_build.literal_0034")), v_live__399613, DvValueTemplate.Instantiate("template.inline.c2i_goals.1076")), AuthoredCopy("m50.c2i_goals.literal_0038")));
        }
        object coordinationGoals = DvValueTemplate.Instantiate("template.c2_coordination_goals", v_actor);
        foreach (object goal in R.Items(coordinationGoals))
        {
            _ = R.push(v_goals__372928, F_campaign_index_goal_window(goal));
        }
        return v_goals__372928;
    }
    public static object F_c2i_reserve_column_law() {
        return DvValueTemplate.Instantiate("template.c2i_reserve_column_law");
    }
    public static object F_c2i_complete_pending_law(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2i_complete_pending_law", v_actor);
    }
    public static object F_c2i_complete_pending_purpose_law(object v_行动者,object v_purpose) {
        return DvValueTemplate.Instantiate("template.c2i_complete_pending_purpose_law", v_行动者, v_purpose);
    }

    public static object F_c2i_record_actions(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2i_record_actions", v_actor);
    }
    public static object F_c2_pending_grants(object v_actor,object v_purpose) {
        return DvValueTemplate.Instantiate("template.c2_pending_grants", v_actor, v_purpose);
    }
    public static object F_c2i_erosion_parameters(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2i_erosion_parameters", v_actor);
    }
    public static object F_c2i_outdoor_danger_law(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c2i_outdoor_danger_law", v_行动者);
    }
    public static object F_c2i_evening_actions(object v_actor) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_evening_actions.1080", v_actor)))
        {
            return new List<object> {  };
        }
        object v_source__413041 = F_campaign_approved(Authored("m50.c2i_evening_actions.literal"));
        object v_when__413149 = F_dv_all(new List<object> { F_c2u_sketch_opportunity(), F_c2_alive(v_actor) });
        object v_result_ref__413217 = F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0009"), Authored("m50.c2_inspections.literal_0038"));
        object v_result__413408 = new List<object> {  };
        object v_watch_index__413429 = R.number("0");
        while (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_evening_actions.1081", v_watch_index__413429)))
        {
            object v_watched__413548 = R.eq(v_watch_index__413429, R.number("1"));
            object v_context__413588 = DvValueTemplate.Instantiate("template.inline.c2i_evening_actions.1082", v_when__413149, v_actor, v_watched__413548);
            object v_options__413737 = AuthoredCopy("m50.c2i_evening_actions.options");
            object v_i__413846 = R.number("0");
            while (R.Bool(R.lt(v_i__413846, R.count(v_options__413737))))
            {
                object v_option__413907 = R.at(v_options__413737, v_i__413846);
                object v_promotes__413944 = F_dv_get(v_option__413907, "promotes");
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_evening_actions.1083", v_watched__413548, v_option__413907)))
                {
                    v_promotes__413944 = AuthoredCopy("m50.c2i_evening_actions.literal_0002");
                }
                object v_id__414331 = R.cat(Authored("m50.c2i_evening_actions.literal_0003"), R.cat(F_dv_get(v_option__413907, "value"), R.cat(Authored("m50.c2_move_action.literal_0002"), R.show(v_watched__413548))));
                object v_law__414557 = R.cat(Authored("m50.c2i_evening_actions.literal_0003"), F_dv_get(v_option__413907, "value"));
                object v_ready__414925 = v_context__413588;
                object v_choice__414958 = DvValueTemplate.Instantiate("template.inline.c2i_evening_actions.1084", v_ready__414925, v_result_ref__413217, v_promotes__413944, v_option__413907, v_source__413041);
                object v_speech__415389 = DvValueTemplate.Instantiate("template.inline.c2i_evening_actions.1085", v_source__413041, v_option__413907);
                object v_action__415560 = DvValueTemplate.Instantiate("template.inline.c2i_evening_actions.1086", v_id__414331, v_ready__414925, v_when__413149, v_law__414557, v_result_ref__413217, v_option__413907, v_source__413041);
                v_action__415560 = F_dv_with(v_action__415560, "owned_safety_choice", v_ready__414925);
                v_action__415560 = DvValueTemplate.Instantiate("template.inline.c2i_evening_actions.1087", v_action__415560, v_choice__414958, v_speech__415389);
                _ = R.push(v_result__413408, v_action__415560);
                v_i__413846 = R.add(v_i__413846, R.number("1"));
            }
            v_watch_index__413429 = R.add(v_watch_index__413429, R.number("1"));
        }
        object v_unsafe__416143 = DvValueTemplate.Instantiate("template.inline.c2i_evening_actions.1088", v_when__413149);
        object v_unsafe_choice__416351 = DvValueTemplate.Instantiate("template.inline.c2i_evening_actions.1089", v_unsafe__416143, v_result_ref__413217, v_source__413041);
        object v_unsafe_action__416937 = DvValueTemplate.Instantiate("template.inline.c2i_evening_actions.1090", v_unsafe__416143, v_when__413149, v_result_ref__413217, v_source__413041);
        _ = R.push(v_result__413408, DvValueTemplate.Instantiate("template.inline.c2i_evening_actions.1091", v_unsafe_action__416937, v_unsafe_choice__416351));
        object v_watch__417649 = F_c2_inspect_action(v_actor, F_campaign_body(v_actor), Authored("m50.c2_norms_build.literal_0004"), AuthoredCopy("m50.c2i_evening_actions.literal_0011"), Authored("m50.c2i_evening_actions.literal_0012"), Authored("m50.c2i_evening_actions.literal_0013"), v_source__413041);
        _ = R.push(v_result__413408, DvValueTemplate.Instantiate("template.inline.c2i_evening_actions.1092", v_watch__417649));
        object v_local_watch__418148 = F_c2_inspect_action(v_actor, F_campaign_body(v_actor), Authored("m50.c2_norms_build.literal_0048"), AuthoredCopy("m50.c2i_evening_actions.literal_0011"), Authored("m50.c2i_evening_actions.literal_0014"), Authored("m50.c2i_evening_actions.literal_0015"), v_source__413041);
        _ = R.push(v_result__413408, DvValueTemplate.Instantiate("template.inline.c2i_evening_actions.1093", v_local_watch__418148));
        return v_result__413408;
    }
    public static object F_c2i_evening_choice_law(object v_行动者,object v_取值) {
        return DvValueTemplate.Instantiate("template.c2i_evening_choice_law", v_行动者, v_取值);
    }
    public static object F_c2i_meter_safe_law() {
        return DvValueTemplate.Instantiate("template.c2i_meter_safe_law");
    }
    public static object F_c2i_meter_stale_law() {
        return DvValueTemplate.Instantiate("template.c2i_meter_stale_law");
    }
    public static object F_c2i_known_route(object v_actor,object v_target) => F_c2i_known_route(v_actor, v_target, out _);
    public static object F_c2i_known_route(object v_actor,object v_target, out object routeDiagnostic) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_known_route.1094", v_actor)))
            return F_c2i_known_route_uncached(v_actor, v_target, out routeDiagnostic);
        v_actor = R.snapshot(v_actor);
        object originalKey = DvValueTemplate.Instantiate("template.inline.c2i_known_route.1095", v_actor, v_target);
        object key = new List<object> { "known-route-with-diagnostic-v1", originalKey };
        object cached = R.memo_get(F_dv_get(v_actor, "mind"), key);
        if (R.Bool(R.at(cached, R.number("0"))))
        {
            object saved = R.at(cached, R.number("1"));
            routeDiagnostic = F_dv_get(saved, "diagnostic");
            return F_dv_get(saved, "route");
        }
        object route = F_c2i_known_route_uncached(v_actor, v_target, out routeDiagnostic);
        _ = R.memo_put(F_dv_get(v_actor, "mind"), key, new List<object> { F_dv_pair("route", route), F_dv_pair("diagnostic", routeDiagnostic) });
        return route;
    }
    public static object F_c2i_known_route_uncached(object v_actor,object v_target, out object routeDiagnostic) {
        object routeChecks = new List<object>();
        object v_position__421388 = F_dv_read(F_dv_get(v_actor, "mind"), F_campaign_position_ref(F_dv_get(v_actor, "id")));
        routeDiagnostic = new List<object> { F_dv_pair("target", v_target), F_dv_pair("position", v_position__421388), F_dv_pair("checks", routeChecks), F_dv_pair("reason", "no-known-path") };
        if (!R.Bool(F_dv_uncontested(v_position__421388)))
        { routeDiagnostic = F_dv_with(routeDiagnostic, "reason", "position-unknown"); return false; }
        if (R.Bool(R.eq(F_dv_get(v_position__421388, "value"), v_target)))
        { routeDiagnostic = F_dv_with(routeDiagnostic, "reason", "already-at-target"); return false; }
        object v_queue__421549 = new List<object> { new List<object> { F_dv_pair("room", F_dv_get(v_position__421388, "value")), F_dv_pair("first", ""), F_dv_pair("basis", new List<object> {  }) } };
        object v_visited__421619 = new List<object> { F_dv_get(v_position__421388, "value") };
        object v_cursor__421655 = R.number("0");
        object v_edges__421675 = F_c2_edges();
        while (R.Bool(R.lt(v_cursor__421655, R.count(v_queue__421549))))
        {
            object v_node__421744 = R.at(v_queue__421549, v_cursor__421655);
            object v_i__421778 = R.number("0");
            while (R.Bool(R.lt(v_i__421778, R.count(v_edges__421675))))
            {
                object v_edge__421837 = R.at(v_edges__421675, v_i__421778);
                object v_destination__421870 = "";
                if (R.Bool(R.eq(F_dv_get(v_edge__421837, "from"), F_dv_get(v_node__421744, "room"))))
                {
                    v_destination__421870 = F_dv_get(v_edge__421837, "to");
                }
                if (R.Bool(R.eq(F_dv_get(v_edge__421837, "to"), F_dv_get(v_node__421744, "room"))))
                {
                    v_destination__421870 = F_dv_get(v_edge__421837, "from");
                }
                if (!R.Bool(R.eq(v_destination__421870, "")) && !R.Bool(F_dv_has(v_visited__421619, v_destination__421870)))
                {
                    object gateTruth = F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_fact(F_campaign_access(F_dv_get(v_edge__421837, "gate")), true));
                    if (!R.Bool(R.eq(gateTruth, "true")))
                    {
                        _ = R.push(routeChecks, new List<object> { F_dv_pair("from", F_dv_get(v_node__421744, "room")), F_dv_pair("to", v_destination__421870), F_dv_pair("gate", F_dv_get(v_edge__421837, "gate")), F_dv_pair("gate_truth", gateTruth), F_dv_pair("adoption", "not-evaluated") });
                        v_i__421778 = R.add(v_i__421778, R.number("1"));
                        continue;
                    }
                    object v_action__422216 = F_c2_move_action(F_dv_get(v_actor, "id"), F_dv_get(v_node__421744, "room"), v_destination__421870, F_dv_get(v_edge__421837, "gate"));
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_known_route_uncached.1096", v_actor)))
                    {
                        v_action__422216 = F_c2_carry_moran_action(F_dv_get(v_node__421744, "room"), v_destination__421870, F_dv_get(v_edge__421837, "gate"));
                    }
                    object v_adoption__422778 = F_dv_find(F_dv_get(F_dv_get(v_actor, "mind"), "adoptions"), F_dv_get(v_action__422216, "predict_law"), false);
                    object adoptionStatus = R.Bool(R.eq(v_adoption__422778, false)) ? "missing" :
                        (!R.Bool(F_dv_get(v_adoption__422778, "active")) ? "inactive" : R.Bool(R.eq(F_dv_get(v_adoption__422778, "mode"), "predict")) ? "active-predict" : "other-mode");
                    _ = R.push(routeChecks, new List<object> { F_dv_pair("from", F_dv_get(v_node__421744, "room")), F_dv_pair("to", v_destination__421870), F_dv_pair("gate", F_dv_get(v_edge__421837, "gate")), F_dv_pair("gate_truth", gateTruth), F_dv_pair("adoption", adoptionStatus) });
                    if (R.Bool((R.Bool((R.Bool(R.not(R.eq(v_adoption__422778, false))) ? (object)(F_dv_get(v_adoption__422778, "active")) : (object)(false))) ? (object)(R.eq(F_dv_get(v_adoption__422778, "mode"), "predict")) : (object)(false))))
                    {
                        object v_first__422961 = F_dv_get(v_node__421744, "first");
                        if (R.Bool(R.eq(v_first__422961, "")))
                        {
                            v_first__422961 = v_destination__421870;
                        }
                        object v_basis__423087 = F_dv_append(F_dv_get(v_node__421744, "basis"), new List<object> { F_dv_pair("method", F_dv_get(v_action__422216, "id")), F_dv_pair("adoption_basis", F_dv_get(v_adoption__422778, "basis")), F_dv_pair("gate", F_campaign_access(F_dv_get(v_edge__421837, "gate"))), F_dv_pair("gate_cell", F_dv_read(F_dv_get(v_actor, "mind"), F_campaign_access(F_dv_get(v_edge__421837, "gate")))) });
                        if (R.Bool(R.eq(v_destination__421870, v_target)))
                        {
                            routeDiagnostic = F_dv_with_many(routeDiagnostic, new List<object> { F_dv_pair("reason", "route-found"), F_dv_pair("next", v_first__422961), F_dv_pair("checks", routeChecks) });
                            return DvValueTemplate.Instantiate("template.inline.c2i_known_route_uncached.1097", v_position__421388, v_first__422961, v_target, v_basis__423087);
                        }
                        _ = R.push(v_queue__421549, new List<object> { F_dv_pair("room", v_destination__421870), F_dv_pair("first", v_first__422961), F_dv_pair("basis", v_basis__423087) });
                        _ = R.push(v_visited__421619, v_destination__421870);
                    }
                }
                v_i__421778 = R.add(v_i__421778, R.number("1"));
            }
            v_cursor__421655 = R.add(v_cursor__421655, R.number("1"));
        }
        routeDiagnostic = F_dv_with(routeDiagnostic, "checks", routeChecks);
        return false;
    }
    public static object F_c2i_closing_navigation(object v_actor) => F_c2i_closing_navigation(v_actor, out _);
    public static object F_c2i_closing_navigation(object v_actor, out object navigationDiagnostic) {
        object routeRows = new List<object>();
        navigationDiagnostic = new List<object> { F_dv_pair("reason", "condition-not-known-true"), F_dv_pair("routes", routeRows) };
        object v_when__423933 = F_campaign_any(F_dv_fact(F_campaign_ref(Authored("m50.c2_inspection_presentation.literal_0015"), Authored("m50.c2_inspection_presentation.literal_0025")), true), F_dv_at_least(F_campaign_clock(), Authored("m50.c2_navigation_goals.literal")));
        if (R.Bool(R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), v_when__423933), "true"))))
        {
            return false;
        }
        object v_venues__424330 = AuthoredCopy("m50.c2i_closing_navigation.venues");
        object v_completed__424436 = F_dv_find(v_actor, Authored("m50.c2_personal_goals.literal_0006"), new List<object> {  });
        object v_i__424551 = R.number("0");
        while (R.Bool(R.lt(v_i__424551, R.count(v_venues__424330))))
        {
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2i_closing_navigation.1098", v_completed__424436, v_actor, v_venues__424330, v_i__424551)))
            {
                navigationDiagnostic = F_dv_with(navigationDiagnostic, "reason", "already-completed");
                return false;
            }
            v_i__424551 = R.add(v_i__424551, R.number("1"));
        }
        navigationDiagnostic = F_dv_with(navigationDiagnostic, "reason", "no-known-route");
        object v_best__424787 = false;
        object v_distance__424809 = R.sub(R.number("0"), R.number("1"));
        v_i__424551 = R.number("0");
        while (R.Bool(R.lt(v_i__424551, R.count(v_venues__424330))))
        {
            object v_room__424880 = R.at(v_venues__424330, v_i__424551);
            if (R.Bool(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_campaign_at(F_dv_get(v_actor, "id"), v_room__424880)), "true")))
            {
                navigationDiagnostic = F_dv_with_many(navigationDiagnostic, new List<object> { F_dv_pair("reason", "already-at-venue"), F_dv_pair("target", v_room__424880), F_dv_pair("routes", routeRows) });
                return DvValueTemplate.Instantiate("template.inline.c2i_closing_navigation.1099", v_room__424880);
            }
            object v_route__425157 = F_c2i_known_route(v_actor, v_room__424880, out object routeDiagnostic);
            _ = R.push(routeRows, routeDiagnostic);
            if (R.Bool((R.Bool(R.not(R.eq(v_route__425157, false))) ? (object)((R.Bool(R.lt(v_distance__424809, R.number("0"))) ? (object)(true) : (object)(R.lt(R.count(F_dv_get(v_route__425157, "path_basis")), v_distance__424809)))) : (object)(false))))
            {
                v_best__424787 = v_route__425157;
                v_distance__424809 = R.count(F_dv_get(v_route__425157, "path_basis"));
            }
            v_i__424551 = R.add(v_i__424551, R.number("1"));
        }
        navigationDiagnostic = F_dv_with(navigationDiagnostic, "routes", routeRows);
        if (!R.Bool(R.eq(v_best__424787, false))) navigationDiagnostic = F_dv_with_many(navigationDiagnostic, new List<object> { F_dv_pair("reason", "route-found"), F_dv_pair("target", F_dv_get(v_best__424787, "target")), F_dv_pair("next", F_dv_get(v_best__424787, "next")) });
        return v_best__424787;
    }
    public static object F_c2g_review_correction_law() {
        return DvValueTemplate.Instantiate("template.c2g_review_correction_law");
    }
    public static object F_c2g_enabled(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2g_enabled", v_actor);
    }
    public static object F_c2g_work(object v_id) {
        if (R.Bool(R.eq(v_id, "")))
        {
            return false;
        }
        object v_key__426760 = DvValueTemplate.Instantiate("template.inline.c2g_work.1100", v_id);
        object v_cached__426841 = R.memo_get(new List<object> {  }, v_key__426760);
        if (R.Bool(R.at(v_cached__426841, R.number("0"))))
        {
            return R.at(v_cached__426841, R.number("1"));
        }
        object v_specs__426925 = F_c2_work_specs();
        object v_i__426958 = R.number("0");
        while (R.Bool(R.lt(v_i__426958, R.count(v_specs__426925))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(v_specs__426925, v_i__426958), "id"), v_id)))
            {
                return R.memo_put(new List<object> {  }, v_key__426760, (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(R.at(v_specs__426925, v_i__426958), "room"), Authored("m50.c2i_rows_build.literal_0005"))) ? (object)(true) : (object)(R.eq(F_dv_get(R.at(v_specs__426925, v_i__426958), "field"), Authored("m50.c2i_rows_build.literal_0039"))))) ? (object)(true) : (object)(R.eq(F_dv_get(R.at(v_specs__426925, v_i__426958), "field"), Authored("m50.c2_total_store_open_known.literal_0025"))))) ? (object)(true) : (object)(R.eq(F_dv_get(R.at(v_specs__426925, v_i__426958), "field"), Authored("m50.c2_total_store_open_known.literal")))));
            }
            v_i__426958 = R.add(v_i__426958, R.number("1"));
        }
        return false;
    }
    public static object F_c2g_without_clock(object v_condition) {
        if (R.Bool((R.Bool(F_dv_has_key(v_condition, "ref")) ? (object)(F_dv_same(F_dv_get(v_condition, "ref"), F_campaign_clock())) : (object)(false))))
        {
            return F_dv_true();
        }
        object v_op__427641 = F_dv_find(v_condition, Authored("m50.c2_navigation_condition.literal"), "");
        if (R.Bool((R.Bool(R.eq(v_op__427641, "all")) ? (object)(true) : (object)(R.eq(v_op__427641, "any")))))
        {
            object v_items__427794 = new List<object> {  };
            object v_i__427818 = R.number("0");
            while (R.Bool(R.lt(v_i__427818, R.count(F_dv_get(v_condition, "items")))))
            {
                _ = R.push(v_items__427794, F_c2g_without_clock(R.at(F_dv_get(v_condition, "items"), v_i__427818)));
                v_i__427818 = R.add(v_i__427818, R.number("1"));
            }
            if (R.Bool(R.eq(v_op__427641, "all")))
            {
                return F_dv_all(v_items__427794);
            }
            object v_negative__428032 = new List<object> {  };
            v_i__427818 = R.number("0");
            while (R.Bool(R.lt(v_i__427818, R.count(v_items__427794))))
            {
                _ = R.push(v_negative__428032, F_dv_not(R.at(v_items__427794, v_i__427818)));
                v_i__427818 = R.add(v_i__427818, R.number("1"));
            }
            return F_dv_not(F_dv_all(v_negative__428032));
        }
        if (R.Bool(R.eq(v_op__427641, "not")))
        {
            if (R.Bool((R.Bool(F_dv_has_key(F_dv_get(v_condition, "item"), "ref")) ? (object)(F_dv_same(F_dv_get(F_dv_get(v_condition, "item"), "ref"), F_campaign_clock())) : (object)(false))))
            {
                return F_dv_true();
            }
            return F_dv_not(F_c2g_without_clock(F_dv_get(v_condition, "item")));
        }
        return v_condition;
    }
    public static object F_c2g_request_receipt(object v_actor,object v_job) {
        return DvValueTemplate.Instantiate("template.c2g_request_receipt", v_actor, v_job);
    }
    public static object F_c2g_work_heard(object v_actor,object v_job) {
        return F_c2_received_requirement(v_actor, F_c2g_request_receipt(v_actor, v_job));
    }
    public static object F_c2g_without_consent(object v_condition,object v_actor,object v_job) {
        if (R.Bool(F_dv_same(v_condition, F_c2_work_accepted(v_actor, v_job))))
        {
            return F_dv_true();
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2g_without_consent.1101", v_condition)))
        {
            object v_items__429492 = new List<object> {  };
            object v_i__429516 = R.number("0");
            while (R.Bool(R.lt(v_i__429516, R.count(F_dv_get(v_condition, "items")))))
            {
                _ = R.push(v_items__429492, F_c2g_without_consent(R.at(F_dv_get(v_condition, "items"), v_i__429516), v_actor, v_job));
                v_i__429516 = R.add(v_i__429516, R.number("1"));
            }
            return F_dv_all(v_items__429492);
        }
        return v_condition;
    }
    public static object F_c2g_granary_available(object v_actor) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2g_granary_available.1102", v_actor)))
        {
            return DvValueTemplate.Instantiate("template.inline.c2g_granary_available.1103");
        }
        object v_jobs__430422 = AuthoredCopy("m50.c2g_granary_available.jobs");
        object v_i__430523 = R.number("0");
        while (R.Bool(R.lt(v_i__430523, R.count(v_jobs__430422))))
        {
            if (R.Bool(F_c2g_work_heard(v_actor, R.at(v_jobs__430422, v_i__430523))))
            {
                return F_dv_true();
            }
            v_i__430523 = R.add(v_i__430523, R.number("1"));
        }
        return F_dv_not(F_dv_true());
    }
    public static object F_c2g_review_requested(object v_actor,object v_target) {
        return DvValueTemplate.Instantiate("template.c2g_review_requested", v_actor, v_target);
    }
    public static object F_c2g_review_heard(object v_actor) {
        if (R.Bool(F_c2_received_requirement(v_actor, DvValueTemplate.Instantiate("template.inline.c2g_review_heard.1104", v_actor))))
        {
            return true;
        }
        if (R.Bool(R.not(F_c2g_enabled(v_actor))))
        {
            return false;
        }
        object v_rooms__431682 = F_c2_rooms();
        object v_i__431710 = R.number("0");
        while (R.Bool(R.lt(v_i__431710, R.count(v_rooms__431682))))
        {
            if (R.Bool(F_c2_received_requirement(v_actor, DvValueTemplate.Instantiate("template.inline.c2g_review_heard.1105", v_actor, v_rooms__431682, v_i__431710))))
            {
                return true;
            }
            v_i__431710 = R.add(v_i__431710, R.number("1"));
        }
        return false;
    }
    public static object F_c2g_refresh_inventory_review(object v_actor) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2g_refresh_inventory_review.1106", v_actor)))
        {
            return v_actor;
        }
        object v_contributed__432634 = Authored("m50.c2i_rows_build.literal_0045");
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2g_refresh_inventory_review.1107", v_actor)))
        {
            v_contributed__432634 = Authored("m50.c2i_rows_build.literal_0004");
        }
        object v_requests__432917 = new List<object> {  };
        object v_receipts__432940 = F_dv_find(v_actor, Authored("m50.c2g_refresh_inventory_review.literal"), new List<object> {  });
        object v_rooms__433060 = F_c2_rooms();
        object v_i__433088 = R.number("0");
        while (R.Bool(R.lt(v_i__433088, R.count(v_receipts__432940))))
        {
            object v_receipt__433142 = R.at(v_receipts__432940, v_i__433088);
            object v_matches__433177 = (R.Bool(R.eq(F_dv_get(v_receipt__433142, "speaker"), Authored("m50.c2_norms_build.literal_0074"))) ? (object)(R.eq(F_dv_get(v_receipt__433142, "action_id"), R.cat(Authored("m50.c2g_review_requested.literal"), F_dv_get(v_actor, "id")))) : (object)(false));
            object v_r__433405 = R.number("0");
            while (R.Bool(R.lt(v_r__433405, R.count(v_rooms__433060))))
            {
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2g_refresh_inventory_review.1108", v_receipt__433142, v_actor, v_rooms__433060, v_r__433405)))
                {
                    v_matches__433177 = true;
                }
                v_r__433405 = R.add(v_r__433405, R.number("1"));
            }
            if (R.Bool(v_matches__433177))
            {
                _ = R.push(v_requests__432917, F_dv_get(v_receipt__433142, "parse_event"));
            }
            v_i__433088 = R.add(v_i__433088, R.number("1"));
        }
        object v_fields__433928 = AuthoredCopy("m50.c2g_refresh_inventory_review.fields");
        object v_known__434040 = new List<object> {  };
        v_i__433088 = R.number("0");
        while (R.Bool(R.lt(v_i__433088, R.count(v_fields__433928))))
        {
            _ = R.push(v_known__434040, R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_fact(F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0008"), R.at(v_fields__433928, v_i__433088)), true)), "true"));
            v_i__433088 = R.add(v_i__433088, R.number("1"));
        }
        object v_inputs__434303 = DvValueTemplate.Instantiate("template.inline.c2g_refresh_inventory_review.1109", v_actor, v_contributed__432634, v_requests__432917, v_known__434040);
        object v_previous__434612 = F_dv_find(v_actor, Authored("m50.c2i_goals.literal_0009"), false);
        if (R.Bool((R.Bool(R.not(R.eq(v_previous__434612, false))) ? (object)(F_dv_same(F_dv_get(v_previous__434612, "inputs"), v_inputs__434303)) : (object)(false))))
        {
            return v_actor;
        }
        return DvValueTemplate.Instantiate("template.inline.c2g_refresh_inventory_review.1110", v_actor, v_inputs__434303);
    }
    public static object F_c2g_storyflow_action(object v_actor,object v_original) {
        object v_action__434999 = v_original;
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2g_storyflow_action.1111", v_action__434999, v_actor)))
        {
            return F_dv_with(v_action__434999, "precondition", F_dv_not(F_dv_true()));
        }
        if (R.Bool(R.not(F_c2g_enabled(v_actor))))
        {
            return v_action__434999;
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2g_storyflow_action.1112", v_action__434999)))
        {
            object v_request__435446 = F_dv_get(F_dv_get(v_action__434999, "speech"), "request");
            object v_resolved__435491 = false;
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2g_storyflow_action.1113", v_request__435446)))
            {
                v_resolved__435491 = R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_fact(F_campaign_own(Authored("m50.c2_norms_build.literal_0013"), Authored("m50.c2i_actions.literal")), true)), "true");
            }
            else
            {
                object v_specs__435867 = F_c2_work_specs();
                object v_i__435908 = R.number("0");
                while (R.Bool(R.lt(v_i__435908, R.count(v_specs__435867))))
                {
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2g_storyflow_action.1114", v_request__435446, v_specs__435867, v_i__435908)))
                    {
                        v_resolved__435491 = R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_c2_work_accepted(F_dv_get(R.at(v_specs__435867, v_i__435908), "actor"), F_dv_get(R.at(v_specs__435867, v_i__435908), "id"))), "true");
                    }
                    v_i__435908 = R.add(v_i__435908, R.number("1"));
                }
            }
            if (R.Bool(v_resolved__435491))
            {
                return F_dv_with(v_action__434999, "precondition", F_dv_not(F_dv_true()));
            }
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2g_storyflow_action.1115", v_action__434999)))
        {
            object v_choice__436603 = F_dv_get(v_action__434999, "personal_choice");
            v_action__434999 = F_dv_with(v_action__434999, "personal_choice", F_dv_with(v_choice__436603, "sacrifices", new List<object> {  }));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2g_storyflow_action.1116", v_action__434999)))
        {
            return F_dv_with(v_action__434999, "precondition", F_dv_not(F_dv_true()));
        }
        object v_job__436909 = F_dv_find(v_action__434999, "work_decision", F_dv_find(v_action__434999, "work_operation", F_dv_find(v_action__434999, "work_reply", "")));
        if (R.Bool((R.Bool(F_c2g_work(v_job__436909)) ? (object)(F_dv_find(v_action__434999, Authored("m50.c2g_storyflow_action.literal_0006"), false)) : (object)(false))))
        {
            v_action__434999 = F_dv_with(v_action__434999, "received_speech_requirement", F_c2g_request_receipt(v_actor, v_job__436909));
        }
        object v_affected__437253 = F_c2g_work(v_job__436909);
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2g_storyflow_action.1117", v_action__434999)))
        {
            v_affected__437253 = true;
            v_action__434999 = F_dv_with(v_action__434999, "precondition", F_c2g_without_consent(F_dv_get(v_action__434999, "precondition"), F_dv_get(v_actor, "id"), F_dv_get(v_action__434999, "id")));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2g_storyflow_action.1118", v_action__434999)))
        {
            v_affected__437253 = true;
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2g_storyflow_action.1119", v_actor, v_action__434999)))
        {
            v_affected__437253 = true;
        }
        if (R.Bool(v_affected__437253))
        {
            v_action__434999 = F_dv_with(v_action__434999, "precondition", F_c2g_without_clock(F_dv_get(v_action__434999, "precondition")));
            if (R.Bool(F_dv_has_key(v_action__434999, "personal_choice")))
            {
                v_action__434999 = F_dv_with(v_action__434999, "personal_choice", F_dv_with(F_dv_get(v_action__434999, "personal_choice"), "requires", F_c2g_without_clock(F_dv_get(F_dv_get(v_action__434999, "personal_choice"), "requires"))));
            }
            if (R.Bool((R.Bool(F_dv_has_key(v_action__434999, "work_decision")) ? (object)(F_dv_has_key(v_action__434999, "received_speech_requirement")) : (object)(false))))
            {
                v_action__434999 = F_dv_with(v_action__434999, "received_speech_requirement", F_c2g_request_receipt(v_actor, v_job__436909));
            }
        }
        return v_action__434999;
    }
    public static object F_c2_work_accepted(object v_actor,object v_id) {
        return DvValueTemplate.Instantiate("template.c2_work_accepted", v_actor, v_id);
    }
    public static object F_c2_work_decision(object v_item,object v_stance,object v_source) {
        return DvValueTemplate.Instantiate("template.c2_work_decision", v_item, v_stance, v_source);
    }
    public static object F_c2_guard_decision(object v_actor,object v_stance,object v_source) {
        return DvValueTemplate.Instantiate("template.c2_guard_decision", v_actor, v_stance, v_source);
    }
    public static object F_c2_partner_already_decided(object v_actor,object v_kind) {
        return DvValueTemplate.Instantiate("template.c2_partner_already_decided", v_actor, v_kind);
    }
    public static object F_c2_work_specs() {
        object v_key__442587 = AuthoredCopy("m50.c2_work_specs.key");
        object v_cached__442678 = R.memo_get(new List<object> {  }, v_key__442587);
        if (R.Bool(R.at(v_cached__442678, R.number("0"))))
        {
            return R.at(v_cached__442678, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__442587, F_c2_work_specs_build());
    }
    public static object F_c2_work_specs_build() {
        return DvValueTemplate.Instantiate("template.c2_work_specs_build");
    }
    public static object F_c2_work_purpose(object v_field) {
        return DvValueTemplate.Instantiate("template.c2_work_purpose", v_field);
    }
    public static object F_c2_quer_restriction() {
        return DvValueTemplate.Instantiate("template.c2_quer_restriction");
    }
    public static object F_c2_quer_restricted(object v_actor) {
        object v_heard__445807 = F_c2_received_requirement(v_actor, AuthoredCopy("m50.c2_quer_restricted.literal"));
        return (R.Bool((R.Bool(v_heard__445807) ? (object)(true) : (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_c2_quer_restriction()), "true")))) ? (object)(R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_fact(F_campaign_own(Authored("m50.c2_norms_build.literal_0013"), Authored("m50.c2i_actions.literal")), true)), "true"))) : (object)(false));
    }
    public static object F_c2_quer_concerned(object v_actor) {
        return (R.Bool((R.Bool(F_c2_quer_restricted(v_actor)) ? (object)(true) : (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_campaign_case_claim(Authored("m50.c2i_rows_build.literal_0047"), Authored("m50.c2_work_decision.literal"))), "true")))) ? (object)(R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_fact(F_campaign_own(Authored("m50.c2_norms_build.literal_0013"), Authored("m50.c2i_actions.literal")), true)), "true"))) : (object)(false));
    }
    public static object F_c2_cooperation_basis(object v_actor,object v_basis) {
        return DvValueTemplate.Instantiate("template.c2_cooperation_basis", v_actor, v_basis);
    }
    public static object F_c2_document_coordination_rows() {
        return DvValueTemplate.Instantiate("template.c2_document_coordination_rows");
    }
    public static object F_c2_cooperation_rows() {
        object v_rows__453988 = F_c2_document_coordination_rows();
        object v_source__454036 = F_campaign_approved(Authored("m50.c2_cooperation_rows.literal"));
        object v_early__454144 = F_campaign_window(Authored("m50.c2_any_night.literal"), Authored("m50.c2_cooperation_rows.literal_0002"), Authored("m50.c2i_rows_build.literal"));
        object v_offer_when__454386 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1120", v_early__454144);
        object v_offer__454550 = F_c2d_row(Authored("m50.c2_supply_actions.literal_0027"), Authored("m50.c2_partner_already_decided.literal_0007"), Authored("m50.c2_norms_build.literal_0048"), v_offer_when__454386, F_dv_true(), Authored("m50.c2_cooperation_rows.literal_0003"), Authored("m50.c2_cooperation_rows.literal_0004"), v_source__454036);
        v_offer__454550 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1121", v_offer__454550, v_early__454144, v_source__454036);
        _ = R.push(v_rows__453988, v_offer__454550);
        object v_accept_when__455937 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1122", v_early__454144);
        object v_accept__456097 = F_c2d_row(Authored("m50.c2_norms_build.literal_0074"), Authored("m50.c2_partner_already_decided.literal_0003"), Authored("m50.c2_norms_build.literal_0048"), v_accept_when__455937, F_dv_true(), Authored("m50.c2_cooperation_rows.literal_0008"), Authored("m50.c2_cooperation_rows.literal_0009"), v_source__454036);
        v_accept__456097 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1123", v_accept__456097, v_early__454144, v_source__454036);
        _ = R.push(v_rows__453988, v_accept__456097);
        object v_medicine_hosts__457478 = AuthoredCopy("m50.c2sf_ceremony_ids.hosts");
        object v_medicine_guests__457586 = AuthoredCopy("m50.c2sf_ceremony_ids.guests");
        object v_medicine_meeting__457696 = F_campaign_window(Authored("m50.c2_any_night.literal_0002"), Authored("m50.c2i_goals.literal_0019"), Authored("m50.c2i_rows_build.literal_0003"));
        object v_invitation_rooms__457949 = AuthoredCopy("m50.c2sf_ceremony_ids.rooms");
        object v_host_index__458059 = R.number("0");
        while (R.Bool(R.lt(v_host_index__458059, R.count(v_medicine_hosts__457478))))
        {
            object v_guest_index__458137 = R.number("0");
            while (R.Bool(R.lt(v_guest_index__458137, R.count(v_medicine_guests__457586))))
            {
                object v_room_index__458226 = R.number("0");
                while (R.Bool(R.lt(v_room_index__458226, R.count(v_invitation_rooms__457949))))
                {
                    object v_host__458322 = R.at(v_medicine_hosts__457478, v_host_index__458059);
                    object v_guest__458377 = R.at(v_medicine_guests__457586, v_guest_index__458137);
                    object v_room__458435 = R.at(v_invitation_rooms__457949, v_room_index__458226);
                    object v_invite_id__458492 = R.cat(v_host__458322, R.cat(Authored("m50.c2sf_ceremony_ids.literal_0003"), R.cat(v_guest__458377, R.cat(Authored("m50.c2_move_action.literal_0002"), v_room__458435))));
                    object v_invitation__458722 = F_c2d_row(v_host__458322, v_invite_id__458492, v_room__458435, v_medicine_meeting__457696, F_dv_true(), Authored("m50.c2_cooperation_rows.literal_0012"), Authored("m50.c2_cooperation_rows.literal_0013"), v_source__454036);
                    v_invitation__458722 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1124", v_invitation__458722, v_guest__458377, v_room__458435);
                    _ = R.push(v_rows__453988, v_invitation__458722);
                    v_room_index__458226 = R.add(v_room_index__458226, R.number("1"));
                }
                v_guest_index__458137 = R.add(v_guest_index__458137, R.number("1"));
            }
            v_host_index__458059 = R.add(v_host_index__458059, R.number("1"));
        }
        object v_work__459505 = F_c2_work_specs();
        object v_i__459537 = R.number("0");
        while (R.Bool(R.lt(v_i__459537, R.count(v_work__459505))))
        {
            object v_item__459587 = R.at(v_work__459505, v_i__459537);
            object v_decided__459615 = F_campaign_own(F_dv_get(v_item__459587, "actor"), R.cat(Authored("m50.c2_initial_body.literal_0007"), F_dv_get(v_item__459587, "id")));
            object v_undecided__459753 = F_dv_fact(v_decided__459615, false);
            object v_item_active__459802 = F_dv_all(new List<object> { F_dv_get(v_item__459587, "when"), v_undecided__459753 });
            object v_take__459860 = F_c2d_row(F_dv_get(v_item__459587, "actor"), R.cat(F_dv_get(v_item__459587, "id"), Authored("m50.c2sf_ceremony_ids.literal")), F_dv_get(v_item__459587, "room"), v_item_active__459802, F_dv_true(), R.cat(Authored("m50.c2_cooperation_rows.literal_0015"), F_dv_get(v_item__459587, "purpose")), R.cat(Authored("m50.c2_cooperation_rows.literal_0016"), F_dv_get(v_item__459587, "purpose")), v_source__454036);
            object v_promotes__460228 = AuthoredCopy("m50.c2_cooperation_rows.promotes");
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1125", v_item__459587)))
            {
                v_promotes__460228 = AuthoredCopy("m50.c2i_evening_actions.literal_0007");
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1126", v_item__459587)))
            {
                v_promotes__460228 = AuthoredCopy("m50.c2_cooperation_rows.literal_0017");
            }
            v_take__459860 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1127", v_take__459860, v_decided__459615, v_item__459587, v_source__454036, v_item_active__459802, v_promotes__460228);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1128", v_item__459587)))
            {
                v_take__459860 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1129", v_take__459860, v_item__459587);
            }
            _ = R.push(v_rows__453988, v_take__459860);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1130", v_item__459587)))
            {
                object v_refuse__462489 = F_c2d_row(Authored("m50.c2_supply_actions.literal_0027"), R.cat(F_dv_get(v_item__459587, "id"), Authored("m50.c2_cooperation_rows.literal_0018")), F_dv_get(v_item__459587, "room"), v_item_active__459802, F_dv_true(), R.cat(Authored("m50.c2_cooperation_rows.literal_0019"), F_dv_get(v_item__459587, "purpose")), Authored("m50.c2_cooperation_rows.literal_0020"), v_source__454036);
                v_refuse__462489 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1131", v_refuse__462489, v_item__459587, v_source__454036, v_decided__459615, v_item_active__459802);
                _ = R.push(v_rows__453988, v_refuse__462489);
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1132", v_item__459587)))
            {
                object v_requester__464469 = Authored("m50.c2_norms_build.literal_0074");
                if (R.Bool(R.not(R.eq(v_requester__464469, F_dv_get(v_item__459587, "actor")))))
                {
                    object v_invite__464614 = F_c2d_row(v_requester__464469, R.cat(F_dv_get(v_item__459587, "id"), Authored("m50.c2sf_ceremony_ids.literal_0002")), F_dv_get(v_item__459587, "room"), v_item_active__459802, F_dv_true(), R.cat(Authored("m50.c2_cooperation_rows.literal_0023"), F_dv_get(v_item__459587, "purpose")), R.cat(Authored("m50.c2_cooperation_rows.literal_0024"), F_dv_get(v_item__459587, "purpose")), v_source__454036);
                    v_invite__464614 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1133", v_invite__464614, v_item__459587);
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1134", v_item__459587)))
                    {
                        v_invite__464614 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1135", v_invite__464614);
                    }
                    _ = R.push(v_rows__453988, v_invite__464614);
                }
            }
            v_i__459537 = R.add(v_i__459537, R.number("1"));
        }
        object v_reviewed_granary__465845 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1136");
        object v_meeting_rooms__466232 = F_c2_rooms();
        object v_meeting_index__466268 = R.number("0");
        while (R.Bool(R.lt(v_meeting_index__466268, R.count(v_meeting_rooms__466232))))
        {
            object v_job_index__466351 = R.number("0");
            while (R.Bool(R.lt(v_job_index__466351, R.count(v_work__459505))))
            {
                object v_job__466425 = R.at(v_work__459505, v_job_index__466351);
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1137", v_job__466425)))
                {
                    object v_room__466760 = R.at(v_meeting_rooms__466232, v_meeting_index__466268);
                    object v_request__466817 = F_c2d_row(Authored("m50.c2_norms_build.literal_0074"), R.cat(F_dv_get(v_job__466425, "id"), R.cat(Authored("m50.c2g_request_receipt.literal"), v_room__466760)), v_room__466760, DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1138", v_reviewed_granary__465845), F_dv_true(), Authored("m50.c2_cooperation_rows.literal_0025"), R.cat(Authored("m50.c2_cooperation_rows.literal_0026"), F_dv_get(v_job__466425, "purpose")), v_source__454036);
                    v_request__466817 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1139", v_request__466817, v_job__466425, v_room__466760);
                    _ = R.push(v_rows__453988, v_request__466817);
                }
                v_job_index__466351 = R.add(v_job_index__466351, R.number("1"));
            }
            v_meeting_index__466268 = R.add(v_meeting_index__466268, R.number("1"));
        }
        object v_reviewed__467945 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1140");
        object v_reviewers__468344 = AuthoredCopy("m50.c2_cooperation_rows.reviewers");
        object v_reviewer_index__468453 = R.number("0");
        while (R.Bool(R.lt(v_reviewer_index__468453, R.count(v_reviewers__468344))))
        {
            object v_reviewer__468534 = R.at(v_reviewers__468344, v_reviewer_index__468453);
            object v_invitation__468584 = F_c2d_row(Authored("m50.c2_norms_build.literal_0074"), R.cat(Authored("m50.c2g_review_requested.literal"), v_reviewer__468534), Authored("m50.c2_norms_build.literal_0004"), v_reviewed__467945, F_dv_true(), Authored("m50.c2_cooperation_rows.literal_0027"), Authored("m50.c2_cooperation_rows.literal_0028"), v_source__454036);
            v_invitation__468584 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1141", v_invitation__468584, v_reviewer__468534);
            _ = R.push(v_rows__453988, v_invitation__468584);
            v_reviewer_index__468453 = R.add(v_reviewer_index__468453, R.number("1"));
        }
        object v_withdraw__469505 = F_c2d_row(Authored("m50.c2_norms_build.literal_0013"), Authored("m50.c2g_storyflow_action.literal_0004"), Authored("m50.c2i_rows_build.literal_0005"), v_reviewed__467945, v_reviewed__467945, Authored("m50.c2_cooperation_rows.literal_0029"), Authored("m50.c2_cooperation_rows.literal_0030"), v_source__454036);
        v_withdraw__469505 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1142", v_withdraw__469505, v_reviewed__467945, v_source__454036);
        _ = R.push(v_rows__453988, v_withdraw__469505);
        object v_status__470933 = F_c2d_row(Authored("m50.c2_supply_actions.literal_0027"), Authored("m50.c2_cooperation_rows.literal_0032"), Authored("m50.c2i_rows_build.literal_0005"), F_dv_true(), F_dv_true(), Authored("m50.c2_cooperation_rows.literal_0033"), Authored("m50.c2_cooperation_rows.literal_0034"), v_source__454036);
        v_status__470933 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1143", v_status__470933);
        _ = R.push(v_rows__453988, v_status__470933);
        v_meeting_index__466268 = R.number("0");
        while (R.Bool(R.lt(v_meeting_index__466268, R.count(v_meeting_rooms__466232))))
        {
            v_reviewer_index__468453 = R.number("0");
            while (R.Bool(R.lt(v_reviewer_index__468453, R.count(v_reviewers__468344))))
            {
                object v_reviewer__471685 = R.at(v_reviewers__468344, v_reviewer_index__468453);
                object v_room__471739 = R.at(v_meeting_rooms__466232, v_meeting_index__466268);
                object v_invitation__471792 = F_c2d_row(Authored("m50.c2_norms_build.literal_0074"), R.cat(Authored("m50.c2g_review_requested.literal_0002"), R.cat(v_reviewer__471685, R.cat(Authored("m50.c2_move_action.literal_0002"), v_room__471739))), v_room__471739, v_reviewed__467945, F_dv_true(), Authored("m50.c2_cooperation_rows.literal_0036"), Authored("m50.c2_cooperation_rows.literal_0037"), v_source__454036);
                v_invitation__471792 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1144", v_invitation__471792, v_reviewer__471685, v_room__471739);
                _ = R.push(v_rows__453988, v_invitation__471792);
                v_reviewer_index__468453 = R.add(v_reviewer_index__468453, R.number("1"));
            }
            v_meeting_index__466268 = R.add(v_meeting_index__466268, R.number("1"));
        }
        object v_withdrawn__472779 = F_dv_fact(F_campaign_own(Authored("m50.c2_norms_build.literal_0013"), Authored("m50.c2i_actions.literal")), true);
        _ = R.push(v_rows__453988, DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1145", v_withdrawn__472779, v_source__454036));
        object v_people__473825 = F_campaign_people();
        object v_clerks__473861 = AuthoredCopy("m50.c2_cooperation_rows.clerks");
        object v_clerk_index__473964 = R.number("0");
        while (R.Bool(R.lt(v_clerk_index__473964, R.count(v_clerks__473861))))
        {
            object v_person_index__474036 = R.number("0");
            while (R.Bool(R.lt(v_person_index__474036, R.count(v_people__473825))))
            {
                object v_person__474118 = R.at(v_people__473825, v_person_index__474036);
                if (R.Bool(R.not(R.eq(v_person__474118, R.at(v_clerks__473861, v_clerk_index__473964)))))
                {
                    object v_pending__474216 = F_dv_at_least(F_campaign_ref(Authored("m50.c2_world_objects.literal_0009"), Authored("m50.c2_total_store_open_known.literal_0012")), R.number("1"));
                    object v_ask__474433 = F_c2d_row(R.at(v_clerks__473861, v_clerk_index__473964), R.cat(R.at(v_clerks__473861, v_clerk_index__473964), R.cat(Authored("m50.c2_cooperation_rows.literal_0043"), v_person__474118)), Authored("m50.c2_norms_build.literal_0034"), v_pending__474216, F_dv_true(), Authored("m50.c2_cooperation_rows.literal_0044"), Authored("m50.c2_cooperation_rows.literal_0045"), v_source__454036);
                    v_ask__474433 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1146", v_ask__474433, v_person__474118);
                    _ = R.push(v_rows__453988, v_ask__474433);
                }
                v_person_index__474036 = R.add(v_person_index__474036, R.number("1"));
            }
            v_clerk_index__473964 = R.add(v_clerk_index__473964, R.number("1"));
        }
        object v_person_index__475355 = R.number("0");
        while (R.Bool(R.lt(v_person_index__475355, R.count(v_people__473825))))
        {
            object v_person__475429 = R.at(v_people__473825, v_person_index__475355);
            object v_purposes__475472 = AuthoredCopy("m50.c2_cooperation_rows.purposes");
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1147", v_person__475429)))
            {
                v_purposes__475472 = AuthoredCopy("m50.c2_cooperation_rows.literal_0047");
            }
            object v_purpose_index__475792 = R.number("0");
            while (R.Bool(R.lt(v_purpose_index__475792, R.count(v_purposes__475472))))
            {
                object v_purpose__475878 = R.at(v_purposes__475472, v_purpose_index__475792);
                object v_pending__475929 = F_dv_at_least(F_campaign_own(v_person__475429, R.cat(Authored("m50.c2_total_store_open_known.literal_0014"), v_purpose__475878)), R.number("1"));
                object v_text__476093 = Authored("m50.c2_cooperation_rows.text");
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1148", v_purpose__475878)))
                {
                    v_text__476093 = Authored("m50.c2_cooperation_rows.literal_0048");
                }
                object v_reply__476395 = F_c2d_reply(v_person__475429, R.cat(v_person__475429, R.cat(Authored("m50.c2_cooperation_rows.literal_0049"), v_purpose__475878)), Authored("m50.c2_norms_build.literal_0034"), v_pending__475929, v_pending__475929, Authored("m50.c2_cooperation_rows.literal_0046"), Authored("m50.c2_cooperation_rows.literal_0050"), v_text__476093, v_source__454036);
                v_reply__476395 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1149", v_reply__476395, v_person__475429, v_purpose__475878);
                _ = R.push(v_rows__453988, v_reply__476395);
                v_purpose_index__475792 = R.add(v_purpose_index__475792, R.number("1"));
            }
            v_person_index__475355 = R.add(v_person_index__475355, R.number("1"));
        }
        object v_heard_color__477108 = AuthoredCopy("m50.c2i_goals.literal_0025");
        object v_color_time__477212 = F_campaign_window(Authored("m50.c2_any_night.literal_0003"), Authored("m50.c2_norm_available.literal"), Authored("m50.c2_night.literal_0002"));
        object v_question__477456 = F_c2d_row(Authored("m50.c2_norms_build.literal_0074"), Authored("m50.c2_cooperation_rows.literal_0051"), Authored("m50.c2_norms_build.literal_0048"), v_color_time__477212, F_dv_true(), Authored("m50.c2_cooperation_rows.literal_0052"), Authored("m50.c2_cooperation_rows.literal_0053"), v_source__454036);
        v_question__477456 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1150", v_question__477456, v_heard_color__477108);
        _ = R.push(v_rows__453988, v_question__477456);
        object v_color_fact__478235 = F_dv_fact(F_c2daily_ref(Authored("m50.c2_cooperation_rows.literal_0056")), Authored("m50.c2_cooperation_rows.literal_0057"));
        object v_encourage__478432 = F_c2d_row(Authored("m50.c2_norms_build.literal_0074"), Authored("m50.c2_cooperation_rows.literal_0058"), Authored("m50.c2_norms_build.literal_0048"), v_color_time__477212, v_color_fact__478235, Authored("m50.c2_cooperation_rows.literal_0059"), F_c2_color_encouragement_text(), v_source__454036);
        object v_advice_id__478822 = Authored("m50.c2_cooperation_rows.advice_id");
        v_encourage__478432 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1151", v_encourage__478432, v_heard_color__477108, v_advice_id__478822, v_source__454036);
        _ = R.push(v_rows__453988, v_encourage__478432);
        object v_watch_states__480257 = AuthoredCopy("m50.c2_cooperation_rows.watch_states");
        object v_watch_index__480372 = R.number("0");
        while (R.Bool(R.lt(v_watch_index__480372, R.count(v_watch_states__480257))))
        {
            object v_watched__480450 = R.at(v_watch_states__480257, v_watch_index__480372);
            object v_assessment__480499 = R.cat(Authored("m50.c2_cooperation_rows.literal_0068"), R.show(v_watched__480450));
            object v_when__480627 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1152", v_watched__480450);
            object v_decisions__480913 = AuthoredCopy("m50.c2_cooperation_rows.decisions");
            object v_decision_index__481026 = R.number("0");
            while (R.Bool(R.lt(v_decision_index__481026, R.count(v_decisions__480913))))
            {
                object v_accepted__481115 = R.at(v_decisions__480913, v_decision_index__481026);
                object v_text__481169 = Authored("m50.c2_cooperation_rows.text_0002");
                if (R.Bool(R.not(v_accepted__481115)))
                {
                    v_text__481169 = Authored("m50.c2_cooperation_rows.literal_0069");
                }
                object v_reply__481396 = F_c2d_row(Authored("m50.c2_initial_body.literal_0009"), R.cat(Authored("m50.c2_cooperation_rows.literal_0070"), R.cat(R.show(v_watched__480450), R.cat(Authored("m50.c2_move_action.literal_0002"), R.show(v_accepted__481115)))), Authored("m50.c2_norms_build.literal_0048"), v_when__480627, F_dv_true(), R.cat(F_dv_get(AuthoredCopy("m50.c2_cooperation_rows.literal_0071"), R.show(v_accepted__481115)), Authored("m50.c2_cooperation_rows.literal_0072")), v_text__481169, v_source__454036);
                v_reply__481396 = DvValueTemplate.Instantiate("template.inline.c2_cooperation_rows.1153", v_reply__481396, v_advice_id__478822, v_accepted__481115, v_assessment__480499);
                _ = R.push(v_rows__453988, v_reply__481396);
                v_decision_index__481026 = R.add(v_decision_index__481026, R.number("1"));
            }
            v_watch_index__480372 = R.add(v_watch_index__480372, R.number("1"));
        }
        return v_rows__453988;
    }
    public static object F_c2_meter_fields() {
        return DvValueTemplate.Instantiate("template.c2_meter_fields");
    }
    public static object F_c2_sample_claim(object v_cell) {
        object v_claims__482725 = F_dv_find(v_cell, Authored("m50.c2_sample_claim.literal"), new List<object> {  });
        object v_latest__482829 = false;
        object v_i__482853 = R.number("0");
        while (R.Bool(R.lt(v_i__482853, R.count(v_claims__482725))))
        {
            object v_claim__482905 = R.at(v_claims__482725, v_i__482853);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_sample_claim.1154", v_claim__482905, v_cell)))
            {
                if (R.Bool((R.Bool(R.eq(v_latest__482829, false)) ? (object)(true) : (object)(R.lt(F_dv_get(v_latest__482829, "time"), F_dv_get(v_claim__482905, "time"))))))
                {
                    v_latest__482829 = v_claim__482905;
                }
            }
            v_i__482853 = R.add(v_i__482853, R.number("1"));
        }
        return v_latest__482829;
    }
    public static object F_c2_meter_fresh(object v_actor) {
        object v_reading__483328 = F_dv_read(F_dv_get(v_actor, "mind"), F_campaign_ref(Authored("m50.c2_norm_condition.literal"), Authored("m50.c2_norm_condition.literal_0002")));
        object v_sampled__483526 = F_dv_read(F_dv_get(v_actor, "mind"), F_campaign_ref(Authored("m50.c2_norm_condition.literal"), Authored("m50.c2_norm_actions.literal_0037")));
        object v_location__483722 = F_dv_read(F_dv_get(v_actor, "mind"), F_campaign_ref(Authored("m50.c2_norm_condition.literal"), Authored("m50.c2_meter_fresh.literal")));
        object v_observed__483913 = F_dv_read(F_dv_get(v_actor, "mind"), F_campaign_ref(Authored("m50.c2_world_objects.literal_0021"), Authored("m50.c2_norm_actions.literal_0037")));
        if (R.Bool((R.Bool((R.Bool(R.not(F_dv_uncontested(v_reading__483328))) ? (object)(true) : (object)(R.not(F_dv_uncontested(v_sampled__483526))))) ? (object)(true) : (object)(R.not(F_dv_uncontested(v_location__483722))))))
        {
            return false;
        }
        object v_read_claim__484236 = F_c2_sample_claim(v_reading__483328);
        object v_epoch_claim__484283 = F_c2_sample_claim(v_sampled__483526);
        object v_location_claim__484331 = F_c2_sample_claim(v_location__483722);
        if (R.Bool((R.Bool((R.Bool(R.eq(v_read_claim__484236, false)) ? (object)(true) : (object)(R.eq(v_epoch_claim__484283, false)))) ? (object)(true) : (object)(R.eq(v_location_claim__484331, false)))))
        {
            return false;
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_meter_fresh.1155", v_location__483722, v_read_claim__484236, v_epoch_claim__484283, v_location_claim__484331)))
        {
            return false;
        }
        if (R.Bool((R.Bool(R.not(F_dv_same(R.at(F_dv_get(v_read_claim__484236, "id"), R.number("0")), R.at(F_dv_get(v_epoch_claim__484283, "id"), R.number("0"))))) ? (object)(true) : (object)(R.not(F_dv_same(R.at(F_dv_get(v_location_claim__484331, "id"), R.number("0")), R.at(F_dv_get(v_epoch_claim__484283, "id"), R.number("0"))))))))
        {
            return false;
        }
        if (R.Bool((R.Bool(F_dv_uncontested(v_observed__483913)) ? (object)(R.not(R.eq(F_dv_get(v_observed__483913, "value"), F_dv_get(v_sampled__483526, "value")))) : (object)(false))))
        {
            return false;
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_meter_fresh.1156", v_actor, v_read_claim__484236)))
        {
            return false;
        }
        return true;
    }
    public static object F_c2_safety_actions(object v_actor) {
        object v_actions__485104 = new List<object> {  };
        object v_source__485126 = F_campaign_approved(Authored("m50.c2_safety_actions.literal"));
        object v_fields__485232 = F_c2_meter_fields();
        object v_sample__485268 = F_c2_inspect_action(v_actor, Authored("m50.c2_norm_condition.literal"), Authored("m50.c2_world_objects.literal_0033"), v_fields__485232, Authored("m50.c2_safety_actions.literal_0002"), Authored("m50.c2_safety_actions.literal_0003"), v_source__485126);
        v_sample__485268 = DvValueTemplate.Instantiate("template.inline.c2_safety_actions.1157", v_sample__485268, v_actor);
        v_sample__485268 = DvValueTemplate.Instantiate("template.inline.c2_safety_actions.1158", v_sample__485268);
        object v_air_presentation__486003 = F_c2_inspection_presentation(v_actor, Authored("m50.c2_world_objects.literal_0021"), AuthoredCopy("m50.c2_safety_actions.literal_0005"));
        object v_completed_at__486229 = F_c2_visible_line(F_dv_true(), Authored("m50.c2_safety_actions.literal_0006"), DvValueTemplate.Instantiate("template.inline.c2_safety_actions.1159"), v_source__485126);
        v_sample__485268 = F_dv_with(v_sample__485268, "presentation", F_dv_with(F_dv_get(v_sample__485268, "presentation"), "lines", F_dv_append(F_c2m_concat(F_dv_get(F_dv_get(v_sample__485268, "presentation"), "lines"), F_dv_get(v_air_presentation__486003, "lines")), v_completed_at__486229)));
        _ = R.push(v_actions__485104, v_sample__485268);
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_safety_actions.1160", v_actor)))
        {
            object v_id__487045 = R.cat(v_actor, Authored("m50.c2_safety_actions.literal_0007"));
            object v_pre__487152 = F_dv_fact(F_campaign_own(v_actor, Authored("m50.c2_world_objects.literal_0040")), true);
            object v_law__487280 = DvValueTemplate.Instantiate("template.inline.c2_safety_actions.1161", v_pre__487152, v_actor);
            _ = R.push(v_actions__485104, DvValueTemplate.Instantiate("template.inline.c2_safety_actions.1162", v_id__487045, v_pre__487152, v_actor, v_source__485126));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_safety_actions.1163", v_actor)))
        {
            object v_id__488223 = R.cat(v_actor, Authored("m50.c2_safety_actions.literal_0011"));
            object v_pre__488330 = DvValueTemplate.Instantiate("template.inline.c2_safety_actions.1164", v_actor);
            object v_law__488816 = F_c2_guard_post_law(v_actor);
            object v_post__488860 = DvValueTemplate.Instantiate("template.inline.c2_safety_actions.1165", v_id__488223, v_pre__488330, v_law__488816, v_actor, v_source__485126);
            v_post__488860 = DvValueTemplate.Instantiate("template.inline.c2_safety_actions.1166", v_post__488860, v_actor, v_source__485126);
            v_post__488860 = DvValueTemplate.Instantiate("template.inline.c2_safety_actions.1167", v_post__488860, v_actor, v_pre__488330, v_source__485126);
            _ = R.push(v_actions__485104, DvValueTemplate.Instantiate("template.inline.c2_safety_actions.1168", v_post__488860, v_actor));
            object v_release_id__490366 = R.cat(v_actor, Authored("m50.c2_safety_actions.literal_0018"));
            object v_release_pre__490481 = DvValueTemplate.Instantiate("template.inline.c2_safety_actions.1169", v_actor);
            object v_release__490719 = DvValueTemplate.Instantiate("template.inline.c2_safety_actions.1170", v_release_id__490366, v_release_pre__490481, v_actor, v_source__485126);
            v_release__490719 = DvValueTemplate.Instantiate("template.inline.c2_safety_actions.1171", v_release__490719, v_actor, v_source__485126, v_release_pre__490481);
            _ = R.push(v_actions__485104, v_release__490719);
        }
        return v_actions__485104;
    }
    public static object F_c2_guard_post_law(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c2_guard_post_law", v_行动者);
    }

    public static object F_c2_received_requirement(object v_actor,object v_requirement) {
        object v_receipts__495007 = F_dv_find(v_actor, Authored("m50.c2g_refresh_inventory_review.literal"), new List<object> {  });
        if (R.Bool(F_dv_has_key(v_requirement, "any_of")))
        {
            object v_i__495174 = R.number("0");
            while (R.Bool(R.lt(v_i__495174, R.count(F_dv_get(v_requirement, "any_of")))))
            {
                if (R.Bool(F_dvp_received_speech_matches(v_receipts__495007, R.at(F_dv_get(v_requirement, "any_of"), v_i__495174))))
                {
                    return true;
                }
                v_i__495174 = R.add(v_i__495174, R.number("1"));
            }
            return false;
        }
        return F_dvp_received_speech_matches(v_receipts__495007, v_requirement);
    }
    public static object F_c2_owned_safety_notice(object v_actor) {
        if (R.Bool(F_c2_received_requirement(v_actor, AuthoredCopy("m50.c2i_goals.literal_0025"))))
        {
            return true;
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_owned_safety_notice.1178", v_actor)))
        {
            return true;
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_owned_safety_notice.1179", v_actor)))
        {
            return true;
        }
        return R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_fact(F_campaign_ref(Authored("m50.c2_norm_condition.literal"), Authored("m50.c2_norm_condition.literal_0002")), false)), "true");
    }
    public static object F_c2_owned_recheck_needed(object v_actor) {
        object v_seen__496301 = F_dv_read(F_dv_get(v_actor, "mind"), F_campaign_ref(Authored("m50.c2_norm_condition.literal"), Authored("m50.c2_norm_condition.literal_0002")));
        if (R.Bool(R.not(F_dv_uncontested(v_seen__496301))))
        {
            return true;
        }
        return R.not(F_c2_meter_fresh(v_actor));
    }
    public static object F_c2_refresh_content_actions(object v_actor) {
        v_actor = F_c2sf_actor(v_actor);
        v_actor = F_c2_query_view(v_actor, v_actor);
        v_actor = DvValueTemplate.Instantiate("template.c2_document_evidence_context", v_actor);
        object v_original_actions__496866 = F_dv_get(v_actor, "actions");
        object v_actions__496908 = new List<object> {  };
        object v_i__496930 = R.number("0");
        object v_notice__496945 = F_c2_owned_safety_notice(v_actor);
        object v_recheck__496993 = F_c2_owned_recheck_needed(v_actor);
        object v_adopted_stop__497043 = R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_fact(F_campaign_own(F_dv_get(v_actor, "id"), Authored("m50.c2_refresh_content_actions.literal")), true)), "true");
        bool storyflowEnabled = R.Bool(F_c2sf_enabled(v_actor));
        object? ceremonyIds = null;
        while (R.Bool(R.lt(v_i__496930, R.count(v_original_actions__496866))))
        {
            object v_action__497263 = R.at(v_original_actions__496866, v_i__496930);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1180", v_action__497263)))
            {
                object v_heard__497411 = Authored("m50.c2_refresh_content_actions.heard");
                if (R.Bool(F_c2_received_requirement(v_actor, AuthoredCopy("m50.c2_moran_outing_heard.literal_0004"))))
                {
                    v_heard__497411 = Authored("m50.c2_refresh_content_actions.literal_0002");
                }
                object v_speaker__497762 = Authored("m50.c2_initial_body.literal_0009");
                if (R.Bool(R.not(F_c2_received_requirement(v_actor, new List<object> { F_dv_pair("speaker", v_speaker__497762), F_dv_pair("action_id", v_heard__497411) }))))
                {
                    v_speaker__497762 = Authored("m50.c2_alive.literal");
                    v_heard__497411 = Authored("m50.c2_personal_goals.relay");
                    if (R.Bool(F_c2_received_requirement(v_actor, DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1181", v_speaker__497762))))
                    {
                        v_heard__497411 = Authored("m50.c2_personal_goals.literal_0007");
                    }
                }
                v_action__497263 = F_dv_with(v_action__497263, "received_speech_requirement", new List<object> { F_dv_pair("speaker", v_speaker__497762), F_dv_pair("action_id", v_heard__497411) });
            }
            if (R.Bool(F_dv_has_key(v_action__497263, "partner_decision")))
            {
                object v_base__498562 = F_dv_find(v_action__497263, Authored("m50.c2_refresh_content_actions.literal_0003"), F_dv_get(v_action__497263, "precondition"));
                object v_choice__498707 = F_dv_find(v_action__497263, Authored("m50.c2_refresh_content_actions.literal_0004"), F_dv_get(v_action__497263, "personal_choice"));
                object v_qualification__498857 = F_dv_true();
                if (R.Bool(F_c2_partner_already_decided(v_actor, F_dv_get(v_action__497263, "partner_decision"))))
                {
                    v_qualification__498857 = F_dv_not(F_dv_true());
                }
                v_action__497263 = F_dv_with_many(v_action__497263, new List<object> { F_dv_pair("owned_precondition_base", v_base__498562), F_dv_pair("owned_choice_base", v_choice__498707), F_dv_pair("precondition", F_dv_all(new List<object> { v_base__498562, v_qualification__498857 })), F_dv_pair("personal_choice", F_dv_with(v_choice__498707, "requires", F_dv_all(new List<object> { F_dv_get(v_choice__498707, "requires"), v_qualification__498857 }))) });
            }
            if (R.Bool(F_dv_has_key(v_action__497263, "work_reply")))
            {
                object v_request__499317 = F_dvs_pending_request(v_actor, DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1182", v_action__497263));
                if (R.Bool(R.lt(R.number("0"), R.count(v_request__499317))))
                {
                    v_action__497263 = DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1183", v_action__497263);
                }
                else
                {
                    v_action__497263 = F_dv_without(F_dv_without(v_action__497263, "response_topic"), Authored("m50.c2_refresh_content_actions.literal_0005"));
                }
            }
            if (R.Bool(F_dv_has_key(v_action__497263, "work_decision")))
            {
                object v_choice__499967 = F_dv_find(v_action__497263, Authored("m50.c2_refresh_content_actions.literal_0004"), F_dv_get(v_action__497263, "personal_choice"));
                v_action__497263 = DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1184", v_action__497263, v_choice__499967);
                object v_request__500247 = F_dvs_pending_request(v_actor, DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1185", v_action__497263));
                if (R.Bool(R.lt(R.number("0"), R.count(v_request__500247))))
                {
                    v_choice__499967 = F_dv_with(v_choice__499967, "basis", F_dv_append(F_dv_get(v_choice__499967, "basis"), new List<object> { F_dv_pair("source_event", F_dv_get(v_request__500247, "source_event")), F_dv_pair("parse_event", F_dv_get(v_request__500247, "parse_event")), F_dv_pair("requester", F_dv_get(v_request__500247, "requester")) }));
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1186", v_actor)))
                {
                    v_choice__499967 = DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1187", v_choice__499967, v_actor);
                }
                if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_actor, "id"), Authored("m50.c2_supply_actions.literal_0027"))) ? (object)(F_c2_quer_concerned(v_actor)) : (object)(false))) ? (object)(R.not(F_dv_find(v_action__497263, Authored("m50.c2_refresh_content_actions.literal_0006"), false))) : (object)(false))))
                {
                    v_choice__499967 = DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1188", v_choice__499967);
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1189", v_actor)))
                {
                    v_choice__499967 = DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1190", v_choice__499967, v_actor);
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1191", v_actor)))
                {
                    v_choice__499967 = DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1192", v_choice__499967, v_actor);
                }
                v_action__497263 = F_dv_with(v_action__497263, "personal_choice", F_dv_with(v_choice__499967, "basis", F_c2_cooperation_basis(v_actor, F_dv_get(v_choice__499967, "basis"))));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1193", v_actor, v_action__497263)))
            {
                object v_source__503372 = F_campaign_approved(Authored("m50.c2_refresh_content_actions.literal_0007"));
                object v_sacrifices__503500 = new List<object> {  };
                if (R.Bool(F_c2_quer_concerned(v_actor)))
                {
                    v_sacrifices__503500 = AuthoredCopy("m50.c2_norms_build.literal_0029");
                }
                v_action__497263 = DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1194", v_action__497263, v_sacrifices__503500, v_actor, v_source__503372);
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1195", v_actor, v_action__497263)))
            {
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1196", v_action__497263)))
                {
                    object v_base__504512 = F_dv_find(v_action__497263, Authored("m50.c2_refresh_content_actions.literal_0003"), F_dv_get(v_action__497263, "precondition"));
                    object v_choice__504661 = F_dv_find(v_action__497263, Authored("m50.c2_refresh_content_actions.literal_0004"), F_dv_get(v_action__497263, "personal_choice"));
                    object v_surface__504815 = F_dv_find(v_action__497263, Authored("m50.c2_refresh_content_actions.literal_0008"), F_dv_get(v_action__497263, "surface"));
                    v_action__497263 = F_dv_with_many(v_action__497263, new List<object> { F_dv_pair("owned_precondition_base", v_base__504512), F_dv_pair("owned_choice_base", v_choice__504661), F_dv_pair("owned_surface_base", v_surface__504815), F_dv_pair("surface", v_surface__504815), F_dv_pair("speech", F_dv_with(F_dv_get(v_action__497263, "speech"), "surface", v_surface__504815)), F_dv_pair("precondition", v_base__504512), F_dv_pair("personal_choice", v_choice__504661) });
                    v_action__497263 = F_dv_with(v_action__497263, "personal_choice", F_dv_with(v_choice__504661, "basis", F_c2_cooperation_basis(v_actor, F_dv_get(v_choice__504661, "basis"))));
                    if (R.Bool(F_c2_quer_restricted(v_actor)))
                    {
                        object v_text__505408 = Authored("m50.c2_refresh_content_actions.text");
                        v_action__497263 = F_dv_with_many(v_action__497263, new List<object> { F_dv_pair("surface", v_text__505408), F_dv_pair("speech", F_dv_with(F_dv_get(v_action__497263, "speech"), "surface", v_text__505408)) });
                    }
                    if (R.Bool(R.not(F_c2_quer_concerned(v_actor))))
                    {
                        v_action__497263 = F_dv_with(v_action__497263, "precondition", F_dv_not(F_dv_true()));
                        v_action__497263 = F_dv_with(v_action__497263, "personal_choice", F_dv_with(v_choice__504661, "requires", F_dv_not(F_dv_true())));
                    }
                }
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1197", v_action__497263)))
            {
                object v_base__505992 = F_dv_find(v_action__497263, Authored("m50.c2_refresh_content_actions.literal_0003"), F_dv_get(v_action__497263, "precondition"));
                object v_surface__506137 = F_dv_find(v_action__497263, Authored("m50.c2_refresh_content_actions.literal_0008"), F_dv_get(v_action__497263, "surface"));
                v_action__497263 = F_dv_with_many(v_action__497263, new List<object> { F_dv_pair("owned_precondition_base", v_base__505992), F_dv_pair("owned_surface_base", v_surface__506137), F_dv_pair("surface", v_surface__506137), F_dv_pair("speech", F_dv_with(F_dv_get(v_action__497263, "speech"), "surface", v_surface__506137)) });
                object v_qualification__506463 = F_dv_not(F_dv_true());
                if (R.Bool(F_c2_quer_concerned(v_actor)))
                {
                    v_qualification__506463 = F_dv_true();
                }
                v_action__497263 = F_dv_with(v_action__497263, "precondition", F_dv_all(new List<object> { v_base__505992, v_qualification__506463 }));
                if (R.Bool(F_c2_quer_restricted(v_actor)))
                {
                    object v_text__506731 = Authored("m50.c2_refresh_content_actions.text_0002");
                    v_action__497263 = F_dv_with_many(v_action__497263, new List<object> { F_dv_pair("surface", v_text__506731), F_dv_pair("speech", F_dv_with(F_dv_get(v_action__497263, "speech"), "surface", v_text__506731)) });
                }
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1198", v_actor, v_action__497263)))
            {
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1199", v_actor)))
                {
                    object v_text__507489 = Authored("m50.c2_refresh_content_actions.text_0003");
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1200", v_action__497263)))
                    {
                        v_text__507489 = Authored("m50.c2_refresh_content_actions.literal_0010");
                    }
                    v_action__497263 = F_dv_with_many(v_action__497263, new List<object> { F_dv_pair("surface", v_text__507489), F_dv_pair("speech", F_dv_with(F_dv_get(v_action__497263, "speech"), "surface", v_text__507489)) });
                }
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1201", v_action__497263)))
            {
                object v_base__508162 = F_dv_find(v_action__497263, Authored("m50.c2_refresh_content_actions.literal_0003"), F_dv_get(v_action__497263, "precondition"));
                v_action__497263 = DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1202", v_action__497263, v_base__508162);
                object v_qualification__508435 = F_dv_not(F_dv_true());
                if (R.Bool(v_notice__496945))
                {
                    v_qualification__508435 = F_dv_true();
                }
                v_action__497263 = F_dv_with(v_action__497263, "precondition", F_dv_all(new List<object> { v_base__508162, v_qualification__508435 }));
                object v_choice__508638 = F_dv_find(v_action__497263, Authored("m50.c2_refresh_content_actions.literal_0004"), F_dv_get(v_action__497263, "personal_choice"));
                v_action__497263 = F_dv_with_many(v_action__497263, new List<object> { F_dv_pair("owned_choice_base", v_choice__508638), F_dv_pair("personal_choice", F_dv_with(v_choice__508638, "requires", F_dv_all(new List<object> { F_dv_get(v_choice__508638, "requires"), v_qualification__508435 }))) });
            }
            if (R.Bool(F_dv_has_key(v_action__497263, "owned_safety_choice")))
            {
                object v_qualification__509007 = F_dv_get(v_action__497263, "owned_safety_choice");
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1203", v_adopted_stop__497043, v_recheck__496993, v_action__497263)))
                {
                    v_qualification__509007 = F_dv_not(F_dv_true());
                }
                v_action__497263 = F_dv_with(v_action__497263, "precondition", v_qualification__509007);
                v_action__497263 = F_dv_with(v_action__497263, "personal_choice", F_dv_with(F_dv_get(v_action__497263, "personal_choice"), "requires", v_qualification__509007));
            }
            v_action__497263 = F_c2g_storyflow_action(v_actor, v_action__497263);
            if (storyflowEnabled)
            {
                v_action__497263 = ApplyC2StoryflowAction(v_actor, v_action__497263, ceremonyIds ??= ReadC2StoryflowCeremonyIds());
            }
            if (R.Bool(F_dv_has_key(v_action__497263, "content_refresh")))
            {
                object refresh = F_dv_get(v_action__497263, "content_refresh");
                v_action__497263 = DvValueTemplate.Instantiate(R.Text(F_dv_get(refresh, "template")), v_actor, v_action__497263);
            }
            _ = R.push(v_actions__496908, v_action__497263);
            v_i__496930 = R.add(v_i__496930, R.number("1"));
        }
        object v_original_rules__509743 = F_dv_find(F_dv_get(v_actor, "policy"), Authored("m50.c2_extend_actor_from.literal_0004"), new List<object> {  });
        object v_rules__509873 = new List<object> {  };
        v_i__496930 = R.number("0");
        while (R.Bool(R.lt(v_i__496930, R.count(v_original_rules__509743))))
        {
            object v_rule__509949 = R.at(v_original_rules__509743, v_i__496930);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1204", v_rule__509949)))
            {
                object v_applies__510086 = F_dv_not(F_dv_true());
                if (R.Bool(v_recheck__496993))
                {
                    v_applies__510086 = F_dv_fact(F_campaign_own(F_dv_get(v_actor, "id"), Authored("m50.c2_refresh_content_actions.literal")), true);
                }
                v_rule__509949 = F_dv_with(v_rule__509949, "available_when", v_applies__510086);
            }
            _ = R.push(v_rules__509873, v_rule__509949);
            v_i__496930 = R.add(v_i__496930, R.number("1"));
        }
        return DvValueTemplate.Instantiate("template.inline.c2_refresh_content_actions.1205", v_actor, v_actions__496908, v_rules__509873);
    }
    public static object F_c2_safety_goals(object v_actor) {
        v_actor = F_c2_query_view(v_actor, v_actor);
        object v_goals__510645 = new List<object> {  };
        object v_source__510665 = DvValueTemplate.Instantiate("template.inline.c2_safety_goals.1206");
        object v_work__510773 = F_c2_work_specs();
        object v_work_index__510805 = R.number("0");
        while (R.Bool(R.lt(v_work_index__510805, R.count(v_work__510773))))
        {
            object v_item__510873 = R.at(v_work__510773, v_work_index__510805);
            if (R.Bool(R.eq(F_dv_get(v_item__510873, "actor"), F_dv_get(v_actor, "id"))))
            {
                object v_candidate__510950 = F_dvp_find_action(F_dv_get(v_actor, "actions"), R.cat(F_dv_get(v_item__510873, "id"), Authored("m50.c2sf_ceremony_ids.literal")));
                if (R.Bool((R.Bool(R.not(R.eq(v_candidate__510950, false))) ? (object)(F_dvp_owned_decision_ready(v_actor, v_candidate__510950, F_dv_get(v_actor, "mind"))) : (object)(false))))
                {
                    object v_work_when__511196 = F_dv_get(v_item__510873, "when");
                    if (R.Bool((R.Bool(F_c2g_enabled(v_actor)) ? (object)(F_c2g_work(F_dv_get(v_item__510873, "id"))) : (object)(false))))
                    {
                        v_work_when__511196 = F_dv_true();
                    }
                    _ = R.push(v_goals__510645, F_dv_with_many(F_campaign_goal(F_c2_work_accepted(F_dv_get(v_actor, "id"), F_dv_get(v_item__510873, "id")), v_work_when__511196, DvValueTemplate.Instantiate("template.inline.c2_safety_goals.1207")), DvValueTemplate.Instantiate("template.inline.c2_safety_goals.1208", v_item__510873)));
                }
            }
            v_work_index__510805 = R.add(v_work_index__510805, R.number("1"));
        }
        object v_outward__511714 = (R.Bool((R.Bool(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_fact(F_campaign_own(F_dv_get(v_actor, "id"), Authored("m50.c2_safety_goals.literal")), true)), "true")) ? (object)(true) : (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_fact(F_campaign_own(F_dv_get(v_actor, "id"), Authored("m50.c2_refresh_content_actions.literal")), true)), "true")))) ? (object)(true) : (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_fact(F_campaign_own(F_dv_get(v_actor, "id"), Authored("m50.c2_safety_goals.literal_0002")), true)), "true")));
        if (R.Bool((R.Bool(v_outward__511714) ? (object)(F_c2_owned_recheck_needed(v_actor)) : (object)(false))))
        {
            object v_previous__512225 = F_dv_read(F_dv_get(v_actor, "mind"), F_campaign_ref(Authored("m50.c2_norm_condition.literal"), Authored("m50.c2_norm_condition.literal_0002")));
            object v_sample__512428 = F_c2_sample_claim(v_previous__512225);
            object v_environment__512476 = F_dv_read(F_dv_get(v_actor, "mind"), F_campaign_ref(Authored("m50.c2_world_objects.literal_0021"), Authored("m50.c2_norm_actions.literal_0037")));
            object v_minimum__512684 = R.number("0");
            if (R.Bool(R.not(R.eq(v_sample__512428, false))))
            {
                v_minimum__512684 = R.add(F_dv_get(v_sample__512428, "time"), Authored("m50.c2_meter_fresh.literal_0002"));
            }
            if (R.Bool((R.Bool(F_dv_uncontested(v_environment__512476)) ? (object)((R.Bool(R.eq(v_sample__512428, false)) ? (object)(true) : (object)(R.lt(F_dv_get(v_sample__512428, "time"), F_dv_get(v_environment__512476, "time"))))) : (object)(false))))
            {
                v_minimum__512684 = F_dv_get(v_environment__512476, "time");
            }
            object v_sampled_epoch__512983 = F_dv_read(F_dv_get(v_actor, "mind"), F_campaign_ref(Authored("m50.c2_norm_condition.literal"), Authored("m50.c2_norm_actions.literal_0037")));
            object v_epoch_sample__513189 = F_c2_sample_claim(v_sampled_epoch__512983);
            if (R.Bool((R.Bool((R.Bool((R.Bool(F_dv_uncontested(v_environment__512476)) ? (object)(F_dv_uncontested(v_sampled_epoch__512983)) : (object)(false))) ? (object)(R.lt(F_dv_get(v_environment__512476, "value"), F_dv_get(v_sampled_epoch__512983, "value"))) : (object)(false))) ? (object)(R.not(R.eq(v_epoch_sample__513189, false))) : (object)(false))))
            {
                _ = R.push(v_goals__510645, F_dv_with_many(F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_world_objects.literal_0021"), Authored("m50.c2_norm_actions.literal_0037")), F_dv_at_least(F_campaign_clock(), Authored("m50.c2_norm_actions.literal_0031")), v_source__510665, F_dv_get(v_epoch_sample__513189, "time")), DvValueTemplate.Instantiate("template.inline.c2_safety_goals.1209", v_actor)));
            }
            else
            {
                _ = R.push(v_goals__510645, F_dv_with_many(F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_norm_condition.literal"), Authored("m50.c2_norm_condition.literal_0002")), F_dv_at_least(F_campaign_clock(), Authored("m50.c2_norm_actions.literal_0031")), v_source__510665, v_minimum__512684), DvValueTemplate.Instantiate("template.inline.c2_safety_goals.1210", v_actor)));
            }
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_safety_goals.1211", v_actor)))
        {
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_safety_goals.1212", v_actor)))
            {
                object v_resolved__515504 = (R.Bool(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_fact(F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0009"), Authored("m50.c2_inspections.literal_0038")), Authored("m50.c2_inspections.literal_0039"))), "true")) ? (object)(true) : (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_fact(F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0009"), Authored("m50.c2_inspections.literal_0038")), Authored("m50.c2_install_actor_laws_into.literal_0004"))), "true")));
                if (R.Bool(R.not(v_resolved__515504)))
                {
                    _ = R.push(v_goals__510645, F_dv_with_many(F_campaign_goal(F_campaign_at(F_dv_get(v_actor, "id"), Authored("m50.c2_norms_build.literal_0048")), F_campaign_window(Authored("m50.c2_any_night.literal_0003"), Authored("m50.c2_norm_available.literal"), Authored("m50.c2_night.literal_0002")), v_source__510665), AuthoredCopy("m50.c2i_goals.literal_0027")));
                }
            }
            if (R.Bool(F_c2_owned_safety_notice(v_actor)))
            {
                object v_candidate__516653 = F_dvp_find_action(F_dv_get(v_actor, "actions"), R.cat(F_dv_get(v_actor, "id"), Authored("m50.c2_safety_goals.literal_0003")));
                if (R.Bool((R.Bool(R.not(R.eq(v_candidate__516653, false))) ? (object)(F_dvp_owned_decision_ready(v_actor, v_candidate__516653, F_dv_get(v_actor, "mind"))) : (object)(false))))
                {
                    _ = R.push(v_goals__510645, F_dv_with_many(F_campaign_goal(F_dv_fact(F_campaign_own(F_dv_get(v_actor, "id"), Authored("m50.c2_safety_actions.literal_0012")), true), F_c2_night(Authored("m50.c2_any_night.literal_0003")), v_source__510665), DvValueTemplate.Instantiate("template.inline.c2_safety_goals.1213", v_actor)));
                }
            }
            _ = R.push(v_goals__510645, F_dv_with_many(F_campaign_goal(F_dv_fact(F_campaign_own(F_dv_get(v_actor, "id"), Authored("m50.c2_world_objects.literal_0042")), true), DvValueTemplate.Instantiate("template.inline.c2_safety_goals.1214", v_actor), v_source__510665), DvValueTemplate.Instantiate("template.inline.c2_safety_goals.1215", v_actor)));
            object v_holding_shift__518336 = F_campaign_any(F_dv_fact(F_campaign_own(F_dv_get(v_actor, "id"), Authored("m50.c2_safety_actions.literal_0012")), true), F_dv_fact(F_campaign_own(F_dv_get(v_actor, "id"), Authored("m50.c2_world_objects.literal_0042")), true));
            _ = R.push(v_goals__510645, F_dv_with_many(F_campaign_goal(F_campaign_at(F_dv_get(v_actor, "id"), Authored("m50.c2_norms_build.literal_0048")), DvValueTemplate.Instantiate("template.inline.c2_safety_goals.1216", v_holding_shift__518336), v_source__510665), AuthoredCopy("m50.c2i_goals.literal_0027")));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_safety_goals.1217", v_actor)))
        {
            _ = R.push(v_goals__510645, F_campaign_goal(F_campaign_at(F_dv_get(v_actor, "id"), Authored("m50.c2_norms_build.literal_0034")), DvValueTemplate.Instantiate("template.inline.c2_safety_goals.1218", v_actor), v_source__510665));
            object v_job__519741 = Authored("m50.c2i_rows_build.literal_0038");
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_safety_goals.1219", v_actor)))
            {
                v_job__519741 = Authored("m50.c2i_rows_build.literal_0042");
            }
            _ = R.push(v_goals__510645, F_dv_with_many(F_campaign_goal(F_dv_fact(F_campaign_own(F_dv_get(v_actor, "id"), Authored("m50.c2_world_objects.literal_0040")), true), DvValueTemplate.Instantiate("template.inline.c2_safety_goals.1220", v_actor, v_job__519741), v_source__510665), DvValueTemplate.Instantiate("template.inline.c2_safety_goals.1221", v_job__519741)));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2_safety_goals.1222", v_actor)))
        {
            object v_minimum__520862 = Authored("m50.c2_personal_goal_templates_build.literal_0004");
            object v_guards__520974 = AuthoredCopy("m50.c2_world_objects.literal_0041");
            object v_i__521084 = R.number("0");
            while (R.Bool(R.lt(v_i__521084, R.count(v_guards__520974))))
            {
                object v_position__521144 = F_dv_read(F_dv_get(v_actor, "mind"), F_campaign_position_ref(R.at(v_guards__520974, v_i__521084)));
                object v_active__521226 = F_dv_read(F_dv_get(v_actor, "mind"), F_campaign_own(R.at(v_guards__520974, v_i__521084), Authored("m50.c2_world_objects.literal_0042")));
                if (R.Bool(F_dv_uncontested(v_position__521144)))
                {
                    v_minimum__520862 = F_dv_max(v_minimum__520862, F_dv_get(v_position__521144, "time"));
                }
                if (R.Bool(F_dv_uncontested(v_active__521226)))
                {
                    v_minimum__520862 = F_dv_max(v_minimum__520862, F_dv_get(v_active__521226, "time"));
                }
                v_i__521084 = R.add(v_i__521084, R.number("1"));
            }
            _ = R.push(v_goals__510645, F_c2i_watch_goal(v_actor, F_c2_night(Authored("m50.c2_any_night.literal_0003")), v_source__510665, v_minimum__520862));
        }
        return v_goals__510645;
    }
    public static object F_c2_color_encouragement_text() {
        return DvValueTemplate.Instantiate("template.c2_color_encouragement_text");
    }
    public static object F_c2m_source() {
        return DvValueTemplate.Instantiate("template.c2m_source");
    }
    public static object F_c2m_row(object v_actor,object v_suffix,object v_room,object v_object,object v_field,object v_requirements,object v_effects,object v_label,object v_text,object v_promotes,object v_sacrifices,object v_private) {
        return DvValueTemplate.Instantiate("template.c2m_row", v_actor, v_suffix, v_room, v_object, v_field, v_requirements, v_effects, v_label, v_text, v_promotes, v_sacrifices, v_private);
    }
    public static object F_c2m_rows() {
        object v_key__522485 = AuthoredCopy("m50.c2m_rows.key");
        object v_cached__522571 = R.memo_get(new List<object> {  }, v_key__522485);
        if (R.Bool(R.at(v_cached__522571, R.number("0"))))
        {
            return R.at(v_cached__522571, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__522485, F_c2m_rows_build());
    }
    public static object F_c2m_rows_build() {
        return DvValueTemplate.Instantiate("template.c2m_rows_build");
    }
    public static object F_c2m_fields() {
        return DvValueTemplate.Instantiate("template.c2m_fields");
    }
    public static object F_c2m_initialize(object v_world) {
        object v_result__543908 = v_world;
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2m_initialize.1223", v_result__543908)))
        {
            v_result__543908 = F_dv_store(v_result__543908, F_campaign_ref(Authored("m50.c2_world_objects.literal_0009"), Authored("m50.c2m_rows_build.literal_0036")), F_dv_field(false));
        }
        object v_internal__544339 = AuthoredCopy("m50.c2m_initialize.internal");
        object v_internal_index__544441 = R.number("0");
        while (R.Bool(R.lt(v_internal_index__544441, R.count(v_internal__544339))))
        {
            object v_ref__544521 = F_campaign_ref(Authored("m50.c2_inspection_detail_lines.literal_0008"), R.at(v_internal__544339, v_internal_index__544441));
            if (R.Bool(R.not(F_dv_get(F_dv_read(v_result__543908, v_ref__544521), "known"))))
            {
                v_result__543908 = F_dv_store(v_result__543908, v_ref__544521, F_dv_field(false));
            }
            v_internal_index__544441 = R.add(v_internal_index__544441, R.number("1"));
        }
        object v_groups__544809 = F_c2m_fields();
        object v_i__544840 = R.number("0");
        while (R.Bool(R.lt(v_i__544840, R.count(v_groups__544809))))
        {
            object v_j__544892 = R.number("0");
            while (R.Bool(R.lt(v_j__544892, R.count(F_dv_get(R.at(v_groups__544809, v_i__544840), "fields")))))
            {
                object v_field__544962 = R.at(F_dv_get(R.at(v_groups__544809, v_i__544840), "fields"), v_j__544892);
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2m_initialize.1224", v_result__543908, v_groups__544809, v_i__544840, v_field__544962)))
                {
                    object v_initial__545284 = false;
                    v_result__543908 = F_dv_store(v_result__543908, F_campaign_ref(F_dv_get(R.at(v_groups__544809, v_i__544840), "object"), v_field__544962), F_dv_field(v_initial__545284));
                }
                v_j__544892 = R.add(v_j__544892, R.number("1"));
            }
            v_i__544840 = R.add(v_i__544840, R.number("1"));
        }
        return v_result__543908;
    }
    public static object F_c2m_ready(object v_row) {
        return DvValueTemplate.Instantiate("template.c2m_ready", v_row);
    }
    public static object F_c2m_authority(object v_row) {
        return DvValueTemplate.Instantiate("template.c2m_authority", v_row);
    }
    public static object F_c2m_law(object v_row,object v_mode) {
        return DvValueTemplate.Instantiate("template.c2m_law", v_row, v_mode);
    }

    public static object F_c2m_actions(object v_actor) {
        object v_result__547883 = new List<object> {  };
        object v_rows__547904 = F_c2m_rows();
        object v_i__547931 = R.number("0");
        while (R.Bool(R.lt(v_i__547931, R.count(v_rows__547904))))
        {
            object v_row__547981 = R.at(v_rows__547904, v_i__547931);
            if (R.Bool(R.eq(F_dv_get(v_row__547981, "actor"), v_actor)))
            {
                object v_grants__548044 = new List<object> { F_campaign_ref(F_dv_get(v_row__547981, "object"), F_dv_get(v_row__547981, "field")) };
                object v_j__548108 = R.number("0");
                while (R.Bool(R.lt(v_j__548108, R.count(F_dv_get(v_row__547981, "effects")))))
                {
                    _ = R.push(v_grants__548044, F_dv_get(R.at(F_dv_get(v_row__547981, "effects"), v_j__548108), "ref"));
                    v_j__548108 = R.add(v_j__548108, R.number("1"));
                }
                object v_ready__548257 = F_c2m_ready(v_row__547981);
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2m_actions.1226", v_row__547981)))
                {
                    v_ready__548257 = DvValueTemplate.Instantiate("template.inline.c2m_actions.1227", v_ready__548257);
                }
                object v_action__548737 = DvValueTemplate.Instantiate("template.inline.c2m_actions.1228", v_row__547981, v_ready__548257, v_grants__548044);
                v_action__548737 = F_dv_with(v_action__548737, "personal_choice", new List<object> { F_dv_pair("group", F_dv_get(v_row__547981, "id")), F_dv_pair("requires", v_ready__548257), F_dv_pair("prohibits", F_dv_fact(F_campaign_ref(F_dv_get(v_row__547981, "object"), F_dv_get(v_row__547981, "field")), true)), F_dv_pair("promotes", F_dv_get(v_row__547981, "promotes")), F_dv_pair("sacrifices", F_dv_get(v_row__547981, "sacrifices")), F_dv_pair("require_positive", true), F_dv_pair("basis", new List<object> { F_c2m_source() }) });
                if (R.Bool(F_dv_get(v_row__547981, "private")))
                {
                    v_action__548737 = DvValueTemplate.Instantiate("template.inline.c2m_actions.1229", v_action__548737, v_ready__548257, v_row__547981, v_actor);
                }
                else
                {
                    v_action__548737 = F_dv_with_many(v_action__548737, new List<object> { F_dv_pair("public_observers", F_campaign_people()), F_dv_pair("public_grants", v_grants__548044) });
                }
                _ = R.push(v_result__547883, v_action__548737);
            }
            v_i__547931 = R.add(v_i__547931, R.number("1"));
        }
        object v_visible_custody__549937 = DvValueTemplate.Instantiate("template.inline.c2m_actions.1230", v_actor);
        object v_held_custody__550222 = DvValueTemplate.Instantiate("template.inline.c2m_actions.1231", v_visible_custody__549937);
        _ = R.push(v_result__547883, DvValueTemplate.Instantiate("template.inline.c2m_actions.1232", v_actor, v_visible_custody__549937, v_held_custody__550222));
        object v_groups__551091 = F_c2m_fields();
        v_i__547931 = R.number("0");
        while (R.Bool(R.lt(v_i__547931, R.count(v_groups__551091))))
        {
            object v_group__551170 = R.at(v_groups__551091, v_i__547931);
            object v_inspect__551201 = F_c2_inspect_action(v_actor, F_dv_get(v_group__551170, "object"), F_dv_get(v_group__551170, "room"), F_dv_get(v_group__551170, "fields"), Authored("m50.c2m_actions.literal_0011"), Authored("m50.c2m_actions.literal_0012"), F_c2m_source());
            v_inspect__551201 = DvValueTemplate.Instantiate("template.inline.c2m_actions.1233", v_inspect__551201, v_actor, v_group__551170);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2m_actions.1234", v_group__551170)))
            {
                v_inspect__551201 = DvValueTemplate.Instantiate("template.inline.c2m_actions.1235", v_inspect__551201);
                object v_comparison__552038 = F_c2_visible_line(F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0009"), Authored("m50.c2m_rows_build.literal_0035")), true), Authored("m50.c2m_actions.literal_0014"), DvValueTemplate.Instantiate("template.inline.c2m_actions.1236"), F_c2m_source());
                v_inspect__551201 = F_dv_with(v_inspect__551201, "presentation", F_dv_with(F_dv_get(v_inspect__551201, "presentation"), "lines", F_dv_append(F_dv_get(F_dv_get(v_inspect__551201, "presentation"), "lines"), v_comparison__552038)));
            }
            _ = R.push(v_result__547883, v_inspect__551201);
            v_i__547931 = R.add(v_i__547931, R.number("1"));
        }
        return v_result__547883;
    }
    public static object F_c2m_row_goal(object v_row,object v_id,object v_when,object v_completion,object v_travel) {
        object v_key__554333 = DvValueTemplate.Instantiate("template.inline.c2m_row_goal.1237", v_id, v_when, v_completion, v_travel);
        object v_cached__554444 = R.memo_get(v_row, v_key__554333);
        if (R.Bool(R.at(v_cached__554444, R.number("0"))))
        {
            return R.at(v_cached__554444, R.number("1"));
        }
        object v_goal__554529 = false;
        if (R.Bool(v_travel))
        {
            v_goal__554529 = F_dv_with_many(F_campaign_goal(F_campaign_at(v_id, F_dv_get(v_row, "room")), v_when, new List<object> { F_c2m_source() }), new List<object> { F_dv_pair("navigation_completion", F_campaign_at(v_id, F_dv_get(v_row, "room"))), F_dv_pair("navigation_room", F_dv_get(v_row, "room")), F_dv_pair("navigation_requirements", new List<object> {  }), F_dv_pair("navigation_methods", new List<object> { F_dv_get(v_row, "id") }) });
        }
        else
        {
            v_goal__554529 = F_dv_with_many(F_campaign_goal(v_completion, v_when, new List<object> { F_c2m_source() }), new List<object> { F_dv_pair("navigation_completion", v_completion), F_dv_pair("navigation_room", F_dv_get(v_row, "room")), F_dv_pair("navigation_requirements", F_dv_get(v_row, "requirements")), F_dv_pair("navigation_methods", new List<object> { F_dv_get(v_row, "id") }) });
        }
        return R.memo_put(v_row, v_key__554333, v_goal__554529);
    }
    public static object F_c2m_observation_goal_templates(object v_id,object v_groups) {
        object v_key__555130 = DvValueTemplate.Instantiate("template.inline.c2m_observation_goal_templates.1238", v_id);
        object v_cached__555233 = R.memo_get(v_groups, v_key__555130);
        if (R.Bool(R.at(v_cached__555233, R.number("0"))))
        {
            return R.at(v_cached__555233, R.number("1"));
        }
        object v_result__555321 = new List<object> {  };
        object v_i__555342 = R.number("0");
        while (R.Bool(R.lt(v_i__555342, R.count(v_groups))))
        {
            object v_group__555394 = R.at(v_groups, v_i__555342);
            object v_j__555425 = R.number("0");
            while (R.Bool(R.lt(v_j__555425, R.count(F_dv_get(v_group__555394, "fields")))))
            {
                _ = R.push(v_result__555321, F_campaign_observation_goal(F_campaign_ref(F_dv_get(v_group__555394, "object"), R.at(F_dv_get(v_group__555394, "fields"), v_j__555425)), F_campaign_at(v_id, F_dv_get(v_group__555394, "room")), new List<object> { F_c2m_source() }, R.number("0")));
                v_j__555425 = R.add(v_j__555425, R.number("1"));
            }
            v_i__555342 = R.add(v_i__555342, R.number("1"));
        }
        return R.memo_put(v_groups, v_key__555130, v_result__555321);
    }
    public static object F_c2m_travel_window(object v_condition) {
        object v_refs__555913 = F_dvs_sim_refs(v_condition);
        object v_clock_only__555953 = R.lt(R.number("0"), R.count(v_refs__555913));
        object v_i__555994 = R.number("0");
        while (R.Bool(R.lt(v_i__555994, R.count(v_refs__555913))))
        {
            if (R.Bool(R.not(F_dv_same(R.at(v_refs__555913, v_i__555994), F_campaign_clock()))))
            {
                v_clock_only__555953 = false;
            }
            v_i__555994 = R.add(v_i__555994, R.number("1"));
        }
        if (R.Bool(v_clock_only__555953))
        {
            return v_condition;
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2m_travel_window.1239", v_condition)))
        {
            object v_items__556310 = new List<object> {  };
            v_i__555994 = R.number("0");
            while (R.Bool(R.lt(v_i__555994, R.count(F_dv_get(v_condition, "items")))))
            {
                _ = R.push(v_items__556310, F_c2m_travel_window(R.at(F_dv_get(v_condition, "items"), v_i__555994)));
                v_i__555994 = R.add(v_i__555994, R.number("1"));
            }
            return F_dv_all(v_items__556310);
        }
        return F_dv_true();
    }
    public static object F_c2m_goals(object v_actor) {
        object v_result__556557 = new List<object> {  };
        object v_rows__556578 = F_c2m_rows();
        object v_i__556605 = R.number("0");
        while (R.Bool(R.lt(v_i__556605, R.count(v_rows__556578))))
        {
            object v_row__556655 = R.at(v_rows__556578, v_i__556605);
            if (R.Bool(R.eq(F_dv_get(v_row__556655, "actor"), F_dv_get(v_actor, "id"))))
            {
                object v_available__556721 = F_dv_all(F_dv_get(v_row__556655, "requirements"));
                object v_completion__556775 = F_dv_fact(F_campaign_ref(F_dv_get(v_row__556655, "object"), F_dv_get(v_row__556655, "field")), true);
                object v_when__556856 = v_available__556721;
                if (R.Bool(F_dv_get(v_row__556655, "private")))
                {
                    object v_motive__556923 = F_dv_fact(F_campaign_own(F_dv_get(v_actor, "id"), Authored("m50.c2_initial_body.literal_0005")), true);
                    v_when__556856 = F_dv_all(new List<object> { v_available__556721, v_motive__556923 });
                    if (R.Bool(R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), v_completion__556775), "true"))))
                    {
                        object v_travel_when__557300 = F_dv_all(new List<object> { v_motive__556923, F_c2m_travel_window(v_available__556721) });
                        _ = R.push(v_result__556557, F_c2m_row_goal(v_row__556655, F_dv_get(v_actor, "id"), v_travel_when__557300, v_completion__556775, true));
                    }
                }
                _ = R.push(v_result__556557, F_c2m_row_goal(v_row__556655, F_dv_get(v_actor, "id"), v_when__556856, v_completion__556775, false));
            }
            v_i__556605 = R.add(v_i__556605, R.number("1"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2m_goals.1240", v_actor)))
        {
            _ = R.push(v_result__556557, F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2i_world_objects.literal"), Authored("m50.c2m_rows_build.literal_0066")), DvValueTemplate.Instantiate("template.inline.c2m_goals.1241", v_actor), new List<object> { F_c2m_source() }, R.number("0")));
        }
        object v_observations__558153 = F_c2m_observation_goal_templates(F_dv_get(v_actor, "id"), F_c2m_fields());
        v_i__556605 = R.number("0");
        while (R.Bool(R.lt(v_i__556605, R.count(v_observations__558153))))
        {
            _ = R.push(v_result__556557, R.at(v_observations__558153, v_i__556605));
            v_i__556605 = R.add(v_i__556605, R.number("1"));
        }
        return v_result__556557;
    }
    public static object F_c2m_labels() {
        return DvValueTemplate.Instantiate("template.c2m_labels");
    }
    public static object F_c2m_visible_lines(object v_object,object v_fields) {
        return DvValueTemplate.Instantiate("template.c2m_visible_lines", v_object, v_fields);
    }
    public static object F_c2m_concat(object v_left,object v_right) {
        return DvValueTemplate.Instantiate("template.c2m_concat", v_left, v_right);
    }
    public static object F_c2m_other_people(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2m_other_people", v_actor);
    }
    public static object F_c2m_ration_contact_effects() {
        return DvValueTemplate.Instantiate("template.c2m_ration_contact_effects");
    }
    public static object F_c2m_ink_contact_law() {
        return DvValueTemplate.Instantiate("template.c2m_ink_contact_law");
    }
    public static object F_c2daily_ref(object v_field) {
        return DvValueTemplate.Instantiate("template.c2daily_ref", v_field);
    }
    public static object F_c2daily_say(object v_actor,object v_id,object v_room,object v_when,object v_label,object v_text,object v_people) {
        return DvValueTemplate.Instantiate("template.c2daily_say", v_actor, v_id, v_room, v_when, v_label, v_text, v_people);
    }
    public static object F_c2daily_after(object v_row,object v_speaker,object v_action) {
        return DvValueTemplate.Instantiate("template.c2daily_after", v_row, v_speaker, v_action);
    }
    public static object F_c2daily_notes_window() {
        return DvValueTemplate.Instantiate("template.c2daily_notes_window");
    }
    public static object F_c2daily_rows() {
        object v_key__563020 = AuthoredCopy("m50.c2daily_rows.key");
        object v_cached__563110 = R.memo_get(new List<object> {  }, v_key__563020);
        if (R.Bool(R.at(v_cached__563110, R.number("0"))))
        {
            return R.at(v_cached__563110, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__563020, F_c2daily_rows_build());
    }
    public static object F_c2daily_rows_build() {
        object v_rows__563277 = new List<object> {  };
        object v_morning__563296 = F_campaign_window(Authored("m50.c2_any_night.literal"), Authored("m50.c2_cooperation_rows.literal_0002"), Authored("m50.c2i_rows_build.literal_0002"));
        _ = R.push(v_rows__563277, F_c2daily_say(Authored("m50.c2_norms_build.literal_0065"), Authored("m50.c2daily_rows_build.literal"), Authored("m50.c2daily_rows_build.literal_0002"), v_morning__563296, Authored("m50.c2daily_rows_build.literal_0003"), Authored("m50.c2daily_rows_build.literal_0004"), AuthoredCopy("m50.c2_cooperation_rows.literal_0063")));
        _ = R.push(v_rows__563277, F_c2daily_after(F_c2daily_say(Authored("m50.c2_initial_body.literal_0009"), Authored("m50.c2daily_rows_build.literal_0005"), Authored("m50.c2daily_rows_build.literal_0002"), v_morning__563296, Authored("m50.c2daily_rows_build.literal_0006"), Authored("m50.c2daily_rows_build.literal_0007"), AuthoredCopy("m50.c2daily_rows_build.literal_0008")), Authored("m50.c2_norms_build.literal_0065"), Authored("m50.c2daily_rows_build.literal")));
        _ = R.push(v_rows__563277, F_c2daily_say(Authored("m50.c2_supply_actions.literal_0027"), Authored("m50.c2daily_rows_build.literal_0009"), Authored("m50.c2daily_rows_build.literal_0002"), v_morning__563296, Authored("m50.c2daily_rows_build.literal_0010"), Authored("m50.c2daily_rows_build.literal_0011"), AuthoredCopy("m50.c2_document_coordination_rows.literal_0005")));
        object v_ding__565238 = F_campaign_window(Authored("m50.c2_any_night.literal"), Authored("m50.c2i_rows_build.literal_0002"), Authored("m50.c2m_rows_build.literal_0060"));
        _ = R.push(v_rows__563277, F_c2daily_say(Authored("m50.c2_norms_build.literal_0033"), Authored("m50.c2daily_rows_build.literal_0012"), Authored("m50.c2_norms_build.literal_0048"), v_ding__565238, Authored("m50.c2daily_rows_build.literal_0013"), Authored("m50.c2daily_rows_build.literal_0014"), AuthoredCopy("m50.c2daily_rows_build.literal_0015")));
        _ = R.push(v_rows__563277, F_c2daily_say(Authored("m50.c2_initial_body.literal"), Authored("m50.c2daily_rows_build.literal_0016"), Authored("m50.c2_norms_build.literal_0048"), v_ding__565238, Authored("m50.c2daily_rows_build.literal_0017"), Authored("m50.c2daily_rows_build.literal_0018"), AuthoredCopy("m50.c2daily_rows_build.literal_0019")));
        _ = R.push(v_rows__563277, F_c2daily_after(F_c2daily_say(Authored("m50.c2_norms_build.literal_0033"), Authored("m50.c2daily_rows_build.literal_0020"), Authored("m50.c2_norms_build.literal_0048"), v_ding__565238, Authored("m50.c2daily_rows_build.literal_0021"), Authored("m50.c2daily_rows_build.literal_0022"), AuthoredCopy("m50.c2daily_rows_build.literal_0023")), Authored("m50.c2_initial_body.literal"), Authored("m50.c2daily_rows_build.literal_0016")));
        _ = R.push(v_rows__563277, F_c2daily_say(Authored("m50.c2_initial_body.literal_0020"), Authored("m50.c2daily_rows_build.literal_0024"), Authored("m50.c2_norms_build.literal_0048"), v_ding__565238, Authored("m50.c2daily_rows_build.literal_0025"), Authored("m50.c2daily_rows_build.literal_0026"), AuthoredCopy("m50.c2daily_rows_build.literal_0027")));
        _ = R.push(v_rows__563277, F_c2daily_after(F_c2daily_say(Authored("m50.c2_norms_build.literal_0082"), Authored("m50.c2daily_rows_build.literal_0028"), Authored("m50.c2_norms_build.literal_0048"), v_ding__565238, Authored("m50.c2daily_rows_build.literal_0029"), Authored("m50.c2daily_rows_build.literal_0030"), AuthoredCopy("m50.c2daily_rows_build.literal_0031")), Authored("m50.c2_initial_body.literal_0020"), Authored("m50.c2daily_rows_build.literal_0024")));
        object v_lesson__568304 = F_campaign_window(Authored("m50.c2_any_night.literal"), Authored("m50.c2m_rows_build.literal_0060"), Authored("m50.c2daily_rows_build.literal_0032"));
        _ = R.push(v_rows__563277, F_c2daily_say(Authored("m50.c2_norms_build.literal_0056"), Authored("m50.c2daily_rows_build.literal_0033"), Authored("m50.c2_norms_build.literal_0057"), v_lesson__568304, Authored("m50.c2daily_rows_build.literal_0034"), Authored("m50.c2daily_rows_build.literal_0035"), AuthoredCopy("m50.c2daily_rows_build.literal_0036")));
        _ = R.push(v_rows__563277, F_c2daily_say(Authored("m50.c2_special_actions.literal_0019"), Authored("m50.c2daily_rows_build.literal_0037"), Authored("m50.c2_norms_build.literal_0057"), v_lesson__568304, Authored("m50.c2daily_rows_build.literal_0038"), Authored("m50.c2daily_rows_build.literal_0039"), AuthoredCopy("m50.c2daily_rows_build.literal_0036")));
        _ = R.push(v_rows__563277, F_c2daily_say(Authored("m50.c2_norms_build.literal_0013"), Authored("m50.c2daily_rows_build.literal_0040"), Authored("m50.c2_norms_build.literal_0057"), v_lesson__568304, Authored("m50.c2daily_rows_build.literal_0041"), Authored("m50.c2daily_rows_build.literal_0042"), AuthoredCopy("m50.c2daily_rows_build.literal_0036")));
        _ = R.push(v_rows__563277, F_c2daily_after(F_c2daily_say(Authored("m50.c2_norms_build.literal_0003"), Authored("m50.c2daily_rows_build.literal_0043"), Authored("m50.c2_norms_build.literal_0057"), v_lesson__568304, Authored("m50.c2daily_rows_build.literal_0044"), Authored("m50.c2daily_rows_build.literal_0045"), AuthoredCopy("m50.c2daily_rows_build.literal_0036")), Authored("m50.c2_alive.literal"), Authored("m50.c2daily_rows_build.literal_0046")));
        object v_rule__570719 = F_dv_fact(F_c2d_ref(Authored("m50.c2daily_rows_build.literal_0047")), Authored("m50.c2daily_rows_build.literal_0048"));
        _ = R.push(v_rows__563277, F_c2d_row(Authored("m50.c2_alive.literal"), Authored("m50.c2daily_rows_build.literal_0046"), Authored("m50.c2_norms_build.literal_0057"), v_lesson__568304, v_rule__570719, Authored("m50.c2daily_rows_build.literal_0049"), Authored("m50.c2daily_rows_build.literal_0050"), F_campaign_approved(Authored("m50.c2daily_rows_build.literal_0051"))));
        object v_noon__571402 = DvValueTemplate.Instantiate("template.inline.c2daily_rows_build.1242");
        object v_table1__571771 = AuthoredCopy("m50.c2daily_rows_build.table1");
        object v_table2__571873 = AuthoredCopy("m50.c2daily_rows_build.table2");
        object v_table3__571975 = AuthoredCopy("m50.c2daily_rows_build.table3");
        object v_table4__572077 = AuthoredCopy("m50.c2daily_rows_build.table4");
        _ = R.push(v_rows__563277, F_c2daily_say(Authored("m50.c2_norms_build.literal_0082"), Authored("m50.c2daily_rows_build.literal_0052"), Authored("m50.c2_norms_build.literal_0048"), v_noon__571402, Authored("m50.c2daily_rows_build.literal_0053"), Authored("m50.c2daily_rows_build.literal_0054"), v_table1__571771));
        _ = R.push(v_rows__563277, F_c2daily_say(Authored("m50.c2_norms_build.literal_0047"), Authored("m50.c2daily_rows_build.literal_0055"), Authored("m50.c2_norms_build.literal_0048"), v_noon__571402, Authored("m50.c2daily_rows_build.literal_0056"), Authored("m50.c2daily_rows_build.literal_0057"), v_table1__571771));
        _ = R.push(v_rows__563277, F_c2daily_say(Authored("m50.c2_norms_build.literal_0090"), Authored("m50.c2daily_rows_build.literal_0058"), Authored("m50.c2_norms_build.literal_0048"), v_noon__571402, Authored("m50.c2daily_rows_build.literal_0059"), Authored("m50.c2daily_rows_build.literal_0060"), v_table2__571873));
        _ = R.push(v_rows__563277, F_c2daily_say(Authored("m50.c2_norms_build.literal_0065"), Authored("m50.c2daily_rows_build.literal_0061"), Authored("m50.c2_norms_build.literal_0048"), v_noon__571402, Authored("m50.c2daily_rows_build.literal_0062"), Authored("m50.c2daily_rows_build.literal_0063"), v_table3__571975));
        _ = R.push(v_rows__563277, F_c2daily_say(Authored("m50.c2_norms_build.literal_0003"), Authored("m50.c2daily_rows_build.literal_0064"), Authored("m50.c2_norms_build.literal_0048"), v_noon__571402, Authored("m50.c2daily_rows_build.literal_0065"), Authored("m50.c2daily_rows_build.literal_0066"), v_table4__572077));
        object v_notes_window__574269 = F_c2daily_notes_window();
        _ = R.push(v_rows__563277, F_c2daily_say(Authored("m50.c2_norms_build.literal_0003"), Authored("m50.c2daily_rows_build.literal_0067"), Authored("m50.c2_norms_build.literal_0004"), DvValueTemplate.Instantiate("template.inline.c2daily_rows_build.1243", v_notes_window__574269), Authored("m50.c2daily_rows_build.literal_0068"), Authored("m50.c2daily_rows_build.literal_0069"), AuthoredCopy("m50.c2_document_coordination_rows.literal_0005")));
        _ = R.push(v_rows__563277, F_c2daily_after(F_c2daily_say(Authored("m50.c2_norms_build.literal_0013"), Authored("m50.c2daily_rows_build.literal_0070"), Authored("m50.c2_norms_build.literal_0004"), v_notes_window__574269, Authored("m50.c2daily_rows_build.literal_0071"), Authored("m50.c2daily_rows_build.literal_0072"), AuthoredCopy("m50.c2daily_rows_build.literal_0073")), Authored("m50.c2_norms_build.literal_0003"), Authored("m50.c2daily_rows_build.literal_0067")));
        _ = R.push(v_rows__563277, F_c2daily_after(F_c2daily_say(Authored("m50.c2_norms_build.literal_0013"), Authored("m50.c2daily_rows_build.literal_0074"), Authored("m50.c2_norms_build.literal_0004"), v_notes_window__574269, Authored("m50.c2daily_rows_build.literal_0075"), Authored("m50.c2daily_rows_build.literal_0076"), AuthoredCopy("m50.c2daily_rows_build.literal_0077")), Authored("m50.c2_norms_build.literal_0013"), Authored("m50.c2daily_rows_build.literal_0070")));
        _ = R.push(v_rows__563277, F_c2daily_after(F_c2daily_say(Authored("m50.c2_norms_build.literal_0090"), Authored("m50.c2daily_rows_build.literal_0078"), Authored("m50.c2_norms_build.literal_0004"), v_notes_window__574269, Authored("m50.c2daily_rows_build.literal_0079"), Authored("m50.c2daily_rows_build.literal_0080"), AuthoredCopy("m50.c2_document_coordination_rows.literal_0005")), Authored("m50.c2_norms_build.literal_0013"), Authored("m50.c2daily_rows_build.literal_0074")));
        _ = R.push(v_rows__563277, F_c2daily_after(F_c2daily_say(Authored("m50.c2_norms_build.literal_0003"), Authored("m50.c2daily_rows_build.literal_0081"), Authored("m50.c2_norms_build.literal_0004"), v_notes_window__574269, Authored("m50.c2daily_rows_build.literal_0082"), Authored("m50.c2daily_rows_build.literal_0083"), AuthoredCopy("m50.c2_document_coordination_rows.literal_0005")), Authored("m50.c2_norms_build.literal_0090"), Authored("m50.c2daily_rows_build.literal_0078")));
        _ = R.push(v_rows__563277, F_c2daily_after(F_c2daily_say(Authored("m50.c2_norms_build.literal_0033"), Authored("m50.c2daily_rows_build.literal_0084"), Authored("m50.c2_norms_build.literal_0004"), v_notes_window__574269, Authored("m50.c2daily_rows_build.literal_0085"), Authored("m50.c2daily_rows_build.literal_0086"), AuthoredCopy("m50.c2_document_coordination_rows.literal_0005")), Authored("m50.c2_norms_build.literal_0003"), Authored("m50.c2daily_rows_build.literal_0081")));
        object v_quake__578342 = DvValueTemplate.Instantiate("template.inline.c2daily_rows_build.1244");
        object v_color__578764 = F_dv_fact(F_c2daily_ref(Authored("m50.c2_cooperation_rows.literal_0056")), Authored("m50.c2_cooperation_rows.literal_0057"));
        object v_relay_index__578956 = R.number("0");
        while (R.Bool(DvValueTemplate.Instantiate("template.inline.c2daily_rows_build.1245", v_relay_index__578956)))
        {
            object v_suffix__579075 = R.show(R.eq(v_relay_index__578956, R.number("1")));
            object v_declaration__579123 = R.cat(Authored("m50.c2_cooperation_rows.literal_0068"), v_suffix__579075);
            object v_relay_id__579242 = R.cat(Authored("m50.c2daily_rows_build.literal_0087"), v_suffix__579075);
            object v_relay__579357 = F_c2d_row(Authored("m50.c2_alive.literal"), v_relay_id__579242, Authored("m50.c2_world_objects.literal_0013"), F_c2_night(Authored("m50.c2_any_night.literal_0003")), F_dv_true(), Authored("m50.c2daily_rows_build.literal_0088"), Authored("m50.c2daily_rows_build.literal_0089"), F_campaign_approved(Authored("m50.c2_personal_goals.literal_0008")));
            _ = R.push(v_rows__563277, DvValueTemplate.Instantiate("template.inline.c2daily_rows_build.1246", v_relay__579357, v_declaration__579123));
            v_relay_index__578956 = R.add(v_relay_index__578956, R.number("1"));
        }
        _ = R.push(v_rows__563277, DvValueTemplate.Instantiate("template.inline.c2daily_rows_build.1247", v_quake__578342, v_color__578764));
        object v_recent_quake__580810 = F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0021"), Authored("m50.c2_norm_condition.literal_0003")), true);
        object v_color_warning__581011 = DvValueTemplate.Instantiate("template.inline.c2daily_rows_build.1248", v_quake__578342, v_recent_quake__580810);
        _ = R.push(v_rows__563277, F_c2daily_after(v_color_warning__581011, Authored("m50.c2_alive.literal"), Authored("m50.c2daily_rows_build.literal_0091")));
        object v_guards__581739 = AuthoredCopy("m50.c2_world_objects.literal_0041");
        object v_g__581845 = R.number("0");
        while (R.Bool(R.lt(v_g__581845, R.count(v_guards__581739))))
        {
            object v_actor__581897 = R.at(v_guards__581739, v_g__581845);
            object v_id__581928 = R.cat(v_actor__581897, Authored("m50.c2daily_rows_build.literal_0098"));
            object v_caution__582036 = F_c2daily_say(v_actor__581897, v_id__581928, Authored("m50.c2_norms_build.literal_0048"), F_c2_night(Authored("m50.c2_any_night.literal_0003")), Authored("m50.c2daily_rows_build.literal_0099"), Authored("m50.c2daily_rows_build.literal_0100"), AuthoredCopy("m50.c2_cooperation_rows.literal_0063"));
            v_caution__582036 = DvValueTemplate.Instantiate("template.inline.c2daily_rows_build.1249", v_caution__582036, v_actor__581897, v_id__581928);
            _ = R.push(v_rows__563277, v_caution__582036);
            object v_commitment__583065 = F_campaign_own(v_actor__581897, Authored("m50.c2_safety_actions.literal_0012"));
            object v_guard_source__583186 = F_campaign_approved(Authored("m50.c2daily_rows_build.literal_0102"));
            object v_undertake__583308 = F_c2daily_say(v_actor__581897, R.cat(v_actor__581897, Authored("m50.c2_safety_goals.literal_0003")), Authored("m50.c2_norms_build.literal_0048"), F_c2_night(Authored("m50.c2_any_night.literal_0003")), Authored("m50.c2daily_rows_build.literal_0103"), Authored("m50.c2daily_rows_build.literal_0104"), AuthoredCopy("m50.c2_cooperation_rows.literal_0063"));
            v_undertake__583308 = DvValueTemplate.Instantiate("template.inline.c2daily_rows_build.1250", v_undertake__583308, v_actor__581897, v_guard_source__583186, v_commitment__583065);
            _ = R.push(v_rows__563277, v_undertake__583308);
            object v_decline__584617 = F_c2daily_say(v_actor__581897, R.cat(v_actor__581897, Authored("m50.c2daily_rows_build.literal_0106")), Authored("m50.c2_norms_build.literal_0048"), F_c2_night(Authored("m50.c2_any_night.literal_0003")), Authored("m50.c2daily_rows_build.literal_0107"), Authored("m50.c2daily_rows_build.literal_0108"), AuthoredCopy("m50.c2_cooperation_rows.literal_0063"));
            v_decline__584617 = DvValueTemplate.Instantiate("template.inline.c2daily_rows_build.1251", v_decline__584617, v_actor__581897, v_guard_source__583186, v_commitment__583065);
            _ = R.push(v_rows__563277, v_decline__584617);
            v_g__581845 = R.add(v_g__581845, R.number("1"));
        }
        object v_people__585938 = F_campaign_people();
        object v_venues__585974 = AuthoredCopy("m50.c2daily_rows_build.venues");
        object v_v__586076 = R.number("0");
        while (R.Bool(R.lt(v_v__586076, R.count(v_venues__585974))))
        {
            object v_room__586128 = R.at(v_venues__585974, v_v__586076);
            object v_weather__586158 = F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0021"), "wind"), true);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2daily_rows_build.1252", v_room__586128)))
            {
                v_weather__586158 = F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0021"), "wind"), false);
            }
            object v_present__586517 = new List<object> { F_c2d_council_when(), v_weather__586158 };
            object v_i__586570 = R.number("0");
            while (R.Bool(R.lt(v_i__586570, R.count(v_people__585938))))
            {
                object v_actor__586630 = R.at(v_people__585938, v_i__586570);
                object v_stance__586665 = F_campaign_any(F_dv_fact(F_campaign_own(v_actor__586630, Authored("m50.c2_safety_goals.literal_0002")), true), F_campaign_any(F_dv_fact(F_campaign_own(v_actor__586630, Authored("m50.c2_refresh_content_actions.literal")), true), F_dv_fact(F_campaign_own(v_actor__586630, Authored("m50.c2daily_rows_build.literal_0109")), true)));
                object v_options__587054 = AuthoredCopy("m50.c2daily_rows_build.options");
                object v_labels__587166 = AuthoredCopy("m50.c2daily_rows_build.labels");
                object v_n__587276 = R.number("0");
                while (R.Bool(R.lt(v_n__587276, R.count(v_options__587054))))
                {
                    object v_ballot__587345 = F_c2daily_say(v_actor__586630, R.cat(v_actor__586630, R.cat(Authored("m50.c2daily_rows_build.literal_0110"), R.cat(v_room__586128, R.at(v_options__587054, v_n__587276)))), v_room__586128, DvValueTemplate.Instantiate("template.inline.c2daily_rows_build.1253", v_present__586517, v_actor__586630, v_options__587054, v_n__587276), R.cat(Authored("m50.c2daily_rows_build.literal_0111"), R.at(v_labels__587166, v_n__587276)), R.cat(Authored("m50.c2daily_rows_build.literal_0112"), R.at(v_labels__587166, v_n__587276)), new List<object> {  });
                    v_ballot__587345 = DvValueTemplate.Instantiate("template.inline.c2daily_rows_build.1254", v_ballot__587345, v_actor__586630, v_options__587054, v_n__587276);
                    object v_norms__588465 = F_c2_norms();
                    object v_q__588505 = R.number("0");
                    while (R.Bool(R.lt(v_q__588505, R.count(v_norms__588465))))
                    {
                        object v_norm__588580 = R.at(v_norms__588465, v_q__588505);
                        if (R.Bool(R.eq(F_dv_get(v_norm__588580, "id"), R.at(v_options__587054, v_n__587276))))
                        {
                            v_ballot__587345 = DvValueTemplate.Instantiate("template.inline.c2daily_rows_build.1255", v_ballot__587345, v_actor__586630, v_norm__588580);
                        }
                        v_q__588505 = R.add(v_q__588505, R.number("1"));
                    }
                    _ = R.push(v_rows__563277, v_ballot__587345);
                    v_n__587276 = R.add(v_n__587276, R.number("1"));
                }
                object v_vote__589273 = F_c2daily_say(v_actor__586630, R.cat(v_actor__586630, R.cat(Authored("m50.c2daily_rows_build.literal_0115"), v_room__586128)), v_room__586128, DvValueTemplate.Instantiate("template.inline.c2daily_rows_build.1256", v_present__586517, v_actor__586630, v_stance__586665), Authored("m50.c2daily_rows_build.literal_0116"), Authored("m50.c2daily_rows_build.literal_0117"), new List<object> {  });
                v_vote__589273 = DvValueTemplate.Instantiate("template.inline.c2daily_rows_build.1257", v_vote__589273, v_actor__586630);
                v_vote__589273 = DvValueTemplate.Instantiate("template.inline.c2daily_rows_build.1258", v_vote__589273);
                _ = R.push(v_rows__563277, v_vote__589273);
                v_i__586570 = R.add(v_i__586570, R.number("1"));
            }
            v_v__586076 = R.add(v_v__586076, R.number("1"));
        }
        return v_rows__563277;
    }
    public static object F_c2daily_inherited_meal_cell(object v_domain,object v_field) {
        return DvValueTemplate.Instantiate("template.c2daily_inherited_meal_cell", v_domain, v_field);
    }
    public static object F_c2daily_fetch_ingredients_law() {
        return DvValueTemplate.Instantiate("template.c2daily_fetch_ingredients_law");
    }
    public static object F_c2daily_cook_law() {
        return DvValueTemplate.Instantiate("template.c2daily_cook_law");
    }
    public static object F_c2daily_deliver_law() {
        return DvValueTemplate.Instantiate("template.c2daily_deliver_law");
    }
    public static object F_c2daily_serve_law(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2daily_serve_law", v_actor);
    }
    public static object F_c2daily_draft_law() {
        return DvValueTemplate.Instantiate("template.c2daily_draft_law");
    }
    public static object F_c2daily_council_end_law() {
        return DvValueTemplate.Instantiate("template.c2daily_council_end_law");
    }
    public static object F_c2daily_council_close_law() {
        return DvValueTemplate.Instantiate("template.c2daily_council_close_law");
    }
    public static object F_c2daily_tremor_law() {
        return DvValueTemplate.Instantiate("template.c2daily_tremor_law");
    }
    public static object F_c2daily_closing_fields() {
        return DvValueTemplate.Instantiate("template.c2daily_closing_fields");
    }
    public static object F_c2daily_chapter_summary(object v_physical) {
        return DvValueTemplate.Instantiate("template.c2daily_chapter_summary", v_physical);
    }
    public static object F_c2daily_watch_law(object v_watched) {
        return DvValueTemplate.Instantiate("template.c2daily_watch_law", v_watched);
    }
    public static object F_c2daily_tasks() {
        return DvValueTemplate.Instantiate("template.c2daily_tasks");
    }
    public static object F_c2daily_task_law(object v_task) {
        return DvValueTemplate.Instantiate("template.c2daily_task_law", v_task);
    }
    public static object F_c2daily_install_scene(object v_scene) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1259", v_scene)))
        {
            return v_scene;
        }
        object v_source__608242 = F_campaign_approved(Authored("m50.c2daily_install_scene.literal_0002"));
        object v_fields__608357 = DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1260", v_scene);
        object v_tasks__609121 = F_c2daily_tasks();
        object v_t__609154 = R.number("0");
        while (R.Bool(R.lt(v_t__609154, R.count(v_tasks__609121))))
        {
            v_fields__608357 = F_dv_with(v_fields__608357, F_dv_get(R.at(v_tasks__609121, v_t__609154), "field"), F_dv_field(false));
            v_t__609154 = R.add(v_t__609154, R.number("1"));
        }
        object v_people__609287 = F_campaign_people();
        object v_f__609323 = R.number("0");
        while (R.Bool(R.lt(v_f__609323, R.count(v_people__609287))))
        {
            v_fields__608357 = DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1261", v_fields__608357, v_people__609287, v_f__609323);
            v_fields__608357 = DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1262", v_fields__608357, v_people__609287, v_f__609323);
            v_fields__608357 = DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1263", v_fields__608357, v_people__609287, v_f__609323);
            v_fields__608357 = DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1264", v_fields__608357, v_people__609287, v_f__609323);
            v_f__609323 = R.add(v_f__609323, R.number("1"));
        }
        v_fields__608357 = DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1265", v_fields__608357);
        object v_physical__610129 = F_campaign_add_new(F_dv_get(v_scene, "physical"), F_c2_object(Authored("m50.c2_inspection_presentation.literal_0015"), Authored("m50.c2_norms_build.literal_0048"), v_fields__608357));
        v_physical__610129 = F_dv_adopt(v_physical__610129, F_campaign_method(Authored("m50.c2daily_install_scene.literal_0004"), F_c2daily_council_end_law()), "enforce", R.number("1"), v_source__608242);
        object v_processes__610537 = F_dv_clone(F_dv_find(v_scene, Authored("m50.c2_extend_scene.literal_0002"), new List<object> {  }));
        _ = R.push(v_processes__610537, DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1266", v_source__608242));
        object v_closing_source__610876 = F_campaign_approved(Authored("m50.c2_navigation_goals.literal_0002"));
        v_physical__610129 = F_dv_adopt(v_physical__610129, F_campaign_method(Authored("m50.c2daily_install_scene.literal_0005"), F_c2daily_council_close_law()), "enforce", R.number("1"), new List<object> { v_closing_source__610876 });
        v_physical__610129 = F_dv_adopt(v_physical__610129, F_campaign_method(Authored("m50.c2daily_install_scene.literal_0006"), F_c2daily_tremor_law()), "enforce", R.number("1"), new List<object> { v_closing_source__610876 });
        _ = R.push(v_processes__610537, DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1267", v_closing_source__610876));
        _ = R.push(v_processes__610537, DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1268", v_closing_source__610876));
        object v_watch_states__611850 = AuthoredCopy("m50.c2_cooperation_rows.decisions");
        object v_w__611962 = R.number("0");
        while (R.Bool(R.lt(v_w__611962, R.count(v_watch_states__611850))))
        {
            object v_id__612020 = R.cat(Authored("m50.c2daily_install_scene.literal_0007"), R.show(R.at(v_watch_states__611850, v_w__611962)));
            v_physical__610129 = F_dv_adopt(v_physical__610129, F_campaign_method(v_id__612020, F_c2daily_watch_law(R.at(v_watch_states__611850, v_w__611962))), "enforce", R.number("1"), v_source__608242);
            _ = R.push(v_processes__610537, new List<object> { F_dv_pair("id", v_id__612020), F_dv_pair("law", v_id__612020), F_dv_pair("basis", new List<object> { v_source__608242 }) });
            v_w__611962 = R.add(v_w__611962, R.number("1"));
        }
        object v_bindings__612353 = F_dv_clone(F_dv_get(v_scene, "bindings"));
        object v_actors__612398 = new List<object> {  };
        object v_i__612419 = R.number("0");
        while (R.Bool(R.lt(v_i__612419, R.count(F_dv_get(v_scene, "actors")))))
        {
            object v_actor__612477 = R.at(F_dv_get(v_scene, "actors"), v_i__612419);
            object v_mind__612514 = F_campaign_add_new(F_dv_get(v_actor__612477, "mind"), F_campaign_local_object(Authored("m50.c2_inspection_presentation.literal_0015")));
            _ = R.push(v_bindings__612353, F_dv_binding(Authored("m50.c2_inspection_presentation.literal_0015"), Authored("m50.c2_inspection_presentation.literal_0015"), F_dv_get(v_actor__612477, "id")));
            object v_personal_record__613043 = DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1269");
            v_mind__612514 = F_dv_store(v_mind__612514, F_c2daily_ref(R.cat(Authored("m50.c2_inspection_detail_lines.literal_0040"), F_dv_get(v_actor__612477, "id"))), v_personal_record__613043);
            v_mind__612514 = F_dv_store(v_mind__612514, F_c2daily_ref(R.cat(Authored("m50.c2_inspection_detail_lines.literal_0042"), F_dv_get(v_actor__612477, "id"))), v_personal_record__613043);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1270", v_actor__612477)))
            {
                v_mind__612514 = F_dv_store(v_mind__612514, F_c2daily_ref(Authored("m50.c2_cooperation_rows.literal_0056")), F_dv_field(Authored("m50.c2_cooperation_rows.literal_0057")));
            }
            object v_actions__613831 = F_dv_clone(F_dv_get(v_actor__612477, "actions"));
            v_t__609154 = R.number("0");
            while (R.Bool(R.lt(v_t__609154, R.count(v_tasks__609121))))
            {
                object v_task__613933 = R.at(v_tasks__609121, v_t__609154);
                if (R.Bool(R.eq(F_dv_get(v_actor__612477, "id"), F_dv_get(v_task__613933, "actor"))))
                {
                    object v_law__614010 = F_c2daily_task_law(v_task__613933);
                    v_physical__610129 = F_dv_adopt(v_physical__610129, F_campaign_method(F_dv_get(v_task__613933, "id"), v_law__614010), "enforce", R.number("1"), v_source__608242);
                    v_mind__612514 = F_dv_adopt(v_mind__612514, F_campaign_method(F_dv_get(v_task__613933, "id"), v_law__614010), "predict", R.number("1"), v_source__608242);
                    v_mind__612514 = F_dv_store(v_mind__612514, F_c2daily_ref(F_dv_get(v_task__613933, "field")), F_dv_field(false));
                    object v_action__614333 = DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1271", v_task__613933, v_law__614010, v_source__608242);
                    if (R.Bool(F_dv_has_key(v_task__613933, "received_speech_requirement")))
                    {
                        v_action__614333 = F_dv_with(v_action__614333, "received_speech_requirement", F_dv_get(v_task__613933, "received_speech_requirement"));
                    }
                    _ = R.push(v_actions__613831, v_action__614333);
                }
                v_t__609154 = R.add(v_t__609154, R.number("1"));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1272", v_actor__612477)))
            {
                object v_id__614932 = Authored("m50.c2daily_install_scene.id");
                object v_law__615022 = F_c2daily_draft_law();
                v_physical__610129 = F_dv_adopt(v_physical__610129, F_campaign_method(v_id__614932, v_law__615022), "enforce", R.number("1"), v_source__608242);
                v_mind__612514 = F_dv_adopt(v_mind__612514, F_campaign_method(v_id__614932, v_law__615022), "predict", R.number("1"), v_source__608242);
                v_mind__612514 = F_dv_store(v_mind__612514, F_c2daily_ref(Authored("m50.c2_norm_available.literal_0002")), F_dv_field(false));
                _ = R.push(v_actions__613831, DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1273", v_id__614932, v_law__615022, v_source__608242));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1274", v_actor__612477)))
            {
                object v_take_id__615921 = Authored("m50.c2daily_install_scene.take_id");
                object v_take_law__616021 = F_c2daily_fetch_ingredients_law();
                v_physical__610129 = F_dv_adopt(v_physical__610129, F_campaign_method(v_take_id__615921, v_take_law__616021), "enforce", R.number("1"), v_source__608242);
                v_mind__612514 = F_dv_adopt(v_mind__612514, F_campaign_method(v_take_id__615921, v_take_law__616021), "predict", R.number("1"), v_source__608242);
                _ = R.push(v_actions__613831, DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1275", v_take_id__615921, v_take_law__616021, v_source__608242));
                object v_id__617320 = Authored("m50.c2daily_install_scene.id_0002");
                object v_law__617415 = F_c2daily_cook_law();
                v_physical__610129 = F_dv_adopt(v_physical__610129, F_campaign_method(v_id__617320, v_law__617415), "enforce", R.number("1"), v_source__608242);
                v_mind__612514 = F_dv_adopt(v_mind__612514, F_campaign_method(v_id__617320, v_law__617415), "predict", R.number("1"), v_source__608242);
                v_mind__612514 = F_dv_store(v_mind__612514, F_c2daily_ref(Authored("m50.c2_personal_goal_templates_build.literal_0026")), F_c2daily_inherited_meal_cell(v_mind__612514, Authored("m50.c2_personal_goal_templates_build.literal_0026")));
                v_mind__612514 = F_dv_store(v_mind__612514, F_c2daily_ref(Authored("m50.c2daily_inherited_meal_cell.literal")), F_c2daily_inherited_meal_cell(v_mind__612514, Authored("m50.c2daily_inherited_meal_cell.literal")));
                _ = R.push(v_actions__613831, DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1276", v_id__617320, v_law__617415, v_source__608242));
                object v_delivery__618848 = Authored("m50.c2daily_install_scene.delivery");
                object v_delivery_law__618950 = F_c2daily_deliver_law();
                v_physical__610129 = F_dv_adopt(v_physical__610129, F_campaign_method(v_delivery__618848, v_delivery_law__618950), "enforce", R.number("1"), v_source__608242);
                v_mind__612514 = F_dv_adopt(v_mind__612514, F_campaign_method(v_delivery__618848, v_delivery_law__618950), "predict", R.number("1"), v_source__608242);
                _ = R.push(v_actions__613831, DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1277", v_delivery__618848, v_delivery_law__618950, v_source__608242));
            }
            if (R.Bool(F_dv_has(F_campaign_people(), F_dv_get(v_actor__612477, "id"))))
            {
                object v_id__620018 = R.cat(F_dv_get(v_actor__612477, "id"), Authored("m50.c2_personal_goal_templates_build.literal_0027"));
                object v_law__620147 = F_c2daily_serve_law(F_dv_get(v_actor__612477, "id"));
                v_physical__610129 = F_dv_adopt(v_physical__610129, F_campaign_method(v_id__620018, v_law__620147), "enforce", R.number("1"), v_source__608242);
                v_mind__612514 = F_dv_adopt(v_mind__612514, F_campaign_method(v_id__620018, v_law__620147), "predict", R.number("1"), v_source__608242);
                _ = R.push(v_actions__613831, DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1278", v_id__620018, v_law__620147, v_actor__612477, v_source__608242));
            }
            _ = R.push(v_actions__613831, F_c2_inspect_action(F_dv_get(v_actor__612477, "id"), Authored("m50.c2_inspection_presentation.literal_0015"), Authored("m50.c2_norms_build.literal_0048"), AuthoredCopy("m50.c2daily_install_scene.literal_0019"), Authored("m50.c2daily_install_scene.literal_0020"), Authored("m50.c2daily_install_scene.literal_0021"), v_source__608242));
            _ = R.push(v_actions__613831, F_dv_with(F_c2_inspect_action(F_dv_get(v_actor__612477, "id"), Authored("m50.c2_inspection_presentation.literal_0015"), Authored("m50.c2_norms_build.literal_0004"), AuthoredCopy("m50.c2daily_install_scene.literal_0022"), Authored("m50.c2daily_install_scene.literal_0023"), Authored("m50.c2daily_install_scene.literal_0024"), v_source__608242), "id", R.cat(F_dv_get(v_actor__612477, "id"), Authored("m50.c2daily_install_scene.literal_0025"))));
            object v_venues__622082 = AuthoredCopy("m50.c2daily_rows_build.venues");
            object v_venue_index__622188 = R.number("0");
            while (R.Bool(R.lt(v_venue_index__622188, R.count(v_venues__622082))))
            {
                object v_room__622268 = R.at(v_venues__622082, v_venue_index__622188);
                object v_council_when__622312 = F_dv_all(new List<object> { F_campaign_at(F_dv_get(v_actor__612477, "id"), v_room__622268), F_c2d_council_when() });
                object v_council_fields__622402 = DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1279", v_actor__612477);
                object v_council__622980 = F_c2_inspect_action(F_dv_get(v_actor__612477, "id"), Authored("m50.c2_inspection_presentation.literal_0015"), v_room__622268, v_council_fields__622402, Authored("m50.c2daily_install_scene.literal_0029"), Authored("m50.c2daily_install_scene.literal_0030"), v_source__608242);
                v_council__622980 = DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1280", v_council__622980, v_actor__612477, v_room__622268, v_council_when__622312);
                _ = R.push(v_actions__613831, v_council__622980);
                object v_when__623580 = DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1281", v_actor__612477, v_room__622268);
                object v_closing__623871 = F_c2_inspect_action(F_dv_get(v_actor__612477, "id"), Authored("m50.c2_inspection_presentation.literal_0015"), v_room__622268, F_c2daily_closing_fields(), Authored("m50.c2daily_install_scene.literal_0032"), Authored("m50.c2daily_install_scene.literal_0033"), v_closing_source__610876);
                v_closing__623871 = DvValueTemplate.Instantiate("template.inline.c2daily_install_scene.1282", v_closing__623871, v_actor__612477, v_room__622268, v_when__623580);
                _ = R.push(v_actions__613831, v_closing__623871);
                v_venue_index__622188 = R.add(v_venue_index__622188, R.number("1"));
            }
            v_actor__612477 = F_dv_with_many(v_actor__612477, new List<object> { F_dv_pair("mind", v_mind__612514), F_dv_pair("actions", F_campaign_public_methods(v_actions__613831)) });
            _ = R.push(v_actors__612398, v_actor__612477);
            v_i__612419 = R.add(v_i__612419, R.number("1"));
        }
        return F_dv_with_many(v_scene, new List<object> { F_dv_pair("physical", v_physical__610129), F_dv_pair("bindings", v_bindings__612353), F_dv_pair("actors", v_actors__612398), F_dv_pair("chapter2_daily_installed", true), F_dv_pair("world_processes", v_processes__610537) });
    }
    public static object F_c2d_ref(object v_field) {
        return DvValueTemplate.Instantiate("template.c2d_ref", v_field);
    }
    public static object F_c2d_person_ref(object v_actor,object v_field) {
        return DvValueTemplate.Instantiate("template.c2d_person_ref", v_actor, v_field);
    }
    public static object F_c2d_mark(object v_actor,object v_method) {
        return DvValueTemplate.Instantiate("template.c2d_mark", v_actor, v_method);
    }
    public static object F_c2d_source(object v_file,object v_line,object v_id) {
        return DvValueTemplate.Instantiate("template.c2d_source", v_file, v_line, v_id);
    }
    public static object F_c2d_people() {
        return DvValueTemplate.Instantiate("template.c2d_people");
    }
    public static object F_c2d_row(object v_actor,object v_id,object v_room,object v_when,object v_literal,object v_label,object v_text,object v_source) {
        return DvValueTemplate.Instantiate("template.c2d_row", v_actor, v_id, v_room, v_when, v_literal, v_label, v_text, v_source);
    }
    public static object F_c2d_question(object v_id,object v_room,object v_when,object v_target,object v_topic,object v_label,object v_text,object v_refs,object v_source) {
        return DvValueTemplate.Instantiate("template.c2d_question", v_id, v_room, v_when, v_target, v_topic, v_label, v_text, v_refs, v_source);
    }
    public static object F_c2d_reply(object v_actor,object v_id,object v_room,object v_when,object v_literal,object v_topic,object v_label,object v_text,object v_source) {
        return DvValueTemplate.Instantiate("template.c2d_reply", v_actor, v_id, v_room, v_when, v_literal, v_topic, v_label, v_text, v_source);
    }
    public static object F_c2d_introductions() {
        object v_key__626389 = AuthoredCopy("m50.c2d_introductions.key");
        object v_cached__626484 = R.memo_get(new List<object> {  }, v_key__626389);
        if (R.Bool(R.at(v_cached__626484, R.number("0"))))
        {
            return R.at(v_cached__626484, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__626389, F_c2d_introductions_build());
    }
    public static object F_c2d_introductions_build() {
        return DvValueTemplate.Instantiate("template.c2d_introductions_build");
    }
    public static object F_c2d_curriculum() {
        return DvValueTemplate.Instantiate("template.c2d_curriculum");
    }
    public static object F_c2d_rows() {
        object v_key__632656 = AuthoredCopy("m50.c2d_rows.key");
        object v_cached__632742 = R.memo_get(new List<object> {  }, v_key__632656);
        if (R.Bool(R.at(v_cached__632742, R.number("0"))))
        {
            return R.at(v_cached__632742, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__632656, F_c2d_rows_build());
    }
    public static object F_c2d_rows_build() {
        object v_rows__632901 = new List<object> {  };
        object v_contract_ref__632920 = F_c2d_ref(Authored("m50.c2d_rows_build.literal"));
        object v_contract_source__633019 = F_campaign_source(Authored("m50.c2_foolish_duty_actions.literal"), R.number("0"), Authored("m50.c2d_rows_build.literal_0002"));
        object v_trace__633213 = DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1283");
        object v_rule4__633938 = F_dv_fact(F_c2d_ref(Authored("m50.c2daily_rows_build.literal_0047")), Authored("m50.c2daily_rows_build.literal_0048"));
        object v_authority__634124 = F_c2d_question(Authored("m50.c2d_rows_build.literal_0008"), Authored("m50.c2_norms_build.literal_0048"), v_trace__633213, Authored("m50.c2_alive.literal"), Authored("m50.c2d_rows_build.literal_0009"), Authored("m50.c2d_rows_build.literal_0010"), Authored("m50.c2d_rows_build.literal_0011"), new List<object> {  }, v_contract_source__633019);
        v_authority__634124 = DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1284", v_authority__634124);
        _ = R.push(v_rows__632901, v_authority__634124);
        object v_sealed__634765 = F_dv_fact(F_c2d_ref(Authored("m50.c2d_rows_build.literal_0013")), Authored("m50.c2d_rows_build.literal_0014"));
        _ = R.push(v_rows__632901, F_c2d_reply(Authored("m50.c2_alive.literal"), Authored("m50.c2d_rows_build.literal_0015"), Authored("m50.c2_norms_build.literal_0048"), F_dv_true(), v_sealed__634765, Authored("m50.c2d_rows_build.literal_0009"), Authored("m50.c2d_rows_build.literal_0016"), Authored("m50.c2d_rows_build.literal_0017"), v_contract_source__633019));
        object v_other_rules__635431 = F_c2d_question(Authored("m50.c2d_rows_build.literal_0018"), Authored("m50.c2_norms_build.literal_0057"), F_dv_all(new List<object> { v_rule4__633938, v_sealed__634765 }), Authored("m50.c2_alive.literal"), Authored("m50.c2d_rows_build.literal_0019"), Authored("m50.c2d_rows_build.literal_0020"), Authored("m50.c2daily_rows_build.literal_0045"), new List<object> {  }, v_contract_source__633019);
        v_other_rules__635431 = DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1285", v_other_rules__635431);
        _ = R.push(v_rows__632901, v_other_rules__635431);
        _ = R.push(v_rows__632901, F_c2d_reply(Authored("m50.c2_alive.literal"), Authored("m50.c2d_rows_build.literal_0022"), Authored("m50.c2_norms_build.literal_0057"), F_dv_true(), F_dv_fact(F_c2d_ref(Authored("m50.c2d_rows_build.literal_0023")), true), Authored("m50.c2d_rows_build.literal_0019"), Authored("m50.c2d_rows_build.literal_0024"), Authored("m50.c2d_rows_build.literal_0025"), v_contract_source__633019));
        object v_origin__636677 = F_c2d_question(Authored("m50.c2d_rows_build.literal_0026"), Authored("m50.c2_norms_build.literal_0057"), v_rule4__633938, Authored("m50.c2_alive.literal"), Authored("m50.c2d_rows_build.literal_0027"), Authored("m50.c2d_rows_build.literal_0028"), Authored("m50.c2d_rows_build.literal_0029"), new List<object> {  }, v_contract_source__633019);
        v_origin__636677 = DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1286", v_origin__636677);
        _ = R.push(v_rows__632901, v_origin__636677);
        object v_bounded__637306 = F_dv_fact(F_c2d_ref(Authored("m50.c2d_rows_build.literal_0023")), true);
        _ = R.push(v_rows__632901, F_c2d_reply(Authored("m50.c2_alive.literal"), Authored("m50.c2d_rows_build.literal_0031"), Authored("m50.c2_norms_build.literal_0057"), F_dv_true(), v_bounded__637306, Authored("m50.c2d_rows_build.literal_0027"), Authored("m50.c2d_rows_build.literal_0032"), Authored("m50.c2d_rows_build.literal_0033"), v_contract_source__633019));
        object v_full__637908 = F_c2d_question(Authored("m50.c2d_rows_build.literal_0034"), Authored("m50.c2_norms_build.literal_0057"), F_dv_all(new List<object> { v_trace__633213, v_rule4__633938, v_sealed__634765, v_bounded__637306 }), Authored("m50.c2_alive.literal"), Authored("m50.c2d_rows_build.literal_0035"), Authored("m50.c2d_rows_build.literal_0036"), Authored("m50.c2d_rows_build.literal_0037"), new List<object> { v_contract_ref__632920 }, v_contract_source__633019);
        v_full__637908 = DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1287", v_full__637908);
        _ = R.push(v_rows__632901, v_full__637908);
        _ = R.push(v_rows__632901, F_c2d_reply(Authored("m50.c2_alive.literal"), Authored("m50.c2d_rows_build.literal_0039"), Authored("m50.c2_norms_build.literal_0057"), F_dv_true(), F_dv_fact(v_contract_ref__632920, F_campaign_contract_text()), Authored("m50.c2d_rows_build.literal_0035"), Authored("m50.c2d_rows_build.literal_0040"), F_campaign_contract_text(), v_contract_source__633019));
        object v_people__639057 = F_campaign_people();
        object v_p__639093 = R.number("0");
        while (R.Bool(R.lt(v_p__639093, R.count(v_people__639057))))
        {
            object v_known_contract__639145 = F_dv_fact(v_contract_ref__632920, F_campaign_contract_text());
            _ = R.push(v_rows__632901, F_c2d_row(R.at(v_people__639057, v_p__639093), R.cat(R.at(v_people__639057, v_p__639093), Authored("m50.c2d_rows_build.literal_0041")), Authored("m50.c2_norms_build.literal_0048"), v_known_contract__639145, v_known_contract__639145, Authored("m50.c2d_rows_build.literal_0042"), F_campaign_contract_text(), v_contract_source__633019));
            v_p__639093 = R.add(v_p__639093, R.number("1"));
        }
        object v_daily__639591 = F_c2daily_rows();
        object v_d__639623 = R.number("0");
        while (R.Bool(R.lt(v_d__639623, R.count(v_daily__639591))))
        {
            _ = R.push(v_rows__632901, R.at(v_daily__639591, v_d__639623));
            v_d__639623 = R.add(v_d__639623, R.number("1"));
        }
        object v_cooperation__639722 = F_c2_cooperation_rows();
        v_d__639623 = R.number("0");
        while (R.Bool(R.lt(v_d__639623, R.count(v_cooperation__639722))))
        {
            _ = R.push(v_rows__632901, R.at(v_cooperation__639722, v_d__639623));
            v_d__639623 = R.add(v_d__639623, R.number("1"));
        }
        object v_intros__639874 = F_c2d_introductions();
        object v_i__639912 = R.number("0");
        while (R.Bool(R.lt(v_i__639912, R.count(v_intros__639874))))
        {
            object v_intro__639964 = R.at(v_intros__639874, v_i__639912);
            object v_literal__639995 = DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1288", v_intro__639964);
            object v_row__640230 = F_c2d_row(F_dv_get(v_intro__639964, "actor"), R.cat(F_dv_get(v_intro__639964, "actor"), Authored("m50.c2d_rows_build.literal_0044")), Authored("m50.c2_norms_build.literal_0048"), v_literal__639995, v_literal__639995, Authored("m50.c2d_rows_build.literal_0045"), F_dv_get(v_intro__639964, "text"), F_dv_get(v_intro__639964, "source"));
            _ = R.push(v_rows__632901, DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1289", v_row__640230));
            v_i__639912 = R.add(v_i__639912, R.number("1"));
        }
        object v_courses__640710 = F_c2d_curriculum();
        v_i__639912 = R.number("0");
        while (R.Bool(R.lt(v_i__639912, R.count(v_courses__640710))))
        {
            object v_course__640795 = R.at(v_courses__640710, v_i__639912);
            object v_literal__640828 = F_dv_fact(F_c2d_ref(F_dv_get(v_course__640795, "id")), F_dv_get(v_course__640795, "text"));
            object v_row__640892 = F_c2d_row(Authored("m50.c2_alive.literal"), R.cat(Authored("m50.c2d_rows_build.literal_0046"), F_dv_get(v_course__640795, "id")), Authored("m50.c2_norms_build.literal_0057"), DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1290", v_literal__640828), v_literal__640828, R.cat(Authored("m50.c2d_rows_build.literal_0047"), F_dv_get(v_course__640795, "title")), F_dv_get(v_course__640795, "text"), F_dv_get(v_course__640795, "source"));
            v_row__640892 = DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1291", v_row__640892);
            _ = R.push(v_rows__632901, v_row__640892);
            v_i__639912 = R.add(v_i__639912, R.number("1"));
        }
        object v_before__641686 = F_dv_fact(F_c2d_mark(Authored("m50.c2_alive.literal"), Authored("m50.c2d_rows_build.literal_0049")), true);
        _ = R.push(v_rows__632901, F_c2d_row(Authored("m50.c2_alive.literal"), Authored("m50.c2d_rows_build.literal_0050"), Authored("m50.c2_norms_build.literal_0057"), v_before__641686, F_dv_true(), Authored("m50.c2d_rows_build.literal_0051"), Authored("m50.c2d_rows_build.literal_0052"), F_c2d_source(Authored("m50.c2_personal_goal_templates_build.literal_0013"), Authored("m50.c2d_rows_build.literal_0053"), Authored("m50.c2d_rows_build.literal_0054"))));
        object v_rooms__642503 = AuthoredCopy("m50.c2d_rows_build.rooms");
        v_i__639912 = R.number("0");
        while (R.Bool(R.lt(v_i__639912, R.count(v_rooms__642503))))
        {
            object v_room__642646 = R.at(v_rooms__642503, v_i__639912);
            object v_after_night__642675 = F_dv_true();
            _ = R.push(v_rows__632901, F_c2d_question(R.cat(Authored("m50.c2d_rows_build.literal_0055"), v_room__642646), v_room__642646, v_after_night__642675, Authored("m50.c2_norms_build.literal_0033"), Authored("m50.c2d_rows_build.literal_0056"), Authored("m50.c2d_rows_build.literal_0057"), Authored("m50.c2d_rows_build.literal_0058"), DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1292"), F_c2d_source(Authored("m50.c2_extend_world.literal_0010"), Authored("m50.c2d_rows_build.literal_0059"), Authored("m50.c2d_rows_build.literal_0060"))));
            _ = R.push(v_rows__632901, F_c2d_question(R.cat(Authored("m50.c2d_rows_build.literal_0061"), v_room__642646), v_room__642646, v_after_night__642675, Authored("m50.c2_supply_actions.literal_0027"), Authored("m50.c2d_rows_build.literal_0062"), Authored("m50.c2d_rows_build.literal_0063"), Authored("m50.c2d_rows_build.literal_0064"), DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1293"), F_c2d_source(Authored("m50.c2_special_actions.literal_0005"), Authored("m50.c2d_rows_build.literal_0065"), Authored("m50.c2d_rows_build.literal_0066"))));
            _ = R.push(v_rows__632901, F_c2d_question(R.cat(Authored("m50.c2d_rows_build.literal_0067"), v_room__642646), v_room__642646, v_after_night__642675, Authored("m50.c2_supply_actions.literal_0027"), Authored("m50.c2d_rows_build.literal_0068"), Authored("m50.c2d_rows_build.literal_0069"), Authored("m50.c2d_rows_build.literal_0070"), DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1294"), F_c2d_source(Authored("m50.c2_extend_world.literal_0010"), Authored("m50.c2d_rows_build.literal_0072"), Authored("m50.c2d_rows_build.literal_0073"))));
            _ = R.push(v_rows__632901, F_c2d_question(R.cat(Authored("m50.c2d_rows_build.literal_0074"), v_room__642646), v_room__642646, v_after_night__642675, Authored("m50.c2_norms_build.literal_0003"), Authored("m50.c2d_rows_build.literal_0075"), Authored("m50.c2d_rows_build.literal_0076"), Authored("m50.c2d_rows_build.literal_0077"), DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1295"), F_c2d_source(Authored("m50.c2_extend_world.literal_0010"), Authored("m50.c2d_rows_build.literal_0053"), Authored("m50.c2d_rows_build.literal_0078"))));
            _ = R.push(v_rows__632901, F_c2d_question(R.cat(Authored("m50.c2d_rows_build.literal_0079"), v_room__642646), v_room__642646, v_after_night__642675, Authored("m50.c2_world_objects.literal_0030"), Authored("m50.c2d_rows_build.literal_0080"), Authored("m50.c2d_rows_build.literal_0081"), Authored("m50.c2d_rows_build.literal_0082"), DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1296"), F_c2d_source(Authored("m50.c2_special_actions.literal_0005"), Authored("m50.c2d_rows_build.literal_0084"), Authored("m50.c2d_rows_build.literal_0085"))));
            object v_mora_fact__646868 = DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1297");
            _ = R.push(v_rows__632901, F_c2d_reply(Authored("m50.c2_norms_build.literal_0033"), R.cat(Authored("m50.c2d_rows_build.literal_0087"), v_room__642646), v_room__642646, v_mora_fact__646868, v_mora_fact__646868, Authored("m50.c2d_rows_build.literal_0056"), Authored("m50.c2d_rows_build.literal_0088"), Authored("m50.c2d_rows_build.literal_0089"), F_c2d_source(Authored("m50.c2_special_actions.literal_0005"), Authored("m50.c2d_rows_build.literal_0090"), Authored("m50.c2d_rows_build.literal_0091"))));
            object v_quer_fact__647929 = DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1298");
            _ = R.push(v_rows__632901, F_c2d_reply(Authored("m50.c2_supply_actions.literal_0027"), R.cat(Authored("m50.c2d_rows_build.literal_0093"), v_room__642646), v_room__642646, v_quer_fact__647929, v_quer_fact__647929, Authored("m50.c2d_rows_build.literal_0062"), Authored("m50.c2d_rows_build.literal_0094"), Authored("m50.c2d_rows_build.literal_0095"), F_c2d_source(Authored("m50.c2_special_actions.literal_0005"), Authored("m50.c2d_rows_build.literal_0096"), Authored("m50.c2d_rows_build.literal_0097"))));
            object v_repaired__648997 = F_dv_fact(F_c2d_person_ref(Authored("m50.c2_supply_actions.literal_0027"), Authored("m50.c2d_rows_build.literal_0071")), true);
            _ = R.push(v_rows__632901, F_c2d_reply(Authored("m50.c2_supply_actions.literal_0027"), R.cat(Authored("m50.c2d_rows_build.literal_0098"), v_room__642646), v_room__642646, v_repaired__648997, v_repaired__648997, Authored("m50.c2d_rows_build.literal_0068"), Authored("m50.c2d_rows_build.literal_0099"), Authored("m50.c2d_rows_build.literal_0100"), F_c2d_source(Authored("m50.c2_special_actions.literal_0005"), Authored("m50.c2_personal_goal_templates_build.literal_0014"), Authored("m50.c2d_rows_build.literal_0101"))));
            object v_ledger__649885 = DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1299");
            _ = R.push(v_rows__632901, F_c2d_reply(Authored("m50.c2_norms_build.literal_0003"), R.cat(Authored("m50.c2d_rows_build.literal_0102"), v_room__642646), v_room__642646, v_ledger__649885, v_ledger__649885, Authored("m50.c2d_rows_build.literal_0075"), Authored("m50.c2d_rows_build.literal_0103"), Authored("m50.c2d_rows_build.literal_0104"), F_c2d_source(Authored("m50.c2_special_actions.literal_0005"), Authored("m50.c2d_rows_build.literal_0105"), Authored("m50.c2d_rows_build.literal_0106"))));
            object v_small__650943 = F_dv_fact(F_c2d_person_ref(Authored("m50.c2_world_objects.literal_0030"), Authored("m50.c2d_rows_build.literal_0083")), true);
            _ = R.push(v_rows__632901, F_c2d_reply(Authored("m50.c2_world_objects.literal_0030"), R.cat(Authored("m50.c2d_rows_build.literal_0107"), v_room__642646), v_room__642646, v_small__650943, v_small__650943, Authored("m50.c2d_rows_build.literal_0080"), Authored("m50.c2d_rows_build.literal_0108"), Authored("m50.c2d_rows_build.literal_0109"), F_c2d_source(Authored("m50.c2_special_actions.literal_0005"), Authored("m50.c2d_rows_build.literal_0110"), Authored("m50.c2d_rows_build.literal_0111"))));
            v_i__639912 = R.add(v_i__639912, R.number("1"));
        }
        object v_requested_repair__651817 = F_dv_fact(F_c2d_person_ref(Authored("m50.c2_initial_body.literal_0020"), Authored("m50.c2d_rows_build.literal_0112")), true);
        object v_marks__652020 = F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0006"), Authored("m50.c2_total_store_open_known.literal_0026")), true);
        object v_repair_witness__652222 = F_c2d_row(Authored("m50.c2_initial_body.literal_0020"), Authored("m50.c2d_rows_build.literal_0113"), Authored("m50.c2_norms_build.literal_0004"), F_dv_all(new List<object> { v_requested_repair__651817, v_marks__652020 }), F_dv_all(new List<object> { v_requested_repair__651817, v_marks__652020 }), Authored("m50.c2d_rows_build.literal_0114"), Authored("m50.c2d_rows_build.literal_0115"), F_c2d_source(Authored("m50.c2_special_actions.literal_0005"), Authored("m50.c2d_curriculum.literal_0009"), Authored("m50.c2d_rows_build.literal_0116")));
        _ = R.push(v_rows__632901, DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1300", v_repair_witness__652222));
        object v_council__653065 = F_c2d_council_when();
        object v_places__653103 = AuthoredCopy("m50.c2daily_rows_build.venues");
        v_i__639912 = R.number("0");
        while (R.Bool(R.lt(v_i__639912, R.count(v_places__653103))))
        {
            object v_room__653253 = R.at(v_places__653103, v_i__639912);
            object v_weather__653283 = F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0021"), "wind"), true);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1301", v_room__653253)))
            {
                v_weather__653283 = F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0021"), "wind"), false);
            }
            object v_when__653642 = F_dv_all(new List<object> { v_council__653065, v_weather__653283 });
            object v_source__653689 = F_campaign_source(Authored("m50.c2_foolish_duty_actions.literal"), R.number("0"), Authored("m50.c2d_rows_build.literal_0118"));
            object v_proposals__653878 = F_c2_norms();
            object v_n__653914 = R.number("0");
            while (R.Bool(R.lt(v_n__653914, R.count(v_proposals__653878))))
            {
                object v_norm__653977 = R.at(v_proposals__653878, v_n__653914);
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1302", v_norm__653977)))
                {
                    object v_literal__654137 = F_dv_fact(F_campaign_ref(F_c2_norm_object(F_dv_get(v_norm__653977, "id")), "text"), F_dv_get(v_norm__653977, "surface"));
                    object v_row__654237 = F_c2d_row(F_dv_get(v_norm__653977, "owner"), R.cat(F_dv_get(v_norm__653977, "owner"), R.cat(Authored("m50.c2d_rows_build.literal_0119"), R.cat(v_room__653253, F_dv_get(v_norm__653977, "id")))), v_room__653253, F_dv_all(new List<object> { v_when__653642, F_c2d_stance(F_dv_get(v_norm__653977, "owner"), F_dv_get(v_norm__653977, "id")) }), v_literal__654137, R.cat(Authored("m50.c2d_rows_build.literal_0120"), F_dv_get(v_norm__653977, "title")), F_dv_get(v_norm__653977, "surface"), v_source__653689);
                    _ = R.push(v_rows__632901, F_dv_with(v_row__654237, "norm_proposal", v_norm__653977));
                }
                v_n__653914 = R.add(v_n__653914, R.number("1"));
            }
            v_i__639912 = R.add(v_i__639912, R.number("1"));
        }
        object v_rescue_source__654683 = F_campaign_source(Authored("m50.c2_foolish_duty_actions.literal"), R.number("0"), Authored("m50.c2d_rows_build.literal_0121"));
        object v_rescue_history__654875 = F_dv_fact(F_campaign_own(Authored("m50.c2_world_objects.literal_0030"), Authored("m50.c2_inspections.literal_0042")), true);
        object v_rescue_report__655075 = F_c2d_row(Authored("m50.c2_world_objects.literal_0030"), Authored("m50.c2d_rows_build.literal_0122"), Authored("m50.c2_norms_build.literal_0048"), F_c2_can_act(Authored("m50.c2_world_objects.literal_0030")), v_rescue_history__654875, Authored("m50.c2d_rows_build.literal_0123"), Authored("m50.c2d_rows_build.literal_0124"), v_rescue_source__654683);
        v_rescue_report__655075 = DvValueTemplate.Instantiate("template.inline.c2d_rows_build.1303", v_rescue_report__655075);
        _ = R.push(v_rows__632901, v_rescue_report__655075);
        object v_comforts__655757 = F_c2d_companion_comfort_rows();
        object v_c__655806 = R.number("0");
        while (R.Bool(R.lt(v_c__655806, R.count(v_comforts__655757))))
        {
            _ = R.push(v_rows__632901, R.at(v_comforts__655757, v_c__655806));
            v_c__655806 = R.add(v_c__655806, R.number("1"));
        }
        object v_rebukes__655911 = F_c2d_demon_rebuke_rows();
        v_c__655806 = R.number("0");
        while (R.Bool(R.lt(v_c__655806, R.count(v_rebukes__655911))))
        {
            _ = R.push(v_rows__632901, R.at(v_rebukes__655911, v_c__655806));
            v_c__655806 = R.add(v_c__655806, R.number("1"));
        }
        object v_shura_rows__656053 = F_c2d_shura_rows();
        v_c__655806 = R.number("0");
        while (R.Bool(R.lt(v_c__655806, R.count(v_shura_rows__656053))))
        {
            _ = R.push(v_rows__632901, R.at(v_shura_rows__656053, v_c__655806));
            v_c__655806 = R.add(v_c__655806, R.number("1"));
        }
        return F_c2m_concat(v_rows__632901, F_c2u_rows());
    }
    public static object F_c2d_companion_comfort_rows() {
        return DvValueTemplate.Instantiate("template.c2d_companion_comfort_rows");
    }
    public static object F_c2d_demon_rebuke_rows() {
        return DvValueTemplate.Instantiate("template.c2d_demon_rebuke_rows");
    }
    public static object F_c2d_demon_trial_dialogues() {
        return DvValueTemplate.Instantiate("template.c2d_demon_trial_dialogues");
    }
    public static object F_c2d_demon_incite_guard() {
        return DvValueTemplate.Instantiate("template.c2d_demon_incite_guard");
    }
    public static object F_c2d_shura_rows() {
        return DvValueTemplate.Instantiate("template.c2d_shura_rows");
    }
    public static object F_c2d_council_when() {
        return DvValueTemplate.Instantiate("template.c2d_council_when");
    }
    public static object F_c2d_known_council_proposal() {
        return DvValueTemplate.Instantiate("template.c2d_known_council_proposal");
    }
    public static object F_c2d_stance(object v_actor,object v_norm) {
        return DvValueTemplate.Instantiate("template.c2d_stance", v_actor, v_norm);
    }
    public static object F_c2d_row_law(object v_row) {
        return DvValueTemplate.Instantiate("template.c2d_row_law", v_row);
    }
    public static object F_c2d_spoke_law(object v_行动者,object v_method,object v_room) {
        return DvValueTemplate.Instantiate("template.c2d_spoke_law", v_行动者, v_method, v_room);
    }
    public static object F_c2d_read_scope_law() {
        return DvValueTemplate.Instantiate("template.c2d_read_scope_law");
    }
    public static object F_c2d_action(object v_row) {
        object v_prerequisites__676768 = new List<object> { F_campaign_at(F_dv_get(v_row, "actor"), F_dv_get(v_row, "room")), F_dv_get(v_row, "when"), F_dv_get(v_row, "literal") };
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_action.1304", v_row)))
        {
            _ = R.push(v_prerequisites__676768, F_dv_fact(F_c2d_mark(F_dv_get(v_row, "actor"), F_dv_get(v_row, "id")), false));
        }
        object v_known__677042 = F_dv_all(v_prerequisites__676768);
        object v_speech__677081 = DvValueTemplate.Instantiate("template.inline.c2d_action.1305", v_row);
        if (R.Bool(F_dv_has_key(v_row, "request")))
        {
            v_speech__677081 = DvValueTemplate.Instantiate("template.inline.c2d_action.1306", v_speech__677081, v_row);
        }
        object v_grants__677513 = F_dv_single(F_c2d_mark(F_dv_get(v_row, "actor"), F_dv_get(v_row, "id")));
        object v_effects__677570 = F_dv_find(v_row, "effects", new List<object> {  });
        object v_e__677617 = R.number("0");
        while (R.Bool(R.lt(v_e__677617, R.count(v_effects__677570))))
        {
            _ = R.push(v_grants__677513, F_dv_get(R.at(v_effects__677570, v_e__677617), "ref"));
            v_e__677617 = R.add(v_e__677617, R.number("1"));
        }
        object v_observation_grants__677889 = F_dv_find(v_row, Authored("m50.c2daily_rows_build.literal_0118"), new List<object> {  });
        v_e__677617 = R.number("0");
        while (R.Bool(R.lt(v_e__677617, R.count(v_observation_grants__677889))))
        {
            if (R.Bool(R.not(F_dv_has(v_grants__677513, R.at(v_observation_grants__677889, v_e__677617)))))
            {
                _ = R.push(v_grants__677513, R.at(v_observation_grants__677889, v_e__677617));
            }
            v_e__677617 = R.add(v_e__677617, R.number("1"));
        }
        object v_raw_listeners__678194 = F_dv_find(v_row, Authored("m50.c2d_action.literal_0003"), F_c2d_people());
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_action.1307", v_row)))
        {
            if (R.Bool(F_dv_has_key(v_row, "opportunity")))
            {
                object v_parts__678504 = F_dv_find(F_dv_get(v_row, "opportunity"), "participants", new List<object> {  });
                if (R.Bool(R.lt(R.number("0"), R.count(v_parts__678504))))
                {
                    v_raw_listeners__678194 = F_dvp_merge_refs(v_parts__678504, AuthoredCopy("m50.c2_document_coordination_rows.literal_0015"));
                }
            }
            else
            {
                if (R.Bool(F_dv_has_key(v_row, "request")))
                {
                    v_raw_listeners__678194 = DvValueTemplate.Instantiate("template.inline.c2d_action.1308", v_row);
                }
                else
                {
                    if (R.Bool(F_dv_has_key(v_row, "response_topic")))
                    {
                        v_raw_listeners__678194 = AuthoredCopy("m50.c2_document_coordination_rows.literal_0015");
                    }
                }
            }
        }
        object v_listeners__679157 = new List<object> {  };
        object v_l__679181 = R.number("0");
        while (R.Bool(R.lt(v_l__679181, R.count(v_raw_listeners__678194))))
        {
            if (R.Bool(R.not(R.eq(R.at(v_raw_listeners__678194, v_l__679181), F_dv_get(v_row, "actor")))))
            {
                _ = R.push(v_listeners__679157, R.at(v_raw_listeners__678194, v_l__679181));
            }
            v_l__679181 = R.add(v_l__679181, R.number("1"));
        }
        object v_action__679348 = DvValueTemplate.Instantiate("template.inline.c2d_action.1309", v_row, v_known__677042, v_grants__677513, v_speech__677081, v_listeners__679157);
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_action.1310", v_row)))
        {
            v_action__679348 = F_dv_without(F_dv_without(v_action__679348, "speech"), Authored("m50.c2d_action.literal_0003"));
        }
        if (R.Bool(F_dv_has_key(v_row, "channel")))
        {
            v_action__679348 = F_dv_with_many(v_action__679348, new List<object> { F_dv_pair("speech", F_dv_with(v_speech__677081, "channel", F_dv_get(v_row, "channel"))), F_dv_pair("listeners", F_c2d_people()) });
        }
        if (R.Bool(F_dv_has_key(v_row, "norm_proposal")))
        {
            v_action__679348 = DvValueTemplate.Instantiate("template.inline.c2d_action.1311", v_action__679348, v_speech__677081, v_row);
        }
        if (R.Bool(F_dv_has_key(v_row, "personal_choice")))
        {
            v_action__679348 = F_dv_with(v_action__679348, "personal_choice", F_dv_get(v_row, "personal_choice"));
        }
        object v_metadata__680648 = AuthoredCopy("m50.c2d_action.metadata");
        object v_metadata_index__680746 = R.number("0");
        while (R.Bool(R.lt(v_metadata_index__680746, R.count(v_metadata__680648))))
        {
            if (R.Bool(F_dv_has_key(v_row, R.at(v_metadata__680648, v_metadata_index__680746))))
            {
                v_action__679348 = F_dv_with(v_action__679348, R.at(v_metadata__680648, v_metadata_index__680746), F_dv_get(v_row, R.at(v_metadata__680648, v_metadata_index__680746)));
            }
            v_metadata_index__680746 = R.add(v_metadata_index__680746, R.number("1"));
        }
        if (R.Bool(F_dv_has_key(v_row, "manipulation")))
        {
            v_action__679348 = F_dv_with(v_action__679348, "speech", F_dv_with(F_dv_get(v_action__679348, "speech"), "manipulation", F_dv_get(v_row, "manipulation")));
        }
        if (R.Bool(F_dv_has_key(v_row, "advice")))
        {
            v_action__679348 = F_dv_with(v_action__679348, "speech", F_dv_with(F_dv_get(v_action__679348, "speech"), "advice", F_dv_get(v_row, "advice")));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_action.1312", v_row, v_action__679348)))
        {
            v_action__679348 = F_campaign_authorized_speech(v_action__679348);
        }
        if (R.Bool(F_dv_has_key(v_row, "response_topic")))
        {
            v_action__679348 = F_dv_with_many(v_action__679348, new List<object> { F_dv_pair("response_topic", F_dv_get(v_row, "response_topic")), F_dv_pair("requires_actual_request", true) });
        }
        if (R.Bool(F_dv_has_key(v_row, "opportunity")))
        {
            v_action__679348 = DvValueTemplate.Instantiate("template.inline.c2d_action.1313", v_action__679348, v_row);
        }
        if (R.Bool(F_dv_has_key(v_row, "received_speech_requirement")))
        {
            v_action__679348 = F_dv_with(v_action__679348, "received_speech_requirement", F_dv_get(v_row, "received_speech_requirement"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_action.1314", v_row)))
        {
            v_action__679348 = F_dv_with_many(v_action__679348, new List<object> { F_dv_pair("case_method", true), F_dv_pair("case_base_when", v_known__677042) });
        }
        return v_action__679348;
    }
    public static object F_c2d_start_watch_law() {
        return DvValueTemplate.Instantiate("template.c2d_start_watch_law");
    }
    public static object F_c2d_small_shadow_law(object v_person) {
        return DvValueTemplate.Instantiate("template.c2d_small_shadow_law", v_person);
    }
    public static object F_c2d_watch_actions(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2d_watch_actions", v_actor);
    }
    public static object F_c2d_repair_record_law(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2d_repair_record_law", v_actor);
    }
    public static object F_c2d_request_repair_record_law() {
        return DvValueTemplate.Instantiate("template.c2d_request_repair_record_law");
    }
    public static object F_c2d_patient_take_record_law() {
        return DvValueTemplate.Instantiate("template.c2d_patient_take_record_law");
    }
    public static object F_c2d_objects() {
        object v_objects__686857 = DvValueTemplate.Instantiate("template.inline.c2d_objects.1315");
        object v_courses__687497 = F_c2d_curriculum();
        object v_i__687533 = R.number("0");
        while (R.Bool(R.lt(v_i__687533, R.count(v_courses__687497))))
        {
            v_objects__686857 = F_dvs_replace(v_objects__686857, R.number("0"), F_dv_with(R.at(v_objects__686857, R.number("0")), "facets", F_dv_with(F_dv_get(R.at(v_objects__686857, R.number("0")), "facets"), "state", F_dv_with(F_dv_get(F_dv_get(R.at(v_objects__686857, R.number("0")), "facets"), "state"), F_dv_get(R.at(v_courses__687497, v_i__687533), "id"), F_dv_field(F_dv_get(R.at(v_courses__687497, v_i__687533), "text"))))));
            v_i__687533 = R.add(v_i__687533, R.number("1"));
        }
        object v_people__687788 = F_c2d_people();
        object v_rows__687819 = F_c2d_rows();
        v_i__687533 = R.number("0");
        while (R.Bool(R.lt(v_i__687533, R.count(v_people__687788))))
        {
            object v_fields__687894 = new List<object> { F_dv_pair("repaired_latch", F_dv_field(false)), F_dv_pair("requested_latch_repair", F_dv_field(false)), F_dv_pair("patient_food_reason", F_dv_field(false)), F_dv_pair("watching", F_dv_field(false)), F_dv_pair("small_shadow_seen", F_dv_field(false)), F_dv_pair("scope_undefined", F_dv_unknown()) };
            object v_j__688135 = R.number("0");
            while (R.Bool(R.lt(v_j__688135, R.count(v_rows__687819))))
            {
                if (R.Bool(R.eq(F_dv_get(R.at(v_rows__687819, v_j__688135), "actor"), R.at(v_people__687788, v_i__687533))))
                {
                    v_fields__687894 = DvValueTemplate.Instantiate("template.inline.c2d_objects.1316", v_fields__687894, v_rows__687819, v_j__688135);
                }
                v_j__688135 = R.add(v_j__688135, R.number("1"));
            }
            object v_intros__688396 = F_c2d_introductions();
            v_j__688135 = R.number("0");
            while (R.Bool(R.lt(v_j__688135, R.count(v_intros__688396))))
            {
                if (R.Bool(R.eq(F_dv_get(R.at(v_intros__688396, v_j__688135), "actor"), R.at(v_people__687788, v_i__687533))))
                {
                    v_fields__687894 = DvValueTemplate.Instantiate("template.inline.c2d_objects.1317", v_fields__687894, v_intros__688396, v_j__688135);
                    v_fields__687894 = F_dv_with(v_fields__687894, "name", F_dv_with(F_dv_field(F_campaign_person_name(R.at(v_people__687788, v_i__687533))), "basis", new List<object> { F_dv_get(R.at(v_intros__688396, v_j__688135), "source") }));
                }
                v_j__688135 = R.add(v_j__688135, R.number("1"));
            }
            _ = R.push(v_objects__686857, F_c2_object(R.cat(Authored("m50.c2d_person_ref.literal"), R.at(v_people__687788, v_i__687533)), "", v_fields__687894));
            v_i__687533 = R.add(v_i__687533, R.number("1"));
        }
        return v_objects__686857;
    }
    public static object F_c2d_case_gate(object v_mind) {
        object v_refs__689027 = DvValueTemplate.Instantiate("template.inline.c2d_case_gate.1318");
        object v_values__690055 = new List<object> {  };
        object v_premises__690076 = new List<object> {  };
        object v_i__690099 = R.number("0");
        while (R.Bool(R.lt(v_i__690099, R.count(v_refs__689027))))
        {
            object v_cell__690149 = F_dv_read(v_mind, R.at(v_refs__689027, v_i__690099));
            if (R.Bool(R.not(F_dv_uncontested(v_cell__690149))))
            {
                return F_dv_not(F_dv_true());
            }
            _ = R.push(v_values__690055, F_dv_get(v_cell__690149, "value"));
            _ = R.push(v_premises__690076, F_dv_fact(R.at(v_refs__689027, v_i__690099), F_dv_get(v_cell__690149, "value")));
            v_i__690099 = R.add(v_i__690099, R.number("1"));
        }
        if (R.Bool((R.Bool(R.lt(R.number("0"), R.at(v_values__690055, R.number("0")))) ? (object)(R.lt(R.add(R.add(R.add(R.at(v_values__690055, R.number("2")), R.at(v_values__690055, R.number("3"))), R.at(v_values__690055, R.number("4"))), R.at(v_values__690055, R.number("5"))), R.add(R.at(v_values__690055, R.number("0")), R.at(v_values__690055, R.number("1"))))) : (object)(false))))
        {
            return F_dv_all(v_premises__690076);
        }
        return F_dv_not(F_dv_true());
    }
    public static object F_c2d_goal_head_build(object v_id,object v_tasks) {
        object v_goals__690654 = new List<object> {  };
        object v_t__690674 = R.number("0");
        while (R.Bool(R.lt(v_t__690674, R.count(v_tasks))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(v_tasks, v_t__690674), "actor"), v_id)))
            {
                _ = R.push(v_goals__690654, F_dv_with_many(F_campaign_goal(F_dv_fact(F_c2daily_ref(F_dv_get(R.at(v_tasks, v_t__690674), "field")), true), F_dv_get(R.at(v_tasks, v_t__690674), "when"), DvValueTemplate.Instantiate("template.inline.c2d_goal_head_build.1319")), new List<object> { F_dv_pair("navigation_room", F_dv_get(R.at(v_tasks, v_t__690674), "room")), F_dv_pair("navigation_methods", new List<object> { F_dv_get(R.at(v_tasks, v_t__690674), "id") }) }));
            }
            v_t__690674 = R.add(v_t__690674, R.number("1"));
        }
        _ = R.push(v_goals__690654, F_dv_with_many(F_campaign_observation_goal(F_c2daily_ref(Authored("m50.c2_personal_goal_templates_build.literal_0026")), F_campaign_window(Authored("m50.c2_any_night.literal"), Authored("m50.c2daily_rows_build.literal_0032"), Authored("m50.c2daily_notes_window.literal")), DvValueTemplate.Instantiate("template.inline.c2d_goal_head_build.1320"), Authored("m50.c2d_goal_head_build.literal_0003")), DvValueTemplate.Instantiate("template.inline.c2d_goal_head_build.1321", v_id)));
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goal_head_build.1322", v_id)))
        {
            _ = R.push(v_goals__690654, F_dv_with_many(F_campaign_observation_goal(F_c2daily_ref(Authored("m50.c2_norm_available.literal_0002")), F_c2daily_notes_window(), DvValueTemplate.Instantiate("template.inline.c2d_goal_head_build.1323"), Authored("m50.c2_norm_condition.literal_0005")), AuthoredCopy("m50.c2d_goal_head_build.literal_0006")));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goal_head_build.1324", v_id)))
        {
            object v_raw_window__692450 = DvValueTemplate.Instantiate("template.inline.c2d_goal_head_build.1325");
            _ = R.push(v_goals__690654, F_dv_with_many(F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_world_objects.literal_0007"), Authored("m50.c2_route_inspections.literal_0003")), v_raw_window__692450, DvValueTemplate.Instantiate("template.inline.c2d_goal_head_build.1326"), R.number("0")), AuthoredCopy("m50.c2d_goal_head_build.literal_0008")));
            _ = R.push(v_goals__690654, F_dv_with_many(F_campaign_goal(F_dv_at_least(F_campaign_own(Authored("m50.c2_norms_build.literal_0047"), Authored("m50.c2_self_presentation.literal_0008")), Authored("m50.c2daily_inherited_meal_cell.literal_0002")), v_raw_window__692450, DvValueTemplate.Instantiate("template.inline.c2d_goal_head_build.1327")), AuthoredCopy("m50.c2d_goal_head_build.literal_0010")));
            _ = R.push(v_goals__690654, F_dv_with_many(F_campaign_goal(F_dv_fact(F_c2daily_ref(Authored("m50.c2daily_inherited_meal_cell.literal")), true), DvValueTemplate.Instantiate("template.inline.c2d_goal_head_build.1328"), DvValueTemplate.Instantiate("template.inline.c2d_goal_head_build.1329")), AuthoredCopy("m50.c2d_goal_head_build.literal_0012")));
            _ = R.push(v_goals__690654, F_dv_with_many(F_campaign_goal(F_dv_fact(F_c2daily_ref(Authored("m50.c2_personal_goal_templates_build.literal_0026")), true), DvValueTemplate.Instantiate("template.inline.c2d_goal_head_build.1330"), DvValueTemplate.Instantiate("template.inline.c2d_goal_head_build.1331")), AuthoredCopy("m50.c2d_goal_head_build.literal_0014")));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goal_head_build.1332", v_id)))
        {
            _ = R.push(v_goals__690654, F_dv_with_many(F_campaign_goal(F_dv_fact(F_c2daily_ref(Authored("m50.c2_norm_available.literal_0002")), true), F_c2daily_notes_window(), DvValueTemplate.Instantiate("template.inline.c2d_goal_head_build.1333")), AuthoredCopy("m50.c2d_goal_head_build.literal_0016")));
        }
        return v_goals__690654;
    }
    public static object F_c2d_goal_head(object v_id,object v_tasks) {
        object v_key__695844 = DvValueTemplate.Instantiate("template.inline.c2d_goal_head.1334", v_id);
        object v_cached__695930 = R.memo_get(v_tasks, v_key__695844);
        if (R.Bool(R.at(v_cached__695930, R.number("0"))))
        {
            return R.at(v_cached__695930, R.number("1"));
        }
        return R.memo_put(v_tasks, v_key__695844, F_c2d_goal_head_build(v_id, v_tasks));
    }
    public static object F_c2d_goal_tail_build(object v_id) {
        object v_goals__696116 = new List<object> {  };
        object v_i__696136 = R.number("0");
        object v_study__696151 = DvValueTemplate.Instantiate("template.inline.c2d_goal_tail_build.1335");
        _ = R.push(v_goals__696116, F_dv_with_many(F_campaign_goal(F_campaign_at(v_id, Authored("m50.c2_norms_build.literal_0057")), v_study__696151, DvValueTemplate.Instantiate("template.inline.c2d_goal_tail_build.1336")), AuthoredCopy("m50.c2d_goal_tail_build.literal_0003")));
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goal_tail_build.1337", v_id)))
        {
            _ = R.push(v_goals__696116, F_dv_with_many(F_campaign_goal(F_dv_fact(F_c2d_person_ref(v_id, Authored("m50.c2d_action.literal_0007")), true), F_c2_any_night(), DvValueTemplate.Instantiate("template.inline.c2d_goal_tail_build.1338")), AuthoredCopy("m50.c2d_goal_tail_build.literal_0006")));
            _ = R.push(v_goals__696116, F_dv_with(F_campaign_observation_goal(F_c2d_person_ref(v_id, Authored("m50.c2d_rows_build.literal_0083")), DvValueTemplate.Instantiate("template.inline.c2d_goal_tail_build.1339", v_id), DvValueTemplate.Instantiate("template.inline.c2d_goal_tail_build.1340"), R.number("0")), Authored("m50.c2_navigation_arrival_goals.literal"), Authored("m50.c2_world_objects.literal_0013")));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goal_tail_build.1341", v_id)))
        {
            object v_ledger_fields__698376 = AuthoredCopy("m50.c2d_goal_tail_build.ledger_fields");
            _ = R.push(v_goals__696116, F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_world_objects.literal_0007"), Authored("m50.c2_route_inspections.literal_0003")), F_campaign_at(v_id, Authored("m50.c2_norms_build.literal_0034")), DvValueTemplate.Instantiate("template.inline.c2d_goal_tail_build.1342"), R.number("0")));
            v_i__696136 = R.number("0");
            while (R.Bool(R.lt(v_i__696136, R.count(v_ledger_fields__698376))))
            {
                _ = R.push(v_goals__696116, F_campaign_observation_goal(F_campaign_ref(Authored("m50.c2_world_objects.literal_0009"), R.at(v_ledger_fields__698376, v_i__696136)), F_campaign_at(v_id, Authored("m50.c2_norms_build.literal_0034")), DvValueTemplate.Instantiate("template.inline.c2d_goal_tail_build.1343"), R.number("0")));
                v_i__696136 = R.add(v_i__696136, R.number("1"));
            }
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goal_tail_build.1344", v_id)))
        {
            _ = R.push(v_goals__696116, F_dv_with_many(F_campaign_goal(F_dv_fact(F_campaign_ref(Authored("m50.c2_world_objects.literal_0006"), Authored("m50.c2_total_store_open_known.literal_0022")), false), DvValueTemplate.Instantiate("template.inline.c2d_goal_tail_build.1345", v_id), DvValueTemplate.Instantiate("template.inline.c2d_goal_tail_build.1346")), AuthoredCopy("m50.c2d_goal_tail_build.literal_0010")));
        }
        object v_opportunity__700509 = F_c2d_known_council_proposal();
        object v_venues__700561 = AuthoredCopy("m50.c2d_goal_tail_build.venues");
        v_i__696136 = R.number("0");
        while (R.Bool(R.lt(v_i__696136, R.count(v_venues__700561))))
        {
            object v_venue__700712 = R.at(v_venues__700561, v_i__696136);
            object v_when__700743 = DvValueTemplate.Instantiate("template.inline.c2d_goal_tail_build.1347", v_id, v_opportunity__700509, v_venue__700712);
            _ = R.push(v_goals__696116, F_dv_with(F_campaign_goal(F_campaign_at(v_id, F_dv_get(v_venue__700712, "room")), v_when__700743, DvValueTemplate.Instantiate("template.inline.c2d_goal_tail_build.1348")), Authored("m50.c2_navigation_arrival_goals.literal"), F_dv_get(v_venue__700712, "room")));
            object v_tally__701584 = AuthoredCopy("m50.c2d_goal_tail_build.tally");
            object v_n__701689 = R.number("0");
            while (R.Bool(R.lt(v_n__701689, R.count(v_tally__701584))))
            {
                _ = R.push(v_goals__696116, F_dv_with(F_campaign_observation_goal(F_c2daily_ref(R.at(v_tally__701584, v_n__701689)), v_when__700743, DvValueTemplate.Instantiate("template.inline.c2d_goal_tail_build.1349"), Authored("m50.c2d_goal_tail_build.literal_0014")), Authored("m50.c2_navigation_arrival_goals.literal"), F_dv_get(v_venue__700712, "room")));
                v_n__701689 = R.add(v_n__701689, R.number("1"));
            }
            v_i__696136 = R.add(v_i__696136, R.number("1"));
        }
        return v_goals__696116;
    }
    public static object F_c2d_goal_tail(object v_id) {
        object v_key__702185 = DvValueTemplate.Instantiate("template.inline.c2d_goal_tail.1350", v_id);
        object v_cached__702271 = R.memo_get(new List<object> {  }, v_key__702185);
        if (R.Bool(R.at(v_cached__702271, R.number("0"))))
        {
            return R.at(v_cached__702271, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__702185, F_c2d_goal_tail_build(v_id));
    }
    public static object F_c2d_row_goal(object v_row,object v_id,object v_when) {
        object v_key__702601 = DvValueTemplate.Instantiate("template.inline.c2d_row_goal.1351", v_id, v_when);
        object v_cached__702692 = R.memo_get(v_row, v_key__702601);
        if (R.Bool(R.at(v_cached__702692, R.number("0"))))
        {
            return R.at(v_cached__702692, R.number("1"));
        }
        return R.memo_put(v_row, v_key__702601, F_campaign_index_goal_window(DvValueTemplate.Instantiate("template.inline.c2d_row_goal.1352", v_row, v_when)));
    }
    public static object F_c2d_trial_evidence_goals(object v_actor) {
        object v_goals__703319 = new List<object> {  };
        object v_patients__703339 = new List<object> {  };
        if (R.Bool((R.Bool(R.eq(F_dv_get(v_actor, "id"), Authored("m50.c2_norms_build.literal_0033"))) ? (object)(F_c2_received_requirement(v_actor, AuthoredCopy("m50.c2d_demon_trial_dialogues.literal_0014"))) : (object)(false))))
        {
            v_patients__703339 = AuthoredCopy("m50.c2_cooperation_rows.literal_0063");
        }
        if (R.Bool((R.Bool(R.eq(F_dv_get(v_actor, "id"), Authored("m50.c2_alive.literal"))) ? (object)(F_c2_received_requirement(v_actor, AuthoredCopy("m50.c2_cooperation_rows.literal_0073"))) : (object)(false))))
        {
            v_patients__703339 = AuthoredCopy("m50.c2d_trial_evidence_goals.literal");
        }
        object v_source__704017 = F_campaign_source(Authored("m50.c2_foolish_duty_actions.literal"), R.number("0"), Authored("m50.c2d_trial_evidence_goals.literal_0002"));
        object v_i__704212 = R.number("0");
        while (R.Bool(R.lt(v_i__704212, R.count(v_patients__703339))))
        {
            object v_patient__704266 = R.at(v_patients__703339, v_i__704212);
            object v_field__704301 = Authored("m50.c2_norm_actions.literal_0029");
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_trial_evidence_goals.1353", v_patient__704266)))
            {
                v_field__704301 = Authored("m50.c2_can_act.literal_0002");
            }
            object v_goal__704579 = F_campaign_observation_goal(F_campaign_own(v_patient__704266, v_field__704301), F_dv_at_least(F_campaign_clock(), Authored("m50.c2_moran_fatal_settle_law.literal")), new List<object> { v_source__704017 }, Authored("m50.c2_moran_fatal_settle_law.literal"));
            object v_place__704852 = F_dv_read(F_dv_get(v_actor, "mind"), F_campaign_position_ref(v_patient__704266));
            if (R.Bool((R.Bool(F_dv_uncontested(v_place__704852)) ? (object)(F_dv_has(F_c2_rooms(), F_dv_get(v_place__704852, "value"))) : (object)(false))))
            {
                v_goal__704579 = DvValueTemplate.Instantiate("template.inline.c2d_trial_evidence_goals.1354", v_goal__704579, v_place__704852, v_actor, v_patient__704266);
            }
            _ = R.push(v_goals__703319, v_goal__704579);
            v_i__704212 = R.add(v_i__704212, R.number("1"));
        }
        return v_goals__703319;
    }
    public static object F_c2d_goals(object v_actor) {
        v_actor = F_c2_query_view(v_actor, v_actor);
        object v_goals__705333 = new List<object> {  };
        object v_rows__705353 = F_c2d_rows();
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goals.1355", v_actor)))
        {
            object v_allowed__705499 = F_dv_find(F_dv_get(v_actor, "policy"), Authored("m50.c2d_goals.literal"), false);
            object v_key__705613 = DvValueTemplate.Instantiate("template.inline.c2d_goals.1356", v_actor, v_allowed__705499);
            object v_cached__705719 = R.memo_get(v_rows__705353, v_key__705613);
            if (R.Bool(R.at(v_cached__705719, R.number("0"))))
            {
                v_rows__705353 = R.at(v_cached__705719, R.number("1"));
            }
            else
            {
                object v_selected__705836 = new List<object> {  };
                object v_row_index__705867 = R.number("0");
                while (R.Bool(R.lt(v_row_index__705867, R.count(v_rows__705353))))
                {
                    object v_row__705949 = R.at(v_rows__705353, v_row_index__705867);
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goals.1357", v_row__705949, v_actor, v_allowed__705499)))
                    {
                        _ = R.push(v_selected__705836, v_row__705949);
                    }
                    v_row_index__705867 = R.add(v_row_index__705867, R.number("1"));
                }
                v_rows__705353 = R.memo_put(v_rows__705353, v_key__705613, v_selected__705836);
            }
        }
        v_rows__705353 = F_c2_query_view(v_actor, v_rows__705353);
        object v_tasks__706348 = F_c2daily_tasks();
        v_goals__705333 = F_dv_clone(F_c2d_goal_head(F_dv_get(v_actor, "id"), v_tasks__706348));
        object v_concerned_ready__706599 = false;
        object v_concerned__706632 = false;
        object v_notice_ready__706659 = false;
        object v_notice__706689 = false;
        object v_case_ready__706713 = false;
        object v_case_gate__706741 = false;
        object v_i__706768 = R.number("0");
        bool storyflowEnabled = R.Bool(F_c2sf_enabled(v_actor));
        object? ceremonyIds = null;
        while (R.Bool(R.lt(v_i__706768, R.count(v_rows__705353))))
        {
            object v_row__706818 = R.at(v_rows__705353, v_i__706768);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goals.1358", v_row__706818, v_actor)))
            {
                object v_when__707113 = F_dv_all(new List<object> { F_dv_get(v_row__706818, "when"), F_dv_get(v_row__706818, "literal") });
                if (storyflowEnabled && R.Bool(F_dv_has_key(ceremonyIds ??= ReadC2StoryflowCeremonyIds(), F_dv_get(v_row__706818, "id"))))
                {
                    v_when__707113 = F_dv_not(F_dv_true());
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goals.1359", v_row__706818)))
                {
                    v_when__707113 = F_dv_all(new List<object> { v_when__707113, F_dv_fact(F_c2d_mark(F_dv_get(v_actor, "id"), F_dv_get(v_row__706818, "id")), false) });
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goals.1360", v_row__706818, v_actor)))
                {
                    v_when__707113 = F_dv_not(F_dv_true());
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goals.1361", v_row__706818, v_actor)))
                {
                    v_when__707113 = F_dv_not(F_dv_true());
                }
                if (R.Bool(F_dv_has_key(v_row__706818, "owned_decision")))
                {
                    object v_candidate__707911 = F_dvp_find_action(F_dv_get(v_actor, "actions"), F_dv_get(v_row__706818, "id"));
                    if (R.Bool((R.Bool(R.eq(v_candidate__707911, false)) ? (object)(true) : (object)(R.not(F_dvp_owned_decision_ready(v_actor, v_candidate__707911, F_dv_get(v_actor, "mind")))))))
                    {
                        v_when__707113 = F_dv_not(F_dv_true());
                    }
                }
                if (R.Bool(F_dv_has_key(v_row__706818, "partner_decision")))
                {
                    object v_candidate__708184 = F_dvp_find_action(F_dv_get(v_actor, "actions"), F_dv_get(v_row__706818, "id"));
                    if (R.Bool((R.Bool(R.eq(v_candidate__708184, false)) ? (object)(true) : (object)(R.not(F_dvp_personal_ready(F_dv_get(v_actor, "mind"), F_dv_get(v_candidate__708184, "personal_choice")))))))
                    {
                        v_when__707113 = F_dv_not(F_dv_true());
                    }
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goals.1362", v_row__706818)))
                {
                    if (R.Bool(R.not(v_concerned_ready__706599)))
                    {
                        v_concerned__706632 = F_c2_quer_concerned(v_actor);
                        v_concerned_ready__706599 = true;
                    }
                    if (R.Bool(R.not(v_concerned__706632)))
                    {
                        v_when__707113 = F_dv_not(F_dv_true());
                    }
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goals.1363", v_row__706818)))
                {
                    if (R.Bool(R.not(v_notice_ready__706659)))
                    {
                        v_notice__706689 = F_c2_owned_safety_notice(v_actor);
                        v_notice_ready__706659 = true;
                    }
                    if (R.Bool(R.not(v_notice__706689)))
                    {
                        v_when__707113 = F_dv_not(F_dv_true());
                    }
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goals.1364", v_row__706818)))
                {
                    if (R.Bool(R.not(v_case_ready__706713)))
                    {
                        v_case_gate__706741 = F_c2d_case_gate(F_dv_get(v_actor, "mind"));
                        v_case_ready__706713 = true;
                    }
                    v_when__707113 = F_dv_all(new List<object> { v_when__707113, v_case_gate__706741 });
                }
                if (R.Bool(F_dv_has_key(v_row__706818, "received_speech_requirement")))
                {
                    if (R.Bool(R.not(F_c2_received_requirement(v_actor, F_dv_get(v_row__706818, "received_speech_requirement")))))
                    {
                        v_when__707113 = F_dv_not(F_dv_true());
                    }
                }
                if (R.Bool(F_dv_has_key(v_row__706818, "completed_action_requirement")))
                {
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goals.1365", v_actor, v_row__706818)))
                    {
                        v_when__707113 = F_dv_not(F_dv_true());
                    }
                }
                if (R.Bool(F_dv_has_key(v_row__706818, "response_topic")))
                {
                    if (R.Bool(R.eq(R.count(F_dvs_pending_request(v_actor, v_row__706818)), R.number("0"))))
                    {
                        v_when__707113 = F_dv_not(F_dv_true());
                    }
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goals.1366", v_row__706818)))
                {
                    object v_candidate__710397 = F_dvp_find_action(F_dv_get(v_actor, "actions"), F_dv_get(v_row__706818, "id"));
                    if (R.Bool((R.Bool(R.eq(v_candidate__710397, false)) ? (object)(true) : (object)(R.not(F_dvs_advice_decision_ready(v_actor, v_candidate__710397, F_dv_get(v_actor, "mind")))))))
                    {
                        v_when__707113 = F_dv_not(F_dv_true());
                    }
                }
                if (R.Bool(F_dv_has_key(v_row__706818, "opportunity")))
                {
                    object v_opp__710666 = F_dv_get(v_row__706818, "opportunity");
                    object v_opp_present__710709 = true;
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goals.1367", v_opp__710666)))
                    {
                        object v_p__710872 = R.number("0");
                        while (R.Bool(R.lt(v_p__710872, R.count(F_dv_get(v_opp__710666, "participants")))))
                        {
                            if (R.Bool((R.Bool(R.not(R.eq(R.at(F_dv_get(v_opp__710666, "participants"), v_p__710872), F_dv_get(v_actor, "id")))) ? (object)(R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_campaign_at(R.at(F_dv_get(v_opp__710666, "participants"), v_p__710872), F_dv_get(v_opp__710666, "location"))), "true"))) : (object)(false))))
                            {
                                v_opp_present__710709 = false;
                            }
                            v_p__710872 = R.add(v_p__710872, R.number("1"));
                        }
                    }
                    else
                    {
                        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goals.1368", v_opp__710666)))
                        {
                            object v_p__711325 = R.number("0");
                            object v_any_present__711360 = false;
                            while (R.Bool(R.lt(v_p__711325, R.count(F_dv_get(v_opp__710666, "participants")))))
                            {
                                if (R.Bool((R.Bool(R.eq(R.at(F_dv_get(v_opp__710666, "participants"), v_p__711325), F_dv_get(v_actor, "id"))) ? (object)(true) : (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_campaign_at(R.at(F_dv_get(v_opp__710666, "participants"), v_p__711325), F_dv_get(v_opp__710666, "location"))), "true")))))
                                {
                                    v_any_present__711360 = true;
                                }
                                v_p__711325 = R.add(v_p__711325, R.number("1"));
                            }
                            if (R.Bool(R.not(v_any_present__711360)))
                            {
                                v_opp_present__710709 = false;
                            }
                        }
                        else
                        {
                            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_goals.1369", v_opp__710666)))
                            {
                                object v_p__711944 = R.number("0");
                                while (R.Bool(R.lt(v_p__711944, R.count(F_dv_get(v_opp__710666, "participants")))))
                                {
                                    if (R.Bool((R.Bool(R.not(R.eq(R.at(F_dv_get(v_opp__710666, "participants"), v_p__711944), F_dv_get(v_actor, "id")))) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_campaign_at(R.at(F_dv_get(v_opp__710666, "participants"), v_p__711944), F_dv_get(v_opp__710666, "location"))), "true")) : (object)(false))))
                                    {
                                        v_opp_present__710709 = false;
                                    }
                                    v_p__711944 = R.add(v_p__711944, R.number("1"));
                                }
                            }
                        }
                    }
                    if (R.Bool(R.not(v_opp_present__710709)))
                    {
                        v_when__707113 = F_dv_not(F_dv_true());
                    }
                }
                _ = R.push(v_goals__705333, F_c2d_row_goal(v_row__706818, F_dv_get(v_actor, "id"), v_when__707113));
            }
            v_i__706768 = R.add(v_i__706768, R.number("1"));
        }
        object v_tail__712517 = F_dv_clone(F_c2d_goal_tail(F_dv_get(v_actor, "id")));
        v_i__706768 = R.number("0");
        while (R.Bool(R.lt(v_i__706768, R.count(v_tail__712517))))
        {
            _ = R.push(v_goals__705333, R.at(v_tail__712517, v_i__706768));
            v_i__706768 = R.add(v_i__706768, R.number("1"));
        }
        object v_evidence_goals__712661 = F_c2d_trial_evidence_goals(v_actor);
        v_i__706768 = R.number("0");
        while (R.Bool(R.lt(v_i__706768, R.count(v_evidence_goals__712661))))
        {
            _ = R.push(v_goals__705333, R.at(v_evidence_goals__712661, v_i__706768));
            v_i__706768 = R.add(v_i__706768, R.number("1"));
        }
        return F_c2m_concat(v_goals__705333, F_c2u_goals(v_actor));
    }
    public static object F_c2d_refresh_actor_goals(object v_actor) {
        object v_view__712919 = F_c2d_refresh_actor_base_goals(v_actor);
        object navigationGoals = F_c2_navigation_goals(v_view__712919, F_dv_get(F_dv_get(v_view__712919, "policy"), "goals"), out object navigationDiagnostic);
        return F_campaign_request_withdrawals(F_campaign_navigation_frontier(F_dv_with_many(v_view__712919, new List<object> { F_dv_pair("policy", F_dv_with(F_dv_get(v_view__712919, "policy"), "goals", navigationGoals)), F_dv_pair("navigation_route_diagnostic", navigationDiagnostic) })));
    }
    public static object F_c2d_refresh_actor_base_goals(object v_actor) {
        object refreshed;
        R.phase("c2-goals-actions-start", "");
        try { refreshed = F_c2_refresh_content_actions(F_c2u_owned_consequences(v_actor)); }
        finally { R.phase("c2-goals-actions-end", ""); }
        R.phase("c2-goals-base-start", "");
        try { return F_c2d_refresh_goals_from(refreshed); }
        finally { R.phase("c2-goals-base-end", ""); }
    }
    public static object F_c2d_refresh_goals_from(object v_actor) {
        v_actor = F_c2g_refresh_inventory_review(v_actor);
        v_actor = DvValueTemplate.Instantiate("template.c2_coordination_observation_context", v_actor);
        v_actor = F_c2_query_view(v_actor, v_actor);
        object v_goals__713506 = F_c2d_goals(v_actor);
        object v_policy__713540 = DvValueTemplate.Instantiate("template.inline.c2d_refresh_goals_from.1370", v_actor, v_goals__713506);
        object v_combined__713662 = F_c2_personal_goals(v_actor);
        object v_i__713707 = R.number("0");
        object v_retired__713800 = F_dv_find(v_policy__713540, Authored("m50.c2_extend_actor_from.literal_0007"), false);
        object v_arrived__713920 = (R.Bool(R.not(R.eq(F_dv_get(v_actor, "id"), Authored("m50.c2_alive.literal")))) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_campaign_at(F_dv_get(v_actor, "id"), Authored("m50.c2_norms_build.literal_0048"))), "true")) : (object)(false));
        if (R.Bool((R.Bool(v_retired__713800) ? (object)(true) : (object)(v_arrived__713920))))
        {
            v_policy__713540 = DvValueTemplate.Instantiate("template.inline.c2d_refresh_goals_from.1371", v_policy__713540);
        }
        object v_carried__714282 = F_dv_find(v_policy__713540, Authored("m50.c2_refresh_actor_goals.literal"), new List<object> {  });
        if (R.Bool((R.Bool(R.not(v_retired__713800)) ? (object)(R.not(v_arrived__713920)) : (object)(false))))
        {
            while (R.Bool(R.lt(v_i__713707, R.count(v_carried__714282))))
            {
                _ = R.push(v_combined__713662, R.at(v_carried__714282, v_i__713707));
                v_i__713707 = R.add(v_i__713707, R.number("1"));
            }
        }
        v_i__713707 = R.number("0");
        while (R.Bool(R.lt(v_i__713707, R.count(v_goals__713506))))
        {
            _ = R.push(v_combined__713662, R.at(v_goals__713506, v_i__713707));
            v_i__713707 = R.add(v_i__713707, R.number("1"));
        }
        object v_original_actions__714636 = F_dv_get(v_actor, "actions");
        object v_actions__714678 = new List<object> {  };
        object v_case_ready__714700 = false;
        object v_case_gate__714728 = false;
        v_i__713707 = R.number("0");
        while (R.Bool(R.lt(v_i__713707, R.count(v_original_actions__714636))))
        {
            object v_action__714813 = R.at(v_original_actions__714636, v_i__713707);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_refresh_goals_from.1372", v_action__714813)))
            {
                object v_base_when__714975 = F_dv_get(v_action__714813, "case_base_when");
                if (R.Bool(R.not(v_case_ready__714700)))
                {
                    v_case_gate__714728 = F_c2d_case_gate(F_dv_get(v_actor, "mind"));
                    v_case_ready__714700 = true;
                }
                v_action__714813 = F_dv_with(v_action__714813, "precondition", F_dv_all(new List<object> { v_base_when__714975, v_case_gate__714728 }));
            }
            _ = R.push(v_actions__714678, v_action__714813);
            v_i__713707 = R.add(v_i__713707, R.number("1"));
        }
        return F_dv_with_many(v_actor, new List<object> { F_dv_pair("policy", F_dv_with(v_policy__713540, "goals", v_combined__713662)), F_dv_pair("actions", v_actions__714678) });
    }
    public static object F_c2d_install_scene(object v_scene) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_install_scene.1373", v_scene)))
        {
            return v_scene;
        }
        object v_physical__715534 = F_dv_get(v_scene, "physical");
        object v_bindings__715569 = F_dv_clone(F_dv_get(v_scene, "bindings"));
        object v_objects__715614 = F_c2d_objects();
        object v_i__715647 = R.number("0");
        while (R.Bool(R.lt(v_i__715647, R.count(v_objects__715614))))
        {
            v_physical__715534 = F_campaign_add_new(v_physical__715534, R.at(v_objects__715614, v_i__715647));
            v_i__715647 = R.add(v_i__715647, R.number("1"));
        }
        object v_rows__715774 = F_c2d_rows();
        v_i__715647 = R.number("0");
        while (R.Bool(R.lt(v_i__715647, R.count(v_rows__715774))))
        {
            v_physical__715534 = F_dv_adopt(v_physical__715534, F_campaign_method(F_dv_get(R.at(v_rows__715774, v_i__715647), "id"), F_c2d_row_law(R.at(v_rows__715774, v_i__715647))), "enforce", R.number("1"), F_dv_get(R.at(v_rows__715774, v_i__715647), "source"));
            v_i__715647 = R.add(v_i__715647, R.number("1"));
        }
        v_physical__715534 = F_dv_adopt(v_physical__715534, F_campaign_method(Authored("m50.c2d_watch_actions.literal_0002"), F_c2d_start_watch_law()), "enforce", R.number("1"), F_c2d_source(Authored("m50.c2_special_actions.literal_0005"), Authored("m50.c2d_introductions_build.literal_0002"), Authored("m50.c2d_install_scene.literal_0002")));
        object v_processes__716390 = F_dv_clone(F_dv_find(v_scene, Authored("m50.c2_extend_scene.literal_0002"), new List<object> {  }));
        object v_small_people__716513 = AuthoredCopy("m50.c2d_install_scene.small_people");
        v_i__715647 = R.number("0");
        while (R.Bool(R.lt(v_i__715647, R.count(v_small_people__716513))))
        {
            object v_id__716680 = R.cat(Authored("m50.c2d_install_scene.literal_0003"), R.at(v_small_people__716513, v_i__715647));
            object v_source__716797 = F_c2d_source(Authored("m50.c2_special_actions.literal_0005"), Authored("m50.c2d_introductions_build.literal_0002"), Authored("m50.c2d_install_scene.literal_0004"));
            v_physical__715534 = F_dv_adopt(v_physical__715534, F_campaign_method(v_id__716680, F_c2d_small_shadow_law(R.at(v_small_people__716513, v_i__715647))), "enforce", R.number("1"), v_source__716797);
            _ = R.push(v_processes__716390, new List<object> { F_dv_pair("id", v_id__716680), F_dv_pair("law", v_id__716680), F_dv_pair("basis", new List<object> { v_source__716797 }) });
            v_i__715647 = R.add(v_i__715647, R.number("1"));
        }
        object v_repair_source__717268 = F_c2d_source(Authored("m50.c2_supply_actions.literal_0005"), Authored("m50.c2_special_actions.literal_0017"), Authored("m50.c2d_install_scene.literal_0005"));
        v_physical__715534 = F_dv_adopt(v_physical__715534, F_campaign_method(Authored("m50.c2d_install_scene.literal_0006"), F_c2d_repair_record_law(Authored("m50.c2_supply_actions.literal_0027"))), "enforce", R.number("1"), v_repair_source__717268);
        v_physical__715534 = F_dv_adopt(v_physical__715534, F_campaign_method(Authored("m50.c2d_install_scene.literal_0007"), F_c2d_repair_record_law(Authored("m50.c2_norms_build.literal_0082"))), "enforce", R.number("1"), v_repair_source__717268);
        v_physical__715534 = F_dv_adopt(v_physical__715534, F_campaign_method(Authored("m50.c2d_install_scene.literal_0008"), F_c2d_request_repair_record_law()), "enforce", R.number("1"), F_c2d_source(Authored("m50.c2_special_actions.literal_0005"), Authored("m50.c2d_curriculum.literal_0009"), Authored("m50.c2d_install_scene.literal_0009")));
        v_physical__715534 = F_dv_adopt(v_physical__715534, F_campaign_method(Authored("m50.c2d_install_scene.literal_0010"), F_c2d_patient_take_record_law()), "enforce", R.number("1"), F_c2d_source(Authored("m50.c2_supply_actions.literal_0005"), Authored("m50.c2_special_actions.literal_0027"), Authored("m50.c2d_install_scene.literal_0011")));
        object v_actors__718854 = new List<object> {  };
        v_i__715647 = R.number("0");
        while (R.Bool(R.lt(v_i__715647, R.count(F_dv_get(v_scene, "actors")))))
        {
            object v_actor__718929 = R.at(F_dv_get(v_scene, "actors"), v_i__715647);
            object v_mind__718966 = F_dv_get(v_actor__718929, "mind");
            object v_j__718997 = R.number("0");
            while (R.Bool(R.lt(v_j__718997, R.count(v_objects__715614))))
            {
                v_mind__718966 = F_campaign_add_new(v_mind__718966, F_campaign_local_object(F_dv_get(R.at(v_objects__715614, v_j__718997), "id")));
                if (R.Bool(R.eq(F_dv_local(v_bindings__715569, F_dv_get(R.at(v_objects__715614, v_j__718997), "id"), F_dv_get(v_actor__718929, "id")), "")))
                {
                    _ = R.push(v_bindings__715569, F_dv_binding(F_dv_get(R.at(v_objects__715614, v_j__718997), "id"), F_dv_get(R.at(v_objects__715614, v_j__718997), "id"), F_dv_get(v_actor__718929, "id")));
                }
                v_j__718997 = R.add(v_j__718997, R.number("1"));
            }
            object v_own_fields__719417 = F_dv_get(F_dv_get(F_dv_find(v_physical__715534, "objects", new List<object> {  }), R.cat(Authored("m50.c2d_person_ref.literal"), F_dv_get(v_actor__718929, "id"))), "facets");
            object v_self_fields__719587 = F_dv_get(v_own_fields__719417, "state");
            v_j__718997 = R.number("0");
            while (R.Bool(R.lt(v_j__718997, R.count(v_self_fields__719587))))
            {
                object v_field__719692 = R.at(v_self_fields__719587, v_j__718997);
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_install_scene.1374", v_field__719692)))
                {
                    if (R.Bool(R.not(F_dv_get(F_dv_read(v_mind__718966, F_c2d_person_ref(F_dv_get(v_actor__718929, "id"), R.at(v_field__719692, R.number("0")))), "known"))))
                    {
                        v_mind__718966 = F_dv_store(v_mind__718966, F_c2d_person_ref(F_dv_get(v_actor__718929, "id"), R.at(v_field__719692, R.number("0"))), R.at(v_field__719692, R.number("1")));
                    }
                }
                v_j__718997 = R.add(v_j__718997, R.number("1"));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_install_scene.1375", v_actor__718929)))
            {
                object v_curriculum_fields__720193 = F_dv_get(F_dv_get(R.at(v_objects__715614, R.number("0")), "facets"), "state");
                v_j__718997 = R.number("0");
                while (R.Bool(R.lt(v_j__718997, R.count(v_curriculum_fields__720193))))
                {
                    v_mind__718966 = F_dv_store(v_mind__718966, F_c2d_ref(R.at(R.at(v_curriculum_fields__720193, v_j__718997), R.number("0"))), R.at(R.at(v_curriculum_fields__720193, v_j__718997), R.number("1")));
                    v_j__718997 = R.add(v_j__718997, R.number("1"));
                }
            }
            object v_actions__720446 = F_dv_clone(F_dv_get(v_actor__718929, "actions"));
            v_j__718997 = R.number("0");
            while (R.Bool(R.lt(v_j__718997, R.count(v_rows__715774))))
            {
                if (R.Bool(R.eq(F_dv_get(R.at(v_rows__715774, v_j__718997), "actor"), F_dv_get(v_actor__718929, "id"))))
                {
                    _ = R.push(v_actions__720446, F_c2d_action(R.at(v_rows__715774, v_j__718997)));
                    v_mind__718966 = F_dv_adopt(v_mind__718966, F_campaign_method(F_dv_get(R.at(v_rows__715774, v_j__718997), "id"), F_c2d_row_law(R.at(v_rows__715774, v_j__718997))), "predict", R.number("1"), F_dv_get(R.at(v_rows__715774, v_j__718997), "source"));
                }
                v_j__718997 = R.add(v_j__718997, R.number("1"));
            }
            object v_watch__720784 = F_c2d_watch_actions(F_dv_get(v_actor__718929, "id"));
            v_j__718997 = R.number("0");
            while (R.Bool(R.lt(v_j__718997, R.count(v_watch__720784))))
            {
                _ = R.push(v_actions__720446, R.at(v_watch__720784, v_j__718997));
                v_j__718997 = R.add(v_j__718997, R.number("1"));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_install_scene.1376", v_actor__718929)))
            {
                object v_examinations__721036 = F_c2_casualty_actions(Authored("m50.c2_alive.literal"));
                v_j__718997 = R.number("0");
                while (R.Bool(R.lt(v_j__718997, R.count(v_examinations__721036))))
                {
                    _ = R.push(v_actions__720446, R.at(v_examinations__721036, v_j__718997));
                    v_j__718997 = R.add(v_j__718997, R.number("1"));
                }
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_install_scene.1377", v_actor__718929)))
            {
                v_mind__718966 = F_dv_adopt(v_mind__718966, F_campaign_method(Authored("m50.c2d_watch_actions.literal_0002"), F_c2d_start_watch_law()), "predict", R.number("1"), F_c2d_source(Authored("m50.c2_special_actions.literal_0005"), Authored("m50.c2d_introductions_build.literal_0002"), Authored("m50.c2d_install_scene.literal_0012")));
            }
            v_j__718997 = R.number("0");
            while (R.Bool(R.lt(v_j__718997, R.count(v_actions__720446))))
            {
                object v_action__721852 = R.at(v_actions__720446, v_j__718997);
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_install_scene.1378", v_actor__718929)))
                {
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_install_scene.1379", v_action__721852, v_actor__718929)))
                    {
                        object v_law__722213 = R.cat(F_dv_get(v_actor__718929, "id"), Authored("m50.c2d_install_scene.literal_0013"));
                        v_action__721852 = DvValueTemplate.Instantiate("template.inline.c2d_install_scene.1380", v_action__721852, v_law__722213, v_actor__718929);
                        v_mind__718966 = F_dv_adopt(v_mind__718966, F_campaign_method(v_law__722213, F_c2d_repair_record_law(F_dv_get(v_actor__718929, "id"))), "predict", R.number("1"), v_repair_source__717268);
                    }
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_install_scene.1381", v_action__721852)))
                {
                    object v_law__722783 = Authored("m50.c2d_install_scene.literal_0008");
                    v_action__721852 = DvValueTemplate.Instantiate("template.inline.c2d_install_scene.1382", v_action__721852, v_law__722783, v_actor__718929);
                    v_mind__718966 = F_dv_adopt(v_mind__718966, F_campaign_method(v_law__722783, F_c2d_request_repair_record_law()), "predict", R.number("1"), F_c2d_source(Authored("m50.c2_special_actions.literal_0005"), Authored("m50.c2d_curriculum.literal_0009"), Authored("m50.c2d_install_scene.literal_0014")));
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2d_install_scene.1383", v_action__721852)))
                {
                    object v_law__723546 = Authored("m50.c2d_install_scene.literal_0010");
                    v_action__721852 = DvValueTemplate.Instantiate("template.inline.c2d_install_scene.1384", v_action__721852, v_law__723546, v_actor__718929);
                    v_mind__718966 = F_dv_adopt(v_mind__718966, F_campaign_method(v_law__723546, F_c2d_patient_take_record_law()), "predict", R.number("1"), F_c2d_source(Authored("m50.c2_supply_actions.literal_0005"), Authored("m50.c2_special_actions.literal_0027"), Authored("m50.c2d_install_scene.literal_0011")));
                }
                v_actions__720446 = F_dvs_replace(v_actions__720446, v_j__718997, v_action__721852);
                v_j__718997 = R.add(v_j__718997, R.number("1"));
            }
            v_actor__718929 = DvValueTemplate.Instantiate("template.inline.c2d_install_scene.1385", v_actor__718929, v_mind__718966, v_actions__720446);
            _ = R.push(v_actors__718854, F_c2d_refresh_actor_goals(v_actor__718929));
            v_i__715647 = R.add(v_i__715647, R.number("1"));
        }
        return F_dv_with_many(v_scene, new List<object> { F_dv_pair("physical", v_physical__715534), F_dv_pair("actors", v_actors__718854), F_dv_pair("bindings", v_bindings__715569), F_dv_pair("world_processes", v_processes__716390), F_dv_pair("chapter2_dialogue_installed", true) });
    }
    public static object F_c2d_known_speaker_name(object v_mind,object v_actor) {
        return DvValueTemplate.Instantiate("template.c2d_known_speaker_name", v_mind, v_actor);
    }
    public static object F_c2h_ref(object v_field) {
        return DvValueTemplate.Instantiate("template.c2h_ref", v_field);
    }
    public static object F_c2h_person(object v_actor,object v_field) {
        return DvValueTemplate.Instantiate("template.c2h_person", v_actor, v_field);
    }
    public static object F_c2h_source() {
        return DvValueTemplate.Instantiate("template.c2h_source");
    }
    public static object F_c2h_contract() {
        return DvValueTemplate.Instantiate("template.c2h_contract");
    }
    public static object F_c2h_consent_requires(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2h_consent_requires", v_actor);
    }
    public static object F_c2h_objection(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2h_objection", v_actor);
    }
    public static object F_c2h_rows() {
        object v_rows__728488 = new List<object> {  };
        object v_source__728507 = F_c2h_source();
        object v_proposal_id__728538 = Authored("m50.c2h_rows.proposal_id");
        object v_propose__728625 = F_c2d_row(Authored("m50.c2_norms_build.literal_0074"), v_proposal_id__728538, Authored("m50.c2_norms_build.literal_0048"), F_c2h_contract(), F_dv_true(), Authored("m50.c2h_rows.literal"), Authored("m50.c2h_rows.literal_0002"), v_source__728507);
        v_propose__728625 = DvValueTemplate.Instantiate("template.inline.c2h_rows.1386", v_propose__728625);
        _ = R.push(v_rows__728488, v_propose__728625);
        object v_people__729264 = F_campaign_people();
        object v_i__729300 = R.number("0");
        while (R.Bool(R.lt(v_i__729300, R.count(v_people__729264))))
        {
            object v_actor__729352 = R.at(v_people__729264, v_i__729300);
            object v_ready__729383 = F_dv_all(new List<object> { F_c2_alive(v_actor__729352), F_c2h_contract() });
            object v_consent__729446 = F_c2h_consent_requires(v_actor__729352);
            object v_options__729497 = AuthoredCopy("m50.c2h_rows.options");
            object v_n__729595 = R.number("0");
            while (R.Bool(R.lt(v_n__729595, R.count(v_options__729497))))
            {
                object v_accepts__729656 = R.eq(R.at(v_options__729497, v_n__729595), Authored("m50.c2h_rows.literal_0004"));
                object v_when__729762 = v_ready__729383;
                object v_text__729792 = F_c2h_objection(v_actor__729352);
                if (R.Bool(v_accepts__729656))
                {
                    v_when__729762 = F_dv_all(new List<object> { v_ready__729383, v_consent__729446 });
                    v_text__729792 = Authored("m50.c2h_rows.literal_0005");
                }
                object v_decision_field__730000 = Authored("m50.c2h_rows.decision_field");
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2h_rows.1387", v_actor__729352)))
                {
                    v_decision_field__730000 = Authored("m50.c2h_rows.literal_0006");
                }
                else
                {
                    if (R.Bool(v_accepts__729656))
                    {
                        v_text__729792 = Authored("m50.c2h_rows.literal_0007");
                    }
                }
                object v_id__730413 = R.cat(v_actor__729352, R.cat(Authored("m50.c2h_rows.literal_0008"), R.at(v_options__729497, v_n__729595)));
                object v_row__730535 = F_c2d_row(v_actor__729352, v_id__730413, Authored("m50.c2_norms_build.literal_0048"), v_when__729762, F_dv_true(), F_dv_get(AuthoredCopy("m50.c2h_rows.literal_0009"), R.at(v_options__729497, v_n__729595)), v_text__729792, v_source__728507);
                v_row__730535 = F_dv_with_many(v_row__730535, new List<object> { DvValueTemplate.Instantiate("template.inline.c2h_rows.1388", v_actor__729352, v_decision_field__730000, v_accepts__729656, v_options__729497, v_n__729595), F_dv_pair("repeatable", true), F_dv_pair("owned_decision", F_c2h_decision(v_actor__729352, R.at(v_options__729497, v_n__729595))), DvValueTemplate.Instantiate("template.inline.c2h_rows.1389", v_proposal_id__728538) });
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2h_rows.1390", v_actor__729352)))
                {
                    v_row__730535 = F_dv_without(v_row__730535, "received_speech_requirement");
                    v_row__730535 = DvValueTemplate.Instantiate("template.inline.c2h_rows.1391", v_row__730535, v_when__729762);
                }
                _ = R.push(v_rows__728488, v_row__730535);
                v_n__729595 = R.add(v_n__729595, R.number("1"));
            }
            v_i__729300 = R.add(v_i__729300, R.number("1"));
        }
        object v_ruan_id__731811 = Authored("m50.c2h_rows.ruan_id");
        object v_proposal__731890 = Authored("m50.c2h_rows.proposal");
        object v_ruan_pre__731971 = DvValueTemplate.Instantiate("template.inline.c2h_rows.1392");
        object v_ruan__732262 = F_c2d_row(Authored("m50.c2_norms_build.literal_0003"), v_ruan_id__731811, Authored("m50.c2_norms_build.literal_0004"), v_ruan_pre__731971, F_dv_true(), Authored("m50.c2h_rows.literal_0011"), Authored("m50.c2h_rows.literal_0012"), v_source__728507);
        v_ruan__732262 = DvValueTemplate.Instantiate("template.inline.c2h_rows.1393", v_ruan__732262, v_ruan_id__731811, v_ruan_pre__731971);
        _ = R.push(v_rows__728488, v_ruan__732262);
        object v_ward_pre__733382 = F_dv_all(new List<object> { F_c2h_present(Authored("m50.c2_norms_build.literal_0013")), F_dv_fact(F_c2h_person(Authored("m50.c2_norms_build.literal_0003"), Authored("m50.c2h_rows.literal_0013")), true), F_c2h_balance_condition() });
        object v_ward__733686 = F_c2d_row(Authored("m50.c2_norms_build.literal_0013"), v_proposal__731890, Authored("m50.c2_norms_build.literal_0004"), v_ward_pre__733382, F_dv_true(), Authored("m50.c2h_rows.literal_0016"), Authored("m50.c2h_rows.literal_0017"), v_source__728507);
        v_ward__733686 = DvValueTemplate.Instantiate("template.inline.c2h_rows.1394", v_ward__733686, v_proposal__731890, v_ruan_id__731811, v_ward_pre__733382);
        _ = R.push(v_rows__728488, v_ward__733686);
        v_i__729300 = R.number("0");
        while (R.Bool(R.lt(v_i__729300, R.count(v_people__729264))))
        {
            object v_actor__734844 = R.at(v_people__729264, v_i__729300);
            object v_ready__734875 = DvValueTemplate.Instantiate("template.inline.c2h_rows.1395", v_actor__734844);
            object v_options__735015 = AuthoredCopy("m50.c2h_rows.options_0002");
            object v_n__735118 = R.number("0");
            while (R.Bool(R.lt(v_n__735118, R.count(v_options__735015))))
            {
                object v_accepts__735179 = R.eq(R.at(v_options__735015, v_n__735118), Authored("m50.c2h_rows.literal_0004"));
                object v_promotes__735285 = AuthoredCopy("m50.c2d_companion_comfort_rows.literal_0023");
                object v_sacrifices__735411 = AuthoredCopy("m50.c2_norms_build.literal_0068");
                object v_text__735527 = Authored("m50.c2h_rows.text");
                if (R.Bool(v_accepts__735179))
                {
                    v_promotes__735285 = AuthoredCopy("m50.c2h_rows.literal_0021");
                    v_sacrifices__735411 = AuthoredCopy("m50.c2d_companion_comfort_rows.literal_0023");
                    v_text__735527 = Authored("m50.c2h_rows.literal_0022");
                }
                object v_id__735958 = R.cat(v_actor__734844, R.cat(Authored("m50.c2h_rows.literal_0023"), R.at(v_options__735015, v_n__735118)));
                object v_row__736080 = F_c2d_row(v_actor__734844, v_id__735958, Authored("m50.c2_norms_build.literal_0004"), v_ready__734875, F_dv_true(), F_dv_get(AuthoredCopy("m50.c2h_rows.literal_0024"), R.at(v_options__735015, v_n__735118)), v_text__735527, v_source__728507);
                v_row__736080 = DvValueTemplate.Instantiate("template.inline.c2h_rows.1396", v_row__736080, v_actor__734844, v_options__735015, v_n__735118, v_id__735958, v_proposal__731890, v_ready__734875, v_promotes__735285, v_sacrifices__735411);
                _ = R.push(v_rows__728488, v_row__736080);
                v_n__735118 = R.add(v_n__735118, R.number("1"));
            }
            v_i__729300 = R.add(v_i__729300, R.number("1"));
        }
        object v_passed__737197 = F_c2h_motion_passed_condition();
        object v_record__737245 = F_c2d_row(Authored("m50.c2_norms_build.literal_0013"), Authored("m50.c2h_rows.literal_0027"), Authored("m50.c2_norms_build.literal_0004"), v_passed__737197, F_dv_true(), Authored("m50.c2h_rows.literal_0028"), Authored("m50.c2h_rows.literal_0029"), v_source__728507);
        v_record__737245 = DvValueTemplate.Instantiate("template.inline.c2h_rows.1397", v_record__737245, v_passed__737197);
        _ = R.push(v_rows__728488, v_record__737245);
        object v_refuse_pre__738266 = DvValueTemplate.Instantiate("template.inline.c2h_rows.1398");
        object v_refuse__738697 = F_c2d_row(Authored("m50.c2_norms_build.literal_0056"), Authored("m50.c2h_rows.literal_0033"), Authored("m50.c2_norms_build.literal_0004"), v_refuse_pre__738266, F_dv_true(), Authored("m50.c2h_rows.literal_0034"), Authored("m50.c2h_rows.literal_0035"), v_source__728507);
        v_refuse__738697 = DvValueTemplate.Instantiate("template.inline.c2h_rows.1399", v_refuse__738697, v_refuse_pre__738266);
        _ = R.push(v_rows__728488, v_refuse__738697);
        return v_rows__728488;
    }
    public static object F_c2h_present(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2h_present", v_actor);
    }
    public static object F_c2h_exile_choice(object v_group,object v_requires,object v_promotes,object v_sacrifices) {
        return DvValueTemplate.Instantiate("template.c2h_exile_choice", v_group, v_requires, v_promotes, v_sacrifices);
    }
    public static object F_c2h_exile_decision(object v_actor,object v_stance) {
        return DvValueTemplate.Instantiate("template.c2h_exile_decision", v_actor, v_stance);
    }
    public static object F_c2h_at_least_conditions(object v_items,object v_start,object v_count) {
        if (R.Bool(R.not(R.lt(R.number("0"), v_count))))
        {
            return F_dv_true();
        }
        if (R.Bool(R.lt(R.sub(R.count(v_items), v_start), v_count)))
        {
            return F_dv_not(F_dv_true());
        }
        return F_campaign_any(F_dv_all(new List<object> { R.at(v_items, v_start), F_c2h_at_least_conditions(v_items, R.add(v_start, R.number("1")), R.sub(v_count, R.number("1"))) }), F_c2h_at_least_conditions(v_items, R.add(v_start, R.number("1")), v_count));
    }
    public static object F_c2h_balance_condition() {
        object v_guardians__741901 = AuthoredCopy("m50.c2h_balance_condition.guardians");
        object v_calculators__742012 = AuthoredCopy("m50.c2h_balance_condition.calculators");
        object v_g__742127 = new List<object> {  };
        object v_c__742143 = new List<object> {  };
        object v_i__742159 = R.number("0");
        while (R.Bool(R.lt(v_i__742159, R.count(v_guardians__741901))))
        {
            _ = R.push(v_g__742127, F_c2h_present(R.at(v_guardians__741901, v_i__742159)));
            v_i__742159 = R.add(v_i__742159, R.number("1"));
        }
        v_i__742159 = R.number("0");
        while (R.Bool(R.lt(v_i__742159, R.count(v_calculators__742012))))
        {
            _ = R.push(v_c__742143, F_c2h_present(R.at(v_calculators__742012, v_i__742159)));
            v_i__742159 = R.add(v_i__742159, R.number("1"));
        }
        object v_cases__742393 = new List<object> {  };
        v_i__742159 = R.number("1");
        while (R.Bool(R.not(R.lt(R.count(v_calculators__742012), v_i__742159))))
        {
            _ = R.push(v_cases__742393, F_dv_all(new List<object> { F_c2h_at_least_conditions(v_c__742143, R.number("0"), v_i__742159), F_dv_not(F_c2h_at_least_conditions(v_g__742127, R.number("0"), v_i__742159)) }));
            v_i__742159 = R.add(v_i__742159, R.number("1"));
        }
        return F_dv_not(F_dv_all(F_c2_negated_conditions(v_cases__742393)));
    }
    public static object F_c2h_motion_passed_condition() {
        object v_clauses__742686 = new List<object> { F_c2h_present(Authored("m50.c2_norms_build.literal_0013")), F_dv_fact(F_c2h_ref(Authored("m50.c2h_rows.literal_0018")), true), F_c2h_balance_condition() };
        object v_people__742906 = F_campaign_people();
        object v_i__742942 = R.number("0");
        while (R.Bool(R.lt(v_i__742942, R.count(v_people__742906))))
        {
            object v_actor__742994 = R.at(v_people__742906, v_i__742942);
            object v_excluded__743107 = F_campaign_any(F_dv_fact(F_campaign_own(v_actor__742994, Authored("m50.c2_alive.literal_0002")), false), F_dv_fact(F_campaign_own(v_actor__742994, Authored("m50.c2_can_act.literal_0002")), true));
            object v_consent__743351 = DvValueTemplate.Instantiate("template.inline.c2h_motion_passed_condition.1400", v_actor__742994);
            _ = R.push(v_clauses__742686, F_campaign_any(v_excluded__743107, v_consent__743351));
            v_i__742942 = R.add(v_i__742942, R.number("1"));
        }
        return F_dv_all(v_clauses__742686);
    }
    public static object F_c2h_inspect_ballot(object v_actor,object v_person) {
        return DvValueTemplate.Instantiate("template.c2h_inspect_ballot", v_actor, v_person);
    }
    public static object F_c2h_is_exile(object v_id) {
        return ContentTemplateQueries.InstantiateBoolean("template.c2h_is_exile", v_id);
    }
    public static object F_c2h_install_scene(object v_scene) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2h_install_scene.1401", v_scene)))
        {
            return v_scene;
        }
        object v_fields__745741 = DvValueTemplate.Instantiate("template.inline.c2h_install_scene.1402");
        object v_people__746142 = F_campaign_people();
        object v_i__746178 = R.number("0");
        while (R.Bool(R.lt(v_i__746178, R.count(v_people__746142))))
        {
            v_fields__745741 = DvValueTemplate.Instantiate("template.inline.c2h_install_scene.1403", v_fields__745741, v_people__746142, v_i__746178);
            v_i__746178 = R.add(v_i__746178, R.number("1"));
        }
        object v_physical__746839 = F_campaign_add_new(F_dv_get(v_scene, "physical"), F_c2_object(Authored("m50.c2h_ref.literal"), Authored("m50.c2_norms_build.literal_0048"), v_fields__745741));
        object v_bindings__747043 = F_dv_clone(F_dv_get(v_scene, "bindings"));
        object v_actors__747088 = new List<object> {  };
        object v_rows__747109 = F_c2h_rows();
        v_i__746178 = R.number("0");
        while (R.Bool(R.lt(v_i__746178, R.count(v_rows__747109))))
        {
            v_physical__746839 = F_dv_store(v_physical__746839, F_c2d_mark(F_dv_get(R.at(v_rows__747109, v_i__746178), "actor"), F_dv_get(R.at(v_rows__747109, v_i__746178), "id")), F_dv_field(false));
            v_physical__746839 = F_dv_adopt(v_physical__746839, F_campaign_method(F_dv_get(R.at(v_rows__747109, v_i__746178), "id"), F_c2d_row_law(R.at(v_rows__747109, v_i__746178))), "enforce", R.number("1"), new List<object> { F_dv_get(R.at(v_rows__747109, v_i__746178), "source") });
            v_i__746178 = R.add(v_i__746178, R.number("1"));
        }
        v_i__746178 = R.number("0");
        while (R.Bool(R.lt(v_i__746178, R.count(F_dv_get(v_scene, "actors")))))
        {
            object v_actor__747466 = R.at(F_dv_get(v_scene, "actors"), v_i__746178);
            object v_mind__747503 = F_campaign_add_new(F_dv_get(v_actor__747466, "mind"), F_campaign_local_object(Authored("m50.c2h_ref.literal")));
            if (R.Bool(R.eq(F_dv_local(v_bindings__747043, Authored("m50.c2h_ref.literal"), F_dv_get(v_actor__747466, "id")), "")))
            {
                _ = R.push(v_bindings__747043, F_dv_binding(Authored("m50.c2h_ref.literal"), Authored("m50.c2h_ref.literal"), F_dv_get(v_actor__747466, "id")));
            }
            object v_actions__747916 = F_dv_clone(F_dv_get(v_actor__747466, "actions"));
            if (R.Bool(F_dv_has(v_people__746142, F_dv_get(v_actor__747466, "id"))))
            {
                v_mind__747503 = F_dv_store(v_mind__747503, F_c2h_person(F_dv_get(v_actor__747466, "id"), Authored("m50.c2h_rows.literal_0010")), F_dv_field(false));
            }
            object v_j__748141 = R.number("0");
            while (R.Bool(R.lt(v_j__748141, R.count(v_rows__747109))))
            {
                object v_row__748199 = R.at(v_rows__747109, v_j__748141);
                if (R.Bool(R.eq(F_dv_get(v_row__748199, "actor"), F_dv_get(v_actor__747466, "id"))))
                {
                    v_mind__747503 = F_dv_store(v_mind__747503, F_c2d_mark(F_dv_get(v_actor__747466, "id"), F_dv_get(v_row__748199, "id")), F_dv_field(false));
                    v_mind__747503 = F_dv_adopt(v_mind__747503, F_campaign_method(F_dv_get(v_row__748199, "id"), F_c2d_row_law(v_row__748199)), "predict", R.number("1"), new List<object> { F_dv_get(v_row__748199, "source") });
                    object v_is_exile__748467 = F_c2h_is_exile(F_dv_get(v_row__748199, "id"));
                    object v_act__748520 = F_c2d_action(v_row__748199);
                    if (R.Bool(R.not(v_is_exile__748467)))
                    {
                        v_act__748520 = DvValueTemplate.Instantiate("template.inline.c2h_install_scene.1404", v_act__748520);
                    }
                    _ = R.push(v_actions__747916, v_act__748520);
                }
                v_j__748141 = R.add(v_j__748141, R.number("1"));
            }
            if (R.Bool(F_dv_has(v_people__746142, F_dv_get(v_actor__747466, "id"))))
            {
                v_j__748141 = R.number("0");
                while (R.Bool(R.lt(v_j__748141, R.count(v_people__746142))))
                {
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2h_install_scene.1405", v_actor__747466, v_people__746142, v_j__748141)))
                    {
                        _ = R.push(v_actions__747916, F_c2h_inspect_ballot(F_dv_get(v_actor__747466, "id"), R.at(v_people__746142, v_j__748141)));
                    }
                    v_j__748141 = R.add(v_j__748141, R.number("1"));
                }
            }
            _ = R.push(v_actors__747088, F_dv_with_many(v_actor__747466, new List<object> { F_dv_pair("mind", v_mind__747503), F_dv_pair("actions", F_campaign_public_methods(v_actions__747916)) }));
            v_i__746178 = R.add(v_i__746178, R.number("1"));
        }
        return F_dv_with_many(v_scene, new List<object> { F_dv_pair("physical", v_physical__746839), F_dv_pair("actors", v_actors__747088), F_dv_pair("bindings", v_bindings__747043), F_dv_pair("chapter2_homecoming_installed", true) });
    }
    public static object F_c2h_decision(object v_actor,object v_stance) {
        object v_refs__749470 = DvValueTemplate.Instantiate("template.inline.c2h_decision.1406");
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2h_decision.1407", v_actor)))
        {
            _ = R.push(v_refs__749470, F_c2h_ref(Authored("m50.c2h_consent_requires.literal_0002")));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2h_decision.1408", v_actor)))
        {
            v_refs__749470 = F_dvp_merge_refs(v_refs__749470, DvValueTemplate.Instantiate("template.inline.c2h_decision.1409"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2h_decision.1410", v_actor)))
        {
            v_refs__749470 = F_dvp_merge_refs(v_refs__749470, DvValueTemplate.Instantiate("template.inline.c2h_decision.1411"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2h_decision.1412", v_actor)))
        {
            v_refs__749470 = F_dvp_merge_refs(v_refs__749470, DvValueTemplate.Instantiate("template.inline.c2h_decision.1413"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2h_decision.1414", v_actor)))
        {
            v_refs__749470 = F_dvp_merge_refs(v_refs__749470, DvValueTemplate.Instantiate("template.inline.c2h_decision.1415", v_actor));
        }
        return DvValueTemplate.Instantiate("template.inline.c2h_decision.1416", v_actor, v_stance, v_refs__749470);
    }
    public static object F_c2h_owner_utility(object v_actor,object v_action,object v_consent) {
        object v_direction__752526 = R.number("1");
        if (R.Bool(R.not(v_consent)))
        {
            v_direction__752526 = R.sub(R.number("0"), R.number("1"));
        }
        object v_trust__752749 = F_dvs_speaker_trust(F_dv_get(v_actor, "policy"), Authored("m50.c2_norms_build.literal_0074"));
        object v_basis__752870 = new List<object> { F_c2h_source(), new List<object> { DvValueTemplate.Instantiate("template.inline.c2h_owner_utility.1417", v_actor), DvValueTemplate.Instantiate("template.inline.c2h_owner_utility.1418", v_actor), DvValueTemplate.Instantiate("template.inline.c2h_owner_utility.1419", v_actor), DvValueTemplate.Instantiate("template.inline.c2h_owner_utility.1420"), F_dv_pair("speaker_trust", v_trust__752749), DvValueTemplate.Instantiate("template.inline.c2h_owner_utility.1421", v_actor), DvValueTemplate.Instantiate("template.inline.c2h_owner_utility.1422", v_actor), F_dv_pair("owned_inputs", F_dv_get(F_dvs_owned_decision_inputs(v_actor, F_dv_get(v_action, "owned_decision"), F_dv_get(v_actor, "mind")), "sources")) } };
        return DvValueTemplate.Instantiate("template.inline.c2h_owner_utility.1423", v_action, v_basis__752870, v_direction__752526);
    }
    public static object F_c2h_refresh_actor(object v_actor) {
        // Borrow stable descendants during queries; only the appended goal root
        // is mutable. Later queries still see each appended goal through actor.
        v_actor = F_c2_query_view(v_actor, v_actor);
        object v_original_actions = F_dv_get(v_actor, "actions");
        object v_actions__754396 = v_original_actions;
        object v_goals__754429 = F_dv_clone(F_dv_get(F_dv_get(v_actor, "policy"), "goals"));
        object v_i__754465 = R.number("0");
        R.phase("c2-homecoming-actions-start", "");
        try {
        while (R.Bool(R.lt(v_i__754465, R.count(v_actions__754396))))
        {
            object v_action__754518 = R.at(v_original_actions, v_i__754465);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2h_refresh_actor.1424", v_action__754518, v_actor)))
            {
                v_action__754518 = F_dv_without(v_action__754518, "personal_choice");
                v_action__754518 = F_dv_with(v_action__754518, Authored("m50.c2h_refresh_actor.literal"), F_c2h_owner_utility(v_actor, v_action__754518, R.eq(F_dv_get(F_dv_get(v_action__754518, "owned_decision"), "stance"), Authored("m50.c2h_rows.literal_0004"))));
                v_actions__754396 = F_dvs_replace(v_actions__754396, v_i__754465, v_action__754518);
            }
            if (R.Bool(F_c2h_is_exile(F_dv_get(v_action__754518, "id"))))
            {
                object v_available__755111 = F_dv_get(v_action__754518, "precondition");
                if (R.Bool((R.Bool(F_dv_has_key(v_action__754518, "received_speech_requirement")) ? (object)(R.not(F_c2_received_requirement(v_actor, F_dv_get(v_action__754518, "received_speech_requirement")))) : (object)(false))))
                {
                    v_available__755111 = F_dv_not(F_dv_true());
                }
                if (R.Bool(R.not(F_dvp_owned_decision_ready(v_actor, v_action__754518, F_dv_get(v_actor, "mind")))))
                {
                    v_available__755111 = F_dv_not(F_dv_true());
                }
                _ = R.push(v_goals__754429, F_campaign_index_goal_window(DvValueTemplate.Instantiate("template.inline.c2h_refresh_actor.1425", v_action__754518, v_available__755111)));
                v_actor = F_c2_query_view(v_actor, F_dv_with(v_actor, "policy", F_dv_with(F_dv_get(v_actor, "policy"), "goals", v_goals__754429)));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2h_refresh_actor.1426", v_actor, v_action__754518)))
            {
                v_action__754518 = DvValueTemplate.Instantiate("template.inline.c2h_refresh_actor.1427", v_action__754518);
                v_actions__754396 = F_dvs_replace(v_actions__754396, v_i__754465, v_action__754518);
                object v_available__756440 = F_dv_get(v_action__754518, "precondition");
                if (R.Bool((R.Bool(F_dv_has_key(v_action__754518, "received_speech_requirement")) ? (object)(R.not(F_c2_received_requirement(v_actor, F_dv_get(v_action__754518, "received_speech_requirement")))) : (object)(false))))
                {
                    v_available__756440 = F_dv_not(F_dv_true());
                }
                if (R.Bool(R.not(F_dvp_owned_decision_ready(v_actor, v_action__754518, F_dv_get(v_actor, "mind")))))
                {
                    v_available__756440 = F_dv_not(F_dv_true());
                }
                _ = R.push(v_goals__754429, F_campaign_index_goal_window(DvValueTemplate.Instantiate("template.inline.c2h_refresh_actor.1428", v_action__754518, v_available__756440)));
                v_actor = F_c2_query_view(v_actor, F_dv_with(v_actor, "policy", F_dv_with(F_dv_get(v_actor, "policy"), "goals", v_goals__754429)));
            }
            v_i__754465 = R.add(v_i__754465, R.number("1"));
        }
        }
        finally { R.phase("c2-homecoming-actions-end", ""); }
        R.phase("c2-homecoming-ballots-start", "");
        try {
        if (R.Bool(F_dv_has(F_campaign_people(), F_dv_get(v_actor, "id"))))
        {
            object v_people__757270 = new List<object> { F_dv_get(v_actor, "id") };
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2h_refresh_actor.1429", v_actor)))
            {
                v_people__757270 = F_campaign_people();
            }
            v_i__754465 = R.number("0");
            while (R.Bool(R.lt(v_i__754465, R.count(v_people__757270))))
            {
                object v_person__757495 = R.at(v_people__757270, v_i__754465);
                object v_opened__757531 = F_dv_fact(F_c2h_ref(Authored("m50.c2h_rows.literal_0018")), true);
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2h_refresh_actor.1430", v_actor)))
                {
                    v_opened__757531 = F_campaign_any(v_opened__757531, F_dv_fact(F_c2h_person(Authored("m50.c2_norms_build.literal_0003"), Authored("m50.c2h_rows.literal_0013")), true));
                }
                object v_ready__757958 = DvValueTemplate.Instantiate("template.inline.c2h_refresh_actor.1431", v_actor, v_person__757495, v_opened__757531);
                object v_refs__758188 = DvValueTemplate.Instantiate("template.inline.c2h_refresh_actor.1432", v_person__757495);
                object v_r__758562 = R.number("0");
                while (R.Bool(R.lt(v_r__758562, R.count(v_refs__758188))))
                {
                    _ = R.push(v_goals__754429, F_dv_with(F_campaign_observation_goal(R.at(v_refs__758188, v_r__758562), v_ready__757958, new List<object> { F_c2h_source() }, R.number("0")), Authored("m50.c2_navigation_arrival_goals.literal"), Authored("m50.c2_norms_build.literal_0004")));
                    v_actor = F_c2_query_view(v_actor, F_dv_with(v_actor, "policy", F_dv_with(F_dv_get(v_actor, "policy"), "goals", v_goals__754429)));
                    v_r__758562 = R.add(v_r__758562, R.number("1"));
                }
                v_i__754465 = R.add(v_i__754465, R.number("1"));
            }
        }
        }
        finally { R.phase("c2-homecoming-ballots-end", ""); }
        v_goals__754429 = F_c2_query_view(v_actor, v_goals__754429);
        object v_substantive__758921 = new List<object> {  };
        v_i__754465 = R.number("0");
        while (R.Bool(R.lt(v_i__754465, R.count(v_goals__754429))))
        {
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2h_refresh_actor.1433", v_goals__754429, v_i__754465)))
            {
                _ = R.push(v_substantive__758921, R.at(v_goals__754429, v_i__754465));
            }
            v_i__754465 = R.add(v_i__754465, R.number("1"));
        }
        object v_view__759168 = F_dv_with_many(v_actor, new List<object> { F_dv_pair("actions", v_actions__754396), F_dv_pair("policy", F_dv_with(F_dv_get(v_actor, "policy"), "goals", v_substantive__758921)) });
        R.phase("c2-goals-navigation-start", "");
        try {
            object navigationGoals = F_c2_navigation_goals(v_view__759168, v_substantive__758921, out object navigationDiagnostic);
            return F_campaign_request_withdrawals(F_campaign_navigation_frontier(F_dv_with_many(v_view__759168, new List<object> { F_dv_pair("policy", F_dv_with(F_dv_get(v_view__759168, "policy"), "goals", navigationGoals)), F_dv_pair("navigation_route_diagnostic", navigationDiagnostic) })));
        }
        finally { R.phase("c2-goals-navigation-end", ""); }
    }
    public static object F_c2h_guardian_balance(object v_physical) {
        object v_groups__759426 = AuthoredCopy("m50.c2h_guardian_balance.groups");
        object v_counts__759530 = new List<object> {  };
        object v_ballots__759551 = new List<object> {  };
        object v_group__759573 = R.number("0");
        while (R.Bool(R.lt(v_group__759573, R.count(v_groups__759426))))
        {
            object v_present__759633 = R.number("0");
            object v_i__759658 = R.number("0");
            while (R.Bool(R.lt(v_i__759658, R.count(R.at(v_groups__759426, v_group__759573)))))
            {
                object v_actor__759725 = R.at(R.at(v_groups__759426, v_group__759573), v_i__759658);
                object v_eligible__759767 = F_dv_truth(v_physical, F_c2h_present(v_actor__759725));
                if (R.Bool(R.eq(v_eligible__759767, "true")))
                {
                    v_present__759633 = R.add(v_present__759633, R.number("1"));
                }
                _ = R.push(v_ballots__759551, DvValueTemplate.Instantiate("template.inline.c2h_guardian_balance.1434", v_actor__759725, v_eligible__759767, v_physical));
                v_i__759658 = R.add(v_i__759658, R.number("1"));
            }
            _ = R.push(v_counts__759530, v_present__759633);
            v_group__759573 = R.add(v_group__759573, R.number("1"));
        }
        object v_all_ballots__760176 = new List<object> {  };
        object v_people__760202 = F_campaign_people();
        object v_i__760238 = R.number("0");
        while (R.Bool(R.lt(v_i__760238, R.count(v_people__760202))))
        {
            object v_actor__760290 = R.at(v_people__760202, v_i__760238);
            _ = R.push(v_all_ballots__760176, DvValueTemplate.Instantiate("template.inline.c2h_guardian_balance.1435", v_actor__760290, v_physical));
            v_i__760238 = R.add(v_i__760238, R.number("1"));
        }
        return new List<object> { F_dv_pair("guardians_present_capable", R.at(v_counts__759530, R.number("0"))), F_dv_pair("calculators_present_capable", R.at(v_counts__759530, R.number("1"))), F_dv_pair("background_gate", F_dv_truth(v_physical, F_c2h_balance_condition())), F_dv_pair("group_members", v_ballots__759551), F_dv_pair("ballots", v_all_ballots__760176), F_dv_pair("unanimous_decision_eligible", F_dv_truth(v_physical, F_c2h_motion_passed_condition())), DvValueTemplate.Instantiate("template.inline.c2h_guardian_balance.1436", v_physical), DvValueTemplate.Instantiate("template.inline.c2h_guardian_balance.1437", v_physical), DvValueTemplate.Instantiate("template.inline.c2h_guardian_balance.1438", v_physical), DvValueTemplate.Instantiate("template.inline.c2h_guardian_balance.1439") };
    }
    public static object F_c2h_trauma_imprints(object v_physical) {
        return DvValueTemplate.Instantiate("template.c2h_trauma_imprints", v_physical);
    }
    public static object F_c2h_summary(object v_physical) {
        object v_others__763934 = new List<object> {  };
        object v_unanimous__763955 = true;
        object v_people__763981 = F_campaign_people();
        object v_i__764017 = R.number("0");
        while (R.Bool(R.lt(v_i__764017, R.count(v_people__763981))))
        {
            object v_id__764069 = R.at(v_people__763981, v_i__764017);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2h_summary.1440", v_id__764069)))
            {
                object v_alive__764191 = F_dv_read(v_physical, F_campaign_own(v_id__764069, Authored("m50.c2_alive.literal_0002")));
                object v_willing__764318 = F_dv_read(v_physical, F_c2h_person(v_id__764069, Authored("m50.c2h_rows.decision_field")));
                _ = R.push(v_others__763934, new List<object> { F_dv_pair("actor", v_id__764069), F_dv_pair("alive", v_alive__764191), F_dv_pair("consent_detective", v_willing__764318), F_dv_pair("stance", F_dv_read(v_physical, F_c2h_person(v_id__764069, "stance"))) });
                if (R.Bool((R.Bool(R.not(F_dv_uncontested(v_alive__764191))) ? (object)(true) : (object)((R.Bool(R.eq(F_dv_get(v_alive__764191, "value"), true)) ? (object)((R.Bool(R.not(F_dv_uncontested(v_willing__764318))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_willing__764318, "value"), true))))) : (object)(false))))))
                {
                    v_unanimous__763955 = false;
                }
            }
            v_i__764017 = R.add(v_i__764017, R.number("1"));
        }
        object v_balance__764765 = F_c2h_guardian_balance(v_physical);
        return DvValueTemplate.Instantiate("template.inline.c2h_summary.1441", v_physical, v_others__763934, v_unanimous__763955, v_balance__764765);
    }
    public static object F_c2u_source(object v_id) {
        return DvValueTemplate.Instantiate("template.c2u_source", v_id);
    }
    public static object F_c2u_ref(object v_field) {
        return DvValueTemplate.Instantiate("template.c2u_ref", v_field);
    }
    public static object F_c2u_done(object v_actor,object v_id) {
        return DvValueTemplate.Instantiate("template.c2u_done", v_actor, v_id);
    }
    public static object F_c2u_row(object v_actor,object v_id,object v_room,object v_when,object v_label,object v_text,object v_kind,object v_effects) {
        return DvValueTemplate.Instantiate("template.c2u_row", v_actor, v_id, v_room, v_when, v_label, v_text, v_kind, v_effects);
    }
    public static object F_c2u_evening() {
        return DvValueTemplate.Instantiate("template.c2u_evening");
    }
    public static object F_c2u_rows() {
        object v_key__767074 = AuthoredCopy("m50.c2u_rows.key");
        object v_cached__767160 = R.memo_get(new List<object> {  }, v_key__767074);
        if (R.Bool(R.at(v_cached__767160, R.number("0"))))
        {
            return R.at(v_cached__767160, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__767074, F_c2u_rows_build());
    }
    public static object F_c2u_rows_build() {
        return DvValueTemplate.Instantiate("template.c2u_rows_build");
    }
    public static object F_c2u_inspection(object v_actor,object v_id,object v_room,object v_fields,object v_label,object v_text) {
        return DvValueTemplate.Instantiate("template.c2u_inspection", v_actor, v_id, v_room, v_fields, v_label, v_text);
    }
    public static object F_c2u_specs(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2u_specs", v_actor);
    }
    public static object F_c2u_goals(object v_actor) {
        object v_goals__803073 = new List<object> {  };
        object v_source__803093 = DvValueTemplate.Instantiate("template.inline.c2u_goals.1442");
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2u_goals.1443", v_actor)))
        {
            object v_recruited__803285 = F_dv_fact(F_c2u_ref(Authored("m50.c2u_goals.literal_0002")), true);
            object v_goal__803400 = F_campaign_observation_goal(F_c2u_ref(Authored("m50.c2u_rows_build.literal_0071")), F_dv_all(new List<object> { v_recruited__803285, F_c2u_evening() }), v_source__803093, R.number("0"));
            _ = R.push(v_goals__803073, DvValueTemplate.Instantiate("template.inline.c2u_goals.1444", v_goal__803400));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2u_goals.1445", v_actor)))
        {
            object v_goal__803813 = F_campaign_observation_goal(F_c2u_ref(Authored("m50.c2u_rows_build.literal_0071")), DvValueTemplate.Instantiate("template.inline.c2u_goals.1446"), v_source__803093, Authored("m50.c2_moran_fatal_settle_law.literal"));
            _ = R.push(v_goals__803073, DvValueTemplate.Instantiate("template.inline.c2u_goals.1447", v_goal__803813, v_actor));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2u_goals.1448", v_actor)))
        {
            object v_refs__804568 = DvValueTemplate.Instantiate("template.inline.c2u_goals.1449");
            object v_i__804846 = R.number("0");
            while (R.Bool(R.lt(v_i__804846, R.count(v_refs__804568))))
            {
                object v_goal__804904 = F_campaign_observation_goal(R.at(v_refs__804568, v_i__804846), DvValueTemplate.Instantiate("template.inline.c2u_goals.1450"), v_source__803093, Authored("m50.c2_moran_fatal_settle_law.literal"));
                _ = R.push(v_goals__803073, DvValueTemplate.Instantiate("template.inline.c2u_goals.1451", v_goal__804904));
                v_i__804846 = R.add(v_i__804846, R.number("1"));
            }
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2u_goals.1452", v_actor)))
        {
            _ = R.push(v_goals__803073, F_campaign_goal(F_campaign_at(F_dv_get(v_actor, "id"), Authored("m50.c2_norms_build.literal_0048")), DvValueTemplate.Instantiate("template.inline.c2u_goals.1453", v_actor), v_source__803093));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2u_goals.1454", v_actor)))
        {
            _ = R.push(v_goals__803073, F_campaign_goal(F_campaign_at(F_dv_get(v_actor, "id"), Authored("m50.c2u_rows_build.literal_0040")), DvValueTemplate.Instantiate("template.inline.c2u_goals.1455"), v_source__803093));
        }
        return v_goals__803073;
    }
    public static object F_c2u_install_scene(object v_scene) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1456", v_scene)))
        {
            return v_scene;
        }
        object v_fields__806528 = DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1457");
        object v_people__807531 = F_campaign_people();
        object v_i__807567 = R.number("0");
        while (R.Bool(R.lt(v_i__807567, R.count(v_people__807531))))
        {
            v_fields__806528 = DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1458", v_fields__806528, v_people__807531, v_i__807567);
            v_fields__806528 = DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1459", v_fields__806528, v_people__807531, v_i__807567);
            v_i__807567 = R.add(v_i__807567, R.number("1"));
        }
        object v_physical__807918 = F_campaign_add_new(F_dv_get(v_scene, "physical"), F_c2_object(Authored("m50.c2u_ref.literal"), "", v_fields__806528));
        object v_bindings__808054 = F_dv_get(v_scene, "bindings");
        object v_actors__808089 = new List<object> {  };
        v_i__807567 = R.number("0");
        while (R.Bool(R.lt(v_i__807567, R.count(F_dv_get(v_scene, "actors")))))
        {
            object v_actor__808164 = R.at(F_dv_get(v_scene, "actors"), v_i__807567);
            object v_mind__808201 = F_campaign_add_new(F_dv_get(v_actor__808164, "mind"), F_campaign_local_object(Authored("m50.c2u_ref.literal")));
            if (R.Bool(R.eq(F_dv_local(v_bindings__808054, Authored("m50.c2u_ref.literal"), F_dv_get(v_actor__808164, "id")), "")))
            {
                v_bindings__808054 = F_dv_append(v_bindings__808054, F_dv_binding(Authored("m50.c2u_ref.literal"), Authored("m50.c2u_ref.literal"), F_dv_get(v_actor__808164, "id")));
            }
            if (R.Bool(F_dv_has(v_people__807531, F_dv_get(v_actor__808164, "id"))))
            {
                v_mind__808201 = F_dv_store(v_mind__808201, F_c2u_ref(R.cat(Authored("m50.c2u_rows_build.literal_0088"), F_dv_get(v_actor__808164, "id"))), DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1460"));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1461", v_actor__808164)))
            {
                v_mind__808201 = F_dv_store(v_mind__808201, F_c2u_ref(R.cat(Authored("m50.c2u_rows_build.literal_0048"), F_dv_get(v_actor__808164, "id"))), DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1462"));
                v_mind__808201 = F_dv_store(v_mind__808201, F_c2u_ref(R.cat(Authored("m50.c2u_goals.literal_0006"), F_dv_get(v_actor__808164, "id"))), DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1463"));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1464", v_actor__808164)))
            {
                v_mind__808201 = F_dv_store(v_mind__808201, F_c2u_ref(Authored("m50.c2u_goals.literal_0002")), DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1465"));
                v_mind__808201 = F_dv_store(v_mind__808201, F_c2u_ref(Authored("m50.c2u_rows_build.literal_0075")), DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1466"));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1467", v_actor__808164)))
            {
                v_mind__808201 = F_dv_store(v_mind__808201, F_c2u_ref(R.cat(Authored("m50.c2u_rows_build.literal_0066"), F_dv_get(v_actor__808164, "id"))), DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1468"));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1469", v_actor__808164)))
            {
                v_mind__808201 = F_dv_store(v_mind__808201, F_c2u_ref(Authored("m50.c2u_rows_build.literal_0012")), DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1470"));
                v_mind__808201 = F_dv_store(v_mind__808201, F_c2u_ref(Authored("m50.c2u_rows_build.literal_0003")), DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1471"));
                v_actor__808164 = DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1472", v_actor__808164);
            }
            object v_specs__811275 = F_c2u_specs(F_dv_get(v_actor__808164, "id"));
            object v_actions__811316 = F_dv_get(v_actor__808164, "actions");
            object v_j__811353 = R.number("0");
            while (R.Bool(R.lt(v_j__811353, R.count(v_specs__811275))))
            {
                object v_spec__811412 = R.at(v_specs__811275, v_j__811353);
                if (R.Bool(R.not(R.eq(F_dv_get(v_spec__811412, "law"), false))))
                {
                    v_physical__807918 = F_dv_adopt(v_physical__807918, F_campaign_method(F_dv_get(F_dv_get(v_spec__811412, "action"), "id"), F_dv_get(v_spec__811412, "law")), "enforce", R.number("1"), new List<object> { F_c2u_source(F_dv_get(F_dv_get(v_spec__811412, "action"), "id")) });
                    v_mind__808201 = F_dv_adopt(v_mind__808201, F_campaign_method(F_dv_get(F_dv_get(v_spec__811412, "action"), "id"), F_dv_get(v_spec__811412, "law")), "predict", R.number("1"), new List<object> { F_c2u_source(F_dv_get(F_dv_get(v_spec__811412, "action"), "id")) });
                }
                _ = R.push(v_actions__811316, F_dv_get(v_spec__811412, "action"));
                v_j__811353 = R.add(v_j__811353, R.number("1"));
            }
            object v_hearing__811802 = F_dv_find(v_actor__808164, Authored("m50.c2u_install_scene.literal_0011"), new List<object> {  });
            _ = R.push(v_hearing__811802, DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1473", v_actor__808164));
            v_actor__808164 = DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1474", v_actor__808164);
            _ = R.push(v_actors__808089, F_dv_with_many(v_actor__808164, new List<object> { F_dv_pair("mind", v_mind__808201), F_dv_pair("actions", v_actions__811316), F_dv_pair("hearing_grants", v_hearing__811802) }));
            v_i__807567 = R.add(v_i__807567, R.number("1"));
        }
        object v_stupor_id__812944 = Authored("m50.c2u_install_scene.stupor_id");
        v_physical__807918 = F_dv_adopt(v_physical__807918, F_campaign_method(v_stupor_id__812944, F_c2u_moran_rescued_stupor_law()), "enforce", R.number("1"), DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1475"));
        object v_clear_id__813234 = Authored("m50.c2u_install_scene.clear_id");
        object v_clear_law__813324 = DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1476", v_clear_id__813234);
        v_physical__807918 = F_dv_adopt(v_physical__807918, F_campaign_method(v_clear_id__813234, v_clear_law__813324), "enforce", R.number("1"), DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1477"));
        object v_processes__814124 = F_dv_append(F_dv_append(F_dv_find(v_scene, Authored("m50.c2_extend_scene.literal_0002"), new List<object> {  }), DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1478", v_stupor_id__812944)), DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1479", v_clear_id__813234));
        object v_declarations__814541 = F_dv_find(v_scene, Authored("m50.c2u_install_scene.literal_0018"), new List<object> {  });
        object v_threat__814659 = DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1480");
        object v_danger_when__815231 = F_campaign_any(v_threat__814659, F_dv_fact(F_c2u_ref(Authored("m50.c2u_rows_build.literal_0025")), true));
        object v_mask__815371 = R.number("0");
        v_i__807567 = R.number("0");
        while (R.Bool(R.lt(v_i__807567, R.count(v_people__807531))))
        {
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1481", v_people__807531, v_i__807567)))
            {
                v_mask__815371 = F_dv_mask_bit(v_i__807567);
            }
            v_i__807567 = R.add(v_i__807567, R.number("1"));
        }
        object v_facts__815586 = F_dv_append(F_dv_find(v_declarations__814541, Authored("m50.c2u_install_scene.literal_0019"), new List<object> {  }), DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1482"));
        object v_dangers__815965 = F_dv_append(F_dv_find(v_declarations__814541, Authored("m50.c2u_install_scene.literal_0021"), new List<object> {  }), DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1483", v_danger_when__815231, v_mask__815371));
        object v_demon__816487 = DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1484", v_threat__814659);
        v_declarations__814541 = DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1485", v_declarations__814541, v_facts__815586, v_dangers__815965, v_demon__816487);
        object v_costs__817305 = F_dv_get(v_scene, "cost_model");
        v_costs__817305 = DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1486", v_costs__817305);
        object v_lengths__817847 = AuthoredCopy("m50.c2u_specs.lengths");
        v_i__807567 = R.number("0");
        while (R.Bool(R.lt(v_i__807567, R.count(v_lengths__817847))))
        {
            object v_vector__817991 = new List<object> { F_dv_pair("time", R.at(v_lengths__817847, v_i__807567)), F_dv_pair("computation", R.number("1")), F_dv_pair("stamina", R.number("0")) };
            v_costs__817305 = DvValueTemplate.Instantiate("template.inline.c2u_install_scene.1487", v_costs__817305, v_lengths__817847, v_i__807567, v_vector__817991);
            v_i__807567 = R.add(v_i__807567, R.number("1"));
        }
        return F_dv_with_many(v_scene, new List<object> { F_dv_pair("physical", v_physical__807918), F_dv_pair("actors", v_actors__808089), F_dv_pair("bindings", v_bindings__808054), F_dv_pair("cost_model", v_costs__817305), F_dv_pair("world_processes", v_processes__814124), F_dv_pair("simulation_declarations", v_declarations__814541), F_dv_pair("chapter2_unconventional_installed", true) });
    }
    public static object F_c2u_sketch_opportunity() {
        return DvValueTemplate.Instantiate("template.c2u_sketch_opportunity");
    }
    public static object F_c2u_signature_consent(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2u_signature_consent", v_actor);
    }
    public static object F_c2u_owned_consequences(object v_actor) {
        return DvValueTemplate.Instantiate("template.c2u_owned_consequences", v_actor);
    }
    public static object F_c2u_recruit_rows(object v_people,object v_task,object v_destination,object v_text) {
        return DvValueTemplate.Instantiate("template.c2u_recruit_rows", v_people, v_task, v_destination, v_text);
    }
    public static object F_c2u_moran_safe_interior() {
        return DvValueTemplate.Instantiate("template.c2u_moran_safe_interior");
    }
    public static object F_c2u_moran_rescued_stupor_law() {
        return DvValueTemplate.Instantiate("template.c2u_moran_rescued_stupor_law");
    }
}
