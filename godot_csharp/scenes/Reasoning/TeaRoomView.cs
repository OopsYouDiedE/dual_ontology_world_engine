using Godot;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Game.Reasoning;

public partial class TeaRoomView : Node2D
{
    public TeaCase Case { get; set; } = null!;
    public TeaFrame Frame { get; set; } = null!;
    public bool Mental { get; set; }
    public Essence? Selected { get; set; }
    public Font RoomFont { get; set; } = null!;
    public Node2D Sorted { get; private set; } = null!;
    private Node2D _layout = null!;
    private TeaObjectsDrawing _objects = null!;
    private Line2D _chairMarker = null!;
    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        _layout = GD.Load<PackedScene>("res://scenes/Reasoning/StudyRoom.tscn").Instantiate<Node2D>(); AddChild(_layout);
        Sorted = _layout.GetNode<Node2D>("Furnishings");
        foreach (var r in TeaRoomGeometry.Obstacles) AddCollision(new(r.X, r.Y, r.W, r.H));
        foreach (var r in new[] { new Rect2(0, 0, 640, 96), new Rect2(0, 0, 32, 416), new Rect2(608, 0, 32, 416), new Rect2(0, 352, 288, 64), new Rect2(352, 352, 288, 64), new Rect2(288, 408, 64, 8) }) AddCollision(r);
        _objects = new TeaObjectsDrawing { Host = this, YSortEnabled = true }; Sorted.AddChild(_objects);
        // A parent draw call is covered by the room's TileMaps. Keep this affordance above them.
        _chairMarker = new Line2D
        {
            Position = ObjectPoint(Case.Chair) + new Vector2(-27, -19),
            Points = [new(0, -5), new(5, 0), new(0, 5), new(-5, 0), new(0, -5)],
            Width = 1.5f, DefaultColor = new Color("d8c17f"), ZIndex = 20, Visible = !Mental
        };
        AddChild(_chairMarker);
    }
    private void AddCollision(Rect2 rect)
    {
        var body = new StaticBody2D { Position = rect.Position + rect.Size / 2, CollisionLayer = 1, CollisionMask = 0 };
        body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = rect.Size } }); AddChild(body);
    }
    public void Refresh()
    {
        _layout.Modulate = Mental ? new Color("c7c9d2") : Colors.White;
        _chairMarker.Visible = !Mental;
        QueueRedraw(); _objects.Refresh();
    }
    public Vector2 ObjectPoint(Essence e)
    {
        if (Case.Points.TryGetValue(e, out var pos))
        {
            if (e == Case.Window) return new(110, 119);
            if (e == Case.Work) return new(317, 190);
            if (e == Case.Supply) return new(475, 106);
            if (e == Case.Cabinet) return new(544, 57);
            if (e == Case.Chair) return new(111, 193);
            if (e == Case.Sink) return new(528, 310);
            if (e == Case.Basin) return new(176, 310);
            if (e == Case.Tray) return new(400, 327);
            return new(pos.X, pos.Y);
        }
        var person = Case.People.FirstOrDefault(p => p.Body == e);
        if (person is not null) { var p = Frame.Positions[person]; return new(p.X + 9, p.Y - 19); }
        return Frame.Objects.TryGetValue(e, out var item) && item.Support is { } support ? ObjectPoint(support) : new(320, 384);
    }
    public override void _Draw()
    {
        if (Selected is not null && Case.Points.TryGetValue(Selected, out var p))
        {
            DrawArc(new(p.X, p.Y - 3), 12, 0.3f, 2.8f, 16, new Color("dcc48b"), 1);
            DrawArc(new(p.X, p.Y - 3), 12, 3.45f, 5.9f, 16, new Color("dcc48b"), 1);
        }
    }
}

