from __future__ import annotations

from dataclasses import dataclass
from fractions import Fraction


@dataclass(frozen=True, slots=True)
class RiskEnvelope:
    cost_kind: str
    bearer_refs: tuple[str, ...]
    magnitude_interval: tuple[Fraction, Fraction] | None
    probability_interval: tuple[Fraction, Fraction] | None
    onset_window: tuple[int, int | None]
    reversibility_class: str
    evidence_quality: str
    ambiguity: Fraction
    model_version: str

    def __post_init__(self) -> None:
        for interval in (self.magnitude_interval, self.probability_interval):
            if interval is not None and interval[0] > interval[1]: raise ValueError("invalid risk interval")
        if self.probability_interval is not None and not (0 <= self.probability_interval[0] <= self.probability_interval[1] <= 1): raise ValueError("probability outside [0,1]")


@dataclass(frozen=True, slots=True)
class SubjectiveScenarioOutcome:
    scenario_id: str
    cost_kind: str
    magnitude: Fraction
    epistemic_evidence_ids: tuple[str, ...]


def scenario_risk_envelope(outcomes: tuple[SubjectiveScenarioOutcome, ...], *, bearer_ref: str, model_version: str, max_scenarios: int = 64) -> tuple[RiskEnvelope, ...]:
    """运行时基准推演只生成 BELIEF 风险包络，绝不生成正式账单。"""

    if not outcomes or len(outcomes) > max_scenarios: raise ValueError("scenario estimate requires a finite non-empty sample")
    grouped: dict[str, list[SubjectiveScenarioOutcome]] = {}
    for item in outcomes:
        if item.magnitude < 0: raise ValueError("scenario cost cannot be negative")
        grouped.setdefault(item.cost_kind, []).append(item)
    result=[]; total=len(outcomes)
    for kind in sorted(grouped):
        group=grouped[kind]; values=[item.magnitude for item in group]
        evidence={ref for item in group for ref in item.epistemic_evidence_ids}
        quality="grounded" if evidence else "unknown"
        result.append(RiskEnvelope(kind,(bearer_ref,),(min(values),max(values)),(Fraction(len(group),total),Fraction(len(group),total)),(0,None),"unknown",quality,Fraction(1,len(group)+1),model_version))
    return tuple(result)

