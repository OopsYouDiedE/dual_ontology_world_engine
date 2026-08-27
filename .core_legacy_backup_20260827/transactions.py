"""Atomic, proof-carrying state transitions.

The public ``WorldState`` is mutable for scenario construction.  This module
therefore establishes atomicity by validating and applying every change to a
private clone.  A caller receives either that complete clone or the untouched
input state.
"""

from __future__ import annotations

from dataclasses import dataclass, field, replace
from enum import Enum
from threading import RLock
from typing import Any, Callable, Iterable, Mapping, Protocol

from .audit import canonical_value
from .errors import ConflictError, EngineError, ValidationError
from .resources import CostContext, Payment, ResourceBundle
from .types import Layer, Proposition, ResourceKind, Support, Truth, Vec3
from .world import WorldState


class ProofStatus(str, Enum):
    PROVED = "proved"
    REFUTED = "refuted"
    CAPABILITY_BOUNDARY = "capability_boundary"


class TransactionStatus(str, Enum):
    COMMITTED = "committed"
    REJECTED = "rejected"
    STALE = "stale"


@dataclass(frozen=True)
class ProofResult:
    obligation: str
    status: ProofStatus
    detail: str = ""


class Effect(Protocol):
    """A typed state difference, never an arbitrary replacement state."""

    def validate(self, state: WorldState) -> None:
                                                   """校验前置条件与约束规范是否满足要求。
                                                   
                                                   Args:
                                                       state (WorldState): 当前状态对象。
                                                   """
                                                   ...

    def apply(self, state: WorldState) -> None:
                                                """将变化效果应用到目标状态中。
                                                
                                                Args:
                                                    state (WorldState): 当前状态对象。
                                                """
                                                ...

    def describe(self) -> Mapping[str, Any]:
                                             """导出当前对象的中文或结构化详细描述。
                                             
                                             Returns:
                                                 Mapping[Tuple[str, Any]]: 计算或处理后的 Mapping[Tuple[str, Any]] 结果。
                                             """
                                             ...


ProofCheck = Callable[[WorldState, "Proposal"], bool | ProofStatus | ProofResult]


@dataclass(frozen=True)
class ProofObligation:
    id: str
    check: ProofCheck

    def prove(self, state: WorldState, proposal: "Proposal") -> ProofResult:
        """执行 prove 对应的业务逻辑处理。
        
        Args:
            state (WorldState): 当前状态对象。
            proposal (Proposal): 待提交的状态修改提案。
        
        Returns:
            ProofResult: 计算或处理后的 ProofResult 结果。
        """
        try:
            outcome = self.check(state, proposal)
        except EngineError as exc:
            return ProofResult(self.id, ProofStatus.REFUTED, str(exc))
        except Exception as exc:  # custom checks cannot corrupt the commit path
            return ProofResult(self.id, ProofStatus.REFUTED, f"check raised {type(exc).__name__}: {exc}")
        if isinstance(outcome, ProofResult):
            return ProofResult(self.id, outcome.status, outcome.detail)
        if isinstance(outcome, ProofStatus):
            return ProofResult(self.id, outcome)
        return ProofResult(self.id, ProofStatus.PROVED if outcome else ProofStatus.REFUTED)


@dataclass(frozen=True)
class FactEffect:
    proposition: Proposition
    value: Truth

    def validate(self, state: WorldState) -> None:
        """校验前置条件与约束规范是否满足要求。
        
        Args:
            state (WorldState): 当前状态对象。
        """
        if self.proposition.layer not in {Layer.OBJECTIVE, Layer.INSTITUTIONAL, Layer.IDENTITY}:
            raise ValidationError("fact effects cannot target a cognitive layer")
        value = ~self.value if self.proposition.negated else self.value
        current = state.facts.get(self.proposition.atom, Truth.UNKNOWN)
        if current is not Truth.UNKNOWN and value is not Truth.UNKNOWN and current is not value:
            raise ConflictError("fact effect conflicts with a committed fact in the same layer")

    def apply(self, state: WorldState) -> None:
        """将变化效果应用到目标状态中。
        
        Args:
            state (WorldState): 当前状态对象。
        """
        self.validate(state)
        value = ~self.value if self.proposition.negated else self.value
        state.facts[self.proposition.atom] = value

    def describe(self) -> Mapping[str, Any]:
        """导出当前对象的中文或结构化详细描述。
        
        Returns:
            Mapping[Tuple[str, Any]]: 计算或处理后的 Mapping[Tuple[str, Any]] 结果。
        """
        return {
            "effect": "fact",
            "atom": str(self.proposition.atom),
            "negated": self.proposition.negated,
            "value": self.value.value,
        }


