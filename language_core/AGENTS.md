# DV 模块维护

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

compiler 中 ck_* 执行名义类型、字面量范围与可变性检查；core/55-cognition.dv 提供接纳分类、有限域配平搜索及独立证书核验。Runtime.cs 只提供基础有理数、Unicode 与容器 ABI。求解器的最优声明须经 DV 核验器重新检查覆盖、可行性与成本；正式提交一起结算认知、资源和搜索/核验工作量。核心标识符和声明关键词使用英文，其他自然语言用文件头 define 映射。

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

session/18-requests.dv记录实际收闻的专业请求、本人回复及同处可见人物。请求不直接取得场景命题；回复由另一主体的正式行动、本人认识与实际听觉传播形成。公开动作观察按声明范围与实际同处过滤。规划将方法前提纳入依赖，并在整个搜索中保留预测状态的分数/耗时非支配路径；重复状态不重复展开。

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

session/26-model-slicing.dv 从本人已采纳法则的读写依赖逆向选取证物相关演算；切片仍遮蔽原计划全部输出，比较消耗真实工作预算。切片吻合不能建立完整解释，矛盾必须带实际预测及本人观察来源。session/16-personal-choice.dv 将本人有来源的价值排序用于同组选择；行动前提、传播与账本仍使用共用机制。跨房间请求由本人已知声道条件取得候选资格，实际接收仍由 speech 的听觉传播判定。

生产编译由 game/entry.dv 的相对 #include 显式组合 core、session、内容与 game/campaign.dv。Session.OpenCampaign 创建正式原生内容，Prepare/Complete 经同一章节扩展入口；章节扩展不重新调用 session_open，不清空账本和事件。

生产呈现绑定在 game/presentation.dv：身体地点映射场景、房间与素材；客观显示数据不写入人物认识。初始放置只用于创建视图，跨地图回执必须来自现场执行。departure 安装独立门状态，章节内容可用与实际过门分开记录。

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
根层存在正式可执行且净正值的非移动行动时，先完整比较一层生成的后继，再保留所有已比较路径中的最佳首步，包括移动，记录positive-current-after-successor-layer、根层与后继比较结果、实际完整层数。深度不足、没有后继与预算不能覆盖整层分别记录，不冒称完成后继比较；没有正值当前行动则继续原多步搜索。该有限理性停止策略不保证全局最优，不上调预算、不改变费用与答辩判定，另立来源基线。生产动作结果文案仅在原生game/player-content.dv按本人已获事实条件呈现；重演摘要只读本人当前有效模型，吻合不等同正式答辩。答辩回馈来自实际answer_accepted/rejected事件。重新询问观察执行时的公开题目字段及真实开放状态，不预置答案，也不冒称另一次独立话语回复。

阶段交接回执只更新主体实际收到的字段；若主体错过后续公开问话，内容目标以本人收到的阶段时刻为证据下界，驱动本人在现场重新核对当前公开题目。正式听闻与重新核对分别保留实际来源；未听到的问话不自动记为听闻。重新核对的公开字段属于直接观察，不伪造第二次独立话语行动。

移动被选为多步路径首步且实际成功后，主体暂存该路径的下一非移动行动；下次决策仍完整运行本人认知和规划，只有新规划把该行动列为当前直接可执行且净正值时才执行，并记录被替换的路径。失败移动、失效资格或没有正收益时丢弃暂存；不预执行预测结果，也不更改答案或行动费用。

线性宿主会话转交使用 session/30-runner.dv 的 dvs_owned_state_update：只新建顶层记录与字段对，沿用已归本会话所有的字段值引用；调用者放弃前态，用于 campaign 扩展后的会话重绑定、Prepare 主体列表替换及行动队列的会话外壳更新。完整保留未知字段、pending、initial 与输入控制字段，不原地修改旧记录、事件条目或快照。不得用于 world/mind、预测、规划分支或证据快照，不改变其独立复制合同。扩章内容可用仍须与实际几何跨门分别验收。

