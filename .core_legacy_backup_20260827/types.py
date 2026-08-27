"""严格，独立的值系统，根据公理规则定义。"""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from math import isfinite
from typing import FrozenSet, Iterable, Mapping

from .errors import CompileError, ValidationError


class Truth(str, Enum):
    TRUE = "true"
    FALSE = "false"
    UNKNOWN = "unknown"

    def __invert__(self) -> "Truth":
        """计算三值逻辑的非逻辑演算。

        Returns:
            Truth: 三值逻辑求值结果（TRUE/FALSE/UNKNOWN）。
        """
        return {
            Truth.TRUE: Truth.FALSE,
            Truth.FALSE: Truth.TRUE,
            Truth.UNKNOWN: Truth.UNKNOWN,
        }[self]

    def __and__(self, other: "Truth") -> "Truth":
        """计算三值逻辑的与（AND）演算。

        Args:
            other (Truth): 参与运算的另一个同类型或兼容对象。

        Returns:
            Truth: 三值逻辑求值结果（TRUE/FALSE/UNKNOWN）。
        """
        if self is Truth.FALSE or other is Truth.FALSE:
            return Truth.FALSE
        if self is Truth.TRUE and other is Truth.TRUE:
            return Truth.TRUE
        return Truth.UNKNOWN

    def __or__(self, other: "Truth") -> "Truth":
        """计算三值逻辑的或（OR）演算。

        Args:
            other (Truth): 参与运算的另一个同类型或兼容对象。

        Returns:
            Truth: 三值逻辑求值结果（TRUE/FALSE/UNKNOWN）。
        """
        if self is Truth.TRUE or other is Truth.TRUE:
            return Truth.TRUE
        if self is Truth.FALSE and other is Truth.FALSE:
            return Truth.FALSE
        return Truth.UNKNOWN


class Support(str, Enum):
    NONE = "none"
    TRUE_ONLY = "true_only"
    FALSE_ONLY = "false_only"
    BOTH = "both"

    @property
    def values(self) -> FrozenSet[Truth]:
        """执行 values 对应的业务逻辑处理。

        Returns:
            FrozenSet[Truth]: 计算或处理后的 FrozenSet[Truth] 结果。
        """
        return {
            Support.NONE: frozenset(),
            Support.TRUE_ONLY: frozenset({Truth.TRUE}),
            Support.FALSE_ONLY: frozenset({Truth.FALSE}),
            Support.BOTH: frozenset({Truth.TRUE, Truth.FALSE}),
        }[self]

    @classmethod
    def from_values(cls, values: Iterable[Truth]) -> "Support":
        """执行 from_values 对应的业务逻辑处理。

        Args:
            values (Iterable[Truth]): 输入参数 values。

        Returns:
            Support: 命题支持集合对象。
        """
        selected = frozenset(v for v in values if v is not Truth.UNKNOWN)
        for item in cls:
            if item.values == selected:
                return item
        raise ValidationError(f"invalid cognitive support: {selected!r}")

    def add(self, truth: Truth) -> "Support":
        """执行 add 对应的业务逻辑处理。

        Args:
            truth (Truth): 输入参数 truth。

        Returns:
            Support: 命题支持集合对象。
        """
        if truth is Truth.UNKNOWN:
            return self
        return Support.from_values(self.values | {truth})

    def remove(self, truth: Truth) -> "Support":
        """执行 remove 对应的业务逻辑处理。

        Args:
            truth (Truth): 输入参数 truth。

        Returns:
            Support: 命题支持集合对象。
        """
        return Support.from_values(self.values - {truth})


class Ontology(str, Enum):
    """新版公理中的三个粗粒度本体层。"""

    PHYSICAL = "physical"
    SEMANTIC = "semantic"
    COGNITIVE = "cognitive"


class Layer(str, Enum):
    """引擎使用的细粒度状态层。

    ``Ontology`` 用于公理级分层，``Layer`` 用于运行时隔离。同一命题在
    不同细粒度层具有彼此独立的状态，跨层变化必须由显式事务效果完成。
    """

    OBJECTIVE = "objective"
    INSTITUTIONAL = "institutional"
    IDENTITY = "identity"
    KNOWLEDGE = "knowledge"
    BELIEF = "belief"
    PERCEPTION = "perception"

    @property
    def ontology(self) -> Ontology:
        return {
            Layer.OBJECTIVE: Ontology.PHYSICAL,
            Layer.INSTITUTIONAL: Ontology.SEMANTIC,
            Layer.IDENTITY: Ontology.SEMANTIC,
            Layer.KNOWLEDGE: Ontology.COGNITIVE,
            Layer.BELIEF: Ontology.COGNITIVE,
            Layer.PERCEPTION: Ontology.COGNITIVE,
        }[self]