@dataclass(frozen=True)
class CognitionEffect:
    actor: str
    proposition: Proposition
    layer: Layer
    truth: Truth
    add: bool = True

    def validate(self, state: WorldState) -> None:
        """校验前置条件与约束规范是否满足要求。
        
        Args:
            state (WorldState): 当前状态对象。
        """
        if self.layer not in {Layer.KNOWLEDGE, Layer.BELIEF, Layer.PERCEPTION}:
            raise ValidationError("cognition effects require a cognitive layer")
        if not self.actor:
            raise ValidationError("cognition effect requires an actor")

    def apply(self, state: WorldState) -> None:
        """将变化效果应用到目标状态中。
        
        Args:
            state (WorldState): 当前状态对象。
        """
        self.validate(state)
        key = (self.actor, self.layer, self.proposition.atom)
        current = state.cognition.get(key, Support.NONE)
        truth = ~self.truth if self.proposition.negated else self.truth
        state.cognition[key] = current.add(truth) if self.add else current.remove(truth)

    def describe(self) -> Mapping[str, Any]:
        """导出当前对象的中文或结构化详细描述。
        
        Returns:
            Mapping[Tuple[str, Any]]: 计算或处理后的 Mapping[Tuple[str, Any]] 结果。
        """
        return {
            "effect": "cognition",
            "actor": self.actor,
            "layer": self.layer.value,
            "atom": str(self.proposition.atom),
            "truth": self.truth.value,
            "add": self.add,
        }


@dataclass(frozen=True)
class ConstraintEffect:
    key: str
    value: Any = True
    remove: bool = False

    _PROTECTED_PREFIXES = ("meta:", "audit:", "registry:", "consent:")

    def validate(self, state: WorldState) -> None:
        """校验前置条件与约束规范是否满足要求。
        
        Args:
            state (WorldState): 当前状态对象。
        """
        if not self.key or self.key.startswith(self._PROTECTED_PREFIXES):
            raise ValidationError("constraint effect targets a protected namespace")
        if not self.remove:
            canonical_value(self.value)

    def apply(self, state: WorldState) -> None:
        """将变化效果应用到目标状态中。
        
        Args:
            state (WorldState): 当前状态对象。
        """
        self.validate(state)
        if self.remove:
            state.active_constraints.pop(self.key, None)
        else:
            state.active_constraints[self.key] = self.value

    def describe(self) -> Mapping[str, Any]:
        """导出当前对象的中文或结构化详细描述。
        
        Returns:
            Mapping[Tuple[str, Any]]: 计算或处理后的 Mapping[Tuple[str, Any]] 结果。
        """
        return {"effect": "constraint", "key": self.key, "remove": self.remove, "value": self.value}


@dataclass(frozen=True)
class ConsentContract:
    owner: str
    payer: str
    proposal_id: str
    lots: frozenset[str]
    active: bool = True


