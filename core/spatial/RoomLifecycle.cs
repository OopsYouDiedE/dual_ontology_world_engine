// @mechanism: native_3d_architect_and_prefabs
// @layer: core/spatial
// @contracts: RoomLifecycle

using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using DuelView.Runtime;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
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
using System.Text;
using System;
using static DuelView.Compiled.R;
using static DuelView.Core.DVKernel;

// --- Source: RoomLifecycle.cs ---
namespace DuelView.Runtime
{

public enum RoomPhase { Unloaded, Preparing, Active, CoolingDown, Unloading }
public enum RoomEffect { None, Prepare, Activate, Retain, Release }
public readonly record struct RoomTransition(RoomPhase Before, RoomPhase After, RoomEffect Effect, long Generation);

/// <summary>Presentation residency only. World metadata and simulation remain owned by the session.</summary>
public sealed class RoomLifecycle
{
    public RoomPhase Phase { get; private set; }
    public long Generation { get; private set; }
    public double LastDemandAt { get; private set; }
    public bool Prepared { get; private set; }
    private double lastNow = double.NegativeInfinity;
    private bool activeDemand;

    public RoomTransition Update(double now, bool prepare, bool active, bool protectedInFlight, double retentionSeconds)
    {
        if (!double.IsFinite(now) || now < lastNow) throw new ArgumentOutOfRangeException(nameof(now));
        if (!double.IsFinite(retentionSeconds) || retentionSeconds < 0) throw new ArgumentOutOfRangeException(nameof(retentionSeconds));
        lastNow = now;
        activeDemand = active;
        var before = Phase;
        var effect = RoomEffect.None;
        if (prepare || active || protectedInFlight) LastDemandAt = now;
        switch (Phase)
        {
            case RoomPhase.Unloaded when prepare || active || protectedInFlight:
                Generation++;
                Phase = RoomPhase.Preparing;
                effect = RoomEffect.Prepare;
                break;
            case RoomPhase.Preparing:
                if (!prepare && !active && !protectedInFlight && now - LastDemandAt >= retentionSeconds)
                { Generation++; Phase = RoomPhase.Unloading; effect = RoomEffect.Release; }
                else if (Prepared && active)
                { Phase = RoomPhase.Active; effect = RoomEffect.Activate; }
                break;
            case RoomPhase.Active when !active:
                Phase = RoomPhase.CoolingDown;
                effect = RoomEffect.Retain;
                break;
            case RoomPhase.CoolingDown:
                if (active) { Phase = RoomPhase.Active; effect = RoomEffect.Activate; }
                else if (!prepare && !protectedInFlight && now - LastDemandAt >= retentionSeconds)
                { Generation++; Phase = RoomPhase.Unloading; effect = RoomEffect.Release; }
                break;
        }
        return new(before, Phase, effect, Generation);
    }

    // Async callbacks carry their generation. A cancelled request cannot publish a node.
    public RoomTransition CompletePreparation(long generation, bool? currentActiveDemand = null)
    {
        var before = Phase;
        if (generation != Generation || Phase != RoomPhase.Preparing || Prepared)
            return new(before, Phase, RoomEffect.None, Generation);
        if (currentActiveDemand.HasValue) activeDemand = currentActiveDemand.Value;
        Prepared = true;
        if (activeDemand) Phase = RoomPhase.Active;
        return new(before, Phase, activeDemand ? RoomEffect.Activate : RoomEffect.None, Generation);
    }

    public RoomTransition FailPreparation(long generation)
    {
        var before = Phase;
        if (generation != Generation || Phase != RoomPhase.Preparing || Prepared)
            return new(before, Phase, RoomEffect.None, Generation);
        Generation++;
        Phase = RoomPhase.Unloading;
        return new(before, Phase, RoomEffect.Release, Generation);
    }

    // Called after the host has removed the tree and released all resource references.
    public bool CompleteRelease(long generation)
    {
        if (generation != Generation || Phase != RoomPhase.Unloading) return false;
        Prepared = false;
        Phase = RoomPhase.Unloaded;
        return true;
    }
}
}

// --- Source: RoomResidency.cs ---
namespace DuelView.Runtime
{


public sealed record RoomRequest(string Room, long Generation, bool Load, RoomEffect Effect = RoomEffect.None);
public sealed record RoomSimulation(string Room, double Distance, double Speed, string Mode);

/// <summary>Only presentation residency changes here. World facts and remembered maps
/// have independent lifetimes. Hosts acknowledge actual assembly and release.</summary>
public sealed class RoomResidency
{
    private sealed class Room(double distance, double speed)
    {
        internal double Distance = distance;
        internal double Speed = speed;
        internal readonly RoomLifecycle Lifecycle = new();
        internal bool Prepare;
        internal bool Visible;
        internal int Pins;
        internal double RetryAfter;
    }
    private readonly Dictionary<string, Room> rooms = new(StringComparer.Ordinal);
    private double retention;
    public double RetentionSeconds => retention;
    private readonly int requestsPerSlice;
    private readonly string inactiveMode;
    private readonly double retryDelay;
    private string[] order = [];
    private int cursor;

    public RoomResidency(JsonElement policy)
    {
        retention = policy.Get("inactive_retention_seconds").Number(double.NaN);
        var budget = policy.Get("room_requests_per_slice").Number(double.NaN);
        inactiveMode = policy.Get("inactive_simulation").Text();
        retryDelay = policy.Get("retry_delay_seconds").Number(double.NaN);
        if (!double.IsFinite(retryDelay) || retryDelay <= 0 || !double.IsFinite(retention) || retention < 0 || !double.IsFinite(budget) || budget < 1 || budget != Math.Truncate(budget) || budget > int.MaxValue || inactiveMode.Length == 0)
            throw new InvalidDataException("Invalid room residency policy.");
        requestsPerSlice = (int)budget;
    }

