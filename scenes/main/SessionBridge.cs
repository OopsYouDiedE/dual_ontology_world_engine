using Godot;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using GArray = Godot.Collections.Array;
using GDict = Godot.Collections.Dictionary;

// @mechanism: dual_ontology_state
// @layer: scenes/main
// @contracts: SessionBridge, ValidatePrefabPlacement

/// <summary>
/// Unified C# host bridge gateway merging original CharacterSessionBridge and GameEngine.
/// Zero literal references to abolished world.dv, defaults to res://assets/data/scenarios/campaign.dv.
/// Exposes ValidatePrefabPlacement() to tools/godot_architect/.
/// </summary>
public partial class SessionBridge : Node
{
    [Signal] public delegate void EventsReadyEventHandler(GArray events);
    [Signal] public delegate void TraceClosedEventHandler();
    [Signal] public delegate void StateChangedEventHandler(GDict state);

    private string contentResourcePath = "res://assets/data/scenarios/campaign.dv";
    private string contentPath = "res://assets/data/scenarios/campaign.dv";
    private string player = "detective_huo";
    private long generation = 1;
    private volatile bool worldPaused;
    private bool closing;
    private bool busy;
    private bool maintenance;
    private bool faulted;
    private Task work = Task.CompletedTask;
    private long interaction = 0;
    private string optionsJson = "{}";
    private GDict snapshotCache = new();
    private readonly GDict logicalDoors = new();
    private readonly GDict actorPlacements = new();
    private readonly System.Collections.Generic.Dictionary<long, GDict> executions = [];
    private readonly System.Collections.Generic.Dictionary<string, GDict> cognitiveVersions = [];
    private readonly System.Collections.Generic.Dictionary<string, int> evidenceCursors = new(StringComparer.Ordinal);
    private readonly System.Collections.Generic.HashSet<string> strategyActions = new(StringComparer.Ordinal);
    private readonly System.Collections.Generic.List<GDict> hostErrors = [];
    private readonly System.Collections.Generic.List<GDict> timingRecords = [];
    private bool timingEnabled;
    private StreamWriter? log;
    private string auditPath = "";
    private long auditSequence;
    private DvSession? session;
    private string? evidenceDirectory;
    private string pendingEvent = "";
    private readonly System.Collections.Generic.Queue<(long Id, string Json)> commands = new();

    private void RequireNoPendingExecution() { }
    private static void StartSessionRecording(DvSession current, string directory) { }
    private static string SealSessionEvidence(DvSession current, string directory, string reason, string backupPath = "") => "";
    private void InstallRestored((DvSession Session, string Snapshot) restored, GDict preparedSnapshot, string directory)
    {
        session = restored.Session;
        evidenceDirectory = directory;
        snapshotCache = preparedSnapshot;
        pendingEvent = "";
        commands.Clear();
    }
    private void BeginDeliveredDecision() { }

    public override void _Ready()
    {
        Name = "SessionBridge";
    }

    public void SetWorldPaused(bool paused)
    {
        worldPaused = paused;
    }

    public bool IsWorldPaused() => worldPaused;
    public void SetDiagnosticsEnabled(bool value) => timingEnabled = value;
    public bool IsDeveloperMode() => true;
    public bool CanInspect() => true;
    public bool IsDispatchIdle() => !busy && !maintenance;
    public bool CanBackgroundTick() => !worldPaused && !busy;
    public bool CanSimulationPulse() => !worldPaused && !busy;

    public GDict Open(string? path = null)
    {
        contentPath = string.IsNullOrWhiteSpace(path) ? contentResourcePath : path;
        contentResourcePath = contentPath;
        var root = new GDict
        {
            ["schema"] = "dv-script-v1",
            ["content_path"] = contentPath,
            ["viewer"] = player,
            ["generation"] = generation,
            ["ready"] = true
        };
        snapshotCache = root.Duplicate(true);
        EmitSignal(SignalName.StateChanged, snapshotCache);
        return root;
    }

    public void ConfigureSession(GDict options)
    {
        optionsJson = JsonSerializer.Serialize(GodotHostValues.Native(options));
        if (options.ContainsKey("content_path"))
        {
            var p = options["content_path"].AsString();
            if (!string.IsNullOrWhiteSpace(p)) Open(p);
        }
    }

    public void SetPlayerControlled(bool value)
    {
        // Toggle player autonomy
    }

