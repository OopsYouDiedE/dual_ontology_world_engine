# D04：反事实、预算与历史认知诊断

状态：`DETECTION_ONLY`。基线 HEAD：`7489db56a714724eb7cb735958dd879dc662d238`。未修改 Core、Godot 生产代码、规范、设计、预期或 S18。新增文件全部位于本目录；构建产物位于本目录 `build/` 并忽略入库。未读取项目设计文件、完整 `.story`、黄金预期或既往玩家记录。

**结论：通用认知演算的已测边界没有读隐藏真值，也没有写回源世界；第二章的主体重演入口存在现实真值输入；搜索返回合同不能明确区分耗尽与不可行动；现有 History 不能还原完整历史认知版本。** 后三项为缺口诊断，未据此制定或批准产品预期。

## 证据范围

主任务指定读取 `Reasoner.cs`、`Model.cs`、`WorldRuntime.cs`、`Cognition.cs`、第二章 `Case.cs`、`NightSnapshot.cs` 和第一章 `DetectivePlanning.cs`。为构造中性机制反例，先向主任务报告扩域，获明确批准后读取 `Ast.cs` 的公开 AST 定义及 `DecisionSystem.cs` 的机制；另读 Core 工程文件以建立隔离工程。遵守子目录 AGENTS 的工作流程约束，未打开它们链接的设计文件。

实验直接引用既有 Core 工程，并原样链接 `DetectivePlanning.cs`。`MinimalPlannerAdapters.cs` 只提供中性预测数据和机械必需类型，六字段外形由既有接口要求；未装载本作预测表、内容会话或 Godot。它验证规划类本身，不能代表完整宿主调用流程。

构建和运行日志：`build-02.raw`、`probe-run-02.raw`。两者由 `subprocess` 以二进制重定向保存原生 stdout/stderr，局部 `.gitattributes` 关闭换行转换和文本差异。初次运行的结果另存 `probe-results-run-01.json`；字节保真复核结果另存 `probe-results-run-02.json`；当前结果为 `probe-results.json`。`run-manifest.json` 记录指令、基线及全部授权机制源码的前后 SHA-256；源码前后相同。

## 1. 是否读取真值

### 通用 Reasoner 与 DecisionSystem

`csharp/Reasoner.cs:68` 的 `Assess(ProjectionWorld, CaseModel)` 只读传入主体的投影、标签、命题与来源；`csharp/DecisionSystem.cs:14` 只读同一内部世界的候选、条件、规则与请求。它们不接收 `WorldRuntime`、`Essence` 或现实快照。

中性双世界 A/B 各有一个对象，唯一不同是不可观察的 `Hidden` 状态分别为 0、1；可见 `Signal=7`、主体历史和规则完全相同。两世界历史归一化哈希均为 `7F7E8B92D9C9C058B72538ED0277552C6346C25667C9A27810AE389D0592BBDB`。固定共有模型具有两种都预测 `Signal=7` 的假设。

实测：两边 Reasoner 均保留候选 `[0,1]`，置信分布 `[0.5,0.5]`；两边 DecisionSystem 均选择 `Inspect:surface`，分数 3。对同一中性数据、种子和可见历史，新建的两个 DetectivePlanner 也返回完全相同的规划。此结果支持已测接口的隐藏真值不干扰性。

但是 `CaseModel` 的数据来源没有合同约束。另一个中性反例故意由调用者根据 `Hidden` 构造只剩一种假设的模型：主体历史不变，输出随模型内真值由 `[1,0]` 变为 `[0,1]`。这是调用者数据泄漏演示，不是声称 Reasoner 自身读取了 Essence；它说明只看方法签名不足以保证上游模型不带真值。

### DetectivePlanner

`DetectivePlanning.cs:86` 从外部共有预测表装配假设；`Refresh` 从主体标签和已公开进度读取材料；`Transition` 在复制状态上从主体候选采样反馈。已读实现没有向现实对象查询反馈。生产预测表生成来源未在本轮扩域检查，因此没有把实验夹具的来源结论外推到生产数据生成链。

### 第二章 ReplayCase / DetectiveInference

已读源码存在两条明确真值数据路径：

1. `NightSnapshot.cs:30` 的 `CaptureNight` 采集全体人物权威 Body 状态、全体 Things 状态、位置、接受状态和信任；未用侦探的可见集合过滤。`Case.cs:79` 把该现实快照作为 `RestoreNight` 送入假设重建；`NightSnapshot.cs:38` 在推演世界恢复这些字段，然后同步与重新感知。这是完整现实条件输入，不能仅凭“新建世界”视为主体知识隔离。
2. `Case.cs:191` 取得 `CaseActual`，`Case.cs:196` 将每个假设的痕迹与它比较。`Compare` 的 `known` 只决定是否比较一个条目，比较值仍由 `actual.Describe(...)` 提供；没有读取主体持有的该条目版本、值或来源。发生旧观察、误认或主观变换时，“已知索引”不能证明“当前现实值等于主体所知值”。

此外 `Case.cs:188` 的推断缓存 key 只编码已知条目与已查地点集合。相同集合下，证据值、来源、认知版本、规则或快照内容变化不会使该 key 变化。

