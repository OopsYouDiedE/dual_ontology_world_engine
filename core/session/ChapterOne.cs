using DuelView.Core;
using DuelView.Compiled;
using static DuelView.Core.DVKernel;
public static partial class CampaignRuntime {
    public static object F_c1_public_methods(object v_actions) {
        return DvValueTemplate.Instantiate("template.c1_public_methods", v_actions);
    }
    public static object F_c1_public_method(object v_action) {
        return DvValueTemplate.Instantiate("template.c1_public_method", v_action);
    }
    public static object F_c1_inquiry_choice(object v_action,object v_group,object v_thorough) {
        return DvValueTemplate.Instantiate("template.c1_inquiry_choice", v_action, v_group, v_thorough);
    }
    public static object F_c1_question_call_rule() {
        return DvValueTemplate.Instantiate("template.c1_question_call_rule");
    }
    public static object F_c1_shallow_question_action(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_shallow_question_action", v_actor);
    }
    public static object F_c1_answer_topic(object v_id) {
        return DvValueTemplate.Instantiate("template.c1_answer_topic", v_id);
    }
    public static object F_c1_once(object v_action) {
        return DvValueTemplate.Instantiate("template.c1_once", v_action);
    }
    public static object F_c1_observe_as(object v_action,object v_source,object v_origin,object v_trust) {
        return DvValueTemplate.Instantiate("template.c1_observe_as", v_action, v_source, v_origin, v_trust);
    }
    public static object F_c1_query_basis(object v_atom) {
        return DvValueTemplate.Instantiate("template.c1_query_basis", v_atom);
    }
    public static object F_c1_request_contact(object v_asker,object v_target) {
        return DvValueTemplate.Instantiate("template.c1_request_contact", v_asker, v_target);
    }
    public static object F_c1_personal_request(object v_action,object v_target) {
        return DvValueTemplate.Instantiate("template.c1_personal_request", v_action, v_target);
    }
    public static object F_c1_vector(object v_time) {
        return DvValueTemplate.Instantiate("template.c1_vector", v_time);
    }
    public static object F_c1_cost(object v_time,object v_source) {
        return DvValueTemplate.Instantiate("template.c1_cost", v_time, v_source);
    }
    public static object F_c1_materials() {
        return DvValueTemplate.Instantiate("template.c1_materials");
    }
    public static object F_c1_authority_conclusions() {
        return DvValueTemplate.Instantiate("template.c1_authority_conclusions");
    }
    public static object F_c1_physical_cognition() {
        object v_fields__15739 = F_c1_materials();
        object v_conclusions__15772 = F_c1_authority_conclusions();
        object v_i__15822 = R.number("0");
        while (R.Bool(R.lt(v_i__15822, R.count(v_conclusions__15772))))
        {
            v_fields__15739 = F_dv_with(v_fields__15739, R.at(R.at(v_conclusions__15772, v_i__15822), R.number("0")), R.at(R.at(v_conclusions__15772, v_i__15822), R.number("1")));
            v_i__15822 = R.add(v_i__15822, R.number("1"));
        }
        return F_campaign_state_object(Authored("m40.c1_physical_cognition.literal"), v_fields__15739);
    }
    public static object F_c1_position(object v_actor,object v_room) {
        return DvValueTemplate.Instantiate("template.c1_position", v_actor, v_room);
    }
    public static object F_c1_edges() {
        return DvValueTemplate.Instantiate("template.c1_edges");
    }
    public static object F_c1_inspect_E1(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c1_inspect_E1", v_行动者);
    }
    public static object F_c1_visible_E1() {
        return DvValueTemplate.Instantiate("template.c1_visible_E1");
    }
    public static object F_c1_inspect_E2(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c1_inspect_E2", v_行动者);
    }
    public static object F_c1_visible_E2() {
        return DvValueTemplate.Instantiate("template.c1_visible_E2");
    }
    public static object F_c1_inspect_E3(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c1_inspect_E3", v_行动者);
    }
    public static object F_c1_visible_E3() {
        return DvValueTemplate.Instantiate("template.c1_visible_E3");
    }
    public static object F_c1_inspect_E4(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c1_inspect_E4", v_行动者);
    }
    public static object F_c1_visible_E4() {
        return DvValueTemplate.Instantiate("template.c1_visible_E4");
    }
    public static object F_c1_inspect_E5(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_inspect_E5", v_actor);
    }
    public static object F_c1_visible_E5() {
        return DvValueTemplate.Instantiate("template.c1_visible_E5");
    }
    public static object F_c1_inspect_E6(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c1_inspect_E6", v_行动者);
    }
    public static object F_c1_visible_E6() {
        return DvValueTemplate.Instantiate("template.c1_visible_E6");
    }
    public static object F_c1_inspect_E7(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_inspect_E7", v_actor);
    }
    public static object F_c1_visible_E7() {
        return DvValueTemplate.Instantiate("template.c1_visible_E7");
    }
public static object F_c1_visible_E8() {
        return DvValueTemplate.Instantiate("template.c1_visible_E8");
    }
    public static object F_c1_inspect_E9(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_inspect_E9", v_actor);
    }
    public static object F_c1_visible_E9() {
        return DvValueTemplate.Instantiate("template.c1_visible_E9");
    }
    public static object F_c1_inspect_E10(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_inspect_E10", v_actor);
    }
    public static object F_c1_visible_E10() {
        return DvValueTemplate.Instantiate("template.c1_visible_E10");
    }
    public static object F_c1_inspect_E11(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c1_inspect_E11", v_行动者);
    }
    public static object F_c1_visible_E11() {
        return DvValueTemplate.Instantiate("template.c1_visible_E11");
    }
    public static object F_c1_inspect_E12(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c1_inspect_E12", v_行动者);
    }
    public static object F_c1_identify_own_lens(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c1_identify_own_lens", v_行动者);
    }
    public static object F_c1_visible_E12() {
        return DvValueTemplate.Instantiate("template.c1_visible_E12");
    }
    public static object F_c1_infer_rules() {
        return DvValueTemplate.Instantiate("template.c1_infer_rules");
    }
    public static object F_c1_adopt_inferences(object v_mind) {
        object v_rules__34585 = F_c1_infer_rules();
        object v_result__34619 = v_mind;
        object v_i__34642 = R.number("0");
        while (R.Bool(R.lt(v_i__34642, R.count(v_rules__34585))))
        {
            v_result__34619 = F_dv_adopt(v_result__34619, R.at(v_rules__34585, v_i__34642), "infer", Authored("m40.c1_adopt_inferences.literal"), DvValueTemplate.Instantiate("template.inline.c1_adopt_inferences.15"));
            v_i__34642 = R.add(v_i__34642, R.number("1"));
        }
        return v_result__34619;
    }
    public static object F_c1_move_name(object v_actor,object v_origin,object v_target) {
        return DvValueTemplate.Instantiate("template.c1_move_name", v_actor, v_origin, v_target);
    }
    public static object F_c1_place_label(object v_place) {
        return DvValueTemplate.Instantiate("template.c1_place_label", v_place);
    }
    public static object F_c1_move_action(object v_actor,object v_origin,object v_target) {
        return DvValueTemplate.Instantiate("template.c1_move_action", v_actor, v_origin, v_target);
    }
    public static object F_c1_add_move_methods(object v_world,object v_actor,object v_mode) {
        object v_edges__36134 = F_c1_edges();
        object v_result__36162 = v_world;
        object v_i__36186 = R.number("0");
        while (R.Bool(R.lt(v_i__36186, R.count(v_edges__36134))))
        {
            object v_edge__36239 = R.at(v_edges__36134, v_i__36186);
            object v_a__36268 = R.at(R.at(v_edges__36134, v_i__36186), R.number("0"));
            object v_b__36297 = R.at(R.at(v_edges__36134, v_i__36186), R.number("1"));
            v_result__36162 = F_dv_adopt(v_result__36162, F_campaign_method(F_c1_move_name(v_actor, v_a__36268, v_b__36297), F_c1_move(v_actor, v_a__36268, v_b__36297)), v_mode, R.number("1"), DvValueTemplate.Instantiate("template.inline.c1_add_move_methods.16"));
            v_result__36162 = F_dv_adopt(v_result__36162, F_campaign_method(F_c1_move_name(v_actor, v_b__36297, v_a__36268), F_c1_move(v_actor, v_b__36297, v_a__36268)), v_mode, R.number("1"), DvValueTemplate.Instantiate("template.inline.c1_add_move_methods.17"));
            v_i__36186 = R.add(v_i__36186, R.number("1"));
        }
        return v_result__36162;
    }
    public static object F_c1_move_actions(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_move_actions", v_actor);
    }
    public static object F_c1_statement(object v_atom) {
        return DvValueTemplate.Instantiate("template.c1_statement", v_atom);
    }
    public static object F_c1_answer_me(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_answer_me", v_actor);
    }
    public static object F_c1_hypothesis_me() {
        return DvValueTemplate.Instantiate("template.c1_hypothesis_me");
    }
    public static object F_c1_answer_V1_0(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_answer_V1_0", v_actor);
    }
    public static object F_c1_hypothesis_V1_0() {
        return DvValueTemplate.Instantiate("template.c1_hypothesis_V1_0");
    }
    public static object F_c1_answer_V1_1(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_answer_V1_1", v_actor);
    }
    public static object F_c1_hypothesis_V1_1() {
        return DvValueTemplate.Instantiate("template.c1_hypothesis_V1_1");
    }
    public static object F_c1_answer_V1_2(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_answer_V1_2", v_actor);
    }
    public static object F_c1_hypothesis_V1_2() {
        return DvValueTemplate.Instantiate("template.c1_hypothesis_V1_2");
    }
    public static object F_c1_answer_V2_0(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_answer_V2_0", v_actor);
    }
    public static object F_c1_hypothesis_V2_0() {
        return DvValueTemplate.Instantiate("template.c1_hypothesis_V2_0");
    }
    public static object F_c1_answer_V2_1(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_answer_V2_1", v_actor);
    }
    public static object F_c1_hypothesis_V2_1() {
        return DvValueTemplate.Instantiate("template.c1_hypothesis_V2_1");
    }
    public static object F_c1_answer_V2_2(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_answer_V2_2", v_actor);
    }
    public static object F_c1_hypothesis_V2_2() {
        return DvValueTemplate.Instantiate("template.c1_hypothesis_V2_2");
    }
public static object F_c1_hypothesis_V3_0() {
        return DvValueTemplate.Instantiate("template.c1_hypothesis_V3_0");
    }
public static object F_c1_hypothesis_V3_1() {
        return DvValueTemplate.Instantiate("template.c1_hypothesis_V3_1");
    }
public static object F_c1_hypothesis_V4_0() {
        return DvValueTemplate.Instantiate("template.c1_hypothesis_V4_0");
    }
public static object F_c1_hypothesis_V4_1() {
        return DvValueTemplate.Instantiate("template.c1_hypothesis_V4_1");
    }
    public static object F_c1_answer_V5_0(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_answer_V5_0", v_actor);
    }
    public static object F_c1_hypothesis_V5_0() {
        return DvValueTemplate.Instantiate("template.c1_hypothesis_V5_0");
    }
    public static object F_c1_answer_V5_1(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_answer_V5_1", v_actor);
    }
    public static object F_c1_hypothesis_V5_1() {
        return DvValueTemplate.Instantiate("template.c1_hypothesis_V5_1");
    }
    public static object F_c1_answer_V5_2(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_answer_V5_2", v_actor);
    }
    public static object F_c1_hypothesis_V5_2() {
        return DvValueTemplate.Instantiate("template.c1_hypothesis_V5_2");
    }
    public static object F_c1_answer_trust(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_answer_trust", v_actor);
    }
    public static object F_c1_hypothesis_trust() {
        return DvValueTemplate.Instantiate("template.c1_hypothesis_trust");
    }
    public static object F_c1_answer_V6_0(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_answer_V6_0", v_actor);
    }
    public static object F_c1_hypothesis_V6_0() {
        return DvValueTemplate.Instantiate("template.c1_hypothesis_V6_0");
    }
    public static object F_c1_answer_V6_1(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_answer_V6_1", v_actor);
    }
    public static object F_c1_hypothesis_V6_1() {
        return DvValueTemplate.Instantiate("template.c1_hypothesis_V6_1");
    }
    public static object F_c1_answer_actions(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_answer_actions", v_actor);
    }
    public static object F_c1_raw_hypotheses() {
        return DvValueTemplate.Instantiate("template.c1_raw_hypotheses");
    }
    public static object F_c1_hypotheses() {
        return DvValueTemplate.Instantiate("template.c1_hypotheses");
    }
    public static object F_c1_talk_slate_code(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_talk_slate_code", v_actor);
    }
    public static object F_c1_talk_slate_person(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_talk_slate_person", v_actor);
    }
public static object F_c1_talk_cups_known(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_talk_cups_known", v_actor);
    }
    public static object F_c1_talk_starter_kept(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_talk_starter_kept", v_actor);
    }
public static object F_c1_talk_recalled_names(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_talk_recalled_names", v_actor);
    }
    public static object F_c1_sole_test(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c1_sole_test", v_行动者);
    }
public static object F_c1_sound_ref(object v_field) {
        return DvValueTemplate.Instantiate("template.c1_sound_ref", v_field);
    }
public static object F_c1_sound_station(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_sound_station", v_actor);
    }
public static object F_c1_sound_words(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_sound_words", v_actor);
    }
public static object F_c1_sound_pending() {
        return DvValueTemplate.Instantiate("template.c1_sound_pending");
    }
public static object F_c1_sound_ready(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_sound_ready", v_actor);
    }
public static object F_c1_sound_withdraw(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_sound_withdraw", v_actor);
    }
public static object F_c1_sound_ready_reply(object v_participant,object v_coordinator) {
        return DvValueTemplate.Instantiate("template.c1_sound_ready_reply", v_participant, v_coordinator);
    }
public static object F_c1_sound_execution_condition(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_sound_execution_condition", v_actor);
    }
public static object F_c1_sound_action(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_sound_action", v_actor);
    }
public static object F_c1_sound_report_listener(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_sound_report_listener", v_actor);
    }
public static object F_c1_loss_testimony(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_loss_testimony", v_actor);
    }
    public static object F_c1_latch_contact_ref() {
        return DvValueTemplate.Instantiate("template.c1_latch_contact_ref");
    }
    public static object F_c1_latch_contact_source() {
        return DvValueTemplate.Instantiate("template.c1_latch_contact_source");
    }
    public static object F_c1_latch_contact_law() {
        return DvValueTemplate.Instantiate("template.c1_latch_contact_law");
    }
    public static object F_c1_latch_contact_method(object v_world,object v_actor,object v_mode) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_latch_contact_method.46", v_actor)))
        {
            return v_world;
        }
        return F_dv_adopt(v_world, F_campaign_method(Authored("m40.c1_latch_contact_method.literal"), F_c1_latch_contact_law()), v_mode, R.number("1"), F_dv_single(F_c1_latch_contact_source()));
    }
    public static object F_c1_latch_contact_action() {
        return DvValueTemplate.Instantiate("template.c1_latch_contact_action");
    }
public static object F_c1_raw_question_object() {
        return DvValueTemplate.Instantiate("template.c1_raw_question_object");
    }
    public static object F_c1_truth_contract(object v_subject) {
        return DvValueTemplate.Instantiate("template.c1_truth_contract", v_subject);
    }
    public static object F_c1_register_speech_contract(object v_world) {
        object v_basis__132827 = DvValueTemplate.Instantiate("template.inline.c1_register_speech_contract.52");
        object v_result__133108 = F_dv_adopt(v_world, F_campaign_method(Authored("m40.c1_register_speech_contract.literal_0003"), F_c1_truth_contract(Authored("m40.c1_observe_as.literal_0005"))), "enforce", R.number("1"), DvValueTemplate.Instantiate("template.inline.c1_register_speech_contract.53"));
        return DvValueTemplate.Instantiate("template.inline.c1_register_speech_contract.54", v_result__133108, v_basis__132827);
    }
    public static object F_c1_handling_basis() {
        return DvValueTemplate.Instantiate("template.c1_handling_basis");
    }
    public static object F_c1_question_object() {
        object v_world__134510 = F_dv_add_object(F_dv_world(Authored("m40.c1_question_object.literal"), "physical", ""), F_c1_raw_question_object());
        v_world__134510 = F_dv_store(v_world__134510, F_dv_ref(Authored("post.c1_question_object.0001"), "state", Authored("m40.c1_question_object.literal_0002")), F_dv_with(F_dv_field(true), "basis", F_c1_handling_basis()));
        v_world__134510 = F_dv_store(v_world__134510, F_dv_ref(Authored("post.c1_question_object.0001"), "state", Authored("m40.c1_question_object.literal_0003")), F_dv_with(F_dv_field(true), "basis", F_c1_handling_basis()));
        v_world__134510 = F_dv_store(v_world__134510, F_dv_ref(Authored("post.c1_question_object.0001"), "state", Authored("m40.c1_question_object.literal_0004")), F_dv_field(false));
        v_world__134510 = F_dv_store(v_world__134510, F_dv_ref(Authored("post.c1_question_object.0001"), "state", Authored("m40.c1_question_object.literal_0005")), F_dv_field(R.number("0")));
        v_world__134510 = F_dv_store(v_world__134510, F_dv_ref(Authored("post.c1_question_object.0001"), "state", Authored("m40.c1_question_object.literal_0006")), F_dv_field(R.number("0")));
        v_world__134510 = F_dv_store(v_world__134510, F_dv_ref(Authored("post.c1_question_object.0001"), "state", Authored("m40.c1_question_object.literal_0007")), F_dv_field(R.number("0")));
        v_world__134510 = F_dv_store(v_world__134510, F_dv_ref(Authored("post.c1_question_object.0001"), "state", Authored("m40.c1_question_object.literal_0008")), F_dv_field(R.number("0")));
        v_world__134510 = F_dv_store(v_world__134510, F_dv_ref(Authored("post.c1_question_object.0001"), "state", Authored("m40.c1_question_object.literal_0009")), F_dv_field(false));
        v_world__134510 = F_dv_store(v_world__134510, F_dv_ref(Authored("post.c1_question_object.0001"), "state", Authored("m40.c1_question_object.literal_0010")), F_dv_field(false));
        v_world__134510 = F_dv_store(v_world__134510, F_dv_ref(Authored("post.c1_question_object.0001"), "state", Authored("m40.c1_question_object.literal_0011")), F_dv_field(false));
        v_world__134510 = F_dv_store(v_world__134510, F_dv_ref(Authored("post.c1_question_object.0001"), "state", Authored("m40.c1_question_object.literal_0012")), F_dv_field(false));
        return F_dv_get(F_dv_get(v_world__134510, "objects"), "question");
    }
    public static object F_c1_original_question_open(object v_question) {
        return DvValueTemplate.Instantiate("template.c1_original_question_open", v_question);
    }
    public static object F_c1_reask_question(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_reask_question", v_actor);
    }
    public static object F_c1_room_objects() {
        return DvValueTemplate.Instantiate("template.c1_room_objects");
    }
    public static object F_c1_add_objects(object v_world,object v_objects) {
        object v_result__139701 = v_world;
        object v_i__139725 = R.number("0");
        while (R.Bool(R.lt(v_i__139725, R.count(v_objects))))
        {
            v_result__139701 = F_dv_add_object(v_result__139701, R.at(v_objects, v_i__139725));
            v_i__139725 = R.add(v_i__139725, R.number("1"));
        }
        return v_result__139701;
    }
    public static object F_c1_physical_world() {
        object v_world__139895 = F_dv_with(F_dv_world(Authored("m40.c1_physical_world.literal"), "physical", ""), "time", Authored("m40.c1_physical_world.literal_0002"));
        object v_people__140099 = AuthoredCopy("m40.c1_sound_invite.literal_0006");
        object v_i__140204 = R.number("0");
        v_world__139895 = F_c1_add_objects(v_world__139895, F_c1_room_objects());
        v_world__139895 = F_dv_add_object(v_world__139895, F_c1_physical_cognition());
        v_world__139895 = F_dv_add_object(v_world__139895, F_c1_question_object());
        v_world__139895 = F_c1_add_objects(v_world__139895, R.content("groups", Authored("m40.c1_physical_world.literal_0003")));
        v_world__139895 = F_dv_add_object(v_world__139895, F_campaign_state_object(Authored("m40.c1_query_basis.literal_0012"), DvValueTemplate.Instantiate("template.inline.c1_physical_world.56")));
        v_world__139895 = F_dv_store(v_world__139895, F_c1_latch_contact_ref(), F_dv_field(false));
        while (R.Bool(R.lt(v_i__140204, R.count(v_people__140099))))
        {
            object v_actor__140867 = R.at(v_people__140099, v_i__140204);
            v_world__139895 = F_dv_add_object(v_world__139895, F_c1_position(v_actor__140867, Authored("m40.c1_shallow_question_action.literal_0004")));
            v_world__139895 = F_c1_add_move_methods(v_world__139895, v_actor__140867, "enforce");
            v_world__139895 = F_c1_evidence_contact_methods(F_c1_latch_contact_method(v_world__139895, v_actor__140867, "enforce"), v_actor__140867, "enforce");
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_physical_world.57", v_actor__140867)))
            {
                v_world__139895 = F_dv_store(v_world__139895, F_c1_deep_resonance_ref(v_actor__140867), F_dv_field(false));
            }
            v_i__140204 = R.add(v_i__140204, R.number("1"));
        }
        v_world__139895 = F_dv_adopt(v_world__139895, F_campaign_method(Authored("m40.c1_physical_world.literal_0005"), F_c1_opening_emit(Authored("post.c1_physical_world.0001"))), "enforce", R.number("1"), DvValueTemplate.Instantiate("template.inline.c1_physical_world.58"));
        v_world__139895 = F_dv_adopt(v_world__139895, F_campaign_method(Authored("m40.c1_physical_world.literal_0008"), F_c1_question_emit(Authored("post.c1_physical_world.0001"))), "enforce", R.number("1"), DvValueTemplate.Instantiate("template.inline.c1_physical_world.59"));
        v_world__139895 = F_dv_adopt(v_world__139895, F_campaign_method(Authored("m40.c1_physical_world.literal_0011"), F_c1_original_question_open(Authored("post.c1_physical_world.0001"))), "enforce", R.number("1"), DvValueTemplate.Instantiate("template.inline.c1_physical_world.60"));
        v_world__139895 = F_c1_adopt_protocol(v_world__139895);
        return F_c1_register_speech_contract(v_world__139895);
    }
    public static object F_c1_subjective_world(object v_actor) {
        object v_mind__143028 = F_dv_with(F_dv_world(R.cat(Authored("m40.c1_subjective_world.literal"), v_actor), "mind", v_actor), "time", Authored("m40.c1_physical_world.literal_0002"));
        v_mind__143028 = F_dv_add_object(v_mind__143028, F_c1_position(v_actor, Authored("m40.c1_shallow_question_action.literal_0004")));
        v_mind__143028 = F_dv_store(v_mind__143028, F_dv_ref(v_actor, "state", Authored("m40.c1_answer_topic.return")), F_dv_field(v_actor));
        v_mind__143028 = F_dv_add_object(v_mind__143028, F_campaign_local_object(Authored("m40.c1_physical_cognition.literal")));
        v_mind__143028 = F_dv_add_object(v_mind__143028, F_campaign_local_object(Authored("m40.c1_query_basis.literal_0012")));
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_subjective_world.61", v_actor)))
        {
            v_mind__143028 = F_dv_store(v_mind__143028, F_c1_deep_resonance_ref(v_actor), F_dv_with(F_dv_field(false), "basis", new List<object> { F_c1_deep_resonance_source(v_actor) }));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_subjective_world.62", v_actor)))
        {
            v_mind__143028 = F_dv_store(v_mind__143028, F_c1_latch_contact_ref(), DvValueTemplate.Instantiate("template.inline.c1_subjective_world.63"));
        }
        v_mind__143028 = F_dv_add_object(v_mind__143028, F_campaign_local_object(Authored("post.c1_subjective_world.0001")));
        v_mind__143028 = F_dv_add_object(v_mind__143028, F_c1_memory_object(v_actor));
        v_mind__143028 = F_dv_add_object(v_mind__143028, F_campaign_local_object(Authored("m40.c1_inspect_E10.literal_0009")));
        v_mind__143028 = F_dv_add_object(v_mind__143028, F_campaign_local_object(Authored("m40.c1_inspect_E10.literal_0011")));
        v_mind__143028 = F_dv_add_object(v_mind__143028, F_campaign_local_object(Authored("m40.c1_inspect_E10.literal_0012")));
        v_mind__143028 = F_dv_add_object(v_mind__143028, F_campaign_local_object(Authored("m40.c1_inspect_E10.literal_0013")));
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_subjective_world.64", v_actor)))
        {
            v_mind__143028 = F_dv_store(v_mind__143028, F_dv_ref(Authored("m40.c1_inspect_E10.literal_0009"), Authored("m40.c1_answer_topic.return"), "owner"), DvValueTemplate.Instantiate("template.inline.c1_subjective_world.65"));
        }
        v_mind__143028 = F_c1_adopt_inferences(v_mind__143028);
        return F_c1_evidence_contact_methods(F_c1_latch_contact_method(F_c1_add_move_methods(v_mind__143028, v_actor, "predict"), v_actor, "predict"), v_actor, "predict");
    }
    public static object F_c1_player_policy() {
        return DvValueTemplate.Instantiate("template.c1_player_policy");
    }
    public static object F_c1_public_goal(object v_question,object v_atom,object v_kind) {
        return DvValueTemplate.Instantiate("template.c1_public_goal", v_question, v_atom, v_kind);
    }
    public static object F_c1_public_question_receipt_goal(object v_stage,object v_line,object v_source_id) {
        return DvValueTemplate.Instantiate("template.c1_public_question_receipt_goal", v_stage, v_line, v_source_id);
    }
    public static object F_c1_public_goals() {
        return DvValueTemplate.Instantiate("template.c1_public_goals");
    }
    public static object F_c1_memory_source(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_memory_source", v_actor);
    }
    public static object F_c1_memory_object(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_memory_object", v_actor);
    }
    public static object F_c1_pragmatic_policy(object v_policy,object v_scope_check) {
        return DvValueTemplate.Instantiate("template.c1_pragmatic_policy", v_policy, v_scope_check);
    }
    public static object F_c1_raw_person_policy(object v_id) {
        return DvValueTemplate.Instantiate("template.c1_raw_person_policy", v_id);
    }
    public static object F_c1_personal_goal(object v_atom,object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_personal_goal", v_atom, v_actor);
    }
    public static object F_c1_personal_goals(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_personal_goals", v_actor);
    }
    public static object F_c1_professional_reply(object v_actor,object v_atom,object v_surface,object v_source) {
        return DvValueTemplate.Instantiate("template.c1_professional_reply", v_actor, v_atom, v_surface, v_source);
    }
    public static object F_c1_professional_replies(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_professional_replies", v_actor);
    }
    public static object F_c1_requested_replies(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_requested_replies", v_actor);
    }
    public static object F_c1_base_person_policy(object v_id) {
        return DvValueTemplate.Instantiate("template.c1_base_person_policy", v_id);
    }
    public static object F_c1_investigation_actions(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_investigation_actions", v_actor);
    }
    public static object F_c1_character(object v_id,object v_dossier) {
        return F_dv_with(F_dv_with(F_dv_with(F_dv_with(F_dv_with(new List<object> { F_dv_pair("id", v_id), F_dv_pair("mind", F_c1_subjective_world(v_id)), F_dv_pair("actions", F_c1_investigation_actions(v_id)), F_dv_pair("policy", F_c1_person_policy(v_id)) }, Authored("m40.c1_character.literal"), AuthoredCopy("m40.c1_character.literal_0002")), "location_ref", F_dv_ref(v_id, "state", "location")), "location", Authored("m40.c1_shallow_question_action.literal_0004")), Authored("m40.c1_character.literal_0003"), F_dv_ref(Authored("post.c1_character.0001"), "state", Authored("m40.c1_reask_question.literal_0002"))), Authored("m40.c1_character.literal_0004"), v_dossier);
    }
    public static object F_c1_character_detective() {
        return F_c1_character(Authored("m40.c1_talk_recalled_names.literal_0006"), Authored("m40.c1_character_detective.literal"));
    }
    public static object F_c1_character_huo() {
        return F_c1_character(Authored("m40.c1_inspect_E7.literal"), Authored("m40.c1_character_huo.literal"));
    }
    public static object F_c1_character_shen() {
        return F_c1_character(Authored("m40.c1_inspect_E9.literal"), Authored("m40.c1_character_shen.literal"));
    }
    public static object F_c1_character_su() {
        return F_c1_character(Authored("m40.c1_inspect_E5.literal"), Authored("m40.c1_character_su.literal"));
    }
    public static object F_c1_current_question(object v_stage) {
        return DvValueTemplate.Instantiate("template.c1_current_question", v_stage);
    }
    public static object F_c1_identity_complete() {
        return DvValueTemplate.Instantiate("template.c1_identity_complete");
    }
    public static object F_c1_voice_complete() {
        return DvValueTemplate.Instantiate("template.c1_voice_complete");
    }
    public static object F_c1_words_complete() {
        return DvValueTemplate.Instantiate("template.c1_words_complete");
    }
    public static object F_c1_trust_complete() {
        return DvValueTemplate.Instantiate("template.c1_trust_complete");
    }
    public static object F_c1_complete_reply(object v_id,object v_literal,object v_support,object v_topic,object v_complete,object v_law,object v_source) {
        return DvValueTemplate.Instantiate("template.c1_complete_reply", v_id, v_literal, v_support, v_topic, v_complete, v_law, v_source);
    }
    public static object F_c1_unasked_reply(object v_id,object v_claim,object v_source) {
        return DvValueTemplate.Instantiate("template.c1_unasked_reply", v_id, v_claim, v_source);
    }
    public static object F_c1_formal_me() {
        return DvValueTemplate.Instantiate("template.c1_formal_me");
    }
    public static object F_c1_formal_V1_0() {
        return DvValueTemplate.Instantiate("template.c1_formal_V1_0");
    }
    public static object F_c1_formal_V1_1() {
        return DvValueTemplate.Instantiate("template.c1_formal_V1_1");
    }
    public static object F_c1_formal_V1_2() {
        return DvValueTemplate.Instantiate("template.c1_formal_V1_2");
    }
    public static object F_c1_formal_V2_0() {
        return DvValueTemplate.Instantiate("template.c1_formal_V2_0");
    }
    public static object F_c1_formal_V2_1() {
        return DvValueTemplate.Instantiate("template.c1_formal_V2_1");
    }
    public static object F_c1_formal_V2_2() {
        return DvValueTemplate.Instantiate("template.c1_formal_V2_2");
    }
    public static object F_c1_formal_V3_0() {
        return DvValueTemplate.Instantiate("template.c1_formal_V3_0");
    }
    public static object F_c1_formal_V3_1() {
        return DvValueTemplate.Instantiate("template.c1_formal_V3_1");
    }
    public static object F_c1_formal_V4_0() {
        return DvValueTemplate.Instantiate("template.c1_formal_V4_0");
    }
    public static object F_c1_formal_V4_1() {
        return DvValueTemplate.Instantiate("template.c1_formal_V4_1");
    }
    public static object F_c1_formal_V5_0() {
        return DvValueTemplate.Instantiate("template.c1_formal_V5_0");
    }
    public static object F_c1_formal_V5_1() {
        return DvValueTemplate.Instantiate("template.c1_formal_V5_1");
    }
    public static object F_c1_formal_V5_2() {
        return DvValueTemplate.Instantiate("template.c1_formal_V5_2");
    }
    public static object F_c1_formal_trust() {
        return DvValueTemplate.Instantiate("template.c1_formal_trust");
    }
    public static object F_c1_formal_V6_0() {
        return DvValueTemplate.Instantiate("template.c1_formal_V6_0");
    }
    public static object F_c1_formal_V6_1() {
        return DvValueTemplate.Instantiate("template.c1_formal_V6_1");
    }
    public static object F_c1_formal_answers() {
        return DvValueTemplate.Instantiate("template.c1_formal_answers");
    }
    public static object F_c1_adopt_protocol(object v_world) {
        object v_result__182145 = v_world;
        object v_stages__182169 = AuthoredCopy("m40.c1_adopt_protocol.stages");
        object v_i__182270 = R.number("0");
        while (R.Bool(R.lt(v_i__182270, R.count(v_stages__182169))))
        {
            object v_stage__182324 = R.at(v_stages__182169, v_i__182270);
            v_result__182145 = F_dv_adopt(v_result__182145, F_campaign_method(R.cat(Authored("m40.c1_adopt_protocol.literal"), v_stage__182324), F_c1_question_accept(Authored("post.c1_adopt_protocol.0001"), v_stage__182324)), "enforce", R.number("1"), DvValueTemplate.Instantiate("template.inline.c1_adopt_protocol.75"));
            v_i__182270 = R.add(v_i__182270, R.number("1"));
        }
        return v_result__182145;
    }
    public static object F_c1_speech(object v_literal,object v_surface,object v_scope) {
        return DvValueTemplate.Instantiate("template.c1_speech", v_literal, v_surface, v_scope);
    }
    public static object F_c1_raw_opening_action() {
        return DvValueTemplate.Instantiate("template.c1_raw_opening_action");
    }
    public static object F_c1_opening_action() {
        return DvValueTemplate.Instantiate("template.c1_opening_action");
    }
    public static object F_c1_opening_memory_fragment() {
        return DvValueTemplate.Instantiate("template.c1_opening_memory_fragment");
    }
    public static object F_c1_opening_illegal_fragment() {
        return DvValueTemplate.Instantiate("template.c1_opening_illegal_fragment");
    }
    public static object F_c1_demon_listener_models() {
        object v_listeners__186955 = AuthoredCopy("m40.c1_sound_agreement.literal_0006");
        object v_result__187066 = new List<object> {  };
        object v_index__187087 = R.number("0");
        object v_basis__187106 = F_c1_handling_basis();
        while (R.Bool(R.lt(v_index__187087, R.count(v_listeners__186955))))
        {
            _ = R.push(v_result__187066, F_dv_pair(R.at(v_listeners__186955, v_index__187087), F_c1_opening_listener_model(R.at(v_listeners__186955, v_index__187087), false, v_basis__187106)));
            v_index__187087 = R.add(v_index__187087, R.number("1"));
        }
        return v_result__187066;
    }
    public static object F_c1_demon_policy() {
        return DvValueTemplate.Instantiate("template.c1_demon_policy");
    }
    public static object F_c1_raw_question_action(object v_stage,object v_surface,object v_first) {
        return DvValueTemplate.Instantiate("template.c1_raw_question_action", v_stage, v_surface, v_first);
    }
    public static object F_c1_question_action(object v_stage,object v_surface,object v_first) {
        return DvValueTemplate.Instantiate("template.c1_question_action", v_stage, v_surface, v_first);
    }
    public static object F_c1_character_demon() {
        object v_mind__191401 = F_dv_with(F_dv_world(Authored("m40.c1_character_demon.literal"), "mind", Authored("m40.c1_observe_as.literal_0005")), "time", Authored("m40.c1_physical_world.literal_0002"));
        v_mind__191401 = F_dv_add_object(v_mind__191401, F_c1_position(Authored("m40.c1_observe_as.literal_0005"), Authored("m40.c1_shallow_question_action.literal_0004")));
        v_mind__191401 = F_dv_add_object(v_mind__191401, F_c1_question_object());
        v_mind__191401 = F_dv_with(v_mind__191401, Authored("m40.c1_character_demon.literal_0002"), F_c1_demon_listener_models());
        return DvValueTemplate.Instantiate("template.inline.c1_character_demon.76", v_mind__191401);
    }
    public static object F_c1_pacing_source() {
        return DvValueTemplate.Instantiate("template.c1_pacing_source");
    }
    public static object F_c1_costs() {
        return DvValueTemplate.Instantiate("template.c1_costs");
    }
    public static object F_c1_bindings() {
        object v_people__197895 = AuthoredCopy("m40.c1_bindings.people");
        object v_objects__197990 = AuthoredCopy("m40.c1_bindings.objects");
        object v_result__198087 = new List<object> {  };
        object v_i__198108 = R.number("0");
        object v_j__198123 = R.number("0");
        while (R.Bool(R.lt(v_i__198108, R.count(v_people__197895))))
        {
            v_j__198123 = R.number("0");
            while (R.Bool(R.lt(v_j__198123, R.count(v_objects__197990))))
            {
                _ = R.push(v_result__198087, F_dv_binding(R.at(v_objects__197990, v_j__198123), R.at(v_objects__197990, v_j__198123), R.at(v_people__197895, v_i__198108)));
                v_j__198123 = R.add(v_j__198123, R.number("1"));
            }
            v_i__198108 = R.add(v_i__198108, R.number("1"));
        }
        return v_result__198087;
    }
    public static object F_c1_binding_gaps() {
        return DvValueTemplate.Instantiate("template.c1_binding_gaps");
    }
    public static object F_c1_scenario_native() {
        return F_dv_with(F_dv_with(F_dv_with(F_dv_with(new List<object> { DvValueTemplate.Instantiate("template.inline.c1_scenario_native.77"), DvValueTemplate.Instantiate("template.inline.c1_scenario_native.78"), F_dv_pair("physical", F_c1_physical_world()), F_dv_pair("actors", new List<object> { F_c1_character_demon(), F_c1_character_detective(), F_c1_character_huo(), F_c1_character_shen(), F_c1_character_su() }) }, "bindings", F_c1_bindings()), "cost_model", F_c1_costs()), "formal_answers", F_c1_formal_answers()), Authored("m40.c1_scenario_native.literal_0003"), Authored("m40.c1_scenario_native.literal_0004"));
    }
    public static object F_c1_variant_weight(object v_weight,object v_id) {
        object v_data__199161 = F_dvs_scenario_base();
        object v_actors__199197 = F_dv_get(v_data__199161, "actors");
        object v_result__199227 = new List<object> {  };
        object v_i__199248 = R.number("0");
        while (R.Bool(R.lt(v_i__199248, R.count(v_actors__199197))))
        {
            object v_actor__199302 = R.at(v_actors__199197, v_i__199248);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_variant_weight.79", v_actor__199302)))
            {
                v_actor__199302 = DvValueTemplate.Instantiate("template.inline.c1_variant_weight.80", v_actor__199302, v_weight);
            }
            _ = R.push(v_result__199227, v_actor__199302);
            v_i__199248 = R.add(v_i__199248, R.number("1"));
        }
        return F_dv_with(F_dv_with(v_data__199161, "actors", v_result__199227), "id", v_id);
    }
    public static object F_dvs_scenario_low() {
        return F_c1_variant_weight(Authored("m40.dvs_scenario_low.literal"), Authored("m40.dvs_scenario_low.literal_0002"));
    }
    public static object F_dvs_scenario_holdout() {
        return F_c1_variant_weight(Authored("m40.dvs_scenario_holdout.literal"), Authored("m40.dvs_scenario_holdout.literal_0002"));
    }
    public static object F_dvs_scenario_boundary() {
        object v_data__200142 = F_dvs_scenario_base();
        object v_actors__200178 = F_dv_get(v_data__200142, "actors");
        object v_result__200208 = new List<object> {  };
        object v_i__200229 = R.number("0");
        object v_position__200244 = F_dv_ref(Authored("m40.c1_inspect_E5.literal"), "state", "location");
        object v_basis__200358 = DvValueTemplate.Instantiate("template.inline.dvs_scenario_boundary.81");
        while (R.Bool(R.lt(v_i__200229, R.count(v_actors__200178))))
        {
            object v_actor__200498 = R.at(v_actors__200178, v_i__200229);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvs_scenario_boundary.82", v_actor__200498)))
            {
                v_actor__200498 = F_dv_with(v_actor__200498, "mind", F_dv_store(F_dv_get(v_actor__200498, "mind"), v_position__200244, DvValueTemplate.Instantiate("template.inline.dvs_scenario_boundary.83", v_actor__200498, v_position__200244, v_basis__200358)));
            }
            _ = R.push(v_result__200208, v_actor__200498);
            v_i__200229 = R.add(v_i__200229, R.number("1"));
        }
        return F_dv_with(F_dv_with(F_dv_with(v_data__200142, "actors", v_result__200208), "physical", F_dv_store(F_dv_get(v_data__200142, "physical"), v_position__200244, DvValueTemplate.Instantiate("template.inline.dvs_scenario_boundary.84", v_data__200142, v_position__200244, v_basis__200358))), "id", Authored("m40.dvs_scenario_boundary.literal_0002"));
    }
    public static object F_c1_report_labels() {
        return DvValueTemplate.Instantiate("template.c1_report_labels");
    }
    public static object F_c1_active_configuration(object v_data) {
        return DvValueTemplate.Instantiate("template.c1_active_configuration", v_data);
    }
    public static object F_dvs_scenario_base() {
        return F_c1_active_configuration(F_c1_scenario_native());
    }
    public static object F_c1_question_text(object v_stage) {
        return DvValueTemplate.Instantiate("template.c1_question_text", v_stage);
    }
    public static object F_c1_person_policy(object v_id) {
        object v_policy__206851 = F_c1_base_person_policy(v_id);
        object v_bindings__206895 = F_dv_get(v_policy__206851, "topic_bindings");
        object v_stages__206937 = AuthoredCopy("m40.c1_adopt_protocol.stages");
        object v_i__207038 = R.number("0");
        while (R.Bool(R.lt(v_i__207038, R.count(v_stages__206937))))
        {
            object v_stage__207092 = R.at(v_stages__206937, v_i__207038);
            _ = R.push(v_bindings__206895, DvValueTemplate.Instantiate("template.inline.c1_person_policy.85", v_stage__207092));
            v_i__207038 = R.add(v_i__207038, R.number("1"));
        }
        object v_updated__207628 = F_dv_with(v_policy__206851, "topic_bindings", v_bindings__206895);
        object v_palm_goals__207691 = F_dv_find(v_updated__207628, "goals", new List<object> {  });
        _ = R.push(v_palm_goals__207691, DvValueTemplate.Instantiate("template.inline.c1_person_policy.86", v_id));
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_person_policy.87", v_id)))
        {
            _ = R.push(v_palm_goals__207691, DvValueTemplate.Instantiate("template.inline.c1_person_policy.88"));
            object v_rooms__208957 = AuthoredCopy("m40.c1_investigation_actions.rooms");
            object v_invitation_room__209067 = R.number("0");
            while (R.Bool(R.lt(v_invitation_room__209067, R.count(v_rooms__208957))))
            {
                _ = R.push(v_palm_goals__207691, DvValueTemplate.Instantiate("template.inline.c1_person_policy.89", v_rooms__208957, v_invitation_room__209067));
                v_invitation_room__209067 = R.add(v_invitation_room__209067, R.number("1"));
            }
        }
        v_updated__207628 = F_dv_with(v_updated__207628, "goals", v_palm_goals__207691);
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_person_policy.90", v_id)))
        {
            object v_goals__209871 = F_dv_find(v_updated__207628, "goals", new List<object> {  });
            _ = R.push(v_goals__209871, DvValueTemplate.Instantiate("template.inline.c1_person_policy.91", v_id));
            object v_keys__210160 = AuthoredCopy("m40.c1_investigation_actions.keys");
            object v_key_index__210268 = R.number("0");
            while (R.Bool(R.lt(v_key_index__210268, R.count(v_keys__210160))))
            {
                _ = R.push(v_goals__209871, DvValueTemplate.Instantiate("template.inline.c1_person_policy.92", v_id, v_keys__210160, v_key_index__210268));
                v_key_index__210268 = R.add(v_key_index__210268, R.number("1"));
            }
            v_updated__207628 = F_dv_with(v_updated__207628, "goals", v_goals__209871);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_person_policy.93", v_id)))
        {
            object v_ask__210903 = F_c1_recall_advice_question();
            v_updated__207628 = DvValueTemplate.Instantiate("template.inline.c1_person_policy.94", v_updated__207628, v_ask__210903);
            object v_responses__211283 = F_c1_recall_advice_responses();
            object v_response_index__211337 = R.number("0");
            while (R.Bool(R.lt(v_response_index__211337, R.count(v_responses__211283))))
            {
                object v_response__211426 = R.at(v_responses__211283, v_response_index__211337);
                v_updated__207628 = DvValueTemplate.Instantiate("template.inline.c1_person_policy.95", v_updated__207628, v_response__211426);
                v_response_index__211337 = R.add(v_response_index__211337, R.number("1"));
            }
        }
        return v_updated__207628;
    }
    public static object F_c1_evidence_contact_ref(object v_actor,object v_evidence) {
        return DvValueTemplate.Instantiate("template.c1_evidence_contact_ref", v_actor, v_evidence);
    }
    public static object F_c1_evidence_contact_premise(object v_actor,object v_evidence) {
        return DvValueTemplate.Instantiate("template.c1_evidence_contact_premise", v_actor, v_evidence);
    }
    public static object F_c1_evidence_contact(object v_行动者,object v_evidence) {
        return DvValueTemplate.Instantiate("template.c1_evidence_contact", v_行动者, v_evidence);
    }
    public static object F_c1_evidence_contact_methods(object v_world,object v_actor,object v_mode) {
        object v_result__213430 = v_world;
        object v_evidence__213454 = AuthoredCopy("m40.c1_evidence_contact_methods.evidence");
        object v_i__213569 = R.number("0");
        while (R.Bool(R.lt(v_i__213569, R.count(v_evidence__213454))))
        {
            object v_name__213623 = R.cat(R.cat(v_actor, Authored("m40.c1_evidence_contact_methods.literal")), R.at(v_evidence__213454, v_i__213569));
            v_result__213430 = F_dv_adopt(v_result__213430, F_campaign_method(v_name__213623, F_c1_evidence_contact(v_actor, R.at(v_evidence__213454, v_i__213569))), v_mode, R.number("1"), DvValueTemplate.Instantiate("template.inline.c1_evidence_contact_methods.96"));
            v_i__213569 = R.add(v_i__213569, R.number("1"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_evidence_contact_methods.97", v_actor)))
        {
            object v_rooms__214163 = AuthoredCopy("m40.c1_investigation_actions.rooms");
            object v_invitation_room__214273 = R.number("0");
            while (R.Bool(R.lt(v_invitation_room__214273, R.count(v_rooms__214163))))
            {
                v_result__213430 = F_dv_adopt(v_result__213430, F_campaign_method(R.cat(Authored("m40.c1_person_policy.literal_0005"), R.at(v_rooms__214163, v_invitation_room__214273)), F_c1_palm_invitation_law(R.at(v_rooms__214163, v_invitation_room__214273))), v_mode, R.number("1"), DvValueTemplate.Instantiate("template.inline.c1_evidence_contact_methods.98"));
                v_invitation_room__214273 = R.add(v_invitation_room__214273, R.number("1"));
            }
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_evidence_contact_methods.99", v_actor)))
        {
            v_result__213430 = F_dv_adopt(v_result__213430, F_campaign_method(R.cat(v_actor, Authored("m40.c1_evidence_contact_methods.literal_0004")), F_c1_deep_resonance_law(v_actor)), v_mode, R.number("1"), new List<object> { F_c1_deep_resonance_source(v_actor) });
        }
        return v_result__213430;
    }
    public static object F_c1_compare_threshold_action(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_compare_threshold_action", v_actor);
    }
    public static object F_c1_compare_palms_action(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_compare_palms_action", v_actor);
    }
    public static object F_c1_deep_resonance_site(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_deep_resonance_site", v_actor);
    }
    public static object F_c1_deep_resonance_source(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_deep_resonance_source", v_actor);
    }
    public static object F_c1_deep_resonance_ref(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_deep_resonance_ref", v_actor);
    }
    public static object F_c1_recall_advice_question() {
        return DvValueTemplate.Instantiate("template.c1_recall_advice_question");
    }
    public static object F_c1_recall_advice_actions() {
        return DvValueTemplate.Instantiate("template.c1_recall_advice_actions");
    }
    public static object F_c1_recall_advice_responses() {
        return DvValueTemplate.Instantiate("template.c1_recall_advice_responses");
    }
    public static object F_c1_deep_resonance_law(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c1_deep_resonance_law", v_行动者);
    }
    public static object F_c1_deep_resonance_action(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_deep_resonance_action", v_actor);
    }
    public static object F_c1_public_answer_speech(object v_action) {
        return DvValueTemplate.Instantiate("template.c1_public_answer_speech", v_action);
    }
    public static object F_c1_answer_conflict_response(object v_actor,object v_key) {
        return DvValueTemplate.Instantiate("template.c1_answer_conflict_response", v_actor, v_key);
    }
    public static object F_c1_palm_invitation_ref() {
        return DvValueTemplate.Instantiate("template.c1_palm_invitation_ref");
    }
    public static object F_c1_palm_invitation_law(object v_room) {
        return DvValueTemplate.Instantiate("template.c1_palm_invitation_law", v_room);
    }
    public static object F_c1_palm_invitation_action(object v_room) {
        return DvValueTemplate.Instantiate("template.c1_palm_invitation_action", v_room);
    }
    public static object F_c1_infer_0(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_0", v_knowledge);
    }
    public static object F_c1_infer_1(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_1", v_knowledge);
    }
    public static object F_c1_infer_2(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_2", v_knowledge);
    }
    public static object F_c1_infer_3(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_3", v_knowledge);
    }
    public static object F_c1_infer_4(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_4", v_knowledge);
    }
    public static object F_c1_infer_5(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_5", v_knowledge);
    }
    public static object F_c1_infer_6(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_6", v_knowledge);
    }
    public static object F_c1_infer_7(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_7", v_knowledge);
    }
    public static object F_c1_infer_8(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_8", v_knowledge);
    }
    public static object F_c1_infer_9(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_9", v_knowledge);
    }
    public static object F_c1_infer_10(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_10", v_knowledge);
    }
    public static object F_c1_infer_11(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_11", v_knowledge);
    }
    public static object F_c1_infer_12(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_12", v_knowledge);
    }
    public static object F_c1_infer_13(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_13", v_knowledge);
    }
    public static object F_c1_infer_14(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_14", v_knowledge);
    }
    public static object F_c1_infer_15(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_15", v_knowledge);
    }
    public static object F_c1_infer_16(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_16", v_knowledge);
    }
    public static object F_c1_infer_17(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_17", v_knowledge);
    }
    public static object F_c1_infer_18(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_18", v_knowledge);
    }
    public static object F_c1_infer_19(object v_knowledge) {
        return DvValueTemplate.Instantiate("template.c1_infer_19", v_knowledge);
    }
    public static object F_c1_move(object v_行动者,object v_起点,object v_目标) {
        return DvValueTemplate.Instantiate("template.c1_move", v_行动者, v_起点, v_目标);
    }
    public static object F_c1_opening_emit(object v_question) {
        return DvValueTemplate.Instantiate("template.c1_opening_emit", v_question);
    }
    public static object F_c1_question_emit(object v_question) {
        return DvValueTemplate.Instantiate("template.c1_question_emit", v_question);
    }
    public static object F_c1_question_accept(object v_question,object v_stage) {
        return DvValueTemplate.Instantiate("template.c1_question_accept", v_question, v_stage);
    }
    public static object F_c1_meeting(object v_requester,object v_speaker,object v_room) {
        return DvValueTemplate.Instantiate("template.c1_meeting", v_requester, v_speaker, v_room);
    }
    public static object F_c1_short_stay_rule(object v_knowledge,object v_question,object v_memory,object v_basis) {
        return DvValueTemplate.Instantiate("template.c1_short_stay_rule", v_knowledge, v_question, v_memory, v_basis);
    }
    public static object F_c1_opening_listener_model(object v_listener,object v_scope_check_estimate,object v_basis) {
        object v_mind__252526 = F_dv_world(R.cat(Authored("m40.c1_opening_listener_model.literal"), v_listener), "mind", v_listener);
        object v_assumption__252665 = DvValueTemplate.Instantiate("template.inline.c1_opening_listener_model.104", v_scope_check_estimate, v_basis);
        _ = F_dv_require(R.lt(R.number("0"), R.count(v_basis)), Authored("m40.c1_opening_listener_model.literal_0003"));
        v_mind__252526 = F_dv_add_object(v_mind__252526, DvValueTemplate.Instantiate("template.inline.c1_opening_listener_model.105"));
        v_mind__252526 = F_dv_add_object(v_mind__252526, DvValueTemplate.Instantiate("template.inline.c1_opening_listener_model.106"));
        v_mind__252526 = F_dv_add_object(v_mind__252526, DvValueTemplate.Instantiate("template.inline.c1_opening_listener_model.107", v_basis));
        return DvValueTemplate.Instantiate("template.inline.c1_opening_listener_model.108", v_mind__252526, v_scope_check_estimate, v_basis, v_assumption__252665);
    }
    public static object F_c1_manual_history_gate() {
        return DvValueTemplate.Instantiate("template.c1_manual_history_gate");
    }
    public static object F_c1_manual_replay_variables() {
        return DvValueTemplate.Instantiate("template.c1_manual_replay_variables");
    }
    public static object F_c1_hist_public_feature(object v_feature) {
        return DvValueTemplate.Instantiate("template.c1_hist_public_feature", v_feature);
    }
    public static object F_c1_protocol_coordinates(object v_rules) {
        return DvValueTemplate.Instantiate("template.c1_protocol_coordinates", v_rules);
    }
    public static object F_c1_campaign_scene() {
        object v_scene__369070 = F_c1_opening_install_scene(F_c1_scenario_native(), Authored("m40.c1_report_labels.literal"));
        v_scene__369070 = F_campaign_install_extension(v_scene__369070,
            DvValueTemplate.Instantiate("template.c1_investigation_repair"));
        v_scene__369070 = F_c1_other_groups_install_scene(v_scene__369070);
        object v_physical__369319 = F_dv_add_object(F_dv_get(v_scene__369070, "physical"), F_campaign_state_object(Authored("m40.c1_campaign_scene.literal"), new List<object> { F_dv_pair("minute", F_dv_field(F_dv_get(F_dv_get(v_scene__369070, "physical"), "time"))) }));
        v_physical__369319 = F_dv_add_object(v_physical__369319, F_campaign_state_object(Authored("m40.c1_campaign_scene.literal_0002"), new List<object> { F_dv_pair("living", F_dv_field(true)), F_dv_pair("contract", F_dv_field(false)), F_dv_pair("astral", F_dv_field(false)), F_dv_pair("logistics", F_dv_field(false)) }));
        object v_actors__369757 = new List<object> {  };
        object v_limit__369778 = R.number("0");
        object v_i__369797 = R.number("0");
        while (R.Bool(R.lt(v_i__369797, R.count(F_dv_get(v_scene__369070, "actors")))))
        {
            object v_actor__369855 = R.at(F_dv_get(v_scene__369070, "actors"), v_i__369797);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_campaign_scene.180", v_actor__369855)))
            {
                object v_goals__369991 = F_dv_find(F_dv_get(v_actor__369855, "policy"), "goals", new List<object> {  });
                object v_groups__370051 = AuthoredCopy("m40.c1_campaign_scene.groups");
                object v_j__370160 = R.number("0");
                while (R.Bool(R.lt(v_j__370160, R.count(v_groups__370051))))
                {
                    object v_action__370228 = F_c1_other_open_action(R.at(v_groups__370051, v_j__370160), Authored("m40.c1_observe_as.literal_0005"));
                    _ = R.push(v_goals__369991, DvValueTemplate.Instantiate("template.inline.c1_campaign_scene.181", v_action__370228, v_groups__370051, v_j__370160));
                    v_j__370160 = R.add(v_j__370160, R.number("1"));
                }
                v_actor__369855 = F_dv_with(v_actor__369855, "policy", F_dv_with(F_dv_get(v_actor__369855, "policy"), "goals", v_goals__369991));
            }
            _ = R.push(v_actors__369757, v_actor__369855);
            v_limit__369778 = R.add(v_limit__369778, F_dv_get(F_dv_get(v_actor__369855, "policy"), "budget"));
            v_i__369797 = R.add(v_i__369797, R.number("1"));
        }
        return F_c1_other_environment_install_scene(F_dv_with_many(F_c1_active_configuration(v_scene__369070), new List<object> { F_dv_pair("physical", v_physical__369319), F_dv_pair("actors", v_actors__369757), F_dv_pair("max_actions", v_limit__369778), F_dv_pair("clock_ref", F_campaign_clock()) }));
    }
    public static object F_c1_other_environment_install_scene(object v_scene) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_environment_install_scene.182", v_scene)))
        {
            return v_scene;
        }
        object v_ventilation_source__371496 = F_campaign_source(Authored("m40.c1_inspect_E10.literal_0017"), Authored("m40.c1_other_environment_install_scene.literal_0002"), Authored("m40.c1_other_environment_install_scene.literal_0003"));
        object v_care_source__371798 = F_campaign_source(Authored("m40.c1_inspect_E10.literal_0017"), Authored("m40.c1_other_environment_install_scene.literal_0004"), Authored("m40.c1_other_environment_install_scene.literal_0005"));
        object v_air__372093 = DvValueTemplate.Instantiate("template.inline.c1_other_environment_install_scene.183");
        object v_afternoon__373390 = DvValueTemplate.Instantiate("template.inline.c1_other_environment_install_scene.184");
        object v_care_response__373674 = DvValueTemplate.Instantiate("template.inline.c1_other_environment_install_scene.185", v_afternoon__373390);
        object v_physical__375332 = F_dv_adopt(F_dv_get(v_scene, "physical"), F_campaign_method(F_dv_get(v_air__372093, "name"), v_air__372093), "enforce", R.number("1"), new List<object> { v_ventilation_source__371496 });
        v_physical__375332 = F_dv_adopt(v_physical__375332, F_campaign_method(F_dv_get(v_care_response__373674, "name"), v_care_response__373674), "enforce", R.number("1"), new List<object> { v_care_source__371798 });
        object v_processes__375713 = F_dv_clone(F_dv_find(v_scene, Authored("m40.c1_other_environment_install_scene.literal_0023"), new List<object> {  }));
        _ = R.push(v_processes__375713, new List<object> { F_dv_pair("law", F_dv_get(v_air__372093, "name")), F_dv_pair("once", true), F_dv_pair("source_refs", new List<object> { v_ventilation_source__371496 }) });
        _ = R.push(v_processes__375713, new List<object> { F_dv_pair("law", F_dv_get(v_care_response__373674, "name")), F_dv_pair("once", true), F_dv_pair("source_refs", new List<object> { v_care_source__371798 }) });
        return F_dv_with_many(v_scene, new List<object> { F_dv_pair("physical", v_physical__375332), F_dv_pair("world_processes", v_processes__375713), F_dv_pair("other_environment_installed", true) });
    }
    public static object F_c1_other_people() {
        object v_key__376717 = AuthoredCopy("m40.c1_other_people.key");
        object v_cached__376810 = R.memo_get(new List<object> {  }, v_key__376717);
        if (R.Bool(R.at(v_cached__376810, R.number("0"))))
        {
            return R.at(v_cached__376810, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__376717, F_c1_other_people_build());
    }
    public static object F_c1_other_people_build() {
        return DvValueTemplate.Instantiate("template.c1_other_people_build");
    }
    public static object F_c1_other_person(object v_id) {
        object v_rows__400259 = F_c1_other_people();
        object v_i__400293 = R.number("0");
        while (R.Bool(R.lt(v_i__400293, R.count(v_rows__400259))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(v_rows__400259, v_i__400293), "id"), v_id)))
            {
                return R.at(v_rows__400259, v_i__400293);
            }
            v_i__400293 = R.add(v_i__400293, R.number("1"));
        }
        _ = F_dv_require(false, Authored("m40.c1_other_person.literal"));
        return false;
    }
    public static object F_c1_other_case(object v_group) {
        return DvValueTemplate.Instantiate("template.c1_other_case", v_group);
    }
    public static object F_c1_other_traces(object v_group) {
        return DvValueTemplate.Instantiate("template.c1_other_traces", v_group);
    }
    public static object F_c1_other_identity(object v_group,object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_other_identity", v_group, v_actor);
    }
    public static object F_c1_other_exclusion_hypotheses(object v_id) {
        object v_person__411418 = F_c1_other_person(v_id);
        object v_spec__411456 = F_c1_other_case(F_dv_get(v_person__411418, "group"));
        object v_traces__411500 = F_c1_other_traces(F_dv_get(v_person__411418, "group"));
        object v_result__411548 = new List<object> {  };
        object v_i__411569 = R.number("0");
        while (R.Bool(R.lt(v_i__411569, R.count(v_traces__411500))))
        {
            object v_trace__411621 = R.at(v_traces__411500, v_i__411569);
            object v_observed__411652 = F_campaign_case_claim(F_dv_get(v_person__411418, "group"), R.cat(Authored("m40.c1_other_exclusion_hypotheses.literal"), F_dv_get(v_trace__411621, "id")));
            object v_proposition__411810 = F_dv_not(F_c1_other_identity(F_dv_get(v_person__411418, "group"), F_dv_get(v_trace__411621, "excluded_actor")));
            object v_available__411899 = DvValueTemplate.Instantiate("template.inline.c1_other_exclusion_hypotheses.186", v_person__411418, v_observed__411652);
            if (R.Bool(F_dv_get(v_trace__411621, "needs_actual_witness")))
            {
                v_available__411899 = DvValueTemplate.Instantiate("template.inline.c1_other_exclusion_hypotheses.187", v_available__411899, v_person__411418);
            }
            _ = R.push(v_result__411548, DvValueTemplate.Instantiate("template.inline.c1_other_exclusion_hypotheses.188", v_id, v_trace__411621, v_person__411418, v_proposition__411810, v_available__411899, v_observed__411652));
            v_i__411569 = R.add(v_i__411569, R.number("1"));
        }
        return v_result__411548;
    }
    public static object F_c1_other_question_open(object v_group) {
        return DvValueTemplate.Instantiate("template.c1_other_question_open", v_group);
    }
    public static object F_c1_other_accept(object v_分区,object v_行动者) {
        return DvValueTemplate.Instantiate("template.c1_other_accept", v_分区, v_行动者);
    }
    public static object F_c1_other_release(object v_group,object v_clock_ref) {
        return DvValueTemplate.Instantiate("template.c1_other_release", v_group, v_clock_ref);
    }
    public static object F_c1_other_open_action(object v_group,object v_speaker) {
        return DvValueTemplate.Instantiate("template.c1_other_open_action", v_group, v_speaker);
    }
    public static object F_c1_other_inspections(object v_id) {
        object v_person__422058 = F_c1_other_person(v_id);
        object v_spec__422096 = F_c1_other_case(F_dv_get(v_person__422058, "group"));
        object v_rows__422140 = F_c1_other_traces(F_dv_get(v_person__422058, "group"));
        object v_result__422186 = new List<object> {  };
        object v_i__422207 = R.number("0");
        while (R.Bool(R.lt(v_i__422207, R.count(v_rows__422140))))
        {
            object v_row__422259 = R.at(v_rows__422140, v_i__422207);
            object v_ref__422286 = F_campaign_case_ref(F_dv_get(v_person__422058, "group"), R.cat(Authored("m40.c1_other_exclusion_hypotheses.literal"), F_dv_get(v_row__422259, "id")));
            object v_action__422435 = F_c1_once(DvValueTemplate.Instantiate("template.inline.c1_other_inspections.189", v_id, v_row__422259, v_spec__422096, v_ref__422286));
            _ = R.push(v_result__422186, DvValueTemplate.Instantiate("template.inline.c1_other_inspections.190", v_action__422435, v_row__422259));
            v_i__422207 = R.add(v_i__422207, R.number("1"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_inspections.191", v_id)))
        {
            object v_departed__423071 = F_campaign_any(F_campaign_at(Authored("m40.c1_other_people_build.literal"), Authored("m40.c1_other_inspections.literal_0002")), F_campaign_at(Authored("m40.c1_other_people_build.literal"), Authored("m40.c1_sound_agreement.literal")));
            _ = R.push(v_result__422186, DvValueTemplate.Instantiate("template.inline.c1_other_inspections.192", v_id, v_spec__422096, v_departed__423071));
        }
        object v_own_ref__423959 = F_dv_ref(R.cat(Authored("m40.c1_other_exclusion_hypotheses.literal_0009"), v_id), "state", Authored("m40.c1_other_inspections.literal_0006"));
        _ = R.push(v_result__422186, DvValueTemplate.Instantiate("template.inline.c1_other_inspections.193", v_id, v_spec__422096, v_own_ref__423959, v_person__422058));
        return v_result__422186;
    }
    public static object F_c1_other_answer_actions(object v_id) {
        object v_person__424591 = F_c1_other_person(v_id);
        object v_spec__424629 = F_c1_other_case(F_dv_get(v_person__424591, "group"));
        object v_result__424673 = new List<object> {  };
        object v_i__424694 = R.number("0");
        while (R.Bool(R.lt(v_i__424694, R.count(F_dv_get(v_spec__424629, "options")))))
        {
            object v_option__424754 = R.at(F_dv_get(v_spec__424629, "options"), v_i__424694);
            object v_answer__424792 = F_c1_other_submission(F_dv_get(v_person__424591, "group"), v_option__424754);
            object v_action_id__424856 = R.cat(R.cat(R.cat(v_id, Authored("m40.c1_other_answer_actions.literal")), F_dv_get(v_person__424591, "group")), R.cat(Authored("m40.c1_move_name.literal_0002"), v_option__424754));
            object v_known__425084 = DvValueTemplate.Instantiate("template.inline.c1_other_answer_actions.194", v_id, v_spec__424629, v_person__424591, v_answer__424792);
            object v_action__425283 = F_campaign_intent_action(v_action_id__424856, "answer", v_known__425084, v_known__425084, "", new List<object> {  }, R.cat(Authored("m40.c1_other_answer_actions.literal_0002"), F_dv_get(F_c1_other_person(v_option__424754), "name")), F_dv_get(v_spec__424629, "source"));
            object v_speech__425503 = F_dv_with(F_c1_speech(v_answer__424792, F_dv_get(v_spec__424629, "source"), Authored("m40.c1_other_answer_actions.literal_0003")), "surface", F_c1_other_literal_report_surface(F_dv_get(v_person__424591, "group"), v_option__424754));
            _ = R.push(v_result__424673, DvValueTemplate.Instantiate("template.inline.c1_other_answer_actions.195", v_action__425283, v_answer__424792, v_action_id__424856, v_person__424591, v_speech__425503, v_id, v_spec__424629));
            object v_rows__426732 = F_c1_other_truth_rows(F_dv_get(v_person__424591, "group"));
            object v_last__426786 = R.at(v_rows__426732, R.sub(R.count(v_rows__426732), R.number("1")));
            object v_reasons__426831 = F_dv_get(v_spec__424629, "reason_options");
            object v_r__426874 = R.number("0");
            while (R.Bool(R.lt(v_r__426874, R.count(v_reasons__426831))))
            {
                object v_reason__426935 = R.at(v_reasons__426831, v_r__426874);
                object v_complete__426972 = DvValueTemplate.Instantiate("template.inline.c1_other_answer_actions.196", v_answer__424792, v_person__424591, v_last__426786, v_reason__426935);
                object v_complete_id__427193 = R.cat(v_action_id__424856, R.cat(Authored("m40.c1_other_answer_actions.literal_0005"), v_reason__426935));
                object v_ready__427339 = DvValueTemplate.Instantiate("template.inline.c1_other_answer_actions.197", v_id, v_spec__424629, v_person__424591, v_complete__426972);
                object v_surface__427667 = F_c1_other_reason_report_surface(F_dv_get(v_person__424591, "group"), v_option__424754, v_reason__426935);
                _ = R.push(v_result__424673, DvValueTemplate.Instantiate("template.inline.c1_other_answer_actions.198", v_complete_id__427193, v_ready__427339, v_surface__427667, v_spec__424629, v_complete__426972, v_person__424591, v_id));
                v_r__426874 = R.add(v_r__426874, R.number("1"));
            }
            v_i__424694 = R.add(v_i__424694, R.number("1"));
        }
        return v_result__424673;
    }
    public static object F_c1_other_formal_answers() {
        return DvValueTemplate.Instantiate("template.c1_other_formal_answers");
    }
    public static object F_c1_other_preconception(object v_id) {
        return DvValueTemplate.Instantiate("template.c1_other_preconception", v_id);
    }
    public static object F_c1_other_initial_inquiry(object v_group) {
        return DvValueTemplate.Instantiate("template.c1_other_initial_inquiry", v_group);
    }
    public static object F_c1_other_reason_prior(object v_bias,object v_option) {
        return DvValueTemplate.Instantiate("template.c1_other_reason_prior", v_bias, v_option);
    }
    public static object F_c1_other_reason_hypotheses(object v_id) {
        object v_person__438886 = F_c1_other_person(v_id);
        object v_spec__438924 = F_c1_other_case(F_dv_get(v_person__438886, "group"));
        object v_bias__438968 = F_c1_other_preconception(v_id);
        object v_options__439011 = F_dv_get(v_spec__438924, "reason_options");
        object v_ordered__439050 = new List<object> {  };
        object v_i__439072 = R.number("0");
        if (R.Bool(R.not(R.eq(v_bias__438968, false))))
        {
            _ = R.push(v_ordered__439050, F_dv_get(v_bias__438968, "preferred"));
        }
        while (R.Bool(R.lt(v_i__439072, R.count(v_options__439011))))
        {
            if (R.Bool(R.not(F_dv_has(v_ordered__439050, R.at(v_options__439011, v_i__439072)))))
            {
                _ = R.push(v_ordered__439050, R.at(v_options__439011, v_i__439072));
            }
            v_i__439072 = R.add(v_i__439072, R.number("1"));
        }
        object v_result__439306 = new List<object> {  };
        v_i__439072 = R.number("0");
        while (R.Bool(R.lt(v_i__439072, R.count(v_ordered__439050))))
        {
            object v_option__439378 = R.at(v_ordered__439050, v_i__439072);
            object v_basis__439411 = F_dv_single(F_dv_get(v_spec__438924, "source"));
            if (R.Bool(R.not(R.eq(v_bias__438968, false))))
            {
                if (R.Bool(R.eq(F_dv_get(v_bias__438968, "preferred"), v_option__439378)))
                {
                    v_basis__439411 = F_dv_get(v_bias__438968, "basis");
                }
            }
            object v_proposition__439570 = F_campaign_case_claim(F_dv_get(v_person__438886, "group"), R.cat(Authored("m40.c1_other_answer_actions.literal_0004"), v_option__439378));
            _ = R.push(v_result__439306, DvValueTemplate.Instantiate("template.inline.c1_other_reason_hypotheses.199", v_person__438886, v_option__439378, v_proposition__439570, v_bias__438968, v_basis__439411));
            v_i__439072 = R.add(v_i__439072, R.number("1"));
        }
        return v_result__439306;
    }
    public static object F_c1_other_mind(object v_id) {
        object v_person__440500 = F_c1_other_person(v_id);
        object v_spec__440538 = F_c1_other_case(F_dv_get(v_person__440500, "group"));
        object v_mind__440582 = F_dv_with(F_dv_world(R.cat(Authored("m40.c1_subjective_world.literal"), v_id), "mind", v_id), "time", Authored("m40.c1_physical_world.literal_0002"));
        v_mind__440582 = F_dv_add_object(v_mind__440582, F_c1_position(v_id, F_dv_get(v_spec__440538, "room")));
        v_mind__440582 = F_dv_store(v_mind__440582, F_dv_ref(v_id, Authored("m40.c1_answer_topic.return"), "name"), F_dv_with(F_dv_field(F_dv_get(v_person__440500, "name")), "basis", new List<object> { F_dv_get(v_person__440500, "initial_source") }));
        v_mind__440582 = F_dv_add_object(v_mind__440582, F_campaign_local_object(R.cat(Authored("m40.c1_other_mind.literal"), F_dv_get(v_person__440500, "group"))));
        object v_people__441178 = F_c1_other_people();
        object v_peer_index__441214 = R.number("0");
        while (R.Bool(R.lt(v_peer_index__441214, R.count(v_people__441178))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(v_people__441178, v_peer_index__441214), "group"), F_dv_get(v_person__440500, "group"))))
            {
                v_mind__440582 = F_dv_add_object(v_mind__440582, F_campaign_local_object(R.cat(Authored("m40.c1_other_exclusion_hypotheses.literal_0009"), F_dv_get(R.at(v_people__441178, v_peer_index__441214), "id"))));
                if (R.Bool(R.not(R.eq(F_dv_get(R.at(v_people__441178, v_peer_index__441214), "id"), v_id))))
                {
                    v_mind__440582 = F_dv_add_object(v_mind__440582, F_campaign_local_object(F_dv_get(R.at(v_people__441178, v_peer_index__441214), "id")));
                }
            }
            v_peer_index__441214 = R.add(v_peer_index__441214, R.number("1"));
        }
        v_mind__440582 = F_dv_add_object(v_mind__440582, F_campaign_state_object(Authored("m40.c1_memory_object.literal"), DvValueTemplate.Instantiate("template.inline.c1_other_mind.200", v_person__440500)));
        v_mind__440582 = F_dv_store(v_mind__440582, F_dv_ref(R.cat(Authored("m40.c1_other_exclusion_hypotheses.literal_0009"), v_id), "state", "initial_surface"), F_dv_with(F_dv_field(F_dv_get(v_person__440500, "initial_surface")), "basis", F_dv_single(F_dv_get(v_person__440500, "initial_source"))));
        return v_mind__440582;
    }
    public static object F_c1_other_policy(object v_id) {
        object v_person__442270 = F_c1_other_person(v_id);
        object v_spec__442308 = F_c1_other_case(F_dv_get(v_person__442270, "group"));
        object v_goals__442352 = new List<object> {  };
        object v_i__442372 = R.number("0");
        while (R.Bool((R.Bool(R.lt(v_i__442372, R.count(F_dv_get(v_spec__442308, "options")))) ? (object)(R.eq(v_id, F_c1_other_reporter(F_dv_get(v_person__442270, "group")))) : (object)(false))))
        {
            _ = R.push(v_goals__442352, DvValueTemplate.Instantiate("template.inline.c1_other_policy.201", v_person__442270, v_spec__442308, v_i__442372));
            v_i__442372 = R.add(v_i__442372, R.number("1"));
        }
        if (R.Bool(R.eq(v_id, F_c1_other_reporter(F_dv_get(v_person__442270, "group")))))
        {
            _ = R.push(v_goals__442352, F_campaign_goal(F_campaign_case_claim(F_dv_get(v_person__442270, "group"), R.cat(Authored("m40.c1_other_exclusion_hypotheses.literal_0012"), v_id)), F_campaign_case_claim(F_dv_get(v_person__442270, "group"), Authored("m40.c1_other_exclusion_hypotheses.literal_0002")), new List<object> { F_dv_get(v_spec__442308, "source") }));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_policy.202", v_id)))
        {
            _ = R.push(v_goals__442352, F_campaign_observation_goal(F_dv_ref(Authored("m40.c1_other_people_build.literal"), "state", "location"), F_campaign_case_claim(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_exclusion_hypotheses.literal_0026")), new List<object> { F_dv_get(v_spec__442308, "source") }, R.number("0")));
        }
        object v_hypotheses__443986 = F_c1_other_exclusion_hypotheses(v_id);
        object v_reasons__444042 = F_c1_other_reason_hypotheses(v_id);
        v_i__442372 = R.number("0");
        while (R.Bool(R.lt(v_i__442372, R.count(v_reasons__444042))))
        {
            _ = R.push(v_hypotheses__443986, R.at(v_reasons__444042, v_i__442372));
            v_i__442372 = R.add(v_i__442372, R.number("1"));
        }
        return DvValueTemplate.Instantiate("template.inline.c1_other_policy.203", v_hypotheses__443986, v_goals__442352, v_person__442270, v_spec__442308);
    }
    public static object F_c1_other_character(object v_id) {
        object v_person__444968 = F_c1_other_person(v_id);
        object v_actions__445006 = F_c1_other_inspections(v_id);
        object v_answers__445050 = F_c1_other_answer_actions(v_id);
        object v_i__445097 = R.number("0");
        while (R.Bool(R.lt(v_i__445097, R.count(v_answers__445050))))
        {
            _ = R.push(v_actions__445006, R.at(v_answers__445050, v_i__445097));
            v_i__445097 = R.add(v_i__445097, R.number("1"));
        }
        object v_replies__445207 = F_c1_other_personal_replies(v_id);
        v_i__445097 = R.number("0");
        while (R.Bool(R.lt(v_i__445097, R.count(v_replies__445207))))
        {
            _ = R.push(v_actions__445006, R.at(v_replies__445207, v_i__445097));
            v_i__445097 = R.add(v_i__445097, R.number("1"));
        }
        return new List<object> { F_dv_pair("id", v_id), F_dv_pair("mind", F_c1_other_mind(v_id)), F_dv_pair("actions", v_actions__445006), F_dv_pair("policy", F_c1_other_policy(v_id)), DvValueTemplate.Instantiate("template.inline.c1_other_character.204"), F_dv_pair("location_ref", F_dv_ref(v_id, "state", "location")), F_dv_pair("location", F_dv_get(F_c1_other_case(F_dv_get(v_person__444968, "group")), "room")), DvValueTemplate.Instantiate("template.inline.c1_other_character.205", v_id), F_dv_pair("dossier", v_person__444968) };
    }
    public static object F_c1_other_group_extend(object v_world,object v_group) {
        object v_spec__446020 = F_c1_other_case(v_group);
        object v_fields__446057 = new List<object> { F_dv_pair("open", F_dv_field(false)), F_dv_pair("current", F_dv_field("")), F_dv_pair("outer_latch_released", F_dv_field(false)), F_dv_pair("formal_release", F_dv_field(false)) };
        object v_i__446202 = R.number("0");
        while (R.Bool(R.lt(v_i__446202, R.count(F_dv_get(v_spec__446020, "options")))))
        {
            v_fields__446057 = DvValueTemplate.Instantiate("template.inline.c1_other_group_extend.206", v_fields__446057, v_spec__446020, v_i__446202);
            v_i__446202 = R.add(v_i__446202, R.number("1"));
        }
        v_i__446202 = R.number("0");
        while (R.Bool(R.lt(v_i__446202, R.count(F_dv_get(v_spec__446020, "reason_options")))))
        {
            v_fields__446057 = DvValueTemplate.Instantiate("template.inline.c1_other_group_extend.207", v_fields__446057, v_spec__446020, v_i__446202);
            v_i__446202 = R.add(v_i__446202, R.number("1"));
        }
        object v_traces__446749 = F_c1_other_traces(v_group);
        v_i__446202 = R.number("0");
        while (R.Bool(R.lt(v_i__446202, R.count(v_traces__446749))))
        {
            v_fields__446057 = DvValueTemplate.Instantiate("template.inline.c1_other_group_extend.208", v_fields__446057, v_traces__446749, v_i__446202);
            v_i__446202 = R.add(v_i__446202, R.number("1"));
        }
        object v_people__447011 = F_c1_other_people();
        v_i__446202 = R.number("0");
        object v_result__447058 = v_world;
        while (R.Bool(R.lt(v_i__446202, R.count(v_people__447011))))
        {
            object v_person__447121 = R.at(v_people__447011, v_i__446202);
            if (R.Bool(R.eq(F_dv_get(v_person__447121, "group"), v_group)))
            {
                v_fields__446057 = DvValueTemplate.Instantiate("template.inline.c1_other_group_extend.209", v_fields__446057, v_person__447121);
                v_fields__446057 = DvValueTemplate.Instantiate("template.inline.c1_other_group_extend.210", v_fields__446057, v_person__447121);
                v_fields__446057 = DvValueTemplate.Instantiate("template.inline.c1_other_group_extend.211", v_fields__446057, v_person__447121);
                v_fields__446057 = DvValueTemplate.Instantiate("template.inline.c1_other_group_extend.212", v_fields__446057, v_person__447121);
                v_result__447058 = F_dv_add_object(v_result__447058, F_c1_position(F_dv_get(v_person__447121, "id"), F_dv_get(v_spec__446020, "room")));
                v_result__447058 = F_dv_store(v_result__447058, F_dv_ref(F_dv_get(v_person__447121, "id"), Authored("m40.c1_answer_topic.return"), "name"), F_dv_with(F_dv_field(F_dv_get(v_person__447121, "name")), "basis", new List<object> { F_dv_get(v_person__447121, "initial_source") }));
                v_result__447058 = F_dv_add_object(v_result__447058, F_campaign_state_object(R.cat(Authored("m40.c1_other_exclusion_hypotheses.literal_0009"), F_dv_get(v_person__447121, "id")), new List<object> { F_dv_pair("material_detail", F_dv_field(F_dv_get(v_person__447121, "material_surface"))), F_dv_pair("current", F_dv_field("")) }));
                v_result__447058 = F_dv_adopt(v_result__447058, F_campaign_method(R.cat(R.cat(Authored("m40.c1_other_formal_answers.literal"), v_group), R.cat(Authored("m40.c1_move_name.literal_0002"), F_dv_get(v_person__447121, "id"))), F_c1_other_accept(v_group, F_dv_get(v_person__447121, "id"))), "enforce", R.number("1"), F_dv_single(F_dv_get(v_spec__446020, "source")));
                v_result__447058 = F_dv_adopt(v_result__447058, F_campaign_method(R.cat(R.cat(Authored("m40.c1_other_formal_answers.literal_0002"), v_group), R.cat(Authored("m40.c1_move_name.literal_0002"), F_dv_get(v_person__447121, "id"))), F_c1_other_complete_accept(v_group, F_dv_get(v_person__447121, "id"))), "enforce", R.number("1"), new List<object> { F_dv_get(v_spec__446020, "source") });
            }
            v_i__446202 = R.add(v_i__446202, R.number("1"));
        }
        v_result__447058 = F_dv_add_object(v_result__447058, F_campaign_state_object(R.cat(Authored("m40.c1_other_mind.literal"), v_group), v_fields__446057));
        v_result__447058 = F_dv_adopt(v_result__447058, F_campaign_method(R.cat(Authored("m40.c1_other_open_action.literal_0002"), v_group), F_c1_other_question_open(v_group)), "enforce", R.number("1"), F_dv_single(F_dv_get(v_spec__446020, "source")));
        return F_c1_other_activity_physical(v_result__447058, v_group);
    }
    public static object F_c1_other_groups_physical(object v_world) {
        return F_c1_other_group_extend(F_c1_other_group_extend(F_c1_other_group_extend(v_world, Authored("m40.c1_other_people_build.literal_0004")), Authored("m40.c1_other_environment_install_scene.literal_0008")), Authored("m40.c1_other_environment_install_scene.literal_0016"));
    }
    public static object F_c1_other_groups_bindings() {
        return DvValueTemplate.Instantiate("template.c1_other_groups_bindings");
    }
    public static object F_c1_other_groups_release_laws(object v_clock_ref) {
        return DvValueTemplate.Instantiate("template.c1_other_groups_release_laws", v_clock_ref);
    }
    public static object F_c1_other_personal_replies(object v_id) {
        object v_person__451471 = F_c1_other_person(v_id);
        object v_ref__451509 = F_dv_ref(R.cat(Authored("m40.c1_other_exclusion_hypotheses.literal_0009"), v_id), "state", Authored("m40.c1_other_inspections.literal_0006"));
        object v_known__451717 = F_dv_fact(v_ref__451509, F_dv_get(v_person__451471, "material_surface"));
        object v_people__451772 = F_c1_other_people();
        object v_result__451808 = new List<object> {  };
        object v_i__451829 = R.number("0");
        while (R.Bool(R.lt(v_i__451829, R.count(v_people__451772))))
        {
            object v_other__451883 = R.at(v_people__451772, v_i__451829);
            if (R.Bool(F_dv_and(R.eq(F_dv_get(v_other__451883, "group"), F_dv_get(v_person__451471, "group")), R.not(R.eq(F_dv_get(v_other__451883, "id"), v_id)))))
            {
                object v_action__451993 = DvValueTemplate.Instantiate("template.inline.c1_other_personal_replies.213", v_id, v_other__451883, v_known__451717, v_person__451471);
                object v_speech__452407 = DvValueTemplate.Instantiate("template.inline.c1_other_personal_replies.214", v_known__451717, v_person__451471);
                _ = R.push(v_result__451808, DvValueTemplate.Instantiate("template.inline.c1_other_personal_replies.215", v_action__451993, v_speech__452407, v_other__451883, v_id));
            }
            v_i__451829 = R.add(v_i__451829, R.number("1"));
        }
        return v_result__451808;
    }
    public static object F_c1_other_release_action(object v_group,object v_speaker,object v_clock_ref) {
        return DvValueTemplate.Instantiate("template.c1_other_release_action", v_group, v_speaker, v_clock_ref);
    }
    public static object F_c1_other_departure_source(object v_id) {
        return DvValueTemplate.Instantiate("template.c1_other_departure_source", v_id);
    }
    public static object F_c1_other_depart(object v_id) {
        object v_person__463661 = F_c1_other_person(v_id);
        object v_source__463699 = F_c1_other_departure_source(v_id);
        object v_premise__463747 = DvValueTemplate.Instantiate("template.inline.c1_other_depart.216", v_id, v_person__463661);
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_depart.217", v_id)))
        {
            v_premise__463747 = DvValueTemplate.Instantiate("template.inline.c1_other_depart.218", v_premise__463747);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_depart.219", v_id)))
        {
            v_premise__463747 = DvValueTemplate.Instantiate("template.inline.c1_other_depart.220", v_premise__463747, v_person__463661);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_depart.221", v_id)))
        {
            v_premise__463747 = DvValueTemplate.Instantiate("template.inline.c1_other_depart.222", v_premise__463747);
        }
        return DvValueTemplate.Instantiate("template.inline.c1_other_depart.223", v_id, v_premise__463747, v_person__463661);
    }
    public static object F_c1_other_departure_actions(object v_id) {
        object v_person__465643 = F_c1_other_person(v_id);
        object v_room__465681 = F_dv_get(F_c1_other_case(F_dv_get(v_person__465643, "group")), "room");
        object v_source__465730 = F_c1_other_departure_source(v_id);
        object v_ref__465778 = F_campaign_case_ref(F_dv_get(v_person__465643, "group"), Authored("m40.c1_other_exclusion_hypotheses.literal_0026"));
        object v_inspect__465912 = DvValueTemplate.Instantiate("template.inline.c1_other_departure_actions.224", v_id, v_room__465681, v_ref__465778, v_person__465643, v_source__465730);
        object v_premise__466367 = F_dv_get(F_c1_other_depart(v_id), "premise");
        object v_depart__466414 = F_dv_with_many(DvValueTemplate.Instantiate("template.inline.c1_other_departure_actions.225", v_id, v_premise__466367, v_source__465730), new List<object> { DvValueTemplate.Instantiate("template.inline.c1_other_departure_actions.226", v_id), F_dv_pair("surface", F_dvs_surface_parts(new List<object> { F_dv_get(F_c1_other_person(v_id), "name"), Authored("m40.c1_other_departure_actions.literal_0007") })), DvValueTemplate.Instantiate("template.inline.c1_other_departure_actions.227"), F_dv_pair("later_intention_surface", F_dv_get(v_source__465730, "surface")), F_dv_pair("travel", new List<object> { F_dv_pair("from", v_room__465681), F_dv_pair("to", F_c1_other_threshold(F_dv_get(v_person__465643, "group"))), F_dv_pair("gate", F_dv_get(v_person__465643, "group")) }) });
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_departure_actions.228", v_id)))
        {
            v_depart__466414 = DvValueTemplate.Instantiate("template.inline.c1_other_departure_actions.229", v_depart__466414);
        }
        v_depart__466414 = DvValueTemplate.Instantiate("template.inline.c1_other_departure_actions.230", v_depart__466414, v_premise__466367, v_person__465643);
        return F_dv_pair(v_inspect__465912, v_depart__466414);
    }
    public static object F_c1_other_group_listeners(object v_group,object v_except) {
        return DvValueTemplate.Instantiate("template.c1_other_group_listeners", v_group, v_except);
    }
    public static object F_c1_other_wake_action(object v_id) {
        object v_person__468462 = F_c1_other_person(v_id);
        object v_room__468500 = F_dv_get(F_c1_other_case(F_dv_get(v_person__468462, "group")), "room");
        object v_literal__468549 = DvValueTemplate.Instantiate("template.inline.c1_other_wake_action.231", v_id, v_person__468462);
        object v_action__468854 = F_c1_once(DvValueTemplate.Instantiate("template.inline.c1_other_wake_action.232", v_id, v_room__468500, v_literal__468549, v_person__468462));
        object v_text__469254 = F_dvs_surface_parts(DvValueTemplate.Instantiate("template.inline.c1_other_wake_action.233", v_person__468462, v_id));
        return DvValueTemplate.Instantiate("template.inline.c1_other_wake_action.234", v_action__468854, v_literal__468549, v_person__468462, v_text__469254, v_id);
    }
    public static object F_c1_other_trace_conversations(object v_id) {
        object v_person__469939 = F_c1_other_person(v_id);
        object v_spec__469977 = F_c1_other_case(F_dv_get(v_person__469939, "group"));
        object v_peers__470021 = F_c1_other_group_listeners(F_dv_get(v_person__469939, "group"), v_id);
        object v_traces__470081 = F_c1_other_traces(F_dv_get(v_person__469939, "group"));
        object v_result__470129 = new List<object> {  };
        object v_i__470150 = R.number("0");
        while (R.Bool(R.lt(v_i__470150, R.count(v_peers__470021))))
        {
            object v_peer__470201 = R.at(v_peers__470021, v_i__470150);
            object v_j__470230 = R.number("0");
            while (R.Bool(R.lt(v_j__470230, R.count(v_traces__470081))))
            {
                object v_trace__470290 = R.at(v_traces__470081, v_j__470230);
                object v_topic__470325 = R.cat(R.cat(F_dv_get(v_person__469939, "group"), Authored("m40.c1_other_trace_conversations.literal")), F_dv_get(v_trace__470290, "id"));
                object v_ref__470470 = F_campaign_case_ref(F_dv_get(v_person__469939, "group"), R.cat(Authored("m40.c1_other_exclusion_hypotheses.literal"), F_dv_get(v_trace__470290, "id")));
                object v_literal__470625 = F_dv_fact(v_ref__470470, true);
                object v_prefix__470671 = R.cat(R.cat(v_id, Authored("m40.c1_other_trace_conversations.literal_0002")), R.cat(v_peer__470201, R.cat(Authored("m40.c1_move_name.literal_0002"), F_dv_get(v_trace__470290, "id"))));
                object v_request__470904 = DvValueTemplate.Instantiate("template.inline.c1_other_trace_conversations.235", v_trace__470290, v_peer__470201, v_topic__470325);
                _ = R.push(v_result__470129, DvValueTemplate.Instantiate("template.inline.c1_other_trace_conversations.236", v_prefix__470671, v_id, v_spec__469977, v_trace__470290, v_peer__470201, v_request__470904, v_ref__470470));
                object v_report__472428 = DvValueTemplate.Instantiate("template.inline.c1_other_trace_conversations.237", v_literal__470625, v_trace__470290);
                _ = R.push(v_result__470129, DvValueTemplate.Instantiate("template.inline.c1_other_trace_conversations.238", v_prefix__470671, v_id, v_spec__469977, v_literal__470625, v_trace__470290, v_peer__470201, v_report__472428, v_topic__470325, v_ref__470470));
                v_j__470230 = R.add(v_j__470230, R.number("1"));
            }
            v_i__470150 = R.add(v_i__470150, R.number("1"));
        }
        return v_result__470129;
    }
    public static object F_c1_other_desire_action(object v_id) {
        object v_person__473831 = F_c1_other_person(v_id);
        object v_room__473869 = F_dv_get(F_c1_other_case(F_dv_get(v_person__473831, "group")), "room");
        object v_ref__473918 = F_dv_ref(R.cat(Authored("m40.c1_other_exclusion_hypotheses.literal_0009"), v_id), "state", Authored("m40.c1_other_inspections.literal_0006"));
        object v_action__474126 = F_c1_once(DvValueTemplate.Instantiate("template.inline.c1_other_desire_action.239", v_id, v_room__473869, v_ref__473918, v_person__473831));
        return DvValueTemplate.Instantiate("template.inline.c1_other_desire_action.240", v_action__474126, v_person__473831, v_ref__473918);
    }
    public static object F_c1_other_groups_install_scene(object v_scene) {
        object v_world__475165 = F_c1_other_groups_physical(F_dv_get(v_scene, "physical"));
        object v_release_laws__475223 = F_c1_other_groups_release_laws(F_campaign_clock());
        object v_release_index__475294 = R.number("0");
        while (R.Bool(R.lt(v_release_index__475294, R.count(v_release_laws__475223))))
        {
            object v_group__475376 = R.at(AuthoredCopy("m40.c1_campaign_scene.groups"), v_release_index__475294);
            v_world__475165 = F_dv_adopt(v_world__475165, F_campaign_method(R.cat(Authored("m40.c1_other_release_action.literal_0002"), v_group__475376), R.at(v_release_laws__475223, v_release_index__475294)), "enforce", R.number("1"), F_dv_single(F_dv_get(F_c1_other_case(v_group__475376), "source")));
            v_release_index__475294 = R.add(v_release_index__475294, R.number("1"));
        }
        object v_actors__475763 = F_dv_clone(F_dv_get(v_scene, "actors"));
        object v_bindings__475804 = F_dv_clone(F_dv_get(v_scene, "bindings"));
        object v_answers__475849 = F_dv_clone(F_dv_get(v_scene, "formal_answers"));
        object v_people__475899 = F_c1_other_people();
        object v_i__475935 = R.number("0");
        while (R.Bool(R.lt(v_i__475935, R.count(v_people__475899))))
        {
            object v_person__475987 = R.at(v_people__475899, v_i__475935);
            object v_object__476019 = R.cat(Authored("m40.c1_other_exclusion_hypotheses.literal_0009"), F_dv_get(v_person__475987, "id"));
            v_world__475165 = F_dv_store(v_world__475165, F_dv_ref(v_object__476019, "state", "initial_surface"), F_dv_field(F_dv_get(v_person__475987, "initial_surface")));
            object v_actor__476257 = F_c1_other_activity_actor(F_c1_other_character(F_dv_get(v_person__475987, "id")), F_dv_get(v_person__475987, "group"));
            object v_depart_method__476347 = F_campaign_method(R.cat(Authored("m40.c1_other_departure_actions.literal_0005"), F_dv_get(v_person__475987, "id")), F_c1_other_depart(F_dv_get(v_person__475987, "id")));
            v_world__475165 = F_dv_adopt(v_world__475165, v_depart_method__476347, "enforce", R.number("1"), F_dv_single(F_dv_get(v_person__475987, "initial_source")));
            object v_own_mind__476619 = F_dv_add_object(F_dv_get(v_actor__476257, "mind"), F_campaign_local_object(Authored("m40.c1_campaign_scene.literal_0002")));
            v_own_mind__476619 = F_dv_adopt(v_own_mind__476619, v_depart_method__476347, "predict", R.number("1"), F_dv_single(F_dv_get(v_person__475987, "initial_source")));
            v_actor__476257 = DvValueTemplate.Instantiate("template.inline.c1_other_groups_install_scene.241", v_actor__476257, v_own_mind__476619, v_person__475987);
            _ = R.push(v_bindings__475804, F_dv_binding(Authored("m40.c1_campaign_scene.literal_0002"), Authored("m40.c1_campaign_scene.literal_0002"), F_dv_get(v_person__475987, "id")));
            _ = R.push(v_bindings__475804, F_dv_binding(Authored("m40.c1_campaign_scene.literal"), Authored("m40.c1_campaign_scene.literal"), F_dv_get(v_person__475987, "id")));
            object v_actions__477517 = new List<object> {  };
            _ = R.push(v_actions__477517, F_c1_other_wake_action(F_dv_get(v_person__475987, "id")));
            _ = R.push(v_actions__477517, F_c1_other_desire_action(F_dv_get(v_person__475987, "id")));
            object v_j__477663 = R.number("0");
            while (R.Bool(R.lt(v_j__477663, R.count(F_dv_get(v_actor__476257, "actions")))))
            {
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_groups_install_scene.242", v_actor__476257, v_j__477663, v_person__475987)))
                {
                    _ = R.push(v_actions__477517, R.at(F_dv_get(v_actor__476257, "actions"), v_j__477663));
                }
                v_j__477663 = R.add(v_j__477663, R.number("1"));
            }
            object v_departure_actions__477937 = F_c1_other_departure_actions(F_dv_get(v_person__475987, "id"));
            v_j__477663 = R.number("0");
            while (R.Bool(R.lt(v_j__477663, R.count(v_departure_actions__477937))))
            {
                _ = R.push(v_actions__477517, R.at(v_departure_actions__477937, v_j__477663));
                v_j__477663 = R.add(v_j__477663, R.number("1"));
            }
            _ = R.push(v_actions__477517, F_c1_other_handoff_action(F_dv_get(v_person__475987, "id")));
            object v_conversations__478208 = F_c1_other_trace_conversations(F_dv_get(v_person__475987, "id"));
            v_j__477663 = R.number("0");
            while (R.Bool(R.lt(v_j__477663, R.count(v_conversations__478208))))
            {
                _ = R.push(v_actions__477517, R.at(v_conversations__478208, v_j__477663));
                v_j__477663 = R.add(v_j__477663, R.number("1"));
            }
            object v_channel__478407 = R.cat(Authored("m40.c1_other_open_action.literal_0008"), F_dv_get(v_person__475987, "group"));
            object v_ready__478529 = F_campaign_at(F_dv_get(v_person__475987, "id"), F_dv_get(F_c1_other_case(F_dv_get(v_person__475987, "group")), "room"));
            v_actor__476257 = DvValueTemplate.Instantiate("template.inline.c1_other_groups_install_scene.243", v_actor__476257, v_actions__477517, v_channel__478407, v_ready__478529, v_person__475987);
            _ = R.push(v_actors__475763, v_actor__476257);
            v_i__475935 = R.add(v_i__475935, R.number("1"));
        }
        object v_local_bindings__479279 = F_c1_other_groups_bindings();
        v_i__475935 = R.number("0");
        while (R.Bool(R.lt(v_i__475935, R.count(v_local_bindings__479279))))
        {
            _ = R.push(v_bindings__475804, R.at(v_local_bindings__479279, v_i__475935));
            v_i__475935 = R.add(v_i__475935, R.number("1"));
        }
        object v_formal__479449 = F_c1_other_formal_answers();
        v_i__475935 = R.number("0");
        while (R.Bool(R.lt(v_i__475935, R.count(v_formal__479449))))
        {
            _ = R.push(v_answers__475849, R.at(v_formal__479449, v_i__475935));
            v_i__475935 = R.add(v_i__475935, R.number("1"));
        }
        object v_installed_actors__479593 = new List<object> {  };
        v_i__475935 = R.number("0");
        while (R.Bool(R.lt(v_i__475935, R.count(v_actors__475763))))
        {
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_groups_install_scene.244", v_actors__475763, v_i__475935)))
            {
                object v_demon__479775 = R.at(v_actors__475763, v_i__475935);
                object v_mind__479810 = F_dv_get(v_demon__479775, "mind");
                object v_actions__479845 = F_dv_clone(F_dv_get(v_demon__479775, "actions"));
                object v_hearing__479896 = F_dv_clone(F_dv_find(v_demon__479775, Authored("m40.c1_sound_install_scene.literal_0009"), new List<object> {  }));
                object v_g__480032 = R.number("0");
                object v_groups__480055 = AuthoredCopy("m40.c1_campaign_scene.groups");
                while (R.Bool(R.lt(v_g__480032, R.count(v_groups__480055))))
                {
                    object v_group__480209 = R.at(v_groups__480055, v_g__480032);
                    object v_spec__480248 = F_c1_other_case(v_group__480209);
                    v_mind__479810 = F_dv_add_object(v_mind__479810, F_campaign_state_object(R.cat(Authored("m40.c1_other_mind.literal"), v_group__480209), new List<object> { F_dv_pair("open", F_dv_field(false)) }));
                    v_mind__479810 = F_dv_adopt(v_mind__479810, F_campaign_method(R.cat(Authored("m40.c1_other_open_action.literal_0002"), v_group__480209), F_c1_other_question_open(v_group__480209)), "predict", R.number("1"), F_dv_single(F_dv_get(v_spec__480248, "source")));
                    _ = R.push(v_bindings__475804, F_dv_binding(R.cat(Authored("m40.c1_other_mind.literal"), v_group__480209), R.cat(Authored("m40.c1_other_mind.literal"), v_group__480209), Authored("m40.c1_observe_as.literal_0005")));
                    object v_listeners__480970 = F_c1_other_group_listeners(v_group__480209, "");
                    object v_k__481039 = R.number("0");
                    while (R.Bool(R.lt(v_k__481039, R.count(v_listeners__480970))))
                    {
                        _ = R.push(v_hearing__479896, DvValueTemplate.Instantiate("template.inline.c1_other_groups_install_scene.245", v_listeners__480970, v_k__481039, v_group__480209, v_spec__480248));
                        _ = R.push(v_hearing__479896, DvValueTemplate.Instantiate("template.inline.c1_other_groups_install_scene.246", v_listeners__480970, v_k__481039, v_spec__480248));
                        v_mind__479810 = F_dv_add_object(v_mind__479810, F_campaign_local_object(R.cat(Authored("m40.c1_other_exclusion_hypotheses.literal_0009"), R.at(v_listeners__480970, v_k__481039))));
                        v_k__481039 = R.add(v_k__481039, R.number("1"));
                    }
                    _ = R.push(v_actions__479845, F_c1_other_open_action(v_group__480209, Authored("m40.c1_observe_as.literal_0005")));
                    v_g__480032 = R.add(v_g__480032, R.number("1"));
                }
                _ = R.push(v_installed_actors__479593, F_dv_with_many(v_demon__479775, new List<object> { F_dv_pair("mind", v_mind__479810), F_dv_pair("actions", v_actions__479845), F_dv_pair("hearing_grants", v_hearing__479896) }));
            }
            else
            {
                _ = R.push(v_installed_actors__479593, R.at(v_actors__475763, v_i__475935));
            }
            v_i__475935 = R.add(v_i__475935, R.number("1"));
        }
        return F_c1_other_inquiry_scene(DvValueTemplate.Instantiate("template.inline.c1_other_groups_install_scene.247", v_scene, v_world__475165, v_installed_actors__479593, v_bindings__475804, v_answers__475849));
    }
    public static object F_c1_other_moral_conflict(object v_id) {
        object v_group__483603 = F_dv_get(F_c1_other_person(v_id), "group");
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_moral_conflict.248", v_group__483603)))
        {
            return DvValueTemplate.Instantiate("template.inline.c1_other_moral_conflict.249", v_group__483603);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_moral_conflict.250", v_group__483603)))
        {
            return DvValueTemplate.Instantiate("template.inline.c1_other_moral_conflict.251", v_group__483603);
        }
        return DvValueTemplate.Instantiate("template.inline.c1_other_moral_conflict.252", v_group__483603);
    }
    public static object F_c1_other_inquiry_scene(object v_scene) {
        object v_world__485822 = F_dv_get(v_scene, "physical");
        object v_actors__485854 = new List<object> {  };
        object v_groups__485875 = AuthoredCopy("m40.c1_campaign_scene.groups");
        object v_g__485976 = R.number("0");
        while (R.Bool(R.lt(v_g__485976, R.count(v_groups__485875))))
        {
            object v_group__486028 = R.at(v_groups__485875, v_g__485976);
            object v_rows__486059 = F_c1_other_truth_rows(v_group__486028);
            object v_j__486106 = R.number("0");
            while (R.Bool(R.lt(v_j__486106, R.count(v_rows__486059))))
            {
                object v_row__486164 = R.at(v_rows__486059, v_j__486106);
                v_world__485822 = F_dv_store(v_world__485822, F_campaign_case_ref(v_group__486028, F_dv_get(v_row__486164, "id")), F_dv_field(false));
                object v_people__486283 = F_c1_other_people();
                object v_k__486327 = R.number("0");
                while (R.Bool(R.lt(v_k__486327, R.count(v_people__486283))))
                {
                    if (R.Bool(R.eq(F_dv_get(R.at(v_people__486283, v_k__486327), "group"), v_group__486028)))
                    {
                        object v_owner__486445 = F_dv_get(R.at(v_people__486283, v_k__486327), "id");
                        object v_law__486491 = F_c1_other_resident_truth_law(v_group__486028, v_row__486164, v_owner__486445);
                        v_world__485822 = F_dv_adopt(v_world__485822, F_campaign_method(R.cat(R.cat(Authored("m40.c1_other_inquiry_scene.literal"), v_owner__486445), R.cat(Authored("m40.c1_move_name.literal_0002"), F_dv_get(v_row__486164, "id"))), v_law__486491), "enforce", R.number("1"), new List<object> { F_dv_get(F_c1_other_case(v_group__486028), "source") });
                    }
                    v_k__486327 = R.add(v_k__486327, R.number("1"));
                }
                v_j__486106 = R.add(v_j__486106, R.number("1"));
            }
            v_g__485976 = R.add(v_g__485976, R.number("1"));
        }
        object v_i__486918 = R.number("0");
        while (R.Bool(R.lt(v_i__486918, R.count(F_dv_get(v_scene, "actors")))))
        {
            object v_actor__486976 = R.at(F_dv_get(v_scene, "actors"), v_i__486918);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_inquiry_scene.253", v_actor__486976)))
            {
                object v_person__487440 = F_c1_other_person(F_dv_get(v_actor__486976, "id"));
                object v_group__487492 = F_dv_get(v_person__487440, "group");
                object v_source__487530 = F_dv_get(F_c1_other_case(v_group__487492), "source");
                object v_rows__487584 = F_c1_other_truth_rows(v_group__487492);
                object v_full_chain__487635 = DvValueTemplate.Instantiate("template.inline.c1_other_inquiry_scene.254", v_group__487492);
                object v_proof_index__487854 = R.number("0");
                while (R.Bool(R.lt(v_proof_index__487854, R.count(v_rows__487584))))
                {
                    _ = R.push(v_full_chain__487635, F_campaign_case_claim(v_group__487492, F_dv_get(R.at(v_rows__487584, v_proof_index__487854), "id")));
                    v_proof_index__487854 = R.add(v_proof_index__487854, R.number("1"));
                }
                object v_final__488070 = F_dv_all(v_full_chain__487635);
                object v_angry__488114 = F_dv_scoped_ref(F_dv_get(v_actor__486976, "id"), R.cat(Authored("m40.c1_other_exclusion_hypotheses.literal_0009"), F_dv_get(v_actor__486976, "id")), Authored("m40.c1_other_inquiry_scene.literal_0002"), Authored("m40.c1_other_inquiry_scene.literal_0003"));
                object v_mind__488428 = F_dv_store(F_dv_get(v_actor__486976, "mind"), v_angry__488114, F_dv_with(F_dv_field(false), "basis", new List<object> { F_dv_get(v_person__487440, "initial_source") }));
                object v_bias__488540 = F_c1_other_preconception(F_dv_get(v_actor__486976, "id"));
                object v_appraisals__488597 = F_dv_find(F_dv_get(v_actor__486976, "policy"), Authored("m40.c1_other_inquiry_scene.literal_0004"), new List<object> {  });
                if (R.Bool(R.not(R.eq(v_bias__488540, false))))
                {
                    object v_conflict__488768 = F_dv_all(new List<object> { F_c1_other_moral_conflict(F_dv_get(v_actor__486976, "id")), F_dv_not(v_final__488070) });
                    _ = R.push(v_appraisals__488597, DvValueTemplate.Instantiate("template.inline.c1_other_inquiry_scene.255", v_actor__486976, v_angry__488114, v_conflict__488768, v_group__487492, v_bias__488540));
                }
                object v_actions__489223 = F_dv_clone(F_dv_get(v_actor__486976, "actions"));
                object v_goals__489274 = new List<object> {  };
                object v_complete_ref__489302 = F_campaign_case_ref(v_group__487492, R.cat(Authored("m40.c1_other_answer_actions.literal_0010"), F_dv_get(v_actor__486976, "id")));
                v_mind__488428 = F_dv_store(v_mind__488428, v_complete_ref__489302, F_dv_with(F_dv_field(false), "basis", new List<object> { v_source__487530 }));
                _ = R.push(v_goals__489274, F_campaign_goal(F_dv_fact(v_complete_ref__489302, true), DvValueTemplate.Instantiate("template.inline.c1_other_inquiry_scene.256", v_final__488070, v_group__487492, v_actor__486976), new List<object> { v_source__487530 }));
                object v_j__489784 = R.number("0");
                while (R.Bool(R.lt(v_j__489784, R.count(F_dv_get(F_dv_get(v_actor__486976, "policy"), "goals")))))
                {
                    object v_goal__489864 = R.at(F_dv_get(F_dv_get(v_actor__486976, "policy"), "goals"), v_j__489784);
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_inquiry_scene.257", v_goal__489864, v_group__487492, v_actor__486976)))
                    {
                        v_goal__489864 = F_campaign_index_goal_window(F_dv_with(v_goal__489864, "available_when", F_campaign_any(F_dv_fact(v_angry__488114, true), v_final__488070)));
                    }
                    _ = R.push(v_goals__489274, v_goal__489864);
                    v_j__489784 = R.add(v_j__489784, R.number("1"));
                }
                v_j__489784 = R.number("0");
                while (R.Bool(R.lt(v_j__489784, R.count(v_rows__487584))))
                {
                    object v_row__490427 = R.at(v_rows__487584, v_j__489784);
                    object v_ref__490462 = F_campaign_case_ref(v_group__487492, F_dv_get(v_row__490427, "id"));
                    v_mind__488428 = F_dv_store(v_mind__488428, v_ref__490462, F_dv_with(F_dv_field(false), "basis", new List<object> { v_source__487530 }));
                    object v_law__490611 = F_c1_other_resident_truth_law(v_group__487492, v_row__490427, F_dv_get(v_actor__486976, "id"));
                    object v_method__490688 = R.cat(R.cat(Authored("m40.c1_other_inquiry_scene.literal"), F_dv_get(v_actor__486976, "id")), R.cat(Authored("m40.c1_move_name.literal_0002"), F_dv_get(v_row__490427, "id")));
                    v_mind__488428 = F_dv_adopt(v_mind__488428, F_campaign_method(v_method__490688, v_law__490611), "predict", R.number("1"), new List<object> { v_source__487530 });
                    object v_after__490997 = F_campaign_case_claim(v_group__487492, F_dv_get(v_row__490427, "after"));
                    object v_ready__491064 = F_dv_all(new List<object> { F_campaign_at(F_dv_get(v_actor__486976, "id"), F_dv_get(F_c1_other_case(v_group__487492), "room")), v_after__490997, F_dv_fact(v_ref__490462, false) });
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_inquiry_scene.258", v_row__490427)))
                    {
                        object v_inspection__491300 = DvValueTemplate.Instantiate("template.inline.c1_other_inquiry_scene.259", v_method__490688, v_row__490427, v_ready__491064, v_law__490611, v_ref__490462, v_source__487530);
                        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_inquiry_scene.260", v_row__490427)))
                        {
                            v_inspection__491300 = F_dv_with_many(v_inspection__491300, new List<object> { F_dv_pair("public_observers", F_c1_other_group_listeners(v_group__487492, F_dv_get(v_actor__486976, "id"))), F_dv_pair("public_grants", new List<object> { v_ref__490462 }) });
                        }
                        _ = R.push(v_actions__489223, v_inspection__491300);
                    }
                    else
                    {
                        object v_ask_id__491920 = R.cat(F_dv_get(v_actor__486976, "id"), R.cat(Authored("m40.c1_other_inquiry_scene.literal_0007"), F_dv_get(v_row__490427, "id")));
                        object v_question__492067 = Authored("m40.c1_other_inquiry_scene.question");
                        _ = R.push(v_actions__489223, DvValueTemplate.Instantiate("template.inline.c1_other_inquiry_scene.261", v_ask_id__491920, v_ready__491064, v_row__490427, v_group__487492, v_question__492067, v_source__487530, v_after__490997, v_ref__490462));
                        if (R.Bool(R.eq(F_dv_get(v_actor__486976, "id"), F_dv_get(v_row__490427, "partner"))))
                        {
                            object v_peers__492942 = F_c1_other_group_listeners(v_group__487492, F_dv_get(v_actor__486976, "id"));
                            object v_k__493021 = R.number("0");
                            while (R.Bool(R.lt(v_k__493021, R.count(v_peers__492942))))
                            {
                                object v_peer__493112 = R.at(v_peers__492942, v_k__493021);
                                object v_peer_method__493161 = R.cat(R.cat(Authored("m40.c1_other_inquiry_scene.literal"), v_peer__493112), R.cat(Authored("m40.c1_move_name.literal_0002"), F_dv_get(v_row__490427, "id")));
                                object v_peer_law__493390 = F_c1_other_resident_truth_law(v_group__487492, v_row__490427, v_peer__493112);
                                v_mind__488428 = F_dv_adopt(v_mind__488428, F_campaign_method(v_peer_method__493161, v_peer_law__493390), "predict", R.number("1"), new List<object> { v_source__487530 });
                                object v_recall__493595 = DvValueTemplate.Instantiate("template.inline.c1_other_inquiry_scene.262", v_peer_method__493161, v_peer_law__493390, v_ref__490462, v_source__487530, v_peer__493112, v_row__490427);
                                _ = R.push(v_actions__489223, v_recall__493595);
                                object v_reply__494323 = DvValueTemplate.Instantiate("template.inline.c1_other_inquiry_scene.263", v_peer_method__493161, v_group__487492, v_row__490427, v_actor__486976, v_source__487530, v_peer__493112);
                                _ = R.push(v_actions__489223, v_reply__494323);
                                _ = R.push(v_goals__489274, F_campaign_goal(F_campaign_case_claim(v_group__487492, F_dv_get(v_row__490427, "id")), v_after__490997, new List<object> { v_source__487530 }));
                                _ = R.push(v_goals__489274, DvValueTemplate.Instantiate("template.inline.c1_other_inquiry_scene.264", v_reply__494323, v_group__487492, v_row__490427, v_source__487530));
                                v_k__493021 = R.add(v_k__493021, R.number("1"));
                            }
                        }
                    }
                    if (R.Bool(R.eq(F_dv_get(v_actor__486976, "id"), F_c1_other_reporter(v_group__487492))))
                    {
                        _ = R.push(v_goals__489274, F_campaign_goal(F_campaign_case_claim(v_group__487492, F_dv_get(v_row__490427, "id")), F_dv_all(new List<object> { v_after__490997, F_dv_fact(v_angry__488114, false) }), new List<object> { v_source__487530 }));
                    }
                    v_j__489784 = R.add(v_j__489784, R.number("1"));
                }
                object v_reason__496275 = F_campaign_case_ref(v_group__487492, R.cat(Authored("m40.c1_other_answer_actions.literal_0004"), F_dv_get(F_c1_other_case(v_group__487492), "authority_reason")));
                object v_inference__496454 = DvValueTemplate.Instantiate("template.inline.c1_other_inquiry_scene.265", v_actor__486976, v_final__488070, v_reason__496275);
                v_mind__488428 = F_dv_adopt(v_mind__488428, F_campaign_method(R.cat(F_dv_get(v_actor__486976, "id"), Authored("m40.c1_other_inquiry_scene.literal_0016")), v_inference__496454), "infer", R.number("1"), new List<object> { v_source__487530 });
                v_actor__486976 = F_dv_with_many(v_actor__486976, new List<object> { F_dv_pair("mind", v_mind__488428), F_dv_pair("actions", v_actions__489223), F_dv_pair("policy", F_dv_with_many(F_dv_get(v_actor__486976, "policy"), new List<object> { F_dv_pair("goals", v_goals__489274), F_dv_pair("current_appraisals", v_appraisals__488597) })) });
            }
            _ = R.push(v_actors__485854, v_actor__486976);
            v_i__486918 = R.add(v_i__486918, R.number("1"));
        }
        return F_dv_with_many(v_scene, new List<object> { F_dv_pair("physical", v_world__485822), F_dv_pair("actors", v_actors__485854) });
    }
    public static object F_c1_other_resident_truth_surface(object v_row) {
        return DvValueTemplate.Instantiate("template.c1_other_resident_truth_surface", v_row);
    }
    public static object F_c1_other_resident_truth_law(object v_group,object v_row,object v_owner) {
        return DvValueTemplate.Instantiate("template.c1_other_resident_truth_law", v_group, v_row, v_owner);
    }
    public static object F_c1_other_introduction(object v_id) {
        return DvValueTemplate.Instantiate("template.c1_other_introduction", v_id);
    }
    public static object F_c1_other_access_ref(object v_group) {
        return DvValueTemplate.Instantiate("template.c1_other_access_ref", v_group);
    }
    public static object F_c1_other_threshold(object v_group) {
        return DvValueTemplate.Instantiate("template.c1_other_threshold", v_group);
    }
    public static object F_c1_other_handoff_text(object v_id) {
        return DvValueTemplate.Instantiate("template.c1_other_handoff_text", v_id);
    }
    public static object F_c1_other_handoff_action(object v_id) {
        object v_person__503171 = F_c1_other_person(v_id);
        object v_receipt__503209 = F_campaign_case_claim(F_dv_get(v_person__503171, "group"), R.cat(Authored("m40.c1_other_exclusion_hypotheses.literal_0012"), v_id));
        object v_ready__503361 = F_dv_all(F_dv_pair(F_campaign_at(v_id, F_c1_other_threshold(F_dv_get(v_person__503171, "group"))), v_receipt__503209));
        object v_text__503454 = F_c1_other_handoff_text(v_id);
        return DvValueTemplate.Instantiate("template.inline.c1_other_handoff_action.266", v_id, v_ready__503361, v_person__503171, v_receipt__503209, v_text__503454);
    }
    public static object F_c1_other_submission(object v_group,object v_option) {
        return DvValueTemplate.Instantiate("template.c1_other_submission", v_group, v_option);
    }
    public static object F_c1_other_material_ref(object v_group,object v_field) {
        return DvValueTemplate.Instantiate("template.c1_other_material_ref", v_group, v_field);
    }
    public static object F_c1_other_material(object v_group,object v_field,object v_value) {
        return DvValueTemplate.Instantiate("template.c1_other_material", v_group, v_field, v_value);
    }
    public static object F_c1_other_initial_material(object v_group) {
        return DvValueTemplate.Instantiate("template.c1_other_initial_material", v_group);
    }
    public static object F_c1_other_reporter(object v_group) {
        return DvValueTemplate.Instantiate("template.c1_other_reporter", v_group);
    }
    public static object F_c1_other_material_identity(object v_group,object v_option) {
        return DvValueTemplate.Instantiate("template.c1_other_material_identity", v_group, v_option);
    }
    public static object F_c1_other_activity_rows(object v_group) {
        object v_key__508524 = DvValueTemplate.Instantiate("template.inline.c1_other_activity_rows.267", v_group);
        object v_cached__508622 = R.memo_get(new List<object> {  }, v_key__508524);
        if (R.Bool(R.at(v_cached__508622, R.number("0"))))
        {
            return R.at(v_cached__508622, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__508524, F_c1_other_activity_declarations(v_group));
    }
    public static object F_c1_other_activity_declarations(object v_group) {
        return DvValueTemplate.Instantiate("template.c1_other_activity_declarations", v_group);
    }
    public static object F_c1_other_activity_law(object v_group,object v_row) {
        return DvValueTemplate.Instantiate("template.c1_other_activity_law", v_group, v_row);
    }
    public static object F_c1_other_activity_physical(object v_world,object v_group) {
        object v_result__531792 = v_world;
        object v_material__531816 = F_c1_other_initial_material(v_group);
        object v_i__531869 = R.number("0");
        while (R.Bool(R.lt(v_i__531869, R.count(v_material__531816))))
        {
            v_result__531792 = F_dv_store(v_result__531792, F_c1_other_material_ref(v_group, R.at(R.at(v_material__531816, v_i__531869), R.number("0"))), F_dv_field(R.at(R.at(v_material__531816, v_i__531869), R.number("1"))));
            v_i__531869 = R.add(v_i__531869, R.number("1"));
        }
        object v_rows__532045 = F_c1_other_activity_rows(v_group);
        v_i__531869 = R.number("0");
        while (R.Bool(R.lt(v_i__531869, R.count(v_rows__532045))))
        {
            v_result__531792 = F_dv_store(v_result__531792, F_campaign_case_ref(v_group, F_dv_get(R.at(v_rows__532045, v_i__531869), "id")), F_dv_field(false));
            v_result__531792 = F_dv_adopt(v_result__531792, F_campaign_method(R.cat(R.cat(Authored("m40.c1_other_activity_declarations.literal_0092"), v_group), R.cat(Authored("m40.c1_move_name.literal_0002"), F_dv_get(R.at(v_rows__532045, v_i__531869), "id"))), F_c1_other_activity_law(v_group, R.at(v_rows__532045, v_i__531869))), "enforce", R.number("1"), new List<object> { F_dv_get(F_c1_other_case(v_group), "source") });
            v_i__531869 = R.add(v_i__531869, R.number("1"));
        }
        return v_result__531792;
    }
    public static object F_c1_other_activity_actor(object v_actor,object v_group) {
        object v_result__532643 = v_actor;
        object v_mind__532667 = F_campaign_add_new(F_dv_get(v_result__532643, "mind"), F_campaign_local_object(Authored("m40.c1_campaign_scene.literal")));
        object v_source__532806 = F_dv_get(F_c1_other_case(v_group), "source");
        object v_rows__532852 = F_c1_other_activity_rows(v_group);
        object v_i__532898 = R.number("0");
        while (R.Bool(R.lt(v_i__532898, R.count(v_rows__532852))))
        {
            v_mind__532667 = F_dv_store(v_mind__532667, F_campaign_case_ref(v_group, F_dv_get(R.at(v_rows__532852, v_i__532898), "id")), F_dv_with(F_dv_field(false), "basis", new List<object> { v_source__532806 }));
            v_i__532898 = R.add(v_i__532898, R.number("1"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_activity_actor.268", v_actor)))
        {
            object v_traces__533180 = DvValueTemplate.Instantiate("template.inline.c1_other_activity_actor.269", v_group);
            object v_inference__533695 = DvValueTemplate.Instantiate("template.inline.c1_other_activity_actor.270", v_traces__533180, v_group);
            v_mind__532667 = F_dv_adopt(v_mind__532667, F_campaign_method(Authored("m40.c1_other_activity_actor.literal_0006"), v_inference__533695), "infer", Authored("m40.c1_other_activity_actor.literal_0007"), DvValueTemplate.Instantiate("template.inline.c1_other_activity_actor.271"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_activity_actor.272", v_actor)))
        {
            object v_traces__534613 = DvValueTemplate.Instantiate("template.inline.c1_other_activity_actor.273", v_group);
            object v_inference__535058 = DvValueTemplate.Instantiate("template.inline.c1_other_activity_actor.274", v_traces__534613, v_group);
            v_mind__532667 = F_dv_adopt(v_mind__532667, F_campaign_method(Authored("m40.c1_other_activity_actor.literal_0013"), v_inference__535058), "infer", Authored("m40.c1_adopt_inferences.literal"), DvValueTemplate.Instantiate("template.inline.c1_other_activity_actor.275"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_activity_actor.276", v_actor)))
        {
            v_mind__532667 = F_dv_store(v_mind__532667, F_c1_other_material_ref(v_group, Authored("m40.c1_other_moral_conflict.literal_0013")), DvValueTemplate.Instantiate("template.inline.c1_other_activity_actor.277"));
        }
        object v_actions__536430 = F_dv_clone(F_dv_get(v_result__532643, "actions"));
        object v_goals__536474 = F_dv_clone(F_dv_get(F_dv_get(v_result__532643, "policy"), "goals"));
        object v_witnesses__536521 = F_dv_append(F_c1_other_group_listeners(v_group, ""), Authored("m40.c1_talk_recalled_names.literal_0006"));
        v_i__532898 = R.number("0");
        while (R.Bool(R.lt(v_i__532898, R.count(v_rows__532852))))
        {
            object v_row__536715 = R.at(v_rows__532852, v_i__532898);
            if (R.Bool(R.eq(F_dv_get(v_row__536715, "actor"), F_dv_get(v_actor, "id"))))
            {
                object v_ref__536781 = F_campaign_case_ref(v_group, F_dv_get(v_row__536715, "id"));
                object v_method__536837 = R.cat(R.cat(Authored("m40.c1_other_activity_declarations.literal_0092"), v_group), R.cat(Authored("m40.c1_move_name.literal_0002"), F_dv_get(v_row__536715, "id")));
                object v_law__537059 = F_c1_other_activity_law(v_group, v_row__536715);
                object v_known__537116 = F_dv_all(new List<object> { F_campaign_at(F_dv_get(v_actor, "id"), F_dv_get(F_c1_other_case(v_group), "room")), F_dv_get(v_row__536715, "when"), F_dv_fact(v_ref__536781, false) });
                object v_grants__537231 = new List<object> { v_ref__536781, F_campaign_clock() };
                object v_j__537281 = R.number("0");
                while (R.Bool(R.lt(v_j__537281, R.count(F_dv_get(v_row__536715, "materials")))))
                {
                    _ = R.push(v_grants__537231, F_c1_other_material_ref(v_group, R.at(F_dv_get(v_row__536715, "materials"), v_j__537281)));
                    v_j__537281 = R.add(v_j__537281, R.number("1"));
                }
                v_mind__532667 = F_dv_adopt(v_mind__532667, F_campaign_method(v_method__536837, v_law__537059), "predict", R.number("1"), new List<object> { v_source__532806 });
                object v_activity__537549 = DvValueTemplate.Instantiate("template.inline.c1_other_activity_actor.278", v_actor, v_row__536715, v_known__537116, v_law__537059, v_method__536837, v_grants__537231, v_source__532806, v_witnesses__536521, v_group);
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_activity_actor.279", v_row__536715)))
                {
                    v_activity__537549 = DvValueTemplate.Instantiate("template.inline.c1_other_activity_actor.280", v_activity__537549, v_source__532806, v_row__536715, v_group, v_actor);
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_activity_actor.281", v_row__536715)))
                {
                    v_activity__537549 = DvValueTemplate.Instantiate("template.inline.c1_other_activity_actor.282", v_activity__537549, v_source__532806, v_row__536715, v_group, v_grants__537231);
                }
                _ = R.push(v_actions__536430, v_activity__537549);
                _ = R.push(v_goals__536474, F_campaign_goal(F_dv_fact(v_ref__536781, true), F_dv_get(v_row__536715, "when"), new List<object> { v_source__532806 }));
            }
            v_i__532898 = R.add(v_i__532898, R.number("1"));
        }
        object v_options__539591 = F_dv_get(F_c1_other_case(v_group), "options");
        v_i__532898 = R.number("0");
        while (R.Bool(R.lt(v_i__532898, R.count(v_options__539591))))
        {
            object v_option__539688 = R.at(v_options__539591, v_i__532898);
            object v_method__539721 = R.cat(R.cat(Authored("m40.c1_other_activity_actor.literal_0019"), v_group), R.cat(Authored("m40.c1_move_name.literal_0002"), v_option__539688));
            object v_inferred__539932 = DvValueTemplate.Instantiate("template.inline.c1_other_activity_actor.283", v_method__539721, v_group, v_option__539688);
            v_mind__532667 = F_dv_adopt(v_mind__532667, F_campaign_method(v_method__539721, v_inferred__539932), "infer", R.number("1"), new List<object> { v_source__532806 });
            v_i__532898 = R.add(v_i__532898, R.number("1"));
        }
        return DvValueTemplate.Instantiate("template.inline.c1_other_activity_actor.284", v_result__532643, v_mind__532667, v_actions__536430, v_goals__536474);
    }
    public static object F_c1_other_literal_report_surface(object v_group,object v_option) {
        object v_name__540544 = F_dv_get(F_c1_other_person(v_option), "name");
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_literal_report_surface.285", v_group)))
        {
            return R.cat(v_name__540544, Authored("m40.c1_other_literal_report_surface.literal"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_literal_report_surface.286", v_group)))
        {
            return R.cat(v_name__540544, Authored("m40.c1_other_literal_report_surface.literal_0002"));
        }
        return R.cat(v_name__540544, Authored("m40.c1_other_literal_report_surface.literal_0003"));
    }
    public static object F_c1_other_reason_report_surface(object v_group,object v_option,object v_reason) {
        object v_spec__541200 = F_c1_other_case(v_group);
        object v_i__541237 = R.number("0");
        while (R.Bool(R.lt(v_i__541237, R.count(F_dv_get(v_spec__541200, "reason_options")))))
        {
            if (R.Bool(R.eq(R.at(F_dv_get(v_spec__541200, "reason_options"), v_i__541237), v_reason)))
            {
                return R.cat(R.cat(F_dv_get(F_c1_other_person(v_option), "name"), Authored("m40.c1_other_reason_report_surface.literal")), R.cat(R.at(F_dv_get(v_spec__541200, "reason_surfaces"), v_i__541237), Authored("m40.c1_other_reason_report_surface.literal_0002")));
            }
            v_i__541237 = R.add(v_i__541237, R.number("1"));
        }
        _ = F_dv_require(false, Authored("m40.c1_other_reason_report_surface.literal_0003"));
        return "";
    }
    public static object F_c1_other_complete_accept(object v_group,object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_other_complete_accept", v_group, v_actor);
    }
    public static object F_c1_other_case_choice_declaration(object v_group,object v_mode,object v_condition,object v_source_refs) {
        return DvValueTemplate.Instantiate("template.c1_other_case_choice_declaration", v_group, v_mode, v_condition, v_source_refs);
    }
    public static object F_c1_other_case_choice(object v_action,object v_group,object v_mode) {
        return DvValueTemplate.Instantiate("template.c1_other_case_choice", v_action, v_group, v_mode);
    }
    public static object F_c1_other_placement_release_goals(object v_goals,object v_group,object v_release,object v_local_release,object v_manifest,object v_local_manifest) {
        object v_old_items__544659 = F_dv_get(F_dv_get(v_release, "premise"), "items");
        object v_new_items__544702 = F_dv_get(F_dv_get(v_local_release, "premise"), "items");
        object v_unmanifested__544751 = F_dv_fact(F_dv_ref(Authored("m40.c1_other_exclusion_hypotheses.literal_0023"), "state", Authored("m40.c1_other_exclusion_hypotheses.literal_0024")), false);
        object v_old_group_manifest__544981 = F_dv_all(new List<object> { R.at(v_old_items__544659, R.number("0")), R.at(v_old_items__544659, R.number("1")), R.at(v_old_items__544659, R.number("2")), R.at(v_old_items__544659, R.number("3")), v_unmanifested__544751 });
        object v_new_group_manifest__545090 = F_dv_all(new List<object> { R.at(v_new_items__544702, R.number("0")), R.at(v_new_items__544702, R.number("1")), R.at(v_new_items__544702, R.number("2")), R.at(v_new_items__544702, R.number("3")), v_unmanifested__544751 });
        object v_receipt_ref__545199 = F_campaign_case_ref(v_group, Authored("m40.c1_other_placement_release_goals.literal"));
        object v_result__545332 = new List<object> {  };
        object v_i__545353 = R.number("0");
        while (R.Bool(R.lt(v_i__545353, R.count(v_goals))))
        {
            object v_goal__545404 = R.at(v_goals, v_i__545353);
            object v_condition__545433 = F_dv_find(v_goal__545404, "available_when", F_dv_true());
            if (R.Bool(F_dv_same(v_condition__545433, F_dv_get(v_release, "premise"))))
            {
                v_goal__545404 = F_campaign_index_goal_window(F_dv_with(v_goal__545404, "available_when", F_dv_get(v_local_release, "premise")));
            }
            if (R.Bool(F_dv_same(v_condition__545433, F_dv_get(v_manifest, "premise"))))
            {
                v_goal__545404 = F_campaign_index_goal_window(F_dv_with(v_goal__545404, "available_when", F_dv_get(v_local_manifest, "premise")));
            }
            if (R.Bool(F_dv_same(v_condition__545433, v_old_group_manifest__544981)))
            {
                v_goal__545404 = F_campaign_index_goal_window(F_dv_with(v_goal__545404, "available_when", v_new_group_manifest__545090));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_placement_release_goals.287", v_goal__545404, v_group)))
            {
                v_goal__545404 = F_dv_with(v_goal__545404, "refresh_refs", F_dvp_add_ref(F_dv_get(v_goal__545404, "refresh_refs"), v_receipt_ref__545199));
            }
            _ = R.push(v_result__545332, v_goal__545404);
            v_i__545353 = R.add(v_i__545353, R.number("1"));
        }
        _ = R.push(v_result__545332, F_campaign_observation_goal(v_receipt_ref__545199, F_campaign_at(Authored("m40.c1_observe_as.literal_0005"), Authored("m40.c1_sound_agreement.literal")), new List<object> { F_dv_get(F_c1_other_case(v_group), "source") }, R.number("0")));
        return v_result__545332;
    }
    public static object F_c1_other_place_detective(object v_scene,object v_zone,object v_profile) {
        object v_group__546783 = v_zone;
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.288", v_zone)))
        {
            v_group__546783 = Authored("m40.c1_other_people_build.literal_0004");
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.289", v_zone)))
        {
            v_group__546783 = Authored("m40.c1_other_environment_install_scene.literal_0008");
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.290", v_zone)))
        {
            v_group__546783 = Authored("m40.c1_other_environment_install_scene.literal_0016");
        }
        _ = F_dv_require(F_dv_has(AuthoredCopy("m40.c1_campaign_scene.groups"), v_group__546783), Authored("m40.c1_other_place_detective.literal_0007"));
        object v_placement_source__547878 = F_dv_get(F_c1_other_case(v_group__546783), "source");
        object v_current_ref__547934 = F_dv_ref(Authored("m40.c1_other_place_detective.literal_0008"), "state", Authored("m40.c1_reask_question.literal_0002"));
        object v_opening__548130 = F_c1_other_question_open(v_group__546783);
        object v_local_opening__548179 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.291", v_opening__548130, v_current_ref__547934, v_group__546783);
        object v_release__548386 = F_c1_other_release(v_group__546783, F_campaign_clock());
        object v_reported__548447 = F_campaign_any(R.at(F_dv_get(F_dv_get(v_release__548386, "premise"), "items"), R.number("0")), F_campaign_case_claim(v_group__546783, Authored("m40.c1_other_placement_release_goals.literal")));
        object v_local_release__548619 = F_dv_with(v_release__548386, "premise", F_dv_all(F_dvs_replace(F_dv_get(F_dv_get(v_release__548386, "premise"), "items"), R.number("0"), v_reported__548447)));
        object v_manifest__548729 = F_c1dep_manifest_law();
        object v_detective_manifest__548770 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.292", v_group__546783, v_release__548386);
        object v_local_manifest__549103 = F_dv_with(v_manifest__548729, "premise", F_dv_all(F_dvs_replace(F_dv_get(F_dv_get(v_manifest__548729, "premise"), "items"), R.number("1"), F_campaign_any(R.at(F_dv_get(F_dv_get(v_manifest__548729, "premise"), "items"), R.number("1")), v_detective_manifest__548770))));
        object v_world__549267 = F_dv_store(F_dv_get(v_scene, "physical"), F_dv_ref(Authored("m40.c1_talk_recalled_names.literal_0006"), "state", "location"), F_dv_field(F_dv_get(F_c1_other_case(v_group__546783), "room")));
        v_world__549267 = F_campaign_add_new(v_world__549267, F_campaign_state_object(Authored("m40.c1_other_place_detective.literal_0008"), new List<object> { F_dv_pair("current", F_dv_field("")) }));
        v_world__549267 = F_dv_reconsider(v_world__549267, F_campaign_method(R.cat(Authored("m40.c1_other_open_action.literal_0002"), v_group__546783), v_local_opening__548179), "enforce", R.number("1"), new List<object> { v_placement_source__547878 });
        v_world__549267 = F_dv_reconsider(v_world__549267, F_campaign_method(R.cat(Authored("m40.c1_other_release_action.literal_0002"), v_group__546783), v_local_release__548619), "enforce", R.number("1"), new List<object> { v_placement_source__547878 });
        v_world__549267 = F_dv_reconsider(v_world__549267, F_campaign_method(Authored("m40.c1_other_place_detective.literal_0009"), v_local_manifest__549103), "enforce", R.number("1"), new List<object> { v_placement_source__547878 });
        object v_actors__550196 = new List<object> {  };
        object v_bindings__550217 = F_dv_clone(F_dv_get(v_scene, "bindings"));
        object v_answers__550262 = F_dv_clone(F_dv_get(v_scene, "formal_answers"));
        v_world__549267 = F_dv_store(v_world__549267, F_campaign_case_ref(v_group__546783, Authored("m40.c1_other_placement_release_goals.literal")), F_dv_field(false));
        v_world__549267 = F_dv_store(v_world__549267, F_campaign_case_ref(v_group__546783, Authored("m40.c1_other_place_detective.literal_0010")), F_dv_field(R.number("0")));
        v_world__549267 = F_dv_store(v_world__549267, F_campaign_case_ref(v_group__546783, Authored("m40.c1_other_place_detective.literal_0011")), F_dv_field(false));
        v_world__549267 = F_dv_store(v_world__549267, F_campaign_case_ref(v_group__546783, Authored("m40.c1_other_place_detective.literal_0012")), F_dv_field(R.number("0")));
        v_world__549267 = F_dv_adopt(v_world__549267, F_campaign_method(R.cat(Authored("m40.c1_other_formal_answers.literal"), R.cat(v_group__546783, Authored("m40.c1_other_place_detective.literal_0013"))), F_c1_other_accept(v_group__546783, Authored("m40.c1_talk_recalled_names.literal_0006"))), "enforce", R.number("1"), new List<object> { F_dv_get(F_c1_other_case(v_group__546783), "source") });
        v_world__549267 = F_dv_adopt(v_world__549267, F_campaign_method(R.cat(Authored("m40.c1_other_formal_answers.literal_0002"), R.cat(v_group__546783, Authored("m40.c1_other_place_detective.literal_0013"))), F_c1_other_complete_accept(v_group__546783, Authored("m40.c1_talk_recalled_names.literal_0006"))), "enforce", R.number("1"), new List<object> { v_placement_source__547878 });
        if (R.Bool(R.eq(F_dv_local(v_bindings__550217, Authored("m40.c1_other_place_detective.literal_0008"), Authored("m40.c1_talk_recalled_names.literal_0006")), "")))
        {
            _ = R.push(v_bindings__550217, F_dv_binding(Authored("m40.c1_other_place_detective.literal_0008"), Authored("m40.c1_other_place_detective.literal_0008"), Authored("m40.c1_talk_recalled_names.literal_0006")));
        }
        object v_i__552157 = R.number("0");
        while (R.Bool(R.lt(v_i__552157, R.count(F_dv_get(v_scene, "actors")))))
        {
            object v_actor__552215 = R.at(F_dv_get(v_scene, "actors"), v_i__552157);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.293", v_actor__552215)))
            {
                object v_question_actions__552351 = new List<object> {  };
                object v_question_index__552390 = R.number("0");
                while (R.Bool(R.lt(v_question_index__552390, R.count(F_dv_get(v_actor__552215, "actions")))))
                {
                    object v_question_action__552491 = R.at(F_dv_get(v_actor__552215, "actions"), v_question_index__552390);
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.294", v_question_action__552491, v_group__546783)))
                    {
                        v_question_action__552491 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.295", v_question_action__552491, v_current_ref__547934);
                    }
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.296", v_question_action__552491, v_group__546783)))
                    {
                        v_question_action__552491 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.297", v_question_action__552491, v_local_release__548619);
                    }
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.298", v_question_action__552491)))
                    {
                        v_question_action__552491 = F_dv_with_many(v_question_action__552491, new List<object> { F_dv_pair("precondition", F_dv_get(v_local_manifest__549103, "premise")), F_dv_pair("authority_precondition", F_dv_get(v_local_manifest__549103, "premise")) });
                    }
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.299", v_question_action__552491, v_group__546783)))
                    {
                        object v_receipt_ref__553820 = F_campaign_case_ref(v_group__546783, Authored("m40.c1_other_placement_release_goals.literal"));
                        v_question_action__552491 = F_dv_with_many(v_question_action__552491, new List<object> { F_dv_pair("grants", F_dvp_add_ref(F_dv_get(v_question_action__552491, "grants"), v_receipt_ref__553820)), F_dv_pair("purpose_refs", F_dvp_add_ref(F_dv_find(v_question_action__552491, Authored("m40.c1_public_method.literal"), F_dv_get(v_question_action__552491, "grants")), v_receipt_ref__553820)) });
                    }
                    _ = R.push(v_question_actions__552351, v_question_action__552491);
                    v_question_index__552390 = R.add(v_question_index__552390, R.number("1"));
                }
                object v_demon_mind__554366 = F_campaign_add_new(F_dv_get(v_actor__552215, "mind"), F_campaign_local_object(Authored("m40.c1_other_place_detective.literal_0008")));
                v_demon_mind__554366 = F_dv_reconsider(v_demon_mind__554366, F_campaign_method(R.cat(Authored("m40.c1_other_open_action.literal_0002"), v_group__546783), v_local_opening__548179), "predict", R.number("1"), new List<object> { v_placement_source__547878 });
                v_demon_mind__554366 = F_dv_reconsider(v_demon_mind__554366, F_campaign_method(R.cat(Authored("m40.c1_other_release_action.literal_0002"), v_group__546783), v_local_release__548619), "predict", R.number("1"), new List<object> { v_placement_source__547878 });
                v_demon_mind__554366 = F_dv_reconsider(v_demon_mind__554366, F_campaign_method(Authored("m40.c1_other_place_detective.literal_0009"), v_local_manifest__549103), "predict", R.number("1"), new List<object> { v_placement_source__547878 });
                object v_demon_hearing__555156 = F_dv_clone(F_dv_find(v_actor__552215, Authored("m40.c1_sound_install_scene.literal_0009"), new List<object> {  }));
                object v_channels__555298 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.300", v_group__546783);
                object v_channel_index__555507 = R.number("0");
                while (R.Bool(R.lt(v_channel_index__555507, R.count(v_channels__555298))))
                {
                    _ = R.push(v_demon_hearing__555156, DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.301", v_channels__555298, v_channel_index__555507, v_group__546783, v_placement_source__547878));
                    v_channel_index__555507 = R.add(v_channel_index__555507, R.number("1"));
                }
                if (R.Bool(R.eq(F_dv_local(v_bindings__550217, Authored("m40.c1_other_place_detective.literal_0008"), Authored("m40.c1_observe_as.literal_0005")), "")))
                {
                    _ = R.push(v_bindings__550217, F_dv_binding(Authored("m40.c1_other_place_detective.literal_0008"), Authored("m40.c1_other_place_detective.literal_0008"), Authored("m40.c1_observe_as.literal_0005")));
                }
                object v_demon_policy__556662 = F_dv_get(v_actor__552215, "policy");
                object v_goal_fields__556707 = AuthoredCopy("m40.c1_other_place_detective.goal_fields");
                object v_field_index__556833 = R.number("0");
                while (R.Bool(R.lt(v_field_index__556833, R.count(v_goal_fields__556707))))
                {
                    object v_field__556926 = R.at(v_goal_fields__556707, v_field_index__556833);
                    if (R.Bool(F_dv_has_key(v_demon_policy__556662, v_field__556926)))
                    {
                        v_demon_policy__556662 = F_dv_with(v_demon_policy__556662, v_field__556926, F_c1_other_placement_release_goals(F_dv_get(v_demon_policy__556662, v_field__556926), v_group__546783, v_release__548386, v_local_release__548619, v_manifest__548729, v_local_manifest__549103));
                    }
                    v_field_index__556833 = R.add(v_field_index__556833, R.number("1"));
                }
                v_actor__552215 = F_dv_with_many(v_actor__552215, new List<object> { F_dv_pair("mind", v_demon_mind__554366), F_dv_pair("actions", v_question_actions__552351), F_dv_pair("hearing_grants", v_demon_hearing__555156), F_dv_pair("policy", v_demon_policy__556662) });
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.302", v_actor__552215)))
            {
                object v_opening_actions__557697 = new List<object> {  };
                object v_opening_index__557735 = R.number("0");
                while (R.Bool(R.lt(v_opening_index__557735, R.count(F_dv_get(v_actor__552215, "actions")))))
                {
                    object v_opening_action__557834 = R.at(F_dv_get(v_actor__552215, "actions"), v_opening_index__557735);
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.303", v_opening_action__557834)))
                    {
                        v_opening_action__557834 = F_dv_without(v_opening_action__557834, Authored("m40.c1_professional_replies.literal"));
                        v_opening_action__557834 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.304", v_opening_action__557834);
                    }
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.305", v_opening_action__557834)))
                    {
                        v_opening_action__557834 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.306", v_opening_action__557834);
                    }
                    _ = R.push(v_opening_actions__557697, v_opening_action__557834);
                    v_opening_index__557735 = R.add(v_opening_index__557735, R.number("1"));
                }
                v_actor__552215 = F_dv_with(v_actor__552215, "actions", v_opening_actions__557697);
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.307", v_actor__552215)))
            {
                object v_mind__559727 = F_dv_store(F_dv_get(v_actor__552215, "mind"), F_dv_ref(Authored("m40.c1_talk_recalled_names.literal_0006"), "state", "location"), F_dv_with(F_dv_field(F_dv_get(F_c1_other_case(v_group__546783), "room")), "basis", new List<object> { F_dv_get(F_c1_other_case(v_group__546783), "source") }));
                v_mind__559727 = F_campaign_add_new(v_mind__559727, F_campaign_local_object(R.cat(Authored("m40.c1_other_mind.literal"), v_group__546783)));
                v_mind__559727 = F_campaign_add_new(v_mind__559727, F_campaign_local_object(Authored("m40.c1_other_place_detective.literal_0008")));
                object v_here__560262 = F_campaign_at(Authored("m40.c1_talk_recalled_names.literal_0006"), F_dv_get(F_c1_other_case(v_group__546783), "room"));
                object v_hearing__560405 = F_dv_append(F_dv_find(v_actor__552215, Authored("m40.c1_sound_install_scene.literal_0009"), new List<object> {  }), DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.308", v_group__546783, v_here__560262, v_placement_source__547878));
                object v_reason_topic__560949 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.309", v_group__546783, v_placement_source__547878);
                object v_policy__561377 = F_dv_with(F_dv_get(v_actor__552215, "policy"), "topic_bindings", F_dv_append(F_dv_find(F_dv_get(v_actor__552215, "policy"), "topic_bindings", new List<object> {  }), v_reason_topic__560949));
                v_actor__552215 = F_dv_with_many(v_actor__552215, new List<object> { F_dv_pair("mind", v_mind__559727), F_dv_pair("location", F_dv_get(F_c1_other_case(v_group__546783), "room")), F_dv_pair("question_ref", v_current_ref__547934), F_dv_pair("hearing_grants", v_hearing__560405), F_dv_pair("policy", v_policy__561377) });
                v_actor__552215 = F_c1_other_activity_actor(v_actor__552215, v_group__546783);
                object v_peers__561742 = F_c1_other_people();
                object v_peer_index__561785 = R.number("0");
                while (R.Bool(R.lt(v_peer_index__561785, R.count(v_peers__561742))))
                {
                    object v_peer__561870 = R.at(v_peers__561742, v_peer_index__561785);
                    if (R.Bool(R.eq(F_dv_get(v_peer__561870, "group"), v_group__546783)))
                    {
                        v_mind__559727 = F_campaign_add_new(F_dv_get(v_actor__552215, "mind"), F_campaign_local_object(F_dv_get(v_peer__561870, "id")));
                        if (R.Bool(R.eq(F_dv_local(v_bindings__550217, F_dv_get(v_peer__561870, "id"), Authored("m40.c1_talk_recalled_names.literal_0006")), "")))
                        {
                            _ = R.push(v_bindings__550217, F_dv_binding(F_dv_get(v_peer__561870, "id"), F_dv_get(v_peer__561870, "id"), Authored("m40.c1_talk_recalled_names.literal_0006")));
                        }
                        object v_ready__562342 = F_campaign_at(Authored("m40.c1_talk_recalled_names.literal_0006"), F_dv_get(F_c1_other_case(v_group__546783), "room"));
                        object v_seen__562494 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.310", v_peer__561870, v_ready__562342, v_group__546783);
                        v_actor__552215 = F_dv_with_many(v_actor__552215, new List<object> { F_dv_pair("mind", v_mind__559727), F_dv_pair("actions", F_dv_append(F_dv_get(v_actor__552215, "actions"), v_seen__562494)), F_dv_pair("policy", F_dv_with(F_dv_get(v_actor__552215, "policy"), "goals", F_dv_append(F_dv_get(F_dv_get(v_actor__552215, "policy"), "goals"), F_campaign_observation_goal(F_dv_ref(F_dv_get(v_peer__561870, "id"), "state", "location"), v_ready__562342, new List<object> { F_dv_get(F_c1_other_case(v_group__546783), "source") }, R.number("0"))))) });
                    }
                    v_peer_index__561785 = R.add(v_peer_index__561785, R.number("1"));
                }
                object v_source__563326 = F_dv_get(F_c1_other_case(v_group__546783), "source");
                object v_public_refs__563380 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.311", v_group__546783, v_current_ref__547934);
                object v_recheck__564130 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.312", v_here__560262, v_public_refs__563380, v_source__563326);
                object v_local_actions__564564 = F_dv_append(F_dv_get(v_actor__552215, "actions"), v_recheck__564130);
                object v_local_goals__564631 = F_dv_clone(F_dv_get(F_dv_get(v_actor__552215, "policy"), "goals"));
                _ = R.push(v_local_goals__564631, F_campaign_observation_goal(v_current_ref__547934, v_here__560262, new List<object> { v_source__563326 }, R.number("0")));
                _ = R.push(v_local_goals__564631, F_campaign_goal(F_campaign_case_claim(v_group__546783, Authored("m40.c1_other_placement_release_goals.literal")), DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.313", v_group__546783), new List<object> { v_source__563326 }));
                _ = R.push(v_local_goals__564631, F_campaign_goal(F_campaign_case_claim(v_group__546783, Authored("m40.c1_other_place_detective.literal_0011")), F_campaign_case_claim(v_group__546783, Authored("m40.c1_other_placement_release_goals.literal")), new List<object> { v_source__563326 }));
                object v_exit_ready__565374 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.314", v_group__546783);
                _ = R.push(v_local_goals__564631, F_campaign_observation_goal(F_campaign_case_ref(v_group__546783, Authored("m40.c1_other_exclusion_hypotheses.literal_0026")), v_here__560262, new List<object> { v_source__563326 }, R.number("0")));
                _ = R.push(v_local_goals__564631, F_campaign_observation_goal(F_c1_other_access_ref(v_group__546783), v_here__560262, new List<object> { v_source__563326 }, R.number("0")));
                _ = R.push(v_local_goals__564631, F_campaign_goal(F_campaign_at(Authored("m40.c1_talk_recalled_names.literal_0006"), F_c1_other_threshold(v_group__546783)), F_dv_all(new List<object> { v_here__560262, v_exit_ready__565374 }), new List<object> { v_source__563326 }));
                _ = R.push(v_local_goals__564631, F_campaign_goal(F_campaign_at(Authored("m40.c1_talk_recalled_names.literal_0006"), Authored("m40.c1_sound_agreement.literal")), v_exit_ready__565374, new List<object> { v_source__563326 }));
                object v_options__566314 = F_dv_get(F_c1_other_case(v_group__546783), "options");
                object v_truth_rows__566370 = F_c1_other_truth_rows(v_group__546783);
                object v_final_truth__566427 = F_campaign_case_claim(v_group__546783, F_dv_get(R.at(v_truth_rows__566370, R.sub(R.count(v_truth_rows__566370), R.number("1"))), "id"));
                object v_option_index__566526 = R.number("0");
                while (R.Bool(R.lt(v_option_index__566526, R.count(v_options__566314))))
                {
                    object v_option__566617 = R.at(v_options__566314, v_option_index__566526);
                    object v_literal__566669 = F_c1_other_submission(v_group__546783, v_option__566617);
                    object v_answer_id__566735 = R.cat(Authored("m40.c1_other_place_detective.literal_0027"), R.cat(v_group__546783, R.cat(Authored("m40.c1_move_name.literal_0002"), v_option__566617)));
                    object v_answer_ready__566958 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.315", v_here__560262, v_group__546783, v_literal__566669);
                    object v_surface__567144 = F_c1_other_literal_report_surface(v_group__546783, v_option__566617);
                    object v_answer_action__567222 = F_dv_with_many(F_campaign_surface_action(v_answer_id__566735, "answer", v_answer_ready__566958, v_answer_ready__566958, "", new List<object> {  }, R.cat(Authored("m40.c1_other_answer_actions.literal_0002"), F_dv_get(F_c1_other_person(v_option__566617), "name")), v_surface__567144, v_source__563326), DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.316", v_literal__566669, v_answer_id__566735, v_group__546783, v_source__563326, v_surface__567144));
                    _ = R.push(v_local_actions__564564, F_c1_other_case_choice(v_answer_action__567222, v_group__546783, Authored("post.c1_other_place_detective.0001")));
                    _ = R.push(v_answers__550262, DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.317", v_answer_id__566735, v_group__546783, v_literal__566669, v_current_ref__547934, v_source__563326));
                    object v_reasons__569565 = F_dv_get(F_c1_other_case(v_group__546783), "reason_options");
                    object v_reason_index__569632 = R.number("0");
                    while (R.Bool(R.lt(v_reason_index__569632, R.count(v_reasons__569565))))
                    {
                        object v_reason__569731 = R.at(v_reasons__569565, v_reason_index__569632);
                        object v_complete__569787 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.318", v_literal__566669, v_final_truth__566427, v_group__546783, v_reason__569731);
                        object v_complete_id__569979 = R.cat(v_answer_id__566735, R.cat(Authored("m40.c1_other_answer_actions.literal_0005"), v_reason__569731));
                        object v_ready__570133 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.319", v_answer_ready__566958, v_group__546783, v_complete__569787);
                        object v_complete_surface__570312 = F_c1_other_reason_report_surface(v_group__546783, v_option__566617, v_reason__569731);
                        object v_complete_action__570410 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.320", v_complete_id__569979, v_ready__570133, v_complete_surface__570312, v_source__563326, v_complete__569787, v_group__546783);
                        _ = R.push(v_local_actions__564564, F_c1_other_case_choice(v_complete_action__570410, v_group__546783, Authored("m40.c1_palm_invitation_action.next")));
                        _ = R.push(v_answers__550262, DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.321", v_complete_id__569979, v_group__546783, v_complete__569787, v_current_ref__547934, v_source__563326));
                        v_reason_index__569632 = R.add(v_reason_index__569632, R.number("1"));
                    }
                    v_option_index__566526 = R.add(v_option_index__566526, R.number("1"));
                }
                v_actor__552215 = F_dv_with_many(v_actor__552215, new List<object> { F_dv_pair("actions", v_local_actions__564564), F_dv_pair("policy", F_dv_with(F_dv_get(v_actor__552215, "policy"), "goals", v_local_goals__564631)) });
                object v_origins__573147 = new List<object> { F_dv_get(F_c1_other_case(v_group__546783), "room"), F_c1_other_threshold(v_group__546783) };
                object v_targets__573229 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.322", v_group__546783);
                object v_move_index__573355 = R.number("0");
                while (R.Bool(R.lt(v_move_index__573355, R.count(v_origins__573147))))
                {
                    object v_move_id__573442 = R.cat(Authored("m40.c1_other_place_detective.literal_0031"), R.cat(v_group__546783, R.cat(Authored("m40.c1_move_name.literal_0002"), R.at(v_origins__573147, v_move_index__573355))));
                    object v_pre__573676 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.323", v_origins__573147, v_move_index__573355, v_group__546783);
                    object v_law__573984 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.324", v_move_id__573442, v_pre__573676, v_targets__573229, v_move_index__573355);
                    v_world__549267 = F_dv_adopt(v_world__549267, F_campaign_method(v_move_id__573442, v_law__573984), "enforce", R.number("1"), new List<object> { v_source__563326 });
                    v_mind__559727 = F_dv_adopt(F_dv_get(v_actor__552215, "mind"), F_campaign_method(v_move_id__573442, v_law__573984), "predict", R.number("1"), new List<object> { v_source__563326 });
                    object v_action__574376 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.325", v_move_id__573442, v_pre__573676, v_source__563326, v_origins__573147, v_move_index__573355, v_targets__573229, v_group__546783);
                    object v_exit_actions__574872 = F_dv_append(F_dv_get(v_actor__552215, "actions"), F_c1_other_case_choice(v_action__574376, v_group__546783, Authored("m40.c1_other_case_choice_declaration.literal")));
                    object v_accounted__575236 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.326", v_pre__573676, v_group__546783);
                    object v_after_account__575393 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.327", v_action__574376, v_move_id__573442, v_accounted__575236);
                    v_actor__552215 = F_dv_with_many(v_actor__552215, new List<object> { F_dv_pair("mind", v_mind__559727), F_dv_pair("actions", F_dv_append(v_exit_actions__574872, v_after_account__575393)) });
                    v_move_index__573355 = R.add(v_move_index__573355, R.number("1"));
                }
                object v_truth__575775 = F_c1_other_truth_rows(v_group__546783);
                object v_j__575827 = R.number("0");
                while (R.Bool(R.lt(v_j__575827, R.count(v_truth__575775))))
                {
                    object v_row__575894 = R.at(v_truth__575775, v_j__575827);
                    v_world__549267 = F_dv_store(v_world__549267, F_campaign_case_ref(v_group__546783, F_dv_get(v_row__575894, "id")), F_dv_field(false));
                    object v_method__576022 = R.cat(Authored("m40.c1_other_place_detective.literal_0035"), F_dv_get(v_row__575894, "id"));
                    object v_law__576149 = F_c1_other_truth_law(v_group__546783, v_row__575894);
                    v_world__549267 = F_dv_adopt(v_world__549267, F_campaign_method(v_method__576022, v_law__576149), "enforce", R.number("1"), new List<object> { F_dv_get(F_c1_other_case(v_group__546783), "source") });
                    v_mind__559727 = F_dv_get(v_actor__552215, "mind");
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.328", v_row__575894)))
                    {
                        v_mind__559727 = F_dv_store(v_mind__559727, F_campaign_case_ref(v_group__546783, F_dv_get(v_row__575894, "id")), F_dv_with(F_dv_field(false), "basis", new List<object> { F_dv_get(F_c1_other_case(v_group__546783), "source") }));
                    }
                    v_mind__559727 = F_dv_adopt(v_mind__559727, F_campaign_method(v_method__576022, v_law__576149), "predict", R.number("1"), new List<object> { F_dv_get(F_c1_other_case(v_group__546783), "source") });
                    object v_action__576731 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.329", v_method__576022, v_row__575894, v_law__576149, v_group__546783);
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.330", v_row__575894)))
                    {
                        v_action__576731 = F_dv_with_many(v_action__576731, new List<object> { F_dv_pair("public_observers", F_c1_other_group_listeners(v_group__546783, "")), F_dv_pair("public_grants", new List<object> { F_campaign_case_ref(v_group__546783, F_dv_get(v_row__575894, "id")) }) });
                    }
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.331", v_row__575894)))
                    {
                        object v_after__577451 = F_campaign_case_claim(v_group__546783, F_dv_get(v_row__575894, "after"));
                        v_action__576731 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.332", v_row__575894, v_group__546783, v_after__577451);
                    }
                    object v_actions__578766 = F_dv_append(F_dv_get(v_actor__552215, "actions"), F_c1_other_case_choice(v_action__576731, v_group__546783, Authored("m40.c1_other_place_detective.literal_0037")));
                    object v_goals__578941 = F_dv_append(F_dv_get(F_dv_get(v_actor__552215, "policy"), "goals"), F_campaign_goal(F_campaign_case_claim(v_group__546783, F_dv_get(v_row__575894, "id")), F_campaign_at(Authored("m40.c1_talk_recalled_names.literal_0006"), F_dv_get(F_c1_other_case(v_group__546783), "room")), new List<object> { F_dv_get(F_c1_other_case(v_group__546783), "source") }));
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.333", v_row__575894)))
                    {
                        _ = R.push(v_goals__578941, DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.334", v_action__576731, v_group__546783));
                    }
                    v_actor__552215 = F_dv_with_many(v_actor__552215, new List<object> { F_dv_pair("mind", v_mind__559727), F_dv_pair("actions", v_actions__578766), F_dv_pair("policy", F_dv_with(F_dv_get(v_actor__552215, "policy"), "goals", v_goals__578941)) });
                    v_j__575827 = R.add(v_j__575827, R.number("1"));
                }
                object v_last__579817 = R.at(v_truth__575775, R.sub(R.count(v_truth__575775), R.number("1")));
                object v_reason__579868 = F_campaign_case_ref(v_group__546783, R.cat(Authored("m40.c1_other_answer_actions.literal_0004"), F_dv_get(F_c1_other_case(v_group__546783), "authority_reason")));
                object v_inference__580047 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.335", v_group__546783, v_last__579817, v_reason__579868);
                v_actor__552215 = F_dv_with(v_actor__552215, "mind", F_dv_adopt(F_dv_get(v_actor__552215, "mind"), F_campaign_method(R.cat(Authored("m40.c1_other_place_detective.literal_0038"), v_group__546783), v_inference__580047), "infer", R.number("1"), new List<object> { F_dv_get(F_c1_other_case(v_group__546783), "source") }));
                if (R.Bool(R.eq(F_dv_local(v_bindings__550217, R.cat(Authored("m40.c1_other_mind.literal"), v_group__546783), Authored("m40.c1_talk_recalled_names.literal_0006")), "")))
                {
                    _ = R.push(v_bindings__550217, F_dv_binding(R.cat(Authored("m40.c1_other_mind.literal"), v_group__546783), R.cat(Authored("m40.c1_other_mind.literal"), v_group__546783), Authored("m40.c1_talk_recalled_names.literal_0006")));
                }
            }
            _ = R.push(v_actors__550196, v_actor__552215);
            v_i__552157 = R.add(v_i__552157, R.number("1"));
        }
        object v_respondents__581018 = new List<object> {  };
        object v_truth__581044 = F_c1_other_truth_rows(v_group__546783);
        v_i__552157 = R.number("0");
        while (R.Bool(R.lt(v_i__552157, R.count(v_actors__550196))))
        {
            object v_actor__581136 = R.at(v_actors__550196, v_i__552157);
            object v_j__581167 = R.number("0");
            while (R.Bool(R.lt(v_j__581167, R.count(v_truth__581044))))
            {
                object v_row__581226 = R.at(v_truth__581044, v_j__581167);
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.336", v_row__581226, v_actor__581136)))
                {
                    object v_source__581402 = F_dv_get(F_c1_other_case(v_group__546783), "source");
                    object v_ref__581460 = F_campaign_case_ref(v_group__546783, F_dv_get(v_row__581226, "id"));
                    object v_method__581520 = R.cat(Authored("m40.c1_other_place_detective.literal_0035"), F_dv_get(v_row__581226, "id"));
                    object v_law__581647 = F_c1_other_truth_law(v_group__546783, v_row__581226);
                    object v_mind__581705 = F_dv_store(F_dv_get(v_actor__581136, "mind"), v_ref__581460, F_dv_with(F_dv_field(false), "basis", new List<object> { v_source__581402 }));
                    v_mind__581705 = F_dv_adopt(v_mind__581705, F_campaign_method(v_method__581520, v_law__581647), "predict", R.number("1"), new List<object> { v_source__581402 });
                    object v_recall__581897 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.337", v_actor__581136, v_row__581226, v_law__581647, v_method__581520, v_ref__581460, v_source__581402);
                    object v_claim__582618 = F_dv_fact(v_ref__581460, true);
                    object v_reply__582666 = DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.338", v_actor__581136, v_row__581226, v_claim__582618, v_group__546783, v_source__581402);
                    object v_actions__584024 = F_dv_append(F_dv_append(F_dv_get(v_actor__581136, "actions"), v_recall__581897), v_reply__582666);
                    object v_goals__584106 = F_dv_append(F_dv_get(F_dv_get(v_actor__581136, "policy"), "goals"), F_campaign_goal(v_claim__582618, F_campaign_case_claim(v_group__546783, F_dv_get(v_row__581226, "after")), new List<object> { v_source__581402 }));
                    _ = R.push(v_goals__584106, DvValueTemplate.Instantiate("template.inline.c1_other_place_detective.339", v_reply__582666, v_claim__582618, v_source__581402));
                    v_actor__581136 = F_dv_with_many(v_actor__581136, new List<object> { F_dv_pair("mind", v_mind__581705), F_dv_pair("actions", v_actions__584024), F_dv_pair("policy", F_dv_with(F_dv_get(v_actor__581136, "policy"), "goals", v_goals__584106)) });
                }
                v_j__581167 = R.add(v_j__581167, R.number("1"));
            }
            _ = R.push(v_respondents__581018, v_actor__581136);
            v_i__552157 = R.add(v_i__552157, R.number("1"));
        }
        return F_dv_with_many(v_scene, new List<object> { F_dv_pair("physical", v_world__549267), F_dv_pair("actors", v_respondents__581018), F_dv_pair("bindings", v_bindings__550217), F_dv_pair("detective_start_group", v_group__546783), F_dv_pair("formal_answers", v_answers__550262) });
    }
    public static object F_c1_other_truth_rows(object v_group) {
        return DvValueTemplate.Instantiate("template.c1_other_truth_rows", v_group);
    }
    public static object F_c1_other_truth_law(object v_group,object v_row) {
        return DvValueTemplate.Instantiate("template.c1_other_truth_law", v_group, v_row);
    }
    public static object F_c1dep_source(object v_file,object v_line,object v_id) {
        return DvValueTemplate.Instantiate("template.c1dep_source", v_file, v_line, v_id);
    }
    public static object F_c1dep_ref(object v_field) {
        return DvValueTemplate.Instantiate("template.c1dep_ref", v_field);
    }
    public static object F_c1dep_cross_ref(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1dep_cross_ref", v_actor);
    }
    public static object F_c1dep_complete() {
        return DvValueTemplate.Instantiate("template.c1dep_complete");
    }
    public static object F_c1dep_can_cross() {
        return DvValueTemplate.Instantiate("template.c1dep_can_cross");
    }
    public static object F_c1dep_outer_law() {
        return DvValueTemplate.Instantiate("template.c1dep_outer_law");
    }
    public static object F_c1dep_pick_law() {
        return DvValueTemplate.Instantiate("template.c1dep_pick_law");
    }
    public static object F_c1dep_pick_contract_law() {
        return DvValueTemplate.Instantiate("template.c1dep_pick_contract_law");
    }
    public static object F_c1dep_inner_law(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c1dep_inner_law", v_行动者);
    }
    public static object F_c1dep_open_law(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c1dep_open_law", v_行动者);
    }
    public static object F_c1dep_cross_law(object v_actor,object v_outward) {
        return DvValueTemplate.Instantiate("template.c1dep_cross_law", v_actor, v_outward);
    }
    public static object F_c1dep_join_law(object v_actor,object v_group) {
        return DvValueTemplate.Instantiate("template.c1dep_join_law", v_actor, v_group);
    }
    public static object F_c1dep_speech(object v_actor,object v_id,object v_room,object v_when,object v_literal,object v_label,object v_text,object v_source) {
        return DvValueTemplate.Instantiate("template.c1dep_speech", v_actor, v_id, v_room, v_when, v_literal, v_label, v_text, v_source);
    }
    public static object F_c1dep_cooperation_actions(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1dep_cooperation_actions", v_actor);
    }
    public static object F_c1dep_actions(object v_actor) {
        object v_actions__598145 = F_c1dep_cooperation_actions(v_actor);
        object v_source__598197 = F_c1dep_source(Authored("m40.c1dep_actions.literal"), Authored("m40.c1_raw_person_policy.literal_0004"), Authored("m40.c1dep_actions.literal_0002"));
        object v_four__598443 = AuthoredCopy("m40.c1_sound_agreement.literal_0006");
        if (R.Bool(F_dv_has(v_four__598443, v_actor)))
        {
            object v_fields__598582 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.340");
            _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.341", v_actor, v_fields__598582));
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_actions.342", v_actor)))
            {
                object v_inner__601561 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.343", v_actor);
                _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.344", v_actor, v_inner__601561, v_source__598197, v_four__598443));
                object v_open__602672 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.345", v_actor);
                _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.346", v_actor, v_open__602672));
            }
            object v_out__604798 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.347", v_actor);
            object v_back__604931 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.348", v_actor);
            object v_out_id__605070 = R.cat(v_actor, Authored("m40.c1dep_actions.literal_0031"));
            object v_in_id__605177 = R.cat(v_actor, Authored("m40.c1dep_actions.literal_0032"));
            _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.349", v_out_id__605070, v_out__604798, v_actor));
            _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.350", v_in_id__605177, v_back__604931, v_actor, v_source__598197));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_actions.351", v_actor)))
        {
            object v_group__606593 = F_dv_get(F_campaign_profile(v_actor), "group");
            _ = R.push(v_actions__598145, F_c1_other_release_action(v_group__606593, v_actor, F_campaign_clock()));
            object v_pre__606644 = F_dv_get(F_c1dep_join_law(v_actor, v_group__606593), "premise");
            object v_id__606700 = R.cat(v_actor, Authored("m40.c1dep_actions.literal_0041"));
            _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.352", v_id__606700, v_pre__606644, v_actor, v_group__606593));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_actions.353", v_actor)))
        {
            _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.354", v_actor, v_source__598197));
            object v_own_pre__608157 = F_dv_get(F_c1dep_pick_contract_law(), "premise");
            _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.355", v_own_pre__608157));
            object v_pre__609692 = F_dv_get(F_c1dep_pick_law(), "premise");
            object v_pick_source__609736 = F_c1dep_source(Authored("m40.c1dep_actions.literal_0051"), Authored("m40.c1_memory_source.literal_0003"), Authored("m40.c1dep_actions.literal_0052"));
            object v_pick__609992 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.356", v_pre__609692, v_pick_source__609736);
            _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.357", v_pick__609992, v_pick_source__609736, v_four__598443));
            object v_receipt__611238 = F_dv_at_least(F_campaign_clock(), Authored("m40.c1dep_can_cross.literal_0010"));
            object v_call__611364 = F_c1dep_speech(v_actor, Authored("m40.c1dep_actions.literal_0059"), Authored("m40.c1_sound_agreement.literal"), v_receipt__611238, F_dv_true(), Authored("m40.c1dep_actions.literal_0060"), Authored("m40.c1dep_actions.literal_0061"), F_c1dep_source(Authored("m40.c1dep_actions.literal_0006"), Authored("m40.c1_memory_source.literal"), Authored("m40.c1dep_actions.literal_0062")));
            _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.358", v_call__611364));
            object v_self__612089 = F_dv_fact(F_c1dep_ref(Authored("m40.c1dep_can_cross.literal_0008")), true);
            object v_reply__612207 = F_c1dep_speech(v_actor, Authored("m40.c1dep_actions.literal_0064"), Authored("m40.c1_sound_agreement.literal"), v_self__612089, v_self__612089, Authored("m40.c1dep_actions.literal_0065"), Authored("m40.c1dep_actions.literal_0066"), F_c1dep_source(Authored("m40.c1dep_actions.literal_0006"), Authored("m40.c1_other_departure_source.literal_0004"), Authored("m40.c1dep_actions.literal_0007")));
            _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.359", v_reply__612207));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_actions.360", v_actor)))
        {
            object v_receipt__613010 = F_dv_at_least(F_campaign_clock(), Authored("m40.c1dep_can_cross.literal_0010"));
            _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.361", v_actor, v_receipt__613010));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_actions.362", v_actor)))
        {
            object v_ask__613983 = F_c1dep_speech(v_actor, Authored("m40.c1dep_actions.literal_0074"), Authored("m40.c1_edges.literal_0007"), F_dv_true(), F_dv_true(), Authored("m40.c1dep_actions.literal_0075"), Authored("m40.c1dep_actions.literal_0076"), F_c1dep_source(Authored("m40.c1dep_actions.literal_0006"), Authored("m40.c1_memory_source.literal_0003"), Authored("m40.c1dep_actions.literal_0077")));
            v_ask__613983 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.363", v_ask__613983);
            _ = R.push(v_actions__598145, v_ask__613983);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_actions.364", v_actor)))
        {
            object v_picked__615114 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.365", v_actor);
            _ = R.push(v_actions__598145, F_c1dep_speech(v_actor, Authored("m40.c1dep_actions.literal_0081"), Authored("m40.c1_sound_agreement.literal"), v_picked__615114, v_picked__615114, Authored("m40.c1dep_actions.literal_0082"), Authored("m40.c1dep_actions.literal_0083"), F_c1dep_source(Authored("m40.c1dep_actions.literal"), Authored("m40.c1_other_departure_source.literal_0020"), Authored("m40.c1dep_actions.literal_0084"))));
            object v_unpicked__615916 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.366", v_actor);
            _ = R.push(v_actions__598145, F_c1dep_speech(v_actor, Authored("m40.c1dep_actions.literal_0085"), Authored("m40.c1_sound_agreement.literal"), v_unpicked__615916, v_unpicked__615916, Authored("m40.c1dep_actions.literal_0086"), Authored("m40.c1dep_actions.literal_0087"), F_c1dep_source(Authored("m40.c1dep_actions.literal"), Authored("m40.c1_requested_replies.literal_0006"), Authored("m40.c1dep_actions.literal_0088"))));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_actions.367", v_actor)))
        {
            object v_picked__616897 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.368", v_actor);
            _ = R.push(v_actions__598145, F_c1dep_speech(v_actor, Authored("m40.c1dep_actions.literal_0089"), Authored("m40.c1_sound_agreement.literal"), v_picked__616897, v_picked__616897, Authored("m40.c1dep_actions.literal_0090"), Authored("m40.c1dep_actions.literal_0091"), F_c1dep_source(Authored("m40.c1dep_actions.literal"), Authored("m40.c1_requested_replies.literal_0004"), Authored("m40.c1dep_actions.literal_0092"))));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_actions.369", v_actor)))
        {
            object v_picked__617777 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.370", v_actor);
            _ = R.push(v_actions__598145, F_c1dep_speech(v_actor, Authored("m40.c1dep_actions.literal_0093"), Authored("m40.c1_sound_agreement.literal"), v_picked__617777, v_picked__617777, Authored("m40.c1dep_actions.literal_0094"), Authored("m40.c1dep_actions.literal_0095"), F_c1dep_source(Authored("m40.c1dep_actions.literal"), Authored("m40.c1_answer_trust.literal_0003"), Authored("m40.c1dep_actions.literal_0096"))));
            object v_unpicked__618569 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.371", v_actor);
            _ = R.push(v_actions__598145, F_c1dep_speech(v_actor, Authored("m40.c1dep_actions.literal_0097"), Authored("m40.c1_sound_agreement.literal"), v_unpicked__618569, v_unpicked__618569, Authored("m40.c1dep_actions.literal_0098"), Authored("m40.c1dep_actions.literal_0099"), F_c1dep_source(Authored("m40.c1dep_actions.literal"), Authored("m40.c1_public_goals.literal_0002"), Authored("m40.c1dep_actions.literal_0100"))));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_actions.372", v_actor)))
        {
            object v_manifestation__619550 = F_c1dep_manifest_law();
            _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.373", v_manifestation__619550, v_source__598197));
            object v_groups__620652 = AuthoredCopy("m40.c1_campaign_scene.groups");
            object v_g__620757 = R.number("0");
            while (R.Bool(R.lt(v_g__620757, R.count(v_groups__620652))))
            {
                object v_group__620817 = R.at(v_groups__620652, v_g__620757);
                object v_refs__620852 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.374", v_group__620817);
                object v_people__621578 = F_c1_other_people();
                object v_p__621622 = R.number("0");
                while (R.Bool(R.lt(v_p__621622, R.count(v_people__621578))))
                {
                    if (R.Bool(R.eq(F_dv_get(R.at(v_people__621578, v_p__621622), "group"), v_group__620817)))
                    {
                        _ = R.push(v_refs__620852, F_campaign_case_ref(v_group__620817, R.cat(Authored("m40.c1_other_exclusion_hypotheses.literal_0012"), F_dv_get(R.at(v_people__621578, v_p__621622), "id"))));
                    }
                    v_p__621622 = R.add(v_p__621622, R.number("1"));
                }
                _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.375", v_group__620817, v_actor, v_refs__620852));
                _ = R.push(v_actions__598145, F_c1_other_release_action(v_group__620817, v_actor, F_campaign_clock()));
                v_g__620757 = R.add(v_g__620757, R.number("1"));
            }
            object v_solved_refs__622764 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.376");
            _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.377", v_actor, v_solved_refs__622764, v_source__598197));
            _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.378", v_actor, v_source__598197));
            _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.379", v_actor, v_source__598197));
            object v_after_answer__624870 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.380", v_actor);
            _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.381", v_after_answer__624870, v_source__598197));
            object v_pre__625440 = F_dv_get(F_c1dep_outer_law(), "premise");
            object v_lifted__625485 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.382", v_pre__625440, v_source__598197);
            _ = R.push(v_actions__598145, DvValueTemplate.Instantiate("template.inline.c1dep_actions.383", v_lifted__625485, v_source__598197, v_four__598443));
        }
        object v_hand_ref__627077 = F_dv_ref(Authored("m40.c1_other_exclusion_hypotheses.literal_0023"), "state", Authored("m40.c1dep_actions.literal_0122"));
        object v_owner_ref__627271 = F_dv_ref(Authored("m40.c1_other_exclusion_hypotheses.literal_0023"), "state", Authored("m40.c1dep_actions.literal_0123"));
        object v_hand_source__627466 = F_campaign_source(Authored("m40.c1_inspect_E10.literal_0017"), R.number("0"), Authored("m40.c1dep_actions.literal_0124"));
        if (R.Bool(F_dv_has(v_four__598443, v_actor)))
        {
            object v_present__627684 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.384", v_actor);
            object v_inspect_hand__628168 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.385", v_actor, v_present__627684, v_hand_ref__627077, v_hand_source__627466);
            object v_local_contact__628506 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.386", v_actor);
            object v_no_hand__628790 = F_dv_fact(F_dv_ref(Authored("m40.c1_other_exclusion_hypotheses.literal_0023"), "state", Authored("m40.c1_other_exclusion_hypotheses.literal_0024")), false);
            object v_failure__629019 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.387", v_actor, v_no_hand__628790, v_hand_source__627466);
            v_inspect_hand__628168 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.388", v_inspect_hand__628168, v_local_contact__628506, v_failure__629019);
            _ = R.push(v_actions__598145, v_inspect_hand__628168);
            object v_seen_print__629780 = F_dv_fact(F_campaign_evidence_ref(Authored("m40.c1_materials.literal_0011")), true);
            object v_ask__629913 = F_c1dep_speech(v_actor, R.cat(v_actor, Authored("m40.c1dep_actions.literal_0131")), Authored("m40.c1_sound_agreement.literal"), v_seen_print__629780, F_dv_true(), Authored("m40.c1dep_actions.literal_0132"), Authored("m40.c1dep_actions.literal_0133"), v_hand_source__627466);
            v_ask__629913 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.389", v_ask__629913, v_owner_ref__627271);
            _ = R.push(v_actions__598145, v_ask__629913);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_actions.390", v_actor)))
        {
            object v_claim__630592 = F_dv_fact(v_owner_ref__627271, Authored("m40.c1_observe_as.literal_0005"));
            object v_reply__630703 = F_c1dep_speech(v_actor, Authored("m40.c1dep_actions.literal_0135"), Authored("m40.c1_sound_agreement.literal"), v_claim__630592, v_claim__630592, Authored("m40.c1dep_actions.literal_0136"), Authored("m40.c1dep_actions.literal_0137"), v_hand_source__627466);
            v_reply__630703 = DvValueTemplate.Instantiate("template.inline.c1dep_actions.391", v_reply__630703, v_actor, v_claim__630592);
            _ = R.push(v_actions__598145, F_campaign_authorized_speech(v_reply__630703));
        }
        return F_c1_public_methods(v_actions__598145);
    }
    public static object F_c1dep_laws(object v_world,object v_actor) {
        object v_mode__631767 = "enforce";
        if (R.Bool(R.eq(F_dv_get(v_world, "domain"), "mind")))
        {
            v_mode__631767 = "predict";
        }
        object v_result__631851 = v_world;
        object v_source__631875 = F_c1dep_source(Authored("m40.c1dep_actions.literal"), Authored("m40.c1_raw_person_policy.literal_0004"), Authored("m40.c1dep_actions.literal_0002"));
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_laws.392", v_actor)))
        {
            v_result__631851 = F_dv_adopt(v_result__631851, F_campaign_method(R.cat(v_actor, Authored("m40.c1dep_actions.literal_0022")), F_c1dep_inner_law(v_actor)), v_mode__631767, R.number("1"), new List<object> { v_source__631875 });
            v_result__631851 = F_dv_adopt(v_result__631851, F_campaign_method(R.cat(v_actor, Authored("m40.c1dep_actions.literal_0025")), F_c1dep_open_law(v_actor)), v_mode__631767, R.number("1"), new List<object> { v_source__631875 });
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_laws.393", v_actor)))
        {
            v_result__631851 = F_dv_adopt(v_result__631851, F_campaign_method(R.cat(v_actor, Authored("m40.c1dep_actions.literal_0031")), F_c1dep_cross_law(v_actor, true)), v_mode__631767, R.number("1"), new List<object> { v_source__631875 });
            v_result__631851 = F_dv_adopt(v_result__631851, F_campaign_method(R.cat(v_actor, Authored("m40.c1dep_actions.literal_0032")), F_c1dep_cross_law(v_actor, false)), v_mode__631767, R.number("1"), new List<object> { v_source__631875 });
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_laws.394", v_actor)))
        {
            v_result__631851 = F_dv_adopt(v_result__631851, F_campaign_method(R.cat(v_actor, Authored("m40.c1dep_actions.literal_0041")), F_c1dep_join_law(v_actor, F_dv_get(F_campaign_profile(v_actor), "group"))), v_mode__631767, R.number("1"), new List<object> { F_dv_get(F_c1_other_departure_source(v_actor), "source") });
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_laws.395", v_actor)))
        {
            v_result__631851 = F_dv_adopt(v_result__631851, F_campaign_method(Authored("m40.c1dep_actions.literal_0047"), F_c1dep_pick_contract_law()), v_mode__631767, R.number("1"), DvValueTemplate.Instantiate("template.inline.c1dep_laws.396"));
            v_result__631851 = F_dv_adopt(v_result__631851, F_campaign_method(Authored("m40.c1dep_actions.literal_0053"), F_c1dep_pick_law()), v_mode__631767, R.number("1"), DvValueTemplate.Instantiate("template.inline.c1dep_laws.397"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_laws.398", v_actor)))
        {
            v_result__631851 = F_dv_adopt(v_result__631851, F_campaign_method(Authored("m40.c1_other_place_detective.literal_0009"), F_c1dep_manifest_law()), v_mode__631767, R.number("1"), new List<object> { v_source__631875 });
            if (R.Bool(R.eq(v_mode__631767, "predict")))
            {
                object v_groups__634606 = AuthoredCopy("m40.c1_campaign_scene.groups");
                object v_g__634715 = R.number("0");
                while (R.Bool(R.lt(v_g__634715, R.count(v_groups__634606))))
                {
                    v_result__631851 = F_dv_adopt(v_result__631851, F_campaign_method(R.cat(Authored("m40.c1_other_release_action.literal_0002"), R.at(v_groups__634606, v_g__634715)), F_c1_other_release(R.at(v_groups__634606, v_g__634715), F_campaign_clock())), v_mode__631767, R.number("1"), new List<object> { F_dv_get(F_c1_other_case(R.at(v_groups__634606, v_g__634715)), "source") });
                    v_g__634715 = R.add(v_g__634715, R.number("1"));
                }
            }
            v_result__631851 = F_dv_adopt(v_result__631851, F_campaign_method(Authored("m40.c1dep_actions.literal_0118"), F_c1dep_outer_law()), v_mode__631767, R.number("1"), new List<object> { v_source__631875 });
        }
        if (R.Bool(R.eq(v_mode__631767, "predict")) && R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_actions.351", v_actor)))
        {
            object group = F_dv_get(F_campaign_profile(v_actor), "group");
            object law = F_c1_other_release(group, F_campaign_clock());
            object action = F_c1_other_release_action(group, v_actor, F_campaign_clock());
            v_result__631851 = F_dv_adopt(v_result__631851, F_campaign_method(F_dv_get(action, "id"), law), v_mode__631767, R.number("1"), new List<object> { v_source__631875 });
        }
        return v_result__631851;
    }
    public static object F_c1dep_goals(object v_actor) {
        object v_goals__635255 = DvValueTemplate.Instantiate("template.c1_self_exit_goals", v_actor);
        object v_source__635275 = DvValueTemplate.Instantiate("template.inline.c1dep_goals.399");
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_goals.400", v_actor)))
        {
            object v_after_trust__635625 = F_dv_fact(F_dv_ref(Authored("post.c1dep_goals.0001"), "state", Authored("m40.c1_question_object.literal_0012")), true);
            object v_released__635771 = F_dv_fact(F_c1dep_ref(Authored("m40.c1dep_can_cross.literal")), true);
            object v_at_door__635888 = F_campaign_at(F_dv_get(v_actor, "id"), Authored("m40.c1_edges.literal_0007"));
            object v_sound__635999 = F_dv_fact(F_c1dep_ref(Authored("m40.c1dep_can_cross.literal_0003")), true);
            object v_recheck__636118 = F_campaign_any(v_sound__635999, F_campaign_any(v_after_trust__635625, F_campaign_any(v_released__635771, v_at_door__635888)));
            _ = R.push(v_goals__635255, F_dv_with_many(F_campaign_goal(F_campaign_at(F_dv_get(v_actor, "id"), Authored("m40.c1_edges.literal_0007")), DvValueTemplate.Instantiate("template.inline.c1dep_goals.401", v_sound__635999), v_source__635275), DvValueTemplate.Instantiate("template.inline.c1dep_goals.402")));
            object v_door_source__636625 = DvValueTemplate.Instantiate("template.inline.c1dep_goals.403");
            _ = R.push(v_goals__635255, F_dv_with_many(F_campaign_observation_goal(F_c1dep_ref(Authored("m40.c1dep_can_cross.literal")), v_recheck__636118, v_door_source__636625, R.number("0")), DvValueTemplate.Instantiate("template.inline.c1dep_goals.404", v_released__635771)));
            _ = R.push(v_goals__635255, F_dv_with_many(F_campaign_observation_goal(F_c1dep_ref(Authored("m40.c1dep_can_cross.literal_0002")), v_recheck__636118, v_door_source__636625, R.number("0")), DvValueTemplate.Instantiate("template.inline.c1dep_goals.405")));
            _ = R.push(v_goals__635255, F_dv_with_many(F_campaign_observation_goal(F_c1dep_ref(Authored("m40.c1_other_exclusion_hypotheses.literal_0002")), v_recheck__636118, v_door_source__636625, R.number("0")), DvValueTemplate.Instantiate("template.inline.c1dep_goals.406")));
            _ = R.push(v_goals__635255, F_dv_with(F_campaign_goal(F_dv_fact(F_c1dep_ref(Authored("m40.c1dep_can_cross.literal_0002")), false), F_dv_fact(F_c1dep_ref(Authored("m40.c1dep_can_cross.literal")), true), v_source__635275), Authored("m40.c1dep_goals.literal_0003"), true));
            _ = R.push(v_goals__635255, F_dv_with(F_campaign_goal(F_dv_fact(F_c1dep_ref(Authored("m40.c1_other_exclusion_hypotheses.literal_0002")), true), F_dv_fact(F_c1dep_ref(Authored("m40.c1dep_can_cross.literal")), true), v_source__635275), Authored("m40.c1dep_goals.literal_0003"), true));
            _ = R.push(v_goals__635255, F_dv_with(F_campaign_goal(F_campaign_at(F_dv_get(v_actor, "id"), Authored("m40.c1_sound_agreement.literal")), F_dv_fact(F_c1dep_ref(Authored("m40.c1dep_can_cross.literal")), true), v_source__635275), Authored("m40.c1dep_goals.literal_0003"), true));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_goals.407", v_actor)))
        {
            object v_leave__639110 = F_c1_other_depart(F_dv_get(v_actor, "id"));
            _ = R.push(v_goals__635255, F_campaign_goal(F_campaign_at(F_dv_get(v_actor, "id"), R.cat(Authored("m40.c1_other_threshold.literal"), F_dv_get(F_campaign_profile(F_dv_get(v_actor, "id")), "group"))), F_dv_get(v_leave__639110, "premise"), new List<object> { F_dv_get(F_c1_other_departure_source(F_dv_get(v_actor, "id")), "source") }));
            object v_join_available__639393 = F_campaign_at(F_dv_get(v_actor, "id"), R.cat(Authored("m40.c1_other_threshold.literal"), F_dv_get(F_campaign_profile(F_dv_get(v_actor, "id")), "group")));
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_goals.408", v_actor)))
            {
                v_join_available__639393 = F_dv_get(F_c1dep_join_law(F_dv_get(v_actor, "id"), F_dv_get(F_campaign_profile(F_dv_get(v_actor, "id")), "group")), "premise");
            }
            _ = R.push(v_goals__635255, F_campaign_goal(F_campaign_at(F_dv_get(v_actor, "id"), Authored("m40.c1_sound_agreement.literal")), v_join_available__639393, new List<object> { F_dv_get(F_c1_other_departure_source(F_dv_get(v_actor, "id")), "source") }));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_goals.409", v_actor)))
        {
            _ = R.push(v_goals__635255, F_campaign_goal(F_campaign_case_claim(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_exclusion_hypotheses.literal_0026")), F_dv_get(F_c1dep_pick_contract_law(), "premise"), v_source__635275));
            _ = R.push(v_goals__635255, F_dv_with(F_campaign_observation_goal(F_c1dep_ref(Authored("m40.c1dep_can_cross.literal")), F_campaign_at(F_dv_get(v_actor, "id"), Authored("m40.c1_sound_agreement.literal")), v_source__635275, R.number("0")), Authored("m40.c1dep_goals.literal_0004"), F_dv_fact(F_c1dep_ref(Authored("m40.c1dep_can_cross.literal")), true)));
            _ = R.push(v_goals__635255, F_campaign_goal(F_dv_fact(F_c1dep_ref(Authored("m40.c1dep_can_cross.literal")), true), F_campaign_at(F_dv_get(v_actor, "id"), Authored("m40.c1_sound_agreement.literal")), DvValueTemplate.Instantiate("template.inline.c1dep_goals.410")));
        }
        object v_actions__641455 = F_c1dep_actions(F_dv_get(v_actor, "id"));
        object v_i__641498 = R.number("0");
        while (R.Bool(R.lt(v_i__641498, R.count(v_actions__641455))))
        {
            if (R.Bool(F_dv_has_key(R.at(v_actions__641455, v_i__641498), "speech")))
            {
                _ = R.push(v_goals__635255, DvValueTemplate.Instantiate("template.inline.c1dep_goals.411", v_actions__641455, v_i__641498));
            }
            v_i__641498 = R.add(v_i__641498, R.number("1"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_goals.412", v_actor)))
        {
            _ = R.push(v_goals__635255, F_campaign_observation_goal(F_campaign_clock(), DvValueTemplate.Instantiate("template.inline.c1dep_goals.413", v_actor), v_source__635275, R.number("0")));
            object v_people__642248 = F_c1_other_people();
            v_i__641498 = R.number("0");
            while (R.Bool(R.lt(v_i__641498, R.count(v_people__642248))))
            {
                object v_ref__642344 = F_campaign_case_ref(F_dv_get(R.at(v_people__642248, v_i__641498), "group"), R.cat(Authored("m40.c1_other_exclusion_hypotheses.literal_0012"), F_dv_get(R.at(v_people__642248, v_i__641498), "id")));
                _ = R.push(v_goals__635255, F_campaign_observation_goal(v_ref__642344, F_campaign_at(F_dv_get(v_actor, "id"), Authored("m40.c1_sound_agreement.literal")), v_source__635275, R.number("0")));
                v_i__641498 = R.add(v_i__641498, R.number("1"));
            }
            object v_groups__642695 = AuthoredCopy("m40.c1_campaign_scene.groups");
            v_i__641498 = R.number("0");
            while (R.Bool(R.lt(v_i__641498, R.count(v_groups__642695))))
            {
                object v_ref__642856 = F_campaign_case_ref(R.at(v_groups__642695, v_i__641498), Authored("m40.c1_other_exclusion_hypotheses.literal_0025"));
                object v_receipt_refs__642995 = new List<object> {  };
                object v_receipt_index__643030 = R.number("0");
                while (R.Bool(R.lt(v_receipt_index__643030, R.count(v_people__642248))))
                {
                    if (R.Bool(R.eq(F_dv_get(R.at(v_people__642248, v_receipt_index__643030), "group"), R.at(v_groups__642695, v_i__641498))))
                    {
                        _ = R.push(v_receipt_refs__642995, F_campaign_case_ref(R.at(v_groups__642695, v_i__641498), R.cat(Authored("m40.c1_other_exclusion_hypotheses.literal_0012"), F_dv_get(R.at(v_people__642248, v_receipt_index__643030), "id"))));
                    }
                    v_receipt_index__643030 = R.add(v_receipt_index__643030, R.number("1"));
                }
                object v_evidence_ref__643425 = F_campaign_case_ref(R.at(v_groups__642695, v_i__641498), Authored("m40.c1_other_exclusion_hypotheses.literal_0015"));
                _ = R.push(v_goals__635255, F_dv_with_many(F_campaign_observation_goal(v_evidence_ref__643425, F_campaign_at(F_dv_get(v_actor, "id"), Authored("m40.c1_sound_agreement.literal")), v_source__635275, R.number("0")), new List<object> { F_dv_pair("refresh_until", F_dv_fact(v_evidence_ref__643425, true)), F_dv_pair("refresh_refs", v_receipt_refs__642995) }));
                object v_reported__643839 = F_dv_not(F_dv_true());
                object v_safe_time__643885 = Authored("m40.c1_physical_world.literal_0002");
                object v_safety__643988 = F_campaign_case_claim(R.at(v_groups__642695, v_i__641498), Authored("m40.c1_other_exclusion_hypotheses.literal_0015"));
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_goals.414", v_groups__642695, v_i__641498)))
                {
                    v_safe_time__643885 = Authored("m40.c1_other_environment_install_scene.literal_0007");
                    v_safety__643988 = DvValueTemplate.Instantiate("template.inline.c1dep_goals.415", v_groups__642695, v_i__641498);
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_goals.416", v_groups__642695, v_i__641498)))
                {
                    v_safe_time__643885 = Authored("m40.c1_other_exclusion_hypotheses.literal_0018");
                    v_safety__643988 = DvValueTemplate.Instantiate("template.inline.c1dep_goals.417", v_groups__642695, v_i__641498);
                }
                object v_person_index__645165 = R.number("0");
                while (R.Bool(R.lt(v_person_index__645165, R.count(v_people__642248))))
                {
                    if (R.Bool(R.eq(F_dv_get(R.at(v_people__642248, v_person_index__645165), "group"), R.at(v_groups__642695, v_i__641498))))
                    {
                        v_reported__643839 = F_campaign_any(v_reported__643839, F_campaign_case_claim(R.at(v_groups__642695, v_i__641498), R.cat(Authored("m40.c1_other_exclusion_hypotheses.literal_0012"), F_dv_get(R.at(v_people__642248, v_person_index__645165), "id"))));
                    }
                    v_person_index__645165 = R.add(v_person_index__645165, R.number("1"));
                }
                object v_manifest_ready__645568 = DvValueTemplate.Instantiate("template.inline.c1dep_goals.418", v_reported__643839, v_safety__643988, v_safe_time__643885, v_actor);
                _ = R.push(v_goals__635255, F_campaign_goal(F_dv_fact(F_dv_ref(Authored("m40.c1_other_exclusion_hypotheses.literal_0023"), "state", Authored("m40.c1_other_exclusion_hypotheses.literal_0024")), true), v_manifest_ready__645568, v_source__635275));
                _ = R.push(v_goals__635255, F_campaign_observation_goal(v_ref__642856, F_campaign_at(F_dv_get(v_actor, "id"), Authored("m40.c1_sound_agreement.literal")), v_source__635275, R.number("0")));
                _ = R.push(v_goals__635255, F_campaign_goal(F_dv_fact(v_ref__642856, true), F_dv_get(F_c1_other_release(R.at(v_groups__642695, v_i__641498), F_campaign_clock()), "premise"), v_source__635275));
                v_i__641498 = R.add(v_i__641498, R.number("1"));
            }
            _ = R.push(v_goals__635255, F_campaign_goal(F_dv_fact(F_dv_ref(Authored("m40.c1_other_exclusion_hypotheses.literal_0023"), "state", Authored("m40.c1_other_exclusion_hypotheses.literal_0024")), true), F_dv_get(F_c1dep_manifest_law(), "premise"), v_source__635275));
            _ = R.push(v_goals__635255, F_campaign_observation_goal(F_campaign_position_ref(F_dv_get(v_actor, "id")), F_dv_true(), v_source__635275, R.number("0")));
            _ = R.push(v_goals__635255, F_campaign_observation_goal(F_c1dep_ref(Authored("m40.c1dep_can_cross.literal")), F_campaign_at(F_dv_get(v_actor, "id"), Authored("m40.c1_sound_agreement.literal")), v_source__635275, R.number("0")));
            _ = R.push(v_goals__635255, F_campaign_goal(F_dv_fact(F_c1dep_ref(Authored("m40.c1dep_can_cross.literal")), true), F_c1dep_complete(), v_source__635275));
        }
        object v_refreshed__647334 = new List<object> {  };
        object v_g__647358 = R.number("0");
        while (R.Bool(R.lt(v_g__647358, R.count(v_goals__635255))))
        {
            object v_goal__647409 = R.at(v_goals__635255, v_g__647358);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_goals.419", v_goal__647409)))
            {
                object v_triggers__647582 = new List<object> {  };
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_goals.420", v_actor)))
                {
                    _ = R.push(v_triggers__647582, F_dv_ref(Authored("post.c1dep_goals.0001"), "state", Authored("m40.c1_question_object.literal_0012")));
                }
                v_goal__647409 = F_dv_with(v_goal__647409, "refresh_refs", v_triggers__647582);
            }
            _ = R.push(v_refreshed__647334, v_goal__647409);
            v_g__647358 = R.add(v_g__647358, R.number("1"));
        }
        v_goals__635255 = v_refreshed__647334;
        return v_goals__635255;
    }
    public static object F_c1dep_install_scene(object v_scene) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.421", v_scene)))
        {
            return v_scene;
        }
        object v_physical__648185 = F_dv_store(F_dv_get(v_scene, "physical"), F_dv_ref(Authored("m40.c1_observe_as.literal_0005"), "state", "location"), F_dv_field(Authored("m40.c1_sound_agreement.literal")));
        v_physical__648185 = F_campaign_add_new(v_physical__648185, DvValueTemplate.Instantiate("template.s04.geb_object"));
        v_physical__648185 = F_campaign_add_new(v_physical__648185, F_campaign_state_object(Authored("m40.c1_other_exclusion_hypotheses.literal_0023"), new List<object> { F_dv_pair("manifested", F_dv_field(false)), F_dv_pair("latch_hand_trace", F_dv_field("")) }));
        v_physical__648185 = F_dv_store(v_physical__648185, F_dv_ref(Authored("m40.c1_other_exclusion_hypotheses.literal_0023"), "state", Authored("m40.c1dep_actions.literal_0122")), F_dv_field(Authored("m40.c1_other_exclusion_hypotheses.literal_0028")));
        v_physical__648185 = F_dv_store(v_physical__648185, F_dv_ref(Authored("m40.c1_other_exclusion_hypotheses.literal_0023"), "state", Authored("m40.c1dep_actions.literal_0123")), F_dv_field(Authored("m40.c1_observe_as.literal_0005")));
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.422", v_physical__648185)))
        {
            v_physical__648185 = F_dv_store(v_physical__648185, F_campaign_access(Authored("m40.c1dep_can_cross.literal_0004")), F_dv_field(false));
        }
        object v_objects__649866 = AuthoredCopy("m40.c1dep_install_scene.objects");
        object v_four__649971 = AuthoredCopy("m40.c1_sound_agreement.literal_0006");
        object v_i__650077 = R.number("0");
        while (R.Bool(R.lt(v_i__650077, R.count(v_four__649971))))
        {
            object v_id__650127 = R.cat(Authored("m40.c1dep_cross_ref.literal"), R.at(v_four__649971, v_i__650077));
            v_physical__648185 = F_campaign_add_new(v_physical__648185, F_campaign_state_object(v_id__650127, new List<object> { F_dv_pair("crossed", F_dv_field(false)) }));
            _ = R.push(v_objects__649866, v_id__650127);
            v_i__650077 = R.add(v_i__650077, R.number("1"));
        }
        object v_actors__650378 = new List<object> {  };
        object v_bindings__650399 = F_dv_clone(F_dv_get(v_scene, "bindings"));
        v_i__650077 = R.number("0");
        while (R.Bool(R.lt(v_i__650077, R.count(F_dv_get(v_scene, "actors")))))
        {
            object v_actor__650498 = R.at(F_dv_get(v_scene, "actors"), v_i__650077);
            v_physical__648185 = F_c1dep_laws(v_physical__648185, F_dv_get(v_actor__650498, "id"));
            object v_mind__650586 = F_dv_get(v_actor__650498, "mind");
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.423", v_actor__650498)))
            {
                v_mind__650586 = F_dv_store(v_mind__650586, F_dv_ref(Authored("m40.c1_observe_as.literal_0005"), "state", "location"), DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.424"));
            }
            object v_j__651109 = R.number("0");
            while (R.Bool(R.lt(v_j__651109, R.count(v_objects__649866))))
            {
                v_mind__650586 = F_campaign_add_new(v_mind__650586, F_campaign_local_object(R.at(v_objects__649866, v_j__651109)));
                if (R.Bool(R.eq(F_dv_local(v_bindings__650399, R.at(v_objects__649866, v_j__651109), F_dv_get(v_actor__650498, "id")), "")))
                {
                    _ = R.push(v_bindings__650399, F_dv_binding(R.at(v_objects__649866, v_j__651109), R.at(v_objects__649866, v_j__651109), F_dv_get(v_actor__650498, "id")));
                }
                v_j__651109 = R.add(v_j__651109, R.number("1"));
            }
            v_mind__650586 = F_c1dep_laws(v_mind__650586, F_dv_get(v_actor__650498, "id"));
            if (R.Bool(F_dv_has(v_four__649971, F_dv_get(v_actor__650498, "id"))))
            {
                v_mind__650586 = F_dv_store(v_mind__650586, F_c1dep_ref(Authored("m40.c1dep_can_cross.literal")), DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.425"));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.426", v_actor__650498)))
            {
                v_mind__650586 = F_dv_store(v_mind__650586, F_dv_ref(Authored("m40.c1_other_exclusion_hypotheses.literal_0023"), "state", Authored("m40.c1_other_exclusion_hypotheses.literal_0024")), DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.427"));
                v_mind__650586 = F_dv_store(v_mind__650586, F_dv_ref(Authored("m40.c1_other_exclusion_hypotheses.literal_0023"), "state", Authored("m40.c1dep_actions.literal_0123")), DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.428"));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.429", v_actor__650498, v_mind__650586)))
            {
                v_mind__650586 = F_dv_store(v_mind__650586, F_c1dep_ref(Authored("m40.c1dep_can_cross.literal_0006")), DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.430"));
            }
            object v_actions__653542 = F_c1dep_unify_demon_position(F_dv_get(v_actor__650498, "actions"));
            object v_hearing__653607 = F_c1dep_unify_demon_position(F_dv_find(v_actor__650498, Authored("m40.c1_sound_install_scene.literal_0009"), new List<object> {  }));
            object v_transport_index__653757 = R.number("0");
            while (R.Bool(R.lt(v_transport_index__653757, R.count(v_hearing__653607))))
            {
                object v_grant__653846 = R.at(v_hearing__653607, v_transport_index__653757);
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.431", v_grant__653846)))
                {
                    v_grant__653846 = DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.432", v_grant__653846);
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.433", v_actor__650498)))
                {
                    v_grant__653846 = DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.434", v_grant__653846);
                }
                v_hearing__653607 = F_dvs_replace(v_hearing__653607, v_transport_index__653757, v_grant__653846);
                v_transport_index__653757 = R.add(v_transport_index__653757, R.number("1"));
            }
            object v_channels__654638 = F_dv_clone(F_dv_find(v_actor__650498, Authored("m40.c1_sound_install_scene.literal_0004"), new List<object> {  }));
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.435", v_actor__650498)))
            {
                v_j__651109 = R.number("0");
                while (R.Bool(R.lt(v_j__651109, R.count(v_actions__653542))))
                {
                    if (R.Bool(F_dv_has_key(R.at(v_actions__653542, v_j__651109), "speech")))
                    {
                        object v_action__654993 = DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.436", v_actions__653542, v_j__651109);
                        v_action__654993 = F_dv_without(v_action__654993, Authored("m40.c1_other_place_detective.literal_0016"));
                        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.437", v_action__654993)))
                        {
                            v_action__654993 = F_dv_without(v_action__654993, Authored("m40.c1dep_actions.literal_0072"));
                        }
                        if (R.Bool(F_dv_has_key(v_action__654993, "response_topic")))
                        {
                            v_action__654993 = DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.438", v_action__654993);
                        }
                        v_actions__653542 = F_dvs_replace(v_actions__653542, v_j__651109, v_action__654993);
                    }
                    v_j__651109 = R.add(v_j__651109, R.number("1"));
                }
                v_j__651109 = R.number("0");
                while (R.Bool(R.lt(v_j__651109, R.count(v_hearing__653607))))
                {
                    v_hearing__653607 = F_dvs_replace(v_hearing__653607, v_j__651109, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.439", v_hearing__653607, v_j__651109));
                    v_j__651109 = R.add(v_j__651109, R.number("1"));
                }
                v_j__651109 = R.number("0");
                while (R.Bool(R.lt(v_j__651109, R.count(v_four__649971))))
                {
                    _ = R.push(v_hearing__653607, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.440", v_four__649971, v_j__651109));
                    _ = R.push(v_hearing__653607, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.441", v_four__649971, v_j__651109));
                    _ = R.push(v_hearing__653607, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.442", v_four__649971, v_j__651109));
                    _ = R.push(v_channels__654638, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.443", v_four__649971, v_j__651109));
                    v_j__651109 = R.add(v_j__651109, R.number("1"));
                }
                v_actor__650498 = DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.444", v_actor__650498);
            }
            if (R.Bool(F_dv_has(v_four__649971, F_dv_get(v_actor__650498, "id"))))
            {
                object v_opener_index__659805 = R.number("0");
                object v_openers__659839 = AuthoredCopy("m40.c1dep_install_scene.openers");
                while (R.Bool(R.lt(v_opener_index__659805, R.count(v_openers__659839))))
                {
                    _ = R.push(v_hearing__653607, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.445", v_openers__659839, v_opener_index__659805, v_actor__650498));
                    v_opener_index__659805 = R.add(v_opener_index__659805, R.number("1"));
                }
                _ = R.push(v_hearing__653607, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.446"));
                v_j__651109 = R.number("0");
                while (R.Bool(R.lt(v_j__651109, R.count(v_four__649971))))
                {
                    if (R.Bool(R.not(R.eq(R.at(v_four__649971, v_j__651109), F_dv_get(v_actor__650498, "id")))))
                    {
                        _ = R.push(v_hearing__653607, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.447", v_four__649971, v_j__651109));
                    }
                    v_j__651109 = R.add(v_j__651109, R.number("1"));
                }
                _ = R.push(v_hearing__653607, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.448", v_actor__650498));
                _ = R.push(v_hearing__653607, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.449", v_actor__650498));
                _ = R.push(v_hearing__653607, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.450", v_actor__650498));
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.451", v_actor__650498)))
                {
                    _ = R.push(v_hearing__653607, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.452", v_actor__650498));
                }
                _ = R.push(v_channels__654638, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.453", v_actor__650498));
                v_j__651109 = R.number("0");
                while (R.Bool(R.lt(v_j__651109, R.count(v_actions__653542))))
                {
                    if (R.Bool(F_dv_has_key(R.at(v_actions__653542, v_j__651109), "speech")))
                    {
                        object v_listeners__666267 = F_dv_find(R.at(v_actions__653542, v_j__651109), "listeners", new List<object> {  });
                        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.454", v_listeners__666267)))
                        {
                            v_actions__653542 = F_dvs_replace(v_actions__653542, v_j__651109, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.455", v_actions__653542, v_j__651109, v_listeners__666267));
                        }
                    }
                    object v_request__666641 = F_dv_find(F_dv_find(R.at(v_actions__653542, v_j__651109), "speech", new List<object> {  }), Authored("m40.c1dep_actions.literal_0078"), false);
                    if (R.Bool(R.not(R.eq(v_request__666641, false))))
                    {
                        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.456", v_request__666641)))
                        {
                            v_actions__653542 = F_dvs_replace(v_actions__653542, v_j__651109, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.457", v_actions__653542, v_j__651109, v_actor__650498));
                        }
                    }
                    v_j__651109 = R.add(v_j__651109, R.number("1"));
                }
                object v_outsiders__667699 = AuthoredCopy("m40.c1dep_install_scene.outsiders");
                v_j__651109 = R.number("0");
                while (R.Bool(R.lt(v_j__651109, R.count(v_outsiders__667699))))
                {
                    _ = R.push(v_hearing__653607, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.458", v_outsiders__667699, v_j__651109, v_actor__650498));
                    v_j__651109 = R.add(v_j__651109, R.number("1"));
                }
                _ = R.push(v_channels__654638, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.459", v_actor__650498));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.460", v_actor__650498)))
            {
                v_j__651109 = R.number("0");
                while (R.Bool(R.lt(v_j__651109, R.count(v_four__649971))))
                {
                    _ = R.push(v_hearing__653607, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.461", v_four__649971, v_j__651109, v_actor__650498));
                    _ = R.push(v_channels__654638, DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.462", v_four__649971, v_j__651109, v_actor__650498));
                    v_j__651109 = R.add(v_j__651109, R.number("1"));
                }
            }
            object v_extra__670680 = F_c1dep_actions(F_dv_get(v_actor__650498, "id"));
            v_j__651109 = R.number("0");
            while (R.Bool(R.lt(v_j__651109, R.count(v_extra__670680))))
            {
                _ = R.push(v_actions__653542, R.at(v_extra__670680, v_j__651109));
                v_j__651109 = R.add(v_j__651109, R.number("1"));
            }
            v_actor__650498 = F_dv_with_many(v_actor__650498, new List<object> { F_dv_pair("mind", F_c1dep_unify_demon_position(v_mind__650586)), F_dv_pair("policy", F_c1dep_unify_demon_position(F_dv_get(v_actor__650498, "policy"))), F_dv_pair("actions", F_c1_public_methods(v_actions__653542)), F_dv_pair("hearing_grants", v_hearing__653607), F_dv_pair("request_channels", v_channels__654638) });
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.463", v_actor__650498)))
            {
                v_actor__650498 = DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.464", v_actor__650498);
            }
            object v_goals__671329 = F_c1dep_goals(v_actor__650498);
            object v_combined__671369 = F_dv_clone(F_dv_find(F_dv_get(v_actor__650498, "policy"), "goals", new List<object> {  }));
            v_j__651109 = R.number("0");
            while (R.Bool(R.lt(v_j__651109, R.count(v_goals__671329))))
            {
                _ = R.push(v_combined__671369, R.at(v_goals__671329, v_j__651109));
                v_j__651109 = R.add(v_j__651109, R.number("1"));
            }
            v_actor__650498 = F_dv_with(v_actor__650498, "policy", F_dv_with_many(F_dv_get(v_actor__650498, "policy"), new List<object> { F_dv_pair("goals", v_combined__671369), F_dv_pair("departure_goals", v_goals__671329) }));
            _ = R.push(v_actors__650378, v_actor__650498);
            v_i__650077 = R.add(v_i__650077, R.number("1"));
        }
        return F_dv_with_many(v_scene, new List<object> { F_dv_pair("physical", F_c1dep_unify_demon_position(v_physical__648185)), F_dv_pair("actors", v_actors__650378), F_dv_pair("bindings", v_bindings__650399), F_dv_pair("departure_installed", true), DvValueTemplate.Instantiate("template.inline.c1dep_install_scene.465") });
    }
    public static object F_c1dep_manifest_law() {
        return DvValueTemplate.Instantiate("template.c1dep_manifest_law");
    }
    public static object F_c1dep_unify_demon_position(object v_value) {
        if (R.Bool(R.not(R.is_list(v_value))))
        {
            return v_value;
        }
        if (R.Bool(F_dv_record_like(v_value)))
        {
            if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_find(v_value, "op", ""), "equals")) ? (object)(F_dv_has_key(v_value, "ref")) : (object)(false))) ? (object)(F_dv_has_key(v_value, "value")) : (object)(false))))
            {
                object v_ref__676498 = F_dv_get(v_value, "ref");
                if (R.Bool(F_dv_record_like(v_ref__676498)))
                {
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1dep_unify_demon_position.466", v_ref__676498, v_value)))
                    {
                        return DvValueTemplate.Instantiate("template.inline.c1dep_unify_demon_position.467", v_value);
                    }
                }
            }
            object v_fields__677065 = new List<object> {  };
            object v_field_index__677090 = R.number("0");
            while (R.Bool(R.lt(v_field_index__677090, R.count(v_value))))
            {
                _ = R.push(v_fields__677065, new List<object> { R.at(R.at(v_value, v_field_index__677090), R.number("0")), F_c1dep_unify_demon_position(R.at(R.at(v_value, v_field_index__677090), R.number("1"))) });
                v_field_index__677090 = R.add(v_field_index__677090, R.number("1"));
            }
            return v_fields__677065;
        }
        object v_result__677332 = new List<object> {  };
        object v_i__677353 = R.number("0");
        while (R.Bool(R.lt(v_i__677353, R.count(v_value))))
        {
            _ = R.push(v_result__677332, F_c1dep_unify_demon_position(R.at(v_value, v_i__677353)));
            v_i__677353 = R.add(v_i__677353, R.number("1"));
        }
        return v_result__677332;
    }
    public static object F_c1_op_source(object v_line,object v_id) {
        return DvValueTemplate.Instantiate("template.c1_op_source", v_line, v_id);
    }
    public static object F_c1_op_people() {
        return DvValueTemplate.Instantiate("template.c1_op_people");
    }
    public static object F_c1_op_wake_room(object v_id) {
        return DvValueTemplate.Instantiate("template.c1_op_wake_room", v_id);
    }
    public static object F_c1_op_initial_room(object v_id) {
        return DvValueTemplate.Instantiate("template.c1_op_initial_room", v_id);
    }
    public static object F_c1_op_name(object v_id,object v_detective_name) {
        return DvValueTemplate.Instantiate("template.c1_op_name", v_id, v_detective_name);
    }
    public static object F_c1_op_profession(object v_id) {
        return DvValueTemplate.Instantiate("template.c1_op_profession", v_id);
    }
    public static object F_c1_op_person_ref(object v_id,object v_field) {
        return DvValueTemplate.Instantiate("template.c1_op_person_ref", v_id, v_field);
    }
    public static object F_c1_op_known_speaker_label(object v_mind,object v_id) {
        return DvValueTemplate.Instantiate("template.c1_op_known_speaker_label", v_mind, v_id);
    }
    public static object F_c1_op_property(object v_object,object v_field,object v_value) {
        return DvValueTemplate.Instantiate("template.c1_op_property", v_object, v_field, v_value);
    }
    public static object F_c1_op_wake_lines(object v_id) {
        return DvValueTemplate.Instantiate("template.c1_op_wake_lines", v_id);
    }
    public static object F_c1_op_person_object(object v_id,object v_detective_name) {
        return DvValueTemplate.Instantiate("template.c1_op_person_object", v_id, v_detective_name);
    }
    public static object F_c1_op_objects(object v_detective_name) {
        return DvValueTemplate.Instantiate("template.c1_op_objects", v_detective_name);
    }
    public static object F_c1_visible_site_objects() {
        return DvValueTemplate.Instantiate("template.c1_visible_site_objects");
    }
    public static object F_c1_op_wake(object v_标识) {
        return DvValueTemplate.Instantiate("template.c1_op_wake", v_标识);
    }
    public static object F_c1_op_wear_coat() {
        return DvValueTemplate.Instantiate("template.c1_op_wear_coat");
    }
    public static object F_c1_op_prepare_tea() {
        return DvValueTemplate.Instantiate("template.c1_op_prepare_tea");
    }
    public static object F_c1_op_serve_tea() {
        return DvValueTemplate.Instantiate("template.c1_op_serve_tea");
    }
    public static object F_c1_op_bite_silver() {
        return DvValueTemplate.Instantiate("template.c1_op_bite_silver");
    }
    public static object F_c1_op_scrape_testpiece() {
        return DvValueTemplate.Instantiate("template.c1_op_scrape_testpiece");
    }
    public static object F_c1_op_play_audition() {
        return DvValueTemplate.Instantiate("template.c1_op_play_audition");
    }
    public static object F_c1_op_rules() {
        return DvValueTemplate.Instantiate("template.c1_op_rules");
    }
    public static object F_c1_op_presentation(object v_id,object v_voice,object v_lines) {
        return DvValueTemplate.Instantiate("template.c1_op_presentation", v_id, v_voice, v_lines);
    }
    public static object F_c1_op_wake_action(object v_id) {
        return DvValueTemplate.Instantiate("template.c1_op_wake_action", v_id);
    }
    public static object F_c1_op_physical_action(object v_id,object v_name,object v_law,object v_grants,object v_source,object v_label,object v_surface) {
        return DvValueTemplate.Instantiate("template.c1_op_physical_action", v_id, v_name, v_law, v_grants, v_source, v_label, v_surface);
    }
    public static object F_c1_op_reward_actions() {
        return DvValueTemplate.Instantiate("template.c1_op_reward_actions");
    }
    public static object F_c1_op_dialogue(object v_id,object v_name,object v_literal,object v_label,object v_surface,object v_source,object v_listeners) {
        return DvValueTemplate.Instantiate("template.c1_op_dialogue", v_id, v_name, v_literal, v_label, v_surface, v_source, v_listeners);
    }
    public static object F_c1_op_question(object v_id,object v_name,object v_label,object v_surface,object v_source,object v_listeners) {
        return DvValueTemplate.Instantiate("template.c1_op_question", v_id, v_name, v_label, v_surface, v_source, v_listeners);
    }
    public static object F_c1_op_reward_refs(object v_id) {
        return DvValueTemplate.Instantiate("template.c1_op_reward_refs", v_id);
    }
    public static object F_c1_op_reward_requirement(object v_id) {
        return DvValueTemplate.Instantiate("template.c1_op_reward_requirement", v_id);
    }
    public static object F_c1_op_pragmatic_policy(object v_id,object v_policy) {
        return DvValueTemplate.Instantiate("template.c1_op_pragmatic_policy", v_id, v_policy);
    }
    public static object F_c1_op_morning_candidates() {
        return DvValueTemplate.Instantiate("template.c1_op_morning_candidates");
    }
    public static object F_c1_op_meet_candidates() {
        return DvValueTemplate.Instantiate("template.c1_op_meet_candidates");
    }
    public static object F_c1_op_tea_candidates(object v_detective_name) {
        return DvValueTemplate.Instantiate("template.c1_op_tea_candidates", v_detective_name);
    }
    public static object F_c1_op_broadcast_candidates() {
        return DvValueTemplate.Instantiate("template.c1_op_broadcast_candidates");
    }
    public static object F_c1_op_reward_candidates(object v_detective_name) {
        return DvValueTemplate.Instantiate("template.c1_op_reward_candidates", v_detective_name);
    }
    public static object F_c1_op_desire_candidates(object v_detective_name) {
        return DvValueTemplate.Instantiate("template.c1_op_desire_candidates", v_detective_name);
    }
    public static object F_c1_op_maintenance_actions() {
        return DvValueTemplate.Instantiate("template.c1_op_maintenance_actions");
    }
    public static object F_c1_op_scene(object v_detective_name) {
        return DvValueTemplate.Instantiate("template.c1_op_scene", v_detective_name);
    }
    public static object F_c1_op_install_world(object v_world,object v_detective_name) {
        object v_result__740069 = F_c1_add_objects(v_world, F_c1_op_objects(v_detective_name));
        object v_people__740140 = F_c1_op_people();
        object v_i__740173 = R.number("0");
        while (R.Bool(R.lt(v_i__740173, R.count(v_people__740140))))
        {
            v_result__740069 = F_dv_store(v_result__740069, F_dv_ref(R.at(v_people__740140, v_i__740173), "state", "location"), F_dv_field(F_c1_op_initial_room(R.at(v_people__740140, v_i__740173))));
            v_result__740069 = F_dv_store(v_result__740069, F_dv_ref(R.at(v_people__740140, v_i__740173), Authored("m40.c1_visible_site_objects.literal_0002"), Authored("m40.c1_visible_site_objects.literal_0003")), F_dv_with(F_dv_field(DvValueTemplate.Instantiate("template.c1_op_initial_awake", R.at(v_people__740140, v_i__740173))), "basis", F_dv_single(F_c1_memory_source(R.at(v_people__740140, v_i__740173)))));
            v_i__740173 = R.add(v_i__740173, R.number("1"));
        }
        object v_rules__740646 = F_c1_op_rules();
        v_i__740173 = R.number("0");
        while (R.Bool(R.lt(v_i__740173, R.count(v_rules__740646))))
        {
            v_result__740069 = F_dv_adopt(v_result__740069, F_campaign_method(F_dv_get(R.at(v_rules__740646, v_i__740173), "name"), F_dv_get(R.at(v_rules__740646, v_i__740173), "law")), "enforce", R.number("1"), F_dv_single(F_dv_get(R.at(v_rules__740646, v_i__740173), "source")));
            v_i__740173 = R.add(v_i__740173, R.number("1"));
        }
        return v_result__740069;
    }
    public static object F_c1_op_install_actor(object v_actor,object v_detective_name) {
        object v_id__740931 = F_dv_get(v_actor, "id");
        object v_mind__740954 = F_dv_get(v_actor, "mind");
        object v_objects__740981 = F_c1_op_objects(v_detective_name);
        object v_i__741030 = R.number("0");
        while (R.Bool(R.lt(v_i__741030, R.count(v_objects__740981))))
        {
            v_mind__740954 = F_dv_add_object(v_mind__740954, F_campaign_local_object(F_dv_get(R.at(v_objects__740981, v_i__741030), "id")));
            v_i__741030 = R.add(v_i__741030, R.number("1"));
        }
        object v_initial_evidence = DvValueTemplate.Instantiate("template.c1_personal_initial_evidence", v_id__740931);
        foreach (object v_entry in R.Items(v_initial_evidence))
        {
            v_mind__740954 = F_dv_store(v_mind__740954, F_dv_get(v_entry, "ref"),
                F_dv_with(F_dv_field(F_dv_get(v_entry, "value")), "basis", F_dv_get(v_entry, "basis")));
        }
        if (R.Bool(F_dv_has(F_c1_op_people(), v_id__740931)))
        {
            v_mind__740954 = F_dv_store(v_mind__740954, F_dv_ref(v_id__740931, "state", "location"), F_dv_field(F_c1_op_initial_room(v_id__740931)));
            v_actor = F_dv_with(v_actor, "location", F_c1_op_initial_room(v_id__740931));
            object v_own__741378 = F_c1_op_person_object(v_id__740931, v_detective_name);
            object v_fields__741437 = F_dv_get(F_dv_get(v_own__741378, "facets"), "state");
            object v_self_source__741476 = F_c1_op_source(Authored("m40.c1_move_action.literal_0003"), Authored("m40.c1_op_install_actor.literal"));
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_install_actor.468", v_id__740931)))
            {
                v_self_source__741476 = F_c1_op_source(Authored("m40.c1_op_meet_candidates.literal_0009"), Authored("m40.c1_op_meet_candidates.literal_0010"));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_install_actor.469", v_id__740931)))
            {
                v_self_source__741476 = F_c1_op_source(Authored("m40.c1_raw_person_policy.literal_0002"), Authored("m40.c1_op_meet_candidates.literal_0006"));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_install_actor.470", v_id__740931)))
            {
                v_self_source__741476 = F_c1_op_source(Authored("m40.c1_other_departure_source.literal_0009"), Authored("m40.c1_op_meet_candidates.literal_0013"));
            }
            object v_k__742509 = R.number("0");
            while (R.Bool(R.lt(v_k__742509, R.count(v_fields__741437))))
            {
                object v_basis__742569 = F_dv_single(v_self_source__741476);
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_install_actor.471", v_fields__741437, v_k__742509)))
                {
                    v_basis__742569 = F_dv_single(F_c1_memory_source(v_id__740931));
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_install_actor.472", v_fields__741437, v_k__742509)))
                {
                    v_basis__742569 = DvValueTemplate.Instantiate("template.inline.c1_op_install_actor.473");
                }
                v_mind__740954 = F_dv_store(v_mind__740954, F_c1_op_person_ref(v_id__740931, R.at(R.at(v_fields__741437, v_k__742509), R.number("0"))), F_dv_with(R.at(R.at(v_fields__741437, v_k__742509), R.number("1")), "basis", v_basis__742569));
                v_k__742509 = R.add(v_k__742509, R.number("1"));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_install_actor.474", v_id__740931)))
            {
                v_mind__740954 = F_dv_store(v_mind__740954, F_dv_ref(Authored("m40.c1_inspect_E10.literal_0009"), Authored("m40.c1_answer_topic.return"), "owner"), F_dv_unknown());
            }
            v_actor = F_dv_with(v_actor, "policy", F_c1_op_pragmatic_policy(v_id__740931, F_dv_get(v_actor, "policy")));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_install_actor.475", v_id__740931)))
        {
            object v_whitelist__743688 = AuthoredCopy("m40.c1_op_install_actor.whitelist");
            v_i__741030 = R.number("0");
            while (R.Bool(R.lt(v_i__741030, R.count(v_objects__740981))))
            {
                if (R.Bool(F_dv_has(v_whitelist__743688, F_dv_get(R.at(v_objects__740981, v_i__741030), "id"))))
                {
                    object v_fields__743912 = F_dv_get(F_dv_get(R.at(v_objects__740981, v_i__741030), "facets"), "state");
                    object v_j__743966 = R.number("0");
                    while (R.Bool(R.lt(v_j__743966, R.count(v_fields__743912))))
                    {
                        object v_ref__744042 = F_dv_ref(F_dv_get(R.at(v_objects__740981, v_i__741030), "id"), "state", R.at(R.at(v_fields__743912, v_j__743966), R.number("0")));
                        v_mind__740954 = F_dv_store(v_mind__740954, v_ref__744042, DvValueTemplate.Instantiate("template.inline.c1_op_install_actor.476", v_fields__743912, v_j__743966));
                        v_j__743966 = R.add(v_j__743966, R.number("1"));
                    }
                }
                v_i__741030 = R.add(v_i__741030, R.number("1"));
            }
        }
        object v_rules__744557 = F_c1_op_rules();
        v_i__741030 = R.number("0");
        while (R.Bool(R.lt(v_i__741030, R.count(v_rules__744557))))
        {
            if (R.Bool(R.eq(F_c1_op_rule_owner(F_dv_get(R.at(v_rules__744557, v_i__741030), "name")), v_id__740931)))
            {
                v_mind__740954 = F_dv_adopt(v_mind__740954, F_campaign_method(F_dv_get(R.at(v_rules__744557, v_i__741030), "name"), F_dv_get(R.at(v_rules__744557, v_i__741030), "law")), "predict", R.number("1"), F_dv_single(F_dv_get(R.at(v_rules__744557, v_i__741030), "source")));
            }
            v_i__741030 = R.add(v_i__741030, R.number("1"));
        }
        if (R.Bool(F_dv_has(F_c1_op_people(), v_id__740931)))
        {
            v_mind__740954 = F_dv_store(v_mind__740954, F_dv_ref(v_id__740931, Authored("m40.c1_visible_site_objects.literal_0002"), Authored("m40.c1_visible_site_objects.literal_0003")), F_dv_with(F_dv_field(DvValueTemplate.Instantiate("template.c1_op_initial_awake", v_id__740931)), "basis", F_dv_single(F_c1_memory_source(v_id__740931))));
        }
        object v_pool__745132 = F_c1_op_flat_candidates(v_detective_name);
        object v_replacements__745186 = new List<object> {  };
        v_i__741030 = R.number("0");
        while (R.Bool(R.lt(v_i__741030, R.count(v_pool__745132))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(v_pool__745132, v_i__741030), "owner"), v_id__740931)))
            {
                _ = R.push(v_replacements__745186, R.at(v_pool__745132, v_i__741030));
            }
            v_i__741030 = R.add(v_i__741030, R.number("1"));
        }
        object v_actions__745351 = new List<object> {  };
        v_i__741030 = R.number("0");
        while (R.Bool(R.lt(v_i__741030, R.count(F_dv_get(v_actor, "actions")))))
        {
            object v_old__745428 = R.at(F_dv_get(v_actor, "actions"), v_i__741030);
            object v_replace__745464 = F_dv_has(AuthoredCopy("m40.c1_op_install_actor.literal_0004"), F_dv_get(v_old__745428, "id"));
            object v_j__745594 = R.number("0");
            while (R.Bool(R.lt(v_j__745594, R.count(v_replacements__745186))))
            {
                if (R.Bool(R.eq(F_dv_get(v_old__745428, "id"), F_dv_get(R.at(v_replacements__745186, v_j__745594), "id"))))
                {
                    v_replace__745464 = true;
                }
                v_j__745594 = R.add(v_j__745594, R.number("1"));
            }
            if (R.Bool(R.not(v_replace__745464)))
            {
                _ = R.push(v_actions__745351, v_old__745428);
            }
            v_i__741030 = R.add(v_i__741030, R.number("1"));
        }
        v_i__741030 = R.number("0");
        while (R.Bool(R.lt(v_i__741030, R.count(v_replacements__745186))))
        {
            _ = R.push(v_actions__745351, R.at(v_replacements__745186, v_i__741030));
            v_i__741030 = R.add(v_i__741030, R.number("1"));
        }
        v_actor = F_dv_with_many(v_actor, new List<object> { F_dv_pair("mind", v_mind__740954), F_dv_pair("actions", v_actions__745351) });
        return F_c1_op_install_purposes(v_actor);
    }
    public static object F_c1_op_bindings(object v_detective_name) {
        return DvValueTemplate.Instantiate("template.c1_op_bindings", v_detective_name);
    }
    public static object F_c1_opening_install_scene(object v_scenario,object v_detective_name) {
        _ = F_dv_require(R.not(R.eq(v_detective_name, "")), Authored("m40.c1_opening_install_scene.literal"));
        object v_result__746695 = F_dv_with(v_scenario, "physical", F_c1_op_install_world(F_dv_get(v_scenario, "physical"), v_detective_name));
        object v_actors__746799 = new List<object> {  };
        object v_i__746820 = R.number("0");
        while (R.Bool(R.lt(v_i__746820, R.count(F_dv_get(v_scenario, "actors")))))
        {
            _ = R.push(v_actors__746799, F_c1_op_install_actor(R.at(F_dv_get(v_scenario, "actors"), v_i__746820), v_detective_name));
            v_i__746820 = R.add(v_i__746820, R.number("1"));
        }
        object v_bindings__746978 = F_dv_get(v_scenario, "bindings");
        object v_new_bindings__747016 = F_c1_op_bindings(v_detective_name);
        v_i__746820 = R.number("0");
        while (R.Bool(R.lt(v_i__746820, R.count(v_new_bindings__747016))))
        {
            _ = R.push(v_bindings__746978, R.at(v_new_bindings__747016, v_i__746820));
            v_i__746820 = R.add(v_i__746820, R.number("1"));
        }
        return F_dv_with_many(v_result__746695, new List<object> { F_dv_pair("actors", v_actors__746799), F_dv_pair("bindings", v_bindings__746978), F_dv_pair("opening_scene", F_c1_op_scene(v_detective_name)) });
    }
    public static object F_c1_op_desire(object v_id) {
        return DvValueTemplate.Instantiate("template.c1_op_desire", v_id);
    }
    public static object F_c1_op_rule_owner(object v_name) {
        return DvValueTemplate.Instantiate("template.c1_op_rule_owner", v_name);
    }
    public static object F_c1_op_reward_object(object v_id) {
        return DvValueTemplate.Instantiate("template.c1_op_reward_object", v_id);
    }
    public static object F_c1_op_invitation_requirement() {
        return DvValueTemplate.Instantiate("template.c1_op_invitation_requirement");
    }
    public static object F_c1_op_slot_inspections() {
        return DvValueTemplate.Instantiate("template.c1_op_slot_inspections");
    }
    public static object F_c1_op_request(object v_action,object v_target,object v_topic) {
        return DvValueTemplate.Instantiate("template.c1_op_request", v_action, v_target, v_topic);
    }
    public static object F_c1_op_appropriate_candidate(object v_action) {
        object v_id__755189 = F_dv_get(v_action, "owner");
        object v_name__755216 = F_dv_get(v_action, "id");
        if (R.Bool(R.not(R.eq(F_dv_get(v_action, "law"), ""))))
        {
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.477", v_action);
        }
        if (R.Bool(F_dv_has_key(v_action, "speech")))
        {
            object v_targets__755437 = new List<object> {  };
            object v_listeners__755463 = F_dv_find(v_action, "listeners", new List<object> {  });
            object v_i__755521 = R.number("0");
            while (R.Bool(R.lt(v_i__755521, R.count(v_listeners__755463))))
            {
                if (R.Bool(R.not(R.eq(R.at(v_listeners__755463, v_i__755521), v_id__755189))))
                {
                    _ = R.push(v_targets__755437, R.at(v_listeners__755463, v_i__755521));
                }
                v_i__755521 = R.add(v_i__755521, R.number("1"));
            }
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.478", v_action, v_targets__755437);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.479", v_name__755216)))
        {
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.480", v_action);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.481", v_name__755216)))
        {
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.482", v_action);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.483", v_name__755216)))
        {
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.484", v_action);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.485", v_name__755216)))
        {
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.486", v_action);
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.487", v_action);
            v_action = F_c1_op_request(v_action, Authored("m40.c1_talk_recalled_names.literal_0006"), Authored("m40.c1_op_appropriate_candidate.literal_0006"));
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.488", v_action);
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.489", v_action);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.490", v_name__755216)))
        {
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.491", v_action);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.492", v_name__755216)))
        {
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.493", v_action);
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.494", v_action);
            v_action = F_c1_op_request(v_action, Authored("m40.c1_inspect_E9.literal"), Authored("m40.c1_op_appropriate_candidate.literal_0010"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.495", v_name__755216)))
        {
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.496", v_action);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.497", v_name__755216)))
        {
            v_action = F_c1_op_request(v_action, Authored("m40.c1_observe_as.literal_0005"), Authored("m40.c1_op_appropriate_candidate.literal_0011"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.498", v_name__755216)))
        {
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.499", v_action);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.500", v_name__755216)))
        {
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.501", v_action);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.502", v_name__755216)))
        {
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.503", v_action);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.504", v_name__755216)))
        {
            v_action = F_dv_without(v_action, Authored("m40.c1_other_place_detective.literal_0016"));
        }
        object v_broadcasts__762600 = AuthoredCopy("m40.c1_op_appropriate_candidate.broadcasts");
        object v_broadcast_index__762719 = R.number("1");
        while (R.Bool(R.lt(v_broadcast_index__762719, R.count(v_broadcasts__762600))))
        {
            if (R.Bool(R.eq(v_name__755216, R.at(v_broadcasts__762600, v_broadcast_index__762719))))
            {
                v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.505", v_action, v_broadcasts__762600, v_broadcast_index__762719);
            }
            v_broadcast_index__762719 = R.add(v_broadcast_index__762719, R.number("1"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.506", v_name__755216)))
        {
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.507", v_action, v_id__755189);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.508", v_name__755216)))
        {
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.509", v_action, v_id__755189);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.510", v_name__755216)))
        {
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.511", v_action);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.512", v_name__755216)))
        {
            v_action = DvValueTemplate.Instantiate("template.inline.c1_op_appropriate_candidate.513", v_action);
        }
        return v_action;
    }
    public static object F_c1_op_flat_candidates(object v_detective_name) {
        object v_catalogs__765366 = new List<object> { F_c1_op_morning_candidates(), F_c1_op_meet_candidates(), F_c1_op_tea_candidates(v_detective_name), F_c1_op_broadcast_candidates(), F_c1_op_reward_candidates(v_detective_name), F_c1_op_desire_candidates(v_detective_name), F_c1_op_slot_inspections(), F_c1_op_maintenance_actions(), F_c1_op_contract_candidates() };
        object v_result__765674 = new List<object> {  };
        object v_i__765695 = R.number("0");
        while (R.Bool(R.lt(v_i__765695, R.count(v_catalogs__765366))))
        {
            object v_rows__765749 = R.at(v_catalogs__765366, v_i__765695);
            object v_j__765781 = R.number("0");
            while (R.Bool(R.lt(v_j__765781, R.count(v_rows__765749))))
            {
                _ = R.push(v_result__765674, F_c1_op_appropriate_candidate(R.at(v_rows__765749, v_j__765781)));
                v_j__765781 = R.add(v_j__765781, R.number("1"));
            }
            v_i__765695 = R.add(v_i__765695, R.number("1"));
        }
        _ = R.push(v_result__765674, DvValueTemplate.Instantiate("template.inline.c1_op_flat_candidates.514"));
        _ = R.push(v_result__765674, DvValueTemplate.Instantiate("template.inline.c1_op_flat_candidates.515"));
        return v_result__765674;
    }
    public static object F_c1_op_know_goal(object v_id,object v_ref,object v_basis,object v_available) {
        return DvValueTemplate.Instantiate("template.c1_op_know_goal", v_id, v_ref, v_basis, v_available);
    }
    public static object F_c1_op_achieve_goal(object v_id,object v_proposition,object v_basis,object v_available) {
        return DvValueTemplate.Instantiate("template.c1_op_achieve_goal", v_id, v_proposition, v_basis, v_available);
    }
    public static object F_c1_op_communicate_goal(object v_id,object v_method,object v_basis,object v_available) {
        return DvValueTemplate.Instantiate("template.c1_op_communicate_goal", v_id, v_method, v_basis, v_available);
    }
    public static object F_c1_op_owned_goals(object v_id) {
        return DvValueTemplate.Instantiate("template.c1_op_owned_goals", v_id);
    }
    public static object F_c1_op_install_purposes(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1_op_install_purposes", v_actor);
    }
    public static object F_c1_op_contract_candidates() {
        return DvValueTemplate.Instantiate("template.c1_op_contract_candidates");
    }
    public static object F_c1n_source(object v_id) {
        return DvValueTemplate.Instantiate("template.c1n_source", v_id);
    }
    public static object F_c1n_mark(object v_id) {
        return DvValueTemplate.Instantiate("template.c1n_mark", v_id);
    }
    public static object F_c1n_judgment_ref() {
        return DvValueTemplate.Instantiate("template.c1n_judgment_ref");
    }
    public static object F_c1n_row(object v_actor,object v_id,object v_start,object v_end,object v_when,object v_label,object v_text) {
        return DvValueTemplate.Instantiate("template.c1n_row", v_actor, v_id, v_start, v_end, v_when, v_label, v_text);
    }
    public static object F_c1n_rows() {
        object v_key__783084 = AuthoredCopy("m40.c1n_rows.key");
        object v_cached__783170 = R.memo_get(new List<object> {  }, v_key__783084);
        if (R.Bool(R.at(v_cached__783170, R.number("0"))))
        {
            return R.at(v_cached__783170, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__783084, F_c1n_rows_build());
    }
    public static object F_c1n_rows_build() {
        object v_rows__783329 = new List<object> {  };
        object v_meet__783348 = DvValueTemplate.Instantiate("template.inline.c1n_rows_build.516");
        _ = R.push(v_rows__783329, F_c1n_row(Authored("m40.c1_other_people_build.literal"), Authored("m40.c1n_rows_build.literal"), Authored("m40.c1n_rows_build.literal_0002"), Authored("m40.c1n_rows_build.literal_0003"), v_meet__783348, Authored("m40.c1n_rows_build.literal_0004"), Authored("m40.c1n_rows_build.literal_0005")));
        object v_apron__784300 = F_c1n_row(Authored("m40.c1_inspect_E9.literal"), Authored("m40.c1n_rows_build.literal_0006"), Authored("m40.c1n_rows_build.literal_0002"), Authored("m40.c1n_rows_build.literal_0003"), F_campaign_at(Authored("m40.c1_other_people_build.literal"), Authored("m40.c1_sound_agreement.literal")), Authored("m40.c1n_rows_build.literal_0007"), Authored("m40.c1n_rows_build.literal_0008"));
        _ = R.push(v_rows__783329, DvValueTemplate.Instantiate("template.inline.c1n_rows_build.517", v_apron__784300));
        object v_show__785103 = F_c1n_row(Authored("m40.c1_other_people_build.literal_0031"), Authored("m40.c1n_rows_build.literal_0009"), Authored("m40.c1n_rows_build.literal_0010"), Authored("m40.c1n_rows_build.literal_0011"), DvValueTemplate.Instantiate("template.inline.c1n_rows_build.518"), Authored("m40.c1n_rows_build.literal_0012"), Authored("m40.c1n_rows_build.literal_0013"));
        v_show__785103 = DvValueTemplate.Instantiate("template.inline.c1n_rows_build.519", v_show__785103);
        _ = R.push(v_rows__783329, v_show__785103);
        object v_restriction__786882 = F_c1n_row(Authored("m40.c1_other_people_build.literal_0031"), Authored("m40.c1n_rows_build.literal_0016"), Authored("m40.c1n_rows_build.literal_0010"), Authored("m40.c1n_rows_build.literal_0011"), DvValueTemplate.Instantiate("template.c1f_argument_present"), Authored("m40.c1n_rows_build.literal_0017"), Authored("m40.c1n_rows_build.literal_0018"));
        v_restriction__786882 = DvValueTemplate.Instantiate("template.inline.c1n_rows_build.520", v_restriction__786882);
        _ = R.push(v_rows__783329, v_restriction__786882);
        object v_inspected__787962 = DvValueTemplate.Instantiate("template.inline.c1n_rows_build.521");
        object v_uncommitted__788344 = F_dv_fact(F_c1n_judgment_ref(), Authored("m40.c1n_rows_build.literal_0022"));
        object v_judgment__788467 = F_c1n_row(Authored("m40.c1_talk_recalled_names.literal_0006"), Authored("m40.c1n_rows_build.literal_0023"), Authored("m40.c1n_rows_build.literal_0010"), Authored("m40.c1n_rows_build.literal_0011"), F_dv_all(new List<object> { v_inspected__787962, v_uncommitted__788344 }), Authored("m40.c1n_rows_build.literal_0024"), Authored("m40.c1n_rows_build.literal_0025"));
        v_judgment__788467 = DvValueTemplate.Instantiate("template.inline.c1n_rows_build.522", v_judgment__788467);
        _ = R.push(v_rows__783329, v_judgment__788467);
        object v_assent__789325 = F_c1n_row(Authored("m40.c1_talk_recalled_names.literal_0006"), Authored("m40.c1n_rows_build.literal_0028"), Authored("m40.c1n_rows_build.literal_0010"), Authored("m40.c1n_rows_build.literal_0011"), DvValueTemplate.Instantiate("template.inline.c1n_rows_build.523", v_uncommitted__788344), Authored("m40.c1n_rows_build.literal_0029"), Authored("m40.c1n_rows_build.literal_0030"));
        v_assent__789325 = DvValueTemplate.Instantiate("template.inline.c1n_rows_build.524", v_assent__789325);
        _ = R.push(v_rows__783329, v_assent__789325);
        object v_defer__790340 = F_c1n_row(Authored("m40.c1_other_people_build.literal_0047"), Authored("m40.c1n_rows_build.literal_0031"), Authored("m40.c1n_rows_build.literal_0010"), Authored("m40.c1n_rows_build.literal_0011"), F_dv_fact(F_c1n_judgment_ref(), Authored("m40.c1n_rows_build.literal_0027")), Authored("m40.c1n_rows_build.literal_0032"), Authored("m40.c1n_rows_build.literal_0033"));
        v_defer__790340 = DvValueTemplate.Instantiate("template.inline.c1n_rows_build.525", v_defer__790340);
        _ = R.push(v_rows__783329, v_defer__790340);
        _ = R.push(v_rows__783329, F_c1n_row(Authored("m40.c1_talk_recalled_names.literal_0006"), Authored("m40.c1n_rows_build.literal_0035"), Authored("m40.c1n_rows_build.literal_0036"), Authored("m40.c1n_rows_build.literal_0037"), F_dv_true(), Authored("m40.c1n_rows_build.literal_0038"), Authored("m40.c1n_rows_build.literal_0039")));
        object v_count__791537 = F_c1n_row(Authored("m40.c1_inspect_E9.literal"), Authored("m40.c1n_rows_build.literal_0040"), Authored("m40.c1n_rows_build.literal_0041"), Authored("m40.c1n_rows_build.literal_0042"), F_dv_true(), Authored("m40.c1n_rows_build.literal_0043"), Authored("m40.c1n_rows_build.literal_0044"));
        v_count__791537 = DvValueTemplate.Instantiate("template.inline.c1n_rows_build.526", v_count__791537);
        _ = R.push(v_rows__783329, v_count__791537);
        _ = R.push(v_rows__783329, F_c1n_row(Authored("m40.c1_other_people_build.literal"), Authored("m40.c1n_rows_build.literal_0056"), Authored("m40.c1_other_activity_declarations.literal_0078"), Authored("m40.c1n_rows_build.literal_0057"), DvValueTemplate.Instantiate("template.inline.c1n_rows_build.529"), Authored("m40.c1n_rows_build.literal_0060"), Authored("m40.c1n_rows_build.literal_0061")));
        object v_partner__797666 = F_c1n_row(Authored("m40.c1_other_people_build.literal"), Authored("post.c1n_rows_build.0001"), Authored("m40.c1_other_activity_declarations.literal_0078"), Authored("m40.c1n_rows_build.literal_0057"), DvValueTemplate.Instantiate("template.inline.c1n_rows_build.530"), Authored("m40.c1n_rows_build.literal_0062"), Authored("m40.c1n_rows_build.literal_0063"));
        v_partner__797666 = DvValueTemplate.Instantiate("template.inline.c1n_rows_build.531", v_partner__797666);
        _ = R.push(v_rows__783329, v_partner__797666);
        object v_accept__798878 = F_c1n_row(Authored("m40.c1_talk_recalled_names.literal_0006"), Authored("m40.c1n_rows_build.literal_0065"), Authored("m40.c1_other_activity_declarations.literal_0078"), Authored("m40.c1n_rows_build.literal_0057"), DvValueTemplate.Instantiate("template.inline.c1n_rows_build.532"), Authored("m40.c1n_rows_build.literal_0066"), Authored("m40.c1n_rows_build.literal_0067"));
        v_accept__798878 = DvValueTemplate.Instantiate("template.inline.c1n_rows_build.533", v_accept__798878);
        _ = R.push(v_rows__783329, v_accept__798878);
        _ = R.push(v_rows__783329, F_c1n_row(Authored("m40.c1_inspect_E7.literal"), Authored("m40.c1n_rows_build.literal_0070"), Authored("m40.c1_other_activity_declarations.literal_0078"), Authored("m40.c1n_rows_build.literal_0057"), F_campaign_at(Authored("m40.c1_talk_recalled_names.literal_0006"), Authored("m40.c1_sound_agreement.literal")), Authored("m40.c1n_rows_build.literal_0071"), Authored("m40.c1n_rows_build.literal_0072")));
        object v_watch__800781 = F_c1n_row(Authored("m40.c1_talk_recalled_names.literal_0006"), Authored("m40.c1n_rows_build.literal_0073"), Authored("m40.c1_other_activity_declarations.literal_0078"), Authored("m40.c1n_rows_build.literal_0057"), F_campaign_at(Authored("m40.c1_inspect_E7.literal"), Authored("m40.c1_sound_agreement.literal")), Authored("m40.c1n_rows_build.literal_0074"), Authored("m40.c1n_rows_build.literal_0075"));
        v_watch__800781 = DvValueTemplate.Instantiate("template.inline.c1n_rows_build.534", v_watch__800781);
        _ = R.push(v_rows__783329, v_watch__800781);
        object v_first_watch__803697 = F_c1n_row(Authored("m40.c1_inspect_E7.literal"), Authored("m40.c1n_rows_build.literal_0081"), Authored("m40.c1n_rows_build.literal_0082"), Authored("m40.c1n_rows_build.literal_0057"), F_dv_fact(F_campaign_first_night_ref(Authored("m40.c1n_rows_build.literal_0077")), true), Authored("m40.c1n_rows_build.literal_0083"), Authored("m40.c1n_rows_build.literal_0084"));
        v_first_watch__803697 = DvValueTemplate.Instantiate("template.inline.c1n_rows_build.536", v_first_watch__803697);
        _ = R.push(v_rows__783329, v_first_watch__803697);
        object v_second_watch__804413 = F_c1n_row(Authored("m40.c1_talk_recalled_names.literal_0006"), Authored("m40.c1n_rows_build.literal_0085"), Authored("m40.c1n_rows_build.literal_0057"), Authored("m40.c1_other_activity_declarations.literal_0079"), F_dv_fact(F_campaign_first_night_ref(Authored("m40.c1n_rows_build.literal_0077")), true), Authored("m40.c1n_rows_build.literal_0086"), Authored("m40.c1n_rows_build.literal_0087"));
        v_second_watch__804413 = DvValueTemplate.Instantiate("template.inline.c1n_rows_build.537", v_second_watch__804413);
        _ = R.push(v_rows__783329, v_second_watch__804413);
        object v_lure_targets__805163 = AuthoredCopy("m40.c1n_rows_build.lure_targets");
        object v_lure_index__805273 = R.number("0");
        while (R.Bool(R.lt(v_lure_index__805273, R.count(v_lure_targets__805163))))
        {
            object v_target__805349 = R.at(v_lure_targets__805163, v_lure_index__805273);
            object v_site__805396 = F_c1_deep_resonance_site(v_target__805349);
            object v_literal__805447 = F_c1_statement(F_dv_get(v_site__805396, "evidence"));
            object v_text__805498 = Authored("m40.c1n_rows_build.text");
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_rows_build.538", v_target__805349)))
            {
                v_text__805498 = Authored("m40.c1n_rows_build.literal_0088");
            }
            object v_lure__805760 = F_c1n_row(Authored("m40.c1_talk_recalled_names.literal_0006"), R.cat(Authored("m40.c1n_rows_build.literal_0089"), v_target__805349), Authored("m40.c1n_rows_build.literal_0041"), Authored("m40.c1n_rows_build.literal_0057"), v_literal__805447, Authored("m40.c1n_rows_build.literal_0090"), v_text__805498);
            object v_source__806187 = F_c1n_source(Authored("m40.c1n_rows_build.literal_0091"));
            v_lure__805760 = DvValueTemplate.Instantiate("template.inline.c1n_rows_build.539", v_lure__805760, v_literal__805447, v_target__805349, v_source__806187);
            _ = R.push(v_rows__783329, v_lure__805760);
            v_lure_index__805273 = R.add(v_lure_index__805273, R.number("1"));
        }
        object v_brush_offs__807390 = F_sims_brush_off_rows();
        object v_brush_index__807434 = R.number("0");
        while (R.Bool(R.lt(v_brush_index__807434, R.count(v_brush_offs__807390))))
        {
            object v_spec__807510 = R.at(v_brush_offs__807390, v_brush_index__807434);
            object v_entry__807554 = F_c1n_row(F_dv_get(v_spec__807510, "actor"), F_dv_get(v_spec__807510, "id"), F_dv_get(v_spec__807510, "start"), F_dv_get(v_spec__807510, "end"), F_dv_get(v_spec__807510, "when"), F_dv_get(v_spec__807510, "label"), F_dv_get(v_spec__807510, "text"));
            if (R.Bool(F_dv_has_key(v_spec__807510, "kind")))
            {
                v_entry__807554 = F_dv_with(v_entry__807554, "kind", F_dv_get(v_spec__807510, "kind"));
            }
            if (R.Bool(F_dv_has_key(v_spec__807510, "received")))
            {
                v_entry__807554 = F_dv_with(v_entry__807554, "received", F_dv_get(v_spec__807510, "received"));
            }
            if (R.Bool(F_dv_has_key(v_spec__807510, "manual_only")))
            {
                v_entry__807554 = F_dv_with(v_entry__807554, "manual_only", F_dv_get(v_spec__807510, "manual_only"));
            }
            v_entry__807554 = F_dv_with_many(v_entry__807554, new List<object> { F_dv_pair("public_observers", F_campaign_people()), F_dv_pair("public_grants", new List<object> { F_c1n_mark(F_dv_get(v_entry__807554, "id")) }) });
            _ = R.push(v_rows__783329, v_entry__807554);
            v_brush_index__807434 = R.add(v_brush_index__807434, R.number("1"));
        }
        return F_c1f_join(v_rows__783329, F_c1f_rows());
    }
    public static object F_c1n_pre(object v_row) {
        return DvValueTemplate.Instantiate("template.c1n_pre", v_row);
    }
    public static object F_c1n_law(object v_条目) {
        return DvValueTemplate.Instantiate("template.c1n_law", v_条目);
    }
    public static object F_c1n_action(object v_row) {
        object v_action__808639 = F_campaign_surface_action(F_dv_get(v_row, "id"), F_dv_get(v_row, "kind"), F_c1n_pre(v_row), F_dv_all(new List<object> { F_c1n_pre(v_row), F_dv_find(v_row, "physical_requires", F_dv_true()) }), F_dv_get(v_row, "id"), F_dv_append(F_dv_get(v_row, "grants"), F_c1n_mark(F_dv_get(v_row, "id"))), F_dv_get(v_row, "label"), F_dv_get(v_row, "text"), F_c1n_source(F_dv_get(v_row, "id")));
        v_action__808639 = DvValueTemplate.Instantiate("template.inline.c1n_action.540", v_action__808639, v_row);
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_action.541", v_row)))
        {
            object v_listeners__809044 = new List<object> {  };
            object v_people__809072 = F_campaign_people();
            object v_listener_index__809112 = R.number("0");
            while (R.Bool(R.lt(v_listener_index__809112, R.count(v_people__809072))))
            {
                if (R.Bool(R.not(R.eq(R.at(v_people__809072, v_listener_index__809112), F_dv_get(v_row, "actor")))))
                {
                    _ = R.push(v_listeners__809044, R.at(v_people__809072, v_listener_index__809112));
                }
                v_listener_index__809112 = R.add(v_listener_index__809112, R.number("1"));
            }
            v_action__808639 = DvValueTemplate.Instantiate("template.inline.c1n_action.542", v_action__808639, v_row, v_listeners__809044);
        }
        if (R.Bool(F_dv_has_key(v_row, "public_grants")))
        {
            v_action__808639 = F_dv_with(v_action__808639, "public_grants", F_dv_get(v_row, "public_grants"));
        }
        if (R.Bool(F_dv_has_key(v_row, "public_observers")))
        {
            v_action__808639 = F_dv_with(v_action__808639, "public_observers", F_dv_get(v_row, "public_observers"));
        }
        if (R.Bool(F_dv_has_key(v_row, "received")))
        {
            v_action__808639 = DvValueTemplate.Instantiate("template.inline.c1n_action.543", v_action__808639, v_row);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_action.544", v_row)))
        {
            v_action__808639 = F_c1_inquiry_choice(v_action__808639, Authored("m40.c1n_action.literal_0004"), R.eq(F_dv_get(v_row, "id"), Authored("m40.c1n_action.literal_0002")));
        }
        object v_metadata__810398 = AuthoredCopy("m40.c1n_action.metadata");
        object v_metadata_index__810496 = R.number("0");
        while (R.Bool(R.lt(v_metadata_index__810496, R.count(v_metadata__810398))))
        {
            if (R.Bool(F_dv_has_key(v_row, R.at(v_metadata__810398, v_metadata_index__810496))))
            {
                v_action__808639 = F_dv_with(v_action__808639, R.at(v_metadata__810398, v_metadata_index__810496), F_dv_get(v_row, R.at(v_metadata__810398, v_metadata_index__810496)));
            }
            v_metadata_index__810496 = R.add(v_metadata_index__810496, R.number("1"));
        }
        if (R.Bool(F_dv_has_key(v_row, "manipulation")))
        {
            v_action__808639 = F_dv_with(v_action__808639, "speech", F_dv_with(F_dv_get(v_action__808639, "speech"), "manipulation", F_dv_get(v_row, "manipulation")));
        }
        return v_action__808639;
    }
    public static object F_c1n_inspections(object v_actor) {
        return F_c1f_join(DvValueTemplate.Instantiate("template.c1n_inspections", v_actor), DvValueTemplate.Instantiate("template.c1_dialogue_fragments", v_actor));
    }
    public static object F_c1n_goals(object v_actor) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_goals.545", v_actor)))
        {
            return new List<object> {  };
        }
        object v_goals__819174 = new List<object> {  };
        object v_rows__819194 = F_c1n_rows();
        object v_i__819221 = R.number("0");
        while (R.Bool(R.lt(v_i__819221, R.count(v_rows__819194))))
        {
            object v_row__819271 = R.at(v_rows__819194, v_i__819221);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_goals.546", v_row__819271, v_actor)))
            {
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_goals.547", v_row__819271)))
                {
                    _ = R.push(v_goals__819174, DvValueTemplate.Instantiate("template.inline.c1n_goals.548", v_row__819271));
                }
                else
                {
                    _ = R.push(v_goals__819174, F_campaign_goal(F_dv_fact(F_c1n_mark(F_dv_get(v_row__819271, "id")), true), F_c1n_pre(v_row__819271), new List<object> { F_c1n_source(F_dv_get(v_row__819271, "id")) }));
                }
            }
            v_i__819221 = R.add(v_i__819221, R.number("1"));
        }
        object v_inspections__820095 = F_c1f_join(F_c1n_inspections(F_dv_get(v_actor, "id")), F_c1f_inspections(F_dv_get(v_actor, "id")));
        v_i__819221 = R.number("0");
        while (R.Bool(R.lt(v_i__819221, R.count(v_inspections__820095))))
        {
            object v_action__820234 = R.at(v_inspections__820095, v_i__819221);
            object v_j__820271 = R.number("0");
            while (R.Bool(R.lt(v_j__820271, R.count(F_dv_get(v_action__820234, "grants")))))
            {
                object v_goal__820338 = F_campaign_observation_goal(R.at(F_dv_get(v_action__820234, "grants"), v_j__820271), F_dv_get(v_action__820234, "precondition"), F_dv_get(v_action__820234, "source_refs"), R.number("0"));
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_goals.549", v_action__820234, v_actor)))
                {
                    object v_triggers__820727 = DvValueTemplate.Instantiate("template.inline.c1n_goals.550");
                    v_goal__820338 = F_dv_with_many(v_goal__820338, new List<object> { F_dv_pair("refresh_until", F_dv_not(F_dv_true())), F_dv_pair("refresh_refs", v_triggers__820727) });
                }
                _ = R.push(v_goals__819174, v_goal__820338);
                v_j__820271 = R.add(v_j__820271, R.number("1"));
            }
            v_i__819221 = R.add(v_i__819221, R.number("1"));
        }
        _ = R.push(v_goals__819174, F_campaign_observation_goal(F_campaign_clock(), DvValueTemplate.Instantiate("template.inline.c1n_goals.551"), DvValueTemplate.Instantiate("template.inline.c1n_goals.552"), Authored("m40.c1n_goals.literal_0005")));
        return v_goals__819174;
    }
    public static object F_c1_inquiry_room(object v_actor,object v_condition) {
        if (R.Bool(R.eq(F_dv_get(v_condition, "op"), "equals")))
        {
            if (R.Bool(F_dv_same_local_ref(F_dv_get(v_actor, "id"), F_dv_get(v_condition, "ref"), F_dv_get(v_actor, "location_ref"))))
            {
                return F_dv_get(v_condition, "value");
            }
        }
        if (R.Bool(R.eq(F_dv_get(v_condition, "op"), "all")))
        {
            object v_i__821738 = R.number("0");
            while (R.Bool(R.lt(v_i__821738, R.count(F_dv_get(v_condition, "items")))))
            {
                object v_room__821807 = F_c1_inquiry_room(v_actor, R.at(F_dv_get(v_condition, "items"), v_i__821738));
                if (R.Bool(R.not(R.eq(v_room__821807, ""))))
                {
                    return v_room__821807;
                }
                v_i__821738 = R.add(v_i__821738, R.number("1"));
            }
        }
        return "";
    }
    public static object F_c1_inquiry_route(object v_actor,object v_target) {
        object v_position__822000 = F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_actor, "location_ref"));
        if (R.Bool((R.Bool(R.not(F_dv_uncontested(v_position__822000))) ? (object)(true) : (object)(R.eq(F_dv_get(v_position__822000, "value"), v_target)))))
        {
            return false;
        }
        object v_queue__822148 = new List<object> { new List<object> { F_dv_pair("room", F_dv_get(v_position__822000, "value")), F_dv_pair("first", ""), F_dv_pair("basis", new List<object> {  }) } };
        object v_seen__822218 = new List<object> { F_dv_get(v_position__822000, "value") };
        object v_cursor__822251 = R.number("0");
        object v_edges__822271 = F_c1_edges();
        while (R.Bool(R.lt(v_cursor__822251, R.count(v_queue__822148))))
        {
            object v_node__822340 = R.at(v_queue__822148, v_cursor__822251);
            object v_i__822374 = R.number("0");
            while (R.Bool(R.lt(v_i__822374, R.count(v_edges__822271))))
            {
                object v_destination__822433 = "";
                if (R.Bool(R.eq(R.at(R.at(v_edges__822271, v_i__822374), R.number("0")), F_dv_get(v_node__822340, "room"))))
                {
                    v_destination__822433 = R.at(R.at(v_edges__822271, v_i__822374), R.number("1"));
                }
                if (R.Bool(R.eq(R.at(R.at(v_edges__822271, v_i__822374), R.number("1")), F_dv_get(v_node__822340, "room"))))
                {
                    v_destination__822433 = R.at(R.at(v_edges__822271, v_i__822374), R.number("0"));
                }
                if (R.Bool((R.Bool(R.not(R.eq(v_destination__822433, ""))) ? (object)(R.not(F_dv_has(v_seen__822218, v_destination__822433))) : (object)(false))))
                {
                    object v_method__822710 = F_c1_move_name(F_dv_get(v_actor, "id"), F_dv_get(v_node__822340, "room"), v_destination__822433);
                    object v_adoption__822787 = F_dv_find(F_dv_get(F_dv_get(v_actor, "mind"), "adoptions"), v_method__822710, false);
                    if (R.Bool((R.Bool((R.Bool(R.not(R.eq(v_adoption__822787, false))) ? (object)(F_dv_get(v_adoption__822787, "active")) : (object)(false))) ? (object)(R.eq(F_dv_get(v_adoption__822787, "mode"), "predict")) : (object)(false))))
                    {
                        object v_first__822958 = F_dv_get(v_node__822340, "first");
                        if (R.Bool(R.eq(v_first__822958, "")))
                        {
                            v_first__822958 = v_destination__822433;
                        }
                        object v_basis__823084 = F_dv_append(F_dv_get(v_node__822340, "basis"), new List<object> { F_dv_pair("method", v_method__822710), F_dv_pair("basis", F_dv_get(v_adoption__822787, "basis")) });
                        if (R.Bool(R.eq(v_destination__822433, v_target)))
                        {
                            return new List<object> { F_dv_pair("next", v_first__822958), F_dv_pair("target", v_target), F_dv_pair("basis", v_basis__823084) };
                        }
                        _ = R.push(v_queue__822148, new List<object> { F_dv_pair("room", v_destination__822433), F_dv_pair("first", v_first__822958), F_dv_pair("basis", v_basis__823084) });
                        _ = R.push(v_seen__822218, v_destination__822433);
                    }
                }
                v_i__822374 = R.add(v_i__822374, R.number("1"));
            }
            v_cursor__822251 = R.add(v_cursor__822251, R.number("1"));
        }
        return false;
    }
    public static object F_c1_inquiry_navigation(object v_actor) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_inquiry_navigation.553", v_actor)))
        {
            return v_actor;
        }
        object v_actions__823664 = F_dv_get(v_actor, "actions");
        object v_action_index__823697 = R.number("0");
        while (R.Bool(R.lt(v_action_index__823697, R.count(v_actions__823664))))
        {
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_inquiry_navigation.554", v_actions__823664, v_action_index__823697)))
            {
                v_actions__823664 = F_dvs_replace(v_actions__823664, v_action_index__823697, F_dv_without(F_dv_without(R.at(v_actions__823664, v_action_index__823697), "personal_choice"), Authored("m40.c1_inquiry_navigation.literal")));
            }
            v_action_index__823697 = R.add(v_action_index__823697, R.number("1"));
        }
        v_actor = F_dv_with(v_actor, "actions", v_actions__823664);
        object v_goals__824180 = new List<object> {  };
        object v_old__824200 = F_dv_get(F_dv_get(v_actor, "policy"), "goals");
        object v_i__824234 = R.number("0");
        while (R.Bool(R.lt(v_i__824234, R.count(v_old__824200))))
        {
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_inquiry_navigation.555", v_old__824200, v_i__824234)))
            {
                _ = R.push(v_goals__824180, R.at(v_old__824200, v_i__824234));
            }
            v_i__824234 = R.add(v_i__824234, R.number("1"));
        }
        if (R.Bool(F_dvp_owned_task_ready(v_actor, v_goals__824180)))
        {
            return F_dv_with(v_actor, "policy", F_dv_with(F_dv_get(v_actor, "policy"), "goals", v_goals__824180));
        }
        object v_selected__824736 = false;
        object v_position__824762 = F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_actor, "location_ref"));
        v_i__824234 = R.number("0");
        while (R.Bool(R.lt(v_i__824234, R.count(v_goals__824180))))
        {
            object v_goal__824869 = R.at(v_goals__824180, v_i__824234);
            if (R.Bool((R.Bool((R.Bool((R.Bool(F_dv_has_key(v_goal__824869, "ref")) ? (object)(F_dvp_topic(v_actor, v_goal__824869)) : (object)(false))) ? (object)(R.lt(R.number("0"), R.count(F_dv_find(v_goal__824869, "basis", new List<object> {  })))) : (object)(false))) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(v_goal__824869, "available_when", F_dv_true())), "true")) : (object)(false))))
            {
                object v_cell__825090 = F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_goal__824869, "ref"));
                object v_pending__825144 = R.not(F_dv_uncontested(v_cell__825090));
                if (R.Bool(R.eq(F_dv_find(v_goal__824869, "kind", ""), "evidence")))
                {
                    v_pending__825144 = R.not(F_dvp_source_supported_since(v_cell__825090, F_dv_find(v_goal__824869, "source", Authored("m40.c1_sound_read_result.literal_0005")), F_dv_find(v_goal__824869, Authored("m40.c1_inquiry_navigation.literal_0003"), R.number("0"))));
                }
                if (R.Bool(v_pending__825144))
                {
                    object v_j__825546 = R.number("0");
                    while (R.Bool(R.lt(v_j__825546, R.count(F_dv_get(v_actor, "actions")))))
                    {
                        object v_action__825629 = R.at(F_dv_get(v_actor, "actions"), v_j__825546);
                        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_inquiry_navigation.556", v_action__825629, v_goal__824869, v_actor)))
                        {
                            object v_room__825911 = F_c1_inquiry_room(v_actor, F_dv_get(v_action__825629, "precondition"));
                            if (R.Bool(R.not(R.eq(v_room__825911, ""))))
                            {
                                if (R.Bool((R.Bool(F_dv_uncontested(v_position__824762)) ? (object)(R.eq(v_room__825911, F_dv_get(v_position__824762, "value"))) : (object)(false))))
                                {
                                    return F_dv_with(v_actor, "policy", F_dv_with(F_dv_get(v_actor, "policy"), "goals", v_goals__824180));
                                }
                                if (R.Bool(R.eq(v_selected__824736, false)))
                                {
                                    object v_route__826279 = F_c1_inquiry_route(v_actor, v_room__825911);
                                    if (R.Bool(R.not(R.eq(v_route__826279, false))))
                                    {
                                        v_selected__824736 = DvValueTemplate.Instantiate("template.inline.c1_inquiry_navigation.557", v_actor, v_route__826279, v_goal__824869, v_action__825629);
                                    }
                                }
                            }
                        }
                        v_j__825546 = R.add(v_j__825546, R.number("1"));
                    }
                }
            }
            v_i__824234 = R.add(v_i__824234, R.number("1"));
        }
        if (R.Bool(R.not(R.eq(v_selected__824736, false))))
        {
            _ = R.push(v_goals__824180, v_selected__824736);
            object v_method__827050 = F_c1_move_name(F_dv_get(v_actor, "id"), F_dv_get(v_position__824762, "value"), F_dv_get(F_dv_get(v_selected__824736, "route"), "next"));
            v_action_index__823697 = R.number("0");
            while (R.Bool(R.lt(v_action_index__823697, R.count(v_actions__823664))))
            {
                if (R.Bool(R.eq(F_dv_get(R.at(v_actions__823664, v_action_index__823697), "id"), v_method__827050)))
                {
                    v_actions__823664 = F_dvs_replace(v_actions__823664, v_action_index__823697, DvValueTemplate.Instantiate("template.inline.c1_inquiry_navigation.558", v_actions__823664, v_action_index__823697));
                }
                v_action_index__823697 = R.add(v_action_index__823697, R.number("1"));
            }
            v_actor = F_dv_with(v_actor, "actions", v_actions__823664);
        }
        return F_dv_with(v_actor, "policy", F_dv_with(F_dv_get(v_actor, "policy"), "goals", v_goals__824180));
    }
    public static object F_c1n_refresh_actor_goals(object v_actor) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_refresh_actor_goals.559", v_actor)))
        {
            object v_combined__827867 = F_dv_clone(F_dv_find(F_dv_get(v_actor, "policy"), Authored("m40.c1n_refresh_actor_goals.literal_0002"), F_dv_get(F_dv_get(v_actor, "policy"), "goals")));
            object v_goals__828024 = F_c1n_goals(v_actor);
            object v_i__828062 = R.number("0");
            while (R.Bool(R.lt(v_i__828062, R.count(v_goals__828024))))
            {
                _ = R.push(v_combined__827867, R.at(v_goals__828024, v_i__828062));
                v_i__828062 = R.add(v_i__828062, R.number("1"));
            }
            v_actor = F_dv_with(v_actor, "policy", F_dv_with_many(F_dv_get(v_actor, "policy"), new List<object> { F_dv_pair("goals", v_combined__827867), F_dv_pair("first_night_goals", v_goals__828024) }));
        }
        return F_campaign_request_withdrawals(F_c1_request_navigation(F_c1n_known_future_wait_actor(F_c1_inquiry_navigation(v_actor))));
    }
    public static object F_c1f_chapter_ready(object v_world) {
        return DvValueTemplate.Instantiate("template.c1f_chapter_ready", v_world);
    }
    public static object F_c1n_install_scene(object v_scene) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_install_scene.560", v_scene)))
        {
            return v_scene;
        }
        object v_rows__828686 = F_c1n_rows();
        object v_fields__828713 = DvValueTemplate.Instantiate("template.inline.c1n_install_scene.561");
        object v_i__829776 = R.number("0");
        while (R.Bool(R.lt(v_i__829776, R.count(v_rows__828686))))
        {
            v_fields__828713 = DvValueTemplate.Instantiate("template.inline.c1n_install_scene.562", v_fields__828713, v_rows__828686, v_i__829776);
            v_i__829776 = R.add(v_i__829776, R.number("1"));
        }
        object v_objects__829973 = DvValueTemplate.Instantiate("template.inline.c1n_install_scene.563", v_fields__828713);
        object v_physical__830784 = F_dv_get(v_scene, "physical");
        object v_bindings__830819 = F_dv_clone(F_dv_get(v_scene, "bindings"));
        v_i__829776 = R.number("0");
        while (R.Bool(R.lt(v_i__829776, R.count(v_objects__829973))))
        {
            v_physical__830784 = F_campaign_add_new(v_physical__830784, R.at(v_objects__829973, v_i__829776));
            v_i__829776 = R.add(v_i__829776, R.number("1"));
        }
        v_i__829776 = R.number("0");
        while (R.Bool(R.lt(v_i__829776, R.count(v_rows__828686))))
        {
            v_physical__830784 = F_dv_adopt(v_physical__830784, F_campaign_method(F_dv_get(R.at(v_rows__828686, v_i__829776), "id"), F_c1n_law(R.at(v_rows__828686, v_i__829776))), "enforce", R.number("1"), new List<object> { F_c1n_source(F_dv_get(R.at(v_rows__828686, v_i__829776), "id")) });
            v_i__829776 = R.add(v_i__829776, R.number("1"));
        }
        object v_costs__831174 = F_dv_clone(F_dv_get(v_scene, "cost_model"));
        object v_durations__831218 = F_c1n_wait_durations();
        object v_duration_index__831260 = R.number("0");
        while (R.Bool(R.lt(v_duration_index__831260, R.count(v_durations__831218))))
        {
            object v_minutes__831341 = R.at(v_durations__831218, v_duration_index__831260);
            v_costs__831174 = DvValueTemplate.Instantiate("template.inline.c1n_install_scene.564", v_costs__831174, v_minutes__831341);
            v_costs__831174 = DvValueTemplate.Instantiate("template.inline.c1n_install_scene.565", v_costs__831174, v_minutes__831341);
            v_duration_index__831260 = R.add(v_duration_index__831260, R.number("1"));
        }
        object v_manual_lengths__831928 = AuthoredCopy("m40.c1n_install_scene.manual_lengths");
        v_duration_index__831260 = R.number("0");
        while (R.Bool(R.lt(v_duration_index__831260, R.count(v_manual_lengths__831928))))
        {
            object v_minutes__832127 = R.at(v_manual_lengths__831928, v_duration_index__831260);
            v_costs__831174 = DvValueTemplate.Instantiate("template.inline.c1n_install_scene.566", v_costs__831174, v_minutes__832127);
            v_duration_index__831260 = R.add(v_duration_index__831260, R.number("1"));
        }
        object v_actors__832497 = new List<object> {  };
        v_i__829776 = R.number("0");
        while (R.Bool(R.lt(v_i__829776, R.count(F_dv_get(v_scene, "actors")))))
        {
            object v_actor__832572 = R.at(F_dv_get(v_scene, "actors"), v_i__829776);
            object v_mind__832609 = F_dv_get(v_actor__832572, "mind");
            object v_j__832640 = R.number("0");
            while (R.Bool(R.lt(v_j__832640, R.count(v_objects__829973))))
            {
                v_mind__832609 = F_campaign_add_new(v_mind__832609, F_campaign_local_object(F_dv_get(R.at(v_objects__829973, v_j__832640), "id")));
                if (R.Bool(R.eq(F_dv_local(v_bindings__830819, F_dv_get(R.at(v_objects__829973, v_j__832640), "id"), F_dv_get(v_actor__832572, "id")), "")))
                {
                    _ = R.push(v_bindings__830819, F_dv_binding(F_dv_get(R.at(v_objects__829973, v_j__832640), "id"), F_dv_get(R.at(v_objects__829973, v_j__832640), "id"), F_dv_get(v_actor__832572, "id")));
                }
                v_j__832640 = R.add(v_j__832640, R.number("1"));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_install_scene.567", v_actor__832572)))
            {
                v_mind__832609 = F_dv_store(v_mind__832609, F_c1n_judgment_ref(), DvValueTemplate.Instantiate("template.inline.c1n_install_scene.568"));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_install_scene.569", v_actor__832572)))
            {
                v_mind__832609 = F_dv_store(v_mind__832609, F_campaign_first_night_ref(Authored("m40.c1n_rows_build.literal_0019")), DvValueTemplate.Instantiate("template.inline.c1n_install_scene.570"));
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_install_scene.571", v_actor__832572)))
            {
                v_mind__832609 = F_dv_store(v_mind__832609, F_c1f_ref(Authored("m40.c1n_install_scene.literal_0011")), DvValueTemplate.Instantiate("template.inline.c1n_install_scene.572"));
                v_mind__832609 = F_dv_store(v_mind__832609, F_c1f_ref(Authored("m40.c1n_install_scene.literal_0013")), DvValueTemplate.Instantiate("template.inline.c1n_install_scene.573"));
            }
            object v_call_object__834375 = R.cat(Authored("m40.c1n_install_scene.literal_0015"), F_dv_get(v_actor__832572, "id"));
            v_physical__830784 = F_campaign_add_new(v_physical__830784, F_campaign_state_object(v_call_object__834375, new List<object> { F_dv_pair("heard", F_dv_field(false)), F_dv_pair("night_observed", F_dv_field(false)) }));
            v_mind__832609 = F_campaign_add_new(v_mind__832609, F_campaign_local_object(v_call_object__834375));
            v_mind__832609 = F_dv_store(v_mind__832609, F_c1n_call_ref(F_dv_get(v_actor__832572, "id")), DvValueTemplate.Instantiate("template.inline.c1n_install_scene.574"));
            if (R.Bool(R.eq(F_dv_local(v_bindings__830819, v_call_object__834375, F_dv_get(v_actor__832572, "id")), "")))
            {
                _ = R.push(v_bindings__830819, F_dv_binding(v_call_object__834375, v_call_object__834375, F_dv_get(v_actor__832572, "id")));
            }
            object v_notice_id__835034 = R.cat(F_dv_get(v_actor__832572, "id"), Authored("m40.c1n_inspections.literal_0026"));
            v_physical__830784 = F_dv_adopt(v_physical__830784, F_campaign_method(v_notice_id__835034, F_c1n_night_seen_law(F_dv_get(v_actor__832572, "id"))), "enforce", R.number("1"), DvValueTemplate.Instantiate("template.inline.c1n_install_scene.575"));
            v_mind__832609 = F_dv_adopt(v_mind__832609, F_campaign_method(v_notice_id__835034, F_c1n_night_seen_law(F_dv_get(v_actor__832572, "id"))), "predict", R.number("1"), DvValueTemplate.Instantiate("template.inline.c1n_install_scene.576"));
            object v_call_id__835537 = R.cat(F_dv_get(v_actor__832572, "id"), Authored("m40.c1n_inspections.literal_0030"));
            v_physical__830784 = F_dv_adopt(v_physical__830784, F_campaign_method(v_call_id__835537, F_c1n_call_law(F_dv_get(v_actor__832572, "id"))), "enforce", R.number("1"), DvValueTemplate.Instantiate("template.inline.c1n_install_scene.577"));
            v_mind__832609 = F_dv_adopt(v_mind__832609, F_campaign_method(v_call_id__835537, F_c1n_call_law(F_dv_get(v_actor__832572, "id"))), "predict", R.number("1"), DvValueTemplate.Instantiate("template.inline.c1n_install_scene.578"));
            object v_wait_id__836022 = R.cat(Authored("m40.c1n_install_scene.literal_0017"), F_dv_get(v_actor__832572, "id"));
            v_physical__830784 = F_campaign_add_new(v_physical__830784, F_campaign_state_object(v_wait_id__836022, new List<object> { F_dv_pair("elapsed", F_dv_field(R.number("0"))) }));
            v_mind__832609 = F_campaign_add_new(v_mind__832609, F_campaign_local_object(v_wait_id__836022));
            v_mind__832609 = F_dv_store(v_mind__832609, F_c1n_wait_ref(F_dv_get(v_actor__832572, "id")), DvValueTemplate.Instantiate("template.inline.c1n_install_scene.579"));
            if (R.Bool(R.eq(F_dv_local(v_bindings__830819, v_wait_id__836022, F_dv_get(v_actor__832572, "id")), "")))
            {
                _ = R.push(v_bindings__830819, F_dv_binding(v_wait_id__836022, v_wait_id__836022, F_dv_get(v_actor__832572, "id")));
            }
            v_duration_index__831260 = R.number("0");
            while (R.Bool(R.lt(v_duration_index__831260, R.count(v_durations__831218))))
            {
                object v_minutes__836701 = R.at(v_durations__831218, v_duration_index__831260);
                object v_wait_method__836754 = F_c1n_wait_id(F_dv_get(v_actor__832572, "id"), v_minutes__836701);
                v_physical__830784 = F_dv_adopt(v_physical__830784, F_campaign_method(v_wait_method__836754, F_c1n_wait_law(F_dv_get(v_actor__832572, "id"), v_minutes__836701)), "enforce", R.number("1"), DvValueTemplate.Instantiate("template.inline.c1n_install_scene.580"));
                v_mind__832609 = F_dv_adopt(v_mind__832609, F_campaign_method(v_wait_method__836754, F_c1n_wait_law(F_dv_get(v_actor__832572, "id"), v_minutes__836701)), "predict", R.number("1"), DvValueTemplate.Instantiate("template.inline.c1n_install_scene.581"));
                v_duration_index__831260 = R.add(v_duration_index__831260, R.number("1"));
            }
            object v_actions__837271 = F_c1f_join(F_dv_get(v_actor__832572, "actions"), F_c1n_manual_wait_actions(F_dv_get(v_actor__832572, "id")));
            object v_hearing__837353 = F_dv_clone(F_dv_find(v_actor__832572, Authored("m40.c1_sound_install_scene.literal_0009"), new List<object> {  }));
            object v_people__837485 = F_campaign_people();
            v_j__832640 = R.number("0");
            while (R.Bool(R.lt(v_j__832640, R.count(v_people__837485))))
            {
                if (R.Bool(R.not(R.eq(R.at(v_people__837485, v_j__832640), F_dv_get(v_actor__832572, "id")))))
                {
                    v_mind__832609 = F_campaign_add_new(v_mind__832609, F_campaign_local_object(R.at(v_people__837485, v_j__832640)));
                    if (R.Bool(R.eq(F_dv_local(v_bindings__830819, R.at(v_people__837485, v_j__832640), F_dv_get(v_actor__832572, "id")), "")))
                    {
                        _ = R.push(v_bindings__830819, F_dv_binding(R.at(v_people__837485, v_j__832640), R.at(v_people__837485, v_j__832640), F_dv_get(v_actor__832572, "id")));
                    }
                    _ = R.push(v_hearing__837353, DvValueTemplate.Instantiate("template.inline.c1n_install_scene.582", v_people__837485, v_j__832640, v_actor__832572));
                    _ = R.push(v_hearing__837353, DvValueTemplate.Instantiate("template.inline.c1n_install_scene.583", v_people__837485, v_j__832640, v_actor__832572));
                }
                v_j__832640 = R.add(v_j__832640, R.number("1"));
            }
            v_j__832640 = R.number("0");
            while (R.Bool(R.lt(v_j__832640, R.count(v_rows__828686))))
            {
                object v_row__839082 = R.at(v_rows__828686, v_j__832640);
                if (R.Bool(R.eq(F_dv_get(v_row__839082, "actor"), F_dv_get(v_actor__832572, "id"))))
                {
                    v_mind__832609 = F_dv_store(v_mind__832609, F_c1n_mark(F_dv_get(v_row__839082, "id")), F_dv_field(false));
                    v_mind__832609 = F_dv_adopt(v_mind__832609, F_campaign_method(F_dv_get(v_row__839082, "id"), F_c1n_law(v_row__839082)), "predict", R.number("1"), new List<object> { F_c1n_source(F_dv_get(v_row__839082, "id")) });
                    _ = R.push(v_actions__837271, F_c1n_action(v_row__839082));
                }
                v_j__832640 = R.add(v_j__832640, R.number("1"));
            }
            object v_inspect__839410 = F_c1f_join(F_c1f_join(F_c1n_inspections(F_dv_get(v_actor__832572, "id")), F_c1f_inspections(F_dv_get(v_actor__832572, "id"))), F_c1f_identity_actions(F_dv_get(v_actor__832572, "id")));
            v_j__832640 = R.number("0");
            while (R.Bool(R.lt(v_j__832640, R.count(v_inspect__839410))))
            {
                _ = R.push(v_actions__837271, R.at(v_inspect__839410, v_j__832640));
                v_j__832640 = R.add(v_j__832640, R.number("1"));
            }
            v_actor__832572 = DvValueTemplate.Instantiate("template.inline.c1n_install_scene.584", v_actor__832572, v_mind__832609, v_actions__837271, v_hearing__837353);
            _ = R.push(v_actors__832497, F_c1n_refresh_actor_goals(v_actor__832572));
            v_i__829776 = R.add(v_i__829776, R.number("1"));
        }
        return F_dv_with_many(v_scene, new List<object> { F_dv_pair("physical", v_physical__830784), F_dv_pair("actors", v_actors__832497), F_dv_pair("bindings", v_bindings__830819), F_dv_pair("cost_model", v_costs__831174), F_dv_pair("first_night_installed", true) });
    }
    public static object F_c1n_wait_durations() {
        return DvValueTemplate.Instantiate("template.c1n_wait_durations");
    }
    public static object F_c1n_wait_kind(object v_minutes) {
        return DvValueTemplate.Instantiate("template.c1n_wait_kind", v_minutes);
    }
    public static object F_c1n_wait_id(object v_actor,object v_minutes) {
        return DvValueTemplate.Instantiate("template.c1n_wait_id", v_actor, v_minutes);
    }
    public static object F_c1n_wait_ref(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1n_wait_ref", v_actor);
    }
    public static object F_c1n_wait_law(object v_行动者,object v_minutes) {
        return DvValueTemplate.Instantiate("template.c1n_wait_law", v_行动者, v_minutes);
    }
    public static object F_c1n_known_wait_actor(object v_actor,object v_target,object v_basis) {
        v_actor = F_c1n_wait_profile(v_actor);
        object v_profiled_actor = v_actor;
        bool readOnlyQueries = R.Bool(F_dv_find(F_dv_get(v_actor, "policy"), "opt_readview", false));
        if (readOnlyQueries) v_actor = R.snapshot(v_actor);
        if (R.Bool((R.Bool((R.Bool(R.not(R.lt(F_dv_get(F_dv_get(v_actor, "mind"), "time"), v_target))) ? (object)(F_dv_has_key(F_dv_get(v_actor, "policy"), "known_wait_active")) : (object)(false))) ? (object)(R.not(F_dv_get(F_dv_get(v_actor, "policy"), "known_wait_active"))) : (object)(false))))
        {
            return v_profiled_actor;
        }
        object v_active_wait__841662 = false;
        object v_actions__841691 = new List<object> {  };
        object v_i__841713 = R.number("0");
        while (R.Bool(R.lt(v_i__841713, R.count(F_dv_get(v_actor, "actions")))))
        {
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_known_wait_actor.585", v_actor, v_i__841713)))
            {
                _ = R.push(v_actions__841691, R.at(F_dv_get(v_actor, "actions"), v_i__841713));
            }
            v_i__841713 = R.add(v_i__841713, R.number("1"));
        }
        object v_goals__841958 = new List<object> {  };
        object v_existing__841978 = F_dv_find(F_dv_get(v_actor, "policy"), "goals", new List<object> {  });
        v_i__841713 = R.number("0");
        while (R.Bool(R.lt(v_i__841713, R.count(v_existing__841978))))
        {
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_known_wait_actor.586", v_existing__841978, v_i__841713)))
            {
                _ = R.push(v_goals__841958, R.at(v_existing__841978, v_i__841713));
            }
            v_i__841713 = R.add(v_i__841713, R.number("1"));
        }
        object v_elapsed__842257 = F_dv_read(F_dv_get(v_actor, "mind"), F_c1n_wait_ref(F_dv_get(v_actor, "id")));
        object v_progress_weight__842320 = F_dv_find(F_dv_get(v_actor, "policy"), Authored("m40.c1n_known_wait_actor.literal_0002"), R.number("1"));
        object v_time_cost__842450 = F_dv_find(F_dv_get(v_actor, "policy"), Authored("m40.c1n_known_wait_actor.literal_0003"), Authored("m40.c1n_known_wait_actor.literal_0004"));
        object v_current_night_action__842649 = false;
        object v_rows__842687 = F_c1n_rows();
        object v_relevant__842714 = F_dvp_goal_refs(v_actor);
        object v_query_actions = v_actions__841691;
        if (readOnlyQueries)
        {
            v_rows__842687 = R.snapshot(v_rows__842687);
            v_relevant__842714 = R.snapshot(v_relevant__842714);
            v_query_actions = R.snapshot(v_query_actions);
        }
        v_i__841713 = R.number("0");
        while (R.Bool(R.lt(v_i__841713, R.count(v_rows__842687))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(v_rows__842687, v_i__841713), "actor"), F_dv_get(v_actor, "id"))))
            {
                object v_action_index__842844 = R.number("0");
                while (R.Bool((R.Bool(R.lt(v_action_index__842844, R.count(v_actions__841691))) ? (object)(R.not(v_current_night_action__842649)) : (object)(false))))
                {
                    object v_action__842964 = R.at(v_query_actions, v_action_index__842844);
                    if (R.Bool((R.Bool(R.eq(F_dv_get(v_action__842964, "id"), F_dv_get(R.at(v_rows__842687, v_i__841713), "id"))) ? (object)(F_dvp_eligible(v_actor, v_action__842964, F_dv_get(v_actor, "mind"), v_relevant__842714)) : (object)(false))))
                    {
                        v_current_night_action__842649 = true;
                    }
                    v_action_index__842844 = R.add(v_action_index__842844, R.number("1"));
                }
            }
            v_i__841713 = R.add(v_i__841713, R.number("1"));
        }
        object v_explicit_wait_choice__843220 = false;
        object v_declared_wait_choices__843258 = F_dv_find(F_dv_get(v_actor, "policy"), Authored("m40.c1n_known_wait_actor.literal_0005"), new List<object> {  });
        object v_declared_index__843395 = R.number("0");
        while (R.Bool(R.lt(v_declared_index__843395, R.count(v_declared_wait_choices__843258))))
        {
            object v_declaration__843488 = R.at(v_declared_wait_choices__843258, v_declared_index__843395);
            if (R.Bool((R.Bool((R.Bool(F_dv_has_key(v_declaration__843488, "target_time")) ? (object)(F_dv_has_key(v_declaration__843488, "available_when")) : (object)(false))) ? (object)(F_dv_has_key(v_declaration__843488, "personal_choice")) : (object)(false))))
            {
                if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_declaration__843488, "target_time"), v_target)) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_declaration__843488, "available_when")), "true")) : (object)(false))) ? (object)(R.lt(R.number("0"), R.count(F_dv_find(F_dv_get(v_declaration__843488, "personal_choice"), "basis", new List<object> {  })))) : (object)(false))))
                {
                    v_explicit_wait_choice__843220 = true;
                }
            }
            v_declared_index__843395 = R.add(v_declared_index__843395, R.number("1"));
        }
        if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(F_dv_get(v_elapsed__842257, "known")) ? (object)(R.lt(F_dv_get(F_dv_get(v_actor, "mind"), "time"), v_target)) : (object)(false))) ? (object)(R.lt(R.number("0"), R.count(v_basis))) : (object)(false))) ? (object)(R.lt(R.number("0"), v_progress_weight__842320)) : (object)(false))) ? (object)((R.Bool(R.not(v_current_night_action__842649)) ? (object)(true) : (object)(v_explicit_wait_choice__843220))) : (object)(false))))
        {
            object v_durations__844125 = F_c1n_wait_durations();
            object v_minutes__844171 = R.number("0");
            v_i__841713 = R.number("0");
            object v_wait_chunk_limit__844211 = F_dv_find(F_dv_get(v_actor, "policy"), Authored("m40.c1n_known_wait_actor.literal_0006"), Authored("m40.c1n_known_wait_actor.literal_0007"));
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_known_wait_actor.587", v_actor)))
            {
                object v_daytime_wait_limit__844541 = F_dv_find(F_dv_get(v_actor, "policy"), Authored("m40.c1n_known_wait_actor.literal_0009"), v_wait_chunk_limit__844211);
                object v_day_minute__844697 = F_dv_get(F_dv_get(v_actor, "mind"), "time");
                while (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_known_wait_actor.588", v_day_minute__844697)))
                {
                    v_day_minute__844697 = R.sub(v_day_minute__844697, Authored("m40.c1n_rows_build.literal_0057"));
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_known_wait_actor.589", v_day_minute__844697)))
                {
                    v_wait_chunk_limit__844211 = F_dv_min(v_wait_chunk_limit__844211, v_daytime_wait_limit__844541);
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_known_wait_actor.590", v_actor, v_day_minute__844697)))
                {
                    v_wait_chunk_limit__844211 = F_dv_min(v_wait_chunk_limit__844211, R.sub(Authored("m40.c1_physical_world.literal_0002"), v_day_minute__844697));
                }
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_known_wait_actor.591", v_actor)))
            {
                v_wait_chunk_limit__844211 = Authored("m40.c1n_known_wait_actor.literal_0011");
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_known_wait_actor.592", v_actor)))
                {
                    v_wait_chunk_limit__844211 = Authored("m40.c1_public_goals.literal_0002");
                }
            }
            while (R.Bool(R.lt(v_i__841713, R.count(v_durations__844125))))
            {
                if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(v_minutes__844171, R.number("0"))) ? (object)(R.not(R.lt(R.at(v_durations__844125, v_i__841713), R.number("1")))) : (object)(false))) ? (object)(R.not(R.lt(v_wait_chunk_limit__844211, R.at(v_durations__844125, v_i__841713)))) : (object)(false))) ? (object)(R.not(R.lt(v_target, R.add(F_dv_get(F_dv_get(v_actor, "mind"), "time"), R.at(v_durations__844125, v_i__841713))))) : (object)(false))))
                {
                    v_minutes__844171 = R.at(v_durations__844125, v_i__841713);
                }
                v_i__841713 = R.add(v_i__841713, R.number("1"));
            }
            if (R.Bool(R.lt(R.number("0"), v_minutes__844171)))
            {
                object v_id__846808 = F_c1n_wait_id(F_dv_get(v_actor, "id"), v_minutes__844171);
                object v_known__846861 = F_dv_true();
                object v_result__846896 = F_dv_at_least(F_c1n_wait_ref(F_dv_get(v_actor, "id")), R.add(F_dv_get(v_elapsed__842257, "value"), v_minutes__844171));
                object v_text__846983 = F_dvs_surface_parts(DvValueTemplate.Instantiate("template.inline.c1n_known_wait_actor.593", v_minutes__844171));
                object v_action__847201 = DvValueTemplate.Instantiate("template.inline.c1n_known_wait_actor.594", v_id__846808, v_minutes__844171, v_known__846861, v_actor, v_text__846983);
                object v_wait_choices__847705 = F_dv_find(F_dv_get(v_actor, "policy"), Authored("m40.c1n_known_wait_actor.literal_0005"), new List<object> {  });
                object v_choice_index__847841 = R.number("0");
                while (R.Bool(R.lt(v_choice_index__847841, R.count(v_wait_choices__847705))))
                {
                    object v_declaration__847937 = R.at(v_wait_choices__847705, v_choice_index__847841);
                    if (R.Bool((R.Bool((R.Bool(F_dv_has_key(v_declaration__847937, "target_time")) ? (object)(F_dv_has_key(v_declaration__847937, "available_when")) : (object)(false))) ? (object)(F_dv_has_key(v_declaration__847937, "personal_choice")) : (object)(false))))
                    {
                        if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_declaration__847937, "target_time"), v_target)) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_declaration__847937, "available_when")), "true")) : (object)(false))) ? (object)(R.lt(R.number("0"), R.count(F_dv_find(F_dv_get(v_declaration__847937, "personal_choice"), "basis", new List<object> {  })))) : (object)(false))))
                        {
                            _ = F_dv_require(R.not(F_dv_has_key(v_action__847201, "personal_choice")), Authored("m40.c1n_known_wait_actor.literal_0016"));
                            v_action__847201 = F_dv_with(v_action__847201, "personal_choice", F_dv_get(v_declaration__847937, "personal_choice"));
                        }
                    }
                    v_choice_index__847841 = R.add(v_choice_index__847841, R.number("1"));
                }
                v_active_wait__841662 = true;
                _ = R.push(v_actions__841691, F_c1_public_method(F_dv_with_many(v_action__847201, new List<object> { F_dv_pair("predict_law", v_id__846808), F_dv_pair("repeatable", true), F_dv_pair("known_opportunity_wait", true), F_dv_pair("wait_until", v_target), F_dv_pair("wait_basis", v_basis), F_dv_pair("activity", "wait"), F_dv_pair("interruptible", true) })));
                object v_net_utility__849147 = Authored("m40.c1n_known_wait_actor.literal_0004");
                object v_wait_value__849255 = R.div(R.add(R.mul(v_minutes__844171, v_time_cost__842450), v_net_utility__849147), v_progress_weight__842320);
                _ = R.push(v_goals__841958, F_dv_with_many(F_campaign_goal(v_result__846896, v_known__846861, v_basis), DvValueTemplate.Instantiate("template.inline.c1n_known_wait_actor.595", v_wait_value__849255, v_minutes__844171, v_time_cost__842450, v_net_utility__849147, v_progress_weight__842320)));
            }
        }
        return F_dv_with_many(v_actor, new List<object> { F_dv_pair("actions", v_actions__841691), F_dv_pair("policy", F_dv_with_many(F_dv_get(v_actor, "policy"), new List<object> { F_dv_pair("goals", v_goals__841958), F_dv_pair("known_wait_active", v_active_wait__841662) })) });
    }
    public static object F_c1n_wait_profile(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1n_wait_profile", v_actor);
    }
    public static object F_c1n_known_future_wait_actor(object v_actor) {
        object v_result_actor = v_actor;
        if (R.Bool(F_dv_find(F_dv_get(v_actor, "policy"), "opt_readview", false)))
            v_actor = R.snapshot(v_actor);
        object v_target__850668 = R.sub(R.number("0"), R.number("1"));
        object v_basis__850689 = new List<object> {  };
        object v_goals__850709 = F_dv_find(F_dv_get(v_actor, "policy"), "goals", new List<object> {  });
        object v_i__850761 = R.number("0");
        while (R.Bool(R.lt(v_i__850761, R.count(v_goals__850709))))
        {
            object v_goal__850812 = R.at(v_goals__850709, v_i__850761);
            object v_own_basis__850841 = F_dv_find(v_goal__850812, "basis", new List<object> {  });
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1n_known_future_wait_actor.596", v_goal__850812, v_own_basis__850841)))
            {
                object v_indexed__851036 = v_goal__850812;
                if (R.Bool((R.Bool(R.not(F_dv_has_key(v_goal__850812, "future_clock_boundaries"))) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_find(v_goal__850812, "future_clock_condition", false), F_dv_find(v_goal__850812, "available_when", F_dv_true())))))))
                {
                    v_indexed__851036 = F_campaign_index_goal_window(v_goal__850812);
                }
                object v_boundaries__851300 = F_dv_get(v_indexed__851036, "future_clock_boundaries");
                object v_candidate__851362 = R.sub(R.number("0"), R.number("1"));
                object v_j__851394 = R.number("0");
                while (R.Bool(R.lt(v_j__851394, R.count(v_boundaries__851300))))
                {
                    if (R.Bool((R.Bool(R.lt(F_dv_get(F_dv_get(v_actor, "mind"), "time"), R.at(v_boundaries__851300, v_j__851394))) ? (object)((R.Bool(R.lt(v_candidate__851362, R.number("0"))) ? (object)(true) : (object)(R.lt(R.at(v_boundaries__851300, v_j__851394), v_candidate__851362)))) : (object)(false))))
                    {
                        v_candidate__851362 = R.at(v_boundaries__851300, v_j__851394);
                    }
                    v_j__851394 = R.add(v_j__851394, R.number("1"));
                }
                if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.lt(v_candidate__851362, R.number("0")))) ? (object)((R.Bool(R.lt(v_target__850668, R.number("0"))) ? (object)(true) : (object)(R.lt(v_candidate__851362, v_target__850668)))) : (object)(false))) ? (object)(F_campaign_future_goal_pending(v_actor, v_goal__850812)) : (object)(false))) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_indexed__851036, "future_nonclock")), "true")) : (object)(false))))
                {
                    v_target__850668 = v_candidate__851362;
                    v_basis__850689 = F_dv_append(v_own_basis__850841, DvValueTemplate.Instantiate("template.inline.c1n_known_future_wait_actor.597", v_indexed__851036));
                }
            }
            v_i__850761 = R.add(v_i__850761, R.number("1"));
        }
        if (R.Bool(R.not(R.lt(v_target__850668, R.number("0")))))
        {
            return F_c1n_known_wait_actor(v_result_actor, v_target__850668, v_basis__850689);
        }
        return F_c1n_known_wait_actor(v_result_actor, F_dv_get(F_dv_get(v_actor, "mind"), "time"), new List<object> {  });
    }
    public static object F_c1n_call_ref(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1n_call_ref", v_actor);
    }
    public static object F_c1n_call_law(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c1n_call_law", v_行动者);
    }
    public static object F_c1n_night_seen_ref(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1n_night_seen_ref", v_actor);
    }
    public static object F_c1n_night_seen_law(object v_行动者) {
        return DvValueTemplate.Instantiate("template.c1n_night_seen_law", v_行动者);
    }
    public static object F_Standard_Detective(object v_replay_depth,object v_resistance,object v_empirical_values,object v_mediation_rules,object v_basis) {
        return DvValueTemplate.Instantiate("template.Standard_Detective", v_replay_depth, v_resistance, v_empirical_values, v_mediation_rules, v_basis);
    }
    public static object F_Foolish_Detective(object v_replay_depth,object v_resistance,object v_values,object v_norm_rules,object v_class_bias,object v_basis) {
        return DvValueTemplate.Instantiate("template.Foolish_Detective", v_replay_depth, v_resistance, v_values, v_norm_rules, v_class_bias, v_basis);
    }
    public static object F_cognitive_sample(object v_key,object v_options,object v_basis) {
        return DvValueTemplate.Instantiate("template.cognitive_sample", v_key, v_options, v_basis);
    }
    public static object F_cognitive_initialization(object v_profile,object v_samples,object v_basis) {
        return DvValueTemplate.Instantiate("template.cognitive_initialization", v_profile, v_samples, v_basis);
    }
    public static object F_c1c_install_scene(object v_scene,object v_seed,object v_profile) {
        _ = F_dv_require((R.Bool((R.Bool(R.eq(v_profile, Authored("m40.Standard_Detective.literal_0002"))) ? (object)(true) : (object)(R.eq(v_profile, Authored("m40.Foolish_Detective.literal"))))) ? (object)(true) : (object)(R.eq(v_profile, Authored("m40.c1c_install_scene.literal")))), Authored("m40.c1c_install_scene.literal_0002"));
        object v_basis__855572 = AuthoredCopy("m40.c1c_install_scene.basis");
        object v_actors__855671 = new List<object> {  };
        object v_i__855692 = R.number("0");
        while (R.Bool(R.lt(v_i__855692, R.count(F_dv_get(v_scene, "actors")))))
        {
            object v_actor__855750 = R.at(F_dv_get(v_scene, "actors"), v_i__855692);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1c_install_scene.598", v_actor__855750)))
            {
                object v_low_trust__855886 = new List<object> {  };
                object v_middle_trust__855918 = new List<object> {  };
                object v_high_trust__855953 = new List<object> {  };
                object v_j__855986 = R.number("0");
                while (R.Bool(R.lt(v_j__855986, R.count(F_dv_get(v_scene, "actors")))))
                {
                    object v_peer__856060 = F_dv_get(R.at(F_dv_get(v_scene, "actors"), v_j__855986), "id");
                    object v_current__856107 = F_dv_find(F_dv_find(F_dv_get(v_actor__855750, "policy"), Authored("m40.c1c_install_scene.literal_0003"), new List<object> {  }), v_peer__856060, Authored("m40.c1_hypothesis_me.literal"));
                    v_low_trust__855886 = DvValueTemplate.Instantiate("template.inline.c1c_install_scene.599", v_low_trust__855886, v_peer__856060, v_current__856107);
                    v_middle_trust__855918 = F_dv_with(v_middle_trust__855918, v_peer__856060, v_current__856107);
                    v_high_trust__855953 = DvValueTemplate.Instantiate("template.inline.c1c_install_scene.600", v_high_trust__855953, v_peer__856060, v_current__856107);
                    v_j__855986 = R.add(v_j__855986, R.number("1"));
                }
                object v_old__856717 = F_dv_get(v_actor__855750, "policy");
                object v_drive__856753 = F_dv_find(v_old__856717, Authored("m40.c1c_install_scene.literal_0005"), R.number("1"));
                object v_caution__856869 = F_dv_find(v_old__856717, Authored("m40.c1c_install_scene.literal_0006"), Authored("m40.c1_hypothesis_me.literal"));
                object v_information__857053 = F_dv_find(v_old__856717, Authored("m40.c1_raw_person_policy.literal"), R.number("1"));
                object v_samples__857173 = DvValueTemplate.Instantiate("template.inline.c1c_install_scene.601", v_drive__856753, v_basis__855572, v_caution__856869, v_low_trust__855886, v_middle_trust__855918, v_high_trust__855953, v_information__857053);
                v_samples__857173 = F_dv_append(v_samples__857173, F_cognitive_sample(Authored("m40.c1c_install_scene.literal_0012"), AuthoredCopy("m40.c1c_install_scene.literal_0013"), v_basis__855572));
                v_samples__857173 = F_dv_append(v_samples__857173, F_cognitive_sample(Authored("m40.c1c_install_scene.literal_0014"), AuthoredCopy("m40.c1c_install_scene.literal_0015"), v_basis__855572));
                v_samples__857173 = F_dv_append(v_samples__857173, F_cognitive_sample(Authored("m40.c1c_install_scene.literal_0016"), AuthoredCopy("m40.c1c_install_scene.literal_0011"), v_basis__855572));
                object v_selected_profile__859186 = new List<object> {  };
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1c_install_scene.602", v_actor__855750)))
                {
                    object v_norms__859337 = F_dv_find(v_old__856717, Authored("m40.c1c_install_scene.literal_0017"), new List<object> {  });
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1c_install_scene.603", v_profile)))
                    {
                        v_selected_profile__859186 = F_Standard_Detective(Authored("m40.c1_other_people_build.literal_0100"), Authored("m40.c1c_install_scene.literal_0018"), AuthoredCopy("m40.c1c_install_scene.literal_0019"), v_norms__859337, v_basis__855572);
                        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1c_install_scene.604", v_profile)))
                        {
                            v_selected_profile__859186 = DvValueTemplate.Instantiate("template.inline.c1c_install_scene.605", v_selected_profile__859186);
                        }
                    }
                    else
                    {
                        v_selected_profile__859186 = F_Foolish_Detective(Authored("m40.c1_adopt_inferences.literal_0002"), Authored("m40.c1c_install_scene.literal_0021"), AuthoredCopy("m40.c1c_install_scene.literal_0022"), new List<object> {  }, F_dv_find(v_old__856717, Authored("m40.c1c_install_scene.literal_0023"), new List<object> {  }), v_basis__855572);
                    }
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1c_install_scene.606", v_actor__855750)))
                {
                    object v_threshold__860770 = F_dv_get(v_selected_profile__859186, "Implicature_Resistance");
                    v_samples__857173 = F_dv_append(v_samples__857173, F_cognitive_sample(Authored("m40.c1c_install_scene.literal_0010"), DvValueTemplate.Instantiate("template.inline.c1c_install_scene.607", v_threshold__860770), v_basis__855572));
                }
                v_actor__855750 = DvValueTemplate.Instantiate("template.inline.c1c_install_scene.608", v_actor__855750, v_selected_profile__859186, v_samples__857173, v_basis__855572);
            }
            _ = R.push(v_actors__855671, v_actor__855750);
            v_i__855692 = R.add(v_i__855692, R.number("1"));
        }
        return F_dv_with_many(v_scene, new List<object> { F_dv_pair("actors", v_actors__855671), F_dv_pair("opening_seed", v_seed), F_dv_pair("detective_profile", v_profile) });
    }
    public static object F_c1_unseen_action_keep(object v_actor_id,object v_group,object v_action_id) {
        return DvValueTemplate.Instantiate("template.c1_unseen_action_keep", v_actor_id, v_group, v_action_id);
    }
    public static object F_c1_unseen_dormitories_simplify(object v_scene) {
        object v_basis__865626 = DvValueTemplate.Instantiate("template.inline.c1_unseen_dormitories_simplify.609");
        object v_world__865749 = F_dv_get(v_scene, "physical");
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_exclusion_hypotheses.literal_0002")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_unseen_dormitories_simplify.literal_0002")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_unseen_dormitories_simplify.literal_0003")), F_dv_with(F_dv_field(R.number("1")), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_unseen_dormitories_simplify.literal_0004")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_unseen_dormitories_simplify.literal_0005")), F_dv_with(F_dv_field(R.number("1")), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_exclusion_hypotheses.literal_0015")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_activity_declarations.literal_0004")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_initial_inquiry.literal")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_moral_conflict.literal")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_submission.literal")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_exclusion_hypotheses.literal_0026")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_exclusion_hypotheses.literal_0025")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_c1_other_access_ref(Authored("m40.c1_other_people_build.literal_0004")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_c1dep_ref(Authored("m40.c1dep_can_cross.literal_0008")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_dv_ref(Authored("m40.c1n_rows_build.literal_0058"), "state", Authored("m40.c1n_rows_build.literal_0059")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_exclusion_hypotheses.literal_0002")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_unseen_dormitories_simplify.literal_0006")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_unseen_dormitories_simplify.literal_0007")), F_dv_with(F_dv_field(R.number("1")), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_unseen_dormitories_simplify.literal_0008")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_unseen_dormitories_simplify.literal_0009")), F_dv_with(F_dv_field(R.number("1")), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_initial_inquiry.literal_0002")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_environment_install_scene.literal_0017")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_exclusion_hypotheses.literal_0016")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_exclusion_hypotheses.literal_0015")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_submission.literal")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_c1_other_material_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_exclusion_hypotheses.literal_0017")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_c1_other_material_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_environment_install_scene.literal_0010")), DvValueTemplate.Instantiate("template.inline.c1_unseen_dormitories_simplify.610", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_c1_other_material_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_environment_install_scene.literal_0009")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_exclusion_hypotheses.literal_0002")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_unseen_dormitories_simplify.literal_0010")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_unseen_dormitories_simplify.literal_0011")), F_dv_with(F_dv_field(R.number("1")), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_unseen_dormitories_simplify.literal_0012")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_unseen_dormitories_simplify.literal_0013")), F_dv_with(F_dv_field(R.number("1")), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_initial_inquiry.literal_0002")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_environment_install_scene.literal_0017")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_activity_declarations.literal_0077")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_exclusion_hypotheses.literal_0019")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_exclusion_hypotheses.literal_0015")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_submission.literal")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_c1_other_material_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_exclusion_hypotheses.literal_0020")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_c1_other_material_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_exclusion_hypotheses.literal_0021")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_c1_other_material_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_environment_install_scene.literal_0021")), F_dv_with(F_dv_field(false), "basis", v_basis__865626));
        v_world__865749 = F_dv_store(v_world__865749, F_c1_other_material_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_environment_install_scene.literal_0022")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
        object v_actors__876971 = new List<object> {  };
        object v_i__876992 = R.number("0");
        while (R.Bool(R.lt(v_i__876992, R.count(F_dv_get(v_scene, "actors")))))
        {
            object v_actor__877050 = R.at(F_dv_get(v_scene, "actors"), v_i__876992);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_unseen_dormitories_simplify.611", v_actor__877050)))
            {
                object v_dmind__877186 = F_dv_get(v_actor__877050, "mind");
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_exclusion_hypotheses.literal_0002")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_unseen_dormitories_simplify.literal_0002")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_exclusion_hypotheses.literal_0015")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_exclusion_hypotheses.literal_0026")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_exclusion_hypotheses.literal_0025")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_c1_other_access_ref(Authored("m40.c1_other_people_build.literal_0004")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_exclusion_hypotheses.literal_0002")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_unseen_dormitories_simplify.literal_0006")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_environment_install_scene.literal_0017")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_exclusion_hypotheses.literal_0016")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_exclusion_hypotheses.literal_0015")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_exclusion_hypotheses.literal_0026")), F_dv_with(F_dv_field(false), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_exclusion_hypotheses.literal_0025")), F_dv_with(F_dv_field(false), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_c1_other_material_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_exclusion_hypotheses.literal_0017")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_c1_other_material_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_environment_install_scene.literal_0010")), DvValueTemplate.Instantiate("template.inline.c1_unseen_dormitories_simplify.612", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_c1_other_material_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_environment_install_scene.literal_0009")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_exclusion_hypotheses.literal_0002")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_unseen_dormitories_simplify.literal_0010")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_environment_install_scene.literal_0017")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_exclusion_hypotheses.literal_0019")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_exclusion_hypotheses.literal_0015")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_exclusion_hypotheses.literal_0026")), F_dv_with(F_dv_field(false), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_exclusion_hypotheses.literal_0025")), F_dv_with(F_dv_field(false), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_c1_other_material_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_exclusion_hypotheses.literal_0020")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_c1_other_material_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_exclusion_hypotheses.literal_0021")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_c1_other_material_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_environment_install_scene.literal_0021")), F_dv_with(F_dv_field(false), "basis", v_basis__865626));
                v_dmind__877186 = F_dv_store(v_dmind__877186, F_c1_other_material_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_environment_install_scene.literal_0022")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                object v_dactions__884669 = new List<object> {  };
                object v_j__884700 = R.number("0");
                while (R.Bool(R.lt(v_j__884700, R.count(F_dv_get(v_actor__877050, "actions")))))
                {
                    object v_act__884775 = R.at(F_dv_get(v_actor__877050, "actions"), v_j__884700);
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_unseen_dormitories_simplify.613", v_act__884775)))
                    {
                        _ = R.push(v_dactions__884669, v_act__884775);
                    }
                    v_j__884700 = R.add(v_j__884700, R.number("1"));
                }
                object v_dgoals__885029 = new List<object> {  };
                object v_old_goals__885058 = F_dv_find(F_dv_get(v_actor__877050, "policy"), "goals", new List<object> {  });
                v_j__884700 = R.number("0");
                while (R.Bool(R.lt(v_j__884700, R.count(v_old_goals__885058))))
                {
                    object v_g__885189 = R.at(v_old_goals__885058, v_j__884700);
                    object v_mid__885227 = F_dv_find(v_g__885189, Authored("m40.c1_unseen_dormitories_simplify.literal_0015"), "");
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_unseen_dormitories_simplify.614", v_mid__885227)))
                    {
                        _ = R.push(v_dgoals__885029, v_g__885189);
                    }
                    v_j__884700 = R.add(v_j__884700, R.number("1"));
                }
                v_actor__877050 = F_dv_with_many(v_actor__877050, new List<object> { F_dv_pair("mind", v_dmind__877186), F_dv_pair("actions", v_dactions__884669), F_dv_pair("policy", F_dv_with(F_dv_get(v_actor__877050, "policy"), "goals", v_dgoals__885029)) });
            }
            else
            {
                object v_group__885704 = F_dv_get(F_campaign_profile(F_dv_get(v_actor__877050, "id")), "group");
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_unseen_dormitories_simplify.615", v_group__885704)))
                {
                    object v_cmind__885870 = F_dv_get(v_actor__877050, "mind");
                    v_cmind__885870 = F_dv_store(v_cmind__885870, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_exclusion_hypotheses.literal_0026")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                    v_cmind__885870 = F_dv_store(v_cmind__885870, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_exclusion_hypotheses.literal_0025")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                    v_cmind__885870 = F_dv_store(v_cmind__885870, F_c1_other_access_ref(Authored("m40.c1_other_people_build.literal_0004")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                    v_cmind__885870 = F_dv_store(v_cmind__885870, F_c1dep_ref(Authored("m40.c1dep_can_cross.literal_0008")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                    v_cmind__885870 = F_dv_store(v_cmind__885870, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_exclusion_hypotheses.literal_0015")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                    v_cmind__885870 = F_dv_store(v_cmind__885870, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_activity_declarations.literal_0004")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                    v_cmind__885870 = F_dv_store(v_cmind__885870, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_initial_inquiry.literal")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                    v_cmind__885870 = F_dv_store(v_cmind__885870, F_campaign_case_ref(Authored("m40.c1_other_people_build.literal_0004"), Authored("m40.c1_other_moral_conflict.literal")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_unseen_dormitories_simplify.616", v_actor__877050)))
                    {
                        v_cmind__885870 = F_dv_store(v_cmind__885870, F_dv_ref(Authored("m40.c1n_rows_build.literal_0058"), "state", Authored("m40.c1n_rows_build.literal_0059")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                    }
                    object v_cactions__888193 = new List<object> {  };
                    object v_j__888228 = R.number("0");
                    while (R.Bool(R.lt(v_j__888228, R.count(F_dv_get(v_actor__877050, "actions")))))
                    {
                        if (R.Bool(F_c1_unseen_action_keep(F_dv_get(v_actor__877050, "id"), v_group__885704, F_dv_get(R.at(F_dv_get(v_actor__877050, "actions"), v_j__888228), "id"))))
                        {
                            _ = R.push(v_cactions__888193, R.at(F_dv_get(v_actor__877050, "actions"), v_j__888228));
                        }
                        v_j__888228 = R.add(v_j__888228, R.number("1"));
                    }
                    object v_dep_goals__888484 = F_dv_find(F_dv_get(v_actor__877050, "policy"), Authored("m40.c1_unseen_dormitories_simplify.literal_0017"), new List<object> {  });
                    object v_night_goals__888631 = F_dv_find(F_dv_get(v_actor__877050, "policy"), Authored("m40.c1n_refresh_actor_goals.literal"), new List<object> {  });
                    object v_cgoals__888768 = new List<object> {  };
                    v_j__888228 = R.number("0");
                    while (R.Bool(R.lt(v_j__888228, R.count(v_dep_goals__888484))))
                    {
                        _ = R.push(v_cgoals__888768, R.at(v_dep_goals__888484, v_j__888228));
                        v_j__888228 = R.add(v_j__888228, R.number("1"));
                    }
                    v_j__888228 = R.number("0");
                    while (R.Bool(R.lt(v_j__888228, R.count(v_night_goals__888631))))
                    {
                        _ = R.push(v_cgoals__888768, R.at(v_night_goals__888631, v_j__888228));
                        v_j__888228 = R.add(v_j__888228, R.number("1"));
                    }
                    v_actor__877050 = F_dv_with_many(v_actor__877050, new List<object> { F_dv_pair("mind", v_cmind__885870), F_dv_pair("actions", v_cactions__888193), F_dv_pair("policy", F_dv_with_many(F_dv_get(v_actor__877050, "policy"), new List<object> { F_dv_pair("goals", v_cgoals__888768), F_dv_pair("first_night_base_goals", v_dep_goals__888484) })) });
                    v_actor__877050 = F_c1n_known_future_wait_actor(v_actor__877050);
                }
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_unseen_dormitories_simplify.617", v_group__885704)))
                {
                    object v_omind__889568 = F_dv_get(v_actor__877050, "mind");
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_unseen_dormitories_simplify.618", v_actor__877050)))
                    {
                        v_omind__889568 = F_dv_store(v_omind__889568, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_exclusion_hypotheses.literal_0016")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                        v_omind__889568 = F_dv_store(v_omind__889568, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0008"), Authored("m40.c1_other_environment_install_scene.literal_0017")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                    }
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_unseen_dormitories_simplify.619", v_actor__877050)))
                    {
                        v_omind__889568 = F_dv_store(v_omind__889568, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_exclusion_hypotheses.literal_0019")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                        v_omind__889568 = F_dv_store(v_omind__889568, F_campaign_case_ref(Authored("m40.c1_other_environment_install_scene.literal_0016"), Authored("m40.c1_other_environment_install_scene.literal_0017")), F_dv_with(F_dv_field(true), "basis", v_basis__865626));
                    }
                    object v_oactions__890989 = new List<object> {  };
                    object v_j__891024 = R.number("0");
                    while (R.Bool(R.lt(v_j__891024, R.count(F_dv_get(v_actor__877050, "actions")))))
                    {
                        if (R.Bool(F_c1_unseen_action_keep(F_dv_get(v_actor__877050, "id"), v_group__885704, F_dv_get(R.at(F_dv_get(v_actor__877050, "actions"), v_j__891024), "id"))))
                        {
                            _ = R.push(v_oactions__890989, R.at(F_dv_get(v_actor__877050, "actions"), v_j__891024));
                        }
                        v_j__891024 = R.add(v_j__891024, R.number("1"));
                    }
                    object v_dep_goals__891280 = F_dv_find(F_dv_get(v_actor__877050, "policy"), Authored("m40.c1_unseen_dormitories_simplify.literal_0017"), new List<object> {  });
                    v_actor__877050 = DvValueTemplate.Instantiate("template.inline.c1_unseen_dormitories_simplify.620", v_actor__877050, v_omind__889568, v_oactions__890989, v_dep_goals__891280);
                }
            }
            _ = R.push(v_actors__876971, v_actor__877050);
            v_i__876992 = R.add(v_i__876992, R.number("1"));
        }
        return DvValueTemplate.Instantiate("template.inline.c1_unseen_dormitories_simplify.621", v_scene, v_world__865749, v_actors__876971);
    }
    public static object F_c1f_source(object v_id) {
        return DvValueTemplate.Instantiate("template.c1f_source", v_id);
    }
    public static object F_c1f_ref(object v_field) {
        return DvValueTemplate.Instantiate("template.c1f_ref", v_field);
    }
    public static object F_c1f_everyone(object v_room) {
        return DvValueTemplate.Instantiate("template.c1f_everyone", v_room);
    }
    public static object F_c1f_every_mark(object v_prefix) {
        return DvValueTemplate.Instantiate("template.c1f_every_mark", v_prefix);
    }
    public static object F_c1f_row(object v_actor,object v_id,object v_room,object v_start,object v_when,object v_label,object v_text) {
        return DvValueTemplate.Instantiate("template.c1f_row", v_actor, v_id, v_room, v_start, v_when, v_label, v_text);
    }
    public static object F_c1f_rows() {
        return DvValueTemplate.Instantiate("template.c1f_rows");
    }
    public static object F_c1f_inspections(object v_actor) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1f_inspections.636", v_actor)))
        {
            return new List<object> {  };
        }
        object v_table_here__905575 = F_campaign_at(v_actor, Authored("m40.c1n_install_scene.literal_0021"));
        object v_prepared__905691 = F_c1n_mark(Authored("m40.c1f_inspections.literal"));
        object v_cooked__905788 = F_c1n_mark(Authored("m40.c1f_inspections.literal_0002"));
        object v_table__905888 = DvValueTemplate.Instantiate("template.inline.c1f_inspections.637", v_actor, v_table_here__905575, v_prepared__905691, v_cooked__905788);
        object v_common__906550 = DvValueTemplate.Instantiate("template.inline.c1f_inspections.638", v_table__905888, v_actor, v_prepared__905691, v_cooked__905788);
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1f_inspections.639", v_actor)))
        {
            return v_common__906550;
        }
        object v_here__907637 = F_campaign_at(v_actor, Authored("c1f.poem_room"));
        object v_written__907743 = DvValueTemplate.Instantiate("template.inline.c1f_inspections.640", v_here__907637);
        object v_board__907867 = DvValueTemplate.Instantiate("template.inline.c1f_inspections.641", v_here__907637);
        v_board__907867 = DvValueTemplate.Instantiate("template.inline.c1f_inspections.642", v_board__907867, v_actor);
        object v_poem__908780 = DvValueTemplate.Instantiate("template.inline.c1f_inspections.643", v_written__907743);
        object v_readable_poem__909534 = DvValueTemplate.Instantiate("template.inline.c1f_inspections.644", v_poem__908780, v_actor);
        object v_contact__910289 = DvValueTemplate.Instantiate("template.inline.c1f_inspections.645", v_written__907743);
        object v_cuff__910489 = DvValueTemplate.Instantiate("template.inline.c1f_inspections.646", v_contact__910289);
        object v_target__910943 = new List<object> { F_dv_pair("name", "person"), F_dv_pair("ref", F_campaign_person_name_ref(Authored("m40.c1_other_people_build.literal_0017"))), F_dv_pair("fallback", F_campaign_unknown_person_label(Authored("m40.c1_other_people_build.literal_0017"), v_actor)) };
        object v_targeted_cuff__911221 = F_dv_with_many(v_cuff__910489, new List<object> { F_dv_pair("label_parts", new List<object> { AuthoredCopy("m40.c1f_inspections.literal_0032"), new List<object> { F_dv_pair("ref", F_campaign_person_name_ref(Authored("m40.c1_other_people_build.literal_0017"))), F_dv_pair("fallback", F_campaign_unknown_person_label(Authored("m40.c1_other_people_build.literal_0017"), v_actor)) }, AuthoredCopy("m40.c1f_inspections.literal_0033") }), DvValueTemplate.Instantiate("template.inline.c1f_inspections.647", v_actor, v_target__910943) });
        return F_c1f_join(F_c1f_join(v_common__906550, new List<object> { v_board__907867, v_readable_poem__909534, v_targeted_cuff__911221 }), DvValueTemplate.Instantiate("template.c1f_red_inspections", v_actor));
    }
    public static object F_c1f_join(object v_left,object v_right) {
        return DvValueTemplate.Instantiate("template.c1f_join", v_left, v_right);
    }
    public static object F_c1f_identity_actions(object v_actor) {
        object v_source__912224 = F_campaign_source(Authored("m40.c1f_identity_actions.literal"), R.number("0"), Authored("m40.c1f_identity_actions.literal_0002"));
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1f_identity_actions.648", v_actor)))
        {
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1f_identity_actions.649", v_actor)))
            {
                return new List<object> {  };
            }
            object v_literal__912628 = F_dv_fact(F_campaign_person_name_ref(v_actor), F_campaign_person_name(v_actor));
            object v_text__912721 = R.cat(Authored("m40.c1f_identity_actions.literal_0003"), R.cat(F_campaign_person_name(v_actor), Authored("m40.c1_other_reason_report_surface.literal_0002")));
            object v_reply__912951 = DvValueTemplate.Instantiate("template.inline.c1f_identity_actions.650", v_actor, v_literal__912628, v_text__912721, v_source__912224);
            return DvValueTemplate.Instantiate("template.inline.c1f_identity_actions.651", v_reply__912951, v_literal__912628, v_source__912224, v_text__912721);
        }
        object v_result__913714 = new List<object> {  };
        object v_people__913735 = F_campaign_people();
        object v_i__913771 = R.number("0");
        while (R.Bool(R.lt(v_i__913771, R.count(v_people__913735))))
        {
            object v_target__913823 = R.at(v_people__913735, v_i__913771);
            if (R.Bool(R.not(R.eq(v_target__913823, v_actor))))
            {
                object v_contact__913888 = F_c1_request_contact(v_actor, v_target__913823);
                object v_action__913949 = DvValueTemplate.Instantiate("template.inline.c1f_identity_actions.652", v_actor, v_target__913823, v_contact__913888, v_source__912224);
                object v_speech__914382 = DvValueTemplate.Instantiate("template.inline.c1f_identity_actions.653", v_source__912224, v_target__913823);
                _ = R.push(v_result__913714, F_dv_with_many(v_action__913949, new List<object> { F_dv_pair("manual_only", true), F_dv_pair("unknown_refs", new List<object> { F_campaign_person_name_ref(v_target__913823) }), F_dv_pair("speech", v_speech__914382), F_dv_pair("listeners", new List<object> { v_target__913823 }), F_dv_pair("label_parts", new List<object> { AuthoredCopy("m40.c1f_identity_actions.literal_0011"), new List<object> { F_dv_pair("ref", F_campaign_person_name_ref(v_target__913823)), F_dv_pair("fallback", F_campaign_unknown_person_label(v_target__913823, v_actor)) }, AuthoredCopy("m40.c1f_identity_actions.literal_0012") }) }));
            }
            v_i__913771 = R.add(v_i__913771, R.number("1"));
        }
        return v_result__913714;
    }
    public static object F_c1n_manual_wait_source() {
        return DvValueTemplate.Instantiate("template.c1n_manual_wait_source");
    }
    public static object F_c1n_manual_wait_actions(object v_actor) {
        return DvValueTemplate.Instantiate("template.c1n_manual_wait_actions", v_actor);
    }
    public static object F_c1f_poem_text() {
        return DvValueTemplate.Instantiate("template.c1f_poem_text");
    }
    public static object F_c1f_poem_translation() {
        return DvValueTemplate.Instantiate("template.c1f_poem_translation");
    }
}
