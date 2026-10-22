# 同源两局上游行动链对照

对象为当前179340bd的S1（Standard）与S4-foolish（Foolish）。只读各自stdout.dvlog、raw-event-index.dvlog、result.dvlog及玩家可见全文；未读设计/世界源码，未执行会话，未扫描原始大DAG。游戏分钟与现实秒分别列出；现实秒直接来自原事件索引process_elapsed_ms，不按倍速换算。

## 关键发现与优先断点

1. S1有实际拼合清册、顾平章致歉、沃德撤回限制；S4只有承担整理/拼合，沃德先将清册藏入铁匣，没有后续实际整理与拼合。此为真实上游差异。
2. 但S1的雀儿在沃德2364撤回时没有听到（speech_unheard / no-audibility-grant），后续仍因未收到撤回而拒绝管道、铜屑、精密门栓、酸蚀抽屉工作。故S1没有形成“翻案→本人收到解除限制→恢复合作”的完整链。不能用世界撤回旗标替代雀儿认知。
3. 两局均有安抚莫兰、实际展出底稿；均未记录实际弃子身份澄清完成。两局均只承担伤疤、配药、道歉交钥匙工作，未记录这些实作完成；雀儿的酸蚀抽屉合作仍被拒绝。
4. 两局都没有三人实际开仓、配药完成、领取药、实际给药链。S4比S1多霍铎承担千斤顶工作，但承担不等于操作。两局药物链均未完成，不能拿它们宣称标准局已解决医化和解并救治成功。
5. 已记录的直接生死分叉是晚间规约：S1有侦探提出并张贴复测约定，苏泠/莫兰实际采纳，莫兰去仪器阵反复采样；S4没有这份约定的实际发布/采纳，莫兰进夹道后实际暴露、昏迷、呼吸衰竭和死亡。两局watched=false，都不能称值守已生效。

## 实际动作对照与实测时刻

以下只列action_execution。undertake/refuse-restriction等本身是已完成的对话行动，绝不当作其所谈论的体力/技术工作已经完成。

### S1