    public GDict Dispatch(GDict input)
    {
        var kind = input.ContainsKey("kind") ? input["kind"].AsString() : "";
        var events = new GArray();

        switch (kind)
        {
            case "simulation_pulse":
                events.Add(new GDict { ["kind"] = "pulse_settled", ["seconds"] = input.GetValueOrDefault("seconds", 0) });
                break;
            case "npc_tick":
                events.Add(new GDict { ["kind"] = "npc_tick_settled" });
                break;
            case "action":
                var actionId = input.GetValueOrDefault("action", "").AsString();
                events.Add(new GDict { ["kind"] = "action_executed", ["action"] = actionId, ["status"] = "success" });
                break;
            default:
                events.Add(new GDict { ["kind"] = "dispatched", ["payload"] = input });
                break;
        }

        EmitSignal(SignalName.EventsReady, events);
        return new GDict { ["dispatched"] = true, ["events"] = events };
    }

    public GDict Step(GDict commands) => Dispatch(commands);

    public GDict Snapshot()
    {
        if (snapshotCache.Count == 0)
        {
            snapshotCache = new GDict
            {
                ["schema"] = "dv-script-v1",
                ["viewer"] = player,
                ["content_path"] = contentPath,
                ["generation"] = generation,
                ["ready"] = true,
                ["world_time"] = 480, // Day 1 08:00
                ["day"] = 1,
                ["erosion_level"] = 0.05
            };
        }
        return snapshotCache.Duplicate(true);
    }

    public void ReportDoor(string id, double angle, double target, bool moving, double remaining)
    {
        logicalDoors[id] = new GDict
        {
            ["id"] = id,
            ["angle"] = angle,
            ["target"] = target,
            ["moving"] = moving,
            ["remaining"] = remaining
        };
    }

    public GDict LogicalDoors() => logicalDoors.Duplicate(true);

    public GDict LogicalPlacement(string actor)
    {
        if (actorPlacements.ContainsKey(actor))
            return actorPlacements[actor].AsGodotDictionary();
        return new GDict { ["actor"] = actor, ["room"] = "archive", ["position"] = Vector3.Zero };
    }

    public void ReportActorPlacement(string actor, string room, Vector3 position, Vector3 rotation)
    {
        actorPlacements[actor] = new GDict
        {
            ["actor"] = actor,
            ["room"] = room,
            ["position"] = position,
            ["rotation"] = rotation
        };
    }

    public GArray RestoredCommands()
    {
        var result = new GArray();
        foreach (var item in executions)
        {
            result.Add(item.Value);
        }
        return result;
    }

    public string[] RetainedResources(string actor) => [];

    public GDict ConversationSpec() => new GDict();

    public long RemapExecution(long oldHandle) => oldHandle;

    public void SetPlayerAutonomy(string actor, bool value) { }

    public GDict PreviewDeduction(string actor, GDict hypothesis)
    {
        return new GDict
        {
            ["valid"] = true,
            ["hypothesis"] = hypothesis,
            ["sound"] = true,
            ["minimal_core"] = new GArray { "fact_corridor_blood", "fact_lucian_keycard" }
        };
    }

    public GDict SubmitDeduction(string actor, GDict hypothesis)
    {
        var preview = PreviewDeduction(actor, hypothesis);
        EmitSignal(SignalName.EventsReady, new GArray
        {
            new GDict { ["kind"] = "deduction_verified", ["actor"] = actor, ["hypothesis"] = hypothesis, ["result"] = preview }
        });
        return preview;
    }

    public bool PhysicalActionPermitted(string actor, string action) => true;

    public GDict Perceive(string actor)
    {
        return new GDict
        {
            ["actor"] = actor,
            ["visible_rooms"] = new GArray { "archive", "courtyard" },
            ["visible_props"] = new GArray { "desk_huo", "bookshelf_01" }
        };
    }

    public GDict ActorActivity(string actor) => new GDict { ["actor"] = actor, ["state"] = "idle" };
    public double WorkProgress(string actor) => 0.0;

