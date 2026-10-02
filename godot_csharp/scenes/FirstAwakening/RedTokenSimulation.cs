using DualOntologyWorldEngine.Core;
using System.Collections.Immutable;
namespace DualOntologyWorldEngine.Game.FirstAwakening;

/// <summary>每次候选使用全新现实、独立主体、独立器具引用；现场证据不进入此世界。</summary>
public sealed partial class RedTokenSimulation : IDisposable
{
    public static readonly Tag Holding = new("Holding"), Filter = new("Filter"), Ash = new("Ash"), Clear = new("Clear"),
        Confirmed = new("Confirmed"), Wiped = new("Wiped"), Washed = new("Washed"), Wrapped = new("Wrapped"),
        Dirty = new("Dirty"), Received = new("Received"), Pushed = new("Pushed"), Taken = new("Taken"), Read = new("Read"), FilterReady = new("FilterReady"), Bolt = new("Bolt"), Sound = new("Sound"), Basin = new("Basin"), Broken = new("Broken"), Released = new("Released"), Accepted = new("Accepted"), Closed = new("Closed"), Narrow = new("Narrow");
    public WorldRuntime World { get; } = new();
    public SimulationSession Runtime { get; }
    public Dictionary<string, SoulEssence> Souls { get; } = new();
    public Dictionary<string, Essence> Objects { get; } = new();
    public List<RedTokenFrame> Snapshots { get; } = new();
    public List<string> Events { get; } = new();
    public List<string> Failures { get; } = new();
    private readonly Dictionary<(string Actor, string Object), Projection> _mental = new();
    private readonly Dictionary<string, Projection> _views = new();
    private readonly Dictionary<string, ActionExecution> _holding = new();
    private readonly Dictionary<string, HashSet<string>> _locallyObserved = new();
    private readonly Essence _room;
    private string? _holder;
    public string? Holder => _holder;
    public bool Flowing => _holder is { } h && Value(h, Holding) == 1;
    public bool Waiting => Flowing && Value("confirmation", Confirmed) != 1;
    public bool HistoricalGray => Value("upstream", Ash) == 1;
    public bool Complete => HistoricalGray && Value("confirmation", Clear) == 1 && !Flowing;
    public bool WhiteAvailable => Value("receipt_red", Received) == 1 && Value("receipt_handle", Received) == 1 && Value("receipt_red", Pushed) == 1 && Value("receipt_handle", Pushed) == 1;

