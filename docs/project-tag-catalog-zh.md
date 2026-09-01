# 项目 Tag 与 Aspect 词表规范

## 1. 规则与原则

- **唯一性**：运行时仅保存英文词（如 `Form`, `Consumable`）。分组标题不作为 Tag。
- **数值规则**：
  - `Aspect` 组：支持同名叠加求和。
  - `Emotion` 组：表示主体当前具体情绪强度。
  - 其余 Tag：默认强度为 `1`，值为 `0` 等同于无此 Tag。
- **边界约束**：目标对象引用、父子关系、空间拓扑、时间戳、布尔真假及认知状态由 `properties` 和 `facets` 承载，严禁编码为字符串 Tag。

---

## 2. Aspect｜十二原理

- `Form`（形质）：物质、身体、空间结构与物理载体强度。
- `Name`（名契）：名称、身份、称号、归属与社会承认强度。
- `Law`（律令）：规则、义务、权限、制度与稳定约束强度。
- `Mind`（心识）：感知、记忆、理解、信念与认知防御强度。
- `Bond`（缘系）：血缘、情感、契约、见证与因果绑定强度。
- `Threshold`（门隙）：边界、入口、层级跨越与作用范围强度。
- `Transmutation`（蜕变）：转化、重组、替代与相变强度。
- `Preservation`（存续）：生命、保护、恢复与身份连续性强度。
- `Death`（终寂）：终止、切断、停止与不可逆结束强度。
- `Desire`（欲求）：愿望、执念与驱动倾向强度。
- `Strife`（争衡）：对抗、伤害、抵抗与强制改变强度。
- `Vestige`（遗痕）：历史、证据、记忆残留与因果追索强度。

---

## 3. Emotion｜十二情绪

- `Love`（爱）：守护、接近与维系目标的倾向。
- `Hatred`（仇恨）：排斥、伤害与否定目标的倾向。
- `Fear`（恐惧）：规避威胁与防御倾向。
- `Anger`（愤怒）：对抗侵犯与障碍的即时倾向。
- `Joy`（喜悦）：满足感与积极预期。
- `Sorrow`（悲伤）：面对失去与终结的心理状态。
- `Hope`（希望）：对有利未来的稳定预期。
- `Despair`（绝望）：对未来可能性的丧失感。
- `Remorse`（悔恨）：对过去行为的补偿与修正倾向。
- `Devotion`（虔诚）：对信念或共同体的稳定献身。
- `Obsession`（执念）：特定目标的持续最高优先级。
- `Serenity`（宁静）：情绪受控与认知稳定状态。

---

## 4. 基础类别与用途

- **Kind（对象类别）**：`Knowledge`（知识）、`Influence`（动态影响）、`Tool`（工具）、`Reagent`（反应材料）、`Agent`（行动主体/从属）、`Record`（信息记录）。
- **Use（用途标记）**：`Consumable`（成功时消耗）、`Catalyst`（催化保留）、`Substitute`（配方允许替代）。
- **State（状态标记）**：`Temporary`（临时状态）、`Concealed`（隐藏性质）、`Tainted`（受污染层数）。

---

## 5. 仪式与言灵结构

- **Rite（仪式结构）**：`Sanctum`（场所）、`Altar`（祭坛中心）、`Circle`（隔离边界）、`Correspondence`（语义地址）、`Invocation`（调用知识）、`Petition`（诉求）、`Offering`（祭品）、`Instrument`（引导仪器）、`Assistant`（协助者）、`Witness`（见证人）、`Attunement`（授权证明）、`Purification`（净化步骤）、`Order`（顺序约束）、`Conjunction`（时机窗口）、`Seal`（收束封印）。
- **Word（言灵结构）**：`Incantation`（发声即行动）、`Declaration`（制度状态宣告）、`Command`（命令）、`Promise`（自我约束承诺）、`Covenant`（多方契约）、`Oath`（身份担保誓言）、`Geas`（禁忌约束）、`Blessing`（祝福）、`Curse`（诅咒）、`Prayer`（祈求请求）、`TrueName`（真名寻址）、`DyingWord`（亡语支付）、`Contingency`（延迟触发条件）、`Ambiguity`（歧义度）。

---

## 6. 规则、危险与异常

- **Rule（规则标记）**：`Information`（信息描述）、`Mandate`（必须）、`Interdiction`（禁止）、`Permission`（许可）、`Preference`（偏好）、`Institutional`（制度权限）、`Supernatural`（超自然强制）、`Truth`（世界事实）、`Claim`（主张记录）、`Belief`（主体信念）、`Sanctioned`（合法授权）、`Falsehood`（虚假记录）、`Conflict`（冲突）、`Exception`（例外）、`Sequence`（顺序执行）、`Deadline`（期限）、`Domain`（作用域）、`Punishment`（违规惩罚）、`Escape`（脱离途径）、`Loophole`（规则漏洞）。
- **Hazard（信息危险）**：`Cognitohazard`（感知即触发）、`Infohazard`（获知即触发）、`Memetic`（模因传播）、`Hermeneutic`（理解即触发）、`IdentityHazard`（身份侵蚀）、`MemoryHazard`（记忆篡改）。
- **Anomaly（异常语义）**：`Replacement`（替代）、`Oblivion`（遗忘）、`Recurrence`（循环）、`Mimicry`（模仿）、`Consumption`（吞噬）、`Contagion`（传染）。

---

## 7. 绑定与代价

- **Binding（目标绑定）**：`Naming`（名称寻址）、`Sympathy`（身体连续/接触）、`Bloodline`（血缘渊源）、`Affinity`（情感联系）、`Karma`（未结因果）、`Locus`（空间锚点）。
- **Cost（代价分类）**：`Lifespan`（寿命）、`Time`（时间）、`Memory`（记忆）、`Identity`（身份）、`Relationship`（关系）、`Authority`（权限）、`Possibility`（未来可能性）、`Material`（物质损耗）、`Exposure`（信息暴露）。
