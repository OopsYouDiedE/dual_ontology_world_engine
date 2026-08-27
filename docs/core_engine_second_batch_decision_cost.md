# 核心引擎第二批基础能力：决策树与路径 Cost

> 研究边界：本文只依据 `core_support_fetures.md`、第一阶段独立设计报告和本轮需求进行推导；未读取项目其他上下文，未使用网络资料。

## 1. 定案

接受以下核心命题：

> 角色在有限认知上展开有限决策树；规则不直接替角色选择动作，而是对特定动作路径提交 cost 修饰；角色在规则取向、规则矛盾、外来干扰和人格内驱共同形成的评价结构上选择意图。

但必须加入四项修正：

1. **cost 不是单一浮点数**，而是类型化、分平面、带来源的有限结构；
2. **角色优化的是自己相信的决策树**，不是偷看客观世界所得的真实未来树；
3. **客观阻碍、主观干扰和强制控制不能全部伪装成 cost**；
4. **相反规则不能用正负数静默相消**，否则真正的道德或规则矛盾会被错误抹除。

最终模型称为：

\[
\boxed{版本绑定的有限认知决策树 + 类型化 CostQuote + 可废止实践推理}
\]

它补充第一阶段微内核，但不改变“任何选择最终只产生统一意图，客观世界仍由事务核结算”的原则。

## 2. 四种力量的统一位置

| 力量 | 在模型中的位置 | 正确作用 | 禁止做法 |
|---|---|---|---|
| 规则造成的优先动作取向 | `CostModifier`、义务理由、严格优先层 | 提高/降低某类路径代价，或形成不可违反/可违背但有后果的规范负担 | 规则直接返回“选择动作 A” |
| 规则冲突造成的矛盾 | modifier 冲突图、理由反驳图、非相消义务集合 | 解析修改权威；保留无法相消的价值牺牲与违约意义 | 用遍历顺序决定；把 +10 与 -10 相消后声称无冲突 |
| 外来干扰 | 客观路径结构、执行报价、认知/情绪/偏好通道或控制关系 | 泥地改变耗时，恐惧改变风险解释，附身改变控制权 | 把身体被强制行动叙述成角色主动选择 |
| 角色性格的内在驱动 | 人格偏序、稳定倾向、构成性禁忌、习惯与满足阈值 | 决定哪些代价重要、哪些理由能击败另一些理由 | 把人格压成一个可被任意规则覆写的权重向量 |

四者共同参与决策，但拥有不同的类型和修改权。核心不预设“规则永远压过人格”或“人格永远战胜控制”；世界规则必须声明介入性质与冲突语义。

## 3. 先区分五个 Cost 平面

\[
C^{execution}\ne C^{belief}_a\ne C^{burden}_a
\ne C^{search}\ne C^{narrative}
\]

| 平面 | 含义 | 读取者 | 能否改变世界合法性 |
|---|---|---|---|
| `EXECUTION` | 实际资源、材料、生命、时间、通道占用与义务 | 客观事务解析器 | 能，但必须通过账本、租约、权限和事务结算 |
| `BELIEF` | 角色根据证据估计的风险、费用和结果区间 | 决策器 | 不能；可以与真实费用不同 |
| `BURDEN` | 羞耻、恐惧、忠诚冲突、违诺意义等主观负担 | 实践推理器 | 只能影响选择；不能直接扣世界资源 |
| `SEARCH` | 启发式、剪枝下界、节点扩展成本 | 有界规划器 | 不能，且不是世界事实 |
| `NARRATIVE` | 对已选事实的表现、显著度与组织建议 | 叙事层 | 绝不能参与动作合法性或客观结算 |

如果角色不知道施法会损失记忆，则 `EXECUTION` 报价包含这项代价，但角色当前 `BELIEF` 报价不能因此自动变高。其动作是否失败、是否事后发现代价，仍要经过正常事务、感知和证据流程。

同理，改变主题或意象标签不得改变角色选择；否则“文学性”会成为越权操纵人格的隐藏规则。

## 4. 决策对象不是单条动作，而是有限条件策略

决策树是主体认为可能发生的有限 AND/OR 树：

