"""实体、物体性质、语义关系、观察与 Aspect 投影。

本模块刻意只保留很少的基础结构：

``Entity``
    唯一的运行时世界对象。人物、门、手稿、知识和情感最终都是 Entity。

``Facet``
    依附在对象上的一条语义。关系、声明、条件名称、观察揭示和额外
    Aspect 都使用同一结构，避免为每种语义创建一个类。

``Passage``
    连接两个场景的结构。它需要和 Entity 一样进入世界树，却不是一个
    可以被拾取、销毁或当作仪式材料使用的实体，所以不继承 Entity。

理解 Entity 时应区分四组数据：

``name``
    调试名称，也是没有条件名称可用时的后备名称。
``tags``
    类别、功能标记和无条件固有 Aspect。
``properties``
    材料、完整度、剂量等只属于对象自身的客观事实。
``facets``
    带目标、来源、强度或条件的语义。

知识只能影响观察结果和主动使用时的估计，不能修改客观 properties、
relations 或 Aspect。真实结算调用 ``Entity.aspects``，界面和 AI 估计调用
``Entity.estimated_aspects``。
"""

from __future__ import annotations

from collections.abc import Callable, Iterable, Mapping
from dataclasses import dataclass
from math import inf
from typing import Any, TypeAlias

Tags: TypeAlias = Mapping[str, int]
Properties: TypeAlias = Mapping[str, Any]
Context: TypeAlias = Mapping[str, Any]
FacetCondition: TypeAlias = Callable[["TreeMember", Context], bool]
AspectContribution: TypeAlias = tuple[object, str, str, int]
AspectProjection: TypeAlias = tuple[tuple[str, int, int], ...]


# 只有这些 Tag 会被当成可叠加的原理强度。其他 Tag 只是分类或功能标记。
ASPECTS: tuple[str, ...] = (
    "Form",
    "Name",
    "Law",
    "Mind",
    "Bond",
    "Threshold",
    "Transmutation",
    "Preservation",
    "Death",
    "Desire",
    "Strife",
    "Vestige",
)


@dataclass(frozen=True, slots=True)
class Facet:
    """依附在对象上的一条轻量语义。

    参数：
        key: 语义类型，例如 ``Owner``、``Claim``、``Reveal`` 或 ``Aspect``。
        value: 语义的值。关系通常直接保存目标 Entity 引用。
        strength: 关系强度、Aspect 强度或条件名称的优先级。
        when: 可选条件，签名为 ``when(owner, context) -> bool``。
        source: 这条语义的来源对象，用于追踪和防止重复计算。

    Facet 不是世界实体，没有 parent 和生命周期。若一段关系本身需要被
    隐藏、转移、损坏或单独到期，应把它提升为普通 Entity。
    """

    key: str
    value: Any
    strength: int = 1
    when: FacetCondition | None = None
    source: object | None = None


# 具体性质到 Aspect 的默认投影。内容层可以增删规则，不必增加物品类。
# 每项为 (Aspect, 乘数, 除数)。
# 属性映射不保存世界状态，只说明“某项具体性质应贡献哪些 Aspect”。
# 例如 sharpness=6 会贡献 Strife=6、Death=2。
PROPERTY_ASPECTS: dict[str, AspectProjection] = {
    "hardness": (("Form", 1, 2), ("Preservation", 1, 4)),
    "durability": (("Preservation", 1, 1), ("Form", 1, 3)),
    "sharpness": (("Strife", 1, 1), ("Death", 1, 3)),
    "permeability": (("Threshold", 1, 1),),
    "reactivity": (("Transmutation", 1, 1),),
    "vitality": (("Preservation", 1, 1),),
    "lethality": (("Death", 1, 1), ("Strife", 1, 2)),
    "recognition": (("Name", 1, 1),),
    "authority": (("Law", 1, 1), ("Name", 1, 2)),
    "cognition": (("Mind", 1, 1),),
    "affinity": (("Bond", 1, 1),),
    "motivation": (("Desire", 1, 1),),
    "conflict": (("Strife", 1, 1),),
    "antiquity": (("Vestige", 1, 1),),
}

# 关系只有在查询目标与 Facet.value 是同一对象时才会产生这些 Aspect。
# 因此“某相片描绘甲”不会在针对乙的仪式中提供 Bond 或 Vestige。
RELATION_ASPECTS: dict[str, dict[str, int]] = {
    "Owner": {"Bond": 1, "Name": 1},
    "Bloodline": {"Bond": 1},
    "Witness": {"Bond": 1, "Law": 1, "Vestige": 1},
    "Depicts": {"Bond": 1, "Vestige": 1},
    "RecordOf": {"Vestige": 1},
    "MadeBy": {"Bond": 1, "Vestige": 1},
    "Harmed": {"Bond": 1, "Strife": 1, "Vestige": 1},
    "IssuedBy": {"Law": 1, "Name": 1},
    "Grants": {"Law": 1},
    "Unlocks": {"Threshold": 1, "Bond": 1},
    "TrueNameOf": {"Name": 3, "Bond": 1},
}

