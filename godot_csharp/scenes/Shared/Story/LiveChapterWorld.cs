using DualOntologyWorldEngine.Core;
namespace DualOntologyWorldEngine.Story;

public sealed record ContinuingCharacter(SoulEssence Soul, Essence Body, Projection? Self,
    IReadOnlyDictionary<Essence, Projection> Known, int Hooks, int Desire, bool KnowsRecallCost);

/// <summary>A live handoff, retaining exact Core references. This is not a reconstructed save.</summary>
public sealed record LiveChapterWorld(WorldRuntime Runtime, SimulationSession Simulation,
    IReadOnlyDictionary<string, Essence> Entities, IReadOnlyDictionary<string, ContinuingCharacter> Characters,
    ChapterSession Progress, WorldSession? Session = null);

public static class ChapterWorldContinuity
{
    public static LiveChapterWorld? Current { get; private set; }
    public static void Publish(LiveChapterWorld world) => Current = world;
    public static void Clear() => Current = null;
}
