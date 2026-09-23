using Godot;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Game.Reasoning;

public partial class TeaReasoningScene : Control
{
	private readonly TeaCase _reality = new(true);
	private TeaCase _simulation = new();
	private readonly string[] _drafts = ["", "", "", ""];
	private readonly HashSet<int> _evidence = [0];
	private TeaRoomView _room = null!;
	private ReasoningActor _detective = null!;
	private readonly List<ReasoningActor> _actors = [];
	private SubViewport _viewport = null!;
	private CodeEdit _editor = null!;
	private Label _mode = null!, _status = null!, _diagnostic = null!, _current = null!, _nearby = null!;
	private RichTextLabel _detail = null!, _log = null!;
	private VBoxContainer _rulePanel = null!;
	private OptionButton _people = null!, _materials = null!;
	private Button _mindButton = null!, _play = null!, _run = null!, _next = null!;
	private HSlider _timeline = null!;
	private PanelContainer _notebook = null!, _transport = null!, _help = null!, _organizer = null!, _comparison = null!;
	private SubViewportContainer _stage = null!;
	private bool _bookOpen, _organizerOpen, _comparisonOpen;
		private bool _mental, _playing, _loading, _dirty, _scrubbing;
	private int _person, _frame, _version;
	private double _elapsed;
	private Essence? _near;
		private readonly Font _font = new SystemFont { FontNames = ["Microsoft YaHei", "Noto Sans CJK SC"] };

	private Control _mindPage = null!, _realityHeader = null!, _modal = null!;
	private SubViewportContainer _mindStage = null!;
	private TeaRoomView _mindRoom = null!;
	private readonly List<ReasoningActor> _mindActors = [];
	private RichTextLabel _thoughts = null!;
	private Label _mindTitle = null!;
	private Button _knowledgeButton = null!;
	private bool _knowledgeOpen;

