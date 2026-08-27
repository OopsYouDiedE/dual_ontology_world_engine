from __future__ import annotations

from dataclasses import dataclass
from fractions import Fraction


@dataclass(frozen=True, slots=True)
class PaymentAlternative:
    alternative_id: str
    amounts: tuple[tuple[str, Fraction], ...]
    burden_dimensions: tuple[tuple[str, Fraction], ...] = ()

    def __post_init__(self) -> None:
        if any(value < 0 for _, value in self.amounts + self.burden_dimensions):
            raise ValueError("payment alternatives cannot contain negative costs")


def bounded_payment_frontier(alternatives: tuple[PaymentAlternative, ...], *, available: dict[str, Fraction], max_alternatives: int = 64) -> tuple[PaymentAlternative, ...]:
    """返回可支付且不被同类代价严格支配的有限集合。"""

    if len(alternatives) > max_alternatives:
        raise ValueError("payment alternative budget exceeded")
    feasible = [item for item in alternatives if all(value <= available.get(kind, Fraction()) for kind, value in item.amounts)]
    def vector(item: PaymentAlternative) -> dict[str, Fraction]:
        result = dict(item.amounts)
        for key, value in item.burden_dimensions: result[f"burden:{key}"] = value
        return result
    def dominates(left: PaymentAlternative, right: PaymentAlternative) -> bool:
        lv, rv = vector(left), vector(right); dimensions = set(lv) | set(rv)
        return all(lv.get(d, Fraction()) <= rv.get(d, Fraction()) for d in dimensions) and any(lv.get(d, Fraction()) < rv.get(d, Fraction()) for d in dimensions)
    return tuple(sorted((item for item in feasible if not any(dominates(other, item) for other in feasible if other is not item)), key=lambda item: item.alternative_id))

