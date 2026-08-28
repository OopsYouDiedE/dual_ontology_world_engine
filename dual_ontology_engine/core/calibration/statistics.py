from __future__ import annotations

from dataclasses import dataclass
from fractions import Fraction

from ..foundation.numbers import add, div, mul, sub


@dataclass(frozen=True, slots=True)
class CostEstimate:
    dimension: str
    point: Fraction | None
    lower: Fraction | None
    upper: Fraction | None
    confidence_delta: Fraction | None
    model_version: str
    evidence_refs: tuple[str, ...]

    def __post_init__(self) -> None:
        values = (self.point, self.lower, self.upper, self.confidence_delta)
        if any(
            value is not None and (not isinstance(value, Fraction) or value < 0)
            for value in values
        ):
            raise ValueError("cost estimate requires nonnegative exact statistics")
        if self.lower is not None and self.upper is not None and self.lower > self.upper:
            raise ValueError("estimate interval inverted")
        if self.confidence_delta is not None and not 0 < self.confidence_delta < 1:
            raise ValueError("confidence delta outside (0,1)")
        if not self.evidence_refs:
            raise ValueError("estimate requires evidence")


@dataclass(frozen=True, slots=True)
class TailRiskEstimate:
    alpha: Fraction
    quantile: Fraction
    cvar: Fraction
    effective_tail_samples: Fraction
    sample_count: int


def _validate_sample(values: tuple[Fraction, ...]) -> None:
    if not values:
        raise ValueError("empty sample")
    if any(not isinstance(value, Fraction) or value < 0 for value in values):
        raise ValueError("sample requires nonnegative Fraction")


def empirical_mean(values: tuple[Fraction, ...]) -> Fraction:
    _validate_sample(values)
    total = Fraction()
    for value in values:
        total = add(total, value)
    return div(total, Fraction(len(values)))


def sample_variance(values: tuple[Fraction, ...]) -> Fraction:
    _validate_sample(values)
    if len(values) < 2:
        raise ValueError("sample variance requires at least two observations")
    mean = empirical_mean(values)
    total = Fraction()
    for value in values:
        difference = sub(value, mean)
        total = add(total, mul(difference, difference))
    return div(total, Fraction(len(values) - 1))


def empirical_quantile(values: tuple[Fraction, ...], alpha: Fraction) -> Fraction:
    _validate_sample(values)
    if not isinstance(alpha, Fraction) or not 0 <= alpha <= 1:
        raise ValueError("quantile alpha outside [0,1]")
    ordered = sorted(values)
    if alpha == 0:
        return ordered[0]
    rank = (alpha.numerator * len(ordered) + alpha.denominator - 1) // alpha.denominator
    return ordered[min(len(ordered) - 1, rank - 1)]


def empirical_cvar(values: tuple[Fraction, ...], alpha: Fraction) -> TailRiskEstimate:
    """精确计算经验上尾 CVaR，并报告实际尾部样本质量。"""

    _validate_sample(values)
    if not isinstance(alpha, Fraction) or not 0 <= alpha < 1:
        raise ValueError("CVaR alpha outside [0,1)")
    ordered = sorted(values, reverse=True)
    tail_mass = mul(Fraction(len(ordered)), sub(Fraction(1), alpha))
    full = tail_mass.numerator // tail_mass.denominator
    remainder = tail_mass - full
    weighted = Fraction()
    for value in ordered[:full]:
        weighted = add(weighted, value)
    if remainder and full < len(ordered):
        weighted = add(weighted, mul(remainder, ordered[full]))
    cvar = div(weighted, tail_mass)
    return TailRiskEstimate(
        alpha,
        empirical_quantile(values, alpha),
        cvar,
        tail_mass,
        len(values),
    )


def joint_confidence_delta(
    total_delta: Fraction, *, models: int, metrics: int, scenario_families: int
) -> Fraction:
    if not isinstance(total_delta, Fraction) or not 0 < total_delta < 1:
        raise ValueError("total confidence delta outside (0,1)")
    if min(models, metrics, scenario_families) <= 0:
        raise ValueError("joint confidence multiplicities must be positive")
    return div(total_delta, Fraction(models * metrics * scenario_families))


def zero_failure_upper_bound(sample_count: int, delta: Fraction) -> Fraction:
    """零失败样本的保守有理上界，不把零观测描述为零风险。"""

    if sample_count <= 0 or not isinstance(delta, Fraction) or not 0 < delta < 1:
        raise ValueError("invalid confidence request")
    # 由 (1-p)^n <= 1/(1+n p) 得到的保守反演；无需浮点或超越函数。
    return min(Fraction(1), div(sub(Fraction(1), delta), mul(Fraction(sample_count), delta)))
