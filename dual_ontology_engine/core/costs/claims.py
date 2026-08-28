from __future__ import annotations
from dataclasses import dataclass
from enum import Enum
from ..foundation.codec import content_id
from .formula import FormulaExpr

class ClaimState(str,Enum):
    PENDING="pending"; SECURED="secured"; UNSECURED="unsecured"; DUE="due"; SETTLED="settled"; BREACHED="breached"; CANCELLED="cancelled"


@dataclass(frozen=True,slots=True)
class BenefitBurdenPair:
    pair_id:str;branch_id:str;benefit_event_ref:str;bearer_ref:str;beneficiary_ref:str|None;origin_rule_id:str;provenance_id:str
    @property
    def content_id(self)->str:return content_id((self.branch_id,self.benefit_event_ref,self.bearer_ref,self.beneficiary_ref,self.origin_rule_id,self.provenance_id))
    def __post_init__(self):
        if self.pair_id!=self.content_id:raise ValueError("benefit/burden pair id not content-bound")

def make_benefit_burden_pair(*,branch_id:str,benefit_event_ref:str,bearer_ref:str,beneficiary_ref:str|None,origin_rule_id:str,provenance_id:str)->BenefitBurdenPair:
    values=(branch_id,benefit_event_ref,bearer_ref,beneficiary_ref,origin_rule_id,provenance_id)
    return BenefitBurdenPair(content_id(values),*values)
@dataclass(frozen=True,slots=True)
class DeferredCostClaim:
    claim_id:str; branch_id:str; bearer_ref:str; beneficiary_or_sink_ref:str|None; dimension:str; amount_expr:FormulaExpr; due_event_ref:str; state:ClaimState; benefit_burden_pair_id:str; origin_rule_id:str
    @property
    def content_id(self)->str:return content_id((self.branch_id,self.bearer_ref,self.beneficiary_or_sink_ref,self.dimension,self.amount_expr,self.due_event_ref,self.benefit_burden_pair_id,self.origin_rule_id))
    def __post_init__(self):
        if self.claim_id!=self.content_id:raise ValueError("claim id not content-bound")
        if not self.benefit_burden_pair_id:raise ValueError("deferred burden must bind its benefit")

def make_claim(*,branch_id:str,bearer_ref:str,beneficiary_or_sink_ref:str|None,dimension:str,amount_expr:FormulaExpr,due_event_ref:str,state:ClaimState,benefit_burden_pair_id:str,origin_rule_id:str)->DeferredCostClaim:
    identity_values=(branch_id,bearer_ref,beneficiary_or_sink_ref,dimension,amount_expr,due_event_ref,benefit_burden_pair_id,origin_rule_id)
    return DeferredCostClaim(content_id(identity_values),branch_id,bearer_ref,beneficiary_or_sink_ref,dimension,amount_expr,due_event_ref,state,benefit_burden_pair_id,origin_rule_id)


_LEGAL={
    ClaimState.PENDING:{ClaimState.SECURED,ClaimState.UNSECURED,ClaimState.DUE,ClaimState.CANCELLED},
    ClaimState.SECURED:{ClaimState.DUE,ClaimState.CANCELLED},
    ClaimState.UNSECURED:{ClaimState.DUE,ClaimState.CANCELLED},
    ClaimState.DUE:{ClaimState.SETTLED,ClaimState.BREACHED,ClaimState.CANCELLED},
    ClaimState.BREACHED:{ClaimState.SETTLED,ClaimState.CANCELLED},
    ClaimState.SETTLED:set(),
    ClaimState.CANCELLED:set(),
}
def transition_claim(claim:DeferredCostClaim,target:ClaimState,*,branch_id:str,event_ref:str)->DeferredCostClaim:
    if claim.branch_id!=branch_id:raise ValueError("claim branch mismatch")
    if target is claim.state:return claim
    if target not in _LEGAL[claim.state]:raise ValueError("illegal claim transition")
    if target is ClaimState.DUE and event_ref!=claim.due_event_ref:raise ValueError("wrong due event")
    return make_claim(branch_id=claim.branch_id,bearer_ref=claim.bearer_ref,beneficiary_or_sink_ref=claim.beneficiary_or_sink_ref,dimension=claim.dimension,amount_expr=claim.amount_expr,due_event_ref=claim.due_event_ref,state=target,benefit_burden_pair_id=claim.benefit_burden_pair_id,origin_rule_id=claim.origin_rule_id)
