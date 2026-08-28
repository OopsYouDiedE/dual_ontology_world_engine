from __future__ import annotations

import itertools
from dataclasses import replace
from fractions import Fraction

import pytest

from dual_ontology_engine.core.foundation.budgets import BudgetMeter, BudgetProfile
from dual_ontology_engine.core.rules.dependencies import (
    Dependency,
    Polarity,
    analyze_dependencies,
)
from dual_ontology_engine.core.rules.triggers import Trigger, monotone_closure
from dual_ontology_engine.core.rules.ir import (
    EffectKind,
    FactEffect,
    RuleIR,
    RulePhase,
    TargetMode,
)
from dual_ontology_engine.core.foundation.ids import BranchId, WorldRef
from dual_ontology_engine.core.foundation.provenance import make_provenance
from dual_ontology_engine.core.transactions.conflicts import resolve_conflicts
from dual_ontology_engine.core.transactions.proposals import (
    ConflictRank,
    ConflictStrategy,
    OperationKind,
    PredicateReadToken,
    StateOperation,
    make_proposal,
)
from dual_ontology_engine.core.transactions.commit import CommitStatus, Kernel
from dual_ontology_engine.core.transactions.intents import make_intent
from dual_ontology_engine.core.transactions.runtime import execute_intent
from dual_ontology_engine.core.world.entities import Entity, EntityDeletion, Fact, PermissionGrant, PermissionRevocation, Subject
from dual_ontology_engine.core.world.relations import EndpointDeletionPolicy, Relation, RelationEndpoint
from dual_ontology_engine.core.world.versions import (
    BranchForkPolicy,
    WorldState,
    fork_version,
    genesis,
)
from dual_ontology_engine.core.world.scopes import ScopeExpr, ScopeOp
from dual_ontology_engine.core.costs.atoms import make_component
from dual_ontology_engine.core.costs.planes import CostPlane
from dual_ontology_engine.core.costs.quotes import quote_cost
from dual_ontology_engine.core.costs.modifiers import (
    ModifierOp,
    StackMode,
    make_modifier,
)
from dual_ontology_engine.core.costs.solver import (
    PaymentAlternative,
    solve_bounded_payment_frontier,
    verify_solver_certificate,
)
from dual_ontology_engine.core.costs.settlement import settlement_plan
from dual_ontology_engine.core.costs.allocation import (
    ResponsibilityShare,
    allocate_compensation,
    make_compensable_damage,
)
from dual_ontology_engine.core.costs.claims import (
    ClaimState,
    make_benefit_burden_pair,
    make_claim,
    transition_claim,
)
from dual_ontology_engine.core.costs.formula import FormulaExpr,analyze_formula
from dual_ontology_engine.core.decisions.deliberator import DecisionContext, decide
from dual_ontology_engine.core.decisions.personality import PersonalityProfile, ValueTier
from dual_ontology_engine.core.decisions.reasons import (
    Reason,
    ReasonPolarity,
    grounded_acceptance,
)
from dual_ontology_engine.core.decisions.tree import ActionPath
from dual_ontology_engine.core.decisions.replanning import (
    PlanCandidate,
    ReplanPolicy,
    ReplanStatus,
    bounded_replan,
)
from dual_ontology_engine.core.decisions.hysteresis import (
    AutonomousTransition,
    ProgressWitnessKind,
    certify_autonomous_transitions,
)
from dual_ontology_engine.core.world.epistemic import EpistemicView
from dual_ontology_engine.core.world.events import (
    ScheduledEventState,
    make_scheduled_event,
    transition_scheduled_event,
)
from dual_ontology_engine.core.world.channels import (
    ChannelDefinition,
    LeasePreemptionRule,
    LeaseRelease,
    make_channel_lease,
    plan_lease_preemption,
)
from dual_ontology_engine.core.calibration.statistics import (
    empirical_cvar,
    empirical_mean,
    joint_confidence_delta,
    sample_variance,
    zero_failure_upper_bound,
)
from dual_ontology_engine.core.world.evidence import (
    BeliefDisposition,
    BeliefPolicy,
    EvidenceLedger,
    EvidencePolarity,
    make_evidence,
    update_belief,
)
from dual_ontology_engine.core.world.propagation import (
    PropagationEdge,
    PropagationPolicy,
    propagate,
)
from dual_ontology_engine.core.calibration.gates import rollout_behavior_change_rate
from dual_ontology_engine.core.calibration.scenarios import (
    PropertyCertificate,
    PropertyId,
    PropertyVerdict,
    RolloutOutcome,
)
from dual_ontology_engine.core.foundation.codec import content_id
from dual_ontology_engine.core.tracing.proofs import (
    ProjectionGrant,
    TraceDag,
    TraceNode,
    project_trace,
)
from dual_ontology_engine.core.transactions.conservation import (
    ConversionRule,
    make_conservation_potential,
    validate_conversion_domain,
)
from dual_ontology_engine.core.tracing.stagnation import ProgressSnapshot,StagnationKind,analyze_stagnation


def test_positive_monotone_scc_is_accepted_and_stratified() -> None:
    nodes = ("seed", "left", "right", "final")
    edges = (
        Dependency("seed", "left", Polarity.POSITIVE),
        Dependency("left", "right", Polarity.POSITIVE),
        Dependency("right", "left", Polarity.POSITIVE),
        Dependency("right", "final", Polarity.NEGATIVE),
    )

    analysis = analyze_dependencies(nodes, edges, frozenset({"left", "right"}))

    assert ("left", "right") in analysis.components
    assert analysis.strata == (
        (("seed",),),
        (("left", "right"),),
        (("final",),),
    )


def test_non_monotone_or_negative_scc_is_rejected() -> None:
    positive_cycle = (
        Dependency("a", "b", Polarity.POSITIVE),
        Dependency("b", "a", Polarity.POSITIVE),
    )
    with pytest.raises(ValueError, match="monotonicity"):
        analyze_dependencies(("a", "b"), positive_cycle, frozenset({"a"}))

    negative_cycle = (
        Dependency("a", "b", Polarity.POSITIVE),
        Dependency("b", "a", Polarity.NEGATIVE),
    )
    with pytest.raises(ValueError, match="negative dependency"):
        analyze_dependencies(("a", "b"), negative_cycle, frozenset({"a", "b"}))


def test_dependency_analysis_is_permutation_invariant() -> None:
    nodes = ("a", "b", "c")
    edges = (
        Dependency("a", "b", Polarity.POSITIVE),
        Dependency("b", "c", Polarity.POSITIVE),
    )
    expected = analyze_dependencies(nodes, edges, frozenset())
    for node_order in itertools.permutations(nodes):
        for edge_order in itertools.permutations(edges):
            assert analyze_dependencies(node_order, edge_order, frozenset()) == expected


def _trigger(rule: str, requires: tuple[str, ...], outputs: tuple[str, ...]) -> Trigger:
    return Trigger(rule, "target", "v1", "event", "PRE_COMMIT", requires, outputs)


def test_monotone_closure_uses_new_values_until_fixed_point() -> None:
    triggers = (
        _trigger("r3", ("c",), ("d",)),
        _trigger("r1", ("a",), ("b",)),
        _trigger("r2", ("b",), ("c",)),
    )
    profile = BudgetProfile(instructions=16, agenda_nodes=16, reserve=0)

    result = monotone_closure(
        frozenset({"a"}),
        triggers,
        frozenset({"a", "b", "c", "d"}),
        BudgetMeter(profile),
    )

    assert result.failure is None
    assert result.values == frozenset({"a", "b", "c", "d"})
    assert len(result.fired_reaction_keys) == 3
    assert result.meter.used_instructions == 3


