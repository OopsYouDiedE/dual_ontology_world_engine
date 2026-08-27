"""Finite proposition expressions and a strict concept registry/compiler."""

from __future__ import annotations

from dataclasses import dataclass, field
from types import MappingProxyType
from typing import Iterable, Mapping, Protocol

from .errors import CompileError
from .types import Layer, Proposition, TimeWindow, Truth


class FactReader(Protocol):
    def evaluate(self, proposition: Proposition) -> Truth:
                                                           """对给定的命题或状态节点进行求值计算。
                                                           
                                                           Args:
                                                               proposition (Proposition): 待评估的闭合命题对象。
                                                           
                                                           Returns:
                                                               Truth: 三值逻辑求值结果（TRUE/FALSE/UNKNOWN）。
                                                           """
                                                           ...


class Expression(Protocol):
    def evaluate(self, world: FactReader) -> Truth:
                                                    """对给定的命题或状态节点进行求值计算。
                                                    
                                                    Args:
                                                        world (FactReader): 全局世界状态实例。
                                                    
                                                    Returns:
                                                        Truth: 三值逻辑求值结果（TRUE/FALSE/UNKNOWN）。
                                                    """
                                                    ...


@dataclass(frozen=True)
class Atom:
    proposition: Proposition

    def evaluate(self, world: FactReader) -> Truth:
        """对给定的命题或状态节点进行求值计算。
        
        Args:
            world (FactReader): 全局世界状态实例。
        
        Returns:
            Truth: 三值逻辑求值结果（TRUE/FALSE/UNKNOWN）。
        """
        return world.evaluate(self.proposition)


@dataclass(frozen=True)
class Not:
    expression: Expression

    def evaluate(self, world: FactReader) -> Truth:
        """对给定的命题或状态节点进行求值计算。
        
        Args:
            world (FactReader): 全局世界状态实例。
        
        Returns:
            Truth: 三值逻辑求值结果（TRUE/FALSE/UNKNOWN）。
        """
        return ~self.expression.evaluate(world)


@dataclass(frozen=True)
class All:
    expressions: tuple[Expression, ...]

    def __post_init__(self) -> None:
        """数据类初始化后的字段校验与约束校验。
        """
        if not self.expressions:
            raise CompileError("an empty conjunction is not executable")

    def evaluate(self, world: FactReader) -> Truth:
        """对给定的命题或状态节点进行求值计算。
        
        Args:
            world (FactReader): 全局世界状态实例。
        
        Returns:
            Truth: 三值逻辑求值结果（TRUE/FALSE/UNKNOWN）。
        """
        result = Truth.TRUE
        for expression in self.expressions:
            result = result & expression.evaluate(world)
        return result


@dataclass(frozen=True)
class Any:
    expressions: tuple[Expression, ...]

    def __post_init__(self) -> None:
        """数据类初始化后的字段校验与约束校验。
        """
        if not self.expressions:
            raise CompileError("an empty disjunction is not executable")

    def evaluate(self, world: FactReader) -> Truth:
        """对给定的命题或状态节点进行求值计算。
        
        Args:
            world (FactReader): 全局世界状态实例。
        
        Returns:
            Truth: 三值逻辑求值结果（TRUE/FALSE/UNKNOWN）。
        """
        result = Truth.FALSE
        for expression in self.expressions:
            result = result | expression.evaluate(world)
        return result


@dataclass(frozen=True)
class PredicateSignature:
    name: str
    subject_types: frozenset[str]
    object_types: frozenset[str]
    layers: frozenset[Layer]