class Modality(str, Enum):
    OBLIGATION = "obligation"
    FORBIDDEN = "forbidden"
    PERMISSION = "permission"


class ResourceKind(str, Enum):
    ACTION = "action"
    ABILITY = "ability"
    SENSE = "sense"
    MEMORY = "memory"
    RELATIONSHIP = "relationship"
    IDENTITY = "identity"
    POSITION = "position"
    SAFETY = "safety"
    LIFE = "life"
    INSTITUTION = "institution"
    ANOMALY = "anomaly"
    MATTER = "matter"
    ENERGY = "energy"
    TIME = "time"
    AUTHORITY = "authority"
    FUTURE_POSSIBILITY = "future_possibility"


class Permanence(str, Enum):
    RECOVERABLE = "recoverable"
    IRREVERSIBLE = "irreversible"


@dataclass(frozen=True, order=True)
class TimeWindow:
    start: float
    end: float

    def __post_init__(self) -> None:
        """数据类初始化后的字段校验与约束校验。
        """
        if not isfinite(self.start) or not isfinite(self.end) or self.end < self.start:
            raise ValidationError("time window must be finite and ordered")

    def contains(self, time: float) -> bool:
        """判断目标坐标或范围是否位于当前区域内。

        Args:
            time (float): 指定的时间点或时间步长。

        Returns:
            bool: 校验或判定结果布尔值。
        """
        return self.start <= time <= self.end

    def overlaps(self, other: "TimeWindow") -> bool:
        """判断两个时间段或空间范围是否存在交集重叠。

        Args:
            other (TimeWindow): 参与运算的另一个同类型或兼容对象。

        Returns:
            bool: 校验或判定结果布尔值。
        """
        return self.start <= other.end and other.start <= self.end


@dataclass(frozen=True)
class Scope:
    subjects: FrozenSet[str] = frozenset()
    locations: FrozenSet[str] = frozenset()
    objects: FrozenSet[str] = frozenset()

    def __post_init__(self) -> None:
        """数据类初始化后的字段校验与约束校验。
        """
        for values in (self.subjects, self.locations, self.objects):
            if any(not value for value in values):
                raise ValidationError("scope identifiers cannot be empty")

    def contains(self, other: "Scope") -> bool:
        """判断目标坐标或范围是否位于当前区域内。

        Args:
            other (Scope): 参与运算的另一个同类型或兼容对象。

        Returns:
            bool: 校验或判定结果布尔值。
        """
        def covers(outer: FrozenSet[str], inner: FrozenSet[str]) -> bool:
            """执行 covers 对应的业务逻辑处理。

            Args:
                outer (FrozenSet[str]): 输入参数 outer。
                inner (FrozenSet[str]): 输入参数 inner。

            Returns:
                bool: 校验或判定结果布尔值。
            """
            return not outer or bool(inner) and outer.issuperset(inner)

        return (
            covers(self.subjects, other.subjects)
            and covers(self.locations, other.locations)
            and covers(self.objects, other.objects)
        )

    def overlaps(self, other: "Scope") -> bool:
        """判断两个时间段或空间范围是否存在交集重叠。

        Args:
            other (Scope): 参与运算的另一个同类型或兼容对象。

        Returns:
            bool: 校验或判定结果布尔值。
        """
        def intersects(left: FrozenSet[str], right: FrozenSet[str]) -> bool:
            """执行 intersects 对应的业务逻辑处理。

            Args:
                left (FrozenSet[str]): 输入参数 left。
                right (FrozenSet[str]): 输入参数 right。

            Returns:
                bool: 校验或判定结果布尔值。
            """
            return not left or not right or bool(left & right)

        return (
            intersects(self.subjects, other.subjects)
            and intersects(self.locations, other.locations)
            and intersects(self.objects, other.objects)
        )


