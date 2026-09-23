using Godot;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Game.Reasoning;

public partial class TeaReasoningScene
{
    private PanelContainer _dialogue = null!;
    private Label _speaker = null!, _dialogueText = null!, _lead = null!;
    private VBoxContainer _dialogueChoices = null!;
    private int _conversationPerson = -1, _conversationRevision;
    private readonly HashSet<Essence> _inspected = [];
    private string _inquiry = "opening";

    private void BuildInvestigation()
    {
        _lead = new Label { Name = "InvestigationLead", Position = new(40, 79), Size = new(950, 24) };
        _lead.AddThemeFontSizeOverride("font_size", 13); _lead.Modulate = new Color("a9c4b6");
        _realityHeader.AddChild(_lead);
        foreach (var actor in _actors)
        {
            var name = new Label { Text = actor.DisplayName, Position = new(-28, -62), Size = new(56, 20), HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            name.AddThemeFontOverride("font", _font); name.AddThemeFontSizeOverride("font_size", 10);
            name.AddThemeColorOverride("font_outline_color", new Color("141b20")); name.AddThemeConstantOverride("outline_size", 3);
            actor.AddChild(name);
        }
        _dialogue = new PanelContainer { Name = "InvestigationDialogue", Visible = false };
        AddChild(_dialogue); _dialogue.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        _dialogue.OffsetLeft = 180; _dialogue.OffsetRight = -180; _dialogue.OffsetTop = -330; _dialogue.OffsetBottom = -88;
        var content = new VBoxContainer(); _dialogue.AddChild(content);
        var heading = new HBoxContainer(); content.AddChild(heading);
        _speaker = LabelIn(heading, "", 20); _speaker.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ButtonIn(heading, "结束交谈  Esc", CloseConversation, "CloseConversation");
        _dialogueText = LabelIn(content, "", 17); _dialogueText.Name = "DialogueAnswer"; _dialogueText.CustomMinimumSize = new(0, 54);
        var choiceScroll = new ScrollContainer { Name = "DialogueChoiceScroll", CustomMinimumSize = new(0, 150), SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        content.AddChild(choiceScroll);
        _dialogueChoices = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _dialogueChoices.AddThemeConstantOverride("separation", 6); choiceScroll.AddChild(_dialogueChoices);
        BuildDebugOptions(); RefreshLead();
    }

    private int NearbyPerson()
    {
        var nearest = _actors.Select((actor, index) => (actor, index)).MinBy(p => p.actor.Position.DistanceSquaredTo(_detective.Position));
        return nearest.actor.Position.DistanceTo(_detective.Position) <= 44 ? nearest.index : -1;
    }
    private void InteractNearest()
    {
        if (_mental || _bookOpen || _help.Visible || _conversationPerson >= 0 || _detective.Busy) return;
        var person = NearbyPerson();
        if (person >= 0) TalkTo(person); else InspectNearest();
    }
    private void CloseConversation()
    {
        _conversationPerson = -1; _conversationRevision++;
        if (_dialogue is not null) _dialogue.Visible = false;
        GetViewport().GuiReleaseFocus();
    }
    private void Say(string text, Action? after = null)
    {
        if (_conversationPerson < 0) return;
        var actor = _actors[_conversationPerson];
        _conversationRevision++;
        actor.StopMotion(); _detective.StopMotion();
        actor.Face(_detective.Position - actor.Position);
        _detective.Face(actor.Position - _detective.Position);
        _detective.PlayGesture("talk");
        actor.PlayGesture("talk");
        // Text and choices acknowledge every click immediately; animation is presentation,
        // not a gate that silently drops input or closes the answer before it can be read.
        _dialogueText.Text = text; after?.Invoke(); RefreshLead();
    }
    private void Question(string title, string answer, Action? after = null)
    {
        ButtonIn(_dialogueChoices, title, () => Say(answer, after));
    }
    private void SetInquiry(string inquiry)
    {
        _inquiry = inquiry; RefreshLead();
    }
    private void OfferRoutes()
    {
        foreach (var child in _dialogueChoices.GetChildren()) { _dialogueChoices.RemoveChild(child); child.QueueFree(); }
        Question("先找茶去了哪里", "医生和画家借过茶具。先看看他们桌上的盆和纸盘，再问他们用途。", () => SetInquiry("tea"));
        Question("先查椅子为什么湿了", "那把椅子没人坐。你看看椅布，再问问负责整理的学员。", () => SetInquiry("chair"));
        Question("先问柜子出了什么事", "整理者说收进去的是空杯。我锁过柜门，现在里面却有满杯。", () => { RecordEvidence(15); SetInquiry("cabinet"); });
    }
    private void TalkTo(int person)
    {
        if (_mental || _bookOpen || _help.Visible || _detective.Busy || _actors[person].Busy) return;
        if (_actors[person].Position.DistanceTo(_detective.Position) > 44) { _log.Text = "走近" + _actors[person].DisplayName + "，按 E 交谈。"; return; }
        _conversationPerson = person; _dialogue.Visible = true;
        if (_actors[person].Position.Y > 240)
        {
            _dialogue.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
            _dialogue.OffsetTop = 112; _dialogue.OffsetBottom = 420;
        }
        else
        {
            _dialogue.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
            _dialogue.OffsetTop = -340; _dialogue.OffsetBottom = -32;
        }
        _dialogue.OffsetLeft = 180; _dialogue.OffsetRight = -180;
        _speaker.Text = _actors[person].DisplayName; _dialogueText.Text = "……";
        foreach (var child in _dialogueChoices.GetChildren()) { _dialogueChoices.RemoveChild(child); child.QueueFree(); }
        if (person == 0)
        {
            Say("下午茶还没开始，壶就空了，窗边的椅子也湿了。可我们四个谁都说自己没喝。", () => RecordEvidence(11));
            Question("我该先从哪里查？", "先挑一件说不通的事。你想查哪一处？", OfferRoutes);
            Question("第一声铃后，窗边是什么杯？", "我看到的还是那只有缺口的杯子，杯里有茶。", () => RecordEvidence(7));
            Question("你说柜子里的杯子怎么了？", "我先前锁柜时是空杯，现在里面却有一只满杯。我也想知道这怎么可能。", () => RecordEvidence(15));
            Question("你锁柜时，杯子是什么样？", "空的。我确认后才锁柜，第三声铃那时还没响。", () => RecordEvidence(9));
            Question("两声铃之间还发生过什么？", "第一声铃后发过收布通知；第二声铃后又发过收柜通知。每项整理做完，下一声铃才响。", () => RecordEvidence(22));
            if (_evidence.Contains(6)) Question("准备时你见过椅布吗？", "准备时我见过椅布，上面没有放东西。后来去取布前，我背对窗边忙别的，没有再核对。", () => RecordEvidence(20));
            else Question("你一直看着窗边吗？", "没有。若要核对我何时看过椅布，先检查椅子上的痕迹再来问我。", () => SetInquiry("chair"));
        }
        else if (person == 1)
        {
            Say("第一声铃后，我按通知换下了有缺口的杯子。旧杯临时找了地方搁，当时忙乱，我一时想不起是哪儿。");
            Question("第二声铃后，你看见了什么？", "窗边已经换成叶纹杯了，杯里有茶。", () => RecordEvidence(8));
            if (_evidence.Contains(6)) Question("椅布上的杯印让你想起什么？", "我想让叶纹杯接替缺口杯，旧杯确实先暂放在椅布上。后来我没有抽走椅布。", () => { RecordEvidence(21); SetInquiry("cups"); });
            else Question("旧杯后来放在哪里？", "我只记得临时搁在窗边附近，具体位置一时想不起来。", () => SetInquiry("chair"));
            Question("收柜时那只备用杯怎样？", "我收进去时，备用素杯明明是空的。", () => RecordEvidence(14));
        }
        else if (person == 2)
        {
            Say("我借过一杯热茶，用完已经把杯子放回原处。我没有喝；那时一直忙着温瓶。");
            if (_evidence.Contains(4)) Question("温瓶盆里为什么有茶？", "我把借来的茶倒进温瓶盆，用来温瓶。杯子用后放回了原处。", () => RecordEvidence(12));
            else Question("你当时在忙什么？", "一直在温瓶，盆和瓶都还没来得及收拾。", () => SetInquiry("tea"));
            Question("你看见杯子最初怎么摆的吗？", "画家准备时画了速写，在工作桌上。你可以先对照一下。", () => SetInquiry("cups"));
        }
        else
        {
            Say("我的道具纸处理过，但我没有喝茶。纸和盘都还留在工作处。");
            if (_evidence.Contains(5)) Question("纸上的茶色，是怎么来的？", "我把借来的茶倒进纸盘，给道具纸染旧。", () => RecordEvidence(13));
            else Question("纸是怎样处理的？", "我调过染液，赶着铺纸，具体痕迹还留在纸盘里。", () => SetInquiry("tea"));
            Question("你画过准备时的摆放？", "画过，就放在中间的工作桌。杯底花纹也画进去了。", () => SetInquiry("cups"));
        }
        RefreshLead();
    }
    private void RecordEvidence(int index)
    {
        var added = _evidence.Add(index); RefreshMaterials();
        _materials.Select(Array.IndexOf(_evidence.Order().ToArray(), index));
        if (added)
        {
            _log.Text = "札记新增：" + TeaCase.Evidence[index].Title;
            if (_conversationPerson >= 0) _dialogueText.Text += "\n\n札记已新增材料；结束后再次交谈，可能出现新的追问。";
        }
        RefreshStatus();
    }
    private void RefreshLead()
    {
        if (_lead is null) return;
        _lead.Text = _inquiry switch
        {
            "tea" => !_evidence.Contains(4) || !_evidence.Contains(5) ? "当前疑问：没人喝，茶去了哪里？  看看左下温瓶盆与右下染纸盘。" : !_evidence.Contains(12) || !_evidence.Contains(13) ? "已经发现茶迹。问医生和画家：他们怎样使用了茶？" : "茶有别的用途，但还没解释湿椅与满杯。可转查椅布，或在脑内尝试自己的解释。",
            "chair" => !_evidence.Contains(6) ? "当前疑问：没人坐的椅子为什么湿了？  到左侧椅子旁，按 F 检查。" : "杯底湿圈与抽布痕迹并存。问整理者，再对照工作桌上的准备速写。",
            "cups" => !_evidence.Contains(1) ? "当前疑问：同一位置，还是同一只杯子吗？  检查中央工作桌的速写。" : !_evidence.Contains(7) || !_evidence.Contains(8) ? "比较杯子身份。分别问主厨与整理者：两次铃后，他们看见了什么？" : "两次目击中的杯子不同。可在脑内写出换位的原因，再看它会造成什么后果。",
            "cabinet" => !_inspected.Contains(_reality.Cabinet) ? "当前疑问：空杯入柜，为什么变满？  检查右上器具柜。" : !_evidence.Contains(9) ? "核对锁柜的时间与空杯状态：问主厨；整理者是否也这样说？" : !_evidence.Contains(2) ? "杯子被锁在柜内。备物台上有配套器物的说明，可以检查。" : "联系器物说明与锁柜陈述。脑内可检验你的假设，模拟结果不会成为新证据。",
            _ => _evidence.Contains(11) ? "可问主厨选择一条疑问，也可以直接走近人物或器物调查。" : "主厨似乎遇到了麻烦。走向右上备物台，靠近“主厨”后按 E 交谈。"
        };
    }
}
