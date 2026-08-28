from __future__ import annotations

from dataclasses import dataclass
from enum import Enum

from ..foundation.ids import WorldRef


@dataclass(frozen=True, slots=True)
class RelationEndpoint:
    role: str
    ref: WorldRef


class EndpointDeletionPolicy(str, Enum):
    REJECT = "reject"
    CASCADE_RELATION = "cascade_relation"


@dataclass(frozen=True, slots=True)
class Relation:
    relation_id: WorldRef
    kind: str
    endpoints: tuple[RelationEndpoint, ...]
    scope_id: str
    valid_from: str
    valid_until: str | None
    provenance_id: str
    endpoint_deletion_policy: EndpointDeletionPolicy = EndpointDeletionPolicy.REJECT

    def __post_init__(self) -> None:
        roles = [endpoint.role for endpoint in self.endpoints]
        if not self.kind or len(self.endpoints) < 2 or len(roles) != len(set(roles)):
            raise ValueError("relation requires distinct endpoint roles")
        if not isinstance(self.endpoint_deletion_policy, EndpointDeletionPolicy):
            raise TypeError("closed endpoint deletion policy required")
