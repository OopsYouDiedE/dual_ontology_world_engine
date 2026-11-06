// @mechanism: causal_clock_and_campaign
// @layer: core/simulation
// @contracts: ExecutionEngine

using DuelView.Autonomy;
using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using DuelView.Runtime;
using DuelView.Spatial;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.Diagnostics;
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
using System.Text.RegularExpressions;
using System.Text;
using System;
using static DuelView.Compiled.R;
using static DuelView.Core.DVKernel;
using static DuelView.Core.DVSimulationKernel;

// --- Source: Execution.cs ---
namespace DuelView.Core
{


/// <summary>Session / Execution: native Core mechanisms.</summary>
public static partial class DVSimulationKernel
{
    private static object ReadCachedValue(string group, object owner, object key) =>
        R.Bool(R.memo_group_enabled(group)) ? R.memo_read(owner, key) : R.snapshot(new List<object> { false, false });

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
}

// --- Source: ExecutionOutcomes.cs ---
namespace DuelView.Core
{


public static partial class DVSimulationKernel
{
    private static string NativeSpatialCompletionFailure(object action, object receipt, object actorId, object actionEvent)
    {
        object Get(object record, string key) => R.Bool(R.record_like(record)) ? F_dv_find(record, key, false) : false;
        var reference = Get(action, "spatial_execution_ref");
        if (reference is false || Get(receipt, "executor") is not "godot" || Get(receipt, "outcome") is not "applied") return "";
        const string invalid = "native-spatial-completion-invalid";
        if (reference is not string { Length: > 0 }) return invalid;
        var definition = Get(R.content("data", reference), "value");
        var spec = Get(definition, "spatial_execution");
        var field = Get(receipt, "field");
        var motionSpec = Get(spec, "object_motion");
        var movingObject = motionSpec is not false;
        var postureSpec = Get(spec, "actor_posture");
        var changingPosture = postureSpec is not false;
        bool Same(string key, object expected) => R.Bool(F_dv_same(Get(field, key), expected));
        if (Get(definition, "kind") is not "spatial-action" || Get(definition, "status") is not "ready"
            || Get(spec, "status") is not "ready" || Get(spec, "version") is not "spatial-action-v1"
            || Get(field, "executor") is not "godot-native-spatial" || Get(field, "protocol") is not "native-spatial-completion-v1"
            || !Same("action_event", actionEvent) || !Same("actor", actorId) || !Same("action_id", Get(action, "id"))
            || !Same("spatial_execution_ref", reference) || !Same("target_id", Get(spec, "target_id"))
            || Get(field, "visual_finished") is not true || (!changingPosture && Get(field, "contact_seen") is not true)
            || Get(field, "final_on_floor") is not true
            || (!changingPosture && (movingObject ? Get(field, "pre_motion_contact") is not true : Get(field, "final_contact") is not true))) return invalid;
        if (changingPosture)
        {
            var pose = Get(field, "actor_posture");
            var from = Get(postureSpec, "from"); var to = Get(postureSpec, "to");
            if (movingObject || !((from is "standing" && to is "sitting") || (from is "sitting" && to is "standing")) ||
                Get(pose, "protocol") is not "native-seat-posture-v1" || Get(pose, "settled") is not true ||
                !R.Bool(F_dv_same(Get(pose, "from"), from)) || !R.Bool(F_dv_same(Get(pose, "to"), to)) ||
                !R.Bool(F_dv_same(Get(pose, "seat_id"), Get(spec, "target_id")))) return invalid;
            bool Within(string measured, string declared)
            {
                var actual = Get(pose, measured); var limit = Get(postureSpec, declared);
                return R.Bool(R.is_numeric_value(actual)) && R.Bool(R.is_numeric_value(limit)) &&
                    double.IsFinite(R.Num(actual)) && double.IsFinite(R.Num(limit)) &&
                    R.Num(actual) >= 0 && R.Num(limit) > 0 && R.Num(actual) <= R.Num(limit);
            }
            if (to is "sitting")
            {
                if (!Within("seat_error_m", "position_tolerance_m") || !Within("left_foot_error_m", "foot_tolerance_m") ||
                    !Within("right_foot_error_m", "foot_tolerance_m")) return invalid;
            }
            else if (!Within("exit_error_m", "position_tolerance_m")) return invalid;
        }
        if (movingObject)
        {
            var motion = Get(field, "object_motion");
            double Number(object record, string key) => R.Bool(R.is_numeric_value(Get(record, key))) ? R.Num(Get(record, key)) : double.NaN;
            var tolerance = Number(motionSpec, "tolerance_degrees");
            if (Get(spec, "contact_phase") is not "before-motion" || Get(motionSpec, "kind") is not "hinged-door"
                || Get(motionSpec, "angle_convention") is not "design-clockwise-degrees"
                || Get(motion, "protocol") is not "native-hinged-sweep-v1" || Get(motion, "settled") is not true
                || Get(motion, "blocked") is not false || !double.IsFinite(tolerance) || tolerance <= 0) return invalid;
            foreach (var (measured, declared) in new[] { ("initial_degrees", "from_degrees"), ("final_degrees", "target_degrees") })
            {
                var actual = Number(motion, measured); var expected = Number(motionSpec, declared);
                if (!double.IsFinite(actual) || !double.IsFinite(expected) || Math.Abs(actual - expected) > tolerance) return invalid;
            }
            var gap = Number(motion, "maximum_sweep_gap_m");
            if (!double.IsFinite(gap) || gap < 0 || gap > 0.020001) return invalid;
        }
        foreach (var (measured, declared) in new[] { ("stand_error_m", "arrival_tolerance_m"), (movingObject ? "pre_motion_contact_error_m" : "contact_error_m", "contact_tolerance_m"), ("facing_error_rad", "facing_tolerance_rad") })
        {
            if (changingPosture && (measured is "stand_error_m" or "contact_error_m")) continue;
            var value = Get(field, measured);
            var limit = Get(spec, declared);
            if (!R.Bool(R.is_numeric_value(value)) || !R.Bool(R.is_numeric_value(limit))) return invalid;
            var number = R.Num(value);
            var maximum = R.Num(limit);
            if (!double.IsFinite(number) || !double.IsFinite(maximum) || number < 0 || maximum <= 0 || number > maximum) return invalid;
        }
        foreach (var key in new[] { "visual_token", "visual_generation" })
        {
            var value = Get(field, key);
            if (!R.Bool(R.is_numeric_value(value))) return invalid;
            var number = R.Num(value);
            if (!double.IsFinite(number) || number < 0 || number != Math.Floor(number)) return invalid;
        }
        return "";
    }

    private static string NativeSpatialRejectionClass(object action, object receipt)
    {
        if (F_dv_find(action, "spatial_execution_ref", false) is not string
            || F_dv_find(receipt, "executor", false) is not "godot"
            || F_dv_find(receipt, "outcome", false) is not "rejected") return "";
        var field = F_dv_find(receipt, "field", false);
        if (!R.Bool(R.record_like(field)) || F_dv_find(field, "executor", false) is not "godot-native-spatial") return "";
        return R.Text(F_dv_find(field, "reason", "")) switch
        {
            "native-spatial-action-cancelled" or "native-spatial-visual-cancelled" or "native-spatial-view-replaced"
                or "native-spatial-host-closing" or "native-spatial-scene-exited" => "native-spatial-cancelled",
            "native-spatial-approach:blocked" => "native-spatial-path-blocked",
            "native-spatial-actor-left-stand" or "native-spatial-participant-left" or "native-spatial-participant-replaced"
                or "native-spatial-world-changed" or "native-spatial-final-alignment-lost"
                or "native-spatial-contact-not-reached" or "native-spatial-final-contact-lost" => "native-spatial-condition-invalidated",
            "spatial-action-unconfigured" or "spatial-action-not-ready" or "native-spatial-port-unavailable"
                or "spatial-binding-mismatch" or "native-spatial-receipt-mismatch" or "native-spatial-visual-start-failed"
                or "native-spatial-visual-token-mismatch" or "native-spatial-visual-timeout"
                or "native-spatial-invalid-transform" => "native-spatial-execution-fault",
            _ => ""
        };
    }

    // The content names the place and participants. Completion checks their
    // current physical locations, independently of the actor's earlier belief.
    private static string CompletionOpportunityFailure(object action, object actor, object actors, object physical)
    {
        var requirement = F_dv_find(action, "opportunity_requirement", false);
        if (requirement is false) return "";
        var location = F_dv_get(requirement, "location");
        bool Present(object person)
        {
            var reference = F_dv_find(person, "location_ref", false);
            if (reference is false) return false;
            var cell = F_dv_read(physical, reference);
            return R.Bool(F_dv_uncontested(cell)) && R.Bool(R.eq(F_dv_get(cell, "value"), location));
        }
        if (!Present(actor)) return "actor-left-required-location";
        var participants = R.Items(F_dv_get(requirement, "participants"));
        var present = 0;
        var unknown = false;
        foreach (var id in participants)
        {
            var person = R.Items(actors).FirstOrDefault(candidate => R.Bool(R.eq(F_dv_get(candidate, "id"), id)));
            if (person is null) { unknown = true; continue; }
            var reference = F_dv_find(person, "location_ref", false);
            if (reference is false || !R.Bool(F_dv_uncontested(F_dv_read(physical, reference)))) unknown = true;
            else if (Present(person)) present++;
        }
        return R.Text(F_dv_get(requirement, "mode")) switch
        {
            "all-present" => present == participants.Count ? "" : "required-participant-absent",
            "at-least-one-present" => present > 0 ? "" : "required-participant-absent",
            "none-present" => present == 0 && !unknown ? "" : "required-absence-not-established",
            _ => throw new InvalidOperationException("Unknown opportunity mode")
        };
    }

    private static string ExecutionOutcomeCategory(object execution)
    {
        if (R.Text(F_dv_get(execution, "status")) == "applied") return "completed";
        var failureClass = R.Text(F_dv_find(execution, "failure_class", ""));
        var reason = R.Text(F_dv_find(execution, "reason", ""));
        if (failureClass.Length == 0 && reason.Length > 0) failureClass = reason;

        return failureClass switch
        {
            "activity-interrupted" or "interrupted" => "interrupted",
            "native-continuous-source-cancel" or "native-spatial-cancelled" or "cancelled" or "action-cancelled"
                or "user-cancelled" or "task-cancelled" => "cancelled",
            "native-spatial-completion-invalid" or "native-spatial-execution-fault" or "execution-fault"
                or "argument-execution-state-changed" or "execution-state-mismatch" => "execution-fault",
            "native-motion-blocked" or "contact-out-of-reach" or "native-spatial-path-blocked" or "path-blocked" => "path-blocked",
            "condition-invalidated" or "actor-left-required-location" or "required-participant-absent"
                or "required-absence-not-established" or "request-context-invalidated"
                or "native-spatial-condition-invalidated" or "target-left-location" => "condition-invalidated",
            "character-refused" or "actor-refused" or "request-refused" or "declined" or "peer-refused"
                or "norm-refused" or "already-accepted-personal-answer" => "character-refused",
            _ => failureClass.Contains("refus", StringComparison.OrdinalIgnoreCase) || failureClass.Contains("declin", StringComparison.OrdinalIgnoreCase)
                ? "character-refused"
                : failureClass.Contains("cancel", StringComparison.OrdinalIgnoreCase)
                    ? "cancelled"
                    : failureClass.Contains("interrupt", StringComparison.OrdinalIgnoreCase)
                        ? "interrupted"
                        : failureClass.Contains("block", StringComparison.OrdinalIgnoreCase)
                            ? "path-blocked"
                            : failureClass.Contains("invalid", StringComparison.OrdinalIgnoreCase)
                                ? "condition-invalidated"
                                : "unclassified-failure"
        };
    }
}
}

// --- Source: Eligibility.cs ---
namespace DuelView.Core
{


/// <summary>Planning / Eligibility: native Core mechanisms.</summary>
public static partial class DVSimulationKernel
{
    public static object F_dvp_question(object v_actor)
    {
        if (R.Bool(F_dv_has_key(v_actor, "question_ref")))
        {
            object v_cell__88846 = F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_actor, "question_ref"));
            if (R.Bool(F_dv_uncontested(v_cell__88846)))
            {
                return F_dv_get(v_cell__88846, "value");
            }
            else
            {
                return "";
            }
        }
        else
        {
            return "";
        }
    }

    public static object F_dvp_topic(object v_actor, object v_item)
    {
        if (R.Bool((R.Bool(R.eq(F_dv_find(v_item, "scope", ""), "personal")) ? (object)(R.lt(R.number("0"), R.count(F_dv_find(v_item, "basis", new System.Collections.Generic.List<object>(0) { })))) : (object)(false))))
        {
            return true;
        }
        object v_topic__89170 = F_dv_find(v_item, "question", F_dv_find(v_item, "topic", ""));
        object v_question__89240 = F_dvp_question(v_actor);
        object v_bindings__89279 = F_dv_find(F_dv_get(v_actor, "policy"), "topic_bindings", new System.Collections.Generic.List<object>(0) { });
        object v_mind__89493 = F_dv_find(v_actor, "mind", false);
        if (R.Bool(R.not(R.is_list(v_mind__89493))))
        {
            return F_dvp_topic_scan(v_actor, v_topic__89170, v_question__89240, v_bindings__89279);
        }
        object v_key__89631 = new System.Collections.Generic.List<object>(6) { "dvp_topic", v_item, F_dv_has_key(v_actor, "question_ref"), F_dv_find(v_actor, "question_ref", false), v_bindings__89279, F_dv_find(v_actor, "heard_topics", new System.Collections.Generic.List<object>(0) { }) };
        object v_cached__89785 = F_dvs_plan_cache_get(v_mind__89493, v_key__89631);
        if (R.Bool(R.at(v_cached__89785, R.number("0"))))
        {
            return R.at(v_cached__89785, R.number("1"));
        }
        return F_dvs_plan_cache_put(v_mind__89493, v_key__89631, F_dvp_topic_scan(v_actor, v_topic__89170, v_question__89240, v_bindings__89279));
    }

