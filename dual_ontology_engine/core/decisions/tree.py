from __future__ import annotations

from dataclasses import dataclass
from enum import Enum


class NodeKind(str, Enum):
    DECISION = "decision"
    OUTCOME = "outcome"
    OBSERVATION = "observation"
    INTERRUPTION = "interruption"
    TERMINAL = "terminal"


@dataclass(frozen=True, slots=True)
class ActionPath:
    path_id: str
    action_id: str
    tags: tuple[str, ...]
    outcome_refs: tuple[str, ...] = ()
    reversibility_rank: int = 0
    information_gain: int = 0
    risk_rank: int = 0

    def __post_init__(self) -> None:
        if min(self.reversibility_rank, self.information_gain, self.risk_rank) < 0:
            raise ValueError("path fallback ranks cannot be negative")


@dataclass(frozen=True, slots=True)
class DecisionNode:
    node_id: str
    kind: NodeKind
    path: ActionPath | None = None
    children: tuple["DecisionNode", ...] = ()


def validate_tree(root: DecisionNode, *, max_depth: int, max_branching: int, max_nodes: int) -> int:
    seen: set[str] = set(); count = 0
    def visit(node: DecisionNode, depth: int) -> None:
        nonlocal count
        if node.node_id in seen: raise ValueError("decision graph cycle or duplicate node")
        if depth > max_depth or len(node.children) > max_branching: raise ValueError("decision tree bound exceeded")
        seen.add(node.node_id); count += 1
        if count > max_nodes: raise ValueError("decision node budget exceeded")
        if node.kind is NodeKind.TERMINAL and node.children: raise ValueError("terminal node has children")
        for child in node.children: visit(child, depth + 1)
    visit(root, 0)
    return count


def finite_paths(root: DecisionNode, *, max_depth: int, max_branching: int, max_nodes: int) -> tuple[ActionPath, ...]:
    """验证有限树并按规范顺序提取其中声明的候选路径。"""

    validate_tree(root, max_depth=max_depth, max_branching=max_branching, max_nodes=max_nodes)
    paths: dict[str, ActionPath] = {}
    stack = [root]
    while stack:
        node = stack.pop()
        if node.path is not None:
            old = paths.get(node.path.path_id)
            if old is not None and old != node.path:
                raise ValueError("same path id has conflicting content")
            paths[node.path.path_id] = node.path
        stack.extend(reversed(node.children))
    return tuple(paths[key] for key in sorted(paths))
