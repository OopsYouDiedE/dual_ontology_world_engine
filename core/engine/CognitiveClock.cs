using DuelView.Compiled;

namespace DuelView.Core;

public static partial class DVKernel
{
    /// <summary>Charge an explicitly committed cognitive operation using its DV duration.
    /// This advances environment time only. Due actions still need normal scheduling
    /// and genuine host completion; UI pauses and CPU/render durations are irrelevant.</summary>
    public static object F_dvs_advance_cognitive_time(object state, double minutes, string actor, string attemptId)
    {
        _ = F_dv_require(double.IsFinite(minutes) && minutes >= 0, "Invalid cognitive duration");
        _ = F_dv_require(F_dv_find(state, "pending", false) is false, "Finish the prepared action before a cognitive time commit");
        if (minutes == 0) return state;
        var world = F_dv_get(state, "physical");
        var start = R.Num(F_dv_get(world, "time"));
        var end = start + minutes;
        _ = F_dv_require(double.IsFinite(end) && end <= R.Num(F_dv_get(state, "horizon")), "Cognitive duration exceeds the session horizon");
        var events = F_dv_clone(F_dv_get(state, "events"));
        // attemptId identifies the cognitive archive record, not an event in the
        // causal DAG. Keep it as correlation data; no evidence parent is invented.
        var timeEvent = F_dvs_event(events, "cognitive_time_advance", actor, start,
            new List<object>(),
            new List<object> { F_dv_pair("attempt_id", attemptId), F_dv_pair("start", start), F_dv_pair("end", end), F_dv_pair("duration_minutes", minutes) });
        while (R.Num(F_dv_get(world, "time")) < end)
        {
            var next = Math.Min(end, R.Num(F_dvs_environment_next_time(world, F_dv_get(state, "story"), end)));
            _ = F_dv_require(next > R.Num(F_dv_get(world, "time")), "Cognitive clock failed to advance");
            world = F_dvs_environment(F_dv_with(world, "time", next), F_dv_get(state, "story"), events, timeEvent);
        }
        return F_dvs_owned_state_update(state, new List<object> {
            F_dv_pair("physical", world), F_dv_pair("events", events), F_dv_pair("host_motion_wait", false) });
    }
}
