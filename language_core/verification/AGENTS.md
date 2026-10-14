# DV 会话模块维护

本目录继承根AGENTS.md和language_core/AGENTS.md。SYNTAX.md定义语言格式，本目录DESIGN.md说明会话结构，README.md说明入口和命令。

## 模块职责

scenarios提供原生DV内容和配置，session组织主体演算与执行，checks独立读取轨迹，report产生中文报告。core+session组合为运行器；core+checks与各入口组合为判定程序。oracle单独导出。

人物、世界、话语、规则、参数和oracle使用DV声明；JSON承接执行后的交换数据。旧.story提供迁移来源，Python计划描述文件和进程。run_verification.py负责源码、构建、导出和进程I/O。

## 运行合同

动作费用使用core/90-action-cost.dv，会话持有成本目录和当前ledger。主体的推知、话语理解、付费重演、竞争论证、规划和回答协议各自保留来源。

session/30-runner 提供 open/prepare/complete/close 会话接口。prepare 保留待执行行动，不先发布物理效果或观察；complete 接受对应 action_event 的实际执行结果与成本，再执行权威法则、感知和传播。命令行逻辑会话通过同一接口使用 formal-model 执行器。session/40-host 导出宿主意图与执行后的呈现数据。Session 类库由 tools/build_session.py 从 DV 源生成，保存与恢复序列化完整会话，不重新初始化认识。

构建宿主类库：`dotnet build language_core/Session/DuelView.Session.csproj -c Release -p:DvPython=<Python路径>`。此构建不代表 Godot 接入或实际几何执行已经完成。

正式 chapter1-*.dv、session 和 checks 已迁移到 surface-v2 可执行语法，旧 surface-v1-proposal 仅为历史提案。run-surface-v2.json 只导出当前原生第一章、独立 oracle 和机制示例，然后运行一局 enabled 正式会话；搭配 --session-only，不默认启动多场景回归。scenarios/cognitive-semantics.dv 独立运行有限域刻画与配平，不注入第一章人物状态。session/20-speech 的实际收闻与预测均调用 mapping 乘法，正式认知变化通过 core/55-cognition 的门槛机制记录；未曾采纳的解释被否定时记录拒斥，不记录撤回。

工程进程时限默认为240现实秒，由--process-timeout-seconds配置。--source-root指定源码，--judge-core-root指定判定Core；--rerun-frozen-session使用指定目录的既有程序和输入。游戏预算由场景配置提供。

## 输出与维护

成果继续存入artifacts/partial-bootstrap-core/，模块架构和接口变动同步本文件及DESIGN.md。停止独立Agent复评的用户裁定继续适用。

cognitive_mind_specimen 将刻画机制连接到真实 Core mind、主体系引用和正式 action-cost ledger，分别导出执行前后脑状态、余额、费用回执和重新读取的认知字段。

回答协议由内容声明 stage、progress_ref 与 completion_ref；session 不内置章节问题名。话语执行同时读取主体本人的字面支持与已注册的世界真值合同。言语内容由聆听链产生证言，不得再以 public_grants 复制隐藏事实。

推演时间线可声明 dispatch(selector, laws)。session 从本人已采纳法则的合取等值条件建立索引，按预测状态选择可能成立的法则；存在未采纳规则、未知选择值、无法提取的条件或会改写选择值的规则时照常展开全组。所有候选的输出均先遮蔽；索引输入、排除理由和实际法则执行记录在同一轨迹。分派与实际预测分别计入原有推演预算。

个人目标可用 achieve 声明未满足命题，规划只以本人已采纳 predict 法则预测达成，不把预测写成已获事实。机会资格读取实际同处观察，话语资格读取成功 listener_parse 回执，欲望资格读取本人有来源的声明。world_processes 只读取物理前提并执行 enforce 法则，时钟由真实行动结算同步；环境变化须另经观察进入主体。宿主 Complete 同时指定观察主体，呈现仅来自本人行动或实际观察、收闻。

付费话语预选缓存仅保存预测事件索引；完整模型、付费前后主体和预测结果保留在事件中，实际发言按索引复用原事件。不得将含旧缓存的 actor_before/after 再嵌入主体缓存，否则多轮选择会递归复制历史。规划最后一层仍评分，但不建立永远不会访问的下一层。
