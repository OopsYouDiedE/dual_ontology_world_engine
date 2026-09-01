"""空间拓扑、行动值序列与统一人物行动协调器。

Soul/规则位于 :mod:`soul`，言灵/配方位于 :mod:`kotodama`。本模块保留旧
聚合导入，并负责一个主行动内的复核、反应、效果、契约和观察顺序。
"""

from __future__ import annotations

from collections.abc import Callable, Iterable, Mapping
from dataclasses import dataclass
from heapq import heappop, heappush
from itertools import count
from math import inf
from typing import Any

from .entity_system import *
from .kotodama import *
from .soul import *


@dataclass(frozen=True, slots=True)
class Route:
    """一次可达路径及其累计行动代价。"""

    nodes: tuple[Entity, ...]
    cost: Number


class WorldSpace(Entity):
    """只保存场景连通关系的世界拓扑，不处理房间内部几何。"""

    def __init__(
        self,
        name: str,
        parent: Entity | None = None,
        tags: Mapping[str, int] | None = None,
        position: int = 0,
    ) -> None:
        """创建世界根节点；links 的键和值均直接使用场景对象引用。"""
        super().__init__(name, parent, {"World": 1, **(tags or {})}, position)
        self.links: dict[Entity, dict[Entity, Number | Passage]] = {}

    def connect(
        self, left: Entity, right: Entity, cost: Number, both_ways: bool = True
    ) -> None:
        """以固定代价连接两个场景；默认同时建立反向边。"""
        if cost <= 0:
            raise ValueError("场景穿越代价必须大于零")
        self.links.setdefault(left, {})[right] = cost
        self.links.setdefault(right, {})
        if both_ways:
            self.links[right][left] = cost

    def add_passage(self, passage: Passage) -> Passage:
        """把带自身通行条件的 Passage 注册为双向拓扑边。"""
        passage.reparent(self)
        self.links.setdefault(passage.left, {})[passage.right] = passage
        self.links.setdefault(passage.right, {})[passage.left] = passage
        return passage

    def route(
        self,
        start: Entity,
        goal: Entity,
        actor: Entity | None = None,
        env: RuleEnvironment | None = None,
    ) -> Route | None:
        """使用 Dijkstra 算法寻找当前条件下代价最低的场景路径。"""
        if start is goal:
            return Route((start,), 0)
        if start not in self.links or goal not in self.links:
            return None
        order = count()
        queue = [(0, next(order), start)]
        costs, previous = {start: 0.0}, {}
        while queue:
            cost, _, node = heappop(queue)
            if node is goal:
                path = [goal]
                while path[-1] is not start:
                    path.append(previous[path[-1]])
                return Route(tuple(reversed(path)), cost)
            if cost != costs.get(node):
                continue
            for neighbor, edge in self.links[node].items():
                if isinstance(edge, Passage):
                    if not edge.allows(node, actor, env):
                        continue
                    edge_cost = edge.cost_from(node)
                else:
                    edge_cost = edge
                candidate = cost + edge_cost
                if candidate >= costs.get(neighbor, inf):
                    continue
                costs[neighbor], previous[neighbor] = candidate, node
                heappush(queue, (candidate, next(order), neighbor))
        return None


@dataclass(slots=True)
class RuntimePolicy:
    """游戏层可替换的空间策略；核心默认只有场景级距离。"""

    has_geometry: bool = False

    def scene_of(self, entity: Entity | None) -> Entity | None:
        """沿世界树向上查找实体所属的最近 Scene。"""
        while entity and not entity.has("Scene"):
            entity = entity.parent
        return entity

    def interaction_route(
        self, space: WorldSpace, actor: Entity, target: Entity
    ) -> Route | None:
        """返回行动者与目标所在场景之间的可用路径。"""
        start, goal = self.scene_of(actor), self.scene_of(target)
        return space.route(start, goal, actor) if start and goal else None

    def interaction_cost(
        self, space: WorldSpace, actor: Entity, target: Entity
    ) -> Number:
        """返回交互路径代价；不可达时返回正无穷。"""
        route = self.interaction_route(space, actor, target)
        return route.cost if route else inf


TimelineCallback = Callable[[], Number | None]


class Timeline:
    """按行动值排序的离散时间队列。

    回调返回数字时会按该延迟再次登记，返回 ``None`` 时结束该行动槽。
    """

    def __init__(self) -> None:
        self.now: Number = 0
        self._queue: list[tuple[Number, int, str, TimelineCallback]] = []
        self._order = count()

    def schedule(self, delay: Number, label: str, callback: TimelineCallback) -> None:
        """从当前时间起延迟指定行动值登记回调。"""
        heappush(self._queue, (self.now + delay, next(self._order), label, callback))

    def next(self) -> str | None:
        """结算队首回调，并返回其标签；队列为空时返回 ``None``。"""
        if not self._queue:
            return None
        self.now, _, label, callback = heappop(self._queue)
        if (delay := callback()) is not None:
            self.schedule(delay, label, callback)
        return label

    def advance_until(self, finished: Callable[[], bool]) -> None:
        """持续结算时间线，直到条件满足或队列耗尽。"""
        while self._queue and not finished():
            self.next()

    @property
    def entries(self) -> tuple[tuple[Number, int, str, TimelineCallback], ...]:
        """返回不改变队列的有序快照。"""
        return tuple(sorted(self._queue))


