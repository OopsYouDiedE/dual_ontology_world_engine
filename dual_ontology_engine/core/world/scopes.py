from __future__ import annotations

from dataclasses import dataclass
from enum import Enum

from ..foundation.budgets import BudgetMeter
from ..foundation.codec import canonical_bytes, content_id
from ..foundation.ids import WorldRef
from .versions import WorldState


class ScopeOp(str, Enum):
    EXPLICIT = "explicit"
    RELATION_NEIGHBORS = "relation_neighbors"
    UNION = "union"
    INTERSECTION = "intersection"
    DIFFERENCE = "difference"


@dataclass(frozen=True, slots=True)
class ScopeExpr:
    op: ScopeOp
    explicit_refs: tuple[WorldRef, ...] = ()
    anchor_ref: WorldRef | None = None
    relation_kind: str | None = None
    children: tuple["ScopeExpr", ...] = ()
    max_targets: int = 64

    def __post_init__(self) -> None:
        if self.max_targets < 0:
            raise ValueError("scope target budget cannot be negative")
        if self.op is ScopeOp.EXPLICIT:
            if self.anchor_ref is not None or self.relation_kind is not None or self.children:
                raise ValueError("explicit scope has only explicit refs")
        elif self.op is ScopeOp.RELATION_NEIGHBORS:
            if self.anchor_ref is None or not self.relation_kind or self.explicit_refs or self.children:
                raise ValueError("relation scope requires one anchor and relation kind")
        elif self.op in {ScopeOp.UNION, ScopeOp.INTERSECTION}:
            if len(self.children) < 2 or self.explicit_refs or self.anchor_ref is not None:
                raise ValueError("set scope requires at least two children")
        elif self.op is ScopeOp.DIFFERENCE:
            if len(self.children) != 2 or self.explicit_refs or self.anchor_ref is not None:
                raise ValueError("difference scope is binary")

    @property
    def scope_id(self) -> str:
        return content_id(self)


@dataclass(frozen=True, slots=True)
class ScopeResolution:
    scope_id: str
    refs: tuple[WorldRef, ...]
    normalized_query: bytes
    dependency_epoch: int
    meter: BudgetMeter


def resolve_scope(
    state: WorldState,
    expression: ScopeExpr,
    meter: BudgetMeter,
) -> ScopeResolution:
    """在冻结世界版本上解析有限作用域，并生成谓词读取依据。"""

    known = {entity.ref for entity in state.entities.values()} | {
        subject.ref for subject in state.subjects.values()
    }

    def evaluate(node: ScopeExpr) -> set[WorldRef]:
        nonlocal meter
        meter = meter.charge(instructions=1, nodes=1)
        if node.op is ScopeOp.EXPLICIT:
            result = set(node.explicit_refs)
        elif node.op is ScopeOp.RELATION_NEIGHBORS:
            result = set()
            for relation in sorted(state.relations.values(), key=lambda item: item.relation_id):
                meter = meter.charge(instructions=1)
                if relation.kind != node.relation_kind:
                    continue
                refs = {endpoint.ref for endpoint in relation.endpoints}
                if node.anchor_ref in refs:
                    result.update(refs - {node.anchor_ref})
        else:
            child_values = [evaluate(child) for child in node.children]
            if node.op is ScopeOp.UNION:
                result = set().union(*child_values)
            elif node.op is ScopeOp.INTERSECTION:
                result = set.intersection(*child_values)
            else:
                result = child_values[0] - child_values[1]
        if not result.issubset(known):
            raise ValueError("scope resolved an unknown or cross-branch reference")
        if len(result) > node.max_targets:
            raise ValueError("scope target budget exceeded")
        return result

    refs = tuple(sorted(evaluate(expression)))
    return ScopeResolution(
        expression.scope_id,
        refs,
        canonical_bytes(expression),
        state.predicate_epoch,
        meter,
    )
