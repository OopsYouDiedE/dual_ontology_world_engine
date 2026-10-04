using System.Collections.Concurrent;
using System.Text;
using Godot;
using DualOntologyWorldEngine.Chapter2.Prototype;
using ProtoGame = DualOntologyWorldEngine.Chapter2.Prototype.Game;

namespace DualOntologyWorldEngine.Chapter2;

/// <summary>
/// 第二章宿主桥：在后台线程上运行原型的文字循环（ConsoleUI + Game），宿主送命令、取输出。
/// 原型逻辑不改动；只有在原型等待输入时（IsWaiting）才读取世界状态，读与写不会并发。
/// </summary>
public partial class Chapter2Bridge : Node
{
    private sealed class QueueReader : TextReader
    {
        public readonly BlockingCollection<string> Lines = new();
        public volatile bool Waiting;
        public int Reads;
        public override string? ReadLine()
        {
            Interlocked.Increment(ref Reads);
            Waiting = true;
            try { return Lines.Take(); }
            catch (InvalidOperationException) { return null; }
            finally { Waiting = false; }
        }
    }

    private sealed class QueueWriter : TextWriter
    {
        public readonly ConcurrentQueue<string> Lines = new();
        private readonly StringBuilder _line = new();
        public override Encoding Encoding => Encoding.UTF8;
        public override void Write(char value)
        {
            if (value == '\n') { Lines.Enqueue(_line.ToString()); _line.Clear(); }
            else if (value != '\r') _line.Append(value);
        }
        public override void WriteLine(string? value) { _line.Append(value); Lines.Enqueue(_line.ToString()); _line.Clear(); }
    }

    private ProtoGame _g = null!;
    private ConsoleUI _ui = null!;
    private readonly QueueReader _in = new();
    private readonly QueueWriter _out = new();
    private Thread? _thread;
    private volatile bool _done;
    private int _submitted;
    private Place? _room;
    private readonly Godot.Collections.Array _storyPresentations = new();

    [Export] public string WorldScriptPath { get; set; } = "";

    public void Start()
    {
        _g = WorldScriptPath.Length == 0 ? Chapter2Script.CreateWorld() : WorldScript.Load(ProjectSettings.GlobalizePath(WorldScriptPath)).CreateGame();
        _g.CaptureFrames = true;
        _g.StartPresentation();
        _room = _g.Where(_g.Det);
        _ui = new ConsoleUI(_g, _in, _out, echo: false, roomScoped: true, spatialHost: true);
        _thread = new Thread(() =>
        {
            try { _ui.Run(); }
            catch (Exception e) { _out.WriteLine("（出错：" + e.Message + "）"); GD.PrintErr(e.ToString()); }
            finally { _done = true; }
        }) { IsBackground = true, Name = "Chapter2Game" };
        _thread.Start();
    }

    public void Send(string line)
    {
        Interlocked.Increment(ref _submitted);
        _in.Lines.Add(line);
    }

    /// <summary>原型停在输入处、命令都已取走：此时可安全读取状态。</summary>
    public bool IsWaiting() => _in.Waiting && _in.Lines.Count == 0 && Volatile.Read(ref _in.Reads) > Volatile.Read(ref _submitted);
    public bool IsDone() => _done;
    public string ScriptId() => Chapter2Script.Id;
    public string ScriptAssembly() => _g.GetType().Assembly.GetName().Name!;
    public string Mode() => _done ? "ended" : _ui.Mode;

    public bool SetMap(string map)
    {
        if (!IsWaiting() || Mode() != "main") return false;
        _room = Chapter2HostContent.MapPlaces.TryGetValue(map, out var place) ? place : null;
        _ui.HasRoom = _room is not null;
        if (_room is { } here)
        {
            if (_g.Story is null) _g.SetPlayerPlace(here);
            else if (_g.Where(_g.Det) != here) Send("go " + Names.Of(here));
        }
        return true;
    }

    public Godot.Collections.Array<string> TakeOutput()
    {
        var a = new Godot.Collections.Array<string>();
        while (_out.Lines.TryDequeue(out var s)) a.Add(s);
        return a;
    }

