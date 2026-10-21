# DV core/session 与内容接线静态审读

日期：2026-10-21。分支：codex/detective-rpg-20261021；HEAD：c27d0b1eedbb02dd2bfb28249df84eea17c4c412。工作区已有大量修改，本报告审读工作区源码，不把问题归因于本轮新增。

范围：已读根及 language_core/AGENTS.md、工程 README，以及 20/30/40/50/60 相关实现；未读 DESIGN、设计规范或旧设计。未改源码、未构建、未运行游戏或流程回归。以下是源码可证的机制与接线结论，不是游戏体验验收，也不自行决定故事走向。

## 1. P1：一次失败会永久消费一次性关键检视

位置：30-session.dv:1686、1690、1729–1735、6365–6371；40-chapter1.dv:98、225–245。

证据链：Complete 仅在 applied 时追加 completed_actions（6370），但任何执行结果都会追加 used（6371）。eligible_base 的 once 与默认非重复动作都用 used 排除。第一章 E5、E6 等关键检视通过 c1_once 或“仅一次 true”声明。因此一次原生现场接触失败、资源/站点失败或权威执行失败，就会永久消费这些行动；即使本人后来重新走位或取得改变资格的真实信息，也仍被 used 拦截。失败没有发放正常 grants，故可能留下永远不能取得的本人证据。

最小修复边界：保留 used 为包含失败尝试的历史，保留每次尝试的费用和预算；一次成功限制使用 completed_actions。不要全局删除 rejected_attempt_signatures：自主主体仍以相同本人输入签名抑制重复失败。

人工重试还需要第二层修复：rejected_signature（1668）只含本人前提、采用前提、逻辑地点、可见共处；同房间局部走位不进 DV，故实际几何改变不会改变它。单改 completed_actions 不足以恢复人工重试。现有 player_actor 与临时 manual_input 可划定人工控制边界；建议仅对上次明确属于现场接触/到站失败的行动，允许显式人工再次点击在临时视图中绕过拒绝签名，仍重核全部本人资格、正式报价、执行与 Complete。须记录失败阶段/种类，不能把权威前提拒绝也无条件放开；不要把 manual_input 写回自主主体，更不要每次调度自动清除签名。现有 receipt.executor/outcome 可以区分执行器及结果，但需核对宿主是否提供足够明确的接触失败原因。

## 2. P1：所有自主主体的行动规划被无条件降为一层

位置：30-session.dv:6090–6092、2076–2079；40-chapter1.dv:744、5387–5393；50-chapter2.dv:1968。

证据链：prepare_actor 对 actor.id != player_actor 的全部主体覆盖 fast_approximation=true、plan_depth=1、plan_budget=32、lookahead_limit=4、positive_move_root_stop=true。formal-model 未设人工玩家时，也包括侦探。dvp_plan 再因 fast_approximation 把 depth_limit 设为 1。Standard_Detective 声明的 plan_depth=6、第二章的 plan_depth=2 均无法决定实际行动树。

影响：只能比较根动作即时分；有赖“先做低即时价值动作，再得到有意义后继”的决策不能由现役行动树发现。内容另设导航/等待目标可以产生动作，但那不等于多步行动规划。此结论不涉及 Replay_Depth 的付费历史重演能力，二者不能混写。

候选改法：把该近似路径变为显式、已记录的控制/运行配置，而非所有非人工主体的默认强制覆盖。恢复声明层数时维持本轮既定展开预算上限与预算前缀，不自动把 32 提升到 320；预算不足如实返回 incomplete-layer-budget，并保存 root/successor/stop 证据。可先取消强制深度与 fast_approximation，仍限制预算32；其有效性必须由新窗口核对，不能承诺32足够完成任何既定深度。该代码是已有未提交工作，建议由主代理结合当前授权决定，不直接回退。

## 3. P1 实现缺口：归乡协商没有接到实际离岛，部分必要条件没有产生路径

位置：50-chapter2.dv:4424–4438、4493–4494、4610、4795。

