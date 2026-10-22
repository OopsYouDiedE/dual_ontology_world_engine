# S3 无侦探：表面作答的因果缺口

本分支没有修改生产源码。检查发现的关键阻断是现有内容声明把“成见动机假设”明确放在本人的答复被接受之后，同时普通 NPC 的答复目录只提供人物／表面材料答复。单改 20/30 的认知顺序或依赖机制，不能让这些声明形成“先因成见和愤怒而作出表面答复”的因果链；强行绕过声明资格将改变剧情语义。

## 本轮真实行为

运行版本 77e31772，分支 opt/fix-npc-causality。命令：

`python mcp/verification/run_debug.py --out artifacts/perf-opt/aggressive/branch-npc --timeout 300 --detective-profile Standard --detective-zone none --plan-depth 2`

使用现役 Release formal-model 和全部默认优化，不改策略档，不注入状态。以下是本轮原始事件，而非手工重排或补写事件：

| 房间及人物 | 表面答复／正式接受 | 其后才采纳的动机解释 | 原始依赖 |
| --- | --- | --- | --- |
| 甲区 ward | e903 → e904，游戏 494 | e936，游戏 494，contract.reason.theft，偏好值 0.5 | available_when=case.contract.receipt.ward=true；source_preconception.preferred=theft、source_strength=3 |
| 丁区 mora | e1267 → e1268，游戏 673 | e1276，游戏 673，logistics.reason.experiment，偏好值 0.5 | available_when=case.logistics.receipt.mora=true；preferred=experiment、source_strength=3 |
| 丙区 elena | e1301 → e1302，游戏 674 | e1340，游戏 674，astral.reason.self_preservation，偏好值 0.5 | available_when=case.astral.receipt.elena=true；preferred=self_preservation、source_strength=3 |

本轮确实存在先于作答的冲突行为，不能把它们说成全部缺失：ward 的质问 e410（483）先于 quer 的拒绝说明 e514、lucian 的反对搜查 e530、ward 的材料陈述 e547（均 484）；mora 的材料陈述 e1205（663）、selma 的材料陈述 e1233（664）也先于相应答复。但“先发生冲突台词”与“私人情绪／成见在选择中导致只能作表面答复”并不是同一个证据。

上述 hypothesis 详情从原始 DAG 提取并校验了其前缀哈希链。事件同时表明：接受使 receipt 成立，此后假设变得合法，原认知机制随即采纳。这与声明一致，不能当成 dirty 缓存漏唤醒的证据。

## 精确源码阻断

1. `dv_core/modules/40-chapter1.dv:2900` 的 c1_other_preconception 定义 preferred/source_strength/basis；source_strength 只作为元数据保留。20/30 中没有读取 source_preconception 或 source_strength 来影响选择的通用逻辑。当前可见的作用是理由候选的排列次序及来源。
2. `40-chapter1.dv:2939` 的 c1_other_reason_hypotheses 把全部理由候选的 available_when 声明为本人 receipt。不是缓存遗漏了已满足条件，而是答前条件明确未满足。
3. `40-chapter1.dv:2981` 的 reason topic binding 同样要求本人 receipt；公开 current 由 c1_other_accept 在接受后改到 group.reason（约 2814）。将单个 available_when 提前仍不能完整修通话题资格。
4. `30-session.dv:303` 的 dvs_hypotheses 正式核对 dvp_topic、basis 和 available_when；约 1780 的 dvp_topic_scan 正式核对当前问题或已听到的话题声明。绕过这些条件，不属于补齐缺失依赖。
5. `40-chapter1.dv:2867` 的 c1_other_answer_actions 只生成 literal-actor-only-not-motive 答复；`c1_other_character`（2984）把这组答复装入每位普通角色。带 complete-causal-account 的完整经过答复则由侦探专用分支生成（约 3778），当前不存在普通 NPC 可以因本人认知变好而选择的同类完整答复。因此“普通 NPC 只答表面”目前至少部分由行动目录决定，不能全归因于其成见。
6. `c1_other_activity_actor`（约 3550）把冲突性 talk 的语义 speech 设为 dv_true()；台词进入 surface，真实活动旗标作为 public_grants。例：quer_insulted 确实能触发拒绝说明和反对搜查，但这些旗标没有作为 c1_other_answer_actions 的动机或选择约束。现有声明中没有一套可直接接线的“三房强烈愤怒导致停止追究”数值或资格合同。

## 为什么没有交付伪修复

未通过以下方式把事件“做对”：提前授予成见事实、把 source_strength=3 擅自解释为某个新效用分、按 NPC 身份降低深度、忽略话题／available_when、强制先输出 hypothesis 事件，或预设只能答表面。

由于用户明确禁止为基线剧情／模型问题补剧情，此分支保留真实失败原因，不偷偷修改 40/50。也未提交一个仅把 available_when 改成 open、却无法证明影响实际答复选择的半成品补丁。

需要主代理依据用户授权作出的内容决策是：既有偏见何时成为可参与选择的私人假设、真实冲突如何影响本人继续调查的意愿，以及普通 NPC 是否具有同样的因果答复机会。确定这些合同后，才能在 30 中接通通用认知与选择，而不是让通用引擎猜剧情。

## 核验边界

- 三房表面人物答复均获接受；**S3 要求的“因为先有强烈成见／愤怒，所以只能答表面”仍未得到因果证明**。
- 没有执行全关闭对照，因此不把本报告写成“实测全关闭基线也未达成”。可以确定的是阻断直接存在于 77e31772 的静态声明，开启／关闭 dirty 本身不会把 receipt 前提变成答前已知事实。
- 未读取设计文件；只读取现役源码和本轮事件中原本携带的来源引用。
- 原始大文件留在本工作树固定目录，仅提交本小报告及必要指纹。


## 完整运行与封存结果

- 正式会话 **195.453 秒**，在本次 300 秒窗口内完成；未满足原始独占 180 秒硬指标，且本轮与其他分支会话并行，不能作为独占性能结论。
- `e2264` 于游戏 1800 分钟、现实 12.579 秒发布第二章内容。
- `e31348` 于游戏 6330 分钟（第五日 09:30）、现实 **194.160 秒**实际执行 `c2.daily.council-close`；`e31349` 实际执行 tremor。最终 `e31354` 封闭会话，停止原因 `actual-chapter-two-closing-record`。
- 构建 42.734 秒；离线取证与因果检查 477.750 秒。这些与正式推进耗时分开。
- `exit_code=0`；`source_unchanged=true`；`run_config_matches=true`；`logical_loop_finished=true`；`evidence_complete=true`。
- 原始 DAG 571,225,562 字节、31,355 个提交事件、完整 final checkpoint/seal。SHA-256：`3a07937f66965599dcb44f9fc1099124eb7498a6885b744edf36e809280429b3`。
- 全部原始事件已投影，未投影尾部为 0。正文文件 `branch-npc/02-引擎输出的玩家可见动作.txt` 为 513,893 字节，已实际打开核对有玩家可见叙述。该层不包含 Godot 画面／碰撞验收。
- 自动因果检查完成，但 `main_causal_supported=false`。完成记录和正常退出不代表 S3 因果标准通过；本报告仍判定该项未达成。
- 此分支只交付可审查的声明缺口与本轮证据，未编造一个生产修复提交。
