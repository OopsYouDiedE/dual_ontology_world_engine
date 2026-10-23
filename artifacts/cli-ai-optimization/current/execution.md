# Current sealed trace: actual execution

Offline derived report; no AI or game action was executed. This table includes offscreen audit facts and is not a claim that the detective saw all events.

- Event records: 6573
- Final clock: D2 00:03
- Session status: host-closed-during-active-session

## Event counts

| Type | Count |
| --- | ---: |
| planning_stop | 1276 |
| cli_interaction | 982 |
| observation | 630 |
| speaker_prediction | 351 |
| action_selected | 330 |
| planning_choice | 330 |
| action_scheduled | 330 |
| cost_quote | 328 |
| host_completion | 327 |
| action_execution | 327 |
| listener_parse | 266 |
| action_wait | 193 |
| co_presence | 116 |
| reason_stop | 100 |
| speech_received_pending | 91 |
| planning_yield | 86 |
| speech_unheard | 85 |
| speech_literal | 72 |
| model_argument_unavailable | 49 |
| inference | 44 |
| public_grants_projection | 42 |
| room_perception | 39 |
| why_not_ready | 35 |
| public_topic_heard | 31 |
| inference_alternative_basis | 18 |
| hypothesis_adopted | 13 |
| answer_submitted | 13 |
| answer_accepted | 13 |
| question_transition | 13 |
| hypothesis_revised | 9 |
| implicature | 8 |
| mind_simulation | 8 |
| inference_dependencies_invalidated | 7 |
| acceptance_changed | 4 |
| implicature_withdrawn | 2 |
| session_start | 1 |
| speech_choice_budget_stop | 1 |
| factor_prune | 1 |
| physical_process | 1 |
| session_stopped | 1 |

## Actor counts

| Actor | Selected | Executed | Planning stop | Yield | Cognition |
| --- | ---: | ---: | ---: | ---: | ---: |
| bertrand | 0 | 0 | 193 | 0 | 0 |
| demon | 20 | 20 | 27 | 0 | 0 |
| detective | 73 | 72 | 3 | 8 | 0 |
| elena | 1 | 1 | 191 | 0 | 0 |
| gu | 22 | 22 | 6 | 0 | 0 |
| huo | 13 | 13 | 42 | 12 | 0 |
| lucian | 23 | 23 | 8 | 0 | 0 |
| margaret | 0 | 0 | 193 | 0 | 0 |
| mora | 14 | 13 | 0 | 0 | 0 |
| moran | 2 | 2 | 190 | 0 | 0 |
| quer | 29 | 29 | 6 | 0 | 0 |
| ruan | 3 | 3 | 189 | 0 | 0 |
| selma | 11 | 10 | 0 | 0 | 0 |
| shen | 49 | 49 | 16 | 3 | 0 |
| su | 39 | 39 | 18 | 63 | 0 |
| veronica | 2 | 2 | 191 | 0 | 0 |
| ward | 29 | 29 | 3 | 0 | 0 |

Execution statuses: {'applied': 326, 'rejected': 1}

## Planning stop reasons

- bertrand: {'selective-no-supported-task-within-search': 193}
- demon: {'selective-no-supported-task-within-search': 27}
- detective: {'selective-no-supported-task-within-search': 3, 'selective-candidate-slice': 8}
- elena: {'selective-no-supported-task-within-search': 191}
- gu: {'selective-no-supported-task-within-search': 6}
- huo: {'selective-candidate-slice': 12, 'selective-no-supported-task-within-search': 42}
- lucian: {'selective-no-supported-task-within-search': 8}
- margaret: {'selective-no-supported-task-within-search': 193}
- moran: {'selective-no-supported-task-within-search': 190}
- quer: {'selective-no-supported-task-within-search': 6}
- ruan: {'selective-no-supported-task-within-search': 189}
- shen: {'selective-candidate-slice': 3, 'selective-no-supported-task-within-search': 16}
- su: {'selective-candidate-slice': 63, 'selective-no-supported-task-within-search': 18}
- veronica: {'selective-no-supported-task-within-search': 191}
- ward: {'selective-no-supported-task-within-search': 3}

## Formal judgments

| Event | Time | Actor | Result |
| --- | --- | --- | --- |
| e1052 | D1 08:14 | quer | {'accepted': True, 'literal_truth': 'true', 'epistemic_support': 'true', 'question_match': 'true', 'answer_complete': 'true', 'law_ready': True, 'reason': 'accepted', 'stage': 'contract.identity', 'accepted_law': 'other.accept.contract.quer'} |
| e1065 | D1 08:14 | lucian | {'accepted': True, 'literal_truth': 'true', 'epistemic_support': 'true', 'question_match': 'true', 'answer_complete': 'true', 'law_ready': True, 'reason': 'accepted', 'stage': 'contract.identity', 'accepted_law': 'other.accept.contract.lucian'} |
| e1078 | D1 08:14 | ward | {'accepted': True, 'literal_truth': 'true', 'epistemic_support': 'true', 'question_match': 'true', 'answer_complete': 'true', 'law_ready': True, 'reason': 'accepted', 'stage': 'contract.identity', 'accepted_law': 'other.accept.contract.ward'} |
| e1091 | D1 08:14 | gu | {'accepted': True, 'literal_truth': 'true', 'epistemic_support': 'true', 'question_match': 'true', 'answer_complete': 'true', 'law_ready': True, 'reason': 'accepted', 'stage': 'contract.identity', 'accepted_law': 'other.accept.contract.gu'} |
| e1591 | D1 08:21 | detective | {'accepted': True, 'literal_truth': 'true', 'epistemic_support': 'true', 'question_match': 'true', 'answer_complete': 'true', 'law_ready': True, 'reason': 'accepted', 'stage': 'identity', 'accepted_law': 'accept.identity'} |
| e2357 | D1 09:44 | detective | {'accepted': True, 'literal_truth': 'true', 'epistemic_support': 'true', 'question_match': 'true', 'answer_complete': 'true', 'law_ready': True, 'reason': 'accepted', 'stage': 'voice_source', 'accepted_law': 'accept.voice_source'} |
| e2560 | D1 10:04 | detective | {'accepted': True, 'literal_truth': 'true', 'epistemic_support': 'true', 'question_match': 'true', 'answer_complete': 'true', 'law_ready': True, 'reason': 'accepted', 'stage': 'words', 'accepted_law': 'accept.words'} |
| e2877 | D1 10:28 | detective | {'accepted': True, 'literal_truth': 'true', 'epistemic_support': 'true', 'question_match': 'true', 'answer_complete': 'true', 'law_ready': True, 'reason': 'accepted', 'stage': 'trust', 'accepted_law': 'accept.trust'} |
| e3260 | D1 11:13 | mora | {'accepted': True, 'literal_truth': 'true', 'epistemic_support': 'true', 'question_match': 'true', 'answer_complete': 'true', 'law_ready': True, 'reason': 'accepted', 'stage': 'logistics.identity', 'accepted_law': 'other.accept.logistics.mora'} |
| e3284 | D1 11:14 | ruan | {'accepted': True, 'literal_truth': 'true', 'epistemic_support': 'true', 'question_match': 'true', 'answer_complete': 'true', 'law_ready': True, 'reason': 'accepted', 'stage': 'astral.identity', 'accepted_law': 'other.accept.astral.ruan'} |
| e3297 | D1 11:14 | elena | {'accepted': True, 'literal_truth': 'true', 'epistemic_support': 'true', 'question_match': 'true', 'answer_complete': 'true', 'law_ready': True, 'reason': 'accepted', 'stage': 'astral.identity', 'accepted_law': 'other.accept.astral.elena'} |
| e3310 | D1 11:14 | selma | {'accepted': True, 'literal_truth': 'true', 'epistemic_support': 'true', 'question_match': 'true', 'answer_complete': 'true', 'law_ready': True, 'reason': 'accepted', 'stage': 'astral.identity', 'accepted_law': 'other.accept.astral.selma'} |
| e3323 | D1 11:14 | moran | {'accepted': True, 'literal_truth': 'true', 'epistemic_support': 'true', 'question_match': 'true', 'answer_complete': 'true', 'law_ready': True, 'reason': 'accepted', 'stage': 'astral.identity', 'accepted_law': 'other.accept.astral.moran'} |

