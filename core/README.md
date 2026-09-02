# Core 多版本结构

- `python/entity_system.py`：唯一 Entity、Facet、Passage、知识采信、关系与 Aspect 投影。
- `python/soul.py`：完整 Intent、开放 ResponseMode、Disposition 人格偏好、Soul Step、认知规则、客观 RulePatch 与结构化 Event。
- `python/social.py`：有限社交评价、目标化 SocialImpact 组合与七反应态度压力。
- `python/kotodama.py`：Entity 配方、言灵签名、Rite、Covenant、代价和效果处理器。
- `python/engine.py`：空间拓扑、行动值 Timeline、条件 EventNode 与嵌套行动协调器。
Soul 的 Step 使用 Facet 条件、优先度和来源组织决策分支，叶子必须产生完整 Intent。普通规则只评价 Soul 已具备的候选，不创造动作，也不直接阻止行动。循环主体在行动槽到期时决策；反应仍是 Intent，但在当前主行动内部结算。

稳定人格由 Soul 下的普通 `Disposition` Entity 与有来源的 `Preference` Facet 表达。人格只调整已有 Intent 的排序，贡献保存在 Decision 中；短期恐惧、疲劳等仍由 `Influence` 表达。Intent 可使用七种标准 `ResponseMode`，也允许自定义字符串或不分类。

普通 Claim 只有进入 Accepted 立场后才会投影到 Soul 的认知规则环境。拒绝或暂存不会删除 Knowledge 与来源。已发生事实继续使用 Event；尚可能发生的条件机会使用 EventNode，节点只能提供 Intent，不能直接修改世界。

言灵、仪式和契约不建立平行对象类族。Recipe、Rite 与 Covenant 都是普通 Entity。签名只读取明确投入对象，跨投入按实际贡献来源去重；目标联系必须来自真实 Facet，代价必须改变实际 Entity 并留下 Paid/PaidFor 关系。未知 Effect 拒绝执行。

游戏层保留具体人格词汇、关系刻度、反应偏好函数、事件条件、数值平衡、场景内几何、碰撞、胜负和用户界面。