@dataclass(frozen=True)
class ConsentEffect:
    """An owner-authenticated, proposal-bound third-party payment grant."""

    owner: str
    payer: str
    proposal_id: str
    lots: frozenset[str] = frozenset()
    revoke: bool = False

    def validate(self, state: WorldState) -> None:
        """校验前置条件与约束规范是否满足要求。
        
        Args:
            state (WorldState): 当前状态对象。
        """
        del state
        if not self.owner or not self.payer or not self.proposal_id:
            raise ValidationError("consent grant requires owner, payer, and proposal id")
        if any(not lot for lot in self.lots):
            raise ValidationError("consent lot ids cannot be empty")

    def apply(self, state: WorldState) -> None:
        """将变化效果应用到目标状态中。
        
        Args:
            state (WorldState): 当前状态对象。
        """
        self.validate(state)
        key = f"consent:{self.owner}:{self.proposal_id}"
        if self.revoke:
            state.active_constraints.pop(key, None)
        else:
            state.active_constraints[key] = ConsentContract(
                self.owner,
                self.payer,
                self.proposal_id,
                self.lots,
            )

    def describe(self) -> Mapping[str, Any]:
        """导出当前对象的中文或结构化详细描述。
        
        Returns:
            Mapping[Tuple[str, Any]]: 计算或处理后的 Mapping[Tuple[str, Any]] 结果。
        """
        return {
            "effect": "consent",
            "owner": self.owner,
            "payer": self.payer,
            "proposal": self.proposal_id,
            "lots": sorted(self.lots),
            "revoke": self.revoke,
        }


@dataclass(frozen=True)
class PositionEffect:
    """A typed position write; topology membership is checked by the scheduler."""

    actor: str
    room_id: str
    position: Vec3

    def validate(self, state: WorldState) -> None:
        """校验前置条件与约束规范是否满足要求。
        
        Args:
            state (WorldState): 当前状态对象。
        """
        if not self.actor or not self.room_id:
            raise ValidationError("position effect requires actor and room ids")
        if self.actor not in state.characters and self.actor not in state.anomalies:
            raise ValidationError("position effect names an unknown actor")

    def apply(self, state: WorldState) -> None:
        """将变化效果应用到目标状态中。
        
        Args:
            state (WorldState): 当前状态对象。
        """
        self.validate(state)
        state.positions[self.actor] = (self.room_id, self.position)

    def describe(self) -> Mapping[str, Any]:
        """导出当前对象的中文或结构化详细描述。
        
        Returns:
            Mapping[Tuple[str, Any]]: 计算或处理后的 Mapping[Tuple[str, Any]] 结果。
        """
        return {
            "effect": "position",
            "actor": self.actor,
            "room": self.room_id,
            "position": {"x": self.position.x, "y": self.position.y, "z": self.position.z},
        }


@dataclass(frozen=True)
class CapabilityClosureEffect:
    """Permanently consume access to a registered character capability."""

    actor: str
    capability: str
    source: str

    def validate(self, state: WorldState) -> None:
        """校验前置条件与约束规范是否满足要求。
        
        Args:
            state (WorldState): 当前状态对象。
        """
        character = state.characters.get(self.actor)
        if character is None or self.capability not in character.capabilities:
            raise ValidationError("permanent sacrifice names an unavailable capability")
        if not self.source:
            raise ValidationError("permanent sacrifice records its source")

    def apply(self, state: WorldState) -> None:
        """将变化效果应用到目标状态中。
        
        Args:
            state (WorldState): 当前状态对象。
        """
        self.validate(state)
        character = state.characters[self.actor]
        state.characters[self.actor] = replace(
            character,
            capabilities=character.capabilities - {self.capability},
        )
        state.active_constraints[f"permanent_capability:{self.actor}:{self.capability}"] = {
            "source": self.source,
            "closed": True,
        }

    def describe(self) -> Mapping[str, Any]:
        """导出当前对象的中文或结构化详细描述。
        
        Returns:
            Mapping[Tuple[str, Any]]: 计算或处理后的 Mapping[Tuple[str, Any]] 结果。
        """
        return {
            "effect": "capability_closure",
            "actor": self.actor,
            "capability": self.capability,
            "source": self.source,
            "permanent": True,
        }


