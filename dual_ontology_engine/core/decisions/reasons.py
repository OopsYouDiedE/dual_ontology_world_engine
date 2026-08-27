from __future__ import annotations

from dataclasses import dataclass
from enum import Enum


class ReasonPolarity(Enum):
    SUPPORT = 1
    OPPOSE = -1


@dataclass(frozen=True, slots=True)
class Reason:
    reason_id: str
    path_id: str
    dimension: str
    polarity: ReasonPolarity
    rank: int
    provenance_ids: tuple[str, ...]
    defeated_by: tuple[str, ...] = ()


@dataclass(frozen=True, slots=True)
class ReasonAcceptance:
    accepted: tuple[Reason, ...]
    rejected: tuple[Reason, ...]
    undecided: tuple[Reason, ...]


def grounded_acceptance(reasons: tuple[Reason, ...]) -> ReasonAcceptance:
    """计算有限论证框架的 grounded 最小不动点。"""

    by_id = {reason.reason_id: reason for reason in reasons}
    if len(by_id) != len(reasons):
        raise ValueError("duplicate reason id")
    unknown = tuple(
        sorted(
            {
                attacker
                for reason in reasons
                for attacker in reason.defeated_by
                if attacker not in by_id
            }
        )
    )
    if unknown:
        raise ValueError(f"unknown reason attacker:{','.join(unknown)}")

    accepted: set[str] = set()
    rejected: set[str] = set()
    changed = True
    while changed:
        changed = False
        for reason_id in sorted(by_id):
            if reason_id in accepted or reason_id in rejected:
                continue
            attackers = set(by_id[reason_id].defeated_by)
            if attackers.issubset(rejected):
                accepted.add(reason_id)
                changed = True
            elif attackers & accepted:
                rejected.add(reason_id)
                changed = True

    ordered = tuple(sorted(reasons, key=lambda reason: reason.reason_id))
    return ReasonAcceptance(
        tuple(reason for reason in ordered if reason.reason_id in accepted),
        tuple(reason for reason in ordered if reason.reason_id in rejected),
        tuple(
            reason
            for reason in ordered
            if reason.reason_id not in accepted and reason.reason_id not in rejected
        ),
    )


def undefeated(reasons: tuple[Reason, ...]) -> tuple[Reason, ...]:
    """兼容入口；返回 grounded 语义下被接受的理由。"""

    return grounded_acceptance(reasons).accepted
