# r20 恢复范围独立审查

审查时间：2026-10-11。只读审查生产源码；未读取设计规范、剧本源、生成剧情或历史游玩记录；未运行游戏、构建或流程回归。此报告判断源码身份和恢复依赖，不评价剧情、人物或游玩效果。

## 身份结论

候选提交完整身份为 `5b8c7dbe0749ac2ffe1dd27f5aa5b78a88740f3e`，提交时间为 2026-10-11 11:51:07 +0800，标题为 `Fix repeated inquiry feedback and profile accelerated simulation`。本次检查时主仓库 HEAD 为 `0a97ab9d386e71b842cb6997a13695bc30caff66`，存在大量生产工作区修改与未跟踪源码。

仅凭该提交不能证明当时 r20 会话采用的完整工作区字节；没有审查到与该会话绑定的完整生产源码指纹，因此恢复后只能称“从 r20-era 提交恢复的候选”。若保留当前测量通道，则进一步称“r20-era 运行源码 + 当前只读测量通道”。不得称精确 r20、原样 r20，或直接把旧会话时刻当作此次候选的结果。

`before-production.zip`、`before-manifest.json` 与 `before-working.patch` 应作为恢复前材料保留，不覆盖。补丁不能独自代表未跟踪文件与原始换行字节。恢复操作应另外封存每个源文件的目标提交 blob、恢复前/后的 SHA-256、来源类别和例外理由。

## 必须保留的当前测量和保护边界

- `tools/guarded_level3.py`：验证提交中的保护 manifest 与工作区一致；启动正式 `full_test.py`，固定四倍速；证据目录已存在即拒绝覆盖；结束后复核保护文件。恢复时不得自动重建 manifest，不得沿用已经与恢复后源码不符的旧 manifest 宣称同条件运行。
- `tools/plot_level3_milestones.py` 与当前报告/绘图口径：保存独立游戏经过时间轴和真实秒轴、采样区间及未达成节点处理。它们是测量工具，不参与主体决策。
- `GameEngine.Measurement.cs` → `GameSession.Measurement.cs` → `Chapter1Session.Measurement.cs` → `ChapterSession.Measurement.cs` 的只读链应保留。最末端 `ReadProgress` 用 `TryGetValue` 复制现有进度，不调用会创建缺失进度组的入口。当前读取包含已提交进度、章节阶段和游戏分钟；不得改成推理、判题、观察或推进会话。
- 对应显式编译项也必须保留：`ChapterRuntime.csproj` 的 `ChapterSession.Measurement.cs`、`Chapter1Script.csproj` 的 `Chapter1Session.Measurement.cs`。整文件回退项目配置会静默删除这些编译项，留下源码存在但通道不工作的混杂状态。
- `FullTest.gd` 的独立 `execution-diagnostics.jsonl` 写入、真实 `elapsed_ms` 采样、`ReadMilestoneProgressJson` 调用、停止时最后采样，以及表单选择前实际渲染帧/截图记录应保留。不得用候选出现、地点改变、UI 开启替代实际关键进展。

## 必须成组处理的恢复范围

1. **Core 与宿主共同政策**：`SubjectiveDecision.cs` 当前 `ActionLearning` 已调用新增 `SubjectiveAgent`、`SubjectiveAgentStorage`；`PlayerController.cs`、`WorldSession.cs`、`WorldSession.Attach.cs`、`WorldSession.Save.cs`、`WorldBehavior.cs`，两章适配和 Morning 系列也引用该合同。只回退 `PlayerController` 或几个评分方法不会得到旧行为，而会留下两套政策和两套状态。若选择旧提交运行合同，这些运行依赖应一并恢复，并从活动编译范围排除该提交不存在的 `SubjectiveAgent.cs`、`SubjectiveAgentStorage.cs` 和相关新增 Morning 源文件；其恢复前字节保留在备份中。
2. **执行与调度**：`ActionRuntime.cs` 当前新增 `CanInspect` 并用于执行检视的现实可达边界；新增 `MorningKnowledge.cs`、`MorningOperations.cs` 会直接调用。`CTBSystem.cs` 当前新增原子 `RescheduleMany`，`MorningEcology.cs` 有调用。回退 Core 而保留新宿主会造成缺失 API；保留 Core 而恢复宿主又不是目标提交合同。恢复这些文件时必须记录现实执行/调度合同确实改变，不能把此变化包装成同条件性能优化。
3. **认知读图与推演**：`SubjectRead.cs`、`SnapshotConsumers.cs`、`SubjectiveReasoning.cs`、`SubjectiveDecision.cs` 当前形成同一已发布快照复用与完成证明缓存合同。不得仅恢复其中一个文件。`Observation.cs`、`SnapshotGraph.cs`、`WorldRuntime.cs` 的变化还包括可选性能诊断作用域；若保留诊断，明确列为独立例外，不笼统声称 Core 全部来自旧提交。
4. **正常导航与执行反馈**：`AgentPilot.gd`、`PilotNavigation.gd`、`GameEngine.cs`、`Main.gd`、`WorldView.gd`、`AwakeningMap.gd`、`UISystem.gd` 与章节交互适配应按合同一起处理。当前新增公开区域定位、执行开始回执、来源稳定身份、沿已见路线持续执行、可见入口真实按键穿越、公开房间来源 UI；只回退导航文件会留下新 offer 字段/宿主 API 与旧执行器失配。任何恢复或再优化都须保持正式键盘/点击、物理碰撞和玩家实际可见信息边界，不直接设置位置、不读取未知碰撞、不预选答案。
5. **保存**：当前 `WorldSession.Save.cs` 从 `story.world-session/1` 变为 `/2` 并保存 Controllers 与 Agencies。它依赖当前共享主体合同。不能只回退版本标记或只回退保存方法；旧版运行应采用新会话，不拿当前 v2 存档强行恢复为旧会话。

