from __future__ import annotations

from collections import deque
from dataclasses import dataclass
from fractions import Fraction

from ..foundation.codec import content_id


@dataclass(frozen=True, slots=True)
class PropagationEdge:
    edge_id: str
    source: str
    target: str
    transfer_fraction: Fraction

    def __post_init__(self) -> None:
        if not self.edge_id or not self.source or not self.target:
            raise ValueError("propagation edge requires identity and endpoints")
        if not isinstance(self.transfer_fraction,Fraction) or not 0 < self.transfer_fraction <= 1:
            raise ValueError("propagation transfer fraction must be exact and in (0,1]")


@dataclass(frozen=True, slots=True)
class PropagationPolicy:
    minimum_quantum: Fraction | None = None
    residual_sink: str | None = None
    max_edge_visits: int | None = None
    max_steps: int = 10_000

    def __post_init__(self) -> None:
        if self.minimum_quantum is not None and (not isinstance(self.minimum_quantum,Fraction) or self.minimum_quantum<=0):raise ValueError("minimum quantum must be positive exact")
        if (self.minimum_quantum is None)!=(self.residual_sink is None):raise ValueError("quantum cutoff requires a residual sink")
        if self.max_edge_visits is not None and self.max_edge_visits<=0:raise ValueError("edge visit bound must be positive")
        if self.max_steps<=0:raise ValueError("propagation step budget must be positive")


@dataclass(frozen=True, slots=True)
class PropagationCertificate:
    graph_id: str
    cyclic: bool
    termination_witness: str
    steps: int
    edge_visits: tuple[tuple[str,int], ...]
    input_amount: Fraction
    delivered_amount: Fraction
    residual_amount: Fraction


@dataclass(frozen=True, slots=True)
class PropagationResult:
    delivered: tuple[tuple[str,Fraction], ...]
    residual: Fraction
    certificate: PropagationCertificate


def _cyclic(edges: tuple[PropagationEdge,...]) -> bool:
    adjacency:dict[str,set[str]]={}
    for edge in edges:adjacency.setdefault(edge.source,set()).add(edge.target)
    visiting=set();done=set()
    def visit(node:str)->bool:
        if node in visiting:return True
        if node in done:return False
        visiting.add(node)
        if any(visit(child) for child in adjacency.get(node,())):return True
        visiting.remove(node);done.add(node);return False
    return any(visit(node) for node in tuple(adjacency))


def validate_propagation_graph(edges:tuple[PropagationEdge,...],policy:PropagationPolicy)->tuple[bool,str]:
    if len({edge.edge_id for edge in edges})!=len(edges):raise ValueError("duplicate propagation edge")
    outgoing:dict[str,Fraction]={}
    for edge in edges:outgoing[edge.source]=outgoing.get(edge.source,Fraction())+edge.transfer_fraction
    if any(total>1 for total in outgoing.values()):raise ValueError("propagation fan-out amplifies conserved amount")
    cyclic=_cyclic(edges)
    if not cyclic:return False,"acyclic"
    if policy.max_edge_visits is not None:return True,"finite-lineage-edge-visits"
    if policy.minimum_quantum is None:raise ValueError("cyclic propagation lacks a finite termination witness")
    if any(edge.source==edge.target and edge.transfer_fraction==1 for edge in edges):raise ValueError("non-dissipative self cycle")
    if any(edge.transfer_fraction==1 for edge in edges):raise ValueError("cyclic quantum propagation requires strict per-edge dissipation")
    return True,"strict-dissipation-with-residual"


def propagate(*,initial:tuple[tuple[str,Fraction],...],edges:tuple[PropagationEdge,...],policy:PropagationPolicy)->PropagationResult:
    """执行精确、守恒、有限的传播；任何预算失败都不返回部分结果。"""
    cyclic,witness=validate_propagation_graph(edges,policy)
    if any(not isinstance(amount,Fraction) or amount<0 for _,amount in initial):raise ValueError("initial propagation amount must be nonnegative exact")
    if len(dict(initial))!=len(initial):raise ValueError("duplicate initial propagation node")
    by_source:dict[str,list[PropagationEdge]]={}
    for edge in sorted(edges,key=lambda item:item.edge_id):by_source.setdefault(edge.source,[]).append(edge)
    queue=deque((node,amount) for node,amount in sorted(initial) if amount)
    delivered:dict[str,Fraction]={};residual=Fraction();visits:dict[str,int]={};steps=0
    while queue:
        steps+=1
        if steps>policy.max_steps:raise ValueError("propagation step budget exhausted")
        node,amount=queue.popleft();outgoing=by_source.get(node,())
        if not outgoing:
            delivered[node]=delivered.get(node,Fraction())+amount;continue
        distributed=Fraction()
        for edge in outgoing:
            transfer=amount*edge.transfer_fraction;distributed+=transfer
            visits[edge.edge_id]=visits.get(edge.edge_id,0)+1
            if policy.max_edge_visits is not None and visits[edge.edge_id]>policy.max_edge_visits:
                residual+=transfer;continue
            if policy.minimum_quantum is not None and transfer<policy.minimum_quantum:
                residual+=transfer;continue
            queue.append((edge.target,transfer))
        residual+=amount-distributed
    input_amount=sum((amount for _,amount in initial),Fraction())
    delivered_amount=sum(delivered.values(),Fraction())
    if delivered_amount+residual!=input_amount:raise AssertionError("propagation conservation failure")
    graph_id=content_id((edges,policy));certificate=PropagationCertificate(graph_id,cyclic,witness,steps,tuple(sorted(visits.items())),input_amount,delivered_amount,residual)
    return PropagationResult(tuple(sorted(delivered.items())),residual,certificate)
