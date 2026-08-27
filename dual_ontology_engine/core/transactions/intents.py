from __future__ import annotations

from dataclasses import MISSING, dataclass, fields

from ..foundation.codec import content_id
from ..foundation.ids import WorldRef
from ..world.scopes import ScopeExpr


@dataclass(frozen=True, slots=True)
class Intent:
    intent_id: str
    branch_id: str
    based_on_version: str
    actor_ref: str
    actor_identity_epoch: int
    action: str
    scope: ScopeExpr
    ability_source: WorldRef | None = None
    parameters: tuple[tuple[str, str], ...] = ()
    proof_hints: tuple[str, ...] = ()

    @property
    def content_id(self) -> str:
        return content_id(
            (
                self.branch_id,
                self.based_on_version,
                self.actor_ref,
                self.actor_identity_epoch,
                self.action,
                self.scope,
                self.ability_source,
                self.parameters,
                self.proof_hints,
            )
        )

    def __post_init__(self) -> None:
        if self.intent_id != self.content_id:
            raise ValueError("intent id must bind its full content")
        if not self.action or self.actor_identity_epoch < 0:
            raise ValueError("invalid intent actor/action")
        keys = [key for key, _ in self.parameters]
        if len(keys) != len(set(keys)):
            raise ValueError("duplicate intent parameter")


def make_intent(**values: object) -> Intent:
    values = dict(values)
    values["intent_id"] = ""
    for field in fields(Intent):
        if field.name in values:
            continue
        if field.default is not MISSING:
            values[field.name] = field.default
        elif field.default_factory is not MISSING:
            values[field.name] = field.default_factory()
    draft = object.__new__(Intent)
    for field_name, field_value in values.items():
        object.__setattr__(draft, field_name, field_value)
    object.__setattr__(draft, "intent_id", Intent.content_id.__get__(draft, Intent))
    Intent.__post_init__(draft)
    return draft
