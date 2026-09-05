# 回合调度、规则与言灵契约模型

## 1. 嵌套行动值回合制

引擎采用单时间线（Timeline）调度模型，严格按 `(绝对行动值升序, 登记序号升序)` 推进：
1. 从队列取出当前最小行动值主体或计划事件。
2. 触发 `Soul.step` 生成候选 Intent。
3. 执行前合法性与客观硬约束复核。
4. 通过 `BeforeReactions` 提交主效果之前的拦截 Intent（反应子回合，递归结算）。
5. 基于最新状态再次复核，结算主行动 Effect 并生成 Event。
6. 触发活动 Covenant 监听器与 RulePatch 效果。
7. 见证主体记录 Observation 与 Knowledge。
8. 通过 `ReactionHandler` 结算已发生事件的见证反应，再通知游戏监听器。
9. `SimulationSession` 按行动与嵌套反应累计的恢复时间登记下一槽；无候选按空闲代价重试。

C# 当前接入方式以[模拟架构与外部接入规范](./simulation-architecture-zh.md)为准。需求演化使用 Timeline 的实际 AV 增量；禁止另外建立角色时钟或按行动次数重复扣除时间消耗。

## 2. 统一意图模型（Intent）

玩家、NPC 与设施均使用统一 `Intent`：
- `actor`：发起主体。
- `mind`：主导该行动的 Soul。
- `effect`：动作 AST；`effect.Op` 是操作，参数保存目标与投入对象引用。C# 的 `Target/Tool` 是从参数派生的便捷访问。
- `motion_cost`：消耗的行动值。
- `response_mode`：七类应对方向（`verify`, `negotiate`, `cooperate`, `restrain`, `protect`, `avoid`, `conceal`）或开放扩展。
- `validate()` / `resolve()`：无副作用校验与主行动生效闭包。

## 3. 目标化社交情绪影响

互动 Event 产生后，依据评价指标（联结、威胁、不可逆损失、自责纠正、恶意意图、互惠破坏等）从固定目录选择完整情绪组合挂载为 `SocialImpact`，对目标候选产生临时偏置：

### 固定组合目录
- `NO_STRONG_CHANGE`：无新增
- `AFFILIATION_GAIN`：Joy 2/2, Hope 1/2
- `SAFE_ACCEPTANCE`：Serenity 2/2, Hope 1/2, Joy 1/1
- `HURT_WITHDRAWAL`：Sorrow 2/2, Anger 1/1
- `DEFENSIVE_ANGER`：Anger 2/1, Fear 1/1
- `MORAL_REJECTION`：Anger 2/2, Hatred 1/2, Sorrow 1/2
- `GRIEF_BOND`：Sorrow 3/3, Devotion 2/3
- `REMORSE_REPAIR`：Remorse 3/2, Sorrow 1/2, Hope 1/1
- `CAUTIOUS_REOPENING`：Hope 1/2, Serenity 1/1, Sorrow 1/2
- `SHARED_RESOLVE`：Hope 2/2, Devotion 1/2, Serenity 1/1
- `THREATENED_DISTRUST`：Fear 2/2, Anger 1/2, Hatred 1/2

### 选择优先级
纠正自身误伤 → 不可逆失去且存在联结 → 明确恶意且高威胁 → 高代价修复 → 归因严重伤害 → 互惠破坏或真相冲突 → 脆弱面接纳 → 推进共同目标 → 得到实际支持 → 一般规范破坏 → 无显著变化。

## 4. 规则体系与硬约束

- **`RulePublication`**：客观规则发布物（信息载体）。
- **`Belief`**：主体理解并接受的主观规则，仅通过 `RuleSystem.evaluate()` 提供决策规范压力，不直接阻止行动。
- **`RulePatch`**：全局或局部客观规则修改，参与 `Intent.validate()` 执行客观硬拦截。

## 5. 言灵、持续仪式与契约

- **言灵结算**：仅读取明确投入对象，按来源去重。未知 Effect 拒绝执行。
- **持续仪式（Rite）**：多步骤或延迟言灵实例化为 Rite 实体，记录槽位、已付代价与失效窗口。
- **契约系统（Covenant）**：
  - **硬约束（HardConstraint）**：行动前直接拒绝违规候选。
  - **监听器（ObligationListener）**：在签约方行动后触发履约或违约惩罚 Effect。
  - **代价追索**：实体间建立明确 `Paid` / `PaidFor` 关系，严禁虚构与套利。
