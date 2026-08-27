from __future__ import annotations

from dataclasses import dataclass

from ..foundation.codec import content_id
from .proposals import ConflictRank, ConflictStrategy, Proposal


@dataclass(frozen=True, slots=True)
class ConflictComponent:
    component_id: str
    atomic_group_ids: tuple[str, ...]
    addresses: tuple[str, ...]
    strategy: ConflictStrategy | None
    winner_group_id: str | None
    outcome: str


@dataclass(frozen=True, slots=True)
class ConflictResolution:
    accepted: tuple[Proposal, ...]
    rejected: tuple[tuple[str, str], ...]
    components: tuple[ConflictComponent, ...] = ()


def _group_id(proposal: Proposal) -> str:
    return proposal.atomic_group_id or proposal.proposal_id


def _operation_payloads(proposals: tuple[Proposal, ...]) -> dict[str, frozenset[str]]:
    result: dict[str, set[str]] = {}
    for proposal in proposals:
        for operation in proposal.operations:
            result.setdefault(operation.address, set()).add(
                content_id((operation.kind, operation.value))
            )
    return {address: frozenset(values) for address, values in result.items()}


def _groups_conflict(left: tuple[Proposal, ...], right: tuple[Proposal, ...]) -> tuple[str, ...]:
    left_payloads = _operation_payloads(left)
    right_payloads = _operation_payloads(right)
    addresses = []
    for address in sorted(set(left_payloads) & set(right_payloads)):
        if left_payloads[address] != right_payloads[address]:
            addresses.append(address)

    left_charges = {
        group.charge_key.key_id for proposal in left for group in proposal.charge_groups
    }
    right_charges = {
        group.charge_key.key_id for proposal in right for group in proposal.charge_groups
    }
    addresses.extend(f"charge:{key}" for key in sorted(left_charges & right_charges))
    return tuple(addresses)


def _group_policy(group: tuple[Proposal, ...]) -> tuple[ConflictStrategy, ConflictRank] | None:
    strategies = {proposal.conflict_strategy for proposal in group}
    ranks = {proposal.conflict_rank for proposal in group}
    if len(strategies) != 1 or len(ranks) != 1:
        return None
    return next(iter(strategies)), next(iter(ranks))


def resolve_conflicts(proposals: tuple[Proposal, ...]) -> ConflictResolution:
    """构造原子组冲突图，并按连通分量进行排列不变的求解。"""

    if len({proposal.proposal_id for proposal in proposals}) != len(proposals):
        return ConflictResolution((), (("batch", "duplicate proposal id"),))

    grouped: dict[str, list[Proposal]] = {}
    for proposal in proposals:
        grouped.setdefault(_group_id(proposal), []).append(proposal)
    groups = {
        key: tuple(sorted(values, key=lambda proposal: proposal.proposal_id))
        for key, values in grouped.items()
    }
    group_ids = sorted(groups)

    rejected_groups: dict[str, str] = {}
    components: list[ConflictComponent] = []
    for group_id in group_ids:
        payloads = _operation_payloads(groups[group_id])
        duplicate_charge_keys = [
            charge_group.charge_key.key_id
            for proposal in groups[group_id]
            for charge_group in proposal.charge_groups
        ]
        internal_addresses = tuple(
            sorted(address for address, values in payloads.items() if len(values) > 1)
        )
        if len(duplicate_charge_keys) != len(set(duplicate_charge_keys)):
            internal_addresses += tuple(
                f"charge:{key}"
                for key in sorted(
                    {
                        key
                        for key in duplicate_charge_keys
                        if duplicate_charge_keys.count(key) > 1
                    }
                )
            )
        if not internal_addresses:
            continue
        rejected_groups[group_id] = f"conflict inside atomic group:{group_id}"
        component_values = (
            (group_id,),
            internal_addresses,
            None,
            None,
            "invalid_atomic_group",
        )
        components.append(ConflictComponent(content_id(component_values), *component_values))

    resolvable_group_ids = [key for key in group_ids if key not in rejected_groups]

    adjacency: dict[str, set[str]] = {key: set() for key in resolvable_group_ids}
    edge_addresses: dict[tuple[str, str], tuple[str, ...]] = {}
    for index, left_id in enumerate(resolvable_group_ids):
        for right_id in resolvable_group_ids[index + 1 :]:
            addresses = _groups_conflict(groups[left_id], groups[right_id])
            if not addresses:
                continue
            adjacency[left_id].add(right_id)
            adjacency[right_id].add(left_id)
            edge_addresses[(left_id, right_id)] = addresses

    visited: set[str] = set()
    for root in resolvable_group_ids:
        if root in visited or not adjacency[root]:
            continue
        stack = [root]
        member_ids: list[str] = []
        while stack:
            current = stack.pop()
            if current in visited:
                continue
            visited.add(current)
            member_ids.append(current)
            stack.extend(sorted(adjacency[current] - visited, reverse=True))
        member_ids.sort()
        member_set = set(member_ids)
        addresses = tuple(
            sorted(
                {
                    address
                    for (left, right), values in edge_addresses.items()
                    if left in member_set and right in member_set
                    for address in values
                }
            )
        )

        policies = {group_id: _group_policy(groups[group_id]) for group_id in member_ids}
        policy_values = {value[0] for value in policies.values() if value is not None}
        strategy = next(iter(policy_values)) if len(policy_values) == 1 and all(
            value is not None for value in policies.values()
        ) else None
        winner: str | None = None
        if strategy is ConflictStrategy.DOMINANCE:
            candidates = []
            for group_id in member_ids:
                rank = policies[group_id][1]  # type: ignore[index]
                if all(
                    group_id == other_id
                    or rank.dominates(policies[other_id][1])  # type: ignore[index]
                    for other_id in member_ids
                ):
                    candidates.append(group_id)
            if len(candidates) == 1:
                winner = candidates[0]

        if winner is not None:
            outcome = "dominance"
            for group_id in member_ids:
                if group_id != winner:
                    rejected_groups[group_id] = f"dominated in conflict component:{root}"
        else:
            outcome = (
                "cancelled"
                if strategy is ConflictStrategy.CANCEL_COMPONENT
                else "unresolved"
            )
            reason = (
                f"cancelled conflict component:{root}"
                if outcome == "cancelled"
                else f"unresolved conflict component:{root}"
            )
            for group_id in member_ids:
                rejected_groups[group_id] = reason

        component_values = (tuple(member_ids), addresses, strategy, winner, outcome)
        components.append(
            ConflictComponent(content_id(component_values), *component_values)
        )

    accepted = tuple(
        sorted(
            (
                proposal
                for group_id, group in groups.items()
                if group_id not in rejected_groups
                for proposal in group
            ),
            key=lambda proposal: proposal.proposal_id,
        )
    )
    rejected = tuple(
        sorted(
            (
                (proposal.proposal_id, rejected_groups[group_id])
                for group_id, group in groups.items()
                if group_id in rejected_groups
                for proposal in group
            )
        )
    )
    return ConflictResolution(accepted, rejected, tuple(sorted(components, key=lambda item: item.component_id)))
