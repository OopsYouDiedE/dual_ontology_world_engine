# 本轮纯引擎 AI CLI 演示独立证据审查

审查日期：2026-10-22。范围仅为本目录本轮证据、根与 DV AGENTS、现役二进制只读工具；未打开新正式会话，未读设计文件、历史报告/轨迹、游戏源码或私有规划候选。使用 `binary_evidence.records` 校验并读取原始控制台，用 `RawDagIndex` 校验两份 DAG，按正式事件与必要字段核对。没有用 next 或 decision 数替代实际 Complete。

## 结论

本轮是引擎自身 AI、固定侦探视角、formal-model、精简记录演示。实际成功 Complete 为全体 106 次，其中侦探本人 4 次。窗口内侦探完成醒来、穿衣、走出卧室到走廊、进入活动厅，随后听到两位人物的公开自我介绍。没有实际作答接受或章节交接证据，不能报告案件或章节完成。

106 条 `action_execution` 的 execution.status 全为 applied，且与 106 条 `host_completion` 按主体、action_event、outcome 一一吻合；存档 executed 与各主体 completed_actions 总数均为 106。所有主体均未出现重复的相同行动 ID。213 次 next 只是宿主推进输入，不是 213 次完成行动。未发现重复完成或全局停止推进的证据；300 秒只覆盖开场的有限推进，不能推断整局可达性或后续是否卡住。

## 时间、输入与停止边界

原始 `console.dvlog` 有 1119 个可读校验帧。launch 指定 `--controller ai --viewer detective`，scope 为 formal-model；全部 107 条 status 均保持 viewer=detective、chapter=1、development_mode=false。原始 stdin 仅有 `view detective` 1 次、`next` 213 次、`quit` 1 次，没有人工选择行动、答案或开发状态修改输入。

| 边界 | 相对启动现实秒 | 说明 |
| --- | ---: | --- |
| window_start | 2.656 | 正式授权窗口起点 |
| 300 秒截止 | 302.656 | 据记录起点加授权时长 |
| 最后 next | 299.844 | 截止前发出，之后不再发 next |
| window_end / quit | 303.391 | elapsed_seconds=300.735，超出截止 0.735 秒 |
| closed | 322.266 | exit_code=0，pipes_complete=true |

窗口超出 0.735 秒属于最后一次在途推进返回和关闭触发的实际边界；退出封存另用 18.875 秒，不能并入 300 秒游戏推进成绩。最后实际 Complete 的公开投影为 e1101，记录时间为启动后 299.844 秒，仍在截止前；截止后没有新增 Complete 的控制台投影，也没有 DAG 中多出的完成动作。最终 session_stopped(e1157) 为 host-closed-during-active-session、actions_executed=106，属于宿主停止活跃会话，非剧情终点。

window_end 的最后 status 为游戏分钟 487（08:07）、第1章、乙区活动厅、active=true；封存 trace.time 与停止记录为 488（08:08）。两者不能互相替换。存档仍有两项排队行动：侦探 `detective.move.hall.foyer`（start=486、due=488）和 demon 的 `other.question.contract`（start=485、due=500）；它们未形成成功 Complete，不能计作到达门厅或取得问题。存档 status 为 action-window-exhausted，与 trace 的宿主关闭状态分别保留。

## 侦探本人真实推进与所见

以下现实耗时均以 window_start 为零点，由对应 stdout 帧原始时间测量；不按游戏时间换算。

| 正式执行事件 | 行动 | 游戏时刻 | 窗口内现实耗时 | 后续可见内容或正式观察 |
| --- | --- | --- | ---: | --- |
| e137 / action1 | detective.opening.wake | 08:01 | 3.016 秒 | e138：本人醒来、个人身份/最后记忆与风衣相关感知；正文记得上船、辨认风衣、想先出去 |
| e236 / action15 | detective.opening.wear-coat | 08:02 | 15.438 秒 | e237：风衣 worn_by/location=detective；正文确认披上熟悉风衣 |
| e475 / action48 | detective.move.R05.corridor | 08:04 | 81.844 秒 | e476：本人 location=corridor；该次可见投影无额外正文 |
| e820 / action77 | detective.move.corridor.hall | 08:06 | 179.235 秒 | e821：本人 location=hall；e917另外记录三位同伴在hall的共处观察 |

