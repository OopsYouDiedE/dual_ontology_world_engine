from __future__ import annotations
from dataclasses import dataclass
from enum import Enum
from fractions import Fraction
from ..foundation.codec import content_id

class PropertyId(str,Enum): CONSERVATION="conservation";NO_REPLAY="no_replay";BOUNDED_SWITCHING="bounded_switching";COGNITIVE_NONINTERFERENCE="cognitive_noninterference";IDENTITY_SINGLE_SUCCESSOR="identity_single_successor";BENEFIT_BURDEN_BOUND="benefit_burden_bound";CAUSAL_TRACE="causal_trace"
class PropertyVerdict(str,Enum): PROVED="proved";REFUTED="refuted";UNKNOWN="unknown";ERROR="error"
@dataclass(frozen=True,slots=True)
class PropertyCertificate:property_id:PropertyId;verdict:PropertyVerdict;witness_refs:tuple[str,...]

@dataclass(frozen=True,slots=True)
class BenchmarkScenario:
    scenario_id:str;family_id:str;initial_state_hash:str;ruleset_hash:str;model_version:str;subject_profile_id:str;goal_id:str;policy_profile_ids:tuple[str,...];interference_profile_id:str;observability_profile_id:str;horizon:int;seed:int;expected_properties:tuple[PropertyId,...]
    @property
    def content_id(self)->str:return content_id((self.family_id,self.initial_state_hash,self.ruleset_hash,self.model_version,self.subject_profile_id,self.goal_id,self.policy_profile_ids,self.interference_profile_id,self.observability_profile_id,self.horizon,self.seed,self.expected_properties))
    def __post_init__(self):
        if self.scenario_id!=self.content_id:raise ValueError("scenario id not content-bound")
        if self.horizon<1 or self.seed<0 or not self.expected_properties:raise ValueError("scenario requires positive horizon/seed and assertions")

@dataclass(frozen=True,slots=True)
class ScenarioSuite:
    suite_id:str;model_version:str;scenario_ids:tuple[str,...]
    @property
    def content_id(self)->str:return content_id((self.model_version,self.scenario_ids))
    def __post_init__(self):
        if self.suite_id!=self.content_id or not self.scenario_ids or len(set(self.scenario_ids))!=len(self.scenario_ids):raise ValueError("invalid scenario suite")

@dataclass(frozen=True,slots=True)
class RolloutOutcome:
    outcome_id:str;scenario_id:str;family_id:str;model_version:str;seed:int;effect_vector:tuple[Fraction,...];cost_atom_ids:tuple[str,...];certificates:tuple[PropertyCertificate,...];hard_failures:tuple[str,...];decision_switches:int;replans:int;versions_created:int;budget_exhausted:bool
    @property
    def content_id(self)->str:return content_id((self.scenario_id,self.family_id,self.model_version,self.seed,self.effect_vector,self.cost_atom_ids,self.certificates,self.hard_failures,self.decision_switches,self.replans,self.versions_created,self.budget_exhausted))
    def __post_init__(self):
        if self.outcome_id!=self.content_id:raise ValueError("outcome id not content-bound")
        if not self.certificates or min(self.seed,self.decision_switches,self.replans,self.versions_created)<0:raise ValueError("invalid/empty outcome")
        if any(not isinstance(v,Fraction) or v<0 for v in self.effect_vector):raise ValueError("negative/non-exact rollout statistic")