def test_monotone_closure_is_permutation_invariant() -> None:
    triggers = (
        _trigger("r1", ("a",), ("b",)),
        _trigger("r2", ("a",), ("c",)),
        _trigger("r3", ("b", "c"), ("d",)),
    )
    profile = BudgetProfile(instructions=16, agenda_nodes=16, reserve=0)
    expected = None
    for order in itertools.permutations(triggers):
        result = monotone_closure(
            frozenset({"a"}),
            order,
            frozenset({"a", "b", "c", "d"}),
            BudgetMeter(profile),
        )
        projection = (result.values, result.fired_reaction_keys)
        expected = expected or projection
        assert projection == expected


def test_monotone_closure_budget_failure_is_atomic() -> None:
    triggers = (
        _trigger("r1", ("a",), ("b",)),
        _trigger("r2", ("b",), ("c",)),
    )
    profile = BudgetProfile(instructions=1, agenda_nodes=4, reserve=0)

    result = monotone_closure(
        frozenset({"a"}),
        triggers,
        frozenset({"a", "b", "c"}),
        BudgetMeter(profile),
    )

    assert result.failure is not None
    assert result.values == frozenset({"a"})


def _proposal(
    label: str,
    address: str,
    value: str,
    *,
    strategy: ConflictStrategy = ConflictStrategy.REJECT_COMPONENT,
    rank: ConflictRank = ConflictRank(),
    atomic_group_id: str = "",
):
    provenance = make_provenance(rule_id=f"rule:{label}", event_id="event")
    fact_id = address.partition(":")[2]
    fact = Fact(
        fact_id,
        WorldRef(BranchId("main"), "subject", 0),
        "state",
        value,
        provenance.provenance_id,
    )
    operation = StateOperation(
        OperationKind.SET_FACT, address, fact, provenance.provenance_id
    )
    return make_proposal(
        branch_id="main",
        base_version="main:0",
        actor_ref="subject",
        actor_identity_epoch=0,
        permission_action="write",
        preconditions=(),
        point_reads=(),
        predicate_reads=(),
        operations=(operation,),
        charge_groups=(),
        provenance_nodes=(provenance,),
        provenance_root=provenance.provenance_id,
        atomic_group_id=atomic_group_id,
        conflict_strategy=strategy,
        conflict_rank=rank,
    )


def test_conflicts_are_resolved_per_connected_component() -> None:
    left = _proposal("left", "fact:shared", "left")
    right = _proposal("right", "fact:shared", "right")
    unrelated = _proposal("other", "fact:other", "safe")

    resolution = resolve_conflicts((right, unrelated, left))

    assert resolution.accepted == (unrelated,)
    assert {proposal_id for proposal_id, _ in resolution.rejected} == {
        left.proposal_id,
        right.proposal_id,
    }
    assert len(resolution.components) == 1
    assert resolution.components[0].addresses == ("fact:shared",)


def test_conflict_dominance_requires_a_unique_componentwise_winner() -> None:
    winner = _proposal(
        "winner",
        "fact:shared",
        "winner",
        strategy=ConflictStrategy.DOMINANCE,
        rank=ConflictRank(authority=2, specificity=3, intervention=1),
    )
    loser = _proposal(
        "loser",
        "fact:shared",
        "loser",
        strategy=ConflictStrategy.DOMINANCE,
        rank=ConflictRank(authority=1, specificity=2, intervention=1),
    )

    resolution = resolve_conflicts((loser, winner))

    assert resolution.accepted == (winner,)
    assert resolution.components[0].winner_group_id == winner.proposal_id


def test_conflict_resolution_is_permutation_invariant() -> None:
    proposals = (
        _proposal("one", "fact:shared", "one"),
        _proposal("two", "fact:shared", "two"),
        _proposal("three", "fact:other", "three"),
    )
    expected = resolve_conflicts(proposals)
    for order in itertools.permutations(proposals):
        assert resolve_conflicts(order) == expected


def test_conflict_inside_atomic_group_rejects_only_that_group() -> None:
    left = _proposal("left", "fact:shared", "left", atomic_group_id="atomic")
    right = _proposal("right", "fact:shared", "right", atomic_group_id="atomic")
    unrelated = _proposal("other", "fact:other", "safe")

    resolution = resolve_conflicts((left, unrelated, right))

    assert resolution.accepted == (unrelated,)
    assert resolution.components[0].outcome == "invalid_atomic_group"


def test_kernel_commits_unrelated_components_after_conflict_rejection() -> None:
    left = _proposal("left", "fact:shared", "left")
    right = _proposal("right", "fact:shared", "right")
    unrelated = _proposal("other", "fact:other", "safe")
    subject_ref = WorldRef(BranchId("main"), "subject", 0)
    subject = Subject(subject_ref, 0, "personality")
    permission = PermissionGrant("grant", "subject", "write", "fact:", 0, "root")
    state = WorldState(
        subjects={"subject": subject},
        permissions=(permission,),
        identity_epochs={"subject": 0},
    )
    base = replace(genesis("main"), state=state)

    result = Kernel().commit(base, (left, right, unrelated))

    assert result.committed
    assert set(result.version.state.facts) == {"other"}
    assert result.trace is not None
    assert result.trace.proposal_ids == (unrelated.proposal_id,)
    assert {proposal_id for proposal_id, _ in result.trace.rejected_proposals} == {
        left.proposal_id,
        right.proposal_id,
    }


def test_intent_rule_closure_compiles_and_commits_deterministically() -> None:
    subject_ref = WorldRef(BranchId("main"), "subject", 0)
    subject = Subject(subject_ref, 0, "personality")
    permission = PermissionGrant("grant", "subject", "act", "fact:", 0, "root")
    state = WorldState(
        subjects={"subject": subject},
        permissions=(permission,),
        identity_epochs={"subject": 0},
    )
    base = replace(genesis("main"), state=state)
    intent = make_intent(
        branch_id="main",
        based_on_version="main:0",
        actor_ref="subject",
        actor_identity_epoch=0,
        action="focus",
        scope=ScopeExpr(ScopeOp.EXPLICIT, explicit_refs=(subject_ref,)),
    )
    derive = RuleIR(
        "derive-ready",
        "focus",
        RulePhase.PRE_COMMIT,
        ("intent.action:focus",),
        ("ready",),
        (),
        "act",
    )
    apply = RuleIR(
        "apply-focused",
        "focus",
        RulePhase.PRE_COMMIT,
        ("ready",),
        ("focused-applied",),
        (
            FactEffect(
                EffectKind.SET_FACT,
                TargetMode.ACTOR,
                "focused",
                literal_value="yes",
            ),
        ),
        "act",
    )
    post = RuleIR(
        "announce-after",
        "focus",
        RulePhase.POST_COMMIT,
        ("focused-applied",),
        ("announcement-due",),
        (),
        "act",
        follow_up_action="announce",
    )

    first = execute_intent(base, intent, (post, apply, derive))
    second = execute_intent(base, intent, (derive, post, apply))

    assert first.committed and second.committed
    assert first.commit == second.commit
    assert tuple(first.commit.version.state.facts.values())[0].predicate == "focused"
    assert first.follow_up_rule_ids == ("announce-after",)
    assert len(first.follow_up_intents)==1
    assert first.follow_up_intents[0].action=="announce"
    assert first.follow_up_intents[0].based_on_version==str(first.commit.version.version_id)
    assert first.trace == second.trace


def test_reason_graph_uses_grounded_fixed_point() -> None:
    first = Reason("first", "p", "d", ReasonPolarity.SUPPORT, 1, ())
    second = Reason("second", "p", "d", ReasonPolarity.OPPOSE, 1, (), ("first",))
    third = Reason("third", "p", "d", ReasonPolarity.SUPPORT, 1, (), ("second",))

    result = grounded_acceptance((third, second, first))

    assert tuple(reason.reason_id for reason in result.accepted) == ("first", "third")
    assert tuple(reason.reason_id for reason in result.rejected) == ("second",)


