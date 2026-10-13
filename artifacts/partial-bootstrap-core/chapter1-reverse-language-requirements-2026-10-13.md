# 第一章代码反推 DV 语言需求

第一章要求一门能表达本人信息边界、可撤销解释、付费因果重演、竞争论证、多步行动和真实反馈的语言。已有切片/理解/分布方案覆盖了其中一部分；仅用普通规则推知加动作softmax不能承接完整执行链。

本文从现役Godot/C#与隔离DV第一章源码反推语言对象、运算和标准库。

## 实际代码范围

现役入口是 `godot_csharp/scenes/Awakening/Script/Chapter1Session.cs` 及其 Knowledge、Speech、Strategies、Replay、Save、SessionFlows等部分，领域核心在 `csharp/`；它依赖现役 `.story` 编译物。隔离DV入口是 `verification/session/30-runner.dv:dvs_run`，经原生DV场景导出输入，依赖core与session的推演/规划/话语/模型论证；它没有Godot几何/碰撞/呈现，不能替代现役第一章。

主要源码指纹见同目录 `chapter1-reverse-source-index-2026-10-13.json`。这是只读审视时的来源索引，不是重新建立测试保护基线。

## 实际依赖关系

```text
章节内容与初始化
  → 权威世界 / 各本人认识 / 公开方法 / 规则与成本目录
  → 实际观察与已闻问题
  → 本人推知 / 暗示解释 / 假说采用
  → 已付费模型轨迹 / 特征与本人观察比较 / 竞争解释排除
  → 可撤销论证 / 动态答案和公共提案
  → 相关目标与信息价值 / 完整层多步规划 / 首步选择
  → 报价与资源预留 / 权威执行 / 回执
  → 本人或实际接收者的认识更新
  → 回答协议 / 正式接受或拒绝 / 章节阶段转移
```

现役宿主另外提供呈现/输入回执、动作生命周期、现场可见/可达、异步等待、行为反馈和保存恢复。DV运行器是顺序轮转的逻辑会话，不自动继承这些事实。

## 从源码反推的必要能力

