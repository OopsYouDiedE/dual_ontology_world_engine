from __future__ import annotations

from dataclasses import dataclass
from enum import Enum


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


class CoreError(Exception):
    pass


class ValidationError(CoreError):
    pass

