# 策略是人物的条件与优先度，不是操作顺序。认知来源是当面听到的题面与门闩说明。
# 牵引不替侦探移动、作答或运行试验；新话语仅占位，主代理补写。
策略牵引
    cooldown_minutes：20
    source_scenes：
        s5_question：s5_question、clock_question
    object_labels：
        hide：收起物件
        destroy：毁坏物件
    object_choices：
        E6：hide、destroy
        E7：hide、destroy
        E10：hide、destroy
    wrong_scenes：answer_wrong_say、answer_wrong_show、answer_need_test
    world_resolution_scenes：quer_opens
    decline_choices：
        talk_huo_menu:choice:9：huo
        talk_shen_menu:choice:9：shen
        talk_su_menu:choice:9：su
    people：
        huo：
            clues：E7、E8
            methods：
                report：strategy_report_huo
                test：strategy_test_huo
                invite：strategy_invite_huo
                remember：strategy_remember_huo
        shen：
            clues：E10、E9、E4
            methods：
                report：strategy_report_shen
                test：strategy_test_shen
                invite：strategy_invite_shen
                remember：strategy_remember_shen
        su：
            clues：E6、E5
            methods：
                report：strategy_report_su
                test：strategy_test_su
                invite：strategy_invite_su
                remember：strategy_remember_su

流程 钟点重提题面
    演出 clock_question

场面 clock_question
    铜口：此为始业之问：门是谁闩上的？闩门之时，此人可曾听见门外的呼喊？ [#id=clock_question.001]
    跳转 END

场面 strategy_report_huo
    # TODO 台词：本人看见侦探久无进展，报告石板或铜口的本人所见；下接实际已知内容。
    霍铎：【TODO 台词：主动报告本人所见】 [#id=strategy_report_huo.001]
    跳转 END
场面 strategy_test_huo
    # TODO 台词：担心题答不完，提议试验；侦探自己决定是否做。
    霍铎：【TODO 台词：提议一起做试验】 [#id=strategy_test_huo.001]
    跳转 END
场面 strategy_invite_huo
    # TODO 台词：请侦探去看自己留意的东西，不代替侦探走位。
    霍铎：【TODO 台词：邀请查看本人留意的物件】 [#id=strategy_invite_huo.001]
    跳转 END
场面 strategy_remember_huo
    # TODO 台词：亲见物件变化后，以本人记得的原内容作证；下接记忆中的所见。
    霍铎：【TODO 台词：以本人记得的内容补位】 [#id=strategy_remember_huo.001]
    跳转 END

场面 strategy_report_shen
    # TODO 台词：本人看见侦探久无进展，报告本人所见；下接实际已知内容。
    沈鼎：【TODO 台词：主动报告本人所见】 [#id=strategy_report_shen.001]
    跳转 END
场面 strategy_test_shen
    # TODO 台词：担心题答不完，提议试验；侦探自己决定是否做。
    沈鼎：【TODO 台词：提议一起做试验】 [#id=strategy_test_shen.001]
    跳转 END
场面 strategy_invite_shen
    # TODO 台词：请侦探去看自己留意的东西，不代替侦探走位。
    沈鼎：【TODO 台词：邀请查看本人留意的物件】 [#id=strategy_invite_shen.001]
    跳转 END
场面 strategy_remember_shen
    # TODO 台词：亲见物件变化后，以本人记得的原内容作证；下接记忆中的所见。
    沈鼎：【TODO 台词：以本人记得的内容补位】 [#id=strategy_remember_shen.001]
    跳转 END

场面 strategy_report_su
    # TODO 台词：本人看见侦探久无进展，报告本人所见；下接实际已知内容。
    苏泠：【TODO 台词：主动报告本人所见】 [#id=strategy_report_su.001]
    跳转 END
场面 strategy_test_su
    # TODO 台词：担心题答不完，提议试验；侦探自己决定是否做。
    苏泠：【TODO 台词：提议一起做试验】 [#id=strategy_test_su.001]
    跳转 END
场面 strategy_invite_su
    # TODO 台词：请侦探去看自己留意的东西，不代替侦探走位。
    苏泠：【TODO 台词：邀请查看本人留意的物件】 [#id=strategy_invite_su.001]
    跳转 END
场面 strategy_remember_su
    # TODO 台词：亲见物件变化后，以本人记得的原内容作证；下接记忆中的所见。
    苏泠：【TODO 台词：以本人记得的内容补位】 [#id=strategy_remember_su.001]
    跳转 END
