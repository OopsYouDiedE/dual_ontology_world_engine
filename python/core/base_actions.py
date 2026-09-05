"""引擎核心通用基础动作系统 (Base Actions)。

工作代码：为角色提供与空间、物品、容器、工具及其他角色的标准交互动作 Intent。
所有意图均基于核心 Entity、F、E 与 Intent 构建，并在 ActionRuntime 中自动
接受规则复核、契约校验、行动值消耗以及因果事件传播。
"""

from __future__ import annotations

from collections.abc import Callable, Mapping
from typing import Any

from .entity_system import Context, Entity, TreeMember
from .soul import E, Effect, F, Intent, Soul


def get_actor_mind(actor: Entity, mind: Soul | None = None) -> Soul:
    """获取角色的心智主体；优先使用已绑定的 Soul。"""
    if mind is not None:
        return mind
    if hasattr(actor, "souls") and actor.souls:
        return actor.souls[0]
    fallback = Soul(f"{actor.name}的心智", actor.parent)
    if hasattr(actor, "bind"):
        actor.bind(fallback)
    return fallback


def move_intent(
    world: Any,
    actor: Entity,
    destination: Entity,
    cost: int | float | None = None,
    mind: Soul | None = None,
    env: Any = None,
) -> Intent:
    """角色在世界拓扑场景之间移动的通用动作。"""
    active_mind = get_actor_mind(actor, mind)
    start = actor.parent

    motion_cost = 1
    if cost is not None:
        motion_cost = cost
    elif hasattr(world, "links") and start in world.links and destination in world.links[start]:
        edge = world.links[start][destination]
        motion_cost = edge.cost if hasattr(edge, "cost") else edge
    elif hasattr(world, "route"):
        route = world.route(start, destination, actor=actor, env=env)
        if route is not None:
            motion_cost = route.cost or 1

    def valid() -> bool:
        if actor.parent is destination:
            return False
        if hasattr(world, "links"):
            if start not in world.links or destination not in world.links[start]:
                return False
            edge = world.links[start][destination]
            if hasattr(edge, "allows") and not edge.allows(actor, env):
                return False
        return True

    def resolve() -> bool:
        actor.reparent(destination)
        if env is not None:
            env.assert_fact(E("located_at", actor, destination))
        active_mind.observe(E("located_at", actor, destination))
        return True

    return Intent(
        actor=actor,
        mind=active_mind,
        effect=F("move", actor, destination),
        motion_cost=motion_cost,
        resolve=resolve,
        label=f"移动：{actor.name}前往{destination.name}",
        validate=valid,
    )


def take_intent(
    actor: Entity,
    item: Entity,
    cost: int | float = 1,
    mind: Soul | None = None,
) -> Intent:
    """从当前场景地面拾取自由物品至随身背包或手中的通用动作。"""
    active_mind = get_actor_mind(actor, mind)

    def valid() -> bool:
        return item.parent is actor.parent

    def resolve() -> bool:
        item.pick_up(actor)
        active_mind.observe(E("holds", actor, item))
        return True

    return Intent(
        actor=actor,
        mind=active_mind,
        effect=F("take", actor, item),
        motion_cost=cost,
        resolve=resolve,
        label=f"拾取：{actor.name}获得{item.name}",
        validate=valid,
    )


def drop_intent(
    actor: Entity,
    item: Entity,
    cost: int | float = 1,
    mind: Soul | None = None,
) -> Intent:
    """放下随身物品至当前场景地面的通用动作。"""
    active_mind = get_actor_mind(actor, mind)

    def valid() -> bool:
        return item.parent is actor

    def resolve() -> bool:
        scene = actor.parent
        item.drop()
        active_mind.observe(E("located_at", item, scene))
        return True

    return Intent(
        actor=actor,
        mind=active_mind,
        effect=F("drop", actor, item),
        motion_cost=cost,
        resolve=resolve,
        label=f"丢弃：{actor.name}放下{item.name}",
        validate=valid,
    )


