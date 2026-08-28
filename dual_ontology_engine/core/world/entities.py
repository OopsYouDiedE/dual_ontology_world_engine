from __future__ import annotations

from dataclasses import dataclass

from ..foundation.ids import WorldRef


@dataclass(frozen=True, slots=True)
class Entity:
    ref: WorldRef
    kind: str
    facets: tuple[tuple[str, str], ...] = ()
    active: bool = True

    def __post_init__(self) -> None:
        if not self.kind or len(dict(self.facets)) != len(self.facets):
            raise ValueError("entity kind must exist and facet keys must be unique")


@dataclass(frozen=True, slots=True)
class Subject:
    ref: WorldRef
    identity_epoch: int
    personality_version: str
    controlled_entities: tuple[WorldRef, ...] = ()


@dataclass(frozen=True, slots=True)
class PermissionGrant:
    grant_id: str
    actor_ref: str
    action: str
    target_prefix: str
    identity_epoch: int
    provenance_id: str
    permission_epoch: int = 0

    def __post_init__(self) -> None:
        if self.identity_epoch<0 or self.permission_epoch<0:raise ValueError("permission epochs cannot be negative")


@dataclass(frozen=True, slots=True)
class PermissionRevocation:
    grant_id: str
    expected_permission_epoch: int

    def __post_init__(self) -> None:
        if not self.grant_id or self.expected_permission_epoch<0:raise ValueError("invalid permission revocation")


@dataclass(frozen=True, slots=True)
class IdentityFacet:
    name: str
    value: str
    exclusive: bool


@dataclass(frozen=True, slots=True)
class Fact:
    fact_id: str
    subject: WorldRef
    predicate: str
    value: str
    provenance_id: str


@dataclass(frozen=True, slots=True)
class EntityDeletion:
    """实体删除命令；事实级联必须由调用规则显式声明。"""

    ref: WorldRef
    cascade_facts: bool = False