def _belief_quote(path_id: str, dimensions: tuple[tuple[str, int], ...]):
    components = tuple(
        make_component(
            plane=CostPlane.BELIEF,
            dimension=dimension,
            amount=Fraction(amount),
            bearer_ref="subject",
            sink_ref=None,
            authority_domain="belief",
            origin_rule="rule",
        )
        for dimension, amount in dimensions
    )
    return quote_cost(
        branch_id="main",
        base_version="v1",
        model_version="model",
        activation_epoch=0,
        subject_id="subject",
        epistemic_version="e1",
        personality_version="personality",
        path_id=path_id,
        plane=CostPlane.BELIEF,
        components=components,
        modifiers=(),
        proof_roots=("proof",),
        current_epoch=0,
    )


def test_decision_uses_value_tiers_before_lower_priority_costs() -> None:
    paths = (
        ActionPath("protect", "protect", ()),
        ActionPath("profit", "profit", ()),
    )
    quotes = (
        _belief_quote("protect", (("duty", 1), ("effort", 10))),
        _belief_quote("profit", (("duty", 2), ("effort", 0))),
    )
    view = EpistemicView("subject", "e1", {}, {}, ("protect", "profit"))
    context = DecisionContext("subject", "goal", "e1", "personality", 0)
    personality = PersonalityProfile(
        "personality", (ValueTier(0, ("duty",)), ValueTier(1, ("effort",)))
    )

    draft, proof = decide(view, context, personality, paths, quotes, ())

    assert draft.path_id == "protect"
    assert proof.alternative_path_ids == ()


def test_decision_uses_declared_reversibility_fallback_not_container_order() -> None:
    cautious = ActionPath("cautious", "probe", (), reversibility_rank=2)
    rash = ActionPath("rash", "rush", (), reversibility_rank=1)
    quotes = (
        _belief_quote("cautious", (("risk", 1),)),
        _belief_quote("rash", (("risk", 1),)),
    )
    view = EpistemicView("subject", "e1", {}, {}, ("probe", "rush"))
    context = DecisionContext("subject", "goal", "e1", "personality", 0)
    personality = PersonalityProfile("personality", (ValueTier(0, ("risk",)),))

    first = decide(view, context, personality, (rash, cautious), quotes, ())
    second = decide(view, context, personality, (cautious, rash), tuple(reversed(quotes)), ())

    assert first == second
    assert first[0].path_id == "cautious"
    assert first[1].fallback_kind == "reversibility"


def test_bounded_payment_solver_returns_verifiable_pareto_certificate() -> None:
    alternatives = (
        PaymentAlternative("mana", (("mana", Fraction(10)),)),
        PaymentAlternative(
            "mixed", (("mana", Fraction(4)), ("material", Fraction(2)))
        ),
        PaymentAlternative("wasteful", (("mana", Fraction(12)),)),
        PaymentAlternative("unpayable", (("material", Fraction(9)),)),
    )
    available = {"mana": Fraction(15), "material": Fraction(3)}
    reserve = {"mana": Fraction(3)}

    result = solve_bounded_payment_frontier(
        tuple(reversed(alternatives)), available=available, reserve=reserve
    )

    assert tuple(item.alternative_id for item in result.frontier) == ("mana", "mixed")
    assert ("wasteful", "mana") in result.certificate.dominance_witnesses
    assert result.certificate.infeasible_witnesses == (
        ("unpayable", "material", Fraction(9), Fraction(3)),
    )
    assert verify_solver_certificate(
        alternatives,
        available=available,
        reserve=reserve,
        certificate=result.certificate,
    )


def test_deferred_claim_can_be_persisted_and_moved_to_due_once() -> None:
    subject_ref = WorldRef(BranchId("main"), "subject", 0)
    state = WorldState(
        subjects={"subject": Subject(subject_ref, 0, "personality")},
        permissions=(PermissionGrant("grant", "subject", "claim", "", 0, "root"),),
        identity_epochs={"subject": 0},
    )
    base = replace(genesis("main"), state=state)
    provenance = make_provenance(rule_id="rule", event_id="benefit-event")
    pair = make_benefit_burden_pair(
        branch_id="main", benefit_event_ref="benefit-event", bearer_ref="subject",
        beneficiary_ref=None, origin_rule_id="rule", provenance_id=provenance.provenance_id,
    )
    due_event = make_scheduled_event(
        branch_id="main", due_time=Fraction(1), event_kind="claim-due", payload=(),
        state=ScheduledEventState.SCHEDULED, provenance_id=provenance.provenance_id,
        benefit_burden_pair_id=pair.pair_id,
    )
    claim = make_claim(
        branch_id="main",
        bearer_ref="subject",
        beneficiary_or_sink_ref=None,
        dimension="mana",
        amount_expr=FormulaExpr("CONST", literal=Fraction(5)),
        due_event_ref=due_event.event_id,
        state=ClaimState.PENDING,
        benefit_burden_pair_id=pair.pair_id,
        origin_rule_id="rule",
    )
    create = make_proposal(
        branch_id="main",
        base_version="main:0",
        actor_ref="subject",
        actor_identity_epoch=0,
        permission_action="claim",
        preconditions=(),
        point_reads=(),
        predicate_reads=(),
        operations=(
            StateOperation(OperationKind.SET_BENEFIT_BURDEN_PAIR, f"pair:{pair.pair_id}", pair, provenance.provenance_id),
            StateOperation(OperationKind.SET_SCHEDULED_EVENT, f"event:{due_event.event_id}", due_event, provenance.provenance_id),
            StateOperation(OperationKind.SET_CLAIM, f"claim:{claim.claim_id}", claim, provenance.provenance_id),
        ),
        charge_groups=(),
        provenance_nodes=(provenance,),
        provenance_root=provenance.provenance_id,
    )

    created = Kernel().commit(base, (create,))
    assert created.committed

    due_claim = transition_claim(claim, ClaimState.DUE, branch_id="main", event_ref=due_event.event_id)
    due_provenance = make_provenance(rule_id="rule", event_id=due_event.event_id)
    mark_due = make_proposal(
        branch_id="main",
        base_version=str(created.version.version_id),
        actor_ref="subject",
        actor_identity_epoch=0,
        permission_action="claim",
        preconditions=(),
        point_reads=(),
        predicate_reads=(),
        operations=(StateOperation(OperationKind.SET_CLAIM, f"claim:{claim.claim_id}", due_claim, due_provenance.provenance_id),),
        charge_groups=(),
        provenance_nodes=(due_provenance,),
        provenance_root=due_provenance.provenance_id,
    )

    due = Kernel().commit(created.version, (mark_due,))
    assert due.committed
    assert due.version.state.claims[claim.claim_id].state is ClaimState.DUE


def _modifier(source: str, operation: ModifierOp, value: int, read: int, write: int, *, expiry=None,stack_mode=StackMode.ACCUMULATE,marker="path",max_applications=None):
    return make_modifier(
        branch_id="main",
        plane=CostPlane.BELIEF,
        selector_id="path",
        dimension="effort",
        operation=operation,
        value=Fraction(value),
        read_stage=read,
        write_stage=write,
        stack_mode=stack_mode,
        stack_group="group",
        authority_domain="belief",
        active_from_epoch=0,
        expires_after_epoch=expiry,
        source_id=source,
        provenance_id="proof",
        application_marker=marker,
        max_applications=max_applications,
    )


