using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Planning / Factors: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_factor_proof_result(object v_valid, object v_remaining, object v_confidence, object v_steps, object v_roots)
    {
        return F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("valid", v_valid), F_dv_pair("remaining", v_remaining), F_dv_pair("confidence", v_confidence), F_dv_pair("steps", v_steps) }, "source_events", v_roots);
    }

    public static object F_dvs_factor_merge(object v_left, object v_right)
    {
        object v_steps__200607 = F_dv_clone(F_dv_get(v_left, "steps"));
        object v_roots__200644 = F_dv_clone(F_dv_get(v_left, "source_events"));
        object v_i__200689 = R.number("0");
        while (R.Bool(R.lt(v_i__200689, R.count(F_dv_get(v_right, "steps")))))
        {
            _ = R.push(v_steps__200607, R.at(F_dv_get(v_right, "steps"), v_i__200689));
            v_i__200689 = R.add(v_i__200689, R.number("1"));
        }
        v_i__200689 = R.number("0");
        while (R.Bool(R.lt(v_i__200689, R.count(F_dv_get(v_right, "source_events")))))
        {
            if (R.Bool(F_dv_has(v_roots__200644, R.at(F_dv_get(v_right, "source_events"), v_i__200689))))
            {
                _ = false;
            }
            else
            {
                _ = R.push(v_roots__200644, R.at(F_dv_get(v_right, "source_events"), v_i__200689));
            }
            v_i__200689 = R.add(v_i__200689, R.number("1"));
        }
        return F_dvs_factor_proof_result(F_dv_and(F_dv_get(v_left, "valid"), F_dv_get(v_right, "valid")), F_dv_get(v_right, "remaining"), F_dv_min(F_dv_get(v_left, "confidence"), F_dv_get(v_right, "confidence")), v_steps__200607, v_roots__200644);
    }

    public static object F_dvs_factor_law_allowed(object v_allowed, object v_law)
    {
        object v_i__201195 = R.number("0");
        object v_found__201209 = false;
        while (R.Bool(R.lt(v_i__201195, R.count(v_allowed))))
        {
            if (R.Bool(F_dv_and(R.eq(F_dv_get(R.at(v_allowed, v_i__201195), "id"), F_dv_get(v_law, "name")), R.eq(F_dv_get(R.at(v_allowed, v_i__201195), "version"), F_dv_get(v_law, "version")))))
            {
                v_found__201209 = true;
            }
            v_i__201195 = R.add(v_i__201195, R.number("1"));
        }
        return v_found__201209;
    }

    public static object F_dvs_factor_roots(object v_actor, object v_mind, object v_ref, object v_events, object v_cutoff, object v_remaining, object v_allowed)
    {
        object v_cell__201489 = F_dv_read(v_mind, v_ref);
        object v_result__201523 = F_dvs_factor_proof_result(false, v_remaining, R.number("0"), new System.Collections.Generic.List<object>(0) { }, new System.Collections.Generic.List<object>(0) { });
        if (R.Bool(R.not(R.lt(v_remaining, R.number("1")))))
        {
            v_result__201523 = F_dvs_factor_proof_result(false, R.sub(v_remaining, R.number("1")), R.number("0"), new System.Collections.Generic.List<object>(0) { }, new System.Collections.Generic.List<object>(0) { });
            if (R.Bool(F_dv_and(F_dv_uncontested(v_cell__201489), F_dv_and(R.lt(R.number("0"), F_dv_get(v_cell__201489, "confidence")), F_dv_and(R.not(F_dv_find(v_cell__201489, "estimated", false)), R.not(R.eq(F_dv_find(v_cell__201489, "source", ""), "abduction")))))))
            {
                object v_support_index__202046 = F_dvs_support_event_index(v_events, F_dv_get(v_actor, "id"));
                object v_positions__202120 = F_dv_find(F_dv_get(v_support_index__202046, "refs"), F_dvs_support_ref_key(F_dv_get(v_actor, "id"), v_ref), new System.Collections.Generic.List<object>(0) { });
                object v_candidate_index__202216 = R.number("0");
                while (R.Bool(F_dv_and(R.lt(v_candidate_index__202216, R.count(v_positions__202120)), R.not(F_dv_get(v_result__201523, "valid")))))
                {
                    object v_i__202343 = R.at(v_positions__202120, v_candidate_index__202216);
                    if (R.Bool(R.not(R.lt(v_i__202343, v_cutoff))))
                    {
                        return v_result__201523;
                    }
                    object v_event__202460 = R.at(v_events, v_i__202343);
                    if (R.Bool(R.eq(F_dv_get(v_event__202460, "actor"), F_dv_get(v_actor, "id"))))
                    {
                        object v_kind__202548 = F_dv_get(v_event__202460, "type");
                        object v_data__202590 = F_dv_get(v_event__202460, "data");
                        if (R.Bool(F_dv_or(R.eq(v_kind__202548, "observation"), R.eq(v_kind__202548, "listener_parse"))))
                        {
                            if (R.Bool(F_dv_has_key(v_data__202590, "mind_after")))
                            {
                                if (R.Bool(F_dv_same(F_dv_read(F_dv_get(v_data__202590, "mind_after"), v_ref), v_cell__201489)))
                                {
                                    object v_observations__202863 = F_dv_find(v_data__202590, "observations", new System.Collections.Generic.List<object>(0) { });
                                    object v_j__202948 = R.number("0");
                                    object v_claims__202990 = F_dv_find(v_cell__201489, "claims", new System.Collections.Generic.List<object>(0) { });
                                    object v_valid__203063 = true;
                                    object v_confidence__203112 = F_dv_get(v_cell__201489, "confidence");
                                    object v_roots__203177 = new System.Collections.Generic.List<object>(0) { };
                                    object v_steps__203224 = new System.Collections.Generic.List<object>(0) { };
                                    if (R.Bool(R.eq(R.count(v_claims__202990), R.number("0"))))
                                    {
                                        v_valid__203063 = false;
                                    }
                                    while (R.Bool(R.lt(v_j__202948, R.count(v_claims__202990))))
                                    {
                                        object v_claim__203447 = R.at(v_claims__202990, v_j__202948);
                                        object v_source__203505 = F_dv_get(R.at(v_claims__202990, v_j__202948), "source");
                                        if (R.Bool(F_dv_and(F_dv_has(v_observations__202863, v_claim__203447), F_dv_and(R.eq(F_dv_get(v_claim__203447, "subject"), F_dv_get(v_actor, "id")), F_dv_and(F_dv_same_local_ref(F_dv_get(v_actor, "id"), F_dv_get(v_claim__203447, "ref"), v_ref), F_dv_and(F_dv_has(F_dv_get(F_dv_get(v_data__202590, "mind_after"), "seen"), F_dv_get(v_claim__203447, "id")), F_dv_or(R.eq(v_source__203505, "perception"), F_dv_or(R.eq(v_source__203505, "recall"), R.eq(v_source__203505, "testimony")))))))))
                                        {
                                            v_confidence__203112 = F_dv_min(v_confidence__203112, F_dv_get(v_claim__203447, "trust"));
                                        }
                                        else
                                        {
                                            v_valid__203063 = false;
                                        }
                                        v_j__202948 = R.add(v_j__202948, R.number("1"));
                                    }
                                    if (R.Bool(F_dv_and(v_valid__203063, R.lt(R.number("0"), v_confidence__203112))))
                                    {
                                        _ = R.push(v_roots__203177, F_dv_get(v_event__202460, "id"));
                                        _ = R.push(v_steps__203224, F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("kind", "observation"), F_dv_pair("event", F_dv_get(v_event__202460, "id")), F_dv_pair("ref", v_ref), F_dv_pair("cell", v_cell__201489) }, "observations", v_claims__202990));
                                        v_result__201523 = F_dvs_factor_proof_result(true, F_dv_get(v_result__201523, "remaining"), v_confidence__203112, v_steps__203224, v_roots__203177);
                                    }
                                }
                            }
                        }
                        if (R.Bool(R.eq(v_kind__202548, "inference")))
                        {
                            v_result__201523 = F_dvs_factor_inference(v_actor, v_mind, v_ref, v_cell__201489, v_events, v_i__202343, v_data__202590, v_event__202460, F_dv_get(v_result__201523, "remaining"), v_allowed);
                        }
                    }
                    v_candidate_index__202216 = R.add(v_candidate_index__202216, R.number("1"));
                }
            }
        }
        return v_result__201523;
    }

    public static object F_dvs_factor_refs_proof(object v_actor, object v_mind, object v_refs, object v_events, object v_cutoff, object v_remaining, object v_allowed)
    {
        object v_result__204800 = F_dvs_factor_proof_result(true, v_remaining, R.number("1"), new System.Collections.Generic.List<object>(0) { }, new System.Collections.Generic.List<object>(0) { });
        object v_i__204869 = R.number("0");
        while (R.Bool(F_dv_and(F_dv_get(v_result__204800, "valid"), R.lt(v_i__204869, R.count(v_refs)))))
        {
            v_result__204800 = F_dvs_factor_merge(v_result__204800, F_dvs_factor_roots(v_actor, v_mind, R.at(v_refs, v_i__204869), v_events, v_cutoff, F_dv_get(v_result__204800, "remaining"), v_allowed));
            v_i__204869 = R.add(v_i__204869, R.number("1"));
        }
        return v_result__204800;
    }

    public static object F_dvs_factor_inference(object v_actor, object v_mind, object v_ref, object v_cell, object v_events, object v_index, object v_data, object v_event, object v_remaining, object v_allowed)
    {
        object v_law__205198 = F_dv_get(v_data, "law");
        object v_before__205221 = F_dv_get(v_data, "mind_before");
        object v_receipt__205255 = F_dv_get(v_data, "receipt");
        object v_current__205286 = F_dv_get(F_dv_get(v_actor, "mind"), "adoptions");
        object v_name__205325 = F_dv_get(F_dv_get(v_data, "law"), "name");
        object v_result__205354 = F_dvs_factor_proof_result(false, v_remaining, R.number("0"), new System.Collections.Generic.List<object>(0) { }, new System.Collections.Generic.List<object>(0) { });
        object v_changes__205424 = F_dv_find(F_dv_get(v_data, "receipt"), "changes", new System.Collections.Generic.List<object>(0) { });
        object v_i__205479 = R.number("0");
        object v_found__205493 = false;
        while (R.Bool(R.lt(v_i__205479, R.count(v_changes__205424))))
        {
            if (R.Bool(F_dv_and(F_dv_same(F_dv_get(R.at(v_changes__205424, v_i__205479), "ref"), v_ref), F_dv_same(F_dv_get(R.at(v_changes__205424, v_i__205479), "after"), v_cell))))
            {
                v_found__205493 = true;
            }
            v_i__205479 = R.add(v_i__205479, R.number("1"));
        }
        if (R.Bool(F_dv_and(v_found__205493, F_dv_and(R.eq(F_dv_get(v_receipt__205255, "status"), "applied"), F_dv_and(F_dvs_factor_law_allowed(v_allowed, v_law__205198), F_dv_and(F_dv_has_key(v_current__205286, v_name__205325), F_dv_has_key(F_dv_get(v_before__205221, "adoptions"), v_name__205325)))))))
        {
            object v_adoption__205859 = F_dv_get(v_current__205286, v_name__205325);
            if (R.Bool(F_dv_and(F_dv_same(v_adoption__205859, F_dv_get(F_dv_get(v_before__205221, "adoptions"), v_name__205325)), F_dv_and(F_dv_get(v_adoption__205859, "active"), F_dv_and(R.eq(F_dv_get(v_adoption__205859, "mode"), "infer"), F_dv_and(F_dv_same(v_law__205198, F_dv_get(v_adoption__205859, "law")), R.eq(F_dv_truth(v_before__205221, F_dv_get(v_law__205198, "premise")), "true")))))))
            {
                if (R.Bool(R.lt(v_remaining, R.number("1"))))
                {
                    _ = false;
                }
                else
                {
                    object v_replayed__206194 = F_dv_infer(v_before__205221, v_name__205325, F_dv_get(v_receipt__205255, "event"), F_dv_get(v_receipt__205255, "time"), F_dv_get(v_before__205221, "revision"));
                    v_result__205354 = F_dvs_factor_proof_result(false, R.sub(v_remaining, R.number("1")), R.number("0"), new System.Collections.Generic.List<object>(0) { }, new System.Collections.Generic.List<object>(0) { });
                    if (R.Bool(F_dv_and(F_dv_same(v_replayed__206194, v_receipt__205255), F_dv_same(F_dv_get(v_data, "mind_after"), F_dv_get(v_receipt__205255, "world")))))
                    {
                        object v_sources__206478 = F_dvs_factor_refs_proof(v_actor, v_before__205221, F_dvs_sim_refs(F_dv_get(v_law__205198, "premise")), v_events, v_index, F_dv_get(v_result__205354, "remaining"), v_allowed);
                        v_result__205354 = v_sources__206478;
                        if (R.Bool(F_dv_and(F_dv_get(v_sources__206478, "valid"), R.lt(R.number("0"), R.count(F_dv_get(v_sources__206478, "source_events"))))))
                        {
                            v_result__205354 = F_dv_with_many(v_sources__206478, new System.Collections.Generic.List<object>(3) { F_dv_pair("source_events", F_dv_append(F_dv_get(v_sources__206478, "source_events"), F_dv_get(v_event, "id"))), F_dv_pair("confidence", F_dv_min(F_dv_get(v_cell, "confidence"), F_dv_min(F_dv_get(v_adoption__205859, "confidence"), F_dv_get(v_sources__206478, "confidence")))), F_dv_pair("steps", F_dv_append(F_dv_get(v_sources__206478, "steps"), F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("kind", "infer"), F_dv_pair("event", F_dv_get(v_event, "id")), F_dv_pair("ref", v_ref), F_dv_pair("cell", v_cell) }, "adoption", v_adoption__205859))) });
                        }
                        else
                        {
                            v_result__205354 = F_dv_with(v_sources__206478, "valid", false);
                        }
                    }
                }
            }
        }
        return v_result__205354;
    }

    public static object F_dvs_factor_domain(object v_domains, object v_id)
    {
        object v_i__207206 = R.number("0");
        object v_result__207220 = false;
        while (R.Bool(R.lt(v_i__207206, R.count(v_domains))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(v_domains, v_i__207206), "id"), v_id)))
            {
                v_result__207220 = R.at(v_domains, v_i__207206);
            }
            v_i__207206 = R.add(v_i__207206, R.number("1"));
        }
        return v_result__207220;
    }

    public static object F_dvs_factor_schema(object v_domains, object v_constraint)
    {
        object v_assignments__207426 = F_dv_get(v_constraint, "assignments");
        object v_scope__207471 = F_dv_get(v_constraint, "scope");
        object v_versions__207504 = F_dv_get(v_constraint, "domain_versions");
        object v_ids__207550 = new System.Collections.Generic.List<object>(0) { };
        object v_facts__207567 = new System.Collections.Generic.List<object>(0) { };
        object v_free__207586 = new System.Collections.Generic.List<object>(0) { };
        object v_valid__207604 = true;
        object v_i__207625 = R.number("0");
        object v_allids__207639 = new System.Collections.Generic.List<object>(0) { };
        object v_refs__207659 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(F_dv_or(R.eq(R.count(v_assignments__207426), R.number("0")), R.eq(R.count(F_dv_find(v_constraint, "basis", new System.Collections.Generic.List<object>(0) { })), R.number("0")))))
        {
            v_valid__207604 = false;
        }
        while (R.Bool(R.lt(v_i__207625, R.count(v_domains))))
        {
            object v_domain__207833 = R.at(v_domains, v_i__207625);
            object v_values__207865 = F_dv_get(R.at(v_domains, v_i__207625), "values");
            object v_j__207904 = R.number("0");
            object v_seen__207922 = new System.Collections.Generic.List<object>(0) { };
            if (R.Bool(F_dv_or(F_dv_has(v_allids__207639, F_dv_get(v_domain__207833, "id")), F_dv_has(v_refs__207659, F_dv_get(v_domain__207833, "ref")))))
            {
                v_valid__207604 = false;
            }
            _ = R.push(v_allids__207639, F_dv_get(v_domain__207833, "id"));
            _ = R.push(v_refs__207659, F_dv_get(v_domain__207833, "ref"));
            if (R.Bool(F_dv_or(R.eq(R.count(v_values__207865), R.number("0")), R.not(R.lt(R.number("0"), F_dv_get(v_domain__207833, "version"))))))
            {
                v_valid__207604 = false;
            }
            while (R.Bool(R.lt(v_j__207904, R.count(v_values__207865))))
            {
                if (R.Bool(F_dv_has(v_seen__207922, R.at(v_values__207865, v_j__207904))))
                {
                    v_valid__207604 = false;
                }
                _ = R.push(v_seen__207922, R.at(v_values__207865, v_j__207904));
                v_j__207904 = R.add(v_j__207904, R.number("1"));
            }
            v_i__207625 = R.add(v_i__207625, R.number("1"));
        }
        v_i__207625 = R.number("0");
        while (R.Bool(R.lt(v_i__207625, R.count(v_assignments__207426))))
        {
            object v_assignment__208445 = R.at(v_assignments__207426, v_i__207625);
            object v_id__208485 = F_dv_get(R.at(v_assignments__207426, v_i__207625), "variable");
            object v_domain__208526 = F_dvs_factor_domain(v_domains, F_dv_get(R.at(v_assignments__207426, v_i__207625), "variable"));
            if (R.Bool(F_dv_has(v_ids__207550, v_id__208485)))
            {
                v_valid__207604 = false;
            }
            _ = R.push(v_ids__207550, v_id__208485);
            if (R.Bool(R.eq(v_domain__208526, false)))
            {
                v_valid__207604 = false;
            }
            else
            {
                if (R.Bool(F_dv_and(F_dv_has(F_dv_get(v_domain__208526, "values"), F_dv_get(v_assignment__208445, "value")), R.eq(F_dv_find(v_versions__207504, v_id__208485, R.sub(R.number("0"), R.number("1"))), F_dv_get(v_domain__208526, "version")))))
                {
                    _ = false;
                }
                else
                {
                    v_valid__207604 = false;
                }
                _ = R.push(v_facts__207567, F_dv_fact(F_dv_get(v_domain__208526, "ref"), F_dv_get(v_assignment__208445, "value")));
            }
            v_i__207625 = R.add(v_i__207625, R.number("1"));
        }
        if (R.Bool(R.eq(R.count(v_scope__207471), R.count(v_ids__207550))))
        {
            _ = false;
        }
        else
        {
            v_valid__207604 = false;
        }
        v_i__207625 = R.number("0");
        while (R.Bool(R.lt(v_i__207625, R.count(v_ids__207550))))
        {
            if (R.Bool(F_dv_has(v_scope__207471, R.at(v_ids__207550, v_i__207625))))
            {
                _ = false;
            }
            else
            {
                v_valid__207604 = false;
            }
            v_i__207625 = R.add(v_i__207625, R.number("1"));
        }
        v_i__207625 = R.number("0");
        while (R.Bool(R.lt(v_i__207625, R.count(v_scope__207471))))
        {
            if (R.Bool(F_dv_has(v_ids__207550, R.at(v_scope__207471, v_i__207625))))
            {
                _ = false;
            }
            else
            {
                v_valid__207604 = false;
            }
            v_i__207625 = R.add(v_i__207625, R.number("1"));
        }
        if (R.Bool(R.eq(R.count(v_facts__207567), R.number("1"))))
        {
            if (R.Bool(F_dv_same(F_dv_get(v_constraint, "condition"), R.at(v_facts__207567, R.number("0")))))
            {
                _ = false;
            }
            else
            {
                v_valid__207604 = false;
            }
        }
        else
        {
            if (R.Bool(F_dv_same(F_dv_get(v_constraint, "condition"), F_dv_all(v_facts__207567))))
            {
                _ = false;
            }
            else
            {
                v_valid__207604 = false;
            }
        }
        v_i__207625 = R.number("0");
        while (R.Bool(R.lt(v_i__207625, R.count(v_domains))))
        {
            if (R.Bool(F_dv_has(v_ids__207550, F_dv_get(R.at(v_domains, v_i__207625), "id"))))
            {
                _ = false;
            }
            else
            {
                _ = R.push(v_free__207586, R.at(v_domains, v_i__207625));
            }
            v_i__207625 = R.add(v_i__207625, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("valid", v_valid__207604), F_dv_pair("fixed", v_assignments__207426), F_dv_pair("free_domains", v_free__207586) };
    }

    public static object F_dvs_factor_scope_proof(object v_domains, object v_fixed, object v_proof)
    {
        object v_steps__209971 = F_dv_get(v_proof, "steps");
        object v_i__209999 = R.number("0");
        object v_valid__210013 = true;
        while (R.Bool(R.lt(v_i__209999, R.count(v_steps__209971))))
        {
            object v_step__210071 = R.at(v_steps__209971, v_i__209999);
            object v_j__210099 = R.number("0");
            while (R.Bool(R.lt(v_j__210099, R.count(v_domains))))
            {
                object v_domain__210160 = R.at(v_domains, v_j__210099);
                object v_k__210196 = R.number("0");
                object v_included__210218 = false;
                if (R.Bool(F_dv_same(F_dv_get(v_step__210071, "ref"), F_dv_get(v_domain__210160, "ref"))))
                {
                    while (R.Bool(R.lt(v_k__210196, R.count(v_fixed))))
                    {
                        if (R.Bool(R.eq(F_dv_get(R.at(v_fixed, v_k__210196), "variable"), F_dv_get(v_domain__210160, "id"))))
                        {
                            v_included__210218 = true;
                        }
                        v_k__210196 = R.add(v_k__210196, R.number("1"));
                    }
                    if (R.Bool(v_included__210218))
                    {
                        _ = false;
                    }
                    else
                    {
                        v_valid__210013 = false;
                    }
                }
                v_j__210099 = R.add(v_j__210099, R.number("1"));
            }
            v_i__209999 = R.add(v_i__209999, R.number("1"));
        }
        return v_valid__210013;
    }

    public static object F_dvs_factor_covers(object v_certificate, object v_assignments)
    {
        object v_fixed__210684 = F_dv_get(F_dv_get(v_certificate, "covered_assignments"), "fixed");
        object v_i__210738 = R.number("0");
        object v_valid__210752 = true;
        while (R.Bool(R.lt(v_i__210738, R.count(v_fixed__210684))))
        {
            if (R.Bool(F_dv_has(v_assignments, R.at(v_fixed__210684, v_i__210738))))
            {
                _ = false;
            }
            else
            {
                v_valid__210752 = false;
            }
            v_i__210738 = R.add(v_i__210738, R.number("1"));
        }
        return v_valid__210752;
    }

    public static object F_dvs_factor_paid(object v_actor, object v_events)
    {
        object v_id__210979 = F_dv_find(v_actor, "model_paid_event", "");
        if (R.Bool(R.eq(v_id__210979, "")))
        {
            return false;
        }
        object v_event__211069 = F_dvs_find_event_by_id(v_events, v_id__210979);
        if (R.Bool(R.eq(v_event__211069, false)))
        {
            return false;
        }
        if (R.Bool(F_dv_and(R.eq(F_dv_get(v_event__211069, "id"), v_id__210979), F_dv_and(R.eq(F_dv_get(v_event__211069, "actor"), F_dv_get(v_actor, "id")), R.eq(F_dv_get(v_event__211069, "type"), "action_execution")))))
        {
            object v_data__211270 = F_dv_get(v_event__211069, "data");
            if (R.Bool(F_dv_and(R.eq(F_dv_get(F_dv_get(v_data__211270, "action"), "kind"), "replay"), F_dv_and(R.eq(F_dv_get(F_dv_get(F_dv_get(v_data__211270, "receipt"), "execution"), "status"), "applied"), R.eq(F_dv_get(F_dv_get(F_dv_get(F_dv_get(F_dv_get(v_data__211270, "receipt"), "ledger"), "receipts"), F_dv_get(F_dv_get(F_dv_get(v_data__211270, "receipt"), "receipt"), "event")), "state"), "settled")))))
            {
                return true;
            }
        }
        return false;
    }

    public static object F_dvs_factor_prune(object v_actor, object v_events, object v_time, object v_parent)
    {
        object v_policy__211580 = F_dv_get(v_actor, "policy");
        object v_mind__211610 = F_dv_get(v_actor, "mind");
        object v_domains__211636 = F_dv_find(F_dv_get(v_actor, "policy"), "factor_domains", new System.Collections.Generic.List<object>(0) { });
        object v_constraints__211698 = F_dv_find(F_dv_get(v_actor, "policy"), "factor_constraints", new System.Collections.Generic.List<object>(0) { });
        object v_cap__211768 = F_dv_find(F_dv_get(v_actor, "policy"), "prune_confidence_limit", R.number("0"));
        object v_remaining__211833 = F_dvs_sim_work_budget(v_actor);
        object v_initial__211880 = F_dvs_sim_work_budget(v_actor);
        object v_certificates__211925 = new System.Collections.Generic.List<object>(0) { };
        object v_rejections__211951 = new System.Collections.Generic.List<object>(0) { };
        object v_i__211975 = R.number("0");
        object v_result__211989 = false;
        object v_paid__212012 = F_dvs_factor_paid(v_actor, v_events);
        while (R.Bool(R.lt(v_i__211975, R.count(v_constraints__211698))))
        {
            object v_constraint__212101 = R.at(v_constraints__211698, v_i__211975);
            object v_reason__212141 = "budget-exhausted";
            object v_start__212181 = v_remaining__211833;
            if (R.Bool(F_dv_and(v_paid__212012, F_dv_and(R.lt(R.number("0"), v_cap__211768), R.lt(v_cap__211768, R.number("1"))))))
            {
                if (R.Bool(R.lt(v_remaining__211833, R.number("1"))))
                {
                    _ = false;
                }
                else
                {
                    object v_schema__212346 = F_dvs_factor_schema(v_domains__211636, v_constraint__212101);
                    v_remaining__211833 = R.sub(v_remaining__211833, R.number("1"));
                    v_reason__212141 = "invalid-domain-or-coupled-condition";
                    if (R.Bool(F_dv_get(v_schema__212346, "valid")))
                    {
                        object v_condition__212558 = F_dv_truth(v_mind__211610, F_dv_get(v_constraint__212101, "condition"));
                        object v_support__212631 = F_dv_truth(v_mind__211610, F_dv_get(v_constraint__212101, "support"));
                        v_reason__212141 = "unknown-or-not-contradicted";
                        if (R.Bool(F_dv_and(R.eq(v_condition__212558, "false"), R.eq(v_support__212631, "true"))))
                        {
                            object v_refs__212839 = F_dvs_sim_refs(F_dv_all(F_dv_pair(F_dv_get(v_constraint__212101, "condition"), F_dv_get(v_constraint__212101, "support"))));
                            object v_proof__212946 = F_dvs_factor_refs_proof(v_actor, v_mind__211610, F_dvs_sim_refs(F_dv_all(F_dv_pair(F_dv_get(v_constraint__212101, "condition"), F_dv_get(v_constraint__212101, "support")))), v_events, R.count(v_events), v_remaining__211833, F_dv_get(v_constraint__212101, "infer_laws"));
                            v_remaining__211833 = F_dv_get(v_proof__212946, "remaining");
                            v_reason__212141 = "unproven-or-inadmissible-source";
                            if (R.Bool(F_dv_and(F_dv_get(v_proof__212946, "valid"), F_dv_and(R.lt(R.number("0"), R.count(F_dv_get(v_proof__212946, "source_events"))), F_dvs_factor_scope_proof(v_domains__211636, F_dv_get(v_schema__212346, "fixed"), v_proof__212946)))))
                            {
                                object v_certificate__213420 = F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("constraint", v_constraint__212101), F_dv_pair("domains", v_domains__211636), F_dv_pair("current_revision", F_dv_get(v_mind__211610, "revision")), F_dv_pair("source_chain", F_dv_get(v_proof__212946, "steps")) }, new System.Collections.Generic.List<object>(7) { F_dv_pair("source_event_ids", F_dv_get(v_proof__212946, "source_events")), F_dv_pair("covered_assignments", new System.Collections.Generic.List<object>(2) { F_dv_pair("fixed", F_dv_get(v_schema__212346, "fixed")), F_dv_pair("free_domains", F_dv_get(v_schema__212346, "free_domains")) }), F_dv_pair("confidence", F_dv_min(v_cap__211768, F_dv_get(v_proof__212946, "confidence"))), F_dv_pair("estimated", true), F_dv_pair("paid_model_event", F_dv_find(v_actor, "model_paid_event", "")), F_dv_pair("paid_units", R.sub(v_start__212181, v_remaining__211833)), F_dv_pair("results", new System.Collections.Generic.List<object>(2) { F_dv_pair("condition", v_condition__212558), F_dv_pair("support", v_support__212631) }) });
                                _ = R.push(v_certificates__211925, v_certificate__213420);
                                v_reason__212141 = "certified";
                            }
                        }
                    }
                }
            }
            else
            {
                v_reason__212141 = "unpaid-or-missing-confidence-policy";
            }
            if (R.Bool(R.eq(v_reason__212141, "certified")))
            {
                _ = false;
            }
            else
            {
                _ = R.push(v_rejections__211951, new System.Collections.Generic.List<object>(3) { F_dv_pair("constraint", F_dv_get(v_constraint__212101, "id")), F_dv_pair("reason", v_reason__212141), F_dv_pair("paid_units", R.sub(v_start__212181, v_remaining__211833)) });
            }
            v_i__211975 = R.add(v_i__211975, R.number("1"));
        }
        v_result__211989 = F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("certificates", v_certificates__211925), F_dv_pair("rejections", v_rejections__211951), F_dv_pair("expanded", R.sub(v_initial__211880, v_remaining__211833)), F_dv_pair("remaining", R.sub(F_dv_find(v_policy__211580, "model_budget", R.number("0")), R.sub(v_initial__211880, v_remaining__211833))) }, new System.Collections.Generic.List<object>(3) { F_dv_pair("work_remaining", v_remaining__211833), F_dv_pair("current_revision", F_dv_get(v_mind__211610, "revision")), F_dv_pair("actor_before", F_dv_without(v_actor, "simulations")) });
        object v_parents__214610 = F_dvs_cognitive_parents(v_actor, F_dv_single(v_parent));
        object v_j__214676 = R.number("0");
        if (R.Bool(R.eq(F_dv_find(v_actor, "model_paid_event", ""), "")))
        {
            _ = false;
        }
        else
        {
            if (R.Bool(F_dv_has(v_parents__214610, F_dv_get(v_actor, "model_paid_event"))))
            {
                _ = false;
            }
            else
            {
                _ = R.push(v_parents__214610, F_dv_get(v_actor, "model_paid_event"));
            }
        }
        if (R.Bool(R.eq(F_dv_find(v_actor, "model_previous_event", ""), "")))
        {
            _ = false;
        }
        else
        {
            if (R.Bool(F_dv_has(v_parents__214610, F_dv_get(v_actor, "model_previous_event"))))
            {
                _ = false;
            }
            else
            {
                _ = R.push(v_parents__214610, F_dv_get(v_actor, "model_previous_event"));
            }
        }
        while (R.Bool(R.lt(v_j__214676, R.count(v_certificates__211925))))
        {
            object v_roots__215178 = F_dv_get(R.at(v_certificates__211925, v_j__214676), "source_event_ids");
            object v_k__215231 = R.number("0");
            while (R.Bool(R.lt(v_k__215231, R.count(v_roots__215178))))
            {
                if (R.Bool(F_dv_has(v_parents__214610, R.at(v_roots__215178, v_k__215231))))
                {
                    _ = false;
                }
                else
                {
                    _ = R.push(v_parents__214610, R.at(v_roots__215178, v_k__215231));
                }
                v_k__215231 = R.add(v_k__215231, R.number("1"));
            }
            v_j__214676 = R.add(v_j__214676, R.number("1"));
        }
        return F_dv_with(v_result__211989, "event", F_dvs_event(v_events, "factor_prune", F_dv_get(v_actor, "id"), v_time, v_parents__214610, v_result__211989));
    }

    public static object F_dvs_factor_certificate_parents(object v_certificate, object v_parents)
    {
        object v_ids__215613 = F_dv_get(v_certificate, "source_event_ids");
        object v_i__215656 = R.number("0");
        object v_valid__215670 = true;
        while (R.Bool(R.lt(v_i__215656, R.count(v_ids__215613))))
        {
            if (R.Bool(F_dv_has(v_parents, R.at(v_ids__215613, v_i__215656))))
            {
                _ = false;
            }
            else
            {
                v_valid__215670 = false;
            }
            v_i__215656 = R.add(v_i__215656, R.number("1"));
        }
        return v_valid__215670;
    }

    public static object F_dvs_factor_certificate_bound(object v_actor, object v_certificate, object v_events)
    {
        object v_event_id__215915 = F_dv_find(v_certificate, "event", "");
        if (R.Bool(R.eq(v_event_id__215915, "")))
        {
            return false;
        }
        object v_event__216012 = F_dvs_find_event_by_id(v_events, v_event_id__215915);
        if (R.Bool(R.eq(v_event__216012, false)))
        {
            return false;
        }
        object v_bare__216111 = F_dv_without(v_certificate, "event");
        if (R.Bool(F_dv_and(R.eq(F_dv_get(v_event__216012, "id"), v_event_id__215915), F_dv_and(R.eq(F_dv_get(v_event__216012, "actor"), F_dv_get(v_actor, "id")), R.eq(F_dv_get(v_event__216012, "type"), "factor_prune")))))
        {
            object v_data__216269 = F_dv_get(v_event__216012, "data");
            if (R.Bool(F_dv_and(F_dv_has(F_dv_get(v_data__216269, "certificates"), v_bare__216111), F_dv_and(R.eq(F_dv_get(F_dv_get(v_data__216269, "actor_before"), "id"), F_dv_get(v_actor, "id")), F_dv_and(R.eq(F_dv_get(F_dv_get(F_dv_get(v_data__216269, "actor_before"), "mind"), "revision"), F_dv_get(v_certificate, "current_revision")), F_dv_and(R.eq(F_dv_find(F_dv_get(v_data__216269, "actor_before"), "model_paid_event", ""), F_dv_get(v_certificate, "paid_model_event")), F_dv_and(F_dv_has(F_dv_get(v_event__216012, "parents"), F_dv_get(v_certificate, "paid_model_event")), F_dvs_factor_certificate_parents(v_certificate, F_dv_get(v_event__216012, "parents")))))))))
            {
                return true;
            }
        }
        return false;
    }

    public static object F_dvs_factor_certificate_current(object v_actor, object v_certificate, object v_events)
    {
        object v_mind__216786 = F_dv_get(v_actor, "mind");
        object v_policy__216812 = F_dv_get(v_actor, "policy");
        object v_required__216842 = F_dv_list3("constraint", "domains", "source_chain");
        object v_i__216911 = R.number("0");
        object v_valid__216925 = true;
        v_required__216842 = F_dv_append(v_required__216842, "source_event_ids");
        v_required__216842 = F_dv_append(v_required__216842, "current_revision");
        v_required__216842 = F_dv_append(v_required__216842, "covered_assignments");
        v_required__216842 = F_dv_append(v_required__216842, "confidence");
        v_required__216842 = F_dv_append(v_required__216842, "estimated");
        v_required__216842 = F_dv_append(v_required__216842, "paid_model_event");
        v_required__216842 = F_dv_append(v_required__216842, "paid_units");
        v_required__216842 = F_dv_append(v_required__216842, "results");
        v_required__216842 = F_dv_append(v_required__216842, "event");
        while (R.Bool(R.lt(v_i__216911, R.count(v_required__216842))))
        {
            if (R.Bool(F_dv_has_key(v_certificate, R.at(v_required__216842, v_i__216911))))
            {
                _ = false;
            }
            else
            {
                v_valid__216925 = false;
            }
            v_i__216911 = R.add(v_i__216911, R.number("1"));
        }
        if (R.Bool(v_valid__216925))
        {
            object v_domains__217586 = F_dv_find(v_policy__216812, "factor_domains", new System.Collections.Generic.List<object>(0) { });
            object v_constraint__217646 = F_dv_get(v_certificate, "constraint");
            object v_cap__217694 = F_dv_find(v_policy__216812, "prune_confidence_limit", R.number("0"));
            object v_units__217757 = F_dv_get(v_certificate, "paid_units");
            if (R.Bool(F_dv_and(R.eq(F_dv_get(v_mind__216786, "revision"), F_dv_get(v_certificate, "current_revision")), F_dv_and(F_dv_get(v_certificate, "estimated"), F_dv_and(F_dv_same(v_domains__217586, F_dv_get(v_certificate, "domains")), F_dv_and(F_dv_has(F_dv_find(v_policy__216812, "factor_constraints", new System.Collections.Generic.List<object>(0) { }), v_constraint__217646), F_dv_and(R.lt(R.number("0"), v_cap__217694), F_dv_and(R.lt(v_cap__217694, R.number("1")), F_dv_and(R.lt(R.number("0"), v_units__217757), F_dv_and(F_dvs_factor_certificate_bound(v_actor, v_certificate, v_events), F_dvs_factor_paid(F_dv_with(v_actor, "model_paid_event", F_dv_get(v_certificate, "paid_model_event")), v_events)))))))))))
            {
                object v_schema__218237 = F_dvs_factor_schema(v_domains__217586, v_constraint__217646);
                if (R.Bool(F_dv_and(F_dv_get(v_schema__218237, "valid"), F_dv_and(R.eq(F_dv_truth(v_mind__216786, F_dv_get(v_constraint__217646, "condition")), "false"), R.eq(F_dv_truth(v_mind__216786, F_dv_get(v_constraint__217646, "support")), "true")))))
                {
                    object v_proof__218449 = F_dvs_factor_refs_proof(v_actor, v_mind__216786, F_dvs_sim_refs(F_dv_all(F_dv_pair(F_dv_get(v_constraint__217646, "condition"), F_dv_get(v_constraint__217646, "support")))), v_events, R.count(v_events), R.sub(v_units__217757, R.number("1")), F_dv_get(v_constraint__217646, "infer_laws"));
                    return F_dv_and(F_dv_get(v_proof__218449, "valid"), F_dv_and(R.eq(F_dv_get(v_proof__218449, "remaining"), R.number("0")), F_dv_and(R.lt(R.number("0"), R.count(F_dv_get(v_proof__218449, "source_events"))), F_dv_and(F_dvs_factor_scope_proof(v_domains__217586, F_dv_get(v_schema__218237, "fixed"), v_proof__218449), F_dv_and(F_dv_same(F_dv_get(v_proof__218449, "steps"), F_dv_get(v_certificate, "source_chain")), F_dv_and(F_dv_same(F_dv_get(v_proof__218449, "source_events"), F_dv_get(v_certificate, "source_event_ids")), F_dv_and(F_dv_same(F_dv_get(v_certificate, "results"), new System.Collections.Generic.List<object>(2) { F_dv_pair("condition", "false"), F_dv_pair("support", "true") }), F_dv_and(F_dv_same(F_dv_get(v_certificate, "covered_assignments"), new System.Collections.Generic.List<object>(2) { F_dv_pair("fixed", F_dv_get(v_schema__218237, "fixed")), F_dv_pair("free_domains", F_dv_get(v_schema__218237, "free_domains")) }), R.eq(F_dv_get(v_certificate, "confidence"), F_dv_min(v_cap__217694, F_dv_get(v_proof__218449, "confidence")))))))))));
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }
    }

    public static object F_dvs_factor_assignment_complete(object v_domains, object v_assignments)
    {
        object v_i__219368 = R.number("0");
        object v_valid__219382 = F_dv_and(R.lt(R.number("0"), R.count(v_domains)), R.eq(R.count(v_domains), R.count(v_assignments)));
        object v_ids__219476 = new System.Collections.Generic.List<object>(0) { };
        while (R.Bool(R.lt(v_i__219368, R.count(v_assignments))))
        {
            object v_item__219536 = R.at(v_assignments, v_i__219368);
            object v_id__219570 = F_dv_get(R.at(v_assignments, v_i__219368), "variable");
            object v_domain__219611 = F_dvs_factor_domain(v_domains, F_dv_get(R.at(v_assignments, v_i__219368), "variable"));
            if (R.Bool(F_dv_has(v_ids__219476, v_id__219570)))
            {
                v_valid__219382 = false;
            }
            _ = R.push(v_ids__219476, v_id__219570);
            if (R.Bool(R.eq(v_domain__219611, false)))
            {
                v_valid__219382 = false;
            }
            else
            {
                if (R.Bool(F_dv_has(F_dv_get(v_domain__219611, "values"), F_dv_get(v_item__219536, "value"))))
                {
                    _ = false;
                }
                else
                {
                    v_valid__219382 = false;
                }
            }
            v_i__219368 = R.add(v_i__219368, R.number("1"));
        }
        return v_valid__219382;
    }

    public static object F_dvs_factor_assignment_key(object v_domains, object v_assignments)
    {
        object v_i__220039 = R.number("0");
        object v_key__220053 = new System.Collections.Generic.List<object>(0) { };
        while (R.Bool(R.lt(v_i__220039, R.count(v_domains))))
        {
            object v_domain__220109 = R.at(v_domains, v_i__220039);
            object v_j__220141 = R.number("0");
            while (R.Bool(R.lt(v_j__220141, R.count(v_assignments))))
            {
                if (R.Bool(R.eq(F_dv_get(v_domain__220109, "id"), F_dv_get(R.at(v_assignments, v_j__220141), "variable"))))
                {
                    _ = R.push(v_key__220053, new System.Collections.Generic.List<object>(2) { F_dv_pair("variable", F_dv_get(R.at(v_assignments, v_j__220141), "variable")), F_dv_pair("value", F_dv_get(R.at(v_assignments, v_j__220141), "value")) });
                }
                v_j__220141 = R.add(v_j__220141, R.number("1"));
            }
            v_i__220039 = R.add(v_i__220039, R.number("1"));
        }
        return v_key__220053;
    }

    public static object F_dvs_factor_catalog_complete(object v_domains, object v_hypotheses)
    {
        object v_product__220465 = R.number("1");
        object v_i__220485 = R.number("0");
        object v_ids__220499 = new System.Collections.Generic.List<object>(0) { };
        object v_refs__220516 = new System.Collections.Generic.List<object>(0) { };
        object v_keys__220534 = new System.Collections.Generic.List<object>(0) { };
        object v_valid__220552 = R.lt(R.number("0"), R.count(v_domains));
        while (R.Bool(R.lt(v_i__220485, R.count(v_domains))))
        {
            object v_domain__220631 = R.at(v_domains, v_i__220485);
            object v_values__220663 = F_dv_get(R.at(v_domains, v_i__220485), "values");
            object v_j__220702 = R.number("0");
            object v_seen__220720 = new System.Collections.Generic.List<object>(0) { };
            if (R.Bool(F_dv_or(F_dv_has(v_ids__220499, F_dv_get(v_domain__220631, "id")), F_dv_has(v_refs__220516, F_dv_get(v_domain__220631, "ref")))))
            {
                v_valid__220552 = false;
            }
            _ = R.push(v_ids__220499, F_dv_get(v_domain__220631, "id"));
            _ = R.push(v_refs__220516, F_dv_get(v_domain__220631, "ref"));
            if (R.Bool(F_dv_or(R.eq(R.count(v_values__220663), R.number("0")), R.not(R.lt(R.number("0"), F_dv_get(v_domain__220631, "version"))))))
            {
                v_valid__220552 = false;
            }
            while (R.Bool(R.lt(v_j__220702, R.count(v_values__220663))))
            {
                if (R.Bool(F_dv_has(v_seen__220720, R.at(v_values__220663, v_j__220702))))
                {
                    v_valid__220552 = false;
                }
                _ = R.push(v_seen__220720, R.at(v_values__220663, v_j__220702));
                v_j__220702 = R.add(v_j__220702, R.number("1"));
            }
            v_product__220465 = R.mul(v_product__220465, R.count(v_values__220663));
            v_i__220485 = R.add(v_i__220485, R.number("1"));
        }
        if (R.Bool(R.eq(v_product__220465, R.count(v_hypotheses))))
        {
            _ = false;
        }
        else
        {
            v_valid__220552 = false;
        }
        v_i__220485 = R.number("0");
        while (R.Bool(R.lt(v_i__220485, R.count(v_hypotheses))))
        {
            object v_assignments__221371 = F_dv_find(R.at(v_hypotheses, v_i__220485), "variable_assignments", new System.Collections.Generic.List<object>(0) { });
            object v_key__221448 = F_dvs_factor_assignment_key(v_domains, F_dv_find(R.at(v_hypotheses, v_i__220485), "variable_assignments", new System.Collections.Generic.List<object>(0) { }));
            if (R.Bool(F_dvs_factor_assignment_complete(v_domains, v_assignments__221371)))
            {
                _ = false;
            }
            else
            {
                v_valid__220552 = false;
            }
            if (R.Bool(F_dv_has(v_keys__220534, v_key__221448)))
            {
                v_valid__220552 = false;
            }
            _ = R.push(v_keys__220534, v_key__221448);
            v_i__220485 = R.add(v_i__220485, R.number("1"));
        }
        return v_valid__220552;
    }
}