|事件|主体|游戏分钟|现实秒|实际行动|结果|
|---|---|---:|---:|---|---|
|e3039|ruan|674|7.381409|ruan.answer.astral.ruan：提交人物答复：阮清月|applied|
|e3052|elena|674|7.387854|elena.answer.astral.ruan：提交人物答复：陌生学员·10|applied|
|e3065|selma|674|7.393607|selma.answer.astral.ruan：提交人物答复：陌生学员·10|applied|
|e3078|moran|674|7.399874|moran.answer.astral.ruan：提交人物答复：陌生学员·10|applied|
|e19929|quer|2283|56.314643|quer.c2.document.dismantle-lock.undertake：承担：拆验锁具，记录实际痕迹。|applied|
|e20088|ward|2285|56.688768|ward.c2.document.half-paper.undertake：承担：查找并出示各自实际持有的散页，核对清册与授权上下文。|applied|
|e20162|lucian|2286|56.832392|lucian.c2.motive.inspect.case.astral.quiet_room：检视现场实物与痕迹|applied|
|e20166|ward|2286|56.840917|ward.c2.document.return-key.undertake：承担：核对代管依据与现场记录，办理钥匙交接。|applied|
|e20309|gu|2288|57.117803|gu.c2.document.apology.undertake：承担：依据实际取得的材料，重新评估并说明先前判断。|applied|
|e20681|quer|2300|59.076817|quer.c2.document.dismantle-lock：检视文献工房的锁具|applied|
|e20729|quer|2301|59.212093|quer.c2.ask-restriction-status：询问指控是否撤回|applied|
|e20811|ward|2303|59.434615|ward.c2.document.half-paper：出示保存的半张纸|applied|
|e20895|detective|2306|59.810197|detective.c2.document.inventory.undertake：承担：查找并出示各自实际持有的散页，核对清册与授权上下文。|applied|
|e20965|detective|2307|59.903976|detective.c2.document.join.undertake：承担：查找并出示各自实际持有的散页，核对清册与授权上下文。|applied|
|e21272|detective|2324|61.012493|detective.c2.document.inventory：整理文献工房的散页|applied|
|e21550|detective|2341|62.740779|detective.c2.document.join：拼合清册并核对记录|applied|
|e21624|gu|2349|62.932900|gu.c2.document.apology：为先前的指认致歉|applied|
|e21819|ward|2364|63.291108|ward.c2.withdraw-restriction：撤回先前的指控|applied|
|e25746|moran|3388|76.993040|moran.c2.motive.inspect.case.astral.quiet_room：检视现场实物与痕迹|applied|
|e25934|moran|3451|77.741474|moran.c2.astral.draft.undertake：承担：承担自己能做的现场核对，所见与结果另行记录。|applied|
|e26327|detective|3460|78.521028|detective.c2.motive.inspect.case.astral.quiet_room：检视现场实物与痕迹|applied|
|e26404|detective|3462|78.680331|detective.c2.astral.reassure.undertake：承担：承担自己能做的现场核对，所见与结果另行记录。|applied|
|e26457|detective|3463|78.760863|detective.c2.astral.reassure：安抚画夹的主人|applied|
|e26623|elena|3477|79.151675|elena.c2.motive.inspect.case.astral.quiet_room：检视现场实物与痕迹|applied|
|e26631|elena|3478|79.177089|elena.c2.astral.identify.undertake：承担：依据实际取得的材料，重新评估并说明先前判断。|applied|
|e27225|moran|3588|81.332208|moran.c2.astral.draft：展出画夹中的底稿|applied|
|e28035|quer|3617|84.288120|quer.c2.granary.precision.refuse-restriction：说明保留意见：在结构与载荷稳定后操作双钩和精密门栓。|applied|
|e28247|margaret|3634|84.749967|margaret.c2.granary.wedge.undertake：承担：检查结构后置楔，持续托住门框。|applied|
|e28749|veronica|3723|86.212495|veronica.c2.safety.scars.undertake：承担：承担自己能做的现场核对，所见与结果另行记录。|applied|
|e28820|veronica|3724|86.301880|veronica.c2.safety.mix.undertake：承担：承担自己能做的现场核对，所见与结果另行记录。|applied|
|e28882|mora|3725|86.395146|mora.c2.safety.apology-key.undertake：承担：承担自己能做的现场核对，所见与结果另行记录。|applied|
|e29006|quer|3742|86.678046|quer.c2.safety.acid-drawer.refuse-restriction：说明保留意见：承担自己能做的现场核对，所见与结果另行记录。|applied|
|e34094|detective|5378|105.619951|detective.c2.propose.measurement_pause：提出：震后复测与返程约定|applied|
|e34137|detective|5379|105.664180|detective.c2.post.measurement_pause：张贴：震后复测与返程约定|applied|
|e34147|detective|5380|105.711197|detective.c2.read.measurement_pause：阅读：震后复测与返程约定|applied|
|e34158|detective|5381|105.757962|detective.c2.accept.measurement_pause：赞成：震后复测与返程约定|applied|
|e34312|su|5392|106.736096|su.c2.accept.measurement_pause：赞成：震后复测与返程约定|applied|
|e35172|su|5582|109.064709|su.c2.read.measurement_pause：阅读：震后复测与返程约定|applied|
|e35192|selma|5582|109.095864|selma.c2.read.measurement_pause：阅读：震后复测与返程约定|applied|
|e35197|moran|5582|109.103971|moran.c2.inspect.current-watch-living：查看门内的值守者|applied|
|e35238|selma|5583|109.242449|selma.c2.decline.measurement_pause：不采纳：震后复测与返程约定|applied|
|e35309|moran|5584|109.472810|moran.c2.read.measurement_pause：阅读：震后复测与返程约定|applied|
|e35324|moran|5585|109.515896|moran.c2.accept.measurement_pause：赞成：震后复测与返程约定|applied|
|e35374|moran|5588|109.688666|moran.c2.move.living_hall.central_hall：前往主楼 · 中央厅|applied|
|e35384|moran|5590|109.719916|moran.c2.move.central_hall.outdoor_meter：前往室外 · 仪器阵|applied|

#### 完成/知识回执

