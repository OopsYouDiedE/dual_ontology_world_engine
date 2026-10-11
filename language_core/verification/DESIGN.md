# DV 正式会话自动验证设计

状态：2026-10-12 正式设计，尚未实现。本轮不实现或运行验收器，也不恢复独立复评。本文只定义通用验证架构；内容依据见[设计规范](../../docs/design-standard.md)，章节实例、答案与对白只由 `.story` 提供，本文不复制章节设定。未绑定的实例条件不能默认已经解决。工程检查不能证明机制、人物或游戏体验合格。

## 架构与接口

主程序及最多三个补充程序全部用 DV 编写；C# ABI 仅平台 I/O，不承载推演、判定、代价或案例求解算法。manifest 将主体可读初始数据、权威数据、验收 oracle 分包；加载器不能把 oracle 或预期里程碑输入策略。允许装载正式 `.story` 声明的初始世界和既有认识，禁止把为验收制造的答案伪装成初始化。

以下模块、API 和入口均为拟议设计，尚未实现，没有可运行命令。

| 模块 | 拟议接口与边界 |
| --- | --- |
| manifest | `freeze(configuration) -> manifest`；`validate(manifest, source, runtime) -> validity`，固定来源、分包、规则、参数与指纹 |
| trace | `append(event) -> event_id`；`seal(session) -> trace_hash`，只新增不可变因果事件 |
| session-runner | `start(manifest) -> session`；`advance(session) -> receipt`，正式开局、主体自行选择与真实执行，无代选权限 |
| read-only-judge | `judge(manifest, sealed_trace, authority_view) -> findings`，自身 DV 算法只读判定，不向主体回写 |
| cost-audit | `audit(quote, reservation, receipt, settlement) -> finding`，核对正式动作代价边界 |
| report | `render(findings, evidence_refs) -> report`，逐项事实、来源、范围与限制，不汇总通过数量或通过率 |

| DV 入口（拟议文件） | 任务 |
| --- | --- |
| `main-causal.dv` | 正式会话主因果、来源、时序和代价契约 |
| `compare-capability.dv` | 同初始开局的授权能力配置比较 |
| `check-evidence-boundary.dv` | 观察、听觉、传播与来源边界比较 |
| `sweep-parameters.dv` | 批准范围内的冻结参数邻域与交互检查 |

当前动态 ABI 不能静态保证分域，设计依赖受控入口和可核对审计；未实现时不得宣称安全。裁判不只信人物自行上报的 causal、basis、cost 字段，而须由冻结公理、真实提交回执和不可变前后版本交叉核对。无可核对来源则为 `insufficient_evidence`。此处的独立推导是只读裁判自身的 DV 算法，不是独立 Agent 复评。

## 基线与运行

manifest 固定 approved axiom 的 ID、version、hash，以及源码、运行程序集、剧本、权威判定、认知前提、输入执行、计时和测量规则。新增或修改公理不得自动接受：记录变更单、原因和具体差异，由主代理对照既有授权核定，再建立明确区分的新基线并重新正式开局，不能冒充同条件优化；不恢复独立复评。已有用户认可范围可直接采用；无依据的新公理或范围保留未绑定。运行期间保护文件变化，该轮为 `invalid_run`。

正式初始状态下，人物以本人的认知、动机和共同决策机制选择行动。禁止脚本代选、预写路线或答案、填脑、强制错误、注入状态、裁判反馈授知，以及跳过真实执行。checker 观测不计入人物游戏钟且不得改变调度。原始证据仅新增到新目录，不覆盖，保留原始字节、全量参数和指纹。

## 事件合同

