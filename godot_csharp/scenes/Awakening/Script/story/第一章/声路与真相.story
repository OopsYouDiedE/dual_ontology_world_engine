鞋底对象
    detective：你自己的靴底
    huo：霍铎的鞋
    shen：沈鼎的鞋
    su：苏泠的鞋

声路站位
    foyer：门边
    R08：R-08
    hall：铜口旁
    pantry：备餐间
    R05：R-05

同伴站位
    su：R08
    huo：hall
    shen：foyer

站位候补
    - R08
    - hall
    - foyer
    - pantry
    - R05

话语 闩门别开
    整句：生活翼！我是格雷厄姆。主楼泄漏。闩上隔离门——无论听到什么，都不要开门。守在铜口旁，等我下一次通知。
    句尾：……开门……开门
    句尾含义：开门
    含义：闩门、禁开、守铜口

话语 开门救我
    整句：开门！快开门——救我！
    句尾：……救我……救我
    句尾含义：求救
    含义：开门、求救

试验句 1
    整句：闩上门，不要开门。
    句尾：……开门
    句尾含义：开门
    含义：闩门、禁开

试验句 2
    整句：开门，救我。
    句尾：……救我
    句尾含义：求救
    含义：开门、求救

试验句 3
    整句：守在铜口旁，等我下一次通知。
    句尾：……通知
    句尾含义：
    含义：守铜口

房间区域
    起居厅：*, *, 160, *
    厕：160, *, 304, 256
    门厅：160, 256, 304, 448
    储物间：160, 448, 304, *
    活动厅：304, *, 584, 416
    备餐间：304, 416, 584, *
    走廊：584, 304, *, 448
    R-08：584, *, 816, 304
    R-05：816, *, 1040, 304
    空房 north：1040, *, *, 304
    R-07：584, 448, 816, *
    R-06：816, 448, 1040, *
    空房 south：1040, 448, *, *

真相
    内闩落下：是
    灰扇：是
    楔面沾灰：是
    楔在角落：是
    鞋印：马球靴 朝插闩
    湿布：1
    湿布压鞋跟：是
    音叉在门框旁：是
    门框凹痕：是
    乐谱第一行：1
    乐谱第二行：是
    石板第一行：1
    石板第二行：1
    杯糖：1

# 声学法则：声音怎样走，由铜口、管路接头、门与墙推出；各处听到什么由引擎按此计算，不再手写结果（总纲 §六 第三期）。
# 铜口的声音：铜口所在房间听得完整；松动接头所在房间只漏出重读的句尾；隔墙低语的房间只听见嗡嗡声；其余只有警铃。
# 门外的喊声：门外完整；门内隔门发闷；若门内房间有松动接头，声音钻进管子，从别处的松动接头发闷地漏出。
# 接头可以被拧紧或拆开（现实中的变化），此后的声路试验随之改变；昨夜的痕迹按昨夜的接头状态。
声学
    铜口所在：活动厅
    隔离门：门厅 | 门外
    接头：
        - 门框接头 | 门厅 | 松
        - 墙内接头 | R-08 | 松
    隔墙低语：备餐间
    听来像在门外：句尾、隔门完整
    喊声可闻：活动厅、走廊、R-05、R-06、R-07、R-08

# 昨夜写定的外部输入；人物回应仍由本人规则选择。时间单位是重演行动值。
昨夜时序
    alarm:
        at: 0
        kind: alarm
    voice_first:
        at: 1.0
        kind: voice
        count: 1
    smoke_door:
        at: 2.0
        kind: smoke
    voice_second:
        at: 2.2
        kind: voice
        count: 2
    wake:
        at: detective_wakes
        kind: wake
        actor: Detective
    limit: 160.0

重演法则
    procedures:
        smoke: seq(require(param("smoke_due")), require(tag("door", "Open", 1)), set("floor", "AshFan", 1), when(tag("door", "Wedged", 1), set("wedge", "Soot", 1)))
        footprint: when((param("door_changed") 且 param("at_origin") 且 tag("floor", "AshFan", 1)), seq(when(option("sole", "PoloBoot"), choose(option("direction", "Bolt"), set("floor", "PoloBootBolt", 1), set("floor", "PoloBootDoorGap", 1))), when(option("sole", "WorkBoot"), choose(option("direction", "Bolt"), set("floor", "WorkBootBolt", 1), set("floor", "WorkBootDoorGap", 1)))))
        cloth: when((tag("cloth", "Placed", 1) 且 param("prints_exist")), set("floor", "ClothOverHeel", 1))
    origin: Foyer
    traces:
        PoloBootBolt:
            sole: PoloBoot
            toward: Bolt
        PoloBootDoorGap:
            sole: PoloBoot
            toward: DoorGap
        WorkBootBolt:
            sole: WorkBoot
            toward: Bolt
        WorkBootDoorGap:
            sole: WorkBoot
            toward: DoorGap
