# 核心引擎第三批研究：代价计算与校准

> 研究边界：本文只依据 `core_support_fetures.md`、前两批独立设计报告和本轮提案进行推导；未读取项目其他上下文，未使用网络资料。

## 1. 对基准场景提案的结论

提案“使用若干基准场景，经过多次调试获得代价函数”是**可用但不充分**的机制。

它最适合回答：

> 某个受限参数族在已声明场景分布中，是否表现出合理的效果—代价关系？

它不能单独回答：

- 未覆盖世界中的代价是多少；
- 极低概率灾难是否不存在；
- 学到的公式是否满足资源守恒、权限、身份和时间不变量；
- 角色不知道的代价是否应进入其决策；
- 关系、记忆、人格和身份损伤能否兑换成统一数字；
- 基准表现是否会推广到新的策略、规则组合和时间分支。

因此最终定案为：

\[
\boxed{
\text{符号规则负责权威结算，基准场景负责离线校准，}
\text{蒙特卡洛量化不确定性，对抗测试负责寻找平均值看不见的坏例。}
}
\]

所有学习或调试结果只能生成新的、不可变的 `CostModelVersion` 候选；不能在当前因果批次中自我修改。

## 2. 必须先分开四个问题

| 问题 | 输出 | 是否是世界事实 |
|---|---|---|
| 运行时结算 | 本次动作实际支付、托管、义务、租约和状态牺牲 | 是，提交后成为事实 |
| 设计期校准 | 哪组公式参数在测试分布中更合理 | 否，只产生模型候选 |
| 角色主观估计 | 角色根据认知认为自己可能付出什么 | 否，是认知状态的一部分 |
| 事后因果评价 | 动作造成了哪些直接、间接、延迟和外部后果 | 已发生部分是事实；反事实比较仍是模型判断 |

第一类必须确定、可重演、可审计；第二和第四类可以有统计不确定性；第三类不得偷看客观隐藏代价。

如果把四者混为一个 `cost`，就会出现：调试模型的预测直接扣玩家资源、角色因未知代价自动回避、未来风险被当作已经发生、事后文学评价反写规则等错误。

## 3. 候选机制与对抗性淘汰

| 候选 | 适用范围 | 优点 | 主要攻击 | 定案 |
|---|---|---|---|---|
| 任意 Python 代价函数 | 所有机制 | 灵活 | 可写世界、阻塞、自指、读取隐藏状态、不可证明终止 | 淘汰正式路径 |
| 受限解析/符号公式 | 明确资源、时间、距离、目标数、通道、确定牺牲 | 精确、可解释、可静态验证 | 公式可能精确地计算错误代理；复杂交互难手写 | 权威基础层 |
| 查表与分段函数 | 有限等级、有限距离带、离散状态 | 稳定、简单、易复现 | 维数灾难、边界跳变、未覆盖组合无法外推 | 局部可用 |
| 有界约束求解 | 多种支付方案、替代资源、容量与义务组合 | 能找到合法结算并给证书 | 无界/非线性问题、秘密支付路径、求解器顺序选解 | 作为结算规划器 |
| 基准场景拟合 | 复杂公式参数、平衡区间 | 能利用多次调试结果 | 过拟合、数据泄漏、场景偏见、策略刷榜、分布漂移 | 离线校准层 |
| 蒙特卡洛仿真 | 随机结果、连锁反应、策略分布 | 可估计均值、分位数和尾部 | 稀有灾难样本巨大；只覆盖采样分布 | 统计评估层 |
| 时间展开因果模型 | 延迟后果、外部性、责任分析 | 可区分直接、间接、使能和承担 | 模型依赖、反事实不唯一、预测易被误当事实 | 预测和归因辅助 |
| 对抗场景搜索 | 循环优惠、震荡、资源复制、极端组合 | 主动找坏例 | 找不到不等于不存在；攻防循环会震荡 | 发布门禁层 |
| 逆向推断/模仿 | 从玩家或角色选择推测其隐含代价 | 可发现设计者未显式写出的偏好 | 多套 cost 可解释同一行为；受认知错误和策略影响 | 仅作诊断，不作权威公式 |
| 在线自适应 | 部署后实时改参数 | 响应分布变化 | `参数→策略→数据→参数` 反馈、不可重放、可被玩家操纵 | 禁止直接更新活动模型 |
| 分层混合 | 上述机制组合 | 同时保留确定性、经验校准和极端安全 | 工程复杂 | 推荐 |

