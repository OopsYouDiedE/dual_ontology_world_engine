"""Soul、完整行动候选、认知规则与客观规则实例。

规则 AST 是可复用的值；发布物、个体信念、客观规则实例和事件是普通
Entity。普通规则只评价 Soul 已经能够构造的完整 Intent，不创造能力，也
不强迫主体。只有 RulePatch、言灵或异常等客观执行路径能够阻止或改变现实。
"""

from __future__ import annotations

from collections.abc import Callable, Iterable, Iterator, Mapping
from dataclasses import dataclass, field, replace
from enum import Enum, auto
from numbers import Real
from typing import Any, TypeAlias

from .entity_system import Entity, Facet, Knowledge


Number: TypeAlias = int | float
Context: TypeAlias = Mapping[str, Any]


@dataclass(frozen=True, slots=True)
class Expr:
    """可匹配、可求值的事实表达式。"""

    op: str
    args: tuple[Any, ...] = ()


@dataclass(frozen=True, slots=True)
class Effect:
    """等待已注册执行路径解释的世界效果。"""

    op: str
    args: tuple[Any, ...] = ()


def E(op: str, *args: Any) -> Expr:
    """以简写形式构造事实表达式。"""
    return Expr(op, args)


def F(op: str, *args: Any) -> Effect:
    """以简写形式构造待执行效果。"""
    return Effect(op, args)


class Mode(Enum):
    """规则指令对匹配行动施加的规范模式。"""
    REQUIRE = auto()
    FORBID = auto()
    PERMIT = auto()
    PREFER = auto()


@dataclass(frozen=True, slots=True)
class Directive:
    """一条规范模式与其目标效果。"""
    mode: Mode
    effect: Effect


@dataclass(frozen=True, slots=True)
class Rule:
    """仅在 condition 成立时参与评价的一组指令。"""
    condition: Expr
    directives: tuple[Directive, ...]
    source: str = ""


class CognitiveStage(Enum):
    """Soul 对规则发布物的认知立场。"""
    RECEIVED = auto()
    UNDERSTOOD = auto()
    ACCEPTED = auto()
    REJECTED = auto()


def RulePublication(
    rule: Rule,
    issuer: Entity,
    guarantee_tag: str = "Guarantee",
    carrier: Entity | None = None,
    scope: Entity | None = None,
    version: object | None = None,
) -> Entity:
    """建立一份真假未定的规则发布物。

    发行者、载体和作用域使用对象引用。Truth/Falsehood 属于世界审计信息，
    不由这个工厂推断，也不会自动进入任何 Soul 的认知。
    """
    publication = Entity(
        f"{issuer.name}发布的规则",
        carrier,
        {"RulePublication": 1, "Information": 1},
        properties={
            "rule": rule,
            "issuer": issuer,
            "carrier": carrier,
            "scope": scope,
            "version": version,
            "guarantee_tag": guarantee_tag,
        },
    )
    publication.relate("IssuedBy", issuer)
    if scope is not None:
        publication.relate("Domain", scope)
    return publication


def _always() -> bool:
    return True


@dataclass(frozen=True, slots=True)
class Intent:
    """主体已经能够执行的完整行动候选。"""

    actor: Entity
    mind: Soul
    effect: Effect
    motion_cost: Number
    resolve: Callable[[], Any]
    label: str = ""
    validate: Callable[[], bool] = _always


@dataclass(frozen=True, slots=True)
class Decision:
    """规则系统对候选效果的评价结果及其主要依据。"""
    intent: Effect
    complies: bool
    score: Number
    basis: Entity | None
    reason: str
    action: Intent | None = None
    pressures: tuple[tuple[Number, Entity, Directive, bool], ...] = ()


StepResult: TypeAlias = Intent | Iterable[Intent] | None
Step: TypeAlias = Callable[["Soul", Entity, "RuleEnvironment", Context], StepResult]


