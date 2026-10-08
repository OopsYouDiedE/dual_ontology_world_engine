续篇 chapter2

    选项 lesson_precise
        标签：它没写哪里算室内。
        仅选择：是

    选项 lesson_who
        标签：它没写对谁说的。
        仅选择：是

    选项 lesson_where
        标签：它没写为什么。
        仅选择：是

    选项 mediate
        标签：出面调停（要花上这一阵）
        仅选择：是

    选项 skip_dispute
        标签：不插手，排队领饭
        仅选择：是

    回应 huo_testimony_close
        角色：霍铎
        意图：作证
        条件：侦探 信任 ≥ 2
        霍铎：你问。能答的，都答。 [#id=chapter2.huo_testimony_close.001] [#emotion=warm]

    回应 huo_testimony
        角色：霍铎
        意图：作证
        条件：侦探 信任 = 1
        霍铎：嗯。问。 [#id=chapter2.huo_testimony.002]

    回应 huo_testimony_guarded
        角色：霍铎
        意图：作证
        条件：侦探 信任 = 0
        霍铎：……问这个做什么。 [#id=chapter2.huo_testimony_guarded.001]

    回应 huo_decline
        角色：霍铎
        意图：拒绝
        条件：侦探 信任 < 0
        霍铎：不谈。那一页，不动。 [#id=chapter2.huo_decline.002]

    回应 shen_testimony_close
        角色：沈鼎
        意图：作证
        条件：侦探 信任 ≥ 2
        沈鼎：来，坐下说，锅里还热着。我晓得的，都讲给你听。 [#id=chapter2.shen_testimony_close.001] [#emotion=warm]

    回应 shen_testimony
        角色：沈鼎
        意图：作证
        条件：侦探 信任 = 1
        沈鼎：你问，我想想。 [#id=chapter2.shen_testimony.002]

    回应 shen_testimony_guarded
        角色：沈鼎
        意图：作证
        条件：侦探 信任 = 0
        沈鼎：你打听这个干嘛？……算了，问吧。 [#id=chapter2.shen_testimony_guarded.001]

    回应 shen_decline
        角色：沈鼎
        意图：拒绝
        条件：侦探 信任 < 0
        沈鼎：这事我不讲。谁拦着人吃饭，我就不帮他添柴。 [#id=chapter2.shen_decline.002]

    回应 su_testimony_close
        角色：苏泠
        意图：作证
        条件：侦探 信任 ≥ 2
        苏泠：你问吧。我听到的，都告诉你。 [#id=chapter2.su_testimony_close.001] [#emotion=warm]

    回应 su_testimony
        角色：苏泠
        意图：作证
        条件：侦探 信任 = 1
        苏泠：你问。 [#id=chapter2.su_testimony.002]

    回应 su_testimony_guarded
        角色：苏泠
        意图：作证
        条件：侦探 信任 = 0
        苏泠：……你要听哪一段？ [#id=chapter2.su_testimony_guarded.001]

    回应 su_decline
        角色：苏泠
        意图：拒绝
        条件：侦探 信任 < 0
        苏泠：不说。话只冲着一个人去，我不会替它出声。 [#id=chapter2.su_decline.002]

    回应 lucian_testimony_close
        角色：卢西安
        意图：作证
        条件：侦探 信任 ≥ 2
        卢西安：亲爱的侦探，请讲——我所见的，一样也不藏。 [#id=chapter2.lucian_testimony_close.001] [#emotion=warm]

    回应 lucian_testimony
        角色：卢西安
        意图：作证
        条件：侦探 信任 = 1
        卢西安：请问吧。 [#id=chapter2.lucian_testimony.002]

    回应 lucian_testimony_guarded
        角色：卢西安
        意图：作证
        条件：侦探 信任 = 0
        卢西安：是谁让你来问的？ [#id=chapter2.lucian_testimony_guarded.001]

    回应 lucian_decline
        角色：卢西安
        意图：拒绝
        条件：侦探 信任 < 0
        卢西安：恕我不答。把规矩磨成刀，去割弱者，我不奉陪。 [#id=chapter2.lucian_decline.002]

    回应 gu_testimony_close
        角色：顾平章
        意图：作证
        条件：侦探 信任 ≥ 2
        顾平章：老朽所知，尽可相告。请坐，慢慢说。 [#id=chapter2.gu_testimony_close.001] [#emotion=warm]

    回应 gu_testimony
        角色：顾平章
        意图：作证
        条件：侦探 信任 = 1
        顾平章：你问吧，老朽听着。 [#id=chapter2.gu_testimony.002]

    回应 gu_testimony_guarded
        角色：顾平章
        意图：作证
        条件：侦探 信任 = 0
        顾平章：问这个，所为何事？ [#id=chapter2.gu_testimony_guarded.001]

    回应 gu_decline
        角色：顾平章
        意图：拒绝
        条件：侦探 信任 < 0
        顾平章：不可说。忌讳之事，岂能轻动。 [#id=chapter2.gu_decline.002]

    回应 quer_testimony_close
        角色：雀儿
        意图：作证
        条件：侦探 信任 ≥ 2
        雀儿：行，你问。别绕，我也不绕，看到啥说啥。 [#id=chapter2.quer_testimony_close.001] [#emotion=warm]

    回应 quer_testimony
        角色：雀儿
        意图：作证
        条件：侦探 信任 = 1
        雀儿：问吧。 [#id=chapter2.quer_testimony.002]

    回应 quer_testimony_guarded
        角色：雀儿
        意图：作证
        条件：侦探 信任 = 0
        雀儿：问这个干嘛？ [#id=chapter2.quer_testimony_guarded.001]

    回应 quer_decline
        角色：雀儿
        意图：拒绝
        条件：侦探 信任 < 0
        雀儿：没门。你们那套抓人的规矩，我不陪。 [#id=chapter2.quer_decline.002]

    回应 ward_testimony_close
        角色：沃德
        意图：作证
        条件：侦探 信任 ≥ 2
        沃德：可以。你问，我照记录答。 [#id=chapter2.ward_testimony_close.001] [#emotion=warm]

    回应 ward_testimony
        角色：沃德
        意图：作证
        条件：侦探 信任 = 1
        沃德：请问。 [#id=chapter2.ward_testimony.002]

    回应 ward_testimony_guarded
        角色：沃德
        意图：作证
        条件：侦探 信任 = 0
        沃德：你先说明来意。 [#id=chapter2.ward_testimony_guarded.001]

    回应 ward_decline
        角色：沃德
        意图：拒绝
        条件：侦探 信任 < 0
        沃德：不予答复。没有记录，我不作任何陈述。 [#id=chapter2.ward_decline.002]

    回应 selma_testimony_close
        角色：赛尔玛
        意图：作证
        条件：侦探 信任 ≥ 2
        赛尔玛：问吧，我这就说，像在林子里给人报路。 [#id=chapter2.selma_testimony_close.001] [#emotion=warm]

    回应 selma_testimony
        角色：赛尔玛
        意图：作证
        条件：侦探 信任 = 1
        赛尔玛：说吧，想知道什么？ [#id=chapter2.selma_testimony.002]

    回应 selma_testimony_guarded
        角色：赛尔玛
        意图：作证
        条件：侦探 信任 = 0
        赛尔玛：你问这个干什么？ [#id=chapter2.selma_testimony_guarded.001]

    回应 selma_decline
        角色：赛尔玛
        意图：拒绝
        条件：侦探 信任 < 0
        赛尔玛：不说。要我填一堆表才能开口，那就免了。 [#id=chapter2.selma_decline.002]

    回应 ruan_testimony_close
        角色：阮清越
        意图：作证
        条件：侦探 信任 ≥ 2
        阮清越：问吧，我全告诉你。 [#id=chapter2.ruan_testimony_close.001] [#emotion=warm]

    回应 ruan_testimony
        角色：阮清越
        意图：作证
        条件：侦探 信任 = 1
        阮清越：问。 [#id=chapter2.ruan_testimony.002]

    回应 ruan_testimony_guarded
        角色：阮清越
        意图：作证
        条件：侦探 信任 = 0
        阮清越：为什么问？……说吧。 [#id=chapter2.ruan_testimony_guarded.001]

    回应 ruan_decline
        角色：阮清越
        意图：拒绝
        条件：侦探 信任 < 0
        阮清越：不说。没有记录的，算不清。 [#id=chapter2.ruan_decline.002]

    回应 elena_testimony_close
        角色：伊莲娜
        意图：作证
        条件：侦探 信任 ≥ 2
        伊莲娜：请讲。我知道的，都告诉你。 [#id=chapter2.elena_testimony_close.001] [#emotion=warm]

    回应 elena_testimony
        角色：伊莲娜
        意图：作证
        条件：侦探 信任 = 1
        伊莲娜：嗯，你问吧。 [#id=chapter2.elena_testimony.002]

    回应 elena_testimony_guarded
        角色：伊莲娜
        意图：作证
        条件：侦探 信任 = 0
        伊莲娜：……你为什么想知道？ [#id=chapter2.elena_testimony_guarded.001]

    回应 elena_decline
        角色：伊莲娜
        意图：拒绝
        条件：侦探 信任 < 0
        伊莲娜：恕我不奉告。把人关在门外的话，我不说。 [#id=chapter2.elena_decline.002]

    回应 moran_testimony_close
        角色：莫兰
        意图：作证
        条件：侦探 信任 ≥ 2
        莫兰：……你问。我看到的，都是真的颜色。 [#id=chapter2.moran_testimony_close.001] [#emotion=warm]

    回应 moran_testimony
        角色：莫兰
        意图：作证
        条件：侦探 信任 = 1
        莫兰：……问吧。我记得的颜色，说给你。 [#id=chapter2.moran_testimony.002]

    回应 moran_testimony_guarded
        角色：莫兰
        意图：作证
        条件：侦探 信任 = 0
        莫兰：……为什么问。 [#id=chapter2.moran_testimony_guarded.001]

    回应 moran_decline
        角色：莫兰
        意图：拒绝
        条件：侦探 信任 < 0
        莫兰：……不说。……那里，是黑的。 [#id=chapter2.moran_decline.002]

    回应 veronica_testimony_close
        角色：维罗妮卡
        意图：作证
        条件：侦探 信任 ≥ 2
        维罗妮卡：可以。你问，我按记录说。 [#id=chapter2.veronica_testimony_close.001] [#emotion=warm]

    回应 veronica_testimony
        角色：维罗妮卡
        意图：作证
        条件：侦探 信任 = 1
        维罗妮卡：你说。 [#id=chapter2.veronica_testimony.002]

    回应 veronica_testimony_guarded
        角色：维罗妮卡
        意图：作证
        条件：侦探 信任 = 0
        维罗妮卡：你要查的是哪一项？ [#id=chapter2.veronica_testimony_guarded.001]

    回应 veronica_decline
        角色：维罗妮卡
        意图：拒绝
        条件：侦探 信任 < 0
        维罗妮卡：不行。我核实不了的事，不往外传。 [#id=chapter2.veronica_decline.002]

    回应 bertrand_testimony_close
        角色：贝尔特朗
        意图：作证
        条件：侦探 信任 ≥ 2
        贝尔特朗：报告：愿意配合。你问。 [#id=chapter2.bertrand_testimony_close.001] [#emotion=warm]

    回应 bertrand_testimony
        角色：贝尔特朗
        意图：作证
        条件：侦探 信任 = 1
        贝尔特朗：请讲。 [#id=chapter2.bertrand_testimony.002]

    回应 bertrand_testimony_guarded
        角色：贝尔特朗
        意图：作证
        条件：侦探 信任 = 0
        贝尔特朗：你先说为什么问。 [#id=chapter2.bertrand_testimony_guarded.001]

    回应 bertrand_decline
        角色：贝尔特朗
        意图：拒绝
        条件：侦探 信任 < 0
        贝尔特朗：拒绝。我不替政客开口。 [#id=chapter2.bertrand_decline.002]

    回应 margaret_testimony_close
        角色：玛格丽特
        意图：作证
        条件：侦探 信任 ≥ 2
        玛格丽特：可以。你问，我把受力点一项项指给你。 [#id=chapter2.margaret_testimony_close.001] [#emotion=warm]

    回应 margaret_testimony
        角色：玛格丽特
        意图：作证
        条件：侦探 信任 = 1
        玛格丽特：你问，我听着。 [#id=chapter2.margaret_testimony.002]

    回应 margaret_testimony_guarded
        角色：玛格丽特
        意图：作证
        条件：侦探 信任 = 0
        玛格丽特：你要查哪一处？ [#id=chapter2.margaret_testimony_guarded.001]

    回应 margaret_decline
        角色：玛格丽特
        意图：拒绝
        条件：侦探 信任 < 0
        玛格丽特：不说。结构没验之前，我不下结论，也不给你结论的材料。 [#id=chapter2.margaret_decline.002]

    回应 mora_testimony_close
        角色：茉拉
        意图：作证
        条件：侦探 信任 ≥ 2
        茉拉：问，快点。我知道的都告诉你，别耽误病人。 [#id=chapter2.mora_testimony_close.001] [#emotion=warm]

    回应 mora_testimony
        角色：茉拉
        意图：作证
        条件：侦探 信任 = 1
        茉拉：问，快点。 [#id=chapter2.mora_testimony.002]

    回应 mora_testimony_guarded
        角色：茉拉
        意图：作证
        条件：侦探 信任 = 0
        茉拉：又问？……快说，我还有病人。 [#id=chapter2.mora_testimony_guarded.001]

    回应 mora_decline
        角色：茉拉
        意图：拒绝
        条件：侦探 信任 < 0
        茉拉：不说。病人等不得，病人的私事我更不会讲。 [#id=chapter2.mora_decline.002]
