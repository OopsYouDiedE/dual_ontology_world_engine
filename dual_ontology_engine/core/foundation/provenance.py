from __future__ import annotations

from dataclasses import dataclass
from .codec import content_id


def canonical_digest(value: object) -> str:
    return content_id(value)


@dataclass(frozen=True, slots=True)
class Provenance:
    provenance_id: str
    rule_id: str | None
    event_id: str | None
    fact_ids: tuple[str, ...] = ()
    parent_ids: tuple[str, ...] = ()

    def __post_init__(self) -> None:
        if not self.provenance_id or self.provenance_id in self.parent_ids:
            raise ValueError("invalid or immediately self-referential provenance")

    @property
    def content_id(self) -> str:
        return canonical_digest((self.rule_id,self.event_id,self.fact_ids,self.parent_ids))


def make_provenance(*, rule_id: str|None, event_id: str|None, fact_ids: tuple[str,...]=(), parent_ids: tuple[str,...]=()) -> Provenance:
    identifier=canonical_digest((rule_id,event_id,fact_ids,parent_ids))
    return Provenance(identifier,rule_id,event_id,fact_ids,parent_ids)


def validate_provenance_dag(nodes: tuple[Provenance, ...]) -> None:
    by_id = {node.provenance_id: node for node in nodes}
    if len(by_id) != len(nodes): raise ValueError("duplicate provenance id")
    if any(node.provenance_id != node.content_id for node in nodes): raise ValueError("provenance id is not content-bound")
    if any(parent not in by_id for node in nodes for parent in node.parent_ids): raise ValueError("unknown provenance parent")
    indegree = {key: 0 for key in by_id}; children = {key: [] for key in by_id}
    for node in nodes:
        for parent in node.parent_ids: indegree[node.provenance_id] += 1; children[parent].append(node.provenance_id)
    queue = sorted(key for key, degree in indegree.items() if degree == 0); visited = 0
    while queue:
        key = queue.pop(0); visited += 1
        for child in sorted(children[key]):
            indegree[child] -= 1
            if indegree[child] == 0: queue.append(child); queue.sort()
    if visited != len(nodes): raise ValueError("provenance cycle")