def test_cost_modifiers_execute_by_declared_stage() -> None:
    component = make_component(
        plane=CostPlane.BELIEF,
        dimension="effort",
        amount=Fraction(10),
        bearer_ref="subject",
        sink_ref=None,
        authority_domain="belief",
        origin_rule="rule",
    )
    subtract = _modifier("subtract", ModifierOp.ADD, -5, 0, 1)
    double = _modifier("double", ModifierOp.MULTIPLY, 2, 1, 2)

    quote = quote_cost(
        branch_id="main",
        base_version="v1",
        model_version="model",
        activation_epoch=0,
        subject_id="subject",
        epistemic_version="e1",
        personality_version="personality",
        path_id="path",
        plane=CostPlane.BELIEF,
        components=(component,),
        modifiers=(double, subtract),
        proof_roots=("proof",),
        current_epoch=0,
    )

    assert quote.final_components[0].amount == Fraction(10)


def test_same_semantic_modifier_application_is_coalesced() -> None:
    component = make_component(
        plane=CostPlane.BELIEF,
        dimension="effort",
        amount=Fraction(10),
        bearer_ref="subject",
        sink_ref=None,
        authority_domain="belief",
        origin_rule="rule",
    )
    first = _modifier("same-source", ModifierOp.ADD, 2, 0, 1,stack_mode=StackMode.ONCE_PER_SOURCE)
    duplicate = _modifier("same-source", ModifierOp.ADD, 2, 0, 1, expiry=10,stack_mode=StackMode.ONCE_PER_SOURCE)

    quote = quote_cost(
        branch_id="main",
        base_version="v1",
        model_version="model",
        activation_epoch=0,
        subject_id="subject",
        epistemic_version="e1",
        personality_version="personality",
        path_id="path",
        plane=CostPlane.BELIEF,
        components=(component,),
        modifiers=(duplicate, first),
        proof_roots=("proof",),
        current_epoch=0,
    )

    assert quote.final_components[0].amount == Fraction(12)
    assert len(quote.applied_modifier_ids) == 1


def test_accumulate_modifiers_remain_distinct_occurrences() -> None:
    component = make_component(
        plane=CostPlane.BELIEF,
        dimension="effort",
        amount=Fraction(10),
        bearer_ref="subject",
        sink_ref=None,
        authority_domain="belief",
        origin_rule="rule",
    )
    first = _modifier("same-source", ModifierOp.ADD, 2, 0, 1)
    second = _modifier("same-source", ModifierOp.ADD, 2, 0, 1, expiry=10)
    quote = quote_cost(
        branch_id="main",base_version="v1",model_version="model",activation_epoch=0,
        subject_id="subject",epistemic_version="e1",personality_version="personality",
        path_id="path",plane=CostPlane.BELIEF,components=(component,),modifiers=(first,second),
        proof_roots=("proof",),current_epoch=0,
    )
    assert quote.final_components[0].amount == Fraction(14)
    assert len(quote.applied_modifier_ids) == 2


def test_once_per_segment_uses_explicit_segment_marker() -> None:
    component = make_component(plane=CostPlane.BELIEF,dimension="effort",amount=Fraction(10),bearer_ref="subject",sink_ref=None,authority_domain="belief",origin_rule="rule")
    first=_modifier("zone",ModifierOp.ADD,2,0,1,stack_mode=StackMode.ONCE_PER_SEGMENT,marker="segment:a")
    second=_modifier("zone",ModifierOp.ADD,2,0,1,expiry=10,stack_mode=StackMode.ONCE_PER_SEGMENT,marker="segment:b")
    quote=quote_cost(branch_id="main",base_version="v1",model_version="model",activation_epoch=0,subject_id="subject",epistemic_version="e1",personality_version="personality",path_id="path",plane=CostPlane.BELIEF,components=(component,),modifiers=(second,first),proof_roots=("proof",),current_epoch=0)
    assert quote.final_components[0].amount==14


def test_max_n_fails_closed_instead_of_truncating_by_order() -> None:
    component = make_component(plane=CostPlane.BELIEF,dimension="effort",amount=Fraction(10),bearer_ref="subject",sink_ref=None,authority_domain="belief",origin_rule="rule")
    modifiers=tuple(_modifier(f"source:{index}",ModifierOp.ADD,index,0,1,stack_mode=StackMode.MAX_N,max_applications=2) for index in (1,2,3))
    failed=False
    try:quote_cost(branch_id="main",base_version="v1",model_version="model",activation_epoch=0,subject_id="subject",epistemic_version="e1",personality_version="personality",path_id="path",plane=CostPlane.BELIEF,components=(component,),modifiers=modifiers,proof_roots=("proof",),current_epoch=0)
    except ValueError as exc:failed="MAX_N" in str(exc)
    assert failed


def test_scheduled_event_is_due_only_after_authoritative_time_advance() -> None:
    subject_ref = WorldRef(BranchId("main"), "subject", 0)
    state = WorldState(
        subjects={"subject": Subject(subject_ref, 0, "personality")},
        permissions=(PermissionGrant("grant", "subject", "event", "event:", 0, "root"),),
        identity_epochs={"subject": 0},
    )
    base = replace(genesis("main"), state=state)
    event = make_scheduled_event(
        branch_id="main",
        due_time=Fraction(5),
        event_kind="claim_due",
        payload=(("claim", "claim-id"),),
        state=ScheduledEventState.SCHEDULED,
        provenance_id="proof",
        benefit_burden_pair_id=None,
    )
    provenance = make_provenance(rule_id="rule", event_id="benefit")
    schedule = make_proposal(
        branch_id="main",
        base_version="main:0",
        actor_ref="subject",
        actor_identity_epoch=0,
        permission_action="event",
        preconditions=(),
        point_reads=(),
        predicate_reads=(),
        operations=(StateOperation(OperationKind.SET_SCHEDULED_EVENT, f"event:{event.event_id}", event, provenance.provenance_id),),
        charge_groups=(),
        provenance_nodes=(provenance,),
        provenance_root=provenance.provenance_id,
    )
    scheduled = Kernel().commit(base, (schedule,))
    assert scheduled.committed

    before_due = Kernel().advance_time(scheduled.version, Fraction(4))
    assert before_due.due_event_ids == ()
    at_due = Kernel().advance_time(before_due.version, Fraction(5))
    assert at_due.due_event_ids == (event.event_id,)

    fired_event = transition_scheduled_event(
        event,
        ScheduledEventState.FIRED,
        branch_id="main",
        current_time=Fraction(5),
    )
    fire_provenance = make_provenance(rule_id="rule", event_id=event.event_id)
    fire = make_proposal(
        branch_id="main",
        base_version=str(at_due.version.version_id),
        actor_ref="subject",
        actor_identity_epoch=0,
        permission_action="event",
        preconditions=(),
        point_reads=(),
        predicate_reads=(),
        operations=(StateOperation(OperationKind.SET_SCHEDULED_EVENT, f"event:{event.event_id}", fired_event, fire_provenance.provenance_id),),
        charge_groups=(),
        provenance_nodes=(fire_provenance,),
        provenance_root=fire_provenance.provenance_id,
    )
    fired = Kernel().commit(at_due.version, (fire,))
    assert fired.committed
    assert fired.version.state.scheduled_events[event.event_id].state is ScheduledEventState.FIRED


def _lease_proposal(base_version: str, lease, provenance, value=None):
    operation = StateOperation(
        OperationKind.ACQUIRE_LEASE if value is None else OperationKind.RELEASE_LEASE,
        f"lease:{lease.lease_id}",
        lease if value is None else value,
        provenance.provenance_id,
    )
    return make_proposal(
        branch_id="main",
        base_version=base_version,
        actor_ref="subject",
        actor_identity_epoch=0,
        permission_action="lease",
        preconditions=(),
        point_reads=(),
        predicate_reads=(),
        operations=(operation,),
        charge_groups=(),
        provenance_nodes=(provenance,),
        provenance_root=provenance.provenance_id,
    )


