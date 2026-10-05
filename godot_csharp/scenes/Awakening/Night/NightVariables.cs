using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Awakening;

/// <summary>四个人。Detective 是玩家。</summary>
public enum Person { Detective, Huo, Shen, Su }

/// <summary>乙区的房间与隔离门外的起居厅。地图几何属宿主；这里只有房间拓扑。</summary>
public enum Room { R05, R06, R07, R08, Corridor, Hall, Pantry, Foyer, Lounge }

// 重演面板的六个变量（设计 §4.1）。每个选项都是“处境与人心”，没有任何一项指定某人做了什么。
public enum SuPlace { OwnRoom, Hall, Pantry }
public enum HuoPlace { ByTube, OwnRoom, Foyer }
public enum VoiceOrigin { Tube, OutsideDoor }
public enum VoiceWords { BoltKeepShut, OpenSaveMe }
public enum DetectiveHeeds { Huo, Su, Himself }
public enum Acquaintance { Strangers, Familiar }

public sealed record NightVariables(SuPlace V1, HuoPlace V2, VoiceOrigin V3, VoiceWords V4, DetectiveHeeds V5, Acquaintance V6)
{
    public static IEnumerable<NightVariables> All()
    {
        foreach (var v1 in Enum.GetValues<SuPlace>())
        foreach (var v2 in Enum.GetValues<HuoPlace>())
        foreach (var v3 in Enum.GetValues<VoiceOrigin>())
        foreach (var v4 in Enum.GetValues<VoiceWords>())
        foreach (var v5 in Enum.GetValues<DetectiveHeeds>())
        foreach (var v6 in Enum.GetValues<Acquaintance>())
            yield return new(v1, v2, v3, v4, v5, v6);
    }

    public override string ToString() => $"V1={V1} V2={V2} V3={V3} V4={V4} V5={V5} V6={V6}";
}

/// <summary>一句话的完整内容、传声管接头漏出的重读句尾，以及听清时可解读出的意思。</summary>
public sealed record Utterance(string Full, string Tail, bool Bolt, bool ForbidOpen, bool HoldTube, bool Open, bool Help)
{
    public static readonly Utterance BoltKeepShut = new(
        "生活翼！我是格雷厄姆。主楼泄漏。闩上隔离门——无论听到什么，都不要开门。守在铜口旁，等我下一次通知。",
        "……开门……开门", Bolt: true, ForbidOpen: true, HoldTube: true, Open: false, Help: false);

    public static readonly Utterance OpenSaveMe = new(
        "开门！快开门——救我！", "……救我……救我", Bolt: false, ForbidOpen: false, HoldTube: false, Open: true, Help: true);

    public static Utterance Of(VoiceWords words) => words == VoiceWords.BoltKeepShut ? BoltKeepShut : OpenSaveMe;

    /// <summary>只听到重读句尾时，听者能从字面得到的东西：有人要开门，或有人求救。</summary>
    public bool TailWantsOpen => Tail.Contains("开门") || Tail.Contains("救我");
    public bool TailHelp => Tail.Contains("救我");
}

/// <summary>声音到达某个位置时剩下什么。</summary>
public enum Hearing { Nothing, AlarmOnly, Murmur, Tail, Full, FullMuffled }

/// <summary>
/// 声路（设计 §4.2 感知表）。这是游戏层的空间事实：铜口的传声管一路沿活动厅北墙进门厅，
/// 贴着隔离门门框通往主楼；一支走活动厅与 R-08 之间的墙内。两处松动接头只漏出低沉的重读音节。
/// </summary>
public static class Acoustics
{
    public static Hearing Hear(VoiceOrigin origin, Room listener) => origin switch
    {
        VoiceOrigin.Tube => listener switch
        {
            Room.Hall => Hearing.Full,
            Room.Foyer or Room.R08 => Hearing.Tail,
            Room.Pantry => Hearing.Murmur,
            _ => Hearing.AlarmOnly
        },
        _ => listener switch
        {
            Room.Foyer or Room.R08 => Hearing.FullMuffled,
            Room.Lounge => Hearing.Full,
            _ => Hearing.Nothing
        }
    };

    /// <summary>只听见句尾或隔门听见时，声音听来像是从门外来的。</summary>
    public static bool SeemsFromOutside(Hearing hearing) => hearing is Hearing.Tail or Hearing.FullMuffled;

    public static string Render(Utterance words, Hearing hearing) => hearing switch
    {
        Hearing.Full => words.Full,
        Hearing.FullMuffled => "（隔着门，发闷）" + words.Full,
        Hearing.Tail => words.Tail,
        Hearing.Murmur => "（有人在说话，听不清）",
        Hearing.AlarmOnly => "（只有警铃）",
        _ => "（什么也听不到）"
    };

    /// <summary>霍铎在活动厅朝走廊喊话：走廊与四间宿舍都听得见，备餐间与门厅隔着门。</summary>
    public static bool HearsShout(Room listener) => listener is Room.Hall or Room.Corridor or Room.R05 or Room.R06 or Room.R07 or Room.R08;
}

/// <summary>声路试验可以让铜口说的句子。整句与句尾都来自同一张表。</summary>
public static class TestSentences
{
    public static readonly ImmutableArray<Utterance> All =
    [
        new("闩上门，不要开门。", "……开门", true, true, false, false, false),
        new("开门，救我。", "……救我", false, false, false, true, true),
        new("守在铜口旁，等我下一次通知。", "……通知", false, false, true, false, false),
    ];
}

public static class Names
{
    public static string Of(Person person) => person switch
    {
        Person.Detective => "侦探", Person.Huo => "霍铎", Person.Shen => "沈鼎", _ => "苏泠"
    };

    public static string Of(Room room) => room switch
    {
        Room.R05 => "R-05", Room.R06 => "R-06", Room.R07 => "R-07", Room.R08 => "R-08",
        Room.Corridor => "走廊", Room.Hall => "活动厅", Room.Pantry => "备餐间", Room.Foyer => "门厅",
        _ => "门外"
    };
}
