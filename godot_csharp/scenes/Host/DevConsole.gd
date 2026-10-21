extends CanvasLayer

const CONSOLE_THEME := preload("res://scenes/UI/Styles/OverlayTheme.tres")
const GEOMETRY := preload("res://scenes/Host/GeometryCatalog.gd")
const FRAME_CAPACITY := 120
var host: Node
var engine: Node
var panel: PanelContainer
var tabs: TabContainer
var command: LineEdit
var output: TextEdit
var physical: TextEdit
var mind: TextEdit
var planning: TextEdit
var actor: LineEdit
var performance: Label
var graph: Control
var replay_text: TextEdit
var replay_status: Label
var replay_board: Control
var file_dialog: FileDialog
var frame_times: Array[float] = []
var _phase_starts: Dictionary = {}
var _durations: Dictionary = {}
var _last_clock := 0
var _replay_speed := 1.0
var _replay_playing := false
var _replay_count := 0

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
	file_dialog.file_selected.connect(func(path: String): engine.LoadReplay(path))
	add_child(file_dialog)
	panel.hide()
	_append("help 显示命令。tp、time、inspect 需要以 --dv-dev 开启会话。")

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
	actor.placeholder_text = "主体 ID（留空为当前观察者）"
	actor.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	toolbar.add_child(actor)
	toolbar.add_child(_button("读取", func(): engine.DevCommand("inspect " + actor.text.strip_edges())))
	var columns := HSplitContainer.new()
	columns.size_flags_vertical = Control.SIZE_EXPAND_FILL
	page.add_child(columns)
	physical = _text_area(columns)
	mind = _text_area(columns)
	physical.text = "客观物理世界"
	mind.text = "主体本人知道、相信与尚未获得的内容"
	var label := Label.new()
	label.text = "规划与行动队列"
	page.add_child(label)
	planning = _text_area(page)
	planning.custom_minimum_size.y = 140

func _build_performance(page: VBoxContainer) -> void:
	performance = Label.new()
	page.add_child(performance)
	graph = FrameGraph.new()
	graph.custom_minimum_size.y = 180
	graph.size_flags_vertical = Control.SIZE_EXPAND_FILL
	page.add_child(graph)
	var explanation := Label.new()
	explanation.text = "最近 120 帧；切离本页停止采样。Prepare、Complete、物理执行按命令与阶段配对。"
	page.add_child(explanation)

func _build_replay(page: VBoxContainer) -> void:
	var controls := HBoxContainer.new()
	page.add_child(controls)
	controls.add_child(_button("载入记录", func(): file_dialog.popup_centered_ratio(0.75)))
	controls.add_child(_button("Play", func():
		engine.ReplayPlay(_replay_speed)
		_replay_playing = true
	))
	controls.add_child(_button("Pause", func():
		engine.ReplayPause()
		_replay_playing = false
	))
	controls.add_child(_button("Step", func():
		engine.ReplayPause()
		_replay_playing = false
		_show_frame(engine.ReplayStep())
	))
	for value: float in [1.0, 2.0, 4.0, 8.0]:
		controls.add_child(_button("%d×" % int(value), func():
			_replay_speed = value
			if _replay_playing: engine.ReplayPlay(value)
		))
	replay_status = Label.new()
	replay_status.text = "尚未载入记录。动作使用演出时钟，移动 0.3 秒，对白按文本长度显示。"
	page.add_child(replay_status)
	var columns := HSplitContainer.new()
	columns.size_flags_vertical = Control.SIZE_EXPAND_FILL
	page.add_child(columns)
	replay_board = ReplayBoard.new()
	replay_board.custom_minimum_size = Vector2(420, 260)
	replay_board.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	columns.add_child(replay_board)
	replay_text = _text_area(columns)

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
	command.grab_focus()

func close() -> void:
	panel.hide()
	engine.ReplayPause()
	_replay_playing = false
	set_process(false)
	_sampling_changed()

func is_open() -> bool:
	return is_instance_valid(panel) and panel.visible

func performance_open() -> bool:
	return is_open() and tabs.current_tab == 2

func _sampling_changed() -> void:
	engine.SetDiagnosticsEnabled(host.audit_enabled or performance_open())
	if not performance_open(): _phase_starts.clear()

