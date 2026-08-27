from __future__ import annotations

from fractions import Fraction
from typing import TypeAlias

Exact: TypeAlias = Fraction
MAX_EXACT_BITS = 256


def exact(value: int | str | Fraction) -> Fraction:
    """将正式数值收敛到确定性的有理数表示。"""

    if isinstance(value, float):
        raise TypeError("binary floats are forbidden in authoritative values")
    result = value if isinstance(value, Fraction) else Fraction(value)
    if result.numerator.bit_length() > MAX_EXACT_BITS or result.denominator.bit_length() > MAX_EXACT_BITS:
        raise OverflowError("exact value exceeds the 256-bit policy")
    return result


def checked(value: Fraction) -> Fraction:
    return exact(value)


def add(left: Fraction, right: Fraction) -> Fraction: return checked(left + right)
def sub(left: Fraction, right: Fraction) -> Fraction: return checked(left - right)
def mul(left: Fraction, right: Fraction) -> Fraction: return checked(left * right)
def div(left: Fraction, right: Fraction) -> Fraction:
    if right == 0: raise ZeroDivisionError("exact division by zero")
    return checked(left / right)


def bounded(value: Fraction, lower: Fraction, upper: Fraction) -> Fraction:
    if lower > upper:
        raise ValueError("invalid interval")
    return min(upper, max(lower, value))
