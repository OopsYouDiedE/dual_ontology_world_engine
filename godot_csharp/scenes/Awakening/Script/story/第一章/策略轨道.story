# 策略是人物的条件与优先度，不是操作顺序。认知来源是当面听到的题面与门闩说明。
# 牵引不替侦探移动、作答或运行试验。报告与补位之后接本人实际知道的证物内容；邀请之后本人走到那件东西旁。
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
    霍铎：（走到你旁边，声音压得很低）题还没答。我看过一样东西，说给你。 [#id=strategy_report_huo.001]
    跳转 END
场面 strategy_test_huo
    霍铎：光看不行，得试。你要试，我搭手。 [#id=strategy_test_huo.001]
    跳转 END
场面 strategy_invite_huo
    霍铎：跟我来。有个地方，你该看一眼。 [#id=strategy_invite_huo.001]
    跳转 END
场面 strategy_remember_huo
    霍铎：这东西动过了。原来什么样，我记得。 [#id=strategy_remember_huo.001]
    跳转 END

场面 strategy_report_shen
    沈鼎：在这儿干站着，题可不会自己答完。我留意过一样东西，你听听。 [#id=strategy_report_shen.001]
    跳转 END
场面 strategy_test_shen
    沈鼎：光这么看是看不出来的。要不动手试一回？我给你搭把手，你说了算。 [#id=strategy_test_shen.001]
    跳转 END
场面 strategy_invite_shen
    沈鼎：来，过来一下。有样东西我一直觉得不对劲，你自己看。 [#id=strategy_invite_shen.001]
    跳转 END
场面 strategy_remember_shen
    沈鼎：这东西被人动过了。先前它什么样，我记得清楚。 [#id=strategy_remember_shen.001]
    跳转 END

场面 strategy_report_su
    苏泠：（她犹豫了一下才开口）……有件事，我一直想说给你听。 [#id=strategy_report_su.001]
    跳转 END
场面 strategy_test_su
    苏泠：……要不要试一次？我可以帮你听。 [#id=strategy_test_su.001]
    跳转 END
场面 strategy_invite_su
    苏泠：……跟我来一下。那边有样东西，我想让你看看。 [#id=strategy_invite_su.001]
    跳转 END
场面 strategy_remember_su
    苏泠：……它原来不是这样的。我记得它先前的样子。 [#id=strategy_remember_su.001]
    跳转 END
