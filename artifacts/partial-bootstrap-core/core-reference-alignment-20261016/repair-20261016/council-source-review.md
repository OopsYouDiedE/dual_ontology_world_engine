# 第五日议事与旧疤动作源审读（2026-10-16）

范围：chapter2-content.dv、chapter2-daily.dv、chapter2-dialogue.dv。仅依据主代理提供的设计条款与源代码审读；未读取设计文档，未编译、构建或运行会话。音乐文件的既有修改未触碰。

## 源码发现与修订

- 旧疤行原归属 bertrand，导致 c2i_actions/c2i_install_laws/c2i_goals 从同一 row 生成贝尔特朗的动作、方法和目标。改 row.actor 与 action id 为 veronica；现在同一生成链全部属于维罗妮卡。assembled 保留茉拉、被救伤员贝尔特朗和维罗妮卡现场会合，r07_skin_test→three_old_scars→medicine_key_given→anti_erosion_mixed 的真实接触依赖未删。three_old_scars 是共享证物字段，后继钥匙与配药依赖保持同一引用。
- 赴会原目标要求已采纳某提案（或提案所有者已提出），阻止未表态者接近现场。改为本人已实际知道至少一个议事提案 text；保留存活、时间、日光与本人所知 wind 的地点选择。该来源不会给不知道提案的人强制路线，也不保证所有人到达。
- 现场发言原只传播 speaker 的 stance；改为对应原生 norm 的完整正文及 text 命题，并携带既有 norm_proposal 合同。实际听闻与随后本人采纳仍是不同步骤。
- 每张选票与每次签名原都要求十六人存活且同处。移除此全体同处前提，保留本人现场执行、存活、议事窗口、风况、未投票/未签署及真实已采纳要求。当地听众由正式 local speech 传播过滤。
- 选票使用同组 personal_choice（council-ballot），原 norm.promotes/sacrifices 作为价值比较依据；只比较本人已经真实接受的提案。所有者提出提案不等同本人已采纳，不直接赋予选票。无任意加分、预置选择或签名。
- council_end 仍要求全十六人的实际 signed 字段。死亡不视为签名；窗口结束或不能收齐签名不伪造终局。震颤呈现不额外要求所有人再次集合。

## 需主代理协调与验收

norm_proposal / norm_decision 在原 DV 内容之外缺少消费者的疑点已交主代理与 repair_core_gates；必须由正式 speech/Complete 接线保证真实收闻只取得提案、本人决策执行才采纳。现场提案 text 仍要经过本人理解，不得直接写 accepted。已向 repair_norm_receipts 发送 text、norm_proposal、norm_decision、accepted.<id> 与 norm_rules 接线合同。

“获知议事机会”当前采取本人实际知道任一议事提案 text 与现有议事窗口构成内容活动机会；主代理确认该边界可用。这不表示角色已知全体邀约，不保证到达；未初始化全体知晓或新增无来源日程邀请。

本记录是源码审读，尚无新正式会话的实际投票、签署或终局证据。不得据此声称章节完成或游戏效果改善。主代理应在全部修复结束后运行授权的一级会话，并逐段核对实际发言、收闻、采纳、选择、回执和后续结果。

最后字段同步：板读动作的 norm_receipt 使用 {norm,content}，content 是 dv_fact(c2_ref(object,"text"),norm.surface)。源码逐项核对该 text 引用也在原 read 动作 grants 中；成功完成且实际观察支持正文后才形成理解回执。此核对未执行编译或会话。
