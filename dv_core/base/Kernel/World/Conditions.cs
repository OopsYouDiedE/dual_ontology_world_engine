using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>World / Conditions: native Core mechanisms.</summary>
public static partial class DVKernel
{
    // The content supplies the temporal reference and contextual object identities.
    public static object F_dv_condition_bounds(object condition, object clockReference, object includeNegated)
    {
        var key = new List<object> { "condition-bounds", clockReference, includeNegated };
        var cached = R.Items(R.memo_get(condition, key));
        if (R.Bool(cached[0])) return cached[1];
        var result = new List<object>();
        var op = R.Text(F_dv_find(condition, "op", ""));
        if (R.Bool(includeNegated) && op == "not")
            return R.memo_put(condition, key, F_dv_condition_bounds(F_dv_get(condition, "item"), clockReference, true));
        if (op == "at_least" && R.Bool(F_dv_same(F_dv_get(condition, "ref"), clockReference)))
            result.Add(F_dv_get(condition, "value"));
        if (op == "all")
        {
            foreach (var child in R.Items(F_dv_get(condition, "items")))
                result.AddRange(R.Items(F_dv_condition_bounds(child, clockReference, false)));
            if (R.Bool(includeNegated))
                foreach (var child in R.Items(F_dv_get(condition, "items")))
                    foreach (var value in R.Items(F_dv_condition_bounds(child, clockReference, true)))
                        if (!R.Bool(F_dv_has(result, value))) result.Add(value);
        }
        return R.memo_put(condition, key, result);
    }

    public static object F_dv_condition_without_context(object condition, object clockReference, object contextualObjects)
    {
        bool Contextual(object node)
        {
            var reference = F_dv_find(node, "ref", false);
            return reference is not false && (R.Bool(F_dv_has(contextualObjects, F_dv_find(reference, "object", ""))) ||
                (R.Text(F_dv_find(node, "op", "")) == "at_least" && R.Bool(F_dv_same(reference, clockReference))));
        }
        var op = R.Text(F_dv_find(condition, "op", ""));
        if (Contextual(condition) || (op == "not" && Contextual(F_dv_get(condition, "item")))) return F_dv_true();
        if (op == "all") return F_dv_all(R.Items(F_dv_get(condition, "items"))
            .Select(child => F_dv_condition_without_context(child, clockReference, contextualObjects)).ToList());
        return condition;
    }