- e21273，detective，游戏2324，现实61.016932秒：case.contract.emergency_inventory_majority=True。
- e21551，detective，游戏2341，现实62.745184秒：case.contract.joined_inventory=True。
- e21835，quer，游戏2364，现实63.296266秒：雀儿未听到沃德撤回话语；reason=no-audibility-grant，source_event=e21821。
- e27226，moran，游戏3588，现实81.335924秒：case.astral.complete_draft=True。
- e27227，detective，游戏3588，现实81.336332秒：case.astral.complete_draft=True。
- e28036，quer，游戏3617，现实84.292101秒：c2.person.quer.work.accepted.quer.c2.granary.precision=False；c2.person.quer.work.decided.quer.c2.granary.precision=True。
- e29007，quer，游戏3742，现实86.683663秒：c2.person.quer.work.accepted.quer.c2.safety.acid-drawer=False；c2.person.quer.work.decided.quer.c2.safety.acid-drawer=True。
- e35198，moran，游戏5582，现实109.107482秒：c2.person.moran.watched=False。

#### 未形成的后续动作链

按两局全部action_execution（包含拒绝回执）核对：实际完整动作 c2.astral.identify、c2.safety.scars、c2.safety.mix、c2.safety.apology-key、c2.granary.jack、c2.granary.wedge、c2.granary.precision 均未记录；带.undertake的对话另见上表。没有以配药、领药、给药/治疗为标签的正式执行记录。外间清点/开启配给台已经发生，但不是粮仓三人开锁，也不是给药。

#### 终态伤亡（实际状态投影，不授予人物知识）

- su：serious_injury=true（游戏5040）；motor_impairment=true（游戏5040）；active_erosion_disease=true（游戏0）；severe_weakness=true（游戏4320）。
本轮已知死亡：无。
### S4-foolish

|事件|主体|游戏分钟|现实秒|实际行动|结果|
|---|---|---:|---:|---|---|
|e2003|ruan|674|5.819496|ruan.answer.astral.ruan：提交人物答复：阮清月|applied|
|e2016|elena|674|5.827390|elena.answer.astral.ruan：提交人物答复：陌生学员·10|applied|
|e2029|selma|674|5.834166|selma.answer.astral.ruan：提交人物答复：陌生学员·10|applied|
|e2042|moran|674|5.840961|moran.answer.astral.ruan：提交人物答复：陌生学员·10|applied|
|e13348|quer|2148|36.695066|quer.c2.ask-restriction-status：询问指控是否撤回|applied|
|e19021|quer|2285|55.683822|quer.c2.document.dismantle-lock.undertake：承担：拆验锁具，记录实际痕迹。|applied|
|e19063|ward|2285|55.697138|ward.c2.document.half-paper.undertake：承担：查找并出示各自实际持有的散页，核对清册与授权上下文。|applied|
|e19142|ward|2286|55.865363|ward.c2.document.return-key.undertake：承担：核对代管依据与现场记录，办理钥匙交接。|applied|
|e19279|lucian|2288|56.180340|lucian.c2.motive.inspect.case.astral.quiet_room：检视现场实物与痕迹|applied|
|e19283|gu|2288|56.191817|gu.c2.document.apology.undertake：承担：依据实际取得的材料，重新评估并说明先前判断。|applied|
|e19737|quer|2302|58.616131|quer.c2.document.dismantle-lock：检视文献工房的锁具|applied|
|e19778|ward|2303|58.708932|ward.c2.document.half-paper：出示保存的半张纸|applied|
|e19949|detective|2308|59.270368|detective.c2.document.inventory.undertake：承担：查找并出示各自实际持有的散页，核对清册与授权上下文。|applied|
|e20007|detective|2309|59.395554|detective.c2.document.join.undertake：承担：查找并出示各自实际持有的散页，核对清册与授权上下文。|applied|
|e24804|moran|3390|76.990890|moran.c2.motive.inspect.case.astral.quiet_room：检视现场实物与痕迹|applied|
|e25654|moran|3457|79.159543|moran.c2.astral.draft.undertake：承担：承担自己能做的现场核对，所见与结果另行记录。|applied|
|e25810|detective|3460|79.369552|detective.c2.motive.inspect.case.astral.quiet_room：检视现场实物与痕迹|applied|
|e25901|detective|3462|79.562517|detective.c2.astral.reassure.undertake：承担：承担自己能做的现场核对，所见与结果另行记录。|applied|
|e25960|detective|3463|80.490355|detective.c2.astral.reassure：安抚画夹的主人|applied|
|e26150|elena|3479|80.853898|elena.c2.motive.inspect.case.astral.quiet_room：检视现场实物与痕迹|applied|
|e26168|elena|3480|80.885201|elena.c2.astral.identify.undertake：承担：依据实际取得的材料，重新评估并说明先前判断。|applied|
|e26885|moran|3594|82.751745|moran.c2.astral.draft：展出画夹中的底稿|applied|
|e27838|huo|3617|85.918849|huo.c2.granary.jack.undertake：承担：核对载荷并操作千斤顶，持续保持抬升。|applied|
|e27897|quer|3618|85.992800|quer.c2.granary.precision.refuse-restriction：说明保留意见：在结构与载荷稳定后操作双钩和精密门栓。|applied|
|e28127|margaret|3636|86.516740|margaret.c2.granary.wedge.undertake：承担：检查结构后置楔，持续托住门框。|applied|
|e28717|veronica|3723|88.304052|veronica.c2.safety.scars.undertake：承担：承担自己能做的现场核对，所见与结果另行记录。|applied|
|e28806|veronica|3724|88.427760|veronica.c2.safety.mix.undertake：承担：承担自己能做的现场核对，所见与结果另行记录。|applied|
|e28877|mora|3725|89.197603|mora.c2.safety.apology-key.undertake：承担：承担自己能做的现场核对，所见与结果另行记录。|applied|
|e29011|quer|3742|89.498274|quer.c2.safety.acid-drawer.refuse-restriction：说明保留意见：承担自己能做的现场核对，所见与结果另行记录。|applied|
|e36329|moran|5582|116.457311|moran.c2.inspect.current-watch-living：查看门内的值守者|applied|
|e36435|moran|5586|116.764144|moran.c2.move.living_hall.central_hall：前往主楼 · 中央厅|applied|
|e36468|moran|5588|116.865650|moran.c2.move.central_hall.outdoor_meter：前往室外 · 仪器阵|applied|
|e36491|moran|5591|116.921458|moran.c2.move.outdoor_meter.outdoor_high：前往室外 · 高地通路|applied|
|e36501|moran|5593|116.941981|moran.c2.move.outdoor_high.outdoor_alley：前往室外 · 夹道|applied|

