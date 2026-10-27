extends CanvasLayer

const CONSOLE_THEME := preload("res://scenes/UI/Styles/OverlayTheme.tres")
const REPLAY_STAGE := preload("res://scenes/Host/ReplayStage.gd")
const INSPECT_INTERVAL := 0.25
const INSPECT_TAB := 1
const PERFORMANCE_TAB := 2
const REPLAY_TAB := 3

var host: Node
var engine: Node
var panel: PanelContainer
var tabs: TabContainer
var command: LineEdit
var output: TextEdit
var physical: TextEdit
var mind: TextEdit
var queue: TextEdit
var actor: LineEdit
var inspect_status: Label
var planning_status: Label
var planning_table: Tree
var planning_detail: TextEdit
var performance: Label
var _engine_performance: Dictionary = {}
var _engine_performance_at_us: int = 0
var graph: Control
var navigation_table: Tree
var replay_text: TextEdit
var replay_status: Label
var replay_stage: Control
var replay_play: Button
var replay_step: Button
var file_dialog: FileDialog
var _history := PerformanceRing.new()
var _phase_starts: Dictionary = {}
var _sampling := false
var _sampling_start_us := 0
var _last_clock := 0
var _inspect_actor := ""
var _inspect_elapsed := INSPECT_INTERVAL
var _inspect_pending := false
var _inspect_failed := false
var _inspection_key := ""
var _selected_plan_path := ""
var _replay_speed := 1.0
var _replay_playing := false
var _replay_count := 0
var _replay_index := -1
var _replay_tail_remaining := 0.0
var _step_pending := false

func _ready() -> void:
	layer = 128
	process_mode = Node.PROCESS_MODE_ALWAYS
	set_process(false)

func configure(owner: Node, bridge: Node) -> void:
	host = owner
	engine = bridge
	_build()

func _build() -> void:
	panel = PanelContainer.new()
	panel.theme = CONSOLE_THEME
	panel.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	panel.offset_left = 28
	panel.offset_top = 28
	panel.offset_right = -28
	panel.offset_bottom = -28
	add_child(panel)
	var margin := MarginContainer.new()
	panel.add_child(margin)
	var root := VBoxContainer.new()
	margin.add_child(root)
	var header := HBoxContainer.new()
	root.add_child(header)
	var title := Label.new()
	title.text = "开发控制台  ·  ` / F12"
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(title)
	header.add_child(_button("关闭", close))
	tabs = TabContainer.new()
	tabs.size_flags_vertical = Control.SIZE_EXPAND_FILL
	root.add_child(tabs)
	_build_commands(_tab("控制"))
	_build_inspect(_tab("双本体・规划"))
	_build_performance(_tab("性能"))
	_build_replay(_tab("回放"))
	tabs.tab_changed.connect(func(_index: int): _sampling_changed())
	file_dialog = FileDialog.new()
	file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	file_dialog.access = FileDialog.ACCESS_FILESYSTEM
	file_dialog.filters = PackedStringArray(["*.dvdag,*.dvlog ; DV 记录"])
	file_dialog.file_selected.connect(_load_replay)
	add_child(file_dialog)
	panel.hide()
	_append("help 显示命令。tp、time、inspect 需要以 --dv-dev 开启会话；回放页可打开实际记录。")

func _tab(title: String) -> VBoxContainer:
	var page := VBoxContainer.new()
	page.name = title
	tabs.add_child(page)
	return page

func _button(title: String, callback: Callable) -> Button:
	var button := Button.new()
	button.text = title
	button.pressed.connect(callback)
	return button

func _text_area(parent: Node) -> TextEdit:
	var area := TextEdit.new()
	area.editable = false
	area.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
	area.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	area.size_flags_vertical = Control.SIZE_EXPAND_FILL
	parent.add_child(area)
	return area

func _labeled_text(parent: Node, title: String) -> TextEdit:
	var column := VBoxContainer.new()
	column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	column.size_flags_vertical = Control.SIZE_EXPAND_FILL
	parent.add_child(column)
	var label := Label.new()
	label.text = title
	column.add_child(label)
	return _text_area(column)

func _build_commands(page: VBoxContainer) -> void:
	output = _text_area(page)
	command = LineEdit.new()
	command.placeholder_text = "help | status | options | act <choice> | inspect [actor] | tp <actor> <place> | time <minutes>"
	command.text_submitted.connect(_command)
	page.add_child(command)

