using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>World / Laws: native Core mechanisms.</summary>
public static partial class DVKernel
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
