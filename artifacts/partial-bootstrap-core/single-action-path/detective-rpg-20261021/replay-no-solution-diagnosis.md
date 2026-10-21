# 本轮重演没有完整解释：源码与封存证据诊断

2026-10-21。审查范围为隔离运行源码 `tmp/detective-rpg-integrated-20261021` 与 `perf-integrated-90s-x4-03/host-session/session.dvdag`，辅以该轮独立证据报告；未读设计文件，未运行会话、模型、构建或流程回归，未改生产源码，未向盲玩代理传递信息。下列源码行号均指该隔离副本，不指正在继续修改的主工作区。

结论：本次重演没有运行任何候选模型。它不是“模型搜索了但找不到完整解释”，也不是推演总预算耗尽。本人尚未取得问题及话题开放的正式收据，全部233个候选在本次状态下都未启用；本次只付费检查了15个因子约束，未形成剪枝证书。90秒窗口结束不能证明后续永远无解。可确认的狭窄机制问题是：空候选的通用replay仍准入，个人选择标签给它即时progress=1，于是发生了一次29游戏分钟的空重演。

## 来源与本次动作

独立重算20/30/40/50/60的SHA-256，全部与本轮source-before.json对应项目一致：
- 20-core.dv：39f22316e4ccbabf305c8730c33fae2b7f68c5f9bfe3a213fa098b2bb6a83be4
- 30-session.dv：5848ca5fe78fa9950d626c2bc98d596996c8f199a1f3c367e5f6f5f407889f7f
- 40-chapter1.dv：2c6daa50b9896dff956cefa2d0283581188c0c61b74bdb6e9df9cb00cb9ad0ae
- 50-chapter2.dv：f04a9f195d2d52b41360f5687ff29f9cfae097af7a34ee74f8e40af814eb75aa
- 60-game.dv：bf1a09d8146e3227f749dd5d8f93f2a0a8e18bcd9e1c3b098f5631bef5a61e4f

通过校验二进制DAG读取器只读展开必要节点，没有把原始轨迹另存JSON。final检查点下events含本轮事件；e1695/e1696在游戏524分钟选择detective.replay，e1883在553分钟正式applied，action147实际费用time=29。该轮独立审查报告核对了85.401s宿主送达与86.591s完整可见正文；这些现实边界不替代精确执行时刻。

## 已有观察与尚缺认识

实际成功与本人已知分开核对。本人最终cognition.state明确有以下true、无冲突、perception来源的细胞：lens_mine(action32)、slate_marks(action104)、pipe_layout(action113)、sound_trial(action127)、bolt_down(action131)、boots_toward_bolt(action133)、cloth_after_boots(action134)、fork_dent(action135)。因此不是现场动作成功却全部漏进本人认识。

| 已发生 | 正式来源 | 当前含义与边界 |
| --- | --- | --- |
| E7 | e1136，488分钟 | 侦探得到slate_marks；40:253–258对侦探不同时发slate_code/slate_person。看过石板不等于已经得到专业辨读。 |
| E8 | e1245，489分钟 | 得到pipe_layout。 |
| sound_trial | e1527，514分钟 | 得到sound_trial与ritual.sound_trial_done；40:552–561仅Su的行动grants包含heads_lost，侦探须通过真实证言链取得，不能据公共试验执行替代。 |
| E1、E3、E4、E5 | e1620/e1648/e1663/e1677，518/520/521/522分钟 | 分别得到bolt_down、boots_toward_bolt、cloth_after_boots、fork_dent。 |
| E2 | e1635，519分钟 | 本次action.grants=[]；尚未执行单独的compare.E2。40:1185–1188将实际厚度/擦痕比对与ash_wedge观察放在另一正式接触行动，不能把打开E2正文当比对完成。 |

本人本轮尚无slate_code、slate_person、heads_lost、boots_fit、recalled_bolt等相应细胞。这里仅列机制诊断所需的缺项，不构成给盲玩家的路线。40:1384–1415中的推理前提显式要求这些组合；例如pipe_layout+sound_trial仍少heads_lost，slate_marks+lens_mine仍少专业辨读项。若这些事实尚未通过本人来源得到，保留未知是正常边界，不能从作者物理世界补给本人。

## 本人认知与采用

e1884.factor_prune.actor_before是付费重演时的本人快照：cognition_blocked=false，reason_budget=320；本人35条active infer、171条active predict，其中c1_infer_0至c1_infer_19均active。没有发现“所有推理法则未采纳”或“认知预算封锁”的证据。

本轮侦探没有inference事件；reason_stop多次为steps=0、remaining=320、no-new-effective-conclusion。它与上述尚缺前提一致，不应只凭“零推理”就判求值器失效。final.hypothesis=false，last_readiness_report为current-public-question-unobserved与no-seriously-adopted-hypothesis；尚无可支持已完成解释的本人采纳。

## 候选为何全部未启用

在e1884的同一个actor_before快照，本人question.state只有awake_scope、memory_removed、not_injury，均来自实际听到demon发言的testimony。current、open_identity、open_voice_source、open_words、open_trust、outstanding_complete均没有本人细胞，故读取为未知。heard_topics仅own_material，不含本轮候选所需话题。