    public void Register(string id, double godotDistance, double godotSpeed)
    {
        if (id.Length == 0 || !double.IsFinite(godotDistance) || godotDistance < 0 || !double.IsFinite(godotSpeed) || godotSpeed <= 0)
            throw new InvalidDataException("Invalid Godot room simulation settings.");
        rooms.Add(id, new(godotDistance, godotSpeed));
        order = rooms.Keys.ToArray();
    }

    public void SetVisible(string id, bool visible, double now)
    {
        var room = rooms[id];
        if (!double.IsFinite(now) || now < 0) throw new ArgumentOutOfRangeException(nameof(now));
        room.Visible = visible;
    }

    public void SetPrepare(string id, bool prepare) => rooms[id].Prepare = prepare;
    public void SetRetentionSeconds(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        retention = seconds;
    }
    public RoomPhase Phase(string id) => rooms[id].Lifecycle.Phase;

    // Pin all rooms along the next real route before a body starts crossing it.
    public void Pin(string id) => rooms[id].Pins++;
    public void Unpin(string id)
    {
        var room = rooms[id];
        if (room.Pins == 0) throw new InvalidOperationException("Room route pin is not held.");
        room.Pins--;
    }

    public IReadOnlyList<RoomRequest> Step(double now)
    {
        if (!double.IsFinite(now) || now < 0) throw new ArgumentOutOfRangeException(nameof(now));
        var requests = new List<RoomRequest>();
        // Both scanning and scene requests are spread across frames.
        for (var n = 0; n < Math.Min(order.Length, requestsPerSlice); n++)
        {
            var id = order[cursor];
            cursor = (cursor + 1) % order.Length;
            var room = rooms[id];
            if (now < room.RetryAfter) continue;
            var change = room.Lifecycle.Update(now, room.Prepare, room.Visible, room.Pins > 0, retention);
            if (change.Effect != RoomEffect.None)
                requests.Add(new(id, change.Generation, change.Effect == RoomEffect.Prepare, change.Effect));
        }
        return requests;
    }

    public RoomRequest? Acknowledge(RoomRequest request, bool success, double now)
    {
        if (!double.IsFinite(now) || now < 0) throw new ArgumentOutOfRangeException(nameof(now));
        var room = rooms[request.Room];
        var lifecycle = room.Lifecycle;
        if (lifecycle.Generation != request.Generation)
            throw new InvalidOperationException("Stale room assembly completion.");
        // Activation/retention are synchronous host effects. Loading and release have acknowledgements.
        if (request.Effect is RoomEffect.Activate or RoomEffect.Retain) return null;
        if (request.Load)
        {
            if (lifecycle.Phase != RoomPhase.Preparing || lifecycle.Prepared)
                throw new InvalidOperationException("Room is not waiting for preparation.");
            if (!success) throw new InvalidOperationException("Failed preparation requires CancelPreparation and actual release acknowledgement.");
            var change = lifecycle.CompletePreparation(request.Generation, room.Visible);
            if (change.Effect == RoomEffect.Activate)
                return new(request.Room, change.Generation, false, change.Effect);
        }
        else
        {
            if (!success) return request; // Host retries the same release; residency remains Unloading.
            if (!lifecycle.CompleteRelease(request.Generation))
                throw new InvalidOperationException("Room is not waiting for release.");
        }
        room.RetryAfter = Math.Max(room.RetryAfter, success ? now : now + retryDelay);
        return null;
    }

    public RoomRequest CancelPreparation(RoomRequest request, double now)
    {
        if (!double.IsFinite(now) || now < 0) throw new ArgumentOutOfRangeException(nameof(now));
        var room = rooms[request.Room];
        var change = room.Lifecycle.FailPreparation(request.Generation);
        if (change.Effect != RoomEffect.Release) throw new InvalidOperationException("Preparation cannot be cancelled.");
        room.RetryAfter = now + retryDelay;
        return new(request.Room, change.Generation, false, change.Effect);
    }

    public bool Ready(string id) => rooms[id].Lifecycle.Prepared && rooms[id].Lifecycle.Phase is RoomPhase.Active or RoomPhase.CoolingDown;
    public bool RouteReady(IEnumerable<string> path) => path.All(Ready);

    public RoomSimulation Simulation(string id, double corePathDistance)
    {
        if (!double.IsFinite(corePathDistance) || corePathDistance < 0) throw new ArgumentOutOfRangeException(nameof(corePathDistance));
        var room = rooms[id];
        return new(id, Ready(id) ? corePathDistance : room.Distance,
            room.Speed, Ready(id) ? "core_path_distance" : inactiveMode);
    }
}
}

// --- Source: SpatialCheckpoints.cs ---
namespace DuelView.Runtime
{

public sealed record SpatialNavigationCheckpoint(long Execution,string FromRoom,string ToRoom,string Authority,string Phase,
    double StartedAt,double Elapsed,double? DueAt,double RemainingDistance,double Speed,PathPoint? Target,bool PureMovement);
public sealed record SpatialAuthorityCheckpoint(bool Enabled,string VisibleRoom,SpatialNavigationCheckpoint[] Navigation);
}
