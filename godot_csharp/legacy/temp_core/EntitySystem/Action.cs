namespace DualOntologyWorldEngine.TempCore;

/// <summary>
/// 主客观投影绑定映射：保存主观 Projection 与现实 Essence / RealityProjection 之间的对应关系。
/// 核心原则：Binding 纯属引擎调度与结算层持有，绝不是主体认知的一部分。
/// 纯幻觉对象的 Binding 为 null。
/// </summary>
public class ProjectionBinding
{
    private readonly Dictionary<Projection, Essence> _projToEssence = [];
    private readonly Dictionary<Essence, Projection> _essenceToRealityProj = [];
    private readonly Dictionary<(SoulEssence, Essence), Projection> _mentalProjections = [];

    public void Bind(Projection proj, Essence essence, Projection? realityProj = null)
    {
        _projToEssence[proj] = essence;
        if (realityProj != null)
        {
            _essenceToRealityProj[essence] = realityProj;
        }
    }

    public void BindMental(SoulEssence soul, Projection mentalProj, Essence realityEssence)
    {
        _mentalProjections[(soul, realityEssence)] = mentalProj;
        _projToEssence[mentalProj] = realityEssence;
    }

    public Projection? FindMentalProjection(SoulEssence soul, Essence realityEssence)
        => _mentalProjections.GetValueOrDefault((soul, realityEssence));

    public Essence? GetEssence(Projection proj) => _projToEssence.GetValueOrDefault(proj);

    public Projection? GetRealityProjection(Essence essence) => _essenceToRealityProj.GetValueOrDefault(essence);
}

/// <summary>行动意图基类，参数严格引用主观 Projection。</summary>
public abstract record ActionIntent(BodyProjection Actor);

/// <summary>开锁意图。</summary>
public record UnlockIntent(BodyProjection Actor, BoundaryProjection Door, PhysicalProjection Key) : ActionIntent(Actor);

/// <summary>空间跨越意图。</summary>
public record TravelIntent(BodyProjection Actor, BoundaryProjection Door) : ActionIntent(Actor);

/// <summary>持有物品意图。</summary>
public record TakeIntent(BodyProjection Actor, PhysicalProjection Item) : ActionIntent(Actor);

/// <summary>
/// 行动运行时结算入口。
/// 接收引用主观 Projection 的 ActionIntent，经由 ProjectionBinding 解绑定至真实 Essence / Property 结算。
/// 严禁直接篡改 RealityProjection 表现层，表现同步统一由 RealitySynchronizer 负责。
/// </summary>
public static class ActionRuntime
{
    public static EventEssence Execute(UnlockIntent intent, ProjectionBinding binding)
    {
        var actorBody = binding.GetEssence(intent.Actor) as BodyEssence;
        var doorEssence = binding.GetEssence(intent.Door) as BoundaryEssence;
        var keyEssence = binding.GetEssence(intent.Key) as PhysicalEssence;

        if (doorEssence == null || keyEssence == null)
        {
            return new EventEssence("AttemptUnlockEvent")
            {
                Success = false,
                Subject = actorBody,
                Object = doorEssence,
                Details = "目标或钥匙不存在真实物理实体（或为纯幻觉）。"
            };
        }

        var lockProp = doorEssence.GetProperty<LockProperty>();
        if (lockProp == null)
        {
            return new EventEssence("AttemptUnlockEvent")
            {
                Success = false,
                Subject = actorBody,
                Object = doorEssence,
                Details = "边界对象未挂载门禁锁机制。"
            };
        }

        var unlocked = lockProp.Unlock(keyEssence);
        if (unlocked)
        {
            return new EventEssence("UnlockEvent")
            {
                Success = true,
                Subject = actorBody,
                Object = doorEssence,
                Details = "钥匙语义身份匹配，开锁成功。"
            };
        }

        return new EventEssence("AttemptUnlockEvent")
        {
            Success = false,
            Subject = actorBody,
            Object = doorEssence,
            Details = "钥匙语义身份不匹配，开锁失败。"
        };
    }

    public static EventEssence Execute(TravelIntent intent, ProjectionBinding binding, SpaceEssence currentSpace)
    {
        var actorBody = binding.GetEssence(intent.Actor) as BodyEssence;
        var doorEssence = binding.GetEssence(intent.Door) as BoundaryEssence;

        if (actorBody == null || doorEssence == null)
        {
            return new EventEssence("TravelEvent")
            {
                Success = false,
                Subject = actorBody,
                Object = doorEssence,
                Details = "无效通行主体或边界通道。"
            };
        }

        var lockProp = doorEssence.GetProperty<LockProperty>();
        if (lockProp != null && lockProp.IsLocked)
        {
            return new EventEssence("TravelEvent")
            {
                Success = false,
                Subject = actorBody,
                Object = doorEssence,
                Details = "边界通道处于锁定状态，无法通行。"
            };
        }

        var success = doorEssence.Travell(currentSpace, actorBody);
        if (success)
        {
            return new EventEssence("TravelEvent")
            {
                Success = true,
                Subject = actorBody,
                Object = doorEssence,
                Details = "跨越空间通行成功。"
            };
        }

        return new EventEssence("TravelEvent")
        {
            Success = false,
            Subject = actorBody,
            Object = doorEssence,
            Details = "未找到对应空间出入口映射。"
        };
    }
}

/// <summary>
/// 现实表现层同步器：负责在客观真实 Essence / Property 发生改变后，将状态同步至现实表现层（RealityProjection）。
/// 保持 ActionRuntime 纯粹结算 Essence，不直接跨层篡改 Projection 表现。
/// </summary>
public static class RealitySynchronizer
{
    public static void Sync(Projection realityProjection, Essence essence)
    {
        if (essence is BoundaryEssence boundary && realityProjection is BoundaryProjection boundaryProj)
        {
            var lockProp = boundary.GetProperty<LockProperty>();
            if (lockProp != null)
            {
                boundaryProj.Tags[LockTag.Locked] = lockProp.IsLocked ? 1.0 : 0.0;
            }
        }
    }

    public static void Sync(ProjectionBinding binding, Essence essence)
    {
        var reality = binding.GetRealityProjection(essence);
        if (reality != null)
        {
            Sync(reality, essence);
        }
    }
}

