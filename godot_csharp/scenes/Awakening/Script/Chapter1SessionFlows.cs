using System.Text.Json;
using DualOntologyWorldEngine.Story;
using static DualOntologyWorldEngine.Story.ChapterEvents;

namespace DualOntologyWorldEngine.Awakening;

using Ev = Dictionary<string, object?>;

/// <summary>原第一章宿主的流程编排，逐项迁移；空间动作以 stage 事件交给现场，等其回执。</summary>
public sealed partial class Chapter1Session
{
    // -------------------------------------------------------------- plans --

    private IEnumerable<Op> Begin()
    {
        foreach (var who in Party)
            _out.Add(StageOp("place", ("who", who), ("to", new Ev { ["anchor"] = "起点·" + who })));
        foreach (var who in Companions) _out.Add(StageOp("pose", ("who", who), ("anim", "idle"), ("facing", "front")));
        _out.Add(StageOp("sit", ("who", "detective"), ("facing", "front"), ("lift", 4.0)));
        Music("door_side");
        yield return new Run(PlayPlan(() => _content.BeginWake(_world)));
    }

    /// <summary>C# 剧本计划逐项给出场面：对白标题，或 @命令。</summary>
    private IEnumerable<Op> PlayPlan(Action begin)
    {
        _planDepth++;
        PauseDaily();
        try
        {
            var plan = _content.StartPlan(begin);
            while (true)
            {
                var next = _content.NextMeeting(plan);
                if (string.IsNullOrEmpty(next)) break;
                if (next.StartsWith('@'))
                {
                    var parts = next[1..].Split(':');
                    yield return new Run(Command(parts[0], parts[1..]));
                }
                else yield return new Run(Say(next));
                _content.Presented(plan);
            }
        }
        finally { _planDepth--; }
    }

    private IEnumerable<Op> Say(string title)
    {
        if (title is "" or "<null>") yield break;
        yield return new Until(() => !_dialogueActive);
        _dialogueActive = true; _storyLock = true;
        PauseDaily();
        _out.Add(StageOp("cancel_click"));
        try
        {
            var packet = _dialogue.Start(title);
            while (true)
            {
                if (packet.WorldCommands.Count > 0)
                {
                    foreach (var command in packet.WorldCommands)
                        yield return new Run(Command(command.Name, command.Arguments.Select(a => a?.ToString() ?? "").ToArray()));
                    packet = _dialogue.Next();
                    continue;
                }
                if (packet.IsComplete || (packet.Lines.Count == 0 && packet.Choices.Count == 0)) break;
                yield return new Await(LinesEvent(packet), true);
                packet = packet.Choices.Count > 0 ? _dialogue.Choose(Text(_reply, "choice_id")) : _dialogue.Next();
            }
        }
        finally { _dialogueActive = false; _storyLock = false; }
    }

    private Ev LinesEvent(InteractionPacket packet)
    {
        string Display(string speaker) => speaker switch
        { "侦探" => DetectiveName, "侦探（心声）" => DetectiveName + "（心声）", _ => speaker };
        var ev = Event("lines", ("lines", packet.Lines.Select(line => (object?)new Ev
        {
            ["speaker"] = line.Speaker, ["speaker_name"] = Display(line.Speaker), ["mood"] = line.Mood, ["text"] = line.Text,
            ["voice_clip"] = line.VoiceClip, ["line_id"] = line.LineId,
        }).ToArray()));
        // 条件未满足且没有锁定提示的话题保持隐藏（原对白框的规则）。
        var choices = packet.Choices.Where(c => c.IsAvailable || c.LockHint != "").ToArray();
        if (choices.Length > 0)
            ev["choices"] = choices.Select(c => (object?)Option(c.Id, c.Label, c.IsAvailable, c.LockHint)).ToArray();
        return ev;
    }

