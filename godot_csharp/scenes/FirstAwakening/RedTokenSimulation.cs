using DualOntologyWorldEngine.Core;
using System.Collections.Immutable;
namespace DualOntologyWorldEngine.Game.FirstAwakening;

/// <summary>每次候选使用全新现实、独立主体、独立器具引用；现场证据不进入此世界。</summary>
public sealed class RedTokenSimulation : IDisposable
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
    private readonly Essence _room;
    private string? _holder;
    public string? Holder => _holder;
    public bool Flowing => _holder is { } h && Value(h, Holding) == 1;
    public bool Waiting => Flowing && Value("confirmation", Confirmed) != 1;
    public bool HistoricalGray => Value("upstream", Ash) == 1;
    public bool Complete => HistoricalGray && Value("confirmation", Clear) == 1 && !Flowing;
    public bool WhiteAvailable => Value("red", Received) == 1 && Value("handle", Received) == 1 && Value("red", Pushed) == 1 && Value("handle", Pushed) == 1;

    public RedTokenSimulation()
    {
        Runtime = new(World);
        _room = Make("room", ProjectionKind.Space);
        foreach (var actor in new[] { "侦探", "工程师霍铎", "主厨沈鼎", "音乐家苏泠", "值守者", "未知后取者", "接待者" })
        {
            var soul = new SoulEssence(actor); Souls.Add(actor, soul); World.Project(soul, actor + "心灵");
            var body = Make(actor, ProjectionKind.Body); soul.Relations.Add((Relations.Controls, body));
        }
        foreach (var obj in new[] { "handle", "red", "cloth", "wedge", "paper", "receiver", "white", "confirmation", "exception", "door", "pipe", "water", "chair", "room_sign", "upstream", "clip", "rack" }) Make(obj);
        foreach (var actor in new[] { "侦探", "工程师霍铎", "主厨沈鼎", "音乐家苏泠" }) Make("reward:" + actor);
        foreach (var space in new[] { "A", "B", "C", "D", "R05", "Dorm" }) Make(space, ProjectionKind.Space);
        foreach (var from in new[] { "room", "A", "B", "C", "D", "R05", "Dorm" })
        foreach (var to in new[] { "room", "A", "B", "C", "D", "R05", "Dorm" }.Where(to => to != from))
        {
            var route = Make("route:" + from + ":" + to); route.Get<SpatialProperty>()!.Space = Objects[from];
            route.Add(new PassageProperty()).Connections[Objects[from]] = Objects[to];
        }
        foreach (var obj in new[] { "handle", "rack", "exception", "pipe" }) Objects[obj].Get<SpatialProperty>()!.Space = Objects["B"];
        foreach (var obj in new[] { "cloth", "clip", "upstream", "confirmation", "water" }) Objects[obj].Get<SpatialProperty>()!.Space = Objects["C"];
        foreach (var obj in new[] { "wedge", "paper", "door" }) Objects[obj].Get<SpatialProperty>()!.Space = Objects["D"];
        foreach (var obj in new[] { "red", "receiver", "white", "chair", "room_sign" }) Objects[obj].Get<SpatialProperty>()!.Space = Objects["A"];
        Objects["chair"].Get<SpatialProperty>()!.Space = Objects["B"];
        Objects["wedge"].Get<StateProperty>()!.Values[Narrow] = 1;
        Objects["handle"].Get<SpatialProperty>()!.Space = Objects["rack"];
        // 初始岗位记忆来自各自实际看到的身体与器具，不共享整场认知。
        foreach (var actor in Souls.Keys) See(actor, actor);
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
        if (actorSpace != targetSpace && !(actorSpace is "B" or "C" && targetSpace is "B" or "C") && obj != actor)
            throw new InvalidOperationException("此岗位看不到该物，须先近身观察。");
        World.Sync();
        var sources = new List<Projection>();
        if (_views.TryGetValue(targetSpace, out var space) && obj != targetSpace) sources.Add(space);
        sources.Add(_views[obj]);
        var p = World.Observe(Souls[actor], sources, Runtime.CTB.Now)[^1]; _mental[(actor, obj)] = p; return p;
    }
    private static string Chinese(string label) => label switch
    {
        "confirm_filter" => "主厨确认夹滤到位", "clip_filter" or "assign_tool" => "夹牢滤布并确认捕集岗位", "capture" => "灰流先经过上游当班捕集片，再落入盆中", "inspect_clear" => "主厨亲见同号转清", "confirm" => "主厨向工程师发出具名转清确认", "wipe" => "取下布擦地，留下地板纤维", "wash" => "厨房清水洗布，盆内留灰", "dirty_wedge" => "门底楔在烟灰路中受灰", "take_wedge" => "撤取门底楔", "wrap_wedge" => "包裹同一沾灰楔，纸内形成转印", "close_door" => "撤楔后配重合门", "take_white" => "双槽交回后取得白片", "misrecord_clear" => "把洗布后的清水误记为转清确认", "open_drawer" => "打开前抽屉，原封条断开", "move_red_to_chair" => "从前抽屉后取同一红筹放到椅上", "open_copper" => "现实铜口质询索取院务移物单", _ => label
    };
    private RedTokenResult Apply(string actor, string label, Func<Projection, Effect> effect, Condition? requirement = null)
    {
        if (!Souls.ContainsKey(actor)) return new(false, "没有该行动者。");
        if (_holding.GetValueOrDefault(actor)?.Status == ExecutionStatus.Pending) return new(false, "该身体仍在持续持柄等待，不能换岗。");
        var body = See(actor, actor);
        try
        {
            var execution = Runtime.Submit(Souls[actor], new ActionIntent(body, effect(body), 0, requirement));
            Runtime.CTB.RunUntil(Runtime.CTB.Now);
            if (execution.Status == ExecutionStatus.Failed) { var failure = label + ":" + execution.Result!.Failure; Failures.Add(failure); return new(false, failure); }
            Events.Add(actor + ":" + Chinese(label)); Snapshot(actor, Chinese(label)); return new(true, Chinese(label));
        }
        catch (InvalidOperationException e) { Failures.Add(label + ":" + e.Message); return new(false, e.Message); }
    }
    private Effect Set(string actor, string obj, Tag tag, int value) => new Effect.Set(See(actor, obj), tag, value);
    private Effect Sequence(params Effect[] effects) => new Effect.Sequence(effects.ToImmutableArray());
    private RedTokenResult Travel(string actor, string destination)
    {
        var from = Objects[actor].Get<SpatialProperty>()!.Space?.TrueName ?? "room";
        if (from == destination) return new(true, "已在岗位。");
        var route = "route:" + from + ":" + destination;
        return Apply(actor, "前往" + destination + "岗位", _ => new Effect.Act(ActionOp.Travel, See(actor, route)));
    }
    public RedTokenResult Perform(string actor, string action, string station = "")
    {
        if (!Souls.ContainsKey(actor)) return new(false, "没有该行动者。");
        var intended = action switch
        {
            "read_exception" or "take_handle" or "hold" or "safe_press" or "knock_pipe" => "B",
            "assign_tool" or "clip_filter" or "release_tool" or "confirm_filter" or "capture" or "water_flow" or "inspect_clear" or "confirm" or "wipe" or "wash" or "misrecord_clear" => "C",
            "take_wedge" or "release_wedge" or "wrap_wedge" or "dirty_wedge" or "bolt" or "close_door" or "knock_gap" => "D",
            "deposit_red" or "deposit_other" or "push_red" or "push_other" or "take_white" or "return_room" or "go_room_without_sign" or "carry_red_without_return" or "open_drawer" or "move_red_to_chair" => "A",
            _ => Objects[actor].Get<SpatialProperty>()!.Space?.TrueName ?? "room"
        };
        if (!string.IsNullOrWhiteSpace(station) && new[] { "A", "B", "C", "D" }.Contains(station) && station != intended) return new(false, "该操作需要在" + intended + "岗位完成。");
        if (action == "verify_reward") intended = "room";
        if (action != "release") { var travel = Travel(actor, intended); if (!travel.Ok) return travel; }
        switch (action)
        {
            case "verify_reward": return Apply(actor, "核验本人约定报偿", _ => Sequence(new Effect.Act(ActionOp.Inspect, See(actor, "reward:" + actor)), Set(actor, "reward:" + actor, Read, 1)));
            case "read_exception":
                var read = Apply(actor, "读取并接受排灰特例", _ => Sequence(Set(actor, "exception", Read, 1), Set(actor, actor, Read, 1)));
                if (read.Ok)
                {
                    var self = See(actor, actor); var signalRule = See(actor, "confirmation");
                    Observation.Observe(Souls[actor], new ObservationInput("排灰特例", ProjectionKind.Procedure) { Rules = [new Rule(new Condition.Tagged(self, Holding, 1), RuleMode.Require, new Effect.Wait(new Condition.Tagged(signalRule, Confirmed, 1), new Effect.Require(new Condition.Constant(Truth.True))), Acceptance: Truth.True)] });
                }
                return read;
            case "assign_tool":
            case "clip_filter":
                if (Objects["cloth"].Get<SpatialProperty>()!.Holder is { } owner && !ReferenceEquals(owner, Objects[actor])) return new(false, "同一块布已由另一个岗位持有。");
                return Apply(actor, action, _ => Sequence(new Effect.Act(ActionOp.Take, See(actor, "cloth")), new Effect.Act(ActionOp.Place, See(actor, "cloth"), See(actor, "clip")), Set(actor, "cloth", Filter, 1), Set(actor, "confirmation", Clear, 0), Set(actor, "confirmation", Confirmed, 0)));
            case "release_tool": return Apply(actor, action, _ => new Effect.Act(ActionOp.Place, See(actor, "cloth"), See(actor, "room")));
            case "take_handle": return Apply(actor, "在器材架接掌唯一手柄", _ => new Effect.Act(ActionOp.Take, See(actor, "handle")));
            case "confirm_filter":
                if (actor != "主厨沈鼎" || See(actor, "cloth").Tags.GetValueOrDefault(Filter) != 1) return new(false, "主厨尚未现场看见布夹到位。");
                var filtered = Apply(actor, "主厨具名确认夹滤到位", _ => Set(actor, "confirmation", FilterReady, 1));
                if (filtered.Ok && Location("工程师霍铎") == "B") See("工程师霍铎", "confirmation");
                return filtered;
            case "bolt": return Apply(actor, "门侧插入内销", _ => Set(actor, "door", Bolt, 1));
            case "safe_press":
                if (Flowing) return new(false, "安全试柄已按住。");
                var safeBody = See(actor, actor);
                var safeExecution = Runtime.Submit(Souls[actor], new ActionIntent(safeBody, Sequence(new Effect.TemporarySetUntilEnd(safeBody, Holding, 1), new Effect.Wait(new Condition.Constant(Truth.False), new Effect.Require(new Condition.Constant(Truth.True)))), 0));
                _holder = actor; _holding[actor] = safeExecution; Runtime.CTB.RunUntil(Runtime.CTB.Now); Snapshot(actor, "按住独立空载试柄，弹簧安全回路保持");
                return new(true, "空载试柄按住；放手后弹簧立即停流。");
            case "hold":
                if (Flowing || Value("handle", Received) == 1) return new(false, "手柄已在使用或已单向交回。");
                if (!ReferenceEquals(Objects["handle"].Get<SpatialProperty>()!.Holder, Objects[actor])) return new(false, "先在器材架接掌手柄，不能凭空占用器具。");
                var signal = See(actor, "confirmation"); var body = See(actor, actor); var handle = See(actor, "handle");
                // 架上柄先取到本人；临时覆盖保持到条件等待结束或玩家取消。
                Effect fullHold = Sequence(new Effect.Require(new Condition.All([new Condition.Tagged(signal, FilterReady, 1), new Condition.Tagged(body, Read, 1), new Condition.At(handle, body)])), new Effect.Set(body, Accepted, 1), new Effect.TemporarySetUntilEnd(body, Holding, 1),
                        new Effect.Wait(new Condition.Tagged(signal, Confirmed, 1), new Effect.Wait(new Condition.Tagged(signal, Released, 1), new Effect.Require(new Condition.Constant(Truth.True)))));
                Effect daily = Sequence(new Effect.Set(body, Accepted, 1), new Effect.Act(ActionOp.Place, handle, See(actor, "rack")));
                // 日常规则与排灰特例分别保存在本人内部世界，只有接受过的规则参与决策。
                var knowsException = body.Tags.GetValueOrDefault(Read) == 1;
                Observation.Observe(Souls[actor], new ObservationInput("当前岗位工作规则", ProjectionKind.Procedure) { Rules =
                    [new Rule(new Condition.Constant(Truth.True), RuleMode.Prefer, daily, 1, Truth.True),
                     new Rule(new Condition.Constant(Truth.True), RuleMode.Prefer, fullHold, 10, knowsException ? Truth.True : Truth.Unknown)] });
                var decision = DecisionSystem.Choose(Souls[actor].World, body)!;
                var execution = Runtime.Submit(Souls[actor], new ActionIntent(body, decision.Effect, 0, Basis: decision.Basis));
                _holding[actor] = execution; _holder = actor; Runtime.CTB.RunUntil(Runtime.CTB.Now);
                if (execution.Status == ExecutionStatus.Completed) { _holder = null; Events.Add(actor + ":未接受排灰特例，日常复位并未持续分流"); Snapshot(actor, "日常规则未产生持续持柄"); return new(true, "日常检查后复位。"); }
                if (execution.Status == ExecutionStatus.Failed) { _holder = null; return new(false, "取柄或持柄动作失败。"); }
                Events.Add(actor + ":持续持柄，等待具名转清确认"); Snapshot(actor, "持续持柄等待主厨具名确认"); return new(true, "持续持柄等待已提交。");
            case "capture":
                if (!Flowing || _holder == actor || !ReferenceEquals(Objects["cloth"].Get<SpatialProperty>()!.Space, Objects["clip"]) || Value("cloth", Filter) != 1) return new(false, "须另人持续持柄，布在本岗位先夹滤。");
                return Apply(actor, action, _ => Sequence(Set(actor, "cloth", Ash, 1), Set(actor, "upstream", Ash, 1), Set(actor, "water", Basin, 1)));
            case "water_flow":
                if (!Flowing) return new(false, "空载弹簧使未持柄水路停止。");
                return Apply(actor, action, _ => Set(actor, "water", Wiped, 1));
            case "inspect_clear":
                if (actor != "主厨沈鼎" || !Flowing || !HistoricalGray) return new(false, "主厨须在捕灰后现场观察同号转清。");
                var result = Apply(actor, action, _ => Set(actor, "confirmation", Clear, 1));
                See(actor, "confirmation"); return result;
            case "confirm":
                if (actor != "主厨沈鼎" || _holder is null || !_mental.TryGetValue((actor, "confirmation"), out var chefSignal) || chefSignal.Tags.GetValueOrDefault(Clear) != 1) return new(false, "主厨尚未亲见同号转清。");
                var receiver = _holder;
                var confirm = Apply(actor, action, _ => Set(actor, "confirmation", Confirmed, 1));
                // 工程师收到具名确认，才更新其自身世界并唤醒等待；两位主体的记忆互不别名。
                See(receiver, "confirmation"); Runtime.CTB.RunUntil(Runtime.CTB.Now);
                Events.Add("主厨沈鼎向" + receiver + "确认同号转清"); return confirm;
            case "release":
                if (_holder != actor) return new(false, "只有持柄者可以放手。");
                if (_holding.TryGetValue(actor, out var hold)) Runtime.Cancel(hold);
                _holder = null; Events.Add(actor + ":放手停流，历史灰痕保留"); Snapshot(actor, "放手停流，历史灰痕保留"); return new(true, "已放手停流。");
            case "wipe":
                if (Flowing) return new(false, "布仍在捕集岗位，持续流动时不能取下擦地。");
                return Apply(actor, action, _ => Sequence(new Effect.Act(ActionOp.Take, See(actor, "cloth")), Set(actor, "cloth", Wiped, 1)));
            case "wash": return Apply(actor, action, _ => Sequence(Set(actor, "cloth", Washed, 1), Set(actor, "water", Basin, 1)));
            case "dirty_wedge": return Apply(actor, action, _ => Set(actor, "wedge", Dirty, 1));
            case "release_wedge": return Apply(actor, "放下楔供撤收", _ => new Effect.Act(ActionOp.Place, See(actor, "wedge"), See(actor, "D")));
            case "take_wedge": return Apply(actor, action, _ => new Effect.Act(ActionOp.Take, See(actor, "wedge")));
            case "wrap_wedge": return Apply(actor, action, _ => Sequence(new Effect.Act(ActionOp.Take, See(actor, "wedge")), Set(actor, "wedge", Wrapped, 1), Set(actor, "paper", Dirty, Value("wedge", Dirty))));
            case "knock_gap": return Apply(actor, "门侧安全敲击试件", _ => Set(actor, "door", Sound, Value("door", Closed) == 1 ? 0 : 1));
            case "knock_pipe": return Apply(actor, "台侧压力铜管安全敲击", _ => Set(actor, "pipe", Sound, 1));
            case "close_door": return Apply(actor, action, _ => Set(actor, "door", Closed, 1));
            case "deposit_red": return Deposit(actor, "red");
            case "deposit_other": return Deposit(actor, "handle");
            case "push_red": return Push(actor, "red");
            case "push_other": return Push(actor, "handle");
            case "take_white":
                if (!WhiteAvailable) return new(false, "柄筹必须均已单向交回且对应接收件到位。");
                return Apply(actor, action, _ => Sequence(new Effect.Act(ActionOp.Take, See(actor, "white")), Set(actor, "white", Taken, 1)));
            case "return_room":
                if (Value("white", Taken) != 1) return new(false, "须先收白片并清点签条，才记录交接后返房。");
                var signed = Apply(actor, "本人清点签条记录交接后返房", _ => new Effect.Change(See(actor, "room_sign"), Taken, 1));
                return signed.Ok ? Travel(actor, "Dorm") : signed;
            case "go_room_without_sign": return Travel(actor, "Dorm");
            case "carry_red_without_return":
                var carried = Apply(actor, "红筹未归还便带回", _ => new Effect.Act(ActionOp.Take, See(actor, "red"))); if (!carried.Ok) return carried;
                var carriedRoom = Travel(actor, "B"); if (!carriedRoom.Ok) return carriedRoom;
                return Apply(actor, "未归筹直接置椅", _ => Sequence(new Effect.Act(ActionOp.Place, See(actor, "red"), See(actor, "chair")), Set(actor, "chair", Taken, 1)));
            case "misrecord_clear": return Apply(actor, action, _ => Sequence(Set(actor, "confirmation", Clear, 1), Set(actor, "confirmation", Confirmed, 1)));
            case "open_drawer": return Apply(actor, action, _ => Set(actor, "receiver", Broken, 1));
            case "move_red_to_chair":
                if (Value("red", Received) != 1 || Value("receiver", Broken) != 1) return new(false, "后取需已交回并实际开前屉。");
                var retrieved = Apply(actor, "从断封前屉取出同一红筹", _ => new Effect.Act(ActionOp.Take, See(actor, "red"))); if (!retrieved.Ok) return retrieved;
                var toChair = Travel(actor, "B"); if (!toChair.Ok) return toChair;
                return Apply(actor, action, _ => Sequence(new Effect.Act(ActionOp.Place, See(actor, "red"), See(actor, "chair")), Set(actor, "chair", Taken, 1)));
            case "open_copper":
                var approach = Travel(actor, "B"); if (!approach.Ok) return approach;
                return Apply(actor, action, _ => Set(actor, "pipe", Taken, 1));
            default: return new(false, "无法识别现场动作。");
        }
    }
    private RedTokenResult Deposit(string actor, string token)
    {
        if (Value(token, Received) == 1) return new(false, "同一物已落入单向匣，不能重复交回或取回。");
        if (token == "handle" && Flowing) return new(false, "必须先放手，才能交回手柄。");
        var location = Location(token);
        var holder = Objects[token].Get<SpatialProperty>()!.Holder;
        if (holder is not null && !ReferenceEquals(holder, Objects[actor])) return new(false, "该物仍在另一人的手里，不能跨岗取用。");
        if (holder is null)
        {
            var retrieve = Travel(actor, location); if (!retrieve.Ok) return retrieve;
            var take = Apply(actor, "从岗位取回" + token, _ => new Effect.Act(ActionOp.Take, See(actor, token))); if (!take.Ok) return take;
        }
        var travel = Travel(actor, "A"); if (!travel.Ok) return travel;
        return Apply(actor, "交回" + token, _ => Sequence(new Effect.Act(ActionOp.Place, See(actor, token), See(actor, "receiver")), Set(actor, token, Received, 1)));
    }
    private string Location(string obj)
    {
        for (var current = Objects[obj]; current is not null; current = current.Get<SpatialProperty>()?.Holder ?? current.Get<SpatialProperty>()?.Space)
            if (current.Kind == ProjectionKind.Space) return current.TrueName;
        return "room";
    }

    private RedTokenResult Push(string actor, string token)
    {
        if (Value(token, Received) != 1) return new(false, "先单向落入再推动该物的接收件。");
        var result = Apply(actor, "推动接收件" + token, _ => Set(actor, token, Pushed, 1));
        if (result.Ok && Value("red", Received) == 1 && Value("handle", Received) == 1 && Value("red", Pushed) == 1 && Value("handle", Pushed) == 1)
            return Apply(actor, "双槽接收机构推出白片", _ => Set(actor, "white", Received, 1));
        return result;
    }
    private void Snapshot(string actor, string caption)
    {
        string Place(string obj) => Objects[obj].Get<SpatialProperty>() is { } spatial ? spatial.Holder?.TrueName ?? spatial.Space?.TrueName ?? "未知" : "未知";
        var positions = new Dictionary<string, string>();
        foreach (var name in new[] { "侦探", "工程师霍铎", "主厨沈鼎", "音乐家苏泠" }) positions[name] = Objects[name].Get<SpatialProperty>()!.Space?.TrueName switch { "B" => "E03", "C" => "E09", "D" => "E06", "A" => "E13", "Dorm" => "E14", "R05" => "E05", _ => "E01" };
        Snapshots.Add(new RedTokenFrame(actor, caption, positions, Flowing, HistoricalGray, HistoricalGray, Place("red"), Place("handle"), Place("cloth"), Value("door", Closed) == 1, Value("cloth", Wiped) == 1));
    }
    public void Dispose() => Runtime.Dispose();
}

public sealed record RedTokenFrame(string Actor, string Caption, IReadOnlyDictionary<string, string> Positions, bool Flowing, bool Gray, bool Captured, string TokenLocation, string HandleLocation, string ClothLocation, bool Closed, bool Wiped);


