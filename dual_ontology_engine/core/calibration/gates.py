from __future__ import annotations
from dataclasses import dataclass
from .models import CalibrationArtifact,CostModelVersion,ArtifactStatus
from .scenarios import BenchmarkScenario,RolloutOutcome,PropertyVerdict

@dataclass(frozen=True,slots=True)
class ReleaseGateResult:approved:bool;reasons:tuple[str,...]

def evaluate_release(artifact:CalibrationArtifact,scenarios:tuple[BenchmarkScenario,...],outcomes:tuple[RolloutOutcome,...],*,required_families:frozenset[str],max_switches:int,max_replans:int)->ReleaseGateResult:
    reasons=[]
    if max_switches<0 or max_replans<0:reasons.append("negative_gate_limit")
    if not scenarios or not outcomes:reasons.append("empty_suite_or_outcomes")
    scenario_by_id={s.scenario_id:s for s in scenarios};outcome_by_scenario={o.scenario_id:o for o in outcomes}
    if len(scenario_by_id)!=len(scenarios) or len(outcome_by_scenario)!=len(outcomes):reasons.append("duplicate_scenario_or_outcome")
    missing=required_families-{s.family_id for s in scenarios}
    if missing:reasons.append("missing_families")
    for scenario in scenarios:
        outcome=outcome_by_scenario.get(scenario.scenario_id)
        if outcome is None:reasons.append("missing_outcome");continue
        if outcome.seed!=scenario.seed or outcome.family_id!=scenario.family_id or outcome.model_version!=scenario.model_version:reasons.append("outcome_binding_mismatch")
        verdicts={c.property_id:c.verdict for c in outcome.certificates}
        if any(verdicts.get(prop) is not PropertyVerdict.PROVED for prop in scenario.expected_properties):reasons.append("assertion_not_proved")
        if outcome.hard_failures or outcome.budget_exhausted:reasons.append("rollout_failure")
        if outcome.decision_switches>max_switches:reasons.append("decision_oscillation")
        if outcome.replans>max_replans:reasons.append("replan_livelock")
    if artifact.adversarial_witness_ids:reasons.append("open_adversarial_witness")
    if artifact.status is not ArtifactStatus.APPROVED:reasons.append("artifact_not_approved")
    return ReleaseGateResult(not reasons,tuple(sorted(set(reasons))))

def publish_model(candidate:CostModelVersion,artifact:CalibrationArtifact,gate:ReleaseGateResult,*,current_epoch:int)->CostModelVersion:
    if not gate.approved:raise ValueError("release gate rejected model")
    if candidate.calibration_profile_id!=artifact.profile.profile_id:raise ValueError("artifact/model mismatch")
    if candidate.activation_epoch<=current_epoch:raise ValueError("model cannot activate in current/past epoch")
    return candidate

def model_is_active(model:CostModelVersion,epoch:int)->bool:return epoch>=model.activation_epoch
