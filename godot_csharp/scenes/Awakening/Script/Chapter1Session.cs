using System.Text.Json;
using DualOntologyWorldEngine.Story;
using static DualOntologyWorldEngine.Story.ChapterEvents;

namespace DualOntologyWorldEngine.Awakening;

using Ev = Dictionary<string, object?>;
using Input = IReadOnlyDictionary<string, object?>;

/// <summary>
/// 第一章的统一事件会话（设计第四部分 §2）。宿主只转发：现场与界面的输入经 Dispatch 进来，
/// 台词、卡片、菜单、表单、走位与镜头作为事件出去。原宿主里的流程编排（相遇、触物、请人比对、
/// 声路试验、答辩、终局与日常）都在这里，以可等待的作业运行；几何由现场在输入的 context 里报告。
/// </summary>
public sealed partial class Chapter1Session : IChapterSession
{
    public string ChapterId => "chapter1";
    private static readonly Chapter1Script Script = new();
    private static readonly Lazy<JsonDocument> LayoutDocument = new(() =>
    {
        using var stream = typeof(Chapter1Data).Assembly.GetManifestResourceStream("Awakening.Layout.json")
            ?? throw new InvalidOperationException("missing compiled chapter layout");
        return JsonDocument.Parse(stream);
    });
    private static JsonElement Layout => LayoutDocument.Value.RootElement;
    private static readonly string[] Companions = ["huo", "shen", "su"];
    private static readonly string[] Party = ["detective", "huo", "shen", "su"];
    private const string Art = "res://ShareAsset/Internal/Props/AwakeningEvidence/";

    private MorningWorld _world = null!;
    private AwakeningContentSession _content = null!;
    private Chapter1Dialogue _dialogue = null!;
    private Chapter1Interaction _interaction = null!;
    private JsonElement Content => _content.Content.RootElement;

    public Chapter1Session() => Reset();

    public void Reset()
    {
        ChapterWorldContinuity.Clear();
        _world = Script.CreateWorld();
        _world.InitializePlayerPerception();
        _ = _world.PersistentSession;
        _content = Script.CreateContent();
        _dialogue = new(_content, _world);
        _interaction = new(_content, _world);
        _jobs.Clear(); _uiOwner = null; _modal = null; _facts.Clear();
        _dialogueActive = _storyLock = _begun = false; _planDepth = 0;
        _dailyJobs.Clear(); _staged.Clear(); _invites.Clear(); _handWait.Clear(); _furnitureSpoken.Clear();
        _currentRoom = ""; _lastSnapshot = ""; _now = 0; _asideUntil = 0; _witnessAfter = 0;
    }

    public MorningWorld World => _world;
    public AwakeningContentSession ContentSession => _content;

    // ------------------------------------------------------------ state --

    private ChapterSession Session => _content.Session;
    private Dictionary<string, object> State => Session.GetProgress("state");
    private string Stage
    {
        get => (string)State["stage"];
        set { Session.Set("state", "stage", value); _world.SetDailyStage(value); }
    }
    private JsonElement StageRules => _content.Stage(Stage);
    private bool StageFlag(string key) => StageRules.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
    private bool Ended => State.GetValueOrDefault("ended") is true;
    private string DetectiveName => (string)State["detective_name"];
    private bool ReplayOpen => State.GetValueOrDefault("replay_open") is true;
    private bool Has(string group, string key) => Session.Has(group, key);
    private static string T(string key, params (string, string)[] args) => Chapter1Data.Text(key, args);
    private static string PersonName(string who) => Chapter1Data.PersonName(who);
    private static Person PersonOf(string who) => (Person)Array.IndexOf(Names.PersonKeys, who);

    private bool _dialogueActive, _storyLock, _begun;
    private int _planDepth;
    private double _now, _asideUntil, _witnessAfter;
    private string _currentRoom = "";
    private readonly HashSet<string> _furnitureSpoken = new(StringComparer.Ordinal);

    // ---------------------------------------------------------- context --

    /// <summary>现场报告的几何事实：房间（区域键与实际室内落脚）、是否可见、是否在走、与侦探的距离、身边的交互点。</summary>
    private sealed class Fact
    {
        public string Room = "", Entered = "", RoomName = "";
        public bool Visible = true, Moving;
        public double Distance = double.PositiveInfinity;
        public HashSet<string> Near = new(StringComparer.Ordinal);
    }
    private readonly Dictionary<string, Fact> _facts = new(StringComparer.Ordinal);
    private bool _fieldActive;
    private string _uiModal = "";
    private Fact F(string who) => _facts.TryGetValue(who, out var fact) ? fact : _facts[who] = new();

