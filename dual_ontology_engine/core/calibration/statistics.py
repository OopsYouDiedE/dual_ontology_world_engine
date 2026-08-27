from __future__ import annotations

from dataclasses import dataclass
from fractions import Fraction
from ..foundation.numbers import add,div


@dataclass(frozen=True, slots=True)
class CostEstimate:
    dimension: str
    point: Fraction | None
    lower: Fraction | None
    upper: Fraction | None
    confidence_delta: Fraction | None
    model_version: str
    evidence_refs: tuple[str, ...]
    def __post_init__(self):
        values=(self.point,self.lower,self.upper,self.confidence_delta)
        if any(v is not None and (not isinstance(v,Fraction) or v<0) for v in values): raise ValueError("cost estimate requires nonnegative exact statistics")
        if self.lower is not None and self.upper is not None and self.lower>self.upper: raise ValueError("estimate interval inverted")
        if not self.evidence_refs: raise ValueError("estimate requires evidence")


def empirical_mean(values: tuple[Fraction, ...]) -> Fraction:
    if not values: raise ValueError("empty sample")
    total=Fraction()
    for value in values:
        if not isinstance(value,Fraction) or value<0: raise ValueError("sample requires nonnegative Fraction")
        total=add(total,value)
    return div(total,Fraction(len(values)))


def zero_failure_upper_bound(sample_count: int, delta: Fraction) -> Fraction:
    """无灾难样本时的简单保守界：p <= ln(1/delta)/n 的有理上界。"""
    if sample_count <= 0 or not 0 < delta < 1: raise ValueError("invalid confidence request")
    # 避免浮点/超越函数进入正式数据；使用 union-bound 风格 1/(n*delta)。
    return min(Fraction(1), Fraction(1, sample_count) / delta)
