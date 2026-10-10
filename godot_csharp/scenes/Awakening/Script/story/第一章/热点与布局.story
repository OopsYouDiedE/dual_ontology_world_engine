热点 E1 隔离门
    证物：E1
    位置：150, 372
    触及：56.0

热点 E2 墙角的木楔与门底的灰
    证物：E2
    位置：270, 436
    触及：46.0

热点 E3 灰上的鞋印
    证物：E3
    位置：162, 410
    触及：40.0

热点 E4 门底的湿布
    证物：E4
    位置：154, 390
    触及：40.0

热点 E5 地上的音叉
    证物：E5
    位置：186, 434
    触及：42.0

热点 E6 书桌上的乐谱
    证物：E6
    位置：652, 176
    触及：52.0

热点 E7 长桌上的石板
    证物：E7
    位置：500, 242
    触及：56.0

热点 E8 铜口
    证物：E8
    点击：铜口菜单
    位置：500, 92
    触及：56.0

热点 E8j 墙上的管接头
    证物：E8
    位置：164, 330
    触及：40.0

热点 E8r 墙上的管接头
    证物：E8
    位置：592, 206
    触及：52.0

热点 E9 水槽里的杯子
    证物：E9
    位置：352, 524
    触及：56.0

热点 E10 碗柜上的罐子
    证物：E10
    位置：512, 502
    触及：56.0

热点 E11a 书架
    证物：E11
    位置：862, 96
    触及：56.0

热点 E11b 书架
    证物：E11
    位置：862, 512
    触及：56.0

热点 E11c 书架
    证物：E11
    位置：638, 512
    触及：56.0

热点 E11d 书架
    证物：E11
    位置：638, 96
    触及：56.0

热点 E12 衣架上的风衣
    证物：E12
    位置：1010, 244
    触及：52.0

额外热点 BOARD
    种类：board
    目标：board
    位置：400, 108
    触及：52.0
    标题：公告框

额外热点 huo
    种类：person
    目标：huo
    触及：44.0
    标题：霍铎

额外热点 shen
    种类：person
    目标：shen
    触及：44.0
    标题：沈鼎

额外热点 su
    种类：person
    目标：su
    触及：44.0
    标题：苏泠

额外热点 quer
    种类：person
    目标：quer
    触及：44.0
    标题：雀儿

额外热点 lucian
    种类：person
    目标：lucian
    触及：44.0
    标题：卢西安

摆设 NoticeFrame
    贴图：notice_frame.png
    位置：400, 80
    层：96.0

摆设 Slate
    贴图：slate.png
    位置：500, 240
    层：272.0

摆设 Score
    贴图：score.png
    位置：652, 174
    层：193.0

摆设 Piano
    贴图：res://ShareAsset/Internal/Props/story_parlor_piano.png
    位置：700, 110
    层：110.0

摆设 Cups
    贴图：cups.png
    位置：352, 522
    层：540.0

摆设 Jar
    贴图：jar.png
    位置：512, 486
    层：540.0

摆设 Fork
    贴图：fork.png
    位置：186, 438
    层：438.0

摆设 Wedge
    贴图：wedge.png
    位置：270, 440
    层：440.0

摆设 OilHook
    贴图：oil_hook.png
    位置：992, 504
    层：516.0

摆设 Whetstone
    贴图：whetstone.png
    位置：652, 594
    层：617.0

地点名单
    - 门厅
    - 活动厅
    - 备餐间
    - R-05
    - R-08
    - 四间宿舍

人名
    detective：侦探
    huo：霍铎
    shen：沈鼎
    su：苏泠
    quer：雀儿
    lucian：卢西安

演员节点
    detective：侦探
    huo：工程师
    shen：厨师
    su：音乐家