#### 完成/知识回执

- e26886，moran，游戏3594，现实82.755816秒：case.astral.complete_draft=True。
- e26887，detective，游戏3594，现实82.756216秒：case.astral.complete_draft=True。
- e27898，quer，游戏3618，现实85.998092秒：c2.person.quer.work.accepted.quer.c2.granary.precision=False；c2.person.quer.work.decided.quer.c2.granary.precision=True。
- e29012，quer，游戏3742，现实89.502673秒：c2.person.quer.work.accepted.quer.c2.safety.acid-drawer=False；c2.person.quer.work.decided.quer.c2.safety.acid-drawer=True。
- e36330，moran，游戏5582，现实116.461325秒：c2.person.moran.watched=False。

#### 未形成的后续动作链

按两局全部action_execution（包含拒绝回执）核对：实际完整动作 c2.astral.identify、c2.safety.scars、c2.safety.mix、c2.safety.apology-key、c2.granary.jack、c2.granary.wedge、c2.granary.precision 均未记录；带.undertake的对话另见上表。没有以配药、领药、给药/治疗为标签的正式执行记录。外间清点/开启配给台已经发生，但不是粮仓三人开锁，也不是给药。

#### 终态伤亡（实际状态投影，不授予人物知识）

- su：serious_injury=true（游戏5040）；motor_impairment=true（游戏5040）；active_erosion_disease=true（游戏0）；severe_weakness=true（游戏4320）。
- moran：dead=true（游戏5714）；in_coma=true（游戏5593）；serious_injury=true（游戏5594）；respiratory_failure=true（游戏5594）。
本轮已知死亡：moran。

## 玩家可见全文的边界

S1第2日15:01实际拼合、第2日15:09顾平章致歉、第2日15:24沃德明确撤回“由我提出的自由限制”；第3日09:35与09:36雀儿仍说“实际听到过先限制我自由的要求，尚未收到撤回说明”。事件e21835给出没有听到的正式回执，因此这不是仅凭全文沉默推测。S4第2日14:27检视见清册在铁匣内、没有取出；后面未见整理、拼合、致歉、撤回实际行动。

两局第3日09:40均实际观察到传声管堵塞、压差式被擦，且底稿/身份相关实作尚未完成；之后只有莫兰展出底稿（S1第3日11:48；S4第3日11:54）。展出底稿不等于身份澄清已被本人/同伴接受。

