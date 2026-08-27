from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True, slots=True)
class TraceNode:
    trace_id: str
    kind: str
    public_payload: tuple[tuple[str, str], ...]
    parent_ids: tuple[str, ...] = ()
    objective_only: bool = False


@dataclass(frozen=True, slots=True)
class TraceDag:
    nodes: tuple[TraceNode, ...]

    def __post_init__(self) -> None:
        ids = {n.trace_id for n in self.nodes}
        if len(ids) != len(self.nodes): raise ValueError("duplicate trace id")
        graph = {n.trace_id: n.parent_ids for n in self.nodes}; visiting: set[str] = set(); done: set[str] = set()
        def visit(key: str) -> None:
            if key in visiting: raise ValueError("trace cycle")
            if key in done: return
            visiting.add(key)
            for parent in graph.get(key, ()): 
                if parent in graph: visit(parent)
            visiting.remove(key); done.add(key)
        for key in graph: visit(key)

    def epistemic_projection(self) -> "TraceDag":
        visible = tuple(node for node in self.nodes if not node.objective_only)
        visible_ids = {n.trace_id for n in visible}
        return TraceDag(tuple(TraceNode(n.trace_id, n.kind, n.public_payload, tuple(p for p in n.parent_ids if p in visible_ids), False) for n in visible))

