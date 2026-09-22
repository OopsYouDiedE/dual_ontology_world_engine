using Godot;

namespace DualOntologyWorldEngine.Game.Reasoning;

public partial class SpellScene : Control
{
	private UtteranceLab _lab = null!;
	private LabSpeaker _speaker = null!;
	private CodeEdit _editor = null!;
	private RuleHighlight _highlight = null!;
	private Label _identity = null!, _diagnosis = null!, _objective = null!, _clock = null!;
	private RichTextLabel _meaning = null!, _words = null!, _journal = null!;
	private SpellDrawing _drawing = null!;
	private SpinBox _speed = null!, _limit = null!;
	private Button _play = null!;
	private bool _loading, _running;
	private double _elapsed;
	public Action? ReturnToReasoning { get; set; }

	public override void _Ready()
	{
		GetWindow().MinSize = new(1100, 800); GetWindow().ContentScaleSize = new(1280, 800);
		GetWindow().Size = new(Math.Max(GetWindow().Size.X, 1280), Math.Max(GetWindow().Size.Y, 800));
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		var background = new ColorRect { Color = new("e9e7de"), MouseFilter = MouseFilterEnum.Ignore };
		background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(background);
		var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(margin);
		foreach (var side in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 16);
		var split = new HBoxContainer(); split.AddThemeConstantOverride("separation", 20); margin.AddChild(split);
		var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = .95f, CustomMinimumSize = new(480, 0) };
		var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.15f };
		split.AddChild(left); split.AddChild(right);
		LabelIn(left, "言灵手稿 / TOKI", 24);
		_identity = LabelIn(left, "", 16);
		LabelIn(left, "学园道本语 · 蓝色结构词 / 黑色已知词 / 红色未知词", 13);
		_highlight = new RuleHighlight { Utterance = true };
		_editor = new CodeEdit { Name = "UtteranceEditor", SyntaxHighlighter = _highlight, GuttersDrawLineNumbers = true,
			WrapMode = TextEdit.LineWrappingMode.Boundary, CustomMinimumSize = new(0, 195), SizeFlagsVertical = SizeFlags.ExpandFill };
		_editor.AddThemeFontSizeOverride("font_size", 18);
		_editor.AddThemeColorOverride("font_color", new("202830")); _editor.AddThemeColorOverride("caret_color", new("246bc4"));
		_editor.AddThemeColorOverride("line_number_color", new("81918d")); _editor.AddThemeColorOverride("selection_color", new("b9d7ee"));
		_editor.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new("faf9f4"), ContentMarginLeft = 10, ContentMarginTop = 12 });
		left.AddChild(_editor);
		_editor.TextChanged += () => { if (!_loading) { _speaker.Draft = _editor.Text; Validate(); } };
		_diagnosis = LabelIn(left, "", 13); _diagnosis.CustomMinimumSize = new(0, 35);
		var actions = new HBoxContainer(); left.AddChild(actions);
		ButtonIn(actions, "发声 · 2 行动值", () => Execute(() => _lab.Cast(_speaker, _editor.Text))).Name = "Cast";
		ButtonIn(actions, "试声 · 观察结果", () => Execute(_lab.StartTrial)).Name = "Trial";
		_meaning = RichIn(left, 66); _meaning.Name = "Meaning";
		LabelIn(left, "已掌握的词根（可滚动）", 14);
		_words = RichIn(left, 105); _words.Name = "Vocabulary";
		LabelIn(left, "一行一命令：对象 o 动作 e 事物。用空格分词，# 写批注。\n未覆盖的声音照常接纳；多句同时命中时，后句覆盖前句。\n每次发声替换自己有权控制的试验台约束。", 12);
		_clock = LabelIn(left, "", 14);
		var settings = new HBoxContainer(); left.AddChild(settings);
		settings.AddChild(new Label { Text = "截止行动值", Modulate = new("273339") });
		_limit = new SpinBox { MinValue = 1, MaxValue = 100000, Value = 60, Step = 1 }; settings.AddChild(_limit);
		settings.AddChild(new Label { Text = "步/秒", Modulate = new("273339") });
		_speed = new SpinBox { MinValue = 1, MaxValue = 20, Value = 3, Step = 1 }; settings.AddChild(_speed);
		var transport = new HBoxContainer(); left.AddChild(transport);
		_play = ButtonIn(transport, "播放", () => { _running = !_running; Refresh(); });
		ButtonIn(transport, "单步", () => { _running = false; _lab.Step(); });
		ButtonIn(transport, "重置课题", Reset);
		ButtonIn(transport, "返回推理", () => { if (ReturnToReasoning is { } back) { back(); QueueFree(); } else GetTree().ChangeSceneToFile("res://scenes/Reasoning/Reasoning.tscn"); });
		LabelIn(right, "只让该来的声音进来", 25);
		_objective = LabelIn(right, "", 15); _objective.CustomMinimumSize = new(0, 64);
		_drawing = new SpellDrawing { CustomMinimumSize = new(0, 265), SizeFlagsVertical = SizeFlags.ExpandFill };
		_drawing.Selected += speaker => { _speaker.Draft = _editor.Text; _speaker = speaker; LoadDraft(); Refresh(); };
		right.AddChild(_drawing);
		LabelIn(right, "点击人物选择施术者。描写决定作用范围，位格决定能否成立。", 13);
		LabelIn(right, "试验记录", 18);
		_journal = RichIn(right, 185); _journal.SizeFlagsVertical = SizeFlags.ExpandFill; _journal.ScrollFollowing = true;
		Reset();
	}

	private static Label LabelIn(BoxContainer parent, string text, int size)
	{
		var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		label.AddThemeColorOverride("font_color", new("273339")); label.AddThemeFontSizeOverride("font_size", size); parent.AddChild(label); return label;
	}
	private static RichTextLabel RichIn(BoxContainer parent, int height)
	{
		var label = new RichTextLabel { CustomMinimumSize = new(0, height), BbcodeEnabled = false };
		label.AddThemeColorOverride("default_color", new("273339")); label.AddThemeFontSizeOverride("normal_font_size", 14); parent.AddChild(label); return label;
	}
	private static Button ButtonIn(BoxContainer parent, string text, Action action)
	{
		var button = new Button { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		button.Pressed += action; parent.AddChild(button); return button;
	}
	private void Execute(Func<string?> action)
	{
		var error = action();
		if (error is not null) { _diagnosis.Text = error; _diagnosis.AddThemeColorOverride("font_color", new("c43a42")); return; }
		if (_limit.Value <= _lab.Now) _limit.Value = _lab.Now + 60;
		_running = true; Refresh();
	}
	private void Validate()
	{
		var parsed = UtteranceLanguage.Parse(_editor.Text, _lab.VocabularyStage);
		_diagnosis.Text = parsed.Errors.Count > 0 ? parsed.Errors[0] : parsed.Clauses.Count == 0 ? "尚未书写咒文。" : $"句法成立 · {parsed.Clauses.Count} 句；含义仍需试验。";
		_diagnosis.AddThemeColorOverride("font_color", new(parsed.Errors.Count > 0 ? "c43a42" : "406854"));
		_meaning.Text = parsed.Errors.Count > 0 ? "完成句法后显示当前描述的字面解释。" : UtteranceLanguage.Describe(parsed.Clauses);
	}
	private void LoadDraft() { _loading = true; _editor.Text = _speaker.Draft; _loading = false; Validate(); }
	private void Reset()
	{
		_lab?.Dispose(); _lab = new(); _speaker = _lab.Speakers[0]; _running = false; _elapsed = 0;
		_limit.Value = 60; _lab.Changed += Refresh;
		_highlight.Stage = 0; _highlight.ClearHighlightingCache(); LoadDraft(); Refresh();
	}
	public override void _Process(double delta)
	{
		if (!_running) return;
		_elapsed += delta; if (_elapsed < 1 / _speed.Value) return; _elapsed = 0;
		var next = _lab.Session.CTB.Pending.FirstOrDefault();
		if (next is null || next.Due > _limit.Value) { _running = false; Refresh(); return; }
		_lab.Step(); if (!_lab.Busy) _running = false; Refresh();
	}
	private void Refresh()
	{
		_identity.Text = $"{_speaker.Name} · 位格 {_speaker.Rank} / 词汇阶段 {_lab.VocabularyStage + 1}";
		if (_highlight.Stage != _lab.VocabularyStage) { _highlight.Stage = _lab.VocabularyStage; _highlight.ClearHighlightingCache(); Validate(); }
		_words.Text = string.Join("\n", UtteranceLanguage.Vocabulary(_lab.VocabularyStage).Select(w => $"{w.Word}  —  {w.Meaning}"));
		_clock.Text = $"行动值 {_lab.Now:0} · {(_lab.TrialRunning ? "试声中" : _lab.Busy ? "发声中" : "等待操作")}";
		_objective.Text = _lab.Solved ? "课题完成。厨房保留两个新来源，拒绝重复与旧声；交谈室保持原样。" : _lab.VocabularyStage switch
		{
			0 => "范围练习：仅让厨房排除声音，交谈室维持原样。器物以房间用途区分：moku 饮食，toki 交谈。",
			1 => "次序练习：保留本轮两个不同来源的首次声音，排除重复。nanpa wan 修饰声音时，序数按每个来源分别计算。",
			_ => "最终课题：再排除上轮留声。首次抵达不等于本轮发声。pi tenpo ni 锚定发声时间，sin 是此处的简写。"
		};
		_play.Text = _running ? "暂停" : "播放"; _journal.Text = string.Join("\n", _lab.Journal);
		_drawing.Lab = _lab; _drawing.Active = _speaker; _drawing.QueueRedraw();
	}
	public override void _ExitTree() => _lab?.Dispose();
}

