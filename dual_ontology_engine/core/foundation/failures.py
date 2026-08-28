from __future__ import annotations

from dataclasses import dataclass
from enum import Enum

from .codec import content_id


class FailureKind(str, Enum):
    INVALID = "invalid"
    STALE = "stale"
    CONFLICT = "conflict"
    UNAUTHORIZED = "unauthorized"
    INSUFFICIENT_RESOURCE = "insufficient_resource"
    BUDGET_EXHAUSTED = "budget_exhausted"
    NON_CONVERGENT = "non_convergent"
    UNSAFE_MODEL = "unsafe_model"


@dataclass(frozen=True, slots=True)
class FailureCertificate:
    kind: FailureKind
    code: str
    base_version: str
    subject_ref: str | None = None
    dependency_refs: tuple[str, ...] = ()
    witness_refs: tuple[str, ...] = ()
    public_message: str = "request rejected"
    ruleset_hash: str = ""
    input_id: str = ""
    reaction_keys: tuple[str, ...] = ()
    axiom_ids: tuple[str, ...] = ()
    conflict_component_ids: tuple[str, ...] = ()
    budget_usage: tuple[tuple[str, int], ...] = ()
    state_digest: str = ""

    def __post_init__(self) -> None:
        if not isinstance(self.kind, FailureKind) or not self.code or not self.base_version:
            raise ValueError("failure certificate requires kind, code, and base version")
        if tuple(sorted(dict(self.budget_usage).items())) != self.budget_usage:
            raise ValueError("failure budget usage must be canonical and unique")

    @property
    def certificate_id(self) -> str:
        return content_id((self.kind,self.code,self.base_version,self.subject_ref,self.dependency_refs,self.witness_refs,self.public_message,self.ruleset_hash,self.input_id,self.reaction_keys,self.axiom_ids,self.conflict_component_ids,self.budget_usage,self.state_digest))


class CoreError(Exception):
    pass


class ValidationError(CoreError):
    pass
