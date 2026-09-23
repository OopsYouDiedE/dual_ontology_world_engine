using Godot;
using DualOntologyWorldEngine.Core;
namespace DualOntologyWorldEngine.Game.Reasoning;

public partial class TeaReasoningScene
{
    private PanelContainer _debugPanel = null!;
    private bool _teleport;
    private int _hoverPerson = -1;
    private Essence? _hoverSite;
    private void BuildDebugOptions()
    {
        _debugPanel = new PanelContainer { Name = "InvestigationDebug", Visible = false, Position = new(40,112), Size = new(360,170) };
        AddChild(_debugPanel);
        var box = new VBoxContainer(); _debugPanel.AddChild(box);
        LabelIn(box,"试玩设置 · F8",18);
        var guidance = new CheckButton { Name = "ShowGuidance", Text = "显示调查指引", ButtonPressed = !OS.GetCmdlineUserArgs().Contains("--tea-no-guidance") }; box.AddChild(guidance);
        _lead.Visible = guidance.ButtonPressed; guidance.Toggled += on => _lead.Visible = on;
        var teleport = new CheckButton { Name = "DebugTeleport", Text = "瞬移至交互点（调试）", ButtonPressed = OS.GetCmdlineUserArgs().Contains("--tea-debug-teleport") }; box.AddChild(teleport);
        _teleport = teleport.ButtonPressed; teleport.Toggled += on => { _teleport = on; _detective.CancelNavigation(); };
        ButtonIn(box,"关闭设置",() => _debugPanel.Visible = false);
    }
    private void ClickReality(Vector2 at)
    {
        if (_detective.Busy) { _log.Text = "正在完成当前动作，请稍候。"; return; }
        var person = PersonAt(at);
        if (person >= 0)
        {
            var actor = _actors[person];
            var approaches = new[] { new Vector2(0,32),new Vector2(-32,0),new Vector2(32,0),new Vector2(0,-32) }.Select(d => actor.Position+d);
            WalkToInteraction(approaches, () => TalkTo(person), actor.DisplayName);
            return;
        }
        var site = SiteAt(at);
        if (site is not null)
        {
            var p = _reality.Points[site];
            WalkToInteraction([new(p.X,p.Y)], () => { _near = site; InspectNearest(); }, site.TrueName);
            return;
        }
        var cell = new Vector2(Mathf.Round(at.X/16)*16,Mathf.Round(at.Y/16)*16);
        WalkToInteraction([cell], () => {}, "所选位置");
    }
    private int PersonAt(Vector2 at)
    {
        if (_actors.Count == 0) return -1;
        var target = _actors.Select((actor, index) => (actor, index, distance: (actor.Position - new Vector2(0, 20)).DistanceTo(at))).MinBy(p => p.distance);
        return target.distance <= 24 ? target.index : -1;
    }
    private Essence? SiteAt(Vector2 at)
    {
        // The small chair marker is deliberately outside the seated actor's body
        // and wins over the adjacent window hotspot.
        if (new Rect2(75, 165, 20, 20).HasPoint(at)) return _reality.Chair;
        foreach (var site in _reality.Sites.Where(s => s != _reality.Entrance))
            if (SiteHotspot(site).HasPoint(at)) return site;
        return null;
    }
    private Rect2 SiteHotspot(Essence site)
    {
        if (site == _reality.Cabinet) return new Rect2(505, 25, 78, 91);
        if (site == _reality.Chair) return new Rect2(72, 135, 82, 94);
        if (site == _reality.Work) return new Rect2(244, 145, 150, 106);
        if (site == _reality.Supply) return new Rect2(425, 74, 104, 94);
        if (site == _reality.Window) return new Rect2(69, 89, 89, 85);
        if (site == _reality.Sink) return new Rect2(482, 274, 92, 70);
        if (site == _reality.Basin) return new Rect2(147, 278, 58, 55);
        if (site == _reality.Tray) return new Rect2(370, 292, 62, 48);
        var p = _room.ObjectPoint(site); return new Rect2(p - new Vector2(28, 28), new Vector2(56, 56));
    }
    private void UpdateRealityHover(Vector2 at)
    {
        _hoverPerson = PersonAt(at);
        _hoverSite = _hoverPerson >= 0 ? null : SiteAt(at);
        _room.Selected = _hoverSite;
        _room.Refresh();
    }
    private void WalkToInteraction(IEnumerable<Vector2> candidates, Action interact, string name)
    {
        var from = ((int)Math.Round(_detective.Position.X/16)*16,(int)Math.Round(_detective.Position.Y/16)*16);
        List<(int X,int Y)>? best = null; Vector2 destination = default;
        foreach (var candidate in candidates)
        {
            var x = (int)Math.Round(candidate.X/16)*16; var y = (int)Math.Round(candidate.Y/16)*16;
            if (!TeaRoomGeometry.Walkable(x,y)) continue;
            var path = TeaRoomGeometry.Path(from,(x,y));
            if (path.Count == 0 && from != (x,y)) continue;
            if (best is null || path.Count < best.Count) { best = path; destination = new(x,y); }
        }
        if (best is null) { _log.Text = "暂时找不到前往" + name + "的路线。"; return; }
        _detective.CancelNavigation(); GetViewport().GuiReleaseFocus();
        if (_teleport) { _detective.Position = destination; _log.Text = "调试瞬移："+name; interact(); return; }
        _log.Text = "前往" + name + " · 到达后自动交互，方向键可取消";
        var points = best.Select(p => new Vector2(p.X,p.Y)).ToList();
        if (points.Count == 0) points.Add(destination);
        _detective.FollowPath(points,interact);
    }
}
