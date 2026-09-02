# 回合、意图、规则与言灵模型

## 1. 嵌套行动值回合制

引擎采用基于行动值（Action Value / Motion Cost）的单时间线调度模型（`Timeline`）。

```text
1. 从 Timeline 取出最小行动值的到期主体或计划事件
2. 触发 Soul.step 生成完整 Intent
3. 执行前复核（位置、权限、资源、言灵硬约束）
4. 允许现场主体/设施在主行动内部提交反应 Intent（反应子回合）
5. 依次结算 Effect，记录代价扣除与事实差分
6. 触发活动 Covenant 监听器与 RulePatch 后续效果
7. 推进异常相位，按本次 motion_cost 重新登记主体的下次行动槽
```

### 核心特性
- **确定性推进**：时间推进以到期事件为步长，相同行动值按进入队列的顺序串行结算。
- **单行动完整性**：一个主回合完整结算单一行动及其诱发的嵌套反应子回合，避免中途并发交错。
- **状态重新核验**：行动在提交与实际到期间存在时间差，到期执行前必须基于当前最新世界状态重新核验合法性。

---

## 2. 统一意图模型（Intent）

玩家、NPC、异常实体及自动反应机制均使用统一的 `Intent` 数据结构：
- **`actor`**：发起行动的实体或主体引用。
- **`action_type`**：动作类型（移动、调查、使用、施展言灵、提案等）。
- **`target` / `inputs`**：目标与投入对象的直接引用。
- **`motion_cost`**：本次行动消耗的行动值。
- **`validate()`**：客观合法性校验，不满足时直接拒绝执行。
- **`response_mode`**：可选的主要应对方向。核心提供 `verify`、`negotiate`、`cooperate`、`restrain`、`protect`、`avoid`、`conceal` 七个标准值，也允许自定义字符串或 `None`。它不替代 Effect，不产生能力，不自带分数。

### 2.1 人格偏好

Soul 可以持有普通 `Disposition` Entity，并以它为来源安装条件 Step 与 Preference：

```text
处境与主体已采信的认知
  → Step 生成完整 Intent
  → 客观条件删除不可执行候选
  → 已接受规则产生规范压力
  → Preference 产生有来源的人格偏好
  → 游戏层当前效用
  → Decision 排序并保留各项依据
```

Preference 只评价已有候选。具体敏感点、保护对象、表达习惯和权重由游戏层提供。稳定人格与短期 Influence 分开保存。

### 2.2 目标化社交情绪

已结算的互动 Event 可以由内容层解释为一组有限评价事实，再由
`select_social_influence()` 从固定目录选择一个完整组合。`SocialImpact`
挂在接收者 Soul 下，直接引用目标人物和来源 Event；组合内每项情绪继续使用
普通 `Influence` Entity 表达。

每个 SocialImpact 可以作为 Preference 来源，对同一目标的 `ResponseMode`
候选提供临时偏置。它不能创造 Intent，不能清除 Knowledge 中的伤害记录，也
不能绕过 RulePatch。不同事件的影响可以并存；同目标、同组合的重复刺激只刷新
持续期并记录 `ReinforcedBy`，不重复叠加取得无限分数。

```text
互动结算
→ 一般评价事实
→ 固定 SocialImpact 组合
→ 对具体目标的 ResponseMode 临时偏置
→ 与规则、稳定人格和任务效用共同评价 Intent
```

组合目录与输入边界见
[`social-emotion-combinations-zh.md`](social-emotion-combinations-zh.md)。

### 2.3 条件事件机会

`Event` 表示已经发生的审计事实；`EventNode` 表示尚可能发生的条件机会。节点具有触发、持续、失效、完成和候选提供回调，但不能直接执行效果。

`EventDirector` 只负责：

- 把条件成立的节点标记为 `offered`；
- 在条件永久破坏后记录 `cancelled`，不自动复活；
- 从节点收集完整 Intent，交回 Soul 正常评价；
- 根据实际完成 Event 开放直接引用的后继节点。

节点检查不消耗行动值。移动、交谈、调查等实际行为继续使用 Intent 的 `motion_cost`。`ActionRuntime` 的通用事后监听器在事件完成见证传播后接收 Event，可由 EventDirector、任务或环境连锁复用。

---

## 3. 规则体系与认知阶段

世界中的规则根据本体层级与载体分为三类：
- **`RulePublication`（规则发布物）**：信息载体，记录文本、发行者与声明。可能真实、错误或过时。
- **`Belief`（主观信念）**：Soul 经过 `receive -> understand -> accept` 纳入自身认知模型的规则。
- **`RulePatch`（客观规则补丁）**：由言灵、授权机制或异常客观激活的全局/局部规则修改。

### 规则评估机制
- 普通规则仅通过 `RuleSystem.evaluate()` 对候选 Intent 进行评价，提供规范压力分（`Decision`），由 `Soul.step()` 结合偏好选出最终意图。
- 普通规则**不直接阻止行动，也不直接修改现实**。
- 客观阻止行动由 `Intent.validate()`、活动的 `RulePatch` 或 `Covenant` 的硬约束（Hard Constraint）执行。

---

## 4. 言灵、仪式与契约（Kotodama）

言灵是受目标绑定、作用域、权限与真实代价约束的语义行动。

### 4.1 言灵签名与结算
- 言灵签名仅读取明确投入的对象，按实际贡献来源去重。
- 简单言灵在验证通过后直接执行其预设 `Effect` 序列。
- 未知或未定义的 `Effect` 坚决拒绝执行。

### 4.2 持续仪式（Rite）
- 多步骤、多人或需延迟释放的高阶言灵实例化为 `Rite` Entity。
- 记录投入槽位、已完成步骤、已支付代价、语义地址与失效时间窗口。
- 准备阶段已支付的代价不予退还；最终吟唱仅负责满足触发条件。

### 4.3 契约系统（Covenant）
- 契约是绑定未来因果与多方义务的持续 Entity。
- **硬约束（Hard Constraint）**：直接在行动前阻止不符合契约的操作。
- **持续监听器（ObligationListener）**：监听签约方行为，当发生违约操作时在同一行动后触发违约惩罚 Effect。
- **代价追踪**：所有投入与支付必须在实体间留下明确的 `Paid` / `PaidFor` 关系，严禁自引用套利。
