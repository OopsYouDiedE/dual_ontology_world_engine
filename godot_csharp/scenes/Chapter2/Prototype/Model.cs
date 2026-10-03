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
    AskLatchHelp, Seize, AnswerName, Escort, Inform, FeedWeak, GiveKey, Tidy, Wander, Share, Tear, Watch, AskOpen
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
    public static string Of(P p) => p switch
    {
        P.Det => "侦探", P.Huo => "霍铎", P.Shen => "沈鼎", P.Su => "苏泠", P.Lucian => "卢西安", P.Gu => "顾平章",
        P.Quer => "雀儿", P.Ward => "沃德", P.Selma => "赛尔玛", P.Ruan => "阮清越", P.Elena => "伊莲娜", P.Moran => "莫兰",
        P.Veronica => "维罗妮卡", P.Bertrand => "贝尔特朗", P.Margaret => "玛格丽特", P.Mora => "茉拉", _ => p.ToString()
    };
    public static string Of(Place p) => p switch
    {
        Place.Hall => "中央厅", Place.Lecture => "讲堂", Place.Logistics => "后勤院", Place.Clinic => "诊疗所",
        Place.Greenhouse => "温室", Place.ArchiveFront => "名录库前厅", Place.Oath => "誓约庭", Place.DormLife => "生活翼",
        Place.DormW => "文契寝区", Place.DormS => "星仪寝区", Place.DormL => "后勤寝区", _ => p.ToString()
    };
    public static string Of(Slot s) => s switch
    {
        Slot.Morning => "清晨", Slot.Forenoon => "上午", Slot.Noon => "午间", Slot.Afternoon => "下午", Slot.Evening => "傍晚", Slot.Night => "夜里", _ => ""
    };
    public static string Of(Group g) => g switch { Group.Life => "生活翼", Group.Wenqi => "文契组", Group.Xingyi => "星仪组", Group.Houqin => "后勤组", _ => "" };
    /// <summary>
    /// 动作帧类别（§12 第 0 条：可映射为画面）：每个动作落到有限的人物动作帧——行走、操作、交谈、递物、坐、看守、蹲查、拉拽——供后续 32×48 动作图集。
    /// </summary>
    public static string Frame(Job j) => j switch
    {
        Job.Sleep or Job.Rest or Job.Class => "坐",
        Job.Collect or Job.FeedWeak or Job.GiveKey or Job.Share => "递物",
        Job.Distribute or Job.Cook or Job.NightTakeSick or Job.NightTakeSickRec or Job.NightTakeSelf or Job.NightTakeSelfRec or Job.RepairLatch
            or Job.Greenhouse or Job.Work or Job.Hoard or Job.PostRules or Job.Inform => "操作",
        Job.FetchWater or Job.Seize or Job.Tear => "拉拽",
        Job.Care or Job.InspectStructure or Job.CountLedger or Job.Stele or Job.Scavenge or Job.Tidy => "蹲查",
        Job.GuardArchive or Job.VisitArchive or Job.Watch => "看守",
        Job.Patrol or Job.Escort or Job.Wander => "行走",
        Job.Socialize or Job.Accuse or Job.Defend or Job.AskLatchHelp or Job.AnswerName or Job.AskOpen => "交谈",
        _ => "行走"
    };

    public static string Of(Job j) => j switch
    {
        Job.Sleep => "回寝区睡下", Job.Rest => "在寝区歇着", Job.Class => "去讲堂上课", Job.Collect => "去配给台领口粮",
        Job.Distribute => "开粮仓、开配给台", Job.Cook => "在灶上煮粥", Job.FetchWater => "挑水", Job.Care => "在诊疗所照看病人",
        Job.NightTakeSick => "夜里为病人取一份口粮（不记账）", Job.NightTakeSickRec => "夜里为病人取一份口粮并记账",
        Job.NightTakeSelf => "夜里自取一份口粮（不记账）", Job.NightTakeSelfRec => "夜里自取一份口粮并记账",
        Job.RepairLatch => "修粮仓门闩", Job.InspectStructure => "查看后勤院的结构", Job.Patrol => "提灯夜巡",
        Job.GuardArchive => "守在名录库前厅", Job.VisitArchive => "去名录库前厅", Job.Greenhouse => "修温室水压",
        Job.Socialize => "在中央厅与人交谈", Job.Work => "做自己的手艺", Job.CountLedger => "清点粮仓、记台账",
        Job.Stele => "去誓约庭看石碑", Job.Scavenge => "到后勤院找吃的", Job.Hoard => "多取两份藏起来",
        Job.Accuse => "在中央厅当众指控", Job.PostRules => "在中央厅张贴规则", Job.Defend => "在中央厅替人说话",
        Job.AskLatchHelp => "请在场的工匠修门闩", Job.Seize => "夜里去开名录库", Job.AnswerName => "应了唤名", Job.Escort => "同行", Job.Inform => "去台账补记昨夜的取用", Job.FeedWeak => "替饿着的人领一份口粮送去", Job.GiveKey => "把粮仓钥匙交给沈鼎", Job.Tidy => "倒空自己寝区的废纸篓", Job.Wander => "去某处看一眼",
        Job.Share => "把自己的口粮分给饿着的人", Job.Tear => "撕下中央厅板上的一张规矩", Job.Watch => "夜里守在粮仓边上", Job.AskOpen => "约人夜里去开名录库", _ => j.ToString()
    };
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
    private static ImmutableDictionary<RF, int> V(params (RF, int)[] items) => items.ToImmutableDictionary(i => i.Item1, i => i.Item2);
    private static ImmutableDictionary<P, int> J(params (P, int)[] items) => items.ToImmutableDictionary(i => i.Item1, i => i.Item2);
    private static readonly Slot[] Day4 = [Slot.Forenoon, Slot.Noon, Slot.Afternoon, Slot.Evening];
    private static readonly Slot[] Free = [Slot.Afternoon];
    private static readonly Slot[] Fore = [Slot.Forenoon];
    private static readonly Slot[] Meal = [Slot.Noon, Slot.Evening];
    private static readonly Slot[] Night = [Slot.Night];
    private static readonly Slot[] Eve = [Slot.Evening];
    private static readonly Slot[] Morn = [Slot.Morning];

    // 第一部分 §5 表的占位数值化。数值只决定倾向，具体行动全部由 DecisionSystem 在各人内部世界里推出。
    public static readonly ImmutableArray<Profile> All =
    [
        new() { Id = P.Det, Group = Group.Life, Dorm = Place.DormLife, Speed = 1.0, Desire = "弄清自己是什么",
            DemonLine = "你的封存匣在名录库里，标签上写着你的名字。" },
        new() { Id = P.Huo, Group = Group.Life, Dorm = Place.DormLife, Speed = 1.0, Cooperation = 1, Build = Build.Large,
            Desire = "超越卡诺循环的动力图纸", DemonLine = "图纸就在名录库，记得它的人，是你。", DesireFlags = RF.OpenArchive,
            Strength = 2, Lock = 2, Structure = 1, Craftsman = true, Voice = "工匠口吻，短",
            Values = V((RF.Records, 1), (RF.OpenArchive, 1), (RF.CentralCook, 1), (RF.EvidenceFirst, 1)),
            Prejudice = J((P.Lucian, -1), (P.Gu, -1)),
            Habits = [new(Job.Class, Fore, 2, true, "第十二课讲机械"), new(Job.Greenhouse, Free, 2), new(Job.Work, Day4, 1), new(Job.Collect, Meal, 3)] },
        new() { Id = P.Shen, Group = Group.Life, Dorm = Place.DormLife, Speed = 0.9, Cooperation = 1, Build = Build.Large,
            Desire = "安稳，护住身边的人", DemonLine = "粮仓的钥匙若在你手里，就没人会挨饿。", DesireFlags = RF.CentralCook,
            Cook = 3, Strength = 1,
            Values = V((RF.CentralCook, 3), (RF.SickPriority, 1), (RF.PerHead, 1), (RF.TargetsQuer, -1), (RF.SealArchive, 1)),
            Prejudice = J((P.Lucian, -1)),
            Habits = [new(Job.FetchWater, Morn, 4), new(Job.Cook, Meal, 4, true), new(Job.Collect, Meal, 3), new(Job.Work, Day4, 1)] },
        new() { Id = P.Su, Group = Group.Life, Dorm = Place.DormLife, Speed = 1.3, Build = Build.Small,
            Desire = "从未听过的声音", DemonLine = "共鸣厅里有一种诸位从未听过的声音。它在名录库的匣子里也响过。", DesireFlags = RF.OpenArchive,
            Signal = 3,
            Values = V((RF.OpenArchive, 1), (RF.Relay, 1), (RF.WardAuthority, -1)),
            Prejudice = J((P.Ward, -1), (P.Lucian, -1)),
            Habits = [new(Job.Class, Fore, 2, true), new(Job.Work, Day4, 1), new(Job.Collect, Meal, 3)] },
        new() { Id = P.Lucian, Group = Group.Wenqi, Dorm = Place.DormW, Speed = 1.1, Cooperation = 1,
            Desire = "言灵的完美韵律", DemonLine = "第十三课的词，你会比任何人都念得准。", DesireFlags = RF.OpenArchive,
            Values = V((RF.EvidenceFirst, 1), (RF.TargetsQuer, -2), (RF.Restricts, -1), (RF.SickPriority, 1), (RF.OpenArchive, 1), (RF.Records, 1)),
            Prejudice = J((P.Veronica, -1)),
            Habits = [new(Job.Class, Fore, 3, true), new(Job.Socialize, Free, 2), new(Job.Defend, Morn, 3), new(Job.Collect, Meal, 3)] },
        new() { Id = P.Gu, Group = Group.Wenqi, Dorm = Place.DormW, Speed = 0.7,
            Desire = "读通古代铭文", DemonLine = "誓约庭的石碑上，有你一辈子都想读的那种字。", DesireFlags = RF.Tradition,
            Values = V((RF.Records, 1), (RF.Tradition, 2), (RF.SealArchive, 3), (RF.OpenArchive, -2), (RF.EvidenceFirst, 1), (RF.PactArchive, 1)),
            Prejudice = J((P.Quer, -1), (P.Veronica, -1)), DemonPrejudice = -1,
            Habits = [new(Job.Class, Fore, 2), new(Job.Stele, Free, 2, true), new(Job.Collect, Meal, 3)] },
        new() { Id = P.Quer, Group = Group.Wenqi, Dorm = Place.DormW, Speed = 1.4, Cooperation = -1, Build = Build.Small,
            Desire = "一个不会赶她走的地方", DemonLine = "名录库里有你的匣子。那里面的人，从没赶过你。", DesireFlags = RF.OpenArchive,
            Lock = 3, Craftsman = true,
            Values = V((RF.TargetsQuer, -4), (RF.Restricts, -2), (RF.Records, -1), (RF.EvidenceFirst, 2), (RF.NightException, 1), (RF.TwoWitness, 1), (RF.OpenArchive, 1), (RF.CommitteeArchive, -2), (RF.WardAuthority, -2)),
            Prejudice = J((P.Ward, -3), (P.Gu, -1)),
            Habits = [new(Job.Work, Day4, 1), new(Job.Collect, Meal, 3)] },
        new() { Id = P.Ward, Group = Group.Wenqi, Dorm = Place.DormW, Speed = 1.0,
            Desire = "成为定规则的人", DemonLine = "这里缺一个定规则的人。诸位迟早要听某个人的。", DesireFlags = RF.WardAuthority | RF.CommitteeArchive,
            Values = V((RF.Records, 2), (RF.WardAuthority, 2), (RF.Restricts, 1), (RF.CommitteeArchive, 3), (RF.NoException, 1), (RF.TwoWitness, 1), (RF.OpenArchive, -1), (RF.EvidenceFirst, 1)),
            Prejudice = J((P.Quer, -2)),
            Habits = [new(Job.Class, Fore, 2), new(Job.PostRules, Eve, 3, true), new(Job.Accuse, Morn, 4), new(Job.Socialize, Free, 2), new(Job.GuardArchive, Night, 2, true), new(Job.Collect, Meal, 3)] },
        new() { Id = P.Selma, Group = Group.Xingyi, Dorm = Place.DormS, Speed = 1.1, Cooperation = 1,
            Desire = "为源质这一新物种定名", DemonLine = "温室里的源质在长。你会是第一个给它命名的人。", DesireFlags = RF.OpenArchive,
            GreenhouseSkill = 3,
            Values = V((RF.OpenArchive, 1), (RF.SickPriority, 1), (RF.WardAuthority, -1), (RF.EvidenceFirst, 1)),
            Prejudice = J((P.Ward, -1)),
            Habits = [new(Job.Greenhouse, Free, 3, true), new(Job.Class, Fore, 2), new(Job.Collect, Meal, 3)] },
        new() { Id = P.Ruan, Group = Group.Xingyi, Dorm = Place.DormS, Speed = 1.0,
            Desire = "赢下与恶魔的对局", DemonLine = "这局棋你落后一子。名录库里有你忘掉的那几步。", DesireFlags = RF.OpenArchive,
            Values = V((RF.Records, 2), (RF.PerHead, 2), (RF.TwoWitness, 1), (RF.EvidenceFirst, 1), (RF.CommitteeArchive, 1), (RF.OpenArchive, 1)),
            Habits = [new(Job.Class, Fore, 3, true), new(Job.CountLedger, Eve, 4), new(Job.CountLedger, Morn, 4), new(Job.Collect, Meal, 3)] },
        new() { Id = P.Elena, Group = Group.Xingyi, Dorm = Place.DormS, Speed = 0.7, Build = Build.Small,
            Desire = "治好脚踝，查明仪式真相", DemonLine = "那晚的仪式，名录库里记着全程。", DesireFlags = RF.OpenArchive,
            Values = V((RF.Tradition, 1), (RF.EvidenceFirst, 1), (RF.PactArchive, 1), (RF.Safety, 1)),
            Prejudice = J((P.Bertrand, -1)),
            Habits = [new(Job.Class, Fore, 1), new(Job.Rest, Day4, 2), new(Job.Collect, Meal, 3)] },
        new() { Id = P.Moran, Group = Group.Xingyi, Dorm = Place.DormS, Speed = 0.9,
            Desire = "看见更多的以太色层", DemonLine = "有些颜色，你只在封存之前见过。", DesireFlags = RF.OpenArchive,
            Values = V((RF.OpenArchive, 1)),
            Habits = [new(Job.Work, Day4, 2, true), new(Job.Collect, Meal, 3)] },
        new() { Id = P.Veronica, Group = Group.Houqin, Dorm = Place.DormL, Speed = 1.0,
            Desire = "独立研究，证明理化能解释一切", DemonLine = "你的试剂柜里，缺的那一瓶，名录库里有记录。", DesireFlags = RF.EvidenceFirst,
            Values = V((RF.EvidenceFirst, 2), (RF.Records, 1), (RF.SealArchive, -1), (RF.OpenArchive, 1)),
            DemonPrejudice = -2,
            Habits = [new(Job.Work, Day4, 2, true), new(Job.Class, Fore, 1), new(Job.Collect, Meal, 3)] },
        new() { Id = P.Bertrand, Group = Group.Houqin, Dorm = Place.DormL, Speed = 1.1, Build = Build.Large,
            Desire = "护住平民同伴", DemonLine = "夜里只有你醒着。他们睡得着，是因为你。", DesireFlags = RF.Safety,
            Strength = 2,
            Values = V((RF.SickPriority, 1), (RF.Records, 1), (RF.Restricts, 1), (RF.Safety, 1)),
            Prejudice = J((P.Veronica, -1), (P.Ward, -1)),
            Habits = [new(Job.Patrol, Night, 3, true), new(Job.FetchWater, Free, 2), new(Job.Collect, Meal, 3)] },
        new() { Id = P.Margaret, Group = Group.Houqin, Dorm = Place.DormL, Speed = 1.0, Cooperation = 1,
            Desire = "亲手建成、被人承认", DemonLine = "温室是半成品。修好它的人，会被所有人记住。", DesireFlags = RF.CentralCook,
            Strength = 1, Structure = 3, GreenhouseSkill = 2,
            Values = V((RF.EvidenceFirst, 1), (RF.CentralCook, 1), (RF.OpenArchive, 1), (RF.Records, 1)),
            Prejudice = J((P.Lucian, -1)),
            Habits = [new(Job.InspectStructure, Free, 3, true), new(Job.Greenhouse, Free, 2), new(Job.Collect, Meal, 3)] },
        new() { Id = P.Mora, Group = Group.Houqin, Dorm = Place.DormL, Speed = 1.0,
            Desire = "不再失去病人", DemonLine = "取回的记忆会伤人。可封着的，也会。", DesireFlags = RF.SickPriority,
            Values = V((RF.SickPriority, 3), (RF.NoException, -2), (RF.CentralCook, -1), (RF.SealArchive, 2), (RF.OpenArchive, -1), (RF.EvidenceFirst, 1), (RF.NightException, 1), (RF.PactArchive, 1)),
            Prejudice = J((P.Veronica, -2)), DemonPrejudice = -1,
            Habits = [new(Job.Collect, Meal, 2)] },
    ];

    public static Profile Of(P p) => All[(int)p];

    public static Cluster ClusterOf(P p) => p switch
    {
        P.Ward or P.Gu or P.Ruan => Cluster.Order,
        P.Shen or P.Quer or P.Bertrand or P.Selma or P.Huo => Cluster.Labor,
        P.Lucian or P.Elena or P.Moran or P.Su => Cluster.Beauty,
        P.Veronica or P.Margaret or P.Mora => Cluster.Evidence,
        _ => Cluster.None
    };
    public static string ClusterName(Cluster c) => c switch { Cluster.Order => "秩序派", Cluster.Labor => "劳作互助派", Cluster.Beauty => "感官与美派", Cluster.Evidence => "实证派", _ => "" };
    /// <summary>值守者：看护、夜巡、记账的人。</summary>
    public static bool OnDuty(P p) => p is P.Mora or P.Bertrand or P.Ruan;

    /// <summary>初始信任：同寝区 +2；第三部分 §2 的三段裂痕与 §5 的成见、依赖写成例外。侦探与生活翼三人由第一章相熟。</summary>
    public static int BaseTrust(P a, P b)
    {
        if (a == b) return 0;
        var t = Of(a).Group == Of(b).Group ? 2 : 0;
        int? o = (a, b) switch
        {
            (P.Det, P.Huo) or (P.Det, P.Shen) or (P.Det, P.Su) or (P.Huo, P.Det) or (P.Shen, P.Det) or (P.Su, P.Det) => 3,
            (P.Ward, P.Quer) => -2, (P.Gu, P.Quer) => -1, (P.Lucian, P.Quer) => 2, (P.Quer, P.Ward) => -3, (P.Quer, P.Gu) => -1,
            (P.Quer, P.Lucian) => 2, (P.Quer, P.Huo) => 2, (P.Quer, P.Shen) => 2, (P.Quer, P.Mora) => 2, (P.Quer, P.Margaret) => 2,
            (P.Lucian, P.Ward) => 1, (P.Shen, P.Quer) => 1, (P.Huo, P.Quer) => 1,
            (P.Elena, P.Ruan) => -2, (P.Ruan, P.Elena) => 1,
            (P.Mora, P.Veronica) => -1, (P.Bertrand, P.Veronica) => -1, (P.Veronica, P.Huo) => 2, (P.Huo, P.Veronica) => 1,
            (P.Margaret, P.Huo) => 2, (P.Margaret, P.Quer) => 1, (P.Huo, P.Margaret) => 1,
            (P.Mora, P.Moran) => 1, (P.Ward, P.Ruan) => 1, (P.Ruan, P.Ward) => 1,
            _ => null
        };
        // 价值相近的人，跨了寝区也多一分好感（初值，可被打破）。
        var affinity = ClusterOf(a) != Cluster.None && ClusterOf(a) == ClusterOf(b) && Of(a).Group != Of(b).Group ? 1 : 0;
        return (o ?? t) + affinity;
    }
}
