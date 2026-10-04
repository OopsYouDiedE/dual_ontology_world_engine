using DualOntologyWorldEngine.Awakening;
using DualOntologyWorldEngine.Story;
using Godot;
using GArray = Godot.Collections.Array;
using GDict = Godot.Collections.Dictionary;

/// <summary>
/// GDScript 与重演引擎之间的桥。清晨的现实（MorningWorld）在整局中只有一个；
/// 每次 RunReplay 都在 NightReplay 中新建独立的昨夜世界，不触碰现实。
/// </summary>
public partial class AwakeningEngine : Node
{
    private static readonly Chapter1Script Chapter = new();
    private MorningWorld _morning = Chapter.CreateWorld();
    private AwakeningContentSession _content = Chapter.CreateContent();

    public GDict Content()
    {
        return Json.ParseString(_content.Content.RootElement.GetRawText()).AsGodotDictionary();
    }

    public string DialogueText() => Chapter.DialogueText;
    public string ScriptId() => Chapter.Id;
    public string ScriptAssembly() => _morning.GetType().Assembly.GetName().Name!;

    public GDict Progress(string group)
    {
        var result = new GDict();
        foreach (var (key, value) in _content.Session.GetProgress(group))
            result[group == "recall_marks" ? Variant.From(int.Parse(key)) : Variant.From(key)] = ToVariant(value);
        return result;
    }
    private static Variant ToVariant(object value) => value switch
    {
        bool b => Variant.From(b), int n => Variant.From(n), long n => Variant.From(n), double n => Variant.From(n),
        Dictionary<string, object> map => Variant.From(ToDictionary(map)),
        object[] array => Variant.From(new GArray(array.Select(ToVariant))),
        _ => Variant.From((string)value),
    };
    private static GDict ToDictionary(Dictionary<string, object> map)
    {
        var result = new GDict();
        foreach (var (key, value) in map) result[key] = ToVariant(value);
        return result;
    }
    private static object FromVariant(Variant value) => value.VariantType switch
    {
        Variant.Type.Bool => value.AsBool(), Variant.Type.Int => value.AsInt64(), Variant.Type.Float => value.AsDouble(),
        Variant.Type.Dictionary => value.AsGodotDictionary().ToDictionary(p => p.Key.AsString(), p => FromVariant(p.Value)),
        Variant.Type.Array => value.AsGodotArray().Select(FromVariant).ToArray(),
        _ => value.AsString(),
    };
    public void ReplaceProgress(string group, GDict values)
    {
        var state = _content.Session.GetProgress(group);
        state.Clear();
        foreach (var (key, value) in values) state[key.AsString()] = FromVariant(value);
    }
    public void SetDetail(string group, string key, string field, Variant value)
        => ((Dictionary<string, object>)_content.Session.GetProgress(group)[key])[field] = FromVariant(value);
    public void SetProgress(string group, Variant key, Variant value)
    {
        _content.Session.Set(group, key.AsString(), FromVariant(value));
        if (group == "examined") _content.Session.Observe("detective", key.AsString());
    }
    public void RemoveProgress(string group, string key) => _content.Session.Remove(group, key);
    public bool V6Open() => _content.V6Open;
    public bool SiteVisible(string stage, string id, string playerRoom, string targetRoom)
    {
        _content.Session.Locate("detective", playerRoom);
        _content.Session.Place(id, targetRoom);
        return _content.SiteVisible(stage, id);
    }
    public bool CanExperiment(string id) => _content.CanExperiment(id);
    public bool CanHelp(string id) => _content.CanHelp(id);
    public bool CanRecall(string who, bool moving) => _content.CanRecall(who, moving);
    public GArray Statements(GArray variables, int replayCount, bool bolted)
        => new(_content.Statements(variables.Select(v => v.AsInt32()).ToArray(), replayCount, bolted).Select(s => Variant.From(s)));
    public string Judge(int step, string said, string shown) => _content.Judge(step, said, shown);
    public int BeginMeeting() => RegisterPlan(_content.StartPlan(() => _content.BeginMeeting(_morning)));
    public int BeginExperiment(string id, string who) => RegisterPlan(_content.StartPlan(() => _content.BeginExperiment(_morning, id, who)));
    public int BeginHelp(string id) => RegisterPlan(_content.StartPlan(() => _content.BeginHelp(_morning, id)));
    public int BeginRecall(string id) => RegisterPlan(_content.StartPlan(() => _content.BeginRecall(_morning, id)));
    public int BeginResolveRecall(string id, string who) => RegisterPlan(_content.StartPlan(() => _content.BeginResolveRecall(_morning, id, who)));
    public void ApplyRecall(string id, string who) => _content.ApplyRecall(_morning, id, who);
    public int BeginAfterReplay() => RegisterPlan(_content.StartPlan(() => _content.BeginAfterReplay(_morning)));
    public int BeginTalk(string who) => RegisterPlan(_content.StartPlan(() => _content.BeginTalk(_morning, who)));
    public int BeginAnswer() => RegisterPlan(_content.StartPlan(() => _content.BeginAnswer(_morning)));
    public int BeginFinale() => RegisterPlan(_content.StartPlan(() => _content.BeginFinale(_morning)));
    public int BeginRest() => RegisterPlan(_content.StartPlan(() => _content.BeginRest(_morning)));
    public int BeginConfirmRest() => RegisterPlan(_content.StartPlan(() => _content.BeginConfirmRest(_morning)));
    public GDict Consider(string who)
    {
        var intent = _content.Consider(_morning, who);
        return intent is null ? new() : new() { ["reach"] = intent.Reach, ["thought"] = intent.Thought };
    }
    public void BeginReach(string who, string thought) => _content.BeginReach(who, thought);
    public int BeginPresentReach(string who) => RegisterPlan(_content.StartPlan(() => _content.BeginPresentReach(_morning, who)));
    public void PrepareAlone(string who) => _content.PrepareAlone(_morning, who);
    public void CompleteAlone(string who) => _content.CompleteAlone(_morning, who);
    public int BeginRoomChanged(string room, long now) => RegisterPlan(_content.StartPlan(() => _content.BeginRoomChanged(_morning, room, now)));
    public string RoomEntry(string room) => _content.RoomEntry(room);
    public string RoomTrigger(string room)
    {
        Locate("detective", room);
        return _content.RoomTrigger(room);
    }
    public GDict LifeTrace(string id) => Json.ParseString(_content.LifeTrace(id).GetRawText()).AsGodotDictionary();
    public int BeginWake() => RegisterPlan(_content.StartPlan(() => _content.BeginWake(_morning)));
    public GArray ExamineContent(string id) => new(_content.Examine(_morning, id).Select(s => Variant.From(s)));
    public bool UnlockReplay() => _content.UnlockReplay();
    public bool NudgeCard(string id) => _content.NudgeCard(id);
    public string CardFacts(string id) => _content.CardFacts(id);
    public GArray CardNotes(string id) => new(_content.CardNotes(_morning, id).Select(s => Variant.From(s)));
    public string WrongName() => (string)_content.Session.GetProgress("state").GetValueOrDefault("wrong_name", "");
    public GDict SoundPositions(string playerRoom)
    {
        var positions = new GDict();
        foreach (var (who, room) in _content.SoundPositions(playerRoom)) positions[who] = room;
        return positions;
    }
    private readonly Dictionary<int, AwakeningContentSession.InteractionPlan> _plans = new();
    private int _nextPlan;
    private int RegisterPlan(AwakeningContentSession.InteractionPlan plan)
    {
        var key = ++_nextPlan;
        _plans[key] = plan;
        return key;
    }
    public string NextMeeting(int key)
    {
        var plan = _plans[key];
        var output = _content.NextMeeting(plan);
        if (plan.Finished) _plans.Remove(key);
        return output;
    }
    public void Presented(int key) => _content.Presented(_plans[key]);