证据：c2h_rows 明注离岛需要另行实现的执行合同；departed 初始化 false，全生产闭包搜索仅见初始化和 summary 读取，未见变为 true 的效果。safety_verified、care_continuity_verified、outside_promise.quer/elena 均初始化 unknown，搜索未见正式写入来源，但直接作为多人 consent 的前提。因此在当前闭包内，这些人的同意前提不可成立，且即使协商成立也无离岛执行。

同类接线：50:4761 明报 exile_execution=not-implemented；exiled 在205初始化 false，1035的 veronica dead-hand 法则却以其为必要前提，生产闭包未见写 true。议案通过、配给剥夺的决定与实际执行不可混同。

这不是要求自动制造安全事实、许诺或预设结局。主代理必须核对授权设计：若这些属于当前交付目标，需补真实行动/来源/执行；若当前只交付协商，应在用户可见范围明确未实施，不能称多种结局已可达。

## 4. P2：人工推演存在，但入口与投影不足以支持可操作的假设/证据对质

位置：40-chapter1.dv:587–594、820；30-session.dv:6290附近 replay Complete 分支、6750–6777；60-game.dv:307–350；Session/Session.cs:77–82。

已接线：人工可通过普通 action id 选择 detective.replay；它走正式费用及 Complete，后者循环本人 policy.hypotheses 批量生成模型。dvg_replay 仅读取该本人已付费、已记录的 simulations，不在 UI 读取时运行模型。不能将此入口说成不存在，也不能据它声称已完成玩家自主构造推演。

缺口：SelectPlayer/Prepare 只接收一个 action id，无当前假设、选择证据、预测后继或对质对象参数。dvg_replay 的每行只有假设标题、状态、比较数量、矛盾数量与来源事件；未投影实际 comparison/contradiction 命题、观测值或本人证据。conflicts 与 predictions 字段恒为 []。因此玩家无法从该工程接口选择某一假设来重演，也无法在该投影中核对某条证据怎样支持/反驳假设。

修复建议：先投影本人已有付费记录中的具体命题、本人观察、比较结果与来源，并明确未知/过期/不完整；若用户确实要求可选假设与证据输入，需独立定义动作合同与本人可知资格，继续走同一正式链。不得把所有作者候选或权威答案直接暴露为玩家事实。UI 是否还有其他人工笔记功能，需由宿主审读补充；本报告只论上述引擎接口。

## 5. 条件性风险：内容目标刷新早于本轮收闻理解与认知

位置：60-game.dv:529–532；30-session.dv:6098–6104、6121、6005、6036；50-chapter2.dv:4204–4206。

目标刷新先执行，随后 prepare_actor 才 drain_received_speech、notice_peers、reason。c2d_goals 对尚未满足的 received_speech_requirement 直接把 available_when 构造为常假；如果该要求正是在随后 drain 中满足，本轮选择仍拿着刷新前常假的目标。decision_revision 又记为认知后的版本；若此时无动作、无排队、无其他真实改变，同刻不会因此重选，最后可能按 idle 停止而非刷新新目标。

这是由具体时序推出的风险；尚未构造正式会话证明现实内容中确实触发。建议统一“本轮认知完成→按本人新状态刷新内容动作与目标→选择”边界，或在确认内容刷新输入变化后显式重新准备选择，防止重复认知/重复计费。不要为解决此问题无条件重跑整个脑循环或自唤醒。

## 未确认项与限制

未确认具体认知泄漏；未实际测量严重等待或整局循环；未确认各剧情节点的实际可达/不可达时刻。没有用静态候选、文本声明或本报告替代正式游戏效果。上述工程修改建议均尚未实施。

审读时源码 SHA-256：
- 20-core.dv：39F22316E4CCBABF305C8730C33FAE2B7F68C5F9BFE3A213FA098B2BB6A83BE4
- 30-session.dv：778498AAEDB466BB2171DC7215F3AFEA240F261C94FB48440408D229E75355CC
- 40-chapter1.dv：ADAFB76192805F882245865AC52CEAD8CF5C22E966BF85777254C341FC47C630
- 50-chapter2.dv：F04A9F195D2D52B41360F5687FF29F9CFAE097AF7A34EE74F8E40AF814EB75AA
- 60-game.dv：CB218DCA88B168779527C11D7A3C46727F280209B6405A3C94B712CA087B18B7