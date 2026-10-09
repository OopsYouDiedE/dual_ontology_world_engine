# D01-B 中文词项与主观切面候选合同

状态：**DETECTION_ONLY / CANDIDATE_NOT_APPROVED**。基线 `7489db56a714724eb7cb735958dd879dc662d238`。本代理没有修改生产、Core、AGENTS、设计、黄金记录或 S18；没有读取项目设计、剧情源、谜底、历史试玩记录或另一 D01 方案。父代理批准额外只读 `csharp/SimulationSession.cs`、`csharp/ActionRuntime.cs`，用于正式提交和执行引用实验。

结论：采用**编译期词项校验 + 主体特定版本的主观词典**可形成候选边界。作者词表只说明允许的中文词、作用域及声明引用；主体词典只从自己的冻结 Projection 世界生成词项候选，不以客观 Essence 真名或全局实体表替主体确定所指。Core 现有机制支持中性实验，但通用作者接口、原子认知快照及执行失效尚未形成完整合同。禁止把实验结果解读为已批准 Core 增补。

## 同一判别集的实际结果

证据：[core-results.json](core-results.json)、[compiler-results.json](compiler-results.json)、[build-first.log](build-first.log)。所有实验均使用正式 Core 工程引用；未另写 Core 求值器、现实结算器或枚举实体。实验冻结容器由现有 `Observation.Observe` 建立，叶节点仍交给正式 `Ast.Evaluate`，组合交给正式 `Ast.All`。

| 判别项 | 实测 | 判定及限制 |
| --- | --- | --- |
| E01 隐藏现实变化、主体可见不变 | 现实数值 1→9，未再次观察；主观数值仍 1，两个快照查询均 False，词项候选相同 | 能守住主体查询边界；不代表现实行动可行性不变 |
| E02 一个中文词的两个可行投影 | 一个 Essence 的两个同名现实切面得到两个主观 Projection；词典保留两个 ReferentId；`AstDecoder` 名称解析拒绝歧义，嵌入可指定第二项 | 现有 Core 可表示歧义；`KnownProjection` 只返第一项，`Reasoner.CaseField` 同名匹配实测选第一项，二者不可作歧义安全入口 |
| E03 求值中认知更新 | 正式 `Ast.All` 两叶之间显式提交 Observation：实时叶结果 True/False，总结果 False；实验冻结读 True/True，总结果 True | 冻结读可避免混版本；仅在单线程静止时捕获，**未证明并发捕获原子性**；实验不是完整 `Ast.Evaluate(All)` 内部并发注入测试 |
| E04 加减中性实体 | 新建投影自动进入词典；Forget 后消失；同名重建分配新 ID，旧 ID 不复用 | 不需新增 C# 实体枚举；实验 ID 只保证本进程、一个主体注册器内稳定 |
| E05 历史重建、跨进程保存 | 旧 ObservationEvent 的 Target.Name 随后续改名变成“新称”，旧 Tags 仍为 1；Forget 后 History 清空；实验旧快照仍保留“旧称” | 当前 History 不是完整不可变历史。跨进程 Core 保存/恢复 **未验证**，本实验没有实现 codec；不能用外部实验快照冒充 Core 历史重建 |
| E06 真假未知与不支持 | 缺标签 Unknown，已知 0 为 False，已知 1 为 True；未知操作 `identify` 解析异常 | 不支持能力属于操作状态，不能转换为 False/Unknown 后继续执行 |
| E07 身份连续性 | 改名保留同一实验 ReferentId；`Associate` 将原投影改绑不同 Essence 时明确拒绝 | 稳定引用不能由名称决定；同名及同一客观身份不自动合并主观切面 |
| E08 执行引用失效 | Forget 后旧原始 AST 的 Exists 仍 True；新嵌入解码、新 Submit 均拒绝。合法延迟提交后客观 Remove 导致 MissingObject；合法延迟提交后仅 Forget 仍 Completed，并写现实数值 7 | 原始 AST 不自行校验世界成员；**已排队的物理目标在激活时未重验主观成员资格**。这是候选失效合同的缺口，不自行改变既有动作语义 |

编译期补充实测：唯一中文别名可编译，同中文别名用于两个实体被拒绝；添加中性实体可编译；未知实体、`facet` 属性被拒绝。故现有作者别名是唯一静态符号，不能直接承载运行时一个词多个主观所指。编译样本全为 `compiler_probe.py` 中的中性内存文本，未读取 `.story`。

另一个静态桥接约束：`InteractionPacket.cs` 的通用对话 `Truth(object)` 对未列出的对象类型返回 true；不能把 Core `Truth` 枚举或新的解析结果对象直接交给它作选项判断。新桥必须显式检查 `Resolved` 且命题为 `Truth.True`，同时保留 Unknown/Unsupported 的呈现原因。现有 WorldBehavior 候选入口已经显式要求 `Ast.Evaluate == Truth.True`；本项未额外执行 UI 实验。

## 候选合同

机器可审阅版本：[candidate-contract.json](candidate-contract.json)。

