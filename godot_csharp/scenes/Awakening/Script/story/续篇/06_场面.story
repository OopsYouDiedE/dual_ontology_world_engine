续篇 chapter2

    场面 arrival
        事件：begin.arrival
        日：2
        时段：早晨
        铜口：诸位，请到中央厅来。全体。 [#id=chapter2.arrival.001]

    场面 gather
        事件：begin.gather
        日：2
        时段：上午
        （中央厅。八角形的穹顶下，十六个人站成了几团。铜柱居中，管道从四面汇进来。） [#id=chapter2.gather.001]
        侦探（心声）：站成几团，是按寝区站的。没人往别的团里走。 [#id=chapter2.gather.002]
        铜口：我是此地的代管者，也是诸位的教师。 [#id=chapter2.gather.003]
        铜口：封存之物，存于名录库。凡学员，皆可取回己物。 [#id=chapter2.gather.004]
        铜口：另：入夜后，名录库中若有人以你之名唤你，勿应。 [#id=chapter2.gather.005]
        （它停了一下。一个声音贴着你的耳朵，说了一句。） [#id=chapter2.gather.006]
        铜口：你的封存匣在名录库里。标签上，写着你的名字。 [#id=chapter2.gather.007]
        侦探（心声）：只有我听见。别人脸上，看不出听见过什么。 [#id=chapter2.gather.008]
        沃德：依据它方才的话，名录库是公物。公物之取用，须有章程。 [#id=chapter2.gather.009]
        雀儿：你的章程，留着自己念。 [#id=chapter2.gather.010]
        沃德：我没有点你的名。 [#id=chapter2.gather.011]
        雀儿：你的眼睛点了。 [#emotion=angry] [#id=chapter2.gather.012]
        铜口：诸位，请到讲堂。今日讲守则的读法。 [#id=chapter2.gather.013]

    场面 lesson
        事件：begin.lesson
        日：2
        时段：上午
        （讲堂。黑板前没有人，粉笔却自己在板上写字。讲台前的灰上，多出一行窄底的鞋印。） [#id=chapter2.lesson.001]
        铜口：一条规矩写了什么，诸位都会读。今日读它没写什么。 [#id=chapter2.lesson.002]
        铜口：以我自己的守则为例。第一条：封存之物，可凭对应之物寻回；寻回者，自负其果。 [#id=chapter2.lesson.003]
        铜口：这一条没写的，最要紧的是什么？——请那位穿风衣的先生答。 [#id=chapter2.lesson.004]
        选择 lesson_answer：lesson_precise、lesson_who、lesson_where
        铜口：本课到此。诸位所立之规，也请这样读。下课。 [#id=chapter2.lesson.005]
        侦探（心声）：读它没写什么。……记下了。 [#id=chapter2.lesson.006]

    场面 lesson_precise_branch
        事件：choice:chapter2:lesson_precise
        日：2
        时段：上午
        动作 lesson_answer：真
        铜口：……正是。“自负其果”，果是什么，条文一字未提。读到这里的人，下回会先问。 [#id=chapter2.lesson_precise_branch.001]
        铜口：很好。 [#id=chapter2.lesson_precise_branch.002]
        侦探（心声）：它是真的高兴。几个人看我的眼神变了。不全是好的那种。 [#id=chapter2.lesson_precise_branch.003]

    场面 lesson_who_branch
        事件：choice:chapter2:lesson_who
        日：2
        时段：上午
        动作 lesson_answer：假
        铜口：写了。“寻回者”——谁寻回，谁便是。不限其人。 [#id=chapter2.lesson_who_branch.001]
        铜口：再读一遍。 [#id=chapter2.lesson_who_branch.002]

    场面 lesson_where_branch
        事件：choice:chapter2:lesson_where
        日：2
        时段：上午
        动作 lesson_answer：假
        铜口：名录库。我方才说过。 [#id=chapter2.lesson_where_branch.001]
        铜口：读条文，不读我。 [#id=chapter2.lesson_where_branch.002]

    场面 free
        事件：begin.free
        日：2
        时段：中午
        侦探（心声）：读不费时辰。看，问，读板上的规矩，都可以慢慢来。 [#id=chapter2.free.001]
        侦探（心声）：想让谁改个主意——带他去亲眼看，跟他讲一条规矩，请他做件事——那要花上一阵。一阵过去，大家各忙各的，人就换了地方。 [#id=chapter2.free.002]
        侦探（心声）：一天就那么几阵。得想好花在谁身上。 [#id=chapter2.free.003]

    场面 noon_dispute
        事件：commit.noon_dispute
        日：2
        时段：中午
        （配给台前排起了队。茉拉攥着粮仓钥匙站在台后，阮清越拿着一本人头账，挡在她面前。） [#id=chapter2.noon_dispute.001]
        阮清越：按人头。十六份，日末清点。 [#id=chapter2.noon_dispute.002]
        茉拉：莫兰脸色不对。病人先领。 [#emotion=worried] [#id=chapter2.noon_dispute.003]
        阮清越：“先”是多少？说个数。 [#emotion=thoughtful] [#id=chapter2.noon_dispute.004]
        茉拉：你让开。 [#id=chapter2.noon_dispute.005]
        侦探（心声）：两个人都没说错。队伍在等。 [#id=chapter2.noon_dispute.006]
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
        侦探（心声）：这一句，先不说。 [#id=chapter2.confront_pause.001]

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
        沃德：账上的疑处，诸位已听见。依据何在，今后如何？ [#id=chapter2.confront_rule.001]
        侦探（心声）：该提一条大家都能照着做的规矩了。 [#id=chapter2.confront_rule.002]

    场面 whisper
        事件：confront.whisper
        日：5
        时段：夜里
        （散场的时候，铜口没有出声。可走出中央厅的每一个人，脚步都慢了一点，像在听耳边有人说话。） [#id=chapter2.whisper.001]
        侦探（心声）：它又对每个人说了一遍那样东西。……今夜，名录库前厅不会太安静。 [#id=chapter2.whisper.002]

    场面 council
        事件：council.begin
        日：6
        时段：早晨
        （第五日清晨。誓约庭。铺石顺坡，十六个人散在几级台阶上。） [#id=chapter2.council.007]
        铜口：今日，诸位要决定名录库的事。我只旁听。 [#id=chapter2.council.002]
        沃德：我仍主张，凡事须有章程。名录库，由委员会保管。 [#id=chapter2.council.003]
        顾平章：回忆即上钩。老朽只一句：封着。 [#emotion=worried] [#id=chapter2.council.004]
        霍铎：……图纸在里面。 [#emotion=hooked] [#id=chapter2.council.005]
        侦探（心声）：每个人会站到哪一边，昨天、前天就定下了大半。看他们这几天亲眼见过什么、饿没饿着、信谁。 [#id=chapter2.council.006]

    场面 box
        事件：council.box
        日：6
        时段：早晨
        （库门开了。十六只封存匣排在四座架上，每只贴着名字。） [#id=chapter2.box.001]
        侦探（心声）：我的名字。在第三座架子上，第二格。 [#id=chapter2.box.002]
        侦探：{{detective_name}}。 [#id=chapter2.box.003]
        （匣子没有动。） [#id=chapter2.box.004]
        侦探：……{{detective_name}}。 [#id=chapter2.box.005]
        （铜口里没有声音。它向来有问必答，这一回没有人问，它也就不说。） [#id=chapter2.box.006]
        侦探（心声）：匣子认得它的主人。它不认我。 [#id=chapter2.box.007]
        侦探（心声）：那么，是谁的？ [#id=chapter2.box.008]

    场面 sealed
        事件：council.sealed
        日：6
        时段：早晨
        （名录库没有开。十六只封存匣留在门后。） [#id=chapter2.sealed.001]
        侦探（心声）：我的名字也在门后。是不是我的，今天没人知道。 [#id=chapter2.sealed.002]
