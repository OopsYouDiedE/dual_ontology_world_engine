using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Awakening;

public sealed record HeardAt(Person Who, Room Where, Hearing Hearing, string Text, bool SeemsFromOutside);

/// <summary>可触物回忆的物件、回忆者、唤回的片段与它所证的变量（设计 §5）。</summary>
public sealed record RecallMemory(string Evidence, Person Who, string Fragment, ImmutableArray<int> Confirms);

/// <summary>
/// 清晨的现实：玩家实际所在的世界。证物是这里的 Essence；四人各有自己的 ProjectionWorld。
/// 声路试验经 Observation.Observe（感知）进入在场者的认知；触物回忆经 Observation.Observe（回忆）只进入回忆者本人的认知。
/// 昨夜重演从不读写这个世界。
/// </summary>
public sealed class MorningWorld
{
    public static readonly ImmutableArray<RecallMemory> Memories =
    [
        new("E5", Person.Su, "门边……有人在喊开门。我想开门让他进来。", [1]),
        new("E7", Person.Huo, "铜口里的声音很清楚……“守在铜口旁，等我下一次通知。”——他再也没有来。", [2, 3, 4]),
        new("E10", Person.Shen, "第二十四道。阿泠的茶不放糖，老霍的放两勺……", [6]),
        new("E1", Person.Detective, "霍铎说闩。我就闩了。我信他。", [5, 6]),
    ];

    private readonly WorldRuntime _runtime = new();
    private readonly Dictionary<Room, Essence> _space = [];
    private readonly Dictionary<Person, SoulEssence> _souls = [];
    private readonly Dictionary<Person, Essence> _bodies = [];
    private readonly Dictionary<string, Essence> _evidence = [];
    private readonly Dictionary<Person, int> _hooks = [];
    private readonly Dictionary<Person, Projection> _recalled = [];
    private double _time;

    public MorningWorld()
    {
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
        Evidence("E10", "酵种罐", Room.Pantry, ("刻痕", 24));
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
        }
    }

    private void Evidence(string id, string name, Room room, params (string Tag, int Value)[] facts)
    {
        var essence = new Essence(name);
        essence.Add(new SpatialProperty { Space = _space[room] });
        var state = essence.Add(new StateProperty());
        foreach (var (tag, value) in facts)
        {
            state.Values[new Tag(tag)] = value;
            state.Observable.Add(new Tag(tag));
        }
        _evidence[id] = essence;
        _runtime.Project(essence, name);
    }

    public ProjectionWorld WorldOf(Person who) => _souls[who].World;
    public int Hooks(Person who) => _hooks[who];
    public int TotalHooks => _hooks.Values.Sum();

    /// <summary>某人亲眼检视一件证物：只把它的可见状态经感知写入此人的认知。</summary>
    public void Examine(Person who, string evidence)
    {
        if (!_evidence.TryGetValue(evidence, out var essence)) return;
        _runtime.Observe(_souls[who], _runtime.Representations(essence), ++_time);
    }

    /// <summary>
    /// 声路试验：铜口说一句话，各人分别站在不同位置听。每个听者得到的感知由同一张声路表决定，
    /// 经 Observation.Observe 写入其本人的内部世界。
    /// </summary>
    public ImmutableArray<HeardAt> SoundTest(Utterance sentence, IReadOnlyDictionary<Person, Room> positions)
    {
        _time++;
        var heard = ImmutableArray.CreateBuilder<HeardAt>();
        foreach (var (who, room) in positions)
        {
            var hearing = Acoustics.Hear(VoiceOrigin.Tube, room);
            var text = Acoustics.Render(sentence, hearing);
            var outside = Acoustics.SeemsFromOutside(hearing);
            Observation.Observe(_souls[who], new ObservationInput("铜口试声", ProjectionKind.Event)
            {
                Source = ObservationSource.Perception, Existence = Truth.True, Time = _time, Details = text,
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(new Tag("听来像在门外"), outside ? 1 : 0)
            });
            heard.Add(new(who, room, hearing, text, outside));
        }
        return heard.ToImmutable();
    }

    /// <summary>触物回忆：本人触碰物件，唤回一个昨夜的认知（来源：回忆）。只写入回忆者本人；记一枚钩。</summary>
    public RecallMemory? Recall(string evidence)
    {
        var memory = Memories.FirstOrDefault(m => m.Evidence == evidence);
        if (memory is null) return null;
        if (_recalled.ContainsKey(memory.Who)) return memory;
        _recalled[memory.Who] = Observation.Observe(_souls[memory.Who], new ObservationInput("昨夜的片段", ProjectionKind.Event)
        {
            Source = ObservationSource.Recall, Existence = Truth.True, Time = ++_time, Details = memory.Fragment
        });
        _hooks[memory.Who]++;
        return memory;
    }

    public bool HasRecalled(Person who) => _recalled.ContainsKey(who);

    /// <summary>客观世界的指纹：所有证物与身体的状态。重演前后必须一致。</summary>
    public string RealityFingerprint()
    {
        var text = new StringBuilder();
        foreach (var (id, essence) in _evidence.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            text.Append(id).Append(':');
            foreach (var (tag, value) in essence.Get<StateProperty>()!.Values.OrderBy(p => p.Key.Name, StringComparer.Ordinal))
                text.Append(tag.Name).Append('=').Append(value).Append(';');
            text.Append(essence.Get<SpatialProperty>()!.Space?.TrueName).Append('|');
        }
        foreach (var (who, body) in _bodies) text.Append(who).Append('@').Append(body.Get<SpatialProperty>()!.Space?.TrueName).Append('|');
        return Hash(text.ToString());
    }

    /// <summary>某人内部世界的指纹：投影与历史。</summary>
    public string MindFingerprint(Person who)
    {
        var world = _souls[who].World;
        var text = new StringBuilder();
        foreach (var p in world.Projections)
        {
            text.Append(p.Name).Append(p.Existence).Append(p.Parent?.Name);
            foreach (var (tag, value) in p.Tags.OrderBy(t => t.Key.Name, StringComparer.Ordinal)) text.Append(tag.Name).Append(value);
            text.Append('|');
        }
        foreach (var h in world.History) text.Append(h.Source).Append(h.Details).Append(h.Time).Append('|');
        return Hash(text.ToString());
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
}
