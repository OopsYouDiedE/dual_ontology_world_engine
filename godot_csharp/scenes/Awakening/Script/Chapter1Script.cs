using System.Collections.Immutable;
using DualOntologyWorldEngine.Story;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Awakening;

/// <summary>原 GD 的调查进度与始业之答判定，原样迁移到 C#。</summary>
public sealed class AwakeningContentSession
{
    public ChapterSession Session { get; } = new();
    public AwakeningContentSession()
    {
        Session.Set("state", "stage", "wake");
        Session.Set("state", "detective_name", "侦探");
        Session.Set("state", "sound_rounds", 0);
        Session.Set("state", "replay_count", 0);
        Session.Set("state", "full_match_seen", false);
        Session.Set("state", "replay_open", false);
        Session.Set("state", "ended", false);
        Session.Set("state", "replay_vars", new object[] { -1, -1, -1, -1, -1, 0 });
        Session.Set("state", "last_run_vars", Array.Empty<object>());
        Session.Set("state", "last_sound", Array.Empty<object>());
        Session.Set("state", "last_unheard_ms", -100000L);
    }
    public System.Text.Json.JsonDocument Content { get; private set; } = null!;
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
    public void BeginMeeting(MorningWorld world)
    {
        var steps = new List<StoryStep<MorningWorld, Person>>();
        void Show(string text)
        {
            steps.Add(new StoryStep<MorningWorld, Person>.Text(text));
            steps.Add(WaitForPresentation());
        }
        foreach (var text in new[] { "s2_meet", "@name", "s2_tea", "s3_broadcast", "@board", "s4_rewards", "s5_question" }) Show(text);
        steps.Add(new StoryStep<MorningWorld, Person>.Branch(_ => Session.Has("flags", "rest_chosen"),
            [new StoryStep<MorningWorld, Person>.Text("@rest")],
            [new StoryStep<MorningWorld, Person>.Text("@investigate"), WaitForPresentation(),
             new StoryStep<MorningWorld, Person>.Text("s6_begin"), WaitForPresentation()]));
        Start(world, steps);
    }
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
    public void BeginExperiment(MorningWorld world, string id, string who)
    {
        var steps = new List<StoryStep<MorningWorld, Person>>();
        void Show(string text)
        {
            steps.Add(new StoryStep<MorningWorld, Person>.Text(text));
            steps.Add(WaitForPresentation());
        }
        switch (id)
        {
            case "E8": Show("@sound"); break;
            case "E11":
                // 宿主先核验架沿距离；内容层只接受 R-05 当地的逐人比对。
                if (who is not ("detective" or "huo" or "shen" or "su") ||
                    Session.CharacterRooms.GetValueOrDefault("detective") != "R05" ||
                    !Session.IsLocalTarget("detective", who)) break;
                if (Session.GetProgress("life").TryGetValue(who, out var activity) &&
                    activity is Dictionary<string, object> busy &&
                    busy.GetValueOrDefault("state") is "going" or "holding") break;
                if (!Session.Has("hand_comparisons", who))
                {
                    Show("help_E11_" + who);
                    steps.Add(new StoryStep<MorningWorld, Person>.Update(_ => Session.Set("hand_comparisons", who, true)));
                }
                steps.Add(new StoryStep<MorningWorld, Person>.Update(_ =>
                {
                    if (new[] { "detective", "huo", "shen", "su" }.All(person => Session.Has("hand_comparisons", person)))
                        Session.Set("experiments", id, true);
                }));
                break;
            case "E3" when who == "": break;
            case "E3" when who != "detective":
                Show("@summon:" + who);
                Show("sole_" + who);
                steps.Add(new StoryStep<MorningWorld, Person>.Update(_ => Session.Set("soles_compared", who, who switch
                {
                    "huo" => "霍铎的鞋底：厚底工装靴，粗齿纹，比灰上那枚宽一指。",
                    "shen" => "沈鼎的鞋底：千层布底，没有纹。",
                    "su" => "苏泠的鞋底：软底小皮鞋，比灰上那枚短一截。",
                    _ => throw new ArgumentException(who),
                })));
                break;
            case "E3":
                Show("exp_E3");
                steps.Add(new StoryStep<MorningWorld, Person>.Update(_ => Session.Set("experiments", id, true)));
                steps.Add(new StoryStep<MorningWorld, Person>.Branch(_ => !Session.Has("flags", "reversal_one"),
                    [new StoryStep<MorningWorld, Person>.Update(_ => Session.Set("flags", "reversal_one", true)),
                     new StoryStep<MorningWorld, Person>.Text("s6_reversal"), WaitForPresentation()], []));
                break;
            default:
                Show("exp_" + id);
                steps.Add(new StoryStep<MorningWorld, Person>.Update(_ => Session.Set("experiments", id, true)));
                break;
        }
        Start(world, steps);
    }
    public void BeginHelp(MorningWorld world, string id)
    {
        var who = Content.RootElement.GetProperty("EVIDENCE").GetProperty(id).GetProperty("help").GetString()!;
        Start(world, [new StoryStep<MorningWorld, Person>.Text("@summon:" + who), WaitForPresentation(),
            new StoryStep<MorningWorld, Person>.Text("help_" + id), WaitForPresentation(),
            new StoryStep<MorningWorld, Person>.Update(_ => Session.Set("helped", id, true))]);
    }
    private static Person PersonOf(string who) => who switch { "detective" => Person.Detective, "huo" => Person.Huo, "shen" => Person.Shen, "su" => Person.Su, _ => throw new ArgumentException(who) };
    public void BeginAfterReplay(MorningWorld world)
    {
        var replay = Session.GetProgress("replay_result");
        var fullMatch = (bool)replay["full_match"];
        var refused = ((object[])replay["frames"]).Cast<Dictionary<string, object>>().Any(frame =>
            ((string)((Dictionary<string, object>)frame["thoughts"])["detective"]).Contains("我凭什么信"));
        var steps = new List<StoryStep<MorningWorld, Person>>();
        if (Convert.ToInt32(Session.GetProgress("state")["replay_count"]) == 1 && !fullMatch) steps.AddRange(Show("replay_first"));
        steps.Add(new StoryStep<MorningWorld, Person>.Branch(_ => refused && !Session.Has("flags", "refusal_seen"),
            [new StoryStep<MorningWorld, Person>.Update(_ => { Session.Set("flags", "refusal_seen", true); Session.Set("flags", "v6_revealed", true); }),
             .. Show("replay_refused")], []));
        steps.Add(new StoryStep<MorningWorld, Person>.Branch(_ => fullMatch && !(bool)Session.GetProgress("state")["full_match_seen"],
            [new StoryStep<MorningWorld, Person>.Update(_ => Session.Set("state", "full_match_seen", true)), .. Show("@sfx:answer_ok"),
             .. Show("@summon:huo"), .. Show("@summon:shen"), .. Show("@summon:su"), .. Show("s8_reversal")], []));
        Start(world, steps);
    }
    public void BeginTalk(MorningWorld world, string who)
    {
        var steps = new List<StoryStep<MorningWorld, Person>>();
        if (Session.GetProgress("life").TryGetValue(who, out var value))
        {
            var life = (Dictionary<string, object>)value;
            if (life.GetValueOrDefault("alone") is true && life.GetValueOrDefault("told") is not true)
            {
                steps.Add(new StoryStep<MorningWorld, Person>.Update(_ => life["told"] = true));
                steps.AddRange(Show("alone_" + who));
            }
        }
        steps.AddRange(Show("talk_" + who));
        Start(world, steps);
    }
    public Dictionary<string, string> SoundPositions(string playerRoom)
    {
        var free = new List<string> { "R08", "hall", "foyer", "pantry", "R05" };
        free.Remove(playerRoom);
        var positions = new Dictionary<string, string> { ["detective"] = playerRoom };
        foreach (var (who, preferred) in new[] { ("su", "R08"), ("huo", "hall"), ("shen", "foyer") })
        {
            var room = free.Contains(preferred) ? preferred : free[0];
            positions[who] = room;
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
        return presentation;
    }
    public void Presented(InteractionPlan plan) => plan.Presentation = "";
    public void BeginAnswer(MorningWorld world)
    {
        Session.Set("state", "stage", "answer");
        Session.GetProgress("answer_state").Clear();
        Session.Set("answer_state", "step", 1);
        Start(world, [.. Show("@music:confrontation"), .. Show("answer_open"), new StoryStep<MorningWorld, Person>.Update(BeginAnswerQuestion)]);
    }
    private void BeginAnswerQuestion(MorningWorld world)
    {
        var step = Convert.ToInt32(Session.GetProgress("answer_state")["step"]);
        if (step > 4) { Start(world, Show("@finale")); return; }
        Start(world, [.. Show("answer_q" + step), .. Show("@answerpick"), new StoryStep<MorningWorld, Person>.Update(ResolveAnswer)]);
    }
    private void ResolveAnswer(MorningWorld world)
    {
        var pick = Session.GetProgress("answer_pick");
        if (pick.Count == 0)
        {
            Start(world, [.. Show("answer_pause"), .. Show("@music:door_side"),
                new StoryStep<MorningWorld, Person>.Update(_ => Session.Set("state", "stage", "investigate")), .. Show("@hud")]);
            return;
        }
        var step = Convert.ToInt32(Session.GetProgress("answer_state")["step"]);
        var said = (string)pick["say"];
        var shown = (string)pick["show"];
        var verdict = Judge(step, said, shown);
        var steps = new List<StoryStep<MorningWorld, Person>>();
        if (verdict == "answer_ok" + step) steps.AddRange(Show("@sfx:answer_ok:-4"));
        else if (verdict.StartsWith("answer_wrong")) steps.AddRange(Show("@sfx:answer_wrong:-3"));
        steps.AddRange(Show(verdict));
        if (verdict == "answer_ok" + step) steps.Add(new StoryStep<MorningWorld, Person>.Update(_ => Session.Set("answer_state", "step", step + 1)));
        steps.Add(new StoryStep<MorningWorld, Person>.Update(BeginAnswerQuestion));
        Start(world, steps);
    }
    public void BeginFinale(MorningWorld world)
    {
        Session.Set("state", "stage", "finale");
        Start(world, [.. Show("@finale_prepare"), .. Show("s10_steps"), .. Show("@door_open"),
            new StoryStep<MorningWorld, Person>.Update(_ => Session.Set("flags", "door_open", true)), .. Show("@redraw"), .. Show("s10_prints"),
            new StoryStep<MorningWorld, Person>.Branch(_ => Session.Has("examined", "E11"), Show("s10_hand"), []),
            .. Show("@su_approach"), .. Show("s10_open"), .. Show("@save:false"), .. Show("@ending:complete")]);
    }
    public void BeginRest(MorningWorld world)
    {
        Session.Set("state", "stage", "finale");
        Start(world, [.. Show("@lock"), .. Show("@music:night:4"), .. Show("ending_rest"), .. Show("@save:true"), .. Show("@ending:rest")]);
    }
    public void BeginConfirmRest(MorningWorld world)
        => Start(world, [new StoryStep<MorningWorld, Person>.Update(_ => Session.Remove("flags", "rest_chosen")), .. Show("rest_confirm"),
            new StoryStep<MorningWorld, Person>.Branch(_ => Session.Has("flags", "rest_chosen"),
                [new StoryStep<MorningWorld, Person>.Update(BeginRest)], [])]);
    private static string OwnObject(string who) => who switch { "huo" => "E7", "su" => "E5", "shen" => "E10", _ => throw new ArgumentException(who) };
    private Dictionary<string, object> Life(string who) => (Dictionary<string, object>)Session.GetProgress("life")[who];
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
            var text = (who, touched) switch
            {
                ("huo", true) => "霍铎看上去有点恍惚。粉笔灰沾在他的指节上。",
                ("su", true) => "苏泠的手指一直在膝上轻轻敲着，像在数拍子。",
                ("shen", true) => "沈鼎在擦手。他看了一眼架上的罐子，又转开了。",
                ("huo", false) => "霍铎在铜口旁站着，石板摆得歪了一点。",
                ("su", false) => "苏泠站在门边，音叉换了个位置。",
                _ => "沈鼎在揉面。酵种罐挪过位置。",
            };
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
                    w.Whisper(PersonOf(who), Content.RootElement.GetProperty("WHISPERS").GetProperty(who).GetString()!);
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
        var stage = (string)Session.GetProgress("state")["stage"];
        if (stage == "roam" && room == "hall")
        {
            Session.Set("state", "stage", "meeting");
            return "meeting";
        }
        return stage == "investigate" ? "investigate" : "";
    }
    public System.Text.Json.JsonElement LifeTrace(string id) => Content.RootElement.GetProperty("LIFE_TRACES").GetProperty(id);
    public string[] Examine(MorningWorld world, string id)
    {
        if (Session.Has("examined", id)) return [];
        Session.Set("examined", id, true);
        Session.Observe("detective", id);
        world.Examine(Person.Detective, id);
        var events = new List<string> { "evidence" };
        if (id is "E1" or "E3" && !(bool)Session.GetProgress("state")["replay_open"]) events.Add("unlock");
        if (AcquaintanceEvidence.Contains(id) && V6Open && !Session.Has("flags", "v6_revealed"))
        {
            Session.Set("flags", "v6_revealed", true);
            events.Add("v6");
        }
        return events.ToArray();
    }
    public bool UnlockReplay()
    {
        if ((bool)Session.GetProgress("state")["replay_open"]) return false;
        Session.Set("state", "replay_open", true);
        return true;
    }
    public bool NudgeCard(string id)
    {
        if (id is not ("E1" or "E5" or "E7" or "E10") || Session.Has("nudged", id)) return false;
        Session.Set("nudged", id, true);
        return true;
    }
    public string CardFacts(string id)
    {
        var facts = Content.RootElement.GetProperty("EVIDENCE").GetProperty(id).GetProperty("facts").GetString()!;
        return id == "E12" && Session.Has("examined", "E7") ? facts + "\n这个圆框带短柄的样子，与石板第二行的小图相同。" : facts;
    }
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
            notes.Add($"手掌比对：{compared.Length}/4。已比对：{(compared.Length == 0 ? "尚无" : string.Join("、", compared))}。");
            if (compared.Length == 4) notes.Add(helps.GetProperty("E11").GetString()!);
            else
            {
                if (compared.Length > 0) notes.Add("半枚手印，比已比对者的手掌窄；其他人的尚未核验。");
                notes.Add($"待比对：{string.Join("、", missing)}。请在 R-05 架沿旁先比自己的手；到同伴面前交谈，逐人邀请来这里，再在现场比对。");
            }
        }
        else if (Session.Has("experiments", id))
        {
            if (id == "E8")
            {
                var sound = ((object[])Session.GetProgress("state")["last_sound"]).Cast<Dictionary<string, object>>();
                notes.Add("声路试验：" + string.Join("；", sound.Select(h =>
                {
                    var place = (string)h["room"] switch { "hall" => "铜口旁", "foyer" => "门边", "pantry" => "备餐间", "R05" => "R-05", "R08" => "R-08", _ => "" };
                    return $"{Name((string)h["who"])}（{place}）听到 {h["text"]} {h["direction"]}";
                })));
            }
            else notes.Add("试验：" + evidence.GetProperty("experiment_result").GetString()!.Replace("\n", ""));
        }
        if (id != "E11" && Session.Has("helped", id) && helps.TryGetProperty(id, out var help)) notes.Add(help.GetString()!);
        if (id == "E3") foreach (var who in new[] { "huo", "shen", "su" })
            if (Session.GetProgress("soles_compared").TryGetValue(who, out var sole)) notes.Add((string)sole);
        if (evidence.TryGetProperty("recall", out var recall))
        {
            var who = recall.GetString()!;
            if (Session.GetProgress("recalls").TryGetValue(who, out var fragment)) notes.Add($"回忆（{Name(who)}）：{fragment}");
            else if (Session.GetProgress("refusals").TryGetValue(who, out var refusal)) notes.Add($"{Name(who)}没有碰它：“{refusal}”");
        }
        if (world.IsDisturbed(id)) notes.Add("它被人拿起过，又放回去了，摆得和原来不一样。");
        return notes.ToArray();
    }
    public void BeginWake(MorningWorld world)
    {
        Session.Set("state", "stage", "wake");
        Start(world, [.. Show("@lock"), .. Show("s1_wake"),
            new StoryStep<MorningWorld, Person>.Update(_ => Session.Set("state", "stage", "roam")), .. Show("@unlock"), .. Show("@hud")]);
    }
    private static readonly string[] AcquaintanceEvidence = ["E9", "E10", "E11", "E12"];
    public bool V6Open => Session.Has("flags", "refusal_seen") || AcquaintanceEvidence.Count(id => Session.Has("examined", id)) >= 2;
    public bool SiteVisible(string stage, string id) =>
        (stage == "roam" ? id is "E12" or "E11a" : stage == "investigate") && Session.IsLocalTarget("detective", id);
    public string[] Statements(int[] variables, int replayCount, bool detectiveBolted)
    {
        var list = new List<string>();
        if (Session.Has("flags", "reversal_one") || detectiveBolted) list.Add("me");
        for (var i = 0; i < (V6Open ? 6 : 5); i++)
            if (variables[i] >= 0 && replayCount > 0) list.Add($"V{i + 1}_{variables[i]}");
        if (replayCount > 0 && variables[4] == 0 && variables[5] == 1) list.Add("trust");
        return list.ToArray();
    }
    public string Judge(int step, string said, string shown)
    {
        bool goodSay = false, goodShow = false;
        switch (step)
        {
            case 1:
                goodSay = said == "me";
                goodShow = shown == "E3" || ((shown is "E12" or "E7") && Session.Has("examined", "E12") && Session.Has("examined", "E7"));
                break;
            case 2:
                goodSay = said == "V3_0";
                goodShow = (shown == "E8" && Session.Has("experiments", "E8")) || shown == "E5";
                if (goodSay && shown == "E8" && !Session.Has("experiments", "E8")) return "answer_need_test";
                break;
            case 3: goodSay = said == "V4_0"; goodShow = shown is "E7" or "E6"; break;
            case 4: goodSay = said is "V5_0" or "V6_1" or "trust"; goodShow = shown is "E9" or "E10" or "E11" or "E7"; break;
        }
        if (goodSay && goodShow) return $"answer_ok{step}";
        if (Content is not null)
            Session.Set("state", "wrong_name", !goodSay
                ? (Content.RootElement.GetProperty("STATEMENTS").TryGetProperty(said, out var statement) ? statement.GetString()! : "")
                : Content.RootElement.GetProperty("EVIDENCE").GetProperty(shown).GetProperty("name").GetString()!);
        return !goodSay ? "answer_wrong_say" : "answer_wrong_show";
    }
    public bool CanExperiment(string id) => !Session.Has("experiments", id) || id == "E8";
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
        var traces = MorningTraces.Evidence;
        Evidence("E1", "闩上的隔离门", Room.Foyer, ("内闩", traces.InnerBoltDown ? 1 : 0), ("外闩", 1), ("猫眼糊灰", 1));
        Evidence("E2", "门底的灰与木楔", Room.Foyer, ("扇形灰", traces.AshFan ? 1 : 0), ("楔面沾灰", traces.WedgeTopSooted ? 1 : 0), ("楔在角落", traces.WedgeInCorner ? 1 : 0));
        Evidence("E3", "灰上的靴印", Room.Foyer, ("马球靴印朝插闩", 1));
        Evidence("E4", "门底的湿布", Room.Foyer, ("沿门底压成直线", traces.Cloth == 1 ? 1 : 0), ("压住鞋跟", traces.ClothOverHeel ? 1 : 0));
        Evidence("E5", "音叉", Room.Foyer, ("门框凹痕", traces.FrameDent ? 1 : 0));
        Evidence("E6", "苏泠的乐谱", Room.R08, ("第一行", traces.ScoreLine1), ("第二行", traces.ScoreLine2 ? 1 : 0));
        Evidence("E7", "霍铎的石板", Room.Hall, ("第一行", traces.SlateLine1), ("第二行", traces.SlateLine2));
        Evidence("E8", "铜口与管接头", Room.Hall, ("接头松动", 1));
        Evidence("E9", "四只杯子", Room.Pantry, ("按人放糖", traces.CupSugar));
        Evidence("E10", "酵种罐", Room.Pantry, ("层层旧面痕", 1));
        Evidence("E11", "架上的无尘方框", Room.R05, ("窄手印", 1));
        Evidence("E12", "侦探的外衣口袋", Room.R05, ("放大镜", 1));
        foreach (var who in Enum.GetValues<Person>())
        {
            var soul = new SoulEssence(Names.Of(who));
            var body = new Essence(Names.Of(who), ProjectionKind.Body);
            body.Add(new SpatialProperty { Space = _space[who == Person.Detective ? Room.R05 : Room.Hall] });
            soul.Relations.Add((Relations.Controls, body));
            _runtime.Project(soul, Names.Of(who) + "之魂");
            _runtime.Project(body, Names.Of(who));
            _souls[who] = soul;
            _bodies[who] = body;
            _hooks[who] = 0;
            // 醒来时只记得登船。
            Observation.Observe(soul, new ObservationInput("登船", ProjectionKind.Event)
            {
                Source = ObservationSource.Introspection, Existence = Truth.True, Details = "只记得上船。"
            });
            _desires[who] = Observation.Observe(soul, new ObservationInput("想要的：" + DesireOf(who), ProjectionKind.Concept)
            {
                Source = ObservationSource.Introspection, Existence = Truth.True,
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Strength, 1)
            });
        }
        foreach (var memory in Memories.Where(m => m.Who != Person.Detective)) WriteMind(memory);
    }

}

/// <summary>第一章完整剧本：原世界初始化、调查流程和原对白随程序集编译。</summary>
public sealed class Chapter1Script
{
    public string Id => "chapter1";
    public string Title => "门的这一侧";
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
