from __future__ import annotations

from dataclasses import dataclass
from fractions import Fraction

from .models import ArtifactStatus, CalibrationArtifact, CostModelVersion
from .scenarios import BenchmarkScenario, PropertyVerdict, RolloutOutcome


@dataclass(frozen=True, slots=True)
class ReleaseGateResult:
    approved: bool
    reasons: tuple[str, ...]
    behavior_change_rate: Fraction | None = None


def rollout_behavior_change_rate(
    incumbent: tuple[RolloutOutcome, ...], candidate: tuple[RolloutOutcome, ...]
) -> Fraction:
    """在共同场景上比较行为可观察签名，不以参数距离冒充行为稳定。"""

    left = {outcome.scenario_id: outcome for outcome in incumbent}
    right = {outcome.scenario_id: outcome for outcome in candidate}
    if len(left) != len(incumbent) or len(right) != len(candidate):
        raise ValueError("duplicate rollout scenario")
    common = sorted(set(left) & set(right))
    if not common:
        raise ValueError("behavior comparison has no common scenario")

    def signature(outcome: RolloutOutcome) -> tuple[object, ...]:
        verdicts = tuple(
            sorted(
                (certificate.property_id.value, certificate.verdict.value)
                for certificate in outcome.certificates
            )
        )
        return (
            outcome.effect_vector,
            outcome.cost_atom_ids,
            verdicts,
            outcome.hard_failures,
            outcome.budget_exhausted,
        )

    changed = sum(signature(left[key]) != signature(right[key]) for key in common)
    return Fraction(changed, len(common))


def evaluate_release(
    artifact: CalibrationArtifact,
    scenarios: tuple[BenchmarkScenario, ...],
    outcomes: tuple[RolloutOutcome, ...],
    *,
    required_families: frozenset[str],
    max_switches: int,
    max_replans: int,
    incumbent_outcomes: tuple[RolloutOutcome, ...] | None = None,
    max_behavior_change: Fraction | None = None,
) -> ReleaseGateResult:
    reasons = []
    behavior_change: Fraction | None = None
    if max_switches < 0 or max_replans < 0:
        reasons.append("negative_gate_limit")
    if not scenarios or not outcomes:
        reasons.append("empty_suite_or_outcomes")
    scenario_by_id = {scenario.scenario_id: scenario for scenario in scenarios}
    outcome_by_scenario = {outcome.scenario_id: outcome for outcome in outcomes}
    if len(scenario_by_id) != len(scenarios) or len(outcome_by_scenario) != len(outcomes):
        reasons.append("duplicate_scenario_or_outcome")
    if set(outcome_by_scenario) - set(scenario_by_id):
        reasons.append("outcome_without_scenario")
    missing = required_families - {scenario.family_id for scenario in scenarios}
    if missing:
        reasons.append("missing_families")

    for scenario in scenarios:
        outcome = outcome_by_scenario.get(scenario.scenario_id)
        if outcome is None:
            reasons.append("missing_outcome")
            continue
        if (
            outcome.seed != scenario.seed
            or outcome.family_id != scenario.family_id
            or outcome.model_version != scenario.model_version
        ):
            reasons.append("outcome_binding_mismatch")
        if len({certificate.property_id for certificate in outcome.certificates}) != len(
            outcome.certificates
        ):
            reasons.append("duplicate_property_certificate")
        verdicts = {
            certificate.property_id: certificate.verdict
            for certificate in outcome.certificates
        }
        if any(
            verdicts.get(property_id) is not PropertyVerdict.PROVED
            for property_id in scenario.expected_properties
        ):
            reasons.append("assertion_not_proved")
        if outcome.hard_failures or outcome.budget_exhausted:
            reasons.append("rollout_failure")
        if outcome.decision_switches > max_switches:
            reasons.append("decision_oscillation")
        if outcome.replans > max_replans:
            reasons.append("replan_livelock")

    if not artifact.coverage_report_hash:
        reasons.append("missing_coverage_report")
    if artifact.profile.training_suite_id == artifact.profile.validation_suite_id:
        reasons.append("training_validation_leakage")
    if any(
        estimate.confidence_delta is None
        or not 0 < estimate.confidence_delta < 1
        for estimate in artifact.estimates
    ):
        reasons.append("missing_confidence_discipline")
    if artifact.adversarial_witness_ids:
        reasons.append("open_adversarial_witness")
    if artifact.status is not ArtifactStatus.APPROVED:
        reasons.append("artifact_not_approved")

    if incumbent_outcomes is not None or max_behavior_change is not None:
        if incumbent_outcomes is None or max_behavior_change is None:
            reasons.append("incomplete_behavior_gate")
        elif not isinstance(max_behavior_change, Fraction) or not 0 <= max_behavior_change <= 1:
            reasons.append("invalid_behavior_change_limit")
        else:
            try:
                behavior_change = rollout_behavior_change_rate(incumbent_outcomes, outcomes)
            except ValueError:
                reasons.append("behavior_comparison_failed")
            else:
                if behavior_change > max_behavior_change:
                    reasons.append("behavior_change_exceeded")

    return ReleaseGateResult(
        not reasons, tuple(sorted(set(reasons))), behavior_change
    )


def publish_model(
    candidate: CostModelVersion,
    artifact: CalibrationArtifact,
    gate: ReleaseGateResult,
    *,
    current_epoch: int,
) -> CostModelVersion:
    if not gate.approved:
        raise ValueError("release gate rejected model")
    if candidate.calibration_profile_id != artifact.profile.profile_id:
        raise ValueError("artifact/model mismatch")
    if candidate.parent_id != artifact.parent_model_id:
        raise ValueError("candidate parent does not match calibration evidence")
    if candidate.activation_epoch <= current_epoch:
        raise ValueError("model cannot activate in current/past epoch")
    return candidate


def model_is_active(model: CostModelVersion, epoch: int) -> bool:
    return epoch >= model.activation_epoch
