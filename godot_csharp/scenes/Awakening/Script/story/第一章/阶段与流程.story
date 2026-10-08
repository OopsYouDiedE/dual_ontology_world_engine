# ====================================================================
# 剧情推进：阶段、触发、演出流程。读这一段就知道第一章怎么往下走。
# 阶段 = 玩家此刻处在剧情的哪一步，决定能不能走动、能点什么、哪些门开着。
# 流程 = 一串按顺序发生的事：演出（场面）、指令（宿主动作）、改阶段、设旗、判断。
# `{人}` 等写在花括号里的是流程开始时传入的参数。`结束` 只能放在流程末尾或某个“若”分支末尾。
# ====================================================================

阶段 wake
    说明：醒来。对白锁定，不能走动。

阶段 roam
    说明：醒来后在自己房里与门厅一带走动；还不能进别人的房间。走进活动厅，就撞上广播。
    走动：是
    日常：是
    检视：是
    可见热点：E12、E11a
    进入 hall：阶段 meeting，信号 meeting

阶段 meeting
    说明：三人聚在活动厅，听广播、读守则、被问起。对白锁定。

阶段 investigate
    说明：自由调查。能记笔记、重演、进别人的宿舍，随时回铜口作答。
    走动：是
    日常：是
    检视：是
    笔记本：是
    宿舍开放：是
    房间事件：是
    可见热点：全部

阶段 playback
    说明：昨夜重演播放中。
    宿舍开放：是

阶段 answer
    说明：始业之答。
    宿舍开放：是

阶段 finale
    说明：门开或歇息的收尾演出。
    宿舍开放：是

阶段 ended
    说明：结局卡已出。

阶段事件
    初始：wake
    相遇：meeting
    重演开始：playback
    重演结束：investigate
    章末：ended

流程 醒来
    阶段 wake
    指令 lock
    演出 s1_wake
    阶段 roam
    指令 unlock
    指令 hud

流程 相遇
    演出 s2_meet
    指令 name
    演出 s2_tea
    指令 stage_action:shen:备餐灶台:interact:back:1.5
    指令 stage_actor:shen:meeting:Chef
    演出 s2_tea_return
    演出 s3_broadcast
    指令 board
    演出 s4_rewards
    指令 stage_action:shen:领取柜:interact:back
    演出 s4_rewards_shen
    指令 stage_actor:shen:meeting:Chef
    指令 stage_action:huo:领取柜:interact:back:0.8:inspect
    演出 s4_rewards_huo
    指令 stage_actor:huo:meeting:Engineer
    指令 stage_action:su:领取柜:interact:back
    演出 s4_rewards_su
    指令 stage_actor:su:meeting:Musician
    演出 s4_rewards_question
    指令 stage_action:detective:领取柜:interact:back
    演出 s4_rewards_empty
    指令 stage_actor:detective:meeting:Detective
    演出 s5_question
    若 旗("rest_chosen")：
        指令 rest
    否则：
        演出 s6_begin
        指令 post:shen
        演出 s6_begin_huo
        指令 post:huo
        演出 s6_begin_su
        指令 post:su
        设旗 posts_assigned
        演出 s6_begin_detective
        阶段 investigate

流程 重演之后
    若 重演次数 == 1 且 非 重演吻合：
        演出 replay_first
    若 重演被拒 且 非 旗("refusal_seen")：
        设旗 refusal_seen
        设旗 v6_revealed
        演出 replay_refused
    若 重演吻合 且 非 已吻合：
        记录 state full_match_seen = true
        指令 sfx:answer_ok
        指令 summon:huo
        指令 summon:shen
        指令 summon:su
        演出 s8_reversal

流程 交谈
    参数：人
    指令 summon:{人}
    若 先开口("{人}")：
        详情 life {人} told = true
        演出 alone_{人}
    演出 talk_{人}

流程 请人看看
    参数：物、人
    参数来源：协助者
    指令 summon:{人}
    演出 help_{物}
    记录 helped {物}
    # 东西是他自己的：拿在手里，会不由自主地闪一下。闪过之后要不要往下想，由他自己定。
    指令 flash:{物}

流程 实验·默认
    参数：物
    演出 exp_{物}
    记录 experiments {物}

流程 实验·E8
    指令 sound