class ActionRuntime:
    """人物、异常和反应共用的嵌套行动结算入口。"""

    def __init__(
        self,
        timeline: Timeline,
        env: RuleEnvironment,
        rules: RuleSystem,
        kotodama: KotodamaSystem | None = None,
        *,
        event_parent: Entity | None = None,
        reactors: Callable[[Intent, Mapping[str, Any]], Iterable[Intent]] = lambda _intent, _context: (),
        witnesses: Callable[[Entity, Mapping[str, Any]], Iterable[Soul]] = lambda _event, _context: (),
    ) -> None:
        """组合时间线、规则、言灵以及游戏层提供的反应与见证者查询。"""
        self.timeline = timeline
        self.env = env
        self.rules = rules
        self.kotodama = kotodama
        self.event_parent = event_parent
        self.reactors = reactors
        self.witnesses = witnesses
        self.patches: list[Entity] = []
        self.events: list[Entity] = []

    def review(
        self, intent: Intent, context: Mapping[str, Any] | None = None
    ) -> tuple[bool, Decision]:
        """同时执行主观规则评价与客观可执行性复核。"""
        context = context or {}
        decision = self.rules.evaluate(
            intent.mind, intent.effect, intent.mind.cognitive_environment(self.env)
        )
        bound = intent.actor in intent.mind.characters
        objective = intent.validate() and self.rules.patch_allows(
            intent.effect, self.patches, self.env, context
        )
        allowed = bound and objective and (
            self.kotodama is None or self.kotodama.allows(intent)
        )
        return allowed, decision

    def resolve(
        self, intent: Intent, context: Mapping[str, Any] | None = None
    ) -> bool:
        """结算一个完整行动，并依次处理反应、事件、契约和观察。"""
        context = {"phase": "main", **(context or {})}
        allowed, _ = self.review(intent, context)
        if not allowed:
            return False
        for reaction in self.reactors(intent, context):
            self.resolve(
                reaction,
                {**context, "phase": "reaction", "parent_intent": intent},
            )
        allowed, _ = self.review(intent, context)
        if not allowed:
            return False
        outcome = intent.resolve()
        event = Event(
            intent.label or intent.effect.op,
            intent.actor,
            intent.effect,
            self.event_parent,
            outcome,
            self.timeline.now,
            context.get("cause") or context.get("parent_intent"),
            {str(context.get("phase", "main")).title(): 1},
        )
        event.relate("Mind", intent.mind)
        self.events.append(event)
        if self.kotodama is not None:
            self.kotodama.observe(intent)
            self.kotodama.trigger()
        for soul in self.witnesses(event, context):
            soul.observe(E("event", event), source=event)
            event.relate("ObservedBy", soul)
        return True

    def submit(
        self,
        intent: Intent,
        *,
        delay: Number | None = None,
        context: Mapping[str, Any] | None = None,
        on_review: Callable[[bool, Decision], Any] | None = None,
        on_complete: Callable[[bool], Any] | None = None,
        wait: bool = True,
    ) -> bool:
        """复核后把行动加入时间线；可选择同步推进到结算完成。"""
        allowed, decision = self.review(intent, context)
        if on_review is not None:
            on_review(allowed, decision)
        if not allowed:
            return False
        done = False

        def settle() -> None:
            nonlocal done
            result = self.resolve(intent, context)
            done = True
            if on_complete is not None:
                on_complete(result)

        self.timeline.schedule(
            intent.motion_cost if delay is None else delay,
            intent.label or intent.effect.op,
            lambda: (settle(), None)[1],
        )
        if wait:
            self.timeline.advance_until(lambda: done)
        return True

    def activate(
        self,
        actor: Entity,
        soul: Soul,
        *,
        delay: Number = 0,
        context: Mapping[str, Any] | None = None,
        utility: Callable[[Intent], Number] = lambda _intent: 0,
        chooser: Callable[[tuple[Decision, ...]], Intent] | None = None,
    ) -> None:
        """注册循环行动槽；行动结算后以本次 motion_cost 重登记。"""
        context = dict(context or {})

        def take_turn() -> Number | None:
            action = soul.step(
                actor,
                self.rules,
                self.env,
                context,
                utility,
                (lambda item: self.kotodama is None or self.kotodama.allows(item)),
                chooser,
            )
            if action is None:
                return context.get("idle_cost")
            self.resolve(action, context)
            return action.motion_cost

        self.timeline.schedule(delay, f"行动槽：{actor.name}", take_turn)


Engine = ActionRuntime
