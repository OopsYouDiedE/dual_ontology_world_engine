from __future__ import annotations

from dataclasses import dataclass
from fractions import Fraction

from ..foundation.codec import content_id


@dataclass(frozen=True, slots=True)
class PaymentAlternative:
    alternative_id: str
    amounts: tuple[tuple[str, Fraction], ...]
    burden_dimensions: tuple[tuple[str, Fraction], ...] = ()

    def __post_init__(self) -> None:
        keys = [key for key, _ in self.amounts]
        burden_keys = [key for key, _ in self.burden_dimensions]
        if len(keys) != len(set(keys)) or len(burden_keys) != len(set(burden_keys)):
            raise ValueError("duplicate payment dimension")
        if any(
            not isinstance(value, Fraction) or value < 0
            for _, value in self.amounts + self.burden_dimensions
        ):
            raise ValueError("payment alternatives require nonnegative exact costs")


@dataclass(frozen=True, slots=True)
class SolverCertificate:
    certificate_id: str
    problem_id: str
    feasible_ids: tuple[str, ...]
    frontier_ids: tuple[str, ...]
    infeasible_witnesses: tuple[tuple[str, str, Fraction, Fraction], ...]
    dominance_witnesses: tuple[tuple[str, str], ...]

    @property
    def content_id(self) -> str:
        return content_id(
            (
                self.problem_id,
                self.feasible_ids,
                self.frontier_ids,
                self.infeasible_witnesses,
                self.dominance_witnesses,
            )
        )

    def __post_init__(self) -> None:
        if self.certificate_id != self.content_id:
            raise ValueError("solver certificate is not content-bound")


@dataclass(frozen=True, slots=True)
class PaymentSolution:
    frontier: tuple[PaymentAlternative, ...]
    certificate: SolverCertificate


def _vector(item: PaymentAlternative) -> dict[str, Fraction]:
    result = dict(item.amounts)
    for key, value in item.burden_dimensions:
        result[f"burden:{key}"] = value
    return result


def _dominates(left: PaymentAlternative, right: PaymentAlternative) -> bool:
    left_values, right_values = _vector(left), _vector(right)
    dimensions = set(left_values) | set(right_values)
    return all(
        left_values.get(key, Fraction()) <= right_values.get(key, Fraction())
        for key in dimensions
    ) and any(
        left_values.get(key, Fraction()) < right_values.get(key, Fraction())
        for key in dimensions
    )


def solve_bounded_payment_frontier(
    alternatives: tuple[PaymentAlternative, ...],
    *,
    available: dict[str, Fraction],
    reserve: dict[str, Fraction] | None = None,
    max_alternatives: int = 64,
) -> PaymentSolution:
    """枚举声明的有限支付域，返回 Pareto 解及可独立复核的见证。"""

    if not alternatives or len(alternatives) > max_alternatives:
        raise ValueError("payment alternative domain empty or over budget")
    if len({item.alternative_id for item in alternatives}) != len(alternatives):
        raise ValueError("duplicate payment alternative id")
    reserve = dict(reserve or {})
    if any(
        not isinstance(value, Fraction) or value < 0
        for value in tuple(available.values()) + tuple(reserve.values())
    ):
        raise ValueError("availability and reserve require nonnegative Fraction")

    ordered = tuple(sorted(alternatives, key=lambda item: item.alternative_id))
    feasible = []
    infeasible_witnesses = []
    for item in ordered:
        failed = False
        for kind, value in item.amounts:
            capacity = available.get(kind, Fraction())
            required_reserve = reserve.get(kind, Fraction())
            if capacity - value < required_reserve:
                infeasible_witnesses.append(
                    (item.alternative_id, kind, value + required_reserve, capacity)
                )
                failed = True
        if not failed:
            feasible.append(item)

    dominance_witnesses = []
    frontier = []
    for item in feasible:
        dominators = [
            other
            for other in feasible
            if other is not item and _dominates(other, item)
        ]
        if dominators:
            witness = min(dominators, key=lambda other: other.alternative_id)
            dominance_witnesses.append((item.alternative_id, witness.alternative_id))
        else:
            frontier.append(item)

    problem_id = content_id(
        (
            ordered,
            tuple(sorted(available.items())),
            tuple(sorted(reserve.items())),
            max_alternatives,
        )
    )
    certificate_values = (
        problem_id,
        tuple(item.alternative_id for item in feasible),
        tuple(item.alternative_id for item in frontier),
        tuple(sorted(infeasible_witnesses)),
        tuple(sorted(dominance_witnesses)),
    )
    certificate = SolverCertificate(
        content_id(certificate_values), *certificate_values
    )
    return PaymentSolution(tuple(frontier), certificate)


def verify_solver_certificate(
    alternatives: tuple[PaymentAlternative, ...],
    *,
    available: dict[str, Fraction],
    reserve: dict[str, Fraction] | None,
    certificate: SolverCertificate,
    max_alternatives: int = 64,
) -> bool:
    """独立重算有限问题；证书内容或任一见证被篡改都会失败。"""

    try:
        expected = solve_bounded_payment_frontier(
            alternatives,
            available=available,
            reserve=reserve,
            max_alternatives=max_alternatives,
        ).certificate
    except (TypeError, ValueError):
        return False
    return expected == certificate


def bounded_payment_frontier(
    alternatives: tuple[PaymentAlternative, ...],
    *,
    available: dict[str, Fraction],
    max_alternatives: int = 64,
) -> tuple[PaymentAlternative, ...]:
    """兼容入口；正式结算应保留 ``solve_*`` 返回的证书。"""

    return solve_bounded_payment_frontier(
        alternatives,
        available=available,
        max_alternatives=max_alternatives,
    ).frontier