之后 e983 的霍铎自介“……霍铎。钳工。”在窗口内 244.579 秒可见，e1003 的苏泠自介“苏泠。弹琴的。”在 256.516 秒可见。其余主体的非可见行动不能作为侦探已知内容。原始 presentation 共106条，其中侦探视角 visible=true 的6条就是上述4次本人行动与2条自介。没有 judgment=true 的可见结果，没有 answer 行动、接受事件或 chapter 转换；status 始终第1章。未读取私有规划内容，故不对正确推演或深层规划质量作结论；本轮也未到达可认定的关键案件检视。

## 二进制封存

两份文件均为 dv-raw-dag-v2，扫描期间未变化，初始/最终 checkpoint 和 seal 齐全，校验链无错误，无残余或未提交尾部。本轮不是逐 event commit 的 journal：包的 committed_events=0，但 trace 顶层 events 数组实际保存1477条事件；不能误报成没有事件。

| 文件 | 字节数 | 验证帧 / 节点 | SHA-256 |
| --- | ---: | --- | --- |
| session.dvdag | 33028808 | 85484 / 85480 | 65533617f50bb91a507d5ff02e1b501759ae515a323529ea0cac4642dfef7f78 |
| session.dvsave | 21238021 | 56743 / 56739 | 950423710ee6cb703a8370f5c2549c8549535f5f11bf305155fa2bd62191f384 |

audit_mode=false，performance_controller=engine。closed 的 changed_sources 为空；这是捕获器给出的本轮结果，审查未另读源码重算指纹。文件完整封存与剧情达成分开；本轮确认前者，不确认后者。

## 截图核对与限制

实际查看 screenshots/01-detective.png、02-detective.png、03-final.png，并读取同目录 selection.md。它们是 CLI 生产渲染器对本轮记录的只读回放导出，不是新正式会话、操作系统终端截图或 Godot 画面。

01显示08:01醒来正文，02显示08:02披风衣正文，均与原始可见内容一致，无明显文字重叠。03显示08:08、乙区活动厅和苏泠/霍铎及一名仍称同伴的人物，支持实际到达活动厅的显示核对。03处于1/2页，底部仍可见早先醒来和穿衣记录，未展示两条自介台词全文；台词已公开的判断来自原始console，不能把这一张截图称作台词全文展示。最终画面还同时列出两组行动菜单，分别位于认知区上方与记录区上方；这是实际可见的重复呈现，不是重复执行证据。截图上方引擎耗时属于引擎采样指标，不能替代本报告按原始窗口计时的现实耗时。

该核对不覆盖现场物理移动、碰撞、鼠标/键盘游戏输入、完整终端翻页体验或 Godot UI，不能作为二/三级游戏本体验收。

## 审查操作偏差

审查中一次把 action_execution.data 误作小记录整体展开，实际包含 receipt.execution.world，导致权威世界大字段输出，已立即停止该只读进程并改用字段路径。这违反本次避免整值展开的操作约束；未读取设计文件或私有 planning 事件。该权威内容不用于推断侦探认知、答案或进展。本报告的本人所见结论仅依据正式 observation 小字段与原始公开 presentation。该偏差作为审查限制保留。

补充截图核对：随后实际查看 screenshots/04-final-page.png，为同一最后帧的2/2页。该页清晰显示霍铎和苏泠两条自介正文，与原始console的e983/e1003一致，没有额外推进正式会话。03中的高亮“前往乙区·门厅”是已选择但尚未Complete的排队动作；主观当前位置仍为活动厅，04也不构成抵达门厅证据。
