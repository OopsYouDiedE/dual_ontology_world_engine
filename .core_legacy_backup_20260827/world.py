"""World state with separated facts, cognition, resources, and audit history."""

from __future__ import annotations

from dataclasses import InitVar, asdict, dataclass, field, fields, is_dataclass
from enum import Enum
from hashlib import sha256
import json
from math import isfinite
from typing import Any, Mapping

from .audit import AuditLedger
from .errors import ValidationError
from .expressions import ConceptRegistry
from .resources import ResourceStore
from .types import IdentityAnchors, Layer, Proposition, Support, Truth, Vec3


def _unit_interval(name: str, value: float) -> None:
    """执行 _unit_interval 对应的业务逻辑处理。
    
    Args:
        name (str): 名称或标识符字符串。
        value (float): 输入参数 value。
    """
    if not 0.0 <= value <= 1.0:
        raise ValidationError(f"{name} must be in [0, 1]")


def _authority_value(value: Any, seen: set[int] | None = None) -> Any:
    """Canonical, complete replay encoding; opaque objects are rejected。
    
    Args:
        value (Any): 输入参数 value。
        seen (Any): 输入参数 seen。
    
    Returns:
        Any: 计算或处理后的 Any 结果。
    """

    if value is None or isinstance(value, (bool, int, str)):
        return value
    if isinstance(value, float):
        if not isfinite(value):
            raise ValidationError("authoritative state numbers must be finite")
        return ["float", value.hex()]
    if isinstance(value, Enum):
        return ["enum", type(value).__module__, type(value).__qualname__, _authority_value(value.value)]
    tracking = seen if seen is not None else set()
    identifier = id(value)
    if identifier in tracking:
        raise ValidationError("authoritative state contains a reference cycle")
    if isinstance(value, Mapping):
        tracking.add(identifier)
        result = [
            (_authority_value(key, tracking), _authority_value(item, tracking))
            for key, item in value.items()
        ]
        tracking.remove(identifier)
        return ["mapping", sorted(result, key=lambda item: json.dumps(item[0], sort_keys=True, default=str))]
    if isinstance(value, (tuple, list, set, frozenset)):
        tracking.add(identifier)
        result = [_authority_value(item, tracking) for item in value]
        tracking.remove(identifier)
        if isinstance(value, (set, frozenset)):
            result.sort(key=lambda item: json.dumps(item, sort_keys=True, default=str))
        return [type(value).__name__, result]
    if is_dataclass(value) and type(value).__module__.startswith("dual_ontology_engine"):
        tracking.add(identifier)
        result = {
            item.name: _authority_value(getattr(value, item.name), tracking)
            for item in fields(value)
        }
        tracking.remove(identifier)
        return ["dataclass", type(value).__module__, type(value).__qualname__, result]
    raise ValidationError(
        f"authoritative state contains an opaque value: {type(value).__module__}.{type(value).__qualname__}"
    )


def _clone_authority_value(value: Any, seen: dict[int, Any] | None = None) -> Any:
    """Deep-copy the closed authority tree without invoking user callbacks。
    
    Args:
        value (Any): 输入参数 value。
        seen (Any): 输入参数 seen。
    
    Returns:
        Any: 计算或处理后的 Any 结果。
    """

    if value is None or isinstance(value, (bool, int, float, str, Enum)):
        return value
    memo = seen if seen is not None else {}
    identifier = id(value)
    if identifier in memo:
        return memo[identifier]
    if isinstance(value, Mapping):
        result: dict[Any, Any] = {}
        memo[identifier] = result
        for key, item in value.items():
            result[_clone_authority_value(key, memo)] = _clone_authority_value(item, memo)
        return result
    if isinstance(value, list):
        result_list: list[Any] = []
        memo[identifier] = result_list
        result_list.extend(_clone_authority_value(item, memo) for item in value)
        return result_list
    if isinstance(value, tuple):
        result_tuple = tuple(_clone_authority_value(item, memo) for item in value)
        memo[identifier] = result_tuple
        return result_tuple
    if isinstance(value, set):
        result_set = {_clone_authority_value(item, memo) for item in value}
        memo[identifier] = result_set
        return result_set
    if isinstance(value, frozenset):
        result_frozen = frozenset(_clone_authority_value(item, memo) for item in value)
        memo[identifier] = result_frozen
        return result_frozen
    if is_dataclass(value) and type(value).__module__.startswith("dual_ontology_engine"):
        cloned_fields = {
            item.name: _clone_authority_value(getattr(value, item.name), memo)
            for item in fields(value)
            if item.init
        }
        result_dataclass = type(value)(**cloned_fields)
        memo[identifier] = result_dataclass
        return result_dataclass
    # Opaque values are never granted write authority. Preserve the reference
    # so validation can reject it without calling copy/deepcopy hooks.
    return value


