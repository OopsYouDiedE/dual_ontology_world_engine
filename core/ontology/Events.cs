// @mechanism: dual_ontology_state
// @layer: core/ontology
// @contracts: Events

using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using System.Buffers;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;
using System.Threading;
using System;

// --- Source: Events.cs ---
namespace DuelView.Core
{


/// <summary>Session / Events: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_event(object v_events, object v_kind, object v_actor, object v_time, object v_parents, object v_data)
    {
        object v_id__892 = R.cat("e", R.show(R.count(v_events)));
        object v_frozen_event__944 = R.snapshot(new System.Collections.Generic.List<object>(6) { F_dv_pair("id", v_id__892), F_dv_pair("type", v_kind), F_dv_pair("actor", v_actor), F_dv_pair("time", v_time), F_dv_pair("parents", v_parents), F_dv_pair("data", F_dvs_event_storage(v_kind, v_data)) });
        _ = R.raw_event(v_frozen_event__944);
        _ = R.push(v_events, v_frozen_event__944);
        return v_id__892;
    }

    public static object F_dvs_event_storage(object v_kind, object v_data)
    {
        if (R.Bool(R.audit_mode()))
        {
            return v_data;
        }
        object v_keep__1415 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.eq(v_kind, "inference")))
        {
            v_keep__1415 = new System.Collections.Generic.List<object>(2) { "mind_before", "mind_after" };
        }
        if (R.Bool((R.Bool(R.eq(v_kind, "observation")) ? (object)(true) : (object)(R.eq(v_kind, "listener_parse")))))
        {
            v_keep__1415 = new System.Collections.Generic.List<object>(1) { "mind_after" };
        }
        if (R.Bool(R.eq(v_kind, "factor_prune")))
        {
            v_keep__1415 = new System.Collections.Generic.List<object>(1) { "actor_before" };
        }
        object v_result__1657 = new System.Collections.Generic.List<object>(0) { };
        object v_index__1677 = R.number("0");
        while (R.Bool(R.lt(v_index__1677, R.count(v_data))))
        {
            object v_entry__1733 = R.at(v_data, v_index__1677);
            if (R.Bool((R.Bool(R.not(F_dv_has(new System.Collections.Generic.List<object>(6) { "mind_before", "mind_after", "physical_before", "physical_after", "actor_before", "actor_after" }, R.at(v_entry__1733, R.number("0"))))) ? (object)(true) : (object)(F_dv_has(v_keep__1415, R.at(v_entry__1733, R.number("0")))))))
            {
                if (R.Bool((R.Bool(R.eq(v_kind, "action_execution")) ? (object)(R.eq(R.at(v_entry__1733, R.number("0")), "receipt")) : (object)(false))))
                {
                    object v_execution__2239 = F_dv_get(R.at(v_entry__1733, R.number("1")), "execution");
                    object v_world__2290 = F_dv_get(v_execution__2239, "world");
                    object v_reference__2334 = R.raw_ref(v_world__2290);
                    if (R.Bool(R.not(R.eq(v_reference__2334, false))))
                    {
                        object v_cut__2595 = new System.Collections.Generic.List<object>(5) { F_dv_pair("world", F_dv_get(v_world__2290, "id")), F_dv_pair("revision", F_dv_get(v_world__2290, "revision")), F_dv_pair("time", F_dv_find(v_world__2290, "time", R.number("0"))), F_dv_pair("raw_ref", v_reference__2334), F_dv_pair("storage", "raw-dag-world-reference") };
                        object v_compact__2768 = F_dv_with(F_dv_without(v_execution__2239, "world"), "world_cut", v_cut__2595);
                        _ = R.push(v_result__1657, F_dv_pair("receipt", F_dv_with(R.at(v_entry__1733, R.number("1")), "execution", v_compact__2768)));
                    }
                    else
                    {
                        _ = R.push(v_result__1657, v_entry__1733);
                    }
                }
                else
                {
                    _ = R.push(v_result__1657, v_entry__1733);
                }
            }
            v_index__1677 = R.add(v_index__1677, R.number("1"));
        }
        return v_result__1657;
    }

    public static object F_dvs_find_event_by_id(object v_events, object v_id)
    {
        v_events = R.snapshot(v_events);
        if (R.Bool((R.Bool(R.lt(R.number("1"), R.length(v_id))) ? (object)(R.eq(R.chr(v_id, R.number("0")), "e")) : (object)(false))))
        {
            object v_num_str__3235 = R.slice(v_id, R.number("1"), R.sub(R.length(v_id), R.number("1")));
            if (R.Bool(R.is_number(v_num_str__3235)))
            {
                object v_idx__3330 = R.number(v_num_str__3235);
                if (R.Bool((R.Bool(R.not(R.lt(v_idx__3330, R.number("0")))) ? (object)(R.lt(v_idx__3330, R.count(v_events))) : (object)(false))))
                {
                    object v_candidate__3429 = R.at(v_events, v_idx__3330);
                    if (R.Bool(R.eq(F_dv_get(v_candidate__3429, "id"), v_id)))
                    {
                        return v_candidate__3429;
                    }
                }
            }
        }
        object v_i__3539 = R.sub(R.count(v_events), R.number("1"));
        while (R.Bool(R.not(R.lt(v_i__3539, R.number("0")))))
        {
            object v_event__3596 = R.at(v_events, v_i__3539);
            if (R.Bool(R.eq(F_dv_get(v_event__3596, "id"), v_id)))
            {
                return v_event__3596;
            }
            v_i__3539 = R.sub(v_i__3539, R.number("1"));
        }
        return false;
    }

    public static object F_dvs_cognitive_parents(object v_actor, object v_parents)
    {
        object v_previous__3754 = F_dv_find(v_actor, "last_cognitive_event", "");
        if (R.Bool(R.eq(v_previous__3754, "")))
        {
            return v_parents;
        }
        else
        {
            return F_dv_explanation_merge(v_parents, F_dv_single(v_previous__3754));
        }
    }

    public static object F_dvs_owned_events_stable(object v_events, object v_actor, object v_cursor)
    {
        v_events = R.snapshot(v_events);
        object v_i__27658 = v_cursor;
        while (R.Bool(R.lt(v_i__27658, R.count(v_events))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(v_events, v_i__27658), "actor"), F_dv_get(v_actor, "id"))))
            {
                if (R.Bool(R.not(F_dv_has(new System.Collections.Generic.List<object>(4) { "planning_stop", "planning_stop_reused", "planning_yield", "action_wait" }, F_dv_get(R.at(v_events, v_i__27658), "type")))))
                {
                    return false;
                }
            }
            v_i__27658 = R.add(v_i__27658, R.number("1"));
        }
        return true;
    }
}
}

// --- Source: Imprints.cs ---
namespace DuelView.Core
{


/// <summary>Cognition / Imprints: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dv_relax(object v_current, object v_baseline, object v_retention)
    {
        return R.add(v_baseline, R.mul(R.sub(v_current, v_baseline), F_dv_unit(v_retention)));
    }

