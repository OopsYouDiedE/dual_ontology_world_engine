using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Awakening;

/// <summary>四个人。Detective 是玩家。</summary>
// STORY-HARDCODE[AWK-063][schema][production]: 本案角色/房间及 V1–V6 选项由连续固定枚举编码。
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

/// <summary>一句话的完整内容、传声管接头漏出的重读句尾，以及听清时可解读出的意思。内容来自 chapter_01.story 的“话语”与“试验句”。</summary>
public sealed record Utterance(string Full, string Tail, bool Bolt, bool ForbidOpen, bool HoldTube, bool Open, bool Help, bool TailWantsOpen, bool TailHelp)
{
    internal static Utterance From(System.Text.Json.JsonElement e) => new(
        e.GetProperty("full").GetString()!, e.GetProperty("tail").GetString()!, e.GetProperty("Bolt").GetBoolean(),
        e.GetProperty("ForbidOpen").GetBoolean(), e.GetProperty("HoldTube").GetBoolean(), e.GetProperty("Open").GetBoolean(),
        e.GetProperty("Help").GetBoolean(), e.GetProperty("TailWantsOpen").GetBoolean(), e.GetProperty("TailHelp").GetBoolean());

    private static readonly Lazy<ImmutableArray<Utterance>> Spoken = new(() =>
        [..Chapter1Data.Root.GetProperty("UTTERANCES").EnumerateObject().Select(p => From(p.Value))]);

    /// <summary>变量 V4 的两个选项依次对应“话语”里的两条（与 VoiceWords 枚举同序）。</summary>
    public static Utterance BoltKeepShut => Spoken.Value[(int)VoiceWords.BoltKeepShut];
    public static Utterance OpenSaveMe => Spoken.Value[(int)VoiceWords.OpenSaveMe];

    public static Utterance Of(VoiceWords words) => Spoken.Value[(int)words];

    // 句尾的含义由剧本声明，改写显示文字不会改变听者认知。
}

/// <summary>声音到达某个位置时剩下什么。</summary>
public enum Hearing { Nothing, AlarmOnly, Murmur, Tail, Full, FullMuffled }

/// <summary>
/// 声路（设计 §4.2 感知表）。这是游戏层的空间事实：铜口的传声管一路沿活动厅北墙进门厅，
/// 贴着隔离门门框通往主楼；一支走活动厅与 R-08 之间的墙内。两处松动接头只漏出低沉的重读音节。
/// </summary>
public static class Acoustics
{
    public sealed record Joint(string Name, Room Room, bool Loose);
    private sealed record Data(Room TubeRoom, Room DoorInner, Room DoorOuter, ImmutableArray<Joint> Joints,
        IReadOnlySet<Room> Murmur, IReadOnlySet<Hearing> SeemsOutside, IReadOnlySet<Room> Shout);

    private static readonly Lazy<Data> Loaded = new(() =>
    {
        var root = Chapter1Data.Root.GetProperty("ACOUSTICS");
        var laws = root.GetProperty("laws");
        var door = laws.GetProperty("door");
        return new(RoomOf(laws.GetProperty("tube_room").GetString()!), RoomOf(door[0].GetString()!), RoomOf(door[1].GetString()!),
            [.. laws.GetProperty("joints").EnumerateArray().Select(j => new Joint(j.GetProperty("name").GetString()!,
                RoomOf(j.GetProperty("room").GetString()!), j.GetProperty("loose").GetBoolean()))],
            laws.GetProperty("murmur").EnumerateArray().Select(r => RoomOf(r.GetString()!)).ToHashSet(),
            root.GetProperty("seems_outside").EnumerateArray().Select(h => Enum.Parse<Hearing>(h.GetString()!)).ToHashSet(),
            root.GetProperty("hears_shout").EnumerateArray().Select(r => RoomOf(r.GetString()!)).ToHashSet());
    });

    private static Room RoomOf(string key) => (Room)Array.IndexOf(Names.RoomKeys, key);

