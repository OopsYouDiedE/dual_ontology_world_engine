场面 answer_open
    铜口：请讲，{{称呼("detective")}}。我一句一句地问。 [#id=answer_open.001]
    铜口：每一问，请阁下说一句结论，再拿出一件撑得住它的东西。说错了不要紧——我只指出它说明的是别的事。 [#id=answer_open.002]
    铜口：我只问我问过的。我所说的每一句，诸位都可以拿来对质。 [#id=answer_open.003]
    跳转 END

场面 answer_q1
    铜口：门，是谁闩上的？ [#id=answer_q1.001]
    跳转 END

场面 answer_ok1
    铜口：坦率。 [#id=answer_ok1.001]
    铜口：那么——阁下闩门的时候，可曾听见门外的呼喊？ [#id=answer_ok1.002]
    跳转 END

场面 answer_q2
    铜口：门外，有人呼喊吗？ [#id=answer_q2.001]
    跳转 END

场面 answer_need_test
    铜口：一根铜管，一个松了的接头。它们说明声音能走哪条路。 [#id=answer_need_test.001]
    铜口：可那条路上会剩下什么，阁下试过吗？ [#id=answer_need_test.002]
    跳转 END

场面 answer_ok2
    铜口：我说，主楼有人向诸位呼喊。 [#id=answer_ok2.001]
    铜口：他确实在主楼。 [#id=answer_ok2.002]
    跳转 END

场面 answer_q3
    铜口：他喊的是什么？ [#id=answer_q3.001]
    跳转 END

场面 answer_ok3
    （铜口沉默了片刻。） [#id=answer_ok3.001]
    铜口：……他要的，是诸位活下去。 [#id=answer_ok3.002]
    跳转 END

场面 answer_q4
    铜口：闩门的人，为何不听门边那位女士的？ [#id=answer_q4.001]
    跳转 END

场面 answer_ok4
    铜口：信任，需要时日。 [#id=answer_ok4.001]
    跳转 END

场面 answer_wrong_show
    若 错名 == "苏泠的乐谱"：
        铜口：乐谱记下的是她听见了什么。它没有说声音从哪里来。 [#id=answer_wrong_show.001]
    否则 若 错名 == "门底的灰与木楔"：
        铜口：灰说明门开过，木楔撑过。是谁撑的、为何撑，它没有说。 [#id=answer_wrong_show.002]
    否则 若 错名 == "门底的湿布"：
        铜口：湿布说明有人堵过门缝。那是另一个人、另一件事。 [#id=answer_wrong_show.003]
    否则 若 (错名 == "酵种罐") 或 (错名 == "架上的无尘方框")：
        铜口：这件东西说的是日子，不是那一夜的这一问。 [#id=answer_wrong_show.004]
    否则 若 错名 == "侦探的外衣口袋"：
        铜口：放大镜是阁下的。它自己不会去闩门，也不会去听门。 [#id=answer_wrong_show.005]
    否则 若 错名 == "门框旁的音叉"：
        铜口：音叉说明有人在门边敲过。敲的人想知道的，和我问的，不是一件事。 [#id=answer_wrong_show.006]
    否则 若 错名 == "灰上的靴印"：
        铜口：靴印回答的是谁站在那里。这一问，问的是别的。 [#id=answer_wrong_show.007]
    否则 若 错名 == "霍铎的石板"：
        铜口：石板记着一条指令。指令从哪里来、经过了谁，它没有写。 [#id=answer_wrong_show.008]
    否则 若 错名 == "铜口与管接头"：
        铜口：铜管说明声音能走哪条路。这一问，问的不是路。 [#id=answer_wrong_show.009]
    否则 若 错名 == "四只杯子"：
        铜口：杯子说明有人泡过茶，按人放了糖。它说明的是别的事。 [#id=answer_wrong_show.010]
    否则 若 错名 == "闩上的隔离门"：
        铜口：门说明它闩着。我已经说过了。 [#id=answer_wrong_show.011]
    否则：
        铜口：{{错名()}}。这件东西说明了别的事。 [#id=answer_wrong_show.012]
    跳转 END

场面 answer_wrong_say
    若 错名 == ""：
        铜口：阁下拿出了东西，却没有说它说明什么。 [#id=answer_wrong_say.001]
    否则：
        铜口：“{{错名()}}” [#id=answer_wrong_say.002]
        铜口：这句话，回答的是别的问题。 [#id=answer_wrong_say.003]
    跳转 END

场面 answer_pause
    铜口：不急。答案备好了，再来。 [#id=answer_pause.001]
    跳转 END
