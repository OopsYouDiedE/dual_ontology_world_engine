from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True, slots=True)
class ValueTier:
    tier: int
    dimensions: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class PersonalityProfile:
    profile_id: str
    value_tiers: tuple[ValueTier, ...]
    taboos: tuple[str, ...] = ()
    commitments: tuple[str, ...] = ()
    habits: tuple[str, ...] = ()

    def __post_init__(self) -> None:
        dims = [d for t in self.value_tiers for d in t.dimensions]
        if len(dims) != len(set(dims)): raise ValueError("a dimension cannot occupy multiple value tiers")