S1全文有侦探第4日17:38提议、第4日17:39张贴、第4日17:40阅读、第4日17:41赞成复测约定。S4没有这段实际公告内容，虽然此前众人多次“检视公告”行动存在；检视不存在/未公布的公告不能替代发布，也不能替代采纳。

## 关于两死前因的审查界限

当前S4-foolish封存终态只记录莫兰死亡；苏泠仍存活、重伤。所要求的“两死”不能从本局补造。S1与S4的医化和解、开仓和实际给药均未闭合，因此现有两局不能证明“标准局成功解决这些心结，而Foolish失败造成两死”这一完整对照。现有证据最先需要处理的是S1撤回没有到达雀儿本人，其次两局技术工作停在承担/拒绝之后，最后标准局反复采样耗尽配额未本人章末。这里列的是断点与已经发生的差异，不宣称设计符合性或全部验收。

原始DAG未展开，具体资格缺失、规划未选动作的完整根因不据缺席推定；工具后处理状态与工程根因另由主代理核对。

## 夹道分叉的原始事件时刻

|局|事件|游戏分钟|现实秒|记录|
|---|---|---:|---:|---|
|S1|e30499|4320|92.760791|c2.physiology.su.weakness|
|S1|e32523|5040|99.980268|c2.physiology.su.impairment|
|S4-foolish|e31053|4320|96.968092|c2.physiology.su.weakness|
|S4-foolish|e33403|5040|105.782837|c2.physiology.su.impairment|
|S4-foolish|e36502|5593|116.943776|c2.outdoor-danger.moran|
|S4-foolish|e36503|5593|116.944560|c2.moran.acute_collapse|
|S4-foolish|e36507|5594|116.950521|c2.physiology.moran.respiratory-failure|
|S4-foolish|e36508|5714|116.953958|c2.physiology.moran.respiratory-terminal|

## 规约实际采纳/不采纳索引

此表仅收action_execution中实际执行的accept/decline，不把阅读、检视、提案、规范候选或世界层有效状态当采纳。

### S1