def store_intent(
    actor: Entity,
    item: Entity,
    container: Entity,
    cost: int | float = 1,
    mind: Soul | None = None,
) -> Intent:
    """将随身物品存放进场景容器（箱子、储物柜等）的通用动作。"""
    active_mind = get_actor_mind(actor, mind)

    def valid() -> bool:
        return (
            item.parent is actor
            and container.parent is actor.parent
            and container.has("Container")
            and container.accessible()
        )

    def resolve() -> bool:
        item.put_into(container)
        active_mind.observe(E("stored", item, container))
        return True

    return Intent(
        actor=actor,
        mind=active_mind,
        effect=F("store", actor, item),
        motion_cost=cost,
        resolve=resolve,
        label=f"收纳：{actor.name}将{item.name}存入{container.name}",
        validate=valid,
    )


def retrieve_intent(
    actor: Entity,
    item: Entity,
    container: Entity,
    cost: int | float = 1,
    mind: Soul | None = None,
) -> Intent:
    """从场景容器中取出物品至随身背包或手中的通用动作。"""
    active_mind = get_actor_mind(actor, mind)

    def valid() -> bool:
        return (
            item.parent is container
            and container.parent is actor.parent
            and container.has("Container")
            and container.accessible()
        )

    def resolve() -> bool:
        item.take_from(container, actor)
        active_mind.observe(E("holds", actor, item))
        return True

    return Intent(
        actor=actor,
        mind=active_mind,
        effect=F("retrieve", actor, item),
        motion_cost=cost,
        resolve=resolve,
        label=f"取出：{actor.name}从{container.name}拿出{item.name}",
        validate=valid,
    )


def open_intent(
    actor: Entity,
    target: Entity,
    cost: int | float = 1,
    mind: Soul | None = None,
) -> Intent:
    """打开场景中的门或容器的通用动作。"""
    active_mind = get_actor_mind(actor, mind)

    def valid() -> bool:
        return (
            target.has("Openable")
            and not target.get("opened", False)
            and not target.locked({"from_scene": actor.parent})
        )

    def resolve() -> bool:
        target.open({"from_scene": actor.parent})
        active_mind.observe(E("opened", target))
        return True

    return Intent(
        actor=actor,
        mind=active_mind,
        effect=F("open", actor, target),
        motion_cost=cost,
        resolve=resolve,
        label=f"打开：{actor.name}开启{target.name}",
        validate=valid,
    )


def close_intent(
    actor: Entity,
    target: Entity,
    cost: int | float = 1,
    mind: Soul | None = None,
) -> Intent:
    """关闭场景中的门或容器的通用动作。"""
    active_mind = get_actor_mind(actor, mind)

    def valid() -> bool:
        return target.has("Openable") and target.get("opened", False)

    def resolve() -> bool:
        target.close()
        active_mind.observe(E("closed", target))
        return True

    return Intent(
        actor=actor,
        mind=active_mind,
        effect=F("close", actor, target),
        motion_cost=cost,
        resolve=resolve,
        label=f"关闭：{actor.name}合上{target.name}",
        validate=valid,
    )


def unlock_intent(
    actor: Entity,
    target: Entity,
    key: Entity,
    cost: int | float = 1,
    mind: Soul | None = None,
) -> Intent:
    """使用钥匙或工具解除门或容器锁闭的通用动作。"""
    active_mind = get_actor_mind(actor, mind)

    def valid() -> bool:
        return key.parent is actor and target.locked({"from_scene": actor.parent})

    def resolve() -> bool:
        target.unlock(side=actor.parent)
        active_mind.observe(E("unlocked", target))
        return True

    return Intent(
        actor=actor,
        mind=active_mind,
        effect=F("unlock", actor, target),
        motion_cost=cost,
        resolve=resolve,
        label=f"解锁：{actor.name}使用{key.name}开启{target.name}的锁",
        validate=valid,
    )


