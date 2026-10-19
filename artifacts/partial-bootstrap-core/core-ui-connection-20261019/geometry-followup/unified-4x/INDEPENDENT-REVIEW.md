# unified-4x 独立证据审查

日期：2026-10-19。独立新上下文代理只读审查本冻结批，未读取设计文档，未启动构建、游戏、探针或修改生产。本文只评价现有材料能支持的工程链与画面事实，不评价人物设计合理性。主代理通知三个窗口串行结束；日志和封存材料与该范围相符。

## 冻结、关闭与证据

独立将 source-hashes.json、after-source-hashes.json 与当前原始文件字节 SHA-256 比较，未发现记录所列文件变化；freeze-result.json 的 changed 亦为空。该清单涵盖所列宿主、执行器、地图、资产及程序集，不能扩称全仓所有文件均受保护。

generated-source-consistency.json 的 differences 为空。静读 dv_sources.py 与 build_session.py 确认其 module_hashes 是编译器源归一化口径：统一换行、替换 include 行并追加模块尾换行；source-hashes 使用原始字节。两类值不能跨口径直接比较。本文没有重新编译生成物。

player-render/close 与 ai-render/close 的 close-result.json 均记录 complete=true、pending_action 为空、unprocessed_commands=0、errors 为空。独立核对两个 manifest 指向的 AppData 原件的字节长度和 SHA-256，并核对 copied=true 的本地副本哈希，未发现不符。大 session.json/save.json 原件仍位于各 manifest 指向的 AppData 路径，未复制到本目录；交付并非自包含，长期保留依赖那些原件。complete 表示封存完成，不表示剧情终局。

import.log、player-render.log、geometry-probe.log、ai-render.log 本轮未见 ERROR、SCRIPT ERROR、WARNING、缺失资源或 ObjectDB 泄漏文本。该观察仅针对本轮这些日志，不消除未执行路径的风险。

## 玩家链

实际以 view_image 查看 player-render/frame-00.png、frame-05.png。前者显示 8:00、地图与候选菜单，包括“前往走廊”；后者显示 8:02、走廊中的侦探、姓名立绘、正文“沿通路走到相连的房间。”及继续按钮。已查看画面中这些文本未见截断或覆盖。

state-00.json 给出 player_control=true、awaiting_input，真实候选包含 detective.move.R05.corridor。capture_4x.gd 读取当前菜单首个可用 Button 并调用 button.pressed.emit()；日志明确 not_physical_input=true。inputs.jsonl 同时保留 select 与 control_signal pressed。这是工程按钮激活，不是原生鼠标点击、键盘输入或独立玩家决策验证。

同一输入记录中的 dv_complete/action17 携带正式移动现场回执：actor=detective、action=detective.move.R05.corridor、serial=18，从 zone_yi:north_1 的 [858,196] 到 zone_yi:corridor 的 [808,360]，execution_world=visible-godot、status=arrived、arrival_error=0、outcome=applied。GameEngine.cs 的 dv_complete 分支实际调用 Session.Complete；日志 e247 对应该动作 applied，visible 的完整正文 trace_id=e247、line_id=e247:surface:0:0、source_id 同该动作。由此可关联实际候选、按钮激活、真实场地移动、Complete 与可见结果。

该移动使用房间目标，station=zone_yi:corridor、radius=0；不能将其当作 E1 等显式站点接触验收。开场 8:00 到该结果 8:02 的窗口随后由 host 关闭，未覆盖后续调查或章节交接。

## AI 开场及来源

实际查看 ai-render/frame-00.png、frame-05.png。房间、侦探与立绘、姓名、8:01、正文“我记得的最后一件事，是上船。”及继续按钮可见，未见该正文截断；首尾画面保持此页。

ai-render/close/visible.jsonl 的完整正文为 trace_id/execution_event=e134、source_id=detective.opening.wake、line_id=e134:presentation.lines:0:0；source_refs 指向 01_开场.story 的 s1_wake.003、第5行。独立读取该源行，文字相符。line_id 是实际输出字段与段序标识，不是源码行号。player 的 source_refs 指向实体登记.story 的“重演通路”第19行，该行确为声明标题；该引用粒度不能扩称逐字正文来源。

AI 日志记录 detective.opening.wake 在481分 applied。采集器未点击继续，页面停留符合等待呈现继续的窗口行为，不能据此判定自主推理停滞，也不能据此声称后续 AI 推进有效。

player 与 AI 的原始 inputs 均记录 host_configuration：playback_speed=4.0、physics_ticks_per_second=240，controller 分别为 player/ai。Main.gd 的时间尺度与物理 tick 设置和记录接线一致。

## 几何夹具

静读 probe_geometry_4x.gd：其直接建立 WorldView、CampusMapView 与 DvFieldExecutor，主体 geometry_probe；不创建 DvSession、不调用 Prepare/Complete、不结算认知。它借用生产场地执行器返回几何回执，是纯夹具，不能算游玩、知识获取或三级验收。

实际查看 geometry-probe/E1.png、E7.png、E10.png、outdoor_meter.png、outdoor_high.png、lane_east.png。室内人物分别位于门厅、长桌侧面和备餐柜台旁；户外仪器可见，人物在通路上；高地目标图仍在道路；lane_east 图中人物位于建筑背侧窄路并呈现遮挡着色。夹具没有 UI/剧情显示，不用它证明玩家观察文字。

receipts.json 记录 timed_out=false。E1/E2/E6/E7/E10/E11、outdoor_meter/outdoor_high 与 lane_east 的现场记录均为 arrived，arrival_error 均为0；该字段是相对 approach_point 的终点误差，并非到站点中心距离。E1 实际 [72,396]、站点距离46；E10 [328,520]、距离43.8634262084961；E11 距离44.2718887329102。E7 实际 [400.526916503906,290.526916503906]，station_distance=46.0000038146973、radius=46，保留浮点尾差，不宣称严格 distance<=radius。户外仪器 [672,1462] 距中心46，高地 [672,1318] 距中心46。lane_east 是 anchor 房间目标，实际 [2104,744]，station 字段为 campus_sw:campus_sw、radius=0，不能写成显式站点回执。

夹具每张地图独立创建角色；室内目标在同一角色上依次执行，户外与西南地图重新建立。它没有验证地图间真实章节跨门。几何日志明确四倍速、240 physics ticks，和正式窗口配置一致。

## 结论范围

本轮证据支持二级 UI 的开场候选、开场正文、一次工程按钮触发的走廊移动及其 Core 完成呈现，以及所列目标的独立几何定位。已查看截图支持这些局部呈现判断。原生输入、显式站点的正式 Core 观察与知识效果、跨地图动作、完整剧情、五日覆盖、长文本分页滚动及完整三级游戏体验均未验证。本轮不是一级无窗口逻辑验收，也不是独立盲玩；不以日志或构建替代它们。
