# D01-A：会话注册表与主体运行时消歧候选

结论：现有 Core 足以组合“一个 Essence、多个现实表现、各主体私有投影、显式嵌入名词、既有动作执行”。它尚不能通过现行 world/v1 会话桥提供所要求的 Facet/Referent/Interpretation 完整合同。不能把作者全局别名表当作角色理解，也不能把一次正常构建当作版本一致性、历史重建或存档迁移通过。

本轮只提出候选，没有实施任何生产、Core、设计规范、预期或 S18 改动。基线经实际检查为 `7489db56a714724eb7cb735958dd879dc662d238`，分支为 `codex/solution-review-audit-20261010`。开始时已存在的修改未覆盖。模式为 **DETECTION_ONLY**：工具没有硬 ACL，隔离依靠授权路径与写入范围；不能声称强沙箱。没有读取项目设计文件、完整剧情、另一 D01 方案、历史试玩或答案预期。

## 证据与实验边界

- `PREDICTIONS.md` 与 `REFERENCE-PREDICTION.md` 在各自实测前封存预测。
- `core/ExistingCore.csproj` 链接原 Core 工程实际纳入的 `.cs` 源（同样排除两个 regression Program），未复制修改引擎；四个既有 Shared/Story 源文件链接入中性驱动。原 Hall 两文件随实际 Core 编译边界链接，但没有读取其内容或调用其功能。
- `experiment/Program.cs`、`reference-probe/Program.cs` 只搭建中性实体、调用真实 `AstDecoder`、`Ast.Evaluate`、`DecisionSystem`、`SimulationSession` 和 `WorldSession`；没有自定义求值器、Effect 执行器或 AST 子类。
- `core-results.json`、`reference-results.json` 为真实进程输出；`logs/` 原始 stdout/stderr 保留字节，局部 `.gitattributes` 使用 `-text -diff`。
- `compiler-results.json` 与 `compiler-alias-complete.json` 运行实际 `world_script.Compiler`，不是自制解析器。输入以 artifact 虚拟路径传入内存并完整留在 JSON 中，路径对应的是源定位标签，无需假称存在源文件。
- `execution-evidence.json` 记录工具版本、命令、退出码和链接/审阅源码前后 SHA256；所有这些生产源码 hash 相同。两个隔离构建均成功，主构建 0 警告、0 错误；没有运行官方 Core/Godot solution 全量构建或游戏界面。

可重建命令（在仓库根目录）：`python artifacts/story-hardcode-marking/execution-20261010/D01A/run_probes.py`，另有 `run_reference_probe.py` 与 `complete_alias_probe.py`。当前结果已封存；后续复测应使用新的输出路径/轮次，不能覆盖本轮原始证据。bin/obj 是 D01A 内的可重建输出，不是生产资产。

## 同一判别集的实际观察

