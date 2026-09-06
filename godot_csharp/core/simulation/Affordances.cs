using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Timeline;

namespace DualOntologyWorldEngine.Core.Simulation;

/// <summary>交互槽位预约状态机枚举。</summary>
public enum SlotReservationState
{
    /// <summary>可用状态：槽位空闲，允许外部检索与打分。</summary>
    Available,

    /// <summary>已预约状态：智能体发起预约，对其他智能体暂时隐匿。</summary>
    Reserved,

    /// <summary>占用执行状态：智能体到达物理槽位，结算持续动作。</summary>
    Occupied,

    /// <summary>释放注销状态：动作完成或中断释放，回归可用。</summary>
    Released
}

/// <summary>物理交互槽位预约记录。</summary>
public sealed class SlotReservation(int slotIndex, Entity? user = null, SlotReservationState state = SlotReservationState.Available, double reservedAt = 0.0)
{
    public int SlotIndex { get; } = slotIndex;
    public Entity? User { get; internal set; } = user;
    public SlotReservationState State { get; internal set; } = state;
    public double ReservedAt { get; internal set; } = reservedAt;
}

/// <summary>物理交互槽位并发与互斥预约状态机管理器。</summary>
public sealed class InteractionSlotManager(int capacity = 1)
{
    public int Capacity { get; } = Math.Max(1, capacity);
    private readonly List<SlotReservation> _slots = Enumerable.Range(0, Math.Max(1, capacity))
        .Select(i => new SlotReservation(i))
        .ToList();

    public IReadOnlyList<SlotReservation> Slots => _slots;

    /// <summary>是否存在空闲可用槽位。</summary>
    public bool HasAvailableSlot => _slots.Any(s => s.State == SlotReservationState.Available);

    /// <summary>预约槽位：将首个可用槽位标记为 Reserved。</summary>
    public bool TryReserve(Entity user, double currentTime, out int slotIndex)
    {
        slotIndex = -1;
        var slot = _slots.FirstOrDefault(s => s.State == SlotReservationState.Available);
        if (slot == null) return false;

        slot.State = SlotReservationState.Reserved;
        slot.User = user;
        slot.ReservedAt = currentTime;
        slotIndex = slot.SlotIndex;
        return true;
    }

    /// <summary>占用槽位：智能体到达后将预约槽位切换为 Occupied。</summary>
    public bool TryOccupy(Entity user)
    {
        var slot = _slots.FirstOrDefault(s => ReferenceEquals(s.User, user) && s.State == SlotReservationState.Reserved);
        if (slot == null) return false;
        slot.State = SlotReservationState.Occupied;
        return true;
    }

    /// <summary>释放槽位：动作完成或中断后归还槽位至 Available。</summary>
    public bool Release(Entity user)
    {
        var slot = _slots.FirstOrDefault(s => ReferenceEquals(s.User, user) && s.State != SlotReservationState.Available);
        if (slot == null) return false;
        slot.State = SlotReservationState.Available;
        slot.User = null;
        slot.ReservedAt = 0.0;
        return true;
    }

    /// <summary>预约超时自动回收机制（防死锁）。</summary>
    public int CleanupTimedOut(double currentTime, double timeoutDuration)
    {
        var timedOut = _slots.Where(s => s.State == SlotReservationState.Reserved && (currentTime - s.ReservedAt) >= timeoutDuration).ToList();
        foreach (var s in timedOut)
        {
            s.State = SlotReservationState.Available;
            s.User = null;
            s.ReservedAt = 0.0;
        }
        return timedOut.Count;
    }

    /// <summary>二次合法性核验：验证指定主体当前是否仍合法持有该槽位预约或占用。</summary>
    public bool ValidateBeforeExecution(Entity user) =>
        _slots.Any(s => ReferenceEquals(s.User, user) && (s.State == SlotReservationState.Reserved || s.State == SlotReservationState.Occupied));
}

/// <summary>环境对象对外发布的可承受性能力描述符。</summary>
public record AffordanceDescriptor(
    string ActionIdentifier,
    InteractionSlotManager? Slots = null,
    Func<Entity, Soul, ActionContext?, bool>? Prerequisite = null,
    double Cost = 1.0,
    IReadOnlyDictionary<string, double>? PayoffProfile = null);

/// <summary>对象发布可交互能力；同一工厂函数可挂在多个对象上，无需逐个角色编写分支。</summary>
public delegate IEnumerable<Intent> AffordanceFactory(Entity target, Soul mind, Entity actor,
    RuleEnvironment environment, ActionContext? context);

/// <summary>
/// 能力（Affordances）相关的静态类，提供对象能力的创建和发现功能
/// </summary>
public static class Affordances
{
    public static Facet Offer(this Entity target, AffordanceFactory factory, object? source = null) =>
        target.Attach(FacetType.Affordance, factory, source: source);

    public static Facet Offer(this Entity target, AffordanceDescriptor descriptor, AffordanceFactory factory, object? source = null) =>
        target.Attach(FacetType.Affordance, factory, source: descriptor);

    /// <summary>
    /// 把感知范围内的对象能力接到现有 Soul 候选管线。范围由游戏决定，不默认读取整个世界。
    /// 游戏应在范围查询中排除不可见对象；每个 Intent 在执行前仍须复核资源和访问条件。
    /// </summary>
    public static Facet Discover(this Soul soul, Func<Entity, IEnumerable<Entity>> perceived) =>
        soul.InstallStep((mind, actor, env, context) =>
            perceived(actor).Distinct().ToArray().SelectMany(target =>
                target.Entries(FacetType.Affordance, context).ToArray()
                    .SelectMany(facet => facet.Value is AffordanceFactory factory
                        ? factory(target, mind, actor, env, context) : [])), source: perceived);
}
