# 收尾合同独立只读审查（2026-10-18）

审查现行 chapter2 daily/content/dialogue、game entry/debug-run 及其调用的正式 Complete、环境推进、观察、目标与等待代码。未读取设计文件、未运行会话或构建，未修改收尾源码。

## 结论

未发现观察授权发生在执行之前或把截止等同全签的静态错误。c2d_action 仅把 observation_grants 合入行动者 grants；Complete 先执行权威行动，再结算环境过程，只有 applied 分支读取 grants。签名后的 council_closed 是该时刻实际环境值，不在预测中写入收束；公开奖励与听闻名单不传播该 observation_grants。

全签由全部 signed.* 的真实值触发 council_ended 与 council_closed；6330 截止只写 council_closed。后续信号读取 closed。正文分别要求 closed+ended 和 closed+not-ended，未知值不能满足任一确定结果分支。全签要求所有声明人物，未把死亡或缺席算作已签；此项设计是否合适留给主代理根据授权判断。

closing 检视真实位于 central_hall/oath_court，并只授权现场读取。其相关字段在 physical 中存在，ballot 初值保留 unknown。两条个人 evidence 目标不会赋予事实：closed 目标在6330激活且要求该时刻以后证据；ended 目标须本人已经知道 closed。未来目标可生成等待机会，不自动宣称签名齐备。

摘要仅在第二章且 physical.closed 确为true时出现，明确 scope=read-only-actual-state-not-character-knowledge；各项保留完整 cell，因此缺失引用、未知 ballot 不会降为false或零。摘要未输入人物认知或规划。debug 停止原因继续独立，narrative_completion 为阅读收束记录的提示，未据动作上限或停止推断正式结局。Snapshot返回系统摘要，但当前未发现Godot消费者把它当作本人已知内容。

## 未决工程风险

静态地图可达不等于主体能够自主完成收尾检视。closing 方法仅在上述两处，而新增目标只有 evidence，未声明导航分段目标；第二章规划深度为2。quiet_room 到central_hall需要2次移动再检视，outdoor_alley需要3次移动再检视，超出一次depth2可见收益范围；closed相关evidence也不走communicate专用的access机会分。其他未完成目标可能把人物带回会场，但源码本身不保证。请正式逻辑轨迹记录各人的实际地点、本人closed证据和首次收尾检视；不能仅用系统摘要存在宣称玩家看到收尾。该项没有安全的纯工程局部修补，未擅自增加地点目标或剧情。

不评价内容是否满足完整设计，也不以本次审查替代任何级别验收。