class Character(Entity):
    """承担现实行动的角色实体，可与一个或多个 Soul 双向绑定。"""

    def __init__(
        self,
        name: str,
        parent: Entity | None = None,
        tags: Mapping[str, int] | None = None,
        position: int = 0,
    ) -> None:
        super().__init__(name, parent, {"Character": 1, **(tags or {})}, position)

    @property
    def souls(self) -> tuple[Soul, ...]:
        """返回当前与角色绑定的全部 Soul。"""
        return tuple(
            facet.value for facet in self.entries("Soul")
            if isinstance(facet.value, Soul)
        )

    def bind(self, soul: Soul) -> None:
        """建立 Character 与 Soul 的双向对象关系。"""
        if soul not in self.souls:
            self.relate("Soul", soul)
        if self not in soul.characters:
            soul.relate("Character", self)

    def unbind(self, soul: Soul) -> None:
        """移除 Character 与 Soul 两侧的绑定关系。"""
        self.detach("Soul", soul)
        soul.detach("Character", self)


class Soul(Entity):
    """保存观察、信念、决策步骤和历史决策的认知主体。"""

    def __init__(
        self,
        name: str,
        parent: Entity | None = None,
        tags: Mapping[str, int] | None = None,
        position: int = 0,
    ) -> None:
        super().__init__(name, parent, {"Soul": 1, **(tags or {})}, position)

    @property
    def characters(self) -> tuple[Character, ...]:
        """返回该认知主体目前能够驱动的角色。"""
        return tuple(
            facet.value for facet in self.entries("Character")
            if isinstance(facet.value, Character)
        )

    @property
    def observations(self) -> tuple[Expr, ...]:
        """返回仍处于当前认知环境中的临时事实。"""
        return tuple(facet.value for facet in self.entries("Observation"))

    @property
    def beliefs(self) -> tuple[Entity, ...]:
        """返回该 Soul 持有的规则信念实体。"""
        return tuple(child for child in self.children if child.has("RuleBelief"))

    @property
    def decisions(self) -> tuple[Decision, ...]:
        """返回已记录的决策结果。"""
        return tuple(facet.value for facet in self.entries("Decision"))

    def bind(self, character: Character) -> None:
        """委托 Character 建立双向绑定。"""
        character.bind(self)

    def observe(self, fact: Expr, source: object | None = None) -> Facet:
        """加入一条当前态观察；相同表达式不会重复附加。"""
        if found := next(
            (item for item in self.entries("Observation") if item.value == fact), None
        ):
            return found
        return self.attach("Observation", fact, source=source)

    def unobserve(self, fact: Expr) -> tuple[Facet, ...]:
        """结束一个临时当前态观察；历史结果应另存为 Event/Knowledge。"""
        return self.detach("Observation", fact)

    def cognitive_environment(
        self, world: RuleEnvironment | None = None
    ) -> RuleEnvironment:
        """只用该 Soul 已观察或已理解的命题建立规则评价视图。"""
        view = RuleEnvironment()
        if world is not None:
            view.aliases.update(world.aliases)
        for fact in self.observations:
            view.assert_fact(fact)
        for knowledge in self.children:
            if not knowledge.has("Knowledge"):
                continue
            for claim in knowledge.entries("Claim"):
                if isinstance(claim.value, Expr):
                    view.assert_fact(claim.value)
        return view

    def receive(
        self, publication: Entity, confidence: int = 1
    ) -> Entity:
        """接收规则发布物，并建立尚未理解的个体信念。"""
        rule = publication.get("rule")
        if found := next(
            (
                belief for belief in self.beliefs
                if belief.get("publication") is publication
            ),
            None,
        ):
            return found
        belief = Knowledge(
            f"对“{publication.name}”的理解",
            self,
            content=rule,
            subject=publication,
            source=publication,
            tags={"RuleBelief": 1, "Belief": 1, "Received": 1},
        )
        belief.properties.update(
            publication=publication,
            rule=rule,
            issuer=publication.get("issuer"),
            confidence=confidence,
            stance=CognitiveStage.RECEIVED,
            stage="received",
        )
        return belief

    @staticmethod
    def _stance(belief: Entity, stance: CognitiveStage) -> Entity:
        for tag in ("Received", "Understood", "Accepted", "Rejected"):
            belief.tags.pop(tag, None)
        belief.change(stance.name.title(), 1)
        belief.set("stance", stance)
        if stance is CognitiveStage.UNDERSTOOD:
            belief.set("stage", "understood")
        elif stance in (CognitiveStage.ACCEPTED, CognitiveStage.REJECTED):
            # “理解到哪一步”与“接受还是拒绝”是两个维度。
            belief.set("stage", "understood")
        return belief

    def understand(self, rule: Rule) -> Entity:
        """把已接收的指定规则推进到理解阶段。"""
        belief = next(item for item in self.beliefs if item.get("rule") is rule)
        if belief.has("Received"):
            self._stance(belief, CognitiveStage.UNDERSTOOD)
        return belief

    def consider(
        self,
        rule: Rule,
        source: Entity,
        confidence: int = 1,
        guarantee_tag: str = "Guarantee",
    ) -> Entity:
        """依据置信度和发行者保证强度决定接受或拒绝规则。"""
        publication = next(
            (
                child for child in source.children
                if child.has("RulePublication") and child.get("rule") is rule
            ),
            None,
        ) or RulePublication(rule, source, guarantee_tag, source)
        belief = self.receive(publication, confidence)
        self._stance(belief, CognitiveStage.UNDERSTOOD)
        issuer = publication.get("issuer")
        support = confidence + issuer.tag(guarantee_tag) * 2 + issuer.tag("Authority")
        # Stability 影响冲突承受与修订，不把低 Stability 实现成自动相信。
        return self._stance(
            belief,
            CognitiveStage.ACCEPTED if support >= 4 else CognitiveStage.REJECTED,
        )

    def accept(self, rule: Rule) -> Entity:
        """显式接受一条已经持有的规则。"""
        return self._stance(
            next(item for item in self.beliefs if item.get("rule") is rule),
            CognitiveStage.ACCEPTED,
        )

    def reject(self, rule: Rule) -> Entity:
        """显式拒绝一条已经持有的规则。"""
        return self._stance(
            next(item for item in self.beliefs if item.get("rule") is rule),
            CognitiveStage.REJECTED,
        )

    def install_step(
        self,
        step: Step,
        priority: int = 0,
        when: Callable[[Entity, Context], bool] | None = None,
        source: object | None = None,
    ) -> Facet:
        """把决策树的一个条件分支安装为 Step Facet。"""
        return self.attach("Step", step, priority, when, source)

    def uninstall_steps(self, source: object) -> tuple[Facet, ...]:
        """移除同一来源安装的全部决策分支。"""
        return self.detach("Step", source=source)

    def choices(
        self,
        actor: Entity,
        env: RuleEnvironment,
        context: Context | None = None,
    ) -> tuple[Intent, ...]:
        """运行有效 Step，收集主体此刻实际能够执行的完整 Intent。"""
        context = {**(context or {}), "actor": actor, "soul": self, "env": env}
        choices: list[Intent] = []
        for facet in sorted(
            self.entries("Step", context), key=lambda item: item.strength, reverse=True
        ):
            result = facet.value(self, actor, env, context)
            if result is None:
                continue
            if isinstance(result, Intent):
                choices.append(result)
            else:
                choices.extend(result)
        return tuple(dict.fromkeys(choices))

    def assess(
        self,
        candidates: Iterable[Intent],
        rules: RuleSystem,
        world: RuleEnvironment,
        utility: Callable[[Intent], Number] = lambda _: 0,
    ) -> tuple[Decision, ...]:
        """在 Soul 的认知环境中评价候选行动并按分数降序排列。"""
        cognitive = self.cognitive_environment(world)
        assessed = (
            replace(decision, action=action, score=decision.score + utility(action))
            for action in candidates
            for decision in (rules.evaluate(self, action.effect, cognitive),)
        )
        return tuple(sorted(assessed, key=lambda item: item.score, reverse=True))

    def step(
        self,
        actor: Entity,
        rules: RuleSystem,
        world: RuleEnvironment,
        context: Context | None = None,
        utility: Callable[[Intent], Number] = lambda _: 0,
        allows: Callable[[Intent], bool] = lambda _: True,
        chooser: Callable[[tuple[Decision, ...]], Intent] | None = None,
    ) -> Intent | None:
        """生成、筛选并选择一次行动，同时记录对应 Decision。"""
        candidates = tuple(
            action for action in self.choices(actor, world, context)
            if action.validate() and allows(action)
        )
        if not candidates:
            return None
        decisions = self.assess(candidates, rules, world, utility)
        action = chooser(decisions) if chooser else decisions[0].action
        chosen = next(item for item in decisions if item.action is action)
        self.attach("Decision", chosen, source=(context or {}).get("source"))
        return action

    def decide(
        self,
        candidates: Iterable[Effect],
        rules: RuleSystem,
        world: RuleEnvironment,
        utility: Callable[[Effect], Number] = lambda _: 0,
    ) -> Decision:
        """兼容裸 Effect 的只读建议入口；不会产生可执行能力。"""
        return rules.decide(
            self, candidates, self.cognitive_environment(world), utility
        )


