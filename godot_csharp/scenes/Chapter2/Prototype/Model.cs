using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>十六名学员。Det 是玩家（侦探）。</summary>
public enum P { Det, Huo, Shen, Su, Lucian, Gu, Quer, Ward, Selma, Ruan, Elena, Moran, Veronica, Bertrand, Margaret, Mora }
public enum Group { Life, Wenqi, Xingyi, Houqin }
public enum Place { Hall, Lecture, Logistics, Clinic, Greenhouse, ArchiveFront, Oath, DormLife, DormW, DormS, DormL }
public enum Slot { Morning, Forenoon, Noon, Afternoon, Evening, Night }
public enum Build { Small, Medium, Large }
/// <summary>跨寝区的价值聚落（§11 第 7 条，初值，可被打破）。</summary>
public enum Cluster { None, Order, Labor, Beauty, Evidence }

/// <summary>NPC 在一个时段（或夜里的一段）里能做的事。每项编译为 Core Effect，见 Jobs。</summary>
public enum Job
{
    Sleep, Rest, Class, Collect, Distribute, Cook, FetchWater, Care,
    NightTakeSick, NightTakeSickRec, NightTakeSelf, NightTakeSelfRec,
    RepairLatch, InspectStructure, Patrol, GuardArchive, VisitArchive, Greenhouse,
    Socialize, Work, CountLedger, Stele, Scavenge, Hoard, Accuse, PostRules, Defend,
    AskLatchHelp, Seize, AnswerName, Escort, Inform, FeedWeak, GiveKey, Tidy, Wander, Share, Tear, Watch, AskOpen, Relay, Teach, Exchange, Follow, Avoid, AskLeave,
    Treat, Retreat, Report, Sample, Needle, NightSketch, Sketch
}

/// <summary>规则的语义标记：规则接受公式据此与各人的欲望、习惯、利益相比。</summary>
[Flags]
public enum RF
{
    None = 0, Records = 1, SickPriority = 2, PerHead = 4, Restricts = 8, TargetsQuer = 16, CentralCook = 32,
    EvidenceFirst = 64, OpenArchive = 128, CommitteeArchive = 256, SealArchive = 512, PactArchive = 1024,
    NightException = 2048, TwoWitness = 4096, WardAuthority = 8192, Tradition = 16384, Relay = 32768,
    Safety = 65536, NoException = 131072, ForbidNightOpen = 262144, ForbidsDesire = 524288, Rations = 1048576, Targets = 2097152
}

public static class T
{
    public static readonly Tag Ration = new("Ration"), Locked = Tags.Locked, LatchLoose = new("LatchLoose"), ToolMarks = new("ToolMarks");
    public static readonly Tag Open = new("Open"), Cooked = new("Cooked"), Water = new("Water"), Handed = new("Handed");
    public static readonly Tag NightRecorded = new("NightRecorded"), Porridge = new("Porridge"), Crumbs = new("Crumbs"), Residue = new("Residue");
    public static readonly Tag Medicine = new("Medicine"), Oil = new("Oil"), Treated = new("Treated"), Work = new("Work");
    public static readonly Tag Pressure = new("Pressure"), Chalk = new("Chalk"), Rubbed = new("Rubbed"), Accusation = new("Accusation");
    public static readonly Tag Posted = new("Posted"), Counted = new("Counted"), Shoe = new("Shoe"), Emptied = new("Emptied"), Torn = new("Torn");
    /// <summary>名录库机关的一站被人占着 / 今夜有人在这一站使过劲；有人守门（身上可见）；中央基座的裂纹（凑近才看得见）。</summary>
    public static readonly Tag Occupied = new("Occupied"), Worked = new("Worked"), Guarding = new("Guarding"), Prying = new("Prying"), Crack = new("Crack"), OwesWater = new("OwesWater");
    /// <summary>众所周知的本事（他人心中的标签）：会开锁、力气大、懂传声。</summary>
    public static readonly Tag Picks = new("Picks"), Strong = new("Strong"), Signals = new("Signals");
    // 身体（客观，可见）
    public static readonly Tag Fed = new("Fed"), Hungry = new("Hungry"), Weak = new("Weak"), Sick = new("Sick"), NightAte = new("NightAte"), Stash = new("Stash");
    // 身体（客观，不可见：钥匙与技艺）
    public static readonly Tag GranaryKey = new("GranaryKey"), MedKey = new("MedKey"), ArchiveKey = new("ArchiveKey"), OilKey = new("OilKey");
    public static readonly Tag Lockpick = new("Lockpick"), Craftsman = new("Craftsman"), Structure = new("Structure");
    // 只存在于认知
    public static readonly Tag Branded = new("Branded"), Insight = new("Insight"), Seen = new("Seen"), Awake = new("Awake");
    public static readonly Tag OweNote = new("OweNote"), Unrecorded = new("Unrecorded"), Accused = new("Accused"), Suspect = new("Suspect");
    public static readonly Tag Shamed = new("Shamed"), SawAct = new("SawAct"), Answered = new("Answered"), Credible = new("Credible");
    public static readonly Tag Day = new("Day"), Domain = new("Domain"), Duty = new("Duty");
    /// <summary>自我身份：规则的“主语”格读的就是这些（此人心中自己的标签）。</summary>
    public static readonly ImmutableDictionary<Group, Tag> InGroup = Enum.GetValues<Group>().ToImmutableDictionary(g => g, g => new Tag("Grp" + g));
    public static readonly ImmutableDictionary<P, Tag> Is = Enum.GetValues<P>().ToImmutableDictionary(p => p, p => new Tag("Is" + p));
    public static readonly ImmutableDictionary<Slot, Tag> SlotTag = Enum.GetValues<Slot>().ToImmutableDictionary(s => s, s => new Tag("Slot" + s));
    private static readonly Dictionary<Job, Tag> _did = Enum.GetValues<Job>().ToDictionary(j => j, j => new Tag("Did" + j));
    public static Tag Did(Job job) => _did[job];
}

