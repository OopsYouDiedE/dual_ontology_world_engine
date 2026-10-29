using DuelView.Compiled;

namespace DuelView.Core;

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
