from __future__ import annotations

from dataclasses import dataclass
from ..foundation.codec import content_id


@dataclass(frozen=True, slots=True)
class TraceNode:
    trace_id: str
    kind: str
    public_payload: tuple[tuple[str, str], ...]
    parent_ids: tuple[str, ...] = ()
    objective_only: bool = False

    @property
    def content_id(self) -> str:
        return content_id((self.kind,self.public_payload,self.parent_ids,self.objective_only))

    def __post_init__(self) -> None:
        if self.trace_id!=self.content_id:raise ValueError("trace node id not content-bound")
        if len(dict(self.public_payload))!=len(self.public_payload):raise ValueError("duplicate trace payload key")


@dataclass(frozen=True, slots=True)
class TraceDag:
    nodes: tuple[TraceNode, ...]

    def __post_init__(self) -> None:
        ids = {n.trace_id for n in self.nodes}
        if len(ids) != len(self.nodes): raise ValueError("duplicate trace id")
        if any(parent not in ids for node in self.nodes for parent in node.parent_ids):raise ValueError("unknown trace parent")
        positions={node.trace_id:index for index,node in enumerate(self.nodes)}
        if any(positions[parent]>=positions[node.trace_id] for node in self.nodes for parent in node.parent_ids):raise ValueError("trace parents must precede children")
        graph = {n.trace_id: n.parent_ids for n in self.nodes}; visiting: set[str] = set(); done: set[str] = set()
        def visit(key: str) -> None:
            if key in visiting: raise ValueError("trace cycle")
            if key in done: return
            visiting.add(key)
            for parent in graph.get(key, ()): 
                if parent in graph: visit(parent)
            visiting.remove(key); done.add(key)
        for key in graph: visit(key)

    def epistemic_projection(self) -> "TraceDag":
        visible = tuple(node for node in self.nodes if not node.objective_only)
        visible_ids = {n.trace_id for n in visible}
        projected=[];id_map={}
        for node in visible:
            parents=tuple(id_map[parent] for parent in node.parent_ids if parent in id_map)
            values=(node.kind,node.public_payload,parents,False);new_id=content_id(values);id_map[node.trace_id]=new_id;projected.append(TraceNode(new_id,node.kind,node.public_payload,parents,False))
        return TraceDag(tuple(projected))


@dataclass(frozen=True, slots=True)
class ProjectionGrant:
    subject_id: str
    epistemic_version: str
    visible_trace_ids: tuple[str, ...]
    payload_keys_by_kind: tuple[tuple[str, tuple[str, ...]], ...] = ()

    def __post_init__(self) -> None:
        if not self.subject_id or not self.epistemic_version:
            raise ValueError("projection grant requires subject and epistemic version")
        if len(set(self.visible_trace_ids)) != len(self.visible_trace_ids):
            raise ValueError("duplicate visible trace id")
        if tuple(sorted(dict(self.payload_keys_by_kind).items())) != self.payload_keys_by_kind:
            raise ValueError("projection payload policy must be canonical and unique")


@dataclass(frozen=True, slots=True)
class EpistemicTraceNode:
    trace_id: str
    kind: str
    public_payload: tuple[tuple[str, str], ...]
    parent_ids: tuple[str, ...]
    subject_id: str
    epistemic_version: str

    @property
    def content_id(self) -> str:
        return content_id((self.kind,self.public_payload,self.parent_ids,self.subject_id,self.epistemic_version))

    def __post_init__(self) -> None:
        if self.trace_id != self.content_id:
            raise ValueError("epistemic trace id not content-bound")


@dataclass(frozen=True, slots=True)
class EpistemicTrace:
    subject_id: str
    epistemic_version: str
    nodes: tuple[EpistemicTraceNode, ...]

    def __post_init__(self) -> None:
        ids={node.trace_id for node in self.nodes}
        if len(ids)!=len(self.nodes):raise ValueError("duplicate epistemic trace id")
        positions={node.trace_id:index for index,node in enumerate(self.nodes)}
        if any(parent not in ids or positions[parent]>=positions[node.trace_id] for node in self.nodes for parent in node.parent_ids):raise ValueError("invalid epistemic trace topology")
        if any(node.subject_id!=self.subject_id or node.epistemic_version!=self.epistemic_version for node in self.nodes):raise ValueError("mixed POV trace")


def project_trace(dag: TraceDag, grant: ProjectionGrant) -> EpistemicTrace:
    """只按单一主体的显式可见集合投影；客观节点即使误授权也不会泄漏。"""

    by_id={node.trace_id:node for node in dag.nodes}
    unknown=set(grant.visible_trace_ids)-set(by_id)
    if unknown:raise ValueError("projection grant references unknown trace")
    visible={trace_id for trace_id in grant.visible_trace_ids if not by_id[trace_id].objective_only}
    allowed_by_kind=dict(grant.payload_keys_by_kind)

    def visible_ancestors(trace_id: str) -> tuple[str, ...]:
        found=set();stack=list(by_id[trace_id].parent_ids)
        while stack:
            parent=stack.pop()
            if parent in visible:
                found.add(parent)
            else:
                stack.extend(by_id[parent].parent_ids)
        return tuple(sorted(found,key=lambda item:next(index for index,node in enumerate(dag.nodes) if node.trace_id==item)))

    id_map:dict[str,str]={};projected=[]
    for node in dag.nodes:
        if node.trace_id not in visible:continue
        keys=set(allowed_by_kind.get(node.kind,()))
        payload=tuple((key,value) for key,value in node.public_payload if key in keys)
        parents=tuple(id_map[parent] for parent in visible_ancestors(node.trace_id))
        values=(node.kind,payload,parents,grant.subject_id,grant.epistemic_version)
        new_node=EpistemicTraceNode(content_id(values),node.kind,payload,parents,grant.subject_id,grant.epistemic_version)
        id_map[node.trace_id]=new_node.trace_id;projected.append(new_node)
    return EpistemicTrace(grant.subject_id,grant.epistemic_version,tuple(projected))
