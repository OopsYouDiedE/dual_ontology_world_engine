"""每批次独立计量的有限资源预算。"""

from __future__ import annotations

from dataclasses import dataclass, field


@dataclass(frozen=True, slots=True)
class BudgetProfile:
    instructions: int = 10_000
    agenda_nodes: int = 1_000
    proposals: int = 256
    writes: int = 2_048
    trace_nodes: int = 2_048
    integer_bits: int = 4_096

    def __post_init__(self) -> None:
        if any(value <= 0 for value in (
            self.instructions, self.agenda_nodes, self.proposals,
            self.writes, self.trace_nodes, self.integer_bits,
        )):
            raise ValueError("all budget limits must be positive")


class BudgetExceeded(RuntimeError):
    def __init__(self, dimension: str) -> None:
        super().__init__(dimension)
        self.dimension = dimension


@dataclass(slots=True)
class BudgetMeter:
    profile: BudgetProfile
    _used: dict[str, int] = field(default_factory=dict, init=False, repr=False)

    def charge(self, dimension: str, amount: int = 1) -> None:
        if amount < 0:
            raise ValueError("budget charges cannot be negative")
        if not hasattr(self.profile, dimension):
            raise ValueError(f"unknown budget dimension: {dimension}")
        used = self._used.get(dimension, 0) + amount
        if used > getattr(self.profile, dimension):
            raise BudgetExceeded(dimension)
        self._used[dimension] = used

    @property
    def consumed(self) -> tuple[tuple[str, int], ...]:
        return tuple(sorted(self._used.items()))