public static class Names
{
    /// <summary>人物、地点、时段、阵营的显示名，全部来自 chapter_02.story。</summary>
    public static string Of(P p) => Chapter2Design.PersonName(p);
    public static string Of(Place p) => Chapter2Design.PlaceName(p);
    public static string Of(Slot s) => Chapter2Design.SlotName(s);
    public static string Of(Group g) => Chapter2Design.GroupName(g);
    /// <summary>
    /// 动作帧类别（§12 第 0 条：可映射为画面）：每个动作落到有限的人物动作帧——行走、操作、交谈、递物、坐、看守、蹲查、拉拽——供后续 32×48 动作图集。
    /// </summary>
    public static string Frame(Job j) => Chapter2Design.JobFrame(j);

    public static string Of(Job j) => Chapter2Design.JobText(j);
    public static readonly ImmutableDictionary<string, P> PersonAlias = new Dictionary<string, P>
    {
        ["det"] = P.Det, ["huo"] = P.Huo, ["shen"] = P.Shen, ["su"] = P.Su, ["lucian"] = P.Lucian, ["gu"] = P.Gu, ["quer"] = P.Quer,
        ["ward"] = P.Ward, ["selma"] = P.Selma, ["ruan"] = P.Ruan, ["elena"] = P.Elena, ["moran"] = P.Moran, ["veronica"] = P.Veronica,
        ["bertrand"] = P.Bertrand, ["margaret"] = P.Margaret, ["mora"] = P.Mora,
    }.Concat(Enum.GetValues<P>().Select(p => new KeyValuePair<string, P>(Of(p), p))).ToImmutableDictionary();
    public static readonly ImmutableDictionary<string, Place> PlaceAlias = new Dictionary<string, Place>
    {
        ["hall"] = Place.Hall, ["lecture"] = Place.Lecture, ["logistics"] = Place.Logistics, ["clinic"] = Place.Clinic,
        ["greenhouse"] = Place.Greenhouse, ["archive"] = Place.ArchiveFront, ["oath"] = Place.Oath, ["life"] = Place.DormLife,
        ["dormw"] = Place.DormW, ["dorms"] = Place.DormS, ["dorml"] = Place.DormL,
    }.Concat(Enum.GetValues<Place>().Select(p => new KeyValuePair<string, Place>(Of(p), p))).ToImmutableDictionary();
}

/// <summary>一条习惯：在什么条件下做什么、权重多少、是否与核心欲望相连（恶魔放大欲望时随之加权）。</summary>
public sealed record HabitSpec(Job Job, Slot[] Slots, int Weight, bool Desire = false, string Note = "");

public sealed class Profile
{
    public required P Id { get; init; }
    public required Group Group { get; init; }
    public required Place Dorm { get; init; }
    public required double Speed { get; init; }
    public int Cooperation { get; init; }
    public Build Build { get; init; } = Build.Medium;
    public string Desire { get; init; } = "";
    public string DemonLine { get; init; } = "";
    public RF DesireFlags { get; init; }
    public int Strength { get; init; }
    public int Lock { get; init; }
    public int Signal { get; init; }
    public int Structure { get; init; }
    public int GreenhouseSkill { get; init; }
    public int Cook { get; init; }
    public bool Craftsman { get; init; }
    public ImmutableDictionary<RF, int> Values { get; init; } = ImmutableDictionary<RF, int>.Empty;
    public ImmutableDictionary<P, int> Prejudice { get; init; } = ImmutableDictionary<P, int>.Empty;
    public int DemonPrejudice { get; init; }
    public ImmutableArray<HabitSpec> Habits { get; init; } = [];
    public string Voice { get; init; } = "";
}

public static class Cast
{
    /// <summary>十六名学员的画像：全部读自 chapter_02.story 的人物块（本事、看重、偏见、日常、欲望……）。</summary>
    public static readonly ImmutableArray<Profile> All = [.. Chapter2AuthoredProfiles.ReadAll(Chapter2Design.Json).Values.OrderBy(p => (int)p.Id)];

    public static Profile Of(P p) => All[(int)p];

    public static Cluster ClusterOf(P p) => Chapter2Design.PersonData(p).Cluster;
    public static string ClusterName(Cluster c) => Chapter2Design.ClusterName(c);
    /// <summary>值守者：看护、夜巡、记账的人。</summary>
    public static bool OnDuty(P p) => Chapter2Design.PersonData(p).OnDuty;

    /// <summary>初始信任：同阵营与同群落的加成是两个常数，个人对个人的例外写在人物块的“信任”里。</summary>
    public static int BaseTrust(P a, P b)
    {
        if (a == b) return 0;
        var t = Of(a).Group == Of(b).Group ? (int)Chapter2Design.Const("同阵营初始信任") : 0;
        int? o = Chapter2Design.PersonData(a).Trust.TryGetValue(b, out var exception) ? exception : null;
        // 价值相近的人，跨了阵营也多一分好感（初值，可被打破）。
        var affinity = ClusterOf(a) != Cluster.None && ClusterOf(a) == ClusterOf(b) && Of(a).Group != Of(b).Group ? (int)Chapter2Design.Const("同群落跨阵营好感") : 0;
        return (o ?? t) + affinity;
    }
}
