namespace DualOntologyWorldEngine.TempCore;

/// <summary>
/// 感知与认知转换接口：将输入投影转换为单次感知或侵蚀的结果。
/// 核心不变量：产出的结果其 Source 恒为 null，严禁向观察者泄露真实 Essence。
/// </summary>
public interface IProjectionTransform
{
    Projection Transform(Projection input, SpaceProjection? contextSpace, SoulEssence? observer);
}

/// <summary>
/// 常规感知转换：利用 Projection.CreateDetached() 消除运行时类型 switch。
/// 仅复制表面标签与表现，Source 自动置为 null。
/// </summary>
public class NormalTransform : IProjectionTransform
{
    public static NormalTransform Instance { get; } = new();

    public Projection Transform(Projection input, SpaceProjection? contextSpace, SoulEssence? observer)
    {
        var perceived = input.CreateDetached();
        foreach (var (k, v) in input.Tags)
        {
            perceived.Tags[k] = v;
        }
        return perceived;
    }
}

/// <summary>
/// 异常认知侵蚀转换：扭曲目标主观投影的语义身份关系。
/// </summary>
public class CorruptionTransform(ConceptProjection distortedTargetIdentity) : IProjectionTransform
{
    public ConceptProjection DistortedTargetIdentity { get; } = distortedTargetIdentity;

    public Projection Transform(Projection input, SpaceProjection? contextSpace, SoulEssence? observer)
    {
        if (!input.Relations.Any(r => r.Relation == StandardRelations.IsA || r.Relation == StandardRelations.HasIdentity))
        {
            return input;
        }

        var corrupted = input.CreateDetached();
        foreach (var (k, v) in input.Tags)
        {
            corrupted.Tags[k] = v;
        }

        // 扭曲其身份关系为异常目标身份
        foreach (var (rel, target) in input.Relations)
        {
            if (rel == StandardRelations.IsA || rel == StandardRelations.HasIdentity)
            {
                corrupted.Relations.Add((rel, DistortedTargetIdentity));
            }
            else
            {
                corrupted.Relations.Add((rel, target));
            }
        }

        return corrupted;
    }
}

/// <summary>
/// 感知管线：负责组织 Transform 并实现 Binding 从 Reality 向 Perceived 连续传播。
/// </summary>
public static class PerceptionPipeline
{
    public static T Perceive<T>(
        T reality,
        SpaceProjection currentSpace,
        SoulEssence observer,
        ProjectionBinding binding,
        IProjectionTransform? transform = null) where T : Projection
    {
        transform ??= NormalTransform.Instance;
        var perceived = (T)transform.Transform(reality, currentSpace, observer);

        // Binding 连续传播：Perceived 投影绑定到 Reality 对应的真实 Essence
        var realityEssence = binding.GetEssence(reality);
        if (realityEssence != null)
        {
            binding.Bind(perceived, realityEssence, reality);
        }

        return perceived;
    }
}