## Physical processes

| Event | Time | Id | Law | Status |
| --- | --- | --- | --- | --- |
| e3634 | D1 12:00 |  | other.environment.care-response | applied |

## Completed formal action receipts

| Event | Time | Actor | Action | Label | Status |
| --- | --- | --- | --- | --- | --- |
| e10 | D1 08:00 | demon | demon.opening | 向铜口播报开场规则 | applied |
| e35 | D1 08:00 | demon | other.question.contract | 提出甲区的问题 | applied |
| e55 | D1 08:00 | demon | other.question.astral | 提出丙区的问题 | applied |
| e75 | D1 08:00 | demon | other.question.logistics | 提出丁区的问题 | applied |
| e216 | D1 08:01 | ward | ward.activity.wax_ash_seen | 查看衣袖与壁炉前的煤灰 | applied |
| e223 | D1 08:01 | ruan | ruan.activity.inspect_own_gate_traces | 查看手掌与袖口的铜屑和压痕 | applied |
| e230 | D1 08:01 | selma | selma.activity.assess_injury | 查看伤踝与同伴的状况 | applied |
| e237 | D1 08:01 | mora | mora.activity.assess_injury | 查看发热同伴的呼吸与体温 | applied |
| e244 | D1 08:01 | veronica | veronica.activity.inspect_own_reagent_traces | 核对瓶签、手套残迹与备用钥匙 | applied |
| e251 | D1 08:01 | detective | detective.opening.wake | 回想醒来前的事 | applied |
| e255 | D1 08:01 | huo | huo.inspect.E7 | 检视长桌上的石板 | applied |
| e259 | D1 08:01 | shen | shen.opening.prepare-tea | 泡茶 | applied |
| e311 | D1 08:02 | su | su.move.hall.foyer | 前往乙区 · 门厅 | applied |
| e318 | D1 08:02 | ward | ward.activity.keys_found | 搜查钥匙与文书 | applied |
| e325 | D1 08:02 | gu | gu.activity.evidence_complete | 沿煤灰足印查找壁炉砖槽 | applied |
| e332 | D1 08:02 | ruan | ruan.activity.ruan_admitted | 说明自己对落闸的判断 | applied |
| e348 | D1 08:02 | selma | selma.activity.care_given | 固定伤踝并送水 | applied |
| e355 | D1 08:02 | mora | mora.activity.care_given | 开始补水降温与床边照护 | applied |
| e362 | D1 08:02 | demon | demon.departure.inspect.contract | 核对甲区的答复与外闩 | applied |
| e366 | D1 08:02 | detective | detective.opening.wear-coat | 穿上风衣 | applied |
| e417 | D1 08:03 | huo | huo.move.hall.foyer | 前往乙区 · 门厅 | applied |
| e425 | D1 08:03 | shen | shen.move.hall.foyer | 前往乙区 · 门厅 | applied |
| e433 | D1 08:03 | su | su.departure.inspect-door | 检视隔离门内外门闩 | applied |
| e439 | D1 08:03 | ward | ward.activity.quer_insulted | 质问私下开柜取图的缘由 | applied |
| e454 | D1 08:03 | selma | selma.activity.support_given | 开始陪伴失神的同伴 | applied |
| e469 | D1 08:03 | veronica | veronica.activity.evidence_complete | 回应试剂取用的质问 | applied |
| e484 | D1 08:03 | demon | demon.departure.inspect.astral | 核对丙区的答复与外闩 | applied |
| e488 | D1 08:03 | detective | detective.inspect.E12 | 检视风衣 | applied |
| e538 | D1 08:04 | huo | huo.departure.inspect-door | 检视隔离门内外门闩 | applied |
| e542 | D1 08:04 | shen | shen.departure.inspect-door | 检视隔离门内外门闩 | applied |
| e546 | D1 08:04 | su | su.inspect.E5 | 检视地上的音叉 | applied |
| e550 | D1 08:04 | quer | quer.activity.refuse_explanation | 拒绝搜查者的盘问 | applied |
| e566 | D1 08:04 | lucian | lucian.activity.object_to_search | 制止对同伴的逼问 | applied |
| e583 | D1 08:04 | ward | ward.activity.report_basis | 核对开柜、藏处与公开授权 | applied |
| e598 | D1 08:04 | moran | moran.activity.evidence_complete | 把表层草图交给同伴看 | applied |
| e605 | D1 08:04 | demon | demon.departure.inspect.logistics | 核对丁区的答复与外闩 | applied |
| e647 | D1 08:05 | detective | detective.move.R05.corridor | 前往乙区 · 宿舍走廊 | applied |
| e653 | D1 08:05 | demon | demon.departure.inspect-body | 查看周围 | applied |
| e673 | D1 08:06 | huo | huo.move.foyer.hall | 前往乙区 · 活动厅 | applied |
| e680 | D1 08:06 | shen | shen.move.foyer.hall | 前往乙区 · 活动厅 | applied |
| e688 | D1 08:06 | su | su.move.foyer.hall | 前往乙区 · 活动厅 | applied |
| e696 | D1 08:06 | demon | demon.departure.inspect-outer | 检视隔离门外闩 | applied |
| e728 | D1 08:07 | demon | demon.opening.reward-invitation | 邀请领取报偿 | applied |
| e741 | D1 08:07 | detective | detective.move.corridor.hall | 前往乙区 · 活动厅 | applied |
| e768 | D1 08:08 | shen | shen.move.hall.pantry | 前往乙区 · 备餐间 | applied |
| e776 | D1 08:08 | su | su.move.hall.corridor | 前往乙区 · 宿舍走廊 | applied |
| e782 | D1 08:08 | demon | demon.opening.desire-offer | 介绍岛上的工作与资源 | applied |
| e796 | D1 08:08 | detective | detective.inspect.E7 | 检视长桌上的石板 | applied |
| e802 | D1 08:08 | huo | huo.opening.inspect-reward-slot | 检视本人领取柜 | applied |
| e828 | D1 08:09 | shen | shen.inspect.E9 | 检视水槽里的杯子 | applied |
| e832 | D1 08:09 | detective | detective.identify-own-lens | 比对放大镜与石板上的图记 | applied |
| e836 | D1 08:09 | huo | huo.opening.scrape-testpiece | 检视领取柜中的试件 | applied |
| e861 | D1 08:10 | shen | shen.inspect.E10 | 检视碗柜上的罐子 | applied |
| e865 | D1 08:10 | su | su.move.corridor.R08 | 前往乙区 · 卧室 R-08 | applied |
| e872 | D1 08:10 | detective | detective.inspect.E8 | 检视铜口 | applied |
| e876 | D1 08:10 | huo | huo.reply.slate_code | 说明本人取得的观察 | applied |
| e921 | D1 08:11 | su | su.inspect.E6 | 检视书桌上的乐谱 | applied |
| e925 | D1 08:11 | detective | detective.sound.invite | 邀请众人分处听取同一句话 | rejected |
| e928 | D1 08:11 | huo | huo.reply.slate_person | 说明本人取得的观察 | applied |
| e965 | D1 08:12 | shen | shen.move.pantry.hall | 前往乙区 · 活动厅 | applied |
| e973 | D1 08:12 | huo | huo.opening.introduce | 向同伴自我介绍 | applied |
| e1004 | D1 08:13 | shen | shen.opening.inspect-reward-slot | 检视本人领取柜 | applied |
| e1008 | D1 08:13 | su | su.move.R08.corridor | 前往乙区 · 宿舍走廊 | applied |
| e1014 | D1 08:13 | huo | huo.opening.seek-drawing | 表明留下寻找图纸的意愿 | applied |
| e1045 | D1 08:14 | shen | shen.opening.bite-silver | 咬验领取柜中的银元 | applied |
| e1049 | D1 08:14 | quer | quer.answer.contract.quer | 提交人物答复：雀儿 | applied |
| e1062 | D1 08:14 | lucian | lucian.answer.contract.quer | 提交人物答复：雀儿 | applied |
| e1075 | D1 08:14 | ward | ward.answer.contract.quer | 提交人物答复：雀儿 | applied |
| e1088 | D1 08:14 | gu | gu.answer.contract.quer | 提交人物答复：雀儿 | applied |
| e1101 | D1 08:14 | huo | huo.opening.maintain-interpretation | 坚持本人现有的解释 | applied |
| e1147 | D1 08:15 | shen | shen.opening.look-arrival | 查看刚到同伴的衣着 | applied |
| e1151 | D1 08:15 | su | su.move.corridor.hall | 前往乙区 · 活动厅 | applied |
| e1159 | D1 08:15 | demon | demon.departure.manifest | 凝实身体以接触门闩 | applied |
| e1185 | D1 08:16 | huo | huo.deep-resonance | 前往乙区 · 活动厅 | applied |
| e1191 | D1 08:16 | shen | shen.reply.cups_known | 说明本人取得的观察 | applied |
| e1206 | D1 08:16 | su | su.opening.inspect-reward-slot | 检视本人领取柜 | applied |
| e1211 | D1 08:16 | demon | other.release.contract | 释放甲区外闩 | applied |
| e1269 | D1 08:17 | shen | shen.reply.starter_kept | 说明本人取得的观察 | applied |
| e1284 | D1 08:17 | su | su.opening.play-audition | 操作领取柜旁的试听机 | applied |
| e1310 | D1 08:18 | shen | shen.opening.welcome | 招呼刚醒来的同伴 | applied |
| e1325 | D1 08:18 | su | su.reply.frame_hollow | 说明本人取得的观察 | applied |
| e1339 | D1 08:18 | quer | quer.leave-dormitory | 前往生活翼 · 甲区门外 | applied |
| e1347 | D1 08:18 | ward | ward.leave-dormitory | 前往生活翼 · 甲区门外 | applied |
| e1353 | D1 08:18 | gu | gu.leave-dormitory | 前往生活翼 · 甲区门外 | applied |
| e1395 | D1 08:19 | shen | shen.opening.introduce | 向同伴自我介绍 | applied |
| e1410 | D1 08:19 | su | su.opening.introduce | 向同伴自我介绍 | applied |
| e1424 | D1 08:19 | quer | quer.doorway-handoff | 说明答复收据与下一步打算 | applied |
| e1436 | D1 08:19 | ward | ward.doorway-handoff | 说明答复收据与下一步打算 | applied |
| e1449 | D1 08:19 | gu | gu.doorway-handoff | 说明答复收据与下一步打算 | applied |
| e1485 | D1 08:20 | shen | shen.opening.serve-tea | 将茶分给在场同伴 | applied |
| e1492 | D1 08:20 | su | su.opening.maintain-interpretation | 坚持本人现有的解释 | applied |
| e1506 | D1 08:20 | lucian | lucian.leave-dormitory | 前往生活翼 · 甲区门外 | applied |
| e1531 | D1 08:21 | shen | shen.opening.ask-name | 询问刚到同伴的姓名 | applied |
| e1539 | D1 08:21 | su | su.opening.ask-sugar | 询问泡茶的习惯 | applied |
| e1553 | D1 08:21 | lucian | lucian.doorway-handoff | 说明答复收据与下一步打算 | applied |
| e1565 | D1 08:21 | ward | ward.departure.join-living | 前往生活翼 · 公共起居厅 | applied |
| e1571 | D1 08:21 | gu | gu.departure.join-living | 前往生活翼 · 公共起居厅 | applied |
| e1589 | D1 08:21 | detective | detective.answer.me | 答辩：闩门的是我 | applied |
| e1652 | D1 08:22 | ward | night.inspect-room.ward | 查看备餐与守夜交班 | applied |
| e1656 | D1 08:22 | gu | night.inspect-room.gu | 查看备餐与守夜交班 | applied |
| e1660 | D1 08:22 | demon | demon.question.voice_source | 提出当前案件问题 | applied |
| e1682 | D1 08:22 | detective | detective.reask_question | 询问当前的问题 | applied |
| e1688 | D1 08:22 | shen | shen.opening.body-habit | 说明泡茶的习惯 | applied |
| e1738 | D1 08:23 | detective | detective.sound.invite | 邀请众人分处听取同一句话 | applied |
| e1759 | D1 08:23 | shen | shen.opening.memory-report | 询问并交流登船记忆 | applied |
| e1796 | D1 08:24 | demon | demon.sound.agree-repeat | 应实际请求答应复述 | applied |
| e1815 | D1 08:24 | detective | detective.sound.take-position | 留在听音处并发出到位信号 | applied |
| e1837 | D1 08:24 | huo | huo.sound.take-position | 留在听音处并发出到位信号 | applied |
| e1860 | D1 08:24 | shen | shen.opening.accept-work | 答应继续做饭 | applied |
| e1893 | D1 08:25 | su | su.move.hall.corridor | 前往乙区 · 宿舍走廊 | applied |
| e1899 | D1 08:25 | detective | detective.opening.introduce | 向同伴自我介绍 | applied |
| e1929 | D1 08:26 | shen | shen.move.hall.foyer | 前往乙区 · 门厅 | applied |
| e1950 | D1 08:27 | shen | shen.sound.take-position | 留在听音处并发出到位信号 | applied |
| e1969 | D1 08:27 | su | su.move.corridor.R08 | 前往乙区 · 卧室 R-08 | applied |
| e1990 | D1 08:28 | su | su.sound.take-position | 留在听音处并发出到位信号 | applied |
| e2024 | D1 08:52 | detective | detective.sound_trial | 进行声路试验 | applied |
| e2061 | D1 08:53 | su | su.sound.remember-hearing | 记下本人在听音处实际听见的声音 | applied |
| e2065 | D1 08:53 | detective | detective.sound.remember-hearing | 记下本人在听音处实际听见的声音 | applied |
| e2086 | D1 08:55 | su | su.move.R08.corridor | 前往乙区 · 宿舍走廊 | applied |
| e2092 | D1 08:55 | detective | detective.move.hall.foyer | 前往乙区 · 门厅 | applied |
| e2118 | D1 08:56 | detective | detective.departure.inspect-door | 检视隔离门内外门闩 | applied |
| e2132 | D1 08:57 | su | su.move.corridor.hall | 前往乙区 · 活动厅 | applied |
| e2140 | D1 08:57 | detective | detective.inspect.E1 | 检视隔离门 | applied |
| e2162 | D1 08:58 | detective | detective.inspect.E3 | 检视灰上的鞋印 | applied |
| e2177 | D1 08:59 | detective | detective.inspect.E4 | 检视门底的湿布 | applied |
| e2191 | D1 09:00 | detective | detective.inspect.E5 | 检视地上的音叉 | applied |
| e2205 | D1 09:22 | ward | ward.wait.first_night.60 | 等待60分钟 | applied |
| e2209 | D1 09:22 | gu | gu.wait.first_night.60 | 等待60分钟 | applied |
| e2226 | D1 09:29 | detective | detective.replay | 在手稿上重演昨夜 | applied |
| e2251 | D1 09:31 | detective | detective.compare.E2 | 前往乙区 · 门厅 | applied |
| e2268 | D1 09:33 | detective | detective.move.foyer.hall | 前往乙区 · 活动厅 | applied |
| e2297 | D1 09:34 | detective | detective.sound.report-hearing | 向在场者报告自己的听感 | applied |
| e2313 | D1 09:34 | su | su.sound.report-hearing | 向在场者报告自己的听感 | applied |
| e2355 | D1 09:44 | detective | detective.answer.V3_0 | 答辩：声音来自铜口在墙内与门框处的管道漏音 | applied |
| e2401 | D1 09:45 | demon | demon.question.words | 提出当前案件问题 | applied |
| e2419 | D1 09:45 | detective | detective.reask_question | 询问当前的问题 | applied |
| e2449 | D1 09:47 | detective | detective.move.hall.corridor | 前往乙区 · 宿舍走廊 | applied |
| e2473 | D1 09:49 | detective | detective.move.corridor.R08 | 前往乙区 · 卧室 R-08 | applied |
| e2491 | D1 09:50 | detective | detective.inspect.E6 | 检视书桌上的乐谱 | applied |
| e2509 | D1 09:52 | detective | detective.move.R08.corridor | 前往乙区 · 宿舍走廊 | applied |
| e2528 | D1 09:54 | detective | detective.move.corridor.hall | 前往乙区 · 活动厅 | applied |
| e2558 | D1 10:04 | detective | detective.answer.V4_0 | 答辩：喊的是闩门，别开 | applied |
| e2602 | D1 10:05 | demon | demon.question.trust | 提出当前案件问题 | applied |
| e2620 | D1 10:05 | detective | detective.reask_question | 询问当前的问题 | applied |
| e2651 | D1 10:07 | detective | detective.move.hall.pantry | 前往乙区 · 备餐间 | applied |
| e2676 | D1 10:08 | detective | detective.inspect.E9 | 检视水槽里的杯子 | applied |
| e2697 | D1 10:09 | detective | detective.inspect.E10 | 检视碗柜上的罐子 | applied |
| e2716 | D1 10:11 | detective | detective.move.pantry.hall | 前往乙区 · 活动厅 | applied |
| e2749 | D1 10:13 | detective | detective.move.hall.foyer | 前往乙区 · 门厅 | applied |
| e2779 | D1 10:15 | detective | detective.touch_latch | 走近并触碰插闩 | applied |
| e2809 | D1 10:16 | detective | detective.inspect.E2 | 检视墙角的木楔与门底的灰 | applied |
| e2823 | D1 10:18 | detective | detective.move.foyer.hall | 前往乙区 · 活动厅 | applied |
| e2854 | D1 10:22 | ward | ward.wait.first_night.60 | 等待60分钟 | applied |
| e2858 | D1 10:22 | gu | gu.wait.first_night.60 | 等待60分钟 | applied |
| e2875 | D1 10:28 | detective | detective.answer.trust | 答辩：说明为何信任霍铎的速记并闩门 | applied |
| e2922 | D1 10:29 | shen | shen.departure.inspect-door | 检视隔离门内外门闩 | applied |
| e2926 | D1 10:29 | demon | demon.departure.manifest | 凝实身体以接触门闩 | applied |
| e2941 | D1 10:30 | detective | detective.move.hall.foyer | 前往乙区 · 门厅 | applied |
| e2949 | D1 10:30 | su | su.move.hall.foyer | 前往乙区 · 门厅 | applied |
| e2957 | D1 10:30 | demon | demon.departure.release-outer | 抬起外闩 | applied |
| e2999 | D1 10:31 | detective | detective.departure.inspect-door | 检视隔离门内外门闩 | applied |
| e3003 | D1 10:31 | shen | shen.departure.inspect-door | 检视隔离门内外门闩 | applied |
| e3007 | D1 10:31 | su | su.departure.inspect-door | 检视隔离门内外门闩 | applied |
| e3028 | D1 10:32 | detective | detective.departure.draw-inner | 拔出门内插闩 | applied |
| e3051 | D1 10:33 | detective | detective.departure.open-door | 拉开隔离门 | applied |
| e3084 | D1 10:35 | detective | detective.departure.cross-out | 前往生活翼 · 公共起居厅 | applied |
| e3091 | D1 10:35 | shen | shen.departure.cross-out | 前往生活翼 · 公共起居厅 | applied |
| e3098 | D1 10:35 | su | su.departure.cross-out | 前往生活翼 · 公共起居厅 | applied |
| e3129 | D1 10:36 | detective | night.inspect-room.detective | 查看备餐与守夜交班 | applied |
| e3133 | D1 10:36 | shen | night.inspect-room.shen | 查看备餐与守夜交班 | applied |
| e3137 | D1 10:36 | su | night.inspect-room.su | 查看备餐与守夜交班 | applied |
| e3158 | D1 10:37 | detective | night.inspect-displayed | 查看桌面是否摆出证物 | applied |
| e3174 | D1 11:02 | mora | mora.wait.first_night.180 | 等待180分钟 | applied |
| e3190 | D1 11:03 | selma | selma.wait.first_night.180 | 等待180分钟 | applied |
| e3194 | D1 11:03 | mora | mora.activity.report_basis | 对照创面、试剂与医嘱记录 | applied |
| e3225 | D1 11:04 | selma | selma.activity.report_basis | 对照拉闸自述与表层草图 | applied |
| e3257 | D1 11:13 | mora | mora.answer.logistics.veronica | 提交人物答复：维罗妮卡 | applied |
| e3281 | D1 11:14 | ruan | ruan.answer.astral.ruan | 提交人物答复：阮清月 | applied |
| e3294 | D1 11:14 | elena | elena.answer.astral.ruan | 提交人物答复：阮清月 | applied |
| e3307 | D1 11:14 | selma | selma.answer.astral.ruan | 提交人物答复：阮清月 | applied |
| e3320 | D1 11:14 | moran | moran.answer.astral.ruan | 提交人物答复：阮清月 | applied |
| e3358 | D1 11:15 | demon | demon.departure.inspect.astral | 核对丙区的答复与外闩 | applied |
| e3371 | D1 11:19 | quer | quer.wait.first_night.180 | 等待180分钟 | applied |
| e3387 | D1 11:21 | lucian | lucian.wait.first_night.180 | 等待180分钟 | applied |
| e3401 | D1 11:22 | ward | ward.wait.first_night.60 | 等待60分钟 | applied |
| e3407 | D1 11:22 | gu | gu.wait.first_night.60 | 等待60分钟 | applied |
| e3426 | D1 11:26 | lucian | lucian.wait.first_night.5 | 等待5分钟 | applied |
| e3440 | D1 11:29 | quer | quer.wait.first_night.10 | 等待10分钟 | applied |
| e3454 | D1 11:30 | quer | quer.wait.first_night.1 | 等待1分钟 | applied |
| e3458 | D1 11:30 | lucian | lucian.wait.first_night.4 | 等待4分钟 | applied |
| e3475 | D1 11:32 | quer | quer.departure.join-living | 前往生活翼 · 公共起居厅 | applied |
| e3481 | D1 11:32 | lucian | lucian.departure.join-living | 前往生活翼 · 公共起居厅 | applied |
| e3505 | D1 11:33 | quer | quer.departure.inspect-yi-outer | 检视乙区外闩 | applied |
| e3509 | D1 11:33 | lucian | night.inspect-room.lucian | 查看备餐与守夜交班 | applied |
| e3526 | D1 11:34 | quer | night.inspect-room.quer | 查看备餐与守夜交班 | applied |
| e3540 | D1 11:35 | quer | quer.departure.call-inside | 呼唤门内的人 | applied |
| e3592 | D1 11:36 | shen | shen.wait.first_night.60 | 等待60分钟 | applied |
| e3599 | D1 11:36 | su | su.wait.first_night.60 | 等待60分钟 | applied |
| e3619 | D1 11:37 | detective | detective.wait.first_night.60 | 等待60分钟 | applied |
| e3637 | D1 12:06 | shen | shen.wait.first_night.30 | 等待30分钟 | applied |
| e3652 | D1 12:22 | ward | ward.wait.first_night.60 | 等待60分钟 | applied |
| e3659 | D1 12:22 | gu | gu.wait.first_night.60 | 等待60分钟 | applied |
| e3678 | D1 12:26 | shen | shen.wait.first_night.20 | 等待20分钟 | applied |
| e3692 | D1 12:30 | shen | shen.wait.first_night.4 | 等待4分钟 | applied |
| e3706 | D1 12:31 | shen | night.apron | 从围裙口袋取出零嘴 | applied |
| e3727 | D1 12:33 | lucian | lucian.wait.first_night.60 | 等待60分钟 | applied |
| e3741 | D1 12:34 | lucian | lucian.departure.rebuke-quer | 提醒同伴隔门交谈的礼数 | applied |
| e3793 | D1 12:35 | quer | quer.wait.first_night.60 | 等待60分钟 | applied |
| e3808 | D1 12:36 | su | su.wait.first_night.60 | 等待60分钟 | applied |
| e3823 | D1 12:37 | detective | detective.wait.first_night.60 | 等待60分钟 | applied |
| e3839 | D1 13:22 | ward | ward.wait.first_night.60 | 等待60分钟 | applied |
| e3844 | D1 13:22 | gu | gu.wait.first_night.60 | 等待60分钟 | applied |
| e3862 | D1 13:31 | shen | shen.wait.first_night.60 | 等待60分钟 | applied |
| e3876 | D1 13:34 | lucian | lucian.wait.first_night.60 | 等待60分钟 | applied |
| e3890 | D1 13:35 | quer | quer.wait.first_night.60 | 等待60分钟 | applied |
| e3904 | D1 13:36 | su | su.wait.first_night.60 | 等待60分钟 | applied |
| e3918 | D1 13:37 | detective | detective.wait.first_night.60 | 等待60分钟 | applied |
| e3933 | D1 13:52 | ward | ward.wait.first_night.30 | 等待30分钟 | applied |
| e3947 | D1 13:57 | ward | ward.wait.first_night.5 | 等待5分钟 | applied |
| e3961 | D1 14:00 | ward | ward.wait.first_night.3 | 等待3分钟 | applied |
| e3975 | D1 14:01 | shen | shen.wait.first_night.30 | 等待30分钟 | applied |
| e3979 | D1 14:01 | ward | night.show-evidence | 出示红纸与蜡布筒 | applied |
| e4002 | D1 14:02 | ward | night.ward-claim | 提议限制行动 | applied |
| e4056 | D1 14:04 | lucian | lucian.wait.first_night.30 | 等待30分钟 | applied |
| e4071 | D1 14:05 | quer | quer.wait.first_night.30 | 等待30分钟 | applied |
| e4086 | D1 14:06 | su | su.wait.first_night.30 | 等待30分钟 | applied |
| e4101 | D1 14:07 | detective | detective.wait.first_night.30 | 等待30分钟 | applied |
| e4116 | D1 14:08 | detective | night.inspect-evidence | 检视蜡布筒封口与红纸边缘 | applied |
| e4131 | D1 14:09 | detective | night.defer-judgment | 提议先核对材料再判断 | applied |
| e4185 | D1 14:13 | mora | mora.wait.first_night.180 | 等待180分钟 | applied |
| e4199 | D1 14:14 | selma | selma.wait.first_night.180 | 等待180分钟 | applied |
| e4215 | D1 14:22 | gu | gu.wait.first_night.60 | 等待60分钟 | applied |
| e4229 | D1 14:23 | gu | night.corroborate | 提出补充核验 | applied |
| e4278 | D1 14:31 | shen | shen.wait.first_night.30 | 等待30分钟 | applied |
| e4294 | D1 14:32 | ward | ward.wait.first_night.30 | 等待30分钟 | applied |
| e4310 | D1 14:34 | lucian | lucian.wait.first_night.30 | 等待30分钟 | applied |
| e4326 | D1 14:35 | quer | quer.wait.first_night.30 | 等待30分钟 | applied |
| e4342 | D1 14:36 | su | su.wait.first_night.30 | 等待30分钟 | applied |
| e4357 | D1 14:39 | detective | detective.wait.first_night.30 | 等待30分钟 | applied |
| e4372 | D1 14:53 | gu | gu.wait.first_night.30 | 等待30分钟 | applied |
| e4386 | D1 15:01 | shen | shen.wait.first_night.30 | 等待30分钟 | applied |
| e4400 | D1 15:02 | ward | ward.wait.first_night.30 | 等待30分钟 | applied |
| e4414 | D1 15:04 | lucian | lucian.wait.first_night.30 | 等待30分钟 | applied |
| e4428 | D1 15:05 | quer | quer.wait.first_night.30 | 等待30分钟 | applied |
| e4442 | D1 15:06 | su | su.wait.first_night.30 | 等待30分钟 | applied |
| e4456 | D1 15:09 | detective | detective.wait.first_night.30 | 等待30分钟 | applied |
| e4471 | D1 15:23 | gu | gu.wait.first_night.30 | 等待30分钟 | applied |
| e4485 | D1 15:39 | detective | detective.wait.first_night.30 | 等待30分钟 | applied |
| e4500 | D1 15:59 | detective | detective.wait.first_night.20 | 等待20分钟 | applied |
| e4515 | D1 16:00 | detective | detective.wait.first_night.1 | 等待1分钟 | applied |
| e4530 | D1 16:01 | detective | night.ask-closed-doors | 询问其他寝区与伤者 | applied |
| e4577 | D1 17:13 | mora | mora.wait.first_night.180 | 等待180分钟 | applied |
| e4591 | D1 17:14 | selma | selma.wait.first_night.180 | 等待180分钟 | applied |
| e4606 | D1 18:01 | shen | shen.wait.first_night.180 | 等待180分钟 | applied |
| e4621 | D1 18:02 | ward | ward.wait.first_night.180 | 等待180分钟 | applied |
| e4636 | D1 18:04 | lucian | lucian.wait.first_night.180 | 等待180分钟 | applied |
| e4651 | D1 18:05 | quer | quer.wait.first_night.180 | 等待180分钟 | applied |
| e4666 | D1 18:06 | su | su.wait.first_night.180 | 等待180分钟 | applied |
| e4680 | D1 18:21 | shen | shen.wait.first_night.20 | 等待20分钟 | applied |
| e4695 | D1 18:23 | gu | gu.wait.first_night.180 | 等待180分钟 | applied |
| e4709 | D1 18:26 | shen | shen.wait.first_night.5 | 等待5分钟 | applied |
| e4723 | D1 18:30 | shen | shen.wait.first_night.4 | 等待4分钟 | applied |
| e4738 | D1 18:31 | shen | night.count-provisions | 清点各区带来的余粮 | applied |
| e4752 | D1 18:32 | shen | night.cook | 准备八人份晚餐 | applied |
| e4772 | D1 18:33 | shen | night.eat.shen | 领取并食用晚餐 | applied |
| e4792 | D1 18:34 | shen | night.inspect-room.shen | 查看备餐与守夜交班 | applied |
| e4806 | D1 19:01 | detective | detective.wait.first_night.180 | 等待180分钟 | applied |
| e4821 | D1 19:02 | detective | night.eat.detective | 领取并食用晚餐 | applied |
| e4842 | D1 20:02 | detective | detective.wait.first_night.60 | 等待60分钟 | applied |
| e4857 | D1 20:05 | quer | quer.wait.first_night.120 | 等待120分钟 | applied |
| e4871 | D1 20:06 | quer | night.eat.quer | 领取并食用晚餐 | applied |
| e4891 | D1 20:13 | mora | mora.wait.first_night.180 | 等待180分钟 | applied |
| e4905 | D1 20:14 | selma | selma.wait.first_night.180 | 等待180分钟 | applied |
| e4919 | D1 20:32 | detective | detective.wait.first_night.30 | 等待30分钟 | applied |
| e4934 | D1 20:36 | quer | quer.wait.first_night.30 | 等待30分钟 | applied |
| e4948 | D1 20:43 | mora | mora.wait.first_night.30 | 等待30分钟 | applied |
| e4962 | D1 20:52 | detective | detective.wait.first_night.20 | 等待20分钟 | applied |
| e4977 | D1 20:56 | quer | quer.wait.first_night.20 | 等待20分钟 | applied |
| e4991 | D1 20:57 | detective | detective.wait.first_night.5 | 等待5分钟 | applied |
| e5006 | D1 20:58 | mora | mora.wait.first_night.15 | 等待15分钟 | applied |
| e5020 | D1 21:00 | detective | detective.wait.first_night.3 | 等待3分钟 | applied |
| e5024 | D1 21:00 | quer | quer.wait.first_night.4 | 等待4分钟 | applied |
| e5028 | D1 21:00 | mora | mora.wait.first_night.2 | 等待2分钟 | applied |
| e5050 | D1 21:01 | quer | night.partner | 邀请明日结伴核查 | applied |
| e5092 | D1 21:01 | mora | mora.activity.evening_check | 晚间复查并留下照护记录 | applied |
| e5114 | D1 21:02 | ward | ward.wait.first_night.180 | 等待180分钟 | applied |
| e5129 | D1 21:04 | lucian | lucian.wait.first_night.180 | 等待180分钟 | applied |
| e5144 | D1 21:06 | su | su.wait.first_night.180 | 等待180分钟 | applied |
| e5159 | D1 21:23 | gu | gu.wait.first_night.180 | 等待180分钟 | applied |
| e5174 | D1 21:34 | shen | shen.wait.first_night.180 | 等待180分钟 | applied |
| e5196 | D1 23:01 | quer | quer.wait.first_night.120 | 等待120分钟 | applied |
| e5210 | D1 23:02 | ward | ward.wait.first_night.120 | 等待120分钟 | applied |
| e5224 | D1 23:04 | lucian | lucian.wait.first_night.120 | 等待120分钟 | applied |
| e5238 | D1 23:06 | su | su.wait.first_night.120 | 等待120分钟 | applied |
| e5252 | D1 23:14 | selma | selma.wait.first_night.180 | 等待180分钟 | applied |
| e5266 | D1 23:23 | gu | gu.wait.first_night.120 | 等待120分钟 | applied |
| e5280 | D1 23:31 | quer | quer.wait.first_night.30 | 等待30分钟 | applied |
| e5294 | D1 23:32 | ward | ward.wait.first_night.30 | 等待30分钟 | applied |
| e5308 | D1 23:34 | shen | shen.wait.first_night.120 | 等待120分钟 | applied |
| e5312 | D1 23:34 | lucian | lucian.wait.first_night.30 | 等待30分钟 | applied |
| e5329 | D1 23:36 | su | su.wait.first_night.30 | 等待30分钟 | applied |
| e5343 | D1 23:51 | quer | quer.wait.first_night.20 | 等待20分钟 | applied |
| e5357 | D1 23:52 | ward | ward.wait.first_night.20 | 等待20分钟 | applied |
| e5371 | D1 23:53 | gu | gu.wait.first_night.30 | 等待30分钟 | applied |
| e5385 | D1 23:54 | shen | shen.wait.first_night.20 | 等待20分钟 | applied |
| e5389 | D1 23:54 | lucian | lucian.wait.first_night.20 | 等待20分钟 | applied |
| e5406 | D1 23:56 | su | su.wait.first_night.20 | 等待20分钟 | applied |
| e5410 | D1 23:56 | quer | quer.wait.first_night.5 | 等待5分钟 | applied |
| e5427 | D1 23:57 | ward | ward.wait.first_night.5 | 等待5分钟 | applied |
| e5441 | D1 23:58 | gu | gu.wait.first_night.5 | 等待5分钟 | applied |
| e5455 | D1 23:59 | shen | shen.wait.first_night.5 | 等待5分钟 | applied |
| e5459 | D1 23:59 | lucian | lucian.wait.first_night.5 | 等待5分钟 | applied |
| e5477 | D2 00:00 | detective | detective.wait.first_night.180 | 等待180分钟 | applied |
| e5481 | D2 00:00 | shen | shen.wait.first_night.1 | 等待1分钟 | applied |
| e5485 | D2 00:00 | su | su.wait.first_night.4 | 等待4分钟 | applied |
| e5489 | D2 00:00 | quer | quer.wait.first_night.4 | 等待4分钟 | applied |
| e5493 | D2 00:00 | lucian | lucian.wait.first_night.1 | 等待1分钟 | applied |
| e5497 | D2 00:00 | ward | ward.wait.first_night.3 | 等待3分钟 | applied |
| e5501 | D2 00:00 | gu | gu.wait.first_night.2 | 等待2分钟 | applied |
| e5536 | D2 00:01 | detective | detective.night.notice-time | 查看光线与时钟 | applied |
| e5540 | D2 00:01 | shen | shen.night.notice-time | 查看光线与时钟 | applied |
| e5544 | D2 00:01 | su | su.night.notice-time | 查看光线与时钟 | applied |
| e5548 | D2 00:01 | quer | quer.night.notice-time | 查看光线与时钟 | applied |
| e5552 | D2 00:01 | lucian | lucian.night.notice-time | 查看光线与时钟 | applied |
| e5556 | D2 00:01 | ward | ward.night.notice-time | 查看光线与时钟 | applied |
| e5560 | D2 00:01 | gu | gu.night.notice-time | 查看光线与时钟 | applied |
| e5564 | D2 00:01 | mora | mora.wait.first_night.180 | 等待180分钟 | applied |

