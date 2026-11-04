using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Cognition / Proofs: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dv_proof_step(object v_id, object v_kind, object v_conclusion, object v_parents, object v_law)
    {
        return F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("id", v_id), F_dv_pair("kind", v_kind), F_dv_pair("conclusion", v_conclusion), F_dv_pair("parents", v_parents) }, "law", v_law);
    }

    public static object F_dv_cost_policy(object v_step, object v_evidence, object v_assumption, object v_conflict, object v_reference, object v_maximum, object v_allow_assumptions, object v_budget)
    {
        _ = F_dv_require(F_dv_and(F_dv_and(R.not(R.lt(v_step, R.number("0"))), R.not(R.lt(v_evidence, R.number("0")))), F_dv_and(F_dv_and(R.lt(R.number("0"), v_assumption), R.not(R.lt(v_conflict, R.number("0")))), F_dv_and(R.lt(R.number("0"), v_reference), R.not(R.lt(v_maximum, R.number("0")))))), "Invalid reasoning costs");
        _ = F_dv_require(R.lt(R.number("0"), v_budget), "Invalid proof budget");
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("step", v_step), F_dv_pair("evidence", v_evidence), F_dv_pair("assumption", v_assumption), F_dv_pair("conflict", v_conflict) }, new System.Collections.Generic.List<object>(4) { F_dv_pair("reference", v_reference), F_dv_pair("maximum", v_maximum), F_dv_pair("allow_assumptions", v_allow_assumptions), F_dv_pair("budget", v_budget) });
    }

    public static object F_dv_rule_concludes(object v_law, object v_conclusion)
    {
        object v_effects__106537 = F_dv_get(v_law, "effects");
        object v_i__106567 = R.number("0");
        object v_found__106581 = false;
        while (R.Bool(R.lt(v_i__106567, R.count(v_effects__106537))))
        {
            object v_effect__106642 = R.at(v_effects__106537, v_i__106567);
            if (R.Bool(R.eq(F_dv_get(v_effect__106642, "op"), "assign")))
            {
                if (R.Bool(F_dv_same(v_conclusion, F_dv_fact(F_dv_get(v_effect__106642, "ref"), F_dv_get(v_effect__106642, "value")))))
                {
                    v_found__106581 = true;
                }
            }
            v_i__106567 = R.add(v_i__106567, R.number("1"));
        }
        return v_found__106581;
    }

    public static object F_dv_match_premises(object v_premise, object v_supplied)
    {
        if (R.Bool(F_dv_same(v_premise, F_dv_true())))
        {
            return R.eq(R.count(v_supplied), R.number("0"));
        }
        else
        {
            if (R.Bool(R.eq(F_dv_get(v_premise, "op"), "all")))
            {
                object v_required__107014 = F_dv_get(v_premise, "items");
                object v_i__107055 = R.number("0");
                object v_valid__107077 = R.eq(R.count(v_supplied), R.count(v_required__107014));
                while (R.Bool(R.lt(v_i__107055, R.count(v_required__107014))))
                {
                    v_valid__107077 = F_dv_and(v_valid__107077, F_dv_has(v_supplied, R.at(v_required__107014, v_i__107055)));
                    v_i__107055 = R.add(v_i__107055, R.number("1"));
                }
                return v_valid__107077;
            }
            else
            {
                return F_dv_same(v_supplied, F_dv_single(v_premise));
            }
        }
    }

    public static object F_dv_check_step(object v_world, object v_step, object v_checked, object v_policy)
    {
        object v_kind__107422 = F_dv_get(v_step, "kind");
        object v_conclusion__107447 = F_dv_get(v_step, "conclusion");
        object v_parents__107484 = F_dv_get(v_step, "parents");
        object v_supplied__107515 = new System.Collections.Generic.List<object>(0) { };
        object v_confidence__107537 = R.number("1");
        object v_i__107560 = R.number("0");
        object v_hypothetical__107574 = false;
        while (R.Bool(R.lt(v_i__107560, R.count(v_parents__107484))))
        {
            object v_parent__107642 = F_dv_get(v_checked, R.at(v_parents__107484, v_i__107560));
            _ = R.push(v_supplied__107515, F_dv_get(v_parent__107642, "conclusion"));
            v_confidence__107537 = F_dv_min(v_confidence__107537, F_dv_get(v_parent__107642, "confidence"));
            v_hypothetical__107574 = F_dv_or(v_hypothetical__107574, F_dv_get(v_parent__107642, "hypothetical"));
            v_i__107560 = R.add(v_i__107560, R.number("1"));
        }
        object v_assumptions__107875 = R.number("0");
        object v_conflicts__107899 = R.number("0");
        object v_local_uncertainty__107921 = R.number("0");
        if (R.Bool(R.eq(v_kind__107422, "known")))
        {
            _ = F_dv_require(R.eq(R.count(v_parents__107484), R.number("0")), "Known premise cannot hide parents");
            _ = F_dv_require(R.eq(F_dv_truth(v_world, v_conclusion__107447), "true"), "Conclusion is not available in this view");
            v_confidence__107537 = F_dv_premise_confidence(v_world, v_conclusion__107447);
            v_local_uncertainty__107921 = R.sub(R.number("1"), v_confidence__107537);
        }
        else
        {
            if (R.Bool(R.eq(v_kind__107422, "assume")))
            {
                _ = F_dv_require(R.eq(R.count(v_parents__107484), R.number("0")), "Assumption cannot hide parents");
                _ = F_dv_require(F_dv_and(F_dv_get(v_policy, "allow_assumptions"), R.not(R.eq(F_dv_get(v_world, "domain"), "physical"))), "Assumption not permitted");
                v_hypothetical__107574 = true;
                v_assumptions__107875 = R.number("1");
                v_confidence__107537 = R.number("0");
                if (R.Bool(R.eq(F_dv_truth(v_world, v_conclusion__107447), "false")))
                {
                    v_conflicts__107899 = R.number("1");
                }
            }
            else
            {
                if (R.Bool(R.eq(v_kind__107422, "axiom")))
                {
                    object v_adoption__108749 = F_dv_get(F_dv_get(v_world, "adoptions"), F_dv_get(v_step, "law"));
                    object v_law__108814 = F_dv_get(v_adoption__108749, "law");
                    _ = F_dv_require(F_dv_get(v_adoption__108749, "active"), "Proof uses a suspended axiom");
                    _ = F_dv_require(R.eq(F_dv_get(v_adoption__108749, "mode"), "infer"), "Operational effects do not prove that an event occurred");
                    _ = F_dv_require(F_dv_rule_concludes(v_law__108814, v_conclusion__107447), "Axiom does not conclude this proposition");
                    _ = F_dv_require(F_dv_match_premises(F_dv_get(v_law__108814, "premise"), v_supplied__107515), "Axiom premises are not established");
                    v_confidence__107537 = F_dv_min(v_confidence__107537, F_dv_get(v_adoption__108749, "confidence"));
                    v_local_uncertainty__107921 = R.sub(R.number("1"), F_dv_get(v_adoption__108749, "confidence"));
                }
                else
                {
                    _ = R.fail("Unsupported proof step");
                }
            }
        }
        return F_dv_with_many(F_dv_clone(v_step), new System.Collections.Generic.List<object>(5) { F_dv_pair("confidence", v_confidence__107537), F_dv_pair("hypothetical", v_hypothetical__107574), F_dv_pair("assumptions", v_assumptions__107875), F_dv_pair("conflicts", v_conflicts__107899), F_dv_pair("local_uncertainty", v_local_uncertainty__107921) });
    }

    public static object F_dv_judge_conclusion(object v_world, object v_conclusion, object v_proof, object v_policy)
    {
        _ = F_dv_require(R.lt(R.number("0"), R.count(v_proof)), "Empty derivation");
        _ = F_dv_require(R.not(R.lt(F_dv_get(v_policy, "budget"), R.count(v_proof))), "Derivation budget exhausted");
        object v_checked__109838 = new System.Collections.Generic.List<object>(0) { };
        object v_i__109859 = R.number("0");
        while (R.Bool(R.lt(v_i__109859, R.count(v_proof))))
        {
            object v_step__109910 = R.at(v_proof, v_i__109859);
            object v_id__109938 = F_dv_get(v_step__109910, "id");
            _ = F_dv_require(R.not(F_dv_has_key(v_checked__109838, v_id__109938)), "Repeated proof node identity");
            v_checked__109838 = F_dv_with(v_checked__109838, v_id__109938, F_dv_check_step(v_world, v_step__109910, v_checked__109838, v_policy));
            v_i__109859 = R.add(v_i__109859, R.number("1"));
        }
        object v_root__110145 = F_dv_get(v_checked__109838, F_dv_get(R.at(v_proof, R.sub(R.count(v_proof), R.number("1"))), "id"));
        object v_queue__110209 = F_dv_single(F_dv_get(v_root__110145, "id"));
        object v_visited__110244 = new System.Collections.Generic.List<object>(0) { };
        object v_cursor__110265 = R.number("0");
        object v_steps__110284 = R.number("0");
        object v_uncertainty__110302 = R.number("0");
        object v_assumptions__110326 = R.number("0");
        object v_conflicts__110350 = R.number("0");
        _ = F_dv_require(F_dv_same(v_conclusion, F_dv_get(v_root__110145, "conclusion")), "Derivation concludes another proposition");
        while (R.Bool(R.lt(v_cursor__110265, R.count(v_queue__110209))))
        {
            object v_id__110511 = R.at(v_queue__110209, v_cursor__110265);
            if (R.Bool(F_dv_has(v_visited__110244, v_id__110511)))
            {
                _ = false;
            }
            else
            {
                object v_step__110610 = F_dv_get(v_checked__109838, v_id__110511);
                object v_parents__110653 = F_dv_get(v_step__110610, "parents");
                object v_p__110692 = R.number("0");
                _ = R.push(v_visited__110244, v_id__110511);
                v_steps__110284 = R.add(v_steps__110284, R.number("1"));
                v_uncertainty__110302 = R.add(v_uncertainty__110302, F_dv_get(v_step__110610, "local_uncertainty"));
                v_assumptions__110326 = R.add(v_assumptions__110326, F_dv_get(v_step__110610, "assumptions"));
                v_conflicts__110350 = R.add(v_conflicts__110350, F_dv_get(v_step__110610, "conflicts"));
                while (R.Bool(R.lt(v_p__110692, R.count(v_parents__110653))))
                {
                    _ = R.push(v_queue__110209, R.at(v_parents__110653, v_p__110692));
                    v_p__110692 = R.add(v_p__110692, R.number("1"));
                }
            }
            v_cursor__110265 = R.add(v_cursor__110265, R.number("1"));
        }
        object v_cost__111092 = R.add(R.add(R.mul(v_steps__110284, F_dv_get(v_policy, "step")), R.mul(v_uncertainty__110302, F_dv_get(v_policy, "evidence"))), R.add(R.mul(v_assumptions__110326, F_dv_get(v_policy, "assumption")), R.mul(v_conflicts__110350, F_dv_get(v_policy, "conflict"))));
        object v_components__111237 = new System.Collections.Generic.List<object>(4) { F_dv_pair("steps", v_steps__110284), F_dv_pair("evidence_uncertainty", v_uncertainty__110302), F_dv_pair("assumptions", v_assumptions__110326), F_dv_pair("conflicts", v_conflicts__110350) };
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("valid", true), F_dv_pair("cost", v_cost__111092), F_dv_pair("components", v_components__111237), F_dv_pair("reasonable", R.not(R.lt(F_dv_get(v_policy, "maximum"), v_cost__111092))) }, new System.Collections.Generic.List<object>(5) { F_dv_pair("reasonableness", R.div(R.number("1"), R.add(R.number("1"), R.div(v_cost__111092, F_dv_get(v_policy, "reference"))))), F_dv_pair("confidence", F_dv_get(v_root__110145, "confidence")), F_dv_pair("hypothetical", F_dv_or(F_dv_get(v_root__110145, "hypothetical"), R.eq(F_dv_get(v_world, "domain"), "prediction"))), F_dv_pair("scope", new System.Collections.Generic.List<object>(3) { F_dv_pair("world", F_dv_get(v_world, "id")), F_dv_pair("subject", F_dv_get(v_world, "subject")), F_dv_pair("revision", F_dv_get(v_world, "revision")) }), F_dv_pair("basis", v_visited__110244) });
    }
}
