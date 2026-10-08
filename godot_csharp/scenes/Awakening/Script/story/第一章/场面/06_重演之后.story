场面 s8_reversal
    （重演的墨影一个个淡去。四个人都没有说话。） [#id=s8_reversal.001]
    沈鼎：……备餐间那罐酵种，不像是昨天才养的。 [#id=s8_reversal.002]
    霍铎：……我的手认得这块石板。 [#id=s8_reversal.003]
    苏泠：（低头看着自己的手） [#id=s8_reversal.004]
    苏泠：他不给我放糖。第一杯就没放。 [#id=s8_reversal.005]
    沈鼎：早上她问我，我说顺手。……哪有那么顺的手。 [#id=s8_reversal.006]
    若 钩数("detective") > 0：
        侦探（心声）：我记得霍铎说闩，我就去闩了门。那时我没有问他第二句。 [#id=s8_reversal.007]
    否则：
        侦探（心声）：若昨夜真是这样，霍铎说闩，我便去闩了门。可我凭什么信他？ [#id=s8_reversal.008]
    跳转 END

场面 rest_confirm
    铜口：阁下可以先在生活翼歇一会。水与食物照常，无需认任何事。 [#id=rest_confirm.001]
    铜口：何时继续查看、何时作答，由阁下自己决定。其他人的去留，不由这次选择决定。 [#id=rest_confirm.002]
    选择：
        - 就这样吧。歇息。 [#id=rest_confirm.003]
            设旗 rest_chosen
        - 还是再看看。 [#id=rest_confirm.004]
    跳转 END