    private void ReadContext(Input input)
    {
        if (input.GetValueOrDefault("context") is not Input context) return;
        _fieldActive = Flag(context, "field_active");
        _uiModal = Text(context, "modal");
        foreach (var (who, value) in Map(context, "actors"))
        {
            if (value is not Input data) continue;
            var fact = F(who);
            fact.Room = Text(data, "room"); fact.Entered = Text(data, "entered"); fact.RoomName = Text(data, "room_name");
            fact.Visible = Flag(data, "visible", true); fact.Moving = Flag(data, "moving");
            fact.Distance = Number(data, "distance", double.PositiveInfinity);
            fact.Near = (data.GetValueOrDefault("near_sites") as IEnumerable<object?> ?? []).Select(s => s?.ToString() ?? "").ToHashSet(StringComparer.Ordinal);
        }
        foreach (var (site, room) in Map(context, "site_rooms")) Session.Place(site, room?.ToString() ?? "");
        foreach (var who in Party) if (_facts.TryGetValue(who, out var fact)) Locate(who, fact.Room);
    }

    private void Locate(string who, string room)
    {
        Session.Locate(who, room);
        var index = Array.IndexOf(Names.RoomKeys, room);
        if (index >= 0) _world.Locate(PersonOf(who), (Room)index);
    }

    /// <summary>与原宿主 field_active 相同：阶段允许行走，没有对白、弹窗、剧情锁。</summary>
    private bool FieldFree => _fieldActive && StageFlag("walk") && !_storyLock && !_dialogueActive && !Ended && _uiOwner is null && _modal is null;

    // ---------------------------------------------------------- dispatch --

    private List<Ev> _out = new();
    private string _lastSnapshot = "";

    public IReadOnlyList<Dictionary<string, object?>> Dispatch(Input input)
    {
        _out = new();
        var kind = Text(input, "kind");
        try
        {
            ReadContext(input);
            switch (kind)
            {
                case "begin":
                    if (_begun) throw new InvalidOperationException(Chapter1Data.Text("界面·错误·已开始"));
                    _begun = true;
                    Start(Begin());
                    break;
                case "presented": case "select": case "form":
                    Reply(input);
                    break;
                case "tick":
                    Tick(Number(input, "delta"));
                    break;
                case "interact":
                    if (FieldFree) Start(Interact(Text(input, "site"), Text(input, "target")));
                    break;
                case "card":
                    var card = Text(input, "card_id");
                    Start(card == "board" ? OpenBoard() : OpenCard(card));
                    break;
                case "furniture": Furniture(input); break;
                case "move": break; // 房间进入由 tick 的实际落脚判定，与原宿主每帧检查一致。
                case "daily": DailyReport(input); break;
                case "replay_vars": State["replay_vars"] = ReplayVariables(input); break;
                case "replay": Start(Replay(ReplayVariables(input), Flag(input, "skip"))); break;
                case "opened":
                    if (Text(input, "panel") == "manuscript" && _content.V6Open) Session.Set("flags", "v6_seen", true);
                    break;
                case "control": SetControl(Text(input, "mode")); break;
                case "debug_jump": Start(Jump(Text(input, "state"))); break;
                default: throw new InvalidOperationException("未知章节事件：" + kind);
            }
            Pump();
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            _out.Add(Event("error", ("message", error.Message)));
        }
        var snapshot = Snapshot();
        var serialized = JsonSerializer.Serialize(snapshot);
        if (kind != "tick" || serialized != _lastSnapshot) _out.Add(Event("state", ("snapshot", snapshot)));
        _lastSnapshot = serialized;
        return _out;
    }

    private static object[] Ints(object? value) => (value as IEnumerable<object?> ?? []).Select(v => (object)Convert.ToInt32(v)).ToArray();

    private void SetControl(string mode)
    {
        _out.Add(StageOp("cancel_click"));
        ReleaseDaily("detective", true);
        _world.SetDetectiveControl(mode);
    }

    // --------------------------------------------------------- scheduler --

    private abstract record Op;
    /// <summary>发出带 presentation_id 的事件并等到回执（presented / select / form）。Ui 事件一次只占一个。</summary>
    private sealed record Await(Ev Event, bool Ui) : Op;
    private sealed record Until(Func<bool> Ready) : Op;
    private sealed record Sleep(double Seconds) : Op;
    private sealed record Run(IEnumerable<Op> Body) : Op;

