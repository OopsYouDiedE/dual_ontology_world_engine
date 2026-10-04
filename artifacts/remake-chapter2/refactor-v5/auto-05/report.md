# auto-05 独立定向回归

本轮未读设计文件或历史报告；生产源码未修改。两个项目构建均成功，Prototype 2警告、Godot宿主25警告、0错误。仅使用现有第二章测试及新测试副本。

## 第一项真实失败

03-notes.png：笔记本仅有标题、空白内容区和返回按钮。同轮 godot-visible-output.txt 与 cli-output.txt 均已有十五个人的笔记内容。全部11张截图均已通过 view_image 查看；该UI失败出现后停止新增依赖测试，等待主代理修复。06b与06c对照面板还可见重复的“吻合/未查”前缀和重复文本。

## 导航事件时序诊断

原始回归退出4，保存在 original-timing-exit4.log。一帧释放版本同样退出4，保存在 one-release-frame-exit4.log。后者明确记录：释放后 physical_d=true；field_active=true；busy=false；modal为空；ui_lock=false；dialogue=false；Input.get_vector=(0,0)；slide碰撞为空；第二次approach路径正确生成。下一轮物理D仍按下导致取消路径。失败末尾D已false、角色停在(424,744)、路径及pending均空。

等待真实physical_d=false后，导航到达断言通过（settled-release-pass.log）。最终测试采用全新release InputEventKey且等待process_frame/实际释放后再接近；最终godot.log退出0。没有绕过到达断言；生产代码无改动。原导航失败属于测试输入队列时序，不能证明生产导航缺陷。

## 自动回归通过项

最终37条命令、231输出项、ended=true。WASD取消接近、到达后打开交互、未变化假设保持有效、改动假设结果过期、跨夜清除旧重演结果均通过。replay.json共5帧。

同一输入在CLI执行，删除命令回显与空行后：Godot231行、CLI231行，逐行完全一致；comparison.json差异为空。当前源码输出不是275行，没有将行数变化作为一致性失败。

## 新反馈检查范围与限制

源码确认 ConfrontPrompt 仅打印 DetHasEvidence=true 的证物，并在未取得证物时拒绝配对；帮过者Negotiate采用“帮助不保证答应每个请求”提示；Compact先Distinct去掉完全相同行；Council在多数开放但玩家支持封闭时记录侦探留在门外，走else分支才念匣标签。此处为源码检查，未完成单独动态情形验证。

5个replay frames的text、ink以及CLI重演文本仍需专项字词断言；整轮231行已一致。

## CLI driver

新会话auto05-cli-driver用start保留exec会话，send帮助完整返回至下一个时段提示，ready从1增为2；无input-ready标记泄漏；后续重演命令都在同一次send完整返回，无下一条输入补读。此会话尚未得到长假设列表（玩家该夜未守夜，返回知道太少），因首个UI失败已停止下游，不能标为长重演列表协议验收通过。stop精确终止记录PID并立即返回，会话输入与玩家可见输出保留在timeline.jsonl。

本轮为自动回归，不构成独立盲玩、有效体验时长或可玩体验验收。