    public static object F_dv_affect_proposal(object v_mind, object v_request, object v_policy)
    {
        object v_appraisal__75869 = F_dv_appraise_request(v_mind, v_request);
        object v_features__75924 = F_dv_get(v_appraisal__75869, "features");
        object v_self__75962 = F_dv_get(v_request, "self");
        object v_valence_ref__75990 = F_dv_ref(v_self__75962, "affect", "valence");
        object v_arousal_ref__76046 = F_dv_ref(v_self__75962, "affect", "arousal");
        object v_valence__76102 = F_dv_read(v_mind, v_valence_ref__75990);
        object v_arousal__76147 = F_dv_read(v_mind, v_arousal_ref__76046);
        _ = F_dv_require(F_dv_and(F_dv_get(v_valence__76102, "known"), F_dv_get(v_arousal__76147, "known")), "Unknown current affect");
        object v_v_delta__76271 = F_dv_dot(v_features__75924, F_dv_get(v_policy, "valence_weights"));
        object v_a_delta__76330 = F_dv_dot(v_features__75924, F_dv_get(v_policy, "arousal_weights"));
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("source_revision", F_dv_get(v_mind, "revision")), F_dv_pair("basis", v_appraisal__75869), F_dv_pair("effects", F_dv_pair(F_dv_assign(v_valence_ref__75990, F_dv_clamp_signed(R.add(F_dv_relax(F_dv_get(v_valence__76102, "value"), F_dv_get(v_policy, "baseline_valence"), F_dv_get(v_policy, "retention")), F_dv_get(v_v_delta__76271, "score")))), F_dv_assign(v_arousal_ref__76046, F_dv_clamp_signed(R.add(F_dv_relax(F_dv_get(v_arousal__76147, "value"), F_dv_get(v_policy, "baseline_arousal"), F_dv_get(v_policy, "retention")), F_dv_get(v_a_delta__76330, "score")))))) };
    }

    public static object F_dv_acceptance_classify(object v_degree, object v_levels)
    {
        _ = F_dv_unit(v_degree);
        _ = F_dv_require(R.lt(R.number("0"), R.count(v_levels)), "Acceptance requires levels");
        _ = F_dv_require(R.eq(R.at(R.at(v_levels, R.number("0")), R.number("1")), R.number("0")), "Acceptance levels must start at zero");
        object v_previous__77051 = R.sub(R.number("0"), R.number("1"));
        object v_selected__77073 = "";
        object v_i__77095 = R.number("0");
        while (R.Bool(R.lt(v_i__77095, R.count(v_levels))))
        {
            object v_threshold__77145 = F_dv_unit(R.at(R.at(v_levels, v_i__77095), R.number("1")));
            _ = F_dv_require(R.lt(v_previous__77051, v_threshold__77145), "Acceptance thresholds must increase");
            if (R.Bool(R.not(R.lt(v_degree, v_threshold__77145))))
            {
                v_selected__77073 = R.at(R.at(v_levels, v_i__77095), R.number("0"));
            }
            v_previous__77051 = v_threshold__77145;
            v_i__77095 = R.add(v_i__77095, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("degree", v_degree), F_dv_pair("level", v_selected__77073) };
    }

    public static object F_dv_threshold_acceptance(object v_value, object v_threshold)
    {
        return F_dv_acceptance_classify(v_value, new System.Collections.Generic.List<object>(2) { new System.Collections.Generic.List<object>(2) { "held", R.number("0") }, new System.Collections.Generic.List<object>(2) { "accepted", v_threshold } });
    }

    public static object F_dv_acceptance_transition(object v_previous, object v_current, object v_source)
    {
        object v_transition__77639 = "unchanged";
        if (R.Bool(R.not(R.eq(F_dv_get(v_previous, "level"), F_dv_get(v_current, "level")))))
        {
            v_transition__77639 = "changed";
            if (R.Bool((R.Bool(R.eq(F_dv_get(v_previous, "level"), "accepted")) ? (object)(R.not(R.eq(F_dv_get(v_current, "level"), "accepted"))) : (object)(false))))
            {
                v_transition__77639 = "withdrawn";
            }
        }
        return new System.Collections.Generic.List<object>(4) { F_dv_pair("before", v_previous), F_dv_pair("after", v_current), F_dv_pair("transition", v_transition__77639), F_dv_pair("source", v_source) };
    }

    public static object F_dv_balance_dot(object v_a, object v_b)
    {
        _ = F_dv_require(R.eq(R.count(v_a), R.count(v_b)), "Balance dimensions differ");
        object v_result__78198 = R.exact("0");
        object v_i__78229 = R.number("0");
        while (R.Bool(R.lt(v_i__78229, R.count(v_a))))
        {
            v_result__78198 = R.add(v_result__78198, R.mul(R.exact(R.at(v_a, v_i__78229)), R.exact(R.at(v_b, v_i__78229))));
            v_i__78229 = R.add(v_i__78229, R.number("1"));
        }
        return v_result__78198;
    }

    public static object F_dv_balance_extent(object v_variables)
    {
        object v_total__78398 = R.number("1");
        object v_i__78416 = R.number("0");
        while (R.Bool(R.lt(v_i__78416, R.count(v_variables))))
        {
            object v_count__78469 = R.count(F_dv_get(R.at(v_variables, v_i__78416), "options"));
            _ = F_dv_require(R.lt(R.number("0"), v_count__78469), "Balance variable has an empty domain");
            v_total__78398 = R.mul(v_total__78398, v_count__78469);
            _ = F_dv_require(R.not(R.lt(R.number("9007199254740991"), v_total__78398)), "Finite domain index exceeds exact integer range");
            v_i__78416 = R.add(v_i__78416, R.number("1"));
        }
        return v_total__78398;
    }

    public static object F_dv_balance_candidate(object v_index, object v_goal, object v_variables, object v_laws, object v_limits, object v_prices)
    {
        object v_cursor__78820 = v_index;
        object v_values__78843 = new System.Collections.Generic.List<object>(0) { };
        object v_costs__78863 = new System.Collections.Generic.List<object>(0) { };
        object v_choices__78882 = new System.Collections.Generic.List<object>(0) { };
        object v_j__78903 = R.number("0");
        while (R.Bool(R.lt(v_j__78903, R.count(F_dv_get(v_limits, "balances")))))
        {
            _ = R.push(v_costs__78863, R.exact("0"));
            _ = F_dv_require(R.not(R.lt(R.exact(R.at(F_dv_get(v_limits, "balances"), v_j__78903)), R.number("0"))), "Negative resource balance");
            _ = F_dv_require(R.not(R.lt(R.exact(R.at(v_prices, v_j__78903)), R.number("0"))), "Negative resource price");
            v_j__78903 = R.add(v_j__78903, R.number("1"));
        }
        object v_i__79169 = R.number("0");
        while (R.Bool(R.lt(v_i__79169, R.count(v_variables))))
        {
            object v_count__79222 = R.count(F_dv_get(R.at(v_variables, v_i__79169), "options"));
            object v_quotient__79273 = R.floor(R.div(v_cursor__78820, v_count__79222));
            object v_choice__79321 = R.sub(v_cursor__78820, R.mul(v_quotient__79273, v_count__79222));
            v_cursor__78820 = v_quotient__79273;
            object v_option__79394 = R.at(F_dv_get(R.at(v_variables, v_i__79169), "options"), v_choice__79321);
            _ = R.push(v_choices__78882, v_choice__79321);
            _ = R.push(v_values__78843, R.exact(F_dv_get(v_option__79394, "value")));
            _ = F_dv_require(R.eq(R.count(F_dv_get(v_option__79394, "cost")), R.count(v_costs__78863)), "Resource cost dimensions differ");
            v_j__78903 = R.number("0");
            while (R.Bool(R.lt(v_j__78903, R.count(v_costs__78863))))
            {
                object v_charge__79674 = R.exact(R.at(F_dv_get(v_option__79394, "cost"), v_j__78903));
                _ = F_dv_require(R.not(R.lt(v_charge__79674, R.number("0"))), "Negative imprint charge");
                _ = R.replace(v_costs__78863, v_j__78903, R.add(R.at(v_costs__78863, v_j__78903), v_charge__79674));
                v_j__78903 = R.add(v_j__78903, R.number("1"));
            }
            v_i__79169 = R.add(v_i__79169, R.number("1"));
        }
        object v_degree__79871 = F_dv_balance_dot(F_dv_get(v_goal, "coefficients"), v_values__78843);
        object v_feasible__79930 = (R.Bool((R.Bool(R.not(R.lt(v_degree__79871, R.exact(F_dv_get(v_goal, "minimum"))))) ? (object)(R.not(R.lt(v_degree__79871, R.number("0")))) : (object)(false))) ? (object)(R.not(R.lt(R.number("1"), v_degree__79871))) : (object)(false));
        v_i__79169 = R.number("0");
        while (R.Bool(R.lt(v_i__79169, R.count(v_laws))))
        {
            if (R.Bool(R.not(R.eq(F_dv_balance_dot(F_dv_get(R.at(v_laws, v_i__79169), "coefficients"), v_values__78843), R.exact(F_dv_get(R.at(v_laws, v_i__79169), "total"))))))
            {
                v_feasible__79930 = false;
            }
            v_i__79169 = R.add(v_i__79169, R.number("1"));
        }
        v_j__78903 = R.number("0");
        while (R.Bool(R.lt(v_j__78903, R.count(v_costs__78863))))
        {
            if (R.Bool(R.lt(R.exact(R.at(F_dv_get(v_limits, "balances"), v_j__78903)), R.at(v_costs__78863, v_j__78903))))
            {
                v_feasible__79930 = false;
            }
            v_j__78903 = R.add(v_j__78903, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(7) { F_dv_pair("index", v_index), F_dv_pair("choices", v_choices__78882), F_dv_pair("values", v_values__78843), F_dv_pair("degree", v_degree__79871), F_dv_pair("charges", v_costs__78863), F_dv_pair("cost", F_dv_balance_dot(v_costs__78863, v_prices)), F_dv_pair("feasible", v_feasible__79930) };
    }

    public static object F_dv_balance_search_limit(object v_total, object v_limits)
    {
        _ = F_dv_require((R.Bool(R.not(R.lt(F_dv_get(v_limits, "search_budget"), R.number("0")))) ? (object)(R.eq(R.floor(F_dv_get(v_limits, "search_budget")), F_dv_get(v_limits, "search_budget"))) : (object)(false)), "Invalid search budget");
        _ = F_dv_require(R.not(R.lt(F_dv_get(v_limits, "work_remaining"), R.number("0"))), "Negative imprint work budget");
        object v_covered__80894 = F_dv_min(v_total, F_dv_get(v_limits, "search_budget"));
        if (R.Bool(R.lt(F_dv_get(v_limits, "work_remaining"), R.mul(v_covered__80894, R.number("2")))))
        {
            object v_lower__81159 = R.number("0");
            object v_upper__81181 = v_covered__80894;
            while (R.Bool(R.lt(v_lower__81159, v_upper__81181)))
            {
                object v_middle__81242 = R.add(v_lower__81159, R.floor(R.div(R.add(R.sub(v_upper__81181, v_lower__81159), R.number("1")), R.number("2"))));
                if (R.Bool(R.not(R.lt(F_dv_get(v_limits, "work_remaining"), R.mul(v_middle__81242, R.number("2"))))))
                {
                    v_lower__81159 = v_middle__81242;
                }
                else
                {
                    v_upper__81181 = R.sub(v_middle__81242, R.number("1"));
                }
            }
            v_covered__80894 = v_lower__81159;
        }
        return v_covered__80894;
    }

    public static object F_dv_balance_stop_reason(object v_visited, object v_total, object v_limits)
    {
        if (R.Bool(R.eq(v_visited, v_total)))
        {
            return "domain-covered";
        }
        object v_search_stop__81589 = R.not(R.lt(v_visited, F_dv_get(v_limits, "search_budget")));
        object v_work_stop__81643 = R.lt(F_dv_get(v_limits, "work_remaining"), R.mul(R.add(v_visited, R.number("1")), R.number("2")));
        if (R.Bool((R.Bool(v_search_stop__81589) ? (object)(v_work_stop__81643) : (object)(false))))
        {
            return "search-and-work-budget";
        }
        if (R.Bool(v_search_stop__81589))
        {
            return "search-budget";
        }
        if (R.Bool(v_work_stop__81643))
        {
            return "work-budget";
        }
        return "prefix-stop";
    }

    public static object F_dv_imprint_analyze(object v_goal, object v_variables, object v_laws, object v_limits, object v_prices)
    {
        _ = F_dv_require(R.eq(R.count(v_prices), R.count(F_dv_get(v_limits, "balances"))), "Resource price dimensions differ");
        object v_total__82234 = F_dv_balance_extent(v_variables);
        _ = F_dv_require(R.eq(R.count(F_dv_get(v_goal, "coefficients")), R.count(v_variables)), "Balance dimensions differ");
        object v_i__82375 = R.number("0");
        while (R.Bool(R.lt(v_i__82375, R.count(v_laws))))
        {
            _ = F_dv_require(R.eq(R.count(F_dv_get(R.at(v_laws, v_i__82375), "coefficients")), R.count(v_variables)), "Balance dimensions differ");
            v_i__82375 = R.add(v_i__82375, R.number("1"));
        }
        object v_option_count__82540 = R.number("0");
        v_i__82375 = R.number("0");
        while (R.Bool(R.lt(v_i__82375, R.count(v_variables))))
        {
            v_option_count__82540 = R.add(v_option_count__82540, R.count(F_dv_get(R.at(v_variables, v_i__82375), "options")));
            v_i__82375 = R.add(v_i__82375, R.number("1"));
        }
        object v_candidate_limit__82697 = F_dv_balance_search_limit(v_total__82234, v_limits);
        object v_branch_applicable__82762 = R.lt(R.number("0"), R.count(v_variables));
        object v_recommended__82814 = "enumerate";
        object v_reason__82848 = "reference-path-has-lower-setup-cost";
        if (R.Bool((R.Bool(v_branch_applicable__82762) ? (object)(R.lt(R.mul(v_option_count__82540, R.number("2")), v_candidate_limit__82697)) : (object)(false))))
        {
            v_recommended__82814 = "branch_bound";
            v_reason__82848 = "cartesian-prefix-exceeds-linear-bound-preparation";
        }
        if (R.Bool(R.not(v_branch_applicable__82762)))
        {
            v_reason__82848 = "zero-dimensional-reference-domain";
        }
        return new System.Collections.Generic.List<object>(16) { F_dv_pair("family", "finite-linear-cartesian"), F_dv_pair("arithmetic", "exact-rational"), F_dv_pair("branch_bound_applicable", v_branch_applicable__82762), F_dv_pair("recommended", v_recommended__82814), F_dv_pair("selection_reason", v_reason__82848), F_dv_pair("total", v_total__82234), F_dv_pair("candidate_limit", v_candidate_limit__82697), F_dv_pair("option_count", v_option_count__82540), F_dv_pair("budget_covers_domain", R.eq(v_candidate_limit__82697, v_total__82234)), F_dv_pair("stop_reason", F_dv_balance_stop_reason(v_candidate_limit__82697, v_total__82234, v_limits)), F_dv_pair("budget_unit", "covered-candidate-prefix"), F_dv_pair("work_per_covered_candidate", R.number("2")), F_dv_pair("bound_scope", "separable-linear-intervals-may-be-loose"), F_dv_pair("requirements", "finite-nonempty-options; matching-linear-dimensions; exact-numeric-values; nonnegative-charges-and-prices"), F_dv_pair("validation", "shape-and-budget; selected-solver-validates-numeric-options"), F_dv_pair("time_guarantee", "none") };
    }

    public static object F_dv_balance_finish(object v_best, object v_visited, object v_total, object v_limits, object v_algorithm, object v_evaluated, object v_bound_checks, object v_preprocessed, object v_coverage, object v_analysis)
    {
        object v_status__83996 = "Unresolved";
        if (R.Bool(R.not(R.eq(v_best, false))))
        {
            v_status__83996 = "Feasible";
            if (R.Bool(R.eq(v_visited, v_total)))
            {
                v_status__83996 = "Optimal";
            }
        }
        else
        {
            if (R.Bool(R.eq(v_visited, v_total)))
            {
                v_status__83996 = "Infeasible";
            }
        }
        object v_proof_kind__84209 = "enumerated-prefix-v1";
        if (R.Bool(R.eq(v_algorithm, "branch_bound")))
        {
            v_proof_kind__84209 = "linear-interval-prefix-v1";
        }
        return new System.Collections.Generic.List<object>(20) { F_dv_pair("status", v_status__83996), F_dv_pair("best", v_best), F_dv_pair("visited", v_visited), F_dv_pair("total", v_total), F_dv_pair("revision", F_dv_get(v_limits, "revision")), F_dv_pair("subject", F_dv_get(v_limits, "subject")), F_dv_pair("algorithm", v_algorithm), F_dv_pair("proof_kind", v_proof_kind__84209), F_dv_pair("coverage", v_coverage), F_dv_pair("arithmetic", "exact-rational"), F_dv_pair("complete", R.eq(v_visited, v_total)), F_dv_pair("stop_reason", F_dv_balance_stop_reason(v_visited, v_total, v_limits)), F_dv_pair("covered_candidates", v_visited), F_dv_pair("charged_work", R.mul(v_visited, R.number("2"))), F_dv_pair("candidate_evaluations", v_evaluated), F_dv_pair("pruned_candidates", R.sub(v_visited, v_evaluated)), F_dv_pair("bound_checks", v_bound_checks), F_dv_pair("preprocessed_options", v_preprocessed), F_dv_pair("counter_scope", "search-diagnostics-not-total-runtime-or-certificate-authority"), F_dv_pair("analysis", v_analysis) };
    }

    public static object F_dv_imprint_enumerate(object v_goal, object v_variables, object v_laws, object v_limits, object v_prices, object v_analysis)
    {
        object v_total__85106 = F_dv_get(v_analysis, "total");
        object v_visited__85137 = R.number("0");
        object v_best__85157 = false;
        while (R.Bool(R.lt(v_visited__85137, F_dv_get(v_analysis, "candidate_limit"))))
        {
            object v_candidate__85228 = F_dv_balance_candidate(v_visited__85137, v_goal, v_variables, v_laws, v_limits, v_prices);
            if (R.Bool(F_dv_get(v_candidate__85228, "feasible")))
            {
                if (R.Bool(R.eq(v_best__85157, false)))
                {
                    v_best__85157 = v_candidate__85228;
                }
                else
                {
                    if (R.Bool(R.lt(F_dv_get(v_candidate__85228, "cost"), F_dv_get(v_best__85157, "cost"))))
                    {
                        v_best__85157 = v_candidate__85228;
                    }
                }
            }
            v_visited__85137 = R.add(v_visited__85137, R.number("1"));
        }
        return F_dv_balance_finish(v_best__85157, v_visited__85137, v_total__85106, v_limits, "enumerate", v_visited__85137, R.number("0"), R.number("0"), new System.Collections.Generic.List<object>(0) { }, v_analysis);
    }

    public static object F_dv_balance_zeroes(object v_count)
    {
        object v_result__85674 = new System.Collections.Generic.List<object>(0) { };
        object v_i__85694 = R.number("0");
        while (R.Bool(R.lt(v_i__85694, v_count)))
        {
            _ = R.push(v_result__85674, R.exact("0"));
            v_i__85694 = R.add(v_i__85694, R.number("1"));
        }
        return v_result__85674;
    }

    public static object F_dv_balance_interval_model(object v_goal, object v_variables, object v_laws, object v_limits, object v_prices)
    {
        _ = F_dv_require(R.eq(R.count(v_prices), R.count(F_dv_get(v_limits, "balances"))), "Resource price dimensions differ");
        _ = F_dv_require(R.eq(R.count(F_dv_get(v_goal, "coefficients")), R.count(v_variables)), "Balance dimensions differ");
        object v_field_count__86232 = R.add(R.add(R.number("2"), R.count(v_laws)), R.count(v_prices));
        object v_exact_prices__86292 = new System.Collections.Generic.List<object>(0) { };
        object v_balances__86318 = new System.Collections.Generic.List<object>(0) { };
        object v_law_totals__86340 = new System.Collections.Generic.List<object>(0) { };
        object v_i__86364 = R.number("0");
        while (R.Bool(R.lt(v_i__86364, R.count(v_prices))))
        {
            object v_price__86414 = R.exact(R.at(v_prices, v_i__86364));
            object v_balance__86454 = R.exact(R.at(F_dv_get(v_limits, "balances"), v_i__86364));
            _ = F_dv_require(R.not(R.lt(v_price__86414, R.number("0"))), "Negative resource price");
            _ = F_dv_require(R.not(R.lt(v_balance__86454, R.number("0"))), "Negative resource balance");
            _ = R.push(v_exact_prices__86292, v_price__86414);
            _ = R.push(v_balances__86318, v_balance__86454);
            v_i__86364 = R.add(v_i__86364, R.number("1"));
        }
        v_i__86364 = R.number("0");
        while (R.Bool(R.lt(v_i__86364, R.count(v_laws))))
        {
            _ = F_dv_require(R.eq(R.count(F_dv_get(R.at(v_laws, v_i__86364), "coefficients")), R.count(v_variables)), "Balance dimensions differ");
            _ = R.push(v_law_totals__86340, R.exact(F_dv_get(R.at(v_laws, v_i__86364), "total")));
            v_i__86364 = R.add(v_i__86364, R.number("1"));
        }
        object v_domains__86925 = new System.Collections.Generic.List<object>(0) { };
        object v_widths__86946 = new System.Collections.Generic.List<object>(1) { R.number("1") };
        object v_lower__86967 = new System.Collections.Generic.List<object>(1) { F_dv_balance_zeroes(v_field_count__86232) };
        object v_upper__87016 = new System.Collections.Generic.List<object>(1) { F_dv_balance_zeroes(v_field_count__86232) };
        object v_option_count__87065 = R.number("0");
        v_i__86364 = R.number("0");
        while (R.Bool(R.lt(v_i__86364, R.count(v_variables))))
        {
            object v_domain__87139 = new System.Collections.Generic.List<object>(0) { };
            object v_minimum__87163 = new System.Collections.Generic.List<object>(0) { };
            object v_maximum__87188 = new System.Collections.Generic.List<object>(0) { };
            object v_choice__87213 = R.number("0");
            while (R.Bool(R.lt(v_choice__87213, R.count(F_dv_get(R.at(v_variables, v_i__86364), "options")))))
            {
                object v_option__87295 = R.at(F_dv_get(R.at(v_variables, v_i__86364), "options"), v_choice__87213);
                object v_value__87349 = R.exact(F_dv_get(v_option__87295, "value"));
                _ = F_dv_require(R.eq(R.count(F_dv_get(v_option__87295, "cost")), R.count(v_prices)), "Resource cost dimensions differ");
                object v_terms__87497 = new System.Collections.Generic.List<object>(1) { R.mul(R.exact(R.at(F_dv_get(v_goal, "coefficients"), v_i__86364)), v_value__87349) };
                object v_j__87562 = R.number("0");
                while (R.Bool(R.lt(v_j__87562, R.count(v_laws))))
                {
                    _ = R.push(v_terms__87497, R.mul(R.exact(R.at(F_dv_get(R.at(v_laws, v_j__87562), "coefficients"), v_i__86364)), v_value__87349));
                    v_j__87562 = R.add(v_j__87562, R.number("1"));
                }
                object v_cost__87722 = R.exact("0");
                v_j__87562 = R.number("0");
                while (R.Bool(R.lt(v_j__87562, R.count(v_prices))))
                {
                    object v_charge__87821 = R.exact(R.at(F_dv_get(v_option__87295, "cost"), v_j__87562));
                    _ = F_dv_require(R.not(R.lt(v_charge__87821, R.number("0"))), "Negative imprint charge");
                    _ = R.push(v_terms__87497, v_charge__87821);
                    v_cost__87722 = R.add(v_cost__87722, R.mul(v_charge__87821, R.at(v_exact_prices__86292, v_j__87562)));
                    v_j__87562 = R.add(v_j__87562, R.number("1"));
                }
                _ = R.push(v_terms__87497, v_cost__87722);
                _ = R.push(v_domain__87139, v_terms__87497);
                if (R.Bool(R.eq(v_choice__87213, R.number("0"))))
                {
                    v_minimum__87163 = F_dv_clone(v_terms__87497);
                    v_maximum__87188 = F_dv_clone(v_terms__87497);
                }
                else
                {
                    v_j__87562 = R.number("0");
                    while (R.Bool(R.lt(v_j__87562, v_field_count__86232)))
                    {
                        _ = R.replace(v_minimum__87163, v_j__87562, F_dv_min(R.at(v_minimum__87163, v_j__87562), R.at(v_terms__87497, v_j__87562)));
                        _ = R.replace(v_maximum__87188, v_j__87562, F_dv_max(R.at(v_maximum__87188, v_j__87562), R.at(v_terms__87497, v_j__87562)));
                        v_j__87562 = R.add(v_j__87562, R.number("1"));
                    }
                }
                v_choice__87213 = R.add(v_choice__87213, R.number("1"));
                v_option_count__87065 = R.add(v_option_count__87065, R.number("1"));
            }
            object v_next_lower__88565 = new System.Collections.Generic.List<object>(0) { };
            object v_next_upper__88593 = new System.Collections.Generic.List<object>(0) { };
            object v_field_index__88621 = R.number("0");
            while (R.Bool(R.lt(v_field_index__88621, v_field_count__86232)))
            {
                _ = R.push(v_next_lower__88565, R.add(R.at(R.at(v_lower__86967, v_i__86364), v_field_index__88621), R.at(v_minimum__87163, v_field_index__88621)));
                _ = R.push(v_next_upper__88593, R.add(R.at(R.at(v_upper__87016, v_i__86364), v_field_index__88621), R.at(v_maximum__87188, v_field_index__88621)));
                v_field_index__88621 = R.add(v_field_index__88621, R.number("1"));
            }
            _ = R.push(v_domains__86925, v_domain__87139);
            _ = R.push(v_widths__86946, R.mul(R.at(v_widths__86946, v_i__86364), R.count(v_domain__87139)));
            _ = R.push(v_lower__86967, v_next_lower__88565);
            _ = R.push(v_upper__87016, v_next_upper__88593);
            v_i__86364 = R.add(v_i__86364, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(9) { F_dv_pair("domains", v_domains__86925), F_dv_pair("widths", v_widths__86946), F_dv_pair("lower", v_lower__86967), F_dv_pair("upper", v_upper__87016), F_dv_pair("field_count", v_field_count__86232), F_dv_pair("goal_minimum", R.exact(F_dv_get(v_goal, "minimum"))), F_dv_pair("law_totals", v_law_totals__86340), F_dv_pair("balances", v_balances__86318), F_dv_pair("option_count", v_option_count__87065) };
    }

    public static object F_dv_balance_interval_kind(object v_model, object v_depth, object v_fixed, object v_best)
    {
        object v_lower__89326 = R.at(F_dv_get(v_model, "lower"), v_depth);
        object v_upper__89361 = R.at(F_dv_get(v_model, "upper"), v_depth);
        object v_goal_lower__89396 = R.add(R.at(v_fixed, R.number("0")), R.at(v_lower__89326, R.number("0")));
        object v_goal_upper__89437 = R.add(R.at(v_fixed, R.number("0")), R.at(v_upper__89361, R.number("0")));
        if (R.Bool((R.Bool((R.Bool(R.lt(v_goal_upper__89437, F_dv_get(v_model, "goal_minimum"))) ? (object)(true) : (object)(R.lt(v_goal_upper__89437, R.number("0"))))) ? (object)(true) : (object)(R.lt(R.number("1"), v_goal_lower__89396)))))
        {
            return "goal-bound";
        }
        object v_j__89582 = R.number("0");
        while (R.Bool(R.lt(v_j__89582, R.count(F_dv_get(v_model, "law_totals")))))
        {
            object v_field__89642 = R.add(R.number("1"), v_j__89582);
            if (R.Bool((R.Bool(R.lt(R.at(F_dv_get(v_model, "law_totals"), v_j__89582), R.add(R.at(v_fixed, v_field__89642), R.at(v_lower__89326, v_field__89642)))) ? (object)(true) : (object)(R.lt(R.add(R.at(v_fixed, v_field__89642), R.at(v_upper__89361, v_field__89642)), R.at(F_dv_get(v_model, "law_totals"), v_j__89582))))))
            {
                return "law-bound";
            }
            v_j__89582 = R.add(v_j__89582, R.number("1"));
        }
        v_j__89582 = R.number("0");
        while (R.Bool(R.lt(v_j__89582, R.count(F_dv_get(v_model, "balances")))))
        {
            object v_field__89882 = R.add(R.add(R.number("1"), R.count(F_dv_get(v_model, "law_totals"))), v_j__89582);
            if (R.Bool(R.lt(R.at(F_dv_get(v_model, "balances"), v_j__89582), R.add(R.at(v_fixed, v_field__89882), R.at(v_lower__89326, v_field__89882)))))
            {
                return "resource-bound";
            }
            v_j__89582 = R.add(v_j__89582, R.number("1"));
        }
        if (R.Bool(R.not(R.eq(v_best, false))))
        {
            object v_field__90073 = R.sub(F_dv_get(v_model, "field_count"), R.number("1"));
            if (R.Bool(R.not(R.lt(R.add(R.at(v_fixed, v_field__90073), R.at(v_lower__89326, v_field__90073)), F_dv_get(v_best, "cost")))))
            {
                return "cost-bound";
            }
        }
        return "";
    }

    public static object F_dv_balance_interval_choices(object v_model, object v_index)
    {
        object v_choices__90257 = new System.Collections.Generic.List<object>(0) { };
        object v_cursor__90278 = v_index;
        object v_i__90301 = R.number("0");
        while (R.Bool(R.lt(v_i__90301, R.count(F_dv_get(v_model, "domains")))))
        {
            object v_count__90358 = R.count(R.at(F_dv_get(v_model, "domains"), v_i__90301));
            object v_quotient__90405 = R.floor(R.div(v_cursor__90278, v_count__90358));
            _ = R.push(v_choices__90257, R.sub(v_cursor__90278, R.mul(v_quotient__90405, v_count__90358)));
            v_cursor__90278 = v_quotient__90405;
            v_i__90301 = R.add(v_i__90301, R.number("1"));
        }
        return v_choices__90257;
    }

    public static object F_dv_balance_interval_probe(object v_model, object v_start, object v_best)
    {
        object v_choices__90699 = F_dv_balance_interval_choices(v_model, v_start);
        object v_fixed__90759 = F_dv_balance_zeroes(F_dv_get(v_model, "field_count"));
        object v_depth__90812 = R.count(F_dv_get(v_model, "domains"));
        object v_checks__90852 = R.number("0");
        while (R.Bool(R.not(R.lt(v_depth__90812, R.number("0")))))
        {
            object v_width__90897 = R.at(F_dv_get(v_model, "widths"), v_depth__90812);
            if (R.Bool(R.eq(R.mul(R.floor(R.div(v_start, v_width__90897)), v_width__90897), v_start)))
            {
                v_checks__90852 = R.add(v_checks__90852, R.number("1"));
                object v_kind__91026 = F_dv_balance_interval_kind(v_model, v_depth__90812, v_fixed__90759, v_best);
                if (R.Bool(R.not(R.eq(v_kind__91026, ""))))
                {
                    return new System.Collections.Generic.List<object>(3) { F_dv_pair("depth", v_depth__90812), F_dv_pair("kind", v_kind__91026), F_dv_pair("bound_checks", v_checks__90852) };
                }
            }
            if (R.Bool(R.lt(R.number("0"), v_depth__90812)))
            {
                object v_terms__91222 = R.at(R.at(F_dv_get(v_model, "domains"), R.sub(v_depth__90812, R.number("1"))), R.at(v_choices__90699, R.sub(v_depth__90812, R.number("1"))));
                object v_j__91291 = R.number("0");
                while (R.Bool(R.lt(v_j__91291, F_dv_get(v_model, "field_count"))))
                {
                    _ = R.replace(v_fixed__90759, v_j__91291, R.add(R.at(v_fixed__90759, v_j__91291), R.at(v_terms__91222, v_j__91291)));
                    v_j__91291 = R.add(v_j__91291, R.number("1"));
                }
            }
            v_depth__90812 = R.sub(v_depth__90812, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("depth", R.number("0")), F_dv_pair("kind", "candidate"), F_dv_pair("bound_checks", v_checks__90852) };
    }

    public static object F_dv_imprint_branch_bound(object v_goal, object v_variables, object v_laws, object v_limits, object v_prices, object v_analysis)
    {
        if (R.Bool(R.eq(F_dv_get(v_analysis, "candidate_limit"), R.number("0"))))
        {
            return F_dv_balance_finish(false, R.number("0"), F_dv_get(v_analysis, "total"), v_limits, "branch_bound", R.number("0"), R.number("0"), R.number("0"), new System.Collections.Generic.List<object>(0) { }, v_analysis);
        }
        object v_model__91741 = F_dv_balance_interval_model(v_goal, v_variables, v_laws, v_limits, v_prices);
        object v_visited__91822 = R.number("0");
        object v_evaluated__91842 = R.number("0");
        object v_bound_checks__91864 = R.number("0");
        object v_coverage__91889 = new System.Collections.Generic.List<object>(0) { };
        object v_best__91911 = false;
        while (R.Bool(R.lt(v_visited__91822, F_dv_get(v_analysis, "candidate_limit"))))
        {
            object v_probe__91982 = F_dv_balance_interval_probe(v_model__91741, v_visited__91822, v_best__91911);
            object v_count__92050 = F_dv_min(R.at(F_dv_get(v_model__91741, "widths"), F_dv_get(v_probe__91982, "depth")), R.sub(F_dv_get(v_analysis, "candidate_limit"), v_visited__91822));
            v_bound_checks__91864 = R.add(v_bound_checks__91864, F_dv_get(v_probe__91982, "bound_checks"));
            if (R.Bool(R.eq(F_dv_get(v_probe__91982, "kind"), "candidate")))
            {
                object v_candidate__92239 = F_dv_balance_candidate(v_visited__91822, v_goal, v_variables, v_laws, v_limits, v_prices);
                v_evaluated__91842 = R.add(v_evaluated__91842, R.number("1"));
                if (R.Bool(F_dv_get(v_candidate__92239, "feasible")))
                {
                    if (R.Bool(R.eq(v_best__91911, false)))
                    {
                        v_best__91911 = v_candidate__92239;
                    }
                    else
                    {
                        if (R.Bool(R.lt(F_dv_get(v_candidate__92239, "cost"), F_dv_get(v_best__91911, "cost"))))
                        {
                            v_best__91911 = v_candidate__92239;
                        }
                    }
                }
            }
            _ = R.push(v_coverage__91889, new System.Collections.Generic.List<object>(4) { F_dv_pair("start", v_visited__91822), F_dv_pair("depth", F_dv_get(v_probe__91982, "depth")), F_dv_pair("count", v_count__92050), F_dv_pair("kind", F_dv_get(v_probe__91982, "kind")) });
            v_visited__91822 = R.add(v_visited__91822, v_count__92050);
        }
        return F_dv_balance_finish(v_best__91911, v_visited__91822, F_dv_get(v_analysis, "total"), v_limits, "branch_bound", v_evaluated__91842, v_bound_checks__91864, F_dv_get(v_model__91741, "option_count"), v_coverage__91889, v_analysis);
    }

    public static object F_dv_imprint_solve_with(object v_capability, object v_goal, object v_variables, object v_laws, object v_limits, object v_prices, object v_algorithm)
    {
        _ = F_dv_require(v_capability, "Imprint capability is required");
        _ = F_dv_require((R.Bool((R.Bool(R.eq(v_algorithm, "auto")) ? (object)(true) : (object)(R.eq(v_algorithm, "enumerate")))) ? (object)(true) : (object)(R.eq(v_algorithm, "branch_bound"))), "Unknown imprint solver");
        object v_base_analysis__93222 = F_dv_imprint_analyze(v_goal, v_variables, v_laws, v_limits, v_prices);
        object v_selected__93304 = v_algorithm;
        if (R.Bool(R.eq(v_selected__93304, "auto")))
        {
            v_selected__93304 = F_dv_get(v_base_analysis__93222, "recommended");
        }
        object v_fallback_reason__93405 = "";
        if (R.Bool((R.Bool(R.eq(v_selected__93304, "branch_bound")) ? (object)(R.not(F_dv_get(v_base_analysis__93222, "branch_bound_applicable"))) : (object)(false))))
        {
            v_selected__93304 = "enumerate";
            v_fallback_reason__93405 = "unsupported-bound-model-reference-fallback";
        }
        object v_analysis__93617 = F_dv_with_many(v_base_analysis__93222, new System.Collections.Generic.List<object>(3) { F_dv_pair("requested_algorithm", v_algorithm), F_dv_pair("selected_algorithm", v_selected__93304), F_dv_pair("fallback_reason", v_fallback_reason__93405) });
        if (R.Bool(R.eq(v_selected__93304, "branch_bound")))
        {
            return F_dv_imprint_branch_bound(v_goal, v_variables, v_laws, v_limits, v_prices, v_analysis__93617);
        }
        return F_dv_imprint_enumerate(v_goal, v_variables, v_laws, v_limits, v_prices, v_analysis__93617);
    }

    public static object F_dv_imprint_solve(object v_capability, object v_goal, object v_variables, object v_laws, object v_limits, object v_prices)
    {
        return F_dv_imprint_solve_with(v_capability, v_goal, v_variables, v_laws, v_limits, v_prices, "auto");
    }

    public static object F_dv_balance_same_numbers(object v_left, object v_right)
    {
        if (R.Bool(R.not(R.eq(R.count(v_left), R.count(v_right)))))
        {
            return false;
        }
        object v_i__94325 = R.number("0");
        while (R.Bool(R.lt(v_i__94325, R.count(v_left))))
        {
            if (R.Bool(R.not(R.eq(R.exact(R.at(v_left, v_i__94325)), R.exact(R.at(v_right, v_i__94325))))))
            {
                return false;
            }
            v_i__94325 = R.add(v_i__94325, R.number("1"));
        }
        return true;
    }

    public static object F_dv_balance_same_witness(object v_left, object v_right)
    {
        return (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_left, "index"), F_dv_get(v_right, "index"))) ? (object)(R.eq(F_dv_get(v_left, "feasible"), F_dv_get(v_right, "feasible"))) : (object)(false))) ? (object)(F_dv_same(F_dv_get(v_left, "choices"), F_dv_get(v_right, "choices"))) : (object)(false))) ? (object)(F_dv_balance_same_numbers(F_dv_get(v_left, "values"), F_dv_get(v_right, "values"))) : (object)(false))) ? (object)(F_dv_balance_same_numbers(F_dv_get(v_left, "charges"), F_dv_get(v_right, "charges"))) : (object)(false))) ? (object)(R.eq(R.exact(F_dv_get(v_left, "degree")), F_dv_get(v_right, "degree"))) : (object)(false))) ? (object)(R.eq(R.exact(F_dv_get(v_left, "cost")), F_dv_get(v_right, "cost"))) : (object)(false));
    }

    public static object F_dv_balance_interval_fixed(object v_model, object v_start, object v_depth)
    {
        object v_choices__94967 = F_dv_balance_interval_choices(v_model, v_start);
        object v_fixed__95027 = F_dv_balance_zeroes(F_dv_get(v_model, "field_count"));
        object v_i__95080 = v_depth;
        while (R.Bool(R.lt(v_i__95080, R.count(F_dv_get(v_model, "domains")))))
        {
            object v_terms__95141 = R.at(R.at(F_dv_get(v_model, "domains"), v_i__95080), R.at(v_choices__94967, v_i__95080));
            object v_j__95190 = R.number("0");
            while (R.Bool(R.lt(v_j__95190, F_dv_get(v_model, "field_count"))))
            {
                _ = R.replace(v_fixed__95027, v_j__95190, R.add(R.at(v_fixed__95027, v_j__95190), R.at(v_terms__95141, v_j__95190)));
                v_j__95190 = R.add(v_j__95190, R.number("1"));
            }
            v_i__95080 = R.add(v_i__95080, R.number("1"));
        }
        return v_fixed__95027;
    }

    public static object F_dv_balance_check_coverage(object v_result, object v_goal, object v_variables, object v_laws, object v_limits, object v_prices, object v_verified)
    {
        object v_coverage__95440 = F_dv_find(v_result, "coverage", false);
        if (R.Bool(R.not(R.is_list(v_coverage__95440))))
        {
            return false;
        }
        if (R.Bool(R.eq(F_dv_get(v_result, "visited"), R.number("0"))))
        {
            return R.eq(R.count(v_coverage__95440), R.number("0"));
        }
        object v_model__95615 = F_dv_balance_interval_model(v_goal, v_variables, v_laws, v_limits, v_prices);
        object v_cursor__95696 = R.number("0");
        object v_i__95715 = R.number("0");
        while (R.Bool(R.lt(v_i__95715, R.count(v_coverage__95440))))
        {
            object v_block__95767 = R.at(v_coverage__95440, v_i__95715);
            if (R.Bool(R.not(F_dv_record_like(v_block__95767))))
            {
                return false;
            }
            object v_start__95862 = F_dv_find(v_block__95767, "start", R.sub(R.number("0"), R.number("1")));
            object v_depth__95910 = F_dv_find(v_block__95767, "depth", R.sub(R.number("0"), R.number("1")));
            object v_count__95958 = F_dv_find(v_block__95767, "count", R.sub(R.number("0"), R.number("1")));
            object v_kind__96006 = F_dv_find(v_block__95767, "kind", "");
            if (R.Bool((R.Bool((R.Bool(R.not(R.is_numeric_value(v_start__95862))) ? (object)(true) : (object)(R.not(R.is_numeric_value(v_depth__95910))))) ? (object)(true) : (object)(R.not(R.is_numeric_value(v_count__95958))))))
            {
                return false;
            }
            if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.eq(v_start__95862, v_cursor__95696))) ? (object)(true) : (object)(R.not(R.eq(R.floor(v_start__95862), v_start__95862))))) ? (object)(true) : (object)(R.lt(v_depth__95910, R.number("0"))))) ? (object)(true) : (object)(R.lt(R.count(v_variables), v_depth__95910)))) ? (object)(true) : (object)(R.not(R.eq(R.floor(v_depth__95910), v_depth__95910))))))
            {
                return false;
            }
            object v_width__96341 = R.at(F_dv_get(v_model__95615, "widths"), v_depth__95910);
            if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.lt(R.number("0"), v_count__95958))) ? (object)(true) : (object)(R.lt(v_width__96341, v_count__95958)))) ? (object)(true) : (object)(R.not(R.eq(R.floor(v_count__95958), v_count__95958))))) ? (object)(true) : (object)(R.lt(F_dv_get(v_result, "visited"), R.add(v_start__95862, v_count__95958))))))
            {
                return false;
            }
            if (R.Bool(R.not(R.eq(R.mul(R.floor(R.div(v_start__95862, v_width__96341)), v_width__96341), v_start__95862))))
            {
                return false;
            }
            if (R.Bool((R.Bool(R.lt(v_count__95958, v_width__96341)) ? (object)(R.not(R.eq(R.add(v_start__95862, v_count__95958), F_dv_get(v_result, "visited")))) : (object)(false))))
            {
                return false;
            }
            if (R.Bool(R.eq(v_kind__96006, "candidate")))
            {
                if (R.Bool((R.Bool(R.not(R.eq(v_depth__95910, R.number("0")))) ? (object)(true) : (object)(R.not(R.eq(v_count__95958, R.number("1")))))))
                {
                    return false;
                }
                object v_candidate__96779 = v_verified;
                if (R.Bool(R.eq(v_verified, false)))
                {
                    v_candidate__96779 = F_dv_balance_candidate(v_start__95862, v_goal, v_variables, v_laws, v_limits, v_prices);
                }
                else
                {
                    if (R.Bool(R.not(R.eq(v_start__95862, F_dv_get(v_verified, "index")))))
                    {
                        v_candidate__96779 = F_dv_balance_candidate(v_start__95862, v_goal, v_variables, v_laws, v_limits, v_prices);
                    }
                }
                if (R.Bool(F_dv_get(v_candidate__96779, "feasible")))
                {
                    if (R.Bool(R.eq(v_verified, false)))
                    {
                        return false;
                    }
                    if (R.Bool(R.lt(F_dv_get(v_candidate__96779, "cost"), F_dv_get(v_verified, "cost"))))
                    {
                        return false;
                    }
                }
            }
            else
            {
                if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.eq(v_kind__96006, "goal-bound"))) ? (object)(R.not(R.eq(v_kind__96006, "law-bound"))) : (object)(false))) ? (object)(R.not(R.eq(v_kind__96006, "resource-bound"))) : (object)(false))) ? (object)(R.not(R.eq(v_kind__96006, "cost-bound"))) : (object)(false))))
                {
                    return false;
                }
                object v_fixed__97454 = F_dv_balance_interval_fixed(v_model__95615, v_start__95862, v_depth__95910);
                if (R.Bool(R.not(R.eq(F_dv_balance_interval_kind(v_model__95615, v_depth__95910, v_fixed__97454, v_verified), v_kind__96006))))
                {
                    return false;
                }
            }
            v_cursor__95696 = R.add(v_start__95862, v_count__95958);
            v_i__95715 = R.add(v_i__95715, R.number("1"));
        }
        return R.eq(v_cursor__95696, F_dv_get(v_result, "visited"));
    }

    public static object F_dv_imprint_check(object v_result, object v_goal, object v_variables, object v_laws, object v_limits, object v_prices)
    {
        if (R.Bool(R.not(F_dv_record_like(v_result))))
        {
            return false;
        }
        object v_total__98014 = F_dv_balance_extent(v_variables);
        if (R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_result, "total"), v_total__98014))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_result, "revision"), F_dv_get(v_limits, "revision")))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_result, "subject"), F_dv_get(v_limits, "subject")))))))
        {
            return false;
        }
        if (R.Bool(R.not(R.is_numeric_value(F_dv_get(v_result, "visited")))))
        {
            return false;
        }
        if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.lt(F_dv_get(v_result, "visited"), R.number("0"))) ? (object)(true) : (object)(R.lt(v_total__98014, F_dv_get(v_result, "visited"))))) ? (object)(true) : (object)(R.lt(F_dv_get(v_limits, "search_budget"), F_dv_get(v_result, "visited"))))) ? (object)(true) : (object)(R.lt(F_dv_get(v_limits, "work_remaining"), R.mul(F_dv_get(v_result, "visited"), R.number("2")))))) ? (object)(true) : (object)(R.not(R.eq(R.floor(F_dv_get(v_result, "visited")), F_dv_get(v_result, "visited")))))))
        {
            return false;
        }
        object v_best__98460 = F_dv_get(v_result, "best");
        object v_verified__98487 = false;
        if (R.Bool(R.not(R.eq(v_best__98460, false))))
        {
            if (R.Bool(R.not(F_dv_record_like(v_best__98460))))
            {
                return false;
            }
            if (R.Bool(R.not(R.is_numeric_value(F_dv_find(v_best__98460, "index", false)))))
            {
                return false;
            }
            if (R.Bool((R.Bool((R.Bool(R.not(R.is_list(F_dv_find(v_best__98460, "choices", false)))) ? (object)(true) : (object)(R.not(R.is_list(F_dv_find(v_best__98460, "values", false)))))) ? (object)(true) : (object)(R.not(R.is_list(F_dv_find(v_best__98460, "charges", false)))))))
            {
                return false;
            }
            if (R.Bool((R.Bool(R.not(F_dv_has_key(v_best__98460, "degree"))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_best__98460, "cost"))))))
            {
                return false;
            }
            if (R.Bool((R.Bool((R.Bool(R.lt(F_dv_get(v_best__98460, "index"), R.number("0"))) ? (object)(true) : (object)(R.not(R.lt(F_dv_get(v_best__98460, "index"), F_dv_get(v_result, "visited")))))) ? (object)(true) : (object)(R.not(R.eq(R.floor(F_dv_get(v_best__98460, "index")), F_dv_get(v_best__98460, "index")))))))
            {
                return false;
            }
            v_verified__98487 = F_dv_balance_candidate(F_dv_get(v_best__98460, "index"), v_goal, v_variables, v_laws, v_limits, v_prices);
            if (R.Bool((R.Bool(R.not(F_dv_get(v_verified__98487, "feasible"))) ? (object)(true) : (object)(R.not(F_dv_balance_same_witness(v_best__98460, v_verified__98487))))))
            {
                return false;
            }
        }
        object v_proof_kind__99293 = F_dv_find(v_result, "proof_kind", "enumerated-prefix-v1");
        if (R.Bool(R.eq(v_proof_kind__99293, "linear-interval-prefix-v1")))
        {
            if (R.Bool(R.not(F_dv_balance_check_coverage(v_result, v_goal, v_variables, v_laws, v_limits, v_prices, v_verified__98487))))
            {
                return false;
            }
        }
        else
        {
            if (R.Bool(R.not(R.eq(v_proof_kind__99293, "enumerated-prefix-v1"))))
            {
                return false;
            }
            object v_i__99716 = R.number("0");
            while (R.Bool(R.lt(v_i__99716, F_dv_get(v_result, "visited"))))
            {
                object v_alternative__99772 = v_verified__98487;
                if (R.Bool(R.eq(v_best__98460, false)))
                {
                    v_alternative__99772 = F_dv_balance_candidate(v_i__99716, v_goal, v_variables, v_laws, v_limits, v_prices);
                }
                else
                {
                    if (R.Bool(R.not(R.eq(v_i__99716, F_dv_get(v_best__98460, "index")))))
                    {
                        v_alternative__99772 = F_dv_balance_candidate(v_i__99716, v_goal, v_variables, v_laws, v_limits, v_prices);
                    }
                }
                if (R.Bool(F_dv_get(v_alternative__99772, "feasible")))
                {
                    if (R.Bool(R.eq(v_best__98460, false)))
                    {
                        return false;
                    }
                    if (R.Bool(R.lt(F_dv_get(v_alternative__99772, "cost"), F_dv_get(v_best__98460, "cost"))))
                    {
                        return false;
                    }
                }
                v_i__99716 = R.add(v_i__99716, R.number("1"));
            }
        }
        if (R.Bool(R.eq(F_dv_get(v_result, "visited"), v_total__98014)))
        {
            if (R.Bool(R.eq(v_best__98460, false)))
            {
                return R.eq(F_dv_get(v_result, "status"), "Infeasible");
            }
            return R.eq(F_dv_get(v_result, "status"), "Optimal");
        }
        if (R.Bool(R.eq(v_best__98460, false)))
        {
            return R.eq(F_dv_get(v_result, "status"), "Unresolved");
        }
        return R.eq(F_dv_get(v_result, "status"), "Feasible");
    }

    public static object F_dv_imprint_commit(object v_state, object v_result, object v_goal, object v_variables, object v_laws, object v_limits, object v_prices, object v_source)
    {
        _ = F_dv_require((R.Bool(R.eq(F_dv_get(v_state, "subject"), F_dv_get(v_limits, "subject"))) ? (object)(R.eq(F_dv_get(v_state, "revision"), F_dv_get(v_limits, "revision"))) : (object)(false)), "Imprint source state changed");
        _ = F_dv_require(F_dv_same(F_dv_get(v_state, "balances"), F_dv_get(v_limits, "balances")), "Imprint resource snapshot changed");
        _ = F_dv_require(F_dv_imprint_check(v_result, v_goal, v_variables, v_laws, v_limits, v_prices), "Invalid balance certificate");
        _ = F_dv_require(R.eq(F_dv_get(v_state, "work_remaining"), F_dv_get(v_limits, "work_remaining")), "Imprint work snapshot changed");
        object v_work_spent__101043 = R.mul(F_dv_get(v_result, "visited"), R.number("2"));
        object v_paid__101083 = F_dv_with_many(v_state, new System.Collections.Generic.List<object>(2) { new System.Collections.Generic.List<object>(2) { "work_remaining", R.sub(F_dv_get(v_state, "work_remaining"), v_work_spent__101043) }, new System.Collections.Generic.List<object>(2) { "revision", R.add(F_dv_get(v_state, "revision"), R.number("1")) } });
        if (R.Bool(R.not(R.eq(F_dv_get(v_result, "status"), "Optimal"))))
        {
            return new System.Collections.Generic.List<object>(4) { F_dv_pair("applied", false), F_dv_pair("state", v_paid__101083), F_dv_pair("result", v_result), F_dv_pair("work_spent", v_work_spent__101043) };
        }
        object v_best__101329 = F_dv_get(v_result, "best");
        object v_balances__101356 = new System.Collections.Generic.List<object>(0) { };
        object v_i__101378 = R.number("0");
        while (R.Bool(R.lt(v_i__101378, R.count(F_dv_get(v_state, "balances")))))
        {
            object v_before__101436 = R.exact(R.at(F_dv_get(v_state, "balances"), v_i__101378));
            object v_after__101485 = R.sub(v_before__101436, R.at(F_dv_get(v_best__101329, "charges"), v_i__101378));
            _ = F_dv_require(R.eq(v_before__101436, R.add(v_after__101485, R.at(F_dv_get(v_best__101329, "charges"), v_i__101378))), "Resource conservation failed");
            _ = R.push(v_balances__101356, v_after__101485);
            v_i__101378 = R.add(v_i__101378, R.number("1"));
        }
        object v_degree__101663 = F_dv_balance_dot(F_dv_get(v_goal, "coefficients"), F_dv_get(v_best__101329, "values"));
        object v_accepted__101727 = F_dv_acceptance_classify(v_degree__101663, F_dv_get(v_state, "levels"));
        object v_previous__101791 = F_dv_acceptance_classify(F_dv_balance_dot(F_dv_get(v_goal, "coefficients"), F_dv_get(v_state, "values")), F_dv_get(v_state, "levels"));
        object v_transition__101896 = F_dv_acceptance_transition(v_previous__101791, v_accepted__101727, v_source);
        object v_receipt__101970 = new System.Collections.Generic.List<object>(7) { F_dv_pair("kind", "Imprint"), F_dv_pair("source", v_source), F_dv_pair("before", F_dv_get(v_state, "values")), F_dv_pair("after", F_dv_get(v_best__101329, "values")), F_dv_pair("charges", F_dv_get(v_best__101329, "charges")), F_dv_pair("proof", v_result), F_dv_pair("transition", v_transition__101896) };
        object v_next__102126 = F_dv_with_many(v_paid__101083, new System.Collections.Generic.List<object>(5) { new System.Collections.Generic.List<object>(2) { "values", F_dv_get(v_best__101329, "values") }, new System.Collections.Generic.List<object>(2) { "balances", v_balances__101356 }, new System.Collections.Generic.List<object>(2) { "revision", R.add(F_dv_get(v_state, "revision"), R.number("1")) }, new System.Collections.Generic.List<object>(2) { "acceptance", v_accepted__101727 }, new System.Collections.Generic.List<object>(2) { "imprint_receipt", v_receipt__101970 } });
        return new System.Collections.Generic.List<object>(4) { F_dv_pair("applied", true), F_dv_pair("state", v_next__102126), F_dv_pair("result", v_result), F_dv_pair("work_spent", v_work_spent__101043) };
    }

    public static object F_dv_imprint_prepare(object v_mind, object v_refs, object v_ledger, object v_work_remaining, object v_levels, object v_search_budget, object v_capability)
    {
        _ = F_dv_domain(v_mind, "mind");
        object v_values__102585 = new System.Collections.Generic.List<object>(0) { };
        object v_i__102605 = R.number("0");
        while (R.Bool(R.lt(v_i__102605, R.count(v_refs))))
        {
            object v_cell__102653 = F_dv_read(v_mind, R.at(v_refs, v_i__102605));
            _ = F_dv_require((R.Bool(F_dv_get(v_cell__102653, "known")) ? (object)(R.not(F_dv_get(v_cell__102653, "conflicted"))) : (object)(false)), "Imprint structural coordinate is not established");
            _ = R.push(v_values__102585, R.exact(F_dv_get(v_cell__102653, "value")));
            v_i__102605 = R.add(v_i__102605, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(12) { F_dv_pair("subject", F_dv_get(v_mind, "subject")), F_dv_pair("revision", F_dv_get(v_mind, "revision")), F_dv_pair("mind_id", F_dv_get(v_mind, "id")), F_dv_pair("refs", v_refs), F_dv_pair("values", v_values__102585), F_dv_pair("balances", new System.Collections.Generic.List<object>(3) { F_dv_get(F_dv_get(v_ledger, "balances"), "time"), F_dv_get(F_dv_get(v_ledger, "balances"), "computation"), F_dv_get(F_dv_get(v_ledger, "balances"), "stamina") }), F_dv_pair("ledger_id", F_dv_get(v_ledger, "id")), F_dv_pair("ledger_revision", F_dv_get(v_ledger, "revision")), F_dv_pair("work_remaining", v_work_remaining), F_dv_pair("levels", v_levels), F_dv_pair("search_budget", v_search_budget), F_dv_pair("imprint_allowed", v_capability) };
    }

    public static object F_dv_imprint_apply(object v_mind, object v_physical, object v_ledger, object v_state, object v_result, object v_goal, object v_variables, object v_laws, object v_prices, object v_event)
    {
        _ = F_dv_domain(v_mind, "mind");
        _ = F_dv_require(F_dv_get(v_state, "imprint_allowed"), "Imprint capability is required");
        _ = F_dv_require((R.Bool((R.Bool(R.eq(F_dv_get(v_mind, "id"), F_dv_get(v_state, "mind_id"))) ? (object)(R.eq(F_dv_get(v_mind, "subject"), F_dv_get(v_state, "subject"))) : (object)(false))) ? (object)(R.eq(F_dv_get(v_mind, "revision"), F_dv_get(v_state, "revision"))) : (object)(false)), "Imprint target snapshot changed");
        _ = F_dv_require((R.Bool(R.eq(F_dv_get(v_ledger, "id"), F_dv_get(v_state, "ledger_id"))) ? (object)(R.eq(F_dv_get(v_ledger, "revision"), F_dv_get(v_state, "ledger_revision"))) : (object)(false)), "Imprint ledger snapshot changed");
        object v_current__103689 = F_dv_imprint_prepare(v_mind, F_dv_get(v_state, "refs"), v_ledger, F_dv_get(v_state, "work_remaining"), F_dv_get(v_state, "levels"), F_dv_get(v_state, "search_budget"), F_dv_get(v_state, "imprint_allowed"));
        _ = F_dv_require(F_dv_balance_same_numbers(F_dv_get(v_current__103689, "values"), F_dv_get(v_state, "values")), "Imprint structural input changed");
        object v_committed__103938 = F_dv_imprint_commit(v_current__103689, v_result, v_goal, v_variables, v_laws, v_state, v_prices, v_event);
        object v_next_mind__104038 = v_mind;
        object v_actual__104063 = F_dv_action_cost_vector(R.number("0"), R.number("0"), R.number("0"));
        object v_outcome__104111 = "rejected";
        if (R.Bool(F_dv_get(v_committed__103938, "applied")))
        {
            object v_charges__104170 = F_dv_get(F_dv_get(F_dv_get(v_committed__103938, "state"), "imprint_receipt"), "charges");
            v_actual__104063 = F_dv_action_cost_vector(R.at(v_charges__104170, R.number("0")), R.at(v_charges__104170, R.number("1")), R.at(v_charges__104170, R.number("2")));
            v_outcome__104111 = "applied";
            object v_origin__104335 = new System.Collections.Generic.List<object>(4) { F_dv_pair("kind", "Imprint"), F_dv_pair("event", v_event), F_dv_pair("subject", F_dv_get(v_mind, "subject")), F_dv_pair("proof", v_result) };
            object v_i__104426 = R.number("0");
            while (R.Bool(R.lt(v_i__104426, R.count(F_dv_get(v_state, "refs")))))
            {
                v_next_mind__104038 = F_dv_store(v_next_mind__104038, R.at(F_dv_get(v_state, "refs"), v_i__104426), F_dv_cell(R.at(F_dv_get(F_dv_get(v_committed__103938, "state"), "values"), v_i__104426), R.number("1"), R.add(F_dv_find(v_physical, "time", R.number("0")), F_dv_get(v_actual__104063, "time")), new System.Collections.Generic.List<object>(1) { v_origin__104335 }));
                v_i__104426 = R.add(v_i__104426, R.number("1"));
            }
            v_next_mind__104038 = F_dv_with_many(v_next_mind__104038, new System.Collections.Generic.List<object>(3) { new System.Collections.Generic.List<object>(2) { "revision", F_dv_get(F_dv_get(v_committed__103938, "state"), "revision") }, new System.Collections.Generic.List<object>(2) { "history", F_dv_append(F_dv_get(v_mind, "history"), F_dv_get(F_dv_get(v_committed__103938, "state"), "imprint_receipt")) }, new System.Collections.Generic.List<object>(2) { "imprint_acceptance", F_dv_get(F_dv_get(v_committed__103938, "state"), "acceptance") } });
            object v_observed__104867 = F_dv_imprint_prepare(v_next_mind__104038, F_dv_get(v_state, "refs"), v_ledger, F_dv_get(F_dv_get(v_committed__103938, "state"), "work_remaining"), F_dv_get(v_state, "levels"), F_dv_get(v_state, "search_budget"), F_dv_get(v_state, "imprint_allowed"));
            _ = F_dv_require(R.not(R.lt(F_dv_balance_dot(F_dv_get(v_goal, "coefficients"), F_dv_get(v_observed__104867, "values")), R.exact(F_dv_get(v_goal, "minimum")))), "Stored cognition did not achieve imprint goal");
        }
        object v_bounds__105172 = F_dv_action_cost_bounds(v_actual__104063, v_actual__104063);
        object v_quote__105227 = F_dv_action_cost_quote(v_ledger, v_physical, "cognitive-imprint", v_event, v_bounds__105172);
        object v_settlement__105318 = F_dv_action_cost_finish(v_ledger, v_physical, v_quote__105227, v_actual__104063, v_outcome__104111);
        return new System.Collections.Generic.List<object>(8) { F_dv_pair("applied", F_dv_get(v_committed__103938, "applied")), F_dv_pair("mind", v_next_mind__104038), F_dv_pair("physical", F_dv_get(F_dv_get(v_settlement__105318, "execution"), "world")), F_dv_pair("ledger", F_dv_get(v_settlement__105318, "ledger")), F_dv_pair("work_remaining", F_dv_get(F_dv_get(v_committed__103938, "state"), "work_remaining")), F_dv_pair("work_spent", F_dv_get(v_committed__103938, "work_spent")), F_dv_pair("proof", v_result), F_dv_pair("cost_receipt", F_dv_get(F_dv_get(F_dv_get(v_settlement__105318, "ledger"), "receipts"), v_event)) };
    }
}
}