func _build_inspect(page: VBoxContainer) -> void:
	var toolbar := HBoxContainer.new()
	page.add_child(toolbar)
	actor = LineEdit.new()
	actor.placeholder_text = "主体 ID（留空为当前观察者，Enter 应用）"
	actor.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	actor.text_submitted.connect(func(_value: String): _select_inspect_actor())
	toolbar.add_child(actor)
	toolbar.add_child(_button("查看主体", _select_inspect_actor))
	inspect_status = Label.new()
	inspect_status.text = "打开本页后每 0.25 秒读取当前状态。"
	page.add_child(inspect_status)
	var columns := HSplitContainer.new()
	columns.size_flags_vertical = Control.SIZE_EXPAND_FILL
	columns.size_flags_stretch_ratio = 1.2
	page.add_child(columns)
	physical = _labeled_text(columns, "Physical · 客观物理世界")
	mind = _labeled_text(columns, "Mind · 所选主体的认识")
	var lower := HSplitContainer.new()
	lower.size_flags_vertical = Control.SIZE_EXPAND_FILL
	lower.size_flags_stretch_ratio = 1.0
	page.add_child(lower)
	queue = _labeled_text(lower, "行动队列 · 策略 · 侵蚀")
	queue.custom_minimum_size.x = 240
	var plans := VBoxContainer.new()
	plans.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	plans.size_flags_stretch_ratio = 2.5
	lower.add_child(plans)
	planning_status = Label.new()
	planning_status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	planning_status.text = "规划候选按净分降序；同分保留原记录次序。"
	plans.add_child(planning_status)
	planning_table = Tree.new()
	planning_table.columns = 9
	planning_table.column_titles_visible = true
	planning_table.hide_root = true
	planning_table.select_mode = Tree.SELECT_ROW
	planning_table.size_flags_vertical = Control.SIZE_EXPAND_FILL
	planning_table.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	planning_table.custom_minimum_size.y = 120
	var titles := ["序", "候选路径", "净分", "总值", "信息", "验证", "进展", "成就", "机会"]
	for index in range(titles.size()):
		planning_table.set_column_title(index, titles[index])
		planning_table.set_column_expand(index, index == 1)
		planning_table.set_column_custom_minimum_width(index, 190 if index == 1 else 55)
	planning_table.item_selected.connect(_plan_selected)
	plans.add_child(planning_table)
	planning_detail = _text_area(plans)
	planning_detail.custom_minimum_size.y = 90
	planning_detail.size_flags_stretch_ratio = 0.6

func _build_performance(page: VBoxContainer) -> void:
	performance = Label.new()
	performance.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	performance.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	performance.text = "等待本页的实际帧与阶段记录。"
	performance.tooltip_text = "摘要每 0.25 秒刷新。推进、决策频率和接口耗时率共用累计现实小时分母。人工 / LLM 接口耗时记录实际决策请求交付至输入被接受的区间；未记录区间保持缺失。现实采样窗口从打开 / 恢复完成到关闭请求，短窗口比率是样本累计值。"
	page.add_child(performance)
	graph = FrameGraph.new()
	graph.history = _history
	graph.custom_minimum_size.y = 180
	graph.size_flags_vertical = Control.SIZE_EXPAND_FILL
	page.add_child(graph)
	var explanation := Label.new()
	explanation.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	explanation.text = "固定 120 帧环：帧耗时和当帧完成的 Prepare / Complete / 寻路 / 现场执行耗时，单位均为毫秒。离开本页停止帧采样；会话累计统计继续记录。下表显示最近 24 次已采样寻路。"
	page.add_child(explanation)
	navigation_table = Tree.new()
	navigation_table.columns = 4
	navigation_table.column_titles_visible = true
	navigation_table.hide_root = true
	navigation_table.size_flags_vertical = Control.SIZE_EXPAND_FILL
	navigation_table.size_flags_stretch_ratio = 0.6
	navigation_table.custom_minimum_size.y = 120
	for index in range(4):
		navigation_table.set_column_title(index, ["采样帧", "寻路 query", "地图 / 主体", "耗时 ms"][index])
		navigation_table.set_column_expand(index, index == 1 or index == 2)
	page.add_child(navigation_table)