    /// <summary>剧本里的 @命令（原 present_dialogue_command）。</summary>
    private IEnumerable<Op> Command(string kind, string[] args)
    {
        string A(int index) => index < args.Length ? args[index] : "";
        switch (kind)
        {
            case "card": return OpenCard(A(0));
            case "talk": return Talk(A(0));
            case "field_board": return OpenBoard();
            case "trace": return OpenTrace(A(0));
            case "tube": return TubeMenu();
            case "experiment": return Experiment(A(0));
            case "help": return Help(A(0));
            case "recall": return Recall(A(0));
            case "compare": return CompareHand(A(0));
            case "close": CloseModal(); break;
            case "name": return AskName();
            case "board": return PresentBoard();
            case "rest": return PlayPlan(() => _content.BeginRest(_world));
            case "posts":
                foreach (var who in Companions) Start(WalkTo(who, new Ev { ["anchor"] = "post:" + who }));
                break;
            case "stage_actor": return WalkTo(A(0), new Ev { ["anchor"] = A(1) });
            case "summon": return Summon(A(0));
            case "consider": Start(Consider(A(0))); break;
            case "finish": return FinishReach(A(0));
            case "sound": return SoundTest();
            case "aside": Aside(string.Join(":", args)); break;
            case "music": Music(A(0), args.Length > 1 ? double.Parse(A(1)) : 2.5); break;
            case "sfx": Sfx(A(0), args.Length > 1 ? double.Parse(A(1)) : -4.0); break;
            case "lock": _storyLock = true; break;
            case "unlock": _storyLock = false; break;
            case "hud": break;
            case "answerpick": return AnswerPick();
            case "finale": return PlayPlan(() => _content.BeginFinale(_world));
            case "finale_prepare":
                _storyLock = true;
                _out.Add(StageOp("place", ("who", "detective"), ("to", new Ev { ["anchor"] = "终局站位·侦探" })));
                _out.Add(StageOp("pose", ("who", "detective"), ("anim", "idle"), ("facing", "left")));
                _out.Add(StageOp("camera", ("to", new Ev { ["anchor"] = "终局镜头" }), ("seconds", 0.0)));
                break;
            case "door_open":
                Sfx("door_unbar", 0);
                Music("main_theme", 4.0);
                _out.Add(StageOp("prop", ("id", "isolation_door"), ("state", "open")));
                break;
            case "redraw": _out.Add(StageOp("prop", ("id", "traces"), ("state", "refresh"))); break;
            case "su_approach": return WalkTo("su", new Ev { ["near_actor"] = "detective", ["offsets"] = new object?[] { new object?[] { 34.0, 6.0 } } });
            case "save": SaveChapterEnd(A(0) == "true"); break;
            case "ending": Ending(A(0)); break;
            default: throw new InvalidDataException("Unknown chapter presentation: " + kind);
        }
        return Nothing();
    }

    // ------------------------------------------------------------- staging --

    /// <summary>把人走到某处（场面调度用）；走不通由现场直接放到那里。完成后归还占用。</summary>
    private IEnumerable<Op> WalkTo(string who, Ev to, double speed = 150.0)
    {
        Claim(who);
        try { yield return new Await(StageOp("walk", ("who", who), ("to", to), ("speed", speed)), false); }
        finally { Unclaim(who); }
    }
    private bool WalkOk => Flag(_reply, "ok", true);

    private void Face(string who, string other)
    {
        _out.Add(StageOp("face", ("who", who), ("toward", other)));
        _out.Add(StageOp("face", ("who", other), ("toward", who)));
    }

    private IEnumerable<Op> Meeting()
    {
        PauseDaily();
        Stage = _content.StageEvent("相遇");
        _storyLock = true;
        _out.Add(StageOp("cancel_click"));
        foreach (var (who, spot, facing) in new[] { ("huo", "Engineer", "right"), ("shen", "Chef", "back"), ("su", "Musician", "left") })
        {
            Claim(who);
            _out.Add(StageOp("place", ("who", who), ("to", new Ev { ["anchor"] = "meeting:" + spot })));
            _out.Add(StageOp("pose", ("who", who), ("anim", "idle"), ("facing", facing)));
            Unclaim(who);
        }
        yield return new Run(WalkTo("detective", new Ev { ["anchor"] = "meeting:Detective" }, 120.0));
        _out.Add(StageOp("pose", ("who", "detective"), ("anim", "idle"), ("facing", "front")));
        yield return new Run(PlayPlan(() => _content.BeginMeeting(_world)));
    }

    private IEnumerable<Op> RoomChanged(string room)
    {
        if (room == "" || F("detective").Entered != room) yield break;
        yield return new Run(PlayPlan(() => _content.BeginRoomChanged(_world, room, (long)(_now * 1000))));
    }

    private IEnumerable<Op> Summon(string who)
    {
        Claim(who);
        try
        {
            var waited = 0.0;
            // 人手上正有事（拿着、走向自己的旧物）：先应一声，忙完再过来。
            if (LifeBusy(who)) yield return new Run(Say("help_busy_" + who));
            while (LifeBusy(who) && waited < 15.0) { yield return new Sleep(0.1); waited += 0.1; }
            if (F(who).Distance > 90)
            {
                yield return new Await(StageOp("walk", ("who", who), ("speed", 240.0), ("to", new Ev
                {
                    ["near_actor"] = "detective", ["avoid"] = true,
                    ["offsets"] = new object?[] { new object?[] { 30.0, 4.0 }, new object?[] { -30.0, 4.0 }, new object?[] { 0.0, -28.0 },
                        new object?[] { 48.0, -10.0 }, new object?[] { -48.0, -10.0 }, new object?[] { 0.0, 30.0 } },
                })), false);
                if (!WalkOk) yield break;
            }
            Face(who, "detective");
        }
        finally { Unclaim(who); }
    }

