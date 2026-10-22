# Standard 乙区历史重演独立证据审查

审查日期：2026-10-23。仅只读审查本轮 history/ 证据；未读取设计文件，未改源码，未运行正式会话、构建或全因果检查。只新增本报告。审查对象由任务指定为03d5c802；抽查该提交30-session.dv字节SHA-256与本轮provenance中的b30141966c88a2c2f26994de045c9e50725d54f23609c7787746f3fedb1594dd一致，未据此声称逐文件复核整个提交。

## 结论

本轮确实执行了一次正式replay，并完成一个声明历史候选的完整时间线，与侦探当时实际已获的七项观察比较全部吻合。不是仅凭答对四问或因果切片的explained推定重演成功。但本轮没有证明唯一解释：竞争候选仍有未知响应上下文及预算耗尽，model_argument_unavailable明确拒绝接受唯一论证。

四次正确回答均有实际answer_accepted；之后实际拔闩、开门、跨出隔离区并获得living_hall与crossed观察。第二章末也有实际closing检视与本人观察；玩家正文明确为议事时段结束、签名未齐，并非完整公约成立。

## 证据入口与方法

证据根目录：`C:/Users/iii/.codex/worktrees/aggressive-planning-fix/dual_ontology_world_engine/artifacts/perf-opt/aggressive/branch-planning/history/`。

对照actual-event-index.dvlog、raw-event-index.dvlog定位，再读取stdout.dvlog的对应事件。二进制帧由binary_evidence.read_frame核验帧SHA-256。原始DAG只建立至e2218提交帧91115的节点偏移，选择展开e2218.data.result.trace与actor_before中需要的认知单元；没有把巨大DAG整体解码入内存。报告history-report.md只用于定位，以下结论另有原始索引、事件或DAG支持。未读取证据里basis字段所引用的设计文档。

现实耗时为raw-event-index的process_elapsed_ms除1000，是进程启动至事件提交的单调时钟读数，含初始化；不是该次动作或模型本身CPU耗时，不按游戏倍速换算。

## 实际节点

| 事件 | 游戏时刻 | 现实耗时 | 直接证据 |
| --- | --- | --- | --- |
| e1470 | 第1日08:21（501） | 5.201427秒 | answer_accepted，accepted=true、literal_truth=true、epistemic_support=true |
| e2084 | 第1日09:16（556） | 6.690214秒 | 同上 |
| e2213 | 第1日09:51（591） | 6.990277秒 | detective.replay action_execution，status=applied |
| e2218 | 第1日09:51（591） | 7.194943秒 | 完整history-case-1，explained、plan_complete=true |
| e2225 | 第1日09:51（591） | 7.307517秒 | model_argument_unavailable，accepted=false、competitor-not-refuted |
| e2268 | 第1日10:05（605） | 7.418595秒 | 第三次answer_accepted，真值及认知支持均true |
| e2512 | 第1日10:29（629） | 8.311367秒 | 第四次answer_accepted，真值及认知支持均true |
| e2679／e2680 | 第1日10:36（636） | 9.230161／9.231728秒 | cross-out执行applied；本人location=living_hall、departure.crossed=true观察 |
| e36511／e36512 | 第5日09:31（6331） | 168.892561／168.931608秒 | 实际章末检视applied；随后本人直接观察 |

离房不是位置字段的孤立变化：前序e2637@633实际拔内闩，e2652@634实际开隔离门，继而e2679跨出及e2680观察。上述均属formal-model执行，未验证Godot几何或真实键鼠碰撞。

## 历史输入和完整时间线

原始e2218的actor_before.mind.objects.cognition.facets.state中九项均known=true、value=true、conflicted=false，且时间都早于591。来源与先前实际事件对照如下。

| 输入 | 本人获得时间 | 先前事件 | 来源及置信度 |
| --- | --- | --- | --- |
| slate_marks | 488 | e768 | 亲自检视，1.0 |
| pipe_layout | 489 | e809 | 亲自检视，1.0 |
| slate_person | 491 | e912，source_event=e910 | 霍铎证言，0.55 |
| bolt_down | 538 | e1958 | 亲自检视，1.0 |
| boots_toward_bolt | 539 | e1973 | 亲自检视，1.0 |
| cloth_after_boots | 540 | e1982 | 亲自检视，1.0 |
| ash_wedge | 543 | e1998 | 实际对照，1.0 |
| heads_lost | 546 | e2040，source_event=e2029 | 苏泠证言，0.55 |
| score_partial | 562 | e2197 | 亲自检视，1.0 |

因此heads_lost没有在本轮缺少实际收据时被回填。slate_person和heads_lost是本人采纳的证言，不可写成直接目击或外部真相。

e2218原始trace含0–58步骤（分派条目另列），从模型时间0的起点/reset，到1的铜口发声与不同位置听感，至3.2请求、5.3846开门烟灰、5.7到访报告与共享响应选择、内闩和靴印、10.0846回房写谱、15.0846沈鼎放湿布，最后到160的来源与听闻论证。它实际走过移动、声路、认知、共同响应、物理痕迹和书写阶段；不是只检验一个局部特征。

result明确expanded=89、remaining=90、replay_stages_expanded=24、failures=[]、stop_reason=declared-plan-complete、plan_complete=true。七项比较为内闩、楔上烟灰、朝闩靴印、湿布覆盖靴印、石板第一行、第二行、乐谱残句：均prediction=true、observation=true、covered=true、result=match、confidence=0.5。同轮e2215/e2217/e2219是causal-refutation切片，不把它们的explained算作完整历史。

## 假设、唯一性与章末边界

e2218.assumptions把起点、Tube、BoltKeepShut、trust_choice=huo、familiar=true、own_alarm_anchor=true、鞋底与方向等列为hypothetical-only，confidence=0.5。原始actor_before中own_alarm_anchor单元不存在；它确实是模型假设，不能报告为已获事实。共享响应步骤是模型内选择，committed=false，不是昨夜人物行动已被真实恢复。

e2216与一些观察矛盾且存在不完整问题；e2220为unknown、plan_complete=false、unknown-required-response-context、budget-exhausted，expanded=78、remaining=0。e2225以competitor-not-refuted拒绝论证。本轮扫描实际事件未发现detective的model_argument_accepted。允许的结论是“找到一个完整声明模型与现有观察相符”，不允许升级为“唯一历史已证实”或“全部竞争模型已排除”。

e36512本人直接观察council_closed=true、council_ended=false、underground_tremor=true，以及committee_plan票数5；多名人物signed/voted仍false。玩家正文第5日09:31实际显示：“议事时段已经结束，公约签名尚未齐备。未签名、未投票与未决事项仍按实际记录保留。”并描述铜口共鸣、余震及来由待查。实际章末观察成立；完整公约成立不成立于这份证据。

result.dvlog记录正式进程退出0、169.5秒、resumed=false、Standard_Detective/zone_yi、source_unchanged=true、run_config_matches=true、36514个已提交事件、投影尾差0、raw_sealing_complete=true；这些是本轮工具记录，未独立全量重验seal链或整个DAG哈希。causal_check_complete=false的1秒超时检查不能借正式进程退出0宣称通过。未运行UI、独立盲玩、全局因果验证，也未评定剧情符合设计。

本次审查完成时所有读取进程已退出，文件流均已关闭。未留一次性脚本，未修改证据文件。
