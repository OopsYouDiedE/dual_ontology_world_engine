# 沃德导航折返审查（冻结04，只读，2026-10-18）

## 范围与结论

只读审查frozen-04/standard-yi的stdout投影、原始DAG和该批封存源码；未读设计规范，未改生产源码，未运行会话。两次DAG索引只用于读取既有证据，未推进游戏。原始记录仍是raw-events.dag.jsonl.partial；以下仅说明已提交事件，不假定整局完成。

确认沃德在没有即时实质收益时，分别因纯到达achievement=1离开中央厅和离开起居厅。共同规划如实扣除了移动时间费用；问题在内容导航追加了一个独立获奖的首步位置目标，并允许实质前提不成立的fallback进入该通道。不是人物被硬编码往返，也不能归因于费用未扣除。

## 一去一回的实际记录

| 实际事件 | stdout行 | 游戏分钟 | 记录 |
| --- | --- | --- | --- |
| e16608 | 3123 | 2267（第2日13:47） | living_hall→central_hall实际applied，time=2 |
| e16643 | 3129 | 2267 | 选择central_hall→living_hall，score=.98 |
| e16753 | 3154 | 2269（13:49） | 到达living_hall，实际applied，time=2 |
| e16785/e16786 | 3158 | 2269 | 选择living_hall→central_hall，score=.98 |
| e16852 | 3170 | 2271（13:51） | 到达central_hall，实际applied，time=2 |
| e16885 | 3176 | 2271 | 又选择central_hall→living_hall，score=.98 |

原始DAG解码e16643和e16786：所选根计划均information=0、verification=0、progress=0、private_utility=0、owner_utility.value=0、achievement=1、opportunity=0，opportunity_evidence为空。两者current_affordance.action=false、net_score=0，stop=complete-depth，time_blocked=0。这些是本人实际比较输出，不是根据标签猜测。

e16643的后继候选包含到起居厅后检视廊门、北廊和时钟，累计得分均.97；其本步achievement=0、information=0。e16786的后继候选包含到中央厅后检视廊门、ward_ledger和ward_night公告板，累计得分亦.97，本步实质收益均0。故两次选择的正分都来自第一步到达，未来自已经预测出的后续实质工作。e16753到达后至e16786选择返回之间没有沃德实质执行事件；投影为本人观察/共同在场处理后立即重新选择。

## 本人信息与可定位的目标

沃德e7303（stdout1213行，1833）实际观察c2.daily.meal_prepared=false、portions=0、draft_notes=false，source=perception/time=1833。检查至2269的沃德投影，没有上述meal_prepared和draft_notes的更新。e16254（3037行，2256）实际看到c2.norm.night_forbid.posted=false；e16283（3045行，2257）看到records_supplement.posted=false。e16754（3154行）取得本人执行回执时钟campaign.clock.minute=2269，source=perception/origin=execution-receipt；不能把本次时钟更新解释成meal_prepared或draft_notes的新观察。

封存content.dv的c2_personal_goals按固定顺序先建立本人规范张贴目标，再建立用餐目标。沃德的night_forbid目标是posted=true、navigation_room=central_hall，navigation_requirements=[c2_norm_available(norm)]。c2_norm_available对night_forbid要求draft_notes=true及c2.dialogue.person.ward.said.ward.c2.discuss-forbid=true。本人draft_notes实际为false，已足以使这个工作前提不成立；不是未知条件，也不是仅仅没走到场地。

同函数的第2日午餐目标为c2.person.ward.eaten.2.noon=true，2160≤本人时钟<2280，navigation_room=living_hall，navigation_requirements为本人rations≥1或c2.daily.meal_prepared=true；方法为ward.c2.collect.2.noon、ward.c2.eat.2.noon及ward.c2.daily.serve。本例发生在该窗口内。meal_prepared已知false；封存c2_person_fields初始rations=0、eaten.2.noon=false，c2_extend_actor_from只向本人初始化其自有字段。至本段投影未发生沃德领餐、吃饭、serve或改变口粮的执行/观察。这个rations=0链条是封存初始化源码与未发生更新的轨迹推导，不能冒写为2269新观察或完整当时mind快照。

沃德已经在1834以e7362完成自我介绍，e7363取得said.ward.c2.dialogue.introduce=true的真实观察。因此本段不能凭“起居厅有介绍动作”就归因为他重复寻找介绍机会。

