using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>World / ActionCosts: native Core mechanisms.</summary>
public static partial class DVKernel
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
