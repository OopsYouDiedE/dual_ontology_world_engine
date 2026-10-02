using DualOntologyWorldEngine.Core;
using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Game.FirstAwakening;

public enum RedTokenWorldKind { History, Scene, SafeTrial }

public enum RedTokenOperation
{
    VerifyReward, ReadException, AssignTool, ClipFilter, ReleaseTool, TakeHandle,
    ConfirmFilter, Bolt, SafePress, Hold, Capture, WaterFlow, InspectClear, Confirm,
    Release, Wipe, Wash, DirtyWedge, ReleaseWedge, TakeWedge, WrapWedge, KnockGap,
    KnockPipe, CloseDoor, DepositRed, DepositOther, PushRed, PushOther, TakeWhite,
    ReturnRoom, GoRoomWithoutSign, CarryRedWithoutReturn, MisrecordClear,
    OpenDrawer, MoveRedToChair, OpenCopper, Accompany, RecallMaterial
}

/// <summary>宿主物件的身份描述与可操作能力；所有状态仍在同一实体的 Property 中。</summary>
public sealed class RedTokenObjectProperty : Property
{
    public string DisplayName { get; init; } = "物件";
    public string UnrecognizedName { get; init; } = "未辨认物件";
    public bool Portable { get; init; }
    public ImmutableHashSet<RedTokenOperation> Operations { get; init; } = [];
}

public sealed partial class RedTokenSimulation
{
    public static readonly Tag Portable = new("Portable");
    public IReadOnlyDictionary<string, IReadOnlyList<Essence>> EvidenceObjects { get; private set; }
        = ImmutableDictionary<string, IReadOnlyList<Essence>>.Empty;

    private static readonly ImmutableDictionary<string, RedTokenOperation> OperationNames =
        new Dictionary<string, RedTokenOperation>(StringComparer.Ordinal)
        {
            ["verify_reward"] = RedTokenOperation.VerifyReward,
            ["read_exception"] = RedTokenOperation.ReadException,
            ["assign_tool"] = RedTokenOperation.AssignTool,
            ["clip_filter"] = RedTokenOperation.ClipFilter,
            ["release_tool"] = RedTokenOperation.ReleaseTool,
            ["take_handle"] = RedTokenOperation.TakeHandle,
            ["confirm_filter"] = RedTokenOperation.ConfirmFilter,
            ["bolt"] = RedTokenOperation.Bolt,
            ["safe_press"] = RedTokenOperation.SafePress,
            ["hold"] = RedTokenOperation.Hold,
            ["capture"] = RedTokenOperation.Capture,
            ["water_flow"] = RedTokenOperation.WaterFlow,
            ["inspect_clear"] = RedTokenOperation.InspectClear,
            ["confirm"] = RedTokenOperation.Confirm,
            ["release"] = RedTokenOperation.Release,
            ["wipe"] = RedTokenOperation.Wipe,
            ["wash"] = RedTokenOperation.Wash,
            ["dirty_wedge"] = RedTokenOperation.DirtyWedge,
            ["release_wedge"] = RedTokenOperation.ReleaseWedge,
            ["take_wedge"] = RedTokenOperation.TakeWedge,
            ["wrap_wedge"] = RedTokenOperation.WrapWedge,
            ["knock_gap"] = RedTokenOperation.KnockGap,
            ["knock_pipe"] = RedTokenOperation.KnockPipe,
            ["close_door"] = RedTokenOperation.CloseDoor,
            ["deposit_red"] = RedTokenOperation.DepositRed,
            ["deposit_other"] = RedTokenOperation.DepositOther,
            ["push_red"] = RedTokenOperation.PushRed,
            ["push_other"] = RedTokenOperation.PushOther,
            ["take_white"] = RedTokenOperation.TakeWhite,
            ["return_room"] = RedTokenOperation.ReturnRoom,
            ["go_room_without_sign"] = RedTokenOperation.GoRoomWithoutSign,
            ["carry_red_without_return"] = RedTokenOperation.CarryRedWithoutReturn,
            ["misrecord_clear"] = RedTokenOperation.MisrecordClear,
            ["open_drawer"] = RedTokenOperation.OpenDrawer,
            ["move_red_to_chair"] = RedTokenOperation.MoveRedToChair,
            ["open_copper"] = RedTokenOperation.OpenCopper,
            ["accompany"] = RedTokenOperation.Accompany,
            ["recall_material"] = RedTokenOperation.RecallMaterial
        }.ToImmutableDictionary(StringComparer.Ordinal);