## Relevant speech and authored presentations

### e10 / D1 08:00 / demon / demon.opening / applied

诸位晨安。我是此地的代管者，也是诸位的教师。诸位初涉斯地，记忆有缺，并非伤病，请勿惊惶。诸位所需之知，我会教。此地对诸位的照料，有其期限。我所立之规，已贴在活动厅的公告框里。
昨夜警铃骤起。主楼曾有人向诸位呼喊。通往主楼之门，在诸位这一侧闩上了。那人再没有回来。
此为始业之问：门是谁闩上的？闩门之时，此人可曾听见门外的呼喊？每一问，说一句结论，再拿出一件撑得住它的东西。痕迹还在。答案备好了，到铜口来。

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "demon", "voice": "visible-action", "lines": [{"template": "墙内的铜管传来轻轻一声咳嗽。", "bindings": []}], "publish_after": "actual-applied-execution", "knowledge_effect": "none"}

### e35 / D1 08:00 / demon / other.question.contract / applied

铜口里传来声音：谁开启内柜，物品藏在哪里，又做了什么？

### e55 / D1 08:00 / demon / other.question.astral / applied

铜口里传来声音：是谁让一个人留在了沉静室里？

### e75 / D1 08:00 / demon / other.question.logistics / applied

铜口里传来声音：白塔柜里那瓶试剂，是谁取走的？

