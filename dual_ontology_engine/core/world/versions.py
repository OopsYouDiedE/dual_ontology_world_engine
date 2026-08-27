from __future__ import annotations
from dataclasses import dataclass, field
from fractions import Fraction
from types import MappingProxyType
from typing import Mapping
from ..foundation.codec import canonical_bytes, content_id
from ..foundation.ids import BranchId, VersionId
from .entities import Entity, Fact, Subject, PermissionGrant, IdentityFacet
from .relations import Relation
from .obligations import Obligation

def _frozen(value: Mapping | None) -> Mapping: return MappingProxyType(dict(value or {}))

@dataclass(frozen=True, slots=True)
class WorldState:
    entities: Mapping[str, Entity] = field(default_factory=lambda: MappingProxyType({}))
    subjects: Mapping[str, Subject] = field(default_factory=lambda: MappingProxyType({}))
    facts: Mapping[str, Fact] = field(default_factory=lambda: MappingProxyType({}))
    relations: Mapping[str, Relation] = field(default_factory=lambda: MappingProxyType({}))
    balances: Mapping[tuple[str,str], Fraction] = field(default_factory=lambda: MappingProxyType({}))
    obligations: Mapping[str, Obligation] = field(default_factory=lambda: MappingProxyType({}))
    permissions: tuple[PermissionGrant,...] = ()
    identity_epochs: Mapping[str,int] = field(default_factory=lambda: MappingProxyType({}))
    identity_facets: Mapping[str,tuple[IdentityFacet,...]] = field(default_factory=lambda: MappingProxyType({}))
    consumed_identity_epochs: frozenset[tuple[str,int]] = frozenset()
    settled_charge_keys: frozenset[str] = frozenset()
    claims: Mapping[str,object] = field(default_factory=lambda: MappingProxyType({}))
    key_epochs: Mapping[str,int] = field(default_factory=lambda: MappingProxyType({}))
    predicate_epoch: int = 0
    def __post_init__(self) -> None:
        for name in ("entities","subjects","facts","relations","balances","obligations","identity_epochs","identity_facets","claims","key_epochs"): object.__setattr__(self,name,_frozen(getattr(self,name)))
        object.__setattr__(self,"permissions",tuple(self.permissions))
        if any(not isinstance(v,Fraction) or v<0 for v in self.balances.values()): raise ValueError("balances require nonnegative Fraction")
        if self.predicate_epoch<0 or any(not isinstance(v,int) or v<0 for v in self.key_epochs.values()): raise ValueError("invalid MVCC epoch")
        if any(not isinstance(v,int) or v<0 for v in self.identity_epochs.values()): raise ValueError("invalid identity epoch")
        if len({g.grant_id for g in self.permissions})!=len(self.permissions): raise ValueError("duplicate permission grant")
        if any(key!=value.fact_id for key,value in self.facts.items()):raise ValueError("fact map key/id mismatch")
        if any(key!=value.ref.local_id for key,value in self.entities.items()):raise ValueError("entity map key/id mismatch")
        if any(key!=value.relation_id.local_id for key,value in self.relations.items()):raise ValueError("relation map key/id mismatch")
        if any(key!=value.obligation_id for key,value in self.obligations.items()):raise ValueError("obligation map key/id mismatch")
        canonical_bytes((self.entities,self.subjects,self.facts,self.relations,self.balances,self.obligations,self.permissions,self.identity_epochs,self.identity_facets,self.consumed_identity_epochs,self.settled_charge_keys,self.claims,self.key_epochs,self.predicate_epoch))
    @property
    def root_hash(self)->str: return content_id((self.entities,self.subjects,self.facts,self.relations,self.balances,self.obligations,self.permissions,self.identity_epochs,self.identity_facets,self.consumed_identity_epochs,self.settled_charge_keys,self.claims,self.key_epochs,self.predicate_epoch))

@dataclass(frozen=True, slots=True)
class WorldVersion:
    version_id: VersionId; branch_id: BranchId; parents: tuple[VersionId,...]; generation:int; world_time:Fraction; state:WorldState; cause_id:str; ruleset_hash:str; commit_seq:int
    def __post_init__(self)->None:
        if self.generation<0 or self.commit_seq<0 or not self.cause_id or not isinstance(self.world_time,Fraction): raise ValueError("invalid world version metadata")

def genesis(branch_id:str,*,ruleset_hash:str="empty")->WorldVersion: return WorldVersion(VersionId(f"{branch_id}:0"),BranchId(branch_id),(),0,Fraction(),WorldState(),"genesis",ruleset_hash,0)

def validate_version_dag(versions:tuple[WorldVersion,...])->None:
    by_id={v.version_id:v for v in versions}
    if len(by_id)!=len(versions): raise ValueError("duplicate world version id")
    for version in versions:
        for parent_id in version.parents:
            parent=by_id.get(parent_id)
            if parent is None or parent.generation>=version.generation: raise ValueError("invalid causal parent")