## 4. 推荐的六层混合体系

```text
层 0：权限、守恒、公理、终止等硬不变量
层 1：可解释的符号基础代价 C₀
层 2：在安全参数域内校准有限修正项 Cθ
层 3：基准场景、配对实验和变形测试
层 4：Monte Carlo、尾部风险与分布漂移报告
层 5：对抗门禁、冻结发布与版本驻留
```

形式上：

\[
C_\theta(x,\pi)=C_0(x,\pi)+\theta^T\phi(x,\pi),
\qquad \theta\in\Theta_{safe}
\]

其中：

- `C₀` 保存世界作者明确知道的结构；
- `φ` 是有限、版本化、可解释的特征；
- `θ` 只能在有界安全域内校准；
- 校准器无权生成新规则、新特征或修改硬不变量；
- 发布后的 `θ` 冻结并进入规则集哈希。

黑箱模型即使在场景上表现优秀，也不得直接负责资源结算。若使用复杂代理，只能提出参数或风险估计，并接受符号外壳的上下界、单调性、守恒和有效域约束。

## 5. 代价的基本单位是 CostAtom

代价不是一个分数，而是行动造成的一组有类型后果：

```python
@dataclass(frozen=True, slots=True)
class CostAtom:
    atom_id: str
    kind: str
    plane: CostPlane
    bearer_ref: str
    beneficiary_refs: tuple[str, ...]
    imposed_by_refs: tuple[str, ...]
    onset: str
    duration: str
    magnitude: object
    reversibility_id: str
    observability_id: str
    causal_event_id: str
    allocation_rule_id: str
    is_projection: bool
    is_settlement: bool
```

建议至少支持：

| 类别 | 典型载体 |
|---|---|
| 资源消耗 | 复式账本项、托管、销毁授权 |
| 时间占用 | 动作段持续时间、通道租约 |
| 身体损伤 | 身体状态差分及来源事件 |
| 能力/机会损失 | 可执行动作集合、载体或权限变化 |
| 义务与债务 | 有限状态义务、债权关系 |
| 关系损伤 | 关系事实、共同历史和解释变化 |
| 记忆/信息损失 | 载体、记忆痕迹、假设可区分性、已知 affordance |
| 身份损伤 | 身份侧面、自我模型冲突、相变候选 |
| 污染与延迟风险 | 来源化污染账本、计划事件、延迟 claim |
| 控制损失 | 控制关系和行动归因变化 |
| 不可逆相变 | 原子相变计划与继承结果 |

不同类型默认不可通约。没有显式世界转换规则时，一段记忆不能自动折算成五点法力，一次背叛也不能用普通资源支付后视为从未发生。

## 6. 运行时权威代价：受限公式与结算语义

权威公式流程：

```text
FormulaExpr(登记事实、动作段、参数包)
→ CostProposal
→ 去重与冲突解析
→ CostQuote
→ SettlementPlan
→ 原子事务提交
```

受限 DSL 可以支持：

- 有界整数、`Fraction` 和定点数；
- 加、减、常数乘法；
- `min/max/clamp`；
- 有限条件分支；
- 对冻结作用域的有限聚合；
- 版本化参数读取。

禁止任意递归、隐式 IO、随机墙钟、未登记查询、读取最终 `CostQuote` 和求值期间修改世界。

公式必须同时给出结算语义：

```text
bearer       谁承担
beneficiary  谁受益，或进入哪个资源汇
dimension    哪类代价
settlement   立即、托管、逐批、成功后或尝试先提交
authority    哪条规则有权收费
provenance   公式、参数、事实和事件来源
```

例如折扣将转账价格从 10 降到 8，必须明确收款方只得 8，或另有补贴者支付 2；不能只减少付款方借记而保持收款方贷记 10。

## 7. 有界约束求解

当动作有多种合法支付方式时，可构造有限整数/有理线性约束：

\[
Ax=b,\qquad Gx\le h,
\qquad x\in\mathbb Q_{\ge0}\times\mathbb Z_{\ge0}
\]