| 节点 | 含义 |
|---|---|
| `DECISION` | 主体可以选择的已知动作 |
| `OUTCOME` | 主体相信可能出现的结果分支 |
| `OBSERVATION` | 未来获得信息后才能决定的分支点 |
| `INTERRUPTION` | 被打断、失效或需要重新报价的节点 |
| `TERMINAL` | 当前审议视野中的终止或估值边界 |

形式上：

\[
T_a=(N,E),\qquad depth(T_a)\le D_{max},\qquad degree(n)\le B_{max}
\]

因此：

\[
|N|\le \sum_{i=0}^{D_{max}}B_{max}^i
=\frac{B_{max}^{D_{max}+1}-1}{B_{max}-1}
\]

树的节点来自 `EpistemicView`。未知的客观禁制不能提前删除角色认为可尝试的路径；角色已知且相信成立的禁制可以参与主观过滤。最终首个动作被提交为统一意图，客观层重新检查真实权限、资源、作用域和通道。

路径评分过程中不得继续动态创建候选。新发现的信息、后悔或“越想做越抗拒”只能在下一已提交版本中开启新的 `decision_epoch`。

## 5. 类型化路径 Cost

路径 cost 是不同维度代数的积，而不是“总分”：

```text
resource.mana       : 非负有理数，顺序加法，结算到账本
resource.material   : 类型化资产转移，不能跨类型相消
time.duration       : 顺序相加；显式并行段可取最大值
channel.focus       : 容量租约，不是普通数值余额
risk.injury         : 主体信念中的有界区间
norm.commitment.X   : 违反某项承诺的独立负担，不与另一承诺相消
drive.protection    : 人格倾向产生的主观理由
affect.fear         : 情绪对风险解释或行动阻力的有界影响
uncertainty         : 证据不足产生的区间宽度或未知标记
```

每个维度必须声明：

- 值域和精确数值表示；
- 路径段之间的组合运算；
- 是否允许相消、封顶或下界；
- 谁拥有修改权；
- 属于哪个 cost 平面；
- 是否需要实际结算，以及接收方或资源汇；
- 比较时属于哪个语义层。

实际支付不能只写成 `mana += 5`。它必须形成带来源的账本项。奖励也不能用负执行 cost 表示，而应在动作成功后作为独立效果贷记；否则路径循环可能无限刷取“负成本”。

## 6. 规则提交 CostModifier，而非修改可变数值

推荐数据流：

```text
不可变动作树
+ 原始 CostComponent
+ 当前版本适用的 CostModifier
+ 冲突/叠加/权限解析
= 版本绑定的 CostQuote
```

一个 modifier 至少声明：

| 字段 | 目的 |
|---|---|
| `plane` | 防止主观恐惧越权修改实际支付 |
| `selector` | 指向 actor、身体/载体、动作种类、路径/段/组件和作用域 |
| `operation` | `ADD/MULTIPLY/FLOOR/CAP/REPLACE` 等受限操作 |
| `dimension` | 只能修改声明的类型化成本维度 |
| `authority_domain` | 证明来源有权修改该维度 |
| `read_stage/write_stage` | 建立无环成本依赖 |
| `stack_mode` | 明确一次性、按段、按来源、上限或累积 |
| `validity` | 分支、版本、时间区间和撤销纪元 |
| `provenance` | 规则、事实、事件 lineage 与来源实体 |

作用粒度必须区分：

| 粒度 | 示例 | 应用次数 |
|---|---|---|
| `PATH` | 整个背叛方案承担一次承诺负担 | 每路径一次 |
| `SEGMENT` | 每次穿越污染区域增加负担 | 每个匹配段一次 |
| `ACTION_KIND` | 所有传送动作实际法力增加 | 每个匹配动作一次 |
| `COST_COMPONENT` | 只修改火属性材料费 | 每个匹配原始组件一次 |

## 7. 规范化 Cost 代数

在同一允许数值化的维度中，采用固定阶段而不是规则注册顺序：

```text
BASE
→ STRUCTURAL_REWRITE
→ MULTIPLICATIVE
→ ADDITIVE
→ FLOOR_CAP
→ SETTLEMENT / SUBJECTIVE_QUOTE
```

规则必须满足：

