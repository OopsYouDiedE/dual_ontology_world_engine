from __future__ import annotations
from dataclasses import MISSING, dataclass, fields
from enum import Enum
from fractions import Fraction
from ..foundation.codec import canonical_bytes, content_id
from ..foundation.provenance import Provenance
from ..world.entities import Entity, Fact
from ..world.relations import Relation
from ..world.obligations import Obligation
from ..world.identity import IdentityTransitionPlan

class OperationKind(str,Enum):
    SET_FACT="set_fact"; DELETE_FACT="delete_fact"; CREATE_ENTITY="create_entity"; CREATE_RELATION="create_relation"; SET_OBLIGATION="set_obligation"; IDENTITY_TRANSITION="identity_transition"; SET_CLAIM="set_claim"

_EXPECTED={OperationKind.SET_FACT:Fact,OperationKind.DELETE_FACT:type(None),OperationKind.CREATE_ENTITY:Entity,OperationKind.CREATE_RELATION:Relation,OperationKind.SET_OBLIGATION:Obligation,OperationKind.IDENTITY_TRANSITION:IdentityTransitionPlan}


class ConflictStrategy(str, Enum):
    """同一冲突分量中由规则预先声明的处理语义。"""

    REJECT_COMPONENT = "reject_component"
    CANCEL_COMPONENT = "cancel_component"
    DOMINANCE = "dominance"


@dataclass(frozen=True, slots=True, order=True)
class ConflictRank:
    """非整数总优先级；三个维度必须同时不低才构成支配。"""

    authority: int = 0
    specificity: int = 0
    intervention: int = 0

    def __post_init__(self) -> None:
        if min(self.authority, self.specificity, self.intervention) < 0:
            raise ValueError("conflict rank cannot be negative")

    def dominates(self, other: "ConflictRank") -> bool:
        left = (self.authority, self.specificity, self.intervention)
        right = (other.authority, other.specificity, other.intervention)
        return all(a >= b for a, b in zip(left, right)) and any(
            a > b for a, b in zip(left, right)
        )

@dataclass(frozen=True,slots=True)
class StateOperation:
    kind:OperationKind; address:str; value:object; provenance_id:str
    def __post_init__(self)->None:
        if not isinstance(self.kind,OperationKind) or not self.address: raise ValueError("closed operation kind/address required")
        if self.kind is OperationKind.SET_CLAIM:
            raise NotImplementedError("UNSUPPORTED: delayed claim persistence is fail-closed")
        elif type(self.value) is not _EXPECTED[self.kind]: raise TypeError(f"invalid payload for {self.kind.value}")
        canonical_bytes(self.value)

@dataclass(frozen=True,slots=True)
class PointRead:
    address:str; expected_epoch:int
    def __post_init__(self):
        if self.expected_epoch<0: raise ValueError("negative read epoch")

@dataclass(frozen=True,slots=True)
class PredicateReadToken:
    normalized_query:bytes; expected_global_epoch:int; negative:bool=False; range_read:bool=False
    def __post_init__(self):
        if self.expected_global_epoch<0 or not self.normalized_query: raise ValueError("invalid predicate token")

@dataclass(frozen=True,slots=True)
class Precondition:
    address:str; expected_content_id:str|None

@dataclass(frozen=True,slots=True)
class Posting:
    account_ref:str; dimension:str; delta:Fraction; provenance_id:str
    def __post_init__(self):
        if not isinstance(self.delta,Fraction): raise TypeError("posting delta requires Fraction")

@dataclass(frozen=True,slots=True)
class ChargeKey:
    key_id:str; branch_id:str; source_id:str; component_id:str; allocation_rule_id:str
    @property
    def content_id(self)->str: return content_id((self.branch_id,self.source_id,self.component_id,self.allocation_rule_id))
    def __post_init__(self):
        if self.key_id!=self.content_id: raise ValueError("charge key must bind branch and source content")

def make_charge_key(branch_id:str,source_id:str,component_id:str,allocation_rule_id:str)->ChargeKey:
    key=content_id((branch_id,source_id,component_id,allocation_rule_id)); return ChargeKey(key,branch_id,source_id,component_id,allocation_rule_id)

@dataclass(frozen=True,slots=True)
class ChargeGroup:
    charge_key:ChargeKey; postings:tuple[Posting,...]
    def __post_init__(self):
        if not self.postings: raise ValueError("charge group cannot be empty")
        dimensions={p.dimension for p in self.postings}
        for dimension in dimensions:
            if sum((p.delta for p in self.postings if p.dimension==dimension),Fraction())!=0: raise ValueError("charge group is unbalanced")

@dataclass(frozen=True,slots=True)
class Proposal:
    proposal_id:str; branch_id:str; base_version:str; actor_ref:str; actor_identity_epoch:int; permission_action:str
    preconditions:tuple[Precondition,...]; point_reads:tuple[PointRead,...]; predicate_reads:tuple[PredicateReadToken,...]
    operations:tuple[StateOperation,...]; charge_groups:tuple[ChargeGroup,...]; provenance_nodes:tuple[Provenance,...]; provenance_root:str
    atomic_group_id:str=""; conflict_strategy:ConflictStrategy=ConflictStrategy.REJECT_COMPONENT; conflict_rank:ConflictRank=ConflictRank()
    @property
    def content_id(self)->str:
        return content_id((self.branch_id,self.base_version,self.actor_ref,self.actor_identity_epoch,self.permission_action,self.preconditions,self.point_reads,self.predicate_reads,self.operations,self.charge_groups,self.provenance_nodes,self.provenance_root,self.atomic_group_id,self.conflict_strategy,self.conflict_rank))
    def __post_init__(self):
        if any(type(operation) is not StateOperation for operation in self.operations): raise TypeError("proposal accepts only canonical StateOperation")
        if any(type(group) is not ChargeGroup or any(type(posting) is not Posting for posting in group.postings) for group in self.charge_groups): raise TypeError("proposal accepts only canonical charge groups")
        if self.proposal_id!=self.content_id: raise ValueError("proposal id must bind its full content")
        if not self.operations and not self.charge_groups: raise ValueError("empty/zero-progress proposal")
        if len({group.charge_key.key_id for group in self.charge_groups})!=len(self.charge_groups): raise ValueError("duplicate charge group")
        if not isinstance(self.conflict_strategy,ConflictStrategy) or not isinstance(self.conflict_rank,ConflictRank): raise TypeError("closed conflict policy required")

def make_proposal(**values:object)->Proposal:
    values=dict(values); values["proposal_id"]=""
    for field in fields(Proposal):
        if field.name in values: continue
        if field.default is not MISSING: values[field.name]=field.default
        elif field.default_factory is not MISSING: values[field.name]=field.default_factory()
    draft=object.__new__(Proposal)
    for field_name,field_value in values.items(): object.__setattr__(draft,field_name,field_value)
    object.__setattr__(draft,"proposal_id",Proposal.content_id.__get__(draft,Proposal))
    Proposal.__post_init__(draft)
    return draft
