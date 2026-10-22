# none 正式会话独立只读审查

审查日期：2026-10-23。审查者为新上下文子 Agent；未读取设计文件，未构建、启动会话或运行全因果检查。依据为当前 branch-npc 二进制索引、必要原始 DAG 前缀与源码。未将事件名当作情绪证据。

## 结论

三房报告人均有真实调查所得的本人知识、实际采用的动机假说、当前且无冲突的 angry=true、随后只提交人物表面事实并被接受的证据。源码把当前愤怒用于开放表面提交目标，同时关闭报告人继续深查的目标；因此存在可核对的选择影响。不能据此宣布完整 S3 已证明：“强烈”没有独立情绪强度量，赛尔玛偏好先验只有 0.6；成见的方向与先验为作者预设，调查触发采用，并非从材料学得新的偏好。该轮全因果检查超时，独立审查没有补作全量证明。

## 指纹与范围

工作树 HEAD=a1adc18a3128408a1503c3002ab37f0f134ed821，git status 无改动。source-hashes.json 中18份源码逐份 SHA-256 与现文件一致，含20/30/35/40/50/60、runner/checks、编译器与记录工具；result 记录 build_source_consistent/source_unchanged/run_config_matches=true。配置 detective_zone=none，formal-model，geometry=false。原始 DAG 摘要记录30111个提交事件、final checkpoint与seal，无尾部残留；摘要指纹3f083a19ec3b39dd281eebc86081428a44cd67b7966ad3f437cd6e33975173e4。本审查未再次全文件计算 DAG SHA，未重新验证完整链；选读前缀逐帧通过 read_frame 校验，全部原始文件只读。

## 三房链条

时间为源记录的游戏分钟；括号为第1日时刻。

| 房 / 报告人 | 本人调查与成见 | 当前实际情绪 | 真实选择、执行、接受 |
|---|---|---|---|
| contract / ward | e211/e212，481（08:01），本人检视蜡灰；e257 同刻采用 contract.reason.theft，prior=0.8。e353/e354，482（08:02）取得钥匙材料；e364 同刻获得现场证据；e463/e464，483（08:03）实际质问后获 quer_insulted | e411，482，仍为 false，不能拿该事件名宣布愤怒；e537，483，才成为 known=true,value=true,conflicted=false，condition_truth=true | e672，484（08:04），选 ward.answer.contract.quer；e992，494（08:14），正式执行；e995，494，answer_accepted |
| astral / selma | e225/e226，481（08:01），本人查看伤情；e285 同刻采用 astral.reason.self_preservation，prior=0.6。e370，482（08:02），获得阮承认操作者与 preserve-three 说法；e653，484（08:04），取得半幅图 | e1370，663（11:03），known=true,value=true,conflicted=false，condition_truth=true；其输入包含481的肿伤、484的 half_sketch、482的 admitted_operator/stated_calculation；后续 report_basis 的 e1393 发生于664 | e1412，664（11:04），选 selma.answer.astral.ruan；e1515，674（11:14），执行；e1518，674，接受 |
| logistics / mora | e232，481（08:01），本人开始查看伤情；e296 同刻采用 logistics.reason.experiment，prior=0.8。e390/e391，482（08:02），本人处理伤情后取得酸泡；e495，483（08:03），现场取得 reported_taker=veronica、reported_reagent=R-07 | e1330，662（11:02），已 true；e1377，663（11:03），刷新后仍 known=true,value=true,conflicted=false，condition_truth=true；医嘱缺失由本人初始记忆 false 刷新为663的直接观察 false，并无相反值冲突 | e1386，663（11:03），选 mora.answer.logistics.veronica；e1421，673（11:13），执行；e1424，673，接受 |

原始 e537/e1370/e1377 的 appraisal facet 明确有本人 subject、current-private-appraisal、输入签名和 hypothesis_id。contract 的愤怒条件依赖实际质问结果及私自开柜材料；astral 依赖伤情、半图和操作者说明；logistics 依赖酸泡、无医嘱及试剂取用说明。均同时依赖尚未得到完整经过，而非只由假说名称置 true。假说事件明确标识 authored-preconception-not-evidence；不能把 prior 当客观动机证据。

## 决策影响、调查菜单与事实边界

40-chapter1.dv:3379 起设置本人 angry，3385 用当前 moral conflict 与未完成完整链更新；3395 以 angry=true 或 final 开放 receipt 目标；3434 只在 angry=false 时开放报告人的下一深查目标。因此情绪改变当时任务可用性。行动记录中 e672/e1386/e1412 的父链分别连接本人调查与假说，后两条还经最新 reason_stop/观察连到情绪刷新。这里证明声明条件与实际选择相符，未做反事实重跑，未声称完整候选集合或“没有别的合法行动”。

2848 的常规检视逐条提供房内痕迹；3400附近向每名本房居民提供 resident.truth 的调查动作，并为真实问话搭配各请求人的回应动作。报告人身份只限制主动深查目标，不删除非报告人的调查菜单。深查仍要求现场、前置材料、真实请求和正式执行。完整动机推断只在 final 材料链成立后由本人的 material-reason 推出。

本轮实际执行索引未见 resident.truth.* 执行或 .reason. 完整答复。因此可以说本轮三房没有继续取得完整深层经过；不能说它们无法获得深层真相。动作与前提存在，且愤怒目标门控不等于能力永久封禁。

2867–2918 将人物答复的 literal-actor-only-not-motive 与完整因果答复分开。前者提交人物事实，接受律给 receipt/level/current/clock，不授予 reason；后者需要完整链和对应动机才可成立。30-session.dv:675–724 的评价函数只从 actor.mind 读取输入，检查主体现有作用域，并以 dv_store 替换私有 appraisal 单元；不写 physical，不把解释注入客观世界。此结论限于审阅到的这些接口，未将摘要中的客观只读状态当成人物知识。

## 计时和未覆盖

142.0619999999908秒是 run_debug.py 的 execution_elapsed_seconds：子进程执行段计时，包含该进程内的初始化、运行、记录与正常退出成本。它不是整次工具耗时，也不是NPC决策纯耗时。构建9.203秒另列；后处理149.719秒另列，包含只读封存/投影等工作。causal_check_complete=false、main_causal_supported=false，原因是1秒只读全因果检查超时；主会话exit_code=0与该检查失败须分开。

未执行Godot画面、几何、碰撞、输入或盲玩验收；未做另一轮性能比较；未验证强烈愤怒的可见表现或强度标尺；未证明所有居民均有相同情绪；未把本轮缺少深查解释成能力缺失。读取进程已退出，文件均通过上下文管理关闭，无保留句柄或临时脚本。