def test_channel_capacity_conflict_rejects_only_lease_component() -> None:
    subject_ref = WorldRef(BranchId("main"), "subject", 0)
    channel = ChannelDefinition("focus", (("slots", Fraction(1)),))
    state = WorldState(
        subjects={"subject": Subject(subject_ref, 0, "personality")},
        permissions=(
            PermissionGrant("lease-grant", "subject", "lease", "lease:", 0, "root"),
            PermissionGrant("write-grant", "subject", "write", "fact:", 0, "root"),
        ),
        identity_epochs={"subject": 0},
        channels={"focus": channel},
    )
    base = replace(genesis("main"), state=state)
    first = make_channel_lease(
        channel_id="focus",
        holder_ref="subject",
        usage=(("slots", Fraction(1)),),
        compatibility_class="spell",
        release_token="release-first",
        acquired_epoch=0,
    )
    second = make_channel_lease(
        channel_id="focus",
        holder_ref="subject",
        usage=(("slots", Fraction(1)),),
        compatibility_class="spell",
        release_token="release-second",
        acquired_epoch=0,
    )
    first_provenance = make_provenance(rule_id="first", event_id="event")
    second_provenance = make_provenance(rule_id="second", event_id="event")
    unrelated = _proposal("other", "fact:other", "safe")

    result = Kernel().commit(
        base,
        (
            _lease_proposal("main:0", first, first_provenance),
            unrelated,
            _lease_proposal("main:0", second, second_provenance),
        ),
    )

    assert result.committed
    assert not result.version.state.leases
    assert set(result.version.state.facts) == {"other"}


def test_channel_lease_release_requires_bound_token() -> None:
    subject_ref = WorldRef(BranchId("main"), "subject", 0)
    channel = ChannelDefinition("focus", (("slots", Fraction(1)),))
    state = WorldState(
        subjects={"subject": Subject(subject_ref, 0, "personality")},
        permissions=(PermissionGrant("grant", "subject", "lease", "lease:", 0, "root"),),
        identity_epochs={"subject": 0},
        channels={"focus": channel},
    )
    base = replace(genesis("main"), state=state)
    lease = make_channel_lease(
        channel_id="focus",
        holder_ref="subject",
        usage=(("slots", Fraction(1)),),
        compatibility_class="spell",
        release_token="secret",
        acquired_epoch=0,
    )
    provenance = make_provenance(rule_id="rule", event_id="acquire")
    acquired = Kernel().commit(base, (_lease_proposal("main:0", lease, provenance),))
    assert acquired.committed

    release_provenance = make_provenance(rule_id="rule", event_id="release")
    wrong = LeaseRelease(lease.lease_id, "subject", "wrong")
    rejected = Kernel().commit(
        acquired.version,
        (_lease_proposal(str(acquired.version.version_id), lease, release_provenance, wrong),),
    )
    assert not rejected.committed
    assert lease.lease_id in rejected.version.state.leases

    correct = LeaseRelease(lease.lease_id, "subject", "secret")
    released = Kernel().commit(
        acquired.version,
        (_lease_proposal(str(acquired.version.version_id), lease, release_provenance, correct),),
    )
    assert released.committed
    assert lease.lease_id not in released.version.state.leases


def test_exact_tail_statistics_report_effective_sample_mass() -> None:
    values = tuple(Fraction(value) for value in (1, 2, 3, 4))

    assert empirical_mean(values) == Fraction(5, 2)
    assert sample_variance(values) == Fraction(5, 3)
    tail = empirical_cvar(values, Fraction(3, 4))
    assert tail.quantile == Fraction(3)
    assert tail.cvar == Fraction(4)
    assert tail.effective_tail_samples == Fraction(1)

    extreme = empirical_cvar(values, Fraction(7, 8))
    assert extreme.cvar == Fraction(4)
    assert extreme.effective_tail_samples == Fraction(1, 2)


def test_joint_confidence_and_zero_failure_bound_are_explicitly_conservative() -> None:
    allocated = joint_confidence_delta(
        Fraction(1, 20), models=2, metrics=5, scenario_families=4
    )
    assert allocated == Fraction(1, 800)
    assert zero_failure_upper_bound(1_000, Fraction(1, 20)) < zero_failure_upper_bound(
        100, Fraction(1, 20)
    )
    assert zero_failure_upper_bound(100, Fraction(1, 20)) > 0


def test_copied_or_repeated_evidence_does_not_increase_independent_weight() -> None:
    root = make_evidence(
        proposition="door-open",
        polarity=EvidencePolarity.SUPPORTS,
        independent_source_id="camera",
    )
    copy = make_evidence(
        proposition="door-open",
        polarity=EvidencePolarity.SUPPORTS,
        independent_source_id=None,
        parent_ids=(root.evidence_id,),
    )
    repeated_copy = make_evidence(
        proposition="door-open",
        polarity=EvidencePolarity.SUPPORTS,
        independent_source_id=None,
        parent_ids=(root.evidence_id, copy.evidence_id),
    )

    assessment = EvidenceLedger((repeated_copy, copy, root)).assess("door-open")

    assert assessment.supporting_sources == ("camera",)
    assert assessment.lower == assessment.upper == Fraction(1)


def test_intervention_evidence_is_not_reused_for_unintervened_hypothesis() -> None:
    observed = make_evidence(
        proposition="switch-causes-light",
        polarity=EvidencePolarity.SUPPORTS,
        independent_source_id="experiment",
        intervention_context="do(switch)",
    )
    ledger = EvidenceLedger((observed,))

    ordinary = ledger.assess("switch-causes-light")
    intervened = ledger.assess(
        "switch-causes-light", intervention_context="do(switch)"
    )

    assert (ordinary.lower, ordinary.upper) == (Fraction(), Fraction(1))
    assert intervened.lower == intervened.upper == Fraction(1)


def test_belief_update_preserves_conflict_and_uses_two_thresholds() -> None:
    support = make_evidence(
        proposition="safe",
        polarity=EvidencePolarity.SUPPORTS,
        independent_source_id="same-origin",
    )
    oppose = make_evidence(
        proposition="safe",
        polarity=EvidencePolarity.OPPOSES,
        independent_source_id="same-origin",
    )
    assessment = EvidenceLedger((support, oppose)).assess("safe")
    policy = BeliefPolicy(Fraction(3, 4), Fraction(1, 4))

    assert update_belief(BeliefDisposition.UNKNOWN, assessment, policy) is BeliefDisposition.CONFLICTED


def _rollout(scenario_id: str, effect: int) -> RolloutOutcome:
    certificates = (
        PropertyCertificate(PropertyId.CONSERVATION, PropertyVerdict.PROVED, ()),
    )
    values = (
        scenario_id,
        "family",
        "model",
        1,
        (Fraction(effect),),
        (f"cost:{effect}",),
        certificates,
        (),
        0,
        0,
        1,
        False,
    )
    return RolloutOutcome(content_id(values), *values)


def test_behavior_gate_measures_outcomes_not_parameter_distance() -> None:
    incumbent = (_rollout("one", 1), _rollout("two", 2))
    candidate = (_rollout("one", 1), _rollout("two", 3))

    assert rollout_behavior_change_rate(incumbent, candidate) == Fraction(1, 2)


def test_common_positive_potential_certifies_non_amplifying_conversions() -> None:
    potential = make_conservation_potential(
        (("a", Fraction(1)), ("b", Fraction(2)))
    )
    rules = (
        ConversionRule("a-to-b", (("a", Fraction(2)),), (("b", Fraction(1)),)),
        ConversionRule("b-to-a", (("b", Fraction(1)),), (("a", Fraction(2)),)),
    )

    certificate = validate_conversion_domain(tuple(reversed(rules)), potential)

    assert tuple(witness.rule_id for witness in certificate.witnesses) == (
        "a-to-b",
        "b-to-a",
    )
    assert all(
        witness.output_potential <= witness.input_potential
        for witness in certificate.witnesses
    )


