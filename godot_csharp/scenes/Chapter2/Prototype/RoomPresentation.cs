using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed record RoomAction(string Label, string Command, string Kind = "command");
public sealed record RoomView(string? Place, Person[] People, string[] Things, bool Board, RoomAction[] Actions);

/// <summary>同一份房间与行动条件供文字入口、地图入口使用，GD只负责呈现及参数输入。</summary>
public static class RoomPresentation
{
    // STORY-HARDCODE[CH2-078][text][production]: 按本章时段、地点与案件状态提供场景动作选项。
    public static RoomView For(Game g, Place? place)
    {
        if (place is not { } here) return new(null, [], [], false, []);
        var actions = new List<RoomAction>();
        var board = g.Board.ContainsKey(here);
        var things = g.RoomThings(here).ToArray();
        var daytime = g.Slot != Slot.Night;
        var confrontation = g.Day == Game.LastDay && g.Slot == Slot.Morning;
        if (daytime && !confrontation)
        {
            foreach (var thing in things) actions.Add(new("细看：" + thing, "查 " + thing, "inspect"));
            if (board) actions.Add(new("在这里贴一条规矩……", "贴 " + Names.Of(here), "post"));
            switch (here)
            {
                case Place.Lecture when g.Slot == Slot.Forenoon:
                    actions.Add(new("上课", "上课"));
                    actions.Add(new("上课，并在答疑时提问……", "上课", "class_question"));
                    break;
                case Place.Logistics:
                    if (g.Slot is Slot.Noon or Slot.Evening) actions.Add(new("领今天的口粮", "领粮"));
                    if (g.Day == Game.FirstDay && g.Slot == Slot.Noon) actions.Add(new("调停配给的争执", "调停"));
                    break;
                case Place.Greenhouse when g.Slot == Slot.Afternoon:
                    actions.Add(new("请两个人一起修温室……", "修温室", "greenhouse"));
                    break;
                case Place.ArchiveFront:
                    actions.Add(new("请人来试开名录库之门……", "开库", "archive"));
                    break;
            }
        }
        if (board) actions.Add(new("读这里的公告（不占时段）", "公告", "info"));
        if (here == Place.Hall && daytime)
        {
            if (confrontation) actions.Add(new("开始公开对质", "对质", "confront"));
            if (CanReopen(g)) actions.Add(new("重开对质（援引共同规则）", "重开对质", "reopen"));
            if (g.Manipulations.Any(m => m.Exposed && !m.Apologized)) actions.Add(new("当众认错", "认错"));
        }
        return new(here.ToString(), g.RoomPeople(here).ToArray(), things, board, actions.ToArray());
    }

    public static bool CanReopen(Game g) => g.Day > Game.CaseDay && !(g.Day == Game.LastDay && g.Slot == Slot.Morning) && !g.QuerCleared && g.Accused is not null;

    // STORY-HARDCODE[CH2-079][text][production]: 定义本章当面交互可执行的具体人物行动。
    public static RoomAction[] PersonActions(Game g, P who, Place? place)
    {
        if (place is not { } here || !g.RoomPeople(here).Any(p => p.Id == who)) return [];
        var name = Names.Of(who);
        var actions = new List<RoomAction> { new("看你对" + name + "的笔记", "笔记 " + name, "notes") };
        if (g.Slot == Slot.Night || g.Day == Game.LastDay && g.Slot == Slot.Morning) return actions.ToArray();
        actions.InsertRange(0, [
            new("问" + name + "知道什么", "问 " + name),
            new("请" + name + "做一件事……", "请 " + name, "ask"),
            new("请" + name + "同去看……", "同去 " + name, "escort"),
            new("对" + name + "讲一条规矩……", "讲 " + name, "tellrule"),
            new("把一条消息转告" + name + "……", "转告 " + name, "news"),
            new("帮" + name + "干活，或兑现答应他的事", "帮 " + name),
            new("把今天的口粮分给" + name, "分粮 " + name),
            new("请" + name + "交出钥匙给……", "交钥匙 " + name, "givekey"),
            new("对" + name + "只说一半实话，关于……", "片面告知 " + name, "slant")
        ]);
        if (!g.Wagers.Any(w => w.W.Day == g.Day)) actions.Add(new("和" + name + "打赌……", "打赌 " + name, "wager"));
        return actions.ToArray();
    }