    // --------------------------------------------------------------- field --

    private IEnumerable<Op> Interact(string site, string target)
    {
        if (site.StartsWith("person:", StringComparison.Ordinal)) site = site[7..];
        if (target != "" && Companions.Contains(target)) site = target;
        var packet = _interaction.Interact(site);
        foreach (var command in packet.WorldCommands)
            yield return new Run(Command(command.Name, command.Arguments.Select(a => a?.ToString() ?? "").ToArray()));
    }

    /// <summary>普通家具：一件只说一次。文字优先取剧本的家具表，其次取地图里画上去的装饰说明。</summary>
    private void Furniture(IReadOnlyDictionary<string, object?> input)
    {
        var asides = Layout.GetProperty("furniture_asides");
        var texts = Map(input, "texts");
        foreach (var node in (input.GetValueOrDefault("nodes") as IEnumerable<object?> ?? []).Select(n => n?.ToString() ?? ""))
        {
            if (_furnitureSpoken.Contains(node)) continue;
            var key = asides.EnumerateObject().Select(p => p.Name).Where(prefix => node.StartsWith(prefix, StringComparison.Ordinal))
                .OrderByDescending(prefix => prefix.Length).FirstOrDefault();
            var text = key is not null ? asides.GetProperty(key).GetString() : texts.GetValueOrDefault(node)?.ToString();
            if (string.IsNullOrEmpty(text)) continue;
            _furnitureSpoken.Add(node);
            Aside(text);
            return;
        }
    }

    // --------------------------------------------------------------- cards --

    private void Examine(string id)
    {
        foreach (var signal in _content.Examine(_world, id))
            switch (signal)
            {
                case "evidence": Sfx("evidence", -3.0); break;
                case "unlock": Start(UnlockReplay()); break;
                case "v6": Start(V6Doubt()); break;
            }
    }

    private List<string> GeometricCardPeople() => Party.Where(CanCompareHand).ToList();

    private IEnumerable<Op> OpenCard(string id, bool brief = false)
    {
        Examine(id);
        if (_content.NudgeCard(id))
        {
            // 恶魔在玩家第一次面对这件物件的回忆选项之前说一句；每件只说一次。
            CloseModal();
            yield return new Run(Say("nudge_" + id));
        }
        var packet = _interaction.CardPacket(id, GeometricCardPeople());
        var card = packet.Card!;
        var picture = Art + "card_" + card.Picture + ".png";
        if (brief)
        {
            OpenModal(Event("card", ("id", id), ("title", card.Title), ("notes", card.Notes.Cast<object?>().ToArray())),
                [(Option("full", "看整张证物卡"), () => OpenCard(id)), (Option("close", T("卡片·返回")), Nothing)]);
            yield break;
        }
        OpenModal(Event("card", ("id", id), ("title", card.Title), ("picture", picture), ("facts", card.Facts), ("source", card.Source),
                ("notes", card.Notes.Cast<object?>().ToArray())),
            packet.Choices.Select(choice => (Option(choice.Id, choice.Label, choice.IsAvailable, choice.LockHint,
                choice.Id.Contains(":compare:") ? "inline" : ""), (Func<IEnumerable<Op>>)(() => ChooseCard(choice.Id)))));
    }

    private IEnumerable<Op> ChooseCard(string choiceId)
    {
        // 执行时按真实站位与是否在走重新核验。
        var packet = _interaction.Choose(choiceId, GeometricCardPeople());
        foreach (var command in packet.WorldCommands)
            yield return new Run(Command(command.Name, command.Arguments.Select(a => a?.ToString() ?? "").ToArray()));
    }

    /// <summary>R-06、R-07 的生活痕迹：只看，不编号，不进重演对照。</summary>
    private IEnumerable<Op> OpenTrace(string id)
    {
        Session.Set("examined", id, true);
        Session.Observe("detective", id);
        var trace = _content.LifeTrace(id);
        OpenModal(Event("card", ("id", id), ("title", trace.GetProperty("title").GetString()),
                ("picture", Art + trace.GetProperty("picture").GetString() + ".png"), ("facts", trace.GetProperty("facts").GetString()),
                ("source", trace.GetProperty("room").GetString())),
            [(Option("close", T("卡片·返回")), Nothing)]);
        yield break;
    }

    private Ev BoardCard() => Event("card", ("id", "board"), ("title", T("公告框·标题")), ("intro", T("公告框·引言")),
        ("items", MorningWorld.PostedRules.Cast<object?>().ToArray()));

