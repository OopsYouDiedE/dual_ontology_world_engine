# 重构实现对照（2026-10-22）

依据用户提供的 `dual_ontology_world_engine.md` 第六、七节。
本文记录实现与接线，不是 AI 行为、性能、画面或实际碰撞验收报告。
本轮新增 CLI 入口、协议与文档，并清理旧桥接生成物；其余条目为现役源码已有实现。

## Step 1：性能与提交边界

| 要求 | 现役实现 |
| --- | --- |
| 默认关闭逐帧 UI/可见性取证 | `Host/PlayerViewTrace.gd`、`RuntimeDiagnostics.gd` 按 audit 显式连接绘制信号；原始输入按事件记录 |
| 消除无上限时序队列 | `Host/GameEngine.cs` 两个计时队列均限制 4096 条，关闭清单记录丢弃量 |
| FrozenList 哈希及索引复用 | `compiler/Runtime.cs` 冻结节点保留 Hash 与 membership hash；已有键更新继承父记录布局；相等仍核对值，避免哈希碰撞误判 |
| 单次快照与按需投影 | `host_bridge/Session.cs` 按观察者/版本缓存；同观察者复用；其他执行者使用精简 ExecutorProjection |
| 安全保存与只读 Trace | Session 的 lastCommittedState、DurableState、TraceValue 与幂等 Close |
| 移动失败统一回滚 | `Host/DvFieldExecutor.gd` 以事件记录源锚点、占位及真实 trail；Complete 的 dv_commit 决定提交/回滚；WorldView 执行恢复，失败保留事务并报告 |
| 调试恢复路径、格式参数与 visited | `mcp/verification/run_debug.py` 使用 checks/65-debug-runner；raw_dag 传递 value_format；30 的 visited 仅记录实际展开节点 |

## Step 2：物理目录与工具边界

Core 使用 compiler/modules/checks/host_bridge，另有 base 基础程序集及 cli 文本宿主。
Godot 只保留运行脚本、场景、发布契约及成品资产。FrameArchive、截图、资产生成、
音乐、适配器与编辑器工具全部位于 mcp；server 按 tools.json 路由懒加载。
65 调试入口与 70 oracle 不进入 60 的生产 include 闭包。
旧 UI/Shader 清单已清理；tmp 不存在，.scratch 无本轮脚本。
旧 host_bridge 根目录 DVProgram.cs/session.dv/ranges.json/source-manifest.json
已清理，现行生成物由构建器写入 obj/dv，不维护第二份源码。

MapSandbox 放在 `mcp/sandboxes/`，而非正式游戏域；工具创建纯 GDScript 工作区，
复用实际场景、契约与 StoryActor。该路径选择落实工具分离要求。
根 AGENTS 明确一次性脚本归 .scratch、任务结束清理、复用工具归 mcp。

## Step 3：双接口与文本宿主

35-campaign-schema 提供 campaign_* 公共构造器、16 人目录、位置、时钟、
目标窗口及跨章结构；40 与 50 分别依赖 35，无直接函数互调。
Session.Project 返回带基版本的 StateDelta；静态投影按地图/章节缓存，
Godot 只转换变化字段，正式热路径不往返 JSON。

`dv_core/cli/DvCli.csproj` 是实际 .NET 8 可执行入口，引用同一 Session。
支持 status/options、编号或 ID 的 act、结构化重演选择、next 单步、
JSON-Lines、终端 TUI、稳定存档与只读轨迹导出。开发命令仍经 Core 授权。
默认等待显式输入；不预置路线、答案、预算、AI 测试配置或运行上限。

CLI 与游戏共用 SmartReplay：读取 dvlog/dvdag 的校验记录，使用内容节拍，
保留原始时间关联但不等待原现实停滞；支持播放、暂停、单步及 1/2/4/8 倍速。
回放不创建正式会话，不通过重放生成新观察；旧记录缺身体轨迹时只能语义重建。

## Step 4：控制台与独立工作流

Main 已拆分 Flow、Lifecycle、Presentation、FieldExecutor、Diagnostics、Trace 等模块。
DevConsole layer=128，反引号/F12 打开，含命令、双本体/规划、性能、回放四页。
Inspector 只在打开时定期刷新；性能页使用 120 帧环，关闭停止采样。
ReplayStage 使用独立视口、实际 WorldView/StoryActor/UI，不修改正式会话。

geometry_contract、presentation_mock_pack 与 35 schema 分别支撑地图、
UI 和 Core 工作流。MapSandbox 调用工具侧 GeometryContractValidator；
MockSession 的纯 GDScript 启动不创建 C# 工程或正式 DV 会话。

WorldView 在 48 px 门前与热点接近途中触发 PrepareAhead；Session 的分支
绑定版本和选择，正式采用或失效重算。NPC 阅读/行走期间后台工作仍经同一
Prepare/实际执行/Complete。30 的 idle_guard 以本人输入、事件、接触与时间边界
证明稳定后跳过，听众预测先筛同房实际听众，未知依赖回正常求值。
20/30 独立基础程序集、各模块源码/接口缓存及 MCP/LSP 后台编译器预热已接线。

## 其他审计问题与边界

UISystem 公开 presentation_id 和 spatial 面板身份；地图页使用正式几何与本人
map_knowledge；生产 quit 集中到 HostLifecycle。3D 文本记录明确纹理/冻结页
身份，不把控件树当作逐像素可见证明。
工具域已采用当前封面状态、源码/封存门禁、命令与物化身份配对、明确渲染未执行
及协议 Mock 范围；formal-model 与 Godot 几何证据分别报告。工具改造不等于
本轮已经运行工具，也不表示可删去仍被 checks/编译器隐式调用的 Core 能力。

文档中的 5–10 倍、JSON 缩小 80%、零停顿等是目标，不能据源码实现宣布达成。
严格值相等也不能只凭单个结构哈希决定，否则会破坏 DV 语义。

## 交付与后续测试

本轮只编译 CLI 及其 Core 依赖，不启动 CLI 会话、65 调试运行器、
70 因果分析、Godot 或 AI 测试。编译结果在交付消息中记录。
一次 Core AI 测试须等用户批准，并由用户提供具体要求和指标。
没有运行证据前，不宣称性能、AI 行为、游戏体验或完整通关验收通过。
