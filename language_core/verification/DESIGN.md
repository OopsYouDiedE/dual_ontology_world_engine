# DV 第一章会话结构

第一章内容由原生DV声明，运行器由core与session模块编译而成。内容位于scenarios，oracle独立导出，JSON承接程序之间的数据交换。

宿主调用 `dvs_session_open` 建立会话，`dvs_session_prepare` 推进一个人物的决策，返回待执行行动；实际执行后以 `action_event/outcome/actual` 调用 `dvs_session_complete`。世界法则、代价结算、个人观察与听闻全部在 DV 完成。序列化会话可原样恢复待执行阶段；宿主不提供答案或认识补丁。`dvs_run` 是使用同一协议的连续逻辑执行器，实际 Godot 输入与几何接线单独实现。

## 模块关系

| 模块 | 职责 |
| --- | --- |
| scenarios | 初始世界、人物、规则、话语、方法和章节协议 |
| session/10-subjective | 本人推知、假说及准备状态 |
| session/15-action-planning | 目标、信息价值、多步路径与首步选择 |
| session/20-speech | 话语、实际收闻、理解和采纳 |
| session/25-simulation | 历史重演、特征比较和候选排除 |
| session/35-model-argument | 解释论证、动态答案和公共提案 |
| session/30-runner | 主体轮转、执行、观察和回答协议 |
| checks / report | 只读判定与中文报告 |

回答协议在内容中提供 stage、progress_ref 和 completion_ref。公共提问的传递仍由实际听觉和发布回执形成，隐藏话语事实只沿个人聆听链进入认知。

推演时间线的 dispatch 指定选择字段和候选法则。运行器从已采纳法则本身提取合取中的等值条件，依据预测状态排除不适用分支，并记录选择输入。候选输出在推演开始时全部遮蔽。索引分派与每条实际预测分别计费，无法建立索引时执行原始规则组。候选排除证书登记可使用的推知法则版本，核验仍重放实际推知及其观察来源。

## 认知过程

运行器以各主体的初始认识开始。本人观察、已闻问题、采纳规则和策略决定推知、理解、假说及行动。话语的字面命题、预设和暗示分别保存；说话者的听者模型预测与听者实际理解分别产生事件。

重演使用本人的假说与预测法则，输出阶段状态、覆盖和特征比较。竞争解释及来源参与估计论证，论证支持输入变化后可以撤回。

## 行动与回答

规划比较多步路径，执行当前首步。候选资格、评分和选择门槛分别处理。独立场景可声明自己的时域；生产 campaign 覆盖五日内容时域，保留原有限行动额。共享时钟记录实际执行经过时间，时域边界不代表章节完成。

有净正值当前非移动行动时，有限策略仍先完整比较一层生成后继，之后保留已比较路径的最佳首步；根层结果、后继层结果、完整层数和停止原因保存在同一选择记录。不足以比较整层的预算、深度上限与无预测后继分别记录，不保证全局最优，不改变累计工作预算或真实费用。重新询问仅读取执行时的实际公开题目与开放字段，不提前授予答案。

动作费用分为时间、计算和体力；执行链包含报价、预留、实际回执和结算。回答协议分别表达字面成立、本人依据、问题匹配、完整性和正式接受，随后产生阶段转移及公开回执。

请求签名比较本人前提的语义状态及实际地点/声道，不因观察编号或时刻更新自动重问；仿真和论证仍保存完整来源。总模型预算耗尽时不再提供新的重演执行，已有有效模型读取保留。原生站点动作通过 travel.station 指定既有几何热点；同地点到达也需要现场回执，之后才执行 DV 接触法则和声明的本人观察。预测只改变接触标记，不预测未知回忆内容。

## 数据与事件

事件包含身份、主体、游戏时刻、父事件、动作、前后版本与收据。个人认知链、付费模型链和公共传播链可分别追溯。manifest关联输入、规则、配置及程序来源。

现役Godot/C#负责输入、碰撞和呈现；本目录为隔离DV逻辑会话。新语法接线见[SYNTAX.md](../SYNTAX.md)，具体语言需求见[第一章反推报告](../../artifacts/partial-bootstrap-core/chapter1-reverse-language-requirements-2026-10-13.md)。


## Surface-v2 接线

实际收闻和本人对他人的预测都使用 dvs_listener_mapping(...) × candidate，各自传入对应视界，预测不覆盖真实听者。dv_threshold_acceptance 根据既有政策门槛形成连续 degree 和离散 level。新材料触发再评价时保存前后接纳状态及来源；未采纳的解释出现反证时是拒斥，已经采纳后降级才是撤回。

有限域刻画由 Core 的求解与核验组合提供。第一章现有内容未定义新的刻画行动及代价法则，因此单独内容包 cognitive-semantics.dv 实际执行该机制；没有借此修改第一章初始认识或注入通关条件。

刻画适配器 dv_imprint_prepare 从实际 Core mind 读取可调字段；dv_imprint_apply 将核验后的结构变化写回这些字段，重读确认目标，并通过现有 action-cost 预留/结算返回同一批 mind、physical、ledger 和工作余额。该接口已由独立原生内容实际调用，尚未为第一章新增角色刻画行动。