    private sealed class Job
    {
        public readonly Stack<IEnumerator<Op>> Frames = new();
        public Op? Blocked;
        public string? Ack;
        public bool Emitted;
        public Input? Reply;
        public double Remaining;
    }
    private readonly List<Job> _jobs = new();
    private Job? _uiOwner;
    private Input _reply = new Dictionary<string, object?>();

    private void Start(IEnumerable<Op> body, bool immediate = false)
    {
        var job = new Job();
        job.Frames.Push(body.GetEnumerator());
        _jobs.Add(job);
        if (immediate) Advance(job);
    }

    private void Pump()
    {
        for (var guard = 0; guard < 100000; guard++)
        {
            var progressed = false;
            foreach (var job in _jobs.ToArray()) progressed |= Advance(job);
            _jobs.RemoveAll(job => job.Frames.Count == 0);
            if (_planDepth == 0 && !_storyLock && !_dialogueActive && _modal is null && _uiOwner is null && _uiModal == "" && _content.RevealV6())
            {
                Start(V6Doubt());
                progressed = true;
            }
            if (!progressed) return;
        }
        throw new InvalidOperationException(Chapter1Data.Text("界面·错误·未收敛"));
    }

    private bool Advance(Job job)
    {
        switch (job.Blocked)
        {
            case Until until when !until.Ready(): return false;
            case Sleep when job.Remaining > 0: return false;
            case Await wait when !job.Emitted:
                if (wait.Ui && _uiOwner is not null && _uiOwner != job) return false;
                Emit(job, wait);
                return true;
            case Await when job.Reply is null: return false;
            case Await:
                _reply = job.Reply!;
                job.Reply = null; job.Ack = null;
                if (_uiOwner == job) _uiOwner = null;
                break;
        }
        job.Blocked = null;
        while (job.Frames.Count > 0)
        {
            var frame = job.Frames.Peek();
            if (!frame.MoveNext()) { frame.Dispose(); job.Frames.Pop(); continue; }
            switch (frame.Current)
            {
                case Run run: job.Frames.Push(run.Body.GetEnumerator()); continue;
                case Until until when until.Ready(): continue;
                case Sleep sleep when sleep.Seconds <= 0: continue;
                case Sleep sleep: job.Blocked = sleep; job.Remaining = sleep.Seconds; return true;
                case Await wait:
                    job.Blocked = wait; job.Emitted = false;
                    if (!wait.Ui || _uiOwner is null || _uiOwner == job) Emit(job, wait);
                    return true;
                case { } other: job.Blocked = other; return true;
            }
        }
        return true;
    }

    private void Emit(Job job, Await wait)
    {
        var id = Guid.NewGuid().ToString("N");
        wait.Event["presentation_id"] = id;
        job.Ack = id; job.Emitted = true;
        if (wait.Ui) _uiOwner = job;
        _out.Add(wait.Event);
    }

    /// <summary>回执：先交给等待它的作业；否则是非阻塞卡片/菜单的选项。</summary>
    private void Reply(Input input)
    {
        var id = Text(input, "presentation_id");
        var waiting = _jobs.FirstOrDefault(job => job.Ack == id && job.Emitted);
        if (waiting is not null) { waiting.Reply = input; return; }
        if (_modal is { } modal && modal.Id == id)
        {
            var choice = Text(input, "choice_id");
            if (!modal.Handlers.TryGetValue(choice, out var handler)) throw new InvalidOperationException(Chapter1Data.Text("界面·错误·选择失效"));
            _modal = null;
            Start(handler());
            return;
        }
        throw new InvalidOperationException(Chapter1Data.Text("界面·错误·凭证失效"));
    }

    // ------------------------------------------------------------ modal --

    private sealed record Modal(string Id, Dictionary<string, Func<IEnumerable<Op>>> Handlers);
    private Modal? _modal;

    /// <summary>非阻塞弹窗（证物卡、菜单）：选项回来时启动对应的作业。</summary>
    private void OpenModal(Ev ev, IEnumerable<(Ev Option, Func<IEnumerable<Op>> Handler)> options)
    {
        var id = Guid.NewGuid().ToString("N");
        var list = options.ToList();
        ev["presentation_id"] = id;
        ev["options"] = list.Select(o => (object?)o.Option).ToArray();
        _modal = new(id, list.ToDictionary(o => (string)o.Option["id"]!, o => o.Handler, StringComparer.Ordinal));
        _out.Add(ev);
    }