func _build_replay(page: VBoxContainer) -> void:
	var controls := HBoxContainer.new()
	page.add_child(controls)
	controls.add_child(_button("载入记录", func(): file_dialog.popup_centered_ratio(0.75)))
	replay_play = _button("Play", _play_replay)
	controls.add_child(replay_play)
	controls.add_child(_button("Pause", _pause_replay))
	replay_step = _button("Step", _step_replay)
	controls.add_child(replay_step)
	for value: float in [1.0, 2.0, 4.0, 8.0]:
		controls.add_child(_button("%d×" % int(value), func():
			_replay_speed = value
			replay_stage.set_playing(_replay_playing or _step_pending, value)
			if _replay_playing: engine.ReplayPlay(value)
		))
	replay_status = Label.new()
	replay_status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	replay_status.text = "载入记录后，在独立 Main / WorldView / UI 中重放。现实静默由演出时钟压缩。"
	page.add_child(replay_status)
	var columns := HSplitContainer.new()
	columns.size_flags_vertical = Control.SIZE_EXPAND_FILL
	page.add_child(columns)
	replay_stage = REPLAY_STAGE.new()
	replay_stage.custom_minimum_size = Vector2(560, 315)
	replay_stage.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	replay_stage.size_flags_vertical = Control.SIZE_EXPAND_FILL
	replay_stage.size_flags_stretch_ratio = 3.0
	columns.add_child(replay_stage)
	replay_text = _labeled_text(columns, "当前片段与原始数据")
	replay_text.custom_minimum_size.x = 220
	_update_replay_controls()

func _input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo and event.physical_keycode in [KEY_F12, KEY_QUOTELEFT]:
		if is_open(): close()
		else: open()
		get_viewport().set_input_as_handled()
	elif is_open() and event.is_action_pressed("ui_cancel"):
		close()
		get_viewport().set_input_as_handled()

func open() -> void:
	panel.show()
	_last_clock = Time.get_ticks_usec()
	set_process(true)
	_sampling_changed()
	if tabs.current_tab == 0: command.grab_focus()

func close() -> void:
	panel.hide()
	_pause_replay()
	set_process(false)
	_sampling_changed()

func is_open() -> bool:
	return is_instance_valid(panel) and panel.visible

func performance_open() -> bool:
	return is_open() and tabs.current_tab == PERFORMANCE_TAB

func _sampling_changed() -> void:
	var enabled := performance_open()
	engine.SetDiagnosticsEnabled(host.audit_enabled or enabled)
	host.set_navigation_sampling(enabled)
	if enabled != _sampling:
		_sampling = enabled
		_phase_starts.clear()
		if enabled:
			_history.clear()
			navigation_table.clear()
			_engine_performance.clear()
			_engine_performance_at_us = 0
			_sampling_start_us = engine.TimingClockUs()
			_last_clock = Time.get_ticks_usec()
	if is_open() and tabs.current_tab == INSPECT_TAB:
		_inspect_elapsed = INSPECT_INTERVAL
	if not is_open() or tabs.current_tab != REPLAY_TAB: _pause_replay()

func begin_shutdown() -> void:
	close()
	set_process_input(false)

func _process(_delta: float) -> void:
	var now := Time.get_ticks_usec()
	var seconds := float(now - _last_clock) / 1000000.0
	_last_clock = now
	if tabs.current_tab == INSPECT_TAB:
		_inspect_elapsed += seconds
		if _inspect_elapsed >= INSPECT_INTERVAL: _request_inspection()
	if performance_open():
		var sample := _consume_timings()
		sample.frame = Engine.get_process_frames()
		sample.frame_ms = seconds * 1000.0
		_history.push(sample)
		_show_performance(sample)
	if tabs.current_tab == REPLAY_TAB: _advance_replay(seconds)

func _select_inspect_actor() -> void:
	_inspect_actor = actor.text.strip_edges()
	_inspect_failed = false
	_inspection_key = ""
	_inspect_elapsed = INSPECT_INTERVAL
	_request_inspection()

func _request_inspection() -> void:
	if _inspect_pending or _inspect_failed: return
	if not engine.CanInspect():
		inspect_status.text = "正式 Inspector 需要 --dv-dev 会话；演出样本提供标记为样本的只读数据。"
		_inspect_elapsed = 0.0
		return
	if not engine.IsDispatchIdle(): return
	_inspect_elapsed = 0.0
	_inspect_pending = true
	engine.DevCommand("inspect " + _inspect_actor)

