from __future__ import annotations
from dataclasses import dataclass,replace
from fractions import Fraction
from ..foundation.codec import content_id
from ..foundation.numbers import add,mul,checked
from .atoms import CostComponent
from .modifiers import CostModifier,ModifierOp,normalize_modifiers
from .planes import CostPlane

@dataclass(frozen=True,slots=True)
class CostQuote:
    quote_id:str; branch_id:str; base_version:str; model_version:str; activation_epoch:int; subject_id:str; epistemic_version:str|None; personality_version:str|None; path_id:str; quote_plane:CostPlane; final_components:tuple[CostComponent,...]; applied_modifier_ids:tuple[str,...]; proof_roots:tuple[str,...]; predicate_token_ids:tuple[str,...]; projection_only:bool
    @property
    def content_id(self)->str:return content_id((self.branch_id,self.base_version,self.model_version,self.activation_epoch,self.subject_id,self.epistemic_version,self.personality_version,self.path_id,self.quote_plane,self.final_components,self.applied_modifier_ids,self.proof_roots,self.predicate_token_ids,self.projection_only))
    def __post_init__(self):
        if self.quote_id!=self.content_id:raise ValueError("quote id not content-bound")
        if not self.final_components or any(c.plane is not self.quote_plane for c in self.final_components):raise ValueError("quote component plane mismatch/empty")
        if self.quote_plane is not CostPlane.EXECUTION and (self.epistemic_version is None or self.personality_version is None):raise ValueError("subjective quote lacks epistemic/personality binding")

def quote_cost(*,branch_id:str,base_version:str,model_version:str,activation_epoch:int,subject_id:str,epistemic_version:str|None,personality_version:str|None,path_id:str,plane:CostPlane,components:tuple[CostComponent,...],modifiers:tuple[CostModifier,...],proof_roots:tuple[str,...],predicate_token_ids:tuple[str,...]=(),projection_only:bool=False,current_epoch:int)->CostQuote:
    if current_epoch<activation_epoch:raise ValueError("cost model not active")
    applicable=normalize_modifiers(tuple(m for m in modifiers if m.plane is plane),branch_id=branch_id,epoch=current_epoch)
    result=[];applied=set()
    for component in components:
        if component.plane is not plane:raise ValueError("cross-plane component")
        if component.bearer_ref!=subject_id and plane is not CostPlane.EXECUTION:raise ValueError("subjective quote bearer mismatch")
        selected=[m for m in applicable if m.dimension==component.dimension and m.selector_id in {"*",path_id,component.component_id}]
        if any(m.authority_domain!=component.authority_domain for m in selected):raise ValueError("modifier authority mismatch")
        replacements=[m for m in selected if m.operation is ModifierOp.REPLACE]
        if len(replacements)>1:raise ValueError("multiple replacements")
        current=replacements[0].value if replacements else component.amount
        for item in (m for m in selected if m.operation is ModifierOp.MULTIPLY):current=mul(current,item.value)
        for item in (m for m in selected if m.operation is ModifierOp.ADD):current=add(current,item.value)
        floors=[m.value for m in selected if m.operation is ModifierOp.FLOOR];caps=[m.value for m in selected if m.operation is ModifierOp.CAP]
        lower=max(floors,default=Fraction());upper=min(caps) if caps else None
        if upper is not None and lower>upper:raise ValueError("floor exceeds cap")
        current=checked(max(lower,current));current=checked(min(upper,current)) if upper is not None else current
        if current<0:raise ValueError("negative final cost")
        applied.update(m.modifier_id for m in selected);result.append(replace(component,amount=current))
    values=(branch_id,base_version,model_version,activation_epoch,subject_id,epistemic_version,personality_version,path_id,plane,tuple(result),tuple(sorted(applied)),proof_roots,predicate_token_ids,projection_only)
    return CostQuote(content_id(values),*values)