### e251 / D1 08:01 / detective / detective.opening.wake / applied

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "detective", "voice": "private-monologue", "lines": [{"template": "我记得的最后一件事，是上船。", "bindings": []}, {"template": "衣架上挂着一件风衣。右手袖口磨得发亮——我写字时总拿那儿蹭桌沿。是我的。", "bindings": []}, {"template": "先出去，看看这是什么地方。", "bindings": []}], "publish_after": "actual-applied-execution", "knowledge_effect": "none"}

### e259 / D1 08:01 / shen / shen.opening.prepare-tea / applied

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "shen", "voice": "visible-action", "lines": [{"template": "沈鼎把茶泡上。", "bindings": []}], "publish_after": "actual-applied-execution", "knowledge_effect": "none"}

### e366 / D1 08:02 / detective / detective.opening.wear-coat / applied

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "detective", "voice": "visible-action", "lines": [{"template": "你披上了这件熟悉的呢料风衣，领口与袖口早已磨平。", "when": {"op": "equals", "ref": {"type": "ref", "object": "coat", "facet": "state", "field": "worn_by"}, "value": "detective"}}]}

### e728 / D1 08:07 / demon / demon.opening.reward-invitation / applied

诸位的报偿，就在铜口下方的领取柜里，各有名签。请亲手核验。

