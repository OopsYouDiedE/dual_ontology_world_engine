# 第四部分 §7 S5b（用户裁定 2026-10-09）：始业日正午乙区还没答完、甲区已经出门时，雀儿挑开乙区的外闩。
# 门开了，题还在：连廊都关着，门外只有一座落灰的起居厅；雀儿与卢西安走进乙区，此后可以交谈。
# 两人只谈自己的手艺、自己那道题与此刻的处境，不替侦探推这一案。

场面 quer_opens
    （门外响起一串细碎的金属声，很轻，很快。停了一下，又响了一串。） [#id=quer_opens.001]
    （外侧的横闩“咔”地一声抬了起来。门被拉开一条缝，一只手扒住了门沿。） [#id=quer_opens.002]
    （门缝里探进来一个瘦小的姑娘，黑短发乱糟糟的，右眉上一道浅疤，手里捏着一根弯过的细铁丝。） [#id=quer_opens.003]
    雀儿：雀儿。甲区的。……等到晌午了，你们一直没出来，外头一直落灰。我不信那张纸，我信我这双手。 [#id=quer_opens.004]
    （她身后跟进来一个年轻人，浅栗色的卷发垂到领口，酒红短斗篷。他欠了欠身，脸上有点无奈。） [#id=quer_opens.005]
    卢西安：卢西安，写诗的。我拦过她——拦了一上午，韵脚都拦没了。 [#id=quer_opens.006]
    铜口：门开了。题还在。 [#id=quer_opens.007]
    铜口：廊都关着，外头只有一座落灰的厅。答案备好了，仍到铜口来。 [#id=quer_opens.008]
    雀儿：（朝铜口翻了个白眼）听见没，它也没说不许开。 [#id=quer_opens.009]
    侦探（心声）：门是开了。可昨夜是谁闩的、为什么闩，开一扇门，答不了。 [#id=quer_opens.010]
    跳转 END

场面 talk_quer
    若 旗("met_quer")：
        雀儿：又来了？ [#id=talk_quer.001]
    否则：
        设旗 met_quer
        雀儿：（蹲在门厅，正拿铁丝拨弄那道内闩）别站我背后。说吧。 [#id=talk_quer.002]
    跳转 talk_quer_menu

场面 talk_quer_menu
    选择：
        - 这扇门的闩，你看得出什么？ [#id=talk_quer_menu.001]
            雀儿：外头那道闩是从外头落的。落得准，手不重。我挑它用了四下，不是三下——吹牛了。 [#id=talk_quer_menu.002]
            雀儿：里头这道是插的，插得正。谁插的我看不出来，闩又不记名字。 [#id=talk_quer_menu.003]
            跳转 talk_quer_menu
        - 你们那道题，后来怎么样了？ [#id=talk_quer_menu.004]
            雀儿：答对了。沃德说柜锁是我开的，没错。他接着就说我偷了东西。 [#id=talk_quer_menu.005]
            雀儿：……我不记得我为什么会有那把钥匙。 [#id=talk_quer_menu.006]
            跳转 talk_quer_menu
        - 你怎么不等我们答完？ [#id=talk_quer_menu.007]
            雀儿：门里的人一上午不出声，门外一直落灰。换你，你等？ [#id=talk_quer_menu.008]
            雀儿：要骂就骂。骂完了，门还是开着的。 [#id=talk_quer_menu.009]
            跳转 talk_quer_menu
        - 没事了。 [#id=talk_quer_menu.010]
            跳转 END

场面 talk_lucian
    若 旗("met_lucian")：
        卢西安：嗯？ [#id=talk_lucian.001]
    否则：
        设旗 met_lucian
        卢西安：（他正对着门内侧贴的规程看）这印红，是导师的。 [#id=talk_lucian.002]
    跳转 talk_lucian_menu

场面 talk_lucian_menu
    选择：
        - 门上的规程。 [#id=talk_lucian_menu.001]
            卢西安：一条规矩，被她用四下铁丝挑开了。 [#id=talk_lucian_menu.002]
            卢西安：沃德会说她违了规；我会说，她是怕你们在里头出事。两句都是真的——这才麻烦。 [#id=talk_lucian_menu.003]
            跳转 talk_lucian_menu
        - 昨夜你记得什么吗？ [#id=talk_lucian_menu.004]
            卢西安：上船。然后一句诗念到一半，韵脚找不着了。……跟你们一样，后面是白的。 [#id=talk_lucian_menu.005]
            跳转 talk_lucian_menu
        - 没事了。 [#id=talk_lucian_menu.006]
            跳转 END

# 题答完时门已开着：不再有外闩抬起的演出，铜口只认下答案。
场面 s10_open_early
    铜口：……诸位答完了。 [#id=s10_open_early.001]
    若 钩数("su") > 0 或 知道("su", "侦探重演吻合")：
        苏泠：你昨夜没听我的。（她停了很久，才把目光从门上收回来）……你听对了。 [#id=s10_open_early.002]
    否则：
        苏泠：（她看着那扇早就开着的门）……外面，真的没有人。 [#id=s10_open_early.003]
    雀儿：（靠在门框上）答完了？那走吧。灰停了没停，我替你们看了一上午。 [#id=s10_open_early.004]
    卢西安：门是她开的，题是你们答的。两件事，别记混了。 [#id=s10_open_early.005]
    铜口：诸位请。 [#id=s10_open_early.006]
    跳转 END
