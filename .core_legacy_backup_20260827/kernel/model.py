"""事务核的不可变权威值。"""

from __future__ import annotations

from dataclasses import dataclass
from decimal import Decimal
from enum import Enum
from hashlib import sha256
import json
from typing import Any, Mapping


Scalar = None | bool | int | str | Decimal


def _require_text(name: str, value: str) -> None:
    if not value or value.strip() != value:
        raise ValueError(f"{name} must be non-empty canonical text")


def _canonical(value: Any) -> Any:
    if isinstance(value, Decimal):
        if not value.is_finite():
            raise ValueError("non-finite decimal is forbidden")
        return {"decimal": format(value, "f")}
    if isinstance(value, Enum):
        return value.value
    if isinstance(value, tuple):
        return [_canonical(item) for item in value]
    if isinstance(value, dict):
        return {str(key): _canonical(item) for key, item in sorted(value.items())}
    if value is None or isinstance(value, (bool, int, str)):
        return value
    raise TypeError(f"non-canonical authority value: {type(value).__qualname__}")


def stable_digest(value: Any) -> str:
    body = json.dumps(_canonical(value), ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    return sha256(body.encode("utf-8")).hexdigest()


@dataclass(frozen=True, slots=True, order=True)
class WorldRef:
    branch_id: str
    local_id: str
    epoch: int

    def __post_init__(self) -> None:
        _require_text("branch_id", self.branch_id)
        _require_text("local_id", self.local_id)
        if self.epoch < 0:
            raise ValueError("epoch cannot be negative")


@dataclass(frozen=True, slots=True)
class WorldVersion:
    version_id: str
    branch_id: str
    parents: tuple[str, ...]
    generation: int
    world_time: Decimal
    state_root: str
    delta_root: str
    cause_id: str
    ruleset_hash: str
    commit_sequence: int

    def __post_init__(self) -> None:
        for name in ("version_id", "branch_id", "state_root", "delta_root", "cause_id", "ruleset_hash"):
            _require_text(name, getattr(self, name))
        if self.generation < 0 or self.commit_sequence < 0:
            raise ValueError("generation and commit_sequence cannot be negative")
        if not self.world_time.is_finite():
            raise ValueError("world_time must be finite")


@dataclass(frozen=True, slots=True)
class Relation:
    relation_id: WorldRef
    kind: str
    endpoints: tuple[tuple[str, WorldRef], ...]
    scope_id: str
    provenance: str

    def __post_init__(self) -> None:
        if len(self.endpoints) < 2:
            raise ValueError("a relation needs at least two endpoints")
        roles = tuple(role for role, _ in self.endpoints)
        if len(set(roles)) != len(roles):
            raise ValueError("relation endpoint roles must be unique")


@dataclass(frozen=True, slots=True)
class PredicateReadToken:
    query_kind: str
    normalized_query: str
    index_partition: str
    dependency_epoch: int
    negative_keys: tuple[str, ...] = ()


class OperationKind(str, Enum):
    PUT = "put"
    DELETE = "delete"
    ADD_RELATION = "add_relation"
    REMOVE_RELATION = "remove_relation"


@dataclass(frozen=True, slots=True)
class Operation:
    """声明式补丁；值域受限，不接受 Python 回调。"""

    kind: OperationKind
    path: tuple[str, ...]
    value: Scalar | tuple[Scalar, ...] = None

    def __post_init__(self) -> None:
        if not self.path or any(not part for part in self.path):
            raise ValueError("operation path must be non-empty")
        _canonical(self.value)


@dataclass(frozen=True, slots=True)
class Intent:
    intent_id: str
    actor: WorldRef
    action: str
    based_on_version: str
    ability_source: WorldRef | None = None
    target_refs: tuple[WorldRef, ...] = ()
    proof_hints: tuple[str, ...] = ()


@dataclass(frozen=True, slots=True)
class Proposal:
    proposal_id: str
    intent_id: str
    reaction_key: tuple[str, ...]
    phase: int
    read_tokens: tuple[PredicateReadToken, ...]
    operations: tuple[Operation, ...]
    provenance: str

    def __post_init__(self) -> None:
        if self.phase < 0:
            raise ValueError("phase cannot be negative")
        if not self.reaction_key:
            raise ValueError("reaction_key is required")


@dataclass(frozen=True, slots=True)
class WorldSnapshot:
    """规范元组保存权威状态；更新总是创建新快照。"""

    version: WorldVersion
    facts: tuple[tuple[tuple[str, ...], Scalar | tuple[Scalar, ...]], ...] = ()
    relations: tuple[Relation, ...] = ()
    predicate_epochs: tuple[tuple[str, int], ...] = ()
    trace_roots: tuple[str, ...] = ()

    def __post_init__(self) -> None:
        paths = [path for path, _ in self.facts]
        if paths != sorted(paths) or len(paths) != len(set(paths)):
            raise ValueError("facts must be uniquely sorted by path")
        for _, value in self.facts:
            _canonical(value)
        relation_ids = [relation.relation_id for relation in self.relations]
        if relation_ids != sorted(relation_ids) or len(relation_ids) != len(set(relation_ids)):
            raise ValueError("relations must be uniquely sorted")

    @classmethod
    def genesis(cls, *, branch_id: str, ruleset_hash: str, world_time: Decimal = Decimal(0)) -> "WorldSnapshot":
        root = stable_digest((branch_id, ruleset_hash, ()))
        version = WorldVersion(
            version_id=stable_digest(("genesis", root)), branch_id=branch_id,
            parents=(), generation=0, world_time=world_time,
            state_root=root, delta_root=stable_digest(()), cause_id="GENESIS",
            ruleset_hash=ruleset_hash, commit_sequence=0,
        )
        return cls(version)

    def fact_map(self) -> dict[tuple[str, ...], Scalar | tuple[Scalar, ...]]:
        return dict(self.facts)

    def epoch_map(self) -> dict[str, int]:
        return dict(self.predicate_epochs)