| 需求 | 实际源码证据 | 语言与标准库应承担什么 |
| --- | --- | --- |
| 权威身份、本地引用、词项和阶段域分开 | csharp/Model.cs、CognitionSnapshot.cs；core/20-observation.dv 的 dv_project_value、dv_perceive | 原生引用/视界类型及投影操作；名称不等于实体，Ref不自动授予权威访问 |
| 问题是否已想到或实际听过 | csharp/Reasoner.cs:95 起的conceived/default；session/35-model-argument.dv:11,22 的公开问题收闻 | ConceptState、QuestionRef及形成来源；尚未想到不能折算为已想到但Unknown，默认答案不是一般先验 |
| 同一材料的本人解释与采纳 | session/20-speech.dv:106,124；场景pragmatics/person_policy | Interpretation、Adoption、作用域桥/撤回依赖；共用cue不直接携带听者结论 |
| 来源相关性与重复收闻 | core/20-observation.dv:65,108,110,144 | Evidence及DependencyGroup；同共同源材料不按独立证据累加，信度/支持份额/认识置信分列 |
| 感知时间和命题有效范围 | core/20-observation.dv 的time字段；session/25-simulation.dv 的start/at/snapshots | TemporalClaim、time anchor及历史/当前域；当前观察ABI主要使用单一time，不能声称已实现通用双时间系统 |
| 字面、预设、暗示和表达分开 | session/20-speech.dv:9,23,40,124；现役Chapter1Speech.cs:41起 | Speech结构和判断种类；表面词串不自动决定世界真值，语气选择不改字面命题 |
| 实际传播与本人听者模型预测分开 | session/20-speech.dv:28,174,224,345 | Receipt/Percept与ModeledMind；模型来源/假设/预算，未闻者不更新，预测不写对方脑内 |
| 反事实或历史重演不复制已知结果 | session/25-simulation.dv:29,39,114；场景history中的时序法则 | Hypothesis、SimulationTrace、PredictionCoverage、明确输出隔离；不得把实际观察值当成已预测成功 |
| 未知条件使可能输出保持未知 | session/25-simulation.dv:29及114后的withheld逻辑 | Unknown propagation/conditional result；不是仅减少候选评分，不随意选false支路 |
| 特征比较与完整竞争空间 | session/25-simulation.dv:76,95,467；chapter1-history.dv:263起的域/约束 | FeatureComparison、CounterEvidence、CandidateSpace、CoverageCertificate；Unknown不能剪枝，作者真值不能作为排序键 |
| 解释论证与直接观察分开 | core/95-model-argument.dv；session/35-model-argument.dv:107,295 | EstimatedArgument、公共ClaimSlot、观察/模型来源和竞争事件；置信不足1、付费与本人来源可核对，不写成客观证明 |
| 论证有效期及输入改变后的撤回 | session/35-model-argument.dv:24,29,45,236 | DependencyFingerprint、Revision、Invalidation；本人自己的论证写入不误判为外来污染，支持变化后重新核验 |
| 公开提案与听者独立采纳 | session/20-speech.dv:57,98,290；35-model-argument.dv:356 | PublicProposal、AcceptanceDecision、作用域重审；公共内容可保留而不采纳，不能广播写脑 |
| 多步行动价值回传 | session/15-action-planning.dv:134,174,203 | PlanResult、ImmediateScore、ReturnedScore、路径与首步关系；搜索/剪枝由普通DV实现，语法原生区分对象和预算 |
| 信息价值不等于预知结果 | session/15-action-planning.dv:25,53,61,123,134 | ObservationScope、Goal/Probe、ExpectedInformation；方法可检视范围与实际检视结果分型 |
| 可行候选、重复尝试与实际选择分开 | session/15-action-planning.dv:86,98,105,203；csharp/SubjectiveDecision.cs | Eligibility、AttemptContext、ActionAdmission、NoSelection；有候选但不选与无候选分列 |
| 实际反馈改变后续行为 | csharp/SubjectiveDecision.cs:10,22,86,181；Chapter1Strategies.cs:27,125,139 | ActionExperience及Outcome；尝试/成功/失败/无推进/中断有不同回执，学习策略为DV库而非人物硬编码 |
| 多维资源而非单个“预算” | core/90-action-cost.dv:2,21,28,45；session/30-runner.dv:58起 | CostVector、Quote、Reservation、Settlement与CognitiveReceipt；时间/计算/体力/模型/规划分别核验，不互换 |
| 回答成立、本人有据、完整、正式接受分开 | session/30-runner.dv:28；35-model-argument.dv:295；checks/08-formal.dv | Submission、AnswerJudgement、ProtocolReceipt；正式规则由内容定义，检查器不反馈答案给主体 |
| 调度、动作阶段、等待、取消与租约 | csharp/SimulationSession.cs:27,65,74,117,182；现役Chapter1Session.cs | ActionLifecycle、Scheduler、Continuation、Lease；属于DV系统库/宿主协议，不能用顺序runner冒称完整迁移 |
| 实际呈现及玩家获得信息的边界 | Chapter1Knowledge.cs:34起；Chapter1Session.cs:320及Reply | PresentationReceipt、VisibleOffer、输入关联ID；当前ObserveShown在输出事件生成时调用，未单独证明真实像素已画出 |
| 保存恢复和稳定事件身份 | Chapter1Save.cs:14,20,71；CognitionSnapshot.cs、TypedEvents.cs | StableId、ClosedSnapshot、PersistenceCodec；恢复不重新观察/推演，未支持保存点明确拒绝 |

行号定位到本轮所读的函数体，两条实现链的范围分别说明。

## 需要修正上一版方案的地方

### 不行动也是结果