@dataclass(frozen=True)
class Proposition:
    """A closed proposition. Natural language never executes directly."""

    subject: str
    predicate: str
    object: str
    location: str
    window: TimeWindow
    layer: Layer = Layer.OBJECTIVE
    negated: bool = False
    terms: Mapping[str, str] = field(
        default_factory=dict, compare=False, hash=False)

    def __post_init__(self) -> None:
        """数据类初始化后的字段校验与约束校验。
        """
        required = (self.subject, self.predicate, self.object, self.location)
        if any(not item or not isinstance(item, str) for item in required):
            raise CompileError(
                "a proposition must bind subject, predicate, object, and location")
        if self.subject == "self" or self.predicate in {"this_statement", "no_cost"}:
            raise CompileError("unresolved or ungrounded self-reference")
        if any(not key or not value for key, value in self.terms.items()):
            raise CompileError(
                "registered terms must have non-empty names and concepts")

    @property
    def atom(self) -> tuple[str, str, str, str, TimeWindow, Layer]:
        """执行 atom 对应的业务逻辑处理。

        Returns:
            tuple[Tuple[str, str, str, str, TimeWindow, Layer]]: 计算或处理后的 tuple[Tuple[str, str, str, str, TimeWindow, Layer]] 结果。
        """
        return (self.subject, self.predicate, self.object, self.location, self.window, self.layer)

    def negate(self) -> "Proposition":
        """执行 negate 对应的业务逻辑处理。

        Returns:
            Proposition: 编译生成的闭合命题对象。
        """
        return Proposition(
            self.subject,
            self.predicate,
            self.object,
            self.location,
            self.window,
            self.layer,
            not self.negated,
            self.terms,
        )


@dataclass(frozen=True)
class Evidence:
    id: str
    source: str
    proposition: Proposition
    provenance: tuple[str, ...]
    reliability: float
    scope: Scope
    expiry: float
    authentic: bool = True

    def __post_init__(self) -> None:
        """数据类初始化后的字段校验与约束校验。
        """
        if not self.id or not self.source or not self.provenance:
            raise ValidationError(
                "evidence requires id, source, and provenance")
        if not 0.0 <= self.reliability <= 1.0:
            raise ValidationError("evidence reliability must be in [0, 1]")

    def active(self, time: float, scope: Scope) -> bool:
        """执行 active 对应的业务逻辑处理。

        Args:
            time (float): 指定的时间点或时间步长。
            scope (Scope): 作用空间或范围对象。

        Returns:
            bool: 校验或判定结果布尔值。
        """
        return self.expiry >= time and self.scope.overlaps(scope)


@dataclass(frozen=True)
class Vec3:
    x: float
    y: float
    z: float

    def __post_init__(self) -> None:
        """数据类初始化后的字段校验与约束校验。
        """
        if not all(isfinite(v) for v in (self.x, self.y, self.z)):
            raise ValidationError("coordinates must be finite")

    def distance(self, other: "Vec3") -> float:
        """执行 distance 对应的业务逻辑处理。

        Args:
            other (Vec3): 参与运算的另一个同类型或兼容对象。

        Returns:
            float: 计算得出的浮点数值。
        """
        return ((self.x - other.x) ** 2 + (self.y - other.y) ** 2 + (self.z - other.z) ** 2) ** 0.5


@dataclass(frozen=True)
class IdentityAnchors:
    self_model: float = 0.0
    body: float = 0.0
    memory: float = 0.0
    social: float = 0.0
    record: float = 0.0
    commitment: float = 0.0
    lingual: float = 0.0

    def __post_init__(self) -> None:
        """数据类初始化后的字段校验与约束校验。
        """
        if any(not 0.0 <= value <= 1.0 for value in self.as_dict().values()):
            raise ValidationError(
                "identity anchor strengths must be in [0, 1]")

    def as_dict(self) -> dict[str, float]:
        """执行 as_dict 对应的业务逻辑处理。

        Returns:
            dict[Tuple[str, float]]: 计算或处理后的 dict[Tuple[str, float]] 结果。
        """
        return {
            "self": self.self_model,
            "body": self.body,
            "memory": self.memory,
            "social": self.social,
            "record": self.record,
            "commitment": self.commitment,
            "lingual": self.lingual,
        }

    def defeats(self, template: Mapping[str, float]) -> bool:
        """执行 defeats 对应的业务逻辑处理。

        Args:
            template (Mapping[Tuple[str, float]]): 输入参数 template。

        Returns:
            bool: 校验或判定结果布尔值。
        """
        values = self.as_dict()
        return any(values.get(name, 0.0) >= threshold for name, threshold in template.items())
