# 独立工程与一级实际证据审查（2026-10-18）

本报告由本轮新建独立上下文 Agent 编写。只读根、language_core、verification 的流程约束、所列工程证据、相关机制源码及本轮实际轨迹；未读取设计文件、主代理设计报告或权威答案包，未启动游戏、prepare/complete 或回归入口。人物与设计符合性由主代理另行核对。本报告只写入本文件。

## 结论与证据完整性

本轮正式一级 formal-model 会话因工程异常退出，不能给出完整一级验收结论。`run-180s/result.json` 明确记录 exit_code=1、elapsed_seconds=71.297、logical_loop_finished=false、evidence_complete=false。它没有用满180秒，也不是剧情终局、行动上限或时域耗尽停止。

同文件记录 build_source_consistent=true、source_unchanged=true；现存 stdout.jsonl、events.jsonl、玩家正文可解析，但没有最终 trace、partial trace 或 debug_end。审查时主代理已告知后续将修复源码，因此下文动态结论仅归属于 run-180s 封存版本，不能外推给随后工作区。

stderr 的实际错误是 `Repeated observation`，调用链为 dv_update_mind ← dvs_public_grants ← dvs_session_complete ← dvg_complete。最后原始即时行是 e12329、gu.c2.document.apology、2329分钟、applied；该行在Complete内部提前打印，没有对应完整debug batch和玩家正文，不能证明该次Complete收尾、所有听者收闻或状态完整保存。最后完整导出的批次止于e12322（2328分钟）；正文止于2320分钟的清册拼合。

二级UI、三级Godot几何/碰撞/交互均未执行、未验证。没有用工程探针替代这些结论，也没有同条件性能改善结论。

## 工程证据审读

- syntax-probes-01 的 branch-boundaries 实际输出为 `["nested","outer","independent","fallback",2]`；comprehension 实际输出包含嵌套列表、空列表及negative/zero/one/many分支。错误运行输入明确产生 Expected Bool、Expected List。不可变重绑定、游离/隔行elif等错误保留生成阶段诊断。它们只支持所给语法输入及错误边界，不能证明正式会话行为。
- runtime-probe-evidence/result.json 将formal_session_executed标为false；记录trace精确数为字符串、save精确数为$dv_rational对象、300层数组、Save/WriteSave一致、恢复往返一致、冻结证据不可变，以及已有正式/partial文件字节保留。该范围不覆盖真实崩溃会话最终证据导出；本轮正式异常没有trace，恰需分开报告。
- checks-neutral-probe 与 extended 的实际stdout包含dispatch仅one且排除two、两个输出被mask、replay-depth-exhausted、accepted/degree、撤回事件完整actor_before/after，以及扩展失效事件。命令记录为独立中性main，未推进正式会话。输出支持这些中性输入，不覆盖未知selector、复杂后代预算阻塞或全部冻结来源验证分支。
- artifacts/rejection-probes-01/execute.stdout.txt 明确显示普通repeatable仍有资格；相同失败签名、无关revision改变和未越过既有阈值的时钟变化不释放资格；相关access、位置或predict法则改变后允许重试；失败表改变规划缓存输入。这没有调用正式执行，允许重试不等于执行成功。
- 冻结前宿主构建日志只证明该解决方案完成编译，不作为人物或游玩效果证据。

## 异常的独立因果定位

run-180s/program/DVProgram.cs 的冻结生成代码在9743行先调用dvs_visible_action_grants，9754行随后调用dvs_public_grants，均传入同一action_event。c2i_actions的调查动作同时携带public_observers及public_grants；gu_apologized被转成talk，增加speech/listeners，仍保留上述字段。

两路均可对同一现场主体、同一ref、同一执行编号生成感知。第二路dv_update_mind拒绝已存在观察，与stderr的Repeated observation及落点一致。这是源码与实际栈吻合的根因定位，不声称已有完整失败现场快照能指出具体首个重复听者；该快照因异常没有导出。修复应保留原重复事件约束与真实来源区别，不能据此删除校验或把完整Complete当作已完成。

