# 等待与调查接线静态复核（2026-10-18）

范围：附件 `pasted-text-1.txt` 的长等待、调查断链、目标真空、章节预算指控。只读根、language_core 与 verification 的 AGENTS；未读取设计文件。设计终幕边界由主代理核对后告知。未启动会话、构建、回归或 Godot，以下均为源码判断，不能证明人物行为或完整游戏改善。

## 指控复核

- `c2i_navigation_focus` 当前已经优先本地可执行任务、其次异地前提成立任务，最后才回退首项待前提任务。附件“恒选第一未完成项”的描述已经过时。其异地分支仍可能选择没有本人已知开放路径的任务，遮挡后面实际可达的调查；本轮把已知路径作为异地 ready 候选条件。本地任务仍优先，实质目标继续完整保留，所有移动仍真实执行。
- 第二章仍声明 `plan_depth=2`，但已有 `c2i_known_route` 从本人已采纳预测法则和已知通路构造逐跳中继。因此不能单凭三跳路程与深度2断言必然断链。合作前提是否实际满足、窗口是否错失，仍需正式轨迹。
- 普通等待默认上界180分钟、队列等待者不进入认知链的事实成立。原长度选择又跳过已注册的5/1分钟动作，并允许跨过目标14分钟，近处机会会被强制15分钟等待越过。本轮取消该下限与容忍，使用原有成本目录里的长度，且不越过目标。另从本人有来源、未完成、非时钟前提已成立的目标中读取开/闭边界，提前结束等待以重新决策；不读取他人目标或未来物理状态。
- 第二章学员扩章时显式声明 `wait_chunk_limit=30`，给普通等待保留更频繁的响应机会；这是新行为参数，不是同条件性能优化。它不使正在排队的非等待操作可中断，也不保证跨角色协作一定发生。恶魔仍沿其独立教师配置。
- 第五日07:30–09:30议事结束即章末由主代理核设计确认；7200仅工程观测窗口。本轮没有添加午夜休整目标，也没有用时间推进冒充剧情完成。无未来目标不等于缺陷。
- 第二章扩展保留原人物剩余行动、模型与认知资源，未找到附件所建议的章节补充配额；本轮不补配额。当前规划工作上限存在每次选择的 `planning_limit` 合同，不能将附件“320次规划全章逐渐烧光”直接当现行事实。具体停止必须读本轮 stop_evidence。

## 与失败回执修复协作

`c2_move_action` 显式授权 `failure_grants=[c2_access(gate)]` 与 `failure_observe_when=c2_at(actor,origin)`。失败代理负责仅在现场执行成功而DV权威拒绝时，经原感知接口投影这些字段。此声明只给当前位置对应门的公开通路状态，不给天气、别人状态、远端闩或隐藏失败原因。它不提前写入到达，也不放宽移动前提。

## 房间避险尚未接通

`c2_close_room_law` 已定义，但 `c2_room_actions` 返回空目录，未查到生产预测/执行方法采纳。每间房的 doors_closed、windows_closed、isolation_closed、seepage、hazard、sheltered 都从 unknown 开始，未见实际操作写入这些前提。`c2_shelter_law` / `c2_seepage_law` 已采纳到物理 enforce 表，故不是孤立死定义；但没有在 `c2_world_processes` 注册，源码已有明确待接线注释。这一组缺口需要明确实际门窗/隔离设施与裂隙初态、可执行操作及其内容来源，不能仅把法则注册后把 unknown 当 false，更不能自行生成操作剧情。本轮未修此内容缺口。

## 公共文档需同步的合同

1. 第二章学员等待片段上界30；仍沿原费用、预算、排队与Complete结算。
2. 知情等待不得越过本人的下一声明时窗边界；边界来自本人有来源且非时钟前提成立的未完成目标，开闭边界只要求重新决策，不保证未来成功。
3. 异地调查导航须存在本人已知开放且采纳的路径；本地可执行调查仍优先。
4. 内容失败观察需同时声明 grants 与现场条件，默认缺声明不授权；宿主回执、权威拒绝、原感知映射分别留证。

## 修改与静态审读

初始等待修复修改限于 `language_core/verification/scenarios/chapter1/opening.dv` 与 `language_core/verification/scenarios/chapter2/content.dv`，以及本报告；未改配额或生成物。逐段核对了等待长度注册、窗口语法、导航优先顺序、移动来源及门状态可见边界。全工作区 diff 检查发现另一代理正在修改的 checks/08-formal.dv 文件末尾空行，已告知主代理；本任务DV文件未出现差异空白问题。语法、构建与正式行为尚待主代理冻结后统一验证。

## 随后新增授权：实际第二章收尾

主代理转达用户新增授权并明确具体条件后，另修改 `chapter2/daily.dv` 和上述 content.dv。保留真实个人投票/签名；全签才令 council_ended=true，同时 closed=true；6330窗口截止则只 closed=true，仍未全签时 ended保持false。地下轻震改为closed后独立一次环境法则，移除旧侦探在中央厅说“签名已收齐”的固定行。没有添加7200等待。

普通生活厅/中央厅草案检视不再读取完整议事结果。两会场新增正式现场closing检视，其前提为本人同处且已知closed或时钟到6330，读取真实逐人投票、签名、票选、closed/ended与轻震标记；正文按本人观察区分签名齐备和待补，不声称全员意见相同。每次签名行动仅允许本人观察closed，使最后签者能够在6330前实际获知收束；其后由正式检视核对完整记录。截止机会及已知closed后核对ended分别列为有来源个人观察目标。

