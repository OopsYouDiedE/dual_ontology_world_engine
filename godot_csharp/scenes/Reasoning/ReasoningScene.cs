using Godot;

namespace DualOntologyWorldEngine.Game.Reasoning;

public partial class ReasoningScene : Control
{
	private BreakfastCase _case = null!;
	private Investigator _selected = null!;
	private CodeEdit _editor = null!;
	private RuleHighlight _highlight = null!;
	private Label _person = null!, _vocabulary = null!, _diagnostics = null!, _clock = null!, _objective = null!;
	private RichTextLabel _journal = null!;
	private CaseDrawing _drawing = null!;
	private SpinBox _time = null!, _speed = null!;
	private Button _play = null!, _spell = null!;
	private bool _loading, _running;
	private double _elapsed;

	public override void _Ready()
	{
		GetWindow().MinSize = new Vector2I(1100, 800);
		GetWindow().ContentScaleSize = new Vector2I(1280, 800);
		GetWindow().Size = new Vector2I(Math.Max(GetWindow().Size.X, 1280), Math.Max(GetWindow().Size.Y, 800));
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		var background = new ColorRect { Color = new Color("e9e7de"), MouseFilter = MouseFilterEnum.Ignore };
		background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(background);
		var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 16);
		AddChild(margin);
		var split = new HBoxContainer(); split.AddThemeConstantOverride("separation", 20); margin.AddChild(split);
		var left = new VBoxContainer { CustomMinimumSize = new Vector2(370, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.85f };
		var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.2f };
		split.AddChild(left); split.AddChild(right);
		Label LabelIn(VBoxContainer parent, string text, int size = 15)
		{
			var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
			label.AddThemeColorOverride("font_color", new Color("273339")); label.AddThemeFontSizeOverride("font_size", size);
			parent.AddChild(label); return label;
		}
		LabelIn(left, "行动札记 / BREAKFAST", 24);
		var tabs = new HBoxContainer(); left.AddChild(tabs);
		AddButton(tabs, "脑内推理", () => _editor.GrabFocus());
		_spell = AddButton(tabs, "言灵实验 · 未开放", OpenSpellLab);
		_person = LabelIn(left, "", 18);
		LabelIn(left, "直接输入规则 · 蓝色关键词 / 黑色已知词 / 红色未知词", 13);
		_highlight = new RuleHighlight();
		_editor = new CodeEdit { CustomMinimumSize = new Vector2(0, 190), SizeFlagsVertical = SizeFlags.ExpandFill,
			SyntaxHighlighter = _highlight, GuttersDrawLineNumbers = true, WrapMode = TextEdit.LineWrappingMode.Boundary };
		_editor.AddThemeColorOverride("font_color", new Color("202830"));
		_editor.AddThemeColorOverride("caret_color", new Color("246bc4"));
		_editor.AddThemeColorOverride("line_number_color", new Color("81918d"));
		_editor.AddThemeColorOverride("selection_color", new Color("b9d7ee"));
		_editor.AddThemeFontSizeOverride("font_size", 18);
		_editor.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color("faf9f4"), ContentMarginLeft = 10, ContentMarginTop = 12 });
		left.AddChild(_editor);
		_editor.TextChanged += () => { if (!_loading) { SaveDraft(); Validate(); } };
		_diagnostics = LabelIn(left, "", 13);
		_diagnostics.CustomMinimumSize = new Vector2(0, 40);
		var execute = new HBoxContainer(); left.AddChild(execute);
		AddButton(execute, "执行此人物方案", RunSelected);
		AddButton(execute, "执行两人方案", RunBoth);
		_vocabulary = LabelIn(left, "", 13);
		_vocabulary.CustomMinimumSize = new Vector2(0, 65);
		LabelIn(left, "一行一个行动；可写：如果 已校时 那么 监听 25\n# 后为批注。执行期间可编辑下一次方案。", 13);
		left.AddChild(new HSeparator());
		_clock = LabelIn(left, "", 16);
		var controls = new HBoxContainer(); left.AddChild(controls);
		controls.AddChild(new Label { Text = "截止行动值", Modulate = new Color("273339") });
		_time = new SpinBox { MinValue = 1, MaxValue = 100000, Value = 60, Step = 1, CustomMinimumSize = new Vector2(95, 0) }; controls.AddChild(_time);
		controls.AddChild(new Label { Text = "步/秒", Modulate = new Color("273339") });
		_speed = new SpinBox { MinValue = 1, MaxValue = 20, Value = 3, Step = 1, CustomMinimumSize = new Vector2(75, 0) }; controls.AddChild(_speed);
		var transport = new HBoxContainer(); left.AddChild(transport);
		_play = AddButton(transport, "播放", () => { if (_time.Value <= _case.Now) _time.Value = _case.Now + 60; _running = !_running; Refresh(); });
		AddButton(transport, "单步", () => { _running = false; _case.Step(); Refresh(); });
		AddButton(transport, "重置实验", Reset);
		LabelIn(right, "大家都准时，早餐却迟到了", 25);
		_objective = LabelIn(right, "", 15);
		_drawing = new CaseDrawing { CustomMinimumSize = new Vector2(0, 270), SizeFlagsVertical = SizeFlags.ExpandFill };
		_drawing.Selected += Select; right.AddChild(_drawing);
		LabelIn(right, "点击场景中的人物切换方案。圆环表示正在监听。", 13);
		LabelIn(right, "观察与执行记录", 18);
		_journal = new RichTextLabel { CustomMinimumSize = new Vector2(0, 170), SizeFlagsVertical = SizeFlags.ExpandFill,
			ScrollFollowing = true, BbcodeEnabled = false };
		_journal.AddThemeColorOverride("default_color", new Color("273339"));
		_journal.AddThemeFontSizeOverride("normal_font_size", 14); right.AddChild(_journal);
		Reset();
	}

	private static Button AddButton(BoxContainer parent, string text, Action action)
	{
		var button = new Button { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		button.Pressed += action; parent.AddChild(button); return button;
	}
	private void SaveDraft()
	{
		if (_selected is null) return;
		_selected.Draft = _editor.Text;
	}
	private void Select(Investigator actor)
	{
		SaveDraft(); _selected = actor; LoadDraft(); Refresh();
	}
	private void LoadDraft()
	{
		_loading = true;
		_editor.Text = _selected.Draft;
		_loading = false; Validate();
	}
	private void OpenSpellLab()
	{
		if (_case.Stage < 4) return;
		SaveDraft(); _running = false; Hide();
		var lab = GD.Load<PackedScene>("res://scenes/Reasoning/Spellwork.tscn").Instantiate<SpellScene>();
		lab.ReturnToReasoning = () => { Show(); Refresh(); };
		GetParent().AddChild(lab);
	}
	private void Validate()
	{
		var parsed = RuleLanguage.Parse(_editor.Text, _case.Stage);
		_diagnostics.Text = parsed.Errors.Count == 0 ? $"语法通过 · {parsed.Program.Count} 条行动" : string.Join("\n", parsed.Errors.Take(2));
		_diagnostics.AddThemeColorOverride("font_color", new Color(parsed.Errors.Count == 0 ? "406854" : "c43a42"));
	}
	private void RunSelected()
	{
		SaveDraft(); var error = _case.Run(_selected, _editor.Text);
		if (error is not null) _diagnostics.Text = error;
		else StartPlayback();
	}
	private void RunBoth()
	{
		SaveDraft();
		// 先验证两份方案，避免其中一份错误时只提交了一半实验。
		var drafts = _case.People.Select(p => (Actor: p, Text: p.Draft)).ToArray();
		if (drafts.Any(d => d.Actor.Busy)) { _diagnostics.Text = "请等待当前人物方案完成。"; return; }
		foreach (var draft in drafts)
		{
			var parsed = RuleLanguage.Parse(draft.Text, _case.Stage);
			if (parsed.Errors.Count > 0 || parsed.Program.Count == 0) { _diagnostics.Text = draft.Actor.Name + "的方案为空或含有错误。"; return; }
		}
		foreach (var draft in drafts) _case.Run(draft.Actor, draft.Text);
		StartPlayback();
	}
	private void StartPlayback() { if (_time.Value <= _case.Now) _time.Value = _case.Now + 60; _running = true; Refresh(); }
	private void Reset()
	{
		_running = false; _elapsed = 0; _case?.Dispose(); _case = new BreakfastCase();
		_selected = _case.People[0]; _time.Value = 60;
		_case.Changed += Refresh; LoadDraft(); Refresh();
	}
	public override void _Process(double delta)
	{
		if (!_running) return;
		_elapsed += delta;
		if (_elapsed < 1 / _speed.Value) return;
		_elapsed = 0;
		var next = _case.Session.CTB.Pending.FirstOrDefault();
		if (next is null || next.Due > _time.Value || _case.Now >= _time.Value)
		{ _running = false; Refresh(); return; }
		_case.Step();
		if (!_case.Busy) _running = false;
		Refresh();
	}
	private void Refresh()
	{
		_person.Text = $"脑内方案 / {_selected.Name} · {_selected.Activity}";
		if (_highlight.Stage != _case.Stage) { _highlight.Stage = _case.Stage; _highlight.ClearHighlightingCache(); Validate(); }
		_vocabulary.Text = "已掌握词汇：" + string.Join(" · ", RuleLanguage.Vocabulary(_case.Stage));
		_clock.Text = $"行动值 {_case.Now:0}  /  接收 {_case.Accepted} 次";
		_play.Text = _running ? "暂停" : "播放";
		_spell.Disabled = _case.Stage < 4; _spell.Text = _case.Stage < 4 ? "言灵实验 · 未开放" : "言灵实验";
		_objective.Text = _case.Stage switch
		{
			0 => "先核对两人的时间依据。默认方案可以开始第一次共同调查。",
			1 => "时间记录出现了差异。检查现场，寻找可以操纵的实验条件。",
			2 => "分别在设备开启、关闭时收集两处记录。完整监听后，回到餐厅提交、比较。",
			3 => "换用可识别的声音检验解释。两人分别监听，记录后再次比较。",
			_ => "已建立可复核的传播解释。现在可以进入言灵实验。"
		};
		_journal.Text = string.Join("\n", _case.Journal);
		_drawing.Case = _case; _drawing.Active = _selected; _drawing.QueueRedraw();
	}
	public override void _ExitTree() => _case?.Dispose();
}