| 字段 | 含义 |
| --- | --- |
| `run_id / event_id / parent_ids` | 运行身份、唯一事件及显式因果前驱 |
| `source_id / source_sequence` | 生产源身份及该源单调序列；不靠日志行号推断跨源因果 |
| `game_time / game_time_unit` | 正式游戏时钟与单位 |
| `real_monotonic_time / real_time_unit` | 独立现实单调计时与单位 |
| `actor_id / mind_version / authority_version` | 主体、本人体版本及相关权威版本 |
| `event_type / phase` | 类型，显式区分 proposal、prediction、commit、observation、adoption、answer acceptance |
| `observation_refs / axiom_refs / hypothesis_refs` | 本人可知来源、公理及假说身份/版本 |
| `action_refs / cost_quote_refs / receipt_refs / session_refs` | 动作、代价单、实际执行回执与会话引用 |
| `payload_hash / producer_version` | 原始载荷及产生机制指纹 |

因果图不可变；更正新建事件引用旧事件，不覆盖历史。prediction 不冒充 observation，proposal 不冒充 commit，候选计算不冒充实际提交或正式接受。缺记录为 `insufficient_evidence`，不补猜；范围内未发生为 `not_observed`，与已观察矛盾 `violated` 分开。状态仅使用 `supported / violated / not_observed / insufficient_evidence / invalid_run`，逐项附来源和理由，不用数量或通过率评价效果。

## 主因果契约

检查以下事件的偏序和依赖：开场表达；字面为真且 speaker 本人有依据；听者分别解析并采纳或保留；首答被正式接受（`t0`）；下一回答因真实缺口被正式驳回；主体认真采用错误假说；正式调查取得新证据；修正论证；实际提交并接受；真实交流中各主体分别撤销旧暗示。

只检查实际偏序，不锁定答案或强制失败来制造顺序。内部穷举候选不是认真采用假说，须有 adoption、来源、动机和后续选择证据。真实缺口由正式提交及裁判回执确认。若自然一次正确，如实记为不符合该叙事要求，不干预决策制造错误。

拒答、ready、submit、accepted 分别记录首次时刻和证据；`t_ready - t0 <= 60` 游戏分钟，现实耗时另记，不按倍速推算。充分性不能只凭置信度，须有完整来源、处理关键竞争解释且无循环假设；ready 不能代替真实提交和接受。缺失时刻保留未知。

固定开场历史时长、当前身体累计居留时长、可回忆时长为三个独立带锚点命题，保留指代、单位和来源；身体累计居留不能替换为醒着时长。历史命题不能因后续时间经过自动变假；撤销 P 不推出 not P。话语合法性不能靠私自换指代。真话是硬门禁，不是可由收益抵消的负分。speaker 预测别人只使用本人持有的 model，不读他人实际脑内。具体时长阈值、回答绑定及判定实例待 `.story` 对接，本文不预置答案。

本阶段优先结构化话语语义 AST：字面命题、指代、作用域、时间锚点、预设与暗示分列，映射 `.story` 表面文案，人物解析策略在 DV 内实现。无需以任意自然语言理解为落地前提；当前并未实现自然语言解析器。未认可的新词义或偷偷指定的说话者私义不能用于真话判定。

拟议结构化语义数据如下，仅为设计，尚未实现：

| 字段 | 合同 |
| --- | --- |
| `utterance_id` | 话语事件的稳定身份 |
| `surface_ref` | `.story` 表面文案引用，不在代码中复制文案 |
| `literal_ast` | 字面命题的结构化语义 |
| `referents / scope / time_anchor` | 指代绑定、作用域和时间锚点 |
| `presuppositions` | 与字面命题分列的预设 |
| `implicature_candidates` | 可推测暗示候选及来源，不等于听者已采纳解释 |

听者实际 parse 与 speaker 对该 parse 的预测为独立事件，分别引用本人版本及来源。听者不能把 oracle 的 intended_implicature 当作自身解析；speaker 预测也不能替代听者实际解释或采纳。

回答判定将 `literal_truth`（字面命题相对真实状态）、`epistemic_support`（本人可知依据）、`causal_completeness`（覆盖所问因果并排除关键误导）三轴独立记录，并另列正式 `answer_acceptance`。正式接受不是字面真理裁判；字面正确但暗含错误解释不能混作说谎，须分别判其解释缺口及支持链。