    private StoryRuntime<MorningWorld, Person>? _script;
    private readonly Queue<GDict> _scriptOutput = new();
    public void LoadScript(string path)
    {
        _scriptOutput.Clear();
        _script = ChapterScript.Load<MorningWorld, Person>(path).CreateRuntime(_morning, new()
        {
            Text = (_, text) => _scriptOutput.Enqueue(new() { ["kind"] = "text", ["content"] = text }),
            Present = (_, output) => _scriptOutput.Enqueue(new() { ["kind"] = output.Kind.ToString().ToLowerInvariant(), ["content"] = output.Content, ["actor"] = Key(output.Actor) }),
            Busy = (world, actor) => world.StoryBusy(actor),
            Execute = (world, actor, effect) => world.SubmitStory(actor, effect),
            Timeline = world => world.StoryTimeline,
        });
    }
    public GDict NextScript()
    {
        _script!.Pump();
        if (_scriptOutput.Count > 0) return _scriptOutput.Dequeue();
        if (_script.PendingChoice is { } choice)
            return new() { ["kind"] = "choice", ["content"] = choice.Prompt,
                ["options"] = new GArray(choice.Options.Select(o => Variant.From(new GDict { ["id"] = o.Id, ["text"] = o.Text }))) };
        return new() { ["kind"] = _script.IsRunning ? "wait" : "done" };
    }
    public void ChooseScript(string option) => _script!.Choose(option);
    public bool PollScript()
    {
        if (_script is null) return false;
        _script.Pump();
        return _scriptOutput.Count > 0 || _script.PendingChoice is not null;
    }