| 判别 | 实际结果 | 结论与范围 |
|---|---|---|
| P01：隐藏现实更新 | 会话候选数 `1 → 1`；未感知的 mental signal 仍为 0，LastObservation 不变；真实动作成功且 counter=1；正式 VerifiedPerception 后 signal=1、候选数=0。 | 本样本的适用判断没有读取隐藏新现实。身体/目标物理可行性仍由激活时的 Core 处理。 |
| P04：一词、两个可执行投影 | 一个 Essence 的两个现实表现都称“件”；私有引用不同；Core 返回两条槽位绑定候选。裸名字报歧义；各自嵌入执行均 Completed，客观 counter 累计 2。 | Core 的多投影/显式引用组合可用，不能擅自把两条候选取首条。 |
| P04：宿主桥与检查侧面 | 同一实体第二表现加入后，WorldSession VerifiedPerception 抛 `This binding expects one reality representation per script entity.`；直接 Core 检查第二 mental，实际只更新第一 mental 的 LastObservation。 | 会话桥阻断多 Facet；既有 Inspect 的含义实际落到“客观实体首个表现”，没有保留所选 Facet。 |
| P05：求值中认知更新 | 旧态完整求值=False，新态完整求值=False；6 次并发观察中第 2 次合取=True。第一叶只在旧值1成立，最后叶只在新值2成立，中间25万常量叶。 | 已实际检出公开 Ast.Evaluate 单次读取混合认知版本。其余5次 False 只说明调度窗口没落中，不抵消正例；测试不推断现实游戏已有竞态。 |
| N02：不同主体同中文词 | 甲、乙各自世界中的“件”绑定不同 private Projection/Essence，使用相同文本各自完成动作；两客观 counter 均为1。跨世界嵌入被拒。 | 私有世界内的名字解析隔离可用；作者 alias→ID 与该行为不同。 |
| N01/C03：归一化 | NFC `é` 和 NFD `e+◌́` 在 Core 各绑定不同投影，编译器也允许两个实体。 | 当前按精确字符串区分，没有共享 Unicode 归一化合同。 |
| H01：历史认知 | 旧事件 Tags=1、事件 Existence=True 保留；旧事件 Target 已变“新称”、Concept、False；Forget 后 History 从2变0。 | 历史并非完整、可重建的认知版本，且遗忘会清理相关历史。 |
| X01：引用/能力失败 | 删除目标后激活为 MissingObject；主体持有但无客观绑定的投影也是 MissingObject；有目标/工具但无 LockProperty 为 UnsupportedOperation；未知词/歧义/未知语法均是带不同消息的 AstParseException。 | 有部分阶段区别，但没有统一可机器消费的错误合同，绑定缺失与物理已失效被合并。 |
| X02：提交后遗忘目标 | 提交后目标被 Forget，激活时 `Contains(target)=false`，真实 Change 仍 Completed/None、原实体 counter 增1。再观察创建新的 mental；试图将其改绑另一实体被拒，原绑定后续执行仍只改变原实体。 | “认知引用失效”与“客观实体失效”并不等价。当前保留已提交目标的引擎绑定；是否撤销意图是需要批准的策略选择，不能静态判为设计错误。 |
| E01：增减中性实体 | 动态登记32个普通 Object，删除16个；没有新增枚举。物理删除后 reality Existence=False，但注册表仍保留32个条目。后续章节加入一个中性实体时旧 Essence/Soul/mental 引用与历史数均保持。 | 字符串身份和内存连续性可用；物理删除是 tombstone 行为；没有实体枚举需要增加。此结果不证明存档重载。 |

首个 C02 alias 样本少写 option 的必需 label，真实编译器拒绝；该失败保存在原 JSON，不能用它评价 alias 能力。补全中性 label 的 C02b 正常编译，目标“器”及 inspect 参数在 IR 已成为同一个 `chapter:probe:x`。因此 alias 是作者期实体索引，在运行时已经没有等待某个主体解释的中文 Referent。C01 重复中文名被编译器拒绝；C04 不同章节重复裸 ID 被全局编译器索引拒绝；C05 新 `facet` 属性被现行白名单明确拒绝。

## 源码依据与不能混同的四层