    private void CloseModal()
    {
        if (_modal is null) return;
        _modal = null;
        _out.Add(Event("close"));
    }

    private static IEnumerable<Op> Nothing() { yield break; }

    // ------------------------------------------------------------- events --

    private static Ev StageOp(string op, params (string Key, object? Value)[] args)
    {
        var ev = Event("stage", ("op", op));
        foreach (var (key, value) in args) ev[key] = value;
        return ev;
    }
    private void Aside(string text)
    {
        _out.Add(Event("aside", ("text", text)));
        _asideUntil = _now + 3.2;
    }
    private void Sfx(string id, double volume = -4.0) => _out.Add(Event("sound", ("id", id), ("volume", volume)));
    private void Music(string id, double fade = 2.5) => _out.Add(Event("music", ("id", id), ("fade", fade)));
    private static object[] Point(JsonElement pair) => [pair[0].GetDouble(), pair[1].GetDouble()];

    // --------------------------------------------------------------- tick --

    private void Tick(double delta)
    {
        _now += delta;
        foreach (var job in _jobs) if (job.Blocked is Sleep) job.Remaining -= delta;
        Triggers();
        Daily(delta);
    }

    /// <summary>原宿主每帧的 _check_triggers：按脚下实际室内地面判定集会与进房。</summary>
    private void Triggers()
    {
        if (_storyLock || _dialogueActive || _modal is not null || _uiOwner is not null || _uiModal != "") return;
        var room = F("detective").Entered;
        if (room == "") return;
        var trigger = _content.RoomTrigger(room);
        if (trigger == "meeting") { Start(Meeting()); return; }
        if (trigger == "room" && room != _currentRoom)
        {
            _currentRoom = room;
            Start(RoomChanged(room));
            _content.RoomEntry(room);
        }
    }

    // -------------------------------------------------------------- daily --

    private sealed class DailyJob { public int Serial; public bool Working; }
    private readonly Dictionary<string, DailyJob> _dailyJobs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _staged = new(StringComparer.Ordinal);
    private readonly HashSet<string> _invites = new(StringComparer.Ordinal);
    /// <summary>应邀到 R-05 架沿的人，在那儿等侦探来比手，比过才回去做自己的事。</summary>
    private readonly HashSet<string> _handWait = new(StringComparer.Ordinal);

    private IEnumerable<string> DailyKeys => _world.DetectiveControl == "simulation" ? Party : Companions;
    private string LifeState(string who) => Session.GetProgress("life").TryGetValue(who, out var life)
        ? ((Dictionary<string, object>)life).GetValueOrDefault("state") as string ?? "" : "";
    private bool LifeBusy(string who) => LifeState(who) is "going" or "holding";

    private List<string> DailyBusy() => DailyKeys.Where(who => _staged.ContainsKey(who) || _invites.Contains(who) || _handWait.Contains(who) || LifeBusy(who)
        || (F(who).Moving && !_dailyJobs.ContainsKey(who))).ToList();

    private void ReleaseDaily(string who, bool suspend = false)
    {
        if (!_dailyJobs.Remove(who)) return;
        _out.Add(StageOp("daily_release", ("who", who)));
        if (suspend) _world.DailySuspend(who);
    }
    private void PauseDaily() { foreach (var who in _dailyJobs.Keys.ToArray()) ReleaseDaily(who, true); }

    private void Daily(double delta)
    {
        _world.SetMorningGameMinutes(_content.GameMinutes);
        _world.SetDailyStage(Stage);
        var active = FieldFree && StageFlag("daily") && _planDepth == 0;
        var busy = DailyBusy();
        if (!active) PauseDaily();
        else foreach (var who in busy) ReleaseDaily(who, true);
        var plans = _world.DailyTick(delta, active, busy);
        if (!active) return;
        var present = new HashSet<string>(StringComparer.Ordinal);
        foreach (var plan in plans)
        {
            if (!DailyKeys.Contains(plan.Who) || busy.Contains(plan.Who)) continue;
            present.Add(plan.Who);
            if (_dailyJobs.TryGetValue(plan.Who, out var job) && job.Serial != plan.Serial) ReleaseDaily(plan.Who);
            if (_dailyJobs.ContainsKey(plan.Who)) continue;
            _dailyJobs[plan.Who] = new() { Serial = plan.Serial };
            _out.Add(StageOp("daily_go", ("who", plan.Who), ("serial", plan.Serial), ("room", plan.Room), ("target", plan.Target),
                ("action", plan.Action), ("speed", plan.Speed)));
        }
        foreach (var who in _dailyJobs.Keys.ToArray()) if (!present.Contains(who)) ReleaseDaily(who);
        if (_now >= _witnessAfter && _now >= _asideUntil)
        {
            _witnessAfter = _now + 1.0;
            var room = F("detective").Entered;
            if (room == "") return;
            var note = _world.DailyWitness(room);
            if (note != "") { Aside(note); _witnessAfter = _now + 18.0; }
        }
    }