### e782 / D1 08:08 / demon / demon.opening.desire-offer / applied

银元在柜里，沈师傅。粮仓、水窖、灶火，岛上也都有。那张减少不可逆损失的闭式热机图纸，此地有人画过，霍先生。此地还有诸位从未听过的声音，苏小姐。

### e836 / D1 08:09 / huo / huo.opening.scrape-testpiece / applied

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "huo", "voice": "visible-action", "lines": [{"template": "霍铎把试件举到灯下，刮配合面。", "bindings": []}], "publish_after": "actual-applied-execution", "knowledge_effect": "none"}

### e925 / D1 08:11 / detective / detective.sound.invite / rejected

我想把这条传声管试清楚。愿意帮忙的话，请在 R-08 和门框旁各留一人，另两人留在厅里；到位后各敲两下。请代管者听见我们都到位后，试读‘请看／铜牌，慢数／四拍’这句新短语，前轻后重。

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "detective", "voice": "visible-action", "lines": [{"template": "手指沿着铜管的接缝移过，停在分支处。", "bindings": []}], "publish_after": "actual-applied-execution", "knowledge_effect": "none"}

### e973 / D1 08:12 / huo / huo.opening.introduce / applied

……霍铎。钳工。

### e1014 / D1 08:13 / huo / huo.opening.seek-drawing / applied

