# DV 世界语义库

这些模块以编译后的 DV 运行，不调用旧 Core 或解释器。依据现行设计规范的第一章、系统映射及本轮用户补充；通用代码不含人物名、对白或答案，具体内容仍由 `.story` 提供。本轮先实现接口，等待用户提供验收代码方案，未接入现役第一章会话。

## 公理与世界

```text
(axiom 名称 版本 (参数...) 前提 效果列表)
```

DV 前端将声明展开为返回公理数据的 DV 函数，仍检查名称和参数。公理含名称、版本、前提、效果；`dv_instance` 指定实例身份，同一规律用于多个物体须使用不同实例名，禁止静默覆盖。`60-object-laws.dv` 提供通用的开门、自动闭合、落闩、封缝和通道声明。规律不会自行调度，执行事件仍由调用者提交。

| 采纳方式 | 入口 | 语义 |
| --- | --- | --- |
| physical / enforce | dv_execute | 真实条件、原子阶段变更、事件和世界版本 |
| mind / predict | dv_predict | 仅本人状态；返回 prediction 副本；冲突数值变更显式拒绝 (conflicted-state) |
| mind / infer | dv_infer | 本人接受的推知规则，产生依据派生声明，置信度取前提与规则 min，保留反证与冲突 |
| mind / norm | dv_norm | 规范条件，不自动成为物理限制 |

`dv_adopt` 要求依据，物理规律置信度为 1；主体可不采纳、以不同方式/置信度采纳，使用 `dv_reconsider` 修订或 `dv_suspend` 暂停。修订保留前后依据。这借鉴声明与环境的组织方式，不声称已有 Lean 的形式证明内核。

## 物体、状态与通道

`dv_world / dv_object / dv_ref / dv_scoped_ref / dv_slice` 统一表达世界、物体、字段引用和切片。普通物体、门、通道使用同一字段存取。切片注明世界、主体、版本、物体和切面；字段保留 known/value/confidence/time/claims/conflicted/support_share/source_reliability。未知不等于 false 或零。跨主体引用带有主体所属约束，防止未经翻译的隐式指代替换。

门有闭合、支撑、内外闩、自动闭合、缝隙和封缝状态；通道有端点、介质、启用、增益、门控命题及可选频段。种类属于可观察的 identity 切面。写入返回副本；调用者不能使用 `rt_push` 擅自修改库的输入记录。

`dv_transport` 是纯传播计算，领域入口 `dv_transmit` 另外检查规律采纳、模式和前提。`dv_transport_theory` 为声明显式选择衰减/过滤语义。输入路径逐段检查连通性、介质、门控和当前参数，只输出实际保留的片段，不恢复丢失全文。路径搜索、多径叠加、声学标定和第一章宿主接线尚未实现。

## 观察更新与指代投影

`dv_perceive` 根据可信适配器提供的 bindings 与 grants 生成观察；`dv_perceive_changes` 只映射已提交且获准暴露的变化。未感知者保留旧认识。`dv_project_value` 递归类型化投影所有实体引用 (`entity-ref`) 与字段引用 (`ref`)；复合命题中引用的客观标识符若未在本主体 bindings 声明，则整项判定为未知，拒绝泄漏客观 ID，不留未投影的客观名称。

`dv_update_mind` 不接收客观世界，只接收带主体、本地引用、来源、时间、信任和依赖身份的观察。核对认知版本及重复事件；同一相关证据组替换而不累加，历史事实与当前状态分离：仅直接物理感知在更晚时刻到达时产生现时状态覆盖，证词、回想及推知作为独立材料合并与竞争，不因接收时刻较晚而抹杀历史直接观察。`dv_aggregate` 明确区分支持份额 (`support_share`)、来源信度 (`source_reliability`) 与认识置信度 (`epistemic_confidence`)，置信度上限在聚合后得以严格保持。新时刻重新聚合，历史材料完备保留。

## 价值与人物回应

`dv_value_subjective` 只评价本人或预测，`dv_value_objective` 只评价物理世界。`dv_criterion` 声明数值/目标准则，输出分项、未知项和完整性；`dv_value_change` 比较变化。客观价值是实况相对于显式准则的结果，没有内置全知道德分数，评分不授予合法性。

`dv_person_policy` 提供价值取向和代价参数；`dv_accept_policy` 将其连同采纳依据写入本人世界，`dv_respond_adopted` 按已采纳策略回应。检查器的 worldview 同时输出 laws 和 value_policies。`dv_appraise_request` 从本人视图读取信任、亲近、尊重、互惠，效价、唤醒，疲劳、压力、注意力，以及请求的威胁、公平、可控、确定和重要性。未知或冲突不默认为人格特征。

`dv_respond` 分开输出行为意图、价值/关系/付出依据和表达轴。候选可由调用者声明为答应、拒绝、协商、延后、询问等；可行条件必须成立，预测须来自同一认知版本。无候选返回 no-response，平分保持声明顺序。表达轴可表达温和拒绝、不悦但帮助，不由“答应/拒绝”直接决定。`dv_affect_proposal` 提供带回落参数的情感变化提案；选择和预测不自动写回情感或关系，关系变化应根据实际获得的互动观察提交。

本机制受情绪评价理论启发，研究依据及设计要求见 `docs/design-standard.md` 本轮补充。线性权重、回落公式和代价尺度属于未标定的游戏参数，不能宣称心理测量有效或临床模型准确。

## 论证代价

`dv_proof_step / dv_judge_conclusion` 先核对前提、采纳、结论、依赖和预算，再计成本。只有 infer 采纳可导出当前命题；操作规律不能证明效果已经实际发生。

```text
cost = 步骤权重×可达节点 + 证据权重×局部不确定性
     + 假设权重×额外假设 + 冲突权重×假设冲突
reasonableness = 1 / (1 + cost / reference)
```

`dv_cost_policy` 显式提供权重、阈值、参考尺度、假设许可和预算。共享节点只计一次，假设成本须为正。无效推导直接报错；有效结果分列 confidence、hypothetical、reasonable、cost、范围和依据。合理度是相对成本尺度的经济性，不是真值或概率；假说不能因此成为事实或授权动作。

当前只核验已知命题、显式假设和赋值式推知结论；没有实现自动证明搜索、完整一阶逻辑或形式验证。代价模型是 DV 扩展，不是 Lean 原有评分。

## 历史检查命令

`dv_session` 保存世界和深拷贝检查点；`dv_session_commit` 核对版本并记录提交；`dv_session_advance` 仅推进时间。通过 `(rt_write "session.json" (rt_json_stringify session))` 保存。JSON 使用列表/键值对编码，不包含可执行代码。

```powershell
python language_core/tools/build_core.py --out artifacts/partial-bootstrap-core/domain-build-<new-run>
dotnet <build>/inspector/bin/Release/net8.0/Program.dll session.json <subject> <time> worldview
dotnet <build>/inspector/bin/Release/net8.0/Program.dll session.json <subject> <time> state
dotnet <build>/inspector/bin/Release/net8.0/Program.dll session.json <subject> <time> slice <object> <facet>
```

worldview 输出当时仍采纳的全部规律、模式、置信和依据；state 输出本人状态；slice 输出指定切面。结果含请求时刻、实际检查点时刻、时间单位和版本。只选不晚于查询时间的本人检查点；未到达的未来拒绝、缺少历史明确报告。检查器只读，不推进模拟，也不用当前信息推造历史。

构建脚本只执行编译器自编译固定点及库/检查器的 C# 生成构建，不执行领域验收场景。现役 `.story` 装载、模拟调度、输入/UI 接线及用户拟定的验收均待完成。
