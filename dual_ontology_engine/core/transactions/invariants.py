from __future__ import annotations

from dataclasses import dataclass

from ..foundation.failures import CoreError
from ..world.versions import WorldState
from ..world.channels import validate_channel_leases


@dataclass(frozen=True, slots=True)
class InvariantWitness:
    code: str
    refs: tuple[str, ...]


class InvariantViolation(CoreError):
    def __init__(self, witness: InvariantWitness):
        super().__init__(witness.code)
        self.witness = witness


def validate_world_invariants(state: WorldState, *, branch_id: str) -> None:
    """校验所有受本次状态差分影响后仍必须成立的内核公理。"""

    entity_refs = {entity.ref for entity in state.entities.values()}
    subject_refs = {subject.ref for subject in state.subjects.values()}
    known_refs = entity_refs | subject_refs
    cross_branch = tuple(
        sorted(
            f"{ref.branch}:{ref.local_id}:{ref.epoch}"
            for ref in known_refs
            if str(ref.branch) != branch_id
        )
    )
    if cross_branch:
        raise InvariantViolation(InvariantWitness("cross-branch-world-ref", cross_branch))

    unknown_fact_subjects = tuple(
        sorted(
            fact.fact_id for fact in state.facts.values() if fact.subject not in known_refs
        )
    )
    if unknown_fact_subjects:
        raise InvariantViolation(
            InvariantWitness("fact-subject-not-in-world", unknown_fact_subjects)
        )

    unknown_controls = tuple(
        sorted(
            f"{subject.ref.local_id}->{controlled.local_id}"
            for subject in state.subjects.values()
            for controlled in subject.controlled_entities
            if controlled not in entity_refs
        )
    )
    if unknown_controls:
        raise InvariantViolation(
            InvariantWitness("controlled-entity-not-in-world", unknown_controls)
        )

    invalid_relations = []
    for relation in state.relations.values():
        if str(relation.relation_id.branch) != branch_id:
            invalid_relations.append(relation.relation_id.local_id)
            continue
        if any(endpoint.ref not in known_refs for endpoint in relation.endpoints):
            invalid_relations.append(relation.relation_id.local_id)
    if invalid_relations:
        raise InvariantViolation(
            InvariantWitness("relation-endpoint-not-in-world", tuple(sorted(invalid_relations)))
        )

    invalid_permissions = tuple(
        sorted(
            grant.grant_id
            for grant in state.permissions
            if grant.actor_ref not in state.subjects
            or state.identity_epochs.get(grant.actor_ref) != grant.identity_epoch
            or state.permission_epochs.get(grant.grant_id,0) != grant.permission_epoch
        )
    )
    if invalid_permissions:
        raise InvariantViolation(
            InvariantWitness("permission-not-bound-to-current-identity", invalid_permissions)
        )

    invalid_facet_owners = tuple(
        sorted(owner for owner in state.identity_facets if owner not in state.subjects)
    )
    if invalid_facet_owners:
        raise InvariantViolation(
            InvariantWitness("identity-facets-have-unknown-owner", invalid_facet_owners)
        )
    duplicate_facets = tuple(
        sorted(
            owner
            for owner, facets in state.identity_facets.items()
            if len({facet.name for facet in facets}) != len(facets)
        )
    )
    if duplicate_facets:
        raise InvariantViolation(
            InvariantWitness("duplicate-identity-facet", duplicate_facets)
        )

    unknown_obligation_bearers = tuple(
        sorted(
            obligation.obligation_id
            for obligation in state.obligations.values()
            if obligation.bearer_ref not in state.subjects
            and obligation.bearer_ref not in state.entities
        )
    )
    if unknown_obligation_bearers:
        raise InvariantViolation(
            InvariantWitness("obligation-bearer-not-in-world", unknown_obligation_bearers)
        )

    invalid_events = tuple(
        sorted(
            event.event_id
            for event in state.scheduled_events.values()
            if event.branch_id != branch_id
        )
    )
    if invalid_events:
        raise InvariantViolation(
            InvariantWitness("scheduled-event-branch-mismatch", invalid_events)
        )

    invalid_claims = tuple(
        sorted(
            claim_id
            for claim_id, claim in state.claims.items()
            if getattr(claim, "branch_id", branch_id) != branch_id
            or getattr(claim, "bearer_ref", None) not in state.subjects
            or getattr(claim, "benefit_burden_pair_id", None) not in state.benefit_burden_pairs
            or getattr(claim, "due_event_ref", None) not in state.scheduled_events
            or (
                getattr(claim, "beneficiary_or_sink_ref", None) is not None
                and claim.beneficiary_or_sink_ref not in state.subjects
                and claim.beneficiary_or_sink_ref not in state.entities
            )
            or (
                getattr(claim, "due_event_ref", None) in state.scheduled_events
                and state.scheduled_events[claim.due_event_ref].benefit_burden_pair_id
                != getattr(claim, "benefit_burden_pair_id", None)
            )
            or (
                getattr(claim, "benefit_burden_pair_id", None) in state.benefit_burden_pairs
                and state.benefit_burden_pairs[claim.benefit_burden_pair_id].bearer_ref
                != getattr(claim, "bearer_ref", None)
            )
        )
    )
    if invalid_claims:
        raise InvariantViolation(
            InvariantWitness("deferred-claim-owner-or-branch-invalid", invalid_claims)
        )

    invalid_pairs = tuple(
        sorted(
            pair_id
            for pair_id, pair in state.benefit_burden_pairs.items()
            if getattr(pair, "branch_id", branch_id) != branch_id
            or getattr(pair, "bearer_ref", None) not in state.subjects
            or (
                getattr(pair, "beneficiary_ref", None) is not None
                and pair.beneficiary_ref not in state.subjects
                and pair.beneficiary_ref not in state.entities
            )
        )
    )
    if invalid_pairs:
        raise InvariantViolation(
            InvariantWitness("benefit-burden-pair-invalid", invalid_pairs)
        )

    unbound_events = tuple(
        sorted(
            event.event_id
            for event in state.scheduled_events.values()
            if event.benefit_burden_pair_id is not None
            and event.benefit_burden_pair_id not in state.benefit_burden_pairs
        )
    )
    if unbound_events:
        raise InvariantViolation(
            InvariantWitness("scheduled-event-burden-pair-missing", unbound_events)
        )

    invalid_lease_holders = tuple(
        sorted(
            lease.lease_id
            for lease in state.leases.values()
            if lease.holder_ref not in state.subjects and lease.holder_ref not in state.entities
        )
    )
    if invalid_lease_holders:
        raise InvariantViolation(
            InvariantWitness("channel-lease-holder-invalid", invalid_lease_holders)
        )
    try:
        validate_channel_leases(dict(state.channels),tuple(state.leases.values()))
    except ValueError as exc:
        raise InvariantViolation(InvariantWitness("channel-lease-invariant",(str(exc),))) from exc