def test_conversion_growth_requires_explicit_external_quota() -> None:
    potential = make_conservation_potential((("mana", Fraction(1)),))
    growth = ConversionRule(
        "growth", (("mana", Fraction(1)),), (("mana", Fraction(2)),)
    )
    failed = False
    try:
        validate_conversion_domain((growth,), potential)
    except ValueError as exc:
        failed = "increases common potential" in str(exc)
    assert failed

    authorized = ConversionRule(
        "mint",
        (),
        (("mana", Fraction(2)),),
        external_authority_ref="world:source",
        external_potential_quota=Fraction(2),
    )
    assert validate_conversion_domain((authorized,), potential).witnesses[0].authorized_external_potential == 2


def test_formula_analysis_registers_dependencies_and_monotonicity() -> None:
    expression=FormulaExpr("ADD",(FormulaExpr("FACT",literal="distance"),FormulaExpr("PARAM",literal="rate")))
    analysis=analyze_formula(expression,allowed_parameters=frozenset({"rate"}),allowed_facts=frozenset({"distance"}),allowed_finite_sets=frozenset(),require_monotone=True)
    assert analysis.fact_keys==("distance",)
    assert analysis.parameter_keys==("rate",)
    assert analysis.monotone


def test_formula_analysis_rejects_selected_cost_self_reference() -> None:
    expression=FormulaExpr("FACT",literal="$final_quote")
    failed=False
    try:analyze_formula(expression)
    except ValueError as exc:failed="self-reference" in str(exc)
    assert failed


def test_stagnation_analyzer_is_read_only_and_reports_repeated_structure() -> None:
    history=tuple(ProgressSnapshot(f"v{index}",Fraction(index),candidate_path_ids=("wait",),reason_ids=("same-reason",)) for index in range(4))
    report=analyze_stagnation(history,window=4)
    assert report.stagnant
    assert set(report.kinds)=={StagnationKind.NO_STATE_PROGRESS,StagnationKind.NO_NEW_EVIDENCE,StagnationKind.CANDIDATE_STASIS,StagnationKind.REPEATED_REASONS,StagnationKind.TASK_INACTIVE}
    assert report.repeated_candidate_path_ids==("wait",)


def test_branch_fork_remaps_world_references_and_rebuilds_permissions() -> None:
    old_ref = WorldRef(BranchId("main"), "hero", 2)
    grant = PermissionGrant("grant", "actor", "move", "hero", 0, "source")
    base = replace(
        genesis("main"),
        state=WorldState(
            entities={"hero": Entity(old_ref, "person")},
            subjects={"hero": Subject(old_ref, 2, "p1", (old_ref,))},
            facts={"alive": Fact("alive", old_ref, "alive", "true", "source")},
            permissions=(grant,),
        ),
    )

    forked = fork_version(base, "possibility-a")

    assert forked.parents == (base.version_id,)
    assert forked.branch_id == BranchId("possibility-a")
    assert forked.state.entities["hero"].ref.branch == BranchId("possibility-a")
    assert forked.state.subjects["hero"].controlled_entities[0].branch == BranchId("possibility-a")
    assert forked.state.facts["alive"].subject.branch == BranchId("possibility-a")
    assert forked.state.permissions == ()


def test_branch_fork_requires_reconciliation_for_branch_local_events() -> None:
    event = make_scheduled_event(
        branch_id="main",
        due_time=Fraction(1),
        event_kind="alarm",
        payload=(),
        state=ScheduledEventState.SCHEDULED,
        provenance_id="source",
    )
    base = replace(
        genesis("main"),
        state=WorldState(scheduled_events={event.event_id: event}),
    )

    failed = False
    try:
        fork_version(base, "possibility-a", policy=BranchForkPolicy())
    except ValueError as exc:
        failed = "explicit reconciliation" in str(exc)
    assert failed


def test_delayed_cost_requires_atomic_benefit_pair_event_and_claim() -> None:
    subject_ref = WorldRef(BranchId("main"), "subject", 0)
    subject = Subject(subject_ref, 0, "personality")
    permission = PermissionGrant("grant", "subject", "defer", "", 0, "root")
    base = replace(
        genesis("main"),
        state=WorldState(
            subjects={"subject": subject},
            permissions=(permission,),
            identity_epochs={"subject": 0},
        ),
    )
    provenance = make_provenance(rule_id="delayed-cost", event_id="benefit")
    pair = make_benefit_burden_pair(
        branch_id="main",
        benefit_event_ref="benefit",
        bearer_ref="subject",
        beneficiary_ref="subject",
        origin_rule_id="delayed-cost",
        provenance_id=provenance.provenance_id,
    )
    event = make_scheduled_event(
        branch_id="main",
        due_time=Fraction(5),
        event_kind="claim-due",
        payload=(),
        state=ScheduledEventState.SCHEDULED,
        provenance_id=provenance.provenance_id,
        benefit_burden_pair_id=pair.pair_id,
    )
    claim = make_claim(
        branch_id="main",
        bearer_ref="subject",
        beneficiary_or_sink_ref=None,
        dimension="mana",
        amount_expr=FormulaExpr("CONST", literal=Fraction(3)),
        due_event_ref=event.event_id,
        state=ClaimState.PENDING,
        benefit_burden_pair_id=pair.pair_id,
        origin_rule_id="delayed-cost",
    )
    operations = (
        StateOperation(OperationKind.SET_BENEFIT_BURDEN_PAIR, f"pair:{pair.pair_id}", pair, provenance.provenance_id),
        StateOperation(OperationKind.SET_SCHEDULED_EVENT, f"event:{event.event_id}", event, provenance.provenance_id),
        StateOperation(OperationKind.SET_CLAIM, f"claim:{claim.claim_id}", claim, provenance.provenance_id),
    )
    proposal = make_proposal(
        branch_id="main", base_version="main:0", actor_ref="subject",
        actor_identity_epoch=0, permission_action="defer", preconditions=(),
        point_reads=(), predicate_reads=(), operations=operations, charge_groups=(),
        provenance_nodes=(provenance,), provenance_root=provenance.provenance_id,
    )

    result = Kernel().commit(base, (proposal,))

    assert result.committed
    assert claim.claim_id in result.version.state.claims
    assert event.event_id in result.version.state.scheduled_events
    assert pair.pair_id in result.version.state.benefit_burden_pairs


def test_unbound_delayed_claim_is_rejected_without_world_change() -> None:
    subject_ref = WorldRef(BranchId("main"), "subject", 0)
    subject = Subject(subject_ref, 0, "personality")
    permission = PermissionGrant("grant", "subject", "defer", "", 0, "root")
    base = replace(genesis("main"), state=WorldState(subjects={"subject": subject}, permissions=(permission,), identity_epochs={"subject": 0}))
    provenance = make_provenance(rule_id="bad-delay", event_id="benefit")
    claim = make_claim(branch_id="main",bearer_ref="subject",beneficiary_or_sink_ref=None,dimension="mana",amount_expr=FormulaExpr("CONST",literal=Fraction(1)),due_event_ref="missing-event",state=ClaimState.PENDING,benefit_burden_pair_id="missing-pair",origin_rule_id="bad-delay")
    operation = StateOperation(OperationKind.SET_CLAIM,f"claim:{claim.claim_id}",claim,provenance.provenance_id)
    proposal = make_proposal(branch_id="main",base_version="main:0",actor_ref="subject",actor_identity_epoch=0,permission_action="defer",preconditions=(),point_reads=(),predicate_reads=(),operations=(operation,),charge_groups=(),provenance_nodes=(provenance,),provenance_root=provenance.provenance_id)

    result = Kernel().commit(base, (proposal,))

    assert not result.committed
    assert result.version is base
    assert result.failure is not None and result.failure.code == "deferred-claim-owner-or-branch-invalid"


