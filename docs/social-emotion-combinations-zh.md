# 有限社交情绪影响组合

## 定位

一次互动首先被解释为有限的一般评价事实，再从固定目录选择一个完整情绪组合。选择器不能输出目录以外的组合，也不能根据人物姓名、作品、章节或预期行为选择结果。

组合被实体化为指向具体目标对象的 `Influence`。它表达的是某主体因某次事件对某人产生的临时情绪，不修改稳定 `Disposition`，也不直接决定下一次行动。

## 输入边界

当前只接受以下评价输入：

- `bond`：互动前的关系联结，取 0、1、2。
- `threat`：主体感知的现实威胁，取 0、1、2。
- `irreversible_loss`：目标已经不可逆地失去。
- `corrected_self_blame`：新材料证明主体此前误伤无辜者。
- `malicious_intent`：主体把本次结果归因为明确恶意。
- `severe_harm_attributed_to_target`：主体认为目标造成严重伤害。
- `reciprocity_breach`：目标破坏互惠关系。
- `truth_conflict`：目标坚持的事实结论直接伤害现有联结。
- `costly_repair`：目标为修复伤害承担可观察的真实代价。
- `vulnerability_accepted`：主体暴露脆弱面后得到接纳。
- `shared_objective_advanced`：互动真实推进共同目标。
- `support_received`：主体得到目标的实际支持。
- `norm_breach`：目标破坏主体认可的共同规范。

出现未登记字段时直接失败。评价输入必须来自 Observation、已接受 Knowledge、Event 结果或关系状态，不能直接填写情绪名称。

## 固定输出目录

```text
NO_STRONG_CHANGE
  无新增 Influence

AFFILIATION_GAIN
  Joy 2/2，Hope 1/2

SAFE_ACCEPTANCE
  Serenity 2/2，Hope 1/2，Joy 1/1

HURT_WITHDRAWAL
  Sorrow 2/2，Anger 1/1

DEFENSIVE_ANGER
  Anger 2/1，Fear 1/1

MORAL_REJECTION
  Anger 2/2，Hatred 1/2，Sorrow 1/2

GRIEF_BOND
  Sorrow 3/3，Devotion 2/3

REMORSE_REPAIR
  Remorse 3/2，Sorrow 1/2，Hope 1/1

CAUTIOUS_REOPENING
  Hope 1/2，Serenity 1/1，Sorrow 1/2

SHARED_RESOLVE
  Hope 2/2，Devotion 1/2，Serenity 1/1

THREATENED_DISTRUST
  Fear 2/2，Anger 1/2，Hatred 1/2
```

每个数字对表示 `intensity/persistence`。组合内的情绪和数值必须整体采用，不能临时增删。

## 选择顺序

当一次互动同时满足多个评价时，按以下顺序选择：

```text
纠正自身误伤
→ 不可逆失去且存在联结
→ 明确恶意且威胁达到 2
→ 高代价修复
→ 将严重伤害归因于目标
→ 互惠破坏或真相冲突且存在联结
→ 脆弱面得到接纳
→ 共同目标获得推进
→ 得到实际支持
→ 一般规范破坏
→ 无显著变化
```

顺序解决同一结果中的竞争解释。例如西园寺仍认为九头龙造成严重伤害，但观察到高代价修复后，新增情绪选择 `CAUTIOUS_REOPENING`；旧有愤怒和未解决事件不会自动删除。

## 实现位置

- `core/python/social.py`：正式核心的固定组合、评价选择器、SocialImpact 工厂和态度压力。
- `python_cli/academy_game.py`：学园内容层把已结算互动解释为评价输入，并把影响接入 NPC 决策。

## 学园游戏接入结果

关系节点不再预先返回唯一动作。同场人物会根据实际关系获得分享记忆、澄清、
质询或保护中的可执行候选，异地只保留能够通过定向广播完成的分享。每个候选
携带对应 `ResponseMode`，情绪组合只参与排序。
