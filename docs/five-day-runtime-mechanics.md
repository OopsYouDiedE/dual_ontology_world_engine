# 五日原型的运行机制

正式声明入口为 `assets/data/scenarios/campaign.dv`；五日共用同一个 `RuntimeSession`，世界事实、人物认知、证据来源、资源和机制历史跨日延续。后日文件里的 `knows` 不会倒灌到第一天，后日所知须由观察、感知、交流或证明取得。

## 日常驱动与意见采纳

`DecisionScheduler` / `DriveCalculator` 负责人物日常行动：从本人认知满足门槛且现场可执行的候选中，按需求、目标、情绪和 Buff 评分。新认知会使相关候选优先进入有界候选集合；实际选择仍由原评分决定。每个选中的行动立即取得共享交互位，后续人物会看到该位已经占用；相同时间片不能重复占用一本排班簿。

`RuntimeMechanics.EvaluateSuggestion` 使用另一套人物 `opinion` 参数，独立计算建议是否被采纳。意见分数由说话者信任、实际已知证据、受建议者的价值倾向、成本、风险及其本人知晓的现场条件组成。日常行动分数不进入这个计算。

```json
{
  "opinion": {
    "base_trust": 0.2,
    "threshold": 1.55,
    "evidence_weight": 0.4,
    "cost_aversion": 0.6,
    "risk_aversion": 1.2,
    "values": { "duty": 1.4, "fairness": 1.1 },
    "trust": { "detective": 0.3 },
    "modifiers": [
      { "when": { "airlock.lock.sealed": true }, "score": 0.8, "reason": "已见安全通口成立" }
    ]
  }
}
```

建议声明置于普通行动内；`when` 属于行动者认知门槛，`requires` 属于实际现场条件。`appeal` 每维在 −1 至 1 之间，`cost` / `risk` 在 0 至 1 之间。

```json
{
  "suggestion": {
    "target": "ward",
    "evidence": ["detective.infer.dual_ration", "detective.infer.granary_cooperation"],
    "appeal": { "duty": 0.7, "fairness": 0.7 },
    "cost": 0.2,
    "risk": 0.05,
    "result": "ration.doc.ward_accepted"
  }
}
```

拒绝不会消费一次性行动，也不会提交它的成功效果。建议中的证据及其来源会传给收件人；接受或拒绝本身是双方可以观察的事实。`LastOpinion` 保留各项贡献、阈值、缺少的证据和真实结果。

## 主动脑内推演

推演声明复用 `CaseTheory` 的有符号 Horn 证明器。每张证据卡绑定人物实际记忆中的条件；未知或有冲突的事实不能成为前提，未见过某事也不能自动成为其否定。规则至少包含一个真实前提，整个推演至少声明两张不同证据卡和一项结论认知。

```json
{
  "deduction": {
    "title": "鞋印与袖口共同指向拖回动作",
    "evidence": [
      { "id": "shoe", "when": { "B_foyer.trace.shoe_vector": "向内" }, "label": "向内鞋印" },
      { "id": "sleeve", "when": { "su.trace.sleeve_vector": "向内" }, "label": "袖口拖痕" }
    ],
    "rules": [
      { "id": "combine", "all": ["shoe", "sleeve"], "then": "drag_supported", "label": "两项方向一致" }
    ],
    "conclusion": "drag_supported",
    "learns": { "B_case.infer.drag_direction_supported": true },
    "qualification": "动作方向得到支持，动机仍未知"
  }
}
```

`DeductionPreview(actor, action)` 只展示该人物已经知道的卡片内容和来源；未知卡片不泄露其命题值。玩家通过 `SubmitDeduction(actor, action, evidenceIds)` 选择实际已知卡片。AI 使用自己当前知道的证据。缺证时，证明器返回未证实，认知、世界结果及一次性完成标记均不提交，玩家可以补证再试。成功时，`LastDeduction` 记录证据 ID、观测来源、规则应用、证明树、理论哈希及结论边界；`learns` 只更新主观认知。其他人物须通过真实交流才能获得该结论。

## 言灵

言灵需要先取得指定认知，并实际消耗资源、承受反噬。它暂时改写一个已声明世界字段，既可影响物理条件，也可被 NPC 观察后影响其意见判断。

```json
{
  "incantation": {
    "formula": "闭合之门，不纳污息",
    "requires": { "detective.infer.safe_airlock": true },
    "resource": "detective.phys.resolve",
    "cost": 12,
    "backlash": { "field": "detective.phys.corruption", "amount": 3 },
    "rule": { "field": "airlock.lock.sealed", "value": true, "duration_minutes": 45 },
    "witnesses": ["huo", "quer"]
  }
}
```

资源、反噬、规则字段必须互不相同，费用、持续时间与反噬量须为有限正数。资源不足或认知缺失会失败。到期时恢复此前世界值并产生真实到期回执；中途存档与加载保留到期时间。施术者获得自己的资源和反噬反馈，见证者只获得其可见的规则变化。

## 策略和自然事件

`strategy` 行动把每个方案的本人所知条件编成实际可证明前提，再由 `StrategyActivity` 分摊计算并比较收益。D2 的探针、药卷与口粮方案，以及 D4 的锁门、盘查与值守方案都使用此机制。谋划结果保留在人物自己认知中；剧情只有通过后续真实行为或观察才能向侦探揭示。

```json
{
  "strategy": {
    "output": "ruan.plan.selected",
    "publish": true,
    "options": [
      { "id": "probe", "label": "借走探针", "when": { "probes.phys.owner": "quer" }, "utility": 17, "basis": "本人已经核对持有人" }
    ]
  }
}
```

顶层 `world_events` 声明时刻、客观条件与实际变化，适用于微震等非人物行为。跨日合并会偏移事件时刻并使 ID 唯一。

```json
{
  "world_events": [
    { "id": "microquake", "at_game_minutes": 1050, "writes": { "island_environment.phys.earthquake": true }, "tell": ["detective", "huo"] }
  ]
}
```

## 持久化和正式验收

`RuntimeSessionState` 版本 2 包含机制检查点，保存推演选证、机制回执、临时规则租期、已触发自然事件，以及已有策略活动状态。恢复时检查人物、动作、资源、运行中的执行和声明兼容性；这不是任意写入人物认知的接口。

正式 CLI 与 Godot 主宿主共用 Core。完整审计用 `MechanicHistory` 记录 `time`、`execution`、`actor`、`action`、`kind`、`success`、`details`；玩家视图必须调用 `MechanicHistoryFor(actor)`，只返回本人记录及证据来源确认该人物已经获知的世界事件。同房 NPC 的内部策略、推演和私有行动 ID 不进入玩家历史，完整磁盘审计仍保留这些内容。完整验收同时检查著作中的剧情条件、核心机制实际成功、认知边界、交互位互斥和最终完成事实。各日行动必须在该日窗口内完成；达到时间上限不等于完成。玩家接管时，仅其 AI 行动评分竞争项标为不适用，证据、证明、言灵、建议和五日结局门槛继续核验。
