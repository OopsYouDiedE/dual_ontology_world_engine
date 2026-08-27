from __future__ import annotations

from dataclasses import dataclass
from fractions import Fraction
from types import MappingProxyType
from typing import Literal, Mapping
from ..foundation.numbers import add,sub,mul,checked

FormulaOp = Literal["CONST", "PARAM", "FACT", "ADD", "SUB", "MUL_CONST", "MIN", "MAX", "CLAMP", "IF", "FINITE_SUM"]


@dataclass(frozen=True, slots=True)
class FormulaExpr:
    op: FormulaOp
    args: tuple["FormulaExpr", ...] = ()
    literal: Fraction | str | None = None

    def __post_init__(self) -> None:
        arities = {"CONST": 0, "PARAM": 0, "FACT": 0, "ADD": 2, "SUB": 2, "MUL_CONST": 2, "MIN": 2, "MAX": 2, "CLAMP": 3, "IF": 3, "FINITE_SUM": 1}
        if self.op not in arities or len(self.args) != arities[self.op]:
            raise ValueError(f"invalid formula arity for {self.op}")
        if self.op in {"CONST", "PARAM", "FACT"} and self.literal is None:
            raise ValueError("leaf formula requires a literal")
        if self.op == "MUL_CONST" and self.args[1].op != "CONST":
            raise ValueError("MUL_CONST requires a literal constant multiplier")


@dataclass(frozen=True, slots=True)
class FormulaContext:
    parameters: Mapping[str, Fraction]
    facts: Mapping[str, Fraction]
    finite_sets: Mapping[str, tuple[Fraction, ...]]
    max_nodes: int = 256
    max_collection: int = 128

    def __post_init__(self) -> None:
        object.__setattr__(self, "parameters", MappingProxyType(dict(self.parameters)))
        object.__setattr__(self, "facts", MappingProxyType(dict(self.facts)))
        object.__setattr__(self, "finite_sets", MappingProxyType(dict(self.finite_sets)))
        if self.max_nodes<1 or self.max_collection<0: raise ValueError("invalid formula budget")
        if any(not isinstance(v,Fraction) for v in tuple(self.parameters.values())+tuple(self.facts.values())+tuple(x for values in self.finite_sets.values() for x in values)): raise TypeError("formula context requires Fraction")


def evaluate(expr: FormulaExpr, context: FormulaContext) -> Fraction:
    used = 0

    def ev(node: FormulaExpr, bound: Fraction | None = None) -> Fraction:
        nonlocal used
        used += 1
        if used > context.max_nodes:
            raise ValueError("formula node budget exceeded")
        if node.op == "CONST":
            if not isinstance(node.literal, Fraction):
                raise TypeError("CONST requires Fraction")
            return node.literal
        if node.op == "PARAM":
            return context.parameters[str(node.literal)]
        if node.op == "FACT":
            return bound if node.literal == "$item" and bound is not None else context.facts[str(node.literal)]
        if node.op == "FINITE_SUM":
            collection = context.finite_sets[str(node.literal)]
            if len(collection) > context.max_collection:
                raise ValueError("finite aggregation budget exceeded")
            total=Fraction()
            for item in collection: total=add(total,ev(node.args[0],item))
            return total
        if node.op == "IF":
            condition = ev(node.args[0], bound)
            return ev(node.args[1] if condition != 0 else node.args[2], bound)
        values = tuple(ev(arg, bound) for arg in node.args)
        if node.op == "ADD": return add(values[0],values[1])
        if node.op == "SUB": return sub(values[0],values[1])
        if node.op == "MUL_CONST": return mul(values[0],values[1])
        if node.op == "MIN": return checked(min(values))
        if node.op == "MAX": return checked(max(values))
        if node.op == "CLAMP": return checked(min(values[2], max(values[1], values[0])))
        raise AssertionError(node.op)

    return ev(expr)
