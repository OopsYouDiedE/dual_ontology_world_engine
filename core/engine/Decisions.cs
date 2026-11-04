using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Planning / Decisions: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_cached_model(object v_actor, object v_hypothesis)
    {
        object v_model__16342 = F_dv_find(F_dv_find(v_actor, "simulations", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_hypothesis, "id"), new System.Collections.Generic.List<object>(2) { F_dv_pair("fit", R.number("0")), F_dv_pair("status", "not-simulated") });
        if (R.Bool(F_dv_or(R.eq(F_dv_find(v_model__16342, "source_revision", R.sub(R.number("0"), R.number("1"))), F_dv_get(F_dv_get(v_actor, "mind"), "revision")), F_dv_and(F_dv_explanation_model_current(F_dv_get(v_actor, "mind"), v_model__16342), F_dv_same(F_dv_find(v_model__16342, "source_response_models", false), F_dv_find(v_actor, "response_models", new System.Collections.Generic.List<object>(0) { }))))))
        {
            return F_dv_with(v_model__16342, "validated_by_inputs", true);
        }
        else
        {
            if (R.Bool(F_dv_and(F_dvs_argument_valid(v_actor), F_dvs_argument_inputs_current(v_actor))))
            {
                object v_argument__16846 = F_dv_get(v_actor, "argument");
                if (R.Bool(F_dv_and(R.eq(F_dv_find(v_model__16342, "source_revision", R.sub(R.number("0"), R.number("1"))), F_dv_get(v_argument__16846, "source_revision")), F_dv_or(R.eq(F_dv_find(v_model__16342, "event", ""), F_dv_get(v_argument__16846, "model_event")), F_dv_has(F_dv_get(v_argument__16846, "competitor_events"), F_dv_find(v_model__16342, "event", ""))))))
                {
                    return F_dv_with(v_model__16342, "validated_by_argument", F_dv_get(v_argument__16846, "id"));
                }
                else
                {
                    return new System.Collections.Generic.List<object>(3) { F_dv_pair("fit", R.number("0")), F_dv_pair("status", "stale-or-unavailable"), F_dv_pair("source_revision", F_dv_find(v_model__16342, "source_revision", R.sub(R.number("0"), R.number("1")))) };
                }
            }
            else
            {
                return new System.Collections.Generic.List<object>(3) { F_dv_pair("fit", R.number("0")), F_dv_pair("status", "stale-or-unavailable"), F_dv_pair("source_revision", F_dv_find(v_model__16342, "source_revision", R.sub(R.number("0"), R.number("1")))) };
            }
        }
    }

    public static object F_dvs_working_explanation(object v_actor, object v_hypothesis)
    {
        if (R.Bool(F_dvs_factor_candidate_rejected(v_actor, v_hypothesis)))
        {
            return false;
        }
        return F_dv_and(F_dvp_topic(v_actor, v_hypothesis), F_dv_and(R.lt(R.number("0"), R.count(F_dv_find(v_hypothesis, "features", new System.Collections.Generic.List<object>(0) { }))), F_dv_and(R.lt(R.number("0"), R.count(F_dvs_sim_plan(v_hypothesis))), F_dv_and(R.lt(R.number("0"), R.count(F_dv_find(v_hypothesis, "basis", new System.Collections.Generic.List<object>(0) { }))), F_dv_and(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(v_hypothesis, "available_when", F_dv_true())), "true"), R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_hypothesis, "proposition")), "false")))))));
    }

    public static object F_dvs_obligation_actions(object v_actions)
    {
        object v_key__17992 = new System.Collections.Generic.List<object>(1) { "dvs-obligation-actions-v1" };
        object v_cached__18036 = F_dvs_declaration_cache_get(v_actions, v_key__17992);
        if (R.Bool(R.at(v_cached__18036, R.number("0"))))
        {
            return R.at(v_cached__18036, R.number("1"));
        }
        object v_result__18136 = new System.Collections.Generic.List<object>(0) { };
        object v_i__18156 = R.number("0");
        while (R.Bool(R.lt(v_i__18156, R.count(v_actions))))
        {
            if (R.Bool(F_dv_find(R.at(v_actions, v_i__18156), "obligation", false)))
            {
                _ = R.push(v_result__18136, R.at(v_actions, v_i__18156));
            }
            v_i__18156 = R.add(v_i__18156, R.number("1"));
        }
        return F_dvs_declaration_cache_put(v_actions, v_key__17992, v_result__18136);
    }

    public static object F_dvs_scan_obligations(object v_actor, object v_now, object v_costs)
    {
        object v_actions__18418 = F_dvs_obligation_actions(F_dv_get(v_actor, "actions"));
        object v_best__18474 = false;
        object v_earliest__18495 = R.add(v_now, R.number("1"));
        object v_considered__18522 = R.number("0");
        object v_admitted__18545 = R.number("0");
        object v_i__18566 = R.number("0");
        while (R.Bool(R.lt(v_i__18566, R.count(v_actions__18418))))
        {
            object v_action__18617 = R.at(v_actions__18418, v_i__18566);
            object v_due__18649 = F_dv_find(v_action__18617, "obligation_due", R.number("0"));
            if (R.Bool((R.Bool(R.not(R.lt(v_now, v_due__18649))) ? (object)(R.lt(v_due__18649, v_earliest__18495)) : (object)(false))))
            {
                v_considered__18522 = R.add(v_considered__18522, R.number("1"));
                object v_relevant__18790 = F_dvs_sim_refs(F_dv_get(v_action__18617, "precondition"));
                if (R.Bool(F_dvp_eligible(v_actor, v_action__18617, F_dv_get(v_actor, "mind"), v_relevant__18790)))
                {
                    v_admitted__18545 = R.add(v_admitted__18545, R.number("1"));
                    object v_model__18961 = F_dv_get(v_costs, F_dv_get(v_action__18617, "kind"));
                    if (R.Bool(F_dv_action_cost_le(F_dv_get(v_model__18961, "maximum"), F_dv_get(F_dv_get(v_actor, "ledger"), "balances"))))
                    {
                        if (R.Bool((R.Bool(R.not(F_dv_has_key(F_dv_get(v_actor, "policy"), "remaining_game_time"))) ? (object)(true) : (object)(R.not(R.lt(F_dv_get(F_dv_get(v_actor, "policy"), "remaining_game_time"), F_dv_get(F_dv_get(v_model__18961, "maximum"), "time")))))))
                        {
                            v_best__18474 = v_action__18617;
                            v_earliest__18495 = v_due__18649;
                        }
                    }
                }
            }
            v_i__18566 = R.add(v_i__18566, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("action", v_best__18474), F_dv_pair("considered", v_considered__18522), F_dv_pair("admitted", v_admitted__18545) };
    }

    public static object F_dvs_due_obligation(object v_actor, object v_now, object v_costs)
    {
        return F_dv_get(F_dvs_scan_obligations(v_actor, v_now, v_costs), "action");
    }

    public static object F_dvs_obligation_choice(object v_actor, object v_costs)
    {
        object v_scanned__19527 = F_dvs_scan_obligations(v_actor, F_dv_find(v_actor, "decision_time", F_dv_get(F_dv_get(v_actor, "mind"), "time")), v_costs);
        object v_action__19630 = F_dv_get(v_scanned__19527, "action");
        if (R.Bool(R.eq(v_action__19630, false)))
        {
            return false;
        }
        object v_refs__19707 = F_dvs_sim_refs(F_dv_get(v_action__19630, "precondition"));
        object v_reason__19756 = "declared-obligation-due";
        object v_work__19799 = new System.Collections.Generic.List<object>(13) { F_dv_pair("cache_hit", false), F_dv_pair("candidates_considered", F_dv_get(v_scanned__19527, "considered")), F_dv_pair("admission_checks", F_dv_get(v_scanned__19527, "considered")), F_dv_pair("eligible_evaluations", F_dv_get(v_scanned__19527, "admitted")), F_dv_pair("forecasts", R.number("0")), F_dv_pair("forecast_reuses", R.number("0")), F_dv_pair("expanded_nodes", R.number("0")), F_dv_pair("candidate_paths", R.number("1")), F_dv_pair("skipped_expansion", true), F_dv_pair("stop_reason", v_reason__19756), F_dv_pair("candidate_order", "declared-obligation"), F_dv_pair("candidate_order_scans", R.number("0")), F_dv_pair("candidate_order_reuses", R.number("0")) };
        object v_plan__20192 = new System.Collections.Generic.List<object>(6) { F_dv_pair("path", new System.Collections.Generic.List<object>(1) { F_dv_get(v_action__19630, "id") }), F_dv_pair("score", R.number("0")), F_dv_pair("relevant_refs", v_refs__19707), F_dv_pair("score_scope", "declared-obligation"), F_dv_pair("mind_revision", F_dv_get(F_dv_get(v_actor, "mind"), "revision")), F_dv_pair("evaluated_at_game_time", F_dv_find(v_actor, "decision_time", F_dv_get(F_dv_get(v_actor, "mind"), "time"))) };
        return new System.Collections.Generic.List<object>(12) { F_dv_pair("action", v_action__19630), F_dv_pair("score", R.number("0")), F_dv_pair("plan_expansions", R.number("0")), F_dv_pair("plans", new System.Collections.Generic.List<object>(1) { v_plan__20192 }), F_dv_pair("selected_path", new System.Collections.Generic.List<object>(1) { F_dv_get(v_action__19630, "id") }), F_dv_pair("relevant_refs", v_refs__19707), F_dv_pair("stop", v_reason__19756), F_dv_pair("strategy", "declared-obligation-v1"), F_dv_pair("approximation", false), F_dv_pair("time_blocked", R.number("0")), F_dv_pair("dependency_expansions", R.number("0")), F_dv_pair("planning_work", v_work__19799) };
    }

    public static object F_dvs_choose(object v_actor, object v_bindings, object v_costs)
    {
        // Selection is a pure query of this decision's owned inputs. Freeze a
        // read view once so record/list reads do not acquire writable branches.
        v_actor = R.snapshot(v_actor);
        v_bindings = R.snapshot(v_bindings);
        v_costs = R.snapshot(v_costs);
        if (R.Bool(R.eq(F_dv_find(v_actor, "simulation_priority_action", ""), "")))
        {
            object v_duty__20882 = F_dvs_obligation_choice(v_actor, v_costs);
            if (R.Bool(R.not(R.eq(v_duty__20882, false))))
            {
                return F_dv_with_many(v_duty__20882, new System.Collections.Generic.List<object>(3) { F_dv_pair("strategy", "approximate-v1"), F_dv_pair("approximation", true), F_dv_pair("stop", "current-owned-obligation") });
            }
        }
        object v_relevant__21088;
        _ = R.phase("decision-goalrefs-start", "");
        try { v_relevant__21088 = ReadGoalRefs(v_actor); }
        finally { _ = R.phase("decision-goalrefs-end", ""); }
        object v_selected__21128 = false;
        object v_best__21153 = R.number("0");
        object v_plans__21170 = new System.Collections.Generic.List<object>(0) { };
        object v_evaluated__21189 = R.number("0");
        object v_considered__21211 = R.number("0");
        object v_time_blocked__21234 = R.number("0");
        object v_priority__21259 = F_dv_find(v_actor, "simulation_priority_action", "");
        object v_time__21327 = F_dv_find(v_actor, "decision_time", F_dv_get(F_dv_get(v_actor, "mind"), "time"));
        object v_i__21391 = R.number("0");
        object decisionActions = F_dvs_navigation_task_candidates(v_actor);
        if (R.Bool(R.eq(v_priority__21259, "")))
            decisionActions = F_dvs_conversation_candidates(v_actor, decisionActions, v_costs);
        while (R.Bool(R.lt(v_i__21391, R.count(decisionActions))))
        {
            object v_action__21448 = R.at(decisionActions, v_i__21391);
            object eligibilityAction = R.Bool(F_dv_has_key(v_action__21448, "base_action_id")) && R.Bool(F_dv_has_key(v_action__21448, "navigation_task"))
                ? F_dv_with(v_action__21448, "id", F_dv_get(v_action__21448, "base_action_id")) : v_action__21448;
            v_considered__21211 = R.add(v_considered__21211, R.number("1"));
            if (R.Bool(F_dvp_eligible(v_actor, eligibilityAction, F_dv_get(v_actor, "mind"), v_relevant__21088)))
            {
                object v_model__21588 = F_dv_get(v_costs, F_dv_get(v_action__21448, "kind"));
                if (R.Bool(R.lt(F_dv_find(F_dv_get(v_actor, "policy"), "remaining_game_time", F_dv_get(F_dv_get(v_model__21588, "maximum"), "time")), F_dv_get(F_dv_get(v_model__21588, "maximum"), "time"))))
                {
                    v_time_blocked__21234 = R.add(v_time_blocked__21234, R.number("1"));
                }
                else
                {
                    if (R.Bool(F_dv_action_cost_le(F_dv_get(v_model__21588, "maximum"), F_dv_get(F_dv_get(v_actor, "ledger"), "balances"))))
                    {
                        v_evaluated__21189 = R.add(v_evaluated__21189, R.number("1"));
                        object v_scored__21937;
                        _ = R.phase("decision-score-start", "");
                        try { v_scored__21937 = F_dvs_current_action_score(v_actor, eligibilityAction, v_bindings, v_relevant__21088, F_dv_get(v_model__21588, "actual")); }
                        finally { _ = R.phase("decision-score-end", ""); }
                        object v_score__22044 = F_dv_get(v_scored__21937, "total");
                        _ = R.push(v_plans__21170, new System.Collections.Generic.List<object>(7) { F_dv_pair("path", new System.Collections.Generic.List<object>(1) { F_dv_get(v_action__21448, "id") }), F_dv_pair("score", v_score__22044), F_dv_pair("value", new System.Collections.Generic.List<object>(2) { F_dv_pair("value", F_dv_get(v_scored__21937, "action_value")), F_dv_pair("current_score", v_scored__21937) }), F_dv_pair("mind_revision", F_dv_get(F_dv_get(v_actor, "mind"), "revision")), F_dv_pair("evaluated_at_game_time", v_time__21327), F_dv_pair("score_scope", "current-owned-utility"), F_dv_pair("prediction_basis", new System.Collections.Generic.List<object>(0) { }) });
                        bool replaceChoice = R.Bool(R.lt(v_best__21153, v_score__22044)) ||
                            (!R.Bool(R.eq(v_priority__21259, "")) && R.Bool(R.eq(F_dv_get(v_action__21448, "id"), v_priority__21259)));
                        object commitment = F_dv_find(v_actor, "navigation_commitment", false);
                        object candidateTask = F_dv_find(v_action__21448, "navigation_task", false);
                        object selectedTask = R.Bool(R.eq(v_selected__21128, false)) ? false : F_dv_find(v_selected__21128, "navigation_task", false);
                        if (!R.Bool(R.eq(commitment, false)))
                        {
                            object held = F_dv_get(commitment, "task");
                            bool candidateHeld = !R.Bool(R.eq(candidateTask, false)) &&
                                R.Bool(F_dv_same(F_dv_get(held, "task_id"), F_dv_get(candidateTask, "task_id"))) &&
                                R.Bool(F_dv_same(F_dv_get(held, "phase"), F_dv_get(candidateTask, "phase")));
                            bool selectedHeld = !R.Bool(R.eq(selectedTask, false)) &&
                                R.Bool(F_dv_same(F_dv_get(held, "task_id"), F_dv_get(selectedTask, "task_id"))) &&
                                R.Bool(F_dv_same(F_dv_get(held, "phase"), F_dv_get(selectedTask, "phase")));
                            // Candidate construction already admits only the held task
                            // or a declared higher-priority preemption. Compare those
                            // symmetrically: scan order must not restore an old task
                            // after an eligible urgent task has already been selected.
                            bool candidateProtected = !R.Bool(R.eq(candidateTask, false));
                            bool selectedProtected = !R.Bool(R.eq(selectedTask, false));
                            if (candidateProtected != selectedProtected)
                                replaceChoice = candidateProtected;
                            else if (candidateProtected && selectedProtected)
                            {
                                object candidatePriority = F_dv_get(candidateTask, "priority");
                                object selectedPriority = F_dv_get(selectedTask, "priority");
                                if (!R.Bool(R.eq(candidatePriority, selectedPriority)))
                                    replaceChoice = R.Bool(R.lt(selectedPriority, candidatePriority));
                                else if (candidateHeld != selectedHeld)
                                    replaceChoice = candidateHeld;
                            }
                        }
                        if (!R.Bool(R.eq(v_priority__21259, "")) && R.Bool(R.eq(F_dv_get(v_action__21448, "id"), v_priority__21259)))
                            replaceChoice = true;
                        if (replaceChoice)
                        {
                            if (R.Bool((R.Bool(R.eq(v_selected__21128, false)) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_selected__21128, "id"), v_priority__21259))))))
                            {
                                v_selected__21128 = v_action__21448;
                                v_best__21153 = v_score__22044;
                            }
                        }
                    }
                }
            }
            v_i__21391 = R.add(v_i__21391, R.number("1"));
        }
        object v_path__22608 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.not(R.eq(v_selected__21128, false))))
        {
            v_path__22608 = new System.Collections.Generic.List<object>(1) { F_dv_get(v_selected__21128, "id") };
        }
        return new System.Collections.Generic.List<object>(12) { F_dv_pair("action", v_selected__21128), F_dv_pair("score", v_best__21153), F_dv_pair("plan_expansions", R.number("0")), F_dv_pair("dependency_expansions", R.number("0")), F_dv_pair("plans", v_plans__21170), F_dv_pair("selected_path", v_path__22608), F_dv_pair("relevant_refs", v_relevant__21088), F_dv_pair("strategy", "approximate-v1"), F_dv_pair("approximation", true), F_dv_pair("stop", "current-candidates-scanned"), F_dv_pair("time_blocked", v_time_blocked__21234), F_dv_pair("planning_work", new System.Collections.Generic.List<object>(7) { F_dv_pair("candidates_considered", v_considered__21211), F_dv_pair("eligible_evaluations", v_evaluated__21189), F_dv_pair("admission_checks", v_considered__21211), F_dv_pair("expanded_nodes", R.number("0")), F_dv_pair("forecasts", R.number("0")), F_dv_pair("skipped_expansion", true), F_dv_pair("stop_reason", "current-candidates-scanned") }) };
    }

    public static object F_dvs_current_appraisals(object v_actor, object v_events, object v_time, object v_parent)
    {
        object v_declarations__23254 = F_dv_find(F_dv_get(v_actor, "policy"), "current_appraisals", new System.Collections.Generic.List<object>(0) { });
        if (R.Bool(R.eq(R.count(v_declarations__23254), R.number("0"))))
        {
            return v_actor;
        }
        _ = F_dv_domain(F_dv_get(v_actor, "mind"), "mind");
        _ = F_dv_require(R.eq(F_dv_get(F_dv_get(v_actor, "mind"), "subject"), F_dv_get(v_actor, "id")), "Appraisal mind must belong to its evaluator");
        object v_inputs_by_id__23510 = F_dv_find(v_actor, "current_appraisal_inputs", new System.Collections.Generic.List<object>(0) { });
        object v_mind__23580 = F_dv_get(v_actor, "mind");
        object v_cognitive__23606 = F_dv_find(v_actor, "last_cognitive_event", v_parent);
        object v_i__23673 = R.number("0");
        while (R.Bool(R.lt(v_i__23673, R.count(v_declarations__23254))))
        {
            object v_declaration__23729 = R.at(v_declarations__23254, v_i__23673);
            object v_ref__23771 = F_dv_get(v_declaration__23729, "ref");
            _ = F_dv_require((R.Bool(R.eq(F_dv_find(v_ref__23771, "subject", ""), F_dv_get(v_actor, "id"))) ? (object)(R.eq(F_dv_get(v_ref__23771, "facet"), "appraisal")) : (object)(false)), "Current appraisal must be explicitly owned and scoped to appraisal facet");
            _ = F_dv_require(R.lt(R.number("0"), R.count(F_dv_get(v_declaration__23729, "basis"))), "Current appraisal needs a declared personal basis");
            object v_refs__24075 = F_dvs_sim_refs(F_dv_get(v_declaration__23729, "when"));
            object v_inputs__24125 = new System.Collections.Generic.List<object>(0) { };
            object v_j__24149 = R.number("0");
            while (R.Bool(R.lt(v_j__24149, R.count(v_refs__24075))))
            {
                _ = F_dv_require(R.eq(F_dv_find(R.at(v_refs__24075, v_j__24149), "subject", F_dv_get(v_actor, "id")), F_dv_get(v_actor, "id")), "Cross-subject appraisal input");
                _ = F_dv_require(R.not(F_dv_same_local_ref(F_dv_get(v_actor, "id"), R.at(v_refs__24075, v_j__24149), v_ref__23771)), "Current appraisal cannot depend on itself");
                _ = R.push(v_inputs__24125, new System.Collections.Generic.List<object>(2) { F_dv_pair("ref", R.at(v_refs__24075, v_j__24149)), F_dv_pair("cell", F_dv_read(v_mind__23580, R.at(v_refs__24075, v_j__24149))) });
                v_j__24149 = R.add(v_j__24149, R.number("1"));
            }
            object v_hypothesis__24523 = F_dv_find(v_actor, "hypothesis", false);
            object v_hypothesis_id__24584 = "";
            if (R.Bool(R.not(R.eq(v_hypothesis__24523, false))))
            {
                v_hypothesis_id__24584 = F_dv_get(v_hypothesis__24523, "id");
            }
            object v_required__24689 = F_dv_find(v_declaration__23729, "hypothesis_id", v_hypothesis_id__24584);
            object v_hypothesis_event__24765 = F_dv_find(v_actor, "hypothesis_event", "");
            object v_signature__24835 = new System.Collections.Generic.List<object>(3) { F_dv_pair("inputs", v_inputs__24125), F_dv_pair("hypothesis_id", v_hypothesis_id__24584), F_dv_pair("declaration", v_declaration__23729) };
            if (R.Bool(R.not(F_dv_same(v_signature__24835, F_dv_find(v_inputs_by_id__23510, F_dv_get(v_declaration__23729, "id"), false)))))
            {
                object v_truth__25023 = F_dv_truth(v_mind__23580, F_dv_get(v_declaration__23729, "when"));
                if (R.Bool(R.not(R.eq(v_hypothesis_id__24584, v_required__24689))))
                {
                    v_truth__25023 = "false";
                }
                object v_next__25154 = F_dv_unknown();
                if (R.Bool(R.eq(v_truth__25023, "true")))
                {
                    v_next__25154 = F_dv_cell(F_dv_get(v_declaration__23729, "value"), R.number("1"), v_time, F_dv_get(v_declaration__23729, "basis"));
                }
                if (R.Bool(R.eq(v_truth__25023, "false")))
                {
                    v_next__25154 = F_dv_cell(F_dv_get(v_declaration__23729, "otherwise"), R.number("1"), v_time, F_dv_get(v_declaration__23729, "basis"));
                }
                v_next__25154 = F_dv_with_many(v_next__25154, new System.Collections.Generic.List<object>(4) { F_dv_pair("time", v_time), F_dv_pair("evaluation_kind", "current-private-appraisal"), F_dv_pair("evaluation_id", F_dv_get(v_declaration__23729, "id")), F_dv_pair("input_signature", v_signature__24835) });
                object v_before__25580 = F_dv_read(v_mind__23580, v_ref__23771);
                object v_changed__25624 = (R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_before__25580, "known"), F_dv_get(v_next__25154, "known")))) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_get(v_before__25580, "value"), F_dv_get(v_next__25154, "value")))))) ? (object)(true) : (object)(F_dv_find(v_before__25580, "conflicted", false)));
                v_mind__23580 = F_dv_with(F_dv_store(v_mind__23580, v_ref__23771, v_next__25154), "revision", R.add(F_dv_get(v_mind__23580, "revision"), R.number("1")));
                object v_parents__25999 = F_dvs_support_parents(F_dv_get(v_actor, "mind"), v_refs__24075, v_events, v_actor);
                v_parents__25999 = F_dv_explanation_merge(v_parents__25999, new System.Collections.Generic.List<object>(1) { v_cognitive__23606 });
                if (R.Bool(R.not(R.eq(v_hypothesis_event__24765, ""))))
                {
                    v_parents__25999 = F_dv_explanation_merge(v_parents__25999, new System.Collections.Generic.List<object>(1) { v_hypothesis_event__24765 });
                }
                v_cognitive__23606 = F_dvs_event(v_events, "private_appraisal_updated", F_dv_get(v_actor, "id"), v_time, v_parents__25999, new System.Collections.Generic.List<object>(9) { F_dv_pair("id", F_dv_get(v_declaration__23729, "id")), F_dv_pair("ref", v_ref__23771), F_dv_pair("before", v_before__25580), F_dv_pair("after", v_next__25154), F_dv_pair("value_changed", v_changed__25624), F_dv_pair("condition_truth", v_truth__25023), F_dv_pair("inputs", v_inputs__24125), F_dv_pair("hypothesis_event", v_hypothesis_event__24765), F_dv_pair("meaning", "replaceable-personal-evaluation-not-world-fact") });
                v_inputs_by_id__23510 = F_dv_with(v_inputs_by_id__23510, F_dv_get(v_declaration__23729, "id"), v_signature__24835);
            }
            v_i__23673 = R.add(v_i__23673, R.number("1"));
        }
        return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(3) { F_dv_pair("mind", v_mind__23580), F_dv_pair("current_appraisal_inputs", v_inputs_by_id__23510), F_dv_pair("last_cognitive_event", v_cognitive__23606) });
    }

    public static object F_dvs_reason(object v_actor, object v_events, object v_time, object v_parent)
    {
        object v_refreshed__26847 = F_dvs_refresh_derivations(v_actor, v_events, v_time, v_parent);
        if (R.Bool(F_dv_find(v_refreshed__26847, "cognition_blocked", false)))
        {
            return v_refreshed__26847;
        }
        else
        {
            return F_dvs_reason_run(v_refreshed__26847, v_events, v_time, v_parent);
        }
    }
    public static object F_dvs_plan_cache_get(object owner, object key) => GetCachedValue("plan", owner, key);

    public static object F_dvs_plan_cache_put(object owner, object key, object value) => PutCachedValue("plan", owner, key, value);

    public static object F_dvs_declaration_cache_get(object owner, object key) => GetCachedValue("declaration", owner, key);

    public static object F_dvs_declaration_cache_put(object owner, object key, object value) => PutCachedValue("declaration", owner, key, value);

    private static object GetCachedValue(string group, object owner, object key) =>
        R.Bool(R.memo_group_enabled(group)) ? R.memo_get(owner, key) : new List<object> { false, false };

    // Only internal pure-query consumers borrow immutable cache results. The
    // public cache operations retain their independent writable-copy contract.
    private static object ReadCachedValue(string group, object owner, object key) =>
        R.Bool(R.memo_group_enabled(group)) ? R.memo_read(owner, key) : R.snapshot(new List<object> { false, false });

    private static object PutCachedValue(string group, object owner, object key, object value) =>
        R.Bool(R.memo_group_enabled(group)) ? R.memo_put(owner, key, value) : value;
}
