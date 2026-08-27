from __future__ import annotations

from dataclasses import dataclass
from fractions import Fraction

from ..costs.quotes import CostQuote
from ..foundation.codec import content_id
from ..world.epistemic import EpistemicView
from .hysteresis import HysteresisPolicy, may_switch_vectors
from .personality import PersonalityProfile
from .reasons import Reason, ReasonPolarity, grounded_acceptance
from .tree import ActionPath, DecisionNode, finite_paths


@dataclass(frozen=True, slots=True)
class DecisionContext:
    subject_id: str
    goal_id: str
    epistemic_version: str
    personality_version: str
    decision_epoch: int
    max_depth: int = 4
    max_branching: int = 8
    max_nodes: int = 256
    max_frontier: int = 64
    max_replans: int = 3

    def __post_init__(self) -> None:
        if min(
            self.decision_epoch,
            self.max_depth,
            self.max_branching,
            self.max_nodes,
            self.max_frontier,
            self.max_replans,
        ) < 0:
            raise ValueError("negative decision bound")


@dataclass(frozen=True, slots=True)
class DecisionKey:
    subject_id: str
    goal_id: str
    input_version: str
    personality_version: str
    decision_epoch: int


@dataclass(frozen=True, slots=True)
class IntentDraft:
    actor_id: str
    action_id: str
    path_id: str
    based_on_epistemic_version: str


@dataclass(frozen=True, slots=True)
class DecisionProof:
    decision_id: str
    context: DecisionContext
    chosen_path_id: str
    principal_reason_ids: tuple[str, ...]
    principal_objection_ids: tuple[str, ...]
    cost_quote_ids: tuple[str, ...]
    incumbent_path_id: str | None
    fallback_kind: str | None
    replan_attempt: int
    alternative_path_ids: tuple[str, ...] = ()
    undecided_reason_ids: tuple[str, ...] = ()


class DeliberationIncomplete(ValueError):
    def __init__(self, candidate_path_ids: tuple[str, ...]):
        super().__init__("deliberation incomplete: no declared deterministic fallback")
        self.candidate_path_ids = candidate_path_ids


def _vector(quote: CostQuote) -> dict[str, Fraction]:
    result: dict[str, Fraction] = {}
    for component in quote.final_components:
        result[component.dimension] = (
            result.get(component.dimension, Fraction()) + component.amount
        )
    return result


def _dominates(
    left: dict[str, Fraction], right: dict[str, Fraction], dimensions: tuple[str, ...]
) -> bool:
    values = [(left.get(key, Fraction()), right.get(key, Fraction())) for key in dimensions]
    return bool(values) and all(a <= b for a, b in values) and any(a < b for a, b in values)


def _unique_extreme(
    paths: tuple[ActionPath, ...], attribute: str, *, prefer_high: bool
) -> ActionPath | None:
    values = [getattr(path, attribute) for path in paths]
    extreme = (max if prefer_high else min)(values)
    candidates = [path for path in paths if getattr(path, attribute) == extreme]
    if len(candidates) == 1 and (extreme != 0 or attribute == "risk_rank"):
        return candidates[0]
    return None


