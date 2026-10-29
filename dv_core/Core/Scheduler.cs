using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Session / Scheduler: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_scheduled_count(object v_actors)
    {
        object v_count__289712 = R.number("0");
        object v_i__289730 = R.number("0");
        while (R.Bool(R.lt(v_i__289730, R.count(v_actors))))
        {
            if (R.Bool(R.not(R.eq(F_dv_find(R.at(v_actors, v_i__289730), "scheduled_action", false), false))))
            {
                v_count__289712 = R.add(v_count__289712, R.number("1"));
            }
            v_i__289730 = R.add(v_i__289730, R.number("1"));
        }
        return v_count__289712;
    }

    public static object F_dvs_interruptible_wait(object v_action)
    {
        if (R.Bool((R.Bool(R.not(F_dv_find(v_action, "interruptible", false))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_action, "activity", ""), "wait"))))))
        {
            return false;
        }
        object v_kind__290217 = F_dv_get(v_action, "kind");
        return (R.Bool(R.eq(v_kind__290217, "wait")) ? (object)(true) : (object)((R.Bool(R.not(R.lt(R.length(v_kind__290217), R.number("5")))) ? (object)(R.eq(R.slice(v_kind__290217, R.number("0"), R.number("5")), "wait.")) : (object)(false))));
    }

    public static object F_dvs_validate_interruptible_wait(object v_action, object v_model)
    {
        _ = F_dv_require(F_dvs_interruptible_wait(v_action), "Interruptible action must be an explicit wait activity");
        _ = F_dv_require((R.Bool((R.Bool(R.not(F_dv_has_key(v_action, "answer"))) ? (object)(R.not(F_dv_has_key(v_action, "speech"))) : (object)(false))) ? (object)(R.not(F_dv_has_key(v_action, "travel"))) : (object)(false)), "Interruptible wait cannot contain another action");
        _ = F_dv_require((R.Bool(R.eq(F_dv_get(F_dv_get(v_model, "minimum"), "time"), R.number("0"))) ? (object)(R.lt(R.number("0"), F_dv_get(F_dv_get(v_model, "actual"), "time"))) : (object)(false)), "Interruptible wait requires a zero time lower bound and positive duration");
        return true;
    }

    public static object F_dvs_wait_input_events(object v_actor, object v_events, object v_cursor, object v_action, object v_actors)
    {
        v_events = R.snapshot(v_events);
        object v_sources__291064 = new System.Collections.Generic.List<object>(0) { };
        object v_i__291085 = v_cursor;
        while (R.Bool(R.lt(v_i__291085, R.count(v_events))))
        {
            object v_event__291140 = R.at(v_events, v_i__291085);
            if (R.Bool((R.Bool(R.eq(F_dv_get(v_event__291140, "actor"), F_dv_get(v_actor, "id"))) ? (object)(F_dv_has(new System.Collections.Generic.List<object>(5) { "speech_received_pending", "listener_parse", "observation", "co_presence", "room_perception" }, F_dv_get(v_event__291140, "type"))) : (object)(false))))
            {
                object subscription = F_dv_find(v_action, "wait_subscription", false);
                object targets = R.Bool(R.eq(subscription, false)) ? false : F_dv_find(subscription, "contact_targets", false);
                object dangerRefs = R.Bool(R.eq(subscription, false)) ? false : F_dv_find(subscription, "danger_refs", new System.Collections.Generic.List<object>());
                object type = F_dv_get(v_event__291140, "type");
                bool relevant = true;
                if (!R.Bool(R.eq(targets, false)) && (R.Bool(R.eq(type, "co_presence")) || R.Bool(R.eq(type, "room_perception")))) relevant = false;
                if (!R.Bool(R.eq(targets, false)) && R.Bool(R.eq(type, "observation")))
                {
                    object data = F_dv_get(v_event__291140, "data");
                    object observations = R.Bool(R.is_list(data)) && R.Bool(F_dv_record_like(data))
                        ? F_dv_find(data, "observations", new System.Collections.Generic.List<object>()) : false;
                    if (!R.Bool(R.is_list(observations))) observations = new System.Collections.Generic.List<object>();
                    bool anySubscribed = false;
                    for (object j = R.number("0"); R.Bool(R.lt(j, R.count(observations))); j = R.add(j, R.number("1")))
                    {
                        object observation = R.at(observations, j);
                        if (!R.Bool(R.is_list(observation)) || !R.Bool(F_dv_record_like(observation))) continue;
                        object reference = F_dv_find(observation, "ref", false);
                        if (!R.Bool(R.is_list(reference)) || !R.Bool(F_dv_record_like(reference)) ||
                            !R.Bool(F_dv_has_key(reference, "type")) || !R.Bool(F_dv_has_key(reference, "object")) ||
                            !R.Bool(F_dv_has_key(reference, "facet")) || !R.Bool(F_dv_has_key(reference, "field"))) continue;
                        object entity = F_dv_get(reference, "object");
                        if (!R.Bool(R.eq(dangerRefs, false)))
                        {
                            for (object d = R.number("0"); R.Bool(R.lt(d, R.count(dangerRefs))); d = R.add(d, R.number("1")))
                            {
                                if (R.Bool(F_dv_same_local_ref(F_dv_get(v_actor, "id"), reference, R.at(dangerRefs, d))))
                                {
                                    anySubscribed = true;
                                    break;
                                }
                            }
                        }
                        if (anySubscribed) break;
                        if (R.Bool(F_dv_has(targets, entity)))
                        {
                            anySubscribed = true;
                            break;
                        }
                    }
                    if (!anySubscribed) relevant = false;
                }
                if (relevant) _ = R.push(v_sources__291064, F_dv_get(v_event__291140, "id"));
            }
            v_i__291085 = R.add(v_i__291085, R.number("1"));
        }
        return v_sources__291064;
    }

    // A declared peer subscription narrows contact interruptions without suppressing
    // observations themselves. Object perception and received speech stay independent.
    public static object F_dvs_wait_contact_changed(object action, object before, object after)
    {
        object subscription = F_dv_find(action, "wait_subscription", false);
        object targets = R.Bool(R.eq(subscription, false)) ? false : F_dv_find(subscription, "contact_targets", false);
        if (R.Bool(R.eq(targets, false))) return R.not(F_dv_same(before, after));
        if (R.Bool(R.eq(before, false)) || R.Bool(R.eq(after, false))) return R.not(F_dv_same(before, after));
        if (!R.Bool(F_dv_same(F_dv_get(before, "position"), F_dv_get(after, "position")))) return true;
        object oldContext = F_dv_get(before, "context");
        object newContext = F_dv_get(after, "context");
        for (object i = R.number("0"); R.Bool(R.lt(i, R.count(targets))); i = R.add(i, R.number("1")))
        {
            object id = R.at(targets, i);
            if (!R.Bool(F_dv_same(F_dv_find(oldContext, id, false), F_dv_find(newContext, id, false)))) return true;
        }
        return false;
    }

    public static object F_dvs_interrupt_waits(object v_state)
    {
        v_state = R.snapshot(v_state);
        object v_actors__291460 = F_dv_get(v_state, "actors");
        object v_events__291490 = false;
        object v_changed__291513 = false;
        object v_time__291537 = F_dv_get(F_dv_get(v_state, "physical"), "time");
        object v_i__291572 = R.number("0");
        while (R.Bool(R.lt(v_i__291572, R.count(v_actors__291460))))
        {
            object v_actor__291622 = R.at(v_actors__291460, v_i__291572);
            object v_original__291652 = F_dv_find(v_actor__291622, "scheduled_action", false);
            if (R.Bool(R.not(R.eq(v_original__291652, false))))
            {
                if (R.Bool((R.Bool((R.Bool(R.lt(v_time__291537, F_dv_get(v_original__291652, "due"))) ? (object)(F_dvs_interruptible_wait(F_dv_get(v_original__291652, "action"))) : (object)(false))) ? (object)(F_dv_has_key(v_original__291652, "wait_watch")) : (object)(false))))
                {
                    object v_queued__291874 = v_original__291652;
                    object v_watch__291912 = F_dv_get(v_queued__291874, "wait_watch");
                    object v_source_events__291958 = F_dv_get(v_state, "events");
                    if (R.Bool(R.not(R.eq(v_events__291490, false))))
                    {
                        v_source_events__291958 = v_events__291490;
                    }
                    object v_sources__292086 = F_dvs_wait_input_events(v_actor__291622, v_source_events__291958, F_dv_get(v_watch__291912, "event_cursor"), F_dv_get(v_queued__291874, "action"), v_actors__291460);
                    object v_heard__292180 = F_dv_find(v_actor__291622, "pending_heard", new System.Collections.Generic.List<object>(0) { });
                    object v_h__292244 = R.number("0");
                    while (R.Bool(R.lt(v_h__292244, R.count(v_heard__292180))))
                    {
                        if (R.Bool(R.not(F_dv_has(v_sources__292086, F_dv_get(R.at(v_heard__292180, v_h__292244), "source")))))
                        {
                            _ = R.push(v_sources__292086, F_dv_get(R.at(v_heard__292180, v_h__292244), "source"));
                        }
                        v_h__292244 = R.add(v_h__292244, R.number("1"));
                    }
                    object v_contact__292462 = F_dvs_idle_contact_input(v_actor__291622, F_dv_get(v_state, "physical"), v_actors__291460);
                    object v_contact_changed = R.not(F_dv_same(v_contact__292462, F_dv_get(v_watch__291912, "contact")));
                    object v_visible_change__292546 = F_dvs_wait_contact_changed(F_dv_get(v_queued__291874, "action"), F_dv_get(v_watch__291912, "contact"), v_contact__292462);
                    if (R.Bool(v_contact_changed))
                    {
                        if (R.Bool(R.eq(v_events__291490, false)))
                        {
                            v_events__291490 = F_dv_clone(F_dv_get(v_state, "events"));
                        }
                        object v_start__292754 = R.count(v_events__291490);
                        v_actor__291622 = F_dvs_notice_peers(v_actor__291622, F_dv_get(v_state, "physical"), v_actors__291460, F_dv_get(v_state, "bindings"), v_events__291490, v_time__291537, F_dv_get(v_queued__291874, "parent"));
                        v_actor__291622 = F_dvs_notice_visible_objects(v_actor__291622, F_dv_get(v_state, "physical"), F_dv_get(v_state, "bindings"), v_events__291490, v_time__291537, F_dv_get(v_queued__291874, "parent"));
                        v_sources__292086 = F_dv_explanation_merge(v_sources__292086, F_dvs_wait_input_events(v_actor__291622, v_events__291490, v_start__292754, F_dv_get(v_queued__291874, "action"), v_actors__291460));
                    }
                    if (R.Bool((R.Bool(v_visible_change__292546) ? (object)(true) : (object)(R.lt(R.number("0"), R.count(v_sources__292086))))))
                    {
                        if (R.Bool(R.eq(v_events__291490, false)))
                        {
                            v_events__291490 = F_dv_clone(F_dv_get(v_state, "events"));
                        }
                        object v_elapsed__293308 = R.sub(v_time__291537, F_dv_get(v_queued__291874, "start"));
                        _ = F_dv_require((R.Bool(R.not(R.lt(v_elapsed__293308, R.number("0")))) ? (object)(R.lt(v_elapsed__293308, F_dv_get(F_dv_get(F_dv_get(v_queued__291874, "model"), "actual"), "time"))) : (object)(false)), "Invalid interrupted wait duration");
                        object v_interruption__293483 = new System.Collections.Generic.List<object>(9) { F_dv_pair("reason", "activity-interrupted"), F_dv_pair("failure_class", "activity-interrupted"), F_dv_pair("action_id", F_dv_get(F_dv_get(v_queued__291874, "action"), "id")), F_dv_pair("start", F_dv_get(v_queued__291874, "start")), F_dv_pair("planned_due", F_dv_get(v_queued__291874, "due")), F_dv_pair("time", v_time__291537), F_dv_pair("elapsed", v_elapsed__293308), F_dv_pair("source_events", v_sources__292086), F_dv_pair("visible_change", v_visible_change__292546) };
                        object v_interrupted__293773 = F_dvs_event(v_events__291490, "wait_interrupted", F_dv_get(v_actor__291622, "id"), v_time__291537, F_dv_explanation_merge(new System.Collections.Generic.List<object>(1) { F_dv_get(v_queued__291874, "parent") }, v_sources__292086), v_interruption__293483);
                        object v_model__294086 = F_dv_with(F_dv_get(v_queued__291874, "model"), "actual", F_dv_with(F_dv_get(F_dv_get(v_queued__291874, "model"), "actual"), "time", v_elapsed__293308));
                        v_queued__291874 = F_dv_with_many(v_queued__291874, new System.Collections.Generic.List<object>(4) { F_dv_pair("model", v_model__294086), F_dv_pair("due", v_time__291537), F_dv_pair("parent", v_interrupted__293773), F_dv_pair("activity_interruption", F_dv_with(v_interruption__293483, "event", v_interrupted__293773)) });
                    }
                    else
                    {
                        if (R.Bool(v_contact_changed) || R.Bool(R.not(R.eq(F_dv_get(v_watch__291912, "event_cursor"), R.count(v_source_events__291958)))))
                        {
                            object updatedWatch = F_dv_with(v_watch__291912, "contact", v_contact__292462);
                            updatedWatch = F_dv_with(updatedWatch, "event_cursor", R.Bool(R.eq(v_events__291490, false)) ? R.count(v_source_events__291958) : R.count(v_events__291490));
                            v_queued__291874 = F_dv_with(v_queued__291874, "wait_watch", updatedWatch);
                        }
                    }
                    if (R.Bool(R.not(F_dv_same(v_queued__291874, v_original__291652))))
                    {
                        // Later contact queries must see earlier updates without
                        // acquiring writable branches from the replacement root.
                        v_actors__291460 = R.snapshot(F_dvs_replace(v_actors__291460, v_i__291572, F_dv_with(v_actor__291622, "scheduled_action", v_queued__291874)));
                        v_changed__291513 = true;
                    }
                }
            }
            v_i__291572 = R.add(v_i__291572, R.number("1"));
        }
        if (R.Bool(R.not(v_changed__291513)))
        {
            return v_state;
        }
        object v_updates__294961 = new System.Collections.Generic.List<object>(1) { F_dv_pair("actors", v_actors__291460) };
        if (R.Bool(R.not(R.eq(v_events__291490, false))))
        {
            v_updates__294961 = F_dv_with(v_updates__294961, "events", v_events__291490);
        }
        return F_dvs_owned_state_update(v_state, v_updates__294961);
    }

    public static object F_dvs_interrupted_wait_due(object v_actors, object v_time)
    {
        object v_i__295169 = R.number("0");
        while (R.Bool(R.lt(v_i__295169, R.count(v_actors))))
        {
            object v_queued__295219 = F_dv_find(R.at(v_actors, v_i__295169), "scheduled_action", false);
            if (R.Bool(R.not(R.eq(v_queued__295219, false))))
            {
                if (R.Bool((R.Bool(R.not(R.lt(v_time, F_dv_get(v_queued__295219, "due")))) ? (object)(R.not(R.eq(F_dv_find(v_queued__295219, "activity_interruption", false), false))) : (object)(false))))
                {
                    return v_i__295169;
                }
            }
            v_i__295169 = R.add(v_i__295169, R.number("1"));
        }
        return R.sub(R.number("0"), R.number("1"));
    }

    public static object F_dvs_dispatch_due(object v_state, object v_index)
    {
        v_state = R.snapshot(v_state);
        object v_updated_actor__295530 = F_dvs_erosion_update(R.at(F_dv_get(v_state, "actors"), v_index), F_dv_get(v_state, "physical"), F_dv_get(F_dv_get(v_state, "physical"), "time"));
        if (R.Bool(R.not(F_dvs_actor_can_act(v_updated_actor__295530))))
        {
            object queue = F_dv_find(v_updated_actor__295530, "scheduled_action", false);
            object held = F_dv_find(v_updated_actor__295530, "navigation_commitment", false);
            object v_cancelled__295680 = F_dv_with(v_updated_actor__295530, "scheduled_action", false);
            if (!R.Bool(R.eq(queue, false)) && !R.Bool(R.eq(held, false)))
            {
                object action = F_dv_get(queue, "action");
                object candidate = F_dv_find(action, "navigation_candidate_id", false);
                if (!R.Bool(R.eq(candidate, false)) && R.Bool(R.eq(candidate, F_dv_find(held, "candidate_id", false))))
                {
                    object events = F_dv_clone(F_dv_get(v_state, "events"));
                    object task = F_dv_get(held, "task");
                    _ = F_dvs_event(events, "navigation_task_released", F_dv_get(v_updated_actor__295530, "id"), F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_single(F_dv_get(queue, "parent")),
                        new List<object> { F_dv_pair("task_id", F_dv_get(task, "task_id")), F_dv_pair("phase", F_dv_get(task, "phase")), F_dv_pair("reason", "actor-unable-before-dispatch"), F_dv_pair("candidate_id", candidate), F_dv_pair("action_event", "") });
                    v_cancelled__295680 = F_dv_with(v_cancelled__295680, "navigation_commitment", false);
                    return F_dvs_owned_state_update(v_state, new List<object> { F_dv_pair("actors", F_dvs_replace(F_dv_get(v_state, "actors"), v_index, v_cancelled__295680)), F_dv_pair("events", events), F_dv_pair("cursor", v_index), F_dv_pair("idle", R.number("0")) });
                }
            }
            return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(3) { F_dv_pair("actors", F_dvs_replace(F_dv_get(v_state, "actors"), v_index, v_cancelled__295680)), F_dv_pair("cursor", v_index), F_dv_pair("idle", R.number("0")) });
        }
        object v_queued__295880 = F_dv_get(v_updated_actor__295530, "scheduled_action");
        object v_time__295928 = F_dv_get(F_dv_get(v_state, "physical"), "time");
        object v_events__296126 = F_dv_clone(F_dv_get(v_state, "events"));
        object v_actor__296166 = F_dvs_drain_received_speech(F_dv_with(v_updated_actor__295530, "scheduled_action", false), v_events__296126, v_time__295928);
        object v_action__296273 = F_dv_get(v_queued__295880, "action");
        if (R.Bool(R.not(F_dvs_argument_action_evidence(v_actor__296166, v_action__296273, v_events__296126))))
        {
            _ = F_dvs_event(v_events__296126, "action_cancelled", F_dv_get(v_actor__296166, "id"), v_time__295928, new System.Collections.Generic.List<object>(1) { F_dv_get(v_queued__295880, "parent") }, new System.Collections.Generic.List<object>(5) { F_dv_pair("action_id", F_dv_get(v_action__296273, "id")), F_dv_pair("reason", "argument-no-longer-current"), F_dv_pair("scheduled_start", F_dv_get(v_queued__295880, "start")), F_dv_pair("due", F_dv_get(v_queued__295880, "due")), F_dv_pair("charged", false) });
            return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(4) { F_dv_pair("actors", F_dvs_replace(F_dv_get(v_state, "actors"), v_index, v_actor__296166)), F_dv_pair("events", v_events__296126), F_dv_pair("cursor", v_index), F_dv_pair("idle", R.number("0")) });
        }
        object v_preparation_parent__296727 = F_dv_get(v_queued__295880, "parent");
        if (R.Bool(F_dvs_action_requires_argument(v_action__296273)))
        {
            v_action__296273 = F_dv_with_many(v_action__296273, new System.Collections.Generic.List<object>(3) { F_dv_pair("argument_evidence_revision", F_dv_get(F_dv_get(v_actor__296166, "mind"), "revision")), F_dv_pair("argument_evidence_time", v_time__295928), F_dv_pair("argument_evidence_mind_time", F_dv_get(F_dv_get(v_actor__296166, "mind"), "time")) });
            v_preparation_parent__296727 = F_dvs_event(v_events__296126, "argument_evidence_prepared", F_dv_get(v_actor__296166, "id"), v_time__295928, F_dvs_cognitive_parents(v_actor__296166, new System.Collections.Generic.List<object>(2) { F_dv_get(v_queued__295880, "parent"), F_dv_get(F_dv_get(v_actor__296166, "argument"), "id") }), new System.Collections.Generic.List<object>(8) { F_dv_pair("action_id", F_dv_get(v_action__296273, "id")), F_dv_pair("argument_id", F_dv_get(F_dv_get(v_actor__296166, "argument"), "id")), F_dv_pair("argument_evidence_revision", F_dv_get(F_dv_get(v_actor__296166, "mind"), "revision")), F_dv_pair("argument_evidence_time", v_time__295928), F_dv_pair("argument_evidence_mind_time", F_dv_get(F_dv_get(v_actor__296166, "mind"), "time")), F_dv_pair("physical_revision", F_dv_get(F_dv_get(v_state, "physical"), "revision")), F_dv_pair("source_event_ids", F_dv_get(F_dv_get(v_actor__296166, "argument"), "source_event_ids")), F_dv_pair("contract", "prepared-evidence-before-own-completion-observations") });
        }
        object v_bounds__297503 = F_dv_action_cost_bounds(F_dv_get(F_dv_get(v_queued__295880, "model"), "minimum"), F_dv_get(F_dv_get(v_queued__295880, "model"), "maximum"));
        object v_event__297586 = R.cat("action", R.show(F_dv_get(v_state, "executed")));
        if (R.Bool(F_dv_has_key(v_action__296273, "navigation_task")))
        {
            v_action__296273 = F_dv_with(v_action__296273, "navigation_action_event", v_event__297586);
            v_actor__296166 = F_dvs_navigation_task_dispatch(v_actor__296166, v_action__296273, v_event__297586);
        }
        object v_quote__297644 = F_dv_with(F_dv_action_cost_quote(F_dv_get(v_actor__296166, "ledger"), F_dv_get(v_state, "physical"), F_dv_find(v_action__296273, "law", F_dv_get(v_action__296273, "id")), v_event__297586, v_bounds__297503), "scheduled_start", F_dv_get(v_queued__295880, "start"));
        object v_interruption__297803 = F_dv_find(v_queued__295880, "activity_interruption", false);
        if (R.Bool(R.not(R.eq(v_interruption__297803, false))))
        {
            v_quote__297644 = F_dv_with(v_quote__297644, "activity_interruption", v_interruption__297803);
        }
        object v_parent__297974 = F_dvs_event(v_events__296126, "cost_quote", F_dv_get(v_actor__296166, "id"), v_time__295928, new System.Collections.Generic.List<object>(1) { v_preparation_parent__296727 }, v_quote__297644);
        object v_pending__298068 = new System.Collections.Generic.List<object>(13) { F_dv_pair("index", v_index), F_dv_pair("actor", v_actor__296166), F_dv_pair("parent", v_parent__297974), F_dv_pair("time", v_time__295928), F_dv_pair("action", v_action__296273), F_dv_pair("id", F_dv_get(v_actor__296166, "id")), F_dv_pair("mind", F_dv_get(v_actor__296166, "mind")), F_dv_pair("model", F_dv_get(v_queued__295880, "model")), F_dv_pair("action_event", v_event__297586), F_dv_pair("ledger", F_dv_get(v_actor__296166, "ledger")), F_dv_pair("quote", v_quote__297644), F_dv_pair("choice", F_dv_get(v_queued__295880, "choice")), F_dv_pair("activity_interruption", v_interruption__297803) };
        if (R.Bool(F_dvs_action_requires_argument(v_action__296273)))
        {
            v_pending__298068 = F_dv_with(v_pending__298068, "argument_evidence_world", F_dv_get(v_state, "physical"));
        }
        return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(4) { F_dv_pair("actors", F_dvs_replace(F_dv_get(v_state, "actors"), v_index, v_actor__296166)), F_dv_pair("events", v_events__296126), F_dv_pair("pending", v_pending__298068), F_dv_pair("idle", R.number("0")) });
    }

    public static object F_dvs_known_clock_boundary(object v_condition, object v_clock, object v_now, object v_limit)
    {
        return F_dv_time_boundary(v_condition, v_clock, v_now, v_limit, "invalidate");
    }

    public static object F_dvs_idle_subject_input(object v_actor, object v_costs)
    {
        v_actor = R.snapshot(v_actor);
        v_costs = R.snapshot(v_costs);
        object v_policy__299346 = F_dv_get(v_actor, "policy");
        object v_maximum__299376 = R.number("0");
        object v_i__299396 = R.number("0");
        while (R.Bool(R.lt(v_i__299396, R.count(v_costs))))
        {
            v_maximum__299376 = F_dv_max(v_maximum__299376, F_dv_get(F_dv_get(R.at(R.at(v_costs, v_i__299396), R.number("1")), "maximum"), "time"));
            v_i__299396 = R.add(v_i__299396, R.number("1"));
        }
        object v_bound__299519 = v_maximum__299376;
        if (R.Bool((R.Bool(F_dv_has_key(v_policy__299346, "remaining_game_time")) ? (object)(R.not(R.lt(F_dv_get(v_policy__299346, "remaining_game_time"), v_bound__299519))) : (object)(false))))
        {
            v_policy__299346 = F_dv_with(v_policy__299346, "remaining_game_time", v_bound__299519);
        }
        object v_result__299696 = new System.Collections.Generic.List<object>(0) { };
        v_i__299396 = R.number("0");
        while (R.Bool(R.lt(v_i__299396, R.count(v_actor))))
        {
            object v_entry__299761 = R.at(v_actor, v_i__299396);
            object v_key__299790 = R.at(v_entry__299761, R.number("0"));
            if (R.Bool(R.not(F_dv_has(new System.Collections.Generic.List<object>(9) { "idle_guard", "decision_time", "decision_revision", "support_events_cursor", "heard_utterances_cursor", "cognition_reused", "cognition_stop", "reason_reused", "last_reason_report" }, v_key__299790))))
            {
                if (R.Bool(R.eq(v_key__299790, "policy")))
                {
                    _ = R.push(v_result__299696, F_dv_pair(v_key__299790, v_policy__299346));
                }
                else
                {
                    _ = R.push(v_result__299696, v_entry__299761);
                }
            }
            v_i__299396 = R.add(v_i__299396, R.number("1"));
        }
        return v_result__299696;
    }

    public static object F_dvs_idle_story_input(object v_story)
    {
        v_story = R.snapshot(v_story);
        object v_result__300267 = new System.Collections.Generic.List<object>(0) { };
        object v_i__300287 = R.number("0");
        while (R.Bool(R.lt(v_i__300287, R.count(v_story))))
        {
            if (R.Bool(R.not(F_dv_has(new System.Collections.Generic.List<object>(2) { "background_scan_cursor", "background_waiting_cursor" }, R.at(R.at(v_story, v_i__300287), R.number("0"))))))
            {
                _ = R.push(v_result__300267, R.at(v_story, v_i__300287));
            }
            v_i__300287 = R.add(v_i__300287, R.number("1"));
        }
        return v_result__300267;
    }

    public static object F_dvs_idle_contact_input(object v_actor, object v_physical, object v_actors)
    {
        if (R.Bool((R.Bool(R.not(F_dv_find(F_dv_get(v_actor, "policy"), "notice_co_presence", false))) ? (object)(R.not(F_dv_find(F_dv_get(v_actor, "policy"), "notice_visible_objects", false))) : (object)(false))))
        {
            return false;
        }
        object v_context__300861 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(F_dv_find(F_dv_get(v_actor, "policy"), "notice_co_presence", false)))
        {
            v_context__300861 = F_dvs_visible_context(v_actor, v_physical, v_actors);
        }
        return new System.Collections.Generic.List<object>(4) { F_dv_pair("context", v_context__300861), F_dv_pair("objects", F_dvs_visible_objects(v_actor, v_physical)), F_dv_pair("position", F_dv_read(v_physical, F_dv_get(v_actor, "location_ref"))), F_dv_pair("visible", F_dv_truth(v_physical, F_dv_find(F_dv_get(v_actor, "policy"), "visual_identity_when", F_dv_true()))) };
    }

    public static object F_dvs_idle_can_wait(object v_actor)
    {
        return (R.Bool(R.eq(R.count(F_dv_find(v_actor, "pending_heard", new System.Collections.Generic.List<object>(0) { })), R.number("0"))) ? (object)(R.not(F_dv_find(v_actor, "cognition_blocked", false))) : (object)(false));
    }

    public static object F_dvs_idle_events_stable(object v_events, object v_actor, object v_guard)
    {
        return (R.Bool(R.not(R.lt(R.count(v_events), F_dv_get(v_guard, "event_cursor")))) ? (object)(F_dvs_owned_events_stable(v_events, v_actor, F_dv_get(v_guard, "event_cursor"))) : (object)(false));
    }

    public static object F_dvs_idle_guard_matches(object v_actor, object v_state)
    {
        object v_guard__301782 = F_dv_find(v_actor, "idle_guard", false);
        if (R.Bool((R.Bool((R.Bool(R.eq(v_guard__301782, false)) ? (object)(true) : (object)(R.not(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_get(v_guard__301782, "until")))))) ? (object)(true) : (object)(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_get(v_guard__301782, "time"))))))
        {
            return false;
        }
        if (R.Bool((R.Bool(R.not(F_dvs_idle_can_wait(v_actor))) ? (object)(true) : (object)(R.not(F_dvs_idle_events_stable(F_dv_get(v_state, "events"), v_actor, v_guard__301782))))))
        {
            return false;
        }
        if (R.Bool(R.not(F_dv_same(F_dvs_idle_subject_input(v_actor, F_dv_get(v_state, "costs")), F_dv_get(v_guard__301782, "subject")))))
        {
            return false;
        }
        if (R.Bool((R.Bool((R.Bool(R.not(F_dv_same(F_dv_get(v_state, "bindings"), F_dv_get(v_guard__301782, "bindings")))) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_get(v_state, "costs"), F_dv_get(v_guard__301782, "costs")))))) ? (object)(true) : (object)(R.not(F_dv_same(F_dvs_idle_story_input(F_dv_get(v_state, "story")), F_dv_get(v_guard__301782, "story")))))))
        {
            return false;
        }
        return F_dv_same(F_dvs_idle_contact_input(v_actor, F_dv_get(v_state, "physical"), F_dv_get(v_state, "actors")), F_dv_get(v_guard__301782, "contact"));
    }

    public static object F_dvs_idle_inputs_changed(object v_actor, object v_state)
    {
        object v_guard__302488 = F_dv_find(v_actor, "idle_guard", false);
        if (R.Bool(R.eq(v_guard__302488, false)))
        {
            return false;
        }
        if (R.Bool((R.Bool(R.not(F_dv_same(F_dvs_idle_subject_input(v_actor, F_dv_get(v_state, "costs")), F_dv_get(v_guard__302488, "subject")))) ? (object)(true) : (object)(R.not(F_dvs_idle_events_stable(F_dv_get(v_state, "events"), v_actor, v_guard__302488))))))
        {
            return true;
        }
        if (R.Bool((R.Bool((R.Bool(R.not(F_dv_same(F_dv_get(v_state, "bindings"), F_dv_get(v_guard__302488, "bindings")))) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_get(v_state, "costs"), F_dv_get(v_guard__302488, "costs")))))) ? (object)(true) : (object)(R.not(F_dv_same(F_dvs_idle_story_input(F_dv_get(v_state, "story")), F_dv_get(v_guard__302488, "story")))))))
        {
            return true;
        }
        return R.not(F_dv_same(F_dvs_idle_contact_input(v_actor, F_dv_get(v_state, "physical"), F_dv_get(v_state, "actors")), F_dv_get(v_guard__302488, "contact")));
    }

    public static object F_dvs_declared_conditions(object v_value)
    {
        if (R.Bool(R.not(R.is_list(v_value))))
        {
            return new System.Collections.Generic.List<object>(0) { };
        }
        if (R.Bool(F_dv_record_like(v_value)))
        {
            if (R.Bool(F_dv_has_key(v_value, "op")))
            {
                if (R.Bool(R.not(F_dv_has(new System.Collections.Generic.List<object>(2) { "assign", "increment" }, F_dv_get(v_value, "op")))))
                {
                    return new System.Collections.Generic.List<object>(1) { v_value };
                }
            }
        }
        object v_conditions__303416 = new System.Collections.Generic.List<object>(0) { };
        object v_i__303440 = R.number("0");
        while (R.Bool(R.lt(v_i__303440, R.count(v_value))))
        {
            object v_nested__303489 = F_dvs_declared_conditions(R.at(v_value, v_i__303440));
            object v_j__303544 = R.number("0");
            while (R.Bool(R.lt(v_j__303544, R.count(v_nested__303489))))
            {
                _ = R.push(v_conditions__303416, R.at(v_nested__303489, v_j__303544));
                v_j__303544 = R.add(v_j__303544, R.number("1"));
            }
            v_i__303440 = R.add(v_i__303440, R.number("1"));
        }
        return v_conditions__303416;
    }

    public static object F_dvs_declared_conditions_cached(object v_value) => DeclaredConditionsCached(v_value, false);

    // Idle analysis only reads declarations. Keep its cached subtrees frozen;
    // the public entry point retains the ordinary writable-result contract.
    private static object DeclaredConditionsCached(object v_value, bool readOnly)
    {
        if (R.Bool(R.not(R.is_list(v_value))))
        {
            return new System.Collections.Generic.List<object>(0) { };
        }
        object v_key__304019 = new System.Collections.Generic.List<object>(1) { "dvs-declared-conditions-subtree-v1" };
        object v_cached__304072 = readOnly ? ReadCachedValue("declaration", v_value, v_key__304019)
            : F_dvs_declaration_cache_get(v_value, v_key__304019);
        if (R.Bool(R.at(v_cached__304072, R.number("0"))))
        {
            return R.at(v_cached__304072, R.number("1"));
        }
        object v_conditions__304170 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(F_dv_record_like(v_value)))
        {
            if (R.Bool(F_dv_has_key(v_value, "op")))
            {
                if (R.Bool(R.not(F_dv_has(new System.Collections.Generic.List<object>(2) { "assign", "increment" }, F_dv_get(v_value, "op")))))
                {
                    var result = F_dvs_declaration_cache_put(v_value, v_key__304019, new System.Collections.Generic.List<object>(1) { v_value });
                    return readOnly ? R.snapshot(result) : result;
                }
            }
        }
        object v_i__304392 = R.number("0");
        while (R.Bool(R.lt(v_i__304392, R.count(v_value))))
        {
            object v_nested__304441 = DeclaredConditionsCached(R.at(v_value, v_i__304392), readOnly);
            object v_j__304503 = R.number("0");
            while (R.Bool(R.lt(v_j__304503, R.count(v_nested__304441))))
            {
                _ = R.push(v_conditions__304170, R.at(v_nested__304441, v_j__304503));
                v_j__304503 = R.add(v_j__304503, R.number("1"));
            }
            v_i__304392 = R.add(v_i__304392, R.number("1"));
        }
        var collected = F_dvs_declaration_cache_put(v_value, v_key__304019, v_conditions__304170);
        return readOnly ? R.snapshot(collected) : collected;
    }

    public static object F_dvs_idle_window_boundary(object v_actor, object v_story, object v_now, object v_limit)
    {
        object v_clock__304753 = F_dv_find(v_story, "clock_ref", false);
        object v_key__304804 = new System.Collections.Generic.List<object>(3) { "dvs_idle_declared_conditions", F_dv_get(v_actor, "policy"), F_dv_get(F_dv_get(v_actor, "mind"), "adoptions") };
        object v_cached__304887 = ReadCachedValue("declaration", F_dv_get(v_actor, "actions"), v_key__304804);
        object v_conditions__304950 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.at(v_cached__304887, R.number("0"))))
        {
            v_conditions__304950 = R.at(v_cached__304887, R.number("1"));
        }
        else
        {
            if (R.Bool(F_dv_find(F_dv_get(v_actor, "policy"), "opt_declcache", false)))
            {
                object v_declarations__305326 = R.snapshot(new System.Collections.Generic.List<object>(3) { F_dv_get(v_actor, "actions"), F_dv_get(v_actor, "policy"), F_dv_get(F_dv_get(v_actor, "mind"), "adoptions") });
                v_conditions__304950 = DeclaredConditionsCached(v_declarations__305326, true);
            }
            else
            {
                v_conditions__304950 = F_dvs_declared_conditions(new System.Collections.Generic.List<object>(3) { F_dv_get(v_actor, "actions"), F_dv_get(v_actor, "policy"), F_dv_get(F_dv_get(v_actor, "mind"), "adoptions") });
            }
            _ = F_dvs_declaration_cache_put(F_dv_get(v_actor, "actions"), v_key__304804, v_conditions__304950);
        }
        // Freeze the new wrapper too: reading a frozen child through a mutable
        // record would otherwise acquire a writable copy of the condition tree.
        object v_boundary__305666 = F_dvs_known_clock_boundary(R.snapshot(F_dv_all(v_conditions__304950)), v_clock__304753, v_now, v_limit);
        object v_duties__305749 = F_dvs_obligation_actions(F_dv_get(v_actor, "actions"));
        object v_duty_index__305804 = R.number("0");
        while (R.Bool(R.lt(v_duty_index__305804, R.count(v_duties__305749))))
        {
            object v_due__305872 = F_dv_find(R.at(v_duties__305749, v_duty_index__305804), "obligation_due", R.number("0"));
            if (R.Bool((R.Bool((R.Bool(R.lt(v_now, v_due__305872)) ? (object)(R.lt(v_due__305872, F_dv_get(v_boundary__305666, "next"))) : (object)(false))) ? (object)(R.not(F_dv_has(F_dv_find(v_actor, "completed_actions", new System.Collections.Generic.List<object>(0) { }), F_dv_get(R.at(v_duties__305749, v_duty_index__305804), "id")))) : (object)(false))))
            {
                v_boundary__305666 = F_dv_with(v_boundary__305666, "next", v_due__305872);
            }
            v_duty_index__305804 = R.add(v_duty_index__305804, R.number("1"));
        }
        if (R.Bool(F_dvs_has_active_communication_goal(v_actor)))
        {
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", false), F_dv_pair("next", F_dv_min(F_dv_get(v_boundary__305666, "next"), F_dv_min(v_limit, R.add(v_now, R.number("1"))))) };
        }
        return v_boundary__305666;
    }

    public static object F_dvs_schedule(object v_state)
    {
        _ = R.phase("schedule-sync-start", "");
        try
        {
            v_state = F_dvs_simulation_sync(v_state);
            v_state = R.snapshot(v_state);
        }
        finally { _ = R.phase("schedule-sync-end", ""); }
        object v_original_actors = F_dv_get(v_state, "actors");
        object v_updated__306553 = v_original_actors;
        object v_erosion_index__306584 = R.number("0");
        object v_any_eroded__306610 = false;
        _ = R.phase("schedule-erosion-start", "");
        try
        {
            while (R.Bool(R.lt(v_erosion_index__306584, R.count(v_updated__306553))))
            {
                object v_original__306686 = R.at(v_original_actors, v_erosion_index__306584);
                object v_eroded__306732 = F_dvs_erosion_update(v_original__306686, F_dv_get(v_state, "physical"), F_dv_get(F_dv_get(v_state, "physical"), "time"));
                if (R.Bool(R.not(F_dv_same(v_eroded__306732, v_original__306686))))
                {
                    v_updated__306553 = F_dvs_replace(v_updated__306553, v_erosion_index__306584, v_eroded__306732);
                    v_any_eroded__306610 = true;
                }
                v_erosion_index__306584 = R.add(v_erosion_index__306584, R.number("1"));
            }
            if (R.Bool(v_any_eroded__306610))
            {
                v_state = R.snapshot(F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(1) { F_dv_pair("actors", v_updated__306553) }));
            }
        }
        finally { _ = R.phase("schedule-erosion-end", ""); }
        _ = R.phase("schedule-waits-start", "");
        try { v_state = R.snapshot(F_dvs_interrupt_waits(v_state)); }
        finally { _ = R.phase("schedule-waits-end", ""); }
        v_updated__306553 = F_dv_get(v_state, "actors");
        object v_current__307173 = v_state;
        object v_due_index__307197 = R.sub(R.number("0"), R.number("1"));
        object v_due_time__307220 = R.add(F_dv_get(v_state, "horizon"), R.number("1"));
        object v_wake_index__307257 = R.sub(R.number("0"), R.number("1"));
        object v_wake_time__307281 = R.add(F_dv_get(v_state, "horizon"), R.number("1"));
        object v_event_wake_index__307319 = R.sub(R.number("0"), R.number("1"));
        object v_event_wake_time__307349 = R.add(F_dv_get(v_state, "horizon"), R.number("1"));
        if (R.Bool(F_dv_has_key(F_dv_get(v_state, "physical"), "simulation")))
        {
            v_event_wake_time__307349 = F_dv_min(R.add(F_dv_get(F_dv_get(F_dv_get(v_state, "physical"), "simulation"), "last_macro"), R.number("15")), F_dv_get(v_state, "horizon"));
            v_event_wake_index__307319 = F_dv_get(v_state, "cursor");
            v_wake_time__307281 = v_event_wake_time__307349;
            v_wake_index__307257 = F_dv_get(v_state, "cursor");
        }
        object v_ready_index__307643 = R.sub(R.number("0"), R.number("1"));
        object v_obligation_index__307668 = R.sub(R.number("0"), R.number("1"));
        object v_obligation_time__307698 = R.add(F_dv_get(v_state, "horizon"), R.number("1"));
        object v_offset__307742 = R.number("0");
        object v_index__307761 = F_dv_get(v_state, "cursor");
        _ = R.phase("schedule-scan-start", "");
        try
        {
            while (R.Bool(R.lt(v_offset__307742, R.count(F_dv_get(v_state, "actors")))))
            {
                object v_actor__307837 = R.at(F_dv_get(v_state, "actors"), v_index__307761);
                object v_queued__307877 = F_dv_find(v_actor__307837, "scheduled_action", false);
                if (R.Bool(R.not(F_dvs_actor_can_act(v_actor__307837))))
                {
                    object held = F_dv_find(v_actor__307837, "navigation_commitment", false);
                    if (!R.Bool(R.eq(v_queued__307877, false)) || !R.Bool(R.eq(held, false)))
                    {
                        object stopped = F_dv_with(v_actor__307837, "scheduled_action", false);
                        object updates = new List<object>();
                        if (!R.Bool(R.eq(held, false)))
                        {
                            object events = F_dv_clone(F_dv_get(v_state, "events"));
                            object task = F_dv_get(held, "task");
                            object parent = R.Bool(R.eq(v_queued__307877, false)) ? F_dv_find(held, "selection_event", "") : F_dv_get(v_queued__307877, "parent");
                            _ = F_dvs_event(events, "navigation_task_released", F_dv_get(v_actor__307837, "id"), F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_single(parent),
                                new List<object> { F_dv_pair("task_id", F_dv_get(task, "task_id")), F_dv_pair("phase", F_dv_get(task, "phase")), F_dv_pair("reason", "actor-unable-to-act"), F_dv_pair("action_event", F_dv_find(held, "pending_action_event", "")) });
                            stopped = F_dv_with(stopped, "navigation_commitment", false);
                            _ = R.push(updates, F_dv_pair("events", events));
                        }
                        v_updated__306553 = F_dvs_replace(v_updated__306553, v_index__307761, stopped);
                        _ = R.push(updates, F_dv_pair("actors", v_updated__306553));
                        v_state = R.snapshot(F_dvs_owned_state_update(v_state, updates));
                    }
                }
                else
                {
                    if (R.Bool(R.not(R.eq(v_queued__307877, false))))
                    {
                        if (R.Bool(R.lt(F_dv_get(v_queued__307877, "due"), v_due_time__307220)))
                        {
                            v_due_time__307220 = F_dv_get(v_queued__307877, "due");
                            v_due_index__307197 = v_index__307761;
                        }
                    }
                    else
                    {
                        if (R.Bool((R.Bool((R.Bool(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_get(v_state, "horizon"))) ? (object)(R.lt(R.number("0"), F_dv_find(F_dv_get(v_actor__307837, "policy"), "budget", R.number("0")))) : (object)(false))) ? (object)(F_dvs_simulation_actor_ready(v_actor__307837, v_state)) : (object)(false))))
                        {
                            object v_duty__308540 = F_dvs_due_obligation(v_actor__307837, F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_get(v_state, "costs"));
                            if (R.Bool(R.not(R.eq(v_duty__308540, false))))
                            {
                                object v_due__308673 = F_dv_find(v_duty__308540, "obligation_due", R.number("0"));
                                if (R.Bool(R.lt(v_due__308673, v_obligation_time__307698)))
                                {
                                    v_obligation_index__307668 = v_index__307761;
                                    v_obligation_time__307698 = v_due__308673;
                                }
                            }
                        }
                        if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_get(v_state, "horizon"))) ? (object)(R.lt(v_ready_index__307643, R.number("0"))) : (object)(false))) ? (object)(R.lt(R.number("0"), F_dv_find(F_dv_get(v_actor__307837, "policy"), "budget", R.number("0")))) : (object)(false))) ? (object)(F_dvs_simulation_actor_ready(v_actor__307837, v_state)) : (object)(false))) ? (object)((R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_actor__307837, "decision_time", R.sub(R.number("0"), R.number("1"))), F_dv_get(F_dv_get(v_state, "physical"), "time")))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_actor__307837, "decision_revision", R.sub(R.number("0"), R.number("1"))), F_dv_get(F_dv_get(v_actor__307837, "mind"), "revision")))))) ? (object)(true) : (object)(R.lt(R.number("0"), R.count(F_dv_find(v_actor__307837, "pending_heard", new System.Collections.Generic.List<object>(0) { })))))) ? (object)(true) : (object)(F_dvs_idle_inputs_changed(v_actor__307837, v_state)))) : (object)(false))))
                        {
                            if (R.Bool(F_dvs_idle_guard_matches(v_actor__307837, v_state)))
                            {
                                object v_guard__309510 = F_dv_get(v_actor__307837, "idle_guard");
                                object v_advanced__309751 = F_dv_with_many(v_guard__309510, new System.Collections.Generic.List<object>(2) { F_dv_pair("event_cursor", R.count(F_dv_get(v_state, "events"))), F_dv_pair("event_anchor", R.at(F_dv_get(v_state, "events"), R.sub(R.count(F_dv_get(v_state, "events")), R.number("1")))) });
                                object v_sleeping__309910 = F_dv_with_many(v_actor__307837, new System.Collections.Generic.List<object>(3) { F_dv_pair("decision_time", F_dv_get(F_dv_get(v_state, "physical"), "time")), F_dv_pair("decision_revision", F_dv_get(F_dv_get(v_actor__307837, "mind"), "revision")), F_dv_pair("idle_guard", v_advanced__309751) });
                                v_updated__306553 = F_dvs_replace(v_updated__306553, v_index__307761, v_sleeping__309910);
                                v_state = R.snapshot(F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(2) { F_dv_pair("actors", v_updated__306553), F_dv_pair("idle", R.add(F_dv_get(v_state, "idle"), R.number("1"))) }));
                            }
                            else
                            {
                                v_ready_index__307643 = v_index__307761;
                            }
                        }
                        object v_wake_guard__310329 = F_dv_find(v_actor__307837, "idle_guard", false);
                        if (R.Bool(R.not(R.eq(v_wake_guard__310329, false))))
                        {
                            if (R.Bool((R.Bool(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_get(v_wake_guard__310329, "until"))) ? (object)(R.lt(F_dv_get(v_wake_guard__310329, "until"), v_wake_time__307281)) : (object)(false))))
                            {
                                v_wake_index__307257 = v_index__307761;
                                v_wake_time__307281 = F_dv_get(v_wake_guard__310329, "until");
                            }
                            if (R.Bool((R.Bool((R.Bool(F_dv_find(v_wake_guard__310329, "declared_time_boundary", false)) ? (object)(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_get(v_wake_guard__310329, "until"))) : (object)(false))) ? (object)(R.lt(F_dv_get(v_wake_guard__310329, "until"), v_event_wake_time__307349)) : (object)(false))))
                            {
                                v_event_wake_index__307319 = v_index__307761;
                                v_event_wake_time__307349 = F_dv_get(v_wake_guard__310329, "until");
                            }
                        }
                    }
                }
                v_index__307761 = R.add(v_index__307761, R.number("1"));
                if (R.Bool(R.eq(v_index__307761, R.count(F_dv_get(v_state, "actors")))))
                {
                    v_index__307761 = R.number("0");
                }
                v_offset__307742 = R.add(v_offset__307742, R.number("1"));
            }
        }
        finally { _ = R.phase("schedule-scan-end", ""); }
        if (R.Bool((R.Bool(R.not(R.lt(v_due_index__307197, R.number("0")))) ? (object)(R.not(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), v_due_time__307220))) : (object)(false))))
        {
            return F_dvs_dispatch_due(v_state, v_due_index__307197);
        }
        if (R.Bool(R.not(R.lt(v_obligation_index__307668, R.number("0")))))
        {
            return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(1) { F_dv_pair("cursor", v_obligation_index__307668) });
        }
        if (R.Bool(R.not(R.lt(v_ready_index__307643, R.number("0")))))
        {
            return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(1) { F_dv_pair("cursor", v_ready_index__307643) });
        }
        if (R.Bool((R.Bool((R.Bool((R.Bool(R.lt(v_due_index__307197, R.number("0"))) ? (object)(R.not(R.lt(v_event_wake_index__307319, R.number("0")))) : (object)(false))) ? (object)(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), v_event_wake_time__307349)) : (object)(false))) ? (object)(R.not(R.lt(F_dv_get(v_state, "horizon"), v_event_wake_time__307349))) : (object)(false))))
        {
            object v_physical__311445 = F_dv_get(v_state, "physical");
            object v_events__311483 = F_dv_clone(F_dv_get(v_state, "events"));
            object v_parent__311527 = F_dvs_event(v_events__311483, "action_wait", "", F_dv_get(v_physical__311445, "time"), new System.Collections.Generic.List<object>(0) { }, new System.Collections.Generic.List<object>(3) { F_dv_pair("until", v_event_wake_time__307349), F_dv_pair("waiting", R.number("0")), F_dv_pair("reason", "declared-subject-time-boundary") });
            while (R.Bool(R.lt(F_dv_get(v_physical__311445, "time"), v_event_wake_time__307349)))
            {
                object v_next_time__311741 = F_dvs_environment_next_time(v_physical__311445, F_dv_get(v_state, "story"), v_event_wake_time__307349);
                v_physical__311445 = F_dvs_environment(F_dv_with(v_physical__311445, "time", v_next_time__311741), F_dv_get(v_state, "story"), v_events__311483, v_parent__311527);
            }
            return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(4) { F_dv_pair("physical", v_physical__311445), F_dv_pair("events", v_events__311483), F_dv_pair("cursor", v_event_wake_index__307319), F_dv_pair("idle", R.number("0")) });
        }
        if (R.Bool(R.not(R.lt(v_due_index__307197, R.number("0")))))
        {
            object v_physical__312086 = F_dv_get(v_state, "physical");
            object v_events__312124 = F_dv_clone(F_dv_get(v_state, "events"));
            object v_until__312168 = F_dv_min(v_due_time__307220, v_wake_time__307281);
            object v_parent__312216 = F_dvs_event(v_events__312124, "action_wait", "", F_dv_get(v_physical__312086, "time"), new System.Collections.Generic.List<object>(0) { }, new System.Collections.Generic.List<object>(2) { F_dv_pair("until", v_until__312168), F_dv_pair("waiting", F_dvs_scheduled_count(F_dv_get(v_state, "actors"))) });
            v_current__307173 = F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(2) { F_dv_pair("events", v_events__312124), F_dv_pair("idle", R.number("0")) });
            while (R.Bool(R.lt(F_dv_get(v_physical__312086, "time"), v_until__312168)))
            {
                object v_next_time__312555 = F_dvs_environment_next_time(v_physical__312086, F_dv_get(v_state, "story"), v_until__312168);
                if (R.Bool(R.lt(v_until__312168, v_next_time__312555)))
                {
                    v_next_time__312555 = v_until__312168;
                }
                v_physical__312086 = F_dvs_environment(F_dv_with(v_physical__312086, "time", v_next_time__312555), F_dv_get(v_state, "story"), v_events__312124, v_parent__312216);
                _ = R.phase("schedule-waits-start", "");
                try { v_current__307173 = R.snapshot(F_dvs_interrupt_waits(F_dvs_owned_state_update(v_current__307173, new System.Collections.Generic.List<object>(2) { F_dv_pair("physical", v_physical__312086), F_dv_pair("events", v_events__312124) }))); }
                finally { _ = R.phase("schedule-waits-end", ""); }
                object v_interrupted__312930 = F_dvs_interrupted_wait_due(F_dv_get(v_current__307173, "actors"), F_dv_get(v_physical__312086, "time"));
                if (R.Bool(R.not(R.lt(v_interrupted__312930, R.number("0")))))
                {
                    return F_dvs_dispatch_due(v_current__307173, v_interrupted__312930);
                }
            }
            if (R.Bool(R.lt(v_until__312168, v_due_time__307220)))
            {
                return F_dvs_owned_state_update(v_current__307173, new System.Collections.Generic.List<object>(1) { F_dv_pair("cursor", v_wake_index__307257) });
            }
            return F_dvs_dispatch_due(v_current__307173, v_due_index__307197);
        }
        return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(1) { F_dv_pair("idle", R.count(F_dv_get(v_state, "actors"))) });
    }

    public static object F_dvs_debug_phase(object v_story, object v_actor, object v_time, object v_phase)
    {
        _ = R.phase(v_phase, v_actor);
        if (R.Bool(F_dv_find(v_story, "debug_phase_progress", false)))
        {
            _ = R.print(R.json_stringify(new System.Collections.Generic.List<object>(4) { F_dv_pair("type", "engine_phase"), F_dv_pair("actor", v_actor), F_dv_pair("game_time", v_time), F_dv_pair("phase", v_phase) }));
        }
        return true;
    }

    public static object F_dvs_prepare_actor(object v_state)
    {
        if (R.Bool(R.not(R.eq(F_dv_find(R.at(F_dv_get(v_state, "actors"), F_dv_get(v_state, "cursor")), "scheduled_action", false), false))))
        {
            return v_state;
        }
        v_state = F_dvs_simulation_sync(v_state);
        v_state = R.snapshot(v_state);
        object v_story__313956 = F_dv_get(v_state, "story");
        object v_physical__313984 = F_dv_get(v_state, "physical");
        object v_actors__314018 = F_dv_get(v_state, "actors");
        object v_events__314048 = F_dv_clone(F_dv_get(v_state, "events"));
        object v_bindings__314088 = F_dv_get(v_state, "bindings");
        object v_costs__314122 = F_dv_get(v_state, "costs");
        object v_run_start__314150 = F_dv_get(v_state, "run_start");
        object v_duration_budget__314186 = F_dv_get(v_state, "duration_budget");
        object v_horizon__314234 = F_dv_get(v_state, "horizon");
        object v_duration_blocked__314266 = F_dv_get(v_state, "duration_blocked");
        object v_limit__314316 = F_dv_get(v_state, "limit");
        object v_cursor__314344 = F_dv_get(v_state, "cursor");
        object v_executed__314374 = F_dv_get(v_state, "executed");
        object v_idle__314408 = F_dv_get(v_state, "idle");
        object v_status__314434 = F_dv_get(v_state, "status");
        object v_initial__314464 = F_dv_get(v_state, "initial");
        object v_index__314496 = v_cursor__314344;
        object v_actor__314519 = F_dv_with_many(R.at(v_actors__314018, v_cursor__314344), new System.Collections.Generic.List<object>(2) { F_dv_pair("idle_guard", false), F_dv_pair("decision_time", F_dv_get(v_physical__313984, "time")) });
        if (R.Bool(F_dv_has_key(v_physical__313984, "simulation")))
        {
            v_actor__314519 = F_dv_with_many(v_actor__314519, new System.Collections.Generic.List<object>(2) { F_dv_pair("simulation_decision_macro", F_dv_get(F_dv_get(v_physical__313984, "simulation"), "macro_count")), F_dv_pair("simulation_decision_nearby", F_dv_get(F_dv_get(v_physical__313984, "simulation"), "last_nearby_second")) });
        }
        object v_parent__314832 = F_dv_get(R.at(v_events__314048, R.sub(R.count(v_events__314048), R.number("1"))), "id");
        object v_time__314883 = F_dv_find(v_physical__313984, "time", R.number("0"));
        object v_local_1__314927 = false;
        if (R.Bool(R.lt(R.add(v_cursor__314344, R.number("1")), R.count(v_actors__314018))))
        {
            v_local_1__314927 = R.add(v_cursor__314344, R.number("1"));
        }
        else
        {
            v_local_1__314927 = R.number("0");
        }
        v_cursor__314344 = v_local_1__314927;
        if (R.Bool((R.Bool(R.not(F_dvs_actor_can_act(v_actor__314519))) ? (object)(true) : (object)(R.not(R.lt(R.number("0"), F_dv_find(F_dv_get(v_actor__314519, "policy"), "budget", R.number("0"))))))))
        {
            v_idle__314408 = R.add(v_idle__314408, R.number("1"));
        }
        else
        {
            if (R.Bool(R.eq(F_dv_find(v_state, "player_selection", ""), "")))
            {
                v_actor__314519 = F_dv_with(v_actor__314519, "policy", F_dv_with(F_dv_get(v_actor__314519, "policy"), "remaining_game_time", R.sub(v_horizon__314234, v_time__314883)));
                v_actor__314519 = F_dvs_update_support_events(v_actor__314519, v_events__314048);
                _ = F_dvs_debug_phase(v_story__313956, F_dv_get(v_actor__314519, "id"), v_time__314883, "cognition-start");
                v_actor__314519 = F_dvs_drain_received_speech(v_actor__314519, v_events__314048, v_time__314883);
                v_actor__314519 = F_dvs_notice_peers(v_actor__314519, v_physical__313984, v_actors__314018, v_bindings__314088, v_events__314048, v_time__314883, v_parent__314832);
                v_actor__314519 = F_dvs_notice_visible_objects(v_actor__314519, v_physical__313984, v_bindings__314088, v_events__314048, v_time__314883, v_parent__314832);
                v_actor__314519 = F_dvs_prepare_cognition(v_actor__314519, v_events__314048, v_time__314883, v_parent__314832);
                _ = F_dvs_debug_phase(v_story__313956, F_dv_get(v_actor__314519, "id"), v_time__314883, "cognition-end");
            }
            v_actor__314519 = F_dvs_materialize_arguments(v_actor__314519, v_events__314048);
            object v_choice__315950 = false;
            if (R.Bool(R.eq(F_dv_find(v_state, "player_actor", ""), F_dv_get(v_actor__314519, "id"))))
            {
                object v_selection__316040 = F_dv_find(v_state, "player_selection", "");
                if (R.Bool(R.eq(v_selection__316040, "")))
                {
                    object v_options__316143 = F_dvs_player_options(v_actor__314519);
                    if (R.Bool(R.lt(R.number("0"), R.count(v_options__316143))))
                    {
                        object v_offered__316401 = F_dv_find(v_actor__314519, "offered_actions", new System.Collections.Generic.List<object>(0) { });
                        object v_option_index__316473 = R.number("0");
                        while (R.Bool(R.lt(v_option_index__316473, R.count(v_options__316143))))
                        {
                            object v_option__316578 = R.at(v_options__316143, v_option_index__316473);
                            if (R.Bool(R.not(F_dv_has(v_offered__316401, F_dv_get(v_option__316578, "id")))))
                            {
                                v_offered__316401 = F_dv_append(v_offered__316401, F_dv_get(v_option__316578, "id"));
                            }
                            v_option_index__316473 = R.add(v_option_index__316473, R.number("1"));
                        }
                        v_actor__314519 = F_dv_with(v_actor__314519, "offered_actions", v_offered__316401);
                        v_actors__314018 = F_dvs_replace(v_actors__314018, v_index__314496, v_actor__314519);
                        return F_dvs_control_state(v_state, v_actors__314018, v_events__314048, v_index__314496, F_dv_find(v_state, "player_actor", ""), "", true);
                    }
                }
                if (R.Bool(R.eq(v_selection__316040, "")))
                {
                    v_choice__315950 = new System.Collections.Generic.List<object>(4) { F_dv_pair("action", false), F_dv_pair("score", R.number("0")), F_dv_pair("plan_expansions", R.number("0")), F_dv_pair("plans", new System.Collections.Generic.List<object>(0) { }) };
                }
                else
                {
                    v_choice__315950 = F_dvs_player_choice(v_actor__314519, v_selection__316040);
                }
            }
            else
            {
                _ = F_dvs_debug_phase(v_story__313956, F_dv_get(v_actor__314519, "id"), v_time__314883, "choice-start");
                _ = R.phase("decision-compute-start", F_dv_get(v_actor__314519, "id"));
                v_actor__314519 = F_dvs_navigation_task_sync(v_actor__314519, v_events__314048, v_time__314883, v_physical__313984);
                v_choice__315950 = F_dvs_choose(v_actor__314519, v_bindings__314088, v_costs__314122);
                object navigationDiagnostic = F_dvs_navigation_task_diagnostics(v_actor__314519, v_choice__315950);
                object declaredCount = R.number("0");
                foreach (object row in R.Items(navigationDiagnostic))
                    if (R.Bool(F_dv_find(row, "declared", false))) declaredCount = R.add(declaredCount, R.number("1"));
                _ = F_dvs_event(v_events__314048, "navigation_task_diagnostic", F_dv_get(v_actor__314519, "id"), v_time__314883, F_dv_single(v_parent__314832), new List<object> { F_dv_pair("rows", navigationDiagnostic), F_dv_pair("row_count", R.count(navigationDiagnostic)), F_dv_pair("declared_count", declaredCount), F_dv_pair("route_search", F_dv_find(v_actor__314519, "navigation_route_diagnostic", false)) });
                _ = R.phase("decision-compute-end", F_dv_get(v_actor__314519, "id"));
                _ = F_dvs_debug_phase(v_story__313956, F_dv_get(v_actor__314519, "id"), v_time__314883, "choice-end");
            }
            v_choice__315950 = F_dv_with_many(v_choice__315950, new System.Collections.Generic.List<object>(5) { F_dv_pair("cognition_reused", F_dv_find(v_actor__314519, "cognition_reused", false)), F_dv_pair("reason_reused", F_dv_find(v_actor__314519, "reason_reused", false)), F_dv_pair("cognition_stop", F_dv_find(v_actor__314519, "cognition_stop", "ordinary-cognition-pass")), F_dv_pair("evaluated_at_game_time", v_time__314883), F_dv_pair("mind_revision", F_dv_get(F_dv_get(v_actor__314519, "mind"), "revision")) });
            object v_action__317883 = F_dv_get(v_choice__315950, "action");
            v_actor__314519 = F_dv_with(v_actor__314519, "decision_revision", F_dv_get(F_dv_get(v_actor__314519, "mind"), "revision"));
            if (R.Bool(R.eq(v_action__317883, false)))
            {
                _ = R.phase("prepare-idle-start", "");
                try
                {
                    if (R.Bool(R.lt(R.number("0"), F_dv_find(v_choice__315950, "time_blocked", R.number("0")))))
                    {
                        v_duration_blocked__314266 = true;
                    }
                    v_idle__314408 = R.add(v_idle__314408, R.number("1"));
                    object v_stop__318152 = F_dvs_event(v_events__314048, "planning_stop", F_dv_get(v_actor__314519, "id"), v_time__314883, F_dv_single(v_parent__314832), v_choice__315950);
                    if (R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_state, "player_actor", ""), F_dv_get(v_actor__314519, "id")))) ? (object)(R.eq(F_dv_find(v_choice__315950, "time_blocked", R.number("0")), R.number("0"))) : (object)(false))) ? (object)(F_dvs_idle_can_wait(v_actor__314519)) : (object)(false))))
                    {
                        object v_boundary__318400 = F_dvs_idle_window_boundary(v_actor__314519, v_story__313956, v_time__314883, v_horizon__314234);
                        if (R.Bool(R.lt(v_time__314883, F_dv_get(v_boundary__318400, "next"))))
                        {
                            object v_guard__318530 = new System.Collections.Generic.List<object>(13) { F_dv_pair("time", v_time__314883), F_dv_pair("until", F_dv_get(v_boundary__318400, "next")), F_dv_pair("source_stop", v_stop__318152), F_dv_pair("proof", "approximate-yield"), F_dv_pair("time_analysis_complete", F_dv_get(v_boundary__318400, "complete")), F_dv_pair("declared_time_boundary", (R.Bool(F_dv_get(v_boundary__318400, "complete")) ? (object)(R.lt(F_dv_get(v_boundary__318400, "next"), v_horizon__314234)) : (object)(false))), F_dv_pair("subject", F_dvs_idle_subject_input(v_actor__314519, v_costs__314122)), F_dv_pair("contact", F_dvs_idle_contact_input(v_actor__314519, v_physical__313984, v_actors__314018)), F_dv_pair("bindings", v_bindings__314088), F_dv_pair("costs", v_costs__314122), F_dv_pair("story", F_dvs_idle_story_input(v_story__313956)), F_dv_pair("event_cursor", R.count(v_events__314048)), F_dv_pair("event_anchor", R.at(v_events__314048, R.sub(R.count(v_events__314048), R.number("1")))) };
                            v_actor__314519 = F_dv_with(v_actor__314519, "idle_guard", R.snapshot(v_guard__318530));
                        }
                    }
                }
                finally { _ = R.phase("prepare-idle-end", ""); }
            }
            else
            {
                if (R.Bool(F_dv_has_key(v_action__317883, "base_action_id")) && R.Bool(F_dv_has_key(v_action__317883, "navigation_task")))
                {
                    object candidateId = F_dv_get(v_action__317883, "id");
                    v_action__317883 = F_dv_with_many(v_action__317883, new List<object> { F_dv_pair("navigation_candidate_id", candidateId), F_dv_pair("id", F_dv_get(v_action__317883, "base_action_id")) });
                }
                v_action__317883 = F_dv_with(v_action__317883, "label", F_dvs_action_label(v_actor__314519, v_action__317883));
                object v_id__319197 = F_dv_get(v_actor__314519, "id");
                object v_mind__319227 = F_dv_get(v_actor__314519, "mind");
                object v_model__319261 = F_dv_get(v_costs__314122, F_dv_get(v_action__317883, "kind"));
                object v_action_event__319312 = R.cat("action", R.show(v_executed__314374));
                object v_ledger__319379 = F_dv_get(v_actor__314519, "ledger");
                _ = R.phase("selection-evidence-start", v_id__319197);
                object v_selection_parents__319470 = F_dvs_selection_parents(v_actor__314519, v_action__317883, v_choice__315950, v_events__314048, v_parent__314832);
                _ = R.phase("selection-evidence-end", v_id__319197);
                _ = R.phase("decision-record-start", v_id__319197);
                v_parent__314832 = F_dvs_event(v_events__314048, "action_selected", v_id__319197, v_time__314883, v_selection_parents__319470, F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("action", v_action__317883), F_dv_pair("revision_before", F_dv_get(v_mind__319227, "revision")), F_dv_pair("score", F_dv_get(v_choice__315950, "score")), F_dv_pair("hypothesis", F_dv_find(v_actor__314519, "hypothesis", false)) }, new System.Collections.Generic.List<object>(8) { F_dv_pair("plan_expansions", F_dv_get(v_choice__315950, "plan_expansions")), F_dv_pair("plans", F_dv_get(v_choice__315950, "plans")), F_dv_pair("selected_path", F_dv_find(v_choice__315950, "selected_path", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("planning_stop", F_dv_find(v_choice__315950, "stop", "manual-choice")), F_dv_pair("planning_strategy", F_dv_find(v_choice__315950, "strategy", "manual-choice")), F_dv_pair("planning_approximation", F_dv_find(v_choice__315950, "approximation", false)), F_dv_pair("planning_work", F_dv_find(v_choice__315950, "planning_work", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("owned_decision", F_dvs_owned_decision_selection(v_actor__314519, v_action__317883)) }));
                v_parent__314832 = F_dvs_event(v_events__314048, "planning_choice", v_id__319197, v_time__314883, F_dv_single(v_parent__314832), v_choice__315950);
                object v_bounds__320442 = F_dv_action_cost_bounds(F_dv_get(v_model__319261, "minimum"), F_dv_get(v_model__319261, "maximum"));
                if (R.Bool(F_dv_or(R.lt(v_horizon__314234, R.add(v_time__314883, F_dv_get(F_dv_get(v_bounds__320442, "maximum"), "time"))), R.lt(v_horizon__314234, R.add(v_time__314883, F_dv_get(F_dv_get(v_model__319261, "actual"), "time"))))))
                {
                    v_duration_blocked__314266 = true;
                    v_idle__314408 = R.add(v_idle__314408, R.number("1"));
                    _ = F_dvs_event(v_events__314048, "duration_budget_rejected", v_id__319197, v_time__314883, F_dv_single(v_parent__314832), new System.Collections.Generic.List<object>(4) { F_dv_pair("action", v_action__317883), F_dv_pair("bounds", v_bounds__320442), F_dv_pair("actual", F_dv_get(v_model__319261, "actual")), F_dv_pair("time_origin", new System.Collections.Generic.List<object>(4) { F_dv_pair("run_start", v_run_start__314150), F_dv_pair("duration_budget", v_duration_budget__314186), F_dv_pair("horizon", v_horizon__314234), F_dv_pair("unit", "minute") }) });
                }
                else
                {
                    if (R.Bool(R.not(F_dv_action_cost_le(F_dv_get(v_bounds__320442, "maximum"), F_dv_get(v_ledger__319379, "balances")))))
                    {
                        _ = F_dvs_event(v_events__314048, "resource_rejected", v_id__319197, v_time__314883, F_dv_single(v_parent__314832), new System.Collections.Generic.List<object>(2) { F_dv_pair("action", v_action__317883), F_dv_pair("ledger", v_ledger__319379) });
                        v_idle__314408 = R.add(v_idle__314408, R.number("1"));
                    }
                    else
                    {
                        object v_queued__321254 = new System.Collections.Generic.List<object>(6) { F_dv_pair("action", v_action__317883), F_dv_pair("choice", new System.Collections.Generic.List<object>(2) { F_dv_pair("relevant_refs", F_dv_get(v_choice__315950, "relevant_refs")), F_dv_pair("selected_path", F_dv_find(v_choice__315950, "selected_path", new System.Collections.Generic.List<object>(0) { })) }), F_dv_pair("model", v_model__319261), F_dv_pair("start", v_time__314883), F_dv_pair("due", R.add(v_time__314883, F_dv_get(F_dv_get(v_model__319261, "actual"), "time"))), F_dv_pair("parent", v_parent__314832) };
                        if (R.Bool(F_dv_find(v_action__317883, "interruptible", false)))
                        {
                            _ = F_dvs_validate_interruptible_wait(v_action__317883, v_model__319261);
                            v_queued__321254 = F_dv_with(v_queued__321254, "wait_watch", new System.Collections.Generic.List<object>(2) { F_dv_pair("contact", F_dvs_idle_contact_input(v_actor__314519, v_physical__313984, v_actors__314018)), F_dv_pair("event_cursor", R.count(v_events__314048)) });
                        }
                        v_actor__314519 = F_dvs_navigation_task_commit(v_actor__314519, v_action__317883, v_events__314048, v_time__314883, v_parent__314832);
                        v_actor__314519 = F_dv_with(v_actor__314519, "scheduled_action", v_queued__321254);
                        _ = F_dvs_event(v_events__314048, "action_scheduled", v_id__319197, v_time__314883, new System.Collections.Generic.List<object>(1) { v_parent__314832 }, new System.Collections.Generic.List<object>(3) { F_dv_pair("action_id", F_dv_get(v_action__317883, "id")), F_dv_pair("due", F_dv_get(v_queued__321254, "due")), F_dv_pair("duration", F_dv_get(F_dv_get(v_model__319261, "actual"), "time")) });
                        v_idle__314408 = R.number("0");
                    }
                }
                _ = R.phase("decision-record-end", v_id__319197);
            }
        }
        v_actors__314018 = F_dvs_replace(v_actors__314018, v_index__314496, v_actor__314519);
        return new System.Collections.Generic.List<object>(19) { F_dv_pair("player_actor", F_dv_find(v_state, "player_actor", "")), F_dv_pair("story", v_story__313956), F_dv_pair("physical", v_physical__313984), F_dv_pair("actors", v_actors__314018), F_dv_pair("events", v_events__314048), F_dv_pair("bindings", v_bindings__314088), F_dv_pair("costs", v_costs__314122), F_dv_pair("run_start", v_run_start__314150), F_dv_pair("duration_budget", v_duration_budget__314186), F_dv_pair("horizon", v_horizon__314234), F_dv_pair("duration_blocked", v_duration_blocked__314266), F_dv_pair("limit", v_limit__314316), F_dv_pair("cursor", v_cursor__314344), F_dv_pair("executed", v_executed__314374), F_dv_pair("idle", v_idle__314408), F_dv_pair("status", v_status__314434), F_dv_pair("initial", v_initial__314464), F_dv_pair("awaiting_player", false), F_dv_pair("player_selection", "") };
    }
}
