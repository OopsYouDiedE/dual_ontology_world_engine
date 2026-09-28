using Godot;

namespace DualOntologyWorldEngine.Game.Reasoning;

public partial class TeaReasoningScene
{
    private PanelContainer _manuscript = null!;
    private ColorRect _replayMat = null!;
    private HBoxContainer _replayCaption = null!;
    private VBoxContainer _wordShelf = null!;
    private Label _replayTime = null!, _draftCaption = null!, _timelineStart = null!, _timelineEnd = null!;
    private Control _replayHeader = null!, _contextPrompt = null!;
    private bool _draftValid = true;

    // The manuscript is a presentation of the existing drafts and simulation, not a second state model.
    private void BuildReplayManuscript(PanelContainer paper, VBoxContainer right, HBoxContainer tools,
        HBoxContainer evidenceTools, HBoxContainer header, VBoxContainer heading, VBoxContainer prompt,
        VBoxContainer playback, HBoxContainer controls)
    {
        Font writing = new SystemFont { FontNames = ["KaiTi", "Microsoft YaHei", "Noto Serif CJK SC"] };
        var serif = new SystemFont { FontNames = ["Noto Serif CJK SC", "SimSun", "Songti SC"], Fallbacks = [writing] };
        _mindPage.Theme = ReasoningTheme.Create(serif);
        _manuscript = paper; _replayHeader = header; _contextPrompt = prompt;
        paper.Theme = ReasoningTheme.Create(serif, true);
        paper.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = new("e5e1d5"), ContentMarginLeft = 32, ContentMarginRight = 32,
            ContentMarginTop = 26, ContentMarginBottom = 26 });
        ((Label)right.GetChild(0)).Text = "行为假设  /  点击场景中的人物，或在此切换";
        ((Label)right.GetChild(0)).Modulate = Colors.White;
        ((Label)right.GetChild(0)).AddThemeColorOverride("font_color", new Color("68716a"));
        _people.AddThemeFontSizeOverride("font_size", 32);
        _people.AddThemeFontOverride("font", writing);
        _people.AddThemeStyleboxOverride("normal", new StyleBoxEmpty { ContentMarginBottom = 14 });
        _editor.AddThemeFontOverride("font", writing);
        _editor.AddThemeFontSizeOverride("font_size", 25);
        _editor.AddThemeConstantOverride("line_spacing", 12);
        _editor.AddThemeStyleboxOverride("normal", new StyleBoxEmpty { ContentMarginTop = 18, ContentMarginBottom = 12 });
        _editor.AddThemeStyleboxOverride("focus", new StyleBoxEmpty { ContentMarginTop = 18, ContentMarginBottom = 12 });
        _editor.AddThemeColorOverride("background_color", Colors.Transparent);
        _editor.AddThemeColorOverride("font_color", new Color("202d29"));
        _editor.AddThemeColorOverride("caret_color", new Color("202d29"));
        _editor.PlaceholderText = "当他注意到什么，\n会怎样行动？\n\n当 条件，则 行动\n\n每行写下一条规则。";
        _diagnostic.AddThemeFontSizeOverride("font_size", 17);
        _diagnostic.CustomMinimumSize = new(0, 58);
        _thoughts.CustomMinimumSize = new(0, 90);
        tools.Reparent(right);
        tools.MoveToFront();
        foreach (var button in tools.GetChildren().OfType<Button>()) { QuietButton(button); button.AddThemeFontSizeOverride("font_size", 14); }
        _knowledgeButton.Text = "当时所知";

        _wordShelf = new VBoxContainer { Name = "AvailableWords" }; _mindPage.AddChild(_wordShelf);
        _wordShelf.AddThemeConstantOverride("separation", 4);
        LabelIn(_wordShelf, "可用词语 · 点击插入，也可直接书写", 17);
        var words = new HFlowContainer { Name = "WordChoices" }; _wordShelf.AddChild(words);
        words.AddThemeConstantOverride("h_separation", 10);
        words.AddThemeConstantOverride("v_separation", 2);
        foreach (var word in new[] { "当", "听过", "第一次茶铃", "第二次茶铃", "认为", "有茶", "，则", "将", "搬到", "查看", "窗边", "洗具台" })
        {
            var token = word;
            var button = ButtonIn(words, word, () => { _editor.InsertTextAtCaret(token + " "); _editor.GrabFocus(); });
            QuietButton(button); button.FocusMode = FocusModeEnum.None;
            button.AddThemeFontSizeOverride("font_size", 19);
        }
        var bottom = new HBoxContainer(); _wordShelf.AddChild(bottom);
        _draftCaption = LabelIn(bottom, "尚未重演", 13); _draftCaption.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _run.Reparent(bottom); _run.ThemeTypeVariation = ""; QuietButton(_run); _run.Text = "从头重演  ↗";
        evidenceTools.Reparent(_wordShelf);
        foreach (var button in evidenceTools.GetChildren().OfType<Button>()) QuietButton(button);

        heading.GetChild<Label>(1).Text = "假设中的过程";
        header.AddThemeConstantOverride("separation", 28);
        _mindTitle.AddThemeFontSizeOverride("font_size", 32);
        QuietButton(header.GetChild<Button>(0));
        _replayMat = new ColorRect { Name = "ReplayPaperFrame", Color = new("ded8c8"), MouseFilter = MouseFilterEnum.Ignore };
        _mindPage.AddChild(_replayMat); _mindPage.MoveChild(_replayMat, 0);
        _replayCaption = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; _mindPage.AddChild(_replayCaption);
        var roomName = LabelIn(_replayCaption, "公共研究室", 17); roomName.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        roomName.AddThemeColorOverride("font_color", new Color("26332d"));
        _replayTime = LabelIn(_replayCaption, "", 16); _replayTime.AddThemeColorOverride("font_color", new Color("26332d"));
        roomName.AutowrapMode = TextServer.AutowrapMode.Off;
        _replayTime.AutowrapMode = TextServer.AutowrapMode.Off;

        _transport.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        _status.Reparent(playback); playback.MoveChild(_status, playback.GetChildCount() - 1);
        controls.Alignment = BoxContainer.AlignmentMode.Center;
        controls.AddThemeConstantOverride("separation", 40);
        var back = ButtonIn(controls, "◀  回退", () => { _playing = false; _frame = Math.Max(0, _frame - 1); DisplayFrame(); }, "BackTea");
        controls.MoveChild(back, 0);
        _next.Text = "逐步  ▸";
        foreach (var button in controls.GetChildren().OfType<Button>()) { QuietButton(button); button.AddThemeFontSizeOverride("font_size", 21); }
        _timeline.CustomMinimumSize = new(0, 30);
        var ticks = new HBoxContainer(); playback.AddChild(ticks); playback.MoveChild(ticks, 2);
        _timelineStart = LabelIn(ticks, "起点", 13); _timelineStart.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _timelineEnd = LabelIn(ticks, "尚无重演记录", 13);
        _timelineStart.AutowrapMode = TextServer.AutowrapMode.Off;
        _timelineEnd.AutowrapMode = TextServer.AutowrapMode.Off;
        _status.Modulate = new Color("a9b1a7");
    }

    private static void QuietButton(Button button)
    {
        var normal = new StyleBoxEmpty { ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 8, ContentMarginBottom = 8 };
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("disabled", normal);
        button.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color(0.5f, 0.57f, 0.51f, 0.17f),
            ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 8, ContentMarginBottom = 8 });
    }

    private void LayoutReplayManuscript()
    {
        if (_manuscript is null) return;
        var left = Math.Max(640, Mathf.Floor((Size.X - 120) * 0.51f));
        var paperX = left + 80;
        _replayHeader.Position = new(32, 28); _replayHeader.Size = new(left, 68);
        _manuscript.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        _manuscript.Position = new(paperX, 36); _manuscript.Size = new(Size.X - paperX - 32, Size.Y - 266);
        _wordShelf.Position = new(paperX, Size.Y - 208); _wordShelf.Size = new(Size.X - paperX - 32, 180);
        // Keep original pixels at an integer scale, including hit testing in OnRoomInput.
        _mindStage.Position = new(40 + Mathf.Floor((left - 640) / 2), 132);
        _replayMat.Position = _mindStage.Position - new Vector2(5, 5); _replayMat.Size = new(650, 460);
        _replayCaption.Position = _mindStage.Position + new Vector2(10, 420); _replayCaption.Size = new(620, 30);
        _transport.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        _transport.Position = new(40, 624); _transport.Size = new(left, 164);
        _contextPrompt.OffsetRight = 40 + left;
    }

    private void RefreshReplayCaption()
    {
        if (_replayTime is null) return;
        var frame = _simulation.Frames[_frame];
        _replayTime.Text = $"{frame.Time:0.0}  ·  " + (_playing ? "重演中" : "已暂停");
        _timelineStart.Text = "起点  0.0";
        _timelineEnd.Text = $"已记录  {_simulation.Frames[^1].Time:0.0}";
        _draftCaption.Text = !_draftValid ? "请先修正规则，再从头重演" : _dirty ? "草稿已修改 · 画面仍为旧重演" : _version == 0 ? "尚未重演 · 空白规则的人物会等待" : $"思路 {_version} · 当前草稿已提交";
    }
}

