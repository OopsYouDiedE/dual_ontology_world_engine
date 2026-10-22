# 第四夜真实值守链静态审查

基线：dcd52f75；独立分支 codex/guard-chain-review。仅审读工程源码，未读取设计文件；未运行正式会话、Godot 或 Core AI。

## 修改与原因

`c2_safety_goals` 原有值守位置目标只有 guard_active=true 后才成立，且未声明既有 maintained-presence 导航语义。于是本人真实承诺之后、实际 take-post 之前，旧读数目标仍可建立新的离厅导航收益。现在本人已承诺或已就位时，位置目标均采用既有 maintain_presence：返厅路线优先，已经在厅内时不再为其他任务创建独立到达收益。仍保留所有实质行动的共同选择；不禁止移动、不强设守门、不加预算或收益。

`c2_guard_post_law` 的正式与预测前提补齐本人 guard_commitment=true，与 take-post 行动原有本人前提一致，避免权威方法绕过承诺。实际 guard_active 仍只来自正式 take-post Complete，离开 living_hall 的原移动法则仍撤销实际值守；release-post 撤销值守和承诺。

## 已核对的接线

- undertake 经本人安全信息与价值比较进入候选；正式说话法则写 commitment，行动 grants 才在 Complete 后供本人取得。
- take-post 的 predict_law 由 c2_actions 统一添加；c2_safety_install 为同一方法安装 predict/enforce 采纳，未缺预测接线。
- c2daily_watch_law 只读真实在厅、存活且 guard_active 的实体，以及莫兰真实在厅位置；承诺本身不产生 watched。
- 莫兰必须通过本地 inspect.current-watch-living/current-watch 的实际观察取得 watched；守门结果未注入本人认识。
- caution-moran 仍要求本人收到色层言语或实际安全信息，并通过真实同房说话给莫兰；没有按 profile 写结果。

## 边界与待验

既有 watched 权威规则表示至少一位真实守望者，未擅改为双人门槛。双人真实值守必须在后续正式记录分别出现两人的 take-post Complete，不能用 watched=true 替代。该补丁不能证明 Standard 已形成双人值守、莫兰采纳劝阻，或 Foolish 的实际失败对比；这些由主代理授权的新会话核对。重复采样刷新和 B 重复自检由其他分支负责，本补丁不触及。未修改医化、仓储、星象、救援、人物价值顺序或任何 profile 分支。

工程验证：`python mcp/editor_lsp/check_dv.py --format json dv_core/modules/50-chapter2.dv` 返回 success=true、diagnostics=[]，范围为 syntax/names/calls/implemented type constraints；内容未执行。