public partial class SpellDrawing : Control
{
	public UtteranceLab? Lab { get; set; }
	public LabSpeaker? Active { get; set; }
	public event Action<LabSpeaker>? Selected;
	private readonly Dictionary<LabSpeaker, Vector2> _people = [];
	private static readonly Color Ink = new("405c60");
	private void Text(Vector2 at, string text, int size = 14) => DrawString(ThemeDB.FallbackFont, at, text, fontSize: size, modulate: Ink);
	public override void _Draw()
	{
		if (Lab is null) return;
		DrawRect(new Rect2(Vector2.Zero, Size), new Color("f5f3e9"));
		var origin = new Vector2(Size.X / 2, 42);
		DrawCircle(origin, 14, new("b78b4f")); Text(origin + new Vector2(-42, -23), "校准发声器");
		for (var i = 0; i < Lab.Receivers.Length; i++)
		{
			var receiver = Lab.Receivers[i]; var center = new Vector2(Size.X * (.27f + i * .46f), 126);
			DrawLine(origin + new Vector2(0, 16), center - new Vector2(0, 28), Ink, 2);
			DrawRect(new Rect2(center - new Vector2(71, 26), new(142, 52)), new Color(i == 0 ? "d8c39d" : "bfcfd0"));
			DrawRect(new Rect2(center - new Vector2(71, 26), new(142, 52)), Ink, false, 2);
			Text(center + new Vector2(-59, -3), receiver.Name);
			Text(center + new Vector2(-59, 17), i == 0 ? "tomo moku · 位格 1" : "tomo toki · 位格 2", 12);
			Text(center + new Vector2(-65, 48), $"接纳 {receiver.Accepted} / 排除 {receiver.Rejected}");
			if (Lab.TrialRunning) DrawCircle(center - new Vector2(0, 36), 5, new("397fa8"));
		}
		Text(new Vector2(16, 202), "最近：" + Lab.LastPulse, 13);
		_people.Clear();
		for (var i = 0; i < Lab.Speakers.Length; i++)
		{
			var speaker = Lab.Speakers[i]; var pos = new Vector2(Size.X * (.27f + i * .46f), Size.Y - 46);
			_people[speaker] = pos; DrawCircle(pos, 16, new(speaker == Active ? "427fa0" : "ab8666"));
			DrawCircle(pos - new Vector2(0, 21), 9, new("e0bc94"));
			Text(pos + new Vector2(-49, 34), speaker.Name + $" / {speaker.Rank}", 13);
		}
	}
	public override void _GuiInput(InputEvent input)
	{
		if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse) return;
		foreach (var (speaker, pos) in _people) if (mouse.Position.DistanceTo(pos) < 35) { Selected?.Invoke(speaker); AcceptEvent(); break; }
	}
}