2026-10-15：批量初始化可使用独占采用表构建器，先复制继承表，每条声明仍立即执行原验证顺序，再仅追加独立条目；finish核对旧前缀与长度，修订号按成功追加数增长，一次持久更新世界，空批保持原世界。中间采用表不得发布、分叉或当作任意更新入口，world/mind/预测仍保持原独立复制合同。
证据目标可显式声明 refresh_until 与 refresh_refs；仅本人尚不知停止命题为真时，以 refresh_refs 中本人已知相关字段的接收时刻和原 minimum_time 的最大值刷新证据下界。缺少 refresh_refs 时保持原下界。本人时钟、无关观察和检视本身的时刻不触发刷新。收到信息与采信内容分别处理；不读取 physical 或预测历史，不改旧观察、认知、预算与判答。
Session新增流式WriteTrace/WriteSave，Trace的精确数仍为原字符串，Save仍为$dv_rational对象标签。宿主关闭等待在途工作、先保存输入与可见记录，再后台流式写原始会话及存档；先写.partial，成功才改为正式文件名，失败保留部分文件。SessionClose在该关闭路径仅调用一次，不自动重试。close-result.json明确各阶段错误与complete，TraceClosed仅表示关闭结束，不能代替证据完整性判断。新建证据目录不得覆盖既有目录；异常也必须释放关闭等待。必要工程检查对照中性值的序列化、别名、验证首错与目标新鲜度，不作游戏评价。


## Evidence and campaign tracking (2026-10-16)

All eligible autonomous actors enter shared cognitive preparation and dvs_choose on their turns; identical completed fixed points may be reused under the documented full-input guard. The incomplete idle cache and its writes are removed. Existing resource/time limits still apply; tracking never filters scheduling.
Execution, observation, answer, transition, model-recheck and simulation events retain the requested full snapshots; simulation results include predicted. Revision metadata supplements snapshots. Other event producers remain subject to separate audit.

The game/campaign.dv helpers dvg_tracked_actors and dvg_chronological_trace project selected actors' events in append order. dvg_run_campaign(max_steps) runs the same formal-model prepare/complete protocol and continuous chapter extension, returns live state, and stops at the completed-action bound or session inactivity. Pass false for the authored action limit, or zero for an unadvanced session. This is audit data, not player-visible knowledge or Godot geometry evidence.
Session.cs exposes internal CampaignTrackedActors/CampaignChronologicalTrace/CampaignRun bridges. python language_core/tools/build_session.py --out language_core/Session generates an inspectable copy; the project excludes that root DVProgram.cs and builds obj/dv instead. Never edit generated copies.
Engineering probe evidence is saved under artifacts/partial-bootstrap-core/single-action-path in new paths. Build/tracking evidence does not certify full gameplay acceptance or performance improvement.

## 一级逻辑输出与调试入口（2026-10-16）

生产入口 game/entry.dv 使用 `#include 相对路径.dv`，路径以包含它的文件目录为基准。tools/dv_sources.py 为构建和编辑器共同解析文件依赖，依赖先于引用者编译，同一路径只加载一次，循环和缺失文件报告位置。每个文件保持独立 define 作用域；引用行保留空行以维护诊断位置。这是文件组合入口，直接调用旧编译器裸文件接口仍需先组合。

game/entry.dv 的 dvg_debug_config 配置观察者、报告主体、事件类型和诊断行动上限。空筛选包含全部，max_steps=false 使用会话原上限。配置只选择输出与诊断停止位置，不修改调度、认知、费用或世界条件。core/98-debug.dv 按原事件顺序只读投影，保留事件与父事件编号、行动、来源、结果和已发生观察；完整快照保留在原始轨迹中。报告不可当作人物知识。

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

rt_record_with 是有序记录的独立复制更新 ABI：原顺序逐项检查，全部同键项替换，无同键时追加；未变项保存独立不可变底本，读取时按复制合同实体化。错误顺序、重复键与分支隔离保持 dv_with 原语义，验证和世界法则继续由 DV 实现。

run_debug.py 在编译前封存包含文件、编译器、运行时与构建工具指纹；启动会话前核对原始指纹、编译模块归一化指纹及实际复制的 Runtime.cs。构建期间变更源码时记录错误并停止，不运行混合版本会话。

## 声明式认知与连续内容维护（2026-10-16）

生产配置入口为 `dvg_open_config({seed, profile, detective_zone})`；配置安装在 `cognitive-profiles.dv`，具体参数属于内容声明的工程初值。开局只抽样声明域并保存选值与种子，恢复存档不重新抽样。`Replay_Depth` 限制有序因果重演阶段，`plan_depth` 独立限制行动规划；重演截断不能认证完整解释。

共鸣资格、公开立场冲突、私人执行效用及病理调度接口位于 `session/16-personal-choice.dv`。主动取回只在成功 Complete 后记录耦合与声明的信任变化；被动触物观察不使用取回结算。物理侵蚀读取权威暴露字段，人物风险判断读取本人认识。公开立场冲突只来自实际收闻记录；预测不生成操纵后果或真实立场证据。

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
