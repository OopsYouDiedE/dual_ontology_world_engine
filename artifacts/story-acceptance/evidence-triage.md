# 六场景当前证据独立审查

审查日期：2026-10-23。只读检查 `C:/Users/iii/Desktop/dual_ontology_acceptance/artifacts/story-acceptance/current`；未读取设计文件或逻辑源码，未启动新会话，未修改当前输出。读取二进制记录使用工程工具 `binary_evidence.py`、`raw_dag.py`。下表事件号均指对应场景 `session.dvdag` 的 final 检查点 `events` 数组，不跨场景混用。

**结论：六场景全部是进行中的会话被宿主主动关闭，不能称为剧情完成或六场景验收通过。** 当前证据确有早期伤情查看、固定伤踝与补水降温行动；没有合作开仓及其后续伤亡结算完成的证据。后者不得用早期照护代替。

## 实际进度

| 场景 | 最后游戏时刻及停止事件 | 已到达的正式进展 | 未到达／限制 |
| --- | --- | --- | --- |
| standard-jia | 第1日11:01，e1510 | 侦探08:14人物答复被接受 e1090（contract.identity）；08:24完整理由被接受 e1320（contract.reason）；雀儿08:26理由被接受 e1341。侦探 e1352 到甲区门外、e1370 到公共起居厅，e1396 查看备餐守夜交班、e1424 查看桌面证物。 | 此后为等待；没有第二章实际交接及合作开仓完成记录。 |
| standard-yi | 第1日08:03，e560 | 开场、检视风衣、苏泠/霍铎到门厅等早期行动；没有 answer_accepted。 | 截止时苏泠仅聚焦“检视隔离门内外门闩”；不能当作本次检视已完成。驱动出错后中止。 |
| standard-bing | 第1日11:03，e1510 | 侦探 e919 检视完整草图；e960 询问草图手势；艾琳娜 e1016 回想、e1074 说明经过。甲区其他人的人物答复 e1118/e1131/e1144/e1157 被接受。 | 侦探自己的丙区答复尚未被接受；末帧赛尔玛等待180分钟。没有第二章交接。 |
| standard-ding | 第1日08:03，e571 | 侦探 e256 核对公开题目；NPC早期观察与照护。没有 answer_accepted。 | 末帧沈鼎仅聚焦自我介绍；驱动出错后中止。 |
| foolish-yi | 第1日08:19，e1465 | 侦探 e870 拒绝承担现场查验；甲区其他人的人物答复 e1121/e1134/e1147/e1160 被接受；e1285 释放甲区外闩，其他人开始离寝区。 | 侦探未取得答复接受；末帧沈鼎招呼刚醒同伴。没有第二章交接。 |
| no-detective | 第1日11:14，e1539 | 甲区 e969/e982/e995/e1008 人物答复被接受；丁区 e1424/e1437/e1450/e1463 为 logistics.identity；丙区 e1492/e1505/e1518/e1531 为 astral.identity。 | 这些是人物层答复，不能替代完整经过和理由。末帧莫兰答复被接受同时仍说封门缘由需要核对；没有第二章交接。 |

以上只陈述当前轨迹发生之事，不判定未展开路径永久不能达到。

## 中止与工程失败

全部场景尾事件 `session_stopped.data.reason` 和 final.status 均为 `host-closed-during-active-session`。对应末次输入记录明确为 `partial-quit` / `quit`，manifest 记录 `process_exited`。这证明关闭，不证明剧情达成。

驱动记录明确的失败为 `AttachConsole` 抛出 `OSError: [WinError 1] 函数不正确`，退出码1：

- `current/foolish-yi-driver-opening.dvlog`
- `current/standard-ding-driver-opening.dvlog`
- `current/standard-ding-driver-chapter1-a.dvlog`
- `current/standard-yi-driver-chapter1-a.dvlog`

这是控制台附着/采集驱动错误的直接证据，不能据此推断引擎逻辑崩溃。其余 chapter1-a 驱动批次正常返回也不能证明剧情完成。

关闭切点仍有待执行队列：standard-jia 6，standard-bing 5，no-detective 5，foolish-yi 6，standard-ding 4。standard-ding 的 pending_host_receipt=true，轨迹状态版本51，耐久版本50；standard-yi 同样在待宿主回执处结束（末帧聚焦），该行动不能计作已完成。队列数量仅说明关闭边界，不作游戏质量评分。

## 合作开仓、治疗与伤亡

六场景均在08:01执行伤情查看，08:02执行早期照护；事件如下：

| 场景 | 赛尔玛查看伤踝／固定伤踝并送水 | 莫拉查看发热同伴／补水降温与床边照护 |
| --- | --- | --- |
| standard-jia | e234 / e439 | e241 / e446 |
| standard-yi | e239 / e404 | e246 / e411 |
| standard-bing | e234 / e439 | e242 / e447 |
| standard-ding | e233 / e438 | e240 / e445 |
| foolish-yi | e239 / e404 | e246 / e411 |
| no-detective | e225 / e383 | e232 / e390 |

抽查 standard-jia、standard-yi 上述 care_given 的嵌套执行回执，status=applied，world=chapter1.physical；因此这里不是只列候选行动。六场景执行列表也确实包含对应 action_execution。它们证明早期照护开始，不证明康复、保命或治疗终局。

未发现本窗口正式执行列表中的合作开仓行动、其完成回执或死亡结算事件。现有记录未到达可验收的合作开仓后续阶段；伤亡最终结果未验证。不能声称“无死亡”“伤员获救”“治疗全部完成”，也不能因未出现死亡就推断机制失效。已有伤情查看也不能视为本轮新造成伤亡。

## 实际查看的截图

已逐张查看以下六张真实控制台截图，与同名 dvlog 的 visible.text 核对末状态：

- `current/standard-jia/0186-before.jpg`：11:01，卢西安位于甲区门外、等待30分钟。
- `current/standard-yi/0057-before.jpg`：08:03，苏泠在乙区门厅，聚焦检视门闩。
- `current/standard-bing/0186-before.jpg`：11:03，赛尔玛在丙区活动厅，等待180分钟。
- `current/standard-ding/0053-before.jpg`：08:03，沈鼎在乙区活动厅，聚焦自我介绍。
- `current/foolish-yi/0172-before.jpg`：08:19，沈鼎在乙区活动厅招呼同伴。
- `current/no-detective/0186-before.jpg`：11:14，莫兰在丙区活动厅，人物答复接受回执可见，缘由仍待核对。

截图右侧部分长行在窗口边缘裁切；二进制控制台文本包含更完整的列内容，不能把完整文本都声称为截图上可读。截图只是CLI表现，不覆盖Godot几何、碰撞或真实输入体验。

## 封存边界

六份 session.dvdag 经只读校验均有 initial/final 检查点和 seal，未发现无效尾部、剩余尾字节或扫描期间变化。注意通用索引器报告 committed_events=0：本批记录把事件保存在 final 检查点的 events 中，而非逐事件顶层索引。实际数组分别含1721（foolish-yi）、1816（no-detective）、1787（standard-bing）、648（standard-ding）、1787（standard-jia）、643（standard-yi）条，其中包含 host-interaction 记录；不能据0判断没有正式事件，也不应称其具有逐事件提交索引。

上述封存校验仅针对 dvdag，本审查没有独立执行恢复验证，也没有完整验证 dvsave 的可恢复性。字节封存完成与剧情完成、在途动作完成是不同结论。
