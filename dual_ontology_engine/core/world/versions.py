from __future__ import annotations
from dataclasses import dataclass, field
from fractions import Fraction
from types import MappingProxyType
from typing import Mapping
from ..foundation.codec import canonical_bytes, content_id
from ..foundation.ids import BranchId, VersionId
from .entities import Entity, Fact, Subject, PermissionGrant, IdentityFacet
from .relations import Relation,RelationEndpoint
from .obligations import Obligation
from .events import ScheduledEvent
from .channels import ChannelDefinition,ChannelLease

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
    scheduled_events: Mapping[str,ScheduledEvent] = field(default_factory=lambda: MappingProxyType({}))
    channels: Mapping[str,ChannelDefinition] = field(default_factory=lambda: MappingProxyType({}))
    leases: Mapping[str,ChannelLease] = field(default_factory=lambda: MappingProxyType({}))
    benefit_burden_pairs: Mapping[str,object] = field(default_factory=lambda: MappingProxyType({}))
    permission_epochs: Mapping[str,int] = field(default_factory=lambda: MappingProxyType({}))
    def __post_init__(self) -> None:
        for name in ("entities","subjects","facts","relations","balances","obligations","identity_epochs","identity_facets","claims","key_epochs","scheduled_events","channels","leases","benefit_burden_pairs","permission_epochs"): object.__setattr__(self,name,_frozen(getattr(self,name)))
        object.__setattr__(self,"permissions",tuple(self.permissions))
        if any(not isinstance(v,Fraction) or v<0 for v in self.balances.values()): raise ValueError("balances require nonnegative Fraction")
        if self.predicate_epoch<0 or any(not isinstance(v,int) or v<0 for v in self.key_epochs.values()): raise ValueError("invalid MVCC epoch")
        if any(not isinstance(v,int) or v<0 for v in self.identity_epochs.values()): raise ValueError("invalid identity epoch")
        if any(not isinstance(v,int) or v<0 for v in self.permission_epochs.values()): raise ValueError("invalid permission epoch")
        if len({g.grant_id for g in self.permissions})!=len(self.permissions): raise ValueError("duplicate permission grant")
        if any(key!=value.fact_id for key,value in self.facts.items()):raise ValueError("fact map key/id mismatch")
        if any(key!=value.ref.local_id for key,value in self.entities.items()):raise ValueError("entity map key/id mismatch")
        if any(key!=value.relation_id.local_id for key,value in self.relations.items()):raise ValueError("relation map key/id mismatch")
        if any(key!=value.obligation_id for key,value in self.obligations.items()):raise ValueError("obligation map key/id mismatch")
        if any(key!=value.event_id for key,value in self.scheduled_events.items()):raise ValueError("scheduled event map key/id mismatch")
        if any(key!=value.channel_id for key,value in self.channels.items()):raise ValueError("channel map key/id mismatch")
        if any(key!=value.lease_id for key,value in self.leases.items()):raise ValueError("lease map key/id mismatch")
        if any(key!=value.pair_id for key,value in self.benefit_burden_pairs.items()):raise ValueError("benefit/burden pair map key/id mismatch")
        canonical_bytes((self.entities,self.subjects,self.facts,self.relations,self.balances,self.obligations,self.permissions,self.identity_epochs,self.identity_facets,self.consumed_identity_epochs,self.settled_charge_keys,self.claims,self.key_epochs,self.predicate_epoch,self.scheduled_events,self.channels,self.leases,self.benefit_burden_pairs,self.permission_epochs))
    @property
    def root_hash(self)->str: return content_id((self.entities,self.subjects,self.facts,self.relations,self.balances,self.obligations,self.permissions,self.identity_epochs,self.identity_facets,self.consumed_identity_epochs,self.settled_charge_keys,self.claims,self.key_epochs,self.predicate_epoch,self.scheduled_events,self.channels,self.leases,self.benefit_burden_pairs,self.permission_epochs))

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


@dataclass(frozen=True,slots=True)
class BranchForkPolicy:
    inherit_balances:bool=True
    inherit_obligations:bool=True
    rebuild_permissions:bool=True
    reacquire_leases:bool=True


def fork_version(base:WorldVersion,new_branch_id:str,*,policy:BranchForkPolicy=BranchForkPolicy())->WorldVersion:
    """把简单冻结状态投影到新因果分支；复杂负担必须先提供专用对账。"""
    if not new_branch_id or new_branch_id==str(base.branch_id):raise ValueError("fork requires a distinct nonempty branch")
    unresolved=[]
    if base.state.claims:unresolved.append("deferred_claims")
    if base.state.scheduled_events:unresolved.append("scheduled_events")
    if base.state.settled_charge_keys:unresolved.append("settled_charges")
    if base.state.leases and not policy.reacquire_leases:unresolved.append("active_leases")
    if unresolved:raise ValueError(f"branch fork requires explicit reconciliation:{','.join(unresolved)}")

    branch=BranchId(new_branch_id)
    def remap(ref):return type(ref)(branch,ref.local_id,ref.epoch)
    entities={key:Entity(remap(value.ref),value.kind,value.facets,value.active) for key,value in base.state.entities.items()}
    subjects={key:Subject(remap(value.ref),value.identity_epoch,value.personality_version,tuple(remap(ref) for ref in value.controlled_entities)) for key,value in base.state.subjects.items()}
    facts={key:Fact(value.fact_id,remap(value.subject),value.predicate,value.value,value.provenance_id) for key,value in base.state.facts.items()}
    relations={key:Relation(remap(value.relation_id),value.kind,tuple(RelationEndpoint(endpoint.role,remap(endpoint.ref)) for endpoint in value.endpoints),value.scope_id,value.valid_from,value.valid_until,value.provenance_id,value.endpoint_deletion_policy) for key,value in base.state.relations.items()}
    permissions=() if policy.rebuild_permissions else base.state.permissions
    balances=base.state.balances if policy.inherit_balances else {}
    obligations=base.state.obligations if policy.inherit_obligations else {}
    leases={} if policy.reacquire_leases else base.state.leases
    state=WorldState(entities=entities,subjects=subjects,facts=facts,relations=relations,balances=balances,obligations=obligations,permissions=permissions,identity_epochs=base.state.identity_epochs,identity_facets=base.state.identity_facets,consumed_identity_epochs=base.state.consumed_identity_epochs,settled_charge_keys=frozenset(),claims={},key_epochs=base.state.key_epochs,predicate_epoch=base.state.predicate_epoch,scheduled_events={},channels=base.state.channels,leases=leases,benefit_burden_pairs={},permission_epochs={} if policy.rebuild_permissions else base.state.permission_epochs)
    generation=base.generation+1
    cause=content_id(("branch-fork",str(base.version_id),new_branch_id,policy))
    version_id=VersionId(f"{new_branch_id}:{generation}:{state.root_hash[:16]}")
    return WorldVersion(version_id,branch,(base.version_id,),generation,base.world_time,state,cause,base.ruleset_hash,0)
