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
        public override string? ReadLine()
        {
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
    private volatile int _pending;

    public void Start()
    {
        _g = new ProtoGame();
        _ui = new ConsoleUI(_g, _in, _out, echo: false);
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
        Interlocked.Increment(ref _pending);
        _in.Lines.Add(line);
    }

    /// <summary>原型停在输入处、命令都已取走：此时可安全读取状态。</summary>
    public bool IsWaiting() => _in.Waiting && _in.Lines.Count == 0;
    public bool IsDone() => _done;
    public string Mode() => _done ? "ended" : _ui.Mode;

    public Godot.Collections.Array<string> TakeOutput()
    {
        var a = new Godot.Collections.Array<string>();
        while (_out.Lines.TryDequeue(out var s)) a.Add(s);
        return a;
    }

    public Godot.Collections.Dictionary Snapshot()
    {
        var d = new Godot.Collections.Dictionary();
        if (!IsWaiting() && !_done) return d;
        d["day"] = _g.Day;
        d["slot"] = (int)_g.Slot;
        d["slot_name"] = Names.Of(_g.Slot);
        d["mode"] = Mode();
        d["det_place"] = _g.Where(_g.Det).ToString();
        var people = new Godot.Collections.Array();
        foreach (var p in _g.People)
            people.Add(new Godot.Collections.Dictionary { ["id"] = p.Id.ToString(), ["name"] = p.Name, ["place"] = _g.Where(p).ToString(), ["asleep"] = p.Asleep });
        d["people"] = people;
        var rules = new Godot.Collections.Array();
        foreach (var s in _g.DetKnownSpecs()) rules.Add(new Godot.Collections.Dictionary { ["label"] = _g.LabelOf(s), ["text"] = s.Text });
        d["rules"] = rules;
        var news = new Godot.Collections.Array();
        var order = _ui.NewsOrder();
        for (var i = 0; i < order.Count; i++) news.Add($"{i + 1}. {_g.Det.News[order[i]].Text}");
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
            foreach (var p in _g.ArchivePlans()) plans.Add($"{p.Text}（{(p.Author is { } a ? Names.Of(a) : "恶魔所立")}）");
            d["plans"] = plans;
        }
        d["can_apologize"] = _g.Manipulations.Any(m => m.Exposed && !m.Apologized);
        d["can_reopen"] = _g.Day >= 4 && !(_g.Day == 4 && _g.Slot == Slot.Morning) && !_g.QuerCleared && _g.Accused is not null;
        return d;
    }

    public override void _ExitTree()
    {
        _in.Lines.CompleteAdding();
    }
}