@dataclass(frozen=True)
class RuleAdoptionEffect:
    """A typed revision of one actor's rule credibility and adoption state.

    Adoption is a cognitive commitment, not a rule validity change.  This effect
    therefore writes only the subject's ``obeyed_rules`` set and the recorded
    per-actor credibility; it never edits the rule itself.
    """

    actor: str
    rule_id: str
    credibility_milli: int
    adopt: bool
    source: str

    def validate(self, state: WorldState) -> None:
        """校验前置条件与约束规范是否满足要求。
        
        Args:
            state (WorldState): 当前状态对象。
        """
        if not self.actor or not self.rule_id or not self.source:
            raise ValidationError("rule adoption revision requires actor, rule, and source")
        if self.actor not in state.characters:
            raise ValidationError("rule adoption revision names an unknown character")
        if isinstance(self.credibility_milli, bool) or not isinstance(self.credibility_milli, int):
            raise ValidationError("recorded credibility must be a fixed-point integer")
        if not 0 <= self.credibility_milli <= 1_000:
            raise ValidationError("recorded credibility must lie inside [0, 1000]")

    def apply(self, state: WorldState) -> None:
        """将变化效果应用到目标状态中。
        
        Args:
            state (WorldState): 当前状态对象。
        """
        self.validate(state)
        character = state.characters[self.actor]
        adopted = character.obeyed_rules
        state.characters[self.actor] = replace(
            character,
            obeyed_rules=(adopted | {self.rule_id}) if self.adopt else (adopted - {self.rule_id}),
        )
        state.active_constraints[f"rule_credibility:{self.actor}:{self.rule_id}"] = {
            "credibility_milli": self.credibility_milli,
            "adopted": self.adopt,
            "source": self.source,
        }

    def describe(self) -> Mapping[str, Any]:
        """导出当前对象的中文或结构化详细描述。
        
        Returns:
            Mapping[Tuple[str, Any]]: 计算或处理后的 Mapping[Tuple[str, Any]] 结果。
        """
        return {
            "effect": "rule_adoption",
            "actor": self.actor,
            "rule": self.rule_id,
            "credibility_milli": self.credibility_milli,
            "adopt": self.adopt,
            "source": self.source,
        }


@dataclass(frozen=True)
class Proposal:
    id: str
    base_version: int
    actor: str
    mechanism: str
    effects: tuple[Effect, ...]
    requirement: ResourceBundle = field(default_factory=ResourceBundle)
    payment: Payment | None = None
    consenting_payers: frozenset[str] = frozenset()
    obligations: tuple[ProofObligation, ...] = ()
    reads: frozenset[str] = frozenset()
    writes: frozenset[str] = frozenset()
    causes: tuple[int, ...] = ()
    audit_payload: Mapping[str, Any] = field(default_factory=dict)

    def __post_init__(self) -> None:
        """数据类初始化后的字段校验与约束校验。
        """
        if not self.id or not self.actor or not self.mechanism:
            raise ValidationError("proposal requires id, actor, and mechanism")
        if self.requirement.total > 0 and self.payment is None:
            raise ValidationError("a nonzero requirement requires an explicit payment")
        if self.payment is not None and self.payment.payer != self.actor:
            if self.payment.payer not in self.consenting_payers:
                raise ValidationError("a non-actor payer requires explicit consent")


@dataclass(frozen=True)
class TransactionResult:
    status: TransactionStatus
    state: WorldState
    proposal_id: str
    proofs: tuple[ProofResult, ...] = ()
    reason: str = ""
    event_sequence: int | None = None

    @property
    def committed(self) -> bool:
        """执行 committed 对应的业务逻辑处理。
        
        Returns:
            bool: 校验或判定结果布尔值。
        """
        return self.status is TransactionStatus.COMMITTED


def _effect_validation(state: WorldState, proposal: Proposal) -> bool:
    """执行 _effect_validation 对应的业务逻辑处理。
    
    Args:
        state (WorldState): 当前状态对象。
        proposal (Proposal): 待提交的状态修改提案。
    
    Returns:
        bool: 校验或判定结果布尔值。
    """
    for effect in proposal.effects:
        if not is_authoritative_effect(effect):
            raise ValidationError(
                "authoritative proposals accept only the engine's closed effect types"
            )
        if isinstance(effect, ConsentEffect) and effect.owner != proposal.actor:
            raise ValidationError("only the resource owner can issue or revoke consent")
        effect.validate(state)
    return True


