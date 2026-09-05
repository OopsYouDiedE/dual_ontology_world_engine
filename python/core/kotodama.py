"""言灵、仪式配方、真实代价与持续因果约束。

言灵定义、Rite 实例和 Covenant 都是普通 Entity。系统只读取调用者明确
投入的对象，先核验槽位、目标关系、权限、局部 Aspect 和步骤，再支付
实际代价。未知 Effect 不会被当成事实悄悄写入世界。
"""

from __future__ import annotations

from collections.abc import Callable, Iterable, Mapping
from numbers import Real
from typing import Any

from .entity_system import ASPECTS, Entity
from .soul import (
    Directive,
    E,
    Effect,
    Expr,
    F,
    Intent,
    Rule,
    RuleEnvironment,
    RuleSystem,
    Soul,
)


def Recipe(
    name: str,
    *,
    slots: Mapping[str, Mapping[str, Any]] | None = None,
    thresholds: Mapping[str, int] | None = None,
    requirements: Iterable[Expr | Callable[[Mapping[str, Any]], bool]] = (),
    costs: Iterable[Effect] = (),
    effects: Iterable[Effect] = (),
    triggers: Iterable[Rule] = (),
    constraints: Iterable[Rule] = (),
    obligations: Iterable[Rule] = (),
    breach: Iterable[Effect] = (),
    exceptions: Iterable[Expr] = (),
    steps: Iterable[Mapping[str, Any]] = (),
    parent: Entity | None = None,
    tags: Mapping[str, int] | None = None,
    **properties: Any,
) -> Entity:
    """创建通用配方；仪式、研究、制作和言灵共用同一签名形式。"""
    recipe = Entity(
        name,
        parent,
        {"Recipe": 1, **(tags or {})},
        properties={
            "slots": dict(slots or {}),
            "thresholds": dict(thresholds or {}),
            "steps": tuple(steps),
            **properties,
        },
    )
    for value in requirements:
        recipe.attach("Requirement", value)
    for value in costs:
        recipe.attach("Cost", value)
    for value in effects:
        recipe.attach("Effect", value)
    for value in triggers:
        recipe.attach("Trigger", value)
    for value in constraints:
        recipe.attach("HardConstraint", value)
    for value in obligations:
        recipe.attach("Obligation", value)
    for value in breach:
        recipe.attach("Breach", value)
    for value in exceptions:
        recipe.attach("Exception", value)
    return recipe


def WordSpirit(
    activation: Iterable[Rule] = (),
    constraints: Iterable[Rule] = (),
    cost: Effect | None = None,
    effects: Iterable[Effect] = (),
    *,
    name: str = "未命名言灵",
    slots: Mapping[str, Mapping[str, Any]] | None = None,
    thresholds: Mapping[str, int] | None = None,
    requirements: Iterable[Expr | Callable[[Mapping[str, Any]], bool]] = (),
    tags: Mapping[str, int] | None = None,
) -> Entity:
    """旧名称的实体工厂；返回真正的 Incantation Recipe Entity。"""
    return Recipe(
        name,
        slots=slots,
        thresholds=thresholds,
        requirements=requirements,
        costs=() if cost is None or cost.op == "none" else (cost,),
        effects=effects,
        triggers=activation,
        constraints=constraints,
        tags={"Incantation": 1, "Kotodama": 1, **(tags or {})},
    )


def aggregate_aspects(
    inputs: Iterable[Entity],
    target: Entity | None = None,
    context: Mapping[str, Any] | None = None,
) -> dict[str, int]:
    """跨全部明确投入按贡献来源去重，防止共享材料或关系重复供能。"""
    contributions: dict[tuple[int, str, str], int] = {}
    for entity in dict.fromkeys(inputs):
        for source, rule, aspect, strength in entity.aspect_contributions(
            target, context
        ):
            token = (id(source), rule, aspect)
            contributions[token] = max(strength, contributions.get(token, 0))
    totals: dict[str, int] = {}
    for (_, _, aspect), strength in contributions.items():
        totals[aspect] = totals.get(aspect, 0) + strength
    return totals


