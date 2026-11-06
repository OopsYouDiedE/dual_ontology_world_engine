// @mechanism: native_3d_architect_and_prefabs
// @layer: scenes/components/architecture
// @contracts: RoomStreamingController
using Godot;
using DuelView.Runtime;
using System.Diagnostics;
using System.Text.Json;
using NVector = System.Numerics.Vector3;
using NPlane = System.Numerics.Plane;
using GDict = Godot.Collections.Dictionary;
using FileAccess = Godot.FileAccess;

/// <summary>Godot presentation adapter. The data scheduler never owns a Godot object.</summary>
public partial class RoomStreamingController : Node3D
{
    [Signal] public delegate void BundleAttachedEventHandler(Node3D bundle);
    private sealed class Bundle(string id, string path, string[] rooms)
    {
        internal readonly string Id = id, Path = path;
        internal readonly string[] Rooms = rooms;
        internal Node3D? Node;
        internal CollisionObject3D[] CollisionObjects=[];
        internal bool Loading, Enabled, RenderVisible;
        internal long Started;
    }
    private sealed record Room(string Id, Aabb Bounds);
    private sealed record Portal(JsonElement Source, List<Vector3> Polygon, NVector[] Numeric, string[] Links, Vector3 Center);
    private readonly Dictionary<string, Room> rooms = new();
    private readonly Dictionary<string, HashSet<string>> openNeighbors = new();
    private readonly List<Bundle> bundles = [];
    private readonly Dictionary<string, RoomRequest> preparations = new();
    private readonly Dictionary<string, RoomRequest> releases = new();
    private readonly List<JsonElement> openings = [];
    private readonly List<Portal> portals = [];
    private JsonElement floorConnections;
    private readonly Dictionary<string, Node> doors = new();
    private readonly Dictionary<string,string> interlockedOpenings = new(StringComparer.Ordinal);
    private sealed class DoorState { internal double Angle, CloseAt, LoggedAngle, Target; internal double SelfClose; internal bool LoggedBlocked, Moving; }
    private readonly Dictionary<string, DoorState> doorStates = new();
    private readonly List<double> frames = [], allFrames = [];
    private readonly List<object> assembly = [];
    private readonly List<GDict> recent = [];
    private RoomResidency residency = null!;
    private Camera3D camera = null!;
    private Node3D? subject;
    private JsonElement objectAnchors;
    public Vector3 OperationSlot(string id,string slot)
    {
        var values=objectAnchors.GetProperty(id).GetProperty("slots").GetProperty(slot).EnumerateArray().Select(value=>value.GetSingle()).ToArray();
        return new(values[0],values[1],values[2]);
    }
    private string receiptFolder="",receiptScope="stage1-zone-b-observer";
    public void ConfigureReceipts(string folder,string scope){receiptFolder=folder;receiptScope=scope;}
    public void SetSubject(Node3D value){subject=value;visibilityDirty=true;}
    public string CurrentRoom()=>currentRoom;
    public string RoomAt(Vector3 position)=>rooms.Values.Where(room=>room.Bounds.HasPoint(position))
        .OrderBy(room=>Math.Abs(position.Y-(room.Bounds.Position.Y+.2f))).FirstOrDefault()?.Id??"";
    public bool RoomResident(string id)=>residency.Phase(id) is RoomPhase.Preparing or RoomPhase.Active or RoomPhase.CoolingDown;
    public bool RoomActive(string id)=>residency.Ready(id);
    private readonly Dictionary<string,int> actorRouteRooms = new(StringComparer.Ordinal);
    private readonly HashSet<string> playerVisibleRooms = new(StringComparer.Ordinal);
    public bool RoomVisible(string id)=>playerVisibleRooms.Contains(id);
    private readonly Dictionary<string,string> actorRooms = new(StringComparer.Ordinal);
    private readonly HashSet<string> verticalRooms = new(StringComparer.Ordinal);
    public void UpdateActorRoom(string actor,Vector3 position)
    {
        var room=RoomAt(position);
        if(room.Length==0 || actorRooms.GetValueOrDefault(actor)==room)return;
        HoldActorRoom(room);
        if(actorRooms.TryGetValue(actor,out var prior))ReleaseActorRoom(prior);
        actorRooms[actor]=room;
    }
    public void ReleaseActorPosition(string actor)
    {
        if(!actorRooms.Remove(actor,out var room))return;
        ReleaseActorRoom(room);
    }
    public void HoldActorRoom(string id)
    {
        if(!rooms.ContainsKey(id))throw new InvalidDataException("Unknown actor route room: "+id);
        actorRouteRooms[id]=actorRouteRooms.GetValueOrDefault(id)+1;
        residency.Pin(id);visibilityDirty=true;
    }
    public void ReleaseActorRoom(string id)
    {
        if(!actorRouteRooms.TryGetValue(id,out var count)||count<1)throw new InvalidOperationException("Actor route room is not held: "+id);
        if(count==1)actorRouteRooms.Remove(id);else actorRouteRooms[id]=count-1;
        residency.Unpin(id);visibilityDirty=true;
    }
    public void InitializeDoor(string opening,bool open)
    {
        if(measuring)throw new InvalidOperationException("Initial door facts are applied before play.");
        var aperture=openings.Single(value=>value.GetProperty("id").GetString()==opening);
        var leaf=aperture.GetProperty("door");
        doorStates[leaf.GetProperty("id").GetString()!].Angle=open?-leaf.GetProperty("open_angle").GetDouble():0;
        visibilityDirty=true;
    }
    public GDict LogicalDoorStates()
    {
        var result=new GDict();
        foreach(var pair in doorStates)
        {
            var state=pair.Value;var angle=state.Angle;var target=state.Target;var moving=state.Moving;
            if(doors.TryGetValue(pair.Key,out var node))
            {angle=node.Get("current_angle").AsDouble();target=node.Get("motion_target").AsDouble();moving=node.Get("moving").AsBool()||node.Get("motion_blocked").AsBool();}
            result[pair.Key]=new GDict {{"Angle",angle},{"Target",target},{"Moving",moving},{"CloseRemaining",state.CloseAt>0?Math.Max(0,state.CloseAt-Now):0}};
        }
        return result;
    }
    public bool RestoreLogicalDoors(GDict values)
    {
        if(measuring || values.Count!=doorStates.Count)return false;
        foreach(var pair in doorStates)
        {
            if(!values.TryGetValue(pair.Key,out var raw)||raw.VariantType!=Variant.Type.Dictionary)return false;
            var saved=raw.AsGodotDictionary();
            if(!saved.ContainsKey("Angle")||!saved.ContainsKey("Target")||!saved.ContainsKey("Moving")||!saved.ContainsKey("CloseRemaining"))return false;
            var angle=saved["Angle"].AsDouble();var target=saved["Target"].AsDouble();var remaining=saved["CloseRemaining"].AsDouble();
            if(!double.IsFinite(angle)||Math.Abs(angle)>180||!double.IsFinite(target)||Math.Abs(target)>180||!double.IsFinite(remaining)||remaining<0)return false;
        }
        foreach(var pair in doorStates)
        {
            var saved=values[pair.Key].AsGodotDictionary();pair.Value.Angle=saved["Angle"].AsDouble();pair.Value.Target=saved["Target"].AsDouble();
            pair.Value.Moving=saved["Moving"].AsBool();var remaining=saved["CloseRemaining"].AsDouble();pair.Value.CloseAt=remaining>0?Now+remaining:0;
        }
        visibilityDirty=true;return true;
    }
    public bool BeginOpenDoor(string opening)
    {
        var aperture=openings.Single(value=>value.GetProperty("id").GetString()==opening);
        var leaf=aperture.GetProperty("door");
        var id=leaf.GetProperty("id").GetString()!;
        if(interlockedOpenings.TryGetValue(opening,out var other))
        {
            var otherAperture=openings.Single(value=>value.GetProperty("id").GetString()==other);
            var otherLeaf=otherAperture.GetProperty("door").GetProperty("id").GetString()!;
            if(Math.Abs(doorStates[otherLeaf].Angle)>1 || doors.TryGetValue(otherLeaf,out var otherNode)&&otherNode.Get("moving").AsBool())
            { Record("door_interlock_rejected",new {opening,other});return false; }
        }
        if(!doors.TryGetValue(id,out var node))return false;
        var target=-leaf.GetProperty("open_angle").GetDouble();
        if(Math.Abs(node.Get("current_angle").AsDouble()-target)<1)return true;
        var accepted=node.Call("begin_motion",target,node.GetMeta("review_motion_speed")).AsBool();
        if(accepted)visibilityDirty=true;
        Record("authorized_door_open",new {opening,target,accepted});
        return accepted;
    }
    public bool DoorOpen(string opening)
    {
        var aperture=openings.Single(value=>value.GetProperty("id").GetString()==opening);
        var leaf=aperture.GetProperty("door");var id=leaf.GetProperty("id").GetString()!;
        return doors.TryGetValue(id,out var node) && !node.Get("moving").AsBool() &&
            Math.Abs(node.Get("current_angle").AsDouble())>=Math.Abs(leaf.GetProperty("open_angle").GetDouble())-1;
    }
    private Vector3 Focus=>GodotObject.IsInstanceValid(subject)?subject!.GlobalPosition+Vector3.Up:camera.GlobalPosition;
    private StreamWriter? log;
    private long previousFrame;
    private int skipFrames, captureExcluded, peakNodes, peakCollisions;
    private double prepareMargin, activeMargin, budgetMs;
    private string currentRoom = "", directory = "";
    private bool closing;
    private bool measuring;
    private bool visibilityDirty=true;
    private Transform3D lastView;
    private float lastCameraSize = -1;
    private int bundleCursor;
    private int collisionEvents;
    private long measuredStart;
    private double previousStreamingWorkMs;
    private double inclusiveFrameTotalMs, inclusiveFrameMaxMs;
    private long inclusiveFrameCount;
    private string debugConfigPath="";
    private DateTime debugConfigStamp;
    private double nextConfigCheck;
    private GDict baseline = new();
    private static long Tick => Stopwatch.GetTimestamp();
    private static double Milliseconds(long from) => (Tick - from) * 1000.0 / Stopwatch.Frequency;
    private static double Now => Time.GetTicksUsec() / 1000000.0;
    private static NVector Numeric(Vector3 v) => new(v.X, v.Y, v.Z);

