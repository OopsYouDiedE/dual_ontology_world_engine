from __future__ import annotations
from dataclasses import dataclass
from enum import Enum
from fractions import Fraction
from typing import TYPE_CHECKING
from ..foundation.codec import content_id
from ..foundation.failures import FailureCertificate,FailureKind
from ..foundation.ids import VersionId
from ..foundation.provenance import validate_provenance_dag
from ..world.versions import WorldState,WorldVersion
from ..world.identity import FacetTransfer
from ..world.relations import EndpointDeletionPolicy
from ..world.events import ScheduledEventState,due_events,transition_scheduled_event
from ..costs.claims import ClaimState,transition_claim
from .conflicts import resolve_conflicts
from .ledger import Ledger
from .proposals import Proposal,OperationKind
from .invariants import InvariantViolation,validate_world_invariants

if TYPE_CHECKING:
    from ..tracing.proofs import TraceDag

class CommitStatus(str,Enum): COMMITTED="committed"; REJECTED="rejected"; STALE="stale"

@dataclass(frozen=True,slots=True)
class CommitTrace:
    trace_id:str; version_id:str; proposal_ids:tuple[str,...]; operation_addresses:tuple[str,...]; charge_keys:tuple[str,...]; provenance_roots:tuple[str,...]; rejected_proposals:tuple[tuple[str,str],...]=(); conflict_component_ids:tuple[str,...]=(); proof:"TraceDag|None"=None

@dataclass(frozen=True,slots=True)
class CommitResult:
    status:CommitStatus; version:WorldVersion; failure:FailureCertificate|None=None; trace:CommitTrace|None=None
    @property
    def committed(self)->bool:return self.status is CommitStatus.COMMITTED

@dataclass(frozen=True,slots=True)
class TimeAdvanceResult:
    version:WorldVersion;due_event_ids:tuple[str,...];trace:CommitTrace

def _failure(base:WorldVersion,kind:FailureKind,code:str,*witnesses:str,status:CommitStatus=CommitStatus.REJECTED)->CommitResult:
    return CommitResult(status,base,FailureCertificate(kind,code,str(base.version_id),witness_refs=tuple(witnesses),ruleset_hash=base.ruleset_hash,state_digest=base.state.root_hash))

def _address_value(state:WorldState,address:str)->object|None:
    prefix,_,key=address.partition(":")
    table={"fact":state.facts,"entity":state.entities,"relation":state.relations,"obligation":state.obligations,"claim":state.claims,"pair":state.benefit_burden_pairs,"event":state.scheduled_events,"lease":state.leases}
    return table.get(prefix,{}).get(key)

def _authorized(state:WorldState,proposal:Proposal)->bool:
    return any(g.actor_ref==proposal.actor_ref and g.action==proposal.permission_action and g.identity_epoch==proposal.actor_identity_epoch and g.permission_epoch==state.permission_epochs.get(g.grant_id,0) and all(op.address.startswith(g.target_prefix) for op in proposal.operations) for g in state.permissions)