    public static object F_dvp_topic_scan(object v_actor, object v_topic, object v_question, object v_bindings)
    {
        object v_i__90020 = R.number("0");
        object v_active__90034 = false;
        if (R.Bool(F_dv_and(R.not(R.eq(v_question, "")), R.eq(v_topic, v_question))))
        {
            v_active__90034 = true;
        }
        while (R.Bool(R.lt(v_i__90020, R.count(v_bindings))))
        {
            object v_binding__90179 = R.at(v_bindings, v_i__90020);
            if (R.Bool(F_dv_and(R.eq(F_dv_get(v_binding__90179, "topic"), v_topic), F_dv_and(R.lt(R.number("0"), R.count(F_dv_find(v_binding__90179, "basis", new System.Collections.Generic.List<object>(0) { }))), F_dv_and(F_dv_or(F_dvs_topic_was_heard(v_actor, v_topic), R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_binding__90179, "heard_when")), "true")), R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_binding__90179, "completed_when")), "true")))))))
            {
                v_active__90034 = true;
            }
            v_i__90020 = R.add(v_i__90020, R.number("1"));
        }
        return v_active__90034;
    }

    public static object F_dvp_add_ref(object v_refs, object v_ref)
    {
        if (R.Bool(F_dv_has(v_refs, v_ref)))
        {
            return v_refs;
        }
        else
        {
            return F_dv_append(v_refs, v_ref);
        }
    }

    public static object F_dvp_merge_refs(object v_refs, object v_extra)
    {
        object v_result__90694 = v_refs;
        object v_i__90716 = R.number("0");
        while (R.Bool(R.lt(v_i__90716, R.count(v_extra))))
        {
            v_result__90694 = F_dvp_add_ref(v_result__90694, R.at(v_extra, v_i__90716));
            v_i__90716 = R.add(v_i__90716, R.number("1"));
        }
        return v_result__90694;
    }

    public static object F_dvp_unmet_refs(object v_mind, object v_proposition, object v_include_known)
    {
        object v_op__90902 = F_dv_get(v_proposition, "op");
        if (R.Bool(R.eq(v_op__90902, "constant")))
        {
            return new System.Collections.Generic.List<object>(0) { };
        }
        else
        {
            if (R.Bool(R.eq(v_op__90902, "not")))
            {
                return F_dvp_unmet_refs(v_mind, F_dv_get(v_proposition, "item"), v_include_known);
            }
            else
            {
                if (R.Bool(R.eq(v_op__90902, "all")))
                {
                    object v_items__91140 = F_dv_get(v_proposition, "items");
                    object v_result__91186 = new System.Collections.Generic.List<object>(0) { };
                    object v_i__91218 = R.number("0");
                    while (R.Bool(R.lt(v_i__91218, R.count(v_items__91140))))
                    {
                        v_result__91186 = F_dvp_merge_refs(v_result__91186, F_dvp_unmet_refs(v_mind, R.at(v_items__91140, v_i__91218), v_include_known));
                        v_i__91218 = R.add(v_i__91218, R.number("1"));
                    }
                    return v_result__91186;
                }
                else
                {
                    if (R.Bool(F_dv_or(v_include_known, R.not(R.eq(F_dv_truth(v_mind, v_proposition), "true")))))
                    {
                        if (R.Bool(R.eq(v_op__90902, "held_for")))
                        {
                            return new System.Collections.Generic.List<object>(2) { F_dv_get(v_proposition, "ref"), F_dv_get(v_proposition, "clock") };
                        }
                        return F_dv_single(F_dv_get(v_proposition, "ref"));
                    }
                    else
                    {
                        return new System.Collections.Generic.List<object>(0) { };
                    }
                }
            }
        }
    }

    public static object F_dvp_goal_refs(object v_actor) => GoalRefs(v_actor, false);

    private static object ReadGoalRefs(object actor) => GoalRefs(R.snapshot(actor), true);

    private static object GoalRefs(object v_actor, bool readOnly)
    {
        var key = new List<object> { "owned-goal-refs-v1" };
        var cached = readOnly ? ReadCachedValue("plan", v_actor, key) : F_dvs_plan_cache_get(v_actor, key);
        if (R.Bool(R.at(cached, R.number("0")))) return R.at(cached, R.number("1"));
        object v_goals__91785 = F_dv_find(F_dv_get(v_actor, "policy"), "goals", new System.Collections.Generic.List<object>(0) { });
        object v_mind__91836 = F_dv_get(v_actor, "mind");
        object v_refs__91862 = new System.Collections.Generic.List<object>(0) { };
        object v_i__91880 = R.number("0");
        while (R.Bool(R.lt(v_i__91880, R.count(v_goals__91785))))
        {
            object v_goal__91931 = R.at(v_goals__91785, v_i__91880);
            if (R.Bool(F_dv_and(F_dvp_topic(v_actor, v_goal__91931), R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(v_goal__91931, "available_when", F_dv_true())), "true"))))
            {
                if (R.Bool(F_dv_has_key(v_goal__91931, "ref")))
                {
                    if (R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_goal__91931, "kind", "know"), "evidence"))) ? (object)(true) : (object)(F_dvp_evidence_due(v_actor, v_goal__91931)))))
                    {
                        v_refs__91862 = F_dvp_add_ref(v_refs__91862, F_dv_get(v_goal__91931, "ref"));
                    }
                }
                else
                {
                    if (R.Bool(F_dv_has_key(v_goal__91931, "proposition")))
                    {
                        v_refs__91862 = F_dvp_merge_refs(v_refs__91862, F_dvp_unmet_refs(v_mind__91836, F_dv_get(v_goal__91931, "proposition"), true));
                    }
                }
            }
            v_i__91880 = R.add(v_i__91880, R.number("1"));
        }
        var result = F_dvs_plan_cache_put(v_actor, key, v_refs__91862);
        return readOnly ? R.snapshot(result) : result;
    }

    public static object F_dvp_scope(object v_actor, object v_action, object v_bindings)
    {
        object v_key__92508 = new System.Collections.Generic.List<object>(3) { "dvp-owned-action-scope-v1", F_dv_get(v_actor, "id"), v_bindings };
        object v_cached__92572 = F_dvs_plan_cache_get(v_action, v_key__92508);
        if (R.Bool(R.at(v_cached__92572, R.number("0"))))
        {
            return R.at(v_cached__92572, R.number("1"));
        }
        object v_declared__92664 = F_dv_find(v_action, "purpose_refs", new System.Collections.Generic.List<object>(0) { });
        object v_grants__92719 = F_dv_find(v_action, "grants", new System.Collections.Generic.List<object>(0) { });
        object v_refs__92766 = v_declared__92664;
        object v_i__92790 = R.number("0");
        while (R.Bool(R.lt(v_i__92790, R.count(v_grants__92719))))
        {
            object v_ref__92842 = R.at(v_grants__92719, v_i__92790);
            if (R.Bool(F_dv_has_key(v_ref__92842, "object")))
            {
                object v_local__92912 = F_dv_local(v_bindings, F_dv_get(v_ref__92842, "object"), F_dv_get(v_actor, "id"));
                if (R.Bool(R.eq(v_local__92912, "")))
                {
                    _ = false;
                }
                else
                {
                    v_refs__92766 = F_dvp_add_ref(v_refs__92766, F_dv_ref(v_local__92912, F_dv_get(v_ref__92842, "facet"), F_dv_get(v_ref__92842, "field")));
                }
            }
            v_i__92790 = R.add(v_i__92790, R.number("1"));
        }
        return F_dvs_plan_cache_put(v_action, v_key__92508, v_refs__92766);
    }

    public static object F_dvp_answer_reward(object v_actor, object v_action, object v_mind)
    {
        if (R.Bool(R.eq(F_dv_find(v_action, "answer_form", "complete-answer"), "supporting-statement")))
        {
            return R.number("0");
        }
        if (R.Bool(F_dv_has_key(v_action, "answer")))
        {
            if (R.Bool(R.eq(F_dv_truth(v_mind, F_dv_get(v_action, "answer")), "true")))
            {
                return R.number("1.0");
            }
            else
            {
                if (R.Bool(F_dv_and(F_dv_has_key(v_actor, "argument"), R.eq(F_dv_find(F_dv_find(v_actor, "argument", new System.Collections.Generic.List<object>(0) { }), "question", ""), F_dv_find(v_action, "argument_question", "")))))
                {
                    return R.number("1.0");
                }
                else
                {
                    object v_hypothesis__93673 = F_dv_find(v_actor, "hypothesis", false);
                    if (R.Bool(R.eq(v_hypothesis__93673, false)))
                    {
                        return R.number("0");
                    }
                    else
                    {
                        if (R.Bool(F_dv_same(F_dv_get(v_hypothesis__93673, "proposition"), F_dv_get(v_action, "answer"))))
                        {
                            return F_dv_find(v_hypothesis__93673, "prior", R.number("0.5"));
                        }
                        else
                        {
                            return R.number("0");
                        }
                    }
                }
            }
        }
        else
        {
            return R.number("0");
        }
    }

    public static object F_dvp_input_signature(object v_actor, object v_action, object v_refs)
    {
        object v_mind__94091 = F_dv_get(v_actor, "mind");
        object v_inputs__94117 = new System.Collections.Generic.List<object>(0) { };
        object v_i__94137 = R.number("0");
        object v_local_1__94151 = false;
        if (R.Bool(F_dv_has_key(v_action, "answer")))
        {
            v_local_1__94151 = F_dvs_sim_refs(F_dv_get(v_action, "answer"));
        }
        else
        {
            v_local_1__94151 = new System.Collections.Generic.List<object>(0) { };
        }
        object v_selected__94289 = F_dvp_merge_refs(v_refs, v_local_1__94151);
        if (R.Bool(F_dv_has_key(v_action, "support")))
        {
            v_selected__94289 = F_dvp_merge_refs(v_selected__94289, F_dvs_sim_refs(F_dv_get(v_action, "support")));
        }
        while (R.Bool(R.lt(v_i__94137, R.count(v_selected__94289))))
        {
            _ = R.push(v_inputs__94117, new System.Collections.Generic.List<object>(2) { F_dv_pair("ref", R.at(v_selected__94289, v_i__94137)), F_dv_pair("cell", F_dvs_reason_cell(F_dv_read(v_mind__94091, R.at(v_selected__94289, v_i__94137)))) });
            v_i__94137 = R.add(v_i__94137, R.number("1"));
        }
        return F_dv_with(new System.Collections.Generic.List<object>(3) { F_dv_pair("question", F_dvp_question(v_actor)), F_dv_pair("claim", F_dv_find(v_action, "answer", false)), F_dv_pair("inputs", v_inputs__94117) }, "reward", F_dvp_answer_reward(v_actor, v_action, v_mind__94091));
    }

    public static object F_dvp_answer_relevant(object v_actor, object v_action)
    {
        if (R.Bool(F_dv_has_key(v_action, "topic")))
        {
            return F_dvp_topic(v_actor, v_action);
        }
        else
        {
            object v_refs__94900 = F_dvs_sim_refs(F_dv_get(v_action, "answer"));
            object v_goals__94947 = ReadGoalRefs(v_actor);
            object v_i__94988 = R.number("0");
            object v_found__95006 = false;
            while (R.Bool(R.lt(v_i__94988, R.count(v_refs__94900))))
            {
                if (R.Bool(F_dv_has(v_goals__94947, R.at(v_refs__94900, v_i__94988))))
                {
                    v_found__95006 = true;
                }
                v_i__94988 = R.add(v_i__94988, R.number("1"));
            }
            return v_found__95006;
        }
    }

    public static object F_dvp_answer_attempt_allowed(object v_prev_sig, object v_cur_sig)
    {
        if (R.Bool(R.eq(v_prev_sig, false)))
        {
            return true;
        }
        else
        {
            if (R.Bool(R.not(F_dv_same(F_dv_get(v_prev_sig, "inputs"), F_dv_get(v_cur_sig, "inputs")))))
            {
                return true;
            }
            else
            {
                if (R.Bool(R.not(F_dv_same(F_dv_get(v_prev_sig, "claim"), F_dv_get(v_cur_sig, "claim")))))
                {
                    return true;
                }
                else
                {
                    if (R.Bool(R.not(F_dv_same(F_dv_get(v_prev_sig, "question"), F_dv_get(v_cur_sig, "question")))))
                    {
                        return true;
                    }
                    else
                    {
                        object v_r1__95631 = F_dv_find(v_prev_sig, "reward", R.number("0"));
                        object v_r2__95691 = F_dv_find(v_cur_sig, "reward", R.number("0"));
                        object v_local_1__95750 = false;
                        if (R.Bool(R.lt(v_r1__95631, v_r2__95691)))
                        {
                            v_local_1__95750 = R.sub(v_r2__95691, v_r1__95631);
                        }
                        else
                        {
                            v_local_1__95750 = R.sub(v_r1__95631, v_r2__95691);
                        }
                        object v_diff__95938 = v_local_1__95750;
                        return R.not(R.lt(v_diff__95938, R.number("0.2")));
                    }
                }
            }
        }
    }

    public static object F_dvp_inspection_signature(object v_actor, object v_action, object v_mind)
    {
        object v_refs__96058 = F_dvp_merge_refs(F_dv_find(v_action, "grants", new System.Collections.Generic.List<object>(0) { }), F_dvs_sim_refs(F_dv_get(v_action, "precondition")));
        object v_inputs__96154 = new System.Collections.Generic.List<object>(0) { };
        object v_i__96174 = R.number("0");
        while (R.Bool(R.lt(v_i__96174, R.count(v_refs__96058))))
        {
            object v_cell__96222 = F_dv_read(v_mind, R.at(v_refs__96058, v_i__96174));
            _ = R.push(v_inputs__96154, new System.Collections.Generic.List<object>(4) { F_dv_pair("ref", R.at(v_refs__96058, v_i__96174)), F_dv_pair("known", F_dv_get(v_cell__96222, "known")), F_dv_pair("value", F_dv_get(v_cell__96222, "value")), F_dv_pair("conflicted", F_dv_get(v_cell__96222, "conflicted")) });
            v_i__96174 = R.add(v_i__96174, R.number("1"));
        }
        object v_evidence__96393 = F_dvp_inspection_freshness(v_actor, v_mind);
        object v_freshness__96450 = new System.Collections.Generic.List<object>(0) { };
        v_i__96174 = R.number("0");
        while (R.Bool(R.lt(v_i__96174, R.count(v_refs__96058))))
        {
            object v_entries__96517 = F_dv_find(v_evidence__96393, F_dv_ref_lookup_key(R.at(v_refs__96058, v_i__96174)), new System.Collections.Generic.List<object>(0) { });
            object v_j__96589 = R.number("0");
            while (R.Bool(R.lt(v_j__96589, R.count(v_entries__96517))))
            {
                _ = R.push(v_freshness__96450, R.at(v_entries__96517, v_j__96589));
                v_j__96589 = R.add(v_j__96589, R.number("1"));
            }
            v_i__96174 = R.add(v_i__96174, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("inputs", v_inputs__96154), F_dv_pair("freshness", v_freshness__96450) };
    }

    public static object F_dvp_rejected_condition_signature(object v_mind, object v_proposition)
    {
        if (R.Bool(R.eq(F_dv_get(v_proposition, "op"), "all")))
        {
            object v_items__97029 = new System.Collections.Generic.List<object>(0) { };
            object v_i__97052 = R.number("0");
            while (R.Bool(R.lt(v_i__97052, R.count(F_dv_get(v_proposition, "items")))))
            {
                _ = R.push(v_items__97029, F_dvp_rejected_condition_signature(v_mind, R.at(F_dv_get(v_proposition, "items"), v_i__97052)));
                v_i__97052 = R.add(v_i__97052, R.number("1"));
            }
            return v_items__97029;
        }
        if (R.Bool(R.eq(F_dv_get(v_proposition, "op"), "not")))
        {
            return F_dvp_rejected_condition_signature(v_mind, F_dv_get(v_proposition, "item"));
        }
        if (R.Bool(R.eq(F_dv_get(v_proposition, "op"), "equals")))
        {
            object v_cell__97388 = F_dv_read(v_mind, F_dv_get(v_proposition, "ref"));
            return new System.Collections.Generic.List<object>(4) { F_dv_pair("ref", F_dv_get(v_proposition, "ref")), F_dv_pair("known", F_dv_get(v_cell__97388, "known")), F_dv_pair("value", F_dv_get(v_cell__97388, "value")), F_dv_pair("conflicted", F_dv_get(v_cell__97388, "conflicted")) };
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("condition", v_proposition), F_dv_pair("truth", F_dv_truth(v_mind, v_proposition)) };
    }

    public static object F_dvp_rejected_signature(object v_actor, object v_action, object v_mind)
    {
        object v_adoption__97672 = F_dv_find(F_dv_get(v_mind, "adoptions"), F_dv_find(v_action, "predict_law", ""), false);
        object v_prediction__97758 = false;
        if (R.Bool(R.not(R.eq(v_adoption__97672, false))))
        {
            v_prediction__97758 = new System.Collections.Generic.List<object>(4) { F_dv_pair("active", F_dv_get(v_adoption__97672, "active")), F_dv_pair("mode", F_dv_get(v_adoption__97672, "mode")), F_dv_pair("law", F_dv_get(v_adoption__97672, "law")), F_dv_pair("inputs", F_dvp_rejected_condition_signature(v_mind, F_dv_get(F_dv_get(v_adoption__97672, "law"), "premise"))) };
        }
        object v_location__97976 = false;
        if (R.Bool(F_dv_has_key(v_actor, "location_ref")))
        {
            object v_cell__98047 = F_dv_read(v_mind, F_dv_get(v_actor, "location_ref"));
            v_location__97976 = new System.Collections.Generic.List<object>(3) { F_dv_pair("known", F_dv_get(v_cell__98047, "known")), F_dv_pair("value", F_dv_get(v_cell__98047, "value")), F_dv_pair("conflicted", F_dv_get(v_cell__98047, "conflicted")) };
        }
        object v_opportunity__98189 = false;
        if (R.Bool(F_dv_has_key(v_action, "opportunity_requirement")))
        {
            v_opportunity__98189 = new System.Collections.Generic.List<object>(3) { F_dv_pair("context", F_dv_find(v_actor, "visible_context", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("observed_location", F_dv_find(v_actor, "visible_context_location", false)), F_dv_pair("visibility_available", F_dv_find(v_actor, "visible_context_clear", false)) };
        }
        object v_request_context__98488 = false;
        object v_request__98520 = F_dv_find(F_dv_find(v_action, "speech", new System.Collections.Generic.List<object>(0) { }), "request", false);
        if (R.Bool(R.not(R.eq(v_request__98520, false))))
        {
            v_request_context__98488 = F_dvs_request_context(F_dvp_actor_view(v_actor, v_mind), v_action, F_dv_get(v_request__98520, "target"));
        }
        return new System.Collections.Generic.List<object>(5) { F_dv_pair("precondition", F_dvp_rejected_condition_signature(v_mind, F_dv_get(v_action, "precondition"))), F_dv_pair("prediction", v_prediction__97758), F_dv_pair("location", v_location__97976), F_dv_pair("opportunity", v_opportunity__98189), F_dv_pair("request_context", v_request_context__98488) };
    }

    public static object F_dvs_manual_continuous_cancel(object v_action, object v_receipt, object v_subject)
    {
        object v_field__99370 = F_dv_find(v_receipt, "field", new System.Collections.Generic.List<object>(0) { });
        object v_proof__99416 = F_dv_find(v_field__99370, "continuous_cancel", false);
        if (R.Bool((R.Bool(R.eq(v_proof__99416, false)) ? (object)(true) : (object)(R.not(F_dv_record_like(v_proof__99416))))))
        {
            return false;
        }
        if (R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_proof__99416, "version", ""), "source-cancel-v2"))) ? (object)(true) : (object)(R.not(F_dv_find(v_proof__99416, "identity_verified", false))))))
        {
            return false;
        }
        if (R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_action, "kind"), "move"))) ? (object)(true) : (object)(F_dv_has_key(F_dv_find(v_action, "travel", new System.Collections.Generic.List<object>(0) { }), "station")))))
        {
            return false;
        }
        object v_travel__99786 = F_dv_find(v_action, "travel", new System.Collections.Generic.List<object>(0) { });
        if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_find(v_travel__99786, "from", ""), "")) ? (object)(true) : (object)(R.eq(F_dv_find(v_travel__99786, "to", ""), "")))) ? (object)(true) : (object)(R.eq(F_dv_get(v_travel__99786, "from"), F_dv_get(v_travel__99786, "to"))))))
        {
            return false;
        }
        if (R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_field__99370, "kind", ""), "daily"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__99370, "status", ""), "blocked"))))) ? (object)(true) : (object)(R.not(F_dv_find(v_field__99370, "continuous_walk", false))))))
        {
            return false;
        }
        if (R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_field__99370, "reason", ""), "released"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_proof__99416, "reason", ""), "released"))))))
        {
            return false;
        }
        if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_field__99370, "who", ""), v_subject))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__99370, "action", ""), F_dv_get(v_action, "id")))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_proof__99416, "actor", ""), v_subject))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_proof__99416, "action", ""), F_dv_get(v_action, "id")))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_proof__99416, "action_event", ""), F_dv_get(v_receipt, "action_event")))))))
        {
            return false;
        }
        object v_request__100497 = F_dv_find(v_field__99370, "request_id", "");
        object v_serial__100548 = F_dv_find(v_field__99370, "serial", false);
        if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.eq(v_request__100497, "")) ? (object)(true) : (object)(R.not(R.eq(v_request__100497, F_dv_find(v_proof__99416, "request_id", "")))))) ? (object)(true) : (object)(R.not(R.is_numeric_value(v_serial__100548))))) ? (object)(true) : (object)(R.not(R.lt(R.number("0"), v_serial__100548))))) ? (object)(true) : (object)(R.not(F_dv_same(v_serial__100548, F_dv_find(v_proof__99416, "serial", false)))))))
        {
            return false;
        }
        object v_from_room__100793 = F_dv_find(v_proof__99416, "from_room", "");
        object v_to_room__100845 = F_dv_find(v_proof__99416, "to_room", "");
        object v_map__100893 = F_dv_find(v_proof__99416, "map", "");
        if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.eq(v_from_room__100793, "")) ? (object)(true) : (object)(R.eq(v_to_room__100845, "")))) ? (object)(true) : (object)(R.eq(v_from_room__100793, v_to_room__100845)))) ? (object)(true) : (object)(R.eq(v_map__100893, "")))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__99370, "source_room", ""), v_from_room__100793))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__99370, "room", ""), v_from_room__100793))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__99370, "source_map", ""), v_map__100893))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__99370, "map", ""), v_map__100893))))))
        {
            return false;
        }
        if (R.Bool((R.Bool(R.not(F_dv_same(F_dv_find(v_field__99370, "transitions", false), new System.Collections.Generic.List<object>(0) { }))) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_find(v_proof__99416, "transitions", false), new System.Collections.Generic.List<object>(0) { }))))))
        {
            return false;
        }
        object v_fields__101344 = new System.Collections.Generic.List<object>(2) { "source_position", "actual_position" };
        object v_i__101400 = R.number("0");
        while (R.Bool(R.lt(v_i__101400, R.count(v_fields__101344))))
        {
            object v_position__101450 = F_dv_find(v_field__99370, R.at(v_fields__101344, v_i__101400), false);
            if (R.Bool((R.Bool((R.Bool(R.not(R.is_list(v_position__101450))) ? (object)(true) : (object)(R.not(R.eq(R.count(v_position__101450), R.number("2")))))) ? (object)(true) : (object)(R.not(F_dv_same(v_position__101450, F_dv_find(v_proof__99416, R.at(v_fields__101344, v_i__101400), false)))))))
            {
                return false;
            }
            if (R.Bool((R.Bool(R.not(R.is_numeric_value(R.at(v_position__101450, R.number("0"))))) ? (object)(true) : (object)(R.not(R.is_numeric_value(R.at(v_position__101450, R.number("1"))))))))
            {
                return false;
            }
            v_i__101400 = R.add(v_i__101400, R.number("1"));
        }
        // Stopping within the departure room is a cancellation even after local movement.
        // The host binds both positions to the retained body and verifies that room.
        if (!R.Bool(F_dv_find(v_proof__99416, "source_room_verified", false)))
        {
            return false;
        }
        object v_source_geometry__101869 = F_dv_find(v_field__99370, "source_geometry", "");
        if (R.Bool((R.Bool(R.not(F_dv_has(new System.Collections.Generic.List<object>(2) { "interior", "source-side-doorway" }, v_source_geometry__101869))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_proof__99416, "source_geometry", ""), v_source_geometry__101869))))))
        {
            return false;
        }
        return true;
    }

    public static object F_dvs_manual_contact_failure(object v_action, object v_receipt, object v_subject)
    {
        if (R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_receipt, "executor", ""), "godot"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_receipt, "outcome", ""), "rejected"))))))
        {
            return "";
        }
        if (R.Bool(F_dvs_manual_continuous_cancel(v_action, v_receipt, v_subject)))
        {
            return "native-continuous-source-cancel";
        }
        object v_field__102392 = F_dv_find(v_receipt, "field", new System.Collections.Generic.List<object>(0) { });
        if (R.Bool((R.Bool(R.eq(F_dv_find(v_field__102392, "executor", ""), "dv-world")) ? (object)(R.eq(F_dv_find(v_field__102392, "contact", true), false)) : (object)(false))))
        {
            object v_site__102540 = F_dv_find(v_field__102392, "site", "");
            if (R.Bool((R.Bool(R.not(R.eq(v_site__102540, ""))) ? (object)(R.not(R.eq(v_site__102540, "self"))) : (object)(false))))
            {
                return "contact-out-of-reach";
            }
        }
        if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_field__102392, "kind", ""), "daily"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__102392, "status", ""), "blocked"))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__102392, "who", ""), v_subject))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__102392, "action", ""), F_dv_get(v_action, "id")))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_field__102392, "reason", ""), ""))))))
        {
            return "";
        }
        object v_proof__102898 = new System.Collections.Generic.List<object>(7) { "station", "approach_point", "station_point", "station_radius", "arrival_tolerance", "actual_position", "arrival_error" };
        object v_i__103036 = R.number("0");
        while (R.Bool(R.lt(v_i__103036, R.count(v_proof__102898))))
        {
            if (R.Bool(R.not(F_dv_has_key(v_field__102392, R.at(v_proof__102898, v_i__103036)))))
            {
                return "";
            }
            v_i__103036 = R.add(v_i__103036, R.number("1"));
        }
        return "native-motion-blocked";
    }

    public static object F_dvp_eligible_base(object v_actor, object v_action, object v_mind, object v_relevant)
    {
        if (R.Bool((R.Bool(F_dv_find(v_actor, "manual_input", false)) ? (object)(F_dv_find(v_action, "known_opportunity_wait", false)) : (object)(false))))
        {
            return false;
        }
        object v_unknown_refs__103519 = F_dv_find(v_action, "unknown_refs", new System.Collections.Generic.List<object>(0) { });
        object v_unknown_index__103578 = R.number("0");
        while (R.Bool(R.lt(v_unknown_index__103578, R.count(v_unknown_refs__103519))))
        {
            if (R.Bool(F_dv_uncontested(F_dv_read(v_mind, R.at(v_unknown_refs__103519, v_unknown_index__103578)))))
            {
                return false;
            }
            v_unknown_index__103578 = R.add(v_unknown_index__103578, R.number("1"));
        }
        object v_entity_lock__103792 = F_dv_find(v_actor, "simulation_entity_lock", false);
        if (R.Bool((R.Bool(F_dv_find(v_action, "requires_entity_reservation", false)) ? (object)(R.eq(v_entity_lock__103792, false)) : (object)(false))))
        {
            return false;
        }
        if (R.Bool((R.Bool((R.Bool(R.not(R.eq(v_entity_lock__103792, false))) ? (object)(F_dv_find(v_entity_lock__103792, "protecting", false)) : (object)(false))) ? (object)(R.not(F_dv_find(v_action, "entity_hold_compatible", false))) : (object)(false))))
        {
            return false;
        }
        if (R.Bool(F_dvs_action_requires_argument(v_action)))
        {
            if (R.Bool(R.not(F_dvs_argument_action_current(F_dvp_actor_view(v_actor, v_mind), v_action))))
            {
                return false;
            }
        }
        if (R.Bool((R.Bool(F_dv_find(v_action, "manual_only", false)) ? (object)(R.not(F_dv_find(v_actor, "manual_input", false))) : (object)(false))))
        {
            return false;
        }
        object v_id__104387 = F_dv_get(v_action, "id");
        object v_used__104479 = F_dv_find(v_actor, "completed_actions", new System.Collections.Generic.List<object>(0) { });
        if (R.Bool((R.Bool((R.Bool(R.not(F_dv_has_key(v_action, "answer"))) ? (object)(R.not(F_dv_has_key(v_action, "speech"))) : (object)(false))) ? (object)(R.not(R.eq(F_dv_get(v_action, "kind"), "replay"))) : (object)(false))))
        {
            if (R.Bool((R.Bool(F_dv_has(v_used__104479, v_id__104387)) ? (object)((R.Bool(F_dv_find(v_action, "once", false)) ? (object)(true) : (object)(R.not(F_dv_find(v_action, "repeatable", false))))) : (object)(false))))
            {
                return false;
            }
        }
        object v_retry__105006 = F_dv_find(F_dv_find(v_actor, "manual_contact_retries", new System.Collections.Generic.List<object>(0) { }), v_id__104387, false);
        object v_explicit_contact_retry__105087 = (R.Bool((R.Bool(F_dv_find(v_actor, "manual_input", false)) ? (object)(R.not(R.eq(v_retry__105006, false))) : (object)(false))) ? (object)((R.Bool(R.not(R.eq(F_dv_find(v_retry__105006, "failure_class", ""), "native-continuous-source-cancel"))) ? (object)(true) : (object)(F_dv_same(F_dv_find(v_retry__105006, "source_signature", false), F_dvp_rejected_signature(v_actor, v_action, v_mind))))) : (object)(false));
        object v_rejected__105355 = F_dv_find(F_dv_find(v_actor, "rejected_attempt_signatures", new System.Collections.Generic.List<object>(0) { }), v_id__104387, false);
        if (R.Bool(R.not(R.eq(v_rejected__105355, false))))
        {
            if (R.Bool((R.Bool(F_dv_same(v_rejected__105355, F_dvp_rejected_signature(v_actor, v_action, v_mind))) ? (object)(R.not(v_explicit_contact_retry__105087)) : (object)(false))))
            {
                return false;
            }
        }
        if (R.Bool(F_dv_and(R.not(F_dv_find(v_actor, "cognition_blocked", false)), F_dv_and(F_dv_find(v_action, "argument_available", true), R.eq(F_dvs_dirty_truth(v_mind, F_dv_get(v_action, "precondition"), F_dv_get(v_actor, "policy"), F_dv_get(v_action, "id")), "true")))))
        {
            if (R.Bool(R.eq(F_dv_get(v_action, "kind"), "inspect")))
            {
                object v_inspection_actor__105845 = F_dvp_actor_view(v_actor, v_mind);
                if (R.Bool(F_dv_same(F_dv_find(F_dv_find(v_actor, "inspection_attempt_signatures", new System.Collections.Generic.List<object>(0) { }), v_id__104387, false), F_dvp_inspection_signature(v_inspection_actor__105845, v_action, v_mind))))
                {
                    if (R.Bool(R.not((R.Bool(F_dv_find(v_actor, "manual_input", false)) ? (object)(F_dv_find(v_action, "manual_reinspect", false)) : (object)(false)))))
                    {
                        return false;
                    }
                }
            }
            if (R.Bool(F_dv_has_key(v_action, "answer")))
            {
                object v_local_1__106248 = false;
                if (R.Bool(F_dv_has_key(v_action, "argument")))
                {
                    v_local_1__106248 = R.number("2");
                }
                else
                {
                    v_local_1__106248 = R.number("1");
                }
                if (R.Bool(F_dv_and(F_dvp_answer_relevant(v_actor, v_action), R.lt(F_dv_find(F_dv_find(v_actor, "accepted_answers", new System.Collections.Generic.List<object>(0) { }), F_dv_find(v_action, "topic", ""), R.number("0")), v_local_1__106248))))
                {
                    object v_speech_ready__106558 = true;
                    if (R.Bool(F_dv_has_key(v_action, "speech")))
                    {
                        v_speech_ready__106558 = R.eq(F_dvs_speech_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_action, "speech")), "true");
                    }
                    return F_dv_and(v_speech_ready__106558, F_dv_and(R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_action, "answer")), "false")), F_dvp_answer_attempt_allowed(F_dv_find(F_dv_find(v_actor, "answer_attempt_signatures", new System.Collections.Generic.List<object>(0) { }), v_id__104387, false), F_dvp_input_signature(v_actor, v_action, v_relevant))));
                }
                else
                {
                    return false;
                }
            }
            else
            {
                if (R.Bool(F_dv_has_key(v_action, "speech")))
                {
                    object v_local_2__107095 = false;
                    if (R.Bool(F_dv_has_key(v_action, "response_topic")))
                    {
                        object v_request__107192 = F_dvs_pending_request(v_actor, v_action);
                        if (R.Bool(R.lt(R.number("0"), R.count(v_request__107192))))
                        {
                            v_local_2__107095 = R.not(R.eq(F_dvs_request_context(v_actor, v_action, F_dv_get(v_request__107192, "requester")), false));
                        }
                        else
                        {
                            v_local_2__107095 = (R.Bool(R.not(F_dv_find(v_action, "requires_actual_request", false))) ? (object)(F_dv_and(R.not(F_dv_has(v_used__104479, v_id__104387)), F_dv_or(F_dv_has_key(F_dv_find(v_actor, "visible_context", new System.Collections.Generic.List<object>(0) { }), F_dv_find(v_action, "offer_to", "")), (R.Bool(R.not(R.eq(F_dv_find(v_action, "opportunity_requirement", false), false))) ? (object)(F_dvp_communication_progress(v_actor, v_action)) : (object)(false))))) : (object)(false));
                        }
                    }
                    else
                    {
                        if (R.Bool(F_dv_has_key(F_dv_get(v_action, "speech"), "request")))
                        {
                            object v_context__107841 = F_dvs_request_context(v_actor, v_action, F_dv_get(F_dv_get(F_dv_get(v_action, "speech"), "request"), "target"));
                            v_local_2__107095 = F_dv_and(R.not(R.eq(v_context__107841, false)), R.not(F_dv_same(v_context__107841, F_dv_find(F_dv_find(v_actor, "request_contexts", new System.Collections.Generic.List<object>(0) { }), v_id__104387, false))));
                        }
                        else
                        {
                            v_local_2__107095 = (R.Bool(R.not(F_dv_has(v_used__104479, v_id__104387))) ? (object)(true) : (object)((R.Bool((R.Bool(F_dv_find(v_action, "repeatable", false)) ? (object)(R.not(F_dv_find(v_action, "once", false))) : (object)(false))) ? (object)(R.not(F_dv_find(v_action, "response_once", false))) : (object)(false))));
                        }
                    }
                    return F_dv_and(R.eq(F_dvs_speech_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_action, "speech")), "true"), v_local_2__107095);
                }
                else
                {
                    if (R.Bool(R.eq(F_dv_get(v_action, "kind"), "replay")))
                    {
                        return (R.Bool((R.Bool(R.lt(R.number("0"), F_dv_find(F_dv_get(v_actor, "policy"), "model_budget", R.number("0")))) ? (object)(F_dvs_replay_has_available_candidate(F_dvp_actor_view(v_actor, v_mind))) : (object)(false))) ? (object)(R.not(F_dv_same(F_dv_find(F_dv_find(v_actor, "replay_attempt_signatures", new System.Collections.Generic.List<object>(0) { }), F_dvs_replay_attempt_key(v_action), false), F_dvs_replay_input_signature(v_actor, v_action, v_relevant)))) : (object)(false));
                    }
                    else
                    {
                        if (R.Bool(F_dv_find(v_action, "once", false)))
                        {
                            return R.not(F_dv_has(v_used__104479, v_id__104387));
                        }
                        else
                        {
                            if (R.Bool(F_dv_find(v_action, "repeatable", false)))
                            {
                                return true;
                            }
                            else
                            {
                                return R.not(F_dv_has(v_used__104479, v_id__104387));
                            }
                        }
                    }
                }
            }
        }
        else
        {
            return false;
        }
    }

    public static object F_dvp_source_supported_since(object v_cell, object v_source, object v_minimum_time)
    {
        object v_claims__109194 = F_dv_find(v_cell, "claims", new System.Collections.Generic.List<object>(0) { });
        object v_i__109239 = R.number("0");
        object v_found__109253 = false;
        while (R.Bool(R.lt(v_i__109239, R.count(v_claims__109194))))
        {
            object v_claim__109313 = R.at(v_claims__109194, v_i__109239);
            if (R.Bool((R.Bool(F_dv_and(R.eq(F_dv_get(v_claim__109313, "source"), v_source), F_dv_same(F_dv_get(v_claim__109313, "value"), F_dv_get(v_cell, "value")))) ? (object)(R.not(R.lt(F_dv_find(v_claim__109313, "time", R.number("0")), v_minimum_time))) : (object)(false))))
            {
                v_found__109253 = true;
            }
            v_i__109239 = R.add(v_i__109239, R.number("1"));
        }
        return v_found__109253;
    }

    public static object F_dvp_evidence_due(object v_actor, object v_goal)
    {
        object v_origin__109786 = F_dv_find(v_actor, "planning_evidence_mind", F_dv_get(v_actor, "mind"));
        return R.not(R.lt(F_dv_find(v_origin__109786, "time", R.number("0")), F_dv_find(v_goal, "minimum_time", R.number("0"))));
    }

    public static object F_dvp_needs_evidence(object v_actor, object v_ref)
    {
        return PendingEvidence(v_actor, v_ref, null);
    }

    // Use the same revision-scoped freshness index as inspection admission.
    // A known value does not fulfill a request for a newer, sourced observation.
    private static bool PendingEvidence(object actor, object reference, object? observationSource)
    {
        var mind = F_dv_get(actor, "mind");
        var index = F_dvp_inspection_freshness(actor, mind);
        var entries = F_dv_find(index, F_dv_ref_lookup_key(reference), new List<object>());
        foreach (var entry in R.Items(entries))
        {
            if (!R.Bool(F_dv_same(F_dv_get(entry, "ref"), reference)) ||
                !R.Bool(F_dv_get(entry, "topic")) ||
                !R.Bool(R.eq(F_dv_get(entry, "available"), "true")) ||
                !R.Bool(F_dv_get(entry, "due"))) continue;
            var source = F_dv_get(entry, "source");
            if (observationSource is not null && !R.Bool(R.eq(source, observationSource))) continue;
            if (!R.Bool(F_dvp_source_supported_since(F_dv_read(mind, reference), source,
                F_dv_get(entry, "minimum_time")))) return true;
        }
        return false;
    }

    public static object F_dvp_evidence_action_progress(object actor, object action, object reference, object bindings)
    {
        if (!PendingEvidence(actor, reference, null)) return false;
        var subject = F_dv_get(actor, "id");
        foreach (var grant in R.Items(F_dv_find(action, "grants", new List<object>())))
        {
            var local = F_dv_local(bindings, F_dv_get(grant, "object"), subject);
            if (R.Bool(R.eq(local, ""))) continue;
            var mapped = F_dv_ref(local, F_dv_get(grant, "facet"), F_dv_get(grant, "field"));
            if (!R.Bool(F_dv_same_local_ref(subject, mapped, reference))) continue;
            var source = F_dv_find(action, "observation_source", "perception");
            foreach (var declared in R.Items(F_dv_find(action, "grant_observation_sources", new List<object>())))
                if (R.Bool(F_dv_same_local_ref(subject, F_dv_get(declared, "ref"), grant)))
                    source = F_dv_get(declared, "source");
            if (PendingEvidence(actor, reference, source)) return true;
        }
        return false;
    }

    public static object F_dvp_owned_task_ready(object v_actor, object v_goals)
    {
        if (R.Bool(R.not(F_dv_find(F_dv_get(v_actor, "policy"), "coordinate_owned_tasks", false))))
        {
            return false;
        }
        object v_i__110889 = R.number("0");
        while (R.Bool(R.lt(v_i__110889, R.count(v_goals))))
        {
            object v_goal__110938 = R.at(v_goals, v_i__110889);
            if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_find(v_goal__110938, "scope", ""), "personal")) ? (object)(R.lt(R.number("0"), R.count(F_dv_find(v_goal__110938, "basis", new System.Collections.Generic.List<object>(0) { })))) : (object)(false))) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(v_goal__110938, "available_when", F_dv_true())), "true")) : (object)(false))))
            {
                object v_kind__111147 = F_dv_find(v_goal__110938, "kind", "");
                object v_pending__111196 = false;
                if (R.Bool((R.Bool(R.eq(v_kind__111147, "achieve")) ? (object)(F_dv_find(v_goal__110938, "navigation_commitment", false)) : (object)(false))))
                {
                    v_pending__111196 = R.Bool(F_dvs_navigation_evidence_pending(v_actor, v_goal__110938)) &&
                        R.Bool(R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_goal__110938, "proposition")), "true")));
                }
                if (R.Bool((R.Bool((R.Bool(R.eq(v_kind__111147, "evidence")) ? (object)(F_dv_has_key(v_goal__110938, "ref")) : (object)(false))) ? (object)(F_dvp_evidence_due(v_actor, v_goal__110938)) : (object)(false))))
                {
                    v_pending__111196 = R.not(F_dvp_source_supported_since(F_dv_read(F_dv_get(v_actor, "mind"), F_dv_get(v_goal__110938, "ref")), F_dv_find(v_goal__110938, "source", "perception"), F_dv_find(v_goal__110938, "minimum_time", R.number("0"))));
                }
                if (R.Bool(v_pending__111196))
                {
                    object v_a__111673 = R.number("0");
                    while (R.Bool(R.lt(v_a__111673, R.count(F_dv_get(v_actor, "actions")))))
                    {
                        object v_action__111754 = R.at(F_dv_get(v_actor, "actions"), v_a__111673);
                        object v_practical__111804 = (R.Bool((R.Bool(R.eq(v_kind__111147, "evidence")) ? (object)(R.eq(F_dv_find(v_action__111754, "inquiry_purpose", ""), "practical-access")) : (object)(false))) ? (object)(F_dv_has(F_dv_find(v_action__111754, "grants", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_goal__110938, "ref"))) : (object)(false));
                        if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(v_kind__111147, "achieve")) ? (object)(true) : (object)(v_practical__111804))) ? (object)(F_dvp_eligible(v_actor, v_action__111754, F_dv_get(v_actor, "mind"), new System.Collections.Generic.List<object>(0) { })) : (object)(false))) ? (object)(R.not(R.lt(F_dvp_personal_motivation(v_actor, v_action__111754), R.number("0")))) : (object)(false))))
                        {
                            if (R.Bool(v_practical__111804))
                            {
                                return true;
                            }
                            if (R.Bool(R.eq(v_kind__111147, "achieve")))
                            {
                                object v_work__112261 = F_dvp_achievement_pending_work(v_actor, v_action__111754, new System.Collections.Generic.List<object>(1) { v_goal__110938 });
                                if (R.Bool(R.lt(R.number("0"), F_dv_get(v_work__112261, "value"))))
                                {
                                    return true;
                                }
                            }
                        }
                        v_a__111673 = R.add(v_a__111673, R.number("1"));
                    }
                }
            }
            v_i__110889 = R.add(v_i__110889, R.number("1"));
        }
        return false;
    }

    public static object F_dvp_personal_motivation(object v_actor, object v_action)
    {
        if (R.Bool(R.not(F_dv_has_key(v_action, "personal_choice"))))
        {
            return R.number("0");
        }
        if (R.Bool(R.not(F_dv_find(F_dv_get(v_actor, "policy"), "value_sensitive_progress", false))))
        {
            return R.number("1");
        }
        object v_order__112681 = F_dv_find(F_dv_get(v_actor, "policy"), "value_order", new System.Collections.Generic.List<object>(0) { });
        object v_i__112738 = R.number("0");
        while (R.Bool(R.lt(v_i__112738, R.count(v_order__112681))))
        {
            object v_impact__112787 = F_dvp_personal_impact(F_dv_get(v_action, "personal_choice"), R.at(v_order__112681, v_i__112738));
            if (R.Bool(R.not(R.eq(v_impact__112787, R.number("0")))))
            {
                return v_impact__112787;
            }
            v_i__112738 = R.add(v_i__112738, R.number("1"));
        }
        return R.number("0");
    }

    public static object F_dvp_extract_required_room(object v_prop, object v_loc_ref)
    {
        if (R.Bool(R.not(F_dv_record_like(v_prop))))
        {
            return "";
        }
        object v_op__113195 = F_dv_find(v_prop, "op", "");
        if (R.Bool(R.eq(v_op__113195, "equals")))
        {
            object v_ref__113259 = F_dv_find(v_prop, "ref", false);
            if (R.Bool((R.Bool(R.not(R.eq(v_ref__113259, false))) ? (object)(F_dv_same(v_ref__113259, v_loc_ref)) : (object)(false))))
            {
                object v_val__113360 = F_dv_find(v_prop, "value", "");
                if (R.Bool(R.not(F_dv_record_like(v_val__113360))))
                {
                    return v_val__113360;
                }
            }
        }
        if (R.Bool(R.eq(v_op__113195, "all")))
        {
            object v_items__113492 = F_dv_find(v_prop, "items", new System.Collections.Generic.List<object>(0) { });
            object v_i__113539 = R.number("0");
            object v_n__113557 = R.count(v_items__113492);
            while (R.Bool(R.lt(v_i__113539, v_n__113557)))
            {
                object v_r__113614 = F_dvp_extract_required_room(R.at(v_items__113492, v_i__113539), v_loc_ref);
                if (R.Bool(R.not(R.eq(v_r__113614, ""))))
                {
                    return v_r__113614;
                }
                v_i__113539 = R.add(v_i__113539, R.number("1"));
            }
        }
        return "";
    }

    public static object F_dvp_find_action(object v_actions, object v_id)
    {
        object v_key__113790 = new System.Collections.Generic.List<object>(2) { "dvp-find-action", v_id };
        object v_cached__113828 = F_dvs_declaration_cache_get(v_actions, v_key__113790);
        if (R.Bool(R.at(v_cached__113828, R.number("0"))))
        {
            return R.at(v_cached__113828, R.number("1"));
        }
        object v_i__113928 = R.number("0");
        object v_result__113942 = false;
        while (R.Bool(R.lt(v_i__113928, R.count(v_actions))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(v_actions, v_i__113928), "id"), v_id)))
            {
                v_result__113942 = R.at(v_actions, v_i__113928);
            }
            v_i__113928 = R.add(v_i__113928, R.number("1"));
        }
        return F_dvs_declaration_cache_put(v_actions, v_key__113790, v_result__113942);
    }

    public static object F_dvp_view_visible_context(object v_actor, object v_mind)
    {
        object v_result__114423 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.not(F_dv_has_key(v_actor, "location_ref"))))
        {
            return v_result__114423;
        }
        object v_own__114511 = F_dv_read(v_mind, F_dv_get(v_actor, "location_ref"));
        if (R.Bool(R.not(F_dv_uncontested(v_own__114511))))
        {
            return v_result__114423;
        }
        object v_previous__114613 = F_dv_find(v_actor, "visible_context", new System.Collections.Generic.List<object>(0) { });
        object v_i__114670 = R.number("0");
        while (R.Bool(R.lt(v_i__114670, R.count(v_previous__114613))))
        {
            object v_entry__114722 = R.at(v_previous__114613, v_i__114670);
            if (R.Bool(R.eq(F_dv_find(R.at(v_entry__114722, R.number("1")), "location", false), F_dv_get(v_own__114511, "value"))))
            {
                _ = R.push(v_result__114423, v_entry__114722);
            }
            v_i__114670 = R.add(v_i__114670, R.number("1"));
        }
        return v_result__114423;
    }

    public static object F_dvp_actor_view(object v_actor, object v_mind)
    {
        v_actor = R.snapshot(v_actor);
        object v_result__114947 = new System.Collections.Generic.List<object>(0) { };
        object v_i__114967 = R.number("0");
        while (R.Bool(R.lt(v_i__114967, R.count(v_actor))))
        {
            object v_entry__115018 = R.at(v_actor, v_i__114967);
            object v_local_1__115047 = false;
            if (R.Bool(R.eq(R.at(v_entry__115018, R.number("0")), "mind")))
            {
                v_local_1__115047 = F_dv_pair("mind", v_mind);
            }
            else
            {
                if (R.Bool(R.eq(R.at(v_entry__115018, R.number("0")), "visible_context")))
                {
                    v_local_1__115047 = F_dv_pair("visible_context", F_dvp_view_visible_context(v_actor, v_mind));
                }
                else
                {
                    v_local_1__115047 = v_entry__115018;
                }
            }
            _ = R.push(v_result__114947, v_local_1__115047);
            v_i__114967 = R.add(v_i__114967, R.number("1"));
        }
        if (R.Bool((R.Bool(R.eq(F_dv_find(v_mind, "domain", ""), "prediction")) ? (object)(R.not(F_dv_has_key(v_actor, "planning_evidence_mind"))) : (object)(false))))
        {
            v_result__114947 = F_dv_with(v_result__114947, "planning_evidence_mind", F_dv_get(v_actor, "mind"));
        }
        return v_result__114947;
    }

    public static object F_dvp_communication_progress(object v_actor, object v_action)
    {
        v_actor = R.snapshot(v_actor);
        object v_goals__115643 = F_dv_find(F_dv_get(v_actor, "policy"), "goals", new System.Collections.Generic.List<object>(0) { });
        // Preserve the original eager basis/availability validation for every
        // communicate goal, in declaration order, once per complete actor
        // snapshot. Argument requirements remain specific to the actual action.
        // Nontext action identifiers retain the original equality/scan path.
        var availableIndex = false;
        if (R.Bool(F_dv_record_like(v_action)) && R.Bool(F_dv_has_key(v_action, "id")) &&
            F_dv_get(v_action, "id") is string method)
        {
            var key = new List<object> { "owned-available-communication-methods-v2" };
            var cached = ReadCachedValue("plan", v_actor, key);
            object index;
            if (R.Bool(R.at(cached, R.number("0"))))
            {
                index = R.at(cached, R.number("1"));
            }
            else
            {
                var groups = new Dictionary<string, List<object>>(StringComparer.Ordinal);
                foreach (var goal in R.Items(v_goals__115643))
                {
                    if (!R.Bool(R.eq(F_dv_find(goal, "kind", ""), "communicate"))) continue;
                    var identifier = F_dv_find(goal, "method_id", "");
                    var available = R.Bool(F_dv_and(
                        R.lt(R.number("0"), R.count(F_dv_find(goal, "basis", new List<object>()))),
                        R.eq(F_dv_truth(F_dv_get(v_actor, "mind"),
                            F_dv_find(goal, "available_when", F_dv_true())), "true")));
                    if (identifier is not string id || !available) continue;
                    if (!groups.TryGetValue(id, out var matching)) groups[id] = matching = new List<object>();
                    matching.Add(goal);
                }
                var entries = new List<object>(groups.Count);
                foreach (var group in groups) entries.Add(F_dv_pair(group.Key, group.Value));
                index = R.snapshot(F_dvs_plan_cache_put(v_actor, key, entries));
            }
            v_goals__115643 = F_dv_find(index, method, new List<object>());
            availableIndex = true;
        }
        object v_i__115694 = R.number("0");
        object v_value__115708 = false;
        while (R.Bool(R.lt(v_i__115694, R.count(v_goals__115643))))
        {
            object v_goal__115767 = R.at(v_goals__115643, v_i__115694);
            if (R.Bool(R.eq(F_dv_find(v_goal__115767, "kind", ""), "communicate")))
            {
                if (availableIndex || R.Bool(F_dv_and(R.eq(F_dv_find(v_goal__115767, "method_id", ""), F_dv_get(v_action, "id")), F_dv_and(R.lt(R.number("0"), R.count(F_dv_find(v_goal__115767, "basis", new System.Collections.Generic.List<object>(0) { }))), R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(v_goal__115767, "available_when", new System.Collections.Generic.List<object>(2) { F_dv_pair("op", "constant"), F_dv_pair("value", "true") })), "true")))))
                {
                    if (R.Bool(F_dv_and(F_dv_find(v_goal__115767, "requires_argument", false), F_dv_find(v_action, "speech_from_argument", false))))
                    {
                        if (R.Bool(F_dv_find(v_action, "argument_available", false)))
                        {
                            v_value__115708 = true;
                        }
                    }
                    if (R.Bool(R.not(F_dv_find(v_goal__115767, "requires_argument", false))))
                    {
                        v_value__115708 = true;
                    }
                }
            }
            v_i__115694 = R.add(v_i__115694, R.number("1"));
        }
        return v_value__115708;
    }

    public static object F_dvp_achievement_pending_work(object v_actor, object v_action, object v_pending)
    {
        object v_i__116492 = R.number("0");
        if (R.Bool(R.eq(R.count(v_pending), R.number("0"))))
        {
            return new System.Collections.Generic.List<object>(3) { F_dv_pair("value", R.number("0")), F_dv_pair("forecasts", R.number("0")), F_dv_pair("forecast_reuses", R.number("0")) };
        }
        object v_name__116603 = F_dv_find(v_action, "predict_law", "");
        object v_adoption__116653 = F_dv_find(F_dv_get(F_dv_get(v_actor, "mind"), "adoptions"), v_name__116603, false);
        if (R.Bool(R.eq(v_adoption__116653, false)))
        {
            return new System.Collections.Generic.List<object>(3) { F_dv_pair("value", R.number("0")), F_dv_pair("forecasts", R.number("0")), F_dv_pair("forecast_reuses", R.number("0")) };
        }
        if (R.Bool((R.Bool(R.not(F_dv_get(v_adoption__116653, "active"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_adoption__116653, "mode"), "predict"))))))
        {
            return new System.Collections.Generic.List<object>(3) { F_dv_pair("value", R.number("0")), F_dv_pair("forecasts", R.number("0")), F_dv_pair("forecast_reuses", R.number("0")) };
        }
        object v_relevant__116931 = false;
        v_i__116492 = R.number("0");
        while (R.Bool((R.Bool(R.lt(v_i__116492, R.count(v_pending))) ? (object)(R.not(v_relevant__116931)) : (object)(false))))
        {
            object v_refs__117020 = new System.Collections.Generic.List<object>(0) { };
            if (R.Bool(F_dv_has_key(R.at(v_pending, v_i__116492), "planning_refs")))
            {
                v_refs__117020 = F_dv_get(R.at(v_pending, v_i__116492), "planning_refs");
            }
            else
            {
                v_refs__117020 = F_dvs_sim_refs(F_dv_get(R.at(v_pending, v_i__116492), "proposition"));
            }
            object v_j__117208 = R.number("0");
            while (R.Bool((R.Bool(R.lt(v_j__117208, R.count(F_dv_get(F_dv_get(v_adoption__116653, "law"), "effects")))) ? (object)(R.not(v_relevant__116931)) : (object)(false))))
            {
                object v_r__117297 = R.number("0");
                while (R.Bool(R.lt(v_r__117297, R.count(v_refs__117020))))
                {
                    if (R.Bool(F_dv_same_local_ref(F_dv_get(v_actor, "id"), F_dv_get(R.at(F_dv_get(F_dv_get(v_adoption__116653, "law"), "effects"), v_j__117208), "ref"), R.at(v_refs__117020, v_r__117297))))
                    {
                        v_relevant__116931 = true;
                    }
                    v_r__117297 = R.add(v_r__117297, R.number("1"));
                }
                v_j__117208 = R.add(v_j__117208, R.number("1"));
            }
            v_i__116492 = R.add(v_i__116492, R.number("1"));
        }
        if (R.Bool(R.not(v_relevant__116931)))
        {
            return new System.Collections.Generic.List<object>(3) { F_dv_pair("value", R.number("0")), F_dv_pair("forecasts", R.number("0")), F_dv_pair("forecast_reuses", R.number("0")) };
        }
        object v_prediction__117624 = F_dvp_forecast(F_dv_get(v_actor, "mind"), v_name__116603);
        object v_forecasts__117676 = R.number("1");
        object v_reuses__117698 = R.number("0");
        if (R.Bool(F_dv_find(v_prediction__117624, "planning_cache_hit", false)))
        {
            v_forecasts__117676 = R.number("0");
            v_reuses__117698 = R.number("1");
        }
        if (R.Bool(R.not(R.eq(F_dv_get(v_prediction__117624, "status"), "applied"))))
        {
            return new System.Collections.Generic.List<object>(3) { F_dv_pair("value", R.number("0")), F_dv_pair("forecasts", v_forecasts__117676), F_dv_pair("forecast_reuses", v_reuses__117698) };
        }
        object v_value__117933 = R.number("0");
        object matchedGoals = new List<object>();
        v_i__116492 = R.number("0");
        while (R.Bool(R.lt(v_i__116492, R.count(v_pending))))
        {
            if (R.Bool(R.eq(F_dv_truth(F_dv_get(v_prediction__117624, "world"), F_dv_get(R.at(v_pending, v_i__116492), "proposition")), "true")))
            {
                v_value__117933 = R.add(v_value__117933, F_dv_find(R.at(v_pending, v_i__116492), "weight", R.number("1")));
                if (R.Bool(R.eq(F_dv_find(v_action, "kind", ""), "move")))
                {
                    object goal = R.at(v_pending, v_i__116492);
                    _ = R.push(matchedGoals, new List<object> { F_dv_pair("proposition", F_dv_get(goal, "proposition")), F_dv_pair("basis", F_dv_find(goal, "basis", new List<object>())), F_dv_pair("weight", F_dv_find(goal, "weight", R.number("1"))) });
                }
            }
            v_i__116492 = R.add(v_i__116492, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(4) { F_dv_pair("value", v_value__117933), F_dv_pair("forecasts", v_forecasts__117676), F_dv_pair("forecast_reuses", v_reuses__117698), F_dv_pair("matched_goals", matchedGoals) };
    }

    public static object F_dvp_refresh_current_evidence(object v_actor, object v_goals)
    {
        object v_result__118356 = new System.Collections.Generic.List<object>(0) { };
        object v_i__118376 = R.number("0");
        while (R.Bool(R.lt(v_i__118376, R.count(v_goals))))
        {
            object v_goal__118425 = R.at(v_goals, v_i__118376);
            if (R.Bool((R.Bool(R.eq(F_dv_find(v_goal__118425, "kind", ""), "evidence")) ? (object)(F_dv_has_key(v_goal__118425, "refresh_until")) : (object)(false))))
            {
                if (R.Bool(R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_goal__118425, "refresh_until")), "true"))))
                {
                    if (R.Bool(F_dv_has_key(v_goal__118425, "refresh_refs")))
                    {
                        object v_relevant_time__118675 = F_dv_find(v_goal__118425, "minimum_time", R.number("0"));
                        object v_r__118748 = R.number("0");
                        while (R.Bool(R.lt(v_r__118748, R.count(F_dv_get(v_goal__118425, "refresh_refs")))))
                        {
                            object v_cell__118841 = F_dv_read(F_dv_get(v_actor, "mind"), R.at(F_dv_get(v_goal__118425, "refresh_refs"), v_r__118748));
                            if (R.Bool(F_dv_get(v_cell__118841, "known")))
                            {
                                v_relevant_time__118675 = F_dv_max(v_relevant_time__118675, F_dv_find(v_cell__118841, "time", R.number("0")));
                            }
                            v_r__118748 = R.add(v_r__118748, R.number("1"));
                        }
                        v_goal__118425 = F_dv_with(v_goal__118425, "minimum_time", v_relevant_time__118675);
                    }
                }
            }
            _ = R.push(v_result__118356, v_goal__118425);
            v_i__118376 = R.add(v_i__118376, R.number("1"));
        }
        return v_result__118356;
    }

    public static object F_dvp_forecast(object v_mind, object v_name)
    {
        object v_key__119314 = new System.Collections.Generic.List<object>(2) { "dvp_forecast", v_name };
        object v_cached__119351 = F_dvs_plan_cache_get(v_mind, v_key__119314);
        if (R.Bool(R.at(v_cached__119351, R.number("0"))))
        {
            return F_dv_with(R.at(v_cached__119351, R.number("1")), "planning_cache_hit", true);
        }
        object v_result__119478 = F_dv_predict(F_dv_with_many(v_mind, new System.Collections.Generic.List<object>(3) { F_dv_pair("domain", "mind"), F_dv_pair("history", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("seen", new System.Collections.Generic.List<object>(0) { }) }), v_name, R.cat("planning-forecast-", R.show(F_dv_get(v_mind, "revision"))), F_dv_find(v_mind, "time", R.number("0")), F_dv_get(v_mind, "revision"));
        _ = F_dvs_plan_cache_put(v_mind, v_key__119314, v_result__119478);
        return F_dv_with(v_result__119478, "planning_cache_hit", false);
    }

    public static object F_dvp_inspection_freshness(object v_actor, object v_mind)
    {
        object v_declared__119897 = F_dv_find(F_dv_get(v_actor, "policy"), "goals", new System.Collections.Generic.List<object>(0) { });
        object v_key__119951 = new System.Collections.Generic.List<object>(2) { "dvp_inspection_freshness-ref-key-v2", v_declared__119897 };
        object v_cached__120015 = F_dvs_plan_cache_get(v_mind, v_key__119951);
        if (R.Bool(R.at(v_cached__120015, R.number("0"))))
        {
            return R.at(v_cached__120015, R.number("1"));
        }
        object v_goals__120105 = F_dvp_refresh_current_evidence(F_dvp_actor_view(v_actor, v_mind), v_declared__119897);
        object v_index__120189 = new System.Collections.Generic.List<object>(0) { };
        object v_i__120208 = R.number("0");
        while (R.Bool(R.lt(v_i__120208, R.count(v_goals__120105))))
        {
            object v_goal__120257 = R.at(v_goals__120105, v_i__120208);
            if (R.Bool((R.Bool(R.eq(F_dv_find(v_goal__120257, "kind", ""), "evidence")) ? (object)(F_dv_has_key(v_goal__120257, "ref")) : (object)(false))))
            {
                object v_name__120369 = F_dv_ref_lookup_key(F_dv_get(v_goal__120257, "ref"));
                object v_entries__120420 = F_dv_append(F_dv_find(v_index__120189, v_name__120369, new System.Collections.Generic.List<object>(0) { }), new System.Collections.Generic.List<object>(6) { F_dv_pair("ref", F_dv_get(v_goal__120257, "ref")), F_dv_pair("minimum_time", F_dv_find(v_goal__120257, "minimum_time", R.number("0"))), F_dv_pair("available", F_dv_truth(v_mind, F_dv_find(v_goal__120257, "available_when", F_dv_true()))), F_dv_pair("due", F_dvp_evidence_due(v_actor, v_goal__120257)), F_dv_pair("source", F_dv_find(v_goal__120257, "source", "perception")), F_dv_pair("topic", F_dvp_topic(v_actor, v_goal__120257)) });
                object v_owned_entries__120744 = F_dv_clone(v_entries__120420);
                object v_position__120794 = R.record_index(v_index__120189, v_name__120369);
                object v_pair__120850 = new System.Collections.Generic.List<object>(2) { v_name__120369, v_owned_entries__120744 };
                if (R.Bool(R.not(R.lt(v_position__120794, R.number("0")))))
                {
                    _ = R.replace(v_index__120189, v_position__120794, v_pair__120850);
                }
                else
                {
                    _ = R.push(v_index__120189, v_pair__120850);
                }
            }
            v_i__120208 = R.add(v_i__120208, R.number("1"));
        }
        return F_dvs_plan_cache_put(v_mind, v_key__119951, v_index__120189);
    }
}
}

