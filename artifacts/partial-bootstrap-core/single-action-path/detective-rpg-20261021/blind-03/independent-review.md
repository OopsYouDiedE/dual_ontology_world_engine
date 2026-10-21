# blind-03 独立证据审查

审查日期：2026-10-21。审查者为本轮结束后新建的独立上下文 Agent。仅读取 blind-03 实际输入、玩家可见记录、宿主事件、现场回执、采样、关闭结果和截图，并读取通用 binary_evidence.py 解码工具；未读取设计文件、生产剧情源码、其他轮记录或答案。没有重开会话、修改源码或向盲玩家提供操作路线。

## 结论与进展边界

600 秒窗口内，侦探在 R-05 卧室内有实际行走，完成一次醒来回想；一项正式离房移动已提交并进入 prepared，随后现场 Complete 被拒绝，未到达走廊。本窗口没有取得可确认的案件有效检视、执行推演、正确作答被接受或章节交接。打开案卷和推演工作台不等于这些节点。

| 事项 | 实际证据与结论 |
| --- | --- |
| 起止时间 | result.dvlog：首次时钟采样 real_ms=2740，游戏 8:00；捕获终点 real_ms=600005，游戏 8:03，游戏时间实测推进 3 分钟。该窗口含启动、阅读和界面停留。 |
| 开场交互 | inputs.dvlog 有 detective.opening.wake 的 select；stdout e136 显示 applied，游戏时间 481。visible.dvlog 可见三段侦探开场正文，包含风衣辨认与“先出去”。这些是开场正文，不证明另行执行风衣检视或穿衣。 |
| 实际行走 | render-state.dvlog 中侦探由 [2458,900] 移到门前 [2401.66455078125,991.000854492188]，后移到 [2401.66455078125,834.999389648438]。对应输入有 A/D/S/W，截图显示人物确实改变位置。 |
| 有效检视 | inputs.dvlog 只有 wake 和 move 两个显式 select，没有 E11/E12 检视 select。最后出现“置物架 [E]”提示，仅表示接近热点。不能将案卷目录显示“证据01/物品01/人物01”直接当作本轮案件检视完成。 |
| 推演 | R 打开工作台，截图及 visible.dvlog 明示“尚未执行重演”“当前没有可进行的重演”。未执行，不评价推演正误。 |
| 作答与章节 | 未见作答提交/接受或章节交接。end.dvlog 仍 chapter=1、chapter_summary=false、logical_location=R05。 |

## 离房请求的正式链

这不是“尚未提交”的情况。host-session/inputs.dvlog 第 53 条记录（零基序号 52）包含 select，choice_id=detective.move.R05.corridor，interaction_id=spatial-162504729-5；continuous_walk.phase=requested，from_room=zone_yi:north_1，to_room=zone_yi:corridor。

run/host-events.dvlog 在 real_ms=172111 提供 action31 的 prepared 意图，目标 logical_location=corridor、execution_room=zone_yi:corridor；其后 real_ms=172133 的 dv_present 为 execution_event=e352、status=rejected、visible=false、lines=[]。stdout 也记录 e352 rejected。

inputs.dvlog 中 action31 的正式 dv_complete 回执为：outcome=rejected；field.status=blocked；reason=released；serial=32；continuous_walk=true；source_geometry=source-side-doorway；room=zone_yi:north_1；actual_position=[2401.66455078125,991.000854492188]；transitions=[]。没有目标房间到达回执，最终状态仍在 R05。

interaction-timing.dvlog 记录 continuous_request_begin（source=entering-doorway），之后 continuous_passage_prepared 的 request_to_prepare_usec=12555047，约 12.56 秒；紧随的 continuous_request_released 原因为 execution_rejected。实际 S 输入在 159112–160014 ms、171176–171678 ms，两次均已释放，prepared 宿主事件在约 172111 ms 才出现。证据支持“已请求，等待正式准备期间输入已释放，现场以 released/blocked 拒绝”。本审查未读实现，不能把这直接定性为设计锁门、碰撞故障或某一源码根因。交互计时 real_us 和捕获 real_ms 有各自起点，报告不将其混用为精确绝对时刻。

玩家可见反馈不足：拒绝 presentation 本身 visible=false、没有正文；拒绝前后截图仍显示卧室和门前角色，时钟由 8:02 变为 8:03。截图本身不能证明已正式提交，也不能独立证明为什么被挡；上述判断依靠正式输入和回执。后续“此处暂时没有可执行的交互”与移除 move 的可用选项是真实记录，但不能据此推断隐含谜底或通行条件。

## 实际查看的截图

实际打开并查看 run/frame-000031603.png（开场对话）、frame-000171991.png（8:02 门前）、frame-000172397.png（8:03 仍门前）、frame-000354480.png（空推演工作台）、frame-000599881.png（8:03 置物架旁）。人物、卧室陈设、门口和热点可见；人物接近置物架时有半透明叠放效果。仅抽查这些帧，不能宣称全程布局、遮挡和全部输入组合均通过验收。两次尝试打开不存在的近邻文件名未产生图像，随后使用实际存在帧完成核对。

## 工程关闭与证据校验

close-result.dvlog：complete=true，audit_mode=true，trace_policy=full，unprocessed_commands=0，pending_action 为空，errors=[]。capture-close.dvlog：capture_end_real_ms=600005，archive_and_host_closed_real_ms=601378，archive_errors=[]。result.dvlog 的进程和 profiler exit_code 均为 0；stdout 结束为 host-closed-during-active-session。这是仍在活动会话时关闭，不是剧情完成。

使用 binary_evidence.records 实际遍历本目录所有 .dvlog/.dvdag/.dvsave，帧长度和 SHA-256 校验未抛出错误。进一步独立复算 session.dvdag 与 save.dvsave 的逐序号/prev/SHA-256 链，均相符；两者有 final checkpoint 与 final_checkpoint=true 的末尾 seal。session.dvdag 的 nodes=39748、save.dvsave 的 nodes=39867，仅是文件结构事实，不是游戏评价。未执行存档恢复，因此不宣称恢复行为通过。

source-before.json 和 source-after.json 原字节相同，result.dvlog 报告 source_changes=[]。godot-stderr.log 有“14 RIDs of type Texture were leaked”关闭警告；不得省略成完全无警告。现有结果无 Godot 脚本错误行，但 godot_script_profile_confirmed/native GPU 结论未获确认。性能受本轮 full audit 与 profiler 影响，不据此作无插桩速度或整体可玩性结论。

## 操作报告交叉核对

已读取本轮 blind-report.md。其主要可见体验结论与原始证据相符：卧室内移动、阅读开场、门道未离房、工作台未执行重演、末尾置物架提示且未继续检视。报告谨慎写明不能从画面判断底层提交状态，符合盲玩观察边界；本独立审查以正式 select/prepared/Complete 补全为“移动已提交后拒绝”。

报告存在一项记录完整性限制：“逐次目的、输入与可见结果”节只有首尾两项，而 run/input-events.dvlog 有 25 条消费记录（包含按键释放），不能将该节理解为全部逐次记录。实际原始输入仍完整保存在 input-events.dvlog 和 host-session/inputs.dvlog；本限制不推翻其体验叙述，也不补造操作者当时目的。案卷目录计数与可见提示不被报告冒充剧情节点。报告中“单次点击南侧地面后未观察到角色位置变化”是操作者观察，没有足够证据把它扩大为所有鼠标导航均失效。

本独立审查已完成，未留下待核对的操作报告。