    private IEnumerable<Op> OpenBoard()
    {
        Session.Set("flags", "board_read", true);
        OpenModal(BoardCard(), [(Option("close", T("卡片·返回")), Nothing)]);
        yield break;
    }

    private IEnumerable<Op> PresentBoard()
    {
        yield return new Await(StageOp("camera", ("to", new Ev { ["anchor"] = "公告框镜头" }), ("seconds", 0.8)), false);
        Session.Set("flags", "board_read", true);
        var card = BoardCard();
        card["options"] = new object?[] { Option("close", T("卡片·返回")) };
        yield return new Await(card, true);
        yield return new Await(StageOp("camera", ("to", new Ev { ["follow"] = true }), ("seconds", 0.5)), false);
    }

    private IEnumerable<Op> Experiment(string id)
    {
        CloseModal();
        if (id == "E11") { yield return new Run(CompareHand("detective")); yield break; }
        var who = "";
        if (id == "E3")
        {
            var options = Content.GetProperty("SOLE_TARGETS").EnumerateObject().Select(p => (object?)Option(p.Name, p.Value.GetString()!)).ToList();
            options.Add(Option("cancel", "算了"));
            yield return new Await(Event("candidates", ("title", T("鞋底·标题")), ("prompt", T("鞋底·提示")), ("options", options.ToArray())), true);
            who = Text(_reply, "choice_id") is var picked && picked != "cancel" ? picked : "";
        }
        yield return new Run(PlayPlan(() => _content.BeginExperiment(_world, id, who)));
        yield return new Run(OpenCard(id));
    }

    private bool AtHandShelf(string who) => F(who).Room == "R05" && F(who).Near.Contains("E11a");
    private bool HandBusy(string who) => (F(who).Moving && !_dailyJobs.ContainsKey(who)) || LifeBusy(who);
    private bool CanCompareHand(string who)
    {
        if (!AtHandShelf("detective")) return false;
        if (who == "detective") return true;
        return AtHandShelf(who) && F(who).Visible && !HandBusy(who);
    }

    private IEnumerable<Op> CompareHand(string who)
    {
        CloseModal();
        if (!CanCompareHand(who))
        {
            Aside(T("提示·比对站位"));
            yield return new Run(OpenCard("E11"));
            yield break;
        }
        yield return new Run(PlayPlan(() => _content.BeginExperiment(_world, "E11", who)));
        yield return new Run(OpenCard("E11"));
    }

    /// <summary>谈话里请同伴到书架边比对：真实走路，暂停时停步，受阻即停。</summary>
    private IEnumerable<Op> InviteHand(string who)
    {
        var name = ("人名", PersonName(who));
        if (!Has("examined", "E11") || !F(who).Visible || F(who).Distance > 56.0) { Aside(T("旁白·邀请距离")); yield break; }
        if (HandBusy(who) || _invites.Contains(who)) { Aside(T("旁白·正忙", name)); yield break; }
        ReleaseDaily(who, true);
        if (AtHandShelf(who)) { Aside(T("旁白·已在架沿", name)); yield break; }
        _invites.Add(who);
        Aside(T("旁白·正赶去", name));
        try
        {
            yield return new Await(StageOp("walk", ("who", who), ("speed", 0.0), ("pausable", true), ("stop_when_stuck", true), ("to", new Ev
            {
                ["near_site"] = "E11a", ["avoid"] = true,
                ["offsets"] = new object?[] { new object?[] { 0.0, 40.0 }, new object?[] { -32.0, 40.0 }, new object?[] { 32.0, 40.0 },
                    new object?[] { -48.0, 56.0 }, new object?[] { 0.0, 72.0 }, new object?[] { 48.0, 56.0 } },
            })), false);
        }
        finally { _invites.Remove(who); }
        switch (Text(_reply, "reason"))
        {
            case "no_route": Aside(T("旁白·路不通", name)); break;
            case "stuck": Aside(T("旁白·被挡", name)); break;
            case "cancelled": break;
            default: Aside(T(AtHandShelf(who) ? "旁白·已到" : "旁白·停下", name)); break;
        }
    }

    private IEnumerable<Op> Help(string id)
    {
        CloseModal();
        yield return new Run(PlayPlan(() => _content.BeginHelp(_world, id)));
        yield return new Run(OpenCard(id, true));
    }

    private IEnumerable<Op> Recall(string id)
    {
        CloseModal();
        yield return new Run(PlayPlan(() => _content.BeginRecall(_world, id)));
        yield return new Run(OpenCard(id));
    }

    private IEnumerable<Op> UnlockReplay()
    {
        if (!_content.UnlockReplay()) yield break;
        yield return new Until(() => !_storyLock && !_dialogueActive && _modal is null && _uiOwner is null && _uiModal == "");
        yield return new Run(Say("replay_unlock"));
    }

