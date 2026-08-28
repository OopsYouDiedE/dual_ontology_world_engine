from __future__ import annotations

from dataclasses import dataclass
from fractions import Fraction

from .planes import CostPlane, Reversibility, SettlementTiming
from ..foundation.codec import content_id


@dataclass(frozen=True, slots=True)
class CostAtom:
    atom_id: str
    kind: str
    plane: CostPlane
    dimension: str
    bearer_ref: str
    beneficiary_refs: tuple[str, ...]
    imposed_by_refs: tuple[str, ...]
    onset: str
    duration: str
    magnitude: Fraction
    reversibility: Reversibility
    observability_id: str
    causal_event_id: str
    allocation_rule_id: str
    settlement_timing: SettlementTiming
    is_projection: bool = False
    is_settlement: bool = False

    def __post_init__(self) -> None:
        if not all((self.atom_id, self.kind, self.dimension, self.bearer_ref, self.causal_event_id, self.allocation_rule_id)):
            raise ValueError("incomplete cost atom")
        if self.magnitude < 0:
            raise ValueError("cost magnitude must be non-negative; rewards are effects")
        if self.plane is not CostPlane.EXECUTION and self.is_settlement:
            raise ValueError("only execution atoms can settle")
        if self.is_projection and self.is_settlement:
            raise ValueError("a projection cannot also be a settlement")
        if not isinstance(self.magnitude,Fraction): raise TypeError("cost magnitude requires Fraction")
        expected=content_id((self.kind,self.plane,self.dimension,self.bearer_ref,self.beneficiary_refs,self.imposed_by_refs,self.onset,self.duration,self.magnitude,self.reversibility,self.observability_id,self.causal_event_id,self.allocation_rule_id,self.settlement_timing,self.is_projection,self.is_settlement))
        if self.atom_id!=expected: raise ValueError("cost atom id not content-bound")


def make_cost_atom(*,kind:str,plane:CostPlane,dimension:str,bearer_ref:str,beneficiary_refs:tuple[str,...],imposed_by_refs:tuple[str,...],onset:str,duration:str,magnitude:Fraction,reversibility:Reversibility,observability_id:str,causal_event_id:str,allocation_rule_id:str,settlement_timing:SettlementTiming,is_projection:bool=False,is_settlement:bool=False)->CostAtom:
    values=(kind,plane,dimension,bearer_ref,beneficiary_refs,imposed_by_refs,onset,duration,magnitude,reversibility,observability_id,causal_event_id,allocation_rule_id,settlement_timing,is_projection,is_settlement)
    return CostAtom(content_id(values),*values)


@dataclass(frozen=True, slots=True)
class CostComponent:
    component_id: str
    plane: CostPlane
    dimension: str
    amount: Fraction
    bearer_ref: str
    sink_ref: str | None
    authority_domain: str
    origin_rule: str
    algebra_id: str = "nonnegative_additive"

    def __post_init__(self) -> None:
        if not isinstance(self.amount,Fraction): raise TypeError("component amount requires Fraction")
        if self.amount < 0:
            raise ValueError("cost component cannot encode rewards")
        expected=content_id((self.plane,self.dimension,self.amount,self.bearer_ref,self.sink_ref,self.authority_domain,self.origin_rule,self.algebra_id))
        if self.component_id!=expected: raise ValueError("component id not content-bound")


def make_component(*,plane:CostPlane,dimension:str,amount:Fraction,bearer_ref:str,sink_ref:str|None,authority_domain:str,origin_rule:str,algebra_id:str="nonnegative_additive")->CostComponent:
    values=(plane,dimension,amount,bearer_ref,sink_ref,authority_domain,origin_rule,algebra_id)
    return CostComponent(content_id(values),*values)
