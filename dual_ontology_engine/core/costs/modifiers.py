from __future__ import annotations
from dataclasses import dataclass
from enum import Enum
from fractions import Fraction
from ..foundation.codec import content_id
from .planes import CostPlane

class ModifierOp(str,Enum): ADD="add"; MULTIPLY="multiply"; FLOOR="floor"; CAP="cap"; REPLACE="replace"
class StackMode(str,Enum): ONCE_PER_PATH="once_per_path"; ONCE_PER_SOURCE="once_per_source"; UNIQUE_GROUP="unique_group"; ACCUMULATE="accumulate"; UNSUPPORTED_SEGMENT="unsupported_segment"; UNSUPPORTED_MAX_N="unsupported_max_n"

@dataclass(frozen=True,slots=True)
class CostModifier:
    modifier_id:str; branch_id:str; plane:CostPlane; selector_id:str; dimension:str; operation:ModifierOp; value:Fraction; read_stage:int; write_stage:int; stack_mode:StackMode; stack_group:str; authority_domain:str; active_from_epoch:int; expires_after_epoch:int|None; source_id:str; provenance_id:str
    @property
    def content_id(self)->str:return content_id((self.branch_id,self.plane,self.selector_id,self.dimension,self.operation,self.value,self.read_stage,self.write_stage,self.stack_mode,self.stack_group,self.authority_domain,self.active_from_epoch,self.expires_after_epoch,self.source_id,self.provenance_id))
    def __post_init__(self):
        if self.modifier_id!=self.content_id:raise ValueError("modifier id is not content-bound")
        if not isinstance(self.value,Fraction):raise TypeError("modifier value requires Fraction")
        if self.read_stage>=self.write_stage:raise ValueError("modifier stage cycle")
        if self.operation is ModifierOp.MULTIPLY and self.value<0:raise ValueError("negative multiplier")
        if self.active_from_epoch<0 or self.expires_after_epoch is not None and self.expires_after_epoch<self.active_from_epoch:raise ValueError("invalid modifier validity")
        if self.stack_mode in {StackMode.UNSUPPORTED_SEGMENT,StackMode.UNSUPPORTED_MAX_N}:raise NotImplementedError("unsupported modifier stack mode")

def make_modifier(**values:object)->CostModifier:
    values=dict(values); values["modifier_id"]=""; draft=object.__new__(CostModifier)
    for k,v in values.items():object.__setattr__(draft,k,v)
    object.__setattr__(draft,"modifier_id",CostModifier.content_id.__get__(draft,CostModifier));CostModifier.__post_init__(draft);return draft

def normalize_modifiers(modifiers:tuple[CostModifier,...],*,branch_id:str,epoch:int)->tuple[CostModifier,...]:
    valid=[]
    for item in modifiers:
        if item.branch_id!=branch_id:raise ValueError("modifier from another branch")
        if epoch<item.active_from_epoch or item.expires_after_epoch is not None and epoch>item.expires_after_epoch:raise ValueError("expired/inactive modifier")
        valid.append(item)
    if len({m.modifier_id for m in valid})!=len(valid):raise ValueError("duplicate modifier id")
    groups:dict[tuple[str,str],list[CostModifier]]={}
    for item in valid:groups.setdefault((item.stack_group,item.dimension),[]).append(item)
    result=[]
    for key,bucket in sorted(groups.items()):
        modes={x.stack_mode for x in bucket}
        if StackMode.UNIQUE_GROUP in modes and len(bucket)>1:raise ValueError("unresolved unique modifier group")
        if StackMode.ONCE_PER_PATH in modes and len(bucket)>1:raise ValueError("once-per-path modifier duplicated")
        if StackMode.ONCE_PER_SOURCE in modes:
            sources=[x.source_id for x in bucket]
            if len(sources)!=len(set(sources)):raise ValueError("once-per-source duplicated")
        result.extend(sorted(bucket,key=lambda x:x.modifier_id))
    return tuple(result)