@dataclass(frozen=True)
class Personality:
    courage: float
    wisdom: float
    unity: float
    risk_aversion: float
    curiosity: float
    rule_trust: float
    self_preservation: float = 1.0

    def __post_init__(self) -> None:
        """数据类初始化后的字段校验与约束校验。
        """
        for name, value in asdict(self).items():
            _unit_interval(name, value)


@dataclass(frozen=True)
class Goal:
    id: str
    weight: float
    description: str
    terminal_predicate: str | None = None

    def __post_init__(self) -> None:
        """数据类初始化后的字段校验与约束校验。
        """
        if not self.id or not self.description or self.weight < 0:
            raise ValidationError("goal requires id, description, and nonnegative weight")


@dataclass(frozen=True)
class Character:
    id: str
    name: str
    identity: str
    personality: Personality
    goals: tuple[Goal, ...]
    initial_knowledge: frozenset[str]
    obeyed_rules: frozenset[str]
    free_lingual: float
    anchors: IdentityAnchors
    capabilities: frozenset[str]
    consent_threshold: float = 0.4

    def __post_init__(self) -> None:
        """数据类初始化后的字段校验与约束校验。
        """
        if not self.id or not self.name or not self.identity or not self.goals:
            raise ValidationError("character requires id, name, identity, and goals")
        if self.free_lingual < 0:
            raise ValidationError("free lingual capacity cannot be negative")
        _unit_interval("consent_threshold", self.consent_threshold)


@dataclass(frozen=True)
class Anomaly:
    id: str
    name: str
    faction: str
    traits: frozenset[str]
    goals: tuple[Goal, ...]
    abilities: frozenset[str]
    obeyed_rules: frozenset[str]
    budget_owner: str
    mutation_operators: frozenset[str]
    identity_attack_template: Mapping[str, float] = field(default_factory=dict)

    def __post_init__(self) -> None:
        """数据类初始化后的字段校验与约束校验。
        """
        if not all((self.id, self.name, self.faction, self.goals, self.budget_owner)):
            raise ValidationError("anomaly requires identity, faction, goals, and a budget owner")
        for value in self.identity_attack_template.values():
            _unit_interval("identity attack threshold", value)


