from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from typing import Mapping


class Truth(Enum):
    FALSE = 0
    UNKNOWN = 1
    TRUE = 2


@dataclass(frozen=True, slots=True)
class Predicate:
    name: str
    arguments: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class BoolExpr:
    op: str
    predicate: Predicate | None = None
    children: tuple["BoolExpr", ...] = ()

    def __post_init__(self) -> None:
        allowed = {"ATOM", "AND", "OR", "NOT"}
        if self.op not in allowed:
            raise ValueError("unknown expression operator")
        if self.op == "ATOM" and (self.predicate is None or self.children):
            raise ValueError("ATOM requires exactly a predicate")
        if self.op == "NOT" and len(self.children) != 1:
            raise ValueError("NOT is unary")
        if self.op in {"AND", "OR"} and not self.children:
            raise ValueError("boolean connective cannot be empty")


def evaluate(expr: BoolExpr, facts: Mapping[Predicate, Truth]) -> Truth:
    if expr.op == "ATOM": return facts.get(expr.predicate, Truth.UNKNOWN)
    values = tuple(evaluate(child, facts) for child in expr.children)
    if expr.op == "NOT": return {Truth.TRUE: Truth.FALSE, Truth.FALSE: Truth.TRUE, Truth.UNKNOWN: Truth.UNKNOWN}[values[0]]
    if expr.op == "AND": return min(values, key=lambda item: item.value)
    return max(values, key=lambda item: item.value)

