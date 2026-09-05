# Entity System 统一模型与数据规范

## 1. 核心架构与基础结构

系统不为物品、知识或组织建立平行类族，核心仅维护三种基础结构：
- **`Entity`**：唯一的运行时世界对象。持有 `name`、`tags`（类别与固有 Aspect）、`properties`（具体性质与直接对象引用）与 `facets`（语义项）。
- **`Facet`**：挂载于实体的轻量语义项，用于关系、声明、条件名称与动态 Aspect。
- **`Passage`**：世界树内部的双端拓扑连接结构（非 Entity），用于计算通行代价。

## 2. 基础属性与资源模型

### 六项基础属性
- **身体实体（Character）**：`Vigor`（体魄）、`Finesse`（巧技）、`Perception`（感知）。
- **意识实体（Soul）**：`Reason`（理性）、`Stability`（自持）、`Presence`（显著）。
- 身体与 Soul 双向绑定，换体时各自独立持有属性。

### 资源模型
- 同质度量存入 `Entity.resources`（如生命、耐力）。具象资源直接使用 Entity。
- 支付必须核销实际对象额度，禁止循环引用套利。

## 3. 知识、观察与采信

客观世界事实、观察结果与主体信念严格解耦：
- **`Reveal` Facet**：定义对象可见条件。未满足时观察者仅能读取公开名称。
- **`Claim` Facet**：记录载体声称的主张。主观错误知识不写入客观世界事实。
- **采信门控**：仅标记为 `Accepted` 的知识参与 `Soul.cognitive_environment()` 决策。拒绝或暂存不删除主张与来源。
- **定向查询**：`claims_about(subject)` 保留主体、目标、来源与置信度。

## 4. Aspect 投影与 Influence 动态影响

- **Aspect 投影**：`entity.aspects(target, context)` 实时汇总固有 Tag、材料衰减、属性映射与 Facet 贡献。
- **Influence**：动态情感与状态统一作为挂载在 Soul 下的子实体，持有类型、强度、持续时间与衰减率。伤势、污染与专注共用该结构。
- **稳定人格（Disposition）**：作为有来源的 Step（候选生成）与 Preference（候选排序）的依据，与临时 Influence 分开维护。

## 5. 附录：核心词表约定

### 十二原理（Aspect）
- `Form`（形质）、`Name`（名契）、`Law`（律令）、`Mind`（心识）、`Bond`（缘系）、`Threshold`（门隙）。
- `Transmutation`（蜕变）、`Preservation`（存续）、`Death`（终寂）、`Desire`（欲求）、`Strife`（争衡）、`Vestige`（遗痕）。

### 十二情绪（Emotion）
- `Love`（爱）、`Hatred`（仇恨）、`Fear`（恐惧）、`Anger`（愤怒）、`Joy`（喜悦）、`Sorrow`（悲伤）。
- `Hope`（希望）、`Despair`（绝望）、`Remorse`（悔恨）、`Devotion`（虔诚）、`Obsession`（执念）、`Serenity`（宁静）。
