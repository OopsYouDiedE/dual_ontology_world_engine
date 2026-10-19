# Host / Session 接线静态记录（2026-10-19）

本子任务只修改 GameEngine.cs、GameEngine.Measurement.cs、Session.cs、Session/README.md；主代理维护 Main、DV 入口及工程合同。遵循集中修改后统一排错的顺序，本子任务未构建、未启动 Godot、未运行逻辑会话、探针或流程回归，未读取设计文档。以下为源码审读结果，不是游戏验收。

## 配置与唯一会话

- 新增 DvSession.OpenConfiguredCampaign(optionsJson)，由 ParseValue 转入 DVProgram.HostOpen → F_dvg_host_open；Viewer 由 HostViewer → F_dvg_host_viewer 读取。
- 保留 OpenCampaign(player) 兼容接口，Godot 新开局统一调用 OpenConfiguredCampaign。C# 不填角色 ID、章节参数、seed 或剧情默认值。
- ConfigureSession 只在新会话尚未启动时接受 JSON 独立副本。SetPlayerControlled 兼容入口只写 controller 参数；两种模式均经过同一 Prepare/Complete。

## 串行与现场数据

- Dispatch 将语义输入序列化后入队。唯一异步队列泵等候当前工作，信号重入只向同一队列追加，不再因 busy 静默遗失完成回执或选择。
- 开局、Prepare、Complete 在后台执行 DV 调用及 viewer 快照导出。主线程只解析已完成 JSON，不遍历正在修改的 DV 世界。
- prepared 的 dv_action 保留原 intent，顶层附 execution_snapshot，其来源为同一后台工作中的 session.Snapshot(intent.actor)，供 Main/WorldView 真实现场执行。缓存 viewer 快照另以 state 事件交 UI。
- Snapshot 每次解析缓存 JSON，读取者得到独立 Godot 字典。ReadMilestoneProgressJson 也只读该缓存，不硬编码观察者，不并发进入 Session。
- pending 按正式 action_event 标识。重复成功回执忽略；不属于 pending 的回执仅报告错误并保留 pending。等待现场时 view_ready/dv_next 不重发意图。
- worker 或投影异常后停止后续调度；因 Complete 可能已成功而投影失败，禁止盲目重试该回执。错误保留到关闭报告。

## 持久化与关闭

- 内部 Task SaveAsync/RestoreAsync 暂停队列、等待原 work，并把完整持久化操作纳入新的 work；Task 不暴露给 Godot Variant 绑定。
- 兼容同步 Save/Restore 只接受空闲宿主，活跃工作时明确抛错。Restore 的投影导出也在后台进行。
- Restore 不调用初始化；保留 DV 存档中的 pending，下一次正式 Prepare 提供恢复的真实意图。旧状态的队列命令清除，原输入记录保留，避免把旧操作移植进新状态。
- CloseTrace 阻止新调度，等候已启动工作后封存输入、可见记录、Trace 与 Save。尚未消费的队列不再触发 Prepare，close-result.json 如实记录 unprocessed_commands、pending_action 和先前 worker/持久化错误。
- Trace/Save 继续独占创建 .partial，完成后改正式名。Trace 关闭调用保持一次，异常不重试；TraceClosed 仅表示关闭过程结束。

## 静态审读边界

阅读了根、language_core 与 godot_csharp 的 AGENTS、现有 GameEngine/Session/Measurement/Main 接口及 DV receipt 的 action_event 核对位置。核对了本文件范围 Git 差异及主代理确认的事件形状。

仍待主代理集中生成/构建以核对新增 DV 函数与生成 C# 名称、Godot C# 绑定和运行程序集一致性。真实移动、离屏几何、UI 连续确认与人工候选操作、完整存档恢复及关机证据需按授权实机检查。本记录不宣称任何验收级别完成、剧情达成或性能改善。

## 独立静审补修：现场 pending 的持久化边界

独立审查指出，DV 后台工作结束不表示 Godot 的真实寻路与碰撞已经结束。busy=false 且 pendingEvent 非空时，现场仍持有当前意图；此时 Restore 会使现场旧回执落入另一状态。

- RequireIdle 现在统一核对 pendingEvent；同步 Save 与 Restore 都拒绝尚未回执的 Godot 执行。
- SaveAsync/RestoreAsync 在进入前明确拒绝 pending；等待原 work 完成后再次核对，以覆盖正在 Prepare 的 worker 在等待期间产生新 pending 的情况。
- 拒绝不会等待现场完成，因此不会与被暂停的回执队列形成互等；原状态、现场意图与回执身份保持原样。
- CloseTrace 继续绕过这条交互存取限制，停止新准备并保留真实 pending 状态作关闭证据。
- 标题页从未开启会话时正常保存输入、可见记录与 close-result，记录 session_opened=false，不制造缺少会话的错误；已开启则记录 true 并沿原 Trace/Save 封存。

本补修只静读接口与控制流，未构建、未运行或启动会话。实际现场中途存取拒绝、正式结束后的存取与标题退出仍待主代理集中检查。