func begin_shutdown() -> void:
	close()
	set_process_input(false)

func _process(_delta: float) -> void:
	var now := Time.get_ticks_usec()
	var seconds := float(now - _last_clock) / 1000000.0
	_last_clock = now
	if performance_open():
		frame_times.append(seconds * 1000.0)
		if frame_times.size() > FRAME_CAPACITY: frame_times.pop_front()
		graph.samples = frame_times
		graph.queue_redraw()
		_consume_timings()
		var total := 0.0
		var peak := 0.0
		for value: float in frame_times:
			total += value
			peak = maxf(peak, value)
		performance.text = "帧均值 %.2f ms  ·  峰值 %.2f ms\nPrepare %.2f ms  ·  Complete %.2f ms  ·  物理 %.2f ms" % [total / frame_times.size(), peak, float(_durations.get("prepare", 0.0)), float(_durations.get("complete", 0.0)), float(_durations.get("physical", 0.0))]
	if tabs.current_tab == 3:
		for frame: Dictionary in engine.ReplayAdvance(seconds): _show_frame(frame)
		replay_board.advance(seconds * _replay_speed if _replay_playing else 0.0)

func _consume_timings() -> void:
	for raw: String in engine.PollTimings():
		var row: Dictionary = JSON.parse_string(raw)
		var stage := str(row.stage)
		var family := ""
		if stage in ["prepare_begin", "prepare_end"]: family = "prepare"
		elif stage in ["complete_begin", "complete_end"]: family = "complete"
		elif stage in ["execution_begin", "execution_end"]: family = "physical"
		if family.is_empty(): continue
		var key := "%s:%s:%s" % [row.command_id, row.interaction_id, family]
		if family == "physical":
			var envelope: Dictionary = JSON.parse_string(str(row.details))
			key = "physical:" + str(envelope.get("details", {}).get("action_event", ""))
		if stage.ends_with("_begin"):
			_phase_starts[key] = float(row.real_us)
			if _phase_starts.size() > 64: _phase_starts.erase(_phase_starts.keys()[0])
		elif _phase_starts.has(key):
			_durations[family] = (float(row.real_us) - float(_phase_starts[key])) / 1000.0
			_phase_starts.erase(key)

func _command(line: String) -> void:
	var text := line.strip_edges()
	if text.is_empty(): return
	command.clear()
	_append("> " + text)
	var parts := text.split(" ", false, 1)
	var argument := str(parts[1]) if parts.size() > 1 else ""
	match parts[0]:
		"help": _append("status：本人投影\noptions：当前合法动作\nact <choice>：从普通空间交互执行动作\ninspect [actor]：客观 / 本人 / 规划\ntp <actor> <place>：开发位置编辑\ntime <minutes>：开发时间推进\naudit on|off：帧与输入取证\n关闭控制台后返回现场。")
		"status": _append(JSON.stringify(engine.Snapshot(), "  "))
		"options":
			for option: Dictionary in engine.Snapshot().get("pending", {}).get("options", []): _append("%s  %s" % [option.get("id", ""), option.get("label", "")])
		"act": host.console_action(argument)
		"inspect": engine.DevCommand(text)
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
			_append(str(event.get("message", "")))
			if tabs.current_tab == 3: replay_status.text = str(event.get("message", ""))
		"dev_inspect":
			var data: Dictionary = event.data
			physical.text = JSON.stringify(data.get("physical", {}), "  ")
			mind.text = JSON.stringify(data.get("mind", {}), "  ")
			planning.text = JSON.stringify({"planning": data.get("planning", {}), "scheduled_action": data.get("scheduled_action", false), "policy": data.get("policy", {}), "erosion": data.get("erosion", 0), "erosion_rank": data.get("erosion_rank", 0), "erosion_stage": data.get("erosion_stage", ""), "erosion_memory_truncated": data.get("erosion_memory_truncated", false), "actor": data.get("actor", "")}, "  ")
			tabs.current_tab = 1
		"replay_loaded":
			_replay_count = int(event.count)
			_replay_playing = false
			replay_board.reset()
			replay_status.text = "已载入 %d 个演出片段。" % _replay_count
			_show_frame(engine.ReplayStep())

