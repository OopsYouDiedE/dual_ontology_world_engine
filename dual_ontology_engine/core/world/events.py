from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from fractions import Fraction

from ..foundation.codec import content_id


class ScheduledEventState(str, Enum):
    SCHEDULED = "scheduled"
    FIRED = "fired"
    CANCELLED = "cancelled"


@dataclass(frozen=True, slots=True)
class ScheduledEvent:
    event_id: str
    branch_id: str
    due_time: Fraction
    event_kind: str
    payload: tuple[tuple[str, str], ...]
    state: ScheduledEventState
    provenance_id: str
    benefit_burden_pair_id: str | None = None

    @property
    def content_id(self) -> str:
        return content_id(
            (
                self.branch_id,
                self.due_time,
                self.event_kind,
                self.payload,
                self.provenance_id,
                self.benefit_burden_pair_id,
            )
        )

    def __post_init__(self) -> None:
        if self.event_id != self.content_id:
            raise ValueError("scheduled event id not content-bound")
        if not isinstance(self.due_time, Fraction) or self.due_time < 0:
            raise ValueError("scheduled event requires nonnegative exact time")
        if not self.event_kind or len(dict(self.payload)) != len(self.payload):
            raise ValueError("scheduled event kind/payload invalid")


def make_scheduled_event(
    *,
    branch_id: str,
    due_time: Fraction,
    event_kind: str,
    payload: tuple[tuple[str, str], ...],
    state: ScheduledEventState,
    provenance_id: str,
    benefit_burden_pair_id: str | None = None,
) -> ScheduledEvent:
    identity_values = (
        branch_id,
        due_time,
        event_kind,
        payload,
        provenance_id,
        benefit_burden_pair_id,
    )
    return ScheduledEvent(content_id(identity_values),branch_id,due_time,event_kind,payload,state,provenance_id,benefit_burden_pair_id)


def transition_scheduled_event(
    event: ScheduledEvent,
    target: ScheduledEventState,
    *,
    branch_id: str,
    current_time: Fraction,
) -> ScheduledEvent:
    if event.branch_id != branch_id:
        raise ValueError("scheduled event branch mismatch")
    if target is event.state:
        return event
    if event.state is not ScheduledEventState.SCHEDULED or target not in {
        ScheduledEventState.FIRED,
        ScheduledEventState.CANCELLED,
    }:
        raise ValueError("illegal scheduled event transition")
    if target is ScheduledEventState.FIRED and current_time < event.due_time:
        raise ValueError("scheduled event cannot fire before due time")
    return make_scheduled_event(
        branch_id=event.branch_id,
        due_time=event.due_time,
        event_kind=event.event_kind,
        payload=event.payload,
        state=target,
        provenance_id=event.provenance_id,
        benefit_burden_pair_id=event.benefit_burden_pair_id,
    )


def due_events(
    events: tuple[ScheduledEvent, ...],
    *,
    branch_id: str,
    current_time: Fraction,
    max_events: int = 128,
) -> tuple[ScheduledEvent, ...]:
    result = tuple(
        sorted(
            (
                event
                for event in events
                if event.branch_id == branch_id
                and event.state is ScheduledEventState.SCHEDULED
                and event.due_time <= current_time
            ),
            key=lambda event: (event.due_time, event.event_id),
        )
    )
    if len(result) > max_events:
        raise ValueError("due event batch budget exceeded")
    return result