def authoritative_effect_types() -> tuple[type[Any], ...]:
    """执行 authoritative_effect_types 对应的业务逻辑处理。
    
    Returns:
        tuple[Tuple[type[Any], Ellipsis]]: 计算或处理后的 tuple[Tuple[type[Any], Ellipsis]] 结果。
    """
    # Lazy import avoids a module cycle while keeping the authority set closed
    # to exact engine-owned classes. Subclasses and structural lookalikes do not
    # acquire write authority.
    return (
        FactEffect,
        CognitionEffect,
        ConstraintEffect,
        CapabilityClosureEffect,
        ConsentEffect,
        PositionEffect,
        RuleAdoptionEffect,
        *_EXTENSION_EFFECT_TYPES,
    )


_EXTENSION_EFFECT_TYPES: list[type[Any]] = []
_NORM_RECORD_PROVIDERS: list[Callable[[WorldState, str], Iterable[Any]]] = []


def register_authoritative_effect_type(effect_type: type[Any]) -> None:
    """Register one support-library effect as an authoritative closed-state effect."""
    if effect_type not in _EXTENSION_EFFECT_TYPES:
        _EXTENSION_EFFECT_TYPES.append(effect_type)


def register_norm_record_provider(
    provider: Callable[[WorldState, str], Iterable[Any]],
) -> None:
    """Register an optional support library's normalized-rule projection."""
    if provider not in _NORM_RECORD_PROVIDERS:
        _NORM_RECORD_PROVIDERS.append(provider)


def is_authoritative_effect(effect: Any) -> bool:
    """判断是否满足 authoritative effect 相关的条件规范。
    
    Args:
        effect (Any): 输入参数 effect。
    
    Returns:
        bool: 校验或判定结果布尔值。
    """
    return type(effect) in authoritative_effect_types()


def _meta_safety(state: WorldState, proposal: Proposal) -> bool:
    """执行 _meta_safety 对应的业务逻辑处理。
    
    Args:
        state (WorldState): 当前状态对象。
        proposal (Proposal): 待提交的状态修改提案。
    
    Returns:
        bool: 校验或判定结果布尔值。
    """
    return all(not key.startswith(("meta:", "audit:", "registry:")) for key in proposal.writes)


def _payment_consent(state: WorldState, proposal: Proposal) -> bool:
    """执行 _payment_consent 对应的业务逻辑处理。
    
    Args:
        state (WorldState): 当前状态对象。
        proposal (Proposal): 待提交的状态修改提案。
    
    Returns:
        bool: 校验或判定结果布尔值。
    """
    if proposal.payment is None:
        return True
    for allocation in proposal.payment.allocations:
        owner = state.resources.get(allocation.lot_id).owner
        if owner != proposal.payment.payer:
            if owner not in proposal.consenting_payers:
                raise ValidationError(f"resource owner {owner} has not consented to payment")
            contract = state.active_constraints.get(f"consent:{owner}:{proposal.id}")
            if type(contract) is not ConsentContract:
                raise ValidationError(f"resource owner {owner} lacks a state-backed consent contract")
            if contract.payer != proposal.payment.payer or contract.active is not True:
                raise ValidationError(f"resource owner {owner} consent contract is invalid")
            allowed_lots = contract.lots
            if allowed_lots and allocation.lot_id not in allowed_lots:
                raise ValidationError(f"resource lot {allocation.lot_id} is outside the consent contract")
    return True


def _effect_cost_floor(state: WorldState, proposal: Proposal) -> bool:
    """执行 _effect_cost_floor 对应的业务逻辑处理。
    
    Args:
        state (WorldState): 当前状态对象。
        proposal (Proposal): 待提交的状态修改提案。
    
    Returns:
        bool: 校验或判定结果布尔值。
    """
    for effect in proposal.effects:
        kind = getattr(effect, "required_resource_kind", None)
        amount = float(getattr(effect, "required_resource_amount", 0.0))
        if kind is not None and proposal.requirement.amount(kind) + 1e-9 < amount:
            raise ValidationError(f"effect requires at least {amount:g} {kind.value}")
    return True


