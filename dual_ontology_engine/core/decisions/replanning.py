from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from typing import Callable

from ..foundation.codec import content_id


class ReplanStatus(str, Enum):
    STABLE = "stable"
    REPLAN_EXHAUSTED = "replan_exhausted"
    SAFE_FALLBACK = "safe_fallback"


@dataclass(frozen=True, slots=True)
class PlanCandidate:
    path_id: str
    quote_id: str
    dependency_snapshot_id: str

    def __post_init__(self) -> None:
        if not self.path_id or not self.quote_id or not self.dependency_snapshot_id:
            raise ValueError("plan candidate requires path, quote, and dependency snapshot")


@dataclass(frozen=True, slots=True)
class ReplanAttempt:
    attempt_index: int
    observed_dependency_snapshot_id: str
    candidate: PlanCandidate
    validated_dependency_snapshot_id: str

    @property
    def stale(self) -> bool:
        return self.observed_dependency_snapshot_id != self.validated_dependency_snapshot_id


@dataclass(frozen=True, slots=True)
class ReplanPolicy:
    max_replans: int
    safe_fallback_path_id: str | None = None

    def __post_init__(self) -> None:
        if self.max_replans < 0:
            raise ValueError("replan budget cannot be negative")
        if self.safe_fallback_path_id == "":
            raise ValueError("safe fallback path cannot be empty")


@dataclass(frozen=True, slots=True)
class ReplanResult:
    status: ReplanStatus
    candidate: PlanCandidate | None
    attempts: tuple[ReplanAttempt, ...]
    failure_certificate_id: str | None


def bounded_replan(
    *,
    read_dependency_snapshot: Callable[[], str],
    plan_once: Callable[[str], PlanCandidate],
    policy: ReplanPolicy,
) -> ReplanResult:
    """在提交前重验依赖；持续干扰只会耗尽有限预算，不会递归活锁。"""

    attempts: list[ReplanAttempt] = []
    for attempt_index in range(policy.max_replans + 1):
        observed = read_dependency_snapshot()
        if not observed:
            raise ValueError("empty dependency snapshot")
        candidate = plan_once(observed)
        if candidate.dependency_snapshot_id != observed:
            raise ValueError("planner returned a candidate bound to another snapshot")
        validated = read_dependency_snapshot()
        attempt = ReplanAttempt(attempt_index, observed, candidate, validated)
        attempts.append(attempt)
        if not attempt.stale:
            return ReplanResult(ReplanStatus.STABLE, candidate, tuple(attempts), None)

    evidence = tuple(
        (item.attempt_index, item.observed_dependency_snapshot_id, item.validated_dependency_snapshot_id)
        for item in attempts
    )
    certificate = content_id(("REPLAN_EXHAUSTED", policy, evidence))
    if policy.safe_fallback_path_id is not None:
        dependency = attempts[-1].validated_dependency_snapshot_id
        fallback = PlanCandidate(
            policy.safe_fallback_path_id,
            content_id(("safe-fallback", policy.safe_fallback_path_id, dependency)),
            dependency,
        )
        return ReplanResult(ReplanStatus.SAFE_FALLBACK, fallback, tuple(attempts), certificate)
    return ReplanResult(ReplanStatus.REPLAN_EXHAUSTED, None, tuple(attempts), certificate)
