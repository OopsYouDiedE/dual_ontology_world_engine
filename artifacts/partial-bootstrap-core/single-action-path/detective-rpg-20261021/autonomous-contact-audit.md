# 自主检视/作答现场接触审读

范围：只读根现役40/60 DV、30 Complete边界及Godot宿主/既有地图源码；未读设计文件，未改根源或06冻结版，未构建或运行。依据源码中已有前提、travel、sensory_mode、物理法则及呈现声明分类，不把类别inspect自动解释为触摸。

## 结论

`field.executor=dv-world,site="",contact=true`不能作为显式现场接触证据：Main把未核对的contact初值设为true。现役c1的E1–E12检视、answer与若干opening动作确实没有travel.station，但三类含义不同：普通E检视在动作模型中仅声明同房，answer是发言/判断；明确已有station的接触动作已经走物理执行器；另有物理操作法则/呈现明确写了咬、刮、播放、穿衣、泡茶等动作，却没有执行空间绑定，是需单列的实际机制缺口。不能把三者合并为“所有AI检视/回答均伪造接触”。

## 主链与绑定差异

- 40:40-41的c1_action只存precondition、authority_precondition、law、grants、source_refs；没有自动生成接触或站点。
- 60:140-165的dvg_intent仅在awaiting_input选项上调用dvg_interaction。prepared若非move且没有travel.station便原样返回，不附UI的site/person绑定。既有action.interaction若确实已声明可保留在action内，但Main非移动分支不消费它。
- 60:177-208的dvg_interaction映射E1–E12、比较鞋底、穿衣、声路试验及所有answer等到site；这些目前是人工UI选项绑定，不能冒称已经在自主Prepared执行过几何。
- Main._dv_execute当前仅move或travel.station进入DvFieldExecutor。其他动作只对viewer的人工_interaction_site调用_in_reach；自主actor没有该手工site时contact保留true，回执没有站点或实际接触核验。Main的外层executor=godot只是宿主路由标识，不使内层默认布尔值成为物理证书。
- 30:6272-6297的Complete验证action_event、实际费用、权威前提和言语条件，按outcome及law执行/结算；未把任意field.contact=true重新解释为世界接触事实。

## c1基础E检视：声明只要求同房

| 动作 | physical/authority前提 | 显式travel.station | 根据源码可作的结论 |
| --- | --- | --- | --- |
| inspect.E1/E2/E3/E4/E5 | foyer | 无 | room级检视；不能从回执称已经触摸门闩、楔或音叉 |
| inspect.E6 | R08 | 无 | 房间内乐谱观察；精细文本可读距离没有另声明，不能临时加半径 |
| inspect.E7/E8 | hall | 无 | 房间内石板/铜管观察；人工有E7/transmitter热点，自主未走该近距UI |
| inspect.E9/E10 | pantry | 无 | 杯/酵种观察；同房是当前形式前提，不是手已触杯/罐的证明 |
| inspect.E11/E12 | R05 | 无 | 书架/风衣观察，未声明站点执行 |

源码位置：40:163-330；E5:225-231、E7:253-259、E9/E10:281-302。霍铎/苏泠/沈鼎的专业额外grants也没有顺带增加station。以上“room级”是实现事实，不宣称所有细节在真实屏幕任意位置可见。E1正文含“拉不动”、E8含“一碰就晃”，与纯视觉模型可能存在表述/来源张力，须主代理核对意图；不能由审读者自动升级为已执行触碰或改写正文。

## 已显式要求站点的动作：当前确有几何路径

| 动作 | 已有travel | 执行与限制 |
| --- | --- | --- |
| detective.touch_latch（40:579-584） | foyer→foyer，site:E1 | kind=move；成功后才给own-executed-contact与身体闪回 |
| *.compare.E2（1185-1188） | foyer→foyer，site:E2 | executed-physical-comparison，原站点/半径/回执链 |
| *.compare.E11（1190-1194） | R05→R05，site:E11 | executed-four-person-palm-comparison；逻辑前提要求四人在R05，但几何只移动执行actor，不能据此宣称四人都已在书架边 |
| *.deep-resonance（1197-1202、1289） | 本房→本房，既有site:E7/E10/E6 | 明确主动接触目标，宿主按原travel.station执行 |

这些不因AI缺_interaction_site而绕过既有station。比较掌印的“所有人同房”与“所有人逐一近书架”仍不是同一几何证明；后者需已有内容明确的多人执行合同，不能由一个actor成功回执代替其余三人输入。