\[
readStage(modifier)<writeStage(modifier)
\]

对于维度 \(d\)：

\[
raw_d=b_d\prod_{m\in M_d}m+\sum_{a\in A_d}a
\]

\[
L_d=\max floors,\qquad U_d=\min caps
\]

\[
q_d=\min(U_d,\max(L_d,raw_d))
\]

要求：

- 使用有界整数、定点数或受限 `Fraction`；禁止 `NaN`、无穷和平台相关舍入；
- 倍率非负；最终执行成本非负；
- `floor > cap` 时该报价失败；
- 加法和倍率各自满足交换律、结合律；固定增减量是否受倍率影响由上述阶段语义固定；
- `REPLACE` 不参与普通折叠，只能由冲突解析后唯一胜出的授权规则产生。

若世界需要“先减 5 再乘 2”，必须声明两个不同阶段，而不是依赖加载顺序。

## 8. 防重复叠加

仅用随机 `ModifierId` 去重无效，因为同一来源可以复制出多个实例。使用语义应用键：

```text
ApplicationKey = (
  branch_id,
  source_semantic_key,
  source_epoch,
  rule_id,
  action_path_id,
  segment_or_path_marker,
  cost_dimension,
  input_version,
  event_lineage
)
```

叠加策略：

| 策略 | 语义 |
|---|---|
| `ONCE_PER_PATH` | 同一路径一次 |
| `ONCE_PER_SEGMENT` | 每个匹配段一次 |
| `ONCE_PER_SOURCE` | 同一语义来源一次，即使有多个对象 ID |
| `MAX_N` | 最多 N 层 |
| `UNIQUE_GROUP` | 同组只保留冲突解析后的一个 |
| `ACCUMULATE` | 明确允许全部累积 |

没有叠加策略、没有稳定来源语义键，或相同事实通过关系、标签和监听器三路重复生成 modifier 时，报价必须失败。

## 9. 规则优先与冲突

### 9.1 修改权冲突

CostModifier 的优先关系来自语义偏序：

\[
P=(plane,phase,authority,scope\ specificity,rank,counter,stackGroup)
\]

普通兼容 `ADD/MULTIPLY` 可以交换折叠。真正需要冲突解析的是：

- 两个 `REPLACE`；
- `floor > cap`；
- “不可修改”与修改规则并存；
- 两个 settlement sink 不一致；
- 客观与主观 cost 被错误混用；
- 两个硬取向规则要求互斥动作。

不可比较且没有声明抵消、覆盖、竞争或共同失败策略时，只让相关路径的 `CostQuote` 失败，不依赖规则 ID 或创建顺序，也不拖垮无关候选。

### 9.2 规范矛盾不能数值相消

假设：

```text
契约 A：必须留下保护甲
契约 B：必须离开保护乙
人格：无法轻易放弃任何同伴
```

这不是 `+10 + (-10) = 0`。正确结构是：

```text
leave  → breach(A), protect(B), burden_abandon_A
stay   → protect(A), breach(B), burden_abandon_B
```

两个违约维度分别保留。角色必须选择牺牲，并在 `DecisionTrace` 中记录主要理由、主要反对理由和未选替代路径。文学性来自冲突痕迹与后果重量，而非算术抵消。

## 10. 外来干扰的分型

| 干扰类型 | 正确建模位置 | 示例 |
|---|---|---|
| 客观环境 | 树的真实执行条件或 `EXECUTION` quote | 泥地增加耗时，结界使目标不可达 |
| 已感知环境 | `BELIEF` quote 与主观候选树 | 角色看见泥地，估计行动变慢 |
| 感知/记忆干扰 | 认知投影管线 | 幻觉、遗忘、错误记录 |
| 情绪干扰 | 注意、风险解释、审议深度、撤销门槛 | 恐惧扩大未知风险区间 |
| 偏好/污染干扰 | 带来源的 `BURDEN` modifier 或人格状态变化 | 对某对象产生异常依恋 |
| 身体控制 | 控制关系、强制意图或执行阻断 | 附身、麻痹、远程操纵 |

外力若能直接指定最终动作，就不再是普通 cost 修饰，而是控制关系或强制意图。叙事和 trace 必须区分“主体决定攻击”与“主体身体被迫攻击”。

