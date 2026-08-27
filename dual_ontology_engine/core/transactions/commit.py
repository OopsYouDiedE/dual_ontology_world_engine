from __future__ import annotations
from dataclasses import dataclass
from enum import Enum
from fractions import Fraction
from ..foundation.codec import content_id
from ..foundation.failures import FailureCertificate,FailureKind
from ..foundation.ids import VersionId
from ..foundation.provenance import validate_provenance_dag
from ..world.versions import WorldState,WorldVersion
from ..world.identity import FacetTransfer
from ..costs.claims import ClaimState,transition_claim
from .conflicts import resolve_conflicts
from .ledger import Ledger
from .proposals import Proposal,OperationKind
from .invariants import InvariantViolation,validate_world_invariants

class CommitStatus(str,Enum): COMMITTED="committed"; REJECTED="rejected"; STALE="stale"

@dataclass(frozen=True,slots=True)
class CommitTrace:
    trace_id:str; version_id:str; proposal_ids:tuple[str,...]; operation_addresses:tuple[str,...]; charge_keys:tuple[str,...]; provenance_roots:tuple[str,...]; rejected_proposals:tuple[tuple[str,str],...]=(); conflict_component_ids:tuple[str,...]=()

@dataclass(frozen=True,slots=True)
class CommitResult:
    status:CommitStatus; version:WorldVersion; failure:FailureCertificate|None=None; trace:CommitTrace|None=None
    @property
    def committed(self)->bool:return self.status is CommitStatus.COMMITTED

def _failure(base:WorldVersion,kind:FailureKind,code:str,*witnesses:str,status:CommitStatus=CommitStatus.REJECTED)->CommitResult:
    return CommitResult(status,base,FailureCertificate(kind,code,str(base.version_id),witness_refs=tuple(witnesses)))

def _address_value(state:WorldState,address:str)->object|None:
    prefix,_,key=address.partition(":")
    table={"fact":state.facts,"entity":state.entities,"relation":state.relations,"obligation":state.obligations,"claim":state.claims}
    return table.get(prefix,{}).get(key)

def _authorized(state:WorldState,proposal:Proposal)->bool:
    return any(g.actor_ref==proposal.actor_ref and g.action==proposal.permission_action and g.identity_epoch==proposal.actor_identity_epoch and all(op.address.startswith(g.target_prefix) for op in proposal.operations) for g in state.permissions)