class RuleEnvironment:
    """保存可解析别名和当前事实的轻量规则求值环境。"""

    def __init__(self) -> None:
        self.aliases: dict[str, Any] = {}
        self.facts: list[Expr] = []
        self.version = 0

    def clear(self) -> None:
        """清空别名、事实和版本计数。"""
        self.aliases.clear()
        self.facts.clear()
        self.version = 0

    def alias(self, name: str, value: Any) -> None:
        """登记不区分大小写的名称到运行时对象映射。"""
        self.aliases[name.casefold()] = value

    def resolve(
        self, value: Any, bindings: Mapping[str, Any] | None = None
    ) -> Any:
        """优先用局部绑定解析字符串，再查询环境别名。"""
        if not isinstance(value, str):
            return value
        local = {key.casefold(): item for key, item in (bindings or {}).items()}
        return local.get(value.casefold(), self.aliases.get(value.casefold(), value))

    def same(self, left: Any, right: Any) -> bool:
        """按表达式结构、实体身份或普通值比较两个参数。"""
        left, right = self.resolve(left), self.resolve(right)
        if isinstance(left, Expr) and isinstance(right, Expr):
            return self.matches(left, right)
        if isinstance(left, Effect) and isinstance(right, Effect):
            return self.effect_matches(left, right)
        if isinstance(left, str) and isinstance(right, str):
            return left.casefold() == right.casefold()
        return left is right if isinstance(left, Entity) or isinstance(right, Entity) else left == right

    def matches(self, fact: Expr, query: Expr) -> bool:
        """判断事实与查询表达式的操作符和参数是否匹配。"""
        return (
            fact.op.casefold() == query.op.casefold()
            and len(fact.args) == len(query.args)
            and all(self.same(a, b) for a, b in zip(fact.args, query.args))
        )

    def effect_matches(self, pattern: Effect, candidate: Effect) -> bool:
        """判断候选效果是否符合模式；sequence 中任一子效果即可匹配。"""
        if pattern.op == "sequence":
            return any(
                self.effect_matches(item, candidate)
                for item in pattern.args if isinstance(item, Effect)
            )
        return (
            pattern.op.casefold() == candidate.op.casefold()
            and len(pattern.args) == len(candidate.args)
            and all(self.same(a, b) for a, b in zip(pattern.args, candidate.args))
        )

    def assert_fact(self, fact: Expr) -> bool:
        """加入尚不存在的事实，并在成功时递增环境版本。"""
        if any(self.matches(item, fact) for item in self.facts):
            return False
        self.facts.append(fact)
        self.version += 1
        return True

    def retract(self, fact: Expr) -> bool:
        """移除第一条匹配事实，并在成功时递增环境版本。"""
        if found := next((item for item in self.facts if self.matches(item, fact)), None):
            self.facts.remove(found)
            self.version += 1
            return True
        return False

    def evaluate(self, expression: Expr) -> bool:
        """求值内建逻辑、实体状态与环境事实表达式。"""
        match expression:
            case Expr("true", _):
                return True
            case Expr("all", args):
                return all(self.evaluate(item) for item in args if isinstance(item, Expr))
            case Expr("any", args):
                return any(self.evaluate(item) for item in args if isinstance(item, Expr))
            case Expr("not", (Expr() as inner,)):
                return not self.evaluate(inner)
            case Expr("state", (subject, str() as state)) if isinstance(self.resolve(subject), Entity):
                return self.resolve(subject).has(state) or any(self.matches(item, expression) for item in self.facts)
            case Expr("parent", (child, parent)) if isinstance(self.resolve(child), Entity):
                return self.resolve(child).parent is self.resolve(parent)
            case Expr("related", (subject, str() as kind, target)) if isinstance(self.resolve(subject), Entity):
                return bool(self.resolve(subject).related(kind, self.resolve(target)))
            case Expr("tag_at_least", (subject, str() as tag, Real() as amount)) if isinstance(self.resolve(subject), Entity):
                return self.resolve(subject).tag(tag) >= amount
            case _:
                return any(self.matches(item, expression) for item in self.facts)

    def effect_is_fact(self, effect: Effect) -> bool:
        """检查效果描述的状态是否已经在环境中成立。"""
        if effect.op == "sequence":
            return all(self.effect_is_fact(item) for item in effect.args if isinstance(item, Effect))
        return self.evaluate(E(effect.op, *effect.args))

    def assert_effect(self, effect: Effect) -> None:
        """把效果转换为事实；sequence 会递归展开。"""
        if effect.op == "sequence":
            for item in effect.args:
                if isinstance(item, Effect):
                    self.assert_effect(item)
        else:
            self.assert_fact(E(effect.op, *effect.args))

    def materialize(
        self, value: Expr | Effect, bindings: Mapping[str, Any] | None = None
    ) -> Expr | Effect:
        """递归解析表达式或效果中的字符串参数，保留其具体类型。"""
        cls = type(value)
        return cls(
            value.op,
            tuple(
                self.materialize(item, bindings)
                if isinstance(item, (Expr, Effect))
                else self.resolve(item, bindings)
                for item in value.args
            ),
        )


