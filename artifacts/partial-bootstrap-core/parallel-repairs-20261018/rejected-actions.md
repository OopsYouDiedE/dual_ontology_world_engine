# 被拒动作的重试与现场认知

本轮只修改 `session/15-action-planning.dv` 和 `session/30-runner.dv`。未读取设计规范、未运行会话、构建或流程回归，未提交。

## 静态发现与修改

现役 `dvp_eligible_base` 的 repeatable 分支直接返回 true，Complete 原先仅记录通用 attempts 修订号；失败动作没有自己的语义屏蔽键。新增 `rejected_attempt_signatures`，在所有资格分支前检查，对非 applied 的真实 Complete 保存失败后本人输入签名；成功执行清除此动作旧失败键。预算与费用照常结算。

签名只读取传入的本人 mind，包括动作前提、本人已采纳的对应 predict 法则及其前提、本人位置。等值条件保留 known/value/conflicted；数值阈值保留条件真值，避免时钟每次前进就再次唤起相同失败。签名不读取 authority_precondition、physical、全局 revision、来源事件或观察时间，也不使用无关目标引用。规划预测节点仍可按预测中的相关输入变化重新比较，真正执行仍由原权威检查决定。

失败观察新增显式内容字段 `failure_grants` 和 `failure_observe_when`。只有 executor=formal-model、宿主回执 outcome=applied、DV执行非 applied、现场观察条件为真时，经原 dv_perceive 的绑定与值投影产生来源正常的观察。缺条件默认为假。失败不继承成功 grants，不从权威前提提取隐藏字段，也不生成答案。observation事件记录实际使用的授权列表。内容代理为第二章移动声明起点同处条件及对应 gate 的 access，未授权远端原因。

独立审查后收紧原实现：原Godot成功回执证明几何终点到达目标房间（Main.gd现场校验及WorldView.gd的room/actual_position/arrival_error），而DV权威拒绝保留逻辑起点，两者不能混用。当前动作只声明逻辑起点条件，没有将失败观察授权现场与原点execution_room对应的独立证明合同。本轮因此不授予任何native/godot失败观察，保留后续正常inspect与失败签名屏蔽；formal-model没有独立几何位置，仍可按逻辑现场检查。未修改Godot，也未将其目标房间回执解释为起点接触。

## 审读与限制

人工核对函数声明、现役调用位置、adoption.law.premise字段、默认条件和失败后签名保存顺序。差异空白检查未在本代理文件发现问题；共享工作区其他文件有既有/并行修改，未代为调整。

尚未验证动态分支、编译器接线、实际停止行为、Godot失败回执与UI。没有声称6221历史停滞的唯一因果，也没有作耗时或游戏完成结论。内容授权可见性的设计判断由主代理负责。

## 公共文档建议

在Core参考、verification DESIGN及相应AGENTS补充签名辅助函数、actor.rejected_attempt_signatures及action.failure_grants/failure_observe_when合同。强调数字阈值按本人真值失效、失败观察仅来自formal-model显式现场授权，native当前不授权失败观察，成功grants仍仅在成功Complete后生效。
