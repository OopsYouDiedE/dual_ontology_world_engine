using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>
/// 案发夜开始时的现实快照（第三部分 §11 第 10 条“重演从现实快照重建”）。
/// 重演新建独立世界、按侦探的行动记录重放白天，得到同样的结构（规则、物件、人物）；
/// 但白天的细节可能与现实不同（第一章带来的认知、调度先后），所以入夜时用这张快照覆盖可观察的处境：
/// 每人在哪、饥饱病伤、钩与欲望、认下了哪些规矩、对每个人的信任；各物件的状态与各板上贴着的规矩。
/// 之后只有假设的变量不同，夜里谁做什么仍由各人的决策推出。
/// </summary>
public sealed record NightSnapshot(
    int Day,
    IReadOnlyDictionary<P, NightSnapshot.PersonState> People,
    IReadOnlyDictionary<string, IReadOnlyDictionary<Tag, int>> Things,
    IReadOnlyDictionary<int, ImmutableHashSet<Place>> Posted)
{
    public sealed record PersonState(Place Place, IReadOnlyDictionary<Tag, int> Body, int Desire, int Hooks, int Whispered, int DemonDoubt, bool KnowsHookCost,
        int HungerStreak, IReadOnlyDictionary<int, Truth> Accepted, IReadOnlyDictionary<P, int> Trust);
}

public sealed partial class Game
{
    /// <summary>现实里案发夜开始时的快照（只在现实世界里记录，重演世界不记）。</summary>
    public NightSnapshot? CaseNightSnapshot { get; private set; }

    private NightSnapshot CaptureNight() => new(Day,
        People.ToDictionary(p => p.Id, p => new NightSnapshot.PersonState(Where(p),
            new Dictionary<Tag, int>(p.Body.Get<StateProperty>()?.Values ?? []), p.Desire, p.Hooks, p.Whispered, p.DemonDoubt, p.KnowsHookCost, p.HungerStreak,
            p.Acc.ToDictionary(a => a.Key.Id, a => a.Value), People.Where(o => o != p).ToDictionary(o => o.Id, o => TrustIn(p, o)))),
        Things.ToDictionary(t => t.TrueName, t => (IReadOnlyDictionary<Tag, int>)new Dictionary<Tag, int>(t.Get<StateProperty>()?.Values ?? [])),
        Specs.ToDictionary(s => s.Id, s => s.PostedAt.ToImmutableHashSet()));

    private void RestoreNight(NightSnapshot snap)
    {
        foreach (var thing in Things)
            if (snap.Things.TryGetValue(thing.TrueName, out var values))
                foreach (var (tag, value) in values) Set(thing, tag, value);
        foreach (var spec in Specs)
            if (snap.Posted.TryGetValue(spec.Id, out var at))
                foreach (var place in at.Where(a => !spec.PostedAt.Contains(a))) Post(spec, place);
        foreach (var p in People)
        {
            if (!snap.People.TryGetValue(p.Id, out var s)) continue;
            p.Body.Get<SpatialProperty>()!.Space = Space[s.Place];
            foreach (var (tag, value) in s.Body) Set(p.Body, tag, value);
            p.Desire = s.Desire; p.Hooks = s.Hooks; p.Whispered = s.Whispered; p.DemonDoubt = s.DemonDoubt; p.KnowsHookCost = s.KnowsHookCost; p.HungerStreak = s.HungerStreak;
            foreach (var spec in Specs)
                if (s.Accepted.TryGetValue(spec.Id, out var truth)) p.Acc[spec] = truth;
            foreach (var (other, trust) in s.Trust)
            {
                var them = p.M[this[other].Body];
                var delta = trust - TrustIn(p, this[other]);
                if (delta == 0) continue;
                Observation.Observe(p.Soul, new ObservationInput(them.Name, them.Kind)
                { Target = them, Source = ObservationSource.Recall, Time = Now, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, them.Tags.GetValueOrDefault(Tags.Trust) + delta) });
            }
        }
        RealDirty = true;
        Rt.Sync();
        PerceiveAll();
    }

    /// <summary>入夜、计划重建之前：现实世界在案发夜记快照；重演世界在同一时刻用快照覆盖处境。</summary>
    private void NightSnapshotHook()
    {
        if (Slot != Slot.Night) return;
        if (Options.RestoreNight is { } snap) { if (snap.Day == Day) RestoreNight(snap); }
        else if (Day == CaseDay - 1) CaseNightSnapshot = CaptureNight();
    }
}
