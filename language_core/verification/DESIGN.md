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

规划比较多步路径，执行当前首步。候选资格、评分和选择门槛分别处理。当前总游戏时长为从开局起的360分钟，共享时钟记录实际执行经过时间。

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
证据目标可显式声明refresh_until命题；仅本人尚不知该命题为真时，在目标组合时把minimum_time刷新为本人已接收时刻：mind.time与本人history中observation-batch各observation.time的最大值，只有存在尚未停止的标记目标时才惰性扫描一次。收到某条信息不等同采信其内容；不读取预测历史或physical。此操作不改旧观察或认知，不授权新的传输频道，也不代选检视；没有更晚的已接收信息时不承诺刷新有效。每个目标可独立停止刷新，原预算与判答不变。
Session新增流式WriteTrace/WriteSave，Trace的精确数仍为原字符串，Save仍为$dv_rational对象标签。宿主关闭等待在途工作、先保存输入与可见记录，再后台流式写原始会话及存档；先写.partial，成功才改为正式文件名，失败保留部分文件。SessionClose在该关闭路径仅调用一次，不自动重试。close-result.json明确各阶段错误与complete，TraceClosed仅表示关闭结束，不能代替证据完整性判断。新建证据目录不得覆盖既有目录；异常也必须释放关闭等待。必要工程检查对照中性值的序列化、别名、验证首错与目标新鲜度，不作游戏评价。
采集可在原5Hz截图之后追加私有render-state.jsonl，只读现有actor/AnimatedSprite2D可见性、位置、动画帧与当前纹理引用，并关联原real_ms和文件名；不加载资源、触发动作或更改呈现。失效节点、空纹理、无效动画帧与诊断文件打开失败容错。该记录不给盲玩者；额外磁盘开销随新来源封存，实际采样间隙照实保留，不能以隐藏节点状态代选行动或代替PNG实视。

## Snapshot evidence and campaign adapter

The shared actor turn executes cognition and dvs_choose without the removed idle cache. dvs_event copies event payloads through dv_with, preserving the restored world/mind/actor snapshots for existing readers. A simulation result includes its predicted world; predictions remain separate from physical execution.
The production game/campaign.dv adapter extends the same live session. dvg_run_campaign(max_steps) uses dvg_prepare and dvg_complete with formal-model receipts, stopping on the completed-action bound or session inactivity. false selects the authored action limit; zero returns the opening state. It returns live state, without closing or restarting at chapter extension. dvg_chronological_trace projects selected actors from append-ordered events; it neither changes scheduling nor grants knowledge. Geometry remains outside this logical adapter.


## 独立事件与证据新鲜度

dvs_event_prepared 接收调用表达式中非空 dv_with/dv_with_many 的独立结果，父事件列表继续复制；普通输入经 dvs_event 独立复制。所有消费者继续读取完整事件数据。

dvp_plan 在临时只读主体视图中刷新显式 refresh_until 目标的 minimum_time，采用本人实际已接收时间。它只替换新视图的 policy 字段对；人物知识、预算、原始策略与预测分支的独立性保持。刷新适用于全部章节，章节扩展仅负责内容组合。