// --- Source: CausalIndex.cs ---
namespace DuelView.Compiled
{


// Shared runtime implementation; state remains owned by the execution context.
public static partial class R
{
    private static readonly AsyncLocal<CausalLookup?> CausalLookupContext = new();
    public static void ConfigureCausalIndex(bool enabled) => CausalLookupContext.Value = enabled ? new CausalLookup() : null;
    private sealed class CausalLookup
    {
        internal object? Owner;
        internal Dictionary<string, object> Events = new(StringComparer.Ordinal);
        internal Dictionary<string, string[]> Parents = new(StringComparer.Ordinal);
        internal Dictionary<string, CausalClosure> Closures = new(StringComparer.Ordinal);
        internal Dictionary<string, int> FirstPositions = new(StringComparer.Ordinal);
        internal Queue<string> ClosureOrder = new();
        internal bool Bind(object events)
        {
            // Called only after the explicit checker switch was enabled.
            // snapshot reuses a valid mutable Backbone and invalidation rebuilds
            // it after edits; fresh DV prefixes acquire their own frozen identity.
            try { events = snapshot(events); }
            catch (Exception) { return false; }
            if (ReferenceEquals(Owner, events)) return true;
            // Unusual inputs retain the original DV error/equality path.
            if (events is not FrozenList || !TryItems(events, out var entries)) return false;
            var byId = new Dictionary<string, object>(StringComparer.Ordinal);
            var parents = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            var position = 0;
            try
            {
                foreach (var entry in entries)
                {
                    if (record_find(entry, "id", false) is not string id) return false;
                    var rawParents = record_find(entry, "parents", new List<object>());
                    if (!TryItems(rawParents, out var items)) return false;
                    var names = new string[items.Count];
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (items[i] is not string name) return false;
                        names[i] = name;
                    }
                    positions.TryAdd(id, position++);
                    byId[id] = entry; parents[id] = names; // original last match
                }
            }
            catch (Exception) { return false; }
            Owner = events; Events = byId; Parents = parents; FirstPositions = positions;
            Closures.Clear(); ClosureOrder.Clear();
            return true;
        }
        internal sealed record CausalClosure(object Ordered, HashSet<string> Members);
        internal CausalClosure Closure(string child)
        {
            if (Closures.TryGetValue(child, out var cached)) return cached;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>(); queue.Enqueue(child);
            var order = new List<object>();
            while (queue.Count != 0)
            {
                var id = queue.Dequeue();
                if (!visited.Add(id)) continue;
                order.Add(id);
                if (Parents.TryGetValue(id, out var parents))
                    foreach (var parent in parents) queue.Enqueue(parent);
            }
            var frozen = new CausalClosure(snapshot(order), visited);
            // At most eight complete closures, and one event-list index.
            if (ClosureOrder.Count == 8) Closures.Remove(ClosureOrder.Dequeue());
            ClosureOrder.Enqueue(child); Closures.Add(child, frozen);
            return frozen;
        }
    }
    private static object CausalResult(bool supported, object value) => new List<object> { supported, value };
    public static object causal_event_index(object events, object id)
    {
        var cache = CausalLookupContext.Value;
        if (cache is null || id is not string name || !cache.Bind(events)) return CausalResult(false, false);
        return CausalResult(true, cache.Events.TryGetValue(name, out var found) ? found : new List<object>());
    }
    public static object causal_ancestors_index(object events, object child)
    {
        var cache = CausalLookupContext.Value;
        if (cache is null || child is not string name || !cache.Bind(events)) return CausalResult(false, false);
        return CausalResult(true, cache.Closure(name).Ordered);
    }
    public static object causal_ancestor_index(object events, object child, object ancestor)
    {
        var cache = CausalLookupContext.Value;
        if (cache is null || child is not string name || ancestor is not string target || !cache.Bind(events)) return CausalResult(false, false);
        // Full BFS is finite for the admitted graph and preserves missing-ID
        // and self-membership semantics; no checks are removed by this lookup.
        return CausalResult(true, cache.Closure(name).Members.Contains(target));
    }