@dataclass
class ConceptRegistry:
    """The only supported route from surface terms to executable propositions."""

    concepts: dict[str, str] = field(default_factory=dict)
    instances: dict[str, str] = field(default_factory=dict)
    locations: set[str] = field(default_factory=set)
    containments: set[tuple[str, str]] = field(default_factory=set)
    predicates: dict[str, PredicateSignature] = field(default_factory=dict)
    _frozen: bool = field(default=False, init=False, repr=False)

    def __setattr__(self, name: str, value: object) -> None:
        """执行 __setattr__ 对应的业务逻辑处理。
        
        Args:
            name (str): 名称或标识符字符串。
            value (object): 输入参数 value。
        """
        if getattr(self, "_frozen", False) and name in {
            "concepts",
            "instances",
            "locations",
            "containments",
            "predicates",
            "_frozen",
        }:
            raise CompileError("a frozen concept registry cannot be rebound")
        object.__setattr__(self, name, value)

    @property
    def frozen(self) -> bool:
        """执行 frozen 对应的业务逻辑处理。
        
        Returns:
            bool: 校验或判定结果布尔值。
        """
        return self._frozen

    def freeze(self) -> "ConceptRegistry":
        """执行 freeze 对应的业务逻辑处理。
        
        Returns:
            ConceptRegistry: 计算或处理后的 ConceptRegistry 结果。
        """
        if self._frozen:
            return self
        # A boolean flag is not an immutability boundary: callers could still
        # mutate the public containers directly.  Publish defensive immutable
        # snapshots when a registry becomes authoritative.
        self.concepts = MappingProxyType(dict(self.concepts))  # type: ignore[assignment]
        self.instances = MappingProxyType(dict(self.instances))  # type: ignore[assignment]
        self.locations = frozenset(self.locations)  # type: ignore[assignment]
        self.containments = frozenset(self.containments)  # type: ignore[assignment]
        self.predicates = MappingProxyType(dict(self.predicates))  # type: ignore[assignment]
        self._frozen = True
        return self

    def _ensure_mutable(self) -> None:
        """执行 _ensure_mutable 对应的业务逻辑处理。
        """
        if self._frozen:
            raise CompileError("the concept registry is frozen for this world")

    def concept(self, term: str, concept_id: str) -> "ConceptRegistry":
        """执行 concept 对应的业务逻辑处理。
        
        Args:
            term (str): 输入参数 term。
            concept_id (str): 输入参数 concept_id。
        
        Returns:
            ConceptRegistry: 计算或处理后的 ConceptRegistry 结果。
        """
        self._ensure_mutable()
        if not term or not concept_id:
            raise CompileError("concept term and id are required")
        previous = self.concepts.get(term)
        if previous is not None and previous != concept_id:
            raise CompileError(f"ambiguous term: {term}")
        self.concepts[term] = concept_id
        return self

    def instance(self, instance_id: str, concept_id: str) -> "ConceptRegistry":
        """执行 instance 对应的业务逻辑处理。
        
        Args:
            instance_id (str): 输入参数 instance_id。
            concept_id (str): 输入参数 concept_id。
        
        Returns:
            ConceptRegistry: 计算或处理后的 ConceptRegistry 结果。
        """
        self._ensure_mutable()
        if concept_id not in self.concepts.values():
            raise CompileError(f"unregistered concept: {concept_id}")
        self.instances[instance_id] = concept_id
        return self

    def location(self, location_id: str) -> "ConceptRegistry":
        """执行 location 对应的业务逻辑处理。
        
        Args:
            location_id (str): 输入参数 location_id。
        
        Returns:
            ConceptRegistry: 计算或处理后的 ConceptRegistry 结果。
        """
        self._ensure_mutable()
        if not location_id:
            raise CompileError("location id is required")
        self.locations.add(location_id)
        return self

    def contains(self, outer: str, inner: str) -> "ConceptRegistry":
        """Register a strict finite scope-containment edge。
        
        Args:
            outer (str): 输入参数 outer。
            inner (str): 输入参数 inner。
        
        Returns:
            ConceptRegistry: 计算或处理后的 ConceptRegistry 结果。
        """

        self._ensure_mutable()
        if not outer or not inner or outer == inner:
            raise CompileError("containment requires distinct nonempty ids")
        self.containments.add((outer, inner))
        return self

    def contains_transitively(self, outer: str, inner: str) -> bool:
        """执行 contains_transitively 对应的业务逻辑处理。
        
        Args:
            outer (str): 输入参数 outer。
            inner (str): 输入参数 inner。
        
        Returns:
            bool: 校验或判定结果布尔值。
        """
        if outer == inner:
            return True
        reached = {outer}
        pending = [outer]
        while pending:
            current = pending.pop()
            for parent, child in self.containments:
                if parent != current or child in reached:
                    continue
                if child == inner:
                    return True
                reached.add(child)
                pending.append(child)
        return False

    def predicate(
        self,
        name: str,
        *,
        subjects: Iterable[str],
        objects: Iterable[str],
        layers: Iterable[Layer],
    ) -> "ConceptRegistry":
        """执行 predicate 对应的业务逻辑处理。
        
        Args:
            name (str): 名称或标识符字符串。
        
        Returns:
            ConceptRegistry: 计算或处理后的 ConceptRegistry 结果。
        """
        self._ensure_mutable()
        self.predicates[name] = PredicateSignature(
            name,
            frozenset(subjects),
            frozenset(objects),
            frozenset(layers),
        )
        return self

    def compile(
        self,
        subject: str,
        predicate: str,
        object: str,
        location: str,
        window: TimeWindow,
        *,
        layer: Layer = Layer.OBJECTIVE,
        negated: bool = False,
        definitions: Mapping[str, str] | None = None,
    ) -> Proposition:
        """将输入三元组编译为强类型闭合命题。
        
        Args:
            subject (str): 输入参数 subject。
            predicate (str): 输入参数 predicate。
            object (str): 输入参数 object。
            location (str): 输入参数 location。
            window (TimeWindow): 输入参数 window。
        
        Returns:
            Proposition: 编译生成的闭合命题对象。
        """
        try:
            subject_type = self.instances[subject]
            object_type = self.instances[object]
            signature = self.predicates[predicate]
        except KeyError as exc:
            raise CompileError(f"unregistered proposition term: {exc.args[0]}") from exc
        if location not in self.locations:
            raise CompileError(f"unregistered location: {location}")
        if subject_type not in signature.subject_types:
            raise CompileError(f"predicate {predicate} rejects subject type {subject_type}")
        if object_type not in signature.object_types:
            raise CompileError(f"predicate {predicate} rejects object type {object_type}")
        if layer not in signature.layers:
            raise CompileError(f"predicate {predicate} rejects layer {layer.value}")
        supplied = dict(definitions or {})
        for term in (subject, predicate, object, location):
            supplied.setdefault(term, self.concepts.get(term, term))
        return Proposition(subject, predicate, object, location, window, layer, negated, supplied)


def is_closed_expression(value: object) -> bool:
    """判断是否满足 closed expression 相关的条件规范。
    
    Args:
        value (object): 输入参数 value。
    
    Returns:
        bool: 校验或判定结果布尔值。
    """
    if type(value) is Atom:
        return True
    if type(value) is Not:
        return is_closed_expression(value.expression)
    if type(value) in {All, Any}:
        return all(is_closed_expression(item) for item in value.expressions)
    return False