| 层 | 当前能用的基础 | 明确缺口与准确位置 |
|---|---|---|
| Entity：客观身份 | `Model.cs:33–49` 的 Essence 是引用身份，TrueName 不是稳定 ID；`CoreScriptWorld.cs:27–35` 是字符串登记；`WorldSession.cs:72–73,178–179,290–307` 提供声明身份和命名空间。`WorldRuntime.cs:34–40` 拒绝静默换绑。 | 核心身份本身没有持久 ID、代际或存档恢复合同。编译器 `world_script.py:481–483` 不许 ID 含冒号，却以裸 ID 在 `616–618,699` 跨章查重；运行时命名空间能力不能反推作者期同名 ID 已可用。 |
| Facet：同一客观身份的多个表现 | `Model.cs:87–91` 的 SurfaceProperty；`WorldRuntime.cs:20–30,43,56–72,76–129` 的 Project/Sync/Observe。不同现实表现是不同引用，认知映射按 `(Soul,Reality)` 保存。 | 会话 `WorldSession.cs:115` 与 `WorldBehavior.cs:83–88` 强制单现实表现；`WorldBehavior.cs:54,125,272–276` 用 EntityId→单mental。`KnownProjection` (`WorldRuntime.cs:45–46`) 返回第一个已知表现；Inspect (`ActionRuntime.cs:244–246`) 再选首表现。 |
| Referent：中文词/作者提及 | `AstDecoder.cs:127–146` 支持世界内名称或精确嵌入。未知名与歧义均明确报错，不自动查现实。 | `world_script.py:149–150,560–579,619–643,702–704` 只有全局唯一 alias→EntityId；`WorldEntityDefinition` (`WorldSession.cs:12–19`) 没有 aliases 字段。`story_rules.py:46–59` 按传入词典分别映射人物/地点/物件，`89–110` 编译固定短语；这里没有主体运行时词义消歧。 |
| Interpretation：某主体对某词的认知版本结果 | 私有 Soul.World (`Model.cs:51–54`)，引用/槽 (`Ast.cs:9–13,52–63`)，跨世界检查 (`Model.cs:195–206`) 已存在。认知/现实分离 (`WorldRuntime.cs:55–56`) 与 Infer/Corrupt (`Cognition.cs:30–44`) 可组合。 | `Ast.Evaluate` (`Ast.cs:69–82`) 逐叶读取活投影，没有一致版本帧。`ObservationEvent` (`Model.cs:163–165`) 缺历史Name/Kind/Parent/DeniedRelations/Rules/Procedure快照；Commit (`213–241`) 原地改引用，Forget (`214–226`) 清历史。 |

补充边界：`Reasoner.cs:74,107,121` 仍按名字找首个/任一投影，无法承担这四层身份合同；本轮只作静态结论，没有用它伪证多侧面推理通过。`WorldBehavior.cs:60–61,163–171` 的 Version 是全会话 choice token 失效号，Offer 也增加它；它不是主体 KnowledgeRevision。WorldSession 自身调用以 `_gate` 串行 (`WorldSession.cs:76,155`，`WorldBehavior.cs:73`)；公开 Runtime/Simulation/World (`WorldSession.cs:78–80`) 和直接 Core 输入仍在其保证之外。

执行边界：Submit 在 `SimulationSession.cs:38–41` 验证私有引用；激活在 `ActionRuntime.cs:14–17` 检查 Actor membership/存在/控制。目标解析 `56–59` 跟随引擎绑定，不再次检查目标是否仍在认知集合；`WorldRuntime.cs:47,49–50,132` 分别规定客观失效、移除与 mental membership。不能简单将“遗忘时强制撤销所有已形成动作”写成修复而越过设计决策。

## 候选合同（只供审批）

建议把上述四层分开登记，而不是增加剧情实体枚举。候选结构在 `candidate-contract.json`；尚未实现，也不是已批准的作者语法。

1. **Entity**：`EntityId` 使用已完全限定的 ordinal ID；引擎私有注册表保存现有 Essence 引用。展示名称改变不改身份；同会话不重复使用已移除 ID、不静默换绑。若未来允许复用 ID，再明确 Generation；本轮没有需要引入复用。
2. **Facet**：`FacetId → EntityId → Essence` 与 `FacetId → reality Projection` 只在引擎侧；`(SubjectId,FacetId) → mental Projection` 保存各自连续引用。多个 Facet 指向同一 Essence，不自动让主体知道它们同一身份。既有 SurfaceProperty/Project/Observe 直接复用。词汇合并不能偷偷把两个 Facet 合并为一个投影。
3. **Referent**：保留 `ReferentId、RawLexeme、CanonicalLexeme、词汇策略版本、源位置`，可选显式 FacetId。建议仅对词汇使用 NFC，保留原文，不对稳定 ID 归一化，也不自行加入大小写/全半角/简繁转换。这是待批准策略；当前实现是精确字符串。归一化冲突保存候选集，不能偷取首项或自动认为同实体。
4. **Interpretation**：以 `SubjectId、ReferentId、KnowledgeRevision、上下文` 生成不可变候选 mental handle 集。零个已认识候选=`Unrecognized`，多个=`Ambiguous`，唯一候选才成为现有 `Noun.Reference`。上下文筛选只读该主体的认知帧，可利用现有 Kind/Tag/Relation/RuleSlot；不得用 WorldRuntime 的客观存在/能力来替主体消歧。解释结果不能公开客观 Essence 绑定或另一个主体的投影。