func _show_frame(frame: Dictionary) -> void:
	if frame.is_empty(): return
	replay_status.text = "%d / %d  ·  %s  ·  %s  ·  %.2f 秒" % [int(frame.sequence) + 1, _replay_count, frame.kind, frame.actor, float(frame.duration)]
	replay_text.text = str(frame.text) + "\n\n" + JSON.stringify(frame.data, "  ")
	replay_board.present(frame, _replay_playing)

class FrameGraph extends Control:
	var samples: Array[float] = []
	func _draw() -> void:
		var ceiling := 33.4
		for value: float in samples: ceiling = maxf(ceiling, value)
		var plot := Rect2(Vector2(8, 16), size - Vector2(16, 32))
		var guide := plot.end.y - 16.7 / ceiling * plot.size.y
		draw_line(Vector2(plot.position.x, guide), Vector2(plot.end.x, guide), Color(0.5, 0.5, 0.5, 0.5))
		var points := PackedVector2Array()
		for index in range(samples.size()): points.append(Vector2(plot.position.x + plot.size.x * index / 119.0, plot.end.y - samples[index] / ceiling * plot.size.y))
		if points.size() > 1: draw_polyline(points, Color(0.3, 0.8, 0.6), 2.0)

class ReplayBoard extends Control:
	var map_id := ""
	var markers: Dictionary = {}
	var _from: Dictionary = {}
	var _to: Dictionary = {}
	var _elapsed := 0.0
	var _duration := 0.3
	func reset() -> void:
		map_id = ""
		markers.clear()
		_from.clear()
		_to.clear()
		queue_redraw()
	func present(frame: Dictionary, animate: bool) -> void:
		var data: Dictionary = frame.data
		var position: Dictionary = data.get("position", {})
		if position.is_empty(): return
		var next_map := str(position.get("map", ""))
		if map_id != next_map:
			markers.clear()
			map_id = next_map
		var room: Dictionary = GEOMETRY.room_data(map_id, str(position.get("room", "")))
		if room.is_empty(): return
		var raw: Array = room.rect
		var point := Vector2(float(raw[0]) + float(raw[2]) / 2.0, float(raw[1]) + float(raw[3]) / 2.0)
		var who := str(frame.actor)
		_from = {"actor": who, "point": markers.get(who, point)}
		_to = {"actor": who, "point": point}
		_duration = float(frame.duration)
		_elapsed = 0.0 if animate else _duration
		markers[who] = _from.point if animate else point
		queue_redraw()
	func advance(seconds: float) -> void:
		if _to.is_empty(): return
		_elapsed = minf(_duration, _elapsed + seconds)
		markers[str(_to.actor)] = Vector2(_from.point).lerp(Vector2(_to.point), _elapsed / _duration)
		queue_redraw()
	func _draw() -> void:
		var font := get_theme_default_font()
		draw_string(font, Vector2(12, 24), "记录位置示意 · " + map_id, HORIZONTAL_ALIGNMENT_LEFT, -1, 16)
		var rooms: Array = GEOMETRY.map_data(map_id).get("rooms", [])
		if rooms.is_empty(): return
		var bounds := Rect2()
		for room: Dictionary in rooms:
			var raw: Array = room.rect
			var rect := Rect2(float(raw[0]), float(raw[1]), float(raw[2]), float(raw[3]))
			bounds = rect if bounds.size == Vector2.ZERO else bounds.merge(rect)
		var scale_value := minf((size.x - 32.0) / bounds.size.x, (size.y - 64.0) / bounds.size.y)
		var offset := Vector2(16, 44) - bounds.position * scale_value
		for room: Dictionary in rooms:
			var raw: Array = room.rect
			var rect := Rect2(Vector2(float(raw[0]), float(raw[1])) * scale_value + offset, Vector2(float(raw[2]), float(raw[3])) * scale_value)
			draw_rect(rect, Color(0.4, 0.4, 0.4, 0.4), false, 1.0)
		for who in markers:
			var point: Vector2 = markers[who] * scale_value + offset
			draw_circle(point, 5.0, Color(0.95, 0.6, 0.3))
			draw_string(font, point + Vector2(8, -4), str(who), HORIZONTAL_ALIGNMENT_LEFT, -1, 13)
