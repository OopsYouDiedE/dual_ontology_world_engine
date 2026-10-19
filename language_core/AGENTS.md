# DV 模块维护

## 空间交互合同（2026-10-19）

同房间宿主接近不提交DV或扣除时间/行动次数，热点交互与跨房间继续走共同Prepare/Complete。game/entry.dv在共享dvs_player_options已准入候选上附加kind与interaction（site/person/self/doorway、目的地图及执行房间），不返回隐藏值，不改变资格。绑定属于原生DV呈现内容；缺少具体绑定的个人动作通过本人手稿访问，宿主不解析剧情ID。dvs_select_player仍复核同一本人资格。宿主的坐标与碰撞不写入认知；实际接触成功后才Complete发布观察。

## Core 与现场接线（2026-10-19）

正式入口使用 `GameEngine.ConfigureSession` 的 `{controller: "player"|"ai", viewer?: string, campaign?: record}`，只允许新会话打开前配置。默认主体、内容参数与可视资源绑定在 DV `dvg_host_open` / `dvg_host_viewer`；宿主不另写人物标识或决策规则。玩家候选与 AI 规划最终进入同一 Prepare/Complete，控制方式不在在途动作中切换。

Main 的导出参数与 `--dv-controller=player|ai`、`--dv-viewer=`、`--dv-profile=`、`--dv-zone=`、`--dv-seed=` 配置同一入口；`--dv-auto-start` 仅触发正常 start，不选择动作。GameEngine 串行消费输入，后台完成 DV 状态转换和观察者/执行主体快照，主线程读取缓存。`dv_action.execution_snapshot` 仅供现场执行，不送入观察者正文。

Main → DvFieldExecutor → WorldView 使用唯一保留的人物实体执行移动。离屏场地使用独立 World2D 的同一地图、寻路和碰撞代码；切图保留真实实体和终点，初次物化核对声明地点与合法出生点。回执核对主体、序号、动作、地图、房间及显式站点到达误差。缺资源或绑定返回 blocked，不以逻辑执行器替代几何到达。唯一 Complete 在执行器返回后提交，普通现场信号不重复结算。

显式保存/恢复拒绝现场 pending；异步存取等待后台工作后再次核对。关闭等待后台工作并保留现场 pending；错误停止继续准备并记录到关闭证据。用户输入与显示记录先保存，原始会话和存档仍流式封存。工程构建与界面专项观察分别报告，不据此宣称完整剧情或五日达成。


本轮附件整批修正遵循根目录最新顺序：源码、声明、设计/工程文档及静态审读全部完成后，才统一构建、探针和冻结会话。任务阶段为 artifacts/partial-bootstrap-core/takeover-repair-20261018/batch-correction-20261018/；旧矩阵不改写。此顺序覆盖下文历史阶段允许的中途工程检查。

手动候选以 action.manual_only 声明；dvp_eligible_base 只在临时 actor.manual_input=true 的只读资格视图中开放。dvs_player_options、dvs_select_player、dvs_player_choice 共用该视图，存回状态仍是原主体；自动规划不设置该字段，其他本人来源、现场与规范资格保持相同。内容询问仍是有来源的实际动作，不能声称新增手动选项使全部AI轨迹不变。

policy.known_opportunity_wait_choices 为有来源的未来等待补充个人选择：target_time须严格匹配实际生成的已知机会，available_when须本人为真，personal_choice.basis须非空。仅这种显式声明允许等待与当前责任同场比较；多个有效匹配报歧义。原等待费用、时间片与实际Complete保持不变，不用未来答复压制眼前动作。

建议通过speech.advice声明并在实际listener_parse后登记；advice_decision是本人沿既有价值/动机评估的付费回应，只有实际成功Complete更新stance。接受、执行和因果归因分别记录。后续行为账本核本次实际selection晚于parse，不能冒领已经排队的行动；没有独立证据的关联明确标post-advice-matching-behavior/causal_attribution=not-established。当前建议记忆随认知截断清理，历史账本不补回认识。普通重复话语须显式repeatable且非once/response_once，仍走共同资格。完整字段与源码索引见core/README.md。

本轮追加反事实比较（2026-10-18）：用户要求完成设计文档中的测试。集中完成接管修复并只读审查后冻结源码，对两种侦探配置与四个初始区分别运行一次最多180秒的独立正式一级会话，主代理串行测量，配置间不改源码；每组由新上下文代理审查实际证据，审查者不读取设计。具体关键节点、范围与版本记录于本任务目录 COUNTERFACTUAL-CRITERIA.md，未覆盖的后续分支保持未知。本条为本轮新增矩阵授权，历史单次窗口与中断记录继续保留。

真实 Complete 以已结算 execution.time 推进本人 mind.time；已有且经声明 clock_ref/本人 bindings 映射的数值时钟取得同次执行回执观察。该观察来源为 perception/execution-receipt，同字段原观察合并，成功与失败均不重复加时；未知/未绑定时钟保持未知，不读取其他权威字段、不在Prepare或预测同步、不全员同步。初次失败窗口与修复后的新来源分开封存。

rt_json_write 目标以 .gz 结尾时使用无损 gzip，解压为完整原JSON字节，仍先 .partial，压缩关闭后发布正式文件。run_debug --trace-compression gzip 明确记录文件名与存储编码；默认none保持原JSON。保存时限包含逻辑与封存，不借压缩改变时域或宣称剧情达成。

显式 `run_debug.py --raw-format dag` 启用在线冻结原值日志，和JSON压缩互斥。事件冻结后先提交原始DAG再发布；节点先定义、事件末提交，帧摘要连锁。initial保存完整会话，正常close后final保存trace与last_live_session，seal后才发布正式文件；默认JSON没有记录器I/O。`tools/raw_dag.py`只读校验/恢复指定事件、节点或checkpoint，以SQLite临时偏移索引限制内存。中断只提供已验证提交前缀，不补终态或在途计算。UTF-8直写和单次流式导出不改变默认JSON值编码；索引引用stdout行、批内位置与raw_ref，不重复模型大值。CLI、ABI与失败边界见core/README.md及compiler/README.md。


## 接管修复工程合同（2026-10-18）

调度不按一次无候选结果长期休眠主体；同一游戏时刻仍用 decision_time/revision 防止重复准备，推进后重新处理实际收闻、同处变化与共同选择。第二章按本人认识、时间和章节刷新目标，取消只覆盖调查字段的刷新签名；实际到起居厅前保留必要旧章目标。

目标可声明 navigation_room、navigation_requirements、navigation_methods。日常、沟通与调查共用一个已知开放路径的首步意图，本地优先须对应实质方法通过共享资格门。下游通路未核实时仅可接近其安全侧，不预测开门、他人响应或任务成功。第二章恢复两层规划，费用、工作预算和正式判定不变；本轮不作同条件提速声明。

Runtime JSON 完整序列化输入值，不按 history、simulations、adoptions、world 或事件名称清空字段。debug 投影明确保留假设、预测、比较、剪枝证书、源采纳与停止理由，大世界/主体快照以原始 trace 为准。保存超时保留部分文件并记为未完整封存。

付费重演缓存采用保守重比门：只有已完成且无执行失败的计划，候选、本人revision/time、采纳、响应模型、有效Replay_Depth及模型起点完全一致时才重比旧预测；否则重新调用同一付费dvs_model，不更新旧模型来源冒充重新推演。新增来源字段缺失时缓存失效。停止诊断分列no-declared-plan、declared-plan-incomplete、declared-plan-complete、budget-exhausted与replay-depth-exhausted，不以停止标签替代plan_complete和实际比较。

run_debug.py 新增 actual-event-index.jsonl、reasoning-evidence.jsonl、evidence-summary.json，含源指纹、行号和字段缺失状态。--evidence-from <既有events.jsonl> --out <新目录> 只流式整理历史投影，不构建、不运行、不补猜缺失字段。result 分列停止、时域、章节摘要和 narrative_completion，正常退出或到达时域不自动建立剧情达成。本任务继续使用 artifacts/partial-bootstrap-core/takeover-repair-20261018/；旧报告及证据原样保留，以新勘误记录区别。

本目录继承仓库根AGENTS.md。语言名为DuelView（DV），源码后缀为`.dv`，实现位于language_core。

## 单一动作实现（2026-10-14）

本节覆盖本文及旧文档中的 r20、旧 Core、解释链和旧工具入口描述。用户要求清理整个项目的旧动作实现；旧源码只能从 Git 历史查阅，历史 artifacts、保护基线、`.story` 与译文保留，不重新编译成可运行旧路径。

