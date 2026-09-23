using Godot;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Game.Reasoning;

public partial class TeaReasoningScene
{
    private static readonly int[] CriticalEvidence = [1, 2, 4, 5, 6, 7, 8, 9, 12, 13, 16, 17, 18, 19, 20, 21, 22];
    private readonly string[] _taskEvents = ["准备时", "第一声铃后", "收布通知时", "第二声铃后", "收柜通知时"];
    private OptionButton _taskTemplate = null!, _taskEvent = null!, _taskObject = null!, _taskDestination = null!, _taskSecondObject = null!;
    private Label _taskPreview = null!, _taskTitle = null!;
    private RichTextLabel _comparisonText = null!;
    private VBoxContainer _differenceButtons = null!;

    private void BuildReasoningTools()
    {
        _organizer = PaperPanel("BehaviorOrganizerModal"); _modal.AddChild(_organizer);
        _organizer.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        _organizer.OffsetLeft = -370; _organizer.OffsetRight = 370; _organizer.OffsetTop = -285; _organizer.OffsetBottom = 285;
        var box = new VBoxContainer(); _organizer.AddChild(box);
        var heading = new HBoxContainer(); box.AddChild(heading);
        _taskTitle = LabelIn(heading, "补充行为假设", 24); _taskTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ButtonIn(heading, "收起", CloseBehaviorOrganizer);
        LabelIn(box, "描述这个人在什么情况下打算做什么。人物会自行走近、取放；每次通知只执行一次。", 14).Modulate = new Color("64756f");

        var form = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddChild(form);
        Label FieldLabel(string text)
        {
            var label = LabelIn(form, text, 15); label.CustomMinimumSize = new(140, 36); label.AutowrapMode = TextServer.AutowrapMode.Off;
            label.SizeFlagsHorizontal = SizeFlags.Fill; label.VerticalAlignment = VerticalAlignment.Center; return label;
        }
        OptionButton FieldPicker(string name)
        {
            var picker = new OptionButton { Name = name, CustomMinimumSize = new(0, 36), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            form.AddChild(picker); return picker;
        }
        FieldLabel("任务"); _taskTemplate = FieldPicker("TaskTemplate");
        foreach (var item in new[] { "搬运一件器物", "倾倒杯中茶", "借用后放回原处", "用新杯替换旧杯", "照记忆收起椅布", "检查后收起椅布", "收存两件器物（叠放）", "收存两件器物（分开放）", "锁上器具柜" }) _taskTemplate.AddItem(item);
        FieldLabel("触发"); _taskEvent = FieldPicker("TaskEvent"); foreach (var item in _taskEvents) _taskEvent.AddItem(item);
        FieldLabel("对象"); _taskObject = FieldPicker("TaskObject");
        FieldLabel("另一对象"); _taskSecondObject = FieldPicker("TaskSecondObject");
        FieldLabel("地点 / 容器"); _taskDestination = FieldPicker("TaskDestination");
        _taskPreview = LabelIn(box, "", 16); _taskPreview.Name = "TaskPreview";
        var actions = new HBoxContainer(); box.AddChild(actions);
        ButtonIn(actions, "写入当前人物的假设", AppendOrganizedTask, "AppendOrganizedTask").ThemeTypeVariation = "PrimaryButton";
        ButtonIn(actions, "取消", CloseBehaviorOrganizer);
        _taskTemplate.ItemSelected += _ => RefreshTaskChoices();
        _taskEvent.ItemSelected += _ => RefreshTaskPreview();
        _taskObject.ItemSelected += _ => { if (_taskTemplate.Selected == 3) RefreshTaskChoices(); else RefreshTaskPreview(); };
        _taskSecondObject.ItemSelected += _ => RefreshTaskPreview();
        _taskDestination.ItemSelected += _ => RefreshTaskPreview();
        RefreshTaskChoices(); _organizer.Visible = false;

        _comparison = PaperPanel("EvidenceComparisonModal"); _modal.AddChild(_comparison);
        _comparison.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        _comparison.OffsetLeft = -390; _comparison.OffsetRight = 390; _comparison.OffsetTop = -300; _comparison.OffsetBottom = 300;
        var compareBox = new VBoxContainer(); _comparison.AddChild(compareBox);
        var compareHeading = new HBoxContainer(); compareBox.AddChild(compareHeading);
        var compareTitle = LabelIn(compareHeading, "证据对照", 24); compareTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ButtonIn(compareHeading, "关闭", CloseComparison);
        LabelIn(compareBox, "只对照已经调查取得的材料；这里不给行为行判分。", 14).Modulate = new Color("64756f");
        _comparisonText = new RichTextLabel { Name = "EvidenceComparisonText", BbcodeEnabled = false, SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new(0, 300) };
        _comparisonText.AddThemeFontSizeOverride("normal_font_size", 17); compareBox.AddChild(_comparisonText);
        var differenceScroll = new ScrollContainer { Name = "DifferenceFrameScroll", CustomMinimumSize = new(0, 120), SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        compareBox.AddChild(differenceScroll);
        _differenceButtons = new VBoxContainer { Name = "DifferenceFrameLinks", SizeFlagsHorizontal = SizeFlags.ExpandFill }; differenceScroll.AddChild(_differenceButtons);
        _comparison.Visible = false;
    }

    private void OpenBehaviorOrganizer()
    {
        _bookOpen = false; _comparisonOpen = false; _help.Visible = false; _organizerOpen = true;
        _taskTitle.Text = _reality.People[_person].Name + " · 补充行为假设";
        _organizer.Visible = true; RefreshTaskChoices();
    }
    private void CloseBehaviorOrganizer() { _organizerOpen = false; _organizer.Visible = false; }
    private void OpenComparison()
    {
        _bookOpen = false; _organizerOpen = false; _help.Visible = false; _comparisonOpen = true;
        RefreshComparison();
    }
    private void CloseComparison() { _comparisonOpen = false; _comparison.Visible = false; }

    private void RefreshTaskChoices()
    {
        if (_taskObject is null) return;
        var template = _taskTemplate.Selected;
        var currentObject = _taskObject.Selected;
        var currentSecond = _taskSecondObject.Selected;
        var currentDestination = _taskDestination.Selected;
        _taskObject.Clear(); _taskSecondObject.Clear(); _taskDestination.Clear();

        Essence[] cups = [_reality.ChipCup, _reality.LeafCup, _reality.PlainCup];
        IEnumerable<Essence> objects = template == 1 ? [.. cups, _reality.Pot]
            : template is 2 or 3 or 6 or 7 ? cups
            : template is 4 or 5 ? [_reality.Cloth]
            : template == 8 ? [_reality.Cabinet]
            : _reality.Objects;
        IEnumerable<Essence> second = template == 3 ? cups.Where((_, index) => index != Math.Clamp(currentObject, 0, cups.Length - 1))
            : template is 6 or 7 ? [_reality.Saucer]
            : [];
        IEnumerable<Essence> destinations = template == 1 ? [_reality.Basin, _reality.Tray, _reality.Chair, _reality.Sink]
            : template == 2 ? [_reality.Basin, _reality.Tray]
            : template == 3 ? _reality.Sites.Where(s => s != _reality.Entrance).Concat([_reality.Cloth])
            : template == 0 ? _reality.Sites.Where(s => s != _reality.Entrance).Concat([_reality.Saucer, _reality.Cloth])
            : [];
        foreach (var item in objects) _taskObject.AddItem(item.TrueName);
        foreach (var item in second) _taskSecondObject.AddItem(item.TrueName);
        foreach (var item in destinations) _taskDestination.AddItem(item.TrueName);
        if (_taskObject.ItemCount > 0) _taskObject.Select(Math.Clamp(currentObject, 0, _taskObject.ItemCount - 1));
        if (_taskSecondObject.ItemCount > 0) _taskSecondObject.Select(Math.Clamp(currentSecond, 0, _taskSecondObject.ItemCount - 1));
        if (_taskDestination.ItemCount > 0) _taskDestination.Select(Math.Clamp(currentDestination, 0, _taskDestination.ItemCount - 1));
        _taskSecondObject.Disabled = _taskSecondObject.ItemCount == 0;
        _taskDestination.Disabled = _taskDestination.ItemCount == 0;
        RefreshTaskPreview();
    }

    private string OrganizedTaskText()
    {
        string Pick(OptionButton option) => option.ItemCount == 0 ? "" : option.GetItemText(option.Selected);
        var at = Pick(_taskEvent); var item = Pick(_taskObject); var other = Pick(_taskSecondObject); var destination = Pick(_taskDestination);
        return _taskTemplate.Selected switch
        {
            0 => $"{at}：将 {item} 搬到 {destination}",
            1 => $"{at}：将 {item} 倒入 {destination}",
            2 => $"{at}：借用 {item} 给 {destination}，用后放回 原处",
            3 => $"{at}：用 {item} 替换 {other}，旧杯暂放 {destination}",
            4 => $"{at}：照记忆收起 {item}",
            5 => $"{at}：检查后收起 {item}",
            6 => $"{at}：收存 {item} 和 {other}，方式 叠放",
            7 => $"{at}：收存 {item} 和 {other}，方式 分开放",
            _ => $"{at}：锁上 {item}"
        };
    }
    private void RefreshTaskPreview()
    {
        if (_taskPreview is not null) _taskPreview.Text = "将追加：\n" + OrganizedTaskText();
    }
    private void AppendOrganizedTask()
    {
        var line = OrganizedTaskText();
        if (string.IsNullOrWhiteSpace(line)) return;
        _editor.Text = string.IsNullOrWhiteSpace(_editor.Text) ? line : _editor.Text.TrimEnd() + "\n" + line;
        _editor.SetCaretLine(_editor.GetLineCount() - 1); _editor.SetCaretColumn(_editor.GetLine(_editor.GetLineCount() - 1).Length);
        CloseBehaviorOrganizer(); _editor.GrabFocus();
    }

    private List<int> MissingCriticalEvidence() => CriticalEvidence.Where(id => !_evidence.Contains(id)).ToList();
    private void RefreshComparison()
    {
        _comparison.Visible = _comparisonOpen;
        var frame = _simulation.Frames[Math.Clamp(_frame, 0, _simulation.Frames.Count - 1)];
        var differences = frame.Differences.Where(IsKnownDifference).ToArray();
        var lines = new List<string> { "当前回放时刻，已检查证据的对照结果：", "" };
        if (differences.Length == 0) lines.Add("当前没有发现与已检查材料冲突的结果。");
        else for (var i = 0; i < differences.Length; i++) lines.Add($"{i + 1}. {differences[i]}");
        var missing = MissingCriticalEvidence();
        if (missing.Count > 0)
        {
            lines.Add(""); lines.Add("尚未检查的关键材料（仅列标题）：");
            foreach (var id in missing) lines.Add("· " + TeaCase.Evidence[id].Title);
        }
        else if (_simulation.Finished && _frame == _simulation.Frames.Count - 1 && differences.Length == 0) { lines.Add(""); lines.Add("推演已完成，并与全部已调查关键证据相容。"); }
        _comparisonText.Text = string.Join("\n", lines);
        foreach (var child in _differenceButtons.GetChildren()) { _differenceButtons.RemoveChild(child); child.QueueFree(); }
        foreach (var difference in differences)
        {
            var first = _simulation.Frames.FindIndex(candidate => candidate.Differences.Contains(difference));
            if (first < 0) continue;
            var targetFrame = first;
            ButtonIn(_differenceButtons, $"回看首次出现 · {difference}", () =>
            {
                _playing = false; _frame = targetFrame; CloseComparison(); DisplayFrame();
            });
        }
    }

    private void AdvancePlaybackBatch()
    {
        var advanced = 0;
        do
        {
            var beforeFrame = _frame;
            var beforeDifferences = _simulation.Frames[Math.Clamp(_frame, 0, _simulation.Frames.Count - 1)].Differences.Length;
            Advance(); advanced++;
            if (_frame == beforeFrame || !_playing) break;
            var shown = _simulation.Frames[_frame];
            if (!shown.Event.Contains("前往", StringComparison.Ordinal) || shown.Differences.Length != beforeDifferences) break;
        } while (advanced < 4);
    }
}
