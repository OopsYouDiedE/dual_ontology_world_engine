from __future__ import annotations

from dataclasses import dataclass
from enum import Enum

from ..foundation.ids import WorldRef


class RulePhase(str, Enum):
    PRE_COMMIT = "pre_commit"
    POST_COMMIT = "post_commit"


class EffectKind(str, Enum):
    SET_FACT = "set_fact"
    DELETE_FACT = "delete_fact"


class TargetMode(str, Enum):
    ACTOR = "actor"
    EACH_SCOPE_TARGET = "each_scope_target"
    FIXED = "fixed"


@dataclass(frozen=True, slots=True)
class FactEffect:
    kind: EffectKind
    target_mode: TargetMode
    predicate: str
    literal_value: str | None = None
    parameter_name: str | None = None
    fixed_target: WorldRef | None = None

    def __post_init__(self) -> None:
        if not self.predicate:
            raise ValueError("fact effect requires a predicate")
        if self.kind is EffectKind.SET_FACT:
            if (self.literal_value is None) == (self.parameter_name is None):
                raise ValueError("SET_FACT requires exactly one value source")
        elif self.literal_value is not None or self.parameter_name is not None:
            raise ValueError("DELETE_FACT cannot carry a value")
        if (self.target_mode is TargetMode.FIXED) != (self.fixed_target is not None):
            raise ValueError("fixed target mode requires exactly one fixed ref")


@dataclass(frozen=True, slots=True)
class RuleIR:
    rule_id: str
    action: str
    phase: RulePhase
    requires: tuple[str, ...]
    outputs: tuple[str, ...]
    effects: tuple[FactEffect, ...]
    permission_action: str
    monotone: bool = True

    def __post_init__(self) -> None:
        if not self.rule_id or not self.action or not self.permission_action:
            raise ValueError("rule identity/action/permission required")
        if len(set(self.requires)) != len(self.requires) or len(set(self.outputs)) != len(self.outputs):
            raise ValueError("duplicate rule dependency key")
        if self.phase is RulePhase.POST_COMMIT and self.effects:
            raise ValueError("POST_COMMIT rules generate a later intent, not current effects")
