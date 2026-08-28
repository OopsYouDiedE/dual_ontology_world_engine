from __future__ import annotations
from dataclasses import dataclass
from fractions import Fraction
from ..foundation.codec import content_id
from ..foundation.numbers import add,mul,checked
from .atoms import CostComponent,make_component
from .modifiers import CostModifier,ModifierOp,application_key,normalize_modifiers
from .planes import CostPlane

@dataclass(frozen=True,slots=True)
class CostQuote:
    quote_id:str; branch_id:str; base_version:str; model_version:str; activation_epoch:int; subject_id:str; epistemic_version:str|None; personality_version:str|None; path_id:str; quote_plane:CostPlane; final_components:tuple[CostComponent,...]; applied_modifier_ids:tuple[str,...]; proof_roots:tuple[str,...]; predicate_token_ids:tuple[str,...]; projection_only:bool; path_hash:str; modifier_set_hash:str; account_epoch_bindings:tuple[tuple[str,int],...]; channel_epoch_bindings:tuple[tuple[str,int],...]; scope_dependency_epoch:int
    @property
    def content_id(self)->str:return content_id((self.branch_id,self.base_version,self.model_version,self.activation_epoch,self.subject_id,self.epistemic_version,self.personality_version,self.path_id,self.quote_plane,self.final_components,self.applied_modifier_ids,self.proof_roots,self.predicate_token_ids,self.projection_only,self.path_hash,self.modifier_set_hash,self.account_epoch_bindings,self.channel_epoch_bindings,self.scope_dependency_epoch))
    def __post_init__(self):
        if self.quote_id!=self.content_id:raise ValueError("quote id not content-bound")
        if not self.final_components or any(c.plane is not self.quote_plane for c in self.final_components):raise ValueError("quote component plane mismatch/empty")
        if self.quote_plane is not CostPlane.EXECUTION and (self.epistemic_version is None or self.personality_version is None):raise ValueError("subjective quote lacks epistemic/personality binding")
        if self.path_hash!=content_id(self.path_id):raise ValueError("quote path hash mismatch")
        if self.modifier_set_hash!=content_id(self.applied_modifier_ids):raise ValueError("quote modifier set hash mismatch")
        if self.scope_dependency_epoch<0 or any(epoch<0 for _,epoch in self.account_epoch_bindings+self.channel_epoch_bindings):raise ValueError("negative quote dependency epoch")
        if tuple(sorted(dict(self.account_epoch_bindings).items()))!=self.account_epoch_bindings or tuple(sorted(dict(self.channel_epoch_bindings).items()))!=self.channel_epoch_bindings:raise ValueError("quote dependencies must be unique and canonical")

def quote_cost(*,branch_id:str,base_version:str,model_version:str,activation_epoch:int,subject_id:str,epistemic_version:str|None,personality_version:str|None,path_id:str,plane:CostPlane,components:tuple[CostComponent,...],modifiers:tuple[CostModifier,...],proof_roots:tuple[str,...],predicate_token_ids:tuple[str,...]=(),projection_only:bool=False,current_epoch:int,account_epochs:tuple[tuple[str,int],...]=(),channel_epochs:tuple[tuple[str,int],...]=(),scope_dependency_epoch:int=0)->CostQuote:
    if current_epoch<activation_epoch:raise ValueError("cost model not active")
    applicable=normalize_modifiers(tuple(m for m in modifiers if m.plane is plane),branch_id=branch_id,epoch=current_epoch)
    semantic_applications={};unique_applications={}
    for modifier in applicable:
        key=application_key(modifier,path_id=path_id,input_version=base_version)
        previous=semantic_applications.get(key)
        semantic_value=(modifier.operation,modifier.value,modifier.read_stage,modifier.write_stage,modifier.authority_domain)
        if previous is not None and previous!=semantic_value:raise ValueError("same modifier application key has conflicting content")
        semantic_applications[key]=semantic_value
        old=unique_applications.get(key)
        if old is None or modifier.modifier_id<old.modifier_id:unique_applications[key]=modifier
    applicable=tuple(sorted(unique_applications.values(),key=lambda item:(item.write_stage,item.read_stage,item.operation.value,item.modifier_id)))
    result=[];applied=set()
    for component in components:
        if component.plane is not plane:raise ValueError("cross-plane component")
        if component.bearer_ref!=subject_id and plane is not CostPlane.EXECUTION:raise ValueError("subjective quote bearer mismatch")
        selected=[m for m in applicable if m.dimension==component.dimension and m.selector_id in {"*",path_id,component.component_id}]
        if any(m.authority_domain!=component.authority_domain for m in selected):raise ValueError("modifier authority mismatch")
        current=component.amount
        stages=sorted({item.write_stage for item in selected})
        for stage in stages:
            bucket=[item for item in selected if item.write_stage==stage]
            replacements=[item for item in bucket if item.operation is ModifierOp.REPLACE]
            if len(replacements)>1:raise ValueError("multiple replacements in one cost stage")
            if replacements:current=replacements[0].value
            multipliers=[item.value for item in bucket if item.operation is ModifierOp.MULTIPLY]
            additions=[item.value for item in bucket if item.operation is ModifierOp.ADD]
            multiplier=Fraction(1)
            for value in multipliers:multiplier=mul(multiplier,value)
            current=mul(current,multiplier)
            addition=Fraction()
            for value in additions:addition=add(addition,value)
            current=add(current,addition)
            floors=[item.value for item in bucket if item.operation is ModifierOp.FLOOR];caps=[item.value for item in bucket if item.operation is ModifierOp.CAP]
            lower=max(floors,default=Fraction());upper=min(caps) if caps else None
            if upper is not None and lower>upper:raise ValueError("floor exceeds cap")
            current=checked(max(lower,current));current=checked(min(upper,current)) if upper is not None else current
        if current<0:raise ValueError("negative final cost")
        applied.update(m.modifier_id for m in selected)
        result.append(make_component(plane=component.plane,dimension=component.dimension,amount=current,bearer_ref=component.bearer_ref,sink_ref=component.sink_ref,authority_domain=component.authority_domain,origin_rule=component.origin_rule,algebra_id=component.algebra_id))
    applied_ids=tuple(sorted(applied));accounts=tuple(sorted(dict(account_epochs).items()));channels=tuple(sorted(dict(channel_epochs).items()))
    if len(accounts)!=len(account_epochs) or len(channels)!=len(channel_epochs):raise ValueError("duplicate quote dependency binding")
    values=(branch_id,base_version,model_version,activation_epoch,subject_id,epistemic_version,personality_version,path_id,plane,tuple(result),applied_ids,proof_roots,predicate_token_ids,projection_only,content_id(path_id),content_id(applied_ids),accounts,channels,scope_dependency_epoch)
    return CostQuote(content_id(values),*values)