def lock_intent(
    actor: Entity,
    target: Entity,
    key: Entity,
    cost: int | float = 1,
    mind: Soul | None = None,
) -> Intent:
    """使用钥匙或工具锁闭门或容器的通用动作。"""
    active_mind = get_actor_mind(actor, mind)

    def valid() -> bool:
        return key.parent is actor and not target.locked({"from_scene": actor.parent})

    def resolve() -> bool:
        target.lock(1, side=actor.parent)
        active_mind.observe(E("locked", target))
        return True

    return Intent(
        actor=actor,
        mind=active_mind,
        effect=F("lock", actor, target),
        motion_cost=cost,
        resolve=resolve,
        label=f"上锁：{actor.name}使用{key.name}锁上{target.name}",
        validate=valid,
    )


def hold_intent(
    actor: Entity,
    item: Entity,
    cost: int | float = 1,
    mind: Soul | None = None,
) -> Intent:
    """将随身物品切换为主手持/装配状态的通用动作。"""
    active_mind = get_actor_mind(actor, mind)

    def valid() -> bool:
        return item.parent is actor and not item.has("Held")

    def resolve() -> bool:
        for other in actor.items:
            if isinstance(other, Entity) and other is not item:
                other.unhold()
        item.hold(actor)
        active_mind.observe(E("ready_weapon_or_tool", actor, item))
        return True

    return Intent(
        actor=actor,
        mind=active_mind,
        effect=F("hold", actor, item),
        motion_cost=cost,
        resolve=resolve,
        label=f"持握：{actor.name}持握{item.name}",
        validate=valid,
    )


def unhold_intent(
    actor: Entity,
    item: Entity,
    cost: int | float = 1,
    mind: Soul | None = None,
) -> Intent:
    """收起手持物品至随身背包的通用动作。"""
    active_mind = get_actor_mind(actor, mind)

    def valid() -> bool:
        return item.parent is actor and item.has("Held")

    def resolve() -> bool:
        item.unhold()
        return True

    return Intent(
        actor=actor,
        mind=active_mind,
        effect=F("unhold", actor, item),
        motion_cost=cost,
        resolve=resolve,
        label=f"收起：{actor.name}收起{item.name}",
        validate=valid,
    )


def examine_intent(
    actor: Entity,
    target: Entity,
    cost: int | float = 1,
    mind: Soul | None = None,
) -> Intent:
    """检视场景中的物品、设施或容器并形成认知的通用动作。"""
    active_mind = get_actor_mind(actor, mind)

    def valid() -> bool:
        return (
            target.parent is actor
            or target.parent is actor.parent
            or (target.parent is not None and target.parent.parent is actor.parent)
        )

    def resolve() -> bool:
        data = target.examine(actor)
        active_mind.observe(E("examined", target))
        if data.get("claims"):
            for claim in data["claims"]:
                active_mind.observe(E("fact", claim))
        return True

    return Intent(
        actor=actor,
        mind=active_mind,
        effect=F("examine", actor, target),
        motion_cost=cost,
        resolve=resolve,
        label=f"检视：{actor.name}仔细观察{target.name}",
        validate=valid,
    )


def read_intent(
    actor: Entity,
    target: Entity,
    cost: int | float = 1,
    mind: Soul | None = None,
) -> Intent:
    """阅读图书、公文、手稿或便签并吸收知识的通用动作。"""
    active_mind = get_actor_mind(actor, mind)

    def valid() -> bool:
        return (
            (target.parent is actor or target.parent is actor.parent)
            and (
                target.has("ReadingMaterial")
                or target.has("InformationCarrier")
                or target.has("Book")
                or target.has("Document")
                or target.has("Manuscript")
            )
        )

    def resolve() -> bool:
        active_mind.observe(E("read", target))
        content = target.get("content")
        if content is not None:
            active_mind.learn(target, (E("text", content),), source=target, stage="accepted")
        for claim in target.entries("Claim"):
            active_mind.learn(target, (E("claim", claim.value),), source=target, stage="accepted")
        return True

    return Intent(
        actor=actor,
        mind=active_mind,
        effect=F("read", actor, target),
        motion_cost=cost,
        resolve=resolve,
        label=f"阅读：{actor.name}翻阅{target.name}",
        validate=valid,
    )