	public override void _Ready()
	{
		GetWindow().MinSize = new(1280, 900); GetWindow().Size = new(1360, 900);
		GetWindow().ContentScaleSize = Vector2I.Zero;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Theme = ReasoningTheme.Create(_font);
		var background = new ColorRect { Color = new("141b20"), MouseFilter = MouseFilterEnum.Ignore };
		AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_stage = new SubViewportContainer { Name = "RoomStage", Size = new(1280, 832), Stretch = true, StretchShrink = 2,
			MouseFilter = MouseFilterEnum.Stop, TextureFilter = TextureFilterEnum.Nearest };
		AddChild(_stage);
		_viewport = new SubViewport { Size = new(640, 416), World2D = new World2D(), RenderTargetUpdateMode = SubViewport.UpdateMode.Always, HandleInputLocally = false, TransparentBg = true };
		_stage.AddChild(_viewport);
		_room = new TeaRoomView { Name = "RealityRoom", Case = _reality, Frame = _reality.Frames[0], RoomFont = _font }; _viewport.AddChild(_room);
		var entrance = _reality.Points[_reality.Entrance];
		_detective = new ReasoningActor { Name = "Detective", IsDetective = true, PlayerControlled = true,
			Position = new(entrance.X, entrance.Y), CollisionLayer = 2, CollisionMask = 1 };
		_room.Sorted.AddChild(_detective); _detective.InteractRequested += InteractNearest;
		AddPeople(_room, _reality, _actors);
		_stage.GuiInput += OnRoomInput;

		_realityHeader = new Control { Name = "RealityHeader", MouseFilter = MouseFilterEnum.Ignore };
		AddChild(_realityHeader); _realityHeader.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		var headerFill = new ColorRect { Color = new("141b20"), MouseFilter = MouseFilterEnum.Ignore };
		_realityHeader.AddChild(headerFill); headerFill.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide); headerFill.OffsetBottom = 104;
		var title = new VBoxContainer { Position = new(40, 24), Size = new(700, 68), MouseFilter = MouseFilterEnum.Ignore }; _realityHeader.AddChild(title); title.AddThemeConstantOverride("separation", 4);
		LabelIn(title, "现场  /  公共研究室", 14).Modulate = new Color("a9c4b6");
		_mode = LabelIn(title, "下午茶还没开始，家具已经喝饱了", 22);
		var actions = new HBoxContainer(); _realityHeader.AddChild(actions);
		actions.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight); actions.OffsetLeft = -330; actions.OffsetRight = -40; actions.OffsetTop = 28;
		ButtonIn(actions, "札记", OpenNotebook, "NotebookButton");
		_mindButton = ButtonIn(actions, "进入脑内  Tab", ToggleMind, "MindButton"); _mindButton.ThemeTypeVariation = "PrimaryButton";

		_mindPage = new Control { Name = "MindWorkspace" }; AddChild(_mindPage); _mindPage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		var mindHeader = new HBoxContainer { Position = new(40, 24), Size = new(800, 68) }; _mindPage.AddChild(mindHeader);
		ButtonIn(mindHeader, "‹ 返回现场", ToggleMind, "ReturnReality");
		var mindHeading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; mindHeader.AddChild(mindHeading);
		_mindTitle = LabelIn(mindHeading, "脑内推演", 24);
		LabelIn(mindHeading, "假设中的过程 · 不会改变现实现场", 13).Modulate = new Color("9caea9");
		_mindStage = new SubViewportContainer { Name = "ReconstructionStage", Size = new(640, 416), TextureFilter = TextureFilterEnum.Nearest };
		_mindPage.AddChild(_mindStage);
		var mindViewport = new SubViewport { Size = new(640, 416), World2D = new World2D(), RenderTargetUpdateMode = SubViewport.UpdateMode.Always, TransparentBg = true };
		_mindStage.AddChild(mindViewport);
		_mindRoom = new TeaRoomView { Name = "ReconstructionRoom", Case = _simulation, Frame = _simulation.Frames[0], RoomFont = _font, Mental = true };
		mindViewport.AddChild(_mindRoom); AddPeople(_mindRoom, _simulation, _mindActors);
		_mindStage.GuiInput += OnRoomInput;

		var rules = new PanelContainer { Name = "BehaviorWorkspace" }; _mindPage.AddChild(rules);
		rules.SetAnchorsAndOffsetsPreset(LayoutPreset.RightWide); rules.OffsetLeft = -440; rules.OffsetRight = -32; rules.OffsetTop = 112; rules.OffsetBottom = -40;
		var right = new VBoxContainer(); rules.AddChild(right);
		LabelIn(right, "人物的行为假设", 14).Modulate = new Color("9caea9");
		_people = new OptionButton { Name = "PeoplePicker" }; foreach (var person in _reality.People) _people.AddItem(person.Name); right.AddChild(_people);
		_people.Select(_person);
		_people.ItemSelected += i => { SaveDraft(); _person = (int)i; LoadDraft(); RefreshPanel(); };
		_rulePanel = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; right.AddChild(_rulePanel);
		_editor = new CodeEdit { Name = "TeaEditor", CustomMinimumSize = new(0, 260), SizeFlagsVertical = SizeFlags.ExpandFill,
			GuttersDrawLineNumbers = false, WrapMode = TextEdit.LineWrappingMode.Boundary, PlaceholderText = "他会留意什么？\n又会怎样行动？\n\n写下条件与行动……" };
		_editor.AddThemeFontOverride("font", _font); _editor.AddThemeFontSizeOverride("font_size", 17); _rulePanel.AddChild(_editor);
		_editor.TextChanged += () => { if (!_loading) { SaveDraft(); _dirty = true; ValidateDrafts(); RefreshStatus(); } };
		_diagnostic = LabelIn(_rulePanel, "", 13);
		var tools = new HBoxContainer(); _rulePanel.AddChild(tools);
		ButtonIn(tools, "句式帮助", () => _help.Visible = true, "RuleHelp");
		ButtonIn(tools, "组织行为", OpenBehaviorOrganizer, "BehaviorOrganizer");
		_knowledgeButton = ButtonIn(tools, "展开当时所知", () => { _knowledgeOpen = !_knowledgeOpen; RefreshPanel(); });
		_thoughts = new RichTextLabel { Name = "SubjectKnowledge", CustomMinimumSize = new(0, 120), BbcodeEnabled = false }; _rulePanel.AddChild(_thoughts);
		_run = ButtonIn(right, "从头推演", StartSimulation, "RunTea"); _run.ThemeTypeVariation = "PrimaryButton";
		var evidenceTools = new HBoxContainer(); right.AddChild(evidenceTools);
		ButtonIn(evidenceTools, "调查札记", OpenNotebook);
		ButtonIn(evidenceTools, "证据对照", OpenComparison, "EvidenceComparison");

		_modal = new Control { Name = "ReadingOverlay" }; AddChild(_modal); _modal.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		var shade = new ColorRect { Color = new Color(0.04f, 0.07f, 0.08f, 0.78f), MouseFilter = MouseFilterEnum.Stop };
		_modal.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_notebook = PaperPanel("ThoughtNotebook"); _modal.AddChild(_notebook);
		_notebook.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
		_notebook.OffsetLeft = -360; _notebook.OffsetRight = 360; _notebook.OffsetTop = -260; _notebook.OffsetBottom = 260;
		var notes = new VBoxContainer(); _notebook.AddChild(notes);
		var noteHeading = new HBoxContainer(); notes.AddChild(noteHeading);
		_current = LabelIn(noteHeading, "调查札记", 24); _current.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		ButtonIn(noteHeading, "关闭", () => { _bookOpen = false; RefreshPanel(); }, "CloseNotebook");
		LabelIn(notes, "只收录已经取得的材料", 13).Modulate = new Color("64756f");
		_materials = new OptionButton { Name = "EvidencePicker" }; notes.AddChild(_materials); _materials.ItemSelected += _ => ShowMaterial();
		_detail = new RichTextLabel { Name = "CaseDetail", SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new(0, 320), BbcodeEnabled = false };
		_detail.AddThemeFontSizeOverride("normal_font_size", 18); notes.AddChild(_detail);

		var prompt = new VBoxContainer { Name = "ContextPrompt" }; AddChild(prompt); prompt.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
		prompt.OffsetLeft = 40; prompt.OffsetRight = 790; prompt.OffsetTop = -78; prompt.OffsetBottom = -24;
		_nearby = LabelIn(prompt, "方向键 / WASD 移动 · E 调查", 14);
		_log = new RichTextLabel { CustomMinimumSize = new(0, 34), BbcodeEnabled = false, ScrollFollowing = true }; prompt.AddChild(_log);
		_log.AddThemeFontSizeOverride("normal_font_size", 13); _log.Modulate = new Color("9caea9");
		_transport = new PanelContainer { Name = "ReplayControls" }; _mindPage.AddChild(_transport);
		_transport.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
		_transport.OffsetLeft = 40; _transport.OffsetRight = -472; _transport.OffsetTop = -214; _transport.OffsetBottom = -110;
		var playback = new VBoxContainer(); _transport.AddChild(playback);
		var controls = new HBoxContainer(); playback.AddChild(controls);
		_status = new Label { Name = "RunStatus", SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart }; _status.AddThemeFontSizeOverride("font_size", 13); controls.AddChild(_status);
		_play = ButtonIn(controls, "播放", () => { if (!_simulation.Started) StartSimulation(); else _playing = !_playing; RefreshStatus(); }, "PlayTea");
		_next = ButtonIn(controls, "下一变化", NextFrame, "StepTea");
		_timeline = new HSlider { Name = "TeaTimeline", SizeFlagsHorizontal = SizeFlags.ExpandFill, MinValue = 0, MaxValue = 0, Step = 1 }; playback.AddChild(_timeline);
		_timeline.ValueChanged += value => { if (_scrubbing) return; _playing = false; _frame = (int)value; DisplayFrame(); };

		_help = PaperPanel("RuleReference"); _modal.AddChild(_help); _help.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
		_help.OffsetLeft = -300; _help.OffsetRight = 300; _help.OffsetTop = -230; _help.OffsetBottom = 230;
		var help = new VBoxContainer(); _help.AddChild(help);
		LabelIn(help, "写下判断，让人物行动", 24);
		LabelIn(help, "传统规则：当 认为 缺口杯 有茶，则 前往 窗边。铃声条件要明写“听过 第一次茶铃”等。\n\n组织行为：任务在所选通知到来时执行一次；人物会自动走近、取放，当前任务做完后才会响下一声铃，不需要追赶隐藏行动值。\n\n“认为”只读取人物自己的真实观察或记忆；不知道并不等于否。自由文本仍可直接编辑。", 17);
		ButtonIn(help, "插入句式", () => { _editor.InsertTextAtCaret("\n当 总是，则 查看 窗边"); _help.Visible = false; _editor.GrabFocus(); });
		ButtonIn(help, "关闭帮助", () => _help.Visible = false); _help.Visible = false;
		BuildReasoningTools();
		BuildInvestigation();
		MoveChild(_modal, GetChildCount() - 1);
		Resized += LayoutStage; LayoutStage();
		LoadDraft(); RefreshMaterials(); RefreshPanel(); RefreshStatus();
	}
	private void AddPeople(TeaRoomView room, TeaCase state, List<ReasoningActor> actors)
	{
		foreach (var person in state.People)
		{
			var actor = new ReasoningActor { Name = "Actor" + actors.Count, DisplayName = person.Name, IsDetective = false,
				PlayerControlled = false, Position = new(person.Position.X, person.Position.Y), CollisionLayer = 2, CollisionMask = 1 };
			room.Sorted.AddChild(actor); actors.Add(actor);
		}
	}
	private void LayoutStage()
	{
		_stage.Position = new(Mathf.Floor((Size.X - 1280) / 2), Mathf.Floor((Size.Y - 832) / 2));
		if (_mindStage is not null) _mindStage.Position = new(Mathf.Floor((Size.X - 472 - 640) / 2), Mathf.Floor((Size.Y - 416) / 2) - 45);
	}
	private PanelContainer PaperPanel(string name) => new() { Name = name, Theme = ReasoningTheme.Create(_font, true) };

	private Label LabelIn(Container parent, string text, int size)
	{
		var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
		label.AddThemeFontSizeOverride("font_size", size); parent.AddChild(label); return label;
	}
	private static Button ButtonIn(Container parent, string text, Action action, string? name = null)
	{
		var button = new Button { Text = text, Name = name ?? text, FocusMode = FocusModeEnum.All }; button.Pressed += action; parent.AddChild(button); return button;
	}
	private void OpenNotebook() { _detective.CancelNavigation(); CloseConversation(); GetViewport().GuiReleaseFocus(); _organizerOpen = false; _comparisonOpen = false; _help.Visible = false; _bookOpen = true; RefreshPanel(); }
	private void SaveDraft() { if (_editor is not null) _drafts[_person] = _editor.Text; }
	private void LoadDraft() { _loading = true; _editor.Text = _drafts[_person]; _loading = false; ValidateDrafts(); }
	private void ValidateDrafts()
	{
		var validation = new TeaCase().Configure(_drafts);
		_diagnostic.Visible = validation.Count > 0;
		_diagnostic.Text = validation.Count == 0 ? "" : validation[0];
		_diagnostic.AddThemeColorOverride("font_color", validation.Count == 0 ? new Color("5e654f") : new Color("9e4938"));
	}
	private void ToggleMind()
	{
		_detective.CancelNavigation(); CloseConversation(); SaveDraft(); _mental = !_mental; _playing = false; _detective.StopMotion();
		_detective.Visible = !_mental; _detective.InputEnabled = !_mental;
		_stage.Visible = !_mental; _realityHeader.Visible = !_mental; _mindPage.Visible = _mental;
		_bookOpen = false; _help.Visible = false; _organizerOpen = false; _comparisonOpen = false; DisplayFrame(); RefreshPanel();
	}
	private void RefreshMaterials()
	{
		_materials.Clear(); foreach (var index in _evidence.Order()) _materials.AddItem(TeaCase.Evidence[index].Title, index);
	}
	private void ShowMaterial()
	{
		if (_materials.ItemCount == 0) return;
		var e = TeaCase.Evidence[_materials.GetSelectedId()]; _detail.Text = e.Text + "\n\n来源：" + e.Source;
	}
	private void RefreshPanel()
	{
		_notebook.Visible = _bookOpen;
		_organizer.Visible = _organizerOpen;
		_comparison.Visible = _comparisonOpen;
		if (_comparisonOpen) RefreshComparison();
		_thoughts.Visible = _knowledgeOpen;
		_knowledgeButton.Text = _knowledgeOpen ? "收起当时所知" : "展开当时所知";
		_mindPage.Visible = _mental;
		if (_bookOpen) ShowMaterial();
		if (_knowledgeOpen)
		{
			var frame = _simulation.Frames[Math.Clamp(_frame, 0, _simulation.Frames.Count - 1)];
			_thoughts.Text = "本次假设中的认知 · 只读\n\n" + frame.Thoughts[_simulation.People[_person]];
		}
	}
	public void StartSimulation()
	{
		SaveDraft(); var next = new TeaCase(); var errors = next.Configure(_drafts);
		if (errors.Count > 0) { _diagnostic.Text = string.Join("\n", errors.Take(3)); _playing = false; _status.Text = "未提交：保留原重演，四人方案均未执行。"; return; }
		_simulation = next; _frame = 0; _version++; _dirty = false; ValidateDrafts();
		if (!_mental) ToggleMind();
		_simulation.Start(); _playing = true; _elapsed = 0; DisplayFrame();
	}
	public void NextFrame()
	{
		_playing = false;
		if (!_simulation.Started) { StartSimulation(); _playing = false; }
		Advance();
	}
	private void Advance()
	{
		if (_frame < _simulation.Frames.Count - 1) _frame++;
		else if (_simulation.Step()) _frame = _simulation.Frames.Count - 1;
		else _playing = false;
		DisplayFrame();
	}
	private void DisplayFrame()
	{
		var c = _mental ? _simulation : _reality;
		var f = c.Frames[_mental ? Math.Clamp(_frame, 0, c.Frames.Count - 1) : 0];
		var room = _mental ? _mindRoom : _room;
		var actors = _mental ? _mindActors : _actors;
		room.Case = c; room.Frame = f; room.Selected = null; room.Refresh();
		for (var i = 0; i < actors.Count; i++)
		{
			var p = f.Positions[c.People[i]]; var actor = actors[i];
			if (_mental && _playing) actor.PresentMotion(actor.Position, new(p.X, p.Y));
			else { actor.Position = new(p.X, p.Y); actor.StopMotion(); }
			if (!_mental || _frame == 0) actor.Face(Vector2.Down);
			if (_mental && _playing && f.Event.StartsWith(c.People[i].Name + "：") && !f.Event.Contains("前往")) actor.PlayGesture("interact");
		}
		_scrubbing = true; _timeline.MaxValue = Math.Max(0, _simulation.Frames.Count - 1); _timeline.Value = _frame; _scrubbing = false;
		_log.Text = _mental ? f.Event + (f.Differences.Any(IsKnownDifference) ? "\n记录差异：" + f.Differences.Last(IsKnownDifference) : "") : "走近人物按 E 交谈；靠近器物按 F 检查。";
		RefreshStatus(); RefreshPanel();
	}
	private bool IsKnownDifference(string message) => message.StartsWith("第一次") ? _evidence.Contains(7) : message.StartsWith("第二次") ? _evidence.Contains(8) : message.StartsWith("第三次") ? _evidence.Contains(9) : message.StartsWith("医生陈述") ? _evidence.Contains(12) : message.StartsWith("画家陈述") ? _evidence.Contains(13) : message.StartsWith("椅面痕迹") ? _evidence.Contains(6) : message.StartsWith("锁柜时") ? _evidence.Contains(9) : message.Contains("温瓶盆") ? _evidence.Contains(4) : message.Contains("染纸盘") ? _evidence.Contains(5) : message.Contains("茶壶") ? _evidence.Contains(19) : message.Contains("椅面") ? _evidence.Contains(6) : message.Contains("缺口杯") ? _evidence.Contains(18) : message.Contains("叶纹杯") ? _evidence.Contains(17) : message.Contains("锁柜") ? _evidence.Contains(9) : false;

	private void RefreshStatus()
	{
		if (_status is null) return;
		_transport.Visible = _mental; _run.Disabled = !_mental; _play.Disabled = !_mental; _next.Disabled = !_mental; _timeline.Editable = _mental;
		var frame = _simulation.Frames[Math.Clamp(_frame, 0, _simulation.Frames.Count - 1)];
		_mindTitle.Text = _mental ? $"脑内重演 · 思路{Math.Max(1, _version)}" + (_dirty ? " · 草稿已修改" : "") : "公共研究室";
		_play.Text = _playing ? "暂停" : "播放";
		foreach (var actor in _mindActors) { if (_mental && !_playing) actor.Sprite.Pause(); else actor.Sprite.Play(); }
		var missing = MissingCriticalEvidence();
		var replayState = _simulation.Finished && _frame == _simulation.Frames.Count - 1 ? "重演结束" : "当前回放";
		_status.Text = !_mental ? "侦探：移动、调查、使用脑内重演技能" : replayState + " · " +
			(frame.Differences.Any(IsKnownDifference) ? $"{frame.Differences.Count(IsKnownDifference)}项已检查记录存在差异" : _simulation.Finished && _frame == _simulation.Frames.Count - 1 && missing.Count == 0 ? "与全部已调查关键证据相容" : _simulation.Finished && _frame == _simulation.Frames.Count - 1 ? $"截至当前已检查内容相容；还有{missing.Count}项材料未检查" : "截至当前尚未完成检查") + (_dirty ? " · 回看旧版本" : "");
	}
	public override void _Process(double delta)
	{
		if (_detective is null) return;
		_modal.Visible = _bookOpen || _help.Visible || _organizerOpen || _comparisonOpen;
		_detective.InputEnabled = !_mental && GetViewport().GuiGetFocusOwner() is not TextEdit and not LineEdit && !_bookOpen && !_help.Visible && !_organizerOpen && !_comparisonOpen && _conversationPerson < 0;
		if (_mental)
		{
			_nearby.Text = "重演中的变化";
			if (_playing && !_bookOpen && !_help.Visible && !_organizerOpen && !_comparisonOpen) { _elapsed += delta; if (_elapsed >= 0.08) { _elapsed = 0; AdvancePlaybackBatch(); } }
		}
		else
		{
			_near = _reality.Sites.OrderBy(s => _detective.Position.DistanceTo(new(_reality.Points[s].X, _reality.Points[s].Y))).First();
			var point = _reality.Points[_near]; var close = _detective.Position.DistanceTo(new(point.X, point.Y)) <= 38;
			var nearbyPerson = NearbyPerson();
			_room.Selected = _hoverSite ?? (close ? _near : null); _room.Refresh();
			_nearby.Text = _conversationPerson >= 0 ? "交谈中 · Esc 结束" : _hoverPerson >= 0 ? $"点击与{_actors[_hoverPerson].DisplayName}交谈" : _hoverSite is not null ? $"点击检查{_hoverSite.TrueName}" : nearbyPerson >= 0 ? $"E  与{_actors[nearbyPerson].DisplayName}交谈" + (close ? $"   ·   F  检查{_near.TrueName}" : "") : close ? $"E / F  检查{_near.TrueName}" : "方向键 / WASD 移动 · E 交谈 · F 检查";
			RefreshLead();
			
		}
	}
	private void InspectNearest()
	{
		if (_mental || _detective.Busy || _bookOpen || _help.Visible || _conversationPerson >= 0) return;
		_near ??= _reality.Sites.Where(s => s != _reality.Entrance).OrderBy(s => _detective.Position.DistanceTo(new(_reality.Points[s].X, _reality.Points[s].Y))).FirstOrDefault();
		if (_near is null) return;
		var site = _near; var point = _reality.Points[site];
		if (_detective.Position.DistanceTo(new(point.X, point.Y)) > 38) return;
		_detective.Face(new Vector2(point.X, point.Y) - _detective.Position);
		var revision = _conversationRevision;
		_detective.PlayGesture("inspect", () =>
		{
			if (_mental || revision != _conversationRevision) return;
			int[] found = site == _reality.Window ? [3, 17] : site == _reality.Supply ? [2, 19] : site == _reality.Basin ? [4] : site == _reality.Tray ? [5] : site == _reality.Chair ? [6] : site == _reality.Cabinet ? [16] : site == _reality.Work ? [1] : site == _reality.Sink ? [18] : [0];
			_inspected.Add(site);
			if (_inquiry == "opening") _inquiry = site == _reality.Basin || site == _reality.Tray ? "tea" : site == _reality.Chair ? "chair" : site == _reality.Cabinet ? "cabinet" : "cups";
			foreach (var i in found) _evidence.Add(i);
			RefreshMaterials(); _materials.Select(Array.IndexOf(_evidence.Order().ToArray(), found[0])); _bookOpen = true; RefreshPanel(); RefreshStatus();
			_log.Text = "调查了" + site.TrueName + "；材料已保留，可随时进入脑内重演。";
		});
	}
	private void OnRoomInput(InputEvent e)
	{
		if (e is InputEventMouseMotion motion && !_mental) { UpdateRealityHover(motion.Position / 2); return; }
		if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse) return;
		GetViewport().GuiReleaseFocus();
		var at = mouse.Position / (_mental ? 1 : 2);
		if (!_mental)
		{
			if (_bookOpen || _help.Visible || _conversationPerson >= 0) return;
			ClickReality(at);
			return;
		}
		if (_mental)
		{
			var nearest = _mindActors.Select((a, i) => (Actor: a, Index: i)).MinBy(a => a.Actor.Position.DistanceTo(at));
			if (nearest.Actor.Position.DistanceTo(at) < 35) { SaveDraft(); _person = nearest.Index; _people.Select(_person); LoadDraft(); RefreshPanel(); }
		}
	}
	public override void _ShortcutInput(InputEvent e)
	{
		if (e is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Tab }) return;
		if (GetViewport().GuiGetFocusOwner() is TextEdit or LineEdit) return;
		ToggleMind();
		GetViewport().SetInputAsHandled();
	}
	public override void _UnhandledKeyInput(InputEvent e)
	{
		if (e is not InputEventKey { Pressed: true, Echo: false } key) return;
		if (key.Keycode == Key.Escape && GetViewport().GuiGetFocusOwner() is TextEdit or LineEdit) { GetViewport().GuiReleaseFocus(); GetViewport().SetInputAsHandled(); return; }
		if (GetViewport().GuiGetFocusOwner() is TextEdit or LineEdit) return;
		if (key.Keycode == Key.F8) { _debugPanel.Visible = !_debugPanel.Visible; GetViewport().SetInputAsHandled(); }
		else if (key.Keycode == Key.F && !_mental) { InspectNearest(); GetViewport().SetInputAsHandled(); }
		else if (key.Keycode == Key.Escape) { if (_conversationPerson >= 0) CloseConversation(); else if (_organizerOpen) CloseBehaviorOrganizer(); else if (_comparisonOpen) CloseComparison(); else if (_help.Visible) _help.Visible = false; else if (_bookOpen) { _bookOpen = false; RefreshPanel(); } else if (_mental) ToggleMind(); GetViewport().SetInputAsHandled(); }
	}
}
