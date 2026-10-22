# taskgraph 已声明等待回归与542停点

日期：2026-10-23。基线07efde02，独立分支opt/fix-replay-regression。只修改30、专用有限夹具与本报告；未读设计文件、未改40，也未补入任何ready或观察事实。

## 当前正式证据的停因

读取主线S1的小投影、索引和34.76MB封存DAG中的最终角色字段。e1973@542为demon.question.voice_source，e1992@542为detective.reask_question成功执行；e2002为detective的taskgraph-no-supported-task，score=0、expanded=1，随后e2003 no-admissible-action-or-budget。侦探仍有action_quota=215、reason_budget=308；不是耗尽预算或高分空路径遮蔽。

最终本人知道sound_trial_requested=true、sound_trial_done=false、coordinator=detective，ready.detective/huo/su=true，但ready.shen=false。霍铎和沈鼎本人知道全部到位；侦探与苏泠不知道沈鼎已到位。独立只读复核定位沈鼎e1630@500真实到位，侦探当时在R08，e1634明确speech_unheard/no-audibility-grant。侦探进入保持站位后不能再通过移动接近补闻；当前动作集中没有可以补发这份信号的合法动作。remember-hearing要求sound_trial_done，当前确实尚不合法。

因此不能靠30把已漏听的内容回填，也不能通过任意推进几分钟解决此刻缺少输入。主代理已另行授权40的实际补闻/重发链。本提交不宣称独立解决542停点。

## 独立存在的通用等待回归

taskgraph原先在score>0之外还无条件排除known_opportunity_wait，导致这些合法、已估值的等待既不进入当前任务，也不进入fallback。现役等待的kind通常是wait.first_night.N，而不是精确字符串wait，因此仅按kind==wait也无法识别它们。

本次保留所有经过正式资格及原价值函数判定为正收益的候选，把known_opportunity_wait与普通wait一起放入根层fallback；已有当前支持任务或合法导航仍优先。其效力继续来自原声明、采纳的等待预测、原goal权重和原成本。没有生成新等待、任意时刻目标、精确搜索保底、免费时间推进或章节/角色分支。选中等待后仍须正式Prepare/Complete，不在规划中提交预测的时间或状态。

最终侦探动作与goals中没有known_opportunity_wait，说明这一独立回归不是把当前542停因简单解释成“漏掉现成等待”；两者必须分别修复与验收。

## 最终有限验证

- Core Release构建成功，0 warning、0 error。
- python mcp/verification/check_planning_evidence.py成功，规划缓存关/开都执行完整夹具。
- 新增wait.fixture.10：本人已有声明目标、已采纳等待法则及原成本，taskgraph选择taskgraph-declared-wait；原mind.time和elapsed不被预测修改。
- 剩余时间不足真实声明时长时，返回action=false且time_blocked>0。
- 缺少本人采纳的预测时，单独wait_until目标不会制造有价值等待。
- 当前有实际调查价值的检视时，优先检视，声明等待只作fallback。
- 既有未来minimum_time、导航、协作续用、来源失效、抢占与off对照有限检查同轮通过。

主代理随后要求暂不启动独立全局诊断，改为统一整合复测。本轮没有运行原先允许的300秒branch-taskgraph正式诊断，没有混用主线程序与修改后源码，也没有建立新的正式运行输出。有限验证不能证明第一章或全局游戏已通过。