    public RedTokenWorldKind Kind { get; }
    public RedTokenSimulation(RedTokenWorldKind kind = RedTokenWorldKind.History)
    {
        Kind = kind;
        Runtime = new(World);
        _room = Make("room", ProjectionKind.Space);
        foreach (var actor in new[] { "侦探", "工程师霍铎", "主厨沈鼎", "音乐家苏泠", "值守者", "未知后取者", "接待者" })
        {
            var soul = new SoulEssence(actor); Souls.Add(actor, soul); World.Project(soul, actor + "心灵");
            var body = Make(actor, ProjectionKind.Body); soul.Relations.Add((Relations.Controls, body));
        }
        foreach (var obj in new[] { "handle", "red", "cloth", "wedge", "paper", "receiver", "white", "confirmation", "water", "chair", "room_sign", "upstream", "clip", "rack" }) Make(obj);
        Make("exception", ProjectionKind.Procedure);
        Make("door", ProjectionKind.Boundary); Make("pipe", ProjectionKind.Boundary);
        foreach (var actor in new[] { "侦探", "工程师霍铎", "主厨沈鼎", "音乐家苏泠" }) Make("reward:" + actor);
        foreach (var space in new[] { "A", "B", "C", "D", "R05", "Dorm" }) Make(space, ProjectionKind.Space);
        foreach (var (from, to) in new[] { ("room", "A"), ("A", "R05"), ("A", "Dorm"), ("A", "B"), ("B", "C"), ("B", "D") })
        foreach (var (start, end) in new[] { (from, to), (to, from) })
        {
            var route = Make("route:" + start + ":" + end, ProjectionKind.Boundary);
            route.Get<SpatialProperty>()!.Space = Objects[start];
            route.Add(new PassageProperty()).Connections[Objects[start]] = Objects[end];
        }
        foreach (var obj in new[] { "handle", "rack", "exception", "pipe" }) Objects[obj].Get<SpatialProperty>()!.Space = Objects["B"];
        foreach (var obj in new[] { "cloth", "clip", "upstream", "confirmation", "water" }) Objects[obj].Get<SpatialProperty>()!.Space = Objects["C"];
        foreach (var obj in new[] { "wedge", "paper", "door" }) Objects[obj].Get<SpatialProperty>()!.Space = Objects["D"];
        foreach (var obj in new[] { "red", "receiver", "white", "chair", "room_sign" }) Objects[obj].Get<SpatialProperty>()!.Space = Objects["A"];
        Objects["chair"].Get<SpatialProperty>()!.Space = Objects["B"];
        Objects["wedge"].Get<StateProperty>()!.Values[Narrow] = 1;
        Objects["handle"].Get<SpatialProperty>()!.Space = Objects["rack"];
        InitializeObjects();
        if (Kind != RedTokenWorldKind.Scene)
        {
            var confirmation = Objects["confirmation"].Get<StateProperty>()!;
            foreach (var tag in new[] { Clear, Confirmed })
            {
                confirmation.Observable.Add(tag);
                confirmation.Values.TryAdd(tag, 0);
            }
        }
        // 初始岗位记忆来自各自实际看到的身体与器具，不共享整场认知。
        foreach (var actor in Souls.Keys) See(actor, actor);
        InitializePeople();
        Runtime.Completed += execution =>
        {
            if (_holder is { } holder && _holding.GetValueOrDefault(holder) == execution && execution.Status == ExecutionStatus.Completed)
            {
                _holder = null;
                RefreshLocalObservation(holder);
                if (Value(holder, Released) == 1)
                {
                    Events.Add(holder + ":持柄父动作完成");
                    Snapshot(holder, "本人收到具名确认，持柄父动作完成并停流");
                }
            }
        };
    }
    private Essence Make(string name, ProjectionKind kind = ProjectionKind.Object)
    {
        var obj = new Essence(name, kind); Objects.Add(name, obj);
        var state = obj.Add(new StateProperty());
        foreach (var tag in new[] { Holding, Filter, Ash, Clear, Confirmed, Wiped, Washed, Wrapped, Dirty, Received, Pushed, Taken, Read, FilterReady, Bolt, Sound, Basin, Broken, Released, Accepted, Closed, Narrow }) state.Observable.Add(tag);
        obj.Add(new SpatialProperty { Space = name == "room" ? null : Objects.GetValueOrDefault("room") });
        _views.Add(name, World.Project(obj, name)); return obj;
    }
    public int Value(string obj, Tag tag) => Objects[obj].Get<TemporaryStateProperty>()?.Override(tag) ?? Objects[obj].Get<StateProperty>()!.Values.GetValueOrDefault(tag);
    private Projection See(string actor, string obj)
    {
        var actorSpace = Location(actor); var targetSpace = Location(obj);
        if (actorSpace != targetSpace && !(actorSpace is "B" or "C" && targetSpace is "B" or "C" && (obj == "confirmation" || Objects[obj].Kind == ProjectionKind.Body)) && obj != actor)
            throw new InvalidOperationException("此岗位看不到该物，须先近身观察。");
        if (!_locallyObserved.TryGetValue(actor, out var observed)) _locallyObserved[actor] = observed = [];
        observed.Add(obj);
        return ObserveObject(actor, obj, obj == actor || Kind != RedTokenWorldKind.Scene || KnownObject(actor, obj)?.Tags.GetValueOrDefault(Identified) == 1);
    }
    private void RefreshLocalObservation(string actor)
    {
        if (!_locallyObserved.TryGetValue(actor, out var observed)) return;
        foreach (var key in observed.ToArray())
            if (key == actor || Location(key) == Location(actor))
                ObserveObject(actor, key, KnownObject(actor, key)?.Tags.GetValueOrDefault(Identified) == 1);
    }
    private static string Chinese(string label) => label switch
    {
        "confirm_filter" => "主厨确认夹滤到位", "clip_filter" or "assign_tool" => "夹牢滤布并确认捕集岗位", "capture" => "灰流先经过上游当班捕集片，再落入盆中", "inspect_clear" => "主厨亲见同号转清", "confirm" => "主厨向工程师发出具名转清确认", "wipe" => "取下布擦地，留下地板纤维", "wash" => "厨房清水洗布，盆内留灰", "dirty_wedge" => "门底楔在烟灰路中受灰", "take_wedge" => "撤取门底楔", "wrap_wedge" => "包裹同一沾灰楔，纸内形成转印", "close_door" => "撤楔后配重合门", "take_white" => "双槽交回后取得白片", "misrecord_clear" => "把洗布后的清水误记为转清确认", "open_drawer" => "打开前抽屉，原封条断开", "move_red_to_chair" => "从前抽屉后取同一红筹放到椅上", "open_copper" => "现实铜口质询索取院务移物单", _ => label
    };
    private RedTokenResult Apply(string actor, RedTokenOperation operation, Func<Projection, Effect> effect, string? description = null)
    {
        if (!Souls.ContainsKey(actor)) return new(false, "没有该行动者。");
        if (_holding.GetValueOrDefault(actor)?.Status == ExecutionStatus.Pending) return new(false, "该身体仍在持续持柄等待，不能换岗。");
        try
        {
            var body = See(actor, actor);
            var prepared = Sequence(new Effect.Require(new Condition.At(body, See(actor, Location(actor)))) , effect(body));
            var procedure = RememberProcedure(actor, operation, prepared, description ?? Chinese(OperationKey(operation)));
            var until = Runtime.CTB.Now + 1;
            var execution = Runtime.Invoke(Souls[actor], body, procedure, 1);
            Runtime.CTB.RunUntil(until);
            if (execution.Status == ExecutionStatus.Failed)
            {
                var failure = OperationKey(operation) + ":" + execution.Result!.Failure;
                Failures.Add(failure); return new(false, failure);
            }
            RefreshLocalObservation(actor);
            var caption = description ?? Chinese(OperationKey(operation));
            Events.Add(actor + ":" + caption); Snapshot(actor, caption); return new(true, caption);
        }
        catch (InvalidOperationException e) { Failures.Add(OperationKey(operation) + ":" + e.Message); return new(false, e.Message); }
    }
    private Effect Set(string actor, string obj, Tag tag, int value)
    {
        var target = See(actor, obj);
        // Set 本身不检查可达；用真实身体岗位约束，感知仍经宿主筛选入口。
        var visited = new HashSet<Essence>();
        for (Essence? item = Objects[obj]; item is not null && visited.Add(item); item = item.Get<SpatialProperty>()?.Holder ?? item.Get<SpatialProperty>()?.Space)
            if (item.Kind == ProjectionKind.Body && !ReferenceEquals(item, Objects[actor]))
                throw new InvalidOperationException("不能操作另一人仍持有的物件。");
        return Sequence(new Effect.Require(new Condition.At(See(actor, actor), See(actor, Location(obj)))), new Effect.Set(target, tag, value));
    }
    private Effect Sequence(params Effect[] effects) => new Effect.Sequence(effects.ToImmutableArray());
    private Effect Take(string actor, string obj)
    {
        var target = See(actor, obj);
        var requirements = new List<Condition> { new Condition.Tagged(target, Portable, 1) };
        if (IsAtObject(Objects[obj], "drawer")) requirements.Add(new Condition.Not(new Condition.Tagged(See(actor, "drawer"), Closed, 1)));
        return Sequence(new Effect.Require(new Condition.All(requirements.ToImmutableArray())), new Effect.Act(ActionOp.Take, target));
    }
    public RedTokenResult MoveTo(string actor, string space) => Travel(actor, space);
    public string ObjectLocation(string key) => Location(key);
    private RedTokenResult Travel(string actor, string destination)
    {
        if (!Souls.ContainsKey(actor) || !Objects.TryGetValue(destination, out var destinationObject) || destinationObject.Kind != ProjectionKind.Space) return new(false, "没有该行动者或岗位。");
        if (_holding.GetValueOrDefault(actor)?.Status == ExecutionStatus.Pending) return new(false, "该身体仍在持柄，不能移动。");
        var from = Location(actor);
        if (from == destination) return new(true, "已在岗位。");
        var previous = new Dictionary<string, string?> { [from] = null };
        var queue = new Queue<string>(); queue.Enqueue(from);
        while (queue.Count > 0 && !previous.ContainsKey(destination))
        {
            var current = queue.Dequeue();
            foreach (var route in Objects.Values.Where(o => o.Get<PassageProperty>() is not null))
                if (route.Get<PassageProperty>()!.Connections.TryGetValue(Objects[current], out var next) && previous.TryAdd(next.TrueName, current)) queue.Enqueue(next.TrueName);
        }
        if (!previous.ContainsKey(destination)) return new(false, "没有相邻通路可到该岗位。");
        var path = new Stack<string>();
        for (var step = destination; step != from; step = previous[step]!) path.Push(step);
        while (path.Count > 0)
        {
            var next = path.Pop(); var body = See(actor, actor); var route = See(actor, "route:" + from + ":" + next);
            var due = Runtime.CTB.Now + 1;
            var execution = Runtime.Submit(Souls[actor], new ActionIntent(body, Sequence(new Effect.Require(new Condition.At(body, See(actor, from))), new Effect.Act(ActionOp.Travel, route)), 1));
            Runtime.CTB.RunUntil(due);
            if (execution.Status != ExecutionStatus.Completed) return new(false, "相邻通路移动失败：" + execution.Result?.Failure);
            from = next; See(actor, actor); Snapshot(actor, "前往" + next + "岗位");
        }
        return new(true, "已到" + destination + "岗位。");
    }
    public RedTokenResult Perform(string actor, string action, string station = "") => TryParseOperation(action, out var operation) ? Perform(actor, operation, station) : new(false, "无法识别现场动作。");
    public RedTokenResult Perform(string actor, RedTokenOperation operation, string station = "")
    {
        if (!Souls.ContainsKey(actor)) return new(false, "没有该行动者。");
        if (Kind == RedTokenWorldKind.Scene && operation is not (RedTokenOperation.VerifyReward or RedTokenOperation.OpenCopper)) return new(false, "现场证物只供观察；历史动作须在独立候选或安全试验世界执行。");
        var targetKey = OperationTarget(actor, operation);
        if (!Objects.TryGetValue(targetKey, out var target) || target.Get<RedTokenObjectProperty>() is not { } properties || !properties.Operations.Contains(operation)) return new(false, "该对象不支持此操作。");
        var intended = operation switch
        {
            RedTokenOperation.VerifyReward or RedTokenOperation.ReturnRoom or RedTokenOperation.GoRoomWithoutSign => "A",
            RedTokenOperation.ReadException or RedTokenOperation.TakeHandle or RedTokenOperation.Hold or RedTokenOperation.SafePress or RedTokenOperation.KnockPipe or RedTokenOperation.OpenCopper or RedTokenOperation.DepositRed or RedTokenOperation.DepositOther or RedTokenOperation.PushRed or RedTokenOperation.PushOther or RedTokenOperation.TakeWhite or RedTokenOperation.CarryRedWithoutReturn or RedTokenOperation.OpenDrawer or RedTokenOperation.MoveRedToChair => "B",
            RedTokenOperation.AssignTool or RedTokenOperation.ClipFilter or RedTokenOperation.ReleaseTool or RedTokenOperation.ConfirmFilter or RedTokenOperation.Capture or RedTokenOperation.WaterFlow or RedTokenOperation.InspectClear or RedTokenOperation.Confirm or RedTokenOperation.Wipe or RedTokenOperation.Wash or RedTokenOperation.MisrecordClear => "C",
            RedTokenOperation.TakeWedge or RedTokenOperation.ReleaseWedge or RedTokenOperation.DirtyWedge or RedTokenOperation.Bolt or RedTokenOperation.CloseDoor or RedTokenOperation.KnockGap => "D",
            _ => Location(actor)
        };
        if (!string.IsNullOrWhiteSpace(station) && station != intended) return new(false, "该操作需要在" + intended + "岗位完成。");
        try
        {
            if (operation != RedTokenOperation.Release) { var travel = Travel(actor, intended); if (!travel.Ok) return travel; }
            return ExecuteOperation(actor, operation);
        }
        catch (InvalidOperationException e) { Failures.Add(e.Message); return new(false, e.Message); }
    }
    private static string OperationTarget(string actor, RedTokenOperation operation) => operation switch
    {
        RedTokenOperation.VerifyReward => "reward:" + actor,
        RedTokenOperation.ReadException => "exception",
        RedTokenOperation.TakeHandle or RedTokenOperation.Hold or RedTokenOperation.SafePress or RedTokenOperation.Release => "handle",
        RedTokenOperation.AssignTool or RedTokenOperation.ClipFilter or RedTokenOperation.ReleaseTool or RedTokenOperation.Wipe or RedTokenOperation.Wash or RedTokenOperation.Capture => "cloth",
        RedTokenOperation.ConfirmFilter or RedTokenOperation.InspectClear or RedTokenOperation.Confirm or RedTokenOperation.MisrecordClear => "confirmation",
        RedTokenOperation.WaterFlow => "water",
        RedTokenOperation.Bolt or RedTokenOperation.KnockGap or RedTokenOperation.CloseDoor => "door",
        RedTokenOperation.DirtyWedge or RedTokenOperation.ReleaseWedge or RedTokenOperation.TakeWedge or RedTokenOperation.WrapWedge => "wedge",
        RedTokenOperation.KnockPipe or RedTokenOperation.OpenCopper => "pipe",
        RedTokenOperation.DepositRed or RedTokenOperation.DepositOther or RedTokenOperation.PushRed or RedTokenOperation.PushOther or RedTokenOperation.OpenDrawer => "receiver",
        RedTokenOperation.TakeWhite => "white",
        RedTokenOperation.ReturnRoom or RedTokenOperation.GoRoomWithoutSign => "room_sign",
        _ => "red"
    };
    private RedTokenResult ExecuteOperation(string actor, RedTokenOperation operation)
    {
        switch (operation)
        {
            case RedTokenOperation.VerifyReward: return Apply(actor, operation, _ => Set(actor, "reward:" + actor, Read, 1));
            case RedTokenOperation.ReadException:
                var read = Apply(actor, operation, _ => Sequence(Set(actor, "exception", Read, 1), Set(actor, actor, Read, 1), Set(actor, "signed_exception", Read, 1)));
                if (read.Ok)
                {
                    RememberSelf(actor, Read, 1, "本人已读取并接受排灰特例。");
                    LearnHoldRules(actor);
                }
                return read;
            case RedTokenOperation.AssignTool:
            case RedTokenOperation.ClipFilter:
                if (Flowing) return new(false, "持续流动时不能调整岗位滤布。");
                return Apply(actor, operation, _ => Sequence(Take(actor, "cloth"), new Effect.Act(ActionOp.Place, See(actor, "cloth"), See(actor, "clip")), Set(actor, "cloth", Filter, 1), Set(actor, "confirmation", FilterReady, 0), Set(actor, "confirmation", Clear, 0), Set(actor, "confirmation", Confirmed, 0)));
            case RedTokenOperation.ReleaseTool:
                if (Flowing) return new(false, "持续流动时不能取下岗位布。");
                return Apply(actor, operation, _ => Sequence(Take(actor, "cloth"), new Effect.Act(ActionOp.Place, See(actor, "cloth"), See(actor, "C")), Set(actor, "cloth", Filter, 0), Set(actor, "confirmation", FilterReady, 0)));
            case RedTokenOperation.TakeHandle:
                var taken = Apply(actor, operation, _ => Sequence(new Effect.Require(new Condition.Not(new Condition.Tagged(See(actor, "receipt_handle"), Received, 1))), Take(actor, "handle")));
                if (taken.Ok) LearnHoldRules(actor);
                return taken;
            case RedTokenOperation.ConfirmFilter:
                if (!HasKnowledge(actor, SupplySkill)) return new(false, "须由掌握接收与供给规程的人现场具名确认。");
                var filtered = Apply(actor, operation, _ => Sequence(new Effect.Require(new Condition.All([new Condition.At(See(actor, "cloth"), See(actor, "clip")), new Condition.Tagged(See(actor, "cloth"), Filter, 1)])), Set(actor, "confirmation", FilterReady, 1)));
                if (filtered.Ok)
                {
                    See(actor, "confirmation");
                    if (Objects["handle"].Get<SpatialProperty>()!.Holder is { } assigned && Souls.ContainsKey(assigned.TrueName))
                        DeliverSignal(actor, assigned.TrueName, FilterReady, actor + "具名确认本岗位夹滤到位");
                }
                return filtered;
            case RedTokenOperation.Bolt: return Apply(actor, operation, _ => Sequence(new Effect.Require(new Condition.Tagged(See(actor, "door"), Closed, 1)), Set(actor, "door", Bolt, 1)));
            case RedTokenOperation.SafePress: return StartHold(actor, true);
            case RedTokenOperation.Hold: return StartHold(actor, false);
            case RedTokenOperation.Capture:
                if (_holder is null || _holder == actor) return new(false, "须另人持续持柄。");
                return Apply(actor, operation, _ => Sequence(new Effect.Require(new Condition.All([new Condition.Tagged(See(actor, _holder), Holding, 1), new Condition.At(See(actor, "cloth"), See(actor, "clip")), new Condition.Tagged(See(actor, "cloth"), Filter, 1)])), Set(actor, "cloth", Ash, 1), Set(actor, "upstream", Ash, 1), Set(actor, "water", Basin, 1)));
            case RedTokenOperation.WaterFlow:
                if (_holder is null) return new(false, "空载弹簧使未持柄水路停止。");
                return Apply(actor, operation, _ => Sequence(new Effect.Require(new Condition.Tagged(See(actor, _holder), Holding, 1)), Set(actor, "water", Wiped, 1)));
            case RedTokenOperation.InspectClear:
                if (!HasKnowledge(actor, SupplySkill) || _holder is null) return new(false, "接收岗位须在捕灰后现场观察同号转清。");
                var result = Apply(actor, operation, _ => Sequence(new Effect.Require(new Condition.All([new Condition.Tagged(See(actor, _holder), Holding, 1), new Condition.Tagged(See(actor, "upstream"), Ash, 1)])), Set(actor, "confirmation", Clear, 1)));
                if (result.Ok) See(actor, "confirmation"); return result;
            case RedTokenOperation.Confirm:
                if (!HasKnowledge(actor, SupplySkill) || _holder is null || !_mental.TryGetValue((actor, "confirmation"), out var chefSignal) || chefSignal.Tags.GetValueOrDefault(Clear) != 1) return new(false, "接收岗位尚未亲见同号转清。");
                var receiver = _holder;
                var confirm = Apply(actor, operation, _ => Sequence(new Effect.Require(new Condition.Tagged(See(actor, "confirmation"), Clear, 1)), Set(actor, "confirmation", Confirmed, 1), Set(actor, "completion_record", Clear, 1), Set(actor, "completion_record", Confirmed, 1)));
                if (confirm.Ok)
                {
                    See(actor, "confirmation");
                    DeliverSignal(actor, receiver, Confirmed, actor + "亲见同号转清并具名确认");
                    if (Value(receiver, Released) == 1)
                    {
                        ReceiveStatement(actor, receiver, "completion:" + receiver, "本人收到具名确认，已完成持柄并停流。");
                        var receipt = Apply(actor, operation, _ => Set(actor, "completion_record", Released, 1), "接收岗位据具名停流回报填写收班副联");
                        if (!receipt.Ok) return receipt;
                    }
                }
                return confirm;
            case RedTokenOperation.Release:
                if (_holding.GetValueOrDefault(actor)?.Status == ExecutionStatus.Completed) return new(true, "本人持柄动作已经完成。");
                if (_holder != actor || !_holding.TryGetValue(actor, out var hold)) return new(false, "只有持柄者可以放手。");
                Runtime.Cancel(hold); _holder = null; RefreshLocalObservation(actor); Events.Add(actor + ":取消持柄，安全停流"); Snapshot(actor, "取消持柄安全停流，不记正常完成"); return new(true, "已取消持柄并安全停流。");
            case RedTokenOperation.Wipe:
            case RedTokenOperation.Wash:
                if (Flowing) return new(false, "持续流动时不能取下岗位布。");
                return Apply(actor, operation, _ => Sequence(Take(actor, "cloth"), new Effect.Require(new Condition.At(See(actor, "cloth"), See(actor, actor))), Set(actor, "cloth", operation == RedTokenOperation.Wipe ? Wiped : Washed, 1), operation == RedTokenOperation.Wash ? Set(actor, "water", Basin, 1) : new Effect.Require(new Condition.Constant(Truth.True))));
            case RedTokenOperation.DirtyWedge: return Apply(actor, operation, _ => Sequence(new Effect.Require(new Condition.All([new Condition.Not(new Condition.Tagged(See(actor, "door"), Closed, 1)), new Condition.Not(new Condition.Tagged(See(actor, "wedge"), Wrapped, 1))])), Set(actor, "wedge", Dirty, 1)));
            case RedTokenOperation.ReleaseWedge: return Apply(actor, operation, _ => new Effect.Act(ActionOp.Place, See(actor, "wedge"), See(actor, "D")));
            case RedTokenOperation.TakeWedge: return Apply(actor, operation, _ => Take(actor, "wedge"));
            case RedTokenOperation.WrapWedge:
                var wedgeOwner = Objects["wedge"].Get<SpatialProperty>()!.Holder;
                if (wedgeOwner is not null && !ReferenceEquals(wedgeOwner, Objects[actor])) return new(false, "同一楔仍在另一人手中。");
                if (wedgeOwner is null)
                {
                    var approachWedge = Travel(actor, Location("wedge")); if (!approachWedge.Ok) return approachWedge;
                    var getWedge = Apply(actor, operation, _ => Take(actor, "wedge")); if (!getWedge.Ok) return getWedge;
                }
                var approachPaper = Travel(actor, Location("paper")); if (!approachPaper.Ok) return approachPaper;
                return Apply(actor, operation, _ => Sequence(new Effect.Require(new Condition.At(See(actor, "wedge"), See(actor, actor))), Set(actor, "wedge", Wrapped, 1), Set(actor, "paper", Dirty, Value("wedge", Dirty)), new Effect.Act(ActionOp.Place, See(actor, "wedge"), See(actor, "paper"))));
            case RedTokenOperation.KnockGap: return Apply(actor, operation, _ => Set(actor, "door", Sound, Value("door", Closed) == 1 ? 0 : 1));
            case RedTokenOperation.KnockPipe: return Apply(actor, operation, _ => Set(actor, "pipe", Sound, 1));
            case RedTokenOperation.CloseDoor:
                if (ReferenceEquals(Objects["wedge"].Get<SpatialProperty>()!.Space, Objects["door"])) return new(false, "门底楔仍支撑门扇，须先撤楔。");
                return Apply(actor, operation, _ => Set(actor, "door", Closed, 1));
            case RedTokenOperation.DepositRed: return Deposit(actor, "red", operation);
            case RedTokenOperation.DepositOther: return Deposit(actor, "handle", operation);
            case RedTokenOperation.PushRed: return Push(actor, "red", operation);
            case RedTokenOperation.PushOther: return Push(actor, "handle", operation);
            case RedTokenOperation.TakeWhite: return Apply(actor, operation, _ => Sequence(new Effect.Require(new Condition.All([ReceiptsReady(actor), new Condition.Not(new Condition.Tagged(See(actor, "white"), Taken, 1))])), Take(actor, "white"), Set(actor, "white", Taken, 1), Set(actor, "receiver", Taken, 1)));
            case RedTokenOperation.ReturnRoom:
                var signKey = "sign:" + actor;
                if (!Objects.ContainsKey(signKey)) return new(false, "没有本人签条。");
                var toReceiver = Travel(actor, "B"); if (!toReceiver.Ok) return toReceiver;
                var whiteReceipt = See(actor, "receiver");
                var toSigns = Travel(actor, "A"); if (!toSigns.Ok) return toSigns;
                var signed = Apply(actor, operation, _ => Sequence(new Effect.Require(new Condition.All([new Condition.Tagged(whiteReceipt, Taken, 1), new Condition.Not(new Condition.Tagged(See(actor, signKey), Taken, 1))])), Set(actor, signKey, Taken, 1), new Effect.Change(See(actor, "room_sign"), Taken, 1)));
                return signed.Ok ? Travel(actor, "Dorm") : signed;
            case RedTokenOperation.GoRoomWithoutSign: return Travel(actor, "Dorm");
            case RedTokenOperation.CarryRedWithoutReturn: return Apply(actor, operation, _ => Sequence(Take(actor, "red"), new Effect.Act(ActionOp.Place, See(actor, "red"), See(actor, "chair")), Set(actor, "chair", Taken, 1)));
            case RedTokenOperation.MisrecordClear: return Apply(actor, operation, _ => Sequence(new Effect.Require(new Condition.All([new Condition.Tagged(See(actor, "water"), Basin, 1), new Condition.Tagged(See(actor, "cloth"), Washed, 1)])), Set(actor, "confirmation", Clear, 1), Set(actor, "confirmation", Confirmed, 1)));
            case RedTokenOperation.OpenDrawer: return Apply(actor, operation, _ => Sequence(Set(actor, "receiver", Broken, 1), Set(actor, "seal", Broken, 1), Set(actor, "drawer", Closed, 0)));
            case RedTokenOperation.MoveRedToChair: return Apply(actor, operation, _ => Sequence(new Effect.Require(new Condition.All([new Condition.Tagged(See(actor, "receipt_red"), Received, 1), new Condition.Tagged(See(actor, "seal"), Broken, 1), new Condition.At(See(actor, "red"), See(actor, "drawer"))])), Take(actor, "red"), new Effect.Act(ActionOp.Place, See(actor, "red"), See(actor, "chair")), Set(actor, "chair", Taken, 1)));
            case RedTokenOperation.OpenCopper:
                var opened = Apply(actor, operation, _ => Set(actor, "pipe", Taken, 1));
                if (opened.Ok)
                {
                    // 铜口响应质询交付原物；公开后才可在台侧观察该纸带。
                    PlaceObject("movement_record", "B");
                    World.Sync();
                }
                return opened;
            default: return new(false, "无法识别现场动作。");
        }
    }
    private Condition ReceiptsReady(string actor) => new Condition.All([new Condition.Tagged(See(actor, "receipt_red"), Received, 1), new Condition.Tagged(See(actor, "receipt_handle"), Received, 1), new Condition.Tagged(See(actor, "receipt_red"), Pushed, 1), new Condition.Tagged(See(actor, "receipt_handle"), Pushed, 1)]);
    private RedTokenResult Deposit(string actor, string token, RedTokenOperation operation)
    {
        if (Value("receipt_" + token, Received) == 1) return new(false, "同一物已落入单向匣，不能重复交回。");
        if (token == "handle" && Flowing) return new(false, "必须先放手，才能交回手柄。");
        var holder = Objects[token].Get<SpatialProperty>()!.Holder;
        if (holder is not null && !ReferenceEquals(holder, Objects[actor])) return new(false, "该物仍在另一人的手里。");
        if (holder is null)
        {
            var retrieve = Travel(actor, Location(token)); if (!retrieve.Ok) return retrieve;
            var take = Apply(actor, operation, _ => Take(actor, token)); if (!take.Ok) return take;
        }
        var travel = Travel(actor, "B"); if (!travel.Ok) return travel;
        return Apply(actor, operation, _ => Sequence(new Effect.Require(new Condition.Not(new Condition.Tagged(See(actor, "receipt_" + token), Received, 1))), new Effect.Act(ActionOp.Place, See(actor, token), See(actor, "drawer")), Set(actor, token, Received, 1), Set(actor, "receipt_" + token, Received, 1)));
    }
    private string Location(string obj)
    {
        var visited = new HashSet<Essence>();
        for (var current = Objects[obj]; current is not null && visited.Add(current); current = current.Get<SpatialProperty>()?.Holder ?? current.Get<SpatialProperty>()?.Space)
            if (current.Kind == ProjectionKind.Space) return current.TrueName;
        return "room";
    }