污染也不能从认知平面越权改变客观支付。若某污染同时具有主观诱导和客观法力税，应产生两个分别授权、分别结算的效果。

## 11. 人格不是一组裸权重

人格快照由以下结构组成：

```text
constitutive_constraints  极少数构成性禁忌
value_partial_order       可不完备的价值偏序
stable_dispositions       稳定倾向而非硬命令
habitual_policies         低强度情境下的习惯策略
tolerated_conflicts       能容忍或无法容忍的理由冲突
```

一般的“勇敢、忠诚、善良”不能默认成为硬约束。严格词典序只能用于作品明确声明的极端设定；否则人物会僵化、无法成长，也无法在紧急情境中重新权衡。

承诺必须是显式关系，具有对象、作用域、期限、撤销条件、违约意义和相变继承策略。承诺可以被违反，但违反必须成为可追溯事实或主观负担，而不是被 cost 算术抹除。

情绪不使用“愤怒 +20 攻击偏好”的通用规则。它更适合改变：

- 注意哪些证据；
- 如何解释未知风险；
- 审议的时间视野和深度；
- 生成哪些候选动作；
- 何时复议既有承诺；
- 如何表达已经形成的意图。

## 12. 可废止理由图

CostQuote 告诉角色“这条路在各维度上意味着什么”，理由图解释“为什么这些意义能支持或反对行动”。

每条 `PracticalReason` 保存：

- 支持或反对的候选；
- 来源是信念、承诺、关系、人格倾向、情绪、cost 还是习惯；
- 对应的证据、事件或承诺引用；
- 所属价值；
- 置信区间；
- 推理层级。

理由图必须有限且分层。同层使用有限论证集上的 grounded 最小不动点；高层只能按明确规则反驳低层。禁止：

- 文学主题作为实践理由；
- 隐藏客观事实生成角色理由；
- 行动结果生成记录后倒过来成为其自身的独立先验理由；
- 理由优先级取决于该理由最终是否获胜。

## 13. 决策算法

推荐完整流程：

```text
1. 冻结认知版本、人格版本、目标和 decision_epoch
2. 从主体已知动作语汇展开有限认知决策树
3. 生成原始 BELIEF/BURDEN cost 与实践理由
4. 收集并冻结适用 CostModifier
5. 校验修改权、叠加策略和阶段依赖；解析 modifier 冲突
6. 生成每条有限策略的版本绑定 CostQuote
7. 应用主体认知中的构成性约束与显式义务
8. 求分层理由图的 grounded 接受集
9. 按稳定价值偏序与同层局部 Pareto 删除明确被支配路径
10. 应用人格满足阈值、切换代价与策略连续性
11. 仅在主体视角近似等价的有限候选间使用固定种子
12. 输出首个 IntentDraft 和 DecisionTrace
13. 客观事务核独立生成 EXECUTION quote 并结算
```

### 13.1 局部 Pareto

对同层软维度：

\[
\pi_a\prec\pi_b
\iff
\forall d,C_d(\pi_a)\le C_d(\pi_b)
\land
\exists d,C_d(\pi_a)<C_d(\pi_b)
\]

维度、叶节点和前沿大小都有硬上限。前沿过大时返回诊断，或使用规则集事先声明、误差有界的近似；不能动态生成更多“文学指标”使每条路径都进入前沿。

### 13.2 满足化与兜底

人格为同层维度定义上下文相关的可接受阈值，而非要求理论全局最优。无明确胜者时按顺序：

1. 维持仍合法的既有策略；
2. 选择可逆性更高的路径；
3. 选择能获得区分性证据的试探；
4. 采用已声明的低风险习惯；
5. 输出 `deliberation_incomplete`，选择等待、求助或协商。

不能在所有不可比较候选中任意抽签。

## 14. 防 cost 自指

决策依赖必须严格单向：

```text
已提交认知/人格/关系
→ 冻结动作树
→ 原始 cost
→ 冻结 CostModifier
→ 冲突解析与 CostQuote
→ 理由接受与动作选择
→ IntentDraft
→ 下一世界事务
```

以下结构在规则装载时拒绝：