def decide(
    view: EpistemicView,
    context: DecisionContext,
    personality: PersonalityProfile,
    paths: tuple[ActionPath, ...],
    quotes: tuple[CostQuote, ...],
    reasons: tuple[Reason, ...],
    *,
    incumbent_path_id: str | None = None,
    replan_attempt: int = 0,
    hysteresis: HysteresisPolicy | None = None,
    residence_epochs: int = 0,
    dependency_changed: bool = True,
    tree_root: DecisionNode | None = None,
) -> tuple[IntentDraft, DecisionProof]:
    if (
        view.subject_id != context.subject_id
        or view.version_id != context.epistemic_version
        or personality.profile_id != context.personality_version
    ):
        raise ValueError("decision binding mismatch")
    if replan_attempt > context.max_replans:
        raise ValueError("replan budget exceeded")
    if tree_root is not None:
        tree_paths = finite_paths(
            tree_root,
            max_depth=context.max_depth,
            max_branching=context.max_branching,
            max_nodes=context.max_nodes,
        )
        if paths and {path.path_id: path for path in paths} != {
            path.path_id: path for path in tree_paths
        }:
            raise ValueError("explicit paths do not match frozen decision tree")
        paths = tree_paths
    if len(paths) > context.max_nodes or len(quotes) > context.max_nodes:
        raise ValueError("decision node budget exceeded")
    if len({path.path_id for path in paths}) != len(paths):
        raise ValueError("duplicate action path")

    by_quote = {quote.path_id: quote for quote in quotes}
    if len(by_quote) != len(quotes):
        raise ValueError("duplicate path quote")
    candidates = []
    for path in paths:
        quote = by_quote.get(path.path_id)
        if (
            path.action_id not in view.affordance_ids
            or quote is None
            or set(path.tags) & set(personality.taboos)
        ):
            continue
        if (
            quote.subject_id != context.subject_id
            or quote.epistemic_version != context.epistemic_version
            or quote.personality_version != context.personality_version
            or quote.path_id != path.path_id
        ):
            raise ValueError("quote/decision binding mismatch")
        candidates.append(path)
    if not candidates:
        raise DeliberationIncomplete(())

    vectors = {path.path_id: _vector(by_quote[path.path_id]) for path in candidates}
    frontier = tuple(candidates)
    ordered_tiers = tuple(sorted(personality.value_tiers, key=lambda tier: tier.tier))
    for tier in ordered_tiers:
        if not tier.dimensions:
            continue
        frontier = tuple(
            path
            for path in frontier
            if not any(
                _dominates(vectors[other.path_id], vectors[path.path_id], tier.dimensions)
                for other in frontier
                if other is not path
            )
        )
    if len(frontier) > context.max_frontier:
        raise ValueError("decision frontier budget exceeded")

    acceptance = grounded_acceptance(reasons)
    active = acceptance.accepted

    def reason_score(path: ActionPath) -> tuple[int, int, int, int]:
        support = max(
            (
                reason.rank
                for reason in active
                if reason.path_id == path.path_id
                and reason.polarity is ReasonPolarity.SUPPORT
            ),
            default=-1,
        )
        objection = max(
            (
                reason.rank
                for reason in active
                if reason.path_id == path.path_id
                and reason.polarity is ReasonPolarity.OPPOSE
            ),
            default=-1,
        )
        commitment = 1 if path.action_id in personality.commitments else 0
        habit = 1 if path.action_id in personality.habits else 0
        return (-support, -commitment, -habit, objection)

    best_score = min(reason_score(path) for path in frontier)
    tied = tuple(path for path in frontier if reason_score(path) == best_score)
    fallback_kind: str | None = None
    best: ActionPath | None = tied[0] if len(tied) == 1 else None
    if best is None:
        incumbent = next((path for path in tied if path.path_id == incumbent_path_id), None)
        if incumbent is not None:
            best = incumbent
            fallback_kind = "incumbent"
    if best is None:
        best = _unique_extreme(tied, "reversibility_rank", prefer_high=True)
        fallback_kind = "reversibility" if best is not None else None
    if best is None:
        best = _unique_extreme(tied, "information_gain", prefer_high=True)
        fallback_kind = "information_probe" if best is not None else None
    if best is None:
        best = _unique_extreme(tied, "risk_rank", prefer_high=False)
        fallback_kind = "low_risk" if best is not None else None
    if best is None:
        wait_paths = [path for path in tied if "fallback:wait" in path.tags]
        if len(wait_paths) == 1:
            best = wait_paths[0]
            fallback_kind = "deliberation_incomplete"
    if best is None:
        raise DeliberationIncomplete(tuple(sorted(path.path_id for path in tied)))

    incumbent = next((path for path in frontier if path.path_id == incumbent_path_id), None)
    dimensions = tuple(
        dimension for tier in ordered_tiers for dimension in tier.dimensions
    )
    if incumbent is not None and incumbent.path_id != best.path_id and hysteresis is not None:
        if not may_switch_vectors(
            incumbent=vectors[incumbent.path_id],
            candidate=vectors[best.path_id],
            dimensions=dimensions,
            residence_epochs=residence_epochs,
            replan_attempt=replan_attempt,
            policy=hysteresis,
            dependency_changed=dependency_changed,
        ):
            best = incumbent
            fallback_kind = "hysteresis"

    supporting = tuple(
        sorted(
            reason.reason_id
            for reason in active
            if reason.path_id == best.path_id
            and reason.polarity is ReasonPolarity.SUPPORT
        )
    )
    opposing = tuple(
        sorted(
            reason.reason_id
            for reason in active
            if reason.path_id == best.path_id
            and reason.polarity is ReasonPolarity.OPPOSE
        )
    )
    quote_ids = tuple(sorted(by_quote[path.path_id].quote_id for path in candidates))
    alternatives = tuple(sorted(path.path_id for path in frontier if path.path_id != best.path_id))
    undecided = tuple(reason.reason_id for reason in acceptance.undecided)
    decision_values = (
        context,
        best.action_id,
        best.path_id,
        supporting,
        opposing,
        quote_ids,
        incumbent.path_id if incumbent else None,
        fallback_kind,
        replan_attempt,
        alternatives,
        undecided,
    )
    decision_id = content_id(decision_values)
    return (
        IntentDraft(
            context.subject_id,
            best.action_id,
            best.path_id,
            context.epistemic_version,
        ),
        DecisionProof(
            decision_id,
            context,
            best.path_id,
            supporting,
            opposing,
            quote_ids,
            incumbent.path_id if incumbent else None,
            fallback_kind,
            replan_attempt,
            alternatives,
            undecided,
        ),
    )
