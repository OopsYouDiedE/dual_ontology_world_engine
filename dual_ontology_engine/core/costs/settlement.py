from __future__ import annotations
from dataclasses import dataclass
from ..transactions.proposals import ChargeGroup,Posting,make_charge_key
from .planes import CostPlane
from .quotes import CostQuote

@dataclass(frozen=True,slots=True)
class SettlementPlan:
    quote_id:str; groups:tuple[ChargeGroup,...]

def settlement_plan(quote:CostQuote,*,branch_id:str,base_version:str,current_epoch:int,active_model_version:str)->SettlementPlan:
    if quote.quote_plane is not CostPlane.EXECUTION or quote.projection_only:raise ValueError("projection/subjective quote cannot settle")
    if quote.branch_id!=branch_id or quote.base_version!=base_version:raise ValueError("stale or cross-branch quote")
    if quote.model_version!=active_model_version or current_epoch<quote.activation_epoch:raise ValueError("inactive cost model")
    groups=[]
    for component in quote.final_components:
        if component.sink_ref is None:raise ValueError("execution component requires sink")
        key=make_charge_key(branch_id,quote.quote_id,component.component_id,component.origin_rule)
        postings=(Posting(component.bearer_ref,component.dimension,-component.amount,component.origin_rule),Posting(component.sink_ref,component.dimension,component.amount,component.origin_rule))
        groups.append(ChargeGroup(key,postings))
    return SettlementPlan(quote.quote_id,tuple(groups))