class RuleSystem:
    """评价 Soul 的主观规范，并检查客观 RulePatch 约束。"""

    def _expanded(self, rule: Rule, env: RuleEnvironment) -> Iterator[Directive]:
        for directive in rule.directives:
            match directive.effect:
                case Effect("when", (Expr() as condition, Effect() as nested)):
                    if env.evaluate(condition):
                        yield Directive(directive.mode, nested)
                case _:
                    yield directive

    @staticmethod
    def belief_priority(belief: Entity) -> int:
        """按置信度、保证标记和发行者权威计算信念优先级。"""
        publication = belief.get("publication")
        issuer = belief.get("issuer")
        guarantee = publication.get("guarantee_tag", "Guarantee")
        return (
            belief.get("confidence", 1)
            + issuer.tag(guarantee) * 2
            + issuer.tag("Authority")
        )

    def active(
        self, soul: Soul, env: RuleEnvironment
    ) -> Iterator[tuple[Entity, Directive]]:
        """遍历条件成立的已接受信念及其当前有效指令。"""
        for belief in soul.beliefs:
            rule = belief.get("rule")
            if belief.has("Accepted") and env.evaluate(rule.condition):
                yield from ((belief, item) for item in self._expanded(rule, env))

    def evaluate(
        self, soul: Soul, intent: Effect, env: RuleEnvironment
    ) -> Decision:
        """汇总所有主观规范压力，生成候选效果的评价结果。"""
        pressures: list[tuple[Number, Entity, Directive, bool]] = []
        for belief, directive in self.active(soul, env):
            matched = env.effect_matches(directive.effect, intent)
            priority = self.belief_priority(belief)
            contribution = {
                (Mode.REQUIRE, True): 100 + priority,
                (Mode.REQUIRE, False): -80 - priority,
                (Mode.PREFER, True): 50 + priority,
                (Mode.PERMIT, True): 20 + priority,
                (Mode.FORBID, True): -100 - priority,
            }.get((directive.mode, matched), 0)
            if contribution:
                pressures.append((contribution, belief, directive, matched))
        if not pressures:
            return Decision(intent, True, 0, None, "无相关已接受规则")
        score = sum(item[0] for item in pressures)
        strongest = max(pressures, key=lambda item: abs(item[0]))
        _, belief, directive, matched = strongest
        complies = not any(
            (item.mode is Mode.FORBID and hit)
            or (item.mode is Mode.REQUIRE and not hit)
            for _, _, item, hit in pressures
        )
        issuer = belief.get("issuer")
        reason = (
            f"未满足 REQUIRE：{issuer.name}"
            if directive.mode is Mode.REQUIRE and not matched
            else f"{directive.mode.name}：{issuer.name}"
        )
        return Decision(intent, complies, score, belief, reason, pressures=tuple(pressures))

    def decide(
        self,
        soul: Soul,
        candidates: Iterable[Effect],
        env: RuleEnvironment,
        utility: Callable[[Effect], Number] = lambda _: 0,
    ) -> Decision:
        """把规则分与外部效用相加，选择得分最高的候选效果。"""
        decisions = (
            replace(item, score=item.score + utility(effect))
            for effect in candidates
            for item in (self.evaluate(soul, effect, env),)
        )
        return max(decisions, key=lambda item: item.score)

    def objective_allows(
        self,
        intent: Effect,
        constraints: Iterable[Rule],
        env: RuleEnvironment,
    ) -> bool:
        """检查候选效果是否满足全部客观规则。"""
        return all(
            self.objective_complies(intent, rule, env)
            for rule in constraints
        )

    def objective_complies(
        self, intent: Effect, rule: Rule, env: RuleEnvironment
    ) -> bool:
        """检查单条客观规则是否允许候选效果。"""
        if not env.evaluate(rule.condition):
            return True
        return all(
            not (
                directive.mode is Mode.FORBID
                and env.effect_matches(directive.effect, intent)
            )
            and not (
                directive.mode is Mode.REQUIRE
                and not env.effect_matches(directive.effect, intent)
            )
            for directive in self._expanded(rule, env)
        )

    def activation_satisfied(self, rule: Rule, env: RuleEnvironment) -> bool:
        """检查规则条件及 REQUIRE/FORBID 描述的世界状态是否满足。"""
        if not env.evaluate(rule.condition):
            return False
        return all(
            env.effect_is_fact(item.effect)
            if item.mode is Mode.REQUIRE
            else not env.effect_is_fact(item.effect)
            if item.mode is Mode.FORBID
            else True
            for item in self._expanded(rule, env)
        )

    def patch_allows(
        self,
        intent: Effect,
        patches: Iterable[Entity],
        env: RuleEnvironment,
        context: Context | None = None,
    ) -> bool:
        """检查当前域中所有启用的 RulePatch 是否允许候选效果。"""
        context = context or {}
        for patch in patches:
            if not patch.has("RulePatch") or not patch.get("active", True):
                continue
            scope = patch.get("scope")
            if scope is not None and scope not in context.get("domains", ()):
                continue
            if not self.objective_allows(intent, (patch.get("rule"),), env):
                return False
        return True