    private IEnumerable<Op> V6Doubt()
    {
        yield return new Until(() => !_storyLock && !_dialogueActive);
        yield return new Run(Say("v6_doubt"));
    }

    // -------------------------------------------------------------- recall --

    private string OwnObject(string who) => _content.OwnObject(who);
    private bool DetectivePresent(string who)
    {
        var room = F("detective").Entered;
        return room != "" && room == F(who).Entered && F(who).Visible && F(who).Distance < 360.0;
    }

    private IEnumerable<Op> Consider(string who)
    {
        if (_invites.Contains(who)) yield break;
        var intent = _content.Consider(_world, who);
        if (intent is { Reach: true }) Start(GoReach(who, intent.Thought));
    }

    private IEnumerable<Op> GoReach(string who, string thought)
    {
        ReleaseDaily(who, true);
        _content.BeginReach(who, thought);
        var obj = OwnObject(who);
        yield return new Sleep(0.6);
        yield return new Until(() => !(_dialogueActive && DetectivePresent(who)));
        yield return new Until(() => FieldFree);
        yield return new Run(WalkTo(who, new Ev { ["approach_site"] = obj, ["inset"] = 14.0 }, 110.0));
        if (!WalkOk) { Session.Remove("life", who); yield break; }
        _out.Add(StageOp("face", ("who", who), ("toward", "site:" + obj)));
        if (!_content.Reach(_world, who)) yield break;
        _out.Add(StageOp("prop", ("id", obj), ("state", "taken")));
        yield return new Run(DetectivePresent(who) ? PresentReach(who) : AloneReach(who));
    }

    private IEnumerable<Op> PresentReach(string who)
    {
        yield return new Until(() => !_dialogueActive && !_storyLock && _uiOwner is null && (_uiModal is "" or "card") && Stage == "investigate");
        if (!DetectivePresent(who)) { yield return new Run(AloneReach(who)); yield break; }
        CloseModal();
        Face(who, "detective");
        yield return new Run(PlayPlan(() => _content.BeginPresentReach(_world, who)));
    }

    private IEnumerable<Op> AloneReach(string who)
    {
        _content.PrepareAlone(_world, who);
        yield return new Sleep(1.2);
        _content.CompleteAlone(_world, who);
        yield return new Run(FinishReach(who));
    }

    private IEnumerable<Op> FinishReach(string who)
    {
        var obj = OwnObject(who);
        _world.PutBack(PersonOf(who));
        _out.Add(StageOp("prop", ("id", obj), ("state", "returned")));
        if (DetectivePresent(who)) ((Dictionary<string, object>)Session.GetProgress("life")[who])["noticed"] = true;
        yield return new Run(WalkTo(who, new Ev { ["anchor"] = "post:" + who }, 110.0));
    }

    // ---------------------------------------------------------------- talk --

    private IEnumerable<Op> Talk(string who)
    {
        PauseDaily();
        Face(who, "detective");
        Session.Remove("flags", "invite_hand_" + who);
        yield return new Run(PlayPlan(() => _content.BeginTalk(_world, who)));
        if (Has("flags", "invite_hand_" + who))
        {
            Session.Remove("flags", "invite_hand_" + who);
            Start(InviteHand(who));
        }
    }

    private IEnumerable<Op> TubeMenu()
    {
        OpenModal(Event("candidates", ("title", T("铜口·标题")), ("prompt", T("铜口·描述"))),
        [
            (Option("examine", T("铜口·检视")), () => OpenCard("E8")),
            (Option("answer", T("铜口·作答")), () => PlayPlan(() => _content.BeginAnswer(_world))),
            (Option("rest", T("铜口·歇息")), () => PlayPlan(() => _content.BeginConfirmRest(_world))),
            (Option("close", T("卡片·返回")), Nothing),
        ]);
        yield break;
    }

    private IEnumerable<Op> AskName()
    {
        yield return new Await(Event("form", ("title", T("报名·标题")), ("prompt", T("报名·提示")), ("layout", "stack"),
            ("fields", new object?[] { new Ev { ["id"] = "name", ["kind"] = "text", ["placeholder"] = T("报名·占位"), ["value"] = "" } }),
            ("actions", new object?[] { new Ev { ["id"] = "confirm", ["label"] = T("报名·确认"), ["default"] = true } })), true);
        var name = (Map(_reply, "values").GetValueOrDefault("name")?.ToString() ?? "").Trim();
        Session.Set("state", "detective_name", name != "" ? name : T("报名·默认名"));
    }

    // ---------------------------------------------------------------- sound --

