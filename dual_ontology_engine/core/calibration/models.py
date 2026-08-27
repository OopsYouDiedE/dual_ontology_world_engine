from __future__ import annotations
from dataclasses import dataclass
from fractions import Fraction
from enum import Enum
from ..foundation.codec import content_id
from .statistics import CostEstimate

class ArtifactStatus(str,Enum):CANDIDATE="candidate";REJECTED="rejected";APPROVED="approved"
@dataclass(frozen=True,slots=True)
class ParameterValue:
    name:str;value:Fraction;lower:Fraction;upper:Fraction
    def __post_init__(self):
        if any(not isinstance(v,Fraction) for v in (self.value,self.lower,self.upper)) or not self.lower<=self.value<=self.upper:raise ValueError("parameter outside exact certified domain")
@dataclass(frozen=True,slots=True)
class CalibrationProfile:
    profile_id:str;parent_model_id:str|None;parameters:tuple[ParameterValue,...];validity_domain_id:str;training_suite_id:str;validation_suite_id:str;invariant_certificate_hash:str
    @property
    def content_id(self)->str:return content_id((self.parent_model_id,self.parameters,self.validity_domain_id,self.training_suite_id,self.validation_suite_id,self.invariant_certificate_hash))
    def __post_init__(self):
        if self.profile_id!=self.content_id or not self.parameters:raise ValueError("invalid calibration profile")
@dataclass(frozen=True,slots=True)
class CalibrationArtifact:
    artifact_id:str;parent_model_id:str;algorithm_hash:str;profile:CalibrationProfile;estimates:tuple[CostEstimate,...];coverage_report_hash:str;adversarial_witness_ids:tuple[str,...];status:ArtifactStatus
    @property
    def content_id(self)->str:return content_id((self.parent_model_id,self.algorithm_hash,self.profile,self.estimates,self.coverage_report_hash,self.adversarial_witness_ids,self.status))
    def __post_init__(self):
        if self.artifact_id!=self.content_id or not self.estimates:raise ValueError("artifact missing content-bound evidence")
@dataclass(frozen=True,slots=True)
class CostModelVersion:
    version_id:str;parent_id:str|None;symbolic_root_hash:str;calibration_profile_id:str;feature_schema_hash:str;activation_epoch:int
    @property
    def content_id(self)->str:return content_id((self.parent_id,self.symbolic_root_hash,self.calibration_profile_id,self.feature_schema_hash,self.activation_epoch))
    def __post_init__(self):
        if self.version_id!=self.content_id or self.activation_epoch<0:raise ValueError("invalid cost model version")

