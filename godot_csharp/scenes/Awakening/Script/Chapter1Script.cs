using System.Collections.Immutable;
using DualOntologyWorldEngine.Story;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Awakening;

/// <summary>原 GD 的调查进度与始业之答判定，原样迁移到 C#。</summary>
public sealed partial class AwakeningContentSession
{
    public ChapterSession Session { get; } = new();
    public AwakeningContentSession()
    {
        Session.Set("state", "stage", StageEvent("初始"));
        Session.Set("state", "detective_name", Chapter1Data.Text("报名·默认名"));
        Session.Set("state", "sound_rounds", 0);
        Session.Set("state", "replay_count", 0);
        Session.Set("state", "full_match_seen", false);
        Session.Set("state", "replay_open", false);
        Session.Set("state", "ended", false);
        Session.Set("state", "replay_vars", new object[] { -1, -1, -1, -1, -1, -1 });
        Session.Set("state", "last_run_vars", Array.Empty<object>());
        Session.Set("state", "last_sound", Array.Empty<object>());
        Session.Set("state", "last_unheard_ms", -100000L);
    }
    public System.Text.Json.JsonDocument Content { get; private set; } = System.Text.Json.JsonDocument.Parse(ReadContent());
    private static string ReadContent()
    {
        using var stream = typeof(Chapter1Data).Assembly.GetManifestResourceStream("Awakening.Content.json")!;
        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadToEnd();
    }
    public void LoadContent(string text) => Content = System.Text.Json.JsonDocument.Parse(text);
    public sealed class InteractionPlan
    {
        internal StoryRuntime<MorningWorld, Person> Story = null!;
        internal string Presentation = "";
        public bool Finished => Presentation == "" && !Story.IsRunning;
    }
    private InteractionPlan _activePlan = new();
    private StoryRuntime<MorningWorld, Person> _story { get => _activePlan.Story; set => _activePlan.Story = value; }
    private string _presentation { get => _activePlan.Presentation; set => _activePlan.Presentation = value; }
    public InteractionPlan StartPlan(Action begin)
    {
        var plan = new InteractionPlan();
        _activePlan = plan;
        begin();
        return plan;
    }
    private StoryStep<MorningWorld, Person> WaitForPresentation()
    {
        var plan = _activePlan;
        return new StoryStep<MorningWorld, Person>.Wait(_ => plan.Presentation == "");
    }
    private StoryStep<MorningWorld, Person> PresentCalculated(Func<string> text)
    {
        var plan = _activePlan;
        return new StoryStep<MorningWorld, Person>.Update(_ => plan.Presentation = text());
    }
    public string NextMeeting() => NextMeeting(_activePlan);
    public void Presented() => Presented(_activePlan);
    public void BeginMeeting(MorningWorld world) => StartFlow(world, "相遇");
    private void Start(MorningWorld world, IReadOnlyList<StoryStep<MorningWorld, Person>> steps)
    {
        var plan = _activePlan;
        plan.Presentation = "";
        _story = new(world, new()
        {
            Text = (_, text) => plan.Presentation = text,
            Present = (_, presentation) => plan.Presentation = presentation.Content,
            Busy = (w, actor) => w.StoryBusy(actor),
            Execute = (w, actor, effect) => w.SubmitStory(actor, effect),
            Timeline = w => w.StoryTimeline,
        }, [new("interaction", _ => true, steps)]);
    }
    /// <summary>做试验：流程按证物编号取“实验·E3”之类，没有专写的就用“实验·默认”。</summary>
    // STORY-HARDCODE[AWK-028][logic][production]: E8 只开听位菜单且 E3 选对象前不结算；其余试验按证物编号调度。
    public void BeginExperiment(MorningWorld world, string id, string who)
    {
        // E8 only opens the placement menu; the actual sound run settles its own time.
        if (id != "E8" && !(id == "E3" && who == "")) ConsumeAction("试验", id);
        StartFlow(world, HasFlow("实验·" + id) ? "实验·" + id : "实验·默认", ("物", id), ("人", who));
    }
    public void BeginHelp(MorningWorld world, string id, string? who = null)
    {
        ConsumeAction("协助", id);
        // 请来辨认的人亲眼看了这件东西。
        if (Content.RootElement.GetProperty("EVIDENCE").GetProperty(id).TryGetProperty("help", out var helper))
            world.Examine(PersonOf(who ?? helper.GetString()!), id);
        if (who is null) StartFlow(world, "请人看看", ("物", id));
        else StartFlow(world, "请人看看", ("物", id), ("人", who));
    }
    private static Person PersonOf(string who) => who switch { "detective" => Person.Detective, "huo" => Person.Huo, "shen" => Person.Shen, "su" => Person.Su, _ => throw new ArgumentException(who) };
    public void BeginAfterReplay(MorningWorld world) => StartFlow(world, "重演之后");
    public void BeginTalk(MorningWorld world, string who)
    {
        ConsumeAction("交谈", who);
        StartFlow(world, "交谈", ("人", who));
    }
    /// <summary>声路试验里同伴各站哪里：偏好与候补顺序来自 chapter_01.story 的“同伴站位”“站位候补”。</summary>
    public Dictionary<string, string> SoundPositions(string playerRoom)
    {
        var free = Chapter1Data.Strings("SPOT_ORDER").ToList();
        free.Remove(playerRoom);
        var positions = new Dictionary<string, string> { ["detective"] = playerRoom };
        foreach (var preference in Content.RootElement.GetProperty("COMPANION_SPOTS").EnumerateObject())
        {
            var room = free.Contains(preference.Value.GetString()!) ? preference.Value.GetString()! : free[0];
            positions[preference.Name] = room;
            free.Remove(room);
        }
        return positions;
    }
    private StoryStep<MorningWorld, Person>[] Show(string text) =>
        [new StoryStep<MorningWorld, Person>.Text(text), WaitForPresentation()];
    public void ApplyRecall(MorningWorld world, string id, string who)
    {
        var memory = world.Recall(id);
        if (memory is null) return;
        Session.Set("recalls", who, memory.Fragment);
        foreach (var variable in memory.Confirms) Session.Set("recall_marks", variable.ToString(), who);
    }
    // STORY-HARDCODE[AWK-029][logic][production]: 侦探与三名同伴的旧物回忆分别走固定角色流程。
    public void BeginRecall(MorningWorld world, string id)
    {
        var who = Content.RootElement.GetProperty("EVIDENCE").GetProperty(id).GetProperty("recall").GetString()!;
        var steps = new List<StoryStep<MorningWorld, Person>>();
        if (who == "detective")
        {
            steps.Add(new StoryStep<MorningWorld, Person>.Update(_ => Session.Remove("flags", "recall_yes")));
            steps.AddRange(Show("recall_ask_detective"));
            steps.Add(new StoryStep<MorningWorld, Person>.Branch(_ => Session.Has("flags", "recall_yes"),
                [new StoryStep<MorningWorld, Person>.Update(w => ApplyRecall(w, id, who)), .. Show("recall_detective")], []));
        }
        else
        {
            steps.AddRange(Show("@summon:" + who));
            var resolve = new List<StoryStep<MorningWorld, Person>>();
            resolve.Add(new StoryStep<MorningWorld, Person>.Branch(w => Session.Has("flags", "board_read") && !w.KnowsRule1(PersonOf(who)),
                [new StoryStep<MorningWorld, Person>.Update(_ => Session.Remove("flags", "show_rule")), .. Show("recall_offer_" + who),
                 new StoryStep<MorningWorld, Person>.Branch(_ => Session.Has("flags", "show_rule"),
                    [new StoryStep<MorningWorld, Person>.Update(w => w.ShowRule1(PersonOf(who))), .. Show("recall_shown_" + who)], [])], []));
            resolve.AddRange(ResolveRecallSteps(id, who));
            steps.Add(new StoryStep<MorningWorld, Person>.Branch(_ => !Session.Has("decided", who), resolve, []));
        }
        Start(world, steps);
    }
    public void BeginResolveRecall(MorningWorld world, string id, string who) => Start(world, ResolveRecallSteps(id, who));
    private IReadOnlyList<StoryStep<MorningWorld, Person>> ResolveRecallSteps(string id, string who)
    {
        var person = PersonOf(who);
        var choice = "refuse";
        void Decide(MorningWorld world, bool final)
        {
            var decision = world.DecideRecall(id)!;
            choice = decision.Choice switch { RecallChoice.Touch => "touch", RecallChoice.Hesitate => "hesitate", _ => "refuse" };
            Session.Set("recall_decision", "thought", decision.Thought);
            Session.Set("recall_decision", "choice", choice);
            if (final && choice == "hesitate") choice = "refuse";
        }
        return [new StoryStep<MorningWorld, Person>.Update(w => Decide(w, false)),
            new StoryStep<MorningWorld, Person>.Branch(_ => choice == "hesitate",
                [new StoryStep<MorningWorld, Person>.Update(_ => { foreach (var key in new[] { "advice_touch", "advice_decline", "advice_own" }) Session.Remove("flags", key); }),
                 .. Show("recall_hesitate_" + who), new StoryStep<MorningWorld, Person>.Update(w =>
                 { w.Advise(person, Session.Has("flags", "advice_touch") ? "touch" : Session.Has("flags", "advice_decline") ? "decline" : "own"); Decide(w, true); })], []),
            new StoryStep<MorningWorld, Person>.Update(_ =>
            { Session.Set("flags", "recall_" + choice + "_" + who, true); if (choice != "touch") Session.Set("refusals", who, Session.GetProgress("recall_decision")["thought"]); }),
            PresentCalculated(() => "recall_" + choice + "_" + who),
            WaitForPresentation(),
            new StoryStep<MorningWorld, Person>.Branch(_ => choice == "touch",
                [new StoryStep<MorningWorld, Person>.Update(w => ApplyRecall(w, id, who)), .. Show("recall_" + who)], []),
            new StoryStep<MorningWorld, Person>.Update(w => { w.Settle(person); Session.Set("decided", who, true); })];
    }
    public string NextMeeting(InteractionPlan plan)
    {
        _activePlan = plan;
        StoryRuntime<MorningWorld, Person>? previous;
        do { previous = _story; _story!.Poll(); } while (_presentation == "" && !ReferenceEquals(previous, _story));
        var presentation = _presentation;
        if (presentation != "") PresentScene(presentation);
        return presentation;
    }
    public void Presented(InteractionPlan plan) => plan.Presentation = "";
    public void BeginAnswer(MorningWorld world) => StartFlow(world, "答辩·开始");
    public void BeginFinale(MorningWorld world) => StartFlow(world, "终局");
    public string OwnObject(string who) => Content.RootElement.GetProperty("OWN_OBJECTS").GetProperty(who).GetString()!;
    private bool ContentList(string key, string id) => Content.RootElement.GetProperty(key).EnumerateArray().Any(item => item.GetString() == id);
    private Dictionary<string, object> Life(string who) => (Dictionary<string, object>)Session.GetProgress("life")[who];
    /// <summary>声路试验里听者所站位置的称呼，来自 chapter_01.story 的“声路站位”。</summary>
    public static string SoundSpot(string room)
        => Chapter1Data.Root.GetProperty("SOUND_SPOTS").TryGetProperty(room, out var label) ? label.GetString()! : room == "" ? "" : Chapter1Data.RoomName(room);
    public Intention? Consider(MorningWorld world, string who)
        => Session.Has("life", who) || Session.Has("decided", who) || Session.Has("recalls", who) ? null : world.Intend(PersonOf(who));
    public void BeginReach(string who, string thought) => Session.Set("life", who, new Dictionary<string, object> { ["state"] = "going", ["thought"] = thought });
    public bool Reach(MorningWorld world, string who)
    {
        if (!world.Reach(PersonOf(who))) { Session.Remove("life", who); return false; }
        if (Session.Has("life", who)) Life(who)["state"] = "holding";
        return true;
    }
    public void BeginPresentReach(MorningWorld world, string who)
    {
        var person = PersonOf(who);
        Start(world, [new StoryStep<MorningWorld, Person>.Update(_ => { Session.Remove("flags", "reach_decline"); Session.Remove("flags", "reach_rule"); }),
            .. Show("reach_" + who), new StoryStep<MorningWorld, Person>.Branch(_ => Session.Has("flags", "reach_decline"),
                [new StoryStep<MorningWorld, Person>.Update(w => w.Advise(person, "decline"))],
                [new StoryStep<MorningWorld, Person>.Branch(_ => Session.Has("flags", "reach_rule"),
                    [new StoryStep<MorningWorld, Person>.Update(w => w.ShowRule1(person)), .. Show("recall_shown_" + who)], [])]),
            .. ResolveRecallSteps(OwnObject(who), who), new StoryStep<MorningWorld, Person>.Update(_ => { Life(who)["state"] = "done"; Life(who)["alone"] = false; }),
            .. Show("@finish:" + who)]);
    }
    private readonly Dictionary<string, RecallDecision> _aloneDecisions = new();
    public void PrepareAlone(MorningWorld world, string who) => _aloneDecisions[who] = world.DecideAlone(OwnObject(who))!;
    public void CompleteAlone(MorningWorld world, string who)
    {
        var decision = _aloneDecisions[who];
        _aloneDecisions.Remove(who);
        var life = Life(who);
        life["alone"] = true;
        life["choice"] = decision.Choice == RecallChoice.Touch ? "touch" : decision.Choice == RecallChoice.Hesitate ? "hesitate" : "refuse";
        life["thought"] = decision.Thought;
        if (decision.Choice == RecallChoice.Touch) life["fragment"] = world.Recall(OwnObject(who))?.Fragment ?? "";
        world.Settle(PersonOf(who));
        Session.Set("decided", who, true);
        life["state"] = "done";
    }
    // STORY-HARDCODE[AWK-030][logic][production]: 固定同伴顺序、调查痕迹阈值与离场低语触发条件属于本章流程。
    public void BeginRoomChanged(MorningWorld world, string room, long now)
    {
        var steps = new List<StoryStep<MorningWorld, Person>>();
        foreach (var who in new[] { "huo", "su", "shen" })
        {
            if (!Session.GetProgress("life").TryGetValue(who, out var value)) continue;
            var life = (Dictionary<string, object>)value;
            if (life.GetValueOrDefault("alone") is not true || life.GetValueOrDefault("noticed") is true || Session.CharacterRooms.GetValueOrDefault(who) != room) continue;
            steps.Add(new StoryStep<MorningWorld, Person>.Update(_ => life["noticed"] = true));
            var touched = life.GetValueOrDefault("choice") is "touch";
            var text = Chapter1Data.Text((touched ? "旁白·碰过·" : "旁白·没碰·") + who);
            steps.AddRange(Show("@aside:" + text));
        }
        if ((string)Session.GetProgress("state")["stage"] == "investigate" && Session.GetProgress("examined").Count >= 4 &&
            now - Convert.ToInt64(Session.GetProgress("state")["last_unheard_ms"]) >= 20000)
        {
            foreach (var who in new[] { "huo", "su", "shen" })
            {
                var obj = OwnObject(who);
                if (Session.Has("nudged", obj) || Session.Has("decided", who) || Session.Has("life", who) || Session.Has("recalls", who) || Session.CharacterRooms.GetValueOrDefault(who) == room) continue;
                steps.Add(new StoryStep<MorningWorld, Person>.Update(w =>
                {
                    Session.Set("state", "last_unheard_ms", now);
                    Session.Set("nudged", obj, true);
                }));
                steps.AddRange(Show("@consider:" + who));
                break;
            }
        }
        Start(world, steps);
    }
    // 房间参数由宿主在脚下真实室内地板上核验；门槛不能报告为到场。
    public string RoomEntry(string room)
    {
        if (room == "" || Session.Has("visited_rooms", room)) return "";
        Session.Set("visited_rooms", room, true);
        return "";
    }
    // 同样只接受宿主已核验的实际进入房间。
    public string RoomTrigger(string room)
    {
        if (room == "") return "";
        ObserveClockRoom(room);
        var stage = Stage(CurrentStage);
        if (stage.GetProperty("enter").TryGetProperty(room, out var enter))
        {
            if (enter.TryGetProperty("flow", out var flow)) return "flow:" + flow.GetString();
            Session.Set("state", "stage", enter.GetProperty("stage").GetString()!);
            return enter.GetProperty("signal").GetString()!;
        }
        return stage.GetProperty("room_events").GetBoolean() ? "room" : "";
    }
    public System.Text.Json.JsonElement LifeTrace(string id) => Content.RootElement.GetProperty("LIFE_TRACES").GetProperty(id);
    // STORY-HARDCODE[AWK-031][logic][production]: E1/E3 开放重演、指定熟悉旁证揭示 V6 的触发映射。
    public string[] Examine(MorningWorld world, string id, IReadOnlySet<string>? visiblePeople = null)
    {
        if (Session.Has("examined", id)) return [];
        ConsumeAction("检视", id);
        Session.Set("examined", id, true);
        Session.Observe("detective", id);
        world.Examine(Person.Detective, id);
        ShareExamination(world, id, visiblePeople);
        var events = new List<string> { "evidence" };
        if (id is "E1" or "E3" && !(bool)Session.GetProgress("state")["replay_open"]) events.Add("unlock");
        if (ContentList("ACQUAINTANCE_EVIDENCE", id) && RevealV6()) events.Add("v6");
        return events.ToArray();
    }
    public bool RevealV6()
    {
        if (CurrentStage != "investigate" || State("replay_open", false) is not true || !V6Open || Session.Has("flags", "v6_revealed")) return false;
        Session.Set("flags", "v6_revealed", true);
        return true;
    }
    public bool UnlockReplay()
    {
        if ((bool)Session.GetProgress("state")["replay_open"]) return false;
        Session.Set("state", "replay_open", true);
        return true;
    }
    public bool NudgeCard(string id)
    {
        if (!ContentList("RECALL_OBJECTS", id) || Session.Has("nudged", id)) return false;
        Session.Set("nudged", id, true);
        return true;
    }
    // STORY-HARDCODE[AWK-032][schema][production]: E12 展示 E7 石板小图是本案证物间固定关联。
    public string CardFacts(string id)
    {
        var facts = Content.RootElement.GetProperty("EVIDENCE").GetProperty(id).GetProperty("facts").GetString()!;
        return id == "E12" && Session.Has("examined", "E7") ? facts + "\n" + Chapter1Data.Text("卡片·E12·石板小图") : facts;
    }
    // STORY-HARDCODE[AWK-033][logic][production]: E11 四人手印进度及 E8 声路试验笔记为本案专属摘要逻辑。
    public string[] CardNotes(MorningWorld world, string id)
    {
        var notes = new List<string>();
        var evidence = Content.RootElement.GetProperty("EVIDENCE").GetProperty(id);
        var helps = Content.RootElement.GetProperty("HELP_NOTES");
        string Name(string who) => Content.RootElement.GetProperty("PERSON_NAMES").GetProperty(who).GetString()!;
        if (id == "E11")
        {
            var people = new[] { "detective", "huo", "shen", "su" };
            var compared = people.Where(person => Session.Has("hand_comparisons", person)).Select(Name).ToArray();
            var missing = people.Where(person => !Session.Has("hand_comparisons", person)).Select(Name).ToArray();
            // 只记已经比过的手；还没想到要比的人不列出来，结论等四只手都比过才下。
            if (compared.Length == 4) notes.Add(helps.GetProperty("E11").GetString()!);
            else if (compared.Length > 0)
            {
                notes.Add(Chapter1Data.Text("笔记·手印·进度", ("名单", string.Join("、", compared))));
                notes.Add(Chapter1Data.Text("笔记·手印·较窄"));
            }
        }
        else if (Session.Has("experiments", id))
        {
            if (id == "E8")
            {
                var sound = ((object[])Session.GetProgress("state")["last_sound"]).Cast<Dictionary<string, object>>();
                notes.Add(Chapter1Data.Text("笔记·声路", ("列表", string.Join("；", sound.Select(h => Chapter1Data.Text((string)h["direction"] == "" ? "笔记·声路·一条无方向" : "笔记·声路·一条",
                    ("人名", Name((string)h["who"])), ("地点", SoundSpot((string)h["room"])), ("文字", ((string)h["text"]).TrimEnd('。')), ("方向", ((string)h["direction"]).TrimEnd('。'))))))));
            }
            else notes.Add(Chapter1Data.Text("笔记·试验", ("结果", evidence.GetProperty("experiment_result").GetString()!.Replace("\n", ""))));
        }
        if (id != "E11" && Session.Has("helped", id) && helps.TryGetProperty(id, out var help)) notes.Add(help.GetString()!);
        if (id == "E3") foreach (var who in new[] { "huo", "shen", "su" })
            if (Session.GetProgress("soles_compared").TryGetValue(who, out var sole)) notes.Add((string)sole);
        if (evidence.TryGetProperty("recall", out var recall))
        {
            var who = recall.GetString()!;
            if (Session.GetProgress("recalls").TryGetValue(who, out var fragment)) notes.Add(Chapter1Data.Text("笔记·回忆", ("人名", Name(who)), ("片段", (string)fragment)));
            else if (Session.GetProgress("refusals").TryGetValue(who, out var refusal)) notes.Add(Chapter1Data.Text("笔记·不碰", ("人名", Name(who)), ("想法", (string)refusal)));
        }
        if (world.IsDisturbed(id)) notes.Add(Chapter1Data.Text("笔记·被动过"));
        return notes.ToArray();
    }
    public void BeginWake(MorningWorld world) => StartFlow(world, "醒来");
    public bool KnownMaterial(string id) => Session.Has("examined", id) || Session.GetProgress("testimony").ContainsKey(id);
    public bool V6Open => Session.Has("flags", "refusal_seen") || Content.RootElement.GetProperty("ACQUAINTANCE_EVIDENCE").EnumerateArray().Count(item => KnownMaterial(item.GetString()!)) >= 2;
    public bool SiteVisible(string stage, string id)
    {
        var sites = Stage(stage).GetProperty("sites");
        var shown = sites.ValueKind == System.Text.Json.JsonValueKind.String || sites.EnumerateArray().Any(site => site.GetString() == id);
        return shown && Session.IsLocalTarget("detective", id);
    }
    public string[] Statements(int[] variables, int replayCount, bool detectiveBolted)
    {
        // 能说出口的结论来自侦探自己的认知：查过某个处境的任一证物，这一问的各种说法就都能说（对错都有，不替玩家选）；
        // 重演里设定过的处境同样算想过。不要求先重演。
        var list = new List<string>();
        if (Session.Has("flags", "reversal_one") || detectiveBolted || (KnownMaterial("E12") && KnownMaterial("E7"))) list.Add("me");
        var known = new bool[6];
        var index = 0;
        foreach (var variable in Content.RootElement.GetProperty("VARIABLES").EnumerateArray())
        {
            if (index >= 6) break;
            var open = index < 5 || V6Open;
            var thought = replayCount > 0 && variables.Length > index && variables[index] >= 0;
            var seen = variable.GetProperty("evidence").EnumerateArray().Any(e => KnownMaterial(e.GetString()!));
            if (open && (seen || thought))
            {
                known[index] = true;
                var count = variable.GetProperty("options").GetArrayLength();
                for (var k = 0; k < count; k++) list.Add($"V{index + 1}_{k}");
            }
            index++;
        }
        if (known[4] && known[5]) list.Add("trust");
        return list.ToArray();
    }
    /// <summary>始业之答的一问：答对与否由 chapter_01.story 的“判定”表达式决定；这里只负责记下错在哪里，供铜口的回应引用。</summary>
    public string Judge(int step, string said, string shown)
    {
        bool goodSay = false, goodShow = false;
        (_judgeSaid, _judgeShown) = (said, shown);
        try
        {
            if (Content.RootElement.GetProperty("VERDICTS").TryGetProperty(step.ToString(), out var verdict))
            {
                if (verdict.TryGetProperty("need_test", out var need) && Truthy(Eval(need, null, NoArgs))) return "answer_need_test";
                goodSay = Truthy(Eval(verdict.GetProperty("say_ok"), null, NoArgs));
                goodShow = Truthy(Eval(verdict.GetProperty("show_ok"), null, NoArgs));
            }
        }
        finally { (_judgeSaid, _judgeShown) = (null, null); }
        if (goodSay && goodShow) return $"answer_ok{step}";
        // 补救：支撑这一命题的证物在现实中都已毁去，代管者要的是有人作答，便凭记得的原样认下正确的陈述。
        if (goodSay && Content.RootElement.GetProperty("VERDICTS").GetProperty(step.ToString()).TryGetProperty("support", out var support)
            && support.EnumerateArray().All(e => Session.Has("destroyed", e.GetString()!)))
        {
            Session.Set("rescue", step.ToString(), Chapter1Data.Text("答辩·证物毁去", ("陈述", Content.RootElement.GetProperty("VERDICTS").GetProperty(step.ToString()).GetProperty("proposition").GetString()!)));
            return "answer_rescue";
        }
        Session.Set("state", "wrong_name", !goodSay
            ? (Content.RootElement.GetProperty("STATEMENTS").TryGetProperty(said, out var statement) ? statement.GetString()! : "")
            : Content.RootElement.GetProperty("EVIDENCE").TryGetProperty(shown, out var card) ? card.GetProperty("name").GetString()! : "");
        return !goodSay ? "answer_wrong_say" : "answer_wrong_show";
    }
    /// <summary>毁掉一件证物；没有这件东西时记为“未建模”，给保守结果（什么也不变）。</summary>
    public bool DestroyEvidence(MorningWorld world, string id)
    {
        // 动手毁物与拧接头都是实在的动作，按试验计时。
        ConsumeAction("试验", "destroy:" + id);
        if (world.Destroy(id)) { Session.Set("destroyed", id, true); return true; }
        Session.Set("unmodeled", "destroy:" + id, Chapter1Data.Text("系统·缺少证物", ("物", id)));
        return false;
    }
    /// <summary>拧紧或拆松一个接头；没有这个接头时记为“未建模”。</summary>
    public bool SetJoint(MorningWorld world, string name, bool loose)
    {
        ConsumeAction("试验", "joint:" + name);
        if (world.SetJoint(name, loose)) return true;
        Session.Set("unmodeled", "joint:" + name, Chapter1Data.Text("系统·缺少接头", ("物", name)));
        return false;
    }
    public bool CanExperiment(string id) => id == "E8" ? SoundTestThinkable : !Session.Has("experiments", id);
    /// <summary>侦探手里有了两种相反的说法（石板上的“闩门”与乐谱或音叉指向的“开门”、门框后的空管），才会想到让铜口说句话、换地方听。</summary>
    public bool SoundTestThinkable => Session.Has("examined", "E7") && (Session.Has("examined", "E6") || Session.Has("helped", "E5"));
    /// <summary>旧物在本人手里闪过一下之后，才有“要不要往下想”。闪回由身体接触引起，不由旁人提议。</summary>
    public bool CanFlash(string id)
    {
        if (!Content.RootElement.GetProperty("EVIDENCE").GetProperty(id).TryGetProperty("recall", out var recall)) return false;
        var who = recall.GetString()!;
        return CanRecall(who, false);
    }
    public bool CanHelp(string id) => !Session.Has("helped", id);
    public bool CanRecall(string who, bool moving) => !Session.Has("recalls", who) && !Session.Has("decided", who) && !moving;
}

