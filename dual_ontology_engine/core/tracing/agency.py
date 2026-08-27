from __future__ import annotations
from dataclasses import dataclass
from enum import Enum

class AgencyKind(str,Enum):
    VOLUNTARY="voluntary";COERCED="coerced";CONTROLLED="controlled";AUTOMATIC="automatic";ACCIDENTAL="accidental";OMISSION="omission";NATURAL="natural"

@dataclass(frozen=True,slots=True)
class AgencyFrame:
    kind:AgencyKind;actor_ref:str|None;controller_ref:str|None;available_alternative_refs:tuple[str,...];decision_proof_ref:str|None;cause_refs:tuple[str,...]
    def __post_init__(self):
        if self.kind is AgencyKind.CONTROLLED and not self.controller_ref:raise ValueError("controlled event requires controller")
        if self.kind in {AgencyKind.VOLUNTARY,AgencyKind.COERCED,AgencyKind.OMISSION} and not self.actor_ref:raise ValueError("agentive event requires actor")
        if self.kind is AgencyKind.OMISSION and not self.available_alternative_refs:raise ValueError("omission requires an available alternative")
        if not self.cause_refs:raise ValueError("agency frame requires committed causes")

