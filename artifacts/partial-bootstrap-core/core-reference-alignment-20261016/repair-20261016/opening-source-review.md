# 初始化与开场接线静态审查（2026-10-16）

本记录只涉及 chapter1-departure.dv、chapter1-content.dv、chapter1-opening.dv、chapter1-other-groups.dv。未读取设计文档；内容取舍由主代理依据用户授权审查。本子任务未运行构建、编译、会话或测试，未提交，未触及 music 修改。

## 改动

- c1dep_unify_demon_position 先使用 dv_record_like 区分记录；记录只递归字段值，保留键和顺序，普通列表逐元素递归。equals 改写前检查 ref、value 存在及 ref 为记录，使用缺省读取核对 object/facet/field；只替换 demon.state.location == hall。纯值原样返回。
- departure 同步统一 actor.policy 中的魔王地点前提。原实现仅转换 actions/mind/hearing，沟通目标仍可能要求本人在旧 hall，与实际 living_hall 不一致。
- c1_answer_actions 所有返回动作调用既有 c1_public_answer_speech。c1_public_method 也已有此包装：它以 dv_with_many 替换 speech/listeners，不追加发言或回执，重复包装不产生重复话语。game/player-content.dv 只读 answer_id 生成界面标签，没有第二个公开答辩包装，本子任务未改该文件。现有包装只对已声明的相反公开结论建立 public_claim，其余答辩保持原行为。
- 六段广播分别要求上一段实际成功完成：opening → reward-invitation → desire-offer → public-alarm → public-latch → question.prompt。声明为 completed_action_requirement: {action_id: 上一段}。通用资格与成功账本由其他子任务接入，不能将入队或 actor.used 当成功。
- 首段改为魔王本人实际收到并理解 shen.opening.body-habit 后才有资格。此话语依据沈鼎实际送茶留下的 served_by；苏泠询糖与沈鼎回复保留原请求机制及自身沟通目标，并未新设必须询答的顺序。铜管听觉仍按 departure 的发送/接收房间及实际传播处理；没有按时钟生成广播、预置收闻或代选发言。
- 默认乙区不修改茶叙的侦探、霍铎、苏泠在场前提。跨区配置仅调整留在乙区三人的开场介绍、问候、记忆交流及送茶机会：须霍铎、沈鼎、苏泠实际同处 hall；不依赖已被移走的侦探发言/出现。仍保留原行动、法则、备茶前提、苏泠自身口味、实际送茶观察与询答传播。

## 来源与机会复核

既有 c1_op_meet_candidates、c1_op_tea_candidates 继续携带 s2_meet / s2_tea 的 source_refs。body-habit 仅为对本人身体习惯的陈述，未改为恢复熟识记忆。首条广播不把该习惯当答案或侦探知识，而只以真实听到的开场互动作为开口机会。奖励邀请、欲望说明、公共历史及问题仍各自按原字面内容支持、物理执行和实际传播取得资格。完成前段只解锁后段，不增加分数或强制主体选择。

## 限制与待主代理复核

- 本记录只证明源码接线范围；尚无本轮运行证据，不能断言开场一定发生、人物一定发言或章节完成。
- 首段以茶叙末端真实收闻作为严格机会；主体可不选择相应互动，因此停滞必须由新正式轨迹判断，不能通过放宽条件补出结果。
- 跨区仍沿用原送茶法则中的四只杯子对象；没有移动侦探或替其喝茶/回应。杯子效果与缺席身体是否符合设计由主代理判断，本子任务没有补写剧情。
- departure 原本为魔王远端发言移除同处机会，替以真实铜管端点；本轮保留该接线并增加真实茶叙收闻门槛。其余远端发言机会仍需主代理结合通用资格与实际轨迹审查。
- 完成前提依赖新的成功账本，需核对成功 Complete 才写、失败/未执行不写，存档/恢复与规划缓存含该字段。该机制由 repair_core_gates 与 runner 负责人维护。
- 建议同步 language_core/core/README.md、language_core/AGENTS.md、verification/AGENTS.md / DESIGN.md 中通用资格字段与成功完成账本合同；AGENTS 不应写入具体人物或剧情结论。纯内容接线与运行证据分别维护。