唯一正式动作链是 `verification/session/30-runner.dv` 的 prepare → 执行回执 → complete：DV 负责主体决策、报价、权威效果、观察、传播与结算；Godot Main → WorldView 只执行移动并返回现场结果；非移动使用同一 complete 的 DV 世界法则。命令行 formal-model 是同一接口的逻辑执行适配，不含 Godot 几何，不能冒称实机验收。预测与推演不发布真实行动效果。

宿主只保留 GameEngine → DvSession → 生成 DV 类库，不保留旧坐席 token、DispatchField、舞台动作分派或第二个 DvRuntime 会话持有者。界面呈现完成不单独触发 Prepare，只有 view_ready / dv_next 启动下一步；等待回执时不重新分派意图。

构建：`dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers -p:NuGetAudit=false`；必要时用 `-p:DvPython=<Python路径>`。DV 编译器自举与编辑器继续使用现有工具，seed 只持有语法 Reader/Value，不含世界执行或解释器。两份解决方案均引用 Session。

旧 verify、FullTest、guarded_level3、PlayerTranscript、回放与旧会话检查入口已移除，不再执行。原保护清单和原始证据保持原字节；本次架构清理不是同条件耗时优化，不建立或重置测量基线。一级现行无窗口逻辑输出入口见本文件新增规范，三级保护运行工具仍待按 DV 合同重建；本轮不以构建或短启动记录代替任何完整验收级别。

只读逐文件审计和清理报告继续放在 `artifacts/partial-bootstrap-core/single-action-path/`。禁止流程回归以及以计数评价游戏的约束继续有效。

## 文档

现行语法、特性与接口统一入口为 [core/README.md](core/README.md)。修改编译语法、Core/session 声明字段、机制或生产接线时，同轮更新该参考、源码索引与本文件涉及的合同；设计目标、工程参数与实际证据分别标识。

SYNTAX.md维护完整书写格式，DESIGN.md说明语言与编译架构。设计和指南直接描述语法、机制、模块和命令。第一章源码需求保存在artifacts/partial-bootstrap-core/chapter1-reverse-language-requirements-2026-10-13.md。

surface-v2 的可执行前端位于 compiler/compiler.dv：缩进、Unicode 标识符、define 别名、函数/局部语句、记录/列表、中缀运算、mapping、acceptance 和有限域 imprint 声明展开后复用 C# 后端。Core、正式 session、checks 与 chapter1 内容已经使用新表层语法。编译器自身保留 S 表达式启动源，C0→C1→C2→C3 继续自举；通用泛型、单位推导、完整能力类型和目标无关 Typed IR 尚未实现。

compiler 中 ck_* 执行名义类型、字面量范围与可变性检查；core/40-value.dv 提供接纳分类、有限域配平搜索及独立证书核验。Runtime.cs 只提供基础有理数、Unicode 与容器 ABI。求解器的最优声明须经 DV 核验器重新检查覆盖、可行性与成本；正式提交一起结算认知、资源和搜索/核验工作量。核心标识符和声明关键词使用英文，其他自然语言用文件头 define 映射。

## 模块

