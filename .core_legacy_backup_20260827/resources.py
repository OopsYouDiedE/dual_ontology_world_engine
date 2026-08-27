"""Typed resources, scenario-local exchange, locking, and consumption."""

from __future__ import annotations

from dataclasses import dataclass, field, replace
from enum import Enum
from math import isfinite
from typing import Iterable, Mapping

from .errors import PaymentError, ValidationError
from .types import Permanence, ResourceKind


@dataclass(frozen=True)
class ResourceBundle:
    amounts: Mapping[ResourceKind, float] = field(default_factory=dict)
    permanence: Permanence = Permanence.RECOVERABLE

    def __post_init__(self) -> None:
        """数据类初始化后的字段校验与约束校验。
        """
        if any(not isfinite(value) or value < 0 for value in self.amounts.values()):
            raise ValidationError("resource requirements must be finite and nonnegative")

    def amount(self, kind: ResourceKind) -> float:
        """查询指定类型资源的当前数量。
        
        Args:
            kind (ResourceKind): 输入参数 kind。
        
        Returns:
            float: 计算得出的浮点数值。
        """
        return float(self.amounts.get(kind, 0.0))

    @property
    def total(self) -> float:
        """计算并返回总资源数值。
        
        Returns:
            float: 计算得出的浮点数值。
        """
        return sum(self.amounts.values())


class LotState(str, Enum):
    AVAILABLE = "available"
    LOCKED = "locked"
    CONSUMED = "consumed"


@dataclass(frozen=True)
class ResourceLot:
    id: str
    owner: str
    kind: ResourceKind
    amount: float
    permanence: Permanence = Permanence.RECOVERABLE
    state: LotState = LotState.AVAILABLE
    locked_by: str | None = None
    consumed_amount: float = 0.0

    def __post_init__(self) -> None:
        """数据类初始化后的字段校验与约束校验。
        """
        if not self.id or not self.owner or not isfinite(self.amount) or self.amount <= 0:
            raise ValidationError("resource lot requires an id, owner, and positive amount")
        if not 0 <= self.consumed_amount <= self.amount:
            raise ValidationError("consumed amount lies outside the lot")
        if self.state is LotState.LOCKED and not self.locked_by:
            raise ValidationError("a locked lot names its transaction")
        if self.state is LotState.CONSUMED and self.consumed_amount <= 0:
            raise ValidationError("a consumed lot records a positive consumption")

    @property
    def available(self) -> float:
        """查询处于未锁定状态的可用资源量。
        
        Returns:
            float: 计算得出的浮点数值。
        """
        return 0.0 if self.state is not LotState.AVAILABLE else self.amount - self.consumed_amount


@dataclass(frozen=True)
class Exchange:
    source: ResourceKind
    target: ResourceKind
    rate: float
    irreversible_eligible: bool = False
    label: str = ""

    def __post_init__(self) -> None:
        """数据类初始化后的字段校验与约束校验。
        """
        if not isfinite(self.rate) or self.rate <= 0:
            raise ValidationError("exchange rate must be positive")


@dataclass(frozen=True)
class CostContext:
    """A scenario-specific, inspectable exchange table.

    The lack of an entry means incomparability. No global exchange rate exists.
    """

    exchanges: tuple[Exchange, ...] = ()

    def exchange(self, source: ResourceKind, target: ResourceKind) -> Exchange | None:
        """执行 exchange 对应的业务逻辑处理。
        
        Args:
            source (ResourceKind): 输入参数 source。
            target (ResourceKind): 目标实体或标识符。
        
        Returns:
            Any: 计算或处理后的 Any 结果。
        """
        if source is target:
            return Exchange(source, target, 1.0, False, "identity")
        return next((item for item in self.exchanges if item.source is source and item.target is target), None)


@dataclass(frozen=True)
class Allocation:
    lot_id: str
    target: ResourceKind
    source_amount: float

    def __post_init__(self) -> None:
        """数据类初始化后的字段校验与约束校验。
        """
        if not self.lot_id or not isfinite(self.source_amount) or self.source_amount <= 0:
            raise ValidationError("allocation requires a lot and positive amount")


@dataclass(frozen=True)
class Payment:
    payer: str
    allocations: tuple[Allocation, ...]
    allow_third_party: bool = False