    /// <summary>只约束发起地点与当面对象；同行目的地、谈论对象和请求任务保留原版语义。</summary>
    public static void Validate(Game g, string command, string[] args)
    {
        var here = g.Where(g.Det);
        void At(Place place)
        {
            if (here != place) throw new PlayerError("请先去" + Names.Of(place) + "。");
        }
        if (command is "ask" or "askabout" or "escort" or "givekey" or "tell" or "tellrule" or "helpout" or "share" or "slant" or "wager")
            if (args.Length > 1 && Names.PersonAlias.TryGetValue(args[1], out var who) && who != P.Det && !g.RoomPeople(here).Any(p => p.Id == who))
                throw new PlayerError(Names.Of(who) + "不在这个房间。");
        switch (command)
        {
            case "class": At(Place.Lecture); break;
            case "ration": case "mediate": At(Place.Logistics); break;
            case "greenhouse": At(Place.Greenhouse); break;
            case "archive": At(Place.ArchiveFront); break;
            case "post":
                if (args.Length > 1 && Names.PlaceAlias.TryGetValue(args[1], out var place)) At(place);
                break;
            case "confront": case "reopen": case "apologize": At(Place.Hall); break;
        }
    }
}

public sealed record ActionFrame(double Time, string? Actor, string Place, string Frame, string Text, bool Anonymous, string? FramesPath = null);
public abstract record FreeEvent(int Slot)
{
    public sealed record Visit(int At, Place Place) : FreeEvent(At);
    public sealed record Choice(int At, string Id, string Option) : FreeEvent(At);
    public sealed record Bet(int At, Wager Wager) : FreeEvent(At);
    public sealed record Corridor(int At, string Name, bool Open) : FreeEvent(At);
    public sealed record DormOut(int At, string Name) : FreeEvent(At);
    public sealed record Mediation(int At, string Wording) : FreeEvent(At);
    public sealed record Summon(int At, string Id) : FreeEvent(At);
}

public sealed partial class Game
{
    public readonly List<(int Slot, Place Place)> PlaceScript = [];
    public readonly List<(int Slot, string Choice, string Option)> StoryChoices = [];
    public readonly List<FreeEvent> FreeEvents = [];
    public readonly List<ActionFrame> PresentationFrames = [];
    public int PresentationRevision { get; private set; }
    public bool CaptureFrames { get; set; }
    private readonly Dictionary<P, (Place Place, Job? Job, bool Asleep)> _presented = [];
    private readonly Dictionary<DualOntologyWorldEngine.Story.ScriptCharacter, Essence?> _scriptPresented = [];
    private readonly HashSet<(ActionExecution Execution, Essence Body, string Frame)> _presentedActions = [];

    public IEnumerable<Person> RoomPeople(Place place)
    {
        SyncChapterRooms();
        var room = Chapter.RoomCharacters(place.ToString()).ToHashSet();
        return Npcs.Where(p => room.Contains(p.Id.ToString()) && !(Slot == Slot.Night && p.Asleep));
    }
    public IEnumerable<string> RoomThings(Place place) => Chapter.RoomObjects(place.ToString());

    private void SyncChapterRooms()
    {
        foreach (var p in People) Chapter.Locate(p.Id.ToString(), Where(p).ToString());
    }

    public void SetPlayerPlace(Place place)
    {
        if (Where(Det) == place) return;
        Det.Body.Get<SpatialProperty>()!.Space = Space[place];
        Rt.Sync();
        SyncChapterRooms();
        Perceive(Det);
        PlaceScript.Add((SlotIndex, place));
        FreeEvents.Add(new FreeEvent.Visit(SlotIndex, place));
    }

    public void ChooseStory(string option)
    {
        var choice = Story!.PendingChoice!;
        Story.Choose(option);
        StoryChoices.Add((SlotIndex, choice.Id, option));
        FreeEvents.Add(new FreeEvent.Choice(SlotIndex, choice.Id, option));
        Story.Pump();
    }