sharing 分别记录各主体的可闻性、接收、解析、理由、采纳/保留和信任更新，不广播写脑。重复证言保留共同来源及依赖，不当作独立来源。

## 动作代价边界

拟议协议（伪 DV，未实现）：

```text
quote(action, args, worldrev, mindrev, costmodelhash) -> quote_id
admit(quote_id, balances, concurrent_reservations) -> reservation_or_rejection
execute(reservation_id) -> execution_receipt
settle(quote_id, reservation_id, execution_receipt) -> settlement
```

所有真实动作包括正式回答与交流都经过此边界，无绕路提交。各维允许明确上下界；admit 核对余额与并发预留，版本过期重新报价。拒绝、失败、中断收费规则预先固定；成本不能为负或非有限；退款仅依明确规则且不能套利。报价、预留、回执与结算相互引用。

游戏时间、计算预算、效用、体力分开，不互相替代；零时间动作仍受计算终止预算约束，不修改现有读不走钟规定。无明确范围配置不能宣告合规，不凭空造数。

## 补充程序

`compare-capability.dv` 使用相同初始正式开局，仅改变已授权能力配置，其余冻结；不做运行时脑内手术。记录各配置真实理由、动作和结果；没有差异则如实记录能力必要性未得到支持。

能力对照的目标现象是：无该能力时字面成立但存在解释缺口，有该能力时主体实际补齐该缺口。此目标须由三轴记录及正式行为支持；未自然出现时如实记录未观察到，不能强制错误、改变裁判或代选动作制造差异。

`check-evidence-boundary.dv` 使用认可初始变体或正式动作改变证据到达、听觉范围等，不删除已知，不替人物选择。核对观察、传播及采纳链，重复证言不作为独立来源。

`sweep-parameters.dv` 预先冻结可调维度/批准范围、基线、邻域、交互和预留集；范围未批准不跑，不挑最佳 seed 掩盖跑偏。随机源按实体/过程身份区分或记录完整消费轨迹，避免分支改变随机消费混淆。语义、公理、代价上限冻结，硬约束优先；只有因果成立才比较延迟/成本，报告敏感边界，无样本依据不声称统计显著。全量参数和原始轨迹可复现。

## 当前接口缺口

以下是已有编译 DV 库的边界，不是拟议验证器能力。路径相对 `language_core/`；旧 Kernel/WorldCore 的 Prove、Plan、Prepare/Commit 不能冒充编译 DV 正式链。

| 当前位置 | 已有事实与缺口 |
| --- | --- |
| `core/80-inspection.dv:4,19,28` | 会话、检查点和时间查询，不是正式调度；检查器无完整论证/代价账本查询 |
| `core/10-laws.dv:71,88` | dv_execute 核验时间/版本/公理不倒退，未扣预算，可传任意不倒退时刻 |
| `core/50-response.dv:87` | effort 仅评分，未接动作代价结算 |
| `core/70-reasoning-cost.dv:2,35,71,99` | 外部传入证明校验，无自动搜索；basis 仅可达 node ID，缺完整步骤快照，未自动入 history |
| `core/20-observation.dv:33,42,57,102` | 有 source/trust/dependency、投映和更新；bindings/grants 为可信外部输入，缺 `.story` 适配 |
| `compiler/compiler.dv` | 解析 DV 列表，不是自然语言或 `.story` 解析器 |

## 实现顺序与交付限制

后续按数据分域与 manifest → 动作代价执行 → `.story` 接入/调度 → 语义与心理推演来源记录 → 只读判定 → 三个补充程序推进。现有接口缺口需实现后保留具体差异，不能靠引用旧链路补齐。

分别记录工程事实、机制因果证据、人物合理性与游戏体验。正式验收仍受仓库触发和证据规则约束；本文不启动流程回归、独立复评或任何验收运行。
