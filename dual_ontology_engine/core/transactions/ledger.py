from __future__ import annotations
from dataclasses import dataclass
from fractions import Fraction
from types import MappingProxyType
from typing import Mapping
from ..foundation.numbers import add, checked
from .proposals import ChargeGroup

@dataclass(frozen=True,slots=True)
class Ledger:
    balances:Mapping[tuple[str,str],Fraction]; settled_charge_keys:frozenset[str]=frozenset()
    def __post_init__(self):
        object.__setattr__(self,"balances",MappingProxyType(dict(self.balances)))
        if any(not isinstance(v,Fraction) or v<0 for v in self.balances.values()): raise ValueError("invalid ledger balance")
    def apply_group(self,group:ChargeGroup,*,branch_id:str)->"Ledger":
        key=group.charge_key
        if key.branch_id!=branch_id: raise ValueError("charge key belongs to another branch")
        if key.key_id in self.settled_charge_keys: raise ValueError("charge replay")
        balances=dict(self.balances); totals:dict[str,Fraction]={}
        for posting in group.postings:
            totals[posting.dimension]=add(totals.get(posting.dimension,Fraction()),posting.delta)
            account=(posting.account_ref,posting.dimension); balances[account]=add(balances.get(account,Fraction()),posting.delta)
            if balances[account]<0: raise ValueError("insufficient balance")
        if any(v!=0 for v in totals.values()): raise ValueError("unbalanced charge group")
        return Ledger(balances,self.settled_charge_keys|{key.key_id})
