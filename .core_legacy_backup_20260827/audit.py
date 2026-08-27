"""Append-only audit ledger and causal dependency graph."""

from __future__ import annotations

from dataclasses import asdict, dataclass
from enum import Enum
from hashlib import sha256
import json
from math import isfinite
from types import MappingProxyType
from typing import Any, Iterable, Iterator, Mapping

from .errors import ValidationError


GENESIS = "GENESIS"


class FrozenDict(Mapping[str, Any]):
    """Recursively immutable mapping used by committed audit events."""

    __slots__ = ("_data", "_sealed")

    def __init__(self, values: Mapping[str, Any]) -> None:
        """初始化实例对象。
        
        Args:
            values (Mapping[Tuple[str, Any]]): 输入参数 values。
        """
        object.__setattr__(
            self,
            "_data",
            MappingProxyType({str(key): _freeze(value) for key, value in values.items()}),
        )
        object.__setattr__(self, "_sealed", True)

    def __setattr__(self, name: str, value: Any) -> None:
        """执行 __setattr__ 对应的业务逻辑处理。
        
        Args:
            name (str): 名称或标识符字符串。
            value (Any): 输入参数 value。
        """
        if getattr(self, "_sealed", False):
            raise ValidationError("committed audit payloads cannot be rebound")
        object.__setattr__(self, name, value)

    def __getitem__(self, key: str) -> Any:
        """执行 __getitem__ 对应的业务逻辑处理。
        
        Args:
            key (str): 输入参数 key。
        
        Returns:
            Any: 计算或处理后的 Any 结果。
        """
        return self._data[key]

    def __iter__(self) -> Iterator[str]:
        """执行 __iter__ 对应的业务逻辑处理。
        
        Returns:
            Iterator[str]: 计算或处理后的 Iterator[str] 结果。
        """
        return iter(self._data)

    def __len__(self) -> int:
        """执行 __len__ 对应的业务逻辑处理。
        
        Returns:
            int: 计算得出的整数数值。
        """
        return len(self._data)

    def __deepcopy__(self, memo: dict[int, Any]) -> "FrozenDict":
        """执行 __deepcopy__ 对应的业务逻辑处理。
        
        Args:
            memo (dict[Tuple[int, Any]]): 输入参数 memo。
        
        Returns:
            FrozenDict: 计算或处理后的 FrozenDict 结果。
        """
        del memo
        return self


def _freeze(value: Any) -> Any:
    """执行 _freeze 对应的业务逻辑处理。
    
    Args:
        value (Any): 输入参数 value。
    
    Returns:
        Any: 计算或处理后的 Any 结果。
    """
    if isinstance(value, FrozenDict):
        return value
    if isinstance(value, Mapping):
        return FrozenDict(value)
    if isinstance(value, (list, tuple)):
        return tuple(_freeze(item) for item in value)
    if isinstance(value, (set, frozenset)):
        return frozenset(_freeze(item) for item in value)
    if value is None or isinstance(value, (bool, int, str)):
        return value
    if isinstance(value, float):
        if not isfinite(value):
            raise ValidationError("audit payload numbers must be finite")
        return value
    if isinstance(value, Enum):
        return _freeze(value.value)
    raise ValidationError(
        f"audit payload contains a non-canonical value: {type(value).__module__}.{type(value).__qualname__}"
    )


def canonical_value(value: Any) -> Any:
    """Return an owned immutable value or reject an opaque Python object。
    
    Args:
        value (Any): 输入参数 value。
    
    Returns:
        Any: 计算或处理后的 Any 结果。
    """

    return _freeze(value)


def _plain(value: Any) -> Any:
    """执行 _plain 对应的业务逻辑处理。
    
    Args:
        value (Any): 输入参数 value。
    
    Returns:
        Any: 计算或处理后的 Any 结果。
    """
    if isinstance(value, Mapping):
        return {str(key): _plain(item) for key, item in value.items()}
    if isinstance(value, (tuple, list)):
        return [_plain(item) for item in value]
    if isinstance(value, (set, frozenset)):
        plain = [_plain(item) for item in value]
        return sorted(
            plain,
            key=lambda item: json.dumps(
                item,
                sort_keys=True,
                ensure_ascii=False,
                separators=(",", ":"),
            ),
        )
    return value