现场表现
    player_actor：detective
    room_owners：
        R05：detective
        R06：huo
        R07：shen
        R08：su
        EN：-
        ES：-
    stations：
        self：
            anchor：起点·{who}
            geometry：start
        piano：
            node：Piano
            geometry：instrument
        tube：
            anchor：site:E8
            geometry：site
        sink：
            anchor：site:E9
            geometry：site
        door：
            anchor：site:E8j
            geometry：site
        score：
            anchor：site:E6
            geometry：site
        table：
            node：StudyTable
            geometry：wide_table
        prep：
            node：Worktable
            geometry：worktop
    replay_objects：
        door：door
        wedge：door
        floor：-
        frame：door
        slate：slate
        tube：tube
        score：desk
        cloth：sink
        cups：sink
        fork：-
    replay_requests：
        bolt_request：
            event：request_bolt
            spot：door
        open_request：
            event：request_open
            spot：door
        keep_open_request：
            event：request_keep_open
            spot：door
    replay_rooms：
        R05：930,200
        R06：930,600
        R07：706,600
        R08：706,200
        corridor：800,388
        hall：440,340
        pantry：470,652
        foyer：224,388
        lounge：64,388
    replay_spots：
        door：
            room：foyer
            point：178,384
        tube：
            room：hall
            point：500,132
        slate：
            room：hall
            point：532,290
        desk：
            room：R08
            point：690,214
        sink：
            room：pantry
            point：352,560
    replay_offsets：
        detective：-22,4
        huo：22,4
        shen：-6,18
        su：10,-12
    replay_room_layouts：
        corridor：row
    traces：
        ash：
            slot：0
            texture：ash_fan.png
            truth：AshFan
        boots：
            slot：1
            texture：boot_print.png
            truth：Prints
        cloth：
            slot：2
            texture：wet_cloth.png
            truth：ClothOverHeel
        joint_west：
            slot：3
            texture：pipe_joint.png
        joint_east：
            slot：4
            texture：pipe_joint.png
        dent：
            slot：5
            texture：frame_dent.png
            truth：FrameDent
        shelf_R05：
            node：BookcaseR05
            texture：shelf_dust_hand.png
        shelf_R06：
            node：BookcaseR06
            texture：shelf_dust.png
        shelf_R07：
            node：BookcaseR07
            texture：shelf_dust.png
        shelf_R08：
            node：BookcaseR08
            texture：shelf_dust.png
        shelf_EN：
            node：EmptyBookcaseEN
            texture：shelf_dust_even.png
        shelf_ES：
            node：EmptyBookcaseES
            texture：shelf_dust_even.png
    composites：
        res://ShareAsset/Internal/Props/FirstAwakeningInterior/nightstand.png：
            lamp：
                x：6
                y：4
                width：8
                height：13
                word：床头油灯
            cabinet：
                x：1
                y：16
                width：18
                height：20
                word：床头柜

房间名
    R05：R-05
    R06：R-06
    R07：R-07
    R08：R-08
    corridor：走廊
    hall：活动厅
    pantry：备餐间
    foyer：门厅
    lounge：门外
    empty：空房

精灵热点
    # 生活翼地图上画着某件证物的精灵节点名：证物或热点编号（玩家视野记录合并同一物件用）
    DoorFace：E1
    DoorGap：E1
    CopperTransmitter：E8
    NoticeFrame：BOARD
    Slate：E7
    Score：E6
    Cups：E9
    Jar：E10
    Fork：E5
    Wedge：E2
    OilHook：L6
    Whetstone：L7
    CoatStandR05：E12

家具旁白
    map.awakening_yi.window.EN：窗。外面是灰的，什么也看不清。
    map.awakening_yi.window.R05：窗。外面是灰的，什么也看不清。
    map.awakening_yi.window.R08：窗。外面是灰的，什么也看不清。
    map.awakening_yi.window.hall：窗。雾贴着玻璃。
    map.awakening_yi.rug.R08：地毯。
    map.awakening_yi.rug.R05：地毯。踩得发亮的地方，在床边。
    map.awakening_yi.rug.R07：地毯。
    map.awakening_yi.rug.R06：地毯。踩得发亮的地方，在床边。
    map.awakening_yi.slippers.R05：一双拖鞋。摆得很正，鞋头朝外。