    public static bool TryParseOperation(string value, out RedTokenOperation operation)
        => OperationNames.TryGetValue(value, out operation);

    public static string OperationKey(RedTokenOperation operation)
        => OperationNames.First(pair => pair.Value == operation).Key;

    private void InitializeObjects()
    {
        foreach (var name in new[] { "board", "study_table", "dorm_desk", "dorm_drawer", "letter_sheet", "torn_letter", "seal", "receipt_red", "receipt_handle", "completion_record", "signed_exception", "rack_record", "movement_record", "cabinet", "coat", "ration", "cup", "ticket", "wastebasket", "red_slot", "handle_slot", "drawer" }) Make(name);
        Make("dorm_door", ProjectionKind.Boundary);
        Make("window", ProjectionKind.Boundary);
        Make("daily_rules", ProjectionKind.Procedure);
        foreach (var actor in new[] { "侦探", "工程师霍铎", "主厨沈鼎", "音乐家苏泠" }) Make("sign:" + actor);

        // Make 的泛化可见状态在此收拢，每个实体只声明自己的可感知标签。
        foreach (var obj in Objects.Values)
        {
            obj.Get<StateProperty>()!.Observable.Clear();
            Describe(obj.TrueName, obj.TrueName, "未辨认物件", false);
        }

        Describe("handle", "分流手柄", "有握痕的金属器具", true,
            RedTokenOperation.TakeHandle, RedTokenOperation.Hold, RedTokenOperation.SafePress, RedTokenOperation.Release, RedTokenOperation.DepositOther);
        Describe("red", "红筹", "红色筹片", true, RedTokenOperation.DepositRed, RedTokenOperation.CarryRedWithoutReturn, RedTokenOperation.MoveRedToChair);
        Describe("white", "白片", "白色薄片", true, RedTokenOperation.TakeWhite);
        ObserveTags("white", Taken);
        Describe("cloth", "滤布", "折叠布料", true, RedTokenOperation.AssignTool, RedTokenOperation.ClipFilter, RedTokenOperation.ReleaseTool, RedTokenOperation.Wipe, RedTokenOperation.Wash, RedTokenOperation.Capture);
        ObserveTags("cloth", Filter, Ash, Wiped, Washed);
        Describe("wedge", "门底楔", "窄木块", true, RedTokenOperation.DirtyWedge, RedTokenOperation.TakeWedge, RedTokenOperation.ReleaseWedge, RedTokenOperation.WrapWedge);
        ObserveTags("wedge", Narrow, Dirty, Wrapped);
        Describe("paper", "包楔纸", "折皱纸张", true, RedTokenOperation.WrapWedge);
        ObserveTags("paper", Dirty);
        Describe("receiver", "单向归还匣", "双槽金属匣", false, RedTokenOperation.DepositRed, RedTokenOperation.DepositOther, RedTokenOperation.PushRed, RedTokenOperation.PushOther, RedTokenOperation.OpenDrawer);
        ObserveTags("receiver", Broken, Taken);
        Describe("red_slot", "筹接收件", "窄接收槽", false, RedTokenOperation.PushRed);
        Describe("handle_slot", "柄接收件", "长接收槽", false, RedTokenOperation.PushOther);
        Describe("drawer", "前抽屉", "封闭抽屉", false, RedTokenOperation.OpenDrawer, RedTokenOperation.MoveRedToChair);
        ObserveTags("drawer", Closed);
        Describe("seal", "抽屉封条", "贴附纸条", false);
        ObserveTags("seal", Broken);
        Describe("receipt_red", "筹槽接收印", "筹槽刻印", false);
        Describe("receipt_handle", "柄槽接收印", "柄槽刻印", false);
        ObserveTags("receipt_red", Received, Pushed);
        ObserveTags("receipt_handle", Received, Pushed);
        Describe("completion_record", "当班收班副联", "签记纸页", true);
        ObserveTags("completion_record", Clear, Confirmed, Released);
        Describe("signed_exception", "具名特例签收页", "折页", true, RedTokenOperation.ReadException);
        ObserveTags("signed_exception", Read);
        Describe("rack_record", "手柄接掌存根", "架边纸页", true);
        ObserveTags("rack_record", Accepted);
        Describe("movement_record", "院务移物单", "巡检纸带", true);
        ObserveTags("movement_record", Read);
        Describe("exception", "排灰特例", "板背规程", false, RedTokenOperation.ReadException);
        ObserveTags("exception", Read);
        Describe("daily_rules", "日常连页规程", "连页纸张", false);
        Describe("door", "配重门", "厚重门扇", false, RedTokenOperation.Bolt, RedTokenOperation.CloseDoor, RedTokenOperation.KnockGap);
        ObserveTags("door", Closed, Bolt, Sound);
        Describe("pipe", "压力铜管与铜口", "铜制管口", false, RedTokenOperation.KnockPipe, RedTokenOperation.OpenCopper);
        ObserveTags("pipe", Sound, Taken);
        Describe("water", "集水盆", "盆中水迹", false, RedTokenOperation.Capture, RedTokenOperation.WaterFlow, RedTokenOperation.Wash);
        ObserveTags("water", Basin, Wiped);
        Describe("upstream", "上游当班捕集片", "管内薄片", false);
        ObserveTags("upstream", Ash);
        Describe("clip", "滤布夹", "金属夹具", false, RedTokenOperation.ClipFilter);
        Describe("confirmation", "盆侧确认机构", "盆侧标记", false, RedTokenOperation.ConfirmFilter, RedTokenOperation.InspectClear, RedTokenOperation.Confirm, RedTokenOperation.MisrecordClear);
        ObserveTags("confirmation", FilterReady);
        if (Kind != RedTokenWorldKind.Scene) ObserveTags("confirmation", Clear, Confirmed, Released);
        Describe("rack", "器材架", "金属支架", false, RedTokenOperation.TakeHandle);
        Describe("board", "指令板", "悬挂木板", false);
        Describe("study_table", "案桌", "木案", false);
        ObserveTags("study_table", Dirty);
        Describe("chair", "长椅", "木椅", false, RedTokenOperation.MoveRedToChair);
        ObserveTags("chair", Taken);
        Describe("room_sign", "返房签条板", "签条板", false, RedTokenOperation.ReturnRoom, RedTokenOperation.GoRoomWithoutSign);
        ObserveTags("room_sign", Taken);
        Describe("cabinet", "报偿领取柜", "凭证柜", false, RedTokenOperation.VerifyReward);
        Describe("dorm_desk", "五号房书桌", "书桌", false);
        Describe("dorm_drawer", "五号房书桌抽屉", "案下抽屉", false);
        ObserveTags("dorm_drawer", Closed);
        Describe("letter_sheet", "抽屉内信纸", "纸张", true);
        Describe("torn_letter", "撕去半页的信纸", "残缺纸页", true);
        Describe("coat", "外衣", "衣架上的衣物", true);
        Describe("ration", "冷餐", "盘中食物", true);
        Describe("cup", "水杯", "杯子", true);
        Describe("ticket", "衣袋凭据", "折叠纸片", true);
        ObserveTags("ticket", Read);
        Describe("dorm_door", "五号房门", "带门牌的房门", false);
        ObserveTags("dorm_door", Closed, Bolt);
        Describe("window", "五号房铁格窗", "铁格窗", false);
        ObserveTags("window", Closed);
        Describe("wastebasket", "纸篓", "篓筐", false);
        foreach (var actor in Souls.Keys)
        {
            Describe(actor, actor, "在场的人", false);
            ObserveTags(actor, Holding);
        }
        foreach (var actor in new[] { "侦探", "工程师霍铎", "主厨沈鼎", "音乐家苏泠" })
        {
            Describe("reward:" + actor, actor + "的约定报偿", "待核验物件", true, RedTokenOperation.VerifyReward);
            ObserveTags("reward:" + actor, Read);
            PlaceObject("reward:" + actor, "cabinet");
            Describe("sign:" + actor, actor + "的返房签条", "具名签条", true, RedTokenOperation.ReturnRoom);
            ObserveTags("sign:" + actor, Taken);
            PlaceObject("sign:" + actor, "room_sign");
        }

        foreach (var name in new[] { "receiver", "red", "chair", "rack", "board", "study_table", "pipe" }) PlaceObject(name, "B");
        foreach (var name in new[] { "clip", "upstream", "confirmation", "water" }) PlaceObject(name, "C");
        foreach (var name in new[] { "room_sign", "cabinet" }) PlaceObject(name, "A");
        foreach (var name in new[] { "dorm_desk", "coat", "dorm_door", "window", "wastebasket" }) PlaceObject(name, "R05");
        PlaceObject("ration", "dorm_desk"); PlaceObject("cup", "dorm_desk"); PlaceObject("ticket", "coat");
        PlaceObject("dorm_drawer", "dorm_desk"); PlaceObject("letter_sheet", "dorm_drawer"); PlaceObject("torn_letter", "dorm_drawer");
        PlaceObject("paper", "wastebasket");
        PlaceObject("daily_rules", "board"); PlaceObject("exception", "board"); PlaceObject("signed_exception", "board");
        PlaceObject("rack_record", "rack"); PlaceObject("completion_record", "confirmation");
        PlaceObject("drawer", "receiver"); PlaceObject("seal", "drawer");
        PlaceObject("red_slot", "receiver"); PlaceObject("handle_slot", "receiver");
        PlaceObject("receipt_red", "red_slot"); PlaceObject("receipt_handle", "handle_slot");
        PlaceObject("white", "receiver"); PlaceObject("handle", "rack");
        PlaceObject("cloth", "clip"); PlaceObject("wedge", "door");
        PlaceObject("movement_record", "room");
        SetInitial("drawer", Closed, 1);
        SetInitial("wedge", Narrow, 1);
        SetInitial("dorm_door", Closed, 1);
        SetInitial("window", Closed, 1);
        if (Kind == RedTokenWorldKind.SafeTrial) PlaceObject("wedge", "D");
        if (Kind == RedTokenWorldKind.Scene)
        {
            PlaceObject("handle", "drawer"); PlaceObject("red", "chair"); PlaceObject("white", "room_sign"); PlaceObject("wedge", "paper");
            foreach (var tag in new[] { Filter, Ash, Wiped, Washed }) SetInitial("cloth", tag, 1);
            SetInitial("door", Closed, 1); SetInitial("door", Bolt, 1);
            SetInitial("study_table", Dirty, 1);
            SetInitial("wedge", Dirty, 1); SetInitial("wedge", Wrapped, 1); SetInitial("paper", Dirty, 1);
            foreach (var name in new[] { "receipt_red", "receipt_handle" })
                foreach (var tag in new[] { Received, Pushed }) SetInitial(name, tag, 1);
            SetInitial("upstream", Ash, 1); SetInitial("water", Basin, 1);
            foreach (var tag in new[] { Clear, Confirmed, Released })
            {
                SetInitial("completion_record", tag, 1);
                SetInitial("confirmation", tag, 1);
            }
            SetInitial("receiver", Broken, 1); SetInitial("seal", Broken, 1); SetInitial("drawer", Closed, 0);
            SetInitial("chair", Taken, 1); SetInitial("room_sign", Taken, 4); SetInitial("white", Taken, 1); SetInitial("receiver", Taken, 1);
            SetInitial("signed_exception", Read, 1); SetInitial("rack_record", Accepted, 1);
            foreach (var actor in new[] { "侦探", "工程师霍铎", "主厨沈鼎", "音乐家苏泠" })
            {
                SetInitial("sign:" + actor, Taken, 1);
                PlaceObject(actor, actor == "侦探" ? "R05" : "A");
            }
        }

        var bindings = new Dictionary<string, IReadOnlyList<Essence>>(StringComparer.Ordinal);
        void Bind(string id, params string[] names) => bindings[id] = names.Select(name => Objects[name]).ToImmutableArray();
        Bind("E01", "cabinet", "reward:侦探", "reward:工程师霍铎", "reward:主厨沈鼎", "reward:音乐家苏泠");
        Bind("E02", "board", "daily_rules", "exception");
        Bind("E03", "handle", "rack");
        Bind("E04", "door", "pipe");
        Bind("E05", "study_table", "wedge");
        Bind("E06", "door", "wedge");
        Bind("E07", "paper", "wedge", "wastebasket");
        Bind("E08", "cloth", "clip");
        Bind("E09", "water", "cloth", "clip", "completion_record");
        Bind("E09.deep", "upstream"); Bind("E09.completion", "completion_record");
        Bind("E10", "rack", "handle", "rack_record");
        Bind("E11", "board", "exception", "signed_exception");
        Bind("E12", "signed_exception", "wedge", "工程师霍铎", "音乐家苏泠");
        Bind("E13", "receiver", "red_slot", "handle_slot", "receipt_red", "receipt_handle", "red", "handle", "white");
        Bind("E14", "room_sign", "white", "sign:侦探", "sign:工程师霍铎", "sign:主厨沈鼎", "sign:音乐家苏泠");
        Bind("E15", "chair", "red", "receiver", "drawer", "seal");
        Bind("E16", "pipe", "movement_record");
        Bind("ROOM_COAT", "coat", "ticket"); Bind("ROOM_DESK", "dorm_desk", "ration", "cup", "dorm_drawer", "letter_sheet", "torn_letter");
        Bind("ROOM_DOOR", "dorm_door"); Bind("ROOM_WINDOW", "window");
        EvidenceObjects = bindings.ToImmutableDictionary(StringComparer.Ordinal);

        // 纸件来源有实际签署对象；未认识签署者时，观察映射不会替读者创造身份。
        void Source(string record, string author, string subject)
        {
            var surface = Objects[record].Add(new SurfaceProperty());
            surface.Relations[_views[record]] = [(Relations.Subject, _views[author]), (Relations.Object, _views[subject])];
        }
        Source("signed_exception", "工程师霍铎", "exception");
        Source("rack_record", "工程师霍铎", "handle");
        Source("completion_record", "主厨沈鼎", "confirmation");
        Source("movement_record", "接待者", "red");
        foreach (var actor in Companions) Source("sign:" + actor, actor, "white");
    }