1. **区分身份。** `EntityId` 标记客观实例，`FacetId` 标记现实表现，二者仅在可信绑定/执行边界使用；`ReferentId` 标记主体内部 Projection，运行时词项解析只接收 `SubjectId + SessionEpoch + SnapshotRevision + VocabularyRevision`。主观 ID 不从 TrueName 派生，不因改名、别名变化而替换，不跨主体共享。Forget 后旧 ID 保留失效记录；同名重建是新 ID。识别同一客观对象属于显式认知输入，不自动合并两个切面。
2. **编译期校验。** 词表、实体/切面声明及支持操作作为外部作者数据校验。静态声明引用必须唯一；运行时词项允许一词多候选。禁止使用静态别名的唯一性推导主体已经识别对象。现有格式没有多切面及词典版本结构，不能静默把新增字段丢弃。
3. **主体快照词典。** 捕获完整主观查询图中的 Name、Kind、Existence、Parent、Tags、Relations、DeniedRelations，以及必要的认知来源摘要；词项索引为中文词→ReferentId 集合。扩展别名须有对应主体获得信息的来源，不能把作者总表直接摄入所有主体。词项是否可指称与它是否存在分开：主体相信不存在的对象仍可被谈论；具体行动适用性再求值。
4. **歧义与三值。** 解析返回 `Resolved / Ambiguous / UnknownReferent / Unsupported / InvalidReference / StaleSnapshot`；仅 `Resolved` 产生可求值引用。命题结果仍严格为 True/False/Unknown。限定词只读该快照；False 可排除候选，Unknown 不得偷偷当作 False 排除以制造唯一答案。剩余两个候选明确报歧义，不按顺序、不查现实真值、不使用 `FirstOrDefault` 消歧。
5. **一致性与执行。** 一次求值始终读同一不可变快照及词典版本。捕获须在认知提交边界保证一致；现有公开 Projection 是可变对象，公开 History.Count 也不能充当版本。执行凭证携带主体、会话代次、主观 ID、快照版本、词典版本、支持能力版本。交回执行边界时重新验证当前主体成员资格，禁止把冻结容器引用提交到现实。版本不符明确重新评估，不按旧名称重绑；现实可行性由既有 Core 行动激活阶段决定。
6. **历史与保存。** 旧快照固定绑定旧词典/能力版本；词表升级不能追改历史。当前候选可以只提供进程内冻结读，并对历史重建、跨进程载入返回 Unsupported。若未来要求正式保存，必须另行批准稳定 ID codec、认知提交序号及完整不可变观察记录；恢复须重建世界归属/AST 引用及动作调度。禁止把现有 History 或专用宿主 save 事件等同完整 Core 存档。

## 首批后收敛的最小审批项

审批清单：[approval-items.json](approval-items.json)。全部为 **PROPOSED / NOT_APPROVED**，没有自行授权实现。

- **P01 作者与宿主词项/切面桥。** 校验词项和切面外部声明、主体获得别名的输入，扩展 `VerifiedPerception` 的选定切面绑定，移除作者桥对“一实体一现实表现”的假设。先保留静态声明唯一性，与运行时多义词分离。影响编译器、宿主格式及模块 AGENTS；不是剧情代码。
- **P02 主观快照及版本边界。** 提供主体稳定 ReferentId、单调认知提交 revision、不可变查询图和一致捕获机制。优先评估宿主串行统一入口是否足够；若公开 `Observation.Observe` 无法被统一封装，则需审批最小 Core 只读快照/revision 能力。实验器的外部递增编号、手填 revision 不满足此项。
- **P03 动作引用失效语义。** 决定合法已提交动作是否允许在目标被遗忘后继续执行。如果要求每阶段持有主观目标，则只在 Core 阶段激活处增加通用引用成员资格校验并明确失败状态；若选择意图接受后引用继续有效，就不能声称“遗忘立即使执行引用失效”。还应禁止词项桥把未消歧名字传给 `CaseField`，不必为本合同改造完整 Reasoner。

完整历史、跨进程保存为**条件后续项 P04**，不并入首次最小实现批准。缺失时明确返回不支持。上述范围到此收敛；没有追加全仓扩审、剧情审评、UI/盲玩或性能推断。

## 验证与隔离记录

构建：.NET 8.0.400，正式 Core 项目引用，输出重定向至 D01B/build，0 警告 / 0 错误。Core 中性程序和现有 `world_script.py` 编译器探针退出码均为 0，全部断言通过。未执行一级/二级/三级验收；机制检测不能代替剧情、人物行为或真实游玩验证。

产物代码 `[Program.cs](Program.cs)` 是实验器；`Snapshot` 仅复制当前主观查询字段，没有规则、流程、完整来源历史及保存恢复，也不保证并发捕获原子性。ID/revision 为实验适配器字段；不是已有 Core 正式能力。

本平台没有实施硬 ACL。工具输出、工程、构建目录和记录均指定在 D01B；Python 禁止写入源码旁的 `__pycache__`。**环境副作用例外：** 为将 .NET CLI 数据目录置于 D01B 而触发首次使用流程，构建日志报告“已安装 ASP.NET Core HTTPS 开发证书”；系统证书存储可能受影响，不能宣称绝对文件输出隔离。后续执行设 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`，直接运行已有实验 DLL。未删除或修改证书。

来源、读取范围和哈希核对见 `[source-evidence.json](source-evidence.json)`。只读 Git 检查确认基线；本代理不提交或推送，由父代理处理本次研究交付。