    /// <summary>story 声明的接头及其初始松紧（昨夜即如此）。</summary>
    public static ImmutableArray<Joint> AuthoredJoints => Loaded.Value.Joints;

    /// <summary>
    /// 按声学法则推出听感：铜口所在完整，松动接头处只剩句尾，隔墙低语处嗡嗡；门外的喊声门内发闷，
    /// 门内房间若有松动接头，声音钻进管子从别处松动接头发闷漏出。<paramref name="loose"/> 缺省为 story 声明的接头状态。
    /// </summary>
    // STORY-HARDCODE[AWK-001][logic][production]: 固定传播表规定铜口完整/接头句尾/隔墙低语/其余警铃，以及门内外喊声组合；拓扑与接头配置读取故事数据。
    public static Hearing Hear(VoiceOrigin origin, Room listener, IReadOnlyDictionary<string, bool>? loose = null)
    {
        var data = Loaded.Value;
        var leaking = data.Joints.Where(j => loose?.GetValueOrDefault(j.Name, j.Loose) ?? j.Loose).Select(j => j.Room).ToHashSet();
        if (origin == VoiceOrigin.Tube)
            return listener == data.TubeRoom ? Hearing.Full : leaking.Contains(listener) ? Hearing.Tail
                : data.Murmur.Contains(listener) ? Hearing.Murmur : Hearing.AlarmOnly;
        if (listener == data.DoorOuter) return Hearing.Full;
        if (listener == data.DoorInner) return Hearing.FullMuffled;
        return leaking.Contains(data.DoorInner) && leaking.Contains(listener) ? Hearing.FullMuffled : Hearing.Nothing;
    }

    /// <summary>只听见句尾或隔门听见时，声音听来像是从门外来的。</summary>
    public static bool SeemsFromOutside(Hearing hearing) => Loaded.Value.SeemsOutside.Contains(hearing);

    public static string Render(Utterance words, Hearing hearing) => hearing switch
    {
        Hearing.Full => Chapter1Data.Text("声响·完整", ("整句", words.Full)),
        Hearing.FullMuffled => Chapter1Data.Text("声响·隔门", ("整句", words.Full)),
        Hearing.Tail => Chapter1Data.Text("声响·句尾", ("句尾", words.Tail)),
        Hearing.Murmur => Chapter1Data.Text("声响·低语"),
        Hearing.AlarmOnly => Chapter1Data.Text("声响·仅警铃"),
        _ => Chapter1Data.Text("声响·无")
    };

    /// <summary>霍铎在活动厅朝走廊喊话：谁听得见由“喊声可闻”决定。</summary>
    public static bool HearsShout(Room listener) => Loaded.Value.Shout.Contains(listener);
}

/// <summary>声路试验可以让铜口说的句子。整句与句尾都来自 chapter_01.story 的“试验句”。</summary>
public static class TestSentences
{
    public static ImmutableArray<Utterance> All => Loaded.Value;
    private static readonly Lazy<ImmutableArray<Utterance>> Loaded = new(() =>
        [..Chapter1Data.Root.GetProperty("TEST_SENTENCES").EnumerateArray().Select(Utterance.From)]);
}

/// <summary>人物与房间的称呼来自 chapter_01.story 的“人名”“房间名”；键与枚举同序。</summary>
// STORY-HARDCODE[AWK-002][schema][production]: 枚举键与本章人物、房间名称按固定顺序绑定。
public static class Names
{
    public static readonly string[] PersonKeys = ["detective", "huo", "shen", "su"];
    public static readonly string[] RoomKeys = ["R05", "R06", "R07", "R08", "corridor", "hall", "pantry", "foyer", "lounge"];
    public static string Of(Person person) => Chapter1Data.PersonName(PersonKeys[(int)person]);
    public static Person PersonOf(string key) => Array.IndexOf(PersonKeys, key) is var i and >= 0 ? (Person)i : throw new ArgumentException("Unknown person key: " + key);
    public static string Of(Room room) => Chapter1Data.RoomName(RoomKeys[(int)room]);
}