func _show_inspection(data: Dictionary) -> void:
	_inspect_pending = false
	_inspect_failed = false
	var who := str(data.get("actor", ""))
	var version := str(data.get("version", ""))
	var source := "演出样本" if bool(data.get("sample", false)) else "正式开发会话"
	inspect_status.text = "%s · %s · 版本 %s · 每 0.25 秒读取" % [source, who, version]
	var key := "%s:%s:%s" % [who, version, str(data.get("planning_event", ""))]
	if key == _inspection_key: return
	_inspection_key = key
	physical.text = JSON.stringify(data.get("physical", {}), "  ")
	mind.text = JSON.stringify(data.get("mind", {}), "  ")
	queue.text = JSON.stringify({"actor": who, "scheduled_action": data.get("scheduled_action", false), "policy": data.get("policy", {}), "erosion": data.get("erosion", 0), "erosion_rank": data.get("erosion_rank", 0), "erosion_stage": data.get("erosion_stage", ""), "erosion_memory_truncated": data.get("erosion_memory_truncated", false)}, "  ")
	var choice: Dictionary = data.get("planning", {}) if data.get("planning", {}) is Dictionary else {}
	_show_plans(choice, str(data.get("planning_event", "")))

func _path_text(value: Array) -> String:
	var names := PackedStringArray()
	for part: Variant in value: names.append(str(part))
	return " → ".join(names)

func _number(value: Variant) -> float:
	if value is Dictionary:
		var parts := str(value["$dv_rational"]).split("/")
		return float(parts[0]) / float(parts[1]) if parts.size() == 2 else float(parts[0])
	return float(value)

func _number_text(value: Variant) -> String:
	return "—" if value == null else "%.3f" % _number(value)

func _show_plans(choice: Dictionary, event_id: String) -> void:
	planning_table.clear()
	planning_detail.text = ""
	var rows: Array[Dictionary] = []
	for candidate: Dictionary in choice.get("plans", []):
		rows.append({"order": rows.size(), "plan": candidate})
	rows.sort_custom(func(left: Dictionary, right: Dictionary) -> bool:
		var a := _number(left.plan.score)
		var b := _number(right.plan.score)
		return a > b if a != b else int(left.order) < int(right.order)
	)
	var selected: Array = choice.get("selected_path", [])
	planning_status.text = "%d 个已记录候选 · 净分 %s · 展开 %s · 完成层 %s\n选中：%s · 停止：%s · 来源：%s" % [rows.size(), _number_text(choice.get("score")), str(choice.get("plan_expansions", "—")), str(choice.get("completed_layers", "—")), _path_text(selected), str(choice.get("stop", "—")), event_id]
	var root := planning_table.create_item()
	for row: Dictionary in rows:
		var candidate: Dictionary = row.plan
		var path: Array = candidate.get("path", [])
		var path_text := _path_text(path)
		var item := planning_table.create_item(root)
		item.set_text(0, str(int(row.order) + 1))
		item.set_text(1, ("✓ " if path == selected else "") + path_text)
		item.set_text(2, _number_text(candidate.get("score")))
		var value: Dictionary = candidate.get("value", {})
		for index in range(6):
			item.set_text(index + 3, _number_text(value.get(["value", "information", "verification", "progress", "achievement", "opportunity"][index])))
		item.set_tooltip_text(1, path_text)
		item.set_metadata(0, candidate)
		if path_text == _selected_plan_path: item.select(0)
	if rows.is_empty(): planning_detail.text = "当前记录没有展开候选。\n" + JSON.stringify(choice, "  ")

func _plan_selected() -> void:
	var item := planning_table.get_selected()
	if item == null: return
	var candidate: Dictionary = item.get_metadata(0)
	_selected_plan_path = _path_text(candidate.get("path", []))
	planning_detail.text = JSON.stringify(candidate, "  ")