    private IEnumerable<Op> SoundTest()
    {
        var sentences = TestSentences.All.Select((s, i) => (object?)new Ev { ["id"] = i.ToString(), ["label"] = $"“{s.Full}”" }).ToArray();
        var spots = Content.GetProperty("SOUND_SPOTS").EnumerateObject().Select(p => (object?)new Ev { ["id"] = p.Name, ["label"] = p.Value.GetString() }).ToArray();
        yield return new Await(Event("form", ("title", T("声路·标题")), ("prompt", T("声路·说明")), ("layout", "stack"),
            ("fields", new object?[]
            {
                new Ev { ["id"] = "sentence", ["kind"] = "choice", ["heading"] = T("声路·选句"), ["value"] = "0", ["options"] = sentences },
                new Ev { ["id"] = "spot", ["kind"] = "choice", ["heading"] = T("声路·选位"), ["value"] = ((Ev)spots[0]!)["id"], ["options"] = spots, ["flow"] = true },
            }),
            ("actions", new object?[] { new Ev { ["id"] = "cancel", ["label"] = "算了" }, new Ev { ["id"] = "start", ["label"] = "开始", ["default"] = true } })), true);
        if (Text(_reply, "action") != "start") yield break;
        var values = Map(_reply, "values");
        var sentence = int.TryParse(values.GetValueOrDefault("sentence")?.ToString(), out var s) ? s : 0;
        var spot = values.GetValueOrDefault("spot")?.ToString() ?? "foyer";
        var map = new Dictionary<Person, Room>();
        foreach (var (who, room) in _content.SoundPositions(spot)) map[PersonOf(who)] = (Room)Array.IndexOf(Names.RoomKeys, room);
        _content.RecordSoundTest();
        Sfx("tuning_fork", -5.0);
        var heard = _world.SoundTest(TestSentences.All[Math.Clamp(sentence, 0, TestSentences.All.Length - 1)], map);
        Session.Set("state", "last_sound", heard.Select(h => (object)new Dictionary<string, object>
        {
            ["who"] = Names.PersonKeys[(int)h.Who], ["room"] = Names.RoomKeys[(int)h.Where], ["text"] = h.Text, ["outside"] = h.SeemsFromOutside,
            ["hearing"] = h.Hearing.ToString(), ["direction"] = h.Direction,
        }).ToArray());
        Session.Set("experiments", "E8", true);
        Session.Set("state", "sound_sentence", sentence);
        var rounds = Convert.ToInt32(State["sound_rounds"]) + 1;
        Session.Set("state", "sound_rounds", rounds);
        if (rounds == 1)
        {
            yield return new Run(Say("sound_test"));
            yield return new Run(OpenCard("E8"));
            yield break;
        }
        // 第二轮起不再逐人复述：一张小表汇总四处所闻。
        var rows = ((object[])State["last_sound"]).Cast<Dictionary<string, object>>().Select(h => (object?)new object?[]
        {
            Chapter1Data.RoomName((string)h["room"]), (string)h["who"] == "detective" ? T("声路·你") : PersonName((string)h["who"]),
            h["text"], h["direction"],
        }).ToArray();
        OpenModal(Event("card", ("id", "sound"), ("title", T("声路·轮标题", ("轮", rounds.ToString()))),
                ("intro", T("声路·铜口说", ("句", TestSentences.All[sentence].Full))), ("table", rows)),
            [(Option("card", "看证物卡"), () => OpenCard("E8")), (Option("close", T("卡片·返回")), Nothing)]);
    }

    // --------------------------------------------------------------- answer --

    private IEnumerable<Op> AnswerPick()
    {
        var step = Convert.ToInt32(Session.GetProgress("answer_state").GetValueOrDefault("step", 1));
        var verdict = Content.GetProperty("VERDICTS").TryGetProperty(step.ToString(), out var v) ? v.GetProperty("question").GetString()! : "";
        var replay = Session.GetProgress("replay_result");
        var statements = _content.Statements(((object[])State["replay_vars"]).Select(Convert.ToInt32).ToArray(),
            Convert.ToInt32(State["replay_count"]), replay.GetValueOrDefault("detective_bolted") is true);
        var say = statements.Select(key => (object?)new Ev { ["id"] = key, ["label"] = Content.GetProperty("STATEMENTS").GetProperty(key).GetString() }).ToArray();
        var show = Content.GetProperty("EVIDENCE").EnumerateObject().Where(e => Has("examined", e.Name))
            .Select(e => (object?)new Ev { ["id"] = e.Name, ["label"] = e.Value.GetProperty("name").GetString() }).ToArray();
        yield return new Await(Event("form", ("title", T("答辩·标题")), ("layout", "tabs"),
            ("header", new Ev { ["question"] = T("答辩·题头", ("问题", verdict)), ["instructions"] = T("答辩·说明") }),
            ("fields", new object?[]
            {
                new Ev { ["id"] = "say", ["kind"] = "choice", ["options"] = say, ["empty_text"] = T("答辩·无陈述"), ["value"] = "" },
                new Ev { ["id"] = "show", ["kind"] = "choice", ["options"] = show, ["empty_text"] = T("答辩·无笔记"), ["value"] = "" },
            }),
            ("summary", new object?[] { new Ev { ["field"] = "say", ["template"] = T("答辩·陈述行") }, new Ev { ["field"] = "show", ["template"] = T("答辩·出示行") } }),
            ("unset_label", T("答辩·未选")),
            ("actions", new object?[]
            {
                new Ev { ["id"] = "tab:say", ["label"] = "陈述", ["tab"] = "say" },
                new Ev { ["id"] = "tab:show", ["label"] = "出示", ["tab"] = "show" },
                new Ev { ["id"] = "cancel", ["label"] = "暂且不答" },
                new Ev { ["id"] = "submit", ["label"] = "作答", ["requires"] = new object?[] { "say", "show" } },
            })), true);
        var values = Map(_reply, "values");
        var pick = Session.GetProgress("answer_pick");
        pick.Clear();
        if (Text(_reply, "action") == "submit")
        {
            pick["say"] = values.GetValueOrDefault("say")?.ToString() ?? "";
            pick["show"] = values.GetValueOrDefault("show")?.ToString() ?? "";
        }
    }