|事件|主体|游戏分钟|现实秒|实际采纳/不采纳|
|---|---|---:|---:|---|
|e5342|shen|1806|15.225025|shen.c2.accept.shen_kitchen（applied）|
|e5368|quer|1807|15.299000|quer.c2.decline.shen_kitchen（applied）|
|e5378|detective|1807|15.310304|detective.c2.accept.shen_kitchen（applied）|
|e5968|su|1866|16.554422|su.c2.decline.shen_kitchen（applied）|
|e8200|lucian|2020|23.038260|lucian.c2.accept.lucian_no_brand（applied）|
|e9426|ward|2052|25.546835|ward.c2.accept.ward_ledger（applied）|
|e9434|gu|2052|25.552814|gu.c2.accept.seal_plan（applied）|
|e9791|ward|2058|26.048650|ward.c2.accept.ward_night（applied）|
|e9983|ward|2064|26.378231|ward.c2.accept.committee_plan（applied）|
|e11582|veronica|2117|30.645575|veronica.c2.accept.veronica_evidence（applied）|
|e11874|quer|2126|31.258847|quer.c2.accept.veronica_evidence（applied）|
|e11961|shen|2128|31.528986|shen.c2.decline.veronica_evidence（applied）|
|e12252|detective|2131|32.257262|detective.c2.accept.veronica_evidence（applied）|
|e12258|su|2131|32.264867|su.c2.accept.veronica_evidence（applied）|
|e12629|margaret|2133|33.615682|margaret.c2.accept.veronica_evidence（applied）|
|e17636|lucian|2253|49.286251|lucian.c2.accept.shen_kitchen（applied）|
|e17642|ward|2253|49.292141|ward.c2.accept.shen_kitchen（applied）|
|e17656|elena|2253|49.312545|elena.c2.decline.shen_kitchen（applied）|
|e17662|selma|2253|49.318765|selma.c2.accept.shen_kitchen（applied）|
|e17668|moran|2253|49.324593|moran.c2.decline.shen_kitchen（applied）|
|e17674|mora|2253|49.330675|mora.c2.accept.shen_kitchen（applied）|
|e17684|margaret|2253|49.346968|margaret.c2.accept.shen_kitchen（applied）|
|e18383|mora|2262|50.974258|mora.c2.decline.ward_ledger（applied）|
|e18397|shen|2262|50.995606|shen.c2.accept.records_supplement（applied）|
|e18405|quer|2262|51.006200|quer.c2.accept.records_supplement（applied）|
|e18462|bertrand|2263|51.219488|bertrand.c2.accept.shen_kitchen（applied）|
|e18468|shen|2263|51.229779|shen.c2.accept.seal_plan（applied）|
|e18501|ruan|2264|51.359091|ruan.c2.accept.records_supplement（applied）|
|e18579|lucian|2265|51.583440|lucian.c2.accept.records_supplement（applied）|
|e18589|mora|2265|51.603030|mora.c2.decline.ward_night（applied）|
|e18715|ward|2267|51.996827|ward.c2.decline.seal_plan（applied）|
|e18832|lucian|2269|52.410150|lucian.c2.accept.ward_ledger（applied）|
|e18851|mora|2269|52.441582|mora.c2.accept.seal_plan（applied）|
|e19042|mora|2272|53.804093|mora.c2.decline.committee_plan（applied）|
|e19064|lucian|2272|54.104156|lucian.c2.decline.ward_night（applied）|
|e19322|lucian|2276|54.800645|lucian.c2.decline.seal_plan（applied）|
|e19438|ruan|2278|55.192037|ruan.c2.accept.ruan_ledger（applied）|
|e19514|lucian|2279|55.347215|lucian.c2.accept.committee_plan（applied）|
|e19823|mora|2282|56.159575|mora.c2.decline.ruan_ledger（applied）|
|e20156|detective|2286|56.823643|detective.c2.accept.records_supplement（applied）|
|e20219|mora|2286|56.865740|mora.c2.accept.mora_night（applied）|
|e20395|detective|2290|57.395373|detective.c2.decline.ward_ledger（applied）|
|e20502|detective|2293|58.434168|detective.c2.decline.ward_night（applied）|
|e20610|detective|2297|58.854475|detective.c2.decline.seal_plan（applied）|
|e20703|detective|2300|59.112914|detective.c2.accept.committee_plan（applied）|
|e23925|veronica|2706|70.315897|veronica.c2.decline.mora_night（applied）|
|e23972|veronica|2709|70.471734|veronica.c2.accept.ruan_ledger（applied）|
|e25545|moran|3366|76.414158|moran.c2.decline.records_supplement（applied）|
|e25587|moran|3370|76.525294|moran.c2.decline.ward_ledger（applied）|
|e25614|moran|3373|76.594652|moran.c2.decline.ward_night（applied）|
|e25653|moran|3377|76.687784|moran.c2.decline.seal_plan（applied）|
|e25683|moran|3380|76.766444|moran.c2.decline.committee_plan（applied）|
|e26079|elena|3455|78.080440|elena.c2.decline.records_supplement（applied）|
|e26313|elena|3459|78.467073|elena.c2.decline.ward_ledger（applied）|
|e26398|elena|3462|78.668637|elena.c2.decline.ward_night（applied）|
|e26527|elena|3466|78.906543|elena.c2.accept.seal_plan（applied）|
|e26557|elena|3469|78.993224|elena.c2.decline.committee_plan（applied）|
|e27754|margaret|3605|82.880780|margaret.c2.accept.records_supplement（applied）|
|e27843|margaret|3609|83.126179|margaret.c2.accept.ward_ledger（applied）|
|e27936|margaret|3612|83.306433|margaret.c2.decline.ward_night（applied）|
|e27959|quer|3613|83.368166|quer.c2.accept.mora_night（applied）|
|e28010|margaret|3616|84.229281|margaret.c2.decline.seal_plan（applied）|
|e28016|quer|3616|84.236890|quer.c2.decline.ruan_ledger（applied）|
|e28099|margaret|3619|84.377701|margaret.c2.accept.committee_plan（applied）|
|e28206|margaret|3630|84.654866|margaret.c2.decline.mora_night（applied）|
|e28236|margaret|3633|84.726333|margaret.c2.accept.ruan_ledger（applied）|
|e34158|detective|5381|105.757962|detective.c2.accept.measurement_pause（applied）|
|e34312|su|5392|106.736096|su.c2.accept.measurement_pause（applied）|
|e35238|selma|5583|109.242449|selma.c2.decline.measurement_pause（applied）|
|e35324|moran|5585|109.515896|moran.c2.accept.measurement_pause（applied）|
|e39362|selma|6335|124.892834|selma.c2.accept.records_supplement（applied）|
|e39369|veronica|6335|124.918931|veronica.c2.accept.records_supplement（applied）|
|e39436|selma|6339|125.204238|selma.c2.decline.ward_ledger（applied）|
|e39442|veronica|6339|125.231575|veronica.c2.accept.ward_ledger（applied）|
|e39487|selma|6342|125.435026|selma.c2.decline.ward_night（applied）|
|e39493|veronica|6342|125.462585|veronica.c2.decline.ward_night（applied）|
|e39559|selma|6346|125.797089|selma.c2.decline.seal_plan（applied）|
|e39565|veronica|6346|125.846790|veronica.c2.decline.seal_plan（applied）|
|e39616|selma|6349|126.086333|selma.c2.decline.committee_plan（applied）|
|e39622|veronica|6349|126.115062|veronica.c2.accept.committee_plan（applied）|
### S4-foolish