我留下。那张图，我得找。

### e1045 / D1 08:14 / shen / shen.opening.bite-silver / applied

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "shen", "voice": "visible-action", "lines": [{"template": "沈鼎走到柜前，咬了一枚银元。", "bindings": []}], "publish_after": "actual-applied-execution", "knowledge_effect": "none"}

### e1101 / D1 08:14 / huo / huo.opening.maintain-interpretation / applied

试件，我刮过。初涉……我先按刚来这里办。

### e1211 / D1 08:16 / demon / other.release.contract / applied

代管者复核已被挑开的甲区门闩，正式宣告沃德等人可以离开。

### e1284 / D1 08:17 / su / su.opening.play-audition / applied

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "su", "voice": "visible-action", "lines": [{"template": "苏泠摇响了柜旁的试听机。", "bindings": []}], "publish_after": "actual-applied-execution", "knowledge_effect": "none"}

### e1310 / D1 08:18 / shen / shen.opening.welcome / applied

哟，又醒一个。进来坐，茶刚泡上。

### e1395 / D1 08:19 / shen / shen.opening.introduce / applied

我姓沈，沈鼎，烧菜的。

### e1410 / D1 08:19 / su / su.opening.introduce / applied

苏泠。弹琴的。

### e1485 / D1 08:20 / shen / shen.opening.serve-tea / applied

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "shen", "voice": "visible-action", "lines": [{"template": "他把茶一杯一杯推过来。推到苏泠面前的那杯，他没有放糖。", "bindings": []}], "publish_after": "actual-applied-execution", "knowledge_effect": "none"}