## 唯一动作入口

30-runner.dv 的 prepare/complete 是唯一真实动作机制。逻辑运行器直接依次调用这两个函数；step 入口已移除。Godot 只承接同一待执行意图和现场回执；旧 C#、旧解释器与旧坐席执行不再存在于工作树。预测/模型只读本人材料，不提交现实动作效果。

线性宿主会话转交使用 session/30-runner.dv 的 dvs_owned_state_update：只新建顶层记录与字段对，沿用已归本会话所有的字段值引用；调用者放弃前态，仅用于 campaign 扩展后的会话重绑定与 Prepare 主体列表替换。完整保留未知字段、pending、initial 与输入控制字段，不原地修改旧记录、事件条目或快照。不得用于 world/mind、预测、规划分支或证据快照，不改变其独立复制合同。扩章内容可用仍须与实际几何跨门分别验收。

2026-10-15：批量初始化可使用独占采用表构建器，先复制继承表，每条声明仍立即执行原验证顺序，再仅追加独立条目；finish核对旧前缀与长度，修订号按成功追加数增长，一次持久更新世界，空批保持原世界。中间采用表不得发布、分叉或当作任意更新入口，world/mind/预测仍保持原独立复制合同。
证据目标可显式声明 refresh_until 与 refresh_refs；仅本人尚不知停止命题为真时，以 refresh_refs 中本人已知相关字段的接收时刻和原 minimum_time 的最大值刷新证据下界。缺少 refresh_refs 时保持原下界。本人时钟、无关观察和检视本身的时刻不触发刷新。收到信息与采信内容分别处理；不读取 physical 或预测历史，不改旧观察、认知、预算与判答。
Session新增流式WriteTrace/WriteSave，Trace的精确数仍为原字符串，Save仍为$dv_rational对象标签。宿主关闭等待在途工作、先保存输入与可见记录，再后台流式写原始会话及存档；先写.partial，成功才改为正式文件名，失败保留部分文件。SessionClose在该关闭路径仅调用一次，不自动重试。close-result.json明确各阶段错误与complete，TraceClosed仅表示关闭结束，不能代替证据完整性判断。新建证据目录不得覆盖既有目录；异常也必须释放关闭等待。必要工程检查对照中性值的序列化、别名、验证首错与目标新鲜度，不作游戏评价。
采集可在原5Hz截图之后追加私有render-state.jsonl，只读现有actor/AnimatedSprite2D可见性、位置、动画帧与当前纹理引用，并关联原real_ms和文件名；不加载资源、触发动作或更改呈现。失效节点、空纹理、无效动画帧与诊断文件打开失败容错。该记录不给盲玩者；额外磁盘开销随新来源封存，实际采样间隙照实保留，不能以隐藏节点状态代选行动或代替PNG实视。

## Snapshot evidence and campaign adapter

The shared actor turn executes cognition and dvs_choose without the removed idle cache. dvs_event copies event payloads through dv_with, preserving the restored world/mind/actor snapshots for existing readers. A simulation result includes its predicted world; predictions remain separate from physical execution.
The production game/campaign.dv adapter extends the same live session. dvg_run_campaign(max_steps) uses dvg_prepare and dvg_complete with formal-model receipts, stopping on the completed-action bound or session inactivity. false selects the authored action limit; zero returns the opening state. It returns live state, without closing or restarting at chapter extension. dvg_chronological_trace projects selected actors from append-ordered events; it neither changes scheduling nor grants knowledge. Geometry remains outside this logical adapter.


## 独立事件与证据新鲜度

dvs_event_prepared 接收调用表达式中非空 dv_with/dv_with_many 的独立结果，父事件列表继续复制；普通输入经 dvs_event 独立复制。所有消费者继续读取完整事件数据。

dvp_plan 在临时只读主体视图中刷新显式 refresh_until 目标的 minimum_time，采用显式 refresh_refs 中本人已知相关字段的接收时间；未声明时保持原证据下界。它只替换新视图的 policy 字段对；人物知识、预算、原始策略与预测分支的独立性保持。刷新适用于全部章节，章节扩展仅负责内容组合。

## 行动时间队列（2026-10-16）

正式会话在主体 scheduled_action 中保存 action、start、due、成本模型、选择所需引用及来源事件。Prepare 先扫描队列与可决策主体；等待者不进入认知和选择链。到期行动重新按当前物理世界报价，由宿主回执进入同一 Complete。scheduled_start 使 action-cost 只结算一次费用；世界时钟已经等待的部分不再累加。当前执行回执的时间必须等于排队时声明的时间，变化时明确报错，不能默默重排或倒拨时间。无排队起点的 Core 调用保持原即时结算合同。

等待推进逐分钟执行环境法则，实际效果继续在 Complete 后产生。相同时刻按循环主体顺序处理到期项。空闲主体同一时刻至多尝试一次；零可用行动与仍有排队行动分开判断。队列和延后收闻随主体保存、扩章与恢复。pending_heard 只存实际听到的话语及当时声道证据，到期后理解；不重新检查已经结束的声道，也不让等待者执行话语理解模型。