def _norm_compliance(state: WorldState, proposal: Proposal) -> bool:
    """执行 _norm_compliance 对应的业务逻辑处理。
    
    Args:
        state (WorldState): 当前状态对象。
        proposal (Proposal): 待提交的状态修改提案。
    
    Returns:
        bool: 校验或判定结果布尔值。
    """
    action = proposal.audit_payload.get("norm_action")
    if not isinstance(action, str) or not action or proposal.actor not in state.characters:
        return True
    adopted = state.characters[proposal.actor].obeyed_rules
    forbidden = any(
        record.action == action
        and getattr(record.modality, "value", record.modality) == "forbidden"
        and getattr(record.state, "value", record.state) == "active"
        and record.rule_id in adopted
        for provider in _NORM_RECORD_PROVIDERS
        for record in provider(state, proposal.actor)
    )
    if forbidden:
        raise ValidationError(f"an active adopted rule forbids action {action}")
    return True


CORE_OBLIGATIONS = (
    ProofObligation("PO-TYPED", _effect_validation),
    ProofObligation("PO-META", _meta_safety),
    ProofObligation("PO-CONSENT", _payment_consent),
    ProofObligation("PO-COVERAGE-FLOOR", _effect_cost_floor),
    ProofObligation("PO-NORM-COMPLIANCE", _norm_compliance),
)


class AtomicCommitter:
    """Serialize and atomically publish proof-carrying proposals."""

    def __init__(self) -> None:
        """初始化实例对象。
        """
        self._lock = RLock()

    def commit(
        self,
        state: WorldState,
        proposal: Proposal,
        *,
        cost_context: CostContext | None = None,
        axioms: Iterable[Any] = (),
    ) -> TransactionResult:
        """原子提交状态修改提案并生成新状态头。
        
        Args:
            state (WorldState): 当前状态对象。
            proposal (Proposal): 待提交的状态修改提案。
        
        Returns:
            TransactionResult: 事务提交结果对象。
        """
        with self._lock:
            if proposal.base_version != state.version:
                return TransactionResult(
                    TransactionStatus.STALE,
                    state,
                    proposal.id,
                    reason=f"base version {proposal.base_version} does not match current version {state.version}",
                )

            original_backup = state.clone()
            try:
                original_digest = state.authority_digest()
            except ValidationError as exc:
                return TransactionResult(
                    TransactionStatus.REJECTED,
                    state,
                    proposal.id,
                    reason=str(exc),
                )

            # All untrusted proof and effect validation runs against a private
            # clone. A hostile validator can only corrupt its disposable copy.
            proofs = [
                item.prove(state.clone(), proposal)
                for item in (*CORE_OBLIGATIONS, *proposal.obligations)
            ]
            for axiom in axioms:
                proofs.append(self._prove_axiom(axiom, state.clone(), proposal))
            try:
                escaped = state.authority_digest() != original_digest
            except ValidationError:
                escaped = True
            if escaped:
                state.restore_from(original_backup)
                return TransactionResult(
                    TransactionStatus.REJECTED,
                    state,
                    proposal.id,
                    tuple(proofs),
                    reason="an untrusted proof callback mutated the authoritative input state",
                )
            failed = next((item for item in proofs if item.status is not ProofStatus.PROVED), None)
            if failed is not None:
                return TransactionResult(
                    TransactionStatus.REJECTED,
                    state,
                    proposal.id,
                    tuple(proofs),
                    reason=f"{failed.obligation}: {failed.detail or failed.status.value}",
                )

            draft = state.clone()
            try:
                if proposal.payment is not None:
                    draft.resources.verify(proposal.payment, proposal.requirement, cost_context or CostContext())
                    draft.resources.lock(proposal.id, proposal.payment)
                for effect in proposal.effects:
                    effect.validate(draft)
                    effect.apply(draft)
                if proposal.payment is not None:
                    draft.resources.consume(proposal.id, proposal.payment)
                next_version = state.version + 1
                event = draft.ledger.append(
                    time=draft.time,
                    turn=draft.turn,
                    kind=(
                        "rule.enforced"
                        if proposal.mechanism == "rule.enforcement"
                        else "transaction.commit"
                    ),
                    actor=proposal.actor,
                    payload={
                        "transaction": proposal.id,
                        "mechanism": proposal.mechanism,
                        "base_version": proposal.base_version,
                        "effects": [effect.describe() for effect in proposal.effects],
                        "proofs": [
                            {"id": proof.obligation, "status": proof.status.value, "detail": proof.detail}
                            for proof in proofs
                        ],
                        **dict(proposal.audit_payload),
                    },
                    causes=proposal.causes,
                )
                draft.version = next_version
                return TransactionResult(
                    TransactionStatus.COMMITTED,
                    draft,
                    proposal.id,
                    tuple(proofs),
                    event_sequence=event.sequence,
                )
            except Exception as exc:
                return TransactionResult(
                    TransactionStatus.REJECTED,
                    state,
                    proposal.id,
                    tuple(proofs),
                    reason=str(exc),
                )

    @staticmethod
    def _prove_axiom(axiom: Any, state: WorldState, proposal: Proposal) -> ProofResult:
        """执行 _prove_axiom 对应的业务逻辑处理。
        
        Args:
            axiom (Any): 输入参数 axiom。
            state (WorldState): 当前状态对象。
            proposal (Proposal): 待提交的状态修改提案。
        
        Returns:
            ProofResult: 计算或处理后的 ProofResult 结果。
        """
        identifier = getattr(axiom, "id", type(axiom).__name__)
        try:
            outcome = axiom.check(state, proposal)
        except Exception as exc:
            return ProofResult(str(identifier), ProofStatus.REFUTED, str(exc))
        value = getattr(outcome, "value", outcome)
        if value is True or value == "proved":
            status = ProofStatus.PROVED
        elif value == "capability_boundary":
            status = ProofStatus.CAPABILITY_BOUNDARY
        else:
            status = ProofStatus.REFUTED
        return ProofResult(str(identifier), status)