class Kernel:
    """唯一权威提交入口；只解释封闭 StateOperation，不执行对象方法。"""
    def commit(self,base:WorldVersion,proposals:tuple[Proposal,...])->CommitResult:
        if not proposals:return _failure(base,FailureKind.INVALID,"empty-batch")
        if any(type(proposal) is not Proposal for proposal in proposals):return _failure(base,FailureKind.INVALID,"non-canonical-proposal-type")
        if any(p.branch_id!=str(base.branch_id) or p.base_version!=str(base.version_id) for p in proposals):return _failure(base,FailureKind.STALE,"branch-or-base-mismatch",status=CommitStatus.STALE)
        resolution=resolve_conflicts(proposals,channels=dict(base.state.channels),existing_leases=tuple(base.state.leases.values()))
        if not resolution.accepted:
            failure=FailureCertificate(FailureKind.CONFLICT,"semantic-write-conflict",str(base.version_id),witness_refs=tuple(x[0] for x in resolution.rejected),ruleset_hash=base.ruleset_hash,conflict_component_ids=tuple(component.component_id for component in resolution.components),state_digest=base.state.root_hash)
            return CommitResult(CommitStatus.REJECTED,base,failure)
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
        entities=dict(base.state.entities);facts=dict(base.state.facts);relations=dict(base.state.relations);obligations=dict(base.state.obligations);claims=dict(base.state.claims);scheduled_events=dict(base.state.scheduled_events);leases=dict(base.state.leases);pairs=dict(base.state.benefit_burden_pairs);permissions=list(base.state.permissions)
        epochs=dict(base.state.key_epochs);identity_epochs=dict(base.state.identity_epochs);facets=dict(base.state.identity_facets);consumed=set(base.state.consumed_identity_epochs);permission_epochs=dict(base.state.permission_epochs)
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
                    elif op.kind is OperationKind.DELETE_ENTITY:
                        if prefix!="entity" or op.value.ref.local_id!=key:raise ValueError("entity deletion semantic address mismatch")
                        current=entities.get(key)
                        if current is None or current.ref!=op.value.ref:raise ValueError("entity deletion target is stale or missing")
                        if any(op.value.ref in subject.controlled_entities for subject in base.state.subjects.values()):raise ValueError("controlled entity deletion requires control reconciliation")
                        if any(lease.holder_ref==key for lease in leases.values()):raise ValueError("leased entity deletion requires lease release")
                        attached=tuple(relation for relation in relations.values() if any(endpoint.ref==op.value.ref for endpoint in relation.endpoints))
                        blocked=tuple(relation.relation_id.local_id for relation in attached if relation.endpoint_deletion_policy is EndpointDeletionPolicy.REJECT)
                        if blocked:raise ValueError(f"entity deletion blocked by relations:{','.join(sorted(blocked))}")
                        related_facts=tuple(fact_id for fact_id,fact in facts.items() if fact.subject==op.value.ref)
                        if related_facts and not op.value.cascade_facts:raise ValueError("entity deletion requires explicit fact cascade")
                        for relation in attached:relations.pop(relation.relation_id.local_id,None)
                        for fact_id in related_facts:facts.pop(fact_id,None)
                        entities.pop(key)
                    elif op.kind is OperationKind.CREATE_RELATION:
                        if key in relations: raise ValueError("relation overwrite forbidden")
                        known={e.ref for e in entities.values()}
                        if any(endpoint.ref not in known for endpoint in op.value.endpoints): raise ValueError("relation has unknown endpoint")
                        relations[key]=op.value
                    elif op.kind is OperationKind.SET_PERMISSION:
                        if prefix!="permission" or op.value.grant_id!=key:raise ValueError("permission semantic address mismatch")
                        if any(grant.grant_id==key for grant in permissions):raise ValueError("permission overwrite forbidden")
                        if op.value.actor_ref not in base.state.subjects:raise ValueError("permission actor unknown")
                        if op.value.identity_epoch!=identity_epochs.get(op.value.actor_ref):raise ValueError("permission identity epoch mismatch")
                        if op.value.permission_epoch!=permission_epochs.get(key,0):raise ValueError("permission revocation epoch mismatch")
                        permissions.append(op.value)
                    elif op.kind is OperationKind.REVOKE_PERMISSION:
                        if prefix!="permission" or op.value.grant_id!=key:raise ValueError("permission revocation semantic address mismatch")
                        current_epoch=permission_epochs.get(key,0)
                        if op.value.expected_permission_epoch!=current_epoch:raise ValueError("stale permission revocation")
                        old_count=len(permissions);permissions=[grant for grant in permissions if grant.grant_id!=key]
                        if len(permissions)==old_count:raise ValueError("unknown/already revoked permission")
                        permission_epochs[key]=current_epoch+1
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
                    elif op.kind is OperationKind.SET_BENEFIT_BURDEN_PAIR:
                        if prefix!="pair" or op.value.pair_id!=key:raise ValueError("benefit/burden pair semantic address mismatch")
                        previous=pairs.get(key)
                        if previous is not None and previous!=op.value:raise ValueError("benefit/burden pair is immutable")
                        pairs[key]=op.value
                    elif op.kind is OperationKind.SET_SCHEDULED_EVENT:
                        if prefix!="event" or op.value.event_id!=key:raise ValueError("scheduled event semantic address mismatch")
                        previous=scheduled_events.get(key)
                        if previous is None:
                            if op.value.state is not ScheduledEventState.SCHEDULED:raise ValueError("new event must begin SCHEDULED")
                            if op.value.branch_id!=str(base.branch_id) or op.value.due_time<base.world_time:raise ValueError("invalid scheduled event branch/time")
                        else:
                            expected=transition_scheduled_event(previous,op.value.state,branch_id=str(base.branch_id),current_time=base.world_time)
                            if expected!=op.value:raise ValueError("scheduled event transition changed immutable fields")
                        scheduled_events[key]=op.value
                    elif op.kind is OperationKind.ACQUIRE_LEASE:
                        if prefix!="lease" or op.value.lease_id!=key:raise ValueError("lease semantic address mismatch")
                        if key in leases:raise ValueError("lease overwrite/replay forbidden")
                        if op.value.holder_ref not in base.state.subjects and op.value.holder_ref not in entities:raise ValueError("unknown lease holder")
                        leases[key]=op.value
                    elif op.kind is OperationKind.RELEASE_LEASE:
                        if prefix!="lease" or op.value.lease_id!=key:raise ValueError("lease release semantic address mismatch")
                        previous=leases.get(key)
                        if previous is None:raise ValueError("unknown/already released lease")
                        if previous.holder_ref!=op.value.holder_ref or previous.release_token!=op.value.release_token:raise ValueError("lease release token mismatch")
                        leases.pop(key)
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
        state=WorldState(entities=entities,subjects=base.state.subjects,facts=facts,relations=relations,balances=ledger.balances,obligations=obligations,permissions=tuple(permissions),identity_epochs=identity_epochs,identity_facets=facets,consumed_identity_epochs=frozenset(consumed),settled_charge_keys=ledger.settled_charge_keys,claims=claims,key_epochs=epochs,predicate_epoch=base.state.predicate_epoch+1,scheduled_events=scheduled_events,channels=base.state.channels,leases=leases,benefit_burden_pairs=pairs,permission_epochs=permission_epochs)
        try:validate_world_invariants(state,branch_id=str(base.branch_id))
        except InvariantViolation as exc:return _failure(base,FailureKind.INVALID,exc.witness.code,*exc.witness.refs)
        generation=base.generation+1;vid=VersionId(f"{base.branch_id}:{generation}:{state.root_hash[:16]}")
        version=WorldVersion(vid,base.branch_id,(base.version_id,),generation,base.world_time,state,content_id(tuple(p.proposal_id for p in active_proposals)),base.ruleset_hash,base.commit_seq+1)
        from ..tracing.proofs import TraceDag,TraceNode
        def trace_node(kind:str,payload:tuple[tuple[str,str],...],parents:tuple[str,...]=(),objective_only:bool=True)->TraceNode:
            node_id=content_id((kind,payload,parents,objective_only));return TraceNode(node_id,kind,payload,parents,objective_only)
        batch_node=trace_node("batch_received",(("base_version",str(base.version_id)),("ruleset_hash",base.ruleset_hash),("proposal_count",str(len(proposals)))),(),False)
        proof_nodes=[batch_node];conflict_nodes=[]
        for component in resolution.components:
            node=trace_node("conflict_component",(("component_id",component.component_id),("outcome",component.outcome),("winner",component.winner_group_id or "")),(batch_node.trace_id,));proof_nodes.append(node);conflict_nodes.append(node)
        operation_nodes=[]
        for proposal in active_proposals:
            validation=trace_node("proposal_validated",(("proposal_id",proposal.proposal_id),("actor",proposal.actor_ref),("permission",proposal.permission_action)),(batch_node.trace_id,));proof_nodes.append(validation)
            provenance=trace_node("provenance_validated",(("root",proposal.provenance_root),("node_count",str(len(proposal.provenance_nodes)))),(validation.trace_id,));proof_nodes.append(provenance)
            operation=trace_node("effects_applied",(("proposal_id",proposal.proposal_id),("addresses",",".join(op.address for op in proposal.operations)),("charge_count",str(len(proposal.charge_groups)))),(provenance.trace_id,));proof_nodes.append(operation);operation_nodes.append(operation)
        commit_node=trace_node("commit_succeeded",(("version_id",str(vid)),("state_root",state.root_hash),("generation",str(generation))),tuple(node.trace_id for node in operation_nodes+conflict_nodes),False);proof_nodes.append(commit_node)
        proof=TraceDag(tuple(proof_nodes))
        trace_values=(str(vid),tuple(p.proposal_id for p in active_proposals),resolution.rejected,tuple(c.component_id for c in resolution.components),commit_node.trace_id)
        trace=CommitTrace(content_id(trace_values),str(vid),tuple(p.proposal_id for p in active_proposals),tuple(op.address for p in active_proposals for op in p.operations),tuple(g.charge_key.key_id for p in active_proposals for g in p.charge_groups),tuple(p.provenance_root for p in active_proposals),resolution.rejected,tuple(c.component_id for c in resolution.components),proof)
        return CommitResult(CommitStatus.COMMITTED,version,None,trace)

    def advance_time(self,base:WorldVersion,to_time:Fraction,*,max_due_events:int=128)->TimeAdvanceResult:
        if not isinstance(to_time,Fraction):raise TypeError("world time requires Fraction")
        if to_time<=base.world_time:raise ValueError("world time must advance strictly")
        due=due_events(tuple(base.state.scheduled_events.values()),branch_id=str(base.branch_id),current_time=to_time,max_events=max_due_events)
        generation=base.generation+1
        cause=content_id(("advance-time",str(base.version_id),to_time,tuple(event.event_id for event in due)))
        version_id=VersionId(f"{base.branch_id}:{generation}:{content_id((base.state.root_hash,to_time))[:16]}")
        version=WorldVersion(version_id,base.branch_id,(base.version_id,),generation,to_time,base.state,cause,base.ruleset_hash,base.commit_seq+1)
        trace_values=(str(version_id),to_time,tuple(event.event_id for event in due))
        from ..tracing.proofs import TraceDag,TraceNode
        node_payload=(("version_id",str(version_id)),("world_time",str(to_time)),("due_events",",".join(event.event_id for event in due)))
        node=TraceNode(content_id(("time_advanced",node_payload,(),False)),"time_advanced",node_payload,(),False)
        trace=CommitTrace(content_id((trace_values,node.trace_id)),str(version_id),(),(),(),(),proof=TraceDag((node,)))
        return TimeAdvanceResult(version,tuple(event.event_id for event in due),trace)

class VersionStore:
    def __init__(self,head:WorldVersion):self._head=head
    @property
    def head(self)->WorldVersion:return self._head
    def submit(self,proposals:tuple[Proposal,...])->CommitResult:
        result=Kernel().commit(self._head,proposals)
        if result.committed:self._head=result.version
        return result
    def advance_time(self,to_time:Fraction,*,max_due_events:int=128)->TimeAdvanceResult:
        result=Kernel().advance_time(self._head,to_time,max_due_events=max_due_events);self._head=result.version;return result

AuthoritativeKernel=Kernel