def _entity_deletion_world(policy: EndpointDeletionPolicy):
    actor_ref=WorldRef(BranchId("main"),"actor",0)
    left_ref=WorldRef(BranchId("main"),"left",0)
    right_ref=WorldRef(BranchId("main"),"right",0)
    relation_ref=WorldRef(BranchId("main"),"bond",0)
    relation=Relation(relation_ref,"bond",(RelationEndpoint("left",left_ref),RelationEndpoint("right",right_ref)),"world","v0",None,"source",policy)
    fact=Fact("left-state",left_ref,"state","present","source")
    state=WorldState(
        entities={"left":Entity(left_ref,"object"),"right":Entity(right_ref,"object")},
        subjects={"actor":Subject(actor_ref,0,"personality")},
        facts={fact.fact_id:fact},relations={"bond":relation},
        permissions=(PermissionGrant("grant","actor","delete","entity:",0,"root"),),
        identity_epochs={"actor":0},
    )
    return replace(genesis("main"),state=state),left_ref


def test_entity_deletion_cascades_whole_relation_and_declared_facts() -> None:
    base,left_ref=_entity_deletion_world(EndpointDeletionPolicy.CASCADE_RELATION)
    provenance=make_provenance(rule_id="delete",event_id="event")
    operation=StateOperation(OperationKind.DELETE_ENTITY,"entity:left",EntityDeletion(left_ref,cascade_facts=True),provenance.provenance_id)
    proposal=make_proposal(branch_id="main",base_version="main:0",actor_ref="actor",actor_identity_epoch=0,permission_action="delete",preconditions=(),point_reads=(),predicate_reads=(),operations=(operation,),charge_groups=(),provenance_nodes=(provenance,),provenance_root=provenance.provenance_id)

    result=Kernel().commit(base,(proposal,))

    assert result.committed
    assert "left" not in result.version.state.entities
    assert "left-state" not in result.version.state.facts
    assert "bond" not in result.version.state.relations


def test_entity_deletion_never_shrinks_rejecting_hyperedge() -> None:
    base,left_ref=_entity_deletion_world(EndpointDeletionPolicy.REJECT)
    provenance=make_provenance(rule_id="delete",event_id="event")
    operation=StateOperation(OperationKind.DELETE_ENTITY,"entity:left",EntityDeletion(left_ref,cascade_facts=True),provenance.provenance_id)
    proposal=make_proposal(branch_id="main",base_version="main:0",actor_ref="actor",actor_identity_epoch=0,permission_action="delete",preconditions=(),point_reads=(),predicate_reads=(),operations=(operation,),charge_groups=(),provenance_nodes=(provenance,),provenance_root=provenance.provenance_id)

    result=Kernel().commit(base,(proposal,))

    assert not result.committed
    assert result.version is base
    assert result.failure is not None and "blocked by relations" in result.failure.code


def test_execution_quote_settlement_rejects_changed_dependencies() -> None:
    component=make_component(plane=CostPlane.EXECUTION,dimension="mana",amount=Fraction(2),bearer_ref="payer",sink_ref="sink",authority_domain="world",origin_rule="rule")
    quote=quote_cost(branch_id="main",base_version="v1",model_version="model",activation_epoch=0,subject_id="payer",epistemic_version=None,personality_version=None,path_id="cast",plane=CostPlane.EXECUTION,components=(component,),modifiers=(),proof_roots=("proof",),current_epoch=0,account_epochs=(("payer",3),("sink",7)),channel_epochs=(("voice",2),),scope_dependency_epoch=4)

    plan=settlement_plan(quote,branch_id="main",base_version="v1",current_epoch=0,active_model_version="model",current_account_epochs=(("sink",7),("payer",3)),current_channel_epochs=(("voice",2),),current_scope_dependency_epoch=4)
    assert len(plan.groups)==1

    failed=False
    try:settlement_plan(quote,branch_id="main",base_version="v1",current_epoch=0,active_model_version="model",current_account_epochs=(("payer",4),("sink",7)),current_channel_epochs=(("voice",2),),current_scope_dependency_epoch=4)
    except ValueError as exc:failed="account dependency invalidated" in str(exc)
    assert failed


def test_replanning_stops_after_a_finite_number_of_invalidations() -> None:
    snapshots=iter(("v1","v2","v2","v3"))
    result=bounded_replan(
        read_dependency_snapshot=lambda:next(snapshots),
        plan_once=lambda dependency:PlanCandidate("attack",f"quote:{dependency}",dependency),
        policy=ReplanPolicy(max_replans=1,safe_fallback_path_id="wait"),
    )

    assert result.status is ReplanStatus.SAFE_FALLBACK
    assert result.candidate is not None and result.candidate.path_id=="wait"
    assert len(result.attempts)==2
    assert result.failure_certificate_id is not None


def test_replanning_accepts_only_a_freshly_revalidated_candidate() -> None:
    snapshots=iter(("v1","v2","v2","v2"))
    result=bounded_replan(
        read_dependency_snapshot=lambda:next(snapshots),
        plan_once=lambda dependency:PlanCandidate("probe",f"quote:{dependency}",dependency),
        policy=ReplanPolicy(max_replans=2),
    )

    assert result.status is ReplanStatus.STABLE
    assert result.candidate is not None and result.candidate.dependency_snapshot_id=="v2"
    assert tuple(attempt.stale for attempt in result.attempts)==(True,False)


def test_conflict_failure_certificate_is_content_bound_and_permutation_invariant() -> None:
    left=_proposal("certificate-left","fact:shared","left")
    right=_proposal("certificate-right","fact:shared","right")
    subject_ref=WorldRef(BranchId("main"),"subject",0)
    base=replace(genesis("main",ruleset_hash="rules"),state=WorldState(subjects={"subject":Subject(subject_ref,0,"personality")},permissions=(PermissionGrant("grant","subject","write","fact:",0,"root"),),identity_epochs={"subject":0}))

    first=Kernel().commit(base,(left,right))
    second=Kernel().commit(base,(right,left))

    assert first.failure is not None and second.failure is not None
    assert first.failure.certificate_id==second.failure.certificate_id
    assert first.failure.ruleset_hash=="rules"
    assert first.failure.state_digest==base.state.root_hash
    assert first.failure.conflict_component_ids


def test_pov_projection_filters_payload_and_bridges_hidden_causal_nodes() -> None:
    def node(kind,payload,parents=(),objective=False):
        values=(kind,payload,parents,objective)
        return TraceNode(content_id(values),kind,payload,parents,objective)
    observed=node("observation",(("message","heard"),("internal_id","entity:secret")))
    hidden=node("objective-rule",(("rule","unknown"),),(observed.trace_id,),True)
    consequence=node("observation",(("message","door moved"),("internal_id","fact:secret")),(hidden.trace_id,))
    dag=TraceDag((observed,hidden,consequence))
    grant=ProjectionGrant("alice","e1",(observed.trace_id,hidden.trace_id,consequence.trace_id),(("observation",("message",)),))

    projection=project_trace(dag,grant)
    other=project_trace(dag,replace(grant,subject_id="bob"))

    assert len(projection.nodes)==2
    assert projection.nodes[1].parent_ids==(projection.nodes[0].trace_id,)
    assert projection.nodes[0].public_payload==(("message","heard"),)
    assert all("secret" not in value for node_ in projection.nodes for _,value in node_.public_payload)
    assert tuple(node_.trace_id for node_ in projection.nodes)!=tuple(node_.trace_id for node_ in other.nodes)


def test_dissipative_cycle_terminates_exactly_and_preserves_residual() -> None:
    edges=(PropagationEdge("a-b","a","b",Fraction(1,2)),PropagationEdge("b-a","b","a",Fraction(1,2)))
    result=propagate(initial=(("a",Fraction(1)),),edges=edges,policy=PropagationPolicy(minimum_quantum=Fraction(1,16),residual_sink="heat"))

    assert result.certificate.termination_witness=="strict-dissipation-with-residual"
    assert result.certificate.input_amount==result.certificate.delivered_amount+result.certificate.residual_amount
    assert result.residual==1
    assert result.certificate.steps<20