def _canonical(value: Any) -> str:
    """执行 _canonical 对应的业务逻辑处理。
    
    Args:
        value (Any): 输入参数 value。
    
    Returns:
        str: 导出的字符串表达或摘要哈希。
    """
    return json.dumps(_plain(value), sort_keys=True, ensure_ascii=False, separators=(",", ":"), default=str)


@dataclass(frozen=True)
class AuditEvent:
    sequence: int
    time: float
    turn: int
    kind: str
    actor: str
    payload: Mapping[str, Any]
    causes: tuple[int, ...]
    previous_hash: str
    digest: str


class AuditLedger:
    def __init__(self, events: Iterable[AuditEvent] = ()) -> None:
        """初始化实例对象。
        
        Args:
            events (Iterable[AuditEvent]): 输入参数 events。
        """
        self._events = list(events)
        if not self.verify():
            raise ValidationError("invalid audit ledger chain")

    @property
    def events(self) -> tuple[AuditEvent, ...]:
        """执行 events 对应的业务逻辑处理。
        
        Returns:
            tuple[Tuple[AuditEvent, Ellipsis]]: 计算或处理后的 tuple[Tuple[AuditEvent, Ellipsis]] 结果。
        """
        return tuple(self._events)

    def append(
        self,
        *,
        time: float,
        turn: int,
        kind: str,
        actor: str,
        payload: Mapping[str, Any],
        causes: Iterable[int] = (),
    ) -> AuditEvent:
        """执行 append 对应的业务逻辑处理。
        
        Returns:
            AuditEvent: 计算或处理后的 AuditEvent 结果。
        """
        sequence = len(self._events) + 1
        previous = self._events[-1].digest if self._events else GENESIS
        cause_tuple = tuple(causes)
        if any(cause < 1 or cause >= sequence for cause in cause_tuple):
            raise ValidationError("causes must reference earlier committed events")
        frozen_payload = FrozenDict(payload)
        body = {
            "sequence": sequence,
            "time": time,
            "turn": turn,
            "kind": kind,
            "actor": actor,
            "payload": frozen_payload,
            "causes": cause_tuple,
            "previous_hash": previous,
        }
        digest = sha256(_canonical(body).encode("utf-8")).hexdigest()
        event = AuditEvent(digest=digest, **body)
        self._events.append(event)
        return event

    def clone(self) -> "AuditLedger":
        """执行 clone 对应的业务逻辑处理。
        
        Returns:
            AuditLedger: 计算或处理后的 AuditLedger 结果。
        """
        return AuditLedger(self._events)

    def verify(self) -> bool:
        """核验凭证、要求与约束是否完全合规。
        
        Returns:
            bool: 校验或判定结果布尔值。
        """
        previous = GENESIS
        for expected, event in enumerate(self._events, start=1):
            body = asdict(event)
            digest = body.pop("digest")
            if event.sequence != expected or event.previous_hash != previous:
                return False
            if any(cause < 1 or cause >= expected for cause in event.causes):
                return False
            if sha256(_canonical(body).encode("utf-8")).hexdigest() != digest:
                return False
            previous = digest
        return True

    def dependency_closure(self, sequence: int) -> frozenset[int]:
        """执行 dependency_closure 对应的业务逻辑处理。
        
        Args:
            sequence (int): 输入参数 sequence。
        
        Returns:
            frozenset[int]: 计算或处理后的 frozenset[int] 结果。
        """
        forward: dict[int, set[int]] = {}
        for event in self._events:
            for cause in event.causes:
                forward.setdefault(cause, set()).add(event.sequence)
        reached = {sequence}
        pending = [sequence]
        while pending:
            current = pending.pop()
            for dependent in forward.get(current, set()):
                if dependent not in reached:
                    reached.add(dependent)
                    pending.append(dependent)
        return frozenset(reached)

    def to_json(self) -> str:
        """执行 to_json 对应的业务逻辑处理。
        
        Returns:
            str: 导出的字符串表达或摘要哈希。
        """
        return _canonical([asdict(event) for event in self._events])