class TransactionalWorld:
    """Own a single latest head so concurrent stale proposals cannot both win."""

    def __init__(self, initial: WorldState, committer: AtomicCommitter | None = None) -> None:
        """初始化实例对象。
        
        Args:
            initial (WorldState): 输入参数 initial。
            committer (Any): 输入参数 committer。
        """
        self._state = initial
        self._committer = committer or AtomicCommitter()
        self._head_lock = RLock()

    @property
    def state(self) -> WorldState:
        """执行 state 对应的业务逻辑处理。
        
        Returns:
            WorldState: 更新或克隆后的世界状态实例。
        """
        with self._head_lock:
            return self._state.clone()

    def submit(
        self,
        proposal: Proposal,
        *,
        cost_context: CostContext | None = None,
        axioms: Iterable[Any] = (),
    ) -> TransactionResult:
        """提交状态变更提案并尝试执行原子更新。
        
        Args:
            proposal (Proposal): 待提交的状态修改提案。
        
        Returns:
            TransactionResult: 事务提交结果对象。
        """
        with self._head_lock:
            result = self._committer.commit(
                self._state,
                proposal,
                cost_context=cost_context,
                axioms=axioms,
            )
            if result.committed:
                self._state = result.state
            return result


def cognitive_support(
    state: WorldState,
    actor: str,
    proposition: Proposition,
    layer: Layer,
) -> Support:
    """Read cognitive support with the same negation normalization as facts。
    
    Args:
        state (WorldState): 当前状态对象。
        actor (str): 执行动作的主体角色标识。
        proposition (Proposition): 待评估的闭合命题对象。
        layer (Layer): 输入参数 layer。
    
    Returns:
        Support: 命题支持集合对象。
    """

    support = state.support(actor, proposition, layer)
    if not proposition.negated or support in {Support.NONE, Support.BOTH}:
        return support
    if support is Support.TRUE_ONLY:
        return Support.FALSE_ONLY
    return Support.TRUE_ONLY


_DEFAULT_COMMITTER = AtomicCommitter()


def commit(
    state: WorldState,
    proposal: Proposal,
    *,
    cost_context: CostContext | None = None,
    axioms: Iterable[Any] = (),
) -> TransactionResult:
    """原子提交状态修改提案并生成新状态头。
    
    Args:
        state (WorldState): 当前状态对象。
        proposal (Proposal): 待提交的状态修改提案。
    
    Returns:
        TransactionResult: 事务提交结果对象。
    """
    return _DEFAULT_COMMITTER.commit(state, proposal, cost_context=cost_context, axioms=axioms)
