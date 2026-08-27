from __future__ import annotations

from dataclasses import dataclass
from typing import NewType

BranchId = NewType("BranchId", str)
VersionId = NewType("VersionId", str)
EntityId = NewType("EntityId", str)
SubjectId = NewType("SubjectId", str)
RuleId = NewType("RuleId", str)
FactId = NewType("FactId", str)


@dataclass(frozen=True, slots=True, order=True)
class WorldRef:
    """跨版本引用；epoch 防止身份被删除后复用。"""

    branch: BranchId
    local_id: str
    epoch: int

    def __post_init__(self) -> None:
        if not self.local_id or self.epoch < 0:
            raise ValueError("WorldRef requires a non-empty id and non-negative epoch")

