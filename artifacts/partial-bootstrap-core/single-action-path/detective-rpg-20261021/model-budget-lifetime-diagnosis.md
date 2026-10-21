# 04模型预算耗尽后的恢复与再重演：只读合同核查

2026-10-21。核查根工程README、Session/README、AGENTS、20–60源码及04已封存DAG；未读设计、未改任何源码/文档配置、未运行会话或模型。06保持冻结。这里“恢复”专指同一正式会话的预算补回，不把新开游戏当恢复同一人物状态。

结论：现实现中model_budget是主体累计模型工作余额，随存档和扩章继承；每次付费replay只重置本次model_work_remaining上限，不补回model_budget。原生生产闭包未找到使该余额增加的合法行动、时间规则、换日或新知识刷新入口。余额为0时，即使钱包充足、本人后来得到新证据、attempt签名已改变，也不能再次准入正式replay。这是需主代理提交用户设计裁定的现行事实，不是自行恢复、加数字或设定休息机制的授权。

## 四个不同账目

| 项 | 当前来源与更新 | 耗尽后含义 |
| --- | --- | --- |
| policy.model_budget | 40-chapter1.dv:688初始320；30的模型执行/重查/因子证明/有付费话语预测从同一余额扣除。不是每次动作重设。 | replay本人准入直接不成立；付费工作核也返回0可用工作。 |
| policy.model_work_remaining | 成功replay Complete开始时按本action成本模型work_limit设置；每次模型工作继续扣除。dvs_sim_work_budget取min(model_budget,model_work_remaining)。 | 只限本次调用工作的上限；新replay即使重设它也不能突破总余额0。 |
| actor.ledger.balances(time/computation/stamina) | 正式Prepare/Complete的报价、预留、单次结算钱包。replay内容报价time=29，另有原computation费用。 | 钱包能否支付是另一门，钱包多不会补充model_budget。 |
| plan_budget/planning_limit | 每轮Prepare由原planning_limit恢复本次行动树展开额度，dependency_budget另列。 | 这是行动选择树，不能借其每轮恢复行为推定历史重演也每轮恢复。 |

当前根30-session.dv:3780–3787的dvs_sim_work_budget明确分别要求总量与方法上限非负，取两者最小；6395只给model_work_remaining设置dv_find(model,work_limit,0)，6405/6433把实际剩余总量写回policy.model_budget。40:1030为replay声明work_limit=320；该work_limit与初始320数值相同，工程身份却不同。

20-core.dv:2368起的dv_action_cost_quote/admit/settle核对ledger/world revision、预算边界、已结算事件与钱包，只按该动作费用预留/退款/扣费；它没有把钱包中的computation换算成policy.model_budget的规则。新报价会发生在每个已准入的行动正式到期分派时；它不是为被本人资格gate拒掉的replay开设预算补充。

## 04的实际扣减链

04 e2843在599分钟完成首个真实replay，钱包当时仍有time=99881、computation=99957、stamina=100000；不是钱包付不起。

随后同次Complete：
- e2844 factor_prune从model_budget=320扣27，remaining/work_remaining=293。
- e2845 me消耗1，剩292，status unknown、plan_complete=false。
- history-case-1的切片预检e2846消耗12，随后完整模型e2847消耗89，剩191；完整模型status unknown、plan_complete=true。
- history-case-3切片e2848消耗12，完整模型e2849消耗89，剩90；完整模型inconsistent。
- history-case-5切片e2850消耗12，完整模型e2851消耗78至0；完整模型unknown、plan_complete=false。

合计320个模型工作单元：27+1+12+89+12+89+12+78。切片预检中status=explained不等于完整历史解释已成立；dvs_model对未反驳的预检继续跑完整模型，相关源码根30:4925–4958。上述事件的两个阶段没有被混为两份完整成功。

最终仍model_budget=0、model_work_remaining=0，simulations保留4项；钱包仍time=99611、computation=99829、stamina=100000。其间未再有真实replay。e2865等model_argument_unavailable.result.remaining=300，后来299，是论证/认知另一预算的结果，不可当作模型余额恢复；final.reason_budget=299与此吻合。

## 话语预测是否同池

根30:3022的dvs_prepare_speech_choices从actor.policy.model_budget读取remaining，在一整个候选层报价所需units；不足则unknown-incomplete-layer、charged_units=0；足够才执行预测、扣减，3127把remaining写回同一model_budget。模型切片、重查和因子证书同样写回该字段。故机制上它们共享累计余额，不是彼此独立的钱包。

但本轮e2844.actor_before.model_budget仍320；首个replay之前列出的speaker_prediction是unknown-no-paid-preselection，不能据事件名就声称本轮话语预测已经消耗了侦探的320。04耗尽链由上述付费replay内部工作足以解释。

## 找到与未找到的恢复路径

生产20–60全量搜索model_budget读写：明确初始化为320、部分非侦探/disabled配置设0，之后都是从当前余额扣减或把求值器remaining写回；未找到正向增加、按本人revision重置、按游戏时间/休息/换日刷新、从ledger钱包兑换等正式路径。60的配置/扩章也没有重建已有主体的model_budget。根AGENTS要求继承原主体/配额余额/账本；Session.RestoreFile直接校验读取已保存状态并构造会话，不再调用初始化预算；恢复存档不能恢复成旧初始余额冒充同一连续状态。

新开dvs_session_open会使用新内容初始policy，这是新会话而非合法补充当前预算。外部改JSON、直接改actor.policy、重开游戏或用旧存档覆盖当前历史，不在本任务授权范围，也不是现有世界内恢复入口。

工程README/Session README没有承诺休息或花29分钟可补模型余额。Session/README:34与language_core/README:91说“本人认识变化后允许重新核对”，针对候选/尝试签名隔离和知识版本，不写预算豁免；AGENTS:99明确行动树预算与付费重演余额分开。工程文档在“新证据仍需正模型余额”上容易被误读，可在用户裁定后进一步明确，但本轮未修改冻结文档。

## 是否阻碍冲突补查后再次正式重演

会。04源码30:1750、当前根源码约1800的replay资格都先要求0<actor.policy.model_budget，再核本人启用候选与attempt签名。新观察能改变签名、重开某个候选资格，却不能使0<0成立。dvs_manual_action虽然允许人工选择一个本人有效hypothesis_id，最后仍走共同dvp_eligible；它也没有单独补预算权。

旧模型的重新比对也不是免费入口：dvs_model_recheck的比较展开从总model_budget扣除，且在正式replay内部受dvs_sim_work_budget门控制。新的证据若需要正式重演来生成新的完整解释/付费来源，当前累计余额耗尽确实会阻断；不能用旧模型、未完整的切片或UI重读替代。

这不等于04的第三问因此必然无法接受。第三问也有本人已经学得words_bolt、尚缺score_partial的直接证据行动链；其是否仍可不经新历史重演完成，需按正式准入/判答和新窗口证据分别判断，不能把预算0作为所有后续失败的唯一原因。循环诊断中可确认的重复任务评分问题也与此分开。

## 待用户设计裁定的事实表述

“当前模型工作是每名主体在整段连续会话内的累计资源；正式replay同时受该余额和单次work_limit限制，支付行动费用只购买本次执行，不补累计模型余额。一次早期replay可能耗尽它，此后补查新证据也不能再次正式重演。工程目前没有预算恢复或钱包兑换机制。”

应由主代理核对授权设计并请用户裁定这一长期资源合同是否符合预期；若需要改变，应明确资源身份、取得来源、适用范围、费用、主体同等规则及证据记录，再单列新基线实施。不得先自动重置、提高预算、杜撰休息/充值数字或放宽完整解释标准。