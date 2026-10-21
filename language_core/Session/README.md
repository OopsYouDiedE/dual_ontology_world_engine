# DV 宿主会话

`Session.cs` 是生成 DV 类库的宿主与序列化边界。生产代码来自 [60-game.dv](../60-game.dv) 及其 20/30/40/50 include 闭包；Session 不依赖 Godot 或已退役的 C# Core。运行时值 ABI 来自 [Runtime.cs](../compiler/Runtime.cs)，所有资格、费用、权威效果、观察、传播与主体决策由 DV 实现。

## 构建

在仓库根目录运行：

```powershell
dotnet build language_core/Session/DuelView.Session.csproj -c Release -p:DvPython=<Python路径> -p:NuGetAudit=false
```

项目在 `CoreCompile` 前调用 `tools/build_session.py`，生成并编译 `Session/obj/dv/DVProgram.cs`。源组合与编辑器共用 `tools/dv_toolchain.py`；生成的模块清单记录源与目标指纹。根目录下的旧 `Session/DVProgram.cs` 即使本地存在也被项目排除，不作为另一份可维护源码。需要单独查看生成物时运行 `python language_core/tools/build_session.py --out language_core/Session/obj/dv`。

## 正式接口

| 接口 | 行为 |
| --- | --- |
| `Open(scenarioJson)` | 从显式原生 DV 交换数据建立会话 |
| `OpenCampaign(player = "")` | 打开原生生产内容，可指定人工控制主体；空参数保持自主控制 |
| `OpenConfiguredCampaign(optionsJson)` | 通过 `dvg_host_open` 应用 `{controller, viewer?, campaign?, audit?}`，默认与配置验证属于 DV |
| `Viewer`、`Snapshot(viewer)` | 读取当前/恢复会话的原生观察者与呈现投影 |
| `Prepare(selection = "")` | 核对本人资格，准备唯一正式意图；已有 pending 时返回同一意图。普通选择为动作 ID；人工重演为 JSON 字符串 `{action_id,hypothesis_id}`，只在此边界解析为 DV 纯数据 |
| `Complete(receiptJson, viewer)` | 接受与动作身份相符的真实执行回执，结算并生成对指定观察者可见的呈现 |
| `Active` | 读取会话是否继续活动 |
| `Save()`、`Restore(string)` | 完整进程内 JSON 快照交换，不用于新轨迹落盘 |
| `WriteTrace(path)`、`WriteSave(path)`、`RestoreFile(path)` | 二进制 DAG 轨迹/存档写入与完整封存校验后的恢复 |
| `Trace()` | 进程内完整逻辑轨迹交换接口，不能补回精简模式已省略的数据 |

回执包含 `action_event`、`outcome`、`actual`（time/computation/stamina）与执行器身份。失败执行仍保留实际费用及失败回执，成功后才应用相应的权威效果与观察。重复准备不产生第二次执行；未知观察、预测结果或候选出现不能代替现场成功回执。

人工选择仅在 DV 等待该主体输入时提交，选择与自主控制共用资格、Prepare/Complete、费用及判答。等待与恢复不重复认知，不生成答案。呈现过滤到本人已完成动作、真实解析话语或实际观察；远处的私人正文不会通过呈现接口泄漏。`surface_voice` 与附加 `voice` 保留区别。

人工重演封装保留原动作 ID，DV 在选择与完成时重核本人候选的 topic、basis、available_when 与 manual_available_when；只对明确选中的一项执行付费重演，不顺带重演其他模型；既有有来源约束的剪枝及覆盖证明仍在同次付费预算内执行。尝试签名按动作与假设分开；本人认识变化后可重新核对，其他候选不因一次尝试而全部关闭。自主重演仍使用原批量路径。新输入方式属于控制机制修正，不作为同条件性能优化。

`Snapshot(viewer).chapter_summary` 只在实际章节结算条件发生后提供系统结果；它不写入人物认识，也不证明 Godot 已显示或验证结算画面。内容扩展保留原会话、账本、队列与事件；内容可用和实际跨门分别记录。

## Godot 适配与在途动作

`GameEngine.ConfigureSession` 只在新会话打开前接受配置。宿主串行处理命令，排队保存重入输入，并以 DV `action_event` 去重完成回执；不相干回执不能清除 pending。后台工作异常停止继续分派，不能自动重试可能已经提交的转换。

打开、准备、完成、观察者投影、JSON 编码和生产事件的 Godot 纯数据物化在同一串行后台工作链中进行。后台每次独立创建 intent、观察者 snapshot 与执行者 snapshot 的 Dictionary/Array 图，完成后经 await 一次转交主线程；相同观察者可以共享不可变 JSON 字符串，两个可变 Godot 图仍各自独立。后台不操作活动场景、节点、加载资源或发送 UI 信号；主线程交接只更新已完成的缓存字符串、pending 标识和事件图引用，再发送原顺序事件。

初次 `view` 事件携带 `resource` 与已经物化的 `snapshot`，Main 直接安装该快照，不再从 JSON 重建同一开局。兼容 `GameEngine.Snapshot()` 仍从最后完成的缓存 JSON 返回独立 Dictionary，显式调用会在调用线程解析；生产事件链不调用它。`ReadMilestoneProgressJson()` 直接读取同一缓存字符串。这些接口不并发遍历正在更新的 DV 状态。`dv_action.execution_snapshot` 仅供实际行动主体的几何执行，不作为玩家取得的新观察。