class Kernel:
    """唯一权威提交入口；只解释封闭 StateOperation，不执行对象方法。"""
    def commit(self,base:WorldVersion,proposals:tuple[Proposal,...])->CommitResult:
        if not proposals:return _failure(base,FailureKind.INVALID,"empty-batch")
        if any(type(proposal) is not Proposal for proposal in proposals):return _failure(base,FailureKind.INVALID,"non-canonical-proposal-type")
        if any(p.branch_id!=str(base.branch_id) or p.base_version!=str(base.version_id) for p in proposals):return _failure(base,FailureKind.STALE,"branch-or-base-mismatch",status=CommitStatus.STALE)
        resolution=resolve_conflicts(proposals)
        if not resolution.accepted:return _failure(base,FailureKind.CONFLICT,"semantic-write-conflict",*(x[0] for x in resolution.rejected))
        active_proposals=resolution.accepted
        try:
            for proposal in active_proposals:
                if proposal.actor_ref not in base.state.subjects: raise PermissionError("unknown-actor")
                if base.state.identity_epochs.get(proposal.actor_ref)!=proposal.actor_identity_epoch: raise PermissionError("identity-epoch-mismatch")
                if not _authorized(base.state,proposal): raise PermissionError("permission-denied")
                validate_provenance_dag(proposal.provenance_nodes)
                ids={n.provenance_id for n in proposal.provenance_nodes}
                if proposal.provenance_root not in ids or any(op.provenance_id not in ids for op in proposal.operations): raise ValueError("missing-provenance-root")
                for read in proposal.point_reads:
                    if base.state.key_epochs.get(read.address,0)!=read.expected_epoch: raise RuntimeError("point-read-invalidated")
                for token in proposal.predicate_reads:
                    if base.state.predicate_epoch!=token.expected_global_epoch: raise RuntimeError("predicate-read-invalidated")
                for condition in proposal.preconditions:
                    current=_address_value(base.state,condition.address); current_id=None if current is None else content_id(current)
                    if current_id!=condition.expected_content_id: raise RuntimeError("precondition-failed")
        except PermissionError as exc:return _failure(base,FailureKind.UNAUTHORIZED,str(exc))
        except RuntimeError as exc:return _failure(base,FailureKind.STALE,str(exc),status=CommitStatus.STALE)
        except (ValueError,TypeError) as exc:return _failure(base,FailureKind.INVALID,str(exc))
        entities=dict(base.state.entities);facts=dict(base.state.facts);relations=dict(base.state.relations);obligations=dict(base.state.obligations);claims=dict(base.state.claims)
        epochs=dict(base.state.key_epochs);identity_epochs=dict(base.state.identity_epochs);facets=dict(base.state.identity_facets);consumed=set(base.state.consumed_identity_epochs)
        ledger=Ledger(base.state.balances,base.state.settled_charge_keys)
        try:
            for proposal in active_proposals:
                for group in proposal.charge_groups: ledger=ledger.apply_group(group,branch_id=str(base.branch_id))
                for op in proposal.operations:
                    prefix,_,key=op.address.partition(":")
                    if op.kind is OperationKind.SET_FACT:
                        if prefix!="fact" or op.value.fact_id!=key: raise ValueError("fact semantic address mismatch")
                        facts[key]=op.value
                    elif op.kind is OperationKind.DELETE_FACT: facts.pop(key,None)
                    elif op.kind is OperationKind.CREATE_ENTITY:
                        if key in entities: raise ValueError("entity overwrite forbidden")
                        entities[key]=op.value
                    elif op.kind is OperationKind.CREATE_RELATION:
                        if key in relations: raise ValueError("relation overwrite forbidden")
                        known={e.ref for e in entities.values()}
                        if any(endpoint.ref not in known for endpoint in op.value.endpoints): raise ValueError("relation has unknown endpoint")
                        relations[key]=op.value
                    elif op.kind is OperationKind.SET_OBLIGATION:
                        previous=obligations.get(key)
                        if previous is not None and previous.obligation_id!=op.value.obligation_id: raise ValueError("obligation identity overwrite")
                        obligations[key]=op.value
                    elif op.kind is OperationKind.SET_CLAIM:
                        previous=claims.get(key)
                        if previous is None:
                            if op.value.state is not ClaimState.PENDING:raise ValueError("new claim must begin PENDING")
                        else:
                            if previous==op.value:raise ValueError("idempotent claim transition makes no progress")
                            event_ids={n.event_id for n in proposal.provenance_nodes if n.event_id is not None}
                            event_ref=previous.due_event_ref if previous.due_event_ref in event_ids else ""
                            expected=transition_claim(previous,op.value.state,branch_id=str(base.branch_id),event_ref=event_ref)
                            if expected!=op.value:raise ValueError("claim transition changed immutable fields")
                            if op.value.state is ClaimState.SETTLED and not any(g.charge_key.source_id in {previous.claim_id,op.value.claim_id} for g in proposal.charge_groups):raise ValueError("settled claim lacks bound charge")
                        claims[key]=op.value
                    elif op.kind is OperationKind.IDENTITY_TRANSITION:
                        plan=op.value; marker=(plan.source_subject,plan.source_epoch)
                        if marker in consumed or identity_epochs.get(plan.source_subject)!=plan.source_epoch: raise ValueError("identity epoch already consumed")
                        source=facets.get(plan.source_subject,());plan.validate(source);instructions={i.facet_name:i for i in plan.instructions};outputs={d:[] for d in plan.destination_subjects}
                        for facet in source:
                            instruction=instructions[facet.name]
                            if instruction.mode is FacetTransfer.MOVE: outputs[plan.destination_subjects[0]].append(facet)
                            elif instruction.mode is FacetTransfer.COPY:
                                for destination in outputs: outputs[destination].append(facet)
                            elif instruction.mode is FacetTransfer.DERIVE:
                                for destination in outputs: outputs[destination].append(type(facet)(facet.name,instruction.derived_value,facet.exclusive))
                        consumed.add(marker);facets.pop(plan.source_subject,None);identity_epochs[plan.source_subject]=plan.source_epoch+1
                        for destination,values in outputs.items():
                            if destination not in base.state.subjects: raise ValueError("unknown identity destination")
                            facets[destination]=tuple(values)
                    epochs[op.address]=epochs.get(op.address,0)+1
        except (ValueError,TypeError) as exc:return _failure(base,FailureKind.INVALID,str(exc))
        state=WorldState(entities,base.state.subjects,facts,relations,ledger.balances,obligations,base.state.permissions,identity_epochs,facets,frozenset(consumed),ledger.settled_charge_keys,claims,epochs,base.state.predicate_epoch+1)
        try:validate_world_invariants(state,branch_id=str(base.branch_id))
        except InvariantViolation as exc:return _failure(base,FailureKind.INVALID,exc.witness.code,*exc.witness.refs)
        generation=base.generation+1;vid=VersionId(f"{base.branch_id}:{generation}:{state.root_hash[:16]}")
        version=WorldVersion(vid,base.branch_id,(base.version_id,),generation,base.world_time,state,content_id(tuple(p.proposal_id for p in active_proposals)),base.ruleset_hash,base.commit_seq+1)
        trace_values=(str(vid),tuple(p.proposal_id for p in active_proposals),resolution.rejected,tuple(c.component_id for c in resolution.components))
        trace=CommitTrace(content_id(trace_values),str(vid),tuple(p.proposal_id for p in active_proposals),tuple(op.address for p in active_proposals for op in p.operations),tuple(g.charge_key.key_id for p in active_proposals for g in p.charge_groups),tuple(p.provenance_root for p in active_proposals),resolution.rejected,tuple(c.component_id for c in resolution.components))
        return CommitResult(CommitStatus.COMMITTED,version,None,trace)

class VersionStore:
    def __init__(self,head:WorldVersion):self._head=head
    @property
    def head(self)->WorldVersion:return self._head
    def submit(self,proposals:tuple[Proposal,...])->CommitResult:
        result=Kernel().commit(self._head,proposals)
        if result.committed:self._head=result.version
        return result

AuthoritativeKernel=Kernel
