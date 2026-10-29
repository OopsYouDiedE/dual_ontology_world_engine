using DuelView.Core;
using DuelView.Compiled;
using static DuelView.Core.DVKernel;
public static partial class CampaignRuntime {

    public static object F_c1_player_scene(object v_scene) {
        object v_actors__485 = new List<object> {  };
        object v_i__506 = R.number("0");
        while (R.Bool(R.lt(v_i__506, R.count(F_dv_get(v_scene, "actors")))))
        {
            object v_actor__564 = R.at(F_dv_get(v_scene, "actors"), v_i__506);
            object v_actions__601 = new List<object> {  };
            object v_j__627 = R.number("0");
            while (R.Bool(R.lt(v_j__627, R.count(F_dv_get(v_actor__564, "actions")))))
            {
                _ = R.push(v_actions__601, F_c1_player_display(F_dv_get(v_actor__564, "id"), R.at(F_dv_get(v_actor__564, "actions"), v_j__627)));
                v_j__627 = R.add(v_j__627, R.number("1"));
            }
            _ = R.push(v_actors__485, F_dv_with(v_actor__564, "actions", v_actions__601));
            v_i__506 = R.add(v_i__506, R.number("1"));
        }
        return F_dv_with(v_scene, "actors", v_actors__485);
    }
    public static object F_c1_player_cards() {
        object v_key__936 = AuthoredCopy("m60.c1_player_cards.key");
        object v_cached__1029 = R.memo_get(new List<object> {  }, v_key__936);
        if (R.Bool(R.at(v_cached__1029, R.number("0"))))
        {
            return R.at(v_cached__1029, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__936, DvValueTemplate.Instantiate("template.inline.c1_player_cards.1528"));
    }
    public static object F_c1_player_display(object v_actor,object v_action) {
        object v_key__3551 = DvValueTemplate.Instantiate("template.inline.c1_player_display.1529", v_actor);
        object v_cached__3644 = R.memo_get(v_action, v_key__3551);
        if (R.Bool(R.at(v_cached__3644, R.number("0"))))
        {
            return R.at(v_cached__3644, R.number("1"));
        }
        return R.memo_put(v_action, v_key__3551, F_c1_player_display_build(v_actor, v_action));
    }
    public static object F_c1_player_display_build(object v_actor,object v_action) {
        object v_result__3855 = v_action;
        if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_action, "kind"), "move")) ? (object)(F_dv_has_key(v_action, "travel")) : (object)(false))) ? (object)(R.not(R.eq(F_dv_get(F_dv_get(v_action, "travel"), "from"), F_dv_get(F_dv_get(v_action, "travel"), "to")))) : (object)(false))))
        {
            v_result__3855 = DvValueTemplate.Instantiate("template.inline.c1_player_display_build.1530", v_result__3855, v_action);
        }
        object v_cards__4157 = F_c1_player_cards();
        object v_i__4192 = R.number("0");
        while (R.Bool(R.lt(v_i__4192, R.count(v_cards__4157))))
        {
            object v_card__4243 = R.at(v_cards__4157, v_i__4192);
            if (R.Bool(F_dvg_text_ends(F_dv_get(v_action, "id"), R.cat(Authored("m60.c1_player_display_build.literal_0002"), F_dv_get(v_card__4243, "id")))))
            {
                v_result__3855 = DvValueTemplate.Instantiate("template.inline.c1_player_display_build.1531", v_result__3855, v_card__4243, v_actor);
            }
            v_i__4192 = R.add(v_i__4192, R.number("1"));
        }
        object v_named__4715 = AuthoredCopy("m60.c1_player_display_build.named");
        v_i__4192 = R.number("0");
        while (R.Bool(R.lt(v_i__4192, R.count(v_named__4715))))
        {
            if (R.Bool(F_dvg_text_ends(F_dv_get(v_action, "id"), R.at(R.at(v_named__4715, v_i__4192), R.number("0")))))
            {
                v_result__3855 = F_dv_with(v_result__3855, "label", R.at(R.at(v_named__4715, v_i__4192), R.number("1")));
            }
            v_i__4192 = R.add(v_i__4192, R.number("1"));
        }
        if (R.Bool(F_dvg_text_ends(F_dv_get(v_action, "id"), Authored("m60.c1_player_display_build.literal_0005"))))
        {
            v_result__3855 = DvValueTemplate.Instantiate("template.inline.c1_player_display_build.1532", v_result__3855);
        }
        if (R.Bool(R.eq(F_dv_get(v_action, "kind"), "answer")))
        {
            object v_answers__5269 = AuthoredCopy("m60.c1_player_display_build.answers");
            object v_id__5382 = F_dv_find(v_action, Authored("m60.c1_player_display_build.literal_0007"), "");
            if (R.Bool(F_dv_has_key(v_answers__5269, v_id__5382)))
            {
                v_result__3855 = DvValueTemplate.Instantiate("template.inline.c1_player_display_build.1533", v_result__3855, v_answers__5269, v_id__5382);
            }
        }
        if (R.Bool(F_dvg_text_ends(F_dv_get(v_action, "id"), Authored("m60.c1_player_display_build.literal_0009"))))
        {
            object v_questions__5810 = DvValueTemplate.Instantiate("template.inline.c1_player_display_build.1534");
            v_result__3855 = DvValueTemplate.Instantiate("template.inline.c1_player_display_build.1535", v_result__3855, v_questions__5810);
        }
        if (R.Bool(F_dvg_text_ends(F_dv_get(v_action, "id"), Authored("m60.c1_player_display_build.literal_0019"))))
        {
            v_result__3855 = DvValueTemplate.Instantiate("template.inline.c1_player_display_build.1536", v_result__3855);
        }
        if (R.Bool(F_dvg_text_ends(F_dv_get(v_action, "id"), Authored("m60.c1_player_display_build.literal_0023"))))
        {
            v_result__3855 = DvValueTemplate.Instantiate("template.inline.c1_player_display_build.1537", v_result__3855);
        }
        if (R.Bool(F_dvg_text_ends(F_dv_get(v_action, "id"), Authored("m60.c1_player_display_build.literal_0027"))))
        {
            v_result__3855 = DvValueTemplate.Instantiate("template.inline.c1_player_display_build.1538", v_result__3855);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_player_display_build.1539", v_result__3855)))
        {
            v_result__3855 = F_dv_with(v_result__3855, Authored("m60.c1_player_display_build.literal_0032"), F_c1_player_name_parts(v_actor, F_dv_find(v_result__3855, "label", "")));
        }
        return v_result__3855;
    }

    public static object F_c1_player_replay_summary(object v_actor) {
        object v_candidates__11322 = F_dv_find(F_dv_get(v_actor, "policy"), Authored("m60.c1_player_replay_summary.literal"), new List<object> {  });
        object v_i__11447 = R.number("0");
        while (R.Bool(R.lt(v_i__11447, R.count(v_candidates__11322))))
        {
            object v_candidate__11503 = R.at(v_candidates__11322, v_i__11447);
            if (R.Bool(F_dvs_working_explanation(v_actor, v_candidate__11503)))
            {
                object v_model__11601 = F_dvs_cached_model(v_actor, v_candidate__11503);
                if (R.Bool(F_dv_explanation_model_complete(v_model__11601, Authored("m60.c1_player_replay_summary.literal_0002"), F_dv_get(F_dv_get(v_actor, "mind"), "revision"))))
                {
                    object v_question__11822 = F_dv_read(F_dv_get(v_actor, "mind"), F_dv_ref(Authored("m60.c1_player_display_build.literal_0017"), "state", Authored("m60.c1_player_display_build.literal_0018")));
                    if (R.Bool(F_dv_uncontested(v_question__11822)))
                    {
                        object v_needed__12103 = F_dv_find(AuthoredCopy("m60.c1_player_replay_summary.literal_0003"), F_dv_get(v_question__11822, "value"), "");
                        if (R.Bool((R.Bool(R.not(R.eq(v_needed__12103, ""))) ? (object)(R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_fact(F_campaign_evidence_ref(v_needed__12103), true)), "true"))) : (object)(false))))
                        {
                            return Authored("m60.c1_player_replay_summary.return");
                        }
                    }
                    return Authored("m60.c1_player_replay_summary.return_0002");
                }
            }
            v_i__11447 = R.add(v_i__11447, R.number("1"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.c1_player_replay_summary.1540", v_actor)))
        {
            return Authored("m60.c1_player_replay_summary.return_0003");
        }
        return Authored("m60.c1_player_replay_summary.return_0004");
    }

    public static object F_dvg_island_locations() {
        object v_places__13899 = F_dvg_places();
        object v_rooms__13930 = F_island_topology_rooms();
        object v_result__13971 = new List<object> {  };
        object v_i__13992 = R.number("0");
        while (R.Bool(R.lt(v_i__13992, R.count(v_places__13899))))
        {
            object v_place__14044 = R.at(R.at(v_places__13899, v_i__13992), R.number("1"));
            if (R.Bool(R.not(R.lt(F_dv_index_by_id(v_rooms__13930, F_dv_get(v_place__14044, "room")), R.number("0")))))
            {
                _ = R.push(v_result__13971, new List<object> { F_dv_pair("location", R.at(R.at(v_places__13899, v_i__13992), R.number("0"))), F_dv_pair("room", F_dv_get(v_place__14044, "room")) });
            }
            v_i__13992 = R.add(v_i__13992, R.number("1"));
        }
        return v_result__13971;
    }

    public static object F_dvg_presentation(object v_state,object v_viewer) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_presentation.1541", v_state)))
        {
            v_state = R.snapshot(v_state);
        }
        object v_result__20282 = F_dvs_session_presentation(v_state, v_viewer);
        object v_actor__20340 = F_dv_find(v_result__20282, "speaker", F_dv_find(v_result__20282, Authored("m60.dvg_presentation.literal_0002"), ""));
        object v_display__20479 = "";
        object v_identity_source__20501 = Authored("m60.dvg_presentation.identity_source");
        object v_revision__20604 = R.sub(R.number("0"), R.number("1"));
        object v_i__20627 = R.number("0");
        while (R.Bool(R.lt(v_i__20627, R.count(F_dv_get(v_state, "actors")))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(F_dv_get(v_state, "actors"), v_i__20627), "id"), v_viewer)))
            {
                object v_subject__20731 = R.at(F_dv_get(v_state, "actors"), v_i__20627);
                v_revision__20604 = F_dv_get(F_dv_get(v_subject__20731, "mind"), "revision");
                v_result__20282 = F_dv_with(v_result__20282, Authored("m60.dvg_presentation.literal_0003"), F_dvg_subjective_person_text(v_state, v_subject__20731, F_dv_find(v_result__20282, Authored("m60.dvg_presentation.literal_0003"), "")));
                object v_projected_lines__21071 = new List<object> {  };
                object v_authored_lines__21109 = F_dv_find(v_result__20282, Authored("m60.dvg_presentation.literal_0004"), new List<object> {  });
                object v_line_index__21237 = R.number("0");
                while (R.Bool(R.lt(v_line_index__21237, R.count(v_authored_lines__21109))))
                {
                    object v_line__21331 = R.at(v_authored_lines__21109, v_line_index__21237);
                    if (R.Bool(R.is_text(v_line__21331)))
                    {
                        _ = R.push(v_projected_lines__21071, F_dvg_subjective_person_text(v_state, v_subject__20731, v_line__21331));
                    }
                    else
                    {
                        _ = R.push(v_projected_lines__21071, v_line__21331);
                    }
                    v_line_index__21237 = R.add(v_line_index__21237, R.number("1"));
                }
                v_result__20282 = DvValueTemplate.Instantiate("template.inline.dvg_presentation.1542", v_result__20282, v_projected_lines__21071);
                v_display__20479 = F_dvg_preview_person_name(v_state, F_dv_get(v_subject__20731, "mind"), v_actor__20340, v_actor__20340, "");
                if (R.Bool(R.not(R.eq(v_display__20479, ""))))
                {
                    v_identity_source__20501 = Authored("m60.dvg_presentation.literal_0005");
                }
                else
                {
                    v_display__20479 = F_dvg_unknown_person_label(v_actor__20340, v_viewer);
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_presentation.1543", v_actor__20340)))
                    {
                        v_identity_source__20501 = Authored("m60.dvg_presentation.literal_0007");
                    }
                }
                if (R.Bool((R.Bool((R.Bool(R.eq(v_actor__20340, v_viewer)) ? (object)(R.eq(F_dv_find(v_result__20282, "status", ""), Authored("m60.dvg_presentation.literal_0008"))) : (object)(false))) ? (object)(F_dvg_text_ends(F_dv_find(v_result__20282, "action_id", ""), Authored("m60.dvg_presentation.literal_0009"))) : (object)(false))))
                {
                    v_result__20282 = F_dv_with(v_result__20282, Authored("m60.dvg_presentation.literal_0004"), F_dv_append(F_dv_find(v_result__20282, Authored("m60.dvg_presentation.literal_0004"), new List<object> {  }), F_c1_player_replay_summary(v_subject__20731)));
                }
            }
            v_i__20627 = R.add(v_i__20627, R.number("1"));
        }
        object v_travel__22828 = F_dv_find(v_result__20282, "travel", new List<object> {  });
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_presentation.1544", v_result__20282, v_viewer)))
        {
            object v_feedback__23103 = Authored("m60.dvg_presentation.feedback");
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_presentation.1545", v_result__20282)))
            {
                v_feedback__23103 = Authored("m60.dvg_presentation.literal_0013");
            }
            object v_specific__23476 = F_dv_find(v_result__20282, Authored("m60.dvg_presentation.literal_0004"), new List<object> {  });
            object v_feedback_lines__23594 = v_specific__23476;
            if (R.Bool(R.eq(R.count(v_specific__23476), R.number("0"))))
            {
                v_feedback_lines__23594 = new List<object> { v_feedback__23103 };
            }
            v_result__20282 = DvValueTemplate.Instantiate("template.inline.dvg_presentation.1546", v_result__20282, v_feedback_lines__23594);
        }
        object v_judgment__23979 = F_dv_find(v_result__20282, Authored("m60.dvg_presentation.literal_0015"), new List<object> {  });
        if (R.Bool(R.not(R.is_list(v_judgment__23979))))
        {
            v_judgment__23979 = new List<object> {  };
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_presentation.1547", v_result__20282, v_viewer)))
        {
            object v_receipt__24377 = "";
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_presentation.1548", v_judgment__23979)))
            {
                v_receipt__24377 = Authored("m60.dvg_presentation.literal_0017");
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_presentation.1549", v_judgment__23979)))
            {
                object v_reasons__24745 = AuthoredCopy("m60.dvg_presentation.reasons");
                v_receipt__24377 = R.cat(Authored("m60.dvg_presentation.literal_0019"), F_dv_find(v_reasons__24745, F_dv_find(v_judgment__23979, Authored("m60.dvg_presentation.literal_0020"), ""), Authored("m60.dvg_presentation.literal_0021")));
            }
            if (R.Bool(R.not(R.eq(v_receipt__24377, ""))))
            {
                v_result__20282 = DvValueTemplate.Instantiate("template.inline.dvg_presentation.1550", v_result__20282, v_receipt__24377, v_display__20479);
            }
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_presentation.1551", v_result__20282, v_viewer)))
        {
            object v_destination__25948 = F_dv_find(v_travel__22828, "to", "");
            if (R.Bool((R.Bool(R.not(R.eq(v_destination__25948, ""))) ? (object)(R.not(R.eq(v_destination__25948, F_dv_find(v_travel__22828, "from", "")))) : (object)(false))))
            {
                object v_title__26083 = F_dvg_location_title(v_destination__25948);
                object v_lines__26140 = F_dv_find(v_result__20282, Authored("m60.dvg_presentation.literal_0004"), new List<object> {  });
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_presentation.1552", v_lines__26140, v_result__20282)))
                {
                    v_lines__26140 = F_dv_append(v_lines__26140, R.cat(Authored("m60.dvg_presentation.literal_0025"), R.cat(v_title__26083, Authored("m60.dvg_presentation.literal_0026"))));
                }
                v_result__20282 = DvValueTemplate.Instantiate("template.inline.dvg_presentation.1553", v_result__20282, v_lines__26140, v_destination__25948, v_title__26083);
            }
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_presentation.1554", v_result__20282)))
        {
            v_display__20479 = Authored("m60.dvg_presentation.literal_0030");
            v_identity_source__20501 = Authored("m60.dvg_presentation.literal_0031");
        }
        return F_dv_with_many(v_result__20282, new List<object> { F_dv_pair("speaker_name", v_display__20479), F_dv_pair("speaker_name_source", v_identity_source__20501), F_dv_pair("presentation_mind_revision", v_revision__20604) });
    }

    public static object F_dvg_people(object v_state,object v_viewer) {
        object v_viewer_mind__28964 = F_dv_get(R.at(F_dv_get(v_state, "actors"), R.number("0")), "mind");
        object v_viewer_index__29008 = R.number("0");
        while (R.Bool(R.lt(v_viewer_index__29008, R.count(F_dv_get(v_state, "actors")))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(F_dv_get(v_state, "actors"), v_viewer_index__29008), "id"), v_viewer)))
            {
                v_viewer_mind__28964 = F_dv_get(R.at(F_dv_get(v_state, "actors"), v_viewer_index__29008), "mind");
            }
            v_viewer_index__29008 = R.add(v_viewer_index__29008, R.number("1"));
        }
        object v_people__29237 = new List<object> {  };
        object v_i__29258 = R.number("0");
        while (R.Bool(R.lt(v_i__29258, R.count(F_dv_get(v_state, "actors")))))
        {
            object v_actor__29316 = R.at(F_dv_get(v_state, "actors"), v_i__29258);
            object v_position__29353 = F_dv_read(F_dv_get(v_state, "physical"), F_campaign_position_ref(F_dv_get(v_actor__29316, "id")));
            if (R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_actor__29316, "id"), v_viewer))) ? (object)(F_dv_uncontested(v_position__29353)) : (object)(false))))
            {
                object v_place__29499 = F_dvg_place(F_dv_get(v_position__29353, "value"));
                if (R.Bool(R.not(R.eq(F_dv_get(v_place__29499, "map"), ""))))
                {
                    object v_person__29587 = new List<object> { F_dv_pair("key", F_dv_get(v_actor__29316, "id")), F_dv_pair("map", F_dv_get(v_place__29499, "map")), F_dv_pair("room", F_dv_get(v_place__29499, "room")), F_dv_pair("execution_room", F_dv_get(v_place__29499, "room")), F_dv_pair("logical_location", F_dv_get(v_position__29353, "value")), F_dv_pair("initial_anchor", F_dvg_initial_anchor(F_dv_get(v_actor__29316, "id"), F_dv_get(v_position__29353, "value"))), DvValueTemplate.Instantiate("template.inline.dvg_people.1555", v_place__29499), F_dv_pair("present", true) };
                    object v_title__29912 = F_dvg_preview_person_name(v_state, v_viewer_mind__28964, F_dv_get(v_actor__29316, "id"), F_dv_get(v_actor__29316, "id"), F_dvg_unknown_person_label(F_dv_get(v_actor__29316, "id"), v_viewer));
                    v_person__29587 = F_dv_with(v_person__29587, "title", v_title__29912);
                    object v_motion__30107 = F_dvg_person_motion(v_actor__29316, F_dv_get(F_dv_get(v_state, "physical"), "time"));
                    if (R.Bool(R.not(R.eq(v_motion__30107, false))))
                    {
                        v_person__29587 = DvValueTemplate.Instantiate("template.inline.dvg_people.1556", v_person__29587, v_motion__30107);
                    }
                    if (R.Bool(F_dv_has_key(v_place__29499, "anchor")))
                    {
                        v_person__29587 = F_dv_with(v_person__29587, "anchor", F_dv_get(v_place__29499, "anchor"));
                    }
                    _ = R.push(v_people__29237, v_person__29587);
                }
            }
            v_i__29258 = R.add(v_i__29258, R.number("1"));
        }
        return v_people__29237;
    }

    public static object F_dvg_replay_comparison(object v_subject,object v_stored,object v_hypothesis,object v_comparison) {
        object v_public__31843 = F_c1_hist_public_feature(F_dv_find(v_comparison, Authored("m60.dvg_replay_comparison.literal"), new List<object> {  }));
        if (R.Bool(R.eq(v_public__31843, false)))
        {
            return false;
        }
        object v_observed__32030 = F_dv_find(v_comparison, Authored("m60.dvg_replay_comparison.literal_0002"), new List<object> {  });
        if (R.Bool(R.not(R.eq(R.count(v_observed__32030), R.number("1")))))
        {
            return false;
        }
        object v_input__32208 = R.at(v_observed__32030, R.number("0"));
        if (R.Bool(R.not(F_dv_same(F_dv_get(v_input__32208, "ref"), F_dv_get(F_dv_get(v_public__31843, "observation"), "ref")))))
        {
            return false;
        }
        object v_cell__32315 = F_dv_get(v_input__32208, "cell");
        object v_current__32342 = F_dv_read(F_dv_get(v_subject, "mind"), F_dv_get(F_dv_get(v_public__31843, "observation"), "ref"));
        if (R.Bool((R.Bool(R.not(F_dv_uncontested(v_cell__32315))) ? (object)(true) : (object)(R.not(F_dv_uncontested(v_current__32342))))))
        {
            return false;
        }
        if (R.Bool((R.Bool(R.not(F_dv_same(v_cell__32315, v_current__32342))) ? (object)(true) : (object)(R.not(R.lt(R.number("0"), F_dv_get(v_cell__32315, "confidence")))))))
        {
            return false;
        }
        object v_observation_action_ids__32661 = new List<object> {  };
        object v_claims__32698 = F_dv_find(v_cell__32315, Authored("m60.dvg_replay_comparison.literal_0003"), new List<object> {  });
        object v_i__32813 = R.number("0");
        while (R.Bool(R.lt(v_i__32813, R.count(v_claims__32698))))
        {
            object v_claim__32865 = R.at(v_claims__32698, v_i__32813);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_replay_comparison.1557", v_claim__32865, v_subject)))
            {
                if (R.Bool((R.Bool((R.Bool(F_dv_same_local_ref(F_dv_get(v_subject, "id"), F_dv_get(v_claim__32865, "ref"), F_dv_get(F_dv_get(v_public__31843, "observation"), "ref"))) ? (object)(F_dv_has(F_dv_get(F_dv_get(v_subject, "mind"), "seen"), F_dv_get(v_claim__32865, "id"))) : (object)(false))) ? (object)(F_dv_same(F_dv_get(v_claim__32865, "value"), F_dv_get(v_cell__32315, "value"))) : (object)(false))))
                {
                    object v_dependency__33222 = F_dv_find(v_claim__32865, Authored("m60.dvg_replay_comparison.literal_0005"), new List<object> {  });
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_replay_comparison.1558", v_dependency__33222, v_claim__32865)))
                    {
                        if (R.Bool((R.Bool((R.Bool(R.eq(R.at(v_dependency__33222, R.number("0")), R.at(F_dv_get(v_claim__32865, "id"), R.number("0")))) ? (object)(R.not(R.eq(R.at(v_dependency__33222, R.number("0")), Authored("m60.dvg_replay_comparison.literal_0008")))) : (object)(false))) ? (object)(F_dv_same_local_ref(F_dv_get(v_subject, "id"), R.at(v_dependency__33222, R.number("1")), F_dv_get(F_dv_get(v_public__31843, "observation"), "ref"))) : (object)(false))))
                        {
                            if (R.Bool(R.not(F_dv_has(v_observation_action_ids__32661, R.at(v_dependency__33222, R.number("0"))))))
                            {
                                _ = R.push(v_observation_action_ids__32661, R.at(v_dependency__33222, R.number("0")));
                            }
                        }
                    }
                }
            }
            v_i__32813 = R.add(v_i__32813, R.number("1"));
        }
        if (R.Bool(R.eq(R.count(v_observation_action_ids__32661), R.number("0"))))
        {
            return false;
        }
        object v_result__34192 = F_dv_find(v_comparison, Authored("m60.dvg_replay_comparison.literal_0009"), "unknown");
        object v_labels__34320 = AuthoredCopy("m60.dvg_replay_comparison.labels");
        return new List<object> { F_dv_pair("title", F_dv_get(v_public__31843, "public_observation_label")), F_dv_pair("text", F_dv_find(v_labels__34320, v_result__34192, F_dv_get(v_labels__34320, "unknown"))), F_dv_pair("result", v_result__34192), F_dv_pair("model_event", F_dv_get(v_stored, "event")), F_dv_pair("source_refs", F_dv_find(v_hypothesis, "basis", new List<object> {  })), F_dv_pair("observation_action_ids", v_observation_action_ids__32661) };
    }
    public static object F_dvg_manual_history_title(object v_hypothesis,object v_subject) {
        object v_variables__34889 = F_c1_manual_replay_variables();
        object v_assignments__34939 = F_dv_find(v_hypothesis, "variable_assignments", new List<object> {  });
        object v_parts__35010 = new List<object> {  };
        object v_i__35030 = R.number("0");
        while (R.Bool(R.lt(v_i__35030, R.count(v_assignments__34939))))
        {
            object v_j__35087 = R.number("0");
            while (R.Bool(R.lt(v_j__35087, R.count(v_variables__34889))))
            {
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_manual_history_title.1559", v_variables__34889, v_j__35087, v_assignments__34939, v_i__35030, v_subject)))
                {
                    object v_values__35362 = F_dv_get(R.at(v_variables__34889, v_j__35087), "values");
                    object v_k__35412 = R.number("0");
                    while (R.Bool(R.lt(v_k__35412, R.count(v_values__35362))))
                    {
                        if (R.Bool(F_dv_same(F_dv_get(R.at(v_values__35362, v_k__35412), "value"), F_dv_get(R.at(v_assignments__34939, v_i__35030), "value"))))
                        {
                            if (R.Bool(R.lt(R.number("0"), R.count(v_parts__35010))))
                            {
                                _ = R.push(v_parts__35010, Authored("m60.dvg_manual_history_title.literal_0002"));
                            }
                            _ = R.push(v_parts__35010, R.cat(F_dv_get(R.at(v_variables__34889, v_j__35087), "label"), R.cat(Authored("m60.dvg_manual_history_title.literal_0003"), F_dv_get(R.at(v_values__35362, v_k__35412), "label"))));
                        }
                        v_k__35412 = R.add(v_k__35412, R.number("1"));
                    }
                }
                v_j__35087 = R.add(v_j__35087, R.number("1"));
            }
            v_i__35030 = R.add(v_i__35030, R.number("1"));
        }
        return R.cat(Authored("m60.dvg_manual_history_title.literal_0004"), R.text_join(v_parts__35010));
    }
    public static object F_dvg_replay_titles() {
        return DvValueTemplate.Instantiate("template.dvg_replay_titles");
    }
    public static object F_dvg_manual_replay_input(object v_actor,object v_action) {
        object v_titles__36338 = F_dvg_replay_titles();
        object v_manual__36376 = DvValueTemplate.Instantiate("template.inline.dvg_manual_replay_input.1560", v_actor);
        object v_refs__36491 = F_dvp_goal_refs(v_actor);
        object v_all__36528 = F_dv_find(F_dv_get(v_actor, "policy"), Authored("m60.c1_player_replay_summary.literal"), new List<object> {  });
        object v_named__36646 = new List<object> {  };
        object v_cases__36666 = new List<object> {  };
        object v_variables__36686 = new List<object> {  };
        object v_i__36710 = R.number("0");
        while (R.Bool(R.lt(v_i__36710, R.count(v_all__36528))))
        {
            object v_candidate__36759 = R.at(v_all__36528, v_i__36710);
            if (R.Bool((R.Bool(F_dvs_manual_candidate_available(v_actor, v_candidate__36759)) ? (object)(F_dvp_eligible(v_manual__36376, DvValueTemplate.Instantiate("template.inline.dvg_manual_replay_input.1561", v_action, v_candidate__36759), F_dv_get(v_actor, "mind"), v_refs__36491)) : (object)(false))))
            {
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_manual_replay_input.1562", v_candidate__36759)))
                {
                    object v_assignments__37148 = F_dv_get(v_candidate__36759, "variable_assignments");
                    if (R.Bool(R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_c1_manual_history_gate()), "true"))))
                    {
                        v_assignments__37148 = new List<object> {  };
                        object v_assignment_index__37334 = R.number("0");
                        while (R.Bool(R.lt(v_assignment_index__37334, R.count(F_dv_get(v_candidate__36759, "variable_assignments")))))
                        {
                            if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_manual_replay_input.1563", v_candidate__36759, v_assignment_index__37334)))
                            {
                                _ = R.push(v_assignments__37148, R.at(F_dv_get(v_candidate__36759, "variable_assignments"), v_assignment_index__37334));
                            }
                            v_assignment_index__37334 = R.add(v_assignment_index__37334, R.number("1"));
                        }
                    }
                    _ = R.push(v_cases__36666, new List<object> { F_dv_pair("id", F_dv_get(v_candidate__36759, "id")), F_dv_pair("assignments", v_assignments__37148) });
                }
                else
                {
                    if (R.Bool(F_dv_has_key(v_titles__36338, F_dv_get(v_candidate__36759, "id"))))
                    {
                        _ = R.push(v_named__36646, new List<object> { F_dv_pair("id", F_dv_get(v_candidate__36759, "id")), F_dv_pair("label", F_dv_get(v_titles__36338, F_dv_get(v_candidate__36759, "id"))) });
                    }
                }
            }
            v_i__36710 = R.add(v_i__36710, R.number("1"));
        }
        object v_declared__38058 = F_c1_manual_replay_variables();
        v_i__36710 = R.number("0");
        while (R.Bool(R.lt(v_i__36710, R.count(v_declared__38058))))
        {
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_manual_replay_input.1564", v_actor, v_declared__38058, v_i__36710)))
            {
                _ = R.push(v_variables__36686, F_dv_without(R.at(v_declared__38058, v_i__36710), Authored("m60.dvg_manual_history_title.literal")));
            }
            v_i__36710 = R.add(v_i__36710, R.number("1"));
        }
        object v_assumption__38460 = "";
        if (R.Bool(R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_c1_manual_history_gate()), "true"))))
        {
            v_assumption__38460 = Authored("m60.dvg_manual_replay_input.literal_0005");
        }
        return new List<object> { F_dv_pair("named", v_named__36646), F_dv_pair("variables", v_variables__36686), F_dv_pair("cases", v_cases__36666), F_dv_pair("initial_assumption", v_assumption__38460) };
    }
    public static object F_dvg_replay(object v_state,object v_viewer) {
        object v_i__38789 = R.number("0");
        while (R.Bool(R.lt(v_i__38789, R.count(F_dv_get(v_state, "actors")))))
        {
            object v_actor__38847 = R.at(F_dv_get(v_state, "actors"), v_i__38789);
            if (R.Bool(R.eq(F_dv_get(v_actor__38847, "id"), v_viewer)))
            {
                object v_key__38920 = AuthoredCopy("m60.dvg_replay.key");
                object v_cached__39016 = R.memo_get(v_actor__38847, v_key__38920);
                if (R.Bool(R.at(v_cached__39016, R.number("0"))))
                {
                    return R.at(v_cached__39016, R.number("1"));
                }
                return R.memo_put(v_actor__38847, v_key__38920, F_dvg_replay_build(v_state, v_viewer));
            }
            v_i__38789 = R.add(v_i__38789, R.number("1"));
        }
        return false;
    }
    public static object F_dvg_replay_build(object v_state,object v_viewer) {
        object v_subject__39272 = false;
        object v_actor_index__39297 = R.number("0");
        while (R.Bool(R.lt(v_actor_index__39297, R.count(F_dv_get(v_state, "actors")))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(F_dv_get(v_state, "actors"), v_actor_index__39297), "id"), v_viewer)))
            {
                v_subject__39272 = R.at(F_dv_get(v_state, "actors"), v_actor_index__39297);
            }
            v_actor_index__39297 = R.add(v_actor_index__39297, R.number("1"));
        }
        if (R.Bool(R.eq(v_subject__39272, false)))
        {
            return AuthoredCopy("m60.dvg_replay_build.return");
        }
        object v_simulations__39635 = F_dv_find(v_subject__39272, Authored("m60.dvg_replay_build.literal"), new List<object> {  });
        object v_titles__39748 = F_dvg_replay_titles();
        object v_rows__39786 = new List<object> {  };
        object v_pass__39805 = R.number("0");
        while (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_replay_build.1565", v_pass__39805, v_rows__39786)))
        {
            object v_i__40174 = R.number("0");
            while (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_replay_build.1566", v_i__40174, v_simulations__39635, v_rows__39786)))
            {
                object v_stored__40333 = R.at(R.at(v_simulations__39635, v_i__40174), R.number("1"));
                object v_hypothesis__40377 = F_dv_find(v_stored__40333, Authored("m60.dvg_replay_build.literal_0003"), false);
                object v_explained__40504 = R.eq(F_dv_find(v_stored__40333, "status", ""), Authored("m60.c1_player_replay_summary.literal_0002"));
                object v_selected__40647 = (R.Bool(R.not(R.eq(v_hypothesis__40377, false))) ? (object)(R.eq(F_dv_find(v_subject__39272, Authored("m60.dvg_replay_build.literal_0004"), ""), F_dv_find(v_hypothesis__40377, "id", ""))) : (object)(false));
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_replay_build.1567", v_hypothesis__40377, v_stored__40333, v_pass__39805, v_selected__40647, v_explained__40504)))
                {
                    object v_current__41110 = F_dvs_cached_model(v_subject__39272, v_hypothesis__40377);
                    object v_status__41179 = F_dv_find(v_current__41110, "status", Authored("m60.dvg_replay_build.literal_0005"));
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_replay_build.1568", v_status__41179, v_stored__40333)))
                    {
                        v_status__41179 = "unknown";
                    }
                    object v_labels__41510 = AuthoredCopy("m60.dvg_replay_build.labels");
                    object v_lines__41622 = DvValueTemplate.Instantiate("template.inline.dvg_replay_build.1569", v_labels__41510, v_status__41179);
                    if (R.Bool((R.Bool(F_dv_has_key(v_stored__40333, "plan_complete")) ? (object)(R.not(F_dv_get(v_stored__40333, "plan_complete"))) : (object)(false))))
                    {
                        _ = R.push(v_lines__41622, Authored("m60.dvg_replay_build.literal_0007"));
                    }
                    if (R.Bool(R.not(F_dv_has_key(v_stored__40333, "plan_complete"))))
                    {
                        _ = R.push(v_lines__41622, Authored("m60.dvg_replay_build.literal_0008"));
                    }
                    _ = R.push(v_lines__41622, R.cat(Authored("m60.dvg_replay_build.literal_0009"), R.show(R.count(F_dv_find(v_stored__40333, Authored("m60.dvg_replay_build.literal_0010"), new List<object> {  })))));
                    _ = R.push(v_lines__41622, R.cat(Authored("m60.dvg_replay_build.literal_0011"), R.show(R.count(F_dv_find(v_stored__40333, Authored("m60.dvg_replay_build.literal_0012"), new List<object> {  })))));
                    object v_readable_comparisons__42574 = new List<object> {  };
                    object v_comparisons__42621 = F_dv_find(v_stored__40333, Authored("m60.dvg_replay_build.literal_0010"), new List<object> {  });
                    object v_comparison_index__42750 = R.number("0");
                    while (R.Bool(R.lt(v_comparison_index__42750, R.count(v_comparisons__42621))))
                    {
                        object v_readable__42861 = F_dvg_replay_comparison(v_subject__39272, v_stored__40333, v_hypothesis__40377, R.at(v_comparisons__42621, v_comparison_index__42750));
                        if (R.Bool(R.not(R.eq(v_readable__42861, false))))
                        {
                            _ = R.push(v_readable_comparisons__42574, v_readable__42861);
                            _ = R.push(v_lines__41622, R.cat(F_dv_get(v_readable__42861, "title"), R.cat(Authored("m60.dvg_manual_history_title.literal_0003"), F_dv_get(v_readable__42861, "text"))));
                        }
                        v_comparison_index__42750 = R.add(v_comparison_index__42750, R.number("1"));
                    }
                    if (R.Bool(R.eq(R.count(v_readable_comparisons__42574), R.number("0"))))
                    {
                        _ = R.push(v_lines__41622, Authored("m60.dvg_replay_build.literal_0013"));
                    }
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_replay_build.1570", v_status__41179, v_readable_comparisons__42574)))
                    {
                        _ = R.push(v_lines__41622, Authored("m60.dvg_replay_build.literal_0014"));
                    }
                    object v_title__43733 = F_dv_find(v_hypothesis__40377, "label", F_dv_find(v_hypothesis__40377, "title", F_dv_find(v_titles__39748, F_dv_get(v_hypothesis__40377, "id"), R.cat(Authored("m60.dvg_replay_build.literal_0015"), R.show(R.add(v_i__40174, R.number("1")))))));
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_replay_build.1571", v_hypothesis__40377)))
                    {
                        v_title__43733 = F_dvg_manual_history_title(v_hypothesis__40377, v_subject__39272);
                    }
                    _ = R.push(v_rows__39786, DvValueTemplate.Instantiate("template.inline.dvg_replay_build.1572", v_title__43733, v_lines__41622, v_stored__40333, v_hypothesis__40377, v_status__41179, v_readable_comparisons__42574));
                }
                v_i__40174 = R.add(v_i__40174, R.number("1"));
            }
            v_pass__39805 = R.add(v_pass__39805, R.number("1"));
        }
        object v_summary__44593 = Authored("m60.dvg_replay_build.summary");
        if (R.Bool(R.lt(R.number("0"), R.count(v_simulations__39635))))
        {
            v_summary__44593 = R.cat(Authored("m60.dvg_replay_build.literal_0018"), R.show(R.count(v_simulations__39635)));
            if (R.Bool(R.lt(R.count(v_rows__39786), R.count(v_simulations__39635))))
            {
                v_summary__44593 = R.cat(v_summary__44593, R.cat(Authored("m60.dvg_replay_build.literal_0019"), R.show(R.count(v_rows__39786))));
            }
        }
        return new List<object> { F_dv_pair("summary", v_summary__44593), F_dv_pair("hypotheses", v_rows__39786), F_dv_pair("conflicts", new List<object> {  }), F_dv_pair("predictions", new List<object> {  }), F_dv_pair("total_records", R.count(v_simulations__39635)), F_dv_pair("shown_records", R.count(v_rows__39786)) };
    }
    public static object F_dvg_location_title(object v_location) {
        return DvValueTemplate.Instantiate("template.dvg_location_title", v_location);
    }
    public static object F_dvg_known_room(object v_location) {
        object v_place__45397 = F_dvg_place(v_location);
        return new List<object> { F_dv_pair("map", F_dv_get(v_place__45397, "map")), F_dv_pair("room", F_dv_get(v_place__45397, "room")), F_dv_pair("logical_location", v_location), F_dv_pair("label", F_dvg_location_title(v_location)), F_dv_pair("region", F_campaign_location_region(v_location)) };
    }
    public static object F_dvg_known_room_add(object v_rooms,object v_room) {
        return DvValueTemplate.Instantiate("template.dvg_known_room_add", v_rooms, v_room);
    }
    public static object F_dvg_known_door_add(object v_knowledge,object v_origin,object v_destination) {
        object v_from__45939 = F_dvg_known_room(v_origin);
        object v_to__45978 = F_dvg_known_room(v_destination);
        object v_rooms__46020 = F_dvg_known_room_add(F_dvg_known_room_add(F_dv_get(v_knowledge, "rooms"), v_from__45939), v_to__45978);
        if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_from__45939, "map"), "")) ? (object)(true) : (object)(R.eq(F_dv_get(v_to__45978, "map"), "")))) ? (object)(true) : (object)((R.Bool(R.eq(F_dv_get(v_from__45939, "map"), F_dv_get(v_to__45978, "map"))) ? (object)(R.eq(F_dv_get(v_from__45939, "room"), F_dv_get(v_to__45978, "room"))) : (object)(false))))))
        {
            return F_dv_with(v_knowledge, "rooms", v_rooms__46020);
        }
        object v_i__46243 = R.number("0");
        while (R.Bool(R.lt(v_i__46243, R.count(F_dv_get(v_knowledge, "doors")))))
        {
            object v_door__46304 = R.at(F_dv_get(v_knowledge, "doors"), v_i__46243);
            object v_forward__46343 = (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(F_dv_get(v_door__46304, "from"), "map"), F_dv_get(v_from__45939, "map"))) ? (object)(R.eq(F_dv_get(F_dv_get(v_door__46304, "from"), "room"), F_dv_get(v_from__45939, "room"))) : (object)(false))) ? (object)(R.eq(F_dv_get(F_dv_get(v_door__46304, "to"), "map"), F_dv_get(v_to__45978, "map"))) : (object)(false))) ? (object)(R.eq(F_dv_get(F_dv_get(v_door__46304, "to"), "room"), F_dv_get(v_to__45978, "room"))) : (object)(false));
            object v_reverse__46478 = (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(F_dv_get(v_door__46304, "from"), "map"), F_dv_get(v_to__45978, "map"))) ? (object)(R.eq(F_dv_get(F_dv_get(v_door__46304, "from"), "room"), F_dv_get(v_to__45978, "room"))) : (object)(false))) ? (object)(R.eq(F_dv_get(F_dv_get(v_door__46304, "to"), "map"), F_dv_get(v_from__45939, "map"))) : (object)(false))) ? (object)(R.eq(F_dv_get(F_dv_get(v_door__46304, "to"), "room"), F_dv_get(v_from__45939, "room"))) : (object)(false));
            if (R.Bool((R.Bool(v_forward__46343) ? (object)(true) : (object)(v_reverse__46478))))
            {
                return F_dv_with(v_knowledge, "rooms", v_rooms__46020);
            }
            v_i__46243 = R.add(v_i__46243, R.number("1"));
        }
        return new List<object> { F_dv_pair("rooms", v_rooms__46020), F_dv_pair("doors", F_dv_append(F_dv_get(v_knowledge, "doors"), new List<object> { F_dv_pair("from", v_from__45939), F_dv_pair("to", v_to__45978), F_dv_pair("connection_known", true), F_dv_pair("passage_action_ids", new List<object> {  }) })) };
    }
    public static object F_dvg_known_passage_add(object v_knowledge,object v_origin,object v_destination,object v_action_id) {
        object v_from__46931 = F_dvg_place(v_origin);
        object v_to__46965 = F_dvg_place(v_destination);
        object v_doors__47002 = new List<object> {  };
        object v_i__47022 = R.number("0");
        while (R.Bool(R.lt(v_i__47022, R.count(F_dv_get(v_knowledge, "doors")))))
        {
            object v_door__47083 = R.at(F_dv_get(v_knowledge, "doors"), v_i__47022);
            object v_forward__47122 = (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(F_dv_get(v_door__47083, "from"), "map"), F_dv_get(v_from__46931, "map"))) ? (object)(R.eq(F_dv_get(F_dv_get(v_door__47083, "from"), "room"), F_dv_get(v_from__46931, "room"))) : (object)(false))) ? (object)(R.eq(F_dv_get(F_dv_get(v_door__47083, "to"), "map"), F_dv_get(v_to__46965, "map"))) : (object)(false))) ? (object)(R.eq(F_dv_get(F_dv_get(v_door__47083, "to"), "room"), F_dv_get(v_to__46965, "room"))) : (object)(false));
            object v_reverse__47257 = (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(F_dv_get(v_door__47083, "from"), "map"), F_dv_get(v_to__46965, "map"))) ? (object)(R.eq(F_dv_get(F_dv_get(v_door__47083, "from"), "room"), F_dv_get(v_to__46965, "room"))) : (object)(false))) ? (object)(R.eq(F_dv_get(F_dv_get(v_door__47083, "to"), "map"), F_dv_get(v_from__46931, "map"))) : (object)(false))) ? (object)(R.eq(F_dv_get(F_dv_get(v_door__47083, "to"), "room"), F_dv_get(v_from__46931, "room"))) : (object)(false));
            if (R.Bool((R.Bool(v_forward__47122) ? (object)(true) : (object)(v_reverse__47257))))
            {
                object v_actions__47428 = F_dv_find(v_door__47083, Authored("m60.dvg_known_passage_add.literal"), new List<object> {  });
                if (R.Bool(R.not(F_dv_has(v_actions__47428, v_action_id))))
                {
                    v_actions__47428 = F_dv_append(v_actions__47428, v_action_id);
                }
                v_door__47083 = DvValueTemplate.Instantiate("template.inline.dvg_known_passage_add.1573", v_door__47083, v_actions__47428);
            }
            _ = R.push(v_doors__47002, v_door__47083);
            v_i__47022 = R.add(v_i__47022, R.number("1"));
        }
        return F_dv_with(v_knowledge, "doors", v_doors__47002);
    }
    public static object F_dvg_observed_map_rooms(object v_history,object v_subject,object v_location_ref) {
        object v_key__47996 = DvValueTemplate.Instantiate("template.inline.dvg_observed_map_rooms.1574", v_subject, v_location_ref);
        object v_cached__48110 = R.memo_get(v_history, v_key__47996);
        if (R.Bool(R.at(v_cached__48110, R.number("0"))))
        {
            return R.at(v_cached__48110, R.number("1"));
        }
        object v_rooms__48199 = new List<object> {  };
        object v_i__48219 = R.number("0");
        while (R.Bool(R.lt(v_i__48219, R.count(v_history))))
        {
            object v_batch__48272 = R.at(v_history, v_i__48219);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_observed_map_rooms.1575", v_batch__48272)))
            {
                object v_j__48430 = R.number("0");
                while (R.Bool(R.lt(v_j__48430, R.count(F_dv_get(v_batch__48272, "observations")))))
                {
                    object v_observed__48510 = R.at(F_dv_get(v_batch__48272, "observations"), v_j__48430);
                    if (R.Bool((R.Bool(R.eq(F_dv_get(v_observed__48510, "source"), Authored("m60.dvg_replay_comparison.literal_0004"))) ? (object)(F_dv_same_local_ref(v_subject, F_dv_get(v_observed__48510, "ref"), v_location_ref)) : (object)(false))))
                    {
                        v_rooms__48199 = F_dvg_known_room_add(v_rooms__48199, F_dvg_known_room(F_dv_get(v_observed__48510, "value")));
                    }
                    v_j__48430 = R.add(v_j__48430, R.number("1"));
                }
            }
            v_i__48219 = R.add(v_i__48219, R.number("1"));
        }
        return R.memo_put(v_history, v_key__47996, v_rooms__48199);
    }
    public static object F_dvg_subject_map_knowledge(object v_actor) {
        object v_key__48947 = DvValueTemplate.Instantiate("template.inline.dvg_subject_map_knowledge.1576", v_actor);
        object v_cached__49420 = R.memo_get(F_dv_get(F_dv_get(v_actor, "mind"), "history"), v_key__48947);
        if (R.Bool(R.at(v_cached__49420, R.number("0"))))
        {
            return R.at(v_cached__49420, R.number("1"));
        }
        object v_knowledge__49520 = AuthoredCopy("m60.dvg_subject_map_knowledge.knowledge");
        object v_current__49635 = F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_actor, "location_ref"));
        if (R.Bool(F_dv_uncontested(v_current__49635)))
        {
            v_knowledge__49520 = F_dv_with(v_knowledge__49520, "rooms", F_dvg_known_room_add(F_dv_get(v_knowledge__49520, "rooms"), F_dvg_known_room(F_dv_get(v_current__49635, "value"))));
        }
        object v_observed_rooms__49844 = F_dvg_observed_map_rooms(F_dv_get(F_dv_get(v_actor, "mind"), "history"), F_dv_get(v_actor, "id"), F_dv_get(v_actor, "location_ref"));
        object v_i__49943 = R.number("0");
        while (R.Bool(R.lt(v_i__49943, R.count(v_observed_rooms__49844))))
        {
            v_knowledge__49520 = F_dv_with(v_knowledge__49520, "rooms", F_dvg_known_room_add(F_dv_get(v_knowledge__49520, "rooms"), R.at(v_observed_rooms__49844, v_i__49943)));
            v_i__49943 = R.add(v_i__49943, R.number("1"));
        }
        object v_completed__50123 = F_dv_find(v_actor, Authored("m60.dvg_subject_map_knowledge.literal_0002"), new List<object> {  });
        object v_offered__50246 = F_dv_find(v_actor, Authored("m60.dvg_subject_map_knowledge.literal_0003"), new List<object> {  });
        object v_routes__50367 = F_dvg_preview_action_routes(F_dv_get(v_actor, "actions"));
        v_i__49943 = R.number("0");
        while (R.Bool(R.lt(v_i__49943, R.count(v_routes__50367))))
        {
            object v_route__50474 = R.at(v_routes__50367, v_i__49943);
            if (R.Bool((R.Bool(F_dv_has(v_completed__50123, F_dv_get(v_route__50474, "action_id"))) ? (object)(true) : (object)(F_dv_has(v_offered__50246, F_dv_get(v_route__50474, "action_id"))))))
            {
                v_knowledge__49520 = F_dvg_known_door_add(v_knowledge__49520, F_dv_get(v_route__50474, "from"), F_dv_get(v_route__50474, "to"));
                if (R.Bool(F_dv_has(v_completed__50123, F_dv_get(v_route__50474, "action_id"))))
                {
                    v_knowledge__49520 = F_dvg_known_passage_add(v_knowledge__49520, F_dv_get(v_route__50474, "from"), F_dv_get(v_route__50474, "to"), F_dv_get(v_route__50474, "action_id"));
                }
            }
            v_i__49943 = R.add(v_i__49943, R.number("1"));
        }
        return R.memo_put(F_dv_get(F_dv_get(v_actor, "mind"), "history"), v_key__48947, v_knowledge__49520);
    }
    public static object F_dvg_map_knowledge(object v_state,object v_viewer,object v_intent) {
        object v_i__50942 = R.number("0");
        while (R.Bool(R.lt(v_i__50942, R.count(F_dv_get(v_state, "actors")))))
        {
            object v_actor__51000 = R.at(F_dv_get(v_state, "actors"), v_i__50942);
            if (R.Bool(R.eq(F_dv_get(v_actor__51000, "id"), v_viewer)))
            {
                object v_knowledge__51073 = F_dvg_subject_map_knowledge(v_actor__51000);
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_map_knowledge.1577", v_intent, v_state, v_i__50942)))
                {
                    object v_j__51257 = R.number("0");
                    while (R.Bool(R.lt(v_j__51257, R.count(F_dv_get(v_intent, "options")))))
                    {
                        object v_action__51341 = F_dvp_find_action(F_dv_get(v_actor__51000, "actions"), F_dv_get(R.at(F_dv_get(v_intent, "options"), v_j__51257), "id"));
                        object v_travel__51428 = F_dv_find(v_action__51341, "travel", new List<object> {  });
                        if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_action__51341, "kind"), "move")) ? (object)(F_dv_has_key(v_travel__51428, "from")) : (object)(false))) ? (object)(F_dv_has_key(v_travel__51428, "to")) : (object)(false))))
                        {
                            v_knowledge__51073 = F_dvg_known_door_add(v_knowledge__51073, F_dv_get(v_travel__51428, "from"), F_dv_get(v_travel__51428, "to"));
                        }
                        v_j__51257 = R.add(v_j__51257, R.number("1"));
                    }
                }
                return v_knowledge__51073;
            }
            v_i__50942 = R.add(v_i__50942, R.number("1"));
        }
        return AuthoredCopy("m60.dvg_subject_map_knowledge.knowledge");
    }
    public static object F_dvg_snapshot(object v_state,object v_viewer) {
        return F_dvg_snapshot_with_intent(v_state, v_viewer, F_dvg_intent(v_state));
    }
    public static object F_dvg_snapshot_location(object v_state,object v_viewer) {
        object v_position__52169 = F_dv_read(F_dv_get(v_state, "physical"), F_campaign_position_ref(v_viewer));
        if (R.Bool(F_dv_uncontested(v_position__52169)))
        {
            return F_dv_with(F_dvg_place(F_dv_get(v_position__52169, "value")), "logical_location", F_dv_get(v_position__52169, "value"));
        }
        return AuthoredCopy("m60.dvg_snapshot_location.return");
    }
    public static object F_dvg_snapshot_static(object v_state,object v_viewer) {
        object v_key__52593 = DvValueTemplate.Instantiate("template.inline.dvg_snapshot_static.1578", v_viewer);
        object v_cached__52689 = R.memo_get(new List<object> {  }, v_key__52593);
        if (R.Bool(R.at(v_cached__52689, R.number("0"))))
        {
            return R.at(v_cached__52689, R.number("1"));
        }
        return R.memo_put(new List<object> {  }, v_key__52593, new List<object> { F_dv_pair("view_kind", "world"), F_dv_pair("field_enabled", true), F_dv_pair("initial_placements", new List<object> {  }), F_dv_pair("presentation", new List<object> { F_dv_pair("player_actor", v_viewer) }) });
    }

    public static object F_dvg_snapshot_dynamic(object v_state,object v_viewer,object v_intent) {
        object v_place__53241 = F_dvg_snapshot_location(v_state, v_viewer);
        object v_snapshot__53295 = new List<object> { DvValueTemplate.Instantiate("template.inline.dvg_snapshot_dynamic.1579", v_state), F_dv_pair("game_time", F_dv_get(F_dv_get(v_state, "physical"), "time")), F_dv_pair("active", F_dvs_session_active(v_state)), F_dv_pair("viewer", v_viewer), F_dv_pair("map", F_dv_get(v_place__53241, "map")), F_dv_pair("room", F_dv_get(v_place__53241, "room")), F_dv_pair("execution_room", F_dv_get(v_place__53241, "room")), F_dv_pair("logical_location", F_dv_get(v_place__53241, "logical_location")), F_dv_pair("initial_anchor", F_dvg_initial_anchor(v_viewer, F_dv_get(v_place__53241, "logical_location"))), DvValueTemplate.Instantiate("template.inline.dvg_snapshot_dynamic.1580", v_place__53241), F_dv_pair("anchor", F_dv_find(v_place__53241, "anchor", "")), DvValueTemplate.Instantiate("template.inline.dvg_snapshot_dynamic.1581", v_state, v_viewer), F_dv_pair("location_title", F_dvg_location_title(F_dv_get(v_place__53241, "logical_location"))), F_dv_pair("map_knowledge", F_dvg_map_knowledge(v_state, v_viewer, v_intent)), F_dv_pair("access", F_dvg_host_access(v_state, F_dv_get(v_place__53241, "map"))), F_dv_pair("traces", F_dvg_traces(F_dv_get(v_state, "physical"))), F_dv_pair("people", F_dvg_people(v_state, v_viewer)), F_dv_pair("pending", v_intent), F_dv_pair("chapter_summary", F_dvg_chapter_summary(v_state)), F_dv_pair("replay", F_dvg_replay(v_state, v_viewer)) };
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_snapshot_dynamic.1582", v_state)))
        {
            return DvValueTemplate.Instantiate("template.inline.dvg_snapshot_dynamic.1583", v_snapshot__53295, v_state);
        }
        return v_snapshot__53295;
    }
    public static object F_dvg_executor_snapshot(object v_state,object v_actor,object v_intent) {
        object v_place__54947 = F_dvg_snapshot_location(v_state, v_actor);
        return F_dv_with_many(F_dvg_snapshot_static(v_state, v_actor), new List<object> { DvValueTemplate.Instantiate("template.inline.dvg_executor_snapshot.1584", v_state), F_dv_pair("game_time", F_dv_get(F_dv_get(v_state, "physical"), "time")), F_dv_pair("active", F_dvs_session_active(v_state)), F_dv_pair("viewer", v_actor), F_dv_pair("map", F_dv_get(v_place__54947, "map")), F_dv_pair("room", F_dv_get(v_place__54947, "room")), F_dv_pair("execution_room", F_dv_get(v_place__54947, "room")), F_dv_pair("logical_location", F_dv_get(v_place__54947, "logical_location")), F_dv_pair("initial_anchor", F_dvg_initial_anchor(v_actor, F_dv_get(v_place__54947, "logical_location"))), DvValueTemplate.Instantiate("template.inline.dvg_executor_snapshot.1585", v_place__54947), F_dv_pair("anchor", F_dv_find(v_place__54947, "anchor", "")), F_dv_pair("player_control", false), F_dv_pair("access", F_dvg_host_access(v_state, F_dv_get(v_place__54947, "map"))), F_dv_pair("people", F_dvg_people(v_state, v_actor)), F_dv_pair("pending", v_intent) });
    }
    public static object F_dvg_snapshot_with_intent(object v_state,object v_viewer,object v_intent) {
        return F_dv_with_many(F_dvg_snapshot_static(v_state, v_viewer), F_dvg_snapshot_dynamic(v_state, v_viewer, v_intent));
    }
    public static object F_dvg_recorded_snapshot(object v_record,object v_viewer) {
        object v_place__56065 = F_dvg_snapshot_location(v_record, v_viewer);
        object v_snapshot__56120 = new List<object> { F_dv_pair("game_time", F_dv_get(F_dv_get(v_record, "physical"), "time")), F_dv_pair("active", false), F_dv_pair("viewer", v_viewer), F_dv_pair("map", F_dv_get(v_place__56065, "map")), F_dv_pair("room", F_dv_get(v_place__56065, "room")), F_dv_pair("execution_room", F_dv_get(v_place__56065, "room")), F_dv_pair("logical_location", F_dv_get(v_place__56065, "logical_location")), F_dv_pair("initial_anchor", F_dvg_initial_anchor(v_viewer, F_dv_get(v_place__56065, "logical_location"))), DvValueTemplate.Instantiate("template.inline.dvg_recorded_snapshot.1586", v_place__56065), F_dv_pair("anchor", F_dv_find(v_place__56065, "anchor", "")), F_dv_pair("player_control", false), F_dv_pair("location_title", F_dvg_location_title(F_dv_get(v_place__56065, "logical_location"))), DvValueTemplate.Instantiate("template.inline.dvg_recorded_snapshot.1587"), F_dv_pair("access", F_dvg_host_access(v_record, F_dv_get(v_place__56065, "map"))), F_dv_pair("traces", F_dvg_traces(F_dv_get(v_record, "physical"))), F_dv_pair("people", F_dvg_people(v_record, v_viewer)), DvValueTemplate.Instantiate("template.inline.dvg_recorded_snapshot.1588"), F_dv_pair("chapter_summary", false), DvValueTemplate.Instantiate("template.inline.dvg_recorded_snapshot.1589") };
        return F_dv_with_many(F_dvg_snapshot_static(v_record, v_viewer), v_snapshot__56120);
    }
    public static object F_dvg_chapter_summary(object v_state) {
        return DvValueTemplate.Instantiate("template.dvg_chapter_summary", v_state);
    }

    public static object F_dvg_configure_actor_policies(object v_scene,object v_config) {
        object v_actors__64844 = new List<object> {  };
        object v_i__64865 = R.number("0");
        while (R.Bool(R.lt(v_i__64865, R.count(F_dv_get(v_scene, "actors")))))
        {
            object v_actor__64923 = R.at(F_dv_get(v_scene, "actors"), v_i__64865);
            object v_actions__64960 = F_dv_get(v_actor__64923, "actions");
            object v_initial_profile__64997 = F_dv_find(F_dv_find(v_actor__64923, Authored("m60.dvg_configure_actor_policies.literal"), new List<object> {  }), Authored("m60.dvg_run_config.literal_0003"), new List<object> {  });
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_configure_actor_policies.1590", v_actor__64923, v_initial_profile__64997)))
            {
                v_actions__64960 = new List<object> {  };
                object v_j__65483 = R.number("0");
                while (R.Bool(R.lt(v_j__65483, R.count(F_dv_get(v_actor__64923, "actions")))))
                {
                    object v_action__65558 = R.at(F_dv_get(v_actor__64923, "actions"), v_j__65483);
                    object v_speech__65605 = F_dv_find(v_action__65558, "speech", new List<object> {  });
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_configure_actor_policies.1591", v_action__65558, v_speech__65605)))
                    {
                        v_action__65558 = DvValueTemplate.Instantiate("template.inline.dvg_configure_actor_policies.1592", v_action__65558);
                    }
                    _ = R.push(v_actions__64960, v_action__65558);
                    v_j__65483 = R.add(v_j__65483, R.number("1"));
                }
            }
            object v_sims_profile__66125 = F_sims_actor_profile(F_dv_get(v_actor__64923, "id"));
            object v_character__66182 = R.content("characters", F_dv_get(v_actor__64923, "id"));
            if (R.Bool(R.not(R.eq(v_character__66182, false))))
            {
                v_actor__64923 = F_dv_apply_content_policy(v_actor__64923, v_character__66182, false);
            }
            _ = R.push(v_actors__64844, F_dv_with_many(v_actor__64923, new List<object> { F_dv_pair("actions", v_actions__64960), F_dv_pair("policy", F_dv_with_many(F_dv_get(v_actor__64923, "policy"), new List<object> { F_dv_pair("sims_autonomy", v_sims_profile__66125), F_dv_pair("simulation_mode", F_dv_get(v_config, "simulation_mode")), F_dv_pair("private_execution_benefit_multiplier", F_dv_get(v_config, "private_execution_benefit_multiplier")), F_dv_pair("homecoming_negotiation", F_dv_get(v_config, "homecoming_negotiation")), F_dv_pair("opt_readview", F_dv_get(v_config, "opt_readview")), F_dv_pair("opt_plancache", F_dv_get(v_config, "opt_plancache")), F_dv_pair("opt_replaycache", F_dv_get(v_config, "opt_replaycache")), F_dv_pair("opt_lazytext", F_dv_get(v_config, "opt_lazytext")), F_dv_pair("opt_recordcache", F_dv_get(v_config, "opt_recordcache")), F_dv_pair("opt_predicate", F_dv_get(v_config, "opt_predicate")), F_dv_pair("opt_predicatecache", F_dv_get(v_config, "opt_predicatecache")), F_dv_pair("opt_declcache", F_dv_get(v_config, "opt_declcache")), F_dv_pair("opt_dirty", F_dv_get(v_config, "opt_dirty")), F_dv_pair("opt_dirty_verify", F_dv_get(v_config, "opt_dirty_verify")), F_dv_pair("opt_blackbox", F_dv_get(v_config, "opt_blackbox")), F_dv_pair("opt_storyflow", F_dv_get(v_config, "opt_storyflow")), F_dv_pair("opt_stateindex", F_dv_get(v_config, "opt_stateindex")) })) }));
            v_i__64865 = R.add(v_i__64865, R.number("1"));
        }
        object v_simulation__67202 = F_dv_get(v_config, "simulation");
        if (R.Bool(R.eq(v_simulation__67202, false)))
        {
            v_simulation__67202 = F_campaign_simulation_config(v_scene);
        }
        return F_dv_with_many(v_scene, new List<object> { F_dv_pair("actors", v_actors__64844), F_dv_pair("run_config", v_config), F_dv_pair("simulation", v_simulation__67202) });
    }

    public static object F_dvg_open_config(object v_config) {
        object v_run_config__70263 = F_dvg_run_config(v_config);
        ConfigureCoreRuntime(v_run_config__70263);
        object v_scene__70308 = F_c1n_install_scene(F_c1dep_install_scene(F_c1_campaign_scene()));
        v_scene__70308 = F_dvg_configure_initial_room(v_scene__70308, v_run_config__70263);
        object v_zone__70444 = F_dv_find(v_config, "detective_zone", Authored("m60.dvg_run_config.literal_0005"));
        object v_profile__70566 = F_dv_find(v_config, Authored("m60.dvg_run_config.literal_0003"), Authored("m60.dvg_run_config.literal_0004"));
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_open_config.1593", v_zone__70444)))
        {
            v_scene__70308 = F_c1_other_place_detective(v_scene__70308, v_zone__70444, v_profile__70566);
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_open_config.1594", v_zone__70444, v_config)))
        {
            v_scene__70308 = F_c1_unseen_dormitories_simplify(v_scene__70308);
        }
        v_scene__70308 = F_c1c_install_scene(v_scene__70308, F_dv_find(v_config, "seed", R.number("1")), v_profile__70566);
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_open_config.1595", v_zone__70444)))
        {
            v_scene__70308 = F_dvg_without_detective(v_scene__70308);
        }
        object v_campaign_minutes__71854 = R.sub(R.mul(Authored("m60.dvg_interaction.literal_0004"), Authored("m60.dvg_open_config.literal_0004")), F_dv_get(F_dv_get(v_scene__70308, "physical"), "time"));
        v_scene__70308 = DvValueTemplate.Instantiate("template.inline.dvg_open_config.1596", v_scene__70308, v_campaign_minutes__71854);
        v_scene__70308 = F_dvg_configure_actor_policies(F_island_install(v_scene__70308, F_dvg_island_locations()), v_run_config__70263);
        v_scene__70308 = F_campaign_install_visible_objects(F_dv_with(v_scene__70308, "physical", F_c1_add_objects(F_dv_get(v_scene__70308, "physical"), F_c1_visible_site_objects())));
        return F_dvs_session_open(F_c1_player_scene(v_scene__70308), Authored("m60.dvg_open_config.literal_0006"));
    }
    public static object F_dvg_chapter_two_ready(object v_state) {
        return DvValueTemplate.Instantiate("template.dvg_chapter_two_ready", v_state);
    }
    public static object F_dvg_extend(object v_state) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_extend.1597", v_state)))
        {
            return v_state;
        }
        object v_live__72933 = F_dv_with_many(F_dv_get(v_state, "story"), new List<object> { F_dv_pair("physical", F_dv_get(v_state, "physical")), F_dv_pair("actors", F_dv_get(v_state, "actors")), F_dv_pair("bindings", F_dv_get(v_state, "bindings")) });
        object v_expanded__73057 = F_campaign_install_visible_objects(F_dvg_configure_actor_policies(F_island_case_install(F_island_install(F_dv_with(F_c2h_install_scene(F_c2daily_install_scene(F_c2d_install_scene(F_c2u_install_scene(F_c2_extend_scene(v_live__72933))))), Authored("m60.dvg_snapshot_dynamic.literal"), Authored("m60.dvg_replay_comparison.literal_0006")), F_dvg_island_locations())), F_dvg_run_config(F_dv_find(F_dv_get(v_state, "story"), Authored("m60.dvg_presentation.literal"), new List<object> {  }))));
        object v_people__73576 = new List<object> {  };
        object v_identity_index__73597 = R.number("0");
        while (R.Bool(R.lt(v_identity_index__73597, R.count(F_dv_get(v_expanded__73057, "actors")))))
        {
            _ = R.push(v_people__73576, F_dvg_persistent_identity_actions(R.at(F_dv_get(v_expanded__73057, "actors"), v_identity_index__73597)));
            v_identity_index__73597 = R.add(v_identity_index__73597, R.number("1"));
        }
        v_expanded__73057 = F_dv_with(v_expanded__73057, "actors", v_people__73576);
        object v_events__73868 = F_dv_clone(F_dv_get(v_state, "events"));
        _ = F_dvs_event(v_events__73868, Authored("m60.dvg_extend.literal"), "", F_dv_get(F_dv_get(v_state, "physical"), "time"), new List<object> {  }, DvValueTemplate.Instantiate("template.inline.dvg_extend.1598"));
        return F_dvs_owned_state_update(v_state, DvValueTemplate.Instantiate("template.inline.dvg_extend.1599", v_expanded__73057, v_events__73868, v_state));
    }
    public static object F_dvg_refresh_goals(object v_current) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_refresh_goals.1600", v_current)))
        {
            v_current = R.snapshot(v_current);
        }
        object v_actors__75079 = F_dv_get(v_current, "actors");
        object v_i__75112 = F_dv_get(v_current, "cursor");
        object v_changed__75140 = false;
        object v_chapter__75165 = F_dv_find(F_dv_get(v_current, "story"), Authored("m60.dvg_snapshot_dynamic.literal"), R.number("1"));
        while (R.Bool(R.lt(v_i__75112, R.add(F_dv_get(v_current, "cursor"), R.number("1")))))
        {
            object v_actor__75401 = R.at(F_dv_get(v_current, "actors"), v_i__75112);
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_refresh_goals.1601", v_actor__75401, v_chapter__75165)))
            {
                object v_refreshed__76029 = v_actor__75401;
                if (R.Bool(R.eq(v_chapter__75165, R.number("1"))))
                {
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_refresh_goals.1602", v_actor__75401)))
                    {
                        v_refreshed__76029 = F_c1n_refresh_actor_goals(v_actor__75401);
                    }
                    else
                    {
                        v_refreshed__76029 = F_c1n_known_future_wait_actor(v_actor__75401);
                    }
                    v_actors__75079 = F_dvs_replace(v_actors__75079, v_i__75112, F_dv_with_many(v_refreshed__76029, new List<object> { F_dv_pair("campaign_goals_revision", F_dv_get(F_dv_get(v_actor__75401, "mind"), "revision")), F_dv_pair("campaign_goals_time", F_dv_get(F_dv_get(v_actor__75401, "mind"), "time")), F_dv_pair("campaign_goals_chapter", v_chapter__75165) }));
                }
                else
                {
                    object v_needs_c1_wait__76577 = (R.Bool((R.Bool(R.eq(F_dv_get(v_actor__75401, "id"), Authored("m60.dvg_refresh_goals.literal_0004"))) ? (object)(R.not(R.eq(F_dv_truth(F_dv_get(v_actor__75401, "mind"), F_campaign_case_claim(Authored("m60.dvg_refresh_goals.literal_0005"), Authored("m60.dvg_refresh_goals.literal_0006"))), "true"))) : (object)(false))) ? (object)(true) : (object)((R.Bool(R.eq(F_dv_get(v_actor__75401, "id"), Authored("m60.dvg_refresh_goals.literal_0007"))) ? (object)(R.not(R.eq(F_dv_truth(F_dv_get(v_actor__75401, "mind"), F_campaign_case_claim(Authored("m60.dvg_refresh_goals.literal_0008"), Authored("m60.dvg_refresh_goals.literal_0009"))), "true"))) : (object)(false))));
                    object v_in_living__77205 = R.eq(F_dv_truth(F_dv_get(v_actor__75401, "mind"), F_campaign_at(F_dv_get(v_actor__75401, "id"), Authored("m60.dvg_map_access.literal_0003"))), "true");
                    object v_case_open__77366 = R.eq(F_dv_truth(F_dv_get(v_actor__75401, "mind"), F_c2d_case_gate(F_dv_get(v_actor__75401, "mind"))), "true");
                    v_refreshed__76029 = F_c2d_refresh_actor_base_goals(v_actor__75401);
                    R.phase("c2-goals-homecoming-start", "");
                    try { v_refreshed__76029 = F_c2h_refresh_actor(v_refreshed__76029); }
                    finally { R.phase("c2-goals-homecoming-end", ""); }
                    R.phase("c2-goals-identity-start", "");
                    try { v_refreshed__76029 = F_dvg_persistent_identity_actions(v_refreshed__76029); }
                    finally { R.phase("c2-goals-identity-end", ""); }
                    R.phase("c2-goals-wait-start", "");
                    try { v_refreshed__76029 = F_c1n_known_future_wait_actor(v_refreshed__76029); }
                    finally { R.phase("c2-goals-wait-end", ""); }
                    v_actors__75079 = F_dvs_replace(v_actors__75079, v_i__75112, F_dv_with_many(v_refreshed__76029, new List<object> { F_dv_pair("campaign_goals_revision", F_dv_get(F_dv_get(v_actor__75401, "mind"), "revision")), F_dv_pair("campaign_goals_time", F_dv_get(F_dv_get(v_actor__75401, "mind"), "time")), F_dv_pair("campaign_goals_chapter", v_chapter__75165), F_dv_pair("c2_goals_in_living", v_in_living__77205), F_dv_pair("c2_goals_case_open", v_case_open__77366), F_dv_pair("c2_goals_needs_wait", v_needs_c1_wait__76577) }));
                }
                v_changed__75140 = true;
            }
            v_i__75112 = R.add(v_i__75112, R.number("1"));
        }
        if (R.Bool(v_changed__75140))
        {
            v_current = F_dvs_owned_state_update(v_current, new List<object> { F_dv_pair("actors", v_actors__75079) });
        }
        R.phase("goals-island-case-start", "");
        try { return F_island_case_refresh(v_current); }
        finally { R.phase("goals-island-case-end", ""); }
    }

    public static object F_dvg_dev_result(object v_state,object v_command,object v_status,object v_message,object v_details) {
        object v_result__83588 = F_dv_with_many(new List<object> { F_dv_pair("status", v_status), F_dv_pair("command", v_command), F_dv_pair("message", v_message), F_dv_pair("development", true), F_dv_pair("game_time", F_dv_get(F_dv_get(v_state, "physical"), "time")) }, v_details);
        return F_dvs_owned_state_update(v_state, DvValueTemplate.Instantiate("template.inline.dvg_dev_result.1603", v_state, v_result__83588));
    }
    public static object F_dvg_dev_actor_index(object v_state,object v_actor) {
        return DvValueTemplate.Instantiate("template.dvg_dev_actor_index", v_state, v_actor);
    }
    public static object F_dvg_dev_next_due(object v_state,object v_target) {
        object v_index__84135 = R.sub(R.number("0"), R.number("1"));
        object v_due__84155 = R.add(v_target, R.number("1"));
        object v_i__84181 = R.number("0");
        while (R.Bool(R.lt(v_i__84181, R.count(F_dv_get(v_state, "actors")))))
        {
            object v_queued__84239 = F_dv_find(R.at(F_dv_get(v_state, "actors"), v_i__84181), Authored("m60.dvg_person_motion.literal"), false);
            if (R.Bool(R.not(R.eq(v_queued__84239, false))))
            {
                if (R.Bool(R.lt(F_dv_get(v_queued__84239, "due"), v_due__84155)))
                {
                    v_index__84135 = v_i__84181;
                    v_due__84155 = F_dv_get(v_queued__84239, "due");
                }
            }
            v_i__84181 = R.add(v_i__84181, R.number("1"));
        }
        return new List<object> { F_dv_pair("index", v_index__84135), F_dv_pair("time", v_due__84155) };
    }
    public static object F_dvg_dev_restore_input(object v_state,object v_original,object v_viewer_index) {
        object v_restored__84604 = F_dvs_owned_state_update(v_state, DvValueTemplate.Instantiate("template.inline.dvg_dev_restore_input.1604", v_state, v_viewer_index, v_original));
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_dev_restore_input.1605", v_original)))
        {
            return F_dvs_prepare_actor(F_dvg_refresh_goals(v_restored__84604));
        }
        return v_restored__84604;
    }
    public static object F_dvg_dev_time(object v_state,object v_minutes,object v_viewer,object v_viewer_index) {
        object v_target__85140 = R.add(F_dv_get(F_dv_get(v_state, "physical"), "time"), v_minutes);
        if (R.Bool(R.lt(v_minutes, R.number("0"))))
        {
            return F_dvg_dev_result(v_state, "time", Authored("m60.dvg_presentation.literal_0010"), Authored("m60.dvg_dev_time.literal"), new List<object> {  });
        }
        if (R.Bool(R.lt(F_dv_get(v_state, "horizon"), v_target__85140)))
        {
            return F_dvg_dev_result(v_state, "time", Authored("m60.dvg_presentation.literal_0010"), Authored("m60.dvg_dev_time.literal_0002"), new List<object> { F_dv_pair("horizon", F_dv_get(v_state, "horizon")), F_dv_pair("requested_time", v_target__85140) });
        }
        object v_events__85674 = F_dv_get(v_state, "events");
        object v_command_event__85705 = F_dvs_event(v_events__85674, Authored("m60.dvg_dev_time.literal_0003"), v_viewer, F_dv_get(F_dv_get(v_state, "physical"), "time"), new List<object> {  }, DvValueTemplate.Instantiate("template.inline.dvg_dev_time.1606", v_minutes, v_target__85140));
        object v_current__86013 = F_dvs_owned_state_update(v_state, new List<object> { F_dv_pair("events", v_events__85674), F_dv_pair("cursor", v_viewer_index), F_dv_pair("awaiting_player", true), F_dv_pair("player_selection", ""), F_dv_pair("idle", R.number("0")) });
        object v_running__86166 = true;
        while (R.Bool(v_running__86166))
        {
            object v_due__86214 = F_dvg_dev_next_due(v_current__86013, v_target__85140);
            if (R.Bool((R.Bool(R.not(R.lt(F_dv_get(F_dv_get(v_current__86013, "physical"), "time"), v_target__85140))) ? (object)((R.Bool(R.lt(F_dv_get(v_due__86214, "index"), R.number("0"))) ? (object)(true) : (object)(R.lt(v_target__85140, F_dv_get(v_due__86214, "time"))))) : (object)(false))))
            {
                v_running__86166 = false;
            }
            else
            {
                if (R.Bool((R.Bool(R.not(R.lt(F_dv_get(v_due__86214, "index"), R.number("0")))) ? (object)(R.not(R.lt(F_dv_get(F_dv_get(v_current__86013, "physical"), "time"), F_dv_get(v_due__86214, "time")))) : (object)(false))))
                {
                    if (R.Bool(R.not(R.lt(F_dv_get(v_current__86013, "executed"), F_dv_get(v_current__86013, "limit")))))
                    {
                        return F_dvg_dev_result(F_dvg_dev_restore_input(v_current__86013, v_state, v_viewer_index), "time", Authored("m60.dvg_dev_time.literal_0005"), Authored("m60.dvg_dev_time.literal_0006"), new List<object> { F_dv_pair("requested_time", v_target__85140), F_dv_pair("resync", true) });
                    }
                    object v_pending_state__86818 = F_dvs_dispatch_due(v_current__86013, F_dv_get(v_due__86214, "index"));
                    object v_pending__86892 = F_dv_find(v_pending_state__86818, "pending", false);
                    if (R.Bool(R.not(R.eq(v_pending__86892, false))))
                    {
                        object v_receipt__87006 = DvValueTemplate.Instantiate("template.inline.dvg_dev_time.1607", v_pending__86892, v_command_event__85705);
                        v_current__86013 = F_dvg_extend(F_dvs_session_complete(v_pending_state__86818, v_receipt__87006));
                        v_current__86013 = F_dvs_owned_state_update(v_current__86013, new List<object> { F_dv_pair("story", F_dv_without(F_dv_get(v_current__86013, "story"), "background_waiting_cursor")), F_dv_pair("cursor", v_viewer_index), F_dv_pair("awaiting_player", true), F_dv_pair("player_selection", "") });
                    }
                    else
                    {
                        v_current__86013 = F_dvs_owned_state_update(v_pending_state__86818, new List<object> { F_dv_pair("cursor", v_viewer_index), F_dv_pair("awaiting_player", true) });
                    }
                }
                else
                {
                    object v_event_count__87781 = R.count(F_dv_get(v_current__86013, "events"));
                    object v_planned__87841 = v_current__86013;
                    if (R.Bool(R.lt(F_dv_get(v_current__86013, "executed"), F_dv_get(v_current__86013, "limit"))))
                    {
                        v_planned__87841 = F_dvg_prepare_background(v_current__86013, v_viewer);
                    }
                    if (R.Bool(R.not(R.eq(F_dv_find(v_planned__87841, "pending", false), false))))
                    {
                        object v_pending__88074 = F_dv_get(v_planned__87841, "pending");
                        object v_receipt__88125 = DvValueTemplate.Instantiate("template.inline.dvg_dev_time.1608", v_pending__88074, v_command_event__85705);
                        v_current__86013 = F_dvg_extend(F_dvs_session_complete(v_planned__87841, v_receipt__88125));
                        v_current__86013 = F_dvs_owned_state_update(v_current__86013, new List<object> { F_dv_pair("story", F_dv_without(F_dv_get(v_current__86013, "story"), "background_waiting_cursor")), F_dv_pair("cursor", v_viewer_index), F_dv_pair("awaiting_player", true), F_dv_pair("player_selection", "") });
                    }
                    else
                    {
                        v_current__86013 = v_planned__87841;
                        if (R.Bool(R.eq(R.count(F_dv_get(v_current__86013, "events")), v_event_count__87781)))
                        {
                            object v_next_due__88867 = F_dvg_dev_next_due(v_current__86013, v_target__85140);
                            object v_boundary__88941 = v_target__85140;
                            if (R.Bool(R.not(R.lt(F_dv_get(v_next_due__88867, "index"), R.number("0")))))
                            {
                                v_boundary__88941 = F_dv_min(v_boundary__88941, F_dv_get(v_next_due__88867, "time"));
                            }
                            object v_next_time__89109 = F_dvs_environment_next_time(F_dv_get(v_current__86013, "physical"), F_dv_get(v_current__86013, "story"), v_boundary__88941);
                            object v_next_events__89378 = F_dv_clone(F_dv_get(v_current__86013, "events"));
                            object v_physical__89446 = F_dvs_environment(F_dv_with(F_dv_get(v_current__86013, "physical"), "time", v_next_time__89109), F_dv_get(v_current__86013, "story"), v_next_events__89378, v_command_event__85705);
                            v_current__86013 = F_dvg_extend(F_dvs_owned_state_update(v_current__86013, new List<object> { F_dv_pair("physical", v_physical__89446), F_dv_pair("events", v_next_events__89378), F_dv_pair("idle", R.number("0")) }));
                        }
                    }
                }
            }
        }
        v_current__86013 = F_dvg_dev_restore_input(v_current__86013, v_state, v_viewer_index);
        return F_dvg_dev_result(v_current__86013, "time", Authored("m60.dvg_presentation.literal_0008"), Authored("m60.dvg_dev_time.literal_0007"), new List<object> { F_dv_pair("requested_time", v_target__85140), F_dv_pair("resync", true), F_dv_pair("command_event", v_command_event__85705) });
    }
    public static object F_dvg_dev_teleport(object v_state,object v_target,object v_viewer,object v_index) {
        object v_place__90093 = F_dvg_place(v_target);
        if (R.Bool(R.eq(F_dv_get(v_place__90093, "map"), "")))
        {
            return F_dvg_dev_result(v_state, Authored("m60.dvg_dev_teleport.literal"), Authored("m60.dvg_presentation.literal_0010"), Authored("m60.dvg_dev_teleport.literal_0002"), new List<object> { F_dv_pair("target", v_target) });
        }
        object v_actor__90428 = R.at(F_dv_get(v_state, "actors"), v_index);
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_dev_teleport.1609", v_actor__90428)))
        {
            return F_dvg_dev_result(v_state, Authored("m60.dvg_dev_teleport.literal"), Authored("m60.dvg_presentation.literal_0010"), Authored("m60.dvg_dev_teleport.literal_0003"), new List<object> {  });
        }
        object v_events__90834 = F_dv_get(v_state, "events");
        object v_event__90865 = F_dvs_event(v_events__90834, Authored("m60.dvg_dev_time.literal_0003"), v_viewer, F_dv_get(F_dv_get(v_state, "physical"), "time"), new List<object> {  }, DvValueTemplate.Instantiate("template.inline.dvg_dev_teleport.1610", v_target));
        object v_ref__91124 = F_dv_get(v_actor__90428, "location_ref");
        object v_source__91158 = DvValueTemplate.Instantiate("template.inline.dvg_dev_teleport.1611", v_event__90865);
        object v_law_id__91364 = R.cat(Authored("m60.dvg_dev_teleport.literal_0006"), v_event__90865);
        object v_law__91470 = DvValueTemplate.Instantiate("template.inline.dvg_dev_teleport.1612", v_law_id__91364, v_ref__91124, v_target);
        object v_adopted__91542 = F_dv_adopt(F_dv_get(v_state, "physical"), v_law__91470, "enforce", R.number("1"), v_source__91158);
        object v_execution__91613 = F_dv_execute(v_adopted__91542, v_law_id__91364, v_law_id__91364, F_dv_get(v_adopted__91542, "time"), F_dv_get(v_adopted__91542, "revision"));
        object v_physical__91702 = F_dv_suspend(F_dv_get(v_execution__91613, "world"), v_law_id__91364);
        object v_observations__91758 = F_dv_perceive(v_physical__91702, F_dv_get(v_actor__90428, "mind"), F_dv_get(v_state, "bindings"), new List<object> { v_ref__91124 }, v_event__90865, F_dv_get(v_physical__91702, "time"));
        object v_mind__91861 = F_dv_get(F_dv_update_mind(F_dv_get(v_actor__90428, "mind"), v_observations__91758, F_dv_get(F_dv_get(v_actor__90428, "mind"), "revision"), F_dvs_observation_profile(v_actor__90428)), "world");
        _ = F_dvs_event(v_events__90834, "observation", F_dv_get(v_actor__90428, "id"), F_dv_get(v_physical__91702, "time"), new List<object> { v_event__90865 }, new List<object> { F_dv_pair("observations", v_observations__91758), F_dv_pair("revision_before", F_dv_get(F_dv_get(v_actor__90428, "mind"), "revision")), F_dv_pair("revision_after", F_dv_get(v_mind__91861, "revision")), F_dv_pair("bindings", F_dv_get(v_state, "bindings")), F_dv_pair("grants", new List<object> { v_ref__91124 }), F_dv_pair("execution_id", v_law_id__91364), F_dv_pair("development", true) });
        object v_relocated__92244 = DvValueTemplate.Instantiate("template.inline.dvg_dev_teleport.1613", v_actor__90428, v_mind__91861, v_target);
        object v_current__92428 = F_dvs_owned_state_update(v_state, new List<object> { F_dv_pair("physical", v_physical__91702), F_dv_pair("actors", F_dvs_replace(F_dv_get(v_state, "actors"), v_index, v_relocated__92244)), F_dv_pair("events", v_events__90834), F_dv_pair("idle", R.number("0")) });
        return F_dvg_dev_result(F_dvg_dev_restore_input(v_current__92428, v_state, v_index), Authored("m60.dvg_dev_teleport.literal"), Authored("m60.dvg_presentation.literal_0008"), Authored("m60.dvg_dev_teleport.literal_0008"), new List<object> { F_dv_pair("target", v_target), F_dv_pair("resync", true), F_dv_pair("command_event", v_event__90865) });
    }
    public static object F_dvg_dev_command(object v_state,object v_command,object v_argument) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_dev_command.1614", v_state)))
        {
            return F_dvg_dev_result(v_state, v_command, Authored("m60.dvg_presentation.literal_0010"), Authored("m60.dvg_dev_command.literal"), new List<object> {  });
        }
        if (R.Bool(R.not(R.eq(F_dv_find(v_state, "pending", false), false))))
        {
            return F_dvg_dev_result(v_state, v_command, Authored("m60.dvg_presentation.literal_0010"), Authored("m60.dvg_dev_command.literal_0002"), new List<object> {  });
        }
        v_state = F_dvs_owned_state_update(v_state, DvValueTemplate.Instantiate("template.inline.dvg_dev_command.1615", v_state));
        object v_viewer__93799 = F_dvg_host_viewer(v_state);
        object v_index__93840 = F_dvg_dev_actor_index(v_state, v_viewer__93799);
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_dev_command.1616", v_command)))
        {
            return F_dvg_dev_teleport(v_state, v_argument, v_viewer__93799, v_index__93840);
        }
        if (R.Bool(R.eq(v_command, "time")))
        {
            object v_minutes__94076 = v_argument;
            if (R.Bool(R.is_text(v_argument)))
            {
                if (R.Bool(R.not(R.is_number(v_argument))))
                {
                    return F_dvg_dev_result(v_state, v_command, Authored("m60.dvg_presentation.literal_0010"), Authored("m60.dvg_dev_command.literal_0003"), new List<object> {  });
                }
                v_minutes__94076 = R.number(v_argument);
            }
            return F_dvg_dev_time(v_state, v_minutes__94076, v_viewer__93799, v_index__93840);
        }
        return F_dvg_dev_result(v_state, v_command, Authored("m60.dvg_presentation.literal_0010"), Authored("m60.dvg_dev_command.literal_0004"), new List<object> {  });
    }
    public static object F_dvg_observer_directory(object v_state) {
        object v_player_index__94954 = F_dv_index_by_id(F_dv_get(v_state, "actors"), F_dv_find(v_state, Authored("m60.dvg_intent.literal_0004"), ""));
        if (R.Bool(R.not(R.lt(v_player_index__94954, R.number("0")))))
        {
            object v_player__95126 = R.at(F_dv_get(v_state, "actors"), v_player_index__94954);
            object v_identities__95175 = new List<object> {  };
            object v_identity_index__95204 = R.number("0");
            while (R.Bool(R.lt(v_identity_index__95204, R.count(F_dv_get(v_state, "actors")))))
            {
                object v_actor__95296 = R.at(F_dv_get(v_state, "actors"), v_identity_index__95204);
                _ = R.push(v_identities__95175, new List<object> { F_dv_get(v_actor__95296, "id"), F_dv_get(F_dv_get(v_actor__95296, "location_ref"), "object") });
                v_identity_index__95204 = R.add(v_identity_index__95204, R.number("1"));
            }
            object v_key__95622 = DvValueTemplate.Instantiate("template.inline.dvg_observer_directory.1617", v_player__95126, v_state, v_identities__95175);
            object v_cached__96007 = R.memo_get(F_dv_get(F_dv_get(v_player__95126, "mind"), "objects"), v_key__95622);
            if (R.Bool(R.at(v_cached__96007, R.number("0"))))
            {
                return R.at(v_cached__96007, R.number("1"));
            }
            object v_directory__96120 = new List<object> {  };
            object v_index__96148 = R.number("0");
            while (R.Bool(R.lt(v_index__96148, R.count(F_dv_get(v_state, "actors")))))
            {
                object v_actor__96222 = R.at(F_dv_get(v_state, "actors"), v_index__96148);
                object v_local__96267 = F_dvg_preview_actor_object(v_state, v_actor__96222, F_dv_get(v_player__95126, "mind"));
                object v_title__96344 = F_dvg_preview_person_name(v_state, F_dv_get(v_player__95126, "mind"), F_dv_get(v_actor__96222, "id"), v_local__96267, Authored("m60.dvg_observer_directory.literal_0002"));
                _ = R.push(v_directory__96120, new List<object> { F_dv_pair("id", F_dv_get(v_actor__96222, "id")), F_dv_pair("title", v_title__96344) });
                v_index__96148 = R.add(v_index__96148, R.number("1"));
            }
            return R.memo_put(F_dv_get(F_dv_get(v_player__95126, "mind"), "objects"), v_key__95622, v_directory__96120);
        }
        object v_result__96664 = new List<object> {  };
        object v_i__96685 = R.number("0");
        while (R.Bool(R.lt(v_i__96685, R.count(F_dv_get(v_state, "actors")))))
        {
            object v_actor__96743 = R.at(F_dv_get(v_state, "actors"), v_i__96685);
            object v_title__96780 = F_dvg_observer_title(v_state, v_actor__96743);
            _ = R.push(v_result__96664, new List<object> { F_dv_pair("id", F_dv_get(v_actor__96743, "id")), F_dv_pair("title", v_title__96780) });
            v_i__96685 = R.add(v_i__96685, R.number("1"));
        }
        return v_result__96664;
    }
    public static object F_dvg_observer_title(object v_state,object v_actor) {
        object v_key__96967 = DvValueTemplate.Instantiate("template.inline.dvg_observer_title.1618", v_actor);
        object v_cached__97230 = R.memo_get(F_dv_get(F_dv_get(v_actor, "mind"), "objects"), v_key__96967);
        if (R.Bool(R.at(v_cached__97230, R.number("0"))))
        {
            return R.at(v_cached__97230, R.number("1"));
        }
        return R.memo_put(F_dv_get(F_dv_get(v_actor, "mind"), "objects"), v_key__96967, F_dvg_preview_person_name(v_state, F_dv_get(v_actor, "mind"), F_dv_get(v_actor, "id"), F_dv_get(F_dv_get(v_actor, "location_ref"), "object"), Authored("m60.dvg_observer_directory.literal_0002")));
    }
    public static object F_dvg_preview_room(object v_location) {
        object v_key__97574 = DvValueTemplate.Instantiate("template.inline.dvg_preview_room.1619", v_location);
        object v_cached__97669 = R.memo_get(new List<object> {  }, v_key__97574);
        if (R.Bool(R.at(v_cached__97669, R.number("0"))))
        {
            return R.at(v_cached__97669, R.number("1"));
        }
        object v_place__97753 = F_dvg_place(v_location);
        return R.memo_put(new List<object> {  }, v_key__97574, new List<object> { F_dv_pair("map", F_dv_get(v_place__97753, "map")), F_dv_pair("room", F_dv_get(v_place__97753, "room")), F_dv_pair("logical_location", v_location), F_dv_pair("label", F_dvg_location_title(v_location)), F_dv_pair("region", F_campaign_location_region(v_location)), F_dv_pair("members", new List<object> {  }), F_dv_pair("is_current", false) });
    }
    public static object F_dvg_preview_location(object v_world,object v_id) {
        object v_fields__98135 = AuthoredCopy("m60.dvg_preview_location.fields");
        object v_i__98239 = R.number("0");
        while (R.Bool(R.lt(v_i__98239, R.count(v_fields__98135))))
        {
            object v_held__98291 = F_dv_read(v_world, F_dv_ref(v_id, "state", R.at(v_fields__98135, v_i__98239)));
            if (R.Bool((R.Bool((R.Bool(F_dv_uncontested(v_held__98291)) ? (object)(R.is_text(F_dv_get(v_held__98291, "value"))) : (object)(false))) ? (object)(R.not(R.eq(F_dv_get(v_held__98291, "value"), ""))) : (object)(false))))
            {
                return v_held__98291;
            }
            v_i__98239 = R.add(v_i__98239, R.number("1"));
        }
        object v_location__98480 = F_dv_read(v_world, F_dv_ref(v_id, "state", "location"));
        if (R.Bool(F_dv_uncontested(v_location__98480)))
        {
            return v_location__98480;
        }
        if (R.Bool(R.eq(F_dv_get(v_world, "domain"), "physical")))
        {
            object v_object__98647 = F_dv_find(F_dv_get(v_world, "objects"), v_id, new List<object> {  });
            object v_declared__98700 = F_dv_find(v_object__98647, "location", "");
            if (R.Bool((R.Bool(R.is_text(v_declared__98700)) ? (object)(R.not(R.eq(v_declared__98700, ""))) : (object)(false))))
            {
                return F_dv_field(v_declared__98700);
            }
        }
        return v_location__98480;
    }
    public static object F_dvg_preview_add_room(object v_rooms,object v_location) {
        if (R.Bool((R.Bool(R.not(R.is_text(v_location))) ? (object)(true) : (object)(R.eq(v_location, "")))))
        {
            return v_rooms;
        }
        return F_dvg_known_room_add(v_rooms, F_dvg_preview_room(v_location));
    }
    public static object F_dvg_preview_add_member(object v_rooms,object v_location,object v_member) {
        if (R.Bool((R.Bool(R.not(R.is_text(v_location))) ? (object)(true) : (object)(R.eq(v_location, "")))))
        {
            return v_rooms;
        }
        object v_place__99179 = F_dvg_preview_room(v_location);
        if (R.Bool(R.eq(F_dv_get(v_place__99179, "map"), "")))
        {
            return v_rooms;
        }
        object v_existing__99270 = R.number("0");
        while (R.Bool(R.lt(v_existing__99270, R.count(v_rooms))))
        {
            object v_saved__99335 = R.at(v_rooms, v_existing__99270);
            if (R.Bool((R.Bool(R.eq(F_dv_get(v_saved__99335, "map"), F_dv_get(v_place__99179, "map"))) ? (object)(R.eq(F_dv_get(v_saved__99335, "room"), F_dv_get(v_place__99179, "room"))) : (object)(false))))
            {
                object v_members__99441 = F_dv_find(v_saved__99335, Authored("m60.dvg_preview_add_member.literal"), new List<object> {  });
                object v_index__99562 = R.number("0");
                while (R.Bool(R.lt(v_index__99562, R.count(v_members__99441))))
                {
                    if (R.Bool(R.eq(F_dv_get(R.at(v_members__99441, v_index__99562), "id"), F_dv_get(v_member, "id"))))
                    {
                        return v_rooms;
                    }
                    v_index__99562 = R.add(v_index__99562, R.number("1"));
                }
            }
            v_existing__99270 = R.add(v_existing__99270, R.number("1"));
        }
        v_rooms = F_dvg_preview_add_room(v_rooms, v_location);
        object v_result__99832 = new List<object> {  };
        object v_i__99853 = R.number("0");
        while (R.Bool(R.lt(v_i__99853, R.count(v_rooms))))
        {
            object v_room__99904 = R.at(v_rooms, v_i__99853);
            if (R.Bool((R.Bool(R.eq(F_dv_get(v_room__99904, "map"), F_dv_get(v_place__99179, "map"))) ? (object)(R.eq(F_dv_get(v_room__99904, "room"), F_dv_get(v_place__99179, "room"))) : (object)(false))))
            {
                object v_members__100000 = F_dv_find(v_room__99904, Authored("m60.dvg_preview_add_member.literal"), new List<object> {  });
                object v_found__100120 = false;
                object v_j__100151 = R.number("0");
                while (R.Bool(R.lt(v_j__100151, R.count(v_members__100000))))
                {
                    if (R.Bool(R.eq(F_dv_get(R.at(v_members__100000, v_j__100151), "id"), F_dv_get(v_member, "id"))))
                    {
                        v_found__100120 = true;
                    }
                    v_j__100151 = R.add(v_j__100151, R.number("1"));
                }
                if (R.Bool(R.not(v_found__100120)))
                {
                    v_members__100000 = F_dv_append(v_members__100000, v_member);
                }
                v_room__99904 = DvValueTemplate.Instantiate("template.inline.dvg_preview_add_member.1620", v_room__99904, v_members__100000);
                if (R.Bool(F_dv_find(v_member, "is_viewer", false)))
                {
                    v_room__99904 = DvValueTemplate.Instantiate("template.inline.dvg_preview_add_member.1621", v_room__99904);
                }
            }
            _ = R.push(v_result__99832, v_room__99904);
            v_i__99853 = R.add(v_i__99853, R.number("1"));
        }
        return v_result__99832;
    }

    public static object F_dvg_pending_self_names(object v_state,object v_viewer) {
        object v_index__103601 = F_dvg_dev_actor_index(v_state, v_viewer);
        if (R.Bool(R.lt(v_index__103601, R.number("0"))))
        {
            return new List<object> {  };
        }
        object v_subject__103691 = R.at(F_dv_get(v_state, "actors"), v_index__103601);
        object v_messages__103730 = F_dv_find(v_subject__103691, Authored("m60.dvg_prepare_background.literal_0004"), new List<object> {  });
        object v_key__103851 = DvValueTemplate.Instantiate("template.inline.dvg_pending_self_names.1622", v_viewer);
        object v_cached__103950 = R.memo_get(v_messages__103730, v_key__103851);
        if (R.Bool(R.at(v_cached__103950, R.number("0"))))
        {
            return R.at(v_cached__103950, R.number("1"));
        }
        object v_result__104040 = new List<object> {  };
        object v_i__104061 = R.number("0");
        while (R.Bool(R.lt(v_i__104061, R.count(v_messages__103730))))
        {
            object v_message__104115 = R.at(v_messages__103730, v_i__104061);
            if (R.Bool(R.not(R.eq(F_dv_find(v_message__104115, "source", ""), ""))))
            {
                object v_name__104204 = F_dvg_literal_self_name(F_dv_get(F_dv_get(v_message__104115, "speech"), "literal_ast"), F_dv_get(v_message__104115, "speaker"));
                if (R.Bool(R.not(R.eq(v_name__104204, ""))))
                {
                    v_result__104040 = DvValueTemplate.Instantiate("template.inline.dvg_pending_self_names.1623", v_result__104040, v_message__104115, v_name__104204);
                }
            }
            v_i__104061 = R.add(v_i__104061, R.number("1"));
        }
        return R.memo_put(v_messages__103730, v_key__103851, v_result__104040);
    }
    public static object F_dvg_preview_person_name(object v_state,object v_world,object v_person,object v_local,object v_fallback) {
        if (R.Bool((R.Bool(R.eq(F_dv_get(v_world, "domain"), "mind")) ? (object)(F_dv_cognition_hidden(v_world, v_local)) : (object)(false))))
        {
            return v_fallback;
        }
        object v_recorded__104699 = F_dvg_preview_name(v_world, v_local, "");
        if (R.Bool(R.not(R.eq(v_recorded__104699, ""))))
        {
            return v_recorded__104699;
        }
        if (R.Bool(R.not(R.eq(v_local, v_person))))
        {
            object v_aliased__104832 = F_dvg_preview_name(v_world, v_person, "");
            if (R.Bool(R.not(R.eq(v_aliased__104832, ""))))
            {
                return v_aliased__104832;
            }
        }
        if (R.Bool(R.eq(F_dv_get(v_world, "domain"), "mind")))
        {
            object v_heard_name__104978 = F_dv_find(F_dvg_pending_self_names(v_state, F_dv_get(v_world, "subject")), v_person, "");
            if (R.Bool(R.not(R.eq(v_heard_name__104978, ""))))
            {
                return v_heard_name__104978;
            }
        }
        return v_fallback;
    }
    public static object F_dvg_preview_actor_object(object v_state,object v_actor,object v_world) {
        if (R.Bool((R.Bool(R.eq(F_dv_get(v_world, "domain"), "mind")) ? (object)(R.eq(F_dv_get(v_world, "subject"), F_dv_get(v_actor, "id"))) : (object)(false))))
        {
            return F_dv_get(F_dv_get(v_actor, "location_ref"), "object");
        }
        object v_key__105305 = DvValueTemplate.Instantiate("template.inline.dvg_preview_actor_object.1624", v_actor, v_world);
        object v_cached__105464 = R.memo_get(F_dv_get(v_state, "bindings"), v_key__105305);
        if (R.Bool(R.at(v_cached__105464, R.number("0"))))
        {
            return R.at(v_cached__105464, R.number("1"));
        }
        object v_physical__105560 = F_dv_get(v_actor, "id");
        object v_i__105589 = R.number("0");
        while (R.Bool(R.lt(v_i__105589, R.count(F_dv_get(v_state, "bindings")))))
        {
            object v_binding__105649 = R.at(F_dv_get(v_state, "bindings"), v_i__105589);
            if (R.Bool((R.Bool(R.eq(F_dv_get(v_binding__105649, "subject"), F_dv_get(v_actor, "id"))) ? (object)(R.eq(F_dv_get(v_binding__105649, "local"), F_dv_get(F_dv_get(v_actor, "location_ref"), "object"))) : (object)(false))))
            {
                v_physical__105560 = F_dv_get(v_binding__105649, "physical");
            }
            v_i__105589 = R.add(v_i__105589, R.number("1"));
        }
        if (R.Bool(R.eq(F_dv_get(v_world, "domain"), "physical")))
        {
            return R.memo_put(F_dv_get(v_state, "bindings"), v_key__105305, v_physical__105560);
        }
        object v_local__105929 = F_dv_local(F_dv_get(v_state, "bindings"), v_physical__105560, F_dv_get(v_world, "subject"));
        if (R.Bool(R.not(R.eq(v_local__105929, ""))))
        {
            return R.memo_put(F_dv_get(v_state, "bindings"), v_key__105305, v_local__105929);
        }
        return R.memo_put(F_dv_get(v_state, "bindings"), v_key__105305, F_dv_get(v_actor, "id"));
    }
    public static object F_dvg_preview_relation_id(object v_identities,object v_id) {
        return DvValueTemplate.Instantiate("template.dvg_preview_relation_id", v_identities, v_id);
    }
    public static object F_dvg_preview_index(object v_world) {
        object v_key__106543 = DvValueTemplate.Instantiate("template.inline.dvg_preview_index.1625", v_world);
        object v_cached__106761 = R.memo_get(F_dv_get(v_world, "objects"), v_key__106543);
        if (R.Bool(R.at(v_cached__106761, R.number("0"))))
        {
            return R.at(v_cached__106761, R.number("1"));
        }
        _ = R.phase(Authored("m60.dvg_preview_index.literal_0002"), F_dv_find(v_world, "subject", ""));
        object v_entries__106976 = new List<object> {  };
        object v_i__106998 = R.number("0");
        while (R.Bool(R.lt(v_i__106998, R.count(F_dv_get(v_world, "objects")))))
        {
            object v_id__107057 = R.at(R.at(F_dv_get(v_world, "objects"), v_i__106998), R.number("0"));
            object v_location__107095 = F_dvg_preview_location(v_world, v_id__107057);
            object v_name__107151 = "";
            if (R.Bool(F_dv_uncontested(v_location__107095)))
            {
                v_name__107151 = F_dvg_preview_name(v_world, v_id__107057, "");
            }
            _ = R.push(v_entries__106976, F_dv_pair(v_id__107057, new List<object> { F_dv_pair("location", v_location__107095), F_dv_pair("name", v_name__107151) }));
            v_i__106998 = R.add(v_i__106998, R.number("1"));
        }
        object v_result__107356 = new List<object> {  };
        v_i__106998 = R.number("0");
        while (R.Bool(R.lt(v_i__106998, R.count(v_entries__106976))))
        {
            object v_entry__107426 = R.at(v_entries__106976, v_i__106998);
            object v_room__107458 = "";
            if (R.Bool(F_dv_uncontested(F_dv_get(R.at(v_entry__107426, R.number("1")), "location"))))
            {
                v_room__107458 = F_dvg_preview_index_room(v_entries__106976, F_dv_get(F_dv_get(R.at(v_entry__107426, R.number("1")), "location"), "value"));
            }
            _ = R.push(v_result__107356, F_dv_pair(R.at(v_entry__107426, R.number("0")), F_dv_with(R.at(v_entry__107426, R.number("1")), "room", v_room__107458)));
            v_i__106998 = R.add(v_i__106998, R.number("1"));
        }
        _ = R.phase(Authored("m60.dvg_preview_index.literal_0003"), F_dv_find(v_world, "subject", ""));
        return R.memo_put(F_dv_get(v_world, "objects"), v_key__106543, v_result__107356);
    }
    public static object F_dvg_preview_index_room(object v_entries,object v_location) {
        object v_visited__107918 = new List<object> {  };
        object v_remaining__107940 = R.add(R.count(v_entries), R.number("1"));
        while (R.Bool((R.Bool((R.Bool(R.lt(R.number("0"), v_remaining__107940)) ? (object)(R.is_text(v_location)) : (object)(false))) ? (object)(R.not(R.eq(v_location, ""))) : (object)(false))))
        {
            if (R.Bool(R.not(R.eq(F_dv_get(F_dvg_place(v_location), "map"), ""))))
            {
                return v_location;
            }
            if (R.Bool(F_dv_has(v_visited__107918, v_location)))
            {
                return "";
            }
            _ = R.push(v_visited__107918, v_location);
            object v_parent__108227 = F_dv_find(v_entries, v_location, false);
            if (R.Bool((R.Bool(R.eq(v_parent__108227, false)) ? (object)(true) : (object)(R.not(F_dv_uncontested(F_dv_get(v_parent__108227, "location")))))))
            {
                return "";
            }
            v_location = F_dv_get(F_dv_get(v_parent__108227, "location"), "value");
            v_remaining__107940 = R.sub(v_remaining__107940, R.number("1"));
        }
        return "";
    }
    public static object F_dvg_preview_members(object v_state,object v_world,object v_viewer,object v_rooms,object v_entries) {
        _ = R.phase(Authored("m60.dvg_preview_members.literal"), v_viewer);
        object v_identities__108621 = new List<object> {  };
        object v_actor_index__108646 = R.number("0");
        while (R.Bool(R.lt(v_actor_index__108646, R.count(F_dv_get(v_state, "actors")))))
        {
            object v_actor__108724 = R.at(F_dv_get(v_state, "actors"), v_actor_index__108646);
            object v_local__108771 = F_dvg_preview_actor_object(v_state, v_actor__108724, v_world);
            v_identities__108621 = F_dv_with(v_identities__108621, v_local__108771, new List<object> { F_dv_pair("id", F_dv_get(v_actor__108724, "id")), F_dv_pair("is_viewer", R.eq(F_dv_get(v_actor__108724, "id"), v_viewer)) });
            v_actor_index__108646 = R.add(v_actor_index__108646, R.number("1"));
        }
        object v_i__108973 = R.number("0");
        while (R.Bool(R.lt(v_i__108973, R.count(F_dv_get(v_world, "objects")))))
        {
            object v_id__109032 = R.at(R.at(F_dv_get(v_world, "objects"), v_i__108973), R.number("0"));
            object v_object__109070 = R.at(R.at(F_dv_get(v_world, "objects"), v_i__108973), R.number("1"));
            object v_descriptor__109112 = F_campaign_object_descriptor(v_id__109032);
            object v_indexed__109169 = F_dv_get(v_entries, v_id__109032);
            object v_location__109212 = F_dv_get(v_indexed__109169, "location");
            object v_person__109253 = F_dv_find(v_identities__108621, v_id__109032, false);
            object v_kind__109306 = F_dv_find(v_object__109070, "map_kind", F_dv_get(v_descriptor__109112, "map_kind"));
            if (R.Bool(R.not(R.eq(v_person__109253, false))))
            {
                v_kind__109306 = "person";
            }
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_preview_members.1626", v_kind__109306, v_location__109212)))
            {
                object v_identity__109615 = v_id__109032;
                object v_self__109646 = false;
                object v_fallback__109676 = Authored("m60.dvg_preview_members.fallback");
                if (R.Bool(R.eq(F_dv_get(v_world, "domain"), "physical")))
                {
                    v_fallback__109676 = F_dv_find(v_object__109070, "map_title", F_dv_get(v_descriptor__109112, "map_title"));
                    if (R.Bool(R.eq(v_fallback__109676, "")))
                    {
                        v_fallback__109676 = Authored("m60.dvg_preview_members.fallback");
                    }
                }
                if (R.Bool(R.not(R.eq(v_person__109253, false))))
                {
                    v_identity__109615 = F_dv_get(v_person__109253, "id");
                    v_self__109646 = F_dv_get(v_person__109253, "is_viewer");
                    v_fallback__109676 = F_dvg_unknown_person_label(v_identity__109615, v_viewer);
                }
                if (R.Bool(v_self__109646))
                {
                    v_fallback__109676 = Authored("m60.dvg_preview_members.literal_0003");
                }
                object v_title__110348 = F_dv_get(v_indexed__109169, "name");
                if (R.Bool(R.eq(v_title__110348, "")))
                {
                    v_title__110348 = v_fallback__109676;
                }
                if (R.Bool(R.not(R.eq(v_person__109253, false))))
                {
                    v_title__110348 = F_dvg_preview_person_name(v_state, v_world, v_identity__109615, v_id__109032, v_fallback__109676);
                }
                object v_carrier__110569 = "";
                object v_container__110599 = "";
                if (R.Bool(F_dv_has_key(v_identities__108621, F_dv_get(v_location__109212, "value"))))
                {
                    v_carrier__110569 = F_dvg_preview_relation_id(v_identities__108621, F_dv_get(v_location__109212, "value"));
                }
                else
                {
                    if (R.Bool((R.Bool(R.eq(F_dv_get(F_dvg_place(F_dv_get(v_location__109212, "value")), "map"), "")) ? (object)(F_dv_has_key(F_dv_get(v_world, "objects"), F_dv_get(v_location__109212, "value"))) : (object)(false))))
                    {
                        v_container__110599 = F_dv_get(v_location__109212, "value");
                    }
                }
                object v_owner__110936 = "";
                object v_owner_title__110964 = "";
                object v_ownership__110998 = F_dv_read(v_world, F_dv_ref(v_id__109032, Authored("m60.c1_player_name_parts.literal"), Authored("m60.dvg_preview_members.literal_0004")));
                if (R.Bool(R.not(F_dv_uncontested(v_ownership__110998))))
                {
                    v_ownership__110998 = F_dv_read(v_world, F_dv_ref(v_id__109032, "state", Authored("m60.dvg_preview_members.literal_0004")));
                }
                if (R.Bool((R.Bool((R.Bool(F_dv_uncontested(v_ownership__110998)) ? (object)(R.is_text(F_dv_get(v_ownership__110998, "value"))) : (object)(false))) ? (object)(R.not(R.eq(F_dv_get(v_ownership__110998, "value"), ""))) : (object)(false))))
                {
                    v_owner__110936 = F_dvg_preview_relation_id(v_identities__108621, F_dv_get(v_ownership__110998, "value"));
                    v_owner_title__110964 = F_dvg_preview_person_name(v_state, v_world, v_owner__110936, F_dv_get(v_ownership__110998, "value"), Authored("m60.dvg_preview_members.literal_0005"));
                }
                object v_room_location__111742 = F_dv_get(v_indexed__109169, "room");
                object v_member__111788 = new List<object> { F_dv_pair("id", v_identity__109615), F_dv_pair("title", v_title__110348), F_dv_pair("kind", v_kind__109306), F_dv_pair("is_viewer", v_self__109646), F_dv_pair("owner", v_owner__110936), F_dv_pair("owner_title", v_owner_title__110964), F_dv_pair("carrier", v_carrier__110569), F_dv_pair("container", v_container__110599) };
                v_rooms = F_dvg_preview_add_member(v_rooms, v_room_location__111742, v_member__111788);
            }
            v_i__108973 = R.add(v_i__108973, R.number("1"));
        }
        _ = R.phase(Authored("m60.dvg_preview_members.literal_0006"), v_viewer);
        return v_rooms;
    }

    public static object F_dvg_preview_sites(object v_rooms,object v_actor,object v_world,object v_allowed,object v_objective,object v_entries) {
        _ = R.phase(Authored("m60.dvg_preview_sites.literal"), F_dv_find(v_world, "subject", ""));
        object v_sites__113337 = F_dvg_preview_action_sites(v_actor);
        object v_i__113386 = R.number("0");
        while (R.Bool(R.lt(v_i__113386, R.count(v_sites__113337))))
        {
            object v_site__113437 = R.at(v_sites__113337, v_i__113386);
            if (R.Bool((R.Bool(v_objective) ? (object)(true) : (object)((R.Bool(F_dv_has(v_allowed, F_dv_get(v_site__113437, "action_id"))) ? (object)(R.not(F_dv_cognition_hidden(v_world, F_dv_get(F_dv_get(v_site__113437, "member"), "id")))) : (object)(false))))))
            {
                object v_location__113581 = F_dv_get(v_site__113437, "location");
                object v_recorded__113623 = F_dv_find(v_entries, F_dv_get(F_dv_get(v_site__113437, "member"), "id"), false);
                if (R.Bool((R.Bool(R.not(R.eq(v_recorded__113623, false))) ? (object)(F_dv_uncontested(F_dv_get(v_recorded__113623, "location"))) : (object)(false))))
                {
                    v_location__113581 = F_dv_get(v_recorded__113623, "room");
                }
                v_rooms = F_dvg_preview_add_member(v_rooms, v_location__113581, F_dv_get(v_site__113437, "member"));
            }
            v_i__113386 = R.add(v_i__113386, R.number("1"));
        }
        _ = R.phase(Authored("m60.dvg_preview_sites.literal_0002"), F_dv_find(v_world, "subject", ""));
        return v_rooms;
    }
    public static object F_dvg_preview_action_sites(object v_actor) {
        object v_key__114300 = DvValueTemplate.Instantiate("template.inline.dvg_preview_action_sites.1627", v_actor);
        object v_cached__114413 = R.memo_get(F_dv_get(v_actor, "actions"), v_key__114300);
        if (R.Bool(R.at(v_cached__114413, R.number("0"))))
        {
            return R.at(v_cached__114413, R.number("1"));
        }
        object v_result__114508 = new List<object> {  };
        object v_i__114529 = R.number("0");
        while (R.Bool(R.lt(v_i__114529, R.count(F_dv_get(v_actor, "actions")))))
        {
            object v_action__114588 = R.at(F_dv_get(v_actor, "actions"), v_i__114529);
            object v_interaction__114627 = F_dvg_interaction(v_action__114588);
            if (R.Bool(R.eq(F_dv_get(v_interaction__114627, "kind"), "site")))
            {
                object v_travel__114722 = F_dv_find(v_action__114588, "travel", new List<object> {  });
                object v_location__114778 = F_dv_find(v_action__114588, Authored("m60.dvg_preview_action_sites.literal_0002"), F_dv_find(v_travel__114722, "from", ""));
                if (R.Bool(R.eq(v_location__114778, "")))
                {
                    v_location__114778 = F_dvp_extract_required_room(F_dv_find(v_action__114588, Authored("m60.dvg_preview_action_sites.literal_0003"), new List<object> {  }), F_dv_get(v_actor, "location_ref"));
                }
                if (R.Bool(R.not(R.eq(v_location__114778, ""))))
                {
                    object v_member__115178 = F_dvg_preview_site_descriptor(v_action__114588, F_dv_get(v_interaction__114627, "site"), v_location__114778);
                    if (R.Bool(R.not(R.eq(v_member__115178, false))))
                    {
                        _ = R.push(v_result__114508, new List<object> { F_dv_pair("action_id", F_dv_get(v_action__114588, "id")), F_dv_pair("location", v_location__114778), F_dv_pair("member", v_member__115178) });
                    }
                }
            }
            v_i__114529 = R.add(v_i__114529, R.number("1"));
        }
        return R.memo_put(F_dv_get(v_actor, "actions"), v_key__114300, v_result__114508);
    }
    public static object F_dvg_preview_action_routes(object v_actions) {
        object v_key__115513 = AuthoredCopy("m60.dvg_preview_action_routes.key");
        object v_cached__115616 = R.memo_get(v_actions, v_key__115513);
        if (R.Bool(R.at(v_cached__115616, R.number("0"))))
        {
            return R.at(v_cached__115616, R.number("1"));
        }
        object v_result__115705 = new List<object> {  };
        object v_i__115726 = R.number("0");
        while (R.Bool(R.lt(v_i__115726, R.count(v_actions))))
        {
            object v_action__115779 = R.at(v_actions, v_i__115726);
            object v_travel__115812 = F_dv_find(v_action__115779, "travel", new List<object> {  });
            if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_action__115779, "kind"), "move")) ? (object)(F_dv_has_key(v_travel__115812, "from")) : (object)(false))) ? (object)(F_dv_has_key(v_travel__115812, "to")) : (object)(false))))
            {
                _ = R.push(v_result__115705, new List<object> { F_dv_pair("action_id", F_dv_get(v_action__115779, "id")), F_dv_pair("from", F_dv_get(v_travel__115812, "from")), F_dv_pair("to", F_dv_get(v_travel__115812, "to")) });
            }
            v_i__115726 = R.add(v_i__115726, R.number("1"));
        }
        return R.memo_put(v_actions, v_key__115513, v_result__115705);
    }

    public static object F_dvg_preview_subject(object v_state,object v_actor,object v_intent) {
        object v_identities__121696 = new List<object> {  };
        object v_i__121721 = R.number("0");
        while (R.Bool(R.lt(v_i__121721, R.count(F_dv_get(v_state, "actors")))))
        {
            object v_other__121779 = R.at(F_dv_get(v_state, "actors"), v_i__121721);
            _ = R.push(v_identities__121696, new List<object> { F_dv_get(v_other__121779, "id"), F_dv_get(v_other__121779, "location_ref") });
            v_i__121721 = R.add(v_i__121721, R.number("1"));
        }
        object v_input_options__121892 = new List<object> {  };
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_preview_subject.1628", v_intent, v_state, v_actor)))
        {
            v_input_options__121892 = F_dv_find(v_intent, "options", new List<object> {  });
        }
        object v_knowledge__122126 = F_dvg_map_knowledge(v_state, F_dv_get(v_actor, "id"), v_intent);
        object v_key__122190 = DvValueTemplate.Instantiate("template.inline.dvg_preview_subject.1629", v_actor, v_state, v_identities__121696, v_input_options__121892, v_knowledge__122126);
        object v_cached__122830 = R.memo_get(F_dv_get(F_dv_get(v_actor, "mind"), "objects"), v_key__122190);
        object v_result__122885 = new List<object> {  };
        if (R.Bool(R.at(v_cached__122830, R.number("0"))))
        {
            v_result__122885 = R.at(v_cached__122830, R.number("1"));
        }
        else
        {
            v_result__122885 = R.memo_put(F_dv_get(F_dv_get(v_actor, "mind"), "objects"), v_key__122190, F_dvg_preview_subject_geometry_known(v_state, v_actor, v_intent, v_knowledge__122126));
        }
        object v_live__123088 = new List<object> { DvValueTemplate.Instantiate("template.inline.dvg_preview_subject.1630", v_state), F_dv_pair("game_time", F_dv_get(F_dv_get(v_state, "physical"), "time")), F_dv_pair("active", F_dvs_session_active(v_state)), DvValueTemplate.Instantiate("template.inline.dvg_preview_subject.1631", v_state, v_actor) };
        return F_dv_with(v_result__122885, "snapshot", F_dv_with_many(F_dv_get(v_result__122885, "snapshot"), v_live__123088));
    }
    public static object F_dvg_preview_subject_geometry_known(object v_state,object v_actor,object v_intent,object v_knowledge) {
        object v_entries__123555 = F_dvg_preview_index(F_dv_get(v_actor, "mind"));
        object v_rooms__123604 = new List<object> {  };
        object v_i__123624 = R.number("0");
        while (R.Bool(R.lt(v_i__123624, R.count(F_dv_get(v_knowledge, "rooms")))))
        {
            _ = R.push(v_rooms__123604, DvValueTemplate.Instantiate("template.inline.dvg_preview_subject_geometry_known.1632", v_knowledge, v_i__123624));
            v_i__123624 = R.add(v_i__123624, R.number("1"));
        }
        v_rooms__123604 = F_dvg_preview_members(v_state, F_dv_get(v_actor, "mind"), F_dv_get(v_actor, "id"), v_rooms__123604, v_entries__123555);
        object v_allowed__123938 = F_dv_append(F_dv_find(v_actor, Authored("m60.dvg_subject_map_knowledge.literal_0002"), new List<object> {  }), "");
        object v_offered__124074 = F_dv_find(v_actor, Authored("m60.dvg_subject_map_knowledge.literal_0003"), new List<object> {  });
        v_i__123624 = R.number("0");
        while (R.Bool(R.lt(v_i__123624, R.count(v_offered__124074))))
        {
            _ = R.push(v_allowed__123938, R.at(v_offered__124074, v_i__123624));
            v_i__123624 = R.add(v_i__123624, R.number("1"));
        }
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_preview_subject_geometry_known.1633", v_intent, v_state, v_actor)))
        {
            v_i__123624 = R.number("0");
            while (R.Bool(R.lt(v_i__123624, R.count(F_dv_find(v_intent, "options", new List<object> {  })))))
            {
                _ = R.push(v_allowed__123938, F_dv_get(R.at(F_dv_get(v_intent, "options"), v_i__123624), "id"));
                v_i__123624 = R.add(v_i__123624, R.number("1"));
            }
        }
        v_rooms__123604 = F_dvg_preview_sites(v_rooms__123604, v_actor, F_dv_get(v_actor, "mind"), v_allowed__123938, false, v_entries__123555);
        object v_position__124680 = F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_actor, "location_ref"));
        object v_place__124740 = AuthoredCopy("m60.dvg_preview_subject_geometry_known.place");
        if (R.Bool((R.Bool(F_dv_uncontested(v_position__124680)) ? (object)(R.is_text(F_dv_get(v_position__124680, "value"))) : (object)(false))))
        {
            object v_room_location__124925 = F_dvg_preview_index_room(v_entries__123555, F_dv_get(v_position__124680, "value"));
            if (R.Bool(R.not(R.eq(v_room_location__124925, ""))))
            {
                v_place__124740 = F_dvg_preview_room(v_room_location__124925);
                v_rooms__123604 = F_dvg_preview_add_member(v_rooms__123604, v_room_location__124925, new List<object> { F_dv_pair("id", F_dv_get(v_actor, "id")), F_dv_pair("title", F_dvg_preview_person_name(v_state, F_dv_get(v_actor, "mind"), F_dv_get(v_actor, "id"), F_dv_get(F_dv_get(v_actor, "location_ref"), "object"), Authored("m60.dvg_preview_members.literal_0003"))), F_dv_pair("kind", "person"), F_dv_pair("is_viewer", true) });
            }
        }
        object v_subjective__125372 = new List<object> { F_dv_pair("rooms", v_rooms__123604), F_dv_pair("doors", F_dvg_preview_doors(F_dv_get(v_actor, "mind"), F_dv_get(v_knowledge, "doors"))) };
        object v_snapshot__125468 = new List<object> { F_dv_pair("viewer", F_dv_get(v_actor, "id")), F_dv_pair("viewer_title", F_dvg_preview_person_name(v_state, F_dv_get(v_actor, "mind"), F_dv_get(v_actor, "id"), F_dv_get(F_dv_get(v_actor, "location_ref"), "object"), Authored("m60.dvg_preview_members.literal_0003"))), F_dv_pair("map", F_dv_get(v_place__124740, "map")), F_dv_pair("room", F_dv_get(v_place__124740, "room")), F_dv_pair("location_title", F_dv_get(v_place__124740, "label")), F_dv_pair("map_knowledge", v_subjective__125372), F_dv_pair("people", new List<object> {  }) };
        return new List<object> { F_dv_pair("snapshot", v_snapshot__125468), F_dv_pair("subjective", v_subjective__125372) };
    }
    public static object F_dvg_preview_objective(object v_state,object v_viewer) {
        object v_rooms__125900 = new List<object> {  };
        object v_knowledge__125920 = AuthoredCopy("m60.dvg_subject_map_knowledge.knowledge");
        object v_places__126035 = F_dvg_places();
        object v_entries__126066 = F_dvg_preview_index(F_dv_get(v_state, "physical"));
        object v_i__126119 = R.number("0");
        while (R.Bool(R.lt(v_i__126119, R.count(v_places__126035))))
        {
            v_rooms__125900 = F_dvg_preview_add_room(v_rooms__125900, R.at(R.at(v_places__126035, v_i__126119), R.number("0")));
            v_i__126119 = R.add(v_i__126119, R.number("1"));
        }
        v_rooms__125900 = F_dvg_preview_members(v_state, F_dv_get(v_state, "physical"), v_viewer, v_rooms__125900, v_entries__126066);
        v_i__126119 = R.number("0");
        while (R.Bool(R.lt(v_i__126119, R.count(F_dv_get(v_state, "actors")))))
        {
            object v_actor__126379 = R.at(F_dv_get(v_state, "actors"), v_i__126119);
            v_rooms__125900 = F_dvg_preview_sites(v_rooms__125900, v_actor__126379, F_dv_get(v_state, "physical"), new List<object> {  }, true, v_entries__126066);
            object v_routes__126500 = F_dvg_preview_action_routes(F_dv_get(v_actor__126379, "actions"));
            object v_j__126563 = R.number("0");
            while (R.Bool(R.lt(v_j__126563, R.count(v_routes__126500))))
            {
                object v_route__126623 = R.at(v_routes__126500, v_j__126563);
                v_rooms__125900 = F_dvg_preview_add_room(F_dvg_preview_add_room(v_rooms__125900, F_dv_get(v_route__126623, "from")), F_dv_get(v_route__126623, "to"));
                v_knowledge__125920 = F_dvg_known_door_add(v_knowledge__125920, F_dv_get(v_route__126623, "from"), F_dv_get(v_route__126623, "to"));
                v_j__126563 = R.add(v_j__126563, R.number("1"));
            }
            v_i__126119 = R.add(v_i__126119, R.number("1"));
        }
        return new List<object> { F_dv_pair("rooms", v_rooms__125900), F_dv_pair("doors", F_dvg_preview_doors(F_dv_get(v_state, "physical"), F_dv_get(v_knowledge__125920, "doors"))) };
    }
    public static object F_dvg_observer_action_label(object v_state,object v_viewer,object v_action) {
        object v_subject__127008 = R.at(F_dv_get(v_state, "actors"), F_dvg_dev_actor_index(v_state, v_viewer));
        return F_dvg_subjective_person_text(v_state, v_subject__127008, F_dvs_action_label(v_subject__127008, v_action));
    }
    public static object F_dvg_persistent_identity_actions(object v_actor) {
        object v_social__127212 = F_c1f_identity_actions(F_dv_get(v_actor, "id"));
        object v_actions__127261 = F_dv_get(v_actor, "actions");
        object v_changed__127294 = false;
        object v_i__127319 = R.number("0");
        while (R.Bool(R.lt(v_i__127319, R.count(v_social__127212))))
        {
            if (R.Bool(R.eq(F_dvp_find_action(v_actions__127261, F_dv_get(R.at(v_social__127212, v_i__127319), "id")), false)))
            {
                v_actions__127261 = F_dv_append(v_actions__127261, F_c1_public_method(R.at(v_social__127212, v_i__127319)));
                v_changed__127294 = true;
            }
            v_i__127319 = R.add(v_i__127319, R.number("1"));
        }
        if (R.Bool(v_changed__127294))
        {
            return F_dv_with(v_actor, "actions", v_actions__127261);
        }
        return v_actor;
    }
    public static object F_dvg_observer_options(object v_state,object v_viewer,object v_include_immediate_scores) {
        if (R.Bool(R.not(F_dvs_session_active(v_state))))
        {
            return new List<object> {  };
        }
        object v_actor__127759 = R.at(F_dv_get(v_state, "actors"), F_dvg_dev_actor_index(v_state, v_viewer));
        object v_key__127973 = DvValueTemplate.Instantiate("template.inline.dvg_observer_options.1634", v_state, v_viewer, v_include_immediate_scores);
        object v_cached__128215 = R.memo_get(v_actor__127759, v_key__127973);
        if (R.Bool(R.at(v_cached__128215, R.number("0"))))
        {
            return R.at(v_cached__128215, R.number("1"));
        }
        return R.memo_put(v_actor__127759, v_key__127973, F_dvg_observer_options_build(v_state, v_viewer, v_actor__127759, v_include_immediate_scores));
    }
    public static object F_dvg_observer_options_build(object v_state,object v_viewer,object v_actor,object v_include_immediate_scores) {
        _ = R.phase(Authored("m60.dvg_observer_options_build.literal"), v_viewer);
        object v_pending__128597 = F_dv_find(v_state, "pending", false);
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_observer_options_build.1635", v_actor, v_pending__128597, v_viewer)))
        {
            _ = R.phase(Authored("m60.dvg_observer_options_build.literal_0002"), v_viewer);
            return new List<object> {  };
        }
        object v_goals__128935 = F_dvp_goal_refs(v_actor);
        object v_view__128973 = v_actor;
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_observer_options_build.1636", v_state, v_viewer)))
        {
            v_view__128973 = DvValueTemplate.Instantiate("template.inline.dvg_observer_options_build.1637", v_actor);
        }
        object v_result__129214 = new List<object> {  };
        object v_i__129235 = R.number("0");
        while (R.Bool(R.lt(v_i__129235, R.count(F_dv_get(v_actor, "actions")))))
        {
            object v_action__129294 = R.at(F_dv_get(v_actor, "actions"), v_i__129235);
            if (R.Bool(F_dvp_eligible(v_view__128973, v_action__129294, F_dv_get(v_actor, "mind"), v_goals__128935)))
            {
                object v_label__129396 = F_dvg_observer_action_label(v_state, v_viewer, v_action__129294);
                if (R.Bool(R.not(R.eq(v_label__129396, ""))))
                {
                    object v_cost__129503 = F_dv_get(F_dv_get(F_dv_get(v_state, "costs"), F_dv_get(v_action__129294, "kind")), "actual");
                    object v_option__129818 = new List<object> { F_dv_pair("id", F_dv_get(v_action__129294, "id")), F_dv_pair("label", v_label__129396), F_dv_pair("available", true), F_dv_pair("kind", F_dv_get(v_action__129294, "kind")), F_dv_pair("interaction", F_dvg_interaction(v_action__129294)), F_dv_pair("cost", v_cost__129503), DvValueTemplate.Instantiate("template.inline.dvg_observer_options_build.1638"), F_dv_pair("mind_revision", F_dv_get(F_dv_get(v_actor, "mind"), "revision")) };
                    if (R.Bool(v_include_immediate_scores))
                    {
                        object v_scored__130162 = F_dvs_current_action_score(DvValueTemplate.Instantiate("template.inline.dvg_observer_options_build.1639", v_view__128973, v_state), v_action__129294, F_dv_get(v_state, "bindings"), v_goals__128935, v_cost__129503);
                        v_option__129818 = DvValueTemplate.Instantiate("template.inline.dvg_observer_options_build.1640", v_option__129818, v_scored__130162, v_state);
                    }
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.dvg_observer_options_build.1641", v_action__129294, v_state, v_viewer)))
                    {
                        v_option__129818 = F_dv_with(v_option__129818, Authored("m60.dvg_intent.literal_0002"), F_dvg_manual_replay_input(v_actor, v_action__129294));
                    }
                    _ = R.push(v_result__129214, v_option__129818);
                }
            }
            v_i__129235 = R.add(v_i__129235, R.number("1"));
        }
        _ = R.phase(Authored("m60.dvg_observer_options_build.literal_0002"), v_viewer);
        return v_result__129214;
    }
}