    private void Describe(string name, string displayName, string unrecognizedName, bool portable, params RedTokenOperation[] operations)
    {
        Objects[name].Add(new RedTokenObjectProperty
        {
            DisplayName = displayName, UnrecognizedName = unrecognizedName,
            Portable = portable, Operations = operations.ToImmutableHashSet()
        });
        ObserveTags(name, Portable);
        SetInitial(name, Portable, portable ? 1 : 0);
    }

    private void ObserveTags(string name, params Tag[] tags)
    {
        var state = Objects[name].Get<StateProperty>()!;
        foreach (var tag in tags)
        {
            state.Observable.Add(tag);
            state.Values.TryAdd(tag, 0);
        }
    }

    private void SetInitial(string name, Tag tag, int value) => Objects[name].Get<StateProperty>()!.Values[tag] = value;

    private void PlaceObject(string name, string support)
    {
        var spatial = Objects[name].Get<SpatialProperty>()!;
        spatial.Holder = null;
        spatial.Space = Objects[support];
    }

    private bool IsAtObject(Essence obj, string support)
    {
        var visited = new HashSet<Essence>();
        for (Essence? current = obj; current is not null && visited.Add(current);
            current = current.Get<SpatialProperty>()?.Holder ?? current.Get<SpatialProperty>()?.Space)
            if (ReferenceEquals(current, Objects[support])) return true;
        return false;
    }

