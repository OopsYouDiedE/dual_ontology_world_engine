续篇 chapter2

    # ====================================================================
    # 第二章的剧情推进（第五部分 §4）：原先写死在 Chapter2InteractionSession.cs 的开场、配给争执、夜里、案发日与议事，
    # 一律写在这里，C# 只执行。语法同第一章《阶段与流程》；新增的指令与触发见每段注释。
    #
    #   指令 scene:<地点别名>     镜头与侦探切到该地点（Living＝起居厅，Hall、Lecture、Logistics …）
    #   指令 corridor:<廊>:开|关  连廊开关（north＝中央厅与讲堂，east＝后勤院、粮仓、诊疗所，archive＝名录库与文献工房）；
    #                             关着的廊走不过去，人物也不往那里去
    #   指令 dorm_out:<寝区>      该区外闩抬起，居民做完手头的事出门（寝区案件的“最早出门”到点时由此执行）
    #   指令 run:<行动>           结算一段时间（stay、class、collect …），其余十五人照各自认知行动
    #   指令 field                交还自由行动
    #   时刻 第 N 日 <时段>：流程 X   时钟到这一时段的开头时执行一次
    # ====================================================================

    流程 始业·门开
        # 第一章门开的那一刻接着往下演；此时只有生活翼可去。
        指令 corridor:north:关
        指令 corridor:east:关
        指令 corridor:archive:关
        指令 scene:Living
        演出 arrival
        指令 field

    流程 始业·甲区
        演出 meet_jia
        指令 field

    流程 始业·晚饭
        # 后勤院去不了；各区吃自家备餐间的存粮，不动配给台。
        指令 scene:DormLife
        演出 supper
        指令 run:eat_pantry
        指令 field

    流程 始业·入夜
        演出 first_night
        指令 field

    流程 第二日·丙区
        指令 dorm_out:丙区
        指令 scene:Living
        演出 meet_bing
        指令 field

    流程 第二日·北廊与首课
        指令 corridor:north:开
        指令 scene:Hall
        演出 gather
        指令 scene:Lecture
        演出 lesson
        指令 run:class
        演出 free
        指令 field

    流程 第二日·东廊与配给
        指令 corridor:east:开
        指令 dorm_out:丁区
        演出 meet_ding
        指令 scene:Logistics
        演出 noon_dispute
        # 选了调停写法，才逐人判定并演 mediate.accept/reject.<人>；被拒不另占时段，只结算这一顿饭。
        指令 run:collect
        指令 field

    流程 第二日·库廊
        指令 corridor:archive:开
        演出 open_archive_wing
        指令 field

    流程 夜里·T
        # 夜里按 T：问一句，就地睡到天亮；守夜只在守夜点。任何一夜都适用，不只始业日。
        选择：
            - 睡到天亮 [#id=chapter2.flow_sleep.001]
                指令 run:sleep
            - 再等等 [#id=chapter2.flow_sleep.002]
                结束

    流程 案发·清晨
        演出 dawn
        指令 scene:Hall
        指令 run:stay
        若 非 有人被指控：
            演出 no_accuse
        演出 after
        指令 field

    流程 对账
        演出 evening
        指令 confront

    流程 议事
        指令 council

    时刻：
        - 第 2 日 早晨：流程 始业·门开
        - 第 2 日 上午：流程 始业·甲区
        - 第 2 日 中午：流程 始业·晚饭
        - 第 2 日 夜里：流程 始业·入夜
        - 第 3 日 早晨：流程 第二日·丙区
        - 第 3 日 上午：流程 第二日·北廊与首课
        - 第 3 日 中午：流程 第二日·东廊与配给
        - 第 3 日 下午：流程 第二日·库廊
        - 第 5 日 早晨：流程 案发·清晨
        - 第 5 日 傍晚：流程 对账
        - 第 6 日 早晨：流程 议事
