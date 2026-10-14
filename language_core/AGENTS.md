# DV 模块维护

本目录继承仓库根AGENTS.md。语言名为DuelView（DV），源码后缀为`.dv`，实现位于language_core。

## 单一动作实现（2026-10-14）

本节覆盖本文及旧文档中的 r20、旧 Core、解释链和旧工具入口描述。用户要求清理整个项目的旧动作实现；旧源码只能从 Git 历史查阅，历史 artifacts、保护基线、`.story` 与译文保留，不重新编译成可运行旧路径。

唯一正式动作链是 `verification/session/30-runner.dv` 的 prepare → 执行回执 → complete：DV 负责主体决策、报价、权威效果、观察、传播与结算；Godot Main → WorldView 只执行移动并返回现场结果；非移动使用同一 complete 的 DV 世界法则。命令行 formal-model 是同一接口的逻辑执行适配，不含 Godot 几何，不能冒称实机验收。预测与推演不发布真实行动效果。

宿主只保留 GameEngine → DvSession → 生成 DV 类库，不保留旧坐席 token、DispatchField、舞台动作分派或第二个 DvRuntime 会话持有者。界面呈现完成不单独触发 Prepare，只有 view_ready / dv_next 启动下一步；等待回执时不重新分派意图。

构建：`dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers -p:NuGetAudit=false`；必要时用 `-p:DvPython=<Python路径>`。DV 编译器自举与编辑器继续使用现有工具，seed 只持有语法 Reader/Value，不含世界执行或解释器。两份解决方案均引用 Session。

旧 verify、FullTest、guarded_level3、PlayerTranscript、回放与旧会话检查入口已移除，不再执行。原保护清单和原始证据保持原字节；本次架构清理不是同条件耗时优化，不建立或重置测量基线。一级三份记录与三级保护运行工具需要以后按 DV 合同重新实现；本轮不以构建或短启动记录代替任何完整验收级别。

只读逐文件审计和清理报告继续放在 `artifacts/partial-bootstrap-core/single-action-path/`。禁止流程回归以及以计数评价游戏的约束继续有效。

## 文档

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

规划评分对每个预测节点建立一个只读人物视图，复用静态动作与政策；评分不写入该视图，正式人物状态仍由会话更新。

专业回复可以声明offer_to；本人支持话语且看到该接收者时，可主动说明一次。已经实际收到的询问仍沿请求回复处理。

## 内容和机制

新内容采用具名axiom、defn和领域构造函数；原生DV执行后导出JSON作为交换数据。oracle为独立内容包，主体包引用公开合同。旧.story提供迁移来源并继续服务现役宿主。

本人推知、预测与物理执行分别使用infer、predict、enforce采纳。理解、采纳、模型、论证和实际行动保留各自来源。decision显式表达planning和admission；各旧入口的评分、门槛、平分与首步选择在对应DV库中迁移。

正式答辩分别记录公共结论等级与主体自己的接受回执。主体接受记录随实际交接更新，规划依据本人回执排除同等级重复提交；只读判定使用提交前的本人记录核对，不以他人的公共结论替代本人的作答。

session/18-requests.dv记录实际收闻的专业请求、本人回复及同处可见人物。请求不直接取得场景命题；回复由另一主体的正式行动、本人认识与实际听觉传播形成。公开动作观察按声明范围与实际同处过滤。规划将方法前提纳入依赖，并在整个搜索中保留预测状态的分数/耗时非支配路径；重复状态不重复展开。

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

生产编译由 game/modules.json 显式组合 core、session、两章内容与 game/campaign.dv。Session.OpenCampaign 创建正式原生内容，Prepare/Complete 经同一章节扩展入口；章节扩展不重新调用 session_open，不清空账本和事件。

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

线性宿主会话转交使用 session/30-runner.dv 的 dvs_owned_state_update：只新建顶层记录与字段对，沿用已归本会话所有的字段值引用；调用者放弃前态，仅用于 campaign 扩展后的会话重绑定与 Prepare 主体列表替换。完整保留未知字段、pending、initial 与输入控制字段，不原地修改旧记录、事件条目或快照。不得用于 world/mind、预测、规划分支或证据快照，不改变其独立复制合同。扩章内容可用仍须与实际几何跨门分别验收。

2026-10-15：批量初始化可使用独占采用表构建器，先复制继承表，每条声明仍立即执行原验证顺序，再仅追加独立条目；finish核对旧前缀与长度，修订号按成功追加数增长，一次持久更新世界，空批保持原世界。中间采用表不得发布、分叉或当作任意更新入口，world/mind/预测仍保持原独立复制合同。
证据目标可显式声明refresh_until命题；仅本人尚不知该命题为真时，在目标组合时把minimum_time刷新为本人mind.time。此操作不改旧观察或认知，不授权新的传输频道，也不代选检视；本人时刻未推进时不承诺刷新有效。每个目标可独立停止刷新，原预算与判答不变。
Session新增流式WriteTrace/WriteSave，Trace的精确数仍为原字符串，Save仍为$dv_rational对象标签。宿主关闭等待在途工作、先保存输入与可见记录，再后台流式写原始会话及存档；先写.partial，成功才改为正式文件名，失败保留部分文件。SessionClose在该关闭路径仅调用一次，不自动重试。close-result.json明确各阶段错误与complete，TraceClosed仅表示关闭结束，不能代替证据完整性判断。新建证据目录不得覆盖既有目录；异常也必须释放关闭等待。必要工程检查对照中性值的序列化、别名、验证首错与目标新鲜度，不作游戏评价。