## 作答与其他同房观察

answer.me/V*/trust（40:394-496）在authority侧只要求hall，另有问题、认知、论据与言语合同；c1_answer_actions再包装为公开作答发言。没有手碰铜口的声明，60的answer→transmitter只是当前人工操作入口。因此同房作答可按当前模型成功，但site空不能证明走到铜口前；也不能无授权把answer成功条件扩大为身体接触。

opening.wake（40:4217-4238）包含自身记忆、体感与当前房间的自观察；不能把private-monologue强制变成走床边。其他寝区trace/own-material/quer-departure观察（2613-2628）同样只要求各自声明房间，不等于触摸。其精确可见性合同尚未在此审读中补设。

## 明确物理操作语义但缺执行空间绑定

| 声明/呈现 | 当前实际前提与缺口 | 现有可用映射及边界 |
| --- | --- | --- |
| opening.bite-silver、scrape-testpiece、play-audition（40:4177-4195、4240-4244） | hall与物件在opening.cabinet；sensory_mode含tactile-inspection/auditory-playback，law记录last_bite_by/last_scraped_by/last_operator。无travel/station；自主直接dv-world可产生这些物理操作结果 | 内容只给cabinet在hall、铜口下方；地图无opening.cabinet/silver/testpiece/audition专属已绑定站点。不能把transmitter或zone_rules任意当作柜前坐标 |
| prepare-tea、serve-tea（40:4167-4178、4298、4314） | hall/茶已泡等法则，实际改变泡茶和分杯状态；无现场操作站点 | 地图有hall_tea_side Marker，但DV未声明这些动作到该Marker的关系；名称相近不足以自动绑定 |
| detective.opening.wear-coat（4296） | R05+owner，law写worn_by；无travel | 人工已有E12绑定及已有几何，若要求接近可复用该明确映射，不加数值；自主Prepared目前未消费 |
| *.compare_sole（40:542-550） | experiment，仅foyer；正文输出鞋底与印迹比对，但无travel | 人工已有E2映射；是否需近印迹与既有station的语义关系应由内容明确，不能以experiment类别强推 |
| *.sound_trial（552-559） | hall+本人知道pipe_layout，改变sound_trial_done并公开grants；无travel | 人工已有transmitter映射；声路操作如何涉及现场与其他听者位置未给空间执行合同 |
| identify-own-lens / opening.empty-slot（332-340、4346） | room/owner等，非显式station | 人工分别E12/zone_rules；读袋内/柜内的近距条件和具体对象不能被site空默认true代替 |

另外60:80-87的talk呈现写苏泠敲门框、霍铎摸石板、沈鼎擦杯/揭罐，正式talk actor可能是提问者而呈现动作主体是同伴。这不是给提问者补site就能证明的物理执行；需主代理决定这些是现时独立动作还是回忆/证言表述，并按原声明拆分或修订。宿主不能代控同伴、补一次成功或把呈现动画当成法则回执。

## 最小提案，按声明分层

1. 先修证据含义：非几何分支不应输出未经测量的contact=true来表示接触。可显式记录contact_checked=false、geometry_scope=world-law/room-only；没有接触声明的纯视觉/自观察/言语仍依原权威前提执行，不无故拦截或偷加站点。具体字段修改须同步工程合同。
2. 内容40/60为确需现场接触的动作声明统一execution/interaction合同，人工和自主Prepared均携带。已有E12、E2、transmitter等明确映射可复用；目标actor、逻辑房、执行map/room和site由内容给出。通用宿主不解析剧情动作ID、不根据中文文字猜目标。
3. 宿主按该合同使用执行actor的execution_snapshot与同一保留身体/导航/碰撞/原站点范围接近，成功Complete后才发布原law/grants；已有travel.station路径可复用。不能单靠viewer的_interaction_site检查NPC，也不改变动作预算、半径或凭UI绑定授知识。若物件无既有明确几何映射，保留缺口并由主代理核对内容，不能临时加站点数值或就近凑站。
4. 普通E检视是否都应近距、answer是否必须站铜口前、多人掌印比对如何现场执行，属于需明确的内容/接口选择；源码当前不足以替用户定论。先不要把所有inspect/answer统一走站点作为“修复”。

此报告只确认源码机制与证据范围，没有复用engine04运行宣称新行为，也没有把同房视觉观察判为物理触碰。后续若改执行合同，须独立新冻结和实际输入/现场回执核查。