# 情感的第一组是主要天然 Aspect，使用完整强度；第二组使用一半强度。
EMOTION_ASPECTS: dict[str, tuple[tuple[str, ...], tuple[str, ...]]] = {
    "Love": (("Desire", "Bond"), ("Preservation",)),
    "Hatred": (("Desire", "Strife"), ("Death",)),
    "Fear": (("Desire", "Preservation"), ("Threshold",)),
    "Anger": (("Desire", "Strife"), ("Form",)),
    "Joy": (("Desire", "Preservation"), ("Bond",)),
    "Sorrow": (("Death", "Vestige"), ("Bond",)),
    "Hope": (("Desire", "Preservation"), ("Threshold",)),
    "Despair": (("Death", "Desire"), ("Mind",)),
    "Remorse": (("Vestige", "Law"), ("Bond",)),
    "Devotion": (("Bond", "Law"), ("Preservation",)),
    "Obsession": (("Desire", "Preservation"), ("Mind",)),
    "Serenity": (("Mind", "Preservation"), ("Law",)),
}

STAGES: dict[str, int] = {
    "received": 0,
    "observed": 1,
    "understood": 2,
    "accepted": 3,
}


class TreeMember:
    """Entity 与 Passage 共享的世界树接口。

    这个类只复用 Python 代码。系统不会直接创建 TreeMember，也不会把它
    视为一种世界对象。
    """

    def __init__(
        self,
        name: str,
        parent: TreeMember | None = None,
        tags: Tags | None = None,
        position: int = 0,
        properties: Properties | None = None,
        facets: Iterable[Facet] | None = None,
    ) -> None:
        """初始化树成员，并在给出 parent 时立即占用一个子节点槽位。

        Tag 保存整数强度，properties 保存对象自身数据；对象关系和条件语义
        使用 Facet，以保留目标、条件与来源。
        """
        self.name = name
        self.parent: TreeMember | None = None
        self.tags: dict[str, int] = dict(tags or {})
        self.properties: dict[str, Any] = dict(properties or {})
        self.facets: list[Facet] = list(facets or ())
        self.children: dict[TreeMember, int] = {}
        if parent is not None:
            self.reparent(parent, position)

    def reparent(self, parent: TreeMember | None, position: int = 0) -> TreeMember:
        """把对象移动到新的父节点，并自动寻找未占用的整数槽位。"""
        if self.parent is not None:
            self.parent.children.pop(self, None)
        self.parent = parent
        if parent is None:
            return self
        occupied = set(parent.children.values())
        while position in occupied:
            position += 1
        parent.children[self] = position
        return self

    def tag(self, name: str) -> int:
        """读取 Tag；不存在的 Tag 等同于强度 0。"""
        return self.tags.get(name, 0)

    def change(self, name: str, amount: int) -> int:
        """修改 Tag 强度；结果小于等于 0 时直接删除该 Tag。"""
        value = self.tag(name) + amount
        if value > 0:
            self.tags[name] = value
        else:
            self.tags.pop(name, None)
        return max(0, value)

    def has(self, name: str) -> bool:
        """判断指定 Tag 的当前强度是否大于零。"""
        return self.tag(name) > 0

    def get(self, name: str, default: Any = None) -> Any:
        """读取对象自身的具体性质，不读取 Tag 或 Facet。"""
        return self.properties.get(name, default)

    def set(self, name: str, value: Any) -> Any:
        """写入一项具体客观性质。value 可以直接是另一个 Entity。"""
        self.properties[name] = value
        return value

    def attach(
        self,
        key: str,
        value: Any,
        strength: int = 1,
        when: FacetCondition | None = None,
        source: object | None = None,
    ) -> Facet:
        """附加一条 Facet，并返回它以便后续精确移除。"""
        facet = Facet(key, value, strength, when, source)
        self.facets.append(facet)
        return facet

    def detach(
        self, key: str, value: Any = None, source: object | None = None
    ) -> tuple[Facet, ...]:
        """移除匹配的 Facet；省略 value/source 表示不按该字段筛选。"""
        removed = tuple(
            facet
            for facet in self.facets
            if facet.key == key
            and (value is None or facet.value is value or facet.value == value)
            and (source is None or facet.source is source)
        )
        for facet in removed:
            self.facets.remove(facet)
        return removed

    def entries(
        self, key: str | None = None, context: Context | None = None
    ) -> tuple[Facet, ...]:
        """返回在当前上下文中生效的 Facet。"""
        context = context or {}
        return tuple(
            facet
            for facet in self.facets
            if (key is None or facet.key == key)
            and (facet.when is None or facet.when(self, context))
        )

    def relate(
        self,
        kind: str,
        target: object,
        strength: int = 1,
        when: FacetCondition | None = None,
        source: object | None = None,
    ) -> Facet:
        """attach 的关系语义写法；target 始终保存实际对象引用。"""
        return self.attach(kind, target, strength, when, source)

    def related(
        self,
        kind: str,
        target: object | None = None,
        context: Context | None = None,
    ) -> tuple[Facet, ...]:
        """查询指定类型、目标和上下文下成立的关系。"""
        return tuple(
            facet
            for facet in self.entries(kind, context)
            if target is None or facet.value is target
        )

    def designate(
        self,
        text: str,
        priority: int = 1,
        when: FacetCondition | None = None,
        source: object | None = None,
    ) -> Facet:
        """登记条件名称；priority 越高越优先。"""
        return self.attach("Designation", text, priority, when, source)

    def reveal(
        self,
        key: str,
        value: Any,
        priority: int = 1,
        when: FacetCondition | None = None,
        source: object | None = None,
    ) -> Facet:
        """登记观察时可以得到的命题，不会直接修改观察者知识。"""
        return self.attach("Reveal", (key, value), priority, when, source)

    def designation(self, context: Context | None = None) -> str:
        """选择当前条件下优先级最高的名称，找不到时返回 name。"""
        choices = self.entries("Designation", context)
        return (
            max(choices, key=lambda facet: facet.strength).value
            if choices
            else self.name
        )


