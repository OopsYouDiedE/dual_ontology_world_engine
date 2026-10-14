# 单一动作路径清理

用户确认：保留 DV 的 Prepare → 现场执行 → Complete，清理整个项目旧动作实现；历史证据与剧情源保留。起始分支为 `codex/partial-bootstrap-core`，开始时没有已修改的受跟踪源码；既有未跟踪成果和 `chapter1-recall.dv` 未纳入提交。

## 实现结果

真实动作只有 `verification/session/30-runner.dv` 的 prepare/complete。主体决策和报价在 prepare 中形成待执行意图，效果、观察、传播、答辩和结算在 complete 中处理。`game/campaign.dv` 只做生产内容组合与章节扩展；`Session/Session.cs` 只持有状态和序列化。逻辑运行器直接调用同一 prepare/complete，移除 step 别名，不另实现行动机制。

正式宿主链：`Main.dispatch → GameEngine.Dispatch → DvSession.Prepare → Main._dv_execute → WorldView.execute_move → 真实导航/碰撞回执 → Main._dv_complete → DvSession.Complete → 呈现`。非移动意图仍由 DV 世界法则执行并沿相同 complete 发布。等待回执时不重复分派意图；读完界面的 presented 不再单独调用 Prepare。

旧 C# Core、旧 Kernel/bootstrap 解释执行、两章旧会话/原型、旧坐席和 token 操作、第二个 DvRuntime 会话持有者、旧检查和运行工具从工作树移除。旧源码可从 Git 历史查阅，未另造可运行 legacy 目录。seed 只留下 C0 需要的 Reader/Value，删除世界与解释执行类型；两份解决方案都引用 DV Session，不再依赖旧 Core。

现场移除了旧 stage 分派、独立 Chapter2Field 控制器、键盘/点击直接移动、快照触发的进出场移动和让路移动、旧舞台回放。身体首次创建和换图的视图放置保留，后续实际行走由准备好的意图启动；人物碰撞不会因清理自动成功。CampusMapView 的跨图路线与站点路线是同一意图的现场阶段，共用 StoryActor.follow_path，不是另一套主体决策或状态提交。

原 `.story`、原生 DV 内容、oracle、法则、代价、初始认识和测量基线未为此清理修改。此次变更是架构清理，不是同条件耗时优化，没有自动重建保护基线。旧保护测量入口退役，DV 对应入口尚待另行实现。

## 逐文件证据

- `code-file-audit.json`：初始项目代码文件清单、逐文件原字节 SHA-256、动作入口命中位置、最终处置和保留文件的修改后指纹。检索覆盖项目源码，真实动作链及相邻执行/导航代码另作源码审读；不把关键词扫描说成逐行人工语义证明。
- `removed-files.json`：每个移除文件及其原因，包含旧控制器的资源依赖闭包。
- `final-source-audit-01.json`：第一次引用检查，包含旧已缺失资源及模板路径误报；保留该记录。
- `final-source-audit-02.json`：最终现役源码清单、动作入口位置与指纹；未发现保留源码引用本次删除的 Godot 资源或旧动作入口。模板路径单列处理；`existing_missing_resources` 如实列出原有素材试验/旧 UI 资源问题，未把它们修成游戏改善。

源码范围包含受跟踪与未忽略的现役源文件。`artifacts` 的历史代码、生成/缓存目录、临时工作和音乐工程不作为现役动作入口；历史证据不通过恢复旧依赖重新变成执行路径。

## 工程排错与短启动

`build-host.log` 记录正式 Godot 解决方案构建，`build-language.log` 记录 Seed 与 DV Session 的 Release 构建。生成 C3 编译器继续由源码自举，移除旧内核后生成链能够运行。构建只说明当前工程和生成连接可以成立，不说明人物或游戏合格。

受限进程的 `godot-import.log`、`godot-import-isolated.log` 和 `godot-import-verbose.log` 记录用户目录错误与 `.NET: Assemblies not found` 初始化崩溃。之后允许已安装 Godot 在进程限制外运行，`godot-import-unrestricted.log` 记录实际导入完成。

`startup-01.log` 是新正式宿主的八秒启动记录。引擎实际选择并完成恶魔的非移动检视 `demon.departure.inspect-accepted`，随后进入侦探自己的认知与规划。关闭时记录 `host-closed-during-active-session`；没有宣称整局结束。`startup-01/startup.png` 已实际查看：主场景、侦探身体、房间、遮挡与 8:00 HUD 已绘制。该窗口内没有侦探移动完成或章节交接证据，不据此证明跨图、离屏人物执行或完整体验。

`startup-01/raw-session.zip` 保存完整 session/save/inputs/visible 的原字节，`raw-byte-manifest.json` 保存指纹；原始输入和可见记录另存 JSONL。压缩仅用于封存，未改内容。局部 `.gitattributes` 对日志与本轮原始证据关闭换行转换。

验收级别：本轮为源码清理、工程排错和短启动，未执行一级三份全程记录、二级 UI 组合专项或三级游戏体验验收，也未运行 flow/flow2 或任何包含它们的回归。机制架构已收敛；人物合理性、整局推进与两章体验未验证。

工作期间出现了历史 artifacts 中若干旧探针 csproj 的并发改动；它们不属于本轮清理脚本的写入范围，未回退、未纳入本轮提交。
