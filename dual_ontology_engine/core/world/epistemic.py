from __future__ import annotations

from dataclasses import dataclass
from types import MappingProxyType
from typing import Mapping


@dataclass(frozen=True, slots=True)
class BeliefValue:
    proposition: str
    supports_true: tuple[str, ...] = ()
    supports_false: tuple[str, ...] = ()


@dataclass(frozen=True, slots=True)
class EpistemicView:
    """不携带客观对象引用的主体可见投影。"""

    subject_id: str
    version_id: str
    observations: Mapping[str, str]
    beliefs: Mapping[str, BeliefValue]
    affordance_ids: tuple[str, ...]

    def __post_init__(self) -> None:
        object.__setattr__(self, "observations", MappingProxyType(dict(self.observations)))
        object.__setattr__(self, "beliefs", MappingProxyType(dict(self.beliefs)))
        if len(set(self.affordance_ids))!=len(self.affordance_ids): raise ValueError("duplicate affordance")