    private RedTokenResult Push(string actor, string token, RedTokenOperation operation)
    {
        var result = Apply(actor, operation, _ => Sequence(new Effect.Require(new Condition.All([new Condition.Tagged(See(actor, "receipt_" + token), Received, 1), new Condition.Not(new Condition.Tagged(See(actor, "receipt_" + token), Pushed, 1)), new Condition.At(See(actor, token), See(actor, "drawer"))])), Set(actor, "receipt_" + token, Pushed, 1), Set(actor, token, Pushed, 1)));
        if (result.Ok && WhiteAvailable) return Apply(actor, operation, _ => Sequence(new Effect.Require(ReceiptsReady(actor)), Set(actor, "white", Received, 1)));
        return result;
    }
    private void Snapshot(string actor, string caption)
    {
        string Place(string obj) => Objects[obj].Get<SpatialProperty>() is { } spatial ? spatial.Holder?.TrueName ?? spatial.Space?.TrueName ?? "未知" : "未知";
        var positions = new Dictionary<string, string>();
        foreach (var name in new[] { "侦探", "工程师霍铎", "主厨沈鼎", "音乐家苏泠" }) positions[name] = Objects[name].Get<SpatialProperty>()!.Space?.TrueName switch { "B" => "E03", "C" => "E09", "D" => "E06", "A" => "E14", "Dorm" => "E14", "R05" => "E07", _ => "E01" };
        Snapshots.Add(new RedTokenFrame(actor, caption, positions, Flowing, HistoricalGray, HistoricalGray, Place("red"), Place("handle"), Place("cloth"), Value("door", Closed) == 1, Value("cloth", Wiped) == 1));
    }
    public void Dispose() => Runtime.Dispose();
}

public sealed record RedTokenFrame(string Actor, string Caption, IReadOnlyDictionary<string, string> Positions, bool Flowing, bool Gray, bool Captured, string TokenLocation, string HandleLocation, string ClothLocation, bool Closed, bool Wiped);