    /// <summary>现场对日常走位的回报：到了（落脚房间由现场核验）或被挡。</summary>
    private void DailyReport(Input input)
    {
        var who = Text(input, "who");
        var serial = (int)Number(input, "serial");
        if (!_dailyJobs.TryGetValue(who, out var job) || job.Serial != serial) return;
        if (Text(input, "status") == "arrived" && _world.DailyArrive(who, serial, Text(input, "room")))
        {
            job.Working = true;
            _out.Add(StageOp("daily_work", ("who", who), ("action", Text(input, "action", "rest"))));
            return;
        }
        _world.DailyBlock(who, serial);
        ReleaseDaily(who);
    }

    /// <summary>剧情取得人物：停掉日常与邀请路线，记一次占用。</summary>
    private void Claim(string who)
    {
        ReleaseDaily(who, true);
        _invites.Remove(who);
        _out.Add(StageOp("halt", ("who", who)));
        _staged[who] = _staged.GetValueOrDefault(who) + 1;
    }
    private void Unclaim(string who)
    {
        var count = _staged.GetValueOrDefault(who) - 1;
        if (count <= 0) _staged.Remove(who); else _staged[who] = count;
    }

    // ----------------------------------------------------------- snapshot --

    public IReadOnlyDictionary<string, object?> Snapshot()
    {
        var keys = new List<object?>();
        if (StageFlag("examine")) keys.Add(T("界面·键位·检视"));
        if (StageFlag("notebook")) keys.Add(T("界面·键位·笔记本"));
        if (ReplayOpen) keys.Add(T("界面·键位·重演"));
        var detective = F("detective");
        return new Dictionary<string, object?>
        {
            ["chapter"] = new Ev { ["id"] = ChapterId, ["title"] = Script.Title, ["window_title"] = T("窗口标题") },
            ["stage"] = Stage, ["ended"] = Ended, ["detective_name"] = DetectiveName,
            ["field_enabled"] = StageFlag("walk") && !_storyLock && !Ended && _uiOwner is null && _modal is null,
            ["player_control"] = _world.DetectiveControl != "simulation" && !_staged.ContainsKey("detective"),
            ["realtime"] = true,
            ["hud"] = new Ev
            {
                ["title"] = T("界面·地点前缀") + " " + detective.RoomName, ["time"] = _content.ClockText, ["keys"] = keys.ToArray(),
            },
            ["controls"] = new Ev
            {
                ["notebook"] = StageFlag("notebook"), ["manuscript"] = ReplayOpen, ["campus"] = false, ["wait"] = false,
                ["examine"] = StageFlag("examine"),
            },
            ["access"] = new Ev { ["dorms"] = StageFlag("dorms") },
            ["sites"] = Sites(),
            ["people"] = Companions.Select(who => (object?)new Ev
            {
                ["key"] = who, ["name"] = PersonName(who), ["title"] = PersonName(who), ["present"] = true,
            }).ToArray(),
            ["pinned"] = Party.Where(who => _staged.ContainsKey(who) || _invites.Contains(who) || _handWait.Contains(who) || LifeBusy(who)).Cast<object?>().ToArray(),
            ["cast"] = JsonSerializer.Deserialize<Dictionary<string, string>>(Layout.GetProperty("dialogue_cast").GetRawText()),
            ["notebook"] = Notebook(),
            ["replay"] = ReplaySnapshot(),
            ["daypart"] = "",
        };
    }

