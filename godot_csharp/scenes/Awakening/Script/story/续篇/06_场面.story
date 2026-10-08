续篇 chapter2

    场面 arrival
        事件：begin.arrival
        日：2
        时段：早晨
        铜口：诸位，请到中央厅来。全体。 [#id=chapter2.arrival.001]
        （起居厅对面的几扇寝区门，先后响起拔闩的声音。有人探出头，又缩回去，过了一会儿才走出来。） [#id=chapter2.arrival.002]

    场面 gather
        事件：begin.gather
        日：2
        时段：上午
        （中央厅。八角形的穹顶下，十六个人站成了几团。铜柱居中，管道从四面汇进来。） [#id=chapter2.gather.001]
        侦探（心声）：站成几团，是按寝区站的。没人往别的团里走。 [#id=chapter2.gather.002]
        铜口：我是此地的代管者，也是诸位的教师。 [#id=chapter2.gather.003]
        铜口：入夜之后，诸位留于室内。外头的气，夜里不养人。 [#id=chapter2.gather.004]
        铜口：另：入夜后，名录库中若有人以你之名唤你，勿应。 [#id=chapter2.gather.005]
        （它停了一下。一个声音贴着你的耳朵，说了一句。） [#id=chapter2.gather.006]
        铜口：方才那道题，阁下答得精确。 [#id=chapter2.gather.007]
        侦探（心声）：我回过头去。铜柱后面没有人。 [#id=chapter2.gather.008]
        沃德：我们那道题，问的是名录库的柜锁。柜锁开过，谁取过什么，须查明白。 [#id=chapter2.gather.009]
        雀儿：你的章程，留着自己念。 [#id=chapter2.gather.010]
        沃德：我没有点你的名。 [#id=chapter2.gather.011]
        雀儿：你的眼睛点了。 [#emotion=angry] [#id=chapter2.gather.012]
        铜口：诸位，请到讲堂。今日讲守则的读法。 [#id=chapter2.gather.013]

    场面 lesson
        事件：begin.lesson
        日：2
        时段：上午
        （讲堂。一个黑衣人背对着众人，在黑板上写字。粉笔停下时，他侧身让出了板上的字；讲台前的灰上，留着一行窄底鞋印。） [#id=chapter2.lesson.001]
        铜口：一条规矩写了什么，诸位都会读。今日读它没写什么。 [#id=chapter2.lesson.002]
        铜口：以我自己的守则为例。第一条：入夜之后，诸位留于室内。 [#id=chapter2.lesson.003]
        铜口：这一条没写的，最要紧的是什么？——请那位穿风衣的先生答。 [#id=chapter2.lesson.004]
        选择 lesson_answer：lesson_precise、lesson_who、lesson_where
        铜口：本课到此。诸位所立之规，也请这样读。下课。 [#id=chapter2.lesson.005]
        侦探（心声）：读它没写什么。……记下了。 [#id=chapter2.lesson.006]

    场面 lesson_precise_branch
        事件：choice:chapter2:lesson_precise
        日：2
        时段：上午
        动作 lesson_answer：真
        铜口：……正是。“室内”二字，条文一字未界。库房、地窖、无窗的厅，都是室内。读到这里的人，下回会先问。 [#id=chapter2.lesson_precise_branch.001]
        铜口：很好。 [#id=chapter2.lesson_precise_branch.002]
        侦探（心声）：这一声“很好”，比刚才的长一点。我一时听不出，它是在夸我，还是在等下一句。 [#id=chapter2.lesson_precise_branch.003]

    场面 lesson_who_branch
        事件：choice:chapter2:lesson_who
        日：2
        时段：上午
        动作 lesson_answer：假
        铜口：写了。“诸位”。一个不漏。 [#id=chapter2.lesson_who_branch.001]
        铜口：再读一遍。 [#id=chapter2.lesson_who_branch.002]

    场面 lesson_where_branch
        事件：choice:chapter2:lesson_where
        日：2
        时段：上午
        动作 lesson_answer：假
        铜口：规矩不必写为什么。 [#id=chapter2.lesson_where_branch.001]
        铜口：读条文，不读我。 [#id=chapter2.lesson_where_branch.002]

    场面 free
        事件：begin.free
        日：2
        时段：中午
        侦探（心声）：先看他们贴了什么，再听他们怎么说。 [#id=chapter2.free.001]
        侦探（心声）：要叫人陪我走一趟，得先问他肯不肯放下手里的事。 [#id=chapter2.free.002]
        （检视、询问与读板不推进时段；带人查看、传授规矩和请求行动需要时间。） [#id=chapter2.free.003]

    场面 noon_dispute
        事件：commit.noon_dispute
        日：2
        时段：中午
        （配给台前排起了队。茉拉攥着粮仓钥匙站在台后，阮清越拿着一本人头账，挡在她面前。） [#id=chapter2.noon_dispute.001]
        阮清越：按人头。十六份，日末清点。 [#id=chapter2.noon_dispute.002]
        茉拉：莫兰脸色不对。病人先领。 [#emotion=worried] [#id=chapter2.noon_dispute.003]
        阮清越：病人先领，可以。代领的人先在账上签个名，日末才找得到人对。 [#emotion=thoughtful] [#id=chapter2.noon_dispute.004]
        茉拉：莫兰等着吃。先把他那一份给我送过去；回来我签，陪你对账。 [#id=chapter2.noon_dispute.005]
        侦探（心声）：一本账，一把钥匙。队伍已经排到我身后了，饭还没递出一份。 [#id=chapter2.noon_dispute.006]
        选择 noon_dispute：mediate、skip_dispute

    场面 mediate_branch
        事件：choice:chapter2:mediate
        日：2
        时段：中午
        动作 mediate：

    场面 skip_dispute_branch
        事件：choice:chapter2:skip_dispute
        日：2
        时段：中午

    场面 night_hint
        事件：commit.night_hint
        日：2
        时段：夜里
        侦探（心声）：入夜了。回 R-05 睡下，或者找个地方守着。 [#id=chapter2.night_hint.001]
        侦探（心声）：起居厅、后勤院、名录库前厅、中央厅，都有能坐一夜的地方。 [#id=chapter2.night_hint.002]

    场面 dawn
        事件：night.dawn
        日：5
        时段：早晨
        （清晨。铜口把众人又叫到了中央厅。阮清越拿着台账站在铜柱旁，沃德站在他身边。） [#id=chapter2.dawn.001]

    场面 after
        事件：night.after
        日：5
        时段：上午
        侦探（心声）：“昨夜，有人进过后勤院。”——它说的每个字都是真的。 [#id=chapter2.after.001]
        侦探（心声）：可“取了而不记”，就一定是偷？ [#id=chapter2.after.002]
        侦探（心声）：晚饭后对账，人人都会在这里。在那之前，先把账上的缺口查清楚。 [#id=chapter2.after.004]

    场面 no_accuse
        事件：night.no_accuse
        日：5
        时段：上午
        侦探（心声）：账上有缺口，没有人站出来指认谁。这一回，没人先开口。 [#id=chapter2.no_accuse.001]

    场面 replay_unlock
        事件：card.replay_unlock
        日：5
        侦探（心声）：台账上的缺口，落在一夜之间。 [#id=chapter2.replay_unlock.001]
        侦探（心声）：那一夜每个人读过哪条规矩、认了哪条，各自就会照着去做。……把那一夜重演一遍。 [#id=chapter2.replay_unlock.002]
        侦探（心声）：（按 R 打开重演手稿。） [#id=chapter2.replay_unlock.003]

    场面 replay_first
        事件：after_replay.first
        侦探（心声）：墨影只看得出身量，看不出是谁。 [#id=chapter2.replay_first.001]
        侦探（心声）：冲突写的是痕迹之差。哪里对不上，就去哪里再看一眼，或者再问一个人。 [#id=chapter2.replay_first.002]

    场面 replay_match
        事件：after_replay.match
        侦探（心声）：……全对上了。 [#emotion=thoughtful] [#id=chapter2.replay_match.001]
        侦探（心声）：没有贼。同一件事，照茉拉的规矩是“急需自取，次日告知”；照沃德的规矩，是私取。 [#id=chapter2.replay_match.002]
        侦探（心声）：规矩贴在三块板上。没有人读过全部。 [#id=chapter2.replay_match.003]

    场面 evening
        事件：confront.evening
        日：5
        时段：傍晚
        （晚饭后。中央厅点起了灯。十五个人到了，莫兰还躺在诊疗所。） [#id=chapter2.evening.003]
        铜口：诸位要对账。我只旁听。 [#id=chapter2.evening.002]

    场面 confront_begin
        事件：confront.begin
        日：5
        时段：傍晚
        侦探（心声）：一问一答。说一句，就拿出能支撑它的东西。 [#id=chapter2.confront_begin.001]

    场面 confront_pause
        事件：confront.pause
        日：5
        时段：傍晚
        侦探：这一问，我手里的东西还接不上。 [#id=chapter2.confront_pause.001]
        沃德：那便仍是疑处。 [#id=chapter2.confront_pause.002]
        侦探：疑处，先别记到谁的名字底下。 [#id=chapter2.confront_pause.003]

    场面 confront_wrong_say
        事件：confront.wrong_say
        日：5
        时段：傍晚
        沃德：这与我问的无关。 [#id=chapter2.confront_wrong_say.001]

    场面 confront_wrong_show
        事件：confront.wrong_show
        日：5
        时段：傍晚
        沃德：依据何在？{{wrong_name()}}证明不了这句话。 [#emotion=angry] [#id=chapter2.confront_wrong_show.001]

    场面 confront_frame
        事件：confront.frame
        日：5
        时段：傍晚
        （众人的目光都落到了雀儿身上。她没有躲。） [#id=chapter2.confront_frame.001]
        雀儿：……行。你也这么看。 [#id=chapter2.confront_frame.002]
        侦探（心声）：每个字都是真的。可我说出它，是在指着她。 [#id=chapter2.confront_frame.003]

    场面 confront_rule
        事件：confront.rule
        日：5
        时段：傍晚
        沃德：今晚说的话都在这里。下一次取粮，照谁的规矩？ [#id=chapter2.confront_rule.001]
        侦探（心声）：写下一条新规矩，抹不掉昨夜的疑处。但下一次，总得有人知道该怎么办。 [#id=chapter2.confront_rule.002]

    场面 whisper
        事件：confront.whisper
        日：5
        时段：夜里
        （散场的时候，铜口没有出声。有几个人走得很慢，回头看了一眼北边。） [#id=chapter2.whisper.001]
        侦探（心声）：铜口没有响。我却还在等它说话。 [#id=chapter2.whisper.002]

    场面 council
        事件：council.begin
        日：6
        时段：早晨
        （第五日清晨。誓约庭。铺石顺坡，来议事的人散在几级台阶上。） [#id=chapter2.council.007]
        铜口：今日，诸位要决定名录库的事。我只旁听。 [#id=chapter2.council.002]
        沃德：谁进门，取走什么，须有账可查。名录库，由委员会保管。 [#id=chapter2.council.003]
        顾平章：账记得下取走什么，记得下取回以后，人会怎样么？老朽只一句：封着。 [#emotion=worried] [#id=chapter2.council.004]
        霍铎：……我的图纸。给不给我，要先问委员会？ [#emotion=hooked] [#id=chapter2.council.005]
        沃德：你的，也须登记。 [#id=chapter2.council.008]
        霍铎：登记。不是准许。两回事。 [#id=chapter2.council.009]
        霍铎：各人的东西，各人取。谁要我的那份，我不答应；我的，也别替我扣下。 [#id=chapter2.council.010]
        侦探（心声）：同一扇门。有人怕开了收不住，有人怕钥匙交出去，就再要不回。 [#id=chapter2.council.006]

    场面 box
        事件：council.box
        日：6
        时段：早晨
        （库门开了。十六只封存匣排在四座架上，每只贴着名字。） [#id=chapter2.box.001]
        侦探（心声）：我的名字。在第三座架子上，第二格。 [#id=chapter2.box.002]
        侦探：{{detective_name}}。 [#id=chapter2.box.003]
        （匣子没有动。） [#id=chapter2.box.004]
        侦探：……{{detective_name}}。 [#id=chapter2.box.005]
        （第二声落下去。匣子依旧不动，铜口也没有出声。） [#id=chapter2.box.006]
        侦探（心声）：名字没有念错。 [#id=chapter2.box.007]
        侦探（心声）：我没有再念第三遍。 [#id=chapter2.box.008]

    场面 sealed
        事件：council.sealed
        日：6
        时段：早晨
        （议事结束了。名录库的门没有开。） [#id=chapter2.sealed.001]
        侦探（心声）：门可以继续关着。散会以后，我们还得吃饭，还得跟刚才不肯点头的人过下去。 [#id=chapter2.sealed.002]