例如：

```text
支付 10 法力
或支付 2 材料 + 4 法力
同时占用 1 个专注通道
支付后至少保留 3 点生命
封印资源不可使用
```

求解器只能在规则已经声明的可替代资源和支付者中选择，不能决定权限、牺牲是否合法或义务能否买断。多解且不可比较时返回有限 Pareto 集给决策系统；不得依赖求解器内部遍历顺序。

正式解必须附可独立验证的 `SolverCertificate`。无界、不可判定、无证书或能通过循环转换增殖资源的问题直接拒绝。

## 8. 基准场景不是若干固定关卡，而是版本化场景族

一个基准场景至少包含：

```python
@dataclass(frozen=True, slots=True)
class BenchmarkScenario:
    scenario_id: str
    family_id: str
    initial_state_hash: str
    ruleset_hash: str
    subject_profile_id: str
    goal_id: str
    policy_profile_ids: tuple[str, ...]
    interference_profile_id: str
    observability_profile_id: str
    horizon: int
    seed: int
    expected_properties: tuple[str, ...]
```

必须覆盖的结构族：

| 场景族 | 要检查的问题 |
|---|---|
| 单动作基线 | 无干扰时的基础公式与结算 |
| 多目标/范围 | 目标数、面积、边界和范围费用 |
| 资源紧缺 | 下界、替代支付、托管和失败仍付费 |
| 关系派生 | 契约、所有权、授权、伤害传播、多人责任 |
| 主体/身体/载体分离 | 谁支付、远端身体用谁的资源 |
| 身份分裂/合并 | 折扣、债权、义务和唯一牺牲是否复制 |
| 并发 | 双花、通道争用、谓词幻读、重复 modifier |
| 可中断过程 | 已付、托管、未付和退款如何处理 |
| 延迟外部性 | 污染、维护费、未来伤害与违约 |
| 时间回溯/分支 | 收益与负担是否被选择性保留或重复兑现 |
| 认知不完整 | 主观估计错误但客观结算正确 |
| 规则冲突 | 两个领域、折扣、覆盖或收费权威冲突 |
| 不可通约牺牲 | 记忆、关系、身份和生命不能被普通货币吞没 |
| 循环优惠/再融资 | 负 cost、互补贴和债务递减环 |
| 稀有灾难 | 身份破坏、资源复制、非收敛与不可逆尾部 |
| 停滞与代理刷分 | “永远不行动”是否通过低切换指标刷榜 |

场景数量不是覆盖证明。报告必须按机制、关系基数、主体结构、时间策略、冲突算子、继承模式和 CostPlane 给出覆盖矩阵，并至少进行关键特征的成对组合覆盖。

## 9. 基准场景必须有变形测试

| 变形 | 必须保持或按规则变化的性质 |
|---|---|
| 重命名实体、规则和关系 ID | 报价与结果同构 |
| 打乱规则、目标和集合枚举顺序 | 报价与选择不变 |
| 复制相同 provenance | 不重复计价 |
| 合并/拆分不可观察原子步骤 | 总结算保持等价 |
| 交换承担者与受益者 | 归属、责任和叙事解释必须相应改变 |
| 已知代价改为未知 | 客观报价不变，角色主观报价必须改变 |
| 自愿改为强迫 | 状态后果可相同，责任与人格解释必须改变 |
| 可恢复改为不可恢复 | 风险包络和理由必须改变 |
| 即时代价改为延迟 | 必须生成 claim/义务/计划事件，而非简单折现消失 |
| 复制隔离时间分支 | 不影响原分支资源和幂等键 |
| 循环执行转换/优惠 | 不产生净收益或净债务减免 |
| “不行动”也增加真实后果 | 基线比较必须反映不行动代价 |

如果模型在交换承担者、自愿/强迫、已知/未知、可逆/不可逆后仍给出同一解释，说明它压扁了文学与因果结构。

## 10. 校准必须同时测量代价和边际效果

只测代价无法判断是否平衡。基准场景需要配对比较：

\[
\Delta E_i=E(do(action);z_i)-E(do(reference);z_i)
\]

\[
\Delta C_i=C(do(action);z_i)-C(do(reference);z_i)
\]

参考策略不能永远只有“不行动”，至少包括：

