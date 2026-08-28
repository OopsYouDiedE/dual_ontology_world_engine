from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from fractions import Fraction

from ..foundation.codec import content_id


class EvidencePolarity(str, Enum):
    SUPPORTS = "supports"
    OPPOSES = "opposes"


@dataclass(frozen=True, slots=True)
class EvidenceNode:
    evidence_id: str
    proposition: str
    polarity: EvidencePolarity
    independent_source_id: str | None
    parent_ids: tuple[str, ...] = ()
    intervention_context: str | None = None

    @property
    def content_id(self) -> str:
        return content_id(
            (
                self.proposition,
                self.polarity,
                self.independent_source_id,
                self.parent_ids,
                self.intervention_context,
            )
        )

    def __post_init__(self) -> None:
        if self.evidence_id != self.content_id:
            raise ValueError("evidence id not content-bound")
        if not self.proposition or len(set(self.parent_ids)) != len(self.parent_ids):
            raise ValueError("invalid evidence proposition/parents")
        if (self.independent_source_id is None) == (not self.parent_ids):
            raise ValueError("root evidence needs a source; derived evidence needs parents")


def make_evidence(
    *,
    proposition: str,
    polarity: EvidencePolarity,
    independent_source_id: str | None,
    parent_ids: tuple[str, ...] = (),
    intervention_context: str | None = None,
) -> EvidenceNode:
    values = (
        proposition,
        polarity,
        independent_source_id,
        parent_ids,
        intervention_context,
    )
    return EvidenceNode(content_id(values), *values)


@dataclass(frozen=True, slots=True)
class EvidenceAssessment:
    proposition: str
    lower: Fraction
    upper: Fraction
    supporting_sources: tuple[str, ...]
    opposing_sources: tuple[str, ...]
    conflicted_sources: tuple[str, ...]
    relevant_evidence_ids: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class EvidenceLedger:
    nodes: tuple[EvidenceNode, ...]

    def __post_init__(self) -> None:
        by_id = {node.evidence_id: node for node in self.nodes}
        if len(by_id) != len(self.nodes):
            raise ValueError("duplicate evidence id")
        if any(parent not in by_id for node in self.nodes for parent in node.parent_ids):
            raise ValueError("unknown evidence parent")
        visiting: set[str] = set()
        done: set[str] = set()

        def visit(evidence_id: str) -> None:
            if evidence_id in visiting:
                raise ValueError("evidence ancestor cycle")
            if evidence_id in done:
                return
            visiting.add(evidence_id)
            for parent in by_id[evidence_id].parent_ids:
                visit(parent)
            visiting.remove(evidence_id)
            done.add(evidence_id)

        for evidence_id in sorted(by_id):
            visit(evidence_id)

    def assess(
        self, proposition: str, *, intervention_context: str | None = None
    ) -> EvidenceAssessment:
        by_id = {node.evidence_id: node for node in self.nodes}
        roots_cache: dict[str, frozenset[str]] = {}

        def roots(evidence_id: str) -> frozenset[str]:
            cached = roots_cache.get(evidence_id)
            if cached is not None:
                return cached
            node = by_id[evidence_id]
            if node.independent_source_id is not None:
                result = frozenset({node.independent_source_id})
            else:
                result = frozenset(
                    source for parent in node.parent_ids for source in roots(parent)
                )
            roots_cache[evidence_id] = result
            return result

        relevant = tuple(
            sorted(
                (
                    node
                    for node in self.nodes
                    if node.proposition == proposition
                    and node.intervention_context == intervention_context
                ),
                key=lambda node: node.evidence_id,
            )
        )
        supporting = {
            source
            for node in relevant
            if node.polarity is EvidencePolarity.SUPPORTS
            for source in roots(node.evidence_id)
        }
        opposing = {
            source
            for node in relevant
            if node.polarity is EvidencePolarity.OPPOSES
            for source in roots(node.evidence_id)
        }
        conflicted = supporting & opposing
        positive_only = supporting - conflicted
        negative_only = opposing - conflicted
        total = len(positive_only) + len(negative_only) + len(conflicted)
        if total == 0:
            lower, upper = Fraction(), Fraction(1)
        else:
            lower = Fraction(len(positive_only), total)
            upper = Fraction(len(positive_only) + len(conflicted), total)
        return EvidenceAssessment(
            proposition,
            lower,
            upper,
            tuple(sorted(positive_only)),
            tuple(sorted(negative_only)),
            tuple(sorted(conflicted)),
            tuple(node.evidence_id for node in relevant),
        )


class BeliefDisposition(str, Enum):
    UNKNOWN = "unknown"
    ACCEPTED = "accepted"
    REJECTED = "rejected"
    CONFLICTED = "conflicted"


@dataclass(frozen=True, slots=True)
class BeliefPolicy:
    accept_threshold: Fraction
    reject_threshold: Fraction

    def __post_init__(self) -> None:
        if not 0 <= self.reject_threshold < self.accept_threshold <= 1:
            raise ValueError("belief thresholds must define a nonempty hysteresis band")


def update_belief(
    previous: BeliefDisposition,
    assessment: EvidenceAssessment,
    policy: BeliefPolicy,
) -> BeliefDisposition:
    """双阈值滞回；冲突来源不会被压成虚假的确定结论。"""

    if assessment.conflicted_sources and assessment.lower < policy.accept_threshold and assessment.upper > policy.reject_threshold:
        return BeliefDisposition.CONFLICTED
    if assessment.lower >= policy.accept_threshold:
        return BeliefDisposition.ACCEPTED
    if assessment.upper <= policy.reject_threshold:
        return BeliefDisposition.REJECTED
    if previous is BeliefDisposition.ACCEPTED and assessment.upper > policy.reject_threshold:
        return previous
    if previous is BeliefDisposition.REJECTED and assessment.lower < policy.accept_threshold:
        return previous
    return BeliefDisposition.UNKNOWN