    public static object causal_prior_index(object events, object position, object id)
    {
        var cache = CausalLookupContext.Value;
        if (cache is null || id is not string name || !cache.Bind(events)) return CausalResult(false, false);
        return CausalResult(true, cache.FirstPositions.TryGetValue(name, out var first) && first < Index(position));
    }
}
}

// --- Source: CausalSupport.cs ---
namespace DuelView.Core
{


/// <summary>Session / CausalSupport: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_update_support_events(object v_actor, object v_events)
    {
        v_events = R.snapshot(v_events);
        object v_support__261279 = F_dv_find(v_actor, "support_events", new System.Collections.Generic.List<object>(0) { });
        object v_cursor__261334 = F_dv_find(v_actor, "support_events_cursor", R.number("0"));
        object v_total__261394 = R.count(v_events);
        if (R.Bool(R.eq(v_cursor__261334, v_total__261394)))
        {
            return v_actor;
        }
        object v_subject__261472 = F_dv_get(v_actor, "id");
        object v_index_key__261499 = new System.Collections.Generic.List<object>(2) { "support-compact-index-v1", v_subject__261472 };
        object v_cached_index__261557 = R.memo_get(v_support__261279, v_index_key__261499);
        object v_index__261612 = false;
        if (R.Bool(R.at(v_cached_index__261557, R.number("0"))))
        {
            v_index__261612 = R.at(v_cached_index__261557, R.number("1"));
        }
        object v_added__261690 = false;
        while (R.Bool(R.lt(v_cursor__261334, v_total__261394)))
        {
            object v_event__261742 = R.at(v_events, v_cursor__261334);
            if (R.Bool(R.eq(F_dv_get(v_event__261742, "actor"), v_subject__261472)))
            {
                object v_type__261816 = F_dv_get(v_event__261742, "type");
                if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(v_type__261816, "observation")) ? (object)(true) : (object)(R.eq(v_type__261816, "listener_parse")))) ? (object)(true) : (object)(R.eq(v_type__261816, "inference")))) ? (object)(true) : (object)(R.eq(v_type__261816, "model_argument_accepted")))))
                {
                    if (R.Bool(R.not(R.eq(v_index__261612, false))))
                    {
                        v_index__261612 = F_dvs_support_compact_add(v_index__261612, v_event__261742, R.count(v_support__261279), v_subject__261472);
                    }
                    _ = R.push(v_support__261279, v_event__261742);
                    v_added__261690 = true;
                }
            }
            v_cursor__261334 = R.add(v_cursor__261334, R.number("1"));
        }
        if (R.Bool((R.Bool(v_added__261690) ? (object)(R.not(R.eq(v_index__261612, false))) : (object)(false))))
        {
            _ = R.memo_put(v_support__261279, v_index_key__261499, v_index__261612);
        }
        return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(2) { F_dv_pair("support_events", v_support__261279), F_dv_pair("support_events_cursor", v_total__261394) });
    }

    public static object F_dvs_subject_support_events(object v_events, object v_subject)
    {
        object v_result__262415 = new System.Collections.Generic.List<object>(0) { };
        object v_j__262435 = R.number("0");
        object v_total__262449 = R.count(v_events);
        while (R.Bool(R.lt(v_j__262435, v_total__262449)))
        {
            object v_event__262509 = R.at(v_events, v_j__262435);
            if (R.Bool(R.eq(F_dv_get(v_event__262509, "actor"), v_subject)))
            {
                object v_type__262580 = F_dv_get(v_event__262509, "type");
                if (R.Bool(F_dv_or(R.eq(v_type__262580, "observation"), F_dv_or(R.eq(v_type__262580, "listener_parse"), F_dv_or(R.eq(v_type__262580, "inference"), R.eq(v_type__262580, "model_argument_accepted"))))))
                {
                    _ = R.push(v_result__262415, v_event__262509);
                }
            }
            v_j__262435 = R.add(v_j__262435, R.number("1"));
        }
        return v_result__262415;
    }

    public static object F_dvs_support_ref_key(object v_subject, object v_ref)
    {
        object v_key__262948 = new System.Collections.Generic.List<object>(2) { "dvs_support_ref_key", v_subject };
        object v_cached__262995 = R.memo_get(v_ref, v_key__262948);
        if (R.Bool(R.at(v_cached__262995, R.number("0"))))
        {
            return R.at(v_cached__262995, R.number("1"));
        }
        return R.memo_put(v_ref, v_key__262948, R.json_stringify(new System.Collections.Generic.List<object>(5) { F_dv_get(v_ref, "type"), F_dv_get(v_ref, "object"), F_dv_get(v_ref, "facet"), F_dv_get(v_ref, "field"), F_dv_find(v_ref, "subject", v_subject) }));
    }

    public static object F_dvs_support_compact_add(object v_index, object v_event, object v_position, object v_subject)
    {
        object v_refs__263494 = F_dv_get(v_index, "refs");
        object v_accepted__263520 = F_dv_get(v_index, "accepted");
        if (R.Bool((R.Bool(R.eq(F_dv_get(v_event, "type"), "observation")) ? (object)(true) : (object)(R.eq(F_dv_get(v_event, "type"), "listener_parse")))))
        {
            object v_observations__263628 = F_dv_get(F_dv_get(v_event, "data"), "observations");
            object v_i__263679 = R.number("0");
            while (R.Bool(R.lt(v_i__263679, R.count(v_observations__263628))))
            {
                object v_observation__263743 = R.at(v_observations__263628, v_i__263679);
                object v_key__263789 = F_dvs_support_ref_key(v_subject, F_dv_get(v_observation__263743, "ref"));
                object v_entries__263857 = F_dv_find(v_refs__263494, v_key__263789, new System.Collections.Generic.List<object>(0) { });
                _ = R.push(v_entries__263857, new System.Collections.Generic.List<object>(4) { F_dv_pair("position", v_position), F_dv_pair("event", F_dv_get(v_event, "id")), F_dv_pair("kind", "observation"), F_dv_pair("observation", v_observation__263743) });
                v_refs__263494 = F_dv_with(v_refs__263494, v_key__263789, v_entries__263857);
                v_i__263679 = R.add(v_i__263679, R.number("1"));
            }
        }
        if (R.Bool(R.eq(F_dv_get(v_event, "type"), "inference")))
        {
            object v_changes__264128 = F_dv_get(F_dv_get(F_dv_get(v_event, "data"), "receipt"), "changes");
            object v_i__264177 = R.number("0");
            while (R.Bool(R.lt(v_i__264177, R.count(v_changes__264128))))
            {
                object v_change__264236 = R.at(v_changes__264128, v_i__264177);
                object v_key__264272 = F_dvs_support_ref_key(v_subject, F_dv_get(v_change__264236, "ref"));
                object v_entries__264335 = F_dv_find(v_refs__263494, v_key__264272, new System.Collections.Generic.List<object>(0) { });
                _ = R.push(v_entries__264335, new System.Collections.Generic.List<object>(5) { F_dv_pair("position", v_position), F_dv_pair("event", F_dv_get(v_event, "id")), F_dv_pair("kind", "inference"), F_dv_pair("ref", F_dv_get(v_change__264236, "ref")), F_dv_pair("after", F_dv_get(v_change__264236, "after")) });
                v_refs__263494 = F_dv_with(v_refs__263494, v_key__264272, v_entries__264335);
                v_i__264177 = R.add(v_i__264177, R.number("1"));
            }
        }
        if (R.Bool(R.eq(F_dv_get(v_event, "type"), "model_argument_accepted")))
        {
            _ = R.push(v_accepted__263520, new System.Collections.Generic.List<object>(2) { F_dv_pair("position", v_position), F_dv_pair("event", F_dv_get(v_event, "id")) });
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("refs", v_refs__263494), F_dv_pair("accepted", v_accepted__263520) };
    }

    public static object F_dvs_support_compact_index(object v_events, object v_subject)
    {
        object v_key__264793 = new System.Collections.Generic.List<object>(2) { "support-compact-index-v1", v_subject };
        object v_cached__264845 = R.memo_get(v_events, v_key__264793);
        if (R.Bool(R.at(v_cached__264845, R.number("0"))))
        {
            return R.at(v_cached__264845, R.number("1"));
        }
        object v_index__264930 = new System.Collections.Generic.List<object>(2) { F_dv_pair("refs", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("accepted", new System.Collections.Generic.List<object>(0) { }) };
        object v_i__264975 = R.number("0");
        while (R.Bool(R.lt(v_i__264975, R.count(v_events))))
        {
            v_index__264930 = F_dvs_support_compact_add(v_index__264930, R.at(v_events, v_i__264975), v_i__264975, v_subject);
            v_i__264975 = R.add(v_i__264975, R.number("1"));
        }
        return R.memo_put(v_events, v_key__264793, v_index__264930);
    }

    public static object F_dvs_support_cell_parents(object v_cell, object v_ref, object v_subject, object v_entries, object v_accepted)
    {
        object v_key__265221 = new System.Collections.Generic.List<object>(5) { "support-cell-parents-v1", v_ref, v_subject, v_entries, v_accepted };
        object v_cached__265296 = R.memo_get(v_cell, v_key__265221);
        if (R.Bool(R.at(v_cached__265296, R.number("0"))))
        {
            return R.at(v_cached__265296, R.number("1"));
        }
        object v_claims__265379 = F_dv_find(v_cell, "claims", new System.Collections.Generic.List<object>(0) { });
        object v_basis__265424 = F_dv_find(v_cell, "basis", new System.Collections.Generic.List<object>(0) { });
        object v_parents__265467 = new System.Collections.Generic.List<object>(0) { };
        object v_i__265488 = R.number("0");
        object v_j__265502 = R.number("0");
        while (R.Bool((R.Bool(R.lt(v_i__265488, R.count(v_entries))) ? (object)(true) : (object)(R.lt(v_j__265502, R.count(v_accepted))))))
        {
            object v_from_entry__265579 = R.not(R.lt(v_j__265502, R.count(v_accepted)));
            if (R.Bool((R.Bool(R.lt(v_i__265488, R.count(v_entries))) ? (object)(R.lt(v_j__265502, R.count(v_accepted))) : (object)(false))))
            {
                v_from_entry__265579 = R.lt(F_dv_get(R.at(v_entries, v_i__265488), "position"), F_dv_get(R.at(v_accepted, v_j__265502), "position"));
            }
            object v_parent__265757 = "";
            if (R.Bool(v_from_entry__265579))
            {
                object v_entry__265808 = R.at(v_entries, v_i__265488);
                if (R.Bool(R.eq(F_dv_get(v_entry__265808, "kind"), "observation")))
                {
                    if (R.Bool((R.Bool(F_dv_same_local_ref(v_subject, F_dv_get(F_dv_get(v_entry__265808, "observation"), "ref"), v_ref)) ? (object)(F_dv_has(v_claims__265379, F_dv_get(v_entry__265808, "observation"))) : (object)(false))))
                    {
                        v_parent__265757 = F_dv_get(v_entry__265808, "event");
                    }
                }
                else
                {
                    if (R.Bool((R.Bool(F_dv_same(F_dv_get(v_entry__265808, "ref"), v_ref)) ? (object)(F_dv_same(F_dv_get(v_entry__265808, "after"), v_cell)) : (object)(false))))
                    {
                        v_parent__265757 = F_dv_get(v_entry__265808, "event");
                    }
                }
                v_i__265488 = R.add(v_i__265488, R.number("1"));
            }
            else
            {
                if (R.Bool(F_dv_has(v_basis__265424, F_dv_get(R.at(v_accepted, v_j__265502), "event"))))
                {
                    v_parent__265757 = F_dv_get(R.at(v_accepted, v_j__265502), "event");
                }
                v_j__265502 = R.add(v_j__265502, R.number("1"));
            }
            if (R.Bool((R.Bool(R.not(R.eq(v_parent__265757, ""))) ? (object)(R.not(F_dv_has(v_parents__265467, v_parent__265757))) : (object)(false))))
            {
                _ = R.push(v_parents__265467, v_parent__265757);
            }
        }
        return R.memo_put(v_cell, v_key__265221, v_parents__265467);
    }

    public static object F_dvs_support_event_index(object v_events, object v_subject)
    {
        object v_key__266500 = new System.Collections.Generic.List<object>(2) { "support-event-index", v_subject };
        object v_cached__266547 = R.memo_get(v_events, v_key__266500);
        if (R.Bool(R.at(v_cached__266547, R.number("0"))))
        {
            return R.at(v_cached__266547, R.number("1"));
        }
        object v_index__266632 = new System.Collections.Generic.List<object>(0) { };
        object v_accepted__266651 = new System.Collections.Generic.List<object>(0) { };
        object v_i__266673 = R.number("0");
        while (R.Bool(R.lt(v_i__266673, R.count(v_events))))
        {
            object v_event__266723 = R.at(v_events, v_i__266673);
            object v_refs__266753 = new System.Collections.Generic.List<object>(0) { };
            if (R.Bool((R.Bool(R.eq(F_dv_get(v_event__266723, "type"), "observation")) ? (object)(true) : (object)(R.eq(F_dv_get(v_event__266723, "type"), "listener_parse")))))
            {
                object v_observations__266853 = F_dv_get(F_dv_get(v_event__266723, "data"), "observations");
                object v_j__266908 = R.number("0");
                while (R.Bool(R.lt(v_j__266908, R.count(v_observations__266853))))
                {
                    _ = R.push(v_refs__266753, F_dv_get(R.at(v_observations__266853, v_j__266908), "ref"));
                    v_j__266908 = R.add(v_j__266908, R.number("1"));
                }
            }
            if (R.Bool(R.eq(F_dv_get(v_event__266723, "type"), "inference")))
            {
                object v_changes__267091 = F_dv_get(F_dv_get(F_dv_get(v_event__266723, "data"), "receipt"), "changes");
                object v_j__267144 = R.number("0");
                while (R.Bool(R.lt(v_j__267144, R.count(v_changes__267091))))
                {
                    _ = R.push(v_refs__266753, F_dv_get(R.at(v_changes__267091, v_j__267144), "ref"));
                    v_j__267144 = R.add(v_j__267144, R.number("1"));
                }
            }
            if (R.Bool(R.eq(F_dv_get(v_event__266723, "type"), "model_argument_accepted")))
            {
                _ = R.push(v_accepted__266651, v_i__266673);
            }
            object v_j__267360 = R.number("0");
            while (R.Bool(R.lt(v_j__267360, R.count(v_refs__266753))))
            {
                object v_ref_key__267416 = F_dvs_support_ref_key(v_subject, R.at(v_refs__266753, v_j__267360));
                object v_positions__267480 = F_dv_find(v_index__266632, v_ref_key__267416, new System.Collections.Generic.List<object>(0) { });
                if (R.Bool(R.not(F_dv_has(v_positions__267480, v_i__266673))))
                {
                    _ = R.push(v_positions__267480, v_i__266673);
                    object v_slot__267619 = R.record_index(v_index__266632, v_ref_key__267416);
                    if (R.Bool(R.lt(v_slot__267619, R.number("0"))))
                    {
                        _ = R.push(v_index__266632, F_dv_pair(v_ref_key__267416, v_positions__267480));
                    }
                    else
                    {
                        _ = R.replace(v_index__266632, v_slot__267619, F_dv_pair(v_ref_key__267416, v_positions__267480));
                    }
                }
                v_j__267360 = R.add(v_j__267360, R.number("1"));
            }
            v_i__266673 = R.add(v_i__266673, R.number("1"));
        }
        return R.memo_put(v_events, v_key__266500, new System.Collections.Generic.List<object>(2) { F_dv_pair("refs", v_index__266632), F_dv_pair("accepted", v_accepted__266651) });
    }

    public static object F_dvs_support_parents(object v_mind, object v_refs, object v_events, object v_subject_or_actor)
    {
        object v_subject__268032 = v_subject_or_actor;
        object v_cand_events__268067 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(F_dv_record_like(v_subject_or_actor)))
        {
            v_subject__268032 = F_dv_get(v_subject_or_actor, "id");
            v_cand_events__268067 = F_dv_find(v_subject_or_actor, "support_events", new System.Collections.Generic.List<object>(0) { });
            if (R.Bool(R.eq(R.count(v_cand_events__268067), R.number("0"))))
            {
                v_cand_events__268067 = F_dvs_subject_support_events(v_events, v_subject__268032);
            }
        }
        else
        {
            v_cand_events__268067 = F_dvs_subject_support_events(v_events, v_subject__268032);
        }
        object v_support_index__268426 = F_dvs_support_compact_index(v_cand_events__268067, v_subject__268032);
        object v_parents__268498 = new System.Collections.Generic.List<object>(0) { };
        object v_i__268519 = R.number("0");
        while (R.Bool(R.lt(v_i__268519, R.count(v_refs))))
        {
            object v_ref__268569 = R.at(v_refs, v_i__268519);
            object v_cell__268595 = F_dv_read(v_mind, v_ref__268569);
            object v_entries__268633 = F_dv_find(F_dv_get(v_support_index__268426, "refs"), F_dvs_support_ref_key(v_subject__268032, v_ref__268569), new System.Collections.Generic.List<object>(0) { });
            object v_supported__268722 = F_dvs_support_cell_parents(v_cell__268595, v_ref__268569, v_subject__268032, v_entries__268633, F_dv_get(v_support_index__268426, "accepted"));
            object v_j__268824 = R.number("0");
            while (R.Bool(R.lt(v_j__268824, R.count(v_supported__268722))))
            {
                if (R.Bool(R.not(F_dv_has(v_parents__268498, R.at(v_supported__268722, v_j__268824)))))
                {
                    _ = R.push(v_parents__268498, R.at(v_supported__268722, v_j__268824));
                }
                v_j__268824 = R.add(v_j__268824, R.number("1"));
            }
            v_i__268519 = R.add(v_i__268519, R.number("1"));
        }
        return v_parents__268498;
    }

    public static object F_dvs_selection_parents(object v_actor, object v_action, object v_choice, object v_events, object v_parent)
    {
        object v_refs__269103 = F_dv_get(v_choice, "relevant_refs");
        object v_parents__269139 = F_dvs_cognitive_parents(v_actor, F_dv_single(v_parent));
        v_refs__269103 = F_dvp_merge_refs(v_refs__269103, F_dvs_sim_refs(F_dv_get(v_action, "precondition")));
        v_refs__269103 = F_dvp_merge_refs(v_refs__269103, F_dvp_owner_requirement_refs(v_action));
        if (R.Bool(F_dv_has_key(v_action, "answer")))
        {
            v_refs__269103 = F_dvp_merge_refs(v_refs__269103, F_dvs_sim_refs(F_dv_get(v_action, "answer")));
        }
        if (R.Bool(F_dv_has_key(v_actor, "question_ref")))
        {
            v_refs__269103 = F_dvp_add_ref(v_refs__269103, F_dv_get(v_actor, "question_ref"));
        }
        v_parents__269139 = F_dv_explanation_merge(v_parents__269139, F_dvs_support_parents(F_dv_get(v_actor, "mind"), v_refs__269103, v_events, v_actor));
        if (R.Bool(R.eq(F_dv_find(v_actor, "hypothesis_event", ""), "")))
        {
            _ = false;
        }
        else
        {
            v_parents__269139 = F_dv_explanation_merge(v_parents__269139, F_dv_single(F_dv_get(v_actor, "hypothesis_event")));
        }
        if (R.Bool(F_dv_has_key(v_action, "argument")))
        {
            v_parents__269139 = F_dv_explanation_merge(v_parents__269139, F_dv_single(F_dv_get(F_dv_get(v_action, "argument"), "id")));
        }
        return v_parents__269139;
    }

    public static object F_dvs_model_parent_ids(object v_actor, object v_parent, object v_prior)
    {
        object v_result__269982 = F_dvs_cognitive_parents(v_actor, F_dv_single(v_parent));
        if (R.Bool(R.eq(F_dv_find(v_actor, "model_paid_event", ""), "")))
        {
            _ = false;
        }
        else
        {
            v_result__269982 = F_dv_explanation_merge(v_result__269982, F_dv_single(F_dv_get(v_actor, "model_paid_event")));
        }
        if (R.Bool(R.eq(F_dv_find(v_actor, "model_previous_event", ""), "")))
        {
            _ = false;
        }
        else
        {
            v_result__269982 = F_dv_explanation_merge(v_result__269982, F_dv_single(F_dv_get(v_actor, "model_previous_event")));
        }
        if (R.Bool(R.eq(v_prior, "")))
        {
            _ = false;
        }
        else
        {
            v_result__269982 = F_dv_explanation_merge(v_result__269982, F_dv_single(v_prior));
        }
        return v_result__269982;
    }

    public static object F_dvs_public_grant_parents(object v_events, object v_parent, object v_subject)
    {
        object v_result__270558 = F_dv_single(v_parent);
        object v_literal__270593 = "";
        object v_i__270614 = R.sub(R.count(v_events), R.number("1"));
        while (R.Bool((R.Bool(R.not(R.lt(v_i__270614, R.number("0")))) ? (object)(R.eq(v_literal__270593, "")) : (object)(false))))
        {
            object v_event__270689 = R.at(v_events, v_i__270614);
            if (R.Bool(R.eq(F_dv_get(v_event__270689, "type"), "speech_literal")))
            {
                if (R.Bool(F_dv_has(F_dv_get(v_event__270689, "parents"), v_parent)))
                {
                    v_literal__270593 = F_dv_get(v_event__270689, "id");
                }
            }
            v_i__270614 = R.sub(v_i__270614, R.number("1"));
        }
        if (R.Bool(R.eq(v_literal__270593, "")))
        {
            _ = false;
        }
        else
        {
            v_result__270558 = F_dv_explanation_merge(v_result__270558, F_dv_single(v_literal__270593));
            v_i__270614 = R.sub(R.count(v_events), R.number("1"));
            object v_found_parse__271010 = false;
            while (R.Bool((R.Bool(R.not(R.lt(v_i__270614, R.number("0")))) ? (object)(R.not(v_found_parse__271010)) : (object)(false))))
            {
                object v_event__271090 = R.at(v_events, v_i__270614);
                if (R.Bool(F_dv_and(R.eq(F_dv_get(v_event__271090, "type"), "listener_parse"), R.eq(F_dv_get(v_event__271090, "actor"), v_subject))))
                {
                    if (R.Bool(R.eq(F_dv_find(F_dv_get(v_event__271090, "data"), "source_event", ""), v_literal__270593)))
                    {
                        v_result__270558 = F_dv_explanation_merge(v_result__270558, F_dv_single(F_dv_get(v_event__271090, "id")));
                        v_found_parse__271010 = true;
                    }
                }
                v_i__270614 = R.sub(v_i__270614, R.number("1"));
            }
        }
        return v_result__270558;
    }
}
}

