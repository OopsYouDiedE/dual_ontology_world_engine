from __future__ import annotations
from dataclasses import dataclass
from enum import Enum
from .entities import IdentityFacet
from ..foundation.codec import content_id

class FacetTransfer(str, Enum):
    MOVE="move"; COPY="copy"; DROP="drop"; DERIVE="derive"

@dataclass(frozen=True, slots=True)
class FacetInstruction:
    facet_name: str; mode: FacetTransfer; derived_value: str | None = None
    def __post_init__(self) -> None:
        if (self.mode is FacetTransfer.DERIVE) != (self.derived_value is not None): raise ValueError("DERIVE alone requires a derived value")

@dataclass(frozen=True, slots=True)
class IdentityTransitionPlan:
    plan_id: str; source_subject: str; source_epoch: int; destination_subjects: tuple[str, ...]; instructions: tuple[FacetInstruction, ...]; provenance_id: str
    @property
    def content_id(self)->str:return content_id((self.source_subject,self.source_epoch,self.destination_subjects,self.instructions,self.provenance_id))
    def __post_init__(self):
        if self.plan_id!=self.content_id:raise ValueError("identity plan id not content-bound")
    def validate(self, facets: tuple[IdentityFacet, ...]) -> None:
        if not self.destination_subjects or len(set(self.destination_subjects)) != len(self.destination_subjects): raise ValueError("identity transition destinations invalid")
        by_name={facet.name:facet for facet in facets}; instructions={item.facet_name:item for item in self.instructions}
        if len(instructions)!=len(self.instructions) or set(instructions)!=set(by_name): raise ValueError("every identity facet requires exactly one instruction")
        for name, facet in by_name.items():
            mode=instructions[name].mode
            if facet.exclusive and mode is FacetTransfer.COPY: raise ValueError("exclusive identity facet cannot be copied")
            if facet.exclusive and mode is FacetTransfer.MOVE and len(self.destination_subjects)!=1: raise ValueError("exclusive identity facet has exactly one successor")