233个policy.hypotheses的available_when全部是对上述未知开放字段的equals true：open_identity 7个、open_voice_source 2个、open_words 2个、open_trust 6个、outstanding_complete 216个。30:1391–1420的topic还需本人当前问题或有真实heard_when支持的topic binding；30:6364等Complete候选循环又要求topic、本人的basis及available_when=true。40:2267–2275把216个历史组合的启用条件明确绑定outstanding_complete。

因此本次模型循环跳过全部候选。final.simulations=[]，整轮侦探没有模型执行/重查事件；不是已经模拟了233个模型而全部不一致。姓名介绍、角色声明、昨夜警铃正文与正式问题开放收据也不能混为同一件事。尚未见本窗口正式问题开放，不等于证明问题永远不会开放。

## 话题缺失的上游：尚未实际提问，不是已听到提问却漏收据

逐项读取demon的正式动作及侦探对应收闻，而非仅观察最终knowledge缺项：demon先在485–530分钟依次执行other.question.contract、other.question.astral、other.question.logistics；各15分钟，已实际applied。之后530分钟选择demon.opening，e1750在550分钟applied；侦探e1754收到待理解收闻，e1876在553分钟解析source_event=e1752。该literal仅含角色声明及awake_scope/memory_removed/not_injury，按源码不包含question.current/open字段。

接着reward-invitation e1782在551分钟、desire-offer e1819在552分钟、public-alarm e1865在553分钟applied；侦探分别在e1878/e1879/e1880解析实际收据。public-latch e1924在554分钟applied，侦探e1935解析其“此侧闩门、来者未归”的literal。40:4357–4365将public-alarm、public-latch与demon.question.prompt声明为不同正式动作，警铃/闩门背景说明不是问题动作本身。

本轮全部事件中无demon.question.prompt的action_selected或action_execution，也无侦探解析该正式提问的事件。物理世界末态question.current仍opening、stage=prompt，open_identity/open_voice_source/open_words/open_trust及outstanding_complete全部false；与本人尚无这些收据一致。因而没有证据支持“已经听见完整始业问题原文，却被解析器漏掉current/open”这一机制错误。所见只是窗口结束前尚未实际执行正式提问；上游调度是否符合故事节奏由主代理核对设计，不能由本报告替它补事实或改优先级。

## 算法预算与证书

e1884.factor_prune：expanded=15，remaining=305，work_remaining=305，certificates=[]。15个history.exclude.*均为unknown-or-not-contradicted，paid_units=1。没有任何剪枝证书，也没有一个被预算截断的模型搜索结果。

final仍model_budget=305、model_work_remaining=305、Replay_Depth=64、reason_budget=320。故不支持提高预算、放宽证书或改为近似答案的修复；本轮根本未进入候选模型求解。原始整数在DAG按对应值读取，未做费用或预算换算。

60:90–102的c1_player_replay_summary只是扫描本人可用、完整的explained模型；没有则返回通用“尚未形成完整解释”，只在model_budget<=0时另报耗尽。当前文案没有谎称总预算耗尽，但没有区分“无启用候选”“没有执行模型”“执行不完整”“确有矛盾”等原因，容易把本轮误读为求解失败。

## 行动规划与可确认机制问题

源码30:1749–1750的replay准入只检查model_budget>0及新attempt signature，没有检查存在本人启用候选。40:587–594的重演动作本人前提只有bolt_down；它又被装饰为personal_choice。30:1869–1870对带personal_choice的动作直接置progress=1。

证据e1696的replay计划value：information=0、verification=0、progress=1、private_utility=0、owner_utility.value=0、achievement=0、value=1；29分钟时间惩罚0.29后净分0.71，selected_path=[detective.replay]。stop=current-affordance-root、plan_expansions=1、completed_layers=1。虽然内容plan_depth=6、planning_limit原值保留，本次因根部已有正值动作停止扩层，不是旧版强制fast_approximation那一行仍生效，也没有证据证明本次因展开预算用尽而漏掉正确模型。

这建立了具体的工程浪费链：没有可启用候选→通用replay仍准入→个人选择标签给正即时分→执行并花29分钟→只做无证书约束检查→返回笼统未完成文案。它没有证明故事无解；修复它也不能保证下个90秒窗口出现正确解释。

最小修复建议：将现役Complete里的本人topic+basis+available_when候选资格提取成通用纯helper，由replay准入要求至少一个满足者；同一资格在Complete仍按当前本人状态重新核对。人工单选还保留manual_available_when及选中id的现有重核。扫描可按mind及完整外部topic/candidate输入保守缓存，避免每个模型再次扫描全表；不能在资格检查中实际运行模型或收费。保留费用、预算、证书、问题和线索前提，不预置问题、不授予缺失证据。

建议另在无候选/全被覆盖/无模型事件的结果中说明真实停止原因，而不是一律让玩家猜测算法为何无解。该投影改进不应改变行动资格或推理事实。

## 范围限制

没有新跑会话，也未求解候选或按设计判定答案。没有对完整流程可达性、后续协作、整局结局、性能提升或盲玩体验作结论。实际缺少观察、本人话题未开启、候选未启用、模型未执行与预算耗尽在本报告中分别列明；这次唯一直接确认的修复对象是“空候选仍付费执行重演”。