    public static readonly string[] PersonKeys = ["detective", "huo", "shen", "su"];
    public static readonly string[] RoomKeys = ["R05", "R06", "R07", "R08", "corridor", "hall", "pantry", "foyer", "lounge"];

    private static string Key(Person p) => PersonKeys[(int)p];
    private static string Key(Room r) => RoomKeys[(int)r];

    public void Reset()
    {
        _morning = Chapter.CreateWorld();
        _content = Chapter.CreateContent();
    }

    /// <summary>变量以选项序号传入（与 NightVariables 的枚举顺序一致）；examined 是玩家已检视的证物编号。</summary>
    public GDict RunReplay(GArray variables, GArray examined)
    {
        var v = new NightVariables((SuPlace)(int)variables[0], (HuoPlace)(int)variables[1], (VoiceOrigin)(int)variables[2],
            (VoiceWords)(int)variables[3], (DetectiveHeeds)(int)variables[4], (Acquaintance)(int)variables[5]);
        var seen = examined.Select(x => x.AsString()).ToHashSet();
        var result = NightReplay.Run(v);
        var frames = new GArray();
        foreach (var f in result.Frames)
        {
            var rooms = new GDict();
            var spots = new GDict();
            var thoughts = new GDict();
            foreach (var p in Enum.GetValues<Person>())
            {
                rooms[Key(p)] = Key(f.Rooms[p]);
                spots[Key(p)] = f.Spots[p];
                thoughts[Key(p)] = f.Thoughts[p];
            }
            frames.Add(new GDict
            {
                ["t"] = f.Time, ["actor"] = f.Actor, ["label"] = f.Label, ["rooms"] = rooms, ["spots"] = spots,
                ["thoughts"] = thoughts, ["door_open"] = f.DoorOpen, ["door_bolted"] = f.DoorBolted,
                ["ash"] = f.AshFan, ["smoke"] = f.SmokeIn,
            });
        }
        var findings = new GArray();
        foreach (var finding in Comparison.Compare(result.Traces, MorningTraces.Evidence, seen.Contains))
            findings.Add(new GDict
            {
                ["id"] = finding.Evidence, ["place"] = finding.Place,
                ["status"] = finding.Status switch { FindingStatus.Match => "match", FindingStatus.Conflict => "conflict", _ => "unvisited" },
                ["text"] = finding.Text,
            });
        var t = result.Traces;
        return new GDict
        {
            ["frames"] = frames, ["findings"] = findings, ["full_match"] = result.FullMatch,
            ["conflicts"] = new GArray(result.Conflicts.Select(c => Variant.From(c))),
            ["bolted"] = t.InnerBoltDown, ["detective_bolted"] = result.Log.Any(l => l.Contains("侦探照霍铎转述")),
        };
    }

    public GArray Sentences() => new(TestSentences.All.Select(s => Variant.From(s.Full)));

    /// <summary>声路试验。positions：人物键 → 房间键。</summary>
    public GArray SoundTest(int sentence, GDict positions)
    {
        var map = new Dictionary<Person, Room>();
        foreach (var (key, value) in positions)
            map[(Person)Array.IndexOf(PersonKeys, key.AsString())] = (Room)Array.IndexOf(RoomKeys, value.AsString());
        var heard = _morning.SoundTest(TestSentences.All[Math.Clamp(sentence, 0, TestSentences.All.Length - 1)], map);
        return new GArray(heard.Select(h => Variant.From(new GDict
        {
            ["who"] = Key(h.Who), ["room"] = Key(h.Where), ["text"] = h.Text, ["outside"] = h.SeemsFromOutside,
            ["hearing"] = h.Hearing.ToString(), ["direction"] = h.Direction,
        })));
    }

    /// <summary>触物回忆：返回片段与所证变量（1..6）；同一人只记一次钩。</summary>
    public GDict Recall(string evidence)
    {
        var memory = _morning.Recall(evidence);
        if (memory is null) return new GDict();
        return new GDict
        {
            ["who"] = Key(memory.Who), ["fragment"] = memory.Fragment,
            ["confirms"] = new GArray(memory.Confirms.Select(c => Variant.From(c))),
            ["hooks"] = _morning.Hooks(memory.Who),
        };
    }