`dvp_plan` 最后以 `best_score > 0` 决定是否返回动作，最佳路径分数非正时返回false。先前“合法候选→argmax分布→抽样”的简化会强迫选择。需要增加明确ActionAdmissionPolicy和NoSelection：先保存全部可评价候选/分数，再决定哪些进入选择支持集；无候选、预算停止、时间阻塞和有候选但无正收益不合并。

这里的正分门槛是当前规划器的事实，不是全语言默认。`dv_respond`取最高分没有同一正分门槛；旧SpeechPolicy按分数及言语行为枚举顺序排序，再过滤已有表达文本。因此不能统一套用“所有选择同门槛/同平分规则”。

### 概率必须绑定到可执行首步

`dvp_plan`比较路径累计分，保存selected_path，返回该路径的第一个动作；它还限制完整层预算、预测循环和时间，前沿按得分/耗时Pareto保留。这里的“动作分数”可能是路径回传分。

需要先形成一个首步动作到已比较路径/回传分/依据的映射，再由指定策略生成ActionDistribution。不能把同一首步下搜索出的多条路径当多张抽签票，也不能按路径数自动增加概率。具体聚合采用旧算法对照或显式新策略，另记同条件与机制变化。第一轮保持既有选择，新增分布不改变执行。

### 历史重演是更大的语义单元

当前模型从本人假说构建私有状态，先屏蔽待预测字段，再应用本人已采纳预测规律，保留每阶段snapshot/coverage，将结果与实际本人观察比较。只有实际轨迹支持的槽位、有效来源和竞争排除才能进入估计论证。

这要求可读地表达“假设→模型输入→时序推演→覆盖→观察比较→竞争论证”，普通`infer`或一个`model`字段集合不足以说明全部合同。算法仍在DV库，不把完整重演器塞成不可检查的原生solver。

### 记忆是多个不同对象

第一章代码使用现时物体切片、话语原句、暗示解释、已采纳规则、历史假说、付费预测、论证输入和行动经历。把它们都放入StoredSlice会丢掉“能用于何种推演/可否撤回/是否已经发生”的区别。

StoredSlice可作为一种材料表示，但需要明确种类和来源；Interpretation、Argument、ActionExperience、PredictionTrace有各自合同，不能以同一“已知列表”处理。WorkingSlice只控制实际取用，不能从它自动推出一个新的心理检索模型。

### 呈现与调度不能从逻辑会话推导已完成

现役会话有presentation_id、Ack、等待作业与实际字段页显示范围，旧Core有CTB和生命周期。DV runner依cursor轮转、共享时钟和逻辑回执执行。这些不是同一个调度/呈现合同。

需要完整保持宿主输入、碰撞、可见信息和呈现证据的边界；当前静态源码能说明输出/观察调用位置，不能说明当轮UI实际画了什么。历史保存/词项消歧/行动反馈也必须单独接线，不能以DV会话推进代替。

## 语言原生机制与库内容的分工

原生机制包括名义类型与信息域、词法主体上下文、来源与版本、阶段、操作能力、预算收据，以及判断、评分和概率对象。

标准DV库承载有限候选空间、证据聚合、解释桥匹配、来源依赖失效、付费历史重演、竞争排除、规划/剪枝、行动学习、调度、资源结算、保存编解码和报告。各库通过函数签名和结果类型连接领域层。

内容DV声明人物初始认识、目标、方法、世界与通道、公理实例、话语、规则/策略参数、正式答案协议、公共提案和章节阶段。现有topic/stage的专案分支应归到对应内容声明。

宿主负责真实空间、输入、画面、平台I/O和最低原子提交适配。共享调度和可达性算法最终由DV系统库承接。

## 实现顺序

先接通引用、命题、效果、来源、规则实例与问题认识；随后接入模型轨迹、竞争论证、多步规划、首步选择和真实执行。现有权重、门槛、平分和费用规则由对应DV库表达。

语言方案已补充planning、admission和NoSelection；第一章使用的协议类型纳入SYNTAX。当前阶段为源码分析。