public partial class TeaObjectsDrawing : Node2D
{
    public TeaRoomView Host { get; set; } = null!;
    private Texture2D _cup = null!, _plate = null!, _pot = null!, _cloth = null!;
    private TeaCase? _case;
    private readonly Dictionary<Essence, TeaPropDrawing> _props = [];
    public override void _Ready()
    {
        var sheet = GD.Load<Texture2D>("res://ShareAsset/Victorian/source/Victorian_Inside_C.png");
        Texture2D Region(Rect2 region) => new AtlasTexture { Atlas = sheet, Region = region };
        _cup = Region(new(168, 100, 20, 21)); _plate = Region(new(7, 129, 22, 19));
        _pot = Region(new(32, 192, 32, 32)); _cloth = Region(new(64, 320, 32, 32));
        Refresh();
    }
    private float Depth(Essence e)
    {
        var c = Host.Case;
        if (e == c.Window || e == c.Supply) return 145;
        if (e == c.Chair) return 209;
        if (e == c.Work) return 241;
        if (e == c.Cabinet) return 97;
        if (e == c.Basin || e == c.Tray || e == c.Sink) return 353;
        var actor = c.People.FirstOrDefault(p => p.Body == e);
        if (actor is not null) return Host.Frame.Positions[actor].Y + 1;
        return Host.Frame.Objects.TryGetValue(e, out var state) && state.Support is { } support ? Depth(support) + 0.01f : 0;
    }
    public void Refresh()
    {
        if (_cup is null || Host.Frame is null) return;
        var c = Host.Case; var f = Host.Frame;
        if (_case != c)
        {
            foreach (var prop in _props.Values) { RemoveChild(prop); prop.QueueFree(); }
            _props.Clear(); _case = c;
            foreach (var e in new[] { c.Chair, c.Basin, c.Tray, c.Cloth, c.Saucer, c.Pot, c.ChipCup, c.LeafCup, c.PlainCup })
            {
                var prop = new TeaPropDrawing { Texture = e == c.Chair ? null : e == c.Cloth ? _cloth : e == c.Saucer || e == c.Basin || e == c.Tray ? _plate : e == c.Pot ? _pot : _cup };
                _props.Add(e, prop); AddChild(prop);
            }
        }
        foreach (var (item, prop) in _props)
        {
            var s = f.Objects[item]; var at = Host.ObjectPoint(item); var inside = false;
            for (var parent = s.Support; parent is not null && f.Objects.TryGetValue(parent, out var ps); parent = ps.Support)
                if (parent == c.Cabinet) { inside = true; break; }
            prop.Visible = !inside || !f.Objects[c.Cabinet].Locked;
            if (item == c.Pot) at += new Vector2(15, -3);
            if (s.Support == c.Supply && item != c.Pot) at += new Vector2(-10, 0);
            prop.Position = new(0, Depth(item)); prop.Offset = at - prop.Position;
            prop.Wet = s.Tea > 0 && item != c.Pot;
            prop.Bowl = item == c.Chair || item == c.Basin || item == c.Tray;
            prop.Mark = item == c.ChipCup ? 1 : item == c.LeafCup ? 2 : 0;
            prop.QueueRedraw();
        }
        QueueRedraw();
    }
    public override void _Draw()
    {
        if (Host.Selected is not null && Host.Case.Points.TryGetValue(Host.Selected, out var p))
            DrawArc(new(p.X, p.Y - 3), 12, 0.3f, 2.8f, 16, new Color("dcc48b"), 1);
    }
}

public partial class TeaPropDrawing : Node2D
{
    public Texture2D? Texture { get; set; }
    public Vector2 Offset { get; set; }
    public bool Wet { get; set; }
    public bool Bowl { get; set; }
    public int Mark { get; set; }
    public override void _Draw()
    {
        if (Texture is not null) DrawTexture(Texture, Offset - Texture.GetSize() / 2);
        if (Wet && Bowl) DrawCircle(Offset, Texture is null ? 7 : 4, new Color(0.40f, 0.22f, 0.08f, 0.7f));
        else if (Wet) DrawLine(Offset + new Vector2(-3, -3), Offset + new Vector2(3, -3), new Color("875627"), 2);
        if (Mark == 1) DrawLine(Offset + new Vector2(-4, -4), Offset + new Vector2(-3, -2), new Color("595746"), 1);
        if (Mark == 2) DrawLine(Offset + new Vector2(-2, 1), Offset + new Vector2(0, 3), new Color("547b41"), 1);
    }
}
