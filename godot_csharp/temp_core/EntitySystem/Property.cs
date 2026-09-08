namespace DualOntologyWorldEngine.TempCore;

public class Property
{
    public Essence Source;
    public HashSet<Essence> Control;
}
/// <summary>
/// 描述物体具有刚体性质。
/// </summary>
public class RigidProperty : Property
{
}

/// <summary>
/// 描述物体可以被破坏。
/// </summary>
public class DestructibleProperty : Property
{
}

/// <summary>
/// 描述物体可以被消耗。
/// </summary>
public class ConsumableProperty : Property
{
}

/// <summary>
/// 描述腐化作用。
/// </summary>
public class CorruptionProperty : Property
{
}

/// <summary>
/// 描述物体可以作为投射物使用。
/// </summary>
public class ProjectileProperty : Property
{
}

/// <summary>
/// 描述物体可以作为近战武器使用。
/// </summary>
public class MeleeWeaponProperty : Property
{
}

/// <summary>
/// 描述物体可以被用于留下标记。
/// </summary>
public class MarkingProperty : Property
{
}

/// <summary>
/// 描述物体表面可以被书写或留下标记。
/// </summary>
public class WritableProperty : Property
{
}

/// <summary>
/// 描述物体可以有限地容纳其他物体。
/// </summary>
public class LimitedContainerProperty : Property
{
}

/// <summary>
/// 描述物体具有浮力相关性质。
/// </summary>
public class BuoyancyProperty : Property
{
}

/// <summary>
/// 描述持续生效的作用。
/// </summary>
public class ContinuousEffectProperty : Property
{
}

/// <summary>
/// 描述物体可以作为货币或具有货币交换意义。
/// </summary>
public class CurrencyProperty : Property
{
}

/// <summary>
/// 描述物体可以提供、储存或承载能量。
/// </summary>
public class EnergyProperty : Property
{
}

/// <summary>
/// 描述物体能够承载已经记录的信息。
/// </summary>
public class RecordProperty : Property
{
}

/// <summary>
/// 描述物体能够产生或提供光。
/// </summary>
public class LightProperty : Property
{
}