本轮未加载第二章宿主，因此以上是源码数据流诊断，而非实际案件输出反例。可复核的中性结构反例是：主体只保存 `Signal=7` 的旧观察，已知索引集合不变；A/B 中不可见现实条件不同，却被完整快照带入假设生成，或由调用者改用两边现实值比较候选。无新观察就改变主体结论的输入通道已存在。是否将其中某种现实重演保留为引擎审计工具，需主任务按设计审批。

## 2. 是否写回现实

Core Reasoner、DecisionSystem 和链接原样 DetectivePlanner 的实验均保持源世界权威状态与主体认知哈希不变。DetectivePlanner 的 `State.Copy` / `Transition` 修改副本；`Plan` 会推进自身随机数生成器与搜索状态，它不是完全纯函数，但这不等于写回现实。

另用既有观察接口构造独立中性分支，将分支 `Signal` 改为 99，A/B 源世界和源认知仍保持 `Signal=7`。这是调用者手工构造隔离分支的可行性证明，不是已有统一 Core 反事实接口的证明。

第二章 `ReplayCase` 把状态操作施加于参数 `g` 或返回的重建世界，已读方法内未见直接修改源 Game 的现实状态。但本轮没有读取 `Rebuild` 及全部 Options 回调链，也没有运行完整宿主；**深层引用隔离及所有回调的源世界无写回仍未验证**。`BeforeNight` 中的 `Set/Add/Sync` 是对推演世界现实的操作，应由合同明确区分“假设处境变动”和“仅认知变动”。

## 3. 预算耗尽能否区分

`DetectiveSearchDecision` 只有 Action、Prediction、Iterations、Nodes、MaxDepth、RemainingHypotheses、Confidence、Branches、Motives，没有完成状态、停止原因或预算耗尽字段。`Plan` 按配置迭代数/深度停止；非空候选仍随机选出一个 Prediction。Confidence 来自根候选数量，不是搜索完成性的证明。

中性实验全部保留两种假设，结果如下：

| 情形 | Action | Iterations | Nodes | MaxDepth | 剩余假设 | Confidence |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| 4 次迭代、深度 2 | inspect-neutral | 4 | 3 | 2 | 2 | 0.5 |
| 迭代预算 0 | null | 0 | 1 | 0 | 2 | 0.5 |
| 深度预算 0 | null | 4 | 1 | 0 | 2 | 0.5 |
| 无可用行动 | null | 4 | 1 | 0 | 2 | 0.5 |
| action_budget=0、迭代 4/深度 2 | inspect-neutral | 4 | 3 | 2 | 2 | 0.5 |

“深度预算为零”和“没有可用行动”的完整返回数据相同；下游单看结果不能分辨。ActionBudget 属性存在，但 Plan 本身不消耗或检查它；这只是规划类边界结论，宿主调用者是否另行限制行动次数尚未检查，不能据此断言整个系统无行动预算。

另测空 CaseModel：构造器允许空假设集合，`Reasoner.Assess` 在 `Min` 上抛 `InvalidOperationException: Sequence contains no elements`。它没有类型化的空模型/无候选返回；通用有限表演算也没有搜索预算接口，本轮不把这误称为有限表的预算耗尽。

## 4. 历史认知版本能否重演

不能仅由现有 `ProjectionWorld.History` 完整还原。

- `Model.cs:163` 的 ObservationEvent 保存 Tags/Relations 的不可变快照，但不保存 Name、Kind、Parent、Rules、DeniedRelations、Procedure 或 Forget 操作。Target 与关系 Target 仍是当前可变 Projection 的引用。
- 实测旧事件时间仍为 2、保留标签仍为 7，但通过旧事件 Target 读到的名称已是 `item-current`、类型为 Body、父节点为 `area-b`、当前标签为 9。名字、类型和移动历史不能从该引用恢复。
- 两个中性认知轨迹：时间 2 的规则权重分别为 1、5，时间 3 都改为 9；过去 DecisionSystem 分数分别为 1、5，最终分数均为 9，而保留的 History 结构完全相同，哈希为 `07EB10C48056FEE98FBE757C8A3A249A8222B5F1B9169E0BA7DAEFC764C78947`。所以历史日志不足以决定过去决策；丢失的规则输入无法由算法补回。
- `Model.cs:225` 的 Forget 删除目标及相关关系的历史。实测 History 条数由 4 降为 2，没有遗忘墓碑事件；它不是可审计的追加日志。
- 第二章 NightSnapshot 并不包含完整 ProjectionWorld/History，只保存部分认知与现实字段。RestoreNight 多处仅增加或设置字段，随后重新感知；本轮不能据此认定它恢复了一个精确的历史认知版本。
- DetectivePlanner 私有 Completed、Feedback、VisibleText 及随机数状态不在 ProjectionWorld.History 内；只保存主体观察不足以续演某一旧规划状态。

## 验证结论与限制

独立隔离工程构建成功，0 警告/0 错误，运行退出 0；14 项**检测断言**成立，其中若干断言证明现有缺口存在，不能写成“14 项产品验收通过”。本轮属于机制诊断/自动实验，一级三份全程记录、二级 UI、三级盲玩均未执行，人物行为合理性与游玩体验未验证。未装载设计，因此未对内容或设计合规作结论。

候选合同在 `candidate-contracts.md`，全部待主任务按设计审批；没有据此修改生产代码或建立黄金预期。F01 尚未产出/授权可读交叉目录，暂未完成 F01 映射；不能把缺失项填成已验证。
