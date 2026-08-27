from __future__ import annotations

from dataclasses import dataclass, replace

from .failures import CoreError


class BudgetExceeded(CoreError):
    pass


@dataclass(frozen=True, slots=True)
class BudgetProfile:
    instructions: int = 10_000
    agenda_nodes: int = 1_000
    proposals: int = 1_000
    created_objects: int = 128
    trace_nodes: int = 2_000
    causal_depth: int = 64
    reserve: int = 32

    def __post_init__(self) -> None:
        if min(self.instructions, self.agenda_nodes, self.proposals, self.created_objects, self.trace_nodes, self.causal_depth, self.reserve) < 0:
            raise ValueError("budgets cannot be negative")
        if self.reserve > self.instructions:
            raise ValueError("failure reserve exceeds instruction budget")


@dataclass(frozen=True, slots=True)
class BudgetMeter:
    profile: BudgetProfile
    used_instructions: int = 0
    used_nodes: int = 0
    used_proposals: int = 0

    def charge(self, *, instructions: int = 0, nodes: int = 0, proposals: int = 0) -> "BudgetMeter":
        if min(instructions, nodes, proposals) < 0: raise ValueError("negative budget charge")
        updated = replace(self, used_instructions=self.used_instructions + instructions, used_nodes=self.used_nodes + nodes, used_proposals=self.used_proposals + proposals)
        if updated.used_instructions > self.profile.instructions - self.profile.reserve:
            raise BudgetExceeded("instruction budget exhausted")
        if updated.used_nodes > self.profile.agenda_nodes or updated.used_proposals > self.profile.proposals:
            raise BudgetExceeded("structural budget exhausted")
        return updated