class Entity(TreeMember):
    """唯一的运行时世界对象。

    不要为“桌子、剑、证件、恐惧”分别增加数据类。它们的差异通常可以
    由 Tag、properties 和 Facet 表达。模块末尾的同名工厂也都返回 Entity。
    """

    def aspect_contributions(
        self, target: object | None = None, context: Context | None = None
    ) -> tuple[AspectContribution, ...]:
        """计算客观 Aspect，并保留每项贡献的来源。

        计算顺序：
        1. 读取对象自身的固有 Aspect Tag。
        2. 继承 material Entity 的 Aspect，并按 integrity 缩放。
        3. 使用 PROPERTY_ASPECTS 投影具体性质。
        4. 读取显式 Aspect Facet。
        5. 读取与 target 匹配的关系型贡献。

        返回项为 ``(source, rule, aspect, strength)``。该方法无副作用，
        不会把临时结果写回 tags。context 中即使存在 observer 也会被移除，
        防止观察者知识污染客观结算。
        """
        context = {**(context or {}), "target": target}
        context.pop("observer", None)
        contributions: list[AspectContribution] = []

        for aspect in ASPECTS:
            if strength := self.tag(aspect):
                contributions.append((self, "intrinsic", aspect, strength))

        # 材料是实际 Entity 引用。这里只继承十二种 Aspect，不继承材料的
        # 分类 Tag，也不把材料原型当成第二份可以再次投入的物质。
        material = self.get("material")
        if isinstance(material, Entity):
            numerator, denominator = 1, 1
            if isinstance(integrity := self.get("integrity"), tuple):
                numerator, denominator = integrity
            for aspect in ASPECTS:
                if strength := material.tag(aspect):
                    amount = strength * numerator // max(1, denominator)
                    if amount > 0:
                        contributions.append((material, "material", aspect, amount))

        # 具体性质只保存一次。它们在查询时投影，不复制成永久 Aspect Tag。
        for name, rules in PROPERTY_ASPECTS.items():
            value = self.get(name, 0)
            if not isinstance(value, (int, float)):
                continue
            for aspect, multiplier, divisor in rules:
                amount = int(value * multiplier // divisor)
                if amount > 0:
                    contributions.append((self, f"property:{name}", aspect, amount))

        influence_now = self.effective_intensity(context.get("time"))
        influence_base = max(1, self.get("intensity", influence_now or 1))
        # Influence 的天然 Aspect 会随当前有效情感强度同步衰减。
        for facet in self.entries(context=context):
            if facet.key == "Aspect" and facet.value in ASPECTS:
                strength = facet.strength
                if self.has("Influence"):
                    strength = strength * influence_now // influence_base
                if strength <= 0:
                    continue
                contributions.append(
                    (facet.source or self, "facet", facet.value, strength)
                )
                continue
            # 关系投影必须有明确目标。没有 target 时不把定向关系当作
            # 可以对任何对象使用的通用力量。
            if target is None or facet.value is not target:
                continue
            for aspect, multiplier in RELATION_ASPECTS.get(facet.key, {}).items():
                contributions.append(
                    (
                        facet.source or facet.value,
                        f"relation:{facet.key}",
                        aspect,
                        facet.strength * multiplier,
                    )
                )

        # 同一来源、同一规则、同一 Aspect 在一次查询中只计算一次，避免
        # 通过别名或重复挂载取得无限强度。
        seen, unique = set(), []
        for source, rule, aspect, strength in contributions:
            token = (id(source), rule, aspect)
            if token not in seen:
                seen.add(token)
                unique.append((source, rule, aspect, strength))
        return tuple(unique)

    def aspects(
        self, target: object | None = None, context: Context | None = None
    ) -> dict[str, int]:
        """返回真实结算使用的客观 Aspect 总值。"""
        totals: dict[str, int] = {}
        for _, _, aspect, strength in self.aspect_contributions(target, context):
            totals[aspect] = totals.get(aspect, 0) + strength
        return totals

    def learn(
        self,
        subject: Entity,
        claims: Iterable[Any] = (),
        source: object | None = None,
        confidence: int = 1,
        stage: str = "observed",
        name: str | None = None,
    ) -> Entity:
        """在当前 Entity 下建立或更新一条 Knowledge Entity。

        通常由 Soul 调用，但核心不硬编码 Soul 类型。subject 是认知所指向
        的真实对象或认知指代对象；claims 可以正确，也可以错误。
        """
        knowledge = next(
            (
                child
                for child in self.children
                if child.has("Knowledge")
                and child.get("subject") is subject
                and child.get("source") is source
            ),
            None,
        )
        if knowledge is None:
            knowledge = Entity(
                name or f"关于{subject.name}的认知",
                self,
                {"Knowledge": 1},
                properties={
                    "subject": subject,
                    "source": source,
                    "confidence": confidence,
                    "stage": stage,
                },
            )
        elif STAGES.get(stage, 0) > STAGES.get(knowledge.get("stage"), 0):
            knowledge.set("stage", stage)
        knowledge.set("confidence", max(confidence, knowledge.get("confidence", 0)))
        existing = [facet.value for facet in knowledge.entries("Claim")]
        for claim in claims:
            if claim not in existing:
                knowledge.attach("Claim", claim, confidence, source=source)
        if stage == "accepted":
            knowledge.tags["Accepted"] = 1
            knowledge.tags.pop("Rejected", None)
        return knowledge

    def knowledge_of(
        self, subject: Entity, stage: str = "observed"
    ) -> tuple[Entity, ...]:
        """返回达到指定认知阶段、且指向 subject 的知识条目。"""
        required = STAGES.get(stage, 0)
        return tuple(
            child
            for child in self.children
            if child.has("Knowledge")
            and child.get("subject") is subject
            and STAGES.get(child.get("stage"), 0) >= required
        )

    def knows(
        self, subject: Entity, claim: Any = None, stage: str = "observed"
    ) -> bool:
        """检查是否知道某对象，以及是否知道关于它的某条 Claim。"""
        entries = self.knowledge_of(subject, stage)
        if claim is None:
            return bool(entries)
        return any(
            any(facet.value == claim for facet in entry.entries("Claim"))
            for entry in entries
        )

    def claims_about(
        self,
        subject: Entity,
        *,
        stage: str = "observed",
        source: object | None = None,
        accepted: bool | None = None,
    ) -> tuple[tuple[Entity, Facet], ...]:
        """查询关于 subject 的声明，同时保留知识载体与来源。

        accepted 为 ``True`` 时只返回已采信知识，为 ``False`` 时只返回
        尚未采信或已拒绝的知识，省略时不按立场筛选。该查询不判断声明
        是否符合客观事实。
        """
        found: list[tuple[Entity, Facet]] = []
        for knowledge in self.knowledge_of(subject, stage):
            if source is not None and knowledge.get("source") is not source:
                continue
            adopted = knowledge.has("Accepted") or knowledge.get("stage") == "accepted"
            if accepted is not None and adopted is not accepted:
                continue
            found.extend((knowledge, claim) for claim in knowledge.entries("Claim"))
        return tuple(found)

    def set_knowledge_stance(self, knowledge: Entity, stance: str) -> Entity:
        """设置普通知识的采信立场，不删除声明或来源历史。

        ``accepted`` 使其中声明进入认知环境；``rejected`` 表示理解但拒绝；
        ``withheld`` 表示理解后暂不判断。其他阶段名称只更新认知深度。
        """
        stance = stance.casefold()
        knowledge.tags.pop("Accepted", None)
        knowledge.tags.pop("Rejected", None)
        if stance == "accepted":
            knowledge.change("Accepted", 1)
            knowledge.set("stage", "accepted")
        elif stance == "rejected":
            knowledge.change("Rejected", 1)
            knowledge.set("stage", "understood")
        elif stance == "withheld":
            knowledge.set("stage", "understood")
        else:
            knowledge.set("stage", stance)
        return knowledge

    def observe(
        self,
        observer: Entity,
        context: Context | None = None,
        learn: bool = True,
    ) -> dict[str, Any]:
        """根据 Reveal 与 Designation 生成观察结果。

        观察只会把满足条件的 Claim 交给 observer。没有 Reveal 的私有
        properties 不会因为观察自动泄露。
        """
        context = {**(context or {}), "observer": observer}
        claims = tuple(facet.value for facet in self.entries("Reveal", context))
        if learn:
            observer.learn(self, claims, source=self)
        return {"subject": self, "name": self.designation(context), "claims": claims}

    def read(self, observer: Entity, context: Context | None = None) -> tuple[Any, ...]:
        """读取 Record 携带的声明和 Knowledge。

        language 若是 Entity，观察者必须至少达到 understood 阶段。阅读
        只改变观察者认知，不会把载体声明写入客观世界。
        """
        language = self.get("language")
        if isinstance(language, Entity) and not observer.knows(
            language, stage="understood"
        ):
            return ()
        claims = [facet.value for facet in self.entries("Claim", context)]
        observer.learn(self, claims, source=self, stage="understood")
        for carried in self.related("Carries", context=context):
            knowledge = carried.value
            if not isinstance(knowledge, Entity):
                continue
            learned = tuple(
                facet.value for facet in knowledge.entries("Claim", context)
            )
            observer.learn(
                knowledge.get("subject") or knowledge,
                learned,
                source=self,
                stage="understood",
                name=knowledge.name,
            )
            claims.extend(learned)
        return tuple(claims)

    def estimated_aspects(
        self,
        observer: Entity,
        target: object | None = None,
        context: Context | None = None,
    ) -> dict[str, int]:
        """返回 observer 根据已有知识能够估计出的 Aspect。

        该结果只供 UI、AI 决策和主动选择使用。真实效果仍调用 aspects。
        """
        known_sources = {
            item.get("subject") for item in observer.children if item.has("Knowledge")
        }
        totals: dict[str, int] = {}
        for source, _, aspect, strength in self.aspect_contributions(target, context):
            if (
                source in known_sources
                or observer.knows(self, ("Aspect", aspect, strength))
                or observer.knows(self, ("Aspect", aspect))
            ):
                totals[aspect] = totals.get(aspect, 0) + strength
        return totals

    def locked(self, context: Context | None = None) -> bool:
        """检查全局锁或当前 from_scene 一侧的锁。"""
        side = (context or {}).get("from_scene")
        return any(
            facet.strength > 0 and (facet.value is None or facet.value is side)
            for facet in self.entries("Lock", context)
        )

    def accessible(self, context: Context | None = None) -> bool:
        """判断容器或门在当前上下文中是否已打开且未上锁。"""
        return (
            not self.has("Openable") or self.get("opened", False)
        ) and not self.locked(context)

    def open(self, context: Context | None = None) -> bool:
        """在当前方向没有有效锁时打开对象。"""
        if self.locked(context):
            return False
        self.set("opened", True)
        return True

    def close(self) -> None:
        """关闭 Openable 对象。"""
        self.set("opened", False)

    def lock(
        self,
        strength: int = 1,
        side: object | None = None,
        source: object | None = None,
    ) -> Facet:
        """添加全局锁或分侧锁；side=None 表示所有方向。"""
        self.close()
        return self.attach("Lock", side, strength, source=source)

    def unlock(
        self, side: object | None = None, source: object | None = None
    ) -> tuple[Facet, ...]:
        """移除指定方向和来源的锁。"""
        return self.detach("Lock", side, source)

    @property
    def items(self) -> tuple[TreeMember, ...]:
        """按槽位顺序返回直接子节点快照，不递归展开后代。"""
        return tuple(
            child
            for child, _ in sorted(self.children.items(), key=lambda item: item[1])
        )

    def put(
        self,
        entity: Entity,
        position: int = 0,
        context: Context | None = None,
    ) -> bool:
        """把实体放入可访问的 Container。"""
        if not self.has("Container") or not self.accessible(context):
            return False
        entity.reparent(self, position)
        return True

    def take(
        self,
        entity: Entity,
        parent: TreeMember,
        position: int = 0,
        context: Context | None = None,
    ) -> bool:
        """从可访问的容器取出实体，并移动到新的父节点。"""
        if not self.accessible(context) or entity.parent is not self:
            return False
        entity.reparent(parent, position)
        return True

    def consume(self) -> tuple[Any, ...]:
        """消耗一次剂量并返回 Effect Facet 中的效果描述。"""
        doses = self.get("doses", 0)
        if doses <= 0:
            return ()
        self.set("doses", doses - 1)
        return tuple(facet.value for facet in self.entries("Effect"))

    def pick_up(self, actor: TreeMember, position: int = 0) -> bool:
        """从当前场景拾取自身至 actor（例如角色背包或手中）。"""
        self.reparent(actor, position)
        return True

    def drop(self, position: int = 0) -> bool:
        """从持有者身上放下自身到持有者所在的场景或父容器中。"""
        if self.parent is None or self.parent.parent is None:
            return False
        self.reparent(self.parent.parent, position)
        self.tags.pop("Held", None)
        return True

    def put_into(
        self,
        container: Entity,
        position: int = 0,
        context: Context | None = None,
    ) -> bool:
        """将自身放入目标容器。"""
        self.tags.pop("Held", None)
        return container.put(self, position, context)

    def take_from(
        self,
        container: Entity,
        actor: TreeMember,
        position: int = 0,
        context: Context | None = None,
    ) -> bool:
        """从容器中取出自身至 actor。"""
        return container.take(self, actor, position, context)

    def hold(self, actor: TreeMember) -> bool:
        """标记自身为 actor 的当前手持/装备物品。"""
        if self.parent is not actor:
            self.reparent(actor)
        self.tags["Held"] = 1
        return True

    def unhold(self) -> bool:
        """解除手持/装备状态。"""
        self.tags.pop("Held", None)
        return True

    def examine(self, observer: object | None = None) -> dict[str, Any]:
        """检视物品的客观状态、属性、承载内容与投影 Aspect。"""
        return {
            "name": self.designation({"observer": observer}),
            "tags": dict(self.tags),
            "properties": dict(self.properties),
            "aspects": dict(self.aspects()),
            "claims": tuple(facet.value for facet in self.entries("Claim")),
            "opened": self.get("opened"),
            "items": [item.name for item in self.items],
        }

    def effective_intensity(self, time: int | float | None = None) -> int | float:
        """计算 Influence 在指定行动值时刻的剩余强度。"""
        intensity = self.get("intensity", 0)
        if not self.has("Influence"):
            return intensity
        if time is None:
            return intensity
        expires_at = self.get("expires_at")
        if expires_at is not None and time >= expires_at:
            return 0
        elapsed = max(0, time - self.get("started_at", time))
        return max(0, intensity - elapsed * self.get("decay", 0))

    def effective_at(self, time: int | float) -> bool:
        """检查带 expires_at/revoked 性质的对象是否仍然有效。"""
        expires_at = self.get("expires_at")
        return not self.get("revoked", False) and (
            expires_at is None or time < expires_at
        )


class Passage(TreeMember):
    """连接两个场景的双端结构；实现世界树接口但不是 Entity。

    Passage 只处理拓扑、方向代价和通行条件。门是它的普通 Entity 子对象；
    Passage.allows 会检查子对象中的 Door 是否已经打开并解除当前侧锁。
    """

    def __init__(
        self,
        name: str,
        left: Entity,
        right: Entity,
        cost: int | float = 1,
        reverse_cost: int | float | None = None,
        parent: TreeMember | None = None,
        tags: Tags | None = None,
        position: int = 0,
        condition: Callable[[Entity, Entity | None, object | None], bool] | None = None,
        designations: Mapping[Entity, str] | None = None,
    ) -> None:
        """创建两个 Scene 之间的拓扑通道。

        Passage 进入世界树以便统一管理，但不是可拾取或消耗的 Entity；方向、
        代价和通行条件只描述跨场景连接，不处理场景内部几何。
        """
        super().__init__(
            name,
            parent,
            {"Passage": 1, "Threshold": 1, **(tags or {})},
            position,
            properties={
                "left": left,
                "right": right,
                "costs": {
                    left: cost,
                    right: cost if reverse_cost is None else reverse_cost,
                },
                "condition": condition,
            },
        )
        for scene, text in (designations or {}).items():
            self.designate(
                text,
                when=lambda _, context, scene=scene: context.get("from_scene") is scene,
            )

    @property
    def left(self) -> Entity:
        """返回通道声明时的左端场景。"""
        return self.get("left")

    @property
    def right(self) -> Entity:
        """返回通道声明时的右端场景。"""
        return self.get("right")

    @property
    def endpoints(self) -> tuple[Entity, Entity]:
        """按声明顺序返回通道两端。"""
        return self.left, self.right

    def other(self, scene: Entity) -> Entity | None:
        """给定一端，返回另一端；scene 不是端点时返回 None。"""
        return (
            self.right
            if scene is self.left
            else self.left
            if scene is self.right
            else None
        )

    def designation_from(self, scene: Entity, observer: Entity | None = None) -> str:
        """按进入方向和观察者知识解析通道名称。"""
        return self.designation({"from_scene": scene, "observer": observer})

    def cost_from(self, scene: Entity) -> int | float:
        """读取从指定端点出发的行动值代价。"""
        return self.get("costs", {}).get(scene, inf)

    def allows(
        self,
        scene: Entity,
        actor: Entity | None = None,
        env: object | None = None,
    ) -> bool:
        """检查端点、门和额外 condition 是否允许穿越。"""
        if self.other(scene) is None:
            return False
        context = {"from_scene": scene, "actor": actor, "env": env}
        if any(
            child.has("Door") and not child.accessible(context)
            for child in self.children
        ):
            return False
        condition = self.get("condition")
        return condition(scene, actor, env) if condition else True


def add_temp_entity(
    timeline: Any,
    name: str,
    tags: Tags,
    parent: TreeMember,
    delete_time: int | float = 60,
) -> Entity:
    """创建临时 Entity，并在指定行动值后将其移出世界树。"""
    entity = Entity(name, parent, tags)
    timeline.schedule(delete_time, name, lambda: remove_entity(entity))
    return entity


def remove_entity(entity: TreeMember) -> dict[Any, Any]:
    """将对象移出世界树；返回值保留给统一效果回调协议。"""
    entity.reparent(None)
    return {}


def transfer_entity(
    left: TreeMember,
    right: TreeMember,
    l_items: Mapping[int, TreeMember] | None = None,
    r_items: Mapping[int, TreeMember] | None = None,
) -> dict[Any, Any]:
    """把两组对象分别转移到指定父节点和槽位。"""
    for position, entity in (l_items or {}).items():
        entity.reparent(left, position)
    for position, entity in (r_items or {}).items():
        entity.reparent(right, position)
    return {}


def change_and_remake(
    parent: TreeMember,
    destroy_items: Iterable[TreeMember],
    keep_items: Iterable[TreeMember],
    generate_items: Iterable[Callable[..., TreeMember]],
) -> dict[Any, Any]:
    """在同一父节点下完成销毁、保留和生成的组合变换。"""
    for entity in destroy_items:
        remove_entity(entity)
    for entity in keep_items:
        entity.reparent(parent)
    for factory in generate_items:
        factory(parent=parent)
    return {}


def change_to_entity(
    entity: TreeMember, new_item_factory: Callable[..., TreeMember]
) -> dict[Any, Any]:
    """以工厂生成的新对象替换原对象，并保持其父节点和槽位。"""
    parent = entity.parent
    if parent is None:
        return {}
    position = parent.children[entity]
    entity.reparent(None)
    new_item_factory(parent=parent, position=position)
    return {}


ITEM_KINDS: dict[str, dict[str, int]] = {
    "Table": {"Furniture": 1, "Surface": 1},
    "Chair": {"Furniture": 1, "Seat": 1},
    "Bed": {"Furniture": 1, "RestingPlace": 1},
    "Lamp": {"Tool": 1, "LightSource": 1},
    "Wardrobe": {"Furniture": 1, "Container": 1, "Openable": 1},
    "Mirror": {"Tool": 1, "Reflective": 1},
    "Stove": {"Tool": 1, "HeatSource": 1},
    "Pot": {"Tool": 1, "Vessel": 1, "Container": 1},
    "Knife": {"Tool": 1, "Weapon": 1, "Blade": 1},
    "Sword": {"Tool": 1, "Weapon": 1, "Blade": 1},
    "Spear": {"Tool": 1, "Weapon": 1, "Polearm": 1},
    "Firearm": {"Tool": 1, "Weapon": 1, "Ranged": 1},
    "Shield": {"Tool": 1, "Armor": 1},
    "Bow": {"Tool": 1, "Weapon": 1, "Ranged": 1},
    "Arrow": {"Ammunition": 1, "Projectile": 1},
    "Bullet": {"Ammunition": 1, "Projectile": 1},
    "Pen": {"Tool": 1, "WritingTool": 1},
    "Paper": {"Reagent": 1, "WritingSurface": 1},
    "Copier": {"Tool": 1, "CopyingTool": 1},
    "Key": {"Tool": 1, "AccessToken": 1},
    "Clothing": {"Wearable": 1},
    "Armor": {"Wearable": 1, "Armor": 1},
    "Currency": {"Exchange": 1},
    "RitualInstrument": {"Tool": 1, "Instrument": 1},
    "Offering": {"Reagent": 1, "Offering": 1},
    "Material": {"Reagent": 1, "Material": 1},
    "Tool": {"Tool": 1},
    "Book": {"ReadingMaterial": 1, "InformationCarrier": 1, "Tool": 1},
    "Document": {"ReadingMaterial": 1, "InformationCarrier": 1, "Record": 1},
    "Device": {"Tool": 1, "Mechanism": 1},
    "Mechanism": {"Tool": 1, "Mechanism": 1},
    "Flask": {"Tool": 1, "Vessel": 1, "Container": 1, "Openable": 1},
    "Bottle": {"Tool": 1, "Vessel": 1, "Container": 1, "Openable": 1},
    "Kit": {"Tool": 1, "Container": 1, "Openable": 1},
    "Specimen": {"Reagent": 1, "Material": 1, "Specimen": 1},
    "Lock": {"Tool": 1, "Mechanism": 1, "Lock": 1},
    "Food": {"Consumable": 1, "Food": 1},
    "Drink": {"Consumable": 1, "Drink": 1},
    "Consumable": {"Consumable": 1},
}


def make_item(
    name: str,
    kind: str,
    parent: TreeMember | None = None,
    tags: Tags | None = None,
    position: int = 0,
    **properties: Any,
) -> Entity:
    """根据 ITEM_KINDS 创建普通物品。

    ``properties`` 会进入独立字典，不会覆盖 Entity 的方法或核心字段。
    """
    item_tags = {kind: 1, **ITEM_KINDS.get(kind, {}), **(tags or {})}
    properties.setdefault("opened", kind == "Pot")
    return Entity(name, parent, item_tags, position, properties)


def Door(
    name: str,
    passage: Passage,
    opened: bool = False,
    locks: Mapping[Entity, int] | None = None,
    tags: Tags | None = None,
    position: int = 0,
) -> Entity:
    """创建安装在 Passage 下的 Door Entity。locks 使用 ``场景: 强度``。"""
    door = Entity(
        name,
        passage,
        {"Door": 1, "Tool": 1, "Openable": 1, **(tags or {})},
        position,
        {"opened": opened},
    )
    for scene, strength in (locks or {}).items():
        door.lock(strength, scene)
    return door


def Container(
    name: str,
    parent: TreeMember | None = None,
    opened: bool = False,
    lock_strength: int = 0,
    tags: Tags | None = None,
    position: int = 0,
) -> Entity:
    """创建可开合、可上锁且能收纳子 Entity 的容器。"""
    container = Entity(
        name,
        parent,
        {"Container": 1, "Openable": 1, **(tags or {})},
        position,
        {"opened": opened},
    )
    if lock_strength > 0:
        container.lock(lock_strength)
    return container


def Box(
    name: str,
    parent: TreeMember | None = None,
    opened: bool = False,
    lock_strength: int = 0,
    tags: Tags | None = None,
    position: int = 0,
) -> Entity:
    """Container 的箱子预设。"""
    return Container(
        name, parent, opened, lock_strength, {"Box": 1, **(tags or {})}, position
    )


def Knowledge(
    name: str,
    parent: TreeMember | None = None,
    content: Any = None,
    subject: Entity | None = None,
    source: object | None = None,
    tags: Tags | None = None,
    position: int = 0,
) -> Entity:
    """创建知识对象；content 是声明内容，不自动成为客观事实。"""
    knowledge = Entity(
        name,
        parent,
        {"Knowledge": 1, **(tags or {})},
        position,
        {"content": content, "subject": subject, "source": source},
    )
    if content is not None:
        knowledge.attach("Claim", content, source=source)
    return knowledge


def InformationCarrier(
    name: str,
    medium: str,
    parent: TreeMember | None = None,
    content: Any = None,
    creator: object | None = None,
    claims: Iterable[Any] | None = None,
    tags: Tags | None = None,
    position: int = 0,
) -> Entity:
    """创建信息载体；若 content 是 Knowledge，则建立 Carries 关系。"""
    carrier = Entity(
        name,
        parent,
        {"InformationCarrier": 1, "Record": 1, medium: 1, **(tags or {})},
        position,
        {"medium": medium, "content": content},
    )
    if creator is not None:
        carrier.relate("MadeBy", creator)
    if isinstance(content, Entity) and content.has("Knowledge"):
        carrier.relate("Carries", content)
    for claim in claims or ():
        carrier.attach("Claim", claim, source=creator)
    return carrier


def Manuscript(
    name: str,
    parent: TreeMember | None = None,
    content: Any = None,
    author: object | None = None,
    language: Entity | None = None,
    claims: Iterable[Any] | None = None,
    tags: Tags | None = None,
    position: int = 0,
) -> Entity:
    """InformationCarrier 的手稿预设。语言使用 Entity 引用时可限制阅读。"""
    manuscript = InformationCarrier(
        name, "Manuscript", parent, content, author, claims, tags, position
    )
    manuscript.set("language", language)
    return manuscript


def IdentityProof(
    name: str,
    subject: Entity,
    issuer: Entity,
    parent: TreeMember | None = None,
    claims: Iterable[Any] | None = None,
    content: Any = None,
    language: Entity | None = None,
    expires_at: int | float | None = None,
    revoked: bool = False,
    tags: Tags | None = None,
    position: int = 0,
) -> Entity:
    """创建身份证明。

    Subject 和 IssuedBy 都只是证件声明；客观身份与真实签发仍由世界核验。
    """
    proof = InformationCarrier(
        name, "IdentityProof", parent, content, issuer, claims, tags, position
    )
    proof.properties.update(language=language, expires_at=expires_at, revoked=revoked)
    # 证件只作出声明；真实身份与签发关系必须由客观世界另行确认。
    proof.attach("Claim", ("Subject", subject), source=issuer)
    proof.attach("Claim", ("IssuedBy", issuer), source=issuer)
    return proof


def AuthorityToken(
    name: str,
    issuer: Entity,
    parent: TreeMember | None = None,
    grants: Iterable[Any] | None = None,
    scope: Any = None,
    tags: Tags | None = None,
    position: int = 0,
) -> Entity:
    """创建印章、权杖等授权载体；持有它不自动取得客观权限。"""
    token = Entity(
        name, parent, {"AuthorityToken": 1, "Tool": 1, **(tags or {})}, position
    )
    # 持有令牌不自动取得权限，令牌上的内容仍须由规则核验。
    token.attach("Claim", ("IssuedBy", issuer), source=issuer)
    for grant in grants or ():
        token.attach("Claim", ("Grants", grant), source=issuer)
    if scope is not None:
        token.attach("Claim", ("Scope", scope), source=issuer)
    return token


def Vehicle(
    name: str,
    parent: TreeMember | None = None,
    opened: bool = True,
    tags: Tags | None = None,
    position: int = 0,
) -> Entity:
    """创建能够收纳乘员或货物的交通载体。移动由行动系统处理。"""
    return Container(name, parent, opened, 0, {"Vehicle": 1, **(tags or {})}, position)


def Potion(
    name: str,
    parent: TreeMember | None = None,
    effects: Iterable[Any] | None = None,
    recipe: object | None = None,
    doses: int = 1,
    tags: Tags | None = None,
    position: int = 0,
) -> Entity:
    """创建具有剂量与 Effect Facet 的消耗性药剂。"""
    potion = Entity(
        name,
        parent,
        {"Potion": 1, "Reagent": 1, "Consumable": 1, **(tags or {})},
        position,
        {"recipe": recipe, "doses": doses},
    )
    for effect in effects or ():
        potion.attach("Effect", effect, source=recipe)
    return potion


def TrueName(
    name: str,
    target: Entity,
    expression: str,
    parent: TreeMember | None = None,
    source: object | None = None,
    tags: Tags | None = None,
    position: int = 0,
) -> Entity:
    """创建关于真名的知识声明。

    它不会自行建立客观 TrueNameOf 关系；真实绑定必须由世界事实核验。
    """
    knowledge = Knowledge(
        name,
        parent,
        ("TrueNameOf", target, expression),
        target,
        source,
        {"TrueNameClaim": 1, "Naming": 1, **(tags or {})},
        position,
    )
    knowledge.set("expression", expression)
    return knowledge


def RitualKnowledge(
    name: str,
    recipe: object,
    parent: TreeMember | None = None,
    source: object | None = None,
    tags: Tags | None = None,
    position: int = 0,
) -> Entity:
    """创建指向具体仪式配方的知识对象。"""
    knowledge = Knowledge(
        name,
        parent,
        recipe,
        recipe,
        source,
        {"RitualKnowledge": 1, "Invocation": 1, **(tags or {})},
        position,
    )
    knowledge.set("recipe", recipe)
    return knowledge


def Influence(
    name: str,
    emotion: str,
    intensity: int,
    parent: TreeMember,
    target: Entity | None = None,
    source: object | None = None,
    mode: str = "impulse",
    persistence: int = 1,
    decay: int = 0,
    started_at: int | float = 0,
    expires_at: int | float | None = None,
    tags: Tags | None = None,
) -> Entity:
    """创建情感或动态影响 Entity，并附加其天然 Aspect。

    主要 Aspect 使用 intensity，次要 Aspect 使用 intensity // 2。mode、
    persistence、decay 与时间字段共同区分短期情绪和长期情感。
    """
    influence = Entity(
        name,
        parent,
        {"Influence": 1, emotion: intensity, **(tags or {})},
        properties={
            "emotion": emotion,
            "intensity": intensity,
            "mode": mode,
            "persistence": persistence,
            "decay": decay,
            "started_at": started_at,
            "expires_at": expires_at,
        },
    )
    if target is not None:
        influence.relate("Target", target)
    if source is not None:
        influence.relate("Source", source)
    primary, secondary = EMOTION_ASPECTS.get(emotion, ((), ()))
    for aspect in primary:
        influence.attach("Aspect", aspect, intensity, source=source)
    for aspect in secondary:
        influence.attach("Aspect", aspect, intensity // 2, source=source)
    return influence