- 延续当前策略；
- 不行动；
- 主体当时已知的主要替代动作；
- 作品规则声明的参考能力或普通手段；
- 实际被选择的策略。

同一初始状态和随机流用于旧、新模型及参考策略的配对评估，以降低差值方差；最终门禁再使用从未参与调试的新随机流。

效果也必须类型化，例如任务推进、控制范围、信息获取、风险转移、对方行动空间缩减和不可逆改变。不能把“造成伤害”当成所有机制的唯一收益。

## 11. 基准数据切分与抗过拟合

推荐四级数据：

| 集合 | 用途 | 使用纪律 |
|---|---|---|
| 训练场景 | 拟合参数 | 可重复使用，但必须记录所有尝试 |
| 验证场景 | 选择模型和超参数 | 每次查看都会增加自适应选择次数 |
| 封闭门禁场景 | 候选冻结后的最终确认 | 一旦被用于调试，就降级为训练数据 |
| 对抗/机制留一集 | 新组合、稀有灾难和跨族泛化 | 与训练生成器和策略族隔离 |

相似 seed、同一初始世界的连续帧、同一场景的轻微变体不能假装成独立样本。独立统计单位应是场景族或预先声明的场景簇；先在簇内聚合，再跨簇计算置信。

运行时规则不得读取场景名、测试标记、seed 序号或 harness 元数据。使用隐藏生成器、ID 重命名、空间尺度扰动、规则顺序置换和策略族留一测试，防止系统专门对刷基准。

## 12. 固定模型的统计置信

若某一同质指标的损失 (L\in[0,M])，对预先固定模型和独立同分布样本：

\[
P(|R-\hat R|\ge\epsilon)
\le2\exp\left(-\frac{2n\epsilon^2}{M^2}\right)
\]

因此：

\[
n\ge\frac{M^2}{2\epsilon^2}\ln\frac{2}{\delta}
\]

即可在置信度 (1-\delta) 下将均值误差控制在 \(\epsilon\)。

同时查看 (m) 个模型、(q) 个指标和 (g) 个场景族时，必须进行联合控制，例如将失败概率分配到 \(\delta/(mqg)\)。如果设计者不断查看门禁结果并继续调参，原置信界失效；这不是“多测试几次更可靠”，而是数据泄漏。

这些概率保证依赖独立、同分布、有界和非自适应等假设。场景分布变化后不能继续沿用旧置信区间。

## 13. 稀有灾难和尾部风险

灾难概率为 (p) 时，运行 (n) 次仍零灾难的概率：

\[
P(X=0)=(1-p)^n
\]

若希望以置信度 (1-\delta) 至少看到一次此类灾难，需要：

\[
n\ge\frac{\ln\delta}{\ln(1-p)}
\approx\frac{\ln(1/\delta)}{p}
\]

所以极低概率灾难无法靠普通抽样证明不存在。观察到零失败时也只能给出上界：

\[
p\le1-\delta^{1/n}
\]

必须结合：

- 世界公理与守恒证明；
- 结构化稀有场景生成；
- 对抗搜索；
- 重要性抽样；
- 时间分支、身份与循环套利的专门性质测试。

同一代价类型内部可以报告期望、分位数和尾部风险：

\[
CVaR_\alpha(L)=
\min_\tau\left[
\tau+\frac1{1-\alpha}E(L-\tau)_+
\right]
\]

但有效尾部样本约为 (n(1-\alpha))。越接近极端尾部，需要的样本越多。不同类型代价不能先求统一总损失再计算 CVaR。

低概率不可逆风险应独立保留为风险原子，而不是被平均值稀释，也不应自动替角色决定“绝不能冒险”。

## 14. 类型化风险包络

```python
@dataclass(frozen=True, slots=True)
class RiskEnvelope:
    cost_kind: str
    bearer_refs: tuple[str, ...]
    magnitude_interval: tuple[Fraction, Fraction] | None
    probability_interval: tuple[Fraction, Fraction] | None
    onset_window: tuple[int, int | None]
    reversibility_class: str
    evidence_quality: str
    ambiguity: Fraction
    model_version: str
```

风险支配只在角色当前重视且可比较的同类维度内成立。资源更便宜但可能删除独特记忆的路径，与资源更贵但记忆安全的路径通常不可比较；核心返回二者，不擅自兑换。