### e1492 / D1 08:20 / su / su.opening.maintain-interpretation / applied

……试听机里的声音，我听见了。初涉，我先按刚到这里听着。

### e1531 / D1 08:21 / shen / shen.opening.ask-name / applied

你呢？穿这么一身风衣，不像跑船的。

### e1539 / D1 08:21 / su / su.opening.ask-sugar / applied

……你怎么知道我喝茶不放糖？

### e1688 / D1 08:22 / shen / shen.opening.body-habit / applied

嗯？……顺手吧。

### e1738 / D1 08:23 / detective / detective.sound.invite / applied

我想把这条传声管试清楚。愿意帮忙的话，请在 R-08 和门框旁各留一人，另两人留在厅里；到位后各敲两下。请代管者听见我们都到位后，试读‘请看／铜牌，慢数／四拍’这句新短语，前轻后重。

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "detective", "voice": "visible-action", "lines": [{"template": "手指沿着铜管的接缝移过，停在分支处。", "bindings": []}], "publish_after": "actual-applied-execution", "knowledge_effect": "none"}

### e1759 / D1 08:23 / shen / shen.opening.memory-report / applied

都还记得啥？我就记得上船。下了跳板往后，脑子里白茫茫一片。

### e1796 / D1 08:24 / demon / demon.sound.agree-repeat / applied