- “若动作最终被选择，就提高它的 cost”；
- “选择最不可能被选择器选中的动作”；
- “若最终 cost 大于 5 则减 2”，且减后条件失效；
- 折扣激活费用由折扣后的结果支付；
- 当前候选效果生成 modifier，反向修改本事务 cost；
- 人格阈值由最终动作反向定义；
- 免费循环产生资源、权限、位置、认知或其他世界进展。

成本依赖图若存在 `Selected → Cost → Selected` 或同阶段非单调 SCC，规则集装载失败。合法反馈必须先提交到下一版本，再进入新的 decision epoch。

## 15. 防决策震荡与重规划活锁

### 15.1 同一版本

键：

\[
DecisionKey=(subject,goal,inputVersion,personalityVersion,epoch)
\]

同一键只求值一次并缓存；动作树、modifier、理由和种子均冻结，所以同一 decision epoch 严格无震荡。

### 15.2 相邻版本

旧策略仍合法时，只有新策略在关键维度上超过滞回裕量才切换。可使用稳健支配：

\[
\forall d,\ C_d(new)\le C_d(old)+\epsilon_d
\]

并要求至少一个关键维度改善超过 \(h_d\)。硬失效、目标消失、控制权撤销或已知灾难可立即打破驻留。

仅有成对切换成本不能防止 `A→B→C→A`。要证明无外源情况下最终停止，每次自发切换必须满足良基秩下降、公共势下降或消耗最小正量的有界资源之一。

若没有这种证明，只承诺窗口内切换次数：

\[
N_{switch}(T)\le MAX\_REPLANS+
\left\lceil\frac{T}{dwell_{min}}\right\rceil
\]

### 15.3 外来干扰下的有限重规划

只在 `DecisionTrace` 依赖集发生真实变化时重规划；多个变化合并为一次；同一输入不重复抽样。每主体、目标、tick 和 decision epoch 都有 `MAX_REPLANS`。

外界持续改变时不能保证动作最终执行，但严格保证有限次后返回 `REPLAN_EXHAUSTED`，转入等待、安全动作或明确失败，不无限递归和重算。

## 16. 跨批次动作报价

长动作必须声明：

| 策略 | 语义 |
|---|---|
| `LOCKED_AT_START` | 开始时托管完整成本；之后变化不影响已锁部分 |
| `PER_BATCH` | 每个可中断步骤按当时版本重新报价 |
| `INDEXED` | 锁定公式，之后按版本化客观指数结算 |
| `FIXED_COMPONENTS` | 已支付部分锁定，未支付部分重新报价 |

任何 `CostQuote` 都绑定分支、基版本、路径哈希、modifier 集合哈希、证明根、谓词读令牌、账户版本、通道版本和作用域依赖纪元。依赖变化后必须重算或使用已声明锁价策略，不能让动作开始时的陈旧价格无限有效。

## 17. Python 类型骨架

```python
from dataclasses import dataclass
from enum import Enum, auto
from fractions import Fraction

class CostPlane(Enum):
    EXECUTION = auto()
    BELIEF = auto()
    BURDEN = auto()
    SEARCH = auto()
    NARRATIVE = auto()

class ModifierOp(Enum):
    ADD = auto()
    MULTIPLY = auto()
    FLOOR = auto()
    CAP = auto()
    REPLACE = auto()

class StackMode(Enum):
    ONCE_PER_PATH = auto()
    ONCE_PER_SEGMENT = auto()
    ONCE_PER_SOURCE = auto()
    MAX_N = auto()
    UNIQUE_GROUP = auto()
    ACCUMULATE = auto()

@dataclass(frozen=True, slots=True)
class CostComponent:
    component_id: str
    plane: CostPlane
    dimension: str
    amount: Fraction
    bearer_ref: str
    sink_ref: str | None
    authority_domain: str
    origin_rule: str
    algebra_id: str

@dataclass(frozen=True, slots=True)
class CostModifier:
    modifier_id: str
    plane: CostPlane
    selector_id: str
    dimension: str
    operation: ModifierOp
    value: Fraction
    read_stage: int
    write_stage: int
    stack_mode: StackMode
    stack_group: str
    max_stacks: int | None
    authority_domain: str
    valid_from: str
    valid_until: str | None
    provenance_id: str

@dataclass(frozen=True, slots=True)
class CostQuote:
    branch_id: str
    base_version: str
    path_id: str
    quote_plane: CostPlane
    final_components: tuple[CostComponent, ...]
    applied_modifier_ids: tuple[str, ...]
    suppressed: tuple[tuple[str, str], ...]
    proof_roots: tuple[str, ...]
    predicate_read_tokens: tuple[str, ...]
    path_hash: str
    modifier_set_hash: str

@dataclass(frozen=True, slots=True)
class DecisionContext:
    subject_id: str
    goal_id: str
    epistemic_version: str
    personality_version: str
    decision_epoch: int
    max_depth: int
    max_branching: int
    max_nodes: int
    max_frontier: int
    max_replans: int

@dataclass(frozen=True, slots=True)
class DecisionProof:
    decision_id: str
    context: DecisionContext
    chosen_path_id: str
    principal_reason_ids: tuple[str, ...]
    principal_objection_ids: tuple[str, ...]
    cost_quote_ids: tuple[str, ...]
    incumbent_path_id: str | None
    fallback_kind: str | None
    replan_attempt: int
```