## 实际行为分段核对

### 开场、调查、答辩与出门

玩家正文从本人独白、披衣、移动进入同处交流；开场他人身份先由实际自我介绍显露，未具名发言仍保留未具名。09:31题目开放后，正文依次呈现现场记录、门闩/脚印/抹布、木楔核对和触觉闪回；后续声路试验明确说明仍需询问实际在场者，重演正文明确区分推演和正式答辩。

正式接受回执分别由e2315@610、e2465@646、e2885@695、e3054@712记录，detail同时保留literal_truth与epistemic_support。随后真实逻辑执行e3194@718为侦探cross-out，正文为11:58跨门进入起居厅。这里区分了信息出现、试验/重演、正式接受与实际移动，没有用候选出现替代推进。未读设计和权威答案，不能独立判定这些答案在设计层是否充分。

正文中的触觉闪回分类、对白/动作叙述分段和规范表态换行实际出现。开场普通移动仍只说“沿通路走到相连的房间”，相较第二章含起终点的移动，读者仅凭正文较难追踪位置；这是实际文本可读性限制。14:19仍显示“未具名主体”而正文提及沃德，21:01未具名发言正文自称雀儿；本报告不在缺少本人身份认识投影时猜测映射错误。

### 拒绝与等待

原始动作输出中的rejected为e3518@833的quer.departure.pick-outer和e4666@1113的gu/night.corroborate。后续已导出轨迹未见相同动作再次执行。窗口没有实际rejected移动，故不能声称本轮已动态验证“第二章反复被拒移动”修复，相关失败现场观察也未触发。

正文及排队事件实际显示等待收短：侦探从2256开始15分钟、2271开始5分钟、随后连续1分钟直到2280，再移动去调查。第二章白天存在30分钟等待；e12317@2320排队到2350的等待尚未完成便遇到会话异常，不应计为已执行。第一日仍有180分钟等待，不能把参数概括为所有章节、全天最多30分钟。

这些记录支持“本窗口确实发生5/1分钟片段并在边界后重新行动”。没有逐项完整wait_basis与所有非时钟前提的最终快照，不能宣称所有等待均未跨越本人机会，或无配额损耗问题。短片段多次出现本身也不能用作体验改善证明。

### 第二章与真实调查协作

e5369@1830是chapter_content_available，明确movement=false；它不等于跨门。侦探第二日09:07的正文及对应实际移动才显示从起居厅进入中央厅。随后在课室、中央厅、起居厅与文献工房间实际移动，不能把这些位置变化单独当作调查完成。

e12015@2285侦探实际进入文献工房，e12044@2286现场检视；正文此时清楚列出锁具、两半清册及拼合尚未完成。接着e12168@2300沃德出示半纸，e12181@2303侦探寻找大半清册，e12188@2303雀儿拆锁。e12309@2320侦探实际拼合；e12310为本人joined_inventory=true的direct/perception观察，e12311–e12313为同处雀儿、沃德、顾平章取得该现场结果。这里存在真实操作→本人/同处观察的连续证据，不能再把本窗口的文献调查描述成完全没有接线。

顾平章其后道歉只留e12329即时applied行，随后发生上述异常。没有完整收闻、正文或最终状态，故该行为的传播和后续结果记录不足。其他调查线、后期协作、章末议事/签名、closing检视、余震、系统结算和7200时域均未在本窗口发生；不能由源码声明或局部文献调查推断它们达成，也不能据本窗口推断整局无法达成。

## 本轮边界

失败签名源码只读本人前提语义、预测采纳与位置，不把无关revision或每分钟时钟增长当作重新尝试依据；失败后仍记used，applied才写completed_actions。失败观察明确限formal-model、宿主回执applied且现场条件成立，不能外推为Godot现场观察已经验证。

等待与收尾导航属于声明和行为条件变化；静态可见的已知路径、完成标记和时窗门槛不能替代后期实际执行。当前证据足以定位本轮工程中断，并确认截至文献拼合的实际逻辑推进；不足以验收完整一级会话、最终章节收束、人物行为合理性或游玩体验。
