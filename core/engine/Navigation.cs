using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Planning / Navigation: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_sims_autonomy_score(object v_actor, object v_action, object actualSeconds)
    {
        object v_profile__394013 = F_dv_find(F_dv_get(v_actor, "policy"), "sims_autonomy", false);
        if (R.Bool(R.eq(v_profile__394013, false)))
        {
            return R.number("0");
        }
        object v_bonus__394119 = R.number("0");
        object v_room__394137 = "";
        if (R.Bool(F_dv_has_key(v_actor, "location_ref")))
        {
            object v_pos__394201 = F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_actor, "location_ref"));
            if (R.Bool((R.Bool(F_dv_uncontested(v_pos__394201)) ? (object)(R.is_text(F_dv_get(v_pos__394201, "value"))) : (object)(false))))
            {
                v_room__394137 = F_dv_get(v_pos__394201, "value");
            }
        }
        object v_habits__394342 = F_dv_find(v_profile__394013, "implicit_habits", new System.Collections.Generic.List<object>(0) { });
        object v_i__394399 = R.number("0");
        while (R.Bool(R.lt(v_i__394399, R.count(v_habits__394342))))
        {
            object v_habit__394449 = R.at(v_habits__394342, v_i__394399);
            if (R.Bool((R.Bool(R.eq(F_dv_find(v_habit__394449, "trigger", ""), v_room__394137)) ? (object)(R.eq(F_dv_find(v_habit__394449, "kind", ""), F_dv_get(v_action, "kind"))) : (object)(false))))
            {
                v_bonus__394119 = R.add(v_bonus__394119, F_dv_find(v_habit__394449, "bonus", R.number("0")));
            }
            v_i__394399 = R.add(v_i__394399, R.number("1"));
        }
        object v_weights__394644 = F_dv_find(v_profile__394013, "autonomy_weights", new System.Collections.Generic.List<object>(0) { });
        var actionWeights = F_dv_find(v_profile__394013, "action_weights", new List<object>());
        var weight = F_dv_find(actionWeights, F_dv_get(v_action, "kind"), false);
        if (weight is not false)
            v_bonus__394119 = R.add(v_bonus__394119, R.mul(F_dv_get(weight, "scale"),
                F_dv_get(v_weights__394644, F_dv_get(weight, "need"))));
        var perceived = F_dv_find(v_action, "perceived_need_effects", false);
        if (perceived is false) perceived = F_dv_find(F_dv_find(v_profile__394013, "action_predictions", new List<object>()), F_dv_get(v_action, "kind"), false);
        if (perceived is not false)
        {
            static Dictionary<string, double> Numbers(object record) => R.Items(record).ToDictionary(
                pair => R.Text(R.at(pair, 0d)), pair => R.Num(R.at(pair, 1d)), StringComparer.Ordinal);
            var score = AutonomyDriveSolver.Score(
                Numbers(F_dvs_autonomy_needs(v_actor)), Numbers(v_weights__394644), Numbers(perceived),
                R.Num(F_dv_find(v_action, "predicted_seconds", actualSeconds)),
                R.Num(F_dv_find(v_profile__394013, "time_cost", 0d)));
            v_bonus__394119 = R.add(v_bonus__394119, score);
        }
        return v_bonus__394119;
    }

    // A journey declared for an observation remains useful only while that
    // owner's evidence is missing or older than the declared trigger.
    public static object F_dvs_navigation_evidence_pending(object actor, object goal)
    {
        var targets = F_dv_find(goal, "navigation_evidence_refs", new List<object>());
        if (R.Bool(R.eq(R.count(targets), R.number("0")))) return true;
        var goals = F_dvp_refresh_current_evidence(actor,
            F_dv_find(F_dv_get(actor, "policy"), "goals", new List<object>()));
        foreach (var target in R.Items(targets))
        foreach (var evidence in R.Items(goals))
        {
            if (!R.Bool(R.eq(F_dv_find(evidence, "kind", ""), "evidence")) ||
                !R.Bool(F_dv_same(F_dv_find(evidence, "ref", false), target)) ||
                !R.Bool(R.eq(F_dv_truth(F_dv_get(actor, "mind"),
                    F_dv_find(evidence, "available_when", F_dv_true())), "true")) ||
                !R.Bool(F_dvp_evidence_due(actor, evidence))) continue;
            if (!R.Bool(F_dvp_source_supported_since(F_dv_read(F_dv_get(actor, "mind"), target),
                F_dv_find(evidence, "source", "perception"),
                F_dv_find(evidence, "minimum_time", R.number("0"))))) return true;
        }
        return false;
    }

    public static object F_dvs_pending_achieve_goals(object v_actor) => PendingAchieveGoals(v_actor, false);

    private static object ReadPendingAchieveGoals(object actor) => PendingAchieveGoals(R.snapshot(actor), true);

    private static object PendingAchieveGoals(object v_actor, bool readOnly)
    {
        var key = new List<object> { "owned-pending-achieve-goals-v1" };
        var cached = readOnly ? ReadCachedValue("plan", v_actor, key) : F_dvs_plan_cache_get(v_actor, key);
        if (R.Bool(R.at(cached, R.number("0")))) return R.at(cached, R.number("1"));
        object v_goals__395037 = F_dv_find(F_dv_get(v_actor, "policy"), "goals", new System.Collections.Generic.List<object>(0) { });
        object v_result__395088 = new System.Collections.Generic.List<object>(0) { };
        object v_i__395108 = R.number("0");
        while (R.Bool(R.lt(v_i__395108, R.count(v_goals__395037))))
        {
            object v_goal__395157 = R.at(v_goals__395037, v_i__395108);
            if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(F_dv_find(v_goal__395157, "kind", ""), "achieve")) ? (object)(F_dvp_topic(v_actor, v_goal__395157)) : (object)(false))) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(v_goal__395157, "available_when", F_dv_true())), "true")) : (object)(false))) ? (object)(R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_goal__395157, "proposition")), "true"))) : (object)(false))))
            {
                if (R.Bool(F_dvs_navigation_evidence_pending(v_actor, v_goal__395157)))
                    _ = R.push(v_result__395088, v_goal__395157);
            }
            v_i__395108 = R.add(v_i__395108, R.number("1"));
        }
        var result = F_dvs_plan_cache_put(v_actor, key, v_result__395088);
        return readOnly ? R.snapshot(result) : result;
    }

    public static object F_dvs_navigation_guard(object v_condition, object v_location, object v_room)
    {
        object v_op__395660 = F_dv_find(v_condition, "op", "");
        if (R.Bool((R.Bool(R.eq(v_op__395660, "equals")) ? (object)(F_dv_same(F_dv_find(v_condition, "ref", false), v_location)) : (object)(false))))
        {
            if (R.Bool(R.eq(F_dv_find(v_condition, "value", ""), v_room)))
            {
                return F_dv_true();
            }
            return F_dv_not(F_dv_true());
        }
        if (R.Bool(R.eq(v_op__395660, "not")))
        {
            return F_dv_not(F_dvs_navigation_guard(F_dv_get(v_condition, "item"), v_location, v_room));
        }
        if (R.Bool(R.eq(v_op__395660, "all")))
        {
            object v_items__396015 = new System.Collections.Generic.List<object>(0) { };
            object v_i__396038 = R.number("0");
            while (R.Bool(R.lt(v_i__396038, R.count(F_dv_get(v_condition, "items")))))
            {
                _ = R.push(v_items__396015, F_dvs_navigation_guard(R.at(F_dv_get(v_condition, "items"), v_i__396038), v_location, v_room));
                v_i__396038 = R.add(v_i__396038, R.number("1"));
            }
            return F_dv_all(v_items__396015);
        }
        return v_condition;
    }

    public static object F_dvs_navigation_distance(object v_actor, object v_origin, object v_target)
    {
        if (R.Bool(R.eq(v_origin, v_target)))
        {
            return R.number("0");
        }
        object v_key__396348 = new System.Collections.Generic.List<object>(5) { "declared-navigation-distance-v1", F_dv_get(v_actor, "actions"), F_dv_get(v_actor, "location_ref"), v_origin, v_target };
        object v_cached__396449 = F_dvs_plan_cache_get(F_dv_get(v_actor, "mind"), v_key__396348);
        if (R.Bool(R.at(v_cached__396449, R.number("0"))))
        {
            return R.at(v_cached__396449, R.number("1"));
        }
        object v_rooms__396545 = new System.Collections.Generic.List<object>(1) { v_origin };
        object v_distances__396570 = new System.Collections.Generic.List<object>(1) { R.number("0") };
        object v_cursor__396594 = R.number("0");
        while (R.Bool(R.lt(v_cursor__396594, R.count(v_rooms__396545))))
        {
            object v_room__396653 = R.at(v_rooms__396545, v_cursor__396594);
            object v_i__396686 = R.number("0");
            while (R.Bool(R.lt(v_i__396686, R.count(F_dv_get(v_actor, "actions")))))
            {
                object v_edge__396751 = R.at(F_dv_get(v_actor, "actions"), v_i__396686);
                if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_edge__396751, "kind"), "move")) ? (object)(F_dv_has_key(v_edge__396751, "travel")) : (object)(false))) ? (object)(R.eq(F_dv_get(F_dv_get(v_edge__396751, "travel"), "from"), v_room__396653)) : (object)(false))) ? (object)(R.not(R.eq(F_dv_get(F_dv_get(v_edge__396751, "travel"), "to"), v_room__396653))) : (object)(false))) ? (object)(R.lt(R.number("0"), R.count(F_dv_find(v_edge__396751, "source_refs", new System.Collections.Generic.List<object>(0) { })))) : (object)(false))))
                {
                    object v_guard__396969 = F_dvs_navigation_guard(F_dv_get(v_edge__396751, "precondition"), F_dv_get(v_actor, "location_ref"), v_room__396653);
                    if (R.Bool((R.Bool(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), v_guard__396969), "true")) ? (object)(R.not(F_dv_has(v_rooms__396545, F_dv_get(F_dv_get(v_edge__396751, "travel"), "to")))) : (object)(false))))
                    {
                        object v_distance__397163 = R.add(R.at(v_distances__396570, v_cursor__396594), R.number("1"));
                        if (R.Bool(R.eq(F_dv_get(F_dv_get(v_edge__396751, "travel"), "to"), v_target)))
                        {
                            return F_dvs_plan_cache_put(F_dv_get(v_actor, "mind"), v_key__396348, v_distance__397163);
                        }
                        _ = R.push(v_rooms__396545, F_dv_get(F_dv_get(v_edge__396751, "travel"), "to"));
                        _ = R.push(v_distances__396570, v_distance__397163);
                    }
                }
                v_i__396686 = R.add(v_i__396686, R.number("1"));
            }
            v_cursor__396594 = R.add(v_cursor__396594, R.number("1"));
        }
        return F_dvs_plan_cache_put(F_dv_get(v_actor, "mind"), v_key__396348, R.sub(R.number("0"), R.number("1")));
    }

    private static object GoalNavigationTargets(object v_actor, object v_pending_achieve)
    {
        v_actor = R.snapshot(v_actor);
        v_pending_achieve = R.snapshot(v_pending_achieve);
        // Every move candidate queries the same owned opportunity set. The
        // complete actor snapshot and the explicit pending-goal argument own
        // this cache, including mind, requests, policy and action parameters.
        var key = new List<object> { "owned-navigation-target-rooms-v3", v_pending_achieve };
        var cached = ReadCachedValue("plan", v_actor, key);
        if (R.Bool(R.at(cached, R.number("0")))) return R.at(cached, R.number("1"));
        // This declaration selects the derived navigation scope. Actual
        // actions still undergo choose's ordinary eligibility and cost checks;
        // unselected hypothetical destinations need no additional admission.
        object selected = new List<object>();
        bool hasSelection = false;
        foreach (var goal in R.Items(F_dv_find(F_dv_get(v_actor, "policy"), "goals", new List<object>())))
        {
            if (!R.Bool(F_dv_has_key(goal, "navigation_intention"))) continue;
            var proposition = F_dv_get(goal, "proposition");
            var positive = R.lt(R.number("0"), F_dv_find(goal, "weight", R.number("1")));
            var available = F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(goal, "available_when", F_dv_true()));
            var based = R.lt(R.number("0"), R.count(F_dv_find(goal, "basis", new List<object>())));
            if (!R.Bool(positive) || !R.Bool(based) || !R.Bool(R.eq(available, "true")) ||
                !R.Bool(R.eq(F_dv_find(goal, "kind", ""), "achieve")) ||
                !R.Bool(R.eq(F_dv_find(proposition, "op", ""), "equals"))) continue;
            if (!R.Bool(F_dv_same(F_dv_get(proposition, "ref"), F_dv_get(v_actor, "location_ref")))) continue;
            var room = F_dvp_extract_required_room(proposition, F_dv_get(v_actor, "location_ref"));
            if (R.Bool(R.eq(room, ""))) continue;
            hasSelection = true;
            if (!R.Bool(F_dv_has(selected, room))) _ = R.push(selected, room);
        }
        if (hasSelection) return R.snapshot(F_dvs_plan_cache_put(v_actor, key, selected));
        object v_target_rooms__397793 = new System.Collections.Generic.List<object>(0) { };
        object v_i__397819 = R.number("0");
        while (R.Bool(R.lt(v_i__397819, R.count(v_pending_achieve))))
        {
            object v_room__397878 = F_dvp_extract_required_room(F_dv_get(R.at(v_pending_achieve, v_i__397819), "proposition"), F_dv_get(v_actor, "location_ref"));
            var weight = F_dv_find(R.at(v_pending_achieve, v_i__397819), "weight", R.number("1"));
            if (R.Bool(R.lt(R.number("0"), weight)) && R.Bool((R.Bool(R.not(R.eq(v_room__397878, ""))) ? (object)(R.not(F_dv_has(v_target_rooms__397793, v_room__397878))) : (object)(false))))
            {
                _ = R.push(v_target_rooms__397793, v_room__397878);
            }
            v_i__397819 = R.add(v_i__397819, R.number("1"));
        }
        object v_actions__398087 = F_dv_find(v_actor, "actions", new System.Collections.Generic.List<object>(0) { });
        object? relevant = null;
        v_i__397819 = R.number("0");
        while (R.Bool(R.lt(v_i__397819, R.count(v_actions__398087))))
        {
            object v_other__398182 = R.at(v_actions__398087, v_i__397819);
            if (R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_other__398182, "kind"), "move"))) ? (object)((R.Bool((R.Bool(F_dvp_communication_progress(v_actor, v_other__398182)) ? (object)(true) : (object)(R.lt(R.number("0"), F_dv_get(F_dvp_achievement_pending_work(v_actor, v_other__398182, v_pending_achieve), "value"))))) ? (object)(true) : (object)((R.Bool(F_dv_has_key(v_other__398182, "response_topic")) ? (object)(R.lt(R.number("0"), R.count(F_dvs_pending_request(v_actor, v_other__398182)))) : (object)(false))))) : (object)(false))))
            {
                object v_req_room__398463 = F_dvp_extract_required_room(F_dv_get(v_other__398182, "precondition"), F_dv_get(v_actor, "location_ref"));
                if (R.Bool((R.Bool(R.not(R.eq(v_req_room__398463, ""))) ? (object)(R.not(F_dv_has(v_target_rooms__397793, v_req_room__398463))) : (object)(false))))
                {
                    object v_guarded__398947 = F_dv_with(v_other__398182, "precondition", F_dvs_navigation_guard(F_dv_get(v_other__398182, "precondition"), F_dv_get(v_actor, "location_ref"), v_req_room__398463));
                    if (R.Bool(F_dvp_eligible(v_actor, v_guarded__398947, F_dv_get(v_actor, "mind"), relevant ??= ReadGoalRefs(v_actor))))
                    {
                        _ = R.push(v_target_rooms__397793, v_req_room__398463);
                    }
                }
            }
            v_i__397819 = R.add(v_i__397819, R.number("1"));
        }
        return R.snapshot(F_dvs_plan_cache_put(v_actor, key, v_target_rooms__397793));
    }

    public static object F_dvs_goal_navigation_score(object v_actor, object v_action, object v_pending_achieve)
    {
        if (R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_action, "kind"), "move"))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_action, "travel"))))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_actor, "location_ref"))))))
        {
            return R.number("0");
        }
        if (R.Bool(R.eq(R.count(F_dv_find(v_action, "source_refs", new System.Collections.Generic.List<object>(0) { })), R.number("0"))))
        {
            return R.number("0");
        }
        object v_target_rooms__397793 = GoalNavigationTargets(v_actor, v_pending_achieve);
        object v_i__397819;
        if (R.Bool(R.eq(R.count(v_target_rooms__397793), R.number("0"))))
        {
            return R.number("0");
        }
        object v_position__399274 = F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_actor, "location_ref"));
        if (R.Bool((R.Bool(R.not(F_dv_uncontested(v_position__399274))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_position__399274, "value"), F_dv_get(F_dv_get(v_action, "travel"), "from")))))))
        {
            return R.number("0");
        }
        object v_dest__399427 = F_dv_get(F_dv_get(v_action, "travel"), "to");
        object v_score__399459 = R.number("0");
        v_i__397819 = R.number("0");
        while (R.Bool(R.lt(v_i__397819, R.count(v_target_rooms__397793))))
        {
            object v_before__399529 = F_dvs_navigation_distance(v_actor, F_dv_get(v_position__399274, "value"), R.at(v_target_rooms__397793, v_i__397819));
            object v_after__399614 = F_dvs_navigation_distance(v_actor, v_dest__399427, R.at(v_target_rooms__397793, v_i__397819));
            if (R.Bool((R.Bool((R.Bool(R.lt(R.number("0"), v_before__399529)) ? (object)(R.not(R.lt(v_after__399614, R.number("0")))) : (object)(false))) ? (object)(R.lt(v_after__399614, v_before__399529)) : (object)(false))))
            {
                object v_progress__399749 = R.number("1.25");
                if (R.Bool(R.eq(v_after__399614, R.number("0"))))
                {
                    v_progress__399749 = R.number("1.5");
                }
                v_score__399459 = F_dv_max(v_score__399459, v_progress__399749);
            }
            v_i__397819 = R.add(v_i__397819, R.number("1"));
        }
        return v_score__399459;
    }

    public static object F_dvs_current_action_score(object v_actor, object v_action, object v_bindings, object v_relevant, object v_actual)
    {
        object v_declared__400147 = F_dv_find(v_action, "simulation_utility", R.number("0"));
        object v_information__400207 = R.number("0");
        object v_scope__400231 = F_dvp_scope(v_actor, v_action, v_bindings);
        object v_i__400282 = R.number("0");
        while (R.Bool(R.lt(v_i__400282, R.count(v_scope__400231))))
        {
            var reference = R.at(v_scope__400231, v_i__400282);
            if (R.Bool(F_dv_has(v_relevant, reference)) &&
                (!R.Bool(F_dv_uncontested(F_dv_read(F_dv_get(v_actor, "mind"), reference))) ||
                 R.Bool(F_dvp_evidence_action_progress(v_actor, v_action, reference, v_bindings))))
            {
                v_information__400207 = R.add(v_information__400207, F_dv_find(F_dv_get(v_actor, "policy"), "information_weight", R.number("1")));
            }
            v_i__400282 = R.add(v_i__400282, R.number("1"));
        }
        object v_private__400525 = F_dvp_private_execution_utility(v_actor, v_action, F_dv_get(v_actor, "mind"));
        object v_owner__400600 = F_dv_get(F_dvp_owner_utility(v_actor, v_action, F_dv_get(v_actor, "mind")), "value");
        object v_pending_achieve__400667 = ReadPendingAchieveGoals(v_actor);
        object achieveResult = F_dvp_achievement_pending_work(v_actor, v_action, v_pending_achieve__400667);
        object v_achieve_work__400726 = R.mul(F_dv_get(achieveResult, "value"), R.number("2"));
        object v_comm_work__400820 = R.number("0");
        if (R.Bool(F_dvp_communication_progress(v_actor, v_action)))
        {
            v_comm_work__400820 = R.number("2");
        }
        object v_nav_work__401073 = R.number("0");
        if (R.Bool(R.eq(v_achieve_work__400726, R.number("0"))))
        {
            v_nav_work__401073 = F_dvs_goal_navigation_score(v_actor, v_action, v_pending_achieve__400667);
        }
        object v_personal__401197 = R.number("0");
        if (R.Bool(F_dv_has_key(v_action, "personal_choice")))
        {
            v_personal__401197 = F_dvp_personal_motivation(v_actor, v_action);
        }
        object v_answer__401322 = R.number("0");
        if (R.Bool(F_dv_has_key(v_action, "answer")))
        {
            v_answer__401322 = F_dvp_answer_reward(v_actor, v_action, F_dv_get(v_actor, "mind"));
        }
        object v_response__401440 = R.number("0");
        if (R.Bool((R.Bool(F_dv_has_key(v_action, "response_topic")) ? (object)(R.lt(R.number("0"), R.count(F_dvs_pending_request(v_actor, v_action)))) : (object)(false))))
        {
            v_response__401440 = R.number("2");
        }
        object v_exploration__401733 = R.number("0");
        if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_action, "kind"), "move")) ? (object)(F_dv_has_key(v_action, "travel")) : (object)(false))) ? (object)(R.eq(v_nav_work__401073, R.number("0"))) : (object)(false))) ? (object)(R.not(F_dv_has(F_dv_find(v_actor, "completed_actions", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_action, "id")))) : (object)(false))))
        {
            if (R.Bool(R.not(F_dv_has(F_dv_find(v_actor, "visited_locations", new System.Collections.Generic.List<object>(0) { }), F_dv_get(F_dv_get(v_action, "travel"), "to")))))
            {
                v_exploration__401733 = F_dv_find(F_dv_get(v_actor, "policy"), "exploration_utility", R.number("1"));
            }
        }
        object v_time__402211 = F_dv_find(v_actor, "decision_time", F_dv_get(F_dv_get(v_actor, "mind"), "time"));
        object v_daytime__402275 = R.sub(v_time__402211, R.mul(R.floor(R.div(v_time__402211, R.number("1440"))), R.number("1440")));
        object v_schedules__402329 = F_dv_find(F_dv_get(v_actor, "policy"), "offscreen_schedules", new System.Collections.Generic.List<object>(0) { });
        object v_schedule_value__402398 = R.number("0");
        v_i__400282 = R.number("0");
        while (R.Bool(R.lt(v_i__400282, R.count(v_schedules__402329))))
        {
            object v_schedule__402474 = R.at(v_schedules__402329, v_i__400282);
            if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_schedule__402474, "action_id"), F_dv_get(v_action, "id"))) ? (object)(R.not(R.lt(v_daytime__402275, F_dv_get(v_schedule__402474, "from")))) : (object)(false))) ? (object)(R.lt(v_daytime__402275, F_dv_get(v_schedule__402474, "to"))) : (object)(false))))
            {
                v_schedule_value__402398 = R.add(v_schedule_value__402398, F_dv_get(v_schedule__402474, "utility"));
            }
            v_i__400282 = R.add(v_i__400282, R.number("1"));
        }
        object v_sims_autonomy__402686 = F_dvs_sims_autonomy_score(v_actor, v_action, R.mul(F_dv_get(v_actual, "time"), 60d));
        var autonomyProfile = F_dv_find(F_dv_get(v_actor, "policy"), "sims_autonomy", false);
        var authoredScale = autonomyProfile is false ? 0d : F_dv_find(autonomyProfile, "score_scale", 0d);
        object v_sims_effective__402749 = R.mul(v_sims_autonomy__402686, F_dv_find(F_dv_get(v_actor, "policy"), "sims_autonomy_scale", authoredScale));
        object v_progress__402838 = R.add(R.add(R.add(R.add(R.add(R.add(R.add(R.add(R.add(R.add(R.add(v_declared__400147, v_private__400525), v_owner__400600), v_achieve_work__400726), v_comm_work__400820), v_nav_work__401073), v_personal__401197), v_answer__401322), v_response__401440), v_exploration__401733), v_schedule_value__402398), v_sims_effective__402749);
        object caseUtility = CaseStrategy.Utility(v_actor, F_dv_get(v_actor, "mind"), v_action);
        v_progress__402838 = R.add(v_progress__402838, caseUtility);
        object v_value__403001 = R.add(v_information__400207, v_progress__402838);
        object v_penalty__403040 = R.mul(F_dv_get(v_actual, "time"), F_dv_find(F_dv_get(v_actor, "policy"), "time_cost", R.number("0.01")));
        object result = new System.Collections.Generic.List<object>(11) { F_dv_pair("action_value", v_value__403001), F_dv_pair("information_component", v_information__400207), F_dv_pair("progress_component", v_progress__402838), F_dv_pair("time_penalty", v_penalty__403040), F_dv_pair("total", R.sub(v_value__403001, v_penalty__403040)), F_dv_pair("achievement_goals", F_dv_find(achieveResult, "matched_goals", new List<object>())), F_dv_pair("components", new System.Collections.Generic.List<object>(12) { F_dv_pair("declared", v_declared__400147), F_dv_pair("private", v_private__400525), F_dv_pair("owner", v_owner__400600), F_dv_pair("achieve", v_achieve_work__400726), F_dv_pair("communicate", v_comm_work__400820), F_dv_pair("navigate", v_nav_work__401073), F_dv_pair("personal", v_personal__401197), F_dv_pair("answer", v_answer__401322), F_dv_pair("response", v_response__401440), F_dv_pair("exploration", v_exploration__401733), F_dv_pair("schedule", v_schedule_value__402398), F_dv_pair("sims_autonomy", v_sims_autonomy__402686) }), F_dv_pair("scope", "subject-current-immediate"), F_dv_pair("formula", "action_value - time_penalty"), F_dv_pair("strategy", "approximate-v1"), F_dv_pair("approximation", true) };
        return F_dv_with_many(result, new List<object> {
            F_dv_pair("case_strategy_component", caseUtility),
            F_dv_pair("case_strategy_receipts", CaseStrategy.ForAction(v_actor, F_dv_get(v_actor, "mind"), v_action)
                .Select(choice => (object)new List<object> {
                    F_dv_pair("action", choice.Action), F_dv_pair("ready", choice.Ready),
                    F_dv_pair("utility", choice.Utility), F_dv_pair("theory_hash", choice.TheoryHash),
                    F_dv_pair("basis", choice.Basis.Cast<object>().ToList()),
                    F_dv_pair("evidence_nodes", choice.EvidenceNodes.Cast<object>().ToList())
                }).ToList()) });
    }
}