可以。各位到位以后，我就读‘请看／铜牌，慢数／四拍’，前轻后重，各读两遍。结束时，以两声轻响为记。

### e1815 / D1 08:24 / detective / detective.sound.take-position / applied

我已经在乙区 · 活动厅，会留在这里听完。铜管响起两下短促的敲击。

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "detective", "voice": "visible-action", "lines": [], "publish_after": "actual-applied-execution", "knowledge_effect": "none"}

### e1837 / D1 08:24 / huo / huo.sound.take-position / applied

我已经在乙区 · 活动厅，会留在这里听完。铜管响起两下短促的敲击。

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "huo", "voice": "visible-action", "lines": [], "publish_after": "actual-applied-execution", "knowledge_effect": "none"}

### e1860 / D1 08:24 / shen / shen.opening.accept-work / applied

……钱是真的。这活我接着干。

### e1899 / D1 08:25 / detective / detective.opening.introduce / applied

侦探。……做侦探的。

### e1950 / D1 08:27 / shen / shen.sound.take-position / applied

我已经在乙区 · 门厅，会留在这里听完。铜管响起两下短促的敲击。

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "shen", "voice": "visible-action", "lines": [], "publish_after": "actual-applied-execution", "knowledge_effect": "none"}

### e1990 / D1 08:28 / su / su.sound.take-position / applied

我已经在乙区 · 卧室 R-08，会留在这里听完。铜管响起两下短促的敲击。

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "su", "voice": "visible-action", "lines": [], "publish_after": "actual-applied-execution", "knowledge_effect": "none"}

### e2024 / D1 08:52 / detective / detective.sound_trial / applied

两声轻响，停歇。试验结束。

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "detective", "voice": "visible-action", "lines": [{"template": "约定的两声轻响传过铜管，读句停止。", "bindings": []}], "publish_after": "actual-applied-execution", "knowledge_effect": "none"}

### e2061 / D1 08:53 / su / su.sound.remember-hearing / applied

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "su", "voice": "private-monologue", "lines": [{"template": "我站在 R-08，贴着门内侧听。听清的是‘……铜牌……四拍’，前面的‘请看’和‘慢数’辨不出来。不看着铜管，会把这声听成门外有人在喊。", "bindings": []}], "publish_after": "actual-applied-execution", "knowledge_effect": "none"}

### e2065 / D1 08:53 / detective / detective.sound.remember-hearing / applied

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "detective", "voice": "private-monologue", "lines": [{"template": "我留在活动厅，面对铜口。听见的是‘请看／铜牌，慢数／四拍’，四拍都能分清；‘请看’和‘慢数’读得轻，‘铜牌’和‘四拍’重。", "bindings": []}], "publish_after": "actual-applied-execution", "knowledge_effect": "none"}

### e2297 / D1 09:34 / detective / detective.sound.report-hearing / applied

我留在活动厅，面对铜口。听见的是‘请看／铜牌，慢数／四拍’，四拍都能分清；‘请看’和‘慢数’读得轻，‘铜牌’和‘四拍’重。

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "detective", "voice": "dialogue", "lines": [], "publish_after": "actual-applied-execution", "knowledge_effect": "none"}

### e2313 / D1 09:34 / su / su.sound.report-hearing / applied

我站在 R-08，贴着门内侧听。听清的是‘……铜牌……四拍’，前面的‘请看’和‘慢数’辨不出来。不看着铜管，会把这声听成门外有人在喊。

Authored presentation (conditional lines remain conditional; verify actual visible JPG):

{"actor": "su", "voice": "dialogue", "lines": [], "publish_after": "actual-applied-execution", "knowledge_effect": "none"}