@dataclass
class WorldState:
    registry: ConceptRegistry
    seed: int
    resources: ResourceStore = field(default_factory=ResourceStore)
    ledger: AuditLedger = field(default_factory=AuditLedger)
    time: float = 0.0
    turn: int = 0
    version: int = 0
    facts: dict[tuple[Any, ...], Truth] = field(default_factory=dict)
    cognition: dict[tuple[str, Layer, tuple[Any, ...]], Support] = field(default_factory=dict)
    sanity: dict[str, float] = field(default_factory=dict)
    characters: dict[str, Character] = field(default_factory=dict)
    anomalies: dict[str, Anomaly] = field(default_factory=dict)
    positions: dict[str, tuple[str, Vec3]] = field(default_factory=dict)
    active_constraints: dict[str, Any] = field(default_factory=dict)
    exposure_signatures: set[tuple[str, str]] = field(default_factory=set)
    knowledge_gains: set[tuple[str, str, str]] = field(default_factory=set)
    hidden_parameters: dict[str, Any] = field(default_factory=dict)
    freeze_registry: InitVar[bool] = True

    def __post_init__(self, freeze_registry: bool) -> None:
        """数据类初始化后的字段校验与约束校验。
        
        Args:
            freeze_registry (bool): 输入参数 freeze_registry。
        """
        if not isinstance(self.seed, int):
            raise ValidationError("hidden seed must be an integer")
        if any(not 0.0 <= value <= 100.0 for value in self.sanity.values()):
            raise ValidationError("sanity values must be in [0, 100]")
        if freeze_registry:
            self.registry.freeze()

    def evaluate(self, proposition: Proposition) -> Truth:
        """对给定的命题或状态节点进行求值计算。
        
        Args:
            proposition (Proposition): 待评估的闭合命题对象。
        
        Returns:
            Truth: 三值逻辑求值结果（TRUE/FALSE/UNKNOWN）。
        """
        value = self.facts.get(proposition.atom, Truth.UNKNOWN)
        return ~value if proposition.negated else value

    def support(self, actor: str, proposition: Proposition, layer: Layer) -> Support:
        """执行 support 对应的业务逻辑处理。
        
        Args:
            actor (str): 执行动作的主体角色标识。
            proposition (Proposition): 待评估的闭合命题对象。
            layer (Layer): 输入参数 layer。
        
        Returns:
            Support: 命题支持集合对象。
        """
        return self.cognition.get((actor, layer, proposition.atom), Support.NONE)

    def set_initial_fact(self, proposition: Proposition, value: Truth) -> None:
        """设置或更新 initial fact 相关属性。
        
        Args:
            proposition (Proposition): 待评估的闭合命题对象。
            value (Truth): 输入参数 value。
        """
        if proposition.negated:
            value = ~value
        current = self.facts.get(proposition.atom, Truth.UNKNOWN)
        if current is not Truth.UNKNOWN and value is not Truth.UNKNOWN and current is not value:
            raise ValidationError("initial facts are inconsistent")
        self.facts[proposition.atom] = value

    def clone(self) -> "WorldState":
        """执行 clone 对应的业务逻辑处理。
        
        Returns:
            WorldState: 更新或克隆后的世界状态实例。
        """
        return WorldState(
            registry=self.registry,
            seed=self.seed,
            resources=self.resources.clone(),
            ledger=self.ledger.clone(),
            time=self.time,
            turn=self.turn,
            version=self.version,
            facts=dict(self.facts),
            cognition=dict(self.cognition),
            sanity=dict(self.sanity),
            characters=_clone_authority_value(self.characters),
            anomalies=_clone_authority_value(self.anomalies),
            positions=dict(self.positions),
            active_constraints=_clone_authority_value(self.active_constraints),
            exposure_signatures=set(self.exposure_signatures),
            knowledge_gains=set(self.knowledge_gains),
            hidden_parameters=_clone_authority_value(self.hidden_parameters),
            freeze_registry=False,
        )

    def public_snapshot(self) -> dict[str, Any]:
        """执行 public_snapshot 对应的业务逻辑处理。
        
        Returns:
            dict[Tuple[str, Any]]: 计算或处理后的 dict[Tuple[str, Any]] 结果。
        """
        return {
            "time": self.time,
            "turn": self.turn,
            "version": self.version,
            "facts": sorted((str(key), value.value) for key, value in self.facts.items()),
            "sanity": dict(sorted(self.sanity.items())),
            "positions": sorted((actor, room, asdict(point)) for actor, (room, point) in self.positions.items()),
            "resources": sorted(
                (lot.id, lot.owner, lot.kind.value, lot.amount, lot.state.value, lot.consumed_amount)
                for lot in self.resources.lots
            ),
            "ledger_head": self.ledger.events[-1].digest if self.ledger.events else "GENESIS",
        }

    def digest(self) -> str:
        """生成并返回当前状态或拓扑的全局摘要哈希。
        
        Returns:
            str: 导出的字符串表达或摘要哈希。
        """
        raw = json.dumps(self.public_snapshot(), sort_keys=True, ensure_ascii=False, default=str)
        return sha256(raw.encode("utf-8")).hexdigest()

    def authority_digest(self) -> str:
        """执行 authority_digest 对应的业务逻辑处理。
        
        Returns:
            str: 导出的字符串表达或摘要哈希。
        """
        raw = json.dumps(
            _authority_value(self.authoritative_snapshot()),
            sort_keys=True,
            ensure_ascii=False,
            separators=(",", ":"),
        )
        return sha256(raw.encode("utf-8")).hexdigest()

    def restore_from(self, source: "WorldState") -> None:
        """Restore the authoritative object after an untrusted callback write。
        
        Args:
            source (WorldState): 输入参数 source。
        """

        restored = source.clone()
        self.registry = restored.registry
        self.seed = restored.seed
        self.resources = restored.resources
        self.ledger = restored.ledger
        self.time = restored.time
        self.turn = restored.turn
        self.version = restored.version
        self.facts = restored.facts
        self.cognition = restored.cognition
        self.sanity = restored.sanity
        self.characters = restored.characters
        self.anomalies = restored.anomalies
        self.positions = restored.positions
        self.active_constraints = restored.active_constraints
        self.exposure_signatures = restored.exposure_signatures
        self.knowledge_gains = restored.knowledge_gains
        self.hidden_parameters = restored.hidden_parameters

    def authoritative_snapshot(self) -> dict[str, Any]:
        """Full replay state. This object is for audit systems, not characters。
        
        Returns:
            dict[Tuple[str, Any]]: 计算或处理后的 dict[Tuple[str, Any]] 结果。
        """

        snapshot = self.public_snapshot()
        snapshot.update(
            {
                "seed": self.seed,
                "cognition": sorted(
                    (actor, layer.value, str(atom), support.value)
                    for (actor, layer, atom), support in self.cognition.items()
                ),
                "characters": sorted(
                    (identifier, asdict(character))
                    for identifier, character in self.characters.items()
                ),
                "anomalies": sorted(
                    (identifier, asdict(anomaly))
                    for identifier, anomaly in self.anomalies.items()
                ),
                "active_constraints": sorted(
                    (key, _authority_value(value)) for key, value in self.active_constraints.items()
                ),
                "exposure_signatures": sorted(self.exposure_signatures),
                "knowledge_gains": sorted(self.knowledge_gains),
                "hidden_parameters": sorted(
                    (key, _authority_value(value)) for key, value in self.hidden_parameters.items()
                ),
            }
        )
        return snapshot
