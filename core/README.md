# Core 多版本结构

- `python/entity_system.py`：唯一 Entity、Facet、Passage、知识、关系与 Aspect 投影。
- `python/soul.py`：完整 Intent、Soul Step、认知规则、客观 RulePatch 与结构化 Event。
- `python/kotodama.py`：Entity 配方、言灵签名、Rite、Covenant、代价和效果处理器。
- `python/engine.py`：空间拓扑、行动值 Timeline 与嵌套行动协调器。
- `godot-csharp/`：Godot C# 实现，供 `Game` 调用。
- `typescript/`：TypeScript 迁移预留目录，当前不实现。

Soul 的 Step 使用 Facet 条件、优先度和来源组织决策分支，叶子必须产生完整 Intent。普通规则只评价 Soul 已具备的候选，不创造动作，也不直接阻止行动。循环主体在行动槽到期时决策；反应仍是 Intent，但在当前主行动内部结算。

言灵、仪式和契约不建立平行对象类族。Recipe、Rite 与 Covenant 都是普通 Entity。签名只读取明确投入对象，跨投入按实际贡献来源去重；目标联系必须来自真实 Facet，代价必须改变实际 Entity 并留下 Paid/PaidFor 关系。未知 Effect 拒绝执行。

游戏层保留具体内容、数值平衡、场景内几何、碰撞、胜负和用户界面。
