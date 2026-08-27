# 物理—语义二重本体论世界模拟引擎

本仓库以 `docs/engine-axioms-zh.md` 为规范来源。当前版本完成首轮内核迁移，提供有限、可审计、可回滚的世界状态变更机制。

## 包结构

代码分为稳定核心与可选领域支持库：

| 包 | 职责 | 依赖规则 |
|---|---|---|
| `dual_ontology_engine.core` | 类型、表达式、资源、审计、世界状态、原子事务 | 不得导入 `supports` |
| `dual_ontology_engine.supports` | 规则、言灵、感知、异常机制、炼金、名称与生命周期 | 只通过 `core` 使用内核能力 |

新代码应从规范位置导入：

```python
from dual_ontology_engine.core import WorldState, AtomicCommitter
from dual_ontology_engine.supports.lingual import LingualEngine
```

旧的模块路径（例如 `dual_ontology_engine.world` 和
`dual_ontology_engine.lingual`）暂时作为兼容入口保留，并重新导出同一个类型对象。
支持库通过核心提供的显式扩展注册点安装领域 Effect 和规范投影；单独导入
`dual_ontology_engine.core` 不会加载支持库。

## 已迁移能力

- 物理、语义、认知三类本体及六个运行时细分层。
- 三值客观求值与允许矛盾信息并存的认知支持。
- 有类型资源批次、支付许可、部分消费、永久性与防重复支付。
- 带证明义务的原子事务、版本串行化、失败回滚与哈希审计链。
- 规则送达、理解、采纳、服从、冲突、优先级和修订生命周期。
- 言灵路径、目标绑定前提、代价、有限扭曲、持续结算与失败审计。
- 冻结后的机制和场景公理注册表，用于异常、炼金术和自定义机制。
- 分阶段感知、理智变化、精神攻击限制、身份锚点与身份替换。

## 当前边界

首轮版本尚未实现完整的许愿解释器、通用跨层桥接图、基于范围和持续时间的自动代价估价器、生命连续性证明器。空间拓扑、人物决策、文学轨迹预测、具体游戏与 Godot 集成未迁移。

公理规范见 `docs/engine-axioms-zh.md`。

## 验证

```powershell
python -m pytest
```