class KotodamaSystem:
    """统一核验简单言灵、持续 Rite 和未来 Covenant。"""

    def __init__(self, env: RuleEnvironment, rules: RuleSystem) -> None:
        """绑定规则环境并注册核心 Effect 处理器。"""
        self.env = env
        self.rules = rules
        self.handlers: dict[
            str,
            tuple[
                Callable[[Effect], bool],
                Callable[[Effect], Any],
                str,
                Mapping[str, int],
            ],
        ] = {}
        self.rites: list[Entity] = []
        self.contracts: list[Entity] = []
        self.constraint_checks = 0
        self.blocked_intents = 0
        self.last_failure = ""
        self._trigger_stack: list[Entity] = []
        self._register_core_effects()

    def register(
        self,
        operation: str,
        validate: Callable[[Effect], bool],
        apply: Callable[[Effect], Any],
        *,
        layer: str = "semantic",
        minimum: Mapping[str, int] | None = None,
    ) -> None:
        """登记唯一合法的 Effect 作用路径及其本体层和最低门槛。"""
        self.handlers[operation.casefold()] = (
            validate,
            apply,
            layer,
            dict(minimum or {}),
        )

    def _register_core_effects(self) -> None:
        """登记核心支持的语义与物理效果；未知操作始终拒绝。"""
        self.register(
            "assert",
            lambda effect: len(effect.args) == 1 and isinstance(effect.args[0], Expr),
            lambda effect: (self.env.assert_fact(effect.args[0]), ())[1],
        )
        self.register(
            "retract",
            lambda effect: len(effect.args) == 1 and isinstance(effect.args[0], Expr),
            lambda effect: (self.env.retract(effect.args[0]), ())[1],
        )
        self.register("change", self._validate_change, self._apply_change)
        self.register("spend", self._validate_spend, self._apply_spend)
        self.register("move", self._validate_move, self._apply_move, layer="physical")
        self.register(
            "remove", self._validate_remove, self._apply_remove, layer="physical"
        )
        self.register(
            "consume", self._validate_consume, self._apply_consume, layer="physical"
        )
        self.register("relate", self._validate_relate, self._apply_relate)
        self.register("detach_relation", self._validate_detach, self._apply_detach)

    @staticmethod
    def _validate_change(effect: Effect) -> bool:
        """确认 Tag 数值变更不会产生负强度。"""
        return (
            len(effect.args) == 3
            and isinstance(effect.args[0], Entity)
            and isinstance(effect.args[1], str)
            and isinstance(effect.args[2], Real)
            and effect.args[0].tag(effect.args[1]) + effect.args[2] >= 0
        )

    @staticmethod
    def _apply_change(effect: Effect) -> tuple[Entity, ...]:
        """应用已验证的 Tag 增减，并返回受影响对象。"""
        subject, tag, amount = effect.args
        subject.change(tag, amount)
        return (subject,)

    @staticmethod
    def _validate_spend(effect: Effect) -> bool:
        """确认支付量为正，且支付后不会低于保留额度。"""
        if not (
            3 <= len(effect.args) <= 4
            and isinstance(effect.args[0], Entity)
            and isinstance(effect.args[1], str)
            and isinstance(effect.args[2], Real)
            and effect.args[2] > 0
        ):
            return False
        subject, tag, amount = effect.args[:3]
        reserve = effect.args[3] if len(effect.args) == 4 else 0
        return isinstance(reserve, Real) and subject.tag(tag) - amount >= reserve

    @staticmethod
    def _apply_spend(effect: Effect) -> tuple[Entity, ...] | None:
        """实际扣除同质资源；校验失败时不产生部分修改。"""
        if not KotodamaSystem._validate_spend(effect):
            return None
        subject, tag, amount = effect.args[:3]
        subject.change(tag, -amount)
        return (subject,)

    @staticmethod
    def _validate_move(effect: Effect) -> bool:
        """确认移动效果提供了实体和合法的新父节点。"""
        return (
            2 <= len(effect.args) <= 3
            and isinstance(effect.args[0], Entity)
            and (effect.args[1] is None or isinstance(effect.args[1], Entity))
        )

    @staticmethod
    def _apply_move(effect: Effect) -> tuple[Entity, ...]:
        """通过世界树重挂载完成移动。"""
        entity, parent = effect.args[:2]
        entity.reparent(parent, effect.args[2] if len(effect.args) == 3 else 0)
        return (entity,)

    @staticmethod
    def _validate_remove(effect: Effect) -> bool:
        """只允许移除当前仍位于世界树中的 Entity。"""
        return (
            len(effect.args) == 1
            and isinstance(effect.args[0], Entity)
            and effect.args[0].parent is not None
        )

    @staticmethod
    def _apply_remove(effect: Effect) -> tuple[Entity, ...] | None:
        """把对象从世界树摘除；对象引用仍可用于事件审计。"""
        if not KotodamaSystem._validate_remove(effect):
            return None
        entity = effect.args[0]
        entity.reparent(None)
        return (entity,)

    @staticmethod
    def _validate_consume(effect: Effect) -> bool:
        """确认具象资源存在且尚未登记到其他支付关系。"""
        return (
            len(effect.args) == 1
            and isinstance(effect.args[0], Entity)
            and effect.args[0].parent is not None
            and not effect.args[0].entries("PaidFor")
        )

    @staticmethod
    def _apply_consume(effect: Effect) -> tuple[Entity, ...] | None:
        """消耗具象资源，将其从当前容器或场景移除。"""
        if not KotodamaSystem._validate_consume(effect):
            return None
        entity = effect.args[0]
        entity.reparent(None)
        return (entity,)

    @staticmethod
    def _validate_relate(effect: Effect) -> bool:
        """确认关系效果至少包含来源、关系名和目标。"""
        return (
            3 <= len(effect.args) <= 4
            and isinstance(effect.args[0], Entity)
            and isinstance(effect.args[1], str)
        )

    @staticmethod
    def _apply_relate(effect: Effect) -> tuple[Entity, ...]:
        """在来源对象上附加指向目标的关系 Facet。"""
        source, kind, target = effect.args[:3]
        source.relate(kind, target, effect.args[3] if len(effect.args) == 4 else 1)
        return (source,)

    @staticmethod
    def _validate_detach(effect: Effect) -> bool:
        """确认关系移除效果包含来源对象和关系名。"""
        return (
            2 <= len(effect.args) <= 3
            and isinstance(effect.args[0], Entity)
            and isinstance(effect.args[1], str)
        )

    @staticmethod
    def _apply_detach(effect: Effect) -> tuple[Entity, ...]:
        """移除指定类型及可选目标的关系 Facet。"""
        source, kind = effect.args[:2]
        source.detach(kind, effect.args[2] if len(effect.args) == 3 else None)
        return (source,)

    def materialize(self, effect: Effect, bindings: Mapping[str, Any]) -> Effect:
        """用仪式槽位绑定替换效果参数中的名称。"""
        return self.env.materialize(effect, bindings)  # type: ignore[return-value]

    def materialize_rule(self, rule: Rule, bindings: Mapping[str, Any]) -> Rule:
        """解析规则条件和全部指令中的槽位名称。"""
        return Rule(
            self.env.materialize(rule.condition, bindings),  # type: ignore[arg-type]
            tuple(
                Directive(item.mode, self.materialize(item.effect, bindings))
                for item in rule.directives
            ),
            rule.source,
        )

    def validate_effect(self, effect: Effect, *, cost: bool = False) -> bool:
        """检查效果是否有注册处理器且参数结构有效。"""
        if effect.op == "sequence":
            return all(
                self.validate_effect(item, cost=cost)
                for item in effect.args
                if isinstance(item, Effect)
            )
        if effect.op == "when" and len(effect.args) == 2:
            condition, nested = effect.args
            return (
                isinstance(condition, Expr)
                and isinstance(nested, Effect)
                and (
                    not self.env.evaluate(condition)
                    or self.validate_effect(nested, cost=cost)
                )
            )
        return effect.op.casefold() in self.handlers and self.handlers[
            effect.op.casefold()
        ][0](effect)

    def apply(self, effect: Effect) -> tuple[Entity, ...] | None:
        """执行已登记效果；None 表示失败，空 tuple 表示成功但无实体差分。"""
        if effect.op == "sequence":
            affected: list[Entity] = []
            for item in effect.args:
                if not isinstance(item, Effect):
                    continue
                result = self.apply(item)
                if result is None:
                    return None
                affected.extend(result)
            return tuple(dict.fromkeys(affected))
        if effect.op == "when" and len(effect.args) == 2:
            condition, nested = effect.args
            if not isinstance(condition, Expr) or not isinstance(nested, Effect):
                return None
            return self.apply(nested) if self.env.evaluate(condition) else ()
        handler = self.handlers.get(effect.op.casefold())
        if handler is None or not handler[0](effect):
            return None
        result = handler[1](effect)
        if (
            result is False
            or result is None
            and effect.op.casefold() not in {"assert", "retract"}
        ):
            return None
        if isinstance(result, Entity):
            return (result,)
        if isinstance(result, Iterable) and not isinstance(result, (str, bytes)):
            return tuple(item for item in result if isinstance(item, Entity))
        return ()

    @staticmethod
    def _entities(
        inputs: Mapping[str, Entity | Iterable[Entity]],
    ) -> tuple[Entity, ...]:
        """展平投入槽位，并按对象身份保持首次出现顺序去重。"""
        found: list[Entity] = []
        for value in inputs.values():
            if isinstance(value, Entity):
                found.append(value)
            else:
                found.extend(item for item in value if isinstance(item, Entity))
        return tuple(dict.fromkeys(found))

    def _check_slot(
        self,
        role: str,
        spec: Mapping[str, Any],
        value: Entity | Iterable[Entity] | None,
        bindings: Mapping[str, Any],
    ) -> bool:
        """检查单个投入槽位的数量、Tag、持有者和目标关系要求。"""
        entities = (value,) if isinstance(value, Entity) else tuple(value or ())
        if not entities:
            return bool(spec.get("optional"))
        if not spec.get("many") and len(entities) != 1:
            return False
        if any(
            entity.tag(tag) < amount
            for entity in entities
            for tag, amount in spec.get("tags", {}).items()
        ):
            return False
        holder = (
            bindings.get(spec.get("holder", "holder"))
            if spec.get("held", False)
            else None
        )
        if holder is not None and any(
            entity.parent is not holder for entity in entities
        ):
            return False
        relation = spec.get("relation")
        target = bindings.get(spec.get("target", "target"))
        return (
            not relation
            or target is not None
            and all(entity.related(relation, target) for entity in entities)
        )

    def signature(
        self,
        recipe: Entity,
        speaker: Soul,
        inputs: Mapping[str, Entity | Iterable[Entity]],
        bindings: Mapping[str, Any],
    ) -> dict[str, int]:
        """汇总明确投入与施术者为本次仪式提供的 Aspect。"""
        entities = [*self._entities(inputs)]
        if recipe.get("include_speaker", True):
            entities.append(speaker)
        return aggregate_aspects(entities, bindings.get("target"), bindings)

    def check(
        self,
        recipe: Entity,
        speaker: Soul,
        inputs: Mapping[str, Entity | Iterable[Entity]],
        *,
        target: Entity | None = None,
        context: Mapping[str, Any] | None = None,
    ) -> tuple[str, ...]:
        """返回配方当前未满足的槽位、门槛、条件和寻址问题。"""
        context = dict(context or {})
        bindings = {**context, **inputs, "speaker": speaker, "target": target}
        missing: list[str] = []
        for role, spec in recipe.get("slots", {}).items():
            if not self._check_slot(role, spec, inputs.get(role), bindings):
                missing.append(f"槽位:{role}")
        values = self.signature(recipe, speaker, inputs, bindings)
        required = dict(recipe.get("thresholds", {}))
        for facet in recipe.entries("Effect"):
            for effect in (
                facet.value.args
                if isinstance(facet.value, Effect) and facet.value.op == "sequence"
                else (facet.value,)
            ):
                if isinstance(effect, Effect) and (
                    handler := self.handlers.get(effect.op.casefold())
                ):
                    for key, amount in handler[3].items():
                        required[key] = max(amount, required.get(key, 0))
        for key, amount in required.items():
            if key in ASPECTS:
                actual = values.get(key, 0)
            else:
                actual = sum(entity.tag(key) for entity in self._entities(inputs))
            if actual < amount:
                missing.append(f"门槛:{key} {actual}/{amount}")
        for facet in recipe.entries("Requirement"):
            requirement = facet.value
            valid = (
                requirement(bindings)
                if callable(requirement)
                else self.env.evaluate(
                    self.env.materialize(requirement, bindings)  # type: ignore[arg-type]
                )
            )
            if not valid:
                missing.append("条件")
        ambiguity = sum(
            item.tag("Ambiguity") + item.tag("Tainted")
            for item in self._entities(inputs)
        )
        purification = sum(item.tag("Purification") for item in self._entities(inputs))
        if recipe.get("unique_target", target is not None) and target is None:
            missing.append("目标")
        if ambiguity > purification + recipe.get("ambiguity_tolerance", 0):
            missing.append("歧义或污染")
        return tuple(missing)

    def target_candidates(
        self,
        recipe: Entity,
        inputs: Mapping[str, Entity | Iterable[Entity]],
    ) -> tuple[Entity, ...]:
        """只从配方声明的 Correspondence 关系解析候选目标。"""
        groups: list[set[Entity]] = []
        for role, spec in recipe.get("slots", {}).items():
            relation = spec.get("relation")
            if not relation or role not in inputs:
                continue
            value = inputs[role]
            entities = (value,) if isinstance(value, Entity) else tuple(value)
            for entity in entities:
                groups.append(
                    {
                        facet.value
                        for facet in entity.related(relation)
                        if isinstance(facet.value, Entity)
                    }
                )
        if not groups:
            return ()
        candidates = set.intersection(*groups)
        return tuple(candidates)

    def begin(
        self,
        recipe: Entity,
        speaker: Soul,
        inputs: Mapping[str, Entity | Iterable[Entity]],
        *,
        target: Entity | None = None,
        context: Mapping[str, Any] | None = None,
    ) -> Entity | None:
        """核验配方并创建进行中的 Rite；此时不提前支付全部代价。"""
        needs_target = recipe.get(
            "unique_target",
            any(spec.get("relation") for spec in recipe.get("slots", {}).values()),
        )
        if target is None and needs_target:
            candidates = self.target_candidates(recipe, inputs)
            if len(candidates) != 1:
                self.last_failure = "无法唯一寻址" if not candidates else "目标存在歧义"
                return None
            target = candidates[0]
        if missing := self.check(
            recipe, speaker, inputs, target=target, context=context
        ):
            self.last_failure = "、".join(missing)
            return None
        parent = (context or {}).get("world") or speaker.parent
        rite = Entity(
            f"{recipe.name}·施行",
            parent,
            {"Rite": 1, "Active": 1, **({"Order": 1} if recipe.get("steps") else {})},
            properties={
                "recipe": recipe,
                "speaker": speaker,
                "target": target,
                "inputs": dict(inputs),
                "context": dict(context or {}),
                "step": 0,
                "phase": "prepared",
            },
        )
        rite.relate("Speaker", speaker)
        if target is not None:
            rite.relate("Target", target)
        for role, value in inputs.items():
            for entity in (value,) if isinstance(value, Entity) else value:
                rite.attach(role, entity)
                rite.relate("Input", entity)
        rite.set(
            "signature",
            self.signature(
                recipe,
                speaker,
                inputs,
                {**dict(context or {}), **inputs, "speaker": speaker, "target": target},
            ),
        )
        self.rites.append(rite)
        return rite

    def _pay(
        self, owner: Entity, costs: Iterable[Effect], bindings: Mapping[str, Any]
    ) -> bool:
        """先验证整组代价，再逐项核销并建立 Paid/PaidFor 审计关系。"""
        materialized = tuple(self.materialize(effect, bindings) for effect in costs)
        if not all(self.validate_effect(effect, cost=True) for effect in materialized):
            self.last_failure = "代价不可支付"
            return False
        for effect in materialized:
            affected = self.apply(effect)
            if affected is None:
                self.last_failure = "代价支付中断"
                return False
            for entity in affected:
                owner.attach("Paid", effect, source=entity)
                entity.relate("PaidFor", owner)
        return True

    def advance(self, rite: Entity) -> Entity | None:
        """推进 Rite 的一个步骤，在步骤边界支付代价并检查条件。"""
        recipe = rite.get("recipe")
        inputs = rite.get("inputs")
        context = rite.get("context")
        bindings = {
            **context,
            **inputs,
            "speaker": rite.get("speaker"),
            "target": rite.get("target"),
            "rite": rite,
        }
        steps = recipe.get("steps", ())
        index = rite.get("step", 0)
        if index < len(steps):
            step = steps[index]
            requirements = step.get("requirements", ())
            if any(
                not (
                    item(bindings)
                    if callable(item)
                    else self.env.evaluate(self.env.materialize(item, bindings))
                )
                for item in requirements
            ):
                self.last_failure = f"步骤:{step.get('name', index)}"
                return None
            if not self._pay(rite, step.get("costs", ()), bindings):
                return None
            for effect in step.get("effects", ()):
                if self.apply(self.materialize(effect, bindings)) is None:
                    rite.set("phase", "interrupted")
                    return None
            rite.attach("CompletedStep", step.get("name", index), source=recipe)
            rite.set("step", index + 1)
            rite.set("phase", "prepared" if index + 1 < len(steps) else "resolving")
            if index + 1 < len(steps):
                return rite
        return self._complete(rite, bindings)

    def _complete(self, rite: Entity, bindings: Mapping[str, Any]) -> Entity | None:
        """完成 Rite：即时配方应用效果，持续配方生成 Covenant。"""
        recipe = rite.get("recipe")
        if not self._pay(
            rite, (item.value for item in recipe.entries("Cost")), bindings
        ):
            rite.set("phase", "interrupted")
            return None
        persistent = bool(
            recipe.has("Covenant")
            or recipe.entries("Trigger")
            or recipe.entries("HardConstraint")
            or recipe.entries("Obligation")
        )
        if persistent:
            contract = Entity(
                recipe.name,
                rite.parent,
                {
                    "Covenant": 1,
                    "Active": 1,
                    **{
                        tag: value
                        for tag, value in recipe.tags.items()
                        if tag
                        in {
                            "Promise",
                            "Oath",
                            "Geas",
                            "Curse",
                            "Blessing",
                            "DyingWord",
                            "Contingency",
                            "Supernatural",
                        }
                    },
                },
                properties={
                    "recipe": recipe,
                    "speaker": rite.get("speaker"),
                    "target": rite.get("target"),
                    "bindings": dict(bindings),
                    "active": True,
                    "fired": False,
                },
            )
            for key in (
                "Trigger",
                "HardConstraint",
                "Obligation",
                "Breach",
                "Exception",
                "Effect",
            ):
                for facet in recipe.entries(key):
                    value = (
                        self.materialize_rule(facet.value, bindings)
                        if isinstance(facet.value, Rule)
                        else self.env.materialize(facet.value, bindings)
                        if isinstance(facet.value, (Expr, Effect))
                        else facet.value
                    )
                    contract.attach(key, value, facet.strength, source=recipe)
            for paid in rite.entries("Paid"):
                contract.attach("Paid", paid.value, paid.strength, source=paid.source)
                if isinstance(paid.source, Entity):
                    paid.source.relate("PaidFor", contract)
            self.contracts.append(contract)
            result = contract
        else:
            for facet in recipe.entries("Effect"):
                if self.apply(self.materialize(facet.value, bindings)) is None:
                    rite.set("phase", "interrupted")
                    return None
            result = rite
        rite.tags.pop("Active", None)
        rite.change("Completed", 1)
        rite.set("phase", "completed")
        return result

    def invoke(
        self,
        recipe: Entity,
        speaker: Soul,
        inputs: Mapping[str, Entity | Iterable[Entity]],
        *,
        target: Entity | None = None,
        context: Mapping[str, Any] | None = None,
    ) -> Entity | None:
        """创建 Rite 并立即推进一次，适用于简单调用入口。"""
        rite = self.begin(recipe, speaker, inputs, target=target, context=context)
        return self.advance(rite) if rite is not None else None

    def _excepted(self, contract: Entity) -> bool:
        """判断契约是否因任一当前成立的 Exception 而暂不生效。"""
        return any(
            isinstance(item.value, Expr) and self.env.evaluate(item.value)
            for item in contract.entries("Exception")
        )

    def allows(self, intent: Intent | Effect) -> bool:
        """检查所有有效 Covenant 的硬约束是否允许一次行动。"""
        effect = intent.effect if isinstance(intent, Intent) else intent
        self.constraint_checks += 1
        allowed = all(
            self.rules.objective_allows(
                effect,
                (item.value for item in contract.entries("HardConstraint")),
                self.env,
            )
            for contract in self.contracts
            if contract.get("active", True)
            and contract.has("Active")
            and not self._excepted(contract)
        )
        self.blocked_intents += int(not allowed)
        return allowed

    def observe(self, intent: Intent | Effect) -> tuple[Entity, ...]:
        """行动发生后结算可违背义务；Promise/Oath 默认不硬阻止。"""
        effect = intent.effect if isinstance(intent, Intent) else intent
        breached: list[Entity] = []
        for contract in self.contracts:
            if not contract.has("Active") or self._excepted(contract):
                continue
            obligations = tuple(item.value for item in contract.entries("Obligation"))
            if obligations and all(
                self.rules.objective_complies(effect, rule, self.env)
                for rule in obligations
            ):
                continue
            if not obligations:
                continue
            contract.change("Breached", 1)
            breached.append(contract)
            for consequence in contract.entries("Breach"):
                self.apply(consequence.value)
        return tuple(breached)

    def trigger(self) -> tuple[Entity, ...]:
        """执行满足激活规则的 Covenant，并防止同一契约递归重入。"""
        fired: list[Entity] = []
        for contract in tuple(self.contracts):
            if (
                not contract.has("Active")
                or contract.has("Firing")
                or contract.get("fired", False)
                or contract in self._trigger_stack
                or self._excepted(contract)
            ):
                continue
            triggers = tuple(item.value for item in contract.entries("Trigger"))
            if triggers and not all(
                self.rules.activation_satisfied(rule, self.env) for rule in triggers
            ):
                continue
            contract.change("Firing", 1)
            self._trigger_stack.append(contract)
            succeeded = True
            for item in contract.entries("Effect"):
                if self.apply(item.value) is None:
                    succeeded = False
                    break
            self._trigger_stack.pop()
            contract.tags.pop("Firing", None)
            if not succeeded:
                continue
            contract.change("Fired", 1)
            contract.set("fired", True)
            if not contract.entries("HardConstraint") and not contract.entries(
                "Obligation"
            ):
                contract.tags.pop("Active", None)
                contract.set("active", False)
            fired.append(contract)
        return tuple(fired)

    def revoke(self, contract: Entity) -> None:
        """停用并标记指定 Covenant 已撤销。"""
        contract.tags.pop("Active", None)
        contract.change("Revoked", 1)
        contract.set("active", False)


__all__ = [
    "Recipe",
    "WordSpirit",
    "aggregate_aspects",
    "KotodamaSystem",
]
