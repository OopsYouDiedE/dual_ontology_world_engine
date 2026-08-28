from __future__ import annotations

from dataclasses import dataclass
from fractions import Fraction
from itertools import combinations

from ..foundation.codec import content_id


@dataclass(frozen=True, slots=True)
class ChannelDefinition:
    channel_id: str
    capacities: tuple[tuple[str, Fraction], ...]
    incompatible_classes: frozenset[tuple[str, str]] = frozenset()

    def __post_init__(self) -> None:
        keys = [key for key, _ in self.capacities]
        if not self.channel_id or len(keys) != len(set(keys)) or not keys:
            raise ValueError("channel requires unique capacity dimensions")
        if any(not isinstance(value, Fraction) or value < 0 for _, value in self.capacities):
            raise ValueError("channel capacity requires nonnegative Fraction")
        for left, right in self.incompatible_classes:
            if not left or not right or left > right:
                raise ValueError("incompatible class pairs must be normalized")

    def incompatible(self, left: str, right: str) -> bool:
        pair = tuple(sorted((left, right)))
        return pair in self.incompatible_classes


@dataclass(frozen=True, slots=True)
class ChannelLease:
    lease_id: str
    channel_id: str
    holder_ref: str
    usage: tuple[tuple[str, Fraction], ...]
    compatibility_class: str
    release_token: str
    acquired_epoch: int

    @property
    def content_id(self) -> str:
        return content_id(
            (
                self.channel_id,
                self.holder_ref,
                self.usage,
                self.compatibility_class,
                self.release_token,
                self.acquired_epoch,
            )
        )

    def __post_init__(self) -> None:
        if self.lease_id != self.content_id:
            raise ValueError("lease id not content-bound")
        keys = [key for key, _ in self.usage]
        if len(keys) != len(set(keys)) or not keys or self.acquired_epoch < 0:
            raise ValueError("lease usage/epoch invalid")
        if any(not isinstance(value, Fraction) or value <= 0 for _, value in self.usage):
            raise ValueError("lease usage requires positive Fraction")
        if not self.compatibility_class or not self.release_token:
            raise ValueError("lease compatibility class/release token required")


def make_channel_lease(
    *,
    channel_id: str,
    holder_ref: str,
    usage: tuple[tuple[str, Fraction], ...],
    compatibility_class: str,
    release_token: str,
    acquired_epoch: int,
) -> ChannelLease:
    values = (
        channel_id,
        holder_ref,
        usage,
        compatibility_class,
        release_token,
        acquired_epoch,
    )
    return ChannelLease(content_id(values), *values)


@dataclass(frozen=True, slots=True)
class LeaseRelease:
    lease_id: str
    holder_ref: str
    release_token: str


def validate_channel_leases(
    channels: dict[str, ChannelDefinition], leases: tuple[ChannelLease, ...]
) -> None:
    by_channel: dict[str, list[ChannelLease]] = {}
    for lease in leases:
        if lease.channel_id not in channels:
            raise ValueError(f"lease references unknown channel:{lease.lease_id}")
        by_channel.setdefault(lease.channel_id, []).append(lease)
    for channel_id, bucket in by_channel.items():
        channel = channels[channel_id]
        capacity = dict(channel.capacities)
        used: dict[str, Fraction] = {}
        ordered = sorted(bucket, key=lambda lease: lease.lease_id)
        for lease in ordered:
            for dimension, amount in lease.usage:
                if dimension not in capacity:
                    raise ValueError(f"lease uses unknown capacity:{lease.lease_id}")
                used[dimension] = used.get(dimension, Fraction()) + amount
            for other in ordered:
                if lease.lease_id < other.lease_id and channel.incompatible(
                    lease.compatibility_class, other.compatibility_class
                ):
                    raise ValueError(
                        f"incompatible channel leases:{lease.lease_id}:{other.lease_id}"
                    )
        for dimension, amount in used.items():
            if amount > capacity[dimension]:
                raise ValueError(f"channel capacity exceeded:{channel_id}:{dimension}")


@dataclass(frozen=True, slots=True)
class LeasePreemptionRule:
    challenger_class: str
    incumbent_class: str

    def __post_init__(self)->None:
        if not self.challenger_class or not self.incumbent_class or self.challenger_class==self.incumbent_class:raise ValueError("invalid lease preemption rule")


@dataclass(frozen=True, slots=True)
class LeasePreemptionPlan:
    candidate_lease_id: str
    release_lease_ids: tuple[str,...]


def validate_preemption_rules(rules:tuple[LeasePreemptionRule,...])->None:
    pairs={(rule.challenger_class,rule.incumbent_class) for rule in rules}
    if len(pairs)!=len(rules):raise ValueError("duplicate lease preemption rule")
    adjacency:dict[str,set[str]]={}
    for challenger,incumbent in pairs:adjacency.setdefault(challenger,set()).add(incumbent)
    visiting=set();done=set()
    def visit(node:str)->None:
        if node in visiting:raise ValueError("cyclic lease preemption order")
        if node in done:return
        visiting.add(node)
        for child in adjacency.get(node,()):visit(child)
        visiting.remove(node);done.add(node)
    for node in tuple(adjacency):visit(node)


def plan_lease_preemption(*,channels:dict[str,ChannelDefinition],candidate:ChannelLease,existing:tuple[ChannelLease,...],rules:tuple[LeasePreemptionRule,...],max_candidates:int=16)->LeasePreemptionPlan:
    """寻找唯一的包含关系最小抢占集；不可比较多解时拒绝任意选边。"""
    validate_preemption_rules(rules)
    relevant=tuple(sorted((lease for lease in existing if lease.channel_id==candidate.channel_id),key=lambda lease:lease.lease_id))
    if len(relevant)>max_candidates:raise ValueError("lease preemption candidate budget exceeded")
    try:
        validate_channel_leases(channels,relevant+(candidate,))
        return LeasePreemptionPlan(candidate.lease_id,())
    except ValueError:
        pass
    allowed={(rule.challenger_class,rule.incumbent_class) for rule in rules}
    removable=tuple(lease for lease in relevant if (candidate.compatibility_class,lease.compatibility_class) in allowed)
    feasible=[]
    for size in range(1,len(removable)+1):
        for subset in combinations(removable,size):
            removed={lease.lease_id for lease in subset}
            try:validate_channel_leases(channels,tuple(lease for lease in relevant if lease.lease_id not in removed)+(candidate,))
            except ValueError:continue
            feasible.append(frozenset(removed))
    minimal=tuple(item for item in feasible if not any(other<item for other in feasible))
    if not minimal:raise ValueError("no authorized lease preemption plan")
    if len(minimal)!=1:raise ValueError("multiple incomparable lease preemption plans")
    return LeasePreemptionPlan(candidate.lease_id,tuple(sorted(minimal[0])))
