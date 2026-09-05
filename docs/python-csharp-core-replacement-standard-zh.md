# Python / C# Core 替换规范

## 1. 目的与替换原则

本规范定义 Python Core 与 Godot C# Core 的语义契约。替换目标为在相同游戏层输入下产生完全一致的世界状态差分、行动顺序与因果记录，不要求源码结构、命名风格或文件划分一致。

验收以跨 Core 规范化轨迹为准，编译通过或类名相似不能证明可替换性。

当前实现主线为 C#，其新增的 SimulationSession、对象 Affordance 与前置拦截接口尚未同步到 Python。本文是跨语言验收目标，不代表已经达到替换条件。C# 外部接入以[模拟架构规范](./simulation-architecture-zh.md)为准。

## 2. 共享 Core 职责边界

### 属于共享 Core
- 世界树层次、场景拓扑与 Passage 穿越代价。
- Entity、Facet、Tag、Property、Aspect 投影与实体间直接对象引用。
- Soul、Intent、主观规则评估（Decision）与客观 RulePatch。
- Timeline 单时间线调度、嵌套反应子回合与 Event 因果链。
- Kotodama 配方、Rite 仪式与 Covenant 契约约束。

### 不属于共享 Core（交由游戏层或外部实现）
- 房间内几何坐标、碰撞交互与实时 Tick。
- 渲染表现与用户界面。
- 固定人类行为枚举、特定情感日结公式与业务专属背包结构。

## 3. 权威状态源与映射规则

### 唯一状态源
- Tag 权威状态为 canonical 字符串到整数强度的映射。C# enum 仅可作为前端语法糖并立即投影到字符串 Tag，禁止维护独立状态。
- Claim、Reveal、Designation、关系与来源统一由 Facet 承载。
- 实体关系直接保存目标运行时对象引用，禁止额外建立仅用于寻址的 ID。

### 引用与相等语义
- Entity、Passage、Soul、Character 及实体引用比较严格使用对象引用相等（Python `is`，C# `ReferenceEquals`）。
- Expr、Effect、Rule 与不可变值对象使用递归结构值相等。
- Tag 与 Facet key 保持开放字符串，区分大小写。

## 4. 统一调度与执行管线

### Timeline 排序契约
Timeline 唯一排序键为 `(绝对行动值升序, 登记序号升序)`。相同行动值严格按登记顺序串行结算，不使用额外优先级权重。

### ActionRuntime 结算管线
1. 验证 actor 与 mind 绑定。
2. 收集候选并计算主观规则 Decision（仅供审计与偏好排序）。
3. 执行 Intent.validate、RulePatch 与 Covenant 硬约束初次复核。
4. 递归结算现场反应 Intent（嵌套子回合）。
5. 基于反应后最新状态再次执行硬约束复核。
6. 执行主 Intent.resolve 并创建 Event。
7. 执行 Covenant 监听与见证传播。
8. 结算已经发生事件的见证反应，完成游戏监听器通知。
9. 循环行动主体按本次 motion_cost 与反应成本累计的恢复时间重新登记下一行动槽。

## 5. 跨 Core 轨迹验收

两端运行相同契约场景，对比以下规范化轨迹：
```text
(行动值, 登记序号)
→ RuleSystem Decision
→ Intent / Effect 结算
→ Entity tag/property/facet/parent 差分
→ Event (time, cause, phase) 与因果记录
→ Covenant 与 Knowledge 变化
```

不比较内存地址、语言类型名或文本日志。代码遵循语言原生引用特性，不为形式一致引入样板层或多余验证。
