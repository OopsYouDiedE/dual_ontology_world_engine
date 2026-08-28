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
        if self.op == "FINITE_SUM" and not isinstance(self.literal,str):
            raise ValueError("FINITE_SUM requires a frozen collection key")


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


@dataclass(frozen=True,slots=True)
class FormulaAnalysis:
    node_count:int
    parameter_keys:tuple[str,...]
    fact_keys:tuple[str,...]
    finite_set_keys:tuple[str,...]
    monotone:bool


_FORBIDDEN_INPUTS=frozenset({"$selected","$final_quote","$current_candidate","$release_score"})


def analyze_formula(expr:FormulaExpr,*,max_nodes:int=256,allowed_parameters:frozenset[str]|None=None,allowed_facts:frozenset[str]|None=None,allowed_finite_sets:frozenset[str]|None=None,require_monotone:bool=False)->FormulaAnalysis:
    """静态登记依赖、拒绝对象环和选择/报价自指。"""
    active:set[int]=set();seen:set[int]=set();parameters=set();facts=set();finite_sets=set();monotone_by_id={}
    def visit(node:FormulaExpr)->bool:
        identity=id(node)
        if identity in active:raise ValueError("formula object cycle")
        if identity in seen:return monotone_by_id[identity]
        active.add(identity);seen.add(identity)
        if len(seen)>max_nodes:raise ValueError("formula node budget exceeded")
        child_monotone=tuple(visit(child) for child in node.args)
        if node.op=="PARAM":parameters.add(str(node.literal))
        elif node.op=="FACT" and node.literal!="$item":facts.add(str(node.literal))
        elif node.op=="FINITE_SUM":finite_sets.add(str(node.literal))
        if node.op in {"CONST","PARAM","FACT"}:monotone=True
        elif node.op in {"ADD","MIN","MAX","FINITE_SUM"}:monotone=all(child_monotone)
        elif node.op=="MUL_CONST":monotone=all(child_monotone) and isinstance(node.args[1].literal,Fraction) and node.args[1].literal>=0
        elif node.op=="CLAMP":monotone=all(child_monotone)
        else:monotone=False
        active.remove(identity);monotone_by_id[identity]=monotone;return monotone
    monotone=visit(expr)
    all_inputs=parameters|facts|finite_sets
    forbidden=all_inputs&_FORBIDDEN_INPUTS
    if forbidden:raise ValueError(f"formula reads forbidden self-reference:{','.join(sorted(forbidden))}")
    if allowed_parameters is not None and not parameters.issubset(allowed_parameters):raise ValueError("formula reads undeclared parameter")
    if allowed_facts is not None and not facts.issubset(allowed_facts):raise ValueError("formula reads undeclared fact")
    if allowed_finite_sets is not None and not finite_sets.issubset(allowed_finite_sets):raise ValueError("formula reads undeclared finite set")
    if require_monotone and not monotone:raise ValueError("formula lacks required monotonicity proof")
    return FormulaAnalysis(len(seen),tuple(sorted(parameters)),tuple(sorted(facts)),tuple(sorted(finite_sets)),monotone)


def evaluate(expr: FormulaExpr, context: FormulaContext) -> Fraction:
    analyze_formula(expr,max_nodes=context.max_nodes,allowed_parameters=frozenset(context.parameters),allowed_facts=frozenset(context.facts),allowed_finite_sets=frozenset(context.finite_sets))
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