流程 实验·E1
    参数：物
    演出 exp_E1
    记录 experiments E1
    # 手握着插闩：闪回由身体接触引起，不由按钮提议。
    指令 flash:E1

流程 实验·E3
    参数：物、人
    若 "{人}" == ""：
        结束
    若 "{人}" != "detective"：
        指令 summon:{人}
        演出 sole_{人}
        记录 soles_compared {人} = 文案:鞋底·{人}
        结束
    演出 exp_E3
    记录 experiments E3
    若 非 旗("reversal_one")：
        设旗 reversal_one
        演出 s6_reversal

流程 实验·E11
    参数：人
    若 非 (是同伴("{人}") 且 在房间("detective", "R05") 且 同处("detective", "{人}") 且 非 忙碌("{人}"))：
        结束
    若 非 有记录("hand_comparisons", "{人}")：
        演出 help_E11_{人}
        记录 hand_comparisons {人}
    若 有记录("hand_comparisons", "detective") 且 有记录("hand_comparisons", "huo") 且 有记录("hand_comparisons", "shen") 且 有记录("hand_comparisons", "su")：
        记录 experiments E11

流程 答辩·开始
    阶段 answer
    清空 answer_state
    记录 answer_state step = 1
    指令 music:confrontation
    演出 answer_open
    继续 答辩·提问

流程 答辩·提问
    参数来源：答辩步
    若 记录值("answer_state", "step") > 4：
        指令 finale
        结束
    演出 answer_q{步}
    指令 answerpick
    继续 答辩·裁决

流程 答辩·裁决
    参数来源：答辩裁决
    若 "{裁决}" == "none"：
        演出 answer_pause
        指令 music:door_side
        阶段 investigate
        指令 hud
        结束
    若 "{裁决}" == "ok"：
        指令 sfx:answer_ok:-4
        演出 answer_ok{步}
        记录 answer_state step = {下一步}
        继续 答辩·提问
        结束
    若 "{裁决}" == "need_test"：
        演出 answer_need_test
        继续 答辩·提问
        结束
    # 补救：支撑这个命题的证物都已毁去时，代管者要的是有人作答——它以记得的原样认下正确的陈述（总纲 §六 第三期）。
    若 "{裁决}" == "rescue"：
        演出 answer_rescue
        指令 sfx:answer_ok:-4
        演出 answer_ok{步}
        记录 answer_state step = {下一步}
        继续 答辩·提问
        结束
    指令 sfx:answer_wrong:-3
    演出 answer_{裁决}
    继续 答辩·提问

流程 终局
    阶段 finale
    指令 finale_prepare
    指令 stage_actor:huo:终局站位·huo
    指令 stage_actor:shen:终局站位·shen
    指令 stage_actor:su:终局站位·su
    演出 s10_steps
    指令 door_open
    设旗 door_open
    指令 redraw
    演出 s10_prints
    若 试过("E11")：
        演出 s10_hand
    指令 su_approach
    演出 s10_open
    指令 save:false
    指令 ending:完成

流程 歇息
    阶段 investigate
    指令 lock
    若 非 旗("posts_assigned")：
        指令 posts
        设旗 posts_assigned
    演出 ending_rest
    清旗 rest_chosen
    指令 unlock
    指令 hud

流程 确认歇息
    清旗 rest_chosen
    演出 rest_confirm
    若 旗("rest_chosen")：
        继续 歇息

# 始业之答：四问各自的标准。陈述、出示是玩家在答辩面板里选的两样东西。
# 说错时铜口只指出“它说明的是别的事”；没做过声路试验就拿铜管作答，铜口会先问一句。
# 每问认定一个命题：陈述须说出它，出示须是本局世界里真实支撑它的证物（证物的“支撑”）。
# 支撑写在证物上，可带条件（若 …）或须先做过该物的试验（须先试验）；判定由编译器从支撑推出，不再手写出示名单（总纲 §六 第三期）。
判定 1
    问题：门，是谁闩上的？
    命题：门是我闩的
    认定陈述：me

判定 2
    问题：门外，有人呼喊吗？
    命题：门外无人
    认定陈述：V3_0

判定 3
    问题：他喊的是什么？
    命题：他喊的是闩门别开
    认定陈述：V4_0

判定 4
    问题：闩门的人，为何不听门边那位女士的？
    命题：不听门边那位的缘由
    认定陈述：trust