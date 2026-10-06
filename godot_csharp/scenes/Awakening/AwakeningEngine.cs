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
    public AwakeningEngine()
    {
        ChapterWorldContinuity.Clear();
        _morning.InitializePlayerPerception();
    }
    private static readonly Chapter1Script Chapter = new();
    private MorningWorld _morning = Chapter.CreateWorld();
    private AwakeningContentSession _content = Chapter.CreateContent();
    private Chapter1Dialogue? _dialogue;
    private Chapter1Interaction? _interaction;
    private Chapter1Interaction FieldInteraction => _interaction ??= new(_content, _morning);
    public GDict StartDialogue(string title) => Packet((_dialogue ??= new(_content, _morning)).Start(title));
    public GDict AdvanceDialogue() => Packet((_dialogue ??= new(_content, _morning)).Next());
    public GDict Choose(string choiceId) => Packet((_dialogue ??= new(_content, _morning)).Choose(choiceId));
    public GDict Interact(string siteId) => Packet(FieldInteraction.Interact(siteId));
    public GDict CardPacket(string id, GArray geometricAvailablePeople) => Packet(FieldInteraction.CardPacket(id, geometricAvailablePeople.Select(p => p.AsString())));
    public GDict ChooseCard(string choiceId, GArray geometricAvailablePeople) => Packet(FieldInteraction.Choose(choiceId, geometricAvailablePeople.Select(p => p.AsString())));
    private static GDict Packet(InteractionPacket packet) => new()
    {
        ["lines"] = new GArray(packet.Lines.Select(line => Variant.From(new GDict
        { ["speaker"] = line.Speaker, ["mood"] = line.Mood, ["text"] = line.Text, ["voice_clip"] = line.VoiceClip, ["line_id"] = line.LineId }))),
        ["choices"] = new GArray(packet.Choices.Select(choice => Variant.From(new GDict
        { ["id"] = choice.Id, ["label"] = choice.Label, ["is_available"] = choice.IsAvailable, ["lock_hint"] = choice.LockHint, ["label_id"] = choice.LabelId }))),
        ["commands"] = new GArray(packet.WorldCommands.Select(command => Variant.From(new GDict
        { ["kind"] = command.Name, ["args"] = new GArray(command.Arguments.Select(value => value is null ? default(Variant) : ToVariant(value))) }))),
        ["presentation_mode"] = packet.PresentationMode.ToString().ToLowerInvariant(), ["finished"] = packet.IsComplete,
        ["card"] = packet.Card is not { } card ? new GDict() : new GDict
        { ["id"] = card.Id, ["title"] = card.Title, ["facts"] = card.Facts, ["source"] = card.Source, ["picture"] = card.Picture,
            ["notes"] = new GArray(card.Notes.Select(note => Variant.From(note))) },
    };

    public GDict Content()
    {
        return Json.ParseString(_content.Content.RootElement.GetRawText()).AsGodotDictionary();
    }

    public GDict CompiledDialogue() => Json.ParseString(System.Text.Json.JsonSerializer.Serialize(
        Chapter1CompiledDialogue.Create(), new System.Text.Json.JsonSerializerOptions
        { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower })).AsGodotDictionary();

    public int GameMinutes() => _content.GameMinutes;
    public string ClockText() => _content.ClockText;
    public string ExpectedExitText() => _content.ExpectedExitText;
    public GArray TakeTiming() => new(_content.TakeTiming().Select(entry => Variant.From(new GDict
    {
        ["kind"] = entry.Kind, ["id"] = entry.Id, ["start_minutes"] = entry.StartMinutes,
        ["end_minutes"] = entry.EndMinutes, ["minutes"] = entry.Minutes,
    })));
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

    public static string[] PersonKeys => Names.PersonKeys;
    public static string[] RoomKeys => Names.RoomKeys;

    private static string Key(Person p) => PersonKeys[(int)p];
    private static string Key(Room r) => RoomKeys[(int)r];

    public void Reset()
    {
        ChapterWorldContinuity.Clear();
        _morning = Chapter.CreateWorld();
        _morning.InitializePlayerPerception();
        _content = Chapter.CreateContent();
        _dialogue = null;
        _interaction = null;
        _plans.Clear();
    }

    /// <summary>变量以选项序号传入（与 NightVariables 的枚举顺序一致）；examined 是玩家已检视的证物编号。</summary>
    public GDict RunReplay(GArray variables, GArray examined)
    {
        var v = new NightVariables((SuPlace)(int)variables[0], (HuoPlace)(int)variables[1], (VoiceOrigin)(int)variables[2],
            (VoiceWords)(int)variables[3], (DetectiveHeeds)(int)variables[4], (Acquaintance)(int)variables[5]);
        var seen = examined.Select(x => x.AsString()).ToHashSet();
        _content.RecordReplayRun();
        var result = NightReplay.Run(v);
        _content.WitnessReplay(_morning, v, result.FullMatch);
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
        _content.RecordSoundTest();
        var heard = _morning.SoundTest(TestSentences.All[Math.Clamp(sentence, 0, TestSentences.All.Length - 1)], map);
        return new GArray(heard.Select(h => Variant.From(new GDict
        {
            ["who"] = Key(h.Who), ["room"] = Key(h.Where), ["text"] = h.Text, ["outside"] = h.SeemsFromOutside,
            ["hearing"] = h.Hearing.ToString(), ["direction"] = h.Direction,
        })));
    }

    private static Person PersonOf(string key) => (Person)Array.IndexOf(PersonKeys, key);

    /// <summary>恶魔对某人说一句朝向其欲望的真话（Corruption 来源）。</summary>
    public int Amplify(string who, string words) => _morning.Amplify(PersonOf(who), words);

    public int DesireStrength(string who) => _morning.DesireStrength(PersonOf(who));

    /// <summary>恶魔对某同伴低语（Corruption）：欲望加强，并记下“铜口对我说的那句话”。</summary>
    public void Whisper(string who, string words) => _morning.Whisper(PersonOf(who), words);
    public bool Whispered(string who) => _morning.Whispered(PersonOf(who));

    /// <summary>宿主把人走到某个房间（房间键）后同步到现实。</summary>
    public void Locate(string who, string room)
    {
        _content.Session.Locate(who, room);
        var index = Array.IndexOf(RoomKeys, room);
        if (index >= 0) _morning.Locate(PersonOf(who), (Room)index);
    }
    public bool Reach(string who) => _content.Reach(_morning, who);
    public void SetDetectiveControl(string mode) => _morning.SetDetectiveControl(mode);
    public string DetectiveControl() => _morning.DetectiveControl;
    public void SetDailyStage(string stage) => _morning.SetDailyStage(stage);
    public void SyncMorningClock() => _morning.SetMorningGameMinutes(_content.GameMinutes);
    public GArray DailyDecisions() => new(_morning.DailyDecisions.Select(p => Variant.From(new GDict
    { ["time"] = p.Time, ["game_minutes"] = p.GameMinutes, ["person"] = p.Person, ["control"] = p.Control, ["serial"] = p.Serial,
        ["room"] = p.Room, ["action"] = p.Action, ["score"] = p.Score,
        ["reasons"] = new GArray(p.Reasons.Select(reason => Variant.From(reason))) })));
    public GDict DailyTick(double delta, bool active, GArray busy)
    {
        SyncMorningClock();
        return new()
        {
            ["actors"] = new GArray(_morning.DailyTick(delta, active, busy.Select(p => p.AsString())).Select(p => Variant.From(new GDict
            { ["who"] = p.Who, ["serial"] = p.Serial, ["room"] = p.Room, ["target"] = p.Target,
                ["action"] = p.Action, ["note"] = p.Note, ["seconds"] = p.Seconds, ["speed"] = p.Speed })))
        };
    }
    public bool DailyArrive(string who, int serial, string room) => _morning.DailyArrive(who, serial, room);
    public void DailySuspend(string who) => _morning.DailySuspend(who);
    public void DailyBlock(string who, int serial) => _morning.DailyBlock(who, serial);
    public string DailyWitness(string room) => _morning.DailyWitness(room);
    public GArray MorningTrajectory() => new(_morning.DailyTrajectory.Select(p => Variant.From(new GDict
    { ["time"] = p.Time, ["game_minutes"] = p.GameMinutes, ["person"] = p.Person, ["room"] = p.Room, ["action"] = p.Action, ["text"] = p.Text })));
    public bool PutBack(string who) => _morning.PutBack(PersonOf(who));
    public bool IsDisturbed(string evidence) => _morning.IsDisturbed(evidence);

    public GArray PostedRules() => new(MorningWorld.PostedRules.Select(c => Variant.From(c)));

    public int Hooks(string who) => _morning.Hooks((Person)Array.IndexOf(PersonKeys, who));
    public int TotalHooks() => _morning.TotalHooks;

    /// <summary>章节结束状态：钩数、信任（V6 结论）、已公开的物证。供后续章节读取。</summary>
    public GDict ChapterEnd(GArray examined, bool familiar, bool askedHand, bool rested)
    {
        if (!rested) ChapterWorldContinuity.Publish(_morning.ExportContinuation(_content.Session));
        var hooks = new GDict();
        foreach (var p in Enum.GetValues<Person>()) hooks[Key(p)] = _morning.Hooks(p);
        return new GDict
        {
            ["hooks"] = hooks, ["trust_familiar"] = familiar, ["evidence_public"] = examined,
            ["asked_hand"] = askedHand, ["rested"] = rested,
        };
    }
}
