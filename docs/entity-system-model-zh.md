# Entity System 统一模型与基础数据规范

## 1. 核心架构与设计原则

根据 Python 动态特性与对象引用原则，系统避免建立平行的实体类族。箱子、门、手稿、证件、药剂、知识、情感与组织均由唯一的 `Entity` 结合轻量 `Facet` 和工厂函数生成。

核心仅维护三种基础结构：
- **`Entity`**：唯一的运行时世界对象。
- **`Facet`**：依附于对象的轻量语义项，用于关系、声明、条件名称、揭示规则与 Aspect 贡献。
- **`Passage`**：实现世界树接口的双端空间连接结构（非 Entity）。

```text
Entity
├── name: str               # 无上下文时的后备名称
├── tags: dict[str, int]    # 类别、功能标记、无条件固有 Aspect
├── properties: dict        # 客观具体性质与直接对象引用（材料、作者、目标等）
└── facets: list[Facet]     # 带条件、目标、强度或来源的动态语义
```

---

## 2. 基础属性与资源模型

### 2.1 六项基础属性
基础属性描述主体的通用能力倾向，依本体分层严格归属：
- **身体实体（Character）**：
  - `Vigor`（体魄）：施力、耐受与身体恢复。
  - `Finesse`（巧技）：协调、精确与身体反应。
  - `Perception`（感知）：环境观察与外显异常辨识。
- **意识实体（Soul）**：
  - `Reason`（理性）：分析、推演与规则解析。
  - `Stability`（自持）：维持自我认同、抵抗认知冲突与精神侵蚀。
  - `Presence`（显著）：表达身份、影响他者与维持语义显著性。

身体与 Soul 换体或多重绑定时，各自独立持有属性，不进行数据复制。

### 2.2 资源模型
- **同质资源池**：生命、耐力、灵性等可度量数值，保存在 `Entity.resources` 中。
- **具象资源**：书籍、药剂、材料、信物等具有独立身份和关系的对象，直接使用 `Entity`。
- **支付原则**：资源支付必须记录来源对象并核销实际额度，严禁同一对象在同一因果链中重复支付。

---

## 3. 知识、观察与信念

客观世界事实、主体观察结果与主观信念严格解耦：

```text
客观 properties / facets / tags
  → observe: 结合观察者状态与条件生成有限 Claim
  → 存入观察者子树中的 Knowledge Entity
  → Soul 经过 receive -> understand -> accept/reject 形成信念
  → 影响估计、命名与决策
```

- **`Reveal` Facet**：定义对象的可见性条件。未满足条件时，观察者仅获得公开名称，无法穿透读取内部性质。
- **`Claim` Facet**：记录知识载体声称的命题。错误知识可以被主体理解并相信，但不会写入客观世界事实（`RuleEnvironment.facts`）。

---

## 4. Aspect 投影与认知估计

Aspect 是单次行动能从对象提取的局部语义质量，通过无副作用投影实时计算：

1. **客观投影（`entity.aspects(target, context)`）**：
   - 固有十二项 Aspect Tag。
   - 材料 Entity 的 Aspect（按完整度 `integrity` 缩放）。
   - `PROPERTY_ASPECTS` 映射的具体物理/语义性质（硬度、锋利度、致死性、权威、历史等）。
   - 挂载的 `Facet("Aspect", ...)`。
   - 目标匹配的定向关系（如 `Depicts` 产生 `Bond` 与 `Vestige`）。
2. **认知估计（`entity.estimated_aspects(observer, target, context)`）**：
   - 仅汇总观察者已知来源的估计值，用于 UI 呈现与 AI 决策，不改变客观结算强度。

---

## 5. 动态影响（Influence）与情感模型

情感与状态统一作为挂载在 Soul 或 Entity 下的 `Influence` 子实体：

```text
Influence Entity
├── parent: Soul / Entity          # 承受者
├── tags: {"Influence": 1, ...}    # 情感种类（如 Fear, Love）与标记
├── properties:                    # 强度、持续时间、衰减率（0 为长期）
└── facets:                        # Target、Source、Anchor、天然 Aspect 贡献
```

- **情感种类**：`Love`, `Hatred`, `Fear`, `Anger`, `Joy`, `Sorrow`, `Hope`, `Despair`, `Remorse`, `Devotion`, `Obsession`, `Serenity`。
- **规则作用**：情感贡献天然 Aspect，影响 Soul 决策权重与自我稳定计算，但不能作为凭空创生的超自然能量。
- **状态统一**：伤势、污染、专注与祝福共用 `Influence` 生命周期，无需单独建立数据类。

---

## 6. 条件命名与关系网络

- **条件命名（Designation）**：通过 `Designation` Facet 在不同方向、认知或状态下呈现不同名称，不修改实体核心身份。
- **关系（Relation）**：通过包含 `source`、`target`、`kind`、`strength` 的 Facet 表达信任、敌意、债务、从属与见证。

---

## 7. 配方系统（Recipe）

配方是跨领域（制作、研究、仪式、交涉）的通用输入结算机制：
- **槽位检查**：验证投入对象的 Kind、Rite 标记、关系与权限。
- **局部 Aspect 汇总**：仅计算合法投入对象的 Aspect 总和，检查阈值。
- **代价与交付**：明确指定投入对象的保留、消耗、损坏、转化或污染策略。
- **严格作用域**：配方仅读取明确投入与引用的对象，禁止扫描全场景 Tag。

---

## 8. 内置物品与工厂函数

所有便捷构造均为工厂函数，返回标准 `Entity`：
`make_item`, `Door`, `Container`, `Box`, `Manuscript`, `IdentityProof`, `AuthorityToken`, `Vehicle`, `Potion`, `Knowledge`, `TrueName`, `RitualKnowledge`, `Influence`。
