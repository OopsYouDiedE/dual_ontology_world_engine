晨间日常
    # 步骤：- 房间 | 目标 | 动作 | 说明 | 权重 | 游戏分钟 [| 允许阶段]
    封锁：乙区
    # 三人早醒，已经互相介绍并初查；侦探汇合前只查看与听辨，核验后才做工。
    人物阶段：
        # 调查时侦探由多步规划或玩家决策接管，不并行执行会合前的闲游。
        侦探：roam
        霍铎：roam、investigate
        沈鼎：roam、investigate
        苏泠：roam、investigate
    旁听：
        - foyer | 门外远处有几下脚步，随后停了。隔着横闩，听不出是谁，也辨不出去了哪里。
        - foyer | 门外有人低声交谈，字句被墙和管路吞掉了。你只能确定：这座生活翼里，还有别人在醒着。
    侦探：
        - R05 | self | check | 在醒来的房间核对随身物，只把眼前能确认的事记下来 | 2 | 8
        - hall | table | check | 到长桌旁观察众人的举动，把亲眼所见与猜测分开 | 2 | 14
        - foyer | door | listen | 到门边听辨声响，暂不把隔墙听见的片段当成整句话 | 1 | 12
    霍铎：
        - hall | tube | check | 检查铜口与管接头 | 2 | 10 | roam、investigate
        - hall | table | work | 在长桌检查配重与备用工具 | 1 | 12 | investigate
        - hall | table | check | 把试过的工具按尺寸排开，留出长桌上放石板的位置 | 1 | 15 | investigate
    沈鼎：
        - pantry | sink | check | 在水槽取水并清洗备用碗 | 1 | 8 | roam、investigate
        - pantry | prep | work | 揉面并续养酵种 | 2 | 12 | investigate
        - pantry | prep | rest | 将揉好的面盖住，等它醒发；手指上的面粉还没拍净 | 1 | 35 | investigate
    苏泠：
        - foyer | door | listen | 在门边听辨管路与门缝的声音 | 1 | 10 | roam、investigate
        - R08 | piano | work | 在房里轻试琴弦，辨听余音 | 2 | 12 | investigate
        - foyer | door | listen | 回到门边静听，等房里的余音散尽再辨方向；她没有碰地上的音叉 | 1 | 18 | investigate
    卢西安：
        - DormW | self | check | 在甲区核对自己的笔记与随身物 | 1 | 8
        - DormW | desk | work | 推敲诗句与言灵韵律 | 2 | 12
        - DormW | desk | listen | 念到句尾时放低声音，听别人说话有没有同样的停顿 | 1 | 17
    顾平章：
        - DormW | self | check | 在甲区核对拓片与随身物 | 1 | 8
        - DormW | desk | work | 整理铭文辨读笔记 | 2 | 12
        - DormW | desk | check | 将不能认定的字留白，把猜读与能看清的笔画分列 | 1 | 19
    雀儿：
        - DormW | self | check | 在甲区核对自己的袋口与物件 | 1 | 8
        - DormW | latch | work | 检查寝区门闩并整理修锁工具 | 2 | 10
        - DormW | latch | check | 收起修锁工具，示意旁人先看门闩的位置，再试着推门 | 1 | 13
    沃德：
        - DormW | self | check | 在甲区核对自己的纸张与随身物 | 1 | 8
        - DormW | desk | work | 整理条款并校对用词 | 2 | 12
        - DormW | desk | listen | 听见试门的动静，搁笔问刚才谁碰过门；没有回应前先空着名字 | 1 | 18
    赛尔玛：
        - DormS | self | check | 在丙区核对标本与观察笔记 | 1 | 8
        - DormS | desk | work | 记录标本状态并整理分类 | 2 | 12
        - DormS | desk | check | 把标本与旧笔记并排，只记能看见的变化，不急着给它定名 | 1 | 21
    阮清越：
        - DormS | self | check | 在丙区核对棋子与自己的记录 | 1 | 8
        - DormS | desk | work | 摆棋复核棋谱与眼前的落子 | 2 | 12
        - DormS | desk | check | 停在记不清的一手，将那枚棋子放到棋盘外，没有补成一局 | 1 | 16
    伊莲娜：
        - DormS | self | check | 在丙区查看脚踝与自己的护具 | 1 | 8
        - DormS | bedside | work | 坐着活动脚踝并记录可用舞步 | 2 | 10
        - DormS | bedside | rest | 脚踝发紧便停下来，拉平护具边缘，暂不站起试步 | 1 | 32
    莫兰：
        - DormS | self | check | 在丙区核对颜料与速写本 | 1 | 8
        - DormS | desk | work | 观察窗边色层并做速写 | 2 | 12
        - DormS | desk | check | 挪开颜料盒，让窗光照上纸边，对照刚才画出的两块颜色 | 1 | 18
    维罗妮卡：
        - DormL | self | check | 在丁区核对试剂与自己的器具 | 1 | 8
        - DormL | desk | work | 清洁器具并整理理化记录 | 2 | 12
        - DormL | desk | check | 封好试剂，把未经验证的解释划到页边，给复核留下一栏 | 1 | 20
    贝尔特朗：
        - DormL | self | check | 在丁区检查自己的装备与提灯 | 1 | 8
        - DormL | door | check | 巡查丁区门口与通路 | 2 | 10
        - DormL | door | listen | 收住脚步听门另一侧的动静，提灯留在手边，没有越过门去 | 1 | 15
    玛格丽特：
        - DormL | self | check | 在丁区核对自己的图纸与量具 | 1 | 8
        - DormL | desk | work | 复核建筑草图与尺寸 | 2 | 12
        - DormL | door | check | 走到门边比照草图，把摸不到的门外尺寸留成虚线 | 1 | 22
    茉拉：
        - DormL | self | check | 在丁区核对自己的药包与绷带 | 1 | 8
        - DormL | desk | work | 整理诊疗记录并清点敷料 | 2 | 12
        - DormL | desk | rest | 将可用的敷料另包好，搁在药包上层，给有人来求助时留用 | 1 | 26
