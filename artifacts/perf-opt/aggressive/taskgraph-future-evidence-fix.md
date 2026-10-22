# 通用任务图与未来证据收益边界

日期：2026-10-23。独立分支opt/fix-replay-regression，已合并7703d634。本次仅修改30与专用有限检查/报告；未读设计，未运行正式会话，未改20/40/50/60/CLI、人物规则或预算。

## 实际循环根因

7703d634 S1的e21438@2329，节点678785（definition_frame700225）的完整plans选择living_hall→central_hall→examine.bertrand，information=1、value=1.05、score=1.02。e21429（节点678713）mind_after中该examine的13项全部known=true、conflicted=false；respiratory_failure有perception@2329、value=false，本人位置有perception@2328。此前来源保留夹具没有覆盖实际触发，不代表实际循环已修好。

现役照护观察要求respiratory_failure的minimum_time=5370，但available_when只要求接触。2329的观察仍必定早于5370；旧30却每次把这项未来要求记为当前information=1。移动改变检视签名，重新允许examine，self检查又覆盖同一身体字段，从而反复往返、自检。

先读小投影和索引，再使用DAG前缀偏移、限定节点展开核对。所读节点帧SHA-256通过；没有全DAG解码、重新验整条链或反复全文件扫描。临时偏移索引与脚本已清理。

新增dvp_evidence_due：未来minimum_time不产生当前证据奖励，不进入当前证据依赖或实用任务协调；门槛读取本人真实mind.time，预测不能提前跨过。freshness签名增加due状态，真实到期会释放旧检视防重复签名。source与minimum_time没有删除或放宽，到期后仍需新的真实观察。

## 任务图合同

读取actor.policy.opt_taskgraph，缺省true；dvs_choose在已有正式义务选择之后进入dvp_taskgraph。false保留原选择性/精确搜索。正式资格、报价、现场执行、Prepare/Complete与观察发布继续使用原链。

- 当前合法任务按原information/verification/progress/achievement、个人价值与时间成本估值，没有角色过滤、线索奖励、外部效用或新预算。
- 只沿本人已采纳的move预测建立导航后继并复核位置；非移动任务不作为预测后继。选择当前支持任务或最近可达任务即停止，不展开一般多行动计划。
- 消费已有navigation_room/navigation_completion/navigation_requirements/navigation_methods。导航价值来自本人目标原weight及原progress_weight，标access_only，不把到场写成协作完成、观察或答案。
- taskgraph_task保留选定任务。真实到达后继续合法方法；缺少实际参与者/输入时保留任务并返回action=false，等待真实变化。更高的本人声明价值可抢占；完成、条件或路径失效重新选择。不锁定角色日常。
- 复用输入含完整语义actor、mind.revision、mind内容/采纳、声明、可见周边、绑定与成本。知识、来源时界、目标或现场变化重新求值；缓存只复用无副作用的选择。

返回strategy=taskgraph-v1、approximation=true。按房间分层和去重会舍弃同室的其他路径/效果，不能宣称一般多层完整搜索、全脑固定点或全路径最优。实际访问节点计入原plan_expansions并受原plan_budget限制；候选、资格、预测另列。等待回执不重派意图。taskgraph_task是会话选择记录；planning_evidence_mind只在预测视图中，不进入实际mind。

## 最终有限验证

- Core Release构建成功，0 warning、0 error。
- python mcp/verification/check_planning_evidence.py成功，未创建会话。
- 缓存关/开均确认：来源保留；未来minimum_time不制造当前move→inspect收益；真实到期释放检视签名；真实观察满足证据；未知或真实来源变化仍可有信息价值。
- 默认入口选择合法导航，真实到达后执行终端检查；预测未填未知结果；完整相同输入只复用选择；知识/采纳变化失效；真实检查完成后不重复奖励；opt_taskgraph=false进入原规划器。
- 个人声明协作可先导航；真实到场缺参与者时保留等待；实际参与条件更新后才选择方法；更高本人价值可抢占；实际目标完成后清除机会。预测没有提交任务结果。

实施中一次语法诊断发现误替换函数参数，一次夹具失败发现其未声明grants、未包含freshness输入；修正后按最终源码重新构建并运行完整有限检查。以上只报告最终结果，不证明S1/S4循环、通关、协作、伤亡或整局性能已改善；未做Godot/UI验收。

## 集成事项

本次范围仅30与专用检查/报告。主代理接入50/60/CLI时需同步工程文档/AGENTS：默认taskgraph-v1是策略变化，off是原搜索对照；完整输入失效、任务字段及access_only/真实Complete边界见本文。未来证据due修复同时作用on/off，不得将其前后当作语义完全不变的纯缓存性能比较。
