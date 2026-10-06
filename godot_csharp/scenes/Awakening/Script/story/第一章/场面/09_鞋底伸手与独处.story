场面 sole_huo
    霍铎：（抬起一只脚，鞋底朝你） [#id=sole_huo.001]
    （厚底工装靴，粗齿纹。比灰上那枚宽了一指。） [#id=sole_huo.002]
    跳转 END

场面 sole_shen
    沈鼎：看鞋？行。（把布鞋翻过来）千层底，自己纳的。 [#id=sole_shen.001]
    （布底没有纹。灰上那枚，纹路清清楚楚。） [#id=sole_shen.002]
    跳转 END

场面 sole_su
    苏泠：（犹豫了一下，抬起脚） [#id=sole_su.001]
    （软底小皮鞋，比灰上那枚短了一截。） [#id=sole_su.002]
    跳转 END

场面 reach_huo
    （霍铎走到长桌边，拿起了那块石板。他盯着上面的字，拇指慢慢压上粉笔印。） [#id=reach_huo.001]
    霍铎：{{生活所思("huo")}} [#id=reach_huo.002]
    选择：
        - （劝阻）“别碰。” [#id=reach_huo.003]
            侦探：别碰。 [#id=reach_huo.004]
            设旗 reach_decline
        - （指着公告框）“守则第 1 条。” [#id=reach_huo.005]
            侦探：公告框上的第一条，你看过没有。 [#id=reach_huo.006]
            设旗 reach_rule
        - （不出声，任他自便） [#id=reach_huo.007]
    跳转 END

场面 reach_su
    （苏泠在门框旁蹲下，拾起了那把音叉。她把它举到耳边，没有敲。） [#id=reach_su.001]
    苏泠：{{生活所思("su")}} [#id=reach_su.002]
    选择：
        - （劝阻）“别碰。” [#id=reach_su.003]
            侦探：别碰。 [#id=reach_su.004]
            设旗 reach_decline
        - （指着公告框）“守则第 1 条。” [#id=reach_su.005]
            侦探：公告框上的第一条，你看过没有。 [#id=reach_su.006]
            设旗 reach_rule
        - （不出声，任她自便） [#id=reach_su.007]
    跳转 END

场面 reach_shen
    （沈鼎从架上端起酵种罐，拇指搭在罐壁的面痕上。） [#id=reach_shen.001]
    沈鼎：{{生活所思("shen")}} [#id=reach_shen.002]
    选择：
        - （劝阻）“别碰。” [#id=reach_shen.003]
            侦探：别碰。 [#id=reach_shen.004]
            设旗 reach_decline
        - （指着公告框）“守则第 1 条。” [#id=reach_shen.005]
            侦探：公告框上的第一条，你看过没有。 [#id=reach_shen.006]
            设旗 reach_rule
        - （不出声，任他自便） [#id=reach_shen.007]
    跳转 END

场面 alone_huo
    若 独处选择("huo") == "touch"：
        霍铎：我想起了一点。 [#id=alone_huo.001]
        共享回忆 huo
        霍铎：{{片段("huo")}} [#id=alone_huo.002]
        霍铎：那块石板……我刚才一个人碰的。 [#id=alone_huo.003]
    否则：
        霍铎：刚才去看了看石板。没碰。 [#id=alone_huo.004]
        霍铎：{{生活所思("huo")}} [#id=alone_huo.005]
    跳转 END

场面 alone_su
    若 独处选择("su") == "touch"：
        苏泠：……我想起了一点。 [#id=alone_su.001]
        共享回忆 su
        苏泠：{{片段("su")}} [#id=alone_su.002]
        苏泠：你不在。我自己握的。 [#id=alone_su.003]
    否则：
        苏泠：我去拿过那把音叉。……又放下了。 [#id=alone_su.004]
        苏泠：{{生活所思("su")}} [#id=alone_su.005]
    跳转 END

场面 alone_shen
    若 独处选择("shen") == "touch"：
        沈鼎：我想起了一点。 [#id=alone_shen.001]
        共享回忆 shen
        沈鼎：{{片段("shen")}} [#id=alone_shen.002]
        沈鼎：罐子是我自己端的。没人劝，也没人拦。 [#id=alone_shen.003]
    否则：
        沈鼎：我把罐子端起来，又放回去了。 [#id=alone_shen.004]
        沈鼎：{{生活所思("shen")}} [#id=alone_shen.005]
    跳转 END