    // --------------------------------------------------------------- replay --

    private IEnumerable<Op> Replay(object[] vars, bool skip)
    {
        if (!ReplayOpen) throw new InvalidOperationException("尚未获得重演手稿。");
        State["replay_vars"] = vars;
        var result = RunReplay(vars.Select(Convert.ToInt32).ToArray());
        var stored = Session.GetProgress("replay_result");
        stored.Clear();
        foreach (var (key, value) in result) stored[key] = value;
        State["replay_count"] = Convert.ToInt32(State["replay_count"]) + 1;
        State["last_run_vars"] = vars.ToArray();
        if (!skip)
        {
            _modal = null;
            _out.Add(Event("close"));
            Stage = _content.StageEvent("重演开始");
            _storyLock = true;
            Sfx("replay", -4.0);
            _out.Add(Event("duck", ("on", true)));
            yield return new Await(Event("replay_play", ("mode", "stage"), ("caption", T("重演·昨夜")), ("frames", result["frames"]),
                ("names", Party.ToDictionary(who => who, who => (object?)PersonName(who))),
                ("door_texts", new Ev { ["open"] = T("重演·门开"), ["bolted"] = T("重演·门闩"), ["closed"] = T("重演·门关"), ["ash"] = T("重演·烟灰") })), true);
            _out.Add(Event("duck", ("on", false)));
            Stage = _content.StageEvent("重演结束");
            _storyLock = false;
        }
        _modal = null;
        _out.Add(Event("close"));
        yield return new Run(PlayPlan(() => _content.BeginAfterReplay(_world)));
        _out.Add(Event("manuscript"));
    }

    /// <summary>原 AwakeningEngine.RunReplay：在独立的昨夜世界里推演，帧与对照存进进度（不触碰清晨现实）。</summary>
    private Dictionary<string, object> RunReplay(int[] variables)
    {
        var v = new NightVariables((SuPlace)variables[0], (HuoPlace)variables[1], (VoiceOrigin)variables[2],
            (VoiceWords)variables[3], (DetectiveHeeds)variables[4], (Acquaintance)variables[5]);
        _content.RecordReplayRun();
        var result = NightReplay.Run(v);
        _content.WitnessReplay(_world, v, result.FullMatch);
        string Key(Person p) => Names.PersonKeys[(int)p];
        var frames = result.Frames.Select(f => (object)new Dictionary<string, object>
        {
            ["t"] = f.Time, ["actor"] = f.Actor, ["label"] = f.Label,
            ["rooms"] = Enum.GetValues<Person>().ToDictionary(Key, p => (object)Names.RoomKeys[(int)f.Rooms[p]]),
            ["spots"] = Enum.GetValues<Person>().ToDictionary(Key, p => (object)f.Spots[p]),
            ["thoughts"] = Enum.GetValues<Person>().ToDictionary(Key, p => (object)f.Thoughts[p]),
            ["door_open"] = f.DoorOpen, ["door_bolted"] = f.DoorBolted, ["ash"] = f.AshFan, ["smoke"] = f.SmokeIn,
        }).ToArray();
        var findings = Comparison.Compare(result.Traces, MorningTraces.Evidence, id => Has("examined", id)).Select(finding => (object)new Dictionary<string, object>
        {
            ["id"] = finding.Evidence, ["place"] = finding.Place, ["text"] = finding.Text,
            ["status"] = finding.Status switch { FindingStatus.Match => "match", FindingStatus.Conflict => "conflict", _ => "unvisited" },
        }).ToArray();
        return new()
        {
            ["frames"] = frames, ["findings"] = findings, ["full_match"] = result.FullMatch,
            ["conflicts"] = result.Conflicts.Select(c => (object)c).ToArray(), ["bolted"] = result.Traces.InnerBoltDown,
            ["detective_bolted"] = result.Log.Any(l => l.Contains("侦探照霍铎转述")),
        };
    }

