from __future__ import annotations
from dataclasses import dataclass
from fractions import Fraction

@dataclass(frozen=True,slots=True)
class PhysicalConsequence:entity_ref:str;state_kind:str;magnitude:Fraction
@dataclass(frozen=True,slots=True)
class RelationshipConsequence:relation_ref:str;history_event_ref:str;interpretation_kind:str
@dataclass(frozen=True,slots=True)
class CognitiveConsequence:subject_ref:str;artifact_ref:str;interpretation_kind:str
@dataclass(frozen=True,slots=True)
class IdentityConsequence:transition_plan_ref:str;successor_refs:tuple[str,...]
@dataclass(frozen=True,slots=True)
class ObligationConsequence:obligation_ref:str;transition_kind:str
Consequence=PhysicalConsequence|RelationshipConsequence|CognitiveConsequence|IdentityConsequence|ObligationConsequence
_CLOSED=(PhysicalConsequence,RelationshipConsequence,CognitiveConsequence,IdentityConsequence,ObligationConsequence)
def validate_consequences(values:tuple[Consequence,...])->None:
    if any(type(value) not in _CLOSED for value in values):raise TypeError("unknown/callback consequence forbidden")