class ResourceStore:
    def __init__(self, lots: Iterable[ResourceLot] = ()) -> None:
        """初始化实例对象。
        
        Args:
            lots (Iterable[ResourceLot]): 输入参数 lots。
        """
        materialized = tuple(lots)
        self._lots = {lot.id: lot for lot in materialized}
        if len(self._lots) != len(materialized):
            raise ValidationError("resource lot ids must be unique")

    def clone(self) -> "ResourceStore":
        """执行 clone 对应的业务逻辑处理。
        
        Returns:
            ResourceStore: 计算或处理后的 ResourceStore 结果。
        """
        return ResourceStore(self._lots.values())

    @property
    def lots(self) -> tuple[ResourceLot, ...]:
        """执行 lots 对应的业务逻辑处理。
        
        Returns:
            tuple[Tuple[ResourceLot, Ellipsis]]: 计算或处理后的 tuple[Tuple[ResourceLot, Ellipsis]] 结果。
        """
        return tuple(self._lots.values())

    def add(self, lot: ResourceLot) -> None:
        """执行 add 对应的业务逻辑处理。
        
        Args:
            lot (ResourceLot): 输入参数 lot。
        """
        if lot.id in self._lots:
            raise ValidationError(f"duplicate resource lot: {lot.id}")
        self._lots[lot.id] = lot

    def get(self, lot_id: str) -> ResourceLot:
        """获取或查询  相关属性与数据。
        
        Args:
            lot_id (str): 输入参数 lot_id。
        
        Returns:
            ResourceLot: 计算或处理后的 ResourceLot 结果。
        """
        try:
            return self._lots[lot_id]
        except KeyError as exc:
            raise PaymentError(f"unknown resource lot: {lot_id}") from exc

    def value_of(
        self,
        payment: Payment,
        requirement: ResourceBundle,
        context: CostContext,
    ) -> dict[ResourceKind, float]:
        """执行 value_of 对应的业务逻辑处理。
        
        Args:
            payment (Payment): 支付凭证或资源代价包。
            requirement (ResourceBundle): 预期的代价或守恒要求。
            context (CostContext): 场景代价或汇率上下文。
        
        Returns:
            dict[Tuple[ResourceKind, float]]: 计算或处理后的 dict[Tuple[ResourceKind, float]] 结果。
        """
        totals: dict[ResourceKind, float] = {}
        used: dict[str, float] = {}
        for allocation in payment.allocations:
            lot = self.get(allocation.lot_id)
            if lot.owner != payment.payer and not payment.allow_third_party:
                raise PaymentError("an unconsenting third party cannot pay")
            if lot.state is not LotState.AVAILABLE:
                raise PaymentError(f"resource lot is already {lot.state.value}: {lot.id}")
            used[lot.id] = used.get(lot.id, 0.0) + allocation.source_amount
            if used[lot.id] > lot.available + 1e-9:
                raise PaymentError(f"allocation exceeds lot: {lot.id}")
            exchange = context.exchange(lot.kind, allocation.target)
            if exchange is None:
                raise PaymentError(f"no declared exchange from {lot.kind.value} to {allocation.target.value}")
            if requirement.permanence is Permanence.IRREVERSIBLE:
                eligible = lot.permanence is Permanence.IRREVERSIBLE or exchange.irreversible_eligible
                if not eligible:
                    raise PaymentError("recoverable resources cannot fund an irreversible result")
            totals[allocation.target] = totals.get(allocation.target, 0.0) + allocation.source_amount * exchange.rate
        return totals

    def verify(self, payment: Payment, requirement: ResourceBundle, context: CostContext) -> None:
        """核验凭证、要求与约束是否完全合规。
        
        Args:
            payment (Payment): 支付凭证或资源代价包。
            requirement (ResourceBundle): 预期的代价或守恒要求。
            context (CostContext): 场景代价或汇率上下文。
        """
        totals = self.value_of(payment, requirement, context)
        for kind, required in requirement.amounts.items():
            if totals.get(kind, 0.0) + 1e-9 < required:
                raise PaymentError(
                    f"insufficient {kind.value}: paid={totals.get(kind, 0.0):g}, required={required:g}"
                )

    def lock(self, transaction_id: str, payment: Payment) -> None:
        """为特定事务锁定指定数量的资源。
        
        Args:
            transaction_id (str): 输入参数 transaction_id。
            payment (Payment): 支付凭证或资源代价包。
        """
        if not transaction_id:
            raise ValidationError("transaction id is required")
        for allocation in payment.allocations:
            lot = self.get(allocation.lot_id)
            self._lots[lot.id] = replace(lot, state=LotState.LOCKED, locked_by=transaction_id)

    def consume(self, transaction_id: str, payment: Payment) -> None:
        """从资源库中正式扣除已被锁定的资源。
        
        Args:
            transaction_id (str): 输入参数 transaction_id。
            payment (Payment): 支付凭证或资源代价包。
        """
        per_lot: dict[str, float] = {}
        for allocation in payment.allocations:
            per_lot[allocation.lot_id] = per_lot.get(allocation.lot_id, 0.0) + allocation.source_amount
        for lot_id, amount in per_lot.items():
            lot = self.get(lot_id)
            if lot.state is not LotState.LOCKED or lot.locked_by != transaction_id:
                raise PaymentError(f"transaction does not own locked lot: {lot_id}")
            consumed = lot.consumed_amount + amount
            next_state = LotState.CONSUMED if consumed >= lot.amount - 1e-9 else LotState.AVAILABLE
            self._lots[lot.id] = replace(
                lot,
                state=next_state,
                locked_by=None,
                consumed_amount=consumed,
            )

    def release(self, transaction_id: str) -> None:
        """释放因特定事务被锁定的资源。
        
        Args:
            transaction_id (str): 输入参数 transaction_id。
        """
        for lot in tuple(self._lots.values()):
            if lot.state is LotState.LOCKED and lot.locked_by == transaction_id:
                self._lots[lot.id] = replace(lot, state=LotState.AVAILABLE, locked_by=None)

    def available_totals(self, owner: str | None = None) -> dict[ResourceKind, float]:
        """执行 available_totals 对应的业务逻辑处理。
        
        Args:
            owner (Any): 输入参数 owner。
        
        Returns:
            dict[Tuple[ResourceKind, float]]: 计算或处理后的 dict[Tuple[ResourceKind, float]] 结果。
        """
        totals: dict[ResourceKind, float] = {}
        for lot in self._lots.values():
            if lot.state is LotState.AVAILABLE and (owner is None or lot.owner == owner):
                totals[lot.kind] = totals.get(lot.kind, 0.0) + lot.available
        return totals