// --- Source: BinaryEvidence.cs ---
namespace DuelView.Compiled
{


// Storage codec only. JSON remains an in-memory host/compiler exchange format.
public static class BinaryEvidence
{
    public static readonly byte[] Magic = "DVLOG2\r\n"u8.ToArray();
    private const int MaxFrame = 256 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public static void RequireBinaryPath(string path)
    {
        var parts = Path.GetFileName(path).Split('.');
        if (parts.Any(p => p.Equals("json", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("jsonl", StringComparison.OrdinalIgnoreCase) || p.Equals("gz", StringComparison.OrdinalIgnoreCase)))
            throw new IOException("JSON recording is forbidden; use .dvlog/.dvdag/.dvsave");
    }
    public static byte[] Encode(object? value)
    {
        var writer = new Encoder();
        writer.Value(value);
        return writer.WrittenSpan.ToArray();
    }
    // One writer owns its buffer. DAG frames can append their already encoded
    // payload directly to an envelope without a second object-graph traversal.
    internal sealed class Encoder
    {
        private readonly ArrayBufferWriter<byte> output = new(1024);
        internal ReadOnlySpan<byte> WrittenSpan => output.WrittenSpan;
        internal int Count => output.WrittenCount;
        internal void Clear() => output.Clear();
        private void Byte(byte value)
        {
            output.GetSpan(1)[0] = value; output.Advance(1);
        }
        private void Int32(int value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(output.GetSpan(4), value);
            output.Advance(4);
        }
        internal void Integer(long value)
        {
            var bytes = output.GetSpan(9); bytes[0] = 3;
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(bytes[1..], value);
            output.Advance(9);
        }
        internal void Text(string text)
        {
            var length = Utf8.GetByteCount(text);
            Byte(5); Int32(length);
            var written = Utf8.GetBytes(text.AsSpan(), output.GetSpan(length));
            output.Advance(written);
        }
        internal void Utf8Text(ReadOnlySpan<byte> text)
        {
            Byte(5); Int32(text.Length);
            text.CopyTo(output.GetSpan(text.Length)); output.Advance(text.Length);
        }
        internal void Map(int count) { Byte(7); Int32(count); }
        internal void List(int count) { Byte(8); Int32(count); }
        internal void Flag(bool value) => Byte(value ? (byte)2 : (byte)1);
        internal void Value(object? value, int depth = 0)
        {
            if (depth > 512) throw new InvalidDataException("Binary evidence nesting exceeds 512");
            switch (value)
            {
                case null: Byte(0); break;
                case bool flag: Flag(flag); break;
                case int number: Integer(number); break;
                case long number: Integer(number); break;
                case double number when double.IsFinite(number):
                    Byte(4);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(output.GetSpan(8), BitConverter.DoubleToInt64Bits(number));
                    output.Advance(8); break;
                case string text: Text(text); break;
                case IDictionary<string, object?> map:
                    Map(map.Count);
                    foreach (var entry in map) { Text(entry.Key); Value(entry.Value, depth + 1); }
                    break;
                case object?[] array:
                    // Host arrays are storage values, not members of the DV value domain.
                    List(array.Length);
                    foreach (var item in array) Value(item, depth + 1);
                    break;
                case JsonElement element: Value(FromJson(element), depth); break;
                default:
                    if (!R.TryItems(value!, out var items)) throw new InvalidDataException("Unsupported binary evidence atom");
                    List(items.Count);
                    for (var i = 0; i < items.Count; i++) Value(items[i], depth + 1);
                    break;
            }
        }
    }
    public static object? FromJson(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().ToDictionary(p => p.Name, p => FromJson(p.Value)),
        JsonValueKind.Array => value.EnumerateArray().Select(FromJson).ToArray(),
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.TryGetInt64(out var integer) ? (object)integer : value.GetDouble(),
        JsonValueKind.True => true, JsonValueKind.False => false, JsonValueKind.Null => null,
        _ => throw new InvalidDataException("Unsupported host record value")
    };
    public static object? Decode(byte[] payload)
    {
        using var input = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(input, Utf8);
        var result = ReadValue(reader, payload.Length, 0);
        if (input.Position != input.Length) throw new InvalidDataException("Trailing binary value bytes");
        return result;
    }
    private static object? ReadValue(BinaryReader reader, int limit, int depth)
    {
        if (depth > 512) throw new InvalidDataException("Binary evidence nesting exceeds 512");
        switch (reader.ReadByte())
        {
            case 0: return null;
            case 1: return false;
            case 2: return true;
            case 3: return reader.ReadInt64();
            case 4:
                var number = reader.ReadDouble();
                if (!double.IsFinite(number)) throw new InvalidDataException("Nonfinite evidence number");
                return number;
            case 5:
                var size = reader.ReadInt32();
                if (size < 0 || size > limit) throw new InvalidDataException("Invalid binary text length");
                var bytes = reader.ReadBytes(size);
                if (bytes.Length != size) throw new EndOfStreamException();
                return Utf8.GetString(bytes);
            case 7:
                var count = reader.ReadInt32();
                if (count < 0 || count > limit) throw new InvalidDataException("Invalid binary map length");
                var map = new Dictionary<string, object?>();
                for (var i = 0; i < count; i++)
                {
                    var key = ReadValue(reader, limit, depth + 1) as string ?? throw new InvalidDataException("Binary map key is not text");
                    map.Add(key, ReadValue(reader, limit, depth + 1));
                }
                return map;
            case 8:
                var length = reader.ReadInt32();
                if (length < 0 || length > limit) throw new InvalidDataException("Invalid binary list length");
                var list = new List<object?>();
                for (var i = 0; i < length; i++) list.Add(ReadValue(reader, limit, depth + 1));
                return list;
            default: throw new InvalidDataException("Unknown binary value tag");
        }
    }
    public static void WriteFrame(Stream stream, object? value)
    {
        var writer = new Encoder();
        writer.Value(value);
        WriteEncodedFrame(stream, writer.WrittenSpan);
    }
    internal static void WriteEncodedFrame(Stream stream, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > MaxFrame) throw new InvalidDataException("Binary frame exceeds size limit");
        Span<byte> prefix = stackalloc byte[4];
        Span<byte> checksum = stackalloc byte[32];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        SHA256.HashData(payload, checksum);
        stream.Write(prefix); stream.Write(checksum); stream.Write(payload);
    }
    public static object? ReadFrame(Stream stream, out bool end)
    {
        Span<byte> prefix = stackalloc byte[4];
        var first = stream.ReadByte();
        end = first < 0;
        if (end) return null;
        prefix[0] = (byte)first; stream.ReadExactly(prefix[1..]);
        var size = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (size < 0 || size > MaxFrame) throw new InvalidDataException("Invalid binary frame size");
        var checksum = new byte[32]; stream.ReadExactly(checksum);
        var payload = new byte[size]; stream.ReadExactly(payload);
        if (!CryptographicOperations.FixedTimeEquals(checksum, SHA256.HashData(payload))) throw new InvalidDataException("Binary frame checksum mismatch");
        return Decode(payload);
    }
    public static void RequireHeader(Stream stream)
    {
        var header = new byte[Magic.Length]; stream.ReadExactly(header);
        if (!header.AsSpan().SequenceEqual(Magic)) throw new InvalidDataException("Unsupported binary evidence header");
    }
    public static void WriteJsonRecords(string path, IEnumerable<string> records)
    {
        RequireBinaryPath(path);
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        output.Write(Magic);
        WriteJsonRecords(output, records);
    }
    public static void WriteRecords(string path, IEnumerable<object?> records)
    {
        RequireBinaryPath(path);
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        output.Write(Magic);
        foreach (var record in records) WriteFrame(output, record);
    }
    public static void AppendJsonRecords(string path, string records)
    {
        RequireBinaryPath(path);
        using var output = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        if (output.Length == 0) output.Write(Magic);
        else { RequireHeader(output); output.Position = output.Length; }
        WriteJsonRecords(output, records.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }
    private static void WriteJsonRecords(Stream stream, IEnumerable<string> records)
    {
        foreach (var record in records)
        {
            using var document = JsonDocument.Parse(record);
            WriteFrame(stream, document.RootElement);
        }
    }
}

}

// --- Source: EvidenceOutput.cs ---
namespace DuelView.Compiled
{


// Shared runtime implementation; state remains owned by the execution context.
public static partial class R
{
    private static readonly object OutputLock = new();
    private static readonly Stream StandardOutput = Console.OpenStandardOutput();
    private static readonly byte[] NewLineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);
    // Same value encoding as json_stringify, without a whole-packet UTF-16 copy.
    private static bool binaryOutputHeader;
    private static bool binaryRecording { get => Caches.BinaryRecording; set => Caches.BinaryRecording = value; }
    public static object json_print(object value)
    {
        lock (OutputLock)
        {
            if (binaryRecording || Environment.GetEnvironmentVariable("DV_BINARY_STDOUT") == "1")
            {
                if (!binaryOutputHeader) { StandardOutput.Write(BinaryEvidence.Magic); binaryOutputHeader = true; }
                // Projection packets are bounded values; exact atoms remain tagged.
                BinaryEvidence.WriteFrame(StandardOutput, BinaryProjection(value)); StandardOutput.Flush(); return true;
            }
            using var writer = new Utf8JsonWriter(StandardOutput, new JsonWriterOptions { SkipValidation = true });
            WriteJsonFast(writer, value);
            writer.Flush();
            StandardOutput.Write(NewLineBytes);
            StandardOutput.Flush();
        }
        return true;
    }