dvs_owned_state_update 的线性会话外壳转交范围增加调度 cursor、physical、events、idle、actors、pending 更新；它仍不修改既有字段对象。队列只存 Complete 所需 relevant_refs，不嵌入整个规划搜索结果。事件保留原独立快照合同。refresh_refs 将复查目标限定到本人已知的相关字段时间；内容列表重建不自动刷新所有观察目标。

本轮改变行动时间模型与内容耗时，属于新行为基线；旧短窗口倍速不用于本轮性能结论。一级使用无窗口正式逻辑会话，未启动 Godot 的结果不证明画面或几何验收。

接近沟通地点的 opportunity 是当前搜索叶节点的预期机会分；进入下一层时仅携带实际行动价值，不把尚未完成的沟通机会累加到无关后继上。可重复检视在成功后保存本人输入的语义签名与显式证据新鲜度；输入与所需证据条件相同时不再次自动检视，未知结果也属于已尝试结果。签名不读取物理世界，不以时间戳变化或无关信息伪造新线索。

每次主体具备决策资格时，plan_budget 以会话开局保存的 planning_limit 作为本次搜索上限，choice.plan_expansions 从该次余额扣除，并计入 planning_work_total。等待者不重置或执行搜索。此上限限制一次搜索的计算规模；动作账本、付费重演额度与实际游戏时间分别结算，保持原有余额合同。

同一时刻的空闲主体在本人认识修订或待理解收闻发生变化时可再次决策；decision_revision 记录一次实际选择结束后的本人修订，防止内部推理产生的修订造成自唤醒循环。scheduled_action 优先于这些唤醒条件，等待中的主体保持不计算。
规划每个预测节点建立独立只读评分上下文，一次评估该节点的证据缺口、待达成目标、可用沟通目标与话语目标，在同节点候选间复用；后继节点重新计算，不跨世界、修订或主体缓存。候选顺序、每层展开预算、分数公式、预测结果及停止条件保持原样。状态键在同一节点只生成一次，输出键字节保持原格式。

个人选择的比较在每次规划开始时按本人的 choice.group 建立动作索引，同组候选保持原动作顺序，全部原资格与价值比较仍执行；该索引只属于当前只读规划视图，后继节点复用同一未变动作目录。没有 normative_method 的动作直接返回原本的无匹配规范结果。

规划备忘保留完整主体输入、bindings 与 costs，仅省略由 runner 使用的 decision_time、decision_revision、planning_work_total，以及仅用于真实事件连边的 last_cognitive_event。选择后的事件连边仍读取主体当前 last_cognitive_event；该字段不参与规划资格与评分。剩余时间覆盖 plan_depth×最大单步上界时，键内归一到该界限：该区间所有搜索路径均满足原时间门槛；低于界限时保留精确余额。其他字段，包括认识、采纳、目标、动作、实际收闻、同处上下文、已用动作、资格与依赖缓存均入键。主体认知链照常执行后才查询规划备忘；实际等待者仍由调度器跳过。命中保留原候选证据与决定，实际 plan_expansions 记录0，并单列 plan_cache_hit/reused_plan_expansions；实际动作费用与每次搜索上限不变。此键依赖关系须随规划读取字段的变更维护。

## 实际资格与规范回执维护（2026-10-16）

本人资格、直接候选、预测搜索及同组个人选择共用门槛。主动效用所读本人引用进入依赖签名；成功完成账本 completed_actions 与包含失败的 used 分开，前者仅由 applied Complete 追加，不由预测写入。实际收闻替代项必须逐项保留来源和理解回执。

norm_proposals 记录已实际获得的规范内容，norm_stances 与 policy.norm_rules 只由本人成功完成的 norm_decision 更新。读板/听闻、理解、采纳和执行的来源事件分列；不把公开提案当作听者同意，不以观察事件编号充当执行编号。规范候选匹配本人完整内容收据，实际选择仍通过共同价值比较。接口与源码索引同步 core/README.md（本目录下为 ../core/README.md）。

统一修改任务先完成源码、声明、文档和静态审读，再启动一次正式一级会话。调试 stop_evidence 只读列出行动窗口、会话上限、各主体配额、认知余额、队列和时域；限额停止与剧情达成分别报告。配额保留来源，不为促成预期关键节点重置人物资源或扩大现有上限。

## 数值类型与解析边界（2026-10-16）

rt_is_number(text) 只检查文本能否解析为有限浮点数，仍供编译器词法扫描使用。运行值检查使用一元 Bool ABI rt_is_numeric_value(value)：有限double与有效精确Rational为真，文本（包括数字文本）、布尔、列表与记录为假，不进行隐式解析。新增平台谓词须同步compiler.dv的arity/返回类型与Core参考；seed沿既有rt_通用发射映射，不增加解释器。

主体数量、规划依赖、物理暴露与表达权重的数值边界遵循同一类型合同。公式、认识来源、成本和成功条件不因类型修复改变。工程值域检查与正式一级会话分列，全部源码修改冻结后统一执行；不以值域结果替代人物或剧情验收。