    /// <summary>可交互点的标题、是否在本阶段开放、是否还没看过（闪光）。位置由地图提供。</summary>
    private Ev Sites()
    {
        var shownList = StageRules.GetProperty("sites");
        bool Enabled(string id) => shownList.ValueKind == JsonValueKind.String || shownList.EnumerateArray().Any(s => s.GetString() == id);
        var result = new Ev();
        foreach (var site in Layout.GetProperty("sites").EnumerateArray())
        {
            var id = site.GetProperty("id").GetString()!;
            result[id] = new Ev { ["title"] = site.GetProperty("title").GetString(), ["enabled"] = Enabled(id),
                ["new"] = !Has("examined", site.GetProperty("evidence").GetString()!) };
        }
        foreach (var trace in Layout.GetProperty("life_traces").EnumerateArray())
        {
            var id = trace.GetProperty("id").GetString()!;
            result[id] = new Ev { ["title"] = trace.GetProperty("title").GetString(), ["enabled"] = Enabled(id), ["new"] = !Has("examined", id) };
        }
        foreach (var extra in Layout.GetProperty("extra_sites").EnumerateObject())
        {
            var kind = extra.Value.GetProperty("kind").GetString();
            var id = kind == "person" ? "person:" + extra.Value.GetProperty("target").GetString() : extra.Name;
            result[id] = new Ev { ["title"] = extra.Value.GetProperty("title").GetString(), ["enabled"] = Enabled(extra.Name),
                ["new"] = kind == "board" && !Has("flags", "board_read") };
        }
        return result;
    }

    private Ev Notebook()
    {
        var sections = new List<object?>();
        foreach (var place in Content.GetProperty("PLACES").EnumerateArray().Select(p => p.GetString()!))
        {
            var entries = Content.GetProperty("EVIDENCE").EnumerateObject()
                .Where(e => Has("examined", e.Name) && e.Value.GetProperty("place").GetString() == place)
                .Select(e => (object?)new Ev
                {
                    ["label"] = e.Value.GetProperty("name").GetString() + (Has("experiments", e.Name) || Has("helped", e.Name) ? "　·" + T("笔记·已试") : ""),
                    ["card_id"] = e.Name,
                }).ToArray();
            if (entries.Length > 0) sections.Add(new Ev { ["heading"] = "[b]" + place + "[/b]", ["style"] = "grid", ["entries"] = entries });
        }
        if (Has("flags", "board_read"))
            sections.Add(new Ev { ["heading"] = "", ["style"] = "grid", ["entries"] = new object?[] { new Ev { ["label"] = T("笔记·公告框"), ["card_id"] = "board" } } });
        var recalls = Session.GetProgress("recalls");
        if (recalls.Count > 0)
            sections.Add(new Ev { ["heading"] = T("笔记·回忆所得"), ["style"] = "list",
                ["entries"] = recalls.Select(p => (object?)new Ev { ["label"] = $"{PersonName(p.Key)}：{p.Value}" }).ToArray() });
        return new Ev { ["empty_text"] = T("笔记·空"), ["sections"] = sections.ToArray() };
    }

    private Ev ReplaySnapshot()
    {
        var variables = Content.GetProperty("VARIABLES").EnumerateArray().ToArray();
        var count = _content.V6Open ? variables.Length : variables.Length - 1;
        var marks = Session.GetProgress("recall_marks");
        var questions = variables.Take(count).Select((variable, index) => (object?)new Ev
        {
            ["question"] = variable.GetProperty("question").GetString(),
            ["options"] = variable.GetProperty("options").EnumerateArray().Select(o => (object?)o.GetString()).ToArray(),
            ["mark"] = marks.TryGetValue((index + 1).ToString(), out var who)
                ? "　[color=seal]" + T("重演·回忆所证", ("人名", PersonName((string)who))) + "[/color]" : "",
            ["fresh"] = index == 5 && !Has("flags", "v6_seen"),
        }).ToArray();
        var result = Session.GetProgress("replay_result");
        var findings = (result.GetValueOrDefault("findings") as object[] ?? []).Cast<Dictionary<string, object>>().Select(f => (object?)new Ev
        {
            ["title"] = (string)f["status"] != "unvisited" ? Content.GetProperty("EVIDENCE").GetProperty((string)f["id"]).GetProperty("name").GetString() : f["place"],
            ["status"] = f["status"], ["text"] = f["text"], ["card_id"] = f["id"],
        }).ToArray();
        return new Ev
        {
            ["open"] = ReplayOpen, ["title"] = T("重演·标题"), ["hint"] = T("重演·提示"), ["questions"] = questions,
            ["vars"] = State["replay_vars"], ["last_run_vars"] = State["last_run_vars"],
            ["result"] = result.Count == 0 ? new Ev() : new Ev { ["full_match"] = result.GetValueOrDefault("full_match") is true, ["findings"] = findings },
        };
    }
    private static object[] ReplayVariables(Input input)
    {
        var values = Ints(input.GetValueOrDefault("vars"));
        if (values.Length == 5) return [.. values, -1];
        if (values.Length != 6) throw new InvalidOperationException(Chapter1Data.Text("界面·错误·手稿未填"));
        return values;
    }
}
