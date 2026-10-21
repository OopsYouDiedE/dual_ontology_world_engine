# 04引擎调查300秒：活动厅/走廊循环诊断

2026-10-21。只读 `engine-investigation-300s-04/host-session/session.dvdag` 与其运行源码 `tmp/detective-rpg-candidate-04-20261021`；未读设计、未启动会话或模型，未给盲玩代理任何信息。下述04源码行号均指隔离副本。根源码06最小修正是在证据诊断后另获主代理授权实施，不能拿它解释04已发生的结果；05与所有旧runtime未改。

结论：确有反复移动后重新规划、但不执行预测中的实质检视的循环；不是第三问未开启，也不是关键房间/检视不在候选中。后期更有可确认的通用规划缺陷：同一路径把同一compare.E2任务计分两次，重复的信息机会使绕路方案显著超过实际可用的E6后继。个人选择标签及导航到达奖励进一步影响权衡；早期循环还有不同的目标/再规划冲突，不能承诺只修重复任务就消除全部循环。

## 本轮真实事实与边界

本轮launch为controller=ai、4倍速、300现实秒、audit=true，执行真实Godot。校验二进制DAG读取final下6218个事件，游戏末时871分钟。重算以下源码与source-before对应项一致：30-session.dv=cac4b05fa5b4d7f3de66212ad74e3ec5366d626ecbdf93edc682e5c45f26cc37；40-chapter1.dv=2c6daa50b9896dff956cefa2d0283581188c0c61b74bdb6e9df9cb00cb9ad0ae；60-game.dv=bf1a09d8146e3227f749dd5d8f93f2a0a8e18bcd9e1c3b098f5631bef5a61e4f。

侦探e2915在611分钟identity获得answer_accepted，e2997在622分钟voice_source获得answer_accepted；accepted_answers末态也各为1。本人已得到question.current=words、open_words=true、requires_evidence=true、outstanding_complete=true；其余open_identity/open_voice_source/open_trust=false。故后期循环不能归因为“第三问未听见/未开放”。

实际applied：move.hall.corridor 72次，move.corridor.hall 72次，共144；选择数则分别72与73。后一个方向含开局485分钟从R05首次正常走向活动厅的那一次；末次e6184在869分钟是已选择/准备的后续，不能补写它已applied。不能将全部144次一概称为无意义；无新实质调查的反复折返从后续重复序列及完整计划证据判断。

本轮仍有其他实际进展（检视、证言/推理、两问接受）；也有窗口中途被新发言打断循环的阶段。因此没有把整段300秒伪写成一条完全静态状态轨迹。

## 缺失观察与可执行后继

本人末态cognition含slate_marks/slate_code/slate_person、pipe_layout/sound_trial/heads_lost、frame_hollow、me、words_bolt、remote_voice等真实认识。尚无score_partial、ash_wedge、boots_fit等相应观察。

第三问动作40:448–455要求本人open_words、words_bolt及score_partial。已知words_bolt不能替代尚未实际检视的score_partial；直接授予第三问答案或放宽该前提不属于工程修复。40:243把score_partial放在E6正式检视grants，40:1185–1188把ash_wedge放在正式compare.E2现场比对。两项在本轮都没有实际执行。

但并非没有路线、准入或搜索视野：末轮e6184同一planning_choice明确列出corridor→R08→inspect.E6，净分28.17；最终policy.goals的inquiry_navigation也明确target_method=detective.inspect.E6、target_ref=score_partial、route.next=R08、本人open_words/证据要求为前提。路线来自本人已active采用的predict移动法则及其内容source_refs，不来自oracle。

末态model_budget=0是本轮付费历史重演之后的另一事实，reason_budget仍299、plan_depth=6、plan_budget末剩300；e6166/e6184行动树分别展开18/20，stop=complete-depth，没有此次行动规划预算用尽的证据。model预算耗尽不能解释为何仍可选的真实检视始终不执行，也不能拿它当提高预算的理由。

## 早期循环：选择下一步后又换了远端目的

| 事件/游戏分钟 | 当次选择及预测后继 | 评分证据 |
| --- | --- | --- |
| e1787 / 530 | hall→corridor→R08→inspect.E6 | 0.95；E6 information=0、verification=0、progress=1。此时动作无inquiry_navigation_choice。 |
| e1817 / 532 | corridor→hall→foyer→compare_sole | 1.79；回hall单步achievement=1、净0.98，compare_sole凭personal_choice得到progress=1。实际可用corridor→R08→E6仍列在同份计划，0.97。 |
| e1833 / 534 | 又选hall→corridor→R08→E6 | 0.95。 |
| e1851 / 536 | 又选corridor→hall→foyer→compare_sole | 1.79。 |

这组证据已经说明不是为了取得不同新观察而真实执行的合理往返：每次实际只走首步，E6和compare_sole都停留在未实现预测；重新决策又改变了目的地。