导航母目标未以独立字段写入planning_choice；所选移动本身只有通路来源。因而“night_forbid与午餐是这次first/fallback的具体母目标”属于按封存函数顺序及上述本人条件重建的最窄解释，不冒称原事件直接记录了母目标ID。原始DAG直接确证的部分是纯到达收益、没有即时实质行动、后继检视没有信息收益以及实际返程。后续若需要无推导歧义的归因，可在导航声明中保留母目标的稳定内容标识、原available_when、requirements真值及来源引用；只增加诊断来源，不改变选择。

## 源码缺口

封存content.dv的c2_navigation_goals先检查goal仍pending、available_when去除本人位置条件后为true，再计算ready。ready=false不会排除候选：异地route存在即保存fallback；没有ready的first时直接first=fallback。随后无论ready的原值，生成c2_goal(本人位于route.next, first.when且本人位于route.origin, basis+route)，没有在生成目标中携带导航实质requirements。dvp_achievement_pending只需本人已采纳的移动预测把这个位置命题变真，便计其weight（默认1）。于是“知道任务条件不成立”仍可转换成“到那里即可得1”的独立收益。

达到某个本地目标房间时，c2_navigation_local_ready找不到真正合格的方法便不会保留本地工作优先；循环继续选另一个异地fallback。重新规划后原来首步位置目标被重建为相反方向，实际抵达并没有持久完成母任务，也没有新的现场信息改变前提。这个接线机制与本段往返、root .98和后继.97相符。

## 通用修复边界（建议，未实施）

1. 将requirement的true、false、unknown分别处理。本人明确false时，不以“任务最终完成”为由追加可反复领取的到达奖励。保留原实质目标，条件以后通过真实事件改变仍可重新取得资格。
2. 未知条件可以产生查验动机，但必须绑定真实、本人可执行的现场信息方法及目标字段、现有来源和新鲜度要求。导航取得的是接近查验机会，不能预言未知结果、他人响应或任务成功。现场检视已无信息收益时，不继续靠固定位置奖励往返。
3. false也可能值得复查，但必须由独立、已有来源的复查目标或真实刷新条件支持；不能将所有false自动当作待调查unknown。本例单纯更新本人时钟不刷新meal_prepared/draft_notes。
4. 区分“未到场所以方法暂不能执行”与“去掉本人位置前提仍被知识、动机、规范、请求、同伴或资源条件阻断”。只剥离该动作确实属于本人的目标地点条件，保留其他共同资格；不得借导航跳过主体决策和实际传播。
5. 已知关闭的下游门可以保留有来源的安全侧查验/接近方法；不能泛化成对任意不可行任务仍给每次首步到达奖励。母目标、路线前缀和现场方法应保留共同标识以避免重规划反向重新获奖。

修复不得注入饭菜/草稿、预置他人回复、冻结沃德、强制等待或禁止正常回头，也不改预算、行动时间、判定和费用。其他实质动机继续进入共同规划。新源码及正式行为证据完成前，以上均是候选机制修正。

## 性能与剧情范围

这段轨迹证明重复移动消耗真实游戏时间和执行工作，尚不能量化修复后的五日现实耗时，也不能据此声称剧情变好。五日性能、实际目标达成、人物动机合理性和完整封存分别记录；没有运行新会话验证任何建议。

## 第六批前最终建议

赞同去除纯fallback路线：实质目标requirements不为true时，不建立它的首步到达奖励；独立evidence目标沿其无额外要求的ready路径驱动现场查验。这保留未知结果和真实执行，避免从“想做但条件不成立”直接派生无限到达奖励。

现有meal_prepared evidence使用minimum_time=0，1833的旧false因此足以满足2130开始的午餐观察窗口。将下界改为2130正好对应既有c2_window(2,690,810)起点；它只要求本窗口的一次真实新观察，不能声称会自动感知此后饭菜变化。可同时绑定本人既有的起居厅c2.daily检视方法，使c2_navigation_local_ready在现场识别真实信息动作；不能凭未知meal_prepared给出true。

沃德草案观察可新增minimum_time=2250、when=c2daily_notes_window、navigation_room=central_hall、navigation_methods=[ward.c2.daily.inspect-central]的evidence目标。既有daily安装已提供这个现场检视并grant draft_notes，只增加本人查验动机，不预置结果。night_forbid仍须本人实际取得草案信息、听见阮的讨论并完成自己的讨论等原有条件；不能将一次看草案代替正式话语与传播。

这些修订改变目标新鲜度及行为资格，应作为独立行为修复批次记录，不能算原冻结04条件下的纯性能优化。没有修改生产或执行这些建议。