def RulePatch(
    name: str,
    rule: Rule,
    parent: Entity | None = None,
    source: Entity | None = None,
    scope: Entity | None = None,
    priority: int = 0,
    tags: Mapping[str, int] | None = None,
) -> Entity:
    """创建会直接约束现实执行路径的客观规则实体。"""
    patch = Entity(
        name,
        parent,
        {"RulePatch": 1, "Truth": 1, **(tags or {})},
        properties={
            "rule": rule,
            "scope": scope,
            "priority": priority,
            "active": True,
        },
    )
    if source is not None:
        patch.relate("Source", source)
    if scope is not None:
        patch.relate("Domain", scope)
    return patch


def Event(
    name: str,
    actor: Entity | None,
    effect: Effect,
    parent: Entity | None = None,
    outcome: object | None = None,
    time: Number = 0,
    cause: object | None = None,
    tags: Mapping[str, int] | None = None,
) -> Entity:
    """创建一次已发生效果的审计事件，并保存行动者与因果引用。"""
    event = Entity(
        name,
        parent,
        {"Event": 1, **(tags or {})},
        properties={
            "actor": actor,
            "effect": effect,
            "outcome": outcome,
            "time": time,
            "cause": cause,
        },
    )
    if actor is not None:
        event.relate("Actor", actor)
    if isinstance(cause, Entity):
        event.relate("CausedBy", cause)
    return event


__all__ = [
    "Number", "Expr", "Effect", "E", "F", "Mode", "Directive", "Rule",
    "CognitiveStage", "RulePublication", "Intent", "Decision", "Step",
    "Character", "Soul", "RuleEnvironment", "RuleSystem", "RulePatch", "Event",
]