func _consume_timings() -> Dictionary:
	var sample := {"prepare_ms": 0.0, "complete_ms": 0.0, "navigation_ms": 0.0, "physical_ms": 0.0, "prepare_count": 0, "complete_count": 0, "navigation_count": 0, "physical_count": 0, "navigation_queries": []}
	for raw: String in engine.PollTimings():
		var row: Dictionary = JSON.parse_string(raw)
		if float(row.real_us) < _sampling_start_us: continue
		var stage := str(row.stage)
		var family := ""
		if stage in ["prepare_begin", "prepare_end"]: family = "prepare"
		elif stage in ["complete_begin", "complete_end"]: family = "complete"
		elif stage in ["execution_begin", "execution_end"]: family = "physical"
		elif stage in ["navigation_begin", "navigation_end"]: family = "navigation"
		if family.is_empty(): continue
		var details: Dictionary = {}
		var key := "%s:%s:%s" % [row.command_id, row.interaction_id, family]
		if family == "physical" or family == "navigation":
			var envelope: Dictionary = JSON.parse_string(str(row.details))
			details = envelope.get("details", {})
			key = family + ":" + str(details["query_id" if family == "navigation" else "action_event"])
		if stage.ends_with("_begin"):
			_phase_starts[key] = {"at": float(row.real_us), "details": details}
		elif _phase_starts.has(key):
			var start: Dictionary = _phase_starts[key]
			var duration := (float(row.real_us) - float(start.at)) / 1000.0
			sample[family + "_ms"] += duration
			sample[family + "_count"] += 1
			if family == "navigation":
				var query: Dictionary = start.details.duplicate(true)
				query.merge(details, true)
				query.duration_ms = duration
				query.real_us = row.real_us
				sample.navigation_queries.append(query)
			_phase_starts.erase(key)
	return sample

func _show_performance(latest: Dictionary) -> void:
	var sums := {"frame_ms": 0.0, "prepare_ms": 0.0, "complete_ms": 0.0, "navigation_ms": 0.0, "physical_ms": 0.0, "prepare_count": 0.0, "complete_count": 0.0, "navigation_count": 0.0, "physical_count": 0.0}
	var peak := 0.0
	for index in range(_history.count):
		var sample: Dictionary = _history.at(index)
		for key: String in sums: sums[key] += float(sample[key])
		peak = maxf(peak, float(sample.frame_ms))
	var frame_summary := "%d / 120 帧 · 帧均值 %.2f ms · 峰值 %.2f ms\n阶段均值：Prepare %s · Complete %s · 寻路 %s · 现场执行 %s" % [_history.count, float(sums.frame_ms) / maxi(_history.count, 1), peak, _phase_average(sums, "prepare"), _phase_average(sums, "complete"), _phase_average(sums, "navigation"), _phase_average(sums, "physical")]
	var now := Time.get_ticks_usec()
	if now - _engine_performance_at_us >= 250000 and engine.has_method("PerformanceSummary"):
		_engine_performance = engine.PerformanceSummary()
		_engine_performance_at_us = now
	if _engine_performance.is_empty():
		performance.text = "会话统计：—"
	else:
		var controller := str(_engine_performance.get("controller_kind", ""))
		var controller_label := str(_engine_performance.get("controller_label", ""))
		if controller_label.is_empty():
			controller_label = "人工" if controller == "human" else "LLM" if controller == "llm" else "引擎"
		performance.text = "推进 %s 游戏小时/现实小时 · 决策 %s 次/现实小时\n%s接口 %s 小时/现实小时 · 采样 %s 现实秒 / 推进 %s 游戏分钟" % [_performance_rate("game_hours_per_real_hour"), _performance_rate("decisions_per_real_hour"), controller_label, _performance_rate("interface_hours_per_real_hour"), _performance_number("elapsed_wall_ms", "%.1f", 1000.0), _performance_number("advanced_game_minutes", "%.1f")]
		performance.text += "\n累计拆解：引擎 %s 秒 · 投影 %s 秒 · 开局/恢复 %s 秒 · 保存/归档 %s 秒" % [_performance_number("engine_ms", "%.3f", 1000.0), _performance_number("projection_ms", "%.3f", 1000.0), _performance_number("startup_ms", "%.3f", 1000.0), _performance_number("persistence_ms", "%.3f", 1000.0)]
	performance.text += "\n\n" + frame_summary
	graph.queue_redraw()
	if latest.navigation_queries.is_empty(): return
	navigation_table.clear()
	var root := navigation_table.create_item()
	var shown := 0
	for index in range(_history.count - 1, -1, -1):
		var sample: Dictionary = _history.at(index)
		var queries: Array = sample.navigation_queries
		for query_index in range(queries.size() - 1, -1, -1):
			var query: Dictionary = queries[query_index]
			var item := navigation_table.create_item(root)
			item.set_text(0, str(sample.frame))
			item.set_text(1, str(query.query_id))
			item.set_text(2, "%s / %s" % [str(query.get("map", "")), str(query.get("actor", ""))])
			item.set_text(3, "%.3f" % float(query.duration_ms))
			item.set_tooltip_text(1, JSON.stringify(query, "  "))
			shown += 1
			if shown == 24: return

