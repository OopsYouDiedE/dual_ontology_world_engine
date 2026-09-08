namespace DualOntologyWorldEngine.TempCore;

/// <summary>
/// 挂载于 Essence 上的属性与机制基类。
/// Source 决定效果来源与强度依据；Control 表示作用目标集合。
/// </summary>
public class Property
{
    public Essence? Source { get; set; }
    public HashSet<Essence> Control { get; } = [];
}

/// <summary>
/// 记录物理实体或身体当前所处的真实空间。
/// </summary>
public class SpatialProperty : Property
{
    public SpaceEssence? Space { get; set; }
}

/// <summary>
/// 基于语义身份匹配与状态标签的门禁锁机制。
/// </summary>
public class LockProperty : Property
{
    public bool IsLocked => Source?.Tags.TryGetValue(LockTag.Locked, out var val) == true && val > 0;

    /// <summary>
    /// 基于语义身份比对传入钥匙是否匹配：
    /// 宿主边界所声明的 AcceptsKey -> Concept 与钥匙所具有的 HasIdentity -> Concept 是否指向同一 ConceptEssence。
    /// </summary>
    public bool CanUnlock(Essence key)
    {
        if (Source == null) return false;

        var acceptedIdentities = Source.Relations
            .Where(r => r.Relation == RelationNames.AcceptsKey)
            .Select(r => r.Target)
            .ToHashSet();

        if (acceptedIdentities.Count == 0) return false;

        var keyIdentities = key.Relations
            .Where(r => r.Relation == RelationNames.HasIdentity)
            .Select(r => r.Target);

        return keyIdentities.Any(acceptedIdentities.Contains);
    }

    /// <summary>
    /// 执行开锁尝试。若匹配成功，直接将宿主客观状态 Tags[Locked] 置为 0。
    /// </summary>
    public bool Unlock(Essence key)
    {
        if (Source == null || !IsLocked) return false;
        if (!CanUnlock(key)) return false;

        Source.Tags[LockTag.Locked] = 0;
        return true;
    }

    public void Lock()
    {
        if (Source != null)
            Source.Tags[LockTag.Locked] = 1;
    }
}

/// <summary>
/// 描述物体外观表面物理特征与线索（如刻痕）。
/// </summary>
public class SurfaceProperty : Property
{
    public string? Symbol { get; set; }
    public Dictionary<Tag, double> SurfaceTags { get; } = [];
}

/// <summary>
/// 描述异常场对处于场内主体的认知侵蚀作用。
/// </summary>
public class CorruptionProperty : Property
{
    public double Intensity { get; set; } = 1.0;

    /// <summary>
    /// 对目标主体的主观世界实施认知扭曲。
    /// </summary>
    public void Corrupt(SoulEssence targetSoul, IProjectionTransform transform, SpaceProjection currentSpace)
    {
        var targets = targetSoul.World.Projections.ToList();
        foreach (var p in targets)
        {
            var transformed = transform.Transform(p, currentSpace, targetSoul);
            if (transformed != null && !ReferenceEquals(transformed, p))
            {
                targetSoul.World.Replace(p, transformed);
            }
        }
    }
}

/// <summary>描述物体具有刚体性质。</summary>
public class RigidProperty : Property { }

/// <summary>描述物体可以被破坏。</summary>
public class DestructibleProperty : Property { }

/// <summary>描述物体可以被消耗。</summary>
public class ConsumableProperty : Property { }

/// <summary>描述物体可以作为投射物使用。</summary>
public class ProjectileProperty : Property { }

/// <summary>描述物体可以作为近战武器使用。</summary>
public class MeleeWeaponProperty : Property { }

/// <summary>描述物体可以被用于留下标记。</summary>
public class MarkingProperty : Property { }

/// <summary>描述物体表面可以被书写或留下标记。</summary>
public class WritableProperty : Property { }

/// <summary>描述物体可以有限地容纳其他物体。</summary>
public class LimitedContainerProperty : Property { }

/// <summary>描述物体具有浮力相关性质。</summary>
public class BuoyancyProperty : Property { }

/// <summary>描述持续生效的作用。</summary>
public class ContinuousEffectProperty : Property { }

/// <summary>描述物体可以作为货币或具有货币交换意义。</summary>
public class CurrencyProperty : Property { }

/// <summary>描述物体可以提供、储存或承载能量。</summary>
public class EnergyProperty : Property { }

/// <summary>描述物体能够承载已经记录的信息。</summary>
public class RecordProperty : Property { }

/// <summary>描述物体能够产生或提供光。</summary>
public class LightProperty : Property { }