    // Opt-in evidence I/O only. The disabled hook never traverses the supplied value.
    // Values are frozen before registration; neither recording nor references mutate them.
    private static object BinaryProjection(object value)
    {
        if (value is Rational exact) return exact.ToString();
        if (TryItems(value, out var items)) return items.Select(BinaryProjection).ToList();
        return value;
    }
    private static RawDagWriter? RawEvidence { get => Caches.RawEvidence; set => Caches.RawEvidence = value; }
    private static object RawLock => Caches.RawLock;
    public static object raw_open(object path)
    {
        if (!AuditMode) throw new InvalidOperationException("Raw evidence requires audit mode");
        lock (RawLock)
        {
            if (RawEvidence is not null) throw new InvalidOperationException("Raw recorder already opened; abort a failed recorder before reopening");
            RawEvidence = new RawDagWriter(Text(path));
            binaryRecording = true;
            return true;
        }
    }
    public static object raw_event(object value)
    {
        if (!AuditMode || RawRecordingContext.Value is false) return true;
        lock (RawLock)
        {
            if (RawEvidence is null) return false;
            RawEvidence.Event(value);
            return true;
        }
    }
    public static object raw_checkpoint(object name, object value)
    {
        lock (RawLock)
        {
            if (RawEvidence is null) return false;
            RawEvidence.Checkpoint(Text(name), value);
            return true;
        }
    }
    public static object raw_finish(object value)
    {
        lock (RawLock)
        {
            if (RawEvidence is null) return false;
            RawEvidence.Finish(value);
            RawEvidence = null;
            return true;
        }
    }
    // Explicit recovery only: preserve the partial file, publish no seal/final name.
    public static object raw_abort()
    {
        lock (RawLock)
        {
            if (RawEvidence is null) return false;
            RawEvidence.Abort();
            RawEvidence = null;
            return true;
        }
    }
    public static object raw_ref(object value)
    {
        lock (RawLock)
        {
            if (RawEvidence is null || !IsDvList(value)) return false;
            return RawEvidence.Reference(value);
        }
    }

