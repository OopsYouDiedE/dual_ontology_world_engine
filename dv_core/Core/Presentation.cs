using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Session / Presentation: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_surface_parts(object v_parts)
    {
        if (R.Bool(R.lazyinternal_enabled()))
        {
            return R.snapshot(new System.Collections.Generic.List<object>(2) { F_dv_pair("format", "surface-parts-v1"), F_dv_pair("parts", v_parts) });
        }
        return F_dvs_render_surface(new System.Collections.Generic.List<object>(2) { F_dv_pair("format", "surface-parts-v1"), F_dv_pair("parts", v_parts) });
    }

    public static object F_dvs_render_surface(object v_surface)
    {
        if (R.Bool(R.is_text(v_surface)))
        {
            return v_surface;
        }
        _ = F_dv_require(R.eq(F_dv_find(v_surface, "format", ""), "surface-parts-v1"), "Unknown visible surface representation");
        object v_rendered__369762 = new System.Collections.Generic.List<object>(0) { };
        object v_i__369784 = R.number("0");
        while (R.Bool(R.lt(v_i__369784, R.count(F_dv_get(v_surface, "parts")))))
        {
            _ = R.push(v_rendered__369762, F_dvs_render_surface(R.at(F_dv_get(v_surface, "parts"), v_i__369784)));
            v_i__369784 = R.add(v_i__369784, R.number("1"));
        }
        return R.text_join(v_rendered__369762);
    }

    public static object F_dvs_present_replace(object v_text, object v_token, object v_value)
    {
        object v_result__369998 = "";
        object v_i__370018 = R.number("0");
        while (R.Bool(R.lt(v_i__370018, R.length(v_text))))
        {
            if (R.Bool((R.Bool(R.not(R.lt(R.length(v_text), R.add(v_i__370018, R.length(v_token))))) ? (object)(R.eq(R.slice(v_text, v_i__370018, R.length(v_token)), v_token)) : (object)(false))))
            {
                v_result__369998 = R.cat(v_result__369998, v_value);
                v_i__370018 = R.add(v_i__370018, R.length(v_token));
            }
            else
            {
                v_result__369998 = R.cat(v_result__369998, R.chr(v_text, v_i__370018));
                v_i__370018 = R.add(v_i__370018, R.number("1"));
            }
        }
        return v_result__369998;
    }

    public static object F_dvs_present_lines(object v_state, object v_viewer, object v_presentation)
    {
        if (R.Bool(R.eq(R.count(F_dv_find(v_presentation, "lines", new System.Collections.Generic.List<object>(0) { })), R.number("0"))))
        {
            return new System.Collections.Generic.List<object>(0) { };
        }
        object v_mind__370479 = false;
        object v_i__370500 = R.number("0");
        while (R.Bool(R.lt(v_i__370500, R.count(F_dv_get(v_state, "actors")))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(F_dv_get(v_state, "actors"), v_i__370500), "id"), v_viewer)))
            {
                v_mind__370479 = F_dv_get(R.at(F_dv_get(v_state, "actors"), v_i__370500), "mind");
            }
            v_i__370500 = R.add(v_i__370500, R.number("1"));
        }
        object v_result__370651 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.eq(v_mind__370479, false)))
        {
            return v_result__370651;
        }
        object v_lines__370715 = F_dv_find(v_presentation, "lines", new System.Collections.Generic.List<object>(0) { });
        v_i__370500 = R.number("0");
        while (R.Bool(R.lt(v_i__370500, R.count(v_lines__370715))))
        {
            object v_line__370811 = R.at(v_lines__370715, v_i__370500);
            object v_visible__370839 = R.eq(F_dv_truth(v_mind__370479, F_dv_find(v_line__370811, "when", F_dv_true())), "true");
            object v_text__370920 = F_dv_get(v_line__370811, "template");
            object v_bindings__370953 = F_dv_find(v_line__370811, "bindings", new System.Collections.Generic.List<object>(0) { });
            object v_j__371006 = R.number("0");
            while (R.Bool((R.Bool(v_visible__370839) ? (object)(R.lt(v_j__371006, R.count(v_bindings__370953))) : (object)(false))))
            {
                object v_binding__371078 = R.at(v_bindings__370953, v_j__371006);
                object v_cell__371116 = F_dv_read(v_mind__370479, F_dv_get(v_binding__371078, "ref"));
                if (R.Bool(F_dv_uncontested(v_cell__371116)))
                {
                    object v_value__371207 = R.show(F_dv_get(v_cell__371116, "value"));
                    object v_labels__371255 = F_dv_find(v_binding__371078, "labels", new System.Collections.Generic.List<object>(0) { });
                    object v_display__371315 = F_dv_find(v_labels__371255, v_value__371207, v_value__371207);
                    if (R.Bool((R.Bool((R.Bool(R.lt(R.number("0"), R.count(v_labels__371255))) ? (object)(R.not(F_dv_has_key(v_labels__371255, v_value__371207))) : (object)(false))) ? (object)(F_dv_has_key(v_binding__371078, "unmapped_fallback")) : (object)(false))))
                    {
                        v_display__371315 = F_dv_get(v_binding__371078, "unmapped_fallback");
                    }
                    if (R.Bool((R.Bool(R.eq(F_dv_find(v_binding__371078, "format", ""), "game-clock")) ? (object)(R.is_numeric_value(F_dv_get(v_cell__371116, "value"))) : (object)(false))))
                    {
                        object v_minute__371658 = R.floor(F_dv_get(v_cell__371116, "value"));
                        object v_day__371712 = R.floor(R.div(v_minute__371658, R.number("1440")));
                        object v_daytime__371766 = R.sub(v_minute__371658, R.mul(v_day__371712, R.number("1440")));
                        object v_hour__371820 = R.floor(R.div(v_daytime__371766, R.number("60")));
                        object v_rest__371874 = R.sub(v_daytime__371766, R.mul(v_hour__371820, R.number("60")));
                        object v_clock_minute__371925 = R.show(v_rest__371874);
                        if (R.Bool(R.lt(v_rest__371874, R.number("10"))))
                        {
                            v_clock_minute__371925 = R.cat("0", v_clock_minute__371925);
                        }
                        v_display__371315 = R.cat("第", R.cat(R.show(R.add(v_day__371712, R.number("1"))), R.cat("日 ", R.cat(R.show(v_hour__371820), R.cat(":", v_clock_minute__371925)))));
                    }
                    v_text__370920 = F_dvs_present_replace(v_text__370920, R.cat("{", R.cat(F_dv_get(v_binding__371078, "name"), "}")), v_display__371315);
                }
                else
                {
                    if (R.Bool((R.Bool(F_dv_has_key(v_binding__371078, "fallback")) ? (object)(R.is_text(F_dv_get(v_binding__371078, "fallback"))) : (object)(false))))
                    {
                        v_text__370920 = F_dvs_present_replace(v_text__370920, R.cat("{", R.cat(F_dv_get(v_binding__371078, "name"), "}")), F_dv_get(v_binding__371078, "fallback"));
                    }
                    else
                    {
                        v_visible__370839 = false;
                    }
                }
                v_j__371006 = R.add(v_j__371006, R.number("1"));
            }
            if (R.Bool(v_visible__370839))
            {
                _ = R.push(v_result__370651, v_text__370920);
            }
            v_i__370500 = R.add(v_i__370500, R.number("1"));
        }
        return v_result__370651;
    }

    public static object F_dvs_session_intent(object v_state)
    {
        object v_pending__372706 = F_dv_find(v_state, "pending", false);
        if (R.Bool(R.eq(v_pending__372706, false)))
        {
            if (R.Bool(F_dv_find(v_state, "awaiting_player", false)))
            {
                return new System.Collections.Generic.List<object>(4) { F_dv_pair("status", "awaiting_input"), F_dv_pair("active", true), F_dv_pair("game_time", F_dv_get(F_dv_get(v_state, "physical"), "time")), F_dv_pair("options", F_dvs_player_options(R.at(F_dv_get(v_state, "actors"), F_dv_get(v_state, "cursor")))) };
            }
            return new System.Collections.Generic.List<object>(3) { F_dv_pair("status", "idle"), F_dv_pair("active", F_dvs_session_active(v_state)), F_dv_pair("game_time", F_dv_get(F_dv_get(v_state, "physical"), "time")) };
        }
        return new System.Collections.Generic.List<object>(8) { F_dv_pair("status", "prepared"), F_dv_pair("actor", F_dv_get(v_pending__372706, "id")), F_dv_pair("action_event", F_dv_get(v_pending__372706, "action_event")), F_dv_pair("action", F_dv_get(v_pending__372706, "action")), F_dv_pair("bounds", F_dv_get(F_dv_get(v_pending__372706, "quote"), "bounds")), F_dv_pair("execution_cost", F_dv_get(F_dv_get(v_pending__372706, "model"), "actual")), F_dv_pair("game_time", F_dv_get(v_pending__372706, "time")), F_dv_pair("activity_interruption", F_dv_find(v_pending__372706, "activity_interruption", false)) };
    }

    public static object F_dvs_session_presentation(object v_state, object v_viewer)
    {
        v_state = R.snapshot(v_state);
        object v_i__373466 = R.sub(R.count(F_dv_get(v_state, "events")), R.number("1"));
        object v_perceived__373505 = false;
        object v_heard__373531 = false;
        object v_judgment__373553 = false;
        while (R.Bool(R.not(R.lt(v_i__373466, R.number("0")))))
        {
            object v_event__373600 = R.at(F_dv_get(v_state, "events"), v_i__373466);
            if (R.Bool((R.Bool(R.eq(F_dv_get(v_event__373600, "actor"), v_viewer)) ? (object)((R.Bool(R.eq(F_dv_get(v_event__373600, "type"), "answer_accepted")) ? (object)(true) : (object)(R.eq(F_dv_get(v_event__373600, "type"), "answer_rejected")))) : (object)(false))))
            {
                v_judgment__373553 = new System.Collections.Generic.List<object>(5) { F_dv_pair("status", F_dv_get(v_event__373600, "type")), F_dv_pair("event_id", F_dv_get(v_event__373600, "id")), F_dv_pair("game_time", F_dv_get(v_event__373600, "time")), F_dv_pair("reason", F_dv_find(F_dv_get(v_event__373600, "data"), "reason", "")), F_dv_pair("stage", F_dv_find(F_dv_get(v_event__373600, "data"), "stage", "")) };
            }
            if (R.Bool((R.Bool(R.eq(F_dv_get(v_event__373600, "actor"), v_viewer)) ? (object)((R.Bool(R.eq(F_dv_get(v_event__373600, "type"), "listener_parse")) ? (object)(true) : (object)(R.eq(F_dv_get(v_event__373600, "type"), "observation")))) : (object)(false))))
            {
                v_perceived__373505 = true;
            }
            if (R.Bool((R.Bool(R.eq(F_dv_get(v_event__373600, "actor"), v_viewer)) ? (object)((R.Bool(R.eq(F_dv_get(v_event__373600, "type"), "listener_parse")) ? (object)(true) : (object)(R.eq(F_dv_get(v_event__373600, "type"), "speech_received_pending")))) : (object)(false))))
            {
                v_heard__373531 = true;
            }
            if (R.Bool(R.eq(F_dv_get(v_event__373600, "type"), "action_execution")))
            {
                object v_action__374243 = F_dv_get(F_dv_get(v_event__373600, "data"), "action");
                object v_execution__374286 = F_dv_get(F_dv_get(F_dv_get(v_event__373600, "data"), "receipt"), "execution");
                object v_applied__374343 = R.eq(F_dv_get(v_execution__374286, "status"), "applied");
                object v_visible__374399 = (R.Bool(R.eq(F_dv_get(v_event__373600, "actor"), v_viewer)) ? (object)(true) : (object)((R.Bool(v_heard__373531) ? (object)(true) : (object)((R.Bool(v_perceived__373505) ? (object)(R.not(F_dv_has_key(v_action__374243, "speech"))) : (object)(false))))));
                object v_surface__374510 = "";
                object v_presentation__374539 = new System.Collections.Generic.List<object>(0) { };
                if (R.Bool((R.Bool(v_applied__374343) ? (object)(v_visible__374399) : (object)(false))))
                {
                    v_surface__374510 = F_dvs_render_surface(F_dv_find(v_action__374243, "surface", F_dv_find(F_dv_find(v_action__374243, "speech", new System.Collections.Generic.List<object>(0) { }), "surface", "")));
                    v_presentation__374539 = F_dv_find(v_action__374243, "presentation", new System.Collections.Generic.List<object>(0) { });
                }
                if (R.Bool((R.Bool(R.not(v_applied__374343)) ? (object)(R.eq(F_dv_get(v_event__373600, "actor"), v_viewer)) : (object)(false))))
                {
                    v_presentation__374539 = F_dv_find(v_action__374243, "failure_presentation", new System.Collections.Generic.List<object>(0) { });
                }
                object v_voice__374933 = "narration";
                if (R.Bool(F_dv_has_key(v_action__374243, "speech")))
                {
                    v_voice__374933 = "dialogue";
                }
                object v_surface_voice__375049 = F_dv_find(v_presentation__374539, "surface_voice", v_voice__374933);
                v_voice__374933 = F_dv_find(v_presentation__374539, "voice", v_voice__374933);
                object v_travel__375185 = new System.Collections.Generic.List<object>(0) { };
                if (R.Bool((R.Bool(v_applied__374343) ? (object)(v_visible__374399) : (object)(false))))
                {
                    v_travel__375185 = F_dv_find(v_action__374243, "travel", new System.Collections.Generic.List<object>(0) { });
                }
                return new System.Collections.Generic.List<object>(18) { F_dv_pair("status", F_dv_get(v_execution__374286, "status")), F_dv_pair("outcome_category", F_dv_find(v_execution__374286, "outcome_category", ExecutionOutcomeCategory(v_execution__374286))), F_dv_pair("failure_class", F_dv_find(v_execution__374286, "failure_class", "")), F_dv_pair("reason", F_dv_find(v_execution__374286, "reason", "")), F_dv_pair("actor", F_dv_get(v_event__373600, "actor")), F_dv_pair("speaker", F_dv_find(v_presentation__374539, "actor", F_dv_get(v_event__373600, "actor"))), F_dv_pair("action_id", F_dv_get(v_action__374243, "id")), F_dv_pair("action_kind", F_dv_get(v_action__374243, "kind")), F_dv_pair("execution_event", F_dv_get(v_event__373600, "id")), F_dv_pair("source_refs", F_dv_find(v_action__374243, "source_refs", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("voice", v_voice__374933), F_dv_pair("surface_voice", v_surface_voice__375049), F_dv_pair("presentation_kind", F_dv_find(v_presentation__374539, "kind", "")), F_dv_pair("game_time", F_dv_get(v_event__373600, "time")), F_dv_pair("visible", (R.Bool(v_applied__374343) ? (object)(v_visible__374399) : (object)(false))), F_dv_pair("surface", v_surface__374510), F_dv_pair("lines", F_dvs_present_lines(v_state, v_viewer, v_presentation__374539)), F_dv_pair("travel", v_travel__375185), F_dv_pair("judgment", v_judgment__373553) };
            }
            v_i__373466 = R.sub(v_i__373466, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("status", "idle"), F_dv_pair("game_time", F_dv_get(F_dv_get(v_state, "physical"), "time")) };
    }
}
