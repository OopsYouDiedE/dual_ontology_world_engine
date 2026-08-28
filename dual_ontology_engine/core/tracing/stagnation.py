from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from fractions import Fraction


class StagnationKind(str,Enum):
    NO_STATE_PROGRESS="no_state_progress"
    NO_NEW_EVIDENCE="no_new_evidence"
    CANDIDATE_STASIS="candidate_stasis"
    REPEATED_REASONS="repeated_reasons"
    TASK_INACTIVE="task_inactive"


@dataclass(frozen=True,slots=True)
class ProgressSnapshot:
    version_id:str
    world_time:Fraction
    new_fact_ids:tuple[str,...]=()
    new_evidence_ids:tuple[str,...]=()
    candidate_path_ids:tuple[str,...]=()
    reason_ids:tuple[str,...]=()
    task_progress_markers:tuple[str,...]=()

    def __post_init__(self)->None:
        if not isinstance(self.world_time,Fraction) or self.world_time<0:raise ValueError("progress snapshot time invalid")


@dataclass(frozen=True,slots=True)
class StagnationReport:
    window_start_version:str
    window_end_version:str
    kinds:tuple[StagnationKind,...]
    repeated_candidate_path_ids:tuple[str,...]
    repeated_reason_ids:tuple[str,...]

    @property
    def stagnant(self)->bool:return bool(self.kinds)


def analyze_stagnation(history:tuple[ProgressSnapshot,...],*,window:int=4)->StagnationReport:
    """纯只读诊断；报告不能作为自动造弱点或造事件的写权限。"""
    if window<2 or len(history)<window:raise ValueError("stagnation analysis requires a complete window")
    values=history[-window:]
    if any(values[index].world_time>values[index+1].world_time for index in range(len(values)-1)):raise ValueError("progress history time moved backward")
    kinds=[]
    if all(not item.new_fact_ids for item in values):kinds.append(StagnationKind.NO_STATE_PROGRESS)
    if all(not item.new_evidence_ids for item in values):kinds.append(StagnationKind.NO_NEW_EVIDENCE)
    candidate_sets={tuple(sorted(item.candidate_path_ids)) for item in values}
    if len(candidate_sets)==1 and next(iter(candidate_sets),()):kinds.append(StagnationKind.CANDIDATE_STASIS)
    reason_sets={tuple(sorted(item.reason_ids)) for item in values}
    if len(reason_sets)==1 and next(iter(reason_sets),()):kinds.append(StagnationKind.REPEATED_REASONS)
    if all(not item.task_progress_markers for item in values):kinds.append(StagnationKind.TASK_INACTIVE)
    return StagnationReport(values[0].version_id,values[-1].version_id,tuple(kinds),next(iter(candidate_sets)) if len(candidate_sets)==1 else (),next(iter(reason_sets)) if len(reason_sets)==1 else ())