`c2daily_chapter_summary(physical)`只读输出原始真实状态，保留未知：人物生存/票签/口粮/饥饿/侵蚀/待补记录，资源台与台账，储备加粮以及三组调查交接字段。主代理负责将其接到系统结算与调试末尾；它不写心智、不重置继承状态、不构成主体可知信息。

主代理随后确认最小持续环境信号设计：underground_tremor为收尾期间尚未散去的低鸣/余震，不是历史瞬时脉冲；现场检视正文为“铜口底噪里夹着极轻的共鸣，脚下仍能觉出余震。来由尚待查明。”环境执行不广播私有认识，现场检视读取当前信号。没有把未在场者当作当时听者。

另经主代理确认修改 `chapter2/dialogue.dv`：通用row可声明 observation_grants，按顺序去重追加到行动者自己的grants，仅在真实成功Complete的原感知链生效，不进入public_grants。签者使用该入口核对closed。没有把预测中的环境效果当本人已知。新增章末流程是用户授权的新内容条件，不能与旧会话作同条件提速比较。此后仍未运行会话或构建。

## 独立审读后的收尾多跳接线

仅有closed/ended观察目标仍可能在深度2下无法从沉静室、白塔或户外走到会场再检视。本轮补 `c2i_closing_navigation`：只有本人时钟已达6330或本人实际已知closed，且本人未成功完成任何closing检视时才生成导航。已在两会场之一时原地核对；否则比较本人已知开放、已采纳预测法则支持的两条路径，取跳数最少者，同长时中央厅优先。目标与中继均只是意图，实际移动继续原prepare/Complete，不预置门状态或到达。

收尾意图存在时，仅抑制旧调查focus的到达/中继奖励，实质调查目标保持；不并列生成两个导航目的。成功closing检视后移除收尾导航。路线缓存加入全部声明gate的本人认识、closed单元、可收尾真值和两会场成功检视记录，真实位置/通路/机会/完成变化会重建。未提高深度或预算，未运行会话；源码差异空白检查未报告该文件问题。

## 昼夜等待参数收窄

独立审查指出全天30分钟片段会额外消耗有限动作配额。本轮改为章节只声明 daytime_wait_chunk_limit=30；等待helper保留本人原wait_chunk_limit（未声明为180），仅第二章且本人日内时间480≤t<1260时取两者较小值。夜间继续本人原上界，窗口边界仍可提前截短；不重置本人原声明。恶魔没有新增白天参数。此前本报告“第二章片段上限30”由本节限定为白天响应参数。

## 未接通房间草案退役

按主代理追加授权，完成全部现役DV以及非artifacts代码的引用搜索：c2.room.*、close_room/shelter/seepage与空room_actions仅在content.dv生产注册内部互引；未发现行动、目标、观察、会话或Godot消费者。c2_rooms仍为正式地点目录c2_locations提供地名，故保留。

将原三个axiom与空action函数逐字保存在 `verification/checks/fixtures/room-safety-draft.dv`，fixture显式包含生产entry以便单独静态审阅，生产entry不反向包含fixture。另存draft对象构造器，原门、窗、隔离、渗漏、危险、庇护输入保持unknown；没有推定其安全值。对Git HEAD原声明做文本对照确认三份axiom和空函数正文原样保留。

生产移除仅草案使用的房间对象生成、共享对象名单、无进程消费者的enforce采纳、三个法则声明，以及空action函数与目录调用；原world_processes已有“未接线”注释随退役清理。runtime_contracts将室内庇护/渗漏定性要求单列为unimplemented_room_safety_draft，列出隔离路径与unknown身份，不再作为生产qualitative_laws宣称。

真实corridor检查/换气/开门、户外危险与暴露执行未改。移除会改变新会话对象/采纳表及序列化形状，因此不是逐字状态等价承诺；静态依据仅支持没有现役动作读取这组无来源字段。旧存档或未知外部消费者未执行，正式行为仍未验证。没有改历史证据、运行会话或构建；主代理将冻结前统一工程验证。

## 正式运行崩溃后的caller只读核对

主代理报告run-180s于71.297现实秒退出，最后行动gu.c2.document.apology@2329，错误Repeated observation。本代理未重跑，仅读现役caller：c2i_actions先为调查行动声明自己grants=[ref]、public_grants=[ref]、public_observers=c2_people；gu_apologized等talk_fields再将行动改talk、添加speech和listeners=c2_people，原public_observers保留。因此单个grants列表没有重复ref，实际重复来自传播路径：行动者先本人grants，之后speech public_grants又包含本人；同处旁观者先visible_action_grants、之后public_grants也再次读取同execution_id/subject/ref。Core观察身份正是这三个字段，同次真实观察被双重投影可导致该错误。

同构talk_fields还包括key_returned、moran_reassured、abandoned_child_misunderstanding_resolved、medicine_key_given。新增observation_grants仅在daily签名row声明closed，dialogue按ref去重追加到本人grants，不进入public_grants，与2329行动无关。没有修改caller或session；结论与具体位置已告知主代理和负责公共传播的代理，修复由其统一处理。
