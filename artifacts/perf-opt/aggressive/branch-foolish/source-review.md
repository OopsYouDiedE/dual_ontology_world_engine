# S4 本人策略、脱困与真实同伴反应（2026-10-23）

## 结论与范围

分支 opt/fix-foolish 已先合并 main f79e77f4（合并提交 bc7f06ef），再完成本候选。主代理提供内容边界及用户全部授权；子代理和独立审查不读取设计文件。没有修改丙丁取证或 NPC 成见安装区域，没有在 NPC、判答或规划器读取 profile。

本轮正式 formal-model 对照达到 S4 的有限目标：Standard 四问获接受并自行过门；Foolish 实际拒绝现场查验，没有提交任何答案，三位同伴因真实收到该拒绝而表达异议或不满，后来雀儿实际挑开外闩，Foolish 只先听到金属声，亲到门厅检查，才拔内闩、开门并实际走进起居厅。两个配置同源、seed=1、zone_yi、统一CLI深度上限3、TopK3，既有优化默认开；各自正式窗口300秒，先Standard后Foolish串行运行。没有Godot几何、碰撞、画面或真实输入验收。

## 实现和因果边界

- 通用收益按本人 value_order 首个非零影响评估 personal_choice，但通过本人 value_sensitive_progress 显式启用；只有侦探配置开启，其他 NPC 保留旧 personal_choice 计法。本轮没有用修改所有 NPC 偏好的方式实现差异。
- Standard empirical/counterfactual initiative=1/1，Foolish=0.01/0.03，表达低主动实证/反事实意愿。低收益不禁止合法正确答案。参数是策略声明，不是经过优化求出的唯一最佳数值。
- practical-access 实用检查不受科研意愿缩放。真实开闩动作产生 latch_sound，只有同一现场声道范围内的人收到该声音；不远程授予 outer_released 或通行状态。本人声后目标与现场检查推动正式移动/检查/拔闩/开门/过门。
- 拒绝协作是两个配置共同拥有的合法选择。它促进 avoid_attachment/pursue_desire，牺牲 understand_cost/solve_case/evidence，按本人价值取舍选择。三位 NPC 的反应要求收到该具体行动的真实发言回执，既不预设 Foolish 身份，也不预置怀疑事实。反应是实际对白，未另行伪造持久信任数值。

## 当前轮事件与正文

Standard：

| 结果 | 正式事件 | 游戏时刻 |
| --- | --- | --- |
| 闩门者答复获接受 | e1470 | 第1日08:21 |
| 声源答复获接受 | e2123 | 第1日09:45 |
| 原话答复获接受 | e2281 | 第1日10:05 |
| 信任缘由答复获接受 | e2525 | 第1日10:29 |
| 声后门闩现场复查 | e2629 | 第1日10:32 |
| 拔内闩、开门、实际过门 | e2650 / e2665 / e2692 | 第1日10:33 / 10:34 / 10:36 |

Standard 没有选择拒绝协作，也没有触发对应三人反应。

Foolish：

| 实际行为与结果 | 正式事件 | 游戏时刻 |
| --- | --- | --- |
| 拒绝现场查验 | e767 | 第1日08:08 |
| 霍、沈、苏收到拒绝 | e770 / e772 / e774 | 第1日08:08 |
| 霍、沈、苏实际解析收到的发言 | e776 / e783 / e792 | 第1日08:08 |
| 苏泠、霍铎、沈鼎实际表达反应 | e976 / e1008 / e1646 | 第1日08:12 / 08:13 / 08:24 |
| 雀儿正式挑开外闩 | e2232 | 第1日13:17 |
| 侦探仅收到 latch_sound=true | e2244 | 第1日13:17 |
| 侦探走到门厅 | e2266 / e2267 | 第1日13:19 |
| 实际检查及首次获知当前外闩/门状态 | e2316 / e2317 | 第1日13:20 |
| 正式拔内闩、开门、实际过门 | e2343 / e2371 / e2405 | 第1日13:21 / 13:22 / 13:24 |

Foolish 全部记录中，侦探 answer_submitted / answer_accepted / answer_rejected 均未发生；这是本轮事实，不推断任意长会话中绝不可能正确答复。

拒绝正文为“查管道、重演这些事我不做。你们去查，结论告诉我就行。”霍铎随后质疑其日后结论，苏泠要求其勿替同伴下结论；这两次反应实际发生时侦探在门厅，因此不在侦探可见正文中。沈鼎随后在侦探已回活动厅时说“大家还关在里面，你倒把活全推给别人。这样搭伙，我可不放心。”这次不满出现在侦探可见正文中。不能把后两人的未听见对白写成侦探已经获知的态度。

Foolish e2244 的真实观察仅为 departure.door.latch_sound；outer_released=true、inner_down=true、open=false、living_exit=true 直到本人e2317现场检查才进入其观察。未给声响添加远程门状态答案。

## 封存、工具结果与未覆盖

两个配置 source-hashes.json 完全相同；result.dvlog 均报告 source_unchanged=true、evidence_complete=true、raw_sealing_complete=true。各自有完整玩家可见正文、stdout二进制事件投影、带校验原始DAG及最终封存。当前新测试替换此前Standard/Foolish目录，没有拼接旧证据。

- Standard 正式会话222.906现实秒，36,414提交事件；原始SHA-256 dab8eb4744a7969ceaebfaee813ac1741bbacf5492a6f76535f0973b4a51e2c9。只读因果诊断超过300秒，causal_check_status=timeout，不能宣称完整因果诊断成功。
- Foolish 正式会话224.704现实秒，36,439提交事件；原始SHA-256 b0002ba84ad744e4120d97aa306197d48f65d750943c69fbc8626bc6ab8a184e。只读因果检查完成，但main_causal_supported=false；未完成第一章答辩，不能伪装成剧情成功。
- 两者本轮停止均有 objective-chapter-two-closing-record 和 actual-viewer-closing-inspection。Standard e36411、Foolish e36436 是第5日09:31观察者实际章末检视。这个结束事实不替代其他章节目标的逐项验收，也不作为剧情体验完全成功结论。
- 会话时长仅记录窗口事实；这次修改包含本人策略和内容行为变化，未做同条件速度改善结论。

证据位于本目录 Standard/ 与 Foolish/ 下 result.dvlog、02-引擎输出的玩家可见动作.txt、actual-event-index.dvlog、stdout.dvlog、raw-events.dvdag、source-hashes.json。巨大原始证据和编译工程仅保留本机，未提交Git。

## 独立只读复核

新上下文 s4_evidence_review 未读设计、未运行额外测试，独立核对提交/接受、实际出门、拒绝/收听/同伴异议及声后门状态观察的上述事件顺序，结论支持本轮有限目标。它未全量重新解码原始DAG，不声称完整因果证明。

保留的呈现风险：现有 surface-action 正文将雀儿动作写成“细铁丝拨了四下，外侧的横闩抬了起来”，比主体内部实际取得的纯声响事实更确定。内部知识链未越权，但本轮没有修正该呈现来源差异，也不能把此句当成侦探现场目击。未因这项风险另改源码而拼接不同源码的验收。