主观风险包络只能使用角色可获得证据。开发者模型知道某隐藏代价，不代表角色能在决策中读取它。

## 15. 因果归因不能等同于收费或责任

需要分开：

| 概念 | 问题 |
|---|---|
| 产生原因 | 哪个事件实际造成状态变化 |
| 使能条件 | 哪些关系、权限和环境使其可能 |
| 承担规则 | 为什么由这个主体支付或承受 |
| 责任规则 | 自愿、误判、强迫、控制、自动还是偶然 |
| 可预见性 | 角色当时是否能合理预见 |
| 可避免性 | 当时认知中的可执行替代能否避免 |

一个动作位于因果链开头，不表示行动者必须承担所有后果。多主体共同造成损害时，若规则分配份额：

\[
w_i\ge0,\qquad\sum_iw_i=1
\]

补偿总额不得超过规则声明的可赔偿损害；惩罚、税费、主观愧疚和叙事严重性若同时存在，必须是不同来源、不同平面的独立原子，不能把同一损害重复收费。

预测外部性时，应固定候选干预：

\[
do(Action=a)
\]

不能把“cost 越高所以行动概率越低”重新放入同一次报价，否则形成：

```text
cost↑ → 选择概率↓ → 预期外部性↓ → cost↓ → 选择概率↑
```

有反馈的世界使用有限时间展开；静态因果模型不得包含未解析即时环。

## 16. 可逆、补偿与不可逆

“数值恢复”不等于历史和结构恢复。至少区分：

```text
immediately_reversible
conditionally_reversible
partially_reversible
compensable_not_restorable
practically_irreversible
axiomatically_irreversible
unknown
```

给定时间窗 (H)：