// --- Source: ActionCosts.cs ---
namespace DuelView.Core
{


/// <summary>World / ActionCosts: native Core mechanisms.</summary>
public static partial class DVSimulationKernel
{
    public static object F_dv_action_cost_vector(object v_time, object v_computation, object v_stamina)
    {
        _ = F_dv_require(F_dv_and(R.not(R.lt(v_time, R.number("0"))), F_dv_and(R.not(R.lt(v_computation, R.number("0"))), R.not(R.lt(v_stamina, R.number("0"))))), "Negative action cost");
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("time", v_time), F_dv_pair("computation", v_computation), F_dv_pair("stamina", v_stamina) };
    }

    public static object F_dv_action_cost_validate(object v_v)
    {
        return F_dv_action_cost_vector(F_dv_get(v_v, "time"), F_dv_get(v_v, "computation"), F_dv_get(v_v, "stamina"));
    }

    public static object F_dv_action_cost_le(object v_a, object v_b)
    {
        return F_dv_and(R.not(R.lt(F_dv_get(v_b, "time"), F_dv_get(v_a, "time"))), F_dv_and(R.not(R.lt(F_dv_get(v_b, "computation"), F_dv_get(v_a, "computation"))), R.not(R.lt(F_dv_get(v_b, "stamina"), F_dv_get(v_a, "stamina")))));
    }