def test_non_dissipative_cycle_is_rejected_before_execution() -> None:
    edges=(PropagationEdge("a-b","a","b",Fraction(1)),PropagationEdge("b-a","b","a",Fraction(1)))
    failed=False
    try:propagate(initial=(("a",Fraction(1)),),edges=edges,policy=PropagationPolicy(minimum_quantum=Fraction(1,16),residual_sink="heat"))
    except ValueError as exc:failed="strict per-edge dissipation" in str(exc)
    assert failed


def test_permission_revocation_epoch_prevents_old_grant_replay() -> None:
    admin_ref=WorldRef(BranchId("main"),"admin",0);subject_ref=WorldRef(BranchId("main"),"subject",0)
    admin=PermissionGrant("admin-grant","admin","revoke","permission:",0,"root",0)
    writer=PermissionGrant("writer-grant","subject","write","fact:",0,"root",0)
    base=replace(genesis("main"),state=WorldState(subjects={"admin":Subject(admin_ref,0,"p"),"subject":Subject(subject_ref,0,"p")},permissions=(admin,writer),identity_epochs={"admin":0,"subject":0}))
    provenance=make_provenance(rule_id="revoke",event_id="event")
    revoke=StateOperation(OperationKind.REVOKE_PERMISSION,"permission:writer-grant",PermissionRevocation("writer-grant",0),provenance.provenance_id)
    proposal=make_proposal(branch_id="main",base_version="main:0",actor_ref="admin",actor_identity_epoch=0,permission_action="revoke",preconditions=(),point_reads=(),predicate_reads=(),operations=(revoke,),charge_groups=(),provenance_nodes=(provenance,),provenance_root=provenance.provenance_id)
    revoked=Kernel().commit(base,(proposal,))
    assert revoked.committed and revoked.version.state.permission_epochs["writer-grant"]==1

    write_provenance=make_provenance(rule_id="write",event_id="later")
    fact=Fact("after-revoke",subject_ref,"state","changed",write_provenance.provenance_id)
    stale_use=make_proposal(branch_id="main",base_version=str(revoked.version.version_id),actor_ref="subject",actor_identity_epoch=0,permission_action="write",preconditions=(),point_reads=(),predicate_reads=(),operations=(StateOperation(OperationKind.SET_FACT,"fact:after-revoke",fact,write_provenance.provenance_id),),charge_groups=(),provenance_nodes=(write_provenance,),provenance_root=write_provenance.provenance_id)
    denied=Kernel().commit(revoked.version,(stale_use,))

    assert not denied.committed
    assert denied.failure is not None and denied.failure.code=="permission-denied"


def test_automatic_successors_require_strict_progress_witnesses() -> None:
    transitions=(
        AutonomousTransition("rank","a","b",rank_before=2,rank_after=1),
        AutonomousTransition("time","b","c",time_advanced=Fraction(1)),
    )
    certificate=certify_autonomous_transitions(transitions)
    assert certificate.witnesses==(("rank",ProgressWitnessKind.RANK_DESCENT),("time",ProgressWitnessKind.TIME_ADVANCE))

    failed=False
    try:certify_autonomous_transitions((AutonomousTransition("loop","x","x"),))
    except ValueError as exc:failed="lacks strict progress" in str(exc)
    assert failed


def test_multi_actor_compensation_is_exact_and_capped_by_damage() -> None:
    damage=make_compensable_damage(causal_event_id="collapse",victim_ref="victim",dimension="health",maximum_compensation=Fraction(10))
    shares=(ResponsibilityShare("actor-b",Fraction(1,4),"joint-liability"),ResponsibilityShare("actor-a",Fraction(1,2),"joint-liability"))
    allocations,certificate=allocate_compensation(damage,tuple(reversed(shares)))

    assert tuple(item.payer_ref for item in allocations)==("actor-a","actor-b")
    assert certificate.total_compensation==Fraction(15,2)
    assert certificate.unallocated_damage==Fraction(5,2)

    failed=False
    try:allocate_compensation(damage,(ResponsibilityShare("a",Fraction(3,4),"rule"),ResponsibilityShare("b",Fraction(3,4),"rule")))
    except ValueError as exc:failed="exceed whole damage" in str(exc)
    assert failed


def test_predicate_read_token_rejects_phantom_after_unrelated_commit() -> None:
    subject_ref=WorldRef(BranchId("main"),"subject",0)
    base=replace(genesis("main"),state=WorldState(subjects={"subject":Subject(subject_ref,0,"p")},permissions=(PermissionGrant("grant","subject","write","fact:",0,"root"),),identity_epochs={"subject":0}))
    first=Kernel().commit(base,(_proposal("epoch-advance","fact:first","one"),))
    assert first.committed and first.version.state.predicate_epoch==1
    provenance=make_provenance(rule_id="range-rule",event_id="event")
    fact=Fact("second",subject_ref,"state","two",provenance.provenance_id)
    proposal=make_proposal(branch_id="main",base_version=str(first.version.version_id),actor_ref="subject",actor_identity_epoch=0,permission_action="write",preconditions=(),point_reads=(),predicate_reads=(PredicateReadToken(b"all-subject-facts",0,range_read=True),),operations=(StateOperation(OperationKind.SET_FACT,"fact:second",fact,provenance.provenance_id),),charge_groups=(),provenance_nodes=(provenance,),provenance_root=provenance.provenance_id)

    stale=Kernel().commit(first.version,(proposal,))

    assert stale.status is CommitStatus.STALE
    assert stale.failure is not None and stale.failure.code=="predicate-read-invalidated"
    assert "second" not in stale.version.state.facts


def test_lease_preemption_finds_only_unique_minimal_authorized_release_set() -> None:
    channel=ChannelDefinition("voice",(("slots",Fraction(1)),))
    incumbent=make_channel_lease(channel_id="voice",holder_ref="a",usage=(("slots",Fraction(1)),),compatibility_class="ordinary",release_token="release-a",acquired_epoch=0)
    candidate=make_channel_lease(channel_id="voice",holder_ref="b",usage=(("slots",Fraction(1)),),compatibility_class="emergency",release_token="release-b",acquired_epoch=1)
    plan=plan_lease_preemption(channels={"voice":channel},candidate=candidate,existing=(incumbent,),rules=(LeasePreemptionRule("emergency","ordinary"),))
    assert plan.release_lease_ids==(incumbent.lease_id,)


def test_cyclic_or_ambiguous_lease_preemption_is_rejected() -> None:
    channel=ChannelDefinition("voice",(("slots",Fraction(2)),))
    existing=tuple(make_channel_lease(channel_id="voice",holder_ref=holder,usage=(("slots",Fraction(1)),),compatibility_class="ordinary",release_token=f"release-{holder}",acquired_epoch=0) for holder in ("a","b"))
    candidate=make_channel_lease(channel_id="voice",holder_ref="c",usage=(("slots",Fraction(1)),),compatibility_class="emergency",release_token="release-c",acquired_epoch=1)
    failed=False
    try:plan_lease_preemption(channels={"voice":channel},candidate=candidate,existing=existing,rules=(LeasePreemptionRule("emergency","ordinary"),))
    except ValueError as exc:failed="incomparable" in str(exc)
    assert failed

    cyclic=False
    try:plan_lease_preemption(channels={"voice":channel},candidate=candidate,existing=existing,rules=(LeasePreemptionRule("emergency","ordinary"),LeasePreemptionRule("ordinary","emergency")))
    except ValueError as exc:cyclic="cyclic" in str(exc)
    assert cyclic