关键类型防线：

```python
def decide(view: EpistemicView, ...) -> tuple[IntentDraft, DecisionProof]: ...
def resolve(intent: Intent, world: ObjectiveView) -> TransactionResult: ...
```

决策器没有 `ObjectiveView`；客观解析器不读取文学主题来决定成败。

## 18. 核心、通用模块与专用库边界

| 层 | 新增职责 |
|---|---|
| 核心微内核 | 精确数值、CostComponent 原语、修改权证明、版本依赖、谓词读、事务结算、统一 trace |
| 核心决策服务 | 有界树/图执行预算、DecisionKey 幂等、依赖分层、有限重规划和失败证书 |
| 通用路径 Cost 模块 | ActionPath、CostModifier 收集、叠加、规范折叠、CostQuote、锁价策略 |
| 通用实践推理模块 | 人格结构、承诺、理由图、局部 Pareto、满足化、策略连续性 |
| 通用认知模块 | 客观报价与主体报价隔离；认知/污染对 BELIEF/BURDEN 的合法影响 |
| 专用库 | 具体动作、成本维度、人格价值、数值、地形、契约、污染和冲突关系 |
| 叙事层 | 把 DecisionProof 与后果组织成文学表达；不得修改决策或结算 |

这使“角色决策”成为基础能力，但不把“勇气、荣誉、胆怯、诅咒”等作品答案硬编码进核心。

## 19. 对抗性拒绝清单

| 编号 | 必须拒绝 |
|---|---|
| D1 | 任意 Python `modify_cost(path)` 回调参与正式决策或结算 |
| D2 | modifier 读取自己正在形成的最终 cost |
| D3 | 选择结果反向修改本次选择的 cost |
| D4 | modifier 没有稳定语义来源键或叠加策略 |
| D5 | 结果依赖规则遍历、注册、创建或容器顺序 |
| D6 | 两个不可比较的 `REPLACE` 同时有效，或 `floor > cap` |
| D7 | 负执行 cost 被当作奖励，或零成本闭环产生世界进展 |
| D8 | modifier 无权修改目标 authority domain |
| D9 | 主观干扰越权修改资源、权限、公理或客观事实 |
| D10 | 客观阻碍仅被设成“大 cost”而没有条件/作用域检查 |
| D11 | 强制控制被归因成主体自主性格决定 |
| D12 | 未知客观事实进入角色候选、报价、理由或剪枝依据 |
| D13 | 相反义务被数值相消后丢失冲突与违约后果 |
| D14 | cost 规则 SCC 非单调且没有分层、延迟或良基证明 |
| D15 | 长动作没有锁价/重报价策略，或 quote 跨分支重放 |
| D16 | 同一输入重复随机，或重规划没有次数和依赖范围上限 |
| D17 | 文学主题、意象或未来剧情需要参与动作比较 |
| D18 | 决策预算耗尽后仍宣称获得全局最优动作 |

## 20. 验证矩阵