    private sealed record RawNode(long Id, long Frame);
    private sealed class RawDagWriter
    {
        private readonly long clockStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        private readonly double startupMilliseconds = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalMilliseconds;
        private readonly string destination;
        private readonly string partial;
        private readonly FileStream stream;
        private readonly ConditionalWeakTable<FrozenList, RawNode> nodes = new();
        private readonly HashSet<string> eventIds = new(StringComparer.Ordinal);
        private readonly BinaryEvidence.Encoder frameBuffer = new();
        private readonly IncrementalHash chainDigest;
        private readonly byte[] previous = Encoding.ASCII.GetBytes(new string('0', 64));
        private long nextNode, sequence, flushedSequence, eventCount;
        private int payloadOffset;
        private bool finished, failed;
        internal RawDagWriter(string path)
        {
            BinaryEvidence.RequireBinaryPath(path);
            destination = Path.GetFullPath(path); partial = destination + ".partial";
            if (File.Exists(destination)) throw new IOException("Evidence destination already exists: " + destination);
            stream = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 1024);
            try
            {
                chainDigest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                stream.Write(BinaryEvidence.Magic);
                BeginFrame("header", 5);
                frameBuffer.Text("format"); frameBuffer.Text("dv-raw-dag-v2");
                frameBuffer.Text("value_encoding"); frameBuffer.Text("typed binary atoms and prior node references");
                frameBuffer.Text("checksum"); frameBuffer.Text("sha256(previous-ascii + LF + sequence-ascii + LF + binary-payload)");
                frameBuffer.Text("clock_origin"); frameBuffer.Text("process-start UTC anchor plus monotonic elapsed; event commit emission, includes initialization");
                CompleteFrame();
                FlushCommit();
            }
            catch { chainDigest?.Dispose(); stream.Dispose(); throw; }
        }
        private void RequireOpen()
        {
            if (finished || failed) throw new InvalidOperationException("Raw recorder is closed or failed");
        }
        private void FlushCommit()
        {
            if (flushedSequence == sequence) return;
            try { stream.Flush(); flushedSequence = sequence; }
            catch { failed = true; throw; }
        }
        private void BeginFrame(string kind, int fields)
        {
            RequireOpen();
            frameBuffer.Clear();
            frameBuffer.Map(4);
            frameBuffer.Text("seq"); frameBuffer.Integer(sequence);
            frameBuffer.Text("prev"); frameBuffer.Utf8Text(previous);
            frameBuffer.Text("payload");
            payloadOffset = frameBuffer.Count;
            frameBuffer.Map(fields);
            frameBuffer.Text("kind"); frameBuffer.Text(kind);
        }
        private long CompleteFrame()
        {
            try
            {
                Span<byte> number = stackalloc byte[20];
                System.Buffers.Text.Utf8Formatter.TryFormat(sequence, number, out var numberLength);
                chainDigest.AppendData(previous);
                chainDigest.AppendData("\n"u8);
                chainDigest.AppendData(number[..numberLength]);
                chainDigest.AppendData("\n"u8);
                chainDigest.AppendData(frameBuffer.WrittenSpan[payloadOffset..]);
                Span<byte> checksum = stackalloc byte[32];
                if (!chainDigest.TryGetHashAndReset(checksum, out var checksumLength) || checksumLength != checksum.Length)
                    throw new InvalidDataException("Invalid DAG digest length");
                Span<byte> text = stackalloc byte[64];
                ReadOnlySpan<byte> hex = "0123456789abcdef"u8;
                for (var i = 0; i < checksum.Length; i++)
                {
                    text[i * 2] = hex[checksum[i] >> 4];
                    text[i * 2 + 1] = hex[checksum[i] & 15];
                }
                frameBuffer.Text("sha256"); frameBuffer.Utf8Text(text);
                BinaryEvidence.WriteEncodedFrame(stream, frameBuffer.WrittenSpan);
                text.CopyTo(previous);
                return sequence++;
            }
            catch { failed = true; throw; }
        }
        private RawNode Node(object value)
        {
            RequireOpen();
            if (!IsDvList(value)) throw new ArgumentException("Raw DAG roots must be DV lists/records");
            return FrozenNode((FrozenList)snapshot(value));
        }
        private RawNode FrozenNode(FrozenList frozen)
        {
            if (nodes.TryGetValue(frozen, out var known)) return known;
            try
            {
                // Define children before starting this parent's envelope. The
                // second pass only reads writer-local identities; it allocates
                // no reference dictionaries or duplicate payload tree.
                for (var i = 0; i < frozen.Count; i++)
                    if (frozen[i] is FrozenList child) FrozenNode(child);
                var id = nextNode++;
                BeginFrame("node", 3);
                frameBuffer.Text("node_id"); frameBuffer.Integer(id);
                frameBuffer.Text("items"); frameBuffer.List(frozen.Count);
                for (var i = 0; i < frozen.Count; i++)
                {
                    var item = frozen[i];
                    if (item is FrozenList child)
                    {
                        if (!nodes.TryGetValue(child, out var node)) throw new InvalidDataException("Missing prior DAG identity");
                        frameBuffer.Map(1); frameBuffer.Text("node"); frameBuffer.Integer(node.Id);
                    }
                    else if (item is Rational exact)
                    {
                        frameBuffer.Map(1); frameBuffer.Text("rational"); frameBuffer.Text(exact.ToString());
                    }
                    else frameBuffer.Value(item, 3);
                }
                var frame = CompleteFrame();
                var result = new RawNode(id, frame); nodes.Add(frozen, result); return result;
            }
            catch { failed = true; throw; }
        }
        internal void Event(object value)
        {
            var frozen = snapshot(value); var id = Text(record_find(frozen, "id", false));
            if (!eventIds.Add(id)) { failed = true; throw new InvalidOperationException("Repeated raw event ID: " + id); }
            var node = Node(frozen);
            try
            {
                BeginFrame("event", 5);
                frameBuffer.Text("event_id"); frameBuffer.Text(id);
                frameBuffer.Text("root"); frameBuffer.Integer(node.Id);
                frameBuffer.Text("ordinal"); frameBuffer.Integer(eventCount);
                frameBuffer.Text("process_elapsed_ms"); frameBuffer.Value(startupMilliseconds + System.Diagnostics.Stopwatch.GetElapsedTime(clockStarted).TotalMilliseconds, 0);
                CompleteFrame();
                FlushCommit(); eventCount++;
            }
            catch { failed = true; throw; }
        }
        internal void Checkpoint(string name, object value)
        {
            var node = Node(value);
            try
            {
                BeginFrame("checkpoint", 4);
                frameBuffer.Text("name"); frameBuffer.Text(name);
                frameBuffer.Text("root"); frameBuffer.Integer(node.Id);
                frameBuffer.Text("events"); frameBuffer.Integer(eventCount);
                CompleteFrame(); FlushCommit();
            }
            catch { failed = true; throw; }
        }
        internal object Reference(object value)
        {
            var node = Node(value); FlushCommit();
            return snapshot(new List<object>
            {
                new List<object> { "format", "dv-raw-dag-v2" },
                new List<object> { "journal", Path.GetFileName(destination) },
                new List<object> { "partial_journal", Path.GetFileName(partial) },
                new List<object> { "node_id", node.Id.ToString(CultureInfo.InvariantCulture) },
                new List<object> { "definition_frame", node.Frame.ToString(CultureInfo.InvariantCulture) }
            });
        }
        internal void Finish(object value)
        {
            Checkpoint("final", value);
            try
            {
                BeginFrame("seal", 4);
                frameBuffer.Text("events"); frameBuffer.Integer(eventCount);
                frameBuffer.Text("nodes"); frameBuffer.Integer(nextNode);
                frameBuffer.Text("final_checkpoint"); frameBuffer.Flag(true);
                CompleteFrame();
                FlushCommit(); stream.Dispose(); chainDigest.Dispose(); File.Move(partial, destination); finished = true;
            }
            catch { failed = true; stream.Dispose(); chainDigest.Dispose(); throw; }
        }
        internal void Abort() { failed = true; stream.Dispose(); chainDigest.Dispose(); }
    }
    public static void WriteEvidenceFile(string path, object value)
    {
        var writer = new RawDagWriter(path);
        try { writer.Checkpoint("initial", value); writer.Finish(value); }
        catch { writer.Abort(); throw; }
    }
    public static object ReadEvidenceFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        BinaryEvidence.RequireHeader(stream);
        var values = new List<object>(); var previous = new string('0', 64); long sequence = 0;
        object? final = null; bool sealedFile = false, initial = false;
        while (true)
        {
            var raw = BinaryEvidence.ReadFrame(stream, out var end); if (end) break;
            if (sealedFile) throw new InvalidDataException("Frames after final seal");
            var envelope = raw as Dictionary<string, object?> ?? throw new InvalidDataException("Invalid DAG envelope");
            if (envelope.Count != 4 || (long)envelope["seq"]! != sequence || (string)envelope["prev"]! != previous)
                throw new InvalidDataException("DAG sequence/checksum predecessor mismatch");
            var payload = (Dictionary<string, object?>)envelope["payload"]!;
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            digest.AppendData(Encoding.ASCII.GetBytes(previous + "\n" + sequence.ToString(CultureInfo.InvariantCulture) + "\n"));
            digest.AppendData(BinaryEvidence.Encode(payload));
            var checksum = Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
            if ((string)envelope["sha256"]! != checksum) throw new InvalidDataException("DAG checksum mismatch");
            previous = checksum; var kind = (string)payload["kind"]!;
            if (sequence++ == 0)
            {
                if (kind != "header" || (string)payload["format"]! != "dv-raw-dag-v2") throw new InvalidDataException("Unsupported DAG header");
                continue;
            }
            if (final is not null && kind != "seal") throw new InvalidDataException("Unsealed final checkpoint");
            if (kind == "node")
            {
                if ((long)payload["node_id"]! != values.Count) throw new InvalidDataException("Invalid DAG node order");
                var items = (List<object?>)payload["items"]!; var value = new List<object>();
                foreach (var item in items)
                {
                    if (item is Dictionary<string, object?> atom)
                    {
                        if (atom.Count != 1) throw new InvalidDataException("Invalid DAG atom");
                        if (atom.TryGetValue("node", out var reference))
                        {
                            var index = (long)reference!;
                            if (index < 0 || index >= values.Count) throw new InvalidDataException("Missing DAG node");
                            value.Add(values[(int)index]);
                        }
                        else if (atom.TryGetValue("rational", out var exact)) value.Add(Rational.From((string)exact!));
                        else throw new InvalidDataException("Unknown DAG atom");
                    }
                    else value.Add(item is long integer ? (object)(double)integer : item ?? throw new InvalidDataException("Null DV atom"));
                }
                values.Add(snapshot(value));
            }
            else if (kind == "checkpoint")
            {
                var root = (long)payload["root"]!;
                if (root < 0 || root >= values.Count || (long)payload["events"]! != 0) throw new InvalidDataException("Invalid value-file checkpoint");
                initial |= (string)payload["name"]! == "initial";
                if ((string)payload["name"]! == "final") final = values[(int)root];
            }
            else if (kind == "seal")
            {
                if (!initial || final is null || payload["final_checkpoint"] is not true || (long)payload["nodes"]! != values.Count || (long)payload["events"]! != 0)
                    throw new InvalidDataException("Invalid binary value seal");
                sealedFile = true;
            }
            else throw new InvalidDataException("Unsupported frame in binary value file");
        }
        return sealedFile && final is not null ? final : throw new InvalidDataException("Incomplete binary value file");
    }
    public static object json_write(object path, object value) => throw new InvalidOperationException("JSON recording is forbidden; use raw DAG binary evidence");
}
}
