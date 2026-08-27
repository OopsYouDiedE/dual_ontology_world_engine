from __future__ import annotations

from dataclasses import dataclass
from enum import Enum


class Polarity(Enum):
    POSITIVE = 1
    NEGATIVE = -1


@dataclass(frozen=True, slots=True)
class Dependency:
    """``target_rule`` 对 ``source_rule`` 的求值依赖。"""

    source_rule: str
    target_rule: str
    polarity: Polarity


@dataclass(frozen=True, slots=True)
class DependencyAnalysis:
    """规则依赖图的确定性 SCC 与拓扑层。"""

    components: tuple[tuple[str, ...], ...]
    strata: tuple[tuple[tuple[str, ...], ...], ...]


def _strongly_connected_components(
    nodes: tuple[str, ...], edges: tuple[Dependency, ...]
) -> tuple[tuple[str, ...], ...]:
    """Tarjan SCC；排序只用于获得与输入容器顺序无关的规范结果。"""

    children = {node: [] for node in nodes}
    for edge in edges:
        children[edge.source_rule].append(edge.target_rule)
    for values in children.values():
        values.sort()

    next_index = 0
    indices: dict[str, int] = {}
    lowlinks: dict[str, int] = {}
    stack: list[str] = []
    on_stack: set[str] = set()
    components: list[tuple[str, ...]] = []

    def visit(node: str) -> None:
        nonlocal next_index
        indices[node] = lowlinks[node] = next_index
        next_index += 1
        stack.append(node)
        on_stack.add(node)

        for child in children[node]:
            if child not in indices:
                visit(child)
                lowlinks[node] = min(lowlinks[node], lowlinks[child])
            elif child in on_stack:
                lowlinks[node] = min(lowlinks[node], indices[child])

        if lowlinks[node] != indices[node]:
            return
        component: list[str] = []
        while True:
            member = stack.pop()
            on_stack.remove(member)
            component.append(member)
            if member == node:
                break
        components.append(tuple(sorted(component)))

    for node in sorted(nodes):
        if node not in indices:
            visit(node)
    return tuple(sorted(components))


def analyze_dependencies(
    nodes: tuple[str, ...],
    edges: tuple[Dependency, ...],
    monotone_rules: frozenset[str],
) -> DependencyAnalysis:
    """验证规则分层并返回规范化组件 DAG。

    正向循环只有在 SCC 中所有规则均声明为单调时才合法；任何 SCC 内负边都
    会在装载期失败。跨层负边是合法的，因为它只读取已经稳定的较低层。
    """

    if len(set(nodes)) != len(nodes):
        raise ValueError("duplicate rule id")
    known = set(nodes)
    if not monotone_rules.issubset(known):
        raise ValueError("unknown monotone rule")
    if any(
        edge.source_rule not in known or edge.target_rule not in known
        for edge in edges
    ):
        raise ValueError("unknown dependency endpoint")
    if any(not isinstance(edge.polarity, Polarity) for edge in edges):
        raise TypeError("dependency polarity must be closed")

    components = _strongly_connected_components(nodes, edges)
    component_of = {
        member: index
        for index, component in enumerate(components)
        for member in component
    }
    cyclic = {
        index
        for index, component in enumerate(components)
        if len(component) > 1
    }
    cyclic.update(
        component_of[edge.source_rule]
        for edge in edges
        if edge.source_rule == edge.target_rule
    )

    for edge in edges:
        source_component = component_of[edge.source_rule]
        target_component = component_of[edge.target_rule]
        if source_component != target_component:
            continue
        if edge.polarity is Polarity.NEGATIVE:
            raise ValueError("negative dependency inside rule SCC")
    for index in cyclic:
        if not set(components[index]).issubset(monotone_rules):
            raise ValueError("cyclic rule SCC lacks monotonicity proof")

    children: dict[int, set[int]] = {index: set() for index in range(len(components))}
    indegree = {index: 0 for index in range(len(components))}
    for edge in edges:
        source = component_of[edge.source_rule]
        target = component_of[edge.target_rule]
        if source == target or target in children[source]:
            continue
        children[source].add(target)
        indegree[target] += 1

    ready = sorted(
        (index for index, degree in indegree.items() if degree == 0),
        key=lambda index: components[index],
    )
    strata: list[tuple[tuple[str, ...], ...]] = []
    visited = 0
    while ready:
        layer = tuple(components[index] for index in ready)
        strata.append(layer)
        visited += len(ready)
        next_ready: set[int] = set()
        for index in ready:
            for child in children[index]:
                indegree[child] -= 1
                if indegree[child] == 0:
                    next_ready.add(child)
        ready = sorted(next_ready, key=lambda index: components[index])
    if visited != len(components):
        raise AssertionError("SCC condensation graph must be acyclic")

    return DependencyAnalysis(components, tuple(strata))


def validate_stratification(
    nodes: tuple[str, ...],
    edges: tuple[Dependency, ...],
    monotone_rules: frozenset[str],
) -> None:
    analyze_dependencies(nodes, edges, monotone_rules)
