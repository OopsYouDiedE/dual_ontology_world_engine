from __future__ import annotations
from dataclasses import dataclass
from ..foundation.codec import content_id
from ..transactions.commit import CommitTrace
from .agency import AgencyFrame
from .consequences import Consequence,validate_consequences

@dataclass(frozen=True,slots=True)
class NarrativeAtom:
    atom_id:str;committed_trace:CommitTrace;agency:AgencyFrame;intent_ref:str;consequences:tuple[Consequence,...];cost_atom_refs:tuple[str,...];witness_refs:tuple[str,...];objective_cause_refs:tuple[str,...];decision_proof_ref:str|None;relationship_history_refs:tuple[str,...]
    @property
    def content_id(self)->str:return content_id((self.committed_trace,self.agency,self.intent_ref,self.consequences,self.cost_atom_refs,self.witness_refs,self.objective_cause_refs,self.decision_proof_ref,self.relationship_history_refs))
    def __post_init__(self):
        if self.atom_id!=self.content_id:raise ValueError("narrative atom not derived from committed content")
        validate_consequences(self.consequences)
        if not self.objective_cause_refs:raise ValueError("narrative cannot erase objective causality")
        if self.agency.kind.value=="voluntary" and self.decision_proof_ref is None:raise ValueError("voluntary attribution requires decision proof")

@dataclass(frozen=True,slots=True)
class PovNarrativeAtom:
    objective_atom_id:str;actor_label:str;visible_consequences:tuple[Consequence,...];visible_cost_refs:tuple[str,...];visible_cause_refs:tuple[str,...];hidden_cause_count:int

def project_narrative(atom:NarrativeAtom,*,visible_refs:frozenset[str],actor_label:str)->PovNarrativeAtom:
    visible_causes=tuple(ref for ref in atom.objective_cause_refs if ref in visible_refs)
    return PovNarrativeAtom(atom.atom_id,actor_label,atom.consequences,tuple(ref for ref in atom.cost_atom_refs if ref in visible_refs),visible_causes,len(atom.objective_cause_refs)-len(visible_causes))