func _performance_number(key: String, pattern: String = "%.3f", divisor: float = 1.0) -> String:
	var value: Variant = _engine_performance.get(key)
	if value == null: return "—"
	var number := float(value) / divisor
	return pattern % number if is_finite(number) and number >= 0.0 else "—"

func _performance_rate(key: String) -> String:
	var elapsed: Variant = _engine_performance.get("elapsed_wall_ms")
	if elapsed == null or not is_finite(float(elapsed)) or float(elapsed) <= 0.0: return "—"
	return _performance_number(key)

func _phase_average(sums: Dictionary, family: String) -> String:
	var count := int(sums[family + "_count"])
	return "—" if count == 0 else "%.2f ms (%d 次)" % [float(sums[family + "_ms"]) / count, count]

func _load_replay(path: String) -> void:
	_prepare_replay_load()
	replay_status.text = "载入记录：" + path
	engine.LoadReplay(path)

func _prepare_replay_load() -> void:
	_pause_replay()
	_replay_count = 0
	_replay_index = -1
	replay_stage.reset()
	replay_text.text = ""
	_update_replay_controls()

func _play_replay() -> void:
	if _replay_count == 0: return
	if _replay_index + 1 >= _replay_count and not replay_stage.busy() and _replay_tail_remaining <= 0.0: return
	_step_pending = false
	_replay_playing = true
	engine.ReplayPlay(_replay_speed)
	replay_stage.set_playing(true, _replay_speed)
	_update_replay_controls()

func _pause_replay() -> void:
	engine.ReplayPause()
	_replay_playing = false
	_step_pending = false
	replay_stage.set_playing(false, _replay_speed)
	_update_replay_controls()

func _step_replay() -> void:
	if _replay_count == 0 or _step_pending: return
	engine.ReplayPause()
	_replay_playing = false
	_step_pending = true
	replay_stage.set_playing(true, _replay_speed)
	_advance_replay(0.0)

func _advance_replay(seconds: float) -> void:
	_update_replay_controls()
	replay_stage.advance(seconds * _replay_speed if _replay_playing or _step_pending else 0.0)
	if _step_pending:
		if replay_stage.busy(): return
		_step_pending = false
		_show_frame(engine.ReplayStep(), false)
		replay_stage.set_playing(false, _replay_speed)
		_update_replay_controls()
		return
	if not _replay_playing or replay_stage.busy(): return
	for frame: Dictionary in engine.ReplayAdvance(seconds): _show_frame(frame, true)
	if _replay_index + 1 == _replay_count and not replay_stage.busy():
		_replay_tail_remaining -= seconds * _replay_speed
		if _replay_tail_remaining <= 0.0:
			_pause_replay()
			replay_status.text += " · 已到记录末尾"

func _update_replay_controls() -> void:
	replay_play.disabled = _replay_count == 0 or (_replay_index + 1 >= _replay_count and not replay_stage.busy() and _replay_tail_remaining <= 0.0)
	replay_step.disabled = _replay_count == 0 or _step_pending or _replay_index + 1 >= _replay_count

func _command(line: String) -> void:
	var text := line.strip_edges()
	if text.is_empty(): return
	command.clear()
	_append("> " + text)
	var parts := text.split(" ", false, 1)
	var argument := str(parts[1]) if parts.size() > 1 else ""
	match parts[0]:
		"help": _append("status：本人投影\noptions：当前合法动作\nact <choice>：从普通空间交互执行动作\ninspect [actor]：打开实时客观 / 本人 / 规划面板\ntp <actor> <place>：开发位置编辑\ntime <minutes>：开发时间推进\naudit on|off：帧与输入取证\n关闭控制台后返回现场。")
		"status": _append(JSON.stringify(engine.Snapshot(), "  "))
		"options":
			for option: Dictionary in engine.Snapshot().get("pending", {}).get("options", []): _append("%s  %s" % [option.get("id", ""), option.get("label", "")])
		"act": host.console_action(argument)
		"inspect":
			actor.text = argument
			tabs.current_tab = INSPECT_TAB
			_select_inspect_actor()
		"tp", "time": engine.DevCommand(text)
		"audit":
			if argument in ["on", "off"]:
				host.set_frame_audit(argument == "on")
				_append("帧取证 " + argument)
			else: _append("用法：audit on|off")
		_: _append("未知命令。help 显示可用命令。")

