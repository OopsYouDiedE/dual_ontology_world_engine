# 本轮 CLI/TUI 独立证据审查

审查日期：2026-10-22。新上下文仅审查本目录最终证据及只读二进制读取工具，未读取设计文件、历史轮次或源码，未启动游戏、会话或回放。四张 PNG 均实际查看。本轮为人工命令验收，不是盲玩或 Core AI 验收。

原始 console.dvlog 共 90 条记录，含 16 条 stdin。输入依次为 focus 03、capture 01、confirm、view detective、六次 next、capture 02、map both、capture 03、page next、capture 04、quit；capture 的完整路径保存在原始记录。启动为 --tui --controller player --viewer detective，启用 audit、trace、save。关闭记录 exit_code=0、timed_out=false、interrupted=false、pipes_complete=true、changed_sources=[]。后者仅证明录制器追踪的启动指纹项目未变化，未独立比对全仓库源码。

最终轨迹的 events 数组含 155 个事件。实际 host_completion/action_execution 各 3 个，均为 formal-model、applied：e131/e132 属 demon（demon.departure.inspect.astral）；e135/e136 属 detective（detective.inspect.E11）；e139/e140 属 quer（quer.inspect.outer-latch）。侦探实际 Complete 只有 1 次。最早 host-interaction-2 的 result 只是 scheduled、visible=false，不能另算一次 Complete；预选、计划、动作候选也不计实际执行。

四个 result 的 actor/viewer/presentation_viewer 分别为 detective/detective/detective、demon/detective/detective、detective/detective/detective、quer/detective/detective。preview.viewer 与 snapshot.viewer 随 actor 分别为 detective、demon、detective、quer；presentation_viewer 始终为 detective，符合固定终端观察者。两个 NPC 完成的 presentation.visible=false；侦探完成的 presentation.visible=true。字段确实区分行动主体、固定观察者与行动主体预览，不能据固定侦探视角把 NPC Complete 归给侦探。

01 图可见“检视书架”聚焦高亮、报价及主观认知；02 图时间 08:01，书架选项消失，记录显示书架积灰、干净长方形痕迹及半枚手印。可确认一次书架检视后的玩家可见进展。03 图显示客观世界列表，04 图为第 2/3 页并显示通道；可见 bed、transmitter、opening.* 等标识或未记录名称占位，不能称命名已完全润色。此处为有限终端截图，未覆盖所有页面、尺寸、真实终端输入方式或 Godot 画面。

session.dvdag 与 session.dvsave 均通过只读帧校验、顺序/校验链与封存扫描：分别 34129/32734 帧、34125/32730 节点；均 complete_sealing=true、invalid_tail=None、residual_tail_bytes=0，扫描期间未变化。两文件是 checkpoint 包装值（外层事件提交数为 0），实际轨迹事件位于 final 根的 events 数组，不能据外层计数说没有行动。最终 session_stopped 为 host-closed-during-active-session，actions_executed=3，游戏时间 481 分钟；这是主动结束有限窗口，不是整局完成。

结论：本轮证据支持有限人工 CLI/TUI 的聚焦、确认、正式执行结果投影、主客观列表/翻页及完整二进制封存；未见字段把固定观察者误写成 NPC 行动主体。未验证正确作答、章节交接、整局达成、Godot 几何/碰撞/输入、回放或恢复。本审查未实施源码修改，未提交。