    public static object F_dv_action_cost_add(object v_a, object v_b)
    {
        return F_dv_action_cost_vector(R.add(F_dv_get(v_a, "time"), F_dv_get(v_b, "time")), R.add(F_dv_get(v_a, "computation"), F_dv_get(v_b, "computation")), R.add(F_dv_get(v_a, "stamina"), F_dv_get(v_b, "stamina")));
    }

    public static object F_dv_action_cost_sub(object v_a, object v_b)
    {
        return F_dv_action_cost_vector(R.sub(F_dv_get(v_a, "time"), F_dv_get(v_b, "time")), R.sub(F_dv_get(v_a, "computation"), F_dv_get(v_b, "computation")), R.sub(F_dv_get(v_a, "stamina"), F_dv_get(v_b, "stamina")));
    }

    public static object F_dv_action_cost_bounds(object v_minimum, object v_maximum)
    {
        _ = F_dv_action_cost_validate(v_minimum);
        _ = F_dv_action_cost_validate(v_maximum);
        _ = F_dv_require(F_dv_action_cost_le(v_minimum, v_maximum), "Action minimum exceeds maximum");
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("minimum", v_minimum), F_dv_pair("maximum", v_maximum) };
    }

    public static object F_dv_action_cost_ledger(object v_id, object v_balances)
    {
        _ = F_dv_action_cost_validate(v_balances);
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("id", v_id), F_dv_pair("revision", R.number("0")), F_dv_pair("balances", v_balances), F_dv_pair("reserved", F_dv_action_cost_vector(R.number("0"), R.number("0"), R.number("0"))) }, new System.Collections.Generic.List<object>(3) { F_dv_pair("receipts", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("events", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("spent", F_dv_action_cost_vector(R.number("0"), R.number("0"), R.number("0"))) });
    }

    public static object F_dv_action_cost_quote(object v_ledger, object v_world, object v_action, object v_event, object v_bounds)
    {
        _ = F_dv_domain(v_world, "physical");
        _ = F_dv_action_cost_bounds(F_dv_get(v_bounds, "minimum"), F_dv_get(v_bounds, "maximum"));
        _ = F_dv_require(R.not(F_dv_has(F_dv_get(v_ledger, "events"), v_event)), "Repeated cost event");
        _ = F_dv_require(R.not(F_dv_has(F_dv_get(v_world, "seen"), v_event)), "Repeated world event");
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("ledger", F_dv_get(v_ledger, "id")), F_dv_pair("revision", F_dv_get(v_ledger, "revision")), F_dv_pair("world", F_dv_get(v_world, "id")), F_dv_pair("world_revision", F_dv_get(v_world, "revision")) }, new System.Collections.Generic.List<object>(3) { F_dv_pair("action", v_action), F_dv_pair("event", v_event), F_dv_pair("bounds", v_bounds) });
    }

    public static object F_dv_action_cost_admit(object v_ledger, object v_world, object v_quote)
    {
        object v_maximum__113732 = F_dv_get(F_dv_get(v_quote, "bounds"), "maximum");
        object v_event__113771 = F_dv_get(v_quote, "event");
        _ = F_dv_require(F_dv_and(R.eq(F_dv_get(v_ledger, "id"), F_dv_get(v_quote, "ledger")), R.eq(F_dv_get(v_ledger, "revision"), F_dv_get(v_quote, "revision"))), "Stale cost quote");
        _ = F_dv_require(F_dv_and(R.eq(F_dv_get(v_world, "id"), F_dv_get(v_quote, "world")), R.eq(F_dv_get(v_world, "revision"), F_dv_get(v_quote, "world_revision"))), "Stale cost world");
        _ = F_dv_action_cost_quote(v_ledger, v_world, F_dv_get(v_quote, "action"), v_event__113771, F_dv_get(v_quote, "bounds"));
        _ = F_dv_require(F_dv_action_cost_le(v_maximum__113732, F_dv_get(v_ledger, "balances")), "Insufficient action resources");
        object v_receipt__114188 = F_dv_with(v_quote, "state", "reserved");
        object v_next__114242 = F_dv_with(v_ledger, "revision", R.add(F_dv_get(v_ledger, "revision"), R.number("1")));
        v_next__114242 = F_dv_with(v_next__114242, "balances", F_dv_action_cost_sub(F_dv_get(v_ledger, "balances"), v_maximum__113732));
        v_next__114242 = F_dv_with(v_next__114242, "reserved", F_dv_action_cost_add(F_dv_get(v_ledger, "reserved"), v_maximum__113732));
        v_next__114242 = F_dv_with(v_next__114242, "events", F_dv_append(F_dv_get(v_ledger, "events"), v_event__113771));
        v_next__114242 = F_dv_with(v_next__114242, "receipts", F_dv_with(F_dv_get(v_ledger, "receipts"), v_event__113771, v_receipt__114188));
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("ledger", v_next__114242), F_dv_pair("receipt", v_receipt__114188) };
    }

    public static object F_dv_action_cost_actual(object v_receipt, object v_actual)
    {
        _ = F_dv_action_cost_validate(v_actual);
        _ = F_dv_require(F_dv_and(F_dv_action_cost_le(F_dv_get(F_dv_get(v_receipt, "bounds"), "minimum"), v_actual), F_dv_action_cost_le(v_actual, F_dv_get(F_dv_get(v_receipt, "bounds"), "maximum"))), "Actual action cost outside bounds");
        return v_actual;
    }

    public static object F_dv_action_cost_settle(object v_ledger, object v_receipt, object v_actual, object v_outcome)
    {
        object v_event__114990 = F_dv_get(v_receipt, "event");
        object v_stored__115020 = F_dv_get(F_dv_get(v_ledger, "receipts"), v_event__114990);
        object v_maximum__115068 = F_dv_get(F_dv_get(v_receipt, "bounds"), "maximum");
        _ = F_dv_require(F_dv_and(R.eq(F_dv_get(v_ledger, "id"), F_dv_get(v_receipt, "ledger")), F_dv_same(v_stored__115020, v_receipt)), "Receipt mismatch or already settled");
        _ = F_dv_require(R.eq(F_dv_get(v_stored__115020, "state"), "reserved"), "Receipt already settled");
        _ = F_dv_require(F_dv_or(R.eq(v_outcome, "applied"), R.eq(v_outcome, "rejected")), "Unknown settlement outcome");
        _ = F_dv_action_cost_actual(v_receipt, v_actual);
        object v_next__115444 = F_dv_with(v_ledger, "revision", R.add(F_dv_get(v_ledger, "revision"), R.number("1")));
        object v_settled__115510 = F_dv_with_many(v_receipt, new System.Collections.Generic.List<object>(3) { F_dv_pair("state", "settled"), F_dv_pair("actual", v_actual), F_dv_pair("outcome", v_outcome) });
        v_next__115444 = F_dv_with(v_next__115444, "balances", F_dv_action_cost_add(F_dv_get(v_ledger, "balances"), F_dv_action_cost_sub(v_maximum__115068, v_actual)));
        v_next__115444 = F_dv_with(v_next__115444, "reserved", F_dv_action_cost_sub(F_dv_get(v_ledger, "reserved"), v_maximum__115068));
        v_next__115444 = F_dv_with(v_next__115444, "spent", F_dv_action_cost_add(F_dv_get(v_ledger, "spent"), v_actual));
        return F_dv_with(v_next__115444, "receipts", F_dv_with(F_dv_get(v_ledger, "receipts"), v_event__114990, v_settled__115510));
    }

    public static object F_dv_action_cost_completion_time(object v_physical, object v_receipt, object v_actual)
    {
        object v_now__116186 = F_dv_find(v_physical, "time", R.number("0"));
        if (R.Bool(F_dv_has_key(v_receipt, "scheduled_start")))
        {
            object v_due__116280 = R.add(F_dv_get(v_receipt, "scheduled_start"), F_dv_get(v_actual, "time"));
            _ = F_dv_require(R.not(R.lt(v_now__116186, v_due__116280)), "Scheduled action has not finished waiting");
            return v_now__116186;
        }
        return R.add(v_now__116186, F_dv_get(v_actual, "time"));
    }

    public static object F_dv_action_cost_clock_commit(object v_physical, object v_receipt, object v_actual, object v_status)
    {
        object v_event__116524 = F_dv_get(v_receipt, "event");
        object v_time__116554 = F_dv_action_cost_completion_time(v_physical, v_receipt, v_actual);
        object v_history__116627 = F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("id", v_event__116524), F_dv_pair("time", v_time__116554), F_dv_pair("kind", "action-cost"), F_dv_pair("status", v_status) }, new System.Collections.Generic.List<object>(2) { F_dv_pair("receipt", v_receipt), F_dv_pair("actual", v_actual) });
        return F_dv_with_many(v_physical, new System.Collections.Generic.List<object>(4) { F_dv_pair("time", v_time__116554), F_dv_pair("revision", R.add(F_dv_get(v_physical, "revision"), R.number("1"))), F_dv_pair("seen", F_dv_append(F_dv_get(v_physical, "seen"), v_event__116524)), F_dv_pair("history", F_dv_append(F_dv_get(v_physical, "history"), v_history__116627)) });
    }

    public static object F_dv_action_cost_execution_metadata(object v_execution, object v_receipt, object v_actual)
    {
        return F_dv_with_many(v_execution, new System.Collections.Generic.List<object>(3) { F_dv_pair("event", F_dv_get(v_receipt, "event")), F_dv_pair("time", F_dv_get(F_dv_get(v_execution, "world"), "time")), F_dv_pair("actual_cost", v_actual) });
    }

    public static object F_dv_action_cost_execute(object v_ledger, object v_physical, object v_quote, object v_actual)
    {
        object v_admitted__117187 = F_dv_action_cost_admit(v_ledger, v_physical, v_quote);
        object v_receipt__117252 = F_dv_get(v_admitted__117187, "receipt");
        _ = F_dv_action_cost_actual(v_receipt__117252, v_actual);
        object v_execution__117330 = F_dv_execute(v_physical, F_dv_get(v_receipt__117252, "action"), F_dv_get(v_receipt__117252, "event"), F_dv_action_cost_completion_time(v_physical, v_receipt__117252, v_actual), F_dv_get(v_receipt__117252, "world_revision"));
        if (R.Bool(R.eq(F_dv_get(v_execution__117330, "status"), "applied")))
        {
            _ = false;
        }
        else
        {
            v_execution__117330 = F_dv_with(v_execution__117330, "world", F_dv_action_cost_clock_commit(v_physical, v_receipt__117252, v_actual, F_dv_get(v_execution__117330, "status")));
        }
        object v_local_1__117671 = false;
        if (R.Bool(R.eq(F_dv_get(v_execution__117330, "status"), "applied")))
        {
            v_local_1__117671 = "applied";
        }
        else
        {
            v_local_1__117671 = "rejected";
        }
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("ledger", F_dv_action_cost_settle(F_dv_get(v_admitted__117187, "ledger"), v_receipt__117252, v_actual, v_local_1__117671)), F_dv_pair("execution", F_dv_action_cost_execution_metadata(v_execution__117330, v_receipt__117252, v_actual)), F_dv_pair("receipt", v_receipt__117252) };
    }

    public static object F_dv_action_cost_finish(object v_ledger, object v_physical, object v_quote, object v_actual, object v_outcome)
    {
        object v_admitted__118055 = F_dv_action_cost_admit(v_ledger, v_physical, v_quote);
        object v_receipt__118120 = F_dv_get(v_admitted__118055, "receipt");
        _ = F_dv_action_cost_actual(v_receipt__118120, v_actual);
        object v_next__118198 = F_dv_action_cost_clock_commit(v_physical, v_receipt__118120, v_actual, v_outcome);
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("ledger", F_dv_action_cost_settle(F_dv_get(v_admitted__118055, "ledger"), v_receipt__118120, v_actual, v_outcome)), F_dv_pair("execution", F_dv_action_cost_execution_metadata(F_dv_result(v_outcome, v_next__118198, new System.Collections.Generic.List<object>(0) { }, F_dv_single(F_dv_get(v_receipt__118120, "event"))), v_receipt__118120, v_actual)), F_dv_pair("receipt", v_receipt__118120) };
    }
}
}