func _append(text: String) -> void:
	output.text += ("\n" if not output.text.is_empty() else "") + text
	if output.text.length() > 64000: output.text = output.text.right(48000)
	output.set_caret_line(output.get_line_count() - 1)

func receive(event: Dictionary) -> void:
	match str(event.kind):
		"dev_error":
			var message := str(event.get("message", ""))
			_append(message)
			if _inspect_pending:
				_inspect_pending = false
				_inspect_failed = true
				inspect_status.text = message
			if tabs.current_tab == REPLAY_TAB: replay_status.text = message
		"dev_inspect": _show_inspection(event.data)
		"replay_loaded":
			_pause_replay()
			_replay_count = int(event.count)
			_replay_index = -1
			replay_stage.reset()
			replay_status.text = "已载入 %d 个片段。" % _replay_count
			_show_frame(engine.ReplayStep(), false)
			_update_replay_controls()

func _show_frame(frame: Dictionary, animate: bool) -> void:
	if frame.is_empty(): return
	_replay_index = int(frame.sequence)
	_replay_tail_remaining = float(frame.duration)
	var data: Dictionary = frame.data
	var source := str(data.get("source", ""))
	if data.get("physical_coordinates_recorded", true) == false: source += " · 逻辑位置重建"
	replay_status.text = "%d / %d · %s · %s · %.2f 秒 · %d×\n%s" % [_replay_index + 1, _replay_count, frame.kind, frame.actor, float(frame.duration), int(_replay_speed), source]
	replay_text.text = str(frame.text) + "\n\n" + JSON.stringify(data, "  ")
	replay_stage.present(frame, animate)
	_update_replay_controls()

class PerformanceRing extends RefCounted:
	const CAPACITY := 120
	var samples: Array[Dictionary] = []
	var cursor := 0
	var count := 0
	func _init() -> void:
		samples.resize(CAPACITY)
	func clear() -> void:
		cursor = 0
		count = 0
		samples.fill({})
	func push(value: Dictionary) -> void:
		samples[cursor] = value
		cursor = (cursor + 1) % CAPACITY
		count = mini(count + 1, CAPACITY)
	func at(index: int) -> Dictionary:
		return samples[(cursor - count + index + CAPACITY) % CAPACITY]

class FrameGraph extends Control:
	var history: RefCounted
	func _draw() -> void:
		if history.count == 0: return
		var series := [
			{"key": "frame_ms", "name": "帧", "color": Color(0.25, 0.78, 0.56)},
			{"key": "prepare_ms", "name": "Prepare", "color": Color(0.95, 0.64, 0.25)},
			{"key": "complete_ms", "name": "Complete", "color": Color(0.82, 0.42, 0.50)},
			{"key": "navigation_ms", "name": "寻路", "color": Color(0.72, 0.56, 0.92)},
			{"key": "physical_ms", "name": "现场执行", "color": Color(0.35, 0.65, 0.95)}
		]
		var ceiling := 33.4
		for index in range(history.count):
			var sample: Dictionary = history.at(index)
			for metric: Dictionary in series: ceiling = maxf(ceiling, float(sample[metric.key]))
		var plot := Rect2(Vector2(12, 42), size - Vector2(24, 62))
		var guide := plot.end.y - 16.7 / ceiling * plot.size.y
		draw_line(Vector2(plot.position.x, guide), Vector2(plot.end.x, guide), Color(0.5, 0.5, 0.5, 0.5))
		var font := get_theme_default_font()
		var legend_x := 12.0
		for metric: Dictionary in series:
			draw_string(font, Vector2(legend_x, 20), str(metric.name), HORIZONTAL_ALIGNMENT_LEFT, -1, 13, metric.color)
			legend_x += 95.0
			var points := PackedVector2Array()
			for index in range(history.count):
				var value := float(history.at(index)[metric.key])
				points.append(Vector2(plot.position.x + plot.size.x * index / 119.0, plot.end.y - value / ceiling * plot.size.y))
			if points.size() > 1: draw_polyline(points, metric.color, 2.0)
		draw_string(font, Vector2(12, size.y - 4), "纵轴 %.1f ms · 参考线 16.7 ms" % ceiling, HORIZONTAL_ALIGNMENT_LEFT, -1, 12)
