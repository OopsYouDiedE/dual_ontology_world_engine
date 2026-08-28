from __future__ import annotations
from dataclasses import MISSING,dataclass,fields
from enum import Enum
from fractions import Fraction
from ..foundation.codec import content_id
from .planes import CostPlane

class ModifierOp(str,Enum): ADD="add"; MULTIPLY="multiply"; FLOOR="floor"; CAP="cap"; REPLACE="replace"
class StackMode(str,Enum): ONCE_PER_PATH="once_per_path"; ONCE_PER_SEGMENT="once_per_segment"; ONCE_PER_SOURCE="once_per_source"; MAX_N="max_n"; UNIQUE_GROUP="unique_group"; ACCUMULATE="accumulate"

@dataclass(frozen=True,slots=True)
class CostModifier:
    modifier_id:str; branch_id:str; plane:CostPlane; selector_id:str; dimension:str; operation:ModifierOp; value:Fraction; read_stage:int; write_stage:int; stack_mode:StackMode; stack_group:str; authority_domain:str; active_from_epoch:int; expires_after_epoch:int|None; source_id:str; provenance_id:str
    application_marker:str="path";max_applications:int|None=None
    @property
    def content_id(self)->str:return content_id((self.branch_id,self.plane,self.selector_id,self.dimension,self.operation,self.value,self.read_stage,self.write_stage,self.stack_mode,self.stack_group,self.authority_domain,self.active_from_epoch,self.expires_after_epoch,self.source_id,self.provenance_id,self.application_marker,self.max_applications))
    def __post_init__(self):
        if self.modifier_id!=self.content_id:raise ValueError("modifier id is not content-bound")
        if not isinstance(self.value,Fraction):raise TypeError("modifier value requires Fraction")
        if self.read_stage>=self.write_stage:raise ValueError("modifier stage cycle")
        if self.operation is ModifierOp.MULTIPLY and self.value<0:raise ValueError("negative multiplier")
        if self.active_from_epoch<0 or self.expires_after_epoch is not None and self.expires_after_epoch<self.active_from_epoch:raise ValueError("invalid modifier validity")
        if not self.application_marker:raise ValueError("modifier application marker required")
        if self.stack_mode is StackMode.MAX_N:
            if self.max_applications is None or self.max_applications<1:raise ValueError("MAX_N requires a positive bound")
        elif self.max_applications is not None:raise ValueError("max_applications is valid only for MAX_N")

def make_modifier(**values:object)->CostModifier:
    values=dict(values); values["modifier_id"]=""
    for field in fields(CostModifier):
        if field.name in values:continue
        if field.default is not MISSING:values[field.name]=field.default
        elif field.default_factory is not MISSING:values[field.name]=field.default_factory()
    draft=object.__new__(CostModifier)
    for k,v in values.items():object.__setattr__(draft,k,v)
    object.__setattr__(draft,"modifier_id",CostModifier.content_id.__get__(draft,CostModifier));CostModifier.__post_init__(draft);return draft


def application_key(modifier:CostModifier,*,path_id:str,input_version:str)->tuple[object,...]:
    """防止同一语义来源经多条监听路径重复叠加。"""
    if modifier.stack_mode is StackMode.ACCUMULATE:
        occurrence=modifier.modifier_id
    elif modifier.stack_mode is StackMode.ONCE_PER_SOURCE:
        occurrence=modifier.source_id
    elif modifier.stack_mode is StackMode.ONCE_PER_PATH:
        occurrence=(modifier.source_id,path_id)
    elif modifier.stack_mode is StackMode.ONCE_PER_SEGMENT:
        occurrence=(modifier.source_id,path_id,modifier.application_marker)
    elif modifier.stack_mode is StackMode.MAX_N:
        occurrence=modifier.modifier_id
    else:
        occurrence=(modifier.stack_group,modifier.selector_id)
    return (modifier.branch_id,occurrence,modifier.active_from_epoch,path_id,modifier.dimension,input_version,modifier.stack_group)

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
        if len(modes)>1:raise ValueError("mixed stack modes in one stack group")
        if StackMode.UNIQUE_GROUP in modes and len(bucket)>1:raise ValueError("unresolved unique modifier group")
        if StackMode.MAX_N in modes:
            bounds={item.max_applications for item in bucket}
            if len(bounds)!=1:raise ValueError("conflicting MAX_N bounds")
            if len(bucket)>next(iter(bounds)):raise ValueError("MAX_N modifier bound exceeded")
        result.extend(sorted(bucket,key=lambda x:(x.write_stage,x.read_stage,x.operation.value,x.modifier_id)))
    return tuple(sorted(result,key=lambda x:(x.write_stage,x.read_stage,x.operation.value,x.modifier_id)))