## 当前诊断链的特殊风险

当前未跟踪 `GameEngine.Diagnostics.cs` 订阅 `CorePerformance`；`GameSession.Diagnostics.cs` 转发 `Chapter1Session.Diagnostics.cs`。后者读取新增 `ActualOperationReceipts` 与 `MainSubmissionReceipts` 等执行审计集合。这些集合与新增 Morning 执行机制相连，不是可无条件叠加到旧提交的独立文件。整目录回退会删除诊断链；仅回退跟踪文件会保留新诊断但缺失它需要的旧/新 API。

建议保留既有诊断源码字节作为证据，同时让恢复候选的诊断通道仅复制该候选确实存在的原始执行记录。不存在的记录明确标为不可用，不伪造空集合表示“没有发生”，也不为接通诊断而把当前执行逻辑混回旧运行。若调整 FullTest 的可用性处理，应单独记录诊断差异。`CorePerformance.cs` 可以作为可选工程诊断例外保留，但不能据此声称旧版本具有同样的阶段样本；阶段耗时重叠且不构成额外独立总耗时证据。

## 可审计执行清单建议

恢复前先建立路径白名单：目标提交内确实存在、属于运行合同且当前不同的源文件；新增运行源码单列“移出活动编译范围”；测量/保护工具单列“保留”；剧情和生成资源单列交由主代理核对。本审查没有读取剧情，因此不为剧情恢复清单或设计符合性背书。生成物必须经对应工具从已确定的源重新生成，不能用旧二进制填补源码不一致。

严禁对 `csharp/`、`godot_csharp/`、`tools/` 或仓库执行不分类型的整目录覆盖/清理：这会删除当前保护和测量工具、未跟踪诊断/执行器，或留下旧生产源码与新未跟踪源码混编。`DualOntologyWorldEngine.Core.csproj` 当前默认包含新增源码，特别需要检查移出的文件；其 `SubjectiveAgentRegression.cs` 排除项也与新增工程同步处理，不能让测试入口进入 Core 类库。子目录 AGENTS.md 应继续准确记录恢复后的实际合同和保留的测量通道，不直接恢复过时规则。

恢复后先核对逐文件 SHA-256 和显式/默认编译范围，再做必要构建与生成一致性排错。保护文件修改原因、具体差异、此独立审查和新基线必须分开记录，然后才可提交明确区分的新基线并开始新的四倍速正式会话。旧历史轨迹与新会话分列；实际输入、玩家可见信息、执行回执、截图和采样区间是后续判断依据。当前报告未进行任何验收级别，工程可构建、机制合理性、人物合理性和体验均未验证。

## 对主代理最终恢复组合的补充审查

主代理决定将生产运行合同恢复为 5b8c7dbe，保留四个已提交 Measurement 文件和两处显式编译项，将当前三处 Diagnostics 完整备份后移出活动源码；FullTest 回到候选基版后只增加调用 ReadMilestoneProgressJson 的旁路记录。此组合在本次静态审查中接口兼容：候选的 GameEngine、GameSession、Chapter1Session、ChapterSession 均为 partial；候选 ChapterSession 已有 _progress；Chapter1Session 已有 Session、State、Stage 以及 _world.GameMinutes。它仍应标明是提交运行源码加当前测量通道，不能称精确历史 r20。

明确移出活动源码并保留字节备份的新增文件：csharp/CorePerformance.cs、SubjectiveAgent.cs、SubjectiveAgentStorage.cs、SubjectiveAgentRegression.cs、SubjectiveAgentRegression.csproj；Night/MorningInvestigation.cs、MorningKnowledge.cs、MorningMainInvestigation.cs、MorningOperations.cs、MorningTravel.cs；Chapter1Session.Diagnostics.cs、GameEngine.Diagnostics.cs、GameSession.Diagnostics.cs。CorePerformance 在此组合中没有旧运行订阅点，移出可避免模糊来源。Chapter1Script.csproj 的 ../Night/*.cs 会纳入新增 Night 文件，Core 项目默认 glob 会纳入新增 .cs，故仅恢复跟踪文件不够。

新增 NeutralDeferredAnswerCases.cs、NeutralExecutionLifecycle.cs、RoomRegistrationCases.cs 与 tests/UnifiedHostCheck 也须保留备份并移出，或明确从对应排错工程编译范围排除；它们绑定新合同，不能作为旧合同的当前排错入口。新剧情源不在本审查阅读范围，源集闭合与生成一致性由主代理核对。保留当前 docs 设计、保护工具、历史证据、用户裁定与更新为实际旧政策的 AGENTS；保护 manifest 应在恢复差异和独立审查已经记录后，建立明确区分的新基线。
