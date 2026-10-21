# 07 独立正式证据审查

2026-10-21；全新独立审查上下文。未读取设计文件、生产剧情源码或其他轮私有记录；只使用本目录原始证据、二进制读取工具及本轮公开报告。没有启动或续跑会话、修改源码、代选行动。以下为引擎侦探本人决策的会话审查，不是独立盲玩。

本轮能确认正式检视、一次付费重演和侦探四项答辩接受；不能确认完整正确推演、E6热点近距接触、实际第二章交接或最终结局。300秒采集结束时会话仍活动；随后关闭完成并封存。

## 原始轨迹与输入

`host-session/session.dvdag` 经 RawDagIndex 验证267,691帧、267,687节点，107,310,866字节，SHA-256为 `a1021d6e75c39ddfc382bc84bbbe588092fc80f478db6dc83fcfed390b0ea57d`，无无效尾部、无剩余或未提交后缀，initial/final/seal完整。

包装层 initial/final 都指向root267686，committed_events=0；实际展开该root的events节点166808，含6,620项e0至e6619，其中477条action_execution、477条host_completion、7条mind_simulation、14条answer_accepted。这里的0只是该完整值导出包装的增量提交计数，绝不表示没有游戏事件。

`inputs.dvlog`共1,343条：AI配置、start、view_ready、476次dv_next、477次dv_complete及宿主输入。配置为四倍播放、240物理tick、每帧最多32物理步。`run/run.dvlog`声明由Input.parse_input_event输入、无桌面设备。本审查没有向会话提供输入。原始输入证明宿主实际执行链，不能单凭AI配置证明任意内部决策都正确。

## 行动前信息、检视和空间边界

开局e137先给出上船记忆、风衣识别及出去查看的目的。石板e1136（现实22.000秒，游戏08:08）与管线e1245（29.995秒，08:09）先于声路试验e1527（46.225秒，08:34）。隔离门e1620（50.744秒，08:38）、地面e1635（59.904秒，08:39）、靴印e1648（64.573秒，08:40）、抹布e1663（69.104秒，08:41）、音叉e1677（72.246秒，08:42）均有applied正式结果与后续正文。

这不是把打开界面当检视：抽核action104、131至135、219、470都有对应Complete/applied及实际费用。但这些检视的field为executor=dv-world、contact=true、site空，缺少独立热点距离/站点到达证明；因此只确认正式观察与玩家可见内容，不把contact布尔值扩大成逐热点近距碰撞验收。

E6的房间移动与检视须分列：e3092/3093是侦探从zone_yi:corridor真实到zone_yi:north_2的Godot移动，actor_instance_id=173107319608，arrival_error=0、tolerance=4、status=arrived；e3110是随后action219正式检视（161.738秒，10:28）。其正文包含手写乐谱、问号及重读“开门”等内容。164.518秒PNG确实显示该正文，但角色在右下、乐谱桌在左下，不能补称已靠近乐谱。

唯一重演前的mind_revision357中，侦探已知slate_marks、bolt_down、boots_toward_bolt、cloth_after_boots、slate_person、me、words_bolt；不是读取作者物理真值后补写其认知。原始完整候选比较中的木楔烟灰和乐谱观察仍unknown。后来的乐谱检视不回填早先推演。

## 付费推演与逐项答辩

唯一正式重演为action207/e2843，现实144.355秒送达、游戏09:59。Complete实际费用time29、computation1、stamina0；ledger_before/after的time从99910降至99881、computation从99958降至99957，保留真实结算证据。

七条mind_simulation中，e2846/e2848/e2850为浅层declared-plan-complete、explained，但各自只比对“石板第一行刻痕”。e2847的六变量组合为R08/hall/Tube/BoltKeepShut/huo/true，完整声明计划完成、24阶段、7项比较，状态却为unknown：5项match，木楔烟灰与乐谱unknown。e2849完整候选inconsistent；e2851为budget-exhausted、plan_complete=false、剩余0。正文也明确“尚未形成能够完整说明现有观察的解释。推演总预算已耗尽。”本窗口没有完整正确重演成立；E6之后没有第二次正式重演。

