using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Session / Execution: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_session_complete_commit(object v_state, object v_receipt)
    {
        v_state = R.snapshot(v_state);
        object v_story__322654 = F_dv_get(v_state, "story");
        object v_physical__322682 = F_dv_get(v_state, "physical");
        object v_actors__322716 = F_dv_get(v_state, "actors");
        object v_events__322746 = F_dv_clone(F_dv_get(v_state, "events"));
        object v_bindings__322786 = F_dv_get(v_state, "bindings");
        object v_costs__322820 = F_dv_get(v_state, "costs");
        object v_run_start__322848 = F_dv_get(v_state, "run_start");
        object v_duration_budget__322884 = F_dv_get(v_state, "duration_budget");
        object v_horizon__322932 = F_dv_get(v_state, "horizon");
        object v_duration_blocked__322964 = F_dv_get(v_state, "duration_blocked");
        object v_limit__323014 = F_dv_get(v_state, "limit");
        object v_cursor__323042 = F_dv_get(v_state, "cursor");
        object v_executed__323072 = F_dv_get(v_state, "executed");
        object v_idle__323106 = F_dv_get(v_state, "idle");
        object v_status__323132 = F_dv_get(v_state, "status");
        object v_initial__323162 = F_dv_get(v_state, "initial");
        object v_pending__323194 = F_dv_get(v_state, "pending");
        object v_index__323226 = F_dv_get(v_pending__323194, "index");
        object v_actor__323256 = F_dv_get(v_pending__323194, "actor");
        object v_parent__323286 = F_dv_get(v_pending__323194, "parent");
        object v_time__323318 = F_dv_get(v_pending__323194, "time");
        object v_action__323346 = F_dv_get(v_pending__323194, "action");
        object v_id__323378 = F_dv_get(v_pending__323194, "id");
        object v_mind__323402 = F_dv_get(v_pending__323194, "mind");
        object v_model__323430 = F_dv_get(v_pending__323194, "model");
        object v_action_event__323460 = F_dv_get(v_pending__323194, "action_event");
        object v_ledger__323504 = F_dv_get(v_pending__323194, "ledger");
        object v_quote__323536 = F_dv_get(v_pending__323194, "quote");
        object v_choice__323566 = F_dv_get(v_pending__323194, "choice");
        object v_interruption__323598 = F_dv_find(v_pending__323194, "activity_interruption", false);
        _ = F_dv_require(R.eq(F_dv_get(v_receipt, "action_event"), v_action_event__323460), "Completion belongs to another action");
        var hostField = F_dv_find(v_receipt, "field", new List<object>());
        if (R.Bool(F_dv_has_key(hostField, "host_elapsed_seconds")))
        {
            var seconds = R.Num(F_dv_get(hostField, "host_elapsed_seconds"));
            _ = F_dv_require(double.IsFinite(seconds) && seconds >= 0, "Invalid host elapsed time");
            _ = F_dv_require(R.Text(F_dv_find(hostField, "host_elapsed_basis", "")) == "godot-monotonic-prepared-to-receipt", "Invalid host elapsed time scope");
        }
        var motionMeasurement = F_dv_find(hostField, "motion_measurement", new List<object>());
        var motionBasis = R.Text(F_dv_find(motionMeasurement, "basis", ""));
        if (R.Bool(F_dv_has_key(motionMeasurement, "physical_distance_m")))
        {
            var metres = R.Num(F_dv_get(motionMeasurement, "physical_distance_m"));
            _ = F_dv_require(double.IsFinite(metres) && metres >= 0, "Invalid host movement distance");
            var transitions = R.Num(F_dv_find(motionMeasurement, "unmeasured_room_transitions", -1d));
            _ = F_dv_require(motionBasis == "godot-3d-physics-displacement" && double.IsFinite(transitions) && transitions >= 0 && transitions == Math.Floor(transitions), "Invalid host movement scope");
            _ = F_dv_require(R.Bool(F_dv_find(motionMeasurement, "complete", false)) == (transitions == 0), "Inconsistent host movement completeness");
        }
        else if (motionBasis.Length > 0)
            _ = F_dv_require(motionBasis == "room-graph" && !R.Bool(F_dv_find(motionMeasurement, "complete", true)), "Invalid unmeasured movement scope");
        if (R.Bool(F_dvs_action_requires_argument(v_action__323346)))
        {
            _ = F_dv_require((R.Bool(F_dv_same(R.at(v_actors__322716, v_index__323226), v_actor__323256)) ? (object)(F_dv_same(v_physical__322682, F_dv_find(v_pending__323194, "argument_evidence_world", false))) : (object)(false)), "Argument execution state changed after Prepare");
            _ = F_dv_require((R.Bool((R.Bool((R.Bool(F_dv_same(v_mind__323402, F_dv_get(v_actor__323256, "mind"))) ? (object)(R.eq(F_dv_find(v_action__323346, "argument_evidence_revision", R.sub(R.number("0"), R.number("1"))), F_dv_get(F_dv_get(v_actor__323256, "mind"), "revision"))) : (object)(false))) ? (object)(R.eq(F_dv_find(v_action__323346, "argument_evidence_time", R.sub(R.number("0"), R.number("1"))), v_time__323318)) : (object)(false))) ? (object)(R.eq(F_dv_find(v_action__323346, "argument_evidence_mind_time", R.sub(R.number("0"), R.number("1"))), F_dv_get(F_dv_get(v_actor__323256, "mind"), "time"))) : (object)(false)), "Prepared argument evidence boundary mismatch");
        }
        _ = F_dv_require(F_dvs_argument_action_evidence(v_actor__323256, v_action__323346, v_events__322746), "Prepared argument no longer has current paid evidence");
        object v_before__324812 = v_physical__322682;
        object v_result__324838 = false;
        _ = F_dv_require(R.eq(F_dv_get(F_dv_get(v_receipt, "actual"), "time"), F_dv_get(F_dv_get(F_dv_get(v_pending__323194, "model"), "actual"), "time")), "Scheduled receipt duration differs from declared duration");
        v_model__323430 = F_dv_with(v_model__323430, "actual", F_dv_get(v_receipt, "actual"));
        v_parent__323286 = F_dvs_event(v_events__322746, "host_completion", v_id__323378, v_time__323318, F_dv_single(v_parent__323286), v_receipt);
        object v_local_2__325128 = false;
        object v_local_3__325152 = false;
        if (R.Bool(F_dv_has_key(v_action__323346, "speech")))
        {
            v_local_3__325152 = F_dv_and(F_dvs_authority_speech_allowed(v_action__323346, v_physical__322682, v_id__323378), R.eq(F_dvs_speech_truth(F_dv_get(v_actor__323256, "mind"), F_dv_get(v_action__323346, "speech")), "true"));
        }
        else
        {
            v_local_3__325152 = true;
        }
        if (R.Bool((R.Bool(v_local_3__325152) ? (object)(R.eq(F_dvs_owned_decision_completion_selection(v_actor__323256, v_action__323346, v_events__322746, v_parent__323286), false)) : (object)(false))))
        {
            v_local_3__325152 = false;
        }
        var requestContextReady = R.Bool(F_dvp_owned_request_ready(R.at(v_actors__322716, v_index__323226), v_action__323346));
        if (!requestContextReady) v_local_3__325152 = false;
        var speechContactReady = R.Bool(F_dvs_speech_contact_ready(R.at(v_actors__322716, v_index__323226), v_action__323346, v_actors__322716, v_physical__322682));
        if (!speechContactReady) v_local_3__325152 = false;
        var nativeSpatialFailure = NativeSpatialCompletionFailure(v_action__323346, v_receipt, v_id__323378, v_action_event__323460);
        if (nativeSpatialFailure.Length > 0) v_local_3__325152 = false;
        var opportunityFailure = CompletionOpportunityFailure(v_action__323346, v_actor__323256, v_actors__322716, v_physical__322682);
        if (opportunityFailure.Length > 0) v_local_3__325152 = false;
        if (R.Bool(R.not(R.eq(v_interruption__323598, false))))
        {
            _ = F_dv_require((R.Bool(F_dvs_interruptible_wait(v_action__323346)) ? (object)(F_dv_same(F_dv_find(v_quote__323536, "activity_interruption", false), v_interruption__323598)) : (object)(false)), "Interrupted wait quote mismatch");
            _ = F_dv_require((R.Bool((R.Bool(R.eq(F_dv_get(v_interruption__323598, "elapsed"), F_dv_get(F_dv_get(v_model__323430, "actual"), "time"))) ? (object)(R.eq(R.add(F_dv_get(v_quote__323536, "scheduled_start"), F_dv_get(F_dv_get(v_model__323430, "actual"), "time")), F_dv_get(v_interruption__323598, "time"))) : (object)(false))) ? (object)(R.eq(F_dv_get(v_interruption__323598, "time"), v_time__323318)) : (object)(false)), "Interrupted wait elapsed time mismatch");
            v_local_2__325128 = F_dv_action_cost_finish(v_ledger__323504, v_physical__322682, v_quote__323536, F_dv_get(v_model__323430, "actual"), "rejected");
        }
        else
        {
            if (R.Bool(F_dv_or(R.not(R.eq(F_dv_get(v_receipt, "outcome"), "applied")), R.not(F_dv_and(R.eq(F_dv_truth(v_physical__322682, F_dv_find(v_action__323346, "authority_precondition", F_dv_get(v_action__323346, "precondition"))), "true"), v_local_3__325152)))))
            {
                v_local_2__325128 = F_dv_action_cost_finish(v_ledger__323504, v_physical__322682, v_quote__323536, F_dv_get(v_model__323430, "actual"), "rejected");
            }
            else
            {
                if (R.Bool(R.eq(F_dv_find(v_action__323346, "law", ""), "")))
                {
                    v_local_2__325128 = F_dv_action_cost_finish(v_ledger__323504, v_physical__322682, v_quote__323536, F_dv_get(v_model__323430, "actual"), "applied");
                }
                else
                {
                    v_local_2__325128 = F_dv_action_cost_execute(v_ledger__323504, v_physical__322682, v_quote__323536, F_dv_get(v_model__323430, "actual"));
                }
            }
        }
        v_result__324838 = v_local_2__325128;
        if (R.Bool(R.not(R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied"))))
        {
            object v_failure_class__326771 = F_dvs_manual_contact_failure(v_action__323346, v_receipt, v_id__323378);
            object v_reason__326847 = F_dv_get(F_dv_get(v_result__324838, "execution"), "status");
            if (R.Text(v_failure_class__326771).Length == 0)
            {
                var receiptOutcome = R.Text(F_dv_find(v_receipt, "outcome", ""));
                var receiptReason = R.Text(F_dv_find(v_receipt, "reason", ""));
                if (receiptOutcome is "cancelled" || receiptReason is "cancelled" || receiptReason.Contains("cancel", StringComparison.OrdinalIgnoreCase))
                    v_failure_class__326771 = "cancelled";
                else if (receiptOutcome is "character-refused" || receiptReason is "character-refused" || receiptReason.Contains("refus", StringComparison.OrdinalIgnoreCase) || receiptReason.Contains("declin", StringComparison.OrdinalIgnoreCase))
                    v_failure_class__326771 = "character-refused";
                else if (nativeSpatialFailure.Length > 0) v_failure_class__326771 = nativeSpatialFailure;
                else if (!requestContextReady || !speechContactReady) v_failure_class__326771 = "request-context-invalidated";
                else if (opportunityFailure.Length > 0) v_failure_class__326771 = opportunityFailure;
                else if (NativeSpatialRejectionClass(v_action__323346, v_receipt) is { Length: > 0 } nativeClass)
                    v_failure_class__326771 = nativeClass;
                else if (R.Text(F_dv_truth(v_before__324812, F_dv_find(v_action__323346, "authority_precondition", F_dv_get(v_action__323346, "precondition")))) != "true")
                    v_failure_class__326771 = "condition-invalidated";
                else if (receiptReason.Length > 0)
                    v_failure_class__326771 = receiptReason;
            }
            if (R.Bool(R.not(R.eq(v_interruption__323598, false))))
            {
                v_failure_class__326771 = "activity-interrupted";
                v_reason__326847 = "activity-interrupted";
            }
            if (R.Bool(R.not(R.eq(v_failure_class__326771, ""))))
            {
                v_reason__326847 = v_failure_class__326771;
            }
            object actionTarget = F_dv_find(v_action__323346, "target", false);
            if (actionTarget is false) actionTarget = F_dv_find(v_action__323346, "target_id", false);
            if (actionTarget is false)
            {
                var opp = F_dv_find(v_action__323346, "opportunity_requirement", false);
                if (opp is not false)
                {
                    var participants = F_dv_find(opp, "participants", false);
                    if (participants is not false && R.Items(participants).Count > 0)
                        actionTarget = R.Items(participants)[0];
                    else actionTarget = F_dv_find(opp, "location", false);
                }
            }
            if (actionTarget is false)
            {
                var spec = F_dv_find(v_action__323346, "spatial_execution", false);
                if (spec is not false) actionTarget = F_dv_find(spec, "target_id", false);
            }
            if (actionTarget is false)
            {
                var speech = F_dv_find(v_action__323346, "speech", false);
                if (speech is not false)
                {
                    var listeners = F_dv_find(v_action__323346, "listeners", false);
                    if (listeners is not false && R.Items(listeners).Count > 0)
                        actionTarget = R.Items(listeners)[0];
                }
            }
            if (actionTarget is false) actionTarget = "";
            v_result__324838 = F_dv_with(v_result__324838, "execution", F_dv_with_many(F_dv_get(v_result__324838, "execution"), new System.Collections.Generic.List<object>(3) { F_dv_pair("failure_class", v_failure_class__326771), F_dv_pair("reason", v_reason__326847), F_dv_pair("target", actionTarget) }));
        }
        v_result__324838 = F_dv_with(v_result__324838, "execution", F_dv_with(F_dv_get(v_result__324838, "execution"), "outcome_category", ExecutionOutcomeCategory(F_dv_get(v_result__324838, "execution"))));
        v_physical__322682 = F_dv_get(F_dv_get(v_result__324838, "execution"), "world");
        v_time__323318 = F_dv_find(v_physical__322682, "time", v_time__323318);
        v_actor__323256 = F_dv_with(v_actor__323256, "ledger", F_dv_get(v_result__324838, "ledger"));
        v_parent__323286 = F_dvs_event(v_events__322746, "action_execution", v_id__323378, v_time__323318, F_dv_single(v_parent__323286), F_dv_with_many(new System.Collections.Generic.List<object>(6) { F_dv_pair("revision_before", F_dv_get(v_before__324812, "revision")), F_dv_pair("revision_after", F_dv_get(v_physical__322682, "revision")), F_dv_pair("physical_before", v_before__324812), F_dv_pair("physical_after", v_physical__322682), F_dv_pair("receipt", v_result__324838), F_dv_pair("action", v_action__323346) }, new System.Collections.Generic.List<object>(4) { F_dv_pair("ledger_before", v_ledger__323504), F_dv_pair("ledger_after", F_dv_get(v_result__324838, "ledger")), F_dv_pair("quote", v_quote__323536), F_dv_pair("actual", F_dv_get(v_model__323430, "actual")) }));
        object v_execution_event__327796 = v_parent__323286;
        v_physical__322682 = F_dvs_environment(v_physical__322682, v_story__322654, v_events__322746, v_parent__323286);
        if (R.Bool(R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied")))
        {
            v_actor__323256 = F_dvs_record_request(F_dv_with(v_actor__323256, "last_action_event", v_parent__323286), v_action__323346);
        }
        _ = R.print(R.json_stringify(F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("event_id", v_parent__323286), F_dv_pair("actor", v_id__323378), F_dv_pair("action_id", F_dv_get(v_action__323346, "id")), F_dv_pair("game_time", v_time__323318) }, "status", F_dv_get(F_dv_get(v_result__324838, "execution"), "status"))));
        object v_local_4__328208 = false;
        object v_observation_grants__328232 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied")))
        {
            v_observation_grants__328232 = F_dv_find(v_action__323346, "grants", new System.Collections.Generic.List<object>(0) { });
        }
        else
        {
            if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_find(v_receipt, "executor", ""), "formal-model")) ? (object)(R.eq(F_dv_get(v_receipt, "outcome"), "applied")) : (object)(false))) ? (object)(R.eq(F_dv_truth(v_physical__322682, F_dv_find(v_action__323346, "failure_observe_when", F_dv_not(F_dv_true()))), "true")) : (object)(false))))
            {
                v_observation_grants__328232 = F_dv_find(v_action__323346, "failure_grants", new System.Collections.Generic.List<object>(0) { });
            }
        }
        v_local_4__328208 = F_dvs_observation_source(F_dv_perceive(v_physical__322682, v_mind__323402, v_bindings__322786, v_observation_grants__328232, v_action_event__323460, v_time__323318), v_action__323346);
        object v_observations__329004 = F_dvs_receipt_clock_observations(v_story__322654, v_mind__323402, v_bindings__322786, v_local_4__328208, v_action_event__323460, v_execution_event__327796, F_dv_get(F_dv_get(v_result__324838, "execution"), "time"));
        object v_profile__329144 = F_dvs_observation_profile(v_actor__323256);
        object v_observed_mind__329193 = F_dv_get(F_dv_update_mind(v_mind__323402, v_observations__329004, F_dv_get(v_mind__323402, "revision"), v_profile__329144), "world");
        v_actor__323256 = F_dv_with(v_actor__323256, "mind", F_dv_with(v_observed_mind__329193, "time", F_dv_max(F_dv_find(v_observed_mind__329193, "time", R.number("0")), F_dv_get(F_dv_get(v_result__324838, "execution"), "time"))));
        if (R.Bool(R.lt(R.number("0"), R.count(v_observations__329004))))
        {
            v_parent__323286 = F_dvs_event(v_events__322746, "observation", v_id__323378, v_time__323318, F_dvs_cognitive_parents(v_actor__323256, F_dv_single(v_parent__323286)), F_dv_with_many(new System.Collections.Generic.List<object>(6) { F_dv_pair("observations", v_observations__329004), F_dv_pair("revision_before", F_dv_get(v_mind__323402, "revision")), F_dv_pair("revision_after", F_dv_get(F_dv_get(v_actor__323256, "mind"), "revision")), F_dv_pair("mind_before", v_mind__323402), F_dv_pair("mind_after", F_dv_get(v_actor__323256, "mind")), F_dv_pair("action", v_action__323346) }, new System.Collections.Generic.List<object>(3) { F_dv_pair("bindings", v_bindings__322786), F_dv_pair("grants", v_observation_grants__328232), F_dv_pair("execution_id", v_action_event__323460) }));
        }
        if (R.Bool(R.lt(R.number("0"), R.count(v_observations__329004))))
        {
            v_actor__323256 = F_dv_with(v_actor__323256, "last_cognitive_event", v_parent__323286);
        }
        v_actor__323256 = F_dvs_complete_cognitive_effects(v_actor__323256, v_action__323346, R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied"), v_events__322746, v_execution_event__327796, v_time__323318);
        v_actor__323256 = F_dvs_complete_norm(v_actor__323256, v_action__323346, R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied"), v_events__322746, v_time__323318, v_execution_event__327796, v_parent__323286);
        v_actor__323256 = F_dvs_complete_advice(v_actor__323256, v_action__323346, R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied"), v_events__322746, v_time__323318, v_execution_event__327796);
        v_actor__323256 = F_dvs_complete_owned_decision(v_actor__323256, v_action__323346, R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied"), v_events__322746, v_execution_event__327796, v_time__323318);
        v_actor__323256 = F_dvs_complete_player_influences(v_actor__323256, v_action__323346, v_before__324812, v_physical__322682, R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied"), v_events__322746, v_execution_event__327796, v_time__323318);
        if (R.Bool(F_dv_and(F_dv_has_key(v_action__323346, "answer"), R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied"))))
        {
            object v_judgment__330661 = F_dvs_answer(F_dv_find(v_story__322654, "formal_answers", new System.Collections.Generic.List<object>(0) { }), v_action__323346, v_physical__322682, F_dv_get(v_actor__323256, "mind"), v_bindings__322786, v_events__322746);
            object v_stage__330781 = "";
            object v_new_level__330804 = R.number("0");
            object v_current_level_cell__330830 = false;
            object v_current_level__330869 = R.number("0");
            object v_is_higher__330899 = false;
            v_parent__323286 = F_dvs_event(v_events__322746, "answer_submitted", v_id__323378, v_time__323318, F_dv_single(v_parent__323286), new System.Collections.Generic.List<object>(6) { F_dv_pair("action", v_action__323346), F_dv_pair("mind_revision", F_dv_get(F_dv_get(v_actor__323256, "mind"), "revision")), F_dv_pair("physical_revision", F_dv_get(v_physical__322682, "revision")), F_dv_pair("mind", F_dv_get(v_actor__323256, "mind")), F_dv_pair("physical", v_physical__322682), F_dv_pair("personal_accepted_answers", F_dv_find(v_actor__323256, "accepted_answers", new System.Collections.Generic.List<object>(0) { })) });
            v_stage__330781 = F_dv_get(v_judgment__330661, "stage");
            v_new_level__330804 = F_dv_get(v_judgment__330661, "truth_level");
            v_current_level_cell__330830 = F_dv_unknown();
            if (R.Bool(R.not(R.eq(F_dv_get(v_judgment__330661, "progress_ref"), false))))
            {
                v_current_level_cell__330830 = F_dv_read(v_physical__322682, F_dv_get(v_judgment__330661, "progress_ref"));
            }
            object v_local_5__331461 = false;
            if (R.Bool(F_dv_get(v_current_level_cell__330830, "known")))
            {
                v_local_5__331461 = F_dv_get(v_current_level_cell__330830, "value");
            }
            else
            {
                v_local_5__331461 = R.number("0");
            }
            v_current_level__330869 = v_local_5__331461;
            v_is_higher__330899 = R.lt(F_dv_find(F_dv_find(v_actor__323256, "accepted_answers", new System.Collections.Generic.List<object>(0) { }), v_stage__330781, R.number("0")), v_new_level__330804);
            if (R.Bool(F_dv_and(F_dv_get(v_judgment__330661, "accepted"), R.not(v_is_higher__330899))))
            {
                v_judgment__330661 = F_dv_with_many(v_judgment__330661, new System.Collections.Generic.List<object>(2) { F_dv_pair("accepted", false), F_dv_pair("reason", "already-accepted-personal-answer") });
            }
            else
            {
                v_judgment__330661 = F_dv_with(v_judgment__330661, "accepted", F_dv_and(F_dv_get(v_judgment__330661, "accepted"), v_is_higher__330899));
            }
            object v_local_6__332008 = false;
            if (R.Bool(F_dv_get(v_judgment__330661, "accepted")))
            {
                v_local_6__332008 = "answer_accepted";
            }
            else
            {
                v_local_6__332008 = "answer_rejected";
            }
            v_parent__323286 = F_dvs_event(v_events__322746, v_local_6__332008, v_id__323378, v_time__323318, F_dv_single(v_parent__323286), v_judgment__330661);
            if (R.Bool(F_dv_and(F_dv_get(v_judgment__330661, "accepted"), R.not(R.eq(F_dv_get(v_judgment__330661, "accepted_law"), "")))))
            {
                object v_transition__332322 = F_dv_execute(v_physical__322682, F_dv_get(v_judgment__330661, "accepted_law"), R.cat(v_action_event__323460, "-accepted"), v_time__323318, F_dv_get(v_physical__322682, "revision"));
                if (R.Bool(R.eq(F_dv_get(v_transition__332322, "status"), "applied")))
                {
                    v_physical__322682 = F_dv_get(v_transition__332322, "world");
                    object v_local_7__332552 = false;
                    if (R.Bool(R.lt(v_current_level__330869, v_new_level__330804)))
                    {
                        v_local_7__332552 = v_new_level__330804;
                    }
                    else
                    {
                        v_local_7__332552 = v_current_level__330869;
                    }
                    v_physical__322682 = F_dv_store(v_physical__322682, F_dv_get(v_judgment__330661, "progress_ref"), F_dv_field(v_local_7__332552));
                    v_physical__322682 = F_dv_store(v_physical__322682, F_dv_get(v_judgment__330661, "completion_ref"), F_dv_field(true));
                    v_actor__323256 = F_dv_with(v_actor__323256, "accepted_answers", F_dv_with(F_dv_find(v_actor__323256, "accepted_answers", new System.Collections.Generic.List<object>(0) { }), v_stage__330781, v_new_level__330804));
                    v_parent__323286 = F_dvs_event(v_events__322746, "question_transition", v_id__323378, v_time__323318, F_dv_single(v_parent__323286), v_transition__332322);
                    v_actors__322716 = F_dvs_replace(v_actors__322716, v_index__323226, v_actor__323256);
                    v_actors__322716 = F_dvs_public_grants(v_actors__322716, v_index__323226, F_dv_with_many(v_action__323346, new System.Collections.Generic.List<object>(3) { F_dv_pair("speech", new System.Collections.Generic.List<object>(2) { F_dv_pair("channel", "formal-receipt"), F_dv_pair("literal_ast", F_dv_true()) }), F_dv_pair("listeners", F_dv_get(v_judgment__330661, "accepted_listeners")), F_dv_pair("public_grants", F_dv_get(v_judgment__330661, "accepted_public_grants")) }), v_physical__322682, v_bindings__322786, v_events__322746, v_time__323318, v_parent__323286, R.cat(v_action_event__323460, "-accepted"));
                    v_actor__323256 = R.at(v_actors__322716, v_index__323226);
                }
            }
        }
        object v_replay_question__333553 = "";
        if (R.Bool((R.Bool(R.eq(F_dv_get(v_action__323346, "kind"), "replay")) ? (object)(R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied")) : (object)(false))))
        {
            object v_preparation__333659 = F_dvs_prepare_replay_argument(v_actor__323256, v_action__323346, v_events__322746, v_time__323318, v_parent__323286);
            v_actor__323256 = F_dv_get(v_preparation__333659, "actor");
            v_replay_question__333553 = F_dv_get(v_preparation__333659, "question");
            if (R.Bool(F_dv_find(v_actor__323256, "cognition_blocked", false)))
            {
                _ = F_dvs_event(v_events__322746, "replay_deferred", v_id__323378, v_time__323318, F_dvs_cognitive_parents(v_actor__323256, new System.Collections.Generic.List<object>(1) { v_parent__323286 }), new System.Collections.Generic.List<object>(4) { F_dv_pair("action_id", F_dv_get(v_action__323346, "id")), F_dv_pair("reason", "argument-revocation-pending"), F_dv_pair("question", v_replay_question__333553), F_dv_pair("reason_budget", F_dv_find(F_dv_get(v_actor__323256, "policy"), "reason_budget", R.number("0"))) });
            }
        }
        if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_action__323346, "kind"), "replay")) ? (object)(R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied")) : (object)(false))) ? (object)(R.not(F_dv_find(v_actor__323256, "cognition_blocked", false))) : (object)(false))) ? (object)(R.not(F_dvs_replay_has_available_candidate(v_actor__323256))) : (object)(false))))
        {
            _ = F_dvs_event(v_events__322746, "replay_deferred", v_id__323378, v_time__323318, new System.Collections.Generic.List<object>(1) { v_parent__323286 }, new System.Collections.Generic.List<object>(3) { F_dv_pair("action_id", F_dv_get(v_action__323346, "id")), F_dv_pair("reason", "no-executable-plan-with-known-external-inputs"), F_dv_pair("model_budget", F_dv_find(F_dv_get(v_actor__323256, "policy"), "model_budget", R.number("0"))) });
        }
        if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_action__323346, "kind"), "replay")) ? (object)(R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied")) : (object)(false))) ? (object)(R.not(F_dv_find(v_actor__323256, "cognition_blocked", false))) : (object)(false))) ? (object)(F_dvs_replay_has_available_candidate(v_actor__323256)) : (object)(false))))
        {
            object v_candidates__334693 = F_dv_find(F_dv_get(v_actor__323256, "policy"), "hypotheses", new System.Collections.Generic.List<object>(0) { });
            object v_manual_id__334758 = F_dv_find(v_action__323346, "replay_hypothesis_id", "");
            if (R.Bool(R.not(R.eq(v_manual_id__334758, ""))))
            {
                object v_selected__334858 = F_dvs_manual_candidate(v_actor__323256, v_manual_id__334758);
                v_candidates__334693 = new System.Collections.Generic.List<object>(0) { };
                if (R.Bool(R.not(R.eq(v_selected__334858, false))))
                {
                    v_candidates__334693 = new System.Collections.Generic.List<object>(1) { v_selected__334858 };
                    v_actor__323256 = F_dv_with(v_actor__323256, "last_manual_replay", v_manual_id__334758);
                }
                else
                {
                    _ = F_dvs_event(v_events__322746, "manual_replay_unavailable", v_id__323378, v_time__323318, new System.Collections.Generic.List<object>(1) { v_parent__323286 }, new System.Collections.Generic.List<object>(3) { F_dv_pair("action_id", F_dv_get(v_action__323346, "id")), F_dv_pair("hypothesis_id", v_manual_id__334758), F_dv_pair("reason", "subject-admission-changed") });
                }
            }
            object v_k__335372 = R.number("0");
            v_actor__323256 = F_dv_with(v_actor__323256, "model_probe_cache", new System.Collections.Generic.List<object>(0) { });
            object v_replayed__335446 = new System.Collections.Generic.List<object>(0) { };
            object v_simulations__335472 = F_dv_find(v_actor__323256, "simulations", new System.Collections.Generic.List<object>(0) { });
            object v_coverages__335532 = new System.Collections.Generic.List<object>(0) { };
            object v_factor_rejections__335559 = new System.Collections.Generic.List<object>(0) { };
            v_actor__323256 = F_dv_with_many(v_actor__323256, new System.Collections.Generic.List<object>(2) { F_dv_pair("model_paid_event", v_execution_event__327796), F_dv_pair("policy", F_dv_with(F_dv_get(v_actor__323256, "policy"), "model_work_remaining", F_dv_find(v_model__323430, "work_limit", R.number("0")))) });
            if (R.Bool(R.lt(R.number("0"), R.count(F_dv_find(F_dv_get(v_actor__323256, "policy"), "factor_constraints", new System.Collections.Generic.List<object>(0) { })))))
            {
                object v_prune__336174 = F_dvs_factor_prune(v_actor__323256, v_events__322746, v_time__323318, v_parent__323286);
                object v_certificates__336244 = new System.Collections.Generic.List<object>(0) { };
                object v_c__336278 = R.number("0");
                while (R.Bool(R.lt(v_c__336278, R.count(F_dv_get(v_prune__336174, "certificates")))))
                {
                    _ = R.push(v_certificates__336244, F_dv_with(R.at(F_dv_get(v_prune__336174, "certificates"), v_c__336278), "event", F_dv_get(v_prune__336174, "event")));
                    v_c__336278 = R.add(v_c__336278, R.number("1"));
                }
                v_actor__323256 = F_dv_with_many(v_actor__323256, new System.Collections.Generic.List<object>(4) { F_dv_pair("factor_certificates", v_certificates__336244), F_dv_pair("model_previous_event", F_dv_get(v_prune__336174, "event")), F_dv_pair("last_cognitive_event", F_dv_get(v_prune__336174, "event")), F_dv_pair("policy", F_dv_with_many(F_dv_get(v_actor__323256, "policy"), new System.Collections.Generic.List<object>(2) { F_dv_pair("model_budget", F_dv_get(v_prune__336174, "remaining")), F_dv_pair("model_work_remaining", F_dv_get(v_prune__336174, "work_remaining")) })) });
            }
            while (R.Bool(R.lt(v_k__335372, R.count(v_candidates__334693))))
            {
                object v_candidate__336788 = R.at(v_candidates__334693, v_k__335372);
                object v_coverage_key__336830 = R.cat(F_dv_find(R.at(v_candidates__334693, v_k__335372), "question", F_dv_find(R.at(v_candidates__334693, v_k__335372), "topic", "")), F_dv_find(R.at(v_candidates__334693, v_k__335372), "group", ""));
                if (R.Bool(F_dv_has_key(v_coverages__335532, v_coverage_key__336830)))
                {
                    _ = false;
                }
                else
                {
                    v_coverages__335532 = F_dv_with(v_coverages__335532, v_coverage_key__336830, F_dvs_factor_coverage(v_actor__323256, v_candidate__336788, v_events__322746));
                }
                if (R.Bool(F_dvs_replay_candidate_available(v_actor__323256, v_candidate__336788)))
                {
                    if (R.Bool(F_dvs_factor_covered(v_candidate__336788, F_dv_get(F_dv_get(v_coverages__335532, v_coverage_key__336830), "certificates"))))
                    {
                        _ = R.push(v_factor_rejections__335559, F_dv_get(v_candidate__336788, "id"));
                    }
                    else
                    {
                        if (R.Bool(R.lt(F_dvs_sim_work_budget(v_actor__323256), R.number("1"))))
                        {
                            v_k__335372 = R.count(v_candidates__334693);
                        }
                        else
                        {
                            object v_cached__337566 = F_dv_find(v_simulations__335472, F_dv_get(v_candidate__336788, "id"), false);
                            object v_simulation__337645 = false;
                            object v_local_8__337692 = false;
                            if (R.Bool(R.not(F_dvs_model_recheck_eligible(v_actor__323256, v_candidate__336788, v_cached__337566, v_time__323318))))
                            {
                                v_local_8__337692 = F_dvs_model(v_actor__323256, v_candidate__336788, v_events__322746, v_time__323318, v_parent__323286);
                            }
                            else
                            {
                                object v_recheck__337949 = F_dvs_model_recheck(v_actor__323256, v_cached__337566);
                                _ = F_dvs_event(v_events__322746, "model_recheck", v_id__323378, v_time__323318, F_dvs_model_parent_ids(v_actor__323256, v_parent__323286, F_dv_find(v_cached__337566, "event", "")), new System.Collections.Generic.List<object>(6) { F_dv_pair("actor_before", F_dv_without(v_actor__323256, "simulations")), F_dv_pair("actor_id", F_dv_get(v_actor__323256, "id")), F_dv_pair("actor_revision", F_dv_get(F_dv_get(v_actor__323256, "mind"), "revision")), F_dv_pair("model_before", v_cached__337566), F_dv_pair("hypothesis", v_candidate__336788), F_dv_pair("result", v_recheck__337949) });
                                v_local_8__337692 = v_recheck__337949;
                            }
                            v_simulation__337645 = v_local_8__337692;
                            v_actor__323256 = F_dv_with(v_actor__323256, "model_probe_cache", F_dv_find(v_simulation__337645, "model_probe_cache", F_dv_get(v_actor__323256, "model_probe_cache")));
                            v_simulation__337645 = F_dv_without(v_simulation__337645, "model_probe_cache");
                            v_simulation__337645 = F_dv_with_many(v_simulation__337645, new System.Collections.Generic.List<object>(3) { F_dv_pair("validation_inputs", F_dv_explanation_merge(F_dv_explanation_model_inputs(F_dv_get(v_actor__323256, "mind"), v_simulation__337645), F_dvs_model_observed_inputs(v_simulation__337645))), F_dv_pair("source_adoptions", F_dv_get(F_dv_get(v_actor__323256, "mind"), "adoptions")), F_dv_pair("source_response_models", F_dv_find(v_actor__323256, "response_models", new System.Collections.Generic.List<object>(0) { })) });
                            v_simulation__337645 = F_dv_with_many(v_simulation__337645, new System.Collections.Generic.List<object>(3) { F_dv_pair("source_mind_time", F_dv_get(F_dv_get(v_actor__323256, "mind"), "time")), F_dv_pair("source_hypothesis", v_candidate__336788), F_dv_pair("source_replay_depth", F_dv_find(F_dv_get(v_actor__323256, "policy"), "Replay_Depth", R.count(F_dvs_sim_plan(v_candidate__336788)))) });
                            v_simulations__335472 = F_dv_with(v_simulations__335472, F_dv_get(v_candidate__336788, "id"), F_dv_with_many(v_simulation__337645, new System.Collections.Generic.List<object>(2) { F_dv_pair("event", F_dv_get(R.at(v_events__322746, R.sub(R.count(v_events__322746), R.number("1"))), "id")), F_dv_pair("source_revision", F_dv_get(F_dv_get(v_actor__323256, "mind"), "revision")) }));
                            v_actor__323256 = F_dv_with_many(v_actor__323256, new System.Collections.Generic.List<object>(3) { F_dv_pair("model_previous_event", F_dv_get(R.at(v_events__322746, R.sub(R.count(v_events__322746), R.number("1"))), "id")), F_dv_pair("last_cognitive_event", F_dv_get(R.at(v_events__322746, R.sub(R.count(v_events__322746), R.number("1"))), "id")), F_dv_pair("policy", F_dv_with_many(F_dv_get(v_actor__323256, "policy"), new System.Collections.Generic.List<object>(2) { F_dv_pair("model_budget", F_dv_get(v_simulation__337645, "remaining")), F_dv_pair("model_work_remaining", F_dv_get(v_simulation__337645, "work_remaining")) })) });
                            _ = R.push(v_replayed__335446, v_candidate__336788);
                        }
                    }
                }
                v_k__335372 = R.add(v_k__335372, R.number("1"));
            }
            v_actor__323256 = F_dv_without(F_dv_with(v_actor__323256, "simulations", v_simulations__335472), "model_probe_cache");
            if (R.Bool(R.eq(v_manual_id__334758, "")))
            {
                v_actor__323256 = F_dv_with(v_actor__323256, "factor_rejections", new System.Collections.Generic.List<object>(2) { F_dv_pair("source_revision", F_dv_get(F_dv_get(v_actor__323256, "mind"), "revision")), F_dv_pair("ids", v_factor_rejections__335559) });
            }
            v_actor__323256 = F_dvs_accept_paid_arguments(v_actor__323256, v_replayed__335446, v_replay_question__333553, v_events__322746, v_time__323318, v_parent__323286);
            v_actor__323256 = F_dvs_materialize_arguments(v_actor__323256, v_events__322746);
        }
        if (R.Bool(F_dv_has_key(v_actor__323256, "location_ref")))
        {
            object v_cell__340172 = F_dv_read(v_physical__322682, F_dv_get(v_actor__323256, "location_ref"));
            if (R.Bool(F_dv_get(v_cell__340172, "known")))
            {
                v_actor__323256 = F_dv_with(v_actor__323256, "location", F_dv_get(v_cell__340172, "value"));
            }
        }
        if (R.Bool(F_dv_has_key(v_action__323346, "answer")))
        {
            v_actor__323256 = F_dv_with(v_actor__323256, "answer_attempt_signatures", F_dv_with(F_dv_find(v_actor__323256, "answer_attempt_signatures", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_action__323346, "id"), F_dvp_input_signature(v_actor__323256, v_action__323346, F_dv_get(v_choice__323566, "relevant_refs"))));
        }
        if (R.Bool(R.eq(F_dv_get(v_action__323346, "kind"), "replay")))
        {
            v_actor__323256 = F_dv_with(v_actor__323256, "replay_attempt_signatures", F_dv_with(F_dv_find(v_actor__323256, "replay_attempt_signatures", new System.Collections.Generic.List<object>(0) { }), F_dvs_replay_attempt_key(v_action__323346), F_dvs_replay_input_signature(v_actor__323256, v_action__323346, F_dv_get(v_choice__323566, "relevant_refs"))));
            if (R.Bool((R.Bool(R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied")) ? (object)(R.eq(v_id__323378, F_dv_find(v_state, "player_actor", ""))) : (object)(false))))
            {
                v_actor__323256 = F_dv_with(v_actor__323256, "manual_replay_refresh", true);
            }
        }
        if (R.Bool((R.Bool(R.eq(F_dv_get(v_action__323346, "kind"), "inspect")) ? (object)(R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied")) : (object)(false))))
        {
            v_actor__323256 = F_dv_with(v_actor__323256, "inspection_attempt_signatures", F_dv_with(F_dv_find(v_actor__323256, "inspection_attempt_signatures", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_action__323346, "id"), F_dvp_inspection_signature(v_actor__323256, v_action__323346, F_dv_get(v_actor__323256, "mind"))));
        }
        object v_retry__341204 = false;
        if (R.Bool((R.Bool(R.not(R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied"))) ? (object)(R.eq(v_id__323378, F_dv_find(v_state, "player_actor", ""))) : (object)(false))))
        {
            object v_failure_class__341320 = F_dvs_manual_contact_failure(v_action__323346, v_receipt, v_id__323378);
            if (R.Bool(R.not(R.eq(v_failure_class__341320, ""))))
            {
                v_retry__341204 = new System.Collections.Generic.List<object>(3) { F_dv_pair("failure_class", v_failure_class__341320), F_dv_pair("action_event", v_action_event__323460), F_dv_pair("source_signature", F_dvp_rejected_signature(v_actor__323256, v_action__323346, F_dv_get(v_actor__323256, "mind"))) };
            }
        }
        v_actor__323256 = F_dv_with(v_actor__323256, "manual_contact_retries", F_dv_with(F_dv_find(v_actor__323256, "manual_contact_retries", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_action__323346, "id"), v_retry__341204));
        if (R.Bool((R.Bool(R.not(R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied"))) ? (object)(R.eq(v_interruption__323598, false)) : (object)(false))))
        {
            v_actor__323256 = F_dv_with(v_actor__323256, "rejected_attempt_signatures", F_dv_with(F_dv_find(v_actor__323256, "rejected_attempt_signatures", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_action__323346, "id"), F_dvp_rejected_signature(v_actor__323256, v_action__323346, F_dv_get(v_actor__323256, "mind"))));
        }
        else
        {
            v_actor__323256 = F_dv_with(v_actor__323256, "rejected_attempt_signatures", F_dv_with(F_dv_find(v_actor__323256, "rejected_attempt_signatures", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_action__323346, "id"), false));
        }
        v_actor__323256 = F_dv_with(v_actor__323256, "attempts", F_dv_with(F_dv_find(v_actor__323256, "attempts", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_action__323346, "id"), F_dv_get(F_dv_get(v_actor__323256, "mind"), "revision")));
        if (R.Bool(R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied")))
        {
            v_actor__323256 = F_dv_with(v_actor__323256, "completed_actions", F_dv_append(F_dv_find(v_actor__323256, "completed_actions", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_action__323346, "id")));
            if (R.Bool((R.Bool(R.eq(F_dv_get(v_action__323346, "kind"), "move")) ? (object)(F_dv_has_key(v_action__323346, "travel")) : (object)(false))))
            {
                object v_position__342456 = F_dv_read(F_dv_get(v_actor__323256, "mind"), F_dv_get(v_actor__323256, "location_ref"));
                if (R.Bool((R.Bool(F_dv_uncontested(v_position__342456)) ? (object)(R.eq(F_dv_get(v_position__342456, "value"), F_dv_get(F_dv_get(v_action__323346, "travel"), "to"))) : (object)(false))))
                {
                    object v_visited__342607 = F_dv_find(v_actor__323256, "visited_locations", new System.Collections.Generic.List<object>(0) { });
                    if (R.Bool(R.not(F_dv_has(v_visited__342607, F_dv_get(F_dv_get(v_action__323346, "travel"), "from")))))
                    {
                        v_visited__342607 = F_dv_append(v_visited__342607, F_dv_get(F_dv_get(v_action__323346, "travel"), "from"));
                    }
                    if (R.Bool(R.not(F_dv_has(v_visited__342607, F_dv_get(F_dv_get(v_action__323346, "travel"), "to")))))
                    {
                        v_visited__342607 = F_dv_append(v_visited__342607, F_dv_get(F_dv_get(v_action__323346, "travel"), "to"));
                    }
                    v_actor__323256 = F_dv_with(v_actor__323256, "visited_locations", v_visited__342607);
                }
            }
        }
        v_actor__323256 = F_dv_with_many(v_actor__323256, new System.Collections.Generic.List<object>(2) { F_dv_pair("used", F_dv_append(F_dv_find(v_actor__323256, "used", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_action__323346, "id"))), F_dv_pair("policy", F_dv_with(F_dv_get(v_actor__323256, "policy"), "budget", R.sub(F_dv_get(F_dv_get(v_actor__323256, "policy"), "budget"), R.number("1")))) });
        v_actors__322716 = F_dvs_replace(v_actors__322716, v_index__323226, v_actor__323256);
        if (R.Bool(F_dv_and(F_dv_has_key(v_action__323346, "public_observers"), R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied"))))
        {
            v_actors__322716 = F_dvs_visible_action_grants(v_actors__322716, v_index__323226, v_action__323346, v_before__324812, v_physical__322682, v_bindings__322786, v_events__322746, v_time__323318, v_parent__323286, v_action_event__323460);
        }
        if (R.Bool(F_dv_and(F_dv_has_key(v_action__323346, "speech"), R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied"))))
        {
            object v_actual_player__343597 = "";
            if (R.Bool(R.eq(v_id__323378, F_dv_find(v_state, "player_actor", ""))))
            {
                v_actual_player__343597 = v_id__323378;
            }
            object v_control__343712 = "autonomous";
            if (R.Bool(R.not(R.eq(v_actual_player__343597, ""))))
            {
                v_control__343712 = "player";
            }
            v_action__323346 = F_dv_with(v_action__323346, "speech", F_dv_with_many(F_dv_get(v_action__323346, "speech"), new System.Collections.Generic.List<object>(4) { F_dv_pair("actual_player_actor", v_actual_player__343597), F_dv_pair("actual_influence_actor", v_id__323378), F_dv_pair("actual_influence_control", v_control__343712), F_dv_pair("actual_literal_truth", F_dv_truth(v_physical__322682, F_dv_get(F_dv_get(v_action__323346, "speech"), "literal_ast"))) }));
            v_actors__322716 = F_dvs_speech_owned(v_actors__322716, v_index__323226, v_action__323346, v_events__322746, v_time__323318, v_parent__323286, v_physical__322682);
            v_actors__322716 = F_dvs_public_grants(v_actors__322716, v_index__323226, v_action__323346, v_physical__322682, v_bindings__322786, v_events__322746, v_time__323318, v_parent__323286, v_action_event__323460);
        }
        if (R.Bool((R.Bool(R.eq(F_dv_get(F_dv_get(v_result__324838, "execution"), "status"), "applied")) ? (object)(R.eq(F_dv_get(v_action__323346, "kind"), "move")) : (object)(false))))
        {
            v_actors__322716 = F_dvs_notice_movement_witnesses(v_actors__322716, v_index__323226, v_before__324812, v_physical__322682, v_bindings__322786, v_events__322746, v_time__323318, v_execution_event__327796);
        }
        v_actor__323256 = R.at(v_actors__322716, v_index__323226);
        v_actor__323256 = F_dvs_notice_peers(v_actor__323256, v_physical__322682, v_actors__322716, v_bindings__322786, v_events__322746, v_time__323318, v_execution_event__327796);
        v_actor__323256 = F_dvs_notice_visible_objects(v_actor__323256, v_physical__322682, v_bindings__322786, v_events__322746, v_time__323318, v_execution_event__327796);
        v_executed__323072 = R.add(v_executed__323072, R.number("1"));
        v_idle__323106 = R.number("0");
        v_actors__322716 = F_dvs_replace(v_actors__322716, v_index__323226, v_actor__323256);
        return new System.Collections.Generic.List<object>(19) { F_dv_pair("player_actor", F_dv_find(v_state, "player_actor", "")), F_dv_pair("story", v_story__322654), F_dv_pair("physical", v_physical__322682), F_dv_pair("actors", v_actors__322716), F_dv_pair("events", v_events__322746), F_dv_pair("bindings", v_bindings__322786), F_dv_pair("costs", v_costs__322820), F_dv_pair("run_start", v_run_start__322848), F_dv_pair("duration_budget", v_duration_budget__322884), F_dv_pair("horizon", v_horizon__322932), F_dv_pair("duration_blocked", v_duration_blocked__322964), F_dv_pair("limit", v_limit__323014), F_dv_pair("cursor", v_cursor__323042), F_dv_pair("executed", v_executed__323072), F_dv_pair("idle", v_idle__323106), F_dv_pair("status", v_status__323132), F_dv_pair("initial", v_initial__323162), F_dv_pair("awaiting_player", false), F_dv_pair("player_selection", "") };
    }

    public static object F_dvs_session_complete(object v_state, object v_receipt)
    {
        object v_pending__415640 = F_dv_get(v_state, "pending");
        object v_original_mind__415672 = F_dv_get(F_dv_get(v_pending__415640, "actor"), "mind");
        object v_previous_successes__415715 = R.count(F_dv_find(F_dv_get(v_pending__415640, "actor"), "completed_actions", new System.Collections.Generic.List<object>(0) { }));
        object v_result__415802 = F_dvs_session_complete_commit(v_state, v_receipt);
        object v_actors__415863 = F_dv_get(v_result__415802, "actors");
        object v_actor__415894 = R.at(v_actors__415863, F_dv_get(v_pending__415640, "index"));
        if (R.Bool(F_dv_find(F_dv_find(v_original_mind__415672, "CognitionFilter", new System.Collections.Generic.List<object>(0) { }), "anterograde", false)))
        {
            v_actor__415894 = F_dv_with(v_actor__415894, "mind", v_original_mind__415672);
            v_actors__415863 = F_dvs_replace(v_actors__415863, F_dv_get(v_pending__415640, "index"), v_actor__415894);
        }
        v_result__415802 = F_dvs_owned_state_update(v_result__415802, new System.Collections.Generic.List<object>(1) { F_dv_pair("actors", v_actors__415863) });
        if (R.Bool(R.lt(v_previous_successes__415715, R.count(F_dv_find(v_actor__415894, "completed_actions", new System.Collections.Generic.List<object>(0) { })))))
        {
            v_result__415802 = F_dvs_simulation_protect_complete(v_result__415802, v_pending__415640);
        }
        return F_dvs_simulation_sync(v_result__415802);
    }
}