public partial class CaseDrawing : Control
{
	public BreakfastCase? Case { get; set; }
	public Investigator? Active { get; set; }
	public event Action<Investigator>? Selected;
	private readonly Dictionary<Investigator, Vector2> _positions = [];
	private static readonly Color Ink = new("405c60");
	private void Text(Vector2 at, string value, int size = 15) => DrawString(ThemeDB.FallbackFont, at, value, fontSize: size, modulate: Ink);
	public override void _Draw()
	{
		if (Case is null) return;
		var w = Size.X; var h = Size.Y;
		DrawRect(new Rect2(Vector2.Zero, Size), new Color("f5f3e9"));
		var rooms = new Dictionary<string, Rect2>
		{
			["走廊"] = new(12, 32, w * .42f, h * .62f - 32),
			["厨房"] = new(w * .46f, 32, w * .51f, h * .62f - 32),
			["餐厅"] = new(12, h * .67f, w - 24, h * .29f)
		};
		foreach (var (name, rect) in rooms) { DrawRect(rect, Ink, false, 2); Text(rect.Position + new Vector2(12, 25), name); }
		var cabinet = new Rect2(w * .70f, 73, w * .20f, 42);
		DrawRect(cabinet, new Color("d8c39d")); DrawRect(cabinet, Ink, false, 2);
		Text(cabinet.Position + new Vector2(4, 25), Case.Stage >= 1 ? "保温柜" : "设备", 13);
		DrawLine(new Vector2(w * .24f, 66), new Vector2(w * .8f, 66), Ink, 3);
		DrawLine(new Vector2(w * .8f, 66), cabinet.Position + new Vector2(cabinet.Size.X / 2, 0), Ink, 3);
		DrawCircle(new Vector2(w * .24f, 66), 8, new Color("c89d55"));
		if (Case.Stage >= 2) Text(new Vector2(w * .70f, 137), Case.CabinetOpen ? "阀门：开" : "阀门：关", 13);
		_positions.Clear();
		for (var i = 0; i < Case.People.Length; i++)
		{
			var actor = Case.People[i]; var rect = rooms[actor.Room];
			var position = rect.Position + new Vector2(rect.Size.X * (actor.Room == "餐厅" ? .35f + i * .32f : .32f + i * .35f), rect.Size.Y * .55f);
			_positions[actor] = position;
			DrawCircle(position, 17, actor == Active ? new Color("427fa0") : new Color("ab8666"));
			DrawCircle(position + new Vector2(0, -22), 9, new Color("e0bc94"));
			if (actor.Listening) DrawArc(position, 24, 0, Mathf.Tau, 32, new Color("6f9d85"), 2);
			Text(position + new Vector2(-25, 38), actor.Name, 14);
		}
	}
	public override void _GuiInput(InputEvent input)
	{
		if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse) return;
		foreach (var (actor, position) in _positions)
			if (mouse.Position.DistanceTo(position) <= 38) { Selected?.Invoke(actor); AcceptEvent(); break; }
	}
}
