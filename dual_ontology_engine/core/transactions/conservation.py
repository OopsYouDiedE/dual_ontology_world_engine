from __future__ import annotations

from dataclasses import dataclass
from fractions import Fraction

from ..foundation.codec import content_id
from ..foundation.numbers import add, mul


@dataclass(frozen=True, slots=True)
class ConversionRule:
    rule_id: str
    inputs: tuple[tuple[str, Fraction], ...]
    outputs: tuple[tuple[str, Fraction], ...]
    external_authority_ref: str | None = None
    external_potential_quota: Fraction = Fraction()

    def __post_init__(self) -> None:
        input_keys = [key for key, _ in self.inputs]
        output_keys = [key for key, _ in self.outputs]
        if not self.rule_id or len(input_keys) != len(set(input_keys)) or len(output_keys) != len(set(output_keys)):
            raise ValueError("conversion rule identity/dimensions invalid")
        if any(
            not isinstance(value, Fraction) or value < 0
            for _, value in self.inputs + self.outputs
        ):
            raise ValueError("conversion quantities require nonnegative Fraction")
        if not self.inputs and not self.outputs:
            raise ValueError("zero-progress conversion")
        if not isinstance(self.external_potential_quota, Fraction) or self.external_potential_quota < 0:
            raise ValueError("external potential quota invalid")
        if (self.external_authority_ref is None) != (self.external_potential_quota == 0):
            raise ValueError("external source authority and positive quota must appear together")


@dataclass(frozen=True, slots=True)
class ConservationPotential:
    potential_id: str
    weights: tuple[tuple[str, Fraction], ...]

    @property
    def content_id(self) -> str:
        return content_id(self.weights)

    def __post_init__(self) -> None:
        if self.potential_id != self.content_id:
            raise ValueError("conservation potential id not content-bound")
        keys = [key for key, _ in self.weights]
        if len(keys) != len(set(keys)) or not keys:
            raise ValueError("conservation potential dimensions invalid")
        if any(not isinstance(value, Fraction) or value <= 0 for _, value in self.weights):
            raise ValueError("common conservation potential must be strictly positive")


def make_conservation_potential(
    weights: tuple[tuple[str, Fraction], ...]
) -> ConservationPotential:
    normalized = tuple(sorted(weights))
    return ConservationPotential(content_id(normalized), normalized)


@dataclass(frozen=True, slots=True)
class ConversionWitness:
    rule_id: str
    input_potential: Fraction
    output_potential: Fraction
    authorized_external_potential: Fraction


@dataclass(frozen=True, slots=True)
class ConservationCertificate:
    certificate_id: str
    potential_id: str
    witnesses: tuple[ConversionWitness, ...]

    @property
    def content_id(self) -> str:
        return content_id((self.potential_id, self.witnesses))

    def __post_init__(self) -> None:
        if self.certificate_id != self.content_id:
            raise ValueError("conservation certificate not content-bound")


def validate_conversion_domain(
    rules: tuple[ConversionRule, ...], potential: ConservationPotential
) -> ConservationCertificate:
    """验证共同严格正势；无授权转换不得增加总势。"""

    if not rules or len({rule.rule_id for rule in rules}) != len(rules):
        raise ValueError("conversion domain empty or has duplicate rule ids")
    weights = dict(potential.weights)
    dimensions = {
        key for rule in rules for key, _ in rule.inputs + rule.outputs
    }
    missing = dimensions - set(weights)
    if missing:
        raise ValueError(f"potential missing resource dimensions:{','.join(sorted(missing))}")

    def weighted(values: tuple[tuple[str, Fraction], ...]) -> Fraction:
        total = Fraction()
        for key, amount in values:
            total = add(total, mul(weights[key], amount))
        return total

    witnesses = []
    for rule in sorted(rules, key=lambda item: item.rule_id):
        input_potential = weighted(rule.inputs)
        output_potential = weighted(rule.outputs)
        increase = max(Fraction(), output_potential - input_potential)
        if increase > rule.external_potential_quota:
            raise ValueError(f"conversion increases common potential:{rule.rule_id}")
        witnesses.append(
            ConversionWitness(
                rule.rule_id,
                input_potential,
                output_potential,
                rule.external_potential_quota,
            )
        )
    values = (potential.potential_id, tuple(witnesses))
    return ConservationCertificate(content_id(values), *values)