|事件|主体|游戏分钟|现实秒|实际采纳/不采纳|
|---|---|---:|---:|---|
|e4341|shen|1806|12.815581|shen.c2.accept.shen_kitchen（applied）|
|e4366|quer|1807|12.877082|quer.c2.decline.shen_kitchen（applied）|
|e4376|detective|1807|12.888182|detective.c2.accept.shen_kitchen（applied）|
|e4981|huo|1866|14.041234|huo.c2.accept.shen_kitchen（applied）|
|e4987|su|1866|14.046790|su.c2.decline.shen_kitchen（applied）|
|e6843|huo|1991|19.310162|huo.c2.accept.open_plan（applied）|
|e6858|quer|1992|19.367500|quer.c2.accept.open_plan（applied）|
|e7354|lucian|2020|20.721095|lucian.c2.accept.lucian_no_brand（applied）|
|e8606|ward|2052|24.046459|ward.c2.accept.ward_ledger（applied）|
|e8614|gu|2052|24.053261|gu.c2.accept.seal_plan（applied）|
|e8972|ward|2058|24.551149|ward.c2.accept.ward_night（applied）|
|e9164|ward|2064|24.896394|ward.c2.accept.committee_plan（applied）|
|e10855|veronica|2117|29.304753|veronica.c2.accept.veronica_evidence（applied）|
|e11205|huo|2127|30.381580|huo.c2.accept.veronica_evidence（applied）|
|e11257|quer|2128|30.546914|quer.c2.accept.veronica_evidence（applied）|
|e11303|shen|2129|31.213404|shen.c2.decline.veronica_evidence（applied）|
|e11571|detective|2131|31.915919|detective.c2.accept.veronica_evidence（applied）|
|e11577|su|2131|31.922707|su.c2.accept.veronica_evidence（applied）|
|e11987|margaret|2133|32.678771|margaret.c2.accept.veronica_evidence（applied）|
|e16398|lucian|2253|47.760607|lucian.c2.accept.shen_kitchen（applied）|
|e16404|ward|2253|47.768708|ward.c2.accept.shen_kitchen（applied）|
|e16418|elena|2253|47.791796|elena.c2.decline.shen_kitchen（applied）|
|e16424|selma|2253|47.798356|selma.c2.accept.shen_kitchen（applied）|
|e16430|moran|2253|47.804527|moran.c2.decline.shen_kitchen（applied）|
|e16436|mora|2253|47.811681|mora.c2.accept.shen_kitchen（applied）|
|e16446|margaret|2253|47.830707|margaret.c2.accept.shen_kitchen（applied）|
|e16978|gu|2259|49.738135|gu.c2.decline.open_plan（applied）|
|e17159|shen|2261|50.158989|shen.c2.decline.open_plan（applied）|
|e17225|mora|2262|50.324370|mora.c2.decline.ward_ledger（applied）|
|e17251|shen|2262|50.355027|shen.c2.accept.records_supplement（applied）|
|e17361|bertrand|2263|50.612718|bertrand.c2.accept.shen_kitchen（applied）|
|e17433|ruan|2264|50.796185|ruan.c2.accept.records_supplement（applied）|
|e17450|huo|2264|50.820200|huo.c2.accept.records_supplement（applied）|
|e17503|shen|2265|50.976816|shen.c2.accept.seal_plan（applied）|
|e17509|lucian|2265|50.984005|lucian.c2.accept.records_supplement（applied）|
|e17524|mora|2265|51.007966|mora.c2.decline.ward_night（applied）|
|e17586|ward|2266|51.197759|ward.c2.decline.open_plan（applied）|
|e17752|mora|2268|51.579492|mora.c2.decline.open_plan（applied）|
|e17787|lucian|2269|51.707568|lucian.c2.accept.ward_ledger（applied）|
|e17793|ward|2269|51.715267|ward.c2.decline.seal_plan（applied）|
|e17926|mora|2271|52.096313|mora.c2.accept.seal_plan（applied）|
|e17974|lucian|2272|52.288201|lucian.c2.decline.ward_night（applied）|
|e18117|mora|2274|52.700278|mora.c2.decline.committee_plan（applied）|
|e18184|lucian|2275|53.599049|lucian.c2.decline.open_plan（applied）|
|e18340|lucian|2278|53.979375|lucian.c2.decline.seal_plan（applied）|
|e18351|ruan|2278|53.997628|ruan.c2.accept.ruan_ledger（applied）|
|e18692|lucian|2281|54.950303|lucian.c2.accept.committee_plan（applied）|
|e18923|mora|2284|55.427363|mora.c2.decline.ruan_ledger（applied）|
|e19131|detective|2286|55.848686|detective.c2.accept.records_supplement（applied）|
|e19331|mora|2288|56.219521|mora.c2.accept.mora_night（applied）|
|e19382|detective|2290|56.495129|detective.c2.accept.ward_ledger（applied）|
|e19493|detective|2293|56.827955|detective.c2.decline.ward_night（applied）|
|e19584|detective|2296|57.243278|detective.c2.decline.open_plan（applied）|
|e19658|detective|2299|58.307027|detective.c2.decline.seal_plan（applied）|
|e19729|detective|2302|58.608638|detective.c2.accept.committee_plan（applied）|
|e22873|veronica|2706|68.977315|veronica.c2.decline.mora_night（applied）|
|e22916|veronica|2709|69.104282|veronica.c2.accept.ruan_ledger（applied）|
|e24587|moran|3366|76.306692|moran.c2.decline.records_supplement（applied）|
|e24622|moran|3370|76.406848|moran.c2.decline.ward_ledger（applied）|
|e24649|moran|3373|76.487170|moran.c2.decline.ward_night（applied）|
|e24679|moran|3376|76.578789|moran.c2.accept.open_plan（applied）|
|e24709|moran|3379|76.685254|moran.c2.decline.seal_plan（applied）|
|e24739|moran|3382|76.805123|moran.c2.decline.committee_plan（applied）|
|e25476|elena|3455|78.960315|elena.c2.decline.records_supplement（applied）|
|e25773|elena|3459|79.293651|elena.c2.decline.ward_ledger（applied）|
|e25894|elena|3462|79.551842|elena.c2.decline.ward_night（applied）|
|e26025|elena|3465|80.585587|elena.c2.decline.open_plan（applied）|
|e26055|elena|3468|80.648732|elena.c2.accept.seal_plan（applied）|
|e26085|elena|3471|80.716051|elena.c2.decline.committee_plan（applied）|
|e27517|margaret|3605|85.076161|margaret.c2.accept.records_supplement（applied）|
|e27616|margaret|3609|85.340542|margaret.c2.accept.ward_ledger（applied）|
|e27692|margaret|3612|85.538507|margaret.c2.decline.ward_night（applied）|
|e27724|huo|3613|85.618179|huo.c2.decline.mora_night（applied）|
|e27743|quer|3614|85.676211|quer.c2.accept.mora_night（applied）|
|e27775|margaret|3615|85.761596|margaret.c2.decline.open_plan（applied）|
|e27808|huo|3616|85.842059|huo.c2.accept.ruan_ledger（applied）|
|e27827|quer|3617|85.905382|quer.c2.decline.ruan_ledger（applied）|
|e27940|margaret|3618|86.007559|margaret.c2.decline.seal_plan（applied）|
|e27981|margaret|3621|86.131178|margaret.c2.accept.committee_plan（applied）|
|e28086|margaret|3632|86.404067|margaret.c2.decline.mora_night（applied）|
|e28116|margaret|3635|86.492482|margaret.c2.accept.ruan_ledger（applied）|