    // --------------------------------------------------------------- ending --

    private void SaveChapterEnd(bool rested)
    {
        if (!rested) ChapterWorldContinuity.Publish(_world.ExportContinuation(Session));
        var hooks = Party.ToDictionary(who => who, who => (object?)_world.Hooks(PersonOf(who)));
        _out.Add(Event("save", ("path", "user://chapter1_end.json"), ("data", new Ev
        {
            ["hooks"] = hooks, ["trust_familiar"] = State.GetValueOrDefault("full_match_seen") is true,
            ["evidence_public"] = Session.GetProgress("examined").Keys.Cast<object?>().ToArray(),
            ["asked_hand"] = Has("flags", "asked_hand"), ["rested"] = rested, ["detective_name"] = DetectiveName,
        })));
    }

    private void Ending(string kind)
    {
        Session.Set("state", "ended", true);
        var hooked = Party.Where(who => _world.Hooks(PersonOf(who)) > 0).Select(PersonName).ToArray();
        var kept = Party.Where(who => _world.Hooks(PersonOf(who)) == 0).Select(who => who == "detective" ? T("结局·你自己") : PersonName(who)).ToArray();
        var door = Has("flags", "door_open");
        var actions = new List<object?>();
        if (door) actions.Add(new Ev { ["id"] = "continue", ["label"] = "继续学园生活 (C)", ["key"] = "C" });
        actions.Add(new Ev { ["id"] = "restart", ["label"] = "重新开始 (R)", ["key"] = "R" });
        _out.Add(Event("ending", ("title", T($"结局·{kind}·标题")), ("outcome", T($"结局·{kind}·正文")),
            ("rows", new object?[]
            {
                T("结局·钩", ("名单", hooked.Length == 0 ? T("结局·无") : string.Join("、", hooked))),
                T("结局·未交", ("名单", kept.Length == 0 ? T("结局·无") : string.Join("、", kept))),
                T(door ? "结局·续章" : "结局·重开"),
            }),
            ("actions", actions.ToArray())));
        Stage = _content.StageEvent("章末");
    }

    // ----------------------------------------------------------- test jump --

    /// <summary>流程跳转回归：直接把局面摆到某个状态（原宿主 jump_to），不走完整游玩。</summary>
    private IEnumerable<Op> Jump(string state)
    {
        CloseModal();
        _storyLock = false;
        _out.Add(StageOp("stand", ("who", "detective")));
        switch (state)
        {
            case "wake": Stage = "roam"; break;
            case "investigate":
                Stage = "investigate";
                foreach (var who in Companions) _out.Add(StageOp("place", ("who", who), ("to", new Ev { ["anchor"] = "post:" + who })));
                _out.Add(StageOp("place", ("who", "detective"), ("to", new Ev { ["point"] = new object?[] { 230.0, 400.0 } })));
                break;
            case "reversal_one":
                yield return new Run(Jump("investigate"));
                foreach (var id in new[] { "E1", "E2", "E3" }) Examine(id);
                Session.Set("state", "replay_open", true);
                break;
            case "replay_match":
                yield return new Run(Jump("reversal_one"));
                foreach (var e in Content.GetProperty("EVIDENCE").EnumerateObject()) Examine(e.Name);
                Session.Set("experiments", "E8", true);
                Session.Set("flags", "reversal_one", true);
                var result = RunReplay([0, 0, 0, 0, 0, 1]);
                var stored = Session.GetProgress("replay_result");
                stored.Clear();
                foreach (var (key, value) in result) stored[key] = value;
                State["replay_vars"] = new object[] { 0, 0, 0, 0, 0, 1 };
                State["last_run_vars"] = new object[] { 0, 0, 0, 0, 0, 1 };
                State["replay_count"] = Convert.ToInt32(State["replay_count"]) + 1;
                Session.Set("state", "full_match_seen", result["full_match"] is true);
                break;
            case "answer_done":
                yield return new Run(Jump("replay_match"));
                Session.GetProgress("answer_state").Clear();
                foreach (var (key, value) in new Dictionary<string, object> { ["step"] = 5, ["said_trust"] = true, ["shown_familiar"] = true })
                    Session.Set("answer_state", key, value);
                Stage = "answer";
                break;
            default: throw new ArgumentException("未知跳转状态：" + state);
        }
    }
}
