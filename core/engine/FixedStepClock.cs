using DuelView.Compiled;

namespace DuelView.Core;

public static partial class DVKernel
{
    /// <summary>Accept one logical idle step. The pulse identity deduplicates input;
    /// its numeric gap is never interpreted as elapsed time or accumulated debt.</summary>
    public static object F_dvs_advance_fixed_step(object state, long pulse)
    {
        _ = F_dv_require(F_dv_find(state, "pending", false) is false, "A prepared action owns its completion boundary");
        var timing = F_dv_get(R.content("data", "host.runtime.presentation_timing"), "value");
        var quantum = R.Num(F_dv_get(timing, "core_step_minutes"));
        _ = F_dv_require(double.IsFinite(quantum) && quantum > 0, "Core step minutes must be finite and positive");
        if (!R.Bool(F_dvs_session_active(state))) return state;
        var world = F_dv_get(state, "physical");
        var start = R.Num(F_dv_get(world, "time"));
        var end = Math.Min(start + quantum, R.Num(F_dv_get(state, "horizon")));
        if (end <= start) return state;
        foreach (var actor in R.Items(F_dv_get(state, "actors")))
        {
            if (F_dv_find(actor, "in_flight_action", false) is not false) continue;
            var queued = F_dv_find(actor, "scheduled_action", false);
            if (queued is false || !R.Bool(F_dvs_actor_can_act(F_dvs_erosion_update(actor, world, start)))) continue;
            var due = R.Num(F_dv_get(queued, "due"));
            _ = F_dv_require(double.IsFinite(due), "Queued action has an invalid due time");
            // Yield an already-due action to normal dispatch. No loop and no
            // time charge can bypass its genuine host completion.
            if (due <= start) return state;
            end = Math.Min(end, due);
        }
        end = Math.Min(end, R.Num(F_dvs_environment_next_time(world, F_dv_get(state, "story"), end)));
        _ = F_dv_require(double.IsFinite(end) && end > start, "Core fixed step failed to advance");
        var events = F_dv_clone(F_dv_get(state, "events"));
        var parent = F_dvs_event(events, "core_clock_step", "", start, new List<object>(), new List<object> {
            F_dv_pair("pulse_sequence", (double)pulse), F_dv_pair("quantum_minutes", quantum),
            F_dv_pair("start", start), F_dv_pair("end", end), F_dv_pair("duration_minutes", end - start) });
        world = F_dvs_environment(F_dv_with(world, "time", end), F_dv_get(state, "story"), events, parent);
        return F_dvs_owned_state_update(state, new List<object> {
            F_dv_pair("physical", world), F_dv_pair("events", events), F_dv_pair("host_motion_wait", false) });
    }
}
