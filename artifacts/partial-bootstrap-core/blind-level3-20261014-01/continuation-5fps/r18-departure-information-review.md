# R18 离场认知链独立静态审查

日期：2026-10-15。只读现役离场声明、判答公共观察、第二章主体/目标刷新和规划证据新鲜度路径。未读取设计、私有规划或本轮原始事件文件，未修改源码、运行会话或构建。主代理提供的 e23/480 等实测事实作为问题背景，本报告不重新测量它们，也不据源码宣称任何释放或跨门已发生。

## 静态结论

存在明确的正常释放认知阻塞风险：物理 solved 字段可以已更新为 true，而本人仍保留早期 false；正式释放前提要求本人认识中的全部 solved 为 true。现役虽有合法的重新检视动作，但固定的新鲜度目标可能不再给它任何信息价值，不能把“有可调用动作”解释为“正式主体会再次选择”。

## 已核对的路径

- c1dep_complete 是四个 solved 字段为真的合取，release-outer 将该条件同时作为主体前提与权威前提。只在物理世界完成不会自动满足主体前提。
- demon.departure.inspect-accepted 是 repeatable 的正式观察，声明 grants 为这些 solved 字段；其本人地点条件使用端口 hall 表示。成功执行后可读取当时真实值，不存在“只能观察一次”的资格限制。
- c1dep_goals 在安装时以 actor.mind.time 建立 evidence minimum_time，存入 policy.departure_goals。c2d_refresh_actor_goals 只把缓存目标加入 combined，没有重新计算这些时间界限。
- dvp_needs_evidence 以匹配来源、匹配当前 cell.value、时间不早于 minimum_time 判断已满足。早期取得的 false 也可满足这个证据要求；不会因为物理值已经改变而自动知道需要复查。
- dvp_action_value 只对未知/有争议或仍需新证据的相关字段增加检视信息价值。因此已有满足旧时间要求的确定 false，会让该检视失去这部分价值。
- c1_question_accept 及 runner 的正式接受更新写入物理 solved/completion_ref；个人 accepted_answers 只记录答题者本人的接受记录，不等于给其他主体 solved 观察。
- 现役基础正式回执 grants 为 current/stage；协议坐标包装添加 completion_ref，但没有把它自动加入 accepted_public_grants。重新询问只观察公开问题及 open 标志，不包含 solved。没有找到其他生产推知/话语观察将这四个 solved 真值提供给该主体的路径。扩章继承世界与认知，未发现把真实 question 字段直接灌入其认知的路径。

## 公共回执的另一个接线边界

runner 的接受公共观察使用 channel=formal-receipt，并仍调用 dvs_speech_hearing。离场给 demon 安装的端口接收 grants 使用空频道或 copper.living；其 body location_ref 指向外侧 living_hall，而答题者在 hall。源码中没有找到 formal-receipt 的频道授权。

因此不能假定 current/stage 的公共回执已经被该主体实际收到。即使仅给 accepted_public_grants 追加 solved 字段，仍可能被相同的接收条件阻止。任何修复必须保留真实接收边界，不得以增加名单、跳过 hearing 或直接读全局事件结果作为已收闻。

## 最小修复方向的取舍

**沿用现有重新检视通路**：刷新离场观察目标的新鲜度，使本人有实际根据重新查看已认定状态，是较窄的机制选择。新鲜度需来自本人实际获得的新时刻或相关变化依据，不能偷看 physical.solved 后决定给检查加分。仅重新调用 c1dep_goals(actor) 也未必足够：若 actor.mind.time 本身没有更新，minimum_time 仍是旧值。无条件每回合提高界限又可能造成反复零时间复查，须明确刷新/满足边界。

**沿用正式接受回执通路**：若内容合同支持该主体作为真实接收者，则可在正式接受后的公开回执中声明相应 completion_ref，并完整接上已有端口的实际传播/接收授权。只在真实接受后读取结果，失败不授予；每次只提供该事件实际公开的结果，不提供未来阶段真值。此方向因果更直接，但同时涉及回执内容与频道合同，不能称为只改目标刷新，也不能省略来源核对。

两者均不能直接把全局完成状态当成本人已知。应先选定唯一清晰的获取合同，不用两套无边界的轮询和广播掩盖接线问题。本审查不代替内容依据裁定，也不要求改变判答条件、预算、释放权威条件或强定行动路线。

## 新会话最低证据

分别记录正式接受事件、该主体实际取得 solved 新值的观察/接收来源、随后主体前提成立、实际释放法则执行、玩家观察到释放，以及真实跨门。源码中的可达路径、内容可用或宿主交付都不能替代这些实际节点。
