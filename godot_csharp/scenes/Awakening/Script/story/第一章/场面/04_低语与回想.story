场面 nudge_E1
    铜口：（远远地）你想知道自己为何在此，侦探先生。我也想知道，阁下会先问哪一句。 [#id=nudge_E1.001]
    放大 detective：你想知道自己为何在此。
    跳转 END

场面 nudge_E5
    铜口：（从墙里，很轻）苏小姐，此地还有你从未听过的声音。 [#id=nudge_E5.001]
    低语 su：此地还有你从未听过的声音。
    跳转 END

场面 nudge_E7
    铜口：（铜口里，声音放得很低）霍先生，超越卡诺循环的那一张，此地有人画过。画它的人，你见过。 [#id=nudge_E7.001]
    低语 huo：超越卡诺循环的那一张，此地有人画过。
    跳转 END

场面 nudge_E10
    铜口：（远远地）沈师傅，你护着的人，比你记得的多。 [#id=nudge_E10.001]
    低语 shen：你护着的人，比你记得的多。
    跳转 END

场面 recall_su
    （她闭上眼。很久。） [#id=recall_su.001]
    苏泠：门边……有人在喊开门。 [#id=recall_su.002]
    苏泠：我想开门，让他进来。 [#id=recall_su.003]
    （她睁开眼，脸色发白。） [#id=recall_su.004]
    苏泠：那个声音，我当时以为……是门外的人。 [#id=recall_su.005]
    跳转 END

场面 recall_huo
    （粉笔灰沾了他一手。） [#id=recall_huo.001]
    霍铎：铜口里的声音……很清楚。 [#id=recall_huo.002]
    霍铎：“等我下一次通知。”◎，铜口。他叫我守铜口。 [#id=recall_huo.003]
    霍铎：……他再也没有来。 [#id=recall_huo.004]
    跳转 END

场面 recall_shen
    （他的拇指摸过罐壁上一层层干结的面痕。） [#id=recall_shen.001]
    沈鼎：又该喂了。 [#id=recall_shen.002]
    沈鼎：阿泠的茶不放糖。老霍的放两勺。那位——（他看了你一眼）——清茶，不放奶。 [#id=recall_shen.003]
    沈鼎：……我叫她阿泠。我怎么会叫她阿泠。 [#id=recall_shen.004]
    跳转 END

场面 recall_offer_su
    苏泠：（她低头看着音叉）……刚才碰到它，脑子里闪了一下。握着它，再想想？ [#id=recall_offer_su.001]
    选择：
        - 提醒她：先问问铜口，那是什么。 [#id=recall_offer_su.002]
            设旗 show_rule
        - 什么都不说。 [#id=recall_offer_su.003]
    跳转 END

场面 recall_shown_su
    苏泠：（她朝墙里问）刚才那是什么？ [#id=recall_shown_su.001]
    铜口：是你封存之物。握久了，它会回来；回来的，你便带着了，连同它连着的此地。 [#id=recall_shown_su.003]
    苏泠：连着此地。……那就是说，回不去了。 [#id=recall_shown_su.002]
    跳转 END

场面 recall_touch_su
    苏泠：{{回想所思()}} [#id=recall_touch_su.001]
    （她把音叉握进掌心。） [#id=recall_touch_su.002]
    跳转 END

场面 recall_refuse_su
    苏泠：{{回想所思()}} [#id=recall_refuse_su.001]
    （她把音叉放回原处，手指在柄上停了一下。） [#id=recall_refuse_su.002]
    跳转 END

场面 recall_hesitate_su
    苏泠：{{回想所思()}} [#id=recall_hesitate_su.001]
    苏泠：……你说呢？ [#id=recall_hesitate_su.002]
    选择：
        - 劝她碰。 [#id=recall_hesitate_su.003]
            侦探：碰吧。我在边上。 [#id=recall_hesitate_su.004]
            设旗 advice_touch
        - 劝她别碰。 [#id=recall_hesitate_su.005]
            侦探：别往下想了。不知道那是什么，先别碰。 [#id=recall_hesitate_su.006]
            设旗 advice_decline
        - 由她自己定。 [#id=recall_hesitate_su.007]
            侦探：这是你的东西。你自己拿主意。 [#id=recall_hesitate_su.008]
            设旗 advice_own
    跳转 END

场面 recall_offer_huo
    霍铎：手一碰，闪了一下。……摸着它，往下想？ [#id=recall_offer_huo.001]
    选择：
        - 提醒他：先问问铜口，那是什么。 [#id=recall_offer_huo.002]
            设旗 show_rule
        - 什么都不说。 [#id=recall_offer_huo.003]
    跳转 END

场面 recall_shown_huo
    霍铎：（朝铜口）刚才那一下，是什么？ [#id=recall_shown_huo.001]
    铜口：是你封存之物。握久了，它会回来；回来的，你便带着了，连同它连着的此地。 [#id=recall_shown_huo.003]
    霍铎：……像拆机器。拆开看了，就装不回原样。 [#id=recall_shown_huo.002]
    跳转 END

场面 recall_touch_huo
    霍铎：{{回想所思()}} [#id=recall_touch_huo.001]
    若 告知规则("huo")：
        霍铎：代价我听见了。我自己担。 [#id=recall_touch_huo.002]
    （他把手掌平放在石板上。） [#id=recall_touch_huo.003]
    跳转 END

场面 recall_refuse_huo
    霍铎：{{回想所思()}} [#id=recall_refuse_huo.001]
    霍铎：不碰。 [#id=recall_refuse_huo.002]
    跳转 END

场面 recall_hesitate_huo
    霍铎：{{回想所思()}} [#id=recall_hesitate_huo.001]
    霍铎：……你说呢？ [#id=recall_hesitate_huo.002]
    选择：
        - 劝他碰。 [#id=recall_hesitate_huo.003]
            侦探：碰吧。我在边上。 [#id=recall_hesitate_huo.004]
            设旗 advice_touch
        - 劝他别碰。 [#id=recall_hesitate_huo.005]
            侦探：别往下想了。不知道那是什么，先别碰。 [#id=recall_hesitate_huo.006]
            设旗 advice_decline
        - 由他自己定。 [#id=recall_hesitate_huo.007]
            侦探：这是你的东西。你自己拿主意。 [#id=recall_hesitate_huo.008]
            设旗 advice_own
    跳转 END

场面 recall_offer_shen
    沈鼎：端着它，脑子里闪过点什么。……我这辈子摸过的罐子比你见过的人多，没一个这样。 [#id=recall_offer_shen.001]
    选择：
        - 提醒他：先问问铜口，那是什么。 [#id=recall_offer_shen.002]
            设旗 show_rule
        - 什么都不说。 [#id=recall_offer_shen.003]
    跳转 END

场面 recall_shown_shen
    沈鼎：（冲墙上的铜口）喂，刚才那是什么？ [#id=recall_shown_shen.001]
    铜口：是你封存之物。握久了，它会回来；回来的，你便带着了，连同它连着的此地。 [#id=recall_shown_shen.003]
    沈鼎：带着此地。……这种买卖，跑江湖的都认得。 [#id=recall_shown_shen.002]
    跳转 END

场面 recall_touch_shen
    沈鼎：{{回想所思()}} [#id=recall_touch_shen.001]
    （他两只手捧起罐子。） [#id=recall_touch_shen.002]
    跳转 END

场面 recall_refuse_shen
    沈鼎：{{回想所思()}} [#id=recall_refuse_shen.001]
    沈鼎：罐子我照样喂。回想就免了。 [#id=recall_refuse_shen.002]
    跳转 END

场面 recall_hesitate_shen
    沈鼎：{{回想所思()}} [#id=recall_hesitate_shen.001]
    沈鼎：罐子是我的。可这事……你说呢？ [#id=recall_hesitate_shen.002]
    选择：
        - 劝他碰。 [#id=recall_hesitate_shen.003]
            侦探：碰吧。我在边上。 [#id=recall_hesitate_shen.004]
            设旗 advice_touch
        - 劝他别碰。 [#id=recall_hesitate_shen.005]
            侦探：别往下想了。不知道那是什么，先别碰。 [#id=recall_hesitate_shen.006]
            设旗 advice_decline
        - 由他自己定。 [#id=recall_hesitate_shen.007]
            侦探：这是你的东西。你自己拿主意。 [#id=recall_hesitate_shen.008]
            设旗 advice_own
    跳转 END

场面 recall_ask_detective
    （指腹压上铁闩的一瞬，有什么东西从很远的地方晃了一下。） [#id=recall_ask_detective.001]
    侦探（心声）：……握住它，往下想？ [#id=recall_ask_detective.002]
    选择：
        - 握住插闩，往下想。 [#id=recall_ask_detective.003]
            侦探：…… [#id=recall_ask_detective.004]
            设旗 recall_yes
        - 松手。我不碰没证明过的东西。 [#id=recall_ask_detective.005]
            侦探：不碰。我想知道的事，自己查。 [#id=recall_ask_detective.006]
            （铁闩又凉了下去。） [#id=recall_ask_detective.007]
    跳转 END

场面 recall_detective
    （他没有松手。有什么东西从很远的地方回来了。） [#id=recall_detective.001]
    侦探（心声）：霍铎说闩。我就闩了。……我信他。 [#id=recall_detective.002]
    侦探（心声）：一句转述，我就去闩了门。我凭的是什么？ [#id=recall_detective.003]
    跳转 END
