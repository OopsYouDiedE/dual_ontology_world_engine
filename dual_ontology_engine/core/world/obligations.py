from __future__ import annotations
from dataclasses import dataclass
from enum import Enum

class ObligationState(str, Enum): PENDING="pending"; SATISFIED="satisfied"; VIOLATED="violated"; REMEDIED="remedied"; CANCELLED="cancelled"

@dataclass(frozen=True, slots=True)
class Obligation:
    obligation_id: str; bearer_ref: str; action_kind: str; state: ObligationState; provenance_id: str; opposed_obligation_id: str | None = None

_LEGAL={ObligationState.PENDING:{ObligationState.SATISFIED,ObligationState.VIOLATED,ObligationState.CANCELLED},ObligationState.VIOLATED:{ObligationState.REMEDIED},ObligationState.SATISFIED:set(),ObligationState.REMEDIED:set(),ObligationState.CANCELLED:set()}

def transition_obligation(value: Obligation, target: ObligationState) -> Obligation:
    if target is value.state: return value
    if target not in _LEGAL[value.state]: raise ValueError("illegal obligation transition")
    return Obligation(value.obligation_id,value.bearer_ref,value.action_kind,target,value.provenance_id,value.opposed_obligation_id)
