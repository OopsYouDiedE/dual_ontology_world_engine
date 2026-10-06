场面 sound_test
    （你们各自站好。活动厅的铜口沉默了一会儿。） [#id=sound_test.001]
    铜口：（乐意之至。）{{声路句子()}} [#id=sound_test.002]
    侦探（心声）：（{{所闻之处("detective")}}）{{所闻("detective")}} [#id=sound_test.003]
    霍铎：（{{所闻之处("huo")}}）{{所闻("huo")}} [#id=sound_test.004]
    沈鼎：（{{所闻之处("shen")}}）{{所闻("shen")}} [#id=sound_test.005]
    苏泠：（{{所闻之处("su")}}）{{所闻("su")}} [#id=sound_test.006]
    苏泠：声音走管子，和人站在面前说话，不是一种声音。我只说我听见的，和它从哪边来。 [#id=sound_test.007]
    跳转 END

场面 talk_huo
    若 知道("huo", "侦探重演吻合")：
        霍铎：你推完了。……我在这儿守了很久，是不是。 [#id=talk_huo.001]
    否则 若 知道("huo", "侦探在重演昨夜")：
        霍铎：你在推昨晚。推出什么，跟我说。 [#id=talk_huo.002]
    否则 若 旗("met_huo")：
        霍铎：嗯。 [#id=talk_huo.003]
    否则：
        设旗 met_huo
        霍铎：……什么事。 [#id=talk_huo.004]
    跳转 talk_huo_menu

场面 talk_huo_menu
    霍铎：你问。 [#id=talk_huo_menu.001]
    选择：
        - 请到 R-05 书架旁，单独比一下手掌轮廓。 [#id=talk_huo_menu.002]
            条件：见过("E11")
            设旗 invite_hand_huo
            跳转 END
        - 这个铜口是什么？ [#id=talk_huo_menu.003]
            霍铎：传声口。主楼那边说话，这边听。图纸上这么画。 [#id=talk_huo_menu.004]
            霍铎：管子往门厅走。我看过墙。 [#id=talk_huo_menu.005]
            跳转 talk_huo_menu
        - 那根铜管的接头。 [#id=talk_huo_menu.006]
            条件：见过("E8")
            霍铎：两个接头都松了。一个在门框边，一个在 R-08 墙上。螺纹滑过。 [#id=talk_huo_menu.007]
            霍铎：管子里说话，松的地方会漏。漏出来多少，漏出来什么——得试。 [#id=talk_huo_menu.008]
            跳转 talk_huo_menu
        - 那扇门是怎么关的？ [#id=talk_huo_menu.009]
            条件：见过("E1")
            霍铎：配重。门往外开，松手，配重拉它回来，自己合上。 [#id=talk_huo_menu.010]
            霍铎：不撑东西，开不住。 [#id=talk_huo_menu.011]
            跳转 talk_huo_menu
        - 石板上的字。 [#id=talk_huo_menu.012]
            条件：见过("E7")
            若 问过("E7")：
                霍铎：是我的字。我说了。……那个小图，我认不出。 [#id=talk_huo_menu.013]
            否则：
                霍铎：石板？在长桌上？我没注意。你拿给我看。 [#id=talk_huo_menu.014]
            跳转 talk_huo_menu
        - 门是我闩的。 [#id=talk_huo_menu.015]
            条件：旗("reversal_one")
            霍铎：……你闩的。 [#id=talk_huo_menu.016]
            霍铎：闩门，不难。手一推。难的是推之前那一下。 [#id=talk_huo_menu.017]
            霍铎：你那一下，听的是谁，我不知道。 [#id=talk_huo_menu.018]
            跳转 talk_huo_menu
        - ◎ 是什么？ [#id=talk_huo_menu.019]
            条件：回想过("huo")
            霍铎：铜口。图纸上，传声口画这个。我早该认出来。 [#id=talk_huo_menu.020]
            霍铎：他叫我守。我就守。守到天亮，没有下一次。 [#id=talk_huo_menu.021]
            跳转 talk_huo_menu
        - 我们在这里很久了。 [#id=talk_huo_menu.022]
            条件：把握("detective", "早已相熟") >= 75
            回应话题 早已相熟：huo
            跳转 talk_huo_menu
        - 昨晚你在哪？ [#id=talk_huo_menu.026]
            霍铎：不记得。只记得上船。 [#id=talk_huo_menu.027]
            跳转 talk_huo_menu
        - 没事了。 [#id=talk_huo_menu.028]
            跳转 END

场面 talk_shen
    若 知道("shen", "侦探重演吻合")：
        沈鼎：推明白了？脸色比早上好。……坐，喝口茶。 [#id=talk_shen.001]
    否则 若 知道("shen", "侦探在重演昨夜")：
        沈鼎：在推昨晚的事？推到我那块布了没有？ [#id=talk_shen.002]
    否则 若 旗("met_shen")：
        沈鼎：又来了？饿了说一声。 [#id=talk_shen.003]
    否则：
        设旗 met_shen
        沈鼎：来得正好。水够，粮够，够我们吃半个月。这是头一件事。 [#id=talk_shen.004]
    跳转 talk_shen_menu

场面 talk_shen_menu
    沈鼎：想问什么？ [#id=talk_shen_menu.001]
    选择：
        - 请到 R-05 书架旁，单独比一下手掌轮廓。 [#id=talk_shen_menu.002]
            条件：见过("E11")
            设旗 invite_hand_shen
            跳转 END
        - 你泡茶的时候，怎么知道她不加糖？ [#id=talk_shen_menu.003]
            沈鼎：顺手。真是顺手。手比脑子快，跟颠勺一样。 [#id=talk_shen_menu.004]
            沈鼎：别看我。我也纳闷。 [#id=talk_shen_menu.005]
            跳转 talk_shen_menu
        - 门底那条湿布。 [#id=talk_shen_menu.006]
            条件：见过("E4")
            沈鼎：堵烟缝，湿布最好。船上一着火，先堵门缝，再说别的。 [#id=talk_shen_menu.007]
            若 问过("E4")：
                沈鼎：那是我拧的。我认。可我不记得拧过。 [#id=talk_shen_menu.008]
            跳转 talk_shen_menu
        - 架上那罐酵种。 [#id=talk_shen_menu.009]
            条件：见过("E10")
            沈鼎：活的。有人天天喂它。 [#id=talk_shen_menu.010]
            沈鼎：这岛上除了我，谁会天天喂酵种？ [#id=talk_shen_menu.011]
            跳转 talk_shen_menu
        - 水槽里的杯子。 [#id=talk_shen_menu.012]
            条件：见过("E9")
            沈鼎：四只，四个样。用过，没洗。 [#id=talk_shen_menu.013]
            沈鼎：我要是在，不会让杯子过夜。……除非那晚顾不上。 [#id=talk_shen_menu.014]
            跳转 talk_shen_menu
        - 门是我闩的。 [#id=talk_shen_menu.015]
            条件：旗("reversal_one")
            沈鼎：你闩的？那你闩之前，门是开着的。 [#id=talk_shen_menu.016]
            沈鼎：跟你说个行里的门道：门缝开着，湿布是往缝里塞的；门关死了，才顺着门底压一条。 [#id=talk_shen_menu.017]
            沈鼎：我那块布，是顺着压的。 [#id=talk_shen_menu.018]
            跳转 talk_shen_menu
        - 铜口那边的声音。 [#id=talk_shen_menu.019]
            条件：试过("E8")
            沈鼎：我在备餐间只听见嗡嗡的，像隔壁有人念经。 [#id=talk_shen_menu.020]
            沈鼎：水一开，连嗡嗡都没了。灶台前的人，耳朵是聋的。 [#id=talk_shen_menu.021]
            跳转 talk_shen_menu
        - 罐壁上那些面痕。 [#id=talk_shen_menu.022]
            条件：把握("detective", "早已相熟") >= 75
            回应话题 早已相熟：shen
            跳转 talk_shen_menu
        - 昨晚你在哪？ [#id=talk_shen_menu.026]
            沈鼎：不记得。 [#id=talk_shen_menu.027]
            沈鼎：可我这两条胳膊，早上起来是酸的。像揉了半宿的面。 [#id=talk_shen_menu.028]
            跳转 talk_shen_menu
        - 没事了。 [#id=talk_shen_menu.029]
            沈鼎：去吧。饭点叫你。 [#id=talk_shen_menu.030]
            跳转 END

场面 talk_su
    若 知道("su", "侦探重演吻合")：
        苏泠：……你都推出来了。我听得出来，你走路的声音变了。 [#id=talk_su.001]
    否则 若 知道("su", "侦探在重演昨夜")：
        苏泠：你在推昨晚。……推到我开门那里了吗。 [#id=talk_su.002]
    否则 若 旗("met_su")：
        苏泠：……嗯。 [#id=talk_su.003]
    否则：
        设旗 met_su
        苏泠：（她靠在门厅的墙边，侧着头，像在听什么） [#id=talk_su.004]
        苏泠：墙里有声音。很低，一直在。 [#id=talk_su.005]
    跳转 talk_su_menu

场面 talk_su_menu
    苏泠：你要问什么。 [#id=talk_su_menu.001]
    选择：
        - 请到 R-05 书架旁，单独比一下手掌轮廓。 [#id=talk_su_menu.002]
            条件：见过("E11")
            设旗 invite_hand_su
            跳转 END
        - 墙里的声音？ [#id=talk_su_menu.003]
            苏泠：像风吹过瓶口。又不是风。 [#id=talk_su_menu.004]
            苏泠：门那边没有声音。我早上贴着门听过。一点都没有。 [#id=talk_su_menu.005]
            跳转 talk_su_menu
        - 那把音叉。 [#id=talk_su_menu.006]
            条件：见过("E5")
            苏泠：是我的。我走到哪儿都带着。 [#id=talk_su_menu.007]
            苏泠：敲一下，听回声，就知道墙后面是实的还是空的。 [#id=talk_su_menu.008]
            跳转 talk_su_menu
        - 你的乐谱。 [#id=talk_su_menu.009]
            条件：见过("E6")
            苏泠：我记下的，就是我听见的。一个音都不会多。 [#id=talk_su_menu.010]
            苏泠：我不知道我什么时候记的。……但那是我的手。 [#id=talk_su_menu.011]
            跳转 talk_su_menu
        - 那扇门。 [#id=talk_su_menu.012]
            条件：见过("E1")
            苏泠：门后面很静。 [#id=talk_su_menu.013]
            若 旗("reversal_one")：
                苏泠：（她看着你的靴子，没有再说下去） [#id=talk_su_menu.014]
            跳转 talk_su_menu
        - 门是我闩的。 [#id=talk_su_menu.015]
            条件：旗("reversal_one")
            苏泠：我知道。你的靴子上有灰。 [#id=talk_su_menu.016]
            苏泠：我没有怪你。 [#id=talk_su_menu.017]
            苏泠：我只是想知道，门外到底有没有人。 [#id=talk_su_menu.018]
            跳转 talk_su_menu
        - 铜口试过的那句话。 [#id=talk_su_menu.019]
            条件：试过("E8")
            苏泠：门边那几个字，是从墙里出来的。现在我分得出。 [#id=talk_su_menu.020]
            苏泠：可要是半夜，铃在响，我刚放下琴……我不知道我分没分。 [#id=talk_su_menu.021]
            跳转 talk_su_menu
        - 我们早就认识。 [#id=talk_su_menu.022]
            条件：把握("detective", "早已相熟") >= 75
            回应话题 早已相熟：su
            跳转 talk_su_menu
        - 昨晚你在哪？ [#id=talk_su_menu.025]
            苏泠：不记得。 [#id=talk_su_menu.026]
            苏泠：……别人问我，我也是这一句。 [#id=talk_su_menu.027]
            跳转 talk_su_menu
        - 没事了。 [#id=talk_su_menu.028]
            跳转 END