    public static object F_dv_true()
    {
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("op", "constant"), F_dv_pair("value", "true") };
    }

    public static object F_dv_fact(object v_ref, object v_expected)
    {
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("op", "equals"), F_dv_pair("ref", v_ref), F_dv_pair("value", v_expected) };
    }

    public static object F_dv_at_least(object v_ref, object v_minimum)
    {
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("op", "at_least"), F_dv_pair("ref", v_ref), F_dv_pair("value", v_minimum) };
    }

    public static object F_dv_all(object v_conditions)
    {
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("op", "all"), F_dv_pair("items", v_conditions) };
    }

    public static object F_dv_not(object v_condition)
    {
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("op", "not"), F_dv_pair("item", v_condition) };
    }

    public static object F_dv_time_materialize(object v_world, object v_condition)
    {
        if (R.Bool(R.eq(F_dv_get(v_condition, "op"), "held_for")))
        {
            object v_cell__14655 = F_dv_read_query(v_world, F_dv_get(v_condition, "ref"));
            if (R.Bool((R.Bool(F_dv_get(v_cell__14655, "known")) ? (object)(R.not(F_dv_get(v_cell__14655, "conflicted"))) : (object)(false))))
            {
                if (R.Bool(R.not(F_dv_same(F_dv_get(v_cell__14655, "value"), F_dv_get(v_condition, "value")))))
                {
                    return F_dv_fact(F_dv_get(v_condition, "ref"), F_dv_get(v_condition, "value"));
                }
                return F_dv_all(new System.Collections.Generic.List<object>(2) { F_dv_fact(F_dv_get(v_condition, "ref"), F_dv_get(v_condition, "value")), F_dv_at_least(F_dv_get(v_condition, "clock"), R.add(F_dv_get(v_cell__14655, "time"), F_dv_get(v_condition, "duration"))) });
            }
            return v_condition;
        }
        if (R.Bool(R.eq(F_dv_get(v_condition, "op"), "not")))
        {
            return F_dv_not(F_dv_time_materialize(v_world, F_dv_get(v_condition, "item")));
        }
        if (R.Bool(R.eq(F_dv_get(v_condition, "op"), "all")))
        {
            object v_items__15153 = new System.Collections.Generic.List<object>(0) { };
            object v_i__15176 = R.number("0");
            while (R.Bool(R.lt(v_i__15176, R.count(F_dv_get(v_condition, "items")))))
            {
                _ = R.push(v_items__15153, F_dv_time_materialize(v_world, R.at(F_dv_get(v_condition, "items"), v_i__15176)));
                v_i__15176 = R.add(v_i__15176, R.number("1"));
            }
            return F_dv_all(v_items__15153);
        }
        return v_condition;
    }

    public static object F_dv_matches(object v_object_id, object v_facet, object v_fields)
    {
        return F_dv_all(R.Block(() => { object comp_source_15579 = v_fields; var comp_result_15579 = new System.Collections.Generic.List<object>(); for (int comp_i_15579 = 0; comp_i_15579 < R.Items(comp_source_15579).Count; comp_i_15579++) { object v_entry__comp_15579 = R.at(comp_source_15579, (double)comp_i_15579); if (R.Bool(true)) comp_result_15579.Add(F_dv_fact(F_dv_ref(v_object_id, v_facet, R.at(v_entry__comp_15579, R.number("0"))), R.at(v_entry__comp_15579, R.number("1")))); } return (object)comp_result_15579; }));
    }

    public static object F_dv_writes(object v_object_id, object v_facet, object v_fields)
    {
        return R.Block(() => { object comp_source_15696 = v_fields; var comp_result_15696 = new System.Collections.Generic.List<object>(); for (int comp_i_15696 = 0; comp_i_15696 < R.Items(comp_source_15696).Count; comp_i_15696++) { object v_entry__comp_15696 = R.at(comp_source_15696, (double)comp_i_15696); if (R.Bool(true)) comp_result_15696.Add(F_dv_assign(F_dv_ref(v_object_id, v_facet, R.at(v_entry__comp_15696, R.number("0"))), R.at(v_entry__comp_15696, R.number("1")))); } return (object)comp_result_15696; });
    }

    public static object F_dv_condition_inputs(object v_condition)
    {
        object v_unsupported__15892 = new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", false), F_dv_pair("reads", new System.Collections.Generic.List<object>(0) { }) };
        if (R.Bool((R.Bool(R.not(F_dv_record_like(v_condition))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_condition, "op"))))))
        {
            return v_unsupported__15892;
        }
        object v_op__16047 = F_dv_get(v_condition, "op");
        if (R.Bool(R.eq(v_op__16047, "constant")))
        {
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", (R.Bool(F_dv_has_key(v_condition, "value")) ? (object)(F_dv_has(new System.Collections.Generic.List<object>(3) { "true", "false", "unknown" }, F_dv_find(v_condition, "value", ""))) : (object)(false))), F_dv_pair("reads", new System.Collections.Generic.List<object>(0) { }) };
        }
        if (R.Bool(R.eq(v_op__16047, "not")))
        {
            if (R.Bool(R.not(F_dv_has_key(v_condition, "item"))))
            {
                return v_unsupported__15892;
            }
            return F_dv_condition_inputs(F_dv_get(v_condition, "item"));
        }
        if (R.Bool(R.eq(v_op__16047, "all")))
        {
            if (R.Bool((R.Bool(R.not(F_dv_has_key(v_condition, "items"))) ? (object)(true) : (object)(R.not(R.is_list(F_dv_get(v_condition, "items")))))))
            {
                return v_unsupported__15892;
            }
            object v_reads__16530 = new System.Collections.Generic.List<object>(0) { };
            object v_complete__16553 = true;
            object v_i__16581 = R.number("0");
            while (R.Bool(R.lt(v_i__16581, R.count(F_dv_get(v_condition, "items")))))
            {
                object v_part__16648 = F_dv_condition_inputs(R.at(F_dv_get(v_condition, "items"), v_i__16581));
                v_complete__16553 = (R.Bool(v_complete__16553) ? (object)(F_dv_get(v_part__16648, "complete")) : (object)(false));
                object v_j__16761 = R.number("0");
                while (R.Bool(R.lt(v_j__16761, R.count(F_dv_get(v_part__16648, "reads")))))
                {
                    if (R.Bool(R.not(F_dv_has(v_reads__16530, R.at(F_dv_get(v_part__16648, "reads"), v_j__16761)))))
                    {
                        _ = R.push(v_reads__16530, F_dv_clone(R.at(F_dv_get(v_part__16648, "reads"), v_j__16761)));
                    }
                    v_j__16761 = R.add(v_j__16761, R.number("1"));
                }
                v_i__16581 = R.add(v_i__16581, R.number("1"));
            }
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", v_complete__16553), F_dv_pair("reads", v_reads__16530) };
        }
        if (R.Bool(R.eq(v_op__16047, "held_for")))
        {
            if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(F_dv_has_key(v_condition, "ref"))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_condition, "value"))))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_condition, "clock"))))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_condition, "duration"))))))
            {
                return v_unsupported__15892;
            }
            object v_target__17256 = F_dv_condition_inputs(F_dv_fact(F_dv_get(v_condition, "ref"), F_dv_get(v_condition, "value")));
            object v_clock__17338 = F_dv_condition_inputs(F_dv_at_least(F_dv_get(v_condition, "clock"), R.number("0")));
            if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(F_dv_get(v_target__17256, "complete"))) ? (object)(true) : (object)(R.not(F_dv_get(v_clock__17338, "complete"))))) ? (object)(true) : (object)(R.not(R.is_numeric_value(F_dv_get(v_condition, "duration")))))) ? (object)(true) : (object)(R.lt(F_dv_get(v_condition, "duration"), R.number("0"))))))
            {
                return v_unsupported__15892;
            }
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", true), F_dv_pair("reads", new System.Collections.Generic.List<object>(2) { F_dv_get(v_condition, "ref"), F_dv_get(v_condition, "clock") }) };
        }
        if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.eq(v_op__16047, "equals"))) ? (object)(R.not(R.eq(v_op__16047, "at_least"))) : (object)(false))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_condition, "ref"))))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_condition, "value"))))))
        {
            return v_unsupported__15892;
        }
        object v_ref__17789 = F_dv_get(v_condition, "ref");
        if (R.Bool((R.Bool(R.not(F_dv_record_like(v_ref__17789))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_ref__17789, "type", ""), "ref"))))))
        {
            return v_unsupported__15892;
        }
        if (R.Bool((R.Bool((R.Bool(R.not(R.is_text(F_dv_find(v_ref__17789, "object", false)))) ? (object)(true) : (object)(R.not(R.is_text(F_dv_find(v_ref__17789, "facet", false)))))) ? (object)(true) : (object)(R.not(R.is_text(F_dv_find(v_ref__17789, "field", false)))))))
        {
            return v_unsupported__15892;
        }
        if (R.Bool(R.not(R.is_text(F_dv_find(v_ref__17789, "subject", "")))))
        {
            return v_unsupported__15892;
        }
        if (R.Bool((R.Bool(R.eq(v_op__16047, "at_least")) ? (object)(R.not(R.is_numeric_value(F_dv_get(v_condition, "value")))) : (object)(false))))
        {
            return v_unsupported__15892;
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", true), F_dv_pair("reads", new System.Collections.Generic.List<object>(1) { F_dv_clone(v_ref__17789) }) };
    }

    public static object F_dv_time_ref_relation(object v_ref, object v_clock)
    {
        if (R.Bool((R.Bool(R.not(F_dv_record_like(v_clock))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_clock, "type", ""), "ref"))))))
        {
            return "unknown";
        }
        if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.is_text(F_dv_find(v_clock, "object", false)))) ? (object)(true) : (object)(R.not(R.is_text(F_dv_find(v_clock, "facet", false)))))) ? (object)(true) : (object)(R.not(R.is_text(F_dv_find(v_clock, "field", false)))))) ? (object)(true) : (object)(R.not(R.is_text(F_dv_find(v_clock, "subject", "")))))))
        {
            return "unknown";
        }
        if (R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_ref, "object"), F_dv_get(v_clock, "object")))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_ref, "facet"), F_dv_get(v_clock, "facet")))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_ref, "field"), F_dv_get(v_clock, "field")))))))
        {
            return "different";
        }
        if (R.Bool(R.not(R.eq(F_dv_find(v_ref, "subject", ""), F_dv_find(v_clock, "subject", "")))))
        {
            return "unknown";
        }
        return "same";
    }

    public static object F_dv_time_boundary(object v_condition, object v_clock, object v_now, object v_until, object v_policy)
    {
        _ = F_dv_require((R.Bool(R.eq(v_policy, "invalidate")) ? (object)(true) : (object)(R.eq(v_policy, "minute"))), "Unknown time analysis policy");
        _ = F_dv_require((R.Bool(R.is_numeric_value(v_now)) ? (object)(R.is_numeric_value(v_until)) : (object)(false)), "Invalid time analysis interval");
        if (R.Bool(R.lt(v_until, v_now)))
        {
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", false), F_dv_pair("next", v_now) };
        }
        object v_fallback__19546 = new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", false), F_dv_pair("next", F_dv_min(v_until, R.add(v_now, R.number("1")))) };
        if (R.Bool((R.Bool(R.not(F_dv_record_like(v_condition))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_condition, "op"))))))
        {
            return v_fallback__19546;
        }
        object v_op__19714 = F_dv_get(v_condition, "op");
        if (R.Bool(R.eq(v_op__19714, "constant")))
        {
            if (R.Bool((R.Bool(R.not(F_dv_has_key(v_condition, "value"))) ? (object)(true) : (object)(R.not(F_dv_has(new System.Collections.Generic.List<object>(3) { "true", "false", "unknown" }, F_dv_get(v_condition, "value")))))))
            {
                return v_fallback__19546;
            }
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", true), F_dv_pair("next", v_until) };
        }
        if (R.Bool(R.eq(v_op__19714, "not")))
        {
            if (R.Bool(R.not(F_dv_has_key(v_condition, "item"))))
            {
                return v_fallback__19546;
            }
            return F_dv_time_boundary(F_dv_get(v_condition, "item"), v_clock, v_now, v_until, v_policy);
        }
        if (R.Bool(R.eq(v_op__19714, "all")))
        {
            if (R.Bool((R.Bool(R.not(F_dv_has_key(v_condition, "items"))) ? (object)(true) : (object)(R.not(R.is_list(F_dv_get(v_condition, "items")))))))
            {
                return v_fallback__19546;
            }
            object v_next__20253 = v_until;
            object v_i__20278 = R.number("0");
            while (R.Bool(R.lt(v_i__20278, R.count(F_dv_get(v_condition, "items")))))
            {
                object v_part__20345 = F_dv_time_boundary(R.at(F_dv_get(v_condition, "items"), v_i__20278), v_clock, v_now, v_until, v_policy);
                if (R.Bool(R.not(F_dv_get(v_part__20345, "complete"))))
                {
                    return v_fallback__19546;
                }
                v_next__20253 = F_dv_min(v_next__20253, F_dv_get(v_part__20345, "next"));
                v_i__20278 = R.add(v_i__20278, R.number("1"));
            }
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", true), F_dv_pair("next", v_next__20253) };
        }
        if (R.Bool(R.eq(v_op__19714, "held_for")))
        {
            return v_fallback__19546;
        }
        object v_inputs__20803 = F_dv_condition_inputs(v_condition);
        if (R.Bool(R.not(F_dv_get(v_inputs__20803, "complete"))))
        {
            return v_fallback__19546;
        }
        if (R.Bool(R.eq(v_clock, false)))
        {
            if (R.Bool(R.eq(v_policy, "minute")))
            {
                return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", true), F_dv_pair("next", v_until) };
            }
            return v_fallback__19546;
        }
        object v_relation__21034 = F_dv_time_ref_relation(F_dv_get(v_condition, "ref"), v_clock);
        if (R.Bool(R.eq(v_relation__21034, "unknown")))
        {
            return v_fallback__19546;
        }
        if (R.Bool(R.eq(v_relation__21034, "different")))
        {
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", true), F_dv_pair("next", v_until) };
        }
        if (R.Bool(R.not(R.is_numeric_value(F_dv_get(v_condition, "value")))))
        {
            return v_fallback__19546;
        }
        if (R.Bool(R.eq(v_policy, "minute")))
        {
            if (R.Bool(R.not(R.eq(v_op__19714, "at_least"))))
            {
                return v_fallback__19546;
            }
            if (R.Bool(R.lt(v_until, F_dv_get(v_condition, "value"))))
            {
                return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", true), F_dv_pair("next", v_until) };
            }
            if (R.Bool(R.lt(v_now, F_dv_get(v_condition, "value"))))
            {
                object v_delta__21519 = R.sub(F_dv_get(v_condition, "value"), v_now);
                object v_steps__21565 = R.number("1");
                while (R.Bool(R.lt(v_steps__21565, v_delta__21519)))
                {
                    v_steps__21565 = R.add(v_steps__21565, R.number("1"));
                }
                return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", true), F_dv_pair("next", F_dv_min(v_until, R.add(v_now, v_steps__21565))) };
            }
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", true), F_dv_pair("next", v_until) };
        }
        if (R.Bool(R.lt(v_now, F_dv_get(v_condition, "value"))))
        {
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", true), F_dv_pair("next", F_dv_min(v_until, F_dv_get(v_condition, "value"))) };
        }
        if (R.Bool((R.Bool(R.eq(v_op__19714, "equals")) ? (object)(R.eq(v_now, F_dv_get(v_condition, "value"))) : (object)(false))))
        {
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", true), F_dv_pair("next", v_now) };
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", true), F_dv_pair("next", v_until) };
    }

    public static object F_dv_analyze_time(object v_condition, object v_clock, object v_now, object v_until, object v_policy)
    {
        object v_boundary__22078 = F_dv_time_boundary(v_condition, v_clock, v_now, v_until, v_policy);
        object v_inputs__22152 = F_dv_condition_inputs(v_condition);
        object v_solver__22200 = "minute-step";
        object v_reason__22231 = "unsupported-predicate";
        if (R.Bool(F_dv_get(v_boundary__22078, "complete")))
        {
            v_solver__22200 = "event-boundary";
            v_reason__22231 = "declared-boundary";
        }
        if (R.Bool((R.Bool((R.Bool(R.not(F_dv_get(v_boundary__22078, "complete"))) ? (object)(F_dv_get(v_inputs__22152, "complete")) : (object)(false))) ? (object)((R.Bool(R.eq(F_dv_find(v_condition, "op", ""), "equals")) ? (object)(true) : (object)(R.eq(F_dv_find(v_condition, "op", ""), "at_least")))) : (object)(false))))
        {
            if (R.Bool(R.eq(v_clock, false)))
            {
                v_reason__22231 = "clock-not-declared";
            }
            else
            {
                if (R.Bool(R.eq(F_dv_time_ref_relation(F_dv_get(v_condition, "ref"), v_clock), "unknown")))
                {
                    v_reason__22231 = "ambiguous-clock-reference";
                }
                else
                {
                    if (R.Bool((R.Bool(R.eq(v_policy, "minute")) ? (object)(R.eq(F_dv_get(v_condition, "op"), "equals")) : (object)(false))))
                    {
                        v_reason__22231 = "clock-equality-needs-sampling";
                    }
                }
            }
        }
        if (R.Bool(R.lt(v_until, v_now)))
        {
            v_solver__22200 = "none";
            v_reason__22231 = "expired-interval";
        }
        return F_dv_with_many(v_boundary__22078, new System.Collections.Generic.List<object>(9) { F_dv_pair("solver", v_solver__22200), F_dv_pair("reason", v_reason__22231), F_dv_pair("policy", v_policy), F_dv_pair("from", v_now), F_dv_pair("until", v_until), F_dv_pair("reads", F_dv_get(v_inputs__22152, "reads")), F_dv_pair("scope", "declaration"), F_dv_pair("assumptions", new System.Collections.Generic.List<object>(2) { "non-clock inputs remain stable", "caller rechecks state changes and ordered effects" }), F_dv_pair("numeric_semantics", "runtime-values") });
    }

    public static object F_dv_analyze_law(object v_world, object v_name)
    {
        object v_adoption__23443 = F_dv_get(F_dv_get(v_world, "adoptions"), v_name);
        object v_law__23492 = F_dv_get(v_adoption__23443, "law");
        object v_inputs__23519 = F_dv_condition_inputs(F_dv_get(v_law__23492, "premise"));
        object v_unknown__23569 = new System.Collections.Generic.List<object>(0) { };
        object v_conflicted__23590 = new System.Collections.Generic.List<object>(0) { };
        object v_i__23614 = R.number("0");
        while (R.Bool(R.lt(v_i__23614, R.count(F_dv_get(v_inputs__23519, "reads")))))
        {
            object v_ref__23670 = R.at(F_dv_get(v_inputs__23519, "reads"), v_i__23614);
            object v_cell__23704 = F_dv_read_query(v_world, v_ref__23670);
            if (R.Bool(R.not(F_dv_get(v_cell__23704, "known"))))
            {
                _ = R.push(v_unknown__23569, v_ref__23670);
            }
            if (R.Bool(F_dv_get(v_cell__23704, "conflicted")))
            {
                _ = R.push(v_conflicted__23590, v_ref__23670);
            }
            v_i__23614 = R.add(v_i__23614, R.number("1"));
        }
        object v_truth__23883 = "unknown";
        if (R.Bool(F_dv_get(v_inputs__23519, "complete")))
        {
            v_truth__23883 = F_dv_truth(v_world, F_dv_get(v_law__23492, "premise"));
        }
        return new System.Collections.Generic.List<object>(15) { F_dv_pair("world", F_dv_get(v_world, "id")), F_dv_pair("subject", F_dv_get(v_world, "subject")), F_dv_pair("revision", F_dv_get(v_world, "revision")), F_dv_pair("law", v_name), F_dv_pair("law_version", F_dv_get(v_law__23492, "version")), F_dv_pair("mode", F_dv_get(v_adoption__23443, "mode")), F_dv_pair("active", F_dv_get(v_adoption__23443, "active")), F_dv_pair("premise", v_truth__23883), F_dv_pair("complete", F_dv_get(v_inputs__23519, "complete")), F_dv_pair("reads", F_dv_get(v_inputs__23519, "reads")), F_dv_pair("unknown", v_unknown__23569), F_dv_pair("conflicted", v_conflicted__23590), F_dv_pair("effects_verified", false), F_dv_pair("scope", "input-snapshot"), F_dv_pair("numeric_semantics", "runtime-values") };
    }

    public static object F_dv_truth(object v_world, object v_proposition)
    {
        if (R.Bool(R.is_list(v_world)))
        {
            if (R.Bool(R.eq(F_dv_get(v_proposition, "op"), "constant")))
            {
                object v_value__24708 = F_dv_get(v_proposition, "value");
                if (R.Bool(R.not(R.is_list(v_value__24708))))
                {
                    return v_value__24708;
                }
            }
        }
        object v_key__24809 = new System.Collections.Generic.List<object>(2) { "dv_truth", v_proposition };
        object v_cached__24849 = R.memo_get(v_world, v_key__24809);
        if (R.Bool(R.at(v_cached__24849, R.number("0"))))
        {
            return R.at(v_cached__24849, R.number("1"));
        }
        return R.memo_put(v_world, v_key__24809, F_dv_truth_uncached(v_world, v_proposition));
    }

    public static object F_dv_truth_uncached(object v_world, object v_proposition)
    {
        object v_op__25050 = F_dv_get(v_proposition, "op");
        if (R.Bool(R.eq(v_op__25050, "constant")))
        {
            return F_dv_get(v_proposition, "value");
        }
        else
        {
            if (R.Bool((R.Bool(R.eq(v_op__25050, "equals")) ? (object)(true) : (object)(R.eq(v_op__25050, "at_least")))))
            {
                if (R.Bool(R.read_ref_view_safe(F_dv_get(v_proposition, "ref"))))
                {
                    v_world = R.read_view(v_world);
                    v_proposition = R.read_view(v_proposition);
                }
            }
            if (R.Bool(R.eq(v_op__25050, "not")))
            {
                object v_inner__25622 = F_dv_truth(v_world, F_dv_get(v_proposition, "item"));
                if (R.Bool(R.eq(v_inner__25622, "unknown")))
                {
                    return "unknown";
                }
                else
                {
                    if (R.Bool(R.eq(v_inner__25622, "true")))
                    {
                        return "false";
                    }
                    else
                    {
                        return "true";
                    }
                }
            }
            else
            {
                if (R.Bool(R.eq(v_op__25050, "all")))
                {
                    object v_items__25945 = F_dv_get(v_proposition, "items");
                    object v_i__25991 = R.number("0");
                    object v_result__26017 = "true";
                    while (R.Bool(R.lt(v_i__25991, R.count(v_items__25945))))
                    {
                        object v_truth__26102 = F_dv_truth(v_world, R.at(v_items__25945, v_i__25991));
                        if (R.Bool(R.eq(v_truth__26102, "false")))
                        {
                            return "false";
                        }
                        else
                        {
                            if (R.Bool(F_dv_and(R.eq(v_truth__26102, "unknown"), R.eq(v_result__26017, "true"))))
                            {
                                v_result__26017 = "unknown";
                            }
                        }
                        v_i__25991 = R.add(v_i__25991, R.number("1"));
                    }
                    return v_result__26017;
                }
                else
                {
                    if (R.Bool((R.Bool(R.predicate_enabled()) ? (object)(R.not(F_dv_has_key(v_world, "CognitionFilter"))) : (object)(false))))
                    {
                        if (R.Bool((R.Bool(R.eq(v_op__25050, "equals")) ? (object)(true) : (object)(R.eq(v_op__25050, "at_least")))))
                        {
                            object v_native_truth__26701 = R.predicate_leaf(v_world, v_proposition);
                            if (R.Bool(R.not(R.eq(v_native_truth__26701, false))))
                            {
                                return v_native_truth__26701;
                            }
                        }
                    }
                    object v_cell__26872 = F_dv_read_query(v_world, F_dv_get(v_proposition, "ref"));
                    if (R.Bool(F_dv_or(R.not(F_dv_get(v_cell__26872, "known")), F_dv_get(v_cell__26872, "conflicted"))))
                    {
                        return "unknown";
                    }
                    else
                    {
                        if (R.Bool(R.eq(v_op__25050, "held_for")))
                        {
                            if (R.Bool(R.not(F_dv_same(F_dv_get(v_cell__26872, "value"), F_dv_get(v_proposition, "value")))))
                            {
                                return "false";
                            }
                            object v_clock__27214 = F_dv_read_query(v_world, F_dv_get(v_proposition, "clock"));
                            if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(F_dv_get(v_clock__27214, "known"))) ? (object)(true) : (object)(F_dv_get(v_clock__27214, "conflicted")))) ? (object)(true) : (object)(R.not(R.is_numeric_value(F_dv_get(v_clock__27214, "value")))))) ? (object)(true) : (object)(R.not(R.is_numeric_value(F_dv_get(v_cell__26872, "time")))))))
                            {
                                return "unknown";
                            }
                            if (R.Bool(R.lt(F_dv_get(v_clock__27214, "value"), R.add(F_dv_get(v_cell__26872, "time"), F_dv_get(v_proposition, "duration")))))
                            {
                                return "false";
                            }
                            return "true";
                        }
                        if (R.Bool(R.eq(v_op__25050, "equals")))
                        {
                            if (R.Bool(F_dv_same(F_dv_get(v_cell__26872, "value"), F_dv_get(v_proposition, "value"))))
                            {
                                return "true";
                            }
                            else
                            {
                                return "false";
                            }
                        }
                        else
                        {
                            if (R.Bool(R.eq(v_op__25050, "at_least")))
                            {
                                if (R.Bool(R.lt(F_dv_get(v_cell__26872, "value"), F_dv_get(v_proposition, "value"))))
                                {
                                    return "false";
                                }
                                else
                                {
                                    return "true";
                                }
                            }
                            else
                            {
                                return R.fail("Unknown proposition operator");
                            }
                        }
                    }
                }
            }
        }
    }
}
