using DuelView.Compiled;

namespace DuelView.Core;

public static partial class DVKernel
{
    public static object F_dvs_autonomy_update(object actor, object time)
    {
        var profile = F_dv_find(F_dv_get(actor, "policy"), "sims_autonomy", false);
        if (profile is false) return actor;
        var now = R.Num(time);
        var saved = F_dv_find(actor, "autonomy_state", false);
        var needs = saved is false ? F_dv_get(profile, "commodity_needs") : F_dv_get(saved, "needs");
        var previous = saved is false ? now : R.Num(F_dv_get(saved, "time"));
        if (now < previous) throw new InvalidOperationException("Autonomy clock cannot move backwards.");
        if (saved is not false && now == previous) return actor;
        var seconds = (now - previous) * 60;
        foreach (var pair in R.Items(F_dv_find(profile, "need_decay_per_second", new List<object>())))
        {
            var key = R.at(pair, 0d);
            var rate = R.Num(R.at(pair, 1d));
            if (!double.IsFinite(rate) || rate < 0) throw new FormatException("Invalid authored need decay.");
            needs = F_dv_with(needs, key, Math.Clamp(R.Num(F_dv_get(needs, key)) - rate * seconds, 0, 100));
        }
        var state = saved is false ? new List<object>() : saved;
        state = F_dv_with_many(state, new List<object> { F_dv_pair("needs", needs), F_dv_pair("time", now) });
        return F_dv_with(actor, "autonomy_state", state);
    }

    public static object F_dvs_autonomy_needs(object actor)
    {
        var saved = F_dv_find(actor, "autonomy_state", false);
        return saved is false ? F_dv_get(F_dv_get(F_dv_get(actor, "policy"), "sims_autonomy"), "commodity_needs") : F_dv_get(saved, "needs");
    }

    public static object F_dvs_autonomy_complete(object actor, object action, object success, object events, object eventId, object time)
    {
        actor = F_dvs_autonomy_update(actor, time);
        var profile = F_dv_find(F_dv_get(actor, "policy"), "sims_autonomy", false);
        if (profile is false || !R.Bool(success)) return actor;
        var state = F_dv_get(actor, "autonomy_state");
        var applied = F_dv_find(state, "applied_events", new List<object>());
        if (R.Bool(F_dv_has(applied, eventId))) return actor;
        // Actual effects are authored separately; predictions are never committed.
        var effects = F_dv_find(action, "completed_need_effects", false);
        if (effects is false) effects = F_dv_find(F_dv_find(profile, "completed_action_effects", new List<object>()), F_dv_get(action, "kind"), false);
        if (effects is false) return actor;
        var before = F_dv_get(state, "needs");
        var after = before;
        foreach (var pair in R.Items(effects))
        {
            var key = R.at(pair, 0d);
            var delta = R.Num(R.at(pair, 1d));
            if (!double.IsFinite(delta)) throw new FormatException("Invalid authored need effect.");
            after = F_dv_with(after, key, Math.Clamp(R.Num(F_dv_get(after, key)) + delta, 0, 100));
        }
        state = F_dv_with_many(state, new List<object> { F_dv_pair("needs", after), F_dv_pair("applied_events", F_dv_append(applied, eventId)) });
        F_dvs_event(events, "autonomy_needs_committed", F_dv_get(actor, "id"), time, F_dv_single(eventId),
            new List<object> { F_dv_pair("action", F_dv_get(action, "id")), F_dv_pair("before", before), F_dv_pair("after", after) });
        return F_dv_with(actor, "autonomy_state", state);
    }
}