// --- Source: Laws.cs ---
namespace DuelView.Core
{


/// <summary>World / Laws: native Core mechanisms.</summary>
public static partial class DVSimulationKernel
{
    public static object F_dv_held_for(object v_ref, object v_value, object v_clock, object v_duration)
    {
        _ = F_dv_require((R.Bool(R.is_numeric_value(v_duration)) ? (object)(R.not(R.lt(v_duration, R.number("0")))) : (object)(false)), "Held-for duration must be nonnegative");
        return new System.Collections.Generic.List<object>(5) { F_dv_pair("op", "held_for"), F_dv_pair("ref", v_ref), F_dv_pair("value", v_value), F_dv_pair("clock", v_clock), F_dv_pair("duration", v_duration) };
    }

    public static object F_dv_assign(object v_ref, object v_value)
    {
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("op", "assign"), F_dv_pair("ref", v_ref), F_dv_pair("value", v_value) };
    }

    public static object F_dv_increment(object v_ref, object v_amount)
    {
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("op", "increment"), F_dv_pair("ref", v_ref), F_dv_pair("value", v_amount) };
    }

    public static object F_dv_axiom(object v_name, object v_version, object v_premise, object v_effects)
    {
        return new System.Collections.Generic.List<object>(4) { F_dv_pair("name", v_name), F_dv_pair("version", v_version), F_dv_pair("premise", F_dv_clone(v_premise)), F_dv_pair("effects", F_dv_clone(v_effects)) };
    }

    public static object F_dv_contract(object v_name, object v_version, object v_premise, object v_effects, object v_postcondition)
    {
        return F_dv_with(F_dv_axiom(v_name, v_version, v_premise, v_effects), "postcondition", v_postcondition);
    }

    public static object F_dv_instance(object v_id, object v_law)
    {
        return F_dv_with_many(v_law, new System.Collections.Generic.List<object>(2) { F_dv_pair("schema", F_dv_get(v_law, "name")), F_dv_pair("name", v_id) });
    }

    public static object F_dv_checked_adoption(object v_domain, object v_adoptions, object v_law, object v_mode, object v_confidence, object v_basis)
    {
        _ = F_dv_unit(v_confidence);
        object v_local_1__29389 = false;
        if (R.Bool(R.eq(v_domain, "physical")))
        {
            v_local_1__29389 = R.eq(v_confidence, R.number("1"));
        }
        else
        {
            v_local_1__29389 = true;
        }
        _ = F_dv_require(v_local_1__29389, "Physical laws require certainty");
        _ = F_dv_require(R.lt(R.number("0"), R.count(v_basis)), "Adoption needs a basis");
        _ = F_dv_require(R.not(F_dv_has_key(v_adoptions, F_dv_get(v_law, "name"))), "Duplicate law instance: provide a distinct instance identity");
        object v_local_2__29754 = false;
        if (R.Bool(R.eq(v_domain, "physical")))
        {
            v_local_2__29754 = R.eq(v_mode, "enforce");
        }
        else
        {
            v_local_2__29754 = F_dv_and(R.eq(v_domain, "mind"), F_dv_or(R.eq(v_mode, "predict"), F_dv_or(R.eq(v_mode, "infer"), R.eq(v_mode, "norm"))));
        }
        _ = F_dv_require(v_local_2__29754, "Adoption mode is incompatible with world");
        return F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("law", v_law), F_dv_pair("mode", v_mode), F_dv_pair("confidence", v_confidence), F_dv_pair("basis", v_basis) }, "active", true);
    }

    public static object F_dv_adopt(object v_world, object v_law, object v_mode, object v_confidence, object v_basis)
    {
        object v_adoption__30198 = F_dv_checked_adoption(F_dv_get(v_world, "domain"), F_dv_get(v_world, "adoptions"), v_law, v_mode, v_confidence, v_basis);
        return F_dv_with_many(v_world, new System.Collections.Generic.List<object>(2) { F_dv_pair("adoptions", F_dv_with(F_dv_get(v_world, "adoptions"), F_dv_get(v_law, "name"), v_adoption__30198)), F_dv_pair("revision", R.add(F_dv_get(v_world, "revision"), R.number("1"))) });
    }

    public static object F_dv_adopt_into(object v_domain, object v_owned_adoptions, object v_law, object v_mode, object v_confidence, object v_basis)
    {
        object v_adoption__30587 = F_dv_checked_adoption(v_domain, v_owned_adoptions, v_law, v_mode, v_confidence, v_basis);
        _ = R.push(v_owned_adoptions, F_dv_pair(F_dv_get(v_law, "name"), v_adoption__30587));
        return true;
    }

    public static object F_dv_finish_owned_adoptions(object v_world, object v_owned_adoptions)
    {
        object v_previous_count__30810 = R.count(F_dv_get(v_world, "adoptions"));
        _ = F_dv_require(R.not(R.lt(R.count(v_owned_adoptions), v_previous_count__30810)), "Adoption builder removed inherited entries");
        object v_i__30967 = R.number("0");
        while (R.Bool(R.lt(v_i__30967, v_previous_count__30810)))
        {
            _ = F_dv_require(F_dv_same(R.at(F_dv_get(v_world, "adoptions"), v_i__30967), R.at(v_owned_adoptions, v_i__30967)), "Adoption builder changed inherited entries");
            v_i__30967 = R.add(v_i__30967, R.number("1"));
        }
        if (R.Bool(R.eq(v_previous_count__30810, R.count(v_owned_adoptions))))
        {
            return v_world;
        }
        return F_dv_with_many(v_world, new System.Collections.Generic.List<object>(2) { F_dv_pair("adoptions", v_owned_adoptions), F_dv_pair("revision", R.sub(R.add(F_dv_get(v_world, "revision"), R.count(v_owned_adoptions)), v_previous_count__30810)) });
    }

    public static object F_dv_suspend(object v_world, object v_name)
    {
        object v_adoptions__31381 = F_dv_get(v_world, "adoptions");
        return F_dv_with_many(v_world, new System.Collections.Generic.List<object>(2) { F_dv_pair("adoptions", F_dv_with(v_adoptions__31381, v_name, F_dv_with(F_dv_get(v_adoptions__31381, v_name), "active", false))), F_dv_pair("revision", R.add(F_dv_get(v_world, "revision"), R.number("1"))) });
    }

    public static object F_dv_reconsider(object v_world, object v_law, object v_mode, object v_confidence, object v_basis)
    {
        object v_name__31630 = F_dv_get(v_law, "name");
        object v_previous__31654 = F_dv_get(F_dv_get(v_world, "adoptions"), v_name__31630);
        object v_updated__31703 = F_dv_adopt(F_dv_with(v_world, "adoptions", F_dv_without(F_dv_get(v_world, "adoptions"), v_name__31630)), v_law, v_mode, v_confidence, v_basis);
        return F_dv_with(v_updated__31703, "history", F_dv_append(F_dv_get(v_world, "history"), new System.Collections.Generic.List<object>(4) { F_dv_pair("kind", "reconsideration"), F_dv_pair("previous", v_previous__31654), F_dv_pair("current", F_dv_get(F_dv_get(v_updated__31703, "adoptions"), v_name__31630)), F_dv_pair("basis", v_basis) }));
    }

    public static object F_dv_premise_confidence(object v_world, object v_proposition)
    {
        object v_op__32047 = F_dv_get(v_proposition, "op");
        if (R.Bool(R.eq(v_op__32047, "constant")))
        {
            return R.number("1");
        }
        else
        {
            if (R.Bool(R.eq(v_op__32047, "not")))
            {
                return F_dv_premise_confidence(v_world, F_dv_get(v_proposition, "item"));
            }
            else
            {
                if (R.Bool(R.eq(v_op__32047, "all")))
                {
                    object v_items__32277 = F_dv_get(v_proposition, "items");
                    object v_i__32323 = R.number("0");
                    object v_confidence__32349 = R.number("1");
                    while (R.Bool(R.lt(v_i__32323, R.count(v_items__32277))))
                    {
                        v_confidence__32349 = F_dv_min(v_confidence__32349, F_dv_premise_confidence(v_world, R.at(v_items__32277, v_i__32323)));
                        v_i__32323 = R.add(v_i__32323, R.number("1"));
                    }
                    return v_confidence__32349;
                }
                else
                {
                    return F_dv_get(F_dv_read(v_world, F_dv_get(v_proposition, "ref")), "confidence");
                }
            }
        }
    }

    public static object F_dv_result(object v_status, object v_world, object v_changes, object v_basis)
    {
        return new System.Collections.Generic.List<object>(4) { F_dv_pair("status", v_status), F_dv_pair("world", F_dv_clone(v_world)), F_dv_pair("changes", F_dv_clone(v_changes)), F_dv_pair("basis", F_dv_clone(v_basis)) };
    }

    public static object F_dv_stage(object v_world, object v_effects, object v_time, object v_basis, object v_confidence)
    {
        object v_stage__32872 = F_dv_clone(v_world);
        object v_changes__32904 = new System.Collections.Generic.List<object>(0) { };
        object v_touched__32925 = new System.Collections.Generic.List<object>(0) { };
        object v_i__32946 = R.number("0");
        object v_status__32960 = "ready";
        while (R.Bool(F_dv_and(R.eq(v_status__32960, "ready"), R.lt(v_i__32946, R.count(v_effects)))))
        {
            object v_effect__33053 = R.at(v_effects, v_i__32946);
            object v_ref__33085 = F_dv_get(v_effect__33053, "ref");
            object v_op__33114 = F_dv_get(v_effect__33053, "op");
            // Read the current owned stage without acquiring its entire object
            // table. The store below still receives the original writable root.
            var stageView = R.snapshot(v_stage__32872);
            object v_before__33141 = F_dv_read_query(stageView, v_ref__33085);
            if (R.Bool(R.not(F_dv_has_key(F_dv_get(stageView, "objects"), F_dv_get(v_ref__33085, "object")))))
            {
                v_status__32960 = "unknown-object";
            }
            else
            {
                if (R.Bool(F_dv_has(v_touched__32925, v_ref__33085)))
                {
                    v_status__32960 = "duplicate-write";
                }
                else
                {
                    if (R.Bool(F_dv_and(R.eq(v_op__33114, "increment"), F_dv_or(R.not(F_dv_get(v_before__33141, "known")), F_dv_get(v_before__33141, "conflicted")))))
                    {
                        v_status__32960 = "conflicted-state";
                    }
                    else
                    {
                        _ = F_dv_require(F_dv_or(R.eq(v_op__33114, "assign"), R.eq(v_op__33114, "increment")), "Unknown effect operator");
                        object v_local_1__33668 = false;
                        if (R.Bool(R.eq(v_op__33114, "assign")))
                        {
                            v_local_1__33668 = F_dv_get(v_effect__33053, "value");
                        }
                        else
                        {
                            v_local_1__33668 = R.add(F_dv_get(v_before__33141, "value"), F_dv_get(v_effect__33053, "value"));
                        }
                        object v_value__33886 = v_local_1__33668;
                        v_stage__32872 = F_dv_stage_store_owned(v_stage__32872, v_ref__33085, F_dv_cell(v_value__33886, v_confidence, v_time, v_basis));
                        _ = R.push(v_changes__32904, new System.Collections.Generic.List<object>(3) { F_dv_pair("ref", v_ref__33085), F_dv_pair("before", v_before__33141), F_dv_pair("after", F_dv_read_query(v_stage__32872, v_ref__33085)) });
                        _ = R.push(v_touched__32925, F_dv_clone(v_ref__33085));
                    }
                }
            }
            v_i__32946 = R.add(v_i__32946, R.number("1"));
        }
        if (R.Bool(R.eq(v_status__32960, "ready")))
        {
            return new System.Collections.Generic.List<object>(4) { F_dv_pair("status", "ready"), F_dv_pair("world", v_stage__32872), F_dv_pair("changes", v_changes__32904), F_dv_pair("basis", v_basis) };
        }
        else
        {
            return F_dv_result(v_status__32960, v_world, new System.Collections.Generic.List<object>(0) { }, v_basis);
        }
    }

    public static object F_dv_infer_stage(object v_world, object v_effects, object v_time, object v_basis, object v_confidence, object v_law_name, object v_event_id)
    {
        object v_stage__34440 = F_dv_clone(v_world);
        object v_changes__34472 = new System.Collections.Generic.List<object>(0) { };
        object v_touched__34493 = new System.Collections.Generic.List<object>(0) { };
        object v_i__34514 = R.number("0");
        object v_status__34528 = "ready";
        object v_profile__34553 = F_dv_find(v_world, "profile", F_dv_observation_profile(new System.Collections.Generic.List<object>(4) { F_dv_pair("perception", R.number("1")), F_dv_pair("testimony", R.number("1")), F_dv_pair("recall", R.number("1")), F_dv_pair("inference", R.number("1")) }, R.number("0.5")));
        while (R.Bool(F_dv_and(R.eq(v_status__34528, "ready"), R.lt(v_i__34514, R.count(v_effects)))))
        {
            object v_effect__34758 = R.at(v_effects, v_i__34514);
            object v_ref__34790 = F_dv_get(v_effect__34758, "ref");
            object v_op__34819 = F_dv_get(v_effect__34758, "op");
            var stageView = R.snapshot(v_stage__34440);
            object v_before__34846 = F_dv_read_query(stageView, v_ref__34790);
            if (R.Bool(R.not(F_dv_has_key(F_dv_get(stageView, "objects"), F_dv_get(v_ref__34790, "object")))))
            {
                v_status__34528 = "unknown-object";
            }
            else
            {
                if (R.Bool(F_dv_has(v_touched__34493, v_ref__34790)))
                {
                    v_status__34528 = "duplicate-write";
                }
                else
                {
                    if (R.Bool(F_dv_and(R.eq(v_op__34819, "increment"), F_dv_or(R.not(F_dv_get(v_before__34846, "known")), F_dv_get(v_before__34846, "conflicted")))))
                    {
                        v_status__34528 = "conflicted-state";
                    }
                    else
                    {
                        _ = F_dv_require(F_dv_or(R.eq(v_op__34819, "assign"), R.eq(v_op__34819, "increment")), "Unknown effect operator");
                        object v_local_1__35373 = false;
                        if (R.Bool(R.eq(v_op__34819, "assign")))
                        {
                            v_local_1__35373 = F_dv_get(v_effect__34758, "value");
                        }
                        else
                        {
                            v_local_1__35373 = R.add(F_dv_get(v_before__34846, "value"), F_dv_get(v_effect__34758, "value"));
                        }
                        object v_value__35591 = v_local_1__35373;
                        object v_claim__35631 = F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("id", F_dv_list3(v_event_id, v_law_name, v_ref__34790)), F_dv_pair("subject", F_dv_get(v_stage__34440, "subject")), F_dv_pair("ref", v_ref__34790), F_dv_pair("value", F_dv_clone(v_value__35591)) }, new System.Collections.Generic.List<object>(5) { F_dv_pair("source", "inference"), F_dv_pair("origin", v_law_name), F_dv_pair("time", v_time), F_dv_pair("trust", v_confidence), F_dv_pair("dependency", F_dv_list3("inference", v_law_name, v_ref__34790)) });
                        object v_old_claims__35915 = F_dv_get(v_before__34846, "claims");
                        object v_local_2__35966 = false;
                        if (R.Bool(F_dv_and(F_dv_get(v_before__34846, "known"), R.eq(R.count(v_old_claims__35915), R.number("0")))))
                        {
                            v_local_2__35966 = F_dv_single(F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("id", F_dv_pair("initial", v_ref__34790)), F_dv_pair("subject", F_dv_get(v_stage__34440, "subject")), F_dv_pair("ref", v_ref__34790), F_dv_pair("value", F_dv_get(v_before__34846, "value")) }, new System.Collections.Generic.List<object>(5) { F_dv_pair("source", "initial"), F_dv_pair("origin", "initial"), F_dv_pair("time", F_dv_get(v_before__34846, "time")), F_dv_pair("trust", F_dv_get(v_before__34846, "confidence")), F_dv_pair("dependency", F_dv_pair("initial", v_ref__34790)) }));
                        }
                        else
                        {
                            v_local_2__35966 = v_old_claims__35915;
                        }
                        object v_claims__36435 = v_local_2__35966;
                        object v_new_claims__36476 = F_dv_replace_claim(v_claims__36435, v_claim__35631);
                        object v_new_cell__36545 = F_dv_aggregate(v_new_claims__36476, v_profile__34553, v_time);
                        v_stage__34440 = F_dv_stage_store_owned(v_stage__34440, v_ref__34790, v_new_cell__36545);
                        _ = R.push(v_changes__34472, new System.Collections.Generic.List<object>(3) { F_dv_pair("ref", v_ref__34790), F_dv_pair("before", v_before__34846), F_dv_pair("after", F_dv_read_query(v_stage__34440, v_ref__34790)) });
                        _ = R.push(v_touched__34493, F_dv_clone(v_ref__34790));
                    }
                }
            }
            v_i__34514 = R.add(v_i__34514, R.number("1"));
        }
        if (R.Bool(R.eq(v_status__34528, "ready")))
        {
            return new System.Collections.Generic.List<object>(4) { F_dv_pair("status", "ready"), F_dv_pair("world", v_stage__34440), F_dv_pair("changes", v_changes__34472), F_dv_pair("basis", v_basis) };
        }
        else
        {
            return F_dv_result(v_status__34528, v_world, new System.Collections.Generic.List<object>(0) { }, v_basis);
        }
    }

    public static object F_dv_evaluate_law_value(object v_world, object v_name, object v_mode, object v_event_id, object v_time, object v_expected_revision, object v_operation)
    {
        _ = F_dv_require((R.Bool(R.eq(v_operation, "verify")) ? (object)(true) : (object)(R.eq(v_operation, "run"))), "Expected verify or run");
        _ = F_dv_require(R.eq(F_dv_get(v_world, "revision"), v_expected_revision), "Stale world revision");
        _ = F_dv_require(R.not(F_dv_has(F_dv_get(v_world, "seen"), v_event_id)), "Repeated world event");
        _ = F_dv_require(R.not(R.lt(v_time, F_dv_find(v_world, "time", R.number("0")))), "World time cannot move backwards");
        _ = F_dv_require((R.Bool((R.Bool(R.eq(F_dv_get(v_world, "domain"), "physical")) ? (object)(R.eq(v_mode, "enforce")) : (object)(false))) ? (object)(true) : (object)((R.Bool(R.eq(F_dv_get(v_world, "domain"), "mind")) ? (object)((R.Bool(R.eq(v_mode, "infer")) ? (object)(true) : (object)(R.eq(v_mode, "predict")))) : (object)(false)))), "Evaluation mode is incompatible with world");
        object v_adoption__37871 = F_dv_get(F_dv_get(v_world, "adoptions"), v_name);
        object v_law__37920 = F_dv_get(v_adoption__37871, "law");
        object v_basis__37947 = F_dv_get(v_adoption__37871, "basis");
        if (R.Bool(R.not(F_dv_get(v_adoption__37871, "active"))))
        {
            return F_dv_result("not-adopted", v_world, new System.Collections.Generic.List<object>(0) { }, v_basis__37947);
        }
        else
        {
            if (R.Bool(R.not(R.eq(F_dv_get(v_adoption__37871, "mode"), v_mode))))
            {
                return F_dv_result("wrong-adoption-mode", v_world, new System.Collections.Generic.List<object>(0) { }, v_basis__37947);
            }
            else
            {
                object v_truth__38210 = F_dv_truth(v_world, F_dv_get(v_law__37920, "premise"));
                if (R.Bool(R.not(R.eq(v_truth__38210, "true"))))
                {
                    return F_dv_result(R.cat("premise-", v_truth__38210), v_world, new System.Collections.Generic.List<object>(0) { }, v_basis__37947);
                }
                else
                {
                    object v_local_1__38403 = false;
                    if (R.Bool(R.eq(v_mode, "infer")))
                    {
                        object v_infer_confidence__38481 = F_dv_min(F_dv_premise_confidence(v_world, F_dv_get(v_law__37920, "premise")), F_dv_get(v_adoption__37871, "confidence"));
                        v_local_1__38403 = F_dv_infer_stage(v_world, F_dv_get(v_law__37920, "effects"), v_time, v_basis__37947, v_infer_confidence__38481, v_name, v_event_id);
                    }
                    else
                    {
                        v_local_1__38403 = F_dv_stage(v_world, F_dv_get(v_law__37920, "effects"), v_time, v_basis__37947, F_dv_get(v_adoption__37871, "confidence"));
                    }
                    object v_staged__38818 = v_local_1__38403;
                    if (R.Bool(R.not(R.eq(F_dv_get(v_staged__38818, "status"), "ready"))))
                    {
                        return v_staged__38818;
                    }
                    else
                    {
                        if (R.Bool(F_dv_has_key(v_law__37920, "postcondition")))
                        {
                            object v_post_truth__39029 = F_dv_truth(F_dv_get(v_staged__38818, "world"), F_dv_get(v_law__37920, "postcondition"));
                            if (R.Bool(R.not(R.eq(v_post_truth__39029, "true"))))
                            {
                                return F_dv_result(R.cat("postcondition-", v_post_truth__39029), v_world, new System.Collections.Generic.List<object>(0) { }, v_basis__37947);
                            }
                        }
                        if (R.Bool(R.eq(v_operation, "verify")))
                        {
                            object v_candidate__39308 = F_dv_with_many(F_dv_get(v_staged__38818, "world"), new System.Collections.Generic.List<object>(3) { F_dv_pair("domain", "verification"), F_dv_pair("source_domain", F_dv_get(v_world, "domain")), F_dv_pair("source_revision", v_expected_revision) });
                            object v_assessment__39472 = new System.Collections.Generic.List<object>(10) { F_dv_pair("world", F_dv_get(v_world, "id")), F_dv_pair("subject", F_dv_get(v_world, "subject")), F_dv_pair("revision", v_expected_revision), F_dv_pair("law", v_name), F_dv_pair("law_version", F_dv_get(v_law__37920, "version")), F_dv_pair("mode", v_mode), F_dv_pair("event", v_event_id), F_dv_pair("time", v_time), F_dv_pair("precision", "runtime-discrete"), F_dv_pair("scope", "input-snapshot") };
                            return F_dv_with_many(F_dv_result("verified", v_world, F_dv_clone(F_dv_get(v_staged__38818, "changes")), F_dv_clone(v_basis__37947)), new System.Collections.Generic.List<object>(2) { F_dv_pair("candidate", v_candidate__39308), F_dv_pair("assessment", v_assessment__39472) });
                        }
                        object v_next__39903 = F_dv_get(v_staged__38818, "world");
                        object v_revision__39947 = R.add(v_expected_revision, R.number("1"));
                        object v_event__40006 = F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("id", v_event_id), F_dv_pair("time", v_time), F_dv_pair("law", v_name), F_dv_pair("law_version", F_dv_get(v_law__37920, "version")) }, new System.Collections.Generic.List<object>(2) { F_dv_pair("changes", F_dv_get(v_staged__38818, "changes")), F_dv_pair("basis", v_basis__37947) });
                        _ = R.push(F_dv_get(v_next__39903, "seen"), F_dv_clone(v_event_id));
                        _ = R.push(F_dv_get(v_next__39903, "history"), v_event__40006);
                        v_next__39903 = F_dv_with_many(v_next__39903, new System.Collections.Generic.List<object>(2) { F_dv_pair("revision", v_revision__39947), F_dv_pair("time", v_time) });
                        return new System.Collections.Generic.List<object>(6) { F_dv_pair("status", "applied"), F_dv_pair("world", v_next__39903), F_dv_pair("changes", F_dv_clone(F_dv_get(v_staged__38818, "changes"))), F_dv_pair("basis", F_dv_clone(v_basis__37947)), F_dv_pair("event", v_event_id), F_dv_pair("time", v_time) };
                    }
                }
            }
        }
    }

    public static object F_dv_evaluate_law(object v_world, object v_name, object v_mode, object v_event_id, object v_time, object v_expected_revision, object v_operation)
    {
        object v_result__40751 = F_dv_evaluate_law_value(v_world, v_name, v_mode, v_event_id, v_time, v_expected_revision, v_operation);
        if (R.Bool((R.Bool(R.eq(v_operation, "run")) ? (object)(R.eq(v_mode, "predict")) : (object)(false))))
        {
            return F_dv_with_many(v_result__40751, new System.Collections.Generic.List<object>(2) { F_dv_pair("world", F_dv_with_many(F_dv_get(v_result__40751, "world"), new System.Collections.Generic.List<object>(2) { F_dv_pair("domain", "prediction"), F_dv_pair("source_revision", v_expected_revision) })), F_dv_pair("source_revision", v_expected_revision) });
        }
        return v_result__40751;
    }

    public static object F_dv_apply_law(object v_world, object v_name, object v_mode, object v_event_id, object v_time, object v_expected_revision)
    {
        return F_dv_evaluate_law(v_world, v_name, v_mode, v_event_id, v_time, v_expected_revision, "run");
    }

    public static object F_dv_verify(object v_world, object v_name, object v_mode, object v_event_id, object v_time, object v_expected_revision)
    {
        return F_dv_evaluate_law(v_world, v_name, v_mode, v_event_id, v_time, v_expected_revision, "verify");
    }

    public static object F_dv_evaluate(object v_world, object v_name, object v_request)
    {
        return F_dv_evaluate_law(v_world, v_name, F_dv_get(v_request, "mode"), F_dv_get(v_request, "event"), F_dv_get(v_request, "time"), F_dv_get(v_request, "revision"), F_dv_get(v_request, "operation"));
    }

    public static object F_dv_execute(object v_physical, object v_name, object v_event_id, object v_time, object v_expected_revision)
    {
        _ = F_dv_domain(v_physical, "physical");
        return F_dv_apply_law(v_physical, v_name, "enforce", v_event_id, v_time, v_expected_revision);
    }

    public static object F_dv_predict(object v_mind, object v_name, object v_event_id, object v_time, object v_expected_revision)
    {
        _ = F_dv_domain(v_mind, "mind");
        return F_dv_apply_law(v_mind, v_name, "predict", v_event_id, v_time, v_expected_revision);
    }

    public static object F_dv_infer(object v_mind, object v_name, object v_event_id, object v_time, object v_expected_revision)
    {
        _ = F_dv_domain(v_mind, "mind");
        return F_dv_apply_law(v_mind, v_name, "infer", v_event_id, v_time, v_expected_revision);
    }

    public static object F_dv_norm(object v_mind, object v_name)
    {
        _ = F_dv_domain(v_mind, "mind");
        object v_adoption__42286 = F_dv_get(F_dv_get(v_mind, "adoptions"), v_name);
        _ = F_dv_require(R.eq(F_dv_get(v_adoption__42286, "mode"), "norm"), "Not a normative adoption");
        if (R.Bool(F_dv_get(v_adoption__42286, "active")))
        {
            return F_dv_truth(v_mind, F_dv_get(F_dv_get(v_adoption__42286, "law"), "premise"));
        }
        else
        {
            return "unknown";
        }
    }
}
}