    public void Configure(string manifestPath, Camera3D observer)
    {
        camera = observer;
        var source = JsonDocument.Parse(FileAccess.GetFileAsString(manifestPath)).RootElement;
        if(source.TryGetProperty("floor_connections",out var connections))floorConnections=connections.Clone();
        if(source.TryGetProperty("object_anchors",out var anchors))objectAnchors=anchors.Clone();
        var scope = source.GetProperty("scope");
        if(scope.TryGetProperty("vertical_regions",out var vertical))
            foreach(var region in vertical.EnumerateArray())verticalRooms.Add(region.GetProperty("id").GetString()!);
        var authored = scope.GetProperty("source").GetString()!;
        var scopePath=source.TryGetProperty("scope_path",out var configuredScope)?configuredScope.GetString()!:"res://assets/data/presentation/campaign_rooms.dv";
        if (FileAccess.GetSha256(authored) != source.GetProperty("source_sha256").GetString()
            || FileAccess.GetSha256(scopePath) != source.GetProperty("scope_sha256").GetString())
            throw new InvalidDataException("Room bundles require recompilation after DV changes.");
        prepareMargin = scope.GetProperty("approach_margin_m").GetDouble();
        activeMargin = scope.GetProperty("active_margin_m").GetDouble();
        budgetMs = scope.GetProperty("preparation_budget_ms").GetDouble();
        foreach(var pair in scope.GetProperty("open_passages").EnumerateArray())
        {
            var ids=pair.EnumerateArray().Select(x=>x.GetString()!).ToArray();
            if (!openNeighbors.ContainsKey(ids[0])) openNeighbors[ids[0]]=[];
            if (!openNeighbors.ContainsKey(ids[1])) openNeighbors[ids[1]]=[];
            openNeighbors[ids[0]].Add(ids[1]);openNeighbors[ids[1]].Add(ids[0]);
        }
        debugConfigPath=ProjectSettings.GlobalizePath(scope.GetProperty("debug_config").GetString()!);
        var debugSettings=RuntimeDebugSettings.Parse(File.ReadAllText(debugConfigPath));
        debugConfigStamp=File.GetLastWriteTimeUtc(debugConfigPath);
        residency = new RoomResidency(JsonSerializer.SerializeToElement(new {
            inactive_retention_seconds = debugSettings.RoomInactiveRetentionSeconds,
            room_requests_per_slice = checked((int)scope.GetProperty("room_requests_per_slice").GetDouble()),
            retry_delay_seconds = scope.GetProperty("retry_delay_seconds").GetDouble(),
            inactive_simulation = scope.GetProperty("inactive_simulation").GetString()
        }));
        var elevation = source.GetProperty("elevation").GetSingle();
        var height = source.GetProperty("clear_height").GetSingle();
        foreach (var property in source.GetProperty("rooms").EnumerateObject())
        {
            var r = property.Value.EnumerateArray().Select(x => x.GetSingle()).ToArray();
            var roomElevation=source.TryGetProperty("room_elevations",out var levels)&&levels.TryGetProperty(property.Name,out var roomLevel)?roomLevel.GetSingle():elevation;
            var roomHeight=source.TryGetProperty("room_clear_heights",out var heights)&&heights.TryGetProperty(property.Name,out var roomHeightValue)?roomHeightValue.GetSingle():height;
            var bounds = new Aabb(new Vector3(r[0], roomElevation - .2f, -r[3]), new Vector3(r[2]-r[0], roomHeight+.4f, r[3]-r[1]));
            rooms.Add(property.Name, new(property.Name, bounds));
            residency.Register(property.Name, 0, 1);
        }
        foreach (var bundle in source.GetProperty("bundles").EnumerateArray())
        {
            if (FileAccess.GetSha256(bundle.GetProperty("path").GetString()!) != bundle.GetProperty("sha256").GetString())
                throw new InvalidDataException("Room bundle checksum mismatch.");
            bundles.Add(new(bundle.GetProperty("id").GetString()!, bundle.GetProperty("path").GetString()!,
                bundle.GetProperty("rooms").EnumerateArray().Select(x => x.GetString()!).ToArray()));
        }
        openings.AddRange(source.GetProperty("openings").EnumerateArray().Select(x => x.Clone()));
        if(scope.TryGetProperty("door_interlocks",out var interlocks))
            foreach(var pair in interlocks.EnumerateArray())
            {
                var ids=pair.EnumerateArray().Select(x=>x.GetString()!).ToArray();
                if(ids.Length!=2 || ids[0]==ids[1] || ids.Any(id=>!openings.Any(opening=>opening.GetProperty("id").GetString()==id && opening.TryGetProperty("door",out _))))
                    throw new InvalidDataException("Invalid authored door interlock.");
                interlockedOpenings.Add(ids[0],ids[1]);interlockedOpenings.Add(ids[1],ids[0]);
            }
        foreach(var aperture in openings)
        {
            var polygon=Aperture(aperture);
            portals.Add(new(aperture,polygon,polygon.Select(Numeric).ToArray(),
                aperture.GetProperty("room_links").EnumerateArray().Select(x=>x.GetString()!).ToArray(),
                polygon.Aggregate(Vector3.Zero,(sum,point)=>sum+point)/polygon.Count));
        }
        foreach (var aperture in openings)
            if (aperture.TryGetProperty("door",out var door))
                doorStates[door.GetProperty("id").GetString()!] = new DoorState {
                    Angle=-door.GetProperty("initial_angle").GetDouble(),
                    SelfClose=door.TryGetProperty("self_close_seconds",out var seconds)?seconds.GetDouble():0
                };
        directory = Path.Combine(ProjectSettings.GlobalizePath(receiptFolder),$"live-{OS.GetProcessId()}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        log = new StreamWriter(Path.Combine(directory,"timeline.jsonl"),false) { AutoFlush = false };
        baseline = Audit();
        Record("baseline", baseline);
        previousFrame = Tick;
    }

    public Godot.Collections.Array StairBuildings()
    {
        using var stairs=new Godot.Collections.Array();
        if(floorConnections.ValueKind==JsonValueKind.Array)
            foreach(var connection in floorConnections.EnumerateArray())
                stairs.Add(Godot.Json.ParseString(JsonSerializer.Serialize(new {
                    waypoints=connection.GetProperty("waypoints"), width=1.3
                })));
        using var topology=new GDict { ["stairs"]=stairs };
        using var building=new GDict { ["origin"]=new Godot.Collections.Array {0,0,0}, ["rotation_degrees"]=0, ["topology"]=topology };
        return new Godot.Collections.Array {building};
    }

    public override void _Process(double delta)
    {
        if (residency is null || closing) return;
        if (Now>=nextConfigCheck)
        {
            nextConfigCheck=Now+1;
            var stamp=File.GetLastWriteTimeUtc(debugConfigPath);
            if (stamp!=debugConfigStamp)
            {
                debugConfigStamp=stamp;
                try
                {
                    var settings=RuntimeDebugSettings.Parse(File.ReadAllText(debugConfigPath));
                    var previous=residency.RetentionSeconds;
                    residency.SetRetentionSeconds(settings.RoomInactiveRetentionSeconds);
                    Record("debug_config_changed",new {path=debugConfigPath,previous_seconds=previous,retention_seconds=residency.RetentionSeconds});
                }
                catch (Exception error) when (error is IOException or InvalidDataException or FormatException or JsonException or KeyNotFoundException)
                { Record("debug_config_rejected",new {path=debugConfigPath,message=error.Message}); }
            }
        }
        var now = Tick;
        var interval = (now - previousFrame) * 1000.0 / Stopwatch.Frequency;
        previousFrame = now;
        allFrames.Add(interval);
        if (measuring)
        {
            inclusiveFrameTotalMs+=interval;inclusiveFrameCount++;
            inclusiveFrameMaxMs=Math.Max(inclusiveFrameMaxMs,interval);
            var excluded=skipFrames>0;
            if (excluded) { skipFrames--; captureExcluded++; } else frames.Add(interval);
            log?.WriteLine(JsonSerializer.Serialize(new {kind="frame",time_us=Time.GetTicksUsec(),interval_ms=interval,capture_excluded=excluded}));
            if (!excluded && interval>25) Record("slow_frame",new {interval_ms=interval,
                previous_streaming_work_ms=previousStreamingWorkMs,
                process_ms=Performance.GetMonitor(Performance.Monitor.TimeProcess)*1000,
                physics_ms=Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess)*1000,
                gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2),
                managed_heap_bytes=GC.GetTotalMemory(false),
                pipeline_surface=Performance.GetMonitor(Performance.Monitor.PipelineCompilationsSurface),
                pipeline_draw=Performance.GetMonitor(Performance.Monitor.PipelineCompilationsDraw)});
        }
        var measured = Tick;
        if (visibilityDirty || camera.GlobalTransform!=lastView || camera.Size!=lastCameraSize)
        {
        visibilityDirty=false;lastView=camera.GlobalTransform;lastCameraSize=camera.Size;
        var planes = ViewPlanes();
        var containing=RoomAt(Focus-Vector3.Up);
        if(containing.Length>0)currentRoom=containing;
        else
        {
            // Keep a local visibility seed across wall thickness and authored room gaps.
            var nearest=rooms.Values.OrderBy(room=>RoomDistance(room,Focus)).FirstOrDefault();
            if(nearest is not null)currentRoom=nearest.Id;
        }
        var seeds=rooms.Values.Where(room=>currentRoom.Length>0 ? room.Id==currentRoom : RoomDistance(room,Focus)<=.1).Select(room=>room.Id);
        var sightLinks=new List<RoomSightLink>();
        foreach(var pair in openNeighbors)
            foreach(var id in pair.Value)
                sightLinks.Add(new(pair.Key,id,RoomVisibility.IntersectsBounds(Numeric(rooms[id].Bounds.Position),Numeric(rooms[id].Bounds.End),planes),true));
        foreach(var portal in portals)
        {
            var intersects=RoomVisibility.ClipAperture(portal.Numeric,planes).Count>0;
            var open=PortalOpen(portal.Source);
            foreach(var from in portal.Links)
                foreach(var to in portal.Links)
                    if(from!=to) sightLinks.Add(new(from,to,intersects,open));
        }
        var visibleRooms=RoomVisibility.ReachableRooms(seeds,sightLinks,false);
        var preparedRooms=RoomVisibility.ReachableRooms(seeds,sightLinks,true);
        var renderedRooms=new HashSet<string>(StringComparer.Ordinal);
        foreach (var room in rooms.Values)
        {
            var distance = RoomDistance(room, Focus);
            var visible = visibleRooms.Contains(room.Id)
                || openNeighbors.TryGetValue(currentRoom,out var neighbors) && neighbors.Contains(room.Id)
                   && RoomVisibility.IntersectsBounds(Numeric(room.Bounds.Position),Numeric(room.Bounds.End),planes);
            foreach (var portal in portals)
            {
                if (visible) break;
                var roomNear = portal.Links.Contains(room.Id);
                var currentNear = portal.Links.Contains(currentRoom);
                if (!roomNear || !currentNear) continue;
                // Activation ahead of the throat also covers the wall thickness between logical rooms.
                if (PortalOpen(portal.Source) && (FlatDistance(portal.Center,Focus) < activeMargin
                    || RoomVisibility.ClipAperture(portal.Numeric, planes).Count > 0))
                    visible = true;
            }
            if(visible)renderedRooms.Add(room.Id);
            residency.SetPrepare(room.Id,distance <= prepareMargin || preparedRooms.Contains(room.Id));
            residency.SetVisible(room.Id,visible || actorRouteRooms.ContainsKey(room.Id),Now);
        }
        playerVisibleRooms.Clear();playerVisibleRooms.UnionWith(renderedRooms);
        }
        foreach (var request in residency.Step(Now)) Apply(request);
        UpdateBundles(measured);
        foreach (var pending in preparations.Values.ToArray())
        {
            if (!bundles.Where(b=>b.Rooms.Contains(pending.Room)).All(b=>b.Node is not null)) continue;
            preparations.Remove(pending.Room);
            var effect = residency.Acknowledge(pending,true,Now);
            Record("preparation_complete",new {room=pending.Room,generation=pending.Generation});
            if (effect is not null) Apply(effect);
        }
        foreach (var pending in releases.Values.ToArray())
        {
            if (bundles.Any(b=>b.Rooms.Contains(pending.Room) && !Wanted(b) && (b.Node is not null || b.Loading))) continue;
            releases.Remove(pending.Room);
            residency.Acknowledge(pending,true,Now);
            Record("release_complete",new {room=pending.Room,generation=pending.Generation});
        }
        foreach (var bundle in bundles)
        {
            var enabled = Enabled(bundle);
            var changed = false;
            if (bundle.Node is not null && enabled != bundle.Enabled)
            {
                SetEnabled(bundle.Node,enabled);
                bundle.Enabled = enabled;
                changed = true;
                Record("bundle_activation",new {bundle=bundle.Id,enabled,ms=Milliseconds(measured)});
            }
            var render=enabled && bundle.Rooms.Any(playerVisibleRooms.Contains);
            if(bundle.Node is not null && (changed || render!=bundle.RenderVisible))
            {
                SetRenderEnabled(bundle.Node,render);
                bundle.RenderVisible=render;
            }
        }
        foreach (var pair in doors)
        {
            if (!doorStates.TryGetValue(pair.Key,out var state)) continue;
            var angle=pair.Value.Get("current_angle").AsDouble();
            state.Target=pair.Value.Get("motion_target").AsDouble();state.Moving=pair.Value.Get("moving").AsBool()||pair.Value.Get("motion_blocked").AsBool();
            if ((Math.Abs(state.Angle)>.01)!=(Math.Abs(angle)>.01)) visibilityDirty=true;
            state.Angle=angle;
            if (Math.Abs(angle-state.LoggedAngle)>=15)
            { Record("door_motion",new {door=pair.Key,angle,moving=pair.Value.Get("moving").AsBool(),blocked=pair.Value.Get("motion_blocked").AsBool()});state.LoggedAngle=angle; }
            var blocked=pair.Value.Get("motion_blocked").AsBool();
            if (blocked!=state.LoggedBlocked)
            {
                using var detail=pair.Value.Get("last_blocker").AsGodotDictionary();
                var blocker=detail.TryGetValue("name",out var name)?name.AsString():"";
                Record("door_blocked",new {door=pair.Key,angle,blocked,blocker});
                state.LoggedBlocked=blocked;
            }
            if (state.SelfClose>0 && Math.Abs(angle)>35 && state.CloseAt==0 && !pair.Value.Get("moving").AsBool() && !blocked) state.CloseAt=Now+state.SelfClose;
            if (state.CloseAt>0 && Now>=state.CloseAt && !pair.Value.Get("moving").AsBool())
            { pair.Value.Call("begin_motion",0,pair.Value.GetMeta("review_motion_speed")); state.CloseAt=0; Record("door_self_close",new {door=pair.Key,angle}); }
        }
        if (Engine.GetProcessFrames() % 60 == 0)
        {
            using var audit = Audit();
            peakNodes = Math.Max(peakNodes,(int)audit["nodes"]);
            peakCollisions = Math.Max(peakCollisions,(int)audit["active_collision_objects"]);
            Record("resources",audit);
            log?.Flush();
        }
        previousStreamingWorkMs=Milliseconds(measured);
    }

    private void Apply(RoomRequest request)
    {
        Record("room_transition",new {room=request.Room,phase=residency.Phase(request.Room).ToString(),effect=request.Effect.ToString(),generation=request.Generation});
        if (request.Effect == RoomEffect.Prepare) preparations[request.Room] = request;
        if (request.Effect == RoomEffect.Release) { preparations.Remove(request.Room); releases[request.Room] = request; }
    }

    private bool Wanted(Bundle bundle)
    {
        foreach(var id in bundle.Rooms)
            if (residency.Phase(id) is RoomPhase.Preparing or RoomPhase.Active or RoomPhase.CoolingDown) return true;
        return false;
    }
    private bool Enabled(Bundle bundle)
    {
        foreach(var id in bundle.Rooms)
            if (residency.Phase(id) is RoomPhase.Active or RoomPhase.CoolingDown) return true;
        return false;
    }

    private void UpdateBundles(long sliceStart)
    {
        for (var visited=0;visited<bundles.Count;visited++)
        {
            var bundle=bundles[bundleCursor];
            bundleCursor=(bundleCursor+1)%bundles.Count;
            var wanted = Wanted(bundle);
            if (bundle.Loading)
            {
                var status = ResourceLoader.LoadThreadedGetStatus(bundle.Path);
                if (status == ResourceLoader.ThreadLoadStatus.Loaded)
                {
                    var loaded = ResourceLoader.LoadThreadedGet(bundle.Path) as PackedScene;
                    bundle.Loading = false;
                    Record("resource_ready",new {bundle=bundle.Id,ms=Milliseconds(bundle.Started),cancelled=!wanted});
                    if (loaded is not null && wanted)
                    {
                        var start = Tick;
                        var node = loaded.Instantiate<Node3D>();
                        var instantiateMs = Milliseconds(start);
                        SetEnabled(node,false);
                        start = Tick;
                        AddChild(node);
                        var attachMs = Milliseconds(start);
                        bundle.Node = node;
                        bundle.CollisionObjects=Descendants(node).OfType<CollisionObject3D>().ToArray();
                        EmitSignal(SignalName.BundleAttached,node);
                        foreach (var candidate in Descendants(node))
                            if (candidate.HasMeta("opening_id") && candidate.HasMethod("begin_motion"))
                            {
                                var id=candidate.GetMeta("world_object_id").AsString();
                                if (!doorStates.ContainsKey(id)) continue;
                                doors[id]=candidate;
                                if (doorStates.TryGetValue(id,out var state))
                                {
                                    candidate.Call("set_angle_checked",state.Angle);
                                    if(state.Moving)candidate.Call("begin_motion",state.Target,candidate.GetMeta("review_motion_speed"));
                                }
                            }
                        var entry = new {bundle=bundle.Id,instantiate_ms=instantiateMs,attach_ms=attachMs,total_ms=Milliseconds(bundle.Started)};
                        assembly.Add(entry);
                        Record("assembly",entry);
                    }
                    loaded?.Dispose();
                }
                else if (status is ResourceLoader.ThreadLoadStatus.Failed or ResourceLoader.ThreadLoadStatus.InvalidResource)
                {
                    bundle.Loading = false;
                    Record("resource_failure",new {bundle=bundle.Id,status=status.ToString()});
                    foreach (var id in bundle.Rooms)
                        if (preparations.Remove(id,out var pending)) Apply(residency.CancelPreparation(pending,Now));
                }
            }
            if (!wanted && bundle.Node is not null)
            {
                foreach (var id in doors.Where(pair=>bundle.Node.IsAncestorOf(pair.Value)).Select(pair=>pair.Key).ToArray()) doors.Remove(id);
                SetEnabled(bundle.Node,false);
                bundle.Node.Free();
                bundle.Node = null;
                bundle.CollisionObjects=[];
                bundle.Enabled = false;
                Record("bundle_released",new {bundle=bundle.Id});
            }
            if (Milliseconds(sliceStart) >= budgetMs) break;
            if (wanted && bundle.Node is null && !bundle.Loading)
            {
                bundle.Started = Tick;
                var error = ResourceLoader.LoadThreadedRequest(bundle.Path,"PackedScene",false,ResourceLoader.CacheMode.Ignore);
                if (error != Error.Ok) throw new IOException($"Cannot prepare {bundle.Path}: {error}");
                bundle.Loading = true;
                Record("prepare_requested",new {bundle=bundle.Id});
            }
        }
    }

    private static void SetEnabled(Node node, bool enabled)
    {
        if (node is Node3D spatial && node.GetParent() is not Node3D) spatial.Visible = enabled;
        if (node is CollisionObject3D body)
        {
            if (!body.HasMeta("residency_layer")) { body.SetMeta("residency_layer",body.CollisionLayer); body.SetMeta("residency_mask",body.CollisionMask); }
            var leafBlocks=!body.HasMeta("door_leaf_collision_enabled") || body.GetMeta("door_leaf_collision_enabled").AsBool();
            body.CollisionLayer = enabled && leafBlocks ? (uint)body.GetMeta("residency_layer") : 0;
            body.CollisionMask = enabled ? (uint)body.GetMeta("residency_mask") : 0;
        }
        node.ProcessMode = enabled ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        var children=node.GetChildren();
        using var childrenOwner=(Godot.Collections.Array)children;
        foreach (var child in children) SetEnabled(child,enabled);
        // A bundle's child visibility is authored. Only its root controls rendering residency.
        if (node.HasMeta("rooms") && node is Node3D root) root.Visible = enabled;
    }

    private static void SetRenderEnabled(Node node,bool enabled)
    {
        if(node is VisualInstance3D visual)
        {
            if(!visual.HasMeta("residency_render_layers"))visual.SetMeta("residency_render_layers",visual.Layers);
            visual.Layers=enabled?(uint)visual.GetMeta("residency_render_layers"):0;
        }
        var children=node.GetChildren();
        using var childrenOwner=(Godot.Collections.Array)children;
        foreach(var child in children)SetRenderEnabled(child,enabled);
    }

    private NPlane[] ViewPlanes()
    {
        var reference = camera.GlobalPosition - camera.GlobalBasis.Z * 5;
        var frustum=camera.GetFrustum();
        using var frustumOwner=(Godot.Collections.Array)frustum;
        return frustum.Select(p=> {
            var n = p.Normal; var d = -p.D;
            if (n.Dot(reference)+d < 0) { n=-n; d=-d; }
            return new NPlane(Numeric(n),d);
        }).ToArray();
    }

    private static float FlatDistance(Aabb bounds, Vector3 point)
    {
        var x = Math.Max(bounds.Position.X-point.X,Math.Max(0,point.X-bounds.End.X));
        var z = Math.Max(bounds.Position.Z-point.Z,Math.Max(0,point.Z-bounds.End.Z));
        return MathF.Sqrt(x*x+z*z);
    }
    private float RoomDistance(Room room,Vector3 focus)
    {
        var flat=FlatDistance(room.Bounds,focus);
        var foot=focus.Y-1;
        var vertical=verticalRooms.Contains(room.Id)
            ? Math.Max(room.Bounds.Position.Y+.2f-foot,Math.Max(0,foot-room.Bounds.End.Y))
            : Math.Abs(foot-(room.Bounds.Position.Y+.2f));
        return MathF.Sqrt(flat*flat+vertical*vertical);
    }
    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X-b.X,a.Z-b.Z).Length();

    private static List<Vector3> Aperture(JsonElement aperture)
    {
        var r = aperture.GetProperty("rect").EnumerateArray().Select(x=>x.GetSingle()).ToArray();
        var bottom = aperture.GetProperty("bottom").GetSingle();
        var top = aperture.GetProperty("top").GetSingle();
        var frame=aperture.GetProperty("frame");
        if (frame.TryGetProperty("profile",out var profile) && profile.GetString()=="circular_blast")
        {
            var radius=frame.GetProperty("clear_diameter_m").GetSingle()*.5f;
            var center=new Vector3((r[0]+r[2])*.5f,bottom+frame.GetProperty("center_height_m").GetSingle(),-(r[1]+r[3])*.5f);
            var along=r[2]-r[0]>r[3]-r[1]?Vector3.Right:Vector3.Forward;
            var count=checked((int)frame.GetProperty("radial_segments").GetDouble());
            return Enumerable.Range(0,count).Select(i=>center+along*MathF.Cos(MathF.Tau*i/count)*radius+Vector3.Up*MathF.Sin(MathF.Tau*i/count)*radius).ToList();
        }
        var a = r[2]-r[0] > r[3]-r[1] ? new Vector2(r[0],(r[1]+r[3])/2) : new Vector2((r[0]+r[2])/2,r[1]);
        var b = r[2]-r[0] > r[3]-r[1] ? new Vector2(r[2],a.Y) : new Vector2(a.X,r[3]);
        return [new(a.X,bottom,-a.Y),new(b.X,bottom,-b.Y),new(b.X,top,-b.Y),new(a.X,top,-a.Y)];
    }

    private bool PortalOpen(JsonElement aperture)
    {
        if (!aperture.TryGetProperty("door",out var door))
        {
            // Some published mechanisms have static closed geometry but no
            // interactive leaf yet. Absence of a leaf is not an open permit.
            return PortalPolicy.OpenWithoutLeaf(aperture);
        }
        var id = door.GetProperty("id").GetString();
        if (doors.TryGetValue(id!,out var node)) return node.Get("moving").AsBool() || Math.Abs(node.Get("current_angle").AsDouble()) > .01;
        return Math.Abs(doorStates.TryGetValue(id!,out var state)?state.Angle:door.GetProperty("initial_angle").GetDouble()) > .01;
    }

    // Navigation is baked without leaves. An unloaded journey may only cross a
    // portal whose actual leaf is fully open; it must recheck while advancing.
    public bool CanTraversePath(Vector3[] points)
    {
        foreach(var aperture in openings)
        {
            bool open;
            if(aperture.TryGetProperty("door",out var door))
            {
                var id=door.GetProperty("id").GetString()!;
                var angle=doors.TryGetValue(id,out var node)?node.Get("current_angle").AsDouble():doorStates.GetValueOrDefault(id)?.Angle??0;
                var moving=node?.Get("moving").AsBool()??false;
                open=!moving && Math.Abs(angle)>=Math.Abs(door.GetProperty("open_angle").GetDouble())-1;
            }
            else open=PortalPolicy.OpenWithoutLeaf(aperture);
            if(open)continue;
            var rect=aperture.GetProperty("rect").EnumerateArray().Select(v=>v.GetSingle()).ToArray();
            var bottom=aperture.GetProperty("bottom").GetSingle();
            var top=aperture.GetProperty("top").GetSingle();
            for(var i=1;i<points.Length;i++)
            {
                var a=points[i-1];var b=points[i];
                if(Math.Max(a.Y,b.Y)<bottom-.2 || Math.Min(a.Y,b.Y)>top)continue;
                var t0=0f;var t1=1f;
                if(ClipAxis(a.X,b.X,rect[0],rect[2],ref t0,ref t1) && ClipAxis(-a.Z,-b.Z,rect[1],rect[3],ref t0,ref t1))return false;
            }
        }
        return true;
    }

    private static bool ClipAxis(float a,float b,float min,float max,ref float lo,ref float hi)
    {
        var delta=b-a;
        if(Math.Abs(delta)<1e-6f)return a>=min && a<=max;
        var first=(min-a)/delta;var last=(max-a)/delta;
        if(first>last)(first,last)=(last,first);
        lo=Math.Max(lo,first);hi=Math.Min(hi,last);
        return lo<=hi;
    }

    public void OperateNearestDoor()
    {
        var forward=-camera.GlobalBasis.Z;
        var nearest=doors.Values.OfType<Node3D>().Select(node=> {
            var aperture=openings.FirstOrDefault(o=>o.TryGetProperty("door",out var leaf)&&leaf.GetProperty("id").GetString()==node.GetMeta("world_object_id").AsString());
            var polygon=Aperture(aperture);
            var center=polygon.Aggregate(Vector3.Zero,(sum,point)=>sum+point)/polygon.Count;
            var direction=center-camera.GlobalPosition;direction.Y=0;
            return new {Node=node,Distance=direction.Length(),Facing=direction.Normalized().Dot(forward),Range=node.GetMeta("review_control_radius_m").AsDouble()};
        }).Where(pair=>pair.Facing>.5)
            .Where(pair=>pair.Distance < pair.Range).OrderByDescending(pair=>pair.Facing).ThenBy(pair=>pair.Distance).FirstOrDefault();
        if (nearest is null) return;
        var node=nearest.Node;
        var target = Math.Abs(node.Get("current_angle").AsDouble()) < 10 ? node.GetMeta("review_open_angle").AsDouble() : 0;
        var accepted=node.Call("begin_motion",target,node.GetMeta("review_motion_speed")).AsBool();
        if (accepted) visibilityDirty=true;
        Record("door_input",new {opening=node.GetMeta("opening_id").AsString(),target,distance_m=nearest.Distance,accepted});
    }

    public void MarkCapture() => skipFrames = 2;
    public bool IsInitialReady() => residency is not null && bundles.All(b=>!b.Loading)
        && rooms.Keys.Any(id=>residency.Phase(id)==RoomPhase.Active);
    public void BeginMeasuredRun()
    {
        measuring=true; previousFrame=Tick; measuredStart=Tick;
        using var audit=Audit();
        Record("measured_run_begin",audit);
        Node.PrintOrphanNodes();
    }
    public void RecordCollision(Node collider)
    {
        collisionEvents++;
        if (collisionEvents%15==1) Record("observer_collision",new {collider=collider.GetPath().ToString(),position=camera.GlobalPosition.ToString()});
    }
    public void RecordInput(string action) => Record("input",new {action,position=camera.GlobalPosition.ToString()});
    private static IEnumerable<Node> Descendants(Node node)
    {
        yield return node;
        var children=node.GetChildren();
        using var childrenOwner=(Godot.Collections.Array)children;
        foreach (var child in children) foreach (var descendant in Descendants(child)) yield return descendant;
    }
    private GDict Audit()
    {
        var orphanDetails=new Godot.Collections.Array<string>(Node.GetOrphanNodeIds().Select(id=>{
            var node=GodotObject.InstanceFromId(unchecked((ulong)id)) as Node;
            return node is null?id.ToString():$"{id}:{node.Name}:{node.GetClass()}:{(node.GetScript().AsGodotObject() as Resource)?.ResourcePath}";
        }));
        using var orphanDetailsOwner=(Godot.Collections.Array)orphanDetails;
        return new() {
        ["nodes"]=(int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount),
        ["orphan_nodes"]=(int)Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount),
        ["static_memory_bytes"]=(long)Performance.GetMonitor(Performance.Monitor.MemoryStatic),
        ["managed_heap_bytes"]=GC.GetTotalMemory(false),
        ["gc0"]=GC.CollectionCount(0),["gc1"]=GC.CollectionCount(1),["gc2"]=GC.CollectionCount(2),
        ["active_collision_objects"]=bundles.Where(b=>b.Node is not null).Sum(b=>b.CollisionObjects.Count(n=>GodotObject.IsInstanceValid(n)&&n.CollisionLayer != 0)),
        ["loaded_bundles"]=bundles.Count(b=>b.Node is not null), ["pending_loads"]=bundles.Count(b=>b.Loading),
        ["orphan_details"]=orphanDetails
        };
    }
    private static GDict Native(object value)
    {
        using var parsed=Json.ParseString(JsonSerializer.Serialize(value));
        return parsed.AsGodotDictionary();
    }
    private void Record(string kind, object details)
    {
        var body = details is GDict dictionary ? dictionary : Native(details);
        var entry = new GDict { ["utc"]=DateTimeOffset.UtcNow.ToString("O"),["time_us"]=(long)Time.GetTicksUsec(),["kind"]=kind,["details"]=body };
        if (details is not GDict) body.Dispose();
        log?.WriteLine(Json.Stringify(entry));
        recent.Add(entry);
        if (recent.Count > 80) {recent[0].Dispose();recent.RemoveAt(0);}
    }
    public GDict Observation()
    {
        var ordered = frames.Order().ToArray();
        using var states = new GDict();
        foreach (var id in rooms.Keys) states[id]=residency.Phase(id).ToString();
        using var doorViews=new GDict();
        foreach(var pair in doorStates)
        {
            var node=doors.GetValueOrDefault(pair.Key);
            using var doorView=new GDict {
                ["angle"]=pair.Value.Angle,["loaded"]=node is not null,["close_at"]=pair.Value.CloseAt,
                ["leaf_collision_enabled"]=(node?.GetNodeOrNull<CollisionObject3D>("VisualRoot/Hinge/CollisionRoot/StaticBody3D")?.CollisionLayer??0)!=0,
                ["moving"]=node?.Get("moving").AsBool()??false,["blocked"]=node?.Get("motion_blocked").AsBool()??false
            };
            if (node is not null)
            {
                using var blocker=node.Get("last_blocker");
                doorView["last_blocker"]=blocker;
            }
            else
            {
                using var empty=new GDict();
                doorView["last_blocker"]=empty;
            }
            doorViews[pair.Key]=doorView;
        }
        var viewPlanes=ViewPlanes();
        var portalViews=new Godot.Collections.Array<GDict>();
        using var portalViewsOwner=(Godot.Collections.Array)portalViews;
        foreach(var aperture in openings)
        {
            var polygon=Aperture(aperture);
            var clipped=RoomVisibility.ClipAperture(polygon.Select(Numeric).ToArray(),viewPlanes);
            using var portalView=Native(new {
                id=aperture.GetProperty("id").GetString(),
                room_links=aperture.GetProperty("room_links").EnumerateArray().Select(x=>x.GetString()).ToArray(),
                structural_world_polygon=polygon.Select(p=>new[]{p.X,p.Y,p.Z}).ToArray(),
                clipped_world_polygon=clipped.Select(p=>new[]{p.X,p.Y,p.Z}).ToArray(),
                in_camera_frustum=clipped.Count>0,
                clear_width_m=aperture.GetProperty("frame").GetProperty("net_width").GetDouble(),
                clear_height_m=aperture.GetProperty("frame").GetProperty("net_height").GetDouble(),
                profile=aperture.GetProperty("frame").TryGetProperty("profile",out var profile)?profile.GetString():"rectangular",
                leaf_open=PortalOpen(aperture)
            });
            portalViews.Add(portalView);
        }
        using var resources=Audit();
        var recentEvents=new Godot.Collections.Array<GDict>(recent);
        using var recentEventsOwner=(Godot.Collections.Array)recentEvents;
        var planeViews=new Godot.Collections.Array<GDict>();
        using var planeViewsOwner=(Godot.Collections.Array)planeViews;
        foreach(var plane in viewPlanes)
        {
            using var view=Native(new {normal=new[]{plane.Normal.X,plane.Normal.Y,plane.Normal.Z},d=plane.D});
            planeViews.Add(view);
        }
        var result = new GDict {
            ["scope"]=receiptScope,["current_room"]=currentRoom,["position"]=camera.GlobalPosition,
            ["room_retention_seconds"]=residency.RetentionSeconds,["debug_config_path"]=debugConfigPath,
            ["phases"]=states,["resources"]=resources,["baseline"]=baseline,["peak_nodes"]=peakNodes,["peak_active_collision_objects"]=peakCollisions,
            ["doors"]=doorViews,["forward"]=-camera.GlobalBasis.Z,
            ["portal_measurements"]=portalViews,["fov_degrees"]=camera.Fov,
            ["frustum_planes"]=planeViews,
            ["frame_samples"]=ordered.Length,["frame_mean_ms"]=ordered.Length>0?ordered.Average():0,
            ["frame_p99_ms"]=ordered.Length>0?ordered[Math.Clamp((int)Math.Ceiling(ordered.Length*.99)-1,0,ordered.Length-1)]:0,
            ["frame_max_ms"]=ordered.Length>0?ordered[^1]:0,["capture_excluded_frames"]=captureExcluded,
            ["inclusive_frame_samples"]=inclusiveFrameCount,
            ["inclusive_frame_mean_ms"]=inclusiveFrameCount>0?inclusiveFrameTotalMs/inclusiveFrameCount:0,
            ["inclusive_frame_max_ms"]=inclusiveFrameMaxMs,
            ["inclusive_frame_fps"]=inclusiveFrameTotalMs>0?inclusiveFrameCount*1000/inclusiveFrameTotalMs:0,
            ["raw_all_frame_mean_ms"]=allFrames.Count>0?allFrames.Average():0,
            ["measurement_seconds"]=measuredStart>0?Milliseconds(measuredStart)/1000:0,["collision_events"]=collisionEvents,
            ["receipt_directory"]=directory,["recent_events"]=recentEvents
        };
        log?.Flush();
        File.WriteAllText(Path.Combine(directory,"summary.json"),Json.Stringify(result,"  "));
        return result;
    }
    public override void _ExitTree()
    {
        closing = true;
        if (residency is null) return;
        foreach (var bundle in bundles)
        {
            if (bundle.Loading) ResourceLoader.LoadThreadedGet(bundle.Path)?.Dispose();
            if (bundle.Node is not null && GodotObject.IsInstanceValid(bundle.Node)) bundle.Node.Free();
            bundle.Node=null;
            bundle.CollisionObjects=[];
            bundle.Loading=false;
        }
        doors.Clear();
        using var audit=Audit();
        Record("shutdown",audit);
        var summaryPath=Path.Combine(directory,"summary.json");
        if (File.Exists(summaryPath))
        {
            using var parsed=Json.ParseString(File.ReadAllText(summaryPath));
            using var summary=parsed.AsGodotDictionary();
            summary["resources"]=audit;
            summary["host_status"]="closed";
            summary["shutdown_time_us"]=(long)Time.GetTicksUsec();
            File.WriteAllText(summaryPath,Json.Stringify(summary,"  "));
        }
        log?.Dispose(); log=null;
        foreach(var entry in recent) entry.Dispose();
        recent.Clear();
        baseline.Dispose();
    }
}
