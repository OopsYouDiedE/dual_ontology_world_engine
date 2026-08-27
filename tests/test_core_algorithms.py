from __future__ import annotations

import itertools
from dataclasses import replace

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
    StateOperation,
    make_proposal,
)
from dual_ontology_engine.core.transactions.commit import Kernel
from dual_ontology_engine.core.transactions.intents import make_intent
from dual_ontology_engine.core.transactions.runtime import execute_intent
from dual_ontology_engine.core.world.entities import Fact, PermissionGrant, Subject
from dual_ontology_engine.core.world.versions import WorldState, genesis
from dual_ontology_engine.core.world.scopes import ScopeExpr, ScopeOp
from dual_ontology_engine.core.costs.atoms import make_component
from dual_ontology_engine.core.costs.planes import CostPlane
from dual_ontology_engine.core.costs.quotes import quote_cost
from dual_ontology_engine.core.decisions.deliberator import DecisionContext, decide
from dual_ontology_engine.core.decisions.personality import PersonalityProfile, ValueTier
from dual_ontology_engine.core.decisions.reasons import (
    Reason,
    ReasonPolarity,
    grounded_acceptance,
)
from dual_ontology_engine.core.decisions.tree import ActionPath
from dual_ontology_engine.core.world.epistemic import EpistemicView


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
    )

    first = execute_intent(base, intent, (post, apply, derive))
    second = execute_intent(base, intent, (derive, post, apply))

    assert first.committed and second.committed
    assert first.commit == second.commit
    assert tuple(first.commit.version.state.facts.values())[0].predicate == "focused"
    assert first.follow_up_rule_ids == ("announce-after",)
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