一个词已解析为唯一 mental 与该 mental 上某个事实为 Truth.Unknown 是两个层次。词汇不存在、未认识、多个解释、解释版本过期、客观对象激活时不存在、动作能力未支持，也应分别返回类型化结果。现行异常消息和 ActionFailure 可作为后两阶段的底层来源，但不能拿所有失败统一当 Unknown。

## 最小新增项与审批门槛

| 项 | 可复用部分 | 需要批准的最小新增合同 |
|---|---|---|
| A01：作者/宿主四层登记 | Essence、Project、SurfaceProperty、Observe、嵌入 Noun、既有状态动作。基本组合无需增加 Core 因果语义。 | Facet/Referent 声明与版本化 IR、主体候选索引、中文词冲突/归一化规则；替换现行单表现桥是生产合同变更。 |
| A02：一致认知读取 | 会话自身 `_gate` 可提供范围较窄的串行输入。 | 若要求所有公开 Core 调用在更新中保持一致：每次认知批提交的单调版本号，并增加不可变读取帧供 Ast.Evaluate 使用，或让读/写共用边界。History.Count/菜单 Version 都不能代替。没有自定义求值器原型证明“已通过”。 |
| A03：Facet 执行连续性 | 物理 Change/Take 等已经按共同 Essence 生效。 | 先决定 Inspect 指选中的 Facet 还是实体默认表现；若选择前者，需要准确保存/解析 originating Facet，而不是首表现。该行为改变需批准。 |
| A04：历史/存档 | 当前 History 的部分不可变字段和内存跨章引用可以保留。 | 完整历史 snapshot 或完整带版本 delta、稳定 mental handles、Forget 保留策略；存档重建、AST关联修复、调度恢复必须有真实 codec/旧档样本再测，不能默认填零或重建故事常量。 |
| A05：诊断/意图生命周期 | parse异常、MissingObject/UnsupportedOperation 与菜单重新检查可包成宿主诊断。 | 未知词/未认识/歧义/解释过期/实体失效/能力缺失的类型合同；若要提交后目标 Forget 时撤销或激活重验 revision，会改变 Core 当前行为，需要单独批准。 |
| A06：能力协商 | 各层现有白名单与 world/v1 format 校验。 | 机器可读 required-capabilities/version、加载前明确拒绝未支持合同与审批记录。F01 是审计输出，不是引擎实际能力协商。 |

不建议把本候选直接实现成新引擎、公开 Essence 给角色、按客观真相自动解释，或通过新增章节专用类型补洞。实验没有证明这些行为被要求，且无设计权限据此作取舍。

## 缺口与验收结论

F01-C002、C004 与本次多投影/名词实测一致；F01-C025 标记实际文件 codec/重载绑定未验证，C026 没有已核验的能力/审批版本合同。D01-A 没有历史存档样本，**历史存档迁移明确 NOT_VERIFIED**；“后续章节复用旧 Soul/Essence”只证明在同进程中的连续会话。

构建：隔离链接现有源码构建通过。机制：本轮中性判别已完成，P05混读、会话多 Facet 阻断、Inspect首表现、历史不完整、意图遗忘保留均有实际证据；这些是能力边界，尚无修复或完整候选合同通过结论。人物行为合理性：未验证（未读设计）；游玩体验：未验证。一级、二级、三级验收均未执行，本轮实验不得冒充其中任一级。

最终范围：所有成果为 D01A 新文件；没有生产提交、推送或架构规范修改。父代理负责和设计核对、整合审批及最终统一提交。
