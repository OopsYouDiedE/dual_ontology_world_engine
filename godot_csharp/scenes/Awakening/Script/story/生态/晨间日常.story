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
        - foyer | 门外远处有几下脚步，随后停了。隔着横闩，听不出是谁，也听不出往哪儿去了。
        - foyer | 门外有人低声交谈，字句被墙和管路吞掉了。看来醒着的，不止这屋里几个人。
    侦探：
        - R05 | self | check | 翻了翻风衣口袋，东西都在 | 2 | 8
        - hall | table | check | 在长桌边站了一会儿，看其他人各忙各的 | 2 | 14
        - foyer | door | listen | 走到门边，侧耳听了听 | 1 | 12
    霍铎：
        - hall | tube | check | 凑近铜口，拿指甲敲了敲管接头 | 2 | 10 | roam、investigate
        - hall | table | work | 把长桌上的工具一件件拿起来看 | 1 | 12 | investigate
        - hall | table | check | 把工具按大小摆成一排，空出放石板的地方 | 1 | 15 | investigate
    沈鼎：
        - pantry | sink | check | 在水槽边接水，顺手把碗冲了 | 1 | 8 | roam、investigate
        - pantry | prep | work | 揉面，又给酵种罐添了一勺面 | 2 | 12 | investigate
        - pantry | prep | rest | 把面团盖上布醒着，手上的面粉还没拍干净 | 1 | 35 | investigate
    苏泠：
        - foyer | door | listen | 靠在门边，听墙里那点嗡嗡声 | 1 | 10 | roam、investigate
        - R08 | piano | work | 回房拨了拨琴弦 | 2 | 12 | investigate
        - foyer | door | listen | 又回到门边，等琴声散了再听；地上的音叉她没碰 | 1 | 18 | investigate
    卢西安：
        - DormW | self | check | 翻了翻桌上的笔记，是自己的字 | 1 | 8
        - DormW | desk | work | 对着纸念一句诗，改了两个字 | 2 | 12
        - DormW | desk | listen | 念到句尾故意压低声音，听屋里有没有人接话 | 1 | 17
    顾平章：
        - DormW | self | check | 把枕边的拓片一张张摊开 | 1 | 8
        - DormW | desk | work | 戴上老花镜，对着铭文一个字一个字认 | 2 | 12
        - DormW | desk | check | 认不出的字，空着不填 | 1 | 19
    雀儿：
        - DormW | self | check | 摸了摸袋口，结还是自己打的 | 1 | 8
        - DormW | latch | work | 蹲在门闩边，把修锁的小工具摆了一地 | 2 | 10
        - DormW | latch | check | 收起工具，推了推门 | 1 | 13
    沃德：
        - DormW | self | check | 把桌上的纸理齐，一张张看过 | 1 | 8
        - DormW | desk | work | 提笔改条款里的一个词 | 2 | 12
        - DormW | desk | listen | 听见有人试门，抬头问了一句是谁 | 1 | 18
    赛尔玛：
        - DormS | self | check | 看了看窗台上那盆苗 | 1 | 8
        - DormS | desk | work | 翻开笔记，记下叶子今天的样子 | 2 | 12
        - DormS | desk | check | 把苗和旧笔记上的画摆在一起比了比 | 1 | 21
    阮清越：
        - DormS | self | check | 把黑子放回棋罐，数了数 | 1 | 8
        - DormS | desk | work | 照着记录把棋局重新摆了一遍 | 2 | 12
        - DormS | desk | check | 摆到想不起来的那一手，捏着棋子停住了 | 1 | 16
    伊莲娜：
        - DormS | self | check | 解开绷带看了看脚踝 | 1 | 8
        - DormS | bedside | work | 坐在床边慢慢转动脚踝 | 2 | 10
        - DormS | bedside | rest | 脚踝一紧就停下，把绷带重新缠好 | 1 | 32
    莫兰：
        - DormS | self | check | 摊开速写本，颜料盒还剩大半 | 1 | 8
        - DormS | desk | work | 对着窗光画了几笔 | 2 | 12
        - DormS | desk | check | 挪开颜料盒，把刚画的两块颜色对着光看 | 1 | 18
    维罗妮卡：
        - DormL | self | check | 一瓶瓶看过试剂的瓶签 | 1 | 8
        - DormL | desk | work | 把器具擦干净，按顺序放回架上 | 2 | 12
        - DormL | desk | check | 把瓶塞一个个压紧，在本子上记了一笔 | 1 | 20
    贝尔特朗：
        - DormL | self | check | 擦了擦提灯的玻璃罩 | 1 | 8
        - DormL | door | check | 在丁区门口来回走了两趟 | 2 | 10
        - DormL | door | listen | 停下脚步听门外的动静，提灯一直拎在手上 | 1 | 15
    玛格丽特：
        - DormL | self | check | 摊开图纸，压上卷尺 | 1 | 8
        - DormL | desk | work | 对着草图量了量墙 | 2 | 12
        - DormL | door | check | 走到门边比了比图纸，门外那一截画成虚线 | 1 | 22
    茉拉：
        - DormL | self | check | 打开药包数了数绷带 | 1 | 8
        - DormL | desk | work | 把敷料按大小分好 | 2 | 12
        - DormL | desk | rest | 另包了一小包敷料，放在药包最上面，有人来要时好拿 | 1 | 26