| 路径 | 职责 |
| --- | --- |
| compiler/compiler.dv | 扫描、解析、名称/调用处理和C#输出 |
| compiler/seed / Runtime.cs | 启动编译器与平台ABI |
| core/*.dv | 世界、规则、观察、传播、价值、回应、费用和论证 |
| verification/session | 正式DV逻辑会话 |
| verification/scenarios | 原生人物、世界、话语、公理实例及配置 |
| verification/checks / report | 只读判定和中文报告 |
| tools | 构建及进程编排 |
| Session | 从 core/session 的 DV 源生成的独立宿主类库；C# 仅封装序列化与对象所有权 |
| editors/visualstudio | Visual Studio 2022 的 DV 诊断客户端；语义检查复用 DV 编译器 |
| editors/vscode | 用户指定的 VS Code 客户端；官方语言客户端连接共用 Python LSP，npm 打包 VSIX |

core、session及编译器的算法使用DV。平台ABI承接数值、容器、文件、控制台和最低执行适配。正式 Godot 项目已改为引用 Session；现场呈现与章节通行仍在集中修复。

容器深拷贝由平台ABI的rt_copy_value执行；DV负责记录更新、推理与规划。记录查找使用rt_record_index/rt_record_find从末项查找，重复键保留末项优先。性能修改通过第一章新会话记录具体推进与实际进程耗时，出错直接修正对应实现，不运行回归套件。

可选性能诊断使用 `python language_core/tools/run_debug.py --profile --out artifacts/partial-bootstrap-core/<新目录>`。`build_session.py --profile` 只在生成的诊断程序集内插入 DV 函数计时，生产类库不计时。原始计数在 `profile-raw.json`，`profile-summary.json` 按工作类型和函数列出调用、独占/包含耗时、平均耗时及独占占比；平台 ABI 费用归属直接调用它的 DV 函数。插桩耗时和 formal-model 范围必须与原生运行区分，不能据此推断实机帧率或游戏验收。

规划评分对每个预测节点建立一个只读人物视图，复用静态动作与政策；评分不写入该视图，正式人物状态仍由会话更新。

已知机会等待只在规划根层比较，避免在同一次预测搜索中重复等待；扩章内容按主体是否实际到达起居厅决定旧章动作和目标是否继续携带，第二章学员规划深度上限为 2。首夜等待片段、天气可见字段与2040分钟后的候选清理见 verification/AGENTS.md；行为效果由新正式逻辑会话记录核对。

专业回复可以声明offer_to；本人支持话语且看到该接收者时，可主动说明一次。已经实际收到的询问仍沿请求回复处理。

## 内容和机制

新内容采用具名axiom、defn和领域构造函数；原生DV执行后导出JSON作为交换数据。oracle为独立内容包，主体包引用公开合同。旧.story提供迁移来源并继续服务现役宿主。

本人推知、预测与物理执行分别使用infer、predict、enforce采纳。理解、采纳、模型、论证和实际行动保留各自来源。decision显式表达planning和admission；各旧入口的评分、门槛、平分与首步选择在对应DV库中迁移。

正式答辩分别记录公共结论等级与主体自己的接受回执。主体接受记录随实际交接更新，规划依据本人回执排除同等级重复提交；只读判定使用提交前的本人记录核对，不以他人的公共结论替代本人的作答。

session/10-subjective.dv记录实际收闻的专业请求、本人回复及同处可见人物。请求不直接取得场景命题；回复由另一主体的正式行动、本人认识与实际听觉传播形成。公开动作观察按声明范围与实际同处过滤。规划将方法前提纳入依赖，并在整个搜索中保留预测状态的分数/耗时非支配路径；重复状态不重复展开。

开场内容的本人材料目标以持续公开信息的本人实际收据激活，阶段更换不抹去动机；专业证言仍要求答复者自行取得认识并正式发言。规划中的答辩话语资格与执行时使用同一本人真值检查，未知话语不作为已可执行的答案。

## 命令

```powershell
dotnet build language_core/LanguageCore.sln -c Release --disable-build-servers
python language_core/tools/build_core.py --out artifacts/partial-bootstrap-core/domain-build-<new-run>
python language_core/tools/bootstrap_compiler.py --out artifacts/partial-bootstrap-core/compiler-<new-run>
python language_core/tools/run_verification.py --config <process-plan.json> --out artifacts/partial-bootstrap-core/<new-run>
python language_core/tools/check_dv.py --format json <source.dv>
python language_core/tools/dv_lsp.py
```

## 工作安排

源码和目标生成物由对应DV构建链连接。架构、接口和命令变动同步本文件；内容设计合并至docs/design-standard.md。阶段成果继续使用artifacts/partial-bootstrap-core/，每轮修改按根流程提交与推送。

run_verification的普通编译/会话执行持续写入当轮stdout/stderr原始文件，结束后记录进程时长与来源封存。可在会话期间查看已经实际发生的行动；完整停止轨迹与只读判定另行记录。

用户2026-10-12停止独立Agent复评的裁定继续适用。新语法运行使用 verification/run-surface-v2.json 的单次正式会话计划；不默认执行旧多场景计划。


当前机器缺少 .NET 8 运行时而有 .NET 9；本轮记录显式使用 DOTNET_ROLL_FORWARD=Major 运行 net8.0 产物。源码仍以 .NET 8 为目标。NuGetAudit=false 仅关闭本地构建时不可达的漏洞索引查询，不改变 DV 检查器或游戏条件。

刻画的 Core 接线由 dv_imprint_prepare/apply 完成：从真实 mind 字段建立配平坐标，按证书写回认知并通过既有 90-action-cost 账本结算；工作额度单独返回，由宿主和认知、世界、账本一同发布。

编辑器检查通过 dv-check.json 选择编译组，逐文件保留 define 作用域和位置，再交给 C3 编译器的 --check 模式；检查不执行内容。tools/dv_toolchain.py 按启动源指纹缓存自举编译器；tools/dv_lsp.py 处理未保存缓冲区和过期诊断。Visual Studio 和 Godot 客户端不另写语义分析器。运行封存覆盖整个 language_core，正式会话运行期间也不要修改此目录的文档或插件文件。

主体内部引用通过 dv_same_local_ref 比较：省略主体的字段解析为当前主体，显式其他主体不等同。推知依赖以法则和输出字段标识，重新推知替换该来源；事件 ID 继续标识实际发生的推知。证据支持份额与来源可靠度分别保存，默认置信度不超过最强实际来源的可靠度。

session/25-simulation.dv 从本人已采纳法则的读写依赖逆向选取证物相关演算；切片仍遮蔽原计划全部输出，比较消耗真实工作预算。切片吻合不能建立完整解释，矛盾必须带实际预测及本人观察来源。session/10-subjective.dv 将本人有来源的价值排序用于同组选择；行动前提、传播与账本仍使用共用机制。跨房间请求由本人已知声道条件取得候选资格，实际接收仍由 speech 的听觉传播判定。

生产编译由 game/entry.dv 的相对 #include 显式组合 core、session 与内容；章节接线函数保存在 entry.dv 本体。Session.OpenCampaign 创建正式原生内容，Prepare/Complete 经同一章节扩展入口；章节扩展不重新调用 session_open，不清空账本和事件。

生产呈现绑定在 game/entry.dv：身体地点映射场景、房间与素材；客观显示数据不写入人物认识。初始放置只用于创建视图，跨地图回执必须来自现场执行。departure 安装独立门状态，章节内容可用与实际过门分开记录。

正式人工控制以 player_actor/awaiting_player/selection 保存在同一DV会话；候选只由本人现有认知、动作前提及共同资格判定生成，选择后仍走原prepare/complete和原费用/判题。人工等待不跳到他人席位、不注入行动结果。人物已声明、带来源的沟通动机可主动发出同主题回复，仍受本人真值、使用记录、机会与实际传播限制。该接线修订单独封存差异与独立审查，不作为旧基线同条件性能优化。
控制模式的会话外壳用显式字段保留，与原会话返回合同一致；不为携带player_actor深拷贝含完整历史的state。等待选择后恢复不重复认知；空候选按原idle/轮转，不制造空菜单暂停。
生产呈现通过dvg_presentation在原可见性过滤后绑定原生人物名称；人工重新询问仅展示本人实际观察到的当前问题字段。听觉候选接收者与实际听觉可达性分别声明，候选名单不会生成收闻。
规划把有来源且属于本人的未完成沟通目标的可达机会单列opportunity；只用本人已采纳predict法则核对available_when与后继方法前提，不预言他人接受、不发布事实。每个预测路径按目标索引只计一次，机会键参与去重，证据包含目标与预测来源和前后可用性。预算、费用、达成与判定均沿原合同；规则变化另封存新来源，不作同条件提速比较。
上述opportunity为access_only：仅本人的移动predict写本人地点并抵达后继方法显式地点，保留全部非空间资格。method_eligible独立记录本人预测下的完整资格，参与者不作预测；到场后重查正式资格。16-personal-choice的eligible_after_base仅拆出原base后条件以复用，不改正式资格。
接近机会也覆盖已经available但方法地点尚不可达的本人目标；本人原地点必须不同于方法地点，不增加目标完成分。原生内容的远端通信方法可声明本人端口地点而非远端身体共处，实际请求传输与听闻仍独立核验，不预置收听或答复。
预测路径从dvp_initial_access建立已在场目标地点集合，起点已有的接近机会也占去重键，禁止先离开再返回领取同一接近分。
路径循环检查使用预测state加achievements，单独的access键不使同一认知状态成为新进展；跨路径非支配key继续含access键。实际r9证明纯接近绕路曾被加分，不改权重，分开循环与历史去重后再新会话核对。
循环key进一步只取预测认知state，不含位置型暂态achievements；累计字段仍仅参与跨路径非支配比较。到目标位置本身可估值，绕回相同状态不能据暂态访问继续展开。
同处请求上下文采用实际看到的地点和本人行动前提输入，不再把接收者任意last_action_event当作重问依据；已发同条件请求仍保留，新的本人前提或不同现场可重问，不改实际收闻与回复资格。

请求输入专用签名保留引用、已知/值/争议及前提真值，过滤观察时刻和来源事件编号；声道请求同时保留方法前提与声道可用前提。目标、主题、地点/声道仍区分，不改仿真与论证的完整输入。这只稳定去重，不替代候答与重试生命周期。总模型预算耗尽后不接纳新的付费重演；上一方法工作余额不作为下一次方法可行性的门槛，现有有效缓存仍可读取。

原生交互方法可声明 travel.station；呈现绑定将逻辑目的地与当前地图执行房间分开，成功现场回执之后才执行权威接触法则及本人来源观察。预测只写声明的接触状态，不预置观察结果；本轮自身交互历史的空初值不能夹带历史谜题信息。此类接线修复另立指纹，不作为旧规则同条件提速。
根层存在正式可执行且净正值的非移动行动时，先完整比较一层生成的后继，再保留所有已比较路径中的最佳首步，包括移动，记录positive-current-after-successor-layer、根层与后继比较结果、实际完整层数。深度不足、没有后继与预算不能覆盖整层分别记录，不冒称完成后继比较；没有正值当前行动则继续原多步搜索。该有限理性停止策略不保证全局最优，不上调预算、不改变费用与答辩判定，另立来源基线。生产动作结果文案仅在原生game/entry.dv按本人已获事实条件呈现；重演摘要只读本人当前有效模型，吻合不等同正式答辩。答辩回馈来自实际answer_accepted/rejected事件。重新询问观察执行时的公开题目字段及真实开放状态，不预置答案，也不冒称另一次独立话语回复。

阶段交接回执只更新主体实际收到的字段；若主体错过后续公开问话，内容目标以本人收到的阶段时刻为证据下界，驱动本人在现场重新核对当前公开题目。正式听闻与重新核对分别保留实际来源；未听到的问话不自动记为听闻。重新核对的公开字段属于直接观察，不伪造第二次独立话语行动。

移动被选为多步路径首步且实际成功后，主体暂存该路径的下一非移动行动；下次决策仍完整运行本人认知和规划，只有新规划把该行动列为当前直接可执行且净正值时才执行，并记录被替换的路径。失败移动、失效资格或没有正收益时丢弃暂存；不预执行预测结果，也不更改答案或行动费用。

线性宿主会话转交使用 session/30-runner.dv 的 dvs_owned_state_update：只新建顶层记录与字段对，沿用已归本会话所有的字段值引用；调用者放弃前态，用于 campaign 扩展后的会话重绑定、Prepare 主体列表替换及行动队列的会话外壳更新。完整保留未知字段、pending、initial 与输入控制字段，不原地修改旧记录、事件条目或快照。不得用于 world/mind、预测、规划分支或证据快照，不改变其独立复制合同。扩章内容可用仍须与实际几何跨门分别验收。

2026-10-15：批量初始化可使用独占采用表构建器，先复制继承表，每条声明仍立即执行原验证顺序，再仅追加独立条目；finish核对旧前缀与长度，修订号按成功追加数增长，一次持久更新世界，空批保持原世界。中间采用表不得发布、分叉或当作任意更新入口，world/mind/预测仍保持原独立复制合同。
证据目标可显式声明 refresh_until 与 refresh_refs；仅本人尚不知停止命题为真时，以 refresh_refs 中本人已知相关字段的接收时刻和原 minimum_time 的最大值刷新证据下界。缺少 refresh_refs 时保持原下界。本人时钟、无关观察和检视本身的时刻不触发刷新。收到信息与采信内容分别处理；不读取 physical 或预测历史，不改旧观察、认知、预算与判答。
Session新增流式WriteTrace/WriteSave，Trace的精确数仍为原字符串，Save仍为$dv_rational对象标签。宿主关闭等待在途工作、先保存输入与可见记录，再后台流式写原始会话及存档；先写.partial，成功才改为正式文件名，失败保留部分文件。SessionClose在该关闭路径仅调用一次，不自动重试。close-result.json明确各阶段错误与complete，TraceClosed仅表示关闭结束，不能代替证据完整性判断。新建证据目录不得覆盖既有目录；异常也必须释放关闭等待。必要工程检查对照中性值的序列化、别名、验证首错与目标新鲜度，不作游戏评价。


## Evidence and campaign tracking (2026-10-16)

All eligible autonomous actors enter shared cognitive preparation and dvs_choose on their turns; identical completed fixed points may be reused under the documented full-input guard. The incomplete idle cache and its writes are removed. Existing resource/time limits still apply; tracking never filters scheduling.
Execution, observation, answer, transition, model-recheck and simulation events retain the requested full snapshots; simulation results include predicted. Revision metadata supplements snapshots. Other event producers remain subject to separate audit.

The game/entry.dv helpers dvg_tracked_actors and dvg_chronological_trace project selected actors' events in append order. dvg_run_campaign(max_steps) runs the same formal-model prepare/complete protocol and continuous chapter extension, returns live state, and stops at the completed-action bound or session inactivity. Pass false for the authored action limit, or zero for an unadvanced session. This is audit data, not player-visible knowledge or Godot geometry evidence.
Session.cs exposes internal CampaignTrackedActors/CampaignChronologicalTrace/CampaignRun bridges. python language_core/tools/build_session.py --out language_core/Session generates an inspectable copy; the project excludes that root DVProgram.cs and builds obj/dv instead. Never edit generated copies.
Engineering probe evidence is saved under artifacts/partial-bootstrap-core/single-action-path in new paths. Build/tracking evidence does not certify full gameplay acceptance or performance improvement.

## 一级逻辑输出与调试入口（2026-10-16）

生产入口 game/entry.dv 使用 `#include 相对路径.dv`，路径以包含它的文件目录为基准。tools/dv_sources.py 为构建和编辑器共同解析文件依赖，依赖先于引用者编译，同一路径只加载一次，循环和缺失文件报告位置。每个文件保持独立 define 作用域；引用行保留空行以维护诊断位置。这是文件组合入口，直接调用旧编译器裸文件接口仍需先组合。

game/entry.dv 的 dvg_debug_config 配置观察者、报告主体、事件类型和诊断行动上限。空筛选包含全部，max_steps=false 使用会话原上限。配置只选择输出与诊断停止位置，不修改调度、认知、费用或世界条件。core/95-model-argument.dv 按原事件顺序只读投影，保留事件与父事件编号、行动、来源、结果和已发生观察；完整快照保留在原始轨迹中。报告不可当作人物知识。

`python language_core/tools/run_debug.py --out artifacts/partial-bootstrap-core/<新目录>` 从 game/debug-run.dv 建立一个正式逻辑会话，沿生产 prepare/complete 与章节扩展运行；生成原始 stdout/stderr、完整 trace、来源指纹、事件 JSONL、中文调试轨迹及 02 可见正文。工程时限默认600秒，可用 --timeout 指定；超时保留已输出部分并记录未完成。一级仅运行无窗口DV逻辑会话，不启动Godot；源文与实际轨迹分别核对，03不属于一级要求。停止于行动上限或短时范围不表示通关。

rt_json_write(path,value) 仅为流式 JSON 文件 ABI：先创建新的 .partial，序列化成功后改为正式文件，拒绝覆盖，失败保留部分字节。保持原数组记录格式与精确数的字符串编码。编译器声明二元内置函数，内容与调试决定仍由 DV 实现。

调试投影独立复制所选字段，调用者修改输出不影响原事件。可见正文中的主文与附加行分别使用 surface_voice/voice；空正文保留在format-notices.jsonl与原轨迹中，不编造台词。旧记录缺少类型时标明源记录未分型。来源指纹包含实际组合模块、编译器Runtime及入口工具。

## 等价复制优化（2026-10-16）

连续独立字段更新可合并为非空 dv_with_many，保留字段顺序、最终值与各输出分支独立性；合并前审查表达式求值没有修改输入的副作用。dv_store 的 dv_with_path 沿更新路径一次构造，其余分支仍独立深拷贝，重复字段遵循原末项读取和全项更新语义。

法则暂存事务 dv_stage/dv_infer_stage 与观察批次 dv_update_mind 先独立复制输入世界；私有 dv_stage_store_owned 仅修改这份尚未发布的副本，按原次序验证，逐次复制前后单元格。失败丢弃暂存结果并返回原世界的独立副本，成功转交暂存世界。观察批次只在独立副本中追加已见标识，末尾一次构造时间、修订和历史；保留逐条验证首错。这个局部构建器不接受既有 world/mind、预测分支或证据快照作为独占输入。

session 的 dvs_event_prepared 与 dvs_event 都发布完整不可变快照，存储与复制合同见下方“不可变证据存储”。

性能比较在独立目录冻结相同内容、配置与行动范围，分别记录运行时间和原始输出指纹；内容修订另做正式逻辑会话。有限行动范围的相同轨迹只证明该范围等价，不代表整局提速或一级验收完成。

run_debug.py 的正式逻辑进程默认使用 server GC，最多四个回收堆；--gc-mode workstation 可还原原模式，--gc-heaps N 指定上限。只并行内存回收，人物决策与世界提交顺序保持原样；运行记录保存请求配置及 GC 环境。编译进程保持原配置。该测量不证明 Godot 宿主已启用相同回收模式。

## 行动时间队列（2026-10-16）

正式会话在主体 scheduled_action 中保存 action、start、due、成本模型、选择所需引用及来源事件。Prepare 先扫描队列与可决策主体；等待者不进入认知和选择链。到期行动重新按当前物理世界报价，由宿主回执进入同一 Complete。scheduled_start 使 action-cost 只结算一次费用；世界时钟已经等待的部分不再累加。当前执行回执的时间必须等于排队时声明的时间，变化时明确报错，不能默默重排或倒拨时间。无排队起点的 Core 调用保持原即时结算合同。

等待推进按可分析时钟边界执行环境法则，变化尚未稳定时逐分钟结算，实际效果继续在 Complete 后产生。相同时刻按循环主体顺序处理到期项。空闲主体同一时刻在本人认识修订与收闻均未变化时至多尝试一次；零可用行动与仍有排队行动分开判断。队列和延后收闻随主体保存、扩章与恢复。pending_heard 只存实际听到的话语及当时声道证据，到期后理解；不重新检查已经结束的声道，也不让等待者执行话语理解模型。

dvs_owned_state_update 的线性会话外壳转交范围增加调度 cursor、physical、events、idle、actors、pending 更新；它仍不修改既有字段对象。队列只存 Complete 所需 relevant_refs，不嵌入整个规划搜索结果。事件保留原独立快照合同。refresh_refs 将复查目标限定到本人已知的相关字段时间；内容列表重建不自动刷新所有观察目标。

本轮改变行动时间模型与内容耗时，属于新行为基线；旧短窗口倍速不用于本轮性能结论。一级使用无窗口正式逻辑会话，未启动 Godot 的结果不证明画面或几何验收。

调试入口 phase_progress 仅输出认知与选择的开始、结束边界，engine_phase 不进入世界或人物知识。它用于辨别尚在计算的阶段，不能当作完成行动。

接近沟通地点的 opportunity 是当前搜索叶节点的预期机会分；进入下一层时仅携带实际行动价值，不把尚未完成的沟通机会累加到无关后继上。可重复检视在成功后保存本人输入的语义签名与显式证据新鲜度；输入与所需证据条件相同时不再次自动检视，未知结果也属于已尝试结果。签名不读取物理世界，不以时间戳变化或无关信息伪造新线索。

每次主体具备决策资格时，plan_budget 以会话开局保存的 planning_limit 作为本次搜索上限，choice.plan_expansions 仅计算行动树节点，从该次余额扣除并计入 planning_work_total；依赖闭包使用独立dependency_budget，工作单列dependency_expansions/dependency_work_total。等待者不重置或执行搜索。此上限限制一次搜索的计算规模；动作账本、付费重演额度与实际游戏时间分别结算，保持原有余额合同。

同一时刻的空闲主体在本人认识修订或待理解收闻发生变化时可再次决策；decision_revision 记录一次实际选择结束后的本人修订，防止内部推理产生的修订造成自唤醒循环。scheduled_action 优先于这些唤醒条件，等待中的主体保持不计算。

## 不可变证据存储（2026-10-16）

rt_snapshot 是平台值 ABI：递归复制普通列表为不可变列表，按结构共享相同的不可变子树；哈希碰撞逐项核对，弱引用池不保持已释放快照的生命周期。原始数组记录结构、字段、精确数及完整前后快照保持不变，JSON 不引入引用编号。运行时 rt_push/rt_replace 拒绝直接修改快照，rt_copy_value 继续产生各分支独立的可变副本。源列表后续修改不影响快照。

dvs_event 与 dvs_event_prepared 都通过该边界发布完整只读事件。后者的旧独占转交只是构造优化，不再是可变事件存储合同。事件消费者用 dv_with/dv_clone 派生可变值，不修改事件本身。Session 的存档与流式输出保持原格式。该优化独立于行动调度和搜索额度修订；中性序列化、源别名隔离、快照修改拒绝及复制后可编辑分别检查，不能用来宣布游戏验收完成。

正式 Complete 使用 dvs_speech_owned 在线性会话中替换实际收闻者，沿用未变主体；调用者放弃前态。普通 dvs_speech 仍先独立复制输入主体列表。两者事件均经不可变快照边界，不共享可变历史证据。

rt_copy_value 的独立复制采用延迟分支实体化：返回独立可变根，子树先以不可变快照保存；rt_at/rt_record_find 从可变父节点取得子列表时，只在该父节点内建立独立可变分支。从不可变快照读取继续只读。DV 的嵌套索引、字段读取和 rt_replace/rt_push 统一走这些 ABI；原值、兄弟副本和事件快照保持隔离，失败事务仍丢弃自己的分支。列表顺序、重复键、精确数与序列化格式保持原样。C# 宿主仅序列化值，不绕过 ABI 修改 Items 返回的列表。

延迟副本保存其未变内容的不可变底本。可变子节点用弱父链接记录失效传播；rt_push/rt_replace 使该节点及全部可变父节点的底本失效，纯读取时的分支实体化保持底本有效。快照复制可复用有效底本。较长不可变记录可缓存末项优先的文本键索引；可变记录仅在底本有效时使用，键修改沿同一失效链撤销索引来源。原始非文本键和不规则记录保持原线性查找语义。C# 的原地值修改同样须经 ABI。

rt_same_value 提供结构值比较 ABI，按列表顺序递归比较，沿用 rt_eq 的精确数与浮点数相等规则；相同不可变底本可直接确认相等。比较直接读取已有子项，不实体化可变分支。不同哈希不直接判不等，保留精确数/浮点数及正负零的值比较语义。

较长可变记录也可建立文本键索引，直接记录各字段对的弱父链接；键、字段对或记录的 ABI 修改沿同一失效链撤销索引。建立索引不复制字段值，不规则记录继续按原顺序线性查找。

rt_record_with 是有序记录的独立复制更新 ABI：原顺序逐项检查，只替换第一个同键项，无同键时追加；rt_record_with_many 更新全部同键项。未变项保存独立不可变底本，读取时按复制合同实体化。错误顺序、重复键与分支隔离保持现行源码语义，验证和世界法则继续由 DV 实现。

run_debug.py 在编译前封存包含文件、编译器、运行时与构建工具指纹；启动会话前核对原始指纹、编译模块归一化指纹及实际复制的 Runtime.cs。构建期间变更源码时记录错误并停止，不运行混合版本会话。

## 声明式认知与连续内容维护（2026-10-16）

生产配置入口为 `dvg_open_config({seed, profile, detective_zone})`；配置安装在 `cognitive-profiles.dv`，具体参数属于内容声明的工程初值。开局只抽样声明域并保存选值与种子，恢复存档不重新抽样。`Replay_Depth` 限制有序因果重演阶段，`plan_depth` 独立限制行动规划；重演截断不能认证完整解释。

共鸣资格、公开立场冲突、私人执行效用及病理调度接口位于 `session/10-subjective.dv`。主动取回只在成功 Complete 后记录耦合与声明的信任变化；被动触物观察不使用取回结算。物理侵蚀读取权威暴露字段，人物风险判断读取本人认识。公开立场冲突只来自实际收闻记录；预测不生成操纵后果或真实立场证据。

连续内容安装保留原世界、账本、主体、排队行动与事件。定时内容只开放机会，实际交谈、协作、取用、签署由正式行动产生。扩章与实际移动分别记录。等待使用明确费用的普通行动，沿原队列按可分析时钟边界结算环境，变化尚未稳定时逐分钟结算；下一机会只从本人有来源的声明目标取得，估值参数在候选上记录。扩展内容时同步维护其时域、机会刷新、资源账及精确费用，不能用提高行动预算或提前写入成果代替可达性修复。

纯值备忘 ABI rt_memo_get/put 以完整不可变输入底本和命名键区分结果，源修改沿已有失效链更换底本；返回值独立复制。每个输入按键的首个文本项分区，每个分区最多保留512个键，按插入顺序淘汰，弱输入表不延长输入生命周期。dv_truth 的纯命题计算及具有完整输入键的规划查询使用该机制；命题查询的完整世界和命题均入键；命题演算和主体访问校验仍由 DV 执行，错误不缓存。该接口不用于带事件、资源结算、采信或执行副作用的过程。

## 实际资格与规范回执维护（2026-10-16）

本人资格、直接候选、预测搜索及同组个人选择共用门槛。主动效用所读本人引用进入依赖签名；成功完成账本 completed_actions 与包含失败的 used 分开，前者仅由 applied Complete 追加，不由预测写入。实际收闻替代项必须逐项保留来源和理解回执。

norm_proposals 记录已实际获得的规范内容，norm_stances 与 policy.norm_rules 只由本人成功完成的 norm_decision 更新。读板/听闻、理解、采纳和执行的来源事件分列；不把公开提案当作听者同意，不以观察事件编号充当执行编号。规范候选匹配本人完整内容收据，实际选择仍通过共同价值比较。接口与源码索引同步 core/README.md（本目录下为 ../core/README.md）。

统一修改任务先完成源码、声明、文档和静态审读，再启动一次正式一级会话。调试 stop_evidence 只读列出行动窗口、会话上限、各主体配额、认知余额、队列和时域；限额停止与剧情达成分别报告。配额保留来源，不为促成预期关键节点重置人物资源或扩大现有上限。

## 数值类型与解析边界（2026-10-16）

2026-10-17 的第一章连续内容修订由用户明确授权把各主体行动配额设为 240；此数值作为新会话工程条件，扩章时沿用剩余额度和既有全局行动数，不重置主体。`max_actions` 按本次安装的主体配额求和。与此前 48 配额会话分列记录，不能作同条件耗时改善结论。成功移动的公开观察使用真实 Complete 的出发位置或终点同处，授予真实执行后的字段；答辩公开回执只传给实际可听者。接口细节见 `core/README.md`。

`policy.wait_chunk_limit` 是主体可声明的单次等待片段上界，默认 180 游戏分钟；等待仍使用普通行动、真实排队和原费用。内容需分别表示物理闩开启与授权放行，前者不自动满足后者；同处操作和铜管通报均在成功 Complete 后传播。

rt_is_number(text) 只检查文本能否解析为有限浮点数，仍供编译器词法扫描使用。运行值检查使用一元 Bool ABI rt_is_numeric_value(value)：有限double与有效精确Rational为真，文本（包括数字文本）、布尔、列表与记录为假，不进行隐式解析。新增平台谓词须同步compiler.dv的arity/返回类型与Core参考；seed沿既有rt_通用发射映射，不增加解释器。

主体数量、规划依赖、物理暴露与表达权重的数值边界遵循同一类型合同。公式、认识来源、成本和成功条件不因类型修复改变。工程值域检查与正式一级会话分列，全部源码修改冻结后统一执行；不以值域结果替代人物或剧情验收。

## 会话上下文与未来机会（2026-10-16）

共同主体资格核验 suppress_after_received_speech 的实际来源/理解回执及 response_once 的成功 completed_actions；未声明的回复保留原请求生命周期。检视语义去重统一用于规划、人工候选与同组比较。未来等待只选本人有来源、未完成且非时间前提已成立的目标，保留原费用、估值和实际执行。修改上述合同同步 core/README.md；整批修订完成并静态审读后再运行正式一级会话。


## 环境边界与规划复用维护（2026-10-18）

等待队列由 `dvs_environment_next_time` 读取已采纳环境 premise 的时钟边界，保留一分钟采样格与实际到期点；尚可变化的进程、增量及级联继续逐分钟结算。不可分析时钟条件回退一分钟。`world_processes.once` 仅在 applied 后挂起；不按规则名称猜测一次性身份。新增时钟运算或环境写入机制时须同步边界分析及 core/README.md。

`dvp_forecast` 只缓存完整 mind 节点上的纯预测，供评分与后继复用。内部规划省略继承 history/seen，保留单元来源及路径去重；真实历史、公共 predict 与重复事件校验不删除。真实更新只在独立 stage 追加日志。规划备忘省略 mind 的审计 history/seen，保留 revision 与所有其他规划输入；新增日志读取依赖须恢复对应键。论证来源先补齐主体增量索引，再遍历本人支持事件。

目标时间元数据由内容构造器预计算，使用时核对 available_when 未变；目标刷新仍逐次检查本人时刻与认知，不按粗粒度时段推迟资格。声明合并与角色目标范围变化属于新行为条件，不能据本轮结果宣称与旧版本同条件加速。集中修改、静态审读和文档更新完成后，只运行本轮授权的一次 `run_debug.py --timeout 180` 一级会话；游戏时域达到 7200 与剧情关键节点达成分别记录。测试不启动 Godot，不运行流程回归。


## 整批动作准备维护（2026-10-18）

`dvs_session_prepare`仍为正式会话入口；`dvs_prepare_actor`是同一prepare中已选主体后的认知/报价/排队阶段。campaign先调度，再只刷新该主体的内容，随后进入该共用阶段；不为内容刷新扫描全员，不在人工已有选择时重排主体，不重复分派pending。改动须同时核对人工控制、无候选、排队到期、章节扩展和宿主入口。

动作目录按已声明地点建立保序缓存切片，未知地点回退全目录，动态资格不缓存为既定结果。私有预测省略审计日志，公共Core与真实世界去重仍保留；话语预选只去掉说话者无关认知，保留听者模型和观察时间先后门。环境时钟边界不向本次due之后的阈值计步；调试投影保留过程及休眠回执。本轮所有动作相关修改和文档审读完成后冻结源码，再执行唯一一次最多180秒的正式一级会话，禁止各处修复之间穿插会话。

同一mind下的检视证据下界与可用性以完整目标声明为键建立纯值索引；各检视仍核验自己的grants与完整前提，不能用缓存跳过本人来源或新鲜度。


## 因果接线、计算上限与诊断维护（2026-10-18）

实际陈述在聆听解析后按原可信度生成证言，不以缺少特殊标签跳过有效陈述。认知快路先处理收闻/现场变化，只复用完整处理前后输入不变的固定点；键保留当次时间、章节、认识、政策、模型、解释、支持来源及同处上下文。晚发生的论证更新须在下次处理，不能认作完成认知。原始实际推知、模型、行动及观察继续保留；只抑制相同零步停止诊断。

依赖拓扑缓存与种子闭包查询分开，闭包保留seeds/独立dependency_budget。plan_expansions只计行动搜索节点，dependency_expansions单列实际闭包工作，完整规划缓存命中两者均为零。独立闭包上限和声明positive_move_root_stop是新的计算合同，报告应与旧共同预算区分，不作同条件提速声明。导航中继只取本人已知开放的路径及本人采纳，不预置到达；完成/位置/通路变化重建意图，所有真实移动仍走原Complete。

调试事件投影为只读冻结值；正式一级继续保留所有实际执行和可见正文，只省略不可见的呈现包与可选阶段诊断，stdout仍直接写原始文件。源码、预算来源、声明、调用链与文档全部完成后再冻结并运行唯一一次180秒正式一级会话，窗口达到7200与剧情实际完成分别核对。

## 连续优化循环与局部复用（2026-10-18）

用户授权每轮集中完成已识别问题处理、接线与静态审读后，冻结源码运行一次最多180秒的一级正式会话；达到7200分钟即停止，否则最多四轮。每轮新增证据并独立审查，保留失败、插桩开销与未覆盖范围，提交并推送；不在改动之间穿插会话。

`dv_with_many`改由通用容器ABI `rt_record_with_many(record,updates)`完成，保持有序记录、更新全部重复键、更新表末值优先、空更新返回原记录及分支独立复制。算法仍只处理容器，不含剧情或世界执行。编译器arity与Core参考同步维护。

整个人物的认知缓存由局部推知固定点键替代：输入包含infer采纳、所有前提/输出字段、历史推导引用、来源单元（仅沿原推知语义剥离time）、推导、签名与剩余推理预算；只有推知前后完全不变且未阻塞时保存。时间条件所读时钟字段仍在键中；语用、假说、准备、论证与话语链继续每次执行，`reason_reused`与`cognition_reused`分列。新增效果读依赖时须扩展此键。

无参静态内容声明可以使用纯值备忘，不能缓存读取会话、时间、世界或人物的构造器。支持性陈述可声明answer_form=supporting-statement，不取得完整答辩奖励；权威判答和人工候选不因评分调整而放宽。询问不携带答复正文，已知其所问命题后不重复发问。姓名只从本人真实介绍收据显示。

诊断插桩每30秒原子保存已完成调用的累计耗时，超时保留最后采样；采样覆盖时长与全程时长分列，插桩运行不与原生运行作同条件吞吐比较。诊断采样只改工具输出，绝不决定人物行为。

第2轮：批量更新先冻结updates再枚举，避免record_find的惰性展开改写枚举器版本。通用二元ABI rt_has_value实现结构值成员查询，dv_has转发，沿rt_same_value的精确数/浮点比较，不展开只读子树、不改变集合或游戏规则。中性验证补充从快照恢复的惰性更新表、冻结更新表和嵌套数值成员判断；第一轮初始化异常独立保留。

第3轮：dv_local按完整bindings值建立分主体索引，重复物理映射仍在查询该身份时报告歧义；dv_validate_bindings仅缓存原顺序检查全部完成后的成功，修改bindings使缓存失效。dvs_sim_plan只缓存timeline/predict_laws的纯结构编译，不缓存模型成功或付费结果。答辩评分不再被通用话语默认进度覆盖，支持性陈述保持零完整答辩奖励；同一准备失败原因和同一模型不因无关revision变化重复发why_not_ready，实际来源事件仍保留。正式测试恢复无插桩，不能与前轮插桩作同条件速度比。

一级正文导出显式识别既有bodily-flash为触觉闪回；规范表态用换行附本人所知条文，不将全文嵌入重复引号。此处仅修复呈现分类与分段。

第4轮：调查导航一次只为一个尚未完成且处于本人已知时窗的任务生成到达/中继意图，优先当前地点可执行任务，随后可执行的异地任务，最后保留等待前提的首项任务；完整实质目标仍全部参加共同规划。导航缓存包括所有相关完成、要求、位置、通路与窗口真值，避免到达多个房间的短暂奖励相互往返。现场调查操作只在成功Complete后通过既有同处与可见性检查传播实际grants，不向异地人物预发结果。此为新行为条件，不作同条件性能比较。
绑定查询缓存每个主体/物理身份的单项结果；修改bindings仍失效。支持来源按完整候选事件值构建引用索引，合并模型接受事件时保留原事件顺序，最终仍核对完整claim、after和basis，不把索引命中当作支持。中性检查与唯一180秒正式逻辑会话分列。
内容移动正文含起终点，调查检视按本人真实字段显示操作完成状态；首夜递零嘴作为可见行动，实际同处观察不再伪装对白。未知姓名仍取本人信息，未取得介绍时不提前公开。

## DV 文件归并与七目录导出（2026-10-18）

生产DV按职责保留五份core、五份session、chapter1/chapter2两个内容目录，以及game的entry/debug-run入口。checks顶层为六份只读检查，authority、fixtures、report在checks子目录分别保存；生产entry不包含oracle、recall示例或验证配置。现役函数名、参数、声明体与字符串值保持原文；旧来源字符串是证据标识，路径迁移通过 artifacts/partial-bootstrap-core/dv-layout-20261018/path-map.json 追溯，不重写旧证据。

结构整理用声明原文与生产依赖闭包对照、编译及导出一致性检查确认工程接线，不执行流程回归或用它宣称游玩改善。新增源文件按职责进入现有目录；出现define/module作用域时须保留独立编译边界，不直接拼接进其他源模块。

一键源码审阅导出入口为 artifacts/partial-bootstrap-core/dv-layout-20261018/export/export_dv_package.bat，默认输出 artifacts/dv_consolidated_package_v2。七个目录各输出一份DV，保留原文件边界和来源指纹；该包用于阅读，不作为新执行入口。--check只校验导出是否与当前源码一致。旧导出与历史证据不改写。

编辑器的只读checks编译组通过game/entry.dv加载生产声明依赖，再额外加载checks与authority；这是静态诊断集合，生产entry不反向包含authority，不执行这些检查。

## 语法、失败回执与证据修复（2026-10-18）

表层语法增加 elif 和单绑定列表推导式 `[for item in source if condition: expression]`，过滤条件可省略、表达式可嵌套。源表达式在外层作用域求值一次；绑定仅在过滤与投影中可见，投影按源顺序对保留项求值。编译器仍不提供闭包或短路高阶集合函数。纯容器多字段更新继续使用 dv_with_many，不新增记录spread。

失败Complete记录actor.rejected_attempt_signatures，资格判断在repeatable之前核对本人相关输入语义；数字阈值以真值区分，不因时钟或无关revision自动重试。action.failure_grants与failure_observe_when显式授权现场失败观察，仅formal-model回执applied而DV权威未成功时使用，经过原绑定投影，不继承成功授权或隐藏拒绝原因。Godot回执不能证明拒绝后保留的逻辑起点现场时，不授予失败观察。

Runtime.json_write独占创建partial与正式输出，不覆盖既有证据；Session Save/WriteSave共用精确数标签与512深度合同。run_debug以debug_end、正式trace、无partial及源码一致性区分完整证据，timeout_stage区分会话运行与完整轨迹封存。完整前后快照保留。集中工程探针后冻结源码，仅一次180秒正式一级会话；独立审查读取本轮记录，未执行级别列明未验证。

收尾与调查共用单一导航意图；本人截止时钟或闭会观察成立后，按本人已知开放且已采纳移动法则的最短路线逐段前往核对地点。完成记录、可用时间真值、位置、全部相关gate及本人closed进入导航缓存键；不提高深度/预算，不写入预测到达。内容收束字段与系统摘要合同见verification/AGENTS.md和core/README.md。

checks/fixtures/room-safety-draft.dv保存未接通的声明草案，只有显式审阅入口可include生产依赖，生产entry不反向包含该文件。没有生产消费者的房间状态/采纳从现役内容退役，正式地点目录与现役空间规则保留；未来接入必须先补齐有来源的输入与操作，并更新接口和实际验收记录。

正式会话第1轮发生工程异常后保留原始目录；修复集中完成并中性重现，再冻结第2轮源码，仅在新目录运行一次最多180秒正式一级会话，另由新上下文代理审查。公共观察按同一执行/主体/引用的实际收据合并，保留Core重复拒绝与独立话语理解；投影回执、实际grants及原声明分列，接口见core/README.md。

第3轮为独立插桩诊断，样本覆盖与180秒进程时长分别报告，不外推原生耗时。诊断后纯值缓存须以完整输入和原首错、返回副本及嵌套修改失效作中性差分；预算、快照与正式决策保持。第4轮恢复原生、集中修改冻结后仅运行一次180秒正式一级会话并由新上下文代理审查。用户随后明确追加一次240秒窗口，集中核对终点边界后冻结源码，仅运行该次新会话；历史窗口及其限制原样保留。dv_read/常量/topic缓存及字符串容器快路合同见core/README.md。

用户最新授权（2026-10-18）：本次新正式会话时限240秒，仅测试一次，目标为实际到达五日时域终点；时域推进、真实收尾和完整证据分别记录。不得通过注入结果、补充预算、伪造等待目标或直接改时钟满足目标。

## 动机反事实配置维护（2026-10-18）

正式dvg_open_config支持seed、profile、detective_zone、su_initial_room和private_execution_benefit_multiplier。Shura_Detective共用标准认知参数，改变价值排序并授权已有manipulation候选参与自主选择；不直接派动作。初始当前房间与事故历史分开，默认R08不改动原场景。run_debug新增--opening-seed、--su-initial-room和--private-execution-benefit-multiplier，实际run_config写入debug_start与result，配置差异与行为差异分别审查。

opportunity_requirement的none-present须有当前地点的实际可见上下文收据，空的预测上下文不能证明现场无人；物理执行法则独立复核现场。收益倍率改变本人的private_execution资格比较及共同规划中的净效用，不改变资源扣费、世界法则或预算。集中修改完成后统一编译，正式窗口串行180秒且新上下文独立审查；维护core/README.md与verification/DESIGN.md，不执行流程回归。

本轮补充：`--homecoming-negotiation`默认关闭，仅显式协商反事实开启。chapter2/homecoming.dv提供真实提议、听闻与个人异议基础，自己的愿意与对侦探申请的同意分开；尚无安全实证、岛外承诺、照护接续验证或实际离岛生成器，归乡效用由本人drive、caution与对申请者的信任参与实际共同评分；动态异议仍须正式事件证明，不能由声明推定CF8达成。失败动作签名保留现场机会范围与可见上下文，真实变化才允许重新尝试。

实际Complete的话语同时记录actual_influence_actor与控制来源player/autonomous，允许正式自主侦探进入原影响记录；旧player字段保持兼容，control明确来源。只有字面真、真实收闻先于选择、指定动作实际执行且后果首次改变才登记。该时间链仍标causal_attribution=not-established，不冒称已证明因果诱杀。

本轮后续集中修复：通用owner_utility按本人声明数量加权进入共同规划并保留分项，private_execution净收益同时影响资格与评分；本人ref依赖进入规划切片。重复owned_decision沟通按本人上下文变化重新开放。冻结02因识别上述缺口结束，保留已完成窗口，不与新源码拼接比较；集中修复后另起冻结批次。

`maintain_presence`是内容声明的有时窗在场意向：本人已达时不追加无关纯导航目标，尚未达时优先生成该意向的已知通路首步；原实质行动仍参加共同选择，不锁定位置或注入等待。该行为调整针对冻结02实际位置奖励折返，须以新会话核对。

`owned_decision.context_policy_keys`可显式列出本人政策字段；这些字段值同时进入决策上下文与来源快照，实际变化允许重新表态。缺省空列表保持原事实/请求比较，不读其他主体政策。

本轮性能维护：Runtime的JSON编码对不可变FrozenList按弱身份复用精确字符串，可变输入沿原编码路径；读取MemoryStream有效区间避免ToArray复制。无父节点的可变容器仍清除自身Backbone/Index后返回；有父节点沿原传播。DAG仅复用浅帧payload缓冲，保持字节、SHA256链、提交刷新与异常封存规则。动作检索按完整actions值与id纯缓存，仍最后匹配；输入或嵌套改变失效。内容刷新动作/规范数组按原顺序单次重建，静态目标模板只按主体id缓存，动态后缀及收据新鲜度照常计算。工程差分比较原始输出与别名/失效，不是剧情验收。

180秒续作第五批：`dvs_replace`转发通用三元`rt_list_with_index`，保留原逐项相等比较、未命中读取与共享引用，不改为深复制或严格索引检查。批量记录更新仅对正规字符串二元项采用索引快路；结构成员索引仅使用冻结或未失效backbone，数字同桶且最终仍逐值比较。异常/非有限值保留原路径；新建查询最多八层新建列表的只读临时指纹，深层/循环回原扫描且不缓存查询。集中中性差分、自举和构建后再冻结一级窗口；未改变预算、时间、决策和证据合同。

冻结05在首个个人决定Complete中异常退出，完整封存与第二运行时窗口未完成；保留失败证据。修复首次决定不存在前驱记录的空值边界后，先中性复现再另建冻结批次。第六批另修正实际轨迹中的不可行任务导航奖励与时窗观察下界；该行为条件变更依据独立审查另记新基线，不作同条件纯性能宣称。串行180秒比较server GC4与workstation GC1，运行时配置与源码收益分开报告。

180秒续作第七批：常用精确数字文本0/1/-1复用不可变数值盒；其他拼写、负零及Text/有限值校验保持原顺序。结构查询指纹只服务已有冻结容器索引，不修改查询、不缓存新建可变查询，桶内仍沿原顺序核对完整相等。中性差分与实际会话分列，不由微基准推算五日耗时。

结构相等的内部递归与成员桶核对使用原生Bool，公开 `rt_eq`/`rt_same_value` 仍返回同一DV Bool值；引用短路、精确数转换顺序、有效底本复用和原序比较不变。不建立新的相等规则。

run_debug的runtime_environment还记录DOTNET_Tiered/COMPlus_Tiered设置；运行时分层编译比较使用同源串行独立窗口并记录显式环境，不将该配置变化冒称DV算法收益。

第八批纯计算续作：检视新鲜度索引使用未发布私有根构建，每桶独立复制并保留原序，完成后一次memo发布；不原地修改共享world/mind或事件。章节对话目标仅缓存静态前后段，head按完整tasks/id、tail按id，原动态扫描/本人条件与调用次序保持。集中中性差分及工程构建后再冻结180秒会话。运行环境记录按大小写不敏感前缀采集，旧Tiered元数据缺口保持原记录。

`rt_eq`、`rt_same_value`与`rt_has_value`公开Bool结果复用不可变盒，内部判断与首错顺序不变；此改动仅减少返回值装箱，不改变DV相等与成员关系。小快照InlineArray缓冲候选未采纳，其局部CPU测量未支持收益。


## 本轮结构修订与240秒复测（2026-10-18）

本轮遵循根目录最新240秒单次授权。目标刷新、空闲决策复用必须分别说明全部依赖和未知回退；时窗阈值分析不等于已经证明整个认知链可休眠。received_speeches/requests的长度不能替代内容变化，当前物理时刻不能直接写入本人时钟。

批量debug投影与在线原始DAG分开：投影延后不删除原始事件；中断未刷新范围与完整封存分别报告。真实阶段checkpoint及恢复只用于有来源的后半程诊断，不能冒充全新开局；接口、精确值及源码配置一致性见core/README.md。新增rt_is_text仅做通用值类型判定，不承担内容决策。


空闲守卫仅由一次真实无动作准备的稳定结果建立；主体完整语义输入、外部支持事件、现场接触、绑定、费用和世界非时钟状态均参与失效。涉及活跃解释或付费话语预测等未证明链路的主体仍正常准备。复用记录planning_stop_reused并引用真实planning_stop，不冒充本轮认知或选择；具体证书边界和保守回退见core/README.md。内容层仅复用已闭合输入的目标构造，动态资格、导航和等待仍计算，不能称全目标刷新守卫已经完成。


## C# 容器与证据维护（2026-10-19）

用户已暂停Antigravity并授权在原工作区集中修改；本轮整合C#结构化、Session边界与已识别DV错误修复。正式会话前冻结全部实际源码；源修改期间不把工作区测量当作冻结证据，不并发运行正式会话。

快照驻留保留全部仍存活的规范身份。Gen2收集后增量清理弱驻留表的死亡桶，每64次维护触点最多处理128桶；memo与递归冻结均触发维护，不淘汰存活身份。结构相等仅以数值兼容的结构指纹排除不等；指纹相同仍依原序逐值比较，未知值沿原路径。JSON不按字段名裁剪；原始DAG逐帧复用SHA-256上下文，摘要输入字节、提交刷新与partial合同保持。

中性容器/别名/精确数差分、失活桶压力与DAG原始字节比较属于工程排错。工程样本耗时不外推正式五日耗时，JSON完整性修复与性能比较分别记录。证据目录为artifacts/partial-bootstrap-core/takeover-repair-20261018/motivation-counterfactual-20261018/csharp-runtime-20261019/。

后续集中C#修订：可变分支的首个弱父引用直接内置在MutableState，仅多父时分配附加表；保留多别名、死亡父引用清理和循环失效遍历。DAG写入器跨帧Reset复用两份Utf8JsonWriter，失败释放使用finally保证文件与摘要释放。先做多父/循环失效及原始帧字节工程差分，正式游戏结果按候选版本分列，不沿用前轮窗口作为新候选验收。

用户最新追加授权（2026-10-19）：允许多轮正式一级窗口，每轮最多240秒；每轮集中修改并先确认具体工程收益，再隔离冻结串行运行，实际进展和失败分别记录。同条件C#比较固定首轮DV内容及配置，Antigravity新增剧情另行集成，不跨版本拼接。


本轮最新用户目标（2026-10-19）：集中完成已识别优化、结构/接线与撤销错误捷径后再测试。事实等价允许结合严格分析与实际证据作有范围的工程取舍，但不能通过降低搜索层数、删除认知/目标或裁剪原始证据冒充同条件提速。实际覆盖外的分支保持未知。数值缓存候选虽然降低微负载分配，正式窗口未显示进步，现封存为未采纳候选；现役底本保留第二批的单父引用与DAG写出器复用。

冻结容器现以内部sealed IReadOnlyList<object>独占不可变object[]，直接采用冻结时新建的子数组，不再继承可变List或二次复制。R.Items返回只读接口，IsDvList/TryItems仍仅识别原生List及内部FrozenList，不扩张DV值域；写操作仅接受原生可变List，读取可变所有者时才解冻选中子分支并建立父失效关系。根复制和记录更新仍为O(n)，不是HAMT或持久向量。枚举器保留List的状态与错误语义；越界读交给私有空List产生原异常。Session的HostValue/SnapshotValue同步识别两种合法容器，Save标签、Trace字符串、原始DAG及独立副本必须集中工程核对。Items的CLR返回签名变化要求编译器自举与Session/宿主统一重建，禁止混用旧程序集。

空冻结集合的接口枚举委托私有原生空List，保留.NET空枚举器的独立错误状态；非空集合采用值类型枚举器。解冻由FrozenList.CopyRoot把私有数组一次复制到独占List，不暴露底本、不逐项扩容。单字段更新按原先校验/冻结/首匹配顺序，内部保留具体冻结类型以免逐项经接口分派；冻结索引仅在完整资格验证后读取具体冻结pair。中性保存/恢复、错误文本和DAG差分与分配样本分列，CPU噪声不包装成整局提速。

## 可见正文的执行来源（2026-10-19）

Session presentation 保留原有surface/lines，并透传动作source_refs。Main为实际发出的正文标注execution_event、source_id（动作ID）、source_field、emitted_line_index与paragraph_index，line_id由真实执行事件和输出字段/序号组成。该序号是过滤后的显示序号，不冒称源文件行号或原声明索引。source_refs保留原声明提供的路径/行/ID，不补造缺少的来源。ChapterDialogue把这些只读元数据保留到已绘制控件与PlayerViewTrace，既不变更正文，也不写入人物认识。旧证据不补写编号。

空间呈现声明以`dvg_geometry_bindings`按map输出，sites的id/point/label、props的id/texture/position/可选footprint由native内容维护。宿主通用实例化真实节点与碰撞，不在宿主推测剧情地点；图像不替代检视、实测或出行资格。当前源码新增空间布置与纯计算优化分别报告，不作同条件剧情速度声明。

初次物化与导航分开：native snapshot/people.initial_anchor是主体的明确初次锚点；地点anchor保留作移动目的地，不被所有同房人物共同当作出生点。缺初次锚点时只选既有同房且可走、未占用的Stand/Spawn；显式锚点也核占位。已有实体沿用真实位置。室外entered_room以地图实际边界及可走栅格判定，不读取不存在的室内terrain_rows；门区、跨图和最终物理回执仍独立核验。

## 冻结记录布局与字面量分配（2026-10-19）

冻结记录首次索引可共享有序文本键布局的只读末槽字典；先验证全部pair，布局命中仍逐项Ordinal核对，重复键及原扫描回退保持。池由SnapshotLock保护，最多128布局、每布局128槽/4096 UTF16字符、池累计65536字符；超限仅清池根，已发布字典由原冻结节点继续持有，不清内容、不存字段值。非文本键、大表和超长布局保留局部索引；准入阈值不是语言有效性条件，也不是全进程索引存量上限。

编译器对已知长度array字面量生成独占List容量构造；dv_pair/dv_single/dv_list3沿原序引用参数创建独占根，不克隆子值。正常元素求值、别名、通常首错及显式游戏费用保持；完整后备数组提前分配，内存耗尽时与元素副作用/错误的先后可能变化，profile函数归属也可能变化。集中工程核对记录布局/失效/保存及原始DAG字节、字面量求值与编译器固定点；分配下降不外推整局耗时，四倍速仍是宿主播放参数。

## 会话审计策略与冻结哈希（2026-10-19）

R.AuditMode 使用 AsyncLocal 按执行上下文隔离，独立逻辑程序默认完整审计。DvSession.OpenConfiguredCampaign 读取 audit（默认false），Prepare/Complete/投影/关闭均在本会话策略作用域运行；存档在序列化边界补写 audit_mode，恢复沿用原策略，旧档缺失时按完整审计处理。宿主 --dv-audit 显式开启完整证据，采集工具固定带此参数。

dvs_event_storage 在冻结前裁剪普通游玩的事件data六种before/after大字段。正式消费者仍需要的 inference.mind_before/mind_after、observation/listener_parse.mind_after 和 factor_prune.actor_before 保留。事件顺序、ID、父边、观察、回执及世界/心智历史法则保持原数据；不裁剪任意嵌套值或改变JSON序列化。非审计 raw_event 不遍历值且返回true；raw_open要求审计模式。普通宿主不导出重复全量session.json，仍保存真实输入、可见信息、计时、可恢复save及明确的close-result策略；该目录不是完整审计证据。

Record已有冻结/可变索引继续使用；不足8槽的文本键记录在第四次查询时也可建索引，可变pair的父链继续失效。冻结新节点时自底向上计算值兼容64位哈希，same_value可按引用或不同兼容哈希短路；同哈希仍深比，精确/浮点数保持原相等语义。存储身份哈希不替代值兼容哈希。源码在compiler/Runtime.cs、compiler/compiler.dv、verification/session/10-subjective.dv和Session/Session.cs；本轮用户要求完成前不执行工程或游戏验证。

## 人工认知与阅读期调度（2026-10-19，用户追加裁定）

用户明确采纳人工玩家省去重度预演、阅读时提前处理NPC。人工轻量认知、实际replay后的完整刷新及source_actor_revision小型见证的具体合同见verification/AGENTS.md与core/README.md。AI控制与NPC仍使用完整共同认知。宿主阅读期继续使用同一Prepare/现场回执/Complete，具体显示与执行队列由godot_csharp/AGENTS.md维护；此为机制变化，不作同条件纯计算提速结论，本批继续暂停验证。
