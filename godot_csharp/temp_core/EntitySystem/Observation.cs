namespace DualOntologyWorldEngine.TempCore;

/// <summary>
/// 单次观察事件记录。
/// </summary>
public record ObservationEvent(
    SoulEssence Observer,
    SpaceProjection Space,
    Projection ObservedTarget,
    string Details);

/// <summary>
/// 观察摄入系统：将当前主体实际能够观察到的投影特征摄入主观世界。
/// 核心约束：
/// 1. 观察者固定为 SoulEssence。
/// 2. 消费已经由 PerceptionPipeline 转换完毕的 PerceivedProjection（Source == null）。
/// 3. 严禁通过 Projection.Source 读取底层真实 Essence。
/// 4. 基于 Binding 连续性确定对象在主观世界的对应实体（严禁以 Name 代替身份）。
/// 5. 纯通用机制，严禁包含特定业务场景的符号推论硬编码。
/// </summary>
public static class Observation
{
    public static ObservationEvent Observe(SoulEssence observer, SpaceProjection currentSpace, PhysicalProjection perceived, ProjectionBinding binding)
        => IngestPerceived(observer, currentSpace, perceived, binding);

    public static ObservationEvent Observe(SoulEssence observer, SpaceProjection currentSpace, BoundaryProjection perceived, ProjectionBinding binding)
        => IngestPerceived(observer, currentSpace, perceived, binding);

    public static ObservationEvent Observe(SoulEssence observer, SpaceProjection currentSpace, SpaceProjection perceived, ProjectionBinding binding)
        => IngestPerceived(observer, currentSpace, perceived, binding);

    public static ObservationEvent Observe(SoulEssence observer, SpaceProjection currentSpace, BodyProjection perceived, ProjectionBinding binding)
        => IngestPerceived(observer, currentSpace, perceived, binding);

    public static ObservationEvent Observe(SoulEssence observer, SpaceProjection currentSpace, ConceptProjection perceived, ProjectionBinding binding)
        => IngestPerceived(observer, currentSpace, perceived, binding);

    public static ObservationEvent Observe(SoulEssence observer, SpaceProjection currentSpace, SoulProjection perceived, ProjectionBinding binding)
        => IngestPerceived(observer, currentSpace, perceived, binding);

    public static ObservationEvent Observe(SoulEssence observer, SpaceProjection currentSpace, AreaProjection perceived, ProjectionBinding binding)
        => IngestPerceived(observer, currentSpace, perceived, binding);

    private static ObservationEvent IngestPerceived<T>(SoulEssence observer, SpaceProjection currentSpace, T perceived, ProjectionBinding binding) where T : Projection
    {
        // 1. 基于 Binding 连续性检索该主体主观世界中对应的认知投影（而非根据 Name）
        var realityEssence = binding.GetEssence(perceived);
        T? mentalTarget = null;

        if (realityEssence != null)
        {
            mentalTarget = binding.FindMentalProjection(observer, realityEssence) as T;
        }

        // 2. 若尚未建立对应的主观投影，则实例化一个全新的独立主观投影并建立连续绑定
        if (mentalTarget == null)
        {
            mentalTarget = (T)perceived.CreateDetached();
            observer.World.Add(mentalTarget);

            if (realityEssence != null)
            {
                binding.BindMental(observer, mentalTarget, realityEssence);
            }
        }

        // 3. 将单次观察到的表面标签特征增量同步至主观认知投影
        var delta = new ProjectionDelta(mentalTarget)
        {
            ChangedTags = new Dictionary<Tag, double>(perceived.Tags)
        };
        observer.World.Apply(delta);

        return new ObservationEvent(observer, currentSpace, perceived, $"Ingested perceived projection: {perceived.Name}");
    }
}