| 性质 | 验证要求 |
|---|---|
| 单位不变性 | 等价单位换算不改变动作偏序 |
| 置换不变性 | modifier 输入顺序任意置换，报价和选择不变 |
| 重复不变性 | 复制同一语义来源不增加叠加次数 |
| 认知不干涉 | 修改主体不可见客观事实，不改变当前决策 |
| cost 隔离 | 未知执行成本变化不改变主观报价；已感知证据变化才可改变 |
| 主题不干涉 | 只改叙事标签，动作决定不变 |
| 冲突保真 | 相反承诺不会相消，两个牺牲方向均留在 trace |
| 抗微扰 | 小于语义容差的 cost/情绪变化不触发策略切换 |
| 有限响应 | 持续独立反证最终触发非构成性承诺复议 |
| 自指拒绝 | `Selected→Cost→Selected` 和折扣振荡在加载期失败 |
| 有限终止 | 节点、前沿、理由和重规划均在预算内结束或明确失败 |
| 重演确定性 | 同一版本、人格、规则集、epoch 和种子产生同一 DecisionProof |
| 强制诚实 | 控制关系导致的行动不被解释成人格选择 |
| 长动作一致性 | 费用变化按声明的锁价策略执行，不重复扣费或沿用陈旧报价 |
| 主要替代解释 | 关键决定至少解释一个未选主要替代路径 |

## 21. 保证边界

| 性质 | 可作出的承诺 |
|---|---|
| 单次决策有限结束 | 严格；前提是树、理由和 modifier 使用受限 DSL 与硬预算 |
| 同一 decision epoch 不震荡 | 严格；冻结输入、一次求值、缓存结果 |
| cost 求值无循环自指 | 严格；阶段依赖与静态 SCC 拒绝 |
| modifier 结果与遍历顺序无关 | 严格；规范阶段、交换折叠、显式冲突解析 |
| 动态重规划不活锁 | 严格为“有限次后停止/失败”；不保证持续干扰下必能行动 |
| 无外源时最终停止切换 | 有条件；需要良基秩、公共势或正耗散证明 |
| 当前有限树上的精确最优 | 有条件；搜索完整且偏序最终选择规则完备时成立 |
| 全局未来最优 | 不保证；角色认知、规划深度和计算预算有限 |
| 人格具有连续性和可解释性 | 结构性保证；具体人物是否写得好由专用库决定 |
| 文本必然具有文学美感 | 不保证；保证因果、POV、冲突和后果基础 |

核心可以作出的最强承诺是：

\[
\boxed{
\text{角色在冻结的有限认知上，于确定预算内生成一个可解释、可重放的动作意图；}
}
\]

\[
\boxed{
\text{规则只能通过有来源、有权限、有作用域和叠加纪律的 CostModifier 影响路径评价；}
}
\]

\[
\boxed{
\text{若规则冲突或外界持续使计划失效，系统在有限步内保留矛盾、停止重算并给出失败证明，而不是震荡、活锁或任意选边。}
\]

## 22. 推荐实现顺序

| 阶段 | 产物 | 首要验证 |
|---|---|---|
| 1 | CostPlane、CostDimension、CostComponent 与代数注册表 | 类型隔离、精确数值、单位不变性 |
| 2 | CostModifier IR、selector、authority 与 stacking | 置换不变、去重、越权拒绝 |
| 3 | CostQuote、规范化阶段与 provenance | 自指拒绝、冲突证明、版本失效 |
| 4 | 有限认知动作树和 DecisionKey | 节点上界、认知不干涉、幂等重演 |
| 5 | 理由图、人格偏序、承诺与满足化 | 冲突不相消、有限响应、替代理由 |
| 6 | 滞回、策略驻留与有限重规划 | 微扰稳定、三态循环、持续干扰停止 |
| 7 | 客观执行报价与事务核对接 | 账本、租约、权限、长动作锁价 |
| 8 | DecisionProof 与叙事投影 | POV 隔离、强制诚实、文学层只读 |

第二批能力首先应实现“报价可以被拒绝、选择可以承认未完成”的语义，而不是急于保证每个角色在每个时刻都能找到唯一动作。安全地保留矛盾，比用一个任意数字把矛盾抹平更符合这个引擎的数学与文学目标。