下表现实点是宿主结果送达，非正文首帧；游戏钟为原始事件值，均非倍率换算。

| 本人实际答辩 | 执行／接受事件 | 现实送达 | 游戏钟 |
| --- | --- | --- | --- |
| 是我闩的门 | e2913／e2915 | 147.040秒 | 10:11 |
| 声音来自铜口，经管道传来 | e2995／e2997 | 153.330秒 | 10:22 |
| 原话是闩门禁开 | e3170／e3172 | 169.655秒 | 10:42 |
| 因早已相熟而信霍铎 | e3342／e3344 | 186.428秒 | 11:01 |

四条接受均accepted=true，literal_truth、epistemic_support、question_match、answer_complete均为true，最终question.current/stage=complete且四个solved均true。全体14次接受中另外10次属于其他主体，不能当侦探14次，也不能以四答接受替代完整正确重演。150.109秒PNG直接显示“本次答辩已获接受”。

## 章节、截止与关闭

后续有拔闩e3470、开门e3485、跨出进入起居厅、夜间活动、晚餐及再次检视鞋底e6461；这些是真实后续进展，不等于实际第二章交接。

采集300,037ms结束时`end.dvlog`为active=true、chapter=1、chapter_summary=false、game_time=1830（次日06:30），pending仍为已Prepare的mora/action476。首个记录3,050ms为480分钟，实测游戏钟推进1350分钟只描述时间范围，不代替剧情节点。

尾部e6615 host_completion及e6616 action_execution完成action476；e6618仅为chapter_content_available、chapter=2、movement=false；e6619 session_stopped明确reason=host-closed-during-active-session、actions_executed477。实际交章在本窗口未发生，不能据此判定整局无法交章。也不能把规划停止、游戏钟终点或引擎退出0叫作剧情结束。

关闭结果complete=true、audit_mode=true、trace_policy=full、unprocessed_commands=0、pending_action空、errors空。捕获时prepared与关闭后pending空各自成立；关闭等待了在途Complete，不能用末帧prepared断言封存后仍悬挂行动。`capture-close.dvlog`为314,086ms完成，archive_errors空，距捕获终点14.049秒。

`save.dvsave`逐帧二进制校验及序号、前驱、SHA链校验268,408帧成功，最后seal声明final_checkpoint=true；SHA-256 `7ce90dcfe62c117be9b1ab63e3dd09fd27140399a88e22d6c5db78ccdf6480ec`与save.casebook.dvlog绑定值一致。目录没有.partial。输入、可见记录、截图、完整原始值和存档均在。没有执行恢复测试，故不以存在存档宣称恢复行为已验收。

## 实际图像审查

实际查看以下4张原始PNG，未重绘或裁切：

- 60.175秒：乙区门厅、侦探地面检视正文；顶栏和对话框可辨，无明显文字遮挡。
- 150.109秒：乙区活动厅，对话框明确“本次答辩已获接受”。
- 164.518秒：R-08、乐谱检视正文，角色与乐谱桌分处房间两侧；近距接触证据不足。
- 299.896秒：生活翼公共起居厅，HUD06:30，无对白；不替代后台关闭和章节事件。

这些画面只覆盖抽查时点，不是全场景、全遮挡或全部输入/碰撞体验验收。可见记录中的未命名物品仍应如实保留，未据此推断全局资源缺失。

## 与本轮公开性能报告对照

已读取父目录`engine-investigation-report-07.md`。其主要行为结论与本审查原始证据一致：四项本人接受、14项全主体接受、唯一付费重演但完整解释未成立、E6正式检视与空间接触边界、active第一章截止、关闭后pending清空、chapter_content_available不算交章、477执行与捕获前476呈现的尾部差异。

本审查未独立重算托管栈占比、每阶段总耗时或跨轮重复路径比较，故不把这些数字当作本独立审查另行验证的性能结论。公开报告保留完整审计、截图、采样及后台重叠限制，没有据此声称普通游玩同条件提速。本审查没有发现需更正其主要本轮行为结论的证据。

运行和审查均已结束；本目录可用于全量carry。本审查仅新增此报告，不改写任何原始记录。
