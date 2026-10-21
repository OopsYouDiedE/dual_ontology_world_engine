# core/session 修正实施记录

2026-10-21，隔离基线完成后按主代理授权落地；未构建、未运行语法检查、探针或会话，避免与基线性能测量争用。

修改范围：
- language_core/30-session.dv：落地 manual-contact-retry-candidate.patch；一次成功限制读取 completed_actions，used/费用/配额保持；仅真实Godot人工接触失败或完整job证明的运动受阻生成按行动存储的重试控制凭据；自主相同签名失败不重试。helper只分类回执，不修改mind或观察。后续同动作Complete覆盖凭据，成功清除。
- 删除Prepare对所有非人工主体无条件设置fast_approximation/plan_depth=1/plan_budget=32/lookahead_limit=4/positive_move_root_stop的单行，保留原planning_limit及独立idle_proof逻辑。未另设新配额，实际执行恢复内容已有声明。这属于控制/规划条件修正，不作同条件速度结论。
- language_core/AGENTS.md、README.md：各新增“现场失败与成功限制”“规划声明”两段。未修改故事及结局。

静态审读：复读helper、eligible_base、Prepare和Complete新段，确认receipt rejected才产生凭据，DV自身拒绝不会产生；人工临时manual_input才绕过拒绝签名；成功once限制仍在，现有inspection/answer/replay签名仍保留；used与预算扣减原语句未改。与manual_replay代理先行划定边界，未写其人工入口或付费重演主体分支。该代理随后会替换replay自身签名两行，须由主代理最终整合差异。

保留限制：早期无路线、缺站点等回执未含完整job证明时仍不释放人工重复尝试。旧版缺completed_actions的存档兼容需要主代理依据现行恢复合同决定，不能假造历史成功。此阶段只有静态审读，编译、保存/恢复、人工重试、自主防循环及行为效果均待集中验证。