from __future__ import annotations
from dataclasses import dataclass
from enum import Enum
from ..foundation.codec import content_id
from .formula import FormulaExpr

class ClaimState(str,Enum): PENDING="pending"; DUE="due"; SETTLED="settled"; CANCELLED="cancelled"
@dataclass(frozen=True,slots=True)
class DeferredCostClaim:
    claim_id:str; branch_id:str; bearer_ref:str; beneficiary_or_sink_ref:str; dimension:str; amount_expr:FormulaExpr; due_event_ref:str; state:ClaimState; benefit_burden_pair_id:str; origin_rule_id:str
    @property
    def content_id(self)->str:return content_id((self.branch_id,self.bearer_ref,self.beneficiary_or_sink_ref,self.dimension,self.amount_expr,self.due_event_ref,self.state,self.benefit_burden_pair_id,self.origin_rule_id))
    def __post_init__(self):
        if self.claim_id!=self.content_id:raise ValueError("claim id not content-bound")
        if not self.benefit_burden_pair_id:raise ValueError("deferred burden must bind its benefit")

_LEGAL={ClaimState.PENDING:{ClaimState.DUE,ClaimState.CANCELLED},ClaimState.DUE:{ClaimState.SETTLED,ClaimState.CANCELLED},ClaimState.SETTLED:set(),ClaimState.CANCELLED:set()}
def transition_claim(claim:DeferredCostClaim,target:ClaimState,*,branch_id:str,event_ref:str)->DeferredCostClaim:
    if claim.branch_id!=branch_id:raise ValueError("claim branch mismatch")
    if target is claim.state:return claim
    if target not in _LEGAL[claim.state]:raise ValueError("illegal claim transition")
    if target is ClaimState.DUE and event_ref!=claim.due_event_ref:raise ValueError("wrong due event")
    values=(claim.branch_id,claim.bearer_ref,claim.beneficiary_or_sink_ref,claim.dimension,claim.amount_expr,claim.due_event_ref,target,claim.benefit_burden_pair_id,claim.origin_rule_id)
    return DeferredCostClaim(content_id(values),*values)