def use_intent(
    actor: Entity,
    tool: Entity,
    target: Entity | None = None,
    cost: int | float = 1,
    mind: Soul | None = None,
    use_effect: Callable[[], Any] | None = None,
) -> Intent:
    """使用工具或操作场景设备的通用动作。"""
    active_mind = get_actor_mind(actor, mind)

    def valid() -> bool:
        return tool.parent is actor or tool.parent is actor.parent

    def resolve() -> bool:
        if use_effect is not None:
            use_effect()
        active_mind.observe(E("used", tool, target))
        return True

    return Intent(
        actor=actor,
        mind=active_mind,
        effect=F("use", actor, tool),
        motion_cost=cost,
        resolve=resolve,
        label=f"使用：{actor.name}使用{tool.name}" + (f"于{target.name}" if target else ""),
        validate=valid,
    )


def consume_intent(
    actor: Entity,
    consumable: Entity,
    cost: int | float = 1,
    mind: Soul | None = None,
) -> Intent:
    """食用、饮用或使用药剂/消耗品的通用动作。"""
    active_mind = get_actor_mind(actor, mind)

    def valid() -> bool:
        return (
            (consumable.parent is actor or consumable.parent is actor.parent)
            and consumable.has("Consumable")
        )

    def resolve() -> bool:
        effects = consumable.consume()
        if consumable.has("Food") or consumable.has("Drink"):
            actor.change("Panic", -1)
            actor.change("Health", 1)
            active_mind.change("Stability", 1)
        active_mind.observe(E("consumed", consumable))
        return True

    return Intent(
        actor=actor,
        mind=active_mind,
        effect=F("consume", actor, consumable),
        motion_cost=cost,
        resolve=resolve,
        label=f"消耗：{actor.name}使用{consumable.name}",
        validate=valid,
    )


def rest_intent(
    actor: Entity,
    resting_place: Entity,
    cost: int | float = 2,
    mind: Soul | None = None,
) -> Intent:
    """在床铺、长椅或休息区休憩恢复的通用动作。"""
    active_mind = get_actor_mind(actor, mind)

    def valid() -> bool:
        return resting_place.parent is actor.parent and resting_place.has("RestingPlace")

    def resolve() -> bool:
        actor.change("Panic", -2)
        active_mind.change("Stability", 2)
        active_mind.observe(E("rested", resting_place))
        return True

    return Intent(
        actor=actor,
        mind=active_mind,
        effect=F("rest", actor, resting_place),
        motion_cost=cost,
        resolve=resolve,
        label=f"休息：{actor.name}在{resting_place.name}休憩",
        validate=valid,
    )


def transfer_intent(
    giver: Entity,
    receiver: Entity,
    item: Entity,
    cost: int | float = 1,
    mind: Soul | None = None,
) -> Intent:
    """同场景角色之间转交随身物品的通用动作。"""
    active_mind = get_actor_mind(giver, mind)

    def valid() -> bool:
        return item.parent is giver and giver.parent is receiver.parent

    def resolve() -> bool:
        item.reparent(receiver)
        item.unhold()
        active_mind.observe(E("transferred", item, receiver))
        if hasattr(receiver, "souls") and receiver.souls:
            receiver.souls[0].observe(E("received", item, giver))
        return True

    return Intent(
        actor=giver,
        mind=active_mind,
        effect=F("transfer", giver, item),
        motion_cost=cost,
        resolve=resolve,
        label=f"交接：{giver.name}将{item.name}递给{receiver.name}",
        validate=valid,
    )