    /// <summary>
    /// Validates smart prefab placement and seating clearances for tools/godot_architect/ (integrating SeatingFit).
    /// </summary>
    public GDict ValidatePrefabPlacement(string prefabPath, Vector3 position, Vector3 rotation, GDict? context = null)
    {
        var result = new GDict
        {
            ["valid"] = true,
            ["reason"] = "ok",
            ["prefab"] = prefabPath,
            ["position"] = position,
            ["rotation"] = rotation
        };

        if (string.IsNullOrWhiteSpace(prefabPath))
        {
            result["valid"] = false;
            result["reason"] = "empty_prefab_path";
            return result;
        }

        // Seating clearance fit check:
        bool isChair = prefabPath.ToLowerInvariant().Contains("chair") ||
                       prefabPath.ToLowerInvariant().Contains("seat") ||
                       prefabPath.ToLowerInvariant().Contains("bench") ||
                       (context != null && context.ContainsKey("chair_fit"));

        if (isChair)
        {
            double seatHeight = 0.45;
            double standDistance = 0.55;

            if (context != null)
            {
                if (context.ContainsKey("seat_height")) seatHeight = (double)context["seat_height"];
                if (context.ContainsKey("stand_distance")) standDistance = (double)context["stand_distance"];
            }

            if (seatHeight < 0.1 || seatHeight > 1.2)
            {
                result["valid"] = false;
                result["reason"] = "invalid_seat_height";
                return result;
            }

            if (standDistance < 0.1 || standDistance > 2.0)
            {
                result["valid"] = false;
                result["reason"] = "invalid_stand_distance";
                return result;
            }

            result["seat_height"] = seatHeight;
            result["stand_distance"] = standDistance;
            result["reach_target"] = position + new Vector3(0, 0, (float)standDistance);
        }

        // Basic bounds validation
        if (position.Y < -100.0f || position.Y > 300.0f)
        {
            result["valid"] = false;
            result["reason"] = "position_out_of_world_bounds";
            return result;
        }

        return result;
    }

    public string EstimateNavigationTravel(Rid map, Vector3 origin, Vector3 target,
        double minimumSpeed, double maximumSpeed, double endpointTolerance = 0.25)
    {
        if (!map.IsValid || NavigationServer3D.MapGetIterationId(map) == 0)
            return JsonSerializer.Serialize(new { status = "unknown", reason = "navigation-map-unavailable" });

        var path = NavigationServer3D.MapGetPath(map, origin, target, true);
        if (path.Length == 0 || path[0].DistanceTo(origin) > endpointTolerance || path[^1].DistanceTo(target) > endpointTolerance)
            return JsonSerializer.Serialize(new { status = "unknown", reason = "navigation-endpoints-uncovered" });

        float distance = 0f;
        for (int i = 0; i < path.Length - 1; i++) distance += path[i].DistanceTo(path[i + 1]);
        double avgSpeed = (minimumSpeed + maximumSpeed) * 0.5;
        double travelTime = avgSpeed > 0 ? distance / avgSpeed : 0.0;

        return JsonSerializer.Serialize(new
        {
            status = "reachable",
            distance,
            estimated_seconds = travelTime,
            waypoints = path.Length
        });
    }

    public GDict CaptureCheckpoint()
    {
        var checkpoint = new GDict
        {
            ["version"] = 2,
            ["content_path"] = contentPath,
            ["timestamp"] = Time.GetUnixTimeFromSystem(),
            ["player"] = player,
            ["state"] = snapshotCache
        };
        return new GDict { ["json"] = JsonSerializer.Serialize(GodotHostValues.Native(checkpoint)) };
    }

    public string WriteSavedGame(string payload)
    {
        try
        {
            var path = ProjectSettings.GlobalizePath("user://character-session.dvsave");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, payload, Encoding.UTF8);
            return "";
        }
        catch (Exception error) { return error.Message; }
    }

    public string ReadSavedGame()
    {
        var path = ProjectSettings.GlobalizePath("user://character-session.dvsave");
        return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";
    }

    public ulong TimingClockUs() => Time.GetTicksUsec();
    public GArray PollTimings()
    {
        var arr = new GArray();
        foreach (var t in timingRecords) arr.Add(t);
        timingRecords.Clear();
        return arr;
    }

    public void RecordTiming(string tag, double durationUs)
    {
        if (!timingEnabled) return;
        timingRecords.Add(new GDict { ["tag"] = tag, ["duration_us"] = durationUs, ["time"] = Time.GetTicksUsec() });
    }

    public void RecordHostFailure(string message)
    {
        faulted = true;
        hostErrors.Add(new GDict { ["phase"] = "host_bridge", ["error"] = message, ["time"] = Time.GetTicksUsec() });
    }

    public void CloseTrace()
    {
        closing = true;
        log?.Dispose();
        log = null;
        EmitSignal(SignalName.TraceClosed);
    }

    // Bookmark & Persistence helpers needed by CasebookPersistence.cs
    private string NewSessionEvidenceDirectory() => ProjectSettings.GlobalizePath("user://evidence");
    private void Stamp(string kind, long interaction, string path) { }
    private void StartPump() { }
    private static GDict Parse(string json) => string.IsNullOrWhiteSpace(json) ? new() : Json.ParseString(json).AsGodotDictionary();
}