Session 在一次状态转换之间保留私有 intent 投影，供 `Prepare` 返回与同态的观察者/执行者快照复用。缓存仅由该实例的串行工作者访问，不向宿主暴露 DV 值；每次 `Prepare`、`Complete` 以及会追加关闭事件的 `Trace`/`WriteTrace` 在执行前清空，抛错也不会留下可继续复用的旧投影，恢复使用新实例。原生 `dvg_snapshot(state, viewer)` 仍直接从该状态计算 intent；Session 使用 `dvg_snapshot_with_intent` 传入自己同态的只读投影。复用不跳过认知、正式选择资格重核或执行，也不跨过正式状态变化沿用旧候选；`SelectPlayer` 的资格重核保持原样。

宿主 JSON 直接递归写入 UTF-8 输出，不先复制成完整的托管 Dictionary/object[] 树。非空字符串二元组列表仍编码为对象，其他列表（包括空列表）仍编码为数组；重复键保持第一次插入位置和最后一个值，Rational 仍使用 `$dv_rational` 字符串标签。投影保持原字段、嵌套形状与 JSON 数值/转义规则，不改变保存或二进制证据格式。宿主投影维持原来的 64 层 JSON 深度上限，超限报告 JsonException。

`PhaseObserver` 只读报告计算阶段，GameEngine 为每个命令重新绑定 interaction_id/command_id；恢复后的会话同样接线。现有 prepare/complete、snapshot 与 delivery 计时保留，另记录 DV intent/snapshot 投影、host_json 编码、`materialize_begin/end` 后台解析、`handoff_begin/end` 主线程交接以及托管 thread_id。阶段之间的空档与未闭合阶段保留原状，不把总等待当作 CPU，也不把编码/物化阶段外的工作算进其耗时。序列化收益、主线程停顿、NPC 决策等待和实际现场移动须分别测量。

Godot 的显式保存与恢复拒绝仍等待现场回执的状态；异步入口暂停命令泵，并在等待先前工作完成后再次核对 pending。恢复后旧状态的排队命令被丢弃，实际输入记录仍保留。来自关闭存档的 pending 在下一次正常 Prepare 后交给现场真实执行。

关闭先停止准备、等待后台工作、保存实际输入和可见记录，再封存会话与存档。关闭可保留 pending 作为证据；`SessionClose` 在该关闭路径仅调用一次，不自动重试。`close-result.dvlog` 区分是否打开会话、未处理命令、pending、各阶段错误及完整性；仅关闭标题页不伪造会话或存档错误。`TraceClosed` 只表示关闭流程结束，不能替代证据完整性或游玩结论。

## 审计与保存合同

`OpenConfiguredCampaign` 的 `audit` 默认 false；独立程序的 ABI 默认完整审计。Session 每次 DV 调用按实例进入 `R.UseAudit` 与阶段观察作用域，彼此不修改全局策略。`Save` / `WriteSave` 在序列化边界补写 `audit_mode`；恢复沿用该值，旧档缺失时默认 true。

精简模式保留正式推理消费者需要的历史字段，并省略特定事件 before/after 大字段；普通宿主不自动导出重复全量 `session.dvdag`。显式 `WriteTrace` 不恢复已经省略的字段。完整采集应在会话打开时开启审计，普通与审计模式的性能、来源覆盖和行为分别报告。

二进制文件独占创建 `.partial`，封存成功后发布正式文件名，拒绝覆盖与 JSON/JSONL/gzip 目标，失败保留部分字节。DAG 保留原字段、顺序、未知字段、不可变共享引用与精确数类型；Rational 原子和相同文本的字符串分型。进程内 JSON 的 `$dv_rational` 标签与落盘 DAG 类型不得混同。

`compiler/Runtime.cs` 的 `BinaryEvidence` 实现 `DVLOG2` 类型帧，包含长度、SHA-256 与负载；DAG 另保留序号、前序摘要、节点、事件、checkpoint 和 seal。`tools/binary_evidence.py` 与 `tools/raw_dag.py` 读取相同格式。宿主 JSON 解码产生的 CLR 数组只在证据编码边界支持，不能扩展 DV 的 List/FrozenList 值域。

## 逻辑诊断与恢复

生成的 `DVProgram.CampaignTrackedActors`、`CampaignChronologicalTrace` 和 `CampaignRun` 调用 `60-game.dv` 的原生函数。跟踪只过滤只读输出，全部合格自主主体仍按正式调度执行。`CampaignRun(false)` 使用内容声明的行动上限，零表示尚未推进的开局；它使用 formal-model，不能冒称 Godot 执行。

`python language_core/tools/run_debug.py --out <新目录> --timeout <本轮授权秒数>` 运行一次正式逻辑输出窗口。`dvg_debug_config` 只设置报告主体、事件筛选与诊断停止范围；完整状态保留在原始证据。一级是无窗口 DV 逻辑，不包含 UI 回放或三级几何验收。断点导出与恢复只接受真实 Complete 后的封存阶段，并校验来源指纹、配置、提交身份和完整性；恢复诊断与全新开局分别报告。

本接口说明不是测试记录。构建、二进制编解码或有限动作结果均不能证明完整剧情、人物行为或五日游玩已经验收。