\[
Reversible_H(s\rightarrow s')
\iff
\exists\pi,Reach(s',\pi,H)\in[s]_{\equiv}
\]

状态等价类由专用世界声明。核心不能根据 HP、关系值或污染量回到旧数字就认定“一切从未发生”。

典型区别：

- 复制死者不必然恢复身份连续性；
- 他人转述能恢复信息内容，不必然恢复第一人称记忆；
- 关系数值回升不删除背叛历史；
- 污染转移给他人不是无代价净化；
- 封印、替代和赔偿都不是原状恢复。

## 17. 延迟代价和 Benefit–Burden 绑定

未来代价必须在当前版本建立以下至少一种事实：

- `DeferredCostClaim`；
- `Obligation`；
- `Escrow`；
- `ScheduledEvent`；
- `InFlightClaim`。

状态机示例：

```text
CREATED → SECURED/UNSECURED → DUE
→ SETTLED | BREACHED | CANCELLED_BY_RULE
```

到期时重新核验条件，但幂等键保证异常重试不会重复结算。

未来欠 100 不会因为主体当前折现为 60，就在权威账本上只剩 60。折现只属于 `BELIEF/BURDEN`，除非债权公式本身明确使用版本化指数。

当前收益与未来负担使用：

```text
BenefitBurdenPairId
```

时间回溯或分支继承若保留收益，必须同时声明如何处理义务、托管、债权、计划事件和在途凭证。默认禁止只保留收益、删除负担。

义务再融资也必须满足共同正势：

\[
p^TLiability_{new}
\ge p^TLiability_{old}-AuthorizedForgiveness
\]

减免只能来自有权债权人或世界规则，不能由循环替换生成。

## 18. 防重复计价

每个实际收费使用：

```text
ChargeKey = (
  branch_id,
  causal_consequence_id,
  charging_rule_id,
  bearer,
  beneficiary_or_sink,
  cost_dimension,
  installment_index
)
```

相同 `ChargeKey` 最多结算一次。

但不能只按因果事件全局去重：一次身体损害可以同时产生受害者的状态损失、行动者的赔偿义务和独立惩罚。它们必须具有不同收费规则与 CostPlane。

每个 proposal 还要标记：

- `is_projection`：只是另一代价的决策/叙事投影；
- `is_settlement`：会进入权威结算；
- `exclusive_group`：同组只能结算一个；
- `stack_group`：允许或限制叠加；
- `origin_claim`：来自哪个后果、债权或义务。

主观恐惧、风险预测和文学严重性不得再次作为执行借记。

## 19. 参数校准目标

基准期望应优先采用：

- 每个类型维度的允许区间；
- 参考动作之间的偏序；
- 任务推进与资源付出的包络；
- 硬失败率上限；
- 决策切换、重规划和非收敛率上限；
- 不可逆尾部风险单独门禁。

而不是一个总平均分。

形式上，可在同层可比较指标上拟合：

\[
\hat\theta=
\arg\min_{\theta\in\Theta_{safe}}
\frac1n\sum_iL_{soft}(\theta;z_i)
+\lambda\|\theta-\theta_{prior}\|^2
\]

正则项限制新模型无依据地远离可解释先验。若多个目标不可通约，保留有限 Pareto 参数集，再由专用库声明的设计偏序选择；不要让优化器私自决定“文学代价等于多少法力”。

## 20. 发布门禁与防参数震荡

运行数据只能生成：

```text
CalibrationProposal
```

不能直接修改活动模型。新版本发布流程：

```text
历史版本数据
→ 离线候选
→ 训练/验证
→ 候选冻结
→ 封闭门禁
→ 对抗测试
→ 新 CostModelVersion
→ 仅影响未来 decision epoch 和事务
```

新模型相对旧模型的配对损失差为：

\[
D_i=L_{new,i}-L_{old,i}
\]

不能只要求样本均值 \(\bar D<0\)，而应要求改善超过发布滞回，例如：

\[
UCB(E[D])< -\gamma
\]

同时所有硬指标的上置信界不超过阈值。区间重叠时保留旧模型，防止统计噪声使参数版本来回切换。

还应限制：

\[
\|\theta_{new}-\theta_{old}\|\le\eta_{max}
\]

以及门禁场景上的行为变化率：

\[
P_{gate}(policy_{new}(z)\ne policy_{old}(z))\le\kappa
\]

参数变化小并不必然使行为变化小，因此两项都要检查。发布后设置最小驻留期；失败可恢复旧版本，但已经提交的历史不重算。

## 21. 分布漂移

每个校准包保存：

- 场景族比例；
- 实体规模；
- 关系密度与冲突密度；
- 主体/载体结构；
- 时间分支深度；
- 人格类型；
- 外来干扰率；
- 决策树深度和分支数；
- 机制与规则组合覆盖。

漂移检测只能说明“当前分布不像基准”，不能证明新分布危险或安全。超出 `validity_domain` 时：

1. 回退到显式安全公式；
2. 标记 `OUT_OF_DOMAIN`；
3. 进入新的离线校准周期；
4. 禁止黑箱盲目外推。

若已知基准分布 (P) 与部署分布 (Q) 的总变差满足 (TV(P,Q)\le\rho)，且损失归一化到 `[0,1]`，则：

\[
R_Q\le R_P+\rho
\]

但通常 \(\rho\) 自身未知，因此不能把它当作免费保证。

## 22. Python 类型骨架

```python
from dataclasses import dataclass
from fractions import Fraction
from typing import Literal

@dataclass(frozen=True, slots=True)
class FormulaExpr:
    op: Literal[
        "CONST", "PARAM", "FACT", "ADD", "SUB",
        "MUL_CONST", "MIN", "MAX", "CLAMP",
        "IF", "FINITE_SUM"
    ]
    args: tuple["FormulaExpr", ...] = ()
    literal: Fraction | str | None = None

@dataclass(frozen=True, slots=True)
class ParameterValue:
    name: str
    value: Fraction
    lower: Fraction
    upper: Fraction

@dataclass(frozen=True, slots=True)
class CalibrationProfile:
    profile_id: str
    parent_model_id: str | None
    parameters: tuple[ParameterValue, ...]
    validity_domain_id: str
    training_suite_hash: str
    validation_suite_hash: str
    invariant_certificate_hash: str

@dataclass(frozen=True, slots=True)
class RolloutOutcome:
    scenario_id: str
    family_id: str
    seed: int
    effect_vector: tuple[Fraction, ...]
    cost_atom_ids: tuple[str, ...]
    hard_failures: tuple[str, ...]
    decision_switches: int
    replans: int
    versions_created: int
    budget_exhausted: bool

@dataclass(frozen=True, slots=True)
class CostEstimate:
    dimension: str
    point: Fraction | None
    lower: Fraction | None
    upper: Fraction | None
    confidence_delta: Fraction | None
    model_version: str
    evidence_refs: tuple[str, ...]

@dataclass(frozen=True, slots=True)
class CalibrationArtifact:
    artifact_id: str
    parent_model_id: str
    algorithm_hash: str
    profile: CalibrationProfile
    estimates: tuple[CostEstimate, ...]
    coverage_report_hash: str
    adversarial_witness_ids: tuple[str, ...]
    status: Literal["CANDIDATE", "REJECTED", "APPROVED"]

@dataclass(frozen=True, slots=True)
class DeferredCostClaim:
    claim_id: str
    branch_id: str
    bearer_ref: str
    beneficiary_or_sink_ref: str | None
    dimension: str
    amount_expr: FormulaExpr
    due_event_ref: str
    state: str
    benefit_burden_pair_id: str | None
    origin_rule_id: str

@dataclass(frozen=True, slots=True)
class CostModelVersion:
    version_id: str
    parent_id: str | None
    symbolic_root_hash: str
    calibration_profile_id: str
    feature_schema_hash: str
    activation_epoch: int
```

所有对象不可变；更新一律生成新版本。

## 23. 核心与模块边界

| 层 | 职责 |
|---|---|
| 核心微内核 | 精确数值、事务、账本、租约、计划事件、分支、权限、provenance 和不变量 |
| 核心 Cost 原语 | CostAtom、ChargeKey、执行结算、版本验证和投影/结算隔离 |
| 公式模块 | 受限 DSL、参数实例化、依赖读集和公式证明 |
| 校准模块 | 基准场景、拟合、覆盖报告、变形测试和模型版本候选 |
| 仿真模块 | 成对 Monte Carlo、尾部统计、独立随机流和预算化 rollout |
| 对抗模块 | 威胁模型、反例搜索、内容去重和有限轮门禁 |
| 因果模块 | 时间展开预测、实际 lineage 归因和多基线比较 |
| 约束模块 | 有界精确求解、Pareto 结算方案和解证书 |
| 义务/时间模块 | 延迟 claim、Benefit–Burden 绑定、分支继承和再融资检查 |
| 专用库 | 具体公式、参数范围、场景目标、效果指标、风险态度和兑换规则 |
| 叙事层 | 从客观与认知 trace 描述代价；不得修改结算或校准结果 |

基准场景、统计学习和因果预测不进入最小事务核；核心只提供让这些模块不能越过守恒、版本与认知隔离的原语。

## 24. 必须拒绝的结构

| 编号 | 拒绝条件 |
|---|---|
| C1 | 公式直接修改余额、关系或世界状态 |
| C2 | 公式读取最终报价、选择结果或自身发布成绩 |
| C3 | 校准参数超出安全域或破坏单调性、非负性、守恒与数值上界 |
| C4 | 当前结构超出模型有效域却继续黑箱外推 |
| C5 | 测试 harness 元数据、场景名或 seed 序号进入正式特征 |
| C6 | 训练、验证和门禁数据边界被隐瞒或重复查看后仍沿用旧置信声明 |
| C7 | 超时、预算耗尽、无动作或崩溃 rollout 被从数据中删除 |
| C8 | 因果预测被当作已发生事实或实际账本损害 |
| C9 | 同一次报价把主体选择概率放入自身外部性循环 |
| C10 | 同一 ChargeKey 重复结算，或主观/叙事投影再次进入执行账本 |
| C11 | 延迟代价没有 claim、义务、托管、计划事件或在途凭证 |
| C12 | 回溯保留收益却丢弃绑定负担，或延迟 claim 跨分支重复兑现 |
| C13 | 不可通约代价被核心擅自换算成资源 |
| C14 | 义务被普通期望值或罚金自动买断 |
| C15 | 求解问题无界、不可判定、无证书或靠遍历顺序选择多解 |
| C16 | 循环优惠、转换或再融资降低共同守恒势 |
| C17 | 零灾难样本被描述为“证明不存在灾难” |
| C18 | 对抗器未找到反例被描述为全局安全证明 |
| C19 | 在线数据在当前批次或活动模型上直接更新参数 |
| C20 | 参数小幅变化未经行为差异检查便宣称策略稳定 |

## 25. 验证矩阵

| 性质 | 验证方法 |
|---|---|
| 公式确定性 | 相同版本、模型和路径得到相同报价 |
| 公式有限性 | 表达式节点、数值位宽和有限聚合均受上限约束 |
| 守恒 | 每个执行代价物化后满足账本、租约和身份不变量 |
| 单调/边界 | 距离、目标数等声明单调特征在无反制规则时保持单调 |
| ID/顺序不变 | 重命名和置换不改变同构结果 |
| 防重复 | provenance 复制、重试和时间分支不会重复计价 |
| 效果—代价配对 | 同一场景/seed 下比较候选与多个参考策略 |
| 统计纪律 | 记录假设数、查看次数、场景簇和联合置信分配 |
| 尾部诚实 | 报告失败概率上界、灾难见证和 CVaR 有效样本量 |
| OOD 安全 | 超出有效域回退、拒绝或重新校准，不盲目外推 |
| 主客观隔离 | 未知客观代价不进入角色当前主观估计 |
| 因果/责任分离 | 自愿、误判、强迫和控制产生不同归因 trace |
| 可逆性诚实 | 补偿、封印、替代和数值恢复不冒充原状恢复 |
| 延迟一致性 | 收益与负担绑定，回溯/分支不产生套利 |
| 发布稳定 | 区间重叠保留旧模型；模型驻留与行为变化率有界 |

## 26. 保证边界

| 性质 | 保证强度 |
|---|---|
| 符号代价有限时间完成 | 严格，前提是 DSL、聚合域、位宽和表达式有界 |
| 相同输入的权威报价可重放 | 严格，前提是模型版本固定且无隐式输入 |
| 执行代价满足守恒与事务原子性 | 严格，所有代价必须物化为核心原语 |
| 调参和对抗过程有限停止 | 严格，候选、epoch、rollout 和对抗轮数有上限 |
| 新模型不改写历史和当前事务 | 严格，版本冻结并只在未来 epoch 激活 |
| 统计区间覆盖真实均值 | 概率保证，依赖独立、同分布、有界和非自适应假设 |
| 基准表现推广到所有世界 | 无法严格保证，依赖覆盖和分布漂移 |
| 零样本灾难证明绝对安全 | 无法保证，只能给概率上界和结构证明 |
| 对抗器找不到漏洞等于无漏洞 | 无法保证，除非威胁空间有限且完整枚举 |
| 校准 cost 保证玩法有趣 | 只能受限保证，需目标、策略族和场景设计合理 |
| 数学代价保证文学美感 | 无法保证，只能保证后果、承担、可逆性和因果结构完整 |

## 27. 推荐实施顺序

| 阶段 | 产物 | 首要验收 |
|---|---|---|
| 1 | CostAtom、ChargeKey、Reversibility、Benefit–Burden | 类型隔离、无重复结算、延迟可追溯 |
| 2 | 受限 FormulaExpr 与安全参数域 | 确定性、有限性、守恒和自指拒绝 |
| 3 | SettlementPlan 与有界约束求解 | 解证书、多解 Pareto、无套利 |
| 4 | BenchmarkScenario、场景族和变形测试 | 结构覆盖、ID/顺序不变、主客观隔离 |
| 5 | 配对 rollout 与效果—代价报告 | 多基线、独立随机流、失败不删样本 |
| 6 | 统计与尾部分析 | 置信界、稀有灾难上界、CVaR 样本报告 |
| 7 | 因果、外部性和延迟义务 | `do(action)` 固定、责任分离、跨时间无套利 |
| 8 | 对抗门禁与 CostModelVersion 发布 | 反例保留、发布滞回、行为差异、可回退 |

第三批最重要的工程原则不是找到一个越来越复杂的“万能代价函数”，而是建立一条证据链：

```text
为什么这样计价
→ 在哪些场景中校准
→ 哪些结构尚未覆盖
→ 统计上有多不确定
→ 极端情况下会怎样失败
→ 最终如何物化为不能逃避、复制或重复的世界后果
```

只有这条证据链完整，经过多次调试得到的代价函数才有资格进入下一个模型版本。
