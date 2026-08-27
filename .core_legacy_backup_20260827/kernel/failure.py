"""稳定失败协议；拒绝和超限都以数据返回，而非泄漏半状态。"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum


class FailureStage(str, Enum):
    NORMALIZE = "normalize"
    READ_VALIDATE = "read_validate"
    REACTION = "reaction"
    CONFLICT = "conflict"
    INVARIANT = "invariant"
    COMMIT = "commit"


class FailureCode(str, Enum):
    STALE_VERSION = "stale_version"
    INVALID_REFERENCE = "invalid_reference"
    BUDGET_EXHAUSTED = "budget_exhausted"
    DUPLICATE_REACTION = "duplicate_reaction"
    WRITE_CONFLICT = "write_conflict"
    INVALID_OPERATION = "invalid_operation"
    INVARIANT_VIOLATION = "invariant_violation"


@dataclass(frozen=True, slots=True)
class FailureCertificate:
    """不包含隐藏世界数据、可安全持久化的失败证明。"""

    code: FailureCode
    stage: FailureStage
    intent_id: str
    base_version_id: str
    public_reason: str
    witness_keys: tuple[str, ...] = ()
    consumed_budget: tuple[tuple[str, int], ...] = ()