    public void RestoreFree(int slot, IReadOnlyList<(int Slot, Place Place)>? places, IReadOnlyList<(int Slot, string Choice, string Option)>? choices,
        IReadOnlyList<FreeEvent>? events = null)
    {
        if (events is not null)
        {
            foreach (var entry in events.Where(e => e.Slot == slot))
            {
                Story?.Pump();
                switch (entry)
                {
                    case FreeEvent.Visit visit: SetPlayerPlace(visit.Place); break;
                    case FreeEvent.Choice selected:
                        if (Story?.PendingChoice?.Id != selected.Id) throw new InvalidOperationException("重演剧情选择与原记录不一致。");
                        ChooseStory(selected.Option); break;
                    case FreeEvent.Bet bet: MakeWager(bet.Wager.With, bet.Wager.Who, bet.Wager.Where, bet.Wager.Spec); break;
                    case FreeEvent.Corridor corridor: HostCorridor(corridor.Name, corridor.Open); break;
                    case FreeEvent.DormOut dorm: HostDormOut(dorm.Name); break;
                    case FreeEvent.Mediation mediation: HostMediate(mediation.Wording); break;
                    case FreeEvent.Summon summon: HostSummon(summon.Id); break;
                }
            }
            return;
        }
        foreach (var (_, place) in places?.Where(p => p.Slot == slot) ?? []) SetPlayerPlace(place);
        foreach (var (_, choice, option) in choices?.Where(c => c.Slot == slot) ?? [])
        {
            Story?.Pump();
            if (Story?.PendingChoice?.Id != choice) throw new InvalidOperationException("重演剧情选择与原记录不一致。");
            ChooseStory(option);
        }
    }

    private void BeginPresentation()
    {
        if (!CaptureFrames) return;
        PresentationRevision++;
        _presented.Clear();
        _presentedActions.Clear();
        foreach (var p in People) _presented[p.Id] = (Where(p), p.Doing, p.Asleep);
        _scriptPresented.Clear();
        if (DefinedScriptWorld is { } world)
            foreach (var actor in world.Characters.Values.Where(c => !People.Any(p => ReferenceEquals(p.Body, c.Body))))
                _scriptPresented[actor] = actor.Body.Get<SpatialProperty>()?.Space;
    }

    private void CapturePresentation(ActionExecution execution)
    {
        if (!CaptureFrames) return;
        var room = Where(Det);
        foreach (var p in People)
        {
            var next = (Place: Where(p), Job: p.Doing, p.Asleep);
            if (!_presented.TryGetValue(p.Id, out var previous)) { _presented[p.Id] = next; continue; }
            _presented[p.Id] = next;
            var isActor = ReferenceEquals(execution.Intent.Actor, p.Self);
            if (previous == next && !isActor) continue;
            var visible = next.Place == room || previous.Place == room;
            if (!visible || Slot == Slot.Night && (p.Asleep || p != Det && LastAction?.Cmd != Cmd.Stakeout)) continue;
            var anonymous = Slot == Slot.Night && p != Det;
            var frame = next.Place != previous.Place ? "行走" : next.Asleep ? "坐" : next.Job is { } job ? Names.Frame(job) : "交谈";
            var firstFrame = _presentedActions.Add((execution, p.Body, frame));
            if (previous == next && !firstFrame) continue;
            var text = anonymous ? "一个" + BuildText(p.Pro.Build) + "身影" : p.Name + (next.Place != previous.Place ? (next.Place == room ? "走进房间" : "离开房间") : next.Job is { } doing ? Names.Of(doing) : "停下了动作");
            PresentationFrames.Add(new(Now, anonymous ? null : p.Id.ToString(), next.Place == room ? next.Place.ToString() : room.ToString(), frame, RenderText(text, p, Det), anonymous));
        }
        if (DefinedScriptWorld is { } scriptWorld)
            foreach (var actor in scriptWorld.Characters.Values.Where(c => !People.Any(p => ReferenceEquals(p.Body, c.Body))))
            {
                var next = actor.Body.Get<SpatialProperty>()?.Space;
                var previous = _scriptPresented.GetValueOrDefault(actor);
                _scriptPresented[actor] = next;
                var acting = ReferenceEquals(execution.Intent.Actor, actor.Self);
                if (actor.SpriteFramesPath is null || ReferenceEquals(previous, next) && !acting) continue;
                if (!ReferenceEquals(next, Space[room]) && !ReferenceEquals(previous, Space[room])) continue;
                if (Slot == Slot.Night && LastAction?.Cmd != Cmd.Stakeout) continue;
                var anonymous = Slot == Slot.Night;
                var moved = !ReferenceEquals(previous, next);
                var frame = moved ? "行走" : "操作";
                var firstFrame = _presentedActions.Add((execution, actor.Body, frame));
                if (ReferenceEquals(previous, next) && !firstFrame) continue;
                var text = anonymous ? "一个身影" : actor.Body.TrueName + (moved ? (ReferenceEquals(next, Space[room]) ? "走进房间" : "离开房间") : "行动");
                PresentationFrames.Add(new(Now, anonymous ? null : "script:" + actor.Key, room.ToString(), frame, text, anonymous,
                    anonymous ? null : actor.SpriteFramesPath));
            }
    }

    public void StartPresentation() => BeginPresentation();
}
