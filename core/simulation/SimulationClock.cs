// @mechanism: causal_clock_and_campaign
// @layer: core/simulation
// @contracts: SimulationClock

using DuelView.Autonomy;
using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using DuelView.Runtime;
using DuelView.Spatial;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using System;
using static DuelView.Compiled.R;
using static DuelView.Core.DVKernel;
using static DuelView.Core.DVSimulationKernel;

// --- Source: SimulationClock.cs ---
namespace DuelView.Runtime
{


public sealed record SimulationClockState(double Now,double PendingSeconds,double? DialogueEnd,double? RestEnd=null,double PhysicalSeconds=0);

/// <summary>Shared world time for actions and physical simulation. Dialogue deadlines
/// use the current rate. A host drains pending work before advancing another slice.</summary>
public sealed class SimulationClock
{
    private readonly double activeRate;
    private readonly double idleRate;
    private readonly double workRate;
    private readonly double maximumStep;
    private readonly double restRate;
    private readonly double restStep;
    private double? dialogueEnd;
    private double pendingSeconds;
    private double physicalSeconds;
    private double? restEnd;
    public bool Resting => restEnd.HasValue;
    public void RestUntil(double time) { if(time>Now)restEnd=time; }
    public double Now { get; private set; }
    public bool FastForwarding => dialogueEnd.HasValue;
    public double PendingSeconds => pendingSeconds+physicalSeconds;
    public double PendingPhysicalSeconds => physicalSeconds;
    public void ClearPendingSeconds(){pendingSeconds=0;physicalSeconds=0;}
    public void ClearUnsimulatedFastForward()=>pendingSeconds=0;
    public void SubmitPhysicalSeconds(double seconds)
    {
        if(!double.IsFinite(seconds)||seconds<0||seconds>60)throw new ArgumentOutOfRangeException(nameof(seconds));
        physicalSeconds+=seconds;
    }
    public void SubmitLogicalSeconds(double seconds)
    {
        if(!double.IsFinite(seconds)||seconds<0||seconds>60)throw new ArgumentOutOfRangeException(nameof(seconds));
        pendingSeconds+=seconds;
    }
    public double Rate(bool idle,bool working)=>restEnd.HasValue?restRate:working?workRate:idle?idleRate:activeRate;
    public SimulationClockState SaveState()=>new(Now,pendingSeconds,dialogueEnd,restEnd,physicalSeconds);
    public void RestoreState(SimulationClockState state)
    {
        if(!double.IsFinite(state.Now)||state.Now<0 || !double.IsFinite(state.PendingSeconds)||state.PendingSeconds<0 ||
           !double.IsFinite(state.PhysicalSeconds)||state.PhysicalSeconds<0||state.DialogueEnd is { } end && (!double.IsFinite(end)||end<state.Now))
            throw new InvalidDataException("Invalid simulation clock checkpoint.");
        Now=state.Now;pendingSeconds=state.PendingSeconds;physicalSeconds=state.PhysicalSeconds;dialogueEnd=state.DialogueEnd;restEnd=state.RestEnd;
    }

    public SimulationClock(JsonElement policy, double initialTime)
    {
        activeRate = policy.Get("active_game_seconds_per_second").Number(double.NaN);
        idleRate = policy.Get("idle_game_seconds_per_second").Number(double.NaN);
        workRate = policy.Get("work_game_seconds_per_second").Number(activeRate);
        maximumStep = policy.Get("maximum_game_seconds_per_slice").Number(double.NaN);
        restRate=policy.Get("rest_game_seconds_per_second").Number(activeRate);
        restStep=policy.Get("rest_maximum_game_seconds_per_slice").Number(maximumStep);
        if (!double.IsFinite(initialTime) || initialTime < 0 || !double.IsFinite(activeRate) || activeRate <= 0 ||
            !double.IsFinite(idleRate) || idleRate <= 0 || idleRate > activeRate || !double.IsFinite(workRate) || workRate<=0 || !double.IsFinite(maximumStep) || maximumStep <= 0)
            throw new InvalidDataException("Invalid DV clock policy.");
        Now = initialTime;
    }

    public void BeginDialogue(double duration)
    {
        if (dialogueEnd.HasValue) throw new InvalidOperationException("Dialogue already consumes clock time.");
        if (!double.IsFinite(duration) || duration <= 0 || !double.IsFinite(Now + duration)) throw new ArgumentOutOfRangeException(nameof(duration));
        dialogueEnd = Now + duration;
    }

    public double Step(double realSeconds, bool idle, bool paused, bool pendingWork, double nextEvent, bool working = false,bool physicalOnly=false)
    {
        if (!double.IsFinite(realSeconds) || realSeconds < 0 || double.IsNaN(nextEvent) || nextEvent < Now)
            throw new ArgumentOutOfRangeException(nameof(realSeconds));
        if (paused) return Now;
        // Navigation has already been simulated by Godot. Its seconds must not
        // be multiplied by work/rest rates or mixed with unexecuted fast-forward.
        if(physicalOnly)pendingSeconds=0;
        else pendingSeconds+=realSeconds*Rate(idle,working);
        if(pendingWork)return Now;
        var available=physicalSeconds+(physicalOnly?0:pendingSeconds);
        if(restEnd is { } wake)nextEvent=Math.Min(nextEvent,wake);
        if(dialogueEnd is { } boundary)nextEvent=Math.Min(nextEvent,boundary);
        var advance=Math.Max(0,Math.Min(Math.Min(available,restEnd.HasValue?restStep:maximumStep),nextEvent-Now));
        Now+=advance;
        var physical=Math.Min(physicalSeconds,advance);physicalSeconds-=physical;
        pendingSeconds=Math.Max(0,pendingSeconds-(advance-physical));
        if (dialogueEnd is { } end && Now >= end) dialogueEnd = null;
        if(restEnd is { } morning && Now>=morning){restEnd=null;pendingSeconds=0;}
        return Now;
    }
}

}

// --- Source: FixedStepClock.cs ---
namespace DuelView.Core
{


public static partial class DVSimulationKernel
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
}