public sealed partial class MorningWorld
{
    public MorningWorld()
    {
        _session = new SimulationSession(_runtime);
        foreach (var room in Enum.GetValues<Room>())
        {
            var space = new Essence(Names.Of(room), ProjectionKind.Space);
            _space[room] = space;
            _runtime.Project(space, Names.Of(room));
        }
        // STORY-HARDCODE[AWK-034][logic][production]: 从本章固定案件真相建立清晨证物实体与醒来人物认知。
        var truth = Chapter1Data.Root.GetProperty("TRUTH");
        int TruthValue(string field, int? equals)
        {
            var value = truth.GetProperty(field);
            var number = value.ValueKind is System.Text.Json.JsonValueKind.True ? 1 : value.ValueKind is System.Text.Json.JsonValueKind.False ? 0 : value.GetInt32();
            return equals is { } expected ? (number == expected ? 1 : 0) : number;
        }
        foreach (var item in Chapter1Data.Root.GetProperty("EVIDENCE").EnumerateObject())
        {
            var data = item.Value;
            Evidence(item.Name, (data.TryGetProperty("mind_name", out var mindName) ? mindName : data.GetProperty("name")).GetString()!,
                (Room)Array.IndexOf(Names.RoomKeys, data.GetProperty("room").GetString()),
                data.GetProperty("world_facts").EnumerateArray().Select(fact => (fact.GetProperty("tag").GetString()!,
                    fact.TryGetProperty("value", out var fixedValue) ? fixedValue.GetInt32()
                        : TruthValue(fact.GetProperty("truth").GetString()!, fact.TryGetProperty("equals", out var eq) ? eq.GetInt32() : null))).ToArray());
        }
        foreach (var who in Enum.GetValues<Person>())
        {
            var soul = new SoulEssence(Names.Of(who));
            var body = new Essence(Names.Of(who), ProjectionKind.Body);
            body.Add(new SpatialProperty { Space = _space[who == Person.Detective ? Room.R05 : Room.Hall] });
            soul.Relations.Add((Relations.Controls, body));
            _runtime.Project(soul, Chapter1Data.Text("认知·魂名", ("名", Names.Of(who))));
            _runtime.Project(body, Names.Of(who));
            _souls[who] = soul;
            _bodies[who] = body;
            _hooks[who] = 0;
            // 醒来时只记得登船。
            Observation.Observe(soul, new ObservationInput(Chapter1Data.Text("认知·登船·名"), ProjectionKind.Event)
            {
                Source = ObservationSource.Introspection, Existence = Truth.True, Details = Chapter1Data.Text("认知·登船·详")
            });
            _desires[who] = Observation.Observe(soul, new ObservationInput(Chapter1Data.Text("认知·欲望", ("欲望", DesireOf(who))), ProjectionKind.Concept)
            {
                Source = ObservationSource.Introspection, Existence = Truth.True,
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Strength, 1)
            });
        }
        foreach (var memory in Memories.Where(m => m.Who != Person.Detective)) WriteMind(memory);
        InitializeEcology();
    }

}

/// <summary>第一章完整剧本：原世界初始化、调查流程和原对白随程序集编译。</summary>
public sealed class Chapter1Script
{
    public string Id => "chapter1";
    public string Title => Chapter1Data.Text("章名");
    public MorningWorld CreateWorld() => new MorningWorld();
    public Chapter1Dialogue CreateDialogue(AwakeningContentSession content, MorningWorld world) => new(content, world);
    public Chapter1Interaction CreateInteraction(AwakeningContentSession content, MorningWorld world) => new(content, world);
    public AwakeningContentSession CreateContent()
    {
        var content = new AwakeningContentSession();
        content.LoadContent(ReadText("Awakening.Content.json"));
        return content;
    }
    private static string ReadText(string name)
    {
        using var stream = typeof(Chapter1Script).Assembly.GetManifestResourceStream(name)!;
        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadToEnd();
    }
}