源码因素须分别看：40:4647的内容achieve目标在尚未放行且已离卧室时保持“位于hall”；30:2332的planning_achievements只在一棵树内累计，下一次真实决策又从当前满足状态开始。30:1869对任意personal_choice直接赋progress=1，即使本次information/verification都是0。30:6253–6257只把选中path的第二项为非move时保存planned_followup；本组第二项都是move，下一次没有该保护。

这些是真实的目标/再规划权衡来源，不等于可以由工程审查擅自删去人物目标、强制E6或增加偏置。没有跨真实决策的全链承诺本身也不能单独判bug；人物在新证据下合理改变计划必须保留。本报告把它列为06最小修正后的剩余风险，不把所有循环归于后述单一缺陷。

## 后期循环：同一任务重复计分的直接证据

| 事件/游戏分钟 | 最终被选中的完整预测path | 净分 |
| --- | --- | --- |
| e6166 / 867 | hall→corridor→hall→foyer→compare.E2→compare.E2 | 52.30 |
| e6184 / 869 | corridor→hall→foyer→compare.E2→compare.E2 | 51.32 |

e6166根hall→corridor已有inquiry_navigation_choice，value.progress=1、achievement=1，单步净分1.98；直接hall→foyer→compare.E2在同一份计划里25.16，hall→corridor→R08→E6为28.15。

e6184的corridor→R08根动作已有导航选择标签，单步progress=1、achievement=1；其后E6得到information=1、verification=1、progress=1，整条28.17。尽管这一真实可执行方向清楚存在，选中路径却回hall去承诺双份E2。

两个重复compare.E2各获得information=1与verification=1，information_weight=12.6，单次value=25.2。它们面对的是同一个本人尚未观察的ash_wedge机会，不是两份已发生新观察。本人predict采用的compare.E2法则仅写ritual.compared.detective.E2=true，不能把未知真实检视结果预写为知识；因此后继节点里的认知仍未知，第二次又拿到同样的信息价值。原设计已有node.tasks去重防御，目的正是避免同路径重复此任务，但放置位置错了。

这条原因不需要设计答案即可确认，也不需要把预测知识当事实。真实执行只走首步：hall转入corridor后重算，corridor转回hall后重算，后续compare.E2永远尚未真的执行，下一轮又能对这份未知信息做双倍承诺。

## 可确认的通用机制缺陷与最小修正

04的30-session.dv:2196先rt_push(plans)，2197以后更新best；直到2225的后继生成，才检查`not is_transit and dv_has(node.tasks, action.id)`。所以重复任务虽不能继续入队，却已经计分、成为可选终点甚至改写best。

最小修法：沿用已有node.tasks合同，把已在该预测路径中的非transit任务排除提前到eligibility与评分前。node.tasks只收非transit id，可直接`not dv_has(node.tasks, action.id)`；保留原后置防御、原预算/深度/行动顺序/成本，不修改真实会话中repeatable资格，也不按E2/E6或地图名称特判。不能通过给预测mind授予未知观察来消掉重复奖励。

这项修复已获主代理授权在根源码作为独立06批次实施：30-session.dv新增准入前这一项，language_core/AGENTS.md及README.md同步。没有改04/05隔离runtime，没有运行构建或会话，没有删除personal_choice加分或强推路线。其实际效果必须由单独冻结06的新证据判断，不能以源码阅读宣布循环已解决。

## 其他静态风险单列

- personal_choice是准入/个人权衡标签，但30:1869无条件把它变成progress=1；对辅助导航也会给值。其与内容声明的achieve到达奖励可能叠加；本轮e6166根动作就是progress1+achievement1。建议后续按“实质选择效果”“导航机会”“已满足目标”分别说明计值身份，不能只为消循环把全部个人选择静音。
- transit的node.seen检查也位于评分/best之后，静态上存在“被禁止继续展开的闭环仍可作为终点计分”的相同结构风险。但本轮hall返回的visible_context可能改变，无法只凭房间相同认定它命中同一完整seen_key；本报告没有证明哪条具体选中闭环确实被该guard拒入队。因此未在06顺便移动此检查。
- next_frontier达到lookahead_limit后直接不再接纳后续节点（2233），可造成声明顺序前缀偏重。它与当前预算/有限搜索合同有关，不能自行换顺序、扩大保留宽度或把有限结果声称完整最优；本轮已明确记录了有用E6候选，并不需要先更换搜索算法来修重复计分。
- 同一非transit任务去重只修已证明的路径内重复；早期首步再规划冲突仍须新证据区分。任何更强跨步承诺都须以本人仍有效目标、来源、资格与新信息为依据，不可固定路线。

## 验收限制

本次没有运行新会话、推演或盲玩。两问实际接受、第三问当前开放、缺少观察、未执行后继、预算耗尽、错误重复计分与窗口停止分别报告。末态是时间窗口关闭，没有据此断言整局永远不能交接。后续验证应观察新正式会话中首步与后继是否真正一致、首次有效E6/E2接触、本人观察、第三问接受和章节交接各自的事件；不能用移动次数减少代替游戏进展。