    private static Person PersonOf(string key) => (Person)Array.IndexOf(PersonKeys, key);

    /// <summary>恶魔对某人说一句朝向其欲望的真话（Corruption 来源）。</summary>
    public int Amplify(string who, string words) => _morning.Amplify(PersonOf(who), words);

    public void ShowRule1(string who) => _morning.ShowRule1(PersonOf(who));
    public bool KnowsRule1(string who) => _morning.KnowsRule1(PersonOf(who));
    public int DesireStrength(string who) => _morning.DesireStrength(PersonOf(who));

    /// <summary>同伴碰不碰旧物：choice 为 touch / hesitate / refuse，thought 为自然语言依据。</summary>
    public GDict DecideRecall(string evidence)
    {
        var decision = _morning.DecideRecall(evidence);
        if (decision is null) return new GDict();
        return new GDict
        {
            ["who"] = Key(decision.Who), ["thought"] = decision.Thought,
            ["choice"] = decision.Choice switch { RecallChoice.Touch => "touch", RecallChoice.Refuse => "refuse", _ => "hesitate" },
        };
    }

    /// <summary>恶魔对某同伴低语（Corruption）：欲望加强，并记下“铜口对我说的那句话”。</summary>
    public void Whisper(string who, string words) => _morning.Whisper(PersonOf(who), words);
    public bool Whispered(string who) => _morning.Whispered(PersonOf(who));

    /// <summary>同伴此刻想不想自己去拿旧物：reach（bool）与 thought。</summary>
    public GDict Intend(string who)
    {
        var intention = _morning.Intend(PersonOf(who));
        return intention is null ? new GDict() : new GDict { ["reach"] = intention.Reach, ["thought"] = intention.Thought };
    }

    /// <summary>宿主把人走到某个房间（房间键）后同步到现实。</summary>
    public void Locate(string who, string room)
    {
        _content.Session.Locate(who, room);
        var index = Array.IndexOf(RoomKeys, room);
        if (index >= 0) _morning.Locate(PersonOf(who), (Room)index);
    }
    public bool Reach(string who) => _content.Reach(_morning, who);
    public bool PutBack(string who) => _morning.PutBack(PersonOf(who));
    public void Settle(string who) => _morning.Settle(PersonOf(who));
    public bool Holding(string who) => _morning.Holding(PersonOf(who));
    public bool IsDisturbed(string evidence) => _morning.IsDisturbed(evidence);

    /// <summary>侦探不在场时，同伴自己的取舍（犹豫则放下）。</summary>
    public GDict DecideAlone(string evidence)
    {
        var decision = _morning.DecideAlone(evidence);
        if (decision is null) return new GDict();
        return new GDict
        {
            ["who"] = Key(decision.Who), ["thought"] = decision.Thought,
            ["choice"] = decision.Choice switch { RecallChoice.Touch => "touch", RecallChoice.Refuse => "refuse", _ => "hesitate" },
        };
    }

    /// <summary>犹豫的同伴反问“你说呢？”：advice 为 touch / decline / own；返回 SocialSystem 的判定。</summary>
    public string Advise(string who, string advice) => _morning.Advise(PersonOf(who), advice).ToString();

    public GArray PostedRules() => new(MorningWorld.PostedRules.Select(c => Variant.From(c)));

    public void Examine(string who, string evidence)
        => _morning.Examine((Person)Array.IndexOf(PersonKeys, who), evidence);

    public int Hooks(string who) => _morning.Hooks((Person)Array.IndexOf(PersonKeys, who));
    public int TotalHooks() => _morning.TotalHooks;
    public bool HasRecalled(string who) => _morning.HasRecalled((Person)Array.IndexOf(PersonKeys, who));

    /// <summary>章节结束状态：钩数、信任（V6 结论）、已公开的物证。供后续章节读取。</summary>
    public GDict ChapterEnd(GArray examined, bool familiar, bool askedHand, bool rested)
    {
        var hooks = new GDict();
        foreach (var p in Enum.GetValues<Person>()) hooks[Key(p)] = _morning.Hooks(p);
        return new GDict
        {
            ["hooks"] = hooks, ["trust_familiar"] = familiar, ["evidence_public"] = examined,
            ["asked_hand"] = askedHand, ["rested"] = rested,
        };
    }
}
