from __future__ import annotations

from dataclasses import dataclass
from fractions import Fraction

from ..foundation.codec import content_id


@dataclass(frozen=True, slots=True)
class CompensableDamage:
    damage_id: str
    causal_event_id: str
    victim_ref: str
    dimension: str
    maximum_compensation: Fraction

    @property
    def content_id(self)->str:return content_id((self.causal_event_id,self.victim_ref,self.dimension,self.maximum_compensation))
    def __post_init__(self)->None:
        if self.damage_id!=self.content_id:raise ValueError("damage id not content-bound")
        if not isinstance(self.maximum_compensation,Fraction) or self.maximum_compensation<0:raise ValueError("invalid compensable damage")


def make_compensable_damage(*,causal_event_id:str,victim_ref:str,dimension:str,maximum_compensation:Fraction)->CompensableDamage:
    values=(causal_event_id,victim_ref,dimension,maximum_compensation)
    return CompensableDamage(content_id(values),*values)


@dataclass(frozen=True, slots=True)
class ResponsibilityShare:
    responsible_ref: str
    share: Fraction
    rule_id: str

    def __post_init__(self)->None:
        if not self.responsible_ref or not self.rule_id or not isinstance(self.share,Fraction) or not 0<=self.share<=1:raise ValueError("invalid responsibility share")


@dataclass(frozen=True, slots=True)
class CompensationAllocation:
    allocation_id: str
    damage_id: str
    payer_ref: str
    recipient_ref: str
    dimension: str
    amount: Fraction
    allocation_rule_id: str

    @property
    def content_id(self)->str:return content_id((self.damage_id,self.payer_ref,self.recipient_ref,self.dimension,self.amount,self.allocation_rule_id))
    def __post_init__(self)->None:
        if self.allocation_id!=self.content_id:raise ValueError("compensation allocation id not content-bound")


@dataclass(frozen=True, slots=True)
class AllocationCertificate:
    damage_id: str
    total_share: Fraction
    total_compensation: Fraction
    unallocated_damage: Fraction
    allocation_ids: tuple[str,...]


def allocate_compensation(damage:CompensableDamage,shares:tuple[ResponsibilityShare,...])->tuple[tuple[CompensationAllocation,...],AllocationCertificate]:
    """按规则份额分配同一损害；罚款等独立来源不得混入此函数。"""
    if len({share.responsible_ref for share in shares})!=len(shares):raise ValueError("duplicate responsible party for one damage")
    total_share=sum((share.share for share in shares),Fraction())
    if total_share>1:raise ValueError("responsibility shares exceed whole damage")
    allocations=[]
    for share in sorted(shares,key=lambda item:(item.responsible_ref,item.rule_id)):
        amount=damage.maximum_compensation*share.share
        values=(damage.damage_id,share.responsible_ref,damage.victim_ref,damage.dimension,amount,share.rule_id)
        allocations.append(CompensationAllocation(content_id(values),*values))
    total=sum((item.amount for item in allocations),Fraction())
    if total>damage.maximum_compensation:raise AssertionError("compensation exceeds declared damage")
    certificate=AllocationCertificate(damage.damage_id,total_share,total,damage.maximum_compensation-total,tuple(item.allocation_id for item in allocations))
    return tuple(allocations),certificate
