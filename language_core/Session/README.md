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
| `Prepare(selection = "")` | 核对本人资格，准备唯一正式意图；已有 pending 时返回同一意图 |
| `Complete(receiptJson, viewer)` | 接受与动作身份相符的真实执行回执，结算并生成对指定观察者可见的呈现 |
| `Active` | 读取会话是否继续活动 |
| `Save()`、`Restore(string)` | 完整进程内 JSON 快照交换，不用于新轨迹落盘 |
| `WriteTrace(path)`、`WriteSave(path)`、`RestoreFile(path)` | 二进制 DAG 轨迹/存档写入与完整封存校验后的恢复 |
| `Trace()` | 进程内完整逻辑轨迹交换接口，不能补回精简模式已省略的数据 |

回执包含 `action_event`、`outcome`、`actual`（time/computation/stamina）与执行器身份。失败执行仍保留实际费用及失败回执，成功后才应用相应的权威效果与观察。重复准备不产生第二次执行；未知观察、预测结果或候选出现不能代替现场成功回执。

人工选择仅在 DV 等待该主体输入时提交，选择与自主控制共用资格、Prepare/Complete、费用及判答。等待与恢复不重复认知，不生成答案。呈现过滤到本人已完成动作、真实解析话语或实际观察；远处的私人正文不会通过呈现接口泄漏。`surface_voice` 与附加 `voice` 保留区别。

`Snapshot(viewer).chapter_summary` 只在实际章节结算条件发生后提供系统结果；它不写入人物认识，也不证明 Godot 已显示或验证结算画面。内容扩展保留原会话、账本、队列与事件；内容可用和实际跨门分别记录。

## Godot 适配与在途动作

`GameEngine.ConfigureSession` 只在新会话打开前接受配置。宿主串行处理命令，排队保存重入输入，并以 DV `action_event` 去重完成回执；不相干回执不能清除 pending。后台工作异常停止继续分派，不能自动重试可能已经提交的转换。

打开、准备、完成与观察者缓存投影在同一后台工作链中进行。主线程读取最后完成的缓存，避免并发遍历正在更新的 DV 状态。`dv_action.execution_snapshot` 仅供实际行动主体的几何执行，不作为玩家取得的新观察；阶段回调 `PhaseObserver` 只读报告计算阶段。

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