    /// <summary>入口只提供现场可见的真实物件；绑定关系不构成观察、回忆或远程同步。</summary>
    public IEnumerable<Essence> VisibleAtEvidence(string id)
    {
        if (id == "E12" || !EvidenceObjects.TryGetValue(id, out var objects)) yield break;
        foreach (var obj in objects)
        {
            bool visible = id switch
            {
                "E01" => IsAtObject(obj, "cabinet"),
                "E02" => IsAtObject(obj, "board") && obj != Objects["exception"],
                "E03" or "E10" => IsAtObject(obj, "rack"),
                "E04" => IsAtObject(obj, "B"),
                "E05" => IsAtObject(obj, "study_table"),
                "E06" => IsAtObject(obj, "door"),
                "E07" => IsAtObject(obj, "wastebasket"),
                "E08" => IsAtObject(obj, "clip"),
                "E09" or "E09.deep" or "E09.completion" => IsAtObject(obj, "C"),
                "E11" => IsAtObject(obj, "board"),
                "E13" => IsAtObject(obj, "receiver") &&
                    (!(obj == Objects["red"] || obj == Objects["handle"] || obj == Objects["white"]) || Value("drawer", Closed) == 0),
                "E14" => IsAtObject(obj, "room_sign"),
                "E15" => IsAtObject(obj, "chair") || IsAtObject(obj, "receiver"),
                "E16" => IsAtObject(obj, "B") && (obj != Objects["movement_record"] || Value("pipe", Taken) == 1),
                "ROOM_COAT" => IsAtObject(obj, "coat"),
                "ROOM_DESK" => IsAtObject(obj, "dorm_desk"),
                "ROOM_DOOR" => IsAtObject(obj, "R05"),
                "ROOM_WINDOW" => IsAtObject(obj, "R05"),
                _ => false
            };
            if (visible) yield return obj;
        }
    }
}
