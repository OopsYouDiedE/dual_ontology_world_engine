using DualOntologyWorldEngine.Awakening;
using Godot;
using GArray = Godot.Collections.Array;
using GDict = Godot.Collections.Dictionary;

/// <summary>
/// GDScript 与重演引擎之间的桥。清晨的现实（MorningWorld）在整局中只有一个；
/// 每次 RunReplay 都在 NightReplay 中新建独立的昨夜世界，不触碰现实。
/// </summary>
public partial class AwakeningEngine : Node
{
    private MorningWorld _morning = new();

    public static readonly string[] PersonKeys = ["detective", "huo", "shen", "su"];
    public static readonly string[] RoomKeys = ["R05", "R06", "R07", "R08", "corridor", "hall", "pantry", "foyer", "main"];

    private static string Key(Person p) => PersonKeys[(int)p];
    private static string Key(Room r) => RoomKeys[(int)r];

    public void Reset() => _morning = new MorningWorld();

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

    public void ShowClause8(string who) => _morning.ShowClause8(PersonOf(who));
    public bool KnowsClause8(string who) => _morning.KnowsClause8(PersonOf(who));
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
    public void Locate(string who, string room) => _morning.Locate(PersonOf(who), (Room)Array.IndexOf(RoomKeys, room));
    public bool Reach(string who) => _morning.Reach(PersonOf(who));
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

    public GArray PostedClauses() => new(MorningWorld.PostedClauses.Select(c => Variant.From(c)));

    public void Examine(string who, string evidence)
        => _morning.Examine((Person)Array.IndexOf(PersonKeys, who), evidence);

    public int Hooks(string who) => _morning.Hooks((Person)Array.IndexOf(PersonKeys, who));
    public int TotalHooks() => _morning.TotalHooks;
    public bool HasRecalled(string who) => _morning.HasRecalled((Person)Array.IndexOf(PersonKeys, who));

    /// <summary>章节结束状态：钩数、信任（V6 结论）、已公开的物证。供后续章节读取。</summary>
    public GDict ChapterEnd(GArray examined, bool familiar, bool askedReverse, bool rested)
    {
        var hooks = new GDict();
        foreach (var p in Enum.GetValues<Person>()) hooks[Key(p)] = _morning.Hooks(p);
        return new GDict
        {
            ["hooks"] = hooks, ["trust_familiar"] = familiar, ["evidence_public"] = examined,
            ["asked_reverse"] = askedReverse, ["rested"] = rested,
        };
    }
}
