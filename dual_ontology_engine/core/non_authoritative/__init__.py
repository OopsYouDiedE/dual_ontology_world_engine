"""隔离的旧领域适配边界。

这里故意不提供 commit、Effect 注册或状态写入 API。旧对象只能转成外部展示数据，
不能进入 :class:`dual_ontology_engine.core.transactions.Kernel`。
"""
from __future__ import annotations
from dataclasses import dataclass

@dataclass(frozen=True,slots=True)
class LegacyDisplayRecord:
    """只能展示的惰性文本记录；不是权威协议成员。"""
    kind:str;label:str;payload_text:str

AUTHORITATIVE_BRIDGE_AVAILABLE=False