    public Godot.Collections.Dictionary Snapshot()
    {
        var d = Chapter2HostContent.Snapshot();
        if (!IsWaiting() && !_done) return d;
        d["jobs"] = new Godot.Collections.Array<string>(ConsoleUI.Jobs.Select(j => j.Name));
        d["global_actions"] = Actions(Chapter2HostContent.GlobalActions(_g));
        d["frames_revision"] = _g.PresentationRevision;
        if (_g.Story?.PendingChoice is { } choice)
        {
            var options = new Godot.Collections.Array();
            foreach (var option in choice.Options.Where(o => o.Available?.Invoke(_g) != false))
                options.Add(new Godot.Collections.Dictionary { ["id"] = option.Id, ["text"] = option.Text });
            d["story_choice"] = new Godot.Collections.Dictionary { ["id"] = choice.Id, ["prompt"] = choice.Prompt, ["options"] = options };
        }
        if (_g.Story is { } story)
            while (story.Presentations.TryDequeue(out var presentation))
                _storyPresentations.Add(new Godot.Collections.Dictionary
                {
                    ["kind"] = presentation.Kind.ToString(), ["content"] = presentation.Content,
                    ["actor"] = presentation.Actor?.ToString() ?? "",
                    ["sequence"] = presentation.Sequence,
                });
        d["story_presentations"] = _storyPresentations;
        d["day"] = _g.Day;
        d["slot"] = (int)_g.Slot;
        d["slot_name"] = Names.Of(_g.Slot);
        d["mode"] = Mode();
        d["card_cell"] = _ui.CardCell;
        d["det_place"] = _g.Where(_g.Det).ToString();
        if (_room is not null) _room = _g.Where(_g.Det);
        var room = RoomPresentation.For(_g, _room);
        var roomPeople = new Godot.Collections.Array();
        foreach (var p in room.People)
            roomPeople.Add(new Godot.Collections.Dictionary
            {
                ["id"] = p.Id.ToString(), ["name"] = p.Name, ["place"] = _g.Where(p).ToString(), ["asleep"] = p.Asleep,
                ["actions"] = Actions(RoomPresentation.PersonActions(_g, p.Id, _room)),
            });
        var roomThings = new Godot.Collections.Array();
        if (_g.DefinedScriptWorld is { } scriptWorld && _room is { } scriptPlace)
            foreach (var character in scriptWorld.Characters.Values.Where(c => c.SpriteFramesPath is not null &&
                         !_g.People.Any(p => ReferenceEquals(p.Body, c.Body)) &&
                         ReferenceEquals(c.Body.Get<DualOntologyWorldEngine.Core.SpatialProperty>()?.Space, _g.Space[scriptPlace])))
                roomPeople.Add(new Godot.Collections.Dictionary
                {
                    ["id"] = "script:" + character.Key, ["name"] = character.Body.TrueName,
                    ["place"] = scriptPlace.ToString(), ["asleep"] = false,
                    ["frames_path"] = character.SpriteFramesPath!, ["actions"] = new Godot.Collections.Array(),
                });
        foreach (var thing in room.Things)
            roomThings.Add(new Godot.Collections.Dictionary { ["key"] = thing, ["place"] = room.Place ?? "" });
        d["room"] = new Godot.Collections.Dictionary
        {
            ["place"] = room.Place ?? "", ["people"] = roomPeople, ["things"] = roomThings,
            ["board"] = room.Board, ["actions"] = Actions(room.Actions),
        };
        var frames = new Godot.Collections.Array();
        foreach (var frame in _g.PresentationFrames)
            frames.Add(new Godot.Collections.Dictionary
            {
                ["time"] = frame.Time, ["actor"] = frame.Actor ?? "", ["place"] = frame.Place,
                ["frame"] = frame.Frame, ["animation"] = Chapter2HostContent.Animation(frame.Frame),
                ["text"] = frame.Text, ["anonymous"] = frame.Anonymous,
                ["frames_path"] = frame.FramesPath ?? "",
            });
        d["frames"] = frames;
        var people = new Godot.Collections.Array();
        foreach (var p in _g.People)
            people.Add(new Godot.Collections.Dictionary { ["id"] = p.Id.ToString(), ["name"] = p.Name });
        d["people"] = people;
        var rules = new Godot.Collections.Array();
        foreach (var (label, s) in _g.PreviewKnownRules()) rules.Add(new Godot.Collections.Dictionary { ["label"] = label, ["text"] = s.Text });
        d["rules"] = rules;
        var news = new Godot.Collections.Array();
        var order = _ui.NewsOrder();
        for (var i = 0; i < order.Count; i++) news.Add(Chapter2Content.PlayerText($"{i + 1}. {_g.Det.News[order[i]].Text}"));
        d["news"] = news;
        var things = new Godot.Collections.Array();
        foreach (var kv in ProtoGame.Things_) things.Add(new Godot.Collections.Dictionary { ["key"] = kv.Key, ["place"] = kv.Value.Place.ToString() });
        d["things"] = things;
        var boards = new Godot.Collections.Array();
        foreach (var b in _g.Board.Keys) boards.Add(b.ToString());
        d["boards"] = boards;
        var places = new Godot.Collections.Dictionary();
        foreach (var pl in Enum.GetValues<Place>()) places[pl.ToString()] = Names.Of(pl);
        d["place_names"] = places;
        d["topics"] = new Godot.Collections.Array<string>(ProtoGame.Topics);
        d["claims"] = new Godot.Collections.Array<string>(ProtoGame.Claims);
        var ev = new Godot.Collections.Array();
        for (var i = 0; i < ProtoGame.EvidenceNames.Length; i++) ev.Add(new Godot.Collections.Dictionary { ["name"] = ProtoGame.EvidenceNames[i], ["have"] = _g.DetHasEvidence(i) });
        d["evidence"] = ev;
        d["cells"] = new Godot.Collections.Array
        {
            new Godot.Collections.Array<string>(Card.Subjects), new Godot.Collections.Array<string>(Card.Conds),
            new Godot.Collections.Array<string>(Card.Behaviors), new Godot.Collections.Array<string>(Card.Exceptions),
            new Godot.Collections.Array<string>(Card.Accounts),
        };
        if (Mode() == "council")
        {
            var plans = new Godot.Collections.Array<string>();
            foreach (var p in _g.ArchivePlans()) plans.Add($"{p.Text}（{(p.Author is { } a ? Names.Of(a) : "教师所立")}）");
            d["plans"] = plans;
        }
        d["can_apologize"] = _g.Manipulations.Any(m => m.Exposed && !m.Apologized);
        d["can_reopen"] = _g.Day >= 4 && !(_g.Day == 4 && _g.Slot == Slot.Morning) && !_g.QuerCleared && _g.Accused is not null;
        d["replay_night"] = _ui.ReplayNightNumber;
        d["replay_options"] = new Godot.Collections.Array<string>(_ui.ReplayOptions().Select(o => o.Text));
        var cards = new Godot.Collections.Array();
        foreach (var entry in InvestigationPresentation.Entries(_g))
            cards.Add(new Godot.Collections.Dictionary
            {
                ["id"] = entry.Id, ["name"] = Chapter2Content.PlayerText(entry.Name), ["place"] = entry.Place, ["kind"] = entry.Kind,
                ["facts"] = new Godot.Collections.Array<string>(entry.Facts.Select(Chapter2Content.PlayerText)),
                ["sources"] = new Godot.Collections.Array<string>(entry.Sources.Select(Chapter2Content.PlayerText)), ["image_key"] = entry.ImageKey,
            });
        d["notebook_cards"] = cards;
        var questions = new Godot.Collections.Array();
        foreach (var question in InvestigationPresentation.Questions(_g, _ui.ReplayNightNumber))
            questions.Add(new Godot.Collections.Dictionary
            {
                ["question"] = Chapter2Content.PlayerText(question.Question), ["context"] = Chapter2Content.PlayerText(question.Context),
                ["options"] = new Godot.Collections.Array<string>(question.Options.Select(Chapter2Content.PlayerText)),
                ["choices"] = new Godot.Collections.Array<int>(question.Choices),
            });
        d["replay_questions"] = questions;
        d["replay_unseen"] = new Godot.Collections.Array<string>(_g.UnreadTracePlaces(_ui.ReplayNightNumber).Select(Names.Of));
        if (_ui.LastReplay is { } replay)
            d["replay"] = new Godot.Collections.Dictionary
            {
                ["night"] = replay.Night,
                ["selected"] = new Godot.Collections.Array<string>(replay.Selected),
                ["lines"] = new Godot.Collections.Array<string>(replay.Lines.Select(Chapter2Content.PlayerText)),
                ["ink"] = new Godot.Collections.Array<string>(replay.Ink.Select(Chapter2Content.PlayerText)),
                ["confirmed"] = replay.Confirmed,
                ["summary"] = Chapter2Content.PlayerText(replay.Summary),
                ["frames"] = new Godot.Collections.Array(replay.Frames.Select(frame => (Variant)new Godot.Collections.Dictionary
                {
                    ["text"] = Chapter2Content.PlayerText(frame.Text), ["place"] = frame.Place, ["figure"] = frame.Figure,
                })),
            };
        return d;
    }

    private static Godot.Collections.Array Actions(IEnumerable<RoomAction> source)
    {
        var actions = new Godot.Collections.Array();
        foreach (var action in source)
            actions.Add(new Godot.Collections.Dictionary
            {
                ["label"] = action.Label, ["command"] = action.Command, ["kind"] = action.Kind,
            });
        return actions;
    }

    public override void _ExitTree()
    {
        _in.Lines.CompleteAdding();
    }
}
